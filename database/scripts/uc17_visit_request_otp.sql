-- ============================================================
-- UC-17: Submit Visit Request — DB changes
-- Run against: pems_db
-- ============================================================

-- 1. Temporary session storage for OTP-gated visit-request submissions
CREATE TABLE IF NOT EXISTS `pending_visit_requests` (
    `pending_id`     CHAR(36)      NOT NULL,
    `email`          VARCHAR(255)  NOT NULL,
    `form_data_json` LONGTEXT      NOT NULL,
    `expires_at`     DATETIME(6)   NOT NULL,
    `ip_address`     VARCHAR(45)   NULL,
    `created_at`     DATETIME(6)   NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    PRIMARY KEY (`pending_id`),
    INDEX `ix_pending_visit_requests_email_expires` (`email`, `expires_at`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- 2. Scheduled cleanup event (optional but recommended)
--    Removes expired sessions older than 1 hour automatically.
DROP EVENT IF EXISTS `evt_purge_pending_visit_requests`;
CREATE EVENT `evt_purge_pending_visit_requests`
    ON SCHEDULE EVERY 30 MINUTE
    DO
        DELETE FROM `pending_visit_requests`
        WHERE `expires_at` < NOW() - INTERVAL 1 HOUR;

-- 3. Ensure new visit-request status values are represented
--    (status column is VARCHAR — no ENUM change required if using VARCHAR)
--    If visit_requests.status is an ENUM, run the ALTER below:
-- ALTER TABLE `visit_requests`
--     MODIFY COLUMN `status` ENUM(
--         'PENDING_APPROVAL',
--         'PENDING_HO_APPROVAL',
--         'PENDING_STAFF_LEAD_APPROVAL',
--         'APPROVED',
--         'REJECTED',
--         'CANCELLED',
--         'IN_PROGRESS',
--         'COMPLETED'
--     ) NOT NULL DEFAULT 'PENDING_STAFF_LEAD_APPROVAL';

-- 4. OTP tokens: ensure VISIT_REQUEST_VERIFY purpose is indexed
--    (otp_tokens table should already exist; index helps the verify query)
--    Using dynamic SQL because CREATE INDEX IF NOT EXISTS requires MySQL 8.0.1+
SET @idx_exists = (
    SELECT COUNT(*) FROM information_schema.STATISTICS
    WHERE table_schema = DATABASE()
      AND table_name   = 'otp_tokens'
      AND index_name   = 'ix_otp_tokens_email_purpose_used'
);
SET @sql = IF(@idx_exists = 0,
    'CREATE INDEX `ix_otp_tokens_email_purpose_used` ON `otp_tokens` (`email`, `purpose`, `used_at`)',
    'SELECT ''Index already exists, skipped.'' AS info'
);
PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;
