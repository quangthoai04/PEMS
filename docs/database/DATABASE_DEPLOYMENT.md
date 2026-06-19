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

- `appsettings.Production.json` chỉ override `Cors:AllowedOrigins`, `AllowedHosts`, `Logging`.
- Secrets (JWT `SecretKey`, `ConnectionStrings:DefaultConnection`, `Smtp:Password`) PHẢI set qua
  biến môi trường / secret manager ở production — KHÔNG commit secret thật.
  Ví dụ (env override key lồng nhau dùng `__`):
  ```bash
  ConnectionStrings__DefaultConnection="server=...;database=...;user=...;password=..."
  JwtSettings__SecretKey="<random-32+ bytes>"
  Smtp__Password="<app-password>"
  ```
