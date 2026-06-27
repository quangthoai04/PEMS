# PEMS — UC-15 Update Profile: Upload Avatar lên Google Drive

> File này dùng cho AI Agent đọc và code chức năng **upload ảnh avatar trong màn Edit Profile** của PEMS.  
> Text profile update hiện đã có và đang chạy. **Không rewrite lại luồng update text**, chỉ bổ sung phần upload avatar.

---

## 1. Mục tiêu chức năng

Hoàn thiện phần đổi ảnh đại diện trong profile:

```text
User đăng nhập
→ Mở màn hình Profile / Edit Profile
→ Chọn ảnh avatar
→ Frontend preview ảnh
→ Gửi file ảnh lên Backend bằng multipart/form-data
→ Backend validate file
→ Backend upload ảnh lên Google Drive folder avatars
→ Backend lưu metadata vào bảng files
→ Backend cập nhật users.avatar_url
→ Frontend hiển thị avatar mới
```

Chức năng này thuộc phạm vi:

```text
UC-14 View Profile
UC-15 Update Profile
```

Nguyên tắc quyền:

```text
- Chỉ user đã đăng nhập mới được upload avatar.
- User chỉ được đổi avatar của chính mình.
- Không cho frontend truyền userId để đổi avatar người khác.
- Không cho update role/campus/department qua luồng profile.
```

---

## 2. Bối cảnh hiện tại

Dự án PEMS dùng:

```text
Backend: ASP.NET Core .NET 8, Clean Architecture, MediatR, EF Core, MySQL
Frontend: React + Vite + TypeScript
Storage mới: Google Drive API OAuthUser
Database: MySQL v10, đã có bảng files và cột users.avatar_url
```

Hiện tại đã có cấu hình Google Drive trong:

```text
backend/PEMS.Api/appsettings.Development.json
```

Ví dụ:

```json
{
  "Storage": {
    "Provider": "GoogleDrive"
  },
  "GoogleDrive": {
    "Enabled": true,
    "AuthMode": "OAuthUser",
    "ClientId": "CLIENT_ID_THAT",
    "ClientSecret": "CLIENT_SECRET_THAT",
    "RedirectUri": "http://localhost:5265/api/google-drive/oauth/callback",
    "RootFolderId": "ID_FOLDER_PEMS_STORAGE",
    "AvatarFolderId": "ID_FOLDER_AVATARS",
    "DocumentFolderId": "ID_FOLDER_DOCUMENTS",
    "GalleryFolderId": "ID_FOLDER_GALLERY",
    "NewsFolderId": "ID_FOLDER_NEWS",
    "MinutesFolderId": "ID_FOLDER_MINUTES",
    "VisitRequestFolderId": "ID_FOLDER_VISIT_REQUESTS",
    "RefreshToken": "REFRESH_TOKEN_THAT"
  }
}
```

Lưu ý:

```text
- appsettings.Development.json không được commit lên GitHub.
- appsettings.json chỉ để placeholder hoặc config không nhạy cảm.
- ClientSecret và RefreshToken tuyệt đối không commit.
```

---

## 3. Phạm vi cần code

### 3.1 Backend cần code

Bổ sung:

```text
1. GoogleDriveOptions
2. GoogleDriveStorageService nếu chưa có
3. UploadProfileAvatarCommand
4. UploadProfileAvatarCommandHandler
5. UploadProfileAvatarCommandValidator
6. UploadProfileAvatarResponse
7. Endpoint PUT /api/profiles/me/avatar
8. Endpoint GET /api/files/{fileId}/content nếu dùng avatar_url dạng backend proxy
```

### 3.2 Frontend cần code

Bổ sung vào màn Profile/Edit Profile hiện tại:

```text
1. UI chọn ảnh avatar
2. Preview avatar trước khi upload
3. Validate file type/size ở frontend
4. Gọi API PUT /api/profiles/me/avatar bằng FormData
5. Cập nhật profile state sau khi upload thành công
6. Cập nhật AuthContext/currentUser nếu header/sidebar đang dùng avatar
7. Hiển thị loading, success, error rõ ràng
```

### 3.3 Không làm trong task này

Không được:

