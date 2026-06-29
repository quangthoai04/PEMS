using System;

namespace PEMS.Application.Galleries.Common;

/// <summary>
/// One row of the "Quản lý khu vực" table (UC-LOC-01/02/03). A location can hold 0, 1 or many gallery
/// items, so the indicator is reported as aggregate counts (never one-per-row) — this UC never edits the
/// items, it only reports how many exist and their published/hidden split.
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

    public bool HasGalleryItems { get; init; }
    public int GalleryItemCount { get; init; }
    public int PublishedGalleryItemCount { get; init; }
    public int HiddenGalleryItemCount { get; init; }
}

/// <summary>
/// Single-location payload returned by Create (UC-LOC-04/05), Update (UC-LOC-06/07) and the status
/// toggle (UC-LOC-08/09). <see cref="Message"/> is only set by the write commands. A location may hold
/// many gallery items now, so the indicator is reported as aggregate counts.
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

    public bool HasGalleryItems { get; init; }
    public int GalleryItemCount { get; init; }
    public int PublishedGalleryItemCount { get; init; }
    public int HiddenGalleryItemCount { get; init; }

    public string? Message { get; init; }
}
