# PEMS — Kế hoạch triển khai hoàn thiện full module News

## 1. Mục tiêu tài liệu

Tài liệu này dùng để giao cho AI/code agent hoặc developer đọc full source dự án PEMS và triển khai hoàn thiện toàn bộ module **News**: xem danh sách public, xem chi tiết, dashboard quản lý, viết bài, chỉnh sửa, upload ảnh Google Drive, duyệt/từ chối, ẩn/hiện, đa ngôn ngữ, dịch tự động, validate, phân quyền, toast, test và build.

Nguyên tắc quan trọng:

> **Không phá hoàn toàn giao diện cũ. Chỉ bổ sung, chỉnh sửa, cải thiện UI khi cần để hoàn thiện nghiệp vụ, sửa lỗi, tăng tính chuyên nghiệp và không làm vỡ trải nghiệm hiện tại.**

---

## 2. Bối cảnh dự án

Dự án PEMS dùng:

- Backend: ASP.NET Core .NET 8, Clean Architecture, MediatR, EF Core/Pomelo MySQL.
- Frontend: React, Vite, TypeScript, Tailwind.
- Database: MySQL fresh-create/manual SQL.
- File storage: Google Drive service hiện tại, lưu metadata vào bảng `files`.
- Authorization: dùng role cố định, không dùng dynamic permissions.

Các role liên quan đến News:

| Role | Vai trò với News |
|---|---|
| Public / Visitor chưa login | Chỉ xem bài `PUBLISHED` |
| VISITOR login | Chỉ xem public news đã `PUBLISHED` |
| STAFF Staff | Tạo/sửa bài theo rule nghiệp vụ, thường là bài của mình hoặc bài liên quan chuyến mình phụ trách |
| STUDENT | Có thể tạo bài nếu được phân quyền theo rule dự án, đặc biệt khi tham gia hỗ trợ chuyến |
| STAFF Leader | Quản lý, duyệt, từ chối, ẩn/hiện bài thuộc campus của mình |
| HO | Nếu có màn hình News thì ưu tiên read-only/overview, không tự ý tham gia duyệt nếu rule không quy định |
| ADMIN | Admin kỹ thuật, không nên tham gia workflow nghiệp vụ News nếu dự án đã tách rõ |

Các bảng News hiện tại:

| Bảng | Vai trò |
|---|---|
| `news` | Metadata bài viết, trạng thái, tác giả, campus, visit instance, cover image |
| `news_translations` | Tiêu đề, summary, slug, SEO theo ngôn ngữ |
| `news_content_sections` | Các section/mục nội dung của bài |
| `news_section_files` | Ảnh/file gắn với từng section |
| `files` | Metadata file/ảnh lưu trên Google Drive |

News status chuẩn:

```text
PENDING_REVIEW
REJECTED
PUBLISHED
HIDDEN
```

Rule đóng đoàn liên quan News:

```text
Visit instance chỉ được đóng khi:
1. Đã có ít nhất một bài news PUBLISHED liên kết với visit_instance_id
   hoặc
2. Host xác nhận chuyến này không yêu cầu tin tức: news_not_required / confirmNoNews
```

---

## 3. Phạm vi triển khai full module News

Cần hoàn thiện đủ các nhóm sau:

| Nhóm | Nội dung |
|---|---|
| Public News | Trang danh sách tin tức, chi tiết tin tức, chỉ hiển thị bài đã publish |
| Dashboard News | Danh sách nội bộ, lọc/search/sort/pagination, xem chi tiết |
| Create News | Viết bài mới, chọn visit instance, nhập title/summary/section/ảnh |
| Edit News | Chỉnh sửa bài, section, ảnh bìa, ảnh trong bài |
| News Editor | Rich text, bold/italic/list/quote/link, nhiều section, preview |
| Image Upload | Upload Drive, không lưu base64, lưu metadata vào `files`, mapping `news_section_files` |
| Image Layout | Căn trái/giữa/phải, size nhỏ/vừa/lớn/full, caption, gallery grid |
| Review Workflow | Approve, reject with reason, publish, hide, unhide |
| Multi-language | Một bài có nhiều bản dịch theo `language_code` |
| Auto Translation | Chọn ngôn ngữ, bấm dịch, tạo bản copy mới |
| Close Delegation | Checkbox “không yêu cầu tin tức” khi đóng đoàn |
| Validation | Frontend + Backend validation đầy đủ |
| Authorization | Check role/campus/owner/status đúng rule |
| Toast | Success/error/loading thống nhất, không mount Toaster lặp |
| Test/Build | Backend build, frontend lint/build, test manual/API/UI/role |

---

## 4. Nguyên tắc triển khai

### 4.1. Không phá UI cũ

Khi sửa UI:

- Không rewrite toàn bộ page nếu UI hiện tại đã dùng được.
- Giữ layout, spacing, style, màu sắc, route, component hiện tại nếu không lỗi.
- Chỉ bổ sung phần thiếu: loading, error, empty state, upload ảnh, language switch, action button, validation message.
- Chỉ refactor khi cần để sửa bug hoặc tránh duplicate code nghiêm trọng.
- Không đổi navigation/menu nếu không cần.
- Không làm mất chức năng cũ đang chạy tốt.

### 4.2. Không phá schema nếu chưa bắt buộc

- Không đổi database schema nếu các bảng hiện tại đã đáp ứng.
- Không tạo migration mới khi chưa được yêu cầu.
- Không đổi tên bảng/cột.
- Không đổi enum status.
- Không xóa seed/data.

### 4.3. Không lưu ảnh base64 vào database

