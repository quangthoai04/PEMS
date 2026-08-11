-- =====================================================================================
-- PEMS — SAFE ENUM + DEFAULT CLEANUP (files.storage_provider, partners defaults)
--
-- Three metadata-only changes, all verified against the code that actually runs:
--
--   1. files.storage_provider  ENUM('LOCAL','S3','AZURE','GCS','GOOGLE_DRIVE','OTHER')
--                            → ENUM('LOCAL','GOOGLE_DRIVE','OTHER')
--   2. partners.profile_status DEFAULT 'APPROVED'  → DEFAULT 'PENDING_APPROVAL'
--   3. partners.visibility     DEFAULT 'PUBLIC'    → DEFAULT 'INTERNAL'
--
-- No column is added or removed, no row is rewritten, no ENUM value that carries data is
-- touched, and no approval workflow changes.
--
-- Safe to run more than once: each statement is guarded by information_schema, so a second
-- run reports the same summary and changes nothing.
--
-- USAGE
--     mysql --default-character-set=utf8mb4 -u root -p <database> \
--           < PEMS_SQL_CLEANUP_SAFE_ENUM_DEFAULT_UP.sql
--
-- =====================================================================================
-- 1. WHY S3 / AZURE / GCS GO — and why LOCAL STAYS
--
-- Removed values: nothing writes them, nothing reads them, nothing validates against them.
-- Measured across the whole repository, the only occurrences are this DDL, a TypeScript
-- union in the documents feature, and prose in docs/. There is no producer: the two upload
-- paths write 'GOOGLE_DRIVE' (FileUploadService, UploadFileCommandHandler Drive branch) or
-- 'LOCAL' (LocalFileStorageService.SaveAsync); the YouTube embed path writes 'OTHER'
-- (GalleryExternalMediaService). There is no consumer: every read branches on
-- 'GOOGLE_DRIVE' or 'LOCAL', with a generic remote-URL fallback for anything else.
--
-- LOCAL IS KEPT AND IS NOT LEGACY. LocalFileStorageService.SaveAsync returns
-- StoredFileInfo("LOCAL", …) for every non-Drive purpose, OpenReadAsync branches on it to
-- read bytes from disk, and UploadFileCommandHandler builds the /api/files/{id}/download
-- URL for exactly those rows. Removing it would mean replacing the disk-storage path, which
-- is a storage-architecture change and explicitly out of scope here.
--
-- DATA GUARD, NOT AN ASSUMPTION. This script refuses to run on any database that still
-- holds an S3/AZURE/GCS row rather than letting MySQL coerce those rows to ''. That case is
-- real, not hypothetical: pems_pr3_test, pems_audit_new and pems_baseline_probe each still
-- hold 2/1/2 such rows from an older seed lineage (docs/database/scripts/phase_1_candidate/
-- 00_fresh_target.sql, file_purpose 'ENUM_STORAGE_COVERAGE'), and that script is a
-- self-contained fresh-create that was never imported or verified. Those databases must be
-- reseeded or have their rows resolved by an owner decision before this script can apply —
-- it will not rewrite them.
--
-- 2. WHY THE partners DEFAULTS CHANGE — fail-closed, matching the coded invariant
--
-- CreatePartnerCommandHandler is the ONLY production INSERT path (CreatePartnerFromGuest
-- delegates to it). It already writes profile_status = 'PENDING_APPROVAL' explicitly, and
-- refuses visibility = 'PUBLIC' outright with PARTNER_PUBLIC_REQUIRES_APPROVED, defaulting
-- to 'INTERNAL'. The canonical seed names both columns explicitly. So the DB defaults are
-- reached only by hand-written SQL — and there they currently mint an APPROVED, PUBLIC
-- partner that never passed approval. That is the one case the column defaults decide, and
-- it should fail closed.
--
-- The ENUM itself is NOT shrunk: DRAFT stays (2 live rows, plus a filter, a label and a
-- badge in the partner management screen), as do PENDING_APPROVAL, APPROVED and REJECTED.
-- ALTER … SET DEFAULT is metadata-only in MySQL 8 — it does not rebuild the table and does
-- not touch a single existing row.
-- =====================================================================================

SET NAMES utf8mb4;

-- ── PRECHECK ──────────────────────────────────────────────────────────────────────────
SELECT '=== PRECHECK: files.storage_provider distribution ===' AS stage;
SELECT storage_provider, COUNT(*) AS rows_now FROM files GROUP BY storage_provider ORDER BY storage_provider;

SELECT '=== PRECHECK: rows holding a value about to be removed (MUST be 0) ===' AS stage;
SELECT COUNT(*) AS blocking_rows FROM files WHERE storage_provider IN ('S3','AZURE','GCS');

