# PROMPT — Fix Document Detail Modal Dynamic by Document Owner Type

## Bối cảnh

Bạn là **Senior Full-stack Engineer** cho dự án **PEMS - Partnership Engagement Management System**.

Tôi đang làm màn hình **Quản lý tài liệu** tại route:

```text
/dashboard/documents
```

Hiện tại UI danh sách tài liệu đã có table, search, filter, nút mắt để xem chi tiết. Khi bấm icon mắt, modal chi tiết đang hiển thị metadata cơ bản của document và phần preview file Google Drive.

Tuy nhiên tôi muốn cải tiến phần **xem chi tiết tài liệu** như sau:

> Với mỗi `owner_type` của tài liệu, modal phải hiển thị **ngữ cảnh nghiệp vụ khác nhau**, không chỉ hiển thị chung chung `owner_type`, `status`, `size`, `description`.

Ví dụ:

- Tài liệu loại `VISIT`, `MINUTES`, `LOGISTICS` phải hiển thị thông tin đoàn: tên đoàn, host, thời gian đoàn diễn ra, campus, trạng thái chuyến thăm, nội dung liên quan đến loại document đó.
- Tài liệu loại `PARTNER` phải hiển thị: tên đối tác, quốc gia, mô tả chung, thông tin liên quan đối tác, nội dung loại document đó.
- Tài liệu loại `NEWS` phải hiển thị: tiêu đề tin tức, mô tả/tóm tắt, người tạo, trạng thái bài viết, nội dung loại document đó.
- Tài liệu loại `REPORT` phải hiển thị: kỳ báo cáo theo ngày/tuần/tháng hoặc khoảng thời gian, mô tả báo cáo, nội dung loại document đó.

---

## Schema cần bám sát

### Bảng `documents`

```text
document_id
file_id
owner_type ENUM('GENERAL','VISIT','PARTNER','MINUTES','NEWS','LOGISTICS','REPORT')
owner_id
campus_id
title
description
document_category
status ENUM('DRAFT','PUBLISHED','ARCHIVED')
created_at
created_by
updated_at
updated_by
```

### Bảng `files`

```text
file_id
storage_provider
bucket_name
object_key
original_filename
mime_type
file_size
checksum_sha256
uploaded_by
uploaded_at
external_file_id
web_view_url
download_url
thumbnail_url
file_purpose
```

### Các bảng nghiệp vụ cần resolve theo `owner_type`

Cần search codebase/schema hiện tại để xác định đúng tên cột trước khi code. Không được bịa field nếu không chắc.

Gợi ý mapping nghiệp vụ:

```text
owner_type = VISIT
→ owner_id có thể liên kết visit_requests.visit_request_id hoặc visit_request_campuses.visit_instance_id theo convention hiện tại của codebase.

owner_type = MINUTES
→ owner_id liên kết minutes.<primary_key> theo schema hiện tại.

owner_type = LOGISTICS
→ owner_id liên kết visit_logistics_items.logistics_item_id.

owner_type = PARTNER
→ owner_id liên kết partners.partner_id.

owner_type = NEWS
→ owner_id liên kết news.news_id.

owner_type = REPORT
→ nếu không có bảng reports vật lý thì không tạo bảng mới; hiển thị theo metadata documents hoặc convention report hiện có.

owner_type = GENERAL
→ tài liệu chung, không cần resolve nghiệp vụ phức tạp.
```

Nếu trong codebase hiện tại `owner_id` đang dùng convention khác, phải ưu tiên convention hiện tại. Không tự đổi schema nếu chưa được yêu cầu.

---

## Yêu cầu chính

Khi bấm icon mắt ở table Document Management:

1. Mở modal chi tiết tài liệu.
2. Modal phải hiển thị đủ 3 nhóm thông tin:
   - Thông tin tài liệu + file.
   - Ngữ cảnh nghiệp vụ theo `owner_type`.
   - Preview / mở Google Drive / tải xuống.
3. Với mỗi `owner_type`, phần ngữ cảnh nghiệp vụ phải render layout khác nhau.
4. Không hiển thị JSON raw cho người dùng.
5. Không dùng mock data.
6. Không upload lại file lên Google Drive.
7. Tận dụng metadata đã có trong bảng `files`, đặc biệt:
   - `web_view_url`
   - `download_url`
   - `thumbnail_url`
   - `external_file_id`
   - `mime_type`
   - `file_size`

