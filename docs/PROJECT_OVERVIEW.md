# PEMS v8 — Project Overview
> Partnership Engagement Management System — FPT University  
> Version: v8 request-status/host-assignment alignment

---

## 1. Project Scope

PEMS digitizes and standardizes the process of receiving domestic and international visitors at FPT University campuses.

The system supports:

- Public visit request submission by Visitor.
- Email/OTP verification before official form submission.
- Single-campus approval by Staff Leader.
- Cross-campus approval by HO.
- Immediate host assignment after approval.
- Campus-level operation through before/during/after/closed stages.
- Partner, documents, gallery, news, minutes, feedback, logistics and reporting management.

---

## 2. Roles

| Role | DB mapping | Main scope |
|---|---|---|
| HO | `role_code = HO` | Cross-campus requests only; does not process single-campus requests |
| Admin | `role_code = ADMIN` | Technical administration; no visit/delegation business access |
| Staff Leader | `role_code = STAFF`, `sub_role = Leader` | Own-campus single-campus approval; own-campus released multi-campus instance |
| Staff | `role_code = STAFF`, `sub_role = Staff` | Operational host/support work within assigned campus/records |
| Department Lead | `role_code = DEPT`, `sub_role = Leader` | Department resource/task management |
| Department | `role_code = DEPT`, `sub_role = Staff` | Assigned department work |
| Student | `role_code = STUDENT` | Assigned participant/support work |
| Visitor | `role_code = VISITOR` | Submit and view own/linked request |

---

## 3. Core Visit Status Model

### 3.1. Request-level status

`visit_requests.status` stores only the state of the request/approval:

```text
PENDING_APPROVAL
APPROVED
REJECTED
CANCELLED
```

### 3.2. Campus-level status

`visit_request_campuses.status` stores the operational state of each campus:

```text
WAITING_REQUEST_APPROVAL
ASSIGNED
BEFORE_VISIT
DURING_VISIT
AFTER_VISIT
CLOSED
CANCELLED
```

`IN_PROGRESS` and `COMPLETED` are display/progress labels derived from campus statuses. They are not stored in `visit_requests.status`.

---

## 4. Main Flow — Visitor Submits Single-Campus Request

```text
Visitor fills form
→ OTP/email verification
→ Backend inserts visit_requests.status = PENDING_APPROVAL
→ Backend inserts visit_request_campuses.status = WAITING_REQUEST_APPROVAL
→ Staff Leader of that campus reviews
→ If rejected: visit_requests.status = REJECTED
→ If approved: Staff Leader chooses host immediately
→ visit_requests.status = APPROVED
→ visit_request_campuses.status = ASSIGNED
→ current_host_user_id = selected host
→ host_assignment_source = MANUAL_APPROVAL
→ Host/campus works through BEFORE_VISIT → DURING_VISIT → AFTER_VISIT → CLOSED
```

There is no “approved but no host” state.

---

## 5. Main Flow — Visitor Submits Multi-Campus Request

```text
Visitor fills form with 2+ campuses
→ OTP/email verification
→ Backend inserts request and campus instances
→ HO sees pending multi-campus request
→ Staff Leaders do not see it before HO approval
→ HO approves or rejects
→ If approved: backend auto-assigns Staff Leader of each selected campus as host
→ Each campus instance becomes ASSIGNED
→ Staff Leader can transfer host to another valid IC Staff if needed
→ Each campus manages its own BEFORE_VISIT → DURING_VISIT → AFTER_VISIT → CLOSED flow
```

---

## 6. Host Assignment

| Case | Host assignment |
|---|---|
| Multi-campus approved by HO | Auto Staff Leader of each campus, `AUTO_STAFF_LEADER` |
| Single-campus approved by Staff Leader | Staff Leader selects host manually, `MANUAL_APPROVAL` |
| Host changed later | Transfer Host, `TRANSFERRED` |

Fields:

```text
current_host_user_id
host_assigned_by
host_assigned_at
host_assignment_source
host_transferred_by
host_transferred_at
host_transfer_note
```

---

## 7. Visibility Rules

| Role | Single-campus | Multi-campus pending HO | Multi-campus after HO approval |
|---|---:|---:|---:|
| ADMIN | No | No | No |
| HO | No | Yes, decide | Yes, view |
| Staff Leader, same campus | Yes, decide | No | Yes, own campus instance |
| Staff Leader, other campus | No | No | No |
| Staff/Department/Student | Assigned/linked only | No unless linked after release | Assigned/linked only |
| Visitor | Own request only | Own request only | Own request only |

---

## 8. UI Status Display

UI should show two layers:

```text
Request Status: Chờ duyệt / Đã duyệt / Bị từ chối / Đã hủy
Campus Progress: Đã giao host / Đang chuẩn bị / Đang diễn ra / Hậu xử lý / Đã hoàn tất
```

Recommended API fields:

```json
{
  "requestStatus": "APPROVED",
  "campusStatus": "BEFORE_VISIT",
  "progressStatus": "PREPARING",
  "approvalDisplay": "Đã duyệt bởi HO",
  "currentHostName": "Nguyễn Văn A",
  "hostAssignmentSource": "AUTO_STAFF_LEADER"
}
```

---

## 9. Removed From Old Design

The following older concepts are no longer valid:

- `visit_requests.status = IN_PROGRESS`.
- `visit_requests.status = COMPLETED`.
- “Approved but waiting host”.
- Campus-level approval/rejection after HO approval for multi-campus requests.
- `actual_start_at` and `actual_end_at` in `visit_request_campuses`.
- `pending_visit_requests` table.
---

# v8.2 Addendum — Hủy đơn/chuyến thăm trong FE-02 Delegation

Bổ sung UC-136 `Cancel Visit Request` vào **FE-02 — Quản lý Tiếp đón Đoàn khách**.

| UC | Tên | Actor |
|---|---|---|
| UC-136 | Cancel Visit Request | Visitor, HO, Staff Leader, Staff/Host |

Luồng hủy:

- Visitor tự hủy request của mình nếu chưa vào giai đoạn đang diễn ra/hậu xử lý/đã đóng.
- Host có thể hủy thay khách nếu khách xác nhận bên ngoài hệ thống nhưng không đăng nhập để tự hủy.
- Staff Leader xử lý hủy trong phạm vi campus mình.
- HO xử lý hủy request liên cơ sở.

Không dùng `external_confirmation_note`. Nếu hủy do xác nhận ngoài hệ thống, thông tin xác nhận được ghi trong `cancellation_reason`.
