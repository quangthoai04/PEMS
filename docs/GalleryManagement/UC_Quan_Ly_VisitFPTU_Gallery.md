# ĐẶC TẢ USE CASE — QUẢN LÝ VisitFPTU Gallery

## 1. Phạm vi đặc tả

Tài liệu này mô tả chức năng **Quản lý VisitFPTU Gallery** theo giao diện hiện tại, bao gồm:

- View list gallery
- Search gallery
- Filter gallery
- Thêm mới gallery item / upload media
- Enable gallery item
- Disable gallery item
- View detail gallery item
- Edit gallery item

Chưa triển khai trong đặc tả này:

- Quản lý khu vực / tòa / vị trí cụ thể
- Thêm mới khu vực
- Sửa khu vực
- Enable / disable khu vực
- Xóa gallery item vĩnh viễn
- Photo face tag
- Public Gallery page

Trang **Quản lý khu vực** sẽ là UC riêng, dùng bảng `gallery_areas` và `gallery_locations`.

---

## 2. Actor và phân quyền

### 2.1 Primary Actor

**Staff Leader**

Điều kiện runtime:

```text
role_code = STAFF
sub_role = LEADER
status = ACTIVE
primary_campus_id IS NOT NULL
```

Staff Leader chỉ quản lý gallery trong campus của mình.

Ví dụ Staff Leader Hà Nội chỉ được xem/thêm/sửa/ẩn/hiện gallery item thuộc `campus_id` Hà Nội.

### 2.2 Secondary Actor

Không có actor thao tác trực tiếp trong UC này.

### 2.3 Forbidden Actors

Các role sau không được gọi API quản lý Gallery nếu backend không có policy riêng cho họ:

```text
VISITOR
STUDENT
DEPARTMENT LEADER
DEPARTMENT STAFF
ADMIN
HO
IC STAFF
```

Ghi chú:

- Public user / Visitor chỉ xem gallery public ở UC public riêng.
- Admin không phải business super-admin để quản lý nội dung gallery.
- HO không quản lý gallery campus trong UC này.
- IC Staff nếu sau này muốn cho upload cần bổ sung rule riêng; hiện tại chốt Staff Leader.

---

## 3. Bảng database liên quan

Theo SQL mới nhất, chức năng này dùng các bảng:

```text
campuses
gallery_areas
gallery_locations
gallery_items
gallery_item_media
files
users
audit_logs / audit_log_changes nếu hệ thống đang ghi audit
```

Không dùng bảng cũ:

```text
galleries
gallery_images
```

Không dùng trong scope này:

```text
photo_face_tags
```

---

## 4. Mô hình quan hệ dữ liệu

```text
campuses
  └── gallery_areas
        └── gallery_locations
              └── gallery_items
                    └── gallery_item_media
                          └── files
```

Ý nghĩa:

- `gallery_areas`: khu vực/tòa/khu lớn, ví dụ `TỔNG QUAN`, `TÒA DELTA`.
- `gallery_locations`: vị trí cụ thể trong khu vực, ví dụ `Thư viện`, `Toàn cảnh Hola Park`.
- `gallery_items`: một item trên bảng quản lý gallery, ví dụ `Khuôn viên flycam xịn`.
- `gallery_item_media`: danh sách file ảnh/video thuộc item.
- `files`: metadata file dùng chung, file thật nằm trên Google Drive.

---

## 5. State machine

### 5.1 `gallery_items.status`

```text
PUBLISHED → HIDDEN
HIDDEN → PUBLISHED
```

Ý nghĩa UI:

| DB status | UI label | Public Gallery |
|---|---|---|
| `PUBLISHED` | Hiển thị | Có thể hiển thị nếu area/location/media cũng hợp lệ |
| `HIDDEN` | Đã ẩn / Ngừng hiển thị | Không hiển thị public |

Toggle ở trang quản lý Gallery chỉ update:

```text
gallery_items.status
```

Không update:

```text
gallery_areas.status
gallery_locations.status
gallery_item_media.status
```

### 5.2 `gallery_item_media.status`

```text
ACTIVE → HIDDEN
HIDDEN → ACTIVE
```

Trong phase hiện tại, UI không có toggle riêng cho từng media. Khi edit thay file, backend có thể soft delete hoặc hide media cũ tùy cách code. Rule bắt buộc là mỗi gallery item phải còn ít nhất một media đang dùng.

### 5.3 Effective public visibility

Một gallery item chỉ được hiển thị ở public Gallery khi thỏa tất cả:

