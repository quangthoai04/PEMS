# PROMPT / ĐẶC TẢ CẬP NHẬT UI — Public VisitFPTU Gallery Area Showcase

## 0. Mục tiêu tài liệu

Tài liệu này dùng cho AI Agent đọc và cập nhật UI Public VisitFPTU Gallery theo yêu cầu mới.

Mục tiêu chính:

```text
Khi người dùng vào Public VisitFPTU Gallery, chọn xem chi tiết một cơ sở/campus, sau đó bấm vào một khu vực trên sidebar, hệ thống sẽ hiển thị ảnh đại diện của khu vực đó phủ rộng toàn màn hình.

Trên ảnh có overlay đen mờ.
Tên khu vực hiển thị ở góc dưới bên trái bằng chữ trắng, in đậm.
Sidebar khu vực vẫn hiển thị bình thường.
Bên phải màn hình có một cột thumbnail dọc, mỗi thumbnail là ảnh đại diện của một vị trí cụ thể thuộc khu vực đang chọn.
Có nút mũi tên lên/xuống và counter dạng 03/12.
```

Phase này chỉ thiết kế tới màn showcase khu vực. Chưa cần mở detail vị trí, chưa cần mở gallery item grid, chưa cần sửa phần public gallery item detail.

---

## 1. Bối cảnh hiện tại

Dự án đã hoàn thiện các phần liên quan:

```text
1. Quản lý khu vực Gallery:
   - gallery_areas có ảnh đại diện khu vực qua cover_file_id.
   - gallery_locations có ảnh đại diện vị trí qua cover_file_id.

2. Quản lý Gallery:
   - gallery_items thuộc location.
   - gallery_items có item_type để phân biệt Media / Đoàn khách.

3. Public VisitFPTU Gallery hiện tại:
   - User vào menu Visit FPTU.
   - Chọn/xem chi tiết campus, ví dụ FPT University Hà Nội.
   - Sidebar bên trái hiển thị danh sách khu vực.
   - Hiện tại UI đang có dạng hero campus và/hoặc gallery viewer cũ.
```

Yêu cầu mới chỉ tác động đến UI public khi user click một khu vực trên sidebar.

---

## 2. Phạm vi cập nhật

### 2.1. Trong scope

Cần cập nhật:

```text
1. Public Gallery màn campus detail.
2. Hành vi click khu vực trên sidebar.
3. Hiển thị ảnh đại diện khu vực làm background fullscreen.
4. Overlay đen mờ trên ảnh khu vực.
5. Tên khu vực ở góc dưới bên trái.
6. Sidebar vẫn hiển thị bình thường.
7. Cột thumbnail ảnh đại diện vị trí ở bên phải.
8. Nút mũi tên lên/xuống để chuyển thumbnail active.
9. Counter ảnh hiện tại / tổng số ảnh, ví dụ 03/12.
10. Loading/empty/fallback state nếu thiếu ảnh.
```

### 2.2. Không nằm trong scope phase này

Không làm các phần sau:

```text
1. Chưa mở gallery item grid khi click location.
2. Chưa mở detail gallery item.
3. Chưa chia tab Media / Đoàn khách trên public.
4. Chưa hiển thị gallery_items trong màn này.
5. Chưa xử lý public filter theo item_type.
6. Chưa sửa sâu public media carousel cũ.
7. Chưa thay đổi database.
8. Chưa thay đổi nghiệp vụ quản lý Gallery.
9. Không mock data.
10. Không sinh file rác.
```

---

## 3. Luồng tổng quát mới

```text
User click menu Visit FPTU
→ Hệ thống mở Public VisitFPTU Gallery
→ User chọn hoặc đang xem chi tiết một campus, ví dụ FPT University Hà Nội
→ Sidebar bên trái hiển thị danh sách khu vực của campus
→ User click một khu vực trên sidebar, ví dụ LAB ZONE
→ Hệ thống chuyển sang màn Area Showcase
→ Ảnh đại diện của LAB ZONE phủ rộng toàn màn hình
→ Trên ảnh có overlay đen mờ
→ Tên LAB ZONE hiển thị ở góc dưới bên trái
→ Sidebar vẫn hiển thị và LAB ZONE active
→ Bên phải màn hình hiển thị danh sách thumbnail dọc của các vị trí thuộc LAB ZONE
→ User bấm mũi tên lên/xuống để chuyển thumbnail active
→ Counter cập nhật dạng 03/12
```

