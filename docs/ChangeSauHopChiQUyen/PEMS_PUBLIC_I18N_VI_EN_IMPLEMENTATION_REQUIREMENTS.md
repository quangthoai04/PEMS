# PEMS — Public Pages i18n VI/EN Implementation Requirements

> Mục tiêu: triển khai dịch **Anh / Việt** cho toàn bộ các trang public / published của PEMS theo hướng đầy đủ, nhất quán, không phá UI, không làm lệch nghiệp vụ và không để sót text cứng, toast, báo lỗi, validation, empty/loading/error state.

---

## 1. Bối cảnh dự án

PEMS là hệ thống web gồm:

```text
backend/PEMS.Api
backend/PEMS.Application
backend/PEMS.Domain
backend/PEMS.Infrastructure
frontend/pems-react
docs/database/scripts
```

Frontend là React + TypeScript, tổ chức theo hướng feature-based:

```text
frontend/pems-react/src/features
frontend/pems-react/src/pages
frontend/pems-react/src/components
frontend/pems-react/src/shared
frontend/pems-react/src/assets
```

Routing hiện nằm tập trung ở:

```text
frontend/pems-react/src/App.tsx
frontend/pems-react/src/shared/constants/appRoutes.ts
```

Backend là ASP.NET Core Clean Architecture, có các controller/module public và content liên quan:

```text
backend/PEMS.Api/Controllers/PublicContentController.cs
backend/PEMS.Api/Controllers/PublicPartnersController.cs
backend/PEMS.Api/Controllers/PublicVisitFptuController.cs
backend/PEMS.Api/Controllers/PublicGalleryTtsController.cs
backend/PEMS.Api/Controllers/NewsController.cs
backend/PEMS.Api/Controllers/FaqsController.cs
backend/PEMS.Api/Controllers/GalleriesController.cs
backend/PEMS.Api/Controllers/PartnersController.cs
```

Khi triển khai, AI Agent phải **đọc source thật trước**, không sửa theo suy đoán.

---

## 2. Mục tiêu chính

Triển khai hệ thống i18n cho các trang public/published với hai ngôn ngữ:

```text
vi: Tiếng Việt
en: English
```

Yêu cầu quan trọng nhất:

```text
Cập nhật phần nào thì phải full sâu phần đó.
```

Nghĩa là khi xử lý một page/module, không chỉ dịch text chính trên giao diện mà phải xử lý đầy đủ:

```text
- UI text cứng trong JSX/TSX
- Header/title/subtitle/section title
- Button label
- Link label
- Placeholder
- Helper text
- Tooltip
- Modal title/content/action
- Toast success/error/warning/info
- Form validation message
- API error message
- Empty state
- Loading state
- Error state
- Confirm dialog
- Badge/status/filter/enum label
- Table/card/list label
- aria-label/title/alt text nếu có
- Date/time/number display theo locale nếu có
```

Không được để tình trạng:

```text
- Trang đã đổi sang EN nhưng toast vẫn tiếng Việt.
- Empty state vẫn tiếng Việt.
- Validation vẫn tiếng Việt.
- Raw key như public.home.hero.title xuất hiện trên UI.
- Một màn trộn lẫn Anh/Việt.
- Dịch làm vỡ layout, vỡ button, xuống dòng xấu trên mobile.
```

---

## 3. Phạm vi public/published cần xử lý

Tập trung vào các màn public hoặc nội dung đã publish, ưu tiên theo thứ tự:

```text
1. Public Layout / Header / Footer / Language Switcher
2. Homepage
3. Public News
4. Public Partners
5. Public FAQ
6. Public Gallery / Visit FPTU
7. Public Visit Request Form nếu form đăng ký tham quan nằm ngoài portal đăng nhập
8. Contact / Policy / Terms nếu có page public riêng
9. Global public error boundary / not found / unauthorized public message nếu có
```

Khi không chắc đường dẫn file, phải search source theo từ khóa:

```text
Home
Homepage
Public
News
Partners
FAQ
Gallery
VisitFptu
Visit FPTU
Contact
Policy
Terms
Header
Footer
Navbar
Language
Toast
```

---

## 4. Phạm vi được sửa

Được sửa:

```text
- Frontend i18n setup
- Translation JSON/resource files
- Public page/component text binding
- Public layout/header/footer language switcher
- API client gắn languageCode hoặc Accept-Language
- Helper map errorCode sang message i18n
- Helper map enum/status sang label i18n
- Public API DTO/query nếu cần truyền languageCode
- Backend public query handler nếu cần trả content theo languageCode
- SQL patch cho translation table nếu thật sự cần và được xác nhận schema chưa có
- Test/audit script cho missing keys/raw text nếu cần
```

---

## 5. Phạm vi không được sửa

Không được sửa ngoài scope:

```text
- Không đổi business logic approve/reject/cancel/visit workflow.
- Không đổi role/sub_role/authorization policy.
- Không đổi route nếu không bắt buộc.
- Không đổi layout lớn hoặc redesign UI.
- Không đổi màu sắc, spacing, responsive nếu task chỉ là i18n.
- Không xóa chức năng hiện có.
- Không thêm thư viện mới nếu project đã có i18n library phù hợp.
- Không hardcode dữ liệu dịch trong component.
- Không đổi enum/status backend chỉ để hiển thị bản dịch.
- Không dịch brand name, email, URL, route, API path, DTO field, enum value kỹ thuật.
- Không tự thêm bảng/cột DB nếu chưa đối chiếu SQL/schema hiện tại.
```

---

## 6. Kiến trúc i18n frontend đề xuất

### 6.1. Vị trí thư mục

Tạo hoặc chuẩn hóa thư mục:

```text
frontend/pems-react/src/shared/i18n/
├── index.ts
├── resources.ts
├── language.ts
├── errorMessage.ts
├── enumLabel.ts
└── locales/
    ├── vi/
    │   ├── common.json
    │   ├── publicLayout.json
    │   ├── home.json
    │   ├── news.json
    │   ├── partners.json
    │   ├── faq.json
    │   ├── gallery.json
    │   ├── visitRequest.json
    │   ├── validation.json
    │   ├── errors.json
    │   └── toast.json
    └── en/
        ├── common.json
        ├── publicLayout.json
        ├── home.json
        ├── news.json
        ├── partners.json
        ├── faq.json
        ├── gallery.json
        ├── visitRequest.json
        ├── validation.json
        ├── errors.json
        └── toast.json
```

Nếu project đã có cấu trúc i18n khác thì **không tạo song song gây trùng**, phải mở rộng cấu trúc hiện có.

### 6.2. Library

Ưu tiên dùng `react-i18next` nếu đã có hoặc nếu project đang dùng i18next.

Nếu chưa có:

```text
- Kiểm tra package.json trước.
- Chỉ thêm dependency nếu thật sự cần.
- Không tự thêm nhiều library i18n khác nhau.
```

### 6.3. Language persistence

Ngôn ngữ được lưu ở:

```text
localStorage key: pems.language
value: vi | en
```

Fallback:

```text
1. localStorage pems.language nếu hợp lệ
2. browser language nếu là vi hoặc en
3. vi mặc định
```

### 6.4. Language switcher

Language switcher ở public header cần:

```text
- Hiển thị rõ VI / EN hoặc Tiếng Việt / English.
- Đổi ngôn ngữ không reload toàn trang nếu không cần.
- Không làm mất input user đang nhập nếu có thể tránh.
- Có aria-label phù hợp.
- Mobile menu cũng phải có language switcher hoặc vẫn truy cập được.
```

---

## 7. API language strategy

### 7.1. Phân tách rõ UI tĩnh và nội dung động

Frontend dịch:

```text
- Text UI tĩnh
- Toast
- Validation
- Empty/loading/error state
- Enum/status/filter label
```

Backend/database xử lý nội dung động:

```text
- News title/summary/content
- Partner description/collaboration summary/location text
- FAQ question/answer
- Gallery area/location/item title/description/caption
- Audio/TTS metadata theo ngôn ngữ nếu có
```

Không nên để frontend tự dịch các đoạn content dài lấy từ DB bằng map cứng hoặc Google Translate runtime.

### 7.2. Query/header truyền ngôn ngữ

Public API nên hỗ trợ một trong hai cách, ưu tiên thống nhất toàn hệ thống:

```http
GET /api/public/news?languageCode=en
GET /api/public/faqs?languageCode=vi
GET /api/public/partners?languageCode=en
GET /api/public/visit-fptu/gallery?languageCode=en
```

Hoặc:

```http
Accept-Language: en
Accept-Language: vi
```

Khuyến nghị triển khai cả hai mức:

```text
- Frontend luôn gửi languageCode query cho public content endpoint.
- API client cũng gắn Accept-Language để backend dùng cho error/message chung.
```

