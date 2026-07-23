-- =====================================================================
-- Patch: Update trg_visit_photos_validate_bi to allow Host, Staff, Admin, and Participants
--
-- Root cause: The legacy trigger required NEW.uploaded_by to be a STUDENT.
-- This patch updates the trigger so IC Staff, Host, Staff Leader, Admin, Supporting Staff,
-- and Student participants can all upload photos to the delegation album.
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
  JOIN roles r ON r.role_id = u.role_id
  LEFT JOIN visit_participants vp ON vp.visit_instance_id = NEW.visit_instance_id
    AND vp.user_id = NEW.uploaded_by
    AND (vp.status = 'ACCEPTED' OR vp.status = 'ASSIGNED')
  WHERE vrc.visit_instance_id = NEW.visit_instance_id
    AND u.status = 'ACTIVE'
    AND (
      vrc.current_host_user_id = NEW.uploaded_by
      OR r.role_code IN ('ADMIN', 'STAFF')
      OR vp.participant_id IS NOT NULL
    );

  IF v_valid_participant = 0 THEN
    SIGNAL SQLSTATE '45000'
      SET MESSAGE_TEXT = 'Visit photo uploader must be an ACTIVE user who is Host, Staff, Admin, or Participant of this visit instance';
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
