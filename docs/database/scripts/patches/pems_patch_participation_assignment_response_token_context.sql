-- ADDITIVE + IDEMPOTENT. Extends email_action_tokens.action_context with
-- 'PARTICIPATION_ASSIGNMENT_RESPONSE' — the Department-Staff delegated-assignment email context,
-- distinct from 'PARTICIPATION_RESPONSE' (a direct invitation) so the two can require opposite
-- starting participant statuses (INVITED vs ASSIGNED). See
-- docs/CanhIter3FixBug/GopYCQuyen/PEMS_EMAIL_ACTION_SYSTEM_WIDE_FIX_SPEC_2026-08-21.md BUG-02.
--
-- Extending a MySQL ENUM by APPENDING a value is metadata-only (no table rebuild of existing
-- rows) and safe on a running database. Existing values keep their ordinal position; nothing
-- already stored changes meaning. Safe to run more than once (skips if already applied).

DELIMITER $$
DROP PROCEDURE IF EXISTS pems_patch_participation_assignment_response_token_context $$
CREATE PROCEDURE pems_patch_participation_assignment_response_token_context()
BEGIN
    DECLARE v_ctx TEXT;

    SELECT COLUMN_TYPE INTO v_ctx
    FROM information_schema.columns
    WHERE table_schema = DATABASE()
      AND table_name = 'email_action_tokens'
      AND column_name = 'action_context';

    IF v_ctx IS NOT NULL AND INSTR(v_ctx, 'PARTICIPATION_ASSIGNMENT_RESPONSE') = 0 THEN
        ALTER TABLE email_action_tokens
            MODIFY action_context ENUM(
                'PARTICIPATION_RESPONSE',
                'PARTICIPATION_ASSIGNMENT_RESPONSE',
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

CALL pems_patch_participation_assignment_response_token_context();
DROP PROCEDURE pems_patch_participation_assignment_response_token_context;

-- Verify
SELECT
  INSTR(COLUMN_TYPE, 'PARTICIPATION_ASSIGNMENT_RESPONSE') > 0 AS ctx_ok
FROM information_schema.columns
WHERE table_schema = DATABASE() AND table_name = 'email_action_tokens' AND column_name = 'action_context';
