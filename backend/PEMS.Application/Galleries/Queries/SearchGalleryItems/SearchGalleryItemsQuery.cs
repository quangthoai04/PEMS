using MediatR;
using PEMS.Application.Common.Models;
using PEMS.Application.Galleries.Common;

namespace PEMS.Application.Galleries.Queries.SearchGalleryItems;

/// <summary>
/// UC-GAL-02 Search / Filter Gallery (Staff Leader). Identical contract to UC-GAL-01 — kept as a
/// separate endpoint to match the project's verb-route convention. Campus scope resolved server-side.
/// </summary>
public sealed class SearchGalleryItemsQuery : IRequest<PaginatedResult<GalleryItemListItemDto>>, IGalleryItemListCriteria
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public string? Keyword { get; set; }
    public long? AreaId { get; set; }
    public long? LocationId { get; set; }
    public string? MediaKind { get; set; }
    public string? ItemType { get; set; }
    public string? Status { get; set; }
    public string? SortBy { get; set; } = "createdAt";
    public string? SortDirection { get; set; } = "desc";
}