```text
- Không rewrite màn Profile nếu text update đang chạy.
- Không sửa logic update text profile nếu không cần.
- Không thêm bảng/cột DB mới nếu users.avatar_url và files đã đủ.
- Không lưu binary ảnh vào MySQL.
- Không lưu base64 vào users.avatar_url.
- Không commit secret Google Drive.
- Không dùng local path như C:\uploads\... hoặc /uploads/avatar.jpg.
- Không cho frontend truyền userId để update avatar.
```

---

## 4. API contract đề xuất

### 4.1 Upload avatar

```http
PUT /api/profiles/me/avatar
Authorization: Bearer <access_token>
Content-Type: multipart/form-data

avatar=<file>
```

Tên field multipart bắt buộc:

```text
avatar
```

Response thành công:

```json
{
  "success": true,
  "message": "Cập nhật ảnh đại diện thành công.",
  "data": {
    "fileId": 123,
    "avatarUrl": "/api/files/123/content",
    "webViewUrl": "https://drive.google.com/file/d/xxx/view",
    "thumbnailUrl": "https://..."
  }
}
```

Response lỗi gợi ý:

```json
{
  "success": false,
  "message": "Ảnh đại diện chỉ hỗ trợ JPG, PNG hoặc WEBP.",
  "errorCode": "AVATAR_INVALID_TYPE"
}
```

### 4.2 Proxy file content

Nếu lưu `users.avatar_url = "/api/files/{fileId}/content"` thì cần endpoint:

```http
GET /api/files/{fileId}/content
Authorization: Bearer <access_token>
```

Endpoint này đọc metadata trong bảng `files`, tải stream từ Google Drive theo `external_file_id`, rồi trả file stream về frontend.

---

## 5. Quy tắc lưu dữ liệu

### 5.1 Bảng users

Sau khi upload avatar thành công:

```text
users.avatar_url = "/api/files/{fileId}/content"
```

Không khuyến nghị lưu trực tiếp Google Drive URL dài vào `users.avatar_url`, vì:

```text
- URL Google có thể dài.
- Khó kiểm soát quyền truy cập.
- Khó thay đổi storage provider sau này.
- Backend proxy giúp thống nhất bảo mật và cache.
```

### 5.2 Bảng files

Insert một row mới vào `files` sau mỗi lần upload avatar thành công.

Giá trị gợi ý:

```text
storage_provider = GOOGLE_DRIVE
file_purpose     = USER_AVATAR
uploaded_by      = currentUserId
external_file_id = Google Drive file id
web_view_url     = Google Drive webViewLink
download_url     = Google Drive download link hoặc backend download route
thumbnail_url    = thumbnail nếu Google trả về
mime_type        = image/jpeg | image/png | image/webp
file_size        = size bytes
original_filename = tên file gốc đã sanitize
object_key       = avatars/{userId}/{yyyyMMddHHmmss}_{guid}.{ext}
checksum_sha256  = hash nếu có implement
```

Nếu enum `file_purpose` trong DB chưa có `USER_AVATAR`, kiểm tra schema hiện tại trước khi code. Nếu chưa có thì dùng giá trị phù hợp đã tồn tại trong schema, hoặc tạo SQL patch rõ ràng nếu bắt buộc.

---

## 6. Backend — thiết kế chi tiết

### 6.1 GoogleDriveOptions

Tạo options class nếu chưa có:

```csharp
public sealed class GoogleDriveOptions
{
    public bool Enabled { get; set; }
    public string AuthMode { get; set; } = "OAuthUser";
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string RedirectUri { get; set; } = string.Empty;
    public string RootFolderId { get; set; } = string.Empty;
    public string AvatarFolderId { get; set; } = string.Empty;
    public string DocumentFolderId { get; set; } = string.Empty;
    public string GalleryFolderId { get; set; } = string.Empty;
    public string NewsFolderId { get; set; } = string.Empty;
    public string MinutesFolderId { get; set; } = string.Empty;
    public string VisitRequestFolderId { get; set; } = string.Empty;
    public string? RefreshToken { get; set; }
}
```

Đăng ký trong DI:

```csharp
services.Configure<GoogleDriveOptions>(
    configuration.GetSection("GoogleDrive"));
```

---

### 6.2 GoogleDriveStorageService

Tạo service trong Infrastructure, ví dụ:

```text
backend/PEMS.Infrastructure/FileStorage/GoogleDrive/
├── GoogleDriveOptions.cs
├── GoogleDriveStorageService.cs
└── GoogleDriveUploadResult.cs
```

