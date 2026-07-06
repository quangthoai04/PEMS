namespace PEMS.Application.Delegations.Commands.RejectCampusInstance;

public sealed record RejectCampusInstanceResponse(
    ulong VisitRequestId,
    ulong VisitInstanceId,
    string RequestStatus,
    string CampusStatus,
    string Message);
