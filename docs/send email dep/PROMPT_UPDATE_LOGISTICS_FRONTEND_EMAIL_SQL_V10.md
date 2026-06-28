# PROMPT CẬP NHẬT LOGISTICS FRONTEND + BACKEND + EMAIL + SEED SQL THEO SQL v10 FIXED STATUS

> Dùng prompt này cho AI Agent/code assistant để cập nhật chi tiết màn **Quy trình tiếp khách → Trước tiếp khách → Chuẩn bị chi tiết / Logistics**, đồng bộ Backend, Frontend, Email action và Seed SQL theo `pems_full_v10_new_final_email_rich_editor_full_fixed_status.sql`.

---

## 0. Vai trò của bạn

Bạn là AI coding agent đang làm việc trong dự án **PEMS — Partnership Engagement Management System** của FPT University.

Bạn phải làm việc như:

```text
Senior Full-stack Engineer
Senior .NET 8 Clean Architecture Developer
Senior React TypeScript Engineer
Database-first MySQL Engineer
Email Workflow / Token Security Reviewer
Enterprise UI/UX Reviewer
QA / Seed Data Consistency Reviewer
```

Nhiệm vụ của bạn là cập nhật code thật, không mock data, không làm UI giả, không scaffold rỗng.

---

## 1. Nguồn chuẩn bắt buộc phải đọc trước khi sửa

Đọc kỹ các file sau trước khi code:

```text
1. pems_full_v10_new_final_email_rich_editor_full_fixed_status.sql
2. DATABASE_SCHEMA_v8_4_refined_v6_v10_no_dynamic_permissions_FULL_UPDATED.md
3. PEMS_CANONICAL_BUSINESS_RULES_v8_4_refined_v6_v10_FULL_UPDATED.md
4. PEMS_UC_IMPLEMENTATION_RULEBOOK_FRONTEND_BACKEND_DATABASE_VALIDATION_SECURITY_v8_4_refined_v6_v10_FULL_UPDATED.md
5. VISITOR_MANAGEMENT_SYSTEM_v8_4_refined_v6_v10_FULL_UPDATED.md
6. PROJECT_STRUCTURE_FULL.md
7. PEMS_UI_DESIGN_SYSTEM_PROMPT.md
```

Sau đó quét source code thật trong project, tối thiểu các khu vực:

```text
Backend:
- backend/PEMS.Domain/Entities
- backend/PEMS.Domain/Enums
- backend/PEMS.Infrastructure/Persistence hoặc DbContext/configuration tương ứng
- backend/PEMS.Application/Delegations hoặc Logistics-related commands/queries
- backend/PEMS.Application/Emails hoặc Email-related commands/handlers
- backend/PEMS.Api/Controllers/DelegationsController.cs
- backend/PEMS.Api/Controllers/DepartmentReceptionTasksController.cs
- backend/PEMS.Api/Controllers/EmailsController.cs
- backend/PEMS.Api/Controllers/PublicContentController.cs hoặc controller public email action nếu đã có

Frontend:
- frontend/pems-react/src/pages/dashboard/visit/VisitProcess.tsx
- frontend/pems-react/src/features/delegations/components/LogisticsRequestSection.tsx
- frontend/pems-react/src/features/delegations/api/delegationsApi.ts
- frontend/pems-react/src/features/delegations/types/delegations.types.ts
- frontend/pems-react/src/features/emails/**
- frontend/pems-react/src/pages/dashboard/email/** hoặc màn quản lý email hiện tại
```

Nếu tên file thực tế khác, tự tìm bằng từ khóa:

```text
visit_logistics_items
LogisticsRequest
PrepareVisitLogistics
VisitLogistics
email_action_tokens
sent_email_recipients
sent_email_attachments
LOGISTICS_REQUEST_RESPONSE
LOGISTICS_ASSIGNEE_RESPONSE
LOGISTICS_PROPOSAL_RESPONSE
LOGISTICS_HANDOVER_SIGNATURE
```

---

## 2. Quy tắc bắt buộc, không được vi phạm

### 2.1. SQL là nguồn chuẩn

Dự án đang theo hướng **database-first / manual SQL**. SQL mới nhất là nguồn chuẩn cho bảng, field, enum, constraint, status.

Không được tự bịa:

```text
- Bảng mới
- Cột mới
- Enum/status mới
- Permission code mới
- Role mới
- Route mới nếu route hiện tại đã có pattern chuẩn
```

### 2.2. Không tự thay đổi schema nếu chưa được duyệt

Nếu trong quá trình code bạn thấy cần đổi bảng, thêm cột, xóa cột, đổi enum, thêm constraint hoặc tạo bảng mới:

```text
DỪNG LẠI và báo cáo trước.
Không được tự ý sửa schema.
Phải ghi rõ:
1. Vì sao schema hiện tại không đủ.
2. Bảng/cột/enum nào muốn đổi.
3. Tác động tới backend/frontend/seed.
4. SQL patch đề xuất.
5. Chờ người dùng duyệt rồi mới code theo schema mới.
```

Bạn chỉ được cập nhật **seed SQL** nếu dữ liệu seed hiện tại chưa khớp schema/flow, và việc cập nhật seed không làm thay đổi cấu trúc bảng.

