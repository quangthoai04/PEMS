# PROMPT TRIỂN KHAI AUTH HARDENING CÒN LẠI CHO PEMS

> File này dùng để giao cho AI/code agent sửa code trực tiếp trong project **PEMS — Partnership Engagement Management System**.  
> Mục tiêu: **không làm lại cơ chế Authentication**, chỉ harden/nâng cấp các phần còn lại sau khi Core Auth Dual Portal đã chạy.

---

## 0. Bối cảnh hiện tại

PEMS hiện đang dùng cơ chế:

```text
Dual Portal + SSO-first + JWT Access Token + Refresh Token + DB-backed Session
```

Các phần đã xử lý hoặc tạm loại khỏi scope lần này:

```text
[ĐÃ LÀM] Google SSO đã có ClientId thật.
[TẠM BỎ QUA] FEID UI hiện không hiển thị vì chưa có provider/credential thật.
[ĐÃ FIX] Frontend redirect/RBAC trắng màn hình sau login đã xử lý.
```

Do đó, **không làm lại các phần này** trong phase hiện tại.

---

## 1. Vai trò của AI/code agent

Bạn là:

```text
Senior .NET Clean Architecture Developer
Security Engineer
Backend Hardening Reviewer
React Integration Reviewer
Database-first MySQL Reviewer
```

Bạn đang sửa project **PEMS** hiện tại.  
Nhiệm vụ của bạn là harden cơ chế auth hiện có, không phá luồng login đang chạy.

---

## 2. Mục tiêu cuối cùng của phase này

Sau phase này hệ thống phải đạt:

```text
[ ] Production không trả stackTrace/error debug ra response.
[ ] Error response 500 ở production không lộ secret, SQL, exception class, file path.
[ ] Refresh token được lưu và xử lý an toàn hơn.
[ ] Nếu có thể, refresh token được lưu hash trong DB.
[ ] Refresh token rotation được kiểm tra/triển khai nếu chưa có.
[ ] Logout revoke đúng current session và refresh token liên quan.
[ ] Khi đổi role/campus/status/password quan trọng thì revoke session cũ.
[ ] user_sessions có index cần thiết.
[ ] Có SQL patch idempotent, không auto-migrate bừa.
[ ] Có cleanup job hoặc SQL script dọn session hết hạn/revoked.
[ ] CORS production chỉ allow domain frontend thật.
[ ] HTTPS production bắt buộc.
[ ] Cookie nếu dùng refresh token phải HttpOnly + Secure + SameSite.
[ ] Nếu dùng cookie tự gửi thì có CSRF strategy rõ.
[ ] XSS checklist cho các vùng render HTML, đặc biệt News section_body_html.
[ ] Không phá Login, SSO, /auth/me, /auth/permissions, refresh, logout hiện có.
[ ] Build backend pass.
[ ] Build frontend pass nếu có chỉnh frontend config/interceptor.
[ ] Có docs/changelog sau khi sửa.
```

---

## 3. Phạm vi làm lần này

### 3.1 In scope

Chỉ làm các phần sau:

```text
1. Production error handling
   - ExceptionHandlingMiddleware
   - Problem/error response contract
   - Environment-based stackTrace hiding

2. Refresh token/session hardening
   - Refresh token hash
   - Refresh token rotation
   - Reuse detection nếu hợp lý
   - Logout revoke đúng session
   - Revoke all sessions khi role/campus/status/password đổi

3. user_sessions database hardening
   - Index
   - Unique constraint nếu phù hợp
   - Cleanup script/job
   - Không phá schema hiện tại

4. CORS/HTTPS/cookie/CSRF config
   - Program.cs
   - appsettings
   - env config
   - CookieOptions nếu đang dùng cookie
   - Axios `withCredentials` chỉ khi thật sự dùng cookie

5. XSS hardening
   - Kiểm tra dangerouslySetInnerHTML
   - Sanitize HTML cho rich text/news
   - Upload file type guard cho HTML/SVG nếu có
   - CSP header nếu có thể thêm an toàn

6. Documentation/test checklist
   - docs/auth/AUTH_HARDENING_REPORT.md
   - docs/auth/AUTH_SECURITY_CHECKLIST.md
   - docs/database/DATABASE_DEPLOYMENT.md nếu có SQL patch
   - docs/architecture/REFACTOR_CHANGELOG.md
```

### 3.2 Out of scope

Không làm trong phase này:

```text
[ ] Không làm lại Core Login.
[ ] Không đổi Dual Portal policy.
[ ] Không đổi Google SSO flow nếu đã chạy.
[ ] Không bật FEID UI.
[ ] Không fake FEID success.
[ ] Không sửa lại RBAC route/menu nếu đã fix.
[ ] Không đổi permission matrix nếu không liên quan.
[ ] Không đổi database destructive.
[ ] Không auto-migrate bằng EF nếu project đang database-first/manual SQL.
[ ] Không tự seed role/campus/permission runtime.
```

