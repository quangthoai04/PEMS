using System.Linq;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Accounts.Commands.UpdateBasicAccountInfo;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Common.Security;
using PEMS.Application.Emails.Common;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Users;
using PEMS.UnitTests.TestInfrastructure;

namespace PEMS.UnitTests.Accounts.UpdateBasicAccountInfo;

/// <summary>
/// Unit tests for <see cref="UpdateBasicAccountInfoCommandHandler"/> — the HO "Chỉnh sửa thông tin"
/// flow (HO_BASIC_INFO spec §9/§10/§25.1). An HO edits ONLY the full name + email of another HO or a
/// Staff Leader; role/campus/department/status are never changed. Runs on EF InMemory: the caller is
/// an HO (id 800, campus 1). Covers scope guards, validation, email uniqueness, provider re-point +
/// session revoke on email change, and the audit entry.
/// </summary>
public class UpdateBasicAccountInfoCommandHandlerTests
{
    private const ulong Campus = Uc106TestData.CampusId;   // 1
    private const ulong HoRoleId = 2;
    private const ulong ActorId = 800;

    private sealed class Harness
    {
        public TestApplicationDbContext Db { get; } = TestApplicationDbContext.Create();
        public FakeCurrentUserService Actor { get; } = new()
        {
            UserId = ActorId,
            Email = "ho.actor@fpt.edu.vn",
            RoleId = HoRoleId,
            RoleCode = RoleCodes.Ho,
            SubRole = null,
            PrimaryCampusId = Campus,
        };
        public FakeDateTimeService Clock { get; } = new();
        public RecordingSessionService Sessions { get; }
        public FakeSystemEmailDispatcher Dispatcher { get; } = new();
        public UpdateBasicAccountInfoCommandHandler Handler { get; }

        /// <summary>Addresses whose send should fail (simulates an SMTP failure). Empty = all succeed.</summary>
        public HashSet<string> FailFor { get; } = new(StringComparer.OrdinalIgnoreCase);

        public Harness()
        {
            Sessions = new RecordingSessionService(Db);
            Dispatcher.OutcomeFor = r => FailFor.Contains(r.To.Email)
                ? EmailDeliveryResult.Failed("SMTP_SEND_FAILED", "SMTP down")
                : EmailDeliveryResult.Sent();
            Handler = new UpdateBasicAccountInfoCommandHandler(
                Db, Actor, new RoleAccessPolicy(), Sessions, Clock, Dispatcher);
        }

        public List<SystemEmailRequest> Sent => Dispatcher.Sent;

        public SystemEmailRequest SentTo(string email)
            => Sent.Single(m => string.Equals(m.To.Email, email, StringComparison.OrdinalIgnoreCase));

        public Task<UpdateBasicAccountInfoResponse> Run(UpdateBasicAccountInfoCommand cmd)
            => Handler.Handle(cmd, CancellationToken.None);
    }

    private static Harness CreateHarness()
    {
        var h = new Harness();
        h.Db.Campuses.Add(Uc106TestData.CreateCampus());
        h.Db.Roles.AddRange(
            Uc106TestData.CreateRole(HoRoleId, RoleCodes.Ho),
            Uc106TestData.CreateRole(Uc106TestData.StaffRoleId, RoleCodes.Staff),
            Uc106TestData.CreateRole(Uc106TestData.StudentRoleId, RoleCodes.Student));
        // The HO caller themselves (self-guard tests).
        h.Db.Users.Add(Ho(ActorId, "ho.actor@fpt.edu.vn"));
        h.Db.SaveChanges();
        return h;
    }

    private static User Ho(ulong id, string email, string status = EntityStatuses.Active)
    {
        var u = Uc106TestData.CreateUser(id, HoRoleId, null, null, Campus);
        u.Email = email;
        u.Status = status;
        u.EmailVerifiedAt = new DateTime(2026, 1, 1);
        return u;
    }

    private static User StaffLeader(ulong id, string email, ulong campus = Campus, string status = EntityStatuses.Active)
    {
        var u = Uc106TestData.CreateUser(id, Uc106TestData.StaffRoleId, UserSubRoles.Leader, 50, campus);
        u.Email = email;
        u.Status = status;
        return u;
    }

    private static UserAuthProvider Google(ulong userId, string email) => new()
    {
        UserId = userId,
        ProviderType = ProviderTypes.GoogleSso,
        ProviderSubject = "google-subject-123",
        ProviderEmail = email,
        IsEnabled = true,
        LinkedAt = new DateTime(2026, 1, 1),
    };

