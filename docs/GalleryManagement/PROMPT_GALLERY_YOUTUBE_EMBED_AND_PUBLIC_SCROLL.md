# PROMPT / ĐẶC TẢ CODE — Bổ sung Video YouTube cho Gallery Item và sửa Scroll Public Gallery

> Tài liệu này dùng cho AI Agent đọc và triển khai trên project PEMS.
>
> Phạm vi gồm:
>
> 1. Cho phép thêm video YouTube vào Gallery Item bên cạnh upload video từ máy.
> 2. Sửa Public VisitFPTU Gallery để danh sách Area và Location dài có thể cuộn độc lập, không bị cắt khỏi viewport.
>
> AI Agent phải đọc source code và database thật trước khi sửa. Không code theo trí nhớ. Không mock data. Không sinh file rác. Không phá các flow Gallery hiện đã hoàn thiện.

---

# 0. Bối cảnh hiện tại

Module Gallery đã hoàn thiện các phần chính:

```text
Campus
→ Gallery Area
→ Gallery Location
→ Gallery Item
→ Gallery Item Media
→ Files
```

Nghiệp vụ hiện hành:

```text
1 campus có nhiều area.
1 area có nhiều location.
1 location có nhiều gallery item.
1 gallery item có nhiều media.
Mỗi gallery item có đúng 1 primary media.
```

Gallery Item hiện hỗ trợ:

```text
- Ảnh upload từ máy.
- Video upload từ máy.
- item_type = MEDIA hoặc VISIT_DELEGATION.
- media_kind = IMAGE, VIDEO hoặc MIXED.
- Upload file thật lên Google Drive qua upload service dùng chung.
- Public Gallery hiển thị Area Showcase và Location Showcase.
```

Yêu cầu mới:

```text
1. Thêm video YouTube bằng URL mà không tải video về PEMS.
2. Public Gallery phải render YouTube bằng iframe embed.
3. Area list và Location list dài phải có scroll dọc độc lập.
4. Không thay đổi các nghiệp vụ Gallery hiện tại ngoài hai phần trên.
```

---

# 1. Quy tắc an toàn và kiến trúc bắt buộc

```text
KHÔNG tải video YouTube về backend.
KHÔNG tải video YouTube lên Google Drive.
KHÔNG lưu binary/base64 video YouTube vào MySQL.
KHÔNG nhận hoặc lưu iframe HTML do user nhập.
KHÔNG cho frontend tự tạo embed HTML không kiểm soát.
KHÔNG dùng URL YouTube làm src của thẻ <video>.
KHÔNG gọi /api/files/{fileId}/content cho media YouTube.
KHÔNG sửa flow upload ảnh/video từ máy đang hoạt động.
KHÔNG làm gallery_item_media.file_id nullable nếu không thật sự bắt buộc.
KHÔNG tạo bảng Gallery media mới nếu có thể giữ kiến trúc hiện tại.
KHÔNG hard-code API key YouTube trong frontend.
KHÔNG mở CSP với frame-src * hoặc img-src *.
KHÔNG làm toàn bộ Public Gallery page cuộn dài theo số lượng area/location.
KHÔNG render Location panel bên trong row Area active.
KHÔNG đổi database relation area → location → gallery item.
KHÔNG đổi item_type, media_kind, status hoặc RBAC hiện tại.
```

Luồng upload file thật vẫn giữ nguyên:

```text
Business Handler
→ IFileUploadService.UploadBusinessFileAsync(...)
→ FileUploadService
→ IFileStorageFolderResolver
→ GoogleDriveStorageService
→ files
→ gallery_item_media
```

Luồng YouTube mới:

```text
Create/Update Gallery Item Handler
→ Gallery External Media Service
→ Validate + normalize YouTube URL
→ Extract YouTube video ID
→ Register metadata trong files
→ Insert gallery_item_media
→ Public render iframe
```

---

# 2. Source và database phải đọc trước khi code

AI Agent phải mở source thật và đối chiếu trước khi sửa.

## 2.1. Database

Đọc file SQL mới nhất của project và kiểm tra trực tiếp:

```text
- CREATE TABLE files
- CREATE TABLE gallery_areas
- CREATE TABLE gallery_locations
- CREATE TABLE gallery_items
- CREATE TABLE gallery_item_media
- Các CHECK/ENUM/UNIQUE/FK/index liên quan
- Nullability của files.file_size
- Kiểu dữ liệu của files.storage_provider
- Kiểu dữ liệu của files.file_purpose
- Constraint unique của gallery_item_media.file_id nếu có
```

Tìm:

