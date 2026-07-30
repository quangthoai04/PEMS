-- =====================================================================
-- 02_up_replace_triggers.sql — G12 contact-guard closure
--
-- Replaces the five primary-contact guard triggers with the hardened bodies now carried by the
-- canonical script. It touches NOTHING else: no table is created or altered, no seed row, no
-- template, no Gallery record, no sent email and no user row is read for modification. The only
-- objects it drops are the five triggers it immediately recreates, by name.
--
-- SAFETY GUARD — you must name the database you intend to change, on the same session:
--
--     SET @pems_guard_confirm_database = 'pems_stage_2026_08';
--     USE pems_stage_2026_08;
--     SOURCE 02_up_replace_triggers.sql;
--
-- Running it without that variable, or with a value that does not match DATABASE(), aborts with
-- SQLSTATE 45000 before any DDL. The confirmation is SPENT at the end of the script, so a pooled
-- connection cannot carry authorisation forward into a migration nobody asked for.
--
-- Idempotent: DROP ... IF EXISTS followed by CREATE means a second run leaves exactly the same five
-- trigger bodies. Verify with 03_verify.sql after each run.
--
-- WHAT CHANGED, and why each part matters:
--   * users.status is read into VARCHAR(30) rather than VARCHAR(20). The ENUM's longest member,
--     PENDING_EMAIL_CONFIRMATION, is 26 characters, so the old width made the guard raise
--     22001 "Data too long" from inside the trigger. The write was still refused, but with a storage
--     error in place of PRIMARY_CONTACT_VISITOR_ACCOUNT_INACTIVE — and every new account passes
--     through that state, so this was reachable, not theoretical.
--   * roles is LEFT JOINed. Under the old inner join a user whose role row could not be read
--     collapsed COUNT(*) to zero and was reported as PRIMARY_CONTACT_USER_NOT_FOUND, which is untrue.
--   * Role and status comparisons use the NULL-safe <=> operator. NULL <> 'VISITOR' is UNKNOWN,
--     which IF treats as false, so an unreadable value would previously have slipped through.
--   * trg_users_protect_active_primary_contact_bu now counts the roles row it looked up. A bare
--     SELECT ... INTO over zero rows leaves the variable NULL and the guard silently stopped guarding.
--
-- This migration changes only how failures are REPORTED and closes the NULL paths. It does not
-- repair data. Run 01_preflight.sql first: a database that already holds violating rows keeps them,
-- and later writes to those rows will begin to fail.
-- =====================================================================

SET NAMES utf8mb4;

-- ── Guard ────────────────────────────────────────────────────────────
-- SIGNAL is only legal inside a compound statement, so the guard is a procedure that is created,
-- called and dropped before any trigger DDL runs.

DROP PROCEDURE IF EXISTS pems_guard_migration_guard;

DELIMITER $

CREATE PROCEDURE pems_guard_migration_guard()
BEGIN
  IF DATABASE() IS NULL THEN
    SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT =
      'No database selected. Connect with a database, or USE one, before running this migration.';
  END IF;

  IF @pems_guard_confirm_database IS NULL OR @pems_guard_confirm_database <> DATABASE() THEN
    SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT =
      'Refusing to migrate: set @pems_guard_confirm_database to the exact name of the database you intend to change.';
  END IF;

  IF (SELECT COUNT(*) FROM information_schema.tables
      WHERE table_schema = DATABASE() AND table_name = 'visit_requests') = 0
     OR (SELECT COUNT(*) FROM information_schema.tables
         WHERE table_schema = DATABASE() AND table_name = 'visit_request_identity_changes') = 0
     OR (SELECT COUNT(*) FROM information_schema.tables
         WHERE table_schema = DATABASE() AND table_name = 'roles') = 0 THEN
    SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT =
      'Refusing to migrate: this database does not carry the visit-request schema these guards protect.';
  END IF;

  -- Two of the five recreated triggers declare FOLLOWS against triggers this migration does not own.
  -- If one of those is absent the CREATE would fail halfway through, leaving the table unguarded.
  IF (SELECT COUNT(*) FROM information_schema.triggers
      WHERE trigger_schema = DATABASE()
        AND trigger_name IN ('trg_visit_requests_cancel_validate_bu', 'trg_users_validate_bu',
                             'trg_identity_changes_transfer_bi', 'trg_identity_changes_transfer_bu')) <> 4 THEN
    SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT =
      'Refusing to migrate: a trigger referenced by FOLLOWS is missing, so the guards could not be recreated in order.';
  END IF;
END$

DELIMITER ;

CALL pems_guard_migration_guard();
DROP PROCEDURE IF EXISTS pems_guard_migration_guard;

-- ── Replace the five guards ──────────────────────────────────────────
-- Dropped and recreated together so a table is never left carrying a mix of old and new bodies.

DROP TRIGGER IF EXISTS `trg_visit_requests_primary_contact_guard_bi`;
DROP TRIGGER IF EXISTS `trg_visit_requests_primary_contact_guard_bu`;
DROP TRIGGER IF EXISTS `trg_users_protect_active_primary_contact_bu`;
DROP TRIGGER IF EXISTS `trg_visit_request_identity_changes_user_guard_bi`;
DROP TRIGGER IF EXISTS `trg_visit_request_identity_changes_user_guard_bu`;

