using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PEMS.Application.Accounts.Commands.UpdateAccountRole;
using PEMS.Application.Accounts.Common;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Common.Security;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Delegations;
using PEMS.Domain.Entities.Users;
using PEMS.Infrastructure.Persistence;
using PEMS.IntegrationTests.TestInfrastructure;
using Xunit;

namespace PEMS.IntegrationTests.Accounts;

/// <summary>
/// The race the whole feature exists to close, proved against real MySQL (spec §23).
///
/// Checking dependencies twice cannot make a role change safe on its own: between the check and the
/// commit, another transaction can still hand the account a Host / participant / logistics duty. The
/// guarantee comes from both sides taking the same <c>SELECT … FOR UPDATE</c> row lock, so the two
/// transactions serialize and the loser re-reads committed state. EF InMemory has no row locks at
/// all and would report success for code that races in production, which is exactly why these tests
/// run on a disposable MySQL database instead.
///
/// The handler is constructed directly rather than driven over HTTP: these tests need two
/// transactions interleaved at precise points, which a request/response boundary cannot express.
/// </summary>
public sealed class AccountRoleChangeConcurrencyTests : IClassFixture<PemsWebApplicationFactory>, IAsyncLifetime
{
    private const string TestPrefix = "[IT-SAFE-ROLE-CHANGE] ";
    private static readonly TimeSpan LockWait = TimeSpan.FromSeconds(10);

    private readonly PemsWebApplicationFactory _factory;

    private ulong _campusId;
    private ulong _icDepartmentId;
    private ulong _generalDepartmentId;
    private ulong _leaderUserId;
    private ulong _targetUserId;
    private ulong _successorUserId;
    private ulong _registrantUserId;
    private ulong _visitRequestId;
    private ulong _visitInstanceId;

    public AccountRoleChangeConcurrencyTests(PemsWebApplicationFactory factory) => _factory = factory;

    // ── fixture ───────────────────────────────────────────────────────────────

    public async Task InitializeAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        _leaderUserId = await DatabaseResetHelper.EnsureTestUserAsync(db, EffectiveRole.StaffLeader);
        var leader = await db.Users.AsNoTracking().FirstAsync(u => u.UserId == _leaderUserId);
        _campusId = leader.PrimaryCampusId!.Value;
        _icDepartmentId = leader.DepartmentId!.Value;

        _generalDepartmentId = await db.Departments.AsNoTracking()
            .Where(d => d.CampusId == _campusId && d.DepartmentType == "GENERAL" && d.Status == EntityStatuses.Active)
            .OrderBy(d => d.DepartmentId)
            .Select(d => d.DepartmentId)
            .FirstAsync();

        var staffRoleId = await db.Roles.AsNoTracking()
            .Where(r => r.RoleCode == RoleCodes.Staff).Select(r => r.RoleId).FirstAsync();

        var target = new User
        {
            FullName = $"{TestPrefix}Target Staff",
            Email = $"it-safe-role-change-target@pems.test",
            RoleId = staffRoleId,
            SubRole = UserSubRoles.Staff,
            PrimaryCampusId = _campusId,
            DepartmentId = _icDepartmentId,
            Status = UserStatuses.Active,
            CreatedVia = "MANUAL_CREATED",
            CreatedAt = DateTime.Now,
        };
        db.Users.Add(target);
        await db.SaveChangesAsync();
        _targetUserId = target.UserId;

        // A colleague in the GENERAL department, ready to take the head seat over when the target
        // is re-roled out of it.
        var departmentRoleId = await db.Roles.AsNoTracking()
            .Where(r => r.RoleCode == RoleCodes.Department).Select(r => r.RoleId).FirstAsync();
        var successor = new User
        {
            FullName = $"{TestPrefix}Successor",
            Email = "it-safe-role-change-successor@pems.test",
            RoleId = departmentRoleId,
            SubRole = UserSubRoles.Staff,
            PrimaryCampusId = _campusId,
            DepartmentId = _generalDepartmentId,
            Status = UserStatuses.Active,
            CreatedVia = "MANUAL_CREATED",
            CreatedAt = DateTime.Now,
        };
        db.Users.Add(successor);
        await db.SaveChangesAsync();
        _successorUserId = successor.UserId;