```bash
grep -n "CREATE TABLE.*files" -n <database-file>.sql
grep -n "CREATE TABLE.*gallery_areas" -n <database-file>.sql
grep -n "CREATE TABLE.*gallery_locations" -n <database-file>.sql
grep -n "CREATE TABLE.*gallery_items" -n <database-file>.sql
grep -n "CREATE TABLE.*gallery_item_media" -n <database-file>.sql
grep -n "storage_provider\|file_purpose\|external_file_id\|web_view_url\|thumbnail_url" <database-file>.sql
```

## 2.2. Backend

Ưu tiên đọc:

```text
backend/PEMS.Application/Common/Files/FilePurpose.cs
backend/PEMS.Application/Common/Files/FileValidationPolicy.cs
backend/PEMS.Application/Common/Files/FileObjectKeyBuilder.cs
backend/PEMS.Application/Common/Files/FileUploadService.cs
backend/PEMS.Application/Common/Interfaces/IFileUploadService.cs
backend/PEMS.Application/Common/Interfaces/IFileStorageFolderResolver.cs

backend/PEMS.Domain/Entities/File.cs
backend/PEMS.Domain/Entities/GalleryItem.cs
backend/PEMS.Domain/Entities/GalleryItemMedia.cs

backend/PEMS.Infrastructure/Persistence/Configurations/FileConfiguration.cs
backend/PEMS.Infrastructure/Persistence/Configurations/GalleryItemConfiguration.cs
backend/PEMS.Infrastructure/Persistence/Configurations/GalleryItemMediaConfiguration.cs

backend/PEMS.Application/Galleries/**
backend/PEMS.Api/Controllers/GalleriesController.cs
backend/PEMS.Api/Controllers/PublicVisitFptuController.cs
backend/PEMS.Api/Middleware/SecurityHeadersMiddleware.cs
backend/PEMS.Api/Extensions/**
```

Tìm:

```bash
grep -R "gallery_item_media" -n backend
grep -R "CreateGallery\|UpdateGallery\|GalleryItemMedia" -n backend/PEMS.Application
grep -R "UploadBusinessFileAsync" -n backend/PEMS.Application/Galleries
grep -R "fileUrl\|thumbnailUrl\|mediaType\|isPrimary" -n backend/PEMS.Application/Galleries
grep -R "Content-Security-Policy\|frame-src\|img-src" -n backend/PEMS.Api
```

## 2.3. Frontend

Tìm màn quản lý Gallery và Public Gallery:

```bash
grep -R "Quản lý Gallery\|Gallery Item\|youtube\|mediaType\|sourceType" -n frontend/pems-react/src
grep -R "VisitFPTU\|Visit FPTU\|Area Showcase\|Location Showcase" -n frontend/pems-react/src
grep -R "overflow-y\|scrollIntoView\|selectedAreaId\|selectedLocationId" -n frontend/pems-react/src
```

Phải xác định chính xác:

```text
- Modal create Gallery Item.
- Modal edit Gallery Item.
- DTO/type media frontend.
- API service create/update Gallery.
- Component render ảnh/video.
- Public campus hero.
- Sidebar area.
- Panel location.
- Area Showcase.
- Location Showcase.
```

---

# PHẦN A — BỔ SUNG VIDEO YOUTUBE CHO GALLERY ITEM

# 3. Mục tiêu nghiệp vụ

Sau cập nhật, Staff Leader có thể thêm media video theo hai nguồn:

```text
1. Tải video từ máy.
2. Thêm video YouTube bằng URL.
```

Luồng YouTube:

```text
Staff Leader chọn “Thêm video YouTube”
→ Dán URL YouTube
→ Backend validate URL
→ Backend extract video ID
→ Backend lưu metadata
→ Gallery Item lưu media như bình thường
→ Public Gallery render YouTube iframe
```

Video YouTube không được copy về hệ thống.

---

# 4. Phương án dữ liệu

## 4.1. Giữ kiến trúc hiện tại

Không tạo bảng mới nếu schema hiện tại cho phép lưu metadata external media trong bảng `files`.

Giữ quan hệ:

```text
gallery_items
→ gallery_item_media
→ files
```

Đối với media YouTube:

```text
files chỉ đại diện cho external media metadata.
Không có binary file trên Google Drive.
```

Ví dụ dữ liệu logic:

```text
storage_provider = OTHER
file_purpose = GALLERY_YOUTUBE_VIDEO
mime_type = video/youtube
external_file_id = <youtube_video_id>
web_view_url = https://www.youtube.com/watch?v=<youtube_video_id>
thumbnail_url = https://i.ytimg.com/vi/<youtube_video_id>/hqdefault.jpg
object_key = youtube/gallery/<unique-key>
original_filename = youtube-<youtube_video_id>
file_size = NULL hoặc 0 tùy nullability database thật
```

Sau đó insert `gallery_item_media`:

