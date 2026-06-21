using MediatR;
using PEMS.Application.Common.Models;
using System.Collections.Generic;

namespace PEMS.Application.Delegations.Queries.ViewGuestDelegationList;

public class ViewGuestDelegationListQuery : IRequest<PaginatedResult<VisitRequestManagementItemDto>>
{
    /// <summary>
    /// "responsible" (default) = Tab "Đơn phụ trách" — requests the user is responsible for
    /// (created, approves, hosts, or is assigned a task on), scoped by role.
    /// "attending" = Tab "Đơn mời tham dự" — requests the user has ACCEPTED a participation
    /// invitation for. Not available to Visitor/Admin.
    /// </summary>
    public string? Tab { get; init; }
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
    public string? Relation { get; init; }
    public bool? ReadOnlyOnly { get; init; }
    public bool? ActionableOnly { get; init; }
    public string? Timing { get; init; }
    public string? SortBy { get; init; }
    public string? SortOrder { get; init; }
}