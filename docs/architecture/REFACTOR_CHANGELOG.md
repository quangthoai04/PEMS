# Refactor Changelog

## 2026-06-19 — Core Auth Backend: FEID controlled adapter + docs

Theo `docs/authentication/PEMS_CORE_AUTH_BACKEND_DUAL_PORTAL_IMPLEMENTATION_PROMPT.md`.
Hầu hết Core Auth backend đã hoàn thành ở các đợt trước (xem mục "Đã có sẵn"). Đợt này
chỉ làm các phần còn thiếu.

### 1. Summary
Bổ sung **FEID** như một adapter có kiểm soát (không fake login success), thêm error code,
config và tài liệu auth. Không động vào luồng Google SSO / Credentials đang chạy tốt.

### 2. UC đã hoàn thành (trạng thái)
- UC-10 Login via SSO (Google) — ✅ đã có, chạy thật.
- UC-11 Login via Credentials — ✅ đã có (dev/test, lockout).
- UC-12 Logout — ✅ revoke session.
- UC-13 Forgot Password — ✅ chỉ LOCAL_PASSWORD, message generic.
- GET /auth/me, GET /auth/permissions — ✅ đọc DB/session mới nhất.
- FEID — ✅ adapter có kiểm soát (`FEID_NOT_CONFIGURED`), chưa có provider thật.

### 3. Files changed (đợt này)
Mới:
- `backend/PEMS.Application/Authentication/Models/ExternalIdentityResult.cs`
- `backend/PEMS.Application/Common/Interfaces/IFeidIdentityVerifier.cs`
- `backend/PEMS.Infrastructure/Identity/FeidIdentityVerifier.cs`
- `backend/PEMS.Application/Authentication/Commands/LoginViaFeid/{Command,Validator,Handler}.cs`
- `docs/auth/AUTH_CORE_BACKEND_DUAL_PORTAL.md`, `docs/auth/AUTH_ERROR_CODES.md`

Sửa:
- `backend/PEMS.Application/Common/Security/AuthErrorCodes.cs` — thêm `FEID_NOT_CONFIGURED`, `FEID_NOT_ELIGIBLE`.
- `backend/PEMS.Infrastructure/DependencyInjection.cs` — đăng ký `IFeidIdentityVerifier`.
- `backend/PEMS.Api/Controllers/AuthenticationController.cs` — thêm `POST /api/auth/feid`.
- `backend/PEMS.Api/appsettings.json` — thêm section `"Feid"`.
- `frontend/pems-react/src/features/authentication/api/authError.ts` — map FEID codes.

### 4. Backend logic implemented
- `POST /api/auth/feid`: gate `AllowFeid` (`FEID_DISABLED`) → `IFeidIdentityVerifier.VerifyAsync`
  (`FEID_NOT_CONFIGURED` khi chưa cấu hình) → ghi `login_logs`/`security_events` cho lần thất bại.
- Verifier đọc `"Feid"` config; chưa có `BaseUrl/ClientId/ClientSecret` → ném coded error, không fake user.

### 5. Config added/updated
- `appsettings.json`: thêm `"Feid": { "BaseUrl":"", "ClientId":"", "ClientSecret":"" }`.

### 6. Error codes added
- `FEID_NOT_CONFIGURED` (403), `FEID_NOT_ELIGIBLE` (403). Xem `docs/auth/AUTH_ERROR_CODES.md`.

### 7. Database changes / SQL patch
- Không có thay đổi DB đợt này. (Patch `created_via` ENUM cho Visitor auto-provision đã có ở
  `database/scripts/patch_auth_dual_portal_sso_first.sql`.)

### 8. Build/test result
- `dotnet build PEMS.Infrastructure` (kéo theo Application/Domain): **succeeded, 0 error**.
- `dotnet build PEMS.Api` (output ra thư mục tạm để tránh file-lock do dev server đang chạy):
  **succeeded, 0 error**. (Build vào bin gốc fail do MSB3021 — file bị PEMS.Api đang chạy khóa, không
  phải lỗi compile.)
- Frontend `tsc --noEmit` trên file đã sửa: 0 lỗi.

### 9. Deviations so với spec
- Spec §11 gợi ý một `IExternalIdentityVerifier` gộp (có tham số `provider`). Codebase đã chọn pattern
  validator riêng từng provider (`IGoogleTokenValidator`), nên FEID dùng `IFeidIdentityVerifier` riêng
  cho nhất quán. Google SSO giữ nguyên `IGoogleTokenValidator`.
- Spec §10 gợi ý `AuthPolicyService` tách riêng. Các handler hiện interleave policy với ghi audit-log
  ngay tại điểm fail (đầy đủ hơn signature `void Ensure...` của spec). Giữ nguyên để không mất audit
  trail của các lần login thất bại; không refactor code đang chạy.
- Tài liệu đặt tại `docs/auth/` đúng tên spec yêu cầu (folder auth docs cũ là `docs/authentication/`).

### 10. Known limitations
- Google SSO cần `GoogleAuth:ClientId` thật mới verify được token.
- FEID chưa có provider/credential thật → luôn `FEID_NOT_CONFIGURED`; post-verify dual-portal flow chưa
  nối (dùng `LoginviaSSOCommandHandler` làm reference khi tích hợp).
- `ExceptionHandlingMiddleware` nhánh 500 vẫn trả `error`/`stackTrace` (debug dev) — nên gate theo môi
  trường trước production.

### 11. TODO next phase
- Tích hợp FEID provider thật + enforce `StudentFeidMinCohort`.
- Cân nhắc gate stackTrace ở 500 theo `IsDevelopment()`.
- Account Management UI wiring (AccountManagement.tsx vẫn dùng mock data).
