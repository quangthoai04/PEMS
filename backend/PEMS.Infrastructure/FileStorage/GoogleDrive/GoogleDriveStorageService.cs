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
/// <see cref="IHttpClientFactory"/>. Mints a short-lived access token from the long-lived refresh token
/// supplied by <see cref="IGoogleDriveCredentialResolver"/>, then uploads/downloads/deletes files.
///
/// Errors are surfaced as <see cref="BusinessRuleException"/> carrying a
/// <see cref="GoogleDriveErrorCodes"/> value for connection/write failures and a
/// <see cref="StorageErrorCodes"/> value for per-file read failures. The split matters: the first group
/// is about the integration (configuration, token, reachability) and the second is about one document.
///
/// <para>
/// No failure may borrow another's code. "Không thể kết nối Google Drive. Vui lòng thử lại." was the
/// answer to a missing network, a rejected client secret, a 500 from Google and a malformed token
/// response alike, and it is honest advice for only one of them.
/// </para>
/// </summary>
public sealed class GoogleDriveStorageService : IGoogleDriveStorageService
{
    private const string TokenEndpoint = "https://oauth2.googleapis.com/token";
    private const string UploadEndpoint =
        "https://www.googleapis.com/upload/drive/v3/files?uploadType=multipart&fields=id,name,size,webViewLink,webContentLink,thumbnailLink";
    private const string FilesEndpoint = "https://www.googleapis.com/drive/v3/files";

    private readonly GoogleDriveOptions _options;
    private readonly IGoogleDriveCredentialResolver _credentialResolver;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<GoogleDriveStorageService> _logger;

    public GoogleDriveStorageService(
        IOptions<GoogleDriveOptions> options,
        IGoogleDriveCredentialResolver credentialResolver,
        IHttpClientFactory httpClientFactory,
        ILogger<GoogleDriveStorageService> logger)
    {
        _options = options.Value;
        _credentialResolver = credentialResolver;
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
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // The token was already minted, so this is transport — not authentication.
            _logger.LogError(ex, "Google Drive avatar upload could not reach the upload endpoint.");
            throw new BusinessRuleException(
                "Google Drive đang tạm thời không khả dụng. Vui lòng thử lại sau ít phút.",
                GoogleDriveErrorCodes.Unavailable);
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Google Drive upload returned {Status}: {Body}", (int)response.StatusCode, body);
            throw ClassifyUploadFailure(response.StatusCode, body, "ảnh");
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
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Google Drive file upload could not reach the upload endpoint.");
            throw new BusinessRuleException(
                "Google Drive đang tạm thời không khả dụng. Vui lòng thử lại sau ít phút.",
                GoogleDriveErrorCodes.Unavailable);
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Google Drive upload returned {Status}: {Body}", (int)response.StatusCode, body);
            throw ClassifyUploadFailure(response.StatusCode, body, "tệp");
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
            _logger.LogError(
                "Google Drive download returned {Status} for externalFileId={ExternalFileId}: {Body}",
                (int)response.StatusCode, externalFileId, body);
            throw DownloadFailure(response.StatusCode, externalFileId);
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
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            request.Dispose();
            // Same code as every other unreachable-storage path, so a caller (and StoredFileProbe) can
            // tell "we could not ask" from "the file is gone" without knowing which method it called.
            _logger.LogError(ex, "Google Drive range download could not reach the files endpoint.");
            throw new BusinessRuleException(
                "Không kết nối được tới Google Drive để tải tệp.", StorageErrorCodes.Unavailable);
        }

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError(
                "Google Drive range download returned {Status} for externalFileId={ExternalFileId}: {Body}",
                (int)response.StatusCode, externalFileId, body);
            var status = response.StatusCode;
            response.Dispose();
            request.Dispose();
            throw DownloadFailure(status, externalFileId);
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

