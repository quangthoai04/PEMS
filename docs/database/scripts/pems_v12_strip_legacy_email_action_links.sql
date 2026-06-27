-- =============================================================================
-- pems_v12_strip_legacy_email_action_links.sql
-- -----------------------------------------------------------------------------
-- Purpose: remove the LEGACY inline action/detail links baked into the action-token
-- email templates' bodies, e.g.
--     <p><a href="{{acceptUrl}}">Chấp nhận tham gia</a> &nbsp; | &nbsp; <a href="{{declineUrl}}">Từ chối</a></p>
--     <p><a href="{{detailUrl}}">Xem yêu cầu hậu cần</a></p>
-- The backend injects exactly ONE canonical action block (wrapped in
-- <!-- PEMS_ACTION_BLOCK_START/END -->) with the real one-time email_action_tokens and
-- the detail button, so a template that still carries its own button/detail row produces
-- DUPLICATE links.
--
-- Schema note: template bodies are in `body_vi` / `body_en` (NOT `body_html`).
--
-- Safety:
--   * Idempotent — REPLACE() of an already-absent substring is a no-op; re-runnable.
--   * Scoped to the action-token template_codes only.
--   * Removes ONLY the exact legacy action/detail <p> blocks — normal links (school site,
--     Google Drive, docs) are never matched.
--   * Reminder templates (VISIT_REMINDER_*) keep their {{detailUrl}} link — they are NOT
--     in scope and use a different (non-action) send path.
--   * The runtime cleaner (EmailComposition.StripActionArtifacts) is the guarantee on send;
--     this patch is DB hygiene so the stored template no longer ships the duplicate.
-- =============================================================================

-- 1) PRE-CHECK — which action-token templates still carry an inline action/detail link?
SELECT email_template_id, template_code, name
FROM email_templates
WHERE template_code IN (
        'VISIT_PARTICIPANT_INVITATION',
        'VISIT_DEPARTMENT_LEADER_INVITATION',
        'VISIT_STUDENT_INVITATION',
        'LOGISTICS_ASSIGNEE_ASSIGNMENT',
        'LOGISTICS_REQUEST_TO_DEPARTMENT')
  AND (body_vi LIKE '%<a href="{{acceptUrl}}"%' OR body_vi LIKE '%<a href="{{declineUrl}}"%'
    OR body_vi LIKE '%<a href="{{detailUrl}}"%' OR body_vi LIKE '%<a href="{{DetailUrl}}"%'
    OR body_en LIKE '%<a href="{{acceptUrl}}"%' OR body_en LIKE '%<a href="{{declineUrl}}"%'
    OR body_en LIKE '%<a href="{{detailUrl}}"%' OR body_en LIKE '%<a href="{{DetailUrl}}"%');

-- 2) CLEAN accept/decline/assign blocks (both " &nbsp; | &nbsp; " and " | " variants).
UPDATE email_templates
SET
  body_vi = REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(body_vi,
    '<p><a href="{{acceptUrl}}">Chấp nhận tham gia</a> &nbsp; | &nbsp; <a href="{{declineUrl}}">Từ chối</a></p>', ''),
    '<p><a href="{{acceptUrl}}">Chấp nhận phối hợp</a> &nbsp; | &nbsp; <a href="{{declineUrl}}">Từ chối</a> &nbsp; | &nbsp; <a href="{{assignUrl}}">Gán nhân sự</a></p>', ''),
    '<p><a href="{{acceptUrl}}">Nhận nhiệm vụ</a> &nbsp; | &nbsp; <a href="{{declineUrl}}">Từ chối</a></p>', ''),
    '<p><a href="{{acceptUrl}}">Chấp nhận</a> | <a href="{{declineUrl}}">Từ chối</a> | <a href="{{assignUrl}}">Gán nhân sự</a></p>', ''),
    '<p><a href="{{acceptUrl}}">Chấp nhận</a> | <a href="{{declineUrl}}">Từ chối</a></p>', ''),
  body_en = REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(body_en,
    '<p><a href="{{acceptUrl}}">Accept invitation</a> &nbsp; | &nbsp; <a href="{{declineUrl}}">Decline</a></p>', ''),
    '<p><a href="{{acceptUrl}}">Accept coordination</a> &nbsp; | &nbsp; <a href="{{declineUrl}}">Decline</a> &nbsp; | &nbsp; <a href="{{assignUrl}}">Assign staff</a></p>', ''),
    '<p><a href="{{acceptUrl}}">Accept assignment</a> &nbsp; | &nbsp; <a href="{{declineUrl}}">Decline</a></p>', ''),
    '<p><a href="{{acceptUrl}}">Accept</a> | <a href="{{declineUrl}}">Decline</a> | <a href="{{assignUrl}}">Assign staff</a></p>', ''),
    '<p><a href="{{acceptUrl}}">Accept</a> | <a href="{{declineUrl}}">Decline</a></p>', '')