```text
gallery_items.status = PUBLISHED
gallery_items.deleted_at IS NULL
gallery_locations.status = ACTIVE
gallery_areas.status = ACTIVE
gallery_item_media.status = ACTIVE
gallery_item_media.deleted_at IS NULL
```

---

## 6. UC-GAL-01 — View List Gallery

### 6.1 Mục tiêu

Staff Leader xem danh sách gallery item trong campus của mình.

### 6.2 Endpoint đề xuất

```http
GET /api/gallery-management/items
```

Query params:

```text
keyword
areaId
locationId
mediaKind
status
page
pageSize
sortBy
sortDirection
```

Không nhận `campusId` từ frontend để tránh giả mạo campus. Backend lấy campus từ current user.

### 6.3 Main Flow

1. Staff Leader mở trang `/dashboard/gallery`.
2. Frontend gọi API list gallery.
3. Backend xác thực user.
4. Backend kiểm tra user là Staff Leader active.
5. Backend lấy `currentUser.primary_campus_id`.
6. Backend query `gallery_items` join `gallery_locations`, `gallery_areas`.
7. Backend chỉ trả item thuộc campus của Staff Leader.
8. Frontend hiển thị bảng với các cột:
   - STT
   - Khu vực
   - Vị trí cụ thể
   - Tiêu đề
   - Định dạng
   - Trạng thái
   - Ngày tạo
   - Hành động

### 6.4 Data trả về cho list item

```json
{
  "galleryItemId": 1,
  "areaId": 1,
  "areaName": "TỔNG QUAN",
  "locationId": 1,
  "locationName": "Toàn cảnh Hola Park",
  "title": "Khuôn viên flycam xịn",
  "description": "Toàn cảnh campus siêu đẹp xanh biếc.",
  "mediaKind": "VIDEO",
  "status": "PUBLISHED",
  "createdAt": "2026-05-28T00:00:00",
  "createdByName": "Nguyễn Văn A",
  "primaryMedia": {
    "mediaId": 10,
    "fileId": 100,
    "mediaType": "VIDEO",
    "fileUrl": "/api/files/100/content",
    "thumbnailUrl": "/api/files/101/content"
  }
}
```

### 6.5 Query logic

```sql
SELECT
    gi.gallery_item_id,
    ga.area_id,
    ga.area_name,
    gl.location_id,
    gl.location_name,
    gi.title,
    gi.description,
    gi.media_kind,
    gi.status,
    gi.created_at,
    pm.media_id AS primary_media_id,
    pm.media_type AS primary_media_type,
    f.file_id,
    f.thumbnail_url
FROM gallery_items gi
JOIN gallery_locations gl
    ON gl.location_id = gi.location_id
JOIN gallery_areas ga
    ON ga.area_id = gl.area_id
LEFT JOIN gallery_item_media pm
    ON pm.gallery_item_id = gi.gallery_item_id
   AND pm.is_primary = 1
   AND pm.deleted_at IS NULL
LEFT JOIN files f
    ON f.file_id = pm.file_id
WHERE ga.campus_id = @currentUserCampusId
  AND gi.deleted_at IS NULL
ORDER BY gi.created_at DESC, gi.gallery_item_id DESC;
```

---

## 7. UC-GAL-02 — Search / Filter Gallery

### 7.1 Mục tiêu

Staff Leader tìm kiếm và lọc gallery item theo tiêu đề, mô tả, khu vực, vị trí, định dạng, trạng thái.

### 7.2 Search fields

Ô search trên UI tìm theo:

```text
gallery_items.title
gallery_items.description
gallery_areas.area_name
gallery_locations.location_name
```

Search nên:

- Trim keyword.
- Không phân biệt hoa thường.
- Hỗ trợ tiếng Việt có dấu.
- Không trả dữ liệu ngoài campus của Staff Leader.

### 7.3 Filter fields

| UI filter | DB |
|---|---|
| Tất cả khu vực | `gallery_areas.area_id` |
| Tất cả vị trí cụ thể | `gallery_locations.location_id` |
| Loại | `gallery_items.media_kind` |
| Trạng thái | `gallery_items.status` |

Giá trị `mediaKind`:

```text
IMAGE
VIDEO
MIXED
```

Giá trị `status`:

```text
PUBLISHED
HIDDEN
```

UI label:

| DB value | UI |
|---|---|
| `IMAGE` | Hình ảnh |
| `VIDEO` | Video |
| `MIXED` | Hỗn hợp |
| `PUBLISHED` | Hiển thị |
| `HIDDEN` | Đã ẩn |

### 7.4 Main Flow

