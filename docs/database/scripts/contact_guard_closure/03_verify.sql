-- =====================================================================
-- 03_verify.sql — proves the G12 guards are present, hardened, ordered, and actually enforcing
--
--     USE pems_stage_2026_08;
--     SOURCE 03_verify.sql;
--
-- Read-only with respect to persisted data: the behavioural probes at the end run inside a
-- transaction that is rolled back, exactly like the canonical self-test. Nothing is left behind.
--
-- The final statement calls pems_guard_verify_gate(), which SIGNALs on any FAIL. Run this file with
-- the mysql client and check its exit code — a non-zero exit means at least one check failed, so it
-- is usable directly as a deployment gate rather than something a human has to read carefully.
-- =====================================================================

SET NAMES utf8mb4;

DROP TEMPORARY TABLE IF EXISTS pems_guard_verify_results;
CREATE TEMPORARY TABLE pems_guard_verify_results (
  sequence_no INT UNSIGNED NOT NULL AUTO_INCREMENT,
  check_name VARCHAR(120) NOT NULL,
  expected VARCHAR(200) NOT NULL,
  actual VARCHAR(300) NOT NULL,
  result ENUM('PASS','FAIL','INFO') NOT NULL,
  PRIMARY KEY (sequence_no)
) ENGINE=MEMORY;

-- ── 1..5 presence, table, timing and order ───────────────────────────

INSERT INTO pems_guard_verify_results (check_name, expected, actual, result)
SELECT
    CONCAT('trigger_present:', t.name),
    'present',
    COALESCE(CONCAT(g.action_timing, ' ', g.event_manipulation, ' ON ', g.event_object_table), 'MISSING'),
    IF(g.trigger_name IS NULL, 'FAIL', 'PASS')
FROM (
    SELECT 'trg_visit_requests_primary_contact_guard_bi' AS name
    UNION ALL SELECT 'trg_visit_requests_primary_contact_guard_bu'
    UNION ALL SELECT 'trg_users_protect_active_primary_contact_bu'
    UNION ALL SELECT 'trg_visit_request_identity_changes_user_guard_bi'
    UNION ALL SELECT 'trg_visit_request_identity_changes_user_guard_bu'
) t
LEFT JOIN information_schema.triggers g
       ON g.trigger_schema = DATABASE() AND g.trigger_name = t.name;

-- ── 6..10 the hardened body markers ──────────────────────────────────
-- Checked as properties of the stored body rather than by trusting that the migration ran: a
-- database restored from an older dump after the migration would fail here, which is the point.

INSERT INTO pems_guard_verify_results (check_name, expected, actual, result)
SELECT
    CONCAT('body_widened_status_var:', trigger_name),
    'v_user_status VARCHAR(30)',
    IF(action_statement LIKE '%v_user_status VARCHAR(30)%', 'VARCHAR(30)',
       IF(action_statement LIKE '%v_user_status VARCHAR(20)%', 'VARCHAR(20) — pre-G12', 'not declared')),
    IF(action_statement LIKE '%v_user_status VARCHAR(30)%', 'PASS', 'FAIL')
FROM information_schema.triggers
WHERE trigger_schema = DATABASE()
  AND trigger_name IN ('trg_visit_requests_primary_contact_guard_bi',
                       'trg_visit_requests_primary_contact_guard_bu',
                       'trg_visit_request_identity_changes_user_guard_bi',
                       'trg_visit_request_identity_changes_user_guard_bu');

INSERT INTO pems_guard_verify_results (check_name, expected, actual, result)
SELECT
    CONCAT('body_left_join_roles:', trigger_name),
    'LEFT JOIN roles',
    IF(action_statement LIKE '%LEFT JOIN roles%', 'LEFT JOIN', 'inner join — pre-G12'),
    IF(action_statement LIKE '%LEFT JOIN roles%', 'PASS', 'FAIL')
