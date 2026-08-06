using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Interfaces;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Delegations;
using PEMS.Shared;

namespace PEMS.Application.Delegations.Services;

/// <summary>
/// One campus as the aggregate sees it. The status alone is not enough any more: the confirmation
/// gate asks whether the campus HAS a confirmed operational contact, which is a different question
/// from what its status says — a campus can sit at WAITING_REQUEST_APPROVAL and still be waiting on
/// a sibling's contact.
/// </summary>
public readonly record struct CampusAggregateInput(string Status, bool HasOperationalContact);

/// <summary>
/// What one recompute did to the GLOBAL confirmation gate.
///
/// <para>
/// The gate is not just a status: opening it is the moment every campus's Staff Leader may finally
/// see the request, and closing it is the moment they all stop seeing it again. Callers need to know
/// which of those happened so they can send (or not send) the approval-ready notification, and they
/// need <see cref="GateRevision"/> as the dedupe key for it — a retry inside one opening must not
/// mail twice, while a genuine re-opening must be allowed to mail again.
/// </para>
/// </summary>
/// <param name="Opened">The request was behind the gate and no longer is.</param>
/// <param name="Closed">The request was past the gate and has fallen back behind it.</param>
/// <param name="GateRevision">The revision after this recompute; bumped on either transition.</param>
public readonly record struct ContactGateTransition(bool Opened, bool Closed, uint GateRevision);

/// <summary>
/// Single source of truth for visit_requests.status (aggregate of campus-instance state).
/// Mirrors EXACTLY the DB triggers trg_visit_campuses_aggregate_ai/au so the tracked EF entity
/// never fights the trigger value: EF flushes visit_request_campuses before visit_requests
/// (table-name ordering), the trigger recomputes the aggregate, then EF's own visit_requests
/// UPDATE writes the same value we computed here.
/// </summary>
public interface IVisitRequestAggregateStatusService
{
    /// <summary>Computes the aggregate status from campus state (pure logic, no side effects).</summary>
    string Compute(string currentRequestStatus, IReadOnlyCollection<CampusAggregateInput> campuses);

    /// <summary>
    /// Recomputes and applies the aggregate onto an already-loaded, tracked request whose
    /// CampusInstances collection reflects the desired end state, bumping the contact-gate revision
    /// when the gate opens or closes. Returns what happened to the gate.
    /// </summary>
    ContactGateTransition Apply(VisitRequest visitRequest);

    /// <summary>Loads the request + instances and recomputes the aggregate (for callers that
    /// do not already track the aggregate). Does NOT call SaveChanges.</summary>
    Task<ContactGateTransition> RecalculateAsync(ulong visitRequestId, CancellationToken cancellationToken);
}

public sealed class VisitRequestAggregateStatusService : IVisitRequestAggregateStatusService
{
    private static readonly string[] ApprovedOrBeyond =
    {
        VisitInstanceStatus.Assigned,
        VisitInstanceStatus.BeforeVisit,
        VisitInstanceStatus.DuringVisit,
        VisitInstanceStatus.AfterVisit,
        VisitInstanceStatus.Closed,
    };

    /// <summary>Both gate stages count as "still awaiting this campus's decision".</summary>
    private static readonly string[] StillPending =
    {
        VisitInstanceStatus.WaitingContactConfirmation,
        VisitInstanceStatus.WaitingRequestApproval,
    };

    private readonly IApplicationDbContext _db;

    public VisitRequestAggregateStatusService(IApplicationDbContext db) => _db = db;

    public string Compute(string currentRequestStatus, IReadOnlyCollection<CampusAggregateInput> campuses)
    {
        // CANCELLED tổng là trạng thái cuối — không bao giờ override.
        if (currentRequestStatus == VisitRequestStatuses.Cancelled)
            return VisitRequestStatuses.Cancelled;

        // A cancelled campus leaves every denominator: nobody is waiting on its contact or decision.
        var active = campuses.Count(c => c.Status != VisitInstanceStatus.Cancelled);
        var unconfirmed = campuses.Count(c =>
            c.Status != VisitInstanceStatus.Cancelled && !c.HasOperationalContact);
        var approved = campuses.Count(c => ApprovedOrBeyond.Contains(c.Status));
        var pending = campuses.Count(c => StillPending.Contains(c.Status));
        var rejected = campuses.Count(c => c.Status == VisitInstanceStatus.Rejected);

        // ── The gate comes FIRST and beats every decision below it, exactly as the SQL trigger orders
        //    its CASE. One campus still missing its operational contact holds the whole request: a
        //    sibling that is otherwise ready must not become visible to its Staff Leader, because the
        //    gate is a property of the REQUEST, not of a campus. ──
        if (unconfirmed > 0)
            return VisitRequestStatuses.PendingContactConfirmation;

        if (active > 0 && rejected == active)
            return VisitRequestStatuses.Rejected;
        if (approved > 0 && pending > 0)
            return VisitRequestStatuses.PartiallyApproved;
        if (approved > 0)
            return VisitRequestStatuses.Approved;
        if (pending > 0)
            return VisitRequestStatuses.PendingApproval;

        // Every campus cancelled (or nothing to aggregate): leave the caller's status alone rather
        // than inventing one. Request-level cancellation is written by the cancel command itself.
        return currentRequestStatus;
    }

    public ContactGateTransition Apply(VisitRequest visitRequest)
    {
        var wasBehindGate = VisitRequestStatuses.IsBehindContactGate(visitRequest.Status);

        visitRequest.Status = Compute(
            visitRequest.Status,
            visitRequest.CampusInstances
                .Select(c => new CampusAggregateInput(c.Status, c.OperationalContactUserId is not null))
                .ToList());

        var isBehindGate = VisitRequestStatuses.IsBehindContactGate(visitRequest.Status);
        var opened = wasBehindGate && !isBehindGate;
        var closed = !wasBehindGate && isBehindGate;

        // The revision is the dedupe key for the approval-ready mail, so it must change on BOTH
        // transitions: bumping only on open would let a close-then-open cycle reuse a key that has
        // already been mailed, and the Staff Leaders would never hear that the request came back.
        if (opened || closed)
            visitRequest.ContactGateRevision += 1;

        return new ContactGateTransition(opened, closed, visitRequest.ContactGateRevision);
    }

    public async Task<ContactGateTransition> RecalculateAsync(
        ulong visitRequestId, CancellationToken cancellationToken)
    {
        var visit = await _db.VisitRequests
            .Include(v => v.CampusInstances)
            .FirstOrDefaultAsync(v => v.VisitRequestId == visitRequestId, cancellationToken);
        if (visit is null)
            return default;

        return Apply(visit);
    }
}
