-- ============================================================================
-- Per-campus form v2 - 10_up_op_contact_required.sql
-- ADDITIVE + IDEMPOTENT. Restores NOT NULL on the two operational (working)
-- contact columns that 09_up_op_contact_optional.sql relaxed.
--
-- WHY THIS REVERSES 09
-- --------------------
-- 09 relaxed these columns because the validator and the frontend treated the
-- operational contact organization / email as OPTIONAL, so a blank value could
-- satisfy neither NOT NULL (NULL rejected) nor the TRIM(x) <> '' CHECK (empty
-- string rejected) and produced a 500 at create. The contract has since changed:
-- all four operational-contact fields are REQUIRED, enforced in the frontend
-- schema and in CampusVisitFormDtoValidator. The reason 09 existed is gone, so
-- the column definition is brought back in line with the contract.
--
-- WHY THIS IS SAFE HERE (evidence gathered before writing this script)
-- -------------------------------------------------------------------
--   * visit_instance_form_details is written ONLY by the v2 services
--     (VisitRequestV2CreateService / V2EditService / VisitAmendmentService).
--     No trigger backfills it - verified against information_schema.TRIGGERS -
--     so the v1 create path cannot introduce a NULL here.
--   * Existing rows contain no NULL/blank in either column, in BOTH pems_db
--     (204 v1-schema + 4 v2-schema detail rows) and pems_review_v2 (204 + 37).
--   * The TRIM(x) <> '' CHECKs are KEPT, so an empty string stays rejected;
--     this script only closes the NULL hole that 09 opened.
--
-- The MODIFY is guarded on IS_NULLABLE, so re-running is a no-op. Requires
-- MySQL 8.0+. Import AFTER 02_up_additive.sql and after 09.
--
-- NOT APPLIED BY THIS CHANGE. Applying it needs the restricted pems_review
-- account on pems_review_v2; see the report accompanying this commit.
-- ============================================================================

DROP PROCEDURE IF EXISTS pems_v2_require_op_contact;

DELIMITER $$
CREATE PROCEDURE pems_v2_require_op_contact()
BEGIN
    DECLARE v_org_nullable VARCHAR(3);
    DECLARE v_email_nullable VARCHAR(3);
    DECLARE v_bad_rows INT;

    -- Refuse to run rather than fail halfway if any offending row exists.
    SELECT COUNT(*) INTO v_bad_rows
    FROM visit_instance_form_details
    WHERE operational_contact_organization IS NULL
       OR operational_contact_email IS NULL;

    IF v_bad_rows > 0 THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'Cannot require operational contact: NULL rows exist. Backfill them first.';
    END IF;

    SELECT IS_NULLABLE INTO v_org_nullable
    FROM information_schema.columns
    WHERE table_schema = DATABASE()
      AND table_name = 'visit_instance_form_details'
      AND column_name = 'operational_contact_organization';

    SELECT IS_NULLABLE INTO v_email_nullable
    FROM information_schema.columns
    WHERE table_schema = DATABASE()
      AND table_name = 'visit_instance_form_details'
      AND column_name = 'operational_contact_email';

    IF v_org_nullable = 'YES' THEN
        ALTER TABLE visit_instance_form_details
            MODIFY operational_contact_organization VARCHAR(255) NOT NULL;
    END IF;

    IF v_email_nullable = 'YES' THEN
        ALTER TABLE visit_instance_form_details
            MODIFY operational_contact_email VARCHAR(150) NOT NULL;
    END IF;
END $$
DELIMITER ;

CALL pems_v2_require_op_contact();
DROP PROCEDURE IF EXISTS pems_v2_require_op_contact;

-- Verify (expect both = 'NO'):
SELECT column_name, is_nullable
FROM information_schema.columns
WHERE table_schema = DATABASE()
  AND table_name = 'visit_instance_form_details'
  AND column_name IN ('operational_contact_organization', 'operational_contact_email');

-- ============================================================================
-- STAGED, DELIBERATELY NOT INCLUDED: visit_requests.working_content NOT NULL
-- ----------------------------------------------------------------------------
-- working_content is now required for every v2 write, but the column lives on
-- visit_requests, which the V1 create path also writes - and the V1 BACKEND has
-- no working-content rule at all (only the V1 frontend requires it). A direct
-- API call to the v1 endpoint can therefore still store a blank, so adding
-- NOT NULL here would convert a silent v1 gap into a 500 on a path this work is
-- not meant to change.
--
-- Current data is clean in both databases (0 blank of 122 v1 + v2 requests in
-- pems_db, 0 of 143 in pems_review_v2), so this is a write-path guarantee gap,
-- not a data problem. Precondition for tightening: either the v1 create
-- validator enforces working content too, or Phase I contract-drop removes the
-- shared legacy column. Until then the rule is enforced in the v2 application
-- layer only.
-- ============================================================================
