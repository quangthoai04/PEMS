# PEMS Public Routes i18n Deep Audit & Completion Prompt — VI/EN

> Dùng file này để giao cho AI Agent/code agent tự đọc source, tự xác định toàn bộ route public không cần đăng nhập, rồi hoàn thiện dịch Anh/Việt full sâu chi tiết cho mọi trang public của PEMS.

---

## 0. Mục tiêu

Hiện tại hệ thống PEMS đã có language switcher `VI/EN`, nhưng nhiều trang public vẫn còn text chưa dịch, ví dụ:

```text
/partners
/faq hoặc /faqs
/news
/visit-fptu
Search popup
Header/Footer
Modal đăng ký tham quan
Login modal
Các trang/chức năng public khác nếu có
```

Nhiệm vụ của bạn là:

1. **Tự đọc source để xác định tất cả route/page mà người chưa đăng nhập vẫn có thể truy cập và xem thông tin.**
2. **Hoàn thiện i18n full sâu chi tiết cho toàn bộ các route/page đó.**
3. Không chỉ dịch menu/header/title, mà phải dịch toàn bộ UI/state/flow liên quan:
   - UI text cứng
   - button/link/CTA
   - placeholder/input label/helper text
   - toast
   - validation error
   - API error message
   - empty/loading/error state
   - modal/popover/dropdown/tooltip
   - pagination/filter/search
   - card/table/badge/status/category label
   - alt/title/aria-label
   - document title nếu project có set title

Khi chọn **EN**, public UI không được còn tiếng Việt, trừ brand name/tên riêng/dynamic DB content chưa có bản dịch và có fallback rõ ràng.

Khi chọn **VI**, public UI không được còn tiếng Anh không cần thiết, trừ brand name/tên riêng/product term đã được chốt giữ nguyên.

---

## 1. Vai trò của AI Agent

Bạn là:

```text
Senior React TypeScript Engineer
Senior Frontend i18n Engineer
Senior UI/UX Engineer
QA reviewer for public-facing multilingual UI
```

Bạn phải làm việc theo hướng:

```text
Read source first
→ Identify all public routes
→ Audit every public page/modal/state
→ Add/update i18n keys
→ Replace hardcoded text
→ Preserve UI layout
→ Verify VI/EN
→ Build/test
→ Report clearly
```

Không sửa theo suy đoán. Không chỉ sửa các trang được chụp ảnh. Phải dựa vào routing/auth guard thật trong source.

---

## 2. Bắt buộc đọc source trước khi sửa

Trước khi sửa code, hãy search và đọc source hiện tại.

Cần kiểm tra tối thiểu:

```text
frontend/pems-react/src/App.tsx
frontend/pems-react/src/shared/constants/appRoutes.ts nếu có
frontend/pems-react/src/routes/** nếu có
frontend/pems-react/src/components/layout/**
frontend/pems-react/src/components/modals/**
frontend/pems-react/src/pages/**
frontend/pems-react/src/features/**
frontend/pems-react/src/shared/i18n/**
frontend/pems-react/src/shared/api/**
frontend/pems-react/src/shared/utils/**
frontend/pems-react/src/shared/constants/**
```

Mục tiêu là xác định route nào:

```text
- Không nằm trong ProtectedRoute/AuthGuard/RequireAuth/PrivateRoute
- Không yêu cầu token để render trang
- Có thể mở từ header/footer/public link
- Có modal public được mở từ page public
- Có form public cho người chưa đăng nhập
- Có page public token như email action nếu có
```

Không đoán theo tên file. Phải dựa vào route/auth guard thật trong source.

---

## 3. Cách tự tìm public routes

Hãy search các pattern sau:

```bash
rg "Route|path=|createBrowserRouter|BrowserRouter|Routes" frontend/pems-react/src
rg "ProtectedRoute|RequireAuth|AuthGuard|PrivateRoute|role|token" frontend/pems-react/src
rg "Header|Footer|PublicLayout|Layout" frontend/pems-react/src
rg "LoginModal|SearchPopup|Visit|Register|Request|Booking" frontend/pems-react/src
```

Sau đó lập danh sách route theo format:

```text
Route:
Component:
Có cần đăng nhập không:
Layout:
Modal/form public liên quan:
API public liên quan:
Trạng thái i18n hiện tại:
```

Các route dự kiến cần audit, nhưng vẫn phải xác nhận bằng source thật:

```text
/
/news
/news/:id
/partners
/partners/:id nếu có
/faq hoặc /faqs
/visit-fptu
/outbound nếu là page nội bộ trong app
/inbound nếu là page nội bộ trong app
/contact nếu có
/privacy-policy nếu có
/terms hoặc /terms-of-use nếu có
/login hoặc /sign-in
/public/email-actions/* nếu có
search popup/public search
modal đăng ký tham quan mở từ homepage hoặc visit-fptu
```

Nếu route là external link như `https://outbound.fpt.edu.vn/`, chỉ cần dịch label hiển thị trong PEMS header/footer. Không sửa website ngoài.

---

## 4. Public route nào cũng phải i18n full sâu

Với từng public route/page, phải audit đủ các nhóm sau:

```text
[ ] Header/menu active label
[ ] Footer
[ ] Page title
[ ] Hero title/subtitle
[ ] Section title/subtitle
[ ] Card title/description
[ ] Button/link/CTA
[ ] Search input placeholder
[ ] Filter label/dropdown option
[ ] Sort label/dropdown option
[ ] Pagination label
[ ] Empty state
[ ] Loading state
[ ] Error state
[ ] API error message
[ ] Toast success/error/warning/info
[ ] Modal title/body/footer
[ ] Popover/dropdown/tooltip
[ ] Form label
[ ] Form placeholder
[ ] Helper text
[ ] Validation message
[ ] Confirm dialog
[ ] Badge/status/category label
[ ] alt/title/aria-label
[ ] Browser document title nếu project có dùng
```

Không được chỉ dịch phần đang nhìn thấy ở màn đầu. Phải mở hết:

```text
- Modal
- Drawer mobile
- Dropdown language
- Search popup
- Filter dropdown
- Sort dropdown
- Detail modal/detail page
- Form step 1/2/3
- Toast/error state
- Validation khi submit form trống
- Empty state khi search/filter không có kết quả
```

---

## 5. Các trang cần chú ý đặc biệt

### 5.1. `/news`

Phải dịch full:

```text
- Hero news title/subtitle
- Badge như Nổi bật, Tin chung, Từ chuyến thăm
- Date label nếu có
- Đọc tiếp
- Search placeholder
- Filter: Tất cả, Nổi bật, Từ chuyến thăm, Tin chung, Tất cả cơ sở
- Sort: Mới nhất, Cũ nhất
- Section title: Tin nổi bật, Dấu ấn các chuyến thăm
- Empty state
- Loading state
- Error state
- News card labels
- Sidebar/right column labels nếu có
- Related news nếu có
```

Gợi ý wording:

```text
Nổi bật -> Featured
Tin chung -> General News
Từ chuyến thăm -> Visit Stories
Tất cả cơ sở -> All campuses
Mới nhất -> Newest
Cũ nhất -> Oldest
Đọc tiếp -> Read more
Tin nổi bật -> Featured News
Dấu ấn các chuyến thăm -> Visit Highlights
```

Lưu ý: dữ liệu DB như `title`, `summary`, `content` của news có thể vẫn là tiếng Việt nếu backend chưa có bản dịch. Không được hardcode dịch ở frontend cho nội dung dài. Hãy truyền `languageCode` hoặc `Accept-Language` nếu backend hỗ trợ. Nếu backend chưa có bản dịch, ghi rõ trong report là dynamic DB content đang fallback.

---

### 5.2. `/partners`

Phải dịch full:

```text
- Đối tác & Hợp tác quốc tế
- FPT University Partnership Network
- Mô tả hero
- Đăng ký ghé thăm
- Khám phá Visit FPTU
- Tổng đối tác công khai
- Quốc gia
- Loại hình đối tác
- Search placeholder: Tìm tên đối tác, quốc gia, mô tả...
- Tất cả quốc gia
- Tất cả loại hình
- Tên A-Z
- Khám phá theo quốc gia
- Country chips
- Partner cards
- View detail
- Website
- Location
- Description
- Empty/loading/error states
```

Gợi ý wording:

```text
Đối tác & Hợp tác quốc tế -> International Partnerships & Collaboration
Đăng ký ghé thăm -> Book a Visit
Khám phá Visit FPTU -> Explore Visit FPTU
Tổng đối tác công khai -> Public Partners
Quốc gia -> Countries
Loại hình đối tác -> Partner Types
Tìm tên đối tác, quốc gia, mô tả... -> Search by partner name, country, or description...
Tất cả quốc gia -> All countries
Tất cả loại hình -> All types
Tên A-Z -> Name A-Z
Khám phá theo quốc gia -> Explore by Country
```