### 7.3. Backend fallback

Nếu content bản EN chưa tồn tại:

```text
- Không crash API.
- Không trả null gây vỡ UI.
- Fallback sang bản VI hoặc default content.
- Response nên có cờ optional nếu cần: translationMissing = true.
```

Ví dụ DTO:

```json
{
  "id": 123,
  "title": "FPT University welcomes international delegation",
  "summary": "...",
  "languageCode": "en",
  "translationMissing": false
}
```

Nếu fallback VI:

```json
{
  "id": 123,
  "title": "Đại học FPT đón tiếp đoàn khách quốc tế",
  "summary": "...",
  "languageCode": "vi",
  "requestedLanguageCode": "en",
  "translationMissing": true
}
```

Frontend có thể hiển thị nhẹ nhàng, không bắt buộc nếu UI chưa cần.

---

## 8. Quy tắc xử lý lỗi và toast

### 8.1. Không phụ thuộc message backend tiếng Việt

Backend có thể vẫn trả `message`, nhưng frontend public không được chỉ hiển thị raw message nếu đang EN.

Chuẩn response nên có:

```json
{
  "success": false,
  "errorCode": "VISIT_REQUEST_DATE_IN_PAST",
  "message": "Ngày tham quan không được ở quá khứ",
  "params": {}
}
```

Frontend xử lý:

```ts
getTranslatedErrorMessage(errorCode, params, fallbackMessage)
```

Nếu không có `errorCode`:

```text
- EN: Something went wrong. Please try again.
- VI: Đã xảy ra lỗi. Vui lòng thử lại.
```

### 8.2. Toast key đặt ở `toast.json`

Ví dụ:

```json
{
  "common": {
    "saveSuccess": "Lưu thành công.",
    "saveFailed": "Không thể lưu dữ liệu. Vui lòng thử lại.",
    "loadFailed": "Không thể tải dữ liệu. Vui lòng thử lại."
  },
  "visitRequest": {
    "submitSuccess": "Yêu cầu tham quan đã được gửi thành công.",
    "submitFailed": "Không thể gửi yêu cầu tham quan. Vui lòng thử lại."
  }
}
```

English:

```json
{
  "common": {
    "saveSuccess": "Saved successfully.",
    "saveFailed": "Unable to save. Please try again.",
    "loadFailed": "Unable to load data. Please try again."
  },
  "visitRequest": {
    "submitSuccess": "Your visit request has been submitted successfully.",
    "submitFailed": "Unable to submit your visit request. Please try again."
  }
}
```

---

## 9. Quy tắc validation i18n

Validation message phải đặt trong `validation.json`, không viết trực tiếp trong component.

Ví dụ key:

```json
{
  "required": "Trường này là bắt buộc.",
  "email": "Vui lòng nhập địa chỉ email hợp lệ.",
  "phone": "Số điện thoại không hợp lệ.",
  "maxLength": "Không được vượt quá {{max}} ký tự.",
  "minLength": "Cần tối thiểu {{min}} ký tự.",
  "dateInPast": "Ngày không được ở quá khứ."
}
```

English:

```json
{
  "required": "This field is required.",
  "email": "Please enter a valid email address.",
  "phone": "Please enter a valid phone number.",
  "maxLength": "Must not exceed {{max}} characters.",
  "minLength": "Must be at least {{min}} characters.",
  "dateInPast": "The date cannot be in the past."
}
```

Không nối chuỗi thủ công như:

```ts
'Tối đa ' + max + ' ký tự'
```

Phải dùng interpolation:

```ts
t('validation.maxLength', { max })
```

---

## 10. Quy tắc enum/status/filter label

Không dịch enum value trong database hoặc backend.

Sai:

```ts
if (status === 'Đã xuất bản')
```

Đúng:

```ts
if (status === 'PUBLISHED')
```

Hiển thị:

```ts
t(`common.status.${status}`)
```

Ví dụ key:

```json
{
  "status": {
    "PUBLISHED": "Đã xuất bản",
    "HIDDEN": "Đã ẩn",
    "APPROVED": "Đã duyệt",
    "PENDING_APPROVAL": "Chờ duyệt",
    "REJECTED": "Đã từ chối",
    "CANCELLED": "Đã hủy"
  }
}
```

English:

```json
{
  "status": {
    "PUBLISHED": "Published",
    "HIDDEN": "Hidden",
    "APPROVED": "Approved",
    "PENDING_APPROVAL": "Pending approval",
    "REJECTED": "Rejected",
    "CANCELLED": "Cancelled"
  }
}
```

