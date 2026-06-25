using MediatR;
using PEMS.Application.Common.Models;

namespace PEMS.Application.Accounts.Queries.RelatedVisitors;

/// <summary>
/// Staff Leader "Related Visitor Accounts" tab — list of Visitor accounts related to the
/// caller's campus via visit requests. The campus scope is always resolved from the
/// authenticated Staff Leader; any client-supplied campus is ignored. Read-only.
/// </summary>
public sealed class GetRelatedVisitorsQuery : IRequest<PaginatedResult<RelatedVisitorAccountListItemDto>>
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;

    /// <summary>Trimmed; matches full_name / email / phone / nationality.</summary>
    public string? Keyword { get; set; }

    /// <summary>Optional account status filter: ACTIVE / INACTIVE / LOCKED.</summary>
    public string? Status { get; set; }

    /// <summary>Optional exact nationality filter (value from the nationality dropdown).</summary>
    public string? Nationality { get; set; }

    /// <summary>lastRelatedRequestAt (default) | createdAt | fullName | latestPlannedStartAt | lastLoginAt.</summary>
    public string? SortBy { get; set; } = "lastRelatedRequestAt";

    /// <summary>asc | desc (default desc).</summary>
    public string? SortDirection { get; set; } = "desc";
}