Tuyệt đối không lưu:

```html
<img src="data:image/png;base64,..." />
```

Không để payload chứa:

```text
data:image/
base64
```

Ảnh phải đi theo luồng:

```text
Frontend upload multipart/form-data
→ Backend upload Google Drive
→ Lưu metadata vào files
→ Trả fileId/url/thumbnailUrl
→ Gắn fileId vào sectionFiles
→ Lưu mapping vào news_section_files
```

### 4.4. Không dùng mock ở public news

Trang `/news` phải gọi API thật, không render từ `allArticles`, fake data, mock array.

### 4.5. Không bỏ sanitize HTML

Public detail render HTML phải sanitize để tránh XSS.

### 4.6. Không mount Toaster nhiều nơi

Chỉ mount `<Toaster />` một lần ở root App. Các component chỉ gọi `toast.success`, `toast.error`, `toast.loading`, không tự mount thêm Toaster riêng.

---

## 5. Phase 0 — Audit hiện trạng trước khi sửa

Trước khi code, cần quét toàn bộ source để biết phần nào đã có, phần nào lỗi.

### 5.1. Backend cần kiểm tra

- `backend/PEMS.Api/Controllers/NewsController.cs`
- `backend/PEMS.Api/Controllers/PublicContentController.cs`
- `backend/PEMS.Application/News/**`
- `backend/PEMS.Application/Delegations/News/**`
- `backend/PEMS.Application/PublicContent/**`
- `backend/PEMS.Domain/Entities/**News**`
- `backend/PEMS.Domain/Enums/**News**`
- `backend/PEMS.Infrastructure/Persistence/Configurations/**News**`
- `ApplicationDbContext`
- File/Google Drive services
- Complete visit/close delegation handler

### 5.2. Frontend cần kiểm tra

- `frontend/pems-react/src/pages/public/news/**`
- `frontend/pems-react/src/pages/dashboard/news/**`
- `frontend/pems-react/src/features/news/**`
- `frontend/pems-react/src/shared/api/publicContentApi.ts`
- `frontend/pems-react/src/shared/api/newsApi.ts` hoặc file tương tự
- `frontend/pems-react/src/shared/api/httpClient.ts`
- Upload helper hiện có
- App routes/constants
- Visit process/close delegation modal

### 5.3. Từ khóa cần search

```text
allArticles
mock
fake
readAsDataURL
data:image
base64
sectionImageSrc
sectionBodyHtml
sectionFiles: []
<Toaster
public/news
/news
/news/:id
CompleteVisitStage
confirmNoNews
news_not_required
```

### 5.4. Báo cáo audit cần có

| Hạng mục | Trạng thái | File/method | Ghi chú |
|---|---|---|---|
| Public list | DONE/PARTIAL/MISSING/BROKEN | ... | ... |
| Public detail | DONE/PARTIAL/MISSING/BROKEN | ... | ... |
| Dashboard list | DONE/PARTIAL/MISSING/BROKEN | ... | ... |
| Create/Edit | DONE/PARTIAL/MISSING/BROKEN | ... | ... |
| Upload cover | DONE/PARTIAL/MISSING/BROKEN | ... | ... |
| Upload section image | DONE/PARTIAL/MISSING/BROKEN | ... | ... |
| Approve/Reject | DONE/PARTIAL/MISSING/BROKEN | ... | ... |
| Hide/Unhide | DONE/PARTIAL/MISSING/BROKEN | ... | ... |
| Multi-language | DONE/PARTIAL/MISSING/BROKEN | ... | ... |
| Auto translation | DONE/PARTIAL/MISSING/BROKEN | ... | ... |
| Close delegation news rule | DONE/PARTIAL/MISSING/BROKEN | ... | ... |

---

## 6. Phase 1 — Chuẩn hóa API contract và DTO

Mục tiêu: thống nhất request/response để frontend/backend không lệch.

### 6.1. Public News List API

Endpoint đề xuất hoặc kiểm tra endpoint hiện có:

```http
GET /api/public/news?pageIndex=1&pageSize=6&languageCode=vi&keyword=&isFeatured=
```

Response nên nhẹ, không trả full content:

```json
{
  "items": [
    {
      "newsId": 1,
      "title": "Cất nóc Tổ hợp Giáo dục Bình Dương",
      "slug": "cat-noc-to-hop-giao-duc-binh-duong",
      "summary": "Ngày 3/7/2026...",
      "coverFileId": 125,
      "coverUrl": "/api/files/125/view",
      "thumbnailUrl": "/api/files/125/thumbnail",
      "publishedAt": "2026-07-03T00:00:00",
      "languageCode": "vi",
      "isFeatured": true
    }
  ],
  "pageIndex": 1,
  "pageSize": 6,
  "totalItems": 20,
  "totalPages": 4
}
```

Backend rule:

- Chỉ query `status = PUBLISHED`.
- Không trả `PENDING_REVIEW`, `REJECTED`, `HIDDEN`.
- Nếu có `languageCode`, ưu tiên bản dịch tương ứng.
- Nếu không có bản dịch target language, có thể fallback sang `vi` hoặc trả empty tùy rule dự án.

### 6.2. Public News Detail API

Endpoint:

```http
GET /api/public/news/{newsId}?languageCode=vi
```

Response:

