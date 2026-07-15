using System.Collections.Generic;
using MediatR;

namespace PEMS.Application.Galleries.Queries.GetGalleryFilterOptions;

/// <summary>
/// Returns the areas (and their locations) of the current Staff Leader's campus so the frontend can
/// populate the area / location filter dropdowns and the upload location picker. This is read-only
/// reference data — actual area/location management is a separate UC. Campus resolved server-side.
/// </summary>
public sealed class GetGalleryFilterOptionsQuery : IRequest<GalleryFilterOptionsDto>
{
}

public sealed class GalleryFilterOptionsDto
{
    public IReadOnlyList<GalleryAreaOptionDto> Areas { get; init; } = new List<GalleryAreaOptionDto>();
}

public sealed class GalleryAreaOptionDto
{
    public ulong AreaId { get; init; }
    public string AreaName { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public ulong? CoverFileId { get; init; }
    public string? CoverUrl { get; init; }
    /// <summary>IMAGE (legacy areas) or VIDEO (MP4 cover) — lets the edit modal preview the right element.</summary>
    public string CoverMediaType { get; init; } = "IMAGE";
    public IReadOnlyList<GalleryLocationOptionDto> Locations { get; init; } = new List<GalleryLocationOptionDto>();
}

public sealed class GalleryLocationOptionDto
{
    public ulong LocationId { get; init; }
    public string LocationName { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
}
