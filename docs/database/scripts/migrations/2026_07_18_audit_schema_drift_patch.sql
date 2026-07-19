-- =============================================================================
-- Schema-drift patch — restore the "per-campus form v2" audit columns that an
-- older imported base is missing. Without them EF's INSERT into audit_logs /
-- audit_log_changes fails with "Unknown column 'correlation_id'" (500), which
-- blocks every write that records an audit entry (e.g. Add Gallery Item).
--
-- Idempotent: each column/index is added only if it does not already exist.
-- Target: pems_db (MySQL 8.0). Matches the canonical DDL in the full SQL script.
-- =============================================================================

SET @schema := DATABASE();

-- Helper macro pattern: add a column only when it is absent.
DELIMITER $$
DROP PROCEDURE IF EXISTS __add_col $$
CREATE PROCEDURE __add_col(IN tbl VARCHAR(64), IN col VARCHAR(64), IN ddl TEXT)
BEGIN
  IF (SELECT COUNT(*) FROM information_schema.columns
      WHERE table_schema = DATABASE() AND table_name = tbl AND column_name = col) = 0 THEN
    SET @s := CONCAT('ALTER TABLE `', tbl, '` ADD COLUMN ', ddl);
    PREPARE st FROM @s; EXECUTE st; DEALLOCATE PREPARE st;
  END IF;
END $$

DROP PROCEDURE IF EXISTS __add_idx $$
CREATE PROCEDURE __add_idx(IN tbl VARCHAR(64), IN idx VARCHAR(64), IN cols TEXT)
BEGIN
  IF (SELECT COUNT(*) FROM information_schema.statistics
      WHERE table_schema = DATABASE() AND table_name = tbl AND index_name = idx) = 0 THEN
    SET @s := CONCAT('ALTER TABLE `', tbl, '` ADD KEY `', idx, '` (', cols, ')');
    PREPARE st FROM @s; EXECUTE st; DEALLOCATE PREPARE st;
  END IF;
END $$
DELIMITER ;

-- ── audit_logs additive columns ────────────────────────────────────────────
CALL __add_col('audit_logs', 'correlation_id',    "correlation_id VARCHAR(100) NULL AFTER request_id");
CALL __add_col('audit_logs', 'visit_request_id',  "visit_request_id BIGINT UNSIGNED NULL AFTER correlation_id");
CALL __add_col('audit_logs', 'visit_instance_id', "visit_instance_id BIGINT UNSIGNED NULL AFTER visit_request_id");
CALL __add_col('audit_logs', 'source_type',       "source_type VARCHAR(80) NULL AFTER visit_instance_id");
CALL __add_col('audit_logs', 'source_id',         "source_id BIGINT UNSIGNED NULL AFTER source_type");
CALL __add_col('audit_logs', 'reason',            "reason VARCHAR(500) NULL AFTER source_id");

CALL __add_idx('audit_logs', 'idx_audit_visit_request_time',  'visit_request_id, created_at');
CALL __add_idx('audit_logs', 'idx_audit_visit_instance_time', 'visit_instance_id, created_at');
CALL __add_idx('audit_logs', 'idx_audit_correlation',         'correlation_id');
CALL __add_idx('audit_logs', 'idx_audit_source',              'source_type, source_id');

-- ── audit_log_changes additive columns ─────────────────────────────────────
CALL __add_col('audit_log_changes', 'change_category', "change_category VARCHAR(40) NULL AFTER field_name");
CALL __add_col('audit_log_changes', 'value_format',    "value_format VARCHAR(20) NOT NULL DEFAULT 'TEXT' AFTER change_category");
CALL __add_col('audit_log_changes', 'is_sensitive',    "is_sensitive TINYINT(1) NOT NULL DEFAULT 0 AFTER value_format");
CALL __add_col('audit_log_changes', 'display_order',   "display_order INT UNSIGNED NOT NULL DEFAULT 0 AFTER is_sensitive");

DROP PROCEDURE IF EXISTS __add_col;
DROP PROCEDURE IF EXISTS __add_idx;
