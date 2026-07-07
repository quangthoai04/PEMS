namespace PEMS.Application.Delegations.Commands.UpdatePendingVisitRequest;

public sealed record UpdatePendingVisitRequestResponse(
    ulong VisitRequestId,
    string RequestStatus,
    string Message);
