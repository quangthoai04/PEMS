-- =====================================================================
-- PEMS — PER-CAMPUS FORM v2 — BACKFILL  (IDEMPOTENT)
-- Plan §4.3, §17.4, §16.4. Run AFTER 02_up_additive.sql.
--
-- Every statement is `INSERT ... SELECT ... WHERE NOT EXISTS` or a guarded
-- UPDATE, so re-running is safe and never duplicates. For very large tables,
-- wrap each INSERT in a keyed batch loop (WHERE visit_request_id BETWEEN ...);
-- the WHERE NOT EXISTS guard makes each batch independently re-runnable.
--
-- Semantics preserved from v1:
--   * Each campus instance gets ONE detail row cloned from the global request form.
--   * Each existing guest/support member is linked to EVERY instance of its request
--     (the v1 "shared list" semantics). Copy-on-write happens later, at first edit.
--   * primary_contact_access_status becomes ACTIVE only where an owner already exists.
--   * Legacy rows keep form_schema_version = 1 (the column default); new v2 writes set 2.
-- =====================================================================

-- ---------------------------------------------------------------------
-- 1. Per-instance form detail — clone the global request form into one row
--    per campus instance. visit_type/other, purpose, working_content, language,
--    media and notes come from visit_requests; operational_contact_* is seeded
--    from the request's single contact_person_* (per-campus contacts diverge later).
-- ---------------------------------------------------------------------
INSERT INTO visit_instance_form_details (
  visit_instance_id,
  delegation_name, visit_type, visit_type_other, purpose, working_content,
  operational_contact_full_name, operational_contact_organization,
  operational_contact_phone, operational_contact_email,
  working_language, transportation_note, media_consent_status, media_consent_note, note_to_fptu,
  form_revision, approval_revision, row_version,
  created_at, created_by, updated_at, updated_by
)
SELECT
  vrc.visit_instance_id,
  vr.delegation_name, vr.visit_type, vr.visit_type_other, vr.purpose, vr.working_content,
  vr.contact_person_full_name, vr.contact_person_organization,
  vr.contact_person_phone, vr.contact_person_email,
  vr.working_language, vr.transportation_note, vr.media_consent_status, vr.media_consent_note, vr.note_to_fptu,
  1, 1, 0,
  vrc.created_at, vr.created_by, NULL, NULL
FROM visit_request_campuses vrc
JOIN visit_requests vr ON vr.visit_request_id = vrc.visit_request_id
WHERE NOT EXISTS (
  SELECT 1 FROM visit_instance_form_details d WHERE d.visit_instance_id = vrc.visit_instance_id
);

-- ---------------------------------------------------------------------
-- 2. Guest/support member links — link every existing member to every campus
--    instance of the SAME request (v1 shared-list semantics). display_order is
--    carried from the member row.
-- ---------------------------------------------------------------------
INSERT INTO visit_instance_guest_members (
  visit_request_id, visit_instance_id, guest_member_id, display_order, created_at, created_by
)
SELECT
  m.visit_request_id, vrc.visit_instance_id, m.guest_member_id, m.display_order,
  vrc.created_at, m.created_by
FROM visit_guest_members m
JOIN visit_request_campuses vrc ON vrc.visit_request_id = m.visit_request_id
WHERE NOT EXISTS (
  SELECT 1 FROM visit_instance_guest_members l
  WHERE l.visit_instance_id = vrc.visit_instance_id
    AND l.guest_member_id = m.guest_member_id
);

-- ---------------------------------------------------------------------
-- 3. Primary-contact access status — ACTIVE where an owner exists (§16.4).
--    Owner-less rows keep the PENDING_CONFIRMATION default (see preflight P08).
--    verified_at falls back to email_verified_at, then submitted_at.
-- ---------------------------------------------------------------------
UPDATE visit_requests
SET primary_contact_access_status = 'ACTIVE',
    primary_contact_verified_at = COALESCE(primary_contact_verified_at, email_verified_at, submitted_at)
WHERE visitor_user_id IS NOT NULL
  AND primary_contact_access_status <> 'ACTIVE';

-- ---------------------------------------------------------------------
-- 4. Baseline revision history — one immutable MIGRATION snapshot per instance
--    so post-cutover diffs have a starting point. Guarded by the unique
--    (visit_instance_id, form_revision) key.
-- ---------------------------------------------------------------------
INSERT INTO visit_instance_form_revision_history (
  visit_request_id, visit_instance_id, form_revision, approval_revision,
  source_type, source_id, snapshot_json, applied_by, applied_at, reason
)
SELECT
  d.req_id, d.visit_instance_id, 1, 1,
  'MIGRATION', NULL,
  JSON_OBJECT(
    'delegationName', d.delegation_name,
    'visitType', d.visit_type,
    'visitTypeOther', d.visit_type_other,
    'purpose', d.purpose,
    'workingContent', d.working_content,
    'operationalContact', JSON_OBJECT(
      'fullName', d.operational_contact_full_name,
      'organization', d.operational_contact_organization,
      'phone', d.operational_contact_phone,
      'email', d.operational_contact_email),
    'workingLanguage', d.working_language,
    'transportationNote', d.transportation_note,
    'mediaConsentStatus', d.media_consent_status,
    'mediaConsentNote', d.media_consent_note,
    'noteToFptu', d.note_to_fptu
  ),
  NULL, d.created_at, 'Backfill from v1 global form'
FROM (
  SELECT vifd.*, vrc.visit_request_id AS req_id
  FROM visit_instance_form_details vifd
  JOIN visit_request_campuses vrc ON vrc.visit_instance_id = vifd.visit_instance_id
) d
WHERE NOT EXISTS (
  SELECT 1 FROM visit_instance_form_revision_history h
  WHERE h.visit_instance_id = d.visit_instance_id AND h.form_revision = 1
);

-- =====================================================================
-- END OF BACKFILL. Next: 04_verify.sql
-- =====================================================================
