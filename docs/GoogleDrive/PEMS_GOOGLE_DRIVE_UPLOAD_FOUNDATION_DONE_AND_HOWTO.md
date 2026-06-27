# PEMS — Nền tảng Upload Google Drive dùng chung: ĐÃ CODE GÌ + HƯỚNG DẪN MỞ RỘNG

> File này là **bản tổng hợp thực tế** sau khi đã refactor nền tảng Google Drive theo
> `PEMS_GOOGLE_DRIVE_STORAGE_FOUNDATION_REFACTOR_FOR_FUTURE_UPLOADS.md`.
>
> Mục đích:
> 1. Ghi lại **chính xác** những gì đã được code (tên file, namespace, signature thật).
> 2. Hướng dẫn từng bước để **code thêm chức năng upload mới** (gallery, news, document, minutes,
>    visit request attachment...) bằng cách **tái sử dụng** nền tảng dùng chung, không viết lại Drive.
>
> AI Agent đọc file này **trước** khi code chức năng upload mới. Mọi thông tin ở đây đã khớp code thật
> (build backend xanh, frontend typecheck sạch tại thời điểm viết). Vẫn nên `grep`/đọc lại file gốc
> trước khi sửa để chắc chắn không bị lệch.

---

## 0. TL;DR cho người vội

Muốn upload 1 file lên Google Drive cho 1 chức năng mới? Chỉ cần:

```csharp
// Trong CommandHandler nghiệp vụ của bạn (đã inject IFileUploadService _fileUpload):
await using var stream = new MemoryStream(request.Content, writable: false);
var uploaded = await _fileUpload.UploadBusinessFileAsync(
    stream,
    request.FileName,
    request.ContentType ?? string.Empty,
    request.Content.LongLength,
    FilePurpose.GalleryImage,   // chọn purpose phù hợp
    (long)userId,
    cancellationToken);

// uploaded.FileId      -> lưu vào bảng nghiệp vụ của bạn (vd gallery_images.file_id)
// uploaded.FileUrl     -> "/api/files/{fileId}/content" (URL proxy để frontend hiển thị)
```

**KHÔNG** gọi `IGoogleDriveStorageService` trực tiếp. **KHÔNG** hard-code folderId. **KHÔNG** tự validate/checksum/insert bảng `files` lại.

---

## 1. Kiến trúc tổng quan

```text
Controller (multipart/form-data)
  └─> MediatR Command (vd: AddGalleryImageCommand) chứa byte[] + fileName + contentType
        └─> CommandHandler nghiệp vụ
              ├─ kiểm tra quyền / nghiệp vụ riêng của bạn
              └─> IFileUploadService.UploadBusinessFileAsync(stream, ..., FilePurpose.X, userId)
                    ├─ FileValidationPolicy.GetRule(purpose)        → rule theo purpose
                    ├─ FileContentValidator.Validate(...)           → size/ext/mime/magic bytes
                    ├─ FileChecksumService.ComputeSha256HexAsync    → checksum_sha256
                    ├─ FileObjectKeyBuilder.Build(...)              → object_key thống nhất
                    ├─ IFileStorageFolderResolver.ResolveFolderId   → folderId theo purpose
                    ├─ IGoogleDriveStorageService.UploadFileAsync   → upload Drive thật
                    ├─ insert row vào bảng `files` (SaveChanges)
                    ├─ nếu DB lỗi → xóa file Drive vừa upload (cleanup, tránh rác)
                    └─ return UploadedFileDto { FileId, FileUrl, ... }
              └─ Handler lưu FileId/FileUrl vào bảng nghiệp vụ của bạn
```

**Tầng thấp nhất (gọi REST Google Drive)** là `IGoogleDriveStorageService` — **đã có sẵn từ trước**,
được tái sử dụng làm storage layer. Nền tảng dùng chung (`FileUploadService`) bọc bên ngoài nó.

**Xem/tải file:** mọi file đều xem qua proxy backend `GET /api/files/{fileId}/content`
(yêu cầu đăng nhập). DB chỉ lưu metadata + external_file_id, **không** lưu binary.

---

## 2. Danh sách file ĐÃ TẠO / ĐÃ SỬA

### 2.1 Backend — file MỚI tạo

