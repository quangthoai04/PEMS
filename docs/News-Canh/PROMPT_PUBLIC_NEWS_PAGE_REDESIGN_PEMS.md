# PROMPT — REDESIGN PUBLIC NEWS PAGE / TAB TIN TỨC PEMS

Bạn là **Senior Frontend UI/UX Engineer + Full-stack Engineer** cho dự án **PEMS — Partnership Engagement Management System**.

Nhiệm vụ: thiết kế và code lại màn/tab **Tin tức public** theo phong cách **International Newsroom** chuyên nghiệp, hiện đại, responsive tốt trên desktop/tablet/mobile, có hiệu ứng chuyển động nhẹ. Mục tiêu tối thiểu: **lấy dữ liệu thật từ DB/API, tuyệt đối không dùng mock data**.

---

## 1. Trước khi sửa

Hãy search và đọc source hiện tại. Không code theo suy đoán.

Cần kiểm tra:

- Page/component tab Tin tức public hiện tại
- Header/Footer public hiện tại
- Route public news
- API service frontend đang gọi news
- Backend `NewsController`, `PublicContentController` hoặc public news endpoint liên quan
- DTO/Handler/Query liên quan tới public news list/detail
- Các bảng thật: `news`, `news_translations`, `news_content_sections`, `news_section_files`, `files`, `campuses`
- Cách project lấy file ảnh qua Google Drive/backend proxy hiện tại

Giữ nguyên:

- Header
- Footer
- Route chính nếu không bắt buộc sửa
- News detail page nếu đang hoạt động
- Logic quản lý news nội bộ/dashboard

---

## 2. Yêu cầu thiết kế UI

Thiết kế lại body tab Tin tức theo bố cục sau.

### A. Newsroom Hero

- Hiển thị 1 bài nổi bật lớn.
- Ưu tiên bài `isFeatured = true`, fallback bài published mới nhất.
- Có cover image thật, title, summary, ngày đăng, campus nếu có, nút **Đọc tiếp**.
- Nếu không có ảnh, dùng fallback visual đẹp bằng gradient nhẹ + logo/text nhỏ, không dùng placeholder thô.

### B. Smart Filter Bar

Có:

- Search keyword
- Filter loại tin:
  - Tất cả
  - Nổi bật
  - Từ chuyến thăm
  - Tin chung
- Filter campus nếu API/data có campus
- Sort: Mới nhất / Cũ nhất

UI filter phải gọn trên desktop và chuyển thành layout dễ dùng trên mobile.

### C. Editorial Top Stories

- Bố cục magazine/asymmetric:
  - 1 bài lớn bên trái
  - 2–3 bài nhỏ bên phải
- Dữ liệu thật từ published news.
- Không hardcode bài viết.

### D. Latest News Grid

- Card tin tức đẹp, ảnh 16:9, title 2 dòng, summary 2–3 dòng, date, campus badge, read more.
- Desktop: 3 columns.
- Tablet: 2 columns.
- Mobile: 1 column.
- Có pagination hoặc load more nếu source hiện tại hỗ trợ.

### E. Campus Stories

- Section **Tin tức theo cơ sở**.
- Tabs/capsules: Tất cả / Hà Nội / TP.HCM / Đà Nẵng / Cần Thơ / Quy Nhơn nếu có campus data thật.
- Khi campus không có bài, hiển thị empty state đẹp.

### F. Visit Highlights

- Section **Dấu ấn các chuyến thăm**.
- Lấy các bài có `visitInstanceId != null` hoặc field tương đương.
- Nếu backend chưa trả field này thì không bịa dữ liệu; chỉ hiển thị section khi có data thật.

---

## 3. Responsive mobile bắt buộc

Mobile phải được thiết kế riêng, không chỉ co nhỏ desktop.

Yêu cầu:

- Header/footer vẫn không vỡ.
- Hero chuyển thành 1 cột.
- Ảnh không méo, dùng aspect ratio hợp lý.
- Filter bar không tràn ngang; có thể dùng scrollable chips hoặc collapsible filter.
- Card news full width, dễ bấm.
- Font title không quá to.
- Khoảng cách section gọn hơn desktop.
- Không có horizontal scroll.
- Nút, tab, search input đủ lớn để thao tác bằng tay.

