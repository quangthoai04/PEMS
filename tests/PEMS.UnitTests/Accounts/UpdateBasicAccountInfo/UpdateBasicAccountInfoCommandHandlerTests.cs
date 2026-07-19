using Microsoft.EntityFrameworkCore;
using Moq;
using PEMS.Application.Accounts.Commands.UpdateBasicAccountInfo;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Common.Security;
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
        public Mock<IEmailService> Email { get; } = new();
        public UpdateBasicAccountInfoCommandHandler Handler { get; }

        public Harness()
        {
            Sessions = new RecordingSessionService(Db);
            Email.Setup(e => e.SendAsync(
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            Handler = new UpdateBasicAccountInfoCommandHandler(
                Db, Actor, new RoleAccessPolicy(), Sessions, Clock, Email.Object);
        }

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

    // ── Success paths ─────────────────────────────────────────────────────────

    [Fact]
    public async Task HoEditsAnotherHo_ChangesEmail_RepointsProvider_RevokesSessions()
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

        var provider = await h.Db.UserAuthProviders.SingleAsync(p => p.UserId == 810);
        Assert.Equal("new.ho@fpt.edu.vn", provider.ProviderEmail);
        Assert.Null(provider.ProviderSubject);                   // SSO re-links next login

        Assert.Contains(h.Sessions.RevokeAllCalls,
            c => c.UserId == 810 && c.Reason == SessionRevokeReasons.AccountEmailChanged);
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
    [InlineData("Nguyễn Văn Tám Mười", "target@student.fpt.edu.vn")]     // subdomain, not exact
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

        var user = await h.Db.Users.SingleAsync(u => u.UserId == 810);
        Assert.Equal("target.ho@fpt.edu.vn", user.Email);
        Assert.Empty(h.Sessions.RevokeAllCalls);
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
