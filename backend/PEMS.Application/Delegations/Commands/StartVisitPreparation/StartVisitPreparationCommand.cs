using MediatR;

namespace PEMS.Application.Delegations.Commands.StartVisitPreparation;

/// <summary>
/// The current Host takes up a campus that was assigned to them: ASSIGNED → BEFORE_VISIT.
///
/// <para>
/// This is the ONLY way that transition happens. It is deliberately an explicit business action, not
/// a side effect of opening the process screen, loading a detail, previewing an email or saving some
/// unrelated field — a campus must never drift into "preparing" because somebody looked at it. Until
/// it is called, every setup mutation on the campus is refused with
/// <c>VISIT_PREPARATION_NOT_STARTED</c>; after it, they all open.
/// </para>
/// <para>
/// <paramref name="ExpectedRowVersion"/> is optional. When the caller supplies it (the process screen
/// has the number it rendered from), a stale value is a 409 rather than a silent overwrite. Omitting
/// it is allowed because the transition is idempotent for the same Host and guarded by the ASSIGNED
/// precondition either way.
/// </para>
/// </summary>
public sealed record StartVisitPreparationCommand(
    ulong VisitRequestId,
    ulong VisitInstanceId,
    int? ExpectedRowVersion = null) : IRequest<StartVisitPreparationResponse>;

/// <summary>
/// Result of starting preparation. <paramref name="AlreadyStarted"/> is true when this call was an
/// idempotent replay — the same Host had already started this campus — so the caller can tell a
/// double-click apart from a first success without either being an error.
/// </summary>
public sealed record StartVisitPreparationResponse(
    ulong VisitRequestId,
    ulong VisitInstanceId,
    string CampusStatus,
    int RowVersion,
    bool AlreadyStarted,
    string Message);
