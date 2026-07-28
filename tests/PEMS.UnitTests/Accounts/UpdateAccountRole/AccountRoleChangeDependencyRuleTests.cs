using PEMS.Application.Accounts.Common;
using PEMS.Domain.Constants;

namespace PEMS.UnitTests.Accounts.UpdateAccountRole;

/// <summary>
/// The blocker matrix on its own, with no database in the way (spec §18). These tests are the
/// specification of WHICH rows count as a live responsibility;
/// <see cref="UpdateAccountRoleCommandHandlerTests"/> then proves the handler wires the same rule to
/// the same 409. Keeping both means a change to the matrix cannot pass by only fixing one of them.
/// </summary>
public class AccountRoleChangeDependencyRuleTests
{
    private const ulong Target = 100;
    private const ulong Other = 200;
    private const ulong VisitId = 5001;

    private static AccountRoleChangeImpact Build(
        IEnumerable<HostAssignmentCandidate>? hosts = null,
        IEnumerable<CoordinatorAssignmentCandidate>? coordinators = null,
        IEnumerable<ParticipantResponsibilityCandidate>? participants = null,
        IEnumerable<LogisticsResponsibilityCandidate>? logistics = null,
        DepartmentHeadCandidate? head = null)
        => AccountRoleChangeDependencyRule.BuildImpact(
            Target,
            hosts ?? Array.Empty<HostAssignmentCandidate>(),
            coordinators ?? Array.Empty<CoordinatorAssignmentCandidate>(),
            participants ?? Array.Empty<ParticipantResponsibilityCandidate>(),
            logistics ?? Array.Empty<LogisticsResponsibilityCandidate>(),
            head);

    // ── §18.1 Visit status matrix ─────────────────────────────────────────────

    [Theory]
    [InlineData(VisitInstanceStatuses.WaitingRequestApproval, true)]
    [InlineData(VisitInstanceStatuses.Assigned, true)]
    [InlineData(VisitInstanceStatuses.BeforeVisit, true)]
    [InlineData(VisitInstanceStatuses.DuringVisit, true)]
    [InlineData(VisitInstanceStatuses.AfterVisit, true)]
    [InlineData(VisitInstanceStatuses.Closed, false)]
    [InlineData(VisitInstanceStatuses.Cancelled, false)]
    [InlineData(VisitInstanceStatuses.Rejected, false)]
    public void VisitStatusMatrix_DecidesWhetherAResponsibilityIsStillLive(string status, bool expectedActive)
        => Assert.Equal(expectedActive, AccountRoleChangeDependencyRule.IsActiveVisit(status));

    [Fact]
    public void WaitingRequestApproval_BlocksEvenThoughUc106TreatsItAsNonOperational()
    {
        // UC-106 excludes WAITING_REQUEST_APPROVAL on purpose (no official host exists yet). A role
        // change cannot borrow that allowlist: a responsibility recorded against a not-yet-approved
        // instance is anomalous data, and stripping the account's permissions is the wrong response
        // to an anomaly (spec §7).
        var impact = Build(hosts: new[]
        {
            new HostAssignmentCandidate
            {
                VisitInstanceId = VisitId,
                VisitStatus = VisitInstanceStatuses.WaitingRequestApproval,
            },
        });

        Assert.False(impact.CanChangeRole);
    }

    // ── §18.2 Host / Coordinator ──────────────────────────────────────────────

    [Fact]
    public void ActiveHost_ProducesHostBlocker()
    {
        var impact = Build(hosts: new[]
        {
            new HostAssignmentCandidate { VisitInstanceId = VisitId, VisitStatus = VisitInstanceStatuses.Assigned },
        });

        var blocker = Assert.Single(impact.Blockers);
        Assert.Equal(AccountRoleChangeDependencyRule.ActiveHostAssignmentsBlockerType, blocker.Type);
        Assert.Equal(1, blocker.Count);
        Assert.Equal(1, blocker.AffectedVisitCount);
        Assert.Equal(new ulong[] { VisitId }, blocker.SampleVisitInstanceIds);
        Assert.Equal(1, impact.AffectedVisitCount);
    }

    [Fact]
    public void ActiveCoordinator_ProducesCoordinatorBlocker()
    {
        var impact = Build(coordinators: new[]
        {
            new CoordinatorAssignmentCandidate { VisitInstanceId = VisitId, VisitStatus = VisitInstanceStatuses.DuringVisit },
        });

        var blocker = Assert.Single(impact.Blockers);
        Assert.Equal(AccountRoleChangeDependencyRule.ActiveCoordinatorAssignmentsBlockerType, blocker.Type);
    }

