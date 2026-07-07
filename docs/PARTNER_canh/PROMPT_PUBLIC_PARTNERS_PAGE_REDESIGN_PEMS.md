# PROMPT — REDESIGN PUBLIC PARTNERS PAGE / TAB ĐỐI TÁC PEMS

Bạn là **Senior Frontend UI/UX Engineer + Full-stack Engineer** cho dự án **PEMS - Partnership Engagement Management System** của FPT University.

Nhiệm vụ: thiết kế và code lại màn/tab **Đối tác public dành cho Visitor/khách quốc tế** theo phong cách chuyên nghiệp, hiện đại, responsive tốt trên desktop/tablet/mobile, có hiệu ứng chuyển động nhẹ. Mục tiêu tối thiểu: **lấy dữ liệu thật từ DB/API, tuyệt đối không dùng mock data**.

---

## 1. Trước khi sửa

Hãy search và đọc source hiện tại. **Không code theo suy đoán.**

Cần kiểm tra:

- Page/component tab Đối tác public hiện tại
- Route public partners/list/detail hiện tại
- Header/Footer public hiện tại
- Frontend API service đang gọi partners
- Backend `PartnersController`, `PublicPartnersController` hoặc public partner endpoint liên quan
- DTO/Query/Handler/Validator liên quan tới public partner list/detail
- Entity/EF config/DbContext liên quan tới `partners`, `partner_contacts`, `files`, `campuses`
- SQL fresh-create mới nhất và dictionary v10/new-final
- Cách project lấy ảnh/logo qua Google Drive/backend file proxy hiện tại

Giữ nguyên:

- Header hiện tại
- Footer hiện tại
- Public navigation hiện tại nếu không bắt buộc sửa
- Dashboard/internal Partner Management nếu không liên quan trực tiếp
- Logic duyệt partner nội bộ
- Business workflow partner approval

---

## 2. Mục tiêu thiết kế

Thiết kế lại tab Đối tác theo concept:

```text
FPTU Partnership Directory
```

Đây là trang public cho khách quốc tế xem mạng lưới đối tác của FPT University, không phải dashboard và không phải trang quản trị.

Yêu cầu quan trọng:

- **Không dùng quả cầu/globe/map lớn** vì homepage đã có visual này.
- Không hardcode logo Amazon/Bosch/Vingroup hoặc bất kỳ dữ liệu giả nào.
- Không dùng mock data.
- Màu sắc giữ theo tông chủ đạo FPTU: navy + orange + nền sáng.
- Thiết kế chuyên nghiệp, sạch, có khoảng trắng, phù hợp đối tác/khách hàng quốc tế.

---

## 3. Partners List Page — Bố cục cần làm

### A. Compact Hero không globe

Hero mới phải gọn, cao khoảng `420–520px` trên desktop, không chiếm quá nhiều màn hình.

Nội dung đề xuất:

```text
Badge: FPT UNIVERSITY PARTNERSHIP NETWORK
Title: Đối tác & Hợp tác quốc tế
Subtitle: Kết nối học thuật, doanh nghiệp và tổ chức toàn cầu trong hệ sinh thái FPT University.
CTA chính: Đăng ký ghé thăm
CTA phụ: Khám phá Visit FPTU
```

Visual bên phải:

- Không dùng globe/map.
- Hiển thị 3–5 logo/initial partner thật từ DB dưới dạng floating cards hoặc partner showcase.
- Nếu partner không có logo, fallback bằng initials từ tên đối tác, ví dụ `SS`, `AN`, `SC`.
- Fallback phải lấy từ data thật, không bịa tên/ảnh.

### B. Trust Metrics từ DB thật

Hiển thị 2–4 metric nhỏ, lấy từ DB/API thật:

```text
Tổng đối tác công khai
Số quốc gia
Số loại đối tác nếu DB/API có partnerType
Số campus sở hữu hồ sơ nếu phù hợp và không lộ thông tin nhạy cảm
```

Không hardcode `500+`, `40+` nếu DB không có dữ liệu tương ứng.

