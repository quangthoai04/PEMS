# Auth Security Checklist

Trạng thái sau phase hardening 2026-06-20.

## Production error handling
- [x] Production không trả stackTrace/error class/SQL/secret.
- [x] 500 trả generic message + `errorCode=INTERNAL_SERVER_ERROR` + `traceId`.
- [x] Server vẫn log đầy đủ exception kèm traceId (không log token/password).

## Refresh token / session
- [x] Refresh token lưu hash (SHA-256), không lưu plain text.
- [x] Refresh token rotation mỗi lần refresh.
- [x] Refresh check session active + user/role ACTIVE.
- [x] Logout revoke session hiện tại (+ session theo refresh token).
- [x] Đổi role/reset password/đổi password → revoke session.
- [ ] Khóa/deactivate user → revoke session (TODO: handler scaffold; tạm chặn bằng SessionValidationMiddleware).
- [ ] Reuse-detection refresh token (TODO, optional).

## Database
- [x] `user_sessions` đã có index cho refresh lookup + revoke-all theo user (schema gốc).
- [x] Patch idempotent thêm index `expires_at`/`revoked_at` cho cleanup.
- [x] Script cleanup session expired+revoked (giữ login_logs/security_events).
- [ ] Lên lịch chạy cleanup (cron/scheduled task) — cấu hình khi deploy.

## CORS / HTTPS
- [x] CORS theo config; production chỉ allow domain frontend thật.
- [x] Không `AllowAnyOrigin`; không bật `AllowCredentials` với AnyOrigin.
- [x] `UseHttpsRedirection` luôn bật; `UseHsts` ở non-Development.
- [ ] Thay domain placeholder `pems.fpt.edu.vn` bằng domain thật trước deploy.

## Cookie / CSRF
- [x] Không dùng cookie cho token → không cần CSRF token.
- [x] Bearer + localStorage; refresh token qua body; `withCredentials` off.

## XSS / Security headers
- [x] Sanitize HTML render-time cho News (`sanitizeHtml`).
- [x] Security headers: nosniff, X-Frame-Options DENY, Referrer-Policy, Permissions-Policy.
- [x] CSP cho production (non-Swagger).
- [ ] Backend sanitize HTML khi lưu News (TODO: Ganss.Xss).
- [ ] Upload guard chặn SVG/HTML/JS (TODO: xác nhận FileValidationService).

## Secrets
- [ ] Production override JWT SecretKey / DB password / SMTP password bằng env/secret manager (KHÔNG commit secret thật).
- [x] `appsettings.Production.json` không chứa secret.

## Build
- [x] `dotnet build PEMS.Api` PASS (0 error/0 warning).
- [x] `npm run build` PASS.
