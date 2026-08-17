using System.Threading;
using System.Threading.Tasks;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.DepartmentLeaderPersonnel.Common;
using PEMS.Domain.Constants;
using Xunit;

namespace PEMS.UnitTests.DepartmentLeaderPersonnel;

/// <summary>
/// SEC-09 remediation. <see cref="DepartmentLeadershipTransferService"/> is the shared transactional
/// core behind BOTH the self-service transfer (<c>actorMustBeCurrentLeader: true</c>) and the legacy
/// third-party reassignment (<c>actorMustBeCurrentLeader: false</c>). These tests exercise the
/// service directly — decoupled from either caller's own scope check — to prove its own concurrency
/// contract: the department's HeadUserId is ALWAYS re-verified against the caller's pre-lock read,
/// unconditionally, for both actor shapes, not merely when the actor claims to be the head.
/// </summary>
public class DepartmentLeadershipTransferServiceTests
{
    private const ulong ThirdPartyActorId = 950; // e.g. a Staff Leader or HO, distinct from both leaders

    private static DepartmentLeadershipTransferService Service(DepartmentLeaderTestHarness h)
        => new(h.Db, h.Locks, h.Sessions, h.Dispatcher, h.Clock);

    [Fact]
    public async Task ThirdParty_transfer_moves_the_seat_and_audits_the_real_actor()
    {
        var h = DepartmentLeaderTestHarness.Create();
        h.AddStaff(DepartmentLeaderTestHarness.StaffId, status: UserStatuses.Active);

        var result = await Service(h).TransferAsync(
            DepartmentLeaderTestHarness.DepartmentId,
            expectedCurrentLeaderUserId: DepartmentLeaderTestHarness.LeaderId,
            newLeaderUserId: DepartmentLeaderTestHarness.StaffId,
            actorUserId: ThirdPartyActorId,
            actorMustBeCurrentLeader: false,
            CancellationToken.None);

        Assert.Equal(DepartmentLeaderTestHarness.StaffId, h.GetDepartment().HeadUserId);
        Assert.Equal(UserSubRoles.Staff, h.GetUser(DepartmentLeaderTestHarness.LeaderId).SubRole);
        Assert.Equal(UserSubRoles.Leader, h.GetUser(DepartmentLeaderTestHarness.StaffId).SubRole);
        Assert.Equal(DepartmentLeaderTestHarness.StaffId, result.NewLeaderUserId);

        // The audit trail records WHO actually performed the action — the third-party actor, not the
        // outgoing leader — which the old legacy handler never recorded at all.
        var audit = h.Db.AuditLogs.Single();
        Assert.Equal(ThirdPartyActorId, audit.ActorUserId);
    }

    [Fact]
    public async Task ThirdParty_locks_both_leaders_in_one_call_before_the_department()
    {
        var h = DepartmentLeaderTestHarness.Create();
        h.AddStaff(DepartmentLeaderTestHarness.StaffId, status: UserStatuses.Active);

        await Service(h).TransferAsync(
            DepartmentLeaderTestHarness.DepartmentId,
            DepartmentLeaderTestHarness.LeaderId, DepartmentLeaderTestHarness.StaffId,
            ThirdPartyActorId, actorMustBeCurrentLeader: false, CancellationToken.None);

        var userBatch = Assert.Single(h.Locks.LockedUserBatches);
        Assert.Contains(DepartmentLeaderTestHarness.LeaderId, userBatch);
        Assert.Contains(DepartmentLeaderTestHarness.StaffId, userBatch);
        Assert.Contains(h.Locks.LockedDepartmentBatches, b => b.Contains(DepartmentLeaderTestHarness.DepartmentId));
    }

    /// <summary>
    /// The core rev.5 correction: a third-party caller only ever knows departmentId + the desired
    /// successor — never the current head's id with certainty — so a seat that moved between the
    /// caller's own pre-read and this call must be caught here, unconditionally, not only when
    /// actorMustBeCurrentLeader is true.
    /// </summary>
    [Fact]
    public async Task ThirdParty_seat_moved_under_the_lock_yields_conflict_not_a_wrong_mutation()
    {
        var h = DepartmentLeaderTestHarness.Create();
        h.AddStaff(DepartmentLeaderTestHarness.StaffId, status: UserStatuses.Active);
        h.AddStaff(902, status: UserStatuses.Active);

        // Simulate a winner having already committed: the seat moved to 902 after the caller's own
        // pre-read (which is what handed this stale value in as expectedCurrentLeaderUserId).
        var department = h.GetDepartment();
        department.HeadUserId = 902;
        h.Db.SaveChanges();
        h.Detach();

        var ex = await Assert.ThrowsAsync<ConflictException>(() => Service(h).TransferAsync(
            DepartmentLeaderTestHarness.DepartmentId,
            expectedCurrentLeaderUserId: DepartmentLeaderTestHarness.LeaderId, // stale
            newLeaderUserId: DepartmentLeaderTestHarness.StaffId,
            actorUserId: ThirdPartyActorId,
            actorMustBeCurrentLeader: false,
            CancellationToken.None));

        Assert.Equal(DepartmentLeaderErrorCodes.LeadershipAlreadyChanged, ex.ErrorCode);
        // The winner's outcome stands — nothing was demoted or promoted based on the stale read; the
        // mismatch is caught before any of the three writes, so the old leader is still LEADER.
        Assert.Equal(902ul, h.GetDepartment().HeadUserId);
        Assert.Equal(UserSubRoles.Leader, h.GetUser(DepartmentLeaderTestHarness.LeaderId).SubRole);
    }

