# PROMPT_CODE_CREATE_NEWS_BACKEND.md

## 0. Mục tiêu

Triển khai backend cho use case **Create News** trong hệ thống **PEMS — Partnership Engagement Management System**.

Use case này cho phép **Staff thường** và **Student** tạo bài tin tức về một chuyến tiếp khách mà chính họ đã tham gia. Bài viết chỉ được tạo **sau khi chuyến tiếp khách đã đóng đoàn**.

Màn hình frontend tương ứng:

```text
/dashboard/news/create
```

Luồng UI đã chốt:

```text
1. User vào /dashboard/news.
2. User bấm + Thêm tin tức mới.
3. Hệ thống mở màn Tạo tin tức.
4. Bước đầu tiên bắt buộc chọn một chuyến tiếp khách đã đóng.
5. Chỉ hiển thị các chuyến mà user hiện tại đã ACCEPTED tham gia.
6. Chỉ hiển thị/chọn các chuyến chưa có bài news.
7. Sau khi chọn chuyến hợp lệ, user mới nhập tiêu đề, mô tả, ảnh đại diện và nội dung chi tiết.
8. Bấm Gửi duyệt.
9. Backend tạo news với status = PENDING_REVIEW.
10. Staff Leader cùng campus nhận notification cần duyệt bài.
```

---

## 1. Quyết định nghiệp vụ đã chốt

```text
Use case: Create News
Module: News Management
Page: /dashboard/news/create
Main endpoint: POST /api/news
Support endpoint: GET /api/news/eligible-visit-instances
Authentication: Required
Allowed creator roles:
- STAFF + sub_role = STAFF
- STUDENT

Not allowed:
- HO
- STAFF + sub_role = LEADER
- ADMIN
- DEPARTMENT
- VISITOR

Rule quan trọng:
- Sau tiếp khách mới được lên bài.
- Chỉ tạo bài cho visit instance có status = CLOSED.
- User phải có visit_participants.status = ACCEPTED trong đúng visitInstanceId.
- Mỗi visitInstance chỉ có tối đa 1 bài news.
- Bài mới tạo luôn status = PENDING_REVIEW.
- Staff Leader cùng campus duyệt bài sau.
```

Lưu ý về UC numbering:

```text
Nếu trong tài liệu Report/Use Case List có số UC khác nhau, khi code hãy bám theo tên chức năng "Create News".
Không code nhầm sang View News List, View News Detail, Approve News, Change News Visibility hoặc Public News.
```

---

## 2. Tài liệu bắt buộc phải đọc trước khi code

AI Agent phải đọc trước:

```text
docs/architecture/PROJECT_STRUCTURE_FULL.md
docs/database/DATABASE_SCHEMA_v8_4_refined_v6_v10_no_dynamic_permissions_FULL_UPDATED.md
docs/PEMS_CANONICAL_BUSINESS_RULES_v8_4_refined_v6_v10_FULL_UPDATED.md
docs/PEMS_UC_IMPLEMENTATION_RULEBOOK_FRONTEND_BACKEND_DATABASE_VALIDATION_SECURITY_v8_4_refined_v6_v10_FULL_UPDATED.md
docs/PROJECT_OVERVIEW_v8_4_refined_v6_v10_FULL_UPDATED.md
Report 3.1_UCS_Template.docx
Report 3.2_ScreenDesignSpec_Template.docx
```

Nếu có mâu thuẫn:

```text
1. Ưu tiên nghiệp vụ đã chốt trong file này.
2. Ưu tiên schema v10 về bảng/cột/enum/status.
3. Ưu tiên PROJECT_STRUCTURE_FULL.md mới nhất về đường dẫn file thật.
4. Tài liệu cũ chỉ dùng đối chiếu, không dùng nếu trái với schema v10 hoặc rule đã chốt.
```

---

## 3. Source of truth theo schema v10

Các bảng chính cần dùng:

```text
news
news_translations
news_content_sections
news_section_files
files
users
campuses
visit_request_campuses
visit_participants
notifications
audit_logs / audit_log_changes nếu project đã có audit
```

Các cột quan trọng của `news`:

```text
news_id
campus_id
visit_instance_id
author_user_id
cover_file_id
status
submitted_at
reviewed_by
reviewed_at
review_note
published_at
created_at
created_by
updated_at
updated_by
```

Status hợp lệ của news:

```text
PENDING_REVIEW
REJECTED
PUBLISHED
HIDDEN
```

Không dùng status cũ:

```text
DRAFT
ARCHIVED
APPROVED
VISIBLE
```

Bảng `visit_participants` dùng để kiểm tra user có thật sự tham gia chuyến tiếp khách không:

```text
visit_instance_id
user_id
participant_role
status
```

Status hợp lệ để tạo news:

```text
ACCEPTED
```

Bảng `visit_request_campuses` dùng để kiểm tra chuyến tiếp khách đã đóng chưa:

```text
visit_instance_id
campus_id
status
planned_start_at
planned_end_at
```

Status bắt buộc để tạo news:

```text
CLOSED
```

---

## 4. Phân biệt với các use case khác

| Use case | Endpoint | Mục đích |
|---|---|---|
| View News List | `GET /api/news` | Xem danh sách tin tức theo role/scope |
| Create News | `POST /api/news` | Staff/Student tạo bài mới sau khi chuyến tiếp khách đã CLOSED |
| Eligible Visit Instances | `GET /api/news/eligible-visit-instances` | Lấy danh sách chuyến đã đóng mà current user đã ACCEPTED và chưa có bài |
| View News Detail | `GET /api/news/{newsId}` | Xem chi tiết bài viết |
| Update News | `PUT /api/news/{newsId}` | Tác giả sửa bài PENDING_REVIEW/REJECTED |
| Approve/Reject News | `PATCH /api/news/{newsId}/review` | Staff Leader duyệt/từ chối |
| Change News Visibility | `PATCH /api/news/{newsId}/visibility` | Staff Leader ẩn/hiện PUBLISHED/HIDDEN |
| Public News | `GET /api/public/news` | Public chỉ xem bài PUBLISHED |

Create News không xử lý duyệt, từ chối, ẩn/hiện hoặc public bài.

---

## 5. UI flow đã chốt

### 5.1. Cách vào màn tạo

User vào:

```text
/dashboard/news
```

Bấm:

```text
+ Thêm tin tức mới
```

Điều hướng tới:

```text
/dashboard/news/create
```

### 5.2. Section đầu tiên bắt buộc

Đầu form phải có section:

```text
0. CHỌN CHUYẾN TIẾP KHÁCH ĐÃ ĐÓNG
```

Trước khi chọn chuyến, các phần sau nên bị disabled hoặc hiển thị mờ:

```text
1. THÔNG TIN CƠ BẢN
2. ẢNH ĐẠI DIỆN
3. NỘI DUNG CHI TIẾT
```

Thông báo gợi ý:

```text
Vui lòng chọn chuyến tiếp khách đã đóng trước khi tạo tin tức.
```

### 5.3. Card chuyến tiếp khách trong dropdown/list

Chỉ hiển thị những chuyến mà user hiện tại đủ điều kiện tạo bài.

Card hiển thị:

```text
Đoàn Đại học ABC đến FPT Hà Nội
Campus: Hà Nội
Thời gian: 10/06/2026 - 12/06/2026
Trạng thái: Đã đóng đoàn
Trạng thái tin tức: Chưa có bài
[Chọn chuyến này]
```

Không cần hiển thị:

```text
Vai trò của bạn
```

### 5.4. Nếu chuyến đã có bài

Có 2 cách UI:

```text
Cách A — Ẩn khỏi danh sách chọn.
Cách B — Hiển thị disabled để user hiểu lý do.
```

Khuyến nghị dùng Cách B nếu UI muốn rõ ràng:

```text
Đoàn Đại học ABC đến FPT Hà Nội
Campus: Hà Nội
Thời gian: 10/06/2026 - 12/06/2026
Trạng thái: Đã đóng đoàn
Trạng thái tin tức: Đã có bài viết
[Không thể chọn]
```

Với Staff/Student, nếu bài đó do người khác tạo, không cần lộ tên người tạo.

### 5.5. Empty state nếu không có chuyến đủ điều kiện

Nếu user chưa có chuyến nào đã đóng mà họ đã ACCEPTED:

```text
Bạn chưa có chuyến tiếp khách đã đóng để viết tin tức.

Bạn chỉ có thể tạo tin tức cho chuyến tiếp khách mà bạn đã xác nhận tham gia và đã được đóng đoàn.
```

Nút tạo/gửi duyệt disabled.

---

## 6. Support endpoint — Eligible Visit Instances

### 6.1. Endpoint

```http
GET /api/news/eligible-visit-instances
```

### 6.2. Mục đích

Trả danh sách các chuyến tiếp khách mà current user được phép chọn khi tạo news.

### 6.3. Authorization

Allowed:

```text
STAFF + sub_role = STAFF
STUDENT
```

Không cho:

```text
STAFF + sub_role = LEADER
HO
ADMIN
DEPARTMENT
VISITOR
```

### 6.4. Query logic bắt buộc

Chỉ trả visit instances thỏa:

```sql
visit_participants.user_id = @CurrentUserId
AND visit_participants.status = 'ACCEPTED'
AND visit_request_campuses.status = 'CLOSED'
```

Và để đảm bảo một chuyến chỉ có một bài:

```sql
AND NOT EXISTS (
    SELECT 1
    FROM news n
    WHERE n.visit_instance_id = visit_request_campuses.visit_instance_id
)
```

Nếu muốn UI hiển thị disabled các chuyến đã có bài, có thể thêm query param:

```http
GET /api/news/eligible-visit-instances?includeAlreadyHasNews=true
```

Khi đó response có thêm:

```text
hasNews: true/false
canSelect: true/false
```

### 6.5. Response DTO đề xuất

```json
{
  "items": [
    {
      "visitInstanceId": 123,
      "visitTitle": "Đoàn Đại học ABC đến FPT Hà Nội",
      "campusName": "FPT University Hà Nội",
      "plannedStartAt": "2026-06-10T09:00:00",
      "plannedEndAt": "2026-06-12T17:00:00",
      "closedAt": "2026-06-12T17:30:00",
      "status": "CLOSED",
      "hasNews": false,
      "canSelect": true
    }
  ]
}
```

Không trả `participantRole` vì UI đã chốt không cần hiển thị vai trò.

---

## 7. Main endpoint — Create News

### 7.1. Endpoint

```http
POST /api/news
Authorization: Bearer <token>
Content-Type: application/json
```

### 7.2. Request DTO đề xuất

```json
{
  "visitInstanceId": 123,
  "coverFileId": 456,
  "title": "Buổi tiếp đón đoàn Đại học ABC tại FPT Hà Nội",
  "summary": "FPT University Hà Nội đã có buổi tiếp đón và trao đổi hợp tác với đoàn Đại học ABC.",
  "contentSections": [
    {
      "sectionOrder": 1,
      "sectionTitle": "Tổng quan buổi tiếp đón",
      "sectionBodyHtml": "<p>Nội dung chi tiết...</p>",
      "sectionFiles": [
        {
          "fileId": 789,
          "usageType": "INLINE_IMAGE",
          "displayOrder": 1
        }
      ]
    }
  ]
}
```

Backend không nhận:

```text
campusId
authorUserId
status
submittedAt
reviewedBy
reviewedAt
reviewNote
publishedAt
isFeatured
createdBy
updatedBy
```

Các field này backend tự xử lý.

---

## 8. Request field rules

| Field | Required | Rule |
|---|---:|---|
| `visitInstanceId` | Có | Phải tồn tại trong `visit_request_campuses`, status phải là `CLOSED`, current user phải có participant `ACCEPTED`, và visit instance chưa có news. |
| `coverFileId` | Không | Nếu có, phải tồn tại trong `files`, là ảnh hợp lệ, và user có quyền dùng file. |
| `title` | Có | Trim, sanitize, không rỗng, tối đa 150 ký tự theo UI. |
| `summary` | Có | Trim, sanitize, không rỗng, tối đa 250 ký tự theo UI. |
| `contentSections` | Có | Tối thiểu 1 section, tối đa 10 section. |
| `contentSections[].sectionOrder` | Có | Từ 1 đến 10, không trùng trong cùng bài. |
| `contentSections[].sectionTitle` | Có | Trim, sanitize, không rỗng, tối đa 255 ký tự. |
| `contentSections[].sectionBodyHtml` | Có | Sanitize HTML, sau khi tách plain text không được rỗng. |
| `sectionFiles` | Không | Nếu có, mỗi `fileId` phải tồn tại, `usageType` thuộc `INLINE_IMAGE` hoặc `ATTACHMENT`. |

---

## 9. Response DTO đề xuất

```json
{
  "success": true,
  "message": "Tạo tin tức thành công. Bài viết đã được gửi cho Staff Leader duyệt.",
  "data": {
    "newsId": 1001,
    "visitInstanceId": 123,
    "status": "PENDING_REVIEW",
    "statusLabel": "Chờ Duyệt",
    "submittedAt": "2026-06-25T18:55:00"
  }
}
```

Nếu bị trùng do người khác vừa tạo bài:

```json
{
  "success": false,
  "message": "Chuyến tiếp khách này đã có bài viết. Mỗi chuyến chỉ được tạo một bài tin tức.",
  "errorCode": "NEWS_ALREADY_EXISTS_FOR_VISIT_INSTANCE"
}
```

Nếu bài đã có là của chính user hiện tại và có thể sửa:

```json
{
  "success": false,
  "message": "Bạn đã có bài viết cho chuyến này. Vui lòng chỉnh sửa bài hiện có.",
  "errorCode": "NEWS_ALREADY_EXISTS_FOR_VISIT_INSTANCE",
  "data": {
    "existingNewsId": 1001,
    "canEditExisting": true
  }
}
```

Nếu bài đã có là của người khác, không trả chi tiết bài cho Staff/Student.

