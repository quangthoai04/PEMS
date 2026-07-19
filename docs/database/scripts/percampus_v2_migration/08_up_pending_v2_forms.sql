-- ============================================================================
-- Per-campus form v2 — Phase G-4A (public OTP initiate) — 08_up_pending_v2_forms.sql
-- ADDITIVE + IDEMPOTENT. Creates the pending-submission store that binds the FULL
-- canonical v2 form snapshot to a submit intent at `initiate` so `verify` builds the
-- request from EXACTLY what was OTP-verified (the client cannot swap campus/member/
-- contact/time/content between initiate and verify — plan §7 security invariant).
--
--   • One row per submission intent (UNIQUE submission_id), created at initiate,
--     reused across OTP resends, marked consumed_at when the request is created.
--   • snapshot_json is the authoritative source; fingerprint_v2 is a defence-in-depth
--     compare. Neither is ever logged.
--   • Independent of the OTP token lifecycle (survives resend, which supersedes the
--     otp_tokens row but keeps the same submission_id).
--
-- CREATE TABLE IF NOT EXISTS is inherently idempotent; re-running is a no-op.
-- Requires MySQL 8.0+. Import AFTER 02_up_additive.sql (independent of 03–07).
-- ============================================================================

CREATE TABLE IF NOT EXISTS visit_request_pending_forms (
    pending_form_id      BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    submission_id        VARCHAR(36)     NOT NULL,
    registrant_email     VARCHAR(255)    NOT NULL,
    form_schema_version  SMALLINT        NOT NULL DEFAULT 2,
    fingerprint_v2       CHAR(64)        NOT NULL,
    snapshot_json        LONGTEXT        NOT NULL,
    created_at           DATETIME        NOT NULL,
    expires_at           DATETIME        NOT NULL,
    consumed_at          DATETIME        NULL,
    PRIMARY KEY (pending_form_id),
    UNIQUE KEY uq_pending_forms_submission (submission_id),
    KEY idx_pending_forms_expires (expires_at)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- Verify (expect 1):
SELECT COUNT(*) AS pending_forms_table_ok
FROM information_schema.tables
WHERE table_schema = DATABASE() AND table_name = 'visit_request_pending_forms';
