-- ============================================================================
-- Per-campus form v2 — Phase H-4 fix — 09_up_op_contact_optional.sql
-- ADDITIVE + IDEMPOTENT. Relaxes two visit_instance_form_details columns from
-- NOT NULL to NULL so an OPTIONAL operational (working) contact organization /
-- email can be stored as NULL when left blank. The validator and the frontend
-- always treated these as optional, but the columns were NOT NULL and carried a
-- `TRIM(x) <> ''` CHECK — an empty string violated the CHECK and a NULL violated
-- NOT NULL, so a blank operational-contact email/org produced a 500 at create.
-- The CHECK constraints are KEPT (they still reject an empty string; NULL passes).
-- Name + phone stay required (NOT NULL).
--
-- MODIFY COLUMN to the same type + NULL is a no-op when already applied → idempotent.
-- Requires MySQL 8.0+. Import AFTER 02_up_additive.sql (independent of 03–08).
-- ============================================================================

DROP PROCEDURE IF EXISTS pems_v2_relax_op_contact_nullability;

DELIMITER $$
CREATE PROCEDURE pems_v2_relax_op_contact_nullability()
BEGIN
    DECLARE v_org_nullable VARCHAR(3);
    DECLARE v_email_nullable VARCHAR(3);

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

    IF v_org_nullable = 'NO' THEN
        ALTER TABLE visit_instance_form_details
            MODIFY operational_contact_organization VARCHAR(255) NULL;
    END IF;

    IF v_email_nullable = 'NO' THEN
        ALTER TABLE visit_instance_form_details
            MODIFY operational_contact_email VARCHAR(150) NULL;
    END IF;
END $$
DELIMITER ;

CALL pems_v2_relax_op_contact_nullability();
DROP PROCEDURE IF EXISTS pems_v2_relax_op_contact_nullability;

-- Verify (expect both = 'YES'):
SELECT column_name, is_nullable
FROM information_schema.columns
WHERE table_schema = DATABASE()
  AND table_name = 'visit_instance_form_details'
  AND column_name IN ('operational_contact_organization', 'operational_contact_email');
