-- =============================================================================
-- cleanup_expired_user_sessions.sql
-- Mục tiêu: dọn các dòng user_sessions đã HẾT HẠN và ĐÃ REVOKE.
--
-- Quy tắc an toàn:
--   - CHỈ xóa session vừa expired vừa revoked (revoked_at IS NOT NULL).
--   - GIỮ session active (revoked_at IS NULL) kể cả khi đã quá expires_at,
--     để còn audit/điều tra; nếu muốn dọn cả expired-but-not-revoked thì
--     đổi điều kiện một cách có chủ đích.
--   - KHÔNG đụng tới login_logs và security_events (audit phải giữ).
--
-- Chạy định kỳ (cron/scheduled task) hằng ngày. MySQL 8.0.
-- =============================================================================

-- Mặc định: xóa session đã revoke và đã quá hạn.
DELETE FROM user_sessions
WHERE expires_at < UTC_TIMESTAMP()
  AND revoked_at IS NOT NULL;

-- Tùy chọn (giữ audit lâu hơn): chỉ dọn sau 30 ngày kể từ khi hết hạn.
-- DELETE FROM user_sessions
-- WHERE expires_at < DATE_SUB(UTC_TIMESTAMP(), INTERVAL 30 DAY)
--   AND revoked_at IS NOT NULL;

-- Số dòng bị xóa: dùng ROW_COUNT() ngay sau DELETE để log nếu cần.
SELECT ROW_COUNT() AS deleted_sessions;
