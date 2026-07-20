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
-- Mode is mandatory and must be exactly UP or DOWN. UP proves the pre-UP state; DOWN proves the
-- post-UP state AND that every value needed for the restore exists BEFORE the first ALTER.
SET @mode = IFNULL(@PHASE1_PREFLIGHT_MODE, 'UP');
SELECT IF(@mode IN ('UP','DOWN'), CONCAT('PASS (mode ', @mode, ')'),
          CONCAT('FAIL: invalid mode ', @mode)) AS check_mode;
SET @fail = @fail + IF(@mode IN ('UP','DOWN'), 0, 1);

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
SELECT IF(@mode <> 'UP' OR @cols_ok = 10, 'PASS',
          CONCAT('FAIL: expected 10 legacy columns with exact definitions, matched ', @cols_ok)) AS check_legacy_columns_exact;
SET @fail = @fail + IF(@mode <> 'UP' OR @cols_ok = 10, 0, 1);

-- 4. Every dependent index Phase I touches must exist (all three, not just two).
SET @ft_ok = (SELECT COUNT(DISTINCT index_name) FROM information_schema.statistics
              WHERE table_schema=@db_name AND table_name='visit_requests' AND index_name='ft_visit_requests_frontend_search');
SET @ix_vt = (SELECT COUNT(DISTINCT index_name) FROM information_schema.statistics
              WHERE table_schema=@db_name AND table_name='visit_requests' AND index_name='idx_visit_requests_visit_type');
SET @ix_mc = (SELECT COUNT(DISTINCT index_name) FROM information_schema.statistics
              WHERE table_schema=@db_name AND table_name='visit_requests' AND index_name='idx_visit_requests_media_consent');
SELECT IF(@mode <> 'UP' OR (@ft_ok=1 AND @ix_vt=1 AND @ix_mc=1), 'PASS',
          CONCAT('FAIL: dependent indexes missing (ft=',@ft_ok,' visit_type=',@ix_vt,' media_consent=',@ix_mc,')')) AS check_dependent_indexes;
SET @fail = @fail + IF(@mode <> 'UP' OR (@ft_ok=1 AND @ix_vt=1 AND @ix_mc=1), 0, 1);

-- 5. The FULLTEXT index must currently INCLUDE delegation_name (UP rebuilds it without that column).
SET @ft_has_dn = (SELECT COUNT(*) FROM information_schema.statistics
                  WHERE table_schema=@db_name AND table_name='visit_requests'
                    AND index_name='ft_visit_requests_frontend_search' AND column_name='delegation_name');
SELECT IF(@mode <> 'UP' OR @ft_has_dn = 1, 'PASS', 'FAIL: ft_visit_requests_frontend_search does not include delegation_name') AS check_ft_shape;
SET @fail = @fail + IF(@mode <> 'UP' OR @ft_has_dn = 1, 0, 1);

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
SELECT IF(@mode <> 'UP' OR @vt_chk_count = 1, CONCAT('PASS (', IFNULL(@vt_chk_name, 'n/a in DOWN mode'), ')'),
          CONCAT('FAIL: expected exactly 1 CHECK referencing visit_type, found ', @vt_chk_count)) AS check_visit_type_constraint;
SET @fail = @fail + IF(@mode <> 'UP' OR @vt_chk_count = 1, 0, 1);

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
SELECT IF(@mode <> 'UP' OR @other_chk = 0, 'PASS',
          CONCAT('FAIL: ', @other_chk, ' additional CHECK(s) reference legacy columns')) AS check_no_other_legacy_checks;
SET @fail = @fail + IF(@mode <> 'UP' OR @other_chk = 0, 0, 1);

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

-- 11a. Every request must have a deterministic source detail (campus_id ASC, then instance id).
SET @unprojectable = (SELECT COUNT(*) FROM visit_requests vr
                      WHERE NOT EXISTS (SELECT 1 FROM visit_request_campuses vrc
                                        JOIN visit_instance_form_details fd ON fd.visit_instance_id = vrc.visit_instance_id
                                        WHERE vrc.visit_request_id = vr.visit_request_id));
SELECT IF(@unprojectable = 0, 'PASS', CONCAT('FAIL: ', @unprojectable, ' requests have no deterministic source detail')) AS check_source_detail_exists;
SET @fail = @fail + IF(@unprojectable = 0, 0, 1);

