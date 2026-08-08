-- =====================================================================
-- 01_preflight.sql — read-only survey of the target database before any template sync
--
-- Run this FIRST, on the target database, and read the output before running 02_sync_templates.sql.
-- It writes nothing. Every statement here is a SELECT; there is no INSERT, UPDATE, DELETE, DDL or
-- transaction, so it is safe to run against anything you are allowed to connect to.
--
-- Usage (mysql client):
--     mysql -h <host> -u <user> -p <database> < 01_preflight.sql
--
-- What it answers, in order:
--   0. Which server and database am I actually connected to?
--   1. Does the email schema this sync depends on exist at all? (fail-closed)
--   2. Am I about to run against a protected database?
--   3. What is in email_templates right now — canonical, legacy, unknown, active, inactive?
--   4. What still references those templates from history and drafts?
--
-- Nothing here decides anything. It produces the numbers a person needs in order to decide.
-- =====================================================================

-- Read Vietnamese template content back as UTF-8. The mysql client on Windows otherwise decodes
-- it with the console codepage and every comparison below is made against mojibake.
SET NAMES utf8mb4;


SELECT '── 0. Connection ───────────────────────────────────────────────' AS ``;

SELECT
  DATABASE()        AS current_database,
  @@hostname        AS server_hostname,
  @@port            AS server_port,
  VERSION()         AS server_version,
  CURRENT_USER()    AS connected_as,
  NOW()             AS checked_at;


SELECT '── 1. Required schema (fail-closed) ────────────────────────────' AS ``;

-- Each required object reports present/MISSING. If anything says MISSING, stop: 02_sync_templates.sql
-- has its own guard and will refuse, but you want to know why here rather than there.
SELECT t.required_object,
       CASE WHEN i.table_name IS NULL THEN 'MISSING' ELSE 'present' END AS state
FROM (
  SELECT 'email_templates'         AS required_object UNION ALL
  SELECT 'sent_emails'                                UNION ALL
  SELECT 'sent_email_recipients'
) t
LEFT JOIN information_schema.tables i
       ON i.table_schema = DATABASE() AND i.table_name = t.required_object
ORDER BY state DESC, t.required_object;

-- Columns the sync writes. A missing one means this script is being run against an older schema.
SELECT c.required_column,
       CASE WHEN i.column_name IS NULL THEN 'MISSING' ELSE i.column_type END AS state
FROM (
  SELECT 'template_code'  AS required_column UNION ALL SELECT 'name'        UNION ALL
  SELECT 'purpose'                           UNION ALL SELECT 'campus_id'   UNION ALL
  SELECT 'description'                       UNION ALL SELECT 'status'      UNION ALL
  SELECT 'subject_vi'                        UNION ALL SELECT 'body_vi'     UNION ALL
  SELECT 'subject_en'                        UNION ALL SELECT 'body_en'     UNION ALL
  SELECT 'body_format'                       UNION ALL SELECT 'variables_text'
) c
LEFT JOIN information_schema.columns i
       ON i.table_schema = DATABASE() AND i.table_name = 'email_templates'
      AND i.column_name = c.required_column
ORDER BY state = 'MISSING' DESC, c.required_column;

-- template_code must be unique, or "upsert by code" is not a well-defined operation.
SELECT 'uq_email_templates_code' AS required_constraint,
       CASE WHEN COUNT(*) = 0 THEN 'MISSING — upsert by code is unsafe' ELSE 'present' END AS state
FROM information_schema.statistics
WHERE table_schema = DATABASE() AND table_name = 'email_templates'
  AND non_unique = 0 AND column_name = 'template_code';

-- The two foreign keys that make deleting a template dangerous.
SELECT constraint_name, table_name, column_name, referenced_table_name
FROM information_schema.key_column_usage
WHERE table_schema = DATABASE() AND referenced_table_name = 'email_templates'
ORDER BY table_name;


SELECT '── 2. Protected-database guard ─────────────────────────────────' AS ``;

-- Advisory here (this file writes nothing). 02_sync_templates.sql enforces it: it refuses to run
-- unless @pems_sync_confirm_database is set to the exact name of the database you intend to modify.
SELECT DATABASE() AS current_database,
       CASE
         WHEN DATABASE() IS NULL                THEN 'STOP — no database selected'
         WHEN DATABASE() = 'pems_db'            THEN 'CAUTION — this is the shared PEMS database'
         WHEN DATABASE() IN ('mysql','sys','information_schema','performance_schema')
                                                THEN 'STOP — server-internal database'
         ELSE 'ok — not a protected name'
       END AS verdict,
       'Set @pems_sync_confirm_database = ''<this exact name>'' before sourcing 02_sync_templates.sql'
         AS how_to_proceed;