1. Staff Leader nhập keyword hoặc chọn filter.
2. Frontend debounce search 300–500 ms.
3. Frontend gọi lại API list với params.
4. Backend áp dụng campus scope trước hoặc trong cùng query.
5. Backend trả kết quả phân trang.
6. Nếu không có kết quả, UI hiển thị empty state: `Không tìm thấy media phù hợp.`

### 7.5 Business Rules

```text
BR-GAL-SEARCH-01: Search không được bỏ qua campus scope.
BR-GAL-SEARCH-02: Filter location chỉ hợp lệ nếu location thuộc area/campus của current user.
BR-GAL-SEARCH-03: Nếu keyword rỗng sau trim thì không áp dụng keyword filter.
BR-GAL-SEARCH-04: Sort mặc định là created_at DESC, gallery_item_id DESC.
```

---

## 8. UC-GAL-03 — View Detail Gallery

### 8.1 Mục tiêu

Staff Leader xem chi tiết một gallery item, gồm ảnh/video preview, danh sách media con, tiêu đề, mô tả, khu vực, vị trí, trạng thái, ngày tạo.

### 8.2 Endpoint đề xuất

```http
GET /api/gallery-management/items/{galleryItemId}
```

### 8.3 Main Flow

1. Staff Leader bấm icon mắt ở dòng gallery item.
2. Frontend gọi API detail.
3. Backend kiểm tra item tồn tại.
4. Backend kiểm tra item thuộc campus của Staff Leader.
5. Backend lấy toàn bộ media đang dùng:
   - `gallery_item_media.deleted_at IS NULL`
   - `gallery_item_media.status = ACTIVE`
6. Backend trả detail.
7. Frontend mở modal detail.

### 8.4 Response đề xuất

```json
{
  "galleryItemId": 1,
  "title": "Khuôn viên flycam xịn",
  "description": "Toàn cảnh campus siêu đẹp xanh biếc.",
  "status": "PUBLISHED",
  "mediaKind": "VIDEO",
  "area": {
    "areaId": 1,
    "areaName": "TỔNG QUAN"
  },
  "location": {
    "locationId": 1,
    "locationName": "Toàn cảnh Hola Park"
  },
  "campus": {
    "campusId": 1,
    "campusCode": "HN",
    "campusName": "FPTU Hà Nội"
  },
  "createdAt": "2026-05-28T00:00:00",
  "createdByName": "Nguyễn Văn A",
  "updatedAt": null,
  "updatedByName": null,
  "media": [
    {
      "mediaId": 10,
      "fileId": 100,
      "mediaType": "VIDEO",
      "fileUrl": "/api/files/100/content",
      "thumbnailUrl": "/api/files/101/content",
      "isPrimary": true,
      "caption": null,
      "altText": null,
      "displayOrder": 0
    }
  ]
}
```

### 8.5 Alternative Flows

```text
AF-GAL-DETAIL-01: Item không tồn tại hoặc đã deleted_at khác NULL
→ HTTP 404.

AF-GAL-DETAIL-02: Item thuộc campus khác
→ HTTP 403.

AF-GAL-DETAIL-03: Item không còn media active
→ vẫn trả detail, nhưng UI hiển thị warning nội bộ: "Media này chưa có file khả dụng."
```

---

## 9. UC-GAL-04 — Add Gallery Item / Upload Media

### 9.1 Mục tiêu

Staff Leader thêm mới gallery item kèm một hoặc nhiều file ảnh/video lên Google Drive.

### 9.2 Endpoint đề xuất

```http
POST /api/gallery-management/items
Content-Type: multipart/form-data
```

Form fields:

```text
title
description
locationId
status
files[]
```

Optional:

```text
caption
altText
```

Không nên để frontend gửi:

```text
campusId
areaName
locationName
mediaKind
fileId
uploadedBy
createdBy
```

Backend tự suy ra các field này.

### 9.3 Input rules

| Field | Rule |
|---|---|
| `title` | Required, trim, 1–255 ký tự |
| `description` | Required, trim, không được rỗng vì DB là `TEXT NOT NULL` |
| `locationId` | Required, phải tồn tại, thuộc campus hiện tại, location ACTIVE, area ACTIVE |
| `status` | Optional, chỉ nhận `PUBLISHED` hoặc `HIDDEN`; default `PUBLISHED` |
| `files[]` | Required khi tạo mới; tối thiểu 1 file, tối đa 5 file |
| file image | JPG/JPEG/PNG/WEBP, dùng `FilePurpose.GalleryImage` |
| file video | MP4/WEBM, dùng `FilePurpose.GalleryVideo` |

Ghi chú quan trọng về UI hiện tại:

```text
UI screenshot đang ghi "Mô tả (không bắt buộc)" nhưng SQL mới nhất yêu cầu gallery_items.description TEXT NOT NULL.
Do đó UI phải đổi label thành "Mô tả *" hoặc backend phải reject nếu description rỗng.
```

### 9.4 Media kind rule

Backend không tin hoàn toàn vào dropdown định dạng của frontend. Backend tự xác định `media_kind` từ file upload:

```text
Toàn bộ file là ảnh   → media_kind = IMAGE
Toàn bộ file là video → media_kind = VIDEO
Có cả ảnh và video    → media_kind = MIXED
```

Nếu UI hiện tại chỉ cho chọn một loại:

```text
Định dạng = Hình ảnh → chỉ cho upload image.
Định dạng = Video    → chỉ cho upload video.
```

Nếu người dùng chọn `Hình ảnh` nhưng upload MP4, frontend phải báo lỗi sớm và backend vẫn phải reject.

### 9.5 Google Drive upload rule

Trong handler nghiệp vụ:

- Inject `IFileUploadService`.
- Với ảnh dùng `FilePurpose.GalleryImage`.
- Với video dùng `FilePurpose.GalleryVideo`.
- Không gọi trực tiếp `IGoogleDriveStorageService`.
- Không tự hard-code folderId.
- Không tự insert metadata vào `files`.

Pseudo flow:

```text
1. Validate role/scope/input.
2. Validate location thuộc campus hiện tại và đang ACTIVE.
3. Validate files không rỗng.
4. Với từng file:
   - Gọi IFileUploadService.UploadBusinessFileAsync(...)
   - Lấy uploaded.FileId
5. Tạo gallery_items.
6. Tạo gallery_item_media cho từng uploaded file.
7. Set is_primary = 1 cho file đầu tiên.
8. Set media_kind theo danh sách file.
9. Trả response detail.
```

### 9.6 Business Rules

```text
BR-GAL-ADD-01: Chỉ Staff Leader active được thêm gallery item.
BR-GAL-ADD-02: Không cho thêm gallery vào location thuộc campus khác.
BR-GAL-ADD-03: Không cho thêm gallery vào area/location INACTIVE.
BR-GAL-ADD-04: Description là bắt buộc.
BR-GAL-ADD-05: Tạo mới phải có ít nhất 1 media.
BR-GAL-ADD-06: Tối đa 5 media cho 1 lần tạo theo UI hiện tại.
BR-GAL-ADD-07: Backend tự tính media_kind.
BR-GAL-ADD-08: File upload dùng Google Drive upload foundation dùng chung.
BR-GAL-ADD-09: File đầu tiên được set is_primary = 1.
BR-GAL-ADD-10: Gallery item mới default PUBLISHED nếu request không truyền status.
BR-GAL-ADD-11: Sau khi tạo thành công phải ghi created_by, created_at và audit log nếu hệ thống đang dùng audit.
```

### 9.7 Alternative Flows

```text
AF-GAL-ADD-01: User không phải Staff Leader
→ HTTP 403.

AF-GAL-ADD-02: Location không tồn tại
→ HTTP 404.

AF-GAL-ADD-03: Location thuộc campus khác
→ HTTP 403.

AF-GAL-ADD-04: Area hoặc location INACTIVE
→ HTTP 409 hoặc 422, message: "Vị trí này đang ngừng hoạt động, không thể upload media mới."

AF-GAL-ADD-05: Thiếu title/description/file
→ HTTP 422.

AF-GAL-ADD-06: File sai định dạng hoặc quá dung lượng
→ HTTP 422, trả mã FILE_INVALID_EXTENSION / FILE_INVALID_TYPE / FILE_TOO_LARGE.

AF-GAL-ADD-07: Google Drive chưa cấu hình folder
→ HTTP 422 hoặc 500 theo error handling hiện có, mã GOOGLE_DRIVE_FOLDER_NOT_CONFIGURED.

AF-GAL-ADD-08: Upload thành công nhưng lưu nghiệp vụ lỗi
→ rollback DB transaction; nếu có cơ chế cleanup file ngoài DB thì gọi cleanup, nếu chưa có thì ghi log orphan file để xử lý sau.
```

---

## 10. UC-GAL-05 — Enable Gallery Item

### 10.1 Mục tiêu

Staff Leader bật lại gallery item đã ẩn để item có thể xuất hiện ở public Gallery.

### 10.2 Endpoint đề xuất

```http
PATCH /api/gallery-management/items/{galleryItemId}/status
```

Body:

```json
{
  "status": "PUBLISHED"
}
```

### 10.3 Main Flow