    /// <summary>
    /// The same race, but for the self-service shape (actorMustBeCurrentLeader: true) where the actor
    /// IS the stale expected leader — proves the unconditional re-check fires before, and regardless
    /// of, the additional actorMustBeCurrentLeader check.
    /// </summary>
    [Fact]
    public async Task SelfService_seat_moved_under_the_lock_yields_conflict_before_the_actor_check()
    {
        var h = DepartmentLeaderTestHarness.Create();
        h.AddStaff(DepartmentLeaderTestHarness.StaffId, status: UserStatuses.Active);
        h.AddStaff(902, status: UserStatuses.Active);

        var department = h.GetDepartment();
        department.HeadUserId = 902;
        h.Db.SaveChanges();
        h.Detach();

        var ex = await Assert.ThrowsAsync<ConflictException>(() => Service(h).TransferAsync(
            DepartmentLeaderTestHarness.DepartmentId,
            expectedCurrentLeaderUserId: DepartmentLeaderTestHarness.LeaderId,
            newLeaderUserId: DepartmentLeaderTestHarness.StaffId,
            actorUserId: DepartmentLeaderTestHarness.LeaderId, // actor == expected (self-service shape)
            actorMustBeCurrentLeader: true,
            CancellationToken.None));

        Assert.Equal(DepartmentLeaderErrorCodes.LeadershipAlreadyChanged, ex.ErrorCode);
    }

    [Fact]
    public async Task SelfService_actor_mismatch_under_lock_is_forbidden()
    {
        // expectedCurrentLeaderUserId is still correct (no race), but the actor calling is not that
        // account — a defensive check for a race between the caller's own scope check and this call.
        var h = DepartmentLeaderTestHarness.Create();
        h.AddStaff(DepartmentLeaderTestHarness.StaffId, status: UserStatuses.Active);

        await Assert.ThrowsAsync<ForbiddenException>(() => Service(h).TransferAsync(
            DepartmentLeaderTestHarness.DepartmentId,
            expectedCurrentLeaderUserId: DepartmentLeaderTestHarness.LeaderId,
            newLeaderUserId: DepartmentLeaderTestHarness.StaffId,
            actorUserId: 999999, // not the leader
            actorMustBeCurrentLeader: true,
            CancellationToken.None));

        // Nothing moved.
        Assert.Equal(DepartmentLeaderTestHarness.LeaderId, h.GetDepartment().HeadUserId);
    }

    [Fact]
    public async Task Candidate_equal_to_expected_leader_is_refused_before_any_lock()
    {
        var h = DepartmentLeaderTestHarness.Create();

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => Service(h).TransferAsync(
            DepartmentLeaderTestHarness.DepartmentId,
            expectedCurrentLeaderUserId: DepartmentLeaderTestHarness.LeaderId,
            newLeaderUserId: DepartmentLeaderTestHarness.LeaderId,
            actorUserId: ThirdPartyActorId,
            actorMustBeCurrentLeader: false,
            CancellationToken.None));

        Assert.Equal(DepartmentLeaderErrorCodes.LeaderCandidateInvalid, ex.ErrorCode);
        Assert.Empty(h.Locks.LockedUserBatches); // refused before taking any lock
    }

    [Fact]
    public async Task ThirdParty_candidate_from_another_department_is_refused()
    {
        var h = DepartmentLeaderTestHarness.Create();
        h.AddOtherDepartment();
        h.AddStaff(
            DepartmentLeaderTestHarness.StaffId,
            departmentId: DepartmentLeaderTestHarness.OtherDepartmentId,
            campusId: DepartmentLeaderTestHarness.OtherCampusId);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => Service(h).TransferAsync(
            DepartmentLeaderTestHarness.DepartmentId,
            DepartmentLeaderTestHarness.LeaderId, DepartmentLeaderTestHarness.StaffId,
            ThirdPartyActorId, actorMustBeCurrentLeader: false, CancellationToken.None));

        Assert.Equal(DepartmentLeaderErrorCodes.LeaderCandidateWrongDepartment, ex.ErrorCode);
        Assert.Equal(DepartmentLeaderTestHarness.LeaderId, h.GetDepartment().HeadUserId);
    }
}
