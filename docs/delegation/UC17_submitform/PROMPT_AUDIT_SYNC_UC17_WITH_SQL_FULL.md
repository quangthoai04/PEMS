# PROMPT_AUDIT_AND_SYNC_UC17_SUBMIT_VISIT_REQUEST_WITH_SQL_FULL

## Mục tiêu

Kiểm tra toàn bộ logic hiện tại của **UC-17 Submit Visit Request** trong PEMS xem đã khớp với tài liệu UC-17 mới và file SQL chuẩn mới nhất `pems_full.sql` hay chưa.

Nếu chưa khớp, hãy cập nhật code để đồng bộ:

```text
UC-17 Submit Visit Request
→ Email verification bằng OTP
→ Không lưu form chưa xác minh vào database
→ Submit form chính thức sau khi OTP verified
→ Insert đúng bảng/cột/trạng thái theo pems_full.sql mới nhất
→ Không xử lý approve/cancel/host assignment trong UC-17
```

Nguồn sự thật bắt buộc:

```text
1. database/scripts/pems_full.sql hoặc file SQL full mới nhất trong repo
2. docs/use-cases hoặc file UC-17 Submit Visit Request mới nhất
3. Entity/DbContext/Configuration hiện tại
```

Nếu code khác SQL, ưu tiên **SQL full mới nhất** làm source of truth.

---

## 0. Phạm vi bắt buộc

### UC-17 bao gồm

```text
1. Visitor nhập public visit request form.
2. Frontend validate cơ bản.
3. Frontend lưu draft tạm trong sessionStorage.
4. Backend gửi OTP đến registrant email.
5. Visitor nhập OTP.
6. Backend verify OTP và trả verificationToken ngắn hạn.
7. Frontend submit full form + verificationToken + idempotencyKey.
8. Backend validate lại toàn bộ dữ liệu.
9. Backend tạo/link VISITOR user nếu cần.
10. Backend insert visit_requests.
11. Backend insert visit_request_campuses.
12. Backend insert visit_guest_members.
13. Backend insert visit_agendas / files/documents nếu form có.
14. Backend ghi status log/audit log nếu schema có.
15. Frontend clear sessionStorage draft sau khi submit thành công.
```

### UC-17 KHÔNG bao gồm

```text
- Approve request.
- Reject request.
- Cancel request.
- Assign host.
- Transfer host.
- Start visit.
- Complete visit.
- Logistics execution.
- Minutes.
- UC-136 Cancel Visit Request.
```

Nếu code hiện tại đang xử lý hủy đơn trong UC-17 thì phải tách ra khỏi UC-17 và đưa về UC-136 / Delegation Reception Management.

---

## 1. Source of truth từ UC-17 mới

### 1.1 Không dùng pending table

Không được tạo hoặc sử dụng:

```sql
pending_visit_requests
```

Rule đúng:

```text
- Draft form chưa xác minh chỉ nằm ở frontend sessionStorage.
- Backend chỉ lưu OTP metadata trong otp_tokens.
- Backend chỉ insert visit_requests sau khi OTP verified và submit official form.
```

### 1.2 Request status

`visit_requests.status` chỉ lưu trạng thái quyết định của request:

```text
PENDING_APPROVAL
APPROVED
REJECTED
CANCELLED
```

Không được lưu trong `visit_requests.status`:

```text
IN_PROGRESS
COMPLETED
BEFORE_VISIT
DURING_VISIT
AFTER_VISIT
CLOSED
```

Các trạng thái vận hành thuộc `visit_request_campuses.status`.

### 1.3 Campus status

`visit_request_campuses.status` lưu tiến độ theo từng campus:

```text
WAITING_REQUEST_APPROVAL
ASSIGNED
BEFORE_VISIT
DURING_VISIT
AFTER_VISIT
CLOSED
CANCELLED
```

Khi UC-17 submit thành công, mỗi campus được chọn phải insert với:

```text
status = WAITING_REQUEST_APPROVAL
current_host_user_id = NULL
host_assigned_by = NULL
host_assigned_at = NULL
host_assignment_source = NULL
```

### 1.4 Initial insert của visit_requests

Khi submit chính thức:

```text
visit_requests.status = PENDING_APPROVAL
visit_requests.email_verified_at = now
visit_requests.visitor_user_id = linked VISITOR user
visit_requests.registrant_nationality = form.registrantNationality
```

