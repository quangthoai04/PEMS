# Refactor Changelog

## 2026-06-20 — UC-17: tách registrant/contact, multi-campus, dọn ExcelUpload mồ côi

### Summary
Làm rõ nghiệp vụ UC-17: email người đăng ký form **chỉ để nhận OTP**; tài khoản VISITOR
được tạo/link từ **email đầu mối liên hệ**. Backend **đã đúng từ trước** (OTP verify theo
registrant email; `EnsureVisitorAccountAsync` dùng contact email; `registrant_email` +
`contact_person_json` + `visitor_user_id` lưu đúng) → **không sửa backend**. Thay đổi chỉ ở frontend.

### Frontend
- `sections/RegisterInfoSection.tsx` — note email người đăng ký nói rõ tài khoản theo dõi tạo theo email đầu mối liên hệ.
- `sections/ContactSection.tsx` — thêm note section + note dưới email (tạo tài khoản VISITOR, đăng nhập Google lần sau);
  đổi nhãn checkbox → "Tôi cũng là đầu mối liên hệ" + helper; auto-fill contact từ registrant giờ **reactive**
  (useEffect theo dõi registrant khi checkbox bật) thay vì one-shot.
- `schema/visitRequest.schema.ts` — `superRefine`: MULTI_CAMPUS cần ≥2 cơ sở (message rõ ràng, **không auto-downgrade**
  về một cơ sở), SINGLE_CAMPUS đúng 1, không trùng cơ sở.
- `sections/VisitInfoSection.tsx` — hiển thị lỗi mức `visits` (scope ↔ số cơ sở / trùng cơ sở).
- Xóa `components/ExcelUpload/ExcelUpload.tsx` — component **mồ côi** (không nơi import), import `validateExcelFile`
  không tồn tại → gây lỗi tsc. Giữ `excelValidator.ts` / `excelDownload.ts` (đang dùng).

### Không đổi
- Backend handler/DTO/flow UC-17, route `initiate/verify/resend-otp`, guest email vẫn required, không khôi phục passport/CCCD.

### Build/Test
- `cd frontend/pems-react && npm run build` (vite) → built OK.
- `npx tsc --noEmit`: file UC-17 (visit-request) **0 lỗi**; còn **22 lỗi pre-existing** ở các feature scaffold KHÔNG liên quan
  (18× sai độ sâu import `../../shared/api/httpClient` → phải là `../../../shared/...`; 3× adapter thiếu export type;
  1× GalleryManagement cast `unknown`→`Blob`). Ngoài phạm vi task này; khuyến nghị cleanup riêng.

## 2026-06-20 — UC-17 public form: UI fix + bỏ field identity không có trong SQL

### Summary
Fix UI form công khai "Đăng ký tham quan" (UC-17) và đồng bộ field với `pems_full(3).sql`:
bỏ cột "Số HC/CMND" (không tồn tại trong `visit_guest_members`) khỏi toàn bộ FE + DTO backend,
thêm note OTP cho email người đăng ký, bỏ icon check xanh đè lên dropdown, và sửa lỗi
validation thời gian làm lệch hàng input. Không đổi route, không đổi UC-17 flow, không thêm cột SQL.

### Backend (bắt buộc vì FE bỏ field)
- `PEMS.Application/Common/DTOs/VisitFormDtos.cs` — bỏ `PassportId` khỏi `VisitorDto`
  (visit_guest_members không có cột passport/identity).
- `PEMS.Application/Delegations/Commands/VisitRequestFormValidationRules.cs` — bỏ rule
  `PassportId NotEmpty` (thêm nhầm ở task hardening trước). `VisitRequestService.CreateAsync`
  vốn không insert passport → không đổi.

### Frontend — bỏ field "Số HC/CMND"
- `types/visitRequest.types.ts` (`VisitorEntry`), `schema/visitRequest.schema.ts` (`visitorSchema`),
  `hooks/useVisitRequestForm.ts` (`DEFAULT_VISITOR`), `api/visitRequestApi.ts` (`mapToPayload`),
  `components/sections/VisitorListSection.tsx` (cột bảng + `rowHasError` + default "Thêm khách"),
  `components/ExcelUpload/excelValidator.ts` (header bắt buộc + data map),
  `components/ExcelUpload/excelDownload.ts` (template tải mẫu). Payload `guestMembers` không còn
  `passportId`/identity field.

### Frontend — UI fix
- `components/shared/FormField.tsx` — thêm prop `showValidIcon` (mặc định true); Select/Combobox/DateTime
  truyền `showValidIcon={false}` để icon check xanh không đè chevron/clear.
