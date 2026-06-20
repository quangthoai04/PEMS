# Auth Hardening Report

> Ngày: 2026-06-20. Phase: hardening còn lại sau Core Auth Dual Portal.
> Nguyên tắc: KHÔNG làm lại login/SSO/RBAC; chỉ harden, không phá luồng đang chạy.

## 1. Summary

Core authentication của PEMS (hash refresh token, rotation, revoke-on-logout, revoke khi đổi
role/password, ẩn stacktrace ở production, CORS theo config, HTTPS redirect, session validation
realtime) **đã có sẵn và đạt chuẩn**. Phase này bổ sung các lớp hardening còn thiếu:

- Chuẩn hoá error code 500 thành `INTERNAL_SERVER_ERROR` + message tiếng Việt generic.
- Triển khai `SecurityHeadersMiddleware` (trước đây là file rỗng) + bật HSTS ở production.
- Sanitize HTML render-time cho News (chống XSS qua `dangerouslySetInnerHTML`).
- Script DB: index cleanup + script dọn session hết hạn/revoked (idempotent, không destructive).
- Cấu hình production: `appsettings.Production.json` (CORS domain thật, HTTPS hosts, log level).
- Đồng bộ tài liệu + checklist + test cases + error codes.

Không thay đổi nền tảng: PEMS vẫn là JWT + DB-backed session, Dual Portal, SSO-first.

## 2. Scope

### Included
- Production error handling (errorCode + message generic).
- Security headers + HSTS.
- XSS sanitize cho News HTML (frontend render-time).
- DB index/cleanup scripts cho `user_sessions`.
- Production CORS/HTTPS config.
- Docs/checklist/test cases/error codes sync.

### Excluded (giữ nguyên / không động)
- Core login credentials, Google SSO flow.
- Dual Portal policy, RBAC route/menu.
- FEID UI (vẫn tắt vì chưa có provider thật).
- Refresh hash/rotation (đã có — chỉ review, không sửa).
- AuthResponse success contract (frontend đang phụ thuộc).

## 3. Files changed

**Backend**
- `PEMS.Application/Common/Security/AuthErrorCodes.cs` — thêm `InternalServerError`, `SessionRevoked`, `TokenExpired`, `Unauthorized`.
- `PEMS.Api/Middleware/ExceptionHandlingMiddleware.cs` — 500 dùng `AuthErrorCodes.InternalServerError` + message tiếng Việt; vẫn ẩn stacktrace ở prod.
- `PEMS.Api/Middleware/SecurityHeadersMiddleware.cs` — thay stub rỗng bằng middleware thật (X-Content-Type-Options, X-Frame-Options, Referrer-Policy, Permissions-Policy, CSP cho prod non-Swagger).
- `PEMS.Api/Program.cs` — đăng ký `SecurityHeadersMiddleware`; `UseHsts()` khi không phải Development.
- `PEMS.Api/appsettings.Production.json` — MỚI: CORS domain thật, AllowedHosts, log level.

**Frontend**
- `src/shared/security/sanitizeHtml.ts` — MỚI: util sanitize HTML không phụ thuộc package.
- `src/pages/NewsDetailPage.tsx` — render qua `sanitizeHtml(...)`.
- `src/pages/dashboard/news/NewsDetailDashboard.tsx` — render qua `sanitizeHtml(...)`.
- `src/features/authentication/api/authError.ts` — map thêm `INTERNAL_SERVER_ERROR`, `SESSION_REVOKED`, `TOKEN_EXPIRED`, `UNAUTHORIZED`.

**Database**
- `database/scripts/patch_auth_hardening_sessions.sql` — MỚI: index `expires_at`/`revoked_at` (idempotent).
- `database/scripts/cleanup_expired_user_sessions.sql` — MỚI: dọn session expired+revoked.

**Docs**
- `docs/auth/AUTH_HARDENING_INVENTORY.md`, `AUTH_HARDENING_REPORT.md`, `AUTH_HARDENING_TEST_CASES.md`, `AUTH_SECURITY_CHECKLIST.md`, `AUTH_ERROR_CODES.md` (update).
- `docs/database/DATABASE_DEPLOYMENT.md` (update/append).
- `docs/architecture/REFACTOR_CHANGELOG.md` (MỚI).

## 4. Production error handling

- `ExceptionHandlingMiddleware` inject `IHostEnvironment`.
- Development: 500 trả thêm `error` + `stackTrace` để debug.
- Production/Staging: 500 chỉ trả `{ success:false, errorCode:"INTERNAL_SERVER_ERROR", message:"Đã xảy ra lỗi hệ thống. Vui lòng thử lại sau.", traceId }`.
- KHÔNG lộ: exception class, SQL, connection string, file path, token, secret.
- `AuthBusinessException` giữ nguyên `errorCode`/`statusCode` (403 cho wrong portal/campus mismatch, không dùng 401).
- Server vẫn log đầy đủ qua `_logger.LogError(ex, ...)` kèm traceId; không log token/password.