Interface gợi ý đặt trong Application abstraction:

```csharp
public interface IFileStorageService
{
    Task<FileStorageUploadResult> UploadAsync(
        Stream stream,
        string fileName,
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

Upload result gợi ý:

```csharp
public sealed class FileStorageUploadResult
{
    public string StorageProvider { get; set; } = "GOOGLE_DRIVE";
    public string ExternalFileId { get; set; } = default!;
    public string? WebViewUrl { get; set; }
    public string? DownloadUrl { get; set; }
    public string? ThumbnailUrl { get; set; }
    public string ObjectKey { get; set; } = default!;
    public string MimeType { get; set; } = default!;
    public long FileSize { get; set; }
}
```

Nhiệm vụ:

```text
1. Đọc ClientId, ClientSecret, RefreshToken từ GoogleDriveOptions.
2. Dùng RefreshToken lấy AccessToken.
3. Tạo Google Drive file metadata:
   - name = objectKey hoặc sanitized filename
   - parents = [AvatarFolderId]
4. Upload stream bằng Google Drive API.
5. Lấy lại id, webViewLink, webContentLink, thumbnailLink nếu có.
6. Trả result cho handler.
```

Nếu `RefreshToken` rỗng hoặc hết hạn, throw lỗi nghiệp vụ rõ:

```text
GOOGLE_DRIVE_NOT_CONNECTED
GOOGLE_DRIVE_TOKEN_EXPIRED
```

---

### 6.3 UploadProfileAvatarCommand

Tạo folder:

```text
backend/PEMS.Application/Profiles/Commands/UploadProfileAvatar/
```

Command:

```csharp
public sealed record UploadProfileAvatarCommand(
    long CurrentUserId,
    Stream FileStream,
    string OriginalFileName,
    string ContentType,
    long FileSize
) : IRequest<UploadProfileAvatarResponse>;
```

Response:

```csharp
public sealed class UploadProfileAvatarResponse
{
    public long FileId { get; set; }
    public string AvatarUrl { get; set; } = default!;
    public string? WebViewUrl { get; set; }
    public string? ThumbnailUrl { get; set; }
}
```

---

### 6.4 Validation

Validator hoặc handler phải đảm bảo:

```text
- File bắt buộc có.
- File size > 0.
- File size <= 2MB.
- MIME type chỉ nhận:
  - image/jpeg
  - image/png
  - image/webp
- Extension chỉ nhận:
  - .jpg
  - .jpeg
  - .png
  - .webp
- Chặn .svg.
- Nên check magic bytes để tránh giả mạo Content-Type.
- User phải tồn tại.
- User phải ACTIVE.
- User chỉ update avatar của chính mình.
```

Magic bytes gợi ý:

```text
JPEG: FF D8 FF
PNG:  89 50 4E 47 0D 0A 1A 0A
WEBP: RIFF....WEBP
```

Thông báo lỗi gợi ý:

```text
AVATAR_FILE_REQUIRED       → Vui lòng chọn ảnh đại diện.
AVATAR_FILE_TOO_LARGE      → Ảnh đại diện không được vượt quá 2MB.
AVATAR_INVALID_TYPE        → Ảnh đại diện chỉ hỗ trợ JPG, PNG hoặc WEBP.
USER_NOT_FOUND             → Không tìm thấy tài khoản.
USER_INACTIVE              → Tài khoản không còn hoạt động.
GOOGLE_DRIVE_NOT_CONNECTED → Google Drive chưa được kết nối.
GOOGLE_DRIVE_TOKEN_EXPIRED → Token Google Drive đã hết hạn, vui lòng kết nối lại.
UPLOAD_AVATAR_FAILED       → Không thể cập nhật ảnh đại diện. Vui lòng thử lại.
```

---

### 6.5 Handler flow

Pseudo flow:

```text
Handle UploadProfileAvatarCommand
1. Load current user by CurrentUserId.
2. Nếu user không tồn tại → NotFound.
3. Nếu user.status != ACTIVE → Business error.
4. Validate file type/size/magic bytes.
5. Build safe objectKey:
   avatars/{currentUserId}/{yyyyMMddHHmmss}_{Guid}.{ext}