Không insert `IN_PROGRESS` hoặc `COMPLETED` vào `visit_requests.status`.

### 1.5 Host assignment không thuộc UC-17

UC-17 chỉ chuẩn bị records để approve flow xử lý sau.

Không được làm trong UC-17:

```text
- Không auto assign Staff Leader ngay lúc submit.
- Không set current_host_user_id khi submit.
- Không set campus status ASSIGNED khi submit.
- Không set host_assignment_source khi submit.
```

Host assignment chỉ xảy ra sau approval:

```text
MULTI_CAMPUS:
HO approves
→ auto-assign Staff Leader của từng campus
→ campus status = ASSIGNED
→ host_assignment_source = AUTO_STAFF_LEADER

SINGLE_CAMPUS:
Staff Leader approves
→ phải chọn host ngay
→ campus status = ASSIGNED
→ host_assignment_source = MANUAL_APPROVAL
```

---

## 2. Backend endpoints phải kiểm tra

UC-17 public endpoints đúng:

```http
POST /api/public/visit-requests/send-verification-code
POST /api/public/visit-requests/verify-code
POST /api/public/visit-requests/submit
```

Hãy kiểm tra controller hiện tại:

```text
PEMS.Api/Controllers/**
PublicVisitRequestsController
VisitRequestsController
AuthenticationController
OtpController
```

Nếu endpoint đang lệch route nhưng frontend đang dùng route cũ, cần quyết định:

```text
- Hoặc update route để khớp spec.
- Hoặc giữ backward-compatible route nhưng thêm route chuẩn.
```

Không phá frontend đang chạy nếu chưa có migration rõ ràng.

---

## 3. Backend flow cần audit

## 3.1 Send OTP

### Request

```json
{
  "email": "visitor@example.com"
}
```

### Logic bắt buộc

```text
[ ] Validate email format.
[ ] Normalize email = trim + lowercase.
[ ] Rate-limit theo IP.
[ ] Rate-limit theo email.
[ ] Enforce resend cooldown, recommend 60 seconds.
[ ] Generate cryptographically secure 6-digit OTP.
[ ] Hash OTP trước khi lưu DB.
[ ] Store OTP metadata trong otp_tokens.
[ ] purpose = VISIT_REQUEST_VERIFY hoặc constant tương ứng.
[ ] Không return OTP trong response.
[ ] Không log OTP plain text.
[ ] Không insert visit_requests ở bước này.
```

### Search keyword

```text
SendVerificationCode
SendVisitRequestVerificationCode
OtpToken
VISIT_REQUEST_VERIFY
GenerateOtp
HashOtp
RateLimit
```

---

## 3.2 Verify OTP

### Request

```json
{
  "email": "visitor@example.com",
  "code": "123456"
}
```

### Logic bắt buộc

```text
[ ] Normalize email = trim + lowercase.
[ ] Find latest unused OTP by email + purpose VISIT_REQUEST_VERIFY.
[ ] OTP chưa expired.
[ ] OTP chưa used.
[ ] Increment attempt_count khi sai code.
[ ] Reject nếu attempt_count >= max_attempts.
[ ] Mark used_at = now khi success.
[ ] Return signed short-lived verificationToken.
[ ] verificationToken chứa purpose VISIT_REQUEST_SUBMIT.
[ ] verificationToken chứa email đã verify.
[ ] verificationToken chứa otpTokenId hoặc jti.
[ ] verificationToken có expiry ngắn.
[ ] Không insert visit_requests ở bước này.
```

### verificationToken payload nên có

```json
{
  "purpose": "VISIT_REQUEST_SUBMIT",
  "email": "visitor@example.com",
  "otpTokenId": 123,
  "exp": 1710000600
}
```

### Search keyword

```text
VerifyCode
VerifyVisitRequestCode
verificationToken
VISIT_REQUEST_SUBMIT
OtpTokenId
used_at
attempt_count
```

---

## 3.3 Submit official form

### Request tối thiểu

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

### Logic bắt buộc

