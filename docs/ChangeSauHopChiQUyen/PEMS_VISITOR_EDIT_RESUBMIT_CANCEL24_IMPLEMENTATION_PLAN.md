# Kế hoạch triển khai chi tiết — Visitor sửa đơn, gửi lại sau từ chối, hủy trước 24h, Host hủy trước tiếp khách

## 0. Mục tiêu cập nhật

Tài liệu này dùng để triển khai code sau khi database đã được cập nhật theo file SQL mới:

```text
pems_full_v10_new_final_campus_independent_approval_self_host_transport_note_resubmit_agenda_cancel24_FULL_UPDATED.sql
```

Các nghiệp vụ cần triển khai:

```text
1. Visitor được sửa đơn khi đơn còn chờ xử lý và chưa có cơ sở nào ra quyết định.
2. Visitor được sửa & gửi lại đơn sau khi toàn bộ request bị từ chối.
3. Visitor chỉ được hủy đơn/lịch trước thời điểm diễn ra tối thiểu 24 giờ.
4. Host chỉ được hủy campus instance trước khi bắt đầu tiếp khách.
5. Không cho hủy khi campus đã DURING_VISIT / AFTER_VISIT / CLOSED.
6. Từ DURING_VISIT trở đi, campus bắt buộc phải có agenda.
7. Khi resubmit, phải lưu snapshot lý do từ chối cũ vào audit trước khi clear decision fields.
```

---

## 1. Database đã cập nhật

### 1.1. File SQL mới

Sử dụng file SQL mới làm baseline:

```text
pems_full_v10_new_final_campus_independent_approval_self_host_transport_note_resubmit_agenda_cancel24_FULL_UPDATED.sql
```

Không dùng lại các bản SQL cũ còn thiếu logic:

```text
- Chưa có resubmission_count
- Chưa có last_resubmitted_at
- Chưa có last_resubmitted_by
- Chưa chặn cancel trong vòng 24h ở trigger/service DB
- Chưa enforce agenda cho DURING_VISIT / AFTER_VISIT / CLOSED
```

### 1.2. Cột mới trong `visit_requests`

Database đã thêm:

```sql
resubmission_count INT UNSIGNED NOT NULL DEFAULT 0,
last_resubmitted_at DATETIME NULL,
last_resubmitted_by BIGINT UNSIGNED NULL
```

Ý nghĩa:

| Cột | Ý nghĩa |
|---|---|
| `resubmission_count` | Số lần Visitor gửi lại đơn sau khi toàn bộ request bị từ chối |
| `last_resubmitted_at` | Thời điểm gần nhất Visitor gửi lại đơn |
| `last_resubmitted_by` | User ID của Visitor gần nhất gửi lại đơn |

### 1.3. Index/FK mới

Đã thêm:

```sql
idx_visit_requests_resubmission
idx_visit_requests_last_resubmitted_by
fk_visit_requests_last_resubmitted_by
```

### 1.4. Rule agenda trong DB

Từ trạng thái sau trở đi bắt buộc phải có ít nhất một agenda:

```text
DURING_VISIT
AFTER_VISIT
CLOSED
```

Nghĩa là backend không được chuyển campus sang các trạng thái trên nếu chưa có agenda.

### 1.5. Rule cancel trong DB

DB đã hỗ trợ kiểm soát logic mới ở mức an toàn dữ liệu. Backend vẫn phải validate trước để trả lỗi nghiệp vụ dễ hiểu.

---

## 2. Quy tắc nghiệp vụ chốt cuối

## 2.1. Visitor sửa đơn trước khi duyệt

Visitor được sửa đơn nếu thỏa toàn bộ điều kiện:

```text
Actor = VISITOR
Request thuộc Visitor hiện tại
visit_requests.status = PENDING_APPROVAL
Tất cả visit_request_campuses.status = WAITING_REQUEST_APPROVAL
Không có campus nào REJECTED / ASSIGNED / BEFORE_VISIT / DURING_VISIT / AFTER_VISIT / CLOSED / CANCELLED
Thời điểm bắt đầu sớm nhất còn cách hiện tại >= 24 giờ
```

