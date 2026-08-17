-- =====================================================================
-- Patch: SEC-14 — trg_visit_photos_validate_bi must match the C# uploader rule exactly
--
-- Root cause: the 2026-07-22 patch let (a) any ADMIN/STAFF role upload regardless of any
-- relationship to the instance, and (b) an ASSIGNED (not yet accepted) participant upload. The
-- application layer (VisitPhotoStudentScope.ResolveAcceptedStudentAsync) has been narrowed to the
-- chốt business rule — Host OR ACCEPTED-status participant of that EXACT visit instance, nothing
-- else — and this patch brings the trigger back in sync so a caller the app approves is never
-- rejected by the trigger (and vice versa), which would otherwise surface as a raw SIGNAL 45000
-- (500) instead of a clean 403, or as a silent DB-layer over-permission the app fix alone would not
-- close.
-- =====================================================================

DELIMITER $$

DROP TRIGGER IF EXISTS trg_visit_photos_validate_bi$$

CREATE TRIGGER trg_visit_photos_validate_bi
BEFORE INSERT ON visit_photos
FOR EACH ROW
BEGIN
  DECLARE v_valid_participant INT DEFAULT 0;
  DECLARE v_valid_file INT DEFAULT 0;

  SELECT COUNT(*) INTO v_valid_participant
  FROM visit_request_campuses vrc
  JOIN users u ON u.user_id = NEW.uploaded_by
  LEFT JOIN visit_participants vp ON vp.visit_instance_id = NEW.visit_instance_id
    AND vp.user_id = NEW.uploaded_by
    AND vp.status = 'ACCEPTED'
  WHERE vrc.visit_instance_id = NEW.visit_instance_id
    AND u.status = 'ACTIVE'
    AND (
      vrc.current_host_user_id = NEW.uploaded_by
      OR vp.participant_id IS NOT NULL
    );

  IF v_valid_participant = 0 THEN
    SIGNAL SQLSTATE '45000'
      SET MESSAGE_TEXT = 'Visit photo uploader must be an ACTIVE user who is Host or an ACCEPTED participant of this visit instance';
  END IF;

  SELECT COUNT(*) INTO v_valid_file
  FROM files f
  WHERE f.file_id = NEW.file_id
    AND f.storage_provider = 'GOOGLE_DRIVE'
    AND f.file_purpose = 'VISIT_REQUEST_PHOTO'
    AND f.mime_type LIKE 'image/%';

  IF v_valid_file = 0 THEN
    SIGNAL SQLSTATE '45000'
      SET MESSAGE_TEXT = 'Visit photo file must be a Google Drive image with VISIT_REQUEST_PHOTO purpose';
  END IF;

  IF NOT (
    (NEW.status = 'ACTIVE'
      AND NEW.removed_at IS NULL
      AND NEW.removed_by IS NULL
      AND NEW.removal_reason IS NULL)
    OR
    (NEW.status = 'REMOVED'
      AND NEW.removed_at IS NOT NULL
      AND NEW.removed_by IS NOT NULL
      AND NEW.removal_reason IS NOT NULL
      AND TRIM(NEW.removal_reason) <> '')
  ) THEN
    SIGNAL SQLSTATE '45000'
      SET MESSAGE_TEXT = 'Visit photo removal metadata is inconsistent with status';
  END IF;
END$$

DELIMITER ;
