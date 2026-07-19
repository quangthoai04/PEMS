-- Verify Script
SET @db_name = DATABASE();

SELECT count(*) AS remaining_legacy_columns
FROM information_schema.columns 
WHERE table_name = 'visit_requests' AND table_schema = @db_name
AND column_name IN (
  'delegation_name', 'visit_type', 'visit_type_other', 'purpose', 
  'working_content', 'working_language', 'transportation_note', 
  'media_consent_status', 'media_consent_note', 'note_to_fptu'
);

SELECT IF(count(*) = 0, 'PASS', 'FAIL') as verification_status
FROM information_schema.columns 
WHERE table_name = 'visit_requests' AND table_schema = @db_name
AND column_name IN (
  'delegation_name', 'visit_type', 'visit_type_other', 'purpose', 
  'working_content', 'working_language', 'transportation_note', 
  'media_consent_status', 'media_consent_note', 'note_to_fptu'
);
