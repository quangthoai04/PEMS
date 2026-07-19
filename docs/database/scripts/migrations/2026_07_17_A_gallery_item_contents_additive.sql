-- =============================================================================
-- Migration A (ADDITIVE) — Gallery Item bilingual content (VI/EN + manual audio)
-- Replaces the EverAI TTS narration mechanism with two Staff-Leader-uploaded
-- audio recordings (Vietnamese + English) plus two descriptions.
--
-- This phase is purely additive: it creates gallery_item_contents (1:1 with
-- gallery_items) and leaves gallery_items.description + gallery_item_tts_audios
-- in place so the running application never breaks mid-deploy. The cleanup
-- (drop tts table + description column) happens in Migration B AFTER the new
-- code is live.
--
-- Idempotent: guarded with information_schema checks so it can be re-run safely.
-- Target: pems_db (MySQL 8.0, InnoDB, utf8mb4).
-- =============================================================================

SET @schema := DATABASE();

-- ── Create table gallery_item_contents (skip if it already exists) ──────────
SET @tbl_exists := (
  SELECT COUNT(*) FROM information_schema.tables
  WHERE table_schema = @schema AND table_name = 'gallery_item_contents'
);

SET @ddl := IF(@tbl_exists = 0, '
CREATE TABLE gallery_item_contents (
    gallery_item_id  BIGINT UNSIGNED NOT NULL,

    description_vi   TEXT NOT NULL
        COMMENT ''Mô tả tiếng Việt, bắt buộc'',
    audio_vi_file_id BIGINT UNSIGNED NOT NULL
        COMMENT ''files.file_id của bản ghi âm tiếng Việt, bắt buộc'',

    description_en   TEXT NOT NULL
        COMMENT ''Mô tả tiếng Anh, bắt buộc'',
    audio_en_file_id BIGINT UNSIGNED NOT NULL
        COMMENT ''files.file_id của bản ghi âm tiếng Anh, bắt buộc'',

    created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    created_by BIGINT UNSIGNED NULL,
    updated_at DATETIME NULL DEFAULT NULL ON UPDATE CURRENT_TIMESTAMP,
    updated_by BIGINT UNSIGNED NULL,

    PRIMARY KEY (gallery_item_id),

    KEY idx_gallery_item_contents_audio_vi (audio_vi_file_id),
    KEY idx_gallery_item_contents_audio_en (audio_en_file_id),

    FULLTEXT KEY ft_gallery_item_contents_descriptions (description_vi, description_en),

    CONSTRAINT chk_gallery_item_description_vi_not_blank
        CHECK (CHAR_LENGTH(TRIM(description_vi)) > 0),
    CONSTRAINT chk_gallery_item_description_en_not_blank
        CHECK (CHAR_LENGTH(TRIM(description_en)) > 0),

    CONSTRAINT fk_gallery_item_contents_item
        FOREIGN KEY (gallery_item_id) REFERENCES gallery_items(gallery_item_id)
        ON UPDATE RESTRICT ON DELETE CASCADE,
    CONSTRAINT fk_gallery_item_contents_audio_vi
        FOREIGN KEY (audio_vi_file_id) REFERENCES files(file_id)
        ON UPDATE CASCADE ON DELETE RESTRICT,
    CONSTRAINT fk_gallery_item_contents_audio_en
        FOREIGN KEY (audio_en_file_id) REFERENCES files(file_id)
        ON UPDATE CASCADE ON DELETE RESTRICT,
    CONSTRAINT fk_gallery_item_contents_created_by
        FOREIGN KEY (created_by) REFERENCES users(user_id)
        ON UPDATE CASCADE ON DELETE SET NULL,
    CONSTRAINT fk_gallery_item_contents_updated_by
        FOREIGN KEY (updated_by) REFERENCES users(user_id)
        ON UPDATE CASCADE ON DELETE SET NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
COMMENT=''Nội dung mô tả và bản ghi âm song ngữ của Gallery Item (1:1)'';',
'SELECT ''gallery_item_contents already exists — skipped'' AS note;');

PREPARE stmt FROM @ddl;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;
