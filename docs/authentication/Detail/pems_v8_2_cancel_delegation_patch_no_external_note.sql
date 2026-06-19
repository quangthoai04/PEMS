-- =====================================================================
-- PEMS v8.2 PATCH — UC-136 Cancel Visit Request under Delegation Feature
-- Target base: pems_full_sql_42tables_final_v8_request_status_host_assignment.sql
-- Purpose:
--   1) Add cancellation metadata without external_confirmation_note.
--   2) Register UC-136 under Delegation Reception Management.
--   3) Keep cancellation separate from approval/rejection decision fields.
-- Notes:
--   - Run this after applying SQL v8.
--   - Do NOT run v8.1 cancel patch first. If v8.1 was already applied,
--     use the cleanup patch generated separately.
-- =====================================================================

USE pems_db;

SET @seed_now = NOW();

-- ---------------------------------------------------------------------
-- 1. Schema: request-level cancellation metadata
-- ---------------------------------------------------------------------

ALTER TABLE visit_requests
  ADD COLUMN cancelled_by BIGINT UNSIGNED NULL COMMENT 'Người thực hiện hủy request/delegation',
  ADD COLUMN cancelled_at DATETIME NULL COMMENT 'Thời điểm hủy request/delegation',
  ADD COLUMN cancellation_actor_type ENUM('VISITOR','HOST','STAFF_LEADER','HO','SYSTEM') NULL COMMENT 'Vai trò thực hiện thao tác hủy',
  ADD COLUMN cancellation_source ENUM('SELF_SERVICE','EXTERNAL_CONFIRMATION','INTERNAL_DECISION') NULL COMMENT 'SELF_SERVICE=Visitor tự hủy; EXTERNAL_CONFIRMATION=hủy sau xác nhận ngoài hệ thống; INTERNAL_DECISION=hủy theo quyết định nội bộ',
  ADD COLUMN cancellation_reason TEXT NULL COMMENT 'Lý do hủy. Nếu EXTERNAL_CONFIRMATION thì ghi rõ kênh xác nhận, thời điểm, người xác nhận và lý do.',
  ADD KEY idx_visit_requests_cancelled (cancelled_by, cancelled_at),
  ADD KEY idx_visit_requests_cancel_actor (cancellation_actor_type, cancelled_at),
  ADD CONSTRAINT fk_visit_requests_cancelled_by
    FOREIGN KEY (cancelled_by) REFERENCES users(user_id)
    ON UPDATE CASCADE ON DELETE SET NULL;

-- ---------------------------------------------------------------------
-- 2. Schema: campus-instance cancellation metadata
-- ---------------------------------------------------------------------

ALTER TABLE visit_request_campuses
  ADD COLUMN cancelled_by BIGINT UNSIGNED NULL COMMENT 'Người thực hiện hủy campus instance',
  ADD COLUMN cancelled_at DATETIME NULL COMMENT 'Thời điểm hủy campus instance',
  ADD COLUMN cancellation_actor_type ENUM('VISITOR','HOST','STAFF_LEADER','HO','SYSTEM') NULL COMMENT 'Vai trò thực hiện thao tác hủy campus instance',
  ADD COLUMN cancellation_source ENUM('SELF_SERVICE','EXTERNAL_CONFIRMATION','INTERNAL_DECISION') NULL COMMENT 'Nguồn hủy campus instance',
  ADD COLUMN cancellation_reason TEXT NULL COMMENT 'Lý do hủy. Nếu EXTERNAL_CONFIRMATION thì ghi rõ kênh xác nhận, thời điểm, người xác nhận và lý do.',
  ADD KEY idx_visit_instances_cancelled (cancelled_by, cancelled_at),
  ADD KEY idx_visit_instances_cancel_actor (cancellation_actor_type, cancelled_at),
  ADD CONSTRAINT fk_visit_instances_cancelled_by
    FOREIGN KEY (cancelled_by) REFERENCES users(user_id)
    ON UPDATE CASCADE ON DELETE SET NULL;

-- ---------------------------------------------------------------------
-- 3. Permission seed: UC-136 belongs to Delegation Reception Management
-- ---------------------------------------------------------------------

INSERT INTO permissions
  (permission_id, permission_code, name, permission_group, description, is_system, created_at)
