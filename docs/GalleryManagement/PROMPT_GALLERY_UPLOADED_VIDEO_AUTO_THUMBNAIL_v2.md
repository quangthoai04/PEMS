# PROMPT / ĐẶC TẢ CODE — Tự động tạo Thumbnail cho Video Upload từ máy trong Gallery Item

> Tài liệu này dùng cho AI Agent đọc và triển khai trên project PEMS.
>
> Mục tiêu:
>
> 1. Khi Staff Leader upload video từ máy vào Gallery Item, backend tự động tạo ảnh thumbnail từ video.
> 2. Thumbnail được upload qua file pipeline hiện tại và lưu vào `gallery_item_media.thumbnail_file_id`.
> 3. Public Gallery và màn quản lý Gallery luôn hiển thị thumbnail cho video upload.
> 4. Video cũ thiếu thumbnail phải có phương án backfill.
> 5. Nếu thumbnail thiếu hoặc lỗi, frontend phải hiển thị placeholder thay vì ô trống.
>
> AI Agent phải đọc source code và database thật trước khi sửa. Không mock data. Không sinh file rác. Không phá flow Gallery, Google Drive, YouTube embed hoặc EverAI TTS hiện tại.

---

# 0. Bối cảnh hiện tại

Module Gallery hiện hỗ trợ:

```text
- Upload ảnh từ máy.
- Upload video từ máy.
- Thêm video YouTube bằng URL.
- Một Gallery Item có nhiều media.
- Mỗi Gallery Item có đúng 1 primary media.
- media_type = IMAGE hoặc VIDEO.
- media_kind = IMAGE, VIDEO hoặc MIXED.
```

Vấn đề hiện tại:

```text
Khi video upload từ máy được chọn làm primary media:
- file_id trỏ tới file video.
- thumbnail_file_id đang NULL hoặc chưa được map.
- Frontend không có ảnh để render thumbnail.
- Thumbnail item bị trống hoặc chỉ còn icon Play.
```

Database hiện đã có:

```text
gallery_item_media.thumbnail_file_id
```

Vì vậy không cần tạo bảng mới.

---

# 1. Mục tiêu kiến trúc

Luồng mới cho video upload:

```text
Staff Leader chọn video từ máy
→ Backend validate video
→ Upload video qua IFileUploadService
→ Backend dùng FFmpeg lấy frame đại diện
→ Tạo ảnh thumbnail JPG/WEBP
→ Upload thumbnail qua IFileUploadService
→ Tạo bản ghi files cho thumbnail
→ gallery_item_media.file_id = file video
→ gallery_item_media.thumbnail_file_id = file thumbnail
→ Public/Management DTO trả thumbnailUrl
→ Frontend render thumbnail + icon Play
```

Quan hệ cuối cùng:

```text
gallery_item_media
├── file_id               → file video
├── thumbnail_file_id     → file ảnh thumbnail
├── media_type            → VIDEO
├── is_primary
├── display_order
└── status
```

---

# 2. Quy tắc bắt buộc

```text
KHÔNG dùng URL video làm src của thẻ <img>.
KHÔNG cố render MP4/WEBM trực tiếp thành thumbnail.
KHÔNG tạo thumbnail trong Public GET endpoint.
KHÔNG để frontend tự cắt frame video bằng canvas làm nguồn chuẩn.
KHÔNG bỏ qua thumbnail_file_id đã có trong database.
KHÔNG upload thumbnail trực tiếp lên Google Drive từ FFmpeg service.
KHÔNG hard-code Google Drive folder ID trong handler.
KHÔNG tạo thumbnail lại mỗi lần edit metadata nếu video không đổi.
KHÔNG để một media VIDEO ACTIVE mới được lưu với thumbnail_file_id NULL nếu flow yêu cầu thumbnail bắt buộc.
KHÔNG xóa hoặc thay đổi flow YouTube thumbnail.
KHÔNG thay đổi media_kind hoặc item_type.
KHÔNG thay đổi rule mỗi Gallery Item có đúng 1 primary media.
```

---

# 3. Source và database phải đọc trước khi code

