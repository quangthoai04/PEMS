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
                "Không thể kết nối hoặc tải ảnh lên Google Drive (Authentication/Network failed).", "GOOGLE_DRIVE_AUTH_FAILED");
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Google Drive upload returned {Status}: {Body}", (int)response.StatusCode, body);
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                throw new BusinessRuleException("Thư mục Google Drive không tồn tại hoặc không có quyền truy cập.", "GOOGLE_DRIVE_FOLDER_NOT_FOUND_OR_NO_PERMISSION");
            }
            throw new BusinessRuleException(
                "Không thể tải ảnh lên Google Drive.", "GOOGLE_DRIVE_UPLOAD_FAILED");
        }

        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;
        var id = root.TryGetProperty("id", out var idEl) ? idEl.GetString() : null;
        if (string.IsNullOrWhiteSpace(id))
        {
            _logger.LogError("Google Drive upload succeeded but returned no file id: {Body}", body);
            throw new BusinessRuleException(
                "Google Drive không trả về URL của file.", "GOOGLE_DRIVE_FILE_URL_MISSING");
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

    public async Task<GoogleDriveUploadResult> UploadFileAsync(
        byte[] content, string driveFileName, string contentType, string? folderId = null, CancellationToken cancellationToken = default)
    {
        var targetFolderId = folderId ?? _options.RootFolderId;
        if (string.IsNullOrWhiteSpace(targetFolderId))
            throw new BusinessRuleException(
                "Google Drive chưa được cấu hình thư mục RootFolderId.", "GOOGLE_DRIVE_NOT_CONNECTED");

        var accessToken = await GetAccessTokenAsync(cancellationToken);
        var client = _httpClientFactory.CreateClient();

        var metadata = JsonSerializer.Serialize(new
        {
            name = driveFileName,
            parents = new[] { targetFolderId },
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
            _logger.LogError(ex, "Google Drive file upload request failed.");
            throw new BusinessRuleException(
                "Không thể kết nối hoặc tải tệp lên Google Drive (Authentication/Network failed).", "GOOGLE_DRIVE_AUTH_FAILED");
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Google Drive upload returned {Status}: {Body}", (int)response.StatusCode, body);
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound || body.Contains("notFound"))
            {
                throw new BusinessRuleException("Thư mục Google Drive không tồn tại hoặc không có quyền truy cập.", "GOOGLE_DRIVE_FOLDER_NOT_FOUND_OR_NO_PERMISSION");
            }
            throw new BusinessRuleException(
                $"Không thể tải tệp lên Google Drive.", "GOOGLE_DRIVE_UPLOAD_FAILED");
        }

        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;
        var id = root.TryGetProperty("id", out var idEl) ? idEl.GetString() : null;
        if (string.IsNullOrWhiteSpace(id))
        {
            _logger.LogError("Google Drive upload succeeded but returned no file id: {Body}", body);
            throw new BusinessRuleException(
                "Google Drive không trả về URL của file.", "GOOGLE_DRIVE_FILE_URL_MISSING");
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
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                throw new BusinessRuleException("Không tìm thấy tệp đính kèm trên Google Drive (Đã bị xóa hoặc không có quyền).", "EMAIL_ATTACHMENT_FILE_NOT_FOUND");
            }
            throw new BusinessRuleException(
                "Không thể tải tệp từ Google Drive.", "EMAIL_ATTACHMENT_DOWNLOAD_FAILED");
        }

        // Buffer to memory: avatars are small and the HttpResponseMessage would otherwise be disposed
        // before the caller streams it.
        var ms = new MemoryStream();
        await response.Content.CopyToAsync(ms, cancellationToken);
        response.Dispose();
        ms.Position = 0;
        return ms;
    }

    public async Task<GoogleDriveDownloadResult> DownloadRangeAsync(
        string externalFileId, long? from, long? to, CancellationToken cancellationToken = default)
    {
        var accessToken = await GetAccessTokenAsync(cancellationToken);
        var client = _httpClientFactory.CreateClient();

        var url = $"{FilesEndpoint}/{Uri.EscapeDataString(externalFileId)}?alt=media";
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        // Forward the byte range to Drive so playback can seek without downloading the whole file.
        var wantRange = from.HasValue || to.HasValue;
        if (wantRange)
        {
            var rangeStart = from ?? 0;
            request.Headers.Range = to.HasValue
                ? new RangeHeaderValue(rangeStart, to.Value)
                : new RangeHeaderValue(rangeStart, null);
        }

        HttpResponseMessage response;
        try
        {
            response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        }
        catch (Exception ex)
        {
            request.Dispose();
            _logger.LogError(ex, "Google Drive range download request failed.");
            throw new BusinessRuleException(
                "Không thể tải tệp từ Google Drive.", "GOOGLE_DRIVE_DOWNLOAD_FAILED");
        }

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("Google Drive range download returned {Status}: {Body}", (int)response.StatusCode, body);
            response.Dispose();
            request.Dispose();
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                throw new BusinessRuleException(
                    "Không tìm thấy tệp trên Google Drive.", "GOOGLE_DRIVE_FILE_NOT_FOUND");
            throw new BusinessRuleException(
                "Không thể tải tệp từ Google Drive.", "GOOGLE_DRIVE_DOWNLOAD_FAILED");
        }

        var isPartial = response.StatusCode == System.Net.HttpStatusCode.PartialContent;
        long? total = null;
        long rangeStartOut = 0;
        long? rangeEndOut = null;

        var contentRange = response.Content.Headers.ContentRange;
        if (isPartial && contentRange is { HasRange: true })
        {
            rangeStartOut = contentRange.From ?? 0;
            rangeEndOut = contentRange.To;
            total = contentRange.Length;
        }

        var contentLength = response.Content.Headers.ContentLength;
        total ??= contentLength;
        if (rangeEndOut is null && total is { } t && t > 0)
            rangeEndOut = t - 1;

        var contentType = response.Content.Headers.ContentType?.ToString();
        var stream = await response.Content.ReadAsStreamAsync(cancellationToken);

        return new GoogleDriveDownloadResult
        {
            Stream = new ResponseOwningStream(stream, response, request),
            TotalLength = total,
            ContentLength = contentLength,
            RangeStart = rangeStartOut,
            RangeEnd = rangeEndOut ?? (total is { } tt && tt > 0 ? tt - 1 : 0),
            IsPartial = isPartial,
            ContentType = contentType,
        };
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

    public async Task<GoogleDriveFolderResult> EnsureChildFolderAsync(
        string folderName, string parentFolderId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(folderName))
            throw new BusinessRuleException("Tên thư mục Google Drive không được để trống.", "GOOGLE_DRIVE_FOLDER_NAME_EMPTY");
        if (string.IsNullOrWhiteSpace(parentFolderId))
            throw new BusinessRuleException(
                "Google Drive chưa được cấu hình thư mục cha.", "GOOGLE_DRIVE_FOLDER_NOT_CONFIGURED");

        var accessToken = await GetAccessTokenAsync(cancellationToken);
        var client = _httpClientFactory.CreateClient();

        // 1) Reuse an existing (non-trashed) child folder with the same name — keeps the call
        //    idempotent so concurrent first-uploads cannot fan out into duplicate folders.
        var escapedName = folderName.Replace("\\", "\\\\").Replace("'", "\\'");
        var query = Uri.EscapeDataString(
            $"name = '{escapedName}' and '{parentFolderId}' in parents " +
            "and mimeType = 'application/vnd.google-apps.folder' and trashed = false");
        var searchUrl = $"{FilesEndpoint}?q={query}&fields=files(id,webViewLink)&pageSize=1";

        using (var searchRequest = new HttpRequestMessage(HttpMethod.Get, searchUrl))
        {
            searchRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            using var searchResponse = await client.SendAsync(searchRequest, cancellationToken);
            var searchBody = await searchResponse.Content.ReadAsStringAsync(cancellationToken);
            if (!searchResponse.IsSuccessStatusCode)
            {
                _logger.LogError("Google Drive folder search returned {Status}: {Body}",
                    (int)searchResponse.StatusCode, searchBody);
                throw new BusinessRuleException(
                    "Không thể truy vấn thư mục trên Google Drive.", "GOOGLE_DRIVE_FOLDER_LOOKUP_FAILED");
            }

            using var searchDoc = JsonDocument.Parse(searchBody);
            if (searchDoc.RootElement.TryGetProperty("files", out var filesEl)
                && filesEl.ValueKind == JsonValueKind.Array
                && filesEl.GetArrayLength() > 0)
            {
                var existing = filesEl[0];
                var existingId = existing.TryGetProperty("id", out var exId) ? exId.GetString() : null;
                if (!string.IsNullOrWhiteSpace(existingId))
                {
                    return new GoogleDriveFolderResult
                    {
                        ExternalFolderId = existingId!,
                        WebViewUrl = existing.TryGetProperty("webViewLink", out var exWv) ? exWv.GetString() : null,
                    };
                }
            }
        }

        // 2) Not found — create it.
        var metadata = JsonSerializer.Serialize(new
        {
            name = folderName,
            mimeType = "application/vnd.google-apps.folder",
            parents = new[] { parentFolderId },
        });

        using var createRequest = new HttpRequestMessage(
            HttpMethod.Post, $"{FilesEndpoint}?fields=id,webViewLink")
        {
            Content = new StringContent(metadata, Encoding.UTF8, "application/json"),
        };
        createRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var createResponse = await client.SendAsync(createRequest, cancellationToken);
        var createBody = await createResponse.Content.ReadAsStringAsync(cancellationToken);
        if (!createResponse.IsSuccessStatusCode)
        {
            _logger.LogError("Google Drive folder create returned {Status}: {Body}",
                (int)createResponse.StatusCode, createBody);
            if (createResponse.StatusCode == System.Net.HttpStatusCode.NotFound || createBody.Contains("notFound"))
                throw new BusinessRuleException(
                    "Thư mục Google Drive không tồn tại hoặc không có quyền truy cập.",
                    "GOOGLE_DRIVE_FOLDER_NOT_FOUND_OR_NO_PERMISSION");
            throw new BusinessRuleException(
                "Không thể tạo thư mục trên Google Drive.", "GOOGLE_DRIVE_FOLDER_CREATE_FAILED");
        }

        using var createDoc = JsonDocument.Parse(createBody);
        var createdRoot = createDoc.RootElement;
        var createdId = createdRoot.TryGetProperty("id", out var idElement) ? idElement.GetString() : null;
        if (string.IsNullOrWhiteSpace(createdId))
        {
            _logger.LogError("Google Drive folder create succeeded but returned no id: {Body}", createBody);
            throw new BusinessRuleException(
                "Google Drive không trả về ID thư mục.", "GOOGLE_DRIVE_FOLDER_CREATE_FAILED");
        }

        return new GoogleDriveFolderResult
        {
            ExternalFolderId = createdId!,
            WebViewUrl = createdRoot.TryGetProperty("webViewLink", out var wvEl) ? wvEl.GetString() : null,
        };
    }

    /// <summary>
    /// A read stream that also owns the HTTP response/request it was read from, disposing them when the
    /// stream is disposed. This lets us return a live streamed body (no memory buffering) while keeping
    /// the underlying <see cref="HttpResponseMessage"/> alive until the caller finishes reading.
    /// </summary>
    private sealed class ResponseOwningStream : Stream
    {
        private readonly Stream _inner;
        private readonly HttpResponseMessage _response;
        private readonly HttpRequestMessage _request;

        public ResponseOwningStream(Stream inner, HttpResponseMessage response, HttpRequestMessage request)
        {
            _inner = inner;
            _response = response;
            _request = request;
        }

        public override bool CanRead => _inner.CanRead;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => _inner.Length;
        public override long Position { get => _inner.Position; set => throw new NotSupportedException(); }
        public override void Flush() => _inner.Flush();
        public override int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, count);
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken ct)
            => _inner.ReadAsync(buffer, offset, count, ct);
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken ct = default)
            => _inner.ReadAsync(buffer, ct);
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _inner.Dispose();
                _response.Dispose();
                _request.Dispose();
            }
            base.Dispose(disposing);
        }

        public override async ValueTask DisposeAsync()
        {
            await _inner.DisposeAsync();
            _response.Dispose();
            _request.Dispose();
            await base.DisposeAsync();
        }
    }

    /// <summary>Exchanges the configured refresh token for a short-lived access token.</summary>
    private async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.RefreshToken))
            throw new BusinessRuleException(
                "Google Drive chưa được kết nối. Vui lòng liên hệ người phụ trách cấu hình.",
                "GOOGLE_DRIVE_CONFIG_MISSING");

        if (string.IsNullOrWhiteSpace(_options.ClientId) || string.IsNullOrWhiteSpace(_options.ClientSecret))
            throw new BusinessRuleException(
                "Google Drive chưa được cấu hình đầy đủ (ClientId/ClientSecret).",
                "GOOGLE_DRIVE_CONFIG_MISSING");

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
                "Không thể kết nối Google Drive. Vui lòng thử lại.", "GOOGLE_DRIVE_AUTH_FAILED");
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
                "Không thể kết nối Google Drive. Vui lòng thử lại.", "GOOGLE_DRIVE_AUTH_FAILED");
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
