# ĐẶC TẢ USE CASE — HIỂN THỊ PUBLIC VisitFPTU Gallery

## 1. Phạm vi UC

UC này mô tả chức năng **hiển thị VisitFPTU Gallery ở trang public** khi người dùng bấm menu header `Visit FPTU`.

Chức năng bao gồm:

- Click header `Visit FPTU` để mở trang Public Gallery.
- Chọn campus.
- Hiển thị toàn bộ khu vực thuộc campus đã chọn.
- Hover vào từng khu vực để hiện danh sách vị trí cụ thể.
- Click vào một vị trí cụ thể để xem gallery item tương ứng.
- Hiển thị title, breadcrumb `area > location`, description, media của gallery item.
- Chuyển ảnh/video trước sau trong một gallery item.
- Phóng to media.
- Chuyển vị trí trước/sau.
- Chuyển khu vực trước/sau.
- Bỏ nút `Xem 360` / không triển khai virtual tour 360.

Không nằm trong scope UC này:

- Quản lý Gallery.
- Quản lý khu vực.
- Upload media.
- Enable/disable gallery item.
- Enable/disable location.
- Sửa nội dung gallery item.
- Xóa gallery item.
- Face tag.
- 360 tour thật.

---

## 2. Actor

### 2.1 Primary Actor

```text
Public User
```

Bao gồm:

- Người chưa đăng nhập.
- Visitor đã đăng nhập.
- Internal user đã đăng nhập nhưng đang xem public page.

### 2.2 Secondary Actor

Không có.

### 2.3 Quyền truy cập

Trang này là public page, không yêu cầu đăng nhập.

Backend public API không được trả dữ liệu nội bộ, audit field nhạy cảm, user tạo, link Google Drive raw hoặc file metadata không cần thiết.

---

## 3. Mục tiêu nghiệp vụ

Trang **VisitFPTU Gallery** dùng để giới thiệu trực quan cơ sở FPT University theo từng campus.

Luồng trải nghiệm mong muốn:

```text
Header Visit FPTU
→ Chọn campus
→ Xem danh sách khu vực của campus
→ Hover khu vực để xem các vị trí cụ thể
→ Click vị trí cụ thể
→ Xem bài đăng gallery của vị trí đó
→ Xem media, chuyển ảnh/video, phóng to, chuyển vị trí/khu vực
```

Vì nghiệp vụ đã chốt:

```text
1 location = tối đa 1 gallery item
1 gallery item = title + description + media + status
```

Nên khi người dùng click vào một vị trí cụ thể, hệ thống sẽ load **tối đa 1 bài đăng gallery** của vị trí đó.

---

## 4. Bảng database liên quan

UC này dùng các bảng:

```text
campuses
gallery_areas
gallery_locations
gallery_items
gallery_item_media
files
```

Không dùng:

```text
photo_face_tags
users
audit_logs
visit_requests
```

---

## 5. Điều kiện hiển thị public

Một campus/khu vực/vị trí/gallery item chỉ được hiển thị public khi thỏa điều kiện:

```sql
campuses.status = 'ACTIVE'
gallery_areas.status = 'ACTIVE'
gallery_locations.status = 'ACTIVE'
gallery_items.status = 'PUBLISHED'
gallery_items.deleted_at IS NULL
gallery_item_media.status = 'ACTIVE'
gallery_item_media.deleted_at IS NULL
```

Vì đã chốt logic quản lý khu vực:

- Nếu location bị disable thì `gallery_locations.status = INACTIVE`.
- Khi disable location, nếu gallery item đang `PUBLISHED` thì backend quản lý đã set về `HIDDEN`.
- Khi enable location lại, gallery item vẫn `HIDDEN` cho đến khi Staff Leader bật lại item.

Do đó public API chỉ cần lọc đúng effective visibility, không cần xử lý lại logic disable/enable.

---

## 6. Route đề xuất

### 6.1 Public route

```text
/visit-fptu
```

Hoặc nếu project đang dùng route gallery:

```text
/gallery
```

Nhưng theo header hiện tại nên ưu tiên:

```text
/visit-fptu
```

### 6.2 Route có campus

```text
/visit-fptu?campus=HN
```

Hoặc:

```text
/visit-fptu/HN
```

### 6.3 Route có location