SELECT '=== PRECHECK: current column definitions ===' AS stage;
SELECT TABLE_NAME, COLUMN_NAME, COLUMN_TYPE, COLUMN_DEFAULT
FROM information_schema.COLUMNS
WHERE TABLE_SCHEMA = DATABASE()
  AND ((TABLE_NAME = 'files'    AND COLUMN_NAME = 'storage_provider')
    OR (TABLE_NAME = 'partners' AND COLUMN_NAME IN ('profile_status','visibility')))
ORDER BY TABLE_NAME, COLUMN_NAME;

SELECT '=== PRECHECK: partners.profile_status distribution (DRAFT is KEPT) ===' AS stage;
SELECT profile_status, COUNT(*) AS rows_now FROM partners GROUP BY profile_status ORDER BY profile_status;

-- ── DATA GUARD ────────────────────────────────────────────────────────────────────────
DROP PROCEDURE IF EXISTS pems_enum_default_cleanup;

DELIMITER $$

CREATE PROCEDURE pems_enum_default_cleanup()
BEGIN
  DECLARE blocking INT DEFAULT 0;
  DECLARE current_type VARCHAR(255);

  -- HARD STOP: never coerce a real row to ''. MySQL would happily do that under a
  -- non-strict sql_mode, which is exactly the silent data loss this guard exists to prevent.
  SELECT COUNT(*) INTO blocking FROM files WHERE storage_provider IN ('S3','AZURE','GCS');
  IF blocking > 0 THEN
    SIGNAL SQLSTATE '45000'
      SET MESSAGE_TEXT = 'Refused: files still holds S3/AZURE/GCS rows. Resolve or reseed them first — this script will not rewrite data.';
  END IF;

  -- 1. Shrink the ENUM, but only when it is not already the target shape (idempotency).
  SELECT COLUMN_TYPE INTO current_type FROM information_schema.COLUMNS
  WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'files' AND COLUMN_NAME = 'storage_provider';

  IF current_type <> "enum('LOCAL','GOOGLE_DRIVE','OTHER')" THEN
    ALTER TABLE `files`
      MODIFY `storage_provider` ENUM('LOCAL','GOOGLE_DRIVE','OTHER') NOT NULL DEFAULT 'LOCAL';
  END IF;

  -- 2 + 3. Defaults. ALTER … SET DEFAULT is metadata-only: no table rebuild, no row touched.
  IF EXISTS (SELECT 1 FROM information_schema.COLUMNS
             WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'partners'
               AND COLUMN_NAME = 'profile_status' AND COLUMN_DEFAULT <> 'PENDING_APPROVAL') THEN
    ALTER TABLE `partners` ALTER COLUMN `profile_status` SET DEFAULT 'PENDING_APPROVAL';
  END IF;

  IF EXISTS (SELECT 1 FROM information_schema.COLUMNS
             WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'partners'
               AND COLUMN_NAME = 'visibility' AND COLUMN_DEFAULT <> 'INTERNAL') THEN
    ALTER TABLE `partners` ALTER COLUMN `visibility` SET DEFAULT 'INTERNAL';
  END IF;
END$$

DELIMITER ;

CALL pems_enum_default_cleanup();
DROP PROCEDURE pems_enum_default_cleanup;

-- ── VERIFY ────────────────────────────────────────────────────────────────────────────
SELECT '=== VERIFY: files.storage_provider is the 3-value ENUM, default LOCAL ===' AS verify;
SELECT COLUMN_TYPE, COLUMN_DEFAULT, IS_NULLABLE FROM information_schema.COLUMNS
WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'files' AND COLUMN_NAME = 'storage_provider';

SELECT '=== VERIFY: partners defaults are PENDING_APPROVAL / INTERNAL ===' AS verify;
SELECT COLUMN_NAME, COLUMN_TYPE, COLUMN_DEFAULT FROM information_schema.COLUMNS
WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'partners'
  AND COLUMN_NAME IN ('profile_status','visibility')
ORDER BY COLUMN_NAME;

SELECT '=== VERIFY: profile_status ENUM still carries all four values, DRAFT included ===' AS verify;
SELECT CASE WHEN COLUMN_TYPE = "enum('DRAFT','PENDING_APPROVAL','APPROVED','REJECTED')"
            THEN 'OK — DRAFT retained' ELSE CONCAT('UNEXPECTED: ', COLUMN_TYPE) END AS profile_status_enum
FROM information_schema.COLUMNS
WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'partners' AND COLUMN_NAME = 'profile_status';

SELECT '=== VERIFY: no files row lost its provider (expect 0) ===' AS verify;
SELECT COUNT(*) AS blank_provider_rows FROM files WHERE storage_provider = '';

SELECT '=== VERIFY: row counts unchanged by this script ===' AS verify;
SELECT (SELECT COUNT(*) FROM files) AS files_rows,
       (SELECT COUNT(*) FROM partners) AS partners_rows,
       (SELECT COUNT(*) FROM partners WHERE profile_status = 'DRAFT') AS draft_partners_kept;

SELECT 'PEMS SAFE ENUM + DEFAULT CLEANUP — DONE' AS status;