---

## 10. Main Flow

### Step 1 — User mở form tạo

User bấm `+ Thêm tin tức mới` ở `/dashboard/news`.

Frontend gọi:

```http
GET /api/news/eligible-visit-instances
```

Backend trả các chuyến:

```text
- current user đã ACCEPTED
- visit instance status = CLOSED
- chưa có news
```

### Step 2 — User chọn chuyến đã đóng

User chọn một chuyến trong danh sách.

Sau khi chọn, UI hiển thị card tóm tắt:

```text
Chuyến tiếp khách được chọn

Đoàn Đại học ABC đến FPT Hà Nội
Campus: Hà Nội
Thời gian tiếp khách: 10/06/2026 - 12/06/2026
Trạng thái: Đã đóng đoàn
Trạng thái tin tức: Chưa có bài

[Đổi chuyến]
```

Không hiển thị “Vai trò của bạn”.

### Step 3 — User nhập thông tin cơ bản

User nhập:

```text
Tiêu đề tin tức
Mô tả ngắn
```

### Step 4 — User chọn ảnh đại diện

User upload hoặc chọn ảnh. Upload trả về `fileId`.

Frontend gửi `coverFileId` trong request tạo news.

### Step 5 — User nhập nội dung chi tiết

User nhập ít nhất 1 content section.

Mỗi section gồm:

```text
Tiêu đề nội dung
Miêu tả / nội dung rich text
File/ảnh trong section nếu có
```

Tối đa 10 section.

### Step 6 — User bấm Gửi duyệt

Khuyến nghị nút submit nên ghi:

```text
Gửi duyệt
```

Không nên ghi “Đăng tin” vì bài chưa public ngay.

### Step 7 — Backend validate role

Chỉ cho:

```text
STAFF + sub_role = STAFF
STUDENT
```

Không cho Staff Leader/HO/Admin/Department/Visitor.

### Step 8 — Backend validate visit instance

Backend load `visit_request_campuses` theo `visitInstanceId`.

Validate:

```text
visit instance tồn tại
status = CLOSED
current user có visit_participants.status = ACCEPTED trong đúng visitInstanceId
```

Nếu user là Staff thường:

```text
role_code = STAFF
sub_role = STAFF
participant_role IN (IC_HOST, IC_SUPPORT)
```

Nếu user là Student:

```text
role_code = STUDENT
participant_role = STUDENT
```

Nếu project không cần siết participant_role vì user role đã đủ rõ, vẫn bắt buộc kiểm tra `status = ACCEPTED`.

### Step 9 — Backend validate một chuyến chỉ có một bài

Trước khi insert:

```sql
SELECT 1
FROM news
WHERE visit_instance_id = @visitInstanceId
LIMIT 1
```

Nếu đã có:

```http
409 Conflict
```

Message:

```text
Chuyến tiếp khách này đã có bài viết. Mỗi chuyến chỉ được tạo một bài tin tức.
```

### Step 10 — Backend sanitize dữ liệu

Sanitize:

```text
title
summary
sectionTitle
sectionBodyHtml
```

Giữ rich text an toàn:

```text
p, br, strong, b, em, i, u, s, ul, ol, li, a, blockquote
```

Loại bỏ:

```text
script
iframe
onerror/onload/onclick...
javascript:
style nguy hiểm
HTML không hợp lệ
```

Tách plain text từ `sectionBodyHtml` để lưu `section_body_text`.

### Step 11 — Backend tạo news

Map `news`:

```text
campus_id = visit_request_campuses.campus_id
visit_instance_id = request.visitInstanceId
author_user_id = currentUserId
cover_file_id = request.coverFileId
status = PENDING_REVIEW
submitted_at = NOW()
reviewed_by = NULL
reviewed_at = NULL
review_note = NULL
published_at = NULL
is_featured = FALSE
row_version = 0
created_by = currentUserId
created_at = NOW()
updated_by = NULL hoặc currentUserId theo convention hiện có
updated_at = NULL hoặc NOW theo convention hiện có
```

### Step 12 — Backend tạo translation tiếng Việt

Tạo `news_translations` bản mặc định:

```text
language_code = vi
title = sanitized title
summary = sanitized summary
slug = generated unique slug
seo_title = title
seo_description = summary
```

Slug gợi ý:

```text
{slug-title}-{newsId}
```

Ví dụ:

```text
buoi-tiep-don-doan-dai-hoc-abc-tai-fpt-ha-noi-1001
```

### Step 13 — Backend tạo content sections

Với mỗi section:

```text
news_translation_id = viTranslationId
section_order = 1..10
section_title = sanitized section title
section_body_html = sanitized html
section_body_text = plain text
created_at = NOW()
```

### Step 14 — Backend tạo section files nếu có

