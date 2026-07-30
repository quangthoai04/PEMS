-- =====================================================================
-- G11 final closure — email_templates.revision
-- STEP 3 of 3: VERIFY
-- =====================================================================
--
-- Read-only except for one rolled-back behavioural probe. Every check prints
-- PASS or FAIL, and pems_revision_verify_gate() raises SQLSTATE 45000 if any
-- check failed — so this can be used as a deployment gate rather than as
-- output somebody has to read carefully.
--
-- Usage:
--   mysql -u <user> -p <database> < 03_verify.sql
-- =====================================================================

SELECT CONCAT('Verifying: ', DATABASE(), ' at ', NOW()) AS status;

DROP TEMPORARY TABLE IF EXISTS _pems_revision_checks;
CREATE TEMPORARY TABLE _pems_revision_checks (
  check_name VARCHAR(120) NOT NULL,
  result     VARCHAR(10)  NOT NULL,
  detail     VARCHAR(400) NULL
) ENGINE=MEMORY;

-- ---------------------------------------------------------------------
-- Structure
-- ---------------------------------------------------------------------
INSERT INTO _pems_revision_checks
SELECT 'column_exists',
       IF(COUNT(*) = 1, 'PASS', 'FAIL'),
       CONCAT('found ', COUNT(*), ' column(s) named revision')
FROM information_schema.columns
WHERE table_schema = DATABASE() AND table_name = 'email_templates' AND column_name = 'revision';

INSERT INTO _pems_revision_checks
SELECT 'column_type_is_int_unsigned',
       IF(LOWER(column_type) = 'int unsigned', 'PASS', 'FAIL'),
       CONCAT('column_type = ', column_type)
FROM information_schema.columns
WHERE table_schema = DATABASE() AND table_name = 'email_templates' AND column_name = 'revision';

-- NOT NULL matters: a NULL revision would make `AND revision = :expected` never match, and every
-- save would be reported as a concurrency conflict against a change nobody made.
INSERT INTO _pems_revision_checks
SELECT 'column_is_not_null',
       IF(is_nullable = 'NO', 'PASS', 'FAIL'),
       CONCAT('is_nullable = ', is_nullable)
FROM information_schema.columns
WHERE table_schema = DATABASE() AND table_name = 'email_templates' AND column_name = 'revision';

INSERT INTO _pems_revision_checks
SELECT 'column_default_is_1',
       IF(column_default = '1', 'PASS', 'FAIL'),
       CONCAT('column_default = ', IFNULL(column_default, 'NULL'))
FROM information_schema.columns
WHERE table_schema = DATABASE() AND table_name = 'email_templates' AND column_name = 'revision';

-- ---------------------------------------------------------------------
-- Data
-- ---------------------------------------------------------------------
INSERT INTO _pems_revision_checks
SELECT 'every_row_has_a_usable_revision',
       IF(COUNT(*) = 0, 'PASS', 'FAIL'),
       CONCAT(COUNT(*), ' row(s) with revision NULL or 0')
FROM email_templates WHERE revision IS NULL OR revision = 0;

INSERT INTO _pems_revision_checks
SELECT 'template_rows_preserved',
       IF(COUNT(*) > 0, 'PASS', 'FAIL'),
       CONCAT(COUNT(*), ' template row(s) present')
FROM email_templates;

-- The migration must not have touched content. Reported as an informational digest so two runs can
-- be compared to each other, which is what actually proves nothing changed.
--
-- Hashed PER ROW first, then the 32-char hashes are concatenated. Concatenating the bodies themselves
-- silently truncates at group_concat_max_len (1024 bytes by default) — one template body exceeds that
-- on its own, so the digest would have been computed over a fraction of the content and would have
-- compared equal across two genuinely different catalogs. The session limit is raised as well, so the
-- hash list cannot be cut either as the catalog grows.
SET SESSION group_concat_max_len = 1048576;

INSERT INTO _pems_revision_checks
SELECT 'content_digest',
       'INFO',
       CONCAT('md5 = ', MD5(GROUP_CONCAT(row_digest ORDER BY template_code SEPARATOR '')))
FROM (
  SELECT template_code,
         MD5(CONCAT_WS('|', template_code, IFNULL(subject_vi,''), IFNULL(body_vi,''),
                            IFNULL(subject_en,''), IFNULL(body_en,''))) AS row_digest
  FROM email_templates
) AS per_row;

-- ---------------------------------------------------------------------
-- Behaviour: the conditional UPDATE the application relies on.
--
-- Rolled back, so verification leaves the database exactly as it found it.
-- This is the check that matters: the structure above could be perfect and the
-- column still useless if the conditional write did not behave.
-- ---------------------------------------------------------------------
SET @probe_id := (SELECT email_template_id FROM email_templates ORDER BY email_template_id LIMIT 1);
SET @probe_rev := (SELECT revision FROM email_templates WHERE email_template_id = @probe_id);

START TRANSACTION;

UPDATE email_templates
   SET revision = revision + 1
 WHERE email_template_id = @probe_id AND revision = @probe_rev;
SET @matched_current := ROW_COUNT();

UPDATE email_templates
   SET revision = revision + 1
 WHERE email_template_id = @probe_id AND revision = @probe_rev;
SET @matched_stale := ROW_COUNT();

ROLLBACK;

INSERT INTO _pems_revision_checks VALUES
  ('conditional_update_matches_current_revision',
   IF(@matched_current = 1, 'PASS', 'FAIL'),
   CONCAT('rows affected = ', @matched_current, ' (expected 1)')),
  ('conditional_update_refuses_a_stale_revision',
   IF(@matched_stale = 0, 'PASS', 'FAIL'),
   CONCAT('rows affected = ', @matched_stale, ' (expected 0)'));

INSERT INTO _pems_revision_checks
SELECT 'probe_left_no_trace',
       IF(revision = @probe_rev, 'PASS', 'FAIL'),
       CONCAT('revision is ', revision, ', expected ', @probe_rev)
FROM email_templates WHERE email_template_id = @probe_id;

-- ---------------------------------------------------------------------
-- Report
-- ---------------------------------------------------------------------
SELECT '=== Results ===' AS section;
SELECT check_name, result, detail FROM _pems_revision_checks ORDER BY check_name;

SELECT '=== Summary ===' AS section;
SELECT
  SUM(result = 'PASS') AS passed,
  SUM(result = 'FAIL') AS failed,
  SUM(result = 'INFO') AS informational
FROM _pems_revision_checks;

DELIMITER $$
DROP PROCEDURE IF EXISTS pems_revision_verify_gate$$
CREATE PROCEDURE pems_revision_verify_gate()
BEGIN
  DECLARE v_failed INT DEFAULT 0;
  SELECT COUNT(*) INTO v_failed FROM _pems_revision_checks WHERE result = 'FAIL';

  IF v_failed > 0 THEN
    SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT =
      'email_template_revision verification FAILED. Do not deploy the backend against this database.';
  END IF;
END$$
DELIMITER ;

CALL pems_revision_verify_gate();
DROP PROCEDURE pems_revision_verify_gate;

SELECT '=== Verification passed ===' AS status;