### C. Professional Filter Bar

Thanh filter cần đẹp, gọn, không chiếm quá nhiều chiều cao.

Desktop:

```text
Search partner name, country, description...
Country dropdown
Partner type dropdown nếu API/data có
Sort: A-Z / Newest / Country
```

Mobile:

- Search full width.
- Country/type dùng scrollable chips hoặc nút “Bộ lọc” mở panel/bottom sheet nếu cần.
- Không có horizontal scroll toàn trang.
- Button/input đủ lớn để thao tác bằng tay.

Filter logic:

```text
keyword: name, shortName, partnerCode, description
country
partnerType nếu có
page/pageSize
sort
```

### D. Country / Region Chips

Thêm section nhỏ hoặc row chips:

```text
Explore by country
Korea (5) · Australia (3) · United States (2) · Singapore (2) ...
```

Dữ liệu lấy từ distinct country trong DB. Click chip thì filter danh sách.

### E. Partner Grid

Không thiết kế card chỉ có logo + tên quá rỗng. Card cần có đủ thông tin ngắn:

```text
Logo hoặc initials
Partner name
Country · City
Partner type nếu có
Description 2 dòng nếu có
View profile →
```

Responsive:

- Desktop: 3 columns nếu card có mô tả; 4 columns chỉ khi card rất gọn.
- Tablet: 2 columns.
- Mobile: 1 column.

Card hover desktop:

- Border chuyển xanh nhạt.
- Logo/image zoom nhẹ.
- Card nâng nhẹ `translate-y-1`.
- Shadow nhẹ, không dùng shadow đậm.

### F. Pagination / Load More

Nếu API hiện tại có pagination, giữ pagination. Nếu không, thêm pagination chuẩn.

Yêu cầu:

- Có loading state.
- Có empty state khi không có partner.
- Có empty state khi search/filter không có kết quả.
- Có error state và nút thử lại.

### G. Final CTA

Cuối trang trước footer có CTA gọn:

```text
Bạn muốn kết nối với FPT University?
Cùng chúng tôi mở rộng cơ hội hợp tác học thuật, trao đổi sinh viên và tiếp cận hệ sinh thái giáo dục quốc tế.

[Đăng ký ghé thăm] [Liên hệ Phòng HTQT]
```

Nền gradient rất nhẹ xanh nhạt → cam nhạt. Không dùng gradient mạnh.

---

## 4. Partner Detail Page — Bố cục cần làm

Thiết kế lại trang chi tiết theo layout 2 cột chuyên nghiệp.

### Desktop layout

```text
Breadcrumb:
Trang chủ / Đối tác / [Partner Name]

[← Quay lại danh sách đối tác]

┌────────────────────────────┬─────────────────────────────────────┐
│ LEFT PROFILE PANEL          │ RIGHT DETAIL PANEL                   │
│                             │                                      │
│ Cover image / logo          │ Giới thiệu chung                     │
│ Partner logo card           │ description                          │
│ Partner name                │                                      │
│ Short name / code           │ Thông tin tổ chức                    │
│ Country · City              │ - Quốc gia                           │
│ Partner type                │ - Thành phố                          │
│                             │ - Địa chỉ / trụ sở                   │
│                             │                                      │
│                             │ Website chính thức                   │
│                             │ [Ghé thăm website đối tác ↗]         │
└────────────────────────────┴─────────────────────────────────────┘
```

Yêu cầu cụ thể:

- Bên trái là ảnh/logo + tên đối tác + thông tin định danh chính.
- Bên phải là mô tả chi tiết, vị trí, link website.
- Left profile panel có thể sticky trên desktop.
- Không để ảnh xám/placeholder khổng lồ chiếm gần cả màn.
- Nếu không có cover/logo, fallback bằng initials đẹp.
- Website button chỉ hiển thị nếu có `websiteUrl` thật.

### Mobile layout

Mobile chuyển thành 1 cột:

```text
Breadcrumb rút gọn
Back button
Profile card
About card
Location card
Website button full width
```