```text
gallery_item_id = item hiện tại
file_id = files.file_id vừa tạo
media_type = VIDEO
is_primary = 0 hoặc 1
display_order = thứ tự media
status = ACTIVE
```

## 4.2. storage_provider

Phương án mặc định ít thay đổi:

```text
storage_provider = OTHER
```

Chỉ thêm `YOUTUBE` vào enum/database nếu source thật đã có convention phù hợp và việc sửa enum là an toàn.

Không bắt buộc sửa schema chỉ để thêm provider mới.

## 4.3. file_purpose

Thêm constant/mapping mới:

```text
GALLERY_YOUTUBE_VIDEO
```

Tên enum/backend đề xuất:

```csharp
GalleryYouTubeVideo
```

Nếu `file_purpose` là VARCHAR thì chỉ cần mapping value.

Nếu có whitelist/check constraint, phải cập nhật đầy đủ database + backend.

## 4.4. Không dùng file content endpoint

Media YouTube không có binary file.

`GET /api/files/{fileId}/content`:

```text
- Không được gọi Google Drive cho record YouTube.
- Nếu bị gọi nhầm, trả lỗi có kiểm soát.
- Không redirect tùy tiện tới YouTube từ file content endpoint.
```

Frontend phải dùng `sourceType` để chọn renderer phù hợp.

---

# 5. YouTube URL contract

Backend phải hỗ trợ tối thiểu các dạng URL:

```text
https://www.youtube.com/watch?v={videoId}
https://youtube.com/watch?v={videoId}
https://youtu.be/{videoId}
https://www.youtube.com/shorts/{videoId}
https://youtube.com/shorts/{videoId}
https://www.youtube.com/embed/{videoId}
https://youtube.com/embed/{videoId}
```

Sau khi parse:

```text
youtubeVideoId = video ID chuẩn
watchUrl = https://www.youtube.com/watch?v={videoId}
embedUrl = https://www.youtube-nocookie.com/embed/{videoId}
thumbnailUrl = https://i.ytimg.com/vi/{videoId}/hqdefault.jpg
```

Không lưu nguyên URL tùy ý nếu có thể canonical hóa.

---

# 6. Validation và security

## 6.1. Input validation

Reject:

```text
- Chuỗi rỗng.
- URL không hợp lệ.
- Domain không phải youtube.com hoặc youtu.be.
- Domain giả mạo như youtube.com.attacker.com.
- Không extract được video ID.
- Video ID sai định dạng.
- Input chứa iframe/script/HTML.
- URL vượt quá độ dài database/validator.
```

Không dùng string `Contains("youtube.com")` để kiểm tra domain.

Phải parse `Uri` và kiểm tra hostname chính xác.

## 6.2. Video ID

Validate theo format YouTube video ID đang dùng trong source implementation.

Không suy luận ID từ bất kỳ query param không xác định nào.

## 6.3. Error codes đề xuất

```text
GALLERY_YOUTUBE_URL_REQUIRED
GALLERY_YOUTUBE_URL_INVALID
GALLERY_YOUTUBE_HOST_NOT_ALLOWED
GALLERY_YOUTUBE_VIDEO_ID_INVALID
GALLERY_YOUTUBE_VIDEO_NOT_FOUND
GALLERY_YOUTUBE_VIDEO_UNAVAILABLE
GALLERY_YOUTUBE_VIDEO_NOT_EMBEDDABLE
```

## 6.4. Optional YouTube Data API

Phase cơ bản:

```text
- Validate URL.
- Extract ID.
- Lưu metadata.
- Render embed.
```

Phase nâng cao nếu project có API key backend:

```text
- Gọi YouTube Data API từ backend.
- Kiểm tra video tồn tại.
- Kiểm tra privacy/status.
- Kiểm tra embeddable.
- Lấy title và thumbnail chuẩn.
```

API key:

```text
- Chỉ nằm trong backend config.
- Không commit key thật.
- Không trả key cho frontend.
- Không log key.
```

Không bắt buộc thêm YouTube Data API nếu user chưa cấu hình key.

---

# 7. Backend abstraction mới

Tạo abstraction ở Application layer, ví dụ:

```csharp
public interface IGalleryExternalMediaService
{
    Task<RegisteredExternalMediaResult> RegisterYouTubeAsync(
        string youtubeUrl,
        CancellationToken cancellationToken);
}
```

Result đề xuất:

```csharp
public sealed record RegisteredExternalMediaResult(
    long FileId,
    string SourceType,
    string ExternalId,
    string CanonicalUrl,
    string EmbedUrl,
    string ThumbnailUrl,
    string MimeType);
```

Implementation đặt tại Infrastructure.

Nhiệm vụ:

```text
1. Validate URL.
2. Extract video ID.
3. Canonicalize URL.
4. Optional: gọi YouTube API.
5. Tạo object_key unique.
6. Insert files metadata.
7. Trả file_id và metadata.
```

