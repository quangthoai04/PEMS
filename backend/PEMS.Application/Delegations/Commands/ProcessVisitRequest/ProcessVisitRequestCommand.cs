using MediatR;

namespace PEMS.Application.Delegations.Commands.ProcessVisitRequest;

/// <summary>
/// UC-22 Process Visit Request — the Staff Leader picks the host for a campus instance.
/// Two modes (resolved from the request scope/status):
///   • SINGLE_CAMPUS + request PENDING: approve the request AND assign the chosen host
///     staff member; instance stays ASSIGNED.
/// </summary>
public sealed record ProcessVisitRequestCommand(
    ulong VisitRequestId,
    ulong VisitInstanceId,
    ulong HostUserId) : IRequest<ProcessVisitRequestResponse>;
