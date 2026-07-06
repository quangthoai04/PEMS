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

<!-- =====================================================================
PEMS DOC UPDATE v8.2-full-preserved-cancel-delegation-no-external-note
Generated: 2026-06-19
Mode: PRESERVE ORIGINAL CONTENT + APPEND ADDENDUM.
No original section below has been removed or compressed.
The addendum section at the end is the authoritative update for cancellation UC-136.
===================================================================== -->

# Permission Rules — PEMS v5

> **Purpose:** File này là bản rule ngắn gọn để backend/frontend triển khai authorization nhất quán theo SQL v5.  
> **Source of truth for detailed UC matrix:** `PERMISSION_MATRIX_UPDATED_V5.md`.  
> File này không lặp lại toàn bộ ma trận UC; nó chỉ ghi các nguyên tắc bắt buộc, scope rule và các case dễ nhầm.

---

> ## ⚠️ CẢNH BÁO CẬP NHẬT — 2026-07-02
>
> 1. **File `PERMISSION_MATRIX_UPDATED_V5.md` nêu ở trên không còn tồn tại trong repo.** Dùng `docs/permissions/PERMISSION_MATRIX.md` (đã cập nhật cùng ngày) thay thế; và dùng `docs/PEMS_CANONICAL_BUSINESS_RULES_v8_4_refined_v6_v10_FULL_UPDATED.md` làm nguồn chuẩn nghiệp vụ cao nhất.
> 2. **Toàn bộ mô hình "Permission Level" (`F/E/R/O`) gắn với `permission_code`/DB `role_permissions` ở mục 1–2 dưới đây mô tả một kiến trúc đã bị thay thế.** Code hiện tại (đã xác nhận: schema không có bảng `permissions`/`role_permissions`) dùng **fixed policy** — kiểm tra trực tiếp `role_code`/`sub_role`/scope trong Handler, không tra permission row trong DB. Giữ lại bảng Effective Role Resolution (mục 2) vì vẫn khớp code; bỏ qua phần ngụ ý có bảng `role_permissions` sống động.
> 3. **§4 Strict Visit / Delegation Visibility — xác nhận đúng với code hiện tại**, kể cả chi tiết "HO View (read-only, monitor)" cho `SINGLE_CAMPUS` (evidence: `ViewGuestDelegationListQueryHandler.cs:455-456`, comment *"HO sees every MULTI_CAMPUS request ... AND every SINGLE_CAMPUS request in read-only monitoring mode"*). Đây là rule chi tiết hơn `CANONICAL_BUSINESS_RULES...md` §10 hiện có — nên dùng file này làm tham chiếu bổ sung cho riêng điểm này.
> 4. **§5 Visit Status vs Display Status** liệt kê lifecycle `PENDING_APPROVAL → APPROVED → IN_PROGRESS → COMPLETED` — đây là **lifecycle SQL v5 cũ**, đã bị thay bằng model 2 tầng hiện tại: `visit_requests.status` (`PENDING_APPROVAL/APPROVED/REJECTED/CANCELLED`) tách biệt với `visit_request_campuses.status` (`WAITING_REQUEST_APPROVAL → ASSIGNED → ASSIGNED → BEFORE_VISIT → DURING_VISIT → AFTER_VISIT → CLOSED/CANCELLED`). Xem `CANONICAL_BUSINESS_RULES...md` §6.
> 5. **UC-136 cancellation section (cuối file)**: bổ sung nhánh Visitor được hủy request ngay cả khi còn `PENDING_APPROVAL` — xem `CANONICAL_BUSINESS_RULES...md` mục "V11.3" để biết chi tiết + evidence.
> 6. **Role/SubRole Canonical Rules (cuối file, mục "Lưu trữ DB")**: dòng nói `role_permissions.sub_role ENUM('NONE','Leader','Staff')` mô tả bảng không còn tồn tại — chỉ `users.sub_role ENUM('LEADER','STAFF')` là còn áp dụng thật.

---

---

## 1. Permission Level Meaning

