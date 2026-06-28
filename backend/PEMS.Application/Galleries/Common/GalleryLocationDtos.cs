using System;

namespace PEMS.Application.Galleries.Common;

/// <summary>
/// One row of the "Quản lý khu vực" table (UC-LOC-01/02/03). The <see cref="HasGalleryItem"/> trio
/// comes from a LEFT JOIN onto the (single, non-deleted) gallery item of the location — this UC never
/// edits the item, it only reports whether one exists.
/// </summary>
public sealed class GalleryLocationListItemDto
{
    public ulong LocationId { get; init; }
    public ulong AreaId { get; init; }
    public string AreaName { get; init; } = string.Empty;
    public string LocationName { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }

    public bool HasGalleryItem { get; init; }
    public ulong? GalleryItemId { get; init; }
    public string? GalleryItemStatus { get; init; }
}

/// <summary>
/// Single-location payload returned by Create (UC-LOC-04/05), Update (UC-LOC-06/07) and the status
/// toggle (UC-LOC-08/09). <see cref="Message"/> is only set by the write commands.
/// </summary>
public sealed class GalleryLocationDetailDto
{
    public ulong LocationId { get; init; }
    public ulong AreaId { get; init; }
    public string AreaName { get; init; } = string.Empty;
    public string LocationName { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }

    public bool HasGalleryItem { get; init; }
    public ulong? GalleryItemId { get; init; }
    public string? GalleryItemStatus { get; init; }

    public string? Message { get; init; }
}
