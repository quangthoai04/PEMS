# PROMPT THỰC THI UC17 — OTP 10 LẦN, TURNSTILE, IDEMPOTENCY VÀ DUPLICATE V2 KHÔNG THÊM GUARD TABLE

## 1. Vai trò của AI Agent

Bạn là Senior Full-stack Engineer chịu trách nhiệm triển khai hoàn chỉnh thay đổi UC17 — gửi đơn đăng ký tham quan trong dự án PEMS, với các vai trò đồng thời:

- Senior ASP.NET Core .NET 8 Clean Architecture Developer.
- Senior React Vite TypeScript Engineer.
- Database-first MySQL 8 Engineer.
- Security/abuse-prevention Reviewer.
- Unit Test, Integration Test và Playwright QA Engineer.
- UI/UX Reviewer tuân thủ PEMS Design System.

Đây là yêu cầu **thực thi code**, không phải yêu cầu lập thêm kế hoạch. Hãy đọc source thật, triển khai đến khi hoàn tất, chạy test/build thật và báo cáo trung thực kết quả.

Không commit, push, tạo PR, đổi branch hoặc làm mất thay đổi có sẵn nếu chưa được yêu cầu riêng.

---

## 2. Bối cảnh dự án

PEMS là hệ thống Partnership/Event/Reception/Engagement Management System của FPT University.

Tech stack:

- Backend: ASP.NET Core .NET 8, Clean Architecture, MediatR, EF Core/Pomelo MySQL.
- Frontend: React Vite TypeScript, Tailwind CSS, react-hook-form, Zod, i18next.
- Database: MySQL 8, database-first, InnoDB, fresh-create/manual SQL.
- Public UC17 flow: validate full form → gửi OTP → verify OTP cùng full payload → tạo/link Visitor → tạo VisitRequest và child data.

UC17 hiện đã được refactor thành một form liên tục, UI phẳng và có màn kết quả sau OTP không tự đóng. Không được làm mất các thay đổi này.

---

## 3. Quyết định kiến trúc đã chốt

### 3.1. Không thêm bảng dedupe guard

Trong lần triển khai này:

- **Không tạo** bảng `visit_request_dedupe_guards`.
- Không dùng MySQL `GET_LOCK`.
- Không thêm Redis/distributed lock.
- Không tuyên bố bảo đảm exactly-once tuyệt đối cho hai phiên độc lập có `submissionId` khác nhau chạy đồng thời.

Chỉ dùng:

1. `submission_id UNIQUE` để chống double-click/network retry của cùng một submit intent.
2. `business_fingerprint` + query theo thời gian/status để phát hiện hai submit intent khác nhau nhưng có cùng nội dung cốt lõi.

### 3.2. Rủi ro concurrency được chấp nhận có chủ đích

Không có guard/distributed lock nên vẫn tồn tại một race hiếm:

```text
Hai phiên độc lập
→ submissionId khác nhau
→ businessFingerprint giống nhau
→ cùng kiểm tra trước khi request nào commit
→ về lý thuyết có thể cùng tạo đơn
```

Phải ghi rõ residual risk này trong báo cáo cuối. Không được viết test hoặc báo cáo khẳng định trường hợp trên luôn chỉ tạo một row.

Các trường hợp sau vẫn bắt buộc chống được:

- Double-click cùng `submissionId`.
- Trình duyệt retry cùng `submissionId`.
- API retry sau khi response bị mất.
- Gửi lại tuần tự bằng `submissionId` khác nhưng cùng fingerprint trong 15 phút.

### 3.3. Không tăng số lượng bảng

SQL chỉ sửa:

- `otp_tokens`.
- `visit_requests`.

Không thêm bảng mới. Nếu database hiện có 58 bảng thì sau thay đổi vẫn là 58 bảng.

---

## 4. Tài liệu và source bắt buộc đọc trước

### 4.1. Thứ tự ưu tiên khi nguồn mâu thuẫn

Ưu tiên:

1. SQL fresh-create mới nhất.
2. SQL Table & Field Dictionary mới nhất.
3. `PEMS_CANONICAL_BUSINESS_RULES`.
4. `PEMS_UC_IMPLEMENTATION_RULEBOOK_FRONTEND_BACKEND_DATABASE_VALIDATION_SECURITY`.
5. `PROJECT_OVERVIEW`.
6. `VISITOR_MANAGEMENT_SYSTEM`.
7. Source code hiện tại.
8. Tài liệu legacy chỉ dùng đối chiếu.

