namespace PEMS.Application.Departments.Common;

/// <summary>
/// Paging / search / filter / sort inputs shared by UC-104 (View Department List) and UC-103
/// (Search and Filter Departments). The campus scope is NOT part of this contract — it is always
/// resolved server-side from the Staff Leader.
/// </summary>
public interface IDepartmentListCriteria
{
    int Page { get; }
    int PageSize { get; }

    /// <summary>Trimmed; matches department name and current head full name.</summary>
    string? Keyword { get; }

    /// <summary>ACTIVE / INACTIVE / empty (all).</summary>
    string? Status { get; }

    /// <summary>name (default) | status | headName | createdAt.</summary>
    string? SortBy { get; }

    /// <summary>asc (default) | desc.</summary>
    string? SortDirection { get; }
}
