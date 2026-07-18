-- =====================================================================
-- PEMS — PER-CAMPUS FORM v2 — ROLLBACK / DOWN  (DESTRUCTIVE)
-- Plan §17.1(5), §17.4, §24.3.
--
-- ⚠️  READ BEFORE RUNNING ⚠️
-- The SAFE rollback for a live system is NOT this script. It is:
--     1. Turn the feature flag PerCampusVisitFormV2 OFF.
--     2. Keep the backend running in dual-read mode (it still reads v1 global
--        columns, which were never dropped).
-- The v2 tables can stay in place indefinitely; they are additive.
--
-- This DOWN script DROPS the v2 tables and columns and is only acceptable when
-- BOTH hold (§24.3):
--     * no production v2 writes have happened yet (detail/link/identity/amendment
--       tables are empty or contain only reversible backfill), OR
--     * you have exported/confirmed a lossless restore path.
-- Dropping these tables DESTROYS per-campus detail, identity-change history and
-- amendment history. There is no way to reconstruct them from the v1 columns
-- once campuses have diverged (has_mixed_campus_details = 1).
--
-- MySQL DDL auto-commits; this cannot run inside a single rollback-able transaction.
-- =====================================================================

-- ---------------------------------------------------------------------
-- 0. Guard: refuse to proceed if v2 data that cannot be reconstructed exists.
--    Comment this block out ONLY after a conscious, backed-up decision.
-- ---------------------------------------------------------------------
SET @mixed_or_identity :=
  (SELECT
     (SELECT COUNT(*) FROM visit_requests WHERE has_mixed_campus_details = 1)
   + (SELECT COUNT(*) FROM visit_request_identity_changes)
   + (SELECT COUNT(*) FROM visit_instance_amendments));

-- Abort the script (with a clear error) ONLY when unreconstructable v2 data exists.
-- The failing text is placed inside a string that is prepared only on the true
-- branch, so a passing guard runs a harmless `SELECT 1`.
SET @guard := IF(@mixed_or_identity > 0,
  'SELECT `DOWN_REFUSED__unreconstructable_v2_data_present__back_up_and_edit_guard` FROM DUAL',
  'SELECT 1 AS guard_passed');
PREPARE guard_stmt FROM @guard;
EXECUTE guard_stmt;
DEALLOCATE PREPARE guard_stmt;
-- If it errors with "Unknown column 'DOWN_REFUSED__...'", the guard is doing its
-- job: there is v2 data that DROP would destroy. Stop, export/back up, then remove
-- this guard block consciously before re-running.

-- ---------------------------------------------------------------------
-- 1. Restore the original cancel trigger (drop the 3A branch).
--    This recreates the baseline trigger from
--    pems_full_v10_TTS_Gallery_FULL_UPDATED_NOTIFICATIONS_FIXED.sql.
-- ---------------------------------------------------------------------
DROP TRIGGER IF EXISTS trg_visit_requests_cancel_validate_bu;

DELIMITER $$
CREATE TRIGGER trg_visit_requests_cancel_validate_bu
BEFORE UPDATE ON visit_requests
FOR EACH ROW
BEGIN
  DECLARE v_cancel_role_code VARCHAR(30);
  DECLARE v_started_campus_count INT DEFAULT 0;
  DECLARE v_cancel_window_violation_count INT DEFAULT 0;

  IF NEW.status = 'CANCELLED' AND OLD.status <> 'CANCELLED' THEN
    IF OLD.status NOT IN ('APPROVED', 'PARTIALLY_APPROVED', 'PENDING_APPROVAL') THEN
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
    FROM users u JOIN roles r ON r.role_id = u.role_id
    WHERE u.user_id = NEW.cancelled_by;
    IF v_cancel_role_code <> 'VISITOR' THEN
      SIGNAL SQLSTATE '45000'
        SET MESSAGE_TEXT = 'Only VISITOR can cancel the main visit request';
    END IF;
    IF NEW.visitor_user_id IS NOT NULL AND NEW.cancelled_by <> NEW.visitor_user_id THEN
      SIGNAL SQLSTATE '45000'
        SET MESSAGE_TEXT = 'Only the contact owner (visitor_user_id) can cancel the main visit request';
    END IF;
    SELECT COUNT(*) INTO v_cancel_window_violation_count
    FROM visit_request_campuses vrc
    WHERE vrc.visit_request_id = OLD.visit_request_id
      AND vrc.status NOT IN ('CANCELLED','REJECTED')
      AND vrc.planned_start_at < DATE_ADD(NEW.cancelled_at, INTERVAL 24 HOUR);
    IF v_cancel_window_violation_count > 0 THEN
      SIGNAL SQLSTATE '45000'
        SET MESSAGE_TEXT = 'Visitor cannot cancel the main visit request within 24 hours of any active campus visit';
    END IF;
    IF OLD.status IN ('APPROVED','PARTIALLY_APPROVED') THEN
      SELECT COUNT(*) INTO v_started_campus_count
      FROM visit_request_campuses vrc
      WHERE vrc.visit_request_id = OLD.visit_request_id
        AND vrc.status IN ('DURING_VISIT','AFTER_VISIT','CLOSED');
      IF v_started_campus_count > 0 THEN
        SIGNAL SQLSTATE '45000'
          SET MESSAGE_TEXT = 'Request has campus visit(s) already started; cancel each not-yet-started campus instead of cancelling the whole request';
      END IF;
    END IF;
  END IF;
