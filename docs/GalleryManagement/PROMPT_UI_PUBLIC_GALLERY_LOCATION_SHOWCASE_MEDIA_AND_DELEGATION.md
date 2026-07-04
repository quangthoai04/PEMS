# PROMPT / ĐẶC TẢ CẬP NHẬT — Public VisitFPTU Gallery Location Showcase: MEDIA Column và VISIT_DELEGATION Row

## 0. Mục tiêu tài liệu

Tài liệu này dùng cho AI Agent đọc và cập nhật UI Public VisitFPTU Gallery ở màn **Location Showcase**.

Mục tiêu chính:

```text
Khi user click vào một ảnh vị trí cụ thể ở màn Area Showcase,
hệ thống mở màn Location Showcase.

Tại Location Showcase:
- Background fullscreen là ảnh đại diện của vị trí cụ thể.
- Góc dưới bên trái hiển thị tên khu vực / tên vị trí.
- Có nút < > để chuyển vị trí trước/sau trong cùng khu vực.
- Bên phải có cột ảnh dọc chứa các gallery item có item_type = MEDIA.
- Phía dưới tên vị trí có section "Đoàn khách đã tới thăm" nếu có gallery item item_type = VISIT_DELEGATION.
- Click ảnh MEDIA hoặc ảnh Đoàn khách ở phase này chỉ set active, chưa mở detail/lightbox.
```

---

## 1. Bối cảnh hiện tại

Public VisitFPTU Gallery hiện đã có màn **Area Showcase**:

```text
- User chọn campus.
- Sidebar bên trái hiển thị danh sách khu vực.
- User click một khu vực.
- Ảnh đại diện khu vực được hiển thị phủ toàn màn hình.
- Có lớp overlay đen mờ.
- Tên khu vực hiển thị ở góc dưới bên trái.
- Bên phải có cột ảnh dọc.
- Mỗi ảnh trong cột dọc là ảnh đại diện của một vị trí cụ thể thuộc khu vực đó.
- Ảnh active có highlight viền trắng phát sáng.
- Có mũi tên lên/xuống để chuyển ảnh vị trí active.
- Có counter dạng 03/12.
```

Yêu cầu lần này là thiết kế tiếp màn **Location Showcase** sau khi user click vào một ảnh vị trí cụ thể trong cột ảnh dọc của Area Showcase.

---

## 2. Phạm vi cập nhật

### 2.1. Trong scope

Cập nhật UI/logic public cho màn Location Showcase:

```text
1. Click thumbnail vị trí ở Area Showcase để mở Location Showcase.
2. Location Showcase dùng ảnh đại diện vị trí làm background fullscreen.
3. Có overlay đen mờ giống Area Showcase.
4. Hiển thị tên khu vực / tên vị trí ở góc dưới bên trái.
5. Có nút < > để chuyển vị trí trước/sau trong cùng khu vực.
6. Thêm cột ảnh dọc bên phải cho gallery item item_type = MEDIA.
7. Thêm section ngang "Đoàn khách đã tới thăm" cho gallery item item_type = VISIT_DELEGATION.
8. Click ảnh MEDIA hoặc ảnh Đoàn khách chỉ set active, chưa mở detail.
9. Nếu đổi vị trí thì reload lại MEDIA items và VISIT_DELEGATION items theo vị trí mới.
```

### 2.2. Không nằm trong scope

Không làm trong phase này:

```text
- Không sửa database.
- Không sửa module Quản lý Gallery nội bộ.
- Không sửa module Quản lý khu vực nội bộ.
- Không sửa upload.
- Không link gallery item Đoàn khách với visit_instance.
- Không mở detail gallery item.
- Không mở lightbox.
- Không đổi background chính khi click gallery item MEDIA.
- Không đổi background chính khi click ảnh Đoàn khách.
- Không thêm tab Media / Đoàn khách ở public.
- Không mock data.
- Không sinh file rác.
```

---

## 3. Luồng tổng quát

### 3.1. Từ Area Showcase sang Location Showcase

```text
Given user đang ở Area Showcase
And bên phải có cột ảnh dọc các vị trí cụ thể
When user click vào một ảnh vị trí bất kỳ
Then hệ thống mở Location Showcase của vị trí đó
And background chính là ảnh đại diện của vị trí cụ thể được chọn.
```

Ví dụ:

```text
User đang xem khu vực LAB ZONE.
Cột ảnh dọc có:
- Phòng Lab
- Studio
- Phòng thực hành
- Phòng máy

User click thumbnail "Phòng Lab"
→ Hệ thống mở màn vị trí cụ thể "LAB ZONE / Phòng Lab".
```

---

## 4. Màn Location Showcase

Màn Location Showcase được thiết kế dựa trên Area Showcase hiện tại.

Layout tổng quan:

