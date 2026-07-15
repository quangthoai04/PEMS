-- =====================================================================
-- PEMS — PER-CAMPUS FORM v2 — POST-BACKFILL VERIFY  (REPORT ONLY)
-- Plan §13, §22.1. Run AFTER 03_backfill.sql. Every violation_count MUST be 0
-- (V07/V10/V11 are presence checks and MUST be 1). No mutation here.
-- =====================================================================

SELECT 'V01_instances_without_detail' AS check_name,
       COUNT(*) AS violation_count,
       'each campus instance must have exactly one detail row' AS note
FROM visit_request_campuses vrc
WHERE NOT EXISTS (SELECT 1 FROM visit_instance_form_details d WHERE d.visit_instance_id = vrc.visit_instance_id)
UNION ALL
-- A detail whose instance is gone (FK should prevent; belt-and-braces).
SELECT 'V02_detail_orphan', COUNT(*),
       'detail rows with no matching campus instance'
FROM visit_instance_form_details d
WHERE NOT EXISTS (SELECT 1 FROM visit_request_campuses vrc WHERE vrc.visit_instance_id = d.visit_instance_id)
UNION ALL
-- Link count must equal SUM over requests of (members * instances).
SELECT 'V03_member_link_count_mismatch', ABS(actual_links - expected_links),
       CONCAT('expected ', expected_links, ' links, found ', actual_links)
FROM (
  SELECT
    (SELECT COUNT(*) FROM visit_instance_guest_members) AS actual_links,
    (SELECT COALESCE(SUM(mc.cnt * ic.cnt), 0)
       FROM (SELECT visit_request_id, COUNT(*) cnt FROM visit_guest_members GROUP BY visit_request_id) mc
       JOIN (SELECT visit_request_id, COUNT(*) cnt FROM visit_request_campuses GROUP BY visit_request_id) ic
         ON ic.visit_request_id = mc.visit_request_id) AS expected_links
) t
UNION ALL
-- Cross-request contamination: a link's request must equal BOTH the instance's
-- request and the member's request. Composite FKs prevent it; verify anyway.
SELECT 'V04_link_cross_request', COUNT(*),
       'link whose request id disagrees with its instance or its member'
FROM visit_instance_guest_members l
LEFT JOIN visit_request_campuses vrc ON vrc.visit_instance_id = l.visit_instance_id
LEFT JOIN visit_guest_members m ON m.guest_member_id = l.guest_member_id
WHERE vrc.visit_request_id IS NULL
   OR m.visit_request_id IS NULL
   OR vrc.visit_request_id <> l.visit_request_id
   OR m.visit_request_id <> l.visit_request_id
UNION ALL
-- Access-status backfill: every owned request must be ACTIVE.
SELECT 'V05_owned_request_not_active', COUNT(*),
       'visitor_user_id set but access status still PENDING_CONFIRMATION'
FROM visit_requests
WHERE visitor_user_id IS NOT NULL AND primary_contact_access_status <> 'ACTIVE'
UNION ALL
-- Owner-less requests must NOT have been flipped to ACTIVE.
SELECT 'V06_ownerless_request_active', COUNT(*),
       'visitor_user_id NULL but access status ACTIVE (should stay PENDING_CONFIRMATION)'
FROM visit_requests
WHERE visitor_user_id IS NULL AND primary_contact_access_status = 'ACTIVE'
UNION ALL
-- Duration constraint present (presence check: must be 1).
SELECT 'V07_duration_constraint_present', COUNT(*),
       'ck_visit_instance_min_duration_30m must exist (expect 1)'
FROM information_schema.TABLE_CONSTRAINTS
WHERE TABLE_SCHEMA = DATABASE()
  AND TABLE_NAME = 'visit_request_campuses'
  AND CONSTRAINT_NAME = 'ck_visit_instance_min_duration_30m'
UNION ALL
-- Migrated detail must match its parent global form (checksum on the copied fields).
SELECT 'V08_detail_vs_parent_mismatch', COUNT(*),
       'form_revision=1 detail whose copied fields differ from the parent request (only meaningful pre-edit)'
FROM visit_instance_form_details d
JOIN visit_request_campuses vrc ON vrc.visit_instance_id = d.visit_instance_id
JOIN visit_requests vr ON vr.visit_request_id = vrc.visit_request_id
WHERE d.form_revision = 1
  AND MD5(CONCAT_WS('|',
        d.delegation_name, d.visit_type, COALESCE(d.visit_type_other,''), d.purpose,
        COALESCE(d.working_content,''), d.operational_contact_full_name,
        d.operational_contact_organization, d.operational_contact_phone, d.operational_contact_email,
        d.working_language, COALESCE(d.transportation_note,''), d.media_consent_status,
        COALESCE(d.media_consent_note,''), COALESCE(d.note_to_fptu,'')))
   <> MD5(CONCAT_WS('|',
        vr.delegation_name, vr.visit_type, COALESCE(vr.visit_type_other,''), vr.purpose,
        COALESCE(vr.working_content,''), vr.contact_person_full_name,
        vr.contact_person_organization, vr.contact_person_phone, vr.contact_person_email,
        vr.working_language, COALESCE(vr.transportation_note,''), vr.media_consent_status,
        COALESCE(vr.media_consent_note,''), COALESCE(vr.note_to_fptu,'')))
