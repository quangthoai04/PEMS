# PEMS – Kế hoạch triển khai Video đại diện cho Gallery Area

## 1. Mục tiêu

Thay đổi chức năng quản lý Gallery Area của STAFF LEADER theo hướng:

- Khi tạo **khu vực mới**, không upload ảnh đại diện khu vực nữa.
- STAFF LEADER phải upload **một video MP4** làm đại diện khu vực.
- Video được lưu trên **Google Drive** bằng nền tảng upload file dùng chung hiện tại.
- Khi người dùng mở Public Gallery và chọn khu vực, video phải:
  - Tự động phát.
  - Luôn tắt tiếng.
  - Tự động lặp lại.
  - Phát trong trang, không tự mở fullscreen trên mobile.
- Các khu vực cũ đang sử dụng ảnh vẫn tiếp tục hoạt động.
- Location Cover vẫn là ảnh như hiện tại.

Baseline đã được rà soát trên repository `quangthoai04/PEMS`, nhánh `Dev`, tại commit:

```text
12f23b93a36427ca1e5448c6d18a16b2deca7a6f
```

AI Agent phải đọc lại source mới nhất trước khi code để bảo đảm không ghi đè các thay đổi mới hơn.

---

# 2. Phạm vi

## 2.1. Trong phạm vi

- Gallery Area cover.
- Form tạo Area mới.
- Form chỉnh sửa Area cover.
- Upload video Area lên Google Drive.
- Metadata bảng `files`.
- Public Area Showcase.
- Public media authorization.
- Streaming video.
- DTO/API liên quan.
- Validation frontend/backend.
- Audit log.
- Backend/frontend tests.

## 2.2. Ngoài phạm vi

Không thay đổi:

- Location Cover.
- Gallery Item media.
- Gallery Item YouTube.
- Visit Delegation.
- Gallery TTS.
- Publish/hide Gallery Item.
- Active/inactive Area và Location.
- Campus scope và permission hiện tại.

Không triển khai:

- FFmpeg hoặc ffprobe.
- Tự động nén/chuyển codec.
- Tự động tạo thumbnail.
- HLS.
- CDN riêng.
- YouTube làm Area cover.
- Background processing.
- Phát âm thanh.
- Nhiều video cho một Area.

---

# 3. Business rule đã chốt

## 3.1. Điều kiện video

Mỗi Area chỉ có một cover chính.

Đối với Area mới:

```text
Số lượng:       1 video
Extension:      .mp4
MIME:           video/mp4
Dung lượng:     tối đa 30 MB
Thời lượng:     tối đa 60 giây
Lưu trữ:        Google Drive
File purpose:   GALLERY_AREA_COVER_VIDEO
```

Video:

- Không được rỗng.
- Không được gửi nhiều file.
- Nên là video ngang.
- Khuyến nghị H.264.
- Khuyến nghị 1280×720 hoặc 1920×1080.
- Khuyến nghị tối đa 30 FPS.
- Khuyến nghị không có audio để giảm dung lượng.

## 3.2. Public playback

Video phải được render bằng HTML5 video với:

```text
autoPlay
muted
loop
playsInline
```

Ngoài ra:

- Dùng `object-fit: cover`.
- Chỉ tải video của Area đang chọn.
- Khi đổi Area, video cũ dừng.
- Khi đóng Area Showcase, video dừng.
- Khi video chưa sẵn sàng, giữ gradient/fallback.
- Video chỉ fade-in sau `canplay`.
- Video lỗi không được làm crash Public Gallery.

## 3.3. Location Cover

Location vẫn dùng ảnh:

```text
.jpg
.jpeg
.png
.webp
```

Không cho Location upload video.

## 3.4. Khu vực cũ đang dùng ảnh

Không migrate bắt buộc.

Quy tắc:

```text
Area cover là video/mp4
→ render VIDEO.

Area cover là image/*
→ render IMAGE.
```

---

# 4. Thiết kế database

## 4.1. Tái sử dụng `gallery_areas.cover_file_id`

Không thêm:

```sql
cover_video_file_id
cover_media_type
cover_video_status
```

Tiếp tục dùng:

```sql
gallery_areas.cover_file_id
```

Trường này trỏ đến:

