namespace PEMS.Application.Delegations.Commands.RejectVisitRequest;

public sealed record RejectVisitRequestResponse(
    ulong VisitRequestId,
    string RequestStatus,
    string Message);
