using System.Collections.Generic;
using PEMS.Application.Delegations.Services;
using PEMS.Domain.Constants;
using PEMS.Shared;
using Xunit;

namespace PEMS.UnitTests.VisitRequests;

/// <summary>
/// Pure aggregate-status matrix for the actor-relation create flows. The service must mirror
/// the SQL aggregate trigger exactly; these cases pin the initial states an authenticated
/// create can produce:
///   - all campuses SEND_FOR_REVIEW  → PENDING_APPROVAL
///   - single own-campus direct-processed → APPROVED
///   - own campus processed + other campuses pending → PARTIALLY_APPROVED
/// plus the pre-existing rejected/cancelled combinations (no regression).
/// </summary>
public class VisitRequestAggregateStatusComputeTests
{
    private readonly VisitRequestAggregateStatusService _service = new(db: null!);

    private string Compute(string current, params string[] statuses)
        => _service.Compute(current, new List<string>(statuses));

    [Fact]
    public void AllPending_IsPendingApproval()
        => Assert.Equal(VisitRequestStatuses.PendingApproval,
            Compute(VisitRequestStatuses.PendingApproval,
                VisitInstanceStatus.WaitingRequestApproval,
                VisitInstanceStatus.WaitingRequestApproval));

    [Fact]
    public void SingleDirectAssigned_IsApproved()
        => Assert.Equal(VisitRequestStatuses.Approved,
            Compute(VisitRequestStatuses.PendingApproval,
                VisitInstanceStatus.Assigned));

    [Fact]
    public void MixedAssignedAndPending_IsPartiallyApproved()
        => Assert.Equal(VisitRequestStatuses.PartiallyApproved,
            Compute(VisitRequestStatuses.PendingApproval,
                VisitInstanceStatus.Assigned,
                VisitInstanceStatus.WaitingRequestApproval));

    [Fact]
    public void AllAssigned_IsApproved()
        => Assert.Equal(VisitRequestStatuses.Approved,
            Compute(VisitRequestStatuses.PendingApproval,
                VisitInstanceStatus.Assigned,
                VisitInstanceStatus.Assigned));

    [Theory]
    [InlineData(VisitInstanceStatus.BeforeVisit)]
    [InlineData(VisitInstanceStatus.DuringVisit)]
    [InlineData(VisitInstanceStatus.AfterVisit)]
    [InlineData(VisitInstanceStatus.Closed)]
    public void OperationalStatuses_CountAsApproved(string status)
        => Assert.Equal(VisitRequestStatuses.Approved,
            Compute(VisitRequestStatuses.PendingApproval, status));

    // ── No regression on the existing decision combinations ──

    [Fact]
    public void AllRejected_IsRejected()
        => Assert.Equal(VisitRequestStatuses.Rejected,
            Compute(VisitRequestStatuses.PendingApproval,
                VisitInstanceStatus.Rejected,
                VisitInstanceStatus.Rejected));

    [Fact]
    public void RejectedPlusPending_IsPendingApproval()
        => Assert.Equal(VisitRequestStatuses.PendingApproval,
            Compute(VisitRequestStatuses.PendingApproval,
                VisitInstanceStatus.Rejected,
                VisitInstanceStatus.WaitingRequestApproval));

    [Fact]
    public void AssignedPlusRejected_IsApproved()
        => Assert.Equal(VisitRequestStatuses.Approved,
            Compute(VisitRequestStatuses.PendingApproval,
                VisitInstanceStatus.Assigned,
                VisitInstanceStatus.Rejected));

    [Fact]
    public void CancelledRequest_StaysCancelled()
        => Assert.Equal(VisitRequestStatuses.Cancelled,
            Compute(VisitRequestStatuses.Cancelled,
                VisitInstanceStatus.Assigned));
}
