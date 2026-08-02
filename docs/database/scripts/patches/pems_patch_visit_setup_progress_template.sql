-- =============================================================================
-- Re-sync ONE template: VISIT_SETUP_PROGRESS_UPDATE
-- =============================================================================
--
-- WHY
--   The dev database carries a revision of this template from before the setup tables moved into the
--   email body. Its body_vi / body_en no longer contain {{setupSummaryBlock}}, so the tables the
--   backend builds from VisitSetupSnapshot have nowhere to be substituted. Before the renderer guard
--   that ships alongside this script, that failed SILENTLY: nothing is left unresolved when there is
--   no placeholder to resolve, so the mail went out saying "here is the latest update on preparations"
--   with no update in it.
--
--   Observed on pems_db (2026-08-02):
--     body_vi LIKE '%{{setupSummaryBlock}}%' -> 0
--     body_en LIKE '%{{setupSummaryBlock}}%' -> 0
--     variables_text                          -> delegationName,campusName,plannedStart,plannedEnd,hostName  (correct)
--
--   variables_text is already right and is NOT touched: {{setupSummaryBlock}} is a trusted block the
--   backend injects, never an editable variable, so listing it there would offer an operator a field
--   they must never be able to fill in.
--
-- SOURCE OF TRUTH
--   docs/database/scripts/PEMS_FULL_VS_31_07_NEW.sql (row 70031) and
--   docs/database/scripts/email_template_cc_bcc_sync/02_sync_templates.sql — both already correct and
--   byte-compatible with the content below. This script exists only to bring an EXISTING row forward;
--   a fresh import from the canonical script does not need it.
--
-- SAFETY
--   * Idempotent: re-running changes nothing once the row matches — including the revision, which is
--     bumped exactly once per repair and never again.
--   * Scoped: touches exactly one template_code, and only body_vi / body_en / revision.
--   * Refuses to run against a row an operator has deliberately rewritten — it only replaces a body it
--     recognises as the known pre-feature revision, so local wording work is never silently discarded.
--   * No DELETE, no reseed, no schema change.
--
-- HOW TO RUN
--   mysql -u root -p pems_db < docs/database/scripts/patches/pems_patch_visit_setup_progress_template.sql
--
-- =============================================================================

SET @canonical_vi := CONCAT(
  '<p>Kính gửi Quý khách,</p>',
  '<p>Đây là cập nhật mới nhất về công tác chuẩn bị cho chuyến thăm của đoàn <strong>{{delegationName}}</strong> tại <strong>{{campusName}}</strong>, dự kiến từ <strong>{{plannedStart}}</strong> đến <strong>{{plannedEnd}}</strong>.</p>',
  '{{setupSummaryBlock}}',
  '<p>Báo cáo Lịch trình chi tiết được đính kèm trong email này.</p>',
  '<p>Nếu Quý khách cần điều chỉnh nội dung nào, vui lòng phản hồi email này để <strong>{{hostName}}</strong> — người phụ trách tiếp đón — kịp thời cập nhật.</p>',
  '<p style="color:#6b7280;font-size:12px">Trân trọng,<br/>PEMS - FPT University</p>');

SET @canonical_en := CONCAT(
  '<p>Dear Guest,</p>',
  '<p>This is the latest update on preparations for the visit of <strong>{{delegationName}}</strong> to <strong>{{campusName}}</strong>, scheduled from <strong>{{plannedStart}}</strong> to <strong>{{plannedEnd}}</strong>.</p>',
  '{{setupSummaryBlock}}',
  '<p>The detailed Schedule Report is attached to this email.</p>',
  '<p>If anything needs adjusting, please reply to this email so that <strong>{{hostName}}</strong>, the host for this visit, can update it in time.</p>',
  '<p style="color:#6b7280;font-size:12px">Best regards,<br/>PEMS - FPT University</p>');

-- The exact pre-feature bodies: the canonical text with the block segment removed and nothing else
-- changed. Matching on these is what makes the update safe to run on a database somebody has been
-- working in — a row that differs for any other reason is left alone and reported below.
SET @stale_vi := REPLACE(@canonical_vi, '{{setupSummaryBlock}}', '');
SET @stale_en := REPLACE(@canonical_en, '{{setupSummaryBlock}}', '');