### 2.3. Không dùng status cũ

`visit_logistics_items.status` trong SQL fixed status chỉ được dùng:

```text
REQUESTED
CHANGE_PROPOSED
ASSIGNED
ACCEPTED
IN_PROGRESS
DONE
REJECTED
DECLINED
CANCELLED
```

Phải xóa/đổi toàn bộ logic cũ nếu còn:

```text
PLANNED
RECEIVED
READY
COMPLETED
PENDING
```

Lưu ý:

```text
DECLINED = nhân sự được phân công từ chối lần phân công.
REJECTED = từ chối toàn bộ yêu cầu logistics.
CANCELLED = hủy yêu cầu logistics.
DONE = hoàn thành hạng mục.
```

Không được dùng `RECEIVED` trong backend enum, frontend badge, seed, email template, notification code, test data hoặc text điều kiện.

### 2.4. Không làm task transfer trái nghiệp vụ

Không triển khai chuyển nhiệm vụ logistics từ người A sang người B sau khi người A đã nhận nhiệm vụ.

Nếu SQL hiện tại có `visit_logistics_assignment_attempts`, chỉ được dùng để lưu lịch sử **lần phân công** và cho phép phân công lại khi lần trước `DECLINED`, không biến nó thành chức năng transfer sau `ACCEPTED/IN_PROGRESS/DONE`.

### 2.5. Không đưa field ký vào `visit_logistics_items`

Không cập nhật ký mượn/ký trả vào `visit_logistics_items`.

Tất cả ký mượn/ký trả phải dùng:

```text
visit_logistics_item_handovers
```

---

## 3. Mục tiêu cập nhật tổng thể

Cập nhật module Logistics trong quy trình tiếp khách để khớp SQL v10 và UI hiện tại.

Hiện frontend ở màn **Quy trình tiếp khách → Trước tiếp khách → Chuẩn bị chi tiết** mới đủ để gửi yêu cầu cơ bản, nhưng còn thiếu các phần sau:

```text
1. priority — mức ưu tiên.
2. due_at — hạn phản hồi/hoàn thành.
3. coordination_mode cho mọi loại resource, không chỉ Welcome LED.
4. offline_coordination_note khi đã trao đổi bên ngoài hệ thống.
5. Card trạng thái sau khi đã gửi yêu cầu.
6. UI xem/chấp nhận/từ chối đề xuất thay đổi khi status = CHANGE_PROPOSED.
7. UI ký mượn/ký trả ở giai đoạn Đang/Sau tiếp khách bằng visit_logistics_item_handovers.
8. Email action tương ứng với request/assignee/proposal/handover.
9. Seed SQL nếu dữ liệu mẫu/template chưa đủ để test flow.
```

---

## 4. SQL field mapping bắt buộc cho `visit_logistics_items`

Đối chiếu chính xác với SQL:

```sql
visit_logistics_items (
  logistics_item_id,
  visit_instance_id,
  item_type,
  title,
  description,
  coordination_mode,
  offline_coordination_note,
  quantity,
  usage_start_at,
  usage_end_at,
  status,
  priority,
  requested_by,
  requested_to_department_id,
  requested_at,
  received_by,
  received_at,
  assigned_to_user_id,
  assigned_by,
  assigned_at,
  assignee_accepted_at,
  assignee_response_note,
  due_at,
  completed_at,
  proposed_by,
  proposed_at,
  proposed_quantity,
  proposed_usage_start_at,
  proposed_usage_end_at,
  proposed_description,
  proposal_note,
  proposal_responded_by,
  proposal_responded_at,
  proposal_response,
  proposal_response_note,
  decision_note,
  row_version,
  created_at,
  created_by,
  updated_at,
  updated_by
)
```

Frontend form chỉ nhập các field nghiệp vụ cần thiết:

| Field SQL | Nguồn UI | Ghi chú |
|---|---|---|
| `item_type` | Loại card: ROOM, TRANSPORT, MEAL, EQUIPMENT, BANNER, LED, OTHER | Không cho nhập tự do nếu là item cố định |
| `title` | Tên mục: Welcome LED, Xe điện, Người lái, Phòng họp, Teabreak, Yêu cầu khác | `OTHER` cho phép nhập tiêu đề |
| `description` | Ghi chú/nội dung chi tiết | Không nhầm với `decision_note` |
| `coordination_mode` | Radio gửi qua hệ thống / đã trao đổi bên ngoài | Bắt buộc khi chọn cần hỗ trợ |
| `offline_coordination_note` | Ghi chú trao đổi ngoài | Chỉ hiện khi `OFFLINE_COORDINATED` |
| `quantity` | Số lượng/số phòng/số suất | Validate >= 1 nếu item cần số lượng |
| `usage_start_at` | Thời gian bắt đầu sử dụng | Validate không ở quá khứ với request mới, trừ khi đang xem/sửa dữ liệu cũ |
| `usage_end_at` | Thời gian kết thúc sử dụng | Phải > `usage_start_at` |
| `priority` | Dropdown mức ưu tiên | Default `MEDIUM` |
| `requested_to_department_id` | Phòng ban xử lý | Bắt buộc với `SYSTEM_REQUEST`; optional với `OFFLINE_COORDINATED` nếu nghiệp vụ hiện tại cho phép |
| `due_at` | Hạn phản hồi/hoàn thành | Nên <= `usage_start_at`; nếu không enforce thì cảnh báo |

