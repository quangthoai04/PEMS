-- =====================================================================================
-- PEMS — AUTH / SECURITY SCHEMA CLEANUP (UP)
--
-- Brings an EXISTING database to the same auth/security schema that a fresh import of
-- docs/database/scripts/PEMS_FULL_VS_31_07_NEW.sql now produces.
--
-- Scope (7 tables): users · user_auth_providers · user_sessions · otp_tokens ·
--                   login_logs · security_events
--
-- PROPERTIES
--   * IDEMPOTENT — every DDL statement is guarded by an INFORMATION_SCHEMA check, so a
--     second run is a no-op. Safe to re-run after a partial failure.
--   * DESTRUCTIVE — it DROPS columns. The data in those columns is gone for good; they
--     were audited as redundant/unused, but TAKE A BACKUP FIRST:
--         mysqldump -u root -p --single-transaction --routines --triggers <db> > backup.sql
--   * FAIL-FAST — section 1 ABORTS the run when legacy values still live in an ENUM that
--     this migration narrows. Nothing is deleted or rewritten to make an ALTER succeed.
--     See PEMS_AUTH_SCHEMA_CLEANUP_LEGACY_REMEDIATION.sql for the opt-in handling.
--
-- USAGE
--     mysql --default-character-set=utf8mb4 -u root -p <database> \
--           < PEMS_AUTH_SCHEMA_CLEANUP_UP.sql
--
--   Always pass --default-character-set=utf8mb4: without it the client mojibakes every
--   Vietnamese string it touches.
--
-- WHAT CHANGES (and why the column was dropped)
--   users
--     - fe_id, uq_users_fe_id ....... FEID has no product flow; removed end-to-end.
--     - email_verified_at ........... users.status (PENDING_EMAIL_CONFIRMATION) is the
--                                     single source of "has this address been proven".
--     - first_login_at .............. last_login_at IS NULL answers "never signed in".
--   user_auth_providers
--     - provider_email .............. a provider always authenticates against its
--                                     account's users.email; the copy only drifted.
--     - is_enabled .................. never disabled by any flow; the gate that matters
--                                     is users.status + AuthOptions.
--     - last_used_at ................ written but never read; login_logs is the trail.
--     - provider_type ............... ENUM loses 'FEID'.
--     - idx_auth_provider_email, idx_auth_provider_type_email_enabled — both index a
--       dropped column.
--     - triggers rewritten: only GOOGLE_SSO requires provider_subject.
--   user_sessions
--     - selected_campus_id .......... an internal account has exactly one primary campus
--                                     (users.primary_campus_id); the trigger only ever
--                                     copied it back. FK + index go with it.
--     - refresh_expires_at .......... mirrored expires_at 1:1.
--     - refresh_revoked_at .......... mirrored revoked_at 1:1.
--     - idx_sessions_refresh_active — indexed the two mirrors.
--     - trg_sessions_validate_bi rewritten: portal/role rules only.
--   otp_tokens
--     - token_type .................. only ever 'OTP_CODE'; MAGIC_LINK never shipped.
--     - last_attempt_at ............. attempt_count + next_attempt_allowed_at drive the
--                                     cooldown; this was written and never read.
--     - human_verified_at ........... the new challenge's issue_reason = HUMAN_RECOVERY
--                                     is the recovery marker (and what the quota counts).
--     - resend_count ................ resend limits count rows by
--                                     (email, purpose, issue_reason, created_at).
--     - 4 unused indexes dropped (see section 5).
--   login_logs
--     - selected_campus_id + FK + idx_login_logs_portal_campus — duplicated
--       users.primary_campus_id per row.
--     - session_id .................. never populated by any caller.
--     - provider_type ............... ENUM loses 'FEID'.
--   security_events
--     - severity KEPT (LOW/MEDIUM/HIGH/CRITICAL) — it drives the Security Monitoring
--       filter and the dashboard panel. It is now WRITTEN properly by
--       SecuritySeverityResolver instead of always falling through to the LOW default.
--     - selected_campus_id + FK + idx_security_portal_campus_time — campus-scoped events
--       already carry `campusId=…` inside detail_text.
--     - provider_type ............... duplicated login_logs.provider_type.
--     - session_id + FK + idx_security_session_time — never populated.
--     - event_type .................. ENUM narrows to the 3 values with a real producer.
--     - failure_reason_code ......... ENUM -> VARCHAR(80). Machine-readable codes grow
--                                     with real flows, and VARCHAR preserves every
--                                     historical value instead of truncating it.
--
-- INDEXES DELIBERATELY LEFT ALONE (no EXPLAIN evidence that they are dead — reported,
-- not dropped): idx_sessions_ip_time, idx_otp_email_purpose_active, idx_otp_ip_time,
-- idx_login_logs_user_time, idx_login_logs_email_status_time, idx_login_logs_ip_status_time,
-- idx_security_user_time, idx_security_email_time, idx_security_failure_reason_time,
-- idx_security_ip_time, idx_security_severity_time.
-- =====================================================================================

