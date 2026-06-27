# PEMS — Refactor nền tảng Google Drive Storage để sẵn sàng mở rộng Gallery / News / Document / Minutes và Deploy Production

> File này dùng cho AI Agent đọc và code/refactor nhẹ phần Google Drive upload hiện tại.  
> Mục tiêu: sau này khi code các chức năng **gallery / news / document / minutes / visit request attachment**, mỗi chức năng chỉ cần thêm phần nghiệp vụ riêng; khi deploy production chỉ cần đổi **config production + Google Cloud Redirect URI**, không phải sửa lại hàng loạt handler upload.

---

## 1. Bối cảnh hiện tại

Dự án PEMS hiện đã tích hợp Google Drive cho chức năng upload avatar profile.

Luồng avatar hiện tại:

```text
User đăng nhập
→ Chọn ảnh avatar trong Profile
→ Frontend gửi multipart/form-data lên Backend
→ Backend validate file
→ Backend upload ảnh lên Google Drive
→ Backend insert metadata vào bảng files
→ Backend update users.avatar_url = /api/files/{fileId}/content
→ Frontend hiển thị avatar mới
```

Các phần đã có hoặc đã định hướng:

```text
- Backend ASP.NET Core .NET 8 Clean Architecture + MediatR.
- Frontend React + Vite + TypeScript.
- Database MySQL v10.
- Bảng files lưu metadata.
- File thật nằm trên Google Drive.
- users.avatar_url lưu URL proxy nội bộ dạng /api/files/{fileId}/content.
- Google Drive config hiện nằm trong appsettings.Development.json.
- OAuthUser + RefreshToken đã dùng được trong dev.
```

Vấn đề cần chuẩn bị:

```text
Hiện tại mới upload avatar. Sau này hệ thống còn cần upload:
- Gallery images/videos
- News images/attachments
- Documents
- Minutes attachments
- Visit request attachments
- Partner/contact documents
- Logistics/handover attachments nếu có

Nếu mỗi module tự viết Google Drive upload riêng thì sau này khi deploy hoặc đổi storage sẽ phải sửa rất nhiều nơi.
```

---

## 2. Mục tiêu refactor

Mục tiêu chính:

```text
1. Không phá luồng upload avatar hiện tại.
2. Tách phần upload/download/delete Google Drive thành service dùng chung.
3. Tách phần validate/checksum/insert files/cleanup thành service dùng chung.
4. Mỗi module sau này chỉ cần gọi service chung với FilePurpose tương ứng.
5. Không hard-code folderId/token/localhost trong handler nghiệp vụ.
6. Production chỉ cần đổi environment variables + Google Cloud Redirect URI.
7. Frontend vẫn chỉ gọi backend PEMS, không gọi Google Drive trực tiếp.
```

Kết quả mong muốn:

```text
Avatar hiện tại vẫn chạy.
Thêm Gallery upload chỉ cần gọi FileUploadService với FilePurpose.GalleryImage.
Thêm News upload chỉ cần gọi FileUploadService với FilePurpose.NewsImage.
Thêm Document upload chỉ cần gọi FileUploadService với FilePurpose.Document.
Thêm Minutes upload chỉ cần gọi FileUploadService với FilePurpose.MinutesAttachment.
Khi deploy, không sửa từng handler upload, chỉ đổi config.
```

---

## 3. Nguyên tắc bắt buộc

Không được:

```text
- Không rewrite lại avatar upload đang chạy.
- Không xóa API avatar hiện tại.
- Không lưu binary/base64 vào MySQL.
- Không lưu direct Google Drive URL vào users.avatar_url.
- Không để frontend gọi Google Drive API trực tiếp.
- Không hard-code localhost trong DB hoặc handler.
- Không hard-code AvatarFolderId trong từng handler nếu đã có resolver.
- Không commit ClientSecret / RefreshToken.
- Không tự ghi RefreshToken vào appsettings.json bằng runtime code.
- Không thêm thư viện mới nếu không cần.
- Không đổi role/permission/RBAC.
- Không dùng mock data.
```

Nên làm:

```text
- DB chỉ lưu metadata file.
- File thật lưu ở Google Drive.
- URL xem/download đi qua backend proxy.
- Handler nghiệp vụ chỉ gọi service upload chung.
- Folder Google Drive được chọn theo FilePurpose.
- Config đọc qua Options / Environment Variables.
- Có cleanup nếu upload Drive thành công nhưng DB save lỗi.
- Có checksum_sha256 cho file upload.
- Có error code rõ cho Google Drive/token/file validation.
```

---

## 4. Kiến trúc đích

### 4.1 Tổng quan flow dùng chung

Flow upload file dùng chung:

```text
Business Handler
→ gọi IFileUploadService.UploadBusinessFileAsync(...)
→ FileUploadService validate file theo purpose
→ FileUploadService tính checksum_sha256
→ FileUploadService build object_key
→ FileUploadService resolve folderId theo FilePurpose
→ FileUploadService gọi IFileStorageService.UploadAsync(...)
→ GoogleDriveStorageService upload file lên Google Drive
→ FileUploadService insert row vào bảng files
→ FileUploadService trả fileId + proxyUrl + metadata
→ Business Handler lưu fileId/proxyUrl vào bảng nghiệp vụ tương ứng
```

Ví dụ theo module:

```text
Avatar:
UploadProfileAvatarCommandHandler
→ FileUploadService.UploadBusinessFileAsync(..., FilePurpose.UserAvatar)
→ update users.avatar_url = /api/files/{fileId}/content

Gallery:
AddGalleryImageCommandHandler
→ FileUploadService.UploadBusinessFileAsync(..., FilePurpose.GalleryImage)
→ insert gallery_images.file_id = fileId

News:
UploadNewsImageCommandHandler
→ FileUploadService.UploadBusinessFileAsync(..., FilePurpose.NewsImage)
→ insert news_section_files.file_id = fileId hoặc update thumbnail_file_id

Document:
CreateDocumentCommandHandler
→ FileUploadService.UploadBusinessFileAsync(..., FilePurpose.Document)
→ insert documents.file_id = fileId hoặc document_files mapping

Minutes:
UploadMinutesAttachmentCommandHandler
→ FileUploadService.UploadBusinessFileAsync(..., FilePurpose.MinutesAttachment)
→ update minutes attachment/link field hoặc insert mapping table
```

---

## 5. Backend tasks cần làm

### 5.1 Tạo hoặc chuẩn hóa `GoogleDriveOptions`

Vị trí gợi ý:

```text
backend/PEMS.Infrastructure/FileStorage/GoogleDrive/GoogleDriveOptions.cs
```

Options cần đủ field:

```csharp
public sealed class GoogleDriveOptions
{
    public bool Enabled { get; set; }
    public string AuthMode { get; set; } = "OAuthUser";

    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string RedirectUri { get; set; } = string.Empty;
    public string? RefreshToken { get; set; }

    public string RootFolderId { get; set; } = string.Empty;
    public string AvatarFolderId { get; set; } = string.Empty;
    public string DocumentFolderId { get; set; } = string.Empty;
    public string GalleryFolderId { get; set; } = string.Empty;
    public string NewsFolderId { get; set; } = string.Empty;
    public string MinutesFolderId { get; set; } = string.Empty;
    public string VisitRequestFolderId { get; set; } = string.Empty;

    // Optional nếu sau này cần:
    public string PartnerFolderId { get; set; } = string.Empty;
    public string LogisticsFolderId { get; set; } = string.Empty;
    public string TempFolderId { get; set; } = string.Empty;
}
```

Đăng ký trong DI:

```csharp
services.Configure<GoogleDriveOptions>(
    configuration.GetSection("GoogleDrive"));
```

Yêu cầu:

```text
- Không đọc trực tiếp appsettings.Development.json trong handler.
- Không truyền ClientSecret/RefreshToken về frontend.
- Production dùng Environment Variables override các key này.
```

---

### 5.2 Tạo `FilePurpose` constants/enum dùng chung

Tạo enum hoặc constants dùng thống nhất ở backend.

Vị trí gợi ý:

```text
backend/PEMS.Application/Common/Files/FilePurpose.cs
```

Ví dụ:

```csharp
public enum FilePurpose
{
    UserAvatar,
    GalleryImage,
    GalleryVideo,
    NewsImage,
    NewsAttachment,
    Document,
    MinutesAttachment,
    VisitRequestAttachment,
    PartnerDocument,
    LogisticsAttachment,
    Other
}
```

Nếu DB `files.file_purpose` là enum/string có giá trị cụ thể, phải map đúng schema hiện tại.

Mapping gợi ý:

```csharp
public static class FilePurposeDbValues
{
    public const string UserAvatar = "USER_AVATAR";
    public const string GalleryImage = "GALLERY_IMAGE";
    public const string GalleryVideo = "GALLERY_VIDEO";
    public const string NewsImage = "NEWS_IMAGE";
    public const string NewsAttachment = "NEWS_ATTACHMENT";
    public const string Document = "DOCUMENT";
    public const string MinutesAttachment = "MINUTES_ATTACHMENT";
    public const string VisitRequestAttachment = "VISIT_REQUEST_ATTACHMENT";
    public const string PartnerDocument = "PARTNER_DOCUMENT";
    public const string LogisticsAttachment = "LOGISTICS_ATTACHMENT";
    public const string Other = "OTHER";
}
```

Yêu cầu:

```text
- Không tự bịa giá trị file_purpose nếu DB enum chưa có.
- Nếu DB chưa có giá trị cần dùng, phải tạo SQL patch rõ ràng hoặc dùng giá trị hợp lệ đã có.
- Không để mỗi module tự viết chuỗi file_purpose rời rạc.
```

---

### 5.3 Tạo `IFileStorageFolderResolver`

Mục tiêu: chọn Google Drive folder theo `FilePurpose`, không hard-code folder trong từng handler.

Vị trí gợi ý:

```text
backend/PEMS.Application/Common/Interfaces/IFileStorageFolderResolver.cs
backend/PEMS.Infrastructure/FileStorage/GoogleDrive/GoogleDriveFolderResolver.cs
```

Interface:

```csharp
public interface IFileStorageFolderResolver
{
    string ResolveFolderId(FilePurpose purpose);
}
```

Implementation:

