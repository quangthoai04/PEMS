# PEMS — Kế hoạch triển khai logic mới: Campus xử lý độc lập, Staff Leader duyệt kèm host, Transportation Note

> File này dùng làm **kế hoạch triển khai chi tiết** cho dev/backend/frontend/test/docs khi cập nhật logic Delegation Reception của PEMS.
>
> Mục tiêu là đồng bộ toàn bộ hệ thống sau khi SQL fresh-create đã được sửa theo logic mới.
>
> SQL mới liên quan:
>
> ```text
> pems_full_v10_new_final_campus_independent_approval_self_host_transport_note_FULL_FIXED.sql
> ```

---

## 0. Tóm tắt logic nghiệp vụ mới

### 0.1. Logic cũ cần bỏ

Không dùng các flow sau nữa:

```text
1. Multi-campus request gửi cho HO duyệt tổng.
2. HO approve/reject multi-campus request.
3. Staff Leader chỉ thấy campus instance sau khi HO approve.
4. Campus instance có trạng thái WAITING_HOST_ASSIGNMENT.
5. Staff Leader duyệt xong rồi mới gán host ở bước sau.
6. Staff Leader không được tự làm host.
7. Quyết định approve/reject lưu ở visit_requests.decided_*.
8. transportation_type enum + transportation_detail.
```

### 0.2. Logic mới cần triển khai

```text
1. Visitor/Staff submit visit request chọn một hoặc nhiều campus.
2. Mỗi campus trong form tạo một visit_request_campuses riêng.
3. Mỗi campus instance được gửi trực tiếp đến Staff Leader của campus đó.
4. HO không còn duyệt multi-campus, chỉ có thể xem monitor/read-only nếu hệ thống vẫn giữ màn HO.
5. Staff Leader xử lý riêng campus instance thuộc campus mình.
6. Staff Leader approve bắt buộc chọn host ngay.
7. Approve thành công: WAITING_REQUEST_APPROVAL -> ASSIGNED.
8. Reject campus: WAITING_REQUEST_APPROVAL -> REJECTED.
9. Staff Leader được chọn chính mình làm host.
10. Quyết định approve/reject lưu ở visit_request_campuses.decided_*.
11. visit_requests.status chỉ là trạng thái tổng/aggregate.
12. transportation_type + transportation_detail được thay bằng transportation_note text.
```

---

## 1. Trạng thái database sau khi cập nhật SQL

### 1.1. `visit_requests.status`

```text
PENDING_APPROVAL
PARTIALLY_APPROVED
APPROVED
REJECTED
CANCELLED
```

Ý nghĩa:

| Status | Ý nghĩa |
|---|---|
| `PENDING_APPROVAL` | Chưa campus nào được duyệt thành công, vẫn còn campus đang chờ xử lý. |
| `PARTIALLY_APPROVED` | Có ít nhất một campus đã được duyệt/gán host, nhưng request vẫn còn campus pending hoặc rejected. |
| `APPROVED` | Tất cả campus đã xử lý xong và có ít nhất một campus được duyệt. |
| `REJECTED` | Tất cả campus đều bị từ chối. |
| `CANCELLED` | Visitor hủy toàn bộ request. |

### 1.2. `visit_request_campuses.status`

```text
WAITING_REQUEST_APPROVAL
ASSIGNED
BEFORE_VISIT
DURING_VISIT
AFTER_VISIT
CLOSED
CANCELLED
REJECTED
```

Không còn:

```text
WAITING_HOST_ASSIGNMENT
```

Flow mới:

```text
WAITING_REQUEST_APPROVAL -> ASSIGNED
WAITING_REQUEST_APPROVAL -> REJECTED
ASSIGNED -> BEFORE_VISIT -> DURING_VISIT -> AFTER_VISIT -> CLOSED
ASSIGNED / BEFORE_VISIT -> CANCELLED nếu đủ điều kiện hủy
```

### 1.3. Decision fields

`visit_requests` không còn lưu quyết định approve/reject bằng:

```text
decided_by
decided_at
decision_actor_role
decision_note
```

Các field này chuyển xuống `visit_request_campuses`:

```text
decided_by
decided_at
decision_actor_role
decision_note
```

Ý nghĩa:

| Field | Ý nghĩa |
|---|---|
| `decided_by` | Staff Leader duyệt/từ chối campus instance. |
| `decided_at` | Thời điểm Staff Leader xử lý. |
| `decision_actor_role` | Luôn là `STAFF_LEADER`. |
| `decision_note` | Ghi chú duyệt hoặc lý do từ chối. Reject bắt buộc có lý do. |

### 1.4. Transportation

Bỏ:

```text
transportation_type
transportation_detail
```

Thêm:

```text
transportation_note
```

Ý nghĩa:

```text
Khách nhập text tự do để mô tả/nhận diện phương tiện di chuyển tới FPTU.
Ví dụ: Xe 16 chỗ màu trắng, biển số 30A-xxxxx, dự kiến tới cổng lúc 8:30.
```

---

## 2. Phạm vi triển khai

Các phần cần cập nhật:

```text
Backend domain/entity/enum/EF/DbContext
DTO/request/response/validator
Submit visit request flow
Approve campus instance flow
Reject campus instance flow
Aggregate status service
HO endpoint/action
Host candidate/self-host logic
Participant creation for host
List/detail visibility + allowedActions
Notification/email
Frontend submit form
Frontend Staff Leader list/detail/action modal
Frontend HO monitor
Frontend Visitor detail
Report/dashboard/visit process summary
Docs/report/test cases
Build + test
```