FROM information_schema.triggers
WHERE trigger_schema = DATABASE()
  AND trigger_name IN ('trg_visit_requests_primary_contact_guard_bi',
                       'trg_visit_requests_primary_contact_guard_bu',
                       'trg_visit_request_identity_changes_user_guard_bi',
                       'trg_visit_request_identity_changes_user_guard_bu');

INSERT INTO pems_guard_verify_results (check_name, expected, actual, result)
SELECT
    CONCAT('body_null_safe_compare:', trigger_name),
    'uses <=>',
    IF(action_statement LIKE '%<=>%', 'NULL-safe', 'bare <> — pre-G12'),
    IF(action_statement LIKE '%<=>%', 'PASS', 'FAIL')
FROM information_schema.triggers
WHERE trigger_schema = DATABASE()
  AND trigger_name IN ('trg_visit_requests_primary_contact_guard_bi',
                       'trg_visit_requests_primary_contact_guard_bu',
                       'trg_users_protect_active_primary_contact_bu',
                       'trg_visit_request_identity_changes_user_guard_bi',
                       'trg_visit_request_identity_changes_user_guard_bu');

INSERT INTO pems_guard_verify_results (check_name, expected, actual, result)
SELECT
    'body_counts_role_lookup:trg_users_protect_active_primary_contact_bu',
    'counts the roles row before trusting the code',
    IF(action_statement LIKE '%v_new_role_count%', 'counted', 'uncounted — pre-G12'),
    IF(action_statement LIKE '%v_new_role_count%', 'PASS', 'FAIL')
FROM information_schema.triggers
WHERE trigger_schema = DATABASE()
  AND trigger_name = 'trg_users_protect_active_primary_contact_bu';

-- ── the six stable codes must all still be reachable in the bodies ───

INSERT INTO pems_guard_verify_results (check_name, expected, actual, result)
SELECT
    CONCAT('stable_code_present:', c.code),
    'signalled by at least one guard',
    CONCAT(COUNT(g.trigger_name), ' trigger(s)'),
    IF(COUNT(g.trigger_name) > 0, 'PASS', 'FAIL')
FROM (
    SELECT 'PRIMARY_CONTACT_USER_NOT_FOUND' AS code
    UNION ALL SELECT 'PRIMARY_CONTACT_USER_MUST_BE_ACTIVE_VISITOR'
    UNION ALL SELECT 'PRIMARY_CONTACT_VISITOR_ACCOUNT_INACTIVE'
    UNION ALL SELECT 'ACTIVE_PRIMARY_CONTACT_REQUIRES_VISITOR_USER'
    UNION ALL SELECT 'PENDING_PRIMARY_CONTACT_MUST_NOT_HAVE_VISITOR_USER'
    UNION ALL SELECT 'LINKED_PRIMARY_CONTACT_ROLE_CANNOT_CHANGE'
    UNION ALL SELECT 'LINKED_PRIMARY_CONTACT_CANNOT_BE_DEACTIVATED'
) c
LEFT JOIN information_schema.triggers g
       ON g.trigger_schema = DATABASE()
      AND g.trigger_name IN ('trg_visit_requests_primary_contact_guard_bi',
                             'trg_visit_requests_primary_contact_guard_bu',
                             'trg_users_protect_active_primary_contact_bu',
                             'trg_visit_request_identity_changes_user_guard_bi',
                             'trg_visit_request_identity_changes_user_guard_bu')
      AND g.action_statement LIKE CONCAT('%', c.code, '%')
GROUP BY c.code;

-- ── ordering: the two guards that declare FOLLOWS must still run second ──

INSERT INTO pems_guard_verify_results (check_name, expected, actual, result)
SELECT
    CONCAT('runs_after_existing_validator:', trigger_name),
    'action_order > 1',
    CONCAT('action_order = ', action_order),
    IF(action_order > 1, 'PASS', 'FAIL')
