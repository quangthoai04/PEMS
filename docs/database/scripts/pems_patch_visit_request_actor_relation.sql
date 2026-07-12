-- =====================================================================
-- PEMS PATCH — VISIT REQUEST ACTOR RELATION + AUTHENTICATED CREATE
-- (safe upgrade for an existing database; NO DROP DATABASE)
--
-- Adds:
--   1. visit_requests.registrant_user_id  (submitter/registrant account — READ-ONLY relation)
--      visitor_user_id keeps its physical name but now explicitly means
--      "contact owner / đầu mối liên hệ" (the action owner of the request).
--   2. visit_request_campuses.decision_actor_role gains 'STAFF' (regular IC Staff
--      direct self-host during authenticated create of their OWN request).
--   3. visit_request_campuses.decision_source ENUM:
--        STANDARD_CAMPUS_REVIEW — Staff Leader approves a pending instance (existing flow)
--        INTERNAL_SELF_HOST     — creator processed their own campus as host in the create transaction
--        INTERNAL_LEADER_ASSIGN — Staff Leader assigned another same-campus IC Staff in the create transaction
--   4. Trigger updates:
--        - main request cancel: cancelled_by must BE the contact owner (visitor_user_id),
--          not merely "some user with role VISITOR".
--        - campus VISITOR cancel: actor must be the parent request's contact owner.
--        - campus decision: allow decision_actor_role='STAFF' ONLY for
--          decision_source='INTERNAL_SELF_HOST' at INSERT time, own campus,
--          decided_by = host_assigned_by = current_host_user_id = the request's registrant.
--        - all other decisions still require a Staff Leader of the same campus.
--   5. Backfill registrant_user_id (conservative — see rules inline).
--   6. Verification queries (run manually, expect 0 rows each).
--
-- Run with:  mysql -uroot -p pems_db  < pems_patch_visit_request_actor_relation.sql
-- (repeat for pems_test)
-- =====================================================================

-- ---------------------------------------------------------------------
-- 1. visit_requests: registrant_user_id (nullable — legacy rows may stay NULL)
-- ---------------------------------------------------------------------
SET @col_exists := (
  SELECT COUNT(*) FROM information_schema.COLUMNS
  WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'visit_requests' AND COLUMN_NAME = 'registrant_user_id');

SET @ddl := IF(@col_exists = 0,
  'ALTER TABLE visit_requests
     ADD COLUMN registrant_user_id BIGINT UNSIGNED NULL
       COMMENT ''Tài khoản NGƯỜI ĐĂNG KÝ (submitter). Chỉ có quyền xem/theo dõi read-only; mọi mutation request-level thuộc về visitor_user_id (đầu mối liên hệ).''
       AFTER visitor_user_id,
     ADD KEY idx_visit_requests_registrant_user (registrant_user_id, submitted_at),
     ADD CONSTRAINT fk_visit_requests_registrant_user
       FOREIGN KEY (registrant_user_id) REFERENCES users(user_id)
       ON UPDATE CASCADE ON DELETE SET NULL',
  'SELECT ''registrant_user_id already exists'' AS skipped');
PREPARE stmt FROM @ddl; EXECUTE stmt; DEALLOCATE PREPARE stmt;

-- Clarify the meaning of visitor_user_id (contact owner, NOT the submitter).
ALTER TABLE visit_requests
  MODIFY COLUMN visitor_user_id BIGINT UNSIGNED NULL
    COMMENT 'Tài khoản ĐẦU MỐI LIÊN HỆ (contact owner) — chủ sở hữu thao tác request (edit/resubmit/cancel/feedback theo status). Luôn là role VISITOR.';

-- ---------------------------------------------------------------------
-- 2. visit_request_campuses: decision_actor_role + decision_source
-- ---------------------------------------------------------------------
ALTER TABLE visit_request_campuses
  MODIFY COLUMN decision_actor_role ENUM('STAFF_LEADER','STAFF') NULL
    COMMENT 'STAFF_LEADER = duyệt chuẩn/gán host/leader self-host; STAFF = IC Staff thường tự nhận host trong transaction TẠO đơn của chính mình (decision_source=INTERNAL_SELF_HOST).';