SELECT '── 3. email_templates as it stands ─────────────────────────────' AS ``;

-- Classification. "canonical" = in the 31-code catalog this sync converges on; "legacy" = one of the
-- nine DL-03 codes the catalog dropped; "unknown" = anything else, which is either operator-authored
-- or from a schema version nobody here has seen. Unknown rows are never modified by the sync.
SELECT bucket, status, COUNT(*) AS templates
FROM (
  SELECT CASE
           WHEN template_code IN (
             'ACCOUNT_EMAIL_CONFIRMATION','ACCOUNT_PENDING_EMAIL_CHANGED_OLD_NOTICE','ACCOUNT_ACTIVATED',
             'ACCOUNT_EMAIL_CHANGED_OLD_NOTICE','ACCOUNT_EMAIL_CHANGED_NEW_NOTICE','ACCOUNT_ROLE_CHANGED',
             'ACCOUNT_STAFF_LEADER_ASSIGNED','ACCOUNT_STAFF_LEADER_REPLACED',
             'DEPT_PERSONNEL_ACCOUNT_DISABLED','DEPT_PERSONNEL_ACCOUNT_ENABLED',
             'DEPT_LEADERSHIP_GRANTED','DEPT_LEADERSHIP_HANDED_OVER',
             'AUTH_PASSWORD_RESET_OTP',
             'VISIT_REQUEST_OTP','VISIT_CONTACT_CLAIM','VISIT_CONTACT_TRANSFER',
             'VISIT_CAMPUS_REJECTED','VISIT_CONTACT_INVITATION_EXPIRED',
             'VISIT_PARTICIPANT_INVITATION','VISIT_STUDENT_INVITATION',
             'VISIT_DEPARTMENT_LEADER_INVITATION','VISIT_DEPARTMENT_STAFF_ASSIGNMENT',
             'VISIT_REMINDER_HOST','VISIT_REMINDER_PARTICIPANTS',
             'LOGISTICS_REQUEST_TO_DEPARTMENT','LOGISTICS_ASSIGNEE_ASSIGNMENT',
             'LOGISTICS_CHANGE_PROPOSAL_TO_HOST','LOGISTICS_EXPENSE_REPORT_REMINDER',
             'VISIT_SETUP_PROGRESS_UPDATE',
             'REPORT_CAMPUS_OPERATION','REPORT_DEPARTMENT_COLLABORATION',
             'REPORT_DEPARTMENT_INVOICE','REPORT_PERSONNEL_PERFORMANCE')
             THEN 'canonical'
           WHEN template_code IN (
             'ACCOUNT_CREATED_INTERNAL','VISIT_REQUEST_APPROVED','VISIT_REQUEST_REJECTED',
             'VISIT_CANCELLED','HOST_ASSIGNMENT','VISIT_REQUEST_SUBMITTED_NOTIFY',
             'LOGISTICS_REQUEST','LOGISTICS_REQUEST_SUBMITTED_NOTIFY','OTP_VISIT_REQUEST')
             THEN 'legacy (DL-03)'
           ELSE 'unknown / operator-authored'
         END AS bucket,
         status
  FROM email_templates
) x
GROUP BY bucket, status
ORDER BY bucket, status;