| Symbol | Meaning | Backend Rule |
|---|---|---|
| `F` | Full Permission | Được thực hiện hành động chính của UC trong scope được giao. Không có nghĩa là toàn quyền toàn hệ thống. |
| `E` | Execute / Edit | Được xử lý, cập nhật, phê duyệt, đổi trạng thái trong scope hợp lệ. |
| `R` | Read | Chỉ xem/tìm kiếm/lọc; không thay đổi dữ liệu. |
| `O` | Own / Object Scope | Chỉ thao tác dữ liệu của chính user hoặc object mà user là owner/participant hợp lệ. |
| `—` | No Access | Không có quyền; frontend ẩn chức năng và backend trả 403 nếu gọi trực tiếp. |

---

## 2. Effective Role Resolution

| `role_code` | `sub_role` | Effective Role |
|---|---|---|
| `ADMIN` | `NULL` / `NONE` | Admin |
| `HO` | `NULL` / `NONE` | HO |
| `STAFF` | `Leader` | Staff Leader |
| `STAFF` | `Staff` | Staff |
| `DEPARTMENT` | `Leader` | Department Lead |
| `DEPARTMENT` | `Staff` | Department |
| `STUDENT` | `NULL` / `NONE` | Student |
| `VISITOR` | `NULL` / `NONE` | VISITOR |

Invalid combinations must not receive implicit permission. For example, `STAFF` without `sub_role` is invalid for authorization.

---

## 3. Authentication / Portal Rules

### 3.1. Visitor Portal

- Only `VISITOR` accounts can log in through the Visitor portal.
- `selected_campus_id` must be `NULL`.
- If a Google SSO / FEID login has no existing user and Visitor auto-provisioning is allowed, backend creates a VISITOR account with:
  - `created_via = 'SSO_AUTO_PROVISION'`
  - `primary_campus_id = NULL`
  - `department_id = NULL`
  - `sub_role = NULL`
  - `status = 'ACTIVE'`
- Visitor portal must not create internal accounts.

### 3.2. Internal Portal

- Internal portal is for non-VISITOR users only.
- Internal user must have exactly one `primary_campus_id` when the role requires campus.
- `selected_campus_id` must match `users.primary_campus_id`.
- Internal portal must not auto-provision unknown SSO users.
- If account exists but role/portal mismatch, return a clear 403/validation error.

### 3.3. Pre-auth Endpoints

The following UCs are pre-auth and must not use business `RequirePermission` before authentication:

- UC-10 Login via SSO.
- UC-11 Login via Credentials.
- UC-13 Forgot Password.

They still require security checks: account status, portal validation, rate limit, lockout, OTP/token expiry, audit/security log.

---

## 4. Strict Visit / Delegation Visibility

This is the most important SQL v5 rule.

| Role | Single-campus request | Multi-campus before HO approval | Multi-campus after HO approval/release |
|---|---:|---:|---:|
| ADMIN | No access | No access | No access |
| HO | View (read-only, monitor) ¹ | View + approve/reject | View |
| Staff Leader, same campus | View + process | No access | View own campus instance |
| Staff Leader, other campus | No access | No access | No access |
| Staff | Only assigned/linked records | No access unless linked after release | Assigned/linked records only |
| Department Lead/Department | Department task/resource scope only | No access unless task/resource is released | Assigned resource/task scope only |
| Student | Assigned/participant scope only | No access unless assigned after release | Assigned/participant scope only |
| VISITOR | Own submitted/linked request only | Own submitted/linked request only | Own submitted/linked request only |

> ¹ **Business rule update (chốt 2026-06):** HO may now **view** `SINGLE_CAMPUS`
> requests in **read-only monitoring** mode. HO still has **no processing rights** on
> `SINGLE_CAMPUS` — no approve / reject / assign-host / transfer-host / cancel. This
> supersedes the earlier "HO No access to SINGLE_CAMPUS" rule for the list/detail screen
> only; the processing restriction is unchanged.

### 4.1. HO Query Rule

HO list/detail now returns **all** requests, but processing is restricted by scope:

```sql
-- Visibility: HO sees MULTI_CAMPUS (decide) + SINGLE_CAMPUS (monitor, read-only).
-- (No visit_scope filter on the list query for HO.)

-- Processing (approve/reject/assign/cancel) is allowed ONLY for:
WHERE visit_scope = 'MULTI_CAMPUS'
```

HO read-only on `SINGLE_CAMPUS` is enforced by the backend AllowedActions builder, which
grants HO only `VIEW_DETAIL` for `SINGLE_CAMPUS` rows (no mutating action). Action
endpoints (approve/reject/assign/cancel) still reject HO on `SINGLE_CAMPUS`.

### 4.2. Staff Leader Query Rule

Staff Leader list/detail must use `vw_visit_requests_for_staff_leader` plus current campus filter:

```sql
WHERE visible_campus_id = @CurrentUserPrimaryCampusId
```

Staff Leader can process only:

```sql
visit_scope = 'SINGLE_CAMPUS'
AND request_status = 'PENDING_APPROVAL'
AND visible_campus_id = @CurrentUserPrimaryCampusId
```

Staff Leader cannot process `MULTI_CAMPUS`.

### 4.3. Admin Query Rule

ADMIN has no visit/delegation business access. Backend should return 403 or use `vw_visit_requests_for_admin`, which intentionally returns zero rows.

---

## 5. Visit Status vs Display Status

`visit_requests.status` is lifecycle status only:

```text
PENDING_APPROVAL → APPROVED → IN_PROGRESS → COMPLETED
PENDING_APPROVAL → REJECTED
PENDING_APPROVAL / APPROVED → CANCELLED
```

Do not add `HO_APPROVED` into `visit_requests.status`.

Use derived display labels instead:

| Display Label | How to derive |
|---|---|
| `WAITING_HO_APPROVAL` | `visit_scope = MULTI_CAMPUS` and `status = PENDING_APPROVAL` |
| `HO_APPROVED` | `visit_scope = MULTI_CAMPUS`, `status IN (APPROVED, IN_PROGRESS, COMPLETED)`, `decision_actor_role = HO` |
| `WAITING_STAFF_LEADER_APPROVAL` | `visit_scope = SINGLE_CAMPUS` and `status = PENDING_APPROVAL` |
| `STAFF_LEADER_APPROVED` | `visit_scope = SINGLE_CAMPUS`, approved lifecycle status, `decision_actor_role = STAFF_LEADER` |

---

## 6. Email Scope Rule

UC-48 `View Email` is `O` own-scope, not broad read.

A user may view/reply only if one of these is true:

- user is sender;
- user is recipient / cc / bcc participant;
- user is an explicitly stored participant in the email conversation;
- email is linked to a visit/delegation that user can access under the same strict visit visibility rule.

No role may read the whole email history simply because it has UC-48.

---

## 7. Public Content Rule

Public endpoints do not need business `RequirePermission`, but must filter:

- published/visible only;
- active only;
- not soft-deleted;
- no internal/private data;
- no draft/pending/rejected/hidden content.

Examples: homepage, FAQ, public news, public gallery, public contact information.

---

## 8. Backend Enforcement Order

For every protected endpoint:

1. Authenticate user/session.
2. Resolve effective role.
3. Check permission code and level.
4. Apply object ownership if level is `O`.
5. Apply data scope: campus, department, participant, owner, linked visit/delegation.
6. Apply business status rule.
7. Apply action-specific rule: approve/reject/assign/publish/close.
8. Write audit log for mutations.

Frontend visibility is only UX. Backend authorization is mandatory.

---

## 9. Change Log

| Version | Description |
|---|---|
| v5 | Rewritten as concise implementation rulebook; removed duplicate full matrix content; added `SSO_AUTO_PROVISION`; added strict visit visibility; clarified Admin no visit access, HO multi-campus only, Staff Leader campus scope, UC-48 own-scope. |
| v11 (2026-07-02) | Rà soát code thật: xác nhận mô hình permission_code/role_permissions đã lỗi thời (không có bảng trong DB); xác nhận §4 HO-read-only-SINGLE_CAMPUS đúng với code; đánh dấu §5 lifecycle status là legacy SQL v5 (đã thay bằng model 2 tầng); bổ sung nhánh Visitor-cancel-khi-PENDING vào UC-136. Xem cảnh báo đầu file. |

