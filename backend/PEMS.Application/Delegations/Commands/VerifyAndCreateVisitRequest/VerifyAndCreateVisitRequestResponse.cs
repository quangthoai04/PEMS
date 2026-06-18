namespace PEMS.Application.Delegations.Commands.VerifyAndCreateVisitRequest;

public sealed record VerifyAndCreateVisitRequestResponse(
    string VisitRequestId,
    string RequestCode,
    string Status,
    string Message);