UNION ALL
-- Every instance must have its MIGRATION baseline revision.
SELECT 'V09_missing_baseline_revision', COUNT(*),
       'campus instance with no form_revision=1 history row'
FROM visit_request_campuses vrc
WHERE NOT EXISTS (
  SELECT 1 FROM visit_instance_form_revision_history h
  WHERE h.visit_instance_id = vrc.visit_instance_id AND h.form_revision = 1)
UNION ALL
-- v2 completeness: a form_schema_version=2 request must have a detail row AND a baseline
-- revision for EVERY instance (its per-campus data may not fall back to the global columns).
SELECT 'V12_v2_request_incomplete', COUNT(DISTINCT vrc.visit_request_id),
       'form_schema_version=2 request with an instance missing detail or baseline revision'
FROM visit_requests vr
JOIN visit_request_campuses vrc ON vrc.visit_request_id = vr.visit_request_id
WHERE vr.form_schema_version = 2
  AND (NOT EXISTS (SELECT 1 FROM visit_instance_form_details d WHERE d.visit_instance_id = vrc.visit_instance_id)
    OR NOT EXISTS (SELECT 1 FROM visit_instance_form_revision_history h
                   WHERE h.visit_instance_id = vrc.visit_instance_id AND h.form_revision = 1))
UNION ALL
-- has_mixed_campus_details must match reality: count distinct per-campus core snapshots per
-- request. distinct>1 => must be flagged mixed(1); distinct<=1 => must be flagged 0.
SELECT 'V13_mixed_flag_mismatch', COUNT(*),
       'has_mixed_campus_details disagrees with the actual number of distinct per-campus snapshots'
FROM visit_requests vr
JOIN (
  SELECT vrc.visit_request_id,
         COUNT(DISTINCT MD5(CONCAT_WS('|',
           d.delegation_name, d.visit_type, COALESCE(d.visit_type_other,''), d.purpose,
           COALESCE(d.working_content,''), d.operational_contact_full_name,
           d.operational_contact_organization, d.operational_contact_phone, d.operational_contact_email,
           d.working_language, COALESCE(d.transportation_note,''), d.media_consent_status,
           COALESCE(d.media_consent_note,''), COALESCE(d.note_to_fptu,'')))) AS sig_count
  FROM visit_instance_form_details d
  JOIN visit_request_campuses vrc ON vrc.visit_instance_id = d.visit_instance_id
  GROUP BY vrc.visit_request_id
) s ON s.visit_request_id = vr.visit_request_id
WHERE (s.sig_count > 1 AND vr.has_mixed_campus_details = 0)
   OR (s.sig_count <= 1 AND vr.has_mixed_campus_details = 1)
UNION ALL
-- Pending-guard integrity: at most one PENDING identity change per (request, relation).
SELECT 'V14_duplicate_pending_identity', COUNT(*),
       'more than one PENDING identity change for the same (request, relation)'
FROM (
  SELECT visit_request_id, target_relation
  FROM visit_request_identity_changes
  WHERE status = 'PENDING'
  GROUP BY visit_request_id, target_relation
  HAVING COUNT(*) > 1
) d
UNION ALL
-- Pending-guard integrity: at most one PENDING_APPROVAL amendment per instance.
SELECT 'V15_duplicate_pending_amendment', COUNT(*),
       'more than one PENDING_APPROVAL amendment for the same instance'
FROM (
  SELECT visit_instance_id
  FROM visit_instance_amendments
  WHERE status = 'PENDING_APPROVAL'
  GROUP BY visit_instance_id
  HAVING COUNT(*) > 1
) a
UNION ALL
-- Composite unique keys present (presence checks: must be 1 each).
SELECT 'V10_uq_vrc_request_instance_present', COUNT(*),
       'uq_vrc_request_instance must exist (expect 1)'
FROM information_schema.STATISTICS
WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'visit_request_campuses'
  AND INDEX_NAME = 'uq_vrc_request_instance' AND SEQ_IN_INDEX = 1
UNION ALL
SELECT 'V11_uq_vgm_request_member_present', COUNT(*),
       'uq_vgm_request_member must exist (expect 1)'
FROM information_schema.STATISTICS
WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'visit_guest_members'
  AND INDEX_NAME = 'uq_vgm_request_member' AND SEQ_IN_INDEX = 1;

-- ---------------------------------------------------------------------
-- New-table presence roll-up (each expected_present should be 1).
-- ---------------------------------------------------------------------
SELECT t.name AS table_name,
       CASE WHEN it.TABLE_NAME IS NULL THEN 0 ELSE 1 END AS expected_present
FROM (
  SELECT 'visit_instance_form_details' AS name UNION ALL
  SELECT 'visit_instance_guest_members' UNION ALL
  SELECT 'visit_request_identity_changes' UNION ALL
  SELECT 'visit_request_identity_change_events' UNION ALL
  SELECT 'visit_instance_amendments' UNION ALL
  SELECT 'visit_instance_amendment_changes' UNION ALL
  SELECT 'visit_instance_form_revision_history' UNION ALL
  SELECT 'visit_request_revision_history'
) t
LEFT JOIN information_schema.TABLES it
  ON it.TABLE_SCHEMA = DATABASE() AND it.TABLE_NAME = t.name
ORDER BY t.name;

-- =====================================================================
-- END OF VERIFY. All violation_count = 0 and all presence checks = 1 → cutover ready.
-- =====================================================================