1. Staff Leader bật toggle ở dòng gallery item.
2. Frontend gọi API update status.
3. Backend kiểm tra item tồn tại và thuộc campus của Staff Leader.
4. Backend kiểm tra item còn ít nhất 1 media active.
5. Backend set `gallery_items.status = PUBLISHED`.
6. Backend set `updated_by`, `updated_at`.
7. Backend ghi audit log.
8. Frontend cập nhật badge thành `Hiển thị`.

### 10.4 Business Rules

```text
BR-GAL-ENABLE-01: Enable chỉ update gallery_items.status.
BR-GAL-ENABLE-02: Không tự update gallery_areas.status hoặc gallery_locations.status.
BR-GAL-ENABLE-03: Nếu area/location đang INACTIVE thì vẫn có thể set item = PUBLISHED trong nội bộ, nhưng public Gallery vẫn không hiển thị do effective visibility không đạt.
BR-GAL-ENABLE-04: Không cho enable nếu item không còn media active.
```

---

## 11. UC-GAL-06 — Disable Gallery Item

### 11.1 Mục tiêu

Staff Leader ẩn gallery item khỏi public Gallery nhưng không xóa dữ liệu.

### 11.2 Endpoint đề xuất

```http
PATCH /api/gallery-management/items/{galleryItemId}/status
```

Body:

```json
{
  "status": "HIDDEN"
}
```

### 11.3 Main Flow

1. Staff Leader tắt toggle ở dòng gallery item.
2. Frontend gọi API update status.
3. Backend kiểm tra scope.
4. Backend set `gallery_items.status = HIDDEN`.
5. Backend set `updated_by`, `updated_at`.
6. Backend ghi audit log.
7. Frontend cập nhật badge thành `Đã ẩn`.

### 11.4 Business Rules

```text
BR-GAL-DISABLE-01: Disable không xóa file Google Drive.
BR-GAL-DISABLE-02: Disable không xóa row gallery_item_media.
BR-GAL-DISABLE-03: Disable không update media.status.
BR-GAL-DISABLE-04: Disable không update area/location.
BR-GAL-DISABLE-05: Public API phải loại item HIDDEN khỏi kết quả.
```

---

## 12. UC-GAL-07 — Edit Gallery Item

### 12.1 Mục tiêu

Staff Leader chỉnh sửa metadata gallery item và có thể thay thế/thêm/bớt file media.

### 12.2 Endpoint đề xuất

```http
PUT /api/gallery-management/items/{galleryItemId}
Content-Type: multipart/form-data
```

Form fields:

```text
title
description
locationId
keepMediaIds[]
newFiles[]
primaryMediaId
```

Có thể tách thành 2 endpoint nếu muốn code sạch hơn:

```http
PUT /api/gallery-management/items/{galleryItemId}
POST /api/gallery-management/items/{galleryItemId}/media
DELETE /api/gallery-management/items/{galleryItemId}/media/{mediaId}
PATCH /api/gallery-management/items/{galleryItemId}/media/{mediaId}/primary
```

Nhưng với UI hiện tại, có thể làm một endpoint edit tổng hợp.

### 12.3 Input rules

| Field | Rule |
|---|---|
| `title` | Required, 1–255 ký tự sau trim |
| `description` | Required, không rỗng |
| `locationId` | Required, active, thuộc campus hiện tại |
| `keepMediaIds[]` | Optional, danh sách media cũ muốn giữ lại |
| `newFiles[]` | Optional khi edit, nhưng tổng media sau edit phải >= 1 |
| `primaryMediaId` | Optional; nếu không truyền thì lấy media đầu tiên còn active |

### 12.4 Main Flow

1. Staff Leader bấm `Chỉnh sửa` trong detail modal.
2. Frontend mở modal edit.
3. Staff Leader sửa tiêu đề/mô tả/khu vực/vị trí hoặc chọn file mới.
4. Staff Leader bấm `Lưu thay đổi`.
5. Backend kiểm tra item tồn tại.
6. Backend kiểm tra item thuộc campus của Staff Leader.
7. Backend validate input.
8. Backend validate location mới thuộc campus hiện tại và active.
9. Backend xử lý media:
   - Giữ media có trong `keepMediaIds`.
   - Media cũ không còn trong `keepMediaIds` thì set `deleted_at/deleted_by` hoặc `status = HIDDEN`.
   - File mới thì upload qua `IFileUploadService`.
   - Insert `gallery_item_media` cho file mới.
   - Đảm bảo còn ít nhất 1 media active.
   - Đảm bảo chỉ có 1 media `is_primary = 1`.
10. Backend update `gallery_items.title`, `description`, `location_id`, `media_kind`, `updated_by`, `updated_at`.
11. Backend ghi audit log.
12. Frontend reload detail/list.

