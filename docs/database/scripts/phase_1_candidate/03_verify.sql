-- =====================================================================================
-- Phase I — READ-ONLY VERIFY (post-UP and post-DOWN). No DDL, no DML.
-- =====================================================================================
-- Mode is selected by the runner:   SET @PHASE1_VERIFY_MODE = 'UP' | 'DOWN';
-- Emits one row per check plus a machine-parseable verdict:
--     PHASE1_VERIFY_RESULT: PASS | FAIL
-- =====================================================================================

SET @db_name = DATABASE();
SET @mode = IFNULL(@PHASE1_VERIFY_MODE, 'UP');
SET @fail = 0;

SELECT CONCAT('Verify mode: ', @mode) AS mode;

-- Common: disposable allowlist.
SELECT IF(@db_name IN ('pems_i_fresh','pems_i_upgrade','pems_i_refusal','pems_i_rollback'),
          'PASS', CONCAT('FAIL: database ', @db_name, ' not in the disposable allowlist')) AS check_db_allowlist;
SET @fail = @fail + IF(@db_name IN ('pems_i_fresh','pems_i_upgrade','pems_i_refusal','pems_i_rollback'), 0, 1);

-- Common: the canonical v2 data must survive BOTH lifecycles untouched.
SET @v2_table = (SELECT COUNT(*) FROM information_schema.tables
                 WHERE table_schema=@db_name AND table_name='visit_instance_form_details');
SELECT IF(@v2_table = 1, 'PASS', 'FAIL: visit_instance_form_details missing') AS check_v2_table_present;
SET @fail = @fail + IF(@v2_table = 1, 0, 1);

SET @detail_rows  = (SELECT COUNT(*) FROM visit_instance_form_details);
SET @instance_rows= (SELECT COUNT(*) FROM visit_request_campuses);
SELECT IF(@detail_rows = @instance_rows, 'PASS',
          CONCAT('FAIL: form details (', @detail_rows, ') != campus instances (', @instance_rows, ')')) AS check_v2_data_intact;
SET @fail = @fail + IF(@detail_rows = @instance_rows, 0, 1);

SET @legacy_present = (SELECT COUNT(*) FROM information_schema.columns
  WHERE table_schema=@db_name AND table_name='visit_requests' AND column_name IN (
    'delegation_name','visit_type','visit_type_other','purpose','working_content',
    'working_language','transportation_note','media_consent_status','media_consent_note','note_to_fptu'));

-- ── UP-mode assertions: the 10 columns and their dependencies are GONE ──────────────
SELECT IF(@mode <> 'UP' OR @legacy_present = 0, 'PASS',
          CONCAT('FAIL: ', @legacy_present, ' legacy columns still present after UP')) AS check_up_columns_dropped;
SET @fail = @fail + IF(@mode <> 'UP' OR @legacy_present = 0, 0, 1);

SET @up_idx = (SELECT COUNT(DISTINCT index_name) FROM information_schema.statistics
               WHERE table_schema=@db_name AND table_name='visit_requests'
                 AND index_name IN ('idx_visit_requests_visit_type','idx_visit_requests_media_consent'));
SELECT IF(@mode <> 'UP' OR @up_idx = 0, 'PASS',
          CONCAT('FAIL: ', @up_idx, ' legacy secondary index(es) still present after UP')) AS check_up_indexes_dropped;
SET @fail = @fail + IF(@mode <> 'UP' OR @up_idx = 0, 0, 1);

SET @up_ft_dn = (SELECT COUNT(*) FROM information_schema.statistics
                 WHERE table_schema=@db_name AND table_name='visit_requests'
                   AND index_name='ft_visit_requests_frontend_search' AND column_name='delegation_name');
SET @up_ft    = (SELECT COUNT(DISTINCT index_name) FROM information_schema.statistics
                 WHERE table_schema=@db_name AND table_name='visit_requests'
                   AND index_name='ft_visit_requests_frontend_search');
SELECT IF(@mode <> 'UP' OR (@up_ft = 1 AND @up_ft_dn = 0), 'PASS',
          'FAIL: after UP the FULLTEXT index must exist and must NOT contain delegation_name') AS check_up_fulltext_rebuilt;
