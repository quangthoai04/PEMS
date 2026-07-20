-- =====================================================================================
-- Phase I — GUARDED DOWN / RESTORE (re-creates the 10 legacy compatibility columns)
-- =====================================================================================
-- FAIL-CLOSED BY CONSTRUCTION. Requires, set by the runner:
--     SET @ENABLE_PHASE_1_RESTORE = 1;
-- and a disposable database. Sourcing this file directly raises an error before any DDL.
--
-- LOSSLESSNESS CONTRACT (§6.3): this script NEVER fabricates data. The legacy columns are
-- re-added NULLable, backfilled from the canonical per-campus details (smallest campus_id
-- projection — the same rule the compatibility projection always used), and only then
-- tightened to NOT NULL. If ANY row cannot be backfilled, the script ABORTS and leaves the
-- columns NULLable rather than inventing placeholder values such as 'N/A'.
--
-- Exact-schema restoration: type, length, nullability, default, comment AND ordinal position
-- are restored (ordinals via AFTER, taken from the authoritative master schema).
-- Residual drift that MySQL cannot avoid without a full table rebuild is measured by
-- 03_verify.sql and reported, never silently accepted:
--   * the master declares the visit_type CHECK UNNAMED, so MySQL auto-assigns
--     visit_requests_chk_N; the EXPRESSION is restored exactly but the auto-generated
--     ordinal suffix may differ from the pre-UP name.
-- =====================================================================================

-- ── GUARD: enable flag + disposable allowlist (aborts before any DDL) ──
SET @guard_ok = IF(
      IFNULL(@ENABLE_PHASE_1_RESTORE, 0) = 1
  AND DATABASE() IN ('pems_i_fresh','pems_i_upgrade','pems_i_refusal','pems_i_rollback'), 1, 0);
SET @guard_sql = IF(@guard_ok = 1, 'DO 0',
  'SELECT `PHASE_I_REFUSED__requires_ENABLE_PHASE_1_RESTORE_on_a_disposable_database`');
PREPARE guard_stmt FROM @guard_sql; EXECUTE guard_stmt; DEALLOCATE PREPARE guard_stmt;

-- ── 1. Re-add the 10 columns NULLable, at their EXACT original ordinal positions ──
ALTER TABLE `visit_requests`
    ADD COLUMN `delegation_name` VARCHAR(200) NULL COMMENT 'Tên đoàn khách (compatibility projection; v2 dùng visit_instance_form_details.delegation_name theo campus)' AFTER `registrant_nationality`,
    ADD COLUMN `visit_type` ENUM('CAMPUS_TOUR','MEETING','WORKSHOP','SIGNING_CEREMONY','EXCHANGE','OTHER') NULL COMMENT 'Compatibility projection; v2 dùng visit_instance_form_details.visit_type theo campus' AFTER `visit_scope`,
    ADD COLUMN `visit_type_other` VARCHAR(255) NULL AFTER `visit_type`,
    ADD COLUMN `purpose` TEXT NULL COMMENT 'Mục đích thăm FPTU (compatibility projection; v2 dùng visit_instance_form_details.purpose theo campus)' AFTER `visit_type_other`,
    ADD COLUMN `working_content` TEXT NULL COMMENT 'Nội dung làm việc (compatibility projection; v2 dùng visit_instance_form_details.working_content theo campus)' AFTER `purpose`,
    ADD COLUMN `working_language` ENUM('VI','EN') NULL COMMENT 'Ngôn ngữ sử dụng trong visit. Chỉ dùng VI/EN theo frontend hiện tại, không có lựa chọn OTHER' AFTER `contact_person_email`,
    ADD COLUMN `transportation_note` TEXT NULL COMMENT 'Nhận diện phương tiện di chuyển tới FPTU do khách nhập tự do' AFTER `working_language`,
    ADD COLUMN `media_consent_status` ENUM('AGREED','DECLINED') NULL AFTER `transportation_note`,
    ADD COLUMN `media_consent_note` TEXT NULL AFTER `media_consent_status`,
    ADD COLUMN `note_to_fptu` TEXT NULL COMMENT 'Ghi chú cho FPTU' AFTER `media_consent_note`;