Tên đối tác, tên quốc gia, mô tả đối tác là dynamic DB content. Nếu có translation table/API thì dùng theo `languageCode`. Nếu chưa có, fallback có kiểm soát và ghi report.

---

### 5.3. `/faq` hoặc `/faqs`

Phải dịch full:

```text
- Hero FAQ title/subtitle nếu có
- Duyệt theo chủ đề
- Tất cả
- Khác
- Đăng ký tham quan
- Tài khoản và truy cập
- Hậu cần và tài nguyên
- Thông báo và email
- Quản lý đoàn tiếp khách
- Tài liệu và truyền thông
- Câu hỏi nổi bật
- FAQ category descriptions
- FAQ card/question/answer UI
- Search FAQ nếu có
- Empty state
- Loading state
- Error state
```

FAQ category enum không được đổi value kỹ thuật. Chỉ map label qua i18n.

Ví dụ:

```ts
ACCOUNT_ACCESS -> t('faq.category.accountAccess')
VISIT_REQUEST -> t('faq.category.visitRequest')
DELEGATION_MANAGEMENT -> t('faq.category.delegationManagement')
LOGISTICS_RESOURCE -> t('faq.category.logisticsResource')
DOCUMENT_MEDIA -> t('faq.category.documentMedia')
NOTIFICATION_EMAIL -> t('faq.category.notificationEmail')
OTHER -> t('faq.category.other')
```

Gợi ý wording:

```text
Duyệt theo chủ đề -> Browse by Topic
Câu hỏi nổi bật -> Featured Questions
Tài khoản và truy cập -> Account & Access
Đăng ký tham quan -> Visit Request
Hậu cần và tài nguyên -> Logistics & Resources
Thông báo và email -> Notifications & Email
Quản lý đoàn tiếp khách -> Delegation Management
Tài liệu và truyền thông -> Documents & Media
Khác -> Other
```

---

### 5.4. `/visit-fptu`

Phải dịch full:

```text
- Hero/section title
- Description
- Bullet list
- CTA: Bắt đầu khám phá, Đăng ký tham quan
- Gallery card title/description/caption
- Area/location/item labels
- Audio/TTS controls nếu có
- Loading audio
- Audio unavailable
- Failed to load audio
- Footer trên page
```

Gợi ý wording:

```text
Bắt đầu khám phá -> Start Exploring
Đăng ký tham quan -> Book a Visit
Hiểu môi trường học thuật và sinh hoạt -> Understand the academic and campus life environment
Khám phá các góc sống ảo độc quyền -> Discover unique photo spots
Chiêm ngưỡng cơ sở vật chất 5 sao -> Explore modern campus facilities
Audio không khả dụng -> Audio is not available
Đang tải audio -> Loading audio
Không thể tải audio -> Failed to load audio
```

---

### 5.5. Modal đăng ký tham quan public

Modal này là flow public hoàn chỉnh, bắt buộc i18n full cả 3 step.

Header:

```text
Campus Visit
Đăng ký tham quan trường
Vui lòng điền đầy đủ thông tin dưới đây để đăng ký lịch trình tham quan.
Close button aria-label
```

Gợi ý EN:

```text
Campus Visit
Book a Campus Visit
Please complete the information below to request a campus visit.
Close
```

Stepper:

```text
Thông tin đăng ký -> Registration Information
Thành phần tham dự -> Delegation Members
Yêu cầu bổ sung -> Additional Requests
1/3, 2/3, 3/3
```

Step 1:

```text
Thông tin người đăng ký -> Registrant Information
I. Thông tin người đăng ký -> I. Registrant Information
Họ và tên -> Full Name
Quốc tịch -> Nationality
Đơn vị / Tổ chức -> Organization
Nhập tên đơn vị/tổ chức của bạn... -> Enter or search for your organization...
Chức danh, phòng ban -> Position / Department
Số điện thoại -> Phone Number
Email -> Email
Campus/cơ sở muốn tham quan -> Campus to Visit
Thời gian dự kiến -> Preferred Visit Time
```

Step 2:

```text
Thành phần tham dự -> Delegation Members
Số lượng khách -> Number of Visitors
Danh sách thành viên -> Member List
Họ và tên thành viên -> Member Full Name
Chức danh -> Position
Email -> Email
Số điện thoại -> Phone Number
Thêm thành viên -> Add Member
Xóa thành viên -> Remove Member
```