Không dùng field/table/enum không tồn tại trong SQL thật. Không đưa dynamic permissions trở lại.

### 4.2. Tài liệu

Đọc tối thiểu:

- `PROJECT_KNOWLEDGE.md`.
- `PEMS_CLAUDE_PROJECT_INSTRUCTIONS_v8_4_refined_v6_v10_FULL_UPDATED.md`.
- `PEMS_UI_DESIGN_SYSTEM_PROMPT.md`.
- `CLEAN_ARCHITECTURE.md`.
- `PEMS_CANONICAL_BUSINESS_RULES_v8_4_refined_v6_v10_FULL_UPDATED.md`.
- `PEMS_UC_IMPLEMENTATION_RULEBOOK_FRONTEND_BACKEND_DATABASE_VALIDATION_SECURITY_v8_4_refined_v6_v10_FULL_UPDATED.md`.
- `VISITOR_MANAGEMENT_SYSTEM_v8_4_refined_v6_v10_FULL_UPDATED.md`.
- `USE_CASE_LIST.md` và UC17 documents/reports hiện có.
- `PROJECT_STRUCTURE_FULL.md`.
- SQL canonical mới nhất và field dictionary tương ứng.

### 4.3. Backend

Search và đọc source thật, tối thiểu:

- `backend/PEMS.Infrastructure/Identity/OtpService.cs`.
- `IOtpService` và các OTP result model.
- `backend/PEMS.Domain/Entities/Users/OtpToken.cs`.
- Entity/configuration của `VisitRequest`.
- `InitiateVisitRequestCommand`, validator và handler.
- `VerifyAndCreateVisitRequestCommand`, validator và handler.
- `ResendVisitRequestOtpCommand`, validator và handler.
- `backend/PEMS.Api/Controllers/VisitRequestsController.cs`.
- Global exception/error handling.
- Transaction methods trong `IApplicationDbContext`.
- Request context/IP accessor/rate limiter hiện có.
- Existing backend tests cho UC17/OTP/visit request.

### 4.4. Frontend

Đọc tối thiểu:

- `frontend/pems-react/src/features/visit-request/api/visitRequestApi.ts`.
- `frontend/pems-react/src/features/visit-request/hooks/useVisitRequestForm.ts`.
- `OtpVerificationModal.tsx`.
- `VisitingFormPopup.tsx`.
- `SubmittedVisitRequestSummary.tsx`.
- Draft storage và reset flow liên quan.
- VI/EN locale của visit request/error/toast.
- `frontend/pems-react/tests/visit-request-single-form.spec.ts`.

### 4.5. Repository safety

Trước khi sửa:

- Chạy `git branch --show-current` và `git status --short`.
- Bảo toàn unrelated changes.
- Không reset/checkout/delete file người dùng đã sửa.
- Nếu `DualPortalLoginForms.tsx` hoặc file khác đang modified từ trước, không đưa vào scope nếu không thật sự liên quan.

---

## 5. Mục tiêu nghiệp vụ

### OTP

- Cho phép tối đa 10 lần **thực sự sai** trên mỗi OTP challenge.
- Nếu mã đúng ở lần thử thứ 10 vẫn phải thành công.
- Sau lần sai thứ 10 mới yêu cầu xác minh người thật.
- Không khóa email/tài khoản vĩnh viễn.
- CAPTCHA thành công phải gửi OTP mới, không mở khóa OTP cũ.
- Số lần sai phải persist thật dù API trả lỗi.
- Chống lost update khi nhiều request thử cùng một OTP.
- Session OTP phải là opaque random token, không phải email.

### Duplicate/idempotency

- Double-click hoặc retry cùng lần gửi không tạo đơn thứ hai.
- Hai lần gửi tuần tự có cùng core visit trong 15 phút bị báo trùng.
- Khác campus/date/time/visit type/contact email không bị báo trùng.
- Request cũ `REJECTED` hoặc `CANCELLED` không chặn request mới.
- Duplicate phải hiện thành màn kết quả riêng sau OTP, không phải lỗi OTP.

---

## 6. Phạm vi được sửa