AI Agent phải kiểm tra chính xác các file/table hiện tại.

## 3.1. Database

Kiểm tra:

```text
CREATE TABLE files
CREATE TABLE gallery_items
CREATE TABLE gallery_item_media
```

Xác nhận:

```text
- gallery_item_media.thumbnail_file_id có tồn tại.
- FK thumbnail_file_id → files.file_id.
- Nullability của thumbnail_file_id.
- Unique constraint liên quan file_id/thumbnail_file_id.
- files.file_size có nullable hay không.
- files.storage_provider.
- files.file_purpose.
- soft-delete columns.
- status enum.
```

Tìm trong SQL:

```bash
grep -n "CREATE TABLE.*files" <database-file>.sql
grep -n "CREATE TABLE.*gallery_item_media" <database-file>.sql
grep -n "thumbnail_file_id" <database-file>.sql
grep -n "file_purpose" <database-file>.sql
```

## 3.2. Backend

Đọc:

```text
backend/PEMS.Application/Common/Files/FilePurpose.cs
backend/PEMS.Application/Common/Files/FileValidationPolicy.cs
backend/PEMS.Application/Common/Files/FileUploadService.cs
backend/PEMS.Application/Common/Interfaces/IFileUploadService.cs
backend/PEMS.Application/Common/Interfaces/IFileStorageFolderResolver.cs

backend/PEMS.Domain/Entities/File.cs
backend/PEMS.Domain/Entities/GalleryItemMedia.cs

backend/PEMS.Infrastructure/Persistence/Configurations/FileConfiguration.cs
backend/PEMS.Infrastructure/Persistence/Configurations/GalleryItemMediaConfiguration.cs

backend/PEMS.Application/Galleries/**
backend/PEMS.Api/Controllers/GalleriesController.cs
backend/PEMS.Api/Controllers/PublicVisitFptuController.cs
```

Tìm:

```bash
grep -R "thumbnail_file_id\|ThumbnailFileId\|thumbnailUrl" -n backend
grep -R "UploadBusinessFileAsync" -n backend/PEMS.Application/Galleries
grep -R "GalleryItemMedia" -n backend/PEMS.Application/Galleries
grep -R "mediaType.*VIDEO\|MediaType.*Video" -n backend
```

## 3.3. Frontend

Tìm:

```bash
grep -R "thumbnailUrl\|thumbnailFileId" -n frontend/pems-react/src
grep -R "mediaType.*VIDEO\|sourceType.*UPLOADED_FILE" -n frontend/pems-react/src
grep -R "Play" -n frontend/pems-react/src/features
```

Phải xác định:

```text
- UI create Gallery Item.
- UI edit Gallery Item.
- Gallery media card.
- Public thumbnail list.
- Primary media renderer.
- Helper tạo file URL.
```

---

# 4. Thêm FilePurpose cho thumbnail video

Thêm constant/backend mapping:

```text
GALLERY_VIDEO_THUMBNAIL
```

Tên C# đề xuất:

```csharp
public const string GalleryVideoThumbnail = "GALLERY_VIDEO_THUMBNAIL";
```

Không dùng chung purpose video gốc nếu project đang phân biệt rõ loại file.

Folder storage:

```text
Phương án mặc định:
- Dùng cùng Gallery Item folder hiện tại.
- Khác file_purpose.

Phương án tách riêng:
- Chỉ dùng khi project đã có convention cấu hình folder riêng.
```

Không bắt buộc thêm `GalleryVideoThumbnailFolderId` nếu không cần.

---

# 5. Video Thumbnail Service

## 5.1. Application interface

```csharp
public interface IVideoThumbnailService
{
    Task<GeneratedVideoThumbnail> GenerateAsync(
        Stream videoStream,
        string originalFileName,
        CancellationToken cancellationToken);
}
```

Result đề xuất:

```csharp
public sealed record GeneratedVideoThumbnail(
    Stream Content,
    string FileName,
    string ContentType,
    long Size);
```

## 5.2. Infrastructure implementation

Đặt implementation tại:

```text
backend/PEMS.Infrastructure/Media/FfmpegVideoThumbnailService.cs
```

