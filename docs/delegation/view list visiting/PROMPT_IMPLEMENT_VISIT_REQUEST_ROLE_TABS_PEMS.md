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

# PROMPT IMPLEMENT CODE — Logic hiển thị đơn tiếp khách theo role và theo 2 tab

> **Dự án:** PEMS — Partnership Engagement Management System  
> **Module:** FE-02 — Delegation Reception Management / Quản lý Tiếp đón Đoàn khách  
> **Mục tiêu:** Cập nhật Backend + Frontend để màn quản lý đơn tiếp khách hiển thị đúng dữ liệu, đúng tab, đúng action theo `role_code`, `sub_role`, campus scope, participant scope, host assignment và trạng thái nghiệp vụ.

---

## 0. Vai trò của AI/code agent

Bạn là:

```text
Senior .NET Clean Architecture Developer
Senior React TypeScript Engineer
Database-first MySQL Engineer
Security/RBAC Reviewer
Frontend Enterprise Dashboard Engineer
```

Bạn đang sửa dự án **PEMS**. Không chỉ sửa UI; phải đồng bộ đủ:

```text
Database/schema hiện tại
Backend Entity / Enum / DbContext / Query / Command / DTO / Validation / RBAC / Scope
Frontend Type / API service / Hook / Page / UI / allowedActions
Manual test cases
Build/test result
```

Không được báo hoàn thành nếu chỉ scaffold hoặc chưa build/test.

---

## 1. Tài liệu và file bắt buộc phải đọc trước khi code

Trước khi sửa, phải đọc các file sau trong repo hiện tại:

```text
docs/permissions/PERMISSION_RULES.md
docs/permissions/PERMISSION_MATRIX.md
docs/use-cases/USE_CASE_LIST.md
docs/use-cases/USE_CASE_NOTES.md
docs/architecture/CLEAN_ARCHITECTURE.md
docs/architecture/PROJECT_STRUCTURE_FULL.md
docs/PEMS_UC_IMPLEMENTATION_RULEBOOK_FRONTEND_BACKEND_DATABASE_VALIDATION_SECURITY.md
database/scripts/pems_full.sql
```

Phải quét các file code liên quan:

```text
Backend:
backend/PEMS.Api/Controllers/DelegationsController.cs
backend/PEMS.Api/Controllers/VisitRequestsController.cs
backend/PEMS.Application/Delegations/Queries/ViewGuestDelegationList/
backend/PEMS.Application/Delegations/Queries/ViewGuestDelegationDetails/
backend/PEMS.Application/Delegations/Queries/SearchDelegations/
backend/PEMS.Application/Delegations/Commands/ApproveCrossCampusRequest/
backend/PEMS.Application/Delegations/Commands/ProcessVisitRequest/
backend/PEMS.Application/Delegations/Commands/ConfirmParticipation/
backend/PEMS.Application/Delegations/Commands/PrepareVisitLogistics/
backend/PEMS.Application/Delegations/Commands/CloseDelegation/
backend/PEMS.Application/Delegations/Commands/UpdateVisitLogistics/
backend/PEMS.Application/Common/Interfaces/ICurrentUserService.cs
backend/PEMS.Application/Common/Interfaces/IPermissionChecker.cs
backend/PEMS.Domain/Entities/Delegations/VisitRequest.cs
backend/PEMS.Domain/Entities/Delegations/VisitRequestCampus.cs
backend/PEMS.Domain/Entities/Delegations/VisitParticipant.cs
backend/PEMS.Domain/Entities/Delegations/VisitStatusLog.cs
backend/PEMS.Domain/Enums/UserRoleCode.cs
backend/PEMS.Domain/Enums/SubRole.cs
backend/PEMS.Domain/Enums/VisitRequestStatus.cs
backend/PEMS.Domain/Enums/VisitInstanceStatus.cs
backend/PEMS.Domain/Constants/VisitRequestConstants.cs
backend/PEMS.Infrastructure/Persistence/ApplicationDbContext.cs
backend/PEMS.Infrastructure/Persistence/Repositories/DelegationRepository.cs

Frontend:
frontend/pems-react/src/pages/dashboard/visit/VisitRequestManagement.tsx
frontend/pems-react/src/pages/dashboard/visit/VisitRequestDetail.tsx
frontend/pems-react/src/pages/dashboard/visit/HoVisitProcessDetail.tsx
frontend/pems-react/src/pages/dashboard/visit/VisitProcess.tsx
frontend/pems-react/src/pages/dashboard/visit/VisitAfterTab.tsx
frontend/pems-react/src/pages/dashboard/visit/VisitDuringTab.tsx
frontend/pems-react/src/pages/dashboard/departments/TaskDetail.tsx
frontend/pems-react/src/pages/dashboard/departments/TaskInvitationDetail.tsx
frontend/pems-react/src/features/delegations/api/delegationsApi.ts
frontend/pems-react/src/features/delegations/hooks/useDelegations.ts
frontend/pems-react/src/features/delegations/types/delegations.types.ts
frontend/pems-react/src/features/delegations/adapters/delegationsAdapter.ts
frontend/pems-react/src/shared/auth/resolveEffectiveRole.ts
frontend/pems-react/src/shared/auth/permissionChecker.ts
frontend/pems-react/src/shared/constants/roles.ts
frontend/pems-react/src/shared/constants/permissions.ts
frontend/pems-react/src/shared/constants/ucCodes.ts
```

Nếu file hoặc bảng chưa tồn tại, không tự bịa. Phải quét schema/code hiện tại rồi tạo patch/command/query phù hợp.

---

## 2. Quy tắc nguồn chuẩn

Ưu tiên theo thứ tự:

```text
1. SQL/database schema mới nhất.
2. Seed role/permission/permission matrix mới nhất.
3. Yêu cầu nghiệp vụ đã chốt trong prompt này.
4. Backend entity/configuration/API hiện tại.
5. Frontend type/API/page hiện tại.
6. Tài liệu cũ chỉ dùng tham khảo nếu không mâu thuẫn.
```

Nếu yêu cầu trong prompt này mâu thuẫn với permission rule cũ, phải xử lý như sau:

```text
- Không âm thầm sửa toàn bộ permission matrix.
- Ghi rõ mismatch.
- Chỉ implement scope/action mới trong đúng màn Delegation/Visit Request nếu yêu cầu này đã được user chốt.
- Nếu cần thay đổi permission seed, tạo SQL patch riêng và ghi rõ.
```

Ví dụ yêu cầu mới chốt:

```text
HO được xem SINGLE_CAMPUS ở chế độ read-only để theo dõi.
HO không được duyệt/từ chối/gán host/hủy SINGLE_CAMPUS.
```

Nếu tài liệu cũ ghi HO không thấy SINGLE_CAMPUS, phải ghi rõ đây là thay đổi nghiệp vụ mới, không được tự cấp action xử lý cho HO.

---

## 3. Nguyên tắc gọi tên role

Không dùng các cụm từ dễ gây nhầm:

```text
IC Head
Staff / Host
Dept Leader role riêng
Staff Leader role riêng nếu SQL không có
```

