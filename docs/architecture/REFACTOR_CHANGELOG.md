# Refactor Changelog

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