Sau khi sửa:

```text
requestStatus vẫn là PENDING_APPROVAL
campusStatus vẫn là WAITING_REQUEST_APPROVAL
updated_at / updated_by cập nhật
row_version tăng
Không tăng resubmission_count
Ghi audit action UPDATE_PENDING_VISIT_REQUEST
Notify Staff Leader các campus liên quan
```

Không cho sửa khi:

```text
PARTIALLY_APPROVED
APPROVED
REJECTED
CANCELLED
Đã có bất kỳ campus nào được duyệt/từ chối/hủy/đang diễn ra
Lịch còn dưới 24 giờ
```

---

## 2.2. Visitor sửa & gửi lại sau reject

Visitor được gửi lại nếu:

```text
Actor = VISITOR
Request thuộc Visitor hiện tại
visit_requests.status = REJECTED
Tất cả visit_request_campuses.status = REJECTED
Không có campus ASSIGNED / BEFORE_VISIT / DURING_VISIT / AFTER_VISIT / CLOSED / CANCELLED
Thời điểm bắt đầu mới sau khi sửa >= now + 24 giờ
```

Phase này không cho đổi danh sách campus khi resubmit.

Lý do:

```text
Nếu đổi campus sau khi reject, hệ thống phải quản lý thêm lịch sử campus cũ/mới, notification cũ/mới, audit phức tạp hơn.
Nếu Visitor muốn đổi campus, họ nên tạo đơn mới.
```

Sau khi gửi lại:

```text
visit_requests.status = PENDING_APPROVAL
visit_requests.resubmission_count += 1
visit_requests.last_resubmitted_at = now
visit_requests.last_resubmitted_by = currentVisitorUserId
visit_request_campuses.status = WAITING_REQUEST_APPROVAL
Clear toàn bộ decision fields cũ ở visit_request_campuses
Notify Staff Leader các campus xử lý lại
```

Các decision fields cần clear:

```text
decision_actor_role = NULL
decided_by = NULL
decided_at = NULL
decision_note = NULL
```

Nếu có host/cancel fields còn sót do dữ liệu cũ, cũng clear để đúng trạng thái chờ xử lý:

```text
current_host_user_id = NULL
host_assigned_by = NULL
host_assigned_at = NULL
cancelled_by = NULL
cancelled_at = NULL
cancellation_actor_type = NULL
cancellation_source = NULL
cancellation_reason = NULL
```

---

## 2.3. Visitor hủy đơn/lịch

Visitor được hủy nếu:

```text
Actor = VISITOR
Request thuộc Visitor hiện tại
request.status IN PENDING_APPROVAL / PARTIALLY_APPROVED / APPROVED
Không có campus DURING_VISIT / AFTER_VISIT / CLOSED
Tất cả campus còn hiệu lực có planned_start_at >= now + 24h
Lý do hủy bắt buộc
```

Nếu còn dưới 24h, backend trả lỗi:

```text
Error code: VISIT_CANCEL_WINDOW_EXPIRED
Message: Lịch thăm sắp diễn ra trong vòng 24 giờ. Vui lòng liên hệ FPTU để được hỗ trợ hủy/thay đổi.
```

---

## 2.4. Host hủy campus instance

Host được hủy campus instance nếu:

```text
Actor là current_host_user_id
campus.status IN ASSIGNED / BEFORE_VISIT
now < planned_start_at
Lý do hủy bắt buộc
```

Không cho hủy nếu:

```text
campus.status = DURING_VISIT
campus.status = AFTER_VISIT
campus.status = CLOSED
campus.status = CANCELLED
campus.status = REJECTED
now >= planned_start_at
```

Lỗi đề xuất:

```text
Error code: HOST_CANNOT_CANCEL_AFTER_VISIT_STARTED
Message: Không thể hủy vì lịch tiếp khách đã bắt đầu hoặc đã diễn ra.
```

---