```text
- Background fullscreen: ảnh đại diện vị trí cụ thể.
- Overlay: lớp phủ đen mờ.
- Sidebar trái: giữ nguyên nếu public page hiện đang có sidebar.
- Góc dưới bên trái:
  + Tên khu vực.
  + Tên vị trí.
  + Mũi tên chuyển vị trí < >.
  + Section "Đoàn khách đã tới thăm" nếu có.
- Bên phải:
  + Cột ảnh dọc MEDIA items của vị trí đang chọn.
  + Mũi tên lên/xuống.
  + Counter hiện tại/tổng số MEDIA items.
```

---

## 5. Background ảnh vị trí cụ thể

Khi vào Location Showcase, ảnh nền chính phải là ảnh đại diện của vị trí cụ thể:

```text
gallery_locations.cover_file_id
→ /api/files/{coverFileId}/content
```

Yêu cầu UI:

```text
- Ảnh vị trí phủ toàn màn hình.
- Dùng object-fit: cover cho background chính.
- Không bị méo ảnh.
- Không bị nhét vào card/panel nhỏ.
- Có overlay đen mờ phủ lên toàn bộ ảnh.
- Overlay giống với Area Showcase để giữ đồng bộ UI.
```

Gợi ý CSS:

```css
.location-showcase {
  position: relative;
  min-height: calc(100vh - 80px);
  overflow: hidden;
}

.location-showcase-background {
  position: absolute;
  inset: 0;
  width: 100%;
  height: 100%;
}

.location-showcase-background img {
  width: 100%;
  height: 100%;
  object-fit: cover;
}

.location-showcase-overlay {
  position: absolute;
  inset: 0;
  background: rgba(0, 0, 0, 0.52);
  z-index: 1;
}
```

---

## 6. Tên khu vực / vị trí ở góc dưới bên trái

Ở phía dưới cùng bên trái, hiển thị tên theo dạng:

```text
KHU VỰC / VỊ TRÍ
```

Ví dụ:

```text
LAB ZONE
Phòng Lab
```

Hoặc nếu UI muốn cùng một dòng:

```text
LAB ZONE / Phòng Lab
```

Yêu cầu style:

```text
- Tên khu vực màu trắng, in đậm, cỡ chữ lớn hơn.
- Tên vị trí màu trắng hoặc trắng mờ, cỡ chữ nhỏ hơn một chút.
- Tên vị trí không in đậm hoặc font-weight nhẹ hơn.
- Text nằm trên overlay và dễ đọc.
- Vị trí text tương tự Area Showcase, ở bottom-left.
- Không bị sidebar che.
```

Gợi ý layout:

```html
<div class="location-title-block">
  <div class="location-area-name">LAB ZONE</div>
  <div class="location-name">Phòng Lab</div>
</div>
```

Gợi ý CSS:

```css
.location-title-block {
  position: absolute;
  left: 120px;
  bottom: 92px;
  z-index: 3;
  color: #fff;
}

.location-area-name {
  font-size: 42px;
  line-height: 1.05;
  font-weight: 900;
  letter-spacing: 0.02em;
  text-transform: uppercase;
}

.location-name {
  margin-top: 8px;
  font-size: 30px;
  line-height: 1.15;
  font-weight: 500;
  color: rgba(255, 255, 255, 0.88);
}
```

---

## 7. Mũi tên chuyển vị trí trước/sau

Bên cạnh hoặc bên dưới cụm tên khu vực/vị trí cần có 2 nút:

```text
<   >
```

Mục đích:

```text
- Chuyển sang vị trí phía trước trong cùng khu vực.
- Chuyển sang vị trí tiếp theo trong cùng khu vực.
```

Yêu cầu UI:

```text
- Hai nút nằm gần cụm tên khu vực/vị trí.
- Style trong suốt/glassmorphism, hiện đại.
- Có border trắng mờ.
- Có hover sáng nhẹ.
- Không giống button dashboard.
- Có thể dùng icon chevron trái/phải.
```

Gợi ý layout:

```text
LAB ZONE
Phòng Lab
[ < ] [ > ]
```

Gợi ý CSS:

```css
.location-nav-arrows {
  display: flex;
  gap: 12px;
  margin-top: 18px;
}

.location-nav-button {
  width: 48px;
  height: 48px;
  border-radius: 999px;
  border: 1px solid rgba(255, 255, 255, 0.28);
  background: rgba(255, 255, 255, 0.10);
  color: #fff;
  backdrop-filter: blur(14px);
  display: inline-flex;
  align-items: center;
  justify-content: center;
  font-size: 24px;
  font-weight: 700;
  transition: all 0.2s ease;
  cursor: pointer;
}

.location-nav-button:hover {
  background: rgba(255, 255, 255, 0.20);
  border-color: rgba(255, 255, 255, 0.52);
  transform: translateY(-2px);
}
```

---

## 8. Logic chuyển vị trí trước/sau

Dữ liệu vị trí lấy từ danh sách `locations` thuộc `area` hiện tại.

