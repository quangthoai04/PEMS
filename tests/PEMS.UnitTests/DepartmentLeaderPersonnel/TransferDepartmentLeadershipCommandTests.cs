using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.DepartmentLeaderPersonnel.Commands.TransferDepartmentLeadership;
using PEMS.Application.DepartmentLeaderPersonnel.Common;
using PEMS.Domain.Constants;
using Xunit;

namespace PEMS.UnitTests.DepartmentLeaderPersonnel;

/// <summary>
/// Leadership handover (spec §16).
///
/// The invariant: three writes — outgoing head → STAFF, incoming → LEADER, department.head_user_id
/// moves — commit together or not at all, so the department is never observed headless or
/// two-headed, and two concurrent transfers resolve to exactly one winner.
/// </summary>
public class TransferDepartmentLeadershipCommandTests
{
    private const ulong SuccessorId = 901;

    private static TransferDepartmentLeadershipCommandHandler Handler(DepartmentLeaderTestHarness h)
        => new(h.Db, h.Scope, h.Locks, h.Sessions, h.Email.Object, h.Clock);

    private static Task<TransferDepartmentLeadershipResponse> Run(
        DepartmentLeaderTestHarness h, ulong newLeaderUserId = SuccessorId)
        => Handler(h).Handle(
            new TransferDepartmentLeadershipCommand { NewLeaderUserId = newLeaderUserId },
            CancellationToken.None);

    [Fact]
    public async Task Valid_successor_takes_the_seat_and_the_outgoing_leader_becomes_staff()
    {
        var h = DepartmentLeaderTestHarness.Create();
        h.AddStaff(SuccessorId, status: UserStatuses.Active);

        var result = await Run(h);

        var outgoing = h.GetUser(DepartmentLeaderTestHarness.LeaderId);
        var incoming = h.GetUser(SuccessorId);

        Assert.Equal(UserSubRoles.Staff, outgoing.SubRole);
        Assert.Equal(UserSubRoles.Leader, incoming.SubRole);
        Assert.Equal(SuccessorId, h.GetDepartment().HeadUserId);

        Assert.True(result.Success);
        Assert.Equal(DepartmentLeaderTestHarness.LeaderId, result.PreviousLeaderUserId);
        Assert.Equal(SuccessorId, result.NewLeaderUserId);
        Assert.True(result.ActorMustSignInAgain);
    }

    /// <summary>
    /// Exactly one head at all times: the outgoing leader keeps DEPARTMENT role but loses LEADER, so
    /// the department never has two people claiming the seat.
    /// </summary>
    [Fact]
    public async Task Department_is_never_left_headless_or_two_headed()
    {
        var h = DepartmentLeaderTestHarness.Create();
        h.AddStaff(SuccessorId, status: UserStatuses.Active);

        await Run(h);

        var leadersInDepartment = h.Db.Users
            .Where(u => u.DepartmentId == DepartmentLeaderTestHarness.DepartmentId
                        && u.SubRole == UserSubRoles.Leader)
            .ToList();

        Assert.Single(leadersInDepartment);
        Assert.Equal(SuccessorId, leadersInDepartment[0].UserId);
        Assert.NotNull(h.GetDepartment().HeadUserId);
    }

    [Fact]
    public async Task Both_accounts_are_signed_out_so_neither_keeps_a_stale_role_claim()
    {
        var h = DepartmentLeaderTestHarness.Create();
        h.AddStaff(SuccessorId, status: UserStatuses.Active);
        h.AddActiveSession(5001, DepartmentLeaderTestHarness.LeaderId);
        h.AddActiveSession(5002, SuccessorId);

        var result = await Run(h);

        Assert.Equal(2, h.Sessions.RevokeAllCalls.Count);
        Assert.Contains(h.Sessions.RevokeAllCalls, c => c.UserId == DepartmentLeaderTestHarness.LeaderId);
        Assert.Contains(h.Sessions.RevokeAllCalls, c => c.UserId == SuccessorId);
        Assert.All(h.Sessions.RevokeAllCalls, c => Assert.Equal(SessionRevokeReasons.RoleChanged, c.Reason));
        Assert.Equal(2, result.RevokedSessions);
    }

    /// <summary>
    /// The concurrency guard: after the lock, the department's head is re-read. If another transfer
    /// already moved the seat, this caller is acting on a stale screen and gets 409 rather than
    /// overwriting the winner.
    /// </summary>
    [Fact]
    public async Task Losing_a_concurrent_transfer_yields_409_instead_of_overwriting_the_winner()
    {
        var h = DepartmentLeaderTestHarness.Create();
        h.AddStaff(SuccessorId, status: UserStatuses.Active);
        h.AddStaff(902, status: UserStatuses.Active);

        // Simulate the winner having committed first: the seat already moved to 902.
        var department = h.GetDepartment();
        department.HeadUserId = 902;
        h.Db.SaveChanges();
        h.Detach();

        // The caller's token still says LEADER, so the gate is what catches it first.
        var ex = await Assert.ThrowsAsync<AuthBusinessException>(() => Run(h));
        Assert.Equal(403, ex.StatusCode);
        Assert.Equal(DepartmentLeaderErrorCodes.DepartmentScopeForbidden, ex.ErrorCode);

        // The winner's outcome stands.
        Assert.Equal(902ul, h.GetDepartment().HeadUserId);
    }