---

## 4. Màn Area Showcase sau khi click khu vực

### 4.1. Dữ liệu chính cần dùng

Khi user click một area, frontend cần có hoặc gọi được dữ liệu:

```text
Area:
- areaId
- areaName
- areaCoverUrl hoặc coverFileId

Locations thuộc area:
- locationId
- locationName
- locationCoverUrl hoặc coverFileId
- displayOrder nếu có
- status
```

Nguồn ảnh:

```text
Ảnh background lớn:
gallery_areas.cover_file_id
→ /api/files/{coverFileId}/content

Thumbnail vị trí:
gallery_locations.cover_file_id
→ /api/files/{coverFileId}/content
```

---

## 5. Layout tổng thể

### 5.1. Cấu trúc màn hình

Màn hình sau khi chọn area nên có bố cục:

```text
┌─────────────────────────────────────────────────────────────┐
│ Header public                                                │
├─────────────────────────────────────────────────────────────┤
│                                                             │
│  [Sidebar khu vực]       Area cover image fullscreen         │
│                          + overlay đen mờ                   │
│                                                             │
│                          Tên khu vực                        │
│                                                             │
│                                              [↑]             │
│                                              [thumb 1]       │
│                                              [thumb 2]       │
│                                              [thumb 3]       │
│                                              [thumb 4]       │
│                                              [thumb 5]       │
│                                              [↓]             │
│                                              03/12           │
│                                                             │
└─────────────────────────────────────────────────────────────┘
```

### 5.2. Kích thước tổng thể

Khuyến nghị:

```css
.public-gallery-area-showcase {
  position: relative;
  min-height: calc(100vh - var(--header-height, 80px));
  width: 100%;
  overflow: hidden;
}
```

Nếu header public đang cao khoảng 80px, phần showcase chiếm phần còn lại của màn hình.

---

## 6. Ảnh đại diện khu vực fullscreen

### 6.1. Cách hiển thị

Ảnh đại diện khu vực phải:

```text
- Phủ rộng toàn bộ vùng content của Public Gallery.
- Kéo dài toàn màn hình theo chiều ngang và chiều cao.
- Không bị nhét vào card/panel nhỏ.
- Không bị méo ảnh.
- Dùng object-fit: cover.
- Có thể crop tự nhiên theo kích thước màn hình.
```

CSS concept:

```css
.area-showcase-background {
  position: absolute;
  inset: 0;
  width: 100%;
  height: 100%;
  z-index: 0;
}

.area-showcase-background img {
  width: 100%;
  height: 100%;
  object-fit: cover;
  object-position: center;
  display: block;
}
```

### 6.2. Không dùng gallery item làm background

Background màn này phải là ảnh đại diện khu vực:

```text
gallery_areas.cover_file_id
```

Không dùng:

```text
- gallery_items primary media
- gallery_item_media
- ảnh đoàn khách
- ảnh vị trí cụ thể
```

---

## 7. Overlay đen mờ

### 7.1. Mục đích

Trên ảnh đại diện khu vực cần phủ một lớp màu đen mờ để:

```text
- Làm dịu ảnh nền.
- Giúp chữ trắng dễ đọc.
- Tạo cảm giác cinematic/public-facing.
- Gần giống ảnh minh họa gallery viewer có nền tối.
```

### 7.2. CSS concept

```css
.area-showcase-overlay {
  position: absolute;
  inset: 0;
  z-index: 1;
  background: rgba(0, 0, 0, 0.48);
}
```

Có thể dùng gradient để nhìn đẹp hơn:

```css
.area-showcase-overlay {
  position: absolute;
  inset: 0;
  z-index: 1;
  background:
    linear-gradient(
      to right,
      rgba(0, 0, 0, 0.62),
      rgba(0, 0, 0, 0.38),
      rgba(0, 0, 0, 0.52)
    ),
    rgba(0, 0, 0, 0.18);
}
```

Overlay phải phủ toàn bộ ảnh, không chỉ một vùng nhỏ.

---