```text
/visit-fptu?campus=HN&locationId=12
```

Route này giúp:

- Share link tới đúng vị trí.
- Reload page vẫn giữ đúng item đang xem.
- Back/forward browser hoạt động tốt.

---

## 7. API đề xuất

## 7.1 API lấy danh sách campus public

```http
GET /api/public/visit-fptu/campuses
```

### Response

```json
{
  "items": [
    {
      "campusId": 1,
      "campusCode": "HN",
      "campusName": "Campus Hà Nội",
      "city": "Hà Nội",
      "coverFileId": 100,
      "coverUrl": "/api/files/100/content"
    },
    {
      "campusId": 2,
      "campusCode": "HCM",
      "campusName": "Campus Hồ Chí Minh",
      "city": "TP.HCM",
      "coverFileId": 101,
      "coverUrl": "/api/files/101/content"
    }
  ]
}
```

Nếu DB chưa có cover riêng cho campus, frontend có thể dùng ảnh fallback tĩnh. Tuy nhiên API vẫn nên trả campus active thật từ DB.

---

## 7.2 API lấy cấu trúc area/location theo campus

```http
GET /api/public/visit-fptu/campuses/{campusCode}/navigation
```

Ví dụ:

```http
GET /api/public/visit-fptu/campuses/HN/navigation
```

### Mục tiêu

API này trả về toàn bộ khu vực active và vị trí active có gallery item published trong campus đã chọn.

### Response đề xuất

```json
{
  "campus": {
    "campusId": 1,
    "campusCode": "HN",
    "campusName": "Campus Hà Nội",
    "city": "Hà Nội"
  },
  "areas": [
    {
      "areaId": 1,
      "areaName": "TỔNG QUAN",
      "displayOrder": 0,
      "locations": [
        {
          "locationId": 1,
          "locationName": "Toàn cảnh Hola Park",
          "displayOrder": 0,
          "galleryItemId": 1,
          "title": "Khuôn viên flycam xịn",
          "mediaKind": "VIDEO",
          "primaryMediaUrl": "/api/files/100/content"
        }
      ]
    },
    {
      "areaId": 4,
      "areaName": "TÒA DELTA",
      "displayOrder": 4,
      "locations": [
        {
          "locationId": 12,
          "locationName": "Trước tòa nhà Delta",
          "displayOrder": 1,
          "galleryItemId": 12,
          "title": "Không gian thực hành hiện đại",
          "mediaKind": "IMAGE",
          "primaryMediaUrl": "/api/files/120/content"
        },
        {
          "locationId": 13,
          "locationName": "Sảnh chính",
          "displayOrder": 2,
          "galleryItemId": 13,
          "title": "Sảnh chính Delta",
          "mediaKind": "IMAGE",
          "primaryMediaUrl": "/api/files/121/content"
        }
      ]
    }
  ]
}
```

### Rule quan trọng

Không trả location nếu:

```text
location INACTIVE
area INACTIVE
location không có gallery item
gallery item HIDDEN
gallery item deleted
gallery item không có media ACTIVE
```

---

## 7.3 API lấy detail gallery item theo location

```http
GET /api/public/visit-fptu/locations/{locationId}/gallery-item
```

### Response đề xuất

```json
{
  "campus": {
    "campusId": 1,
    "campusCode": "HN",
    "campusName": "Campus Hà Nội"
  },
  "area": {
    "areaId": 4,
    "areaName": "TÒA DELTA"
  },
  "location": {
    "locationId": 12,
    "locationName": "Trước tòa nhà Delta"
  },
  "galleryItem": {
    "galleryItemId": 12,
    "title": "Không gian thực hành hiện đại",
    "description": "Khu vực học tập và thực hành hiện đại dành cho sinh viên.",
    "mediaKind": "IMAGE",
    "status": "PUBLISHED"
  },
  "media": [
    {
      "mediaId": 101,
      "fileId": 501,
      "mediaType": "IMAGE",
      "url": "/api/files/501/content",
      "thumbnailUrl": "/api/files/501/content",
      "caption": null,
      "altText": "Không gian thực hành hiện đại",
      "isPrimary": true,
      "displayOrder": 0
    },
    {
      "mediaId": 102,
      "fileId": 502,
      "mediaType": "IMAGE",
      "url": "/api/files/502/content",
      "thumbnailUrl": "/api/files/502/content",
      "caption": null,
      "altText": "Không gian thực hành hiện đại",
      "isPrimary": false,
      "displayOrder": 1
    }
  ],
  "navigation": {
    "previousLocationId": 11,
    "nextLocationId": 13,
    "previousAreaId": 3,
    "nextAreaId": 5
  }
}
```