## 2.5. Agenda bắt buộc từ DURING_VISIT trở đi

Trước khi chuyển campus sang:

```text
DURING_VISIT
AFTER_VISIT
CLOSED
```

Backend phải kiểm tra:

```text
visit_agendas có ít nhất 1 dòng theo visit_instance_id
```

Nếu chưa có agenda:

```text
Error code: VISIT_AGENDA_REQUIRED_BEFORE_START
Message: Cần có agenda trước khi bắt đầu tiếp khách.
```

---

# 3. Backend — phần cần cập nhật

## 3.1. Entity `VisitRequest`

Tìm entity:

```text
backend/PEMS.Domain/**/VisitRequest.cs
```

Thêm properties:

```csharp
public int ResubmissionCount { get; set; }
public DateTime? LastResubmittedAt { get; set; }
public long? LastResubmittedBy { get; set; }
```

Nếu project đang dùng `ulong`/`uint` cho ID thì dùng đúng type hiện tại, không trộn kiểu.

---

## 3.2. EF Configuration

Tìm file config:

```text
backend/PEMS.Infrastructure/**/VisitRequestConfiguration.cs
```

Thêm mapping:

```csharp
builder.Property(x => x.ResubmissionCount)
    .HasColumnName("resubmission_count")
    .HasDefaultValue(0);

builder.Property(x => x.LastResubmittedAt)
    .HasColumnName("last_resubmitted_at");

builder.Property(x => x.LastResubmittedBy)
    .HasColumnName("last_resubmitted_by");
```

Nếu đang khai báo FK thủ công:

```csharp
builder.HasOne<User>()
    .WithMany()
    .HasForeignKey(x => x.LastResubmittedBy)
    .OnDelete(DeleteBehavior.SetNull);
```

---

## 3.3. DTO list/detail

Các DTO liên quan cần bổ sung:

```text
VisitRequestManagementItemDto
VisitRequestDetailDto
SubmittedVisitRequestDetailDto
EditableVisitRequestDetailDto nếu có
```

Thêm fields:

```csharp
public int ResubmissionCount { get; set; }
public DateTime? LastResubmittedAt { get; set; }
public long? LastResubmittedBy { get; set; }
public string? LastResubmittedByName { get; set; }
```

Nếu frontend dùng camelCase, response phải ra:

```ts
resubmissionCount
lastResubmittedAt
lastResubmittedBy
lastResubmittedByName
```

---

## 3.4. AllowedActions mới

Thêm enum/string action:

```text
EDIT_PENDING_REQUEST
RESUBMIT_REJECTED_REQUEST
```

Không cho frontend tự đoán quyền. Backend phải trả action sau khi kiểm tra đầy đủ trạng thái, owner, thời gian 24h.

---

# 4. Backend — rule allowedActions

## 4.1. Action `EDIT_PENDING_REQUEST`

Trả action khi:

```csharp
isVisitor
&& request.VisitorUserId == currentUserId
&& request.Status == VisitRequestStatus.PendingApproval
&& campuses.All(c => c.Status == VisitRequestCampusStatus.WaitingRequestApproval)
&& campuses.Min(c => c.PlannedStartAt) >= now.AddHours(24)
```

Không trả action nếu có bất kỳ campus:

```text
REJECTED
ASSIGNED
BEFORE_VISIT
DURING_VISIT
AFTER_VISIT
CLOSED
CANCELLED
```

---

## 4.2. Action `RESUBMIT_REJECTED_REQUEST`

Trả action khi:

```csharp
isVisitor
&& request.VisitorUserId == currentUserId
&& request.Status == VisitRequestStatus.Rejected
&& campuses.All(c => c.Status == VisitRequestCampusStatus.Rejected)
```

Không trả action nếu request đang:

```text
PENDING_APPROVAL
PARTIALLY_APPROVED
APPROVED
CANCELLED
```

---

# 5. Backend — API endpoints đề xuất

## 5.1. Get editable detail

Endpoint:

```http
GET /api/visit-requests/{visitRequestId}/edit-detail
```