### 8.1. Bấm nút `<`

```text
- Tìm vị trí trước đó trong danh sách locations của khu vực hiện tại.
- Cập nhật selectedLocation.
- Background đổi sang ảnh đại diện vị trí mới.
- Text khu vực/vị trí đổi theo vị trí mới.
- Reload MEDIA items của vị trí mới.
- Reload VISIT_DELEGATION items của vị trí mới.
- Reset activeMediaItemIndex = 0.
- Reset activeVisitDelegationIndex = 0.
- Nếu đang ở vị trí đầu tiên và bấm < thì loop sang vị trí cuối cùng.
```

### 8.2. Bấm nút `>`

```text
- Tìm vị trí tiếp theo trong danh sách locations của khu vực hiện tại.
- Cập nhật selectedLocation.
- Background đổi sang ảnh đại diện vị trí mới.
- Text khu vực/vị trí đổi theo vị trí mới.
- Reload MEDIA items của vị trí mới.
- Reload VISIT_DELEGATION items của vị trí mới.
- Reset activeMediaItemIndex = 0.
- Reset activeVisitDelegationIndex = 0.
- Nếu đang ở vị trí cuối cùng và bấm > thì loop về vị trí đầu tiên.
```

Chốt:

```text
- Có loop để trải nghiệm mượt.
- Thứ tự vị trí dùng đúng thứ tự location trong area hiện tại.
- Ưu tiên theo display_order ASC, location_name ASC hoặc theo thứ tự API trả về nếu API đã sort đúng.
```

Pseudo logic:

```ts
function goToPreviousLocation() {
  const currentIndex = locations.findIndex(x => x.locationId === selectedLocationId);
  const prevIndex = currentIndex <= 0 ? locations.length - 1 : currentIndex - 1;
  setSelectedLocation(locations[prevIndex]);
}

function goToNextLocation() {
  const currentIndex = locations.findIndex(x => x.locationId === selectedLocationId);
  const nextIndex = currentIndex >= locations.length - 1 ? 0 : currentIndex + 1;
  setSelectedLocation(locations[nextIndex]);
}
```

---

## 9. Cột ảnh dọc MEDIA ở Location Showcase

Tại trang vị trí cụ thể, cần thêm một cột ảnh dọc ở bên phải màn hình.

Cột này có style giống cột ảnh dọc ở Area Showcase, nhưng dữ liệu khác.

### 9.1. Khác biệt dữ liệu

```text
Area Showcase:
- Cột dọc chứa ảnh đại diện của các vị trí cụ thể thuộc khu vực.

Location Showcase:
- Cột dọc chứa ảnh primary của các gallery item có item_type = MEDIA thuộc vị trí cụ thể đang chọn.
```

### 9.2. Điều kiện item MEDIA được hiển thị

Cột ảnh dọc MEDIA lấy dữ liệu từ `gallery_items` với điều kiện:

```text
gallery_items.location_id = selectedLocationId
gallery_items.item_type = 'MEDIA'
gallery_items.status = 'PUBLISHED'
gallery_items.deleted_at IS NULL
gallery_item_media.status = 'ACTIVE'
gallery_item_media.deleted_at IS NULL
gallery_item_media.is_primary = 1
```

Mỗi gallery item MEDIA được đại diện bằng media chính:

```text
gallery_item_media.is_primary = 1
```

Nếu dữ liệu lỗi không có primary media:

```text
Backend fallback lấy media ACTIVE đầu tiên theo:
display_order ASC, media_id ASC.
```

### 9.3. Dữ liệu cần có cho mỗi MEDIA item

```json
{
  "galleryItemId": 70,
  "title": "Không gian Phòng Lab",
  "itemType": "MEDIA",
  "mediaKind": "IMAGE",
  "primaryMedia": {
    "mediaId": 301,
    "fileId": 9001,
    "mediaType": "IMAGE",
    "url": "/api/files/9001/content",
    "thumbnailUrl": "/api/files/9001/content",
    "altText": "Không gian Phòng Lab"
  }
}
```

### 9.4. Media type

Với `mediaType = IMAGE`:

```text
- Hiển thị ảnh.
```

Với `mediaType = VIDEO`:

```text
- Hiển thị thumbnail nếu có.
- Nếu không có thumbnail thì dùng poster hoặc placeholder.
- Có thể hiển thị icon play nhỏ trên thumbnail.
```

---

## 10. UI cột ảnh dọc MEDIA

Cột ảnh dọc MEDIA nằm bên phải màn hình, tương tự cột thumbnail ở Area Showcase.

Yêu cầu:

```text
- Ảnh xếp dọc.
- Có thumbnail active.
- Thumbnail active không bị crop hai bên.
- Thumbnail active có viền trắng và glow/phát sáng.
- Thumbnail không active mờ hơn.
- Có nút mũi tên lên/xuống để chuyển ảnh active.
- Có counter dạng 01/05, 03/12.
- Nếu danh sách nhiều hơn số ảnh hiển thị được, cột phải scroll/slide theo active index.
- Không bị cố định vài ảnh đầu.
```

