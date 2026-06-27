using System.Collections.Generic;
using MediatR;
using PEMS.Application.Delegations.Queries.GetParticipantCandidates;

namespace PEMS.Application.Delegations.Queries.GetDepartmentStaffCandidates;

/// <summary>
/// Department-staff candidate search for the "Gán nhân sự" screen. Lists active DEPARTMENT staff of
/// the calling Department Leader's own department/campus, excluding those already in the instance.
/// Department-Leader-only; scope re-validated in the handler.
/// </summary>
public sealed record GetDepartmentStaffCandidatesQuery(ulong VisitInstanceId, string? Keyword)
    : IRequest<IReadOnlyList<ParticipantCandidateDto>>;
