-- Preflight Script for Phase I Guarded Drop
SET @db_name = DATABASE();

-- 1. DB allowlist
SELECT IF(@db_name IN ('pems_i_fresh', 'pems_i_upgrade', 'pems_i_refusal', 'pems_i_rollback'), 'PASS', 'FAIL: Database not in allowlist') AS check_db;

-- 2. MySQL Version (>= 8.0.16 for CHECK constraint drops)
SELECT IF(VERSION() >= '8.0.16', 'PASS', 'FAIL: MySQL version must be >= 8.0.16') AS check_version;

-- 3. Exact 10 legacy columns must exist
SELECT IF(COUNT(*) = 10, 'PASS', 'FAIL: Missing legacy columns') AS check_legacy_columns
FROM information_schema.columns
WHERE table_name = 'visit_requests' AND table_schema = @db_name
AND column_name IN (
  'delegation_name', 'visit_type', 'visit_type_other', 'purpose',
  'working_content', 'working_language', 'transportation_note',
  'media_consent_status', 'media_consent_note', 'note_to_fptu'
);

-- 4. Check dependencies (indexes)
SELECT IF(COUNT(*) > 0, 'PASS', 'FAIL: ft_visit_requests_frontend_search missing') AS check_ft_index
FROM information_schema.statistics
WHERE table_name = 'visit_requests' AND table_schema = @db_name AND index_name = 'ft_visit_requests_frontend_search';

SELECT IF(COUNT(*) > 0, 'PASS', 'FAIL: idx_visit_requests_visit_type missing') AS check_idx_visit_type
FROM information_schema.statistics
WHERE table_name = 'visit_requests' AND table_schema = @db_name AND index_name = 'idx_visit_requests_visit_type';

-- 5. Zero request with form_schema_version <> 2
SELECT IF(COUNT(*) = 0, 'PASS', 'FAIL: Found visit_requests with form_schema_version <> 2') AS check_schema_version
FROM visit_requests WHERE form_schema_version <> 2;

-- 6. Each request must have exactly one form detail per campus instance
SELECT IF(COUNT(*) = 0, 'PASS', 'FAIL: Found campus instances without exactly 1 form detail') AS check_details_per_campus
FROM visit_request_campuses vrc
LEFT JOIN visit_instance_form_details fd ON vrc.visit_instance_id = fd.visit_instance_id
WHERE fd.visit_instance_id IS NULL;

-- 7. No orphan form details
SELECT IF(COUNT(*) = 0, 'PASS', 'FAIL: Orphan form details found') AS check_orphan_details
FROM visit_instance_form_details fd
LEFT JOIN visit_request_campuses vrc ON fd.visit_instance_id = vrc.visit_instance_id
WHERE vrc.visit_instance_id IS NULL;

-- 8. Readiness Override 
SELECT IF(@OVERRIDE_RUNTIME_BLOCKERS = 1, 'PASS', 'FAIL: Runtime blockers still exist (V1 fallback active, legacy reads/writes exist). Must set @OVERRIDE_RUNTIME_BLOCKERS=1 to drill.') AS check_readiness;