DELIMITER $$

CREATE TRIGGER trg_visit_requests_primary_contact_guard_bi
BEFORE INSERT ON visit_requests
FOR EACH ROW
BEGIN
  DECLARE v_user_count INT DEFAULT 0;
  DECLARE v_role_code VARCHAR(30) DEFAULT NULL;
  DECLARE v_user_status VARCHAR(30) DEFAULT NULL;

  IF NEW.visitor_user_id IS NOT NULL THEN
    SELECT COUNT(*), MAX(r.role_code), MAX(u.status)
      INTO v_user_count, v_role_code, v_user_status
    FROM users u
    LEFT JOIN roles r ON r.role_id = u.role_id
    WHERE u.user_id = NEW.visitor_user_id;

    IF v_user_count <> 1 THEN
      SIGNAL SQLSTATE '45000'
        SET MESSAGE_TEXT = 'PRIMARY_CONTACT_USER_NOT_FOUND';
    END IF;

    IF NOT (v_role_code <=> 'VISITOR') THEN
      SIGNAL SQLSTATE '45000'
        SET MESSAGE_TEXT = 'PRIMARY_CONTACT_USER_MUST_BE_ACTIVE_VISITOR';
    END IF;

    IF NOT (v_user_status <=> 'ACTIVE') THEN
      SIGNAL SQLSTATE '45000'
        SET MESSAGE_TEXT = 'PRIMARY_CONTACT_VISITOR_ACCOUNT_INACTIVE';
    END IF;
  END IF;

  IF NEW.primary_contact_access_status = 'ACTIVE'
     AND NEW.visitor_user_id IS NULL THEN
    SIGNAL SQLSTATE '45000'
      SET MESSAGE_TEXT = 'ACTIVE_PRIMARY_CONTACT_REQUIRES_VISITOR_USER';
  END IF;

  IF NEW.primary_contact_access_status = 'PENDING_CONFIRMATION'
     AND NEW.visitor_user_id IS NOT NULL THEN
    SIGNAL SQLSTATE '45000'
      SET MESSAGE_TEXT = 'PENDING_PRIMARY_CONTACT_MUST_NOT_HAVE_VISITOR_USER';
  END IF;
END$$

CREATE TRIGGER trg_visit_requests_primary_contact_guard_bu
BEFORE UPDATE ON visit_requests
FOR EACH ROW
FOLLOWS trg_visit_requests_cancel_validate_bu
BEGIN
  DECLARE v_user_count INT DEFAULT 0;
  DECLARE v_role_code VARCHAR(30) DEFAULT NULL;
  DECLARE v_user_status VARCHAR(30) DEFAULT NULL;

  IF NEW.visitor_user_id IS NOT NULL THEN
    SELECT COUNT(*), MAX(r.role_code), MAX(u.status)
      INTO v_user_count, v_role_code, v_user_status
    FROM users u
    LEFT JOIN roles r ON r.role_id = u.role_id
    WHERE u.user_id = NEW.visitor_user_id;

    IF v_user_count <> 1 THEN
      SIGNAL SQLSTATE '45000'
        SET MESSAGE_TEXT = 'PRIMARY_CONTACT_USER_NOT_FOUND';
    END IF;

    IF NOT (v_role_code <=> 'VISITOR') THEN
      SIGNAL SQLSTATE '45000'
        SET MESSAGE_TEXT = 'PRIMARY_CONTACT_USER_MUST_BE_ACTIVE_VISITOR';
    END IF;

    IF NOT (v_user_status <=> 'ACTIVE') THEN
      SIGNAL SQLSTATE '45000'
        SET MESSAGE_TEXT = 'PRIMARY_CONTACT_VISITOR_ACCOUNT_INACTIVE';
    END IF;
  END IF;

  IF NEW.primary_contact_access_status = 'ACTIVE'
     AND NEW.visitor_user_id IS NULL THEN
    SIGNAL SQLSTATE '45000'
      SET MESSAGE_TEXT = 'ACTIVE_PRIMARY_CONTACT_REQUIRES_VISITOR_USER';
  END IF;

  IF NEW.primary_contact_access_status = 'PENDING_CONFIRMATION'
     AND NEW.visitor_user_id IS NOT NULL THEN
    SIGNAL SQLSTATE '45000'
      SET MESSAGE_TEXT = 'PENDING_PRIMARY_CONTACT_MUST_NOT_HAVE_VISITOR_USER';
  END IF;
END$$