    [Fact]
    public void ClosedHost_AndCancelledCoordinator_ProduceNoBlocker()
    {
        var impact = Build(
            hosts: new[] { new HostAssignmentCandidate { VisitInstanceId = VisitId, VisitStatus = VisitInstanceStatuses.Closed } },
            coordinators: new[] { new CoordinatorAssignmentCandidate { VisitInstanceId = VisitId + 1, VisitStatus = VisitInstanceStatuses.Cancelled } });

        Assert.True(impact.CanChangeRole);
        Assert.Equal(0, impact.AffectedVisitCount);
    }

    // ── §18.3 Participants ────────────────────────────────────────────────────

    private static ParticipantResponsibilityCandidate Participant(
        string status, string role = ParticipantRoles.IcSupport,
        string visitStatus = VisitInstanceStatuses.Assigned,
        ulong? instanceHost = null, ulong visitId = VisitId,
        bool delegated = false) => new()
    {
        VisitInstanceId = visitId,
        ParticipantRole = role,
        ParticipantStatus = status,
        VisitStatus = visitStatus,
        InstanceHostUserId = instanceHost,
        DelegatedToSubstitute = delegated,
    };

    [Fact]
    public void InvitedParticipant_ProducesPendingInvitationBlocker()
    {
        var impact = Build(participants: new[] { Participant(ParticipantStatuses.Invited) });

        var blocker = Assert.Single(impact.Blockers);
        Assert.Equal(AccountRoleChangeDependencyRule.PendingParticipantInvitationsBlockerType, blocker.Type);
    }

    [Theory]
    [InlineData(ParticipantStatuses.Accepted)]
    [InlineData(ParticipantStatuses.Assigned)]
    public void AcceptedOrAssignedParticipant_ProducesActiveParticipationBlocker(string status)
    {
        var impact = Build(participants: new[] { Participant(status) });

        var blocker = Assert.Single(impact.Blockers);
        Assert.Equal(AccountRoleChangeDependencyRule.ActiveVisitParticipationsBlockerType, blocker.Type);
    }

    [Theory]
    [InlineData(ParticipantStatuses.Declined)]
    [InlineData(ParticipantStatuses.Removed)]
    public void ResolvedParticipant_ProducesNoBlocker(string status)
        => Assert.True(Build(participants: new[] { Participant(status) }).CanChangeRole);

    [Theory]
    [InlineData(ParticipantRoles.IcSupport)]
    [InlineData(ParticipantRoles.DeptSupport)]
    [InlineData(ParticipantRoles.Student)]
    public void EverySupportingParticipantRole_Blocks(string participantRole)
        => Assert.False(Build(participants: new[]
        {
            Participant(ParticipantStatuses.Accepted, participantRole),
        }).CanChangeRole);

    [Fact]
    public void CanonicalHostParticipantRow_IsNotCountedASecondTime()
    {
        var impact = Build(
            hosts: new[] { new HostAssignmentCandidate { VisitInstanceId = VisitId, VisitStatus = VisitInstanceStatuses.Assigned } },
            participants: new[] { Participant(ParticipantStatuses.Assigned, ParticipantRoles.IcHost, instanceHost: Target) });

        var blocker = Assert.Single(impact.Blockers);
        Assert.Equal(AccountRoleChangeDependencyRule.ActiveHostAssignmentsBlockerType, blocker.Type);
        Assert.Equal(1, impact.AffectedVisitCount);
    }

    [Fact]
    public void OrphanIcHostRow_StillBlocks_FailClosed()
    {
        // IC_HOST row while the instance points at somebody else. Nothing here repairs the data —
        // it refuses the role change and leaves the anomaly for a human (spec §8.4).
        var impact = Build(participants: new[]
        {
            Participant(ParticipantStatuses.Assigned, ParticipantRoles.IcHost, instanceHost: Other),
        });

        var blocker = Assert.Single(impact.Blockers);
        Assert.Equal(AccountRoleChangeDependencyRule.ActiveVisitParticipationsBlockerType, blocker.Type);
    }

    // ── §18.3b Department Leader who has delegated the visit to their staff ───

    [Fact]
    public void DelegatedDeptSupportRow_DoesNotBlock()
    {
        // Assigning a staff member leaves the leader's own row at ASSIGNED (there is no DELEGATED
        // status), which used to read as "still personally on the hook" even though the reception
        // screen already shows the staff member as the responsible one.
        var impact = Build(participants: new[]
        {
            Participant(ParticipantStatuses.Assigned, ParticipantRoles.DeptSupport, delegated: true),
        });

        Assert.True(impact.CanChangeRole);
        Assert.Equal(0, impact.AffectedVisitCount);
    }

