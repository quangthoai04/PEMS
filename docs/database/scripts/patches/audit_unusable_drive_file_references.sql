-- ═══════════════════════════════════════════════════════════════════════════════════════════════
-- AUDIT — files rows that claim Google Drive storage but cannot address a file there
--
-- READ-ONLY. Every statement is a SELECT. Nothing here changes a row; the repair is section 6,
-- commented out, and is a decision for whoever owns the data.
--
-- WHY THIS EXISTS
--   Traced 2026-08-02 from a live failure on visit process 3107 (visit_request 2004): preparing the
--   "Cập nhật công tác chuẩn bị" email died with "Không tìm thấy tệp đính kèm trên Google Drive".
--   There was no attachment involved. The delegation's partner (SeoulTech, partner_id 1) carries
--   logo_file_id = 1, whose external_file_id is 'drv-logo-seoultech' — a seed placeholder Drive has
--   never heard of. Drive answered 404, the report render let that escape, and a decorative image
--   took down the whole document.
--
--   The CODE side is fixed: the partner logo is now best-effort, and the mandatory Schedule Report
--   is probed before a draft is reused or a message is sent. So nothing below is breaking anything
--   today. It is listed because a row that points at no file is still a row that will mislead the
--   next person who reads it, and because "why is this partner's logo missing" deserves an answer
--   better than silence.
--
-- HOW A PLACEHOLDER IS RECOGNISED
--   Google Drive file ids are 25+ characters of [A-Za-z0-9_-] (28-44 in practice). The seed values
--   ('drv-logo-seoultech', 'ext-file-205', 'drv-asset-211'…) are far shorter. The regex below is
--   deliberately conservative — it flags only ids too short to be real, so a genuine id is never
--   caught. It can still MISS a fabricated id that happens to be long enough; treat a clean result
--   as "no obvious placeholders", not as proof every id resolves.
-- ═══════════════════════════════════════════════════════════════════════════════════════════════

SET @unusable := '^[A-Za-z0-9_-]{25,}$';

-- ── 1. How much of the files table is affected ────────────────────────────────────────────────
SELECT 'files: GOOGLE_DRIVE, id looks real'      AS bucket, COUNT(*) AS rows_affected
FROM files WHERE storage_provider = 'GOOGLE_DRIVE' AND external_file_id REGEXP @unusable
UNION ALL
SELECT 'files: GOOGLE_DRIVE, id unusable', COUNT(*)
FROM files WHERE storage_provider = 'GOOGLE_DRIVE'
  AND (external_file_id IS NULL OR external_file_id = '' OR external_file_id NOT REGEXP @unusable)
UNION ALL
SELECT 'files: LOCAL', COUNT(*) FROM files WHERE storage_provider = 'LOCAL';

-- ── 2. Which business records point at one ────────────────────────────────────────────────────
-- Ordered by how much it matters: a broken SCHEDULE_REPORT or email attachment is a user-visible
-- failure, a broken partner logo is cosmetic.
SELECT 'documents.file_id (incl. SCHEDULE_REPORT)' AS reference, COUNT(*) AS broken
FROM documents d JOIN files f ON f.file_id = d.file_id
WHERE f.storage_provider = 'GOOGLE_DRIVE'
  AND (f.external_file_id IS NULL OR f.external_file_id = '' OR f.external_file_id NOT REGEXP @unusable)
UNION ALL
SELECT 'email_draft_attachments.file_id', COUNT(*)
FROM email_draft_attachments a JOIN files f ON f.file_id = a.file_id
WHERE f.storage_provider = 'GOOGLE_DRIVE'
  AND (f.external_file_id IS NULL OR f.external_file_id = '' OR f.external_file_id NOT REGEXP @unusable)
UNION ALL
SELECT 'sent_email_attachments.file_id', COUNT(*)
FROM sent_email_attachments a JOIN files f ON f.file_id = a.file_id
WHERE f.storage_provider = 'GOOGLE_DRIVE'
  AND (f.external_file_id IS NULL OR f.external_file_id = '' OR f.external_file_id NOT REGEXP @unusable)
UNION ALL
SELECT 'partners.logo_file_id', COUNT(*)
FROM partners p JOIN files f ON f.file_id = p.logo_file_id
WHERE f.storage_provider = 'GOOGLE_DRIVE'
  AND (f.external_file_id IS NULL OR f.external_file_id = '' OR f.external_file_id NOT REGEXP @unusable)
