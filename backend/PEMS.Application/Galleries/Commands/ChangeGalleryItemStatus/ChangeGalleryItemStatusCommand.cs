using MediatR;

namespace PEMS.Application.Galleries.Commands.ChangeGalleryItemStatus;

/// <summary>
/// UC-GAL-05 Enable / UC-GAL-06 Disable Gallery Item (Staff Leader). Toggles only
/// <c>gallery_items.status</c> between PUBLISHED and HIDDEN. Campus scope enforced server-side.
/// </summary>
public sealed record ChangeGalleryItemStatusCommand(
    long GalleryItemId,
    string Status) : IRequest<ChangeGalleryItemStatusResponse>;