        // The guest who submitted, and — self-matched — the campus's operational contact. A campus past
        // WAITING_CONTACT_CONFIRMATION may not have a NULL contact, and this suite is about role-change
        // concurrency, so the shortest valid contact model is the right one. Deliberately NOT one of the
        // internal accounts above: making the successor or the target a contact would hand them
        // guest-side rights and quietly change what the concurrency test is measuring.
        var registrant = new User
        {
            FullName = $"{TestPrefix}Registrant",
            Email = "it-safe-role-change-registrant@pems.test",
            RoleId = await db.Roles.Where(r => r.RoleCode == RoleCodes.Visitor)
                .Select(r => r.RoleId).FirstAsync(),
            Status = UserStatuses.Active,
            CreatedVia = "MANUAL_CREATED",
            CreatedAt = DateTime.Now,
        };
        db.Users.Add(registrant);
        await db.SaveChangesAsync();
        _registrantUserId = registrant.UserId;

        var request = new VisitRequest
        {
            RequestCode = $"IT-SRC-{Guid.NewGuid().ToString("N")[..8]}",
            RegistrantUserId = registrant.UserId,
            RegistrantFullName = "Registrant",
            RegistrantNationality = "VN",
            RegistrantOrganization = "Org",
            RegistrantJobTitle = "Manager",
            RegistrantPhone = "0900000000",
            RegistrantEmail = "it-safe-role-change-registrant@pems.test",
            VisitScope = VisitScopes.SingleCampus,
            Status = VisitRequestStatuses.PendingApproval,
            CreatedAt = DateTime.Now,
        };
        db.VisitRequests.Add(request);
        await db.SaveChangesAsync();
        _visitRequestId = request.VisitRequestId;

