-- =============================================================================
-- Migration (ADDITIVE) — account_email_confirmations (P0 #1)
--
-- One-time email-ownership proof for newly-created internal accounts. An account
-- is created with users.status = 'PENDING_EMAIL_CONFIRMATION' and is activated
-- only when a matching, unexpired, PENDING token is confirmed. Only the token
-- HASH is stored; the raw token lives solely in the emailed confirmation link.
--
-- Purely additive: creates one new table and does not alter users/departments/
-- campuses, so the running application never breaks mid-deploy. The new
-- PENDING_EMAIL_CONFIRMATION value for users.status is a plain string (the column
-- is VARCHAR, not a native ENUM in the canonical schema), so no column change is
-- required for it.
--
-- Idempotent: guarded with information_schema checks so it can be re-run safely.
-- Target: a disposable / allowlisted database ONLY (never pems_db, never prod).
-- MySQL 8.0, InnoDB, utf8mb4.
-- =============================================================================

SET @schema := DATABASE();

-- ── Create table account_email_confirmations (skip if it already exists) ─────
SET @tbl_exists := (
  SELECT COUNT(*) FROM information_schema.tables
  WHERE table_schema = @schema AND table_name = 'account_email_confirmations'
);

SET @ddl := IF(@tbl_exists = 0, '
CREATE TABLE account_email_confirmations (
    confirmation_id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,

    user_id         BIGINT UNSIGNED NOT NULL
        COMMENT ''Tài khoản đang chờ xác nhận email'',
    target_email    VARCHAR(255) NOT NULL
        COMMENT ''Email được chứng minh quyền sở hữu (email chuẩn hoá tại thời điểm phát hành)'',
    token_hash      CHAR(64) NOT NULL
        COMMENT ''SHA-256 (hex) của token gốc — KHÔNG bao giờ lưu token gốc'',

    status          VARCHAR(20) NOT NULL DEFAULT ''PENDING''
        COMMENT ''PENDING / CONFIRMED / EXPIRED / SUPERSEDED / CANCELLED'',

    expires_at      DATETIME NOT NULL,
    resend_count    INT NOT NULL DEFAULT 0,

    created_at      DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at      DATETIME NULL DEFAULT NULL ON UPDATE CURRENT_TIMESTAMP,
    confirmed_at    DATETIME NULL DEFAULT NULL,
    cancelled_at    DATETIME NULL DEFAULT NULL,

    PRIMARY KEY (confirmation_id),

    UNIQUE KEY uq_account_email_confirmations_token_hash (token_hash),
    KEY idx_account_email_confirmations_user (user_id),
    KEY idx_account_email_confirmations_status_expiry (status, expires_at),

    CONSTRAINT chk_account_email_confirmations_status
        CHECK (status IN (''PENDING'',''CONFIRMED'',''EXPIRED'',''SUPERSEDED'',''CANCELLED'')),
    CONSTRAINT chk_account_email_confirmations_target_email_not_blank
        CHECK (CHAR_LENGTH(TRIM(target_email)) > 0),

    CONSTRAINT fk_account_email_confirmations_user
        FOREIGN KEY (user_id) REFERENCES users(user_id)
        ON UPDATE CASCADE ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
COMMENT=''Bằng chứng sở hữu email một-lần cho tài khoản nội bộ mới (P0 #1)'';',
'SELECT ''account_email_confirmations already exists — skipped'' AS note;');

PREPARE stmt FROM @ddl;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;