Không được tái tạo:

```text
permissions
role_permissions
permission_code runtime authorization
dynamic permission matrix
WAITING_HOST_ASSIGNMENT
transportation_type enum
HO approve multi-campus
request-level decision approve/reject
```

---

# 3. Kế hoạch triển khai chi tiết theo bước

---

## Bước 4 — Update entity/enum/EF/DbContext

### 4.1. Mục tiêu

Đồng bộ domain/backend data model với SQL mới.

### 4.2. File/module cần kiểm tra

Tự search source theo các keyword:

```text
VisitRequest
VisitRequestCampus
VisitRequestStatus
VisitRequestCampusStatus
WAITING_HOST_ASSIGNMENT
TransportationType
TransportationDetail
transportation_type
transportation_detail
DecisionActorRole
decided_by
decided_at
decision_actor_role
decision_note
```

Các khu vực thường cần sửa:

```text
backend/PEMS.Domain/Entities
backend/PEMS.Domain/Enums
backend/PEMS.Infrastructure/Persistence/Configurations
backend/PEMS.Infrastructure/Persistence/ApplicationDbContext.cs
backend/PEMS.Application/**/Dtos
backend/PEMS.Application/**/Queries
backend/PEMS.Application/**/Commands
```

### 4.3. Việc cần làm

#### 4.3.1. `VisitRequestStatus`

Thêm:

```csharp
PARTIALLY_APPROVED
```

Đảm bảo enum còn:

```csharp
PENDING_APPROVAL,
PARTIALLY_APPROVED,
APPROVED,
REJECTED,
CANCELLED
```

#### 4.3.2. `VisitRequestCampusStatus`

Xóa:

```csharp
WAITING_HOST_ASSIGNMENT
```

Thêm:

```csharp
REJECTED
```

Đảm bảo enum còn:

```csharp
WAITING_REQUEST_APPROVAL,
ASSIGNED,
BEFORE_VISIT,
DURING_VISIT,
AFTER_VISIT,
CLOSED,
CANCELLED,
REJECTED
```

#### 4.3.3. Entity `VisitRequest`

Xóa property nếu đang có:

```csharp
DecidedBy
DecidedAt
DecisionActorRole
DecisionNote
TransportationType
TransportationDetail
```

Thêm:

```csharp
public string? TransportationNote { get; set; }
```

Giữ cancellation fields:

```csharp
CancelledBy
CancelledAt
CancellationActorType
CancellationSource
CancellationReason
```

#### 4.3.4. Entity `VisitRequestCampus`

Thêm:

```csharp
public ulong? DecidedBy { get; set; }
public DateTime? DecidedAt { get; set; }
public string? DecisionActorRole { get; set; }
public string? DecisionNote { get; set; }
```

Hoặc dùng kiểu hiện tại của project nếu đang dùng `long`, `long?`, enum string, enum typed.

Giữ host fields:

```csharp
CurrentHostUserId
HostAssignedBy
HostAssignedAt
```

#### 4.3.5. EF configuration

Sửa mapping:

```text
visit_requests.transportation_note
visit_request_campuses.decided_by
visit_request_campuses.decided_at
visit_request_campuses.decision_actor_role
visit_request_campuses.decision_note
```

Xóa mapping cũ:

```text
visit_requests.transportation_type
visit_requests.transportation_detail
visit_requests.decided_by
visit_requests.decided_at
visit_requests.decision_actor_role
visit_requests.decision_note
```

### 4.4. Kiểm tra hoàn thành

```text
[ ] Backend compile không còn lỗi enum/property không tồn tại.
[ ] Không còn reference WAITING_HOST_ASSIGNMENT trong backend runtime code.
[ ] Không còn TransportationType/TransportationDetail trong entity/EF.
[ ] VisitRequestCampus có decision fields mới.
```

---

## Bước 5 — Update DTO/request/response/validators

### 5.1. Mục tiêu

Đồng bộ API contract với logic mới.

### 5.2. DTO cần kiểm tra

Search:

```text
SubmitVisitRequest
CreateVisitRequest
UpdateVisitRequest
VisitRequestDetail
VisitRequestListItem
VisitRequestCampusDto
ApproveVisitRequest
RejectVisitRequest
AssignHost
HostCandidate
AllowedActions
Transportation
```

### 5.3. Request DTO submit/update

Xóa:

```csharp
TransportationType
TransportationDetail
```

Thêm:

```csharp
public string? TransportationNote { get; set; }
```

Validation:

```text
- Optional.
- Trim trước khi lưu.
- Nếu dùng TEXT ở DB vẫn nên giới hạn 1000–2000 ký tự ở backend.
- Không nhận HTML/script.
- Không cần validate enum phương tiện nữa.
```

### 5.4. Response DTO list/detail

Trả status tổng:

```json
{
  "requestStatus": "PARTIALLY_APPROVED"
}
```

Trả trạng thái từng campus:

```json
{
  "campusInstances": [
    {
      "visitInstanceId": 1001,
      "campusId": 1,
      "campusName": "FPTU Hà Nội",
      "status": "ASSIGNED",
      "currentHostUserId": 20,
      "currentHostName": "...",
      "decidedBy": 5,
      "decidedByName": "...",
      "decidedAt": "...",
      "decisionNote": "..."
    }
  ]
}
```

