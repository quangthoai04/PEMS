# Visitor Management System — Main Flow v8

> Replacement for `VISITOR_MANAGEMENT_SYSTEM(2).md`.  
> Aligned with SQL v8: request status separated from campus status, no pending visit table, immediate host assignment after approval.

---

## 1. Purpose

This document describes the main visitor request and delegation handling flow for FPT University campuses.

The system supports:

- Visitor public visit request submission.
- OTP/email verification before saving official request.
- Single-campus and multi-campus routing.
- HO approval for multi-campus requests.
- Staff Leader approval for single-campus requests.
- Immediate host assignment after approval.
- Campus-level before/during/after/closed operation.

---

## 2. Status Model

### 2.1. Request status

Stored in `visit_requests.status`:

```text
PENDING_APPROVAL
APPROVED
REJECTED
CANCELLED
```

This is the status of the request/approval only.

### 2.2. Campus status

Stored in `visit_request_campuses.status`:

```text
WAITING_REQUEST_APPROVAL
ASSIGNED
BEFORE_VISIT
DURING_VISIT
AFTER_VISIT
CLOSED
CANCELLED
```

This is the operational status of each campus instance.

---

## 3. Visitor Public Submit Flow

```text
Visitor fills visit form
→ Frontend stores draft in sessionStorage
→ Visitor requests OTP
→ Backend stores OTP in otp_tokens only
→ Visitor enters OTP
→ Backend returns verificationToken
→ Frontend submits official form + verificationToken
→ Backend validates form and token
→ Backend creates/links VISITOR user
→ Backend inserts visit_requests
→ Backend inserts visit_request_campuses
→ Backend inserts visit_guest_members
```

Initial database state:

```text
visit_requests.status = PENDING_APPROVAL
visit_request_campuses.status = WAITING_REQUEST_APPROVAL
current_host_user_id = NULL
host_assignment_source = NULL
```

---

## 4. Flow A — Single-Campus Request

```text
Visitor submits request for 1 campus
→ Staff Leader of that campus sees pending request
→ Staff Leader approves/rejects
```

If rejected:

```text
visit_requests.status = REJECTED
```

If approved:

```text
Staff Leader must select host immediately
visit_requests.status = APPROVED
visit_request_campuses.status = ASSIGNED
current_host_user_id = selected host
host_assignment_source = MANUAL_APPROVAL
host_assigned_by = current Staff Leader
host_assigned_at = now
```

Then the campus progresses:

```text
ASSIGNED
→ BEFORE_VISIT
→ DURING_VISIT
→ AFTER_VISIT
→ CLOSED
```

---

## 5. Flow B — Multi-Campus Request

```text
Visitor submits request for 2+ campuses
→ HO sees pending request
→ Staff Leaders do not see it while pending HO approval
→ HO approves/rejects
```

If rejected:

```text
visit_requests.status = REJECTED
```

If approved:

```text
visit_requests.status = APPROVED
For each selected campus:
  visit_request_campuses.status = ASSIGNED
  current_host_user_id = Staff Leader of that campus
  host_assignment_source = AUTO_STAFF_LEADER
  host_assigned_by = HO user id or system actor
  host_assigned_at = now
```

After release, each Staff Leader sees only their own campus instance.

Staff Leader may transfer host:

```text
current_host_user_id = new IC Staff
host_assignment_source = TRANSFERRED
host_transferred_by = current actor
host_transferred_at = now
host_transfer_note = reason
```

---

## 6. UI Display

### Single-campus list row example

```text
Request Status: Chờ Staff Leader duyệt
Campus Progress: Chờ đơn được duyệt
Host: Chưa có
```

After approval:

```text
Request Status: Đã duyệt
Campus Progress: Đã giao host
Host: Nguyễn Văn A
```

### Multi-campus list row for HO

```text
Request Status: Chờ HO duyệt
Scope: Liên cơ sở
```

After HO approval:

```text
Request Status: Đã duyệt bởi HO
Campus Progress: Đã giao host các cơ sở
```

### Staff Leader view after HO approval

```text
Request Status: Đơn từ HO / Đã duyệt
Campus Progress: Đã giao bạn làm host
Host: Staff Leader campus hiện tại
```

---

## 7. Visibility

| Actor | Can see pending single-campus? | Can see pending multi-campus? | Can see released multi-campus? |
|---|---:|---:|---:|
| HO | No | Yes | Yes |
| Staff Leader same campus | Yes | No | Own campus only |
| Staff Leader other campus | No | No | No |
| Admin | No | No | No |
| Visitor | Own submitted request | Own submitted request | Own submitted request |

---

## 8. Removed/Invalid Old Flow

Do not implement these old states anymore:

```text
Đã duyệt — chưa có HOST
Staff click Nhận đón after approval
Campus approve/reject after HO approval
visit_requests.status = IN_PROGRESS / COMPLETED
actual_start_at / actual_end_at in visit_request_campuses
```

The host must be assigned at the moment of approval, either automatically or manually depending on visit scope.
---

# v8.2 Addendum — Luồng hủy request/delegation

## Visitor tự hủy

```text
[Visitor đăng nhập]
  ↓
[Mở chi tiết request của mình]
  ↓
[Bấm Hủy đơn]
  ↓
[Nhập lý do hủy]
  ↓
Backend kiểm tra:
- request thuộc visitor hiện tại
- request chưa REJECTED/CANCELLED
- campus chưa DURING_VISIT/AFTER_VISIT/CLOSED
  ↓
visit_requests.status = CANCELLED
visit_request_campuses.status = CANCELLED
cancellation_source = SELF_SERVICE
```

## Host hủy thay khách

```text
[Khách xác nhận hủy bên ngoài hệ thống]
  ↓
[Host hiện tại vào chi tiết delegation/campus instance]
  ↓
[Bấm Hủy thay khách]
  ↓
[Nhập cancellation_reason gồm kênh xác nhận + thời gian + lý do]
  ↓
Backend kiểm tra current_host_user_id
  ↓
Campus instance hoặc request single-campus chuyển CANCELLED
cancellation_source = EXTERNAL_CONFIRMATION
```

Không dùng `external_confirmation_note`; lý do và bằng chứng xác nhận ngoài hệ thống đều nằm trong `cancellation_reason`.
