-- =====================================================================
-- PEMS — PER-CAMPUS FORM v2 + IDENTITY EDIT — MIGRATION UP (ADDITIVE)
-- Plan: docs/ChangeSauHopChiQUyen/sauhop_13-07/
--       PEMS_MULTI_CAMPUS_PER_CAMPUS_FORM_AND_IDENTITY_EDIT_PLAN.md  (PR-2)
-- Baseline: pems_full_v10_TTS_Gallery_FULL_UPDATED_NOTIFICATIONS_FIXED.sql
-- Target:   MySQL 8.0.16+  (CHECK constraints enforced; virtual generated
--           columns indexable; functional expressions in CHECK allowed).
--
-- SAFETY / ORDER
--   1. Take a backup.
--   2. Run 01_preflight_readiness.sql and RESOLVE every reported row.
--      In particular, any visit_request_campuses row with a schedule shorter
--      than 30 minutes MUST be fixed by a human before this script runs, or
--      the ck_visit_instance_min_duration_30m ADD CONSTRAINT will fail.
--   3. Run THIS script (additive only — no data is dropped or rewritten).
--   4. Run 03_backfill.sql, then 04_verify.sql.
--
-- IDEMPOTENCY
--   Every ADD COLUMN / ADD INDEX / ADD CONSTRAINT is wrapped in an
--   information_schema guard, and every table uses CREATE TABLE IF NOT EXISTS,
--   so this whole script is safe to re-run. (MySQL, unlike MariaDB, has no
--   `ADD COLUMN IF NOT EXISTS`, hence the helper procedures below.)
--
--   NOTE: MySQL auto-commits each DDL statement; a mid-script failure leaves
--   earlier statements applied. Because every step is guarded, simply fix the
--   cause and re-run — already-applied steps are skipped.
-- =====================================================================

SET @DB := DATABASE();

-- The helper CALLs below pass DDL fragments as DOUBLE-QUOTED strings, which are
-- string literals only when ANSI_QUOTES is OFF. Guarantee that for this session
-- (and restore the original mode at the end). STRICT/other modes are preserved.
SET @__old_sql_mode := @@session.sql_mode;
SET SESSION sql_mode := REPLACE(@@session.sql_mode, 'ANSI_QUOTES', '');

-- ---------------------------------------------------------------------
-- 0. Idempotency helper procedures (dropped again at the end of the file)
-- ---------------------------------------------------------------------
DROP PROCEDURE IF EXISTS pems_v2_add_column;
DROP PROCEDURE IF EXISTS pems_v2_add_index;
DROP PROCEDURE IF EXISTS pems_v2_add_constraint;

DELIMITER $$

CREATE PROCEDURE pems_v2_add_column(IN p_table VARCHAR(64), IN p_column VARCHAR(64), IN p_ddl TEXT)
BEGIN
  IF NOT EXISTS (
    SELECT 1 FROM information_schema.COLUMNS
    WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = p_table AND COLUMN_NAME = p_column
  ) THEN
    SET @s = CONCAT('ALTER TABLE `', p_table, '` ADD COLUMN ', p_ddl);
    PREPARE stmt FROM @s; EXECUTE stmt; DEALLOCATE PREPARE stmt;
  END IF;
END$$

CREATE PROCEDURE pems_v2_add_index(IN p_table VARCHAR(64), IN p_index VARCHAR(64), IN p_ddl TEXT)
BEGIN
  IF NOT EXISTS (
    SELECT 1 FROM information_schema.STATISTICS
    WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = p_table AND INDEX_NAME = p_index
  ) THEN
    SET @s = CONCAT('ALTER TABLE `', p_table, '` ADD ', p_ddl);
    PREPARE stmt FROM @s; EXECUTE stmt; DEALLOCATE PREPARE stmt;
  END IF;
END$$

CREATE PROCEDURE pems_v2_add_constraint(IN p_table VARCHAR(64), IN p_constraint VARCHAR(64), IN p_ddl TEXT)
BEGIN
  IF NOT EXISTS (
    SELECT 1 FROM information_schema.TABLE_CONSTRAINTS
    WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = p_table AND CONSTRAINT_NAME = p_constraint
  ) THEN
    SET @s = CONCAT('ALTER TABLE `', p_table, '` ADD CONSTRAINT ', p_ddl);
    PREPARE stmt FROM @s; EXECUTE stmt; DEALLOCATE PREPARE stmt;
  END IF;
END$$

DELIMITER ;