- `components/sections/RegisterInfoSection.tsx` — Email có helper text note OTP (`subtitle`);
  dropdown "Quốc tịch" + "Đơn vị công tác" tắt icon check xanh.
- `components/sections/VisitInfoSection.tsx` — hàng thời gian dùng `items-start` + error slot cố định
  (`min-h-[20px]`) cho mỗi cột + label "Múi giờ" cho badge → lỗi thời gian không làm lệch/nhảy input.

### Build/Test
- `dotnet build backend/PEMS.Api/PEMS.Api.csproj` → 0 warning / 0 error.
- `cd frontend/pems-react && npm run build` (vite) → built OK.
- `npm run lint` (tsc) còn 1 lỗi pre-existing ở `ExcelUpload/ExcelUpload.tsx` (component mồ côi, không
  được import, import `validateExcelFile` không tồn tại) — không liên quan thay đổi này.

## 2026-06-20 — Dọn dẹp scaffold/stub chết sau khi UC-17 đồng bộ

### Summary
Xóa scaffold UC-17 cũ (`SubmitVisitRequest`) chỉ `throw NotImplementedException` và 2 behaviour stub rỗng,
sau khi UC-17 thật đã chạy theo flow `initiate → verify → resend-otp`. Không đụng UC-17 thật, không đổi route frontend.

### Removed (dead UC-17 scaffold — không được frontend/backend dùng)
- `backend/PEMS.Application/Delegations/Commands/SubmitVisitRequest/` (cả thư mục):
  `SubmitVisitRequestCommand.cs`, `SubmitVisitRequestCommandHandler.cs` (throw `NotImplementedException`),
  `SubmitVisitRequestCommandValidator.cs`, `SubmitVisitRequestResponse.cs`.
- `tests/PEMS.ApplicationTests/Delegations/SubmitVisitRequestCommandTests.cs` (skipped) +
  `SubmitVisitRequestCommandHandlerTests.cs` (rỗng). *(Thư mục PEMS.ApplicationTests không có .csproj nên không được compile.)*

### Removed (empty behaviour stubs — namespace sai `PEMS.Shared`, không implement `IPipelineBehavior`, không đăng ký DI)
- `backend/PEMS.Application/Common/Behaviours/TransactionBehaviour.cs`
- `backend/PEMS.Application/Common/Behaviours/IdempotencyBehaviour.cs`

### Changed
- `backend/PEMS.Api/Controllers/DelegationsController.cs` — bỏ route `POST /api/Delegations/submitvisitrequest`;
  thay bằng comment trỏ tới UC-17 thật trong `VisitRequestsController`.

### Notes
- **UC-17 transaction** vẫn được đặt tường minh trong `VerifyAndCreateVisitRequestCommandHandler`
  (`BeginTransactionAsync` → commit/rollback) — không phụ thuộc behaviour stub đã xóa.
- **IdempotencyKey** vẫn là future enhancement (hiện chống double-submit bằng OTP single-use + duplicate guard 10 phút).
- UC-17 thật không đổi: `POST /api/visit-requests/initiate | /verify | /resend-otp`.

### Build/Test
- `dotnet build backend/PEMS.Api/PEMS.Api.csproj` → 0 warning / 0 error.
- `PEMS.ArchitectureTests` → 14 passed.

## 2026-06-20 — UC-17 Submit Visit Request: sync với SQL v8.3 + hardening

### Summary
Audit + đồng bộ UC-17 (public visit request + OTP) theo `pems_full(3).sql`. Entity/config/DbContext đã khớp SQL;
bổ sung phần thiếu trong luồng tạo đơn: re-validate full form server-side ở bước tạo, kiểm tra campus tồn tại + ACTIVE,
bọc toàn bộ submit trong transaction (consume OTP → provision visitor → insert request/campuses/guests), thêm duplicate guard,
và machine-readable error codes. Giữ nguyên luồng 2-bước `initiate`/`verify` mà frontend đang chạy (không phá frontend).
Chi tiết: `docs/delegation/UC17_SUBMIT_VISIT_REQUEST_SYNC_REPORT.md`.

### Backend
- `PEMS.Application/Delegations/Commands/IVisitRequestFormCommand.cs` — MỚI: contract chung cho form Initiate + VerifyAndCreate.
- `PEMS.Application/Delegations/Commands/VisitRequestFormValidationRules.cs` — MỚI: rule FluentValidation dùng chung
  (required/max-length theo cột SQL, scope↔số campus: SINGLE=1/MULTI≥2, không trùng campus, end>start).
