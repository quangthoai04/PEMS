# Database Deployment — Auth Hardening

> Liên quan phase Auth Hardening 2026-06-20. PEMS theo hướng database-first / manual SQL.
> KHÔNG dùng `dotnet ef database update` / auto-migrate cho các thay đổi này.

## Thứ tự chạy

1. Schema gốc (đã có): `database/scripts/pems_full.sql` — đã chứa bảng `user_sessions` cùng
   các index: `uq_sessions_refresh_hash`, `idx_sessions_user_active`, `idx_sessions_refresh_active`,
   `idx_sessions_portal_campus`, `idx_sessions_ip_time`.

2. Patch index cleanup (idempotent):
   ```bash
   mysql -u <user> -p <database> < database/scripts/patch_auth_hardening_sessions.sql
   ```
   - Thêm `idx_sessions_expires_at`, `idx_sessions_revoked_at` nếu chưa có.
   - Chạy lại nhiều lần an toàn (kiểm tra `information_schema.statistics`).

3. Cleanup session hết hạn/revoked (chạy định kỳ):
   ```bash
   mysql -u <user> -p <database> < database/scripts/cleanup_expired_user_sessions.sql
   ```
   - Xóa session `expires_at < now AND revoked_at IS NOT NULL`.
   - KHÔNG đụng `login_logs`, `security_events`.
   - Khuyến nghị lên lịch hằng ngày (Windows Task Scheduler / cron / MySQL EVENT).

## MySQL EVENT (tùy chọn) cho cleanup tự động

```sql
-- Cần SET GLOBAL event_scheduler = ON; (hoặc cấu hình my.cnf)
CREATE EVENT IF NOT EXISTS ev_cleanup_user_sessions
ON SCHEDULE EVERY 1 DAY
DO
  DELETE FROM user_sessions
  WHERE expires_at < UTC_TIMESTAMP()
    AND revoked_at IS NOT NULL;
```

## Cấu hình production (không thuộc DB nhưng liên quan deploy)

- `appsettings.Production.json` chỉ override `Cors:AllowedOrigins`, `AllowedHosts`, `Logging` — KHÔNG chứa secret.
- `appsettings.json` (base) đang chứa secret DEV (JWT secret, DB password, SMTP app password). Ở production
  các giá trị này PHẢI bị override bằng biến môi trường / secret manager. `WebApplication.CreateBuilder`
  đã nạp Environment Variables sau cùng nên env tự override file config — không cần đổi code.
- Quy ước key lồng nhau: dấu `:` trong key config → `__` (double underscore) trong env var.

### Biến môi trường bắt buộc set ở production

```bash
# Bắt buộc (secrets)
ConnectionStrings__DefaultConnection="server=...;port=3306;database=pems_db;user=...;password=...;AllowUserVariables=True;GuidFormat=None"
JwtSettings__SecretKey="<random >= 32 bytes, KHÔNG dùng giá trị dev>"
Smtp__Password="<gmail-app-password / smtp password>"
Smtp__User="<smtp user>"
Smtp__FromEmail="<from email>"

# Bắt buộc (môi trường + domain)
ASPNETCORE_ENVIRONMENT="Production"
AllowedHosts="pems.fpt.edu.vn;api.pems.fpt.edu.vn"       # thay domain thật
Cors__AllowedOrigins__0="https://pems.fpt.edu.vn"        # thay domain frontend thật

# Tuỳ chọn (nếu bật provider tương ứng)
GoogleAuth__ClientId="<google-client-id-production>.apps.googleusercontent.com"
Feid__ClientId="..."
Feid__ClientSecret="..."        # chỉ khi FEID có provider thật; mặc định AllowFeid=false
```

> ⚠️ SMTP app password `Smtp:Password` đang nằm trong `appsettings.json` (dev) đã bị commit vào git.
> Trước khi deploy production: **rotate (đổi) app password này**, set giá trị mới qua env, và không commit secret mới.

### Checklist production

- [ ] `ASPNETCORE_ENVIRONMENT=Production`.
- [ ] Tất cả secret (JWT/DB/SMTP) đọc từ env/secret manager, không từ file commit.
- [ ] `Cors:AllowedOrigins` = domain frontend thật (không `*`, không `AllowAnyOrigin`).
- [ ] `AllowedHosts` = domain API thật.
- [ ] Google OAuth Authorized JavaScript origins đã thêm domain frontend thật.
- [ ] Backend chạy HTTPS; frontend gọi HTTPS API.
- [ ] FEID vẫn `AllowFeid=false` nếu chưa có credential thật.
- [ ] Production 500 không trả `stackTrace` (đã verify ở `ExceptionHandlingMiddleware`).