SET @col_exists := (
  SELECT COUNT(*) FROM information_schema.COLUMNS
  WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'visit_request_campuses' AND COLUMN_NAME = 'decision_source');

SET @ddl := IF(@col_exists = 0,
  'ALTER TABLE visit_request_campuses
     ADD COLUMN decision_source ENUM(''STANDARD_CAMPUS_REVIEW'',''INTERNAL_SELF_HOST'',''INTERNAL_LEADER_ASSIGN'') NULL
       COMMENT ''Nguồn quyết định: STANDARD_CAMPUS_REVIEW=Staff Leader duyệt instance pending; INTERNAL_SELF_HOST=người tạo tự nhận host own campus trong create; INTERNAL_LEADER_ASSIGN=Leader gán IC Staff cùng campus trong create.''
       AFTER decision_actor_role,
     ADD KEY idx_visit_instances_decision_source (decision_source, decided_at)',
  'SELECT ''decision_source already exists'' AS skipped');
PREPARE stmt FROM @ddl; EXECUTE stmt; DEALLOCATE PREPARE stmt;

-- ---------------------------------------------------------------------
-- 3. Triggers (drop first — the decision_source backfill below must not be
--    re-validated by the OLD triggers, which legacy seed rows can violate)
-- ---------------------------------------------------------------------
DROP TRIGGER IF EXISTS trg_visit_requests_cancel_validate_bu;
DROP TRIGGER IF EXISTS trg_visit_campuses_cancel_validate_bu;
DROP TRIGGER IF EXISTS trg_visit_campuses_assignment_validate_bi;
DROP TRIGGER IF EXISTS trg_visit_campuses_assignment_validate_bu;

-- Existing decided rows all came from the standard Staff Leader review flow.
UPDATE visit_request_campuses
SET decision_source = 'STANDARD_CAMPUS_REVIEW'
WHERE decided_by IS NOT NULL AND decision_source IS NULL;

DELIMITER $$

-- Main request cancellation: only the CONTACT OWNER may cancel the whole request.
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
    FROM users u
    JOIN roles r ON r.role_id = u.role_id
    WHERE u.user_id = NEW.cancelled_by;

    IF v_cancel_role_code <> 'VISITOR' THEN
      SIGNAL SQLSTATE '45000'
        SET MESSAGE_TEXT = 'Only VISITOR can cancel the main visit request';
    END IF;

    -- Actor relation hardening: the canceller must be THE contact owner of this
    -- request, not merely any account with role VISITOR (legacy rows with a NULL
    -- owner keep the old role-only check).
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