Mục tiêu:

```text
Load đầy đủ dữ liệu form cũ để Visitor sửa hoặc gửi lại.
```

Phải kiểm tra:

```text
Actor là Visitor owner
Request đang editable hoặc resubmittable
```

Nếu detail endpoint hiện tại đã đủ dữ liệu form thì có thể reuse, nhưng nên tách endpoint edit-detail để rõ quyền.

---

## 5.2. Pending edit

Endpoint:

```http
PUT /api/visit-requests/{visitRequestId}/pending-edit
```

Command đề xuất:

```text
UpdatePendingVisitRequestCommand
UpdatePendingVisitRequestCommandHandler
UpdatePendingVisitRequestCommandValidator
```

---

## 5.3. Resubmit rejected request

Endpoint:

```http
POST /api/visit-requests/{visitRequestId}/resubmit
```

Command đề xuất:

```text
ResubmitRejectedVisitRequestCommand
ResubmitRejectedVisitRequestCommandHandler
ResubmitRejectedVisitRequestCommandValidator
```

---

# 6. Backend — `UpdatePendingVisitRequestCommand`

## 6.1. Input payload

Payload nên reuse payload submit request hiện tại:

```text
delegationName
partnerName / partnerId nếu có
registrant fields
contact fields
visit purpose / working content
working language
media consent
transportation_note
note_to_fptu
guest members
campus schedules
agenda draft nếu form có
```

---

## 6.2. Validation

Handler/validator cần kiểm tra:

```text
1. Current user là VISITOR.
2. Request tồn tại.
3. Request thuộc current visitor.
4. request.status = PENDING_APPROVAL.
5. Tất cả campus.status = WAITING_REQUEST_APPROVAL.
6. planned_start_at mới >= now + 24h.
7. planned_end_at > planned_start_at.
8. Validate campus list nếu cho sửa campus khi pending.
9. Tất cả campus được chọn có ACTIVE Staff Leader.
10. Validate guest member count, phone, email, media consent, required fields như submit mới.
```

---

## 6.3. Cho đổi campus khi pending không?

Nên cho đổi campus khi pending, vì chưa cơ sở nào ra quyết định.

Điều kiện:

```text
Tất cả campus hiện tại vẫn WAITING_REQUEST_APPROVAL
Campus mới được chọn phải có ACTIVE Staff Leader
Không để request rỗng campus
```

Cách xử lý đơn giản:

```text
Xóa/recreate campus rows hoặc diff update đều được.
Vì chưa có quyết định/host/setup nên không mất nghiệp vụ.
```

Nếu dùng xóa/recreate, phải cẩn thận FK với bảng con. Nếu đã có bảng phụ thuộc, dùng diff update an toàn hơn.

---

## 6.4. Transaction xử lý

```text
1. Begin transaction.
2. Load request + campuses + guest members.
3. Check owner.
4. Check editable.
5. Validate input.
6. Update visit_requests fields.
7. Update guest members.
8. Update campus schedules/campus list.
9. Update updated_at / updated_by / row_version.
10. Ghi audit action UPDATE_PENDING_VISIT_REQUEST.
11. Notify Staff Leader liên quan.
12. Commit.
```

Không update:

```text
resubmission_count
last_resubmitted_at
last_resubmitted_by
```

---

# 7. Backend — `ResubmitRejectedVisitRequestCommand`

## 7.1. Payload

Reuse payload form submit hiện tại.

Phase này không cho đổi danh sách campus.

Payload campus gửi lên phải có cùng tập `campus_id` với request cũ.

---

## 7.2. Validation

```text
1. Current user là VISITOR.
2. Request tồn tại.
3. Request thuộc current visitor.
4. request.status = REJECTED.
5. Tất cả campus.status = REJECTED.
6. Không có campus đã duyệt/hủy/đang diễn ra.
7. Campus list gửi lên trùng campus list cũ.
8. planned_start_at mới >= now + 24h.
9. Validate toàn bộ form như submit mới.
```

---

