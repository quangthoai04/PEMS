-- ============================================================================
-- 2026-07-26 — Tell a full "sửa đơn" apart from a "sửa nhanh" in the revision history.
--
-- WHY
--   Editing a still-pending request and applying a safe/quick correction are two different acts with
--   different rules (a pending edit rewrites content and may add or drop campuses; a safe edit touches
--   a narrow allowlist after approval). Both were written to the revision tables as source_type =
--   'SAFE_EDIT', because that was the only value available, so the timeline reported every pending edit
--   as "đã sửa nhanh" — telling the user, and the campus Staff Leader reading the same timeline, that
--   something narrow had happened when in fact the whole form had changed.
--
--   The read model derives the timeline event code from source_type. Rather than guess intent from
--   Vietnamese message text or from the revision NUMBER (both fragile), this adds the missing value so
--   the immutable row states what it was.
--
-- WHAT
--   Adds 'PENDING_EDIT' to the source_type ENUM of:
--     • visit_instance_form_revision_history
--     • visit_request_revision_history
--
-- SAFETY
--   Purely additive: widening an ENUM neither rewrites nor invalidates existing rows, and no existing
--   value is removed or reordered. Historical rows keep 'SAFE_EDIT' and are read exactly as before —
--   this migration does NOT reclassify them, because there is no way to tell after the fact which of
--   those rows were pending edits, and inventing an answer would corrupt an immutable audit trail.
--   Idempotent: re-running finds the value already present and does nothing.
--
-- ROLLBACK
--   See the DOWN block at the end. It is safe ONLY while no row uses the new value; the guard checks.
-- ============================================================================

-- ── UP ──────────────────────────────────────────────────────────────────────

SET @db := DATABASE();

-- visit_instance_form_revision_history.source_type
SET @needs_instance := (
  SELECT COUNT(*) FROM information_schema.COLUMNS
  WHERE TABLE_SCHEMA = @db
    AND TABLE_NAME = 'visit_instance_form_revision_history'
    AND COLUMN_NAME = 'source_type'
    AND COLUMN_TYPE NOT LIKE '%PENDING_EDIT%'
);
SET @sql := IF(@needs_instance = 1,
  "ALTER TABLE visit_instance_form_revision_history
     MODIFY COLUMN source_type
     ENUM('CREATE','SAFE_EDIT','PENDING_EDIT','AMENDMENT_APPLIED','MIGRATION','RESUBMIT') NOT NULL",
  'SELECT ''visit_instance_form_revision_history.source_type already has PENDING_EDIT'' AS note');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

-- visit_request_revision_history.source_type
SET @needs_request := (
  SELECT COUNT(*) FROM information_schema.COLUMNS
  WHERE TABLE_SCHEMA = @db
    AND TABLE_NAME = 'visit_request_revision_history'
    AND COLUMN_NAME = 'source_type'
    AND COLUMN_TYPE NOT LIKE '%PENDING_EDIT%'
);
SET @sql := IF(@needs_request = 1,
  "ALTER TABLE visit_request_revision_history
     MODIFY COLUMN source_type
     ENUM('CREATE','SAFE_EDIT','PENDING_EDIT','MIGRATION','RESUBMIT') NOT NULL",
  'SELECT ''visit_request_revision_history.source_type already has PENDING_EDIT'' AS note');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

-- ── VERIFY ──────────────────────────────────────────────────────────────────
SELECT
  TABLE_NAME,
  COLUMN_TYPE,
  IF(COLUMN_TYPE LIKE '%PENDING_EDIT%', 'PASS', 'FAIL') AS pending_edit_present
FROM information_schema.COLUMNS
WHERE TABLE_SCHEMA = DATABASE()
  AND COLUMN_NAME = 'source_type'
  AND TABLE_NAME IN ('visit_instance_form_revision_history', 'visit_request_revision_history')
ORDER BY TABLE_NAME;

-- ── DOWN (manual — run only if no row uses PENDING_EDIT) ────────────────────
-- SELECT COUNT(*) FROM visit_instance_form_revision_history WHERE source_type = 'PENDING_EDIT';
-- SELECT COUNT(*) FROM visit_request_revision_history       WHERE source_type = 'PENDING_EDIT';
-- -- Both MUST be 0; otherwise those rows would be silently coerced to '' by MySQL.
-- ALTER TABLE visit_instance_form_revision_history
--   MODIFY COLUMN source_type
--   ENUM('CREATE','SAFE_EDIT','AMENDMENT_APPLIED','MIGRATION','RESUBMIT') NOT NULL;
-- ALTER TABLE visit_request_revision_history
--   MODIFY COLUMN source_type
--   ENUM('CREATE','SAFE_EDIT','MIGRATION','RESUBMIT') NOT NULL;