Breakpoints tham khảo:

```text
Mobile  < 768px
Tablet  768–1024px
Desktop > 1024px
```

---

## 4. Hiệu ứng chuyển động

Thêm hiệu ứng nhẹ, chuyên nghiệp:

- Section fade-up khi xuất hiện.
- Card hover nâng nhẹ trên desktop.
- Image hover zoom nhẹ `scale-105`.
- Active filter tab có underline/indicator màu cam.
- Skeleton loading có shimmer nhẹ.
- Không dùng animation lòe loẹt, xoay 3D, bounce mạnh.

Nếu project đã dùng `framer-motion` hoặc `motion/react` thì dùng lại. Không thêm thư viện mới nếu chưa cần.

---

## 5. Data/API bắt buộc

Không dùng mock data.

Public news chỉ được lấy:

- `status = PUBLISHED`
- Không hiển thị draft/hidden/internal data.
- Không lộ review note, internal note, created internal metadata nhạy cảm.

Nếu API list public hiện tại chưa đủ, tạo hoặc mở rộng endpoint:

```http
GET /api/public/news
```

Query params đề xuất:

```text
keyword
campusId
type = all | featured | visit | general
language = vi | en
page
pageSize
sort = latest | oldest
```

DTO đề xuất:

```ts
{
  id: number;
  slug: string;
  title: string;
  summary: string;
  coverUrl: string | null;
  publishedAt: string;
  campusId: number | null;
  campusName: string | null;
  campusCode: string | null;
  isFeatured: boolean;
  isVisitRelated: boolean;
  readingMinutes: number;
}
```

Nếu cần endpoint gom data cho trang newsroom:

```http
GET /api/public/news/landing
```

Trả về:

```ts
{
  featured: NewsCardDto | null;
  topStories: NewsCardDto[];
  latest: PagedResult<NewsCardDto>;
  campusGroups: {
    campusId: number | null;
    campusName: string;
    items: NewsCardDto[];
  }[];
  visitHighlights: NewsCardDto[];
}
```

Backend phải theo Clean Architecture:

```text
Controller → MediatR Query → Handler → DTO → DbContext/Repository
```

---

## 6. UI style

Dùng style FPTU professional:

```text
Primary blue: #004c91
Orange accent: #F37021
Text chính: slate-800/slate-900
Text phụ: slate-500/slate-600
Border: slate-200
Background: white/slate-50
```

Không dùng quá nhiều card lồng card.
Không dùng shadow quá đậm.
Không dùng màu quá nhiều.
Ưu tiên khoảng trắng, ảnh thật, typography rõ ràng.

---

## 7. Empty/loading/error state

Bắt buộc có:

- Loading skeleton cho hero + cards.
- Empty state khi không có tin.
- Empty state khi filter/search không có kết quả.
- Error state có nút thử lại.
- Ảnh lỗi phải fallback an toàn, không vỡ layout.

---

## 8. Không được làm

- Không sửa dashboard/news management nội bộ nếu không cần.
- Không hardcode tin tức, logo, ảnh, campus.
- Không dùng mock data.
- Không thêm table/field/enum nếu SQL chưa có.
- Không đổi business workflow news approval/publish.
- Không làm lộ bài chưa published.
- Không làm build TypeScript/C# lỗi.

---

## 9. Test bắt buộc

Sau khi sửa:

- Chạy frontend typecheck/build/lint theo script hiện có.
- Chạy backend build nếu có sửa backend.
- Test các case:
  - Desktop hiển thị đẹp.
  - Tablet hiển thị đẹp.
  - Mobile không tràn ngang.
  - Không có news → empty state.
  - News không có ảnh → fallback đẹp.
  - Search có kết quả.
  - Search không có kết quả.
  - Filter featured.
  - Filter visit-related.
  - Filter campus.
  - Click **Đọc tiếp** mở đúng trang chi tiết.
  - Public không thấy draft/hidden/internal data.

---

## 10. Báo cáo sau khi hoàn thành

Trả report gồm:

1. File đã sửa/tạo
2. API đã dùng/tạo
3. Data source thật đã dùng
4. Bố cục UI mới
5. Responsive mobile đã xử lý thế nào
6. Các case đã test
7. Build/test result
8. Phần chưa làm được hoặc cần xác nhận
