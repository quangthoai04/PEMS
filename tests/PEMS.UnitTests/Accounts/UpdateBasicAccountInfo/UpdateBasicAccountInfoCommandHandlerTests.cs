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
            Email = "ho.actor@test.local",
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
        h.Db.Users.Add(Ho(ActorId, "ho.actor@test.local"));
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
        h.Db.Users.Add(Ho(810, "old.ho@test.local"));
        h.Db.UserAuthProviders.Add(Google(810, "old.ho@test.local"));
        h.Db.SaveChanges();

        var res = await h.Run(new UpdateBasicAccountInfoCommand
        {
            UserId = 810,
            FullName = "  Head Office Mới  ",
            Email = "  NEW.HO@Test.Local ",
        });

        Assert.True(res.EmailChanged);
        var user = await h.Db.Users.SingleAsync(u => u.UserId == 810);
        Assert.Equal("Head Office Mới", user.FullName);          // trimmed, casing preserved
        Assert.Equal("new.ho@test.local", user.Email);          // normalized lowercase
        Assert.Null(user.EmailVerifiedAt);                       // re-verify on next login

        var provider = await h.Db.UserAuthProviders.SingleAsync(p => p.UserId == 810);
        Assert.Equal("new.ho@test.local", provider.ProviderEmail);
        Assert.Null(provider.ProviderSubject);                   // SSO re-links next login

        Assert.Contains(h.Sessions.RevokeAllCalls,
            c => c.UserId == 810 && c.Reason == SessionRevokeReasons.AccountEmailChanged);
    }

    [Fact]
    public async Task HoEditsStaffLeader_FullNameOnly_NoSessionRevoke()
    {
        var h = CreateHarness();
        h.Db.Users.Add(StaffLeader(820, "leader@test.local"));
        h.Db.SaveChanges();

        var res = await h.Run(new UpdateBasicAccountInfoCommand
        {
            UserId = 820,
            FullName = "Trưởng phòng IC Mới",
            Email = "leader@test.local",   // unchanged
        });

        Assert.False(res.EmailChanged);
        Assert.Equal(0, res.RevokedSessions);
        Assert.Empty(h.Sessions.RevokeAllCalls);
        var user = await h.Db.Users.SingleAsync(u => u.UserId == 820);
        Assert.Equal("Trưởng phòng IC Mới", user.FullName);
        Assert.Equal("leader@test.local", user.Email);
    }

    [Fact]
    public async Task SameEmailDifferentCasing_IsNotAnEmailChange()
    {
        var h = CreateHarness();
        h.Db.Users.Add(StaffLeader(820, "leader@test.local"));
        h.Db.SaveChanges();

        var res = await h.Run(new UpdateBasicAccountInfoCommand
        {
            UserId = 820,
            FullName = "User 820",
            Email = "LEADER@Test.Local",
        });

        Assert.False(res.EmailChanged);
        Assert.Empty(h.Sessions.RevokeAllCalls);
    }

    [Fact]
    public async Task EmailChange_WritesAuditWithRelinkFlag()
    {
        var h = CreateHarness();
        h.Db.Users.Add(Ho(810, "old.ho@test.local"));
        h.Db.SaveChanges();

        await h.Run(new UpdateBasicAccountInfoCommand
        {
            UserId = 810, FullName = "User 810", Email = "brand.new@test.local",
        });

        var audit = await h.Db.AuditLogs.SingleAsync(a => a.EntityId == 810);
        Assert.Equal("UPDATE_ACCOUNT_BASIC_INFO", audit.Action);
        Assert.Equal(ActorId, audit.ActorUserId);
        var change = Assert.Single(audit.Changes);
        Assert.Contains("brand.new@test.local", change.NewValueText);
        Assert.Contains("authenticationRelinkRequired", change.NewValueText);
    }

    // ── Guards ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task HoCannotEditSelf()
    {
        var h = CreateHarness();
        await Assert.ThrowsAsync<ForbiddenException>(() => h.Run(new UpdateBasicAccountInfoCommand
        {
            UserId = ActorId, FullName = "Tôi", Email = "me.new@test.local",
        }));
    }

    [Fact]
    public async Task HoCannotEditLockedTarget()
    {
        var h = CreateHarness();
        h.Db.Users.Add(Ho(810, "locked.ho@test.local", status: UserStatuses.Locked));
        h.Db.SaveChanges();

        await Assert.ThrowsAsync<BusinessRuleException>(() => h.Run(new UpdateBasicAccountInfoCommand
        {
            UserId = 810, FullName = "X", Email = "locked.ho@test.local",
        }));
    }

    [Fact]
    public async Task HoCannotEditOutOfScopeRole()
    {
        var h = CreateHarness();
        var student = Uc106TestData.CreateUser(830, Uc106TestData.StudentRoleId, null, null, Campus);
        student.Email = "student@test.local";
        h.Db.Users.Add(student);
        h.Db.SaveChanges();

        await Assert.ThrowsAsync<ForbiddenException>(() => h.Run(new UpdateBasicAccountInfoCommand
        {
            UserId = 830, FullName = "SV", Email = "student@test.local",
        }));
    }

    [Fact]
    public async Task DuplicateEmail_Throws409Conflict()
    {
        var h = CreateHarness();
        h.Db.Users.Add(Ho(810, "target.ho@test.local"));
        h.Db.Users.Add(StaffLeader(820, "taken@test.local"));
        h.Db.SaveChanges();

        var ex = await Assert.ThrowsAsync<ConflictException>(() => h.Run(new UpdateBasicAccountInfoCommand
        {
            UserId = 810, FullName = "User 810", Email = "taken@test.local",
        }));
        Assert.Equal("EMAIL_ALREADY_EXISTS", ex.ErrorCode);
    }

    [Fact]
    public async Task NonHoCaller_IsForbidden()
    {
        var h = CreateHarness();
        h.Actor.RoleCode = RoleCodes.Staff;
        h.Actor.SubRole = UserSubRoles.Leader;
        h.Db.Users.Add(StaffLeader(820, "leader@test.local"));
        h.Db.SaveChanges();

        await Assert.ThrowsAsync<ForbiddenException>(() => h.Run(new UpdateBasicAccountInfoCommand
        {
            UserId = 820, FullName = "X", Email = "leader@test.local",
        }));
    }

    [Fact]
    public async Task BlankFullName_IsRejected()
    {
        var h = CreateHarness();
        h.Db.Users.Add(Ho(810, "target.ho@test.local"));
        h.Db.SaveChanges();

        await Assert.ThrowsAsync<ValidationException>(() => h.Run(new UpdateBasicAccountInfoCommand
        {
            UserId = 810, FullName = "   ", Email = "target.ho@test.local",
        }));
    }
}