## 7.3. Snapshot audit trước khi clear

Trước khi clear decision fields, tạo snapshot:

```json
[
  {
    "visitInstanceId": 5124,
    "campusId": 3,
    "campusName": "FPT University Đà Nẵng",
    "oldStatus": "REJECTED",
    "decidedBy": 11,
    "decidedByName": "IC Staff Leader Đà Nẵng",
    "decidedAt": "2026-07-02 15:20:00",
    "decisionActorRole": "STAFF_LEADER",
    "decisionNote": "Không đủ nhân sự tiếp đoàn vào thời gian này"
  }
]
```

Ghi vào audit:

```text
audit_logs.action = RESUBMIT_REJECTED_VISIT_REQUEST
audit_logs.entity_type = VISIT_REQUEST
audit_logs.entity_id = visit_request_id
```

`audit_log_changes` đề xuất:

| field_name | old_value_text | new_value_text |
|---|---|---|
| `request.status` | `REJECTED` | `PENDING_APPROVAL` |
| `resubmission_count` | count cũ | count mới |
| `campus_decisions_before_resubmit_json` | JSON snapshot | `cleared_for_resubmission` |

---

## 7.4. Transaction xử lý

```text
1. Begin transaction.
2. Lock request + campus rows.
3. Check owner.
4. Check resubmittable.
5. Validate payload.
6. Build campus decision snapshot.
7. Ghi audit_logs + audit_log_changes.
8. Update visit_requests form fields.
9. Update guest members.
10. Update planned_start_at/planned_end_at của từng campus.
11. Reset từng campus:
    - status = WAITING_REQUEST_APPROVAL
    - decision_actor_role = NULL
    - decided_by = NULL
    - decided_at = NULL
    - decision_note = NULL
    - current_host_user_id = NULL
    - host_assigned_by = NULL
    - host_assigned_at = NULL
    - cancelled_by = NULL
    - cancelled_at = NULL
    - cancellation_actor_type = NULL
    - cancellation_source = NULL
    - cancellation_reason = NULL
    - row_version += 1
12. Update request:
    - status = PENDING_APPROVAL
    - resubmission_count += 1
    - last_resubmitted_at = now
    - last_resubmitted_by = currentUserId
    - cancellation fields = NULL nếu có
    - row_version += 1
13. Notify Staff Leader từng campus.
14. Commit.
```

---

# 8. Backend — cập nhật cancel command

## 8.1. Visitor cancel request

Tìm handler hiện tại:

```text
CancelVisitRequestCommandHandler
```

Thêm check:

```csharp
var activeCampuses = campuses.Where(c =>
    c.Status != Cancelled &&
    c.Status != Rejected);

if (activeCampuses.Any(c => c.Status == DuringVisit || c.Status == AfterVisit || c.Status == Closed))
    throw BusinessException("VISIT_ALREADY_STARTED_CANNOT_CANCEL", ...);

if (activeCampuses.Any(c => c.PlannedStartAt < now.AddHours(24)))
    throw BusinessException("VISIT_CANCEL_WINDOW_EXPIRED", ...);
```

---

## 8.2. Visitor cancel campus

Tìm handler:

```text
CancelVisitRequestCampusCommandHandler
```

Thêm check:

```text
campus.planned_start_at >= now + 24h
campus.status không thuộc DURING_VISIT / AFTER_VISIT / CLOSED / CANCELLED / REJECTED
```

---

## 8.3. Host cancel campus

Trong cùng handler hoặc handler riêng, check:

```text
current_user_id = campus.current_host_user_id
campus.status IN ASSIGNED / BEFORE_VISIT
now < planned_start_at
```

Không cho hủy từ `DURING_VISIT` trở đi.

---

# 9. Backend — kiểm tra agenda trước khi chuyển trạng thái

Tìm các handler/service chuyển trạng thái:

```text
StartVisitCommandHandler
UpdateVisitStatusCommandHandler
VisitProcessCommandHandler
CloseVisitCommandHandler
```

Trước khi set:

```text
DURING_VISIT
AFTER_VISIT
CLOSED
```

phải check:

```csharp
var hasAgenda = await db.VisitAgendas.AnyAsync(x => x.VisitInstanceId == visitInstanceId);
if (!hasAgenda)
    throw BusinessException("VISIT_AGENDA_REQUIRED_BEFORE_START", "Cần có agenda trước khi bắt đầu tiếp khách.");
```

---

# 10. Frontend — types/api

## 10.1. Types

File:

```text
frontend/pems-react/src/features/delegations/types/delegations.types.ts
```

Thêm allowed actions:

```ts
export type AllowedAction =
  | ...
  | 'EDIT_PENDING_REQUEST'
  | 'RESUBMIT_REJECTED_REQUEST';
```

Thêm fields vào item/detail type:

```ts
resubmissionCount?: number;
lastResubmittedAt?: string | null;
lastResubmittedBy?: number | null;
lastResubmittedByName?: string | null;
```

---

## 10.2. API

File:

```text
frontend/pems-react/src/features/delegations/api/delegationsApi.ts
```

Thêm:

```ts
getEditableVisitRequestDetail: (visitRequestId: number) =>
  httpClient.get(`/visit-requests/${visitRequestId}/edit-detail`),

updatePendingVisitRequest: (visitRequestId: number, payload: VisitRequestFormPayload) =>
  httpClient.put(`/visit-requests/${visitRequestId}/pending-edit`, payload),

resubmitRejectedVisitRequest: (visitRequestId: number, payload: VisitRequestFormPayload) =>
  httpClient.post(`/visit-requests/${visitRequestId}/resubmit`, payload),
```

---

# 11. Frontend — list UI

## 11.1. File chính

```text
frontend/pems-react/src/pages/dashboard/visit/VisitRequestManagement.tsx
```

Trong `renderRowActions(row)`, thêm nút:

```text
EDIT_PENDING_REQUEST -> Sửa đơn
RESUBMIT_REJECTED_REQUEST -> Sửa & gửi lại
```

Đề xuất slot:

```text
Slot 1: Xem form đăng ký
Slot 2: Process/detail/summary
Slot 3: Approve/Accept/Edit/Resubmit
Slot 4: Reject/Cancel/Reason/Feedback
```

Nếu row là Visitor và có `EDIT_PENDING_REQUEST`, render:

```tsx
<ActionIconButton
  title="Sửa đơn đăng ký tham quan"
  tone="blue"
  icon={<PencilLine className="h-5 w-5" />}
  onClick={(e) => {
    e.stopPropagation();
    navTo(`/dashboard/visit/edit/${row.visitRequestId}`);
  }}
/>
```

Nếu có `RESUBMIT_REJECTED_REQUEST`, render:

```tsx
<ActionIconButton
  title="Sửa & gửi lại đơn"
  tone="orange"
  icon={<PencilLine className="h-5 w-5" />}
  onClick={(e) => {
    e.stopPropagation();
    navTo(`/dashboard/visit/resubmit/${row.visitRequestId}`);
  }}
/>
```

Nếu import thêm icon `RefreshCw` được thì dùng `RefreshCw` cho resubmit.

---

# 12. Frontend — form create/edit/resubmit

## 12.1. Route

Thêm routes:

```text
/dashboard/visit/create
/dashboard/visit/edit/:visitRequestId
/dashboard/visit/resubmit/:visitRequestId
```

## 12.2. Form mode

Form submit hiện tại nên đổi thành:

```ts
type VisitRequestFormMode = 'create' | 'edit' | 'resubmit';
```

Xác định mode theo route:

```text
/create -> create
/edit/:id -> edit
/resubmit/:id -> resubmit
```

## 12.3. Load data

Với edit/resubmit:

```text
GET /api/visit-requests/{id}/edit-detail
```

Map dữ liệu backend vào form:

```text
Thông tin người đăng ký
Thông tin đoàn khách
Thành viên đoàn
Campus/time schedule
Ngôn ngữ làm việc
Media consent
Transportation note
Note to FPTU
```

