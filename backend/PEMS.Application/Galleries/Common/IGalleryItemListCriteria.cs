namespace PEMS.Application.Galleries.Common;

/// <summary>
/// Paging / search / filter / sort inputs shared by UC-GAL-01 (View List) and UC-GAL-02 (Search &amp;
/// Filter). The campus scope is NOT part of this contract — it is always resolved server-side from the
/// Staff Leader so the client can never spoof a campus.
/// </summary>
public interface IGalleryItemListCriteria
{
    int Page { get; }
    int PageSize { get; }

    /// <summary>Trimmed; matches title, description, area name and location name.</summary>
    string? Keyword { get; }

    /// <summary>Filter by area (must belong to the caller's campus).</summary>
    long? AreaId { get; }

    /// <summary>Filter by location (must belong to the caller's campus).</summary>
    long? LocationId { get; }

    /// <summary>IMAGE / VIDEO / MIXED / empty (all).</summary>
    string? MediaKind { get; }

    /// <summary>MEDIA / VISIT_DELEGATION / empty (all). Content type, distinct from media kind.</summary>
    string? ItemType { get; }

    /// <summary>PUBLISHED / HIDDEN / empty (all).</summary>
    string? Status { get; }

    /// <summary>
    /// Narration status filter — one of TtsManagementStatuses (READY / PROCESSING / FAILED / STALE /
    /// NOT_CREATED / DISABLED / INVALID_DESCRIPTION) / empty (all). Computed server-side, so filtering by
    /// it pages the computed set in memory (see the executor).
    /// </summary>
    string? AudioStatus { get; }

    /// <summary>createdAt (default) | title | status.</summary>
    string? SortBy { get; }

    /// <summary>asc | desc (default).</summary>
    string? SortDirection { get; }
}