Phải mô tả và code theo `role_code` + `sub_role`.

### 3.1. Role mapping bắt buộc

```text
ADMIN
= role_code = ADMIN
= Quản trị kỹ thuật hệ thống.
= Không tham gia nghiệp vụ xử lý đơn tiếp khách.

HO
= role_code = HO
= Xử lý nghiệp vụ liên cơ sở.
= Có thể xem read-only một số đơn để theo dõi nếu yêu cầu nghiệp vụ đã chốt.

Staff Leader
= role_code = STAFF
AND sub_role = Leader
= Người xử lý đơn thuộc campus của mình.

Staff
= role_code = STAFF
AND sub_role = Staff
= Nhân sự IC thông thường trong campus.

Staff được gán làm Host
= Không phải role đăng nhập riêng.
= Vẫn là role_code = STAFF, sub_role = Staff.
= Host chính phải xác định ưu tiên bằng visit_request_campuses.current_host_user_id = currentUser.id.

Department Lead
= role_code = DEPT
AND sub_role = Leader
= Trưởng bộ phận ngoài IC.

Department
= role_code = DEPT
AND sub_role = Staff
= Nhân sự phòng ban ngoài IC.

Student
= role_code = STUDENT
= Sinh viên hỗ trợ.

Visitor
= role_code = VISITOR
= Khách gửi yêu cầu thăm.
```

### 3.2. Lưu ý DEPT / DEPARTMENT

Nếu SQL hiện tại đã chuẩn hóa `role_code = DEPARTMENT` thay cho `DEPT`, phải dùng `DEPARTMENT` trong code, enum, DTO, frontend constants và query.

Không dùng lẫn lộn:

```text
DEPT
DEPARTMENT
```

Nếu schema hiện tại vẫn dùng `DEPT`, giữ `DEPT`.

---

## 4. Mục tiêu nghiệp vụ cần implement

Màn quản lý đơn tiếp khách cần có logic theo nhóm:

```text
Tab 1: Đơn phụ trách
Tab 2: Đơn mời tham dự
Visitor: Đơn của tôi
Department Staff: ưu tiên Tab Nhiệm vụ công việc nếu đang ở workspace phòng ban
Admin: không dùng màn nghiệp vụ tiếp khách
```

### 4.1. Tab 1 — Đơn phụ trách

Tab **Đơn phụ trách** hiển thị đơn mà user hiện tại có trách nhiệm xử lý, phụ trách, được gán host, được giao task, hoặc có scope theo role/campus.

Action có thể có:

```text
VIEW_DETAIL
HO_APPROVE
HO_REJECT
APPROVE_AND_ASSIGN_HOST
CAMPUS_REJECT
TRANSFER_HOST
PREPARE_VISIT
INVITE_IC_SUPPORT
INVITE_DEPT_SUPPORT
INVITE_STUDENT
CANCEL_BY_HOST
CANCEL_BY_VISITOR
```

Frontend ưu tiên render button theo `allowedActions[]` từ backend.

Backend vẫn phải validate lại role/permission/scope/status ở từng endpoint action.

### 4.2. Tab 2 — Đơn mời tham dự

Tab **Đơn mời tham dự** chỉ hiển thị đơn mà user đã được mời và đã chấp nhận tham gia.

Điều kiện chung:

```text
visit_participants.user_id = currentUser.id
AND visit_participants.status = ACCEPTED
AND visit_participants.is_host = false
AND visit_participants.participant_role IN (IC_SUPPORT, DEPT_SUPPORT, STUDENT)
AND visit_requests.status NOT IN (REJECTED, CANCELLED)
AND currentUser.id không phải host chính của visit instance
AND currentUser.id không phải người tạo đơn
AND currentUser không phải role_code = HO
AND currentUser không phải role_code = ADMIN
AND currentUser không phải role_code = VISITOR
AND currentUser không phải role_code = STAFF, sub_role = Leader
```

Action trong Tab 2 chỉ có:

```text
VIEW_DETAIL
```

Không có action:

```text
HO_APPROVE
HO_REJECT
APPROVE_AND_ASSIGN_HOST
CAMPUS_REJECT
TRANSFER_HOST
CANCEL_BY_HOST
CANCEL_BY_VISITOR
ACCEPT_INVITATION
DECLINE_INVITATION
```

`ACCEPT_INVITATION` và `DECLINE_INVITATION` thuộc màn chi tiết lời mời riêng, không thuộc Tab Đơn mời tham dự.

### 4.3. Visitor — Đơn của tôi

Visitor không dùng 2 tab trên.

Visitor chỉ thấy:

```text
Đơn của tôi
```

Điều kiện:

```text
role_code = VISITOR
AND visit_requests.visitor_user_id = currentUser.id
```

hoặc theo cột owner/created_by/registrant_user_id thực tế trong SQL.

Visitor được:

```text
VIEW_DETAIL
CANCEL_BY_VISITOR nếu status/time cho phép
```

Visitor không được:

```text
Duyệt
Từ chối
Gán host
Xem phân công nội bộ không cần thiết
Xem đơn của visitor khác
```

---

## 5. Chi tiết visibility theo từng role

## 5.1. ADMIN

Điều kiện:

```text
role_code = ADMIN
```

Backend list query:

```text
Không trả danh sách đơn nghiệp vụ tiếp khách cho ADMIN.
```

Frontend:

```text
Không hiển thị Tab Đơn phụ trách.
Không hiển thị Tab Đơn mời tham dự.
Redirect sang màn Admin phù hợp hoặc hiển thị empty state:
"Admin không tham gia luồng xử lý đơn tiếp khách."
```

ADMIN không có action nghiệp vụ visit/delegation.

---

## 5.2. HO

Điều kiện:

```text
role_code = HO
```

HO thấy ở Tab Đơn phụ trách:

```text
- Đơn MULTI_CAMPUS đang chờ HO duyệt.
- Đơn MULTI_CAMPUS đã được HO xử lý để xem lại/lịch sử.
- Đơn SINGLE_CAMPUS để theo dõi và xem chi tiết read-only theo yêu cầu mới đã chốt.
```

HO không xử lý đơn SINGLE_CAMPUS.

Action với MULTI_CAMPUS đang chờ HO duyệt:

```text
VIEW_DETAIL
HO_APPROVE
HO_REJECT
```

Action với MULTI_CAMPUS đã xử lý:

```text
VIEW_DETAIL
```

Action với SINGLE_CAMPUS:

```text
VIEW_DETAIL
```

Không có:

```text
APPROVE_AND_ASSIGN_HOST
CAMPUS_REJECT
TRANSFER_HOST
CANCEL_BY_HOST
CANCEL_BY_VISITOR
```

Backend phải enforce:

```text
Nếu request.visit_scope = SINGLE_CAMPUS:
- HO chỉ read-only.
- Gọi API approve/reject/assign-host/cancel phải trả 403 hoặc 409 tùy rule.
```

---

## 5.3. Staff Leader

Điều kiện:

```text
role_code = STAFF
AND sub_role = Leader
```

Staff Leader thấy ở Tab Đơn phụ trách:

```text
- Đơn SINGLE_CAMPUS thuộc campus của mình.
- Đơn MULTI_CAMPUS đã được HO duyệt và có campus của mình.
- Đơn MULTI_CAMPUS đã bị HO từ chối nhưng có campus của mình để theo dõi và xem lý do từ chối.
```

Staff Leader không thấy:

```text
- Đơn thuộc campus khác.
- Đơn MULTI_CAMPUS chưa được HO duyệt.
- Đơn MULTI_CAMPUS không chứa campus của mình.
- Đơn MULTI_CAMPUS đã bị HO từ chối nhưng không chứa campus của mình.
```

Action:

```text
VIEW_DETAIL
APPROVE_AND_ASSIGN_HOST với SINGLE_CAMPUS thuộc campus mình đang chờ duyệt
CAMPUS_REJECT với SINGLE_CAMPUS thuộc campus mình đang chờ duyệt
TRANSFER_HOST nếu đã có host chính thức, có permission và status cho phép
```

Quy tắc duyệt:

```text
Khi Staff Leader bấm Duyệt phải mở modal chọn Host.
Không được duyệt ngay nếu chưa chọn host.
Host được chọn phải:
- role_code = STAFF
- sub_role = Staff
- status = ACTIVE
- cùng campus với đơn/campus instance
- không phải Staff Leader nếu nghiệp vụ không cho Staff Leader làm host
```

Quy tắc multi-campus sau HO duyệt:

```text
Sau khi HO duyệt đơn MULTI_CAMPUS:
- Backend tự động gán Staff Leader của từng campus liên quan làm Host tạm thời.
- current_host_user_id có thể là Staff Leader tạm thời nếu SQL/logic hiện tại hỗ trợ.
- Staff Leader bắt buộc phân công một Staff thuộc campus mình làm Host chính thức trước khi tiếp tục các bước vận hành.
- Sau khi đã gán Host chính thức, Staff Leader có thể chuyển Host nếu có permission và trạng thái cho phép.
```

Staff Leader không có Tab Đơn mời tham dự.

---

## 5.4. Staff được gán làm Host

Điều kiện user:

```text
role_code = STAFF
AND sub_role = Staff
```

Điều kiện dữ liệu chính:

```text
visit_request_campuses.current_host_user_id = currentUser.id
```

Nguồn xác định Host chính:

```text
Ưu tiên visit_request_campuses.current_host_user_id.
Không xác định Host chính chỉ bằng visit_participants.
```

Host thấy đơn ở Tab Đơn phụ trách.

Host không thấy chính đơn đó ở Tab Đơn mời tham dự.

Action của Host:

```text
VIEW_DETAIL
PREPARE_VISIT
INVITE_IC_SUPPORT
INVITE_DEPT_SUPPORT
INVITE_STUDENT
CANCEL_BY_HOST nếu status/time cho phép
CLOSE_DELEGATION nên nằm trong màn Chuẩn bị tiếp khách / vận hành đoàn, không đặt nút đóng trực tiếp ngoài list
```

Host không được:

```text
Duyệt đơn
Từ chối đơn trong giai đoạn phê duyệt
Gán host như Staff Leader
Xem toàn bộ đơn campus nếu không được phân công
```

---

## 5.5. Staff thường

Điều kiện:

```text
role_code = STAFF
AND sub_role = Staff
```

### 5.5.1. Đơn phụ trách / nhiệm vụ trực tiếp

Staff thường thấy ở khu vực Đơn phụ trách nếu:

```text
- User được giao nhiệm vụ cụ thể liên quan đến đoàn.
- User có bản ghi visit_participants hợp lệ liên quan đến đúng visit instance.
- participant_role = IC_SUPPORT.
- status IN (INVITED, ACCEPTED, ASSIGNED) tùy flow hiện tại.
- Bản ghi chưa bị REMOVED.
```

Nếu user là host chính:

```text
visit_request_campuses.current_host_user_id = currentUser.id
```

thì hiển thị theo logic Host, không hiển thị lặp lại theo logic Staff thường.

### 5.5.2. Lời mời đang chờ phản hồi

Nếu participant:

```text
visit_participants.user_id = currentUser.id
AND participant_role = IC_SUPPORT
AND status = INVITED
AND is_host = false
AND participant_role != IC_HOST
AND chưa bị REMOVED
```

thì Staff có thể thực hiện:

```text
ACCEPT_INVITATION
DECLINE_INVITATION
```

Hai action này chỉ áp dụng cho lời mời của chính Staff đó.

Sau khi phản hồi:

```text
Accept:
- status = ACCEPTED
- responded_at = now

Decline:
- status = DECLINED
- responded_at = now
- rejection/decline reason nếu SQL có
```

Sau khi ACCEPTED, đơn xuất hiện trong Tab Đơn mời tham dự.

### 5.5.3. Tab Đơn mời tham dự

Điều kiện:

```text
participant_role = IC_SUPPORT
AND status = ACCEPTED
AND is_host = false
```

Action:

```text
VIEW_DETAIL
```

Không có action approve/reject/assign/cancel trong Tab 2.

---

## 5.6. Department Lead

Điều kiện:

```text
role_code = DEPT
AND sub_role = Leader
```

Nếu SQL dùng `DEPARTMENT`, thay `DEPT` bằng `DEPARTMENT`.

### 5.6.1. Tab Đơn phụ trách

Department Lead thấy đơn/yêu cầu trong Tab Đơn phụ trách khi:

```text
- Phòng ban được IC giao yêu cầu hỗ trợ.
- Department Lead được giao xử lý resource/task.
- Department Lead cần phân công nhân sự trong phòng ban.
- Department Lead xử lý yêu cầu phương tiện, phòng họp, thiết bị hoặc dịch vụ nội bộ.
```

Luồng task/resource:

```text
PENDING
→ Department Lead có thể Xác nhận ngay / Từ chối / Đề xuất thay đổi

WAITING_FOR_APPROVAL
→ IC xem xét đề xuất
→ Department Lead chỉ xem trạng thái và nội dung đề xuất đã gửi

CONFIRMED
→ Mở khối Đơn thỏa thuận và giao việc
→ Hai bên ký bàn giao
→ Hai bên ký nghiệm thu

COMPLETED
→ Hoàn thành yêu cầu khi đủ điều kiện ký
```

Không được:

```text
Duyệt đơn tiếp khách
Từ chối đơn tiếp khách
Gán host
Hủy đơn nghiệp vụ chính
```

Trừ khi permission matrix hiện tại có UC riêng cho department resource/task.

### 5.6.2. Tab Đơn mời tham dự

Điều kiện:

```text
participant_role = DEPT_SUPPORT
AND status = ACCEPTED
AND is_host = false
```

Nếu code hiện tại dùng `invitation_status = CONFIRMED`, cần adapter map:

```text
CONFIRMED <-> ACCEPTED
REJECTED <-> DECLINED
```

nhưng không được đổi enum DB nếu SQL không có.

Action trong Tab 2:

```text
VIEW_DETAIL
```

Không xử lý task/resource trong Tab 2.

---

## 5.7. Department Staff

Điều kiện:

```text
role_code = DEPT
AND sub_role = Staff
```

