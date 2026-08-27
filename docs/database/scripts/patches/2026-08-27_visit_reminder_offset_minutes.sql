-- =============================================================================================
-- 2026-08-27 — VisitProcess "Nhắc nhở chuyến thăm": days_before + reminder_time → offset_minutes
--
-- VẤN ĐỀ
--   visit_instance_reminder_settings lưu "days_before" (số ngày, INT) + "reminder_time" (một giờ
--   cố định trong ngày, TIME) rồi tính scheduled_at = DATE(planned_start_at) - days_before ngày +
--   reminder_time. Đây là công thức "ngày lịch + giờ tùy ý", KHÔNG PHẢI phép trừ thời lượng thật
--   từ thời điểm bắt đầu chuyến thăm — "1 ngày trước" chỉ trùng đúng giờ visit khi reminder_time
--   được set bằng đúng giờ đó, và hoàn toàn không biểu diễn được mốc dưới 1 ngày (10 phút, 30
--   phút, 1 giờ, 2 giờ trước).
--
--   UI mới (2 card Người phụ trách / Thành phần tham gia, mỗi card 1 dropdown "Nhắc trước") cần
--   semantics thật: scheduled_at = planned_start_at - offset_minutes, để "1 ngày trước" luôn là
--   "đúng giờ đó của ngày hôm trước" và các mốc dưới-ngày mới biểu diễn được.
--
-- THAY ĐỔI
--   Cột mới offset_minutes (INT UNSIGNED NOT NULL) thay thế days_before + reminder_time.
--   scheduled_at KHÔNG đổi ý nghĩa (vẫn là thời điểm hệ thống sẽ gửi) — chỉ đổi CÁCH nó được
--   tính ở tầng ứng dụng từ lần save kế tiếp trở đi.
--
-- BACKFILL (an toàn cho reminder đang PENDING)
--   offset_minutes = TIMESTAMPDIFF(MINUTE, scheduled_at, planned_start_at) của chính visit_instance
--   đó — dùng ĐÚNG giá trị scheduled_at đã lưu sẵn, nên một reminder đang PENDING vẫn gửi ĐÚNG thời
--   điểm cũ, không hề bị dịch chuyển bởi patch này. Trường hợp dữ liệu cũ có scheduled_at >=
--   planned_start_at (âm/0 phút — lẽ ra không hợp lệ theo rule "phải trước giờ bắt đầu" nhưng có
--   thể sót ở dữ liệu demo cũ) được kẹp về tối thiểu 1 phút để cột NOT NULL luôn có nghĩa.
--
-- WHICH FILE TO RUN ON AN EXISTING DATABASE
--   THIS ONE. docs/database/scripts/PEMS_FULL_VS_31_07_NEW.sql đã có offset_minutes ngay từ đầu
--   cho một lần import mới — chạy nó vào DB sống sẽ dựng lại toàn bộ schema.
--
-- IDEMPOTENT — mỗi bước tự kiểm tra information_schema trước khi đổi; chạy lại nhiều lần an toàn.
--
-- ROLLBACK — xem §5 cuối file (khôi phục days_before/reminder_time là mất thông tin vì phép biến
-- đổi offset_minutes → (days_before, reminder_time) không phải song ánh; ROLLBACK thật cần backup).
--
-- LƯU Ý MySQL Workbench "Safe Updates": §2 UPDATE theo offset_minutes (không phải cột khóa) nên
-- Workbench mặc định chặn với Error 1175. Script tự tắt SQL_SAFE_UPDATES ở phạm vi session và trả
-- lại giá trị cũ ở cuối — không cần đổi Preferences tay.
-- =============================================================================================
SET @pems_old_safe_updates := @@SESSION.SQL_SAFE_UPDATES;
SET SESSION SQL_SAFE_UPDATES = 0;

-- ── §1. Thêm cột offset_minutes (nullable trước, để backfill) — idempotent ─────────────────────
SET @col_exists := (
  SELECT COUNT(*) FROM information_schema.COLUMNS
  WHERE TABLE_SCHEMA = DATABASE()
    AND TABLE_NAME = 'visit_instance_reminder_settings'
    AND COLUMN_NAME = 'offset_minutes');

SET @sql := IF(@col_exists = 0,
  'ALTER TABLE visit_instance_reminder_settings
     ADD COLUMN offset_minutes INT UNSIGNED NULL
       COMMENT ''Số phút nhắc trước planned_start_at (Nhắc trước bao lâu)''
       AFTER target_group',
  'SELECT ''[skip] cột offset_minutes đã tồn tại''');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

