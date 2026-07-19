-- =====================================================================
-- 2026-07-19 — Expense report "no expense" confirmation flag
-- Purpose:
--   Phòng ban sau khi ký nghiệm thu có thể bấm "Không có chi phí" thay vì
--   nhập bảng kê. Host dùng cờ này để biết đơn đã được xác nhận chi phí
--   (SAVED + no_expense) hay còn chờ nhập (DRAFT) để gửi nhắc nhở.
-- Idempotent: chạy lại không lỗi (kiểm tra information_schema trước khi ALTER).
-- =====================================================================

SET @col_exists := (
  SELECT COUNT(*) FROM information_schema.COLUMNS
  WHERE TABLE_SCHEMA = DATABASE()
    AND TABLE_NAME = 'visit_expense_reports'
    AND COLUMN_NAME = 'no_expense'
);

SET @ddl := IF(@col_exists = 0,
  'ALTER TABLE visit_expense_reports ADD COLUMN no_expense TINYINT(1) NOT NULL DEFAULT 0 COMMENT ''1 = phòng ban/host xác nhận không phát sinh chi phí cho báo cáo này'' AFTER report_note',
  'SELECT ''no_expense already exists'' AS notice');

PREPARE stmt FROM @ddl;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;