---

# Addendum — Authorization Rules cho UC-136 Cancel Visit Request


## V8.2 Addendum — UC-136 Cancel Visit Request thuộc Delegation Reception Management

> Phần này là nội dung bổ sung, không xóa nội dung gốc. Nếu nội dung gốc có flow cũ như “đã duyệt nhưng chưa có host” hoặc “mỗi cơ sở duyệt lại sau HO”, hãy ưu tiên rule V8.2 trong phần addendum này.

### 1. Feature ownership

UC hủy đơn thăm thuộc **FE-02 — Quản lý Tiếp đón Đoàn khách / Delegation Reception Management** vì đây là thao tác trên vòng đời đoàn/visit request, không phải bước submit form.

```text
Feature: FE-02 Delegation Reception Management
UC: UC-136 Cancel Visit Request
Permission code: UC-136.CANCEL_VISIT_REQUEST
```

### 2. Không dùng `external_confirmation_note`

Không tạo cột `external_confirmation_note`. Khi Host hủy thay khách dựa trên xác nhận ngoài hệ thống, toàn bộ thông tin xác nhận được ghi vào `cancellation_reason`.

```text
cancellation_source = EXTERNAL_CONFIRMATION
cancellation_reason = "Khách xác nhận hủy qua email/điện thoại/Zalo..., thời gian..., người xác nhận..., lý do..."
```

### 3. Cancellation metadata chuẩn

Áp dụng cho `visit_requests` và `visit_request_campuses`:

```sql
cancelled_by BIGINT UNSIGNED NULL,
cancelled_at DATETIME NULL,
cancellation_actor_type ENUM('VISITOR','HOST','STAFF_LEADER','HO','SYSTEM') NULL,
cancellation_source ENUM('SELF_SERVICE','EXTERNAL_CONFIRMATION','INTERNAL_DECISION') NULL,
cancellation_reason TEXT NULL
```

### 4. Meaning của `cancellation_source`

| Value | Meaning | Khi dùng |
|---|---|---|
| `SELF_SERVICE` | Người dùng tự thao tác trên hệ thống | Visitor tự hủy đơn của chính họ |
| `EXTERNAL_CONFIRMATION` | Hủy dựa trên xác nhận ngoài hệ thống | Host hủy thay khách sau khi khách xác nhận qua email/điện thoại/Zalo/gặp trực tiếp |
| `INTERNAL_DECISION` | Nội bộ hủy vì lý do vận hành | HO/Staff Leader hủy vì campus không thể tiếp, trùng lịch, lý do tổ chức |

### 5. Rule hủy theo role

| Actor | Scope | Nguồn hủy hợp lệ | Ghi chú |
|---|---|---|---|
| Visitor | Đơn của chính họ | `SELF_SERVICE` | Chỉ hủy khi chưa vào giai đoạn `DURING_VISIT`, `AFTER_VISIT`, `CLOSED` |
| Host | Campus instance mình đang phụ trách | `EXTERNAL_CONFIRMATION` | Bắt buộc nhập `cancellation_reason` rõ kênh/thời điểm/người xác nhận |
| Staff Leader | Đơn/campus thuộc campus mình | `INTERNAL_DECISION` hoặc `EXTERNAL_CONFIRMATION` | Không xử lý campus khác |
| HO | `MULTI_CAMPUS` | `INTERNAL_DECISION` hoặc `EXTERNAL_CONFIRMATION` | Có thể hủy request tổng liên cơ sở nếu nghiệp vụ cho phép |
| Admin | Không có quyền nghiệp vụ visit/delegation | Không áp dụng | ADMIN không được hủy delegation |

### 6. Rule trạng thái