-- Campus instance cancellation: VISITOR cancel must be executed by the parent
-- request's contact owner; HOST cancel unchanged.
CREATE TRIGGER trg_visit_campuses_cancel_validate_bu
BEFORE UPDATE ON visit_request_campuses
FOR EACH ROW
BEGIN
  DECLARE v_request_status VARCHAR(30);
  DECLARE v_contact_owner_id BIGINT UNSIGNED;

  IF NEW.status = 'CANCELLED' AND OLD.status <> 'CANCELLED' THEN
    SELECT status, visitor_user_id INTO v_request_status, v_contact_owner_id
    FROM visit_requests
    WHERE visit_request_id = NEW.visit_request_id;

    IF OLD.status = 'WAITING_REQUEST_APPROVAL' THEN
      IF v_request_status <> 'CANCELLED' THEN
        SIGNAL SQLSTATE '45000'
          SET MESSAGE_TEXT = 'Pending campus instance can be cancelled only as a consequence of cancelling the pending main request';
      END IF;
    ELSE
      IF v_request_status NOT IN ('APPROVED','PARTIALLY_APPROVED') THEN
        SIGNAL SQLSTATE '45000'
          SET MESSAGE_TEXT = 'Campus instance can be cancelled only after at least one campus has been approved, except pending-request cascade cancellation';
      END IF;

      IF OLD.status IN ('DURING_VISIT','AFTER_VISIT','CLOSED') THEN
        SIGNAL SQLSTATE '45000'
          SET MESSAGE_TEXT = 'Campus instance already started/finished/closed cannot be cancelled';
      END IF;
    END IF;

    IF NEW.cancelled_by IS NULL OR NEW.cancelled_at IS NULL
       OR NEW.cancellation_actor_type IS NULL OR NEW.cancellation_source IS NULL THEN
      SIGNAL SQLSTATE '45000'
        SET MESSAGE_TEXT = 'cancelled_by, cancelled_at, cancellation_actor_type and cancellation_source are required when campus instance is cancelled';
    END IF;

    IF NEW.cancellation_reason IS NULL OR TRIM(NEW.cancellation_reason) = '' THEN
      SIGNAL SQLSTATE '45000'
        SET MESSAGE_TEXT = 'cancellation_reason is required when campus instance is cancelled';
    END IF;

    IF NEW.cancellation_actor_type = 'VISITOR' AND OLD.planned_start_at < DATE_ADD(NEW.cancelled_at, INTERVAL 24 HOUR) THEN
      SIGNAL SQLSTATE '45000'
        SET MESSAGE_TEXT = 'Visitor cannot cancel a campus visit within 24 hours of its planned start time';
    END IF;

    IF NEW.cancellation_actor_type = 'HOST' AND NEW.cancelled_at >= OLD.planned_start_at THEN
      SIGNAL SQLSTATE '45000'
        SET MESSAGE_TEXT = 'HOST cannot cancel a campus visit after the planned start time';
    END IF;

    IF NEW.cancellation_actor_type = 'VISITOR' THEN
      IF NEW.cancellation_source <> 'SELF_SERVICE' THEN
        SIGNAL SQLSTATE '45000'
          SET MESSAGE_TEXT = 'VISITOR campus cancellation must use SELF_SERVICE source';
      END IF;
      -- Actor relation hardening: the visitor canceller must be the contact owner
      -- of the parent request (legacy rows with NULL owner keep old behaviour).
      IF v_contact_owner_id IS NOT NULL AND NEW.cancelled_by <> v_contact_owner_id THEN
        SIGNAL SQLSTATE '45000'
          SET MESSAGE_TEXT = 'VISITOR campus cancellation requires cancelled_by to be the contact owner of the parent request';
      END IF;
    ELSEIF NEW.cancellation_actor_type = 'HOST' THEN
      IF OLD.status = 'WAITING_REQUEST_APPROVAL' THEN
        SIGNAL SQLSTATE '45000'
          SET MESSAGE_TEXT = 'HOST cannot cancel a pending-approval campus instance';
      END IF;
      IF NEW.cancellation_source <> 'EXTERNAL_CONFIRMATION' THEN
        SIGNAL SQLSTATE '45000'
          SET MESSAGE_TEXT = 'HOST cancellation on behalf of visitor must use EXTERNAL_CONFIRMATION source';
      END IF;
      IF NEW.current_host_user_id IS NULL OR NEW.cancelled_by <> NEW.current_host_user_id THEN
        SIGNAL SQLSTATE '45000'
          SET MESSAGE_TEXT = 'HOST cancellation requires cancelled_by to be the official current host of this campus instance';
      END IF;
    ELSE
      SIGNAL SQLSTATE '45000'
        SET MESSAGE_TEXT = 'Only VISITOR or HOST can cancel a campus instance';
    END IF;
  END IF;
END$$