Yêu cầu mobile:

- Không tràn ngang.
- Ảnh không méo, dùng aspect ratio hợp lý.
- Button cao khoảng 44–48px, dễ bấm.
- Font title không quá to.
- Spacing gọn hơn desktop.

---

## 5. Data/API bắt buộc

Không dùng mock data.

Public partners chỉ được lấy dữ liệu hợp lệ để hiển thị công khai:

```text
profile_status = APPROVED
status/cooperation_status = ACTIVE nếu DB/API có field tương ứng
visibility/public flag nếu DB/API có field tương ứng
```

Không hiển thị public:

```text
DRAFT
PENDING_APPROVAL
REJECTED
INACTIVE
review_note
reviewed_by
created_by
updated_by
internal owner/campus metadata nhạy cảm
contact person email/phone nếu chưa xác định là public
```

Nếu API public partners hiện tại còn mock/stub hoặc thiếu dữ liệu cần thiết, hãy hoàn thiện backend theo Clean Architecture.

Endpoint đề xuất:

```http
GET /api/public/partners
```

Query params đề xuất:

```text
keyword
country
partnerType
page
pageSize
sort
```

Response list đề xuất:

```ts
{
  items: PublicPartnerCardDto[];
  totalItems: number;
  page: number;
  pageSize: number;
  countries: { name: string; count: number }[];
  partnerTypes: { code: string; name: string; count: number }[];
  stats: {
    totalPartners: number;
    totalCountries: number;
  };
}
```

Detail endpoint đề xuất:

```http
GET /api/public/partners/{slugOrId}
```

DTO card đề xuất:

```ts
{
  id: number;
  slug?: string;
  partnerCode?: string;
  name: string;
  shortName?: string;
  country?: string;
  city?: string;
  partnerType?: string;
  description?: string;
  logoUrl?: string | null;
  coverUrl?: string | null;
  websiteUrl?: string | null;
}
```

DTO detail đề xuất:

```ts
{
  id: number;
  slug?: string;
  partnerCode?: string;
  name: string;
  shortName?: string;
  description?: string;
  country?: string;
  city?: string;
  address?: string;
  websiteUrl?: string | null;
  partnerType?: string;
  logoUrl?: string | null;
  coverUrl?: string | null;
}
```

Ảnh/logo:

- Lấy qua `files` + backend proxy/file service hiện có.
- Không để frontend gọi trực tiếp Google Drive URL nếu project đã chuẩn hóa backend proxy.
- Nếu thiếu ảnh, fallback bằng initials từ partner name.

---

## 6. Backend rule nếu cần sửa API

Nếu phải tạo/mở rộng API, tuân thủ Clean Architecture:

```text
Controller → MediatR Query → Handler → DTO → DbContext/Repository
```

Yêu cầu:

- Controller chỉ nhận request, gọi Mediator, trả response.
- Handler xử lý query/filter/sort/pagination.
- Không trả field nội bộ ra public DTO.
- Không dùng dynamic permissions.
- Không dùng `permissions`, `role_permissions`, `permission_code`, `permission_level`.
- Không tạo field/table/enum mới nếu SQL chưa có.
- Không tự ý thêm schema nếu chưa được yêu cầu.

---

## 7. UI style

Dùng style FPTU professional / international portal:

```text
Primary blue: #004c91
Orange accent: #F37021
Text chính: slate-800/slate-900
Text phụ: slate-500/slate-600
Border: slate-200
Background: white/slate-50
```

Nguyên tắc:

- Sạch, sáng, chuyên nghiệp.
- Nhiều khoảng trắng.
- Không dùng quá nhiều khung lớn.
- Không dùng shadow quá đậm.
- Không dùng gradient mạnh.
- Không dùng quá nhiều màu trong cùng một section.
- Không dùng globe/map ở tab Đối tác.

---

## 8. Animation / interaction

Thêm hiệu ứng nhẹ, chuyên nghiệp:

