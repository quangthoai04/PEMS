using System.Collections.Generic;
using MediatR;

namespace PEMS.Application.Delegations.Queries.GetAgendaResponsibleCandidates;

/// <summary>
/// The valid set of people who can be assigned as the responsible person of an agenda item for a
/// campus instance: the current host + ACCEPTED supporting participants (IC_SUPPORT / DEPT_SUPPORT /
/// STUDENT), all ACTIVE. Scoped to anyone with a relation to the instance (host / staff leader of the
/// campus / HO / accepted participant); 403 otherwise.
/// </summary>
public sealed record GetAgendaResponsibleCandidatesQuery(ulong VisitInstanceId)
    : IRequest<IReadOnlyList<AgendaResponsibleCandidateDto>>;