---

## 4. Tài liệu và code bắt buộc đọc trước

Đọc docs trước:

```text
PROJECT_STRUCTURE_FULL.md
PROJECT_OVERVIEW.md
CLEAN_ARCHITECTURE.md
DATABASE_SCHEMA.md
docs/auth/AUTH_CORE_BACKEND_DUAL_PORTAL.md
docs/auth/AUTH_ERROR_CODES.md
docs/auth/AUTH_DUAL_PORTAL_SSO_FIRST.md nếu có
docs/architecture/REFACTOR_CHANGELOG.md
database/scripts/*.sql
database/seed/*.sql
```

Quét code thật:

```text
backend/PEMS.Api/Middleware/ExceptionHandlingMiddleware.cs
backend/PEMS.Api/Middleware/SessionValidationMiddleware.cs
backend/PEMS.Api/Extensions/AuthenticationExtensions.cs
backend/PEMS.Api/Extensions/CorsExtensions.cs nếu có
backend/PEMS.Api/Program.cs
backend/PEMS.Api/appsettings*.json

backend/PEMS.Api/Controllers/AuthenticationController.cs
backend/PEMS.Application/Authentication/**
backend/PEMS.Application/Accounts/**
backend/PEMS.Application/Profiles/**
backend/PEMS.Application/Common/Interfaces/**
backend/PEMS.Application/Common/Security/**
backend/PEMS.Application/Common/Exceptions/**

backend/PEMS.Domain/Entities/User.cs
backend/PEMS.Domain/Entities/UserSession.cs
backend/PEMS.Domain/Entities/UserAuthProvider.cs
backend/PEMS.Domain/Entities/Role.cs
backend/PEMS.Domain/Entities/Permission.cs

backend/PEMS.Infrastructure/Persistence/**
backend/PEMS.Infrastructure/Repositories/**
backend/PEMS.Infrastructure/Identity/**
backend/PEMS.Infrastructure/Services/**

frontend/pems-react/src/shared/api/**
frontend/pems-react/src/features/authentication/**
frontend/pems-react/src/shared/auth/**
frontend/pems-react/src/pages/**
frontend/pems-react/src/components/**
```

Nếu tên folder khác thực tế, dùng cấu trúc thật trong repo và ghi rõ trong changelog.

---

## 5. Quy tắc tuyệt đối không được vi phạm

```text
[ ] Không phá login hiện có.
[ ] Không đổi response success AuthResponse nếu frontend đang phụ thuộc.
[ ] Không trả passwordHash, refreshTokenHash, provider secret, security stamp ra API.
[ ] Không dùng 401 cho wrong portal/campus mismatch.
[ ] Không fake Google/FEID success.
[ ] Không auto-create internal user.
[ ] Không tin userId/campusId/role từ frontend body.
[ ] Không để stackTrace lộ ở production.
[ ] Không AllowAnyOrigin ở production.
[ ] Không bật AllowCredentials với AllowAnyOrigin.
[ ] Không render HTML user nhập nếu chưa sanitize.
[ ] Không tự chạy destructive SQL.
[ ] Không xóa code cũ nếu chưa chắc dependency.
```

---

## 6. Phase 1 — Audit hiện trạng

Trước khi sửa, tạo file ngắn:

```text
docs/auth/AUTH_HARDENING_INVENTORY.md
```

Nội dung phải có:

```markdown
# Auth Hardening Inventory

## 1. Current auth mechanism
- JWT access token:
- Refresh token:
- DB session:
- SessionValidationMiddleware:
- Token storage frontend:

## 2. Exception handling
- Middleware file:
- Current 400/401/403/500 response:
- Có trả stackTrace ở production không:

## 3. Refresh token
- Refresh token lưu ở đâu:
- Plain text hay hash:
- Có rotation không:
- Có revoke khi logout không:
- Có revoke all khi đổi role/status/password không:

## 4. CORS/HTTPS
- Current CORS config:
- Dev origins:
- Production origins:
- AllowCredentials:
- HTTPS redirection:

## 5. Cookie/CSRF
- Có dùng cookie refresh token không:
- Cookie flags hiện tại:
- CSRF strategy:

## 6. XSS
- Có dangerouslySetInnerHTML không:
- Module nào render HTML:
- News section_body_html xử lý thế nào:
- Upload file type guard:

## 7. Database
- user_sessions columns:
- indexes hiện có:
- cleanup job/script hiện có:
```

Không code trước khi có inventory.

---

## 7. Phase 2 — Production error handling hardening

### 7.1 Mục tiêu

Ở development có thể log chi tiết, nhưng production response không được lộ:

```text
- stackTrace
- exception class nội bộ
- SQL query
- connection string
- file path local
- provider secret
- token
- password
```

