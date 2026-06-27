using System.Collections.Generic;
using MediatR;

namespace PEMS.Application.Delegations.Queries.GetParticipantCandidates;

/// <summary>
/// Candidate search for the host's invite dropdowns. <see cref="Type"/> is IC_SUPPORT (active IC
/// staff of the campus) or STUDENT (active students of the campus). Each candidate carries
/// schedule-conflict info against the instance window. Host-only; scope re-validated in the handler.
/// </summary>
public sealed record GetParticipantCandidatesQuery(
    ulong VisitInstanceId,
    string Type,
    string? Keyword) : IRequest<IReadOnlyList<ParticipantCandidateDto>>;

public static class ParticipantCandidateTypes
{
    public const string IcSupport = "IC_SUPPORT";
    public const string Student = "STUDENT";
}