---

## 8. Query SQL public đề xuất

## 8.1 Query lấy navigation area/location

```sql
SELECT
    c.campus_id,
    c.campus_code,
    c.name AS campus_name,

    ga.area_id,
    ga.area_name,
    ga.display_order AS area_display_order,

    gl.location_id,
    gl.location_name,
    gl.display_order AS location_display_order,

    gi.gallery_item_id,
    gi.title,
    gi.media_kind,

    gm.media_id AS primary_media_id,
    gm.file_id AS primary_file_id

FROM campuses c
JOIN gallery_areas ga
    ON ga.campus_id = c.campus_id
JOIN gallery_locations gl
    ON gl.area_id = ga.area_id
JOIN gallery_items gi
    ON gi.location_id = gl.location_id
   AND gi.status = 'PUBLISHED'
   AND gi.deleted_at IS NULL
JOIN gallery_item_media gm
    ON gm.gallery_item_id = gi.gallery_item_id
   AND gm.is_primary = 1
   AND gm.status = 'ACTIVE'
   AND gm.deleted_at IS NULL
WHERE c.campus_code = @CampusCode
  AND c.status = 'ACTIVE'
  AND ga.status = 'ACTIVE'
  AND gl.status = 'ACTIVE'
ORDER BY
    ga.display_order ASC,
    ga.area_name ASC,
    gl.display_order ASC,
    gl.location_name ASC;
```

---

## 8.2 Query lấy detail gallery item

```sql
SELECT
    c.campus_id,
    c.campus_code,
    c.name AS campus_name,

    ga.area_id,
    ga.area_name,

    gl.location_id,
    gl.location_name,

    gi.gallery_item_id,
    gi.title,
    gi.description,
    gi.media_kind,
    gi.status,

    gm.media_id,
    gm.file_id,
    gm.media_type,
    gm.thumbnail_file_id,
    gm.caption,
    gm.alt_text,
    gm.is_primary,
    gm.display_order

FROM gallery_locations gl
JOIN gallery_areas ga
    ON ga.area_id = gl.area_id
JOIN campuses c
    ON c.campus_id = ga.campus_id
JOIN gallery_items gi
    ON gi.location_id = gl.location_id
   AND gi.status = 'PUBLISHED'
   AND gi.deleted_at IS NULL
JOIN gallery_item_media gm
    ON gm.gallery_item_id = gi.gallery_item_id
   AND gm.status = 'ACTIVE'
   AND gm.deleted_at IS NULL
WHERE gl.location_id = @LocationId
  AND gl.status = 'ACTIVE'
  AND ga.status = 'ACTIVE'
  AND c.status = 'ACTIVE'
ORDER BY
    gm.is_primary DESC,
    gm.display_order ASC,
    gm.media_id ASC;
```

---

## 9. UI layout theo màn hình hiện tại

## 9.1 Header

Header giữ menu hiện tại:

```text
Phòng HTQT
Trang chủ
Tin tức
Đối tác
Outbound
Inbound
Visit FPTU
FAQ
Search
Language
User menu nếu đã đăng nhập
```

Khi click `Visit FPTU`:

```text
Navigate tới /visit-fptu
```

Menu `Visit FPTU` active:

```text
text-orange
border-bottom orange
```

---

## 9.2 Màn chọn campus

Khi vào `/visit-fptu`, hệ thống hiển thị hero chọn campus.

Hiển thị 5 campus:

```text
Campus Hà Nội
Campus Hồ Chí Minh
Campus Đà Nẵng
Campus Cần Thơ
Campus Quy Nhơn
```

Mỗi campus có thể hiển thị:

- Tên campus.
- Ảnh nền hoặc card campus.
- Hover effect nhẹ.
- Click để chọn campus.

Sau khi chọn campus:

```text
Load navigation area/location của campus đó.
Set selectedCampus.
Set selectedLocation = location đầu tiên có gallery item published.
Load gallery item detail của selectedLocation.
```