-- Campus instance decision/host rules (INSERT).
-- Standard review + leader assign: decided_by/host_assigned_by must be a Staff
-- Leader of the same campus (decision_actor_role STAFF_LEADER).
-- INTERNAL_SELF_HOST by a regular IC Staff is ONLY valid here (insert-time = the
-- create transaction): decided_by = host_assigned_by = current_host_user_id =
-- the request's registrant, ACTIVE STAFF/STAFF of the same campus, actor role 'STAFF'.
CREATE TRIGGER trg_visit_campuses_assignment_validate_bi
BEFORE INSERT ON visit_request_campuses
FOR EACH ROW
BEGIN
  DECLARE v_request_status VARCHAR(30);
  DECLARE v_registrant_user_id BIGINT UNSIGNED;
  DECLARE v_agenda_count INT DEFAULT 0;
  DECLARE v_host_role_code VARCHAR(30);
  DECLARE v_host_sub_role VARCHAR(30);
  DECLARE v_host_campus_id BIGINT UNSIGNED;
  DECLARE v_assigner_role_code VARCHAR(30);
  DECLARE v_assigner_sub_role VARCHAR(30);
  DECLARE v_assigner_campus_id BIGINT UNSIGNED;
  DECLARE v_decider_role_code VARCHAR(30);
  DECLARE v_decider_sub_role VARCHAR(30);
  DECLARE v_decider_campus_id BIGINT UNSIGNED;
  DECLARE v_decider_status VARCHAR(30);
  DECLARE v_coord_role_code VARCHAR(30);
  DECLARE v_coord_sub_role VARCHAR(30);
  DECLARE v_coord_campus_id BIGINT UNSIGNED;
  DECLARE v_source VARCHAR(40);

  SELECT status, registrant_user_id INTO v_request_status, v_registrant_user_id
  FROM visit_requests
  WHERE visit_request_id = NEW.visit_request_id;

  IF v_request_status = 'CANCELLED' AND NEW.status <> 'CANCELLED' THEN
    SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Cannot create active campus instance under a cancelled request';
  END IF;

  IF NEW.status = 'WAITING_REQUEST_APPROVAL' THEN
    IF NEW.current_host_user_id IS NOT NULL OR NEW.host_assigned_by IS NOT NULL OR NEW.host_assigned_at IS NOT NULL
       OR NEW.decided_by IS NOT NULL OR NEW.decided_at IS NOT NULL OR NEW.decision_actor_role IS NOT NULL
       OR NEW.decision_source IS NOT NULL THEN
      SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'WAITING_REQUEST_APPROVAL must not have host or decision data';
    END IF;
  END IF;

  IF NEW.status = 'REJECTED' THEN
    IF NEW.decided_by IS NULL OR NEW.decided_at IS NULL OR NEW.decision_actor_role <> 'STAFF_LEADER'
       OR NEW.decision_note IS NULL OR TRIM(NEW.decision_note) = '' THEN
      SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'REJECTED campus instance requires Staff Leader decision metadata and decision_note';
    END IF;
    IF NEW.current_host_user_id IS NOT NULL OR NEW.host_assigned_by IS NOT NULL OR NEW.host_assigned_at IS NOT NULL THEN
      SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'REJECTED campus instance must not have official host assignment';
    END IF;
  END IF;

  IF NEW.status IN ('ASSIGNED','BEFORE_VISIT','DURING_VISIT','AFTER_VISIT','CLOSED') THEN
    IF NEW.current_host_user_id IS NULL OR NEW.host_assigned_by IS NULL OR NEW.host_assigned_at IS NULL THEN
      SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Approved/operational campus instance requires official host assignment';
    END IF;
    IF NEW.decided_by IS NULL OR NEW.decided_at IS NULL OR NEW.decision_actor_role IS NULL THEN
      SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Approved/operational campus instance requires decision metadata';
    END IF;
  END IF;

  IF NEW.status IN ('DURING_VISIT','AFTER_VISIT','CLOSED') THEN
    SELECT COUNT(*) INTO v_agenda_count
    FROM visit_agendas va
    WHERE va.visit_instance_id = NEW.visit_instance_id;

    IF v_agenda_count = 0 THEN
      SIGNAL SQLSTATE '45000'
        SET MESSAGE_TEXT = 'Campus instance cannot be DURING_VISIT/AFTER_VISIT/CLOSED without at least one agenda item';
    END IF;
  END IF;

  IF NEW.coordinator_user_id IS NOT NULL THEN
    SELECT r.role_code, u.sub_role, u.primary_campus_id INTO v_coord_role_code, v_coord_sub_role, v_coord_campus_id
    FROM users u JOIN roles r ON r.role_id = u.role_id
    WHERE u.user_id = NEW.coordinator_user_id;
    IF NOT (v_coord_role_code = 'STAFF' AND v_coord_sub_role = 'LEADER' AND v_coord_campus_id = NEW.campus_id) THEN
      SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'coordinator_user_id must be Staff Leader of the same campus';
    END IF;
  END IF;

  IF NEW.decided_by IS NOT NULL THEN
    SET v_source = COALESCE(NEW.decision_source, 'STANDARD_CAMPUS_REVIEW');

    SELECT r.role_code, u.sub_role, u.primary_campus_id, u.status
      INTO v_decider_role_code, v_decider_sub_role, v_decider_campus_id, v_decider_status
    FROM users u JOIN roles r ON r.role_id = u.role_id
    WHERE u.user_id = NEW.decided_by;

    IF v_source = 'INTERNAL_SELF_HOST' AND NEW.decision_actor_role = 'STAFF' THEN
      -- Regular IC Staff direct self-host — ONLY inside the create transaction of
      -- their OWN request, own campus, host = decider = assigner = registrant.
      IF NOT (v_decider_role_code = 'STAFF' AND v_decider_sub_role = 'STAFF'
              AND v_decider_campus_id = NEW.campus_id AND v_decider_status = 'ACTIVE') THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'INTERNAL_SELF_HOST by STAFF requires an ACTIVE regular Staff of the same campus';
      END IF;
      IF v_registrant_user_id IS NULL OR v_registrant_user_id <> NEW.decided_by THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'STAFF self-host decision is only valid on a request registered by that same Staff';
      END IF;
      IF NEW.current_host_user_id IS NULL OR NEW.current_host_user_id <> NEW.decided_by
         OR NEW.host_assigned_by IS NULL OR NEW.host_assigned_by <> NEW.decided_by THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'STAFF self-host requires decided_by = host_assigned_by = current_host_user_id';
      END IF;
    ELSE
      IF NEW.decision_actor_role <> 'STAFF_LEADER' THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'decision_actor_role must be STAFF_LEADER unless INTERNAL_SELF_HOST by the registering Staff';
      END IF;
      IF NOT (v_decider_role_code = 'STAFF' AND v_decider_sub_role = 'LEADER' AND v_decider_campus_id = NEW.campus_id) THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'decided_by must be Staff Leader of the same campus';
      END IF;
    END IF;
  END IF;

  IF NEW.host_assigned_by IS NOT NULL THEN
    IF NEW.decided_by IS NOT NULL AND NEW.decided_by <> NEW.host_assigned_by THEN
      SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'host_assigned_by must match decided_by when approving a campus instance';
    END IF;
    -- Same-person INTERNAL_SELF_HOST STAFF case already fully validated above.
    IF NOT (COALESCE(NEW.decision_source,'STANDARD_CAMPUS_REVIEW') = 'INTERNAL_SELF_HOST'
            AND NEW.decision_actor_role = 'STAFF') THEN
      SELECT r.role_code, u.sub_role, u.primary_campus_id INTO v_assigner_role_code, v_assigner_sub_role, v_assigner_campus_id
      FROM users u JOIN roles r ON r.role_id = u.role_id
      WHERE u.user_id = NEW.host_assigned_by;
      IF NOT (v_assigner_role_code = 'STAFF' AND v_assigner_sub_role = 'LEADER' AND v_assigner_campus_id = NEW.campus_id) THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'host_assigned_by must be Staff Leader of the same campus';
      END IF;
    END IF;
  END IF;

  IF NEW.current_host_user_id IS NOT NULL THEN
    SELECT r.role_code, u.sub_role, u.primary_campus_id INTO v_host_role_code, v_host_sub_role, v_host_campus_id
    FROM users u JOIN roles r ON r.role_id = u.role_id
    WHERE u.user_id = NEW.current_host_user_id;
    IF NOT (
      (v_host_role_code = 'STAFF' AND v_host_sub_role = 'STAFF' AND v_host_campus_id = NEW.campus_id)
      OR
      (v_host_role_code = 'STAFF' AND v_host_sub_role = 'LEADER' AND v_host_campus_id = NEW.campus_id AND NEW.current_host_user_id = NEW.host_assigned_by)
    ) THEN
      SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'current_host_user_id must be IC Staff of same campus or the approving Staff Leader themself';
    END IF;
  END IF;