SELECT
  NULL,
  'UC-136.CANCEL_VISIT_REQUEST',
  'UC-136 - Cancel Visit Request',
  'Delegation Reception Management',
  'Cancel visit request/delegation within valid scope. Visitor cancels own request; current Host cancels after external confirmation; HO/Staff Leader cancel within delegated scope.',
  TRUE,
  @seed_now
WHERE NOT EXISTS (
  SELECT 1 FROM permissions WHERE permission_code = 'UC-136.CANCEL_VISIT_REQUEST'
);

INSERT INTO role_permissions
  (role_id, sub_role, permission_id, permission_level, granted_at)
SELECT
  r.role_id,
  x.sub_role,
  p.permission_id,
  x.permission_level,
  @seed_now
FROM (
  SELECT 'VISITOR' AS role_code, 'NONE' AS sub_role, 'UC-136.CANCEL_VISIT_REQUEST' AS permission_code, 'O' AS permission_level
  UNION ALL SELECT 'STAFF', 'Leader', 'UC-136.CANCEL_VISIT_REQUEST', 'E'
  UNION ALL SELECT 'STAFF', 'Staff',  'UC-136.CANCEL_VISIT_REQUEST', 'O'
  UNION ALL SELECT 'HO',    'NONE',   'UC-136.CANCEL_VISIT_REQUEST', 'E'
) x
JOIN roles r
  ON r.role_code = x.role_code
 AND r.deleted_at IS NULL
JOIN permissions p
  ON p.permission_code = x.permission_code
ON DUPLICATE KEY UPDATE
  permission_level = VALUES(permission_level),
  granted_at = VALUES(granted_at);

-- ---------------------------------------------------------------------
-- 4. Replace request decision triggers: APPROVED/REJECTED only.
--    CANCELLED is handled by cancellation metadata, not decision fields.
-- ---------------------------------------------------------------------

DELIMITER $$

DROP TRIGGER IF EXISTS trg_visit_requests_decision_validate_bi$$
DROP TRIGGER IF EXISTS trg_visit_requests_decision_validate_bu$$

CREATE TRIGGER trg_visit_requests_decision_validate_bi
BEFORE INSERT ON visit_requests
FOR EACH ROW
BEGIN
  DECLARE v_actor_role_code VARCHAR(30);
  DECLARE v_actor_sub_role VARCHAR(30);

  IF NEW.status IN ('APPROVED','REJECTED') THEN
    IF NEW.decision_actor_role IS NULL THEN
      SIGNAL SQLSTATE '45000'
        SET MESSAGE_TEXT = 'decision_actor_role is required when visit request is approved/rejected';
    END IF;

    IF NEW.decision_actor_role <> 'SYSTEM' AND NEW.decided_by IS NULL THEN
      SIGNAL SQLSTATE '45000'
        SET MESSAGE_TEXT = 'decided_by is required for non-system visit request decision';
    END IF;

    IF NEW.visit_scope = 'SINGLE_CAMPUS'
       AND NEW.decision_actor_role NOT IN ('STAFF_LEADER','SYSTEM') THEN
      SIGNAL SQLSTATE '45000'
        SET MESSAGE_TEXT = 'Only STAFF_LEADER can decide SINGLE_CAMPUS request';
    END IF;

    IF NEW.visit_scope = 'MULTI_CAMPUS'
       AND NEW.decision_actor_role NOT IN ('HO','SYSTEM') THEN
      SIGNAL SQLSTATE '45000'
        SET MESSAGE_TEXT = 'Only HO can decide MULTI_CAMPUS request';
    END IF;

    IF NEW.decision_actor_role <> 'SYSTEM' THEN
      SELECT r.role_code, u.sub_role
        INTO v_actor_role_code, v_actor_sub_role
      FROM users u
      JOIN roles r ON r.role_id = u.role_id
      WHERE u.user_id = NEW.decided_by;

      IF NEW.decision_actor_role = 'HO' AND v_actor_role_code <> 'HO' THEN
        SIGNAL SQLSTATE '45000'
          SET MESSAGE_TEXT = 'decision_actor_role HO requires decided_by user with HO role';
      END IF;

      IF NEW.decision_actor_role = 'STAFF_LEADER'
         AND NOT (v_actor_role_code = 'STAFF' AND v_actor_sub_role = 'Leader') THEN
        SIGNAL SQLSTATE '45000'
          SET MESSAGE_TEXT = 'decision_actor_role STAFF_LEADER requires STAFF Leader user';
      END IF;
    END IF;
  END IF;