Thêm summary nếu cần:

```json
{
  "campusDecisionSummary": {
    "total": 3,
    "pending": 1,
    "approved": 1,
    "rejected": 1,
    "cancelled": 0
  }
}
```

### 5.5. Approve DTO mới

Approve phải nhận host:

```csharp
public sealed class ApproveCampusInstanceRequest
{
    public long HostUserId { get; set; }
    public string? DecisionNote { get; set; }
}
```

Validator:

```text
HostUserId bắt buộc > 0.
DecisionNote optional, trim, max length.
```

Nếu không có host:

```text
HTTP 400/422
Code: HOST_REQUIRED_ON_APPROVAL
Message: Khi duyệt yêu cầu, bạn phải chọn host chính thức.
```

### 5.6. Reject DTO mới

```csharp
public sealed class RejectCampusInstanceRequest
{
    public string DecisionNote { get; set; } = string.Empty;
}
```

Validator:

```text
DecisionNote bắt buộc.
Trim.
Max length.
Không nhận HTML/script.
```

### 5.7. AllowedActions DTO

Allowed actions phải theo từng campus instance:

```json
{
  "canApprove": true,
  "canReject": true,
  "canCancelCampus": false,
  "canViewDetail": true
}
```

Rule:

```text
Staff Leader đúng campus + instance WAITING_REQUEST_APPROVAL -> canApprove/canReject = true.
HO -> canApprove/canReject = false.
Host hiện tại + ASSIGNED/BEFORE_VISIT -> canCancelCampus tùy rule hủy.
Visitor owner -> canCancelRequest tùy request status.
```

### 5.8. Kiểm tra hoàn thành

```text
[ ] API không còn trả/nhận transportationType.
[ ] API không còn trả/nhận transportationDetail.
[ ] API trả transportationNote.
[ ] Approve request bắt buộc hostUserId.
[ ] Reject request bắt buộc decisionNote.
[ ] List/detail trả trạng thái từng campus.
```

---

## Bước 6 — Sửa submit routing tới Staff Leader từng campus

### 6.1. Mục tiêu

Multi-campus không gửi HO nữa. Mỗi campus instance gửi thẳng Staff Leader của campus đó.

### 6.2. Handler cần kiểm tra

Search:

```text
SubmitVisitRequest
CreateVisitRequest
VisitRequestSubmit
CreateVisitRequestCommandHandler
SubmitVisitRequestCommandHandler
```

### 6.3. Logic mới

Khi submit:

```text
1. Validate form.
2. Tạo visit_requests với status = PENDING_APPROVAL.
3. Tạo visit_request_campuses cho từng campus đã chọn:
   - status = WAITING_REQUEST_APPROVAL
   - current_host_user_id = NULL
   - decided_by/decided_at/decision_actor_role/decision_note = NULL
4. Với mỗi campus:
   - tìm Staff Leader ACTIVE của campus đó.
   - nếu không có Staff Leader: trả lỗi rõ ràng, không tạo request nửa vời.
   - set coordinator_user_id nếu vẫn giữ coordinator field.
   - gửi notification/email cho Staff Leader campus đó.
5. Không gửi yêu cầu duyệt cho HO.
6. Không tạo host/participant/logistics/minutes/calendar trước khi campus được approve.
```

### 6.4. Validation bắt buộc

```text
[ ] Campus được chọn phải ACTIVE.
[ ] Mỗi campus phải có Staff Leader ACTIVE.
[ ] Không cho submit nếu có campus thiếu Staff Leader, trừ khi có rule nghiệp vụ riêng.
[ ] Không tin campusId từ frontend nếu không tồn tại trong DB.
```

### 6.5. Lỗi đề xuất

```text
CAMPUS_HAS_NO_ACTIVE_STAFF_LEADER
Cơ sở [Tên cơ sở] chưa có Staff Leader đang hoạt động nên chưa thể tiếp nhận yêu cầu.
```

### 6.6. Kiểm tra hoàn thành

```text
[ ] Single-campus route tới Staff Leader campus đó.
[ ] Multi-campus route tới từng Staff Leader của từng campus.
[ ] HO không nhận notification duyệt multi-campus.
[ ] Submit không tạo host/participants/logistics/minutes.
```

---

## Bước 7 — Tạo/sửa approve campus instance bắt buộc host

### 7.1. Mục tiêu

Staff Leader approve campus instance của campus mình và bắt buộc chọn host trong cùng action.

### 7.2. API đề xuất

```http
POST /api/visit-requests/{requestId}/campuses/{visitInstanceId}/approve
```

Body:

```json
{
  "hostUserId": 456,
  "decisionNote": "Đồng ý tiếp nhận đoàn"
}
```

### 7.3. Handler đề xuất

```text
ApproveCampusInstanceCommand
ApproveCampusInstanceCommandHandler
ApproveCampusInstanceCommandValidator
```

### 7.4. Rule backend

Handler phải check:

```text
1. Actor là STAFF + LEADER.
2. Actor.primary_campus_id = visit_request_campuses.campus_id.
3. visit_request_campuses.status = WAITING_REQUEST_APPROVAL.
4. visit_requests.status != CANCELLED.
5. HostUserId bắt buộc.
6. Host hợp lệ theo rule host candidate mới.
7. Nếu HostUserId là Staff Leader thì phải là chính actor đang duyệt.
8. Host không conflict lịch.
9. current_host_user_id phải NULL.
10. Không cho approve lại nếu campus đã ASSIGNED/REJECTED/CANCELLED/CLOSED.
```

