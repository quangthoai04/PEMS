# CẬP NHẬT UC — PUBLIC VisitFPTU Gallery: Hiển thị nhiều Gallery Item theo Location

## 1. Mục tiêu thay đổi

Hiện tại khi người dùng click vào một **vị trí cụ thể**, UI đang hiển thị từng gallery item theo dạng carousel bài đăng:

```text
Bài đăng 1 / 3
Bài đăng 2 / 3
Bài đăng 3 / 3
```

và người dùng bấm mũi tên để chuyển giữa các gallery item.

Logic mới cần đổi thành:

```text
Click vào 1 location
→ Hiển thị toàn bộ gallery item của location đó dưới dạng lưới media
→ Mỗi gallery item đại diện bằng media chính is_primary
→ 1 dòng hiển thị 5 media chính
→ Hover media có hiệu ứng
→ Click media bất kỳ mở trang/detail mô tả đầy đủ của gallery item đó
```

Vậy có 2 tầng hiển thị rõ ràng:

```text
Tầng 1: Location Gallery Grid
- Hiển thị danh sách gallery item của location.
- Mỗi item là 1 card/media thumbnail lấy từ media is_primary.

Tầng 2: Gallery Item Detail
- Hiển thị chi tiết 1 gallery item.
- Có breadcrumb, title, description, toàn bộ media của item đó.
```

---

## 2. Nghiệp vụ đã chốt

```text
1 campus có nhiều area.
1 area có nhiều location.
1 location có nhiều gallery item.
1 gallery item có nhiều media.
Mỗi gallery item có đúng 1 media chính is_primary.
```

Public chỉ hiển thị item khi đủ điều kiện:

```text
campus ACTIVE
area ACTIVE
location ACTIVE
gallery item PUBLISHED
gallery item chưa deleted
gallery item có ít nhất 1 media ACTIVE
```

---

## 3. Luồng public mới

### 3.1 Luồng tổng quát

```text
User click header Visit FPTU
→ Hệ thống mở trang Public VisitFPTU Gallery
→ User chọn campus
→ Hệ thống hiển thị danh sách area của campus
→ User hover area
→ Hệ thống hiển thị danh sách location thuộc area
→ User click location
→ Hệ thống hiển thị lưới toàn bộ gallery item của location đó
→ User hover từng media card để xem hiệu ứng
→ User click một media card
→ Hệ thống mở detail của gallery item đó
→ User xem title, breadcrumb, description, toàn bộ media
```

---

## 4. Màn 1 — Location Gallery Grid

### 4.1 Khi click một location

Khi user click vào vị trí cụ thể, ví dụ:

```text
TÒA ALPHA > Trước tòa
```

thì hệ thống không mở thẳng 1 gallery item nữa.

Thay vào đó, hệ thống hiển thị toàn bộ gallery item thuộc location đó ở dạng lưới.

Ví dụ location `TÒA ALPHA / Trước tòa` có 12 gallery item:

```text
Item 1: Cổng chính Alpha
Item 2: Sân trước Alpha buổi sáng
Item 3: Sinh viên check-in Alpha
Item 4: Khu vực cây xanh Alpha
...
```

thì màn grid hiển thị 12 card media.

---

### 4.2 Layout grid

Yêu cầu layout:

```text
1 dòng = 5 media card
```

Ví dụ:

```text
[Item 1] [Item 2] [Item 3] [Item 4] [Item 5]
[Item 6] [Item 7] [Item 8] [Item 9] [Item 10]
[Item 11] [Item 12]
```

Mỗi card lấy media chính của gallery item:

```text
gallery_item_media.is_primary = 1
```

Nếu vì dữ liệu lỗi không có `is_primary = 1`, backend hoặc frontend fallback lấy media đầu tiên theo:

```text
display_order ASC
media_id ASC
```

---

### 4.3 Nội dung mỗi media card

Mỗi card nên hiển thị:

```text
- Ảnh/video chính của gallery item
- Icon loại media nếu là video
- Title ngắn của gallery item
- Optional: badge Hình ảnh / Video / Hỗn hợp
```

Ví dụ card:

```text
[Ảnh chính]
Cổng chính Alpha
Hình ảnh
```

Với video:

```text
[Thumbnail video]
Icon Play
Video
```

Nếu media chính là video, nên hiển thị:

```text
- thumbnail_file_id nếu có
- nếu không có thumbnail thì dùng video poster hoặc placeholder video
- icon play ở giữa
```

---

### 4.4 Hiệu ứng hover media card

Khi hover qua media card:

```text
- Card phóng nhẹ scale 1.03 hoặc 1.05
- Overlay tối nhẹ
- Hiện title rõ hơn
- Hiện text “Xem chi tiết”
- Cursor pointer
```

Ví dụ hover:

```text
[Ảnh được scale nhẹ]
Overlay: Xem chi tiết
Title: Cổng chính Alpha
```

Không nên đổi gallery item khi chỉ hover. Hover chỉ là hiệu ứng UI.

Chỉ khi click media card thì mới mở detail.

---

### 4.5 Empty state khi location không có item public

Trường hợp location active nhưng không có gallery item public:

```text
Location không có gallery item PUBLISHED
```

thì public page có 2 lựa chọn:

#### Chốt đề xuất

Không hiển thị location đó trong danh sách public location.

Vì nếu user click location rồi thấy rỗng sẽ làm trải nghiệm kém.

Nếu vẫn muốn hiển thị location thì empty state:

```text
Vị trí này hiện chưa có nội dung Gallery công khai.
```

Nhưng với public UI hiện tại, nên lọc luôn location không có item public.

---

## 5. Màn 2 — Gallery Item Detail

### 5.1 Khi click một media card

Khi user click vào một media chính trong grid, hệ thống mở detail của gallery item đó.

Detail giữ phong cách hiện tại:

```text
- Breadcrumb
- Title
- Description
- Toàn bộ media của gallery item
- Media viewer / carousel
- Phóng to media
- Chuyển ảnh/video trước sau trong item
```

Ví dụ:

```text
TÒA ALPHA > Trước tòa

Cổng chính Alpha

Khu vực cổng chính của tòa Alpha, nơi sinh viên thường check-in...
```

---

### 5.2 Nội dung detail

Detail cần hiển thị:

```text
Breadcrumb: AREA > LOCATION
Title: gallery_items.title
Description: gallery_items.description
Media: toàn bộ gallery_item_media của item
```

Media trong detail lấy tất cả:

```text
gallery_item_media.status = ACTIVE
gallery_item_media.deleted_at IS NULL
```

Sắp xếp:

```text
is_primary DESC
display_order ASC
media_id ASC
```

---

### 5.3 Media viewer trong detail

Trong detail của một gallery item, vẫn giữ carousel media như hiện tại.

Nếu item có nhiều media:

```text
Media 1 / n
Media 2 / n
...
```

Nhưng đây là **carousel media của một gallery item**, không phải carousel bài đăng.

Cần phân biệt rõ:

```text
Không còn: Bài đăng 1/3, Bài đăng 2/3.
Chỉ còn: Media 1/n trong detail của item.
```

Nếu muốn tránh chữ gây nhầm, dùng dots indicator thay vì text.

---

### 5.4 Nút quay lại grid

Trong detail cần có nút:

```text
← Quay lại danh sách hình ảnh
```

hoặc:

```text
← Trở về vị trí
```

Khi bấm:

```text
Trở lại grid của location đang chọn.
Giữ nguyên selected campus, area, location.
```

---

## 6. Route đề xuất

### 6.1 Route chọn campus/location

```text
/visit-fptu?campus=HN&locationId=12
```

Route này hiển thị grid gallery item của location.

### 6.2 Route detail gallery item

```text
/visit-fptu?campus=HN&locationId=12&itemId=88
```

Route này mở detail của gallery item.

Nếu user reload link detail:

```text
Hệ thống load campus navigation
→ load location grid
→ load detail itemId
```

Nếu item không còn public:

```text
Hiển thị thông báo: Nội dung này hiện không còn được hiển thị.
Sau đó quay lại grid location nếu còn item khác.
```

---

## 7. API cần có

### 7.1 API navigation area/location

Giữ API hiện tại nhưng phải đảm bảo location chỉ xuất hiện nếu có ít nhất 1 gallery item public.

```http
GET /api/public/visit-fptu/campuses/{campusCode}/navigation
```

Response location nên có thêm số lượng item public:

```json
{
  "areaId": 2,
  "areaName": "TÒA ALPHA",
  "locations": [
    {
      "locationId": 12,
      "locationName": "Trước tòa",
      "publicGalleryItemCount": 8
    }
  ]
}
```

---

### 7.2 API lấy grid gallery item theo location

Thêm hoặc sửa endpoint:

```http
GET /api/public/visit-fptu/locations/{locationId}/gallery-items
```

Mục tiêu:

```text
Trả toàn bộ gallery item PUBLISHED của location đó.
Mỗi item chỉ trả media chính is_primary để render grid.
```

Response đề xuất:

