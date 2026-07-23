using MediatR;
using PEMS.Application.Galleries.Common;

namespace PEMS.Application.Galleries.Commands.UpdateGalleryLocation;

/// <summary>
/// Direct edit of a location AND its current area ("Chỉnh sửa khu vực và vị trí"). The handler UPDATEs
/// the existing rows in place — it never creates a new area and never moves the location to another area
/// (both ids stay unchanged). Editing never touches the location's status or its gallery items. Covers
/// are optional: omitted → kept; supplied → replaced (area cover must be an MP4 video).
/// The EN names may come from the translation preview (AUTO_PREVIEW + source hash) or a manual edit
/// (MANUAL) — in both cases the provider is NOT called again; when a Vietnamese name changed and no
/// usable EN was supplied, the legacy translate-during-save path runs (one batched provider request).
/// </summary>
public sealed record UpdateGalleryLocationCommand(
    long LocationId,
    string AreaName,
    string? AreaNameEn,
    string? AreaTranslationOrigin,
    string? AreaTranslationSourceHash,
    string LocationName,
    string? LocationNameEn,
    string? LocationTranslationOrigin,
    string? LocationTranslationSourceHash,
    GalleryUploadFileCommandDto? AreaCoverVideo,
    GalleryUploadFileCommandDto? LocationCoverImage) : IRequest<GalleryLocationDetailDto>;