- SQL canonical và incremental patch liên quan `otp_tokens`, `visit_requests`.
- Domain entities/configurations tương ứng.
- OTP application/infrastructure services.
- Initiate/verify/resend/recover commands, validators, handlers.
- Visit Requests controller và error contract.
- Human verification abstraction + Turnstile infrastructure implementation.
- Request metadata/IP/User-Agent abstraction nếu chưa có.
- Fingerprint builder/idempotency logic.
- Frontend visit request API/hook/OTP modal/result UI.
- i18n VI/EN.
- Unit Tests, Integration Tests, Playwright tests.

## 7. Phạm vi không được sửa

- Không thêm `visit_request_dedupe_guards` hoặc bảng mới tương đương.
- Không thay đổi approval/routing theo campus.
- Không thay đổi Staff Leader approve/reject/host rules.
- Không thay đổi role/sub-role/authorization.
- Không thay đổi edit pending, resubmit rejected, cancel 24 giờ.
- Không thay đổi cấu trúc form một trang đã hoàn thành.
- Không lưu raw OTP, raw challenge token hoặc raw Turnstile token.
- Không tự tạo EF Migration nếu project đang dùng manual/canonical SQL.
- Không commit secrets.

---

# PHẦN A — OTP V2

## 8. Lỗi hiện tại cần xác minh và sửa

Baseline hiện được biết:

1. `Otp:MaxAttempts` mặc định 5.
2. `OtpService.VerifyAsync()` tăng `AttemptCount` và gọi `SaveChangesAsync()`.
3. `VerifyAndCreateVisitRequestCommandHandler` mở transaction trước verify OTP.
4. Khi OTP sai, handler ném exception rồi rollback transaction.
5. Do đó `attempt_count` có khả năng bị rollback và không tăng thật.
6. `/initiate` trả `sessionToken`, nhưng token hiện là email.
7. `/verify` chưa gửi token này về backend.
8. Backend đang chọn OTP mới nhất theo email + purpose.
9. IP/User-Agent của UC17 đang truyền `null`.

Xác minh từng điểm bằng code. Viết Integration Test tái hiện persistence bug trước hoặc cùng lúc sửa.

## 9. Quy tắc attempt

| Lần thực sự sai | Hành vi |
| ---: | --- |
| 1–5 | Báo sai, cho thử lại |
| 6 | Server cooldown 2 giây |
| 7 | Cooldown 4 giây |
| 8 | Cooldown 8 giây |
| 9 | Cooldown 15 giây |
| 10 | Invalid challenge và yêu cầu human verification |

Yêu cầu:

- So sánh mã trước khi quyết định đây là lần sai thứ 10.
- Correct-attempt-10 phải success.
- Backend enforce cooldown; countdown frontend chỉ là presentation.
- Frontend không tự tăng/giảm attempt.
- Challenge đã yêu cầu CAPTCHA không nhận OTP code tiếp.

## 10. Không khóa email

Phân biệt:

1. Attempt limit theo challenge.
2. Issue/resend limit theo email.
3. Source limit theo IP/session.

Không thêm `email_locked`, `account_locked`, `blocked_email` hoặc logic khóa vĩnh viễn.

Configuration thay vì magic number:

```json
{
  "Otp": {
    "VisitRequestCodeMinutes": 5,
    "MaxAttempts": 10,
    "ProgressiveDelayStartAttempt": 6,
    "MinResendIntervalSeconds": 60,
    "MaxStandardIssuesPerEmailPerHour": 5,
    "MaxRecoveryIssuesPerEmailPerHour": 1,
    "AbsoluteMaxIssuesPerEmailPerHour": 7
  }
}
```

CAPTCHA recovery có thể vượt soft standard limit một lần nhưng không được vượt absolute hard limit.

## 11. Opaque session/challenge token

Giữ tên `sessionToken` để giảm breaking change nhưng đổi semantics:

- Sinh bằng CSPRNG.
- Frontend nhận raw token.
- Database chỉ lưu SHA-256.
- Không log raw token.
- Token gắn với normalized email, purpose và submission ID.

`/initiate` response tối thiểu:

```json
{
  "sessionToken": "opaque-random-token",
  "maskedEmail": "ha***@example.com",
  "expiresAt": "2026-07-11T12:05:00+07:00",
  "resendAfterSeconds": 60,
  "maxAttempts": 10,
  "message": "..."
}
```

`/verify` nhận:

```json
{
  "submissionId": "UUID",
  "sessionToken": "opaque-random-token",
  "otpCode": "123456",
  "...": "full form payload"
}
```

Verify phải kiểm tra:

- Challenge token hash tồn tại.
- Đúng email/purpose/submissionId.
- Chưa hết hạn, chưa used, chưa invalidated.
- Chưa yêu cầu human verification.

## 12. Transaction OTP

### OTP sai

```text
Begin transaction
→ Lock otp_tokens row
→ Kiểm tra cooldown
→ So sánh mã
→ Tăng attempt_count
→ Cập nhật last_attempt_at/next_attempt_allowed_at
→ Nếu sai lần 10: human_verification_required_at + invalidated_at
→ Save
→ Commit
→ Sau commit mới trả typed error
```

Không được để catch chung rollback mất attempt.

### OTP đúng và tạo đơn thành công

```text
Begin transaction
→ Lock OTP row
→ Verify đúng
→ Idempotency re-check
→ Duplicate check
→ Validate routing/provision Visitor/create request + children
→ Mark OTP used
→ Save
→ Commit
```

OTP consumption và create request phải atomic.

### OTP đúng nhưng create lỗi sau verify

- Rollback.
- OTP chưa bị mất.

### OTP đúng nhưng duplicate

- Commit OTP đã dùng.
- Không tạo request/account/child/notification mới.
- Trả typed `409 DUPLICATE_VISIT_REQUEST`.
- Frontend hiển thị duplicate result.

### Concurrency cùng OTP

- Dùng `SELECT ... FOR UPDATE` hoặc atomic mechanism tương đương.
- Không cho lost update attempt count.
- Integration Test bằng MySQL thật.

## 13. Turnstile

Application abstraction:

```csharp
public interface IHumanVerificationService
{
    Task<HumanVerificationResult> VerifyAsync(
        string token,
        string? ipAddress,
        CancellationToken cancellationToken);
}
```

Infrastructure triển khai Turnstile bằng HttpClient và server-side validation:

- Kiểm tra `success`.
- Kiểm tra expected `action`.
- Kiểm tra allowed hostname.
- Reject expired/replayed token.
- Remote IP lấy phía server nếu sử dụng.

Config/secret:

- Site key frontend lấy từ Vite env.
- Secret chỉ backend/environment.
- Production fail closed nếu bật nhưng thiếu secret.
- Testing dùng fake service, không gọi Internet.
- Development bypass nếu có phải explicit và không chạy được trong Production.

Thêm endpoint, ví dụ:

```http
POST /api/visit-requests/otp/recover
```

Request:

```json
{
  "submissionId": "UUID",
  "sessionToken": "old-session-token",
  "humanVerificationToken": "turnstile-token",
  "registrantFullName": "Nguyễn Văn A"
}
```

Success:

- Invalid OTP cũ.
- Ghi human verified.
- Issue OTP/challenge mới với `issue_reason = HUMAN_RECOVERY`.
- Attempt mới bằng 0.
- Trả session token mới.

## 14. Error contract OTP

| HTTP | Code |
| ---: | --- |
| 400 | `OTP_INVALID` |
| 400 | `OTP_EXPIRED` |
| 400 | `OTP_NOT_FOUND` |
| 400/401 theo convention | `OTP_SESSION_INVALID` |
| 429 | `OTP_RETRY_LATER` |
| 428 | `OTP_HUMAN_VERIFICATION_REQUIRED` |
| 400 | `HUMAN_VERIFICATION_FAILED` |
| 429 | `OTP_RESEND_RATE_LIMITED` |

Response phải có metadata khi phù hợp:

```json
{
  "errorCode": "OTP_INVALID",
  "remainingAttempts": 2,
  "retryAfterSeconds": 8,
  "humanVerificationRequired": false
}
```

Không để frontend parse message tiếng Việt.

---

# PHẦN B — SUBMISSION ID VÀ BUSINESS FINGERPRINT

## 15. Ý nghĩa bắt buộc

### `submission_id`

Là mã UUID của **một submit intent**, dùng chống lỗi kỹ thuật:

- Double-click.
- Retry do mất mạng.
- Frontend gửi lại vì không nhận được response.

Cùng một flow initiate/resend/recover/verify phải giữ cùng `submissionId`.

### `business_fingerprint`

Là SHA-256 của dữ liệu cốt lõi chuyến thăm, dùng phát hiện hai submit intent khác nhau có cùng nội dung nghiệp vụ.

Hai trường không thay thế nhau.

Ví dụ:

