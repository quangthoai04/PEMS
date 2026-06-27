# PEMS — Bổ sung `checksum_sha256` cho luồng Upload Avatar Profile

> File này dùng cho AI Agent đọc và code bổ sung phần tính/lưu `checksum_sha256` khi upload ảnh avatar trong UC-15 Update Profile.  
> Bối cảnh: upload avatar lên Google Drive đã chạy thành công; `users.avatar_url` đã lưu dạng `/api/files/{fileId}/content`; bảng `files` đã có cột `checksum_sha256` nhưng hiện đang `NULL`.

---

## 1. Mục tiêu

Bổ sung tính SHA-256 checksum cho file avatar khi upload.

Luồng sau khi sửa:

```text
User chọn ảnh avatar
→ Backend validate file
→ Backend copy/read file stream an toàn
→ Backend tính SHA-256 checksum từ nội dung file
→ Backend reset stream về đầu
→ Backend upload file lên Google Drive
→ Backend insert metadata vào bảng files, bao gồm checksum_sha256
→ Backend update users.avatar_url = /api/files/{fileId}/content
→ Frontend hiển thị avatar mới
```

Yêu cầu chính:

```text
- Không sửa database vì cột checksum_sha256 đã tồn tại.
- Không sửa luồng update text profile hiện tại.
- Không nhận checksum từ frontend.
- Không dùng filename, Google Drive file id, webViewUrl để tính checksum.
- Chỉ tính checksum từ binary content của file.
```

---

## 2. Ý nghĩa của `checksum_sha256`

`checksum_sha256` là mã băm SHA-256 của nội dung file.

Nó dùng để:

```text
1. Kiểm tra file có bị thay đổi hoặc hỏng không.
2. Truy vết chính xác nội dung file.
3. Phát hiện file upload trùng nếu sau này cần.
4. Audit dữ liệu file.
5. So sánh hai file có cùng nội dung dù khác tên file.
```

Ví dụ:

```text
avatar.jpg
my-profile-picture.jpg
```

Nếu 2 file có nội dung binary giống hệt nhau thì `checksum_sha256` sẽ giống nhau.

---

## 3. Dữ liệu mong muốn sau khi upload

Sau khi upload avatar thành công, bảng `files` cần có:

```text
file_id          = auto increment
storage_provider = GOOGLE_DRIVE
external_file_id = Google Drive file id
mime_type        = image/jpeg | image/png | image/webp
file_size        > 0
checksum_sha256  = chuỗi SHA-256 hex lowercase dài 64 ký tự
uploaded_by      = currentUserId
file_purpose     = USER_AVATAR hoặc giá trị avatar hợp lệ theo schema hiện tại
```

Ví dụ checksum hợp lệ:

```text
a3f5c2e9d9f2b8d3e4f1a6c7b8e9f0d1c2a3b4c5d6e7f8091a2b3c4d5e6f7081
```

Đặc điểm:

```text
- 64 ký tự
- lowercase
- chỉ gồm 0-9 và a-f
```

---

## 4. Nguyên tắc tính checksum

Đúng:

```text
checksum_sha256 = SHA256(file binary content)
```

Sai:

```text
checksum_sha256 = SHA256(original_filename)
checksum_sha256 = SHA256(external_file_id)
checksum_sha256 = SHA256(web_view_url)
checksum_sha256 = SHA256(download_url)
```

Frontend không gửi checksum. Backend tự tính để đảm bảo tin cậy.

---

## 5. Vấn đề cần cẩn thận với Stream

Khi backend đọc stream để tính checksum, vị trí stream sẽ nằm ở cuối file.

Nếu sau đó dùng chính stream đó upload Google Drive mà không reset lại vị trí, file upload có thể bị:

```text
- rỗng
- thiếu nội dung
- lỗi upload
```

Vì vậy sau khi tính checksum, phải reset stream về đầu:

```csharp
stream.Position = 0;
```

Nếu stream không hỗ trợ seek, nên copy sang `MemoryStream`.

Vì avatar chỉ giới hạn 2MB, khuyến nghị dùng `MemoryStream` cho đơn giản và an toàn.

---

## 6. Cách triển khai khuyến nghị

### 6.1 Tạo helper tính SHA-256

Tạo helper dùng chung, ví dụ:

```text
backend/PEMS.Application/Common/Files/FileChecksumHelper.cs
```

Hoặc đặt theo cấu trúc hiện tại của project nếu đã có thư mục helper/utility cho file.

Code gợi ý:

```csharp
using System.Security.Cryptography;

public static class FileChecksumHelper
{
    public static async Task<string> ComputeSha256HexAsync(
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

Kết quả trả về phải là SHA-256 hex lowercase dài 64 ký tự.

---

### 6.2 Sửa `UploadProfileAvatarCommandHandler`

Tìm handler đang upload avatar, ví dụ:

```text
UploadProfileAvatarCommandHandler
```

Hiện tại handler có thể đang làm:

```text
1. Validate user
2. Validate file
3. Upload Google Drive
4. Insert files
5. Update users.avatar_url
```

Cần sửa thành:

```text
1. Validate user
2. Validate file
3. Copy file stream vào MemoryStream
4. Tính checksum_sha256 từ MemoryStream
5. Reset MemoryStream về đầu
6. Upload Google Drive bằng MemoryStream
7. Insert files, bao gồm checksum_sha256
8. Update users.avatar_url
9. SaveChanges
```

Pseudo code:

```csharp
await using var memoryStream = new MemoryStream();
await request.FileStream.CopyToAsync(memoryStream, cancellationToken);

if (memoryStream.Length <= 0)
{
    throw new ValidationException("Vui lòng chọn ảnh đại diện.");
}

memoryStream.Position = 0;
var checksumSha256 = await FileChecksumHelper.ComputeSha256HexAsync(
    memoryStream,
    cancellationToken);

if (checksumSha256.Length != 64)
{
    throw new InvalidOperationException("Invalid SHA-256 checksum length.");
}

memoryStream.Position = 0;
var uploadResult = await _fileStorageService.UploadAsync(
    memoryStream,
    objectKey,
    request.ContentType,
    avatarFolderId,
    cancellationToken);
```

Khi tạo entity `files`, set thêm:

```csharp
ChecksumSha256 = checksumSha256
```

Ví dụ:

```csharp
var file = new FileEntity
{
    StorageProvider = "GOOGLE_DRIVE",
    ObjectKey = objectKey,
    OriginalFilename = sanitizedOriginalFileName,
    MimeType = request.ContentType,
    FileSize = memoryStream.Length,
    ChecksumSha256 = checksumSha256,
    UploadedBy = request.CurrentUserId,
    ExternalFileId = uploadResult.ExternalFileId,
    WebViewUrl = uploadResult.WebViewUrl,
    DownloadUrl = uploadResult.DownloadUrl,
    ThumbnailUrl = uploadResult.ThumbnailUrl,
    FilePurpose = "USER_AVATAR"
};
```

Tên entity/property phải khớp code thật trong project.

---

## 7. Validation liên quan

Không cần nhận checksum từ request.

Backend vẫn giữ validation file hiện tại:

```text
- File bắt buộc có.
- File size > 0.
- File size <= 2MB.
- Chỉ nhận image/jpeg, image/png, image/webp.
- Chỉ nhận extension .jpg, .jpeg, .png, .webp.
- Chặn .svg.
- Check magic bytes nếu đã có.
```

Checksum chỉ được tính sau khi file đã qua validation cơ bản.

Nếu checksum lỗi:

```text
- Không upload lên Google Drive.
- Không insert files.
- Không update users.avatar_url.
- Trả lỗi rõ: FILE_CHECKSUM_FAILED hoặc UPLOAD_AVATAR_FAILED.
```

---

## 8. Có cần chặn upload trùng không?

Không cần trong task này.

Chỉ cần tính và lưu checksum.

Không tự thêm logic chặn trùng vì cần quyết định nghiệp vụ riêng:

```text
- Cùng user upload lại avatar giống cũ có nên chặn không?
- Gallery/news/document có xử lý trùng giống avatar không?
- Nếu trùng thì dùng lại file cũ hay tạo row mới?
```

Với avatar hiện tại, cứ cho upload và lưu checksum.

---

## 9. Có cần tính checksum cho các upload khác không?

Task này chỉ yêu cầu cho:

```text
PUT /api/profiles/me/avatar
```

Nếu project đã có upload service dùng chung cho document/gallery/news/minutes, có thể viết helper dùng chung để sau tái sử dụng.

Không tự sửa lan rộng các UC khác nếu chưa được yêu cầu.

---

## 10. Error handling

### 10.1 Checksum lỗi

Nếu tính checksum lỗi:

```text
- Không upload Google Drive.
- Không insert files.
- Không update users.avatar_url.
- Trả lỗi backend rõ ràng.
```

### 10.2 Upload Drive thành công nhưng DB lỗi

Giữ hoặc bổ sung cleanup hiện tại:

```text
- Cố gắng xóa file Google Drive vừa upload.
- Log lỗi cleanup nếu xóa bù thất bại.
```

Pseudo:

```csharp
string? uploadedExternalFileId = null;

try
{
    var uploadResult = await _fileStorageService.UploadAsync(...);
    uploadedExternalFileId = uploadResult.ExternalFileId;

    // insert files + update users.avatar_url
    await _dbContext.SaveChangesAsync(cancellationToken);

    return response;
}
catch
{
    if (!string.IsNullOrWhiteSpace(uploadedExternalFileId))
    {
        try
        {
            await _fileStorageService.DeleteAsync(uploadedExternalFileId, cancellationToken);
        }
        catch
        {
            // log cleanup failure
        }
    }

    throw;
}
```

### 10.3 Upload Drive lỗi

Nếu Google Drive upload lỗi:

```text
- Không insert files.
- Không update users.avatar_url.
- Trả lỗi upload rõ ràng.
```

---

## 11. SQL kiểm tra sau khi code

Sau khi upload avatar thành công, chạy:

```sql
SELECT
    file_id,
    storage_provider,
    original_filename,
    mime_type,
    file_size,
    checksum_sha256,
    uploaded_by,
    external_file_id,
    file_purpose
