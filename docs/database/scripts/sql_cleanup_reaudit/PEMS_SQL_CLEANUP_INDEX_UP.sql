-- =====================================================================================
-- PEMS — FILE / PARTNER / DOCUMENT DEAD-INDEX CLEANUP
--
-- Brings an existing database in line with the index set in PEMS_FULL_VS_31_07_NEW.sql
-- after every index on files / partners / partner_translations / partner_aliases /
-- documents was re-checked against the queries that actually run. Seven keys go, none
-- arrive. No column, ENUM, default, constraint or row is touched.
--
-- Safe to run more than once: every statement is guarded by information_schema, so a
-- second run reports the same summary and changes nothing.
--
-- USAGE
--     mysql --default-character-set=utf8mb4 -u root -p <database> \
--           < PEMS_SQL_CLEANUP_INDEX_UP.sql
--
-- =====================================================================================
-- WHY EACH ONE. No index is dropped for being unused on a small dataset — these tables
-- are small today and that is deliberately not the argument. Each is dropped because no
-- query can use it AT ANY SIZE, or because another key already opens with the same
-- column.
--
-- DROPPED — nothing in the product uses MySQL FULLTEXT. Every text search in the backend
-- is `col.ToLower().Contains(kw)`, which EF translates to `LIKE '%kw%'`; a leading
-- wildcard cannot seek a BTREE index and does not touch a FULLTEXT one at all. There is
-- no `MATCH … AGAINST` anywhere in the C# sources, in the canonical script, or in any
-- patch script. Confirmed against SearchInformationQueryHandler (public site-wide
-- search), SearchDocumentsQueryHandler and GetPartnersQueryHandler:
--   partners.ft_partners_search                    (name, short_name, description)
--   partner_translations.ft_partner_translations_search
--                                                  (name, short_name, description, address)
--   documents.ft_documents_search                  (title, description)
--
-- DROPPED — nothing queries the column at all:
--   files.idx_files_checksum          checksum_sha256 is computed on upload and echoed in
--                                     the detail DTO. There is no dedupe, no integrity
--                                     re-check and no `WHERE checksum_sha256 = …` — the
--                                     column stays, the index has no predicate to serve.
--   files.idx_files_external_file_id  every read of external_file_id starts from a known
--                                     file_id (OpenReadAsync, StoredFileProbe, the gallery
--                                     and news media handlers), so the traffic is PK-side.
--                                     Nothing resolves provider-id → files row. The one
--                                     script that scans the column,
--                                     audit_unusable_drive_file_references.sql, uses
--                                     REGEXP, which cannot seek an index either.
--
-- DROPPED — no query leads with the index's first column:
--   partner_translations.idx_partner_translations_lang_status  (language_code,
--                                     translation_status). Every read starts from
--                                     partner_id — GetPublicPartners, GetPublicPartnerDetail,
--                                     GetPartners, GetPartnerDetail, UpdatePartner — and is
--                                     served by uq_partner_translations_lang
--                                     (partner_id, language_code). A key leading with
--                                     language_code could only ever narrow to ~50% of the
--                                     table ('vi' or 'en'), so the planner would not choose
--                                     it even if a query existed.
--
-- DROPPED — another key already opens with the same column, so nothing can regress:
--   partner_aliases.idx_partner_alias_partner  (partner_id)
--                                     ⊂ uq_partner_alias_key (partner_id, alias_name_key).
--                                     The unique key keeps FK fk_partner_alias_partner
--                                     backed after the drop; the VERIFY block below proves
--                                     it rather than assuming it.
--
-- KEPT, and worth saying why, because they look similar to the ones above:
--   partner_aliases.idx_partner_alias_lookup (alias_name_key, status)
--     This is exactly PartnerMatcher's predicate — `Status == "ACTIVE" && AliasNameKey ==
--     key` — the alias branch of guest/OCR organisation matching. Both columns, in order.
--   files.idx_files_uploaded_by (uploaded_by, uploaded_at)
--     The only key leading with uploaded_by, so it backs FK fk_files_uploaded_by.
--   documents.idx_documents_campus_status (campus_id, status)
--     Staff Leader campus scoping, and the only key leading with campus_id, so it backs
--     FK fk_documents_campus.
--   documents.idx_documents_category_status (document_category, status)
--     SearchDocuments filters those two together.
--   files.idx_files_mime_time, files.idx_files_purpose_time
--     Neither column has a predicate today, so both are drop-eligible on the same evidence
--     as idx_files_checksum. They are left in place because this pass was scoped to the
--     candidates raised in the re-audit; they are recorded here so the next pass does not
--     have to re-derive them.
-- =====================================================================================

SET NAMES utf8mb4;

DROP PROCEDURE IF EXISTS pems_cleanup_index_drop_if_exists;

DELIMITER $$

-- Drops an index only when it is present AND is not the last key able to back a foreign
-- key on its leading column. MySQL would refuse with ERROR 1553 anyway; failing here with
-- a readable message beats failing mid-script with the server's wording.
CREATE PROCEDURE pems_cleanup_index_drop_if_exists(IN tbl VARCHAR(64), IN idx VARCHAR(64))
BEGIN
  DECLARE lead_col VARCHAR(64);

  IF EXISTS (SELECT 1 FROM information_schema.STATISTICS
             WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = tbl AND INDEX_NAME = idx) THEN

    SELECT COLUMN_NAME INTO lead_col FROM information_schema.STATISTICS
    WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = tbl AND INDEX_NAME = idx
      AND SEQ_IN_INDEX = 1;

    -- Is the leading column the first column of a foreign key on this table, and is this
    -- index the only one that opens with it? If so, refuse rather than break the FK.
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

SELECT '=== BEFORE ===' AS stage;
SELECT COUNT(DISTINCT INDEX_NAME) AS indexes_now FROM information_schema.STATISTICS
WHERE TABLE_SCHEMA = DATABASE()
  AND TABLE_NAME IN ('files','partners','partner_translations','partner_aliases','documents');

-- ── DROP ──────────────────────────────────────────────────────────────────────────────
CALL pems_cleanup_index_drop_if_exists('partners',             'ft_partners_search');
CALL pems_cleanup_index_drop_if_exists('partner_translations', 'ft_partner_translations_search');
CALL pems_cleanup_index_drop_if_exists('partner_translations', 'idx_partner_translations_lang_status');
CALL pems_cleanup_index_drop_if_exists('documents',            'ft_documents_search');
CALL pems_cleanup_index_drop_if_exists('files',                'idx_files_checksum');
CALL pems_cleanup_index_drop_if_exists('files',                'idx_files_external_file_id');
CALL pems_cleanup_index_drop_if_exists('partner_aliases',      'idx_partner_alias_partner');

DROP PROCEDURE pems_cleanup_index_drop_if_exists;

-- ── VERIFY ────────────────────────────────────────────────────────────────────────────
SELECT '=== VERIFY: dropped indexes (expected 0 rows) ===' AS verify;
SELECT TABLE_NAME, INDEX_NAME FROM information_schema.STATISTICS
WHERE TABLE_SCHEMA = DATABASE() AND INDEX_NAME IN (
  'ft_partners_search','ft_partner_translations_search','idx_partner_translations_lang_status',
  'ft_documents_search','idx_files_checksum','idx_files_external_file_id',
  'idx_partner_alias_partner')
GROUP BY TABLE_NAME, INDEX_NAME;

SELECT '=== VERIFY: keys that must survive (expected 5 rows) ===' AS verify;
SELECT TABLE_NAME, INDEX_NAME, GROUP_CONCAT(COLUMN_NAME ORDER BY SEQ_IN_INDEX) AS cols
FROM information_schema.STATISTICS
WHERE TABLE_SCHEMA = DATABASE() AND INDEX_NAME IN (
  'uq_partner_alias_key','idx_partner_alias_lookup','uq_partner_translations_lang',
  'idx_files_uploaded_by','idx_documents_campus_status')
GROUP BY TABLE_NAME, INDEX_NAME ORDER BY TABLE_NAME, INDEX_NAME;

SELECT '=== VERIFY: every foreign key still has a backing index (expected 0 rows) ===' AS verify;
SELECT k.TABLE_NAME, k.CONSTRAINT_NAME, k.COLUMN_NAME AS unbacked_column
FROM information_schema.KEY_COLUMN_USAGE k
WHERE k.TABLE_SCHEMA = DATABASE() AND k.REFERENCED_TABLE_NAME IS NOT NULL
  AND k.TABLE_NAME IN ('files','partners','partner_translations','partner_aliases','documents')
  AND k.ORDINAL_POSITION = 1
  AND NOT EXISTS (SELECT 1 FROM information_schema.STATISTICS s
                  WHERE s.TABLE_SCHEMA = k.TABLE_SCHEMA AND s.TABLE_NAME = k.TABLE_NAME
                    AND s.COLUMN_NAME = k.COLUMN_NAME AND s.SEQ_IN_INDEX = 1);

SELECT '=== VERIFY: no column/ENUM/default changed by this script ===' AS verify;
SELECT TABLE_NAME, COUNT(*) AS columns_now FROM information_schema.COLUMNS
WHERE TABLE_SCHEMA = DATABASE()
  AND TABLE_NAME IN ('files','partners','partner_translations','partner_aliases','documents')
GROUP BY TABLE_NAME ORDER BY TABLE_NAME;

SELECT '=== AFTER ===' AS stage;
SELECT COUNT(DISTINCT INDEX_NAME) AS indexes_now FROM information_schema.STATISTICS
WHERE TABLE_SCHEMA = DATABASE()
  AND TABLE_NAME IN ('files','partners','partner_translations','partner_aliases','documents');

SELECT 'PEMS FILE/PARTNER/DOCUMENT INDEX CLEANUP — DONE' AS status;
