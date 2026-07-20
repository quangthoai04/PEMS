-- Down Restore Script for Phase I
-- This script contains ONLY the DDL payload.
-- It MUST be executed via run_migration.ps1 which ensures zero-mutation refusal before execution.
-- DO NOT RUN THIS SCRIPT DIRECTLY ON PRODUCTION!

-- 1. Add columns back as NULLable first for backfilling
ALTER TABLE `visit_requests`
    ADD COLUMN `delegation_name` VARCHAR(200) NULL COMMENT 'Tên đoàn khách (compatibility projection; v2 dùng visit_instance_form_details.delegation_name theo campus)',
    ADD COLUMN `visit_type` ENUM('CAMPUS_TOUR','MEETING','WORKSHOP','SIGNING_CEREMONY','EXCHANGE','OTHER') NULL COMMENT 'Compatibility projection; v2 dùng visit_instance_form_details.visit_type theo campus',
    ADD COLUMN `visit_type_other` VARCHAR(255) NULL,
    ADD COLUMN `purpose` TEXT NULL COMMENT 'Mục đích thăm FPTU (compatibility projection; v2 dùng visit_instance_form_details.purpose theo campus)',
    ADD COLUMN `working_content` TEXT NULL COMMENT 'Nội dung làm việc (compatibility projection; v2 dùng visit_instance_form_details.working_content theo campus)',
    ADD COLUMN `working_language` ENUM('VI','EN') NULL COMMENT 'Ngôn ngữ sử dụng trong visit. Chỉ dùng VI/EN theo frontend hiện tại, không có lựa chọn OTHER',
    ADD COLUMN `transportation_note` TEXT NULL COMMENT 'Nhận diện phương tiện di chuyển tới FPTU do khách nhập tự do',
    ADD COLUMN `media_consent_status` ENUM('AGREED','DECLINED') NULL,
    ADD COLUMN `media_consent_note` TEXT NULL,
    ADD COLUMN `note_to_fptu` TEXT NULL COMMENT 'Ghi chú cho FPTU';

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

-- 3. Modify columns to NOT NULL and add defaults based on master V11 schema
-- For any row with schema_version=1 that might have nulls (should not exist, but to be safe we coalesce)
UPDATE visit_requests SET delegation_name = 'N/A' WHERE delegation_name IS NULL;
UPDATE visit_requests SET visit_type = 'CAMPUS_TOUR' WHERE visit_type IS NULL;
UPDATE visit_requests SET purpose = 'N/A' WHERE purpose IS NULL;
UPDATE visit_requests SET working_language = 'EN' WHERE working_language IS NULL;
UPDATE visit_requests SET media_consent_status = 'DECLINED' WHERE media_consent_status IS NULL;

ALTER TABLE `visit_requests`
    MODIFY COLUMN `delegation_name` VARCHAR(200) NOT NULL COMMENT 'Tên đoàn khách (compatibility projection; v2 dùng visit_instance_form_details.delegation_name theo campus)',
    MODIFY COLUMN `visit_type` ENUM('CAMPUS_TOUR','MEETING','WORKSHOP','SIGNING_CEREMONY','EXCHANGE','OTHER') NOT NULL DEFAULT 'CAMPUS_TOUR' COMMENT 'Compatibility projection; v2 dùng visit_instance_form_details.visit_type theo campus',
    MODIFY COLUMN `purpose` TEXT NOT NULL COMMENT 'Mục đích thăm FPTU (compatibility projection; v2 dùng visit_instance_form_details.purpose theo campus)',
    MODIFY COLUMN `working_language` ENUM('VI','EN') NOT NULL DEFAULT 'EN' COMMENT 'Ngôn ngữ sử dụng trong visit. Chỉ dùng VI/EN theo frontend hiện tại, không có lựa chọn OTHER',
    MODIFY COLUMN `media_consent_status` ENUM('AGREED','DECLINED') NOT NULL DEFAULT 'DECLINED';

-- 4. Re-add indexes and CHECK constraint
ALTER TABLE `visit_requests` 
    ADD KEY `idx_visit_requests_visit_type` (`visit_type`),
    ADD KEY `idx_visit_requests_media_consent` (`media_consent_status`),
    ADD CHECK (visit_type <> 'OTHER' OR (visit_type_other IS NOT NULL AND TRIM(visit_type_other) <> ''));

-- 5. Re-create FULLTEXT index to include delegation_name
ALTER TABLE `visit_requests` DROP INDEX `ft_visit_requests_frontend_search`;
ALTER TABLE `visit_requests` ADD FULLTEXT KEY `ft_visit_requests_frontend_search` (
    `request_code`, `delegation_name`, `registrant_full_name`, `registrant_organization`, `registrant_email`, 
    `contact_person_full_name`, `contact_person_organization`, `contact_person_email`
);

SELECT 'Phase I Restore Completed' as result;
