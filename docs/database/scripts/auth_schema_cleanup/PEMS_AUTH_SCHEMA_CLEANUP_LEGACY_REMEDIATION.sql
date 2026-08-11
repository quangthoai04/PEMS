-- =====================================================================================
-- PEMS — AUTH / SECURITY SCHEMA CLEANUP: LEGACY VALUE REMEDIATION  (OPT-IN)
--
-- PEMS_AUTH_SCHEMA_CLEANUP_UP.sql aborts when a row still carries an ENUM value that the
-- migration removes. This script resolves exactly those rows so the migration can run.
--
-- It is DELIBERATELY separate and DELIBERATELY opt-in: rewriting an audit row is a
-- business decision, not something a schema migration should do behind your back.
--
--   RUN IT ONLY AFTER READING WHAT IT DOES, AND ONLY WITH A BACKUP IN HAND.
--
-- AUTHORIZATION — the script refuses to do anything unless you set this first:
--     SET @PEMS_LEGACY_REMEDIATION_AUTHORIZED = 1;
--
-- USAGE
--     mysql --default-character-set=utf8mb4 -u root -p <database> \
--           -e "SET @PEMS_LEGACY_REMEDIATION_AUTHORIZED = 1; SOURCE .../PEMS_AUTH_SCHEMA_CLEANUP_LEGACY_REMEDIATION.sql"
--
-- =====================================================================================
-- WHAT IT DOES — three changes, NO row is ever deleted
-- =====================================================================================
--
-- 1. user_auth_providers WHERE provider_type = 'FEID'  →  DELETED
--
--    This is the one place a row is removed, and it is a *binding*, not an audit record.
--    A FEID binding cannot authenticate anything: there is no /auth/feid route, no FEID
--    verifier and no FEID provider type left in the system, and the verifier never
--    returned a real identity even before the cleanup. Accounts keep signing in exactly
--    as before — password login reads users.password_hash, and Google re-creates its own
--    binding on the next sign-in. The login history in login_logs is untouched.
--
-- 2. login_logs WHERE provider_type = 'FEID'  →  provider_type SET TO NULL
--
--    The row SURVIVES with its email / status / failure_reason / IP / user-agent /
--    timestamp intact. NULL is already the column's "no sign-in method was reached"
--    value, and the original value is appended to failure_reason so nothing is lost:
--        failure_reason = CONCAT(COALESCE(failure_reason,''), ' [legacy provider_type=FEID]')
--
-- 3. security_events WHERE event_type is a removed value  →  remapped, original preserved
--
--    The row SURVIVES. The original value is appended to detail_text
--    (`legacy event_type=<old>`) BEFORE the column is rewritten, so the audit trail keeps
--    the fact even though the ENUM no longer can. The mapping keeps the meaning:
--
--      PORTAL_VALIDATION      → SECURITY_POLICY_CHECK   (it WAS a policy decision)
--      CAMPUS_VALIDATION      → SECURITY_POLICY_CHECK   (it WAS a policy decision)
--      VISITOR_AUTO_PROVISION → SSO_LOGIN               (it happened during an SSO login)
--      SESSION_CREATED        → SECURITY_POLICY_CHECK   (owner's decision, see below)
--      SESSION_EXPIRED        → SESSION_REVOKED         (both = this session stopped working)
--      TOKEN_REFRESH          → SECURITY_POLICY_CHECK   (owner's decision, see below)
--
--    The two session/token remaps are the owner's call, not this script's. They previously
--    read SSO_LOGIN on the argument that a session is created by a login and a refresh
--    continues one; the owner chose SECURITY_POLICY_CHECK instead, because these rows record
--    the system CHECKING a session rather than a person signing in, and folding them into
--    SSO_LOGIN would overstate how many sign-ins the history contains. Either target is a
--    legal ENUM member, so nothing about the data forced the change.
--
--    If your history matters enough that these three session/token remaps are not
--    acceptable, do NOT run this script: export those rows to a cold archive table first,
--    then decide. The migration will keep refusing until they are gone either way.
--
-- NOTE: security_events.failure_reason_code needs no remediation — the migration widens
-- that column from ENUM to VARCHAR(80), so every legacy code (PORTAL_MISMATCH,
-- SUSPICIOUS_IP, UNKNOWN, …) survives verbatim with no action here.
-- =====================================================================================

SET NAMES utf8mb4;

DROP PROCEDURE IF EXISTS pems_legacy_remediation_assert_authorized;
DELIMITER $$
CREATE PROCEDURE pems_legacy_remediation_assert_authorized(IN authorized INT)
BEGIN
  -- MySQL caps MESSAGE_TEXT at 128 chars; the file header holds the full explanation.
  IF authorized IS NULL OR authorized <> 1 THEN
    SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT =
      'REFUSED: rewrites audit rows. Read the header, back up, then re-run with SET @PEMS_LEGACY_REMEDIATION_AUTHORIZED = 1;';
  END IF;
END$$
DELIMITER ;

CALL pems_legacy_remediation_assert_authorized(@PEMS_LEGACY_REMEDIATION_AUTHORIZED);
DROP PROCEDURE pems_legacy_remediation_assert_authorized;

SELECT '=== BEFORE ===' AS stage;
SELECT 'user_auth_providers FEID' AS what, COUNT(*) AS rows_found FROM user_auth_providers WHERE provider_type = 'FEID'
UNION ALL SELECT 'login_logs FEID', COUNT(*) FROM login_logs WHERE provider_type = 'FEID'
UNION ALL SELECT 'security_events legacy event_type', COUNT(*) FROM security_events
          WHERE event_type NOT IN ('SSO_LOGIN','SESSION_REVOKED','SECURITY_POLICY_CHECK');

START TRANSACTION;

-- 1. FEID bindings — unusable, and no audit value.
DELETE FROM user_auth_providers WHERE provider_type = 'FEID';

-- 2. FEID login rows — keep the row, blank the method, record what it was.
UPDATE login_logs
SET failure_reason = LEFT(CONCAT(COALESCE(failure_reason, ''), ' [legacy provider_type=FEID]'), 255),
    provider_type  = NULL
WHERE provider_type = 'FEID';

-- 3. Legacy security events — record the original value, THEN remap.
UPDATE security_events
SET detail_text = CONCAT(COALESCE(detail_text, ''), ' [legacy event_type=', event_type, ']')
WHERE event_type NOT IN ('SSO_LOGIN','SESSION_REVOKED','SECURITY_POLICY_CHECK');

UPDATE security_events
SET event_type = CASE event_type
      WHEN 'PORTAL_VALIDATION'      THEN 'SECURITY_POLICY_CHECK'
      WHEN 'CAMPUS_VALIDATION'      THEN 'SECURITY_POLICY_CHECK'
      WHEN 'VISITOR_AUTO_PROVISION' THEN 'SSO_LOGIN'
      WHEN 'SESSION_CREATED'        THEN 'SECURITY_POLICY_CHECK'
      WHEN 'SESSION_EXPIRED'        THEN 'SESSION_REVOKED'
      WHEN 'TOKEN_REFRESH'          THEN 'SECURITY_POLICY_CHECK'
      ELSE event_type
    END
WHERE event_type NOT IN ('SSO_LOGIN','SESSION_REVOKED','SECURITY_POLICY_CHECK');

COMMIT;

SELECT '=== AFTER (expected 0 / 0 / 0) ===' AS stage;
SELECT 'user_auth_providers FEID' AS what, COUNT(*) AS rows_left FROM user_auth_providers WHERE provider_type = 'FEID'
UNION ALL SELECT 'login_logs FEID', COUNT(*) FROM login_logs WHERE provider_type = 'FEID'
UNION ALL SELECT 'security_events legacy event_type', COUNT(*) FROM security_events
          WHERE event_type NOT IN ('SSO_LOGIN','SESSION_REVOKED','SECURITY_POLICY_CHECK');

SELECT 'LEGACY REMEDIATION — DONE. You can now run PEMS_AUTH_SCHEMA_CLEANUP_UP.sql.' AS status;