END$$

CREATE TRIGGER trg_visit_requests_decision_validate_bu
BEFORE UPDATE ON visit_requests
FOR EACH ROW
BEGIN
  DECLARE v_actor_role_code VARCHAR(30);
  DECLARE v_actor_sub_role VARCHAR(30);

  IF NEW.status IN ('APPROVED','REJECTED') THEN
    IF NEW.decision_actor_role IS NULL THEN
      SIGNAL SQLSTATE '45000'
        SET MESSAGE_TEXT = 'decision_actor_role is required when visit request is approved/rejected';
    END IF;

    IF NEW.decision_actor_role <> 'SYSTEM' AND NEW.decided_by IS NULL THEN
      SIGNAL SQLSTATE '45000'
        SET MESSAGE_TEXT = 'decided_by is required for non-system visit request decision';
    END IF;

    IF NEW.visit_scope = 'SINGLE_CAMPUS'
       AND NEW.decision_actor_role NOT IN ('STAFF_LEADER','SYSTEM') THEN
      SIGNAL SQLSTATE '45000'
        SET MESSAGE_TEXT = 'Only STAFF_LEADER can decide SINGLE_CAMPUS request';
    END IF;

    IF NEW.visit_scope = 'MULTI_CAMPUS'
       AND NEW.decision_actor_role NOT IN ('HO','SYSTEM') THEN
      SIGNAL SQLSTATE '45000'
        SET MESSAGE_TEXT = 'Only HO can decide MULTI_CAMPUS request';
    END IF;

    IF NEW.decision_actor_role <> 'SYSTEM' THEN
      SELECT r.role_code, u.sub_role
        INTO v_actor_role_code, v_actor_sub_role
      FROM users u
      JOIN roles r ON r.role_id = u.role_id
      WHERE u.user_id = NEW.decided_by;

      IF NEW.decision_actor_role = 'HO' AND v_actor_role_code <> 'HO' THEN
        SIGNAL SQLSTATE '45000'
          SET MESSAGE_TEXT = 'decision_actor_role HO requires decided_by user with HO role';
      END IF;

      IF NEW.decision_actor_role = 'STAFF_LEADER'
         AND NOT (v_actor_role_code = 'STAFF' AND v_actor_sub_role = 'Leader') THEN
        SIGNAL SQLSTATE '45000'
          SET MESSAGE_TEXT = 'decision_actor_role STAFF_LEADER requires STAFF Leader user';
      END IF;
    END IF;
  END IF;
END$$

-- ---------------------------------------------------------------------
-- 5. Cancellation validation triggers.
--    These triggers validate required metadata only. Detailed scope such as
--    ownership/current-host/campus filters must still be checked in backend.
-- ---------------------------------------------------------------------

DROP TRIGGER IF EXISTS trg_visit_requests_cancel_validate_bu$$
DROP TRIGGER IF EXISTS trg_visit_campuses_cancel_validate_bu$$

CREATE TRIGGER trg_visit_requests_cancel_validate_bu
BEFORE UPDATE ON visit_requests
FOR EACH ROW
BEGIN
  IF NEW.status = 'CANCELLED' AND OLD.status <> 'CANCELLED' THEN
    IF OLD.status = 'REJECTED' THEN
      SIGNAL SQLSTATE '45000'
        SET MESSAGE_TEXT = 'Rejected request cannot be cancelled; it is already ended';
    END IF;

    IF NEW.cancellation_actor_type IS NULL
       OR NEW.cancellation_source IS NULL
       OR NEW.cancelled_at IS NULL THEN
      SIGNAL SQLSTATE '45000'
        SET MESSAGE_TEXT = 'cancellation_actor_type, cancellation_source and cancelled_at are required when request is cancelled';
    END IF;

    IF NEW.cancellation_actor_type <> 'SYSTEM' AND NEW.cancelled_by IS NULL THEN
      SIGNAL SQLSTATE '45000'
        SET MESSAGE_TEXT = 'cancelled_by is required for non-system cancellation';
    END IF;

    IF NEW.cancellation_source IN ('EXTERNAL_CONFIRMATION','INTERNAL_DECISION')
       AND (NEW.cancellation_reason IS NULL OR TRIM(NEW.cancellation_reason) = '') THEN
      SIGNAL SQLSTATE '45000'
        SET MESSAGE_TEXT = 'cancellation_reason is required for external-confirmation/internal cancellation';
    END IF;

    IF NEW.cancellation_actor_type = 'VISITOR'
       AND NEW.cancellation_source <> 'SELF_SERVICE' THEN
      SIGNAL SQLSTATE '45000'
        SET MESSAGE_TEXT = 'VISITOR cancellation must use SELF_SERVICE source';
    END IF;

    IF NEW.cancellation_actor_type = 'HOST'
       AND NEW.cancellation_source <> 'EXTERNAL_CONFIRMATION' THEN
      SIGNAL SQLSTATE '45000'
        SET MESSAGE_TEXT = 'HOST cancellation on behalf of visitor must use EXTERNAL_CONFIRMATION source';
    END IF;
  END IF;