Nếu muốn vào thẳng màn Hà Nội như screenshot thì có thể default:

```text
Nếu URL không có campus:
default campus = HN hoặc campus đầu tiên trong API response.
```

---

## 9.3 Màn campus gallery

Màn chính gồm 3 vùng:

```text
Left sidebar: danh sách khu vực
Center panel: thông tin gallery item
Right panel: media viewer
```

### Left sidebar — danh sách khu vực

Hiển thị toàn bộ area của campus đang chọn.

Ví dụ:

```text
TỔNG QUAN
TÒA ALPHA
TÒA BETA
TÒA DELTA
TÒA EPSILON
TÒA GAMMA
KHU DỊCH VỤ
KÝ TÚC XÁ
KHU THỂ THAO
```

Area đang active có màu cam.

Mỗi area có icon media nhỏ bên phải.

### Hover area để hiện vị trí cụ thể

Khi trỏ chuột vào một area:

```text
Hiển thị flyout danh sách locations thuộc area đó.
```

Ví dụ hover `TÒA DELTA`:

```text
Trước tòa nhà Delta
Sảnh chính
Thư viện
Phòng học điển hình
Trung tâm khởi nghiệp & nghiên cứu
Phòng thí nghiệm đổi mới & sáng tạo SAP
Phòng học Nhạc cụ dân tộc
```

Flyout nên:

- Nằm bên phải sidebar.
- Có nền cam hoặc nền tối theo design.
- Không che mất sidebar.
- Có scroll nếu quá nhiều location.
- Mỗi location có icon pin hoặc media.
- Location đang chọn có style active.

### Click location

Khi click một location:

```text
Set selectedArea
Set selectedLocation
Call API detail gallery item
Update center panel
Update media viewer
Update URL query locationId
```

---

## 9.4 Center panel — nội dung gallery item

Hiển thị:

```text
Badge: TRẢI NGHIỆM KHÔNG GIAN hoặc VISIT FPTU GALLERY
Title
Breadcrumb: AREA > LOCATION
Description
Navigation: Khu vực trước / Tiếp theo
```

Với gallery item:

```text
Title: TỔNG QUAN
Breadcrumb: TỔNG QUAN > Toàn cảnh Hola Park
Description: ...
```

Hoặc:

```text
Title: Không gian thực hành hiện đại
Breadcrumb: TÒA DELTA > Trung tâm khởi nghiệp & nghiên cứu
Description: ...
```

Không hiển thị:

```text
Nút Xem 360
Nút Bắt đầu tham quan 360 nếu đang có ý nghĩa mở virtual tour
```

Nếu vẫn cần CTA, đổi thành CTA bình thường:

```text
Xem hình ảnh
```

Nhưng theo yêu cầu hiện tại: bỏ nút `Xem 360`.

---

## 9.5 Right panel — media viewer

Hiển thị media của gallery item.

Media có thể là:

```text
IMAGE
VIDEO
MIXED
```

### Với IMAGE

Hiển thị ảnh lớn.

Có:

- Nút previous media.
- Nút next media.
- Dots indicator.
- Click ảnh để phóng to.
- Nút đóng khi đang fullscreen.

### Với VIDEO

Hiển thị video player.

Rule:

```text
Không autoplay có âm thanh.
Nếu autoplay thì phải muted.
Có controls.
Có poster/thumbnail nếu có thumbnail_file_id.
```

### Với MIXED

Media carousel hỗ trợ cả ảnh và video.

Khi chuyển media:

```text
Nếu media hiện tại là video đang phát, pause video trước khi chuyển.
```

---

## 10. Tương tác chi tiết

## 10.1 Chọn campus

### Main Flow

1. User click menu `Visit FPTU`.
2. System mở `/visit-fptu`.
3. System load danh sách campus active.
4. User chọn campus.
5. System load danh sách area/location của campus đó.
6. System chọn location đầu tiên có gallery item published.
7. System hiển thị gallery item của location đó.

### Alternative Flow

Nếu campus không có area/location/gallery item public:

```text
Hiển thị empty state:
"Campus này hiện chưa có nội dung Gallery công khai."
```

---

## 10.2 Hover area

### Main Flow

1. User hover vào một area ở sidebar.
2. System hiển thị flyout location của area đó.
3. User di chuyển chuột vào flyout.
4. Flyout vẫn mở.
5. User click location.
6. System load gallery item của location.

