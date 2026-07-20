-- Verify Script for Phase I
SET @db_name = DATABASE();

SELECT IF(@db_name IN ('pems_i_fresh', 'pems_i_upgrade', 'pems_i_refusal', 'pems_i_rollback'), 'PASS', 'FAIL: Database not in allowlist') AS check_db;

-- 1. Exact 10 legacy columns must NOT exist
SELECT IF(COUNT(*) = 0, 'PASS', 'FAIL: Legacy columns still exist') AS check_legacy_columns_dropped
FROM information_schema.columns
WHERE table_name = 'visit_requests' AND table_schema = @db_name
AND column_name IN (
  'delegation_name', 'visit_type', 'visit_type_other', 'purpose',
  'working_content', 'working_language', 'transportation_note',
  'media_consent_status', 'media_consent_note', 'note_to_fptu'
);

-- 2. Dependencies removed
SELECT IF(COUNT(*) = 0, 'PASS', 'FAIL: idx_visit_requests_visit_type still exists') AS check_idx_visit_type_dropped
FROM information_schema.statistics
WHERE table_name = 'visit_requests' AND table_schema = @db_name AND index_name = 'idx_visit_requests_visit_type';

-- 3. Check FULLTEXT is present but does NOT contain delegation_name
SELECT IF(COUNT(*) > 0, 'PASS', 'FAIL: ft_visit_requests_frontend_search missing') AS check_ft_index_exists
FROM information_schema.statistics
WHERE table_name = 'visit_requests' AND table_schema = @db_name AND index_name = 'ft_visit_requests_frontend_search';

SELECT IF(COUNT(*) = 0, 'PASS', 'FAIL: ft_visit_requests_frontend_search still contains delegation_name') AS check_ft_columns
FROM information_schema.statistics
WHERE table_name = 'visit_requests' AND table_schema = @db_name AND index_name = 'ft_visit_requests_frontend_search' AND column_name = 'delegation_name';

-- 4. V2 tables intact
SELECT IF(COUNT(*) > 0, 'PASS', 'FAIL: visit_instance_form_details missing') AS check_v2_table
FROM information_schema.tables
WHERE table_name = 'visit_instance_form_details' AND table_schema = @db_name;
