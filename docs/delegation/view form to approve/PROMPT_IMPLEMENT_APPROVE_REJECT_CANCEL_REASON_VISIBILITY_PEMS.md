> [!WARNING]
> **LEGACY ARCHITECTURE NOTE (Campus-independent Approval Update)**
> This document has been updated to reflect the new Campus-independent Approval architecture.
> - **HO is now monitor/read-only.** There is no centralized multi-campus approval by HO.
> - **Staff Leader approval is per-campus.** Each Staff Leader directly receives and approves/rejects their own campus instance right after submission.
> - **Self-hosting is supported.** Staff Leaders can assign themselves as the host during approval.
> - **ASSIGNED is removed.** Approving a request now requires assigning a host immediately.
> - **New statuses:** `PARTIALLY_APPROVED` (request level) and `REJECTED` (campus level) are added. 
> - **Cancel logic:** Visitors can cancel requests in `PENDING_APPROVAL` or `PARTIALLY_APPROVED` states.
> - **Transportation:** `transportation_note` and `transportation_note` are replaced by `transportation_note`.
> Please refer to the latest codebase and SQL schema for the current implementation.

# PROMPT_IMPLEMENT_APPROVE_REJECT_CANCEL_AND_REASON_VISIBILITY_PEMS

> Mục tiêu: hướng dẫn AI Agent cập nhật code PEMS cho các luồng **Duyệt / Từ chối đơn** của **HO và Staff Leader**, luồng **Cancel** của **Visitor và Host**, và phần **xem lý do hủy / lý do từ chối** theo đúng role/scope/status canonical v10.

---

## 0. Kết luận nghiệp vụ cần code

### 0.1 Duyệt / Từ chối trước khi duyệt

Nếu request còn đang chờ duyệt:

```text
visit_requests.status = PENDING_APPROVAL
```

thì chỉ có 2 hướng:

```text
APPROVED  = được duyệt
REJECTED  = bị từ chối
```

Không dùng `CANCELLED` cho request đang `PENDING_APPROVAL`.

Actor xử lý:

```text
SINGLE_CAMPUS  -> Staff Leader đúng campus duyệt / từ chối
MULTI_CAMPUS   -> HO duyệt / từ chối
```

### 0.2 Cancel sau khi đã duyệt

Cancel chỉ dùng sau khi request đã được duyệt:

```text
visit_requests.status = APPROVED
```

Actor cancel được code trong phạm vi hiện tại:

```text
Visitor owner  -> tự hủy request / instance của chính mình
Host           -> hủy campus instance mình phụ trách khi đã có xác nhận ngoài hệ thống
```

Không code:

```text
Staff Leader cancel after approved
HO cancel after approved
Admin cancel delegation
Department cancel delegation
Student cancel delegation
SYSTEM cancel delegation
```

Nếu nghiệp vụ sau này muốn Staff Leader/HO cancel sau approved thì phải chốt lại business rule và schema trước.

### 0.3 Xem lý do từ chối / lý do hủy

- Lý do **từ chối trước duyệt** lấy từ:

```text
visit_requests.decision_note
```

- Lý do **hủy sau duyệt** lấy từ:

```text
visit_requests.cancellation_reason
visit_request_campuses.cancellation_reason
```

Không dùng lẫn:

```text
Không dùng cancellation_reason làm lý do reject.
Không dùng decision_note làm lý do cancel.
```

---

## 1. File nguồn nghiệp vụ cần đọc trước khi code

Bắt buộc đọc các file sau trong repo:

```text
docs/PEMS_CANONICAL_BUSINESS_RULES_v8_4_refined_v6_v10_FULL_UPDATED.md
docs/VISITOR_MANAGEMENT_SYSTEM_v8_4_refined_v6_v10_FULL_UPDATED.md
docs/PEMS_UC_IMPLEMENTATION_RULEBOOK_FRONTEND_BACKEND_DATABASE_VALIDATION_SECURITY_v8_4_refined_v6_v10_FULL_UPDATED.md
docs/database/DATABASE_SCHEMA_v8_4_refined_v6_v10_no_dynamic_permissions_FULL_UPDATED.md
docs/architecture/PROJECT_STRUCTURE_FULL.md
```

Nếu tài liệu cũ mâu thuẫn với canonical v10, ưu tiên canonical v10.

Không dùng dynamic permissions:

```text
Không tạo permissions
Không tạo role_permissions
Không query permission động
Không hard-code STAFF_LEADER
```

Role Staff Leader phải xác định bằng:

```text
role_code = STAFF
sub_role = LEADER
```

---

## 2. Các bảng/cột chính cần dùng

### 2.1 `visit_requests`

Dùng cho trạng thái tổng của request/form:

```text
visit_request_id
request_code
visitor_user_id
visit_scope
status
decided_by
decided_at
decision_actor_role
decision_note
cancelled_by
cancelled_at
cancellation_actor_type
cancellation_source
cancellation_reason
row_version nếu code đang dùng optimistic concurrency
```

Status hợp lệ:

```text
PENDING_APPROVAL
APPROVED
REJECTED
CANCELLED
```

### 2.2 `visit_request_campuses`

Dùng cho từng campus instance:

```text
visit_instance_id
visit_request_id
campus_id
planned_start_at
planned_end_at
status
coordinator_user_id
coordinator_assigned_by
coordinator_assigned_at
current_host_user_id
host_assigned_by
host_assigned_at
cancelled_by
cancelled_at
cancellation_actor_type
cancellation_source
cancellation_reason
```

Status instance liên quan:

```text
WAITING_REQUEST_APPROVAL
ASSIGNED
ASSIGNED
BEFORE_VISIT
DURING_VISIT
AFTER_VISIT
CLOSED
CANCELLED
```

---

## 3. Backend files cần kiểm tra/cập nhật

### API controllers

```text
backend/PEMS.Api/Controllers/DelegationsController.cs
backend/PEMS.Api/Controllers/VisitRequestsController.cs
```

Controller chỉ nhận request, gọi MediatR, trả response. Không query DbContext trực tiếp trong controller.

### Commands

```text
backend/PEMS.Application/Delegations/Commands/ProcessVisitRequest/
backend/PEMS.Application/Delegations/Commands/RejectVisitRequest/
backend/PEMS.Application/Delegations/Commands/ApproveCrossCampusRequest/
backend/PEMS.Application/Delegations/Commands/CancelVisitRequest/
```

Nếu project đang có command khác tên nhưng cùng chức năng thì dùng command hiện có, không tạo trùng nếu không cần.

### Queries / detail

```text
backend/PEMS.Application/Delegations/Queries/ViewPreApprovalVisitRequestReview/
backend/PEMS.Application/Delegations/Queries/ViewGuestDelegationDetails/
backend/PEMS.Application/Delegations/Queries/ViewGuestDelegationList/
backend/PEMS.Application/Delegations/Queries/SearchDelegations/
```

Khuyến nghị nếu chưa có:

```text
backend/PEMS.Application/Delegations/Queries/GetSubmittedVisitRequestFormDetail/
├── GetSubmittedVisitRequestFormDetailQuery.cs
├── GetSubmittedVisitRequestFormDetailQueryHandler.cs
└── SubmittedVisitRequestFormDetailDto.cs
```

### Domain / Security

```text
backend/PEMS.Domain/Entities/Delegations/VisitRequest.cs
backend/PEMS.Domain/Entities/Delegations/VisitRequestCampus.cs

backend/PEMS.Domain/Enums/VisitRequestStatus.cs
backend/PEMS.Domain/Enums/VisitInstanceStatus.cs
backend/PEMS.Domain/Enums/VisitScope.cs
backend/PEMS.Domain/Enums/DecisionActorRole.cs
backend/PEMS.Domain/Enums/CancellationActorType.cs
backend/PEMS.Domain/Enums/CancellationSource.cs

backend/PEMS.Application/Common/Interfaces/ICurrentUserService.cs
backend/PEMS.Application/Common/Security/
backend/PEMS.Infrastructure/Persistence/ApplicationDbContext.cs
```

---

## 4. Frontend files cần kiểm tra/cập nhật

```text
frontend/pems-react/src/pages/dashboard/visit/VisitRequestManagement.tsx
frontend/pems-react/src/pages/dashboard/visit/VisitRequestDetail.tsx
frontend/pems-react/src/pages/dashboard/visit/VisitProcess.tsx
frontend/pems-react/src/pages/dashboard/visit/HoVisitProcessDetail.tsx

frontend/pems-react/src/features/delegations/types/delegations.types.ts
frontend/pems-react/src/features/delegations/api/delegationsApi.ts
frontend/pems-react/src/features/delegations/adapters/delegationsAdapter.ts
frontend/pems-react/src/features/delegations/hooks/useDelegations.ts
frontend/pems-react/src/features/delegations/config/visitRequestFilterConfig.ts

frontend/pems-react/src/shared/api/endpoints.ts
frontend/pems-react/src/shared/constants/v10Domain.ts

frontend/pems-react/src/components/modals/VisitDetailsModal.tsx
frontend/pems-react/src/components/modals/PreApprovalVisitRequestReviewModal.tsx
frontend/pems-react/src/components/modals/AssignHostModal.tsx
```

