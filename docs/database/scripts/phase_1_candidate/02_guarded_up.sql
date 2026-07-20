-- Guarded UP Script for Phase I
-- This script contains ONLY the DDL payload.
-- It MUST be executed via run_migration.ps1 which ensures zero-mutation refusal before execution.
-- DO NOT RUN THIS SCRIPT DIRECTLY ON PRODUCTION!

-- 1. Drop dependent index ft_visit_requests_frontend_search and recreate without delegation_name
ALTER TABLE `visit_requests` DROP INDEX `ft_visit_requests_frontend_search`;
ALTER TABLE `visit_requests` ADD FULLTEXT KEY `ft_visit_requests_frontend_search` (
    `request_code`, `registrant_full_name`, `registrant_organization`, `registrant_email`,
    `contact_person_full_name`, `contact_person_organization`, `contact_person_email`
);

-- 2. Drop other dependent indexes
ALTER TABLE `visit_requests` DROP INDEX `idx_visit_requests_visit_type`;
ALTER TABLE `visit_requests` DROP INDEX `idx_visit_requests_media_consent`;

-- 3. Drop dependent CHECK constraint for visit_type dynamically
SET @chk_name = (SELECT CONSTRAINT_NAME FROM information_schema.TABLE_CONSTRAINTS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'visit_requests' AND CONSTRAINT_TYPE = 'CHECK' AND ENFORCED = 'YES' LIMIT 1);
SET @sql = IF(@chk_name IS NOT NULL, CONCAT('ALTER TABLE visit_requests DROP CHECK ', @chk_name), 'SELECT 1');
PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

-- 4. Drop the 10 legacy columns
ALTER TABLE `visit_requests`
    DROP COLUMN `delegation_name`,
    DROP COLUMN `visit_type`,
    DROP COLUMN `visit_type_other`,
    DROP COLUMN `purpose`,
    DROP COLUMN `working_content`,
    DROP COLUMN `working_language`,
    DROP COLUMN `transportation_note`,
    DROP COLUMN `media_consent_status`,
    DROP COLUMN `media_consent_note`,
    DROP COLUMN `note_to_fptu`;

SELECT 'Phase I Drop Completed' as result;
