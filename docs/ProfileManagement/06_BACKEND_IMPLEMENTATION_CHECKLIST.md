# Backend Implementation Checklist — Profile UC

## 1. Controllers

### ProfilesController

Endpoints:

```http
GET /api/profile/me
PATCH /api/profile/me
POST /api/profile/me/avatar
```

Rules:

```text
- Controller không chứa business logic phức tạp.
- Controller lấy request và gọi Mediator/Application service.
- Không nhận userId cho self-service profile.
```

### FilesController

Endpoint:

```http
GET /api/files/{fileId}/preview
```

Rules:

```text
- Xác thực user.
- Kiểm tra quyền xem file.
- Stream file từ Google Drive.
```

## 2. Application layer

Tạo/cập nhật:

```text
Profiles/Queries/ViewMyProfile
- ViewMyProfileQuery
- ViewMyProfileQueryHandler
- ViewProfileResponse

Profiles/Commands/UpdateMyProfile
- UpdateMyProfileCommand
- UpdateMyProfileCommandHandler
- UpdateMyProfileCommandValidator
- UpdateProfileResponse

Profiles/Commands/UploadMyAvatar
- UploadMyAvatarCommand
- UploadMyAvatarCommandHandler
- UploadMyAvatarCommandValidator
- UploadAvatarResponse

Files/Queries/PreviewFile
- PreviewFileQuery
- PreviewFileQueryHandler
```

## 3. Domain/constants

```csharp
public static class RoleCodes
{
    public const string Admin = "ADMIN";
    public const string HO = "HO";
    public const string Staff = "STAFF";
    public const string Department = "DEPARTMENT";
    public const string Student = "STUDENT";
    public const string Visitor = "VISITOR";
}

public static class SubRoles
{
    public const string Leader = "LEADER";
    public const string Staff = "STAFF";
}

public static class FilePurposes
{
    public const string UserAvatar = "USER_AVATAR";
    public const string VisitDocument = "VISIT_DOCUMENT";
    public const string VisitPhoto = "VISIT_PHOTO";
    public const string PartnerDocument = "PARTNER_DOCUMENT";
    public const string MinutesAttachment = "MINUTES_ATTACHMENT";
    public const string NewsMedia = "NEWS_MEDIA";
    public const string GalleryMedia = "GALLERY_MEDIA";
    public const string Temp = "TEMP";
}
```

## 4. ViewMyProfileHandler rules

```text
- Lấy currentUserId từ ICurrentUserService.
- Query users + roles + campuses + departments.
- Không trả password_hash/security fields.
- Build displayRole.
- Build displayPosition:
  LEADER => Trưởng phòng
  STAFF  => Nhân viên
- Build displayCampusName từ campus.name.
- Không hardcode ADMIN = Hà Nội.
- Không hardcode HO = Toàn quốc.
- VISITOR không cần campus.
```

## 5. UpdateMyProfileHandler rules

```text
- Lấy currentUserId từ ICurrentUserService.
- Reject payload chứa field cấm.
- Validate fullName, phone, gender, nationality.
- nationality chỉ cho VISITOR.
- Update users.full_name, users.gender, users.phone, users.nationality nếu hợp lệ.
- Set updated_at = now.
- Set updated_by = currentUserId.
- Return updated profile.
```

## 6. UploadMyAvatarHandler rules

```text
- Lấy currentUserId từ ICurrentUserService.
- Validate file.
- Tính checksum_sha256.
- Tạo object_key unique.
- Upload lên Google Drive folder USER_AVATAR.
- Insert files metadata.
- Update users.avatar_url = /api/files/{fileId}/preview.
- Set users.updated_at, users.updated_by.
- Return avatarUrl and fileId.
```

## 7. Files table insert fields

```text
storage_provider = GOOGLE_DRIVE
bucket_name = profile-avatars folder ID
object_key = profile-avatars/{userId}/{yyyyMMddHHmmss}-{uuid}-{safeFileName}
original_filename = safe original filename
mime_type = image/png | image/jpeg | image/webp
file_size = file size
checksum_sha256 = sha256 hash
uploaded_by = currentUserId
external_file_id = Google Drive file ID
web_view_url = Google Drive view URL if available
download_url = null or available URL
thumbnail_url = null or available URL
file_purpose = USER_AVATAR
```

## 8. GoogleDriveFileStorageService

Responsibilities:

```text
- Read GoogleDrive options from config.
- Initialize DriveService using service account JSON.
- Pick folder ID by filePurpose.
- Upload file stream.
- Return external_file_id, web_view_url, etc.
- Open/read file stream for preview.
- Delete uploaded file if DB transaction fails after Drive upload.
```

## 9. Transaction handling

```text
Recommended order:
1. Validate input.
2. Upload to Google Drive.
3. Begin DB transaction.
4. Insert files.
5. Update users.avatar_url.
6. Commit DB transaction.
7. If DB fails after Drive upload, attempt delete Drive file.
```

Do not update `users.avatar_url` before `files` insert succeeds.

## 10. PreviewFileHandler rules

```text
- Query files by fileId.
- If not found -> 404.
- Authorize.
- Use external_file_id to read from Google Drive.
- Return stream with files.mime_type.
```

## 11. Security rules

```text
- Do not expose Google Drive service account JSON.
- Do not log private key.
- Do not return Google Drive direct link if not needed.
- Do not allow arbitrary fileId preview without authorization.
- Do not trust frontend MIME/extension only.
- Sanitize original_filename.
```

## 12. Build verification

```bash
dotnet build
```

Manual verification:

```text
- Login as VISITOR, STAFF, DEPARTMENT, STUDENT, ADMIN, HO.
- GET /api/profile/me returns correct role-based data.
- PATCH profile text updates only allowed fields.
- Upload avatar creates files record and updates users.avatar_url.
- Preview avatar works through backend URL.
```