## 5. Refresh token / session changes

**Không thay đổi code** — chỉ review và xác nhận đạt chuẩn:
- Refresh token: random 48 bytes, lưu **SHA-256 hash** (`SecureTokenGenerator`).
- Rotation: mỗi lần refresh sinh token mới, update hash + expiry trên session.
- Refresh flow check: session tồn tại + chưa revoked + chưa expired + user ACTIVE + role ACTIVE/chưa xóa; nếu fail → revoke session + 401.
- Raw refresh token chỉ trả 1 lần khi login/refresh.

## 6. Revoke session rules (hiện trạng + xác nhận)

| Sự kiện | Handler | Revoke | Reason |
|---|---|---|---|
| Logout | `LogoutCommandHandler` | session hiện tại (+ session theo refresh token) | `USER_LOGOUT` |
| Đổi role | `UpdateAccountRoleCommandHandler` | tất cả session | `ROLE_CHANGED` |
| Reset password | `ResetPasswordCommandHandler` | tất cả session | `PASSWORD_RESET` |
| Đổi password | `ChangePasswordCommandHandler` | mọi session KHÁC session hiện tại | `PASSWORD_CHANGED` |
| Khóa/deactivate | `ManageAccountStatusCommandHandler` | tất cả session (khi `ACTIVE` → non-ACTIVE) | `ACCOUNT_DEACTIVATED` |

`SessionValidationMiddleware` vẫn chặn realtime user/role non-ACTIVE ở request kế tiếp; revoke session ở UC-97
là lớp bổ sung để DB phản ánh đúng trạng thái bảo mật và refresh token cũ không dùng lại được.

## 7. CORS / HTTPS / cookie / CSRF

- **CORS**: policy `PemsFrontend` đọc `Cors:AllowedOrigins` từ config. Dev: localhost. Prod: `appsettings.Production.json` chỉ allow `https://pems.fpt.edu.vn` (cập nhật domain thật khi deploy). KHÔNG `AllowAnyOrigin`, KHÔNG `AllowCredentials`.
- **HTTPS**: `UseHttpsRedirection()` luôn bật; `UseHsts()` chỉ ở non-Development.
- **Cookie/CSRF**: KHÔNG dùng cookie. Token lưu localStorage, gửi Bearer; refresh token qua body. Vì không có cookie tự gửi cho state-changing request nên **không cần CSRF token**. Rủi ro chính là XSS → đã hardening ở Phase 10. (Xem mục Risks.)

## 8. XSS hardening

- Tạo `sanitizeHtml()` (DOMParser allowlist, không thêm dependency): xóa `script/iframe/object/embed/style/...`, xóa attribute `on*`, lọc URL `javascript:/vbscript:/data:text\/html`.
- Áp dụng cho 2 chỗ render News HTML (`NewsDetailPage`, `NewsDetailDashboard`).
- Backend sanitize (`IHtmlSanitizerService` / package `HtmlSanitizer`, namespace `Ganss.Xss`): **đã thêm service + DI**.
  Wiring vào News create/edit còn chờ vì handlers News hiện vẫn là scaffold (xem mục Update + Risks/TODO).

## 14. Update — UC-97 Account Status Session Revoke

- Implement đầy đủ `ManageAccountStatusCommandHandler` (UC-97), trước đây là scaffold `NotImplementedException`.
- Command nhận `UserId`, `Status` (`ACTIVE|INACTIVE|LOCKED`, khớp `users.status` ENUM), `Reason?`.
- Scope: ADMIN/HO toàn hệ thống; Staff Leader chỉ trong campus của mình; chặn actor tự đổi status chính mình.
- Khi `ACTIVE` → non-ACTIVE: gọi `RevokeAllActiveSessionsAsync(userId, ACCOUNT_DEACTIVATED, actorId)` +
  ghi `security_events` (`ACCOUNT_LOCKED`, severity HIGH). Reactivate (→ACTIVE) reset `failed_login_count`/`locked_until`.
- Ghi `audit_logs` (action `MANAGE_ACCOUNT_STATUS`, old/new status) cho mọi thay đổi.
- Access token cũ → 401 (SessionValidationMiddleware), refresh token cũ → 401 (session đã revoked).
- Không đổi flow đổi role / reset password / change password đang chạy.

## 15. Update — Backend News HTML Sanitization

- Thêm package `HtmlSanitizer` (namespace `Ganss.Xss`) vào `PEMS.Infrastructure`.
- Thêm `IHtmlSanitizerService` (Application) + `HtmlSanitizerService` (Infrastructure), đăng ký Singleton.
- Cấu hình: bỏ `script/iframe/object/embed/form/input/button/style/...`, bỏ event handler `on*`,
  chỉ cho scheme `http/https/mailto/tel` (chặn `javascript:`, `data:text/html`, ...).