```json
{
  "newsId": 1,
  "status": "PUBLISHED",
  "languageCode": "vi",
  "title": "Cất nóc Tổ hợp Giáo dục Bình Dương",
  "slug": "cat-noc-to-hop-giao-duc-binh-duong",
  "summary": "Ngày 3/7/2026...",
  "coverUrl": "/api/files/125/view",
  "publishedAt": "2026-07-03T00:00:00",
  "availableLanguages": ["vi", "en"],
  "sections": [
    {
      "sectionId": 10,
      "sectionOrder": 1,
      "sectionTitle": "Lễ cất nóc dự án",
      "sectionBodyHtml": "<p><strong>Ngày 3/7/2026</strong>...</p>",
      "files": [
        {
          "fileId": 201,
          "url": "/api/files/201/view",
          "thumbnailUrl": "/api/files/201/thumbnail",
          "caption": "Toàn cảnh buổi lễ",
          "altText": "Toàn cảnh buổi lễ",
          "layout": "single",
          "align": "center",
          "size": "large",
          "displayOrder": 1
        }
      ]
    }
  ]
}
```

Backend rule:

- Nếu bài không `PUBLISHED`, public API trả 404 hoặc forbidden-safe response.
- Không để lộ review note, internal metadata không cần thiết.
- Chỉ trả HTML đã lưu, frontend sanitize trước khi render.

### 6.3. Internal News List API

Endpoint:

```http
GET /api/news?pageIndex=1&pageSize=10&status=&keyword=&languageCode=&visitInstanceId=&sortBy=createdAt&sortDirection=desc
```

Response:

```json
{
  "items": [
    {
      "newsId": 1,
      "title": "...",
      "summary": "...",
      "status": "PENDING_REVIEW",
      "languageCode": "vi",
      "coverUrl": "/api/files/125/view",
      "createdByName": "...",
      "createdAt": "...",
      "reviewedByName": "...",
      "reviewedAt": "...",
      "publishedAt": "...",
      "visitInstanceId": 1001,
      "campusId": 1
    }
  ],
  "pageIndex": 1,
  "pageSize": 10,
  "totalItems": 100
}
```

### 6.4. Create/Edit payload

Request:

```json
{
  "visitInstanceId": 1001,
  "languageCode": "vi",
  "title": "Cất nóc Tổ hợp Giáo dục Bình Dương",
  "summary": "Ngày 3/7/2026...",
  "seoTitle": "...",
  "seoDescription": "...",
  "coverFileId": 125,
  "sections": [
    {
      "sectionOrder": 1,
      "sectionTitle": "Lễ cất nóc dự án",
      "sectionBodyHtml": "<p><strong>Ngày 3/7/2026</strong>...</p>",
      "sectionFiles": [
        {
          "fileId": 201,
          "displayOrder": 1,
          "caption": "Toàn cảnh buổi lễ",
          "altText": "Toàn cảnh buổi lễ",
          "layout": "single",
          "align": "center",
          "size": "large"
        }
      ]
    }
  ],
  "rowVersion": "..."
}
```

Không được có base64 trong `sectionBodyHtml`.

---

## 7. Phase 2 — Hoàn thiện Public News

### 7.1. Trang `/news`

Yêu cầu:

- Bỏ mock array `allArticles` hoặc bất kỳ fake data nào đang dùng để render.
- Gọi API thật `GET /api/public/news`.
- Giữ layout hiện tại nếu đã đẹp.
- Bổ sung state nếu thiếu:
  - loading skeleton/spinner
  - error state
  - empty state
  - pagination
  - search nếu UI hiện tại có
  - language filter nếu đã hỗ trợ đa ngôn ngữ

Acceptance Criteria:

```text
- Vào /news hiển thị bài thật trong DB.
- Chỉ bài PUBLISHED xuất hiện.
- Bài HIDDEN, REJECTED, PENDING_REVIEW không xuất hiện.
- Click card chuyển sang chi tiết đúng.
- Không còn render từ hardcode allArticles.
- API lỗi không làm trắng màn hình.
```

### 7.2. Trang `/news/:id`

Yêu cầu:

- Gọi API public detail thật.
- Render cover, title, published date, summary, sections, images.
- Sanitize HTML trước khi `dangerouslySetInnerHTML`.
- CSS responsive:
  - ảnh không tràn container
  - text không bị ngắt chữ xấu
  - list/blockquote/heading hiển thị đúng
- Nếu bài không tồn tại hoặc không `PUBLISHED`, hiển thị 404/empty state đẹp.

Các tag HTML cần render ổn:

```text
p, br, strong, em, u
h2, h3
ul, ol, li
blockquote
a
figure, figcaption
img
div
```

---

## 8. Phase 3 — Hoàn thiện Dashboard News Management

### 8.1. Danh sách quản lý News

Route ví dụ:

```text
/dashboard/news
```

Yêu cầu:

| Thành phần | Yêu cầu |
|---|---|
| Search | Tìm theo title/summary/creator |
| Filter status | PENDING_REVIEW, REJECTED, PUBLISHED, HIDDEN |
| Filter language | vi/en/ja/ko/zh nếu có |
| Filter visit | Bài có/không có visit instance |
| Pagination | Không load tất cả |
| Sort | CreatedAt, PublishedAt, UpdatedAt |
| Status badge | Màu rõ, dễ hiểu |
| Action | View/Edit/Approve/Reject/Hide/Unhide tùy role/status |
| Empty state | Có |
| Loading/Error | Có |
| Toast | Có, không trùng |

Không phá UI table/card cũ nếu đang dùng tốt. Chỉ cải thiện phần thiếu.

### 8.2. Chi tiết internal News

Route ví dụ:

```text
/dashboard/news/:id
```

Cần hiển thị:

- Title
- Summary
- Cover
- Status
- Language
- Author
- Created date
- Updated date
- Review info
- Review note nếu bị reject
- Visit instance link nếu có
- Sections
- Images/files
- Available translations
- Action buttons theo role/status