## 8. Tên khu vực ở góc dưới bên trái

### 8.1. Nội dung

Hiển thị tên khu vực đang chọn:

```text
LAB ZONE
DELTA
ACADEMIC AREA
COVERAGE ZONE
```

Dữ liệu lấy từ:

```text
gallery_areas.area_name
```

### 8.2. Vị trí

Tên khu vực nằm:

```text
- Phía dưới bên trái màn hình.
- Nằm trên ảnh và overlay.
- Không bị sidebar che.
- Canh theo vùng content bên phải sidebar.
```

Vì sidebar nằm bên trái, không đặt title sát mép trái màn hình nếu bị sidebar đè.

CSS concept:

```css
.area-showcase-title-wrap {
  position: absolute;
  z-index: 3;
  left: 320px; /* hoặc tính theo sidebar width + spacing */
  bottom: 80px;
  max-width: 720px;
}

.area-showcase-title {
  color: #fff;
  font-size: 40px;
  line-height: 1.1;
  font-weight: 800;
  letter-spacing: -0.02em;
  text-shadow: 0 12px 32px rgba(0, 0, 0, 0.45);
}
```

Có thể responsive:

```css
@media (max-width: 1024px) {
  .area-showcase-title-wrap {
    left: 32px;
    bottom: 56px;
  }

  .area-showcase-title {
    font-size: 32px;
  }
}
```

### 8.3. Không cần mô tả dài

Phase này chỉ yêu cầu tên khu vực.

Không bắt buộc hiển thị:

```text
- Description khu vực.
- Số lượng gallery item.
- CTA xem chi tiết.
- Button mở location.
```

---

## 9. Sidebar khu vực

### 9.1. Sidebar vẫn hiển thị bình thường

Sidebar bên trái vẫn giữ như màn hiện tại:

```text
- Hiển thị danh sách khu vực.
- User vẫn có thể click khu vực khác.
- Không bị ẩn khi Area Showcase xuất hiện.
- Không bị ảnh background che mất.
- Có z-index cao hơn background và overlay.
```

CSS concept:

```css
.visit-gallery-sidebar {
  position: relative;
  z-index: 5;
}
```

### 9.2. Active state

Khu vực đang chọn phải được active.

Ví dụ user click `LAB ZONE`:

```text
LAB ZONE được highlight active trên sidebar.
```

### 9.3. Click area khác

Khi user click area khác:

```text
1. selectedAreaId đổi sang area mới.
2. Background đổi sang areaCoverUrl của area mới.
3. Tên khu vực đổi theo area mới.
4. Thumbnail list bên phải đổi sang locations của area mới.
5. activeLocationThumbnailIndex reset về 0.
6. Counter reset về 01/n.
```

---

## 10. Cột thumbnail ảnh vị trí bên phải

### 10.1. Nguồn dữ liệu

Cột thumbnail bên phải hiển thị danh sách vị trí cụ thể thuộc khu vực đang chọn.

Mỗi thumbnail dùng:

```text
gallery_locations.cover_file_id
```

Không dùng gallery item media.

### 10.2. Layout

Thumbnail list:

```text
- Nằm bên phải màn hình.
- Xếp theo chiều dọc.
- Nằm trên ảnh nền và overlay.
- Có z-index cao hơn overlay.
- Mỗi ảnh bo góc nhẹ.
- Có khoảng cách đều.
```

CSS concept:

```css
.location-thumbnail-rail {
  position: absolute;
  z-index: 4;
  right: 32px;
  top: 50%;
  transform: translateY(-50%);
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 12px;
}

.location-thumbnail-list {
  display: flex;
  flex-direction: column;
  gap: 12px;
  max-height: 520px;
  overflow: hidden;
}

.location-thumbnail {
  width: 78px;
  height: 78px;
  border-radius: 10px;
  overflow: hidden;
  border: 2px solid rgba(255, 255, 255, 0.22);
  opacity: 0.62;
  cursor: pointer;
  transition: transform .2s ease, opacity .2s ease, border-color .2s ease;
}

.location-thumbnail img {
  width: 100%;
  height: 100%;
  object-fit: cover;
  display: block;
}
```

### 10.3. Thumbnail active

Thumbnail đang active cần nổi bật hơn.

