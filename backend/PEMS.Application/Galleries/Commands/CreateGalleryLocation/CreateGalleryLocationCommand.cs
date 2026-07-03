using MediatR;
using PEMS.Application.Galleries.Common;

namespace PEMS.Application.Galleries.Commands.CreateGalleryLocation;

/// <summary>
/// UC-LOC-04 (add location into an existing area) / UC-LOC-05 (create a new area + its first location).
/// <c>Mode</c> is EXISTING_AREA or NEW_AREA. Campus, keys and audit fields are derived server-side.
/// The cover images (buffered by the controller) are mandatory master-data images: a location always
/// needs one; a brand-new area needs one too. Each cover is exactly one image, not a gallery item.
/// </summary>
public sealed record CreateGalleryLocationCommand(
    string Mode,
    long? AreaId,
    string? NewAreaName,
    string LocationName,
    GalleryUploadFileCommandDto? AreaCoverImage,
    GalleryUploadFileCommandDto? LocationCoverImage) : IRequest<GalleryLocationDetailDto>;
