using MediatR;

namespace PEMS.Application.Delegations.Commands.ProcessVisitRequest;

/// <summary>
/// UC-22 Process Visit Request — the Staff Leader picks the host for a campus instance.
/// Two modes (resolved from the request scope/status):
///   • SINGLE_CAMPUS + request PENDING: approve the request AND assign the chosen host
///     (host_assignment_source = MANUAL_APPROVAL); request → APPROVED, instance → ASSIGNED.
///   • MULTI_CAMPUS + request APPROVED: hand the host off from the interim IC head to a real
///     staff member (host_assignment_source = TRANSFERRED); instance stays ASSIGNED.
/// </summary>
public sealed record ProcessVisitRequestCommand(
    ulong VisitRequestId,
    ulong VisitInstanceId,
    ulong HostUserId) : IRequest<ProcessVisitRequestResponse>;
