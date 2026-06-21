using System;
using MediatR;
using PEMS.Application.Common.Models;

namespace PEMS.Application.Delegations.Queries.GetVisitInvitations;

public class GetVisitInvitationsQuery : IRequest<PaginatedResult<InvitationListItemDto>>
{
    public string? Keyword { get; set; }
    public string? InvitationStatus { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}