Action buttons:

| Action | Khi nào hiện |
|---|---|
| Edit | Người có quyền sửa, status cho phép sửa |
| Approve | Staff Leader, bài PENDING_REVIEW, đúng campus |
| Reject | Staff Leader, bài PENDING_REVIEW, đúng campus |
| Hide | Staff Leader, bài PUBLISHED, đúng campus |
| Unhide | Staff Leader, bài HIDDEN, đúng campus |
| Translate | Người có quyền quản lý bài |

---

## 9. Phase 4 — Hoàn thiện Create/Edit News Editor

### 9.1. Form chính

Create/Edit cần có:

| Field | Rule |
|---|---|
| Visit Instance | Optional hoặc required tùy flow tạo bài từ chuyến |
| Language Code | Required, mặc định `vi` |
| Title | Required, max length theo backend/schema |
| Summary | Required hoặc strongly recommended |
| SEO Title | Optional |
| SEO Description | Optional |
| Cover Image | Required hoặc strongly recommended |
| Sections | Ít nhất 1 section |
| Section Title | Required hoặc optional tùy thiết kế, nhưng nên có |
| Section Body | Required, không được chỉ toàn HTML rỗng |
| Section Images | Optional |

### 9.2. Rich text editor

Nên hỗ trợ:

```text
Bold
Italic
Underline nếu có
Heading 2 / Heading 3
Paragraph
Bullet list
Numbered list
Quote
Link
Image placeholder/insert image
```

Không nên hỗ trợ tự do:

```text
script
iframe
position absolute
style inline nguy hiểm
font-size tùy ý quá nhiều
color tùy ý gây vỡ branding
```

### 9.3. Section management

Cần hỗ trợ:

- Add section
- Remove section
- Reorder section
- Collapse/expand section nếu nhiều section
- Validate từng section
- Preview section hoặc preview toàn bài nếu có thể

### 9.4. Edit behavior

Khi edit:

- Load dữ liệu cũ chính xác.
- Không làm mất cover cũ nếu user không đổi.
- Không làm mất ảnh section cũ nếu user không xóa.
- Thêm ảnh mới không ghi đè nhầm ảnh cũ.
- Xóa ảnh phải cập nhật `news_section_files` đúng.
- Nếu dùng `row_version`, gửi đúng `rowVersion` để tránh update conflict.

---

## 10. Phase 5 — Upload ảnh Google Drive và layout ảnh

### 10.1. Cover image

Luồng:

```text
User chọn cover
→ Frontend upload multipart/form-data
→ Backend upload Google Drive
→ Backend lưu metadata vào files
→ Backend trả fileId + url
→ Frontend set coverFileId
→ Submit news dùng coverFileId
```

Validate:

- Chỉ cho image mime type: jpg, jpeg, png, webp.
- Giới hạn dung lượng, ví dụ 5MB hoặc theo rule project.
- Báo lỗi rõ nếu upload fail.

### 10.2. Section image/file

Không được dùng `FileReader.readAsDataURL()` để lưu base64 vào HTML.

Cần sửa triệt để các đoạn:

```text
readAsDataURL
sectionImageSrc
<img src="data:image/...">
sectionFiles: [] khi đã có ảnh
```

Luồng đúng:

```text
User chọn ảnh trong section
→ Upload Drive qua backend
→ Nhận fileId/url/thumbnailUrl
→ Add vào sectionFiles của section đó
→ Render preview bằng url
→ Khi submit, gửi sectionFiles
→ Backend lưu news_section_files
```

### 10.3. Endpoint upload section file

Nếu backend đã có endpoint, dùng endpoint hiện tại.

Nếu chưa có, thêm endpoint theo convention hiện tại, ví dụ:

```http
POST /api/news/section-file-upload
Content-Type: multipart/form-data
```

Response:

```json
{
  "fileId": 201,
  "url": "/api/files/201/view",
  "thumbnailUrl": "/api/files/201/thumbnail",
  "originalFileName": "event-photo.jpg",
  "mimeType": "image/jpeg",
  "sizeBytes": 1234567
}
```

Backend không được lưu file local folder mới nếu dự án đã dùng Google Drive.

### 10.4. Layout ảnh

Nên hỗ trợ option an toàn:

| Option | Values |
|---|---|
| align | `left`, `center`, `right` |
| size | `small`, `medium`, `large`, `full` |
| layout | `single`, `grid-2`, `grid-3` |
| caption | string |
| altText | string |
| displayOrder | number |

Không cho nhập CSS tự do.

Ví dụ render:

```html
<figure class="news-image align-center size-large">
  <img src="/api/files/201/view" data-file-id="201" alt="Toàn cảnh buổi lễ" />
  <figcaption>Toàn cảnh buổi lễ</figcaption>
</figure>
```

Gallery:

```html
<div class="news-gallery grid-2">
  <figure>
    <img src="/api/files/201/view" data-file-id="201" alt="Ảnh 1" />
    <figcaption>Ảnh 1</figcaption>
  </figure>
  <figure>
    <img src="/api/files/202/view" data-file-id="202" alt="Ảnh 2" />
    <figcaption>Ảnh 2</figcaption>
  </figure>
</div>
```

Nếu schema chưa có field caption/layout/align/size trong `news_section_files`, có 2 hướng:

1. Tạm lưu layout/caption trong HTML an toàn.
2. Nếu bắt buộc cần quản trị chuyên nghiệp hơn, đề xuất schema change riêng nhưng không tự ý thực hiện nếu chưa được duyệt.

