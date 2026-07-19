-- =============================================================================
-- Migration B (CLEANUP) — remove the EverAI TTS mechanism + the legacy single
-- description column, now that the bilingual-content code is live.
--
-- Run this ONLY after Migration A + the new application build are deployed.
--
-- Steps (idempotent, guarded with information_schema checks):
--   1. Drop the gallery_item_tts_audios table (EverAI narration history).
--   2. Delete old seed gallery items that have no bilingual content row — they
--      are no longer valid (each item now requires VI+EN description + audio).
--      The FK cascades delete their gallery_item_media + gallery_item_contents.
--   3. Drop the FULLTEXT index that includes `description`, then drop the
--      `gallery_items.description` column, then recreate the FULLTEXT on title.
--
-- Target: pems_db (MySQL 8.0, InnoDB, utf8mb4).
-- =============================================================================

SET @schema := DATABASE();

-- ── 1. Drop EverAI TTS table ────────────────────────────────────────────────
DROP TABLE IF EXISTS gallery_item_tts_audios;

-- ── 2. Delete legacy seed items with no bilingual content (cascades media) ──
DELETE FROM gallery_items
WHERE gallery_item_id NOT IN (SELECT gallery_item_id FROM gallery_item_contents);

-- ── 3a. Drop the FULLTEXT index that references `description` (if present) ──
SET @ft_exists := (
  SELECT COUNT(*) FROM information_schema.statistics
  WHERE table_schema = @schema AND table_name = 'gallery_items'
    AND index_name = 'ft_gallery_items_search'
);
SET @sql := IF(@ft_exists > 0,
  'ALTER TABLE gallery_items DROP INDEX ft_gallery_items_search',
  'SELECT ''ft_gallery_items_search already absent'' AS note');
PREPARE s FROM @sql; EXECUTE s; DEALLOCATE PREPARE s;

-- ── 3b. Drop the legacy `description` column (if present) ──
SET @col_exists := (
  SELECT COUNT(*) FROM information_schema.columns
  WHERE table_schema = @schema AND table_name = 'gallery_items'
    AND column_name = 'description'
);
SET @sql := IF(@col_exists > 0,
  'ALTER TABLE gallery_items DROP COLUMN description',
  'SELECT ''gallery_items.description already dropped'' AS note');
PREPARE s FROM @sql; EXECUTE s; DEALLOCATE PREPARE s;

-- ── 3c. Recreate the FULLTEXT index on title only (search now spans the
--        bilingual descriptions via ft_gallery_item_contents_descriptions) ──
SET @ft_exists := (
  SELECT COUNT(*) FROM information_schema.statistics
  WHERE table_schema = @schema AND table_name = 'gallery_items'
    AND index_name = 'ft_gallery_items_search'
);
SET @sql := IF(@ft_exists = 0,
  'ALTER TABLE gallery_items ADD FULLTEXT KEY ft_gallery_items_search (title)',
  'SELECT ''ft_gallery_items_search already present'' AS note');
PREPARE s FROM @sql; EXECUTE s; DEALLOCATE PREPARE s;
