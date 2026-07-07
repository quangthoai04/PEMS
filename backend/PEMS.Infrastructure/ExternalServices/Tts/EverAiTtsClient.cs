using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PEMS.Application.Common.Options;
using PEMS.Application.Galleries.Tts;

namespace PEMS.Infrastructure.ExternalServices.Tts;

/// <summary>
/// HttpClientFactory-typed client for the EverAI TTS REST API. The ApiKey rides only in the
/// Authorization header — it is never logged and never appears in any exception path here.
/// <see cref="DownloadAudioAsync"/> fetches the temporary <c>audio_link</c> WITHOUT the key (it is a
/// pre-signed public link, and leaking the bearer to a third-party host must be avoided).
/// </summary>
public sealed class EverAiTtsClient : IEverAiTtsClient
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    // Outer safety cap; the real per-purpose limit is enforced by FileValidationPolicy on upload.
    private const long MaxAudioDownloadBytes = 50L * 1024 * 1024;

    private readonly HttpClient _http;
    private readonly EverAiTtsOptions _options;
    private readonly ILogger<EverAiTtsClient> _logger;

    public EverAiTtsClient(HttpClient http, IOptions<EverAiTtsOptions> options, ILogger<EverAiTtsClient> logger)
    {
        _http = http;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<EverAiCreateTtsResponse> CreateAsync(
        EverAiCreateTtsRequest request, CancellationToken cancellationToken)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, BuildUrl("/tts"))
        {
            Content = JsonContent.Create(request, options: Json),
        };
        Authorize(message);

        using var response = await _http.SendAsync(message, cancellationToken);
        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            _logger.LogError("EverAI rejected the create request with {StatusCode} (check the API key).",
                (int)response.StatusCode);
            return new EverAiCreateTtsResponse
            {
                Status = 0,
                ErrorCode = GalleryTtsErrorCodes.EverAiAuthFailed,
                ErrorMessage = "EverAI từ chối xác thực API key.",
            };
        }

        var parsed = await ReadJsonAsync<EverAiCreateTtsResponse>(response, cancellationToken);
        return parsed ?? new EverAiCreateTtsResponse
        {
            Status = 0,
            ErrorCode = GalleryTtsErrorCodes.EverAiRequestFailed,
            ErrorMessage = $"EverAI trả về phản hồi không đọc được (HTTP {(int)response.StatusCode}).",
        };
    }

    public async Task<EverAiGetTtsResponse> GetRequestAsync(
        string requestId, CancellationToken cancellationToken)
    {
        using var message = new HttpRequestMessage(HttpMethod.Get, BuildUrl($"/tts/{Uri.EscapeDataString(requestId)}"));
        Authorize(message);

        using var response = await _http.SendAsync(message, cancellationToken);
        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            return new EverAiGetTtsResponse
            {
                Status = 0,
                ErrorCode = GalleryTtsErrorCodes.EverAiAuthFailed,
                ErrorMessage = "EverAI từ chối xác thực API key.",
            };
        }

        var parsed = await ReadJsonAsync<EverAiGetTtsResponse>(response, cancellationToken);
        return parsed ?? new EverAiGetTtsResponse
        {
            Status = 0,
            ErrorCode = GalleryTtsErrorCodes.EverAiRequestFailed,
            ErrorMessage = $"EverAI trả về phản hồi không đọc được (HTTP {(int)response.StatusCode}).",
        };
    }

    public async Task<EverAiAudioDownload> DownloadAudioAsync(
        string audioLink, CancellationToken cancellationToken)
    {
        if (!Uri.TryCreate(audioLink, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
        {
            throw new InvalidOperationException("EverAI audio_link không phải là URL hợp lệ.");
        }

        // Deliberately no Authorization header: audio_link points at EverAI's file host, not the API.
        using var response = await _http.GetAsync(
            uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        if (response.Content.Headers.ContentLength is { } length && length > MaxAudioDownloadBytes)
            throw new InvalidOperationException("File audio EverAI vượt quá kích thước cho phép.");

        var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
        if (bytes.LongLength > MaxAudioDownloadBytes)
            throw new InvalidOperationException("File audio EverAI vượt quá kích thước cho phép.");

        return new EverAiAudioDownload(bytes, response.Content.Headers.ContentType?.MediaType);
    }

    private void Authorize(HttpRequestMessage message)
        => message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);

    private string BuildUrl(string path) => _options.BaseUrl.TrimEnd('/') + path;

    private async Task<T?> ReadJsonAsync<T>(HttpResponseMessage response, CancellationToken cancellationToken)
        where T : class
    {
        try
        {
            return await response.Content.ReadFromJsonAsync<T>(Json, cancellationToken);
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException)
        {
            _logger.LogWarning(ex, "EverAI response (HTTP {StatusCode}) could not be parsed as JSON.",
                (int)response.StatusCode);
            return null;
        }
    }
}
