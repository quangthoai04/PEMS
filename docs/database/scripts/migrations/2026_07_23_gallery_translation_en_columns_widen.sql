-- ============================================================================
-- PEMS – Gallery automatic translation: widen the persisted English columns.
-- Date: 2026-07-23
--
-- Why: the Gallery auto-translation feature persists the Google-translated
-- English strings in area_name_en / location_name_en / title_en. A translated
-- English string can be longer than its Vietnamese source and must never be
-- truncated (the business rule marks the translation FAILED instead of cutting
-- the string), so the EN columns get headroom over the VI columns:
--   gallery_areas.area_name_en         VARCHAR(150) -> VARCHAR(255)
--   gallery_locations.location_name_en VARCHAR(150) -> VARCHAR(255)
--   gallery_items.title_en             VARCHAR(255) -> VARCHAR(500)
--
-- Additive / idempotent-safe: MODIFY COLUMN only widens (no data loss); running
-- it twice is harmless. The Vietnamese columns are NOT touched.
-- Matches the full baseline: PEMS_FULL_V2_I18N_GOOGLE_VISION_FACE_SCAN_COMPLETE_FIXED.sql
-- ============================================================================

ALTER TABLE gallery_areas
  MODIFY COLUMN area_name_en VARCHAR(255) NULL COMMENT 'Tên tiếng Anh đã dịch và lưu trong DB';

ALTER TABLE gallery_locations
  MODIFY COLUMN location_name_en VARCHAR(255) NULL COMMENT 'Tên tiếng Anh đã dịch và lưu trong DB';

ALTER TABLE gallery_items
  MODIFY COLUMN title_en VARCHAR(500) NULL COMMENT 'Tiêu đề tiếng Anh đã dịch và lưu trong DB';
