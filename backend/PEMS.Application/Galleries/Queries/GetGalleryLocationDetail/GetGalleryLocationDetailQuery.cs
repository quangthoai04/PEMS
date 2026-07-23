using System;
using MediatR;

namespace PEMS.Application.Galleries.Queries.GetGalleryLocationDetail;

/// <summary>
/// Authoritative single-location detail for the edit modal (direct area/location edit). Returns the
/// bilingual names + translation statuses + cover metadata of the location AND its current area, so the
/// modal never has to trust a possibly-stale list row. Staff Leader only, campus-scoped.
/// </summary>
public sealed record GetGalleryLocationDetailQuery(long LocationId) : IRequest<GalleryLocationEditDetailDto>;

public sealed class GalleryLocationEditDetailDto
{
    public ulong LocationId { get; init; }
    public ulong AreaId { get; init; }

    public string AreaName { get; init; } = string.Empty;
    public string? AreaNameEn { get; init; }
    public string AreaTranslationStatus { get; init; } = string.Empty;
    public ulong? AreaCoverFileId { get; init; }
    public string? AreaCoverUrl { get; init; }
    /// <summary>IMAGE (legacy areas) or VIDEO (MP4 cover).</summary>
    public string AreaCoverMediaType { get; init; } = string.Empty;

    public string LocationName { get; init; } = string.Empty;
    public string? LocationNameEn { get; init; }
    public string LocationTranslationStatus { get; init; } = string.Empty;
    public ulong? LocationCoverFileId { get; init; }
    public string? LocationCoverUrl { get; init; }

    public DateTime? UpdatedAt { get; init; }
}