    [Fact]
    public void DelegatedLeader_WhoThenAcceptsPersonally_BlocksAgain()
    {
        // ACCEPTED is only reachable by the leader acting on their own invitation, so it outranks
        // the delegation: they are attending this visit themselves.
        var impact = Build(participants: new[]
        {
            Participant(ParticipantStatuses.Accepted, ParticipantRoles.DeptSupport, delegated: true),
        });

        var blocker = Assert.Single(impact.Blockers);
        Assert.Equal(AccountRoleChangeDependencyRule.ActiveVisitParticipationsBlockerType, blocker.Type);
    }

    [Fact]
    public void DelegationFlag_IsNotAGeneralExemption_ForOtherParticipantRoles()
    {
        // Only the DEPT_SUPPORT hand-down has a substitute. An IC_SUPPORT row on a visit the user
        // happens to have delegated something else on is still their own responsibility.
        var impact = Build(participants: new[]
        {
            Participant(ParticipantStatuses.Assigned, ParticipantRoles.IcSupport, delegated: true),
        });

        Assert.False(impact.CanChangeRole);
    }

    [Fact]
    public void DelegationExemption_IsPerVisit()
    {
        // Handing visit A to a staff member says nothing about visit B.
        var impact = Build(participants: new[]
        {
            Participant(ParticipantStatuses.Assigned, ParticipantRoles.DeptSupport, delegated: true),
            Participant(ParticipantStatuses.Assigned, ParticipantRoles.DeptSupport, visitId: VisitId + 1),
        });

        var blocker = Assert.Single(impact.Blockers);
        Assert.Equal(AccountRoleChangeDependencyRule.ActiveVisitParticipationsBlockerType, blocker.Type);
        Assert.Equal(1, blocker.Count);
        Assert.Equal(new ulong[] { VisitId + 1 }, blocker.SampleVisitInstanceIds);
    }

    // ── §18.4 Logistics ───────────────────────────────────────────────────────

    private static LogisticsResponsibilityCandidate Logistics(
        string status, ulong? assignedTo = null, ulong? receivedBy = null,
        string visitStatus = VisitInstanceStatuses.Assigned,
        ulong itemId = 1, ulong visitId = VisitId) => new()
    {
        LogisticsItemId = itemId,
        VisitInstanceId = visitId,
        LogisticsStatus = status,
        VisitStatus = visitStatus,
        AssignedToUserId = assignedTo,
        ReceivedBy = receivedBy,
    };

    [Theory]
    [InlineData("REQUESTED")]
    [InlineData("CHANGE_PROPOSED")]
    [InlineData("ASSIGNED")]
    [InlineData("ACCEPTED")]
    [InlineData("IN_PROGRESS")]
    public void AssigneeOnAnActiveStatus_Blocks(string status)
        => Assert.False(Build(logistics: new[] { Logistics(status, assignedTo: Target) }).CanChangeRole);

    [Theory]
    [InlineData("DONE")]
    [InlineData("REJECTED")]
    [InlineData("DECLINED")]
    [InlineData("CANCELLED")]
    public void AssigneeOnATerminalStatus_DoesNotBlock(string status)
        => Assert.True(Build(logistics: new[] { Logistics(status, assignedTo: Target) }).CanChangeRole);

    [Theory]
    [InlineData("REQUESTED")]
    [InlineData("CHANGE_PROPOSED")]
    public void ReceiverOfAnUnassignedItem_Blocks(string status)
        => Assert.False(Build(logistics: new[] { Logistics(status, assignedTo: null, receivedBy: Target) }).CanChangeRole);

    [Fact]
    public void ReceiverOfAnItemAssignedToSomeoneElse_DoesNotBlock()
    {
        // Once the item is somebody else's to do, received_by is a record of who took the request in
        // — history, not a duty this account still owes (spec §8.5).
        var impact = Build(logistics: new[] { Logistics("IN_PROGRESS", assignedTo: Other, receivedBy: Target) });
        Assert.True(impact.CanChangeRole);
    }

    [Fact]
    public void ReceiverOfAnItemAlreadyAssigned_DoesNotBlock_EvenWhileStillRequested()
    {
        var impact = Build(logistics: new[] { Logistics("REQUESTED", assignedTo: Other, receivedBy: Target) });
        Assert.True(impact.CanChangeRole);
    }