Nếu SQL dùng `DEPARTMENT`, thay `DEPT` bằng `DEPARTMENT`.

Đối với Department Staff, không nên hiển thị theo tư duy “Đơn phụ trách” như IC/Host.

UI nên ưu tiên:

```text
Tab: Nhiệm vụ công việc
```

Department Staff chỉ thấy nhiệm vụ giao trực tiếp cho mình.

Ví dụ:

```text
Chuẩn bị teabreak
Chuẩn bị phòng họp
Hỗ trợ chuyên môn
Điều phối nhân sự phòng ban
Lái xe điện đón khách
Chuẩn bị tài liệu
Hỗ trợ hậu cần
```

Action:

```text
VIEW_TASK
ACCEPT_TASK
DECLINE_TASK
PROPOSE_TASK_CHANGE
SIGN_HANDOVER
SIGN_ACCEPTANCE
COMPLETE_TASK nếu đủ chữ ký
```

Department Staff không được:

```text
Duyệt đơn tiếp khách
Từ chối đơn tiếp khách
Gán host
Hủy đơn tiếp khách
Xem đơn không liên quan đến nhiệm vụ được giao
```

Nếu Department Staff chỉ là participant và đã ACCEPTED lời mời:

```text
Hiển thị trong Tab Đơn mời tham dự theo participant_role = DEPT_SUPPORT.
```

---

## 5.8. Student

Điều kiện:

```text
role_code = STUDENT
```

Student thấy Đơn phụ trách nếu được giao nhiệm vụ cụ thể.

Nếu chỉ được mời tham gia và đã ACCEPTED:

```text
Tab Đơn mời tham dự
```

Điều kiện Tab 2:

```text
participant_role = STUDENT
AND status = ACCEPTED
AND is_host = false
```

Student không được:

```text
Duyệt đơn
Từ chối đơn
Gán host
Hủy đơn
Xem đơn không liên quan
```

---

## 5.9. Visitor

Điều kiện:

```text
role_code = VISITOR
```

Visitor chỉ thấy:

```text
Đơn của tôi
```

Action:

```text
VIEW_DETAIL
CANCEL_BY_VISITOR nếu status/time cho phép
```

Không hiển thị phân công nội bộ không cần thiết.

---

## 6. Participant roles

Bảng `visit_participants` chỉ được hiểu có 4 loại participant role:

```text
IC_HOST
IC_SUPPORT
DEPT_SUPPORT
STUDENT
```

Đây là `participant_role`, không phải `role_code`.

UI màn Thành phần tham gia hiển thị:

```text
1. Host
2. Staff hỗ trợ IC
3. Phòng ban hỗ trợ
4. Sinh viên hỗ trợ
```

Mapping:

```text
IC_HOST      -> Host
IC_SUPPORT   -> Staff hỗ trợ IC
DEPT_SUPPORT -> Phòng ban hỗ trợ
STUDENT      -> Sinh viên hỗ trợ
```

Không hiển thị nhóm cũ:

```text
Student Buddy
Buddy
Media
Interpreter
Other
```

---

# 7. Backend implementation details

## 7.1. Không viết logic trong Controller

Controller chỉ được:

```text
- Nhận route/query/body.
- Gọi IMediator.Send().
- Trả ApiResponse/ActionResult.
```

Không query DbContext trực tiếp trong Controller.

---

## 7.2. Query/Command cần kiểm tra hoặc bổ sung

Ưu tiên tận dụng query/command hiện có:

```text
ViewGuestDelegationListQuery
ViewGuestDelegationDetailsQuery
SearchDelegationsQuery
ProcessVisitRequestCommand
ApproveCrossCampusRequestCommand
ConfirmParticipationCommand
PrepareVisitLogisticsCommand
CloseDelegationCommand
UpdateVisitLogisticsCommand
```

Nếu chưa đủ, bổ sung có kiểm soát:

```text
backend/PEMS.Application/Delegations/Queries/ViewMyResponsibleDelegations/
backend/PEMS.Application/Delegations/Queries/ViewMyAcceptedInvitations/
backend/PEMS.Application/Delegations/Queries/ViewMyVisitRequests/
backend/PEMS.Application/Delegations/Queries/SearchAvailableHosts/
backend/PEMS.Application/Delegations/Commands/AssignVisitHost/
backend/PEMS.Application/Delegations/Commands/TransferVisitHost/
backend/PEMS.Application/Delegations/Commands/RespondVisitParticipantInvitation/
```

Nếu project đang dùng query chung `ViewGuestDelegationListQuery`, có thể mở rộng query đó bằng param:

```text
viewMode = RESPONSIBLE | INVITED | MY_REQUESTS | TASKS
```

Nhưng không làm breaking change frontend cũ nếu page khác đang dùng.

---

## 7.3. API route đề xuất

Trước khi tạo route mới, kiểm tra `ApiRoutes.cs`, controller hiện có và frontend đang gọi route nào.

Route gợi ý nếu chưa có:

```http
GET /api/delegations/my-responsible
GET /api/delegations/my-invitations/accepted
GET /api/delegations/my-visit-requests
GET /api/delegations/{visitRequestId}
GET /api/delegations/{visitRequestId}/campuses/{visitCampusId}
GET /api/delegations/{visitRequestId}/available-hosts?campusId=&keyword=
POST /api/delegations/{visitRequestId}/approve-and-assign-host
POST /api/delegations/{visitRequestId}/reject
POST /api/delegations/{visitRequestId}/campuses/{visitCampusId}/assign-host
POST /api/delegations/{visitRequestId}/campuses/{visitCampusId}/transfer-host
POST /api/delegations/participants/{participantId}/respond
GET /api/departments/tasks/my
```

Không đặt route nghiệp vụ xử lý đơn trong public submit form controller.

---

## 7.4. DTO response cho list item

Tạo hoặc cập nhật DTO list item, không trả entity trực tiếp.

Gợi ý:

```csharp
public sealed class DelegationListItemDto
{
    public long VisitRequestId { get; set; }
    public long? VisitCampusId { get; set; }

    public string RequestCode { get; set; } = default!;
    public string DelegationName { get; set; } = default!;
    public string OrganizationName { get; set; } = default!;

    public string VisitScope { get; set; } = default!; // SINGLE_CAMPUS | MULTI_CAMPUS
    public long? CampusId { get; set; }
    public string? CampusName { get; set; }

    public DateTime StartAt { get; set; }
    public DateTime EndAt { get; set; }

    public string RequestStatus { get; set; } = default!;
    public string? CampusStatus { get; set; }
    public string DisplayStatus { get; set; } = default!;

    public long? CurrentHostUserId { get; set; }
    public string? CurrentHostName { get; set; }

    public string CurrentUserRelation { get; set; } = default!;
    // NONE | HO_REVIEWER | CAMPUS_APPROVER | TEMP_HOST | HOST | IC_SUPPORT | DEPT_SUPPORT | STUDENT_SUPPORT | VISITOR_OWNER | DEPARTMENT_TASK_OWNER

    public string TabType { get; set; } = default!;
    // RESPONSIBLE | INVITED | MY_REQUESTS | TASKS

    public bool IsReadOnly { get; set; }
    public List<string> AllowedActions { get; set; } = new();
}
```