CSS concept:

```css
.location-thumbnail.active {
  opacity: 1;
  border-color: #fff;
  transform: scale(1.06);
  box-shadow: 0 12px 28px rgba(0, 0, 0, 0.4);
}
```

Có thể dùng border cam theo brand:

```css
.location-thumbnail.active {
  border-color: #f37021;
}
```

Chọn một style nhất quán với UI hiện tại.

### 10.4. Số lượng thumbnail hiển thị

Nếu area có nhiều location, không nên hiển thị tất cả nếu vượt chiều cao màn hình.

Đề xuất:

```text
- Hiển thị 5–7 thumbnail gần active index.
- Hoặc hiển thị list có overflow hidden và translate theo active index.
- Phase đầu có thể hiển thị tối đa số thumbnail vừa màn hình.
```

Yêu cầu quan trọng nhất:

```text
Có cột ảnh dọc bên phải.
Có active thumbnail.
Có arrow lên/xuống.
Có counter hiện tại/tổng số.
```

---

## 11. Nút mũi tên lên/xuống

### 11.1. Vị trí

Có 2 mũi tên:

```text
- Mũi tên lên ở phía trên cột thumbnail.
- Mũi tên xuống ở phía dưới cột thumbnail.
```

Hoặc nằm cạnh cột thumbnail nếu UI hiện tại dễ bố trí hơn.

### 11.2. Chức năng

```text
Click arrow down:
→ activeLocationThumbnailIndex tăng 1.

Click arrow up:
→ activeLocationThumbnailIndex giảm 1.
```

### 11.3. Loop

Chốt đề xuất: có loop.

```text
Nếu đang ở ảnh cuối và bấm xuống:
→ quay về ảnh đầu.

Nếu đang ở ảnh đầu và bấm lên:
→ quay về ảnh cuối.
```

Ví dụ:

```text
03/12 + down → 04/12
12/12 + down → 01/12
01/12 + up → 12/12
```

### 11.4. CSS concept

```css
.location-thumb-arrow {
  width: 42px;
  height: 42px;
  border-radius: 999px;
  border: 1px solid rgba(255, 255, 255, 0.32);
  background: rgba(0, 0, 0, 0.28);
  color: #fff;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  cursor: pointer;
  backdrop-filter: blur(8px);
  transition: background .2s ease, transform .2s ease;
}

.location-thumb-arrow:hover {
  background: rgba(255, 255, 255, 0.18);
  transform: scale(1.04);
}
```

---

## 12. Counter ảnh hiện tại / tổng số ảnh

### 12.1. Format

Counter hiển thị bên dưới cột thumbnail:

```text
03/12
```

Format:

```text
currentIndex + 1, padding 2 chữ số / total, padding 2 chữ số
```

Ví dụ:

```text
01/01
01/08
03/12
10/12
```

### 12.2. Ý nghĩa

```text
03 = thumbnail vị trí đang active.
12 = tổng số vị trí có ảnh đại diện thuộc khu vực đang chọn.
```

### 12.3. CSS concept

```css
.location-thumb-counter {
  margin-top: 4px;
  color: #fff;
  font-weight: 800;
  letter-spacing: 0.08em;
  font-size: 14px;
  text-shadow: 0 8px 20px rgba(0, 0, 0, 0.45);
}
```

---

## 13. Hành vi khi click thumbnail vị trí

Phase này chỉ thiết kế tới màn Area Showcase, nên hành vi click thumbnail nên giữ đơn giản.

```text
Click thumbnail vị trí:
→ Chỉ đổi active thumbnail.
→ Counter đổi theo thumbnail active.
→ Không mở detail location.
→ Không mở gallery item grid.
→ Không mở media viewer.
```

Quan trọng:

```text
Ảnh background lớn vẫn là ảnh đại diện khu vực.
Không đổi background lớn sang ảnh vị trí.
```

Lý do:

```text
Theo yêu cầu hiện tại, ảnh phủ toàn màn hình là ảnh đại diện khu vực.
Thumbnail bên phải chỉ là danh sách ảnh đại diện của vị trí cụ thể thuộc khu vực đó.
```

---

## 14. API / DTO public cần có

Nếu public API hiện tại chưa trả ảnh đại diện area/location, cần cập nhật DTO public navigation/campus detail.

