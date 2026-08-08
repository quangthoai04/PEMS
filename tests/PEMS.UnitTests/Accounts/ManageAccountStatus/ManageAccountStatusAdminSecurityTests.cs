using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Moq;
using PEMS.Application.Accounts.Commands.ManageAccountStatus;
using PEMS.Application.Accounts.Common;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Notifications.Common;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Users;
using PEMS.UnitTests.TestInfrastructure;

namespace PEMS.UnitTests.Accounts.ManageAccountStatus;

/// <summary>
/// ADMIN security control over <see cref="ManageAccountStatusCommandHandler"/> —
/// ADMIN_ACCOUNT_MANAGEMENT spec §4/§16.1/§17-§21.
///
/// <para>
/// ADMIN is Global Read + Security Control: it may move an account ACTIVE → LOCKED and LOCKED →
/// ACTIVE, with a reason, never its own. Everything else on this endpoint — ACTIVE ↔ INACTIVE, and
/// any transition out of INACTIVE / PENDING_EMAIL_CONFIRMATION — is personnel management and belongs
/// to HO / Staff Leader. The refusals matter as much as the two allowed moves: they are what keeps
/// "security state" and "business status" from collapsing into one toggle with two owners.
/// </para>
/// </summary>
public class ManageAccountStatusAdminSecurityTests
{
    private const ulong Campus = Uc106TestData.CampusId;   // 1
    private const ulong AdminRoleId = 1;
    private const ulong HoRoleId = 2;
    private const ulong ActorId = 700;
    private const ulong TargetId = 810;
    private const string Reason = "Nghi ngờ tài khoản bị xâm nhập";

    private sealed class Harness
    {
        public TestApplicationDbContext Db { get; } = TestApplicationDbContext.Create();
        public FakeCurrentUserService Actor { get; } = new()
        {
            UserId = ActorId, RoleCode = RoleCodes.Admin, SubRole = null, RoleId = AdminRoleId,
            PrimaryCampusId = Campus,
        };
        public FakeDateTimeService Clock { get; } = new();
        public RecordingSessionService Sessions { get; }
        public Mock<ISecurityAuditService> Audit { get; } = new();
        public Mock<INotificationService> Notifications { get; } = new();
        public ManageAccountStatusCommandHandler Handler { get; }

