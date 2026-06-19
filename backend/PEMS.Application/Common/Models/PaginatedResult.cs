namespace PEMS.Application.Common.Models;

/// <summary>
/// Standard paged envelope returned by list/search/filter queries. The shape is
/// serialized verbatim to the frontend (camelCase) — see UC-95 / UC-99.
/// </summary>
public sealed class PaginatedResult<T>
{
    public IReadOnlyList<T> Items { get; init; } = Array.Empty<T>();
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalItems { get; init; }
    public int TotalPages { get; init; }
    public bool HasNextPage { get; init; }
    public bool HasPreviousPage { get; init; }

    public static PaginatedResult<T> Create(
        IReadOnlyList<T> items,
        int page,
        int pageSize,
        int totalItems)
    {
        var safeSize = pageSize < 1 ? 1 : pageSize;
        var totalPages = totalItems <= 0 ? 0 : (int)Math.Ceiling(totalItems / (double)safeSize);

        return new PaginatedResult<T>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalItems = totalItems,
            TotalPages = totalPages,
            HasNextPage = page < totalPages,
            HasPreviousPage = page > 1
        };
    }
}
