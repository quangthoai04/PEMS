# PEMS Core Auth Backend — Dual Portal (SSO-first)

Trạng thái triển khai của Core Authentication backend theo
`docs/authentication/PEMS_CORE_AUTH_BACKEND_DUAL_PORTAL_IMPLEMENTATION_PROMPT.md`.

> TL;DR: Các UC core (UC-10 SSO Google, UC-11 Credentials, UC-12 Logout, UC-13 Forgot
> Password, GET /auth/me, GET /auth/permissions) đã chạy thật. Phần bổ sung mới nhất là
> **FEID adapter có kiểm soát** (không fake login) + tài liệu/error-code.

---

## 1. Tổng quan kiến trúc

- **Controller** (`AuthenticationController`) chỉ nhận DTO, set IP/UserAgent từ HttpContext,
  rồi `IMediator.Send(...)`. Không có business logic, không gọi DbContext trực tiếp.
- **Handlers** (MediatR) chứa orchestration; rule portal/campus/role được áp dụng inline kèm
  ghi `login_logs` + `security_events` ngay tại điểm fail (xem mục 5).
- **Validators** (FluentValidation, chạy qua `ValidationBehaviour`) chỉ kiểm tra format input.
- **AuthOptions** (singleton, bind từ section `"AuthOptions"`) điều khiển login mode và provider flags.
- **Error contract**: mọi lỗi nghiệp vụ auth ném `AuthBusinessException(errorCode, message, statusCode)`;
  `ExceptionHandlingMiddleware` map thành `{ success:false, errorCode, message }` với đúng HTTP status.

## 2. Endpoint

| Method | Route | Command/Query | Auth |
|---|---|---|---|
| POST | `/api/auth/login` | `LoginviaCredentialsCommand` | Anonymous |
| POST | `/api/auth/google` | `LoginviaSSOCommand` (Google ID token) | Anonymous |
| POST | `/api/auth/feid` | `LoginviaFeidCommand` | Anonymous |
| POST | `/api/auth/refresh` | `RefreshTokenCommand` | Anonymous |
| POST | `/api/auth/logout` | `LogoutCommand` | Bearer |
| GET | `/api/auth/me` | `GetCurrentUserQuery` | Bearer |
| GET | `/api/auth/permissions` | `GetCurrentUserPermissionsQuery` | Bearer |
| POST | `/api/auth/forgot-password` | `ForgotPasswordCommand` | Anonymous |
| POST | `/api/auth/reset-password` | `ResetPasswordCommand` | Anonymous |

## 3. Login mode (AuthOptions)

```jsonc
"AuthOptions": {
  "LoginMode": "DevMixed",            // DevMixed | ProductionSsoOnly
  "AllowPasswordLogin": true,
  "AllowGoogleSso": true,
  "AllowFeid": false,
  "AutoCreateVisitorOnExternalLogin": true,
  "AutoCreateInternalOnExternalLogin": false,  // luôn bị bỏ qua: internal KHÔNG auto-create
  "StudentFeidMinCohort": "K19"
}
```

- `PasswordLoginEnabled = AllowPasswordLogin && LoginMode != ProductionSsoOnly`.
  → `ProductionSsoOnly` chặn password login tuyệt đối (`PASSWORD_LOGIN_DISABLED`).
- Internal account **không bao giờ** được auto-create dù cấu hình gì (`AutoCreateInternalOnExternalLogin`
  chỉ để hiển thị parity với spec).

## 4. Dual portal — luồng nghiệp vụ

### VISITOR portal
- **V1** email chưa tồn tại + `AutoCreateVisitorOnExternalLogin=true` → auto-provision VISITOR
  (campus/department/password = NULL, `created_via = SSO_AUTO_PROVISION`), link provider, issue token.
- **V2** email là VISITOR đang ACTIVE → ensure provider link, issue token.
- **V3** email là role internal → `403 WRONG_PORTAL_INTERNAL_ACCOUNT` (không đổi role, không tạo mới).

