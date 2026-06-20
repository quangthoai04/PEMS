using System.Collections.Generic;

namespace PEMS.Application.Delegations.Commands.ApproveCrossCampusRequest;

public sealed record ApproveCrossCampusRequestResponse(
    ulong VisitRequestId,
    string RequestStatus,
    IReadOnlyList<AssignedCampusDto> AssignedCampuses,
    string Message);

public sealed record AssignedCampusDto(
    ulong VisitInstanceId,
    ulong CampusId,
    ulong? HostUserId,
    string Status);
