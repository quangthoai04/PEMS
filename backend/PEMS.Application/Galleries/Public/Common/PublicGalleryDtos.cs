using System;
using System.Collections.Generic;
using PEMS.Application.Galleries.Common;

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
    /// <summary>Area cover image (gallery_areas.cover_file_id) — the Area Showcase fullscreen background.</summary>
    public ulong? AreaCoverFileId { get; init; }
    public string? AreaCoverUrl { get; init; }
    public IReadOnlyList<PublicGalleryLocationDto> Locations { get; init; } = Array.Empty<PublicGalleryLocationDto>();
}

public sealed class PublicGalleryLocationDto
{
    public ulong LocationId { get; init; }
    public string LocationName { get; init; } = string.Empty;
    public int DisplayOrder { get; init; }
    /// <summary>Location cover image (gallery_locations.cover_file_id) — the Area Showcase thumbnail rail.</summary>
    public ulong? LocationCoverFileId { get; init; }
    public string? LocationCoverUrl { get; init; }
    /// <summary>The lead item shown as the location's nav thumbnail (a location may hold many items).</summary>
    public ulong GalleryItemId { get; init; }
    public string Title { get; init; } = string.Empty;
    public string MediaKind { get; init; } = string.Empty;
    /// <summary>How many public-visible items this location carries (≥ 1).</summary>
    public int PublicGalleryItemCount { get; init; }
    public string? PrimaryMediaUrl { get; init; }
}

// ── Location gallery grid (UC §7.2 grid / BR-PGAL-GRID-01..04) ───────────────

/// <summary>
/// The album grid of one location: every public-visible gallery item represented by its primary media
/// (BR-PGAL-GRID-02). Campus/area/location header reported once. Used by the LOCATION_GRID screen.
/// </summary>
public sealed class PublicLocationGalleryGridDto
{
    public PublicCampusDto Campus { get; init; } = new();
    public PublicGalleryAreaSummaryDto Area { get; init; } = new();
    public PublicGalleryLocationSummaryDto Location { get; init; } = new();
    public IReadOnlyList<PublicGalleryGridItemDto> Items { get; init; } = Array.Empty<PublicGalleryGridItemDto>();
}

/// <summary>One card in the location grid — a gallery item shown by its primary media + a short preview.</summary>
public sealed class PublicGalleryGridItemDto
{
    public ulong GalleryItemId { get; init; }
    public string Title { get; init; } = string.Empty;
    public string DescriptionPreview { get; init; } = string.Empty;
    public string MediaKind { get; init; } = string.Empty;
    public PublicGalleryMediaDto? PrimaryMedia { get; init; }
}

// ── Location showcase (MEDIA column + VISIT_DELEGATION row) ──────────────────

/// <summary>
/// One gallery item shown by its primary media in the Location Showcase — used for both the right-hand
/// MEDIA column and the "Đoàn khách đã tới thăm" row. <see cref="ItemType"/> is MEDIA or VISIT_DELEGATION.
/// </summary>
public sealed class PublicGalleryShowcaseItemDto
{
    public ulong GalleryItemId { get; init; }
    public string Title { get; init; } = string.Empty;
    public string ItemType { get; init; } = string.Empty;
    public string MediaKind { get; init; } = string.Empty;
    public PublicGalleryMediaDto? PrimaryMedia { get; init; }
}

/// <summary>
/// The Location Showcase payload: the location's own MEDIA gallery items (right column) and its
/// VISIT_DELEGATION gallery items (bottom-left row), each by its primary media. Campus/area/location
/// echoed for headers. Empty lists are valid (the UI just hides that section). Anonymous / read-only.
/// </summary>
public sealed class PublicLocationShowcaseDto
{
    public PublicCampusDto Campus { get; init; } = new();
    public PublicGalleryAreaSummaryDto Area { get; init; } = new();
    public PublicGalleryLocationSummaryDto Location { get; init; } = new();
    public IReadOnlyList<PublicGalleryShowcaseItemDto> MediaItems { get; init; } = Array.Empty<PublicGalleryShowcaseItemDto>();
    public IReadOnlyList<PublicGalleryShowcaseItemDto> VisitDelegationItems { get; init; } = Array.Empty<PublicGalleryShowcaseItemDto>();
}

// ── Gallery item detail (UC §7.3 / BR-PGAL-GRID-07) ──────────────────────────

/// <summary>One gallery item with its full ordered ACTIVE media list (the detail screen).</summary>
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

    /// <summary>UPLOADED_FILE or YOUTUBE — the public page renders an iframe for YOUTUBE, else img/video.</summary>
    public string SourceType { get; init; } = GalleryMediaSourceTypes.UploadedFile;

    /// <summary>Scoped proxy content URL for an uploaded file; null for a YouTube reference (no binary).</summary>
    public string? Url { get; init; }
    public string? ThumbnailUrl { get; init; }

    // YouTube-only (null for uploaded files).
    public string? YoutubeVideoId { get; init; }
    public string? EmbedUrl { get; init; }
    public string? WebViewUrl { get; init; }

    public string? Caption { get; init; }
    public string? AltText { get; init; }
    public bool IsPrimary { get; init; }
    public int DisplayOrder { get; init; }
}