### 7.2 Sửa ExceptionHandlingMiddleware

Tìm:

```text
backend/PEMS.Api/Middleware/ExceptionHandlingMiddleware.cs
```

Yêu cầu:

```text
- Inject IWebHostEnvironment hoặc IHostEnvironment.
- Nếu exception là AuthBusinessException:
  - giữ errorCode/message/statusCode như hiện tại.
- Nếu validation exception:
  - trả 400, không lộ stackTrace.
- Nếu UnauthorizedAccess/token/session exception:
  - trả 401 với errorCode phù hợp.
- Nếu exception bất ngờ:
  - Development: có thể trả detail nếu project đang cần debug.
  - Production/Staging: chỉ trả generic message.
```

Response production 500 chuẩn:

```json
{
  "success": false,
  "errorCode": "INTERNAL_SERVER_ERROR",
  "message": "Đã xảy ra lỗi hệ thống. Vui lòng thử lại sau."
}
```

Nếu có `traceId`, được phép trả:

```json
{
  "success": false,
  "errorCode": "INTERNAL_SERVER_ERROR",
  "message": "Đã xảy ra lỗi hệ thống. Vui lòng thử lại sau.",
  "traceId": "00-..."
}
```

Không trả:

```json
{
  "stackTrace": "...",
  "exception": "NullReferenceException",
  "sql": "SELECT ..."
}
```

### 7.3 Logging

Phải log exception ở server bằng logger:

```csharp
_logger.LogError(exception, "Unhandled exception. TraceId: {TraceId}", traceId);
```

Không log token/password/refreshToken raw.

### 7.4 ErrorCode bổ sung

Nếu chưa có, thêm constant:

```csharp
public const string InternalServerError = "INTERNAL_SERVER_ERROR";
```

Cập nhật:

```text
backend/PEMS.Application/Common/Security/AuthErrorCodes.cs
frontend/pems-react/src/features/authentication/api/authError.ts nếu frontend cần map
docs/auth/AUTH_ERROR_CODES.md
```

---

## 8. Phase 3 — Refresh token hardening

### 8.1 Mục tiêu

Refresh token là token dài hạn, phải bảo vệ kỹ hơn access token.

Yêu cầu chuẩn:

```text
- Refresh token raw chỉ trả cho client đúng một lần khi login/refresh.
- DB chỉ lưu hash của refresh token.
- Khi refresh, hash token client gửi rồi so sánh.
- Refresh token có expires_at.
- Session phải active.
- User phải ACTIVE.
- Nếu token/session revoked thì từ chối.
- Nếu rotate được thì rotate mỗi lần refresh.
```

### 8.2 Kiểm tra entity UserSession

Tìm:

```text
backend/PEMS.Domain/Entities/UserSession.cs
```

Kiểm tra có các field hoặc tương đương không:

```text
session_id
user_id
refresh_token_hash
jwt_id / jti / access_token_jti nếu có
expires_at
revoked_at
created_at
created_by_ip
user_agent
last_used_at
replaced_by_session_id hoặc replaced_by_refresh_token_hash nếu có rotation chain
revoked_reason nếu có
```

Không bắt buộc thêm toàn bộ nếu schema hiện tại chưa có. Nhưng tối thiểu cần:

```text
refresh_token_hash
expires_at
revoked_at
last_used_at hoặc updated_at
```

Nếu thiếu, tạo SQL patch idempotent.

### 8.3 Hash refresh token

Không lưu refresh token plain text.

Tạo hoặc dùng service:

```csharp
public interface ITokenHashService
{
    string HashToken(string rawToken);
    bool VerifyToken(string rawToken, string tokenHash);
}
```

Có thể dùng:

```text
- HMACSHA256 với server-side secret riêng
- SHA256 nếu chưa có secret, nhưng ưu tiên HMAC
```

Không dùng password hasher chậm cho mọi refresh nếu không cần; refresh token là random high entropy, hash/HMAC đủ.

Config gợi ý:

```json
"TokenHashing": {
  "RefreshTokenHmacSecret": "<set-by-secret-manager-or-env>"
}
```

Không hardcode secret trong code.

### 8.4 Refresh flow chuẩn

`RefreshTokenCommandHandler` phải làm:

```text
1. Nhận refresh token từ request body hoặc cookie theo thiết kế hiện tại.
2. Nếu token rỗng -> 401 TOKEN_EXPIRED hoặc UNAUTHORIZED.
3. Hash token.
4. Tìm session theo refresh_token_hash.
5. Check session tồn tại.
6. Check session chưa revoked.
7. Check session chưa expired.
8. Load user + role.
9. Check user ACTIVE.
10. Nếu role inactive/disabled -> revoke session, trả 401/403 phù hợp.
11. Rotate refresh token:
    - Sinh refresh token mới.
    - Hash token mới.
    - Update refresh_token_hash mới vào session hiện tại
      hoặc revoke session cũ + tạo session mới tùy kiến trúc hiện tại.
12. Update last_used_at.
13. Trả access token mới + refresh token mới.
```