```csharp
public sealed class GoogleDriveFolderResolver : IFileStorageFolderResolver
{
    private readonly GoogleDriveOptions _options;

    public GoogleDriveFolderResolver(IOptions<GoogleDriveOptions> options)
    {
        _options = options.Value;
    }

    public string ResolveFolderId(FilePurpose purpose)
    {
        var folderId = purpose switch
        {
            FilePurpose.UserAvatar => _options.AvatarFolderId,
            FilePurpose.GalleryImage => _options.GalleryFolderId,
            FilePurpose.GalleryVideo => _options.GalleryFolderId,
            FilePurpose.NewsImage => _options.NewsFolderId,
            FilePurpose.NewsAttachment => _options.NewsFolderId,
            FilePurpose.Document => _options.DocumentFolderId,
            FilePurpose.MinutesAttachment => _options.MinutesFolderId,
            FilePurpose.VisitRequestAttachment => _options.VisitRequestFolderId,
            FilePurpose.PartnerDocument => string.IsNullOrWhiteSpace(_options.PartnerFolderId)
                ? _options.DocumentFolderId
                : _options.PartnerFolderId,
            FilePurpose.LogisticsAttachment => string.IsNullOrWhiteSpace(_options.LogisticsFolderId)
                ? _options.DocumentFolderId
                : _options.LogisticsFolderId,
            _ => _options.DocumentFolderId
        };

        if (string.IsNullOrWhiteSpace(folderId))
        {
            throw new BusinessRuleException(
                "GOOGLE_DRIVE_FOLDER_NOT_CONFIGURED",
                $"Google Drive folder is not configured for purpose {purpose}.");
        }

        return folderId;
    }
}
```

Yêu cầu:

```text
- Handler không gọi trực tiếp _options.AvatarFolderId nếu có resolver.
- Module mới chỉ truyền FilePurpose.
- Nếu thiếu folder config, trả lỗi rõ.
```

---

### 5.4 Tạo `IFileStorageService`

Đây là service storage thấp nhất, chỉ lo upload/download/delete vật lý lên Google Drive.

Vị trí gợi ý:

```text
backend/PEMS.Application/Common/Interfaces/IFileStorageService.cs
backend/PEMS.Infrastructure/FileStorage/GoogleDrive/GoogleDriveStorageService.cs
```

Interface:

```csharp
public interface IFileStorageService
{
    Task<FileStorageUploadResult> UploadAsync(
        Stream stream,
        string objectKey,
        string contentType,
        string folderId,
        CancellationToken cancellationToken);

    Task<Stream> DownloadAsync(
        string externalFileId,
        CancellationToken cancellationToken);

    Task DeleteAsync(
        string externalFileId,
        CancellationToken cancellationToken);
}
```

Upload result:

```csharp
public sealed class FileStorageUploadResult
{
    public string StorageProvider { get; init; } = "GOOGLE_DRIVE";
    public string ExternalFileId { get; init; } = default!;
    public string? WebViewUrl { get; init; }
    public string? DownloadUrl { get; init; }
    public string? ThumbnailUrl { get; init; }
    public string ObjectKey { get; init; } = default!;
    public string MimeType { get; init; } = default!;
    public long FileSize { get; init; }
}
```

Yêu cầu:

```text
- GoogleDriveStorageService tự lấy access token từ RefreshToken.
- Không log ClientSecret / RefreshToken / AccessToken.
- Nếu RefreshToken rỗng → GOOGLE_DRIVE_NOT_CONNECTED.
- Nếu invalid_grant/token expired → GOOGLE_DRIVE_TOKEN_EXPIRED.
- Nếu upload lỗi → GOOGLE_DRIVE_UPLOAD_FAILED.
- Nếu download lỗi do file mất → FILE_NOT_FOUND_IN_STORAGE.
```

---

### 5.5 Tạo `IFileUploadService` dùng chung

Đây là service application-level: validate, checksum, upload, insert bảng `files`.

Vị trí gợi ý:

```text
backend/PEMS.Application/Common/Interfaces/IFileUploadService.cs
backend/PEMS.Application/Common/Files/FileUploadService.cs
```

Interface:

```csharp
public interface IFileUploadService
{
    Task<UploadedFileDto> UploadBusinessFileAsync(
        Stream stream,
        string originalFileName,
        string contentType,
        long fileSize,
        FilePurpose purpose,
        long uploadedBy,
        CancellationToken cancellationToken);
}
```

DTO trả về:

```csharp
public sealed class UploadedFileDto
{
    public long FileId { get; init; }
    public string FileUrl { get; init; } = default!; // /api/files/{fileId}/content
    public string StorageProvider { get; init; } = "GOOGLE_DRIVE";
    public string ExternalFileId { get; init; } = default!;
    public string? WebViewUrl { get; init; }
    public string? DownloadUrl { get; init; }
    public string? ThumbnailUrl { get; init; }
    public string MimeType { get; init; } = default!;
    public long FileSize { get; init; }
    public string ChecksumSha256 { get; init; } = default!;
    public string ObjectKey { get; init; } = default!;
}
```

Nhiệm vụ của `FileUploadService`:

```text
1. Validate stream/fileName/contentType/fileSize theo purpose.
2. Copy stream vào MemoryStream nếu cần.
3. Tính checksum_sha256.
4. Sanitize original filename.
5. Build object_key.
6. Resolve folderId theo FilePurpose.
7. Upload lên Google Drive qua IFileStorageService.
8. Insert row vào bảng files.
9. Save DB.
10. Nếu DB save lỗi sau upload Drive, cố gắng delete file Drive vừa upload.
11. Return UploadedFileDto.
```

Pseudo flow:

```csharp
public async Task<UploadedFileDto> UploadBusinessFileAsync(
    Stream stream,
    string originalFileName,
    string contentType,
    long fileSize,
    FilePurpose purpose,
    long uploadedBy,
    CancellationToken cancellationToken)
{
    var rule = _fileValidationPolicy.GetRule(purpose);
    _fileValidator.Validate(originalFileName, contentType, fileSize, rule);

    await using var memoryStream = new MemoryStream();
    await stream.CopyToAsync(memoryStream, cancellationToken);

    if (memoryStream.Length <= 0)
    {
        throw new ValidationException("File is empty.");
    }

    memoryStream.Position = 0;
    var checksum = await _checksumService.ComputeSha256HexAsync(memoryStream, cancellationToken);

    memoryStream.Position = 0;
    var objectKey = _objectKeyBuilder.Build(purpose, uploadedBy, originalFileName);
    var folderId = _folderResolver.ResolveFolderId(purpose);

    string? uploadedExternalFileId = null;

    try
    {
        var uploadResult = await _storage.UploadAsync(
            memoryStream,
            objectKey,
            contentType,
            folderId,
            cancellationToken);

        uploadedExternalFileId = uploadResult.ExternalFileId;

        var file = new FileEntity
        {
            StorageProvider = "GOOGLE_DRIVE",
            ObjectKey = objectKey,
            OriginalFilename = sanitizedFileName,
            MimeType = contentType,
            FileSize = memoryStream.Length,
            ChecksumSha256 = checksum,
            UploadedBy = uploadedBy,
            ExternalFileId = uploadResult.ExternalFileId,
            WebViewUrl = uploadResult.WebViewUrl,
            DownloadUrl = uploadResult.DownloadUrl,
            ThumbnailUrl = uploadResult.ThumbnailUrl,
            FilePurpose = purpose.ToDbValue()
        };

        _db.Files.Add(file);
        await _db.SaveChangesAsync(cancellationToken);

        return new UploadedFileDto
        {
            FileId = file.FileId,
            FileUrl = $"/api/files/{file.FileId}/content",
            StorageProvider = "GOOGLE_DRIVE",
            ExternalFileId = uploadResult.ExternalFileId,
            WebViewUrl = uploadResult.WebViewUrl,
            DownloadUrl = uploadResult.DownloadUrl,
            ThumbnailUrl = uploadResult.ThumbnailUrl,
            MimeType = contentType,
            FileSize = memoryStream.Length,
            ChecksumSha256 = checksum,
            ObjectKey = objectKey
        };
    }
    catch
    {
        if (!string.IsNullOrWhiteSpace(uploadedExternalFileId))
        {
            try
            {
                await _storage.DeleteAsync(uploadedExternalFileId, cancellationToken);
            }
            catch
            {
                // log cleanup failure, do not hide original exception
            }
        }

        throw;
    }
}
```

Tên entity/property phải khớp code thật trong project.

---

### 5.6 Tạo `FileValidationPolicy`

Không dùng rule avatar cho tất cả file.

Vị trí gợi ý:

```text
backend/PEMS.Application/Common/Files/FileValidationPolicy.cs
```

Rule object:

```csharp
public sealed class FileValidationRule
{
    public long MaxSizeBytes { get; init; }
    public IReadOnlySet<string> AllowedMimeTypes { get; init; } = new HashSet<string>();
    public IReadOnlySet<string> AllowedExtensions { get; init; } = new HashSet<string>();
    public bool RequireImageMagicBytes { get; init; }
}
```

Policy:

```csharp
public interface IFileValidationPolicy
{
    FileValidationRule GetRule(FilePurpose purpose);
}
```

Rules gợi ý:

```text
USER_AVATAR:
- JPG / JPEG / PNG / WEBP
- max 2MB
- check magic bytes
- block SVG

GALLERY_IMAGE:
- JPG / JPEG / PNG / WEBP
- max 5MB hoặc 10MB
- check magic bytes
- block SVG

GALLERY_VIDEO:
- MP4 / WEBM
- max theo nghiệp vụ

NEWS_IMAGE:
- JPG / JPEG / PNG / WEBP
- max 5MB
- check magic bytes

NEWS_ATTACHMENT:
- PDF / DOCX / XLSX / PPTX / JPG / PNG
- max 10MB hoặc 20MB

DOCUMENT:
- PDF / DOCX / XLSX / PPTX / JPG / PNG
- max 10MB hoặc 20MB

MINUTES_ATTACHMENT:
- PDF / DOCX
- max 10MB

VISIT_REQUEST_ATTACHMENT:
- PDF / DOCX / JPG / PNG
- max 10MB
```

Yêu cầu:

```text
- Frontend validate để UX tốt.
- Backend validate lại đầy đủ.
- Không nhận SVG cho ảnh.
- Không tin Content-Type từ frontend nếu có magic bytes check.
```

---

### 5.7 Tạo `FileChecksumService`

Vị trí gợi ý:

```text
backend/PEMS.Application/Common/Files/FileChecksumService.cs
```

Interface:

```csharp
public interface IFileChecksumService
{
    Task<string> ComputeSha256HexAsync(
        Stream stream,
        CancellationToken cancellationToken = default);
}
```

Implementation:

```csharp
public sealed class FileChecksumService : IFileChecksumService
{
    public async Task<string> ComputeSha256HexAsync(
        Stream stream,
        CancellationToken cancellationToken = default)
    {
        if (stream.CanSeek)
        {
            stream.Position = 0;
        }

        using var sha256 = SHA256.Create();
        var hashBytes = await sha256.ComputeHashAsync(stream, cancellationToken);

        if (stream.CanSeek)
        {
            stream.Position = 0;
        }

        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }
}
```

Yêu cầu:

```text
- checksum_sha256 là lowercase hex.
- Độ dài 64 ký tự.
- Không tính checksum từ filename/external_file_id/webViewUrl.
- Không nhận checksum từ frontend.
```

