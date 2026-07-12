namespace PEMS.Application.Delegations.Commands.CreateAuthenticatedVisitRequest;

public sealed record CreateAuthenticatedVisitRequestResponse(
    ulong VisitRequestId,
    string RequestCode,
    string Status,
    string Message,
    bool HasHostingConflictWarning);
