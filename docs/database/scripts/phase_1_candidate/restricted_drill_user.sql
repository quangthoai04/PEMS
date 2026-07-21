-- =====================================================================
-- Restricted MySQL account for Phase I disposable drills.
--
-- THIS SCRIPT IS NOT RUN BY ANY AGENT OR TOOL. It creates a login and grants
-- privileges, which is exactly the class of statement the import guard refuses.
-- The project owner runs it manually, once, when they choose to.
--
--   mysql -uroot -p < restricted_drill_user.sql
--
-- WHY IT IS NEEDED
-- ----------------
-- On 2026-07-20 a master dump was imported with an intended target of a disposable
-- database. The dump contained `DROP DATABASE IF EXISTS pems_db;` followed by
-- `CREATE DATABASE ... pems_db;` and `USE pems_db;`, so the whole payload landed in the
-- protected pems_db instead.
--
-- The statement-aware import guard now blocks that payload before mysql is ever
-- spawned. But a guard is code, and code has bugs. The credential is the layer that
-- makes the failure survivable: an account with no rights on pems_db / pems_test /
-- pems_pr3_test cannot damage them even if every other control is wrong.
--
-- Defence in depth means the drill account must NOT be root.
-- =====================================================================

-- Choose a password that is NOT reused from any application account, and do not commit it.
-- Replace the placeholder before running.
CREATE USER IF NOT EXISTS 'pems_drill'@'localhost' IDENTIFIED BY 'CHANGE_ME_BEFORE_RUNNING';

-- Exactly the four disposable databases, one grant each. No wildcard such as `pems_i_%`:
-- a pattern would also match a future database nobody intended to expose.
GRANT ALL PRIVILEGES ON `pems_i_fresh`.*    TO 'pems_drill'@'localhost';
GRANT ALL PRIVILEGES ON `pems_i_upgrade`.*  TO 'pems_drill'@'localhost';
GRANT ALL PRIVILEGES ON `pems_i_refusal`.*  TO 'pems_drill'@'localhost';
GRANT ALL PRIVILEGES ON `pems_i_rollback`.* TO 'pems_drill'@'localhost';

-- The drill harness creates and drops these databases itself, so it needs CREATE at the
-- server level. This is the one broad privilege the account holds; it grants no access to
-- data in any existing schema.
GRANT CREATE ON *.* TO 'pems_drill'@'localhost';

-- Deliberately NOT granted: SUPER, GRANT OPTION, RELOAD, SHUTDOWN, REPLICATION,
-- FILE, PROCESS, and any privilege whatsoever on pems_db, pems_test or pems_pr3_test.
-- import_disposable_fixture.ps1 inspects SHOW GRANTS and refuses to run if it finds any
-- of them.

FLUSH PRIVILEGES;

-- Verify (expect: no reference to any protected schema, no *.* ALL PRIVILEGES):
--   SHOW GRANTS FOR 'pems_drill'@'localhost';
--
-- Then point the harness at it:
--   $env:MYSQL_USER = 'pems_drill'
--   $env:MYSQL_PASSWORD = '<the password chosen above>'
--
-- To remove the account again:
--   DROP USER 'pems_drill'@'localhost';