- `.../InitiateVisitRequest/*` + `.../VerifyAndCreateVisitRequest/*` — cả 2 command implement interface; cả 2 validator
  dùng rule chung ⇒ bước tạo đơn re-validate full form (không tin form đã hợp lệ ở bước OTP). Validator verify thêm rule OTP.
- `.../VerifyAndCreateVisitRequestCommandHandler.cs` — bọc transaction (`BeginTransactionAsync` → commit/rollback),
  duplicate guard (email+delegation+scope trong 10 phút, chưa rejected/cancelled → `DUPLICATE_VISIT_REQUEST` 409),
  gửi email xác nhận SAU commit.
- `PEMS.Infrastructure/Services/VisitRequestService.cs` — kiểm tra campus tồn tại (`CAMPUS_NOT_FOUND`) + ACTIVE
  (`CAMPUS_INACTIVE`), planned-time không ở quá khứ (`INVALID_VISIT_TIME`, grace 1 ngày), dùng hằng số trạng thái,
  map working_language VI/EN/OTHER; host fields để NULL (host assignment KHÔNG thuộc UC-17).
- `IApplicationDbContext` + `ApplicationDbContext` — thêm `BeginTransactionAsync`.
- `Common/Exceptions/ConflictException.cs` + `BusinessRuleException.cs` — thêm `ErrorCode` (optional);
  `ExceptionHandlingMiddleware` trả `errorCode` cho 409/422.
- `PEMS.Domain/Constants/VisitRequestConstants.cs` — thêm `VisitInstanceStatuses` + `VisitRequestErrorCodes`.

### Build/Test
- `dotnet build backend/PEMS.Api/PEMS.Api.csproj` → 0 warning / 0 error.
- `PEMS.ArchitectureTests` → 14 passed. Frontend không đổi nên không build.

## 2026-06-20 — Auth Hardening TODO Completion

### Summary
Hoàn thiện các TODO còn lại sau phase Auth Hardening: implement UC-97 revoke session khi
khóa/deactivate user, thêm backend HTML sanitizer (package `HtmlSanitizer`, namespace `Ganss.Xss`),
harden `FileValidationService` chặn upload SVG/HTML/JS/executable, và chuẩn hoá secrets/domain
production qua environment variables. KHÔNG đụng login/SSO/RBAC/session flow đang chạy.

### Backend
- `PEMS.Application/Accounts/Commands/ManageAccountStatus/*` — implement đầy đủ Command/Validator/Response/Handler
  (trước là scaffold `NotImplementedException`). Handler: scope check (Staff Leader theo campus, ADMIN/HO toàn hệ thống),
  chặn tự đổi status chính mình, update `users.status`, ghi `audit_logs`; khi rời trạng thái `ACTIVE`
  → `RevokeAllActiveSessionsAsync(userId, ACCOUNT_DEACTIVATED, actorId)` + `security_events` (`ACCOUNT_LOCKED`, severity HIGH).
  Reactivate (→ACTIVE) reset `failed_login_count` / `locked_until`.
- `PEMS.Application/Common/Security/IHtmlSanitizerService.cs` — MỚI (interface).
- `PEMS.Infrastructure/Security/HtmlSanitizerService.cs` — MỚI (impl: bỏ script/iframe/object/embed/form/style…,
  bỏ event handler `on*`, chỉ cho scheme http/https/mailto/tel).
- `PEMS.Application/Common/Interfaces/IFileValidationService.cs` — định nghĩa interface (trước rỗng), framework-agnostic.
- `PEMS.Infrastructure/FileStorage/FileValidationService.cs` — denylist extension + MIME
  (chặn `.svg/.svgz/.html/.htm/.js/.jsx/.ts/.php/.aspx/.jsp/.exe/.bat/.ps1/.sh/.jar…` và `image/svg+xml`,
  `text/html`, `application/javascript`…), kiểm tra size, không tin `ContentType` client.
- `PEMS.Infrastructure/DependencyInjection.cs` — đăng ký `IHtmlSanitizerService` + `IFileValidationService` (Singleton).
- `PEMS.Infrastructure.csproj` — thêm package `HtmlSanitizer` 9.0.892 (NuGet id là `HtmlSanitizer`, namespace `Ganss.Xss`).

### Config
- `frontend/pems-react/.env.example` — bổ sung mục production (API URL thật; KHÔNG để secret trong frontend env).
- `docs/database/DATABASE_DEPLOYMENT.md` — danh sách env var bắt buộc cho production (secrets dùng `__`),
  cảnh báo rotate SMTP app password đã commit, checklist production.