Nếu chưa thể rotate vì frontend chưa hỗ trợ:

```text
- Không làm vỡ refresh hiện tại.
- Ghi TODO rõ trong AUTH_HARDENING_REPORT.md.
- Tối thiểu phải hash và check revoked/expires/user active.
```

### 8.5 Reuse detection nếu có rotation

Nếu dùng mô hình revoke session cũ + tạo session mới:

```text
- Khi refresh token cũ bị dùng lại sau khi đã rotate:
  - coi là token reuse.
  - revoke toàn bộ session của user hoặc ít nhất session chain.
  - ghi security_events TOKEN_REUSE_DETECTED.
```

Nếu chưa có bảng/security event type, ghi TODO.

---

## 9. Phase 4 — Logout và revoke session chuẩn

### 9.1 Logout current session

`LogoutCommand` phải:

```text
- Lấy current userId và sessionId từ JWT claims/current user service.
- Tìm user_session tương ứng.
- Set revoked_at = now.
- Set revoked_reason = LOGOUT nếu có field.
- Nếu refresh token hash nằm cùng session thì vô hiệu luôn.
- Ghi login_logs/security_events LOGOUT.
```

Không nên logout toàn bộ thiết bị trừ khi endpoint tên rõ:

```text
POST /api/auth/logout-all
```

### 9.2 Logout all sessions optional

Nếu project cần, tạo command riêng:

```text
LogoutAllSessionsCommand
```

Rule:

```text
- Chỉ user hiện tại tự logout all session của mình
  hoặc admin có permission rõ mới revoke session người khác.
- Không dùng chung với LogoutCommand thường.
```

---

## 10. Phase 5 — Revoke session khi đổi role/campus/status/password

### 10.1 Khi nào phải revoke session

Bắt buộc revoke toàn bộ session của user khi:

```text
- User bị LOCKED/INACTIVE.
- User bị đổi role.
- User bị đổi subRole.
- User bị đổi primaryCampusId/campusId.
- User bị đổi departmentId.
- User đổi password.
- Admin reset password.
- UserAuthProvider bị unlink.
- Role của user bị disable/inactive nếu hệ thống có role status.
- Permission matrix thay đổi lớn nếu project chưa có permission version.
```

### 10.2 Các handler cần kiểm tra

Quét và sửa các handler liên quan:

```text
backend/PEMS.Application/Accounts/**
backend/PEMS.Application/Profiles/**
backend/PEMS.Application/Authentication/**
backend/PEMS.Application/Roles/**
backend/PEMS.Application/Permissions/**
```

Tìm các command kiểu:

```text
UpdateUserRoleCommand
UpdateAccountRoleCommand
UpdateUserStatusCommand
LockUserCommand
DeactivateUserCommand
ChangePasswordCommand
ResetPasswordCommand
UpdateProfileCampusCommand
AssignUserDepartmentCommand
```

Sau khi update thành công, gọi service:

```csharp
await _sessionService.RevokeAllUserSessionsAsync(
    userId,
    reason: "SECURITY_CONTEXT_CHANGED",
    cancellationToken);
```

### 10.3 Không revoke khi nào

Không cần revoke khi chỉ đổi:

```text
- avatar
- phone
- display name
- bio/profile text
```

Trừ khi project có policy bắt buộc.

### 10.4 Audit

Mỗi lần revoke do thay đổi security context phải ghi:

```text
security_events:
- SESSION_REVOKED
- SECURITY_CONTEXT_CHANGED
- PASSWORD_CHANGED
- ACCOUNT_LOCKED
```

Nếu bảng hiện tại chưa có event type, dùng event type đang có hoặc ghi TODO.

---

## 11. Phase 6 — user_sessions DB index và cleanup

### 11.1 Không auto-migrate

Project PEMS theo hướng database-first/manual SQL.

Không dùng:

```text
dotnet ef database update
```

nếu project đang cấm auto-migrate.

Tạo file:

```text
database/scripts/patch_auth_hardening_sessions.sql
```

### 11.2 SQL patch idempotent gợi ý cho MySQL 8

AI/code agent phải đối chiếu schema thật trước khi áp dụng. Nếu MySQL version không hỗ trợ `IF NOT EXISTS` cho index trong môi trường hiện tại, dùng kiểm tra `information_schema.statistics`.

Gợi ý:

```sql
-- patch_auth_hardening_sessions.sql
-- Mục tiêu: thêm index cho user_sessions phục vụ auth/session validation/refresh/logout.

-- 1. Index theo user_id để revoke all sessions nhanh
CREATE INDEX idx_user_sessions_user_id
ON user_sessions(user_id);

-- 2. Index theo refresh_token_hash để refresh token lookup nhanh
CREATE INDEX idx_user_sessions_refresh_token_hash
ON user_sessions(refresh_token_hash);

-- 3. Index theo expires_at để cleanup session hết hạn
CREATE INDEX idx_user_sessions_expires_at
ON user_sessions(expires_at);

-- 4. Index theo revoked_at để lọc session active/revoked
CREATE INDEX idx_user_sessions_revoked_at
ON user_sessions(revoked_at);

-- 5. Nếu có session_id/jti riêng và chưa unique/index
CREATE INDEX idx_user_sessions_session_id
ON user_sessions(session_id);
```

Nhưng script cuối cùng phải **idempotent**, tránh lỗi duplicate index khi chạy lại.

### 11.3 Unique refresh token hash

Nếu mỗi refresh token hash là duy nhất, cân nhắc:

```sql
CREATE UNIQUE INDEX uq_user_sessions_refresh_token_hash
ON user_sessions(refresh_token_hash);
```

Chỉ thêm unique nếu chắc chắn:

```text
- refresh_token_hash không null với active session
- không có duplicate data cũ
- flow refresh không dùng nhiều record cùng hash
```

Nếu không chắc, dùng normal index trước.

### 11.4 Cleanup script

Tạo:

```text
database/scripts/cleanup_expired_user_sessions.sql
```

Gợi ý:

```sql
DELETE FROM user_sessions
WHERE expires_at < UTC_TIMESTAMP()
  AND revoked_at IS NOT NULL;
```

Nếu muốn giữ audit lâu hơn:

```sql
DELETE FROM user_sessions
WHERE expires_at < DATE_SUB(UTC_TIMESTAMP(), INTERVAL 30 DAY)
  AND revoked_at IS NOT NULL;
```

Không xóa `login_logs` và `security_events`.

### 11.5 Cleanup job

Nếu project có background worker/Hangfire/Quartz:

```text
- Tạo job chạy mỗi ngày.
- Chỉ dọn session expired/revoked.
- Log số dòng đã dọn.
```

Nếu chưa có background job:

```text
- Không thêm framework nặng nếu không cần.
- Để SQL script/manual task.
- Ghi rõ trong DATABASE_DEPLOYMENT.md.
```

---

## 12. Phase 7 — CORS hardening

### 12.1 Kiểm tra Program.cs / AuthenticationExtensions

Tìm:

```text
builder.Services.AddCors(...)
app.UseCors(...)
```

Production không được:

```csharp
.AllowAnyOrigin()
.AllowAnyHeader()
.AllowAnyMethod()
```

Đặc biệt không được:

```csharp
.AllowAnyOrigin()
.AllowCredentials()
```

### 12.2 Config theo environment

Tạo config:

```json
"Cors": {
  "AllowedOrigins": [
    "http://localhost:5173",
    "http://localhost:3000"
  ]
}
```

Production:

```json
"Cors": {
  "AllowedOrigins": [
    "https://pems.fpt.edu.vn"
  ]
}
```

Nếu backend domain riêng:

```text
Frontend: https://pems.fpt.edu.vn
Backend:  https://api.pems.fpt.edu.vn
```

CORS chỉ allow frontend domain.

### 12.3 Code gợi ý

```csharp
var allowedOrigins = builder.Configuration
    .GetSection("Cors:AllowedOrigins")
    .Get<string[]>() ?? Array.Empty<string>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("FrontendPolicy", policy =>
    {
        policy
            .WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod();

        // Chỉ bật AllowCredentials nếu dùng cookie cross-site thật sự.
        // policy.AllowCredentials();
    });
});
```

Pipeline:

```csharp
app.UseCors("FrontendPolicy");
app.UseAuthentication();
app.UseAuthorization();
```

Thứ tự cần kiểm tra theo code thật.

---

## 13. Phase 8 — HTTPS hardening

### 13.1 Backend

Production phải có:

```csharp
app.UseHttpsRedirection();
```

Nếu deploy sau reverse proxy/Nginx/IIS/Render/Azure, kiểm tra forwarded headers nếu cần:

```csharp
app.UseForwardedHeaders();
```

Chỉ thêm nếu hosting yêu cầu và cấu hình đúng.

### 13.2 Frontend env

Production frontend `.env.production`:

```env
VITE_API_BASE_URL=https://api.pems.fpt.edu.vn/api
```

Không để production gọi:

```env
VITE_API_BASE_URL=http://...
```

### 13.3 Cookie Secure

Nếu refresh token dùng cookie:

```csharp
Response.Cookies.Append("refreshToken", refreshToken, new CookieOptions
{
    HttpOnly = true,
    Secure = true,
    SameSite = SameSiteMode.Strict,
    Expires = DateTimeOffset.UtcNow.AddDays(7)
});
```