### Rule

```text
Hover area chỉ mở location list.
Hover area không tự đổi gallery item.
Chỉ click location mới đổi nội dung gallery item.
```

---

## 10.3 Click location

### Main Flow

1. User click một location trong flyout.
2. Frontend gọi API detail theo `locationId`.
3. Backend kiểm tra location public-visible.
4. Backend trả gallery item và media.
5. Frontend cập nhật:
   - active area
   - active location
   - title
   - breadcrumb
   - description
   - media viewer
   - URL query

### Alternative Flow

Nếu location vừa bị disabled hoặc item vừa bị hidden:

```text
API trả 404 hoặc empty result.
Frontend hiển thị:
"Nội dung này hiện không còn được hiển thị."
Sau đó tự chuyển về location public đầu tiên còn hợp lệ nếu có.
```

---

## 10.4 Chuyển media trước/sau

### Main Flow

1. User click mũi tên trái/phải hoặc dots.
2. System chuyển media trong danh sách media của gallery item.
3. Nếu hết danh sách:
   - Có thể loop về media đầu/cuối.
   - Hoặc disable nút next/prev.

Chốt đề xuất:

```text
Nên loop media để trải nghiệm mượt hơn.
```

Ví dụ:

```text
media cuối + next → media đầu
media đầu + prev → media cuối
```

---

## 10.5 Phóng to media

### Main Flow

1. User click vào ảnh hoặc icon phóng to.
2. System mở fullscreen/lightbox.
3. Lightbox hiển thị media hiện tại.
4. User có thể:
   - Next media.
   - Previous media.
   - Đóng lightbox.
   - Bấm ESC để đóng.
   - Click overlay để đóng nếu không click vào media.

### Rule

```text
Lightbox chỉ dùng cho media của gallery item hiện tại.
Không load media của location khác trong lightbox.
```

Với video:

```text
Fullscreen video dùng video controls.
Không autoplay âm thanh.
```

---

## 10.6 Chuyển vị trí trước/sau

Trong center panel có nút:

```text
Khu vực trước
Tiếp theo
```

Nhưng tên nút nên hiểu là chuyển item theo danh sách vị trí public trong campus.

Đề xuất đổi wording rõ hơn:

```text
Vị trí trước
Vị trí tiếp theo
```

Nếu muốn giữ UI hiện tại thì vẫn được, nhưng logic là chuyển location.

### Main Flow

1. User bấm `Tiếp theo`.
2. System tìm location kế tiếp trong danh sách flattened:

```text
Area 1 - Location 1
Area 1 - Location 2
Area 2 - Location 1
Area 2 - Location 2
...
```

3. System load gallery item của location kế tiếp.
4. Active area/sidebar/flyout được cập nhật.
5. URL query cập nhật.

### Prev/Next order

Thứ tự chuyển:

```text
area.display_order ASC
area.area_name ASC
location.display_order ASC
location.location_name ASC
```

### Loop

Chốt đề xuất:

```text
Có loop.
```

Ví dụ:

```text
Đang ở location cuối cùng → bấm tiếp theo → về location đầu tiên.
Đang ở location đầu tiên → bấm vị trí trước → về location cuối cùng.
```

---

## 10.7 Chuyển khu vực trước/sau

Nếu cần chuyển theo khu vực, nên có logic riêng:

```text
Khu vực trước
Khu vực tiếp theo
```

Khi chuyển khu vực:

1. System tìm area trước/sau trong campus.
2. System chọn location đầu tiên public-visible của area đó.
3. Load gallery item của location đó.

Nếu area không có location public-visible thì bỏ qua area đó.

Nếu UI chỉ có 2 nút ở dưới description, nên ưu tiên wording:

```text
Khu vực trước
Khu vực tiếp theo
```

và logic là chuyển area.

Nhưng vì user yêu cầu cả **chuyển ảnh trước sau** và **chuyển khu vực trước sau**, nên nên tách rõ:

- Media viewer có mũi tên/dots để chuyển media.
- Center panel có nút `Khu vực trước` / `Khu vực tiếp theo` để chuyển area.
- Flyout location dùng để chọn location cụ thể.

---

## 11. Business Rules

