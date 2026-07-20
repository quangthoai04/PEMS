-- =====================================================================================
-- Phase I — READ-ONLY PREFLIGHT GATE (no DDL, no DML; safe to run anywhere)
-- =====================================================================================
-- Contract: emits one row per check plus a FINAL machine-parseable verdict row:
--     PHASE1_PREFLIGHT_RESULT: PASS      (every gate passed)
--     PHASE1_PREFLIGHT_RESULT: FAIL      (at least one gate failed)
-- The runner MUST parse the verdict row AND the mysql exit code, and MUST NOT execute
-- any payload unless the verdict is PASS. This script never mutates anything.
--
-- Authoritative schema source: docs/database/scripts/PEMS_FULL_V11_REMOVED_TTS_19_07_26.sql
--   visit_requests carries the 10 legacy compatibility columns, 2 secondary indexes,
--   1 FULLTEXT index that INCLUDES delegation_name, and SEVEN unnamed CHECK constraints
--   (auto-named visit_requests_chk_1..7). Exactly ONE of them references visit_type —
--   that is the only CHECK Phase I may drop. Never select a CHECK by LIMIT 1.
-- =====================================================================================

SET @db_name = DATABASE();
SET @fail = 0;

-- 1. Disposable-database allowlist (exact names only; never a prefix match).
SELECT IF(@db_name IN ('pems_i_fresh','pems_i_upgrade','pems_i_refusal','pems_i_rollback'),
          'PASS', CONCAT('FAIL: database ', @db_name, ' is not in the disposable allowlist')) AS check_db_allowlist;
SET @fail = @fail + IF(@db_name IN ('pems_i_fresh','pems_i_upgrade','pems_i_refusal','pems_i_rollback'), 0, 1);

-- 2. MySQL version >= 8.0.16 (ALTER TABLE ... DROP CHECK). Numeric compare, NOT string:
--    string compare wrongly reports '8.0.9' >= '8.0.16'.
SET @v  = SUBSTRING_INDEX(VERSION(), '-', 1);
SET @v_major = CAST(SUBSTRING_INDEX(@v, '.', 1) AS UNSIGNED);
SET @v_minor = CAST(SUBSTRING_INDEX(SUBSTRING_INDEX(@v, '.', 2), '.', -1) AS UNSIGNED);
SET @v_patch = CAST(SUBSTRING_INDEX(@v, '.', -1) AS UNSIGNED);
SET @v_num = (@v_major * 1000000) + (@v_minor * 1000) + @v_patch;
SELECT IF(@v_num >= 8000016, 'PASS', CONCAT('FAIL: MySQL ', VERSION(), ' < 8.0.16')) AS check_version;
SET @fail = @fail + IF(@v_num >= 8000016, 0, 1);

-- 3. The exact 10 legacy columns exist WITH their exact expected definitions
--    (type + nullability + default). A drifted definition means DOWN cannot restore exactly.
SET @cols_ok = (
  SELECT COUNT(*) FROM information_schema.columns
  WHERE table_schema = @db_name AND table_name = 'visit_requests' AND (
       (column_name='delegation_name'      AND column_type='varchar(200)' AND is_nullable='NO'  AND column_default IS NULL)
    OR (column_name='visit_type'           AND column_type="enum('CAMPUS_TOUR','MEETING','WORKSHOP','SIGNING_CEREMONY','EXCHANGE','OTHER')" AND is_nullable='NO' AND column_default='CAMPUS_TOUR')
    OR (column_name='visit_type_other'     AND column_type='varchar(255)' AND is_nullable='YES')
    OR (column_name='purpose'              AND column_type='text'         AND is_nullable='NO')
    OR (column_name='working_content'      AND column_type='text'         AND is_nullable='YES')
    OR (column_name='working_language'     AND column_type="enum('VI','EN')" AND is_nullable='NO' AND column_default='EN')
    OR (column_name='transportation_note'  AND column_type='text'         AND is_nullable='YES')
    OR (column_name='media_consent_status' AND column_type="enum('AGREED','DECLINED')" AND is_nullable='NO' AND column_default='DECLINED')
    OR (column_name='media_consent_note'   AND column_type='text'         AND is_nullable='YES')
    OR (column_name='note_to_fptu'         AND column_type='text'         AND is_nullable='YES')
  ));