```text
[ ] Verify verificationToken signature.
[ ] Verify token purpose = VISIT_REQUEST_SUBMIT.
[ ] Verify token not expired.
[ ] Verify token email == registrantEmail normalized.
[ ] Validate all form fields again server-side.
[ ] Validate visitScope SINGLE_CAMPUS/MULTI_CAMPUS.
[ ] Validate campus count by scope.
[ ] Validate campus exists and ACTIVE.
[ ] Validate no duplicate campus.
[ ] Validate plannedEndAt > plannedStartAt.
[ ] Validate plannedStartAt not in the past.
[ ] Validate guestMembers.
[ ] Validate agenda if applicable.
[ ] Validate files if applicable.
[ ] Check idempotencyKey.
[ ] Check duplicate recent request.
[ ] Start transaction.
[ ] Create/link VISITOR user.
[ ] Insert visit_requests.
[ ] Insert visit_request_campuses.
[ ] Insert visit_guest_members.
[ ] Insert visit_agendas if applicable.
[ ] Insert files/documents if applicable.
[ ] Insert status log/audit log if schema has it.
[ ] Commit transaction.
[ ] Return request id/code/status.
```

### Không được làm ở submit

```text
[ ] Không approve request.
[ ] Không reject request.
[ ] Không cancel request.
[ ] Không assign host.
[ ] Không set campus status ASSIGNED.
[ ] Không set request status IN_PROGRESS/COMPLETED.
[ ] Không set actual_start_at/actual_end_at nếu SQL không còn dùng.
```

### Search keyword

```text
SubmitVisitRequest
SubmitVisitRequestCommand
SubmitVisitRequestCommandHandler
CreateVisitRequest
VisitRequestCampus
VisitGuestMember
VisitAgenda
idempotencyKey
DUPLICATE_VISIT_REQUEST
```

---

## 4. SQL full audit checklist

Mở file SQL full mới nhất:

```text
database/scripts/pems_full.sql
```

Hoặc file SQL full được cung cấp mới nhất, ví dụ:

```text
pems_full.sql
pems_full(2).sql
pems_full(3).sql
```

Chọn đúng bản chuẩn nhất và đồng bộ code theo bản đó.

## 4.1 Bảng phải kiểm tra

```text
visit_requests
visit_request_campuses
visit_guest_members
visit_agendas
users
otp_tokens
files
documents
audit_logs
security_events
login_logs
partners
partner_contacts
```

Chỉ dùng bảng thật sự tồn tại trong SQL full.

Nếu code insert vào bảng không còn tồn tại trong SQL, phải sửa code.

Nếu code thiếu insert vào bảng bắt buộc theo SQL/UC, phải bổ sung.

---

## 4.2 Kiểm tra visit_requests

Cần đối chiếu:

```text
[ ] Tên PK đúng: visit_request_id hay id?
[ ] Kiểu ID đúng: BIGINT/INT numeric auto increment, không còn Guid nếu SQL đã đổi.
[ ] Tên cột status đúng.
[ ] ENUM status đúng: PENDING_APPROVAL, APPROVED, REJECTED, CANCELLED.
[ ] Có visitor_user_id không?
[ ] Có registrant_email không?
[ ] Có registrant_full_name không?
[ ] Có registrant_organization không?
[ ] Có registrant_phone không?
[ ] Có registrant_nationality không?
[ ] Có email_verified_at không?
[ ] Có delegation_name không?
[ ] Có visit_scope không?
[ ] Có purpose không?
[ ] Có expected_guest_count không?
[ ] Có working_language không?
[ ] Có submitted_at/created_at không?
[ ] Có decided_by/decided_at/decision_note không?
[ ] Có row_version không?
[ ] Có cancellation metadata không?
```

Cập nhật entity/config/DTO/handler theo đúng tên cột SQL.

---

## 4.3 Kiểm tra visit_request_campuses

Cần đối chiếu:

```text
[ ] Tên PK đúng.
[ ] visit_request_id FK đúng.
[ ] campus_id FK đúng.
[ ] status ENUM đúng.
[ ] WAITING_REQUEST_APPROVAL tồn tại.
[ ] ASSIGNED tồn tại.
[ ] BEFORE_VISIT tồn tại.
[ ] DURING_VISIT tồn tại.
[ ] AFTER_VISIT tồn tại.
[ ] CLOSED tồn tại.
[ ] CANCELLED tồn tại.
[ ] current_host_user_id tồn tại hay tên khác?
[ ] host_assigned_by tồn tại hay tên khác?
[ ] host_assigned_at tồn tại hay tên khác?
[ ] host_assignment_source tồn tại hay tên khác?
[ ] planned_start_at/planned_end_at nằm ở bảng này hay visit_requests?
[ ] row_version có không?
[ ] cancellation metadata có không?
```

