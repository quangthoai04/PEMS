-- =====================================================================================
-- Phase I — GUARDED UP (destructive: drops the 10 legacy compatibility columns)
-- =====================================================================================
-- FAIL-CLOSED BY CONSTRUCTION. This script refuses to run unless ALL of the following
-- session variables are set by the runner AFTER 01_preflight.sql returned
-- "PHASE1_PREFLIGHT_RESULT: PASS":
--     SET @ENABLE_PHASE_1_DROP = 1;
--     SET @PHASE1_PREFLIGHT_OK = 1;
-- and the current database is in the disposable allowlist.
--
-- Sourcing this file directly (mysql < 02_guarded_up.sql) raises an error BEFORE any DDL,
-- so it cannot silently mutate a database. MySQL DDL auto-commits and CANNOT be rolled
-- back: every guard must therefore run BEFORE the first ALTER, never inside a transaction.
-- =====================================================================================

-- ── GUARD 1: enable flags + preflight proof + disposable allowlist (aborts before any DDL) ──
SET @guard_ok = IF(
      IFNULL(@ENABLE_PHASE_1_DROP, 0) = 1
  AND IFNULL(@PHASE1_PREFLIGHT_OK, 0) = 1
  AND DATABASE() IN ('pems_i_fresh','pems_i_upgrade','pems_i_refusal','pems_i_rollback'), 1, 0);
SET @guard_sql = IF(@guard_ok = 1, 'DO 0',
  'SELECT `PHASE_I_REFUSED__requires_ENABLE_PHASE_1_DROP_and_PHASE1_PREFLIGHT_OK_on_a_disposable_database`');
PREPARE guard_stmt FROM @guard_sql; EXECUTE guard_stmt; DEALLOCATE PREPARE guard_stmt;

-- ── GUARD 2: resolve the visit_type CHECK by EXPRESSION and prove it is unique ──
-- visit_requests carries SEVEN unnamed CHECK constraints (visit_requests_chk_1..7). Only one
-- references visit_type. Picking one with `LIMIT 1` (as the previous candidate did) drops an
-- unrelated integrity constraint AND leaves the real one in place, so the later DROP COLUMN
-- fails after the index drops have already auto-committed => corrupted, partially-migrated schema.
SET @vt_chk_count = (
  SELECT COUNT(*) FROM information_schema.check_constraints cc
  JOIN information_schema.table_constraints tc
    ON tc.constraint_schema = cc.constraint_schema AND tc.constraint_name = cc.constraint_name
  WHERE cc.constraint_schema = DATABASE() AND tc.table_name = 'visit_requests'
    AND cc.check_clause LIKE '%visit_type%');
SET @vt_chk_name = (
  SELECT tc.constraint_name FROM information_schema.check_constraints cc
  JOIN information_schema.table_constraints tc
    ON tc.constraint_schema = cc.constraint_schema AND tc.constraint_name = cc.constraint_name
  WHERE cc.constraint_schema = DATABASE() AND tc.table_name = 'visit_requests'
    AND cc.check_clause LIKE '%visit_type%'
  ORDER BY tc.constraint_name LIMIT 1);
SET @chk_guard_sql = IF(@vt_chk_count = 1, 'DO 0',
  'SELECT `PHASE_I_REFUSED__expected_exactly_one_CHECK_referencing_visit_type`');
PREPARE chk_guard FROM @chk_guard_sql; EXECUTE chk_guard; DEALLOCATE PREPARE chk_guard;

SELECT CONCAT('Phase I UP: dropping CHECK ', @vt_chk_name) AS step;

-- ── PAYLOAD ──────────────────────────────────────────────────────────────────────────
-- 1. Drop ONLY the verified visit_type CHECK (resolved by expression above).
SET @sql = CONCAT('ALTER TABLE `visit_requests` DROP CHECK `', @vt_chk_name, '`');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

-- 2. Rebuild the FULLTEXT index WITHOUT delegation_name (all other members unchanged).
ALTER TABLE `visit_requests` DROP INDEX `ft_visit_requests_frontend_search`;
ALTER TABLE `visit_requests` ADD FULLTEXT KEY `ft_visit_requests_frontend_search` (
    `request_code`, `registrant_full_name`, `registrant_organization`, `registrant_email`,
    `contact_person_full_name`, `contact_person_organization`, `contact_person_email`
);

-- 3. Drop the secondary indexes that cover legacy columns.
ALTER TABLE `visit_requests` DROP INDEX `idx_visit_requests_visit_type`;
ALTER TABLE `visit_requests` DROP INDEX `idx_visit_requests_media_consent`;

-- 4. Drop the 10 legacy compatibility columns.
ALTER TABLE `visit_requests`
    DROP COLUMN `delegation_name`,
    DROP COLUMN `visit_type`,
    DROP COLUMN `visit_type_other`,
    DROP COLUMN `purpose`,
    DROP COLUMN `working_content`,
    DROP COLUMN `working_language`,
    DROP COLUMN `transportation_note`,
    DROP COLUMN `media_consent_status`,
    DROP COLUMN `media_consent_note`,
    DROP COLUMN `note_to_fptu`;

SELECT 'PHASE1_UP_RESULT: DONE' AS result;