---

## 11. Module requirements

## 11.1. Public Layout / Header / Footer

Phải xử lý:

```text
- Menu label
- Login button
- CTA button
- Language switcher
- Mobile menu
- Footer section title
- Footer link
- Contact label
- Copyright
- Social/external link aria-label nếu có
```

Key gợi ý:

```text
publicLayout.nav.home
publicLayout.nav.news
publicLayout.nav.partners
publicLayout.nav.faq
publicLayout.nav.gallery
publicLayout.nav.contact
publicLayout.nav.login
publicLayout.language.vi
publicLayout.language.en
publicLayout.footer.quickLinks
publicLayout.footer.contact
publicLayout.footer.copyright
```

English wording:

```text
Home
News
Partners
FAQs
Visit FPTU Gallery
Contact
Sign in
```

Vietnamese wording:

```text
Trang chủ
Tin tức
Đối tác
Câu hỏi thường gặp
Thư viện Visit FPTU
Liên hệ
Đăng nhập
```

---

## 11.2. Homepage

Phải xử lý:

```text
- Hero title/subtitle
- CTA chính/phụ
- Section title/subtitle
- Card title/description
- News preview section
- Partner preview section
- Gallery preview section
- FAQ preview section
- Loading/empty/error state cho từng section nếu lấy API
- Button “Xem thêm” / “View more”
```

Không phá UI:

```text
- Không đổi layout homepage.
- English text phải ngắn gọn để không làm card quá cao.
- Nếu title dài, dùng line-clamp hiện có nếu đã có.
```

---

## 11.3. Public News

### UI tĩnh cần dịch

```text
- News
- Latest news
- Featured news
- Search news
- All categories
- Published on
- Author
- Read more
- Back to news
- Related news
- No news available
- No matching news found
- Unable to load news
```

### Dữ liệu động cần theo languageCode

```text
- title
- summary
- content
- content section heading/body
- image caption
- category display name nếu category là content hiển thị
```

### Backend rule

Nếu đã có `news_translations`, dùng bảng hiện có. Không tạo bảng mới trùng chức năng.

Public API phải chỉ trả bài đã publish/public. Không để đổi languageCode làm lộ draft/hidden/internal content.

---

## 11.4. Public Partners

### UI tĩnh cần dịch

```text
- Our Partners
- International partner network
- Search partners
- Country
- Field / Sector
- Website
- View details
- Partner details
- Location
- Contact information
- Visit website
- No partners available
- No matching partners found
- Unable to load partners
```

### Dữ liệu động cần theo languageCode

```text
- display_name nếu có tên hiển thị song ngữ
- description
- collaboration_summary
- location_text nếu là text nhập tay
- sector/field label nếu không phải enum cố định
```

### Proper noun rule

Không dịch tên riêng nếu không có bản dịch chính thức:

```text
Kyoto University -> Kyoto University
FPT University -> FPT University
Da Nang Campus -> Da Nang Campus hoặc FPT University Da Nang Campus tùy content gốc
```

Không dịch máy móc thành tên kỳ lạ.

---

## 11.5. Public FAQ

### UI tĩnh cần dịch

```text
- Frequently Asked Questions
- Search FAQs
- All categories
- Clear search
- No FAQs available
- No FAQs available in this category
- No matching FAQs found
- Unable to load FAQs
```

### FAQ type label

Các enum kỹ thuật giữ nguyên:

```text
ACCOUNT_ACCESS
VISIT_REQUEST
DELEGATION_MANAGEMENT
LOGISTICS_RESOURCE
DOCUMENT_MEDIA
NOTIFICATION_EMAIL
OTHER
```

Label VI:

```text
ACCOUNT_ACCESS -> Tài khoản và đăng nhập
VISIT_REQUEST -> Yêu cầu tham quan
DELEGATION_MANAGEMENT -> Quản lý đoàn khách
LOGISTICS_RESOURCE -> Hậu cần và tài nguyên
DOCUMENT_MEDIA -> Tài liệu và truyền thông
NOTIFICATION_EMAIL -> Thông báo và email
OTHER -> Khác
```

Label EN:

```text
ACCOUNT_ACCESS -> Account and sign-in
VISIT_REQUEST -> Visit requests
DELEGATION_MANAGEMENT -> Delegation management
LOGISTICS_RESOURCE -> Logistics and resources
DOCUMENT_MEDIA -> Documents and media
NOTIFICATION_EMAIL -> Notifications and email
OTHER -> Other
```