Khuyến nghị tạo thêm component:

```text
frontend/pems-react/src/features/delegations/components/SubmittedVisitRequestInfoPanel.tsx
frontend/pems-react/src/features/delegations/components/RejectVisitRequestModal.tsx
frontend/pems-react/src/features/delegations/components/CancelVisitRequestModal.tsx
frontend/pems-react/src/features/delegations/components/DecisionReasonPanel.tsx
frontend/pems-react/src/features/delegations/components/CancellationReasonPanel.tsx
```

---

## 5. API cần có / cần nối

### 5.1 Approve visit request

Endpoint đề xuất:

```http
POST /api/delegations/visit-requests/{visitRequestId}/approve
```

Body:

```json
{
  "decisionNote": "Đồng ý tiếp nhận"
}
```

`decisionNote` khi approve có thể optional.

Nếu backend hiện đã có route approve khác, frontend phải dùng đúng route đang tồn tại. Không tạo route trùng.

### 5.2 Reject visit request

Endpoint đề xuất:

```http
POST /api/delegations/visit-requests/{visitRequestId}/reject
```

Body:

```json
{
  "reason": "Không phù hợp lịch tiếp khách của cơ sở"
}
```

Validator:

```text
reason required
trim(reason).Length >= 5
trim(reason).Length <= 1000
```

### 5.3 Cancel visit request / campus instance

Endpoint đề xuất cho cancel request tổng:

```http
POST /api/delegations/visit-requests/{visitRequestId}/cancel
```

Body:

```json
{
  "reason": "Khách tự hủy do thay đổi lịch công tác",
  "scope": "FULL_REQUEST"
}
```

Endpoint đề xuất cho cancel campus instance:

```http
POST /api/delegations/visit-instances/{visitInstanceId}/cancel
```

Body cho Visitor partial cancel hoặc Host cancel:

```json
{
  "reason": "Khách xác nhận hủy qua email lúc 09:30 ngày 24/06/2026, người xác nhận: Nguyễn Văn A, lý do: thay đổi lịch công tác",
  "scope": "CAMPUS_INSTANCE"
}
```

Nếu project hiện chỉ có `CancelVisitRequestCommand` dùng chung, có thể truyền thêm `visitInstanceId` nullable trong body. Không cần ép đúng route trên nếu code hiện tại đã có contract khác, nhưng phải đảm bảo rõ request-level và instance-level.

### 5.4 Detail endpoint phải trả reason info

Endpoint detail đang được dùng bởi modal/page phải trả thêm:

```text
decisionInfo
cancellationInfo
availableActions
```

Nếu có endpoint mới:

```http
GET /api/delegations/visit-requests/{visitRequestId}/submitted-form-detail
```

hoặc dùng detail hiện có, nhưng response phải có đủ reason info.

---

## 6. DTO detail cần có

Bổ sung vào DTO detail / submitted form detail:

```csharp
public sealed class VisitRequestDecisionInfoDto
{
    public string? DecisionStatus { get; set; }
    public string? DecisionActorRole { get; set; }
    public long? DecidedByUserId { get; set; }
    public string? DecidedByFullName { get; set; }
    public DateTime? DecidedAt { get; set; }
    public string? DecisionNote { get; set; }
}

public sealed class VisitRequestCancellationInfoDto
{
    public bool IsCancelled { get; set; }

    // REQUEST hoặc CAMPUS_INSTANCE
    public string? CancellationLevel { get; set; }

    public long? CancelledByUserId { get; set; }
    public string? CancelledByFullName { get; set; }
    public DateTime? CancelledAt { get; set; }

    public string? CancellationActorType { get; set; }
    public string? CancellationSource { get; set; }
    public string? CancellationReason { get; set; }
}

public sealed class VisitRequestAvailableActionsDto
{
    public bool CanApprove { get; set; }
    public bool CanReject { get; set; }
    public bool CanCancel { get; set; }
    public bool CanAssignHost { get; set; }
}
```

TypeScript tương ứng:

```ts
export type VisitRequestDecisionInfo = {
  decisionStatus?: string | null;
  decisionActorRole?: string | null;
  decidedByUserId?: number | null;
  decidedByFullName?: string | null;
  decidedAt?: string | null;
  decisionNote?: string | null;
};

export type VisitRequestCancellationInfo = {
  isCancelled: boolean;
  cancellationLevel?: 'REQUEST' | 'CAMPUS_INSTANCE' | null;
  cancelledByUserId?: number | null;
  cancelledByFullName?: string | null;
  cancelledAt?: string | null;
  cancellationActorType?: string | null;
  cancellationSource?: string | null;
  cancellationReason?: string | null;
};

export type VisitRequestAvailableActions = {
  canApprove: boolean;
  canReject: boolean;
  canCancel: boolean;
  canAssignHost: boolean;
};
```

---

## 7. Approve / Reject business rules

### 7.1 Staff Leader approve single-campus

Cho phép nếu:

```text
currentUser.role_code = STAFF
currentUser.sub_role = LEADER
visit_requests.visit_scope = SINGLE_CAMPUS
visit_requests.status = PENDING_APPROVAL
exists visit_request_campuses:
  campus_id = currentUser.primary_campus_id
  status = WAITING_REQUEST_APPROVAL
```

Khi approve:

```text
visit_requests.status = APPROVED
visit_requests.decided_by = currentUser.user_id
visit_requests.decided_at = now
visit_requests.decision_actor_role = STAFF_LEADER
visit_requests.decision_note = decisionNote nếu có

visit_request_campuses.status = ASSIGNED
```

Không tự gán host trong approve nếu luồng hiện tại tách bước gán host sau đó.

### 7.2 Staff Leader reject single-campus

Cho phép nếu cùng điều kiện với approve single-campus.

Khi reject:

```text
visit_requests.status = REJECTED
visit_requests.decided_by = currentUser.user_id
visit_requests.decided_at = now
visit_requests.decision_actor_role = STAFF_LEADER
visit_requests.decision_note = reason bắt buộc
```

Không dùng `CANCELLED`.

### 7.3 HO approve multi-campus

Cho phép nếu:

```text
currentUser.role_code = HO
visit_requests.visit_scope = MULTI_CAMPUS
visit_requests.status = PENDING_APPROVAL
all related visit_request_campuses.status = WAITING_REQUEST_APPROVAL
```

Khi approve:

```text
visit_requests.status = APPROVED
visit_requests.decided_by = currentUser.user_id
visit_requests.decided_at = now
visit_requests.decision_actor_role = HO
visit_requests.decision_note = decisionNote nếu có
```

Với từng campus instance:

```text
visit_request_campuses.status = ASSIGNED
visit_request_campuses.coordinator_user_id = Staff Leader của campus đó nếu tìm được
visit_request_campuses.coordinator_assigned_by = currentUser.user_id
visit_request_campuses.coordinator_assigned_at = now
```

Sau đó Staff Leader từng campus mới nhìn thấy instance của mình và gán host chính thức.

Không làm:

```text
Không để từng Staff Leader duyệt lại request tổng sau HO.
Không auto coi Staff Leader là host chính thức.
Không cho Staff Leader campus khác thấy instance không thuộc campus mình.
```

### 7.4 HO reject multi-campus

Cho phép nếu:

```text
currentUser.role_code = HO
visit_requests.visit_scope = MULTI_CAMPUS
visit_requests.status = PENDING_APPROVAL
```

Khi reject:

```text
visit_requests.status = REJECTED
visit_requests.decided_by = currentUser.user_id
visit_requests.decided_at = now
visit_requests.decision_actor_role = HO
visit_requests.decision_note = reason bắt buộc
```

Không chuyển campus instance sang `CANCELLED`.

### 7.5 Forbidden cases

Backend phải chặn:

```text
HO approve/reject SINGLE_CAMPUS -> 403
Staff Leader approve/reject MULTI_CAMPUS pending HO -> 403
Staff Leader campus khác approve/reject SINGLE_CAMPUS -> 403
Staff thường approve/reject -> 403
Department Leader/Staff approve/reject -> 403
Student approve/reject -> 403
Visitor approve/reject -> 403
Admin approve/reject business delegation -> 403
Request không còn PENDING_APPROVAL -> 409
Campus instance không còn WAITING_REQUEST_APPROVAL khi duyệt single-campus -> 409
```

---

## 8. Cancel business rules

### 8.1 Không cancel trước duyệt

Nếu:

```text
visit_requests.status = PENDING_APPROVAL
```

thì không cho cancel. Người duyệt phải dùng reject flow.

Kết quả:

```text
Cancel API -> 409 hoặc 400
Message: Đơn đang chờ duyệt, nếu không tiếp nhận hãy dùng luồng từ chối.
```