Không set `Secure=false` ở production.

---

## 14. Phase 9 — Cookie và CSRF strategy

### 14.1 Xác định token storage hiện tại

Trong inventory phải ghi rõ:

```text
Access token lưu ở đâu:
Refresh token lưu ở đâu:
Có dùng cookie không:
Axios có withCredentials không:
```

### 14.2 Nếu vẫn dùng Bearer token + localStorage

Nếu hiện tại refresh token đang ở localStorage và chưa đổi được ngay:

```text
- Không bắt buộc chuyển ngay sang cookie nếu sẽ làm vỡ frontend.
- Phải ghi risk trong AUTH_HARDENING_REPORT.md.
- Tăng XSS hardening.
- Access token nên short-lived.
- Refresh token nên hash DB + rotation.
```

### 14.3 Nếu chuyển refresh token sang HttpOnly cookie

Backend:

```text
- Login response set refreshToken cookie.
- Refresh endpoint đọc refreshToken từ cookie.
- Logout clear cookie.
- Cookie: HttpOnly + Secure + SameSite.
```

Frontend:

```ts
axios.create({
  baseURL: import.meta.env.VITE_API_BASE_URL,
  withCredentials: true
});
```

CORS backend:

```csharp
policy.WithOrigins(allowedOrigins)
      .AllowAnyHeader()
      .AllowAnyMethod()
      .AllowCredentials();
```

Không dùng `AllowAnyOrigin` khi có credentials.

### 14.4 SameSite rule

Nếu frontend/backend same-site:

```text
SameSite=Strict hoặc Lax
```

Nếu frontend/backend khác site và bắt buộc cross-site cookie:

```text
SameSite=None; Secure
```

Khi dùng `SameSite=None`, phải có CSRF strategy.

### 14.5 CSRF strategy

Nếu refresh token cookie chỉ dùng cho:

```text
POST /api/auth/refresh
POST /api/auth/logout
```

Tối thiểu:

```text
- SameSite=Lax/Strict nếu deployment cho phép.
- Check Origin/Referer cho auth endpoints dùng cookie.
- Không dùng GET cho action thay đổi dữ liệu.
```

Nếu cookie dùng cho mọi API state-changing:

```text
- Implement CSRF token double-submit hoặc antiforgery token.
- Frontend gửi X-CSRF-TOKEN.
- Backend validate token.
```

Không cần CSRF token phức tạp nếu access token vẫn gửi bằng `Authorization: Bearer` và cookie chỉ dùng refresh với SameSite phù hợp, nhưng phải ghi rõ decision.

---

## 15. Phase 10 — XSS hardening

### 15.1 Tìm điểm nguy hiểm

Search toàn frontend:

```text
dangerouslySetInnerHTML
innerHTML
insertAdjacentHTML
DOMParser
iframe
script
section_body_html
bodyHtml
contentHtml
richText
editor
```

Search backend:

```text
section_body_html
html
content
news_content_sections
public_contents
gallery
```

### 15.2 React render text bình thường thì ổn

Không cần sanitize nếu chỉ render:

```tsx
<p>{text}</p>
```

React escape text mặc định.

### 15.3 Nếu render HTML rich text

Nếu có:

```tsx
<div dangerouslySetInnerHTML={{ __html: html }} />
```

Bắt buộc sanitize trước.

Frontend gợi ý:

```bash
npm install dompurify
npm install -D @types/dompurify
```

Utility:

```ts
import DOMPurify from 'dompurify';

export function sanitizeHtml(input: string): string {
  return DOMPurify.sanitize(input, {
    USE_PROFILES: { html: true },
    FORBID_TAGS: ['script', 'iframe', 'object', 'embed'],
    FORBID_ATTR: ['onerror', 'onclick', 'onload', 'onmouseover']
  });
}
```

Render:

```tsx
<div dangerouslySetInnerHTML={{ __html: sanitizeHtml(section.sectionBodyHtml) }} />
```

### 15.4 Backend sanitize cũng cần

Không chỉ sanitize frontend. Backend nên sanitize trước khi lưu hoặc trước khi trả ra API.

Nếu dùng .NET library:

```text
Ganss.Xss HtmlSanitizer
```

Tạo service:

```csharp
public interface IHtmlSanitizerService
{
    string Sanitize(string html);
}
```

Dùng ở các command:

```text
CreateNewsContentSectionCommand
UpdateNewsContentSectionCommand
CreatePublicContentCommand nếu còn
```

Không sanitize quá mạnh làm mất format cần thiết như:

```text
p, br, strong, em, ul, ol, li, a, img nếu hệ thống cho ảnh
```

Nhưng phải chặn:

```text
script
iframe không whitelist
onerror/onload/onclick
javascript: URL
data:text/html
```