---

### 5.8 Tạo `FileObjectKeyBuilder`

Mục tiêu: object_key thống nhất, dễ trace.

Vị trí gợi ý:

```text
backend/PEMS.Application/Common/Files/FileObjectKeyBuilder.cs
```

Format gợi ý:

```text
{purpose-folder}/{yyyy}/{MM}/{dd}/{uploadedBy}_{yyyyMMddHHmmss}_{guid}.{ext}
```

Ví dụ:

```text
avatars/2026/06/27/15_20260627153022_0b2a6c9f.jpg
gallery/2026/06/27/15_20260627153210_6d9e2a10.webp
news/2026/06/27/15_20260627153344_aa923dd1.png
documents/2026/06/27/15_20260627153501_f1d2c3a4.pdf
minutes/2026/06/27/15_20260627153612_59cb023e.docx
```

Yêu cầu:

```text
- Không dùng filename gốc làm object_key trực tiếp.
- Sanitize extension.
- Không để path traversal kiểu ../.
- object_key lưu vào files.object_key.
```

---

### 5.9 Chuẩn hóa `GET /api/files/{fileId}/content`

Endpoint này là nền tảng cho avatar/gallery/news/document/minutes.

Yêu cầu:

```text
1. Load file metadata từ bảng files.
2. Nếu không tồn tại → 404.
3. Check quyền đọc file theo file_purpose/module nếu cần.
4. Nếu storage_provider = GOOGLE_DRIVE:
   - dùng external_file_id download stream từ Google Drive.
5. Return File(stream, mime_type).
6. Thêm cache header phù hợp.
7. Nếu Google Drive báo file mất → FILE_NOT_FOUND_IN_STORAGE.
```

Gợi ý route:

```http
GET /api/files/{fileId}/content
```

Response đúng:

```text
200 OK
Content-Type: image/jpeg | image/png | application/pdf | ...
Body = file stream
```

Cache gợi ý:

```text
Avatar/private file:
Cache-Control: private, max-age=3600

Public gallery/news image:
Cache-Control: public, max-age=86400 nếu file public và không nhạy cảm
```

Lưu ý:

```text
- Với avatar user nội bộ, có thể yêu cầu authentication.
- Với public news/gallery image, có thể cần route public riêng hoặc allow public theo file_purpose + visibility nghiệp vụ.
- Không expose raw Google Drive token.
```

---

### 5.10 Chuẩn hóa error codes

Dùng chung cho mọi module upload:

```text
FILE_REQUIRED
FILE_EMPTY
FILE_TOO_LARGE
FILE_INVALID_TYPE
FILE_INVALID_EXTENSION
FILE_MAGIC_BYTES_MISMATCH
FILE_CHECKSUM_FAILED
FILE_NOT_FOUND
FILE_NOT_FOUND_IN_STORAGE

GOOGLE_DRIVE_NOT_CONNECTED
GOOGLE_DRIVE_FOLDER_NOT_CONFIGURED
GOOGLE_DRIVE_TOKEN_EXPIRED
GOOGLE_DRIVE_UPLOAD_FAILED
GOOGLE_DRIVE_DOWNLOAD_FAILED
GOOGLE_DRIVE_DELETE_FAILED

FILE_UPLOAD_FAILED
FILE_DOWNLOAD_FAILED
```

Yêu cầu:

```text
- Backend trả message rõ.
- Frontend hiển thị lỗi dễ hiểu.
- Không trả raw exception/token.
```

---

## 6. Frontend tasks cần làm

### 6.1 Tạo helper resolve file URL

Vị trí gợi ý:

```text
frontend/pems-react/src/shared/utils/resolveFileUrl.ts
```

Code gợi ý:

```ts
export function resolveFileUrl(url?: string | null): string | null {
  if (!url) return null;

  if (url.startsWith("http://") || url.startsWith("https://")) {
    return url;
  }

  const baseUrl = import.meta.env.VITE_API_BASE_URL ?? "";
  return `${baseUrl}${url}`;
}
```

Yêu cầu:

```text
- Avatar/header/sidebar/gallery/news dùng chung helper này.
- Không tự nối localhost trong từng component.
- Production chỉ cần đổi VITE_API_BASE_URL.
```

---

### 6.2 Tạo upload API helper dùng chung

Vị trí gợi ý:

```text
frontend/pems-react/src/shared/api/fileUploadApi.ts
```

Gợi ý:

```ts
export interface UploadedFileResponse {
  fileId: number;
  fileUrl: string;
  webViewUrl?: string;
  thumbnailUrl?: string;
  mimeType: string;
  fileSize: number;
  checksumSha256?: string;
}

export async function uploadFileToEndpoint(
  endpoint: string,
  fieldName: string,
  file: File,
): Promise<UploadedFileResponse> {
  const formData = new FormData();
  formData.append(fieldName, file);

  const response = await httpClient.put(endpoint, formData, {
    headers: {
      "Content-Type": "multipart/form-data",
    },
  });

  return response.data.data;
}
```

Lưu ý:

```text
- Avatar có thể vẫn dùng uploadProfileAvatar() wrapper riêng.
- Gallery/news/document/minutes tạo wrapper riêng nhưng dùng chung logic FormData.
- Không để mỗi component tự viết axios multipart lặp lại.
```

---

### 6.3 Tạo frontend file validation utility

Vị trí gợi ý:

```text
frontend/pems-react/src/shared/utils/fileValidation.ts
```

Gợi ý:

```ts
export type FilePurpose =
  | "USER_AVATAR"
  | "GALLERY_IMAGE"
  | "NEWS_IMAGE"
  | "DOCUMENT"
  | "MINUTES_ATTACHMENT"
  | "VISIT_REQUEST_ATTACHMENT";

export interface FileValidationRule {
  maxSizeBytes: number;
  allowedMimeTypes: string[];
  allowedExtensions: string[];
}

export function getFileValidationRule(purpose: FilePurpose): FileValidationRule {
  switch (purpose) {
    case "USER_AVATAR":
      return {
        maxSizeBytes: 2 * 1024 * 1024,
        allowedMimeTypes: ["image/jpeg", "image/png", "image/webp"],
        allowedExtensions: [".jpg", ".jpeg", ".png", ".webp"],
      };
    case "GALLERY_IMAGE":
    case "NEWS_IMAGE":
      return {
        maxSizeBytes: 5 * 1024 * 1024,
        allowedMimeTypes: ["image/jpeg", "image/png", "image/webp"],
        allowedExtensions: [".jpg", ".jpeg", ".png", ".webp"],
      };
    case "DOCUMENT":
    case "MINUTES_ATTACHMENT":
    case "VISIT_REQUEST_ATTACHMENT":
      return {
        maxSizeBytes: 10 * 1024 * 1024,
        allowedMimeTypes: [
          "application/pdf",
          "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
          "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
          "application/vnd.openxmlformats-officedocument.presentationml.presentation",
          "image/jpeg",
          "image/png",
        ],
        allowedExtensions: [".pdf", ".docx", ".xlsx", ".pptx", ".jpg", ".jpeg", ".png"],
      };
    default:
      return {
        maxSizeBytes: 10 * 1024 * 1024,
        allowedMimeTypes: [],
        allowedExtensions: [],
      };
  }
}
```

Yêu cầu:

```text
- Frontend validation chỉ để UX.
- Backend vẫn là kiểm tra cuối cùng.
- Không cho SVG ở nhóm ảnh.
```

---

## 7. Config tasks

### 7.1 `appsettings.json`

Chỉ để placeholder hoặc config không nhạy cảm.

Ví dụ:

```json
{
  "Storage": {
    "Provider": "GoogleDrive"
  },
  "GoogleDrive": {
    "Enabled": true,
    "AuthMode": "OAuthUser",
    "RedirectUri": "",
    "RootFolderId": "",
    "AvatarFolderId": "",
    "DocumentFolderId": "",
    "GalleryFolderId": "",
    "NewsFolderId": "",
    "MinutesFolderId": "",
    "VisitRequestFolderId": ""
  }
}
```

Không để:

```text
ClientSecret
RefreshToken
AccessToken
ServiceAccount JSON
```

---

### 7.2 `appsettings.Development.json`

Dùng cho dev local, không commit.

```json
{
  "GoogleDrive": {
    "ClientId": "DEV_CLIENT_ID",
    "ClientSecret": "DEV_CLIENT_SECRET",
    "RedirectUri": "http://localhost:5265/api/google-drive/oauth/callback",
    "RootFolderId": "DEV_ROOT_FOLDER_ID",
    "AvatarFolderId": "DEV_AVATAR_FOLDER_ID",
    "DocumentFolderId": "DEV_DOCUMENT_FOLDER_ID",
    "GalleryFolderId": "DEV_GALLERY_FOLDER_ID",
    "NewsFolderId": "DEV_NEWS_FOLDER_ID",
    "MinutesFolderId": "DEV_MINUTES_FOLDER_ID",
    "VisitRequestFolderId": "DEV_VISIT_REQUEST_FOLDER_ID",
    "RefreshToken": "DEV_REFRESH_TOKEN"
  }
}
```

---

### 7.3 `appsettings.Development.example.json`

Commit file này cho team.

```json
{
  "GoogleDrive": {
    "ClientId": "YOUR_GOOGLE_OAUTH_CLIENT_ID",
    "ClientSecret": "YOUR_GOOGLE_OAUTH_CLIENT_SECRET",
    "RedirectUri": "http://localhost:5265/api/google-drive/oauth/callback",
    "RootFolderId": "YOUR_ROOT_FOLDER_ID",
    "AvatarFolderId": "YOUR_AVATAR_FOLDER_ID",
    "DocumentFolderId": "YOUR_DOCUMENT_FOLDER_ID",
    "GalleryFolderId": "YOUR_GALLERY_FOLDER_ID",
    "NewsFolderId": "YOUR_NEWS_FOLDER_ID",
    "MinutesFolderId": "YOUR_MINUTES_FOLDER_ID",
    "VisitRequestFolderId": "YOUR_VISIT_REQUEST_FOLDER_ID",
    "RefreshToken": "YOUR_REFRESH_TOKEN"
  }
}
```

---

### 7.4 Production Environment Variables

Production không dùng `appsettings.Development.json`.

Dùng env:

```text
ASPNETCORE_ENVIRONMENT=Production

Storage__Provider=GoogleDrive

GoogleDrive__Enabled=true
GoogleDrive__AuthMode=OAuthUser
GoogleDrive__ClientId=<prod-client-id>
GoogleDrive__ClientSecret=<prod-client-secret>
GoogleDrive__RedirectUri=https://api.your-domain.com/api/google-drive/oauth/callback
GoogleDrive__RootFolderId=<prod-root-folder-id>
GoogleDrive__AvatarFolderId=<prod-avatar-folder-id>
GoogleDrive__DocumentFolderId=<prod-document-folder-id>
GoogleDrive__GalleryFolderId=<prod-gallery-folder-id>
GoogleDrive__NewsFolderId=<prod-news-folder-id>
GoogleDrive__MinutesFolderId=<prod-minutes-folder-id>
GoogleDrive__VisitRequestFolderId=<prod-visit-request-folder-id>
GoogleDrive__RefreshToken=<prod-refresh-token>
```