### 8.2 Visitor self-service cancel

Cho phép nếu:

```text
currentUser.role_code = VISITOR
currentUser.user_id = visit_requests.visitor_user_id
visit_requests.status = APPROVED
target instance chưa DURING_VISIT / AFTER_VISIT / CLOSED
```

Set metadata request-level hoặc instance-level:

```text
cancelled_by = currentUser.user_id
cancelled_at = now
cancellation_actor_type = VISITOR
cancellation_source = SELF_SERVICE
cancellation_reason = reason do Visitor nhập
```

Trường hợp single-campus:

```text
visit_requests.status = CANCELLED
visit_request_campuses.status = CANCELLED
```

Trường hợp multi-campus full cancel:

```text
visit_requests.status = CANCELLED
tất cả active future instances = CANCELLED
```

Trường hợp multi-campus partial cancel:

```text
chỉ visit_request_campuses.status của campus target = CANCELLED
visit_requests.status vẫn = APPROVED nếu còn instance active
```

### 8.3 Host cancel by external confirmation

Cho phép nếu:

```text
currentUser.user_id = visit_request_campuses.current_host_user_id
visit_requests.status = APPROVED
visit_request_campuses.status NOT IN (DURING_VISIT, AFTER_VISIT, CLOSED, CANCELLED)
khách đã xác nhận hủy ngoài hệ thống
reason bắt buộc ghi rõ kênh/thời điểm/người xác nhận/lý do
```

Set metadata ở campus instance:

```text
visit_request_campuses.status = CANCELLED
visit_request_campuses.cancelled_by = currentUser.user_id
visit_request_campuses.cancelled_at = now
visit_request_campuses.cancellation_actor_type = HOST
visit_request_campuses.cancellation_source = EXTERNAL_CONFIRMATION
visit_request_campuses.cancellation_reason = reason
```

Nếu single-campus và host cancel instance duy nhất:

```text
visit_requests.status = CANCELLED
visit_requests.cancelled_by = currentUser.user_id
visit_requests.cancelled_at = now
visit_requests.cancellation_actor_type = HOST
visit_requests.cancellation_source = EXTERNAL_CONFIRMATION
visit_requests.cancellation_reason = reason
```

Nếu multi-campus và còn instance active:

```text
visit_requests.status vẫn APPROVED
chỉ instance hiện tại CANCELLED
```

Nếu multi-campus và tất cả instance đều cancelled:

```text
visit_requests.status = CANCELLED
```

### 8.4 Forbidden cancel cases

Backend phải chặn:

```text
Staff Leader cancel after approved -> 403 hoặc không có route/action
HO cancel after approved -> 403 hoặc không có route/action
Admin cancel delegation -> 403
Department cancel delegation -> 403
Student cancel delegation -> 403
Visitor không phải owner -> 403
Host không phải current_host_user_id -> 403
Cancel instance DURING_VISIT / AFTER_VISIT / CLOSED -> 409
Cancel request REJECTED -> 409
Cancel request đã CANCELLED -> 409
Cancel instance đã CANCELLED -> 409
```

---

## 9. Xem lý do từ chối

### 9.1 Khi nào hiển thị

Nếu:

```text
visit_requests.status = REJECTED
decision_note IS NOT NULL
```

UI phải hiển thị panel:

```text
Lý do từ chối
- Vai trò xử lý: HO / Staff Leader
- Người xử lý: decidedByFullName hoặc ID nếu không join được
- Thời gian xử lý: decidedAt
- Nội dung: decisionNote
```

Không hiện nút Duyệt/Từ chối lại cho request `REJECTED`.

### 9.2 Role được xem lý do từ chối

Theo scope detail:

```text
Visitor owner:
- Xem lý do từ chối request của chính mình.

HO:
- Xem lý do từ chối multi-campus.

Staff Leader:
- Xem lý do từ chối single-campus thuộc campus mình.
- Không xem multi-campus bị HO từ chối, vì request đó không được release xuống campus con.

Admin:
- Không mặc định xem business delegation.

Các role khác:
- Chỉ xem nếu đã có detail visibility hợp lệ theo participant/task/ownership trong màn tương ứng.
```

---

## 10. Xem lý do hủy

### 10.1 Khi nào hiển thị

Nếu request bị hủy:

```text
visit_requests.status = CANCELLED
visit_requests.cancellation_reason IS NOT NULL
```

hiển thị request-level cancellation panel.

Nếu campus instance bị hủy:

```text
visit_request_campuses.status = CANCELLED
visit_request_campuses.cancellation_reason IS NOT NULL
```

hiển thị instance-level cancellation panel.

Panel UI:

```text
Lý do hủy
- Cấp hủy: Toàn bộ đơn / Cơ sở hiện tại
- Người hủy: cancelledByFullName hoặc ID
- Thời gian hủy: cancelledAt
- Vai trò hủy: Visitor / Host
- Nguồn hủy: Tự hủy / Xác nhận ngoài hệ thống
- Nội dung: cancellationReason
```

Nếu reason null:

```text
Chưa ghi nhận lý do hủy
```

### 10.2 Role được xem lý do hủy

Backend chỉ trả cancellation info nếu user đã có quyền xem detail request/instance.

Quy tắc đề xuất:

```text
Visitor owner:
- Xem lý do hủy request/campus instance của chính mình.

Host:
- Xem lý do hủy campus instance mình là current_host_user_id.
- Nếu host đã thực hiện hủy thay khách, phải xem lại được reason đã nhập.

Staff Leader:
- Xem lý do hủy single-campus thuộc campus mình.
- Xem lý do hủy multi-campus instance thuộc campus mình sau HO approve.
- Không xem multi-campus pending HO hoặc multi-campus rejected bởi HO.

HO:
- Xem lý do hủy multi-campus request tổng và các instance liên quan.
- Không xem single-campus nếu canonical vẫn quy định HO không xử lý single-campus.

IC Staff / Department / Student:
- Chỉ xem lý do hủy nếu họ có detail visibility hợp lệ với instance đó, ví dụ là participant/task/invitation liên quan.
- Chỉ trả cancellation info của instance họ được thấy, không trả toàn bộ request nếu ngoài scope.

Admin:
- Không mặc định xem business cancellation reason.
```

Frontend có thể ẩn/hiện panel theo data trả về, nhưng backend là nơi enforce scope.

---

## 11. Frontend UI yêu cầu

### 11.1 Nút Duyệt

Hiển thị khi:

```text
availableActions.canApprove = true
```

Click:

```text
1. Mở confirm modal: "Bạn có chắc muốn duyệt đơn đăng ký tham quan này không?"
2. User xác nhận.
3. Gọi approve API.
4. Thành công:
   - Toast "Duyệt đơn thành công"
   - Refresh list
   - Đóng hoặc reload detail modal
5. Thất bại:
   - Hiển thị lỗi backend
```

Không tự đổi status ở frontend nếu API chưa thành công.

### 11.2 Nút Từ chối

Hiển thị khi:

```text
availableActions.canReject = true
```

Click:

```text
1. Mở RejectVisitRequestModal.
2. Bắt buộc nhập lý do.
3. Submit gọi reject API.
4. Thành công:
   - Toast "Từ chối đơn thành công"
   - Refresh list
   - Đơn biến mất khỏi tab Chờ duyệt
   - Detail rejected có panel Lý do từ chối
```

Không dùng `window.prompt`.

### 11.3 Nút Cancel

Hiển thị khi:

```text
availableActions.canCancel = true
```

Với Visitor:

```text
Label: Hủy đơn
Modal title: Hủy yêu cầu tham quan
Reason required hoặc optional tùy validator hiện hành, nhưng khuyến nghị required để giải thích.
cancellation_source = SELF_SERVICE
```

Với Host:

```text
Label: Hủy theo xác nhận của khách
Modal title: Hủy chuyến thăm theo xác nhận ngoài hệ thống
Reason required
Placeholder: "Khách xác nhận hủy qua email/điện thoại/Zalo..., thời gian..., người xác nhận..., lý do..."
cancellation_source = EXTERNAL_CONFIRMATION
```

Không hiện cancel khi:

```text
DURING_VISIT
AFTER_VISIT
CLOSED
CANCELLED
REJECTED
PENDING_APPROVAL
```

### 11.4 Panel Lý do từ chối

Component:

```text
DecisionReasonPanel
```

Render nếu:

```text
requestStatus = REJECTED
decisionInfo.decisionNote exists
```

### 11.5 Panel Lý do hủy

Component:

```text
CancellationReasonPanel
```

Render nếu:

```text
cancellationInfo.isCancelled = true
```

hoặc:

```text
requestStatus = CANCELLED
campusStatus = CANCELLED
```

---

## 12. Frontend API functions

Trong `delegationsApi.ts`:

```ts
export async function approveVisitRequest(
  visitRequestId: number,
  payload?: { decisionNote?: string }
) {
  const res = await httpClient.post(
    endpoints.delegations.approveVisitRequest(visitRequestId),
    payload ?? {}
  );
  return unwrapApiResponse(res);
}

export async function rejectVisitRequest(
  visitRequestId: number,
  payload: { reason: string }
) {
  const res = await httpClient.post(
    endpoints.delegations.rejectVisitRequest(visitRequestId),
    payload
  );
  return unwrapApiResponse(res);
}

export async function cancelVisitRequest(
  visitRequestId: number,
  payload: { reason: string; scope?: 'FULL_REQUEST' }
) {
  const res = await httpClient.post(
    endpoints.delegations.cancelVisitRequest(visitRequestId),
    payload
  );
  return unwrapApiResponse(res);
}

export async function cancelVisitInstance(
  visitInstanceId: number,
  payload: { reason: string; scope?: 'CAMPUS_INSTANCE' }
) {
  const res = await httpClient.post(
    endpoints.delegations.cancelVisitInstance(visitInstanceId),
    payload
  );
  return unwrapApiResponse(res);
}
```

Trong `endpoints.ts`:

```ts
approveVisitRequest: (visitRequestId: number) =>
  `/delegations/visit-requests/${visitRequestId}/approve`,

rejectVisitRequest: (visitRequestId: number) =>
  `/delegations/visit-requests/${visitRequestId}/reject`,

cancelVisitRequest: (visitRequestId: number) =>
  `/delegations/visit-requests/${visitRequestId}/cancel`,

cancelVisitInstance: (visitInstanceId: number) =>
  `/delegations/visit-instances/${visitInstanceId}/cancel`,
```

Nếu backend đã có route khác, chỉnh theo route thật.

---

## 13. Backend implementation notes

### 13.1 Transaction

Approve/reject/cancel phải chạy trong transaction nếu cập nhật nhiều bảng:

```text
visit_requests
visit_request_campuses
notifications
sent_emails
audit_logs
```

Nếu chưa có notification/email ổn định, không fake success email; ghi TODO hoặc log theo policy hiện tại.

### 13.2 Concurrency

Nếu entity có `row_version`, kiểm tra stale update:

```text
Nếu row_version trong request cũ hơn DB -> 409
Không overwrite decision/cancellation metadata đã tồn tại.
```

Nếu chưa dùng row_version ở command hiện tại, không bắt buộc thêm ngay, nhưng không được ghi đè request đã REJECTED/CANCELLED/APPROVED bằng action cũ.

### 13.3 Error code/message

Đề xuất:

```text
403: Không có quyền thực hiện thao tác này.
409: Đơn không còn ở trạng thái chờ duyệt.
409: Chuyến thăm đã bắt đầu/kết thúc/đóng, không thể hủy.
400: Vui lòng nhập lý do từ chối.
400: Vui lòng nhập lý do hủy.
404: Không tìm thấy đơn đăng ký tham quan.
```

---

## 14. Test cases bắt buộc

### 14.1 Approve/Reject

```text
1. Staff Leader approve single-campus đúng campus -> request APPROVED, instance ASSIGNED.
2. Staff Leader reject single-campus đúng campus -> request REJECTED, decision_note lưu đúng.
3. Staff Leader reject reason rỗng -> validation error.
4. Staff Leader campus khác approve/reject -> 403.
5. Staff Leader approve/reject multi-campus pending HO -> 403.
6. HO approve multi-campus -> request APPROVED, all instances ASSIGNED, coordinator_user_id set nếu có Staff Leader.
7. HO reject multi-campus -> request REJECTED, decision_note lưu đúng.
8. HO approve/reject single-campus -> 403.
9. Admin/Staff thường/Department/Student/Visitor approve/reject -> 403.
10. Approve/reject request không còn PENDING_APPROVAL -> 409.
```

### 14.2 Cancel

```text
1. Visitor owner cancel approved single-campus future -> request CANCELLED, instance CANCELLED, actor VISITOR, source SELF_SERVICE.
2. Visitor owner cancel one campus in approved multi-campus -> target instance CANCELLED, request vẫn APPROVED nếu còn instance active.
3. Visitor không phải owner cancel -> 403.
4. Host current_host_user_id cancel own instance -> instance CANCELLED, actor HOST, source EXTERNAL_CONFIRMATION, reason required.
5. Host cancel instance không phải mình -> 403.
6. Host cancel DURING_VISIT/AFTER_VISIT/CLOSED -> 409.
7. Cancel PENDING_APPROVAL -> 409, yêu cầu dùng reject flow.
8. Cancel REJECTED -> 409.
9. Cancel already CANCELLED -> 409.
10. Staff Leader/HO/Admin/Department/Student cancel after approved -> 403 hoặc không có action.
```