### 10.1. Click ảnh trong cột MEDIA

Phase này chưa cần mở detail hoặc modal.

Khi user click một ảnh trong cột dọc MEDIA:

```text
- Chỉ set ảnh đó thành active.
- Cập nhật activeMediaItemIndex.
- Cập nhật counter.
- Không mở gallery item detail.
- Không mở lightbox.
- Không đổi background chính.
- Không chuyển page.
```

Background chính của Location Showcase vẫn luôn là ảnh đại diện vị trí cụ thể, không phải ảnh gallery item MEDIA.

### 10.2. Mũi tên lên/xuống trong cột MEDIA

Bấm mũi tên xuống:

```text
activeMediaItemIndex = activeMediaItemIndex + 1.
Nếu đang ở ảnh cuối thì loop về ảnh đầu.
```

Bấm mũi tên lên:

```text
activeMediaItemIndex = activeMediaItemIndex - 1.
Nếu đang ở ảnh đầu thì loop về ảnh cuối.
```

Sau khi active index thay đổi:

```text
- Thumbnail active phải luôn nằm trong vùng nhìn thấy.
- Nếu danh sách nhiều hơn số thumbnail đang hiển thị, container phải scroll/slide theo active item.
- Counter cập nhật đúng.
```

Ví dụ:

```text
01/08 bấm xuống → 02/08
04/08 bấm xuống → 05/08 và cột thumbnail lướt xuống để thấy ảnh số 5
08/08 bấm xuống → 01/08
```

### 10.3. Counter của cột MEDIA

Dưới cột ảnh dọc MEDIA hiển thị counter:

```text
01/05
03/12
```

Format:

```text
- Số hiện tại dùng 2 chữ số.
- Tổng số dùng 2 chữ số nếu nhỏ hơn 10.
```

Ví dụ:

```text
1 item  → 01/01
5 item  → 01/05
12 item → 03/12
```

### 10.4. Empty state cho cột MEDIA

Nếu vị trí cụ thể không có gallery item `item_type = MEDIA` public:

```text
- Ẩn toàn bộ cột ảnh dọc MEDIA để màn sạch hơn.
- Không hiển thị mũi tên/counter sai.
- Không làm UI lỗi.
```

---

## 11. Section "Đoàn khách đã tới thăm"

Nếu vị trí cụ thể đang chọn có gallery item thuộc loại Đoàn khách thì hiển thị section này.

### 11.1. Điều kiện hiển thị

Hiển thị section khi có ít nhất một item thỏa:

```text
gallery_items.location_id = selectedLocationId
gallery_items.item_type = 'VISIT_DELEGATION'
gallery_items.status = 'PUBLISHED'
gallery_items.deleted_at IS NULL
gallery_item_media.status = 'ACTIVE'
gallery_item_media.deleted_at IS NULL
```

Nếu không có item nào:

```text
- Không hiển thị section "Đoàn khách đã tới thăm".
- Không để khoảng trống xấu.
```

### 11.2. Dữ liệu trong section Đoàn khách

Mỗi gallery item Đoàn khách được đại diện bằng media chính:

```text
gallery_item_media.is_primary = 1
```

Nếu dữ liệu lỗi không có primary media:

```text
Backend fallback lấy media ACTIVE đầu tiên theo:
display_order ASC, media_id ASC.
```

Dữ liệu cần có:

```json
{
  "galleryItemId": 88,
  "title": "Đoàn ABC tham quan Phòng Lab",
  "itemType": "VISIT_DELEGATION",
  "mediaKind": "IMAGE",
  "primaryMedia": {
    "mediaId": 401,
    "fileId": 9101,
    "mediaType": "IMAGE",
    "url": "/api/files/9101/content",
    "thumbnailUrl": "/api/files/9101/content",
    "altText": "Đoàn ABC tham quan Phòng Lab"
  }
}
```

### 11.3. UI section Đoàn khách

Section nằm dưới cụm tên khu vực/vị trí ở góc dưới bên trái.

Cấu trúc:

```text
LAB ZONE
Phòng Lab

[ < ] [ > ]

Đoàn khách đã tới thăm

[Ảnh 1] [Ảnh 2] [Ảnh 3] [Ảnh 4] ...

05 ảnh
```

Yêu cầu UI:

```text
- Title "Đoàn khách đã tới thăm" màu trắng.
- Nằm dưới tên vị trí và nút chuyển vị trí.
- Cỡ chữ vừa, rõ ràng.
- Danh sách ảnh nằm ngang.
- Style ảnh giống thumbnail nhưng xếp ngang.
- Ảnh active có viền trắng glow.
- Thumbnail không active opacity nhẹ hơn.
- Ảnh active không bị crop hai bên nếu có active state.
- Danh sách ảnh ngang có thể scroll ngang nếu nhiều item.
- Bên dưới danh sách có số lượng.
```