## 12.4. Submit

```text
create -> POST submit hiện tại
edit -> PUT /api/visit-requests/{id}/pending-edit
resubmit -> POST /api/visit-requests/{id}/resubmit
```

## 12.5. UI banner

Mode edit:

```text
Bạn đang chỉnh sửa đơn đang chờ xử lý. Sau khi lưu, Staff Leader sẽ xem thông tin mới nhất.
```

Mode resubmit:

```text
Đơn này đã bị từ chối. Bạn có thể chỉnh sửa thông tin và gửi lại để các cơ sở xem xét lại. Lý do từ chối cũ sẽ được lưu trong lịch sử hệ thống.
```

---

# 13. Notifications / Email

## 13.1. Visitor sửa pending

Gửi notification/email cho Staff Leader các campus:

```text
Visitor đã cập nhật thông tin đơn đăng ký tham quan. Vui lòng xem lại trước khi xử lý.
```

Action code đề xuất:

```text
VISIT_REQUEST_UPDATED_BY_VISITOR
```

## 13.2. Visitor resubmit rejected

Gửi notification/email cho Staff Leader các campus:

```text
Visitor đã chỉnh sửa và gửi lại đơn đã bị từ chối. Vui lòng xử lý lại.
```

Action code đề xuất:

```text
VISIT_REQUEST_RESUBMITTED_BY_VISITOR
```

---

# 14. Tests bắt buộc

## 14.1. Backend tests

### Edit pending

```text
1. Visitor edit PENDING_APPROVAL thành công.
2. Visitor edit fail nếu request không thuộc Visitor.
3. Visitor edit fail nếu request PARTIALLY_APPROVED.
4. Visitor edit fail nếu request APPROVED.
5. Visitor edit fail nếu request REJECTED.
6. Visitor edit fail nếu có campus REJECTED.
7. Visitor edit fail nếu có campus ASSIGNED.
8. Visitor edit fail nếu lịch còn dưới 24h.
9. Visitor edit thành công khi đổi campus và campus mới có Staff Leader ACTIVE.
10. Visitor edit fail khi campus mới không có Staff Leader ACTIVE.
```

### Resubmit rejected

```text
1. Visitor resubmit request REJECTED toàn bộ thành công.
2. Resubmit fail nếu request không thuộc Visitor.
3. Resubmit fail nếu request PARTIALLY_APPROVED.
4. Resubmit fail nếu có campus đã ASSIGNED.
5. Resubmit fail nếu campus list bị đổi.
6. Resubmit fail nếu lịch mới dưới 24h.
7. Resubmit reset request.status về PENDING_APPROVAL.
8. Resubmit reset campus.status về WAITING_REQUEST_APPROVAL.
9. Resubmit clear decision fields.
10. Resubmit tăng resubmission_count.
11. Resubmit set last_resubmitted_at/by.
12. Resubmit ghi audit snapshot decision cũ trước khi clear.
```

### Cancel

```text
1. Visitor cancel request thành công khi tất cả campus còn >= 24h.
2. Visitor cancel fail nếu còn campus dưới 24h.
3. Visitor cancel fail nếu có campus DURING_VISIT.
4. Visitor cancel fail nếu có campus AFTER_VISIT.
5. Visitor cancel fail nếu có campus CLOSED.
6. Host cancel thành công khi ASSIGNED và now < planned_start_at.
7. Host cancel thành công khi BEFORE_VISIT và now < planned_start_at.
8. Host cancel fail khi DURING_VISIT.
9. Host cancel fail khi now >= planned_start_at.
10. Host cancel fail nếu không phải current_host_user_id.
```

### Agenda

```text
1. Không cho chuyển sang DURING_VISIT nếu chưa có agenda.
2. Không cho chuyển sang AFTER_VISIT nếu chưa có agenda.
3. Không cho chuyển sang CLOSED nếu chưa có agenda.
4. Cho chuyển trạng thái nếu đã có agenda.
```

---

## 14.2. Frontend manual tests

