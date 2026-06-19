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
- [ ] (TODO) Khóa user (ManageAccountStatus) → session bị revoke khi handler được implement; hiện tại bị chặn realtime bởi SessionValidationMiddleware.

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
- [ ] Runtime: nội dung `<script>alert(1)</script>` trong News không chạy.
- [ ] Runtime: `<img src=x onerror=alert(1)>` → thuộc tính `onerror` bị loại.
- [ ] Runtime: `<a href="javascript:alert(1)">` → href bị loại.
- [ ] (TODO) SVG/HTML upload bị chặn (FileValidationService).

## 7. Build
- [x] Backend: `dotnet build PEMS.Api` → 0 error, 0 warning (build ra output dir riêng vì dev server đang khóa bin mặc định).
- [ ] Frontend: `npm run build` → xem REFACTOR_CHANGELOG.md.
- [ ] Backend test: chưa có/không cấu hình test project auth (ghi rõ nếu chạy `dotnet test`).
