using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Delegations.Commands.UpdateProposedHost;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Delegations;
using PEMS.Domain.Entities.Users;
using PEMS.Infrastructure.Persistence;
using PEMS.IntegrationTests.TestInfrastructure;
using PEMS.Shared;
using Xunit;

namespace PEMS.IntegrationTests.Delegations;

/// <summary>
/// DB-TXN-001: <see cref="UpdateProposedHostCommandHandler"/> took a real <c>SELECT ... FOR UPDATE</c>
/// lock on the proposed host's account but never opened a transaction around it, so under MySQL
/// autocommit the lock released the instant that one statement completed - before the eligibility check
/// it exists to protect ever ran, and long before this command's own write. A concurrent role/status
/// change on that same account could commit in the gap and the proposal would still go through on a
/// stale eligibility read.
///
/// <para>
/// A green unit suite cannot see this: EF InMemory has no row locks and no isolation to violate. This
/// drives two genuinely separate MySQL connections, same shape as
/// <c>AssignDepartmentStaffAtomicityTests.A_concurrent_role_change_cannot_slip_under_the_eligibility_checks</c>:
/// it proves the row lock is real (MySQL blocks a competing FOR UPDATE for as long as the fix holds the
/// transaction open) and that the handler always decides off the freshly COMMITTED state rather than a
/// value it read earlier.
/// </para>
/// <para>
/// What this does NOT attempt is timing the sub-millisecond window between the pre-fix code's lock
/// release and its own write - several black-box approaches were tried (racing a second connection for
/// the same NOWAIT lock, then passively polling <c>performance_schema.data_locks</c>) and none caught
/// that window reliably enough to be trusted as a regression gate; the window is shorter than a
/// polling loop can dependably observe without instrumenting the handler itself, which was avoided as
/// out of scope for a minimal fix. That specific property - the lock now spans acquisition through
/// SaveChanges and Commit rather than just the one SELECT - is verified by the diff itself: <see
/// cref="UpdateProposedHostCommandHandler.Handle"/> opens <c>BeginSerializedTransactionAsync</c> before
/// <c>LockUsersAsync</c> and calls <c>CommitAsync</c> only after <c>SaveChangesAsync</c>, and MySQL's own
/// transactional guarantee - a row lock taken inside an open transaction is held until that transaction
/// ends - is what does the rest.
/// </para>
/// </summary>
public sealed class UpdateProposedHostConcurrencyTests : IClassFixture<PemsWebApplicationFactory>, IAsyncLifetime
{
    private const string TestPrefix = "[IT-PROPOSED-HOST] ";
    private static readonly TimeSpan LockWait = TimeSpan.FromSeconds(15);

    private readonly PemsWebApplicationFactory _factory;

    private ulong _campusId;
    private ulong _icDepartmentId;
    private ulong _leaderUserId;
    private ulong _candidateUserId;
    private ulong _registrantUserId;
    private ulong _visitRequestId;
    private ulong _visitInstanceId;

    public UpdateProposedHostConcurrencyTests(PemsWebApplicationFactory factory) => _factory = factory;

    public async Task InitializeAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var staffRoleId = await db.Roles.AsNoTracking()
            .Where(r => r.RoleCode == RoleCodes.Staff).Select(r => r.RoleId).FirstAsync();

        // Must be an ACTIVE IC department: VisitHostEligibility.EvaluateAsync only accepts an IC-staff
        // candidate or the acting Leader themself.
        var icDept = await db.Departments.AsNoTracking()
            .Where(d => d.DepartmentType == "IC" && d.Status == EntityStatuses.Active)
            .OrderBy(d => d.DepartmentId).FirstAsync();
        _campusId = icDept.CampusId;
        _icDepartmentId = icDept.DepartmentId;