- Hero text fade-up.
- Partner cards stagger fade-up khi xuất hiện.
- Card hover nâng nhẹ trên desktop.
- Logo/image hover zoom nhẹ `scale-105`.
- Filter chip active transition màu orange.
- Detail page left panel fade-left, right cards fade-up.
- Skeleton loading shimmer nhẹ.

Không dùng:

- Animation xoay 3D.
- Bounce mạnh.
- Globe/map động.
- Hiệu ứng lòe loẹt.
- Animation làm chậm thao tác hoặc gây thiếu nghiêm túc.

Nếu project đã dùng `framer-motion` hoặc `motion/react`, dùng lại. Không thêm thư viện mới nếu không cần.

---

## 9. Responsive bắt buộc

Mobile phải được xử lý riêng, không chỉ co nhỏ desktop.

Yêu cầu:

- Không có horizontal scroll.
- Hero 1 cột.
- Filter gọn, không tràn.
- Partner card full width.
- Detail page 1 cột.
- Nút website/CTA dễ bấm.
- Hình ảnh giữ aspect ratio, không méo.
- Header/footer không vỡ.

Breakpoints tham khảo:

```text
Mobile < 768px
Tablet 768–1024px
Desktop > 1024px
```

---

## 10. Loading / Empty / Error states

Bắt buộc có:

- Loading skeleton cho hero/showcase + grid.
- Empty state khi không có partner public.
- Empty state khi filter/search không có kết quả.
- Error state có nút thử lại.
- Image error fallback an toàn.
- Detail not found state.

---

## 11. Không được làm

- Không dùng mock data.
- Không hardcode logo/tên đối tác/quốc gia.
- Không dùng quả cầu/globe/map trong tab Đối tác.
- Không sửa dashboard/internal Partner Management nếu không cần.
- Không đổi business workflow approve/reject partner.
- Không làm lộ partner chưa approved/public.
- Không lộ review note/internal audit fields/contact private data.
- Không thêm table/field/enum nếu SQL chưa có.
- Không thêm thư viện mới nếu không cần.
- Không làm build TypeScript/C# lỗi.

---

## 12. Test bắt buộc

Sau khi sửa:

- Chạy frontend typecheck/build/lint theo script hiện có.
- Chạy backend build nếu có sửa backend.
- Test manual:
  - Desktop list page đẹp, không có globe.
  - Tablet layout không vỡ.
  - Mobile không tràn ngang.
  - Partner có logo hiển thị đúng.
  - Partner không có logo fallback initials đẹp.
  - Search keyword có kết quả.
  - Search không có kết quả hiển thị empty state.
  - Filter country hoạt động.
  - Sort hoạt động nếu có.
  - Pagination/load more hoạt động nếu có.
  - Click card mở đúng detail.
  - Detail hiển thị đúng layout: trái ảnh + tên, phải mô tả + vị trí + website.
  - Website button chỉ hiện khi có URL thật.
  - Public không thấy DRAFT/PENDING/REJECTED/INACTIVE/internal fields.

---

## 13. Báo cáo sau khi hoàn thành

Trả report gồm:

1. File đã sửa/tạo
2. API đã dùng/tạo
3. Data source thật đã dùng
4. Bố cục UI mới của list page
5. Bố cục UI mới của detail page
6. Responsive mobile đã xử lý thế nào
7. Animation/interaction đã thêm
8. Các case đã test
9. Build/test result
10. Phần chưa làm được hoặc cần xác nhận

---

## 14. Definition of Done

Task chỉ được coi là hoàn thành khi:

- Tab Đối tác public dùng data thật từ DB/API.
- Không còn mock data/hardcode logo.
- Không còn globe/map ở tab Đối tác.
- List page chuyên nghiệp, có search/filter/empty/loading/error state.
- Detail page đúng yêu cầu: trái ảnh + tên, phải mô tả + vị trí + website.
- Desktop/tablet/mobile đều ổn.
- Header/footer giữ nguyên.
- Không phá dashboard/internal partner management.
- Build frontend pass.
- Backend build pass nếu có sửa backend.