-- =====================================================================
-- 1. visit_requests — schema version + mixed flag + contact access state
--    (Plan §4.1, §16.4). Global form columns are KEPT as a compatibility
--    projection; they are no longer the operational source for v2.
-- =====================================================================
CALL pems_v2_add_column('visit_requests', 'form_schema_version',
  "form_schema_version TINYINT UNSIGNED NOT NULL DEFAULT 1 COMMENT 'Form contract version of this request: 1=legacy global-form, 2=per-campus detail' AFTER created_source");

CALL pems_v2_add_column('visit_requests', 'has_mixed_campus_details',
  "has_mixed_campus_details TINYINT(1) NOT NULL DEFAULT 0 COMMENT 'Backend-derived: 1 when campus detail snapshots differ across campuses. Never accepted from the client.' AFTER form_schema_version");

CALL pems_v2_add_column('visit_requests', 'primary_contact_access_status',
  "primary_contact_access_status ENUM('PENDING_CONFIRMATION','ACTIVE') NOT NULL DEFAULT 'PENDING_CONFIRMATION' COMMENT 'PENDING_CONFIRMATION=primary contact B has not claimed the request yet; ACTIVE=contact owner confirmed. Backfilled ACTIVE where visitor_user_id IS NOT NULL.' AFTER note_to_fptu");

CALL pems_v2_add_column('visit_requests', 'primary_contact_verified_at',
  "primary_contact_verified_at DATETIME NULL COMMENT 'When the primary contact claim/transfer was applied (Vietnam wall-clock).' AFTER primary_contact_access_status");

CALL pems_v2_add_index('visit_requests', 'idx_visit_requests_schema_version',
  "KEY idx_visit_requests_schema_version (form_schema_version, has_mixed_campus_details)");

CALL pems_v2_add_index('visit_requests', 'idx_visit_requests_contact_access',
  "KEY idx_visit_requests_contact_access (primary_contact_access_status)");

-- Composite unique key required so the per-instance guest link table can carry a
-- (visit_request_id, guest_member_id) FK. guest_member_id is already globally
-- unique, so this composite is trivially unique.
CALL pems_v2_add_index('visit_guest_members', 'uq_vgm_request_member',
  "UNIQUE KEY uq_vgm_request_member (visit_request_id, guest_member_id)");

-- =====================================================================
-- 2. visit_request_campuses — 30-minute duration guard + composite unique
--    (Plan §4.5, §17.2). The existing unnamed CHECK (planned_end_at >
--    planned_start_at) is retained; this adds the named minimum-duration rule.
--    PREFLIGHT MUST BE CLEAN or this ADD CONSTRAINT fails on violating rows.
-- =====================================================================
CALL pems_v2_add_constraint('visit_request_campuses', 'ck_visit_instance_min_duration_30m',
  "ck_visit_instance_min_duration_30m CHECK (TIMESTAMPDIFF(MINUTE, planned_start_at, planned_end_at) >= 30)");

-- Composite unique key so the per-instance guest link table can carry a
-- (visit_request_id, visit_instance_id) FK. visit_instance_id is already the PK,
-- so this composite is trivially unique.
CALL pems_v2_add_index('visit_request_campuses', 'uq_vrc_request_instance',
  "UNIQUE KEY uq_vrc_request_instance (visit_request_id, visit_instance_id)");

