-- =====================================================================
-- Patch: add VISIT_INSTANCE_MEDIA to documents.owner_type
--
-- Root cause: the student "Đóng góp kết quả" photo-upload feature writes and reads
-- documents.owner_type = 'VISIT_INSTANCE_MEDIA' (UploadVisitPhotosCommandHandler and
-- GetVisitInstanceContributionQueryHandler), but the master schema declared the column as
--   ENUM('GENERAL','VISIT','PARTNER','MINUTES','NEWS','LOGISTICS','REPORT')
-- with no such value. Every photo upload therefore failed with
--   MySQL 1265 "Data truncated for column 'owner_type'".
--
-- The code is internally consistent (same value on write and read) and deliberately keeps
-- visit-instance media as its OWN owner type, distinct from generic 'VISIT' documents. The
-- correct fix is to extend the enum, not to overload 'VISIT'.
--
-- Idempotent by construction: MODIFY COLUMN sets the column to exactly this definition, so
-- re-running is a no-op. It only WIDENS the enum (appends one value), so existing rows,
-- the NOT NULL constraint and the default are all unaffected. No dynamic SQL — the plain
-- MODIFY passes the import safety guard.
-- =====================================================================

ALTER TABLE `documents`
  MODIFY COLUMN `owner_type`
    ENUM('GENERAL','VISIT','PARTNER','MINUTES','NEWS','LOGISTICS','REPORT','VISIT_INSTANCE_MEDIA')
    NOT NULL DEFAULT 'GENERAL';

-- Verify.
SELECT CONCAT('documents.owner_type = ', COLUMN_TYPE) AS result
FROM information_schema.columns
WHERE table_schema = DATABASE() AND table_name = 'documents' AND column_name = 'owner_type';
