using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Common.Models;
using PEMS.Application.Common.Storage;

namespace PEMS.Infrastructure.FileStorage.GoogleDrive;

/// <summary>
/// Dependency-free Google Drive client (no Google SDK): talks the raw REST API over
/// <see cref="IHttpClientFactory"/>. Mints a short-lived access token from the configured
/// long-lived <c>RefreshToken</c>, then uploads/downloads/deletes files. Used for user avatars.
///
/// Errors are surfaced as <see cref="BusinessRuleException"/> with stable error codes so the UI
/// can react: <c>GOOGLE_DRIVE_NOT_CONNECTED</c> (no refresh token configured),
/// <c>GOOGLE_DRIVE_TOKEN_EXPIRED</c> (Google returned <c>invalid_grant</c>),
/// <c>UPLOAD_AVATAR_FAILED</c> (any other Drive failure).
/// </summary>
public sealed class GoogleDriveStorageService : IGoogleDriveStorageService
{
    private const string TokenEndpoint = "https://oauth2.googleapis.com/token";
    private const string UploadEndpoint =
        "https://www.googleapis.com/upload/drive/v3/files?uploadType=multipart&fields=id,name,size,webViewLink,webContentLink,thumbnailLink";
    private const string FilesEndpoint = "https://www.googleapis.com/drive/v3/files";

    private readonly GoogleDriveOptions _options;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<GoogleDriveStorageService> _logger;

    public GoogleDriveStorageService(
        IOptions<GoogleDriveOptions> options,
        IHttpClientFactory httpClientFactory,
        ILogger<GoogleDriveStorageService> logger)
    {
        _options = options.Value;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<GoogleDriveUploadResult> UploadAvatarAsync(
        byte[] content, string driveFileName, string contentType, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.AvatarFolderId))
            throw new BusinessRuleException(
                "Google Drive chưa được cấu hình thư mục ảnh đại diện.", "GOOGLE_DRIVE_NOT_CONNECTED");

        var accessToken = await GetAccessTokenAsync(cancellationToken);
        var client = _httpClientFactory.CreateClient();

        // multipart/related: a JSON metadata part followed by the raw media part.
        var metadata = JsonSerializer.Serialize(new
        {
            name = driveFileName,
            parents = new[] { _options.AvatarFolderId },
        });

        using var multipart = new MultipartContent("related");
        var metaPart = new StringContent(metadata, Encoding.UTF8);
        metaPart.Headers.ContentType = new MediaTypeHeaderValue("application/json") { CharSet = "UTF-8" };
        multipart.Add(metaPart);

        var mediaPart = new ByteArrayContent(content);
        mediaPart.Headers.ContentType = new MediaTypeHeaderValue(
            string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType);
        multipart.Add(mediaPart);

        using var request = new HttpRequestMessage(HttpMethod.Post, UploadEndpoint) { Content = multipart };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        HttpResponseMessage response;
        try
        {
            response = await client.SendAsync(request, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Google Drive avatar upload request failed.");
            throw new BusinessRuleException(
                "Không thể cập nhật ảnh đại diện. Vui lòng thử lại.", "UPLOAD_AVATAR_FAILED");
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Google Drive upload returned {Status}: {Body}", (int)response.StatusCode, body);
            throw new BusinessRuleException(
                "Không thể cập nhật ảnh đại diện. Vui lòng thử lại.", "UPLOAD_AVATAR_FAILED");
        }

        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;
        var id = root.TryGetProperty("id", out var idEl) ? idEl.GetString() : null;
        if (string.IsNullOrWhiteSpace(id))
        {
            _logger.LogError("Google Drive upload succeeded but returned no file id: {Body}", body);
            throw new BusinessRuleException(
                "Không thể cập nhật ảnh đại diện. Vui lòng thử lại.", "UPLOAD_AVATAR_FAILED");
        }

        long size = content.LongLength;
        if (root.TryGetProperty("size", out var sizeEl) && sizeEl.ValueKind == JsonValueKind.String
            && long.TryParse(sizeEl.GetString(), out var parsedSize))
            size = parsedSize;

        return new GoogleDriveUploadResult
        {
            ExternalFileId = id!,
            WebViewUrl = root.TryGetProperty("webViewLink", out var wv) ? wv.GetString() : null,
            DownloadUrl = root.TryGetProperty("webContentLink", out var wc) ? wc.GetString() : null,
            ThumbnailUrl = root.TryGetProperty("thumbnailLink", out var tn) ? tn.GetString() : null,
            FileSize = size,
        };
    }

