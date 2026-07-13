using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Interfaces;
using PEMS.Domain.Constants;

namespace PEMS.Application.Campuses.Common;

/// <summary>One campus visit instance that blocks disabling the campus (limited example row).</summary>
public sealed class CampusVisitBlockerExample
{
    public ulong VisitInstanceId { get; init; }
    public ulong RequestId { get; init; }
    public string? RequestCode { get; init; }
    public string? DelegationName { get; init; }
    public string Status { get; init; } = null!;
    public DateTime? PlannedStartAt { get; init; }
    public DateTime? PlannedEndAt { get; init; }
}

/// <summary>Computed impact of disabling a campus (UC-86 §12: blockers by instance status).</summary>
public sealed class CampusStatusImpact
{
    public int BlockerCount { get; init; }
    public IReadOnlyDictionary<string, int> BlockersByStatus { get; init; } =
        new Dictionary<string, int>();
    public IReadOnlyList<CampusVisitBlockerExample> BlockerExamples { get; init; } =
        Array.Empty<CampusVisitBlockerExample>();
    public bool HasBlockers => BlockerCount > 0;
}

/// <summary>
/// UC-86 §12 blocker classification on <c>visit_request_campuses.status</c> — the campus
/// INSTANCE, never the aggregate <c>visit_requests.status</c> (BR-86-09) and never dates
/// (BR-86-10). Only truly terminal statuses allow disable; every other value (including any
/// unknown/legacy status that may appear at runtime) blocks, per the canonical lifecycle.
/// </summary>
public static class CampusVisitBlockerRule
{
    /// <summary>Terminal instance statuses — the campus's business flow has really ended.</summary>
    public static readonly IReadOnlyList<string> TerminalStatuses = new[]
    {
        VisitInstanceStatuses.Closed,
        VisitInstanceStatuses.Cancelled,
        VisitInstanceStatuses.Rejected,
    };

    /// <summary>Known non-terminal (operational) statuses, for documentation/tests.</summary>
    public static readonly IReadOnlyList<string> BlockingStatuses = new[]
    {
        VisitInstanceStatuses.WaitingRequestApproval,
        VisitInstanceStatuses.Assigned,
        VisitInstanceStatuses.BeforeVisit,
        VisitInstanceStatuses.DuringVisit,
        VisitInstanceStatuses.AfterVisit,
    };

    /// <summary>Non-terminal ⇒ blocks. Unknown statuses are treated as non-terminal (safe default).</summary>
    public static bool Blocks(string status) =>
        !TerminalStatuses.Contains(status, StringComparer.Ordinal);
}

/// <summary>
/// Shared UC-86 disable-impact computation, used by both the status-impact preview query and the
/// manage-status command so the confirmation modal and the transactional guard can never disagree
/// (BR-86-17). Dependency source is <c>visit_request_campuses.campus_id</c> only; historical child
/// data (participants, agendas, logistics, minutes, …) of terminal instances is deliberately NOT a
/// blocker (§13) — closing them completely is enforced by the close-visit UC, not here.
/// </summary>
public static class CampusStatusImpactCalculator
{
    /// <summary>Cap on example rows so the preview payload stays small (§18).</summary>
    public const int MaxBlockerExamples = 5;

    public static async Task<CampusStatusImpact> ComputeDisableImpactAsync(
        IApplicationDbContext db, ulong campusId, CancellationToken ct)
    {
        var terminal = CampusVisitBlockerRule.TerminalStatuses.ToArray();

        // Counts per status (grouped in SQL; NOT IN terminal ⇔ CampusVisitBlockerRule.Blocks).
        var byStatus = await db.VisitRequestCampuses.AsNoTracking()
            .Where(c => c.CampusId == campusId && !terminal.Contains(c.Status))
            .GroupBy(c => c.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        if (byStatus.Count == 0)
            return new CampusStatusImpact();

        // Limited examples for the confirmation modal — no personal data, just the delegation
        // identity + schedule (dates are display/sort only, never part of the decision).
        var examples = await (
            from instance in db.VisitRequestCampuses.AsNoTracking()
            join request in db.VisitRequests.AsNoTracking()
                on instance.VisitRequestId equals request.VisitRequestId
            where instance.CampusId == campusId && !terminal.Contains(instance.Status)
            orderby instance.PlannedStartAt
            select new CampusVisitBlockerExample
            {
                VisitInstanceId = instance.VisitInstanceId,
                RequestId = instance.VisitRequestId,
                RequestCode = request.RequestCode,
                DelegationName = request.DelegationName,
                Status = instance.Status,
                PlannedStartAt = instance.PlannedStartAt,
                PlannedEndAt = instance.PlannedEndAt,
            })
            .Take(MaxBlockerExamples)
            .ToListAsync(ct);

        return new CampusStatusImpact
        {
            BlockerCount = byStatus.Sum(s => s.Count),
            BlockersByStatus = byStatus.ToDictionary(s => s.Status, s => s.Count),
            BlockerExamples = examples,
        };
    }
}