SET @OLD_SQL_MODE = @@SESSION.sql_mode;
SET SESSION sql_mode = 'STRICT_ALL_TABLES';
SET NAMES utf8mb4;

-- Helper: run a DDL statement only when a guard query returns > 0.
DROP PROCEDURE IF EXISTS pems_auth_cleanup_exec_if;
DELIMITER $$
CREATE PROCEDURE pems_auth_cleanup_exec_if(IN guard_sql TEXT, IN ddl_sql TEXT)
BEGIN
  DECLARE v_hits INT DEFAULT 0;
  SET @pems_guard_sql = CONCAT('SELECT COUNT(*) INTO @pems_guard_hits FROM (', guard_sql, ') g');
  PREPARE s FROM @pems_guard_sql; EXECUTE s; DEALLOCATE PREPARE s;
  SET v_hits = @pems_guard_hits;
  IF v_hits > 0 THEN
    SET @pems_ddl_sql = ddl_sql;
    PREPARE s FROM @pems_ddl_sql; EXECUTE s; DEALLOCATE PREPARE s;
  END IF;
END$$
DELIMITER ;

-- Shorthands for the two guards used everywhere below.
SET @db = DATABASE();


-- =====================================================================================
-- 1. PRECHECK — report, then ABORT if a narrowing ALTER would destroy existing values
-- =====================================================================================

SELECT '=== PRECHECK: rows carrying values this migration would drop ===' AS `precheck`;

-- Every count is taken through dynamic SQL guarded by an INFORMATION_SCHEMA lookup, so a
-- SECOND run — where the column or the ENUM value no longer exists — reports 0 instead of
-- failing with "Unknown column". That is what makes the whole script re-runnable.
DROP PROCEDURE IF EXISTS pems_auth_cleanup_count_if;
DELIMITER $$
CREATE PROCEDURE pems_auth_cleanup_count_if(IN guard_sql TEXT, IN count_sql TEXT)
BEGIN
  SET @pems_guard_sql = CONCAT('SELECT COUNT(*) INTO @pems_guard_hits FROM (', guard_sql, ') g');
  PREPARE s FROM @pems_guard_sql; EXECUTE s; DEALLOCATE PREPARE s;
  IF @pems_guard_hits > 0 THEN
    SET @pems_count_sql = CONCAT('SELECT COUNT(*) INTO @pems_count FROM (', count_sql, ') c');
    PREPARE s FROM @pems_count_sql; EXECUTE s; DEALLOCATE PREPARE s;
  ELSE
    SET @pems_count = 0;
  END IF;
END$$
DELIMITER ;

-- Blocker 1 — FEID bindings (only countable while the ENUM still offers the value).
CALL pems_auth_cleanup_count_if(
  "SELECT 1 FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE()
     AND TABLE_NAME = 'user_auth_providers' AND COLUMN_NAME = 'provider_type'
     AND COLUMN_TYPE LIKE '%FEID%'",
  "SELECT 1 FROM user_auth_providers WHERE provider_type = 'FEID'");
SET @blk_providers = @pems_count;

-- Blocker 2 — FEID login rows.
CALL pems_auth_cleanup_count_if(
  "SELECT 1 FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE()
     AND TABLE_NAME = 'login_logs' AND COLUMN_NAME = 'provider_type'
     AND COLUMN_TYPE LIKE '%FEID%'",
  "SELECT 1 FROM login_logs WHERE provider_type = 'FEID'");
SET @blk_loginlogs = @pems_count;