### 12.5 Business Rules

```text
BR-GAL-EDIT-01: Edit không đổi gallery_item_id.
BR-GAL-EDIT-02: Edit không cho chuyển item sang campus khác.
BR-GAL-EDIT-03: Edit location chỉ được chọn location ACTIVE thuộc campus current user.
BR-GAL-EDIT-04: Description bắt buộc.
BR-GAL-EDIT-05: Nếu thay file, file mới vẫn phải qua Google Drive upload foundation.
BR-GAL-EDIT-06: Sau edit, item phải còn ít nhất 1 media active.
BR-GAL-EDIT-07: Sau edit, mỗi item chỉ có 1 primary media.
BR-GAL-EDIT-08: media_kind phải được tính lại theo media active còn lại.
BR-GAL-EDIT-09: Edit metadata không tự đổi status PUBLISHED/HIDDEN.
BR-GAL-EDIT-10: Nếu item đang HIDDEN, edit xong vẫn HIDDEN.
BR-GAL-EDIT-11: Nếu item đang PUBLISHED, edit xong vẫn PUBLISHED và public view phản ánh dữ liệu mới.
```

### 12.6 Alternative Flows

```text
AF-GAL-EDIT-01: Item không tồn tại
→ HTTP 404.

AF-GAL-EDIT-02: Item thuộc campus khác
→ HTTP 403.

AF-GAL-EDIT-03: Xóa hết media cũ mà không upload media mới
→ HTTP 422, message: "Gallery item phải có ít nhất một file media."

AF-GAL-EDIT-04: File mới sai định dạng/kích thước
→ HTTP 422.

AF-GAL-EDIT-05: primaryMediaId không thuộc item
→ HTTP 422.

AF-GAL-EDIT-06: Concurrent update/stale data nếu có row version hoặc updated_at check
→ HTTP 409.
```

---

## 13. DTO đề xuất

### 13.1 `GalleryItemListQuery`

```csharp
public sealed record GalleryItemListQuery(
    string? Keyword,
    long? AreaId,
    long? LocationId,
    string? MediaKind,
    string? Status,
    int Page = 1,
    int PageSize = 10,
    string? SortBy = "createdAt",
    string? SortDirection = "desc"
) : IRequest<PagedResult<GalleryItemListItemDto>>;
```

### 13.2 `GalleryItemListItemDto`

```csharp
public sealed class GalleryItemListItemDto
{
    public long GalleryItemId { get; init; }
    public long AreaId { get; init; }
    public string AreaName { get; init; } = string.Empty;
    public long LocationId { get; init; }
    public string LocationName { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string MediaKind { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
    public GalleryPrimaryMediaDto? PrimaryMedia { get; init; }
}
```

### 13.3 `CreateGalleryItemCommand`

```csharp
public sealed record CreateGalleryItemCommand(
    string Title,
    string Description,
    long LocationId,
    string? Status,
    IReadOnlyList<GalleryUploadFileCommandDto> Files
) : IRequest<GalleryItemDetailDto>;
```

### 13.4 `GalleryUploadFileCommandDto`

```csharp
public sealed record GalleryUploadFileCommandDto(
    byte[] Content,
    string FileName,
    string? ContentType,
    long FileSize,
    string? Caption,
    string? AltText
);
```

### 13.5 `UpdateGalleryItemCommand`

```csharp
public sealed record UpdateGalleryItemCommand(
    long GalleryItemId,
    string Title,
    string Description,
    long LocationId,
    IReadOnlyList<long> KeepMediaIds,
    IReadOnlyList<GalleryUploadFileCommandDto> NewFiles,
    long? PrimaryMediaId
) : IRequest<GalleryItemDetailDto>;
```

### 13.6 `ChangeGalleryItemStatusCommand`

```csharp
public sealed record ChangeGalleryItemStatusCommand(
    long GalleryItemId,
    string Status
) : IRequest<GalleryItemDetailDto>;
```

---

## 14. Backend implementation notes

### 14.1 Clean Architecture

Controller chỉ nhận request, map `IFormFile` sang command và gọi MediatR.

Không đặt logic nghiệp vụ trong controller.

Handler chịu trách nhiệm:

```text
- Check current user
- Check Staff Leader role
- Check campus scope
- Check DB existence
- Check status hợp lệ
- Gọi IFileUploadService
- Insert/update gallery_items
- Insert/update gallery_item_media
- Audit log nếu hệ thống đang dùng
```

### 14.2 Role helper

Nên dùng helper/constant hiện có:

```text
RoleCodes.Staff
SubRoles.Leader
```