---

## 11. Phase 6 — Review, Approve, Reject, Publish, Hide

### 11.1. Workflow chuẩn

```text
Create/Edit
→ PENDING_REVIEW
→ Staff Leader review
→ Approve: PUBLISHED
→ Reject: REJECTED + review_note
→ Hide: HIDDEN
→ Unhide: PUBLISHED
```

### 11.2. Validate backend

| Action | Validate |
|---|---|
| Approve | Chỉ Staff Leader đúng campus, status phải PENDING_REVIEW |
| Reject | Chỉ Staff Leader đúng campus, status phải PENDING_REVIEW, reason required |
| Hide | Chỉ Staff Leader đúng campus, status phải PUBLISHED |
| Unhide | Chỉ Staff Leader đúng campus, status phải HIDDEN |
| Public detail | Chỉ status PUBLISHED |
| Edit | Đúng người/đúng role/đúng status theo rule |
| Create from visit | Visit instance hợp lệ, user có quyền với visit đó |

### 11.3. Validate frontend

- Reject popup bắt buộc nhập lý do.
- Confirm trước khi hide/unhide.
- Toast loading/success/error.
- Disable button khi đang submit.
- Không hiện action nếu user không có quyền.
- Nếu backend trả 403/409/400, hiện message rõ.

### 11.4. Toast message đề xuất

| Action | Success | Error |
|---|---|---|
| Create | `Tạo bài viết thành công` | `Không thể tạo bài viết` |
| Edit | `Cập nhật bài viết thành công` | `Không thể cập nhật bài viết` |
| Upload cover | `Tải ảnh bìa thành công` | `Không thể tải ảnh bìa` |
| Upload section image | `Tải ảnh nội dung thành công` | `Không thể tải ảnh nội dung` |
| Approve | `Đã duyệt và xuất bản bài viết` | `Không thể duyệt bài viết` |
| Reject | `Đã từ chối bài viết` | `Không thể từ chối bài viết` |
| Hide | `Đã ẩn bài viết` | `Không thể ẩn bài viết` |
| Unhide | `Đã hiển thị lại bài viết` | `Không thể hiển thị lại bài viết` |
| Translate | `Dịch bài viết thành công` | `Không thể dịch bài viết` |

---

## 12. Phase 7 — Multi-language thủ công

### 12.1. Mục tiêu

Một bài `news` có thể có nhiều bản dịch trong `news_translations`:

```text
news_id = 1
- vi
- en
- ja
- ko
```

Mỗi bản dịch có sections riêng.

### 12.2. Dashboard cần có

Trong detail/edit:

- Hiển thị danh sách bản dịch hiện có.
- Cho chọn language để xem/sửa.
- Cho tạo bản dịch thủ công.
- Không tạo trùng `(news_id, language_code)`.
- Nếu language đã tồn tại thì báo lỗi hoặc chuyển sang edit bản đó.

### 12.3. Public cần có

- Public detail hiển thị language switch nếu có nhiều bản dịch.
- Nếu user chọn `en` nhưng chưa có bản `en`, fallback rõ ràng hoặc báo chưa có bản dịch.
- URL có thể dùng query:

```text
/news/123?lang=en
```

hoặc route nếu dự án muốn:

```text
/en/news/123
```

Không bắt buộc đổi route lớn nếu hiện tại chưa có i18n routing.

---

## 13. Phase 8 — Auto Translation API

### 13.1. Provider đề xuất

Ưu tiên:

```text
Google Cloud Translation Advanced + Glossary
```

Lý do:

- Phù hợp hệ sinh thái Google đang dùng Drive.
- Có glossary để giữ thuật ngữ FPT/PEMS/campus/phòng ban/chức danh.
- Dễ cấu hình theo project Google Cloud.

Có thể bổ sung sau:

```text
OpenAI API để polish văn phong truyền thông hoặc dịch theo JSON schema thông minh hơn.
```

### 13.2. Cấu hình backend

Không hardcode key trong code. Dùng appsettings, environment variables hoặc API configuration module hiện tại.

Ví dụ:

```json
{
  "Translation": {
    "Provider": "GoogleCloudTranslation",
    "DefaultSourceLanguage": "vi",
    "AllowedTargetLanguages": ["en", "ja", "ko", "zh-CN"],
    "Google": {
      "ProjectId": "your-project-id",
      "Location": "global",
      "CredentialsPath": "service-account.json",
      "GlossaryId": "pems-glossary"
    },
    "OpenAI": {
      "Model": "gpt-5.5-mini",
      "ApiKeySecretRef": "OPENAI_API_KEY",
      "EnablePolish": false
    }
  }
}
```

### 13.3. Service abstraction

Tạo interface:

```csharp
public interface INewsTranslationService
{
    Task<TranslatedNewsDto> TranslateNewsAsync(
        NewsTranslationSourceDto source,
        string targetLanguage,
        CancellationToken cancellationToken);
}
```

Implementation:

```text
GoogleNewsTranslationService
OpenAiNewsTranslationService, optional
```

### 13.4. API auto translate

Endpoint đề xuất:

```http
POST /api/news/{newsId}/translations/auto-translate
```

Payload:

```json
{
  "sourceLanguage": "vi",
  "targetLanguage": "en",
  "provider": "GoogleCloudTranslation",
  "useGlossary": true,
  "saveAsDraft": true
}
```

Response:

```json
{
  "newsId": 1,
  "languageCode": "en",
  "title": "Topping-out ceremony for FPT Binh Duong Education Complex...",
  "summary": "...",
  "sections": [
    {
      "sectionOrder": 1,
      "sectionTitle": "Project topping-out ceremony",
      "sectionBodyHtml": "<p>...</p>",
      "copiedFileIds": [201, 202]
    }
  ],
  "createdTranslationId": 15
}
```

