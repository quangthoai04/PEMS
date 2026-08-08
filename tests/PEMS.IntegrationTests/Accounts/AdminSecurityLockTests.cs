using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PEMS.Application.Accounts.Commands.CreateAccount;
using PEMS.Application.Accounts.Commands.ManageAccountStatus;
using PEMS.Application.Accounts.Commands.UpdateAccountRole;
using PEMS.Application.Accounts.Common;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Common.Security;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Users;
using PEMS.Infrastructure.Persistence;
using PEMS.IntegrationTests.TestInfrastructure;
using Xunit;

namespace PEMS.IntegrationTests.Accounts;

/// <summary>
/// ADMIN = Global Read + Security Control, proved against real MySQL —
/// ADMIN_ACCOUNT_MANAGEMENT spec §45/§47.
///
/// <para>
/// The unit suite already covers the decision logic. What only a real database can show is the
/// STATE after the call: that a security lock actually leaves <c>users.status = LOCKED</c> with every
/// row in <c>user_sessions</c> revoked and an <c>audit_logs</c> row saying SECURITY_LOCK_ACCOUNT,
/// and — the half that matters more — that a REFUSED business mutation leaves the database
/// byte-for-byte as it was: no user inserted, no role moved, no audit row claiming a success that
/// never happened. A 403 that still wrote something is precisely the failure this boundary exists to
/// prevent, and an in-memory provider cannot rule it out.
/// </para>
///
/// <para>
/// The handlers are constructed directly rather than driven over HTTP, matching
/// <see cref="AccountRoleChangeConcurrencyTests"/>: the collaborators (session revoke, audit,
/// notification) are the REAL ones resolved from the same scope, so they share this test's
/// DbContext and their writes are visible to the assertions.
/// </para>
/// </summary>
public sealed class AdminSecurityLockTests : IClassFixture<PemsWebApplicationFactory>, IAsyncLifetime
{
    private const string TestPrefix = "[IT-ADMIN-SECURITY] ";
    private const string TargetEmail = "it-admin-security-target@pems.test";
    private const string CreateAttemptEmail = "it-admin-security-created@pems.test";

    private readonly PemsWebApplicationFactory _factory;

    private ulong _adminUserId;
    private ulong _campusId;
    private ulong _targetUserId;
    private ulong _studentRoleId;

    public AdminSecurityLockTests(PemsWebApplicationFactory factory) => _factory = factory;

    // ── fixture ───────────────────────────────────────────────────────────────

    public async Task InitializeAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        _adminUserId = await DatabaseResetHelper.EnsureTestUserAsync(db, EffectiveRole.Admin);
        var admin = await db.Users.AsNoTracking().FirstAsync(u => u.UserId == _adminUserId);
        _campusId = admin.PrimaryCampusId!.Value;

        _studentRoleId = await db.Roles.AsNoTracking()
            .Where(r => r.RoleCode == RoleCodes.Student).Select(r => r.RoleId).FirstAsync();