Gợi ý CSS:

```css
.visit-delegation-section {
  margin-top: 28px;
  max-width: 720px;
}

.visit-delegation-title {
  color: #fff;
  font-size: 20px;
  font-weight: 800;
  margin-bottom: 14px;
}

.visit-delegation-list {
  display: flex;
  gap: 12px;
  overflow-x: auto;
  padding: 4px 4px 10px;
  scrollbar-width: thin;
}

.visit-delegation-thumb {
  flex: 0 0 auto;
  width: 96px;
  height: 72px;
  border-radius: 16px;
  overflow: hidden;
  border: 1px solid rgba(255, 255, 255, 0.18);
  background: rgba(0, 0, 0, 0.35);
  opacity: 0.72;
  transition: all 0.2s ease;
  cursor: pointer;
}

.visit-delegation-thumb img {
  width: 100%;
  height: 100%;
  object-fit: cover;
}

.visit-delegation-thumb.active {
  border: 2px solid rgba(255, 255, 255, 0.95);
  box-shadow:
    0 0 0 4px rgba(255, 255, 255, 0.16),
    0 0 24px rgba(255, 255, 255, 0.45);
  opacity: 1;
  transform: scale(1.04);
}

.visit-delegation-count {
  margin-top: 8px;
  color: rgba(255, 255, 255, 0.72);
  font-size: 14px;
  font-weight: 700;
}
```

### 11.4. Click ảnh Đoàn khách

Phase này chưa cần mở detail.

Khi user click ảnh trong danh sách ngang Đoàn khách:

```text
- Chỉ set activeVisitDelegationIndex.
- Cập nhật active style.
- Không mở detail.
- Không mở lightbox.
- Không đổi background chính.
- Không chuyển page.
```

### 11.5. Số lượng bên dưới danh sách ảnh

Bên dưới danh sách ảnh ngang hiển thị tổng số item Đoàn khách.

Chốt đề xuất:

```text
05 ảnh
```

Nếu có active counter thì có thể hiển thị:

```text
01/05
```

Nhưng không bắt buộc.

---

## 12. Phân biệt rõ MEDIA và VISIT_DELEGATION

Tại Location Showcase có 2 nhóm gallery item khác nhau:

### 12.1. Cột ảnh dọc bên phải

```text
- Hiển thị gallery item có item_type = MEDIA.
- Đại diện cho ảnh/video giới thiệu vị trí.
- Style giống thumbnail dọc ở Area Showcase.
```

### 12.2. Section ngang "Đoàn khách đã tới thăm"

```text
- Hiển thị gallery item có item_type = VISIT_DELEGATION.
- Đại diện cho ảnh/video đoàn khách đã tới thăm tại vị trí đó.
- Style giống thumbnail nhưng xếp ngang.
```

Không được trộn 2 loại này vào nhau.

---

## 13. API đề xuất

Nếu đang có endpoint Location Showcase, cập nhật endpoint đó để trả cả `mediaItems` và `visitDelegationItems`.

```http
GET /api/public/visit-fptu/locations/{locationId}/showcase
```

Response đề xuất:

```json
{
  "campus": {
    "campusId": 1,
    "campusCode": "HN",
    "campusName": "FPT University Hà Nội"
  },
  "area": {
    "areaId": 3,
    "areaName": "LAB ZONE",
    "areaCoverUrl": "/api/files/1001/content"
  },
  "location": {
    "locationId": 25,
    "locationName": "Phòng Lab",
    "locationCoverUrl": "/api/files/2001/content"
  },
  "locationsInArea": [
    {
      "locationId": 25,
      "locationName": "Phòng Lab",
      "locationCoverUrl": "/api/files/2001/content"
    },
    {
      "locationId": 26,
      "locationName": "Studio",
      "locationCoverUrl": "/api/files/2002/content"
    }
  ],
  "mediaItems": [
    {
      "galleryItemId": 70,
      "title": "Không gian Phòng Lab",
      "itemType": "MEDIA",
      "mediaKind": "IMAGE",
      "primaryMedia": {
        "mediaId": 301,
        "fileId": 9001,
        "mediaType": "IMAGE",
        "url": "/api/files/9001/content",
        "thumbnailUrl": "/api/files/9001/content",
        "altText": "Không gian Phòng Lab"
      }
    }
  ],
  "visitDelegationItems": [
    {
      "galleryItemId": 88,
      "title": "Đoàn ABC tham quan Phòng Lab",
      "itemType": "VISIT_DELEGATION",
      "mediaKind": "IMAGE",
      "primaryMedia": {
        "mediaId": 401,
        "fileId": 9101,
        "mediaType": "IMAGE",
        "url": "/api/files/9101/content",
        "thumbnailUrl": "/api/files/9101/content",
        "altText": "Đoàn ABC tham quan Phòng Lab"
      }
    }
  ]
}
```