END$$

-- Campus instance decision/host rules (UPDATE).
-- A NEW decision appearing on update (OLD.decided_by IS NULL) is always the
-- standard Staff Leader review — STAFF/INTERNAL_SELF_HOST can never be introduced
-- after creation (a regular Staff must not approve an existing pending request).
-- Rows that already carry a valid insert-time STAFF self-host decision keep passing
-- consistency checks on later lifecycle updates.
CREATE TRIGGER trg_visit_campuses_assignment_validate_bu
BEFORE UPDATE ON visit_request_campuses
FOR EACH ROW
BEGIN
  DECLARE v_request_status VARCHAR(30);
  DECLARE v_registrant_user_id BIGINT UNSIGNED;
  DECLARE v_agenda_count INT DEFAULT 0;
  DECLARE v_host_role_code VARCHAR(30);
  DECLARE v_host_sub_role VARCHAR(30);
  DECLARE v_host_campus_id BIGINT UNSIGNED;
  DECLARE v_assigner_role_code VARCHAR(30);
  DECLARE v_assigner_sub_role VARCHAR(30);
  DECLARE v_assigner_campus_id BIGINT UNSIGNED;
  DECLARE v_decider_role_code VARCHAR(30);
  DECLARE v_decider_sub_role VARCHAR(30);
  DECLARE v_decider_campus_id BIGINT UNSIGNED;
  DECLARE v_coord_role_code VARCHAR(30);
  DECLARE v_coord_sub_role VARCHAR(30);
  DECLARE v_coord_campus_id BIGINT UNSIGNED;
  DECLARE v_source VARCHAR(40);

  SELECT status, registrant_user_id INTO v_request_status, v_registrant_user_id
  FROM visit_requests
  WHERE visit_request_id = NEW.visit_request_id;

  IF v_request_status = 'CANCELLED' AND NEW.status <> 'CANCELLED' THEN
    SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Cannot update campus instance to active status under a cancelled request';
  END IF;

  IF OLD.current_host_user_id IS NOT NULL AND NOT (NEW.current_host_user_id <=> OLD.current_host_user_id) THEN
    SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Official host cannot be changed after first assignment';
  END IF;

  -- A decision introduced AFTER creation must come from the standard campus review.
  IF OLD.decided_by IS NULL AND NEW.decided_by IS NOT NULL THEN
    IF COALESCE(NEW.decision_source, 'STANDARD_CAMPUS_REVIEW') <> 'STANDARD_CAMPUS_REVIEW'
       OR COALESCE(NEW.decision_actor_role,'') <> 'STAFF_LEADER' THEN
      SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Post-create campus decisions must use STANDARD_CAMPUS_REVIEW by a Staff Leader';
    END IF;
  END IF;

  IF NEW.status = 'WAITING_REQUEST_APPROVAL' THEN
    IF NEW.current_host_user_id IS NOT NULL OR NEW.host_assigned_by IS NOT NULL OR NEW.host_assigned_at IS NOT NULL
       OR NEW.decided_by IS NOT NULL OR NEW.decided_at IS NOT NULL OR NEW.decision_actor_role IS NOT NULL
       OR NEW.decision_source IS NOT NULL THEN
      SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'WAITING_REQUEST_APPROVAL must not have host or decision data';
    END IF;
  END IF;

  IF NEW.status = 'REJECTED' THEN
    IF OLD.status <> 'WAITING_REQUEST_APPROVAL' AND OLD.status <> 'REJECTED' THEN
      SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Only pending campus instance can be rejected';
    END IF;
    IF NEW.decided_by IS NULL OR NEW.decided_at IS NULL OR NEW.decision_actor_role <> 'STAFF_LEADER'
       OR NEW.decision_note IS NULL OR TRIM(NEW.decision_note) = '' THEN
      SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'REJECTED campus instance requires Staff Leader decision metadata and decision_note';
    END IF;
    IF NEW.current_host_user_id IS NOT NULL OR NEW.host_assigned_by IS NOT NULL OR NEW.host_assigned_at IS NOT NULL THEN
      SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'REJECTED campus instance must not have official host assignment';
    END IF;
  END IF;

  IF NEW.status IN ('ASSIGNED','BEFORE_VISIT','DURING_VISIT','AFTER_VISIT','CLOSED') THEN
    IF NEW.current_host_user_id IS NULL OR NEW.host_assigned_by IS NULL OR NEW.host_assigned_at IS NULL THEN
      SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Approved/operational campus instance requires official host assignment';
    END IF;
    IF NEW.decided_by IS NULL OR NEW.decided_at IS NULL OR NEW.decision_actor_role IS NULL THEN
      SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Approved/operational campus instance requires decision metadata';
    END IF;
  END IF;

  IF NEW.status IN ('DURING_VISIT','AFTER_VISIT','CLOSED') THEN
    SELECT COUNT(*) INTO v_agenda_count
    FROM visit_agendas va
    WHERE va.visit_instance_id = NEW.visit_instance_id;

    IF v_agenda_count = 0 THEN
      SIGNAL SQLSTATE '45000'
        SET MESSAGE_TEXT = 'Campus instance cannot be DURING_VISIT/AFTER_VISIT/CLOSED without at least one agenda item';
    END IF;
  END IF;

  IF NEW.coordinator_user_id IS NOT NULL THEN
    SELECT r.role_code, u.sub_role, u.primary_campus_id INTO v_coord_role_code, v_coord_sub_role, v_coord_campus_id
    FROM users u JOIN roles r ON r.role_id = u.role_id
    WHERE u.user_id = NEW.coordinator_user_id;
    IF NOT (v_coord_role_code = 'STAFF' AND v_coord_sub_role = 'LEADER' AND v_coord_campus_id = NEW.campus_id) THEN
      SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'coordinator_user_id must be Staff Leader of the same campus';
    END IF;
  END IF;

  IF NEW.decided_by IS NOT NULL THEN
    SET v_source = COALESCE(NEW.decision_source, 'STANDARD_CAMPUS_REVIEW');

    SELECT r.role_code, u.sub_role, u.primary_campus_id INTO v_decider_role_code, v_decider_sub_role, v_decider_campus_id
    FROM users u JOIN roles r ON r.role_id = u.role_id
    WHERE u.user_id = NEW.decided_by;

    IF v_source = 'INTERNAL_SELF_HOST' AND NEW.decision_actor_role = 'STAFF' THEN
      -- Consistency re-check of an insert-time STAFF self-host on later updates.
      IF NOT (v_decider_role_code = 'STAFF' AND v_decider_sub_role = 'STAFF' AND v_decider_campus_id = NEW.campus_id) THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'INTERNAL_SELF_HOST by STAFF requires a regular Staff of the same campus';
      END IF;
      IF v_registrant_user_id IS NULL OR v_registrant_user_id <> NEW.decided_by THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'STAFF self-host decision is only valid on a request registered by that same Staff';
      END IF;
      IF NEW.current_host_user_id IS NULL OR NEW.current_host_user_id <> NEW.decided_by
         OR NEW.host_assigned_by IS NULL OR NEW.host_assigned_by <> NEW.decided_by THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'STAFF self-host requires decided_by = host_assigned_by = current_host_user_id';
      END IF;
    ELSE
      IF NEW.decision_actor_role <> 'STAFF_LEADER' THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'decision_actor_role must be STAFF_LEADER unless INTERNAL_SELF_HOST by the registering Staff';
      END IF;
      IF NOT (v_decider_role_code = 'STAFF' AND v_decider_sub_role = 'LEADER' AND v_decider_campus_id = NEW.campus_id) THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'decided_by must be Staff Leader of the same campus';
      END IF;
    END IF;
  END IF;

  IF NEW.host_assigned_by IS NOT NULL THEN
    IF NEW.decided_by IS NOT NULL AND NEW.decided_by <> NEW.host_assigned_by THEN
      SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'host_assigned_by must match decided_by when approving a campus instance';
    END IF;
    IF NOT (COALESCE(NEW.decision_source,'STANDARD_CAMPUS_REVIEW') = 'INTERNAL_SELF_HOST'
            AND NEW.decision_actor_role = 'STAFF') THEN
      SELECT r.role_code, u.sub_role, u.primary_campus_id INTO v_assigner_role_code, v_assigner_sub_role, v_assigner_campus_id
      FROM users u JOIN roles r ON r.role_id = u.role_id
      WHERE u.user_id = NEW.host_assigned_by;
      IF NOT (v_assigner_role_code = 'STAFF' AND v_assigner_sub_role = 'LEADER' AND v_assigner_campus_id = NEW.campus_id) THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'host_assigned_by must be Staff Leader of the same campus';
      END IF;
    END IF;
  END IF;

  IF NEW.current_host_user_id IS NOT NULL THEN
    SELECT r.role_code, u.sub_role, u.primary_campus_id INTO v_host_role_code, v_host_sub_role, v_host_campus_id
    FROM users u JOIN roles r ON r.role_id = u.role_id
    WHERE u.user_id = NEW.current_host_user_id;
    IF NOT (
      (v_host_role_code = 'STAFF' AND v_host_sub_role = 'STAFF' AND v_host_campus_id = NEW.campus_id)
      OR
      (v_host_role_code = 'STAFF' AND v_host_sub_role = 'LEADER' AND v_host_campus_id = NEW.campus_id AND NEW.current_host_user_id = NEW.host_assigned_by)
    ) THEN
      SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'current_host_user_id must be IC Staff of same campus or the approving Staff Leader themself';
    END IF;
  END IF;
