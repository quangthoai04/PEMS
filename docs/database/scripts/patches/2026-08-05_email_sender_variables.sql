-- ---------------------------------------------------------------------------
-- 2026-08-05 — Replace the reply-contact block with sender template variables
-- ---------------------------------------------------------------------------
--
-- WHAT THIS DOES
--   1. Rewrites every stored body that still carries {{contactInformationBlock}}
--      so it prints the SENDER instead — name, role and address, as ordinary
--      template variables.
--   2. Appends the six {{sender*}} names to variables_text on the 28 templates
--      whose capability permits them, so the renderer's declared-vs-supplied
--      check passes. The three credential-bearing templates are left alone.
--
-- WHY variables_text MATTERS MORE THAN THE BODIES
--   The renderer reads the DECLARED variable list from this column, not from
--   the code registry, and refuses any send whose supplied set does not match
--   it exactly. From the deploy of this change the dispatcher supplies the six
--   sender values to every capable template — so a database that has the new
--   code and the old column fails EVERY send of those 28 templates with
--   EMAIL_TEMPLATE_VARIABLE_UNKNOWN. Section 2 is therefore not cosmetic and
--   not optional; run this BEFORE or WITH the deploy, never after.
--
-- WHAT IT DELIBERATELY DOES NOT DO
--   It does not drop email_contact_policies. No code reads or writes that table
--   any more, which is what makes dropping it safe — but dropping schema is a
--   separate, separately-approved task, and a patch that removes a table cannot
--   be rolled back by re-running anything.
--
--   It also does not deliver the §16 CONTENT REWRITE (2026-08-05). Every one of
--   the 31 templates was given a new subject and body that day — one structure
--   across the catalogue: greeting, summary table, facts, action area, security
--   note, sender card, footer. Section 1 below swaps a placeholder inside prose
--   an operator may have edited and leaves the rest of their wording alone,
--   which is the right behaviour for a migration and the wrong mechanism for a
--   rewrite. The new content reaches a deployed database through the catalogue
--   sync (email_template_cc_bcc_sync/02_sync_templates.sql), or through a fresh
--   import of the canonical seed. Running this patch and stopping there leaves
--   the deployment on the old wording, correctly and on purpose.
--
-- IDEMPOTENT: every statement is guarded. Running it twice changes nothing the
-- second time. Safe on a fresh import of PEMS_FULL_VS_31_07_NEW.sql (which
-- already ships the new content — every guard simply matches nothing).
--
-- RUN IT AS:
--   mysql --default-character-set=utf8mb4 -u root -p pems_db < this_file.sql
--
--   The charset flag is REQUIRED. Without it the client negotiates latin1, and
--   every Vietnamese string this patch writes is stored double-encoded — the
--   rows look updated, the counts look right, and every affected mail goes out
--   mojibaked. There is no way to tell from the verification queries below.
-- ---------------------------------------------------------------------------

-- ── 1. Bodies: the contact block becomes a sender signature ─────────────────
--
-- REPLACE() rather than a per-template UPDATE with the full new body: the
-- placeholder sits inside prose an operator may legitimately have edited, and
-- overwriting the whole column would silently discard their wording. This
-- swaps the block and leaves everything around it exactly as it is.
--
-- A body that no longer contains the placeholder is not matched, so an operator
-- who had already deleted it keeps their version.

UPDATE email_templates
SET body_vi = REPLACE(
      body_vi,
      '{{contactInformationBlock}}',
      '<div style="margin:20px 0;padding:14px 18px;background:#f9fafb;border-left:3px solid #004c91;border-radius:8px"><p style="margin:0 0 6px;font-size:12px;color:#6b7280;letter-spacing:.04em">NGƯỜI GỬI</p><p style="margin:0;line-height:1.7"><strong>{{senderName}}</strong><br/>{{senderRole}}<br/>{{senderEmail}}</p></div>')
WHERE body_vi LIKE '%{{contactInformationBlock}}%';

UPDATE email_templates
SET body_en = REPLACE(
      body_en,
      '{{contactInformationBlock}}',
      '<div style="margin:20px 0;padding:14px 18px;background:#f9fafb;border-left:3px solid #004c91;border-radius:8px"><p style="margin:0 0 6px;font-size:12px;color:#6b7280;letter-spacing:.04em">SENDER</p><p style="margin:0;line-height:1.7"><strong>{{senderName}}</strong><br/>{{senderRole}}<br/>{{senderEmail}}</p></div>')
