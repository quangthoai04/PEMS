namespace PEMS.Application.Delegations.Commands.ProcessVisitRequest;

public sealed record ProcessVisitRequestResponse(
    ulong VisitRequestId,
    ulong VisitInstanceId,
    string RequestStatus,
    string CampusStatus,
    ulong HostUserId,
    string Message);