- Ảnh đối với Area cũ.
- Video đối với Area mới hoặc Area đã thay cover.

Loại media được suy ra từ record `files`:

```text
files.file_purpose
files.mime_type
```

Ưu tiên xác định:

```csharp
if (file.FilePurpose == "GALLERY_AREA_COVER_VIDEO"
    || file.MimeType.StartsWith("video/",
        StringComparison.OrdinalIgnoreCase))
{
    coverMediaType = "VIDEO";
}
else
{
    coverMediaType = "IMAGE";
}
```

## 4.2. Migration

Không cần đổi schema `gallery_areas`.

Nếu `files.file_purpose` có enum/check constraint, bổ sung:

```text
GALLERY_AREA_COVER_VIDEO
```

Agent phải cập nhật migration/schema dump theo convention project.

---

# 5. FilePurpose và folder routing

Cập nhật:

```text
backend/PEMS.Application/Common/Files/FilePurpose.cs
```

Thêm enum:

```csharp
GalleryAreaCoverVideo
```

Thêm DB value:

```csharp
public const string GalleryAreaCoverVideo =
    "GALLERY_AREA_COVER_VIDEO";
```

Thêm mapping:

```csharp
FilePurpose.GalleryAreaCoverVideo
    => FilePurposeDbValues.GalleryAreaCoverVideo,
```

Thêm folder prefix:

```csharp
FilePurpose.GalleryAreaCoverVideo
    => "gallery/areas",
```

Không dùng nhầm:

```text
GALLERY_ITEM_VIDEO
GALLERY_VIDEO
GALLERY_DELEGATION_VIDEO
```

---

# 6. Backend validation

Cập nhật:

```text
backend/PEMS.Application/Common/Files/FileValidationPolicy.cs
```

Thêm rule:

```csharp
FilePurpose.GalleryAreaCoverVideo => new FileValidationRule
{
    MaxSizeBytes = 30 * 1024 * 1024,

    AllowedMimeTypes =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "video/mp4"
        },

    AllowedExtensions =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".mp4"
        },

    RequireImageMagicBytes = false
},
```

Backend phải kiểm tra:

- File tồn tại.
- Chỉ có một file.
- File size > 0.
- File size <= 30 MB.
- Extension `.mp4`.
- MIME `video/mp4`.
- Không tin hoàn toàn dữ liệu frontend.
- Không log binary/base64.

## Thời lượng 60 giây

Trong phạm vi đơn giản này:

- Frontend bắt buộc đọc metadata và chặn video trên 60 giây.
- Backend chưa thêm FFmpeg/ffprobe.
- Backend cưỡng chế định dạng và dung lượng.

Không tự ý thêm FFmpeg vào task.

---

# 7. Error code và thông báo

Bổ sung theo convention `GalleryErrorCodes` hiện tại:

```text
AREA_COVER_VIDEO_REQUIRED
AREA_COVER_VIDEO_INVALID
AREA_COVER_VIDEO_TOO_LARGE
AREA_COVER_VIDEO_UPLOAD_FAILED
```

Thông báo:

```text
Vui lòng chọn một video đại diện cho khu vực.
```

```text
Video đại diện khu vực chỉ hỗ trợ định dạng MP4.
```

```text
Video đại diện khu vực không được vượt quá 30 MB.
```

```text
Video đại diện khu vực không được dài quá 60 giây.
```

```text
File video không hợp lệ hoặc đã bị hỏng.
```

```text
Không thể tải video đại diện khu vực lên hệ thống. Vui lòng thử lại.
```

---

# 8. Refactor helper cover

File hiện tại cần kiểm tra:

```text
backend/PEMS.Application/Galleries/Common/GalleryCoverImage.cs
```

Hiện helper đang giả định Area và Location cover đều là ảnh.

Khuyến nghị tách:

```text
GalleryAreaCoverVideo.cs
GalleryLocationCoverImage.cs
```

## 8.1. GalleryAreaCoverVideo

Nhiệm vụ:

1. Validate MP4.
2. Gọi `IFileUploadService`.
3. Dùng `FilePurpose.GalleryAreaCoverVideo`.
4. Trả về `files.file_id`.

Pseudo-code:

