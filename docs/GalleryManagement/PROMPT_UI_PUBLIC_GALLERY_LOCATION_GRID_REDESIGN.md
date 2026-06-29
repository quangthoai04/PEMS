# PROMPT — Cập nhật UI Public VisitFPTU Gallery Location Grid

## 1. Bối cảnh hiện tại

Hiện tại public VisitFPTU Gallery đã hỗ trợ mô hình:

```text
1 location có nhiều gallery item.
1 gallery item có title, description, media_kind, status và nhiều media.
Mỗi gallery item có media chính thông qua gallery_item_media.is_primary = 1.
```

Khi người dùng click vào một vị trí cụ thể trong public Gallery, hệ thống đã hiển thị toàn bộ gallery item của vị trí đó dưới dạng lưới media. Tuy nhiên UI hiện tại nhìn chưa đẹp vì:

```text
- Grid bị nhồi trong khung quá nặng.
- Nền blur/viền dày làm UI giống dashboard nội bộ hơn là public gallery.
- Card media bị cắt ở hàng dưới, gây cảm giác lỗi layout.
- Media card chưa có phân cấp thị giác rõ.
- Badge “Hình ảnh” hơi thô.
- Sidebar area còn giống menu admin.
- Header location chưa tạo cảm giác premium/public-facing.
```

Mục tiêu của prompt này là yêu cầu AI Agent chỉnh lại UI cho đẹp hơn, thoáng hơn, đúng tinh thần **Public Campus Gallery / Museum Gallery**, nhưng **không đổi nghiệp vụ và không đổi API nếu không cần thiết**.

---

## 2. Mục tiêu UI mới

Chuyển UI từ kiểu:

```text
Dark panel + grid bị nhét trong khung + card bị cắt
```

sang kiểu:

```text
Premium Campus Gallery / Museum Gallery
```

Tức là:

```text
Header public trắng
Background campus mờ tối nhẹ
Left sidebar khu vực gọn hơn
Main content rộng và thoáng
Location header sang hơn
Grid media 5 card / dòng trên desktop
Card media có hover đẹp
Click card mở detail gallery item
```

Không biến trang public thành dashboard nội bộ. Đây là trang giới thiệu campus, nên UI cần tạo cảm giác khám phá, hình ảnh, hiện đại, mượt.

---

## 3. Phạm vi chỉnh sửa

Cần chỉnh:

```text
- Layout tổng thể public gallery page.
- Location header.
- Grid gallery item theo location.
- Media card.
- Hover effect media card.
- Badge loại media.
- Sidebar area.
- Location chips nếu có thể thêm.
- Detail transition và nút quay lại grid nếu đang thiếu.
```

Không chỉnh:

```text
- Không đổi database.
- Không đổi nghiệp vụ quản lý gallery.
- Không đổi nghiệp vụ quản lý khu vực.
- Không mock data.
- Không thay API nếu dữ liệu hiện tại đã đủ.
- Không sinh file rác.
```

---

## 4. Layout tổng thể đề xuất

Bố cục desktop nên là:

```text
┌───────────────────────────────────────────────┐
│ Header public                                 │
├──────────────┬────────────────────────────────┤
│ Sidebar area │ Main content                   │
│ 240px        │ Location gallery grid          │
└──────────────┴────────────────────────────────┘
```

Kích thước đề xuất:

```css
.visit-gallery-page {
  min-height: calc(100vh - 80px);
  padding: 48px 56px;
}

.visit-gallery-sidebar {
  width: 240px;
  flex-shrink: 0;
}

.visit-gallery-main {
  width: 100%;
  max-width: 1280px;
}
```

Với màn desktop lớn, main content phải đủ rộng để 5 card/dòng trông thoáng, không bị bó.

---

## 5. Location header cần làm lại

Hiện tại header đang hiển thị dạng:

```text
TÒA ALPHA > TRƯỚC TÒA
Danh sách hình ảnh / video
11 nội dung
```