Không đặt toàn bộ parse logic trong Create/Update Gallery handler.

---

# 8. Request Create/Edit Gallery Item

## 8.1. Giữ multipart/form-data

Vì create/edit Gallery hiện có upload file, tiếp tục dùng multipart.

Mở rộng request:

```text
title
description
locationId
itemType
status
newFiles[]
youtubeUrls[]
keepMediaIds[]
primaryMediaKey
```

Tên field cuối cùng phải theo convention source hiện tại; không tự đổi contract nếu không cần.

## 8.2. youtubeUrls[]

Cho phép:

```text
- 0 URL.
- 1 URL.
- Nhiều URL YouTube trong cùng Gallery Item.
```

Gallery Item có thể chứa:

```text
- Chỉ ảnh upload.
- Chỉ video upload.
- Chỉ video YouTube.
- Ảnh upload + video YouTube.
- Video upload + video YouTube.
- Ảnh upload + video upload + video YouTube.
```

## 8.3. Primary media key

Primary media phải hỗ trợ:

```text
- Existing media.
- New uploaded file.
- New YouTube media.
```

Đề xuất key:

```text
existing:{mediaId}
upload:{index}
youtube:{index}
```

Nếu source hiện tại đã có cơ chế key khác, mở rộng cơ chế đó thay vì viết lại toàn bộ.

Backend phải enforce:

```text
Mỗi Gallery Item sau create/edit có đúng 1 primary media.
```

Fallback nếu primary input không hợp lệ phải theo rule hiện tại của Gallery.

---

# 9. Create Gallery Item

Luồng đề xuất:

```text
1. Validate actor Staff Leader và campus scope như hiện tại.
2. Validate location/area status như hiện tại.
3. Validate title, description, itemType, status như hiện tại.
4. Upload file local bằng IFileUploadService như hiện tại.
5. Với mỗi youtubeUrl:
   - Gọi IGalleryExternalMediaService.RegisterYouTubeAsync.
   - Nhận file_id metadata.
6. Tạo GalleryItemMedia cho uploaded file và YouTube media.
7. Resolve primary media.
8. Tính media_kind.
9. Lưu trong cùng transaction.
10. Giữ hook TTS hiện tại nếu đang có.
```

Không để việc đăng ký YouTube media chạy ngoài transaction nếu có thể làm DB ở trạng thái dở dang.

---

# 10. Update Gallery Item

Edit phải hỗ trợ:

```text
- Giữ YouTube media cũ.
- Xóa YouTube media cũ.
- Thêm YouTube media mới.
- Giữ upload media cũ.
- Thêm upload media mới.
- Thay đổi primary giữa existing/upload/YouTube.
```

Rule:

```text
1. Sau edit phải còn ít nhất 1 media active.
2. Sau edit có đúng 1 primary media.
3. media_kind phải tính lại theo toàn bộ media active.
4. Xóa media YouTube chỉ soft-delete/hide theo convention hiện tại.
5. Không gọi Google Drive delete với YouTube metadata.
6. Không ảnh hưởng Gallery Item khác cùng location.
```

---

# 11. media_kind

Không thêm `YOUTUBE` vào `media_kind`.

Quy tắc:

```text
Tất cả media active là ảnh
→ IMAGE

Tất cả media active là video
(upload video hoặc YouTube)
→ VIDEO

Có ít nhất 1 ảnh và ít nhất 1 video
→ MIXED
```

Nguồn media:

```text
UPLOADED_FILE
YOUTUBE
```

Loại media:

```text
IMAGE
VIDEO
```

Hai khái niệm này phải tách biệt.

---

# 12. DTO response

Media DTO phải trả đủ dữ liệu để frontend không tự suy luận.

Ví dụ:

```json
{
  "mediaId": 100,
  "fileId": 200,
  "mediaType": "VIDEO",
  "sourceType": "YOUTUBE",
  "fileUrl": null,
  "youtubeVideoId": "abc123xyz89",
  "embedUrl": "https://www.youtube-nocookie.com/embed/abc123xyz89",
  "webViewUrl": "https://www.youtube.com/watch?v=abc123xyz89",
  "thumbnailUrl": "https://i.ytimg.com/vi/abc123xyz89/hqdefault.jpg",
  "isPrimary": true,
  "displayOrder": 0,
  "status": "ACTIVE"
}
```

Uploaded video:

```json
{
  "mediaId": 101,
  "fileId": 201,
  "mediaType": "VIDEO",
  "sourceType": "UPLOADED_FILE",
  "fileUrl": "/api/files/201/content",
  "youtubeVideoId": null,
  "embedUrl": null,
  "webViewUrl": null,
  "thumbnailUrl": "/api/files/202/content",
  "isPrimary": false,
  "displayOrder": 1,
  "status": "ACTIVE"
}
```