        public Harness()
        {
            Sessions = new RecordingSessionService(Db);
            Notifications
                .Setup(n => n.CreateAsync(It.IsAny<CreateNotificationRequest>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            Handler = new ManageAccountStatusCommandHandler(
                Db, Actor, Sessions, Audit.Object, Clock, Notifications.Object);
        }

        public Task<ManageAccountStatusResponse> Run(ManageAccountStatusCommand cmd)
            => Handler.Handle(cmd, CancellationToken.None);

        /// <summary>A refused request must leave the account and its sessions byte-for-byte unchanged.</summary>
        public async Task AssertNothingHappened(string expectedStatus)
        {
            var user = await Db.Users.AsNoTracking().SingleAsync(u => u.UserId == TargetId);
            Assert.Equal(expectedStatus, user.Status);
            Assert.Empty(Sessions.RevokeAllCalls);
            Assert.Empty(Db.AuditLogs);
            Notifications.Verify(
                n => n.CreateAsync(It.IsAny<CreateNotificationRequest>(), It.IsAny<CancellationToken>()),
                Times.Never);
        }
    }

    private static Harness CreateHarness(string targetStatus = EntityStatuses.Active, ulong targetCampus = 2)
    {
        var h = new Harness();
        h.Db.Campuses.AddRange(Uc106TestData.CreateCampus(), Uc106TestData.CreateCampus(2));
        h.Db.Roles.AddRange(
            Uc106TestData.CreateRole(AdminRoleId, RoleCodes.Admin),
            Uc106TestData.CreateRole(HoRoleId, RoleCodes.Ho),
            Uc106TestData.CreateRole(Uc106TestData.StaffRoleId, RoleCodes.Staff),
            Uc106TestData.CreateRole(Uc106TestData.StudentRoleId, RoleCodes.Student));

        var admin = Uc106TestData.CreateUser(ActorId, AdminRoleId, null, null, Campus);
        h.Db.Users.Add(admin);

        // Deliberately on ANOTHER campus: ADMIN's authority here is system-wide, and a campus-scoped
        // fixture would let a campus check pass for the wrong reason.
        var target = Uc106TestData.CreateUser(TargetId, Uc106TestData.StudentRoleId, null, null, targetCampus);
        target.Status = targetStatus;
        h.Db.Users.Add(target);
        h.Db.SaveChanges();
        return h;
    }

    private static ManageAccountStatusCommand Cmd(string status, string? reason = Reason) =>
        new() { UserId = TargetId, Status = status, Reason = reason };

    // ── Security LOCK ───────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ActiveTarget_IsLocked_SessionsRevoked_AndAuditedAsSecurityLock()
    {
        var h = CreateHarness();
        h.Db.UserSessions.AddRange(
            Uc106TestData.CreateActiveSession(1, TargetId),
            Uc106TestData.CreateActiveSession(2, TargetId));
        h.Db.SaveChanges();

        var res = await h.Run(Cmd(UserStatuses.Locked));

        Assert.Equal(UserStatuses.Locked, res.Status);
        Assert.Equal(2, res.RevokedSessions);

        var user = await h.Db.Users.AsNoTracking().SingleAsync(u => u.UserId == TargetId);
        Assert.Equal(UserStatuses.Locked, user.Status);
        Assert.Equal(ActorId, user.UpdatedBy);
        Assert.Equal(h.Clock.VietnamNow, user.UpdatedAt);

        // Every live session is gone, and says why — a security lock, not a deactivation.
        Assert.Contains(h.Sessions.RevokeAllCalls,
            c => c.UserId == TargetId && c.Reason == SessionRevokeReasons.AccountSecurityLocked);
        Assert.Empty(await h.Db.UserSessions.AsNoTracking()
            .Where(s => s.UserId == TargetId && s.RevokedAt == null).ToListAsync());

        var audit = Assert.Single(h.Db.AuditLogs.Local);
        Assert.Equal("SECURITY_LOCK_ACCOUNT", audit.Action);
        Assert.Equal(ActorId, audit.ActorUserId);
        Assert.Equal(TargetId, audit.EntityId);
        var change = Assert.Single(audit.Changes);
        Assert.Equal(UserStatuses.Active, change.OldValueText);
        // Read as JSON, not as a substring: the serializer escapes non-ASCII, so a raw Contains on a
        // Vietnamese reason fails for a reason that has nothing to do with the audit being right.
        using var recorded = JsonDocument.Parse(change.NewValueText!);
        Assert.Equal(UserStatuses.Locked, recorded.RootElement.GetProperty("status").GetString());
        Assert.Equal(Reason, recorded.RootElement.GetProperty("reason").GetString());
    }

    [Fact]
    public async Task Lock_WithoutReason_IsRefused_AndChangesNothing()
    {
        var h = CreateHarness();

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            h.Run(Cmd(UserStatuses.Locked, reason: "   ")));

        Assert.Equal(AccountErrorCodes.SecurityReasonRequired, ex.ErrorCode);
        await h.AssertNothingHappened(UserStatuses.Active);
    }

    [Fact]
    public async Task AdminCannotLockOwnAccount()
    {
        var h = CreateHarness();

        var ex = await Assert.ThrowsAsync<AuthBusinessException>(() =>
            h.Handler.Handle(new ManageAccountStatusCommand
            {
                UserId = ActorId, Status = UserStatuses.Locked, Reason = Reason,
            }, CancellationToken.None));

        Assert.Equal(AccountErrorCodes.AccountSecuritySelfActionForbidden, ex.ErrorCode);
        Assert.Equal(403, ex.StatusCode);
        var admin = await h.Db.Users.AsNoTracking().SingleAsync(u => u.UserId == ActorId);
        Assert.Equal(EntityStatuses.Active, admin.Status);
        Assert.Empty(h.Sessions.RevokeAllCalls);
    }

    [Fact]
    public async Task InactiveTarget_CannotBeSecurityLocked()
    {
        // An account that already cannot sign in needs no security lock stacked on top — and refusing
        // it is what removes any need for a status_before_lock column.
        var h = CreateHarness(UserStatuses.Inactive);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => h.Run(Cmd(UserStatuses.Locked)));

        Assert.Equal(AccountErrorCodes.SecurityLockInvalidState, ex.ErrorCode);
        await h.AssertNothingHappened(UserStatuses.Inactive);
    }