### 14.1. API đề xuất

Có thể dùng endpoint hiện tại nếu đang có:

```http
GET /api/public/visit-fptu/campuses/{campusCode}/navigation
```

Cập nhật response để có:

```json
{
  "campus": {
    "campusId": 1,
    "campusCode": "HN",
    "campusName": "FPT University Hà Nội"
  },
  "areas": [
    {
      "areaId": 10,
      "areaName": "LAB ZONE",
      "areaCoverFileId": 1001,
      "areaCoverUrl": "/api/files/1001/content",
      "displayOrder": 1,
      "locations": [
        {
          "locationId": 101,
          "locationName": "Phòng Lab",
          "locationCoverFileId": 2001,
          "locationCoverUrl": "/api/files/2001/content",
          "displayOrder": 1
        },
        {
          "locationId": 102,
          "locationName": "Phòng Studio",
          "locationCoverFileId": 2002,
          "locationCoverUrl": "/api/files/2002/content",
          "displayOrder": 2
        }
      ]
    }
  ]
}
```

### 14.2. Không cần gallery_items ở phase này

Màn này chỉ cần:

```text
- Area cover image.
- Area name.
- Location cover images.
- Location count.
```

Không cần query:

```text
- gallery_items
- gallery_item_media
- item_type
- media_kind
```

Trừ khi API navigation hiện tại đã join gallery items để lọc public. Nếu đang có logic đó, không được phá luồng cũ; chỉ bổ sung cover fields.

---

## 15. Public visibility / filtering

Area Showcase nên chỉ hiển thị dữ liệu public-safe.

Rule:

```text
campus.status = ACTIVE
area.status = ACTIVE
location.status = ACTIVE
```

Với phase này, vì không hiển thị gallery item, không bắt buộc kiểm tra item PUBLISHED cho area showcase. Tuy nhiên nếu public navigation hiện tại chỉ muốn hiển thị location có nội dung public, có thể giữ logic cũ.

Chốt đề xuất:

```text
- Sidebar area hiển thị area ACTIVE thuộc campus.
- Thumbnail location hiển thị location ACTIVE thuộc area.
- Nếu project hiện tại đang ẩn location không có gallery item public thì có thể giữ nguyên để không thay đổi public scope quá nhiều.
```

Không trả dữ liệu nội bộ:

```text
- created_by
- updated_by
- deleted_by
- external_file_id raw
- object_key
- checksum
- Google Drive raw link
```

Ảnh phải dùng file proxy:

```text
/api/files/{fileId}/content
```

---

## 16. Frontend state đề xuất

Thêm/cập nhật state cho màn Area Showcase:

```ts
type PublicGalleryViewMode =
  | 'CAMPUS_HERO'
  | 'AREA_SHOWCASE'
  | 'LOCATION_GRID'
  | 'ITEM_DETAIL';

type PublicGalleryState = {
  selectedCampusCode: string | null;

  areas: PublicGalleryArea[];
  selectedAreaId: number | null;

  viewMode: PublicGalleryViewMode;

  activeLocationThumbnailIndex: number;

  isNavigationLoading: boolean;
};
```

DTO frontend:

```ts
type PublicGalleryArea = {
  areaId: number;
  areaName: string;
  areaCoverUrl?: string | null;
  displayOrder: number;
  locations: PublicGalleryLocation[];
};

type PublicGalleryLocation = {
  locationId: number;
  locationName: string;
  locationCoverUrl?: string | null;
  displayOrder: number;
};
```

---

## 17. Frontend flow chi tiết

### 17.1. On load campus detail

```text
1. Load campus public navigation.
2. Lưu danh sách areas.
3. Hiển thị màn campus hero như hiện tại nếu user chưa chọn area.
4. Sidebar hiển thị danh sách areas.
```

### 17.2. Click area sidebar

```text
1. User click area.
2. setSelectedAreaId(areaId).
3. setViewMode('AREA_SHOWCASE').
4. setActiveLocationThumbnailIndex(0).
5. Render area cover image fullscreen.
6. Render area name bottom-left.
7. Render location thumbnail rail bên phải.
8. Render counter 01/n.
```

### 17.3. Click arrow down

