namespace PEMS.Application.Delegations.Commands.CancelVisitRequest;

/// <summary>Outcome of a UC-136 cancel: the resulting request status plus the campuses that were cancelled.</summary>
public sealed record CancelVisitRequestResponse(
    ulong VisitRequestId,
    string RequestStatus,
    IReadOnlyList<CancelledCampusDto> CancelledCampuses,
    string Message);

public sealed record CancelledCampusDto(
    ulong VisitInstanceId,
    string Status);