Không dùng:

```text
STAFF_L
STAFF_P
STAFF_LEADER as role_code
DEPT
```

### 14.3 Scope helper

Tạo private helper hoặc service:

```csharp
private async Task<GalleryLocation> LoadActiveLocationInCurrentCampusAsync(
    long locationId,
    long currentCampusId,
    CancellationToken ct)
```

Điều kiện:

```sql
gallery_locations.location_id = @locationId
gallery_locations.status = 'ACTIVE'
gallery_areas.status = 'ACTIVE'
gallery_areas.campus_id = @currentCampusId
```

### 14.4 Media primary rule

SQL hiện tại có index `(gallery_item_id, is_primary)` nhưng index này không tự chặn nhiều primary. Backend bắt buộc đảm bảo:

```text
Một gallery item chỉ có một media is_primary = 1.
```

Nên code trong transaction:

```text
1. Set tất cả media của item is_primary = 0.
2. Set media được chọn is_primary = 1.
```

Nếu được phép chỉnh SQL, nên thêm generated column + unique key ở DB level trong patch riêng.

### 14.5 File purpose

Khi upload:

```text
Image → FilePurpose.GalleryImage
Video → FilePurpose.GalleryVideo
```

Không dùng `FilePurpose.Document` cho gallery.

---

## 15. Frontend implementation notes

### 15.1 Trang list

Route gợi ý:

```text
/dashboard/gallery
```

UI theo ảnh hiện tại:

- Breadcrumb: `Dashboard / Quản lý Gallery / Quản lý Gallery Hà Nội`
- Title: `VisitFPTU Gallery`
- Subtitle: `Quản lý tài nguyên hình ảnh và video`
- Button phụ: `Quản lý khu vực`
- Button chính: `Tải lên Media`
- Filter bar:
  - Search input
  - Area dropdown
  - Location dropdown
  - Type dropdown
  - Status dropdown
- Table:
  - STT
  - Khu vực
  - Vị trí cụ thể
  - Tiêu đề
  - Định dạng
  - Trạng thái
  - Ngày tạo
  - Hành động

### 15.2 Upload modal

UI hiện tại cần chỉnh:

```text
"Mô tả (không bắt buộc)" → "Mô tả *"
```

Vì DB yêu cầu `gallery_items.description NOT NULL`.

Fields:

```text
Tiêu đề *
Danh mục tòa/khu *
Vị trí thực tế *
Định dạng *
Mô tả *
Files *
```

Rule UX:

```text
- Tối đa 5 file.
- Không cho submit nếu chưa có file.
- Không cho submit nếu title rỗng.
- Không cho submit nếu description rỗng.
- Nếu chọn Hình ảnh thì chỉ accept image.
- Nếu chọn Video thì chỉ accept video.
- Hiển thị preview file đã chọn.
```

### 15.3 Detail modal

Detail modal hiển thị:

```text
- Preview media chính
- Thumbnail list
- Badge media kind
- Badge status
- Campus
- Area
- Location
- Title
- Description
- Created date
- Button Chỉnh sửa
```

### 15.4 Edit modal

Edit modal hiển thị dữ liệu hiện có:

```text
- Title
- Area
- Location
- Type
- Description
- Current files
- Upload replacement/additional files
```

Button:

```text
Hủy bỏ
Lưu thay đổi
```

Không cho lưu nếu sau edit không còn file nào.

---

## 16. Public Gallery query rule

Public API không thuộc trang quản lý này, nhưng backend quản lý status phải đảm bảo public query sau này dùng rule:

```sql
SELECT
    gi.gallery_item_id,
    ga.area_name,
    gl.location_name,
    gi.title,
    gi.description,
    gi.media_kind,
    gm.media_id,
    gm.media_type,
    f.file_id
FROM gallery_items gi
JOIN gallery_locations gl ON gl.location_id = gi.location_id
JOIN gallery_areas ga ON ga.area_id = gl.area_id
JOIN gallery_item_media gm ON gm.gallery_item_id = gi.gallery_item_id
JOIN files f ON f.file_id = gm.file_id
WHERE ga.status = 'ACTIVE'
  AND gl.status = 'ACTIVE'
  AND gi.status = 'PUBLISHED'
  AND gi.deleted_at IS NULL
  AND gm.status = 'ACTIVE'
  AND gm.deleted_at IS NULL;
```

---

## 17. Acceptance Criteria

### AC-GAL-01 — View list đúng campus

Given Staff Leader Hà Nội đăng nhập  
When mở trang VisitFPTU Gallery  
Then hệ thống chỉ hiển thị gallery item thuộc campus Hà Nội  
And không hiển thị item của campus khác.

