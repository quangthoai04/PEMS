using MediatR;

namespace PEMS.Application.Galleries.Tts.Queries.GetPublicGalleryItemTtsAudioStatus;

/// <summary>
/// Public poll endpoint (<c>GET /api/public/gallery-items/{id}/tts-audio</c>): read-only status of the
/// item's CURRENT narration — never creates a job. Statuses: READY / PROCESSING / NOT_CREATED /
/// TEMPORARILY_UNAVAILABLE / DISABLED / INVALID_DESCRIPTION.
/// </summary>
public sealed record GetPublicGalleryItemTtsAudioStatusQuery(long GalleryItemId)
    : IRequest<GalleryItemTtsAudioResponse>;