        var target = new User
        {
            FullName = $"{TestPrefix}Target",
            Email = TargetEmail,
            RoleId = _studentRoleId,
            PrimaryCampusId = _campusId,
            StudentCode = $"ITADM{DateTime.Now:HHmmssfff}",
            Status = UserStatuses.Active,
            CreatedVia = "MANUAL_CREATED",
            CreatedAt = DateTime.Now,
        };
        db.Users.Add(target);
        await db.SaveChangesAsync();
        _targetUserId = target.UserId;
    }

    public Task DisposeAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        // audit_log_changes before audit_logs, and every row referencing the users before the users
        // themselves — the same ordering rule the other account fixtures follow.
        return FixtureCleanup.For(db)
            .Root("audit_log_changes",
                $"audit_log_id IN (SELECT audit_log_id FROM audit_logs WHERE entity_type = 'User' AND entity_id = {_targetUserId})")
            .Root("audit_logs", $"entity_type = 'User' AND entity_id = {_targetUserId}")
            .Root("notifications", $"recipient_user_id = {_targetUserId}")
            .Root("user_sessions", $"user_id = {_targetUserId}")
            .Root("users", $"email IN ('{TargetEmail}', '{CreateAttemptEmail}')")
            .RunAsync();
    }

    // ── §45 Case: ADMIN lock ──────────────────────────────────────────────────

    [Fact]
    public async Task SecurityLock_LocksTheAccount_RevokesEverySession_AndWritesASecurityAudit()
    {
        await GiveTargetActiveSessionsAsync(2);

        using (var scope = _factory.Services.CreateScope())
        {
            var response = await RunStatusAsync(scope, new ManageAccountStatusCommand
            {
                UserId = _targetUserId,
                Status = UserStatuses.Locked,
                Reason = "Nghi ngờ tài khoản bị xâm nhập",
            });
            Assert.Equal(UserStatuses.Locked, response.Status);
            Assert.Equal(2, response.RevokedSessions);
        }

        using var verify = _factory.Services.CreateScope();
        var db = verify.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var user = await db.Users.AsNoTracking().FirstAsync(u => u.UserId == _targetUserId);
        Assert.Equal(UserStatuses.Locked, user.Status);
        Assert.Equal(_adminUserId, user.UpdatedBy);

        // Not "the API said 2": the rows themselves are revoked, and they say a security lock did it.
        var sessions = await db.UserSessions.AsNoTracking()
            .Where(s => s.UserId == _targetUserId).ToListAsync();
        Assert.Equal(2, sessions.Count);
        Assert.All(sessions, s =>
        {
            Assert.NotNull(s.RevokedAt);
            Assert.Equal(SessionRevokeReasons.AccountSecurityLocked, s.RevokedReason);
            Assert.Equal(_adminUserId, s.RevokedBy);
        });

        var audit = await SingleAuditAsync(db);
        Assert.Equal("SECURITY_LOCK_ACCOUNT", audit.Action);
        Assert.Equal(_adminUserId, audit.ActorUserId);
    }

    [Fact]
    public async Task SecurityUnlock_ReopensTheAccount_ClearsLockoutCounters_AndDoesNotRestoreSessions()
    {
        await GiveTargetActiveSessionsAsync(1);
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await db.Database.ExecuteSqlRawAsync(
                "UPDATE users SET status = 'LOCKED', failed_login_count = 5, "
                + "locked_until = DATE_ADD(NOW(), INTERVAL 1 DAY) WHERE user_id = {0}", _targetUserId);
            await db.Database.ExecuteSqlRawAsync(
                "UPDATE user_sessions SET revoked_at = NOW(), revoked_reason = {0} WHERE user_id = {1}",
                SessionRevokeReasons.AccountSecurityLocked, _targetUserId);
        }

        using (var scope = _factory.Services.CreateScope())
        {
            var response = await RunStatusAsync(scope, new ManageAccountStatusCommand
            {
                UserId = _targetUserId,
                Status = UserStatuses.Active,
                Reason = "Điều tra hoàn tất",
            });
            Assert.Equal(UserStatuses.Active, response.Status);
            Assert.Equal(0, response.RevokedSessions);
        }

        using var verify = _factory.Services.CreateScope();
        var db2 = verify.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var user = await db2.Users.AsNoTracking().FirstAsync(u => u.UserId == _targetUserId);
        Assert.Equal(UserStatuses.Active, user.Status);
        Assert.Equal(0, user.FailedLoginCount);
        Assert.Null(user.LockedUntil);

        // The holder signs in again; they are not handed their pre-lock session back.
        var session = await db2.UserSessions.AsNoTracking().FirstAsync(s => s.UserId == _targetUserId);
        Assert.NotNull(session.RevokedAt);

        var audit = await SingleAuditAsync(db2);
        Assert.Equal("SECURITY_UNLOCK_ACCOUNT", audit.Action);
    }

    [Fact]
    public async Task AdminCannotMoveAnAccountBetweenBusinessStates()
    {
        using (var scope = _factory.Services.CreateScope())
        {
            var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => RunStatusAsync(scope,
                new ManageAccountStatusCommand
                {
                    UserId = _targetUserId,
                    Status = UserStatuses.Inactive,
                    Reason = "Nhân sự nghỉ việc",
                }));
            Assert.Equal(AccountErrorCodes.AdminBusinessStatusChangeNotAllowed, ex.ErrorCode);
        }

        await AssertTargetUntouchedAsync(UserStatuses.Active);
    }

    [Fact]
    public async Task SecurityLockWithoutAReasonWritesNothing()
    {
        await GiveTargetActiveSessionsAsync(1);

        using (var scope = _factory.Services.CreateScope())
        {
            var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => RunStatusAsync(scope,
                new ManageAccountStatusCommand
                {
                    UserId = _targetUserId, Status = UserStatuses.Locked, Reason = "  ",
                }));
            Assert.Equal(AccountErrorCodes.SecurityReasonRequired, ex.ErrorCode);
        }

        await AssertTargetUntouchedAsync(UserStatuses.Active);

        using var verify = _factory.Services.CreateScope();
        var db = verify.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var session = await db.UserSessions.AsNoTracking().FirstAsync(s => s.UserId == _targetUserId);
        Assert.Null(session.RevokedAt);
    }

    // ── §45 Case: denied mutation ─────────────────────────────────────────────

    [Fact]
    public async Task AdminCreateAccount_Is403_AndNoRowIsWritten()
    {
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var handler = new CreateAccountCommandHandler(
                db,
                AdminActor(),
                scope.ServiceProvider.GetRequiredService<global::Application.Common.Interfaces.IPasswordHasher>(),
                scope.ServiceProvider.GetRequiredService<IDateTimeService>(),
                scope.ServiceProvider.GetRequiredService<AuthOptions>(),
                scope.ServiceProvider.GetRequiredService<PEMS.Application.Emails.Common.ISystemEmailDispatcher>(),
                scope.ServiceProvider.GetRequiredService<PEMS.Application.Notifications.Common.INotificationService>(),
                scope.ServiceProvider.GetRequiredService<IAccountEmailConfirmationService>());

            var ex = await Assert.ThrowsAsync<AuthBusinessException>(() => handler.Handle(new CreateAccountCommand
            {
                RoleCode = RoleCodes.Student,
                FullName = $"{TestPrefix}Should Not Exist",
                Email = CreateAttemptEmail,
                StudentCode = $"ITADM{DateTime.Now:HHmmssfff}X",
                PrimaryCampusId = _campusId,
            }, CancellationToken.None));

            Assert.Equal(AccountErrorCodes.AdminAccountCreationNotAllowed, ex.ErrorCode);
            Assert.Equal(403, ex.StatusCode);
        }

        using var verify = _factory.Services.CreateScope();
        var verifyDb = verify.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.False(await verifyDb.Users.AsNoTracking().AnyAsync(u => u.Email == CreateAttemptEmail));
        Assert.False(await verifyDb.AccountEmailConfirmations.AsNoTracking()
            .AnyAsync(c => c.TargetEmail == CreateAttemptEmail));
    }

    [Fact]
    public async Task AdminUpdateAccountRole_Is403_AndTheAccountIsUnchanged()
    {
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var handler = new UpdateAccountRoleCommandHandler(
                db,
                AdminActor(),
                scope.ServiceProvider.GetRequiredService<ISessionService>(),
                scope.ServiceProvider.GetRequiredService<IDateTimeService>(),
                scope.ServiceProvider.GetRequiredService<PEMS.Application.Emails.Common.ISystemEmailDispatcher>(),
                scope.ServiceProvider.GetRequiredService<IUserMutationLockService>(),
                scope.ServiceProvider.GetRequiredService<IPendingAccountEmailChangeService>(),
                scope.ServiceProvider.GetRequiredService<IAccountEmailConfirmationService>());

            var ex = await Assert.ThrowsAsync<AuthBusinessException>(() => handler.Handle(
                new UpdateAccountRoleCommand
                {
                    UserId = _targetUserId,
                    NewRoleCode = RoleCodes.Staff,
                    SubRole = UserSubRoles.Staff,
                    PrimaryCampusId = _campusId,
                    FullName = "Tên Bị Đổi",
                }, CancellationToken.None));

            Assert.Equal(AccountErrorCodes.AdminAccountEditNotAllowed, ex.ErrorCode);
            Assert.Equal(403, ex.StatusCode);
        }

        using var verify = _factory.Services.CreateScope();
        var db2 = verify.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var user = await db2.Users.AsNoTracking().Include(u => u.Role).FirstAsync(u => u.UserId == _targetUserId);
        Assert.Equal(RoleCodes.Student, user.Role!.RoleCode);
        Assert.Equal($"{TestPrefix}Target", user.FullName);
        Assert.Empty(await db2.AuditLogs.AsNoTracking()
            .Where(a => a.EntityType == "User" && a.EntityId == _targetUserId).ToListAsync());
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    private Task<ManageAccountStatusResponse> RunStatusAsync(IServiceScope scope, ManageAccountStatusCommand cmd)
    {
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var handler = new ManageAccountStatusCommandHandler(
            db,
            AdminActor(),
            scope.ServiceProvider.GetRequiredService<ISessionService>(),
            scope.ServiceProvider.GetRequiredService<ISecurityAuditService>(),
            scope.ServiceProvider.GetRequiredService<IDateTimeService>(),
            scope.ServiceProvider.GetRequiredService<PEMS.Application.Notifications.Common.INotificationService>());
        return handler.Handle(cmd, CancellationToken.None);
    }

    private ICurrentUserService AdminActor() => new StaticAdmin(_adminUserId, _campusId);

    private async Task GiveTargetActiveSessionsAsync(int count)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        for (var i = 0; i < count; i++)
        {
            db.UserSessions.Add(new UserSession
            {
                UserId = _targetUserId,
                LoginPortal = "INTERNAL",
                SelectedCampusId = _campusId,
                CreatedAt = DateTime.Now,
                ExpiresAt = DateTime.Now.AddDays(1),
            });
        }
        await db.SaveChangesAsync();
    }

    private async Task AssertTargetUntouchedAsync(string expectedStatus)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var user = await db.Users.AsNoTracking().FirstAsync(u => u.UserId == _targetUserId);
        Assert.Equal(expectedStatus, user.Status);
        Assert.Empty(await db.AuditLogs.AsNoTracking()
            .Where(a => a.EntityType == "User" && a.EntityId == _targetUserId).ToListAsync());
    }

    private async Task<PEMS.Domain.Entities.Users.AuditLog> SingleAuditAsync(ApplicationDbContext db)
        => await db.AuditLogs.AsNoTracking()
            .Where(a => a.EntityType == "User" && a.EntityId == _targetUserId)
            .SingleAsync();

    /// <summary>The ADMIN identity, exactly as a real ADMIN JWT would present it.</summary>
    private sealed class StaticAdmin : ICurrentUserService
    {
        public StaticAdmin(ulong userId, ulong campusId)
        {
            UserId = userId;
            PrimaryCampusId = campusId;
        }

        public bool IsAuthenticated => true;
        public ulong? UserId { get; }
        public string? Email => "it-admin-security-actor@pems.test";
        public ulong? RoleId => null;
        public string? RoleCode => RoleCodes.Admin;
        public string? SubRole => null;
        public ulong? PrimaryCampusId { get; }
        public ulong? DepartmentId => null;
        public ulong? SessionId => 1;
        public string? LoginPortal => "INTERNAL";
    }
}