```csharp
internal static class GalleryAreaCoverVideo
{
    public static async Task<ulong> UploadAsync(
        IFileUploadService fileUploadService,
        GalleryUploadFileCommandDto file,
        ulong actorId,
        CancellationToken cancellationToken)
    {
        EnsureMp4(file);

        await using var stream =
            new MemoryStream(file.Content, writable: false);

        var uploaded =
            await fileUploadService.UploadBusinessFileAsync(
                stream,
                file.FileName,
                file.ContentType ?? string.Empty,
                file.FileSize,
                FilePurpose.GalleryAreaCoverVideo,
                (long)actorId,
                cancellationToken);

        return (ulong)uploaded.FileId;
    }
}
```

## 8.2. Upload memory

Ưu tiên:

```text
IFormFile.OpenReadStream()
→ upload service
→ Google Drive
```

Không nên tạo nhiều bản sao:

```text
IFormFile → MemoryStream → ToArray → MemoryStream
```

Nếu command architecture hiện tại bắt buộc `byte[]`:

- Giới hạn cứng 30 MB.
- Chỉ buffer một lần.
- Không tạo thêm bản sao không cần thiết.
- Cấu hình request limit phù hợp.
- Ghi technical debt.

## 8.3. Location helper

Giữ nguyên rule ảnh hiện tại và `GalleryLocationCover`.

---

# 9. API tạo Area/Location

Rà soát:

```text
backend/PEMS.Api/Controllers/GalleriesController.cs
backend/PEMS.Application/Galleries/Commands/CreateGalleryLocation/*
backend/PEMS.Application/Galleries/Commands/CreateGalleryLocation/CreateGalleryLocationCommandHandler.cs
```

## 9.1. Multipart mới

Trường hợp `NEW_AREA`:

```text
mode
areaName
areaCoverVideo
locationName
locationCoverImage
```

Thay field:

```text
areaCoverImage
```

bằng:

```text
areaCoverVideo
```

## 9.2. Rule theo mode

### NEW_AREA

Bắt buộc:

- `areaName`.
- `areaCoverVideo`.
- `locationName`.
- `locationCoverImage`.

### EXISTING_AREA

- Không yêu cầu Area video.
- Không thay Area cover.
- Giữ business rule Location hiện tại.

## 9.3. Command

Dùng property rõ nghĩa:

```csharp
public GalleryUploadFileCommandDto? AreaCoverVideo { get; init; }
```

Không dùng tên `AreaCoverImage` để chứa video.

## 9.4. Create handler

Luồng:

```text
1. Resolve STAFF LEADER scope.
2. Lấy campus từ JWT/current user.
3. Validate mode.
4. Validate tên Area và Location.
5. Validate duplicate theo rule hiện tại.
6. NEW_AREA: bắt buộc AreaCoverVideo.
7. Validate LocationCoverImage.
8. Upload Area video lên Google Drive.
9. Upload Location image lên Google Drive.
10. Tạo GalleryArea.
11. Area.CoverFileId = areaVideoFileId.
12. Tạo GalleryLocation.
13. Location.CoverFileId = locationImageFileId.
14. Commit transaction.
15. Ghi audit.
16. Trả DTO.
```

## 9.5. Cleanup

Google Drive upload không nằm trong DB transaction.

Phải xử lý:

```text
Area video upload thành công
Location image upload thất bại
```

và:

```text
Upload thành công
Database commit thất bại
```

Tái sử dụng cleanup/orphan file mechanism hiện tại.

Không để Area/Location dở dang.

---

# 10. API chỉnh sửa Area

Rà soát:

```text
backend/PEMS.Application/Galleries/Commands/UpdateGalleryLocation/*
backend/PEMS.Application/Galleries/Commands/UpdateGalleryLocation/UpdateGalleryLocationCommandHandler.cs
backend/PEMS.Api/Controllers/GalleriesController.cs
```

Rule:

- Không upload video mới → giữ cover cũ.
- Upload video mới → validate và upload.
- Chỉ cập nhật `CoverFileId` sau upload thành công.
- Commit database trước.
- Cleanup/soft-delete file cũ sau commit.
- Upload/update thất bại → giữ cover cũ.

Không được:

```text
xóa file cũ
→ rồi mới upload file mới
```

Cho phép Area cũ chuyển từ ảnh sang video.