```text
Double-click:
submissionId giống nhau
fingerprint giống nhau
→ submission_id UNIQUE xử lý

Hai tab:
submissionId khác nhau
fingerprint giống nhau
→ business fingerprint query xử lý nếu request đầu đã commit

Đổi campus/ngày:
submissionId khác
fingerprint khác
→ cho tạo request mới
```

## 16. Lifecycle submissionId

Frontend tạo bằng:

```ts
crypto.randomUUID()
```

Giữ nguyên qua:

- `/initiate`.
- `/resend-otp`.
- `/otp/recover`.
- `/verify`.

Reset khi:

- Người dùng đóng success/duplicate result.
- Người dùng bỏ hoàn toàn form.
- Bắt đầu submit intent mới theo state machine rõ ràng.

Không hiển thị submissionId và không dùng nó như authorization token.

Backend:

- Validate format UUID.
- Nếu đã có request cùng submissionId, normalized registrant email và fingerprint cùng khớp: trả lại request cũ bằng HTTP 200.
- Nếu cùng submissionId nhưng email/fingerprint khác: `409 IDEMPOTENCY_KEY_REUSED`.
- Unique DB constraint là hàng rào cuối.

## 17. Concurrent retry cùng submissionId

Hai request cùng submissionId có thể cùng kiểm tra trước khi row được commit. Phải xử lý:

- Kiểm tra idempotency trước verify.
- Re-check ở điểm thích hợp sau khi chờ OTP row/transaction lock.
- Catch unique-constraint `DbUpdateException`, re-query row đã tạo.
- Nếu email/fingerprint khớp, trả response idempotent.
- Nếu không khớp, trả `IDEMPOTENCY_KEY_REUSED`.

Không trả OTP error sai cho retry hợp lệ nếu request đầu đã được tạo.

## 18. Fingerprint v1

Tạo server-side pure deterministic builder. Client không được tự quyết định fingerprint.

Thành phần:

1. Normalized registrant email.
2. Normalized effective contact email.
3. Normalized delegation name.
4. Effective visit scope.
5. Visit type.
6. Normalized `visitTypeOther` khi `OTHER`, ngược lại rỗng.
7. Sorted campus schedules: campus code + start + end.

Normalization:

- Email: trim + lowercase invariant.
- Text: Unicode NFC, trim, collapse whitespace, lowercase invariant.
- Không bỏ dấu tiếng Việt.
- Enum/campus: uppercase invariant.
- Campus visits: sort theo campus/start/end.
- Date/time: canonical wall-clock UTC+7 đến phút, không lệch ngày/giờ UI đã nhập.
- Prefix canonical input bằng `v1`.
- Hash SHA-256 hex 64 ký tự.
- Không lưu canonical raw PII string.

Không đưa vào hard fingerprint:

- Purpose.
- Working content.
- Notes.
- Transportation note.
- Visitors.
- Support team.
- Media consent.
- PartnerId.

## 19. Duplicate rule

Chỉ duplicate khi:

```text
business_fingerprint bằng nhau
AND submitted_at >= now - 15 phút
AND status NOT IN (REJECTED, CANCELLED)
```

Cho phép request mới nếu:

- Khác registrant/contact email.
- Khác campus.
- Khác start/end.
- Khác visit type/OTHER value.
- Request cũ REJECTED/CANCELLED.
- Request giống nhưng ngoài 15 phút.

Trong scope này không triển khai fuzzy similarity/warning.

## 20. Không đặt fingerprint UNIQUE

`business_fingerprint` chỉ có normal index. Tuyệt đối không tạo:

```sql
UNIQUE (business_fingerprint)
```

Vì unique fingerprint sẽ chặn vĩnh viễn request hợp lệ sau 15 phút hoặc sau khi request cũ bị reject/cancel.

## 21. Duplicate response

Sau OTP hợp lệ, trả tối thiểu:

```json
{
  "errorCode": "DUPLICATE_VISIT_REQUEST",
  "existingVisitRequestId": 123,
  "existingRequestCode": "VR-2026-000123",
  "existingStatus": "PENDING_APPROVAL",
  "existingSubmittedAt": "2026-07-11T11:30:00+07:00",
  "message": "..."
}
```

Không trả full dữ liệu đơn cũ. Không gửi email/notification mới.

---

# PHẦN C — SQL BẮT BUỘC

## 22. Chỉ sửa hai bảng

Không thêm bảng mới. Tạo incremental patch và cập nhật canonical fresh-create SQL.