Uploaded image:

```json
{
  "mediaId": 102,
  "fileId": 203,
  "mediaType": "IMAGE",
  "sourceType": "UPLOADED_FILE",
  "fileUrl": "/api/files/203/content",
  "youtubeVideoId": null,
  "embedUrl": null,
  "webViewUrl": null,
  "thumbnailUrl": "/api/files/203/content",
  "isPrimary": false,
  "displayOrder": 2,
  "status": "ACTIVE"
}
```

Frontend phải dựa vào `sourceType`.

---

# 13. UI Quản lý Gallery

## 13.1. Chọn nguồn video

Trong modal create/edit Gallery Item, phần video có:

```text
Nguồn video

(●) Tải video từ máy
( ) Thêm video YouTube
```

Có thể dùng tabs, segmented control hoặc radio theo component hiện tại.

Không thay đổi UI upload ảnh.

## 13.2. Khi chọn upload từ máy

Giữ nguyên:

```text
- File picker.
- Validation extension/size.
- Preview.
- Remove.
- Primary selection.
```

## 13.3. Khi chọn YouTube

Hiển thị:

```text
URL video YouTube *
[ https://www.youtube.com/watch?v=... ]

[Kiểm tra video]
```

Sau khi hợp lệ:

```text
- Hiển thị thumbnail.
- Hiển thị preview iframe.
- Hiển thị URL canonical.
- Cho phép remove.
- Cho phép chọn primary.
- Cho phép thêm URL khác.
```

Không nhận iframe HTML.

## 13.4. Edit media cũ

YouTube media cũ phải hiển thị giống một media card:

```text
- Badge YouTube.
- Thumbnail.
- Nút preview.
- Nút remove.
- Radio/checkbox primary theo UI hiện tại.
```

Không cố gọi `/api/files/{fileId}/content`.

---

# 14. Public Gallery render YouTube

Tại mọi component render media:

```text
sourceType = UPLOADED_FILE + mediaType = IMAGE
→ <img>

sourceType = UPLOADED_FILE + mediaType = VIDEO
→ <video controls>

sourceType = YOUTUBE
→ <iframe>
```

Ví dụ:

```tsx
if (media.sourceType === 'YOUTUBE') {
  return (
    <div className="aspect-video w-full overflow-hidden rounded-xl bg-black">
      <iframe
        src={media.embedUrl}
        title={media.altText || 'YouTube video'}
        className="h-full w-full"
        allow="accelerometer; autoplay; clipboard-write; encrypted-media; gyroscope; picture-in-picture"
        allowFullScreen
      />
    </div>
  );
}

if (media.mediaType === 'VIDEO') {
  return (
    <video
      src={media.fileUrl}
      controls
      preload="metadata"
      className="h-full w-full object-contain"
    />
  );
}

return <img src={media.fileUrl} alt={media.altText || ''} />;
```

Không autoplay mặc định.

Player phải responsive 16:9.

---

# 15. Public thumbnail và fallback

YouTube media thumbnail:

```text
thumbnail_url từ metadata YouTube.
```

Nếu thumbnail lỗi:

```text
- Hiển thị video placeholder.
- Không làm hỏng list.
```

Nếu player lỗi:

```text
Video hiện không khả dụng.
[Xem trên YouTube]
```

Các trường hợp:

```text
- Video bị xóa.
- Chuyển private.
- Tắt embed.
- Age restriction.
- Region restriction.
- Network error.
```

Không để một media lỗi làm crash toàn bộ Gallery page.

---

# 16. Content Security Policy

Cập nhật CSP đúng vị trí source hiện tại.

Cho phép tối thiểu:

```text
frame-src:
- https://www.youtube.com
- https://www.youtube-nocookie.com

img-src:
- https://i.ytimg.com
- https://img.youtube.com
```

Có thể cần thêm:

```text
connect-src
media-src
```

chỉ khi player thực tế yêu cầu và phải kiểm tra browser console.

Không dùng:

```text
frame-src *
img-src *
```

Giữ các domain cũ đang hoạt động.

---

# PHẦN B — SỬA SCROLL PUBLIC GALLERY

# 17. Vấn đề hiện tại

Khi seed nhiều dữ liệu:

```text
Campus Hà Nội có nhiều Gallery Area.
Một Area có thể có 20 Gallery Location.
```

Hiện tượng:

```text
- Area sidebar kéo dài xuống dưới viewport.
- Area cuối không xem/click được.
- Khi area active nằm gần đáy, Location panel bị đẩy xuống.
- Location cuối bị cắt.
- Không có vùng scroll riêng.
- Viewer chính bị che hoặc layout tràn.
```