### Dữ liệu động cần theo languageCode

```text
- question
- answer
```

Nếu DB hiện chưa có translation table cho FAQ, đề xuất thêm sau khi xác nhận schema:

```sql
CREATE TABLE faq_translations (
    faq_translation_id BIGINT UNSIGNED PRIMARY KEY AUTO_INCREMENT,
    faq_id BIGINT UNSIGNED NOT NULL,
    language_code VARCHAR(10) NOT NULL,
    question TEXT NOT NULL,
    answer TEXT NOT NULL,
    created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at DATETIME NULL DEFAULT NULL ON UPDATE CURRENT_TIMESTAMP,
    UNIQUE KEY uq_faq_translations_faq_language (faq_id, language_code),
    CONSTRAINT fk_faq_translations_faq FOREIGN KEY (faq_id) REFERENCES faqs(faq_id)
);
```

Không thêm SQL nếu schema mới đã có bảng tương đương.

---

## 11.6. Public Gallery / Visit FPTU

### UI tĩnh cần dịch

```text
- Visit FPTU Gallery
- Explore FPT University
- Areas
- Locations
- Media
- Photos
- Videos
- Audio guide
- Listen
- Pause
- View details
- Back to gallery
- No locations available
- No media available
- Unable to load gallery
- English audio is not available
- Vietnamese audio is not available
```

### Dữ liệu động cần theo languageCode

```text
- area name
- area description
- location name
- location description
- item title
- item description
- media caption
```

### TTS/audio rule

Nếu gallery có TTS/audio:

```text
- Audio phải gắn languageCode.
- EN mode ưu tiên audio EN.
- VI mode ưu tiên audio VI.
- Không phát audio VI trong EN mode nếu không có cảnh báo/fallback rõ.
- Nếu audio chưa có cho ngôn ngữ hiện tại, icon loa vẫn có thể hiển thị disabled hoặc tooltip thông báo.
```

---

## 11.7. Public Visit Request Form

Nếu form đăng ký tham quan là public, đây là module phải làm rất sâu.

### UI tĩnh cần dịch

```text
- Form title/subtitle
- Section title
- Field label
- Placeholder
- Helper text
- Required mark explanation nếu có
- Submit button
- Back/reset/cancel button
- Success screen/message
- Error screen/message
```

### Validation cần dịch

```text
- Required
- Invalid email
- Invalid phone
- Date cannot be in the past
- Start time must be before end time
- Please select at least one campus
- Guest count must be greater than 0
- Maximum length exceeded
```

### Toast cần dịch

```text
- Submit success
- Submit failed
- Draft saved nếu có
- Upload failed nếu có file
- Session expired nếu có auth liên quan
```

### API error cần map

Ví dụ:

```text
VISIT_REQUEST_DATE_IN_PAST
VISIT_REQUEST_CAMPUS_REQUIRED
VISIT_REQUEST_INVALID_EMAIL
VISIT_REQUEST_DUPLICATE_CONTACT
VISIT_REQUEST_OUTSIDE_WORKING_HOURS
```

Nếu error code chưa chuẩn, chỉ map các code đang có trong source. Không bịa code trong code thật.

---

## 12. Translation key naming convention

Dùng key có namespace rõ ràng:

```text
common.*
publicLayout.*
home.*
news.*
partners.*
faq.*
gallery.*
visitRequest.*
validation.*
errors.*
toast.*
```

Không dùng key quá chung kiểu:

```text
title
button1
text2
message
```

Ví dụ tốt:

```text
home.hero.title
home.hero.subtitle
home.hero.primaryCta
news.list.searchPlaceholder
news.detail.backToList
faq.empty.noMatchingResults
partners.detail.visitWebsite
gallery.audio.notAvailable
visitRequest.form.submitSuccess
```

---

## 13. Glossary VI/EN chuẩn cho PEMS

