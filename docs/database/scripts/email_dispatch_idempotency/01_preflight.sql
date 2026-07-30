-- =====================================================================
-- 01_preflight.sql — read-only survey before adding the email send-idempotency table
--
-- Run this FIRST, on the target database, and read the output before running 02_up_additive.sql.
-- It writes nothing: every statement is a SELECT. No INSERT, UPDATE, DELETE, DDL or transaction.
--
-- Usage (mysql client):
--     mysql -h <host> -u <user> -p <database> < 01_preflight.sql
--
-- What it answers, in order:
--   0. Which server and database am I actually connected to?
--   1. Do the tables the new foreign keys point at exist? (fail-closed)
--   2. Am I about to run against a protected database?
--   3. Does email_send_idempotency already exist, and if so does it match?
--   4. What would the migration change?
--
-- Nothing here decides anything. It produces the numbers a person needs in order to decide.
-- =====================================================================

SELECT '── 0. Connection ───────────────────────────────────────────────' AS ``;

SELECT
  DATABASE()        AS current_database,
  @@hostname        AS server_hostname,
  @@port            AS server_port,
  VERSION()         AS server_version,
  CURRENT_USER()    AS connected_as,
  NOW()             AS checked_at;


SELECT '── 1. Referenced schema (fail-closed) ──────────────────────────' AS ``;

-- The new table has two foreign keys. Both parents must already exist, with the column types the
-- constraints require — a BIGINT UNSIGNED child column cannot reference a signed parent.
SELECT t.required_object,
       CASE WHEN i.table_name IS NULL THEN 'MISSING' ELSE 'present' END AS state
FROM (
  SELECT 'users'       AS required_object UNION ALL
  SELECT 'sent_emails'
) t
LEFT JOIN information_schema.tables i
       ON i.table_schema = DATABASE() AND i.table_name = t.required_object
ORDER BY state DESC, t.required_object;

SELECT c.parent_table, c.parent_column,
       COALESCE(i.column_type, 'MISSING') AS actual_type,
       CASE
         WHEN i.column_type IS NULL THEN 'MISSING'
         WHEN i.column_type LIKE 'bigint%unsigned%' THEN 'compatible'
         ELSE 'INCOMPATIBLE — foreign key would be refused'
       END AS verdict
FROM (
  SELECT 'users'       AS parent_table, 'user_id'       AS parent_column UNION ALL
  SELECT 'sent_emails' AS parent_table, 'sent_email_id' AS parent_column
) c
LEFT JOIN information_schema.columns i
       ON i.table_schema = DATABASE() AND i.table_name = c.parent_table
      AND i.column_name = c.parent_column;


SELECT '── 2. Protected database check ─────────────────────────────────' AS ``;

-- Advisory only. 02_up_additive.sql refuses outright unless you name the target explicitly.
SELECT DATABASE() AS current_database,
       CASE
         WHEN DATABASE() IS NULL     THEN 'NO DATABASE SELECTED — connect with one'
         WHEN DATABASE() = 'pems_db' THEN 'PROTECTED — this is the deployed database. Migrate it only under an approved change window.'
         ELSE 'not a protected name'
       END AS verdict;


SELECT '── 3. Does the table already exist? ────────────────────────────' AS ``;

SELECT CASE WHEN COUNT(*) = 0
            THEN 'absent — 02_up_additive.sql will create it'
            ELSE 'present — 02_up_additive.sql will make no change'
       END AS email_send_idempotency_state
FROM information_schema.tables
WHERE table_schema = DATABASE() AND table_name = 'email_send_idempotency';

-- When it already exists, show the shape so a mismatch is visible before the verify step names it.
SELECT column_name, column_type, is_nullable, column_default
FROM information_schema.columns
WHERE table_schema = DATABASE() AND table_name = 'email_send_idempotency'
ORDER BY ordinal_position;

SELECT index_name, non_unique, GROUP_CONCAT(column_name ORDER BY seq_in_index) AS columns
FROM information_schema.statistics
WHERE table_schema = DATABASE() AND table_name = 'email_send_idempotency'
GROUP BY index_name, non_unique
ORDER BY index_name;

SELECT constraint_name, referenced_table_name,
       GROUP_CONCAT(column_name ORDER BY ordinal_position) AS columns
FROM information_schema.key_column_usage
WHERE table_schema = DATABASE() AND table_name = 'email_send_idempotency'
  AND referenced_table_name IS NOT NULL
GROUP BY constraint_name, referenced_table_name
ORDER BY constraint_name;


SELECT '── 4. Existing rows (only meaningful on a re-run) ──────────────' AS ``;

-- On a first run the table is absent and there is nothing to summarise; on a re-run these rows are the
-- audit trail the rollback guidance protects — what was reserved, and what was actually sent.
-- Prepared rather than written literally: a plain SELECT would abort the whole script on a first run.
SET @has_idem_table := (
  SELECT COUNT(*) FROM information_schema.tables
  WHERE table_schema = DATABASE() AND table_name = 'email_send_idempotency');

SET @idem_rows_sql := IF(@has_idem_table = 0,
  "SELECT 'table absent — nothing to summarise' AS note",
  "SELECT state, COUNT(*) AS rows_in_state, MIN(created_at) AS oldest, MAX(created_at) AS newest
     FROM email_send_idempotency GROUP BY state ORDER BY state");

PREPARE pems_idem_preflight FROM @idem_rows_sql;
EXECUTE pems_idem_preflight;
DEALLOCATE PREPARE pems_idem_preflight;


SELECT '── Preflight complete. Nothing was written. ────────────────────' AS ``;