```json
{
  "campus": {
    "campusId": 1,
    "campusCode": "HN",
    "campusName": "Campus Hà Nội"
  },
  "area": {
    "areaId": 2,
    "areaName": "TÒA ALPHA"
  },
  "location": {
    "locationId": 12,
    "locationName": "Trước tòa"
  },
  "items": [
    {
      "galleryItemId": 88,
      "title": "Cổng chính Alpha",
      "descriptionPreview": "Không gian phía trước tòa Alpha...",
      "mediaKind": "IMAGE",
      "primaryMedia": {
        "mediaId": 301,
        "fileId": 9001,
        "mediaType": "IMAGE",
        "url": "/api/files/9001/content",
        "thumbnailUrl": "/api/files/9001/content",
        "altText": "Cổng chính Alpha"
      }
    },
    {
      "galleryItemId": 89,
      "title": "Không gian sinh viên check-in",
      "descriptionPreview": "Khu vực sinh viên thường chụp ảnh...",
      "mediaKind": "VIDEO",
      "primaryMedia": {
        "mediaId": 302,
        "fileId": 9002,
        "mediaType": "VIDEO",
        "url": "/api/files/9002/content",
        "thumbnailUrl": "/api/files/9100/content",
        "altText": "Không gian sinh viên check-in"
      }
    }
  ]
}
```

---

### 7.3 API lấy detail gallery item

```http
GET /api/public/visit-fptu/gallery-items/{galleryItemId}
```

Response:

```json
{
  "campus": {
    "campusId": 1,
    "campusCode": "HN",
    "campusName": "Campus Hà Nội"
  },
  "area": {
    "areaId": 2,
    "areaName": "TÒA ALPHA"
  },
  "location": {
    "locationId": 12,
    "locationName": "Trước tòa"
  },
  "galleryItem": {
    "galleryItemId": 88,
    "title": "Cổng chính Alpha",
    "description": "Không gian phía trước tòa Alpha...",
    "mediaKind": "IMAGE"
  },
  "media": [
    {
      "mediaId": 301,
      "fileId": 9001,
      "mediaType": "IMAGE",
      "url": "/api/files/9001/content",
      "thumbnailUrl": "/api/files/9001/content",
      "caption": null,
      "altText": "Cổng chính Alpha",
      "isPrimary": true,
      "displayOrder": 0
    },
    {
      "mediaId": 303,
      "fileId": 9003,
      "mediaType": "IMAGE",
      "url": "/api/files/9003/content",
      "thumbnailUrl": "/api/files/9003/content",
      "caption": null,
      "altText": "Sân trước Alpha",
      "isPrimary": false,
      "displayOrder": 1
    }
  ]
}
```

---

## 8. SQL query đề xuất

### 8.1 Query lấy grid item theo location

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
    gi.display_order,
    gi.created_at,

    gm.media_id,
    gm.file_id,
    gm.media_type,
    gm.thumbnail_file_id,
    gm.alt_text,
    gm.is_primary

FROM gallery_items gi
JOIN gallery_locations gl
    ON gl.location_id = gi.location_id
JOIN gallery_areas ga
    ON ga.area_id = gl.area_id
JOIN campuses c
    ON c.campus_id = ga.campus_id
JOIN gallery_item_media gm
    ON gm.gallery_item_id = gi.gallery_item_id
   AND gm.status = 'ACTIVE'
   AND gm.deleted_at IS NULL
WHERE gl.location_id = @LocationId
  AND c.status = 'ACTIVE'
  AND ga.status = 'ACTIVE'
  AND gl.status = 'ACTIVE'
  AND gi.status = 'PUBLISHED'
  AND gi.deleted_at IS NULL
  AND gm.is_primary = 1
ORDER BY
    gi.display_order ASC,
    gi.created_at DESC,
    gi.gallery_item_id DESC;
```

Nếu sợ dữ liệu thiếu `is_primary`, có thể dùng query fallback bằng window function hoặc xử lý backend.

---

### 8.2 Query detail gallery item

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

    gm.media_id,
    gm.file_id,
    gm.media_type,
    gm.thumbnail_file_id,
    gm.caption,
    gm.alt_text,
    gm.is_primary,
    gm.display_order

FROM gallery_items gi
JOIN gallery_locations gl
    ON gl.location_id = gi.location_id
JOIN gallery_areas ga
    ON ga.area_id = gl.area_id
JOIN campuses c
    ON c.campus_id = ga.campus_id
JOIN gallery_item_media gm
    ON gm.gallery_item_id = gi.gallery_item_id
WHERE gi.gallery_item_id = @GalleryItemId
  AND c.status = 'ACTIVE'
  AND ga.status = 'ACTIVE'
  AND gl.status = 'ACTIVE'
  AND gi.status = 'PUBLISHED'
  AND gi.deleted_at IS NULL
  AND gm.status = 'ACTIVE'
  AND gm.deleted_at IS NULL
ORDER BY
    gm.is_primary DESC,
    gm.display_order ASC,
    gm.media_id ASC;
```

