using System;

namespace PEMS.Application.Galleries.Common;

/// <summary>One row of the Staff Leader gallery list (UC-GAL-01 / UC-GAL-02).</summary>
public sealed class GalleryItemListItemDto
{
    public ulong GalleryItemId { get; init; }
    public ulong AreaId { get; init; }
    public string AreaName { get; init; } = string.Empty;
    public ulong LocationId { get; init; }
    public string LocationName { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string MediaKind { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
    public string? CreatedByName { get; init; }
    public GalleryPrimaryMediaDto? PrimaryMedia { get; init; }
}

/// <summary>Lightweight primary-media projection shown as the thumbnail in the list.</summary>
public sealed class GalleryPrimaryMediaDto
{
    public ulong MediaId { get; init; }
    public ulong FileId { get; init; }
    public string MediaType { get; init; } = string.Empty;
    public string FileUrl { get; init; } = string.Empty;
    public string? ThumbnailUrl { get; init; }
}
