using MediatR;
using PEMS.Application.Common.Models;
using System.Collections.Generic;

namespace PEMS.Application.Delegations.Queries.ViewGuestDelegationList;

public class ViewGuestDelegationListQuery : IRequest<PaginatedResult<VisitRequestManagementItemDto>>
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 10;
    public string? Keyword { get; init; }
    public string? RequestStatus { get; init; }
    public string? CampusStatus { get; init; }
    public ulong? CampusId { get; init; }
    public string? VisitScope { get; init; }
    public string? VisitScopes { get; init; }
    public DateTime? FromDate { get; init; }
    public DateTime? ToDate { get; init; }
    public bool CancelledOnly { get; init; }
}