    [Fact]
    public async Task Candidate_from_another_department_is_refused()
    {
        var h = DepartmentLeaderTestHarness.Create();
        h.AddOtherDepartment();
        h.AddStaff(
            SuccessorId,
            departmentId: DepartmentLeaderTestHarness.OtherDepartmentId,
            campusId: DepartmentLeaderTestHarness.OtherCampusId);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => Run(h));

        Assert.Equal(DepartmentLeaderErrorCodes.LeaderCandidateWrongDepartment, ex.ErrorCode);
        Assert.Equal(DepartmentLeaderTestHarness.LeaderId, h.GetDepartment().HeadUserId);
    }

    [Theory]
    [InlineData(UserStatuses.Inactive)]
    [InlineData(UserStatuses.PendingEmailConfirmation)]
    [InlineData(UserStatuses.Locked)]
    public async Task Candidate_that_cannot_sign_in_is_refused(string status)
    {
        var h = DepartmentLeaderTestHarness.Create();
        h.AddStaff(SuccessorId, status: status);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => Run(h));

        Assert.Equal(DepartmentLeaderErrorCodes.LeaderCandidateNotActive, ex.ErrorCode);
        // Nothing moved.
        Assert.Equal(DepartmentLeaderTestHarness.LeaderId, h.GetDepartment().HeadUserId);
        Assert.Equal(UserSubRoles.Leader, h.GetUser(DepartmentLeaderTestHarness.LeaderId).SubRole);
        Assert.Equal(UserSubRoles.Staff, h.GetUser(SuccessorId).SubRole);
    }

    [Fact]
    public async Task Missing_candidate_is_refused()
    {
        var h = DepartmentLeaderTestHarness.Create();

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => Run(h, 123456));

        Assert.Equal(DepartmentLeaderErrorCodes.LeaderCandidateInvalid, ex.ErrorCode);
        Assert.Equal(DepartmentLeaderTestHarness.LeaderId, h.GetDepartment().HeadUserId);
    }

    [Fact]
    public async Task Transferring_to_oneself_is_refused()
    {
        var h = DepartmentLeaderTestHarness.Create();

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(
            () => Run(h, DepartmentLeaderTestHarness.LeaderId));

        Assert.Equal(DepartmentLeaderErrorCodes.LeaderCandidateInvalid, ex.ErrorCode);
    }

    [Fact]
    public async Task Department_staff_caller_cannot_transfer_leadership()
    {
        var h = DepartmentLeaderTestHarness.Create();
        h.AddStaff(SuccessorId, status: UserStatuses.Active);
        h.Actor.SubRole = UserSubRoles.Staff;

        await Assert.ThrowsAsync<AuthBusinessException>(() => Run(h));
        Assert.Equal(DepartmentLeaderTestHarness.LeaderId, h.GetDepartment().HeadUserId);
    }

    [Fact]
    public async Task Both_users_and_the_department_are_locked_before_the_write()
    {
        var h = DepartmentLeaderTestHarness.Create();
        h.AddStaff(SuccessorId, status: UserStatuses.Active);

        await Run(h);

        // Both ids in ONE call, so the service can order them ascending and no deadlock is possible.
        var userBatch = Assert.Single(h.Locks.LockedUserBatches);
        Assert.Contains(DepartmentLeaderTestHarness.LeaderId, userBatch);
        Assert.Contains(SuccessorId, userBatch);

        Assert.Contains(
            h.Locks.LockedDepartmentBatches,
            batch => batch.Contains(DepartmentLeaderTestHarness.DepartmentId));
    }

    [Fact]
    public async Task Transfer_is_audited_against_the_department()
    {
        var h = DepartmentLeaderTestHarness.Create();
        h.AddStaff(SuccessorId, status: UserStatuses.Active);

        await Run(h);

        var audit = h.Db.AuditLogs.Single();
        Assert.Equal(DepartmentPersonnelAuditActions.TransferLeadership, audit.Action);
        Assert.Equal("Department", audit.EntityType);
        Assert.Equal(DepartmentLeaderTestHarness.DepartmentId, audit.EntityId);
        Assert.Contains(SuccessorId.ToString(), audit.Changes.Single().NewValueText!);
    }

    [Fact]
    public async Task Both_parties_are_notified_after_commit()
    {
        var h = DepartmentLeaderTestHarness.Create();
        h.AddStaff(SuccessorId, status: UserStatuses.Active, email: "successor@fpt.edu.vn");

        var result = await Run(h);

        Assert.Contains("successor@fpt.edu.vn", h.SentTo);
        Assert.Contains("leader@fpt.edu.vn", h.SentTo);
        Assert.Equal(DepartmentPersonnelEmails.StatusSent, result.EmailNotificationStatus);
    }

    /// <summary>A notification failure must not surface as a failed transfer — it already committed.</summary>
    [Fact]
    public async Task Notification_failure_does_not_roll_back_the_transfer()
    {
        var h = DepartmentLeaderTestHarness.Create();
        h.AddStaff(SuccessorId, status: UserStatuses.Active);
        h.MakeEmailFail();

        var result = await Run(h);

        Assert.True(result.Success);
        Assert.Equal(SuccessorId, h.GetDepartment().HeadUserId);
        Assert.Equal(DepartmentPersonnelEmails.StatusFailed, result.EmailNotificationStatus);
    }
}
