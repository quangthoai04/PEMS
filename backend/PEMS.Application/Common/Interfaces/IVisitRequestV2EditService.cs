using System.Threading;
using System.Threading.Tasks;
using PEMS.Application.Common.DTOs;
using PEMS.Domain.Entities.Delegations;

namespace PEMS.Application.Common.Interfaces;

/// <summary>Recomputed request-level state returned after a v2 edit (for the handler's response + audit).</summary>
public sealed record V2EditResult(string VisitScope, bool HasMixed, int RequestRowVersion);

/// <summary>
/// Per-campus form v2 EDIT aggregate service (plan §6.4). Applies a full, resolved per-campus snapshot to an
/// already-loaded, tracked <see cref="VisitRequest"/> inside the caller's open transaction, resolving DB-
/// generated ids for new members/instances via a mid-flush; the caller owns commit. Keeps members per-campus
/// independent (copy-on-write), recomputes scope / has_mixed / fingerprint / compatibility projection, and
/// writes immutable revision history. Flows share the primitives:
/// <list type="bullet">
///   <item><see cref="ApplyPendingEditAsync"/> — a still-fully-pending request (campus set may change);</item>
///   <item>rejected-resubmit (campus set fixed, instance ids kept, decisions cleared) — added with that slice.</item>
/// </list>
/// </summary>
public interface IVisitRequestV2EditService
{
    Task<V2EditResult> ApplyPendingEditAsync(
        VisitRequest request, VisitRequestEditV2Dto edit, ulong actorId, System.DateTime now, CancellationToken ct);
}