---

## Yêu cầu backend/API

### 1. API detail document

Cập nhật hoặc tạo API:

```http
GET /api/documents/{documentId}
```

API này phải trả detail đầy đủ gồm:

```text
document
file
campus
createdBy
updatedBy
uploadedBy
ownerContext
```

Ví dụ response shape đề xuất:

```ts
interface DocumentDetailResponse {
  document: {
    documentId: number;
    fileId: number;
    ownerType: 'GENERAL' | 'VISIT' | 'PARTNER' | 'MINUTES' | 'NEWS' | 'LOGISTICS' | 'REPORT';
    ownerId: number | null;
    campusId: number | null;
    title: string;
    description: string | null;
    documentCategory: string | null;
    status: 'DRAFT' | 'PUBLISHED' | 'ARCHIVED';
    createdAt: string;
    createdBy: number | null;
    updatedAt: string | null;
    updatedBy: number | null;
  };

  file: {
    fileId: number;
    storageProvider: string;
    bucketName: string | null;
    objectKey: string | null;
    originalFilename: string;
    mimeType: string | null;
    fileSize: number | null;
    checksumSha256: string | null;
    uploadedBy: number | null;
    uploadedAt: string | null;
    externalFileId: string | null;
    webViewUrl: string | null;
    downloadUrl: string | null;
    thumbnailUrl: string | null;
    filePurpose: string | null;
  };

  campus: {
    campusId: number;
    campusCode: string;
    campusName: string;
  } | null;

  createdByUser: UserSummary | null;
  updatedByUser: UserSummary | null;
  uploadedByUser: UserSummary | null;

  ownerContext: DocumentOwnerContext;
}
```

### 2. Owner context DTO

Backend phải resolve `ownerContext` theo `owner_type`.

Đề xuất union type:

```ts
type DocumentOwnerContext =
  | GeneralDocumentContext
  | VisitDocumentContext
  | MinutesDocumentContext
  | LogisticsDocumentContext
  | PartnerDocumentContext
  | NewsDocumentContext
  | ReportDocumentContext
  | UnknownDocumentContext;
```

---

## Context theo từng `owner_type`

### A. `owner_type = VISIT`

Modal phải hiển thị section: **Thông tin đoàn khách**.

Cần resolve và trả về nếu có:

```text
Tên đoàn / visit title
Visit request ID
Visit instance ID nếu có
Campus
Host chính
Thời gian bắt đầu dự kiến
Thời gian kết thúc dự kiến
Trạng thái request
Trạng thái campus instance
Loại visit / scope nếu có
Ghi chú/lý do liên quan nếu có
```

UI section đề xuất:

```text
Thông tin đoàn khách
- Tên đoàn: ...
- Mã request: REQ #...
- Mã instance: INST #...
- Host: ...
- Thời gian diễn ra: dd/MM/yyyy HH:mm - dd/MM/yyyy HH:mm
- Campus: ...
- Trạng thái: ...

Nội dung tài liệu VISIT
- Loại tài liệu: document_category
- Mô tả: documents.description
- File: original_filename
```

Nếu không resolve được host hoặc visit title, hiển thị fallback:

```text
Không tìm thấy thông tin đoàn tương ứng hoặc dữ liệu đã thay đổi.
```

Không được crash modal.

---

### B. `owner_type = MINUTES`

Modal phải hiển thị section: **Thông tin biên bản** và vẫn hiển thị ngữ cảnh đoàn.

Cần resolve:

```text
Tên đoàn
Visit request ID
Visit instance ID
Host chính
Thời gian đoàn diễn ra
Minutes ID
Tiêu đề biên bản nếu có
Người tạo biên bản nếu có
Thời gian tạo/cập nhật biên bản nếu có
Trạng thái biên bản nếu có
Tóm tắt/nội dung biên bản nếu schema có
```

UI section đề xuất:

```text
Thông tin biên bản
- Biên bản: MINUTES #...
- Thuộc đoàn: ...
- Host: ...
- Thời gian đoàn: ...
- Người tạo biên bản: ...
- Cập nhật gần nhất: ...

Nội dung tài liệu biên bản
- Loại tài liệu: document_category
- Mô tả tài liệu: documents.description
- File đính kèm: original_filename
```

Nếu bảng `minutes` không có title/content, chỉ hiển thị các field có thật.

---

### C. `owner_type = LOGISTICS`

Modal phải hiển thị section: **Thông tin hậu cần / logistics** và ngữ cảnh đoàn.

Cần resolve:

```text
Tên đoàn
Visit request ID
Visit instance ID
Host chính
Thời gian đoàn diễn ra
Logistics item ID
Tên hạng mục logistics/resource
Loại logistics nếu có
Department/phòng ban phụ trách nếu có
Người được giao nếu có
Trạng thái logistics
Priority nếu có
Thời gian yêu cầu / deadline nếu có
Ghi chú hoặc proposal nếu có
```

UI section đề xuất:

```text
Thông tin logistics
- Hạng mục: ...
- Thuộc đoàn: ...
- Host: ...
- Thời gian đoàn: ...
- Bộ phận phụ trách: ...
- Người xử lý: ...
- Trạng thái: ...
- Mức ưu tiên: ...

Nội dung tài liệu logistics
- Loại tài liệu: document_category
- Mô tả tài liệu: documents.description
- File đính kèm: original_filename
```

Nếu dữ liệu logistics không resolve được, fallback bằng:

```text
LOGISTICS #owner_id
```

---

### D. `owner_type = PARTNER`

Modal phải hiển thị section: **Thông tin đối tác**.

Cần resolve:

```text
Partner ID
Tên đối tác
Quốc gia
Loại đối tác nếu có
Mô tả chung / profile summary nếu có
Website nếu có
Email/phone nếu có
Trạng thái hồ sơ đối tác
Owner campus nếu có
Người tạo nếu có
```

UI section đề xuất:

```text
Thông tin đối tác
- Tên đối tác: ...
- Quốc gia: ...
- Loại đối tác: ...
- Trạng thái hồ sơ: ...
- Website: ...
- Liên hệ: ...
- Mô tả chung: ...

Nội dung tài liệu đối tác
- Loại tài liệu: document_category
- Mô tả tài liệu: documents.description
- File: original_filename
```

Nếu chưa có country trong schema hoặc tên cột khác, dùng đúng field hiện tại. Không tự thêm field.

---

### E. `owner_type = NEWS`

Modal phải hiển thị section: **Thông tin bài tin tức**.

Cần resolve:

```text
News ID
Tiêu đề bài viết
Mô tả/tóm tắt
Người tạo bài viết
Ngày tạo
Ngày cập nhật
Trạng thái bài viết
Ngày xuất bản nếu có
Reviewer/approver nếu có
```

UI section đề xuất:

```text
Thông tin tin tức
- Tiêu đề: ...
- Mô tả: ...
- Người tạo: ...
- Ngày tạo: ...
- Trạng thái: ...
- Ngày xuất bản: ...

Nội dung tài liệu tin tức
- Loại tài liệu: document_category
- Mô tả tài liệu: documents.description
- File/media: original_filename
```

Nếu news có nhiều bảng như `news_translations`, `news_content_sections`, `news_section_files`, chỉ join khi cần và không làm query quá nặng. Detail document chỉ cần summary đủ hiểu.

---

### F. `owner_type = REPORT`

Modal phải hiển thị section: **Thông tin báo cáo**.

Vì schema hiện tại có thể không có bảng `reports` vật lý, không được tự tạo bảng mới.

Cách xử lý:

1. Nếu codebase/schema có bảng report thật, resolve theo bảng đó.
2. Nếu không có bảng report thật, lấy từ metadata của `documents` và `files`.
3. Có thể parse/hiển thị thông tin kỳ báo cáo nếu backend hiện đã có convention trong `document_category`, `title`, `description`, hoặc `owner_id`.
4. Không bịa dữ liệu.

UI section đề xuất:

```text
Thông tin báo cáo
- Tên báo cáo: documents.title
- Kỳ báo cáo: ngày / tuần / tháng / khoảng thời gian nếu resolve được
- Mô tả: documents.description
- Loại báo cáo: document_category
- Người tạo: createdByUser
- Ngày tạo: created_at

Nội dung tài liệu báo cáo
- File báo cáo: original_filename
- Định dạng: mime_type / extension
- Dung lượng: file_size
```

Nếu không resolve được kỳ báo cáo:

```text
Kỳ báo cáo: Chưa xác định
```

---

### G. `owner_type = GENERAL`

Modal hiển thị section: **Tài liệu chung**.

```text
- Tiêu đề tài liệu
- Danh mục
- Mô tả
- Campus nếu có
- Người tạo
- Ngày tạo
- File
```

---

### H. Unknown / lỗi resolve owner

Nếu `owner_type` hợp lệ nhưng `owner_id` không tìm thấy bản ghi liên quan:

```text
Không tìm thấy bản ghi nghiệp vụ liên quan.
```

Vẫn phải hiển thị:

```text
owner_type
owner_id
document metadata
file metadata
```

Không được làm modal lỗi trắng.

---

## Yêu cầu UI modal chi tiết

### 1. Layout tổng quan

Modal nên chia 2 cột như hiện tại nhưng cần cải tiến:

```text
Header:
[Icon file] Title
original_filename
status badge
close button

Body:
Left panel:
- Thông tin nhanh
- Ngữ cảnh nghiệp vụ theo owner_type
- Chia sẻ / tải xuống / mở Drive

Right panel:
- Preview file nếu có
- Nếu preview không hoạt động thì hiển thị fallback rõ ràng

Bottom hoặc collapsible:
- Metadata đầy đủ documents + files
```

### 2. Header modal

Hiển thị:

```text
Title: documents.title
Subtitle: files.original_filename
Badge status: DRAFT/PUBLISHED/ARCHIVED
Badge owner_type
```

### 3. Quick info cards

Hiển thị các card nhỏ:

```text
Loại nghiệp vụ: owner_type
Trạng thái: status
Kích thước: formatted file_size
Ngày tải: uploaded_at
Danh mục: document_category
Storage: storage_provider
```

### 4. Dynamic business context section

Tên section thay đổi theo `owner_type`:

```text
VISIT     → Thông tin đoàn khách
MINUTES   → Thông tin biên bản
LOGISTICS → Thông tin logistics
PARTNER   → Thông tin đối tác
NEWS      → Thông tin tin tức
REPORT    → Thông tin báo cáo
GENERAL   → Tài liệu chung
```

### 5. Document content section

Luôn hiển thị:

```text
Nội dung tài liệu
- Tiêu đề tài liệu
- Mô tả tài liệu
- Danh mục tài liệu
- File gốc
```

### 6. Full metadata section

Có thể dùng accordion:

```text
Xem metadata kỹ thuật
```

Khi mở, hiển thị đầy đủ:

```text
document_id
file_id
owner_type
owner_id
campus_id
title
description
document_category
status
created_at
created_by
updated_at
updated_by
storage_provider
bucket_name
object_key
original_filename
mime_type
file_size
checksum_sha256
uploaded_by
uploaded_at
external_file_id
web_view_url
download_url
thumbnail_url
file_purpose
```

---

## Yêu cầu preview Google Drive

Hiện modal đang cố preview URL `drive.example` và báo lỗi không tìm thấy IP. Cần sửa:

1. Không iframe trực tiếp nếu `web_view_url` là fake/example hoặc không phải URL hợp lệ.
2. Validate URL trước khi render iframe/preview.
3. Nếu là image/video và có `thumbnail_url`, hiển thị thumbnail.
4. Nếu là PDF và có `web_view_url` hợp lệ, có thể nhúng preview hoặc chỉ hiển thị nút mở Drive tùy pattern hiện tại.
5. Nếu là DOCX/XLSX/PPTX, không cần iframe phức tạp; hiển thị card:

```text
Không thể xem trước trực tiếp trong hệ thống.
Vui lòng mở bằng Google Drive.
```

6. Luôn có button:

```text
Mở trong Google Drive
Tải xuống máy
Copy link
```

7. Nếu không có link hợp lệ:

```text
Chưa có liên kết Google Drive hợp lệ cho file này.
```

8. Không hiển thị lỗi trình duyệt thô như:

```text
Không thể tìm thấy địa chỉ IP của máy chủ drive.example
```

cho người dùng cuối.

---

## Yêu cầu frontend

1. Tạo component render động theo `ownerType`, ví dụ:

```text
DocumentOwnerContextPanel.tsx
```

hoặc nếu không muốn tạo component mới, tách function render rõ ràng:

```ts
renderOwnerContext(detail.ownerContext)
```

2. Không dùng `any` bừa bãi.
3. Định nghĩa type rõ:

```ts
DocumentOwnerType
DocumentDetail
DocumentFileInfo
DocumentOwnerContext
VisitDocumentContext
MinutesDocumentContext
LogisticsDocumentContext
PartnerDocumentContext
NewsDocumentContext
ReportDocumentContext
GeneralDocumentContext
UnknownDocumentContext
```

4. UI không crash nếu thiếu dữ liệu context.
5. Field nào null thì hiển thị:

```text
Chưa có dữ liệu
```

hoặc ẩn nếu không quan trọng.

6. Không hard-code owner_id mapping ở frontend. Frontend chỉ render theo `ownerContext` backend trả về.
7. Frontend có thể fallback nếu backend chưa trả đủ context:

```text
owner_type + #owner_id
```

8. Format:

```text
file_size → KB/MB/GB
ngày giờ → dd/MM/yyyy HH:mm
status → badge
owner_type → badge
```

---

## Yêu cầu backend implementation

1. Controller chỉ gọi MediatR, không viết business query trong Controller.
2. Handler detail document:
   - Load document by document_id.
   - Join/load file.
   - Check role/scope hiện tại.
   - Resolve campus.
   - Resolve createdBy/updatedBy/uploadedBy.
   - Resolve ownerContext theo owner_type.
3. Không query quá nặng cho list. Context chi tiết chỉ cần resolve ở detail API.
4. Không tạo bảng mới.
5. Không sửa schema nếu chưa cần.
6. Nếu owner_type mapping chưa rõ, đọc code/schema hiện tại trước.
7. Nếu không resolve được owner, trả `UnknownDocumentContext`, không throw 500.
8. Scope Staff Leader:
   - Nếu màn document đang dùng cho Staff Leader campus hiện tại, backend phải đảm bảo document thuộc `currentUser.primary_campus_id`.
   - Không cho xem document campus khác qua direct API.
9. Nếu document `campus_id IS NULL`, chỉ cho xem nếu business rule hiện tại cho phép tài liệu GENERAL/toàn hệ thống. Nếu chưa rõ, mặc định chặn với Staff Leader.

---

## Scope Staff Leader campus

Vì tôi đang dùng role Staff Leader campus Hòa Lạc/Hà Nội:

1. List documents chỉ hiển thị tài liệu thuộc campus tôi quản lý.
2. Detail document cũng phải check scope.
3. Không được chỉ ẩn ở frontend.
4. Backend phải enforce:

```text
documents.campus_id = currentUser.primary_campus_id
```

5. Nếu direct API gọi document campus khác:

```text
403 hoặc 404 theo convention hiện tại
```

---

## Yêu cầu action trong modal

### Button `Mở trong Google Drive`

- Dùng `files.web_view_url` nếu hợp lệ.
- Mở tab mới.
- Nếu null hoặc không hợp lệ thì disable button và tooltip:

```text
Chưa có link Google Drive hợp lệ
```

### Button `Tải xuống máy`

- Ưu tiên dùng endpoint download/proxy hiện có nếu project đã có.
- Nếu chưa có endpoint, dùng `files.download_url` nếu hợp lệ.
- Nếu không có link, disable.

### Button `Copy link`

- Copy `web_view_url` nếu có.
- Nếu không có thì copy `download_url` nếu có.
- Hiển thị toast/feedback nhỏ nếu project đã có cơ chế toast.

### Button share Zalo/Gmail

- Giữ nếu đang có, nhưng phải dùng URL hợp lệ.
- Nếu không có URL hợp lệ thì disable hoặc ẩn.

---

## Empty/error/loading state

### Loading