### 22.1. `otp_tokens`

Thay đổi tương đương:

```sql
ALTER TABLE otp_tokens
  ADD COLUMN challenge_token_hash CHAR(64) NULL
    COMMENT 'SHA-256 của opaque challenge token; không lưu raw token'
    AFTER token_hash,

  ADD COLUMN submission_id CHAR(36) NULL
    COMMENT 'UUID của submit intent UC17'
    AFTER challenge_token_hash,

  ADD COLUMN issue_reason VARCHAR(30) NOT NULL DEFAULT 'INITIAL'
    COMMENT 'INITIAL, RESEND hoặc HUMAN_RECOVERY'
    AFTER submission_id,

  ADD COLUMN last_attempt_at DATETIME NULL
    AFTER attempt_count,

  ADD COLUMN next_attempt_allowed_at DATETIME NULL
    AFTER last_attempt_at,

  ADD COLUMN human_verification_required_at DATETIME NULL
    AFTER next_attempt_allowed_at,

  ADD COLUMN human_verified_at DATETIME NULL
    AFTER human_verification_required_at,

  ADD COLUMN invalidated_at DATETIME NULL
    AFTER human_verified_at,

  ADD COLUMN invalidation_reason VARCHAR(40) NULL
    AFTER invalidated_at,

  MODIFY COLUMN max_attempts INT UNSIGNED NOT NULL DEFAULT 10,

  ADD UNIQUE KEY uq_otp_challenge_token_hash (challenge_token_hash),
  ADD KEY idx_otp_submission (submission_id),
  ADD KEY idx_otp_email_purpose_active_v2
    (email, purpose, invalidated_at, expires_at),
  ADD KEY idx_otp_issue_limit
    (email, purpose, issue_reason, created_at);
```

Yêu cầu:

- Existing rows được phép có null challenge/submission metadata.
- Không bulk update OTP đang hoạt động từ max 5 lên 10; default 10 áp dụng cho token mới.
- Dùng charset/collation/index naming đúng convention source thật.

### 22.2. `visit_requests`

```sql
ALTER TABLE visit_requests
  ADD COLUMN submission_id CHAR(36) NULL
    COMMENT 'UUID idempotency cho một submit intent'
    AFTER request_code,

  ADD COLUMN business_fingerprint CHAR(64) NULL
    COMMENT 'SHA-256 fingerprint v1 của core visit identity'
    AFTER submission_id,

  ADD UNIQUE KEY uq_visit_requests_submission_id (submission_id),

  ADD KEY idx_visit_requests_fingerprint_time_status
    (business_fingerprint, submitted_at, status);
```

Yêu cầu:

- Existing rows để NULL, không cần backfill toàn lịch sử.
- Không đặt fingerprint UNIQUE.
- Không thêm guard table.

## 23. SQL deliverables

- Incremental patch chạy trên database hiện có.
- Canonical fresh-create SQL đồng bộ.
- Entity + EF mapping đồng bộ.
- Cập nhật schema/dictionary docs nếu project duy trì.
- Không tự tạo migration nếu repository không dùng migration cho baseline này.
- Patch phải có thứ tự apply rõ ràng và không chạm seed ngoài phạm vi.

---

# PHẦN D — FRONTEND/UX

## 24. Hook/API state

Cập nhật tối thiểu:

```text
submissionId
otpSessionToken
remainingAttempts
retryAfterSeconds
humanVerificationRequired
isRecoveringOtp
duplicateResult
```

Yêu cầu:

- Attempt/retry lấy từ backend.
- Disable double-submit khi đang request.
- Giữ nguyên form và submitted snapshot khi OTP/CAPTCHA lỗi.
- Resend/recover thay session token cũ bằng token mới.
- Duplicate không set `otpError`.
- Reset tập trung không gây auto-save draft ngoài ý muốn.

## 25. OTP modal state machine

```text
OTP_ENTRY
HUMAN_VERIFICATION
RECOVERING
```

### OTP entry

- Hiển thị remaining attempts.
- Enforce retry countdown presentation theo server value.
- Disable verify khi chưa đủ 6 số/đang xử lý/chưa hết cooldown.

### Human verification

- Ẩn hoặc disable OTP cũ.
- Hiển thị Turnstile.
- Không cho normal resend để bypass.

### Recovering

- Loading, chống double-call.
- Success nhận token mới, reset OTP input và quay lại OTP entry.