6. Upload stream lên Google Drive AvatarFolderId.
7. Insert row vào files.
8. SaveChanges để có fileId.
9. Set users.avatar_url = "/api/files/{fileId}/content".
10. SaveChanges.
11. Return fileId, avatarUrl, webViewUrl, thumbnailUrl.
```

Lưu ý transaction:

```text
- MySQL transaction không bao phủ được Google Drive.
- Nếu upload Google Drive thành công nhưng DB save lỗi, handler nên cố gắng xóa file Drive vừa upload.
- Nếu xóa bù cũng lỗi, log lại để xử lý thủ công.
```

Pseudo cleanup:

```csharp
string? uploadedExternalFileId = null;

try
{
    var uploadResult = await storage.UploadAsync(...);
    uploadedExternalFileId = uploadResult.ExternalFileId;

    // insert files, update users.avatar_url
    await db.SaveChangesAsync(cancellationToken);

    return response;
}
catch
{
    if (!string.IsNullOrWhiteSpace(uploadedExternalFileId))
    {
        try { await storage.DeleteAsync(uploadedExternalFileId, cancellationToken); }
        catch { /* log cleanup failure */ }
    }

    throw;
}
```

---

### 6.6 Controller

Trong `ProfilesController` thêm endpoint:

```csharp
[HttpPut("me/avatar")]
[Authorize]
[Consumes("multipart/form-data")]
public async Task<IActionResult> UploadAvatar(IFormFile avatar, CancellationToken cancellationToken)
{
    var currentUserId = _currentUser.UserId;

    await using var stream = avatar.OpenReadStream();

    var result = await _mediator.Send(
        new UploadProfileAvatarCommand(
            currentUserId,
            stream,
            avatar.FileName,
            avatar.ContentType,
            avatar.Length),
        cancellationToken);

    return Ok(ApiResponse.Success(result, "Cập nhật ảnh đại diện thành công."));
}
```

Controller chỉ nhận request và gọi MediatR. Không query DB trong controller.

---

### 6.7 FilesController proxy content

Nếu chưa có, tạo endpoint:

```http
GET /api/files/{fileId}/content
```

Flow:

```text
1. Load files by fileId.
2. Nếu không tồn tại → 404.
3. Check quyền đọc file.
4. Nếu storage_provider = GOOGLE_DRIVE:
   - dùng external_file_id download stream từ Drive
   - return File(stream, mime_type)
5. Với avatar:
   - authenticated user có thể xem avatar user khác trong hệ thống
   - nếu cần public avatar thì phải thiết kế policy riêng
```

Response:

```csharp
return File(stream, file.MimeType);
```

Có thể thêm cache header cho avatar:

```text
Cache-Control: private, max-age=3600
```

---

## 7. Frontend — thiết kế chi tiết

### 7.1 API function

Trong profile API service:

```ts
export async function uploadProfileAvatar(file: File): Promise<UploadAvatarResponse> {
  const formData = new FormData();
  formData.append("avatar", file);

  const response = await httpClient.put("/api/profiles/me/avatar", formData, {
    headers: {
      "Content-Type": "multipart/form-data",
    },
  });

  return response.data.data;
}
```

Type:

```ts
export interface UploadAvatarResponse {
  fileId: number;
  avatarUrl: string;
  webViewUrl?: string;
  thumbnailUrl?: string;
}
```

---

### 7.2 Frontend validation

Trước khi gọi API, validate:

```ts
const MAX_AVATAR_SIZE = 2 * 1024 * 1024;

const ALLOWED_AVATAR_TYPES = [
  "image/jpeg",
  "image/png",
  "image/webp",
];

function validateAvatarFile(file: File): string | null {
  if (!ALLOWED_AVATAR_TYPES.includes(file.type)) {
    return "Ảnh đại diện chỉ hỗ trợ JPG, PNG hoặc WEBP.";
  }

  if (file.size > MAX_AVATAR_SIZE) {
    return "Ảnh đại diện không được vượt quá 2MB.";
  }

  return null;
}
```

Không chỉ dựa vào frontend. Backend vẫn phải validate lại.

---

### 7.3 UI flow

Trên màn Edit Profile:

```text
1. Hiển thị avatar hiện tại.
2. Có nút "Đổi ảnh".
3. Khi bấm "Đổi ảnh" → mở file picker.
4. Sau khi chọn file:
   - Validate type/size.
   - Preview ảnh bằng URL.createObjectURL(file).
   - Hiển thị tên file và dung lượng nếu cần.
   - Hiển thị trạng thái "Chưa lưu" nếu chưa upload.