---

# 11. Entity, query và DTO

Không thêm `CoverMediaType` vào entity nếu có thể suy ra từ `files`.

Query cần lấy file metadata:

- `file_id`.
- `mime_type`.
- `file_purpose`.
- trạng thái deleted/status.
- URL scoped.

Tránh N+1 query.

DTO Area cần thêm:

```text
areaCoverMediaType
```

Giá trị:

```text
IMAGE
VIDEO
```

Giữ:

```text
areaCoverFileId
areaCoverUrl
```

Ví dụ:

```json
{
  "areaId": 10,
  "areaName": "Khu vực Alpha",
  "areaCoverFileId": 125,
  "areaCoverUrl": "/api/public/.../media/125/content",
  "areaCoverMediaType": "VIDEO"
}
```

Cập nhật:

- Management list DTO.
- Management detail DTO.
- Create/update response.
- Public campus navigation DTO.
- Public Area Showcase DTO.
- DTO factory/builder.
- TypeScript types.

Không trả direct Google Drive URL cho Public.

---

# 12. Public media authorization

Rà soát:

```text
backend/PEMS.Api/Controllers/PublicVisitFptuController.cs
public media handler/service hiện tại
```

Cho phép file Area cover video khi:

```text
file_id = gallery_areas.cover_file_id
AND Area ACTIVE
AND Area chưa bị xóa
AND Campus public hợp lệ
AND file chưa bị xóa
AND purpose là:
    GALLERY_AREA_COVER
    hoặc GALLERY_AREA_COVER_VIDEO
```

Không cho:

- Area inactive.
- File đã xóa.
- File không thuộc Area public.
- File tùy ý trên Drive.
- Client dùng external Drive ID trực tiếp.
- Truy cập khác campus bằng cách đổi fileId.

---

# 13. Streaming video từ Google Drive

Đây là phần bắt buộc cho video một phút.

## 13.1. Không buffer toàn bộ video

Không dùng:

```text
download toàn bộ
→ MemoryStream
→ ToArray
→ trả byte[]
```

## 13.2. Hỗ trợ HTTP Range

Endpoint video cần hỗ trợ:

```http
Range: bytes=...
```

Response phù hợp:

```http
206 Partial Content
Accept-Ranges: bytes
Content-Range: bytes start-end/total
Content-Length: ...
Content-Type: video/mp4
```

## 13.3. Nếu stream seekable

Có thể dùng:

```csharp
return File(
    stream,
    "video/mp4",
    enableRangeProcessing: true);
```

Chỉ dùng khi:

- Stream seek được.
- Có length đúng.
- Stream sống đến hết response.

## 13.4. Nếu Google Drive stream không seekable

Mở rộng storage abstraction để download range, ví dụ:

```csharp
Task<FileRangeResult> DownloadRangeAsync(
    string externalFileId,
    long from,
    long? to,
    CancellationToken cancellationToken);
```

Google Drive adapter gửi Range header tới Drive API.

Controller trả `206`.

Không expose access token.

---

# 14. Frontend Management

Rà soát:

```text
frontend/pems-react/src/pages/dashboard/gallery/LocationManagementStaffLeader.tsx
frontend/pems-react/src/pages/dashboard/gallery/types*
frontend/pems-react/src/pages/dashboard/gallery/api*
```

## 14.1. Types

```ts
export type AreaCoverMediaType = 'IMAGE' | 'VIDEO';
```

## 14.2. Input

Khi `NEW_AREA`:

```tsx
<input
  type="file"
  accept="video/mp4"
/>
```

Label:

```text
Video đại diện khu vực
```

Helper:

```text
Chỉ chấp nhận MP4, tối đa 30 MB và tối đa 60 giây.
Khuyến nghị video ngang, H.264, Full HD hoặc thấp hơn.
```

Location vẫn dùng input ảnh.

## 14.3. Validation frontend

Constants:

```ts
const MAX_AREA_VIDEO_BYTES = 30 * 1024 * 1024;
const MAX_AREA_VIDEO_DURATION_SECONDS = 60;
```

Kiểm tra:

1. File tồn tại.
2. Một file.
3. Extension `.mp4`.
4. MIME `video/mp4`.
5. Size > 0.
6. Size <= 30 MB.
7. Duration hợp lệ.
8. Duration <= 60.

