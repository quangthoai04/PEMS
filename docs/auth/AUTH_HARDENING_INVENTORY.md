# Auth Hardening Inventory

> Phase 1 audit. Trạng thái auth THỰC TẾ trong repo trước khi harden (2026-06-20).
> Không code trước khi có file này.

## 1. Current auth mechanism
- **JWT access token**: `JwtTokenService` (`PEMS.Infrastructure/Identity/JwtTokenService.cs`). HS256, `JwtSettings.SecretKey`, `AccessTokenMinutes = 60`. Claims gồm `UserId`, `SessionId`, `RoleCode`, `LoginPortal` (xem `PemsClaimTypes`).
- **Refresh token**: opaque random 48 bytes (`SecureTokenGenerator.GenerateOpaqueToken`). Chỉ trả raw cho client 1 lần. DB lưu **SHA-256 hash** (`refresh_token_hash`). `RefreshTokenDays = 7`.
- **DB session**: bảng `user_sessions`. Mỗi login tạo 1 row qua `SessionService.CreateSessionAsync`. Refresh token hash gộp chung vào session (1 session = 1 refresh token).
- **SessionValidationMiddleware**: chạy sau `UseAuthentication`. Mỗi request đã xác thực: check session còn active (`IsSessionActiveAsync`) + user.Status = ACTIVE + role.Status = ACTIVE + role chưa soft-delete. Nếu fail → 401. ⇒ Logout / khóa account / disable role có hiệu lực ngay ở request kế tiếp.
- **Token storage frontend**: `localStorage` (`authStorage.ts`) — keys `accessToken`, `refreshToken`. Access token gắn qua `Authorization: Bearer` (`authInterceptor`). Refresh token gửi trong **body** `POST /auth/refresh`. **Không dùng cookie.**

## 2. Exception handling
- **Middleware file**: `PEMS.Api/Middleware/ExceptionHandlingMiddleware.cs` (đăng ký ngoài cùng pipeline).
- **Current 400/401/403/404/409/422/500 response**:
  - `ValidationException` → 400 `{ success:false, message, errors, traceId }`.
  - `AuthBusinessException` → giữ `StatusCode` riêng (403/401/...) `{ success:false, errorCode, message, traceId }`.
  - `AuthenticationFailedException` → 401 `{ success:false, message, traceId }`.
  - `ForbiddenException` → 403, `NotFoundException` → 404, `ConflictException` → 409, `BusinessRuleException` → 422.
  - Unexpected → 500.
- **Có trả stackTrace ở production không**: KHÔNG. Production 500 chỉ trả `{ success:false, errorCode, message, traceId }`. Chỉ Development thêm `error` + `stackTrace`.
- **Gap**: errorCode 500 đang là `INTERNAL_ERROR`; docs/prompt chuẩn hoá là `INTERNAL_SERVER_ERROR`. Message generic đang là tiếng Anh.

## 3. Refresh token
- **Refresh token lưu ở đâu**: cột `user_sessions.refresh_token_hash`.
- **Plain text hay hash**: **hash** (SHA-256 hex). Raw token không bao giờ vào DB.
- **Có rotation không**: CÓ. `RefreshTokenCommandHandler` gọi `SessionService.RotateRefreshTokenAsync` mỗi lần refresh → sinh refresh token mới, update hash + `refresh_expires_at` + `expires_at` trên cùng session.
- **Có revoke khi logout không**: CÓ. `LogoutCommandHandler` revoke session theo `SessionId` từ JWT claim, và revoke thêm session theo refresh token nếu khác.
- **Có revoke all khi đổi role/status/password không**:
  - Đổi role: `UpdateAccountRoleCommandHandler` → `RevokeAllActiveSessionsAsync(RoleChanged)`. ✅
  - Reset password: `ResetPasswordCommandHandler` → `RevokeAllActiveSessionsAsync(PasswordReset)`. ✅
  - Change password: `ChangePasswordCommandHandler` → revoke mọi session KHÁC session hiện tại (`PasswordChanged`). ✅
  - Khóa / deactivate account (`ManageAccountStatusCommandHandler`): **chưa** — handler còn là scaffold (`NotImplementedException`, UC-97 chưa chốt spec). Mitigation: `SessionValidationMiddleware` chặn user non-ACTIVE ở request kế tiếp. → TODO.