- Frontend render-time sanitize giữ nguyên (lớp thứ 2).
- **Lưu ý**: News create/edit handlers (`AddMultilingualNews`, `EditNews`) hiện là scaffold
  `NotImplementedException` — chưa có luồng lưu DB để gọi sanitize. Service đã sẵn sàng; sẽ inject
  `_htmlSanitizer.Sanitize(...)` khi News được implement. ⇒ Phần này là **PARTIAL**.

## 16. Update — Upload File Guard

- `IFileValidationService` + `FileValidationService` (denylist extension + MIME), đăng ký Singleton.
- Chặn `.svg/.svgz/.html/.htm/.xhtml/.js/.mjs/.jsx/.ts/.tsx/.vbs/.php/.asp/.aspx/.jsp/.exe/.bat/.cmd/.ps1/.sh/.jar...`
  và MIME `image/svg+xml`, `text/html`, `application/xhtml+xml`, `application/javascript`, `text/javascript`,
  `application/x-msdownload`, `application/x-sh`, ...; kiểm tra size; không tin `ContentType` từ client.
- Interface framework-agnostic (nhận `fileName`, `contentType`, `sizeBytes`) vì `PEMS.Application` không tham chiếu ASP.NET.
- **Lưu ý**: hiện chưa có upload endpoint/handler nào gọi service (các FileStorage service khác vẫn là scaffold rỗng).
  Service đã sẵn sàng để gọi từ upload flow khi Gallery/News/Documents được implement.

## 9. Database changes / SQL patch

- `patch_auth_hardening_sessions.sql`: thêm `idx_sessions_expires_at`, `idx_sessions_revoked_at` (idempotent qua `information_schema.statistics`). Các index refresh/user-active/refresh-hash đã có sẵn trong `pems_full.sql`.
- `cleanup_expired_user_sessions.sql`: `DELETE` session `expires_at < now AND revoked_at IS NOT NULL`. Không đụng `login_logs`/`security_events`.
- Không auto-migrate; chạy thủ công/scheduled.

## 10. Manual test results

Xem `AUTH_HARDENING_TEST_CASES.md`. Các case build/biên dịch đã verify; các case runtime (đăng nhập thật, refresh, revoke) cần chạy trên môi trường có DB — đánh dấu trong file test cases.

## 11. Build/test result

- Backend: `dotnet build PEMS.Api` — compile PASS (xem changelog mục Commands Run). Lưu ý: khi dev server đang chạy, build vào `bin` mặc định sẽ báo lỗi file-lock (MSB3021) — đó KHÔNG phải lỗi biên dịch; verify bằng cách build ra output dir riêng hoặc tắt dev server.
- Frontend: `npm run build` (Vite + tsc) — xem changelog.

## 12. Remaining TODOs

1. ~~`ManageAccountStatusCommandHandler` (UC-97) còn scaffold~~ — **DONE**: implement + revoke session `ACCOUNT_DEACTIVATED` (xem mục 14).
2. Backend HTML sanitize cho News — **PARTIAL**: đã có `IHtmlSanitizerService` + `HtmlSanitizerService` + DI; còn chờ wire vào
   `AddMultilingualNews`/`EditNews` khi 2 handler News (hiện scaffold) được implement (xem mục 15).
3. Nâng cấp frontend từ `sanitizeHtml` hand-rolled sang `DOMPurify` khi có thể `npm install dompurify`.
4. Upload file guard — **PARTIAL**: đã có `FileValidationService` chặn extension + MIME (SVG/HTML/JS/exec...);
   còn chờ gọi từ upload flow thật (Gallery/News/Documents chưa implement) + bổ sung magic-bytes check cho ảnh (xem mục 16).
5. Reuse-detection cho refresh token (rotation hiện thay hash in-place, không có chain → không phát hiện reuse token cũ; cân nhắc nếu cần mức cao hơn).
6. Cấu hình cron/scheduled chạy `cleanup_expired_user_sessions.sql`.

## 13. Risks

- **Secrets trong `appsettings.json` (dev)**: `JwtSettings.SecretKey`, DB password, SMTP password đang nằm trong file dev. Ở production PHẢI override bằng biến môi trường / secret manager, không commit secret thật. `appsettings.Production.json` cố tình KHÔNG chứa secret. Danh sách env var + checklist: `docs/database/DATABASE_DEPLOYMENT.md`. ⚠️ SMTP app password đã bị commit trong git → **rotate trước khi deploy**.
- **Token ở localStorage**: dễ bị đánh cắp nếu có XSS. Mitigation: access token short-lived (60'), refresh token hash + rotation, sanitize HTML. Cân nhắc chuyển refresh token sang HttpOnly cookie trong tương lai (kèm CSRF) — không làm ở phase này để không phá frontend.
- **Domain production placeholder**: `pems.fpt.edu.vn` là ví dụ; phải thay bằng domain thật trước khi deploy.