UNION ALL
SELECT 'partners.cover_file_id', COUNT(*)
FROM partners p JOIN files f ON f.file_id = p.cover_file_id
WHERE f.storage_provider = 'GOOGLE_DRIVE'
  AND (f.external_file_id IS NULL OR f.external_file_id = '' OR f.external_file_id NOT REGEXP @unusable);

-- ── 3. The partner logos, named ───────────────────────────────────────────────────────────────
SELECT p.partner_id, p.name, p.logo_file_id, f.external_file_id, f.original_filename
FROM partners p JOIN files f ON f.file_id = p.logo_file_id
WHERE f.storage_provider = 'GOOGLE_DRIVE'
  AND (f.external_file_id IS NULL OR f.external_file_id = '' OR f.external_file_id NOT REGEXP @unusable)
ORDER BY p.partner_id;

-- ── 4. Archived Schedule Reports that cannot be opened ────────────────────────────────────────
-- Expected to be EMPTY. A row here is serious: the delegation shows a report in its documents list
-- that nobody can download, and (before the code fix) the setup-progress email would have attached it.
SELECT d.document_id, d.owner_id AS visit_request_id, d.file_id, f.external_file_id, d.title, d.created_at
FROM documents d JOIN files f ON f.file_id = d.file_id
WHERE d.document_category = 'SCHEDULE_REPORT'
  AND f.storage_provider = 'GOOGLE_DRIVE'
  AND (f.external_file_id IS NULL OR f.external_file_id = '' OR f.external_file_id NOT REGEXP @unusable)
ORDER BY d.document_id;

-- ── 5. Orphans: unusable rows nothing references ──────────────────────────────────────────────
-- These are safe to leave alone. Counted so the totals in section 1 add up and nobody assumes the
-- difference is hiding somewhere important.
SELECT COUNT(*) AS unreferenced_unusable_files
FROM files f
WHERE f.storage_provider = 'GOOGLE_DRIVE'
  AND (f.external_file_id IS NULL OR f.external_file_id = '' OR f.external_file_id NOT REGEXP @unusable)
  AND NOT EXISTS (SELECT 1 FROM documents               x WHERE x.file_id       = f.file_id)
  AND NOT EXISTS (SELECT 1 FROM email_draft_attachments x WHERE x.file_id       = f.file_id)
  AND NOT EXISTS (SELECT 1 FROM sent_email_attachments  x WHERE x.file_id       = f.file_id)
  AND NOT EXISTS (SELECT 1 FROM partners                x WHERE x.logo_file_id  = f.file_id)
  AND NOT EXISTS (SELECT 1 FROM partners                x WHERE x.cover_file_id = f.file_id);

-- ═══════════════════════════════════════════════════════════════════════════════════════════════
-- 6. REPAIR — NOT RUN. Read this before uncommenting anything.
--
-- There is no way to "fix" these rows: the bytes were never uploaded, so no correct external_file_id
-- exists to write in. The only choices are to detach the reference (the record stops claiming a
-- picture it does not have) or to leave it and let the code degrade, which it now does.
--
-- Detaching is a DELETION of a business fact — "this partner has a logo" — so it is a decision for
-- the data owner, not a migration to run on the way past. If it is taken, do it per environment,
-- with a backup, and never on production without written approval.
--
-- Re-uploading the real logos through the Partner screen is the better repair where the images exist:
-- that writes a genuine Drive id through the normal upload path and needs no SQL at all.
--
--   -- Detach unusable partner logos (review section 3 output FIRST):
--   -- UPDATE partners p JOIN files f ON f.file_id = p.logo_file_id
--   --   SET p.logo_file_id = NULL
--   -- WHERE f.storage_provider = 'GOOGLE_DRIVE'
--   --   AND (f.external_file_id IS NULL OR f.external_file_id = ''
--   --        OR f.external_file_id NOT REGEXP '^[A-Za-z0-9_-]{25,}$');
--
--   -- Same for covers:
--   -- UPDATE partners p JOIN files f ON f.file_id = p.cover_file_id
--   --   SET p.cover_file_id = NULL
--   -- WHERE f.storage_provider = 'GOOGLE_DRIVE'
--   --   AND (f.external_file_id IS NULL OR f.external_file_id = ''
--   --        OR f.external_file_id NOT REGEXP '^[A-Za-z0-9_-]{25,}$');
--
-- Do NOT delete the files rows themselves. They are referenced by checksum/audit history in places
-- this script does not enumerate, and removing them buys nothing once the reference is detached.
-- ═══════════════════════════════════════════════════════════════════════════════════════════════
