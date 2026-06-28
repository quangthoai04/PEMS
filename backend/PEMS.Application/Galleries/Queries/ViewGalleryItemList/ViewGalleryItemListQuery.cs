using MediatR;
using PEMS.Application.Common.Models;
using PEMS.Application.Galleries.Common;

namespace PEMS.Application.Galleries.Queries.ViewGalleryItemList;

/// <summary>
/// UC-GAL-01 View List Gallery (Staff Leader). Paging / search / filter / sort over the caller's own
/// campus gallery items. Campus is resolved server-side — never accepted from the client.
/// </summary>
public sealed class ViewGalleryItemListQuery : IRequest<PaginatedResult<GalleryItemListItemDto>>, IGalleryItemListCriteria
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public string? Keyword { get; set; }
    public long? AreaId { get; set; }
    public long? LocationId { get; set; }
    public string? MediaKind { get; set; }
    public string? Status { get; set; }
    public string? SortBy { get; set; } = "createdAt";
    public string? SortDirection { get; set; } = "desc";
}