Phần này nên đổi sang header gọn và sang hơn:

```text
[TÒA ALPHA > TRƯỚC TÒA]

Trước tòa Alpha
Khám phá hình ảnh và video nổi bật tại khu vực này.

11 nội dung · 8 hình ảnh · 3 video
```

Trong đó:

```text
- Breadcrumb area > location là pill nhỏ màu cam.
- Tên location là title chính.
- Subtitle ngắn tạo cảm giác public-facing.
- Count tổng nội dung / ảnh / video nếu API có.
```

Nếu API chưa có count image/video thì hiển thị tối thiểu:

```text
11 nội dung
```

CSS concept:

```css
.location-header {
  padding: 28px 32px;
  border-radius: 28px;
  background: linear-gradient(
    135deg,
    rgba(255,255,255,0.12),
    rgba(255,255,255,0.04)
  );
  border: 1px solid rgba(255,255,255,0.14);
  backdrop-filter: blur(18px);
}

.location-breadcrumb-pill {
  display: inline-flex;
  align-items: center;
  gap: 8px;
  padding: 8px 16px;
  border-radius: 999px;
  background: linear-gradient(90deg, #f37021, #f59e0b);
  color: #fff;
  font-weight: 800;
  letter-spacing: 0.08em;
  text-transform: uppercase;
}

.location-title {
  margin-top: 18px;
  font-size: 40px;
  line-height: 1.1;
  font-weight: 900;
  color: #fff;
}

.location-subtitle {
  margin-top: 10px;
  color: rgba(255,255,255,0.72);
  font-size: 16px;
}

.location-stats {
  margin-top: 14px;
  color: rgba(255,255,255,0.8);
  font-weight: 600;
}
```

Không nên dùng “Danh sách hình ảnh / video” làm title chính. Tên location nên là title chính để UI có ngữ cảnh rõ hơn.

---

## 6. Grid media

### 6.1 Số cột

Desktop cần giữ đúng yêu cầu:

```text
1 dòng = 5 media card
```

CSS:

```css
.location-gallery-grid {
  display: grid;
  grid-template-columns: repeat(5, minmax(0, 1fr));
  gap: 20px;
}
```

Responsive đề xuất:

```text
>= 1440px: 5 columns
1200px - 1439px: 4 columns
768px - 1199px: 3 columns
< 768px: 1-2 columns
```

CSS:

```css
@media (max-width: 1439px) {
  .location-gallery-grid {
    grid-template-columns: repeat(4, minmax(0, 1fr));
  }
}

@media (max-width: 1199px) {
  .location-gallery-grid {
    grid-template-columns: repeat(3, minmax(0, 1fr));
  }
}

@media (max-width: 767px) {
  .location-gallery-grid {
    grid-template-columns: repeat(2, minmax(0, 1fr));
  }
}

@media (max-width: 520px) {
  .location-gallery-grid {
    grid-template-columns: 1fr;
  }
}
```

### 6.2 Không để card bị cắt

Hiện tại hàng thứ hai đang bị cắt ở đáy. Phải sửa.

Không dùng container có height cứng gây cắt card.

Ưu tiên:

```css
.gallery-grid-panel {
  max-height: none;
  overflow: visible;
}
```

Khuyến nghị dùng **page scroll toàn trang**, không dùng scroll riêng trong panel.

Nếu bắt buộc panel scroll riêng:

```css
.gallery-grid-panel {
  max-height: calc(100vh - 260px);
  overflow-y: auto;
  padding-right: 8px;
}
```

Nhưng phải có scrollbar rõ và không cắt ngang card.

---

## 7. Media card design

Mỗi media card đại diện cho một gallery item.

Card phải lấy media chính:

```text
gallery_item_media.is_primary = 1
```

Nếu dữ liệu lỗi không có primary media:

```text
Fallback lấy media ACTIVE đầu tiên theo display_order ASC, media_id ASC.
```