    private static UserAuthProvider LocalPassword(ulong userId, string email) => new()
    {
        UserId = userId,
        ProviderType = ProviderTypes.LocalPassword,
        ProviderEmail = email,
        IsEnabled = true,
        LinkedAt = new DateTime(2026, 1, 1),
    };

    // ── Success paths ─────────────────────────────────────────────────────────

    [Fact]
    public async Task HoEditsAnotherHo_ChangesEmail_UnlinksSsoProvider_RevokesSessions()
    {
        var h = CreateHarness();
        h.Db.Users.Add(Ho(810, "old.ho@fpt.edu.vn"));
        h.Db.UserAuthProviders.Add(Google(810, "old.ho@fpt.edu.vn"));
        h.Db.SaveChanges();

        var res = await h.Run(new UpdateBasicAccountInfoCommand
        {
            UserId = 810,
            FullName = "  Head Office Mới  ",
            Email = "  NEW.HO@FPT.EDU.VN ",
        });

        Assert.True(res.EmailChanged);
        var user = await h.Db.Users.SingleAsync(u => u.UserId == 810);
        Assert.Equal("Head Office Mới", user.FullName);          // trimmed, casing preserved
        Assert.Equal("new.ho@fpt.edu.vn", user.Email);          // normalized lowercase
        Assert.Null(user.EmailVerifiedAt);                       // re-verify on next login

        // The Google row is DELETED, not blanked: a subject-less SSO row is rejected by the MySQL
        // trigger trg_auth_providers_validate_bu, and login re-creates the link on next sign-in.
        Assert.False(await h.Db.UserAuthProviders.AnyAsync(p => p.UserId == 810));

        Assert.Contains(h.Sessions.RevokeAllCalls,
            c => c.UserId == 810 && c.Reason == SessionRevokeReasons.AccountEmailChanged);
    }

    [Fact]
    public async Task HoEditsAnotherHo_ChangesEmail_KeepsLocalPasswordProvider_RepointedAtNewEmail()
    {
        var h = CreateHarness();
        h.Db.Users.Add(Ho(811, "old.local@fpt.edu.vn"));
        h.Db.UserAuthProviders.Add(LocalPassword(811, "old.local@fpt.edu.vn"));
        h.Db.SaveChanges();

        await h.Run(new UpdateBasicAccountInfoCommand
        {
            UserId = 811,
            FullName = "Head Office Local",
            Email = "new.local@fpt.edu.vn",
        });

        // LOCAL_PASSWORD carries no external subject, so it survives the email change and simply
        // follows the account to the new address.
        var provider = await h.Db.UserAuthProviders.SingleAsync(p => p.UserId == 811);
        Assert.Equal(ProviderTypes.LocalPassword, provider.ProviderType);
        Assert.Equal("new.local@fpt.edu.vn", provider.ProviderEmail);
    }

    [Fact]
    public async Task HoEditsStaffLeader_FullNameOnly_NoSessionRevoke()
    {
        var h = CreateHarness();
        h.Db.Users.Add(StaffLeader(820, "leader@fpt.edu.vn"));
        h.Db.SaveChanges();

        var res = await h.Run(new UpdateBasicAccountInfoCommand
        {
            UserId = 820,
            FullName = "Trưởng phòng IC Mới",
            Email = "leader@fpt.edu.vn",   // unchanged
        });

        Assert.False(res.EmailChanged);
        Assert.Equal(0, res.RevokedSessions);
        Assert.Empty(h.Sessions.RevokeAllCalls);
        var user = await h.Db.Users.SingleAsync(u => u.UserId == 820);
        Assert.Equal("Trưởng phòng IC Mới", user.FullName);
        Assert.Equal("leader@fpt.edu.vn", user.Email);
    }

    [Fact]
    public async Task SameEmailDifferentCasing_IsNotAnEmailChange()
    {
        var h = CreateHarness();
        h.Db.Users.Add(StaffLeader(820, "leader@fpt.edu.vn"));
        h.Db.SaveChanges();

        var res = await h.Run(new UpdateBasicAccountInfoCommand
        {
            UserId = 820,
            FullName = "Trần Thị Tám Hai",
            Email = "LEADER@FPT.EDU.VN",
        });

        Assert.False(res.EmailChanged);
        Assert.Empty(h.Sessions.RevokeAllCalls);
    }

