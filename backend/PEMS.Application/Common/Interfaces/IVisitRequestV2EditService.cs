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

    /// <summary>
    /// Sends ONE rejected campus back for review, leaving every sibling exactly as it was.
    ///
    /// <para>
    /// The whole-request <see cref="ApplyResubmitAsync"/> cannot express this: it refuses unless EVERY
    /// campus is rejected, and it resets every one of them. That is the right shape when a request was
    /// refused outright, and the wrong shape entirely when one campus said no and another already said
    /// yes and assigned a host — which is the case the operational contact of the refused campus is
    /// looking at.
    /// </para>
    /// <para>
    /// The aggregate is recomputed from the campuses rather than assumed, so a request with one campus
    /// back in review and one already approved lands on PARTIALLY_APPROVED, the same value the database
    /// trigger computes for itself.
    /// </para>
    /// </summary>
    Task<V2EditResult> ApplyInstanceResubmitAsync(
        VisitRequest request, VisitRequestCampus instance, CampusVisitEditV2Dto content,
        ulong actorId, System.DateTime now, CancellationToken ct);
}
