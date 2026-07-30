-- =====================================================================
-- G11 final closure — email_templates.revision
-- STEP 1 of 3: PREFLIGHT (READ-ONLY)
-- =====================================================================
--
-- Adds nothing, changes nothing. Run it first and read the output; every
-- question 02 could fail on is answered here, while it is still cheap.
--
-- Usage:
--   mysql -u <user> -p <database> < 01_preflight.sql
--
-- What the column is for
-- ----------------------
-- Optimistic concurrency on email template CONTENT (UC-44 update, and
-- restore-to-default). The previous token was updated_at, which is DATETIME
-- with no fractional part: two saves inside the same second stored an
-- identical stamp, compared equal, and the second silently overwrote the
-- first. A monotonic integer has no such blind spot.
--
-- Safety
-- ------
-- The change is ADDITIVE. No existing column, index, trigger or row value is
-- modified. Existing rows receive revision = 1, which is what the application
-- treats as "never edited through the new mechanism".
-- =====================================================================

SELECT '=== Target ===' AS section;
SELECT DATABASE() AS current_database, NOW() AS run_at, VERSION() AS mysql_version;

-- ---------------------------------------------------------------------
-- 1. Is the column already present?
--    Rerunning after a successful 02 is safe; this says whether it would
--    be a no-op.
-- ---------------------------------------------------------------------
SELECT '=== 1. Column state ===' AS section;
SELECT
  CASE WHEN COUNT(*) = 0 THEN 'ABSENT — 02 will add it'
       ELSE 'PRESENT — 02 will be a no-op' END AS revision_column,
  COUNT(*) AS column_count
FROM information_schema.columns
WHERE table_schema = DATABASE()
  AND table_name = 'email_templates'
  AND column_name = 'revision';

-- If present, is it the RIGHT column? A revision of the wrong type or
-- nullability would satisfy "exists" and still break the conditional UPDATE.
SELECT column_name, column_type, is_nullable, column_default, extra
FROM information_schema.columns
WHERE table_schema = DATABASE()
  AND table_name = 'email_templates'
  AND column_name = 'revision';

-- ---------------------------------------------------------------------
-- 2. Does the table exist at all, and how big is it?
--    ALTER TABLE ... ADD COLUMN rewrites the table on older MySQL; on 8.0
--    it is INSTANT for a column added at the end with a literal default.
--    Row count tells you whether that distinction matters here.
-- ---------------------------------------------------------------------
SELECT '=== 2. Table state ===' AS section;
SELECT
  (SELECT COUNT(*) FROM information_schema.tables
    WHERE table_schema = DATABASE() AND table_name = 'email_templates') AS table_present,
  (SELECT COUNT(*) FROM email_templates) AS template_rows,
  (SELECT COUNT(*) FROM email_templates WHERE status = 'ACTIVE') AS active_rows;

-- ---------------------------------------------------------------------
-- 3. Rows whose content is empty.
--    Not blocking, and NOT fixed by this migration — reported because a
--    template with no content will fail its contract the first time somebody
--    opens it, and that is worth knowing before you conclude the migration
--    broke something.
-- ---------------------------------------------------------------------
SELECT '=== 3. Content sanity (informational) ===' AS section;
SELECT COUNT(*) AS templates_missing_vi_or_en
FROM email_templates
WHERE status = 'ACTIVE'
  AND (subject_vi IS NULL OR subject_vi = '' OR body_vi IS NULL OR body_vi = ''
    OR subject_en IS NULL OR subject_en = '' OR body_en IS NULL OR body_en = '');

-- ---------------------------------------------------------------------
-- 4. Template codes the application registry does not know.
--    Informational. These are historical rows kept because sent_emails or
--    drafts reference them; they are not editable and not restorable, and the
--    revision column is meaningless for them but harmless.
-- ---------------------------------------------------------------------
SELECT '=== 4. Historical rows (informational) ===' AS section;
SELECT template_code, status, updated_at
FROM email_templates
ORDER BY template_code;

SELECT '=== Preflight complete — read the output above before running 02 ===' AS section;