| File | Namespace | Vai trò |
|------|-----------|---------|
| `PEMS.Application/Common/Files/FilePurpose.cs` | `PEMS.Application.Common.Files` | enum `FilePurpose` + `FilePurposeDbValues` (string DB) + extension `.ToDbValue()`, `.ToObjectKeyPrefix()` |
| `PEMS.Application/Common/Files/FileValidationRule.cs` | `...Common.Files` | DTO rule: MaxSizeBytes, AllowedMimeTypes, AllowedExtensions, RequireImageMagicBytes |
| `PEMS.Application/Common/Files/FileValidationPolicy.cs` | `...Common.Files` | `IFileValidationPolicy` impl — rule theo từng purpose |
| `PEMS.Application/Common/Files/FileContentValidator.cs` | `...Common.Files` | static validate: size/ext/mime + sniff magic bytes ảnh, chặn SVG/spoof; ném `BusinessRuleException` mã `FILE_*` |
| `PEMS.Application/Common/Files/FileChecksumService.cs` | `...Common.Files` | `IFileChecksumService` impl — SHA-256 hex thường 64 ký tự |
| `PEMS.Application/Common/Files/FileObjectKeyBuilder.cs` | `...Common.Files` | `IFileObjectKeyBuilder` impl — build object_key |
| `PEMS.Application/Common/Files/FileUploadService.cs` | `...Common.Files` | `IFileUploadService` impl — **service chính** |
| `PEMS.Application/Common/Files/UploadedFileDto.cs` | `...Common.Files` | DTO kết quả upload |
| `PEMS.Application/Common/Interfaces/IFileValidationPolicy.cs` | `...Common.Interfaces` | interface policy |
| `PEMS.Application/Common/Interfaces/IFileChecksumService.cs` | `...Common.Interfaces` | interface checksum |
| `PEMS.Application/Common/Interfaces/IFileObjectKeyBuilder.cs` | `...Common.Interfaces` | interface object key |
| `PEMS.Application/Common/Interfaces/IFileUploadService.cs` | `...Common.Interfaces` | interface service chính |
| `PEMS.Application/Common/Interfaces/IFileStorageFolderResolver.cs` | `...Common.Interfaces` | interface resolver folder |
| `PEMS.Infrastructure/FileStorage/GoogleDrive/GoogleDriveFolderResolver.cs` | `PEMS.Infrastructure.FileStorage.GoogleDrive` | `IFileStorageFolderResolver` impl — map purpose → folderId từ `GoogleDriveOptions` |
| `PEMS.Api/appsettings.Development.example.json` | — | file mẫu config cho team (placeholder, commit được) |

### 2.2 Backend — file ĐÃ SỬA

| File | Sửa gì |
|------|--------|
| `PEMS.Application/Profiles/Commands/UploadProfileAvatar/UploadProfileAvatarCommandHandler.cs` | Bỏ phần tự upload/validate/checksum. Giữ kiểm tra auth + user ACTIVE. Gọi `IFileUploadService.UploadBusinessFileAsync(..., FilePurpose.UserAvatar, ...)`. **Response giữ nguyên.** |
| `PEMS.Application/DependencyInjection.cs` | Đăng ký: `IFileChecksumService`, `IFileValidationPolicy` (Singleton); `IFileObjectKeyBuilder`, `IFileUploadService` (Scoped) |
| `PEMS.Infrastructure/DependencyInjection.cs` | Đăng ký `IFileStorageFolderResolver → GoogleDriveFolderResolver` (Scoped) |
| `PEMS.Api/appsettings.json` | Thêm khối `Storage` + `GoogleDrive` **placeholder không có secret** (Enabled, AuthMode, RedirectUri rỗng, các FolderId rỗng) |

### 2.3 Backend — file CŨ vẫn giữ nguyên (KHÔNG sửa)

- `PEMS.Application/Common/Interfaces/IGoogleDriveStorageService.cs` — tầng storage Drive (upload/download/delete). **Tái sử dụng.**
- `PEMS.Infrastructure/FileStorage/GoogleDrive/GoogleDriveStorageService.cs` — impl REST Drive (mint access token từ RefreshToken).
- `PEMS.Application/Common/Storage/GoogleDriveOptions.cs` — options config.
- `PEMS.Domain/Entities/Documents/UploadedFile.cs` — entity bảng `files`.
- `PEMS.Api/Controllers/FilesController.cs` — endpoint `/api/files/{id}/content` và `/download`.
- `PEMS.Application/Files/Queries/GetFileContent/GetFileContentQueryHandler.cs` — stream file từ Drive.