5. User bấm "Lưu ảnh" hoặc "Cập nhật ảnh".
6. Trong lúc upload:
   - disable button
   - hiện spinner/loading nhỏ trên avatar
7. Thành công:
   - update avatarUrl trong profile state
   - update AuthContext/currentUser nếu header/sidebar dùng avatar
   - clear selected file
   - toast success
8. Thất bại:
   - giữ avatar cũ
   - hiển thị lỗi rõ
```

Gợi ý UI:

```text
[Avatar tròn lớn]
Tên người dùng
Email readonly
[Đổi ảnh] [Lưu ảnh]
```

Không cần thêm modal nếu màn profile đã có layout avatar.

---

### 7.4 Cập nhật AuthContext/currentUser

Nếu header/sidebar hiển thị avatar từ `currentUser.avatarUrl`, sau upload thành công phải cập nhật cả context.

Ví dụ logic:

```ts
setProfile((prev) => ({
  ...prev,
  avatarUrl: result.avatarUrl,
}));

updateCurrentUser?.({
  avatarUrl: result.avatarUrl,
});
```

Tên hàm thực tế tùy code hiện tại. Không tự rewrite AuthContext nếu chưa cần.

---

### 7.5 Dọn object URL preview

Nếu dùng `URL.createObjectURL(file)` để preview, phải cleanup:

```ts
useEffect(() => {
  if (!previewUrl) return;

  return () => {
    URL.revokeObjectURL(previewUrl);
  };
}, [previewUrl]);
```

---

## 8. Security / Privacy

Bắt buộc:

```text
- Không commit ClientSecret / RefreshToken.
- Không expose Google Drive RefreshToken về frontend.
- Không để frontend gọi Google Drive API trực tiếp.
- Frontend chỉ gọi backend PEMS.
- Backend là nơi upload lên Google Drive.
- Backend kiểm tra JWT/session trước khi cho upload.
- Backend kiểm tra user chỉ sửa avatar của chính mình.
- Backend validate MIME, extension, size và magic bytes.
- Không nhận SVG.
- Không lưu base64 vào DB.
```

---

## 9. Error handling

### 9.1 Google Drive token hết hạn

Khi refresh token hết hạn, backend trả lỗi rõ:

```text
GOOGLE_DRIVE_TOKEN_EXPIRED
```

Frontend hiển thị:

```text
Google Drive token đã hết hạn. Vui lòng liên hệ người phụ trách cấu hình để kết nối lại.
```

Trong dev Testing, refresh token có thể hết hạn sau 7 ngày. Người phụ trách chỉ cần chạy lại:

```text
GET /api/google-drive/oauth/connect
```

để lấy token mới rồi cập nhật `appsettings.Development.json`.

### 9.2 Upload Drive thành công, DB lỗi

Backend cố gắng xóa file Google Drive vừa upload để tránh file rác.

### 9.3 DB update user thành công nhưng file content không xem được

Kiểm tra:

```text
- files.external_file_id đúng chưa.
- RefreshToken còn hạn không.
- Endpoint /api/files/{fileId}/content có download được Drive stream không.
- users.avatar_url có đúng /api/files/{fileId}/content không.
```

---

## 10. Manual test checklist

Backend:

```text
[ ] Build backend thành công.
[ ] PUT /api/profiles/me/avatar yêu cầu đăng nhập.
[ ] Upload JPG nhỏ hơn 2MB thành công.
[ ] Upload PNG nhỏ hơn 2MB thành công.
[ ] Upload WEBP nhỏ hơn 2MB thành công.
[ ] Upload PDF bị chặn.
[ ] Upload SVG bị chặn.
[ ] Upload file >2MB bị chặn.
[ ] Upload file đổi extension giả bị chặn nếu có magic bytes check.
[ ] Google Drive folder avatars có file mới.
[ ] Bảng files có row mới.
[ ] users.avatar_url được update.
[ ] GET /api/files/{fileId}/content trả ảnh đúng.
```

Frontend:

```text
[ ] Màn Profile vẫn hiển thị text profile hiện tại.
[ ] Update text profile vẫn chạy như cũ.
[ ] Click đổi ảnh mở file picker.
[ ] Chọn ảnh hợp lệ có preview.
[ ] Chọn file không hợp lệ hiện lỗi ngay.
[ ] Bấm lưu ảnh hiển thị loading.
[ ] Thành công avatar đổi ngay.
[ ] Refresh trang avatar vẫn hiển thị.
[ ] Header/sidebar avatar đổi theo nếu có.
[ ] Lỗi upload không làm mất avatar cũ.
```

Database/Drive:

```text
[ ] files.storage_provider = GOOGLE_DRIVE.
[ ] files.file_purpose = USER_AVATAR hoặc giá trị hợp lệ theo schema.
[ ] files.uploaded_by = currentUserId.
[ ] files.external_file_id có giá trị.
[ ] users.avatar_url trỏ đến backend file content endpoint.
```

---

## 11. Prompt triển khai nhanh cho AI Agent

Dán đoạn sau cho AI Agent:

```text
Hoàn thiện chức năng upload avatar cho UC-15 Update Profile trong PEMS.

