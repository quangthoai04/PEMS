using MediatR;
using PEMS.Application.Galleries.Public.Common;

namespace PEMS.Application.Galleries.Tts.Queries.GetPublicGalleryItemTtsAudioStatus;

/// <summary>
/// Thin wrapper over <see cref="IGalleryItemTtsService.GetAudioStatusAsync"/> — returns the
/// fine-grained status (the poll contract exposes all of them, unlike the ensure endpoint).
/// </summary>
public sealed class GetPublicGalleryItemTtsAudioStatusQueryHandler
    : IRequestHandler<GetPublicGalleryItemTtsAudioStatusQuery, GalleryItemTtsAudioResponse>
{
    private readonly IGalleryItemTtsService _tts;

    public GetPublicGalleryItemTtsAudioStatusQueryHandler(IGalleryItemTtsService tts) => _tts = tts;

    public async Task<GalleryItemTtsAudioResponse> Handle(
        GetPublicGalleryItemTtsAudioStatusQuery request, CancellationToken cancellationToken)
    {
        var result = await _tts.GetAudioStatusAsync(
            request.GalleryItemId, requirePublicVisible: true, cancellationToken);

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
            TtsAudioStatuses.TemporarilyUnavailable => new GalleryItemTtsAudioResponse
            {
                Status = TtsAudioStatuses.TemporarilyUnavailable,
                Message = GalleryTtsMessages.TemporarilyUnavailable,
            },
            _ => new GalleryItemTtsAudioResponse { Status = result.Status },
        };
    }
}
