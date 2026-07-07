# PROMPT — REDESIGN PUBLIC FAQ PAGE PEMS

Bạn là Senior Frontend UI/UX Engineer cho dự án PEMS.

Nhiệm vụ: thiết kế và code lại màn **FAQ public** theo hướng **Help Center chuyên nghiệp**, hiện đại, phù hợp khách quốc tế, responsive tốt desktop/tablet/mobile, có hiệu ứng nhẹ. Mục tiêu tối thiểu: **lấy dữ liệu thật từ DB/API, không dùng mock data**.

## 1. Trước khi sửa

Hãy search và đọc source hiện tại, không code theo suy đoán.

Cần kiểm tra:
- Page/component FAQ public hiện tại
- Header/Footer public hiện tại
- API service frontend đang gọi FAQ
- Backend `FaqsController` hoặc public FAQ endpoint
- DTO/Query/Handler liên quan public FAQ
- Bảng `faqs` trong SQL hiện tại

Giữ nguyên:
- Header
- Footer
- Route chính nếu không bắt buộc sửa
- Logic quản lý FAQ nội bộ/dashboard

## 2. Data rule bắt buộc

Public FAQ chỉ hiển thị:

```text
status = PUBLISHED
```

Không hiển thị:
- HIDDEN
- internal metadata không cần thiết
- created_by / updated_by nếu không phục vụ public UI

FAQ type dùng đúng DB hiện tại:

```text
ACCOUNT_ACCESS
VISIT_REQUEST
DELEGATION_MANAGEMENT
LOGISTICS_RESOURCE
DOCUMENT_MEDIA
NOTIFICATION_EMAIL
OTHER
```

Không dùng category cũ như Tuition/Visa/Dormitory nếu DB không còn.

## 3. UI layout cần thiết kế

Thiết kế lại body FAQ theo bố cục:

### A. Compact Hero

- Nền navy `#004c91`
- Badge: HELP CENTER
- Title: “Chúng tôi có thể giúp gì cho bạn?”
- Subtitle ngắn về tài khoản, đăng ký tham quan, quản lý đoàn, hậu cần, tài liệu, thông báo.
- Search box lớn, nổi bật.
- Không dùng dấu hỏi quá to chiếm màn hình.

### B. Topic Cards

Hiển thị các nhóm FAQ theo type thật:
- Tài khoản & truy cập
- Đăng ký tham quan
- Quản lý đoàn
- Hậu cần
- Tài liệu & media
- Thông báo & email
- Khác

Mỗi card có icon, label, số lượng câu hỏi thật, mô tả ngắn. Click card sẽ filter FAQ list.

### C. Suggested Questions

Hiển thị 4–6 câu hỏi nổi bật/đầu tiên theo:

```text
display_order ASC, created_at DESC
```

Không bịa “popular” nếu DB chưa có `view_count`.

### D. Main FAQ Section

Desktop:
- Left sticky topic nav
- Right accordion list

Tablet/mobile:
- Topic nav chuyển thành horizontal chips
- Accordion full width

Accordion:
- Hiển thị category badge + question + chevron
- Khi mở, answer nằm trong box nền slate-50, viền trái xanh/cam
- Chevron rotate mượt
- Chỉ mở/đóng trong UI, không làm mất data

### E. Final CTA

Section cuối trước footer:
- “Bạn vẫn cần hỗ trợ?”
- Button: “Liên hệ Phòng HTQT”
- Button: “Đăng ký tham quan”

## 4. Search/filter behavior

Search:
- Debounce 300–500ms
- Search trong question, answer, faqType
- Case-insensitive contains
- Kết hợp AND với selected category
- Hiển thị “Tìm thấy X câu hỏi”
- Có empty state khi không có kết quả

Empty state:

```text
Không tìm thấy câu hỏi phù hợp. Hãy thử từ khóa khác hoặc liên hệ Phòng HTQT.
```

## 5. Responsive mobile bắt buộc

Mobile phải xử lý riêng:
- Hero thấp hơn desktop
- Search full width
- Topic cards thành 1–2 cột hoặc horizontal carousel
- Topic chips scroll ngang, không xuống quá nhiều dòng
- FAQ card full width
- Không horizontal scroll
- Button/tab đủ lớn để bấm bằng tay
- Font title không quá to

## 6. Animation

Thêm hiệu ứng nhẹ:
- Hero fade-up
- Topic cards stagger fade-up
- Card hover nâng nhẹ trên desktop
- Search focus glow nhẹ
- Accordion expand/collapse mượt
- Active topic chuyển màu cam
- Loading skeleton shimmer

Không dùng animation lố, bounce mạnh, 3D rotate, particle.

## 7. Style

Dùng đúng brand:
- Primary blue: `#004c91`
- Orange: `#F37021`
- Text: slate-800/slate-900
- Text phụ: slate-500/slate-600
- Border: slate-200
- Background: white/slate-50

Thiết kế sạch, chuyên nghiệp, nhiều khoảng trắng, không quá nhiều khung lớn.

## 8. Không được làm

- Không dùng mock data
- Không hardcode FAQ
- Không sửa dashboard FAQ management nếu không cần
- Không đổi business logic FAQ
- Không thêm table/field/enum nếu SQL không có
- Không làm public lộ FAQ hidden/internal
- Không làm build TypeScript/C# lỗi

## 9. Test sau khi sửa

Kiểm tra:
- Desktop/tablet/mobile đẹp, không tràn ngang
- Search có kết quả
- Search không có kết quả
- Filter theo từng FAQ type
- Accordion mở/đóng đúng
- Không có FAQ thì empty state đẹp
- Chỉ hiển thị PUBLISHED
- Header/footer giữ nguyên
- Build/typecheck/lint không lỗi

## 10. Report cuối

Trả report gồm:
1. File đã sửa/tạo
2. API đã dùng/tạo
3. Data source thật
4. UI layout mới
5. Responsive mobile đã xử lý
6. Case đã test
7. Build/test result
8. Phần chưa làm được hoặc cần xác nhận
