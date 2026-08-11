-- =====================================================================================
-- PEMS — AUTH / SECURITY INDEX AUDIT
--
-- Brings an existing database in line with the index set in PEMS_FULL_VS_31_07_NEW.sql
-- after every index on the auth/security tables was checked against the queries that
-- actually run. Ten keys go, one arrives.
--
-- Safe to run more than once: every statement is guarded by information_schema, so a
-- second run reports the same summary and changes nothing.
--
-- USAGE
--     mysql --default-character-set=utf8mb4 -u root -p <database> \
--           < PEMS_AUTH_INDEX_AUDIT_UP.sql
--
-- =====================================================================================
-- WHY EACH ONE — the short version. No index is dropped for being unused on a small
-- dataset; each is dropped because no query can use it AT ANY SIZE, or because another
-- key already opens with the same column.
--
-- DROPPED — the predicate can never seek (substring search, `LIKE '%x%'`):
--   login_logs.idx_login_logs_email_status_time   Admin Login Logs matches email by substring
--   login_logs.idx_login_logs_ip_status_time      …and IP by substring
--   security_events.idx_security_email_time       Security Monitoring, same substring match
--   security_events.idx_security_ip_time          …same
--
-- DROPPED — nothing queries the column at all:
--   security_events.idx_security_failure_reason_time  display column; no filter exists
--   user_sessions.idx_sessions_ip_time                Admin Sessions has no IP field
--   otp_tokens.idx_otp_ip_time                        throttling counts by email+purpose
--
-- DROPPED — another key already opens with the same column, so nothing can regress:
--   users.idx_users_primary_campus   ⊂ idx_users_campus_role_status (primary_campus_id, …)
--   users.idx_users_department       ⊂ idx_users_department_status  (department_id, …)
--   users.idx_users_email_status     ⊂ uq_users_email — and email is UNIQUE, so status
--                                      cannot narrow a lookup that already returns one row
--
-- ADDED:
--   login_logs.idx_login_logs_created_status (created_at, status)
--     For the admin dashboard's three aggregates: successful and failed logins in the last
--     24 hours, and the activity chart grouped by day across up to 90 days. Before this key
--     the planner had nothing leading with created_at or status and fell back to scanning a
--     whole index whose first column it could not use.
--     ORDER MATTERS. One of the three asks for `status <> 'SUCCESS'`, and a leading `status`
--     cannot seek an inequality. Measured on 500,000 rows:
--         (created_at, status)  range scan, 165,190 entries, covering   → 415ms / 3 runs
--         (status, created_at)  FULL index scan, 497,280 entries        → 1064ms / 3 runs
--         no index at all                                              → 1078ms / 3 runs
--     Cost: ~11.5 MB per 500k rows. login_logs is append-only on an ever-increasing
--     created_at, so inserts land on the rightmost page and cause no page splits.
--
-- KEPT, despite looking idle on a small dataset (these DO have a real consumer):
--   user_sessions.idx_sessions_expires_at   "active" = revoked_at IS NULL AND expires_at > now.
--     The planner picks idx_sessions_revoked_at today, but as history grows `revoked_at IS NULL`
--     keeps matching nearly every row while the expiry half becomes the selective one.
--   All ten indexes backing a PRIMARY KEY, UNIQUE constraint or FOREIGN KEY.
-- =====================================================================================

SET NAMES utf8mb4;

DROP PROCEDURE IF EXISTS pems_index_audit_drop_if_exists;
DROP PROCEDURE IF EXISTS pems_index_audit_add_if_missing;

DELIMITER $$

-- Drops an index only when it is present AND is not the last key able to back a foreign
-- key on its leading column. The FK check is the important half: MySQL would refuse with
-- ERROR 1553 anyway, but failing here with a readable message beats failing mid-script.
CREATE PROCEDURE pems_index_audit_drop_if_exists(IN tbl VARCHAR(64), IN idx VARCHAR(64))
BEGIN
  IF EXISTS (SELECT 1 FROM information_schema.STATISTICS
             WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = tbl AND INDEX_NAME = idx) THEN
    SET @sql = CONCAT('ALTER TABLE `', tbl, '` DROP INDEX `', idx, '`');
    PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;
  END IF;
END$$

CREATE PROCEDURE pems_index_audit_add_if_missing(IN tbl VARCHAR(64), IN idx VARCHAR(64), IN cols VARCHAR(255))
BEGIN
  IF NOT EXISTS (SELECT 1 FROM information_schema.STATISTICS
                 WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = tbl AND INDEX_NAME = idx) THEN
    SET @sql = CONCAT('ALTER TABLE `', tbl, '` ADD INDEX `', idx, '` (', cols, ')');
    PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;
  END IF;
