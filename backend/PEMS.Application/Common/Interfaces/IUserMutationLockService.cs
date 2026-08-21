using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace PEMS.Application.Common.Interfaces;

/// <summary>
/// Shared pessimistic row locks over <c>users</c> and <c>departments</c>, taken inside the caller's
/// transaction.
///
/// Re-checking dependencies twice is not enough to make a role change safe: between the check and
/// the commit another transaction can still assign the account a Host/participant/logistics
/// responsibility. Every flow that either changes a role or creates such a responsibility takes the
/// same lock on the same rows first, so the two serialize (spec §13):
///
/// <code>
/// begin transaction → lock user(s) → re-read role/status/campus → validate → write → commit
/// </code>
///
/// Whichever transaction locks first wins; the other one blocks, then re-reads and sees the
/// committed state — either the assignment is refused because the role already changed, or the role
/// change returns 409 because the responsibility now exists.
/// </summary>
public interface IUserMutationLockService
{
    /// <summary>
    /// Locks the given user rows for update. Ids are locked in ascending order by the implementation
    /// so two flows holding overlapping sets can never deadlock each other. Unknown ids are ignored
    /// (nothing to lock); an empty collection is a no-op.
    /// </summary>
    Task LockUsersAsync(IReadOnlyCollection<ulong> userIds, CancellationToken cancellationToken);

    /// <summary>Same contract as <see cref="LockUsersAsync"/> for <c>departments</c> rows.</summary>
    Task LockDepartmentsAsync(IReadOnlyCollection<ulong> departmentIds, CancellationToken cancellationToken);

    /// <summary>
    /// Lock hierarchy (tiers 2-5) for anything that mutates a <c>VisitParticipant</c> or
    /// <c>VisitLogisticsItem</c> row, or that must serialize against such a mutation — Portal and
    /// Email response handlers, delegation/assignment handlers, and cancellation/completion cascades
    /// all take these in the same fixed order (VisitRequest → VisitRequestCampus → business target →
    /// EmailActionToken group) so any two of them serialize instead of racing on a stale read.
    /// Same ascending-order, ignore-unknown-ids, no-op-on-empty contract as <see cref="LockUsersAsync"/>.
    /// </summary>
    Task LockVisitRequestsAsync(IReadOnlyCollection<ulong> visitRequestIds, CancellationToken cancellationToken);

    /// <summary>Tier 3 — locks <c>visit_request_campuses</c> rows by <c>visit_instance_id</c>.</summary>
    Task LockVisitRequestCampusesAsync(IReadOnlyCollection<ulong> visitInstanceIds, CancellationToken cancellationToken);

    /// <summary>Tier 4 — locks <c>visit_participants</c> rows by <c>participant_id</c>.</summary>
    Task LockVisitParticipantsAsync(IReadOnlyCollection<ulong> participantIds, CancellationToken cancellationToken);

    /// <summary>Tier 4 — locks <c>visit_logistics_items</c> rows by <c>logistics_item_id</c>.</summary>
    Task LockVisitLogisticsItemsAsync(IReadOnlyCollection<ulong> logisticsItemIds, CancellationToken cancellationToken);

    /// <summary>
    /// Tier 5 — locks every <c>email_action_tokens</c> row sharing the given <c>action_group_key</c>
    /// (the Accept/Decline/etc. tokens minted together for one business action). Must be re-acquired
    /// and re-read inside the same transaction right before consuming/burning tokens — never trust a
    /// token object fetched before the transaction opened. No-op for an empty/null key.
    /// </summary>
    Task LockEmailActionTokenGroupAsync(string? actionGroupKey, CancellationToken cancellationToken);
}