```text
BR-PGAL-01: Header Visit FPTU điều hướng tới public gallery page.

BR-PGAL-02: Public Gallery không yêu cầu đăng nhập.

BR-PGAL-03: Chỉ hiển thị campus ACTIVE.

BR-PGAL-04: Khi chọn campus, chỉ hiển thị area ACTIVE thuộc campus đó.

BR-PGAL-05: Chỉ hiển thị location ACTIVE thuộc area ACTIVE.

BR-PGAL-06: Chỉ hiển thị location có gallery item PUBLISHED.

BR-PGAL-07: Mỗi location tối đa một gallery item.

BR-PGAL-08: Click location sẽ hiển thị gallery item tương ứng của location đó.

BR-PGAL-09: Nếu location không có gallery item public, không hiển thị location đó trong flyout public.

BR-PGAL-10: Nếu gallery item HIDDEN thì không hiển thị public.

BR-PGAL-11: Nếu location INACTIVE thì không hiển thị location và gallery item public.

BR-PGAL-12: Nếu area INACTIVE thì không hiển thị area, location và gallery item public.

BR-PGAL-13: Public API không trả Google Drive raw link nếu hệ thống đang dùng file proxy.

BR-PGAL-14: Media URL public phải dùng backend file proxy dạng /api/files/{fileId}/content hoặc endpoint public file proxy tương ứng.

BR-PGAL-15: Media carousel chỉ hiển thị gallery_item_media ACTIVE và chưa deleted.

BR-PGAL-16: Media đầu tiên hiển thị là media is_primary = 1; nếu thiếu primary thì lấy display_order nhỏ nhất.

BR-PGAL-17: Bỏ nút Xem 360 và không gọi API/route 360.

BR-PGAL-18: Hover area chỉ mở danh sách location; không tự load gallery item.

BR-PGAL-19: Chỉ click location mới đổi gallery item.

BR-PGAL-20: Chuyển khu vực trước/sau phải bỏ qua area không có location public-visible.

BR-PGAL-21: Search/filter nếu có ở public Gallery phải chỉ tìm trong dữ liệu public-visible.

BR-PGAL-22: Khi dữ liệu public bị thay đổi trong lúc user đang xem, API detail có thể trả 404; frontend phải hiển thị fallback an toàn.
```

---

## 12. Backend validation và scope

Public endpoint không dùng Staff Leader scope, nhưng vẫn phải lọc dữ liệu public-safe.

Không được trả:

```text
created_by
updated_by
deleted_by
createdByName
updatedByName
Google Drive external_file_id raw nếu không cần
checksum
object_key
download_url raw nếu không public-safe
```

Chỉ trả:

```text
campus display info
area display info
location display info
gallery title/description/mediaKind
media urls qua file proxy
caption/altText nếu public-safe
```

---

## 13. DTO đề xuất

### 13.1 PublicCampusDto

```csharp
public sealed class PublicCampusDto
{
    public long CampusId { get; init; }
    public string CampusCode { get; init; } = string.Empty;
    public string CampusName { get; init; } = string.Empty;
    public string? City { get; init; }
    public string? CoverUrl { get; init; }
}
```

### 13.2 PublicGalleryNavigationDto

```csharp
public sealed class PublicGalleryNavigationDto
{
    public PublicCampusDto Campus { get; init; } = default!;
    public IReadOnlyList<PublicGalleryAreaDto> Areas { get; init; } = [];
}
```

### 13.3 PublicGalleryAreaDto

```csharp
public sealed class PublicGalleryAreaDto
{
    public long AreaId { get; init; }
    public string AreaName { get; init; } = string.Empty;
    public int DisplayOrder { get; init; }
    public IReadOnlyList<PublicGalleryLocationDto> Locations { get; init; } = [];
}
```

### 13.4 PublicGalleryLocationDto

```csharp
public sealed class PublicGalleryLocationDto
{
    public long LocationId { get; init; }
    public string LocationName { get; init; } = string.Empty;
    public int DisplayOrder { get; init; }
    public long GalleryItemId { get; init; }
    public string Title { get; init; } = string.Empty;
    public string MediaKind { get; init; } = string.Empty;
    public string? PrimaryMediaUrl { get; init; }
}
```

### 13.5 PublicGalleryItemDetailDto