---

## 7.5. DTO available host

Khi Staff Leader chọn Host, backend cần trả danh sách Staff trong campus và warning trùng lịch.

Gợi ý DTO:

```csharp
public sealed class AvailableHostDto
{
    public long UserId { get; set; }
    public string FullName { get; set; } = default!;
    public string Email { get; set; } = default!;
    public long CampusId { get; set; }
    public string CampusName { get; set; } = default!;
    public string AccountStatus { get; set; } = default!;

    public bool HasScheduleConflict { get; set; }
    public List<HostScheduleConflictDto> Conflicts { get; set; } = new();
}

public sealed class HostScheduleConflictDto
{
    public long VisitRequestId { get; set; }
    public long VisitCampusId { get; set; }
    public string DelegationName { get; set; } = default!;
    public DateTime StartAt { get; set; }
    public DateTime EndAt { get; set; }
    public string Message { get; set; } = default!;
}
```

---

## 7.6. Host search rule

Endpoint search host phải:

```text
- Chỉ trả Staff thuộc cùng campus.
- Chỉ trả role_code = STAFF và sub_role = Staff.
- Chỉ trả user ACTIVE.
- Hỗ trợ keyword search theo fullName/email/phone nếu có.
- Không cần bấm search riêng ở frontend; frontend debounce keyword và gọi API hoặc filter local theo dữ liệu đã tải.
- Không loại Staff trùng lịch khỏi danh sách.
- Chỉ đánh dấu warning.
```

Nếu tải toàn bộ staff campus không quá lớn:

```text
Frontend có thể filter real-time local bằng state.
```

Nếu campus có nhiều staff:

```text
Frontend dùng debounce 250–400ms và gọi API keyword.
```

Không reload toàn bộ trang.

---

## 7.7. Logic kiểm tra trùng lịch Host

Overlap condition:

```sql
existing_start_at < current_end_at
AND existing_end_at > current_start_at
```

Chỉ xét các đơn/campus instance đang active:

```text
NOT IN (REJECTED, CANCELLED, CLOSED)
```

và các bản ghi host assignment hiện hành:

```text
visit_request_campuses.current_host_user_id = candidateUserId
```

Nếu schema dùng bảng participant/assignment riêng, phải theo schema thật.

Warning message gợi ý:

```text
Cảnh báo: Staff này đang phụ trách đoàn {DelegationName} từ {StartAt} đến {EndAt}, có khả năng trùng lịch với đoàn hiện tại.
```

---

## 7.8. Backend allowedActions builder

Tạo hoặc cập nhật service/policy:

```text
DelegationAllowedActionPolicy
DelegationScopePolicy
DelegationVisibilityQueryBuilder
```

Nếu project không có service policy, có thể đặt trong Application/Delegations/Rules.

Không hard-code logic rải rác trong nhiều handler.

Gợi ý:

```csharp
public interface IDelegationVisibilityPolicy
{
    IQueryable<VisitRequest> ApplyListScope(IQueryable<VisitRequest> query, CurrentUserContext user, DelegationViewMode mode);
    bool CanViewDetail(VisitRequest request, VisitRequestCampus? campus, CurrentUserContext user);
}

public interface IDelegationAllowedActionPolicy
{
    IReadOnlyList<string> GetAllowedActions(DelegationActionContext context);
}
```

---

## 7.9. Detail API phải enforce scope

Không chỉ filter ở list.

Khi user gọi trực tiếp:

```http
GET /api/delegations/{id}
```

Backend phải check:

```text
- Current user có quyền xem request/campus này không?
- Nếu không, trả 404 hoặc 403 theo convention hiện tại.
```

Khuyến nghị:

```text
403 nếu user biết ID nhưng vượt quyền rõ ràng.
404 nếu cần tránh lộ sự tồn tại dữ liệu.
```

---

## 7.10. Business validation cho action

### HO approve/reject

```text
role_code = HO
visit_scope = MULTI_CAMPUS
status = PENDING_APPROVAL
```

Reject bắt buộc lý do.

### Staff Leader approve and assign host

```text
role_code = STAFF
sub_role = Leader
campus = currentUser.primaryCampusId
visit_scope = SINGLE_CAMPUS hoặc campus instance sau HO release theo rule hiện tại
status/campus_status cho phép
hostUserId hợp lệ
```

### Assign/transfer host

```text
actor = Staff Leader của campus
target host = STAFF + Staff + ACTIVE + same campus
status cho phép
```

### Host prepare visit

```text
current_host_user_id = currentUser.id
status/campus_status cho phép
```

### Host cancel by external confirmation

```text
current_host_user_id = currentUser.id
cancellation_source = EXTERNAL_CONFIRMATION
cancellation_reason bắt buộc
phải chưa vào giai đoạn không cho hủy
```

### Participant respond invitation

```text
visit_participants.id = participantId
visit_participants.user_id = currentUser.id
status = INVITED
participant_role IN (IC_SUPPORT, DEPT_SUPPORT, STUDENT)
is_host = false
```

Accept:

```text
status = ACCEPTED
responded_at = now
```

Decline:

```text
status = DECLINED
responded_at = now
decline_reason bắt buộc nếu nghiệp vụ yêu cầu
```

---

# 8. Frontend implementation details

## 8.1. Effective role

Frontend phải resolve role rõ ràng:

```ts
export type EffectiveRole =
  | "ADMIN"
  | "HO"
  | "STAFF_LEADER"
  | "STAFF"
  | "DEPARTMENT_LEAD"
  | "DEPARTMENT"
  | "STUDENT"
  | "VISITOR";
```

Mapping:

```ts
roleCode = "ADMIN" -> "ADMIN"
roleCode = "HO" -> "HO"
roleCode = "STAFF" + subRole = "Leader" -> "STAFF_LEADER"
roleCode = "STAFF" + subRole = "Staff" -> "STAFF"
roleCode = "DEPT" + subRole = "Leader" -> "DEPARTMENT_LEAD"
roleCode = "DEPT" + subRole = "Staff" -> "DEPARTMENT"
roleCode = "DEPARTMENT" + subRole = "Leader" -> "DEPARTMENT_LEAD" nếu SQL đã đổi
roleCode = "DEPARTMENT" + subRole = "Staff" -> "DEPARTMENT"
roleCode = "STUDENT" -> "STUDENT"
roleCode = "VISITOR" -> "VISITOR"
```

Nếu `STAFF` hoặc `DEPT/DEPARTMENT` thiếu `subRole`:

```text
Không tự đoán.
Redirect invalid-account hoặc forbidden.
```

---

## 8.2. Frontend types cần bổ sung

Cập nhật:

```text
frontend/pems-react/src/features/delegations/types/delegations.types.ts
```

Gợi ý:

```ts
export type DelegationViewMode =
  | "RESPONSIBLE"
  | "INVITED"
  | "MY_REQUESTS"
  | "TASKS";

export type DelegationAllowedAction =
  | "VIEW_DETAIL"
  | "HO_APPROVE"
  | "HO_REJECT"
  | "APPROVE_AND_ASSIGN_HOST"
  | "CAMPUS_REJECT"
  | "TRANSFER_HOST"
  | "PREPARE_VISIT"
  | "INVITE_IC_SUPPORT"
  | "INVITE_DEPT_SUPPORT"
  | "INVITE_STUDENT"
  | "CANCEL_BY_HOST"
  | "CANCEL_BY_VISITOR"
  | "ACCEPT_INVITATION"
  | "DECLINE_INVITATION"
  | "CLOSE_DELEGATION";

export type ParticipantRole =
  | "IC_HOST"
  | "IC_SUPPORT"
  | "DEPT_SUPPORT"
  | "STUDENT";

export type ParticipantStatus =
  | "INVITED"
  | "ACCEPTED"
  | "DECLINED"
  | "ASSIGNED"
  | "REMOVED";

export type DelegationCurrentUserRelation =
  | "NONE"
  | "HO_REVIEWER"
  | "CAMPUS_APPROVER"
  | "TEMP_HOST"
  | "HOST"
  | "IC_SUPPORT"
  | "DEPT_SUPPORT"
  | "STUDENT_SUPPORT"
  | "VISITOR_OWNER"
  | "DEPARTMENT_TASK_OWNER";

export interface DelegationListItem {
  visitRequestId: number;
  visitCampusId?: number | null;
  requestCode: string;
  delegationName: string;
  organizationName: string;
  visitScope: "SINGLE_CAMPUS" | "MULTI_CAMPUS";
  campusId?: number | null;
  campusName?: string | null;
  startAt: string;
  endAt: string;
  requestStatus: string;
  campusStatus?: string | null;
  displayStatus: string;
  currentHostUserId?: number | null;
  currentHostName?: string | null;
  currentUserRelation: DelegationCurrentUserRelation;
  tabType: DelegationViewMode;
  isReadOnly: boolean;
  allowedActions: DelegationAllowedAction[];
}
```

---

## 8.3. API service

Cập nhật:

```text
frontend/pems-react/src/features/delegations/api/delegationsApi.ts
```

Gợi ý:

```ts
export const delegationsApi = {
  getMyResponsibleDelegations: (params) =>
    httpClient.get("/delegations/my-responsible", { params }),

  getMyAcceptedInvitations: (params) =>
    httpClient.get("/delegations/my-invitations/accepted", { params }),

  getMyVisitRequests: (params) =>
    httpClient.get("/delegations/my-visit-requests", { params }),

  getDetail: (visitRequestId, visitCampusId?) =>
    httpClient.get(`/delegations/${visitRequestId}`, { params: { visitCampusId } }),

  searchAvailableHosts: (visitRequestId, params) =>
    httpClient.get(`/delegations/${visitRequestId}/available-hosts`, { params }),

  approveAndAssignHost: (visitRequestId, body) =>
    httpClient.post(`/delegations/${visitRequestId}/approve-and-assign-host`, body),

  rejectVisitRequest: (visitRequestId, body) =>
    httpClient.post(`/delegations/${visitRequestId}/reject`, body),

  transferHost: (visitRequestId, visitCampusId, body) =>
    httpClient.post(`/delegations/${visitRequestId}/campuses/${visitCampusId}/transfer-host`, body),

  respondParticipantInvitation: (participantId, body) =>
    httpClient.post(`/delegations/participants/${participantId}/respond`, body),
};
```

Nếu project hiện tại dùng route khác, giữ route hiện tại và cập nhật constants.

---

## 8.4. Hook quản lý 2 tab

Cập nhật hoặc tạo:

```text
frontend/pems-react/src/features/delegations/hooks/useDelegationTabs.ts
```

Hook cần quản lý:

```text
activeTab
filters
pagination
loading
error
items
refetch
```

Logic:

```text
Nếu effectiveRole = ADMIN:
- Không gọi API delegation list nghiệp vụ.
- Hiển thị empty/redirect.

Nếu effectiveRole = VISITOR:
- Gọi myVisitRequests.
- Hiển thị "Đơn của tôi".

Nếu effectiveRole = HO:
- Chỉ hiển thị Tab Đơn phụ trách.
- Không hiển thị Tab Đơn mời tham dự.

Nếu effectiveRole = STAFF_LEADER:
- Chỉ hiển thị Tab Đơn phụ trách.
- Không hiển thị Tab Đơn mời tham dự.

Nếu effectiveRole = STAFF:
- Hiển thị Tab Đơn phụ trách nếu có host/task/pending invitation theo API.
- Hiển thị Tab Đơn mời tham dự nếu có accepted invitation.

Nếu effectiveRole = DEPARTMENT_LEAD:
- Hiển thị Tab Đơn phụ trách cho task/resource.
- Hiển thị Tab Đơn mời tham dự nếu accepted participant.

Nếu effectiveRole = DEPARTMENT:
- Ưu tiên Tab Nhiệm vụ công việc.
- Có thể hiển thị Tab Đơn mời tham dự nếu accepted participant.

Nếu effectiveRole = STUDENT:
- Hiển thị task/đơn phụ trách nếu được giao.
- Hiển thị Tab Đơn mời tham dự nếu accepted participant.
```

---

## 8.5. UI tab visibility

Không hiển thị tab mà role không được dùng.

```text
ADMIN:
- Không tab.

VISITOR:
- Đơn của tôi.

HO:
- Đơn phụ trách.

STAFF_LEADER:
- Đơn phụ trách.

STAFF:
- Đơn phụ trách.
- Đơn mời tham dự.

DEPARTMENT_LEAD:
- Đơn phụ trách.
- Đơn mời tham dự.

DEPARTMENT:
- Nhiệm vụ công việc.
- Đơn mời tham dự nếu cần.

STUDENT:
- Đơn phụ trách nếu có task.
- Đơn mời tham dự.
```

Empty state:

```text
Tab Đơn phụ trách:
"Bạn chưa có đơn phụ trách nào."

Tab Đơn mời tham dự:
"Bạn chưa có đơn mời tham dự nào đã được chấp nhận."

Đơn của tôi:
"Bạn chưa gửi đơn thăm nào."

Nhiệm vụ công việc:
"Bạn chưa có nhiệm vụ công việc nào."
```

Không coi empty là lỗi.

---

## 8.6. Render actions

Frontend không tự suy luận action bằng role nếu backend đã trả `allowedActions[]`.

Ví dụ:

```tsx
{item.allowedActions.includes("VIEW_DETAIL") && <ViewButton />}
{item.allowedActions.includes("HO_APPROVE") && <ApproveButton />}
{item.allowedActions.includes("HO_REJECT") && <RejectButton />}
{item.allowedActions.includes("APPROVE_AND_ASSIGN_HOST") && <ApproveAssignHostButton />}
{item.allowedActions.includes("TRANSFER_HOST") && <TransferHostButton />}
{item.allowedActions.includes("PREPARE_VISIT") && <PrepareVisitButton />}
{item.allowedActions.includes("CANCEL_BY_HOST") && <CancelButton />}
```