FROM information_schema.triggers
WHERE trigger_schema = DATABASE()
  AND trigger_name IN ('trg_visit_requests_primary_contact_guard_bu',
                       'trg_users_protect_active_primary_contact_bu',
                       'trg_visit_request_identity_changes_user_guard_bi',
                       'trg_visit_request_identity_changes_user_guard_bu');

-- ── nothing else on these tables was disturbed ───────────────────────

INSERT INTO pems_guard_verify_results (check_name, expected, actual, result)
SELECT
    'other_triggers_preserved',
    'the 4 FOLLOWS targets still exist',
    CONCAT(COUNT(*), ' of 4'),
    IF(COUNT(*) = 4, 'PASS', 'FAIL')
FROM information_schema.triggers
WHERE trigger_schema = DATABASE()
  AND trigger_name IN ('trg_visit_requests_cancel_validate_bu', 'trg_users_validate_bu',
                       'trg_identity_changes_transfer_bi', 'trg_identity_changes_transfer_bu');

-- ── behavioural probes: rolled back, so persisted data is untouched ──

DROP PROCEDURE IF EXISTS sp_pems_guard_verify_probes;
DELIMITER $$
CREATE PROCEDURE sp_pems_guard_verify_probes()
BEGIN
  DECLARE v_sqlstate CHAR(5) DEFAULT NULL;
  DECLARE v_message VARCHAR(1000) DEFAULT NULL;
  DECLARE v_raised BOOLEAN DEFAULT FALSE;
  DECLARE v_req BIGINT UNSIGNED DEFAULT NULL;
  DECLARE v_internal BIGINT UNSIGNED DEFAULT NULL;
  DECLARE v_visitor_role BIGINT UNSIGNED DEFAULT NULL;

  SELECT MIN(visit_request_id) INTO v_req FROM visit_requests
   WHERE visitor_user_id IS NOT NULL AND primary_contact_access_status = 'ACTIVE' AND status <> 'CANCELLED';
  SELECT MIN(u.user_id) INTO v_internal FROM users u JOIN roles r ON r.role_id = u.role_id
   WHERE r.role_code <> 'VISITOR';
  SELECT role_id INTO v_visitor_role FROM roles WHERE role_code = 'VISITOR';

  IF v_req IS NULL OR v_internal IS NULL OR v_visitor_role IS NULL THEN
    INSERT INTO pems_guard_verify_results (check_name, expected, actual, result)
    VALUES ('behavioural_probes', 'a live ACTIVE contact + an internal user + a VISITOR role',
            'fixture unavailable in this database — probes skipped', 'INFO');
  ELSE
    -- P1: an internal account may not become the primary contact.
    SET v_sqlstate = NULL; SET v_message = NULL; SET v_raised = FALSE;
    START TRANSACTION;
    BEGIN
      DECLARE CONTINUE HANDLER FOR SQLEXCEPTION
      BEGIN GET DIAGNOSTICS CONDITION 1 v_sqlstate = RETURNED_SQLSTATE, v_message = MESSAGE_TEXT; SET v_raised = TRUE; END;
      UPDATE visit_requests SET visitor_user_id = v_internal WHERE visit_request_id = v_req;
    END;
    ROLLBACK;
    INSERT INTO pems_guard_verify_results (check_name, expected, actual, result)
    VALUES ('probe_internal_user_rejected', '45000 PRIMARY_CONTACT_USER_MUST_BE_ACTIVE_VISITOR',
            CONCAT(COALESCE(v_sqlstate, 'NO_ERROR'), ' ', COALESCE(v_message, 'accepted')),
            IF(v_raised AND v_sqlstate = '45000' AND v_message = 'PRIMARY_CONTACT_USER_MUST_BE_ACTIVE_VISITOR', 'PASS', 'FAIL'));

    -- P2: the case the widened variable fixed — an unconfirmed VISITOR must produce the business
    -- code, not 22001 "Data too long".
    SET v_sqlstate = NULL; SET v_message = NULL; SET v_raised = FALSE;
    START TRANSACTION;
    BEGIN
      DECLARE CONTINUE HANDLER FOR SQLEXCEPTION
      BEGIN GET DIAGNOSTICS CONDITION 1 v_sqlstate = RETURNED_SQLSTATE, v_message = MESSAGE_TEXT; SET v_raised = TRUE; END;
      INSERT INTO users (user_id, role_id, sub_role, email, password_hash, full_name, status, primary_campus_id, department_id, created_at, updated_at)
      VALUES (99794, v_visitor_role, NULL, 'guard.verify.pending@example.test', 'x', 'Guard Verify Pending', 'PENDING_EMAIL_CONFIRMATION', NULL, NULL, NOW(), NULL);
      UPDATE visit_requests SET visitor_user_id = 99794 WHERE visit_request_id = v_req;
    END;
    ROLLBACK;
    INSERT INTO pems_guard_verify_results (check_name, expected, actual, result)
    VALUES ('probe_unconfirmed_visitor_gets_business_code', '45000 PRIMARY_CONTACT_VISITOR_ACCOUNT_INACTIVE',
            CONCAT(COALESCE(v_sqlstate, 'NO_ERROR'), ' ', COALESCE(v_message, 'accepted')),
            IF(v_raised AND v_sqlstate = '45000' AND v_message = 'PRIMARY_CONTACT_VISITOR_ACCOUNT_INACTIVE', 'PASS', 'FAIL'));

    -- P3: a valid relation must NOT be blocked. A guard that refuses everything is not a guard.
    SET v_sqlstate = NULL; SET v_message = NULL; SET v_raised = FALSE;
    START TRANSACTION;
    BEGIN
      DECLARE CONTINUE HANDLER FOR SQLEXCEPTION
      BEGIN GET DIAGNOSTICS CONDITION 1 v_sqlstate = RETURNED_SQLSTATE, v_message = MESSAGE_TEXT; SET v_raised = TRUE; END;
      UPDATE visit_requests SET updated_at = updated_at WHERE visit_request_id = v_req;
    END;
    ROLLBACK;
    INSERT INTO pems_guard_verify_results (check_name, expected, actual, result)
    VALUES ('probe_valid_relation_not_blocked', 'accepted',
            IF(v_raised, CONCAT(COALESCE(v_sqlstate, '?'), ' ', COALESCE(v_message, '?')), 'accepted'),
            IF(v_raised, 'FAIL', 'PASS'));
  END IF;
