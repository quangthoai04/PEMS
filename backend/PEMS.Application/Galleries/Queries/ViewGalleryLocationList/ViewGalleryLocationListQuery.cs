using MediatR;
using PEMS.Application.Common.Models;
using PEMS.Application.Galleries.Common;

namespace PEMS.Application.Galleries.Queries.ViewGalleryLocationList;

/// <summary>
/// UC-LOC-01 (list) / UC-LOC-02 (search) / UC-LOC-03 (filter) — paged area/location list for the
/// "Quản lý khu vực" screen, scoped to the caller's campus (resolved server-side, never from the client).
/// </summary>
public sealed class ViewGalleryLocationListQuery : IRequest<PaginatedResult<GalleryLocationListItemDto>>
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public string? Keyword { get; set; }
    public long? AreaId { get; set; }
    public string? Status { get; set; }
    public string? SortBy { get; set; } = "createdAt";
    public string? SortDirection { get; set; } = "desc";
}