### AC-GAL-02 — Search theo tiêu đề

Given có item `Khuôn viên flycam xịn`  
When nhập keyword `flycam`  
Then item đó xuất hiện trong kết quả.

### AC-GAL-03 — Search theo vị trí

Given có item thuộc vị trí `Thư viện`  
When nhập keyword `Thư viện`  
Then item đó xuất hiện trong kết quả.

### AC-GAL-04 — Filter theo khu vực

Given có item thuộc `TÒA DELTA` và item thuộc `TÒA ALPHA`  
When chọn filter `TÒA DELTA`  
Then chỉ item thuộc `TÒA DELTA` được hiển thị.

### AC-GAL-05 — Filter theo định dạng

Given có item IMAGE và item VIDEO  
When chọn `Video`  
Then chỉ item có `media_kind = VIDEO` được hiển thị.

### AC-GAL-06 — Add gallery item thành công

Given Staff Leader active  
And location active thuộc campus của Staff Leader  
When nhập title, description và upload file hợp lệ  
Then backend tạo một row `gallery_items`  
And tạo ít nhất một row `gallery_item_media`  
And tạo metadata file trong `files` qua upload service  
And item xuất hiện trong list.

### AC-GAL-07 — Add thiếu mô tả bị chặn

Given Staff Leader mở modal upload  
When để trống description và bấm upload  
Then frontend không cho submit hoặc backend trả HTTP 422  
And không tạo gallery item.

### AC-GAL-08 — Add vào location inactive bị chặn

Given location đang INACTIVE  
When Staff Leader upload media vào location đó  
Then backend trả HTTP 409 hoặc 422  
And không tạo gallery item.

### AC-GAL-09 — Disable gallery item

Given item đang PUBLISHED  
When Staff Leader tắt toggle  
Then `gallery_items.status = HIDDEN`  
And item không xuất hiện ở public Gallery.

### AC-GAL-10 — Enable gallery item

Given item đang HIDDEN và còn media active  
When Staff Leader bật toggle  
Then `gallery_items.status = PUBLISHED`  
And item có thể xuất hiện public nếu area/location active.

### AC-GAL-11 — Edit metadata

Given item đang tồn tại  
When Staff Leader sửa title/description/location hợp lệ  
Then gallery_item_id không đổi  
And updated_at/updated_by được cập nhật  
And list/detail hiển thị dữ liệu mới.

### AC-GAL-12 — Edit thay file

Given item có 1 media cũ  
When Staff Leader upload file mới và bỏ media cũ  
Then media cũ bị hidden/deleted_at hoặc không còn active  
And media mới được tạo qua upload service  
And item vẫn còn ít nhất 1 media active.

### AC-GAL-13 — Non-Staff-Leader bị chặn

Given user không phải Staff Leader  
When gọi API add/edit/enable/disable gallery  
Then backend trả HTTP 403  
And không thay đổi database.

---

## 18. Checklist cho AI Agent khi code

```text
[ ] Đọc SQL mới nhất và xác nhận dùng gallery_areas/gallery_locations/gallery_items/gallery_item_media.
[ ] Không dùng lại entity/table cũ galleries/gallery_images.
[ ] Không code quản lý khu vực trong scope này.
[ ] Tạo/cập nhật entity, enum, configuration, DbContext cho 4 bảng gallery mới nếu chưa có.
[ ] Tạo DTO/query/command/validator theo UC này.
[ ] Controller chỉ nhận request và gọi MediatR.
[ ] Handler kiểm tra role STAFF + LEADER.
[ ] Handler lấy campus từ current user, không tin campusId frontend.
[ ] List/search/filter áp dụng campus scope trong mọi query.
[ ] Upload dùng IFileUploadService.
[ ] Image dùng FilePurpose.GalleryImage.
[ ] Video dùng FilePurpose.GalleryVideo.
[ ] Không gọi trực tiếp IGoogleDriveStorageService.
[ ] Không hard-code Google Drive folder ID.
[ ] Không tự insert bảng files.
[ ] Tự tính media_kind từ media active.
[ ] Đảm bảo mỗi item có đúng 1 primary media.
[ ] Toggle chỉ update gallery_items.status.
[ ] Edit không tự đổi trạng thái PUBLISHED/HIDDEN.
[ ] UI đổi "Mô tả (không bắt buộc)" thành "Mô tả *".
[ ] Frontend validate file sớm nhưng backend vẫn validate cuối cùng.
[ ] Build backend.
[ ] Build frontend.
[ ] Test Postman cho list/search/add/detail/edit/enable/disable.
```