-- 11b. REAL projection parity (UP only): compare ALL TEN stored legacy values against the
--      deterministic compatibility projection using NULL-safe equality (<=>). No COALESCE/TRIM,
--      because normalising would hide exactly the drift this gate exists to catch.
--      A request whose legacy columns disagree with its source detail cannot be restored
--      losslessly by DOWN, so any mismatch must refuse UP. Dynamic SQL: after UP the columns
--      do not exist and a static reference would not parse in DOWN mode.
SET @parity_sql = IF(@mode = 'UP',
  'SELECT COUNT(*) INTO @parity_mismatch FROM visit_requests vr JOIN (
       SELECT vrc.visit_request_id AS rid, fd.*,
              ROW_NUMBER() OVER (PARTITION BY vrc.visit_request_id
                                 ORDER BY vrc.campus_id ASC, vrc.visit_instance_id ASC) AS rn
       FROM visit_request_campuses vrc
       JOIN visit_instance_form_details fd ON fd.visit_instance_id = vrc.visit_instance_id
   ) s ON s.rid = vr.visit_request_id AND s.rn = 1
   WHERE NOT ( vr.delegation_name      <=> s.delegation_name
           AND vr.visit_type           <=> s.visit_type
           AND vr.visit_type_other     <=> s.visit_type_other
           AND vr.purpose              <=> s.purpose
           AND vr.working_content      <=> s.working_content
           AND vr.working_language     <=> s.working_language
           AND vr.transportation_note  <=> s.transportation_note
           AND vr.media_consent_status <=> s.media_consent_status
           AND vr.media_consent_note   <=> s.media_consent_note
           AND vr.note_to_fptu         <=> s.note_to_fptu )',
  'SELECT 0 INTO @parity_mismatch');
PREPARE parity_stmt FROM @parity_sql; EXECUTE parity_stmt; DEALLOCATE PREPARE parity_stmt;
SELECT IF(@mode <> 'UP' OR @parity_mismatch = 0, 'PASS',
          CONCAT('FAIL: ', @parity_mismatch, ' request(s) whose legacy columns differ from the deterministic projection')) AS check_projection_parity_10_fields;
SET @fail = @fail + IF(@mode <> 'UP' OR @parity_mismatch = 0, 0, 1);

-- 11c. DOWN-mode restorability, proven BEFORE the first ALTER (the restore itself must never
--      mutate schema and then discover it cannot finish).
SET @dn_cols_present = (SELECT COUNT(*) FROM information_schema.columns
  WHERE table_schema=@db_name AND table_name='visit_requests' AND column_name IN (
    'delegation_name','visit_type','visit_type_other','purpose','working_content',
    'working_language','transportation_note','media_consent_status','media_consent_note','note_to_fptu'));
SELECT IF(@mode <> 'DOWN' OR @dn_cols_present = 0, 'PASS',
          CONCAT('FAIL: DOWN requires the post-UP state but ', @dn_cols_present, ' legacy column(s) still exist')) AS check_down_state_is_post_up;
SET @fail = @fail + IF(@mode <> 'DOWN' OR @dn_cols_present = 0, 0, 1);

-- Mandatory source values must be non-null, otherwise DOWN could not satisfy NOT NULL without
-- fabricating data (which is forbidden).
SET @dn_null_src = (SELECT COUNT(*) FROM visit_requests vr
  JOIN (SELECT vrc.visit_request_id AS rid, fd.delegation_name, fd.visit_type, fd.purpose,
               fd.working_language, fd.media_consent_status,
               ROW_NUMBER() OVER (PARTITION BY vrc.visit_request_id
                                  ORDER BY vrc.campus_id ASC, vrc.visit_instance_id ASC) AS rn
        FROM visit_request_campuses vrc
        JOIN visit_instance_form_details fd ON fd.visit_instance_id = vrc.visit_instance_id) s
    ON s.rid = vr.visit_request_id AND s.rn = 1
  WHERE s.delegation_name IS NULL OR s.visit_type IS NULL OR s.purpose IS NULL
     OR s.working_language IS NULL OR s.media_consent_status IS NULL);
SELECT IF(@mode <> 'DOWN' OR @dn_null_src = 0, 'PASS',
          CONCAT('FAIL: ', @dn_null_src, ' request(s) have NULL mandatory source values; restore would have to fabricate data')) AS check_down_mandatory_sources;
SET @fail = @fail + IF(@mode <> 'DOWN' OR @dn_null_src = 0, 0, 1);

-- 12. Runtime blockers. Real readiness must come from the verified code audit artifact
--     (PHASE_I_AUDIT_REPORT.md). @OVERRIDE_RUNTIME_BLOCKERS is a DISPOSABLE-DRILL-ONLY
--     acknowledgement; it is never evidence that production is ready.
SELECT IF(IFNULL(@OVERRIDE_RUNTIME_BLOCKERS,0) = 1, 'PASS (drill override acknowledged)',
          'FAIL: runtime V1 dependencies still exist; set @OVERRIDE_RUNTIME_BLOCKERS=1 for a DISPOSABLE drill only') AS check_runtime_blockers;
SET @fail = @fail + IF(IFNULL(@OVERRIDE_RUNTIME_BLOCKERS,0) = 1, 0, 1);

-- FINAL machine-parseable verdict (the runner gates on this token AND the exit code).
SELECT CONCAT('PHASE1_PREFLIGHT_RESULT: ', IF(@fail = 0, 'PASS', 'FAIL')) AS verdict;