    [Fact]
    public async Task EmailChange_WritesAuditWithRelinkFlag()
    {
        var h = CreateHarness();
        h.Db.Users.Add(Ho(810, "old.ho@fpt.edu.vn"));
        h.Db.SaveChanges();

        await h.Run(new UpdateBasicAccountInfoCommand
        {
            UserId = 810, FullName = "Nguyễn Văn Tám Mười", Email = "brand.new@fpt.edu.vn",
        });

        var audit = await h.Db.AuditLogs.SingleAsync(a => a.EntityId == 810);
        Assert.Equal("UPDATE_ACCOUNT_BASIC_INFO", audit.Action);
        Assert.Equal(ActorId, audit.ActorUserId);
        var change = Assert.Single(audit.Changes);
        Assert.Contains("brand.new@fpt.edu.vn", change.NewValueText);
        Assert.Contains("authenticationRelinkRequired", change.NewValueText);
    }

    // ── Email change notifications (spec §3/§4 — old-address privacy, new-address snapshot) ──

    /// <summary>Adds a Staff Leader (id 830) whose department has a real, asserted name.</summary>
    private static User StaffLeaderWithDepartment(Harness h, ulong id, string email, string departmentName)
    {
        var department = Uc106TestData.CreateGeneralDepartment(departmentId: 50);
        department.Name = departmentName;
        h.Db.Departments.Add(department);
        var u = StaffLeader(id, email);
        return u;
    }

    [Fact]
    public async Task EmailChange_OldAddressMail_IsAnonymous_LeaksNothingAboutTheAccount()
    {
        var h = CreateHarness();
        h.Db.Users.Add(StaffLeaderWithDepartment(h, 830, "old.leader@fpt.edu.vn", "Phòng Hợp tác Quốc tế"));
        h.Db.UserAuthProviders.Add(Google(830, "old.leader@fpt.edu.vn"));
        h.Db.SaveChanges();

        await h.Run(new UpdateBasicAccountInfoCommand
        {
            UserId = 830,
            FullName = "Trần Văn Lãnh Đạo",
            Email = "new.leader@fpt.edu.vn",
        });

        // The address being unlinked may belong to somebody with no connection to the account, so the
        // notice is anonymous BY CONSTRUCTION: the template declares no variables and the handler passes
        // none, so there is no holder name, no new address and no role/campus/department to leak — not
        // even a display name on the envelope. (The wording itself lives in the template; that it renders
        // with nothing unresolved is proven in the renderer + seed-contract integration tests.)
        var mail = h.SentTo("old.leader@fpt.edu.vn");
        Assert.Equal(SystemEmailTemplates.AccountEmailChangedOldNotice, mail.TemplateCode);
        Assert.Empty(mail.Variables);
        Assert.Null(mail.TrustedBlocks);
        Assert.Null(mail.To.DisplayName);
    }

    [Fact]
    public async Task EmailChange_NewAddressMail_NamesTheHolder_AndMasksTheOldAddress()
    {
        var h = CreateHarness();
        h.Db.Users.Add(StaffLeaderWithDepartment(h, 830, "old.leader@fpt.edu.vn", "Phòng IC"));
        h.Db.SaveChanges();

        await h.Run(new UpdateBasicAccountInfoCommand
        {
            UserId = 830,
            FullName = "Nguyễn Thị Trưởng Phòng",
            Email = "leader.new@fpt.edu.vn",
        });

        var mail = h.SentTo("leader.new@fpt.edu.vn");
        Assert.Equal(SystemEmailTemplates.AccountEmailChangedNewNotice, mail.TemplateCode);
        Assert.Equal("Nguyễn Thị Trưởng Phòng", mail.Variables["fullName"]);
        // The holder is entitled to know WHICH address was unlinked, but the mail carries only enough of
        // it to recognise — never the address in full.
        Assert.Equal("ol***@fpt.edu.vn", mail.Variables["oldEmailMasked"]);
        Assert.DoesNotContain(mail.Variables.Values, v => v.Contains("old.leader@fpt.edu.vn"));
    }

    [Fact]
    public async Task EmailChange_SendsExactlyTwoMessages_OneToEachAddress()
    {
        var h = CreateHarness();
        h.Db.Users.Add(Ho(840, "old.ho2@fpt.edu.vn"));
        h.Db.SaveChanges();

        await h.Run(new UpdateBasicAccountInfoCommand
        {
            UserId = 840,
            FullName = "Head Office Người",
            Email = "ho2.new@fpt.edu.vn",
        });

        // One message per person — never one message addressing both, which would hand each of them the
        // other's address.
        Assert.Equal(2, h.Sent.Count);
        Assert.Equal(
            new[] { "ho2.new@fpt.edu.vn", "old.ho2@fpt.edu.vn" },
            h.Sent.Select(m => m.To.Email).OrderBy(e => e, StringComparer.Ordinal).ToArray());
    }