Nhiệm vụ:

```text
1. Nhận stream video.
2. Ghi video vào file tạm nếu FFmpeg cần path.
3. Chọn thời điểm lấy frame.
4. Gọi FFmpeg.
5. Resize frame.
6. Xuất JPG hoặc WEBP.
7. Trả stream/file thumbnail.
8. Xóa toàn bộ temp file trong finally.
```

---

# 6. FFmpeg

## 6.1. Runtime dependency

Backend host/container phải có FFmpeg.

Cập nhật:

```text
- Dockerfile.
- README/deployment docs.
- Health/config verification nếu project có.
```

## 6.2. Lệnh tham khảo

```bash
ffmpeg \
  -ss 00:00:01 \
  -i input.mp4 \
  -frames:v 1 \
  -vf "scale=640:-2" \
  -q:v 3 \
  -y output.jpg
```

Không lấy frame đúng `00:00:00`.

## 6.3. Chọn frame

```text
1. Ưu tiên giây thứ 1.
2. Nếu video ngắn hơn 1 giây, lấy khoảng 10% duration.
3. Nếu không đọc được duration, fallback khoảng 0.1–0.5 giây.
```

## 6.4. Kích thước

```text
Width: khoảng 640 px
Height: tự động theo aspect ratio
Format: JPEG
Quality: cân bằng
```

---

# 7. Validation và error handling

Giữ validation video hiện tại:

```text
- Extension.
- MIME type.
- File size.
- Empty file.
```

Sau FFmpeg kiểm tra:

```text
- File thumbnail tồn tại.
- Size > 0.
- MIME image/jpeg hoặc image/webp.
- Kích thước hợp lệ.
```

Error codes đề xuất:

```text
GALLERY_VIDEO_THUMBNAIL_GENERATION_FAILED
GALLERY_VIDEO_THUMBNAIL_EMPTY
GALLERY_VIDEO_FORMAT_UNSUPPORTED
GALLERY_VIDEO_FFMPEG_NOT_AVAILABLE
GALLERY_VIDEO_TEMP_FILE_FAILED
```

Không trả raw process output hoặc stack trace cho frontend.

---

# 8. Upload thumbnail qua file pipeline hiện tại

FFmpeg service chỉ tạo ảnh.

Upload phải đi qua:

```text
IFileUploadService
→ FileUploadService
→ Folder resolver
→ GoogleDriveStorageService
→ files metadata
```

Không gọi Google Drive trong `FfmpegVideoThumbnailService`.

Luồng:

```text
1. Upload video.
2. Generate thumbnail.
3. Upload thumbnail với FilePurpose.GalleryVideoThumbnail.
4. Nhận videoFileId.
5. Nhận thumbnailFileId.
6. Tạo GalleryItemMedia.
```

---

# 9. Create Gallery Item

## 9.1. Ảnh

```text
file_id = image file
thumbnail_file_id = NULL
media_type = IMAGE
```

Frontend dùng `fileUrl` làm thumbnail.

## 9.2. Video upload

```text
file_id = video file
thumbnail_file_id = generated thumbnail file
media_type = VIDEO
```

Luồng:

```text
Upload video
→ mở stream video
→ generate thumbnail
→ upload thumbnail
→ tạo media row
```

Phải điều chỉnh theo signature thật của `IFileUploadService`.

---

# 10. Transaction và cleanup

Nếu:

```text
- Video upload thành công.
- Thumbnail generation/upload thất bại.
```

thì không tạo `gallery_item_media` thiếu thumbnail.

Cần:

```text
- Throw lỗi.
- Rollback DB.
- Cleanup external file nếu project đã có compensating cleanup.
- Nếu chưa có cleanup, ghi rõ technical limitation.
```

Temp files phải xóa trong `finally`.

---

# 11. Update Gallery Item

## 11.1. Thêm video mới

```text
Upload video
→ generate thumbnail
→ upload thumbnail
→ create media row
```

## 11.2. Giữ video cũ

```text
- Giữ file_id.
- Giữ thumbnail_file_id.
- Không generate lại.
```

## 11.3. Xóa video