Initial insert UC-17 phải đúng:

```text
status = WAITING_REQUEST_APPROVAL
current_host_user_id = NULL
host_assigned_by = NULL
host_assigned_at = NULL
host_assignment_source = NULL
```

---

## 4.4 Kiểm tra visit_guest_members

Cần đối chiếu:

```text
[ ] Tên PK đúng.
[ ] visit_request_id FK đúng.
[ ] full_name/name đúng tên cột.
[ ] title/position đúng tên cột.
[ ] organization đúng tên cột.
[ ] email đúng tên cột.
[ ] phone đúng tên cột.
[ ] nationality đúng tên cột.
[ ] is_head_of_delegation/is_primary đúng tên cột.
[ ] sort_order/order_index đúng tên cột.
```

Handler không được insert field không có trong SQL.

---

## 4.5 Kiểm tra visit_agendas

Cần đối chiếu:

```text
[ ] Bảng visit_agendas có tồn tại không.
[ ] Tên PK đúng.
[ ] visit_request_id FK đúng.
[ ] campus_id có cần không.
[ ] title/activity đúng tên cột.
[ ] description đúng tên cột.
[ ] start_time/end_time hoặc planned_start_at/planned_end_at đúng tên cột.
[ ] sort_order đúng tên cột.
```

Nếu UC-17 form có agenda dự kiến, insert theo đúng schema.

Nếu form chưa có agenda, không fake insert agenda rỗng.

---

## 4.6 Kiểm tra otp_tokens

Cần đối chiếu:

```text
[ ] Tên PK đúng.
[ ] email/user_id có cột nào.
[ ] purpose có không.
[ ] token_hash/code_hash/otp_hash đúng tên cột.
[ ] expires_at có không.
[ ] used_at có không.
[ ] attempt_count có không.
[ ] max_attempts có không.
[ ] created_at có không.
[ ] ip_address/user_agent có không.
```

Không lưu OTP plain text.

---

## 4.7 Kiểm tra users / visitor user

Cần đối chiếu:

```text
[ ] users.status ENUM hiện tại là gì.
[ ] VISITOR role tồn tại như thế nào.
[ ] visitor_user_id trong visit_requests link users.user_id đúng.
[ ] created_via có giá trị phù hợp không.
[ ] password_hash nullable không.
[ ] primary_campus_id nullable cho VISITOR không.
[ ] department_id nullable cho VISITOR không.
```

Rule:

```text
- Nếu registrant email đã có VISITOR user ACTIVE, link user đó.
- Nếu chưa có user, tạo VISITOR user theo rule hiện tại.
- Không auto-create internal user.
- Nếu email thuộc internal account, không đổi role lung tung.
```

---

## 5. Entity / DTO / Mapping audit

## 5.1 Entity phải khớp SQL

Kiểm tra các entity:

```text
PEMS.Domain/Entities/VisitRequest.cs
PEMS.Domain/Entities/VisitRequestCampus.cs
PEMS.Domain/Entities/VisitGuestMember.cs
PEMS.Domain/Entities/VisitAgenda.cs
PEMS.Domain/Entities/OtpToken.cs
PEMS.Domain/Entities/User.cs
```

Checklist:

```text
[ ] Không còn Guid? Id nếu SQL dùng numeric ID.
[ ] Property name map đúng column name.
[ ] Enum/string status map đúng SQL.
[ ] Nullable khớp SQL.
[ ] MaxLength khớp SQL.
[ ] Required khớp SQL.
[ ] FK navigation đúng.
[ ] RowVersion/concurrency đúng nếu SQL có row_version.
```

## 5.2 EF Configuration phải khớp SQL

Kiểm tra:

```text
PEMS.Infrastructure/Persistence/Configurations/**
```

Checklist:

```text
[ ] ToTable đúng tên bảng.
[ ] HasKey đúng PK.
[ ] HasColumnName đúng.
[ ] HasColumnType đúng.
[ ] HasMaxLength đúng.
[ ] IsRequired đúng.
[ ] HasConversion đúng nếu enum.
[ ] HasOne/WithMany FK đúng.
[ ] Index/unique key không mâu thuẫn SQL.
```

## 5.3 DbContext

Kiểm tra:

```text
PEMS.Infrastructure/Persistence/PemsDbContext.cs
```

Checklist:

```text
[ ] Có DbSet<VisitRequest>.
[ ] Có DbSet<VisitRequestCampus>.
[ ] Có DbSet<VisitGuestMember>.
[ ] Có DbSet<VisitAgenda> nếu bảng tồn tại.
[ ] Có DbSet<OtpToken>.
[ ] ApplyConfigurationsFromAssembly đầy đủ.
[ ] Không còn DbSet cho bảng đã bỏ như pending_visit_requests.
```

---

## 6. Frontend audit checklist

Kiểm tra public submit flow:

```text
frontend/pems-react/src/**
```

Search:

```text
visitRequestDraft
pems.visitRequestDraft
send-verification-code
verify-code
verificationToken
idempotencyKey
SubmitVisitRequest
VisitRequestForm
```

## 6.1 Draft sessionStorage

Frontend phải:

```text
[ ] Lưu draft vào sessionStorage trước khi gửi OTP.
[ ] Key: pems.visitRequestDraft hoặc key thống nhất.
[ ] Draft có version.
[ ] Draft có idempotencyKey.
[ ] Draft có createdAt/expiresAt.
[ ] Draft expire sau khoảng 30 phút.
[ ] Không lưu File object vào sessionStorage.
[ ] Nếu reload page, yêu cầu chọn lại file.
[ ] Clear draft sau submit thành công.
```

## 6.2 Frontend request flow

```text
[ ] Step 1: fill form.
[ ] Step 2: validate basic fields.
[ ] Step 3: send OTP.
[ ] Step 4: enter OTP.
[ ] Step 5: verify OTP nhận verificationToken.
[ ] Step 6: submit full form + verificationToken + idempotencyKey.
[ ] Step 7: show success request code/id.
```

## 6.3 Không hiển thị sai status

Frontend phải phân biệt:

```text
visit_requests.status = trạng thái quyết định request
visit_request_campuses.status = trạng thái vận hành theo campus
```

Không hiển thị `IN_PROGRESS/COMPLETED` từ `visit_requests.status`.

---

## 7. Validation rules cần đảm bảo

## 7.1 Registrant

```text
[ ] registrantFullName required, max 150.
[ ] registrantOrganization required, max 200.
[ ] registrantEmail required, email format.
[ ] registrantEmail phải match verificationToken email.
[ ] registrantPhone optional, max 50.
[ ] registrantNationality optional, max 100.
```

## 7.2 Request

```text
[ ] delegationName required, max 200.
[ ] visitScope required: SINGLE_CAMPUS hoặc MULTI_CAMPUS.
[ ] purpose required.
[ ] expectedGuestCount required, >= 1.
[ ] workingLanguage: VI, EN hoặc OTHER.
```

## 7.3 Campus selection

```text
[ ] SINGLE_CAMPUS: exactly 1 campus.
[ ] MULTI_CAMPUS: at least 2 campuses.
[ ] Mỗi campus phải tồn tại.
[ ] Mỗi campus phải ACTIVE.
[ ] Không duplicate campus.
[ ] plannedEndAt > plannedStartAt.
[ ] plannedStartAt không ở quá khứ.
```

## 7.4 Duplicate detection

Reject duplicate nếu gần đây có request giống:

```text
same registrant_email
same delegation_name
same visit_scope
same campus set
same first planned_start_at
submitted within last 10 minutes
status not in REJECTED/CANCELLED
```

Return:

```text
409 DUPLICATE_VISIT_REQUEST
```

---

## 8. Error codes cần khớp

Cần kiểm tra backend/frontend error mapping có các code:

```text
VALIDATION_ERROR
RATE_LIMITED
OTP_RESEND_TOO_SOON
OTP_SEND_LIMIT_REACHED
OTP_INVALID
OTP_EXPIRED
OTP_ATTEMPT_LIMIT_EXCEEDED
VERIFICATION_TOKEN_INVALID
VERIFICATION_TOKEN_EXPIRED
EMAIL_MISMATCH
DUPLICATE_VISIT_REQUEST
CAMPUS_NOT_FOUND
CAMPUS_INACTIVE
INVALID_VISIT_SCOPE
INVALID_VISIT_TIME
FILE_REQUIRED_AGAIN
```

