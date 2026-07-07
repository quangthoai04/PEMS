using MediatR;
using PEMS.Application.Galleries.Public.Common;

namespace PEMS.Application.Galleries.Tts.Commands.EnsurePublicGalleryItemTtsAudio;

/// <summary>
/// Thin wrapper over <see cref="IGalleryItemTtsService.EnsureAudioAsync"/>. Maps the fine-grained
/// service statuses onto the public ensure contract (READY / PROCESSING / TEMPORARILY_UNAVAILABLE) —
/// DISABLED and INVALID_DESCRIPTION deliberately look like a temporary problem to anonymous visitors.
/// The READY audio URL uses the anonymous gallery-scoped media proxy (<c>/api/files</c> requires auth).
/// </summary>
public sealed class EnsurePublicGalleryItemTtsAudioCommandHandler
    : IRequestHandler<EnsurePublicGalleryItemTtsAudioCommand, GalleryItemTtsAudioResponse>
{
    private readonly IGalleryItemTtsService _tts;

    public EnsurePublicGalleryItemTtsAudioCommandHandler(IGalleryItemTtsService tts) => _tts = tts;

    public async Task<GalleryItemTtsAudioResponse> Handle(
        EnsurePublicGalleryItemTtsAudioCommand request, CancellationToken cancellationToken)
    {
        var result = await _tts.EnsureAudioAsync(
            request.GalleryItemId,
            TtsTriggerSources.LazyGenerate,
            actorUserId: null,
            requirePublicVisible: true,
            bypassFailedCooldown: false,
            cancellationToken);

        return result.Status switch
        {
            TtsAudioStatuses.Ready => new GalleryItemTtsAudioResponse
            {
                Status = TtsAudioStatuses.Ready,
                AudioUrl = PublicGalleryFileUrls.Content((ulong)result.AudioFileId!.Value),
                VoiceCode = result.VoiceCode,
                AudioType = result.AudioType,
            },
            TtsAudioStatuses.Processing => new GalleryItemTtsAudioResponse
            {
                Status = TtsAudioStatuses.Processing,
                Message = GalleryTtsMessages.Processing,
            },
            _ => new GalleryItemTtsAudioResponse
            {
                Status = TtsAudioStatuses.TemporarilyUnavailable,
                Message = GalleryTtsMessages.TemporarilyUnavailable,
            },
        };
    }
}
