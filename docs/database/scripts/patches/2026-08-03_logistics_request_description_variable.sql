-- =====================================================================
-- 2026-08-03_logistics_request_description_variable.sql
--
-- LOGISTICS_REQUEST_TO_DEPARTMENT: carry the Host's "Mô tả chi tiết" under its own variable.
--
--   OUT  {{coordinationNote}}   labelled "Ghi chú phối hợp" / "Coordination note"
--   IN   {{logisticsDescription}} labelled "Nội dung chi tiết công việc" / "Detailed work content"
--   OUT  the whole "Hạn phản hồi" / "Respond by" list item, with its {{dueAt}}
--
-- WHY, in one paragraph. The send point has always passed visit_logistics_items.description into this
-- message — but under the name coordinationNote, which is a DIFFERENT column
-- (offline_coordination_note) and a different business field. The department therefore read the work
-- content under the heading "Ghi chú phối hợp", and the compose-screen preview, which supplies the real
-- coordination note (always NULL on a SYSTEM_REQUEST, because an OFFLINE_COORDINATED item is recorded
-- DONE and sends no email at all), showed "Không có ghi chú phối hợp." where the send would show the
-- description. One variable, two meanings, two different outputs for the same request.
--
-- "Hạn phản hồi" goes for a separate reason: the Host has no response-deadline field any more. The
-- server still derives due_at as usage-start minus 24h for its own scheduling, and THAT IS UNCHANGED —
-- this patch does not touch the column, the handler, or any other template that shows it
-- (LOGISTICS_ASSIGNEE_ASSIGNMENT and LOGISTICS_EXPENSE_REPORT_REMINDER keep their {{dueAt}}). It only
-- stops printing a deadline to the department that nobody set and nobody committed to.
--
-- SURGICAL BY CONSTRUCTION. This is a REPLACE() over the two fragments, not an overwrite of the row.
-- An operator who reworded the greeting, restyled the list or moved {{actionBlock}} keeps every one of
-- those edits; only the variable fragments change. An overwrite would have silently discarded their
-- work, and the admin UI gives no way to get it back.
--
-- IDEMPOTENT: the guard matches only rows that still contain the OLD fragments, so a second run reports
-- 0 rows affected and changes nothing. Safe to re-run, and safe on a database already imported from the
-- updated canonical seed.
--
-- Run with:  mysql --default-character-set=utf8mb4 -u root -p pems_db < <this file>
-- (the charset flag is not optional: without it every Vietnamese string this script writes is mojibaked)
-- =====================================================================

-- ── BEFORE ────────────────────────────────────────────────────────────
SELECT 'BEFORE' AS phase,
       template_code,
       variables_text,
       body_vi LIKE '%{{coordinationNote}}%'   AS has_old_note_vi,
       body_vi LIKE '%Hạn phản hồi%'           AS has_old_deadline_vi,
       body_en LIKE '%{{coordinationNote}}%'   AS has_old_note_en,
       body_en LIKE '%Respond by%'             AS has_old_deadline_en,
       body_vi LIKE '%{{logisticsDescription}}%' AS already_migrated_vi
FROM email_templates
WHERE template_code = 'LOGISTICS_REQUEST_TO_DEPARTMENT';

-- ── VI body ───────────────────────────────────────────────────────────
UPDATE email_templates
SET body_vi = REPLACE(
        REPLACE(
            body_vi,
            '<li>Hạn phản hồi: <strong>{{dueAt}}</strong></li>',
            ''),
        '<p><strong>Ghi chú phối hợp:</strong> {{coordinationNote}}</p>',
        '<p><strong>Nội dung chi tiết công việc:</strong></p><p>{{logisticsDescription}}</p>')
WHERE template_code = 'LOGISTICS_REQUEST_TO_DEPARTMENT'
  AND (body_vi LIKE '%{{coordinationNote}}%' OR body_vi LIKE '%Hạn phản hồi%');

-- ── EN body ───────────────────────────────────────────────────────────
UPDATE email_templates
SET body_en = REPLACE(
        REPLACE(
            body_en,
            '<li>Respond by: <strong>{{dueAt}}</strong></li>',
            ''),
        '<p><strong>Coordination note:</strong> {{coordinationNote}}</p>',
        '<p><strong>Detailed work content:</strong></p><p>{{logisticsDescription}}</p>')
WHERE template_code = 'LOGISTICS_REQUEST_TO_DEPARTMENT'
  AND (body_en LIKE '%{{coordinationNote}}%' OR body_en LIKE '%Respond by%');

-- ── Declared variables ────────────────────────────────────────────────
-- Set outright rather than REPLACE()d: this column is a generated list, not prose, and the registry
-- (SystemEmailTemplates) is its single author. A row whose variables_text disagrees with the registry
-- fails the send closed, so leaving a hand-edited ordering intact here would be preserving a defect.
UPDATE email_templates
SET variables_text = 'departmentLeaderName,requesterName,logisticsTitle,logisticsItemType,quantity,usageStartAt,usageEndAt,logisticsDescription'
WHERE template_code = 'LOGISTICS_REQUEST_TO_DEPARTMENT'
  AND variables_text <> 'departmentLeaderName,requesterName,logisticsTitle,logisticsItemType,quantity,usageStartAt,usageEndAt,logisticsDescription';

-- ── AFTER ─────────────────────────────────────────────────────────────
SELECT 'AFTER' AS phase,
       template_code,
       variables_text,
       body_vi LIKE '%{{logisticsDescription}}%' AS has_new_var_vi,
       body_en LIKE '%{{logisticsDescription}}%' AS has_new_var_en,
       body_vi LIKE '%{{coordinationNote}}%'     AS leftover_note_vi,
       body_vi LIKE '%Hạn phản hồi%'             AS leftover_deadline_vi,
       body_en LIKE '%{{coordinationNote}}%'     AS leftover_note_en,
       body_en LIKE '%Respond by%'               AS leftover_deadline_en,
       body_vi LIKE '%{{actionBlock}}%'          AS keeps_action_block,
       body_vi LIKE '%{{contactInformationBlock}}%' AS keeps_contact_block
FROM email_templates
WHERE template_code = 'LOGISTICS_REQUEST_TO_DEPARTMENT';
