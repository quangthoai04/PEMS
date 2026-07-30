-- =====================================================================
-- 03_verify.sql — proves the send-idempotency migration landed correctly
--
-- Run AFTER 02_up_additive.sql, on the same database:
--     mysql -h <host> -u <user> -p <database> < 03_verify.sql
--
-- Read-only against business data. It creates and drops one TEMPORARY table to collect results, and it
-- ENDS BY FAILING when any check failed — a non-zero exit code, not a table a tired person can skim
-- past. A verification you can accidentally ignore is not a verification.
-- =====================================================================

DROP TEMPORARY TABLE IF EXISTS _pems_idem_verify;
CREATE TEMPORARY TABLE _pems_idem_verify (
  seq      INT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,
  check_id VARCHAR(60)  NOT NULL,
  verdict  VARCHAR(8)   NOT NULL,
  detail   VARCHAR(400) NOT NULL
) ENGINE=MEMORY;


-- ── 1. The table exists ───────────────────────────────────────────────
INSERT INTO _pems_idem_verify (check_id, verdict, detail)
SELECT 'table-exists',
       IF(COUNT(*) = 1, 'PASS', 'FAIL'),
       CONCAT('email_send_idempotency present: ', COUNT(*))
FROM information_schema.tables
WHERE table_schema = DATABASE() AND table_name = 'email_send_idempotency';


-- ── 2. Every column the backend writes is present, with the right type ─
INSERT INTO _pems_idem_verify (check_id, verdict, detail)
SELECT CONCAT('column-', e.column_name),
       CASE
         WHEN a.column_name IS NULL THEN 'FAIL'
         WHEN a.column_type <> e.expected_type THEN 'FAIL'
         WHEN a.is_nullable <> e.expected_null THEN 'FAIL'
         ELSE 'PASS'
       END,
       CONCAT('expected ', e.expected_type, ' null=', e.expected_null,
              ' · actual ', COALESCE(a.column_type, 'MISSING'), ' null=', COALESCE(a.is_nullable, '-'))
FROM (
  SELECT 'email_send_idempotency_id' AS column_name, 'bigint unsigned' AS expected_type, 'NO'  AS expected_null UNION ALL
  SELECT 'actor_user_id',                            'bigint unsigned',                 'NO'  UNION ALL
  SELECT 'operation_code',                           'varchar(64)',                     'NO'  UNION ALL
  SELECT 'idempotency_key_hash',                     'char(64)',                        'NO'  UNION ALL
  SELECT 'request_fingerprint',                      'char(64)',                        'NO'  UNION ALL
  SELECT 'state',                                    'varchar(32)',                     'NO'  UNION ALL
  SELECT 'sent_email_id',                            'bigint unsigned',                 'YES' UNION ALL
  SELECT 'result_message',                           'varchar(500)',                    'YES' UNION ALL
  SELECT 'failure_code',                             'varchar(64)',                     'YES' UNION ALL
  SELECT 'attempt_count',                            'int unsigned',                    'NO'  UNION ALL
  SELECT 'created_at',                               'datetime',                        'NO'  UNION ALL
  SELECT 'updated_at',                               'datetime',                        'NO'  UNION ALL
  SELECT 'dispatch_started_at',                      'datetime',                        'YES' UNION ALL
  SELECT 'completed_at',                             'datetime',                        'YES'
) e
LEFT JOIN information_schema.columns a
       ON a.table_schema = DATABASE() AND a.table_name = 'email_send_idempotency'
      AND a.column_name = e.column_name;


-- ── 3. The unique constraint — the whole concurrency contract ─────────
-- Exactly (actor_user_id, operation_code, idempotency_key_hash), in that order, non-unique = 0.
INSERT INTO _pems_idem_verify (check_id, verdict, detail)
SELECT 'unique-actor-op-key',
       IF(COUNT(*) = 1, 'PASS', 'FAIL'),
       CONCAT('matching unique indexes: ', COUNT(*), ' (expected exactly 1)')
FROM (
  SELECT index_name
  FROM information_schema.statistics
  WHERE table_schema = DATABASE() AND table_name = 'email_send_idempotency' AND non_unique = 0
  GROUP BY index_name
  HAVING GROUP_CONCAT(column_name ORDER BY seq_in_index)
         = 'actor_user_id,operation_code,idempotency_key_hash'
) m;


-- ── 4. Foreign keys, with the delete behaviour audit depends on ───────
INSERT INTO _pems_idem_verify (check_id, verdict, detail)
SELECT CONCAT('fk-', e.constraint_name),
       CASE
         WHEN a.constraint_name IS NULL THEN 'FAIL'
         WHEN a.delete_rule <> e.expected_delete THEN 'FAIL'
         ELSE 'PASS'
       END,
       CONCAT('expected ON DELETE ', e.expected_delete,
              ' · actual ', COALESCE(a.delete_rule, 'MISSING'))
FROM (
  SELECT 'fk_email_send_idempotency_actor'      AS constraint_name, 'RESTRICT' AS expected_delete UNION ALL
  SELECT 'fk_email_send_idempotency_sent_email',                    'SET NULL'
) e
LEFT JOIN information_schema.referential_constraints a
       ON a.constraint_schema = DATABASE() AND a.table_name = 'email_send_idempotency'
      AND a.constraint_name = e.constraint_name;


-- ── 5. The state CHECK constraint really constrains ───────────────────
-- Its presence matters more than its text: without it a typo'd state would be stored silently and the
-- replay logic would treat an unknown value as "not succeeded" forever.
INSERT INTO _pems_idem_verify (check_id, verdict, detail)
SELECT 'check-state-enumerated',
       IF(COUNT(*) = 1, 'PASS', 'FAIL'),
       CONCAT('chk_email_send_idempotency_state present: ', COUNT(*))