Đọc duration:

```ts
function readVideoDuration(file: File): Promise<number> {
  return new Promise((resolve, reject) => {
    const video = document.createElement('video');
    const url = URL.createObjectURL(file);

    video.preload = 'metadata';

    const cleanup = () => {
      video.removeAttribute('src');
      video.load();
      URL.revokeObjectURL(url);
    };

    video.onloadedmetadata = () => {
      const duration = video.duration;
      cleanup();

      if (!Number.isFinite(duration) || duration <= 0) {
        reject(new Error(
          'File video không hợp lệ hoặc đã bị hỏng.'
        ));
        return;
      }

      resolve(duration);
    };

    video.onerror = () => {
      cleanup();
      reject(new Error(
        'File video không hợp lệ hoặc đã bị hỏng.'
      ));
    };

    video.src = url;
  });
}
```

## 14.4. Preview

```tsx
<video
  src={previewUrl}
  autoPlay
  muted
  loop
  playsInline
  controls
  preload="metadata"
/>
```

Hiển thị:

- Tên file.
- Dung lượng.
- Thời lượng.
- Chọn lại.
- Xóa.

Revoke object URL khi đổi file/unmount.

## 14.5. FormData

```ts
formData.append('areaCoverVideo', areaCoverVideo);
```

Location:

```ts
formData.append('locationCoverImage', locationCoverImage);
```

## 14.6. Edit và list

- Area video: preview video trong modal/form.
- Area ảnh cũ: hiển thị ảnh.
- Không chọn video mới: không gửi field.
- Không autoplay tất cả video trong table/list.
- List chỉ hiển thị icon video hoặc preview không autoplay.

---

# 15. Public frontend

Rà soát:

```text
frontend/pems-react/src/pages/public/CampusDetailVisitPage.tsx
public gallery types/API liên quan
```

## 15.1. Type

```ts
export type PublicAreaCoverMediaType =
  | 'IMAGE'
  | 'VIDEO';
```

## 15.2. Component background

Tạo component:

```text
AreaShowcaseBackground
```

Logic:

```text
VIDEO → <video>
IMAGE → <img>
error/missing → gradient fallback
```

Video:

```tsx
<video
  key={area.areaId}
  ref={videoRef}
  src={area.areaCoverUrl ?? undefined}
  autoPlay
  muted
  loop
  playsInline
  preload="auto"
  onCanPlay={handleCanPlay}
  onError={handleVideoError}
  className={
    videoReady
      ? 'area-showcase-video is-ready'
      : 'area-showcase-video'
  }
/>
```

CSS:

```css
.area-showcase-video,
.area-showcase-image {
  position: absolute;
  inset: 0;
  width: 100%;
  height: 100%;
  object-fit: cover;
}

.area-showcase-video {
  opacity: 0;
  transition: opacity 400ms ease;
}

.area-showcase-video.is-ready {
  opacity: 1;
}
```

## 15.3. State

```ts
const [videoReady, setVideoReady] = useState(false);
const [videoFailed, setVideoFailed] = useState(false);
```

Khi Area đổi:

```ts
useEffect(() => {
  setVideoReady(false);
  setVideoFailed(false);
}, [selectedArea?.areaId]);
```

Khi `canplay`:

```ts
const handleCanPlay = async () => {
  const video = videoRef.current;
  if (!video) return;

  try {
    await video.play();
    setVideoReady(true);
  } catch {
    setVideoReady(false);
  }
};
```

Khi lỗi:

```ts
const handleVideoError = () => {
  setVideoReady(false);
  setVideoFailed(true);
};
```

## 15.4. Chỉ tải một video

Không render video cho toàn bộ `areas`.

Chỉ render video của `selectedArea`.

Cleanup:

```ts
useEffect(() => {
  const video = videoRef.current;

  return () => {
    if (!video) return;

    video.pause();
    video.removeAttribute('src');
    video.load();
  };
}, [selectedArea?.areaId]);
```

## 15.5. Visibility

Khi tab hidden:

- Pause video.

Khi visible lại:

- Nếu showcase còn mở thì play lại.

Khi đóng showcase:

- Pause.
- Remove source.
- Release buffer.

## 15.6. Audio

Video Public luôn muted.

Không thêm nút bật âm thanh.

---

# 16. Audit

## Create Area

Ghi:

```text
Action: CREATE_GALLERY_AREA
areaId
areaName
coverFileId
coverMediaType = VIDEO
filePurpose = GALLERY_AREA_COVER_VIDEO
campusId
actorUserId
timestamp
```

## Replace cover

Ghi:

```text
Action: UPDATE_GALLERY_AREA_COVER
areaId
oldCoverFileId
newCoverFileId
oldCoverMediaType
newCoverMediaType = VIDEO
actorUserId
timestamp
```

Không ghi binary, base64, access token hoặc URL nhạy cảm.

---

# 17. Security

Giữ nguyên:

- Role `STAFF`.
- Sub-role `LEADER`.
- Campus scope từ JWT/current user.
- Không tin campusId từ frontend.
- Handler tự guard scope.
- Không sửa Area ngoài campus.
- Public media file phải được authorize theo quan hệ business.
- Không expose Google Drive credential.

---

# 18. Request/server configuration

Kiểm tra:

- ASP.NET multipart limit.
- Kestrel limit.
- Reverse proxy/nginx/IIS limit nếu có.
- Google Drive upload timeout.
- Cancellation token.

Request limit chỉ nên cao hơn 30 MB một khoảng nhỏ cho multipart overhead, ví dụ 32–35 MB.

Không mở giới hạn quá lớn.

---

# 19. Backend tests bắt buộc

## FilePurpose

- Map đúng DB value.
- Map đúng folder.
- Không map nhầm Gallery Item.

## Validation

- Chấp nhận `.mp4` + `video/mp4`.
- Từ chối `.webm`.
- Từ chối `.mov`.
- Từ chối `.avi`.
- Từ chối image MIME.
- Từ chối > 30 MB.
- Từ chối file rỗng.

## Create

- NEW_AREA bắt buộc video.
- Upload MP4 hợp lệ.
- Không chấp nhận ảnh Area.
- EXISTING_AREA không yêu cầu video.
- Location vẫn bắt buộc ảnh.
- `CoverFileId` đúng.
- Purpose đúng.
- Scope đúng.
- Audit đúng.
- Cleanup khi lỗi.

## Update

- Không có video mới → giữ cover.
- Có video mới → thay cover.
- Upload fail → giữ cover cũ.
- DB fail → giữ cover cũ.
- Area ảnh cũ đổi sang video được.
- Không sửa khác campus.

## DTO/Public

- Video → `VIDEO`.
- Ảnh cũ → `IMAGE`.
- URL scoped đúng.
- Area inactive bị chặn.
- File không thuộc Area bị chặn.
- Range hợp lệ trả 206.
- Không buffer toàn bộ video.

---

# 20. Frontend tests bắt buộc

## Management

- NEW_AREA hiện input MP4.
- Không còn input ảnh Area.
- Location vẫn có input ảnh.
- Thiếu video bị chặn.
- Sai extension/MIME bị chặn.
- > 30 MB bị chặn.
- > 60 giây bị chặn.
- Preview hoạt động.
- Object URL được revoke.
- FormData gửi `areaCoverVideo`.

## Public

- Click Area video → tự chạy.
- Có muted.
- Có loop.
- Có playsInline.
- Object-fit cover.
- Chỉ một video active.
- Đổi Area → video cũ dừng.
- `canplay` → fade-in.
- Error → giữ fallback.
- Đóng showcase → dừng.
- Area ảnh cũ vẫn chạy đúng.

---

# 21. Tiêu chí nghiệm thu

