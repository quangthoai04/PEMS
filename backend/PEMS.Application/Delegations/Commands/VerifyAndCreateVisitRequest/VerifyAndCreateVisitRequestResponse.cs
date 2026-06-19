namespace PEMS.Application.Delegations.Commands.VerifyAndCreateVisitRequest;

public sealed record VerifyAndCreateVisitRequestResponse(
    ulong VisitRequestId,
    string RequestCode,
    string Status,
    string Message);