### 7.5. Transaction xử lý

Trong cùng transaction:

```text
1. Set visit_request_campuses.status = ASSIGNED.
2. Set decided_by = currentUserId.
3. Set decided_at = now.
4. Set decision_actor_role = STAFF_LEADER.
5. Set decision_note = request.DecisionNote.
6. Set current_host_user_id = HostUserId.
7. Set host_assigned_by = currentUserId.
8. Set host_assigned_at = now.
9. Create/update visit_participants row cho host với participant_role = IC_HOST.
10. Recalculate visit_requests.status.
11. Gửi notification/email cho Visitor và Host.
12. Ghi audit log.
```

### 7.6. Kiểm tra hoàn thành

```text
[ ] Approve không có host bị lỗi.
[ ] Approve đúng campus chuyển WAITING_REQUEST_APPROVAL -> ASSIGNED.
[ ] Host fields được set.
[ ] Decision fields ở campus được set.
[ ] Request tổng được aggregate lại.
[ ] Participant host được tạo/cập nhật.
```

---

## Bước 8 — Tạo/sửa reject campus instance

### 8.1. Mục tiêu

Staff Leader từ chối riêng campus instance của campus mình.

### 8.2. API đề xuất

```http
POST /api/visit-requests/{requestId}/campuses/{visitInstanceId}/reject
```

Body:

```json
{
  "decisionNote": "Campus không thể tiếp nhận trong khung thời gian này."
}
```

### 8.3. Rule backend

```text
1. Actor là STAFF + LEADER.
2. Actor.primary_campus_id = visit_request_campuses.campus_id.
3. visit_request_campuses.status = WAITING_REQUEST_APPROVAL.
4. visit_requests.status != CANCELLED.
5. decisionNote bắt buộc.
6. Không set host.
7. Không tạo participant/logistics/minutes/calendar.
```

### 8.4. Transaction xử lý

```text
1. Set status = REJECTED.
2. Set decided_by = currentUserId.
3. Set decided_at = now.
4. Set decision_actor_role = STAFF_LEADER.
5. Set decision_note = reason.
6. Recalculate request aggregate status.
7. Notify Visitor.
8. Audit log.
```

### 8.5. Kiểm tra hoàn thành

```text
[ ] Reject thiếu lý do bị lỗi.
[ ] Reject một campus không reject toàn bộ request nếu còn campus khác pending/assigned.
[ ] Request tổng tính đúng PENDING_APPROVAL/PARTIALLY_APPROVED/APPROVED/REJECTED.
```

---

## Bước 9 — Tạo aggregate status service

### 9.1. Mục tiêu

Có một nơi duy nhất tính `visit_requests.status` từ trạng thái các campus instance.

### 9.2. Service đề xuất

```text
IVisitRequestAggregateStatusService
VisitRequestAggregateStatusService
```

Method:

```csharp
Task RecalculateAsync(long visitRequestId, CancellationToken cancellationToken);
```

### 9.3. Rule aggregate

```text
Nếu visit_requests.status = CANCELLED:
    không đổi

Nếu tất cả campus = REJECTED:
    visit_requests.status = REJECTED

Nếu có ít nhất 1 campus thuộc nhóm approved/beyond
và vẫn còn campus WAITING_REQUEST_APPROVAL hoặc REJECTED:
    visit_requests.status = PARTIALLY_APPROVED

Nếu không còn campus WAITING_REQUEST_APPROVAL
và có ít nhất 1 campus approved/beyond:
    visit_requests.status = APPROVED

Nếu chưa có campus nào approved/beyond
và vẫn còn campus WAITING_REQUEST_APPROVAL:
    visit_requests.status = PENDING_APPROVAL
```

Approved/beyond:

```text
ASSIGNED
BEFORE_VISIT
DURING_VISIT
AFTER_VISIT
CLOSED
```

### 9.4. Ví dụ kỳ vọng

| Campus statuses | Request status |
|---|---|
| HN pending, HCM pending | `PENDING_APPROVAL` |
| HN rejected, HCM pending | `PENDING_APPROVAL` |
| HN assigned, HCM pending | `PARTIALLY_APPROVED` |
| HN assigned, HCM rejected | `APPROVED` |
| HN assigned, HCM assigned | `APPROVED` |
| HN rejected, HCM rejected | `REJECTED` |
| request cancelled | `CANCELLED` |

### 9.5. Kiểm tra hoàn thành

```text
[ ] Service được gọi sau approve/reject/cancel campus.
[ ] Service không override CANCELLED tổng.
[ ] Unit test đủ các case aggregate.
```

---

## Bước 10 — Disable HO approve/reject

### 10.1. Mục tiêu

HO không còn xử lý approval multi-campus.

### 10.2. Việc cần làm

Search và xử lý:

```text
ApproveCrossCampusRequest
RejectCrossCampusRequest
HO approve
HO reject
ProcessCrossCampus
decision_actor_role = HO
```

### 10.3. Backend

Với endpoint cũ:

```text
Option A: Xóa route nếu không còn frontend gọi.
Option B: Giữ route nhưng trả 410 Gone.
Option C: Trả 403 Forbidden với message logic đã thay đổi.
```

Khuyến nghị:

```text
Trả 410 Gone nếu API đã deprecated rõ ràng.
Trả 403 nếu vẫn giữ route nhưng HO không còn quyền mutate.
```

Message:

```text
Luồng duyệt liên cơ sở đã thay đổi. Mỗi Staff Leader xử lý campus instance của campus mình; HO không còn duyệt request tổng.
```

### 10.4. Frontend

```text
[ ] Bỏ nút approve/reject ở HO page.
[ ] Bỏ tab “Chờ HO duyệt liên cơ sở” nếu có.
[ ] HO detail chỉ xem trạng thái từng campus.
```

### 10.5. Kiểm tra hoàn thành

```text
[ ] HO gọi endpoint approve/reject cũ không thể thay đổi dữ liệu.
[ ] UI HO không còn nút duyệt/từ chối.
[ ] Multi-campus vẫn xử lý được bởi Staff Leader từng campus.
```

---

## Bước 11 — Sửa host candidate self-host

### 11.1. Mục tiêu

Staff Leader có thể chọn chính mình làm host khi approve campus instance.

### 11.2. Rule host hợp lệ

Host hợp lệ gồm:

```text
1. IC Staff thường:
   role_code = STAFF
   sub_role = STAFF
   primary_campus_id = campus_id
   department_type = IC
   status = ACTIVE

2. Chính Staff Leader đang duyệt:
   user_id = currentUserId
   role_code = STAFF
   sub_role = LEADER
   primary_campus_id = campus_id
   department_type = IC
   status = ACTIVE
```

Không cho:

```text
Staff Leader chọn Staff Leader khác làm host.
Staff Leader chọn user campus khác.
Staff Leader chọn Department/Student/HO/Admin/Visitor làm host.
Inactive user làm host.
User không thuộc IC department làm host.
```

### 11.3. Host candidate query

Sửa query để trả:

```text
- Danh sách IC Staff thường cùng campus.
- Thêm option chính actor Staff Leader nếu đủ điều kiện.
```

Response nên có flag:

```json
{
  "userId": 5,
  "fullName": "Nguyễn Văn A",
  "roleLabel": "Staff Leader",
  "isSelf": true,
  "isStaffLeaderSelfHostOption": true,
  "conflictCount": 0
}
```

### 11.4. Conflict check

Vẫn check lịch cho self-host:

```text
calendar_events ACTIVE
visit_request_campuses khác mà user đang host với status ASSIGNED/BEFORE_VISIT/DURING_VISIT
overlap rule: existing_start < targetEnd && existing_end > targetStart
```

### 11.5. Kiểm tra hoàn thành

```text
[ ] Staff Leader thấy option “Tôi làm host chính”.
[ ] Staff Leader khác không xuất hiện.
[ ] Conflict count hoạt động với cả self-host.
[ ] Approve validate lại host server-side, không chỉ dựa vào frontend.
```

---

## Bước 12 — Sửa participant creation cho host

### 12.1. Mục tiêu

Khi approve + assign host, hệ thống tạo/cập nhật participant host đúng.

### 12.2. Logic

Sau approve:

```text
visit_participants.visit_instance_id = campus instance id
visit_participants.user_id = hostUserId
visit_participants.participant_role = IC_HOST
visit_participants.status = ASSIGNED hoặc ACCEPTED theo convention hiện tại của project
is_host = true nếu field này còn dùng
assigned_by = Staff Leader đang duyệt
assigned_at = now
```

Nếu row đã tồn tại:

```text
Không tạo trùng.
Update participant_role/status/is_host nếu cần.
```

### 12.3. Rule chống trùng

Đảm bảo:

```text
Một user không có nhiều participant row active trong cùng visit instance.
```

### 12.4. Kiểm tra hoàn thành

```text
[ ] Approve host IC Staff tạo participant IC_HOST.
[ ] Approve self-host Staff Leader tạo participant IC_HOST cho chính Staff Leader.
[ ] Không tạo trùng participant khi retry hoặc seed cũ đã có row.
```

---

## Bước 13 — Sửa list/detail visibility và allowedActions

### 13.1. Mục tiêu

Tất cả danh sách/chi tiết hiển thị đúng theo logic campus xử lý độc lập.

### 13.2. Staff Leader visibility

Staff Leader thấy:

```text
- Single-campus request thuộc campus mình.
- Multi-campus campus instance thuộc campus mình ngay sau submit.
```

Không cần chờ HO approve.

Allowed actions:

```text
WAITING_REQUEST_APPROVAL + đúng campus -> canApprove/canReject = true.
ASSIGNED trở đi -> canApprove/canReject = false.
REJECTED -> read-only.
CANCELLED/CLOSED -> read-only.
```

### 13.3. HO visibility

HO:

```text
- Có thể xem monitor/read-only nếu vẫn giữ nghiệp vụ monitor.
- Không có canApprove/canReject.
- Không gán host.
- Không cancel thay Staff Leader/Host/Visitor.
```

### 13.4. IC Staff/Host visibility

IC Staff thấy khi:

```text
- là current_host_user_id
- hoặc có participant relationship hợp lệ
```

Host có action theo lifecycle:

```text
ASSIGNED/BEFORE_VISIT -> prepare/cancel campus nếu rule cho phép.
DURING_VISIT -> manage attendance/minutes/media theo rule.
AFTER_VISIT -> đóng góp hậu xử lý.
CLOSED/CANCELLED -> read-only.
```