END$$
DELIMITER ;

-- ---------------------------------------------------------------------
-- 2. Drop v2 tables (children first to satisfy FKs).
-- ---------------------------------------------------------------------
DROP TABLE IF EXISTS visit_request_pending_forms;
DROP TABLE IF EXISTS visit_instance_amendment_changes;
DROP TABLE IF EXISTS visit_instance_amendments;
DROP TABLE IF EXISTS visit_instance_form_revision_history;
DROP TABLE IF EXISTS visit_request_revision_history;
DROP TABLE IF EXISTS visit_request_identity_change_events;
DROP TABLE IF EXISTS visit_request_identity_changes;
DROP TABLE IF EXISTS visit_instance_guest_members;
DROP TABLE IF EXISTS visit_instance_form_details;

-- ---------------------------------------------------------------------
-- 3. Drop additive constraints / indexes / columns (guarded).
-- ---------------------------------------------------------------------
DROP PROCEDURE IF EXISTS pems_v2_drop_column;
DROP PROCEDURE IF EXISTS pems_v2_drop_index;
DROP PROCEDURE IF EXISTS pems_v2_drop_constraint;

DELIMITER $$
CREATE PROCEDURE pems_v2_drop_column(IN p_table VARCHAR(64), IN p_column VARCHAR(64))
BEGIN
  IF EXISTS (SELECT 1 FROM information_schema.COLUMNS
             WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = p_table AND COLUMN_NAME = p_column) THEN
    SET @s = CONCAT('ALTER TABLE `', p_table, '` DROP COLUMN `', p_column, '`');
    PREPARE stmt FROM @s; EXECUTE stmt; DEALLOCATE PREPARE stmt;
  END IF;
END$$
CREATE PROCEDURE pems_v2_drop_index(IN p_table VARCHAR(64), IN p_index VARCHAR(64))
BEGIN
  IF EXISTS (SELECT 1 FROM information_schema.STATISTICS
             WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = p_table AND INDEX_NAME = p_index) THEN
    SET @s = CONCAT('ALTER TABLE `', p_table, '` DROP INDEX `', p_index, '`');
    PREPARE stmt FROM @s; EXECUTE stmt; DEALLOCATE PREPARE stmt;
  END IF;
END$$
CREATE PROCEDURE pems_v2_drop_constraint(IN p_table VARCHAR(64), IN p_constraint VARCHAR(64))
BEGIN
  IF EXISTS (SELECT 1 FROM information_schema.TABLE_CONSTRAINTS
             WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = p_table AND CONSTRAINT_NAME = p_constraint) THEN
    SET @s = CONCAT('ALTER TABLE `', p_table, '` DROP CHECK `', p_constraint, '`');
    PREPARE stmt FROM @s; EXECUTE stmt; DEALLOCATE PREPARE stmt;
  END IF;
END$$
DELIMITER ;

CALL pems_v2_drop_constraint('visit_request_campuses', 'ck_visit_instance_min_duration_30m');
CALL pems_v2_drop_index('visit_request_campuses', 'uq_vrc_request_instance');
CALL pems_v2_drop_index('visit_guest_members', 'uq_vgm_request_member');

CALL pems_v2_drop_index('audit_logs', 'idx_audit_visit_request_time');
CALL pems_v2_drop_index('audit_logs', 'idx_audit_visit_instance_time');
CALL pems_v2_drop_index('audit_logs', 'idx_audit_correlation');
CALL pems_v2_drop_index('audit_logs', 'idx_audit_source');
CALL pems_v2_drop_column('audit_logs', 'correlation_id');
CALL pems_v2_drop_column('audit_logs', 'visit_request_id');
CALL pems_v2_drop_column('audit_logs', 'visit_instance_id');
CALL pems_v2_drop_column('audit_logs', 'source_type');
CALL pems_v2_drop_column('audit_logs', 'source_id');
CALL pems_v2_drop_column('audit_logs', 'reason');

CALL pems_v2_drop_column('audit_log_changes', 'change_category');
CALL pems_v2_drop_column('audit_log_changes', 'value_format');
CALL pems_v2_drop_column('audit_log_changes', 'is_sensitive');
CALL pems_v2_drop_column('audit_log_changes', 'display_order');

CALL pems_v2_drop_index('visit_requests', 'idx_visit_requests_schema_version');
CALL pems_v2_drop_index('visit_requests', 'idx_visit_requests_contact_access');
CALL pems_v2_drop_column('visit_requests', 'primary_contact_verified_at');
CALL pems_v2_drop_column('visit_requests', 'primary_contact_access_status');
CALL pems_v2_drop_column('visit_requests', 'has_mixed_campus_details');
CALL pems_v2_drop_column('visit_requests', 'form_schema_version');

DROP PROCEDURE IF EXISTS pems_v2_drop_column;
DROP PROCEDURE IF EXISTS pems_v2_drop_index;
DROP PROCEDURE IF EXISTS pems_v2_drop_constraint;

-- =====================================================================
-- END OF DOWN. The schema now matches the v1 baseline (minus any data the
-- dropped tables held). Restart the backend on the v1 code path.
-- =====================================================================