    [Fact]
    public async Task PendingEmailConfirmationTarget_CannotBeSecurityLocked()
    {
        var h = CreateHarness(UserStatuses.PendingEmailConfirmation);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => h.Run(Cmd(UserStatuses.Locked)));

        Assert.Equal(AccountErrorCodes.SecurityLockInvalidState, ex.ErrorCode);
        await h.AssertNothingHappened(UserStatuses.PendingEmailConfirmation);
    }

    // ── Security UNLOCK ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task LockedTarget_IsUnlocked_LockoutCountersCleared_AndAuditedAsSecurityUnlock()
    {
        var h = CreateHarness(UserStatuses.Locked);
        var target = await h.Db.Users.SingleAsync(u => u.UserId == TargetId);
        target.FailedLoginCount = 7;
        target.LockedUntil = new DateTime(2026, 9, 1);
        // A session revoked by the earlier lock. Unlocking must NOT bring it back.
        var deadSession = Uc106TestData.CreateActiveSession(9, TargetId);
        deadSession.RevokedAt = new DateTime(2026, 7, 1);
        deadSession.RevokedReason = SessionRevokeReasons.AccountSecurityLocked;
        h.Db.UserSessions.Add(deadSession);
        h.Db.SaveChanges();

        var res = await h.Run(Cmd(UserStatuses.Active, "Điều tra hoàn tất"));

        Assert.Equal(UserStatuses.Active, res.Status);
        Assert.Equal(0, res.RevokedSessions);

        var user = await h.Db.Users.AsNoTracking().SingleAsync(u => u.UserId == TargetId);
        Assert.Equal(UserStatuses.Active, user.Status);
        Assert.Equal(0, user.FailedLoginCount);
        Assert.Null(user.LockedUntil);

        // The old session stays dead: the holder signs in again, they are not handed their pre-lock
        // tokens back.
        var session = await h.Db.UserSessions.AsNoTracking().SingleAsync(s => s.SessionId == 9);
        Assert.NotNull(session.RevokedAt);
        Assert.Empty(h.Sessions.RevokeAllCalls);

        var audit = Assert.Single(h.Db.AuditLogs.Local);
        Assert.Equal("SECURITY_UNLOCK_ACCOUNT", audit.Action);
        Assert.Equal(UserStatuses.Locked, Assert.Single(audit.Changes).OldValueText);
    }

    [Fact]
    public async Task Unlock_WithoutReason_IsRefused_AndChangesNothing()
    {
        var h = CreateHarness(UserStatuses.Locked);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            h.Run(Cmd(UserStatuses.Active, reason: null)));

        Assert.Equal(AccountErrorCodes.SecurityReasonRequired, ex.ErrorCode);
        await h.AssertNothingHappened(UserStatuses.Locked);
    }

    [Fact]
    public async Task AdminCannotUnlockOwnAccount()
    {
        var h = CreateHarness();
        var admin = await h.Db.Users.SingleAsync(u => u.UserId == ActorId);
        admin.Status = UserStatuses.Locked;
        h.Db.SaveChanges();

        var ex = await Assert.ThrowsAsync<AuthBusinessException>(() =>
            h.Handler.Handle(new ManageAccountStatusCommand
            {
                UserId = ActorId, Status = UserStatuses.Active, Reason = "Đã xác minh chủ tài khoản",
            }, CancellationToken.None));

        Assert.Equal(AccountErrorCodes.AccountSecuritySelfActionForbidden, ex.ErrorCode);
    }

    // ── Business status is NOT ADMIN's ──────────────────────────────────────────────────────────

    [Fact]
    public async Task AdminCannotDeactivateAnAccount()
    {
        var h = CreateHarness();

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => h.Run(Cmd(UserStatuses.Inactive)));

        Assert.Equal(AccountErrorCodes.AdminBusinessStatusChangeNotAllowed, ex.ErrorCode);
        await h.AssertNothingHappened(UserStatuses.Active);
    }

    [Fact]
    public async Task AdminCannotReactivateAnInactiveAccount()
    {
        // INACTIVE → ACTIVE is "this person is back", a personnel decision. Only LOCKED → ACTIVE is a
        // security unlock, so the refusal names the business boundary rather than an invalid state.
        var h = CreateHarness(UserStatuses.Inactive);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => h.Run(Cmd(UserStatuses.Active)));

        Assert.Equal(AccountErrorCodes.AdminBusinessStatusChangeNotAllowed, ex.ErrorCode);
        await h.AssertNothingHappened(UserStatuses.Inactive);
    }

    [Fact]
    public async Task AdminCannotActivateAPendingAccount()
    {
        var h = CreateHarness(UserStatuses.PendingEmailConfirmation);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => h.Run(Cmd(UserStatuses.Active)));

        Assert.Equal(AccountErrorCodes.SecurityUnlockInvalidState, ex.ErrorCode);
        await h.AssertNothingHappened(UserStatuses.PendingEmailConfirmation);
    }

    // ── Fail closed ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ARoleThatManagesNoAccountsIsRefusedOutright()
    {
        // Not "the screen is hidden from them" — a valid token plus a hand-written request must fail.
        var h = CreateHarness();
        h.Actor.UserId = 950;
        h.Actor.RoleCode = RoleCodes.Student;
        h.Actor.SubRole = null;

        var ex = await Assert.ThrowsAsync<AuthBusinessException>(() => h.Run(Cmd(UserStatuses.Locked)));

        Assert.Equal(AccountErrorCodes.AccountStatusChangeForbidden, ex.ErrorCode);
        Assert.Equal(403, ex.StatusCode);
        await h.AssertNothingHappened(UserStatuses.Active);
    }

    [Fact]
    public async Task DepartmentStaffCannotManageStatusEvenWithinTheirOwnCampus()
    {
        var h = CreateHarness(targetCampus: Campus);
        h.Actor.UserId = 951;
        h.Actor.RoleCode = RoleCodes.Department;
        h.Actor.SubRole = UserSubRoles.Staff;

        await Assert.ThrowsAsync<AuthBusinessException>(() => h.Run(Cmd(UserStatuses.Inactive)));
        await h.AssertNothingHappened(UserStatuses.Active);
    }

    // ── HO / Staff Leader regression: LOCKED belongs to ADMIN ───────────────────────────────────

    [Fact]
    public async Task StaffLeaderCannotSecurityLock()
    {
        var h = CreateHarness(targetCampus: Campus);
        h.Actor.UserId = 900;
        h.Actor.RoleCode = RoleCodes.Staff;
        h.Actor.SubRole = UserSubRoles.Leader;
        h.Actor.PrimaryCampusId = Campus;

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => h.Run(Cmd(UserStatuses.Locked)));

        Assert.Equal(AccountErrorCodes.SecurityStatusRequiresAdmin, ex.ErrorCode);
        await h.AssertNothingHappened(UserStatuses.Active);
    }

    [Fact]
    public async Task StaffLeaderCannotSecurityUnlock()
    {
        var h = CreateHarness(UserStatuses.Locked, targetCampus: Campus);
        h.Actor.UserId = 900;
        h.Actor.RoleCode = RoleCodes.Staff;
        h.Actor.SubRole = UserSubRoles.Leader;
        h.Actor.PrimaryCampusId = Campus;

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => h.Run(Cmd(UserStatuses.Active)));

        Assert.Equal(AccountErrorCodes.SecurityStatusRequiresAdmin, ex.ErrorCode);
        await h.AssertNothingHappened(UserStatuses.Locked);
    }

    [Fact]
    public async Task StaffLeaderStillTogglesBusinessStatusInTheirOwnCampus()
    {
        // The regression guard for everything above: the same fixture DOES go through for the
        // transition Staff Leader owns, so the refusals are attributable to the rule and not to a
        // fixture that could never have succeeded.
        var h = CreateHarness(targetCampus: Campus);
        h.Actor.UserId = 900;
        h.Actor.RoleCode = RoleCodes.Staff;
        h.Actor.SubRole = UserSubRoles.Leader;
        h.Actor.PrimaryCampusId = Campus;

        var res = await h.Run(Cmd(UserStatuses.Inactive, reason: null));

        Assert.Equal(UserStatuses.Inactive, res.Status);
        var audit = Assert.Single(h.Db.AuditLogs.Local);
        Assert.Equal("MANAGE_ACCOUNT_STATUS", audit.Action);
        Assert.Contains(h.Sessions.RevokeAllCalls,
            c => c.UserId == TargetId && c.Reason == SessionRevokeReasons.AccountDeactivated);
    }
}
