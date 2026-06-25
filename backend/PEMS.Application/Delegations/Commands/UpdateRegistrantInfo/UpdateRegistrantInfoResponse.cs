namespace PEMS.Application.Delegations.Commands.UpdateRegistrantInfo;

public sealed record UpdateRegistrantInfoResponse(
    ulong VisitRequestId,
    string FullName,
    string Organization,
    string? JobTitle,
    string Phone,
    string Email,
    string Message);