Backend tự set các field sau, frontend không cho user nhập trực tiếp:

```text
requested_by
requested_at
received_by
received_at
assigned_to_user_id
assigned_by
assigned_at
assignee_accepted_at
completed_at
proposed_by
proposed_at
proposal_responded_by
proposal_responded_at
created_at
created_by
updated_at
updated_by
row_version
```

---

## 5. Cập nhật Frontend Logistics UI

### 5.1. Các card logistics cần hỗ trợ

Trong màn hiện tại đang có các nhóm:

```text
Mục 1: Welcome LED
Mục 2: Chuẩn bị cho Campus Tour
  - Xe điện
  - Người lái
Mục 3: Chuẩn bị cho họp
  - Phòng họp
  - Teabreak
Mục 4: Khác
  - Yêu cầu khác
```

Phải cập nhật mỗi card theo cùng pattern thống nhất.

### 5.2. Radio lựa chọn nhu cầu

Mỗi card logistics phải có 3 trạng thái lựa chọn:

```text
1. Không cần
2. Cần hỗ trợ — gửi yêu cầu qua hệ thống
3. Cần hỗ trợ — đã trao đổi bên ngoài hệ thống
```

Mapping:

```text
Không cần:
- Nếu chưa có logistics item: không tạo row.
- Nếu đã có item và user hủy nhu cầu: gọi API cancel nếu trạng thái cho phép, set status = CANCELLED, lưu decision_note nếu có.

Cần hỗ trợ — gửi yêu cầu qua hệ thống:
- coordination_mode = SYSTEM_REQUEST
- Bắt buộc chọn phòng ban xử lý.
- Khi bấm gửi, backend tạo/cập nhật logistics item và gửi email/action token nếu flow yêu cầu.

Cần hỗ trợ — đã trao đổi bên ngoài hệ thống:
- coordination_mode = OFFLINE_COORDINATED
- Hiện field offline_coordination_note bắt buộc.
- Không gửi email action request tới Department Leader nếu không cần xử lý qua hệ thống.
- Không bắt buộc chọn phòng ban nếu nghiệp vụ hiện tại coi đây là record ghi nhận ngoài hệ thống.
- Nếu backend hiện đang bắt department bắt buộc cho mọi item, không tự đổi schema; hãy điều chỉnh validation theo SQL vì requested_to_department_id đang nullable, hoặc báo rõ nếu có conflict.
```

Nếu hiện code chỉ có pattern này cho Welcome LED thì phải nhân rộng sang Xe điện, Người lái, Phòng họp, Teabreak và Yêu cầu khác.

### 5.3. Field bắt buộc trong mỗi card khi chọn cần hỗ trợ

Thêm/đảm bảo các field sau:

```text
- Số lượng / số phòng / số suất
- Thời gian bắt đầu sử dụng
- Thời gian kết thúc sử dụng
- Mức ưu tiên
- Hạn phản hồi/hoàn thành
- Ghi chú chi tiết
- Phòng ban xử lý nếu SYSTEM_REQUEST
- Ghi chú trao đổi ngoài nếu OFFLINE_COORDINATED
```

UI label gợi ý:

```text
Mức ưu tiên *
Hạn cần phản hồi / hoàn thành
Ghi chú xử lý ngoài hệ thống *
```

Dropdown priority:

```text
LOW     -> Thấp
MEDIUM  -> Trung bình
HIGH    -> Cao
URGENT  -> Khẩn cấp
```

Default priority: `MEDIUM`.

### 5.4. Validate frontend

Phải validate trước khi gọi API:

```text
1. quantity phải là số nguyên >= 1 nếu item yêu cầu số lượng.
2. Không cho số âm, số 0, NaN, chuỗi rỗng sau trim.
3. Tự normalize số có leading zero: 0002 -> 2.
4. usage_start_at bắt buộc nếu gửi yêu cầu qua hệ thống.
5. usage_end_at bắt buộc nếu gửi yêu cầu qua hệ thống.
6. usage_end_at > usage_start_at.
7. Với request mới, usage_start_at không được nằm trong quá khứ.
8. due_at nếu nhập thì không được nằm trong quá khứ.
9. due_at nên <= usage_start_at; nếu rule hiện tại chưa chốt, dùng warning hoặc báo lại trước khi enforce cứng.
10. SYSTEM_REQUEST bắt buộc có requested_to_department_id.
11. OFFLINE_COORDINATED bắt buộc có offline_coordination_note.
12. OTHER bắt buộc có title.
```

Không để lỗi hiện tại tái diễn:

```text
- Dropdown chọn phòng ban bị ẩn giá trị.
- Date chưa validate ngày quá khứ.
- Number input cho số âm hoặc 0002 không normalize.
- Button trong email/logistics chưa cập nhật theo yêu cầu.
```

### 5.5. Card trạng thái sau khi gửi yêu cầu