### 7.1 Aspect ratio

Dùng aspect ratio thống nhất:

```css
aspect-ratio: 16 / 10;
```

Lý do:

```text
- Trông cinematic hơn 1:1.
- Hợp với ảnh/video campus.
- 5 card/dòng vẫn đủ thoáng.
```

### 7.2 Card CSS concept

```css
.gallery-card {
  position: relative;
  aspect-ratio: 16 / 10;
  border-radius: 20px;
  overflow: hidden;
  cursor: pointer;
  background: rgba(255,255,255,0.08);
  box-shadow: 0 18px 45px rgba(0,0,0,0.28);
  transition: transform .22s ease, box-shadow .22s ease, border-color .22s ease;
}

.gallery-card:hover {
  transform: translateY(-6px) scale(1.015);
  box-shadow: 0 28px 70px rgba(0,0,0,0.42);
}

.gallery-card img,
.gallery-card video {
  width: 100%;
  height: 100%;
  object-fit: cover;
  transition: transform .35s ease;
}

.gallery-card:hover img,
.gallery-card:hover video {
  transform: scale(1.08);
}

.gallery-card::after {
  content: "";
  position: absolute;
  inset: 0;
  background: linear-gradient(
    to top,
    rgba(0,0,0,0.72),
    rgba(0,0,0,0.16),
    transparent
  );
  pointer-events: none;
}
```

---

## 8. Nội dung mỗi card

Mỗi card nên có:

```text
- Media chính của gallery item.
- Badge loại media ở góc trái.
- Title ở đáy card.
- Overlay hover “Xem chi tiết”.
- Video có icon play.
```

Ví dụ card:

```text
┌────────────────────┐
│ [📷 Hình ảnh]      │
│                    │
│                    │
│ Cổng chính Alpha   │
└────────────────────┘
```

Với video:

```text
┌────────────────────┐
│ [▶ Video]          │
│        ▶           │
│                    │
│ Clip khuôn viên    │
└────────────────────┘
```

---

## 9. Badge loại media

Badge hiện tại hơi thô. Nên dùng pill nhỏ hơn.

Label đề xuất:

```text
📷 Hình ảnh
▶ Video
▦ Hỗn hợp
```

CSS:

```css
.media-badge {
  position: absolute;
  top: 12px;
  left: 12px;
  z-index: 2;
  padding: 6px 10px;
  border-radius: 999px;
  font-size: 12px;
  font-weight: 700;
  color: white;
  background: rgba(15, 23, 42, 0.62);
  backdrop-filter: blur(10px);
  border: 1px solid rgba(255,255,255,0.18);
}
```

---

## 10. Hover effect

Hover phải tạo cảm giác “có thể click”.

Khi hover:

```text
- Ảnh zoom nhẹ.
- Card nâng nhẹ.
- Overlay tối hơn.
- Hiện text “Xem chi tiết”.
- Có icon mũi tên hoặc eye.
```

CSS concept:

```css
.gallery-card-hover-cta {
  position: absolute;
  inset: 0;
  z-index: 3;
  display: flex;
  align-items: center;
  justify-content: center;
  opacity: 0;
  background: rgba(0,0,0,0.18);
  transition: opacity .22s ease;
}

.gallery-card:hover .gallery-card-hover-cta {
  opacity: 1;
}

.gallery-card-hover-cta span {
  padding: 9px 14px;
  border-radius: 999px;
  color: white;
  font-weight: 800;
  background: rgba(243,112,33,0.92);
  box-shadow: 0 12px 28px rgba(243,112,33,.28);
}
```

Hover không được tự mở detail. Chỉ click mới mở.

---

## 11. Sidebar area

Sidebar hiện tại nhìn hơi giống admin menu. Cần làm mềm hơn.

Nên hiển thị area item kèm số nội dung public nếu API có:

```text
TÒA ALPHA        11
LAB ZONE         4
COVERAGE ZONE    8
```

Active item:

```css
.area-item.active {
  background: linear-gradient(90deg, #f37021, #f59e0b);
  box-shadow: 0 10px 28px rgba(243,112,33,.35);
  color: #fff;
}
```

Normal item:

```css
.area-item {
  background: rgba(255,255,255,0.06);
  border-bottom: 1px solid rgba(255,255,255,0.08);
  color: rgba(255,255,255,0.86);
}
```

Không làm sidebar quá đậm hoặc quá nhiều viền.

---

## 12. Location chips

Nên thêm hàng location chips dưới header để chuyển nhanh giữa các location cùng area.

Ví dụ:

```text
[Trước tòa] [Sảnh chính] [Thư viện] [Phòng học điển hình]
```

Active chip màu cam.

Lợi ích:

```text
- Người dùng không cần hover lại sidebar liên tục.
- Người dùng biết area hiện tại có những location nào.
- UI giống trang explore/gallery hơn.
```

CSS concept:

```css
.location-chip-row {
  display: flex;
  flex-wrap: wrap;
  gap: 10px;
  margin: 18px 0 24px;
}

.location-chip {
  padding: 9px 14px;
  border-radius: 999px;
  color: rgba(255,255,255,.78);
  background: rgba(255,255,255,.08);
  border: 1px solid rgba(255,255,255,.12);
  transition: all .18s ease;
}

.location-chip.active,
.location-chip:hover {
  color: #fff;
  background: linear-gradient(90deg, #f37021, #f59e0b);
  box-shadow: 0 10px 24px rgba(243,112,33,.26);
}
```

Nếu chưa muốn thêm chips ở phase này thì có thể bỏ, nhưng khuyến nghị nên thêm vì cải thiện UX rõ rệt.

---

## 13. Detail gallery item

Khi click card, mở detail gallery item như hiện tại nhưng cần có nút quay lại grid:

```text
← Quay lại Trước tòa
```

Hoặc:

```text
← Quay lại danh sách
```

Detail vẫn hiển thị:

```text
- Breadcrumb: AREA > LOCATION
- Title
- Description
- Toàn bộ media của gallery item
- Media viewer lớn
- Thumbnail/dots của media trong item
- Phóng to media
```

Nên thêm transition:

```text
Grid card click → detail panel fade/slide in.
Back → quay lại grid, giữ selected campus/area/location.
```

---

## 14. Thiết kế tổng thể mong muốn

Khi chọn location, màn hình nên giống:

```text
[Sidebar areas]

Main:
┌─────────────────────────────────────────────────────────────┐
│ TÒA ALPHA > TRƯỚC TÒA                                       │
│ Trước tòa Alpha                                             │
│ Khám phá những hình ảnh và video nổi bật tại khu vực này.   │
│ 11 nội dung · 8 hình ảnh · 3 video                          │
└─────────────────────────────────────────────────────────────┘

[Trước tòa] [Sảnh chính] [Thư viện] [Phòng học]

┌──────────┐ ┌──────────┐ ┌──────────┐ ┌──────────┐ ┌──────────┐
│ Card 1   │ │ Card 2   │ │ Card 3   │ │ Card 4   │ │ Card 5   │
└──────────┘ └──────────┘ └──────────┘ └──────────┘ └──────────┘
┌──────────┐ ┌──────────┐ ┌──────────┐ ┌──────────┐ ┌──────────┐
│ Card 6   │ │ Card 7   │ │ Card 8   │ │ Card 9   │ │ Card 10  │
└──────────┘ └──────────┘ └──────────┘ └──────────┘ └──────────┘
```

---

## 15. Empty state

Nếu location không có gallery item public:

```text
Vị trí này hiện chưa có nội dung Gallery công khai.
```

Tuy nhiên public navigation nên ưu tiên không hiển thị location rỗng.

Nếu do dữ liệu vừa thay đổi trong khi user đang xem:

```text
Nội dung này hiện không còn được hiển thị.
```