Với mỗi section file:

```text
section_id = createdSectionId
file_id = request fileId
usage_type = INLINE_IMAGE hoặc ATTACHMENT
display_order = request displayOrder
created_at = NOW()
```

Validate file tồn tại và user có quyền sử dụng.

### Step 15 — Backend tạo notification cho Staff Leader

Tìm Staff Leader cùng campus:

```text
role_code = STAFF
sub_role = LEADER
primary_campus_id = news.campus_id
status = ACTIVE
```

Hoặc nếu project dùng `campuses.ic_head_user_id`, có thể gửi cho `ic_head_user_id`.

Notification:

```text
title = "Tin tức mới chờ duyệt"
message = "{AuthorName} mới đăng tin tức cần chờ bạn duyệt: {NewsTitle}"
type/category = NEWS_PENDING_REVIEW
target_type = NEWS
target_id = newsId
is_read = false
created_at = NOW()
```

Nếu có nhiều Staff Leader hợp lệ, gửi cho tất cả.

Nếu không tìm thấy Staff Leader:

```text
Vẫn tạo bài PENDING_REVIEW.
Log warning.
Response success kèm warning nếu project hỗ trợ.
```

Không nên fail tạo bài chỉ vì thiếu notification receiver.

### Step 16 — Commit transaction

Các thao tác sau phải nằm trong transaction:

```text
news
news_translations
news_content_sections
news_section_files
notifications
audit
```

Nếu bất kỳ bước nào lỗi, rollback toàn bộ.

---

## 11. Chống trùng khi Staff và Student cùng lúc tạo bài

Vì một chuyến chỉ có một bài news, cần xử lý đồng thời bằng 3 lớp.

### 11.1. Lớp UI

`GET /api/news/eligible-visit-instances` chỉ trả hoặc cho chọn chuyến chưa có bài:

```sql
NOT EXISTS (
  SELECT 1
  FROM news n
  WHERE n.visit_instance_id = vrc.visit_instance_id
)
```

### 11.2. Lớp backend

`POST /api/news` phải check lại trước khi insert:

```sql
EXISTS (
  SELECT 1
  FROM news
  WHERE visit_instance_id = @visitInstanceId
)
```

Nếu đã có, trả:

```http
409 Conflict
```

### 11.3. Lớp database

Bắt buộc thêm unique constraint để chống race condition:

```sql
ALTER TABLE news
ADD UNIQUE KEY uq_news_visit_instance_one_article (visit_instance_id);
```

Nếu đang chỉnh file schema create mới:

```sql
UNIQUE KEY uq_news_visit_instance_one_article (visit_instance_id)
```

Case đồng thời:

```text
Staff và Student cùng thấy chuyến chưa có bài.
Cả hai cùng bấm Gửi duyệt.
Request insert đầu tiên thành công.
Request insert thứ hai bị DB unique constraint chặn.
Backend catch duplicate key và trả 409 Conflict.
```

### 11.4. Không dùng giữ chỗ/draft lock

Không cần tạo cơ chế giữ chỗ khi user chọn chuyến.

Lý do:

```text
News status hiện tại không có DRAFT.
Giữ chỗ/draft lock làm phức tạp schema.
Submit trước thì được tạo bài.
Submit sau nhận 409.
```

---

## 12. Business Rules

| ID | Rule |
|---|---|
| BR-CN-01 | Chỉ Staff thường và Student được tạo news. |
| BR-CN-02 | Staff Leader, HO, Admin, Department, Visitor không được tạo news. |
| BR-CN-03 | User phải chọn một `visitInstanceId`. |
| BR-CN-04 | User chỉ được chọn visit instance mà chính user có `visit_participants.status = ACCEPTED`. |
| BR-CN-05 | Chỉ được tạo news khi `visit_request_campuses.status = CLOSED`. |
| BR-CN-06 | Mỗi `visitInstanceId` chỉ có tối đa 1 bài news. |
| BR-CN-07 | Backend phải có unique constraint `news.visit_instance_id` để chống tạo trùng đồng thời. |
| BR-CN-08 | Backend không nhận `campusId` từ frontend; `campus_id` lấy từ visit instance. |
| BR-CN-09 | Backend không nhận `status` từ frontend; news mới luôn `PENDING_REVIEW`. |
| BR-CN-10 | Bài mới không public ngay; chỉ Staff Leader cùng campus thấy để duyệt. |
| BR-CN-11 | Tác giả chỉ nhìn thấy bài do chính mình tạo trong list author mode. |
| BR-CN-12 | Staff Leader thấy bài thuộc campus mình để duyệt. |
| BR-CN-13 | HO chỉ thấy bài đã `PUBLISHED` trong màn HO read-only, không liên quan create. |
| BR-CN-14 | Tiêu đề bắt buộc, tối đa 150 ký tự. |
| BR-CN-15 | Mô tả ngắn bắt buộc, tối đa 250 ký tự. |
| BR-CN-16 | Nội dung chi tiết bắt buộc có 1–10 section. |
| BR-CN-17 | Rich text phải sanitize trước khi lưu. |
| BR-CN-18 | Nếu có file/ảnh, file phải tồn tại và thuộc quyền sử dụng hợp lệ. |
| BR-CN-19 | Tạo thành công phải gửi notification cho Staff Leader cùng campus nếu có. |
| BR-CN-20 | Toàn bộ thao tác tạo news phải nằm trong transaction. |

