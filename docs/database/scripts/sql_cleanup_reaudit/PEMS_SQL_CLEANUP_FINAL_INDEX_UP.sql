-- =====================================================================================
-- PEMS — FINAL DEAD-INDEX CLEANUP (2 files keys + the whole FULLTEXT family)
--
-- Closes the index half of the SQL cleanup re-audit. Thirteen keys go, none arrive. No
-- column, ENUM, default, constraint, trigger, seed row or data value is touched.
--
-- Safe to run more than once: every statement is guarded by information_schema, so a
-- second run reports the same summary and changes nothing.
--
-- USAGE
--     mysql --default-character-set=utf8mb4 -u root -p <database> \
--           < PEMS_SQL_CLEANUP_FINAL_INDEX_UP.sql
--
-- =====================================================================================
-- 1. THE TWO `files` KEYS — no query leads with their first column
--
--   idx_files_mime_time    (mime_type,    uploaded_at)
--   idx_files_purpose_time (file_purpose, uploaded_at)
--
-- Re-verified against the current sources: every reference to MimeType and FilePurpose in
-- the backend is a PROJECTION — `.Select(f => new { f.FileId, f.FilePurpose, f.MimeType })`
-- in the gallery/news builders, `.Select(f => f.FilePurpose)` after a primary-key lookup in
-- FileAccessAuthorizationService, and DTO assignments in SearchDocuments / ViewDocumentDetail.
-- There is no WHERE, no ORDER BY and no GROUP BY on either column anywhere. Every file row is
-- reached by file_id (PK) or `file_id IN (…)`.
--
-- uploaded_at IS queried — SearchDocuments filters a date range on it and orders by it — but
-- it is the SECOND column of both keys. MySQL cannot seek or order on a trailing column
-- without an equality on the leading one, so neither key can serve that traffic at any table
-- size. idx_files_uploaded_by (uploaded_by, uploaded_at) is untouched and keeps backing
-- fk_files_uploaded_by; no foreign key or UNIQUE constraint references mime_type or
-- file_purpose.
--
-- 2. THE FULLTEXT FAMILY — the product does not use MySQL FULLTEXT at all
--
-- Eleven FULLTEXT indexes, zero consumers between them. Searched the whole repository for
-- MATCH(, AGAINST(, IN BOOLEAN MODE, IN NATURAL LANGUAGE MODE, FromSql, FromSqlRaw,
-- FromSqlInterpolated, ExecuteSql, ExecuteSqlRaw and ExecuteSqlInterpolated: raw SQL does
-- exist, but every statement is a `SELECT … WHERE <pk> = … FOR UPDATE` row lock or an email
-- reservation UPSERT. Not one uses full-text syntax. The database itself holds 0 views,
-- 0 stored routines, 0 events, and no trigger body containing MATCH/AGAINST.
--
-- Every module's search is `col.ToLower().Contains(kw)` or EF.Functions.Like, which EF emits
-- as `LIKE '%kw%'` — a leading wildcard that a FULLTEXT index does not serve. Confirmed
-- per table:
--   faqs / faq_translations          ViewListFAQ, ViewFaq, SearchInformation   → Contains
--   gallery_items (×2) / contents    SearchInformation gallery branch           → Contains
--   minutes                          SearchAndFilterMinutes                     → Contains
--   news_translations / sections     SearchInformation news branch              → Contains
--   visit_requests                   ViewGuestDelegationList (request_code,
--                                    registrant_full_name/organization)         → Contains
--   visit_instance_form_details      same handler (delegation_name)             → Contains
--   sent_email_recipients            GetVisitInstanceSentEmails filters the
--                                    recipient IN MEMORY with string.Equals     → no SQL text
--                                                                                  search at all
--
-- A FULLTEXT index is not free: it carries its own auxiliary tables and is maintained on
-- every insert and update of the indexed columns. These eleven were paying that cost to
-- serve nothing.
--
-- 3. WHAT IS DELIBERATELY KEPT
--
-- Nothing else in the audit is revisited here. files.storage_provider keeps LOCAL,
-- partners keeps DRAFT and public_slug, partner_aliases keeps status (soft delete + revive),
-- documents keeps its lifecycle and MINUTES/NEWS owner types, and partner_translations keeps
-- country/city — those hold genuinely localized values ('South Korea' vs 'Hàn Quốc').
-- =====================================================================================

SET NAMES utf8mb4;

DROP PROCEDURE IF EXISTS pems_final_index_drop_if_exists;

DELIMITER $$

-- Drops an index only when it is present AND is not the last key able to back a foreign key
-- on its leading column. MySQL would refuse with ERROR 1553 anyway; failing here with a
-- readable message beats failing mid-script with the server's wording.
CREATE PROCEDURE pems_final_index_drop_if_exists(IN tbl VARCHAR(64), IN idx VARCHAR(64))
BEGIN
  DECLARE lead_col VARCHAR(64);

  IF EXISTS (SELECT 1 FROM information_schema.STATISTICS
             WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = tbl AND INDEX_NAME = idx) THEN

    SELECT COLUMN_NAME INTO lead_col FROM information_schema.STATISTICS
    WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = tbl AND INDEX_NAME = idx
      AND SEQ_IN_INDEX = 1;

    IF EXISTS (SELECT 1 FROM information_schema.KEY_COLUMN_USAGE k
               WHERE k.TABLE_SCHEMA = DATABASE() AND k.TABLE_NAME = tbl
                 AND k.REFERENCED_TABLE_NAME IS NOT NULL
                 AND k.ORDINAL_POSITION = 1 AND k.COLUMN_NAME = lead_col)
       AND NOT EXISTS (SELECT 1 FROM information_schema.STATISTICS s
                       WHERE s.TABLE_SCHEMA = DATABASE() AND s.TABLE_NAME = tbl
                         AND s.COLUMN_NAME = lead_col AND s.SEQ_IN_INDEX = 1
                         AND s.INDEX_NAME <> idx) THEN
      SIGNAL SQLSTATE '45000'
        SET MESSAGE_TEXT = 'Refused: dropping this index would leave a foreign key unbacked.';
    END IF;

    SET @sql = CONCAT('ALTER TABLE `', tbl, '` DROP INDEX `', idx, '`');
    PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;
  END IF;
END$$

DELIMITER ;

-- ── PRECHECK ──────────────────────────────────────────────────────────────────────────
SELECT '=== PRECHECK: targets present (0 after a successful re-run) ===' AS stage;
SELECT TABLE_NAME, INDEX_NAME, INDEX_TYPE, GROUP_CONCAT(COLUMN_NAME ORDER BY SEQ_IN_INDEX) AS cols
FROM information_schema.STATISTICS
WHERE TABLE_SCHEMA = DATABASE() AND INDEX_NAME IN (
  'idx_files_mime_time','idx_files_purpose_time',
  'ft_faqs_search','ft_faq_translations_search','ft_gallery_items_search',
  'ft_gallery_items_search_en','ft_gallery_item_contents_descriptions','ft_minutes_search',
  'ft_news_translations_search','ft_news_sections_search','ft_sent_email_recipients_search',
  'ft_vifd_search','ft_visit_requests_frontend_search')
GROUP BY TABLE_NAME, INDEX_NAME, INDEX_TYPE ORDER BY TABLE_NAME, INDEX_NAME;

SELECT '=== PRECHECK: FULLTEXT indexes anywhere in this schema ===' AS stage;
SELECT COUNT(DISTINCT CONCAT(TABLE_NAME,'.',INDEX_NAME)) AS fulltext_indexes_now
FROM information_schema.STATISTICS WHERE TABLE_SCHEMA = DATABASE() AND INDEX_TYPE = 'FULLTEXT';

SELECT '=== PRECHECK: structural counts ===' AS stage;
SELECT (SELECT COUNT(*) FROM information_schema.TABLES
        WHERE TABLE_SCHEMA = DATABASE() AND TABLE_TYPE = 'BASE TABLE') AS tables_now,
       (SELECT COUNT(*) FROM information_schema.TABLE_CONSTRAINTS
        WHERE TABLE_SCHEMA = DATABASE() AND CONSTRAINT_TYPE = 'FOREIGN KEY') AS fks_now,
       (SELECT COUNT(*) FROM information_schema.TRIGGERS
        WHERE TRIGGER_SCHEMA = DATABASE()) AS triggers_now,
       (SELECT COUNT(*) FROM information_schema.COLUMNS
        WHERE TABLE_SCHEMA = DATABASE()) AS columns_now;

-- ── DROP: the two files keys ──────────────────────────────────────────────────────────
CALL pems_final_index_drop_if_exists('files', 'idx_files_mime_time');
CALL pems_final_index_drop_if_exists('files', 'idx_files_purpose_time');

-- ── DROP: the FULLTEXT family ─────────────────────────────────────────────────────────
CALL pems_final_index_drop_if_exists('faqs',                        'ft_faqs_search');
CALL pems_final_index_drop_if_exists('faq_translations',            'ft_faq_translations_search');
CALL pems_final_index_drop_if_exists('gallery_items',               'ft_gallery_items_search');
CALL pems_final_index_drop_if_exists('gallery_items',               'ft_gallery_items_search_en');
CALL pems_final_index_drop_if_exists('gallery_item_contents',       'ft_gallery_item_contents_descriptions');
CALL pems_final_index_drop_if_exists('minutes',                     'ft_minutes_search');
CALL pems_final_index_drop_if_exists('news_translations',           'ft_news_translations_search');
CALL pems_final_index_drop_if_exists('news_content_sections',       'ft_news_sections_search');
CALL pems_final_index_drop_if_exists('sent_email_recipients',       'ft_sent_email_recipients_search');
CALL pems_final_index_drop_if_exists('visit_instance_form_details', 'ft_vifd_search');
CALL pems_final_index_drop_if_exists('visit_requests',              'ft_visit_requests_frontend_search');

DROP PROCEDURE pems_final_index_drop_if_exists;

-- ── VERIFY ────────────────────────────────────────────────────────────────────────────
SELECT '=== VERIFY: dropped indexes (expected 0 rows) ===' AS verify;
SELECT TABLE_NAME, INDEX_NAME FROM information_schema.STATISTICS
WHERE TABLE_SCHEMA = DATABASE() AND INDEX_NAME IN (
  'idx_files_mime_time','idx_files_purpose_time',
  'ft_faqs_search','ft_faq_translations_search','ft_gallery_items_search',
  'ft_gallery_items_search_en','ft_gallery_item_contents_descriptions','ft_minutes_search',
  'ft_news_translations_search','ft_news_sections_search','ft_sent_email_recipients_search',
  'ft_vifd_search','ft_visit_requests_frontend_search')
GROUP BY TABLE_NAME, INDEX_NAME;

SELECT '=== VERIFY: no FULLTEXT index left anywhere (expected 0) ===' AS verify;
SELECT COUNT(DISTINCT CONCAT(TABLE_NAME,'.',INDEX_NAME)) AS fulltext_indexes_now
FROM information_schema.STATISTICS WHERE TABLE_SCHEMA = DATABASE() AND INDEX_TYPE = 'FULLTEXT';

SELECT '=== VERIFY: keys that must survive (expected 4 rows) ===' AS verify;
SELECT TABLE_NAME, INDEX_NAME, GROUP_CONCAT(COLUMN_NAME ORDER BY SEQ_IN_INDEX) AS cols
FROM information_schema.STATISTICS
WHERE TABLE_SCHEMA = DATABASE() AND INDEX_NAME IN (
  'uq_files_object_key','idx_files_uploaded_by','uq_partner_alias_key','idx_partner_alias_lookup')
GROUP BY TABLE_NAME, INDEX_NAME ORDER BY TABLE_NAME, INDEX_NAME;

SELECT '=== VERIFY: every foreign key still has a backing index (expected 0 rows) ===' AS verify;
SELECT k.TABLE_NAME, k.CONSTRAINT_NAME, k.COLUMN_NAME AS unbacked_column
FROM information_schema.KEY_COLUMN_USAGE k
WHERE k.TABLE_SCHEMA = DATABASE() AND k.REFERENCED_TABLE_NAME IS NOT NULL
  AND k.ORDINAL_POSITION = 1
  AND NOT EXISTS (SELECT 1 FROM information_schema.STATISTICS s
                  WHERE s.TABLE_SCHEMA = k.TABLE_SCHEMA AND s.TABLE_NAME = k.TABLE_NAME
                    AND s.COLUMN_NAME = k.COLUMN_NAME AND s.SEQ_IN_INDEX = 1);

SELECT '=== VERIFY: structural counts unchanged by this script ===' AS verify;
SELECT (SELECT COUNT(*) FROM information_schema.TABLES
        WHERE TABLE_SCHEMA = DATABASE() AND TABLE_TYPE = 'BASE TABLE') AS tables_now,
       (SELECT COUNT(*) FROM information_schema.TABLE_CONSTRAINTS
        WHERE TABLE_SCHEMA = DATABASE() AND CONSTRAINT_TYPE = 'FOREIGN KEY') AS fks_now,
       (SELECT COUNT(*) FROM information_schema.TRIGGERS
        WHERE TRIGGER_SCHEMA = DATABASE()) AS triggers_now,
       (SELECT COUNT(*) FROM information_schema.COLUMNS
        WHERE TABLE_SCHEMA = DATABASE()) AS columns_now;

SELECT 'PEMS FINAL DEAD-INDEX CLEANUP — DONE' AS status;