### 14.3 Reason visibility

```text
1. Visitor owner mở request REJECTED -> thấy decision_note.
2. Staff Leader mở single-campus rejected thuộc campus mình -> thấy decision_note.
3. Staff Leader không thấy multi-campus rejected bởi HO -> 403 hoặc không có trong list.
4. HO mở multi-campus rejected -> thấy decision_note.
5. Visitor owner mở cancelled request -> thấy cancellation_reason.
6. Host mở cancelled instance mình hủy -> thấy cancellation_reason.
7. Staff Leader mở cancelled single-campus campus mình -> thấy cancellation_reason.
8. HO mở cancelled multi-campus -> thấy cancellation_reason.
9. IC Staff/Department/Student chỉ thấy reason nếu có detail scope liên quan.
10. Admin không mặc định thấy business reason.
```

---

## 15. Manual test checklist

```text
[ ] Login Staff Leader HN.
[ ] Mở đơn single-campus HN PENDING_APPROVAL.
[ ] Bấm Duyệt -> confirm -> success -> status Đã duyệt / Chờ gán Host.
[ ] Mở đơn single-campus HN khác PENDING_APPROVAL.
[ ] Bấm Từ chối -> nhập lý do -> success -> status Đã từ chối.
[ ] Mở detail đơn đã từ chối -> thấy panel Lý do từ chối.
[ ] Login HO.
[ ] Mở đơn multi-campus PENDING_APPROVAL.
[ ] Bấm Duyệt -> success -> Staff Leader từng campus thấy instance của mình.
[ ] Mở multi-campus khác -> Từ chối -> thấy Lý do từ chối ở HO/Visitor.
[ ] Login Visitor owner.
[ ] Mở approved future request -> bấm Hủy đơn -> nhập reason -> success -> thấy Lý do hủy.
[ ] Login Host.
[ ] Mở instance mình phụ trách BEFORE_VISIT/ASSIGNED -> bấm Hủy theo xác nhận khách -> nhập reason đầy đủ -> success.
[ ] Login Staff thường/Department/Student/Admin -> không thấy nút approve/reject/cancel ngoài scope.
```

---

## 16. Không được làm

```text
- Không dùng CANCELLED thay cho REJECTED khi request đang PENDING_APPROVAL.
- Không dùng decision_note làm lý do hủy.
- Không dùng cancellation_reason làm lý do từ chối.
- Không cho HO xử lý single-campus.
- Không cho Staff Leader xử lý multi-campus pending HO.
- Không cho Staff Leader xem multi-campus rejected bởi HO.
- Không cho Staff Leader/HO/Admin cancel after approved nếu canonical hiện tại chưa cho.
- Không hard-code STAFF_LEADER.
- Không dùng dynamic permissions.
- Không chỉ hide button trên frontend; backend phải enforce.
- Không tự đổi status client-side nếu API fail.
- Không dùng mock data.
- Không tạo thêm cột SQL nếu schema đã có field.
```

---

## 17. Build/check

Sau khi sửa backend:

```bash
dotnet build
```

Nếu test project chạy được:

```bash
dotnet test
```

Sau khi sửa frontend:

```bash
cd frontend/pems-react
npm run build
```

Nếu có thêm/sửa route/action controller, phải restart backend:

```bash
dotnet run --project backend/PEMS.Api
```

---

## 18. Báo cáo sau khi code

Báo cáo theo format:

```text
1. Summary
- Đã triển khai approve/reject cho HO và Staff Leader.
- Đã triển khai cancel cho Visitor và Host.
- Đã hiển thị lý do từ chối/lý do hủy theo role/scope.

2. Files changed
Backend:
Frontend:

3. API implemented/connected
- Approve:
- Reject:
- Visitor cancel:
- Host cancel:
- Detail reason:

4. Business rules implemented
- Staff Leader single-campus approve/reject:
- HO multi-campus approve/reject:
- Visitor cancel:
- Host cancel:
- Reason visibility:

5. UI behavior
- Nút Duyệt:
- Nút Từ chối:
- Nút Hủy của Visitor:
- Nút Hủy của Host:
- Panel Lý do từ chối:
- Panel Lý do hủy:

6. Verification
- dotnet build:
- npm run build:
- dotnet test nếu có:
- Manual test cases:

7. Known limitations / next steps
- ...
```