-- ── 2. Backfill the compatibility projection from the canonical per-campus detail ──
UPDATE visit_requests vr
JOIN (
    SELECT vrc.visit_request_id,
           fd.delegation_name, fd.visit_type, fd.visit_type_other, fd.purpose,
           fd.working_content, fd.working_language, fd.transportation_note,
           fd.media_consent_status, fd.media_consent_note, fd.note_to_fptu,
           ROW_NUMBER() OVER (PARTITION BY vrc.visit_request_id ORDER BY vrc.campus_id ASC) AS rn
    FROM visit_request_campuses vrc
    JOIN visit_instance_form_details fd ON fd.visit_instance_id = vrc.visit_instance_id
) sub ON sub.visit_request_id = vr.visit_request_id AND sub.rn = 1
SET vr.delegation_name      = sub.delegation_name,
    vr.visit_type           = sub.visit_type,
    vr.visit_type_other     = sub.visit_type_other,
    vr.purpose              = sub.purpose,
    vr.working_content      = sub.working_content,
    vr.working_language     = sub.working_language,
    vr.transportation_note  = sub.transportation_note,
    vr.media_consent_status = sub.media_consent_status,
    vr.media_consent_note   = sub.media_consent_note,
    vr.note_to_fptu         = sub.note_to_fptu;

-- ── 3. LOSSLESSNESS GATE: refuse to fabricate values for the NOT NULL columns ──
-- The master declares delegation_name / visit_type / purpose / working_language /
-- media_consent_status as NOT NULL. If the backfill above could not populate every row,
-- the restore is NOT lossless: abort with the columns still NULLable instead of writing
-- placeholder data (explicitly forbidden by the corrective contract).
SET @unbackfilled = (SELECT COUNT(*) FROM visit_requests
                     WHERE delegation_name IS NULL OR visit_type IS NULL OR purpose IS NULL
                        OR working_language IS NULL OR media_consent_status IS NULL);
SET @loss_sql = IF(@unbackfilled = 0, 'DO 0',
  'SELECT `PHASE_I_RESTORE_ABORTED__backfill_incomplete_refusing_to_fabricate_NOT_NULL_values`');
PREPARE loss_stmt FROM @loss_sql; EXECUTE loss_stmt; DEALLOCATE PREPARE loss_stmt;

-- ── 4. Tighten to the EXACT master definitions (MODIFY preserves ordinal position) ──
ALTER TABLE `visit_requests`
    MODIFY COLUMN `delegation_name` VARCHAR(200) NOT NULL COMMENT 'Tên đoàn khách (compatibility projection; v2 dùng visit_instance_form_details.delegation_name theo campus)',
    MODIFY COLUMN `visit_type` ENUM('CAMPUS_TOUR','MEETING','WORKSHOP','SIGNING_CEREMONY','EXCHANGE','OTHER') NOT NULL DEFAULT 'CAMPUS_TOUR' COMMENT 'Compatibility projection; v2 dùng visit_instance_form_details.visit_type theo campus',
    MODIFY COLUMN `purpose` TEXT NOT NULL COMMENT 'Mục đích thăm FPTU (compatibility projection; v2 dùng visit_instance_form_details.purpose theo campus)',
    MODIFY COLUMN `working_language` ENUM('VI','EN') NOT NULL DEFAULT 'EN' COMMENT 'Ngôn ngữ sử dụng trong visit. Chỉ dùng VI/EN theo frontend hiện tại, không có lựa chọn OTHER',
    MODIFY COLUMN `media_consent_status` ENUM('AGREED','DECLINED') NOT NULL DEFAULT 'DECLINED';

-- ── 5. Restore the dependent indexes ──
ALTER TABLE `visit_requests`
    ADD KEY `idx_visit_requests_visit_type` (`visit_type`),
    ADD KEY `idx_visit_requests_media_consent` (`media_consent_status`);

-- ── 6. Restore the visit_type CHECK with the master's exact expression ──
-- Declared unnamed in the master, so MySQL auto-names it (see the drift note in the header).
ALTER TABLE `visit_requests`
    ADD CHECK (visit_type <> 'OTHER' OR (visit_type_other IS NOT NULL AND TRIM(visit_type_other) <> ''));

-- ── 7. Restore the FULLTEXT index INCLUDING delegation_name ──
ALTER TABLE `visit_requests` DROP INDEX `ft_visit_requests_frontend_search`;
ALTER TABLE `visit_requests` ADD FULLTEXT KEY `ft_visit_requests_frontend_search` (
    `request_code`, `delegation_name`, `registrant_full_name`, `registrant_organization`, `registrant_email`,
    `contact_person_full_name`, `contact_person_organization`, `contact_person_email`
);

SELECT 'PHASE1_DOWN_RESULT: DONE' AS result;
