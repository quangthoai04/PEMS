namespace PEMS.Application.Campuses.Common;

/// <summary>
/// Shared criteria contract for the campus list read model so UC-82 (View Campus List)
/// and UC-83 (Search and Filter Campus) drive the same executor. Search + filters
/// combine with AND logic; an empty/whitespace value means "no filter".
/// </summary>
public interface ICampusListCriteria
{
    /// <summary>Free-text search over campus name and IC Head full name.</summary>
    string? Keyword { get; }

    /// <summary>Exact (case-insensitive) city filter.</summary>
    string? City { get; }

    /// <summary>Exact campus id filter.</summary>
    ulong? CampusId { get; }

    /// <summary>ACTIVE / INACTIVE status filter.</summary>
    string? Status { get; }

    int Page { get; }
    int PageSize { get; }

    /// <summary>One of name | campusCode | city | status. Defaults to name.</summary>
    string? SortBy { get; }

    /// <summary>asc | desc. Defaults to asc.</summary>
    string? SortOrder { get; }
}
