-- ============================================================================
-- Per-campus form v2 — Phase D-4 (TRANSFER) — 07_up_transfer_tokens.sql
-- ADDITIVE + IDEMPOTENT. Appends the 'VISIT_CONTACT_TRANSFER' value to the
-- email_action_tokens.action_context ENUM so the 24h primary-contact TRANSFER
-- invitation reuses the same hash/expiry/single-use token store as the
-- INITIAL_CLAIM (plan §4.4). target_type 'VISIT_REQUEST_IDENTITY_CHANGE'
-- already exists (06_up). Appending an ENUM value is metadata-only and safe on
-- a running database; re-running this script is a no-op.
-- Requires MySQL 8.0+. Import AFTER 06_up_identity_claim_tokens.sql.
-- ============================================================================

DROP PROCEDURE IF EXISTS pems_v2_extend_transfer_token_enum;

DELIMITER $$
CREATE PROCEDURE pems_v2_extend_transfer_token_enum()
BEGIN
    DECLARE v_ctx TEXT;

    SELECT COLUMN_TYPE INTO v_ctx
    FROM information_schema.columns
    WHERE table_schema = DATABASE()
      AND table_name = 'email_action_tokens'
      AND column_name = 'action_context';

    IF v_ctx IS NOT NULL AND INSTR(v_ctx, 'VISIT_CONTACT_TRANSFER') = 0 THEN
        ALTER TABLE email_action_tokens
            MODIFY action_context ENUM(
                'PARTICIPATION_RESPONSE',
                'LOGISTICS_REQUEST_RESPONSE',
                'LOGISTICS_ASSIGNEE_RESPONSE',
                'LOGISTICS_NEGOTIATION',
                'LOGISTICS_PROPOSAL_RESPONSE',
                'LOGISTICS_HANDOVER_SIGNATURE',
                'VISIT_CONTACT_CLAIM',
                'VISIT_CONTACT_TRANSFER'
            ) NOT NULL;
    END IF;
END $$
DELIMITER ;

CALL pems_v2_extend_transfer_token_enum();
DROP PROCEDURE IF EXISTS pems_v2_extend_transfer_token_enum;

-- Verify (expect 1):
SELECT
  INSTR(COLUMN_TYPE, 'VISIT_CONTACT_TRANSFER') > 0 AS transfer_ctx_ok
FROM information_schema.columns
WHERE table_schema = DATABASE() AND table_name = 'email_action_tokens' AND column_name = 'action_context';