Đây là lỗi UI overflow/layout, không phải lỗi database.

---

# 18. Mục tiêu layout

Toàn bộ Gallery viewer phải giới hạn trong viewport:

```text
height = 100dvh - public header height
```

Desktop:

```text
┌──────────────────────────────────────────────────────────┐
│ Public Header                                            │
├────────────────┬─────────────────────┬───────────────────┤
│ Area Sidebar   │ Location Panel      │ Gallery Viewer    │
│ scroll riêng   │ scroll riêng        │ fixed viewport    │
│                │                     │                   │
└────────────────┴─────────────────────┴───────────────────┘
```

Không để body page dài theo số item.

---

# 19. Area Sidebar

Cấu trúc:

```text
Area Sidebar
├── Header / collapse button
└── Area List
    └── overflow-y: auto
```

Yêu cầu:

```text
- Sidebar không vượt viewport.
- Area list có scroll dọc.
- Area cuối xem/click được.
- Active area giữ highlight.
- Không có horizontal scroll.
- Scroll area không kéo viewer.
- Scrollbar mỏng nhưng nhìn thấy.
```

CSS concept:

```css
.public-gallery-shell {
  position: relative;
  height: calc(100dvh - var(--public-header-height, 82px));
  min-height: 0;
  overflow: hidden;
}

.public-gallery-area-sidebar {
  position: absolute;
  top: 0;
  bottom: 0;
  left: 24px;
  z-index: 20;

  display: flex;
  flex-direction: column;
  min-height: 0;
  overflow: hidden;
}

.public-gallery-area-list {
  flex: 1;
  min-height: 0;
  overflow-y: auto;
  overflow-x: hidden;
  overscroll-behavior: contain;
  scrollbar-gutter: stable;
}
```

`min-height: 0` là bắt buộc trong flex layout.

---

# 20. Location Panel

Location panel phải là sibling độc lập của Area sidebar.

Cấu trúc đúng:

```text
Gallery Shell
├── Area Sidebar
├── Location Panel
└── Viewer
```

Cấu trúc bị cấm:

```text
Area List
└── Area Active Row
    └── Location Panel
```

Không căn `top` theo row area active.

Panel phải giới hạn từ dưới header đến đáy viewport.

CSS concept:

```css
.public-gallery-location-panel {
  position: absolute;
  top: 0;
  bottom: 0;
  left: var(--area-sidebar-right);
  z-index: 21;

  display: flex;
  flex-direction: column;
  min-height: 0;
  overflow: hidden;
}

.public-gallery-location-list {
  flex: 1;
  min-height: 0;
  overflow-y: auto;
  overflow-x: hidden;
  overscroll-behavior: contain;
  scrollbar-gutter: stable;
}
```

Yêu cầu:

```text
- 20 locations xem được đầy đủ.
- Location cuối click được.
- Scroll location không cuộn area.
- Panel không bị clipping.
- Panel không vượt đáy viewport.
```

Nếu kiến trúc hiện tại bắt buộc overlay, có thể dùng React Portal, nhưng ưu tiên sibling trong Gallery shell.

---

# 21. Scroll độc lập

Hành vi:

```text
Pointer trên Area list
→ chỉ Area list cuộn.

Pointer trên Location list
→ chỉ Location list cuộn.

Pointer trên Viewer
→ không cuộn hai panel.
```

Dùng:

```css
overscroll-behavior: contain;
```

Không bắt wheel event thủ công nếu CSS native đã xử lý đủ.

---

# 22. Auto-scroll active item

Khi selected ID thay đổi:

```text
- Click.
- Next/previous.
- Route/query param.
- Reload.
- Programmatic navigation.
```

Đưa item active vào vùng nhìn thấy:

```tsx
useEffect(() => {
  activeAreaRef.current?.scrollIntoView({
    block: 'nearest',
    behavior: 'smooth',
  });
}, [selectedAreaId]);

useEffect(() => {
  activeLocationRef.current?.scrollIntoView({
    block: 'nearest',
    behavior: 'smooth',
  });
}, [selectedLocationId]);
```

Không luôn dùng `block: 'center'`.

Không chạy animation lặp vô hạn.

---

# 23. Scrollbar

Ví dụ:

```css
.public-gallery-area-list,
.public-gallery-location-list {
  scrollbar-width: thin;
  scrollbar-color:
    rgba(255, 255, 255, 0.45)
    rgba(255, 255, 255, 0.08);
}

.public-gallery-area-list::-webkit-scrollbar,
.public-gallery-location-list::-webkit-scrollbar {
  width: 6px;
}

.public-gallery-area-list::-webkit-scrollbar-thumb,
.public-gallery-location-list::-webkit-scrollbar-thumb {
  background: rgba(255, 255, 255, 0.42);
  border-radius: 999px;
}

.public-gallery-area-list::-webkit-scrollbar-track,
.public-gallery-location-list::-webkit-scrollbar-track {
  background: rgba(255, 255, 255, 0.08);
}
```