Nếu backend dùng tên khác, cần quyết định:

```text
- Hoặc cập nhật về đúng code trong UC-17.
- Hoặc map backward-compatible nhưng docs phải ghi rõ.
```

Frontend phải hiển thị message tiếng Việt phù hợp.

---

## 9. Insert transaction bắt buộc

Submit official form phải dùng transaction.

Pseudo flow:

```csharp
await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);

try
{
    // 1. Verify verificationToken
    // 2. Validate duplicate/idempotency
    // 3. Create/link visitor user
    // 4. Insert visit_request with PENDING_APPROVAL
    // 5. Insert visit_request_campuses with WAITING_REQUEST_APPROVAL
    // 6. Insert visit_guest_members
    // 7. Insert visit_agendas/files if applicable
    // 8. Insert audit/status log if applicable

    await _db.SaveChangesAsync(cancellationToken);
    await transaction.CommitAsync(cancellationToken);
}
catch
{
    await transaction.RollbackAsync(cancellationToken);
    throw;
}
```

Không được save nửa chừng làm DB lệch dữ liệu.

---

## 10. Insert value checklist

## 10.1 visit_requests insert

Phải set đúng:

```text
[ ] status = PENDING_APPROVAL
[ ] visitor_user_id = linked VISITOR user
[ ] registrant_full_name
[ ] registrant_organization
[ ] registrant_email normalized
[ ] registrant_phone
[ ] registrant_nationality
[ ] email_verified_at = now
[ ] delegation_name
[ ] visit_scope
[ ] purpose
[ ] expected_guest_count
[ ] working_language
[ ] submitted_at/created_at nếu cột có
[ ] row_version default nếu cột có
```

Không set:

```text
[ ] Không set status = APPROVED.
[ ] Không set status = IN_PROGRESS.
[ ] Không set status = COMPLETED.
[ ] Không set decided_by/decided_at khi submit.
[ ] Không set cancelled_by/cancelled_at khi submit.
```

## 10.2 visit_request_campuses insert

Phải set đúng:

```text
[ ] visit_request_id
[ ] campus_id
[ ] status = WAITING_REQUEST_APPROVAL
[ ] planned_start_at
[ ] planned_end_at
[ ] current_host_user_id = NULL
[ ] host_assigned_by = NULL
[ ] host_assigned_at = NULL
[ ] host_assignment_source = NULL
[ ] row_version default nếu cột có
```

Không set:

```text
[ ] Không set status = ASSIGNED.
[ ] Không set current_host_user_id khi submit.
[ ] Không set host_assignment_source khi submit.
```

## 10.3 visit_guest_members insert

Phải set đúng theo SQL:

```text
[ ] visit_request_id
[ ] full_name/name
[ ] title/position
[ ] organization
[ ] email
[ ] phone
[ ] nationality
[ ] is_head_of_delegation/is_primary
[ ] sort_order/order_index
```

## 10.4 visit_agendas insert nếu có

```text
[ ] visit_request_id
[ ] campus_id nếu schema yêu cầu
[ ] title/activity
[ ] description
[ ] planned_start_at/start_time
[ ] planned_end_at/end_time
[ ] sort_order/order_index
```

---

## 11. Code update rules

Khi phát hiện mismatch:

### 11.1 Nếu DTO lệch SQL

```text
- Cập nhật DTO/request model.
- Không giữ field cũ nếu SQL đã bỏ và không còn dùng.
- Nếu cần backward-compatible với frontend cũ, map field cũ sang field mới rõ ràng.
```

### 11.2 Nếu Entity lệch SQL

```text
- Cập nhật entity property type/name.
- Cập nhật EF configuration.
- Cập nhật DbContext.
- Không sửa SQL nếu SQL full là chuẩn mới nhất.
```

### 11.3 Nếu Handler insert sai

```text
- Sửa handler insert đúng bảng/cột/status.
- Đảm bảo transaction.
- Đảm bảo không insert host assignment ở UC-17.
- Đảm bảo không insert trạng thái vận hành vào visit_requests.status.
```