-- A currently linked ACTIVE primary contact cannot be silently converted into an
-- internal account or deactivated. Cancelled requests are excluded by decision.
CREATE TRIGGER trg_users_protect_active_primary_contact_bu
BEFORE UPDATE ON users
FOR EACH ROW
FOLLOWS trg_users_validate_bu
BEGIN
  DECLARE v_new_role_code VARCHAR(30) DEFAULT NULL;
  DECLARE v_new_role_count INT DEFAULT 0;
  DECLARE v_linked_request_count INT DEFAULT 0;

  IF NOT (NEW.role_id <=> OLD.role_id)
     OR NOT (NEW.status <=> OLD.status) THEN
    -- COUNT alongside the code so "no such role" is distinguishable from "role named VISITOR".
    -- A bare SELECT ... INTO over zero rows leaves the variable NULL, and `NULL <> 'VISITOR'`
    -- evaluates to UNKNOWN, which an IF treats as false — the change would have slipped through.
    SELECT COUNT(*), MAX(role_code)
      INTO v_new_role_count, v_new_role_code
    FROM roles
    WHERE role_id = NEW.role_id;

    SELECT COUNT(*)
      INTO v_linked_request_count
    FROM visit_requests vr
    WHERE vr.visitor_user_id = OLD.user_id
      AND vr.primary_contact_access_status = 'ACTIVE'
      AND vr.status <> 'CANCELLED';

    IF v_linked_request_count > 0 THEN
      -- Fail closed: only a role that reads back as exactly one row named VISITOR is accepted.
      IF v_new_role_count <> 1 OR NOT (v_new_role_code <=> 'VISITOR') THEN
        SIGNAL SQLSTATE '45000'
          SET MESSAGE_TEXT = 'LINKED_PRIMARY_CONTACT_ROLE_CANNOT_CHANGE';
      END IF;

      IF NOT (NEW.status <=> 'ACTIVE') THEN
        SIGNAL SQLSTATE '45000'
          SET MESSAGE_TEXT = 'LINKED_PRIMARY_CONTACT_CANNOT_BE_DEACTIVATED';
      END IF;
    END IF;
  END IF;
END$$

-- Identity claims/transfers may remain PENDING without an account. As soon as a
-- new_user_id is assigned (including APPLIED), it must be an ACTIVE VISITOR.
CREATE TRIGGER trg_visit_request_identity_changes_user_guard_bi
BEFORE INSERT ON visit_request_identity_changes
FOR EACH ROW
FOLLOWS trg_identity_changes_transfer_bi
BEGIN
  DECLARE v_user_count INT DEFAULT 0;
  DECLARE v_role_code VARCHAR(30) DEFAULT NULL;
  DECLARE v_user_status VARCHAR(30) DEFAULT NULL;

  IF NEW.new_user_id IS NOT NULL THEN
    SELECT COUNT(*), MAX(r.role_code), MAX(u.status)
      INTO v_user_count, v_role_code, v_user_status
    FROM users u
    LEFT JOIN roles r ON r.role_id = u.role_id
    WHERE u.user_id = NEW.new_user_id;

    IF v_user_count <> 1 THEN
      SIGNAL SQLSTATE '45000'
        SET MESSAGE_TEXT = 'PRIMARY_CONTACT_USER_NOT_FOUND';
    END IF;

    IF NOT (v_role_code <=> 'VISITOR') THEN
      SIGNAL SQLSTATE '45000'
        SET MESSAGE_TEXT = 'PRIMARY_CONTACT_USER_MUST_BE_ACTIVE_VISITOR';
    END IF;

    IF NOT (v_user_status <=> 'ACTIVE') THEN
      SIGNAL SQLSTATE '45000'
        SET MESSAGE_TEXT = 'PRIMARY_CONTACT_VISITOR_ACCOUNT_INACTIVE';
    END IF;
  END IF;
END$$

CREATE TRIGGER trg_visit_request_identity_changes_user_guard_bu
BEFORE UPDATE ON visit_request_identity_changes
FOR EACH ROW
FOLLOWS trg_identity_changes_transfer_bu
BEGIN
  DECLARE v_user_count INT DEFAULT 0;
  DECLARE v_role_code VARCHAR(30) DEFAULT NULL;
  DECLARE v_user_status VARCHAR(30) DEFAULT NULL;

  IF NEW.new_user_id IS NOT NULL THEN
    SELECT COUNT(*), MAX(r.role_code), MAX(u.status)
      INTO v_user_count, v_role_code, v_user_status
    FROM users u
    LEFT JOIN roles r ON r.role_id = u.role_id
    WHERE u.user_id = NEW.new_user_id;

    IF v_user_count <> 1 THEN
      SIGNAL SQLSTATE '45000'
        SET MESSAGE_TEXT = 'PRIMARY_CONTACT_USER_NOT_FOUND';
    END IF;

    IF NOT (v_role_code <=> 'VISITOR') THEN
      SIGNAL SQLSTATE '45000'
        SET MESSAGE_TEXT = 'PRIMARY_CONTACT_USER_MUST_BE_ACTIVE_VISITOR';
    END IF;

    IF NOT (v_user_status <=> 'ACTIVE') THEN
      SIGNAL SQLSTATE '45000'
        SET MESSAGE_TEXT = 'PRIMARY_CONTACT_VISITOR_ACCOUNT_INACTIVE';
    END IF;
  END IF;
END$$
DELIMITER ;

-- Spend the confirmation so a pooled session cannot carry authorisation into another migration.
SET @pems_guard_confirm_database = NULL;

SELECT 'contact_guard_closure 02_up_replace_triggers.sql completed' AS status;
