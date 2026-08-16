-- =============================================================================
-- Backfill metadata_json (eventKey + structured params) for legacy VISITOR
-- notifications created before the semantic-notification refactor
-- =============================================================================
--
-- WHY
--   Notifications created before this change have metadata_json = NULL, so a Visitor reading
--   in English would see the row's Vietnamese Title/Message verbatim (or, after this same
--   change ships, a generic "You have a new notification" fallback) instead of a proper EN
--   sentence. This script enriches ONLY the rows that can be reconstructed from data that
--   still exists and still resolves — it never guesses, and never parses the Vietnamese
--   prose in Title/Message to invent params (see docs/CanhIter3FixBug — Phase 12 rule).
--
-- INVESTIGATION (pems_db, 2026-08-16, read-only queries — see chat history for the full trace)
--   36 total notification rows. Only 5 have a VISITOR-role recipient:
--     - notification_id 99019, 99022 (type VISIT_REQUEST_APPROVED): title/message match the
--       CURRENT CampusApprovalExecutor.cs producer output exactly, and visit_request_id /
--       campus_id / actor_user_id all resolve to real, still-existing rows (request codes
--       VR20260815630D14C / VR20260815EA1CB80, campus "FPT University Hà Nội", host
--       "IC Staff Leader Hà Nội"). FULLY_RECONSTRUCTABLE — backfilled below.
--     - notification_id 7, 99014, 99015 (types VISIT_APPROVED / VISITOR_CANCEL_CONFIRMED):
--       title = "Thông báo nghiệp vụ PEMS #<id>", message = generic placeholder boilerplate
--       ("Thông báo ghi nhận thay đổi nghiệp vụ liên quan..."), and EVERY semantic FK column
--       (visit_request_id, visit_instance_id, campus_id, actor_user_id) is NULL. related_id
--       does not resolve to any row PEMS.Domain currently models. This is generic seed/fixture
--       filler text, not a record of a real business event — NON_RECONSTRUCTABLE. There is no
--       data left anywhere to derive requestCode/campusName/hostName/reason from, so these 3
--       rows are intentionally left untouched; the frontend's generic unmapped-notification
--       fallback covers them safely (no raw-Vietnamese leak, see
--       resolveNotificationPresentation.ts).
--     - The other 31 rows all have a Staff/HO/Department/Admin/Student recipient — not
--       Visitor's notification regardless of notification_type name, confirmed by an explicit
--       join to users/roles rather than assumed from the type string.
--
--   This script's WHERE clause is the general, reusable rule (not a hardcoded id list): any row
--   of type VISIT_REQUEST_APPROVED, still missing metadata_json, whose recipient really is
--   VISITOR and whose visit_request_id/campus_id/actor_user_id all resolve to a row that still
--   exists. On THIS database that matches exactly notification_id 99019 and 99022 today, but the
--   script is safe to re-run later against a bigger/different dataset — it will simply pick up
--   any additional row meeting the same real criteria.
--
-- SAFETY
--   * Idempotent: `metadata_json IS NULL` in the WHERE clause means a second run touches zero
--     rows once applied.
--   * Scoped: only notification_type = 'VISIT_REQUEST_APPROVED' (the one Visitor-reachable type
--     with exactly one producer in the codebase — CampusApprovalExecutor.cs — so a VISITOR-role
--     recipient is unambiguous, unlike VISIT_STATUS_CHANGED/VISIT_CANCELLED/
--     VISIT_REQUEST_SUBMITTED, which are shared with Staff/HO-facing producers and are NOT
--     touched here).
--   * INNER JOINs to visit_requests / campuses / users(actor) mean a row whose FK no longer
--     resolves is silently excluded, never backfilled with a guess.
--   * Does not touch is_read, created_at, recipient_user_id, title, message, action_url,
--     dedupe_key, or any other column — only sets metadata_json.
--   * No DELETE, no new rows, no schema change.
--
-- DRY RUN (run this SELECT first to see exactly what would change)
--   SELECT n.notification_id, n.recipient_user_id, vr.request_code, c.name AS campus_name,
--          actor.full_name AS host_name
--   FROM notifications n
--   JOIN users u ON u.user_id = n.recipient_user_id
--   JOIN roles r ON r.role_id = u.role_id
--   JOIN visit_requests vr ON vr.visit_request_id = n.visit_request_id
--   JOIN campuses c ON c.campus_id = n.campus_id
--   JOIN users actor ON actor.user_id = n.actor_user_id
--   WHERE n.notification_type = 'VISIT_REQUEST_APPROVED'
--     AND n.metadata_json IS NULL
--     AND r.role_code = 'VISITOR';
--
-- HOW TO RUN
--   mysql -u root -p pems_db < docs/database/scripts/patches/pems_patch_backfill_visitor_notification_metadata.sql
--
-- =============================================================================

UPDATE notifications n
JOIN users u ON u.user_id = n.recipient_user_id
JOIN roles r ON r.role_id = u.role_id
JOIN visit_requests vr ON vr.visit_request_id = n.visit_request_id
JOIN campuses c ON c.campus_id = n.campus_id
JOIN users actor ON actor.user_id = n.actor_user_id
SET n.metadata_json = JSON_OBJECT(
    'eventKey', 'CAMPUS_APPROVED',
    'params', JSON_OBJECT(
        'campusName', c.name,
        'requestCode', vr.request_code,
        'hostName', actor.full_name
    )
)
WHERE n.notification_type = 'VISIT_REQUEST_APPROVED'
  AND n.metadata_json IS NULL
  AND r.role_code = 'VISITOR';