Step 3:

```text
Yêu cầu bổ sung -> Additional Requests
Mục đích chuyến thăm -> Visit Purpose
Ghi chú -> Notes
Yêu cầu phiên dịch -> Interpretation Request
Yêu cầu truyền thông/hình ảnh -> Media Request
Yêu cầu hậu cần -> Logistics Request
Điều khoản/chính sách -> Terms and Policy
```

Footer buttons:

```text
Hủy -> Cancel
Quay lại -> Back
Lưu tạm 30p -> Save draft for 30 min
Tiếp theo -> Next
Gửi yêu cầu -> Submit Request
Đang gửi... -> Submitting...
Đã lưu tạm -> Draft saved
Lưu tạm thất bại -> Failed to save draft
```

Validation/toast/API errors:

```text
Trường này là bắt buộc. -> This field is required.
Vui lòng nhập email hợp lệ. -> Please enter a valid email address.
Vui lòng nhập số điện thoại hợp lệ. -> Please enter a valid phone number.
Vui lòng chọn quốc tịch. -> Please select a nationality.
Vui lòng chọn ít nhất một cơ sở. -> Please select at least one campus.
Ngày tham quan không được ở quá khứ. -> The visit date cannot be in the past.
Số lượng khách phải lớn hơn 0. -> The number of visitors must be greater than 0.
Gửi yêu cầu tham quan thành công. -> Your visit request has been submitted successfully.
Không thể gửi yêu cầu. Vui lòng thử lại. -> Failed to submit the request. Please try again.
Lưu tạm thành công. -> Draft saved successfully.
Không thể lưu tạm. Vui lòng thử lại. -> Failed to save draft. Please try again.
```

---

### 5.6. Search popup public

Phải dịch full:

```text
- Search title
- Search placeholder
- Search button
- Recent searches nếu có
- Popular keywords nếu có
- No results
- Loading
- Error
- Result type labels: News, FAQ, Partner, Gallery
- View all results
- Clear search
- Close search aria-label
```

Gợi ý wording:

```text
Tìm kiếm -> Search
Nhập từ khóa tìm kiếm... -> Enter a search keyword...
Tìm kiếm gần đây -> Recent Searches
Từ khóa phổ biến -> Popular Keywords
Không tìm thấy kết quả -> No matching results found
Xem tất cả kết quả -> View all results
Xóa tìm kiếm -> Clear search
Đóng tìm kiếm -> Close search
```

---

### 5.7. Header/Footer/Public layout

Header phải dịch:

```text
- Department label: Phòng HTQT / Intl. Relations
- Home
- News
- Partners
- Outbound
- Inbound
- Visit FPTU
- FAQs
- Search aria-label
- Language label/dropdown
- Sign in
- Mobile menu labels
```

Footer phải dịch:

```text
- International Relations Department
- Education System
- Connect with us
- Privacy Policy
- Terms of Use
- FAQs
- Address/contact labels
- Copyright nếu có
```

Chú ý layout header:

```text
- Header được phép dùng label ngắn để tránh vỡ layout.
- Page title bên trong trang có thể dùng label đầy đủ.
- Không để lỗi kiểu: InboundVisit FPTU Gallery bị dính vào nhau.
```

---

### 5.8. Login modal/public auth UI

Nếu login modal mở được khi chưa đăng nhập, nó cũng thuộc public scope.

Phải dịch:

```text
- Title/subtitle
- Internal portal/visitor portal nếu có
- Campus selection
- Sign in with Google
- Email/password nếu có
- Forgot password nếu có
- Error message
- Loading state
- Disabled account message
- Wrong portal message
- Close aria-label
```

---

## 6. Không được để text hardcoded trong public source

Sau khi xác định public components, hãy search text hardcoded:

```bash
rg "[ÀÁÂÃÈÉÊÌÍÒÓÔÕÙÚĂĐĨŨƠàáâãèéêìíòóôõùúăđĩũơƯĂẮẰẲẴẶẤẦẨẪẬẾỀỂỄỆỐỒỔỖỘỚỜỞỠỢỨỪỬỮỰỳýỷỹỵ]" frontend/pems-react/src
rg "toast\\.|alert\\(|confirm\\(|placeholder=|aria-label=|title=|alt=" frontend/pems-react/src
rg ">[^<{]*[A-Za-zÀ-ỹ][^<{]*<" frontend/pems-react/src
```

Không nhất thiết mọi kết quả đều sai, nhưng phải review từng kết quả trong public source.

