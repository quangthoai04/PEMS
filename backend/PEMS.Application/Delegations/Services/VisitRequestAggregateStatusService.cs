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
/// Single source of truth for visit_requests.status (aggregate of campus-instance decisions).
/// Mirrors EXACTLY the DB triggers trg_visit_campuses_aggregate_ai/au so the tracked EF entity
/// never fights the trigger value: EF flushes visit_request_campuses before visit_requests
/// (table-name ordering), the trigger recomputes the aggregate, then EF's own visit_requests
/// UPDATE writes the same value we computed here.
/// </summary>
public interface IVisitRequestAggregateStatusService
{
    /// <summary>Computes the aggregate status from campus-instance statuses (pure logic).</summary>
    string Compute(string currentRequestStatus, IReadOnlyCollection<string> campusStatuses);

    /// <summary>Recomputes and applies the aggregate onto an already-loaded, tracked request
    /// whose CampusInstances collection reflects the desired end state.</summary>
    void Apply(VisitRequest visitRequest);

    /// <summary>Loads the request + instances and recomputes the aggregate (for callers that
    /// do not already track the aggregate). Does NOT call SaveChanges.</summary>
    Task RecalculateAsync(ulong visitRequestId, CancellationToken cancellationToken);
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

    private readonly IApplicationDbContext _db;

    public VisitRequestAggregateStatusService(IApplicationDbContext db) => _db = db;

    public string Compute(string currentRequestStatus, IReadOnlyCollection<string> campusStatuses)
    {
        // CANCELLED tổng là trạng thái cuối — không bao giờ override.
        if (currentRequestStatus == VisitRequestStatuses.Cancelled)
            return VisitRequestStatuses.Cancelled;

        var total = campusStatuses.Count;
        var approved = campusStatuses.Count(s => ApprovedOrBeyond.Contains(s));
        var pending = campusStatuses.Count(s => s == VisitInstanceStatus.WaitingRequestApproval);
        var rejected = campusStatuses.Count(s => s == VisitInstanceStatus.Rejected);

        if (total > 0 && rejected == total)
            return VisitRequestStatuses.Rejected;
        if (approved > 0 && (pending > 0 || rejected > 0))
            return VisitRequestStatuses.PartiallyApproved;
        if (approved > 0 && pending == 0)
            return VisitRequestStatuses.Approved;
        if (approved == 0 && pending > 0)
            return VisitRequestStatuses.PendingApproval;

        return currentRequestStatus;
    }

    public void Apply(VisitRequest visitRequest)
    {
        visitRequest.Status = Compute(
            visitRequest.Status,
            visitRequest.CampusInstances.Select(c => c.Status).ToList());
    }

    public async Task RecalculateAsync(ulong visitRequestId, CancellationToken cancellationToken)
    {
        var visit = await _db.VisitRequests
            .Include(v => v.CampusInstances)
            .FirstOrDefaultAsync(v => v.VisitRequestId == visitRequestId, cancellationToken);
        if (visit is null)
            return;

        Apply(visit);
    }
}
