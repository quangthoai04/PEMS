using System;
using System.Collections.Generic;

namespace PEMS.Application.Galleries.Common;

/// <summary>
/// Full gallery item projection returned by UC-GAL-03 (Detail) and reused as the success payload of
/// Add (UC-GAL-04) and Edit (UC-GAL-07). <see cref="Message"/> is only set by the write commands.
/// </summary>
public sealed class GalleryItemDetailDto
{
    public ulong GalleryItemId { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string ItemType { get; init; } = string.Empty;
    public string ItemTypeLabel { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string MediaKind { get; init; } = string.Empty;

    public GalleryAreaRefDto Area { get; init; } = new();
    public GalleryLocationRefDto Location { get; init; } = new();
    public GalleryCampusRefDto Campus { get; init; } = new();

    public DateTime CreatedAt { get; init; }
    public string? CreatedByName { get; init; }
    public DateTime? UpdatedAt { get; init; }
    public string? UpdatedByName { get; init; }

    public IReadOnlyList<GalleryMediaDto> Media { get; init; } = Array.Empty<GalleryMediaDto>();

    public string? Message { get; init; }
}

public sealed class GalleryAreaRefDto
{
    public ulong AreaId { get; init; }
    public string AreaName { get; init; } = string.Empty;
}

public sealed class GalleryLocationRefDto
{
    public ulong LocationId { get; init; }
    public string LocationName { get; init; } = string.Empty;
}

public sealed class GalleryCampusRefDto
{
    public ulong CampusId { get; init; }
    public string CampusCode { get; init; } = string.Empty;
    public string CampusName { get; init; } = string.Empty;
}

public sealed class GalleryMediaDto
{
    public ulong MediaId { get; init; }
    public ulong FileId { get; init; }
    public string MediaType { get; init; } = string.Empty;
    public string FileUrl { get; init; } = string.Empty;
    public string? ThumbnailUrl { get; init; }
    public bool IsPrimary { get; init; }
    public string? Caption { get; init; }
    public string? AltText { get; init; }
    public uint DisplayOrder { get; init; }
}
