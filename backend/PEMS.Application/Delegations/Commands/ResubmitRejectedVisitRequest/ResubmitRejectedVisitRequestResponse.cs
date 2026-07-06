namespace PEMS.Application.Delegations.Commands.ResubmitRejectedVisitRequest;

public sealed record ResubmitRejectedVisitRequestResponse(
    ulong VisitRequestId,
    string RequestStatus,
    int ResubmissionCount,
    string Message);
