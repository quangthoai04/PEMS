using MediatR;

namespace PEMS.Application.Delegations.Queries.ResolveNotificationVisitTarget;

/// <summary>
/// Resolves the EXACT current business target of a Visit notification deep link — never the
/// notification's own historical snapshot, and never a guess off an aggregated list row.
///
/// <para>
/// A notification only ever names WHERE it happened (<see cref="VisitRequestId"/>, optionally
/// <see cref="VisitInstanceId"/>). What is TRUE NOW — status, relation, whether this instance even
/// still exists, whether the caller may still see it — is re-derived here at click time, fresh from
/// the caller's own current relations. See
/// docs/CanhIter3FixBug/GopYCQuyen/PEMS_NOTIFICATION_VISIT_EXACT_TARGET_IMPLEMENTATION_PLAN.md.
/// </para>
/// </summary>
public sealed class ResolveNotificationVisitTargetQuery : IRequest<NotificationVisitTargetDto>
{
    public ulong VisitRequestId { get; init; }

    /// <summary>
    /// Null means the notification named the request only (e.g. a request-level event, or an
    /// ambiguous multi-campus one) — resolution stays at request scope, never guesses a campus.
    /// </summary>
    public ulong? VisitInstanceId { get; init; }
}
