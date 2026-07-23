using System.Threading;
using System.Threading.Tasks;
using PEMS.Application.Common.DTOs;
using PEMS.Domain.Entities.Delegations;

namespace PEMS.Application.Common.Interfaces;

/// <summary>Recomputed request-level state returned after a v2 edit (for the handler's response + audit).</summary>
public sealed record V2EditResult(string VisitScope, bool HasMixed, int RequestRowVersion);

/// <summary>
/// Per-campus form EDIT aggregate service (plan §6.4). Applies a full, resolved per-campus snapshot to an
/// already-loaded, tracked <see cref="VisitRequest"/> inside the caller's open transaction, resolving DB-
/// generated ids for new members/instances via a mid-flush; the caller owns commit. Each edit lands on the
/// instance it targets; members stay per-campus independent (copy-on-write) and campus content is never
/// copied up onto the request row. Recomputes scope / has_mixed / fingerprint — facts about the campus set,
/// not content — and writes immutable revision history. Flows share the primitives:
/// <list type="bullet">
///   <item><see cref="ApplyPendingEditAsync"/> — a still-fully-pending request (campus set may change);</item>
///   <item><see cref="ApplyResubmitAsync"/> — a fully-REJECTED request: campus set fixed, instance ids KEPT
///         (no delete/recreate → history/FKs intact), old decisions snapshotted to audit then cleared,
///         resubmission_count++, instances reset to WAITING and re-routed to the CURRENT Staff Leaders.</item>
/// </list>
/// Both start with a <c>SELECT … FOR UPDATE</c> row-version guard so concurrent writers serialize and exactly
/// one wins; the loser gets a stable 409.
/// </summary>
public interface IVisitRequestV2EditService
{
    Task<V2EditResult> ApplyPendingEditAsync(
        VisitRequest request, VisitRequestEditV2Dto edit, ulong actorId, System.DateTime now, CancellationToken ct);

    Task<V2EditResult> ApplyResubmitAsync(
        VisitRequest request, VisitRequestEditV2Dto edit, ulong actorId, System.DateTime now, CancellationToken ct);
}