- Production secrets override qua environment variables / secret manager (env override mặc định của
  `WebApplication.CreateBuilder` — không cần đổi code). Production CORS chỉ allow domain frontend thật.

### Tests
- Backend build PASS: `dotnet build PEMS.Api -p:BaseOutputPath=./.tmp-build/` → 0 error, 0 warning.
- `tests/PEMS.ApplicationTests` KHÔNG có `.csproj` (không compile/chạy) → chưa thêm unit test tự động cho UC-97;
  test runtime/manual ghi trong `AUTH_HARDENING_TEST_CASES.md` (pending trên môi trường có DB).
- News create/edit handlers vẫn là scaffold (`NotImplementedException`) nên CHƯA wire `IHtmlSanitizerService`
  vào luồng lưu DB; service đã sẵn sàng để gọi khi News được implement.

## 2026-06-20 — Auth Hardening (post Core Auth Dual Portal)

### Summary
Harden các lớp còn lại của auth mà không phá login/SSO/RBAC đang chạy: chuẩn hoá error 500,
security headers + HSTS, XSS sanitize cho News, script DB cleanup, cấu hình production. Core auth
(hash refresh token, rotation, revoke-on-change, ẩn stacktrace prod, CORS config-based) đã có sẵn —
chỉ review, không sửa.

### Files Changed
**Backend**
- `PEMS.Application/Common/Security/AuthErrorCodes.cs` — thêm `InternalServerError`, `SessionRevoked`, `TokenExpired`, `Unauthorized`.
- `PEMS.Api/Middleware/ExceptionHandlingMiddleware.cs` — 500 dùng `INTERNAL_SERVER_ERROR` + message tiếng Việt.
- `PEMS.Api/Middleware/SecurityHeadersMiddleware.cs` — stub rỗng → middleware thật.
- `PEMS.Api/Program.cs` — đăng ký SecurityHeaders + `UseHsts()` non-Development.
- `PEMS.Api/appsettings.Production.json` — MỚI.

**Frontend**
- `src/shared/security/sanitizeHtml.ts` — MỚI.
- `src/pages/NewsDetailPage.tsx`, `src/pages/dashboard/news/NewsDetailDashboard.tsx` — sanitize HTML.
- `src/features/authentication/api/authError.ts` — map thêm 4 error code.

**Database**
- `database/scripts/patch_auth_hardening_sessions.sql` — MỚI (index idempotent).
- `database/scripts/cleanup_expired_user_sessions.sql` — MỚI.

**Docs**
- `docs/auth/AUTH_HARDENING_INVENTORY.md`, `AUTH_HARDENING_REPORT.md`, `AUTH_HARDENING_TEST_CASES.md`, `AUTH_SECURITY_CHECKLIST.md`, `AUTH_ERROR_CODES.md`.
- `docs/database/DATABASE_DEPLOYMENT.md`, `docs/architecture/REFACTOR_CHANGELOG.md`.

### Backend Changes
- Production-safe 500 contract (`INTERNAL_SERVER_ERROR`), giữ stacktrace chỉ ở Development.
- Security headers cho mọi response; CSP strict cho production non-Swagger; HSTS production.

### Frontend Changes
- `sanitizeHtml()` (DOMParser allowlist, không thêm dependency) áp dụng cho mọi render HTML News.
- Bổ sung message tiếng Việt cho error code session/internal.

### Database Changes
- 2 index phục vụ cleanup (idempotent). Script dọn session expired+revoked.

### Security Changes
- Ẩn chi tiết lỗi production; security headers + HSTS + CSP; XSS sanitize; CORS production khoá domain.

### Commands Run
```
dotnet build PEMS.Api/PEMS.Api.csproj -p:BaseOutputPath=<temp>   # 0 error, 0 warning
npm run build                                                     # ✓ built (chỉ warning chunk-size có sẵn)
```
> Lưu ý: build vào `bin` mặc định khi dev server đang chạy sẽ báo MSB3021 (file-lock) — đó KHÔNG phải lỗi biên dịch.

### Remaining TODOs
1. Implement revoke session trong `ManageAccountStatusCommandHandler` (UC-97) khi có spec.
2. Backend HTML sanitize cho News (Ganss.Xss + `IHtmlSanitizerService`).
3. Nâng `sanitizeHtml` → DOMPurify khi cài được package.
4. Xác nhận FileValidationService chặn SVG/HTML/JS upload.
5. Lên lịch chạy `cleanup_expired_user_sessions.sql`.
6. Override secrets production qua env/secret manager.