    /// <summary>
    /// Maps a failed Drive read onto one of the <see cref="StorageErrorCodes"/>, so an operator reading
    /// the response can tell a vanished file from a permission problem from a broken connection. Both
    /// download paths share it — they used to answer differently for the same HTTP status, and the
    /// generic one answered <c>EMAIL_ATTACHMENT_FILE_NOT_FOUND</c> for reads that had nothing to do with
    /// email (partner logos, avatars, news images), which sent every such investigation to the wrong place.
    ///
    /// <para>
    /// The 404 wording deliberately keeps both possibilities open. Drive returns 404 for a file the
    /// credential is not allowed to see as well as for one that is gone, so naming only deletion would
    /// state as fact something this code cannot know.
    /// </para>
    /// </summary>
    /// <summary>
    /// Maps a rejected Drive WRITE onto one of the <see cref="GoogleDriveErrorCodes"/>. Shared by the
    /// avatar and generic upload paths, which previously answered the same status differently and both
    /// answered <c>UPLOAD_FAILED</c> for a 401 and a 503 — one of which means the connection is broken
    /// and one of which means try again in a minute.
    /// </summary>
    private static BusinessRuleException ClassifyUploadFailure(
        System.Net.HttpStatusCode status, string body, string what)
    {
        if (status == System.Net.HttpStatusCode.NotFound
            || body.Contains("notFound", StringComparison.OrdinalIgnoreCase))
            return new BusinessRuleException(
                "Thư mục lưu trữ trên Google Drive không tồn tại hoặc tài khoản không có quyền truy cập.",
                GoogleDriveErrorCodes.FolderNotFoundOrNoPermission);

        if (status == System.Net.HttpStatusCode.Unauthorized)
            return new BusinessRuleException(
                "Kết nối Google Drive đã hết hạn. Vui lòng kết nối lại Google Drive.",
                GoogleDriveErrorCodes.TokenExpired);

        if (status == System.Net.HttpStatusCode.Forbidden)
            return new BusinessRuleException(
                "Google Drive từ chối ghi vào thư mục lưu trữ (thiếu quyền hoặc đã vượt hạn mức).",
                GoogleDriveErrorCodes.FolderNotFoundOrNoPermission);

        if ((int)status >= 500 || status == System.Net.HttpStatusCode.TooManyRequests)
            return new BusinessRuleException(
                "Google Drive đang tạm thời không khả dụng. Vui lòng thử lại sau ít phút.",
                GoogleDriveErrorCodes.Unavailable);

        return new BusinessRuleException(
            $"Không thể tải {what} lên Google Drive.", GoogleDriveErrorCodes.UploadFailed);
    }

