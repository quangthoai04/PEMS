-- Down Restore Script
DELIMITER //
CREATE PROCEDURE `ExecutePhase1Restore`()
BEGIN
    DECLARE db_name VARCHAR(255);
    SELECT DATABASE() INTO db_name;
    
    IF db_name NOT IN ('pems_i_fresh', 'pems_i_upgrade', 'pems_i_refusal', 'pems_i_rollback') THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Refused: Not an allowed disposable database.';
    END IF;

    IF @ENABLE_PHASE_1_RESTORE != 1 OR @ENABLE_PHASE_1_RESTORE IS NULL THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Refused: Explicit opt-in required (@ENABLE_PHASE_1_RESTORE = 1)';
    END IF;

    -- 1. Add columns back
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

    SELECT 'Phase I Restore Completed' as result;
END//

DELIMITER ;

CALL ExecutePhase1Restore();
DROP PROCEDURE ExecutePhase1Restore;