```text
1. Nếu totalLocations = 0: không làm gì.
2. Nếu activeLocationThumbnailIndex < totalLocations - 1:
   index = index + 1.
3. Nếu đang ở cuối:
   index = 0.
4. Counter cập nhật.
5. Thumbnail active cập nhật.
```

### 17.4. Click arrow up

```text
1. Nếu totalLocations = 0: không làm gì.
2. Nếu activeLocationThumbnailIndex > 0:
   index = index - 1.
3. Nếu đang ở đầu:
   index = totalLocations - 1.
4. Counter cập nhật.
5. Thumbnail active cập nhật.
```

### 17.5. Click thumbnail

```text
1. setActiveLocationThumbnailIndex(index).
2. Counter cập nhật.
3. Không mở location detail.
4. Không đổi background lớn.
```

---

## 18. Empty / fallback states

### 18.1. Area không có cover image

Vì ảnh khu vực đã bắt buộc ở quản lý khu vực, case này chủ yếu là dữ liệu cũ hoặc lỗi.

Fallback đề xuất:

```text
- Dùng ảnh campus hiện tại nếu có.
- Nếu không có, dùng placeholder public gallery.
- Không crash page.
```

UI có thể hiển thị tên khu vực bình thường.

### 18.2. Area không có location

Nếu area không có location active:

```text
- Vẫn có thể hiển thị area cover image và tên khu vực.
- Cột thumbnail bên phải hiển thị empty state nhỏ:
  "Chưa có vị trí hiển thị"
```

Tuy nhiên public UX tốt hơn là không hiển thị area rỗng trên sidebar.

### 18.3. Location thiếu cover image

Nếu location thiếu ảnh:

Có 2 lựa chọn:

```text
Cách 1: Hiển thị placeholder thumbnail.
Cách 2: Ẩn location khỏi thumbnail list.
```

Đề xuất:

```text
Dùng placeholder để không làm lệch số lượng location.
```

### 18.4. Ảnh load lỗi

Nếu ảnh load lỗi:

```text
- Background dùng fallback.
- Thumbnail dùng placeholder.
- Không crash page.
```

---

## 19. Responsive behavior

### 19.1. Desktop

Desktop là trọng tâm phase này.

```text
- Sidebar bên trái giữ như hiện tại.
- Area cover fullscreen.
- Title khu vực bottom-left.
- Thumbnail rail bên phải.
```

### 19.2. Tablet/mobile

Nếu cần responsive cơ bản:

```text
- Sidebar có thể collapse hoặc giữ theo UI hiện tại.
- Thumbnail rail có thể chuyển xuống dưới thành hàng ngang.
- Counter vẫn hiển thị.
```

Nhưng phase này ưu tiên desktop giống ảnh minh họa.

---

## 20. UI style mong muốn

Style tổng thể:

```text
- Public-facing.
- Cinematic.
- Giống gallery/museum showcase.
- Không giống dashboard nội bộ.
- Ảnh lớn là trọng tâm.
- Overlay tối giúp chữ nổi.
- Thumbnail rail bên phải gọn, rõ, hiện đại.
```

Không nên:

```text
- Dùng card trắng lớn như dashboard.
- Dùng table/list nội bộ.
- Nhồi nhiều text lên ảnh.
- Mở detail quá sớm.
- Làm sidebar biến mất.
```

---

## 21. Business Rules

```text
BR-PGAL-AREA-01:
Khi user click một khu vực trên sidebar public gallery, hệ thống chuyển sang màn Area Showcase.

BR-PGAL-AREA-02:
Màn Area Showcase dùng ảnh đại diện khu vực làm background fullscreen.

BR-PGAL-AREA-03:
Ảnh background khu vực lấy từ gallery_areas.cover_file_id.

BR-PGAL-AREA-04:
Trên ảnh background phải có overlay đen mờ phủ toàn màn hình.

BR-PGAL-AREA-05:
Tên khu vực hiển thị ở góc dưới bên trái, màu trắng, in đậm.

BR-PGAL-AREA-06:
Sidebar khu vực vẫn hiển thị và khu vực đang chọn phải active.

BR-PGAL-AREA-07:
Bên phải màn hình hiển thị danh sách thumbnail dọc của các vị trí thuộc khu vực đang chọn.

BR-PGAL-AREA-08:
Thumbnail vị trí lấy từ gallery_locations.cover_file_id.

BR-PGAL-AREA-09:
Có mũi tên lên/xuống để chuyển thumbnail active.

BR-PGAL-AREA-10:
Counter hiển thị dạng current/total, ví dụ 03/12.

BR-PGAL-AREA-11:
Click thumbnail chỉ đổi active thumbnail ở phase này, chưa mở location detail.

BR-PGAL-AREA-12:
Background lớn vẫn là ảnh khu vực, không đổi sang ảnh vị trí khi click thumbnail.

BR-PGAL-AREA-13:
Phase này chưa sửa public gallery item grid/detail.

BR-PGAL-AREA-14:
Public API không được trả Google Drive raw link hoặc metadata nội bộ.
```

