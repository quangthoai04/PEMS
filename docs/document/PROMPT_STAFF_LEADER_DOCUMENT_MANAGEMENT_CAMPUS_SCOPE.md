# Prompt nâng cấp UI/API Document Management cho Staff Leader theo campus scope

## 1. Bối cảnh

Bạn là **Senior Full-stack Engineer** cho dự án **PEMS - Partnership Engagement Management System**.

Nhiệm vụ: nâng cấp màn hình **Document Management** tại route:

```text
/dashboard/documents
```

Màn hình này phục vụ role **Staff Leader** đang quản lý tài liệu trong campus của mình. Với trường hợp hiện tại, user đang đăng nhập là **Staff Leader campus Hà Nội**, nên màn hình chỉ được hiển thị tài liệu thuộc **campus Hà Nội**.

## 2. Mục tiêu chính

- Màn hình chỉ hiển thị tài liệu thuộc campus của Staff Leader đang đăng nhập.
- Backend phải tự lấy `currentUser.primary_campus_id` từ token/session để lọc dữ liệu.
- Frontend **không được truyền `campusId`** trong query params.
- UI **không có filter Campus**.
- Không cho user xem tài liệu campus khác bằng cách gọi API trực tiếp.
- Tận dụng file đã lưu trên Google Drive thông qua metadata trong bảng `files`.
- Không dùng mock data.
- Không upload lại file nếu file đã có `external_file_id`, `web_view_url`, `download_url` trong bảng `files`.

---

## 3. Schema cần bám sát

### 3.1. Bảng `documents`

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

### 3.2. Bảng `files`

