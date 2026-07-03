using MediatR;
using PEMS.Application.Galleries.Common;

namespace PEMS.Application.Galleries.Commands.UpdateGalleryLocation;

/// <summary>
/// UC-LOC-06 (rename / move location to an existing area) / UC-LOC-07 (move into a freshly created area).
/// Editing never changes the location's status or its gallery item's status. The location cover image is
/// optional on edit — when omitted the existing cover is kept; when supplied it replaces it. Creating a
/// new area (NEW_AREA mode) always requires an area cover image.
/// </summary>
public sealed record UpdateGalleryLocationCommand(
    long LocationId,
    string Mode,
    long? AreaId,
    string? NewAreaName,
    string LocationName,
    GalleryUploadFileCommandDto? AreaCoverImage,
    GalleryUploadFileCommandDto? LocationCoverImage) : IRequest<GalleryLocationDetailDto>;
