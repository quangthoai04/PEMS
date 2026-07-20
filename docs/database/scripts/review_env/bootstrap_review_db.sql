-- =====================================================================
-- Bootstrap for the isolated PEMS V2 REVIEW environment.
--
-- THE PROJECT OWNER RUNS THIS MANUALLY, ONCE. No agent or script runs it.
--
--   mysql -uroot -p < bootstrap_review_db.sql
--
-- It creates a database and grants privileges — exactly the statement classes the
-- import guard refuses — so it deliberately lives outside the automated path.
--
-- WHY A SEPARATE DATABASE
-- -----------------------
-- On 2026-07-20 a master dump was imported with an intended target of a disposable
-- database. The dump carried `DROP DATABASE IF EXISTS pems_db` / `CREATE DATABASE
-- pems_db` / `USE pems_db`, so the whole payload landed in the protected pems_db.
-- See PHASE_I_DB_IMPORT_INCIDENT_2026_07_20.md.
--
-- Reviewing V2 therefore does NOT reuse pems_db, and does not reuse the Phase I
-- drill databases either: those get dropped and rebuilt by destructive migrations,
-- which would wipe review data mid-session.
-- =====================================================================

-- 1. The review database. Charset/collation match the master baseline so an imported
--    fixture does not silently change collation behaviour.
CREATE DATABASE IF NOT EXISTS `pems_review_v2`
  CHARACTER SET utf8mb4
  COLLATE utf8mb4_unicode_ci;

-- 2. A restricted account scoped to that database only.
--    Replace the placeholder before running. Do NOT reuse an application password,
--    and do not commit the value anywhere.
CREATE USER IF NOT EXISTS 'pems_review'@'localhost'
  IDENTIFIED BY 'CHANGE_ME_BEFORE_RUNNING';

-- 3. Full rights INSIDE the review database, and nowhere else.
GRANT ALL PRIVILEGES ON `pems_review_v2`.* TO 'pems_review'@'localhost';

-- Deliberately NOT granted, because each one would re-open the incident's blast radius:
--   *.*  ALL PRIVILEGES ...... would reach pems_db / pems_test / pems_pr3_test
--   WITH GRANT OPTION ........ would let the account widen its own rights
--   SUPER / SYSTEM_USER ...... would allow global settings changes
--   FILE ..................... would allow reading/writing server-side files
--   RELOAD / REPLICATION ..... would allow binlog and replication operations
--   CREATE ON *.* ............ not needed: this script already created the database,
--                              and the review harness must never create one itself
--
-- import_disposable_fixture.ps1 inspects SHOW GRANTS and refuses to run when it finds
-- global privileges or any grant touching a protected schema.

FLUSH PRIVILEGES;

-- 4. Verify. Expect exactly one USAGE line plus one grant on `pems_review_v2`.*,
--    and no mention of pems_db, pems_test or pems_pr3_test:
--
--      SHOW GRANTS FOR 'pems_review'@'localhost';
--
--    Then hand the credentials to the review harness via environment variables
--    (never a committed file):
--
--      $env:MYSQL_USER     = 'pems_review'
--      $env:MYSQL_PASSWORD = '<the password chosen above>'
--
-- 5. To tear the review environment down again:
--
--      DROP DATABASE `pems_review_v2`;
--      DROP USER 'pems_review'@'localhost';
--
--    Dropping and re-running this script is the supported way to reset review data.