### 2.4 Frontend — file MỚI tạo

| File | Vai trò |
|------|---------|
| `src/shared/utils/resolveFileUrl.ts` | Chuẩn hóa URL file (relative → absolute theo `VITE_API_BASE_URL`); URL http(s) giữ nguyên. Tự bỏ `/api` thừa để tránh `/api/api`. |
| `src/shared/api/fileUploadApi.ts` | Helper upload multipart dùng chung (`uploadFileToEndpoint`) + interface `UploadedFileResponse`. |
| `src/shared/utils/fileValidation.ts` | Validate phía client theo purpose (chỉ để UX): `getFileValidationRule`, `validateFile`. |

---

## 3. Hợp đồng (contract) các thành phần chính

### 3.1 `FilePurpose` (enum)

```csharp
namespace PEMS.Application.Common.Files;

public enum FilePurpose
{
    UserAvatar, GalleryImage, GalleryVideo, NewsImage, NewsAttachment,
    Document, MinutesAttachment, VisitRequestAttachment,
    PartnerDocument, LogisticsAttachment, Other
}
```

Map sang string lưu vào `files.file_purpose` qua `purpose.ToDbValue()`:

| FilePurpose | files.file_purpose (DB) | object_key prefix |
|-------------|-------------------------|-------------------|
| UserAvatar | `USER_AVATAR` | `avatars` |
| GalleryImage | `GALLERY_IMAGE` | `gallery` |
| GalleryVideo | `GALLERY_VIDEO` | `gallery` |
| NewsImage | `NEWS_IMAGE` | `news` |
| NewsAttachment | `NEWS_ATTACHMENT` | `news` |
| Document | `DOCUMENT` | `documents` |
| MinutesAttachment | `MINUTES_ATTACHMENT` | `minutes` |
| VisitRequestAttachment | `VISIT_REQUEST_ATTACHMENT` | `visit-requests` |
| PartnerDocument | `PARTNER_DOCUMENT` | `partners` |
| LogisticsAttachment | `LOGISTICS_ATTACHMENT` | `logistics` |
| Other | `OTHER` | `other` |

> ⚠️ Nếu cột `files.file_purpose` trong DB là `ENUM`/có CHECK constraint, **phải đảm bảo giá trị
> string ở trên hợp lệ** trước khi dùng purpose mới (xem mục 7).

### 3.2 `IFileUploadService` — service chính