## 26. Duplicate result

Hiển thị trong modal/result flow, không auto-close:

- Badge `Đã gửi trước đó` và EN tương ứng.
- Existing request code.
- Status map từ code.
- Submitted time.
- Thông báo không tạo request mới.
- Full submitted snapshot chỉ đọc bằng component hiện có hoặc variant phù hợp.

Không hiển thị duplicate như lỗi đỏ dưới OTP.

## 27. i18n/accessibility/mobile

- Thêm đầy đủ VI/EN.
- Không hardcode text mới.
- Không raw translation key.
- Dialog/ARIA/focus hợp lý.
- Turnstile và duplicate result không overflow ở 390×844.
- Không làm mất result summary không auto-close đã hoàn thành.

---

# PHẦN E — TEST

## 28. Nguyên tắc

- Unit Test không dùng DB.
- Integration Test dùng MySQL test database thật.
- Không chạm `pems_db`.
- Dùng Theory/parameterized test để tránh lặp.
- E2E mock OTP/Turnstile/email.
- Không sửa test chỉ để che implementation sai.

## 29. Unit Test OTP

Phủ tối thiểu:

1. Sai tăng attempt và remaining đúng.
2. Sai đến 9 chưa CAPTCHA.
3. Sai lần 10 yêu cầu CAPTCHA.
4. Đúng ở lần thử 10 thành công.
5. Cooldown boundary.
6. Expired/used/invalidated/session-invalid.
7. Raw challenge token được hash.
8. CAPTCHA fail không issue OTP.
9. CAPTCHA success invalid old + issue new.
10. Standard/recovery/absolute issue limit.

## 30. Unit Test fingerprint

1. Case/whitespace normalization cho cùng fingerprint.
2. Campus array order không ảnh hưởng.
3. Notes/purpose/guests/support không ảnh hưởng.
4. Mỗi core field thay đổi làm fingerprint khác.
5. Wall-clock UTC+7 không lệch ngày/giờ.

## 31. Integration Test OTP

1. Wrong verify trả lỗi nhưng `attempt_count` tăng thật sau request.
2. Sai 10 lần tạo human-required state.
3. Correct-attempt-10 thành công.
4. Concurrent attempts không lost update.
5. Fake CAPTCHA fail/success đúng side effect.
6. OTP cũ không dùng sau recovery.
7. OTP đúng nhưng create lỗi thì `used_at` rollback.
8. OTP đúng + create success commit atomic.
9. Rate-limit response/metadata đúng.

## 32. Integration Test idempotency/duplicate

Bắt buộc:

1. Cùng `submissionId`, retry tuần tự → HTTP 200 cùng request, tổng row = 1.
2. Cùng `submissionId`, concurrent retry → tối đa một request; loser re-query và nhận response idempotent.
3. Cùng `submissionId` nhưng fingerprint/email khác → `IDEMPOTENCY_KEY_REUSED`.
4. Different submissionId + same fingerprint sau khi request đầu commit, trong 15 phút → duplicate 409.
5. Khác campus/date/time/type/contact → được tạo.
6. Previous REJECTED/CANCELLED → được tạo.
7. Same fingerprint ngoài 15 phút → được tạo.
8. Duplicate không tạo child/account/notification/email mới.

Không viết acceptance test bắt buộc:

```text
Hai different submissionId + same fingerprint chạy đúng đồng thời luôn chỉ tạo một request
```

Vì phiên bản này không có guard/distributed lock và đã chấp nhận residual race đó.

## 33. Playwright

Mở rộng UC17 spec hiện có:

1. Wrong OTP hiển thị remaining.
2. Retry cooldown disable confirm.
3. Lần sai 10 chuyển human verification.
4. CAPTCHA fail giữ form.
5. CAPTCHA success nhận OTP mới.
6. OTP mới submit success, summary không auto-close.
7. Duplicate hiện result riêng, không phải OTP error.
8. Retry cùng submission ID không tạo UI result mới sai.
9. Mobile không overflow.

Mock `/initiate`, `/verify`, `/resend-otp`, `/otp/recover` và Turnstile callback; không gọi dịch vụ thật.

---

# PHẦN F — THỨ TỰ TRIỂN KHAI

## 34. Phase 1 — Baseline

- `git status`.
- Chạy build/test hiện trạng.
- Viết failing Integration Test cho attempt rollback.
- Ghi nhận false-positive duplicate cũ.