WHERE body_en LIKE '%{{contactInformationBlock}}%';

-- The subject is checked too. No shipped subject has ever carried the block —
-- the renderer refuses one that does — but a hand-edited row could, and leaving
-- it there would fail every send with an unresolved placeholder.
UPDATE email_templates
SET subject_vi = REPLACE(subject_vi, '{{contactInformationBlock}}', '')
WHERE subject_vi LIKE '%{{contactInformationBlock}}%';

UPDATE email_templates
SET subject_en = REPLACE(subject_en, '{{contactInformationBlock}}', '')
WHERE subject_en LIKE '%{{contactInformationBlock}}%';

-- ── 2. variables_text: declare the sender variables ─────────────────────────
--
-- Everything EXCEPT the three templates whose message is a one-time credential.
-- Expressed as an exclusion rather than a list of 28 codes so that a template
-- added to the catalog later is covered by default — the safe direction, since
-- a missing declaration breaks sends and a spare one is inert.
--
-- The NOT LIKE guard is what makes this idempotent. It tests for 'senderName'
-- specifically rather than for the whole appended string: a partially-applied
-- run (interrupted between statements) leaves rows in one of only two states,
-- and this recognises both.

UPDATE email_templates
SET variables_text = CASE
      WHEN variables_text IS NULL OR variables_text = ''
        THEN 'senderName,senderRole,senderEmail,senderPhone,senderDepartment,senderCampus'
      ELSE CONCAT(variables_text, ',senderName,senderRole,senderEmail,senderPhone,senderDepartment,senderCampus')
    END
WHERE template_code NOT IN (
        'ACCOUNT_EMAIL_CONFIRMATION',
        'AUTH_PASSWORD_RESET_OTP',
        'VISIT_REQUEST_OTP')
  AND (variables_text IS NULL OR variables_text NOT LIKE '%senderName%');

-- …and the other direction: a credential-bearing template must NOT declare them.
-- Only reachable if somebody ran an earlier draft of this patch without the
-- exclusion list, but a declared-and-never-supplied variable fails every send,
-- so it is worth undoing rather than assuming it never happened.
UPDATE email_templates
SET variables_text = NULLIF(TRIM(BOTH ',' FROM REPLACE(
      REPLACE(CONCAT(',', variables_text, ','),
              ',senderName,senderRole,senderEmail,senderPhone,senderDepartment,senderCampus,', ','),
      ',,', ',')), '')
WHERE template_code IN (
        'ACCOUNT_EMAIL_CONFIRMATION',
        'AUTH_PASSWORD_RESET_OTP',
        'VISIT_REQUEST_OTP')
  AND variables_text LIKE '%senderName%';

-- ── 3. Verification ─────────────────────────────────────────────────────────
-- Expected: block_left = 0, missing_sender_vars = 0, wrongly_declared = 0.

SELECT
  (SELECT COUNT(*) FROM email_templates
     WHERE body_vi LIKE '%{{contactInformationBlock}}%'
        OR body_en LIKE '%{{contactInformationBlock}}%'
        OR subject_vi LIKE '%{{contactInformationBlock}}%'
        OR subject_en LIKE '%{{contactInformationBlock}}%')            AS block_left,

  (SELECT COUNT(*) FROM email_templates
     WHERE template_code NOT IN ('ACCOUNT_EMAIL_CONFIRMATION',
                                 'AUTH_PASSWORD_RESET_OTP',
                                 'VISIT_REQUEST_OTP')
       AND (variables_text IS NULL OR variables_text NOT LIKE '%senderName%')) AS missing_sender_vars,

  (SELECT COUNT(*) FROM email_templates
     WHERE template_code IN ('ACCOUNT_EMAIL_CONFIRMATION',
                             'AUTH_PASSWORD_RESET_OTP',
                             'VISIT_REQUEST_OTP')
       AND variables_text LIKE '%senderName%')                          AS wrongly_declared;

-- A Vietnamese spot-check. If this prints mojibake, the client charset was
-- wrong (see the header) — restore from backup and re-run with utf8mb4 rather
-- than trying to repair the rows in place.
SELECT template_code, SUBSTRING(body_vi, LOCATE('NGƯỜI GỬI', body_vi), 40) AS sender_heading
FROM email_templates
WHERE body_vi LIKE '%NGƯỜI GỬI%'
ORDER BY template_code
LIMIT 3;