```csharp
namespace PEMS.Application.Common.Interfaces;

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

### 3.3 `UploadedFileDto` — kết quả trả về

```csharp
public sealed class UploadedFileDto
{
    public long FileId { get; init; }
    public string FileUrl { get; init; }          // "/api/files/{fileId}/content"
    public string StorageProvider { get; init; }  // "GOOGLE_DRIVE"
    public string ExternalFileId { get; init; }   // id file trên Drive
    public string? WebViewUrl { get; init; }
    public string? DownloadUrl { get; init; }
    public string? ThumbnailUrl { get; init; }
    public string MimeType { get; init; }
    public long FileSize { get; init; }
    public string ChecksumSha256 { get; init; }
    public string ObjectKey { get; init; }
}
```

### 3.4 `IFileStorageFolderResolver` — map purpose → folderId

```csharp
public interface IFileStorageFolderResolver
{
    string ResolveFolderId(FilePurpose purpose);   // ném GOOGLE_DRIVE_FOLDER_NOT_CONFIGURED nếu thiếu
}
```

Mapping thực tế trong `GoogleDriveFolderResolver` (dùng đúng tên field thật của `GoogleDriveOptions`):

| FilePurpose | Field trong GoogleDriveOptions |
|-------------|--------------------------------|
| UserAvatar | `AvatarFolderId` |
| GalleryImage / GalleryVideo | `GalleryFolderId` |
| NewsImage / NewsAttachment | `NewsFolderId` |
| Document / PartnerDocument / LogisticsAttachment | `DocumentPartnerFolderId` |
| MinutesAttachment | `MinutesFolderId` |
| VisitRequestAttachment | `VisitRequestDocumentFolderId` |
| (fallback nếu rỗng) | `RootFolderId` |

> Lưu ý: `GoogleDriveOptions` còn có `VisitRequestPhotoFolderId` (chưa được map mặc định). Nếu cần
> tách ảnh visit request sang folder riêng, thêm 1 purpose mới (vd `VisitRequestPhoto`) và map vào field này.

---

## 4. Rule validate theo purpose (server-side — `FileValidationPolicy`)

| Purpose | Max size | Định dạng cho phép | Magic bytes? |
|---------|----------|--------------------|--------------|
| UserAvatar | 2 MB | JPG/JPEG/PNG/WEBP | ✅ (chặn SVG/spoof) |
| GalleryImage, NewsImage | 5 MB | JPG/JPEG/PNG/WEBP | ✅ |
| GalleryVideo | 100 MB | MP4/WEBM | ❌ |
| NewsAttachment, Document, PartnerDocument, LogisticsAttachment | 20 MB | PDF/DOCX/XLSX/PPTX/JPG/PNG | ❌ |
| MinutesAttachment | 10 MB | PDF/DOCX | ❌ |
| VisitRequestAttachment | 10 MB | PDF/DOCX/JPG/PNG | ❌ |
| (mặc định/Other) | 10 MB | PDF/DOCX/XLSX/PPTX/JPG/PNG | ❌ |

Frontend (`fileValidation.ts`) có rule **tương ứng** để báo lỗi sớm — nhưng **backend luôn là nơi quyết định cuối cùng**.

---

## 5. Mã lỗi chuẩn (BusinessRuleException → HTTP 422)

> ⚠️ Trong code thật, constructor là `BusinessRuleException(message, errorCode)` — **message trước, code sau**
> (ngược với pseudo-code trong tài liệu gốc).

Validate file (từ `FileContentValidator`):
- `FILE_EMPTY` — file rỗng
- `FILE_TOO_LARGE` — vượt kích thước
- `FILE_INVALID_EXTENSION` — sai phần mở rộng
- `FILE_INVALID_TYPE` — sai MIME
- `FILE_MAGIC_BYTES_MISMATCH` — nội dung không khớp (ảnh giả mạo / SVG)

Google Drive / folder (từ resolver + storage service):
- `GOOGLE_DRIVE_FOLDER_NOT_CONFIGURED` — chưa cấu hình folder cho purpose
- `GOOGLE_DRIVE_NOT_CONNECTED` / `GOOGLE_DRIVE_CONFIG_MISSING` — thiếu RefreshToken/ClientId
- `GOOGLE_DRIVE_TOKEN_EXPIRED` — token hết hạn (invalid_grant)
- `GOOGLE_DRIVE_UPLOAD_FAILED` / `GOOGLE_DRIVE_AUTH_FAILED` — upload/kết nối lỗi
- `GOOGLE_DRIVE_FOLDER_NOT_FOUND_OR_NO_PERMISSION` — folder không tồn tại/không có quyền

Frontend chỉ cần hiển thị `message` (tiếng Việt) trả về; không phụ thuộc vào từng mã.

---

## 6. Config

### 6.1 `appsettings.json` (commit, KHÔNG secret)
Có khối `Storage.Provider = "GoogleDrive"` + `GoogleDrive` với các key cấu trúc rỗng (placeholder).

### 6.2 `appsettings.Development.json` (KHÔNG commit — đã gitignore)
Chứa secret thật cho dev: `ClientId`, `ClientSecret`, `RefreshToken`, các `*FolderId`.

### 6.3 `appsettings.Development.example.json` (commit cho team)
Bản mẫu có placeholder `YOUR_...` để dev mới copy thành `appsettings.Development.json`.

### 6.4 Production — dùng Environment Variables (override)
```text
GoogleDrive__Enabled=true
GoogleDrive__AuthMode=OAuthUser
GoogleDrive__ClientId=<prod-client-id>
GoogleDrive__ClientSecret=<prod-client-secret>
GoogleDrive__RefreshToken=<prod-refresh-token>
GoogleDrive__RedirectUri=https://api.your-domain.com/api/google-drive/oauth/callback
GoogleDrive__RootFolderId=...
GoogleDrive__AvatarFolderId=...
GoogleDrive__GalleryFolderId=...
GoogleDrive__NewsFolderId=...
GoogleDrive__DocumentPartnerFolderId=...
GoogleDrive__MinutesFolderId=...
GoogleDrive__VisitRequestDocumentFolderId=...
GoogleDrive__VisitRequestPhotoFolderId=...
```
Frontend prod: `VITE_API_BASE_URL=https://api.your-domain.com/api`

Khi deploy chỉ đổi: env backend + Google Cloud Authorized Redirect URI + `VITE_API_BASE_URL` + CORS.
**Không** phải sửa handler upload nào.

