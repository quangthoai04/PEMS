-- =====================================================================================
-- PEMS — REMOVE OBSOLETE SYNTHETIC SECURITY SEED ROWS  (development / demo databases)
--
-- Seven seeded demo rows in security_events were written with failure_reason_code values
-- the runtime no longer emits:
--
--     CAMPUS_MISMATCH   PORTAL_MISMATCH   SUSPICIOUS_IP   UNKNOWN
--
-- They have been deleted from PEMS_FULL_VS_31_07_NEW.sql, so a FRESH import no longer
-- creates them. This script removes them from a database that was seeded earlier.
--
-- USAGE
--     mysql --default-character-set=utf8mb4 -u root -p <database> \
--           < PEMS_OBSOLETE_SECURITY_SEED_REMOVE.sql
--
-- =====================================================================================
-- WHY DELETE RATHER THAN REWRITE
--
-- These rows are demo fixtures, not history: every one carries a synthetic address
-- (@…example / @partner.example), a hand-set June 2026 timestamp and a seed-block id.
-- They were invented to make the Security Monitoring screen look populated, and the
-- scenarios they illustrate (a campus mismatch, a suspicious IP) no longer correspond to
-- anything the system can produce. Rewriting them to a code that IS producible would keep
-- a row that never described a real event; deleting them is the honest option.
--
-- =====================================================================================
-- WHAT IT DELETES — exactly seven rows, matched on full identity
--
--   id   email_snapshot                      ip_address     created_at
--    2   kim.minjae@seoultech.example        203.113.10.8   2026-06-23 07:28:00
--    4   staff.leader.hn@fpt.edu.vn          10.10.1.13     2026-06-21 08:30:00
--  203   staff.hn@fpt.edu.vn                 10.30.0.203    2026-06-21 10:00:00
--  302   visitor.security16@partner.example  10.31.0.3      2026-06-22 10:15:00
--  303   visitor.security17@partner.example  10.31.0.4      2026-06-22 11:15:00
--  310   visitor.security24@partner.example  10.31.0.11     2026-06-22 08:15:00
--  311   visitor.security25@partner.example  10.31.0.12     2026-06-22 09:15:00
--
-- The match is on id AND email AND ip AND created_at together — deliberately NOT on
-- failure_reason_code, because a database that already had the earlier alignment applied
-- now holds a corrected code on these same rows. All four fields agreeing is what proves
-- the row is the seed fixture rather than a genuine audit record that happens to share an
-- id. A real runtime row cannot satisfy all four: it would need a synthetic address AND a
-- 2026-06 timestamp AND an id inside the seed block.
--
-- Nothing else is touched. No other table, no other row, no schema change. Nothing
-- references security_events by foreign key, so the delete has no cascade.
--
-- The four strings ALSO exist as AuthErrorCodes — the HTTP errorCode a 403 returns, which
-- the frontend renders. Those are live and are unrelated to this script.
-- =====================================================================================

SET NAMES utf8mb4;

SELECT '=== BEFORE ===' AS stage;
SELECT COUNT(*) AS security_events_total,
       SUM(failure_reason_code IN ('CAMPUS_MISMATCH','PORTAL_MISMATCH','SUSPICIOUS_IP','UNKNOWN')) AS retired_reason_rows,
       SUM(detail_text LIKE '%legacy event_type=%') AS legacy_markers
FROM security_events;

SELECT 'rows this script will delete' AS what, COUNT(*) AS n FROM security_events
WHERE (security_event_id, email_snapshot, ip_address, created_at) IN (
  (  2, 'kim.minjae@seoultech.example',       '203.113.10.8', '2026-06-23 07:28:00'),
  (  4, 'staff.leader.hn@fpt.edu.vn',         '10.10.1.13',   '2026-06-21 08:30:00'),
  (203, 'staff.hn@fpt.edu.vn',                '10.30.0.203',  '2026-06-21 10:00:00'),
  (302, 'visitor.security16@partner.example', '10.31.0.3',    '2026-06-22 10:15:00'),
  (303, 'visitor.security17@partner.example', '10.31.0.4',    '2026-06-22 11:15:00'),
  (310, 'visitor.security24@partner.example', '10.31.0.11',   '2026-06-22 08:15:00'),
  (311, 'visitor.security25@partner.example', '10.31.0.12',   '2026-06-22 09:15:00'));

START TRANSACTION;

DELETE FROM security_events
WHERE (security_event_id, email_snapshot, ip_address, created_at) IN (
  (  2, 'kim.minjae@seoultech.example',       '203.113.10.8', '2026-06-23 07:28:00'),
  (  4, 'staff.leader.hn@fpt.edu.vn',         '10.10.1.13',   '2026-06-21 08:30:00'),
  (203, 'staff.hn@fpt.edu.vn',                '10.30.0.203',  '2026-06-21 10:00:00'),
  (302, 'visitor.security16@partner.example', '10.31.0.3',    '2026-06-22 10:15:00'),
  (303, 'visitor.security17@partner.example', '10.31.0.4',    '2026-06-22 11:15:00'),
  (310, 'visitor.security24@partner.example', '10.31.0.11',   '2026-06-22 08:15:00'),
  (311, 'visitor.security25@partner.example', '10.31.0.12',   '2026-06-22 09:15:00'));

COMMIT;

SELECT '=== AFTER (retired_reason_rows expected 0) ===' AS stage;
SELECT COUNT(*) AS security_events_total,
       SUM(failure_reason_code IN ('CAMPUS_MISMATCH','PORTAL_MISMATCH','SUSPICIOUS_IP','UNKNOWN')) AS retired_reason_rows,
       SUM(detail_text LIKE '%legacy event_type=%') AS legacy_markers
FROM security_events;

SELECT '=== the seven ids are gone (expected 0 rows) ===' AS stage;
SELECT security_event_id, email_snapshot FROM security_events
WHERE security_event_id IN (2, 4, 203, 302, 303, 310, 311);

SELECT 'OBSOLETE SYNTHETIC SECURITY SEED REMOVAL — DONE' AS status;