| VI | EN | Ghi chú |
|---|---|---|
| Trang chủ | Home | Dùng trong menu |
| Tin tức | News | Public news |
| Đối tác | Partners | Không dùng Partnership trong menu |
| Câu hỏi thường gặp | FAQs | Tiêu đề đầy đủ: Frequently Asked Questions |
| Thư viện Visit FPTU | Visit FPTU Gallery | Giữ brand/feature name |
| Cơ sở | Campus | Không dùng Facility |
| Đoàn khách | Visiting Delegation | Sát nghiệp vụ tiếp khách |
| Yêu cầu tham quan | Visit Request | Không dùng Tour Request |
| Lịch trình | Agenda | Dùng cho chương trình làm việc |
| Lịch | Schedule / Calendar | Tùy ngữ cảnh |
| Người liên hệ | Contact Person | Form visitor |
| Tổ chức | Organization | Visitor/partner |
| Quốc gia | Country | Form/filter |
| Website | Website | Không dùng Web |
| Xem chi tiết | View details | CTA |
| Xem thêm | View more | Section CTA |
| Đọc thêm | Read more | News CTA |
| Đang tải | Loading | Loading state |
| Không có dữ liệu | No data available | Generic empty |
| Không tìm thấy kết quả | No matching results found | Search/filter empty |
| Thử lại | Try again | Error action |
| Gửi yêu cầu | Submit request | Form submit |
| Đã xuất bản | Published | Content status |
| Đã ẩn | Hidden | Visibility |
| Chờ duyệt | Pending approval | Status |
| Đã duyệt | Approved | Status |
| Đã từ chối | Rejected | Status |
| Đã hủy | Cancelled | Status, dùng British spelling có 2 chữ l nếu muốn nhất quán |
| Hậu cần | Logistics | Không dùng hậu cần = Backend |
| Tài nguyên | Resources | Logistics/resource context |
| Thông báo | Notifications | Module |
| Email | Email | Không dịch |

---

## 14. Quy tắc không phá UI

```text
1. Không đổi layout khi chỉ thay text.
2. Không đổi className Tailwind nếu không cần.
3. Không làm tăng width cố định gây tràn mobile.
4. Với button có text dài, ưu tiên bản dịch ngắn gọn.
5. Kiểm tra desktop/tablet/mobile sau khi đổi EN.
6. Không dùng uppercase toàn bộ cho câu dài tiếng Anh.
7. Không để text dài trong badge. Badge nên dùng label ngắn.
8. Dùng line-clamp/truncate nếu UI hiện đã có pattern đó.
9. Không nhét HTML vào JSON translation nếu có thể dùng <Trans>.
10. Với câu có link/span/strong, dùng <Trans> để giữ markup.
```

---

## 15. Quy tắc bảo mật và dữ liệu cá nhân

Khi dịch public page:

```text
- Không làm lộ dữ liệu nội bộ/draft/hidden chỉ vì languageCode.
- Không bypass authorization ở endpoint không public.
- Không trả thông tin debug/internal exception ra public UI.
- Error public nên thân thiện, không lộ stack trace, SQL, table/field nội bộ.
- Không đưa API key, secret, .env content vào translation file.
- Không log raw personal data chỉ để debug i18n.
```

Với nội dung chứa dữ liệu cá nhân:

```text
- Không tự gửi nội dung cá nhân sang dịch vụ dịch bên ngoài nếu chưa có policy/consent.
- Không tự động dịch dữ liệu người dùng nhập trong form public.
- Chỉ dịch label/hướng dẫn/lỗi; dữ liệu user nhập giữ nguyên.
```

---

## 16. Kế hoạch triển khai theo phase

## Phase 0 — Audit hiện trạng

Mục tiêu: biết chính xác text cứng và flow đang nằm ở đâu.

Cần làm:

```text
1. Search toàn bộ frontend public pages/components.
2. Liệt kê file public liên quan.
3. Liệt kê text cứng VI/EN.
4. Liệt kê toast đang dùng.
5. Liệt kê validation/error state.
6. Liệt kê API public cần truyền languageCode.
7. Kiểm tra package.json có i18next/react-i18next chưa.
8. Kiểm tra backend đã có bảng translation nào cho news/faq/partner/gallery chưa.
```

Không sửa code ở phase này ngoài ghi chú/audit nếu chưa chắc.

## Phase 1 — i18n foundation

Cần làm:

```text
1. Cài/chuẩn hóa i18next nếu cần.
2. Tạo shared/i18n.
3. Tạo locale JSON vi/en.
4. Tạo LanguageProvider hoặc init trong main entry.
5. Tạo helper đổi ngôn ngữ.
6. Tạo helper đọc current language.
7. Tạo API interceptor gắn Accept-Language.
8. Tạo convention cho translation key.
9. Đảm bảo fallback không hiện raw key.
```