---

## 7. HƯỚNG DẪN: code chức năng upload MỚI (step-by-step)

Ví dụ chạy xuyên suốt: **Gallery — thêm ảnh vào gallery** (`gallery_images.file_id`).

### Bước 0 — Có cần thêm FilePurpose mới không?
- Nếu purpose đã tồn tại (GalleryImage, NewsImage, Document, MinutesAttachment, VisitRequestAttachment...) → **dùng luôn**, sang Bước 1.
- Nếu cần purpose hoàn toàn mới:
  1. Thêm giá trị vào enum `FilePurpose` (FilePurpose.cs).
  2. Thêm hằng vào `FilePurposeDbValues` + nhánh trong `ToDbValue()` + `ToObjectKeyPrefix()`.
  3. Thêm nhánh rule trong `FileValidationPolicy.GetRule(...)`.
  4. Thêm nhánh map folder trong `GoogleDriveFolderResolver.ResolveFolderId(...)` + field tương ứng trong `GoogleDriveOptions` + config.
  5. **Kiểm tra DB**: nếu `files.file_purpose` là ENUM/CHECK, viết SQL patch thêm giá trị mới. **Không tự bịa giá trị nếu DB chưa cho phép.**

### Bước 1 — Tạo Command + Handler nghiệp vụ (backend)

```csharp
// Application/Gallery/Commands/AddGalleryImage/AddGalleryImageCommand.cs
public sealed record AddGalleryImageCommand(
    long GalleryId,
    byte[] Content,
    string FileName,
    string? ContentType) : IRequest<AddGalleryImageResponse>;
```

```csharp
// Application/Gallery/Commands/AddGalleryImage/AddGalleryImageCommandHandler.cs
public sealed class AddGalleryImageCommandHandler
    : IRequestHandler<AddGalleryImageCommand, AddGalleryImageResponse>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IFileUploadService _fileUpload;     // <-- inject service dùng chung

    public AddGalleryImageCommandHandler(
        IApplicationDbContext db, ICurrentUserService currentUser, IFileUploadService fileUpload)
    {
        _db = db; _currentUser = currentUser; _fileUpload = fileUpload;
    }

    public async Task<AddGalleryImageResponse> Handle(
        AddGalleryImageCommand request, CancellationToken ct)
    {
        // 1) Kiểm tra auth + quyền/nghiệp vụ RIÊNG của bạn (KHÔNG đụng tới upload).
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is not { } userId)
            throw new ForbiddenException();
        // ... kiểm tra gallery tồn tại, quyền chỉnh sửa gallery, v.v.

        if (request.Content is null || request.Content.Length == 0)
            throw new BusinessRuleException("Vui lòng chọn ảnh.", "FILE_REQUIRED");

        // 2) Giao toàn bộ việc file cho service dùng chung.
        await using var stream = new MemoryStream(request.Content, writable: false);
        var uploaded = await _fileUpload.UploadBusinessFileAsync(
            stream,
            request.FileName,
            request.ContentType ?? string.Empty,
            request.Content.LongLength,
            FilePurpose.GalleryImage,     // <-- chọn purpose
            (long)userId,
            ct);

        // 3) Lưu liên kết vào bảng nghiệp vụ của bạn.
        var image = new GalleryImage
        {
            GalleryId = request.GalleryId,
            FileId = (ulong)uploaded.FileId,
            // ... các field nghiệp vụ khác
        };
        _db.GalleryImages.Add(image);
        await _db.SaveChangesAsync(ct);

        // 4) Trả về cho frontend (FileUrl để hiển thị).
        return new AddGalleryImageResponse
        {
            GalleryImageId = (long)image.GalleryImageId,
            FileId = uploaded.FileId,
            FileUrl = uploaded.FileUrl,         // "/api/files/{id}/content"
            ThumbnailUrl = uploaded.ThumbnailUrl,
        };
    }
}
```

> Cần thêm `using PEMS.Application.Common.Files;` (FilePurpose) và `using PEMS.Application.Common.Interfaces;`.

### Bước 2 — Controller (backend)

