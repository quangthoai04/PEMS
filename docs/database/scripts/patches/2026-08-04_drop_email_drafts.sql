-- ---------------------------------------------------------------------------
-- Drop the email draft schema.
--
-- The compose screen no longer persists a draft: a message lives in the browser
-- until it is sent, and the three send paths (manual compose, reply, the Host's
-- setup-progress update) post the whole message. Protection against a double
-- click moved from the atomic DRAFT -> SENT claim to `Idempotency-Key`, whose
-- reservations live in `email_send_idempotency` and are NOT touched here.
--
-- WHY THE ROWS ARE NOT MIGRATED. An email_drafts row is unsent work in progress
-- — by definition nothing was delivered from it, and there is no destination
-- table for "a message somebody was still writing". Every row that ever became
-- an email already has its own `sent_emails` record, which this script leaves
-- alone: the FK from email_drafts to sent_emails points the other way, so no
-- history row depends on a draft.
--
-- Files that were attached to an unsent draft stay in `files`. They are the
-- author's own uploads and are still readable by them through the uploader
-- fallback in FileAccessAuthorizationService; only the draft's claim on them
-- goes. Deleting them here would destroy documents on the strength of a schema
-- change.
--
-- SAFE TO RE-RUN. Every statement is IF EXISTS, and the drop order is
-- children-then-parent so the two CASCADE foreign keys never block it.
--
-- Run this against a database that already carries the draft tables. A fresh
-- import of the canonical script does not need it — those tables are gone from
-- PEMS_FULL_VS_31_07_NEW.sql as of the same change.
-- ---------------------------------------------------------------------------

-- Reports what is about to go, so the operator sees the cost before it is paid
-- rather than after. Zero rows is the expected answer on a demo database.
SELECT 'email_drafts (pending)'            AS what, COUNT(*) AS rows_affected FROM email_drafts
UNION ALL
SELECT 'email_draft_recipients (pending)', COUNT(*) FROM email_draft_recipients
UNION ALL
SELECT 'email_draft_attachments (pending)', COUNT(*) FROM email_draft_attachments;

-- Children first: both carry ON DELETE CASCADE to email_drafts, and dropping the
-- parent while a child's FK still references it fails with errno 150.
DROP TABLE IF EXISTS `email_draft_attachments`;
DROP TABLE IF EXISTS `email_draft_recipients`;
DROP TABLE IF EXISTS `email_drafts`;

-- Proof, from information_schema rather than from the absence of an error.
SELECT 'email_draft tables remaining' AS check_name,
       COUNT(*) AS issue_count
FROM information_schema.tables
WHERE table_schema = DATABASE()
  AND table_name IN ('email_drafts', 'email_draft_recipients', 'email_draft_attachments');

-- The reservation table must NOT have been affected: it is what replaced the
-- draft claim, so a run that removed it would leave every send unprotected.
SELECT 'email_send_idempotency present' AS check_name,
       CASE WHEN COUNT(*) = 1 THEN 0 ELSE 1 END AS issue_count
FROM information_schema.tables
WHERE table_schema = DATABASE()
  AND table_name = 'email_send_idempotency';