### 13.5. Dịch HTML đúng cách

Không gửi HTML lẫn ảnh lộn xộn nếu không kiểm soát. Cần:

1. Parse `sectionBodyHtml`.
2. Tách text nodes cần dịch.
3. Giữ nguyên tag HTML an toàn.
4. Không dịch URL, file id, class, data attributes.
5. Ghép lại HTML sau khi dịch.
6. Sanitize lại trước khi lưu/render.

### 13.6. Copy ảnh sang bản dịch

Khi tạo bản dịch:

```text
Bản gốc section 1 có fileIds: 201, 202
→ Bản dịch section 1 copy mapping fileIds: 201, 202
```

Không upload lại ảnh nếu không cần.

### 13.7. UI dịch

Trong detail/edit có nút:

```text
Dịch bài viết
```

Modal gồm:

| Field | Rule |
|---|---|
| Source Language | Chọn từ bản dịch hiện có |
| Target Language | Không được trùng language đã có |
| Provider | Google mặc định |
| Use Glossary | Mặc định bật nếu có glossary |
| Preview | Hiển thị kết quả trước khi lưu nếu có thể |
| Save Translation | Lưu bản dịch vào DB |

Không tự publish bản dịch ngay sau khi dịch. Bản dịch nên đi qua review nếu rule hiện tại yêu cầu.

---

## 14. Phase 9 — Close Delegation + News

### 14.1. Backend rule

Giữ rule hiện tại:

```text
Không cho close nếu:
- không có news PUBLISHED theo visit_instance_id
- và confirmNoNews/news_not_required không true
```

### 14.2. Frontend UI

Trong modal xác nhận đóng đoàn, thêm checkbox:

```text
[ ] Chuyến này không yêu cầu tin tức
```

Khi tick checkbox, payload gửi:

```json
{
  "confirmNoNews": true
}
```

hoặc đúng field name backend hiện có.

### 14.3. Validate

| Case | Kết quả |
|---|---|
| Có news PUBLISHED | Đóng đoàn được |
| Không có news, tick không cần tin | Đóng đoàn được |
| Không có news, không tick | Không cho đóng |
| Có news PENDING_REVIEW | Chưa đủ điều kiện |
| Có news HIDDEN | Chưa đủ điều kiện |

Thông báo lỗi nên rõ:

```text
Bạn cần tạo và xuất bản tin tức cho chuyến này hoặc xác nhận chuyến này không yêu cầu tin tức.
```

---

## 15. Validation chi tiết

### 15.1. Frontend validation

| Field | Rule |
|---|---|
| Title | Required, không toàn khoảng trắng, giới hạn độ dài |
| Summary | Required/khuyến nghị, giới hạn độ dài |
| Language Code | Required |
| Cover Image | Required hoặc warning tùy rule |
| Sections | Tối thiểu 1 section |
| Section body | Không được rỗng sau khi strip HTML |
| Section image | Đúng mime type, đúng size |
| Reject reason | Required |
| Target language | Required, không trùng bản dịch đã có |
| Translation provider | Required khi auto translate |

### 15.2. Backend validation

Backend phải validate lại toàn bộ, không tin frontend:

| Nhóm | Rule |
|---|---|
| Required field | Title/language/sections/body |
| Length | Theo schema và DTO |
| Status transition | Không cho transition sai |
| Authorization | Role/campus/owner |
| File | FileId tồn tại, đúng type, user có quyền dùng |
| Visit instance | Tồn tại, user có quyền, trạng thái hợp lệ |
| Translation | Không trùng `(news_id, language_code)` |
| HTML | Sanitize hoặc reject tag nguy hiểm |
| Concurrency | Check rowVersion nếu có |

### 15.3. HTML validation

Không cho tag/attribute nguy hiểm:

```text
script
iframe
object
embed
onerror
onclick
javascript:
style nguy hiểm
```

Cho phép tag an toàn:

```text
p, br, strong, em, u
h2, h3
ul, ol, li
blockquote
a
figure, figcaption
img
div
```

---

## 16. Authorization chi tiết

### 16.1. Public

- `GET /api/public/news`: AllowAnonymous.
- `GET /api/public/news/{id}`: AllowAnonymous.
- Chỉ trả bài `PUBLISHED`.

### 16.2. Staff/Student creator

Có thể:

- Tạo bài nếu rule dự án cho phép.
- Xem bài mình tạo.
- Sửa bài mình tạo nếu status cho phép.
- Không approve/reject/hide/unhide.

Không được:

- Duyệt bài của mình.
- Xem/sửa bài ngoài scope nếu không có quyền.

### 16.3. Staff Leader

Có thể:

- Xem bài thuộc campus của mình.
- Approve/reject bài thuộc campus.
- Hide/unhide bài thuộc campus.
- Xem review history nếu có.

Không được:

- Quản lý bài campus khác.
- Bỏ qua reason khi reject.

### 16.4. HO/Admin

Theo rule dự án:

- HO nên read-only hoặc không tham gia workflow News nếu nghiệp vụ không quy định.
- Admin không nên tham gia workflow nghiệp vụ.

Nếu code hiện tại khác, cần báo cáo trước khi sửa.

---

## 17. Toast và UX

### 17.1. Toaster

- Chỉ mount Toaster một lần tại `App.tsx`.
- Xóa Toaster lặp trong:
  - `CreateNews.tsx`
  - `EditNews.tsx`
  - `NewsDetailDashboard.tsx`
  - các page News khác nếu có.

