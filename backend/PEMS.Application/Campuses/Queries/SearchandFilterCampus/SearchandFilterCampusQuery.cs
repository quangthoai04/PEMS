using MediatR;
using PEMS.Application.Campuses.Common;
using PEMS.Application.Common.Models;

namespace PEMS.Application.Campuses.Queries.SearchandFilterCampus;

/// <summary>
/// UC-83 Search and Filter Campus. Same shape as UC-82 (dedicated endpoint) — both
/// resolve through <see cref="CampusListQueryExecutor"/>. Bound from the query string.
/// </summary>
public class SearchandFilterCampusQuery : IRequest<PaginatedResult<CampusListItemDto>>, ICampusListCriteria
{
    public string? Keyword { get; set; }
    public string? City { get; set; }
    public ulong? CampusId { get; set; }
    public string? Status { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public string? SortBy { get; set; }
    public string? SortOrder { get; set; }
}