---

## 13. Alternative Flows

### AF-01 — Không đăng nhập

```text
POST /api/news không có token
-> 401 Unauthorized
```

### AF-02 — Role không được tạo

```text
HO / Staff Leader / Admin / Department / Visitor gọi POST /api/news
-> 403 Forbidden
```

### AF-03 — User chưa ACCEPTED trong visit instance

```text
Không có visit_participants row
Hoặc status != ACCEPTED
-> 403 Forbidden
```

Message:

```text
Bạn chỉ có thể tạo tin tức cho chuyến tiếp khách mà bạn đã xác nhận tham gia.
```

### AF-04 — Visit instance chưa CLOSED

```text
visit_request_campuses.status != CLOSED
-> 409 Conflict
```

Message:

```text
Chỉ có thể tạo tin tức sau khi chuyến tiếp khách đã đóng đoàn.
```

### AF-05 — Visit instance không tồn tại

```text
visitInstanceId không tồn tại
-> 404 Not Found
```

### AF-06 — Visit instance đã có bài

```text
news.visit_instance_id đã tồn tại
-> 409 Conflict
```

Message:

```text
Chuyến tiếp khách này đã có bài viết. Mỗi chuyến chỉ được tạo một bài tin tức.
```

Nếu bài là của chính current user và còn sửa được:

```text
Trả existingNewsId và canEditExisting = true.
```

Nếu bài do người khác tạo:

```text
Không trả chi tiết bài cho Staff/Student.
```

### AF-07 — Thiếu title/summary/content section

```text
-> 400 Bad Request
```

### AF-08 — Có 11 content sections

```text
-> 400 Bad Request
```

Message:

```text
Mỗi bài tin tức chỉ được tối đa 10 nội dung chi tiết.
```

### AF-09 — File không hợp lệ

```text
coverFileId hoặc section fileId không tồn tại / không được phép dùng
-> 400 hoặc 403 tùy convention
```

### AF-10 — Không tìm thấy Staff Leader để gửi notification

```text
Vẫn tạo news thành công.
Log warning.
Không rollback chỉ vì thiếu notification receiver.
```

---

## 14. Data Mapping

### 14.1. news

| Column | Value |
|---|---|
| `campus_id` | Từ `visit_request_campuses.campus_id` |
| `visit_instance_id` | `request.visitInstanceId` |
| `author_user_id` | `currentUserId` |
| `cover_file_id` | `request.coverFileId` hoặc NULL |
| `status` | `PENDING_REVIEW` |
| `submitted_at` | NOW |
| `reviewed_by` | NULL |
| `reviewed_at` | NULL |
| `review_note` | NULL |
| `published_at` | NULL |
| `is_featured` | FALSE |
| `row_version` | 0 |
| `created_by` | `currentUserId` |
| `created_at` | NOW |
| `updated_by` | NULL hoặc `currentUserId` theo convention |
| `updated_at` | NULL hoặc NOW theo convention |

### 14.2. news_translations

| Column | Value |
|---|---|
| `news_id` | News vừa tạo |
| `language_code` | `vi` |
| `title` | Sanitized title |
| `slug` | Generated unique slug |
| `summary` | Sanitized summary |
| `seo_title` | Sanitized title |
| `seo_description` | Sanitized summary |

### 14.3. news_content_sections

| Column | Value |
|---|---|
| `news_translation_id` | Translation tiếng Việt |
| `section_order` | 1..10 |
| `section_title` | Sanitized section title |
| `section_body_html` | Sanitized rich text HTML |
| `section_body_text` | Plain text từ HTML |
| `created_at` | NOW |

### 14.4. news_section_files

| Column | Value |
|---|---|
| `section_id` | Section vừa tạo |
| `file_id` | FileId được truyền lên |
| `usage_type` | `INLINE_IMAGE` hoặc `ATTACHMENT` |
| `display_order` | Theo request |
| `created_at` | NOW |

### 14.5. notifications

