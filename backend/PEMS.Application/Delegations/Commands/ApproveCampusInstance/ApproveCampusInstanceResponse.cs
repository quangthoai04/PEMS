namespace PEMS.Application.Delegations.Commands.ApproveCampusInstance;

public sealed record ApproveCampusInstanceResponse(
    ulong VisitRequestId,
    ulong VisitInstanceId,
    string RequestStatus,
    string CampusStatus,
    ulong HostUserId,
    string Message);