```csharp
[HttpPost("{galleryId}/images")]
[Authorize]
[RequestSizeLimit(10 * 1024 * 1024)]   // hơi cao hơn giới hạn nghiệp vụ
[Consumes("multipart/form-data")]
public async Task<IActionResult> AddImage(
    long galleryId, IFormFile file, CancellationToken ct)
{
    if (file is null || file.Length == 0)
        return BadRequest(new { message = "Tệp tải lên rỗng hoặc không hợp lệ." });

    using var ms = new MemoryStream();
    await file.CopyToAsync(ms, ct);

    var result = await _mediator.Send(new AddGalleryImageCommand(
        galleryId, ms.ToArray(), file.FileName, file.ContentType), ct);

    return Ok(result);
}
```

> Pattern này giống y `ProfilesController.UploadAvatar` (buffer bytes ở controller → command). Avatar dùng `[HttpPut]`,
> chức năng tạo mới thường dùng `[HttpPost]`.

### Bước 3 — Frontend

```ts
// validate sớm (UX)
import { validateFile } from '@/shared/utils/fileValidation';
import { uploadFileToEndpoint } from '@/shared/api/fileUploadApi';
import { resolveFileUrl } from '@/shared/utils/resolveFileUrl';

const check = validateFile(file, 'GALLERY_IMAGE');
if (!check.ok) { showError(check.message); return; }

// upload (field name 'file' phải khớp tham số IFormFile ở controller)
const res = await uploadFileToEndpoint(`/gallery/${galleryId}/images`, 'file', file, 'post');

// hiển thị: ảnh là private -> dùng useAuthenticatedImage(res.fileUrl)
//           nếu là asset public -> resolveFileUrl(res.fileUrl)
```

**Hiển thị ảnh:**
- Ảnh **cần đăng nhập** (avatar, file nội bộ): dùng hook `useAuthenticatedImage(path)` (đính kèm Bearer token).
- Asset **public** hoặc link tuyệt đối: dùng `resolveFileUrl(url)`.

### Bước 4 — Build & test
- `dotnet build` backend phải xanh.
- `npm run build` (hoặc `tsc --noEmit`) frontend phải sạch.
- Test thật 1 lần: upload thành công → có row trong `files` (đủ metadata + checksum) → ảnh hiển thị qua `/api/files/{id}/content`.

---

## 8. Mẫu nhanh cho từng module

| Module | FilePurpose | Bảng nghiệp vụ lưu liên kết (theo schema thật) |
|--------|-------------|-----------------------------------------------|
| Gallery ảnh | `GalleryImage` | `gallery_images.file_id` |
| Gallery video | `GalleryVideo` | `gallery_images.file_id` (hoặc bảng video) |
| News ảnh | `NewsImage` | `news_section_files.file_id` hoặc `news.thumbnail_file_id` |
| News đính kèm | `NewsAttachment` | `news_section_files.file_id` |
| Document | `Document` | `documents.file_id` hoặc mapping `document_files` |
| Minutes đính kèm | `MinutesAttachment` | field/bảng attachment của minutes |
| Visit request đính kèm | `VisitRequestAttachment` | bảng request/guest/member/document tương ứng |

> ⚠️ Tên bảng/cột nghiệp vụ ở trên là **gợi ý** — phải kiểm tra schema thật trước khi code (xem
> `PEMS.Domain/Entities/...` và `IApplicationDbContext`). Nếu chưa có `DbSet`/entity tương ứng thì
> tạo theo schema thật.

---

## 9. QUY TẮC BẮT BUỘC (đọc kỹ)

**KHÔNG được:**
- ❌ Tự gọi `IGoogleDriveStorageService` trong handler nghiệp vụ (trừ khi có lý do đặc biệt). Dùng `IFileUploadService`.
- ❌ Hard-code folderId / token / localhost trong handler.
- ❌ Tự viết lại validate/checksum/insert bảng `files`/cleanup — đã có trong `FileUploadService`.
- ❌ Lưu binary/base64 vào DB.
- ❌ Lưu URL Google Drive trực tiếp vào field nghiệp vụ (vd avatar_url). Luôn lưu `/api/files/{id}/content`.
- ❌ Cho frontend gọi Google Drive API trực tiếp.
- ❌ Commit `ClientSecret`/`RefreshToken`. Không log token.
- ❌ Bịa giá trị `file_purpose` mà DB chưa cho phép.
- ❌ Nhận SVG cho nhóm ảnh.

**NÊN làm:**
- ✅ Handler chỉ lo nghiệp vụ + gọi `UploadBusinessFileAsync` + lưu `FileId` vào bảng của mình.
- ✅ Chọn đúng `FilePurpose`; thêm purpose mới đúng quy trình (mục 7 Bước 0) nếu cần.
- ✅ Validate frontend để UX, nhưng tin backend là cuối cùng.
- ✅ Dùng `resolveFileUrl` / `useAuthenticatedImage`, không tự nối localhost.