```text
Đang tải chi tiết tài liệu...
```

### Document not found

```text
Không tìm thấy tài liệu hoặc bạn không có quyền xem tài liệu này.
```

### Owner context missing

```text
Không tìm thấy bản ghi nghiệp vụ liên quan. Vẫn hiển thị thông tin tài liệu và file.
```

### File link invalid

```text
Chưa có liên kết Google Drive hợp lệ cho file này.
```

Không để modal trắng hoặc hiện `{}`.

---

## UI style

- Giữ style enterprise dashboard hiện tại.
- Primary blue: `#004c91`.
- Orange: `#F37021` chỉ dùng cho CTA đặc biệt nếu cần.
- Card: `rounded-2xl border border-slate-200 bg-white shadow-sm`.
- Modal: `rounded-3xl shadow-2xl`, rộng `max-w-5xl` hoặc `max-w-6xl`.
- Badge nhỏ gọn.
- Không dùng gradient mạnh.
- Không dùng màu lòe loẹt.
- Không để modal bị overflow xấu.
- Responsive:
  - Desktop: 2 cột.
  - Tablet/mobile: stack thành 1 cột.
- Icon-only button phải có `title` và `aria-label`.

---

## Clean code rules

1. Không dùng mock data.
2. Không hard-code dữ liệu ví dụ như `drive.example`.
3. Không hard-code campus Hòa Lạc/Hà Nội trong backend.
4. Không hard-code owner context bằng text không có trong DB.
5. Không thêm thư viện mới nếu không cần.
6. Không refactor sâu ngoài module Documents.
7. Không đổi route/list table nếu không cần.
8. Không làm mất chức năng search/filter/table hiện có.
9. Không để TypeScript build lỗi.
10. Không để backend build lỗi.
11. Không báo hoàn thành nếu detail modal còn hiện `{}` hoặc URL fake.

---

## Manual test bắt buộc

1. Login bằng Staff Leader campus Hòa Lạc/Hà Nội.
2. Vào `/dashboard/documents`.
3. List chỉ thấy document thuộc campus hiện tại.
4. Bấm mắt ở document `VISIT`:
   - Modal hiển thị thông tin đoàn, host, thời gian đoàn, nội dung tài liệu.
5. Bấm mắt ở document `MINUTES`:
   - Modal hiển thị thông tin biên bản và đoàn liên quan.
6. Bấm mắt ở document `LOGISTICS`:
   - Modal hiển thị hạng mục logistics, người phụ trách, trạng thái, đoàn liên quan.
7. Bấm mắt ở document `PARTNER`:
   - Modal hiển thị tên đối tác, quốc gia, mô tả chung, nội dung tài liệu.
8. Bấm mắt ở document `NEWS`:
   - Modal hiển thị tiêu đề tin tức, mô tả, người tạo, trạng thái.
9. Bấm mắt ở document `REPORT`:
   - Modal hiển thị thông tin kỳ báo cáo nếu có, mô tả, file báo cáo.
10. Bấm mắt ở document không resolve được owner:
   - Modal không crash, hiển thị fallback.
11. File không có link Drive hợp lệ:
   - Không hiện lỗi `drive.example` trong preview.
   - Button mở Drive/tải xuống disable hợp lý.
12. File có `web_view_url` hợp lệ:
   - Mở tab Google Drive đúng.
13. File có `download_url` hợp lệ:
   - Tải xuống được hoặc gọi endpoint download hiện có.
14. Copy link hoạt động.
15. Direct API document campus khác bị chặn.
16. Build backend pass.
17. Build frontend pass.

---

## Kết quả cần báo cáo sau khi code

Sau khi sửa xong, báo cáo rõ:

1. Root cause modal cũ chưa hiển thị đúng context.
2. File backend đã sửa.
3. File frontend đã sửa.
4. API detail document đã thay đổi gì.
5. Mapping `owner_type` → context đã implement những loại nào.
6. Loại nào chưa resolve được vì thiếu schema/data.
7. Cách xử lý URL Google Drive không hợp lệ.
8. Cách enforce Staff Leader campus scope.
9. Kết quả build backend.
10. Kết quả build frontend.
11. Checklist test thủ công.
