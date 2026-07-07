namespace PEMS.Application.Galleries.Tts;

/// <summary>
/// Thin HTTP client for the EverAI TTS REST API (implemented in Infrastructure via HttpClientFactory).
/// Implementations must never log the ApiKey. Only the backend talks to EverAI — the frontend never
/// calls it directly, and the temporary <c>audio_link</c> is only ever used by
/// <see cref="DownloadAudioAsync"/> for the one-time transfer to Google Drive.
/// </summary>
public interface IEverAiTtsClient
{
    Task<EverAiCreateTtsResponse> CreateAsync(
        EverAiCreateTtsRequest request,
        CancellationToken cancellationToken);

    Task<EverAiGetTtsResponse> GetRequestAsync(
        string requestId,
        CancellationToken cancellationToken);

    /// <summary>Downloads the generated audio from EverAI's temporary <c>audio_link</c>.</summary>
    Task<EverAiAudioDownload> DownloadAudioAsync(
        string audioLink,
        CancellationToken cancellationToken);
}