```csharp
public sealed class PublicGalleryItemDetailDto
{
    public PublicCampusDto Campus { get; init; } = default!;
    public PublicGalleryAreaSummaryDto Area { get; init; } = default!;
    public PublicGalleryLocationSummaryDto Location { get; init; } = default!;
    public PublicGalleryItemSummaryDto GalleryItem { get; init; } = default!;
    public IReadOnlyList<PublicGalleryMediaDto> Media { get; init; } = [];
}
```

### 13.6 PublicGalleryMediaDto

```csharp
public sealed class PublicGalleryMediaDto
{
    public long MediaId { get; init; }
    public long FileId { get; init; }
    public string MediaType { get; init; } = string.Empty;
    public string Url { get; init; } = string.Empty;
    public string? ThumbnailUrl { get; init; }
    public string? Caption { get; init; }
    public string? AltText { get; init; }
    public bool IsPrimary { get; init; }
    public int DisplayOrder { get; init; }
}
```

---

## 14. Frontend state đề xuất

```ts
type PublicGalleryState = {
  campuses: PublicCampus[];
  selectedCampusCode: string | null;

  areas: PublicGalleryArea[];
  hoveredAreaId: number | null;
  selectedAreaId: number | null;
  selectedLocationId: number | null;

  currentItem: PublicGalleryItemDetail | null;
  currentMediaIndex: number;

  isCampusLoading: boolean;
  isNavigationLoading: boolean;
  isDetailLoading: boolean;

  isLightboxOpen: boolean;
};
```

---

## 15. Frontend flow đề xuất

```text
1. On mount:
   - Load active campus list.
   - Resolve selected campus from URL or default HN/first campus.

2. When selectedCampus changes:
   - Load navigation area/location.
   - Pick location from URL if valid.
   - Else pick first location in first area with public item.
   - Load detail.

3. When area hover:
   - Set hoveredAreaId.
   - Show flyout.

4. When location click:
   - Set selectedLocationId.
   - Set selectedAreaId by parent area.
   - Update URL.
   - Load detail.

5. When media next/prev:
   - Update currentMediaIndex.

6. When lightbox open:
   - Show current media fullscreen.

7. When area next/prev:
   - Resolve next/prev area with locations.
   - Pick first location of that area.
   - Load detail.
```

---

## 16. Empty / loading / error states

### 16.1 Campus không có gallery public

```text
Campus này hiện chưa có nội dung Gallery công khai.
```

### 16.2 Area không có location public

Không hiển thị area đó trên public sidebar.

Nếu vẫn muốn hiển thị area để đủ cấu trúc campus, disable area và show tooltip:

```text
Khu vực này hiện chưa có nội dung công khai.
```

Nhưng đề xuất public UX:

```text
Không hiển thị area rỗng.
```

### 16.3 Location detail không còn public

```text
Nội dung này hiện không còn được hiển thị.
```

Sau đó:

```text
Nếu còn location khác → tự chuyển tới location đầu tiên.
Nếu không còn → empty state campus.
```

### 16.4 Media lỗi load

Hiển thị placeholder:

```text
Không thể tải media.
```

Không làm crash toàn page.

---

## 17. Acceptance Criteria

### AC-PGAL-01 — Header Visit FPTU mở public gallery

Given user đang ở bất kỳ trang nào  
When user click menu `Visit FPTU`  
Then hệ thống điều hướng tới trang `/visit-fptu`  
And menu `Visit FPTU` được active.

### AC-PGAL-02 — Hiển thị danh sách campus

Given có các campus ACTIVE  
When user mở trang Visit FPTU  
Then hệ thống hiển thị danh sách campus để chọn.

### AC-PGAL-03 — Chọn campus hiển thị area public

Given user chọn `Campus Hà Nội`  
When API navigation trả dữ liệu  
Then sidebar hiển thị các khu vực ACTIVE thuộc campus Hà Nội có nội dung public.

### AC-PGAL-04 — Hover area hiển thị location

Given sidebar có area `TÒA DELTA`  
When user hover `TÒA DELTA`  
Then hệ thống hiển thị flyout danh sách vị trí cụ thể thuộc `TÒA DELTA`.

### AC-PGAL-05 — Click location hiển thị gallery item

Given location `Thư viện` có gallery item PUBLISHED  
When user click `Thư viện`  
Then hệ thống hiển thị title, breadcrumb `TÒA DELTA > Thư viện`, description và media của gallery item đó.

