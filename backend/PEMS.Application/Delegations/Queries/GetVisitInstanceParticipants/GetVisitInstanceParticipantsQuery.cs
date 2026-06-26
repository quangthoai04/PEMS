using System.Collections.Generic;
using MediatR;

namespace PEMS.Application.Delegations.Queries.GetVisitInstanceParticipants;

/// <summary>
/// Lists all participant rows of a campus instance (host snapshot + invited supporters), for the
/// VisitProcess "Thành phần tham gia" panel. Scope enforced in the handler (internal relation only;
/// the visitor/guest owner is excluded). Used both on first load and for cheap refetch after an
/// invite / remove / assignment.
/// </summary>
public sealed record GetVisitInstanceParticipantsQuery(ulong VisitInstanceId)
    : IRequest<IReadOnlyList<VisitParticipantListItemDto>>;
