using MediatR;
using PEMS.Application.Galleries.Common;

namespace PEMS.Application.Galleries.Commands.ChangeGalleryLocationStatus;

/// <summary>
/// UC-LOC-08 (enable) / UC-LOC-09 (disable) — toggles <c>gallery_locations.status</c>. Disabling also
/// hides the location's gallery item if it was PUBLISHED; enabling never re-publishes it.
/// </summary>
public sealed record ChangeGalleryLocationStatusCommand(
    long LocationId,
    string Status) : IRequest<GalleryLocationDetailDto>;