### 13.5. Visitor visibility

Visitor thấy request của chính họ và trạng thái từng campus:

```text
HN: Chờ xử lý
HCM: Đã tiếp nhận / host
DN: Từ chối / lý do
```

### 13.6. Kiểm tra hoàn thành

```text
[ ] Staff Leader HN không thấy action của HCM.
[ ] Staff Leader HN thấy HN instance ngay sau submit multi-campus.
[ ] HO không có action approve/reject.
[ ] Visitor detail hiển thị đúng từng campus.
```

---

## Bước 14 — Sửa notification/email

### 14.1. Submit notification

Cũ:

```text
Multi-campus -> HO
```

Mới:

```text
Mỗi campus -> Staff Leader của campus đó
```

Nội dung đề xuất:

```text
Bạn có một yêu cầu tham quan mới cần xử lý tại [Campus].
Vui lòng xem chi tiết, duyệt/từ chối và chọn host nếu duyệt.
```

### 14.2. Approve notification

Gửi Visitor:

```text
Cơ sở [Campus] đã tiếp nhận yêu cầu tham quan của bạn.
Host phụ trách: [Host name].
```

Gửi Host:

```text
Bạn được gán làm host chính cho đoàn [DelegationName] tại [Campus].
```

Nếu self-host:

```text
Có thể không cần gửi email cho chính Staff Leader, nhưng nên tạo notification/audit nội bộ nếu hệ thống đang dùng.
```

### 14.3. Reject notification

Gửi Visitor:

```text
Cơ sở [Campus] đã từ chối tiếp nhận yêu cầu tham quan.
Lý do: [DecisionNote]
```

### 14.4. Aggregate summary notification

Có thể gửi khi:

```text
- Tất cả campus đã xử lý xong.
- Tất cả campus đều rejected.
- Request chuyển APPROVED/PARTIALLY_APPROVED/REJECTED.
```

### 14.5. Kiểm tra hoàn thành

```text
[ ] Submit multi-campus tạo notification cho từng Staff Leader.
[ ] HO không nhận email duyệt.
[ ] Approve gửi Visitor + Host.
[ ] Reject gửi Visitor kèm lý do.
[ ] Email template không còn nhắc HO duyệt multi-campus.
```

---

## Bước 15 — Sửa frontend form + Staff Leader + HO + Visitor detail

### 15.1. Submit form

Bỏ:

```text
Dropdown transportation type
Field transportation detail cũ
```

Thêm textarea:

```text
Label: Nhận diện phương tiện di chuyển tới FPTU
Placeholder: Ví dụ: Xe 16 chỗ màu trắng, biển số 30A-xxxxx, dự kiến tới cổng lúc 8:30.
Required: Không
Field: transportationNote
```

### 15.2. Staff Leader list

Sửa wording:

```text
Đơn chờ HO duyệt -> bỏ
Đơn chờ xử lý tại campus -> dùng
Duyệt -> Duyệt & gán host
```

Staff Leader phải thấy multi-campus instance thuộc campus mình ngay sau submit.

### 15.3. Staff Leader approve modal

Modal cần có:

```text
- Tóm tắt đoàn khách.
- Thông tin campus đang xử lý.
- Chọn host chính thức.
- Option “Tôi làm host chính”.
- Dropdown/list IC Staff cùng campus.
- Conflict warning nếu host bận.
- Ghi chú duyệt optional.
```

Nút submit:

```text
Duyệt & gán host
```

Disable nếu chưa chọn host.

### 15.4. Staff Leader reject modal

```text
- Lý do từ chối bắt buộc.
- Textarea rõ ràng.
- Submit: Từ chối campus này.
```

### 15.5. HO page

```text
- Bỏ approve/reject buttons.
- Bỏ tab chờ duyệt liên cơ sở nếu chỉ dùng cho approval.
- Giữ monitor table nếu cần.
- Detail hiển thị trạng thái từng campus, read-only.
```

### 15.6. Visitor detail

Hiển thị theo campus:

```text
FPTU Hà Nội: Đã tiếp nhận — Host: ...
FPTU HCM: Từ chối — Lý do: ...
FPTU Đà Nẵng: Chờ xử lý
```

Không chỉ hiển thị một status tổng.

### 15.7. Kiểm tra hoàn thành

```text
[ ] Form gửi transportationNote.
[ ] Không còn transportationType trên UI.
[ ] Staff Leader approve bắt buộc host.
[ ] HO không còn nút duyệt.
[ ] Visitor thấy trạng thái từng campus.
```

---

## Bước 16 — Sửa report/dashboard/visit process summary

### 16.1. Mục tiêu

Dashboard/report không hiểu sai `PARTIALLY_APPROVED`, `REJECTED` ở campus, và không còn `WAITING_HOST_ASSIGNMENT`.

### 16.2. Dashboard counters

Sửa counters:

```text
Pending xử lý tại campus = visit_request_campuses.status = WAITING_REQUEST_APPROVAL theo campus scope.
Đã tiếp nhận = status IN (ASSIGNED, BEFORE_VISIT, DURING_VISIT, AFTER_VISIT, CLOSED).
Từ chối = visit_request_campuses.status = REJECTED.
Hủy = visit_request_campuses.status = CANCELLED hoặc request.status = CANCELLED tùy scope.
```

### 16.3. Report tổng

Request-level report:

```text
PENDING_APPROVAL
PARTIALLY_APPROVED
APPROVED
REJECTED
CANCELLED
```