END$$

DELIMITER ;

-- ---------------------------------------------------------------------
-- 4. Backfill registrant_user_id (conservative)
-- ---------------------------------------------------------------------
-- 4a. Registrant email == contact owner's email → same account.
UPDATE visit_requests vr
JOIN users u ON u.user_id = vr.visitor_user_id
SET vr.registrant_user_id = vr.visitor_user_id
WHERE vr.registrant_user_id IS NULL
  AND LOWER(TRIM(vr.registrant_email)) = LOWER(TRIM(u.email));

-- 4b. Different email → link ONLY an already-existing ACTIVE VISITOR account.
--     Never mass-create accounts for historical rows; never link internal accounts.
UPDATE visit_requests vr
JOIN users u ON LOWER(TRIM(u.email)) = LOWER(TRIM(vr.registrant_email))
JOIN roles r ON r.role_id = u.role_id AND r.role_code = 'VISITOR'
SET vr.registrant_user_id = u.user_id
WHERE vr.registrant_user_id IS NULL
  AND u.status = 'ACTIVE'
  AND vr.created_source = 'VISITOR_SUBMITTED';

-- 4c. Legacy STAFF_CREATED / unresolved rows stay NULL — report them for manual audit:
SELECT 'unresolved_registrant_rows' AS check_name, COUNT(*) AS row_count
FROM visit_requests WHERE registrant_user_id IS NULL;