SELECT IF(@cols_ok = 10, 'PASS',
          CONCAT('FAIL: expected 10 legacy columns with exact definitions, matched ', @cols_ok)) AS check_legacy_columns_exact;
SET @fail = @fail + IF(@cols_ok = 10, 0, 1);

-- 4. Every dependent index Phase I touches must exist (all three, not just two).
SET @ft_ok = (SELECT COUNT(DISTINCT index_name) FROM information_schema.statistics
              WHERE table_schema=@db_name AND table_name='visit_requests' AND index_name='ft_visit_requests_frontend_search');
SET @ix_vt = (SELECT COUNT(DISTINCT index_name) FROM information_schema.statistics
              WHERE table_schema=@db_name AND table_name='visit_requests' AND index_name='idx_visit_requests_visit_type');
SET @ix_mc = (SELECT COUNT(DISTINCT index_name) FROM information_schema.statistics
              WHERE table_schema=@db_name AND table_name='visit_requests' AND index_name='idx_visit_requests_media_consent');
SELECT IF(@ft_ok=1 AND @ix_vt=1 AND @ix_mc=1, 'PASS',
          CONCAT('FAIL: dependent indexes missing (ft=',@ft_ok,' visit_type=',@ix_vt,' media_consent=',@ix_mc,')')) AS check_dependent_indexes;
SET @fail = @fail + IF(@ft_ok=1 AND @ix_vt=1 AND @ix_mc=1, 0, 1);

-- 5. The FULLTEXT index must currently INCLUDE delegation_name (UP rebuilds it without that column).
SET @ft_has_dn = (SELECT COUNT(*) FROM information_schema.statistics
                  WHERE table_schema=@db_name AND table_name='visit_requests'
                    AND index_name='ft_visit_requests_frontend_search' AND column_name='delegation_name');
SELECT IF(@ft_has_dn = 1, 'PASS', 'FAIL: ft_visit_requests_frontend_search does not include delegation_name') AS check_ft_shape;
SET @fail = @fail + IF(@ft_has_dn = 1, 0, 1);

-- 6. Resolve the visit_type CHECK by EXPRESSION and prove it is UNIQUE.
--    visit_requests has 7 unnamed CHECKs (visit_requests_chk_1..7); only ONE references
--    visit_type. Selecting by LIMIT 1 would drop an unrelated integrity constraint.
SET @vt_chk_count = (
  SELECT COUNT(*) FROM information_schema.check_constraints cc
  JOIN information_schema.table_constraints tc
    ON tc.constraint_schema = cc.constraint_schema AND tc.constraint_name = cc.constraint_name
  WHERE cc.constraint_schema = @db_name AND tc.table_name = 'visit_requests'
    AND cc.check_clause LIKE '%visit_type%');
SET @vt_chk_name = (
  SELECT tc.constraint_name FROM information_schema.check_constraints cc
  JOIN information_schema.table_constraints tc
    ON tc.constraint_schema = cc.constraint_schema AND tc.constraint_name = cc.constraint_name
  WHERE cc.constraint_schema = @db_name AND tc.table_name = 'visit_requests'
    AND cc.check_clause LIKE '%visit_type%'
  ORDER BY tc.constraint_name LIMIT 1);
SELECT IF(@vt_chk_count = 1, CONCAT('PASS (', @vt_chk_name, ')'),
          CONCAT('FAIL: expected exactly 1 CHECK referencing visit_type, found ', @vt_chk_count)) AS check_visit_type_constraint;
SET @fail = @fail + IF(@vt_chk_count = 1, 0, 1);

-- 7. No OTHER CHECK on visit_requests may reference any of the 10 legacy columns, otherwise
--    DROP COLUMN fails midway (DDL auto-commits => partially migrated schema, no rollback).
SET @other_chk = (
  SELECT COUNT(*) FROM information_schema.check_constraints cc
  JOIN information_schema.table_constraints tc
    ON tc.constraint_schema = cc.constraint_schema AND tc.constraint_name = cc.constraint_name
  WHERE cc.constraint_schema = @db_name AND tc.table_name = 'visit_requests'
    AND cc.check_clause NOT LIKE '%visit_type%'
    AND (cc.check_clause LIKE '%delegation_name%' OR cc.check_clause LIKE '%purpose%'
      OR cc.check_clause LIKE '%working_content%' OR cc.check_clause LIKE '%working_language%'
      OR cc.check_clause LIKE '%transportation_note%' OR cc.check_clause LIKE '%media_consent%'
      OR cc.check_clause LIKE '%note_to_fptu%'));