FROM information_schema.table_constraints
WHERE constraint_schema = DATABASE() AND table_name = 'email_send_idempotency'
  AND constraint_type = 'CHECK'
  AND constraint_name = 'chk_email_send_idempotency_state';


-- ── 6. No row is in a state the backend cannot read ───────────────────
INSERT INTO _pems_idem_verify (check_id, verdict, detail)
SELECT 'rows-state-known',
       IF(COUNT(*) = 0, 'PASS', 'FAIL'),
       CONCAT('rows with an unrecognised state: ', COUNT(*))
FROM email_send_idempotency
WHERE state NOT IN ('RESERVED','PREPARING','DISPATCHING','SUCCEEDED',
                    'FAILED_BEFORE_DISPATCH','OUTCOME_UNKNOWN');


-- ── 7. Success rows are internally consistent ─────────────────────────
INSERT INTO _pems_idem_verify (check_id, verdict, detail)
SELECT 'succeeded-has-completion',
       IF(COUNT(*) = 0, 'PASS', 'FAIL'),
       CONCAT('SUCCEEDED rows with no completed_at: ', COUNT(*))
FROM email_send_idempotency
WHERE state = 'SUCCEEDED' AND completed_at IS NULL;

INSERT INTO _pems_idem_verify (check_id, verdict, detail)
SELECT 'succeeded-passed-dispatch',
       IF(COUNT(*) = 0, 'PASS', 'FAIL'),
       CONCAT('SUCCEEDED rows that never recorded a dispatch start: ', COUNT(*))
FROM email_send_idempotency
WHERE state = 'SUCCEEDED' AND dispatch_started_at IS NULL;

-- A row that says nothing was dispatched must not carry a dispatch timestamp. This is the invariant
-- the retry rule rests on: FAILED_BEFORE_DISPATCH is the ONLY state a same-key retry may resume from.
INSERT INTO _pems_idem_verify (check_id, verdict, detail)
SELECT 'failed-before-dispatch-is-clean',
       IF(COUNT(*) = 0, 'PASS', 'FAIL'),
       CONCAT('FAILED_BEFORE_DISPATCH rows carrying a dispatch timestamp: ', COUNT(*))
FROM email_send_idempotency
WHERE state = 'FAILED_BEFORE_DISPATCH' AND dispatch_started_at IS NOT NULL;


-- ── 8. The record stores hashes, not secrets ──────────────────────────
-- 64 lower-case hex characters, both columns. A value of any other shape means something other than a
-- SHA-256 was written — the raw key, for instance.
INSERT INTO _pems_idem_verify (check_id, verdict, detail)
SELECT 'key-hash-is-a-hash',
       IF(COUNT(*) = 0, 'PASS', 'FAIL'),
       CONCAT('rows whose idempotency_key_hash is not 64 hex chars: ', COUNT(*))
FROM email_send_idempotency
WHERE idempotency_key_hash NOT REGEXP '^[0-9a-f]{64}$';

INSERT INTO _pems_idem_verify (check_id, verdict, detail)
SELECT 'fingerprint-is-a-hash',
       IF(COUNT(*) = 0, 'PASS', 'FAIL'),
       CONCAT('rows whose request_fingerprint is not 64 hex chars: ', COUNT(*))
FROM email_send_idempotency
WHERE request_fingerprint NOT REGEXP '^[0-9a-f]{64}$';


-- ── 9. Nothing outside this table was touched ─────────────────────────
-- Informational: the migration creates one table and no other object. If these counts moved between
-- the preflight and now, something other than this script ran.
INSERT INTO _pems_idem_verify (check_id, verdict, detail)
SELECT 'scope-email-templates', 'INFO', CONCAT('email_templates rows: ', COUNT(*)) FROM email_templates;

INSERT INTO _pems_idem_verify (check_id, verdict, detail)
SELECT 'scope-sent-emails', 'INFO', CONCAT('sent_emails rows: ', COUNT(*)) FROM sent_emails;

INSERT INTO _pems_idem_verify (check_id, verdict, detail)
SELECT 'scope-sent-email-attachments', 'INFO',
       CONCAT('sent_email_attachments rows: ', COUNT(*)) FROM sent_email_attachments;


-- ── Results ──────────────────────────────────────────────────────────
SELECT seq, check_id, verdict, detail FROM _pems_idem_verify ORDER BY seq;

SELECT
  SUM(verdict = 'PASS') AS passed,
  SUM(verdict = 'FAIL') AS failed,
  SUM(verdict = 'INFO') AS informational
FROM _pems_idem_verify;


-- ── The gate ─────────────────────────────────────────────────────────
-- Everything above is readable output. This is the part that cannot be skimmed past.

DROP PROCEDURE IF EXISTS pems_idem_verify_gate;

DELIMITER $

CREATE PROCEDURE pems_idem_verify_gate()
BEGIN
  IF (SELECT COUNT(*) FROM _pems_idem_verify WHERE verdict = 'FAIL') > 0 THEN
    SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT =
      'Send-idempotency verification FAILED. Read the FAIL rows above; do not deploy the backend against this database.';
  END IF;
END$

DELIMITER ;

CALL pems_idem_verify_gate();
DROP PROCEDURE pems_idem_verify_gate;

DROP TEMPORARY TABLE _pems_idem_verify;

SELECT 'Send-idempotency verification passed.' AS result;