Nếu backend chưa trả `allowedActions`, tạm dùng adapter có comment TODO, nhưng không được bỏ backend validation.

---

## 8.7. Modal chọn Host

Khi Staff Leader bấm Duyệt:

```text
- Mở modal chọn Host.
- Gọi API danh sách Staff cùng campus.
- Có ô search.
- Search real-time.
- Không cần nút tìm kiếm riêng.
- Chọn Host xong mới enable nút Xác nhận duyệt.
- Nếu host có conflict, hiển thị warning nhưng vẫn cho chọn nếu nghiệp vụ không cấm.
```

UI warning:

```text
Cảnh báo: Staff này đang phụ trách đoàn ABC từ 09:00 - 11:00 ngày 15/08/2025, có khả năng trùng lịch với đoàn hiện tại.
```

Không dùng màu quá gắt; dùng warning badge/card nhẹ.

---

## 8.8. Trang lời mời riêng

Action `ACCEPT_INVITATION` / `DECLINE_INVITATION` phải nằm ở:

```text
TaskInvitationDetail
hoặc VisitParticipantInvitationDetail nếu có
```

Không đặt ở Tab Đơn mời tham dự.

Sau khi accept:

```text
- Gọi respondParticipantInvitation.
- Refetch Tab Đơn mời tham dự.
- Đơn xuất hiện ở Tab 2.
```

Sau khi decline:

```text
- Không xuất hiện ở Tab 2.
- Có thể hiển thị lịch sử nếu màn lời mời yêu cầu.
```

---

# 9. Database / schema checklist

Trước khi code, kiểm tra các bảng/cột thực tế:

```sql
visit_requests
visit_request_campuses
visit_participants
visit_status_logs
users
roles
role_permissions
permissions
coordination_tasks hoặc task table tương đương
departments
campuses
```

Cần xác định chính xác:

```text
visit_requests.id
visit_requests.status
visit_requests.visit_scope
visit_requests.visitor_user_id hoặc owner column
visit_requests.created_by
visit_requests.start_at/end_at hoặc planned_start/planned_end columns

visit_request_campuses.id
visit_request_campuses.visit_request_id
visit_request_campuses.campus_id
visit_request_campuses.status
visit_request_campuses.current_host_user_id hoặc host_user_id
visit_request_campuses.start_at/end_at nếu có

visit_participants.id
visit_participants.visit_request_id hoặc visit_request_campus_id
visit_participants.user_id
visit_participants.participant_role
visit_participants.status
visit_participants.is_host
visit_participants.responded_at
visit_participants.decline_reason nếu có
```

Nếu thiếu cột cần thiết:

```text
- Không tự đổi schema bằng EF migration.
- Tạo SQL patch idempotent trong database/scripts/.
- Ghi rõ patch cần chạy.
```

---

# 10. Permission / UC mapping

Dùng permission code thật trong seed. Không tự bịa nếu seed khác.

Mapping nghiệp vụ gợi ý:

```text
UC-18 Approve Cross-Campus Request
- HO_APPROVE
- HO_REJECT

UC-19 View Guest Delegation Details
- VIEW_DETAIL

UC-20 View Guest Delegation List
- List Tab Đơn phụ trách / Đơn mời tham dự / Đơn của tôi nếu dùng chung

UC-21 Search Delegations
- Search/filter

UC-22 Process Visit Request
- APPROVE_AND_ASSIGN_HOST
- CAMPUS_REJECT

UC-25 Prepare Visit Logistics
- PREPARE_VISIT

UC-27 Confirm Participation
- ACCEPT_INVITATION
- DECLINE_INVITATION

UC-28 Approve Resource Request
- Department Lead xử lý resource

UC-29 Propose Resource Modification
- Department Lead/Department đề xuất thay đổi

UC-30 Confirm The Change Proposal
- IC/Host xác nhận đề xuất

UC-41 Close Delegation
- CLOSE_DELEGATION

UC-110 Review Assigned Tasks
- Department/Student xem task

UC-111 Assign Tasks
- Department Lead/IC giao task

UC-136 Cancel Visit Request
- CANCEL_BY_HOST
- CANCEL_BY_VISITOR
```

Nếu permission seed đang dùng tên khác, phải map đúng seed hiện tại.

---

# 11. Security / RBAC / scope

Bắt buộc:

```text
- Frontend chỉ ẩn/hiện UI.
- Backend là lớp quyết định cuối cùng.
- List query phải filter scope server-side.
- Detail query phải check scope server-side.
- Action endpoint phải check permission + scope + status.
- Không tin campusId/userId từ frontend.
- Không trả dữ liệu nội bộ không cần thiết cho Visitor.
- Không cho Admin tự động có quyền nghiệp vụ visit/delegation.
```

Lỗi cần trả:

```json
{
  "success": false,
  "errorCode": "DELEGATION_SCOPE_FORBIDDEN",
  "message": "Bạn không có quyền truy cập đơn tiếp khách này."
}
```

Các errorCode gợi ý:

```text
DELEGATION_SCOPE_FORBIDDEN
VISIT_REQUEST_NOT_FOUND
HOST_ASSIGNMENT_INVALID_ROLE
HOST_ASSIGNMENT_CAMPUS_MISMATCH
HOST_ASSIGNMENT_USER_INACTIVE
VISIT_REQUEST_STATUS_NOT_ALLOWED
PARTICIPANT_INVITATION_NOT_FOUND
PARTICIPANT_INVITATION_ALREADY_RESPONDED
PARTICIPANT_INVITATION_FORBIDDEN
HO_CANNOT_PROCESS_SINGLE_CAMPUS
STAFF_LEADER_CANNOT_PROCESS_OTHER_CAMPUS
VISITOR_CAN_ONLY_VIEW_OWN_REQUEST
```

---

# 12. UI design constraints

Giữ style enterprise dashboard:

```text
Primary blue: #004c91
Primary orange: #F37021
Text chính: slate-800/slate-900
Text phụ: slate-500/slate-600
Border: slate-200/slate-300
Card: rounded-2xl border border-slate-200 bg-white shadow-sm
```

Không được:

```text
- Làm UI tràn ngang.
- Cắt chữ cột hành động.
- Button xuống dòng.
- Dùng màu quá nhiều.
- Dùng gradient mạnh.
- Rewrite toàn bộ page nếu chỉ cần sửa logic/tabs.
```

Table/list:

```text
- Desktop dùng table/grid gọn.
- Mobile dùng card list.
- Action render theo allowedActions.
- Badge trạng thái nhỏ gọn, dễ đọc.
```

---

# 13. Test cases bắt buộc

## 13.1. Backend list visibility