```text
1. Visitor thấy nút Sửa đơn ở PENDING_APPROVAL hợp lệ.
2. Visitor không thấy nút Sửa đơn khi APPROVED/PARTIALLY_APPROVED.
3. Visitor thấy nút Sửa & gửi lại khi request REJECTED toàn bộ.
4. Bấm Sửa đơn load đúng dữ liệu real vào form.
5. Bấm Sửa & gửi lại load đúng dữ liệu real vào form.
6. Submit edit thành công, quay về list, trạng thái vẫn Chờ xử lý.
7. Submit resubmit thành công, trạng thái về Chờ xử lý.
8. Staff Leader nhìn thấy đơn resubmit trong danh sách cần xử lý.
9. Nút hủy không hiện hoặc backend chặn khi còn dưới 24h.
10. Host không hủy được khi campus đang DURING_VISIT.
11. Nếu thiếu agenda, không bắt đầu tiếp khách được.
```

---

# 15. Thứ tự triển khai khuyến nghị

```text
Bước 1. Import SQL mới hoặc chạy migration 3 cột nếu DB local chưa fresh-create.
Bước 2. Update backend entity + EF mapping.
Bước 3. Update DTO + allowedActions.
Bước 4. Implement rule EDIT_PENDING_REQUEST / RESUBMIT_REJECTED_REQUEST trong list/detail query.
Bước 5. Implement GET edit-detail.
Bước 6. Implement UpdatePendingVisitRequestCommand.
Bước 7. Implement ResubmitRejectedVisitRequestCommand.
Bước 8. Update cancel handlers với rule 24h + Host cannot cancel after start.
Bước 9. Update start/close visit handlers để check agenda required.
Bước 10. Update notification/email.
Bước 11. Update frontend types/api.
Bước 12. Update VisitRequestManagement.tsx thêm nút sửa/gửi lại.
Bước 13. Update form create/edit/resubmit.
Bước 14. Chạy backend build/test.
Bước 15. Chạy frontend build/test.
Bước 16. Test tay full role Visitor/Staff Leader/Host.
Bước 17. Cập nhật docs/report/test case.
```

---

# 16. Checklist hoàn thành

## SQL

```text
[ ] SQL mới import được không lỗi.
[ ] visit_requests có 3 cột resubmit.
[ ] FK last_resubmitted_by hoạt động.
[ ] Seed không còn campus DURING/AFTER/CLOSED thiếu agenda.
[ ] Trigger/rule DB không chặn sai các trạng thái hợp lệ.
```

## Backend

```text
[ ] Entity/EF build pass.
[ ] DTO trả resubmission fields.
[ ] AllowedActions đúng theo role/status/time.
[ ] Edit pending command hoạt động.
[ ] Resubmit rejected command hoạt động.
[ ] Audit snapshot ghi trước khi clear decision fields.
[ ] Cancel 24h hoạt động.
[ ] Host không hủy được khi DURING_VISIT.
[ ] Agenda required check hoạt động.
[ ] Notification/email gửi đúng người.
```

## Frontend

```text
[ ] Visitor thấy nút Sửa đơn đúng case.
[ ] Visitor thấy nút Sửa & gửi lại đúng case.
[ ] Form edit load dữ liệu thật.
[ ] Form resubmit load dữ liệu thật.
[ ] Submit edit gọi đúng endpoint.
[ ] Submit resubmit gọi đúng endpoint.
[ ] Error backend hiển thị rõ.
[ ] Build TypeScript pass.
```

---

# 17. Format báo cáo sau khi triển khai

Sau khi code xong, báo cáo theo format:

```md
# Implementation Report — Visitor Edit / Resubmit / Cancel 24h

## Files changed
| File | Change |
|---|---|
| ... | ... |

## SQL
- ...

## Backend
- ...

## Frontend
- ...

## Business rules enforced
- ...

## Tests
- Backend build: pass/fail
- Frontend build: pass/fail
- Manual tests: pass/fail

## Remaining risks
- ...
```