---

## 10. Checklist khi thêm module upload mới

```text
[ ] Đã chọn (hoặc thêm đúng quy trình) FilePurpose phù hợp.
[ ] Nếu thêm purpose mới: cập nhật enum + DbValues + ToObjectKeyPrefix + Policy + FolderResolver + Options + config + (SQL patch enum nếu cần).
[ ] Command + Handler nghiệp vụ inject IFileUploadService, KHÔNG đụng Drive trực tiếp.
[ ] Handler kiểm tra quyền/nghiệp vụ riêng trước khi upload.
[ ] Lưu uploaded.FileId vào bảng nghiệp vụ; trả uploaded.FileUrl cho frontend.
[ ] Controller multipart, field name khớp frontend, có RequestSizeLimit hợp lý.
[ ] Frontend: validateFile + uploadFileToEndpoint; hiển thị bằng useAuthenticatedImage (private) hoặc resolveFileUrl (public).
[ ] dotnet build xanh, frontend typecheck sạch.
[ ] Test thật: row trong files đủ metadata + checksum; xem được qua /api/files/{id}/content.
[ ] Không commit secret; không hard-code localhost/folderId/token.
```

---

## 11. Khác biệt so với tài liệu refactor gốc (để AI Agent không nhầm)

1. **KHÔNG tạo `IFileStorageService` mới** như tài liệu mô tả — tên này **đã tồn tại** trong dự án cho
   store local/email (contract `SaveAsync`/`OpenReadAsync` khác hẳn). Tầng storage Drive là
   `IGoogleDriveStorageService` có sẵn (`UploadFileAsync`/`DownloadAsync`/`DeleteAsync`), được tái sử dụng.
2. **Tên field folder trong `GoogleDriveOptions` là tên THẬT**: `DocumentPartnerFolderId`,
   `VisitRequestDocumentFolderId`, `VisitRequestPhotoFolderId`, `AvatarFolderId`, `GalleryFolderId`,
   `NewsFolderId`, `MinutesFolderId`, `RootFolderId` — KHÔNG phải `DocumentFolderId`/`VisitRequestFolderId`
   như ví dụ trong tài liệu.
3. **`BusinessRuleException(message, errorCode)`** — message trước, code sau (ngược tài liệu).
4. **Endpoint `GET /api/files/{id}/content` giữ nguyên** (đã stream Drive + Content-Type + cache
   `private, max-age=3600`). Không đổi mã lỗi download vì dùng chung với pipeline email.
5. Mã lỗi validate avatar đổi `AVATAR_*` → `FILE_*` (theo chuẩn chung). Frontend chỉ hiển thị message nên không vỡ.
6. `UploadedFileDto` của nền tảng nằm ở namespace `PEMS.Application.Common.Files` (khác với
   `UploadedFileDto` cũ ở `PEMS.Application.Files.Commands.UploadFile` — của endpoint upload email/attachment).

---

## 12. Tham chiếu nhanh (file:vai trò)

- Service chính: `PEMS.Application/Common/Files/FileUploadService.cs`
- Enum purpose: `PEMS.Application/Common/Files/FilePurpose.cs`
- Rule validate: `PEMS.Application/Common/Files/FileValidationPolicy.cs` + `FileContentValidator.cs`
- Resolver folder: `PEMS.Infrastructure/FileStorage/GoogleDrive/GoogleDriveFolderResolver.cs`
- Storage Drive (tầng thấp): `PEMS.Infrastructure/FileStorage/GoogleDrive/GoogleDriveStorageService.cs`
- Options: `PEMS.Application/Common/Storage/GoogleDriveOptions.cs`
- Entity files: `PEMS.Domain/Entities/Documents/UploadedFile.cs`
- Endpoint xem file: `PEMS.Api/Controllers/FilesController.cs` → `GetFileContentQueryHandler.cs`
- Ví dụ tham khảo (đã refactor): `PEMS.Application/Profiles/Commands/UploadProfileAvatar/UploadProfileAvatarCommandHandler.cs`
- Frontend: `src/shared/api/fileUploadApi.ts`, `src/shared/utils/fileValidation.ts`, `src/shared/utils/resolveFileUrl.ts`
```