-- =====================================================================
-- 3. visit_instance_form_details — one full form snapshot PER campus instance
--    (Plan §4.2, §4.6). Active per-campus data lives here in v2.
-- =====================================================================
CREATE TABLE IF NOT EXISTS visit_instance_form_details (
  visit_instance_id BIGINT UNSIGNED NOT NULL,

  delegation_name VARCHAR(200) NOT NULL COMMENT 'Tên đoàn hiển thị tại campus này',
  visit_type ENUM('CAMPUS_TOUR','MEETING','WORKSHOP','SIGNING_CEREMONY','EXCHANGE','OTHER') NOT NULL DEFAULT 'CAMPUS_TOUR',
  visit_type_other VARCHAR(255) NULL,
  purpose TEXT NOT NULL COMMENT 'Mục đích tại campus này',
  working_content TEXT NULL COMMENT 'Nội dung làm việc tại campus này',

  operational_contact_full_name VARCHAR(150) NOT NULL COMMENT 'Đầu mối làm việc tại cơ sở này (snapshot vận hành, KHÔNG cấp quyền đăng nhập)',
  operational_contact_organization VARCHAR(255) NOT NULL,
  operational_contact_phone VARCHAR(50) NOT NULL,
  operational_contact_email VARCHAR(150) NOT NULL,

  working_language ENUM('VI','EN') NOT NULL DEFAULT 'EN',
  transportation_note TEXT NULL COMMENT 'Nhận diện phương tiện tới campus này',
  media_consent_status ENUM('AGREED','DECLINED') NOT NULL DEFAULT 'DECLINED',
  media_consent_note TEXT NULL,
  note_to_fptu TEXT NULL,

  -- Revision counters (Plan §4.6). form_revision increments on EVERY applied
  -- change; approval_revision only when an approval-sensitive amendment is applied.
  form_revision INT UNSIGNED NOT NULL DEFAULT 1,
  approval_revision INT UNSIGNED NOT NULL DEFAULT 1,
  row_version INT UNSIGNED NOT NULL DEFAULT 0 COMMENT 'Optimistic concurrency token',

  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  created_by BIGINT UNSIGNED NULL,
  updated_at DATETIME NULL DEFAULT NULL ON UPDATE CURRENT_TIMESTAMP,
  updated_by BIGINT UNSIGNED NULL,

  PRIMARY KEY (visit_instance_id),

  KEY idx_vifd_visit_type (visit_type),
  KEY idx_vifd_language (working_language),
  KEY idx_vifd_media_consent (media_consent_status),
  KEY idx_vifd_op_contact_email (operational_contact_email),
  FULLTEXT KEY ft_vifd_search (delegation_name, purpose, working_content,
    operational_contact_full_name, operational_contact_organization, operational_contact_email),

  CONSTRAINT ck_vifd_visit_type_other CHECK (visit_type <> 'OTHER'
    OR (visit_type_other IS NOT NULL AND TRIM(visit_type_other) <> '')),
  CONSTRAINT ck_vifd_delegation_name CHECK (TRIM(delegation_name) <> ''),
  CONSTRAINT ck_vifd_purpose CHECK (TRIM(purpose) <> ''),
  CONSTRAINT ck_vifd_op_contact_name CHECK (TRIM(operational_contact_full_name) <> ''),
  CONSTRAINT ck_vifd_op_contact_org CHECK (TRIM(operational_contact_organization) <> ''),
  CONSTRAINT ck_vifd_op_contact_phone CHECK (TRIM(operational_contact_phone) <> ''),
  CONSTRAINT ck_vifd_op_contact_email CHECK (TRIM(operational_contact_email) <> ''),

  -- ON DELETE CASCADE: deleting a pending campus instance removes its detail row.
  CONSTRAINT fk_vifd_instance
    FOREIGN KEY (visit_instance_id) REFERENCES visit_request_campuses (visit_instance_id)
    ON UPDATE CASCADE ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
COMMENT='Snapshot form đầy đủ, độc lập cho từng campus instance (v2). Mỗi row là một bản hoàn chỉnh; không có cờ same_as_other_campus.';

-- =====================================================================
-- 4. visit_instance_guest_members — per-campus guest/support links
--    (Plan §4.3). Composite FKs bind member AND instance to the SAME request,
--    which blocks linking a member of request A into an instance of request B.
-- =====================================================================
CREATE TABLE IF NOT EXISTS visit_instance_guest_members (
  visit_request_id BIGINT UNSIGNED NOT NULL,
  visit_instance_id BIGINT UNSIGNED NOT NULL,
  guest_member_id BIGINT UNSIGNED NOT NULL,
  display_order INT UNSIGNED NOT NULL DEFAULT 0,

  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  created_by BIGINT UNSIGNED NULL,

  PRIMARY KEY (visit_instance_id, guest_member_id),
  KEY idx_vigm_request (visit_request_id),
  KEY idx_vigm_member (guest_member_id),
  KEY idx_vigm_instance_order (visit_instance_id, display_order),

  -- ON DELETE CASCADE on the instance side: removing a campus instance cascades
  -- only its link rows (member rows and downstream FKs are untouched).
  CONSTRAINT fk_vigm_instance
    FOREIGN KEY (visit_request_id, visit_instance_id)
    REFERENCES visit_request_campuses (visit_request_id, visit_instance_id)
    ON UPDATE CASCADE ON DELETE CASCADE,
  CONSTRAINT fk_vigm_member
    FOREIGN KEY (visit_request_id, guest_member_id)
    REFERENCES visit_guest_members (visit_request_id, guest_member_id)
    ON UPDATE CASCADE ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
COMMENT='Bảng nối khách/đội hỗ trợ theo campus instance. Composite FK mang cả request để chống cross-request link; copy-on-write khi campus dùng chung member cũ được sửa.';

-- =====================================================================
-- 5. visit_request_identity_changes — INITIAL_CLAIM / TRANSFER state machine
--    (Plan §4.4, §9, §16.4). This is the CURRENT state of an invitation/transfer,
--    NOT a token store. Raw Google id token / OTP / acceptance token are never
--    stored here (only email_action_tokens keeps the single-use hash).
-- =====================================================================
CREATE TABLE IF NOT EXISTS visit_request_identity_changes (
  identity_change_id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
  visit_request_id BIGINT UNSIGNED NOT NULL,

  change_kind ENUM('INITIAL_CLAIM','TRANSFER') NOT NULL,
  target_relation ENUM('PRIMARY_CONTACT') NOT NULL DEFAULT 'PRIMARY_CONTACT',
  confirmation_method ENUM('GOOGLE_SSO','OTP_FALLBACK') NOT NULL DEFAULT 'GOOGLE_SSO',

  old_user_id BIGINT UNSIGNED NULL,
  new_user_id BIGINT UNSIGNED NULL,
  old_email_normalized VARCHAR(150) NULL,
  new_email_normalized VARCHAR(150) NULL COMMENT 'Required while PENDING; may be NULL after 90-day retention redaction',
  new_email_masked VARCHAR(150) NOT NULL COMMENT 'Always retained, even after redaction',
  pending_snapshot_json JSON NULL COMMENT 'Contact snapshot proposed by the requester; redacted after retention',

  status ENUM('PENDING','APPLIED','DECLINED','EXPIRED','CANCELLED','SUPERSEDED') NOT NULL DEFAULT 'PENDING',
  expected_request_row_version INT UNSIGNED NOT NULL,

  requested_by BIGINT UNSIGNED NOT NULL,
  requested_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  expires_at DATETIME NOT NULL,
  applied_at DATETIME NULL,
  declined_at DATETIME NULL,
  cancelled_at DATETIME NULL,
  superseded_at DATETIME NULL,
  retention_until DATETIME NULL,
  redacted_at DATETIME NULL,
  reason VARCHAR(500) NULL,
  resend_count INT UNSIGNED NOT NULL DEFAULT 0,

  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  updated_at DATETIME NULL DEFAULT NULL ON UPDATE CURRENT_TIMESTAMP,

  -- One in-flight (PENDING) change per (request, relation). MySQL has no partial
  -- unique index, so we index a VIRTUAL guard that is NULL for every non-PENDING row.
  pending_guard VARCHAR(80) GENERATED ALWAYS AS (
    CASE WHEN status = 'PENDING' THEN CONCAT(visit_request_id, ':', target_relation) ELSE NULL END
  ) VIRTUAL,

  PRIMARY KEY (identity_change_id),
  UNIQUE KEY uq_identity_change_pending (pending_guard),
  KEY idx_identity_change_request_relation_status (visit_request_id, target_relation, status),
  KEY idx_identity_change_status_expires (status, expires_at),
  KEY idx_identity_change_retention (status, retention_until),
  KEY idx_identity_change_new_email (new_email_normalized),

  -- NOTE: "TRANSFER requires old_user_id NOT NULL" is enforced by
  -- trg_identity_changes_transfer_bi/bu below, NOT a CHECK: MySQL 8.0 (error 3823)
  -- forbids a CHECK on old_user_id because that column carries an FK referential
  -- action (ON DELETE SET NULL). Same trigger workaround the schema uses elsewhere.
  CONSTRAINT fk_identity_change_request
    FOREIGN KEY (visit_request_id) REFERENCES visit_requests (visit_request_id)
    ON UPDATE CASCADE ON DELETE CASCADE,
  CONSTRAINT fk_identity_change_old_user
    FOREIGN KEY (old_user_id) REFERENCES users (user_id)
    ON UPDATE CASCADE ON DELETE SET NULL,
  CONSTRAINT fk_identity_change_new_user
    FOREIGN KEY (new_user_id) REFERENCES users (user_id)
    ON UPDATE CASCADE ON DELETE SET NULL,
  CONSTRAINT fk_identity_change_requested_by
    FOREIGN KEY (requested_by) REFERENCES users (user_id)
    ON UPDATE CASCADE ON DELETE RESTRICT
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
COMMENT='Trạng thái hiện tại của claim/transfer đầu mối chính. visitor_user_id chỉ được swap trong transaction chuyển sang APPLIED.';

-- Append-only history of every identity-change transition (Plan §4.4).
CREATE TABLE IF NOT EXISTS visit_request_identity_change_events (
  identity_change_event_id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
  identity_change_id BIGINT UNSIGNED NOT NULL,
  visit_request_id BIGINT UNSIGNED NOT NULL,
  event_type VARCHAR(80) NOT NULL,
  from_status VARCHAR(30) NULL,
  to_status VARCHAR(30) NULL,
  actor_user_id BIGINT UNSIGNED NULL,
  email_masked VARCHAR(150) NULL,
  reason VARCHAR(500) NULL,
  correlation_id VARCHAR(100) NULL,
  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,

  PRIMARY KEY (identity_change_event_id),
  KEY idx_ice_change (identity_change_id, created_at),
  KEY idx_ice_request (visit_request_id, created_at),
  KEY idx_ice_correlation (correlation_id),

  CONSTRAINT fk_ice_change
    FOREIGN KEY (identity_change_id) REFERENCES visit_request_identity_changes (identity_change_id)
    ON UPDATE CASCADE ON DELETE CASCADE,
  CONSTRAINT fk_ice_actor
    FOREIGN KEY (actor_user_id) REFERENCES users (user_id)
    ON UPDATE CASCADE ON DELETE SET NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
COMMENT='Append-only: mọi transition (PENDING/APPLIED/DECLINED/EXPIRED/CANCELLED/SUPERSEDED/RESENT/REDACTED). Không bao giờ update/delete ngoài retention job.';

-- TRANSFER must capture the old owner (Plan §4.4). Enforced by trigger because a
-- CHECK on old_user_id is rejected (MySQL error 3823 — FK referential action column).
DROP TRIGGER IF EXISTS trg_identity_changes_transfer_bi;
DROP TRIGGER IF EXISTS trg_identity_changes_transfer_bu;

DELIMITER $$
CREATE TRIGGER trg_identity_changes_transfer_bi
BEFORE INSERT ON visit_request_identity_changes
FOR EACH ROW
BEGIN
  IF NEW.change_kind = 'TRANSFER' AND NEW.old_user_id IS NULL THEN
    SIGNAL SQLSTATE '45000'
      SET MESSAGE_TEXT = 'TRANSFER identity change requires old_user_id (the current owner)';
  END IF;
END$$
CREATE TRIGGER trg_identity_changes_transfer_bu
BEFORE UPDATE ON visit_request_identity_changes
FOR EACH ROW
BEGIN
  IF NEW.change_kind = 'TRANSFER' AND NEW.old_user_id IS NULL THEN
    SIGNAL SQLSTATE '45000'
      SET MESSAGE_TEXT = 'TRANSFER identity change requires old_user_id (the current owner)';
  END IF;
END$$
DELIMITER ;

-- =====================================================================
-- 6. Amendment / revision state machine (Plan §4.6, §16.6).
--    Active data stays normalized; JSON only holds immutable proposal/history.
-- =====================================================================
CREATE TABLE IF NOT EXISTS visit_instance_amendments (
  amendment_id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
  visit_request_id BIGINT UNSIGNED NOT NULL,
  visit_instance_id BIGINT UNSIGNED NOT NULL,
  amendment_no INT UNSIGNED NOT NULL,

  status ENUM('DRAFT','PENDING_APPROVAL','APPROVED','REJECTED','WITHDRAWN','EXPIRED','CANCELLED') NOT NULL DEFAULT 'DRAFT',
  base_form_revision INT UNSIGNED NOT NULL,
  base_approval_revision INT UNSIGNED NOT NULL,

  requested_by BIGINT UNSIGNED NOT NULL,
  requested_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  reason VARCHAR(500) NULL,

  decided_by BIGINT UNSIGNED NULL,
  decided_at DATETIME NULL,
  decision_note VARCHAR(500) NULL,

  expires_at DATETIME NULL,
  withdrawn_at DATETIME NULL,
  expected_instance_row_version INT UNSIGNED NOT NULL,

  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  updated_at DATETIME NULL DEFAULT NULL ON UPDATE CURRENT_TIMESTAMP,

  -- Only one PENDING_APPROVAL amendment per instance (partial-unique via guard).
  amendment_pending_guard BIGINT UNSIGNED GENERATED ALWAYS AS (
    CASE WHEN status = 'PENDING_APPROVAL' THEN visit_instance_id ELSE NULL END
  ) VIRTUAL,

  PRIMARY KEY (amendment_id),
  UNIQUE KEY uq_amendment_pending (amendment_pending_guard),
  UNIQUE KEY uq_amendment_instance_no (visit_instance_id, amendment_no),
  KEY idx_amendment_instance_status_time (visit_instance_id, status, requested_at),
  KEY idx_amendment_request (visit_request_id, status),

  CONSTRAINT fk_amendment_instance
    FOREIGN KEY (visit_request_id, visit_instance_id)
    REFERENCES visit_request_campuses (visit_request_id, visit_instance_id)
    ON UPDATE CASCADE ON DELETE CASCADE,
  CONSTRAINT fk_amendment_requested_by
    FOREIGN KEY (requested_by) REFERENCES users (user_id)
    ON UPDATE CASCADE ON DELETE RESTRICT,
  CONSTRAINT fk_amendment_decided_by
    FOREIGN KEY (decided_by) REFERENCES users (user_id)
    ON UPDATE CASCADE ON DELETE SET NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
COMMENT='Đề xuất thay đổi approval-sensitive cho một campus instance đã duyệt. Chỉ một PENDING_APPROVAL/instance; FK mang cả request để chống gắn sai request.';

CREATE TABLE IF NOT EXISTS visit_instance_amendment_changes (
  amendment_change_id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
  amendment_id BIGINT UNSIGNED NOT NULL,
  field_path VARCHAR(150) NOT NULL COMMENT 'Stable path, e.g. purpose, delegationName, members.GUEST[456].status',
  change_class VARCHAR(40) NOT NULL COMMENT 'SAFE | APPROVAL_SENSITIVE | STRUCTURAL | PRIVACY_URGENT',
  old_value_json JSON NULL,
  new_value_json JSON NULL,
  is_sensitive TINYINT(1) NOT NULL DEFAULT 0,
  display_order INT UNSIGNED NOT NULL DEFAULT 0,
  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,

  PRIMARY KEY (amendment_change_id),
  KEY idx_amendment_change_amendment (amendment_id, display_order),

  CONSTRAINT fk_amendment_change_amendment
    FOREIGN KEY (amendment_id) REFERENCES visit_instance_amendments (amendment_id)
    ON UPDATE CASCADE ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
COMMENT='Field-level proposal của một amendment (immutable sau khi PENDING_APPROVAL).';

CREATE TABLE IF NOT EXISTS visit_instance_form_revision_history (
  revision_history_id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
  visit_request_id BIGINT UNSIGNED NOT NULL,
  visit_instance_id BIGINT UNSIGNED NOT NULL,
  form_revision INT UNSIGNED NOT NULL,
  approval_revision INT UNSIGNED NOT NULL,
  source_type ENUM('CREATE','SAFE_EDIT','AMENDMENT_APPLIED','MIGRATION','RESUBMIT') NOT NULL,
  source_id BIGINT UNSIGNED NULL COMMENT 'e.g. amendment_id for AMENDMENT_APPLIED',
  snapshot_json JSON NOT NULL COMMENT 'Immutable full detail snapshot AFTER this revision applied',
  applied_by BIGINT UNSIGNED NULL,
  applied_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  reason VARCHAR(500) NULL,

  PRIMARY KEY (revision_history_id),
  UNIQUE KEY uq_vifrh_instance_form_revision (visit_instance_id, form_revision),
  KEY idx_vifrh_request_time (visit_request_id, applied_at),
  KEY idx_vifrh_source (source_type, source_id),

  CONSTRAINT fk_vifrh_instance
    FOREIGN KEY (visit_request_id, visit_instance_id)
    REFERENCES visit_request_campuses (visit_request_id, visit_instance_id)
    ON UPDATE CASCADE ON DELETE CASCADE,
  CONSTRAINT fk_vifrh_applied_by
    FOREIGN KEY (applied_by) REFERENCES users (user_id)
    ON UPDATE CASCADE ON DELETE SET NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
COMMENT='Lịch sử revision per-instance; mỗi form_revision là một snapshot immutable.';

-- Request-level snapshot history (registrant + primary-contact DISPLAY fields).
-- Email/account relation history is kept by identity-change events, not here.
CREATE TABLE IF NOT EXISTS visit_request_revision_history (
  request_revision_history_id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
  visit_request_id BIGINT UNSIGNED NOT NULL,
  request_revision INT UNSIGNED NOT NULL,
  source_type ENUM('CREATE','SAFE_EDIT','MIGRATION','RESUBMIT') NOT NULL,
  source_id BIGINT UNSIGNED NULL,
  snapshot_json JSON NOT NULL,
  applied_by BIGINT UNSIGNED NULL,
  applied_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  reason VARCHAR(500) NULL,

  PRIMARY KEY (request_revision_history_id),
  UNIQUE KEY uq_vrrh_request_revision (visit_request_id, request_revision),
  KEY idx_vrrh_request_time (visit_request_id, applied_at),

  CONSTRAINT fk_vrrh_request
    FOREIGN KEY (visit_request_id) REFERENCES visit_requests (visit_request_id)
    ON UPDATE CASCADE ON DELETE CASCADE,
  CONSTRAINT fk_vrrh_applied_by
    FOREIGN KEY (applied_by) REFERENCES users (user_id)
    ON UPDATE CASCADE ON DELETE SET NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
COMMENT='Lịch sử snapshot cấp request (display fields). Quan hệ email/account lấy identity-change events làm lịch sử chính.';

-- =====================================================================
-- 7. Audit hardening (Plan §4.7). Additive columns + indexes only.
-- =====================================================================
CALL pems_v2_add_column('audit_logs', 'correlation_id',
  "correlation_id VARCHAR(100) NULL AFTER request_id");
CALL pems_v2_add_column('audit_logs', 'visit_request_id',
  "visit_request_id BIGINT UNSIGNED NULL AFTER correlation_id");
CALL pems_v2_add_column('audit_logs', 'visit_instance_id',
  "visit_instance_id BIGINT UNSIGNED NULL AFTER visit_request_id");
CALL pems_v2_add_column('audit_logs', 'source_type',
  "source_type VARCHAR(80) NULL AFTER visit_instance_id");
CALL pems_v2_add_column('audit_logs', 'source_id',
  "source_id BIGINT UNSIGNED NULL AFTER source_type");
CALL pems_v2_add_column('audit_logs', 'reason',
  "reason VARCHAR(500) NULL AFTER source_id");

CALL pems_v2_add_index('audit_logs', 'idx_audit_visit_request_time',
  "KEY idx_audit_visit_request_time (visit_request_id, created_at)");
CALL pems_v2_add_index('audit_logs', 'idx_audit_visit_instance_time',
  "KEY idx_audit_visit_instance_time (visit_instance_id, created_at)");
CALL pems_v2_add_index('audit_logs', 'idx_audit_correlation',
  "KEY idx_audit_correlation (correlation_id)");
CALL pems_v2_add_index('audit_logs', 'idx_audit_source',
  "KEY idx_audit_source (source_type, source_id)");

CALL pems_v2_add_column('audit_log_changes', 'change_category',
  "change_category VARCHAR(40) NULL AFTER field_name");
CALL pems_v2_add_column('audit_log_changes', 'value_format',
  "value_format VARCHAR(20) NOT NULL DEFAULT 'TEXT' AFTER change_category");
CALL pems_v2_add_column('audit_log_changes', 'is_sensitive',
  "is_sensitive TINYINT(1) NOT NULL DEFAULT 0 AFTER value_format");
CALL pems_v2_add_column('audit_log_changes', 'display_order',
  "display_order INT UNSIGNED NOT NULL DEFAULT 0 AFTER is_sensitive");

-- NOTE on audit FKs: audit_logs.visit_request_id / visit_instance_id are LEFT
-- NULLABLE and intentionally carry NO foreign key. Audit history must survive a
-- business row being deleted; §17.2 forbids cascade-deleting audit with business rows.

-- =====================================================================
-- 8. Cancel trigger — exception 3A (Plan §16.7, §19.6).
--    Registrant may cancel ONLY while the initial primary contact has not
--    claimed (primary_contact_access_status = PENDING_CONFIRMATION). Once the
--    contact is ACTIVE, cancel reverts to the exact contact-owner rule. Every
--    other guard (status window / 24h / already-started campus) is unchanged.
-- =====================================================================
DROP TRIGGER IF EXISTS trg_visit_requests_cancel_validate_bu;

DELIMITER $$

CREATE TRIGGER trg_visit_requests_cancel_validate_bu
BEFORE UPDATE ON visit_requests
FOR EACH ROW
BEGIN
  DECLARE v_cancel_role_code VARCHAR(30);
  DECLARE v_started_campus_count INT DEFAULT 0;
  DECLARE v_cancel_window_violation_count INT DEFAULT 0;

  IF NEW.status = 'CANCELLED' AND OLD.status <> 'CANCELLED' THEN
    -- Visitor được hủy request tổng khi còn PENDING_APPROVAL hoặc khi đã APPROVED nhưng chưa campus nào bắt đầu.
    IF OLD.status NOT IN ('APPROVED', 'PARTIALLY_APPROVED', 'PENDING_APPROVAL') THEN
      SIGNAL SQLSTATE '45000'
        SET MESSAGE_TEXT = 'Only pending or approved request/delegation can be cancelled';
    END IF;

    IF NEW.cancelled_by IS NULL OR NEW.cancelled_at IS NULL THEN
      SIGNAL SQLSTATE '45000'
        SET MESSAGE_TEXT = 'cancelled_by and cancelled_at are required when request is cancelled';
    END IF;

    IF NEW.cancellation_reason IS NULL OR TRIM(NEW.cancellation_reason) = '' THEN
      SIGNAL SQLSTATE '45000'
        SET MESSAGE_TEXT = 'cancellation_reason is required when request/delegation is cancelled';
    END IF;

    SELECT r.role_code INTO v_cancel_role_code
    FROM users u
    JOIN roles r ON r.role_id = u.role_id
    WHERE u.user_id = NEW.cancelled_by;

    -- Actor relation (cancel exception 3A, §16.7/§19.6). Discriminate on the contact
    -- access state, with NULL-safe comparisons throughout:
    --   ACTIVE               -> only the exact contact owner (visitor_user_id), VISITOR.
    --   PENDING_CONFIRMATION -> the registrant (exception 3A) or, if already set, the
    --                           contact owner; role must be in the VISITOR/STAFF create
    --                           group. HO/ADMIN/DEPARTMENT/STUDENT never gain cancel via role.
    IF NEW.primary_contact_access_status = 'ACTIVE' THEN
      IF NEW.visitor_user_id IS NULL OR NEW.cancelled_by <> NEW.visitor_user_id THEN
        SIGNAL SQLSTATE '45000'
          SET MESSAGE_TEXT = 'Only the contact owner (visitor_user_id) can cancel this request';
      END IF;
      IF v_cancel_role_code <> 'VISITOR' THEN
        SIGNAL SQLSTATE '45000'
          SET MESSAGE_TEXT = 'Contact owner cancelling the request must be a VISITOR';
      END IF;
    ELSE
      IF (NEW.registrant_user_id IS NULL OR NEW.cancelled_by <> NEW.registrant_user_id)
         AND (NEW.visitor_user_id IS NULL OR NEW.cancelled_by <> NEW.visitor_user_id) THEN
        SIGNAL SQLSTATE '45000'
          SET MESSAGE_TEXT = 'Only the registrant (while initial contact is pending) or the contact owner can cancel this request';
      END IF;
      IF v_cancel_role_code NOT IN ('VISITOR','STAFF') THEN
        SIGNAL SQLSTATE '45000'
          SET MESSAGE_TEXT = 'Registrant-exception cancel is limited to VISITOR/STAFF create-group accounts';
      END IF;
    END IF;

    -- Visitor self-service cancellation must be at least 24 hours before every active campus schedule.
    SELECT COUNT(*) INTO v_cancel_window_violation_count
    FROM visit_request_campuses vrc
    WHERE vrc.visit_request_id = OLD.visit_request_id
      AND vrc.status NOT IN ('CANCELLED','REJECTED')
      AND vrc.planned_start_at < DATE_ADD(NEW.cancelled_at, INTERVAL 24 HOUR);

    IF v_cancel_window_violation_count > 0 THEN
      SIGNAL SQLSTATE '45000'
        SET MESSAGE_TEXT = 'Cannot cancel the main visit request within 24 hours of any active campus visit';
    END IF;

    -- After APPROVED, block whole-request cancel if any campus already started.
    IF OLD.status IN ('APPROVED','PARTIALLY_APPROVED') THEN
      SELECT COUNT(*) INTO v_started_campus_count
      FROM visit_request_campuses vrc
      WHERE vrc.visit_request_id = OLD.visit_request_id
        AND vrc.status IN ('DURING_VISIT','AFTER_VISIT','CLOSED');

      IF v_started_campus_count > 0 THEN
        SIGNAL SQLSTATE '45000'
          SET MESSAGE_TEXT = 'Request has campus visit(s) already started; cancel each not-yet-started campus instead of cancelling the whole request';
      END IF;
    END IF;
  END IF;
END$$

DELIMITER ;

-- ---------------------------------------------------------------------
-- 9. Drop idempotency helpers.
-- ---------------------------------------------------------------------
DROP PROCEDURE IF EXISTS pems_v2_add_column;
DROP PROCEDURE IF EXISTS pems_v2_add_index;
DROP PROCEDURE IF EXISTS pems_v2_add_constraint;

-- Restore the caller's original sql_mode.
SET SESSION sql_mode := @__old_sql_mode;

-- =====================================================================
-- END OF UP. Next: 03_backfill.sql
-- =====================================================================