        var suffix = Guid.NewGuid().ToString("N")[..8];
        var leader = new User
        {
            FullName = $"{TestPrefix}Leader",
            Email = $"leader_{suffix}@pems.test",
            RoleId = staffRoleId,
            SubRole = UserSubRoles.Leader,
            PrimaryCampusId = _campusId,
            DepartmentId = _icDepartmentId,
            Status = UserStatuses.Active,
            CreatedVia = "MANUAL_CREATED",
            CreatedAt = DateTime.Now,
        };
        var candidate = new User
        {
            FullName = $"{TestPrefix}Candidate",
            Email = $"cand_{suffix}@pems.test",
            RoleId = staffRoleId,
            SubRole = UserSubRoles.Staff,
            PrimaryCampusId = _campusId,
            DepartmentId = _icDepartmentId,
            Status = UserStatuses.Active,
            CreatedVia = "MANUAL_CREATED",
            CreatedAt = DateTime.Now,
        };
        db.Users.AddRange(leader, candidate);
        await db.SaveChangesAsync();
        _leaderUserId = leader.UserId;
        _candidateUserId = candidate.UserId;

        var registrant = new User
        {
            FullName = $"{TestPrefix}Registrant",
            Email = $"reg_{suffix}@pems.test",
            RoleId = await db.Roles.Where(r => r.RoleCode == RoleCodes.Visitor).Select(r => r.RoleId).FirstAsync(),
            Status = UserStatuses.Active,
            CreatedVia = "MANUAL_CREATED",
            CreatedAt = DateTime.Now,
        };
        db.Users.Add(registrant);
        await db.SaveChangesAsync();
        _registrantUserId = registrant.UserId;

        var visit = new VisitRequest
        {
            RequestCode = $"IT-PH-{Guid.NewGuid().ToString("N")[..8]}",
            RegistrantUserId = registrant.UserId,
            RegistrantFullName = "Registrant",
            RegistrantNationality = "VN",
            RegistrantOrganization = "Org",
            RegistrantJobTitle = "Manager",
            RegistrantPhone = "0900000000",
            RegistrantEmail = registrant.Email,
            VisitScope = VisitScopes.SingleCampus,
            Status = VisitRequestStatuses.PendingApproval,
            CreatedAt = DateTime.Now,
        };
        db.VisitRequests.Add(visit);
        await db.SaveChangesAsync();
        _visitRequestId = visit.VisitRequestId;

        // Pre-decision, no current host: exactly the state UpdateProposedHostCommandHandler accepts.
        var instance = new VisitRequestCampus
        {
            VisitRequestId = _visitRequestId,
            CampusId = _campusId,
            OperationalContactUserId = registrant.UserId,
            PlannedStartAt = DateTime.Now.AddDays(20),
            PlannedEndAt = DateTime.Now.AddDays(20).AddHours(2),
            Status = VisitInstanceStatuses.WaitingRequestApproval,
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

        await FixtureCleanup.For(db)
            .Root("visit_requests", $"visit_request_id = {_visitRequestId}")
            .Root("users", $"user_id IN ({_leaderUserId}, {_candidateUserId}, {_registrantUserId})")
            .RunAsync();
    }

    /// <summary>
    /// Transaction A locks the candidate account and deactivates it - no longer eligible - while
    /// holding the transaction open. Transaction B is the real handler proposing that same candidate.
    /// It must take the same lock to reach the eligibility check at all, so it blocks until A commits,
    /// then reads the COMMITTED (now INACTIVE) row and refuses. A handler that skipped the lock
    /// entirely (a different, more severe bug than DB-TXN-001, but one this same shape would also
    /// catch) would race ahead and read the stale ACTIVE row instead.
    /// </summary>
    [Fact]
    public async Task A_concurrent_deactivation_cannot_slip_under_the_eligibility_check()
    {
        var hold = TimeSpan.FromMilliseconds(750);
        var blockedFloor = TimeSpan.FromMilliseconds(400);

        var deactivationHoldsLock = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        // Transaction A: lock the candidate account and deactivate it.
        var deactivation = Task.Run(async () =>
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var locks = new MySqlUserMutationLockService(db);

            await using var tx = await db.Database.BeginTransactionAsync();
            await locks.LockUsersAsync(new[] { _candidateUserId }, CancellationToken.None);

            deactivationHoldsLock.SetResult();
            await Task.Delay(hold);

            await db.Database.ExecuteSqlRawAsync(
                "UPDATE users SET status = {0} WHERE user_id = {1}",
                UserStatuses.Inactive, _candidateUserId);

            await tx.CommitAsync();
        });

