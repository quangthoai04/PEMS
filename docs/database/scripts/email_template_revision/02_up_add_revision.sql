-- =====================================================================
-- G11 final closure — email_templates.revision
-- STEP 2 of 3: APPLY
-- =====================================================================
--
-- Adds ONE additive column. Nothing else is created, altered or deleted.
--
--   email_templates.revision INT UNSIGNED NOT NULL DEFAULT 1
--
-- Usage (the confirmation is deliberate — see below):
--
--   SET @pems_guard_confirm_database = 'the_database_name';
--   SOURCE 02_up_add_revision.sql;
--
-- or:
--
--   mysql -u <user> -p <db> -e "SET @pems_guard_confirm_database='<db>'; SOURCE 02_up_add_revision.sql;"
--
-- Why a confirmation variable
-- ---------------------------
-- The same pattern as contact_guard_closure. A migration is usually run by
-- pasting a command, and the fastest way to damage a production database is to
-- paste it while connected to the wrong one. Naming the target out loud, and
-- having the script refuse when the name does not match the connection, makes
-- that mistake cost nothing. The variable is spent at the end so a second
-- SOURCE in the same session cannot run unconfirmed.
--
-- Idempotent
-- ----------
-- Running it twice is a no-op the second time: the column is added only if
-- information_schema says it is absent. Verified by running it twice and
-- diffing the resulting schema.
-- =====================================================================

-- ---------------------------------------------------------------------
-- Guard
-- ---------------------------------------------------------------------
DELIMITER $$

DROP PROCEDURE IF EXISTS pems_revision_migration_guard$$
CREATE PROCEDURE pems_revision_migration_guard()
BEGIN
  IF @pems_guard_confirm_database IS NULL THEN
    SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT =
      'REFUSED: set @pems_guard_confirm_database to the exact name of the database you intend to migrate.';
  END IF;

  IF @pems_guard_confirm_database <> DATABASE() THEN
    SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT =
      'REFUSED: @pems_guard_confirm_database does not match the connected database.';
  END IF;

  IF (SELECT COUNT(*) FROM information_schema.tables
       WHERE table_schema = DATABASE() AND table_name = 'email_templates') = 0 THEN
    SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT =
      'REFUSED: email_templates does not exist in this database. Wrong target?';
  END IF;
END$$

DELIMITER ;

CALL pems_revision_migration_guard();
DROP PROCEDURE pems_revision_migration_guard;

SELECT CONCAT('Guard passed. Migrating: ', DATABASE()) AS status;

-- ---------------------------------------------------------------------
-- Add the column, once.
--
-- PREPARE/EXECUTE rather than a bare ALTER because "add it only if absent"
-- cannot be expressed in DDL, and because CREATE PROCEDURE forces an implicit
-- COMMIT while PREPARE does not.
--
-- Placed at the end of the table with a literal default, which on MySQL 8.0 is
-- an INSTANT operation: no table rebuild, no long lock, regardless of row count.
-- ---------------------------------------------------------------------
SET @needs_column := (
  SELECT COUNT(*) = 0 FROM information_schema.columns
  WHERE table_schema = DATABASE()
    AND table_name = 'email_templates'
    AND column_name = 'revision');

SET @ddl := IF(@needs_column,
  'ALTER TABLE email_templates
     ADD COLUMN revision INT UNSIGNED NOT NULL DEFAULT 1 AFTER variables_text',
  'SELECT ''revision already present — nothing to do'' AS skipped');

PREPARE stmt FROM @ddl;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

-- ---------------------------------------------------------------------
-- Existing rows.
--
-- The DEFAULT already gave every existing row 1, because a column added with
-- a NOT NULL default is materialised as that default for rows that predate it.
-- This statement exists for the one case the default does not cover: a rerun
-- against a database where somebody had already added a NULLABLE revision by
-- hand. It touches nothing otherwise.
-- ---------------------------------------------------------------------
UPDATE email_templates SET revision = 1 WHERE revision IS NULL OR revision = 0;

SELECT ROW_COUNT() AS rows_normalised;

-- The confirmation is spent: a second SOURCE in this session must re-confirm.
SET @pems_guard_confirm_database = NULL;

SELECT '=== 02 complete — now run 03_verify.sql ===' AS status;
