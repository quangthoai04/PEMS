using System.Collections.Generic;
using MediatR;

namespace PEMS.Application.Delegations.Queries.ViewMyVisitInvitations;

/// <summary>
/// UC-27: the signed-in user's own participation invitations. By default returns only
/// pending (INVITED) invitations to respond to; set <see cref="IncludeResponded"/> to also
/// include the ACCEPTED/DECLINED history.
/// </summary>
public sealed class ViewMyVisitInvitationsQuery : IRequest<List<VisitInvitationDto>>
{
    public bool IncludeResponded { get; init; }
}