Theo convention hiện tại:

```text
- Soft-delete media row hoặc remove relation.
- Xử lý file video và thumbnail cùng policy.
- Không xóa cứng nếu hệ thống dùng retention/audit.
```

## 11.4. Thay video

```text
- Tạo thumbnail mới.
- Không dùng thumbnail của video cũ.
```

## 11.5. Chỉ edit metadata

```text
- Không regenerate thumbnail.
```

---

# 12. Primary media

Giữ rule:

```text
Mỗi Gallery Item có đúng 1 primary media.
```

Khi primary là uploaded video:

```text
- Thumbnail list dùng thumbnail_file_id.
- Không dùng video file URL làm ảnh.
```

Primary vẫn hỗ trợ:

```text
- Uploaded image.
- Uploaded video.
- YouTube video.
```

---

# 13. DTO mapping

Uploaded video:

```json
{
  "mediaId": 10,
  "fileId": 5001,
  "mediaType": "VIDEO",
  "sourceType": "UPLOADED_FILE",
  "fileUrl": "/api/files/5001/content",
  "thumbnailFileId": 5002,
  "thumbnailUrl": "/api/files/5002/content",
  "isPrimary": true,
  "displayOrder": 0,
  "status": "ACTIVE"
}
```

Ảnh:

```json
{
  "mediaId": 11,
  "fileId": 5003,
  "mediaType": "IMAGE",
  "sourceType": "UPLOADED_FILE",
  "fileUrl": "/api/files/5003/content",
  "thumbnailFileId": null,
  "thumbnailUrl": "/api/files/5003/content",
  "isPrimary": false
}
```

YouTube giữ flow riêng và không chạy FFmpeg.

---

# 14. Frontend helper thumbnail

```ts
export function getGalleryMediaThumbnailUrl(
  media: GalleryMedia,
): string | null {
  if (media.sourceType === 'YOUTUBE') {
    return media.thumbnailUrl ?? null;
  }

  if (media.mediaType === 'VIDEO') {
    return media.thumbnailUrl ?? null;
  }

  return media.thumbnailUrl ?? media.fileUrl ?? null;
}
```

Không dùng:

```tsx
<img src={media.fileUrl} />
```

cho uploaded video.

---

# 15. Frontend renderer và fallback

```tsx
const thumbnailUrl = getGalleryMediaThumbnailUrl(media);

<div className="relative h-full w-full overflow-hidden rounded-xl bg-black/60">
  {thumbnailUrl ? (
    <img
      src={thumbnailUrl}
      alt={media.altText || media.title || 'Video thumbnail'}
      className="h-full w-full object-cover"
      onError={() => handleThumbnailError(media.mediaId)}
    />
  ) : (
    <div className="flex h-full w-full items-center justify-center bg-slate-900/80">
      <Video className="h-8 w-8 text-white/75" />
    </div>
  )}

  {media.mediaType === 'VIDEO' && (
    <div className="absolute inset-0 flex items-center justify-center">
      <div className="flex h-10 w-10 items-center justify-center rounded-full bg-black/65 text-white">
        <Play className="h-5 w-5 fill-current" />
      </div>
    </div>
  )}
</div>
```

Fallback bắt buộc khi:

```text
- thumbnailUrl NULL.
- thumbnail request 404.
- thumbnail file corrupt.
- data cũ chưa backfill.
```

Không để ô trống hoặc broken image.

---

# 16. Optional manual thumbnail upload

Có thể bổ sung sau:

```text
Ảnh đại diện video — không bắt buộc
[Chọn ảnh]
```

Rule:

```text
Có thumbnail user upload
→ dùng ảnh đó.

Không có
→ backend generate bằng FFmpeg.
```

Đây là optional, không bắt buộc nếu chỉ cần auto thumbnail.

---

# 17. Backfill video cũ

Query tìm video thiếu thumbnail:

```sql
SELECT
    gim.media_id,
    gim.gallery_item_id,
    gim.file_id
FROM gallery_item_media gim
WHERE gim.media_type = 'VIDEO'
  AND gim.status = 'ACTIVE'
  AND gim.deleted_at IS NULL
  AND gim.thumbnail_file_id IS NULL;
```

