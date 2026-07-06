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

# UC-17 Submit Visit Request — Email Verification + v8 Request/Campus Status Flow

> Replacement for `uc17 submit form.md`.  
> Aligned with SQL v8: numeric IDs, no `pending_visit_requests`, request status separated from campus operational status, immediate host assignment after approval.

---

## 1. Goal

Implement the public Visitor visit-request submission flow without storing unverified form data in the database.

The system must:

1. Keep the draft form temporarily on frontend only.
2. Verify the registrant email using OTP.
3. Submit the official form only after OTP verification succeeds.
4. Insert `visit_requests` only after verification and backend validation.
5. Create `visit_request_campuses` rows with status `WAITING_REQUEST_APPROVAL`.
6. Keep `visit_requests.status` as request decision status only.
7. Use campus statuses for operational progress.

---

## 2. Fixed Database Rules

### 2.1. No pending table

Do not create or use:

```sql
pending_visit_requests
```

Frontend keeps draft form in `sessionStorage`. Backend only stores OTP metadata in `otp_tokens` until final submit.

### 2.2. Request status

`visit_requests.status` stores only request decision state:

```sql
PENDING_APPROVAL
APPROVED
REJECTED
CANCELLED
```

It must not store:

```sql
IN_PROGRESS
COMPLETED
```

### 2.3. Campus status

`visit_request_campuses.status` stores per-campus state:

```sql
WAITING_REQUEST_APPROVAL
ASSIGNED
BEFORE_VISIT
DURING_VISIT
AFTER_VISIT
CLOSED
CANCELLED
```

`WAITING_REQUEST_APPROVAL` means the campus was selected but the main request has not been approved yet.

### 2.4. Host assignment

After approval, there is no “approved but waiting host” state.

- `MULTI_CAMPUS`: HO approves → backend auto-assigns Staff Leader of each campus as host → campus status becomes `ASSIGNED`.
- `SINGLE_CAMPUS`: Staff Leader approves → Staff Leader must choose a host immediately → campus status becomes `ASSIGNED`.
- Transfer later: use Transfer Host feature, update `current_host_user_id` and `host_assignment_source = 'TRANSFERRED'`.

---

## 3. Frontend Flow

```text
Step 1: Visitor fills visit form
Step 2: Frontend validates basic fields
Step 3: Frontend stores draft in sessionStorage
Step 4: Frontend asks backend to send OTP to registrant email
Step 5: Visitor enters OTP
Step 6: Backend verifies OTP and returns short-lived verificationToken
Step 7: Frontend reads draft from sessionStorage
Step 8: Frontend submits full form + verificationToken + idempotencyKey
Step 9: Backend validates again and inserts official records
Step 10: Frontend clears sessionStorage draft
```

### Draft storage

```ts
const VISIT_REQUEST_DRAFT_KEY = "pems.visitRequestDraft";

type VisitRequestDraft = {
  version: 1;
  step: "EMAIL_VERIFY";
  email: string;
  idempotencyKey: string;
  data: SubmitVisitRequestFormValues;
  createdAt: number;
  expiresAt: number;
};
```

Expiration: 30 minutes.

Do not store `File` objects in sessionStorage. Keep files in memory; if page reloads, require user to select files again.

---

## 4. Backend Endpoints

```http
POST /api/public/visit-requests/send-verification-code
POST /api/public/visit-requests/verify-code
POST /api/public/visit-requests/submit
```

### 4.1. Send OTP

Request:

```json
{ "email": "visitor@example.com" }
```

Backend requirements:

- Validate email format.
- Normalize email by trim + lowercase.
- Rate-limit by IP and email.
- Enforce resend cooldown, recommended 60 seconds.
- Generate cryptographically secure 6-digit OTP.
- Store only hashed OTP in `otp_tokens`.
- Do not return OTP in API response or logs.

Recommended limits:

| Limit | Rule |
|---|---|
| IP short window | 10 send attempts / 10 minutes |
| IP hourly | 30 send attempts / 1 hour |
| Email short window | 3 OTP sends / 15 minutes |
| Email daily | 8 OTP sends / day |
| Resend cooldown | 60 seconds |

### 4.2. Verify OTP

Request:

```json
{
  "email": "visitor@example.com",
  "code": "123456"
}
```

Backend requirements:

- Find latest unused OTP with purpose `VISIT_REQUEST_VERIFY`.
- OTP must not be expired.
- Increment `attempt_count` on wrong code.
- Reject when `attempt_count >= max_attempts`.
- Mark token `used_at = now` on success.
- Return signed short-lived `verificationToken`.

Token should contain:

```json
{
  "purpose": "VISIT_REQUEST_SUBMIT",
  "email": "visitor@example.com",
  "otpTokenId": 123,
  "exp": 1710000600
}
```

### 4.3. Submit official form

Request includes:

```json
{
  "verificationToken": "...",
  "idempotencyKey": "uuid",
  "registrantEmail": "visitor@example.com",
  "visitScope": "SINGLE_CAMPUS",
  "campuses": [],
  "guestMembers": []
}
```

Backend requirements:

1. Verify `verificationToken` signature, purpose, expiry.
2. Ensure token email matches `registrantEmail`.
3. Validate all form fields again.
4. Check duplicate/idempotency.
5. Start transaction.
6. Create/link VISITOR user.
7. Insert `visit_requests`.
8. Insert `visit_request_campuses` with `WAITING_REQUEST_APPROVAL`.
9. Insert `visit_guest_members`.
10. Insert files/documents if applicable.
11. Insert status log/audit log.
12. Commit.

---

## 5. Insert Rules

### 5.1. `visit_requests`

Initial insert:

```text
status = PENDING_APPROVAL
email_verified_at = now
visitor_user_id = linked visitor user
registrant_nationality = form.registrantNationality
```

Do not insert `IN_PROGRESS` or `COMPLETED` into this table.

### 5.2. `visit_request_campuses`

Initial insert for each selected campus:

```text
status = WAITING_REQUEST_APPROVAL
current_host_user_id = NULL
host_assigned_by = NULL
host_assigned_at = NULL
host_assignment_source = NULL
```

### 5.3. Host assignment after approval

This is not part of UC-17 submit, but UC-17 must prepare records correctly for these later flows.

#### Multi-campus approval by HO

```text
visit_requests.status = APPROVED
visit_requests.decision_actor_role = HO

For each campus instance:
status = ASSIGNED
current_host_user_id = Staff Leader of that campus
host_assignment_source = AUTO_STAFF_LEADER
host_assigned_by = HO user id or system actor
host_assigned_at = now
```

#### Single-campus approval by Staff Leader

```text
visit_requests.status = APPROVED
visit_requests.decision_actor_role = STAFF_LEADER

Campus instance:
status = ASSIGNED
current_host_user_id = selected host from valid IC staff list
host_assignment_source = MANUAL_APPROVAL
host_assigned_by = current Staff Leader
host_assigned_at = now
```

---

## 6. Backend Validation

### Registrant

- `registrantFullName`: required, max 150.
- `registrantOrganization`: required, max 200.
- `registrantEmail`: required, email format, must match verification token.
- `registrantPhone`: optional, max 50.
- `registrantNationality`: optional, max 100.

### Request

- `delegationName`: required, max 200.
- `visitScope`: required, `SINGLE_CAMPUS` or `MULTI_CAMPUS`.
- `purpose`: required.
- `expectedGuestCount`: required, >= 1.
- `workingLanguage`: `VI`, `EN`, or `OTHER`.

### Campus selection

- If `SINGLE_CAMPUS`, exactly 1 campus is required.
- If `MULTI_CAMPUS`, at least 2 campuses are required.
- Each campus must exist and be active.
- No duplicate campus in the same request.
- `plannedEndAt > plannedStartAt`.
- Planned visit time cannot be in the past.

### Duplicate detection

Reject duplicate submit if recent request matches:

```text
same registrant_email
same delegation_name
same visit_scope
same campus set
same first planned_start_at
submitted within last 10 minutes
status not in REJECTED/CANCELLED
```

Return `409 DUPLICATE_VISIT_REQUEST`.

---

## 7. Error Codes

| Code | Message |
|---|---|
| `VALIDATION_ERROR` | Dữ liệu không hợp lệ. Vui lòng kiểm tra lại. |
| `RATE_LIMITED` | Bạn thao tác quá nhanh. Vui lòng thử lại sau ít phút. |
| `OTP_RESEND_TOO_SOON` | Vui lòng chờ trước khi yêu cầu mã mới. |
| `OTP_SEND_LIMIT_REACHED` | Email này đã yêu cầu mã quá nhiều lần. Vui lòng thử lại sau. |
| `OTP_INVALID` | Mã xác thực không đúng hoặc đã hết hạn. |
| `OTP_EXPIRED` | Mã xác thực đã hết hạn. Vui lòng yêu cầu mã mới. |
| `OTP_ATTEMPT_LIMIT_EXCEEDED` | Bạn đã nhập sai quá số lần cho phép. Vui lòng yêu cầu mã mới. |
| `VERIFICATION_TOKEN_INVALID` | Phiên xác thực không hợp lệ. Vui lòng xác thực lại email. |
| `VERIFICATION_TOKEN_EXPIRED` | Phiên xác thực đã hết hạn. Vui lòng xác thực lại email. |
| `EMAIL_MISMATCH` | Email đã xác thực không khớp với email trong form. Vui lòng xác thực lại. |
| `DUPLICATE_VISIT_REQUEST` | Yêu cầu này có vẻ đã được gửi trước đó. Vui lòng kiểm tra lại. |
| `CAMPUS_NOT_FOUND` | Cơ sở được chọn không tồn tại. |
| `CAMPUS_INACTIVE` | Cơ sở được chọn hiện không hoạt động. |
| `INVALID_VISIT_SCOPE` | Loại yêu cầu thăm quan không hợp lệ. |
| `INVALID_VISIT_TIME` | Thời gian thăm quan không hợp lệ. |
| `FILE_REQUIRED_AGAIN` | Vui lòng chọn lại file đính kèm trước khi gửi. |

---

## 8. Acceptance Criteria

- No `pending_visit_requests` table.
- No unverified form stored in database.
- OTP is hashed, not plain text.
- `visit_requests.status` starts as `PENDING_APPROVAL`.
- `visit_request_campuses.status` starts as `WAITING_REQUEST_APPROVAL`.
- `current_host_user_id` is null before approval.
- Single-campus approval must choose a host.
- Multi-campus approval auto-assigns Staff Leader for each campus.
- No `actual_start_at` or `actual_end_at` is used.
- Frontend displays request status and campus/progress status separately.

---

# Addendum — UC-17 không xử lý hủy đơn


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


## UC-17 boundary

UC-17 chỉ bao gồm:

1. Visitor nhập form.
2. Frontend lưu draft ở `sessionStorage`.
3. Backend gửi OTP.
4. Backend verify OTP.
5. Frontend submit form chính thức.
6. Backend tạo `visit_requests` và `visit_request_campuses`.

UC-17 không bao gồm hủy đơn sau khi form đã submit. Sau khi request tồn tại, mọi thao tác hủy thuộc UC-136 trong Delegation Reception Management.
