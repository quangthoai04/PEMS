using MediatR;

namespace PEMS.Application.Galleries.Tts.Commands.RegenerateGalleryItemTtsAudio;

/// <summary>
/// Staff Leader "Tạo lại audio" (<c>POST /api/gallery-management/items/{id}/tts-audio/regenerate</c>):
/// forces a fresh MANUAL_REGENERATE job for the item's current description + settings, bypassing the
/// failed cooldown. Campus-scoped like every other gallery management action.
/// </summary>
public sealed record RegenerateGalleryItemTtsAudioCommand(long GalleryItemId)
    : IRequest<GalleryItemTtsAudioResponse>;