### 11.4 Nếu Frontend flow sai

```text
- Sửa flow OTP trước submit.
- Sửa sessionStorage draft.
- Sửa request payload.
- Sửa status display.
- Không lưu form chưa verify vào backend.
```

### 11.5 Nếu docs lệch code sau khi sửa

```text
- Cập nhật docs để mô tả đúng code mới.
- Không để docs nói một kiểu, code chạy một kiểu.
```

---

## 12. Files có khả năng cần sửa

### Backend Application

```text
backend/PEMS.Application/Features/VisitRequests/**
backend/PEMS.Application/VisitRequests/**
backend/PEMS.Application/Delegations/**
backend/PEMS.Application/Common/Security/**
backend/PEMS.Application/Common/Interfaces/**
```

### Backend Api

```text
backend/PEMS.Api/Controllers/PublicVisitRequestsController.cs
backend/PEMS.Api/Controllers/VisitRequestsController.cs
backend/PEMS.Api/Middleware/**
```

### Backend Domain

```text
backend/PEMS.Domain/Entities/VisitRequest.cs
backend/PEMS.Domain/Entities/VisitRequestCampus.cs
backend/PEMS.Domain/Entities/VisitGuestMember.cs
backend/PEMS.Domain/Entities/VisitAgenda.cs
backend/PEMS.Domain/Entities/OtpToken.cs
backend/PEMS.Domain/Enums/**
```

### Backend Infrastructure

```text
backend/PEMS.Infrastructure/Persistence/PemsDbContext.cs
backend/PEMS.Infrastructure/Persistence/Configurations/**
backend/PEMS.Infrastructure/Services/**
```

### Frontend

```text
frontend/pems-react/src/pages/**
frontend/pems-react/src/features/visit-requests/**
frontend/pems-react/src/features/public/**
frontend/pems-react/src/shared/api/**
frontend/pems-react/src/shared/security/**
```

### Database/docs

```text
database/scripts/pems_full.sql
docs/use-cases/**
docs/architecture/REFACTOR_CHANGELOG.md
docs/database/DATABASE_SCHEMA.md
docs/database/DATABASE_DEPLOYMENT.md
```

---

## 13. Build / test commands

### Backend

```bash
dotnet restore
dotnet build backend/PEMS.Api/PEMS.Api.csproj
```

Nếu dev server khóa `bin`:

```bash
dotnet build backend/PEMS.Api/PEMS.Api.csproj -p:BaseOutputPath=./.tmp-build/
```

Nếu có test project:

```bash
dotnet test
```

Nếu test project không tồn tại hoặc không compile, ghi rõ trong report.

### Frontend

Chạy nếu có sửa frontend:

```bash
cd frontend/pems-react
npm install
npm run build
```

### Database

Không tự ý migrate/seed bằng code nếu project đang theo database-first.

Chỉ kiểm tra schema:

```bash
mysql -u <user> -p <database> < database/scripts/pems_full.sql
```

Hoặc chạy trên database test/staging riêng.

---

## 14. Manual runtime test checklist

## 14.1 OTP flow

```text
[ ] Send OTP với email hợp lệ → success generic.
[ ] OTP không xuất hiện trong response.
[ ] OTP không xuất hiện trong log.
[ ] otp_tokens lưu hash, không lưu plain code.
[ ] Resend quá nhanh → OTP_RESEND_TOO_SOON.
[ ] Gửi quá limit → OTP_SEND_LIMIT_REACHED hoặc RATE_LIMITED.
[ ] Verify sai OTP → OTP_INVALID + attempt_count tăng.
[ ] Verify quá số lần → OTP_ATTEMPT_LIMIT_EXCEEDED.
[ ] Verify expired OTP → OTP_EXPIRED.
[ ] Verify đúng OTP → trả verificationToken.
```

## 14.2 Submit flow