Sau đó có thể chuyển về location đầu tiên còn nội dung public.

---

## 16. Checklist cho AI Agent

```text
[ ] Cập nhật UI Public VisitFPTU Gallery phần Location Grid.
[ ] Không đổi nghiệp vụ backend nếu không cần.
[ ] Không mock data.
[ ] Không sinh file rác.
[ ] Bỏ khung grid quá nặng nếu đang làm UI bí.
[ ] Main content rộng hơn, max-width khoảng 1280px.
[ ] Location header hiển thị breadcrumb area > location.
[ ] Location header dùng tên location làm title chính.
[ ] Location header có subtitle ngắn.
[ ] Location header có count tổng nội dung / ảnh / video nếu API có.
[ ] Thêm location chips dưới header nếu khả thi.
[ ] Grid desktop 5 columns.
[ ] Grid gap khoảng 20px.
[ ] Card aspect-ratio 16/10.
[ ] Card border-radius khoảng 20px.
[ ] Card overflow hidden.
[ ] Card dùng primary media của gallery item.
[ ] Video card có icon play hoặc thumbnail.
[ ] Badge media nhỏ ở góc trái.
[ ] Title nằm ở overlay đáy card.
[ ] Hover card scale nhẹ.
[ ] Hover ảnh zoom nhẹ.
[ ] Hover hiện overlay “Xem chi tiết”.
[ ] Click card mở detail gallery item.
[ ] Detail có nút quay lại grid.
[ ] Không để grid panel cắt card ở hàng dưới.
[ ] Ưu tiên page scroll toàn trang thay vì scroll panel bị cắt.
[ ] Test desktop 5 cards/dòng.
[ ] Test 1, 2, 5, 6, 11 item.
[ ] Test video item.
[ ] Test empty state.
[ ] Build frontend.
```

---

## 17. Prompt ngắn có thể đưa trực tiếp cho AI Agent

```text
Cập nhật UI Public VisitFPTU Gallery phần Location Grid.

Mục tiêu:
- Khi click vào location, hiển thị toàn bộ gallery item của location dưới dạng grid.
- Giữ logic 1 row desktop = 5 media card.
- Thiết kế lại grid cho đẹp, thoáng, public-facing, không giống dashboard nội bộ.

Yêu cầu UI:
1. Bỏ khung grid quá nặng và tránh cắt card ở hàng dưới.
2. Main content rộng hơn, max-width khoảng 1280px.
3. Location header hiển thị:
   - breadcrumb area > location
   - tên location làm title chính
   - subtitle ngắn
   - count tổng nội dung / ảnh / video nếu API có.
4. Thêm location chips dưới header để chuyển nhanh các location cùng area nếu khả thi.
5. Grid dùng CSS:
   - desktop 5 columns
   - gap 20px
   - card aspect-ratio 16/10
   - border-radius 20px
   - overflow hidden
6. Card media:
   - dùng primary media của gallery item
   - overlay gradient ở đáy
   - title ở đáy
   - badge loại media ở góc trái
   - video có icon play
7. Hover card:
   - scale nhẹ
   - ảnh zoom nhẹ
   - overlay hiện “Xem chi tiết”
   - cursor pointer
8. Click card mở detail gallery item hiện tại.
9. Detail có nút quay lại grid.
10. Không thay đổi API business logic nếu không cần.
11. Không làm mock data.
12. Code clean, không sinh file rác.
```

---

## 18. Chốt cuối cùng

Hướng UI nên chốt:

```text
Location Grid = album gallery.
Gallery Item Detail = bài đăng chi tiết.
```

Cần sửa mạnh nhất ở 3 phần:

```text
1. Header location gọn, sang và có ngữ cảnh hơn.
2. Grid card rộng, đều, không bị cắt.
3. Hover card có overlay “Xem chi tiết”.
```

Nếu có thể làm thêm, nên thêm **location chips** dưới header vì cải thiện UX chuyển vị trí rất rõ.
