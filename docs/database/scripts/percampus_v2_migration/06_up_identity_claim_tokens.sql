-- ============================================================================
-- Per-campus form v2 — Phase D (identity claim) — 06_up_identity_claim_tokens.sql
-- ADDITIVE + IDEMPOTENT. Extends the email_action_tokens ENUMs so the identity
-- INITIAL_CLAIM invitation can reuse the existing hash/expiry/single-use token
-- store (plan §4.4):
--   • action_context += 'VISIT_CONTACT_CLAIM'
--   • target_type    += 'VISIT_REQUEST_IDENTITY_CHANGE'
-- Extending a MySQL ENUM by APPENDING values is metadata-only (no table rebuild
-- of existing rows' semantics) and safe on a running database. Existing values
-- keep their order/indices. Re-running this script is a no-op.
-- Requires MySQL 8.0+. Import AFTER 02_up_additive.sql (any time; independent).
-- ============================================================================

DROP PROCEDURE IF EXISTS pems_v2_extend_email_action_enums;

DELIMITER $$
CREATE PROCEDURE pems_v2_extend_email_action_enums()
BEGIN
    DECLARE v_ctx TEXT;
    DECLARE v_tgt TEXT;

    SELECT COLUMN_TYPE INTO v_ctx
    FROM information_schema.columns
    WHERE table_schema = DATABASE()
      AND table_name = 'email_action_tokens'
      AND column_name = 'action_context';

    SELECT COLUMN_TYPE INTO v_tgt
    FROM information_schema.columns
    WHERE table_schema = DATABASE()
      AND table_name = 'email_action_tokens'
      AND column_name = 'target_type';

    IF v_ctx IS NOT NULL AND INSTR(v_ctx, 'VISIT_CONTACT_CLAIM') = 0 THEN
        ALTER TABLE email_action_tokens
            MODIFY action_context ENUM(
                'PARTICIPATION_RESPONSE',
                'LOGISTICS_REQUEST_RESPONSE',
                'LOGISTICS_ASSIGNEE_RESPONSE',
                'LOGISTICS_NEGOTIATION',
                'LOGISTICS_PROPOSAL_RESPONSE',
                'LOGISTICS_HANDOVER_SIGNATURE',
                'VISIT_CONTACT_CLAIM'
            ) NOT NULL;
    END IF;

    IF v_tgt IS NOT NULL AND INSTR(v_tgt, 'VISIT_REQUEST_IDENTITY_CHANGE') = 0 THEN
        ALTER TABLE email_action_tokens
            MODIFY target_type ENUM(
                'VISIT_PARTICIPANT',
                'LOGISTICS_ITEM',
                'LOGISTICS_HANDOVER',
                'VISIT_REQUEST_IDENTITY_CHANGE'
            ) NOT NULL;
    END IF;
END $$
DELIMITER ;

CALL pems_v2_extend_email_action_enums();
DROP PROCEDURE IF EXISTS pems_v2_extend_email_action_enums;

-- Verify (expect both = 1):
SELECT
  INSTR(COLUMN_TYPE, 'VISIT_CONTACT_CLAIM') > 0 AS ctx_ok
FROM information_schema.columns
WHERE table_schema = DATABASE() AND table_name = 'email_action_tokens' AND column_name = 'action_context';
SELECT
  INSTR(COLUMN_TYPE, 'VISIT_REQUEST_IDENTITY_CHANGE') > 0 AS tgt_ok
FROM information_schema.columns
WHERE table_schema = DATABASE() AND table_name = 'email_action_tokens' AND column_name = 'target_type';