### 17.2. Toast pattern

Dùng pattern:

```text
toast.loading → toast.success / toast.error
```

Hoặc helper toast chung nếu dự án đã có.

### 17.3. Error message

Không hiện lỗi raw khó hiểu nếu có thể map:

| Backend error | Message UI |
|---|---|
| 400 validation | Hiển thị field lỗi |
| 403 | Bạn không có quyền thực hiện thao tác này |
| 404 | Không tìm thấy bài viết |
| 409 conflict | Dữ liệu đã thay đổi hoặc trạng thái không hợp lệ |
| Upload fail | Không thể tải ảnh lên, vui lòng thử lại |
| Translation config missing | Chưa cấu hình API dịch ngôn ngữ |

---

## 18. Test plan

### 18.1. Backend build

Chạy:

```bash
dotnet build
```

Nếu có solution file:

```bash
dotnet build <solution-name>.sln
```

### 18.2. Frontend build

Kiểm tra script trong `package.json`, sau đó chạy:

```bash
npm run lint
npm run build
```

Nếu có typecheck riêng:

```bash
npm run type-check
```

### 18.3. API test cases

| ID | Case | Expected |
|---|---|---|
| NEWS-API-001 | Public list | Chỉ trả PUBLISHED |
| NEWS-API-002 | Public detail PUBLISHED | 200 |
| NEWS-API-003 | Public detail PENDING | 404 |
| NEWS-API-004 | Create valid news | 200/201 |
| NEWS-API-005 | Create thiếu title | 400 |
| NEWS-API-006 | Edit valid news | 200 |
| NEWS-API-007 | Upload cover image | Lưu Drive + files |
| NEWS-API-008 | Upload section image | Lưu Drive + files |
| NEWS-API-009 | Approve đúng role | PUBLISHED |
| NEWS-API-010 | Reject thiếu reason | 400 |
| NEWS-API-011 | Hide published | HIDDEN |
| NEWS-API-012 | Unhide hidden | PUBLISHED |
| NEWS-API-013 | Auto translate valid | Tạo translation mới |
| NEWS-API-014 | Auto translate trùng language | 409/400 |
| NEWS-API-015 | Close visit thiếu news/no confirm | 409 |
| NEWS-API-016 | Close visit confirmNoNews true | Success |

### 18.4. UI manual test cases

| ID | Case | Expected |
|---|---|---|
| NEWS-UI-001 | Vào `/news` | Hiển thị bài thật |
| NEWS-UI-002 | Không có bài published | Empty state |
| NEWS-UI-003 | API public lỗi | Error state |
| NEWS-UI-004 | Vào detail bài published | Render đầy đủ |
| NEWS-UI-005 | Render bold/italic/list/quote | Không vỡ layout |
| NEWS-UI-006 | Tạo bài với cover | Upload và save thành công |
| NEWS-UI-007 | Tạo bài với section image | Không có base64 trong payload |
| NEWS-UI-008 | Edit bài giữ ảnh cũ | Không mất ảnh |
| NEWS-UI-009 | Approve bài | Public thấy bài |
| NEWS-UI-010 | Hide bài | Public không thấy |
| NEWS-UI-011 | Reject bài | Có reason |
| NEWS-UI-012 | Dịch bài sang English | Tạo bản EN |
| NEWS-UI-013 | Switch language public detail | Hiển thị bản dịch |
| NEWS-UI-014 | Đóng đoàn thiếu news | Báo lỗi |
| NEWS-UI-015 | Tick không cần news | Đóng đoàn được |

### 18.5. Role test cases

| ID | Role | Case | Expected |
|---|---|---|---|
| NEWS-ROLE-001 | Public | Xem PUBLISHED | Được |
| NEWS-ROLE-002 | Public | Xem HIDDEN | Không được |
| NEWS-ROLE-003 | Staff | Tạo bài | Được nếu rule cho phép |
| NEWS-ROLE-004 | Staff | Approve bài | Không được |
| NEWS-ROLE-005 | Staff Leader | Approve bài cùng campus | Được |
| NEWS-ROLE-006 | Staff Leader | Approve bài campus khác | Không được |
| NEWS-ROLE-007 | HO | Duyệt news nếu rule không cho | Không được |
| NEWS-ROLE-008 | Admin | Workflow nghiệp vụ nếu không quy định | Không được hoặc read-only |

### 18.6. File/storage test cases

| ID | Case | Expected |
|---|---|---|
| FILE-001 | Upload image jpg | Thành công |
| FILE-002 | Upload png/webp | Thành công |
| FILE-003 | Upload file quá size | Báo lỗi |
| FILE-004 | Upload sai mime | Báo lỗi |
| FILE-005 | Section image save | Có record `files` + `news_section_files` |
| FILE-006 | Payload submit | Không có base64 |
| FILE-007 | Public render image | Ảnh hiển thị đúng |

### 18.7. Translation test cases

| ID | Case | Expected |
|---|---|---|
| TRANS-001 | Dịch VI → EN | Tạo bản EN |
| TRANS-002 | Dịch sang language đã tồn tại | Chặn hoặc hỏi overwrite |
| TRANS-003 | Dịch giữ HTML | Tag còn đúng |
| TRANS-004 | Dịch giữ ảnh | Copy file mapping |
| TRANS-005 | API config thiếu | Báo lỗi rõ |
| TRANS-006 | Glossary enabled | Thuật ngữ chính giữ nhất quán |
| TRANS-007 | Translation provider fail | Không tạo bản lỗi nửa chừng |