## 4. CORS/HTTPS
- **Current CORS config**: `Program.cs`, policy `PemsFrontend`. Đọc origins từ `Cors:AllowedOrigins`; nếu rỗng fallback `localhost:3000/5173`. `AllowAnyHeader().AllowAnyMethod()`. **Không** `AllowAnyOrigin`, **không** `AllowCredentials`.
- **Dev origins**: `appsettings.json` → `http://localhost:3000/3001/3002/5173`.
- **Production origins**: chưa có file `appsettings.Production.json` → cần thêm.
- **AllowCredentials**: KHÔNG bật (đúng, vì dùng Bearer, không cookie).
- **HTTPS redirection**: CÓ — `app.UseHttpsRedirection()`. Chưa có `UseHsts()`.

## 5. Cookie/CSRF
- **Có dùng cookie refresh token không**: KHÔNG. Bearer + localStorage, refresh token qua body.
- **Cookie flags hiện tại**: N/A.
- **CSRF strategy**: Không cần CSRF token — không có cookie tự gửi nào dùng cho state-changing request. Mọi request xác thực bằng `Authorization: Bearer` (không tự gửi cross-site). Rủi ro chính của localStorage là **XSS** → tăng cường ở Phase 10.

## 6. XSS
- **Có dangerouslySetInnerHTML không**: CÓ — 2 file:
  - `frontend/pems-react/src/pages/NewsDetailPage.tsx`
  - `frontend/pems-react/src/pages/dashboard/news/NewsDetailDashboard.tsx`
  - Hiện render từ **mock data** hardcoded (`articleMock.content`). Khi nối backend (news section HTML) đây là điểm XSS.
- **Module nào render HTML**: News (chi tiết bài viết).
- **News section_body_html xử lý thế nào**: Entity `NewsContentSection` có nội dung HTML; các command News (`AddMultilingualNews`, `EditNews`, ...) hiện chưa sanitize HTML trước khi lưu.
- **Upload file type guard**: có `FileUploadValidationFilter` + `IFileValidationService` (`FileValidationService`) — cần xác nhận chặn SVG/HTML (ngoài scope sâu của auth, ghi TODO).

## 7. Database (user_sessions)
- **Columns**: `session_id (PK, BIGINT AI)`, `user_id`, `login_portal ENUM('VISITOR','INTERNAL')`, `selected_campus_id`, `auth_provider_id`, `refresh_token_hash VARCHAR(255)`, `refresh_expires_at`, `refresh_revoked_at`, `ip_address`, `user_agent`, `created_at`, `expires_at`, `revoked_at`, `revoked_by`, `revoked_reason`.
- **Indexes hiện có** (trong `database/scripts/pems_full.sql`):
  - `UNIQUE uq_sessions_refresh_hash (refresh_token_hash)` ✅
  - `idx_sessions_user_active (user_id, revoked_at, expires_at)` ✅
  - `idx_sessions_portal_campus (login_portal, selected_campus_id)` ✅
  - `idx_sessions_refresh_active (refresh_token_hash, refresh_revoked_at, refresh_expires_at)` ✅
  - `idx_sessions_ip_time (ip_address, created_at)` ✅
  - ⇒ Index cho refresh-lookup / revoke-all-by-user **đã đủ**. Còn thiếu index thuần theo `expires_at` / `revoked_at` để job cleanup quét nhanh → bổ sung bằng patch idempotent.
- **Cleanup job/script hiện có**: KHÔNG → tạo `cleanup_expired_user_sessions.sql`.

## 8. Kết luận audit
Core auth (hash refresh token, rotation, revoke logout, revoke khi đổi role/password, hide stacktrace prod, CORS config-based, HTTPS redirect, session validation realtime) **đã có và đạt chuẩn**. Phần hardening còn lại chủ yếu là: chuẩn hoá error code, security headers, XSS sanitize render-time, script DB cleanup, cấu hình production, và tài liệu.