### 15.5 Upload file guard

Nếu hệ thống cho upload file ảnh:

```text
- Chỉ allow MIME/type ảnh rõ: image/png, image/jpeg, image/webp.
- Cẩn thận image/svg+xml vì SVG có thể chứa script.
- Nếu chưa sanitize SVG, chặn SVG upload.
- Không cho upload .html, .js, .exe.
- Kiểm tra extension + MIME + magic bytes nếu service có.
```

### 15.6 CSP header optional

Nếu có thể thêm header an toàn:

```text
Content-Security-Policy:
default-src 'self';
script-src 'self';
object-src 'none';
base-uri 'self';
frame-ancestors 'none';
img-src 'self' data: https:;
style-src 'self' 'unsafe-inline';
```

Không thêm CSP quá chặt làm vỡ app nếu chưa test. Nếu chưa chắc, ghi TODO.

---

## 16. Phase 11 — Security headers cơ bản

Có thể thêm middleware/header nếu chưa có:

```text
X-Content-Type-Options: nosniff
X-Frame-Options: DENY
Referrer-Policy: no-referrer-when-downgrade hoặc strict-origin-when-cross-origin
Permissions-Policy: camera=(), microphone=(), geolocation=()
```

Không thêm HSTS ở local dev. Production có thể:

```text
Strict-Transport-Security: max-age=31536000; includeSubDomains
```

Chỉ bật HSTS khi chắc chắn toàn domain/subdomain dùng HTTPS.

---

## 17. Phase 12 — Error code đồng bộ

Đảm bảo các file đồng bộ:

```text
backend/PEMS.Application/Common/Security/AuthErrorCodes.cs
frontend/pems-react/src/features/authentication/api/authError.ts
docs/auth/AUTH_ERROR_CODES.md
```

Các code quan trọng vẫn phải giữ:

```text
INVALID_CREDENTIALS
PASSWORD_LOGIN_DISABLED
CAMPUS_REQUIRED
CAMPUS_MISMATCH
WRONG_PORTAL_VISITOR_ACCOUNT
WRONG_PORTAL_INTERNAL_ACCOUNT
INTERNAL_ACCOUNT_NOT_FOUND
ACCOUNT_INACTIVE
ACCOUNT_LOCKED
SSO_DISABLED
EXTERNAL_AUTH_FAILED
VISITOR_PROVISION_DISABLED
FEID_DISABLED
FEID_NOT_CONFIGURED
FEID_NOT_ELIGIBLE
SESSION_REVOKED
TOKEN_EXPIRED
UNAUTHORIZED
INTERNAL_SERVER_ERROR
```

Không dùng 401 cho:

```text
WRONG_PORTAL_VISITOR_ACCOUNT
WRONG_PORTAL_INTERNAL_ACCOUNT
CAMPUS_MISMATCH
INTERNAL_ACCOUNT_NOT_FOUND
```

Các lỗi này phải là 403.

---

## 18. Phase 13 — Test checklist bắt buộc

Tạo/cập nhật:

```text
docs/auth/AUTH_HARDENING_TEST_CASES.md
```

### 18.1 Production error handling

```text
[ ] Development 500 có log chi tiết server.
[ ] Production 500 response không có stackTrace.
[ ] Production 500 response có generic message.
[ ] AuthBusinessException vẫn trả đúng errorCode/status.
[ ] Validation vẫn trả 400.
```

### 18.2 Refresh token/session

```text
[ ] Login success tạo user_session.
[ ] Refresh token lookup bằng hash hoặc secure storage.
[ ] Refresh với token hợp lệ trả access token mới.
[ ] Refresh với token expired trả 401.
[ ] Refresh với token revoked trả 401.
[ ] Refresh với user INACTIVE/LOCKED bị chặn.
[ ] Logout xong gọi protected API bị 401.
[ ] Logout xong refresh token cũ không dùng được.
[ ] Nếu rotation: refresh token cũ không dùng lại được.
```

### 18.3 Revoke session khi đổi security context

```text
[ ] Đổi password xong session cũ bị revoke.
[ ] Admin đổi role user xong session cũ bị revoke.
[ ] Admin đổi campus user xong session cũ bị revoke.
[ ] Khóa user xong session cũ bị revoke.
[ ] User login lại nhận role/campus/permission mới.
```

### 18.4 CORS/HTTPS

```text
[ ] Dev allow localhost frontend.
[ ] Production chỉ allow domain frontend thật.
[ ] Origin lạ bị chặn.
[ ] Không có AllowAnyOrigin ở production.
[ ] HTTPS redirect hoạt động ở production.
[ ] Production frontend gọi API bằng https.
```

### 18.5 Cookie/CSRF nếu dùng cookie

