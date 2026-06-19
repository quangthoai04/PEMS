-- =============================================================================
-- patch_auth_hardening_sessions.sql
-- Mục tiêu: bổ sung index cho user_sessions phục vụ JOB CLEANUP session
--           hết hạn / đã revoke (quét theo expires_at, revoked_at).
--
-- Bối cảnh: schema gốc (database/scripts/pems_full.sql) ĐÃ có sẵn:
--   - UNIQUE uq_sessions_refresh_hash (refresh_token_hash)        -> refresh lookup
--   - idx_sessions_user_active (user_id, revoked_at, expires_at)  -> revoke-all theo user
--   - idx_sessions_refresh_active (refresh_token_hash, refresh_revoked_at, refresh_expires_at)
--   - idx_sessions_portal_campus, idx_sessions_ip_time
-- Patch này CHỈ thêm 2 index thuần (expires_at) và (revoked_at) mà schema gốc chưa có,
-- giúp DELETE cleanup không phải full-scan.
--
-- An toàn: idempotent (kiểm tra information_schema.statistics trước khi tạo),
--          KHÔNG destructive, KHÔNG đổi cột, chạy lại nhiều lần không lỗi.
-- Áp dụng: MySQL 8.0. Đặt @db = đúng tên database trước khi chạy nếu khác 'pems_db'.
-- =============================================================================

SET @db := DATABASE();

-- 1) Index theo expires_at — phục vụ cleanup quét session đã hết hạn.
SET @exists := (
  SELECT COUNT(*) FROM information_schema.statistics
  WHERE table_schema = @db
    AND table_name   = 'user_sessions'
    AND index_name   = 'idx_sessions_expires_at'
);
SET @sql := IF(@exists = 0,
  'CREATE INDEX idx_sessions_expires_at ON user_sessions (expires_at)',
  'SELECT "idx_sessions_expires_at already exists" AS info');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

-- 2) Index theo revoked_at — phục vụ lọc session đã revoke khi cleanup.
SET @exists := (
  SELECT COUNT(*) FROM information_schema.statistics
  WHERE table_schema = @db
    AND table_name   = 'user_sessions'
    AND index_name   = 'idx_sessions_revoked_at'
);
SET @sql := IF(@exists = 0,
  'CREATE INDEX idx_sessions_revoked_at ON user_sessions (revoked_at)',
  'SELECT "idx_sessions_revoked_at already exists" AS info');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

-- Lưu ý: KHÔNG cần thêm index cho refresh_token_hash / user_id / session_id —
-- chúng đã được phủ bởi các index/unique key có sẵn trong pems_full.sql.
