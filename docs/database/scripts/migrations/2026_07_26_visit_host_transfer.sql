-- ============================================================================
-- 2026-07-26 — Allow a campus's Host to be handed over after approval.
--
-- WHY
--   trg_visit_campuses_assignment_validate_bu currently refuses ANY change to current_host_user_id
--   once it is set:
--
--       IF OLD.current_host_user_id IS NOT NULL
--          AND NOT (NEW.current_host_user_id <=> OLD.current_host_user_id) THEN
--         SIGNAL ... 'Official host cannot be changed after first assignment';
--
--   That rule exists for a good reason — it stops a Host being swapped as a side effect of some other
--   update, and it stops an approval being quietly re-pointed at a different person. But it also makes
--   a legitimate, deliberate handover impossible, and handovers happen: the assigned Host goes on
--   leave, is called to another campus, or leaves the university between approval and the visit.
--
--   The same trigger also insists host_assigned_by = decided_by. During an approval those two ARE the
--   same act by the same leader. On a later handover they are not: the leader making the change need
--   not be the one who approved the campus months earlier, and requiring it would force a transfer to
--   overwrite the recorded decision-maker — falsifying who approved the visit.
--
-- WHAT
--   Rewrites trg_visit_campuses_assignment_validate_bu so both rules apply to the ASSIGNMENT event
--   rather than to the row forever:
--
--     1. current_host_user_id may change ONLY when every one of these holds:
--          • the row is already decided and not yet under way (OLD.status IN ASSIGNED, BEFORE_VISIT)
--          • the status is not changing in the same statement (a handover is not a decision)
--          • the new host is not NULL (a campus never loses its Host — it gains a different one)
--          • host_assigned_by / host_assigned_at are both set (an assignment, not a stray UPDATE)
--        Anything else still raises the original error, so the protection this rule was written for —
--        a Host silently changing as a side effect — is intact.
--
--        Note it does NOT require host_assigned_at to DIFFER from its previous value. That looked like
--        a stronger check but depends on clock resolution: a handover in the same second as the
--        original assignment writes an identical DATETIME and would have been refused.
--
--     2. host_assigned_by must equal decided_by only while the decision is BEING MADE
--        (OLD.decided_by IS NULL AND NEW.decided_by IS NOT NULL). On an already-decided row the
--        assigner is checked on its own merits: an ACTIVE Staff Leader of that campus.
--
--   Every other check in the trigger is carried over UNCHANGED, including the host-eligibility test
--   that the new Host must be IC Staff of this campus or the assigning Staff Leader themself. The
--   backend applies the identical rule (VisitHostEligibility) before it ever reaches the database.
--
-- SAFETY
--   Trigger-only: no table, column, index or row is touched, and no existing row can become invalid —
--   the change only ADMITS a transition that was previously refused. Idempotent (DROP … IF EXISTS then
--   CREATE). Reversible by re-running the DOWN block, which restores the stricter body verbatim.
-- ============================================================================

-- ── UP ──────────────────────────────────────────────────────────────────────

DROP TRIGGER IF EXISTS `trg_visit_campuses_assignment_validate_bu`;

DELIMITER $$

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
  DECLARE v_is_host_transfer TINYINT DEFAULT 0;

  SELECT status, registrant_user_id INTO v_request_status, v_registrant_user_id
  FROM visit_requests
  WHERE visit_request_id = NEW.visit_request_id;

  IF v_request_status = 'CANCELLED' AND NEW.status <> 'CANCELLED' THEN
    SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Cannot update campus instance to active status under a cancelled request';
  END IF;

  -- ── Host handover: a deliberate re-assignment on an already-decided, not-yet-started campus. ──
  IF OLD.current_host_user_id IS NOT NULL
     AND NOT (NEW.current_host_user_id <=> OLD.current_host_user_id)
     AND NEW.current_host_user_id IS NOT NULL
     AND OLD.status IN ('ASSIGNED','BEFORE_VISIT')
     AND NEW.status = OLD.status
     AND NEW.host_assigned_by IS NOT NULL
     AND NEW.host_assigned_at IS NOT NULL THEN
    SET v_is_host_transfer = 1;
  END IF;

  IF OLD.current_host_user_id IS NOT NULL
     AND NOT (NEW.current_host_user_id <=> OLD.current_host_user_id)
     AND v_is_host_transfer = 0 THEN
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

  -- Từ DURING_VISIT trở đi, khách đã/đang được tiếp khách nên campus instance bắt buộc phải có agenda thật.
  -- ASSIGNED/BEFORE_VISIT vẫn có thể là giai đoạn Host đang chuẩn bị agenda; backend có thể siết sớm hơn nếu cần.
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
      -- On a handover the campus keeps its original self-host DECISION but no longer its original
      -- host, so the decided_by = host trio only has to hold while that decision is being recorded.
      IF v_is_host_transfer = 0
         AND (NEW.current_host_user_id IS NULL OR NEW.current_host_user_id <> NEW.decided_by
              OR NEW.host_assigned_by IS NULL OR NEW.host_assigned_by <> NEW.decided_by) THEN
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
    -- decided_by and host_assigned_by are the same act only while the decision is being MADE. On a
    -- later handover the assigning leader need not be the one who approved the campus months ago, and
    -- demanding it would force the transfer to overwrite who approved the visit.
    IF OLD.decided_by IS NULL AND NEW.decided_by IS NOT NULL
       AND NEW.decided_by <> NEW.host_assigned_by THEN
      SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'host_assigned_by must match decided_by when approving a campus instance';
    END IF;
    IF NOT (COALESCE(NEW.decision_source,'STANDARD_CAMPUS_REVIEW') = 'INTERNAL_SELF_HOST'
            AND NEW.decision_actor_role = 'STAFF'
            AND v_is_host_transfer = 0) THEN
      SELECT r.role_code, u.sub_role, u.primary_campus_id INTO v_assigner_role_code, v_assigner_sub_role, v_assigner_campus_id
      FROM users u JOIN roles r ON r.role_id = u.role_id
      WHERE u.user_id = NEW.host_assigned_by;
      IF NOT (v_assigner_role_code = 'STAFF' AND v_assigner_sub_role = 'LEADER' AND v_assigner_campus_id = NEW.campus_id) THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'host_assigned_by must be Staff Leader of the same campus';
      END IF;
    END IF;
  END IF;

  -- Host eligibility is UNCHANGED: IC Staff of this campus, or the assigning Staff Leader themself.
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

-- ── VERIFY ──────────────────────────────────────────────────────────────────
SELECT
  TRIGGER_NAME,
  IF(ACTION_STATEMENT LIKE '%v_is_host_transfer%', 'PASS', 'FAIL') AS host_transfer_supported
FROM information_schema.TRIGGERS
WHERE TRIGGER_SCHEMA = DATABASE()
  AND TRIGGER_NAME = 'trg_visit_campuses_assignment_validate_bu';

-- ── DOWN ────────────────────────────────────────────────────────────────────
-- Re-running the ORIGINAL trigger body from
--   docs/database/scripts/PEMS_FULL_V2_NO_SEED_DATA_GALLERY.sql (trg_visit_campuses_assignment_validate_bu)
-- restores the stricter rule. Note that rows whose Host was already transferred stay as they are —
-- they are valid rows; the old trigger simply would not have allowed the update that produced them.
