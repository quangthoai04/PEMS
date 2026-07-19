-- Preflight Script for Phase I Guarded Drop
-- 1. Check if DB is disposable (starts with pems_i_)
SET @db_name = DATABASE();
SELECT IF(@db_name IN ('pems_i_fresh', 'pems_i_upgrade', 'pems_i_refusal', 'pems_i_rollback'), 'PASS', 'FAIL') AS disposable_db_check;

-- 2. Verify existence of the 10 legacy columns
SELECT count(*) AS legacy_columns_count 
FROM information_schema.columns 
WHERE table_name = 'visit_requests' AND table_schema = @db_name
AND column_name IN (
  'delegation_name', 'visit_type', 'visit_type_other', 'purpose', 
  'working_content', 'working_language', 'transportation_note', 
  'media_consent_status', 'media_consent_note', 'note_to_fptu'
);

-- 3. Check for any orphan members (just as a sanity check)
SELECT count(*) AS orphan_members
FROM visit_instance_guest_members m
LEFT JOIN visit_requests r ON m.visit_request_id = r.visit_request_id
WHERE r.visit_request_id IS NULL;