    private static BusinessRuleException DownloadFailure(
        System.Net.HttpStatusCode status, string externalFileId) => status switch
        {
            System.Net.HttpStatusCode.NotFound => new BusinessRuleException(
                "Không đọc được tệp trên Google Drive: tệp không tồn tại, đã bị xoá, hoặc tài khoản " +
                "dịch vụ không được chia sẻ tệp này.",
                StorageErrorCodes.FileNotFound),

            System.Net.HttpStatusCode.Forbidden => new BusinessRuleException(
                "Google Drive từ chối quyền đọc tệp này. Kiểm tra quyền chia sẻ của tài khoản dịch vụ " +
                "hoặc hạn mức truy cập.",
                StorageErrorCodes.FileForbidden),

            System.Net.HttpStatusCode.Unauthorized => new BusinessRuleException(
                "Kết nối Google Drive không còn hợp lệ (token bị từ chối).",
                StorageErrorCodes.AuthFailed),

            _ => new BusinessRuleException(
                "Không thể tải tệp từ Google Drive.", StorageErrorCodes.Unavailable),
        };

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
    /// The API-management "Test kết nối" probe. Every step it can fail at maps to the code that names that
    /// step — a missing root folder id never reaches Google, an expired grant is reported as expired rather
    /// than as an outage, and a 404 on the folder says the folder, not the connection.
    /// </summary>
    public async Task<string> CheckConnectionAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.RootFolderId))
            throw new BusinessRuleException(
                "Google Drive chưa được cấu hình thư mục RootFolderId.",
                GoogleDriveErrorCodes.NotConnected);

        // Throws with the right code on its own: ConfigMissing, CredentialUnreadable, TokenExpired,
        // AuthFailed or Unavailable.
        var accessToken = await GetAccessTokenAsync(cancellationToken);
        var client = _httpClientFactory.CreateClient();

        var url = $"{FilesEndpoint}/{Uri.EscapeDataString(_options.RootFolderId!)}?fields=id,name";
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        HttpResponseMessage response;
        try
        {
            response = await client.SendAsync(request, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // The token was already minted, so the credential is fine and the network is not.
            _logger.LogError(ex, "Google Drive connection test could not reach the files endpoint.");
            throw new BusinessRuleException(
                "Google Drive đang tạm thời không khả dụng. Vui lòng thử lại sau ít phút.",
                GoogleDriveErrorCodes.Unavailable);
        }

        using (response)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Google Drive connection test returned {Status}: {Body}",
                    (int)response.StatusCode, body);
                throw ClassifyUploadFailure(response.StatusCode, body, "thư mục gốc");
            }

            try
            {
                using var doc = JsonDocument.Parse(body);
                var name = doc.RootElement.TryGetProperty("name", out var nameEl) ? nameEl.GetString() : null;
                return string.IsNullOrWhiteSpace(name) ? _options.RootFolderId! : name!;
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Google Drive connection test response was not valid JSON.");
                throw new BusinessRuleException(
                    "Google Drive đang tạm thời không khả dụng. Vui lòng thử lại sau ít phút.",
                    GoogleDriveErrorCodes.Unavailable);
            }
        }
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

    /// <summary>
    /// Exchanges the configured refresh token for a short-lived access token.
    ///
    /// <para>
    /// Every failure here is classified into one of the <see cref="GoogleDriveErrorCodes"/> because they
    /// ask four different people to do four different things: fix the configuration, reconnect the
    /// account, fix the OAuth client, or simply wait. They used to share one code and one sentence —
    /// "Không thể kết nối Google Drive. Vui lòng thử lại." — which is only true advice for the last of
    /// the four, and which is what the Host saw for all of them.
    /// </para>
    /// </summary>
    private async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.ClientId) || string.IsNullOrWhiteSpace(_options.ClientSecret))
            throw new BusinessRuleException(
                "Google Drive chưa được cấu hình đầy đủ (ClientId/ClientSecret).",
                GoogleDriveErrorCodes.ConfigMissing);

        // Resolved per call, from the database first. Nothing here remembers the answer: an ADMIN who
        // reconnects must not have to wait for a restart before the next upload uses the new token.
        var refreshToken = await _credentialResolver.ResolveRefreshTokenAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(refreshToken))
            throw new BusinessRuleException(
                "Google Drive chưa được kết nối. Vui lòng kết nối lại Google Drive trong màn Cấu hình API.",
                GoogleDriveErrorCodes.ConfigMissing);

        var client = _httpClientFactory.CreateClient();
        using var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"] = _options.ClientId!,
            ["client_secret"] = _options.ClientSecret!,
            ["refresh_token"] = refreshToken!,
            ["grant_type"] = "refresh_token",
        });

        HttpResponseMessage response;
        try
        {
            response = await client.PostAsync(TokenEndpoint, form, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Nothing was answered, so nothing is known about the credentials. Calling this an auth
            // failure — as it did — sent every network incident to whoever owns the OAuth client.
            _logger.LogError(ex, "Google Drive token request could not reach the token endpoint.");
            throw new BusinessRuleException(
                "Google Drive đang tạm thời không khả dụng. Vui lòng thử lại sau ít phút.",
                GoogleDriveErrorCodes.Unavailable);
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            // invalid_grant ⇒ the refresh token was revoked or expired (Drive "Testing" apps: ~7 days).
            if (body.Contains("invalid_grant", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning(
                    "Google Drive refresh token expired/revoked (token endpoint {Status}, error={Error}).",
                    (int)response.StatusCode, DescribeTokenError(body));
                throw new BusinessRuleException(
                    "Kết nối Google Drive đã hết hạn. Vui lòng kết nối lại Google Drive.",
                    GoogleDriveErrorCodes.TokenExpired);
            }

            // 5xx and 429 are Google having a bad moment; 4xx is our credentials being wrong. The first
            // is worth retrying and the second never is, so they must not arrive as the same code.
            var transient = (int)response.StatusCode >= 500
                            || response.StatusCode == System.Net.HttpStatusCode.TooManyRequests;

            _logger.LogError(
                "Google Drive token endpoint returned {Status} (error={Error}).",
                (int)response.StatusCode, DescribeTokenError(body));

            throw transient
                ? new BusinessRuleException(
                    "Google Drive đang tạm thời không khả dụng. Vui lòng thử lại sau ít phút.",
                    GoogleDriveErrorCodes.Unavailable)
                : new BusinessRuleException(
                    "Không thể xác thực với Google Drive. Vui lòng kiểm tra cấu hình kết nối Google Drive.",
                    GoogleDriveErrorCodes.AuthFailed);
        }

        string? accessToken = null;
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("access_token", out var tokenEl))
                accessToken = tokenEl.GetString();
        }
        catch (JsonException ex)
        {
            // A 200 that is not JSON is a captive portal or a proxy, not a credential problem.
            _logger.LogError(ex, "Google Drive token response was not valid JSON.");
            throw new BusinessRuleException(
                "Google Drive đang tạm thời không khả dụng. Vui lòng thử lại sau ít phút.",
                GoogleDriveErrorCodes.Unavailable);
        }

        if (string.IsNullOrEmpty(accessToken))
        {
            // Deliberately does not log the body: on this path it parsed as JSON, and a token response
            // is the one payload from Google that carries a credential. Answered as UNAVAILABLE rather
            // than as an auth failure because Google accepted the credentials — it is the response that
            // is malformed. (This throw used to carry the code "UPLOAD_AVATAR_FAILED", inherited from
            // the avatar feature this client was first written for, on a path every Drive upload shares.)
            _logger.LogError("Google Drive token response contained no access_token.");
            throw new BusinessRuleException(
                "Google Drive đang tạm thời không khả dụng. Vui lòng thử lại sau ít phút.",
                GoogleDriveErrorCodes.Unavailable);
        }

        return accessToken;
    }

    /// <summary>
    /// The <c>error</c> field of an OAuth error body ("invalid_grant", "invalid_client", …) — enough to
    /// diagnose, without copying the whole payload of a credential endpoint into the log.
    /// </summary>
    private static string DescribeTokenError(string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            return doc.RootElement.TryGetProperty("error", out var el) ? el.GetString() ?? "?" : "?";
        }
        catch (JsonException)
        {
            return "(unparseable)";
        }
    }
}
