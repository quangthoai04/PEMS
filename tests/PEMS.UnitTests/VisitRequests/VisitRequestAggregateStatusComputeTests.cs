using System.Linq;
using PEMS.Application.Delegations.Services;
using PEMS.Domain.Constants;
using PEMS.Shared;
using Xunit;

namespace PEMS.UnitTests.VisitRequests;

/// <summary>
/// Pure aggregate-status matrix. The service must mirror the SQL aggregate trigger exactly, so these
/// cases pin both halves of it:
///
///   • the CONFIRMATION GATE — while any active campus has no confirmed operational contact, the whole
///     request reads PENDING_CONTACT_CONFIRMATION whatever the campus decisions say;
///   • the DECISION aggregate once the gate is open — pending / partially approved / approved /
///     rejected, with BEFORE_VISIT counting as approved because that is exactly what an approval writes.
/// </summary>
public class VisitRequestAggregateStatusComputeTests
{
    private readonly VisitRequestAggregateStatusService _service = new(db: null!);

    /// <summary>Campuses whose contacts have all confirmed — the gate is open, so decisions decide.</summary>
    private string Compute(string current, params string[] statuses)
        => _service.Compute(current, statuses.Select(s => new CampusAggregateInput(s, true)).ToList());

    /// <summary>Campuses given explicitly as (status, hasConfirmedContact).</summary>
    private string ComputeWithContacts(string current, params (string Status, bool Confirmed)[] campuses)
        => _service.Compute(current,
            campuses.Select(c => new CampusAggregateInput(c.Status, c.Confirmed)).ToList());

    // ── The gate ────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void UnconfirmedCampus_HoldsWholeRequestAtGate()
        => Assert.Equal(VisitRequestStatuses.PendingContactConfirmation,
            ComputeWithContacts(VisitRequestStatuses.PendingContactConfirmation,
                (VisitInstanceStatus.WaitingContactConfirmation, false),
                (VisitInstanceStatus.WaitingContactConfirmation, false)));

    /// <summary>
    /// One campus confirmed, its sibling not. The confirmed campus is otherwise ready for its own
    /// Staff Leader and it still must not be visible: the gate is a property of the REQUEST, not of a
    /// campus.
    /// </summary>
    [Fact]
    public void PartiallyConfirmed_StillHoldsWholeRequestAtGate()
        => Assert.Equal(VisitRequestStatuses.PendingContactConfirmation,
            ComputeWithContacts(VisitRequestStatuses.PendingContactConfirmation,
                (VisitInstanceStatus.WaitingRequestApproval, true),
                (VisitInstanceStatus.WaitingContactConfirmation, false)));

    [Fact]
    public void AllConfirmed_OpensGateToPendingApproval()
        => Assert.Equal(VisitRequestStatuses.PendingApproval,
            ComputeWithContacts(VisitRequestStatuses.PendingContactConfirmation,
                (VisitInstanceStatus.WaitingRequestApproval, true),
                (VisitInstanceStatus.WaitingRequestApproval, true)));

    /// <summary>A cancelled campus leaves the denominator: nobody is waiting on a contact for it.</summary>
    [Fact]
    public void CancelledCampusWithoutContact_DoesNotHoldGate()
        => Assert.Equal(VisitRequestStatuses.PendingApproval,
            ComputeWithContacts(VisitRequestStatuses.PendingContactConfirmation,
                (VisitInstanceStatus.WaitingRequestApproval, true),
                (VisitInstanceStatus.Cancelled, false)));

    // ── Decisions, once the gate is open ────────────────────────────────────────────────────────

    [Fact]
    public void AllPending_IsPendingApproval()
        => Assert.Equal(VisitRequestStatuses.PendingApproval,
            Compute(VisitRequestStatuses.PendingApproval,
                VisitInstanceStatus.WaitingRequestApproval,
                VisitInstanceStatus.WaitingRequestApproval));

    [Fact]
    public void SingleApprovedCampus_IsApproved()
        => Assert.Equal(VisitRequestStatuses.Approved,
            Compute(VisitRequestStatuses.PendingApproval,
                VisitInstanceStatus.BeforeVisit));

    [Fact]
    public void MixedApprovedAndPending_IsPartiallyApproved()
        => Assert.Equal(VisitRequestStatuses.PartiallyApproved,
            Compute(VisitRequestStatuses.PendingApproval,
                VisitInstanceStatus.BeforeVisit,
                VisitInstanceStatus.WaitingRequestApproval));

    [Fact]
    public void AllApproved_IsApproved()
        => Assert.Equal(VisitRequestStatuses.Approved,
            Compute(VisitRequestStatuses.PendingApproval,
                VisitInstanceStatus.BeforeVisit,
                VisitInstanceStatus.BeforeVisit));

    [Theory]
    [InlineData(VisitInstanceStatus.Assigned)]
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
    public void ApprovedPlusRejected_IsApproved()
        => Assert.Equal(VisitRequestStatuses.Approved,
            Compute(VisitRequestStatuses.PendingApproval,
                VisitInstanceStatus.BeforeVisit,
                VisitInstanceStatus.Rejected));

    [Fact]
    public void CancelledRequest_StaysCancelled()
        => Assert.Equal(VisitRequestStatuses.Cancelled,
            Compute(VisitRequestStatuses.Cancelled,
                VisitInstanceStatus.BeforeVisit));
}