---

## 9. Frontend state đề xuất

```ts
type PublicGalleryViewMode = 'CAMPUS_SELECT' | 'LOCATION_GRID' | 'ITEM_DETAIL';

type PublicGalleryState = {
  selectedCampusCode: string | null;
  selectedAreaId: number | null;
  selectedLocationId: number | null;
  selectedGalleryItemId: number | null;

  areas: PublicGalleryArea[];
  locationGridItems: PublicGalleryGridItem[];
  currentItemDetail: PublicGalleryItemDetail | null;

  viewMode: PublicGalleryViewMode;

  isNavigationLoading: boolean;
  isGridLoading: boolean;
  isDetailLoading: boolean;

  currentDetailMediaIndex: number;
  isLightboxOpen: boolean;
};
```

---

## 10. Frontend flow mới

### 10.1 Click location

```text
User click location
→ set selectedLocationId
→ set selectedGalleryItemId = null
→ set viewMode = LOCATION_GRID
→ gọi GET /locations/{locationId}/gallery-items
→ render grid 5 item / row
```

### 10.2 Click media card trong grid

```text
User click media card
→ set selectedGalleryItemId
→ set viewMode = ITEM_DETAIL
→ gọi GET /gallery-items/{galleryItemId}
→ render detail hiện tại
```

### 10.3 Click quay lại

```text
User click “Quay lại danh sách”
→ set selectedGalleryItemId = null
→ set viewMode = LOCATION_GRID
→ giữ nguyên selectedLocationId
→ không cần reload nếu grid cache còn
```

---

## 11. UI thay đổi cụ thể

### 11.1 Bỏ phần bài đăng 1/3

Xóa UI:

```text
Bài đăng 1 / 3
Bài đăng 2 / 3
Bài đăng 3 / 3
```

và mũi tên chuyển bài đăng.

Thay bằng grid:

```text
TÒA ALPHA > Trước tòa

Danh sách hình ảnh/video

[Card 1] [Card 2] [Card 3] [Card 4] [Card 5]
[Card 6] [Card 7] [Card 8] [Card 9] [Card 10]
```

### 11.2 Card grid

Mỗi card:

```text
- Aspect ratio cố định, ví dụ 1:1 hoặc 4:3.
- Border radius theo style hiện tại.
- Hover scale nhẹ.
- Overlay title.
- Video có icon play.
```

### 11.3 Detail

Detail giữ layout hiện tại nhưng bổ sung nút quay lại grid:

```text
← Quay lại danh sách
```

Detail hiển thị:

```text
- Breadcrumb: AREA > LOCATION
- Title
- Description
- Media viewer lớn
- Thumbnail/dots của media trong item
- Nút phóng to
```

---

## 12. Business Rules cập nhật

```text
BR-PGAL-GRID-01: Khi click location, hệ thống hiển thị toàn bộ gallery item public của location dưới dạng grid.

BR-PGAL-GRID-02: Mỗi gallery item trong grid được đại diện bằng media có is_primary = 1.

BR-PGAL-GRID-03: Một dòng grid trên desktop hiển thị 5 media card.

BR-PGAL-GRID-04: Nếu gallery item không có media is_primary do dữ liệu lỗi, hệ thống fallback lấy media ACTIVE đầu tiên theo display_order ASC, media_id ASC.

BR-PGAL-GRID-05: Hover media card chỉ hiển thị hiệu ứng, không tự mở detail và không đổi item.

BR-PGAL-GRID-06: Click media card mới mở detail của gallery item tương ứng.

BR-PGAL-GRID-07: Detail gallery item hiển thị breadcrumb, title, description và toàn bộ media ACTIVE của item đó.

BR-PGAL-GRID-08: Bỏ carousel chuyển bài đăng dạng “Bài đăng 1/3”.

BR-PGAL-GRID-09: Carousel trong detail chỉ dùng để chuyển media của gallery item hiện tại.

BR-PGAL-GRID-10: Public grid chỉ hiển thị gallery item PUBLISHED.

BR-PGAL-GRID-11: Public grid không hiển thị item thuộc location INACTIVE hoặc area INACTIVE.

BR-PGAL-GRID-12: API public không trả dữ liệu quản trị như created_by, updated_by, deleted_by, object_key, checksum, external_file_id raw.

BR-PGAL-GRID-13: Media public phải dùng file proxy backend, không dùng Google Drive raw link nếu raw link không public-safe.
```