    [Theory]
    // (oldFails, newFails) -> expected EmailNotificationStatus
    [InlineData(false, false, "SENT")]
    [InlineData(true, false, "PARTIAL")]
    [InlineData(false, true, "PARTIAL")]
    [InlineData(true, true, "FAILED")]
    public async Task EmailChange_SendFailures_YieldStatus_ButAccountStillSaved(
        bool oldFails, bool newFails, string expectedStatus)
    {
        var h = CreateHarness();
        h.Db.Users.Add(Ho(850, "old.status@fpt.edu.vn"));
        h.Db.SaveChanges();
        if (oldFails) h.FailFor.Add("old.status@fpt.edu.vn");
        if (newFails) h.FailFor.Add("new.status@fpt.edu.vn");

        var res = await h.Run(new UpdateBasicAccountInfoCommand
        {
            UserId = 850,
            FullName = "Head Office Status",
            Email = "new.status@fpt.edu.vn",
        });

        Assert.Equal(expectedStatus, res.EmailNotificationStatus);
        // Both addresses are always attempted — a first-send failure never skips the second.
        Assert.Equal(2, h.Sent.Count);
        // The account is committed regardless of email outcome.
        var user = await h.Db.Users.SingleAsync(u => u.UserId == 850);
        Assert.Equal("new.status@fpt.edu.vn", user.Email);
        Assert.Equal("Head Office Status", user.FullName);
    }

    [Fact]
    public async Task EmailChange_PassesVariablesRaw_LeavingTheEncodingToTheRenderer()
    {
        // Escaping now happens in exactly ONE place — the renderer HTML-encodes every variable value
        // before substituting it (proven against a real template in EmailTemplateRendererTests). A
        // handler that pre-encoded here would double-escape, so the contract is: pass the value as it
        // is in the database and let the renderer do its job.
        var h = CreateHarness();
        h.Db.Users.Add(Ho(860, "r&d.old@fpt.edu.vn"));
        h.Db.SaveChanges();

        await h.Run(new UpdateBasicAccountInfoCommand
        {
            UserId = 860,
            FullName = "Lê Văn Ánh",
            Email = "encode.new@fpt.edu.vn",
        });

        var mail = h.SentTo("encode.new@fpt.edu.vn");
        Assert.Equal("Lê Văn Ánh", mail.Variables["fullName"]);           // not "L&#234;..."
        Assert.Equal("r&***@fpt.edu.vn", mail.Variables["oldEmailMasked"]); // not "r&amp;***@..."
    }

    // ── Guards ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task HoCannotEditSelf()
    {
        var h = CreateHarness();
        await Assert.ThrowsAsync<ForbiddenException>(() => h.Run(new UpdateBasicAccountInfoCommand
        {
            UserId = ActorId, FullName = "Tôi", Email = "me.new@fpt.edu.vn",
        }));
    }

    [Fact]
    public async Task HoCannotEditLockedTarget()
    {
        var h = CreateHarness();
        h.Db.Users.Add(Ho(810, "locked.ho@fpt.edu.vn", status: UserStatuses.Locked));
        h.Db.SaveChanges();

        await Assert.ThrowsAsync<BusinessRuleException>(() => h.Run(new UpdateBasicAccountInfoCommand
        {
            UserId = 810, FullName = "X", Email = "locked.ho@fpt.edu.vn",
        }));
    }

    [Fact]
    public async Task HoCannotEditOutOfScopeRole()
    {
        var h = CreateHarness();
        var student = Uc106TestData.CreateUser(830, Uc106TestData.StudentRoleId, null, null, Campus);
        student.Email = "student@fpt.edu.vn";
        h.Db.Users.Add(student);
        h.Db.SaveChanges();

        await Assert.ThrowsAsync<ForbiddenException>(() => h.Run(new UpdateBasicAccountInfoCommand
        {
            UserId = 830, FullName = "SV", Email = "student@fpt.edu.vn",
        }));
    }

    [Fact]
    public async Task DuplicateEmail_Throws409Conflict()
    {
        var h = CreateHarness();
        h.Db.Users.Add(Ho(810, "target.ho@fpt.edu.vn"));
        h.Db.Users.Add(StaffLeader(820, "taken@fpt.edu.vn"));
        h.Db.SaveChanges();

        var ex = await Assert.ThrowsAsync<ConflictException>(() => h.Run(new UpdateBasicAccountInfoCommand
        {
            UserId = 810, FullName = "Nguyễn Văn Tám Mười", Email = "taken@fpt.edu.vn",
        }));
        Assert.Equal("EMAIL_ALREADY_EXISTS", ex.ErrorCode);
    }