| Column | Value |
|---|---|
| `recipient_user_id` | Staff Leader cùng campus |
| `title` | `Tin tức mới chờ duyệt` |
| `message` | `{AuthorName} mới đăng tin tức cần chờ bạn duyệt: {NewsTitle}` |
| `type/category` | `NEWS_PENDING_REVIEW` |
| `target_type` | `NEWS` |
| `target_id` | `newsId` |
| `is_read` | FALSE |
| `created_at` | NOW |

---

## 15. Backend files cần tạo/cập nhật

### API layer

```text
backend/PEMS.Api/Controllers/NewsController.cs
```

Thêm endpoints:

```text
GET /api/news/eligible-visit-instances
POST /api/news
```

### Application layer

```text
backend/PEMS.Application/News/Queries/GetEligibleVisitInstancesForNews/GetEligibleVisitInstancesForNewsQuery.cs
backend/PEMS.Application/News/Queries/GetEligibleVisitInstancesForNews/GetEligibleVisitInstancesForNewsQueryHandler.cs
backend/PEMS.Application/News/Queries/GetEligibleVisitInstancesForNews/GetEligibleVisitInstancesForNewsDto.cs

backend/PEMS.Application/News/Commands/CreateNews/CreateNewsCommand.cs
backend/PEMS.Application/News/Commands/CreateNews/CreateNewsCommandHandler.cs
backend/PEMS.Application/News/Commands/CreateNews/CreateNewsCommandValidator.cs
backend/PEMS.Application/News/Commands/CreateNews/CreateNewsResponse.cs
```

### Domain constants

```text
backend/PEMS.Domain/Constants/NewsConstants.cs
backend/PEMS.Domain/Constants/VisitParticipantConstants.cs
backend/PEMS.Domain/Constants/VisitInstanceStatus.cs
```

### Infrastructure / DbContext

Đảm bảo có DbSet:

```text
News
NewsTranslation
NewsContentSection
NewsSectionFile
VisitRequestCampus
VisitParticipant
Notification
File
User
Campus
```

### SQL/schema update

Cần thêm unique constraint:

```sql
UNIQUE KEY uq_news_visit_instance_one_article (visit_instance_id)
```

Nếu schema hiện tại đã có unique tương đương thì không tạo trùng.

---

## 16. Validation checklist cho AI Agent

Backend phải pass các case:

```text
[ ] GET /api/news/eligible-visit-instances chỉ trả chuyến user đã ACCEPTED.
[ ] GET /api/news/eligible-visit-instances chỉ trả chuyến status CLOSED.
[ ] GET /api/news/eligible-visit-instances không trả chuyến đã có news nếu includeAlreadyHasNews không bật.
[ ] Card response không trả participantRole vì UI không cần hiển thị vai trò.
[ ] Staff thường đã ACCEPTED trong visit instance CLOSED tạo news thành công.
[ ] Student đã ACCEPTED trong visit instance CLOSED tạo news thành công.
[ ] Staff Leader gọi POST /api/news bị 403.
[ ] HO gọi POST /api/news bị 403.
[ ] Department gọi POST /api/news bị 403.
[ ] Visitor gọi POST /api/news bị 403.
[ ] Staff/Student không thuộc visit instance bị 403.
[ ] Staff/Student thuộc instance nhưng status INVITED bị 403.
[ ] Staff/Student thuộc instance nhưng status DECLINED bị 403.
[ ] Visit instance chưa CLOSED bị 409.
[ ] Visit instance CANCELLED bị 409.
[ ] Visit instance đã có news bị 409.
[ ] Race condition tạo trùng bị DB unique constraint chặn.
[ ] Thiếu title bị 400.
[ ] Title > 150 ký tự bị 400.
[ ] Thiếu summary bị 400.
[ ] Summary > 250 ký tự bị 400.
[ ] Không có content section bị 400.
[ ] Có 11 content sections bị 400.
[ ] HTML nguy hiểm bị sanitize.
[ ] Tạo thành công sinh news.status = PENDING_REVIEW.
[ ] Tạo thành công sinh news_translations language_code = vi.
[ ] Tạo thành công sinh đúng số news_content_sections.
[ ] Tạo thành công tạo notification cho Staff Leader cùng campus nếu có.
[ ] Tạo thành công không set reviewed_by/reviewed_at/published_at.
```

---

## 17. Manual test bằng curl/Postman

### 17.1. Eligible visits — Staff token

```bash
curl -X GET "http://localhost:5265/api/news/eligible-visit-instances"   -H "Authorization: Bearer <STAFF_TOKEN>"
```

Expected:

```text
200 OK
Chỉ có visit instances mà staff đã ACCEPTED, status CLOSED, chưa có news
Không có participantRole trong response
```

### 17.2. Create news success