-- Blocker 3 — security events on a removed event_type.
CALL pems_auth_cleanup_count_if(
  "SELECT 1 FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE()
     AND TABLE_NAME = 'security_events' AND COLUMN_NAME = 'event_type'
     AND COLUMN_TYPE <> 'enum(''SSO_LOGIN'',''SESSION_REVOKED'',''SECURITY_POLICY_CHECK'')'",
  "SELECT 1 FROM security_events
     WHERE event_type NOT IN ('SSO_LOGIN','SESSION_REVOKED','SECURITY_POLICY_CHECK')");
SET @blk_events = @pems_count;

-- Informational — these columns are dropped outright; the value goes with them.
CALL pems_auth_cleanup_count_if(
  "SELECT 1 FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE()
     AND TABLE_NAME = 'users' AND COLUMN_NAME = 'fe_id'",
  "SELECT 1 FROM users WHERE fe_id IS NOT NULL");
SET @info_feid = @pems_count;

CALL pems_auth_cleanup_count_if(
  "SELECT 1 FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE()
     AND TABLE_NAME = 'security_events' AND COLUMN_NAME = 'provider_type'",
  "SELECT 1 FROM security_events WHERE provider_type IS NOT NULL");
SET @info_secprov = @pems_count;

DROP PROCEDURE pems_auth_cleanup_count_if;

SELECT 'user_auth_providers.provider_type = FEID'  AS what, @blk_providers AS rows_found, 'BLOCKS the ALTER' AS impact
UNION ALL SELECT 'login_logs.provider_type = FEID',           @blk_loginlogs, 'BLOCKS the ALTER'
UNION ALL SELECT 'security_events.event_type = legacy value', @blk_events,    'BLOCKS the ALTER'
UNION ALL SELECT 'users.fe_id IS NOT NULL',                   @info_feid,     'column dropped — value lost by design'
UNION ALL SELECT 'security_events.provider_type IS NOT NULL', @info_secprov,  'column dropped — value lost by design';

-- Abort with a readable message rather than letting MySQL truncate audit history.
SET @blockers = @blk_providers + @blk_loginlogs + @blk_events;

DROP PROCEDURE IF EXISTS pems_auth_cleanup_assert_clean;
DELIMITER $$
CREATE PROCEDURE pems_auth_cleanup_assert_clean(IN blockers INT)
BEGIN
  -- MESSAGE_TEXT is capped at 128 chars by MySQL, hence the terse wording; the PRECHECK
  -- table printed just above names every blocking row, and the remediation script's own
  -- header explains exactly what it does to each one.
  IF blockers > 0 THEN
    SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT =
      'ABORTED, nothing changed: legacy ENUM values remain (see PRECHECK above). Run ..._LEGACY_REMEDIATION.sql first, then retry.';
  END IF;
END$$
DELIMITER ;

CALL pems_auth_cleanup_assert_clean(@blockers);
DROP PROCEDURE pems_auth_cleanup_assert_clean;


-- =====================================================================================
-- 2. DROP dependent FOREIGN KEYS  (must precede the column drops)
-- =====================================================================================

CALL pems_auth_cleanup_exec_if(
  "SELECT 1 FROM information_schema.TABLE_CONSTRAINTS WHERE CONSTRAINT_SCHEMA = DATABASE()
     AND TABLE_NAME = 'user_sessions' AND CONSTRAINT_NAME = 'fk_sessions_selected_campus'",
  'ALTER TABLE user_sessions DROP FOREIGN KEY fk_sessions_selected_campus');

CALL pems_auth_cleanup_exec_if(
  "SELECT 1 FROM information_schema.TABLE_CONSTRAINTS WHERE CONSTRAINT_SCHEMA = DATABASE()
     AND TABLE_NAME = 'login_logs' AND CONSTRAINT_NAME = 'fk_login_logs_campus'",
  'ALTER TABLE login_logs DROP FOREIGN KEY fk_login_logs_campus');

CALL pems_auth_cleanup_exec_if(
  "SELECT 1 FROM information_schema.TABLE_CONSTRAINTS WHERE CONSTRAINT_SCHEMA = DATABASE()
     AND TABLE_NAME = 'security_events' AND CONSTRAINT_NAME = 'fk_security_events_selected_campus'",
  'ALTER TABLE security_events DROP FOREIGN KEY fk_security_events_selected_campus');

CALL pems_auth_cleanup_exec_if(
  "SELECT 1 FROM information_schema.TABLE_CONSTRAINTS WHERE CONSTRAINT_SCHEMA = DATABASE()
     AND TABLE_NAME = 'security_events' AND CONSTRAINT_NAME = 'fk_security_events_session'",
  'ALTER TABLE security_events DROP FOREIGN KEY fk_security_events_session');


-- =====================================================================================
-- 3. DROP dependent INDEXES  (must precede the column drops)
-- =====================================================================================

-- users
CALL pems_auth_cleanup_exec_if(
  "SELECT 1 FROM information_schema.STATISTICS WHERE TABLE_SCHEMA = DATABASE()
     AND TABLE_NAME = 'users' AND INDEX_NAME = 'uq_users_fe_id' LIMIT 1",
  'ALTER TABLE users DROP INDEX uq_users_fe_id');

-- user_auth_providers
CALL pems_auth_cleanup_exec_if(
  "SELECT 1 FROM information_schema.STATISTICS WHERE TABLE_SCHEMA = DATABASE()
     AND TABLE_NAME = 'user_auth_providers' AND INDEX_NAME = 'idx_auth_provider_email' LIMIT 1",
  'ALTER TABLE user_auth_providers DROP INDEX idx_auth_provider_email');

CALL pems_auth_cleanup_exec_if(
  "SELECT 1 FROM information_schema.STATISTICS WHERE TABLE_SCHEMA = DATABASE()
     AND TABLE_NAME = 'user_auth_providers' AND INDEX_NAME = 'idx_auth_provider_type_email_enabled' LIMIT 1",
  'ALTER TABLE user_auth_providers DROP INDEX idx_auth_provider_type_email_enabled');

-- user_sessions
CALL pems_auth_cleanup_exec_if(
  "SELECT 1 FROM information_schema.STATISTICS WHERE TABLE_SCHEMA = DATABASE()
     AND TABLE_NAME = 'user_sessions' AND INDEX_NAME = 'idx_sessions_portal_campus' LIMIT 1",
  'ALTER TABLE user_sessions DROP INDEX idx_sessions_portal_campus');

CALL pems_auth_cleanup_exec_if(
  "SELECT 1 FROM information_schema.STATISTICS WHERE TABLE_SCHEMA = DATABASE()
     AND TABLE_NAME = 'user_sessions' AND INDEX_NAME = 'idx_sessions_refresh_active' LIMIT 1",
  'ALTER TABLE user_sessions DROP INDEX idx_sessions_refresh_active');

-- otp_tokens — four indexes with no query behind them.
--   idx_otp_submission            : submission_id is only ever read together with the
--                                   challenge hash, which has its own UNIQUE index.
--   idx_otp_email_purpose_active_v2 / idx_otp_user_purpose_active :
--                                   verify looks a challenge up by challenge_token_hash
--                                   (UNIQUE), never by these prefixes.
--   idx_otp_issue_limit           : the quota query filters (email, purpose, created_at)
--                                   and classifies issue_reason in memory, so
--                                   idx_otp_email_purpose_time already covers it.
CALL pems_auth_cleanup_exec_if(
  "SELECT 1 FROM information_schema.STATISTICS WHERE TABLE_SCHEMA = DATABASE()
     AND TABLE_NAME = 'otp_tokens' AND INDEX_NAME = 'idx_otp_submission' LIMIT 1",
  'ALTER TABLE otp_tokens DROP INDEX idx_otp_submission');

CALL pems_auth_cleanup_exec_if(
  "SELECT 1 FROM information_schema.STATISTICS WHERE TABLE_SCHEMA = DATABASE()
     AND TABLE_NAME = 'otp_tokens' AND INDEX_NAME = 'idx_otp_email_purpose_active_v2' LIMIT 1",
  'ALTER TABLE otp_tokens DROP INDEX idx_otp_email_purpose_active_v2');

CALL pems_auth_cleanup_exec_if(
  "SELECT 1 FROM information_schema.STATISTICS WHERE TABLE_SCHEMA = DATABASE()
     AND TABLE_NAME = 'otp_tokens' AND INDEX_NAME = 'idx_otp_issue_limit' LIMIT 1",
  'ALTER TABLE otp_tokens DROP INDEX idx_otp_issue_limit');

-- idx_otp_user_purpose_active is the ONLY index leading with user_id, so fk_otp_tokens_user
-- currently leans on it — MySQL refuses to drop it while that is true (ERROR 1553). Give the
-- foreign key a dedicated single-column index FIRST, then drop the wide one: same constraint,
-- 4 columns of index turned into 1.
CALL pems_auth_cleanup_exec_if(
  "SELECT 1 FROM information_schema.STATISTICS s WHERE s.TABLE_SCHEMA = DATABASE()
     AND s.TABLE_NAME = 'otp_tokens' AND s.INDEX_NAME = 'idx_otp_user_purpose_active'
     AND NOT EXISTS (SELECT 1 FROM information_schema.STATISTICS s2
                     WHERE s2.TABLE_SCHEMA = DATABASE()
                       AND s2.TABLE_NAME = 'otp_tokens' AND s2.INDEX_NAME = 'idx_otp_user')
   LIMIT 1",
  'ALTER TABLE otp_tokens ADD INDEX idx_otp_user (user_id)');

CALL pems_auth_cleanup_exec_if(
  "SELECT 1 FROM information_schema.STATISTICS WHERE TABLE_SCHEMA = DATABASE()
     AND TABLE_NAME = 'otp_tokens' AND INDEX_NAME = 'idx_otp_user_purpose_active' LIMIT 1",
  'ALTER TABLE otp_tokens DROP INDEX idx_otp_user_purpose_active');

-- login_logs
CALL pems_auth_cleanup_exec_if(
  "SELECT 1 FROM information_schema.STATISTICS WHERE TABLE_SCHEMA = DATABASE()
     AND TABLE_NAME = 'login_logs' AND INDEX_NAME = 'idx_login_logs_portal_campus' LIMIT 1",
  'ALTER TABLE login_logs DROP INDEX idx_login_logs_portal_campus');

-- security_events
CALL pems_auth_cleanup_exec_if(
  "SELECT 1 FROM information_schema.STATISTICS WHERE TABLE_SCHEMA = DATABASE()
     AND TABLE_NAME = 'security_events' AND INDEX_NAME = 'idx_security_portal_campus_time' LIMIT 1",
  'ALTER TABLE security_events DROP INDEX idx_security_portal_campus_time');

CALL pems_auth_cleanup_exec_if(
  "SELECT 1 FROM information_schema.STATISTICS WHERE TABLE_SCHEMA = DATABASE()
     AND TABLE_NAME = 'security_events' AND INDEX_NAME = 'idx_security_session_time' LIMIT 1",
  'ALTER TABLE security_events DROP INDEX idx_security_session_time');


-- =====================================================================================
-- 4. DROP COLUMNS
-- =====================================================================================

CALL pems_auth_cleanup_exec_if(
  "SELECT 1 FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE()
     AND TABLE_NAME = 'users' AND COLUMN_NAME = 'fe_id'",
  'ALTER TABLE users DROP COLUMN fe_id');

CALL pems_auth_cleanup_exec_if(
  "SELECT 1 FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE()
     AND TABLE_NAME = 'users' AND COLUMN_NAME = 'email_verified_at'",
  'ALTER TABLE users DROP COLUMN email_verified_at');

CALL pems_auth_cleanup_exec_if(
  "SELECT 1 FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE()
     AND TABLE_NAME = 'users' AND COLUMN_NAME = 'first_login_at'",
  'ALTER TABLE users DROP COLUMN first_login_at');

CALL pems_auth_cleanup_exec_if(
  "SELECT 1 FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE()
     AND TABLE_NAME = 'user_auth_providers' AND COLUMN_NAME = 'provider_email'",
  'ALTER TABLE user_auth_providers DROP COLUMN provider_email');

CALL pems_auth_cleanup_exec_if(
  "SELECT 1 FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE()
     AND TABLE_NAME = 'user_auth_providers' AND COLUMN_NAME = 'is_enabled'",
  'ALTER TABLE user_auth_providers DROP COLUMN is_enabled');

CALL pems_auth_cleanup_exec_if(
  "SELECT 1 FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE()
     AND TABLE_NAME = 'user_auth_providers' AND COLUMN_NAME = 'last_used_at'",
  'ALTER TABLE user_auth_providers DROP COLUMN last_used_at');

CALL pems_auth_cleanup_exec_if(
  "SELECT 1 FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE()
     AND TABLE_NAME = 'user_sessions' AND COLUMN_NAME = 'selected_campus_id'",
  'ALTER TABLE user_sessions DROP COLUMN selected_campus_id');

CALL pems_auth_cleanup_exec_if(
  "SELECT 1 FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE()
     AND TABLE_NAME = 'user_sessions' AND COLUMN_NAME = 'refresh_expires_at'",
  'ALTER TABLE user_sessions DROP COLUMN refresh_expires_at');

CALL pems_auth_cleanup_exec_if(
  "SELECT 1 FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE()
     AND TABLE_NAME = 'user_sessions' AND COLUMN_NAME = 'refresh_revoked_at'",
  'ALTER TABLE user_sessions DROP COLUMN refresh_revoked_at');

CALL pems_auth_cleanup_exec_if(
  "SELECT 1 FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE()
     AND TABLE_NAME = 'otp_tokens' AND COLUMN_NAME = 'token_type'",
  'ALTER TABLE otp_tokens DROP COLUMN token_type');

CALL pems_auth_cleanup_exec_if(
  "SELECT 1 FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE()
     AND TABLE_NAME = 'otp_tokens' AND COLUMN_NAME = 'last_attempt_at'",
  'ALTER TABLE otp_tokens DROP COLUMN last_attempt_at');

CALL pems_auth_cleanup_exec_if(
  "SELECT 1 FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE()
     AND TABLE_NAME = 'otp_tokens' AND COLUMN_NAME = 'human_verified_at'",
  'ALTER TABLE otp_tokens DROP COLUMN human_verified_at');

CALL pems_auth_cleanup_exec_if(
  "SELECT 1 FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE()
     AND TABLE_NAME = 'otp_tokens' AND COLUMN_NAME = 'resend_count'",
  'ALTER TABLE otp_tokens DROP COLUMN resend_count');

CALL pems_auth_cleanup_exec_if(
  "SELECT 1 FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE()
     AND TABLE_NAME = 'login_logs' AND COLUMN_NAME = 'selected_campus_id'",
  'ALTER TABLE login_logs DROP COLUMN selected_campus_id');

CALL pems_auth_cleanup_exec_if(
  "SELECT 1 FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE()
     AND TABLE_NAME = 'login_logs' AND COLUMN_NAME = 'session_id'",
  'ALTER TABLE login_logs DROP COLUMN session_id');

CALL pems_auth_cleanup_exec_if(
  "SELECT 1 FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE()
     AND TABLE_NAME = 'security_events' AND COLUMN_NAME = 'selected_campus_id'",
  'ALTER TABLE security_events DROP COLUMN selected_campus_id');

CALL pems_auth_cleanup_exec_if(
  "SELECT 1 FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE()
     AND TABLE_NAME = 'security_events' AND COLUMN_NAME = 'provider_type'",
  'ALTER TABLE security_events DROP COLUMN provider_type');

CALL pems_auth_cleanup_exec_if(
  "SELECT 1 FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE()
     AND TABLE_NAME = 'security_events' AND COLUMN_NAME = 'session_id'",
  'ALTER TABLE security_events DROP COLUMN session_id');


-- =====================================================================================
-- 5. ALTER COLUMN TYPES  (narrowing ENUMs — section 1 already proved no row is affected)
-- =====================================================================================

CALL pems_auth_cleanup_exec_if(
  "SELECT 1 FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE()
     AND TABLE_NAME = 'user_auth_providers' AND COLUMN_NAME = 'provider_type'
     AND COLUMN_TYPE <> 'enum(''LOCAL_PASSWORD'',''GOOGLE_SSO'')'",
  "ALTER TABLE user_auth_providers
     MODIFY COLUMN provider_type ENUM('LOCAL_PASSWORD','GOOGLE_SSO') NOT NULL");

CALL pems_auth_cleanup_exec_if(
  "SELECT 1 FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE()
     AND TABLE_NAME = 'user_auth_providers' AND COLUMN_NAME = 'provider_subject'
     AND COLUMN_COMMENT <> 'Required for GOOGLE_SSO; NULL for LOCAL_PASSWORD'",
  "ALTER TABLE user_auth_providers
     MODIFY COLUMN provider_subject VARCHAR(255) NULL
     COMMENT 'Required for GOOGLE_SSO; NULL for LOCAL_PASSWORD'");

CALL pems_auth_cleanup_exec_if(
  "SELECT 1 FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE()
     AND TABLE_NAME = 'login_logs' AND COLUMN_NAME = 'provider_type'
     AND COLUMN_TYPE <> 'enum(''LOCAL_PASSWORD'',''GOOGLE_SSO'')'",
  "ALTER TABLE login_logs
     MODIFY COLUMN provider_type ENUM('LOCAL_PASSWORD','GOOGLE_SSO') NULL");

CALL pems_auth_cleanup_exec_if(
  "SELECT 1 FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE()
     AND TABLE_NAME = 'security_events' AND COLUMN_NAME = 'event_type'
     AND COLUMN_TYPE <> 'enum(''SSO_LOGIN'',''SESSION_REVOKED'',''SECURITY_POLICY_CHECK'')'",
  "ALTER TABLE security_events
     MODIFY COLUMN event_type ENUM('SSO_LOGIN','SESSION_REVOKED','SECURITY_POLICY_CHECK') NOT NULL
     COMMENT 'Chỉ giữ các loại sự kiện có producer thật trong hệ thống'");

-- ENUM -> VARCHAR is a WIDENING change: every historical code survives verbatim.
CALL pems_auth_cleanup_exec_if(
  "SELECT 1 FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE()
     AND TABLE_NAME = 'security_events' AND COLUMN_NAME = 'failure_reason_code'
     AND DATA_TYPE <> 'varchar'",
  "ALTER TABLE security_events
     MODIFY COLUMN failure_reason_code VARCHAR(80) NULL
     COMMENT 'Mã lý do thất bại/chặn (machine-readable, mở rộng được); NULL khi SUCCESS'");

CALL pems_auth_cleanup_exec_if(
  "SELECT 1 FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE()
     AND TABLE_NAME = 'security_events' AND COLUMN_NAME = 'severity'
     AND COLUMN_COMMENT NOT LIKE '%SecuritySeverityResolver%'",
  "ALTER TABLE security_events
     MODIFY COLUMN severity ENUM('LOW','MEDIUM','HIGH','CRITICAL') NOT NULL DEFAULT 'LOW'
     COMMENT 'Do backend gán qua SecuritySeverityResolver (event_type + result + failure_reason_code), không phải mặc định LOW'");


-- =====================================================================================
-- 6. RECREATE CHANGED TRIGGERS
-- =====================================================================================

DROP TRIGGER IF EXISTS trg_auth_providers_validate_bi;
DROP TRIGGER IF EXISTS trg_auth_providers_validate_bu;
DROP TRIGGER IF EXISTS trg_sessions_validate_bi;

DELIMITER $$

-- GOOGLE_SSO must carry the subject that identifies the external account.
-- LOCAL_PASSWORD legitimately has provider_subject = NULL.
CREATE TRIGGER trg_auth_providers_validate_bi
BEFORE INSERT ON user_auth_providers
FOR EACH ROW
BEGIN
  IF NEW.provider_type = 'GOOGLE_SSO'
     AND (NEW.provider_subject IS NULL OR TRIM(NEW.provider_subject) = '') THEN
    SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'GOOGLE_SSO provider_subject is required';
  END IF;
END$$

CREATE TRIGGER trg_auth_providers_validate_bu
BEFORE UPDATE ON user_auth_providers
FOR EACH ROW
BEGIN
  IF NEW.provider_type = 'GOOGLE_SSO'
     AND (NEW.provider_subject IS NULL OR TRIM(NEW.provider_subject) = '') THEN
    SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'GOOGLE_SSO provider_subject is required';
  END IF;
END$$

-- Portal/role consistency only. The selected-campus branch is gone with the column: an
-- internal account has exactly one campus (users.primary_campus_id) and the trigger used
-- to do nothing but copy it back onto the session row.
CREATE TRIGGER trg_sessions_validate_bi
BEFORE INSERT ON user_sessions
FOR EACH ROW
BEGIN
  DECLARE v_role_code VARCHAR(30);
  DECLARE v_primary_campus_id BIGINT UNSIGNED;

  SELECT r.role_code, u.primary_campus_id
    INTO v_role_code, v_primary_campus_id
  FROM users u
  JOIN roles r ON r.role_id = u.role_id
  WHERE u.user_id = NEW.user_id;

  IF NEW.login_portal = 'VISITOR' THEN
    IF v_role_code <> 'VISITOR' THEN
      SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Only VISITOR can login via Visitor Portal';
    END IF;
  ELSEIF NEW.login_portal = 'INTERNAL' THEN
    IF v_role_code = 'VISITOR' THEN
      SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'VISITOR cannot login via Internal Portal';
    END IF;
    IF v_primary_campus_id IS NULL THEN
      SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Internal user must have primary_campus_id';
    END IF;
  END IF;
END$$

DELIMITER ;


-- =====================================================================================
-- 7. VERIFY
-- =====================================================================================

DROP PROCEDURE pems_auth_cleanup_exec_if;
SET SESSION sql_mode = @OLD_SQL_MODE;

SELECT '=== VERIFY: dropped columns (expected 0 rows) ===' AS `verify`;
SELECT TABLE_NAME, COLUMN_NAME
FROM information_schema.COLUMNS
WHERE TABLE_SCHEMA = DATABASE()
  AND (   (TABLE_NAME = 'users'               AND COLUMN_NAME IN ('fe_id','email_verified_at','first_login_at'))
       OR (TABLE_NAME = 'user_auth_providers' AND COLUMN_NAME IN ('provider_email','is_enabled','last_used_at'))
       OR (TABLE_NAME = 'user_sessions'       AND COLUMN_NAME IN ('selected_campus_id','refresh_expires_at','refresh_revoked_at'))
       OR (TABLE_NAME = 'otp_tokens'          AND COLUMN_NAME IN ('token_type','last_attempt_at','human_verified_at','resend_count'))
       OR (TABLE_NAME = 'login_logs'          AND COLUMN_NAME IN ('selected_campus_id','session_id'))
       OR (TABLE_NAME = 'security_events'     AND COLUMN_NAME IN ('selected_campus_id','provider_type','session_id')));

SELECT '=== VERIFY: dropped indexes / FKs (expected 0 rows) ===' AS `verify`;
SELECT TABLE_NAME, INDEX_NAME
FROM information_schema.STATISTICS
WHERE TABLE_SCHEMA = DATABASE()
  AND INDEX_NAME IN ('uq_users_fe_id','idx_auth_provider_email','idx_auth_provider_type_email_enabled',
                     'idx_sessions_portal_campus','idx_sessions_refresh_active',
                     'idx_otp_submission','idx_otp_email_purpose_active_v2','idx_otp_issue_limit',
                     'idx_otp_user_purpose_active','idx_login_logs_portal_campus',
                     'idx_security_portal_campus_time','idx_security_session_time')
GROUP BY TABLE_NAME, INDEX_NAME;

SELECT TABLE_NAME, CONSTRAINT_NAME
FROM information_schema.TABLE_CONSTRAINTS
WHERE CONSTRAINT_SCHEMA = DATABASE()
  AND CONSTRAINT_NAME IN ('fk_sessions_selected_campus','fk_login_logs_campus',
                          'fk_security_events_selected_campus','fk_security_events_session');

SELECT '=== VERIFY: column types ===' AS `verify`;
SELECT TABLE_NAME, COLUMN_NAME, COLUMN_TYPE
FROM information_schema.COLUMNS
WHERE TABLE_SCHEMA = DATABASE()
  AND (   (TABLE_NAME = 'user_auth_providers' AND COLUMN_NAME = 'provider_type')
       OR (TABLE_NAME = 'login_logs'          AND COLUMN_NAME = 'provider_type')
       OR (TABLE_NAME = 'security_events'     AND COLUMN_NAME IN ('event_type','failure_reason_code','severity')))
ORDER BY TABLE_NAME, COLUMN_NAME;

SELECT '=== VERIFY: triggers present (expected 3 rows) ===' AS `verify`;
SELECT TRIGGER_NAME, EVENT_MANIPULATION, EVENT_OBJECT_TABLE
FROM information_schema.TRIGGERS
WHERE TRIGGER_SCHEMA = DATABASE()
  AND TRIGGER_NAME IN ('trg_auth_providers_validate_bi','trg_auth_providers_validate_bu','trg_sessions_validate_bi')
ORDER BY TRIGGER_NAME;

SELECT '=== VERIFY: kept columns still present (expected 12 rows) ===' AS `verify`;
SELECT TABLE_NAME, COLUMN_NAME
FROM information_schema.COLUMNS
WHERE TABLE_SCHEMA = DATABASE()
  AND (   (TABLE_NAME = 'users'               AND COLUMN_NAME IN ('password_hash','failed_login_count','locked_until','last_login_at','created_by','updated_by','primary_campus_id'))
       OR (TABLE_NAME = 'user_auth_providers' AND COLUMN_NAME IN ('provider_subject','linked_at'))
       OR (TABLE_NAME = 'user_sessions'       AND COLUMN_NAME IN ('login_portal','revoked_by','revoked_reason')))
ORDER BY TABLE_NAME, COLUMN_NAME;

SELECT 'PEMS AUTH SCHEMA CLEANUP — DONE' AS status;