### INTERNAL portal
- **I0** thiếu `selectedCampusId` → `400 CAMPUS_REQUIRED`.
- **I1** email chưa tồn tại → `403 INTERNAL_ACCOUNT_NOT_FOUND` (KHÔNG auto-create).
- **I2** email là VISITOR → `403 WRONG_PORTAL_VISITOR_ACCOUNT`.
- **I3** internal + campus khớp `PrimaryCampusId` → success; campus sai → `403 CAMPUS_MISMATCH`.

Credentials login (`LoginviaCredentialsCommand`) áp dụng **cùng** policy portal/campus/role, nhưng chỉ
sau khi verify password (chống account enumeration). Có lockout: `failed_login_count` + `locked_until`
(cấu hình `Security:MaxFailedLoginAttempts` / `Security:LockoutMinutes`).

## 5. Audit
Mỗi điểm thành công/thất bại đều ghi:
- `login_logs` — SUCCESS / FAILED / BLOCKED + failureReason nội bộ.
- `security_events` — LOGIN_SUCCESS / LOGIN_FAILED / LOGIN_BLOCKED / ACCOUNT_LOCKED / LOGOUT /
  PASSWORD_RESET_REQUESTED.

## 6. Session & token
- Login thành công → tạo `user_sessions` (DB-backed) + JWT access token (claims: userId, email,
  roleCode, sessionId/jti, ...) + refresh token.
- `SessionValidationMiddleware`: với mọi request đã authenticate, check session còn active + user/role
  còn ACTIVE; nếu không → `401`. Logout/deactivate/role-disable có hiệu lực ngay.
- `LogoutCommand` revoke session gắn với access token hiện tại (và session của refresh token nếu khác).

## 7. Forgot password (UC-13)
- Chỉ phát OTP cho account có `LOCAL_PASSWORD` (password hash hoặc provider LOCAL_PASSWORD enabled) và ACTIVE.
- Account chỉ-SSO → **không** reset local password.
- Luôn trả message generic (chống account enumeration).

## 8. FEID adapter (mới)

FEID hiện **chưa có provider/credential thật**. Theo nguyên tắc "không fake SSO/FEID success", FEID được
triển khai như một adapter có kiểm soát:

- `IFeidIdentityVerifier` (Application) → `FeidIdentityVerifier` (Infrastructure), đọc section `"Feid"`
  (`BaseUrl`/`ClientId`/`ClientSecret`).
- `POST /api/auth/feid` → `LoginviaFeidCommand`:
  1. `AllowFeid = false` → `403 FEID_DISABLED`.
  2. Verifier khi chưa cấu hình → ném `403 FEID_NOT_CONFIGURED` (không tạo user, không fake email).
  3. Ghi `login_logs`/`security_events` cho lần thất bại.
- Khi có provider thật: điền phần verify trong `FeidIdentityVerifier` (trả `ExternalIdentityResult`,
  enforce `StudentFeidMinCohort` → `FEID_NOT_ELIGIBLE`), rồi áp dụng **đúng** dual-portal policy như
  `LoginviaSSOCommandHandler` (xem mục 4) và issue qua `AuthResultBuilder`.

> Lưu ý kiến trúc: codebase dùng validator riêng từng provider (`IGoogleTokenValidator`), nên FEID cũng
> dùng `IFeidIdentityVerifier` riêng thay vì một `IExternalIdentityVerifier` gộp như gợi ý trong spec §11.

## 9. Known limitations
- **Google SSO** cần `GoogleAuth:ClientId` thật để verify token; rỗng → token bị từ chối (`EXTERNAL_AUTH_FAILED`).
- **FEID** chưa tích hợp endpoint/credential thật → luôn trả `FEID_NOT_CONFIGURED`. Post-verify dual-portal
  flow cho FEID chưa được nối (dùng `LoginviaSSOCommandHandler` làm reference khi tích hợp).
- `ExceptionHandlingMiddleware` vẫn trả `error`/`stackTrace` ở nhánh 500 (phục vụ debug dev) — nên gate
  theo môi trường trước khi lên production.