---

## 19. Gợi ý thứ tự commit

### Commit group 1 — Public News

- Bỏ mock public list.
- Gọi API thật.
- Loading/error/empty.
- Public detail CSS/sanitize nếu cần.

### Commit group 2 — Toast cleanup

- Xóa Toaster lặp.
- Chuẩn hóa toast helper nếu có.

### Commit group 3 — Image upload fix

- Fix không lưu base64.
- Upload section image Drive.
- Lưu `sectionFiles`.
- Render image preview/detail.

### Commit group 4 — Editor improvement

- Validate form.
- Section add/remove/reorder.
- Image layout/caption.
- Preview nếu có.

### Commit group 5 — Review workflow

- Check approve/reject/hide/unhide.
- Role/status validation.
- UI action visibility.

### Commit group 6 — Close delegation news rule

- Checkbox không yêu cầu tin tức.
- Payload `confirmNoNews`.
- Error message rõ.

### Commit group 7 — Multi-language manual

- Language switch.
- Translation list.
- Create/edit translation thủ công.
- Public language switch.

### Commit group 8 — Auto translation

- Translation config.
- Translation service.
- Auto translate endpoint.
- Translate modal.
- Copy image mappings.

### Commit group 9 — Test/build cleanup

- Build backend/frontend.
- Fix lint/type errors.
- Manual test.
- Remove dead mock code.

---

## 20. Prompt triển khai cho AI/code agent

Có thể copy nguyên prompt này để giao cho AI/code agent:

```md
# PROMPT: Triển khai hoàn thiện full module News trong PEMS

Bạn hãy đọc toàn bộ source code hiện tại của dự án PEMS và triển khai hoàn thiện module News theo tài liệu kế hoạch này.

## Nguyên tắc bắt buộc

- Không phá hoàn toàn giao diện cũ.
- Giữ UI hiện tại nếu đang ổn; chỉ bổ sung/cải thiện phần cần thiết.
- Không đổi database schema nếu chưa thật sự bắt buộc.
- Không tạo migration.
- Không lưu base64 ảnh vào DB.
- Không dùng local upload folder mới.
- Ảnh phải upload qua Google Drive service hiện tại và lưu metadata vào bảng files.
- Public chỉ xem bài PUBLISHED.
- Không bỏ sanitize HTML.
- Không mount Toaster lặp trong từng page.
- Không phá workflow approve/reject/publish/hide hiện tại.
- Không thay đổi role rule nếu không liên quan trực tiếp.
- Không commit, không chạy lệnh nguy hiểm.

## Việc cần làm

1. Audit hiện trạng News: backend, frontend, upload, mock, base64, toast, role, close delegation.
2. Sửa Public News List dùng API thật, không dùng mock allArticles.
3. Sửa Public News Detail render HTML/ảnh an toàn, responsive.
4. Hoàn thiện Dashboard News List/Detail với search/filter/pagination/action/status badge.
5. Hoàn thiện Create/Edit News Editor: title, summary, cover, language, sections, rich text, validation.
6. Sửa upload ảnh trong section: không dùng FileReader.readAsDataURL để lưu base64; upload Drive; lưu files + news_section_files.
7. Hỗ trợ layout ảnh an toàn: align, size, single/grid, caption, altText.
8. Kiểm tra approve/reject/hide/unhide đúng role/status/campus.
9. Thêm checkbox “Chuyến này không yêu cầu tin tức” khi đóng đoàn và gửi confirmNoNews.
10. Hoàn thiện multi-language thủ công: list bản dịch, tạo/sửa bản dịch, public language switch.
11. Tích hợp auto translation: Google Cloud Translation Advanced + glossary, optional OpenAI polish nếu có cấu hình.
12. Validate frontend/backend đầy đủ.
13. Chuẩn hóa toast/loading/error.
14. Chạy build/test: dotnet build, npm run lint, npm run build.

## Báo cáo sau khi làm

Trả về báo cáo theo format:

### A. File đã sửa

| File | Nội dung sửa |
|---|---|

### B. Chức năng đã hoàn thiện

| Chức năng | Trạng thái | Ghi chú |
|---|---|---|

### C. API/DTO thay đổi

| API/DTO | Thay đổi |
|---|---|

### D. Validation/Authorization đã kiểm tra

| Nhóm | Kết quả |
|---|---|

### E. Build/Test

| Lệnh/Test | Kết quả |
|---|---|

### F. Phần còn lại/chưa làm được

Ghi rõ file, lý do, và hướng xử lý tiếp theo nếu còn tồn tại.
```

---

## 21. Definition of Done

Module News chỉ được xem là hoàn thiện khi đạt đủ:

```text
1. /news dùng API thật, không mock.
2. /news/:id xem được bài PUBLISHED, không xem được bài hidden/pending/rejected.
3. Dashboard list/detail hoạt động ổn.
4. Create/Edit tạo bài có section, rich text, cover, ảnh section.
5. Không còn base64 trong payload hoặc DB.
6. Ảnh upload lên Google Drive, có metadata trong files.
7. Ảnh section có mapping trong news_section_files.
8. Approve/reject/hide/unhide đúng role/status/campus.
9. Reject bắt buộc reason.
10. Close delegation xử lý đúng news hoặc confirmNoNews.
11. Multi-language xem/tạo/sửa được.
12. Auto translate tạo bản dịch mới, giữ section và ảnh.
13. Toast không trùng, message rõ.
14. Frontend không crash khi API lỗi.
15. Backend build pass.
16. Frontend lint/build pass.
17. Manual test full workflow pass.
```