WHERE template_code IN (
        'VISIT_PARTICIPANT_INVITATION',
        'VISIT_DEPARTMENT_LEADER_INVITATION',
        'VISIT_STUDENT_INVITATION',
        'LOGISTICS_ASSIGNEE_ASSIGNMENT',
        'LOGISTICS_REQUEST_TO_DEPARTMENT');

-- 2b) CLEAN logistics "view detail" anchors from the LOGISTICS_REQUEST_TO_DEPARTMENT body. The detail
--     button lives in the backend's canonical action block, so the template body must not carry its own
--     {{detailUrl}} anchor. Both {{detailUrl}} and {{DetailUrl}} casings (MySQL REPLACE is case-sensitive).
UPDATE email_templates
SET
  body_vi = REPLACE(REPLACE(REPLACE(REPLACE(body_vi,
    '<p><a href="{{detailUrl}}">Xem chi tiết trong PEMS</a></p>', ''),
    '<p><a href="{{DetailUrl}}">Xem chi tiết trong PEMS</a></p>', ''),
    '<p><a href="{{detailUrl}}">Xem yêu cầu hậu cần</a></p>', ''),
    '<p><a href="{{DetailUrl}}">Xem yêu cầu hậu cần</a></p>', ''),
  body_en = REPLACE(REPLACE(REPLACE(REPLACE(body_en,
    '<p><a href="{{detailUrl}}">View in PEMS</a></p>', ''),
    '<p><a href="{{DetailUrl}}">View in PEMS</a></p>', ''),
    '<p><a href="{{detailUrl}}">View logistics request</a></p>', ''),
    '<p><a href="{{DetailUrl}}">View logistics request</a></p>', '')
WHERE template_code = 'LOGISTICS_REQUEST_TO_DEPARTMENT';

-- 3) POST-CHECK — expect 0 rows. Any remaining action/detail anchor in a body means a variant not
--    covered above; copy it as another REPLACE() (the runtime cleaner still strips it on send meanwhile).
SELECT email_template_id, template_code, name
FROM email_templates
WHERE template_code IN (
        'VISIT_PARTICIPANT_INVITATION',
        'VISIT_DEPARTMENT_LEADER_INVITATION',
        'VISIT_STUDENT_INVITATION',
        'LOGISTICS_ASSIGNEE_ASSIGNMENT',
        'LOGISTICS_REQUEST_TO_DEPARTMENT')
  AND (body_vi LIKE '%<a href="{{acceptUrl}}"%' OR body_vi LIKE '%<a href="{{declineUrl}}"%'
    OR body_vi LIKE '%<a href="{{detailUrl}}"%' OR body_vi LIKE '%<a href="{{DetailUrl}}"%'
    OR body_en LIKE '%<a href="{{acceptUrl}}"%' OR body_en LIKE '%<a href="{{declineUrl}}"%'
    OR body_en LIKE '%<a href="{{detailUrl}}"%' OR body_en LIKE '%<a href="{{DetailUrl}}"%');