Nếu không muốn gom một endpoint, có thể dùng 2 endpoint riêng:

```http
GET /api/public/visit-fptu/locations/{locationId}/media-items
GET /api/public/visit-fptu/locations/{locationId}/visit-delegation-items
```

Chốt đề xuất:

```text
Ưu tiên gom vào showcase endpoint nếu màn Location Showcase cần load một lần.
```

---

## 14. Query lấy MEDIA items cho cột dọc

SQL logic:

```sql
SELECT
    gi.gallery_item_id,
    gi.title,
    gi.item_type,
    gi.media_kind,
    pm.media_id,
    pm.file_id,
    pm.media_type,
    pm.thumbnail_file_id,
    pm.alt_text
FROM gallery_items gi
JOIN gallery_locations gl
    ON gl.location_id = gi.location_id
JOIN gallery_areas ga
    ON ga.area_id = gl.area_id
JOIN campuses c
    ON c.campus_id = ga.campus_id
JOIN gallery_item_media pm
    ON pm.gallery_item_id = gi.gallery_item_id
   AND pm.status = 'ACTIVE'
   AND pm.deleted_at IS NULL
WHERE gi.location_id = @LocationId
  AND gi.item_type = 'MEDIA'
  AND gi.status = 'PUBLISHED'
  AND gi.deleted_at IS NULL
  AND gl.status = 'ACTIVE'
  AND ga.status = 'ACTIVE'
  AND c.status = 'ACTIVE'
  AND pm.is_primary = 1
ORDER BY
    gi.display_order ASC,
    gi.created_at DESC,
    gi.gallery_item_id DESC;
```

Nếu thiếu primary media, fallback lấy media ACTIVE đầu tiên theo:

```text
display_order ASC, media_id ASC
```

---

## 15. Query lấy VISIT_DELEGATION items cho section ngang

SQL logic:

```sql
SELECT
    gi.gallery_item_id,
    gi.title,
    gi.item_type,
    gi.media_kind,
    pm.media_id,
    pm.file_id,
    pm.media_type,
    pm.thumbnail_file_id,
    pm.alt_text
FROM gallery_items gi
JOIN gallery_locations gl
    ON gl.location_id = gi.location_id
JOIN gallery_areas ga
    ON ga.area_id = gl.area_id
JOIN campuses c
    ON c.campus_id = ga.campus_id
JOIN gallery_item_media pm
    ON pm.gallery_item_id = gi.gallery_item_id
   AND pm.status = 'ACTIVE'
   AND pm.deleted_at IS NULL
WHERE gi.location_id = @LocationId
  AND gi.item_type = 'VISIT_DELEGATION'
  AND gi.status = 'PUBLISHED'
  AND gi.deleted_at IS NULL
  AND gl.status = 'ACTIVE'
  AND ga.status = 'ACTIVE'
  AND c.status = 'ACTIVE'
  AND pm.is_primary = 1
ORDER BY
    gi.display_order ASC,
    gi.created_at DESC,
    gi.gallery_item_id DESC;
```

Nếu thiếu primary media, fallback lấy media ACTIVE đầu tiên theo:

```text
display_order ASC, media_id ASC
```

---

## 16. State frontend đề xuất

Cần có state:

```ts
type LocationShowcaseState = {
  selectedAreaId: number | null;
  selectedArea: PublicGalleryArea | null;

  selectedLocationId: number | null;
  selectedLocation: PublicGalleryLocation | null;
  selectedLocationIndex: number;
  locationsInSelectedArea: PublicGalleryLocation[];

  mediaItems: PublicGalleryShowcaseItem[];
  activeMediaItemIndex: number;
  mediaItemRefs: React.RefObject<HTMLElement>[];

  visitDelegationItems: PublicGalleryShowcaseItem[];
  activeVisitDelegationIndex: number;

  isLocationShowcaseMode: boolean;
  isMediaItemsLoading: boolean;
  isDelegationLoading: boolean;
};
```

### 16.1. Khi click thumbnail vị trí từ Area Showcase

```text
- set selectedLocation.
- set selectedLocationIndex.
- set isLocationShowcaseMode = true.
- load mediaItems của selectedLocation.
- load visitDelegationItems của selectedLocation.
- activeMediaItemIndex = 0.
- activeVisitDelegationIndex = 0.
```

### 16.2. Khi bấm mũi tên chuyển vị trí `< >`

```text
- update selectedLocation.
- update selectedLocationIndex.
- background đổi theo selectedLocation.locationCoverUrl.
- load lại mediaItems.
- load lại visitDelegationItems.
- reset activeMediaItemIndex = 0.
- reset activeVisitDelegationIndex = 0.
```

### 16.3. Khi click ảnh trong cột dọc MEDIA