END$$

CREATE TRIGGER trg_visit_campuses_cancel_validate_bu
BEFORE UPDATE ON visit_request_campuses
FOR EACH ROW
BEGIN
  IF NEW.status = 'CANCELLED' AND OLD.status <> 'CANCELLED' THEN
    IF OLD.status IN ('DURING_VISIT','AFTER_VISIT','CLOSED') THEN
      SIGNAL SQLSTATE '45000'
        SET MESSAGE_TEXT = 'Cannot cancel campus instance after it is during/after visit or closed';
    END IF;

    IF NEW.cancellation_actor_type IS NULL
       OR NEW.cancellation_source IS NULL
       OR NEW.cancelled_at IS NULL THEN
      SIGNAL SQLSTATE '45000'
        SET MESSAGE_TEXT = 'cancellation_actor_type, cancellation_source and cancelled_at are required when campus instance is cancelled';
    END IF;

    IF NEW.cancellation_actor_type <> 'SYSTEM' AND NEW.cancelled_by IS NULL THEN
      SIGNAL SQLSTATE '45000'
        SET MESSAGE_TEXT = 'cancelled_by is required for non-system campus cancellation';
    END IF;

    IF NEW.cancellation_source IN ('EXTERNAL_CONFIRMATION','INTERNAL_DECISION')
       AND (NEW.cancellation_reason IS NULL OR TRIM(NEW.cancellation_reason) = '') THEN
      SIGNAL SQLSTATE '45000'
        SET MESSAGE_TEXT = 'cancellation_reason is required for external-confirmation/internal cancellation';
    END IF;

    IF NEW.cancellation_actor_type = 'VISITOR'
       AND NEW.cancellation_source <> 'SELF_SERVICE' THEN
      SIGNAL SQLSTATE '45000'
        SET MESSAGE_TEXT = 'VISITOR campus cancellation must use SELF_SERVICE source';
    END IF;

    IF NEW.cancellation_actor_type = 'HOST'
       AND NEW.cancellation_source <> 'EXTERNAL_CONFIRMATION' THEN
      SIGNAL SQLSTATE '45000'
        SET MESSAGE_TEXT = 'HOST campus cancellation must use EXTERNAL_CONFIRMATION source';
    END IF;
  END IF;
END$$

DELIMITER ;

-- ---------------------------------------------------------------------
-- 6. Verification queries
-- ---------------------------------------------------------------------

SELECT permission_code, permission_group
FROM permissions
WHERE permission_code = 'UC-136.CANCEL_VISIT_REQUEST';

SELECT r.role_code, rp.sub_role, p.permission_code, rp.permission_level
FROM role_permissions rp
JOIN roles r ON r.role_id = rp.role_id
JOIN permissions p ON p.permission_id = rp.permission_id
WHERE p.permission_code = 'UC-136.CANCEL_VISIT_REQUEST'
ORDER BY r.role_code, rp.sub_role;

SELECT table_name, column_name, column_type
FROM information_schema.columns
WHERE table_schema = DATABASE()
  AND table_name IN ('visit_requests','visit_request_campuses')
  AND column_name LIKE 'cancellation%'
ORDER BY table_name, ordinal_position;

SELECT table_name, column_name
FROM information_schema.columns
WHERE table_schema = DATABASE()
  AND column_name = 'external_confirmation_note';
