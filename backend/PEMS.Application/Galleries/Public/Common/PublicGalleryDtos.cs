using System;
using System.Collections.Generic;

namespace PEMS.Application.Galleries.Public.Common;

// ── Campus list (UC §7.1) ────────────────────────────────────────────────────

/// <summary>One active campus shown on the VisitFPTU campus picker. No admin/audit fields (BR-PGAL-12).</summary>
public sealed class PublicCampusDto
{
    public ulong CampusId { get; init; }
    public string CampusCode { get; init; } = string.Empty;
    public string CampusName { get; init; } = string.Empty;
    public string? City { get; init; }
    public ulong? CoverFileId { get; init; }
    public string? CoverUrl { get; init; }
}

public sealed class PublicCampusListDto
{
    public IReadOnlyList<PublicCampusDto> Items { get; init; } = Array.Empty<PublicCampusDto>();
}

// ── Campus navigation (UC §7.2) ──────────────────────────────────────────────

/// <summary>Area/location tree of public-visible content for one campus (left sidebar + hover flyout).</summary>
public sealed class PublicGalleryNavigationDto
{
    public PublicCampusDto Campus { get; init; } = new();
    public IReadOnlyList<PublicGalleryAreaDto> Areas { get; init; } = Array.Empty<PublicGalleryAreaDto>();
}

public sealed class PublicGalleryAreaDto
{
    public ulong AreaId { get; init; }
    public string AreaName { get; init; } = string.Empty;
    public int DisplayOrder { get; init; }
    public IReadOnlyList<PublicGalleryLocationDto> Locations { get; init; } = Array.Empty<PublicGalleryLocationDto>();
}

public sealed class PublicGalleryLocationDto
{
    public ulong LocationId { get; init; }
    public string LocationName { get; init; } = string.Empty;
    public int DisplayOrder { get; init; }
    public ulong GalleryItemId { get; init; }
    public string Title { get; init; } = string.Empty;
    public string MediaKind { get; init; } = string.Empty;
    public string? PrimaryMediaUrl { get; init; }
}

// ── Location gallery-item detail (UC §7.3) ───────────────────────────────────

public sealed class PublicGalleryItemDetailDto
{
    public PublicCampusDto Campus { get; init; } = new();
    public PublicGalleryAreaSummaryDto Area { get; init; } = new();
    public PublicGalleryLocationSummaryDto Location { get; init; } = new();
    public PublicGalleryItemSummaryDto GalleryItem { get; init; } = new();
    public IReadOnlyList<PublicGalleryMediaDto> Media { get; init; } = Array.Empty<PublicGalleryMediaDto>();
}

public sealed class PublicGalleryAreaSummaryDto
{
    public ulong AreaId { get; init; }
    public string AreaName { get; init; } = string.Empty;
}

public sealed class PublicGalleryLocationSummaryDto
{
    public ulong LocationId { get; init; }
    public string LocationName { get; init; } = string.Empty;
}

public sealed class PublicGalleryItemSummaryDto
{
    public ulong GalleryItemId { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string MediaKind { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
}

public sealed class PublicGalleryMediaDto
{
    public ulong MediaId { get; init; }
    public ulong FileId { get; init; }
    public string MediaType { get; init; } = string.Empty;
    public string Url { get; init; } = string.Empty;
    public string? ThumbnailUrl { get; init; }
    public string? Caption { get; init; }
    public string? AltText { get; init; }
    public bool IsPrimary { get; init; }
    public int DisplayOrder { get; init; }
}
