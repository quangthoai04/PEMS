# Auth Hardening Test Cases

> Trạng thái: `[x]` đã verify (build/code), `[ ]` cần chạy runtime trên môi trường có DB.

## 1. Production error handling
- [x] Code: Development 500 trả thêm `error` + `stackTrace`.
- [x] Code: Production/Staging 500 chỉ trả `success/errorCode/message/traceId`.
- [x] Code: errorCode 500 = `INTERNAL_SERVER_ERROR`, message tiếng Việt generic.
- [x] Code: `AuthBusinessException` giữ `errorCode`/`statusCode` riêng (403 cho wrong portal/campus).
- [ ] Runtime: ép 1 exception bất ngờ ở Production → response KHÔNG có stackTrace/SQL/secret.
- [ ] Runtime: validation sai → 400 kèm `errors`.

## 2. Refresh token / session
- [ ] Login success tạo 1 row `user_sessions`, `refresh_token_hash` là hash (không phải raw).
- [ ] Refresh với token hợp lệ → access token mới + refresh token mới (rotation).
- [ ] Refresh token cũ (trước rotation) → 401 (hash đã đổi, không tìm thấy session active).
- [ ] Refresh với token expired → 401.
- [ ] Refresh với session revoked → 401.
- [ ] Refresh khi user INACTIVE/LOCKED hoặc role inactive → revoke session + 401.
- [ ] Logout xong gọi protected API → 401 (SessionValidationMiddleware).
- [ ] Logout xong refresh token cũ không dùng được.

## 3. Revoke session khi đổi security context
- [ ] Đổi password (ChangePassword) → session khác bị revoke, session hiện tại còn dùng được.
- [ ] Reset password → tất cả session bị revoke.
- [ ] Admin/Staff Leader đổi role user → tất cả session user đó bị revoke (`RevokedSessions` > 0).
- [ ] User login lại nhận role/campus/permission mới.
- [x] Code: Khóa user (ManageAccountStatus, `ACTIVE`→`INACTIVE`/`LOCKED`) → `RevokeAllActiveSessionsAsync(... ACCOUNT_DEACTIVATED)` + `security_events`.
- [x] Code: Reactivate (`→ACTIVE`) reset `failed_login_count`/`locked_until`, KHÔNG revoke.
- [x] Code: Actor không thể đổi status chính mình; Staff Leader chỉ trong campus của mình.
- [ ] Runtime: khóa user → access token cũ gọi protected API → 401. (pending DB)
- [ ] Runtime: khóa user → refresh token cũ → 401; `user_sessions.revoked_at IS NOT NULL`. (pending DB)

## 4. CORS / HTTPS / Security headers
- [x] Code: Production CORS chỉ allow `https://pems.fpt.edu.vn` (config), không `AllowAnyOrigin`/`AllowCredentials`.
- [x] Code: `UseHsts()` chỉ chạy ở non-Development; `UseHttpsRedirection()` luôn bật.
- [x] Code: SecurityHeaders set `X-Content-Type-Options`, `X-Frame-Options`, `Referrer-Policy`, `Permissions-Policy`; CSP cho prod non-Swagger.
- [ ] Runtime: response có header `X-Content-Type-Options: nosniff`, `X-Frame-Options: DENY`.
- [ ] Runtime: origin lạ bị chặn ở production.
- [ ] Runtime: Swagger UI (dev) vẫn hoạt động (CSP không áp cho /swagger và dev).

## 5. Cookie / CSRF
- [x] Không dùng cookie → không cần CSRF token (Bearer + localStorage, refresh qua body).
- [x] Axios `httpClient` KHÔNG bật `withCredentials`.

## 6. XSS
- [x] Code: `sanitizeHtml` xóa `<script>`, `on*`, `javascript:`/`data:text/html` URL, `iframe/object/embed`.
- [x] Code: 2 chỗ render News dùng `sanitizeHtml(...)`.
- [x] Code: Backend `IHtmlSanitizerService`/`HtmlSanitizerService` (package `HtmlSanitizer`) bỏ script/iframe/object/embed/form/style, `on*`, scheme chỉ http/https/mailto/tel; đăng ký DI.
- [ ] Runtime: nội dung `<script>alert(1)</script>` trong News không chạy.
- [ ] Runtime: `<img src=x onerror=alert(1)>` → thuộc tính `onerror` bị loại.
- [ ] Runtime: `<a href="javascript:alert(1)">` → href bị loại.
- [ ] (PENDING) Backend sanitize News trước khi lưu DB — chờ wire vào `AddMultilingualNews`/`EditNews` (handler News còn scaffold).

## 6b. Upload guard (FileValidationService)
- [x] Code: denylist extension chặn `.svg/.svgz/.html/.htm/.js/.jsx/.ts/.php/.aspx/.jsp/.exe/.bat/.ps1/.sh/.jar...`.
- [x] Code: denylist MIME chặn `image/svg+xml`, `text/html`, `application/javascript`, `application/x-msdownload`, `application/x-sh`...
- [x] Code: kiểm tra size + không tin `ContentType` client; đăng ký DI.
- [ ] Runtime: upload `.svg` / `.html` / `.js` bị chặn; ảnh `.jpg/.png/.webp` + `.pdf` hợp lệ vẫn pass.
      (PENDING: chưa có upload endpoint gọi service — Gallery/News/Documents chưa implement.)

## 7. Build
- [x] Backend: `dotnet build PEMS.Api` → 0 error, 0 warning (build ra output dir riêng vì dev server đang khóa bin mặc định).
- [ ] Frontend: `npm run build` → xem REFACTOR_CHANGELOG.md.
- [ ] Backend test: chưa có/không cấu hình test project auth (ghi rõ nếu chạy `dotnet test`).