Frontend production:

```text
VITE_API_BASE_URL=https://api.your-domain.com
```

Backend CORS:

```text
AllowedOrigins=https://your-frontend-domain.com
```

---

## 8. Google Cloud tasks để ít sửa khi deploy

Hiện tại dev dùng:

```text
http://localhost:5265/api/google-drive/oauth/callback
```

Khi deploy production, thêm Authorized Redirect URI:

```text
https://api.your-domain.com/api/google-drive/oauth/callback
```

Nên chuẩn bị từ bây giờ:

```text
- Tên OAuth Client rõ: PEMS Backend Drive OAuth.
- Branding app rõ: PEMS Drive Storage.
- User support email là email quản trị hệ thống.
- Không dùng tài khoản Google cá nhân làm storage production.
- Tạo tài khoản storage riêng hoặc Shared Drive nếu có điều kiện.
```

Folder nên tách:

```text
PEMS_STORAGE_DEV
PEMS_STORAGE_PROD
```

Cấu trúc folder giống nhau để đổi config dễ:

```text
avatars
documents
gallery
news
minutes
visit-requests
partners
logistics
temp
```

---

## 9. OAuth connect/callback tasks

Hiện tại endpoint OAuth chỉ để lấy RefreshToken.

Yêu cầu giữ:

```text
- /api/google-drive/oauth/connect
- /api/google-drive/oauth/callback
```

Dev:

```text
- Cho chạy ở Development.
- Hiển thị RefreshToken để copy vào appsettings.Development.json.
```

Production:

```text
- Không mở public.
- Hoặc chỉ ADMIN kỹ thuật được gọi.
- Sau khi lấy token production xong, nên tắt hoặc khóa endpoint.
```

Không làm:

```text
- Không tự ghi token vào appsettings.json.
- Không expose ClientSecret.
- Không log RefreshToken.
```

---

## 10. Cách module mới nên dùng FileUploadService

### 10.1 Gallery

Gallery upload image:

```text
AddGalleryImageCommandHandler
→ check role/scope gallery
→ gọi FileUploadService.UploadBusinessFileAsync(..., FilePurpose.GalleryImage)
→ insert gallery_images với file_id = uploaded.FileId
→ return data cho frontend
```

Gallery upload video:

```text
FilePurpose.GalleryVideo
→ folder GalleryFolderId
→ validate MP4/WEBM theo rule riêng
```

---

### 10.2 News

News upload image:

```text
UploadNewsImageCommandHandler
→ check người có quyền edit/create news
→ gọi FileUploadService.UploadBusinessFileAsync(..., FilePurpose.NewsImage)
→ insert news_section_files hoặc update thumbnail_file_id
```

News attachment:

```text
FilePurpose.NewsAttachment
→ folder NewsFolderId
→ validate ảnh/pdf/docx nếu nghiệp vụ cho phép
```

---

### 10.3 Document

Document upload:

```text
CreateDocumentCommandHandler
→ check quyền Document Management
→ gọi FileUploadService.UploadBusinessFileAsync(..., FilePurpose.Document)
→ insert documents hoặc mapping document_files theo schema thật
```

Document view/download:

```text
GET /api/files/{fileId}/content
→ check quyền document scope
→ stream file từ Google Drive
```

---

### 10.4 Minutes

Minutes attachment:

```text
UploadMinutesAttachmentCommandHandler
→ check quyền edit/view minutes
→ gọi FileUploadService.UploadBusinessFileAsync(..., FilePurpose.MinutesAttachment)
→ link file_id vào minutes hoặc bảng attachment nếu có
```

---

### 10.5 Visit Request Attachment

Visit request attachment:

```text
SubmitVisitRequestCommandHandler hoặc UploadVisitRequestAttachmentCommandHandler
→ FilePurpose.VisitRequestAttachment
→ folder VisitRequestFolderId
→ link file_id vào request/guest/member/document table theo schema thật
```

---

## 11. Deploy sau này sẽ cần đổi gì

Nếu làm đúng refactor này, khi deploy production chỉ cần đổi:

```text
1. Google Cloud Authorized Redirect URI:
   http://localhost:5265/... → https://api.domain.com/...

2. Backend environment variables:
   GoogleDrive__ClientId
   GoogleDrive__ClientSecret
   GoogleDrive__RefreshToken
   GoogleDrive__RedirectUri
   GoogleDrive__FolderIds

3. Frontend:
   VITE_API_BASE_URL=https://api.domain.com

4. Backend CORS:
   allowed frontend domain

5. OAuth App status:
   Testing → In production nếu chạy lâu dài

6. Storage:
   DEV folder IDs → PROD folder IDs
```

Không cần sửa:

```text
- UploadProfileAvatarCommandHandler
- Gallery upload handler
- News upload handler
- Document upload handler
- Minutes upload handler
- Frontend components đang dùng resolveFileUrl
- DB schema nếu file_purpose đã chuẩn
```

---

## 12. Manual test checklist sau refactor

Avatar regression:

```text
[ ] Upload avatar vẫn thành công.
[ ] users.avatar_url vẫn là /api/files/{fileId}/content.
[ ] files có metadata đúng.
[ ] checksum_sha256 không null nếu đã implement.
[ ] Header/sidebar/profile vẫn hiển thị avatar.
```

File service:

```text
[ ] IFileStorageService upload được lên Google Drive.
[ ] IFileStorageService download được theo external_file_id.
[ ] IFileStorageService delete được file vừa upload.
[ ] FileUploadService insert row files đúng.
[ ] FolderResolver chọn đúng folder theo purpose.
[ ] FileValidationPolicy chặn file sai.
```

Proxy content:

```text
[ ] GET /api/files/{fileId}/content trả đúng Content-Type.
[ ] File ảnh hiển thị trong browser/Postman.
[ ] File PDF/DOCX download được.
[ ] File đã xóa trên Drive trả lỗi rõ.
```

Config:

```text
[ ] appsettings.Development.json không bị git track.
[ ] appsettings.Development.example.json có placeholder.
[ ] Không có ClientSecret/RefreshToken trong appsettings.json.
[ ] Không hard-code localhost trong code.
```

Frontend:

```text
[ ] resolveFileUrl hoạt động với /api/files/{id}/content.
[ ] VITE_API_BASE_URL đổi được backend domain.
[ ] Không component nào tự nối localhost.
```

Build:

```text
[ ] dotnet build backend thành công.
[ ] npm run build frontend thành công.
```

---

## 13. Acceptance Criteria

```text
AC-01: Avatar upload hiện tại vẫn chạy sau refactor.
AC-02: Có IFileStorageService dùng chung cho upload/download/delete.
AC-03: Có FileUploadService dùng chung cho validate/checksum/upload/insert files/cleanup.
AC-04: Có FilePurpose constants/enum dùng chung.
AC-05: Có FolderResolver mapping FilePurpose sang folderId.
AC-06: Không hard-code Google Drive folderId trong từng handler nghiệp vụ.
AC-07: Không hard-code localhost trong avatarUrl/fileUrl.
AC-08: DB chỉ lưu metadata và proxy URL/file_id, không lưu binary/base64.
AC-09: Frontend dùng helper resolveFileUrl với VITE_API_BASE_URL.
AC-10: OAuth connect/callback không mở public trong production.
AC-11: appsettings.Development.json không commit; appsettings.json không chứa secret.
AC-12: Module mới như gallery/news/document/minutes có thể tái sử dụng FileUploadService.
AC-13: Khi deploy chỉ cần đổi production env variables + Google Cloud Redirect URI + frontend API base/CORS.
```

---

## 14. Prompt ngắn cho AI Agent

```text
Refactor nhẹ nền tảng Google Drive storage để sẵn sàng mở rộng upload cho gallery/news/document/minutes, không phá upload avatar hiện tại.

Bối cảnh:
- Upload avatar lên Google Drive đã chạy.
- users.avatar_url lưu /api/files/{fileId}/content.
- files lưu metadata, external_file_id, file_purpose, checksum_sha256.
- OAuthUser + RefreshToken đang dùng ở dev.
- Sau này sẽ upload gallery/news/documents/minutes/visit request attachments.
- Mục tiêu là sau này mỗi module chỉ thêm nghiệp vụ riêng, deploy chỉ đổi config production + Google Cloud Redirect URI.

Yêu cầu backend:
1. Tạo/chuẩn hóa IFileStorageService upload/download/delete.
2. Tạo GoogleDriveStorageService implement IFileStorageService.
3. Tạo FilePurpose constants/enum và mapping DB value.
4. Tạo IFileStorageFolderResolver mapping FilePurpose → GoogleDrive folderId.
5. Tạo FileValidationPolicy theo purpose.
6. Tạo FileChecksumService tính SHA-256 lowercase 64 ký tự.
7. Tạo FileObjectKeyBuilder build object_key thống nhất.
8. Tạo FileUploadService dùng chung:
   - validate
   - checksum
   - object_key
   - resolve folder
   - upload Drive
   - insert files
   - cleanup Drive nếu DB lỗi
   - return fileId + /api/files/{fileId}/content
9. Sửa UploadProfileAvatarCommandHandler để dùng FileUploadService thay vì tự upload Drive trực tiếp, nhưng giữ API response hiện tại.
10. Chuẩn hóa GET /api/files/{fileId}/content để stream file từ Google Drive.
11. Không hard-code localhost/folderId/token trong handler.
12. Không commit secret.

Yêu cầu frontend:
1. Tạo resolveFileUrl(url) dùng VITE_API_BASE_URL.
2. Avatar/header/sidebar/profile dùng resolveFileUrl.
3. Tạo helper upload multipart dùng chung nếu phù hợp.
4. Không component nào tự nối localhost.

Config:
1. appsettings.json chỉ placeholder, không secret.
2. appsettings.Development.json giữ secret local, không commit.
3. appsettings.Development.example.json có placeholder cho team.
4. Production dùng env variables:
   GoogleDrive__ClientId, ClientSecret, RefreshToken, RedirectUri, FolderIds.
5. OAuth connect/callback chỉ Development hoặc admin-only.

Acceptance:
- Avatar upload vẫn chạy.
- files metadata đầy đủ.
- GET /api/files/{id}/content vẫn hiển thị ảnh.
- Có thể thêm gallery/news/document/minutes upload bằng cách gọi FileUploadService với FilePurpose tương ứng.
- Deploy production không phải sửa từng handler upload.
```