```text
- set activeMediaItemIndex.
- scroll thumbnail active vào vùng nhìn thấy nếu cần.
- không mở detail.
```

### 16.4. Khi click ảnh trong danh sách ngang Đoàn khách

```text
- set activeVisitDelegationIndex.
- không mở detail.
```

---

## 17. Empty / fallback states

### 17.1. Không có MEDIA item

```text
- Ẩn toàn bộ cột ảnh dọc MEDIA.
- Không hiển thị counter/mũi tên MEDIA.
- UI không lỗi.
```

### 17.2. Không có VISIT_DELEGATION item

```text
- Không hiển thị section "Đoàn khách đã tới thăm".
- Không để khoảng trống xấu.
```

### 17.3. Vị trí thiếu cover image

```text
- Dùng fallback ảnh khu vực hoặc ảnh campus.
- Không crash UI.
```

### 17.4. Gallery item thiếu primary media

```text
- Backend fallback media đầu tiên.
- Nếu vẫn không có media ACTIVE thì không trả item đó.
```

---

## 18. Không làm trong phase này

Không làm:

```text
- Không sửa database.
- Không sửa quản lý Gallery nội bộ.
- Không sửa quản lý khu vực nội bộ.
- Không sửa upload.
- Không link visit instance.
- Không mở detail gallery item khi click ảnh MEDIA.
- Không mở detail gallery item khi click ảnh Đoàn khách.
- Không mở lightbox.
- Không đổi background chính khi click gallery item.
- Không thêm tab Media / Đoàn khách ở public.
- Không mock data.
- Không sinh file rác.
```

---

## 19. Acceptance Criteria

### AC-LOC-01 — Click ảnh vị trí mở Location Showcase

```text
Given user đang ở Area Showcase
When user click một ảnh vị trí trong cột ảnh dọc
Then hệ thống mở màn Location Showcase
And background là ảnh đại diện của vị trí đó.
```

### AC-LOC-02 — Background vị trí có overlay

```text
Given user đang ở Location Showcase
Then ảnh đại diện vị trí phủ toàn màn hình
And có lớp overlay đen mờ giống Area Showcase.
```

### AC-LOC-03 — Hiển thị đúng khu vực / vị trí

```text
Given selected location thuộc area LAB ZONE
When mở Location Showcase
Then hệ thống hiển thị LAB ZONE bằng chữ trắng, in đậm, cỡ lớn
And hiển thị tên vị trí bên dưới nhỏ hơn, không in đậm.
```

### AC-LOC-04 — Mũi tên chuyển vị trí trước/sau

```text
Given area có nhiều vị trí
When user bấm mũi tên >
Then hệ thống chuyển sang vị trí tiếp theo trong cùng area
And background, tên vị trí, MEDIA list, delegation list cập nhật đúng.
```

### AC-LOC-05 — Loop vị trí

```text
Given user đang ở vị trí cuối cùng trong area
When bấm mũi tên >
Then hệ thống chuyển về vị trí đầu tiên.
```

### AC-MEDIA-01 — Location Showcase có cột MEDIA bên phải

```text
Given user mở Location Showcase của một vị trí có gallery item MEDIA
Then bên phải màn hình hiển thị cột ảnh dọc
And mỗi ảnh đại diện cho một gallery item item_type = MEDIA.
```

### AC-MEDIA-02 — Cột MEDIA dùng primary media

```text
Given một gallery item MEDIA có nhiều media
When render cột ảnh dọc
Then hệ thống dùng media is_primary = 1 làm thumbnail đại diện.
```

### AC-MEDIA-03 — MEDIA và Đoàn khách không bị trộn

```text
Given vị trí có cả item_type = MEDIA và item_type = VISIT_DELEGATION
When mở Location Showcase
Then item MEDIA hiển thị ở cột dọc bên phải
And item VISIT_DELEGATION hiển thị ở section ngang "Đoàn khách đã tới thăm".
```

### AC-MEDIA-04 — Click ảnh MEDIA chưa mở gì

```text
Given user đang xem cột ảnh dọc MEDIA
When click một ảnh trong cột
Then ảnh đó trở thành active
And không mở detail
And không mở lightbox
And không đổi background chính.
```

### AC-MEDIA-05 — Không có MEDIA thì ẩn cột dọc

```text
Given vị trí không có gallery item MEDIA public
When mở Location Showcase
Then không hiển thị cột MEDIA bên phải
And UI không lỗi.
```

### AC-DELEGATION-01 — Có Đoàn khách thì hiển thị section

```text
Given selected location có gallery item PUBLISHED với item_type = VISIT_DELEGATION
When mở Location Showcase
Then hiển thị tiêu đề "Đoàn khách đã tới thăm"
And hiển thị danh sách ảnh ngang đại diện cho các gallery item đó.
```

### AC-DELEGATION-02 — Không có Đoàn khách thì ẩn section

