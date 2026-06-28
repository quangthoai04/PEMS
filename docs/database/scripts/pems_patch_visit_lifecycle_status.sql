-- =============================================================================
-- PEMS — Patch: vòng đời tiếp khách (hủy PENDING_APPROVAL) + điều kiện đóng đoàn
-- Áp dụng cho schema pems_full_v10_new_final_email_rich_editor_full_fixed_status.sql
--
-- Tài liệu nguồn: docs/delegation/status/PEMS_VISIT_LIFECYCLE_LOGISTICS_STATUS_REQUIREMENTS.md
--   • §1.1 / §4.1: Visitor được hủy đơn khi đơn còn PENDING_APPROVAL (không chỉ APPROVED).
--   • §10:        Điều kiện đóng đoàn cần cờ "Host xác nhận chuyến này không cần bài tin tức".
--
-- LƯU Ý:
--   • Patch này PHẢI được chạy TRƯỚC khi deploy backend mới — backend ánh xạ cột
--     visit_request_campuses.news_not_required; thiếu cột sẽ làm mọi truy vấn campus lỗi.
--   • ALTER TABLE ADD COLUMN bên dưới chạy MỘT lần (MySQL không hỗ trợ IF NOT EXISTS ổn định).
--   • Chạy bằng mysql CLI (hỗ trợ lệnh DELIMITER).
-- =============================================================================

-- ── §1.1: cho phép Visitor hủy đơn PENDING_APPROVAL (ngoài APPROVED) ─────────
-- Trigger gốc chỉ cho APPROVED -> CANCELLED. Mở rộng để PENDING_APPROVAL -> CANCELLED
-- cũng hợp lệ, VẪN chỉ dành cho người hủy có role VISITOR (Host hủy campus instance,
-- không bao giờ flip đơn tổng). Các ràng buộc cancelled_by/cancelled_at/cancellation_reason
-- giữ nguyên.
DROP TRIGGER IF EXISTS trg_visit_requests_cancel_validate_bu;

DELIMITER $$
CREATE TRIGGER trg_visit_requests_cancel_validate_bu
BEFORE UPDATE ON visit_requests
FOR EACH ROW
BEGIN
  DECLARE v_cancel_role_code VARCHAR(30);

  IF NEW.status = 'CANCELLED' AND OLD.status <> 'CANCELLED' THEN
    -- §1.1: PENDING_APPROVAL hoặc APPROVED đều có thể bị hủy; REJECTED/CLOSED/khác thì không.
    IF OLD.status NOT IN ('APPROVED', 'PENDING_APPROVAL') THEN
      SIGNAL SQLSTATE '45000'
        SET MESSAGE_TEXT = 'Only pending or approved request/delegation can be cancelled';
    END IF;

    IF NEW.cancelled_by IS NULL OR NEW.cancelled_at IS NULL THEN
      SIGNAL SQLSTATE '45000'
        SET MESSAGE_TEXT = 'cancelled_by and cancelled_at are required when request is cancelled';
    END IF;

    IF NEW.cancellation_reason IS NULL OR TRIM(NEW.cancellation_reason) = '' THEN
      SIGNAL SQLSTATE '45000'
        SET MESSAGE_TEXT = 'cancellation_reason is required when request/delegation is cancelled';
    END IF;

    SELECT r.role_code INTO v_cancel_role_code
    FROM users u
    JOIN roles r ON r.role_id = u.role_id
    WHERE u.user_id = NEW.cancelled_by;

    IF v_cancel_role_code <> 'VISITOR' THEN
      SIGNAL SQLSTATE '45000'
        SET MESSAGE_TEXT = 'Only VISITOR can cancel the main visit request';
    END IF;
  END IF;
END$$
DELIMITER ;

-- ── §10: cờ "không cần bài tin tức" cho điều kiện đóng đoàn ──────────────────
-- Host xác nhận chuyến này không cần tạo/duyệt bài tin tức. Điều kiện đóng đoàn:
-- có ít nhất 1 news PUBLISHED liên quan instance HOẶC news_not_required = 1.
ALTER TABLE visit_request_campuses
  ADD COLUMN news_not_required TINYINT(1) NOT NULL DEFAULT 0 AFTER close_note;