---

## 22. Acceptance Criteria

### AC-PGAL-AREA-01 — Click area hiển thị ảnh khu vực fullscreen

```text
Given user đang ở Public VisitFPTU Gallery của một campus
And sidebar có khu vực LAB ZONE
When user click LAB ZONE
Then hệ thống hiển thị ảnh đại diện của LAB ZONE phủ rộng toàn màn hình
And ảnh được lấy từ gallery_areas.cover_file_id.
```

---

### AC-PGAL-AREA-02 — Overlay đen mờ

```text
Given user đã click một khu vực
When ảnh khu vực hiển thị fullscreen
Then trên ảnh có lớp overlay đen mờ phủ toàn bộ màn hình
And chữ trên ảnh vẫn đọc rõ.
```

---

### AC-PGAL-AREA-03 — Tên khu vực bottom-left

```text
Given user click khu vực LAB ZONE
When màn Area Showcase hiển thị
Then tên LAB ZONE hiển thị ở phía dưới bên trái
And chữ màu trắng
And chữ in đậm
And cỡ chữ to vừa.
```

---

### AC-PGAL-AREA-04 — Sidebar vẫn hiển thị

```text
Given user đang ở màn Area Showcase
When khu vực LAB ZONE được chọn
Then sidebar khu vực vẫn hiển thị bình thường
And LAB ZONE được active
And user vẫn có thể click khu vực khác.
```

---

### AC-PGAL-AREA-05 — Thumbnail vị trí bên phải

```text
Given khu vực LAB ZONE có 12 vị trí cụ thể
And mỗi vị trí có ảnh đại diện
When user mở LAB ZONE
Then bên phải màn hình hiển thị cột thumbnail dọc
And mỗi thumbnail là ảnh đại diện của một vị trí thuộc LAB ZONE.
```

---

### AC-PGAL-AREA-06 — Counter hiện tại/tổng số

```text
Given khu vực LAB ZONE có 12 vị trí
When thumbnail thứ 3 đang active
Then counter hiển thị 03/12.
```

---

### AC-PGAL-AREA-07 — Mũi tên xuống chuyển thumbnail tiếp theo

```text
Given thumbnail thứ 3 đang active
When user click mũi tên xuống
Then thumbnail thứ 4 được active
And counter đổi thành 04/12.
```

---

### AC-PGAL-AREA-08 — Mũi tên lên chuyển thumbnail trước đó

```text
Given thumbnail thứ 3 đang active
When user click mũi tên lên
Then thumbnail thứ 2 được active
And counter đổi thành 02/12.
```

---

### AC-PGAL-AREA-09 — Arrow có loop

```text
Given thumbnail cuối cùng đang active
When user click mũi tên xuống
Then thumbnail đầu tiên được active
And counter đổi thành 01/total.

Given thumbnail đầu tiên đang active
When user click mũi tên lên
Then thumbnail cuối cùng được active.
```

---

### AC-PGAL-AREA-10 — Click thumbnail chưa mở detail

```text
Given user đang ở màn Area Showcase
When user click một thumbnail vị trí
Then thumbnail đó được active
And counter cập nhật
And hệ thống không mở location detail
And hệ thống không mở gallery item grid.
```

---

### AC-PGAL-AREA-11 — Background không đổi khi click thumbnail