```text
Given selected location không có gallery item VISIT_DELEGATION public
When mở Location Showcase
Then không hiển thị section "Đoàn khách đã tới thăm"
And không để khoảng trống xấu.
```

### AC-DELEGATION-03 — Click ảnh Đoàn khách chưa mở gì

```text
Given user đang xem section Đoàn khách đã tới thăm
When click một ảnh trong danh sách ngang
Then ảnh đó trở thành active
And không mở detail
And không mở lightbox
And không đổi background chính.
```

### AC-DELEGATION-04 — Số lượng hiển thị đúng

```text
Given selected location có 5 gallery item Đoàn khách public
When mở Location Showcase
Then bên dưới danh sách ảnh hiển thị "05 ảnh" hoặc counter tương đương.
```

### AC-RELOAD-01 — Đổi vị trí reload đúng dữ liệu

```text
Given user đang ở Location Showcase
When bấm mũi tên chuyển sang vị trí tiếp theo
Then background đổi sang ảnh vị trí mới
And cột MEDIA reload theo vị trí mới
And section Đoàn khách reload theo vị trí mới
And active indexes reset về 0.
```

---

## 20. Checklist triển khai

```text
[ ] Khi click thumbnail vị trí ở Area Showcase, chuyển sang Location Showcase.
[ ] Background Location Showcase dùng locationCoverUrl.
[ ] Thêm overlay đen mờ giống Area Showcase.
[ ] Hiển thị areaName lớn, bold, màu trắng.
[ ] Hiển thị locationName nhỏ hơn, không bold.
[ ] Thêm 2 nút mũi tên < > chuyển vị trí trong cùng area.
[ ] Chuyển vị trí cập nhật background, title, MEDIA list, delegation list.
[ ] Có loop khi chuyển vị trí đầu/cuối.
[ ] Thêm cột ảnh dọc MEDIA ở Location Showcase.
[ ] Cột MEDIA nằm bên phải, style giống Area Showcase thumbnail column.
[ ] Cột MEDIA lấy gallery_items.item_type = MEDIA.
[ ] Chỉ lấy item PUBLISHED, chưa deleted.
[ ] Mỗi item dùng primary media.
[ ] Có fallback nếu thiếu primary media.
[ ] Active MEDIA thumbnail có border trắng glow.
[ ] Active MEDIA thumbnail không bị crop hai bên.
[ ] Có mũi tên lên/xuống cho cột MEDIA nếu cần.
[ ] Có counter cho cột MEDIA.
[ ] Click MEDIA thumbnail chỉ set active, không mở gì.
[ ] Load gallery items có item_type = VISIT_DELEGATION theo location.
[ ] Chỉ lấy item PUBLISHED, chưa deleted, media ACTIVE.
[ ] Mỗi delegation item dùng primary media.
[ ] Render section "Đoàn khách đã tới thăm" nếu có dữ liệu.
[ ] Danh sách ảnh Đoàn khách nằm ngang.
[ ] Style danh sách ảnh ngang giống thumbnail nhưng xếp ngang.
[ ] Active delegation thumbnail có viền trắng glow.
[ ] Click ảnh Đoàn khách chỉ set active, không mở gì.
[ ] Không trộn MEDIA và VISIT_DELEGATION.
[ ] Đổi vị trí bằng < > phải reload cả mediaItems và visitDelegationItems.
[ ] Reset active indexes khi đổi vị trí.
[ ] Không sửa DB.
[ ] Không sửa quản lý nội bộ.
[ ] Không mở gallery item detail ở phase này.
[ ] Build frontend.
[ ] Test area có 1, 2, nhiều location.
[ ] Test location có 0 MEDIA, 1 MEDIA, nhiều MEDIA.
[ ] Test location có 0 Đoàn khách, 1 Đoàn khách, nhiều Đoàn khách.
```

---

## 21. Chốt cuối cùng

Màn Location Showcase sau cập nhật sẽ có cấu trúc:

```text
Location Showcase
├── Background fullscreen = ảnh đại diện vị trí cụ thể
├── Overlay đen mờ
├── Sidebar trái nếu layout hiện tại có
├── Bottom-left content
│   ├── Tên khu vực
│   ├── Tên vị trí
│   ├── Nút chuyển vị trí < >
│   └── Section ngang "Đoàn khách đã tới thăm" nếu có item_type = VISIT_DELEGATION
└── Right vertical column
    └── Danh sách ảnh primary của gallery item item_type = MEDIA
```

Phân loại dữ liệu:

```text
item_type = MEDIA
→ hiển thị ở cột ảnh dọc bên phải.

item_type = VISIT_DELEGATION
→ hiển thị ở section ngang "Đoàn khách đã tới thăm".
```

Click ảnh ở cả 2 nhóm trong phase này:

```text
Chỉ set active, chưa mở detail, chưa mở lightbox, chưa đổi background chính.
```