Không ẩn scrollbar hoàn toàn trên desktop.

---

# 24. Responsive

## 24.1. Tablet

```text
- Area sidebar có thể thu gọn.
- Location panel có thể overlay/drawer.
- Mỗi panel vẫn max-height theo viewport.
- List bên trong overflow-y-auto.
```

## 24.2. Mobile

```text
- Area list dùng drawer.
- Location list dùng drawer hoặc step tiếp theo.
- max-height = viewport trừ header và safe area.
- Không làm body dài theo số item.
- Item đủ lớn để tap.
- Scroll cảm ứng hoạt động.
```

Dùng `100dvh`, không chỉ `100vh`, để xử lý mobile browser chrome tốt hơn.

---

# PHẦN C — NHỮNG PHẦN KHÔNG ĐƯỢC THAY ĐỔI

# 25. Ngoài scope

Không thay đổi:

```text
1. Quan hệ area → location → gallery item.
2. Rule một location có nhiều gallery item.
3. item_type = MEDIA / VISIT_DELEGATION.
4. media_kind = IMAGE / VIDEO / MIXED.
5. Area cover.
6. Location cover.
7. Google Drive routing hiện tại.
8. EverAI TTS.
9. Description tối đa 1000 ký tự.
10. Campus selection.
11. Area Showcase background.
12. Location Showcase background.
13. PUBLISHED/HIDDEN.
14. Staff Leader RBAC.
15. Campus scope.
16. Upload ảnh/video từ máy đang chạy.
17. Public API không liên quan.
18. Database seed hiện tại.
```

YouTube chỉ là một nguồn video mới.

Scroll chỉ sửa layout Public Gallery.

---

# 26. Acceptance Criteria — YouTube

## AC-YT-01 — Thêm video YouTube

```text
Given Staff Leader đang tạo Gallery Item
When nhập URL YouTube hợp lệ
And submit form
Then hệ thống tạo media VIDEO cho Gallery Item
And không upload video lên Google Drive
And media được lưu bằng metadata external.
```

## AC-YT-02 — Public phát YouTube

```text
Given Gallery Item có YouTube media ACTIVE
When public user xem media đó
Then frontend render YouTube iframe
And video được xem trong PEMS
And frontend không dùng thẻ video với YouTube URL.
```

## AC-YT-03 — URL sai

```text
Given URL không thuộc YouTube
When submit
Then backend reject
And trả error code rõ ràng
And không tạo files/gallery_item_media rác.
```

## AC-YT-04 — media_kind MIXED

```text
Given Gallery Item có ảnh active
And có YouTube video active
Then media_kind = MIXED.
```

## AC-YT-05 — media_kind VIDEO

```text
Given Gallery Item chỉ có uploaded video và/hoặc YouTube video
Then media_kind = VIDEO.
```

## AC-YT-06 — Primary YouTube

```text
Given YouTube media được chọn primary
Then Gallery Item có đúng 1 primary media
And public thumbnail dùng YouTube thumbnail.
```

## AC-YT-07 — Video unavailable

```text
Given video bị xóa/private/không embed được
When public user mở Gallery
Then Gallery page vẫn hoạt động
And hiển thị fallback “Video hiện không khả dụng”.
```

## AC-YT-08 — Edit

```text
Given Gallery Item đã có YouTube media
When Staff Leader edit
Then có thể giữ, xóa, thêm YouTube media
And có thể đổi primary
And sau submit vẫn còn ít nhất 1 media.
```

---

# 27. Acceptance Criteria — Scroll Public Gallery

## AC-SCROLL-01 — Area dài

```text
Given campus có 24 areas
When user mở Public Gallery
Then Area sidebar nằm trong viewport
And user cuộn được tới area cuối
And area cuối click được.
```

## AC-SCROLL-02 — Location dài

```text
Given một area có 20 locations
When user chọn area
Then Location panel nằm trong viewport
And user cuộn được tới location cuối
And location cuối click được.
```

## AC-SCROLL-03 — Scroll độc lập

```text
When user cuộn Area list
Then Location list và Viewer không di chuyển.

When user cuộn Location list
Then Area list và Viewer không di chuyển.
```

## AC-SCROLL-04 — Active item

```text
Given selected area/location nằm ngoài vùng nhìn thấy
When selected ID thay đổi
Then frontend scrollIntoView với block = nearest
And item active xuất hiện trong viewport.
```

## AC-SCROLL-05 — Panel không bị cắt