-- ── §2. Backfill từ scheduled_at đã lưu (giữ nguyên mốc gửi của reminder đang PENDING) ──────────
UPDATE visit_instance_reminder_settings r
JOIN visit_request_campuses c ON c.visit_instance_id = r.visit_instance_id
SET r.offset_minutes = GREATEST(TIMESTAMPDIFF(MINUTE, r.scheduled_at, c.planned_start_at), 1)
WHERE r.offset_minutes IS NULL;

-- Dòng mồ côi (visit_instance_id không còn tồn tại — không nên xảy ra vì có FK CASCADE, nhưng
-- kiểm tra cho chắc): nếu còn NULL sau backfill, gán tạm 1440 phút (1 ngày) để cột NOT NULL ở
-- §3 không chặn migration; dữ liệu này chỉ là rác lịch sử, không ảnh hưởng reminder sống.
UPDATE visit_instance_reminder_settings
SET offset_minutes = 1440
WHERE offset_minutes IS NULL;

-- ── §3. Bắt buộc NOT NULL (idempotent — chạy lại vẫn an toàn) ───────────────────────────────────
ALTER TABLE visit_instance_reminder_settings
  MODIFY COLUMN offset_minutes INT UNSIGNED NOT NULL
    COMMENT 'Số phút nhắc trước planned_start_at (Nhắc trước bao lâu)';

-- ── §4. Xóa 2 cột cũ đã được offset_minutes thay thế — idempotent ───────────────────────────────
SET @col_exists := (
  SELECT COUNT(*) FROM information_schema.COLUMNS
  WHERE TABLE_SCHEMA = DATABASE()
    AND TABLE_NAME = 'visit_instance_reminder_settings'
    AND COLUMN_NAME = 'days_before');
SET @sql := IF(@col_exists > 0,
  'ALTER TABLE visit_instance_reminder_settings DROP COLUMN days_before',
  'SELECT ''[skip] cột days_before đã được xóa''');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @col_exists := (
  SELECT COUNT(*) FROM information_schema.COLUMNS
  WHERE TABLE_SCHEMA = DATABASE()
    AND TABLE_NAME = 'visit_instance_reminder_settings'
    AND COLUMN_NAME = 'reminder_time');
SET @sql := IF(@col_exists > 0,
  'ALTER TABLE visit_instance_reminder_settings DROP COLUMN reminder_time',
  'SELECT ''[skip] cột reminder_time đã được xóa''');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

-- ── §5. Verify ───────────────────────────────────────────────────────────────────────────────
-- Kỳ vọng: zero_or_negative_offset = 0, null_offset = 0, days_before/reminder_time không còn tồn tại.
SELECT
  COUNT(*)                                   AS total_rows,
  SUM(offset_minutes IS NULL)                AS null_offset,
  SUM(offset_minutes <= 0)                   AS zero_or_negative_offset
FROM visit_instance_reminder_settings;

SELECT COUNT(*) AS legacy_columns_remaining
FROM information_schema.COLUMNS
WHERE TABLE_SCHEMA = DATABASE()
  AND TABLE_NAME = 'visit_instance_reminder_settings'
  AND COLUMN_NAME IN ('days_before', 'reminder_time');

-- Reminder đang PENDING phải vẫn khớp scheduled_at cũ theo công thức mới (kỳ vọng: 0 dòng lệch).
SELECT r.reminder_setting_id, r.scheduled_at, c.planned_start_at, r.offset_minutes
FROM visit_instance_reminder_settings r
JOIN visit_request_campuses c ON c.visit_instance_id = r.visit_instance_id
WHERE r.status = 'PENDING'
  AND c.planned_start_at - INTERVAL r.offset_minutes MINUTE <> r.scheduled_at;

SET SESSION SQL_SAFE_UPDATES = @pems_old_safe_updates;

-- ── §6. Rollback (chạy tay nếu cần, cần backup cho days_before/reminder_time thật) ──────────────
-- ALTER TABLE visit_instance_reminder_settings ADD COLUMN days_before INT UNSIGNED NOT NULL DEFAULT 0;
-- ALTER TABLE visit_instance_reminder_settings ADD COLUMN reminder_time TIME NOT NULL DEFAULT '09:00:00';
-- ALTER TABLE visit_instance_reminder_settings DROP COLUMN offset_minutes;