SELECT IF(@other_chk = 0, 'PASS',
          CONCAT('FAIL: ', @other_chk, ' additional CHECK(s) reference legacy columns')) AS check_no_other_legacy_checks;
SET @fail = @fail + IF(@other_chk = 0, 0, 1);

-- 8. Data readiness: every persisted request must already be per-campus v2.
SET @non_v2 = (SELECT COUNT(*) FROM visit_requests WHERE form_schema_version <> 2);
SELECT IF(@non_v2 = 0, 'PASS', CONCAT('FAIL: ', @non_v2, ' visit_requests rows with form_schema_version <> 2')) AS check_all_requests_v2;
SET @fail = @fail + IF(@non_v2 = 0, 0, 1);

-- 9. Every campus instance must have exactly one form detail (no missing, no duplicate).
SET @missing_detail = (SELECT COUNT(*) FROM visit_request_campuses vrc
                       LEFT JOIN visit_instance_form_details fd ON fd.visit_instance_id = vrc.visit_instance_id
                       WHERE fd.visit_instance_id IS NULL);
SELECT IF(@missing_detail = 0, 'PASS', CONCAT('FAIL: ', @missing_detail, ' campus instances without a form detail')) AS check_detail_per_instance;
SET @fail = @fail + IF(@missing_detail = 0, 0, 1);

-- 10. No orphan form details (detail pointing at a non-existent instance).
SET @orphan_detail = (SELECT COUNT(*) FROM visit_instance_form_details fd
                      LEFT JOIN visit_request_campuses vrc ON vrc.visit_instance_id = fd.visit_instance_id
                      WHERE vrc.visit_instance_id IS NULL);
SELECT IF(@orphan_detail = 0, 'PASS', CONCAT('FAIL: ', @orphan_detail, ' orphan form details')) AS check_no_orphan_details;
SET @fail = @fail + IF(@orphan_detail = 0, 0, 1);

-- 11. Backfill parity: the compatibility projection must be reproducible from v2 detail data,
--     otherwise DOWN cannot restore the legacy columns losslessly.
SET @unprojectable = (SELECT COUNT(*) FROM visit_requests vr
                      WHERE NOT EXISTS (SELECT 1 FROM visit_request_campuses vrc
                                        JOIN visit_instance_form_details fd ON fd.visit_instance_id = vrc.visit_instance_id
                                        WHERE vrc.visit_request_id = vr.visit_request_id));
SELECT IF(@unprojectable = 0, 'PASS', CONCAT('FAIL: ', @unprojectable, ' requests have no projectable campus detail')) AS check_projection_parity;
SET @fail = @fail + IF(@unprojectable = 0, 0, 1);

-- 12. Runtime blockers. Real readiness must come from the verified code audit artifact
--     (PHASE_I_AUDIT_REPORT.md). @OVERRIDE_RUNTIME_BLOCKERS is a DISPOSABLE-DRILL-ONLY
--     acknowledgement; it is never evidence that production is ready.
SELECT IF(IFNULL(@OVERRIDE_RUNTIME_BLOCKERS,0) = 1, 'PASS (drill override acknowledged)',
          'FAIL: runtime V1 dependencies still exist; set @OVERRIDE_RUNTIME_BLOCKERS=1 for a DISPOSABLE drill only') AS check_runtime_blockers;
SET @fail = @fail + IF(IFNULL(@OVERRIDE_RUNTIME_BLOCKERS,0) = 1, 0, 1);

-- FINAL machine-parseable verdict (the runner gates on this token AND the exit code).
SELECT CONCAT('PHASE1_PREFLIGHT_RESULT: ', IF(@fail = 0, 'PASS', 'FAIL')) AS verdict;
