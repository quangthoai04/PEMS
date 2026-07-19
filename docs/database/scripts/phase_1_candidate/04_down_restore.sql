-- Down Restore Script
-- 1. Add columns back
ALTER TABLE `visit_requests`
    ADD COLUMN `delegation_name` varchar(255) NULL,
    ADD COLUMN `visit_type` varchar(50) NULL,
    ADD COLUMN `visit_type_other` varchar(255) NULL,
    ADD COLUMN `purpose` text NULL,
    ADD COLUMN `working_content` text NULL,
    ADD COLUMN `working_language` varchar(100) NULL,
    ADD COLUMN `transportation_note` text NULL,
    ADD COLUMN `media_consent_status` varchar(20) NULL,
    ADD COLUMN `media_consent_note` text NULL,
    ADD COLUMN `note_to_fptu` text NULL;

-- 2. Backfill from compatibility projection (smallest campus_id)
UPDATE visit_requests vr
JOIN (
    SELECT vrc.visit_request_id,
           fd.delegation_name, fd.visit_type, fd.visit_type_other, fd.purpose,
           fd.working_content, fd.working_language, fd.transportation_note,
           fd.media_consent_status, fd.media_consent_note, fd.note_to_fptu,
           ROW_NUMBER() OVER (PARTITION BY vrc.visit_request_id ORDER BY vrc.campus_id ASC) as rn
    FROM visit_request_campuses vrc
    JOIN visit_instance_form_details fd ON vrc.visit_instance_id = fd.visit_instance_id
) sub ON vr.visit_request_id = sub.visit_request_id AND sub.rn = 1
SET vr.delegation_name = sub.delegation_name,
    vr.visit_type = sub.visit_type,
    vr.visit_type_other = sub.visit_type_other,
    vr.purpose = sub.purpose,
    vr.working_content = sub.working_content,
    vr.working_language = sub.working_language,
    vr.transportation_note = sub.transportation_note,
    vr.media_consent_status = sub.media_consent_status,
    vr.media_consent_note = sub.media_consent_note,
    vr.note_to_fptu = sub.note_to_fptu
WHERE vr.form_schema_version >= 2;