SET @fail = @fail + IF(@mode <> 'UP' OR (@up_ft = 1 AND @up_ft_dn = 0), 0, 1);

SET @up_chk = (SELECT COUNT(*) FROM information_schema.check_constraints cc
               JOIN information_schema.table_constraints tc
                 ON tc.constraint_schema=cc.constraint_schema AND tc.constraint_name=cc.constraint_name
               WHERE cc.constraint_schema=@db_name AND tc.table_name='visit_requests'
                 AND cc.check_clause LIKE '%visit_type%');
SELECT IF(@mode <> 'UP' OR @up_chk = 0, 'PASS',
          'FAIL: the visit_type CHECK still exists after UP') AS check_up_visit_type_check_dropped;
SET @fail = @fail + IF(@mode <> 'UP' OR @up_chk = 0, 0, 1);

-- The unrelated CHECK constraints MUST survive UP (the old LIMIT 1 payload destroyed one of them).
SET @up_other_chk = (SELECT COUNT(*) FROM information_schema.check_constraints cc
                     JOIN information_schema.table_constraints tc
                       ON tc.constraint_schema=cc.constraint_schema AND tc.constraint_name=cc.constraint_name
                     WHERE cc.constraint_schema=@db_name AND tc.table_name='visit_requests'
                       AND cc.check_clause NOT LIKE '%visit_type%');
SELECT IF(@mode <> 'UP' OR @up_other_chk = 6, 'PASS',
          CONCAT('FAIL: expected the 6 unrelated visit_requests CHECKs to survive UP, found ', @up_other_chk)) AS check_up_unrelated_checks_intact;
SET @fail = @fail + IF(@mode <> 'UP' OR @up_other_chk = 6, 0, 1);

-- ── DOWN-mode assertions: exact definitions, exact ordinals, dependencies restored ──
SET @dn_exact = (SELECT COUNT(*) FROM information_schema.columns
  WHERE table_schema=@db_name AND table_name='visit_requests' AND (
       (column_name='delegation_name'      AND column_type='varchar(200)' AND is_nullable='NO')
    OR (column_name='visit_type'           AND column_type="enum('CAMPUS_TOUR','MEETING','WORKSHOP','SIGNING_CEREMONY','EXCHANGE','OTHER')" AND is_nullable='NO' AND column_default='CAMPUS_TOUR')
    OR (column_name='visit_type_other'     AND column_type='varchar(255)' AND is_nullable='YES')
    OR (column_name='purpose'              AND column_type='text'         AND is_nullable='NO')
    OR (column_name='working_content'      AND column_type='text'         AND is_nullable='YES')
    OR (column_name='working_language'     AND column_type="enum('VI','EN')" AND is_nullable='NO' AND column_default='EN')
    OR (column_name='transportation_note'  AND column_type='text'         AND is_nullable='YES')
    OR (column_name='media_consent_status' AND column_type="enum('AGREED','DECLINED')" AND is_nullable='NO' AND column_default='DECLINED')
    OR (column_name='media_consent_note'   AND column_type='text'         AND is_nullable='YES')
    OR (column_name='note_to_fptu'         AND column_type='text'         AND is_nullable='YES')));
SELECT IF(@mode <> 'DOWN' OR @dn_exact = 10, 'PASS',
          CONCAT('FAIL: after DOWN only ', @dn_exact, '/10 legacy columns match the exact master definition')) AS check_down_columns_exact;
SET @fail = @fail + IF(@mode <> 'DOWN' OR @dn_exact = 10, 0, 1);