---

## 13. Acceptance Criteria

### AC-PGAL-GRID-01 — Click location hiển thị grid item

Given location `TÒA ALPHA / Trước tòa` có 8 gallery item PUBLISHED  
When user click location `Trước tòa`  
Then hệ thống hiển thị 8 media card trong grid  
And mỗi card đại diện cho một gallery item.

### AC-PGAL-GRID-02 — Một dòng có 5 media

Given location có 8 gallery item  
When user mở grid trên desktop  
Then dòng đầu hiển thị 5 media card  
And dòng thứ hai hiển thị 3 media card.

### AC-PGAL-GRID-03 — Card dùng media primary

Given mỗi gallery item có nhiều media  
When render grid  
Then mỗi card lấy media có `is_primary = 1` của gallery item đó.

### AC-PGAL-GRID-04 — Hover card có hiệu ứng

Given user đang xem grid  
When user hover một media card  
Then card có hiệu ứng hover  
And hiển thị overlay/title  
And không tự mở detail.

### AC-PGAL-GRID-05 — Click card mở detail

Given user đang xem grid  
When user click một media card  
Then hệ thống mở detail của gallery item tương ứng  
And hiển thị breadcrumb, title, description, toàn bộ media.

### AC-PGAL-GRID-06 — Không còn bài đăng 1/3

Given location có nhiều gallery item  
When user click location  
Then hệ thống không hiển thị text `Bài đăng 1/3`  
And không dùng mũi tên chuyển gallery item như carousel bài đăng.

### AC-PGAL-GRID-07 — Detail vẫn có media carousel

Given gallery item có nhiều media  
When user mở detail  
Then user có thể chuyển media trước/sau trong gallery item đó.

### AC-PGAL-GRID-08 — Quay lại grid

Given user đang ở detail gallery item  
When user click `Quay lại danh sách`  
Then hệ thống quay về grid của location hiện tại  
And không mất selected campus/area/location.

### AC-PGAL-GRID-09 — Không hiển thị hidden item

Given location có 3 item PUBLISHED và 2 item HIDDEN  
When user mở public grid của location  
Then chỉ 3 item PUBLISHED được hiển thị.

### AC-PGAL-GRID-10 — Không hiển thị item của inactive location

Given location đang INACTIVE  
When user mở public Gallery  
Then location đó không hiển thị trong public navigation  
And toàn bộ item thuộc location đó không hiển thị public.

---

## 14. Checklist cho AI Agent

```text
[ ] Bỏ UI “Bài đăng x/y” ở public Gallery.
[ ] Bỏ mũi tên chuyển gallery item dạng carousel bài đăng.
[ ] Khi click location, gọi API lấy danh sách gallery item public của location.
[ ] Render gallery item dưới dạng grid.
[ ] Desktop: 1 dòng hiển thị 5 media card.
[ ] Mỗi card lấy media is_primary của gallery item.
[ ] Video card có icon play/thumbnail.
[ ] Hover card có overlay/scale/title.
[ ] Click card mở detail gallery item.
[ ] Detail giữ breadcrumb area > location.
[ ] Detail hiển thị title, description, toàn bộ media của item.
[ ] Detail vẫn hỗ trợ chuyển media trước/sau.
[ ] Có nút quay lại grid location.
[ ] Public API chỉ trả item PUBLISHED.
[ ] Public API chỉ trả location ACTIVE + area ACTIVE.
[ ] Public API không expose metadata nội bộ.
[ ] Handle empty grid.
[ ] Handle item vừa bị hidden/disabled khi user đang xem.
[ ] Build backend.
[ ] Build frontend.
[ ] Test location có 0, 1, 5, 6, nhiều gallery item.
```

---

## 15. Chốt cuối cùng

Logic mới của public Gallery là:

```text
Click area/location không còn mở từng bài đăng dạng 1/3.
Click location sẽ mở một album grid.
Mỗi gallery item là một card trong grid.
Card dùng media is_primary làm ảnh/video đại diện.
Click card mới mở detail gallery item.
Detail gallery item giữ cách hiển thị hiện tại.
```

Như vậy UI sẽ hợp lý hơn với mô hình mới **1 location có nhiều gallery item**, vì người dùng nhìn được toàn bộ nội dung của vị trí đó ngay trên một màn hình thay vì phải bấm từng bài đăng một.
