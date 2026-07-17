-- =====================================================================
-- PEMS — PER-CAMPUS FORM v2 — PREFLIGHT / READINESS  (REPORT ONLY)
-- Plan §17.5, §22.1. This script performs NO mutation. Run it before
-- 02_up_additive.sql and RESOLVE every non-empty result set by hand.
-- Nothing here fixes data automatically (§4.5: "Không sửa seed/lịch thật
-- một cách tự động; xuất danh sách vi phạm để xử lý trước.").
--
-- Read the SUMMARY result first; any violation_count > 0 means the detail
-- query with the same check_name lists the offending rows.
-- =====================================================================

-- ---------------------------------------------------------------------
-- SUMMARY — one row per check, with a violation_count.
-- ---------------------------------------------------------------------
SELECT 'P01_duration_under_30m' AS check_name,
       COUNT(*) AS violation_count,
       'visit_request_campuses schedule shorter than 30 minutes — BLOCKS ck_visit_instance_min_duration_30m' AS note
FROM visit_request_campuses
WHERE TIMESTAMPDIFF(MINUTE, planned_start_at, planned_end_at) < 30
UNION ALL
SELECT 'P02_end_not_after_start', COUNT(*),
       'planned_end_at <= planned_start_at'
FROM visit_request_campuses
WHERE planned_end_at <= planned_start_at
UNION ALL
SELECT 'P03_request_without_campus', COUNT(*),
       'visit_requests with zero campus instances'
FROM visit_requests vr
WHERE NOT EXISTS (SELECT 1 FROM visit_request_campuses vrc WHERE vrc.visit_request_id = vr.visit_request_id)
UNION ALL
SELECT 'P04_duplicate_request_campus', COUNT(*),
       'duplicate (visit_request_id, campus_id) — must already be unique'
FROM (
  SELECT visit_request_id, campus_id
  FROM visit_request_campuses
  GROUP BY visit_request_id, campus_id
  HAVING COUNT(*) > 1
) d
UNION ALL
SELECT 'P05_request_missing_guest', COUNT(*),
       'visit_requests with no GUEST member'
FROM visit_requests vr
WHERE NOT EXISTS (
  SELECT 1 FROM visit_guest_members m
  WHERE m.visit_request_id = vr.visit_request_id AND m.member_type = 'GUEST')
UNION ALL
SELECT 'P06_visitor_role_status_invalid', COUNT(*),
       'visitor_user_id is set but the user is not an ACTIVE VISITOR'
FROM visit_requests vr
JOIN users u ON u.user_id = vr.visitor_user_id
JOIN roles r ON r.role_id = u.role_id
WHERE vr.visitor_user_id IS NOT NULL
  AND (r.role_code <> 'VISITOR' OR u.status <> 'ACTIVE')
UNION ALL
SELECT 'P07_registrant_role_status_invalid', COUNT(*),
       'registrant_user_id is set but the user is INACTIVE/LOCKED'
FROM visit_requests vr
JOIN users u ON u.user_id = vr.registrant_user_id
WHERE vr.registrant_user_id IS NOT NULL
  AND u.status <> 'ACTIVE'
UNION ALL
SELECT 'P08_owner_missing_for_active_hint', COUNT(*),
       'requests that will be backfilled ACTIVE need visitor_user_id; this lists requests with NULL owner (stay PENDING_CONFIRMATION) for review'
FROM visit_requests vr
WHERE vr.visitor_user_id IS NULL
  AND vr.status NOT IN ('CANCELLED','REJECTED')
UNION ALL
SELECT 'P09_member_request_mismatch', COUNT(*),
       'guest members whose visit_request_id points to a missing request'
FROM visit_guest_members m
WHERE NOT EXISTS (SELECT 1 FROM visit_requests vr WHERE vr.visit_request_id = m.visit_request_id)
UNION ALL
SELECT 'P10_registrant_email_blank_or_bad', COUNT(*),
       'registrant_email blank or without a single @'
FROM visit_requests
WHERE TRIM(registrant_email) = ''
   OR registrant_email NOT LIKE '%@%.%'
UNION ALL
SELECT 'P11_contact_email_blank_or_bad', COUNT(*),
       'contact_person_email blank or without a single @'
FROM visit_requests
WHERE TRIM(contact_person_email) = ''
   OR contact_person_email NOT LIKE '%@%.%'
UNION ALL
SELECT 'P13_orphan_instance_no_request', COUNT(*),
       'visit_request_campuses pointing to a missing request (FK should already prevent this)'
FROM visit_request_campuses vrc
WHERE NOT EXISTS (SELECT 1 FROM visit_requests vr WHERE vr.visit_request_id = vrc.visit_request_id);

-- ---------------------------------------------------------------------
-- DETAIL queries — only inspect the ones whose SUMMARY count > 0.
-- ---------------------------------------------------------------------

-- P01 — schedules shorter than 30 minutes (must be fixed before UP).
SELECT vrc.visit_instance_id, vrc.visit_request_id, vrc.campus_id,
       vrc.planned_start_at, vrc.planned_end_at,
       TIMESTAMPDIFF(MINUTE, vrc.planned_start_at, vrc.planned_end_at) AS minutes,
       vrc.status
FROM visit_request_campuses vrc
WHERE TIMESTAMPDIFF(MINUTE, vrc.planned_start_at, vrc.planned_end_at) < 30
ORDER BY minutes, vrc.visit_instance_id;

-- P03 — requests with no campus instance.
SELECT vr.visit_request_id, vr.request_code, vr.status, vr.submitted_at
FROM visit_requests vr
WHERE NOT EXISTS (SELECT 1 FROM visit_request_campuses vrc WHERE vrc.visit_request_id = vr.visit_request_id)
ORDER BY vr.visit_request_id;

-- P05 — requests with no GUEST member.
SELECT vr.visit_request_id, vr.request_code, vr.status
FROM visit_requests vr
WHERE NOT EXISTS (
  SELECT 1 FROM visit_guest_members m
  WHERE m.visit_request_id = vr.visit_request_id AND m.member_type = 'GUEST')
ORDER BY vr.visit_request_id;

-- P06 — visitor_user_id set but not an ACTIVE VISITOR.
SELECT vr.visit_request_id, vr.request_code, vr.visitor_user_id,
       r.role_code, u.status
FROM visit_requests vr
JOIN users u ON u.user_id = vr.visitor_user_id
JOIN roles r ON r.role_id = u.role_id
WHERE vr.visitor_user_id IS NOT NULL
  AND (r.role_code <> 'VISITOR' OR u.status <> 'ACTIVE')
ORDER BY vr.visit_request_id;

-- P08 — owner-less non-terminal requests (will remain PENDING_CONFIRMATION).
SELECT vr.visit_request_id, vr.request_code, vr.status,
       vr.registrant_email, vr.contact_person_email
FROM visit_requests vr
WHERE vr.visitor_user_id IS NULL
  AND vr.status NOT IN ('CANCELLED','REJECTED')
ORDER BY vr.visit_request_id;

-- =====================================================================
-- END OF PREFLIGHT. If every SUMMARY violation_count is 0 (P08 is
-- informational — owner-less rows that stay PENDING_CONFIRMATION), proceed to
-- 02_up_additive.sql.
-- =====================================================================