Campus-level report:

```text
WAITING_REQUEST_APPROVAL
ASSIGNED
BEFORE_VISIT
DURING_VISIT
AFTER_VISIT
CLOSED
CANCELLED
REJECTED
```

### 16.4. Visit process summary

Không check cứng:

```text
visit_requests.status = APPROVED
```

để mở process. Phải dùng campus instance status:

```text
ASSIGNED/BEFORE_VISIT/DURING_VISIT/AFTER_VISIT/CLOSED
```

### 16.5. Kiểm tra hoàn thành

```text
[ ] Dashboard Staff Leader đếm pending theo campus instance.
[ ] HO dashboard không có pending approval action.
[ ] Reports hiểu PARTIALLY_APPROVED.
[ ] Không còn WAITING_HOST_ASSIGNMENT trong chart/filter/badge.
```

---

## Bước 17 — Update docs/report

### 17.1. Tài liệu cần cập nhật

```text
PEMS_CANONICAL_BUSINESS_RULES...
VISITOR_MANAGEMENT_SYSTEM...
PROJECT_OVERVIEW...
PEMS_UC_IMPLEMENTATION_RULEBOOK...
PEMS_CLAUDE_PROJECT_INSTRUCTIONS...
PERMISSION_MATRIX / PERMISSION_RULES nếu còn dùng tham khảo
USE_CASE_LIST / USE_CASE_NOTES
SQL Table & Field Dictionary
Report 3.0 SRS
Report 3.1 UCS
Report 3.1 RTW
Report 3.2 Screen Design Spec
Report 4 TDS
Report 5.x Test cases
Seed README / changelog nếu có
```

### 17.2. Cụm từ cần tìm và sửa/xóa

```text
HO duyệt multi-campus
Approve Cross-Campus Request
WAITING_HOST_ASSIGNMENT
Staff Leader gán host sau HO approve
Staff Leader không được làm host
Host candidate chỉ STAFF + STAFF
transportation_type
transportation_detail
SELF_ARRANGED
FPTU_SUPPORT
UNKNOWN
OTHER
decision ở visit_requests
```

### 17.3. Nội dung thay thế chuẩn

```text
Multi-campus request được tách thành từng campus instance và gửi trực tiếp đến Staff Leader của từng campus.
Staff Leader approve campus instance bắt buộc chọn host ngay.
Approve thành công chuyển campus instance từ WAITING_REQUEST_APPROVAL sang ASSIGNED.
Staff Leader được chọn chính mình làm host.
HO không còn approve/reject multi-campus, chỉ monitor/read-only nếu còn màn HO.
Transportation là text tự do: transportation_note.
```

### 17.4. Kiểm tra hoàn thành

```text
[ ] Docs không còn flow HO approve multi-campus.
[ ] Docs không còn WAITING_HOST_ASSIGNMENT.
[ ] Docs mô tả PARTIALLY_APPROVED.
[ ] Docs mô tả decision fields ở visit_request_campuses.
[ ] Docs mô tả transportation_note.
```

---

## Bước 18 — Chạy backend build

### 18.1. Lệnh tham khảo

Tùy cấu trúc solution thực tế:

```bash
dotnet restore

dotnet build
```

Hoặc:

```bash
dotnet build PEMS.slnx
```

### 18.2. Lỗi thường gặp cần xử lý

```text
Enum WAITING_HOST_ASSIGNMENT không còn tồn tại.
TransportationType/TransportationDetail không còn tồn tại.
VisitRequest.DecidedBy không còn tồn tại.
DTO thiếu transportationNote.
Mapper vẫn map field cũ.
Query still references request-level decision fields.
```

### 18.3. Definition of Done

```text
[ ] Backend build 0 errors.
[ ] Không suppress lỗi bằng cách comment bừa logic.
[ ] Không thêm lại field cũ để build tạm.
```

---

## Bước 19 — Chạy frontend build

### 19.1. Lệnh tham khảo

```bash
cd frontend/pems-react
npm install
npm run build
```

Hoặc nếu project dùng pnpm/yarn thì dùng package manager hiện tại của repo.

### 19.2. Lỗi thường gặp cần xử lý

```text
Type VisitRequest vẫn có transportationType.
UI badge vẫn có WAITING_HOST_ASSIGNMENT.
Approve modal chưa truyền hostUserId.
AllowedActions type chưa cập nhật.
Visitor detail chưa có campusDecisionSummary/campusInstances.
```

### 19.3. Definition of Done

```text
[ ] Frontend build 0 errors.
[ ] Không còn UI dùng transportationType.
[ ] Không còn label/nút HO approve multi-campus.
[ ] Staff Leader approve modal bắt buộc host.
```

---

## Bước 20 — Chạy unit/integration/manual test

### 20.1. Unit test backend

Cần có test:

```text
[ ] Submit multi-campus route tới từng Staff Leader.
[ ] Submit fail nếu campus thiếu Staff Leader active.
[ ] HO approve multi-campus bị 403/410.
[ ] Staff Leader HN approve HN instance thành ASSIGNED.
[ ] Staff Leader HN không approve được HCM instance.
[ ] Approve không có host -> HOST_REQUIRED_ON_APPROVAL.
[ ] Staff Leader tự chọn mình làm host -> thành công.
[ ] Staff Leader chọn Staff Leader khác làm host -> lỗi.
[ ] Reject campus thiếu decisionNote -> lỗi.
[ ] Reject một campus không reject toàn bộ request nếu campus khác còn pending/assigned.
[ ] Aggregate status đúng PENDING/PARTIALLY_APPROVED/APPROVED/REJECTED.
[ ] transportationNote optional, trim, lưu đúng text.
[ ] transportationNote không nhận HTML/script.
```

