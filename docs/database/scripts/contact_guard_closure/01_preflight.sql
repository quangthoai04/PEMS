-- =====================================================================
-- 01_preflight.sql — read-only survey before the G12 contact-guard replacement
--
-- Changes nothing. Run it against the database you intend to migrate and read the output before
-- running 02_up_replace_triggers.sql.
--
--     USE pems_stage_2026_08;
--     SOURCE 01_preflight.sql;
--
-- What you are checking for:
--   * all five guard triggers are present (a missing one means this database predates the guards
--     and needs the canonical import path, not this migration);
--   * whether each trigger already carries the hardened body — the marker is the NULL-safe `<=>`
--     comparison, which the pre-G12 bodies do not contain;
--   * that no row currently violates the invariant the guards enforce. The migration does not
--     repair data; it only replaces trigger bodies. A database with existing violations will keep
--     them, and every later write to those rows will start failing. Fix them first.
-- =====================================================================

SET NAMES utf8mb4;

SELECT '── target ──────────────────────────────────────────' AS section;

SELECT
    DATABASE() AS current_database,
    CASE
        WHEN DATABASE() IS NULL THEN 'NO DATABASE SELECTED — USE one before running the migration.'
        WHEN DATABASE() = 'pems_db' THEN 'PROTECTED — this is the deployed database. Migrate it only under an approved change window.'
        ELSE 'ok'
    END AS target_note,
    VERSION() AS server_version;

SELECT '── guard triggers present ──────────────────────────' AS section;

SELECT
    t.name AS expected_trigger,
    IF(g.trigger_name IS NULL, 'MISSING', 'present') AS presence,
    COALESCE(g.event_manipulation, '-') AS event,
    COALESCE(g.event_object_table, '-') AS on_table,
    COALESCE(g.action_order, 0) AS action_order,
    -- The marker has to be chosen per trigger. A generic "contains <=>" or "contains VARCHAR(30)"
    -- test reports trg_users_protect_active_primary_contact_bu as already hardened, because its
    -- pre-G12 body legitimately contains both (`NEW.role_id <=> OLD.role_id`, and a VARCHAR(30)
    -- role-code variable). v_new_role_count exists only in the G12 body.
    CASE
        WHEN g.trigger_name IS NULL THEN '-'
        WHEN t.name = 'trg_users_protect_active_primary_contact_bu' THEN
            IF(g.action_statement LIKE '%v_new_role_count%', 'HARDENED (G12 already applied)', 'pre-G12 body — replace')
        ELSE
            IF(g.action_statement LIKE '%v_user_status VARCHAR(30)%' AND g.action_statement LIKE '%LEFT JOIN roles%',
               'HARDENED (G12 already applied)', 'pre-G12 body — replace')
    END AS body_state
FROM (
    SELECT 'trg_visit_requests_primary_contact_guard_bi' AS name
    UNION ALL SELECT 'trg_visit_requests_primary_contact_guard_bu'
    UNION ALL SELECT 'trg_users_protect_active_primary_contact_bu'
    UNION ALL SELECT 'trg_visit_request_identity_changes_user_guard_bi'
    UNION ALL SELECT 'trg_visit_request_identity_changes_user_guard_bu'
) t
LEFT JOIN information_schema.triggers g
       ON g.trigger_schema = DATABASE() AND g.trigger_name = t.name
ORDER BY t.name;

SELECT '── triggers this migration must NOT disturb ────────' AS section;

-- The replacement drops and recreates five triggers by name. Everything else on these three tables
-- must survive untouched, and two of the five declare FOLLOWS against a trigger in this list — so if
-- one of those is missing, the recreate would fail and you need to know that before starting.
SELECT
    event_object_table AS on_table,
    trigger_name,
    event_manipulation AS event,
    action_order,
    IF(trigger_name IN (
        'trg_visit_requests_primary_contact_guard_bi',
        'trg_visit_requests_primary_contact_guard_bu',
        'trg_users_protect_active_primary_contact_bu',
        'trg_visit_request_identity_changes_user_guard_bi',
        'trg_visit_request_identity_changes_user_guard_bu'
    ), 'REPLACED BY THIS MIGRATION', 'must be preserved') AS disposition
FROM information_schema.triggers
WHERE trigger_schema = DATABASE()
  AND event_object_table IN ('visit_requests', 'users', 'visit_request_identity_changes')
ORDER BY event_object_table, event_manipulation, action_order;

SELECT '── existing data violations (must be 0) ────────────' AS section;

SELECT 'active_contact_with_wrong_role_or_status' AS check_name, COUNT(*) AS issue_count
FROM visit_requests vr
JOIN users u ON u.user_id = vr.visitor_user_id
LEFT JOIN roles r ON r.role_id = u.role_id
WHERE vr.primary_contact_access_status = 'ACTIVE'
  AND (NOT (r.role_code <=> 'VISITOR') OR NOT (u.status <=> 'ACTIVE'));

SELECT 'active_contact_without_visitor_user' AS check_name, COUNT(*) AS issue_count
FROM visit_requests
WHERE primary_contact_access_status = 'ACTIVE' AND visitor_user_id IS NULL;

SELECT 'pending_contact_with_visitor_user' AS check_name, COUNT(*) AS issue_count
FROM visit_requests
WHERE primary_contact_access_status = 'PENDING_CONFIRMATION' AND visitor_user_id IS NOT NULL;

SELECT 'identity_change_targeting_non_active_visitor' AS check_name, COUNT(*) AS issue_count
FROM visit_request_identity_changes ic
JOIN users u ON u.user_id = ic.new_user_id
LEFT JOIN roles r ON r.role_id = u.role_id
WHERE ic.new_user_id IS NOT NULL
  AND (NOT (r.role_code <=> 'VISITOR') OR NOT (u.status <=> 'ACTIVE'));

SELECT 'contact_pointing_at_missing_user' AS check_name, COUNT(*) AS issue_count
FROM visit_requests vr
LEFT JOIN users u ON u.user_id = vr.visitor_user_id
WHERE vr.visitor_user_id IS NOT NULL AND u.user_id IS NULL;

SELECT '── reachable account states (context, not a gate) ──' AS section;

-- PENDING_EMAIL_CONFIRMATION is 26 characters. The pre-G12 triggers read users.status into a
-- VARCHAR(20), so a visitor in this state made the guard raise 22001 "Data too long" instead of
-- PRIMARY_CONTACT_VISITOR_ACCOUNT_INACTIVE. The count below is how often that is reachable here.
SELECT
    u.status AS account_status,
    COUNT(*) AS visitor_count
FROM users u
JOIN roles r ON r.role_id = u.role_id
WHERE r.role_code = 'VISITOR'
GROUP BY u.status
ORDER BY u.status;