Bối cảnh:
- Text profile update đã có và đang chạy, không rewrite phần text update.
- Chỉ bổ sung upload avatar.
- Backend .NET 8 Clean Architecture + MediatR + EF Core + MySQL.
- Frontend React/Vite/TypeScript.
- Google Drive OAuthUser đã được cấu hình trong appsettings.Development.json.
- Không commit secret.

Yêu cầu backend:
1. Thêm endpoint PUT /api/profiles/me/avatar nhận multipart/form-data field "avatar".
2. Controller chỉ gọi MediatR, không query DbContext trong controller.
3. Thêm UploadProfileAvatarCommand/Handler/Validator/Response.
4. Validate:
   - required file
   - max 2MB
   - image/jpeg, image/png, image/webp
   - extension .jpg/.jpeg/.png/.webp
   - chặn svg
   - check magic bytes nếu có thể
5. Handler:
   - lấy currentUserId từ auth/current user context
   - chỉ update avatar của chính current user
   - user phải tồn tại và ACTIVE
   - upload file lên Google Drive AvatarFolderId
   - insert metadata vào files với storage_provider=GOOGLE_DRIVE, file_purpose=USER_AVATAR hoặc enum hợp lệ
   - update users.avatar_url = "/api/files/{fileId}/content"
   - nếu DB save lỗi sau khi upload Drive, cố gắng delete file Drive vừa upload
6. Nếu chưa có, thêm GoogleDriveStorageService để upload/download/delete file từ Drive bằng RefreshToken.
7. Nếu dùng avatar_url dạng /api/files/{fileId}/content, thêm endpoint GET /api/files/{fileId}/content để proxy ảnh từ Drive.

Yêu cầu frontend:
1. Bổ sung UI đổi avatar trong màn Profile hiện tại.
2. Chọn file, validate type/size trước khi upload.
3. Preview ảnh trước khi lưu.
4. Gọi PUT /api/profiles/me/avatar bằng FormData.
5. Sau thành công, cập nhật profile state và AuthContext/currentUser avatar nếu header/sidebar dùng.
6. Hiển thị loading/error/success.
7. Không đổi role/permission/routing logic.

Test:
- Build backend.
- Build frontend.
- Test JPG/PNG/WEBP thành công.
- Test PDF/SVG/file >2MB bị chặn.
- Refresh trang avatar vẫn hiển thị.
- Kiểm tra Google Drive folder avatars, bảng files, bảng users.
```

---

## 12. Acceptance Criteria

```text
AC-01: User đã đăng nhập có thể upload ảnh JPG/PNG/WEBP làm avatar.
AC-02: User không đăng nhập không thể upload avatar.
AC-03: User không thể update avatar của user khác.
AC-04: File không hợp lệ bị chặn ở frontend và backend.
AC-05: File hợp lệ được upload vào Google Drive folder avatars.
AC-06: Metadata file được lưu vào bảng files.
AC-07: users.avatar_url được cập nhật sau khi upload thành công.
AC-08: Refresh page vẫn hiển thị avatar mới.
AC-09: Text profile update hiện tại không bị ảnh hưởng.
AC-10: Secret Google Drive không bị commit lên GitHub.
```