1. STAFF LEADER tạo Area mới bằng một MP4.
2. Chỉ nhận một video.
3. Tối đa 30 MB.
4. Frontend từ chối video trên 60 giây.
5. Video lưu trên Google Drive.
6. Metadata lưu trong `files`.
7. Purpose là `GALLERY_AREA_COVER_VIDEO`.
8. `gallery_areas.cover_file_id` trỏ đến video.
9. Không thêm cột video mới.
10. Location Cover vẫn là ảnh.
11. Area ảnh cũ vẫn hoạt động.
12. DTO trả `areaCoverMediaType`.
13. Public video tự phát.
14. Public video muted.
15. Public video loop.
16. Public video playsInline.
17. Chỉ tải video Area đang chọn.
18. Chuyển Area thì video cũ dừng.
19. Đóng showcase thì video dừng.
20. Có fade-in sau `canplay`.
21. Video lỗi không crash trang.
22. Public authorization đúng.
23. Video endpoint không trả toàn bộ `byte[]`.
24. Có HTTP Range hoặc cơ chế tương đương với Drive.
25. Audit đầy đủ.
26. Backend/frontend build pass.
27. Tests pass.
28. Không ảnh hưởng YouTube, TTS, Location Cover.
29. Không thêm FFmpeg/HLS/background job.

---

# 22. Thứ tự triển khai

## Phase 1

- FilePurpose.
- DB value.
- Folder routing.
- Validation.
- Migration/check constraint.
- Unit tests.

## Phase 2

- Refactor cover helper.
- Controller multipart.
- Create command/handler.
- Update command/handler.
- Cleanup.
- Audit.

## Phase 3

- Query file metadata.
- DTO media type.
- Management/public DTO factory.
- Tests.

## Phase 4

- Public media authorization.
- Google Drive Range streaming.
- Integration tests.

## Phase 5

- Management types.
- Video input.
- Size/duration validation.
- Preview.
- FormData.
- Edit/list behavior.

## Phase 6

- Public type.
- `AreaShowcaseBackground`.
- Autoplay/muted/loop/playsInline.
- Fade-in.
- Cleanup.
- Visibility handling.

## Phase 7

Regression test:

- Management Gallery.
- Location Management.
- Public navigation.
- Area Showcase.
- Location Showcase.
- Gallery Item detail.
- YouTube.
- TTS.
- Active/inactive.
- Mobile Safari.
- Chrome desktop.
- Mạng chậm.

---

# 23. Do / Don't

## Do

- Tái sử dụng `cover_file_id`.
- Dùng purpose riêng.
- Lưu trên Google Drive.
- Dùng scoped media endpoint.
- Validate frontend/backend.
- Giữ Area ảnh cũ.
- Chỉ tải video Area đang chọn.
- Cleanup video cũ.
- Hỗ trợ HTTP Range.
- Giữ Clean Architecture hiện tại.
- Viết tests.

## Don't

- Không lưu binary trong database.
- Không public-share trực tiếp file Drive.
- Không expose token.
- Không dùng YouTube.
- Không dùng Gallery Item purpose.
- Không cho Location upload video.
- Không preload tất cả video.
- Không autoplay video trong mọi management row.
- Không buffer toàn bộ video khi public playback.
- Không xóa cover cũ trước commit.
- Không thêm FFmpeg.
- Không thêm HLS.
- Không tạo thumbnail tự động.
- Không thay đổi Gallery Item local video rule.
- Không tin campusId từ frontend.

---

# 24. Cấu hình cuối cùng

```text
Số lượng:        1 video/Area
Định dạng:       MP4
Extension:       .mp4
MIME:            video/mp4
Dung lượng:      tối đa 30 MB
Thời lượng:      tối đa 60 giây
Lưu trữ:         Google Drive
Purpose:         GALLERY_AREA_COVER_VIDEO
Folder:          gallery/areas
Database:        gallery_areas.cover_file_id
Location Cover:  vẫn là ảnh
Public:          autoPlay + muted + loop + playsInline
Hiển thị:        object-fit cover, fade-in sau canplay
Tương thích:     Area ảnh cũ vẫn hoạt động
Không dùng:      FFmpeg, HLS, thumbnail, background job
```

---

# 25. Báo cáo bắt buộc từ AI Agent sau khi code

Agent phải báo cáo:

1. Danh sách file đã sửa.
2. Migration đã thêm.
3. API contract trước/sau.
4. Business rule đã cài đặt.
5. Cách xử lý Google Drive Range.
6. Cách giữ tương thích Area ảnh cũ.
7. Tests đã thêm.
8. Kết quả backend build.
9. Kết quả frontend build.
10. Kết quả test.
11. Technical debt còn lại.
12. Các bước manual test.

Không được chỉ báo “đã hoàn thành” mà không có bằng chứng build/test.