## Phase 2 — Public Layout + Header + Footer + Homepage

Cần làm full:

```text
1. Dịch toàn bộ header/footer/homepage.
2. Thêm language switcher.
3. Dịch mobile menu.
4. Dịch loading/empty/error của section trên homepage.
5. Kiểm tra responsive EN/VI.
```

## Phase 3 — Public News

Cần làm full:

```text
1. Dịch UI list/detail/search/filter.
2. Truyền languageCode vào API list/detail.
3. Backend trả content theo languageCode nếu đã có translation model.
4. Fallback content nếu thiếu bản dịch.
5. Dịch loading/empty/error/toast.
6. Kiểm tra published-only.
```

## Phase 4 — Public Partners

Cần làm full:

```text
1. Dịch UI list/detail/search/filter.
2. Truyền languageCode vào API.
3. Xử lý proper noun không dịch sai.
4. Backend trả description/collaboration/location theo languageCode nếu có dữ liệu.
5. Dịch loading/empty/error/toast.
```

## Phase 5 — Public FAQ

Cần làm full:

```text
1. Dịch UI FAQ.
2. Dịch FAQ type label bằng key.
3. Không đổi enum value.
4. Truyền languageCode vào API nếu có FAQ translation.
5. Dịch question/answer theo DB nếu có translation table.
6. Dịch empty/search/filter/loading/error.
```

## Phase 6 — Public Gallery / Visit FPTU

Cần làm full:

```text
1. Dịch UI gallery.
2. Truyền languageCode vào API.
3. Xử lý area/location/item/media theo languageCode.
4. Xử lý audio/TTS theo languageCode.
5. Dịch trạng thái audio unavailable.
6. Kiểm tra không phát nhầm audio VI ở EN nếu không có fallback rõ.
```

## Phase 7 — Public Visit Request Form

Cần làm full:

```text
1. Dịch label/placeholder/helper.
2. Dịch validation.
3. Dịch toast.
4. Dịch API error.
5. Không dịch dữ liệu user nhập.
6. Kiểm tra submit fail không mất dữ liệu form.
```

## Phase 8 — Global cleanup

Cần làm:

```text
1. Search lại raw Vietnamese/English hardcoded trong public scope.
2. Search raw translation key trên UI runtime.
3. Kiểm tra missing keys VI/EN.
4. Build frontend.
5. Typecheck/lint nếu có.
6. Manual test toàn bộ public flow.
```

---

## 17. Audit commands gợi ý

Chạy trong `frontend/pems-react`.

Tìm text tiếng Việt trong source:

```bash
rg -n "[ÀÁÂÃÈÉÊÌÍÒÓÔÕÙÚĂĐĨŨƠàáâãèéêìíòóôõùúăđĩũơƯĂẠ-ỹ]" src
```

Tìm toast:

```bash
rg -n "toast\.|enqueueSnackbar|message\.|notification\." src
```

Tìm placeholder/title/aria/alt hardcoded:

```bash
rg -n "placeholder=|title=|aria-label=|alt=" src
```

Tìm text trong JSX dạng chuỗi trực tiếp:

```bash
rg -n ">[^<{]*[A-Za-zÀ-ỹ][^<{]*<" src/pages src/components src/features
```

Tìm raw key khả nghi sau khi build/runtime có thể chưa map:

```bash
rg -n "t\(['\"](home|news|partners|faq|gallery|visitRequest|publicLayout|common|toast|errors|validation)\." src
```

Các command trên chỉ là gợi ý. Không xóa/sửa tự động nếu chưa review.

---

## 18. Test requirements

### 18.1. Frontend manual test

Với mỗi page public đã sửa, test đủ:

```text
1. Load page ở VI.
2. Đổi sang EN.
3. Reload page, EN vẫn được giữ.
4. Đổi lại VI.
5. Search/filter nếu có.
6. Empty state nếu không có data.
7. Loading state nếu có thể mô phỏng.
8. Error state nếu API fail.
9. Toast success/error nếu có action.
10. Validation form nếu có form.
11. Mobile layout.
12. Không có raw key.
13. Không còn text trộn Anh/Việt.
```

### 18.2. Backend/API test

Nếu sửa backend public API:

```text
1. languageCode=vi trả nội dung VI.
2. languageCode=en trả nội dung EN nếu có.
3. languageCode không hợp lệ fallback an toàn hoặc 400 tùy validator.
4. Thiếu translation fallback không crash.
5. Hidden/draft/internal content không bị lộ.
6. Query/filter/search vẫn đúng sau khi thêm languageCode.
7. Không phá API cũ nếu frontend/admin đang dùng.
```

