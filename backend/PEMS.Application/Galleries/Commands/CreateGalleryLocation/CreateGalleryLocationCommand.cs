using MediatR;
using PEMS.Application.Galleries.Common;

namespace PEMS.Application.Galleries.Commands.CreateGalleryLocation;

/// <summary>
/// UC-LOC-04 (add location into an existing area) / UC-LOC-05 (create a new area + its first location).
/// <c>Mode</c> is EXISTING_AREA or NEW_AREA. Campus, keys and audit fields are derived server-side.
/// A brand-new area needs a mandatory MP4 cover <b>video</b> (the Area Showcase background); a location
/// always needs a mandatory cover <b>image</b>. Each cover is exactly one file, not a gallery item.
/// The optional EN fields carry the "Dịch sang EN" preview result (AUTO_PREVIEW + source hash) or a
/// manual EN (MANUAL) — when valid they are persisted WITHOUT calling the provider again; when absent
/// the legacy translate-during-save path runs.
/// </summary>
public sealed record CreateGalleryLocationCommand(
    string Mode,
    long? AreaId,
    string? NewAreaName,
    string? NewAreaNameEn,
    string? AreaTranslationOrigin,
    string? AreaTranslationSourceHash,
    string LocationName,
    string? LocationNameEn,
    string? LocationTranslationOrigin,
    string? LocationTranslationSourceHash,
    GalleryUploadFileCommandDto? AreaCoverVideo,
    GalleryUploadFileCommandDto? LocationCoverImage) : IRequest<GalleryLocationDetailDto>;