-- ---------------------------------------------------------------------
-- 5. Verification queries (expect issue_count = 0 for each)
-- ---------------------------------------------------------------------
-- Contact owner must always be role VISITOR.
SELECT 'contact_owner_not_visitor' AS check_name, COUNT(*) AS issue_count
FROM visit_requests vr
JOIN users u ON u.user_id = vr.visitor_user_id
JOIN roles r ON r.role_id = u.role_id
WHERE r.role_code <> 'VISITOR';

-- STAFF-actor decisions must be INTERNAL_SELF_HOST, own campus, self-host, on own registered request.
SELECT 'invalid_staff_direct_decision' AS check_name, COUNT(*) AS issue_count
FROM visit_request_campuses c
JOIN visit_requests vr ON vr.visit_request_id = c.visit_request_id
LEFT JOIN users du ON du.user_id = c.decided_by
WHERE c.decision_actor_role = 'STAFF'
  AND (c.decision_source <> 'INTERNAL_SELF_HOST'
       OR c.decided_by IS NULL
       OR c.decided_by <> c.current_host_user_id
       OR c.decided_by <> c.host_assigned_by
       OR vr.registrant_user_id IS NULL
       OR vr.registrant_user_id <> c.decided_by
       OR du.primary_campus_id <> c.campus_id);

-- Decisions with a source must have a decider.
SELECT 'decision_source_without_decider' AS check_name, COUNT(*) AS issue_count
FROM visit_request_campuses
WHERE decision_source IS NOT NULL AND decided_by IS NULL;

-- Registrant linked to an internal account is forbidden.
SELECT 'registrant_linked_internal_account' AS check_name, COUNT(*) AS issue_count
FROM visit_requests vr
JOIN users u ON u.user_id = vr.registrant_user_id
JOIN roles r ON r.role_id = u.role_id
WHERE r.role_code NOT IN ('VISITOR','STAFF');