        var instance = new VisitRequestCampus
        {
            VisitRequestId = _visitRequestId,
            CampusId = _campusId,
            PlannedStartAt = DateTime.Now.AddDays(30),
            PlannedEndAt = DateTime.Now.AddDays(30).AddHours(2),
            Status = VisitInstanceStatuses.WaitingRequestApproval,
            OperationalContactUserId = registrant.UserId,
            OperationalContactConfirmedAt = DateTime.Now,
            OperationalContactConfirmationSource = OperationalContactSources.RegistrantSelfMatch,
            CreatedAt = DateTime.Now,
        };
        db.VisitRequestCampuses.Add(instance);
        await db.SaveChangesAsync();
        _visitInstanceId = instance.VisitInstanceId;
    }

    public async Task DisposeAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        await db.Database.ExecuteSqlRawAsync(
            "DELETE FROM visit_logistics_items WHERE visit_instance_id = {0}", _visitInstanceId);
        await db.Database.ExecuteSqlRawAsync(
            "DELETE FROM visit_participants WHERE visit_instance_id = {0}", _visitInstanceId);
        await db.Database.ExecuteSqlRawAsync(
            "DELETE FROM visit_request_campuses WHERE visit_request_id = {0}", _visitRequestId);
        await db.Database.ExecuteSqlRawAsync(
            "DELETE FROM visit_requests WHERE visit_request_id = {0}", _visitRequestId);
        await db.Database.ExecuteSqlRawAsync(
            "UPDATE departments SET head_user_id = NULL WHERE head_user_id IN ({0}, {1})",
            _targetUserId, _successorUserId);
        await db.Database.ExecuteSqlRawAsync(
            "DELETE FROM users WHERE user_id = {0}", _successorUserId);
        await db.Database.ExecuteSqlRawAsync(
            "DELETE FROM audit_log_changes WHERE audit_log_id IN (SELECT audit_log_id FROM audit_logs WHERE entity_type = 'User' AND entity_id = {0})", _targetUserId);
        await db.Database.ExecuteSqlRawAsync(
            "DELETE FROM audit_logs WHERE entity_type = 'User' AND entity_id = {0}", _targetUserId);
        await db.Database.ExecuteSqlRawAsync(
            "DELETE FROM users WHERE user_id = {0}", _targetUserId);
        // After the request and its campuses, so neither registrant_user_id nor
        // operational_contact_user_id is still pointing here.
        await db.Database.ExecuteSqlRawAsync(
            "DELETE FROM users WHERE user_id = {0}", _registrantUserId);
    }

    // ── §23.1 A responsibility that committed first makes the role change fail ──

    [Fact]
    public async Task HostAssignedAndCommitted_ThenRoleChange_Returns409()
    {
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await AssignHostAsync(db, _visitInstanceId, _targetUserId);
        }

        using var roleScope = _factory.Services.CreateScope();
        var roleDb = roleScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var handler = CreateHandler(roleDb);

        var ex = await Assert.ThrowsAsync<ConflictException>(() => handler.Handle(ToStudentCommand(), CancellationToken.None));
        Assert.Equal(AccountErrorCodes.AccountRoleChangeBlockedByActiveResponsibilities, ex.ErrorCode);

        await AssertTargetUnchangedAsync(RoleCodes.Staff, UserSubRoles.Staff);
    }

    // ── §23.2 The role change holds the lock; the assignment waits and then sees the new role ──

    [Fact]
    public async Task RoleChangeHoldsTheLock_AssignmentWaitsThenSeesTheNewRole()
    {
        // A holds the lock for this long AFTER B is known to be contending for it, so B's wait is
        // caused by the lock rather than by B simply starting late.
        var hold = TimeSpan.FromMilliseconds(500);

        // Scheduling noise is single-digit milliseconds; an unlocked SELECT returns in ~0. A floor at
        // half the hold separates "genuinely blocked" from "not blocked" without depending on runner speed.
        var blockedFloor = TimeSpan.FromMilliseconds(250);

        var roleChangeHoldsTheLock = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var assignmentIsContending = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        // Transaction A: lock the account, change its role, commit.
        var roleChange = Task.Run(async () =>
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var locks = new MySqlUserMutationLockService(db);

            await using var tx = await db.Database.BeginTransactionAsync();
            await locks.LockUsersAsync(new[] { _targetUserId }, CancellationToken.None);
            roleChangeHoldsTheLock.SetResult();

            // Keep holding until B has asked for the same lock, then a while longer.
            await assignmentIsContending.Task.WaitAsync(LockWait);
            await Task.Delay(hold);

            var studentRoleId = await db.Roles.AsNoTracking()
                .Where(r => r.RoleCode == RoleCodes.Student).Select(r => r.RoleId).FirstAsync();
            await db.Database.ExecuteSqlRawAsync(
                "UPDATE users SET role_id = {0}, sub_role = NULL, department_id = NULL, student_code = 'ITSRC001' WHERE user_id = {1}",
                studentRoleId, _targetUserId);

            await tx.CommitAsync();
        });

        // Transaction B: wants the same lock in order to assign a Host.
        var assignment = Task.Run(async () =>
        {
            await roleChangeHoldsTheLock.Task.WaitAsync(LockWait);

            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var locks = new MySqlUserMutationLockService(db);

            // Open the transaction before announcing, so the announcement means "asking for the lock now"
            // and not "still opening a connection".
            await using var tx = await db.Database.BeginTransactionAsync();

            assignmentIsContending.SetResult();
            var startedWaiting = Stopwatch.StartNew();
            await locks.LockUsersAsync(new[] { _targetUserId }, CancellationToken.None);
            startedWaiting.Stop();

            // Eligibility is re-read AFTER the lock, so it observes the committed role.
            var roleCode = await db.Users.AsNoTracking()
                .Where(u => u.UserId == _targetUserId)
                .Select(u => u.Role!.RoleCode)
                .FirstAsync();

            await tx.RollbackAsync();
            return (RoleCode: roleCode, Waited: startedWaiting.Elapsed);
        });

        await roleChange.WaitAsync(LockWait);
        var (observedRole, waited) = await assignment.WaitAsync(LockWait);

        // B was blocked while A held the row: the lock really is what serialized the two flows.
        Assert.True(waited >= blockedFloor,
            $"The assignment acquired the user lock after only {waited.TotalMilliseconds:F0} ms while the role " +
            $"change held it for {hold.TotalMilliseconds:F0} ms — the lock is not serializing the two flows.");

        // And having waited, it reads committed state: STUDENT, so it refuses to make this account a Host.
        Assert.Equal(RoleCodes.Student, observedRole);
    }

    // ── §23.3 / §23.4 Participant and logistics duties block just as a Host does ──

    [Fact]
    public async Task ParticipantAcceptedConcurrently_ThenRoleChange_Returns409()
    {
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.VisitParticipants.Add(new VisitParticipant
            {
                VisitInstanceId = _visitInstanceId,
                UserId = _targetUserId,
                ParticipantRole = ParticipantRoles.IcSupport,
                IsHost = false,
                Status = ParticipantStatuses.Accepted,
                CreatedAt = DateTime.Now,
            });
            await db.SaveChangesAsync();
        }

        using var roleScope = _factory.Services.CreateScope();
        var handler = CreateHandler(roleScope.ServiceProvider.GetRequiredService<ApplicationDbContext>());

        var ex = await Assert.ThrowsAsync<ConflictException>(() => handler.Handle(ToStudentCommand(), CancellationToken.None));
        Assert.Equal(AccountErrorCodes.AccountRoleChangeBlockedByActiveResponsibilities, ex.ErrorCode);
        await AssertTargetUnchangedAsync(RoleCodes.Staff, UserSubRoles.Staff);
    }

    [Fact]
    public async Task ActiveLogisticsAssignment_ThenRoleChange_Returns409()
    {
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.VisitLogisticsItems.Add(new VisitLogisticsItem
            {
                VisitInstanceId = _visitInstanceId,
                ItemType = "OTHER",
                Title = $"{TestPrefix}Item",
                Quantity = 1,
                Status = "IN_PROGRESS",
                RequestedToDepartmentId = _generalDepartmentId,
                AssignedToUserId = _targetUserId,
                CreatedAt = DateTime.Now,
            });
            await db.SaveChangesAsync();
        }

        using var roleScope = _factory.Services.CreateScope();
        var handler = CreateHandler(roleScope.ServiceProvider.GetRequiredService<ApplicationDbContext>());

        var ex = await Assert.ThrowsAsync<ConflictException>(() => handler.Handle(ToStudentCommand(), CancellationToken.None));
        Assert.Equal(AccountErrorCodes.AccountRoleChangeBlockedByActiveResponsibilities, ex.ErrorCode);
        await AssertTargetUnchangedAsync(RoleCodes.Staff, UserSubRoles.Staff);
    }

    // ── §23.5 Department head stays consistent ────────────────────────────────

    [Fact]
    public async Task DepartmentHead_MustBeReassignedBeforeTheirRoleCanChange()
    {
        var departmentRoleId = await MakeTargetDepartmentLeaderAndHeadAsync();

        using (var roleScope = _factory.Services.CreateScope())
        {
            var handler = CreateHandler(roleScope.ServiceProvider.GetRequiredService<ApplicationDbContext>());

            var ex = await Assert.ThrowsAsync<ConflictException>(
                () => handler.Handle(ToStudentCommand(), CancellationToken.None));
            Assert.Equal(AccountErrorCodes.AccountRoleChangeBlockedByActiveResponsibilities, ex.ErrorCode);
        }

        // The department is NEVER left headless by a refused role change.
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var head = await db.Departments.AsNoTracking()
                .Where(d => d.DepartmentId == _generalDepartmentId).Select(d => d.HeadUserId).FirstAsync();
            Assert.Equal(_targetUserId, head);
        }

        await AssertTargetUnchangedAsync(RoleCodes.Department, UserSubRoles.Leader);

        // After a real handover the role change goes through, and it does not touch the new head.
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await db.Database.ExecuteSqlRawAsync(
                "UPDATE departments SET head_user_id = {0} WHERE department_id = {1}",
                _leaderUserId, _generalDepartmentId);
        }

        using (var roleScope = _factory.Services.CreateScope())
        {
            var handler = CreateHandler(roleScope.ServiceProvider.GetRequiredService<ApplicationDbContext>());
            await handler.Handle(ToStudentCommand(), CancellationToken.None);
        }

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var user = await db.Users.AsNoTracking().Include(u => u.Role).FirstAsync(u => u.UserId == _targetUserId);
            Assert.Equal(RoleCodes.Student, user.Role!.RoleCode);
            var head = await db.Departments.AsNoTracking()
                .Where(d => d.DepartmentId == _generalDepartmentId).Select(d => d.HeadUserId).FirstAsync();
            Assert.Equal(_leaderUserId, head);
            Assert.NotEqual(departmentRoleId, user.RoleId);
        }
    }

    [Fact]
    public async Task DepartmentHeadHandover_CommitsTogetherWithTheRoleChange()
    {
        await MakeTargetDepartmentLeaderAndHeadAsync();

        using (var roleScope = _factory.Services.CreateScope())
        {
            var handler = CreateHandler(roleScope.ServiceProvider.GetRequiredService<ApplicationDbContext>());
            var command = ToStudentCommand();
            command.ReplacementDepartmentHeadUserId = _successorUserId;
            await handler.Handle(command, CancellationToken.None);
        }

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var head = await db.Departments.AsNoTracking()
            .Where(d => d.DepartmentId == _generalDepartmentId).Select(d => d.HeadUserId).FirstAsync();
        Assert.Equal(_successorUserId, head);

        var successor = await db.Users.AsNoTracking().FirstAsync(u => u.UserId == _successorUserId);
        Assert.Equal(UserSubRoles.Leader, successor.SubRole);

        var target = await db.Users.AsNoTracking().Include(u => u.Role).FirstAsync(u => u.UserId == _targetUserId);
        Assert.Equal(RoleCodes.Student, target.Role!.RoleCode);
    }

    [Fact]
    public async Task DepartmentHeadHandover_IsRolledBackWhenAnotherBlockerRefuses()
    {
        // The handover is written and flushed BEFORE the dependency check runs (so the check reads
        // the new head). A blocker found a moment later must take it back down with it — otherwise a
        // refused role change would still have moved a department to a new head. Only a real
        // transaction can prove this, which is why the assertion lives here and not in the unit suite.
        // Host FIRST, department head second — that order is the only one the schema allows, and it is
        // also the real sequence: a campus host is an IC Staff member, and only afterwards were they
        // moved into the GENERAL department and given the head seat. Assigning the host after the move
        // would ask the database to accept a DEPARTMENT account as a campus host, which
        // trg_visit_campuses_assignment_validate_bu refuses outright.
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await AssignHostAsync(db, _visitInstanceId, _targetUserId);
        }

        await MakeTargetDepartmentLeaderAndHeadAsync();

        using (var roleScope = _factory.Services.CreateScope())
        {
            var handler = CreateHandler(roleScope.ServiceProvider.GetRequiredService<ApplicationDbContext>());
            var command = ToStudentCommand();
            command.ReplacementDepartmentHeadUserId = _successorUserId;

            var ex = await Assert.ThrowsAsync<ConflictException>(
                () => handler.Handle(command, CancellationToken.None));
            Assert.Equal(AccountErrorCodes.AccountRoleChangeBlockedByActiveResponsibilities, ex.ErrorCode);
        }

        using var verifyScope = _factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var head = await verifyDb.Departments.AsNoTracking()
            .Where(d => d.DepartmentId == _generalDepartmentId).Select(d => d.HeadUserId).FirstAsync();
        Assert.Equal(_targetUserId, head);

        var successor = await verifyDb.Users.AsNoTracking().FirstAsync(u => u.UserId == _successorUserId);
        Assert.Equal(UserSubRoles.Staff, successor.SubRole);

        await AssertTargetUnchangedAsync(RoleCodes.Department, UserSubRoles.Leader);
    }

    // ── §23.6 Rollback leaves nothing behind ──────────────────────────────────

    [Fact]
    public async Task FailureAfterTheDependencyCheck_RollsEverythingBack()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var locks = new MySqlUserMutationLockService(db);
        var studentRoleId = await db.Roles.AsNoTracking()
            .Where(r => r.RoleCode == RoleCodes.Student).Select(r => r.RoleId).FirstAsync();

        await using (var tx = await db.Database.BeginTransactionAsync())
        {
            await locks.LockUsersAsync(new[] { _targetUserId }, CancellationToken.None);

            var impact = await AccountRoleChangeDependencyChecker.CheckAsync(
                db, _targetUserId, RoleCodes.Staff, UserSubRoles.Staff, _icDepartmentId,
                RoleCodes.Student, null, null, CancellationToken.None);
            Assert.True(impact.CanChangeRole);

            var user = await db.Users.FirstAsync(u => u.UserId == _targetUserId);
            user.RoleId = studentRoleId;
            user.SubRole = null;
            user.DepartmentId = null;
            user.StudentCode = "ITSRC999";
            db.AuditLogs.Add(new AuditLog
            {
                ActorUserId = _leaderUserId,
                CampusId = _campusId,
                Action = "UPDATE_ACCOUNT_ROLE",
                EntityType = "User",
                EntityId = _targetUserId,
                CreatedAt = DateTime.Now,
            });
            await db.SaveChangesAsync();

            // Something fails between SaveChanges and Commit.
            await tx.RollbackAsync();
        }

        await AssertTargetUnchangedAsync(RoleCodes.Staff, UserSubRoles.Staff);

        using var verifyScope = _factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var auditCount = await verifyDb.AuditLogs.AsNoTracking()
            .CountAsync(a => a.EntityType == "User" && a.EntityId == _targetUserId);
        Assert.Equal(0, auditCount);
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    private UpdateAccountRoleCommand ToStudentCommand() => new()
    {
        UserId = _targetUserId,
        NewRoleCode = RoleCodes.Student,
        StudentCode = "ITSRC100",
    };

    private UpdateAccountRoleCommandHandler CreateHandler(ApplicationDbContext db)
        => new(
            db,
            new StaticCurrentUser(_leaderUserId, _campusId, _icDepartmentId),
            new NoopSessionService(),
            new SystemClock(),
            new NoopEmailDispatcher(),
            new MySqlUserMutationLockService(db),
            new UnusedPendingEmailChangeService(),
            new UnusedConfirmationService());

    /// <summary>
    /// Puts the campus instance into the state a real host assignment leaves behind: ASSIGNED, with the
    /// host AND the decision metadata that authorised it.
    ///
    /// <para>
    /// Setting <c>current_host_user_id</c> alone is rejected by
    /// <c>trg_visit_campuses_assignment_validate_bu</c> — a WAITING_REQUEST_APPROVAL row may carry no host
    /// or decision data, and an ASSIGNED row must carry both. Writing only the host column produced a row
    /// the application could never create, so the blocker was being proved against a state that does not
    /// exist in production.
    /// </para>
    /// </summary>
    private async Task AssignHostAsync(ApplicationDbContext db, ulong instanceId, ulong hostUserId)
    {
        await db.Database.ExecuteSqlRawAsync(
            "UPDATE visit_request_campuses SET status = 'ASSIGNED', current_host_user_id = {0}, "
            + "host_assigned_by = {1}, host_assigned_at = NOW(), decided_by = {1}, decided_at = NOW(), "
            + "decision_actor_role = 'STAFF_LEADER', decision_source = 'STANDARD_CAMPUS_REVIEW' "
            + "WHERE visit_instance_id = {2}",
            hostUserId, _leaderUserId, instanceId);
    }

    private async Task<ulong> MakeTargetDepartmentLeaderAndHeadAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var departmentRoleId = await db.Roles.AsNoTracking()
            .Where(r => r.RoleCode == RoleCodes.Department).Select(r => r.RoleId).FirstAsync();

        await db.Database.ExecuteSqlRawAsync(
            "UPDATE users SET role_id = {0}, sub_role = {1}, department_id = {2} WHERE user_id = {3}",
            departmentRoleId, UserSubRoles.Leader, _generalDepartmentId, _targetUserId);
        await db.Database.ExecuteSqlRawAsync(
            "UPDATE departments SET head_user_id = {0} WHERE department_id = {1}",
            _targetUserId, _generalDepartmentId);

        return departmentRoleId;
    }

    private async Task AssertTargetUnchangedAsync(string expectedRoleCode, string? expectedSubRole)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var user = await db.Users.AsNoTracking().Include(u => u.Role)
            .FirstAsync(u => u.UserId == _targetUserId);

        Assert.Equal(expectedRoleCode, user.Role!.RoleCode);
        Assert.Equal(expectedSubRole, user.SubRole);
        Assert.Null(user.StudentCode);
    }

    // ── minimal doubles: the collaborators a refused role change must never reach ──

    private sealed class StaticCurrentUser : ICurrentUserService
    {
        public StaticCurrentUser(ulong userId, ulong campusId, ulong departmentId)
        {
            UserId = userId;
            PrimaryCampusId = campusId;
            DepartmentId = departmentId;
        }

        public bool IsAuthenticated => true;
        public ulong? UserId { get; }
        public string? Email => "it-safe-role-change-leader@pems.test";
        public ulong? RoleId => null;
        public string? RoleCode => RoleCodes.Staff;
        public string? SubRole => UserSubRoles.Leader;
        public ulong? PrimaryCampusId { get; }
        public ulong? DepartmentId { get; }
        public ulong? SessionId => 1;
        public string? LoginPortal => "INTERNAL";
    }

    private sealed class SystemClock : IDateTimeService
    {
        public DateTime UtcNow => DateTime.UtcNow;
        public DateTime VietnamNow => DateTime.UtcNow.AddHours(7);
    }

    private sealed class NoopSessionService : ISessionService
    {
        public int RevokeAllCalls { get; private set; }

        public Task<int> RevokeAllActiveSessionsAsync(
            ulong userId, string reason, ulong? revokedBy = null, CancellationToken cancellationToken = default)
        {
            RevokeAllCalls++;
            return Task.FromResult(0);
        }

        public Task<SessionTokens> CreateSessionAsync(User user, string loginPortal, ulong? authProviderId,
            string? ipAddress, string? userAgent, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<UserSession?> GetActiveByRefreshTokenAsync(string rawRefreshToken, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<bool> IsSessionActiveAsync(ulong sessionId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<SessionTokens> RotateRefreshTokenAsync(UserSession session, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task RevokeSessionAsync(ulong sessionId, string reason, ulong? revokedBy = null, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    /// <summary>
    /// Swallows the role-changed notification. This suite is about the LOCK, and the mail is sent
    /// after the transaction commits — so the dispatcher only has to not throw.
    /// </summary>
    private sealed class NoopEmailDispatcher : PEMS.Application.Emails.Common.ISystemEmailDispatcher
    {
        public Task<PEMS.Application.Emails.Common.SystemEmailDispatchResult> SendAsync(
            PEMS.Application.Emails.Common.SystemEmailRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult(new PEMS.Application.Emails.Common.SystemEmailDispatchResult(
                EmailDeliveryResult.Sent(), SentEmailId: 0, EmailTemplateId: 0));

        public Task<PEMS.Application.Emails.Common.PreparedSystemEmail> PrepareAsync(
            PEMS.Application.Emails.Common.SystemEmailRequest request, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<EmailDeliveryResult> DeliverAsync(
            PEMS.Application.Emails.Common.PreparedSystemEmail prepared, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    /// <summary>
    /// The pending-email path is reached only when the command actually changes the address of a
    /// PENDING_EMAIL_CONFIRMATION account, and no test here does. Throwing rather than returning a
    /// plausible value keeps that assumption visible: if the handler ever starts issuing a token on a
    /// pure role change, this suite fails loudly instead of quietly exercising a different flow.
    /// </summary>
    private sealed class UnusedPendingEmailChangeService : IPendingAccountEmailChangeService
    {
        public Task<PreparedPendingEmailChange> PrepareAsync(
            User user, string newEmail, string? newFullName, CancellationToken cancellationToken)
            => throw new NotSupportedException(
                "A role-only change must not prepare a pending email change.");
    }

    private sealed class UnusedConfirmationService : IAccountEmailConfirmationService
    {
        public int ExpiryHours => throw new NotSupportedException();

        public Task<string> IssuePendingAsync(
            ulong userId, string normalizedTargetEmail, bool isResend, CancellationToken cancellationToken)
            => throw new NotSupportedException(
                "A role-only change must not issue an account email confirmation.");

        public string BuildConfirmUrl(string rawToken) => throw new NotSupportedException();

        public string BuildLoginUrl() => throw new NotSupportedException();
    }
}