```bash
curl -X POST "http://localhost:5265/api/news"   -H "Authorization: Bearer <STAFF_TOKEN>"   -H "Content-Type: application/json"   -d '{
    "visitInstanceId": 123,
    "coverFileId": 456,
    "title": "Buổi tiếp đón đoàn Đại học ABC tại FPT Hà Nội",
    "summary": "FPT University Hà Nội đã có buổi tiếp đón và trao đổi hợp tác với đoàn Đại học ABC.",
    "contentSections": [
      {
        "sectionOrder": 1,
        "sectionTitle": "Tổng quan buổi tiếp đón",
        "sectionBodyHtml": "<p>Nội dung chi tiết...</p>",
        "sectionFiles": []
      }
    ]
  }'
```

Expected:

```text
201 Created hoặc 200 OK theo convention
status = PENDING_REVIEW
```

### 17.3. Create news duplicate

```bash
curl -X POST "http://localhost:5265/api/news"   -H "Authorization: Bearer <STUDENT_TOKEN>"   -H "Content-Type: application/json"   -d '{ "... cùng visitInstanceId đã có news ..." }'
```

Expected:

```text
409 Conflict
```

### 17.4. Staff Leader cannot create

```bash
curl -X POST "http://localhost:5265/api/news"   -H "Authorization: Bearer <STAFF_LEADER_TOKEN>"   -H "Content-Type: application/json"   -d '{ "...": "..." }'
```

Expected:

```text
403 Forbidden
```

---

## 18. Phần tham khảo riêng — Bản dịch tiếng Anh

Phần này chưa bắt buộc cho Create News giai đoạn đầu.

Khuyến nghị UI:

```text
4. BẢN DỊCH TIẾNG ANH
[ ] Tạo bản tiếng Anh cho bài viết này
```

Khi bật checkbox:

```text
[ Dịch tự động từ bản tiếng Việt ]
[ Tự nhập bản tiếng Anh ]
```

Nếu dùng dịch tự động:

```text
- Gọi endpoint translate-preview.
- Hiển thị bản EN để user kiểm tra.
- User có thể sửa lại.
- Chỉ lưu bản EN khi user xác nhận.
```

Không nên tự động lưu bản dịch tiếng Anh âm thầm vì nội dung đối ngoại cần chính xác.

Endpoint tham khảo:

```http
POST /api/news/translate-preview
```

Response trả bản nháp tiếng Anh, không lưu DB.

Giai đoạn 1 nên hoàn thiện:

```text
Tạo bài tiếng Việt -> PENDING_REVIEW -> notify Staff Leader
```

Giai đoạn 2 mới thêm:

```text
Translate preview -> User review -> Save EN translation
```

---

## 19. Definition of Done

AI Agent chỉ báo hoàn thành khi đủ:

```text
[ ] Xác nhận đang triển khai Create News.
[ ] Có GET /api/news/eligible-visit-instances.
[ ] Có POST /api/news.
[ ] Staff thường và Student được tạo nếu đã ACCEPTED trong đúng visitInstance.
[ ] Chỉ visitInstance CLOSED mới được tạo news.
[ ] Một visitInstance chỉ có tối đa 1 news.
[ ] Có unique constraint chống trùng visit_instance_id.
[ ] Staff Leader/HO/Admin/Department/Visitor bị 403 khi create.
[ ] Backend không nhận campusId/status từ frontend.
[ ] Tạo news status = PENDING_REVIEW.
[ ] Tạo news_translations language_code = vi.
[ ] Tạo content sections đúng thứ tự, tối đa 10.
[ ] Sanitize rich text.
[ ] Tạo notification cho Staff Leader cùng campus.
[ ] Transaction rollback nếu lỗi.
[ ] dotnet build PASS.
[ ] Có unit/integration test hoặc manual test rõ ràng.
```

---

## 20. Output mong muốn từ AI Agent sau khi code

```text
1. Files read
- ...

2. Files changed
- backend/PEMS.Api/Controllers/NewsController.cs
- backend/PEMS.Application/News/Queries/GetEligibleVisitInstancesForNews/...
- backend/PEMS.Application/News/Commands/CreateNews/...
- backend/PEMS.Domain/Constants/NewsConstants.cs
- database/schema/script nếu thêm unique constraint
- tests/...

3. Endpoints implemented
- GET /api/news/eligible-visit-instances
- POST /api/news

4. Business rules implemented
- Staff/Student only
- user must be ACCEPTED participant of selected visitInstance
- visit instance must be CLOSED
- one news per visitInstance
- status PENDING_REVIEW
- notify Staff Leader

5. Validation
- title/summary/content sections
- files
- rich text sanitize
- duplicate news
- role/scope

6. Test result
- dotnet build: PASS/FAIL
- dotnet test: PASS/FAIL
- Manual API test: PASS/FAIL

7. Notes/Risks
- ...
```
