-- =====================================================================
-- 02_up_additive.sql — adds email_send_idempotency (G11 / R-103)
--
-- Purely additive. It creates ONE table and touches nothing else: no template, no draft, no sent email,
-- no attachment, no history row is read, written or deleted by this script.
--
-- SAFETY GUARD — you must name the database you intend to change, on the same session:
--
--     SET @pems_idem_confirm_database = 'pems_stage_2026_08';
--     USE pems_stage_2026_08;
--     SOURCE 02_up_additive.sql;
--
-- Running it without that variable, or with a value that does not match DATABASE(), aborts with
-- SQLSTATE 45000 before any DDL. The confirmation is SPENT at the end of the script — a second run on
-- the same session must name its target again, so a pooled connection cannot carry authorisation
-- forward into a migration nobody asked for.
--
-- Idempotent: running it twice makes no second change and creates no duplicate index. Verify with
-- 03_verify.sql after each run.
-- =====================================================================

-- The column comments below are Vietnamese, and the CHECK constraint's literals are stored with the
-- character set of the connection that created them. The mysql client on Windows defaults to the
-- console codepage, so WITHOUT this line the comments land as mojibake and the constraint is recorded
-- with _cp850 literals — measured, not assumed. The canonical script sets the same thing for the same
-- reason.
SET NAMES utf8mb4;

-- ── Guard ────────────────────────────────────────────────────────────
-- SIGNAL is only legal inside a compound statement, so the guard is a procedure that is created,
-- called and dropped. It runs before the CREATE TABLE below.

DROP PROCEDURE IF EXISTS pems_idem_migration_guard;

DELIMITER $

CREATE PROCEDURE pems_idem_migration_guard()
BEGIN
  IF DATABASE() IS NULL THEN
    SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT =
      'No database selected. Connect with a database, or USE one, before running this migration.';
  END IF;

  IF @pems_idem_confirm_database IS NULL OR @pems_idem_confirm_database <> DATABASE() THEN
    SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT =
      'Refusing to migrate: set @pems_idem_confirm_database to the exact name of the database you intend to change.';
  END IF;

  IF (SELECT COUNT(*) FROM information_schema.tables
      WHERE table_schema = DATABASE() AND table_name = 'users') = 0 THEN
    SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT =
      'Refusing to migrate: table `users` is missing, so the actor foreign key cannot be created.';
  END IF;

  IF (SELECT COUNT(*) FROM information_schema.tables
      WHERE table_schema = DATABASE() AND table_name = 'sent_emails') = 0 THEN
    SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT =
      'Refusing to migrate: table `sent_emails` is missing, so the history foreign key cannot be created.';
  END IF;
END$

DELIMITER ;

CALL pems_idem_migration_guard();
DROP PROCEDURE pems_idem_migration_guard;


-- ── The table ────────────────────────────────────────────────────────
-- IF NOT EXISTS is what makes a second run a no-op. It is NOT a way to tolerate a table of the wrong
-- shape: 03_verify.sql compares the shape column by column and fails when it differs.

CREATE TABLE IF NOT EXISTS email_send_idempotency (
  email_send_idempotency_id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,

  actor_user_id BIGINT UNSIGNED NOT NULL
    COMMENT 'Người bấm gửi, đọc từ JWT đã xác thực — không bao giờ từ payload',
  operation_code VARCHAR(64) NOT NULL
    COMMENT 'Một trong sáu hành động gửi báo cáo/hóa đơn, ví dụ REPORT_HO_CAMPUS',
  idempotency_key_hash CHAR(64) NOT NULL
    COMMENT 'SHA-256 (hex) của Idempotency-Key — KHÔNG lưu key gốc',
  request_fingerprint CHAR(64) NOT NULL
    COMMENT 'SHA-256 (hex) của nội dung nghiệp vụ đã chuẩn hoá; cùng key khác fingerprint = từ chối, không gửi',

  state VARCHAR(32) NOT NULL DEFAULT 'RESERVED'
    COMMENT 'RESERVED / PREPARING / DISPATCHING / SUCCEEDED / FAILED_BEFORE_DISPATCH / OUTCOME_UNKNOWN',

  sent_email_id BIGINT UNSIGNED NULL
    COMMENT 'Bản ghi lịch sử của lần gửi thành công, để đối chiếu',
  result_message VARCHAR(500) NULL
    COMMENT 'Thông báo thành công, phát lại nguyên văn cho lần gọi trùng',
  failure_code VARCHAR(64) NULL
    COMMENT 'Mã lỗi ổn định; không chứa địa chỉ, số tiền, token hay nội dung thư',

  attempt_count INT UNSIGNED NOT NULL DEFAULT 0
    COMMENT 'Số lần handler thực sự chạy dưới key này (chỉ tăng khi retry sau FAILED_BEFORE_DISPATCH)',

  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  updated_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  dispatch_started_at DATETIME NULL
    COMMENT 'Thời điểm ngay trước lời gọi ra ngoài; có giá trị nghĩa là không thể khẳng định chưa gửi',
  completed_at DATETIME NULL,

  PRIMARY KEY (email_send_idempotency_id),

  -- The whole contract rests on this one constraint: it is what makes two concurrent requests with the
  -- same key collide in the database rather than in application code.
  UNIQUE KEY uq_email_send_idempotency_actor_op_key
    (actor_user_id, operation_code, idempotency_key_hash),

  KEY idx_email_send_idempotency_state (state, created_at),
  KEY idx_email_send_idempotency_sent_email (sent_email_id),

  CONSTRAINT chk_email_send_idempotency_state
    CHECK (state IN ('RESERVED','PREPARING','DISPATCHING','SUCCEEDED',
                     'FAILED_BEFORE_DISPATCH','OUTCOME_UNKNOWN')),

  -- RESTRICT, not CASCADE: this table is the record of what a person sent. Deleting the person must
  -- not silently delete the evidence, and PEMS hard-deletes no user anywhere in the backend.
  CONSTRAINT fk_email_send_idempotency_actor
    FOREIGN KEY (actor_user_id) REFERENCES users(user_id)
    ON UPDATE CASCADE ON DELETE RESTRICT,

  -- SET NULL: if a history row is ever removed the reservation stays, because the fact that a send
  -- happened is exactly what must survive.
  CONSTRAINT fk_email_send_idempotency_sent_email
    FOREIGN KEY (sent_email_id) REFERENCES sent_emails(sent_email_id)
    ON UPDATE CASCADE ON DELETE SET NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
COMMENT='Chống gửi trùng cho sáu hành động gửi báo cáo/hóa đơn (G11 / R-103). Chỉ lưu hash của key và của yêu cầu.';


-- ── Spend the confirmation ───────────────────────────────────────────
-- The next run on this session must name its target again.
SET @pems_idem_confirm_database = NULL;

SELECT 'email_dispatch_idempotency migration applied. Run 03_verify.sql next.' AS result;
