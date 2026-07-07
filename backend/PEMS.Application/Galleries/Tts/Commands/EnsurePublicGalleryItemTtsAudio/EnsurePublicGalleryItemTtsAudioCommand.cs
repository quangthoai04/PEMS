using MediatR;

namespace PEMS.Application.Galleries.Tts.Commands.EnsurePublicGalleryItemTtsAudio;

/// <summary>
/// Public speaker-icon action (<c>POST /api/public/gallery-items/{id}/tts-audio/ensure</c>): returns
/// the current narration when READY, otherwise lazily creates a generation job (LAZY_GENERATE) for the
/// item's current description. Anonymous; the item must be effectively public-visible or it 404s.
/// </summary>
public sealed record EnsurePublicGalleryItemTtsAudioCommand(long GalleryItemId)
    : IRequest<GalleryItemTtsAudioResponse>;