-- Exact ordinal positions (authoritative order from the master schema).
SET @dn_ord = (SELECT COUNT(*) FROM information_schema.columns c
  JOIN (SELECT 'delegation_name' AS n, 'registrant_nationality' AS prev UNION ALL
        SELECT 'visit_type','visit_scope'                       UNION ALL
        SELECT 'visit_type_other','visit_type'                  UNION ALL
        SELECT 'purpose','visit_type_other'                     UNION ALL
        SELECT 'working_content','purpose'                      UNION ALL
        SELECT 'working_language','contact_person_email'        UNION ALL
        SELECT 'transportation_note','working_language'         UNION ALL
        SELECT 'media_consent_status','transportation_note'     UNION ALL
        SELECT 'media_consent_note','media_consent_status'      UNION ALL
        SELECT 'note_to_fptu','media_consent_note') m ON m.n = c.column_name
  JOIN information_schema.columns p
    ON p.table_schema=c.table_schema AND p.table_name=c.table_name AND p.column_name=m.prev
  WHERE c.table_schema=@db_name AND c.table_name='visit_requests'
    AND c.ordinal_position = p.ordinal_position + 1);
SELECT IF(@mode <> 'DOWN' OR @dn_ord = 10, 'PASS',
          CONCAT('FAIL: after DOWN only ', @dn_ord, '/10 legacy columns sit at their original ordinal position')) AS check_down_ordinals_exact;
SET @fail = @fail + IF(@mode <> 'DOWN' OR @dn_ord = 10, 0, 1);

SET @dn_idx = (SELECT COUNT(DISTINCT index_name) FROM information_schema.statistics
               WHERE table_schema=@db_name AND table_name='visit_requests'
                 AND index_name IN ('idx_visit_requests_visit_type','idx_visit_requests_media_consent'));
SET @dn_ft_dn = (SELECT COUNT(*) FROM information_schema.statistics
                 WHERE table_schema=@db_name AND table_name='visit_requests'
                   AND index_name='ft_visit_requests_frontend_search' AND column_name='delegation_name');
SELECT IF(@mode <> 'DOWN' OR (@dn_idx = 2 AND @dn_ft_dn = 1), 'PASS',
          CONCAT('FAIL: after DOWN indexes not restored (secondary=', @dn_idx, ', ft_has_delegation_name=', @dn_ft_dn, ')')) AS check_down_indexes_restored;
SET @fail = @fail + IF(@mode <> 'DOWN' OR (@dn_idx = 2 AND @dn_ft_dn = 1), 0, 1);

SET @dn_chk = (SELECT COUNT(*) FROM information_schema.check_constraints cc
               JOIN information_schema.table_constraints tc
                 ON tc.constraint_schema=cc.constraint_schema AND tc.constraint_name=cc.constraint_name
               WHERE cc.constraint_schema=@db_name AND tc.table_name='visit_requests'
                 AND cc.check_clause LIKE '%visit_type%');
SELECT IF(@mode <> 'DOWN' OR @dn_chk = 1, 'PASS',
          CONCAT('FAIL: after DOWN expected exactly 1 visit_type CHECK, found ', @dn_chk)) AS check_down_visit_type_check_restored;
SET @fail = @fail + IF(@mode <> 'DOWN' OR @dn_chk = 1, 0, 1);

-- No fabricated placeholder data may have been written by the restore.
SET @dn_fake = (SELECT COUNT(*) FROM information_schema.columns
                WHERE table_schema=@db_name AND table_name='visit_requests' AND column_name='delegation_name');
-- Dynamic SQL is REQUIRED here: after UP the column does not exist, and MySQL parses every
-- branch of a static IF(), so a direct reference would raise "Unknown column" in UP mode.
SET @na_sql = IF(@mode = 'DOWN' AND @dn_fake = 1,
  'SELECT COUNT(*) INTO @dn_na FROM visit_requests WHERE delegation_name = ''N/A'' OR purpose = ''N/A''',
  'SELECT 0 INTO @dn_na');
PREPARE na_stmt FROM @na_sql; EXECUTE na_stmt; DEALLOCATE PREPARE na_stmt;
SELECT IF(@mode <> 'DOWN' OR @dn_na = 0, 'PASS',
          CONCAT('FAIL: ', @dn_na, ' row(s) contain fabricated placeholder values')) AS check_down_no_fabricated_data;
SET @fail = @fail + IF(@mode <> 'DOWN' OR @dn_na = 0, 0, 1);

SELECT CONCAT('PHASE1_VERIFY_RESULT: ', IF(@fail = 0, 'PASS', 'FAIL')) AS verdict;
