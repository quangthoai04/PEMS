-- =====================================================================
-- 02_sync_templates.sql — bring email_templates in line with the canonical catalog
--
-- Run AFTER 01_preflight.sql on the same connection/session, and BEFORE 03_verify.sql.
--
-- GENERATED, DO NOT HAND-EDIT. The 31 VALUES rows below are the CANONICAL SEED's own values, read
-- back out of a database freshly imported from
--   docs/database/scripts/PEMS_FULL_VS_31_07_NEW.sql
-- and not yet synced — so they are what that file produces, byte for byte, rather than a
-- transcription of it. To regenerate after a deliberate catalog change, follow the procedure in
-- 04_rollback_guidance.md.
--
-- The header used to claim these rows were "lifted verbatim" from the seed. They were not: measured
-- 2026-08-02, they matched backend/PEMS.Application/Emails/Common/Assets/email-template-defaults.json
-- on all six text columns for 31 of 31 templates, and differed from the seed on 26 of them. The
-- consequence was the opposite of what this script is for — importing the seed and then running this
-- reported "31 updated" and rewrote the subject, body, name and description of 26 templates to
-- different wording. Regenerating from the seed makes the claim true and the first run a near no-op.
--
-- NOTE for whoever regenerates next: the seed and email-template-defaults.json still disagree on
-- prose for those 26 (the seed's wording is markedly terser). That divergence is real, predates this
-- script, and is asserted by EmailTemplateDefaultsParityTests — it is a content decision for the
-- product owner, NOT something to paper over by regenerating this file from the JSON again.
--
-- What it does, and just as importantly what it does not:
--   * upserts the 31 canonical templates BY template_code — never by numeric
--     email_template_id, so an existing row keeps its id and every sent_emails / email_drafts
--     foreign key pointing at it stays valid;
--   * updates an existing row only where a column actually differs, so re-running does not churn
--     updated_at and does not write rows the binlog will replicate for no reason;
--   * flips the 9 legacy codes of DL-03 to INACTIVE. It does NOT delete them: seeded
--     sent_emails rows may still hold a foreign key to them, and history must stay readable;
--   * leaves every other row alone. A template an operator authored in the admin UI is neither
--     deactivated nor deleted nor rewritten;
--   * never touches sent_emails, sent_email_recipients, sent_email_attachments, email_drafts,
--     email_draft_recipients, email_action_tokens, files, or anything outside email_templates.
--
-- Idempotent: running it twice changes nothing the second time (asserted by 03_verify.sql).
-- =====================================================================

-- ── Connection character set ──────────────────────────────────────────────────────────────────
-- EVERY subject and body below is Vietnamese, and the mysql client on Windows defaults to the
-- console codepage (cp850/cp1252). Without this line the client tells the server "these bytes are
-- cp850", the server transcodes them, and all thirty templates land as mojibake — measured, not
-- assumed: a CLI run against a freshly imported canonical database reported 30 rows "updated" when
-- the correct answer is zero, and left "Tài khoản" stored as "T├ái khoß║ún".
--
-- It hid for a while because the automated suite connects through MySql.Data, which is already
-- UTF-8, and because a snapshot taken through the same mis-configured client compares mangled text
-- to mangled text and finds them equal. The canonical seed script sets the same thing, for the same
-- reason.
SET NAMES utf8mb4;

-- ── Guard: refuse to run anywhere the operator has not named explicitly ────────────────────────
-- Set this on the SAME session before sourcing the file:
--     SET @pems_sync_confirm_database = '<exact database name>';
-- Typing the name is the confirmation. Without it the script stops before writing anything, which
-- is what stands between "I sourced the wrong file in the wrong tab" and a rewritten catalog.
--
-- The variable is SESSION-scoped, and the script clears it again on the way out (last statement).
-- That matters wherever a session outlives one use of it — a connection pool, or a mysql client left
-- open across several files: without the reset, one confirmation would silently authorise every
-- later run on the same connection. Measured, not assumed: before the reset existed, running this
-- script twice on a pooled connection let the second run through with no confirmation at all.
DELIMITER $$
DROP PROCEDURE IF EXISTS pems_email_sync_guard$$
CREATE PROCEDURE pems_email_sync_guard()
BEGIN
  IF DATABASE() IS NULL THEN
    SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT =
      'No database selected. USE the target database before sourcing 02_sync_templates.sql.';
  END IF;
  IF @pems_sync_confirm_database IS NULL OR @pems_sync_confirm_database <> DATABASE() THEN
    SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT =
      'Refusing to sync: set @pems_sync_confirm_database to the exact name of the database you intend to modify.';
  END IF;
  IF NOT EXISTS (SELECT 1 FROM information_schema.tables
                 WHERE table_schema = DATABASE() AND table_name = 'email_templates') THEN
    SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT =
      'Refusing to sync: email_templates does not exist. Run 01_preflight.sql and read its output.';
  END IF;
END$$
DELIMITER ;
CALL pems_email_sync_guard();
DROP PROCEDURE pems_email_sync_guard;

START TRANSACTION;

-- ── Canonical catalog, staged ─────────────────────────────────────────────────────────────────
DROP TEMPORARY TABLE IF EXISTS _pems_canonical_templates;
CREATE TEMPORARY TABLE _pems_canonical_templates (
  template_code  VARCHAR(100) NOT NULL,
  name           VARCHAR(150) NOT NULL,
  purpose        VARCHAR(100) NOT NULL,
  campus_id      BIGINT UNSIGNED NULL,
  description    VARCHAR(500) NULL,
  status         ENUM('ACTIVE','INACTIVE') NOT NULL DEFAULT 'ACTIVE',
  subject_vi     VARCHAR(255) NULL,
  body_vi        LONGTEXT NULL,
  subject_en     VARCHAR(255) NULL,
  body_en        LONGTEXT NULL,
  body_format    ENUM('PLAIN_TEXT','HTML') NOT NULL DEFAULT 'HTML',
  variables_text VARCHAR(700) NULL,
  created_at     DATETIME NOT NULL,
  PRIMARY KEY (template_code)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

INSERT INTO _pems_canonical_templates
  (template_code, name, purpose, campus_id, description, status,
   subject_vi, body_vi, subject_en, body_en, body_format, variables_text, created_at)
VALUES

-- ── ACCOUNT ─────────────────────────────────────────────────────────────────
  ('ACCOUNT_EMAIL_CONFIRMATION',
   'Xác nhận email tài khoản',
   'ACCOUNT', NULL,
   'Xác nhận quyền sở hữu email cho tài khoản nội bộ mới.',
   'ACTIVE',
   'Xác nhận email cho tài khoản PEMS',
   CONCAT(
     '<p>Xin chào {{fullName}},</p>',
     '<p>Vai trò: {{roleName}}; campus: {{campusName}}; liên kết hết hạn sau {{expiresInHours}} giờ.</p>',
     '{{actionBlock}}'),
   'Confirm your PEMS account email',
   CONCAT(
     '<p>Hello {{fullName}},</p>',
     '<p>Role: {{roleName}}; campus: {{campusName}}; the link expires in {{expiresInHours}} hours.</p>',
     '{{actionBlock}}'),
   'HTML', 'fullName,roleName,campusName,expiresInHours', CURRENT_TIMESTAMP),

  ('ACCOUNT_PENDING_EMAIL_CHANGED_OLD_NOTICE',
   'Cảnh báo đổi email tài khoản chờ xác nhận',
   'ACCOUNT', NULL,
   'Cảnh báo tới email cũ khi email của tài khoản chưa kích hoạt bị thay đổi.',
   'ACTIVE',
   'Email chờ xác nhận của tài khoản PEMS đã thay đổi',
   '<p>Email đang chờ xác nhận của tài khoản PEMS vừa được thay đổi. Nếu bạn không thực hiện, hãy liên hệ quản trị viên.</p>',
   'Your pending PEMS email was changed',
   '<p>The pending email of your PEMS account was changed. Contact an administrator if this was not you.</p>',
   'HTML', NULL, CURRENT_TIMESTAMP),

  ('ACCOUNT_ACTIVATED',
   'Tài khoản đã kích hoạt',
   'ACCOUNT', NULL,
   'Thông báo tài khoản đã được kích hoạt.',
   'ACTIVE',
   'Tài khoản PEMS đã được kích hoạt',
   CONCAT(
     '<p>Xin chào {{fullName}},</p>',
     '<p>Tài khoản {{roleName}} tại {{campusName}} đã hoạt động.</p>'),
   'Your PEMS account is active',
   CONCAT(
     '<p>Hello {{fullName}},</p>',
     '<p>Your {{roleName}} account at {{campusName}} is active.</p>'),
   'HTML', 'fullName,roleName,campusName', CURRENT_TIMESTAMP),

  ('ACCOUNT_EMAIL_CHANGED_OLD_NOTICE',
   'Cảnh báo email cũ',
   'ACCOUNT', NULL,
   'Thông báo bảo mật tới email cũ sau khi đổi email.',
   'ACTIVE',
   'Email tài khoản PEMS đã được thay đổi',
   '<p>Email đăng nhập của tài khoản PEMS vừa được thay đổi. Nếu bạn không thực hiện, hãy liên hệ quản trị viên.</p>',
   'Your PEMS email was changed',
   '<p>Your PEMS sign-in email was changed. Contact an administrator if this was not you.</p>',
   'HTML', NULL, CURRENT_TIMESTAMP),

  ('ACCOUNT_EMAIL_CHANGED_NEW_NOTICE',
   'Thông báo email mới',
   'ACCOUNT', NULL,
   'Thông báo tới email mới sau khi đổi email.',
   'ACTIVE',
   'Email mới đã được gắn với tài khoản PEMS',
   CONCAT(
     '<p>Xin chào {{fullName}},</p>',
     '<p>Email cũ {{oldEmailMasked}} đã được thay thế thành công.</p>'),
   'Your new PEMS email is active',
   CONCAT(
     '<p>Hello {{fullName}},</p>',
     '<p>The previous email {{oldEmailMasked}} was replaced successfully.</p>'),
   'HTML', 'fullName,oldEmailMasked', CURRENT_TIMESTAMP),

  ('ACCOUNT_ROLE_CHANGED',
   'Vai trò tài khoản thay đổi',
   'ACCOUNT', NULL,
   'Thông báo thay đổi vai trò người dùng.',
   'ACTIVE',
   'Vai trò PEMS của bạn đã thay đổi',
   CONCAT(
     '<p>Xin chào {{fullName}},</p>',
     '<p>Vai trò tại {{campusName}} đổi từ {{oldRoleName}} thành {{newRoleName}}.</p>'),
   'Your PEMS role changed',
   CONCAT(
     '<p>Hello {{fullName}},</p>',
     '<p>Your role at {{campusName}} changed from {{oldRoleName}} to {{newRoleName}}.</p>'),
   'HTML', 'fullName,oldRoleName,newRoleName,campusName', CURRENT_TIMESTAMP),

  ('ACCOUNT_STAFF_LEADER_ASSIGNED',
   'Bổ nhiệm Staff Leader',
   'ACCOUNT', NULL,
   'Thông báo bổ nhiệm Staff Leader.',
   'ACTIVE',
   'Bạn được bổ nhiệm Staff Leader',
   CONCAT(
     '<p>Xin chào {{fullName}},</p>',
     '<p>Bạn được bổ nhiệm tại {{campusName}} từ {{effectiveDate}}. Lý do: {{reason}}.</p>'),
   'You were assigned Staff Leader',
   CONCAT(
     '<p>Hello {{fullName}},</p>',
     '<p>You were assigned at {{campusName}} from {{effectiveDate}}. Reason: {{reason}}.</p>'),
   'HTML', 'fullName,campusName,effectiveDate,reason', CURRENT_TIMESTAMP),

  ('ACCOUNT_STAFF_LEADER_REPLACED',
   'Thay thế Staff Leader',
   'ACCOUNT', NULL,
   'Thông báo kết thúc nhiệm kỳ Staff Leader.',
   'ACTIVE',
   'Nhiệm kỳ Staff Leader đã được bàn giao',
   CONCAT(
     '<p>Xin chào {{fullName}},</p>',
     '<p>Tại {{campusName}}, {{successorName}} tiếp nhận từ {{effectiveDate}}. Lý do: {{reason}}.</p>'),
   'Your Staff Leader assignment was handed over',
   CONCAT(
     '<p>Hello {{fullName}},</p>',
     '<p>At {{campusName}}, {{successorName}} takes over from {{effectiveDate}}. Reason: {{reason}}.</p>'),
   'HTML', 'fullName,campusName,successorName,effectiveDate,reason', CURRENT_TIMESTAMP),

-- ── DEPARTMENT PERSONNEL (Trưởng phòng quản lý nhân sự phòng mình) ──────────
  ('DEPT_PERSONNEL_ACCOUNT_DISABLED',
   'Khóa tài khoản nhân sự phòng ban',
   'ACCOUNT', NULL,
   'Thông báo khóa tài khoản nhân sự phòng ban.',
   'ACTIVE',
   'Tài khoản phòng ban đã bị vô hiệu hóa',
   CONCAT(
     '<p>Xin chào {{fullName}},</p>',
     '<p>Tài khoản tại {{departmentName}} đã bị vô hiệu hóa. Lý do: {{reason}}.</p>'),
   'Your department account was disabled',
   CONCAT(
     '<p>Hello {{fullName}},</p>',
     '<p>Your account in {{departmentName}} was disabled. Reason: {{reason}}.</p>'),
   'HTML', 'fullName,departmentName,reason', CURRENT_TIMESTAMP),

  ('DEPT_PERSONNEL_ACCOUNT_ENABLED',
   'Mở lại tài khoản nhân sự phòng ban',
   'ACCOUNT', NULL,
   'Thông báo mở lại tài khoản nhân sự phòng ban.',
   'ACTIVE',
   'Tài khoản phòng ban đã hoạt động',
   CONCAT(
     '<p>Xin chào {{fullName}},</p>',
     '<p>Tài khoản tại {{departmentName}} đã được mở lại.</p>'),
   'Your department account is active',
   CONCAT(
     '<p>Hello {{fullName}},</p>',
     '<p>Your account in {{departmentName}} is active again.</p>'),
   'HTML', 'fullName,departmentName', CURRENT_TIMESTAMP),

  ('DEPT_LEADERSHIP_GRANTED',
   'Bổ nhiệm Department Leader',
   'ACCOUNT', NULL,
   'Thông báo trao quyền Department Leader.',
   'ACTIVE',
   'Bạn được trao quyền Department Leader',
   CONCAT(
     '<p>Xin chào {{fullName}},</p>',
     '<p>Bạn là Department Leader của {{departmentName}}.</p>'),
   'Department leadership granted',
   CONCAT(
     '<p>Hello {{fullName}},</p>',
     '<p>You are now the Department Leader of {{departmentName}}.</p>'),
   'HTML', 'fullName,departmentName', CURRENT_TIMESTAMP),

  ('DEPT_LEADERSHIP_HANDED_OVER',
   'Bàn giao Department Leader',
   'ACCOUNT', NULL,
   'Thông báo bàn giao quyền Department Leader.',
   'ACTIVE',
   'Quyền Department Leader đã được bàn giao',
   CONCAT(
     '<p>Xin chào {{fullName}},</p>',
     '<p>Quyền lãnh đạo {{departmentName}} đã được bàn giao.</p>'),
   'Department leadership handed over',
   CONCAT(
     '<p>Hello {{fullName}},</p>',
     '<p>Leadership of {{departmentName}} was handed over.</p>'),
   'HTML', 'fullName,departmentName', CURRENT_TIMESTAMP),

-- ── AUTH ────────────────────────────────────────────────────────────────────
  ('AUTH_PASSWORD_RESET_OTP',
   'OTP đặt lại mật khẩu',
   'AUTH', NULL,
   'Mã OTP đặt lại mật khẩu; nội dung không được lưu vào lịch sử.',
   'ACTIVE',
   'Mã OTP đặt lại mật khẩu PEMS',
   CONCAT(
     '<p>Xin chào {{fullName}},</p>',
     '<p>Mã OTP: {{otpCode}}. Mã hết hạn sau {{expireMinutes}} phút.</p>'),
   'PEMS password reset OTP',
   CONCAT(
     '<p>Hello {{fullName}},</p>',
     '<p>OTP: {{otpCode}}. It expires in {{expireMinutes}} minutes.</p>'),
   'HTML', 'fullName,otpCode,expireMinutes', CURRENT_TIMESTAMP),

-- ── VISIT_REQUEST ───────────────────────────────────────────────────────────
  ('VISIT_REQUEST_OTP',
   'OTP gửi yêu cầu tham quan',
   'VISIT_REQUEST', NULL,
   'Mã OTP xác minh email trước khi gửi yêu cầu; nội dung không được lưu vào lịch sử.',
   'ACTIVE',
   'Mã OTP xác minh yêu cầu tham quan',
   CONCAT(
     '<p>Xin chào {{fullName}},</p>',
     '<p>Mã OTP: {{otpCode}}. Mã hết hạn sau {{expireMinutes}} phút.</p>'),
   'Visit request verification OTP',
   CONCAT(
     '<p>Hello {{fullName}},</p>',
     '<p>OTP: {{otpCode}}. It expires in {{expireMinutes}} minutes.</p>'),
   'HTML', 'fullName,otpCode,expireMinutes', CURRENT_TIMESTAMP),

  ('VISIT_CONTACT_CLAIM',
   'Xác nhận đầu mối chính',
   'VISIT_REQUEST', NULL,
   'Mời đầu mối chính nhận quyền quản lý yêu cầu.',
   'ACTIVE',
   'Xác nhận đầu mối cho yêu cầu {{requestCode}}',
   CONCAT(
     '<p>Xin chào {{contactFullName}},</p>',
     '<p>Bạn được mời làm đầu mối cho {{delegationName}}, mã {{requestCode}}.</p>',
     '{{actionBlock}}'),
   'Confirm contact for request {{requestCode}}',
   CONCAT(
     '<p>Hello {{contactFullName}},</p>',
     '<p>You are invited to manage {{delegationName}}, request {{requestCode}}.</p>',
     '{{actionBlock}}'),
   'HTML', 'contactFullName,requestCode,delegationName', CURRENT_TIMESTAMP),

  ('VISIT_CONTACT_TRANSFER',
   'Bàn giao đầu mối chính',
   'VISIT_REQUEST', NULL,
   'Mời đầu mối mới nhận quyền quản lý từ đầu mối hiện tại.',
   'ACTIVE',
   'Bàn giao đầu mối cho yêu cầu {{requestCode}}',
   CONCAT(
     '<p>Xin chào {{contactFullName}},</p>',
     '<p>{{currentContactName}} mời bạn tiếp nhận {{delegationName}}, mã {{requestCode}}.</p>',
     '{{actionBlock}}'),
   'Transfer contact for request {{requestCode}}',
   CONCAT(
     '<p>Hello {{contactFullName}},</p>',
     '<p>{{currentContactName}} invited you to take over {{delegationName}}, request {{requestCode}}.</p>',
     '{{actionBlock}}'),
   'HTML', 'contactFullName,currentContactName,requestCode,delegationName', CURRENT_TIMESTAMP),

-- ── VISIT_PARTICIPANT ───────────────────────────────────────────────────────
  ('VISIT_PARTICIPANT_INVITATION',
   'Mời nhân sự IC tham gia',
   'VISIT_PARTICIPANT', NULL,
   'Host mời nhân sự IC tham gia đoàn.',
   'ACTIVE',
   'Mời tham gia đoàn {{delegationName}}',
   CONCAT(
     '<p>Xin chào {{recipientName}},</p>',
     '<p>Bạn được mời tham gia {{delegationName}} tại {{campusName}}, lúc {{plannedTime}}. Host: {{hostName}}; vai trò: {{roleLabel}}; lời nhắn: {{hostMessage}}.</p>',
     '{{actionBlock}}'),
   'Invitation to join {{delegationName}}',
   CONCAT(
     '<p>Hello {{recipientName}},</p>',
     '<p>You are invited to {{delegationName}} at {{campusName}}, {{plannedTime}}. Host: {{hostName}}; role: {{roleLabel}}; note: {{hostMessage}}.</p>',
     '{{actionBlock}}'),
   'HTML', 'recipientName,delegationName,campusName,plannedTime,hostName,roleLabel,hostMessage', CURRENT_TIMESTAMP),

  ('VISIT_STUDENT_INVITATION',
   'Mời sinh viên tham gia',
   'VISIT_PARTICIPANT', NULL,
   'Host mời sinh viên hỗ trợ đoàn.',
   'ACTIVE',
   'Mời sinh viên hỗ trợ {{delegationName}}',
   CONCAT(
     '<p>Xin chào {{recipientName}},</p>',
     '<p>Bạn được mời hỗ trợ {{delegationName}} tại {{campusName}}, lúc {{plannedTime}}. Host: {{hostName}}; vai trò: {{roleLabel}}; lời nhắn: {{hostMessage}}.</p>',
     '{{actionBlock}}'),
   'Student invitation for {{delegationName}}',
   CONCAT(
     '<p>Hello {{recipientName}},</p>',
     '<p>You are invited to support {{delegationName}} at {{campusName}}, {{plannedTime}}. Host: {{hostName}}; role: {{roleLabel}}; note: {{hostMessage}}.</p>',
     '{{actionBlock}}'),
   'HTML', 'recipientName,delegationName,campusName,plannedTime,hostName,roleLabel,hostMessage', CURRENT_TIMESTAMP),

  ('VISIT_DEPARTMENT_LEADER_INVITATION',
   'Mời Department Leader tham gia',
   'VISIT_PARTICIPANT', NULL,
   'Host mời Department Leader phối hợp.',
   'ACTIVE',
   'Mời phòng ban phối hợp {{delegationName}}',
   CONCAT(
     '<p>Xin chào {{recipientName}},</p>',
     '<p>Bạn được mời phối hợp {{delegationName}} tại {{campusName}}, lúc {{plannedTime}}. Host: {{hostName}}; vai trò: {{roleLabel}}; lời nhắn: {{hostMessage}}.</p>',
     '{{actionBlock}}'),
   'Department invitation for {{delegationName}}',
   CONCAT(
     '<p>Hello {{recipientName}},</p>',
     '<p>You are invited to coordinate {{delegationName}} at {{campusName}}, {{plannedTime}}. Host: {{hostName}}; role: {{roleLabel}}; note: {{hostMessage}}.</p>',
     '{{actionBlock}}'),
   'HTML', 'recipientName,delegationName,campusName,plannedTime,hostName,roleLabel,hostMessage', CURRENT_TIMESTAMP),

  ('VISIT_DEPARTMENT_STAFF_ASSIGNMENT',
   'Phân công Department Staff',
   'VISIT_PARTICIPANT', NULL,
   'Department Leader phân công nhân sự cùng phòng ban.',
   'ACTIVE',
   'Phân công hỗ trợ đoàn {{delegationName}}',
   CONCAT(
     '<p>Xin chào {{recipientName}},</p>',
     '<p>Bạn được phân công hỗ trợ {{delegationName}} tại {{campusName}}, lúc {{plannedTime}}, cho {{departmentName}}.</p>'),
   'Assignment for {{delegationName}}',
   CONCAT(
     '<p>Hello {{recipientName}},</p>',
     '<p>You were assigned to support {{delegationName}} at {{campusName}}, {{plannedTime}}, for {{departmentName}}.</p>'),
   'HTML', 'recipientName,delegationName,campusName,plannedTime,departmentName', CURRENT_TIMESTAMP),

-- ── VISIT_REMINDER ──────────────────────────────────────────────────────────
  ('VISIT_REMINDER_HOST',
   'Nhắc lịch cho host',
   'VISIT_REMINDER', NULL,
   'Nhắc host trước lịch tiếp đoàn.',
   'ACTIVE',
   'Nhắc lịch tiếp đoàn {{delegationName}}',
   CONCAT(
     '<p>Xin chào {{hostName}},</p>',
     '<p>{{delegationName}} tại {{campusName}} diễn ra từ {{plannedStart}} đến {{plannedEnd}}.</p>'),
   'Visit reminder: {{delegationName}}',
   CONCAT(
     '<p>Hello {{hostName}},</p>',
     '<p>{{delegationName}} at {{campusName}} runs from {{plannedStart}} to {{plannedEnd}}.</p>'),
   'HTML', 'hostName,delegationName,campusName,plannedStart,plannedEnd', CURRENT_TIMESTAMP),

  ('VISIT_REMINDER_PARTICIPANTS',
   'Nhắc lịch cho người tham gia',
   'VISIT_REMINDER', NULL,
   'Nhắc người tham gia đã nhận lời.',
   'ACTIVE',
   'Nhắc lịch tham gia {{delegationName}}',
   CONCAT(
     '<p>Xin chào {{recipientName}},</p>',
     '<p>{{delegationName}} tại {{campusName}} diễn ra từ {{plannedStart}} đến {{plannedEnd}}.</p>'),
   'Participation reminder: {{delegationName}}',
   CONCAT(
     '<p>Hello {{recipientName}},</p>',
     '<p>{{delegationName}} at {{campusName}} runs from {{plannedStart}} to {{plannedEnd}}.</p>'),
   'HTML', 'recipientName,delegationName,campusName,plannedStart,plannedEnd', CURRENT_TIMESTAMP),

-- ── LOGISTICS ───────────────────────────────────────────────────────────────
  ('LOGISTICS_REQUEST_TO_DEPARTMENT',
   'Yêu cầu hậu cần tới phòng ban',
   'LOGISTICS', NULL,
   'Host gửi yêu cầu hậu cần cho Department Leader.',
   'ACTIVE',
   'Yêu cầu hậu cần mới: {{logisticsTitle}}',
   CONCAT(
     '<p>Xin chào {{departmentLeaderName}},</p>',
     '<p>{{requesterName}} yêu cầu {{logisticsTitle}} ({{logisticsItemType}}), số lượng {{quantity}}, từ {{usageStartAt}} đến {{usageEndAt}}, hạn {{dueAt}}. Ghi chú: {{coordinationNote}}.</p>',
     '{{actionBlock}}'),
   'New logistics request: {{logisticsTitle}}',
   CONCAT(
     '<p>Hello {{departmentLeaderName}},</p>',
     '<p>{{requesterName}} requested {{logisticsTitle}} ({{logisticsItemType}}), quantity {{quantity}}, from {{usageStartAt}} to {{usageEndAt}}, due {{dueAt}}. Note: {{coordinationNote}}.</p>',
     '{{actionBlock}}'),
   'HTML', 'departmentLeaderName,requesterName,logisticsTitle,logisticsItemType,quantity,usageStartAt,usageEndAt,dueAt,coordinationNote', CURRENT_TIMESTAMP),

  ('LOGISTICS_ASSIGNEE_ASSIGNMENT',
   'Phân công xử lý hậu cần',
   'LOGISTICS', NULL,
   'Department Leader phân công Department Staff xử lý hậu cần.',
   'ACTIVE',
   'Bạn được phân công: {{logisticsTitle}}',
   CONCAT(
     '<p>Xin chào {{assigneeName}},</p>',
     '<p>Bạn xử lý {{logisticsTitle}} cho {{delegationName}} tại {{campusName}}, hạn {{dueAt}}.</p>',
     '{{actionBlock}}'),
   'You were assigned: {{logisticsTitle}}',
   CONCAT(
     '<p>Hello {{assigneeName}},</p>',
     '<p>You will handle {{logisticsTitle}} for {{delegationName}} at {{campusName}}, due {{dueAt}}.</p>',
     '{{actionBlock}}'),
   'HTML', 'assigneeName,logisticsTitle,dueAt,campusName,delegationName', CURRENT_TIMESTAMP),

  ('LOGISTICS_CHANGE_PROPOSAL_TO_HOST',
   'Đề xuất đổi hậu cần tới host',
   'LOGISTICS', NULL,
   'Department Staff gửi đề xuất thay đổi cho current host.',
   'ACTIVE',
   'Đề xuất thay đổi: {{logisticsTitle}}',
   CONCAT(
     '<p>Xin chào {{hostName}},</p>',
     '<p>{{departmentName}} đề xuất đổi {{logisticsTitle}} cho {{delegationName}} từ {{originalQuantity}} thành {{proposedQuantity}}, thời gian {{proposedUsageStartAt}}–{{proposedUsageEndAt}}. Nội dung: {{proposedDescription}}. Ghi chú: {{proposalNote}}.</p>',
     '{{actionBlock}}'),
   'Change proposal: {{logisticsTitle}}',
   CONCAT(
     '<p>Hello {{hostName}},</p>',
     '<p>{{departmentName}} proposed changing {{logisticsTitle}} for {{delegationName}} from {{originalQuantity}} to {{proposedQuantity}}, time {{proposedUsageStartAt}}–{{proposedUsageEndAt}}. Detail: {{proposedDescription}}. Note: {{proposalNote}}.</p>',
     '{{actionBlock}}'),
   'HTML', 'hostName,logisticsTitle,departmentName,delegationName,originalQuantity,proposedQuantity,proposedUsageStartAt,proposedUsageEndAt,proposedDescription,proposalNote', CURRENT_TIMESTAMP),

  ('LOGISTICS_EXPENSE_REPORT_REMINDER',
   'Nhắc nộp báo cáo chi phí',
   'LOGISTICS', NULL,
   'Nhắc người phụ trách nộp báo cáo chi phí cho hạng mục hoàn tất.',
   'ACTIVE',
   'Nhắc báo cáo chi phí: {{itemTitle}}',
   CONCAT(
     '<p>Xin chào {{recipientName}},</p>',
     '<p>Hãy hoàn tất báo cáo chi phí cho {{itemTitle}}, đoàn {{delegationName}}, trước {{dueAt}}.</p>'),
   'Expense report reminder: {{itemTitle}}',
   CONCAT(
     '<p>Hello {{recipientName}},</p>',
     '<p>Please complete the expense report for {{itemTitle}}, delegation {{delegationName}}, by {{dueAt}}.</p>'),
   'HTML', 'recipientName,itemTitle,dueAt,delegationName', CURRENT_TIMESTAMP),

-- ── REPORT ──────────────────────────────────────────────────────────────────
  ('REPORT_CAMPUS_OPERATION',
   'Báo cáo vận hành campus',
   'REPORT', NULL,
   'Gửi báo cáo vận hành tiếp khách của một campus kèm tệp PDF đính kèm.',
   'ACTIVE',
   '[PEMS] Báo cáo vận hành campus — {{campusName}} ({{periodFrom}} – {{periodTo}})',
   CONCAT(
     '<p>Xin chào <strong>{{recipientName}}</strong>,</p>',
     '<p>Đính kèm là báo cáo vận hành tiếp khách của <strong>{{campusName}}</strong> cho giai đoạn <strong>{{periodFrom}}</strong> đến <strong>{{periodTo}}</strong>.</p>',
     '<p>Báo cáo tổng hợp số lượng đoàn, tiến độ xử lý yêu cầu và tình hình hậu cần trong kỳ.</p>',
     '<p style="color:#6b7280;font-size:12px">Trân trọng,<br/>PEMS - FPT University</p>'),
   '[PEMS] Campus operations report — {{campusName}} ({{periodFrom}} – {{periodTo}})',
   CONCAT(
     '<p>Hello <strong>{{recipientName}}</strong>,</p>',
     '<p>Attached is the visit operations report for <strong>{{campusName}}</strong> covering <strong>{{periodFrom}}</strong> to <strong>{{periodTo}}</strong>.</p>',
     '<p>It summarises delegation volume, request handling progress and logistics activity for the period.</p>',
     '<p style="color:#6b7280;font-size:12px">Best regards,<br/>PEMS - FPT University</p>'),
   'HTML', 'recipientName,campusName,periodFrom,periodTo', CURRENT_TIMESTAMP),

  ('REPORT_DEPARTMENT_COLLABORATION',
   'Báo cáo phối hợp tiếp khách của phòng ban',
   'REPORT', NULL,
   'Gửi Trưởng phòng ban báo cáo mức độ phối hợp tiếp khách của phòng ban kèm tệp PDF đính kèm.',
   'ACTIVE',
   '[PEMS] Báo cáo phối hợp tiếp khách — {{departmentName}} ({{periodFrom}} – {{periodTo}})',
   CONCAT(
     '<p>Xin chào <strong>{{recipientName}}</strong>,</p>',
     '<p>Đính kèm là báo cáo phối hợp tiếp khách của <strong>{{departmentName}}</strong> cho giai đoạn <strong>{{periodFrom}}</strong> đến <strong>{{periodTo}}</strong>.</p>',
     '<p>Báo cáo ghi nhận các yêu cầu hậu cần phòng ban đã tiếp nhận, thời gian phản hồi và kết quả xử lý.</p>',
     '<p style="color:#6b7280;font-size:12px">Trân trọng,<br/>PEMS - FPT University</p>'),
   '[PEMS] Visit collaboration report — {{departmentName}} ({{periodFrom}} – {{periodTo}})',
   CONCAT(
     '<p>Hello <strong>{{recipientName}}</strong>,</p>',
     '<p>Attached is the visit collaboration report for <strong>{{departmentName}}</strong> covering <strong>{{periodFrom}}</strong> to <strong>{{periodTo}}</strong>.</p>',
     '<p>It records the logistics requests the department accepted, response times and outcomes.</p>',
     '<p style="color:#6b7280;font-size:12px">Best regards,<br/>PEMS - FPT University</p>'),
   'HTML', 'recipientName,departmentName,periodFrom,periodTo', CURRENT_TIMESTAMP),

  ('REPORT_DEPARTMENT_INVOICE',
   'Hóa đơn hậu cần tiếp khách của phòng ban',
   'REPORT', NULL,
   'Gửi hóa đơn hậu cần tiếp khách của phòng ban kèm tệp PDF. Dùng chung cho hai chiều gửi: phòng ban gửi lên và campus gửi xuống.',
   'ACTIVE',
   '[PEMS] Hóa đơn hậu cần tiếp khách — {{departmentName}} ({{periodFrom}} – {{periodTo}})',
   CONCAT(
     '<p>Xin chào <strong>{{recipientName}}</strong>,</p>',
     '<p>Đính kèm là hóa đơn hậu cần tiếp khách của <strong>{{departmentName}}</strong> cho giai đoạn <strong>{{periodFrom}}</strong> đến <strong>{{periodTo}}</strong>.</p>',
     '<p>Vui lòng đối chiếu các hạng mục và chi phí đã kê khai trước khi phê duyệt.</p>',
     '<p style="color:#6b7280;font-size:12px">Trân trọng,<br/>PEMS - FPT University</p>'),
   '[PEMS] Visit logistics invoice — {{departmentName}} ({{periodFrom}} – {{periodTo}})',
   CONCAT(
     '<p>Hello <strong>{{recipientName}}</strong>,</p>',
     '<p>Attached is the visit logistics invoice for <strong>{{departmentName}}</strong> covering <strong>{{periodFrom}}</strong> to <strong>{{periodTo}}</strong>.</p>',
     '<p>Please reconcile the listed items and reported costs before approving.</p>',
     '<p style="color:#6b7280;font-size:12px">Best regards,<br/>PEMS - FPT University</p>'),
   'HTML', 'recipientName,departmentName,periodFrom,periodTo', CURRENT_TIMESTAMP),

  ('REPORT_PERSONNEL_PERFORMANCE',
   'Báo cáo hiệu suất nhân sự tiếp khách',
   'REPORT', NULL,
   'Gửi từng cá nhân báo cáo hiệu suất tham gia tiếp khách kèm tệp PDF. Phạm vi thống kê do người gửi truyền vào qua scopeLabel.',
   'ACTIVE',
   '[PEMS] Báo cáo hiệu suất {{scopeLabel}} — {{personName}} ({{periodFrom}} – {{periodTo}})',
   CONCAT(
     '<p>Xin chào <strong>{{personName}}</strong>,</p>',
     '<p>Đính kèm là báo cáo hiệu suất <strong>{{scopeLabel}}</strong> của bạn cho giai đoạn <strong>{{periodFrom}}</strong> đến <strong>{{periodTo}}</strong>.</p>',
     '<p>Báo cáo ghi nhận các nhiệm vụ bạn đã nhận, tỷ lệ hoàn thành và thời gian phản hồi trong kỳ.</p>',
     '<p style="color:#6b7280;font-size:12px">Trân trọng,<br/>PEMS - FPT University</p>'),
   '[PEMS] Performance report {{scopeLabel}} — {{personName}} ({{periodFrom}} – {{periodTo}})',
   CONCAT(
     '<p>Hello <strong>{{personName}}</strong>,</p>',
     '<p>Attached is your <strong>{{scopeLabel}}</strong> performance report covering <strong>{{periodFrom}}</strong> to <strong>{{periodTo}}</strong>.</p>',
     '<p>It records the assignments you accepted, completion rate and response times for the period.</p>',
     '<p style="color:#6b7280;font-size:12px">Best regards,<br/>PEMS - FPT University</p>'),
   'HTML', 'personName,scopeLabel,periodFrom,periodTo', CURRENT_TIMESTAMP),

  ('VISIT_SETUP_PROGRESS_UPDATE',
   'Cập nhật công tác chuẩn bị tiếp khách',
   'REPORT', NULL,
   'Người phụ trách tiếp đón gửi bản cập nhật công tác chuẩn bị tới khách và thành phần tham gia, kèm Báo cáo Lịch trình. Không mang liên kết dùng một lần.',
   'ACTIVE',
   '[PEMS] Cập nhật công tác chuẩn bị — {{delegationName}} tại {{campusName}}',
   CONCAT(
     '<p>Kính gửi Quý khách,</p>',
     '<p>Đây là cập nhật mới nhất về công tác chuẩn bị cho chuyến thăm của đoàn <strong>{{delegationName}}</strong> tại <strong>{{campusName}}</strong>, dự kiến từ <strong>{{plannedStart}}</strong> đến <strong>{{plannedEnd}}</strong>.</p>',
     '{{setupSummaryBlock}}',
     '<p>Báo cáo Lịch trình chi tiết được đính kèm trong email này.</p>',
     '<p>Nếu Quý khách cần điều chỉnh nội dung nào, vui lòng phản hồi email này để <strong>{{hostName}}</strong> — người phụ trách tiếp đón — kịp thời cập nhật.</p>',
     '<p style="color:#6b7280;font-size:12px">Trân trọng,<br/>PEMS - FPT University</p>'),
   '[PEMS] Preparation update — {{delegationName}} at {{campusName}}',
   CONCAT(
     '<p>Dear Guest,</p>',
     '<p>This is the latest update on preparations for the visit of <strong>{{delegationName}}</strong> to <strong>{{campusName}}</strong>, scheduled from <strong>{{plannedStart}}</strong> to <strong>{{plannedEnd}}</strong>.</p>',
     '{{setupSummaryBlock}}',
     '<p>The detailed Schedule Report is attached to this email.</p>',
     '<p>If anything needs adjusting, please reply to this email so that <strong>{{hostName}}</strong>, the host for this visit, can update it in time.</p>',
     '<p style="color:#6b7280;font-size:12px">Best regards,<br/>PEMS - FPT University</p>'),
   'HTML', 'delegationName,campusName,plannedStart,plannedEnd,hostName', CURRENT_TIMESTAMP);

-- The staged catalog must be exactly what we expect before a single production row is touched.
DELIMITER $$
DROP PROCEDURE IF EXISTS pems_email_stage_check$$
CREATE PROCEDURE pems_email_stage_check()
BEGIN
  IF (SELECT COUNT(*) FROM _pems_canonical_templates) <> 31 THEN
    SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT =
      'Staged canonical catalog does not hold the expected number of templates. Aborting.';
  END IF;
  IF EXISTS (SELECT 1 FROM _pems_canonical_templates
             WHERE subject_vi IS NULL OR subject_vi = '' OR body_vi IS NULL OR body_vi = ''
                OR subject_en IS NULL OR subject_en = '' OR body_en IS NULL OR body_en = '') THEN
    SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT =
      'Staged catalog has a template missing VI or EN content. Aborting.';
  END IF;
END$$
DELIMITER ;
CALL pems_email_stage_check();
DROP PROCEDURE pems_email_stage_check;

-- ── 1. Insert canonical templates the target does not have yet ────────────────────────────────
INSERT INTO email_templates
  (template_code, name, purpose, campus_id, description, status,
   subject_vi, body_vi, subject_en, body_en, body_format, variables_text, created_at)
SELECT c.template_code, c.name, c.purpose, c.campus_id, c.description, c.status,
       c.subject_vi, c.body_vi, c.subject_en, c.body_en, c.body_format, c.variables_text, c.created_at
FROM _pems_canonical_templates c
LEFT JOIN email_templates t ON t.template_code = c.template_code
WHERE t.email_template_id IS NULL;

SELECT ROW_COUNT() AS inserted_templates;

-- ── 2. Update the ones that exist, only where something actually differs ──────────────────────
-- Matched on template_code; email_template_id is never written, so foreign keys survive.
-- <=> is the NULL-safe comparison: without it, a row whose description is NULL on both sides would
-- compare UNKNOWN and be rewritten on every run.
UPDATE email_templates t
JOIN _pems_canonical_templates c ON c.template_code = t.template_code
SET t.name           = c.name,
    t.purpose        = c.purpose,
    t.campus_id      = c.campus_id,
    t.description    = c.description,
    t.status         = c.status,
    t.subject_vi     = c.subject_vi,
    t.body_vi        = c.body_vi,
    t.subject_en     = c.subject_en,
    t.body_en        = c.body_en,
    t.body_format    = c.body_format,
    t.variables_text = c.variables_text
WHERE NOT (t.name           <=> c.name
       AND t.purpose        <=> c.purpose
       AND t.campus_id      <=> c.campus_id
       AND t.description    <=> c.description
       AND t.status         <=> c.status
       AND t.subject_vi     <=> c.subject_vi
       AND t.body_vi        <=> c.body_vi
       AND t.subject_en     <=> c.subject_en
       AND t.body_en        <=> c.body_en
       AND t.body_format    <=> c.body_format
       AND t.variables_text <=> c.variables_text);

SELECT ROW_COUNT() AS updated_templates;

-- ── 3. Retire the legacy codes, without deleting them ─────────────────────────────────────────
-- Named one by one on purpose. "Everything not in the canonical set" would also catch templates an
-- operator authored, and there is no column that tells the two apart after the fact.
UPDATE email_templates
SET status = 'INACTIVE'
WHERE status <> 'INACTIVE'
  AND template_code IN ('ACCOUNT_CREATED_INTERNAL', 'VISIT_REQUEST_APPROVED', 'VISIT_REQUEST_REJECTED', 'VISIT_CANCELLED', 'HOST_ASSIGNMENT', 'VISIT_REQUEST_SUBMITTED_NOTIFY', 'LOGISTICS_REQUEST', 'LOGISTICS_REQUEST_SUBMITTED_NOTIFY', 'OTP_VISIT_REQUEST');

SELECT ROW_COUNT() AS deactivated_legacy_templates;

DROP TEMPORARY TABLE IF EXISTS _pems_canonical_templates;

COMMIT;

SELECT 'sync complete' AS result, DATABASE() AS db, NOW() AS finished_at;

-- Spend the confirmation. The next run on this session must name its target again.
SET @pems_sync_confirm_database = NULL;
