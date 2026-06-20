using System.Collections.Generic;
using MediatR;

namespace PEMS.Application.Delegations.Queries.GetHostCandidates;

/// <summary>
/// UC-22 helper: list the staff of a campus instance's campus who can be picked as host,
/// each flagged with any schedule conflict against the instance's planned window so the
/// Staff Leader can avoid double-booking. The warning is advisory (does not block).
/// </summary>
public sealed record GetHostCandidatesQuery(ulong VisitInstanceId)
    : IRequest<IReadOnlyList<HostCandidateDto>>;