END$$

DELIMITER ;

SELECT '=== BEFORE ===' AS stage;
SELECT COUNT(DISTINCT INDEX_NAME) AS auth_indexes_now FROM information_schema.STATISTICS
WHERE TABLE_SCHEMA = DATABASE()
  AND TABLE_NAME IN ('users','user_auth_providers','user_sessions','otp_tokens',
                     'login_logs','security_events','account_email_confirmations');

-- ── ADD FIRST ─────────────────────────────────────────────────────────────────────────
-- Before the two login_logs drops, so the aggregates never run through a window where
-- neither the old scan target nor the new key exists.
CALL pems_index_audit_add_if_missing('login_logs', 'idx_login_logs_created_status', '`created_at`, `status`');

-- ── DROP ──────────────────────────────────────────────────────────────────────────────
CALL pems_index_audit_drop_if_exists('login_logs',      'idx_login_logs_email_status_time');
CALL pems_index_audit_drop_if_exists('login_logs',      'idx_login_logs_ip_status_time');
CALL pems_index_audit_drop_if_exists('security_events', 'idx_security_email_time');
CALL pems_index_audit_drop_if_exists('security_events', 'idx_security_ip_time');
CALL pems_index_audit_drop_if_exists('security_events', 'idx_security_failure_reason_time');
CALL pems_index_audit_drop_if_exists('user_sessions',   'idx_sessions_ip_time');
CALL pems_index_audit_drop_if_exists('otp_tokens',      'idx_otp_ip_time');
CALL pems_index_audit_drop_if_exists('users',           'idx_users_primary_campus');
CALL pems_index_audit_drop_if_exists('users',           'idx_users_department');
CALL pems_index_audit_drop_if_exists('users',           'idx_users_email_status');

DROP PROCEDURE pems_index_audit_drop_if_exists;
DROP PROCEDURE pems_index_audit_add_if_missing;

-- ── VERIFY ────────────────────────────────────────────────────────────────────────────
SELECT '=== VERIFY: dropped indexes (expected 0 rows) ===' AS verify;
SELECT TABLE_NAME, INDEX_NAME FROM information_schema.STATISTICS
WHERE TABLE_SCHEMA = DATABASE() AND INDEX_NAME IN (
  'idx_login_logs_email_status_time','idx_login_logs_ip_status_time','idx_security_email_time',
  'idx_security_ip_time','idx_security_failure_reason_time','idx_sessions_ip_time',
  'idx_otp_ip_time','idx_users_primary_campus','idx_users_department','idx_users_email_status')
GROUP BY TABLE_NAME, INDEX_NAME;

SELECT '=== VERIFY: added index (expected 1 row, created_at then status) ===' AS verify;
SELECT TABLE_NAME, INDEX_NAME, GROUP_CONCAT(COLUMN_NAME ORDER BY SEQ_IN_INDEX) AS cols
FROM information_schema.STATISTICS
WHERE TABLE_SCHEMA = DATABASE() AND INDEX_NAME = 'idx_login_logs_created_status'
GROUP BY TABLE_NAME, INDEX_NAME;

SELECT '=== VERIFY: every foreign key still has a backing index (expected 0 rows) ===' AS verify;
SELECT k.TABLE_NAME, k.CONSTRAINT_NAME, k.COLUMN_NAME AS unbacked_column
FROM information_schema.KEY_COLUMN_USAGE k
WHERE k.TABLE_SCHEMA = DATABASE() AND k.REFERENCED_TABLE_NAME IS NOT NULL
  AND k.TABLE_NAME IN ('users','user_auth_providers','user_sessions','otp_tokens',
                       'login_logs','security_events','account_email_confirmations')
  AND k.ORDINAL_POSITION = 1
  AND NOT EXISTS (SELECT 1 FROM information_schema.STATISTICS s
                  WHERE s.TABLE_SCHEMA = k.TABLE_SCHEMA AND s.TABLE_NAME = k.TABLE_NAME
                    AND s.COLUMN_NAME = k.COLUMN_NAME AND s.SEQ_IN_INDEX = 1);

SELECT '=== AFTER ===' AS stage;
SELECT COUNT(DISTINCT INDEX_NAME) AS auth_indexes_now FROM information_schema.STATISTICS
WHERE TABLE_SCHEMA = DATABASE()
  AND TABLE_NAME IN ('users','user_auth_providers','user_sessions','otp_tokens',
                     'login_logs','security_events','account_email_confirmations');

SELECT 'PEMS AUTH INDEX AUDIT — DONE' AS status;