Không backfill trong Public GET.

Phương án:

```text
1. Internal command/manual endpoint.
2. Console maintenance tool.
3. Background maintenance job một lần.
```

Luồng:

```text
Query video thiếu thumbnail
→ tải video từ storage
→ generate thumbnail
→ upload thumbnail
→ update thumbnail_file_id
```

Backfill phải idempotent:

```text
- Chỉ xử lý thumbnail_file_id IS NULL.
- Không tạo lại nếu đã có thumbnail.
- Có log success/failure theo media_id.
- Chạy lại an toàn.
```

---

# 18. Logging và security

Log:

```text
- gallery_item_id.
- media_id.
- video file_id.
- thumbnail file_id.
- thời gian xử lý.
- lỗi FFmpeg đã sanitize.
```

Security:

```text
- Temp filename generate nội bộ.
- Không dùng original filename làm path.
- Chống path traversal.
- Timeout FFmpeg.
- Kill process nếu timeout.
- Giới hạn concurrency.
- Xóa temp files trong finally.
```

Không log credential hoặc binary.

---

# 19. Performance

```text
- Chỉ extract 1 frame.
- Không encode lại video.
- Thumbnail width khoảng 640 px.
- Không load toàn bộ video lớn vào RAM nếu không cần.
- Giới hạn số FFmpeg process đồng thời.
```

---

# 20. Kiểm tra điều kiện runtime của FFmpeg

FFmpeg chỉ là một **runtime prerequisite** nếu phương án triển khai dùng executable `ffmpeg` để cắt frame từ video.

AI Agent phải kiểm tra môi trường chạy backend hiện tại trước khi sửa cấu hình triển khai:

```text
- Nếu máy local/server đã có FFmpeg:
  không cần thay đổi deployment.

- Nếu backend chạy bằng Docker:
  chỉ kiểm tra Docker image đã có FFmpeg hay chưa.
  Chỉ sửa Dockerfile khi project thực sự deploy bằng Docker
  và image hiện tại chưa có FFmpeg.

- Nếu backend chạy trực tiếp trên server:
  chỉ cần bảo đảm server có executable FFmpeg trong PATH
  hoặc có đường dẫn cấu hình rõ ràng.

- Nếu project chọn giải pháp thumbnail không dùng FFmpeg binary:
  bỏ qua toàn bộ phần prerequisite này.
```

Không được tự ý:

```text
- Chuyển project sang Docker.
- Sửa Dockerfile khi project không dùng Docker.
- Thay đổi pipeline deploy ngoài phạm vi chỉ để “cho chắc”.
- Hard-code đường dẫn FFmpeg theo một máy cụ thể.
```

Backend nên kiểm tra khả năng gọi FFmpeg và trả lỗi cấu hình rõ ràng khi executable không tồn tại:

```text
GALLERY_VIDEO_FFMPEG_NOT_AVAILABLE
```

Nếu cần cấu hình đường dẫn:

```text
Ffmpeg:ExecutablePath
```

Giá trị mặc định có thể là:

```text
ffmpeg
```

để dùng executable trong `PATH`.

Ví dụ cài FFmpeg trong Docker chỉ là **tham khảo có điều kiện**, không phải yêu cầu bắt buộc:

```dockerfile
RUN apt-get update \
    && apt-get install -y --no-install-recommends ffmpeg \
    && rm -rf /var/lib/apt/lists/*
```

Chỉ áp dụng đoạn trên khi:

```text
- Backend thực sự chạy bằng Docker.
- Base image hiện tại chưa có FFmpeg.
- Team chấp thuận thay đổi Dockerfile.
```

---

# 21. Acceptance Criteria

## AC-THUMB-01

```text
Given Staff Leader upload video từ máy
When tạo Gallery Item thành công
Then thumbnail được generate
And upload vào storage
And thumbnail_file_id không NULL.
```

## AC-THUMB-02

```text
Given uploaded video là primary
When Public Gallery render thumbnail list
Then thumbnail image hiển thị
And icon Play nằm phía trên
And ô không bị trống.
```

