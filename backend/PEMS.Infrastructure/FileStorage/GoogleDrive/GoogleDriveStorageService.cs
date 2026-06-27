using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Common.Models;

namespace PEMS.Infrastructure.FileStorage.GoogleDrive;

/// <summary>
/// <see cref="IFileStorageService"/> backed by the Google Drive v3 REST API in OAuth "user"
/// mode. A long-lived refresh token (from config) is exchanged for a short-lived access token
/// per request; uploads/downloads/deletes are plain HTTPS calls so no Google SDK dependency is
/// needed. The refresh token / client secret stay server-side and are never exposed.
/// </summary>
public sealed class GoogleDriveStorageService : IFileStorageService
{
    private const string TokenEndpoint = "https://oauth2.googleapis.com/token";
    private const string UploadEndpoint = "https://www.googleapis.com/upload/drive/v3/files?uploadType=multipart&fields=id,webViewLink,webContentLink,thumbnailLink";
    private const string FilesEndpoint = "https://www.googleapis.com/drive/v3/files";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly GoogleDriveOptions _options;
    private readonly ILogger<GoogleDriveStorageService> _logger;

    public GoogleDriveStorageService(
        IHttpClientFactory httpClientFactory,
        IOptions<GoogleDriveOptions> options,
        ILogger<GoogleDriveStorageService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<FileStorageUploadResult> UploadAsync(
        Stream stream, string fileName, string contentType, string folderId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(folderId))
            throw new BusinessRuleException("Thư mục lưu trữ trên Google Drive chưa được cấu hình.", "GOOGLE_DRIVE_NOT_CONNECTED");

        var accessToken = await GetAccessTokenAsync(cancellationToken);
        var http = _httpClientFactory.CreateClient();

        // multipart/related: part 1 = JSON metadata, part 2 = the binary media.
        var metadata = new { name = fileName, parents = new[] { folderId } };
        var multipart = new MultipartContent("related")
        {
            JsonContent.Create(metadata, options: JsonOptions),
            BuildMediaPart(stream, contentType),
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, UploadEndpoint) { Content = multipart };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var response = await http.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("Google Drive upload failed ({Status}): {Body}", (int)response.StatusCode, body);
            throw new BusinessRuleException("Không thể tải tệp lên Google Drive.", "UPLOAD_AVATAR_FAILED");
        }

        var file = await response.Content.ReadFromJsonAsync<DriveFile>(JsonOptions, cancellationToken)
            ?? throw new BusinessRuleException("Phản hồi từ Google Drive không hợp lệ.", "UPLOAD_AVATAR_FAILED");

        return new FileStorageUploadResult
        {
            StorageProvider = "GOOGLE_DRIVE",
            ExternalFileId = file.Id,
            WebViewUrl = file.WebViewLink,
            DownloadUrl = file.WebContentLink,
            ThumbnailUrl = file.ThumbnailLink,
            MimeType = contentType,
        };
    }

    public async Task<Stream> DownloadAsync(string externalFileId, CancellationToken cancellationToken)
    {
        var accessToken = await GetAccessTokenAsync(cancellationToken);
        var http = _httpClientFactory.CreateClient();

        var url = $"{FilesEndpoint}/{Uri.EscapeDataString(externalFileId)}?alt=media";
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var response = await http.SendAsync(request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
            throw new NotFoundException("File", externalFileId);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("Google Drive download failed ({Status}): {Body}", (int)response.StatusCode, body);
            throw new BusinessRuleException("Không thể tải tệp từ Google Drive.", "UPLOAD_AVATAR_FAILED");
        }

        // Buffer into memory (avatars are small, ≤2MB) so the HttpResponseMessage can be disposed
        // here — the caller gets a self-contained, seekable stream.
        var buffer = new MemoryStream();
        await response.Content.CopyToAsync(buffer, cancellationToken);
        buffer.Position = 0;
        return buffer;
    }

    public async Task DeleteAsync(string externalFileId, CancellationToken cancellationToken)
    {
        var accessToken = await GetAccessTokenAsync(cancellationToken);
        var http = _httpClientFactory.CreateClient();

        var url = $"{FilesEndpoint}/{Uri.EscapeDataString(externalFileId)}";
        using var request = new HttpRequestMessage(HttpMethod.Delete, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var response = await http.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode && response.StatusCode != HttpStatusCode.NotFound)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogWarning("Google Drive delete failed ({Status}): {Body}", (int)response.StatusCode, body);
        }
    }

    private static StreamContent BuildMediaPart(Stream stream, string contentType)
    {
        var media = new StreamContent(stream);
        media.Headers.ContentType = MediaTypeHeaderValue.Parse(string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType);
        return media;
    }

    /// <summary>
    /// Exchanges the configured refresh token for a fresh access token. Surfaces clear business
    /// errors when Drive is not connected (no refresh token) or the token was revoked/expired.
    /// </summary>
    private async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken)
    {
        if (!_options.Enabled)
            throw new BusinessRuleException("Tích hợp Google Drive đang tắt.", "GOOGLE_DRIVE_NOT_CONNECTED");

        if (string.IsNullOrWhiteSpace(_options.RefreshToken)
            || string.IsNullOrWhiteSpace(_options.ClientId)
            || string.IsNullOrWhiteSpace(_options.ClientSecret))
        {
            throw new BusinessRuleException("Google Drive chưa được kết nối.", "GOOGLE_DRIVE_NOT_CONNECTED");
        }

        var http = _httpClientFactory.CreateClient();
        using var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"] = _options.ClientId,
            ["client_secret"] = _options.ClientSecret,
            ["refresh_token"] = _options.RefreshToken!,
            ["grant_type"] = "refresh_token",
        });

        using var response = await http.PostAsync(TokenEndpoint, form, cancellationToken);
        var payload = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            // Google returns 400 invalid_grant when the refresh token is expired/revoked.
            if (payload.Contains("invalid_grant", StringComparison.OrdinalIgnoreCase))
                throw new BusinessRuleException("Token Google Drive đã hết hạn, vui lòng kết nối lại.", "GOOGLE_DRIVE_TOKEN_EXPIRED");

            _logger.LogError("Google Drive token refresh failed ({Status}): {Body}", (int)response.StatusCode, payload);
            throw new BusinessRuleException("Không thể xác thực với Google Drive.", "GOOGLE_DRIVE_NOT_CONNECTED");
        }

        var token = JsonSerializer.Deserialize<TokenResponse>(payload, JsonOptions);
        if (token is null || string.IsNullOrWhiteSpace(token.AccessToken))
            throw new BusinessRuleException("Không thể xác thực với Google Drive.", "GOOGLE_DRIVE_NOT_CONNECTED");

        return token.AccessToken;
    }

    private sealed class TokenResponse
    {
        [JsonPropertyName("access_token")] public string? AccessToken { get; set; }
        [JsonPropertyName("expires_in")] public int ExpiresIn { get; set; }
        [JsonPropertyName("token_type")] public string? TokenType { get; set; }
    }

    private sealed class DriveFile
    {
        [JsonPropertyName("id")] public string Id { get; set; } = default!;
        [JsonPropertyName("webViewLink")] public string? WebViewLink { get; set; }
        [JsonPropertyName("webContentLink")] public string? WebContentLink { get; set; }
        [JsonPropertyName("thumbnailLink")] public string? ThumbnailLink { get; set; }
    }
}