```text
Given active area nằm gần cuối danh sách
When Location panel mở
Then panel vẫn bắt đầu từ vị trí layout cố định
And không bị đẩy xuống theo row active
And không bị cắt ở đáy viewport.
```

---

# 28. Test tối thiểu

## 28.1. Backend YouTube

```text
1. watch?v URL.
2. youtu.be URL.
3. Shorts URL.
4. embed URL.
5. URL không phải YouTube.
6. Fake subdomain/domain.
7. URL chứa script/iframe.
8. Video ID invalid.
9. Multiple YouTube URLs.
10. Duplicate URL trong cùng request.
11. Create chỉ YouTube video.
12. Create ảnh + YouTube.
13. Create uploaded video + YouTube.
14. Edit giữ YouTube media.
15. Edit xóa YouTube media.
16. Edit thêm YouTube media.
17. Primary existing media.
18. Primary uploaded file.
19. Primary YouTube.
20. Không còn media sau edit → reject.
21. media_kind IMAGE.
22. media_kind VIDEO.
23. media_kind MIXED.
24. File content endpoint nhận YouTube file_id → controlled error.
25. Transaction rollback khi đăng ký một URL thất bại.
```

## 28.2. Frontend quản lý Gallery

```text
1. Chuyển source upload/YouTube.
2. Preview YouTube.
3. Thêm nhiều URL.
4. Remove URL.
5. Select primary.
6. Edit media YouTube cũ.
7. Error message URL sai.
8. Loading state khi kiểm tra URL.
9. Submit multipart đúng contract.
10. Không mất file upload khi thêm URL YouTube.
```

## 28.3. Public media

```text
1. Uploaded image.
2. Uploaded video.
3. YouTube video.
4. YouTube primary.
5. YouTube thumbnail lỗi.
6. YouTube player lỗi.
7. Fullscreen.
8. Responsive 16:9.
9. CSP không chặn iframe.
10. Không autoplay mặc định.
```

## 28.4. Public scroll

```text
1. 1 area.
2. 24 areas.
3. 1 location.
4. 20 locations.
5. Active area đầu.
6. Active area giữa.
7. Active area cuối.
8. Active location đầu.
9. Active location cuối.
10. Desktop lớn.
11. Laptop chiều cao thấp.
12. Tablet.
13. Mobile.
14. Browser zoom 125%.
15. Browser zoom 150%.
16. Mouse wheel.
17. Trackpad.
18. Touch scroll.
19. Collapse/expand sidebar.
20. Reload route có selected area/location.
```

---

# 29. Build và kiểm tra

Sau khi sửa:

```bash
dotnet build
npm run build
```

Nếu project có test:

```bash
dotnet test
npm test
```

Kiểm tra browser console:

```text
- Không có CSP error YouTube.
- Không có mixed content.
- Không có iframe blocked do domain sai.
- Không có request /api/files/{youtubeFileId}/content.
- Không có React key warning.
- Không có infinite useEffect/scroll loop.
```

---

# 30. Deliverables bắt buộc

AI Agent phải báo cáo:

```text
1. Database/schema có sửa hay không.
2. SQL patch nào được tạo.
3. Backend files changed.
4. Frontend files changed.
5. Config/CSP files changed.
6. API contract trước/sau.
7. Cách phân biệt UPLOADED_FILE và YOUTUBE.
8. Cách tính media_kind.
9. Cách primary media hoạt động.
10. Cách Area/Location scroll hoạt động.
11. Build backend result.
12. Build frontend result.
13. Test result.
14. Những phần chưa thể xác nhận runtime.
```

Không báo “hoàn thành” nếu chưa build hoặc chưa nói rõ lý do không chạy được.

---

# 31. Definition of Done

Task chỉ được coi là hoàn thành khi:

```text
[ ] Video upload từ máy vẫn hoạt động.
[ ] Video YouTube thêm được bằng URL.
[ ] Không tải YouTube video về PEMS.
[ ] Không upload YouTube video lên Google Drive.
[ ] YouTube metadata được lưu đúng relation files → gallery_item_media.
[ ] Public Gallery render YouTube iframe.
[ ] CSP cho phép đúng domain YouTube.
[ ] media_kind tính đúng.
[ ] Primary media hoạt động với YouTube.
[ ] Edit keep/add/remove YouTube hoạt động.
[ ] Area list dài cuộn được.
[ ] Location list dài cuộn được.
[ ] Hai vùng scroll độc lập.
[ ] Location panel không bị cắt theo row active.
[ ] Active item tự scroll vào vùng nhìn thấy.
[ ] Responsive không vỡ.
[ ] Backend build xanh.
[ ] Frontend build xanh.
[ ] Không mock data.
[ ] Không sinh file rác.
[ ] Không phá flow Gallery/TTS/Google Drive hiện tại.
```