Cho phép giữ nguyên:

```text
- FPT University
- FPT Education
- Email, URL, địa chỉ
- Tên đối tác/tên quốc gia nếu là dữ liệu DB/tên riêng
- Enum value kỹ thuật không hiển thị trực tiếp
```

Không được giữ:

```text
- Label tiếng Việt trong EN mode
- Toast tiếng Việt trong EN mode
- Validation tiếng Việt trong EN mode
- Placeholder tiếng Việt trong EN mode
- Empty/loading/error tiếng Việt trong EN mode
- Menu/footer tiếng Việt trong EN mode
```

---

## 7. Cấu trúc locale đề xuất

Nếu project đã có i18n structure thì mở rộng structure hiện tại. Nếu chưa đủ namespace, tạo/bổ sung:

```text
frontend/pems-react/src/shared/i18n/locales/vi/
- common.json
- publicLayout.json
- home.json
- news.json
- partners.json
- faq.json
- visitFptu.json
- visitRequest.json
- search.json
- validation.json
- errors.json
- toast.json

frontend/pems-react/src/shared/i18n/locales/en/
- common.json
- publicLayout.json
- home.json
- news.json
- partners.json
- faq.json
- visitFptu.json
- visitRequest.json
- search.json
- validation.json
- errors.json
- toast.json
```

Không gom tất cả key vào một file JSON khổng lồ nếu project đang chia namespace.

---

## 8. Quy tắc đặt key

Key phải rõ module, không đặt chung chung.

Ví dụ tốt:

```json
{
  "hero": {
    "title": "Đối tác & Hợp tác quốc tế",
    "subtitle": "Kết nối học thuật, doanh nghiệp và tổ chức toàn cầu trong hệ sinh thái FPT University."
  },
  "filters": {
    "searchPlaceholder": "Tìm tên đối tác, quốc gia, mô tả...",
    "allCountries": "Tất cả quốc gia",
    "allTypes": "Tất cả loại hình",
    "sortNameAsc": "Tên A-Z"
  },
  "empty": {
    "title": "Không tìm thấy đối tác phù hợp",
    "description": "Hãy thử thay đổi từ khóa hoặc bộ lọc."
  }
}
```

Không đặt key kiểu:

```json
{
  "text1": "...",
  "button1": "...",
  "abc": "..."
}
```

---

## 9. Chuẩn wording VI/EN

Dùng wording thống nhất sau:

```text
Trang chủ -> Home
Tin tức -> News
Đối tác -> Partners
Đối tác & Hợp tác quốc tế -> International Partnerships & Collaboration
Đăng ký ghé thăm -> Book a Visit
Khám phá Visit FPTU -> Explore Visit FPTU
Thư viện FPTU -> Visit FPTU
Thư viện Visit FPTU -> Visit FPTU Gallery
Câu hỏi thường gặp -> FAQs
Đăng nhập -> Sign in
Tìm kiếm -> Search
Tất cả -> All
Tất cả quốc gia -> All countries
Tất cả loại hình -> All types
Tên A-Z -> Name A-Z
Mới nhất -> Newest
Cũ nhất -> Oldest
Nổi bật -> Featured
Tin chung -> General News
Từ chuyến thăm -> Visit Stories
Đọc tiếp -> Read more
Duyệt theo chủ đề -> Browse by Topic
Câu hỏi nổi bật -> Featured Questions
Không có dữ liệu -> No data available
Không tìm thấy kết quả -> No matching results found
Đang tải -> Loading
Thử lại -> Try again
```

Header có thể dùng label ngắn để tránh vỡ layout:

```text
Câu hỏi thường gặp -> FAQ
Visit FPTU Gallery -> Visit FPTU
Thư viện Visit FPTU -> Thư viện FPTU
```

Page title bên trong trang vẫn có thể dùng bản đầy đủ.

---

## 10. Dynamic DB content rule

Phân biệt rõ:

### 10.1. UI static text

Dịch ở frontend bằng i18n JSON.

### 10.2. Dynamic DB content

Không tự dịch nội dung dài bằng frontend. Cần lấy theo `languageCode` nếu backend đã hỗ trợ.

Public API nên truyền một trong hai cách:

```text
?languageCode=vi|en
```

hoặc:

```text
Accept-Language: vi|en
```

Cần kiểm tra các service public:

```text
public news API
public partners API
public FAQ API
public gallery/visit-fptu API
public search API
```