```text
ADMIN:
- GET list trả empty hoặc 403 theo route policy.
- Không có allowedActions nghiệp vụ.

HO:
- Thấy MULTI_CAMPUS pending.
- Thấy MULTI_CAMPUS đã xử lý.
- Thấy SINGLE_CAMPUS read-only nếu yêu cầu mới đã chốt.
- SINGLE_CAMPUS không có approve/reject/assign/cancel action.

STAFF + Leader campus A:
- Thấy SINGLE_CAMPUS campus A.
- Không thấy SINGLE_CAMPUS campus B.
- Không thấy MULTI_CAMPUS chưa HO duyệt.
- Thấy MULTI_CAMPUS đã HO duyệt có campus A.
- Thấy MULTI_CAMPUS bị HO từ chối có campus A để xem lý do.
- Không thấy MULTI_CAMPUS không chứa campus A.

STAFF + Staff là current_host_user_id:
- Thấy đơn ở Tab Đơn phụ trách.
- Không thấy duplicate ở Tab Đơn mời tham dự.

STAFF + Staff participant IC_SUPPORT ACCEPTED:
- Thấy ở Tab Đơn mời tham dự.
- allowedActions chỉ VIEW_DETAIL.

STAFF + Staff participant IC_SUPPORT INVITED:
- Không thấy ở Tab Đơn mời tham dự.
- Có thể thấy ở màn lời mời cần phản hồi.
- Accept cập nhật status ACCEPTED.
- Decline cập nhật status DECLINED.

DEPT + Leader:
- Thấy task/resource department được giao.
- Thấy participant DEPT_SUPPORT ACCEPTED ở Tab Đơn mời tham dự.
- Không duyệt/từ chối/gán host.

DEPT + Staff:
- Thấy nhiệm vụ được giao trực tiếp.
- Participant DEPT_SUPPORT ACCEPTED thì thấy Tab Đơn mời tham dự.

STUDENT:
- Thấy task được giao.
- Participant STUDENT ACCEPTED thì thấy Tab Đơn mời tham dự.
- Không có action approve/reject/assign/cancel.

VISITOR:
- Chỉ thấy đơn của mình.
- Không thấy đơn visitor khác.
```

---

## 13.2. Backend action validation

```text
HO approve SINGLE_CAMPUS -> 403/409.
HO approve MULTI_CAMPUS pending -> success.
HO reject MULTI_CAMPUS without reason -> 400.

Staff Leader approve campus khác -> 403.
Staff Leader approve own campus without host -> 400.
Staff Leader assign host khác campus -> 403/400.
Staff Leader assign inactive staff -> 400/409.

Host prepare own campus instance -> success.
Host prepare not-own instance -> 403.
Host cancel with empty reason -> 400.
Host cancel after closed -> 409.

Participant accept own INVITED -> success.
Participant accept someone else's invitation -> 403.
Participant accept already ACCEPTED -> 409.
Participant decline must save responded_at.
```

---

## 13.3. Frontend UI tests/manual tests

```text
Login từng role:
- admin@fpt.edu.vn
- ho@fpt.edu.vn
- staff.leader.hn@fpt.edu.vn
- staff.hn@fpt.edu.vn
- dept.leader.hn@fpt.edu.vn
- dept.hn@fpt.edu.vn
- student@fpt.edu.vn
- visitor@example.com

Kiểm tra:
- Tab hiển thị đúng role.
- Empty state đúng.
- Không trắng màn hình.
- Không hiện button sai quyền.
- Modal chọn host bắt buộc trước khi duyệt.
- Search host real-time.
- Host conflict warning hiển thị nhưng vẫn chọn được.
- Tab Đơn mời tham dự không có accept/decline.
- Màn lời mời riêng có accept/decline.
- Visitor không thấy phân công nội bộ.
- Mobile không tràn ngang.
```

---

# 14. Build/test commands

Backend:

```bash
dotnet restore
dotnet build
dotnet test
```

Frontend:

```bash
cd frontend/pems-react
npm install
npm run build
npm run lint
npm run typecheck
```

Nếu không có script `lint` hoặc `typecheck`, ghi rõ:

```text
Script npm run lint không tồn tại.
Script npm run typecheck không tồn tại.
```

Không báo pass giả.

---

# 15. Output bắt buộc sau khi sửa

Sau khi code xong, báo cáo theo format:

```text
1. Summary
2. Files changed
3. Backend changes
4. Frontend changes
5. Database changes / SQL patch nếu có
6. API contract
7. Permission/scope rules
8. Validation rules
9. Manual test cases
10. Build/test result
11. Known limitations
12. TODO / cần xác nhận
```

Nếu có mismatch phải báo rõ:

```text
- Mismatch ở đâu.
- Tài liệu/schema/code nào đang khác.
- Đã xử lý tạm thế nào.
- Có cần cập nhật permission matrix / SQL seed không.
```

---

# 16. Definition of Done

Task chỉ được coi là xong khi:

```text
[ ] Không còn thuật ngữ IC Head trong code/UI liên quan.
[ ] Không gọi Staff / Host như role đăng nhập.
[ ] Effective role dùng role_code + sub_role.
[ ] Host chính xác định ưu tiên từ visit_request_campuses.current_host_user_id.
[ ] Backend list query filter theo role/scope/tab.
[ ] Backend detail query check scope.
[ ] Backend action endpoint check permission/scope/status.
[ ] Backend trả allowedActions[] đúng.
[ ] Tab Đơn phụ trách hiển thị đúng role.
[ ] Tab Đơn mời tham dự chỉ hiển thị ACCEPTED participant.
[ ] Tab Đơn mời tham dự chỉ có VIEW_DETAIL.
[ ] Accept/Decline lời mời nằm ở màn lời mời riêng.
[ ] Modal chọn Host có search real-time.
[ ] Host conflict warning hiển thị đúng.
[ ] Department Staff ưu tiên Nhiệm vụ công việc.
[ ] Visitor chỉ thấy Đơn của tôi.
[ ] Admin không vào nghiệp vụ tiếp khách.
[ ] Frontend không trắng màn hình khi API lỗi/403/empty.
[ ] Build backend pass hoặc ghi rõ lỗi thật.
[ ] Build frontend pass hoặc ghi rõ lỗi thật.
[ ] Có test case/manual test rõ ràng.
```

---

# 17. Ghi chú chốt cuối cùng

Logic chốt cần giữ:

```text
1. Không dùng thuật ngữ IC Head.
2. Không gọi Staff / Host như một role.
3. Host là phân công trên visit instance, không phải role đăng nhập.
4. Staff Leader = role_code STAFF + sub_role Leader.
5. Staff thường = role_code STAFF + sub_role Staff.
6. Department Lead = role_code DEPT + sub_role Leader hoặc DEPARTMENT + Leader theo SQL hiện tại.
7. Department = role_code DEPT + sub_role Staff hoặc DEPARTMENT + Staff theo SQL hiện tại.
8. Tab Đơn phụ trách dùng cho người xử lý/chủ trì/được giao việc.
9. Tab Đơn mời tham dự chỉ dùng cho participant đã ACCEPTED.
10. HO, Admin, Visitor, Staff Leader không có Tab Đơn mời tham dự.
11. Staff được gán làm Host nằm ở Tab Đơn phụ trách, không nằm ở Tab Đơn mời tham dự.
12. Staff thường, Department Lead, Department, Student chỉ nằm ở Tab Đơn mời tham dự khi có participant hợp lệ và status = ACCEPTED.
13. Backend phải filter đúng dữ liệu trước khi trả về.
14. Frontend render action theo allowedActions nếu backend có trả.
```