```text
file_id
storage_provider ENUM('LOCAL','S3','AZURE','GCS','GOOGLE_DRIVE','OTHER')
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

## 4. Yêu cầu phân quyền và scope dữ liệu

### 4.1. Role được phép dùng màn hình

Màn hình này phục vụ **Staff Leader** quản lý tài liệu trong campus của mình.

Backend phải kiểm tra:

```text
currentUser.role_code = STAFF
currentUser.sub_role = LEADER
currentUser.primary_campus_id IS NOT NULL
```

### 4.2. Scope query bắt buộc

Query `documents` bắt buộc lọc:

```sql
documents.campus_id = currentUser.primary_campus_id
```

Với Staff Leader campus Hà Nội, API chỉ được trả tài liệu của campus Hà Nội.

### 4.3. Quy tắc bảo mật

- Frontend không được truyền `campusId` trong query params.
- Frontend không được hiển thị dropdown/filter Campus.
- Nếu API bị gọi trực tiếp kèm `campusId` khác, backend phải bỏ qua hoặc chặn.
- Tuyệt đối không trả dữ liệu campus khác.
- Nếu document có `campus_id = NULL`, không hiển thị trong màn Staff Leader campus, trừ khi business rule hiện tại đã quy định Staff Leader được xem tài liệu GENERAL toàn hệ thống.
- Nếu chưa có rule rõ ràng, mặc định **không hiển thị `campus_id = NULL`**.

---

## 5. Yêu cầu UI

### 5.1. Layout tổng thể

- Giữ layout dashboard hiện tại.
- Giữ sidebar hiện tại.
- Giữ route hiện tại: `/dashboard/documents`.
- Không đổi role routing nếu không cần.

Đổi subtitle thành:

```text
Tra cứu, xem và tải xuống tài liệu nghiệp vụ của campus Hà Nội được lưu trên Google Drive
```

Tuy nhiên, không hard-code Hà Nội nếu hệ thống đã có `currentUser.primaryCampusName`. Ưu tiên hiển thị động theo user hiện tại:

```text
Tra cứu, xem và tải xuống tài liệu nghiệp vụ của campus {primaryCampusName} được lưu trên Google Drive
```

Hiển thị một badge nhỏ dưới tiêu đề hoặc cạnh subtitle:

```text
Campus: Hà Nội
```

Hoặc lấy theo dữ liệu thật:

```text
Campus: {currentUser.primaryCampusName}
```

### 5.2. Summary cards

Thêm các summary cards phía trên table:

```text
Tổng tài liệu
DRAFT
PUBLISHED
ARCHIVED
Tổng dung lượng nếu API trả được
```

Không có card hoặc filter chọn campus.

### 5.3. Filter bar chính

Filter bar chính gồm:

```text
Search input lớn
Dropdown status
Dropdown ownerType
Dropdown documentCategory
Date range
Button Lọc
Button Reset
```

Search input tìm theo:

```text
documents.title
documents.description
documents.document_category
files.original_filename
owner display name nếu backend resolve được
```

Dropdown `status`:

```text
All
DRAFT
PUBLISHED
ARCHIVED
```

Dropdown `ownerType`:

```text
All
GENERAL
VISIT
PARTNER
MINUTES
NEWS
LOGISTICS
REPORT
```

Dropdown `documentCategory`:

- Ưu tiên lấy từ API distinct categories nếu có.
- Nếu chưa có API riêng, lấy từ data list hiện tại.
- Không hard-code bừa nếu DB đang để `VARCHAR(100)`.

Date range:

```text
uploadedFrom
uploadedTo
```

Hoặc:

```text
createdFrom
createdTo
```

Tùy API hiện tại thuận tiện hơn, nhưng phải đặt tên rõ ràng.

### 5.4. Bộ lọc nâng cao

Thêm collapsible section:

```text
Bộ lọc nâng cao
```

Khi mở ra, hiển thị:

```text
mimeType / file extension
storageProvider
filePurpose
uploadedBy
createdBy
file size min/max
hasDriveLink
ownerId
documentId / fileId
```

### 5.5. Không có campus filter

Bắt buộc:

- Không hiển thị dropdown Campus.
- Không hiển thị filter Campus trên desktop.
- Không hiển thị filter Campus trên mobile.
- Không truyền `campusId` lên API.

Nếu cần nhắc người dùng về scope, chỉ hiển thị text nhỏ:

```text
Đang hiển thị tài liệu thuộc campus Hà Nội
```

Hoặc động theo user:

```text
Đang hiển thị tài liệu thuộc campus {primaryCampusName}
```

---

## 6. Table desktop

### 6.1. Cột cần hiển thị

Table desktop hiển thị các cột:

```text
Tài liệu
Ngữ cảnh nghiệp vụ
Trạng thái
File
Thời gian
Hành động
```

Không hiển thị cột Campus trong table chính vì toàn bộ dữ liệu đã được scope theo campus hiện tại.

### 6.2. Cột “Tài liệu”

Hiển thị:

```text
Icon theo loại file
title
original_filename
DOC #document_id · FILE #file_id
```

Ví dụ:

```text
Hướng dẫn quy trình đón tiếp khách đoàn
huong_dan_quy_trinh.pdf
DOC #12 · FILE #88
```

### 6.3. Cột “Ngữ cảnh nghiệp vụ”

Hiển thị:

```text
owner_type badge
owner display name hoặc owner_type #owner_id
document_category
```

Ví dụ:

```text
PARTNER
Đại học Quốc gia Hà Nội
PARTNER_PROFILE
```

Hoặc fallback:

```text
VISIT
VISIT #102
AGENDA
```

### 6.4. Cột “Trạng thái”

Badge màu:

```text
DRAFT      = vàng nhạt
PUBLISHED  = xanh/cyan
ARCHIVED   = xám
```

### 6.5. Cột “File”

Hiển thị:

```text
extension hoặc mime ngắn
file size formatted
storage_provider
```

Ví dụ:

```text
PDF
2.4 MB
GOOGLE_DRIVE
```

Không hiển thị raw `mime_type` dài trong table.

### 6.6. Cột “Thời gian”

Hiển thị gọn:

```text
Tải lên: dd/MM/yyyy HH:mm
Tạo doc: dd/MM/yyyy HH:mm
Cập nhật: dd/MM/yyyy HH:mm
```

Nếu thiếu `updated_at`, hiển thị:

```text
Chưa cập nhật
```

### 6.7. Cột “Hành động”

Các action nên có:

```text
Xem chi tiết
Mở trên Google Drive
Tải xuống
Copy link
Sửa metadata nếu role/API cho phép
Lưu trữ nếu role/API cho phép
```

Icon-only button phải có:

```text
title
aria-label
```

Không chỉ để một nút “Tải xuống”, vì người dùng cần xem metadata và kiểm tra tài liệu thuộc nghiệp vụ nào.

---

## 7. Mobile/tablet UI

- Không ép table desktop gây horizontal scroll toàn trang.
- Mobile dùng card list.
- Card hiển thị:

```text
title
original_filename
status
owner_type
document_category
file size
uploaded_at
action buttons
```

- Không hiển thị campus filter trên mobile.
- Có thể hiển thị badge read-only:

```text
Campus: Hà Nội
```

Hoặc:

```text
Campus: {primaryCampusName}
```

---

## 8. Modal xem chi tiết

### 8.1. Layout modal

Modal rộng, dễ đọc:

```text
max-w-4xl hoặc max-w-5xl
```

Không nhét toàn bộ field vào table. Toàn bộ field đầy đủ phải nằm trong modal chi tiết.

### 8.2. Header modal

Header gồm:

```text
File icon
title
original_filename
status badge
Button Mở trên Google Drive
Button Tải xuống
```

### 8.3. Section “Thông tin tài liệu”

Hiển thị đủ field từ `documents`:

```text
document_id
title
description
document_category
status
owner_type
owner_id
campus_id
campus name/code nếu API trả về
created_at
created_by
updated_at
updated_by
```

### 8.4. Section “Thông tin file / Google Drive”

Hiển thị đủ field từ `files`:

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

### 8.5. Section “Ngữ cảnh nghiệp vụ”

Hiển thị:

```text
owner_type
owner_id
owner display name nếu backend resolve được
fallback: owner_type + #owner_id
```

### 8.6. Section “Audit”

Hiển thị:

```text
created_by display name
created_at
updated_by display name
updated_at
uploaded_by display name
uploaded_at
```

### 8.7. URL và preview

Với URL dài như:

```text
web_view_url
download_url
thumbnail_url
```

Yêu cầu:

- Không show full trong table.
- Trong modal hiển thị rút gọn.
- Có nút copy.
- Có nút mở link.

Nếu là image/video và có `thumbnail_url`, hiển thị preview nhỏ.

Nếu là PDF/DOCX/XLSX, chỉ cần nút:

```text
Mở trên Google Drive
Tải xuống
```

Không cần nhúng iframe phức tạp.

---

## 9. Yêu cầu backend/API

### 9.1. Nguyên tắc

- Không tạo bảng mới.
- Không lưu binary file trong DB.
- Không upload lại file lên Drive nếu `files` đã có `external_file_id`, `web_view_url`, `download_url`.
- Không tạo bảng `reports` nếu schema hiện tại chưa có.
- Không dùng mock data.

### 9.2. API list

Endpoint:

```http
GET /api/documents
```

Query params được phép:

```text
q
status
ownerType
ownerId
documentCategory
storageProvider
mimeType
filePurpose
uploadedFrom
uploadedTo
createdFrom
createdTo
minSize
maxSize
hasDriveLink
page
pageSize
sortBy
sortDir
```

Không nhận hoặc không sử dụng `campusId` từ frontend cho Staff Leader.

Backend tự lấy campus scope:

```text
currentUser.primary_campus_id
```

Điều kiện bắt buộc:

```sql
d.campus_id = currentUser.primary_campus_id
```

### 9.3. Join dữ liệu

API list phải join:

```sql
documents d
files f ON d.file_id = f.file_id
campuses c ON d.campus_id = c.campus_id
users createdUser ON d.created_by = createdUser.user_id
users updatedUser ON d.updated_by = updatedUser.user_id
users uploadedUser ON f.uploaded_by = uploadedUser.user_id
```

Tùy ORM/repository hiện tại, có thể dùng LINQ/EF query tương ứng, nhưng kết quả phải trả đủ dữ liệu cần thiết cho UI.

### 9.4. API detail

Endpoint:

```http
GET /api/documents/{documentId}
```

Detail cũng phải kiểm tra scope:

```text
document.campus_id = currentUser.primary_campus_id
```

Nếu document không thuộc campus của user hiện tại:

```text
Trả 403 hoặc 404 theo convention hiện tại của project.
```

API detail trả:

```text
document fields
file fields
campus summary
createdBy display name
updatedBy display name
uploadedBy display name
owner display summary nếu resolve được
```

### 9.5. Resolve owner display name

Backend resolve owner display name theo `owner_type` nếu có thể:

```text
PARTNER   -> partners.name
VISIT     -> visit_requests hoặc visit_request_campuses theo convention hiện tại của codebase
MINUTES   -> minutes title/code nếu có
NEWS      -> news title
LOGISTICS -> visit_logistics_items item_name/title nếu có
REPORT    -> nếu không có reports table thì chỉ hiển thị REPORT #owner_id
GENERAL   -> Tài liệu chung
```

Nếu chưa chắc owner mapping thì không bịa. Trả fallback:

```text
{owner_type} #{owner_id}
```

### 9.6. Download/open Drive

Nếu có chức năng download:

- Ưu tiên dùng existing file download/proxy endpoint nếu project đã có.
- Nếu `files.download_url` có dữ liệu và được phép mở trực tiếp thì dùng.
- Nếu chỉ có `web_view_url`, action **Mở Drive** vẫn phải hoạt động.

### 9.7. Edit metadata nếu có

Nếu có edit metadata:

Chỉ cho sửa:

```text
documents.title
documents.description
documents.document_category
documents.status
```

Không sửa từ UI thường:

```text
files.object_key
files.external_file_id
files.checksum_sha256
```

Validate `status` đúng enum:

```text
DRAFT
PUBLISHED
ARCHIVED
```

Detail/edit cũng phải kiểm tra campus scope.

Không hard-delete document; nếu cần ẩn thì dùng:

```text
ARCHIVED
```

---

## 10. Yêu cầu frontend

- Không dùng mock data.
- Không truyền `campusId` lên API trong màn Staff Leader Document Management.
- Không hiển thị dropdown Campus.
- Không hiển thị cột Campus trong table chính.
- Có thể hiển thị campus ở header dạng read-only badge.
- Không hard-code Hà Nội nếu hệ thống đã có `currentUser.primaryCampusName`.
- Ưu tiên lấy campus name từ auth/current user.
- Nếu chưa có thì backend response summary có thể trả `scopeCampusName`.

Tạo type/interface rõ ràng:

```text
DocumentListItem
DocumentDetail
DocumentFilterParams
DocumentStatus
DocumentOwnerType
```

Tách service/hook theo pattern hiện có:

```text
documentsApi.ts
useDocuments.ts
```

Hoặc dùng query pattern hiện tại của project nếu đã có.

Yêu cầu khác:

- Debounce search 300-500ms.
- Format `file_size` thành KB/MB/GB.
- Format date theo `dd/MM/yyyy HH:mm`.
- Loading/empty/error state rõ ràng.

Empty state:

```text
Chưa có tài liệu nào trong campus của bạn.
```

Khi có filter nhưng không ra kết quả:

```text
Không tìm thấy tài liệu phù hợp với bộ lọc.
```

Khi copy link thành công, hiển thị toast hoặc feedback nhỏ nếu project đã có cơ chế sẵn.

Không thêm thư viện mới nếu không cần.

---

## 11. Style UI

Thiết kế theo phong cách enterprise dashboard:

```text
Sạch
Gọn
Hiện đại
Dễ đọc
Không màu mè
Không hiệu ứng thừa
```

Màu chuẩn:

```text
Primary blue: #004c91
Orange: #F37021
Text chính: slate-800 hoặc slate-900
Text phụ: slate-500 hoặc slate-600
Border: slate-200 hoặc slate-300
Background: slate-50
Card: white
```

Card style:

```text
rounded-2xl border border-slate-200 bg-white shadow-sm
```

Table:

```text
Header navy
Badge trạng thái nhỏ gọn
Không để horizontal scroll toàn trang
Action column đủ rộng, không cắt chữ Hành động
```

Mobile:

```text
Dùng card list thay vì ép table
Không có horizontal scroll toàn trang
```

Accessibility:

```text
Icon-only button phải có title và aria-label
Focus state rõ ràng
Button không bị xuống dòng
```

---

## 12. Yêu cầu code clean

- Không refactor sâu ngoài phạm vi Document Management.
- Không đổi route/role logic nếu không cần.
- Không hard-code role bằng text rải rác; dùng constants/enums hiện có.
- Không hard-code campus Hà Nội trong backend query.
- Backend phải dùng `currentUser.primary_campus_id`.
- Hà Nội chỉ là dữ liệu thực tế của user hiện tại.
- Không tạo bảng mới.
- Không tạo reports table nếu schema hiện tại chưa có.
- Không dùng `any` bừa bãi trong TypeScript.
- Không để TypeScript build lỗi.
- Không để backend build lỗi.
- Không báo hoàn thành nếu chưa test hoặc chưa nói rõ phần chưa test được.

---

## 13. Build/test bắt buộc

Nếu sửa backend:

```bash
dotnet build
```

Nếu sửa frontend:

```bash
npm run build
```

Hoặc dùng lệnh build thực tế của project nếu đang dùng `pnpm`/`yarn`.

Manual test:

```text
1. Login Staff Leader campus Hà Nội.
2. Vào /dashboard/documents.
3. Không thấy filter Campus.
4. Không thấy cột Campus trong table chính.
5. Chỉ thấy tài liệu thuộc campus Hà Nội.
6. Không thấy tài liệu campus khác.
7. Search hoạt động.
8. Filter status hoạt động.
9. Filter ownerType hoạt động.
10. Filter documentCategory hoạt động.
11. Bộ lọc nâng cao hoạt động nếu đã triển khai.
12. Modal detail mở đúng.
13. Modal detail hiển thị đủ documents fields.
14. Modal detail hiển thị đủ files fields.
15. Mở Google Drive hoạt động nếu web_view_url có dữ liệu.
16. Tải xuống hoạt động nếu download_url/proxy endpoint có dữ liệu.
17. Copy link hoạt động.
18. Gọi trực tiếp API với campusId khác không trả dữ liệu campus khác.
19. Gọi detail document thuộc campus khác bị chặn.
20. Mobile/tablet không bị vỡ layout.
```

---

## 14. Kết quả cần báo cáo sau khi code

Sau khi code xong, báo cáo ngắn gọn nhưng đầy đủ:

```text
1. File đã sửa.
2. API đã thêm/sửa.
3. UI đã thay đổi gì.
4. Scope Staff Leader campus được enforce ở đâu.
5. Cách test thủ công.
6. Build đã chạy chưa.
7. Những phần chưa làm được và lý do nếu có.
```

---

## 15. Lưu ý quan trọng

Điểm quan trọng nhất là không chỉ ẩn filter campus ở frontend. Backend cũng phải tự khóa dữ liệu theo:

```text
currentUser.primary_campus_id
```

Như vậy Staff Leader Hà Nội không thể xem tài liệu campus khác kể cả khi gọi API trực tiếp.