    [Fact]
    public void SameItemAsBothReceiverAndAssignee_CountsOnce()
    {
        var impact = Build(logistics: new[] { Logistics("REQUESTED", assignedTo: Target, receivedBy: Target) });

        var blocker = Assert.Single(impact.Blockers);
        Assert.Equal(1, blocker.Count);
        Assert.Equal(1, blocker.AffectedVisitCount);
    }

    [Fact]
    public void ActiveLogisticsOnATerminalVisit_DoesNotBlock()
        => Assert.True(Build(logistics: new[]
        {
            Logistics("IN_PROGRESS", assignedTo: Target, visitStatus: VisitInstanceStatuses.Closed),
        }).CanChangeRole);

    // ── §18.5 Department head ─────────────────────────────────────────────────

    [Fact]
    public void DepartmentHead_ProducesItsOwnBlocker_NamingTheDepartment()
    {
        var impact = Build(head: new DepartmentHeadCandidate
        {
            DepartmentId = 60,
            DepartmentName = "Phòng Công tác Sinh viên",
        });

        var blocker = Assert.Single(impact.Blockers);
        Assert.Equal(AccountRoleChangeDependencyRule.DepartmentHeadAssignmentBlockerType, blocker.Type);
        Assert.Contains("Phòng Công tác Sinh viên", blocker.Message);
        Assert.Equal(0, blocker.AffectedVisitCount);
    }

    // ── §18.6 Multi-blocker aggregation ───────────────────────────────────────

    [Fact]
    public void EveryBlockerIsReturnedTogether_AndVisitsAreCountedDistinctly()
    {
        var impact = Build(
            hosts: new[] { new HostAssignmentCandidate { VisitInstanceId = 1, VisitStatus = VisitInstanceStatuses.Assigned } },
            coordinators: new[] { new CoordinatorAssignmentCandidate { VisitInstanceId = 2, VisitStatus = VisitInstanceStatuses.BeforeVisit } },
            participants: new[]
            {
                Participant(ParticipantStatuses.Invited, visitId: 2),
                Participant(ParticipantStatuses.Accepted, visitId: 3),
            },
            logistics: new[]
            {
                Logistics("ASSIGNED", assignedTo: Target, itemId: 10, visitId: 3),
                Logistics("ACCEPTED", assignedTo: Target, itemId: 11, visitId: 4),
            },
            head: new DepartmentHeadCandidate { DepartmentId = 60, DepartmentName = "Phòng Hành chính" });

        Assert.Equal(6, impact.Blockers.Count);
        // Visits 1..4 — NOT the sum of the per-blocker counts, which would double-count 2 and 3.
        Assert.Equal(4, impact.AffectedVisitCount);
        Assert.All(impact.Blockers, b => Assert.False(string.IsNullOrWhiteSpace(b.Message)));
    }

    [Fact]
    public void SampleVisitIds_AreDistinctAndCapped()
    {
        var hosts = Enumerable.Range(1, 12)
            .Select(i => new HostAssignmentCandidate
            {
                VisitInstanceId = (ulong)i,
                VisitStatus = VisitInstanceStatuses.Assigned,
            })
            .ToList();

        var blocker = Assert.Single(Build(hosts: hosts).Blockers);
        Assert.Equal(12, blocker.Count);
        Assert.Equal(12, blocker.AffectedVisitCount);
        Assert.Equal(AccountRoleChangeDependencyRule.SampleVisitInstanceLimit, blocker.SampleVisitInstanceIds.Count);
        Assert.Equal(blocker.SampleVisitInstanceIds.Distinct().Count(), blocker.SampleVisitInstanceIds.Count);
    }

    [Fact]
    public void SummaryMessage_ListsEveryBlocker_AndTellsTheUserWhatToDo()
    {
        var impact = Build(
            hosts: new[] { new HostAssignmentCandidate { VisitInstanceId = 1, VisitStatus = VisitInstanceStatuses.Assigned } },
            head: new DepartmentHeadCandidate { DepartmentId = 60, DepartmentName = "Phòng Hành chính" });

        var message = AccountRoleChangeDependencyRule.BuildSummaryMessage(impact);

        Assert.Contains("Host", message);
        Assert.Contains("Phòng Hành chính", message);
        Assert.Contains("chuyển giao", message);
    }

    [Fact]
    public void NoResponsibilities_MeansTheRoleMayChange()
    {
        var impact = Build();
        Assert.True(impact.CanChangeRole);
        Assert.Empty(impact.Blockers);
        Assert.Equal(0, impact.AffectedVisitCount);
    }
}
