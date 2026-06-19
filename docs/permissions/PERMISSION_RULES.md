# Permission Rules — PEMS v8

> **Purpose:** File này là bản rule ngắn gọn để backend/frontend triển khai authorization nhất quán theo SQL v8.  
> **Source of truth for detailed UC matrix:** `PERMISSION_MATRIX_UPDATED_V5.md`.  
> File này không lặp lại toàn bộ ma trận UC; nó chỉ ghi các nguyên tắc bắt buộc, scope rule và các case dễ nhầm.

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
| `DEPT` | `Leader` | Department Lead |
| `DEPT` | `Staff` | Department |
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

This is the most important SQL v8 rule.

| Role | Single-campus request | Multi-campus before HO approval | Multi-campus after HO approval/release |
|---|---:|---:|---:|
| ADMIN | No access | No access | No access |
| HO | No access | View + approve/reject | View |
| Staff Leader, same campus | View + process | No access | View own campus instance |
| Staff Leader, other campus | No access | No access | No access |
| Staff | Only assigned/linked records | No access unless linked after release | Assigned/linked records only |
| Department Lead/Department | Department task/resource scope only | No access unless task/resource is released | Assigned resource/task scope only |
| Student | Assigned/participant scope only | No access unless assigned after release | Assigned/participant scope only |
| VISITOR | Own submitted/linked request only | Own submitted/linked request only | Own submitted/linked request only |

### 4.1. HO Query Rule

HO list/detail must use `vw_visit_requests_for_ho` or equivalent predicate:

```sql
WHERE visit_scope = 'MULTI_CAMPUS'
```

HO must not see `SINGLE_CAMPUS` even with direct ID access.

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

## 5. Visit Request Status vs Campus Progress Status

`visit_requests.status` is request/approval status only:

```text
PENDING_APPROVAL → APPROVED
PENDING_APPROVAL → REJECTED
PENDING_APPROVAL / APPROVED → CANCELLED
```

Do not add `HO_APPROVED`, `IN_PROGRESS`, or `COMPLETED` into `visit_requests.status`.

Use derived display labels instead:

| Display Label | How to derive |
|---|---|
| `WAITING_HO_APPROVAL` | `visit_scope = MULTI_CAMPUS` and `visit_requests.status = PENDING_APPROVAL` |
| `HO_APPROVED` | `visit_scope = MULTI_CAMPUS`, `visit_requests.status = APPROVED`, `decision_actor_role = HO` |
| `WAITING_STAFF_LEADER_APPROVAL` | `visit_scope = SINGLE_CAMPUS` and `visit_requests.status = PENDING_APPROVAL` |
| `STAFF_LEADER_APPROVED` | `visit_scope = SINGLE_CAMPUS`, `visit_requests.status = APPROVED`, `decision_actor_role = STAFF_LEADER` |
| `IN_PROGRESS` | derived from one or more `visit_request_campuses.status = DURING_VISIT` |
| `COMPLETED` | derived when all campus instances are `CLOSED` |

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


---

## 9. SQL v8 Visit Request Status Rule

`visit_requests.status` is request/approval-only:

```text
PENDING_APPROVAL
APPROVED
REJECTED
CANCELLED
```

It must not store `IN_PROGRESS` or `COMPLETED`.

`visit_request_campuses.status` is per-campus operational status:

```text
WAITING_REQUEST_APPROVAL
ASSIGNED
BEFORE_VISIT
DURING_VISIT
AFTER_VISIT
CLOSED
CANCELLED
```

Frontend must display two layers:

```text
requestStatus = visit_requests.status
campusStatus = visit_request_campuses.status
progressStatus = derived display label from vw_visit_request_progress_summary
```

## 10. SQL v8 Host Assignment Rule

`WAITING_REQUEST_APPROVAL` means the campus instance is waiting for main request approval. It is not a “waiting host” state.

After approval:

- `MULTI_CAMPUS`: HO approves; backend auto-assigns each campus Staff Leader as host; campus status becomes `ASSIGNED`; `host_assignment_source = AUTO_STAFF_LEADER`.
- `SINGLE_CAMPUS`: Staff Leader approves; Staff Leader must select host immediately; campus status becomes `ASSIGNED`; `host_assignment_source = MANUAL_APPROVAL`.
- Transfer Host: update current host and set `host_assignment_source = TRANSFERRED`.

A campus instance with status `ASSIGNED`, `BEFORE_VISIT`, `DURING_VISIT`, `AFTER_VISIT`, or `CLOSED` must have `current_host_user_id`.

## 11. SQL v8 Removed Columns

`visit_request_campuses` must not use:

```text
actual_start_at
actual_end_at
```

Do not reference these fields in backend entity, DTO, validators, mapping, query, or frontend forms.
---

# v8.2 Addendum — Cancel thuộc Delegation Feature

## UC-136 — Cancel Visit Request

- Permission code: `UC-136.CANCEL_VISIT_REQUEST`.
- Permission group: `Delegation Reception Management`.
- Backend module: `PEMS.Application/Delegations/Commands/CancelVisitRequest`.
- Controller: `DelegationsController`.

## Required metadata

Khi chuyển `visit_requests.status` hoặc `visit_request_campuses.status` sang `CANCELLED`, backend phải set:

```text
cancelled_by
cancelled_at
cancellation_actor_type
cancellation_source
cancellation_reason
```

Không dùng `external_confirmation_note`.

## Source rules

| Source | Rule |
|---|---|
| `SELF_SERVICE` | Visitor tự hủy trong hệ thống. |
| `EXTERNAL_CONFIRMATION` | Host/Staff hủy thay khách sau khi khách xác nhận bên ngoài. `cancellation_reason` bắt buộc ghi rõ kênh xác nhận, thời gian, người xác nhận, lý do. |
| `INTERNAL_DECISION` | HO/Staff Leader hủy theo lý do nội bộ hợp lệ. `cancellation_reason` bắt buộc. |

## Hard stop

Không cho hủy campus instance nếu status đã là:

```text
DURING_VISIT
AFTER_VISIT
CLOSED
```

Nếu đã xử lý xong visit, dùng UC-41 `Close Delegation`, không dùng UC-136.