FROM files
ORDER BY file_id DESC
LIMIT 5;
```

Kỳ vọng:

```text
checksum_sha256 không NULL
checksum_sha256 dài 64 ký tự
storage_provider = GOOGLE_DRIVE
external_file_id có giá trị
file_size > 0
```

Check độ dài checksum:

```sql
SELECT
    file_id,
    checksum_sha256,
    CHAR_LENGTH(checksum_sha256) AS checksum_length
FROM files
WHERE file_id = <file_id>;
```

Kỳ vọng:

```text
checksum_length = 64
```

Check file avatar mới nhất của user:

```sql
SELECT
    u.user_id,
    u.full_name,
    u.avatar_url,
    f.file_id,
    f.checksum_sha256,
    f.external_file_id,
    f.mime_type,
    f.file_size
FROM users u
LEFT JOIN files f
    ON u.avatar_url = CONCAT('/api/files/', f.file_id, '/content')
WHERE u.user_id = <user_id>;
```

---

## 12. Test checklist

### 12.1 Upload hợp lệ

```text
[ ] Upload JPG thành công, checksum_sha256 không NULL.
[ ] Upload PNG thành công, checksum_sha256 không NULL.
[ ] Upload WEBP thành công, checksum_sha256 không NULL.
[ ] checksum_sha256 dài 64 ký tự.
[ ] Avatar vẫn hiển thị được qua /api/files/{fileId}/content.
[ ] Google Drive folder avatars có file mới.
[ ] users.avatar_url trỏ tới file mới nhất.
```

### 12.2 Upload không hợp lệ

```text
[ ] Upload PDF bị chặn, không insert files.
[ ] Upload SVG bị chặn, không insert files.
[ ] Upload file >2MB bị chặn, không insert files.
[ ] Upload file giả mạo extension bị chặn nếu đã có magic bytes check.
```

### 12.3 Upload trùng ảnh

Upload cùng một ảnh 2 lần.

Kỳ vọng:

```text
[ ] Tạo 2 row files khác nhau.
[ ] checksum_sha256 giống nhau nếu nội dung ảnh y hệt.
[ ] users.avatar_url trỏ tới file upload mới nhất.
```

---

## 13. Acceptance Criteria

```text
AC-01: Upload avatar hợp lệ vẫn thành công như trước.
AC-02: Bảng files lưu checksum_sha256 sau khi upload avatar.
AC-03: checksum_sha256 là SHA-256 hex lowercase, dài 64 ký tự.
AC-04: Không nhận checksum từ frontend.
AC-05: Không dùng filename/Google Drive file id/webViewUrl để tính checksum.
AC-06: Không làm file upload lên Google Drive bị rỗng do quên reset stream.
AC-07: users.avatar_url vẫn được update đúng dạng /api/files/{fileId}/content.
AC-08: GET /api/files/{fileId}/content vẫn trả ảnh đúng.
AC-09: File không hợp lệ không được upload và không tạo checksum.
AC-10: Không sửa schema DB nếu không cần.
AC-11: Không ảnh hưởng luồng update text profile hiện tại.
```

---

## 14. Prompt ngắn cho AI Agent

```text
Bổ sung tính checksum_sha256 cho luồng upload avatar profile.

Bối cảnh:
- Upload avatar lên Google Drive đã chạy thành công.
- users.avatar_url đang lưu dạng /api/files/{fileId}/content.
- Bảng files đã có cột checksum_sha256 nhưng hiện đang NULL.
- Không sửa database.
- Không sửa luồng update text profile.
- Không nhận checksum từ frontend.

Yêu cầu:
1. Trong UploadProfileAvatarCommandHandler, trước khi upload Google Drive, tính SHA-256 từ nội dung file.
2. Lưu checksum dạng hex lowercase 64 ký tự vào files.checksum_sha256.
3. Cẩn thận stream: sau khi đọc stream để tính checksum phải reset về đầu trước khi upload.
4. Với avatar max 2MB, có thể copy vào MemoryStream để an toàn.
5. Không upload nếu file invalid.
6. Không insert files nếu checksum/upload lỗi.
7. Nếu upload Drive thành công nhưng DB save lỗi, giữ hoặc bổ sung cleanup xóa file Drive vừa upload.
8. Không dùng filename/Google file id/webViewUrl để tính checksum.
9. Không bắt frontend truyền checksum.
10. Build backend sau khi sửa.
11. Test upload JPG/PNG/WEBP và kiểm tra checksum_sha256 trong DB không còn NULL.

Kết quả mong muốn:
- Sau upload avatar thành công, bảng files có checksum_sha256 không NULL.
- CHAR_LENGTH(checksum_sha256) = 64.
- Avatar vẫn hiển thị được qua /api/files/{fileId}/content.
```