## 35. Phase 2 — SQL/domain

- Incremental patch hai bảng.
- Canonical SQL.
- Entity/mapping/index/docs.
- Không thêm table.

## 36. Phase 3 — OTP correctness

- Commit invalid attempt đúng.
- Row lock.
- Max attempts 10.
- Progressive cooldown.
- Test pass trước khi làm CAPTCHA.

## 37. Phase 4 — Challenge/Turnstile

- Opaque session token.
- Submission binding.
- Request metadata.
- Recover endpoint.
- Rate limits.
- Frontend state machine.

## 38. Phase 5 — Idempotency/fingerprint

- UUID lifecycle.
- Fingerprint builder.
- Idempotent replay logic + unique exception handling.
- Sequential duplicate query.
- Duplicate result UI.
- Không thêm concurrency guard.

## 39. Phase 6 — Full verification

- i18n parity.
- Unit tests.
- Integration tests.
- Targeted Playwright.
- Full existing suites.

---

# PHẦN G — VERIFICATION VÀ BÁO CÁO

## 40. Commands

Xác định đúng solution/package path rồi chạy tối thiểu:

```bash
dotnet test tests/PEMS.UnitTests
dotnet test tests/PEMS.IntegrationTests
```

Frontend:

```bash
npm run lint
npm run build
npx playwright test tests/visit-request-single-form.spec.ts
npx playwright test
```

Nếu command thật khác, dùng script đúng của repository và ghi rõ.

Không báo pass nếu chưa chạy, timeout hoặc bị skip không giải thích.

## 41. Báo cáo cuối

Phải ghi:

1. Current-state findings và root cause.
2. File thêm/sửa.
3. SQL patch/canonical changes; xác nhận không thêm bảng.
4. OTP transaction/concurrency behavior.
5. Turnstile/rate-limit behavior.
6. `submissionId` lifecycle và unique replay handling.
7. Fingerprint fields/normalization/excluded fields.
8. Duplicate window/status behavior.
9. Residual race do không có guard table.
10. Unit Test command + pass/total.
11. Integration Test command + pass/total.
12. Lint/build/Playwright command + kết quả thật.
13. Test chưa chạy được và lý do.
14. `git status --short` cuối, tách scope/unrelated changes.

Không nói “đã bảo đảm concurrent different submissionId same fingerprint chỉ tạo một row”.

---

# PHẦN H — DEFINITION OF DONE

- [ ] Wrong OTP trả lỗi nhưng attempt tăng thật trong MySQL.
- [ ] Correct-attempt-10 success; wrong-attempt-10 mới yêu cầu CAPTCHA.
- [ ] Server enforce cooldown.
- [ ] CAPTCHA tạo OTP/challenge mới, không mở OTP cũ.
- [ ] Không permanent lock email/account.
- [ ] Session token không phải email và raw token không persist/log.
- [ ] IP/User-Agent lấy phía server.
- [ ] Chỉ sửa `otp_tokens` và `visit_requests`; không thêm bảng.
- [ ] `submission_id` có UNIQUE index.
- [ ] Same submissionId retry trả cùng request, không tạo row thứ hai.
- [ ] Same submissionId + changed identity bị từ chối.
- [ ] `business_fingerprint` là normal index, không UNIQUE.
- [ ] Sequential same fingerprint trong 15 phút bị duplicate.
- [ ] Khác campus/date/time/type/contact được phép tạo.
- [ ] REJECTED/CANCELLED không chặn.
- [ ] Duplicate là result state, không phải OTP error, không auto-close.
- [ ] Submitted snapshot vẫn xem được.
- [ ] VI/EN đầy đủ.
- [ ] SQL/entity/mapping/API/frontend đồng bộ.
- [ ] Unit Tests pass.
- [ ] Integration Tests pass trên test DB.
- [ ] Lint/build pass.
- [ ] Targeted và full Playwright pass.
- [ ] Residual concurrency risk được ghi rõ, không bị che giấu.
- [ ] Không làm mất unrelated changes và không tự commit/push.

Nếu code/schema thực tế mâu thuẫn với prompt, không âm thầm bỏ qua. Nêu bằng chứng, ưu tiên nguồn chuẩn mới nhất, chọn thay đổi ít phá vỡ nhất vẫn giữ đúng các quyết định nghiệp vụ đã chốt và báo cáo rõ.