### AC-PGAL-06 — Không hiển thị location inactive

Given location `Hồ sen` có status INACTIVE  
When user mở public Gallery  
Then location `Hồ sen` không xuất hiện trong flyout  
And gallery item của location đó không hiển thị public.

### AC-PGAL-07 — Không hiển thị item hidden

Given location active nhưng gallery item status HIDDEN  
When user mở public Gallery  
Then location đó không hiển thị trong public navigation  
Or nếu location vẫn hiển thị theo thiết kế, khi click không có item public.

Chốt đề xuất: không hiển thị location nếu item HIDDEN.

### AC-PGAL-08 — Media carousel hoạt động

Given gallery item có nhiều media  
When user bấm next/previous media  
Then media viewer chuyển đúng media theo thứ tự display_order.

### AC-PGAL-09 — Phóng to media

Given user đang xem một ảnh  
When user click ảnh hoặc nút phóng to  
Then hệ thống mở lightbox fullscreen  
And user có thể đóng lightbox.

### AC-PGAL-10 — Chuyển khu vực tiếp theo

Given user đang xem một location thuộc `TÒA ALPHA`  
When user bấm `Khu vực tiếp theo`  
Then hệ thống chuyển sang area tiếp theo có location public  
And hiển thị gallery item của location đầu tiên trong area đó.

### AC-PGAL-11 — Bỏ nút xem 360

Given user mở trang Visit FPTU  
Then màn hình không hiển thị nút `Xem 360`  
And frontend không gọi route/API 360.

### AC-PGAL-12 — Public API không trả dữ liệu quản trị

Given user gọi public gallery API  
Then response không chứa created_by, updated_by, deleted_by, external_file_id raw, checksum hoặc object_key.

---

## 18. Checklist cho AI Agent khi code

```text
[ ] Đọc DB mới nhất: pems_full_v10_new_final_visit_lifecycle_news_not_required.sql.
[ ] Dùng bảng campuses, gallery_areas, gallery_locations, gallery_items, gallery_item_media, files.
[ ] Không dùng bảng cũ galleries/gallery_images.
[ ] Không code quản lý Gallery trong UC này.
[ ] Không code quản lý khu vực trong UC này.
[ ] Header Visit FPTU điều hướng tới public gallery route.
[ ] Public page không yêu cầu đăng nhập.
[ ] Load campus ACTIVE.
[ ] Khi chọn campus, load area/location public-visible.
[ ] Chỉ trả area ACTIVE.
[ ] Chỉ trả location ACTIVE.
[ ] Chỉ trả gallery item PUBLISHED.
[ ] Chỉ trả media ACTIVE.
[ ] Không hiển thị location không có gallery item public.
[ ] Hover area hiển thị flyout location.
[ ] Click location mới load gallery item detail.
[ ] Hiển thị title, breadcrumb area > location, description, media.
[ ] Media carousel hỗ trợ ảnh/video.
[ ] Có phóng to media/lightbox.
[ ] Có chuyển media trước/sau.
[ ] Có chuyển khu vực trước/sau.
[ ] Bỏ nút Xem 360.
[ ] Không gọi API/route 360.
[ ] Dùng file proxy /api/files/{fileId}/content hoặc endpoint public file proxy tương ứng.
[ ] Không expose Google Drive raw metadata ra public.
[ ] Handle empty/loading/error state.
[ ] Build backend.
[ ] Build frontend.
[ ] Test public route không login.
[ ] Test login rồi vẫn vào được public route.
```

---

## 19. Chốt nghiệp vụ cuối cùng

UC này là **public display layer** của dữ liệu Gallery đã được quản lý ở 2 UC trước.

Công thức chốt:

```text
Campus ACTIVE
→ Area ACTIVE
→ Location ACTIVE
→ Gallery item PUBLISHED
→ Media ACTIVE
= được hiển thị public
```

Người dùng public sẽ đi theo luồng:

```text
Visit FPTU
→ Chọn campus
→ Hover khu vực
→ Click vị trí
→ Xem bài đăng gallery của vị trí đó
→ Xem media / phóng to / chuyển ảnh / chuyển khu vực
```

Nút `Xem 360` bị loại khỏi scope hiện tại.