Sau khi item đã được gửi/tồn tại trong DB, frontend không chỉ hiển thị form trống. Phải hiển thị card trạng thái/read-only summary:

```text
- Tên hạng mục
- Badge status
- Mức ưu tiên
- Phòng ban xử lý
- Người gửi + thời điểm gửi
- Người tiếp nhận + thời điểm tiếp nhận nếu có
- Nhân sự được phân công + thời điểm phân công nếu có
- Ghi chú phản hồi của assignee nếu có
- Hạn hoàn thành
- Thời điểm hoàn thành nếu có
- Lý do từ chối/hủy nếu status REJECTED/CANCELLED/DECLINED
```

Không cho bấm “Gửi yêu cầu” lặp lại nếu item đang ở:

```text
REQUESTED
CHANGE_PROPOSED
ASSIGNED
ACCEPTED
IN_PROGRESS
DONE
```

Chỉ cho sửa/hủy theo đúng rule hiện tại và trạng thái cho phép.

### 5.6. UI proposal khi Department đề xuất thay đổi

Khi logistics item có:

```text
status = CHANGE_PROPOSED
```

và có các field `proposed_*`, Host/IC Staff phải thấy card:

```text
Đề xuất thay đổi từ phòng ban
- Số lượng gốc -> số lượng đề xuất
- Thời gian bắt đầu/kết thúc gốc -> thời gian đề xuất
- Nội dung gốc -> nội dung đề xuất
- Lý do đề xuất / proposal_note
- Nút Chấp nhận đề xuất
- Nút Từ chối đề xuất
- Ô ghi chú phản hồi đề xuất
```

Mapping:

```text
Chấp nhận đề xuất:
- proposal_response = ACCEPTED
- proposal_response_note = ghi chú nếu có
- backend cập nhật các field gốc theo proposed_* nếu business rule hiện tại cho phép
- status chuyển theo flow đã chốt, ví dụ ACCEPTED/ASSIGNED/REQUESTED tùy handler hiện tại; không tự bịa nếu chưa có rule, phải kiểm tra code và báo rõ.

Từ chối đề xuất:
- proposal_response = REJECTED
- proposal_response_note bắt buộc hoặc optional theo validator hiện tại
- status quay về trạng thái phù hợp hoặc REJECTED theo flow hiện tại; không tự bịa status mới.
```

Nếu backend chưa có endpoint proposal response, bổ sung command/query đúng Clean Architecture.

---

## 6. Cập nhật Frontend cho ký mượn/ký trả

Không nhét ký mượn/ký trả vào form tạo yêu cầu ban đầu.

Thêm UI ở tab phù hợp:

```text
- Tab 2: Đang tiếp khách
- Hoặc Tab 3: Sau tiếp khách
```

Tùy flow hiện tại của `VisitDuringTab` và `VisitAfterTab`.

Dùng bảng:

```text
visit_logistics_item_handovers
```

Field cần hiển thị:

```text
handover_type: BORROW / RETURN
borrower_signed_by
borrower_signed_at
provider_signed_by
provider_signed_at
item_condition
condition_note
attachment_file_id
```

UI gợi ý:

```text
Ký bàn giao / mượn
- Bên mượn ký nhận
- Bên cho mượn ký bàn giao

Ký trả / nhận lại
- Bên mượn ký trả
- Bên cho mượn ký nhận lại

Tình trạng tài sản
- Tốt
- Hư hỏng
- Thiếu/mất
- Khác
Ghi chú tình trạng
Ảnh/file biên bản đính kèm
```

Quy tắc ký:

```text
1. BORROW phải được tạo/cập nhật khi giao/mượn resource.
2. RETURN phải được tạo/cập nhật khi trả/nhận lại resource.
3. Mỗi logistics item tối đa có 1 BORROW và 1 RETURN nhờ unique (logistics_item_id, handover_type).
4. Không cho ký RETURN nếu BORROW chưa hoàn tất đủ chữ ký cần thiết.
5. attachment_file_id nếu có phải trỏ tới files, file binary lưu qua Google Drive foundation hiện có.
6. Nếu cần gửi link ký qua email, dùng email_action_tokens với action_context = LOGISTICS_HANDOVER_SIGNATURE.
```

---

## 7. Cập nhật Backend logistics

### 7.1. Entity/Enum/DbContext

Đảm bảo backend khớp SQL mới:

```text
visit_logistics_items:
- Có coordination_mode
- Có offline_coordination_note
- Có priority
- Có due_at
- Có proposal fields
- Không còn status cũ
- Không còn handover_confirmed_* hoặc service_report_* trong entity

visit_logistics_item_handovers:
- Có entity/configuration nếu chưa có
- Có unique (logistics_item_id, handover_type)
- Có FK file attachment_file_id

email_action_tokens:
- Có entity/configuration nếu chưa có
- Không lưu token raw

sent_email_attachments / email_drafts nếu SQL đã có:
- Entity/configuration phải khớp SQL nếu code sử dụng.
```

Nếu entity/enum/backend còn `Received`, `Planned`, `Ready`, phải xóa hoặc map lại đúng theo SQL.

### 7.2. API/DTO

Cập nhật DTO request/response logistics để có:

```text
itemType
title
description
coordinationMode
offlineCoordinationNote
quantity
usageStartAt
usageEndAt
priority
requestedToDepartmentId
dueAt
rowVersion nếu update cần optimistic concurrency
```

Response nên trả thêm:

```text
logisticsItemId
status
requestedByName
requestedAt
requestedDepartmentName
receivedByName
receivedAt
assignedToUserName
assignedAt
assigneeAcceptedAt
assigneeResponseNote
completedAt
decisionNote
proposedQuantity
proposedUsageStartAt
proposedUsageEndAt
proposedDescription
proposalNote
proposalResponse
proposalResponseNote
handoverSummary nếu cần
```

Không trả dữ liệu nhạy cảm không cần thiết.

### 7.3. Validation backend

Backend là lớp bảo vệ cuối cùng. Không chỉ dựa vào frontend.

Validator/handler phải enforce:

```text
1. visitInstanceId tồn tại và current user có quyền Host/IC Staff với instance đó.
2. itemType thuộc enum SQL.
3. title bắt buộc, max 255.
4. coordinationMode bắt buộc, thuộc SYSTEM_REQUEST/OFFLINE_COORDINATED.
5. OFFLINE_COORDINATED bắt buộc có offlineCoordinationNote sau trim.
6. SYSTEM_REQUEST bắt buộc có requestedToDepartmentId là GENERAL department cùng campus, ACTIVE.
7. quantity null hoặc >= 1 tùy item type; nếu user nhập thì phải >= 1.
8. usageEndAt > usageStartAt nếu cả hai có giá trị.
9. request mới không được dùng usageStartAt trong quá khứ, trừ case đang cập nhật dữ liệu cũ có lý do rõ.
10. dueAt nếu có không được trong quá khứ.
11. priority thuộc LOW/MEDIUM/HIGH/URGENT.
12. Không cho update item ở trạng thái terminal không hợp lệ: DONE/CANCELLED/REJECTED, trừ endpoint chuyên biệt nếu có rule.
13. Không cho gửi lại item trùng loại nếu item hiện tại đang active.
14. Không cho đổi assigned_to_user_id sau khi đã ACCEPTED/IN_PROGRESS/DONE.
```

### 7.4. Status transition đề xuất cần kiểm tra source trước khi áp dụng

Không tự bịa transition. Hãy kiểm tra code hiện có. Nếu chưa có chuẩn, báo lại trước khi thay đổi.

Transition mong muốn ở mức nghiệp vụ:

```text
SYSTEM_REQUEST:
REQUESTED -> ASSIGNED -> ACCEPTED -> IN_PROGRESS -> DONE
REQUESTED -> REJECTED
ASSIGNED -> DECLINED nếu assignee từ chối lần phân công
REQUESTED/ASSIGNED/ACCEPTED -> CHANGE_PROPOSED nếu phòng ban đề xuất thay đổi
CHANGE_PROPOSED -> ACCEPTED/ASSIGNED/REQUESTED sau khi Host chấp nhận, tùy flow hiện tại
CHANGE_PROPOSED -> REJECTED hoặc quay lại trạng thái trước sau khi Host từ chối, tùy flow hiện tại
Bất kỳ trạng thái còn cho phép -> CANCELLED nếu Host/IC hủy trước khi hoàn thành

OFFLINE_COORDINATED:
Không chạy email/action flow phòng ban nếu chỉ ghi nhận ngoài hệ thống.
Status xử lý theo rule hiện có. Nếu chưa có, báo lại đề xuất trước khi code cứng.
```

### 7.5. Clean Architecture

Tuân thủ kiến trúc hiện tại:

```text
- Controller chỉ nhận request, gọi MediatR, trả response.
- Không nhét business logic vào Controller.
- Validator xử lý input validation.
- Handler xử lý business validation và update DB.
- Transaction theo pipeline nếu project đã có.
- Audit log nếu project đang dùng AuditLogBehaviour.
- Không try-catch bừa bãi trong handler nếu middleware đã xử lý exception.
```

---

## 8. Cập nhật Email workflow

### 8.1. Phạm vi email cần cập nhật

Email phải hỗ trợ đầy đủ logistics flow mới:

```text
1. Host/IC gửi request logistics cho Department Leader.
2. Department Leader accept/decline/negotiation hoặc xử lý request.
3. Department Leader phân công nhân sự phòng ban.
4. Nhân sự phòng ban accept/decline assignment.
5. Department gửi đề xuất thay đổi logistics cho Host/IC.
6. Host/IC accept/reject proposal.
7. Email ký mượn/ký trả nếu flow dùng email action.
8. Sent email phải tracking recipient và trạng thái bấm nút.
```

### 8.2. Bảng email phải dùng

Dựa đúng SQL:

```text
email_templates
sent_emails
sent_email_recipients
sent_email_attachments nếu có file/ảnh đính kèm
email_action_tokens
```

Không thêm inbox thật:

```text
Không thêm email_threads.
Không thêm email_messages.
Không đọc Gmail inbox.
Không sync mailbox phản hồi tự do.
```

### 8.3. Email action token

Mỗi nút trong email phải tạo token một lần:

```text
action_context:
- PARTICIPATION_RESPONSE
- LOGISTICS_REQUEST_RESPONSE
- LOGISTICS_ASSIGNEE_RESPONSE
- LOGISTICS_NEGOTIATION
- LOGISTICS_PROPOSAL_RESPONSE
- LOGISTICS_HANDOVER_SIGNATURE

target_type:
- VISIT_PARTICIPANT
- LOGISTICS_ITEM
- LOGISTICS_HANDOVER

intended_action:
- ACCEPT
- DECLINE
- NEGOTIATE
- APPROVE_PROPOSAL
- REJECT_PROPOSAL
- CONFIRM_BORROW
- CONFIRM_RETURN

result_status:
- PENDING
- SUCCESS
- ALREADY_RESPONDED
- EXPIRED
- INVALID
- FAILED
```

Security bắt buộc:

```text
1. Link email chứa raw token, DB chỉ lưu token_hash.
2. Token phải có expires_at.
3. Token chỉ dùng một lần.
4. Các token cùng một nhóm quyết định dùng chung action_group_key.
5. Nếu người nhận đã phản hồi bằng một nút rồi, bấm nút khác phải trả ALREADY_RESPONDED, không update nghiệp vụ lần hai.
6. Ghi used_at, used_action, result_status, result_message, used_ip, used_user_agent.
7. Validate target_type + target_id + status hiện tại trước khi update nghiệp vụ.
8. Public endpoint có thể AllowAnonymous nhưng handler phải validate token nghiêm ngặt.
```

### 8.4. Các email logistics bắt buộc có nút

#### A. Email Host/IC gửi request logistics cho Department Leader

Context:

```text
LOGISTICS_REQUEST_RESPONSE
```

Nút:

```text
- Đồng ý tiếp nhận / ACCEPT
- Từ chối yêu cầu / DECLINE
- Đề xuất thay đổi / NEGOTIATE nếu flow hiện có hỗ trợ negotiation trực tiếp
```

Khi Department Leader bấm:

```text
ACCEPT:
- Update logistics item theo flow tiếp nhận.
- Set received_by, received_at nếu nghiệp vụ có bước tiếp nhận.
- result_status = SUCCESS.

DECLINE:
- status = REJECTED.
- decision_note bắt buộc nếu endpoint cho nhập lý do.
- result_status = SUCCESS.

Nếu item đã được xử lý:
- Không update lần hai.
- result_status = ALREADY_RESPONDED.
```

#### B. Email Department Leader phân công nhân sự

Context:

```text
LOGISTICS_ASSIGNEE_RESPONSE
```

Nút:

```text
- Nhận nhiệm vụ / ACCEPT
- Từ chối nhiệm vụ / DECLINE
```

Khi assignee bấm:

```text
ACCEPT:
- status = ACCEPTED hoặc update assignment attempt = ACCEPTED theo code hiện có.
- assignee_accepted_at = NOW().
- result_status = SUCCESS.

DECLINE:
- status = DECLINED hoặc assignment attempt = DECLINED.
- assignee_response_note nên bắt buộc nếu UI public action có form nhập lý do.
- Cho Department Leader phân công lại nếu lần trước DECLINED, không coi là transfer sau khi accepted.
```

#### C. Email đề xuất thay đổi logistics

Context:

```text
LOGISTICS_PROPOSAL_RESPONSE
```

Nút:

```text
- Chấp nhận đề xuất / APPROVE_PROPOSAL
- Từ chối đề xuất / REJECT_PROPOSAL
```

Khi Host/IC bấm:

```text
APPROVE_PROPOSAL:
- proposal_response = ACCEPTED.
- proposal_responded_by / proposal_responded_at được set.
- Apply proposed_* vào field gốc nếu handler hiện có quy định.
- Không tự bịa status mới.

REJECT_PROPOSAL:
- proposal_response = REJECTED.
- proposal_response_note nếu có.
- Không apply proposed_*.
```

#### D. Email ký mượn/ký trả

Context:

```text
LOGISTICS_HANDOVER_SIGNATURE
```

Target:

```text
LOGISTICS_HANDOVER
```

Nút:

```text
- Xác nhận ký mượn/bàn giao / CONFIRM_BORROW
- Xác nhận ký trả/nhận lại / CONFIRM_RETURN
```

Khi bấm:

```text
- Update visit_logistics_item_handovers.
- Không update ký vào visit_logistics_items.
- Nếu cần biết là borrower hay provider, handler phải xác định từ token recipient/context hoặc endpoint phải có đủ metadata hiện có trong code. Nếu schema chưa đủ metadata để phân biệt vai trò ký, báo lại trước khi sửa schema.
```

### 8.5. Email attachment / inline image

Nếu chức năng gửi email có kèm file hoặc ảnh:

```text
1. Không lưu binary local lâu dài.
2. Dùng Google Drive upload foundation đã tích hợp.
3. Lưu metadata file vào bảng files.
4. Lưu mapping email attachment vào sent_email_attachments nếu SQL hiện tại có bảng này.
5. Inline image dùng content_id và body HTML dạng cid nếu flow hiện tại hỗ trợ.
6. Không tự thêm bảng attachment mới.
```