```text
[ ] Submit thiếu verificationToken → VERIFICATION_TOKEN_INVALID.
[ ] Submit token expired → VERIFICATION_TOKEN_EXPIRED.
[ ] Submit email khác email verified → EMAIL_MISMATCH.
[ ] Submit SINGLE_CAMPUS với 0 campus → validation error.
[ ] Submit SINGLE_CAMPUS với 2 campus → validation error.
[ ] Submit MULTI_CAMPUS với 1 campus → validation error.
[ ] Submit campus inactive → CAMPUS_INACTIVE.
[ ] Submit plannedEndAt <= plannedStartAt → INVALID_VISIT_TIME.
[ ] Submit valid form → tạo visit_requests PENDING_APPROVAL.
[ ] Submit valid form → tạo visit_request_campuses WAITING_REQUEST_APPROVAL.
[ ] current_host_user_id NULL sau submit.
[ ] Duplicate submit trong 10 phút → DUPLICATE_VISIT_REQUEST.
[ ] idempotencyKey retry không tạo bản ghi duplicate.
```

## 14.3 Database verification SQL

```sql
SELECT visit_request_id, status, visitor_user_id, registrant_email, email_verified_at
FROM visit_requests
ORDER BY visit_request_id DESC
LIMIT 5;
```

Kỳ vọng:

```text
status = PENDING_APPROVAL
email_verified_at IS NOT NULL
visitor_user_id IS NOT NULL
```

```sql
SELECT visit_request_campus_id, visit_request_id, campus_id, status,
       current_host_user_id, host_assigned_by, host_assigned_at, host_assignment_source
FROM visit_request_campuses
WHERE visit_request_id = <ID>;
```

Kỳ vọng:

```text
status = WAITING_REQUEST_APPROVAL
current_host_user_id IS NULL
host_assigned_by IS NULL
host_assigned_at IS NULL
host_assignment_source IS NULL
```

---

## 15. Output report bắt buộc sau khi làm

Sau khi audit/sửa xong, trả report theo format:

```md
# UC-17 Submit Visit Request Sync Report

## Summary
- SQL source of truth:
- UC-17 doc source:
- Result: MATCHED / UPDATED / PARTIAL / BLOCKED

## Mismatches Found
| Area | Current | Expected | Action |
|---|---|---|---|
| visit_requests.status | ... | PENDING_APPROVAL only at submit | Fixed |
| visit_request_campuses.status | ... | WAITING_REQUEST_APPROVAL at submit | Fixed |

## Files Changed

### Backend
- ...

### Frontend
- ...

### Database
- ...

### Docs
- ...

## Insert Rules Verified
- [ ] visit_requests.status = PENDING_APPROVAL
- [ ] visit_request_campuses.status = WAITING_REQUEST_APPROVAL
- [ ] current_host_user_id = NULL before approval
- [ ] No pending_visit_requests table
- [ ] No unverified form stored in DB
- [ ] No cancel logic in UC-17

## Commands Run
```bash
dotnet build ...
npm run build
```

## Runtime Tests
- ...

## Remaining TODO / Risks
- ...
```

---

## 16. Definition of Done

Chỉ coi là hoàn thành khi:

```text
[ ] Code UC-17 khớp SQL full mới nhất.
[ ] Không còn insert vào bảng/cột đã bị bỏ.
[ ] Không còn dùng pending_visit_requests.
[ ] Không lưu form chưa verify vào database.
[ ] OTP lưu hash, không lưu plain text.
[ ] Submit chỉ chạy sau verify OTP.
[ ] visit_requests.status khi submit = PENDING_APPROVAL.
[ ] visit_request_campuses.status khi submit = WAITING_REQUEST_APPROVAL.
[ ] current_host_user_id null trước approval.
[ ] Host assignment không nằm trong UC-17.
[ ] Cancel logic không nằm trong UC-17.
[ ] Duplicate/idempotency hoạt động.
[ ] Backend build pass.
[ ] Frontend build pass nếu có sửa frontend.
[ ] Runtime test pass trên DB test/staging.
[ ] Docs/changelog/schema docs cập nhật đúng.
```

---

## 17. Kết luận

Nhiệm vụ này là audit + đồng bộ UC-17 theo SQL full mới nhất.

Không mở rộng sang các luồng khác.

Trục đúng của UC-17 là:

```text
Draft frontend only
→ Send OTP
→ Verify OTP
→ Submit official form
→ Insert visit_requests PENDING_APPROVAL
→ Insert visit_request_campuses WAITING_REQUEST_APPROVAL
→ Wait for approval flow
```

Mọi phần sau submit như approve/reject/cancel/assign host/logistics/minutes phải nằm ở UC khác.