- `visit_requests.status = CANCELLED` dùng khi hủy request/delegation tổng.
- `visit_request_campuses.status = CANCELLED` dùng khi hủy một campus instance.
- Không cho hủy campus instance nếu đã vào `DURING_VISIT`, `AFTER_VISIT`, hoặc `CLOSED`.
- Không dùng `CANCELLED` thay cho `REJECTED`. Nếu đơn đang `PENDING_APPROVAL` và người duyệt không chấp nhận, dùng reject flow.

### 7. Vị trí code Clean Architecture

```text
PEMS.Application/Delegations/Commands/CancelVisitRequest/
├── CancelVisitRequestCommand.cs
├── CancelVisitRequestCommandHandler.cs
├── CancelVisitRequestCommandValidator.cs
└── CancelVisitRequestResponse.cs
```

Controller chỉ nhận request và gọi `IMediator`. Logic kiểm tra scope, current host, request/campus status, và cancellation metadata nằm trong Handler/Domain Entity.


## Backend authorization checklist

```text
1. Check authenticated user, trừ trường hợp public token-based visitor cancel được thiết kế riêng.
2. Check permission UC-136.CANCEL_VISIT_REQUEST.
3. Check role scope.
4. Check ownership/assignment/current host.
5. Check request/campus status.
6. Require cancellation_reason for EXTERNAL_CONFIRMATION and INTERNAL_DECISION.
7. Write audit log and visit_status_logs.
```

## Không dùng `external_confirmation_note`

Mọi bằng chứng xác nhận ngoài hệ thống được ghi trong `cancellation_reason`, không tạo field riêng.

## Role/SubRole Canonical Rules

PEMS không dùng role riêng cho Staff Leader hoặc Department Leader. Hệ thống dùng role chính kết hợp với subRole.

| Nhóm người dùng | role_code | sub_role | Ghi chú |
|---|---|---|---|
| Admin | `ADMIN` | NULL / NONE | Quản trị hệ thống |
| Head Office | `HO` | NULL / NONE | Cấp Head Office |
| Staff | `STAFF` | `STAFF` | Nhân sự IC thường |
| Staff Leader | `STAFF` | `LEADER` | Trưởng IC / người duyệt campus |
| Department Staff | `DEPARTMENT` | `STAFF` | Nhân sự phòng ban |
| Department Leader | `DEPARTMENT` | `LEADER` | Trưởng phòng ban |
| Student | `STUDENT` | NULL / NONE | Sinh viên hỗ trợ |
| Visitor | `VISITOR` | NULL / NONE | Khách ngoài |

### Quy tắc

- Không dùng role `DEPT` (đã đổi sang `DEPARTMENT`).
- Không dùng role `STAFF_LEADER`, `DEPT_LEADER`, `DEPARTMENT_LEADER` — Leader luôn là `sub_role = LEADER`.
- Staff Leader luôn là `role_code = STAFF` + `sub_role = LEADER`; Staff thường là `STAFF` + `STAFF`.
- Department Leader luôn là `role_code = DEPARTMENT` + `sub_role = LEADER`; Department Staff là `DEPARTMENT` + `STAFF`.
- ADMIN, HO, STUDENT, VISITOR không dùng `sub_role` trong bảng `users` (NULL). Trong `role_permissions` dùng `NONE`.
- Lưu trữ DB: `users.sub_role ENUM('LEADER','STAFF')` (uppercase); `role_permissions.sub_role ENUM('NONE','Leader','Staff')`. So khớp dùng collation `utf8mb4_unicode_ci` (case-insensitive) nên `LEADER` khớp `Leader`. Code phải normalize uppercase khi so sánh để tránh lỗi casing.
- `DEPT_SUPPORT` là role tham dự đoàn (visit participant), KHÔNG phải role_code — không đổi tên.
- Các enum audit `decision_actor_role`/`cancellation_actor_type`/`host_assignment_source` chứa `STAFF_LEADER`/`AUTO_STAFF_LEADER` là nhãn người thực hiện (audit), KHÔNG phải role_code — giữ nguyên để không phá trigger.