Nếu backend chưa có bản dịch DB content, frontend fallback nhưng phải ghi rõ trong report:

```text
Dynamic DB content chưa có bản dịch EN, đang fallback sang dữ liệu gốc từ API.
```

Không được để UI crash hoặc hiện raw key.

---

## 11. Layout stability khi đổi ngôn ngữ

Khi đổi VI/EN, không được làm layout vỡ hoặc text đè nhau.

Bắt buộc kiểm tra:

```text
- Header không bị sát chữ như InboundVisit FPTU Gallery
- Button không bị giãn lệch quá mạnh
- Card không bị tràn chữ
- Filter bar không bị horizontal overflow
- Modal không tràn ngang
- Mobile drawer vẫn đọc được
```

Áp dụng kỹ thuật:

```text
- Header dùng label ngắn riêng nếu cần
- Nav item có min-width/fixed width hợp lý
- Dùng shrink-0 cho icon
- Dùng whitespace-nowrap cho button/nav ngắn
- Dùng truncate có chủ đích cho vùng hẹp
- Dùng flex-wrap cho filter bar
- Dùng max-width ổn định cho modal/content
```

Không được hy sinh khả năng đọc bằng cách truncate nội dung quan trọng trong body chính. Chỉ truncate ở header/nav/card phụ nếu thật sự cần.

---

## 12. Test bắt buộc

Manual test:

```text
1. Clear token/localStorage hoặc mở incognito.
2. Chọn EN.
3. Truy cập từng public route đã tìm được.
4. Mở mọi modal/dropdown/search/filter/detail.
5. Submit form trống để hiện validation.
6. Search từ khóa không tồn tại để hiện empty state.
7. Nếu có thể, simulate API error để kiểm tra error state.
8. Đổi sang VI và kiểm tra lại.
9. Kiểm tra desktop.
10. Kiểm tra mobile/tablet.
```

Build/test:

```bash
npm run build
npm run lint
```

Nếu project không có lint thì chạy typecheck/script tương ứng và ghi rõ.

---

## 13. Không được làm

```text
- Không đổi business logic.
- Không đổi auth guard.
- Không đổi route.
- Không đổi API contract nếu không bắt buộc.
- Không thêm thư viện mới nếu project đã có i18next.
- Không dùng Google Translate runtime để dịch UI.
- Không hardcode song ngữ kiểu "Ngôn ngữ / Language" nếu có thể dùng i18n.
- Không báo hoàn thành nếu mới dịch page chính nhưng modal/toast/validation vẫn chưa dịch.
- Không bỏ qua mobile drawer.
- Không bỏ qua footer.
```

---

## 14. Definition of Done

Chỉ báo hoàn thành khi đạt đủ:

```text
[ ] Đã tự tìm và liệt kê toàn bộ public routes không cần đăng nhập.
[ ] Đã audit từng public route.
[ ] EN mode không còn text tiếng Việt trong public UI, trừ tên riêng/dynamic DB fallback có ghi rõ.
[ ] VI mode không còn text tiếng Anh không cần thiết.
[ ] Header/footer đã dịch.
[ ] Search popup đã dịch.
[ ] Public modals/forms đã dịch.
[ ] Toast/validation/API error/empty/loading/error state đã dịch.
[ ] Filter/sort/pagination đã dịch.
[ ] Badge/status/category label đã dịch.
[ ] alt/title/aria-label đã dịch.
[ ] Không còn raw translation key trên UI.
[ ] Không có text đè nhau hoặc horizontal scroll do bản dịch dài.
[ ] npm run build pass.
[ ] npm run lint hoặc typecheck pass nếu có.
```

---

## 15. Report sau khi sửa

Sau khi hoàn thành, tạo report ngắn theo format:

```md
# Public i18n Deep Completion Report

## 1. Public routes audited
| Route | Component | Auth required? | Status |
|---|---|---|---|

## 2. Files changed
| File | Change |
|---|---|

## 3. Locale keys added/updated
| Namespace | VI keys | EN keys |
|---|---:|---:|

## 4. Public flows verified
- Header/footer:
- Search popup:
- News:
- Partners:
- FAQ:
- Visit FPTU:
- Visit request modal:
- Login modal:
- Other public routes:

## 5. Dynamic DB content fallback
| Module | Field | Current behavior | Future recommendation |
|---|---|---|---|

## 6. Build/test result
- npm run build:
- npm run lint/typecheck:

## 7. Remaining risks
- ...
```