    public async Task<Stream> DownloadAsync(string externalFileId, CancellationToken cancellationToken = default)
    {
        var accessToken = await GetAccessTokenAsync(cancellationToken);
        var client = _httpClientFactory.CreateClient();

        var url = $"{FilesEndpoint}/{Uri.EscapeDataString(externalFileId)}?alt=media";
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("Google Drive download returned {Status}: {Body}", (int)response.StatusCode, body);
            throw new BusinessRuleException(
                "Không thể tải ảnh từ Google Drive.", "UPLOAD_AVATAR_FAILED");
        }

        // Buffer to memory: avatars are small and the HttpResponseMessage would otherwise be disposed
        // before the caller streams it.
        var ms = new MemoryStream();
        await response.Content.CopyToAsync(ms, cancellationToken);
        response.Dispose();
        ms.Position = 0;
        return ms;
    }

    public async Task DeleteAsync(string externalFileId, CancellationToken cancellationToken = default)
    {
        var accessToken = await GetAccessTokenAsync(cancellationToken);
        var client = _httpClientFactory.CreateClient();

        var url = $"{FilesEndpoint}/{Uri.EscapeDataString(externalFileId)}";
        using var request = new HttpRequestMessage(HttpMethod.Delete, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var response = await client.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogWarning("Google Drive delete returned {Status} for {FileId}: {Body}",
                (int)response.StatusCode, externalFileId, body);
        }
    }

    /// <summary>Exchanges the configured refresh token for a short-lived access token.</summary>
    private async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.RefreshToken))
            throw new BusinessRuleException(
                "Google Drive chưa được kết nối. Vui lòng liên hệ người phụ trách cấu hình.",
                "GOOGLE_DRIVE_NOT_CONNECTED");

        if (string.IsNullOrWhiteSpace(_options.ClientId) || string.IsNullOrWhiteSpace(_options.ClientSecret))
            throw new BusinessRuleException(
                "Google Drive chưa được cấu hình đầy đủ (ClientId/ClientSecret).",
                "GOOGLE_DRIVE_NOT_CONNECTED");

        var client = _httpClientFactory.CreateClient();
        using var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"] = _options.ClientId!,
            ["client_secret"] = _options.ClientSecret!,
            ["refresh_token"] = _options.RefreshToken!,
            ["grant_type"] = "refresh_token",
        });

        HttpResponseMessage response;
        try
        {
            response = await client.PostAsync(TokenEndpoint, form, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Google Drive token request failed.");
            throw new BusinessRuleException(
                "Không thể kết nối Google Drive. Vui lòng thử lại.", "UPLOAD_AVATAR_FAILED");
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            // invalid_grant ⇒ the refresh token was revoked or expired (Drive "Testing" apps: ~7 days).
            if (body.Contains("invalid_grant", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("Google Drive refresh token expired/revoked: {Body}", body);
                throw new BusinessRuleException(
                    "Token Google Drive đã hết hạn. Vui lòng kết nối lại Google Drive.",
                    "GOOGLE_DRIVE_TOKEN_EXPIRED");
            }

            _logger.LogError("Google Drive token endpoint returned {Status}: {Body}", (int)response.StatusCode, body);
            throw new BusinessRuleException(
                "Không thể kết nối Google Drive. Vui lòng thử lại.", "UPLOAD_AVATAR_FAILED");
        }

        using var doc = JsonDocument.Parse(body);
        if (!doc.RootElement.TryGetProperty("access_token", out var tokenEl)
            || tokenEl.GetString() is not { Length: > 0 } accessToken)
        {
            _logger.LogError("Google Drive token response missing access_token: {Body}", body);
            throw new BusinessRuleException(
                "Không thể kết nối Google Drive. Vui lòng thử lại.", "UPLOAD_AVATAR_FAILED");
        }

        return accessToken;
    }
}
