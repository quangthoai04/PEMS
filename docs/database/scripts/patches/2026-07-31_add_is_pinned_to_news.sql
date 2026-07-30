-- Patch: Add is_pinned column to news table
-- Created at: 2026-07-31

ALTER TABLE news
  ADD COLUMN is_pinned TINYINT(1) NOT NULL DEFAULT 0 COMMENT 'Bài viết được ghim ở Dấu ấn các chuyến thăm' AFTER is_featured,
  ADD INDEX idx_news_pinned (is_pinned, status, published_at);