```text
[ ] Cookie refresh token có HttpOnly.
[ ] Cookie refresh token có Secure ở production.
[ ] Cookie có SameSite đúng.
[ ] Nếu withCredentials=true thì CORS không AllowAnyOrigin.
[ ] Origin/Referer check hoặc CSRF token có nếu cookie dùng cho state-changing API.
```

### 18.6 XSS

```text
[ ] Search không còn dangerouslySetInnerHTML không sanitize.
[ ] News section_body_html được sanitize.
[ ] Input <script>alert(1)</script> không chạy.
[ ] onerror/onload/onclick bị loại.
[ ] javascript: URL bị loại.
[ ] SVG upload bị chặn hoặc sanitize.
[ ] HTML/JS file upload bị chặn.
```

### 18.7 Build

```bash
dotnet restore
dotnet build
dotnet test
```

Nếu có frontend sửa:

```bash
cd frontend/pems-react
npm install
npm run build
npm run lint
npm run typecheck
```

Nếu không có lint/typecheck script, ghi rõ không có script.

---

## 19. Output bắt buộc sau khi hoàn thành

Tạo/cập nhật các file:

```text
docs/auth/AUTH_HARDENING_INVENTORY.md
docs/auth/AUTH_HARDENING_REPORT.md
docs/auth/AUTH_HARDENING_TEST_CASES.md
docs/auth/AUTH_SECURITY_CHECKLIST.md
docs/auth/AUTH_ERROR_CODES.md
docs/database/DATABASE_DEPLOYMENT.md nếu có SQL patch
docs/architecture/REFACTOR_CHANGELOG.md
database/scripts/patch_auth_hardening_sessions.sql nếu cần
database/scripts/cleanup_expired_user_sessions.sql nếu cần
```

`AUTH_HARDENING_REPORT.md` format:

```markdown
# Auth Hardening Report

## 1. Summary

## 2. Scope
### Included
### Excluded

## 3. Files changed

## 4. Production error handling

## 5. Refresh token/session changes

## 6. Revoke session rules

## 7. CORS/HTTPS/cookie/CSRF

## 8. XSS hardening

## 9. Database changes / SQL patch

## 10. Manual test results

## 11. Build/test result

## 12. Remaining TODOs

## 13. Risks
```

`REFACTOR_CHANGELOG.md` format:

```markdown
# Refactor Changelog

## Summary

## Files Changed

## Backend Changes

## Frontend Changes

## Database Changes

## Security Changes

## Commands Run

## Remaining TODOs
```

---

## 20. Thứ tự triển khai đề xuất

Làm đúng thứ tự để tránh phá auth:

```text
PHASE 1  — Inventory/Audit.
PHASE 2  — Production error handling.
PHASE 3  — Refresh token hash/rotation review.
PHASE 4  — Logout/revoke session chuẩn.
PHASE 5  — Revoke all sessions khi đổi role/campus/status/password.
PHASE 6  — user_sessions index + cleanup SQL.
PHASE 7  — CORS config theo environment.
PHASE 8  — HTTPS/cookie secure config.
PHASE 9  — CSRF strategy nếu dùng cookie.
PHASE 10 — XSS sanitize/render/upload guard.
PHASE 11 — Security headers optional.
PHASE 12 — Error code docs sync.
PHASE 13 — Test/build/docs.
```

---

## 21. Tiêu chí nghiệm thu cuối cùng

Chỉ báo hoàn thành khi đạt đủ:

```text
[ ] Không phá login credentials dev/test.
[ ] Không phá Google SSO.
[ ] FEID vẫn tắt/ẩn đúng như hiện tại nếu chưa làm provider thật.
[ ] Không phá frontend RBAC/redirect đã fix.
[ ] Production không lộ stackTrace.
[ ] Refresh token không lưu plain text nếu đã có điều kiện sửa DB/code.
[ ] Logout revoke được session hiện tại.
[ ] Đổi role/campus/status/password revoke session cũ.
[ ] user_sessions có index/cleanup.
[ ] CORS production không AllowAnyOrigin.
[ ] HTTPS production config đúng.
[ ] Cookie nếu dùng có HttpOnly/Secure/SameSite.
[ ] Có CSRF decision rõ nếu dùng cookie.
[ ] HTML rich text được sanitize.
[ ] Upload file nguy hiểm bị chặn.
[ ] Error codes backend/frontend/docs đồng bộ.
[ ] dotnet build pass.
[ ] npm run build pass nếu có sửa frontend.
[ ] Có changelog/report/test cases.
```

---

## 22. Kết luận bắt buộc

Kết quả cuối cùng phải giữ nguyên định hướng:

```text
PEMS tiếp tục dùng hybrid authentication:
JWT-based authentication kết hợp database-backed session management,
theo kiến trúc Dual Portal và SSO-first.
```

Không thay đổi nền tảng auth.  
Chỉ harden để đủ an toàn trước production.