    [Fact]
    public async Task NonHoCaller_IsForbidden()
    {
        var h = CreateHarness();
        h.Actor.RoleCode = RoleCodes.Staff;
        h.Actor.SubRole = UserSubRoles.Leader;
        h.Db.Users.Add(StaffLeader(820, "leader@fpt.edu.vn"));
        h.Db.SaveChanges();

        await Assert.ThrowsAsync<ForbiddenException>(() => h.Run(new UpdateBasicAccountInfoCommand
        {
            UserId = 820, FullName = "X", Email = "leader@fpt.edu.vn",
        }));
    }

    [Fact]
    public async Task BlankFullName_IsRejected()
    {
        var h = CreateHarness();
        h.Db.Users.Add(Ho(810, "target.ho@fpt.edu.vn"));
        h.Db.SaveChanges();

        await Assert.ThrowsAsync<ValidationException>(() => h.Run(new UpdateBasicAccountInfoCommand
        {
            UserId = 810, FullName = "   ", Email = "target.ho@fpt.edu.vn",
        }));
    }

    // ── Shared identity rules (IDENTITY_VALIDATION spec §6.2 / §10.2, AC-04/AC-10) ──
    // Reached through the handler directly, i.e. exactly like a client bypassing the modal.

    [Theory]
    [InlineData("Nguyễn Văn Tám Mười", "target@yahoo.com")]              // domain not allowed
    [InlineData("Nguyễn Văn Tám Mười", "target@fe.edu.vn")]              // dropped from the allowlist
    [InlineData("Nguyễn Văn Tám Mười", "target@student.fpt.edu.vn")]     // subdomain, not exact
    [InlineData("Nguyễn Văn Tám Mười", "target@gmail.com.vn")]           // look-alike, not exact
    [InlineData("Nguyễn Văn Tám Mười", "target+test@gmail.com")]         // plus addressing
    [InlineData("Nguyễn Văn Tám Mười", "abc..def@gmail.com")]            // malformed local-part
    [InlineData("Nguyễn Văn 123", "target.ho@fpt.edu.vn")]               // digits in the name
    [InlineData("A", "target.ho@fpt.edu.vn")]                            // name too short
    public async Task InvalidIdentity_IsRejected_AndNothingIsChanged(string fullName, string email)
    {
        var h = CreateHarness();
        h.Db.Users.Add(Ho(810, "target.ho@fpt.edu.vn"));
        h.Db.SaveChanges();

        await Assert.ThrowsAsync<ValidationException>(() => h.Run(new UpdateBasicAccountInfoCommand
        {
            UserId = 810, FullName = fullName, Email = email,
        }));

        // The rejection happens before any mutation: the address stands, no session is cut, nobody
        // is notified and no success audit is written.
        var user = await h.Db.Users.SingleAsync(u => u.UserId == 810);
        Assert.Equal("target.ho@fpt.edu.vn", user.Email);
        Assert.Empty(h.Sessions.RevokeAllCalls);
        Assert.Empty(h.Sent);
        Assert.False(await h.Db.AuditLogs.AnyAsync());
    }

    /// <summary>Re-saving the target's own email must not trip the uniqueness check (§5.6).</summary>
    [Fact]
    public async Task OwnEmail_IsExcludedFromTheUniquenessCheck()
    {
        var h = CreateHarness();
        h.Db.Users.Add(Ho(810, "target.ho@fpt.edu.vn"));
        h.Db.SaveChanges();

        var res = await h.Run(new UpdateBasicAccountInfoCommand
        {
            UserId = 810, FullName = "Tên Mới Hợp Lệ", Email = "target.ho@fpt.edu.vn",
        });

        Assert.False(res.EmailChanged);
        Assert.Equal("Tên Mới Hợp Lệ", (await h.Db.Users.SingleAsync(u => u.UserId == 810)).FullName);
    }

    [Fact]
    public async Task DuplicateEmail_IsDetectedCaseInsensitively()
    {
        var h = CreateHarness();
        h.Db.Users.Add(Ho(810, "target.ho@fpt.edu.vn"));
        h.Db.Users.Add(StaffLeader(820, "taken@fpt.edu.vn"));
        h.Db.SaveChanges();

        var ex = await Assert.ThrowsAsync<ConflictException>(() => h.Run(new UpdateBasicAccountInfoCommand
        {
            UserId = 810, FullName = "Nguyễn Văn Tám Mười", Email = "  TAKEN@FPT.EDU.VN  ",
        }));
        Assert.Equal("EMAIL_ALREADY_EXISTS", ex.ErrorCode);
    }
}