-- Which of the 31 canonical codes this database is missing entirely (the sync will INSERT these).
SELECT c.template_code AS canonical_code_absent_here
FROM (
  SELECT 'ACCOUNT_EMAIL_CONFIRMATION' AS template_code UNION ALL SELECT 'ACCOUNT_PENDING_EMAIL_CHANGED_OLD_NOTICE' UNION ALL
  SELECT 'ACCOUNT_ACTIVATED'                           UNION ALL SELECT 'ACCOUNT_EMAIL_CHANGED_OLD_NOTICE'         UNION ALL
  SELECT 'ACCOUNT_EMAIL_CHANGED_NEW_NOTICE'            UNION ALL SELECT 'ACCOUNT_ROLE_CHANGED'                     UNION ALL
  SELECT 'ACCOUNT_STAFF_LEADER_ASSIGNED'               UNION ALL SELECT 'ACCOUNT_STAFF_LEADER_REPLACED'            UNION ALL
  SELECT 'DEPT_PERSONNEL_ACCOUNT_DISABLED'             UNION ALL SELECT 'DEPT_PERSONNEL_ACCOUNT_ENABLED'           UNION ALL
  SELECT 'DEPT_LEADERSHIP_GRANTED'                     UNION ALL SELECT 'DEPT_LEADERSHIP_HANDED_OVER'              UNION ALL
  SELECT 'AUTH_PASSWORD_RESET_OTP'                     UNION ALL SELECT 'VISIT_REQUEST_OTP'                        UNION ALL
  SELECT 'VISIT_CONTACT_CLAIM'                         UNION ALL SELECT 'VISIT_CONTACT_TRANSFER'                   UNION ALL
  SELECT 'VISIT_CAMPUS_REJECTED'                       UNION ALL SELECT 'VISIT_CONTACT_INVITATION_EXPIRED'       UNION ALL
  SELECT 'VISIT_PARTICIPANT_INVITATION'                UNION ALL SELECT 'VISIT_STUDENT_INVITATION'                 UNION ALL
  SELECT 'VISIT_DEPARTMENT_LEADER_INVITATION'          UNION ALL SELECT 'VISIT_DEPARTMENT_STAFF_ASSIGNMENT'        UNION ALL
  SELECT 'VISIT_REMINDER_HOST'                         UNION ALL SELECT 'VISIT_REMINDER_PARTICIPANTS'              UNION ALL
  SELECT 'LOGISTICS_REQUEST_TO_DEPARTMENT'             UNION ALL SELECT 'LOGISTICS_ASSIGNEE_ASSIGNMENT'            UNION ALL
  SELECT 'LOGISTICS_CHANGE_PROPOSAL_TO_HOST'           UNION ALL SELECT 'LOGISTICS_EXPENSE_REPORT_REMINDER'        UNION ALL
  SELECT 'VISIT_SETUP_PROGRESS_UPDATE'                 UNION ALL SELECT 'REPORT_CAMPUS_OPERATION'                  UNION ALL
  SELECT 'REPORT_DEPARTMENT_COLLABORATION'             UNION ALL SELECT 'REPORT_DEPARTMENT_INVOICE'                UNION ALL
  SELECT 'REPORT_PERSONNEL_PERFORMANCE'
) c
LEFT JOIN email_templates t ON t.template_code = c.template_code
WHERE t.email_template_id IS NULL
ORDER BY c.template_code;

-- Content problems that already exist. The sync fixes these for canonical codes by overwriting them;
-- for unknown codes it does not, so anything listed here under an unknown code stays as it is.
SELECT template_code, status,
       (subject_vi IS NULL OR subject_vi = '') AS missing_subject_vi,
       (body_vi    IS NULL OR body_vi    = '') AS missing_body_vi,
       (subject_en IS NULL OR subject_en = '') AS missing_subject_en,
       (body_en    IS NULL OR body_en    = '') AS missing_body_en
FROM email_templates
WHERE subject_vi IS NULL OR subject_vi = '' OR body_vi IS NULL OR body_vi = ''
   OR subject_en IS NULL OR subject_en = '' OR body_en IS NULL OR body_en = ''
ORDER BY template_code;

-- Duplicate codes. Should be impossible given the unique key; if the key is missing this finds the mess.
SELECT template_code, COUNT(*) AS rows_with_this_code
FROM email_templates
GROUP BY template_code
HAVING COUNT(*) > 1;


SELECT '── 4. What references these templates ──────────────────────────' AS ``;

SELECT 'sent_emails total'                       AS metric, COUNT(*) AS value FROM sent_emails
UNION ALL SELECT 'sent_emails with template FK',  COUNT(*) FROM sent_emails WHERE email_template_id IS NOT NULL
UNION ALL SELECT 'sent_emails with body_snapshot',COUNT(*) FROM sent_emails WHERE body_snapshot IS NOT NULL
UNION ALL SELECT 'sent_email_recipients total',   COUNT(*) FROM sent_email_recipients
UNION ALL SELECT 'sent_email_recipients BCC',     COUNT(*) FROM sent_email_recipients WHERE recipient_type = 'BCC';

-- Templates that history still points at. Deleting any of these would orphan a reference, which is
-- exactly why the sync deactivates legacy codes instead of removing them.
SELECT t.template_code, t.status,
       COUNT(DISTINCT s.sent_email_id)  AS referencing_sent_emails
FROM email_templates t
LEFT JOIN sent_emails  s ON s.email_template_id = t.email_template_id
GROUP BY t.template_code, t.status
HAVING referencing_sent_emails > 0
ORDER BY referencing_sent_emails DESC, t.template_code;


SELECT '── 5. Preservation checksums (record these) ────────────────────' AS ``;

-- 03_verify.sql prints the same six checksums after the sync. Identical output proves the sync
-- touched none of these tables. Different output is not automatically wrong — a live system keeps
-- sending mail while you work — but it is something to explain rather than skip past.
CHECKSUM TABLE sent_emails, sent_email_recipients, sent_email_attachments, email_action_tokens;


SELECT '── preflight complete — nothing was written ────────────────────' AS ``;