### 8.6. Email template variables

Cập nhật template variables để hỗ trợ logistics mới:

```text
{{VisitTitle}}
{{VisitCode}}
{{CampusName}}
{{ItemTitle}}
{{ItemType}}
{{Quantity}}
{{UsageStartAt}}
{{UsageEndAt}}
{{DueAt}}
{{Priority}}
{{RequestedByName}}
{{RequestedDepartmentName}}
{{Description}}
{{OfflineCoordinationNote}}
{{ProposalNote}}
{{ProposedQuantity}}
{{ProposedUsageStartAt}}
{{ProposedUsageEndAt}}
{{ProposedDescription}}
{{AcceptUrl}}
{{DeclineUrl}}
{{NegotiateUrl}}
{{ApproveProposalUrl}}
{{RejectProposalUrl}}
{{ConfirmBorrowUrl}}
{{ConfirmReturnUrl}}
{{DetailUrl}}
```

Email template editor phải normalize biến cả dạng:

```text
{{VariableName}}
%7B%7BVariableName%7D%7D
```

Không để preview lộ HTML thô nếu body_format = HTML.

---

## 9. Cập nhật Email Management UI

Màn quản lý email phải hiển thị đúng scope v10:

```text
- Email gửi đi/outbox
- Delivery tracking theo từng recipient
- Trạng thái email action token nếu email có nút
- Không hiển thị inbox thật
```

Cần bổ sung/đảm bảo:

```text
1. sent_emails.status hiển thị đúng: QUEUED, SENT, DELIVERED, FAILED, PARTIAL_FAILED, BOUNCED.
2. sent_email_recipients.delivery_status hiển thị từng người nhận.
3. email_action_tokens.result_status hiển thị nếu email có nút action.
4. Nếu token đã dùng, hiển thị used_action + used_at + result_message.
5. Nếu token hết hạn, hiển thị EXPIRED.
6. Không toast “Gửi email thành công” khi backend trả status FAILED/PARTIAL_FAILED.
7. Frontend phải đọc response success/status từ backend, không hard-code success theo HTTP 200.
```

---

## 10. Cập nhật Seed SQL nếu cần

Được phép cập nhật seed SQL nếu cần để test đầy đủ flow, nhưng không thay đổi schema.

Seed cần có dữ liệu mẫu cho:

```text
1. Logistics item SYSTEM_REQUEST với priority/due_at.
2. Logistics item OFFLINE_COORDINATED với offline_coordination_note.
3. Logistics item CHANGE_PROPOSED có proposed_* và proposal_note.
4. Logistics item ASSIGNED/ACCEPTED/IN_PROGRESS/DONE để test badge/card trạng thái.
5. Logistics item REJECTED/DECLINED/CANCELLED để test reason.
6. Handover BORROW/RETURN mẫu trong visit_logistics_item_handovers.
7. Email templates cho logistics request/assignee/proposal/handover.
8. sent_emails + sent_email_recipients mẫu nếu project đang seed email history.
9. email_action_tokens mẫu với PENDING/SUCCESS/ALREADY_RESPONDED/EXPIRED nếu cần test UI trạng thái.
```

Seed không được có status cũ:

```text
PLANNED
RECEIVED
READY
```

Nếu seed hiện tại còn status cũ, phải sửa hoặc báo cáo rõ file seed nào còn sai.

Seed phải tôn trọng FK:

```text
- visit_instance_id tồn tại.
- requested_by là user hợp lệ.
- requested_to_department_id là GENERAL department cùng campus nếu SYSTEM_REQUEST.
- assigned_to_user_id là user DEPARTMENT/STAFF hợp lệ theo scope phòng ban.
- file_id trong attachment_file_id tồn tại nếu seed handover attachment.
```

---

## 11. UI/UX yêu cầu

Giữ phong cách enterprise dashboard hiện tại của PEMS:

```text
Primary blue: #004c91
Primary orange: #F37021
Card: rounded-2xl border border-slate-200 bg-white shadow-sm
Text chính: slate-800/slate-900
Text phụ: slate-500/slate-600
Không gradient mạnh
Không shadow quá đậm
Không phá responsive
Không refactor UI quá sâu nếu không cần
```

Cần sửa các lỗi UI hiện tại nếu có liên quan:

```text
1. Dropdown chọn phòng ban không được mất giá trị đang chọn.
2. Date/time input phải có lỗi inline rõ ràng.
3. Number input không cho âm/0 và normalize leading zero.
4. Button gửi yêu cầu không bị hiện khi item không còn được gửi lại.
5. Card trạng thái phải dễ nhìn, không lồng card quá nhiều.
6. Mobile/tablet không được horizontal scroll.
```

---

## 12. Backend/Frontend build và kiểm thử

Sau khi sửa phải chạy:

```text
Backend:
- dotnet build
- dotnet test nếu project có test tương ứng

Frontend:
- npm install nếu cần
- npm run build
- npm run lint nếu project có
```

Nếu không chạy được vì thiếu môi trường, phải báo rõ:

```text
- Lệnh đã thử chạy
- Lỗi cụ thể
- Vì sao không chạy được
- Những phần đã kiểm tra thủ công
```