-- Byte-exact comparison, so the script does not depend on the column's collation matching the
-- session's. Comparing a utf8mb4_unicode_ci column against a session variable directly raises
-- "Illegal mix of collations" on some servers; CONVERT ... USING binary sidesteps that entirely and is
-- also the stricter test — two bodies differing only by accent or case are NOT the same body.
-- (LIKE with a plain literal is safe as-is: a literal adopts the column's collation.)

-- ── Before ───────────────────────────────────────────────────────────────────
SELECT 'BEFORE' AS phase,
       template_code,
       revision,
       (body_vi LIKE '%{{setupSummaryBlock}}%') AS vi_has_block,
       (body_en LIKE '%{{setupSummaryBlock}}%') AS en_has_block,
       (CONVERT(body_vi USING binary) = CONVERT(@stale_vi USING binary)) AS vi_is_known_stale,
       (CONVERT(body_en USING binary) = CONVERT(@stale_en USING binary)) AS en_is_known_stale,
       variables_text
FROM email_templates
WHERE template_code = 'VISIT_SETUP_PROGRESS_UPDATE';

-- ── Repair (idempotent, and only the revision we recognise) ──────────────────
--
-- ONE statement for both languages so `revision` moves exactly once per repair, not once per column.
--
-- The bump is not decoration. `revision` is the optimistic-concurrency token: EmailTemplateContentWriter
-- writes `... SET revision = revision + 1 ... WHERE email_template_id = ? AND revision = ?`, and both the
-- edit and the restore-default commands require the caller to send the revision their screen was showing.
-- Repairing the body without moving it would leave anybody who already had the template screen open able
-- to save the old content straight back over this fix — which is exactly how the row was lost in the
-- first place. Bumping makes that stale save fail closed instead.
UPDATE email_templates
SET body_vi = CASE
                WHEN CONVERT(body_vi USING binary) = CONVERT(@stale_vi USING binary) THEN @canonical_vi
                ELSE body_vi
              END,
    body_en = CASE
                WHEN CONVERT(body_en USING binary) = CONVERT(@stale_en USING binary) THEN @canonical_en
                ELSE body_en
              END,
    revision = revision + 1
WHERE template_code = 'VISIT_SETUP_PROGRESS_UPDATE'
  AND (CONVERT(body_vi USING binary) = CONVERT(@stale_vi USING binary)
    OR CONVERT(body_en USING binary) = CONVERT(@stale_en USING binary));

-- ── Catch-up: rows an EARLIER revision of this script repaired ───────────────
--
-- That earlier revision fixed the body but did not touch `revision`, leaving the stale-editor window
-- above open on any database it was already run against. This closes it once.
--
-- Pinned to the exact post-repair state — revision still 2, both bodies carrying the block, and both
-- hashing to the canonical content — so it is inert everywhere else and cannot fire twice: after it runs
-- the revision is 3 and the row no longer matches. A fresh import never matches it either, because the
-- repair above has already moved the revision.
UPDATE email_templates
SET revision = revision + 1
WHERE template_code = 'VISIT_SETUP_PROGRESS_UPDATE'
  AND revision = 2
  AND body_vi LIKE '%{{setupSummaryBlock}}%'
  AND body_en LIKE '%{{setupSummaryBlock}}%'
  AND SHA2(CONVERT(body_vi USING binary), 256)
      = '48a953dad2c32fd86da4e4bd5e497e56e18a49f981dbca8029fe367321ef49b5'
  AND SHA2(CONVERT(body_en USING binary), 256)
      = 'cc1fdad21135c14e626660e6ae1f6af5de68e8822f5f417fa85da5f206e5039f';

-- ── After ────────────────────────────────────────────────────────────────────
SELECT 'AFTER' AS phase,
       template_code,
       revision,
       (body_vi LIKE '%{{setupSummaryBlock}}%') AS vi_has_block,
       (body_en LIKE '%{{setupSummaryBlock}}%') AS en_has_block,
       variables_text
FROM email_templates
WHERE template_code = 'VISIT_SETUP_PROGRESS_UPDATE';

-- ── Verdict ──────────────────────────────────────────────────────────────────
-- A row still missing the block after the update was NOT the revision this script knows how to fix.
-- That is a deliberate hand-edit, and re-syncing it is a decision for whoever made it — so this
-- reports rather than overwrites.
SELECT CASE
         WHEN NOT EXISTS (SELECT 1 FROM email_templates WHERE template_code = 'VISIT_SETUP_PROGRESS_UPDATE')
           THEN 'MISSING: no such template row — import the canonical script instead'
         WHEN EXISTS (SELECT 1 FROM email_templates
                      WHERE template_code = 'VISIT_SETUP_PROGRESS_UPDATE'
                        AND body_vi LIKE '%{{setupSummaryBlock}}%'
                        AND body_en LIKE '%{{setupSummaryBlock}}%')
           THEN 'OK: both languages carry {{setupSummaryBlock}}'
         ELSE 'MANUAL: body differs from the known pre-feature revision — re-sync by hand, nothing was overwritten'
       END AS verdict;