```text
Given user đang xem khu vực LAB ZONE
When user click thumbnail vị trí bất kỳ
Then background lớn vẫn là ảnh đại diện của LAB ZONE
And không đổi sang ảnh vị trí.
```

---

### AC-PGAL-AREA-12 — Không crash khi thiếu ảnh

```text
Given một area hoặc location thiếu cover image do dữ liệu cũ
When user mở màn Area Showcase
Then hệ thống hiển thị fallback/placeholder phù hợp
And page không bị crash.
```

---

## 23. Checklist cho AI Agent

```text
[ ] Đọc code public VisitFPTU Gallery hiện tại trước khi sửa.
[ ] Không mock data.
[ ] Không sinh file rác.
[ ] Không sửa database trong phase này.
[ ] Kiểm tra public API đã trả areaCoverUrl/locationCoverUrl chưa.
[ ] Nếu chưa, bổ sung DTO public navigation để trả area/location cover URL.
[ ] Không trả Google Drive raw link.
[ ] Khi click area sidebar, set viewMode = AREA_SHOWCASE.
[ ] Render area cover image fullscreen.
[ ] Render overlay đen mờ phủ toàn ảnh.
[ ] Render tên area ở bottom-left bằng chữ trắng, in đậm.
[ ] Sidebar vẫn hiển thị và area active.
[ ] Render thumbnail rail bên phải.
[ ] Thumbnail dùng location cover image.
[ ] Có thumbnail active state.
[ ] Có arrow up/down.
[ ] Arrow up/down cập nhật activeLocationThumbnailIndex.
[ ] Arrow có loop đầu/cuối.
[ ] Hiển thị counter dạng 03/12.
[ ] Click thumbnail chỉ đổi active state.
[ ] Click thumbnail không mở detail/grid.
[ ] Background lớn không đổi sang ảnh vị trí khi click thumbnail.
[ ] Handle area thiếu cover image.
[ ] Handle location thiếu cover image.
[ ] Handle area không có location.
[ ] Build frontend.
[ ] Build backend nếu có sửa DTO/API.
[ ] Test click từng area.
[ ] Test area có 1 location.
[ ] Test area có nhiều location, ví dụ 12.
[ ] Test arrow up/down.
[ ] Test counter.
[ ] Test sidebar vẫn hoạt động.
```

---

## 24. Prompt ngắn có thể đưa trực tiếp cho AI Agent

```text
Cập nhật UI Public VisitFPTU Gallery màn Area Showcase.

Khi user đang ở Public Gallery campus detail và click một khu vực trên sidebar:
1. Hiển thị ảnh đại diện khu vực từ gallery_areas.cover_file_id phủ fullscreen.
2. Phủ overlay đen mờ lên ảnh.
3. Hiển thị tên khu vực ở góc dưới bên trái, chữ trắng, in đậm, cỡ to vừa.
4. Sidebar khu vực vẫn hiển thị bình thường và khu vực đang chọn active.
5. Bên phải màn hình hiển thị cột thumbnail dọc, mỗi thumbnail là ảnh đại diện của một location thuộc khu vực đó từ gallery_locations.cover_file_id.
6. Có nút mũi tên lên/xuống để chuyển thumbnail active.
7. Có counter dạng 03/12 bên dưới cột thumbnail.
8. Click thumbnail chỉ đổi active thumbnail và counter, chưa mở detail location/gallery item.
9. Background lớn luôn là ảnh khu vực, không đổi sang ảnh vị trí.
10. Chưa sửa public gallery item grid/detail trong phase này.
11. Không mock data, không sinh file rác, không dùng Google Drive raw link.
```

---

## 25. Chốt cuối cùng

Sau khi cập nhật, public VisitFPTU Gallery sẽ có thêm màn showcase khu vực:

```text
Campus detail
→ Sidebar chọn khu vực
→ Area Showcase
```

Area Showcase hiển thị:

```text
- Ảnh đại diện khu vực phủ toàn màn hình.
- Overlay đen mờ.
- Tên khu vực ở bottom-left.
- Sidebar vẫn hiển thị.
- Cột thumbnail vị trí bên phải.
- Arrow up/down.
- Counter hiện tại/tổng số.
```

Phase này dừng tại màn showcase khu vực, chưa đi tiếp vào chi tiết vị trí hoặc gallery item.
