using MediatR;

namespace PEMS.Application.Delegations.Commands.CancelVisitRequest;

/// <summary>
/// UC-136 Cancel Visit Request (Delegation Reception Management).
///
/// Cancellation is a POST-APPROVAL action only — before approval, a request is ended
/// via the reject flow (UC-18 / UC-22), never here.
///
/// <para>Scope:</para>
/// <list type="bullet">
///   <item>Visitor cancels their own approved request (self-service).</item>
///   <item>Current Host cancels the campus instance they own after the guest confirms
///   the cancellation through an external channel (external confirmation).</item>
///   <item>Staff Leader (campus scope) / HO (multi-campus scope) may cancel as well.</item>
///   <item>Admin must NOT cancel delegations.</item>
/// </list>
///
/// When <see cref="VisitInstanceId"/> is null the whole request is cancelled (with all
/// its still-cancellable campus instances). When set, only that campus instance is
/// cancelled; if it is single-campus, or it is the last active campus, the overall
/// request also becomes CANCELLED.
/// </summary>
public sealed record CancelVisitRequestCommand(
    ulong VisitRequestId,
    ulong? VisitInstanceId,
    string CancellationReason
) : IRequest<CancelVisitRequestResponse>;
