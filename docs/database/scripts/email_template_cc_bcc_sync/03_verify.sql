-- =====================================================================
-- 03_verify.sql — prove the target database is in the state the sync promised
--
-- Read-only. Run AFTER 02_sync_templates.sql, and again after a second sync run.
--
-- Usage (mysql client):
--     mysql -h <host> -u <user> -p <database> < 03_verify.sql
--
-- Two kinds of check live here, and the difference matters:
--
--   INTRINSIC — true of the end state on its own ("all 30 canonical codes are ACTIVE", "no legacy
--   code is ACTIVE", "variables_text matches the placeholders actually used"). Every one of these
--   reports PASS or FAIL in the `verdict` column, and section Z fails the whole run if any did.
--
--   DIFFERENTIAL — "nothing else changed". No single snapshot can prove that, so this script does
--   not pretend to. It prints CHECKSUMs (section 9) of the tables the sync must not touch. Run
--   01_preflight.sql before the sync and this after: the checksum lines must be identical. The
--   automated test EmailTemplateSyncScriptTests does exactly that comparison, and additionally
--   proves the second sync run is a no-op.
-- =====================================================================

-- Read Vietnamese template content back as UTF-8. The mysql client on Windows otherwise decodes
-- it with the console codepage and every comparison below is made against mojibake.
SET NAMES utf8mb4;


-- Every check writes one row into this temporary table so section Z can give a single verdict
-- instead of asking a human to scan twenty result sets and notice the one that says FAIL.
DROP TEMPORARY TABLE IF EXISTS _pems_verify_results;
CREATE TEMPORARY TABLE _pems_verify_results (
  seq      INT AUTO_INCREMENT PRIMARY KEY,
  check_id VARCHAR(8)   NOT NULL,
  check_name VARCHAR(90) NOT NULL,
  verdict  VARCHAR(6)   NOT NULL,
  detail   VARCHAR(500) NULL
) ENGINE=InnoDB;


SELECT '── A. Every canonical template is present and ACTIVE ───────────' AS ``;

DROP TEMPORARY TABLE IF EXISTS _pems_canonical_codes;
CREATE TEMPORARY TABLE _pems_canonical_codes (template_code VARCHAR(100) PRIMARY KEY) ENGINE=InnoDB;
INSERT INTO _pems_canonical_codes (template_code) VALUES
  ('ACCOUNT_EMAIL_CONFIRMATION'),('ACCOUNT_PENDING_EMAIL_CHANGED_OLD_NOTICE'),('ACCOUNT_ACTIVATED'),
  ('ACCOUNT_EMAIL_CHANGED_OLD_NOTICE'),('ACCOUNT_EMAIL_CHANGED_NEW_NOTICE'),('ACCOUNT_ROLE_CHANGED'),
  ('ACCOUNT_STAFF_LEADER_ASSIGNED'),('ACCOUNT_STAFF_LEADER_REPLACED'),
  ('DEPT_PERSONNEL_ACCOUNT_DISABLED'),('DEPT_PERSONNEL_ACCOUNT_ENABLED'),
  ('DEPT_LEADERSHIP_GRANTED'),('DEPT_LEADERSHIP_HANDED_OVER'),
  ('AUTH_PASSWORD_RESET_OTP'),
  ('VISIT_REQUEST_OTP'),('VISIT_CONTACT_CLAIM'),('VISIT_CONTACT_TRANSFER'),
  ('VISIT_PARTICIPANT_INVITATION'),('VISIT_STUDENT_INVITATION'),
  ('VISIT_DEPARTMENT_LEADER_INVITATION'),('VISIT_DEPARTMENT_STAFF_ASSIGNMENT'),
  ('VISIT_REMINDER_HOST'),('VISIT_REMINDER_PARTICIPANTS'),
  ('LOGISTICS_REQUEST_TO_DEPARTMENT'),('LOGISTICS_ASSIGNEE_ASSIGNMENT'),
  ('LOGISTICS_CHANGE_PROPOSAL_TO_HOST'),('LOGISTICS_EXPENSE_REPORT_REMINDER'),
  ('REPORT_CAMPUS_OPERATION'),('REPORT_DEPARTMENT_COLLABORATION'),
  ('REPORT_DEPARTMENT_INVOICE'),('REPORT_PERSONNEL_PERFORMANCE');

DROP TEMPORARY TABLE IF EXISTS _pems_legacy_codes;
CREATE TEMPORARY TABLE _pems_legacy_codes (template_code VARCHAR(100) PRIMARY KEY) ENGINE=InnoDB;
INSERT INTO _pems_legacy_codes (template_code) VALUES
  ('ACCOUNT_CREATED_INTERNAL'),('VISIT_REQUEST_APPROVED'),('VISIT_REQUEST_REJECTED'),
  ('VISIT_CANCELLED'),('HOST_ASSIGNMENT'),('VISIT_REQUEST_SUBMITTED_NOTIFY'),
  ('LOGISTICS_REQUEST'),('LOGISTICS_REQUEST_SUBMITTED_NOTIFY'),('OTP_VISIT_REQUEST');

-- A1: no caller left without a template — every registry code exists.
INSERT INTO _pems_verify_results (check_id, check_name, verdict, detail)
SELECT 'A1', 'every canonical code exists (no caller without a template)',
       IF(COUNT(*) = 0, 'PASS', 'FAIL'),
       IF(COUNT(*) = 0, '30 codes present', CONCAT('missing: ', GROUP_CONCAT(c.template_code)))
FROM _pems_canonical_codes c
LEFT JOIN email_templates t ON t.template_code = c.template_code
WHERE t.email_template_id IS NULL;

-- A2: and every one of them is ACTIVE, or the renderer refuses at send time.
INSERT INTO _pems_verify_results (check_id, check_name, verdict, detail)
SELECT 'A2', 'every canonical code is ACTIVE',
       IF(COUNT(*) = 0, 'PASS', 'FAIL'),
       IF(COUNT(*) = 0, 'none inactive', CONCAT('inactive: ', GROUP_CONCAT(t.template_code)))
FROM _pems_canonical_codes c
JOIN email_templates t ON t.template_code = c.template_code
WHERE t.status <> 'ACTIVE';

SELECT c.template_code, t.status, t.purpose, t.body_format
FROM _pems_canonical_codes c
LEFT JOIN email_templates t ON t.template_code = c.template_code
WHERE t.email_template_id IS NULL OR t.status <> 'ACTIVE'
ORDER BY c.template_code;


SELECT '── B. No dead template is ACTIVE ───────────────────────────────' AS ``;

-- B1: the nine DL-03 codes must be present-but-INACTIVE or absent. Never ACTIVE.
INSERT INTO _pems_verify_results (check_id, check_name, verdict, detail)
SELECT 'B1', 'no legacy (DL-03) code is ACTIVE',
       IF(COUNT(*) = 0, 'PASS', 'FAIL'),
       IF(COUNT(*) = 0, 'all retired or absent', CONCAT('still active: ', GROUP_CONCAT(t.template_code)))
FROM _pems_legacy_codes l
JOIN email_templates t ON t.template_code = l.template_code
WHERE t.status = 'ACTIVE';

-- B2: a legacy row that something still references must survive as a row. Deleting it would orphan
-- history; this proves the sync deactivated rather than deleted.
INSERT INTO _pems_verify_results (check_id, check_name, verdict, detail)
SELECT 'B2', 'referenced legacy templates still exist (deactivated, not deleted)',
       IF(COUNT(*) = 0, 'PASS', 'FAIL'),
       CONCAT(COUNT(*), ' referenced legacy row(s) missing')
FROM (
  SELECT s.email_template_id FROM sent_emails  s WHERE s.email_template_id IS NOT NULL
  UNION
  SELECT d.email_template_id FROM email_drafts d WHERE d.email_template_id IS NOT NULL
) refs
LEFT JOIN email_templates t ON t.email_template_id = refs.email_template_id
WHERE t.email_template_id IS NULL;

SELECT l.template_code,
       IFNULL(t.status, '(absent)') AS status,
       COUNT(DISTINCT s.sent_email_id)  AS referencing_sent_emails,
       COUNT(DISTINCT d.email_draft_id) AS referencing_drafts
FROM _pems_legacy_codes l
LEFT JOIN email_templates t ON t.template_code = l.template_code
LEFT JOIN sent_emails  s ON s.email_template_id = t.email_template_id
LEFT JOIN email_drafts d ON d.email_template_id = t.email_template_id
GROUP BY l.template_code, t.status
ORDER BY l.template_code;


SELECT '── C. No duplicate template_code ───────────────────────────────' AS ``;

INSERT INTO _pems_verify_results (check_id, check_name, verdict, detail)
SELECT 'C1', 'template_code is unique',
       IF(COUNT(*) = 0, 'PASS', 'FAIL'),
       CONCAT(COUNT(*), ' code(s) appear more than once')
FROM (SELECT template_code FROM email_templates GROUP BY template_code HAVING COUNT(*) > 1) d;


SELECT '── D. Canonical rows carry both languages ──────────────────────' AS ``;

INSERT INTO _pems_verify_results (check_id, check_name, verdict, detail)
SELECT 'D1', 'no canonical template missing VI or EN',
       IF(COUNT(*) = 0, 'PASS', 'FAIL'),
       IF(COUNT(*) = 0, 'all 30 bilingual', CONCAT('incomplete: ', GROUP_CONCAT(t.template_code)))
FROM _pems_canonical_codes c
JOIN email_templates t ON t.template_code = c.template_code
WHERE t.subject_vi IS NULL OR t.subject_vi = '' OR t.body_vi IS NULL OR t.body_vi = ''
   OR t.subject_en IS NULL OR t.subject_en = '' OR t.body_en IS NULL OR t.body_en = '';


SELECT '── E. variables_text matches the placeholders actually used ────' AS ``;

-- Both sides expanded to rows, then compared as sets. {{actionBlock}} is excluded on the text side:
-- the backend injects it as trusted HTML and it is deliberately not an editable variable, so a
-- template that uses it must NOT list it.
DROP TEMPORARY TABLE IF EXISTS _pems_used_vars;
CREATE TEMPORARY TABLE _pems_used_vars (template_code VARCHAR(100), var_name VARCHAR(100)) ENGINE=InnoDB;

INSERT INTO _pems_used_vars (template_code, var_name)
WITH RECURSIVE scan AS (
  SELECT t.template_code,
         CONCAT(IFNULL(t.subject_vi,''), ' ', IFNULL(t.body_vi,''), ' ',
                IFNULL(t.subject_en,''), ' ', IFNULL(t.body_en,'')) AS rest,
         CAST(NULL AS CHAR(100)) AS var_name
  FROM email_templates t
  JOIN _pems_canonical_codes c ON c.template_code = t.template_code
  UNION ALL
  SELECT s.template_code,
         SUBSTRING(s.rest, LOCATE('}}', s.rest) + 2),
         TRIM(SUBSTRING(s.rest, LOCATE('{{', s.rest) + 2,
                        LOCATE('}}', s.rest) - LOCATE('{{', s.rest) - 2))
  FROM scan s
  WHERE LOCATE('{{', s.rest) > 0
    AND LOCATE('}}', s.rest) > LOCATE('{{', s.rest)
)
SELECT DISTINCT template_code, var_name
FROM scan
WHERE var_name IS NOT NULL AND var_name <> '' AND var_name <> 'actionBlock';

DROP TEMPORARY TABLE IF EXISTS _pems_listed_vars;
CREATE TEMPORARY TABLE _pems_listed_vars (template_code VARCHAR(100), var_name VARCHAR(100)) ENGINE=InnoDB;

INSERT INTO _pems_listed_vars (template_code, var_name)
WITH RECURSIVE split AS (
  SELECT t.template_code,
         CONCAT(IFNULL(t.variables_text, ''), ',') AS rest,
         CAST(NULL AS CHAR(100)) AS var_name
  FROM email_templates t
  JOIN _pems_canonical_codes c ON c.template_code = t.template_code
  UNION ALL
  SELECT s.template_code,
         SUBSTRING(s.rest, LOCATE(',', s.rest) + 1),
         TRIM(SUBSTRING(s.rest, 1, LOCATE(',', s.rest) - 1))
  FROM split s
  WHERE LOCATE(',', s.rest) > 0
)
SELECT DISTINCT template_code, var_name
FROM split
WHERE var_name IS NOT NULL AND var_name <> '';

INSERT INTO _pems_verify_results (check_id, check_name, verdict, detail)
SELECT 'E1', 'every placeholder used is listed in variables_text',
       IF(COUNT(*) = 0, 'PASS', 'FAIL'),
       IF(COUNT(*) = 0, 'no unlisted placeholder',
          CONCAT('unlisted: ', GROUP_CONCAT(CONCAT(u.template_code, '.', u.var_name))))
FROM _pems_used_vars u
LEFT JOIN _pems_listed_vars l
       ON l.template_code = u.template_code AND l.var_name = u.var_name
WHERE l.var_name IS NULL;

INSERT INTO _pems_verify_results (check_id, check_name, verdict, detail)
SELECT 'E2', 'every variable listed in variables_text is actually used',
       IF(COUNT(*) = 0, 'PASS', 'FAIL'),
       IF(COUNT(*) = 0, 'no orphan variable',
          CONCAT('unused: ', GROUP_CONCAT(CONCAT(l.template_code, '.', l.var_name))))
FROM _pems_listed_vars l
LEFT JOIN _pems_used_vars u
       ON u.template_code = l.template_code AND u.var_name = l.var_name
WHERE u.var_name IS NULL;

-- E3: placeholders are lower camelCase by contract. A PascalCase one silently renders empty.
INSERT INTO _pems_verify_results (check_id, check_name, verdict, detail)
SELECT 'E3', 'no PascalCase placeholder',
       IF(COUNT(*) = 0, 'PASS', 'FAIL'),
       IF(COUNT(*) = 0, 'all camelCase',
          CONCAT('offenders: ', GROUP_CONCAT(CONCAT(template_code, '.', var_name))))
FROM _pems_used_vars
-- 'c' forces a case-sensitive match. The column collation is case-insensitive, so a plain REGEXP
-- would match every name, and BINARY casts to the binary charset, which REGEXP_LIKE rejects.
WHERE REGEXP_LIKE(var_name, '^[A-Z]', 'c');

-- E4: no token/OTP/action-URL variable may be an editable template variable. Those are minted per
-- send and injected as {{actionBlock}}; making one editable would let a template move or forge it.
INSERT INTO _pems_verify_results (check_id, check_name, verdict, detail)
SELECT 'E4', 'no token/URL variable is editable content',
       IF(COUNT(*) = 0, 'PASS', 'FAIL'),
       IF(COUNT(*) = 0, 'none',
          CONCAT('offenders: ', GROUP_CONCAT(CONCAT(template_code, '.', var_name))))
FROM _pems_listed_vars
WHERE LOWER(var_name) IN ('actionblock','actionurl','token','rawtoken','confirmurl','reseturl','link','url');

SELECT u.template_code, COUNT(*) AS placeholders_used
FROM _pems_used_vars u GROUP BY u.template_code ORDER BY u.template_code;


SELECT '── F. Nothing depends on a numeric template id ─────────────────' AS ``;

-- The sync matches on template_code and never writes email_template_id. The observable consequence
-- is that every existing reference still resolves; an id-based upsert would have broken these.
INSERT INTO _pems_verify_results (check_id, check_name, verdict, detail)
SELECT 'F1', 'no sent_emails row references a missing template',
       IF(COUNT(*) = 0, 'PASS', 'FAIL'), CONCAT(COUNT(*), ' orphan reference(s)')
FROM sent_emails s
LEFT JOIN email_templates t ON t.email_template_id = s.email_template_id
WHERE s.email_template_id IS NOT NULL AND t.email_template_id IS NULL;

INSERT INTO _pems_verify_results (check_id, check_name, verdict, detail)
SELECT 'F2', 'no email_drafts row references a missing template',
       IF(COUNT(*) = 0, 'PASS', 'FAIL'), CONCAT(COUNT(*), ' orphan reference(s)')
FROM email_drafts d
LEFT JOIN email_templates t ON t.email_template_id = d.email_template_id
WHERE d.email_template_id IS NOT NULL AND t.email_template_id IS NULL;


SELECT '── G. History is intact ────────────────────────────────────────' AS ``;

-- The sync must never rewrite what was actually sent. A row whose subject or body_snapshot went
-- NULL would mean history was edited to match the new catalog.
INSERT INTO _pems_verify_results (check_id, check_name, verdict, detail)
SELECT 'G1', 'no sent_emails row lost its subject',
       IF(COUNT(*) = 0, 'PASS', 'FAIL'), CONCAT(COUNT(*), ' row(s) with empty subject')
FROM sent_emails WHERE subject IS NULL OR subject = '';

INSERT INTO _pems_verify_results (check_id, check_name, verdict, detail)
SELECT 'G2', 'no SENT row lost its body_snapshot',
       IF(COUNT(*) = 0, 'PASS', 'FAIL'), CONCAT(COUNT(*), ' SENT row(s) without a snapshot')
FROM sent_emails WHERE status IN ('SENT','DELIVERED') AND body_snapshot IS NULL;

-- A recipient row with no parent would mean history rows were deleted underneath it.
INSERT INTO _pems_verify_results (check_id, check_name, verdict, detail)
SELECT 'G3', 'no orphan recipient rows',
       IF(COUNT(*) = 0, 'PASS', 'FAIL'), CONCAT(COUNT(*), ' orphan recipient(s)')
FROM sent_email_recipients r
LEFT JOIN sent_emails s ON s.sent_email_id = r.sent_email_id
WHERE s.sent_email_id IS NULL;


SELECT '── H. Drafts survived ──────────────────────────────────────────' AS ``;

INSERT INTO _pems_verify_results (check_id, check_name, verdict, detail)
SELECT 'H1', 'no orphan draft recipient rows',
       IF(COUNT(*) = 0, 'PASS', 'FAIL'), CONCAT(COUNT(*), ' orphan draft recipient(s)')
FROM email_draft_recipients r
LEFT JOIN email_drafts d ON d.email_draft_id = r.email_draft_id
WHERE d.email_draft_id IS NULL;

SELECT status, COUNT(*) AS drafts FROM email_drafts GROUP BY status ORDER BY status;


SELECT '── I. Templates outside the catalog (informational) ────────────' AS ``;

-- Neither canonical nor legacy: operator-authored, or from a schema version this script has not
-- seen. The sync leaves these completely alone. They are listed, not judged — an ACTIVE one is not
-- a failure, but a person should know it is there.
SELECT t.template_code, t.status, t.purpose, t.created_at, t.updated_at
FROM email_templates t
LEFT JOIN _pems_canonical_codes c ON c.template_code = t.template_code
LEFT JOIN _pems_legacy_codes    l ON l.template_code = t.template_code
WHERE c.template_code IS NULL AND l.template_code IS NULL
ORDER BY t.template_code;

INSERT INTO _pems_verify_results (check_id, check_name, verdict, detail)
SELECT 'I1', 'templates outside the catalog (left untouched by design)', 'INFO',
       CONCAT(COUNT(*), ' unknown/operator-authored template(s)')
FROM email_templates t
LEFT JOIN _pems_canonical_codes c ON c.template_code = t.template_code
LEFT JOIN _pems_legacy_codes    l ON l.template_code = t.template_code
WHERE c.template_code IS NULL AND l.template_code IS NULL;


SELECT '── 9. Preservation checksums (compare before vs after) ─────────' AS ``;

-- Identical output from 01_preflight.sql and from here means the sync touched none of it.
CHECKSUM TABLE sent_emails, sent_email_recipients, sent_email_attachments,
               email_drafts, email_draft_recipients, email_action_tokens;

SELECT 'sent_emails'            AS scope, COUNT(*) AS rows_now FROM sent_emails
UNION ALL SELECT 'sent_email_recipients',  COUNT(*) FROM sent_email_recipients
UNION ALL SELECT 'sent_email_attachments', COUNT(*) FROM sent_email_attachments
UNION ALL SELECT 'email_drafts',           COUNT(*) FROM email_drafts
UNION ALL SELECT 'email_draft_recipients', COUNT(*) FROM email_draft_recipients
UNION ALL SELECT 'email_action_tokens',    COUNT(*) FROM email_action_tokens;


SELECT '── Z. Verdict ──────────────────────────────────────────────────' AS ``;

SELECT check_id, check_name, verdict, detail FROM _pems_verify_results ORDER BY seq;

SELECT
  SUM(verdict = 'PASS') AS passed,
  SUM(verdict = 'FAIL') AS failed,
  SUM(verdict = 'INFO') AS informational,
  IF(SUM(verdict = 'FAIL') = 0, 'VERIFY PASSED', 'VERIFY FAILED — see the FAIL rows above') AS result
FROM _pems_verify_results;

-- Make a failure non-ignorable: a script sourced in a terminal scrolls, and a person reads the last
-- screen. This raises an error so the exit status is non-zero and CI cannot mistake red for green.
DELIMITER $$
DROP PROCEDURE IF EXISTS pems_email_verify_gate$$
CREATE PROCEDURE pems_email_verify_gate()
BEGIN
  IF (SELECT COUNT(*) FROM _pems_verify_results WHERE verdict = 'FAIL') > 0 THEN
    SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT =
      'Email template sync verification FAILED. Read the FAIL rows above; do not proceed to deploy.';
  END IF;
END$$
DELIMITER ;
CALL pems_email_verify_gate();
DROP PROCEDURE pems_email_verify_gate;

DROP TEMPORARY TABLE IF EXISTS _pems_used_vars;
DROP TEMPORARY TABLE IF EXISTS _pems_listed_vars;
DROP TEMPORARY TABLE IF EXISTS _pems_canonical_codes;
DROP TEMPORARY TABLE IF EXISTS _pems_legacy_codes;
DROP TEMPORARY TABLE IF EXISTS _pems_verify_results;