### 18.3. Build/test commands

Tối thiểu:

```bash
cd frontend/pems-react
npm run build
```

Nếu project có script:

```bash
npm run lint
npm run typecheck
```

Backend nếu có sửa:

```bash
dotnet build
```

Nếu có test project thật sự được wire:

```bash
dotnet test
```

Nếu không chạy được test, phải ghi rõ lý do trong report.

---

## 19. Definition of Done

Một module/page được coi là xong khi:

```text
[ ] Không còn UI text cứng trong scope page/module.
[ ] Toast đã dịch đầy đủ.
[ ] Validation đã dịch đầy đủ.
[ ] Empty/loading/error state đã dịch đầy đủ.
[ ] Modal/dropdown/tooltip/popover đã dịch đầy đủ.
[ ] Badge/status/enum/filter label dùng translation key.
[ ] API error code được map sang i18n message nếu có.
[ ] Dynamic content lấy theo languageCode hoặc fallback rõ ràng.
[ ] Không dịch enum/status/route/API/DTO field kỹ thuật.
[ ] Không phá UI desktop/mobile.
[ ] Không đổi nghiệp vụ ngoài scope.
[ ] Không để raw key hiển thị.
[ ] VI/EN đều đủ key.
[ ] Frontend build pass.
[ ] Backend build pass nếu có sửa backend.
[ ] Có report liệt kê file đã sửa và phần đã kiểm tra.
```

---

## 20. Report format sau khi triển khai

Sau khi code xong, AI Agent phải báo cáo theo format:

```md
# Public i18n VI/EN Implementation Report

## 1. Summary
- Đã triển khai phần nào
- Chưa triển khai phần nào
- Có thay đổi backend/database không

## 2. Files changed
| File | Change |
|---|---|
| ... | ... |

## 3. Pages/modules completed
| Module | UI | Toast | Validation | Error | Empty/Loading | Dynamic content | Mobile checked |
|---|---|---|---|---|---|---|---|
| Header/Footer | Yes | N/A | N/A | Yes | N/A | N/A | Yes |

## 4. Language keys added
- common
- publicLayout
- home
- news
- partners
- faq
- gallery
- visitRequest
- validation
- errors
- toast

## 5. API/backend changes
- Endpoint changed
- languageCode handling
- fallback behavior

## 6. Verification
- npm run build: pass/fail
- npm run lint/typecheck: pass/fail/not available
- dotnet build: pass/fail/not changed
- manual test: pass/fail

## 7. Known limitations
- Missing DB translations
- Fallback behavior
- Module deferred
```

---

## 21. Prompt ngắn để giao cho AI Agent code

Có thể dùng prompt sau sau khi đã đặt file này trong `docs/`:

```text
Bạn là Senior Full-stack Engineer cho PEMS.

Hãy đọc file yêu cầu: docs/[đường_dẫn]/PEMS_PUBLIC_I18N_VI_EN_IMPLEMENTATION_REQUIREMENTS.md và source hiện tại trước khi sửa.

Nhiệm vụ: triển khai i18n Anh/Việt cho các trang public/published theo đúng phase. Cập nhật module nào thì phải xử lý full sâu module đó: UI text cứng, toast, validation, API error, empty/loading/error state, modal/dropdown/tooltip, enum/status label, aria/alt/title. Không để trộn Anh/Việt và không để raw key hiện trên UI.

Không được phá UI, không redesign, không đổi business logic, không đổi route/API/enum/status nếu không bắt buộc. Dynamic DB content phải lấy theo languageCode hoặc fallback rõ ràng. Frontend dịch UI tĩnh, backend/database xử lý content động.

Trước khi sửa, audit source thật. Sau khi sửa, chạy build/test phù hợp và báo cáo theo format trong file yêu cầu.
```

---

## 22. Ghi chú triển khai thực tế

Ưu tiên làm theo thứ tự nhỏ và chắc:

```text
1. Foundation + Header/Footer/Homepage
2. News
3. Partners
4. FAQ
5. Gallery
6. Visit Request Form
7. Global cleanup
```

Không nên làm toàn bộ public area trong một lần commit lớn nếu project đang có nhiều text cứng. Làm từng module, mỗi module pass checklist rồi mới chuyển sang module tiếp theo.