---

## 13. Manual test checklist

Phải test tối thiểu các case:

### Case 1 — SYSTEM_REQUEST hợp lệ

```text
Given Host/IC Staff mở VisitProcess tab Trước tiếp khách
When chọn Cần hỗ trợ — gửi yêu cầu qua hệ thống
And nhập quantity = 2, usage_start_at future, usage_end_at > start, priority MEDIUM, due_at hợp lệ, chọn department
And bấm Gửi yêu cầu
Then tạo/cập nhật visit_logistics_items với coordination_mode SYSTEM_REQUEST
And status đúng flow
And sent_emails/sent_email_recipients/email_action_tokens được tạo nếu flow email request yêu cầu
And UI chuyển sang card trạng thái
```

### Case 2 — OFFLINE_COORDINATED

```text
Given Host/IC Staff chọn Cần hỗ trợ — đã trao đổi bên ngoài hệ thống
When nhập offline_coordination_note và thông tin cần ghi nhận
Then backend lưu coordination_mode OFFLINE_COORDINATED
And lưu offline_coordination_note
And không gửi email action request nếu không xử lý qua hệ thống
```

### Case 3 — Validate số lượng và thời gian

```text
quantity = -1 -> lỗi
quantity = 0 -> lỗi
quantity = 0002 -> normalize thành 2
usage_start_at quá khứ với request mới -> lỗi
usage_end_at <= usage_start_at -> lỗi
```

### Case 4 — Proposal

```text
Given logistics item status CHANGE_PROPOSED
When Host/IC mở màn logistics
Then thấy card đề xuất thay đổi
When bấm Chấp nhận đề xuất
Then proposal_response = ACCEPTED và flow cập nhật đúng
When bấm Từ chối đề xuất
Then proposal_response = REJECTED, không apply proposed_*
```

### Case 5 — Email action one-time

```text
Given email có 2 nút ACCEPT/DECLINE cùng action_group_key
When người nhận bấm ACCEPT lần đầu
Then nghiệp vụ được update, token SUCCESS
When bấm DECLINE hoặc bấm lại ACCEPT
Then không update lần hai, trả ALREADY_RESPONDED
```

### Case 6 — Assignee decline và phân công lại

```text
Given Department Leader phân công nhân sự A
When A bấm DECLINE và nhập lý do
Then assignment attempt DECLINED hoặc item status DECLINED theo code hiện có
And Department Leader được phân công lại nhân sự khác nếu flow hiện tại cho phép
But nếu item đã ACCEPTED/IN_PROGRESS/DONE thì không được đổi assignee
```

### Case 7 — Handover

```text
Given logistics item cần ký mượn/trả
When ký BORROW
Then insert/update visit_logistics_item_handovers handover_type BORROW
When ký RETURN
Then insert/update handover_type RETURN
And không update field ký vào visit_logistics_items
```

### Case 8 — Email management UI

```text
Given email gửi logistics có recipients và action tokens
When mở màn quản lý email
Then thấy trạng thái gửi email, trạng thái từng recipient, trạng thái nút đã bấm/chưa bấm/hết hạn
And không có inbox thật
```

---

## 14. Output báo cáo bắt buộc sau khi code

Sau khi hoàn thành, báo cáo theo format:

```text
1. Tóm tắt vấn đề gốc
2. Files đã sửa
3. Backend đã cập nhật gì
4. Frontend đã cập nhật gì
5. Email workflow đã cập nhật gì
6. Seed SQL có sửa không, sửa file nào
7. Có thay đổi schema không?
   - Nếu không: ghi rõ không đổi bảng/cột/enum.
   - Nếu có nhu cầu đổi: chưa tự đổi, đã báo đề xuất.
8. Status cũ đã được xóa ở đâu
9. Build/test đã chạy
10. Manual test checklist kết quả
11. Những điểm còn rủi ro/chưa chốt cần người dùng quyết định
```

Không được báo “đã hoàn thành” nếu:

```text
- Chưa build được mà không nói rõ.
- Chưa update email action.
- Chưa kiểm tra seed/status cũ.
- Chưa đảm bảo không dùng status RECEIVED/PLANNED/READY.
- Tự ý đổi schema mà chưa báo.
```

---

## 15. Kết quả mong muốn cuối cùng

Sau khi cập nhật, hệ thống phải đạt:

```text
1. Frontend logistics form khớp đủ field quan trọng của visit_logistics_items.
2. Host/IC Staff gửi request logistics có priority, due_at, coordination_mode, offline note.
3. UI hiển thị trạng thái sau khi gửi, không cho gửi lặp sai flow.
4. Department proposal hiển thị và phản hồi được.
5. Ký mượn/ký trả dùng visit_logistics_item_handovers, không dùng field cũ.
6. Email logistics có nút action đúng context, token một lần, tracking trạng thái bấm.
7. Email management hiển thị sent/outbox/delivery/action status, không inbox thật.
8. Seed SQL đủ dữ liệu mẫu để test flow nếu cần.
9. Không có status cũ PLANNED/RECEIVED/READY trong logistics.
10. Không thay đổi schema nếu chưa được duyệt.
```