## AC-THUMB-03

```text
Given media là IMAGE
Then frontend dùng image file làm thumbnail.
```

## AC-THUMB-04

```text
Given media là YouTube
Then dùng thumbnail YouTube
And không chạy FFmpeg.
```

## AC-THUMB-05

```text
Given video cũ đã có thumbnail
When chỉ edit metadata
Then thumbnail_file_id không đổi
And không regenerate.
```

## AC-THUMB-06

```text
Given thêm video mới trong edit
Then thumbnail mới được generate và upload.
```

## AC-THUMB-07

```text
Given FFmpeg không tạo được thumbnail
When submit
Then backend trả lỗi rõ ràng
And không tạo media VIDEO active thiếu thumbnail.
```

## AC-THUMB-08

```text
Given media cũ thiếu hoặc lỗi thumbnail
When frontend render
Then hiển thị placeholder video
And không hiển thị ô trống.
```

## AC-THUMB-09

```text
Given media VIDEO cũ có thumbnail_file_id NULL
When chạy backfill
Then thumbnail_file_id được cập nhật
And chạy lại không tạo duplicate.
```

---

# 22. Test bắt buộc

Backend:

```text
1. Upload MP4.
2. Upload WEBM nếu hỗ trợ.
3. Video ngắn.
4. Video dài.
5. Frame đầu đen.
6. File corrupt.
7. MIME giả.
8. FFmpeg không tồn tại.
9. FFmpeg timeout.
10. Thumbnail output empty.
11. Upload thumbnail storage lỗi.
12. Create chỉ video.
13. Create ảnh + video.
14. Create nhiều video.
15. Edit thêm video.
16. Edit giữ video.
17. Edit xóa video.
18. Edit metadata.
19. Primary là video.
20. Transaction failure.
21. Temp cleanup.
```

Frontend:

```text
1. Uploaded video có thumbnail.
2. Uploaded video primary.
3. Thumbnail URL lỗi.
4. Thumbnail URL null.
5. Placeholder video.
6. Icon Play.
7. Image vẫn đúng.
8. YouTube vẫn đúng.
9. Public thumbnail list.
10. Management media card.
11. Responsive.
```

Backfill:

```text
1. Một video thiếu thumbnail.
2. Nhiều video thiếu thumbnail.
3. Video đã có thumbnail.
4. Video file không tồn tại.
5. Chạy lại.
6. Partial failure.
```

---

# 23. Build và báo cáo

Chạy:

```bash
dotnet build
npm run build
```

Nếu có test:

```bash
dotnet test
npm test
```

Báo cáo:

```text
1. Database có sửa hay không.
2. FilePurpose đã thêm.
3. Thumbnail service files.
4. FFmpeg dependency.
5. Create handler changed.
6. Update handler changed.
7. DTO mapping changed.
8. Frontend helper/components changed.
9. Backfill tool/endpoint/job.
10. Backend build.
11. Frontend build.
12. Test result.
13. Deployment requirement.
14. Limitation chưa xác nhận runtime.
```

---

# 24. Definition of Done

```text
[ ] Dùng thumbnail_file_id hiện có.
[ ] Không tạo bảng mới.
[ ] Thêm FilePurpose thumbnail.
[ ] FFmpeg service hoạt động.
[ ] Temp files cleanup.
[ ] Video upload mới có thumbnail_file_id.
[ ] Video primary hiển thị thumbnail.
[ ] Edit metadata không regenerate.
[ ] Video mới trong edit có thumbnail.
[ ] DTO trả thumbnailUrl.
[ ] Frontend dùng thumbnailUrl cho video.
[ ] Frontend có placeholder.
[ ] YouTube không bị ảnh hưởng.
[ ] Có backfill video cũ.
[ ] Backfill idempotent.
[ ] Runtime chạy thumbnail đã có FFmpeg hoặc có giải pháp thay thế tương đương.
[ ] Không sửa Dockerfile/deployment nếu project không cần.
[ ] Backend build thành công.
[ ] Frontend build thành công.
[ ] Không mock data.
[ ] Không sinh file rác.