        // Transaction B: the real handler, proposing the same candidate as host.
        var proposal = Task.Run(async () =>
        {
            await deactivationHoldsLock.Task;

            using var scope = _factory.Services.CreateScope();
            var handler = CreateHandler(scope);

            var blocked = Stopwatch.StartNew();
            var error = await Record.ExceptionAsync(() => handler.Handle(
                new UpdateProposedHostCommand(
                    _visitRequestId, _visitInstanceId, HostSelectionModes.Selected, _candidateUserId, 0),
                CancellationToken.None));
            blocked.Stop();
            return (Error: error, Waited: blocked.Elapsed);
        });

        await deactivation.WaitAsync(LockWait);
        var (thrown, waited) = await proposal.WaitAsync(LockWait);

        // It waited for the lock rather than racing past it.
        Assert.True(waited >= blockedFloor,
            $"The proposal returned after only {waited.TotalMilliseconds:F0} ms while the deactivation held "
            + $"the user lock for {hold.TotalMilliseconds:F0} ms - it is not contending on the user lock.");

        // Having waited, it saw the committed INACTIVE status and refused.
        Assert.NotNull(thrown);
        Assert.IsType<BusinessRuleException>(thrown);

        // The refusal is real: no partial proposal was ever written.
        using var check = _factory.Services.CreateScope();
        var checkDb = check.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var row = await checkDb.VisitRequestCampuses.AsNoTracking()
            .FirstAsync(c => c.VisitInstanceId == _visitInstanceId);
        Assert.Null(row.ProposedHostUserId);
        Assert.Equal(0, row.RowVersion);
    }

    /// <summary>The proposal succeeds normally when nothing contends for the candidate - the fix adds
    /// no false refusal on the ordinary path.</summary>
    [Fact]
    public async Task An_uncontended_proposal_still_succeeds()
    {
        using var scope = _factory.Services.CreateScope();
        var handler = CreateHandler(scope);

        var result = await handler.Handle(
            new UpdateProposedHostCommand(
                _visitRequestId, _visitInstanceId, HostSelectionModes.Selected, _candidateUserId, 0),
            CancellationToken.None);

        Assert.Equal(_candidateUserId, result.ProposedHostUserId);

        using var check = _factory.Services.CreateScope();
        var checkDb = check.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var row = await checkDb.VisitRequestCampuses.AsNoTracking()
            .FirstAsync(c => c.VisitInstanceId == _visitInstanceId);
        Assert.Equal(_candidateUserId, row.ProposedHostUserId);
        Assert.Equal(1, row.RowVersion);
    }

    private UpdateProposedHostCommandHandler CreateHandler(IServiceScope scope)
    {
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<ApplicationDbContext>();

        return new UpdateProposedHostCommandHandler(
            db,
            new StaticLeader(_leaderUserId, _campusId),
            sp.GetRequiredService<IDateTimeService>(),
            new MySqlUserMutationLockService(db));
    }

    /// <summary>The acting IC Staff Leader of the campus.</summary>
    private sealed class StaticLeader : ICurrentUserService
    {
        public StaticLeader(ulong userId, ulong campusId)
        {
            UserId = userId;
            PrimaryCampusId = campusId;
        }

        public ulong? UserId { get; }
        public string? Email => null;
        public string? RoleCode => RoleCodes.Staff;
        public string? SubRole => UserSubRoles.Leader;
        public ulong? RoleId => null;
        public ulong? PrimaryCampusId { get; }
        public ulong? DepartmentId => null;
        public bool IsAuthenticated => true;
        public ulong? SessionId => null;
        public string? LoginPortal => null;
    }
}