END$$
DELIMITER ;

CALL sp_pems_guard_verify_probes();
DROP PROCEDURE IF EXISTS sp_pems_guard_verify_probes;

-- ── report ───────────────────────────────────────────────────────────

SELECT sequence_no, check_name, expected, actual, result
FROM pems_guard_verify_results ORDER BY sequence_no;

SELECT
    SUM(result = 'PASS') AS pass_count,
    SUM(result = 'FAIL') AS fail_count,
    SUM(result = 'INFO') AS info_count
FROM pems_guard_verify_results;

-- ── gate ─────────────────────────────────────────────────────────────
-- Fails the process, so a pipeline does not have to parse the table above.

DROP PROCEDURE IF EXISTS pems_guard_verify_gate;
DELIMITER $$
CREATE PROCEDURE pems_guard_verify_gate()
BEGIN
  DECLARE v_fail INT DEFAULT 0;
  SELECT COUNT(*) INTO v_fail FROM pems_guard_verify_results WHERE result = 'FAIL';
  IF v_fail > 0 THEN
    SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'contact_guard_closure verification FAILED — see the table above.';
  END IF;
END$$
DELIMITER ;

CALL pems_guard_verify_gate();
DROP PROCEDURE IF EXISTS pems_guard_verify_gate;

SELECT 'contact_guard_closure 03_verify.sql: all checks passed' AS status;