### 20.2. Integration test

Flow chính:

```text
1. Visitor submit request chọn HN + HCM.
2. HN Staff Leader thấy HN instance ngay.
3. HCM Staff Leader thấy HCM instance ngay.
4. HO không có action duyệt.
5. HN Staff Leader approve + self-host.
6. HCM Staff Leader reject.
7. Request tổng = APPROVED nếu tất cả campus đã xử lý và có ít nhất một campus assigned.
8. Visitor detail thấy HN assigned, HCM rejected.
9. Host nhận task/notification.
10. Logistics/minutes không được tạo trước khi campus assigned.
```

Flow partial:

```text
1. Visitor submit HN + HCM + DN.
2. HN approve.
3. HCM pending.
4. DN rejected.
5. Request tổng = PARTIALLY_APPROVED.
```

Flow all rejected:

```text
1. HN rejected.
2. HCM rejected.
3. Request tổng = REJECTED.
```

### 20.3. Manual test frontend

```text
[ ] Form không còn dropdown transportation type.
[ ] Textarea nhận diện phương tiện gửi đúng transportationNote.
[ ] Staff Leader thấy multi-campus instance ngay sau submit.
[ ] Modal duyệt bắt buộc chọn host.
[ ] Option “Tôi làm host chính” hiển thị đúng.
[ ] Host conflict warning hiển thị đúng.
[ ] Reject reason bắt buộc.
[ ] HO chỉ xem, không duyệt.
[ ] Visitor thấy trạng thái từng campus.
[ ] Badge status không còn WAITING_HOST_ASSIGNMENT.
```

### 20.4. SQL import test

Với file SQL fresh-create mới:

```bash
mysql -u root -p < pems_full_v10_new_final_campus_independent_approval_self_host_transport_note_FULL_FIXED.sql
```

Kiểm tra sau import:

```sql
SELECT status, COUNT(*) FROM visit_requests GROUP BY status;
SELECT status, COUNT(*) FROM visit_request_campuses GROUP BY status;
SHOW COLUMNS FROM visit_requests LIKE 'transportation%';
SHOW COLUMNS FROM visit_request_campuses LIKE 'decided%';
```

Kỳ vọng:

```text
visit_requests có transportation_note, không có transportation_type/transportation_detail.
visit_requests không có decided_*.
visit_request_campuses có decided_*.
Không có WAITING_HOST_ASSIGNMENT trong dữ liệu.
Có thể có PARTIALLY_APPROVED trong visit_requests nếu seed có case partial.
```

---

# 4. Checklist cuối cùng

## 4.1. Backend checklist

```text
[ ] Entity khớp SQL mới.
[ ] Enum khớp SQL mới.
[ ] DTO khớp API mới.
[ ] Submit route tới Staff Leader từng campus.
[ ] Approve campus bắt buộc host.
[ ] Reject campus lưu reason.
[ ] Aggregate status service hoạt động.
[ ] HO approve/reject disabled.
[ ] Host candidate có self-host.
[ ] Participant host tạo đúng.
[ ] AllowedActions theo từng campus instance.
[ ] Notification/email đúng actor mới.
[ ] Build backend pass.
```

## 4.2. Frontend checklist

```text
[ ] Form dùng transportationNote.
[ ] Không còn transportation enum.
[ ] Staff Leader list hiển thị pending campus instance.
[ ] Approve modal có chọn host bắt buộc.
[ ] Có option “Tôi làm host chính”.
[ ] Reject modal bắt buộc lý do.
[ ] HO không còn action duyệt.
[ ] Visitor detail hiển thị từng campus.
[ ] Dashboard/report không còn WAITING_HOST_ASSIGNMENT.
[ ] Build frontend pass.
```

## 4.3. Data/test/docs checklist

```text
[ ] Seed không có WAITING_HOST_ASSIGNMENT.
[ ] Seed có REJECTED campus.
[ ] Seed có PARTIALLY_APPROVED request nếu cần demo.
[ ] Seed có Staff Leader self-host case.
[ ] Seed có transportation_note.
[ ] Unit tests pass.
[ ] Integration tests pass.
[ ] Manual tests pass.
[ ] Docs/report cập nhật đầy đủ.
```

---

# 5. Definition of Done

Task cập nhật logic chỉ được xem là hoàn thành khi:

```text
1. SQL import sạch trên database mới.
2. Backend build 0 errors.
3. Frontend build 0 errors.
4. Không còn runtime reference WAITING_HOST_ASSIGNMENT.
5. Không còn runtime reference transportation_type / transportation_detail.
6. Không còn HO approve/reject multi-campus.
7. Staff Leader từng campus xử lý được multi-campus instance của mình ngay sau submit.
8. Approve bắt buộc chọn host và chuyển thẳng sang ASSIGNED.
9. Staff Leader có thể tự chọn mình làm host.
10. Visitor nhìn thấy trạng thái từng campus.
11. Request aggregate status tính đúng.
12. Unit/integration/manual test covering core flow pass.
13. Docs/report được cập nhật theo logic mới.
```
