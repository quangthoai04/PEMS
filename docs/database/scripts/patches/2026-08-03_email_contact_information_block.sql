-- ===========================================================================
-- PEMS — Khối thông tin liên hệ trong email (patch cho database đã tồn tại)
--
-- Đưa một database cũ về đúng trạng thái mà PEMS_FULL_VS_31_07_NEW.sql tạo ra
-- khi import mới. Gồm ba việc:
--
--   1. Tạo bảng email_contact_policies (nếu chưa có).
--   2. Nạp chính sách mặc định cho 31 template + 1 dòng SYSTEM.
--   3. Chèn {{contactInformationBlock}} vào body của 14 template mà nội dung có
--      câu bảo người nhận đi liên hệ.
--
-- CHẠY LẠI ĐƯỢC. Lần chạy thứ hai không đổi revision, không đổi updated_at, và
-- không đổi hash của bất kỳ body nào — mọi bước đều có điều kiện.
--
-- KHÔNG GHI ĐÈ NỘI DUNG NGƯỜI DÙNG ĐÃ SỬA. Bước 3 chỉ đụng vào những body còn
-- khớp CHÍNH XÁC với bản canonical trước đó (nhận dạng qua đoạn ký tự đóng thư).
-- Một template đã được biên tập lại sẽ bị BỎ QUA và liệt kê trong phần verdict
-- để người vận hành tự thêm khối vào — vì không có cách nào an toàn để đoán chỗ
-- đặt khối trong một đoạn văn mà mình không viết.
--
-- HỆ QUẢ NẾU KHÔNG CHẠY: 14 template đó sẽ từ chối gửi với mã
-- EMAIL_TEMPLATE_REQUIRED_CONTACT_BLOCK_NOT_IN_BODY. Đây là fail-closed có chủ
-- ý — thà chặn còn hơn gửi ra một câu "vui lòng liên hệ" không kèm địa chỉ nào.
--
-- ROLLBACK: xem cuối file.
-- ===========================================================================

-- ── Preflight ──────────────────────────────────────────────────────────────
SELECT '=== PREFLIGHT ===' AS step;

SELECT
  (SELECT COUNT(*) FROM information_schema.tables
    WHERE table_schema = DATABASE() AND table_name = 'email_contact_policies')  AS policy_table_exists,
  (SELECT COUNT(*) FROM email_templates)                                        AS templates_total,
  (SELECT COUNT(*) FROM email_templates
    WHERE body_vi LIKE '%{{contactInformationBlock}}%')                         AS bodies_vi_with_block_before,
  (SELECT COUNT(*) FROM email_templates
    WHERE body_en LIKE '%{{contactInformationBlock}}%')                         AS bodies_en_with_block_before;

-- Hash trước, để đối chiếu sau khi chạy và giữa hai lần chạy.
SELECT 'BEFORE' AS phase,
       MD5(GROUP_CONCAT(template_code, ':', MD5(body_vi), ':', MD5(body_en)
                        ORDER BY template_code SEPARATOR '|')) AS bodies_digest
FROM email_templates;


-- ── 1. Bảng chính sách ─────────────────────────────────────────────────────
-- Định nghĩa phải trùng từng chữ với canonical; xem chú thích thiết kế ở đó.
CREATE TABLE IF NOT EXISTS email_contact_policies (
  email_contact_policy_id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,

  scope_type ENUM('TEMPLATE','CAMPUS','DEPARTMENT','SYSTEM') NOT NULL
    COMMENT 'Cấp trong chuỗi kế thừa',
  scope_key VARCHAR(64) NULL
    COMMENT 'template_code / campus_id / department_id dạng chuỗi; NULL cho dòng SYSTEM duy nhất',

  requirement ENUM('NONE','OPTIONAL','REQUIRED') NULL
    COMMENT 'NONE=không hiển thị; OPTIONAL=hiện nếu tìm được; REQUIRED=hiện, không tìm được thì chặn gửi',
  contact_source ENUM('HOST','SENDER','HOST_THEN_SENDER',
                      'CAMPUS_DEFAULT','DEPARTMENT_DEFAULT','SUPPORT_CONTACT') NULL
    COMMENT 'Nguồn tra cứu đầu mối. Là enum chứ không phải user_id: đầu mối đúng phụ thuộc chuyến thăm và cơ sở',

  show_email      TINYINT(1) NULL COMMENT 'NULL = kế thừa cấp dưới',
  show_phone      TINYINT(1) NULL COMMENT 'NULL = kế thừa cấp dưới',
  show_department TINYINT(1) NULL COMMENT 'NULL = kế thừa cấp dưới',
  show_campus     TINYINT(1) NULL COMMENT 'NULL = kế thừa cấp dưới',
  show_sender     TINYINT(1) NULL COMMENT 'Dòng "Được gửi bởi"; NULL = kế thừa cấp dưới',

  heading_vi VARCHAR(150) NULL,
  heading_en VARCHAR(150) NULL,

  reply_to_source ENUM('NONE','CONTACT','SENDER') NULL
    COMMENT 'Địa chỉ đặt vào header Reply-To của thư',

  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  created_by BIGINT UNSIGNED NULL,
  updated_at DATETIME NULL DEFAULT NULL ON UPDATE CURRENT_TIMESTAMP,
  updated_by BIGINT UNSIGNED NULL,

  PRIMARY KEY (email_contact_policy_id),
  UNIQUE KEY uq_email_contact_policies_scope (scope_type, scope_key),

  CONSTRAINT fk_email_contact_policies_created_by
    FOREIGN KEY (created_by) REFERENCES users(user_id)
    ON UPDATE CASCADE ON DELETE SET NULL,
  CONSTRAINT fk_email_contact_policies_updated_by
    FOREIGN KEY (updated_by) REFERENCES users(user_id)
    ON UPDATE CASCADE ON DELETE SET NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
COMMENT='Chính sách hiển thị khối thông tin liên hệ trong email. Chỉ lưu chính sách, không lưu dữ liệu liên hệ.';


-- ── 2. Chính sách mặc định ─────────────────────────────────────────────────
-- INSERT IGNORE, KHÔNG phải ON DUPLICATE KEY UPDATE: một dòng đã tồn tại có thể
-- là quản trị viên đã chỉnh, và một patch nạp lại mặc định sẽ lặng lẽ xoá lựa
-- chọn đó. Lần chạy thứ hai vì vậy không đụng dòng nào.
INSERT IGNORE INTO email_contact_policies
  (scope_type, scope_key, requirement, contact_source,
   show_email, show_phone, show_department, show_campus, show_sender,
   heading_vi, heading_en, reply_to_source,
   created_at, created_by, updated_at, updated_by)
VALUES
  ('TEMPLATE', 'ACCOUNT_EMAIL_CONFIRMATION', 'NONE', NULL, 1, 1, 0, 0, 0, NULL, NULL, 'NONE', CURRENT_TIMESTAMP, NULL, NULL, NULL),
  ('TEMPLATE', 'AUTH_PASSWORD_RESET_OTP', 'NONE', NULL, 1, 1, 0, 0, 0, NULL, NULL, 'NONE', CURRENT_TIMESTAMP, NULL, NULL, NULL),
  ('TEMPLATE', 'VISIT_REQUEST_OTP', 'NONE', NULL, 1, 1, 0, 0, 0, NULL, NULL, 'NONE', CURRENT_TIMESTAMP, NULL, NULL, NULL),
  ('TEMPLATE', 'VISIT_REMINDER_HOST', 'NONE', NULL, 1, 1, 0, 0, 0, NULL, NULL, 'NONE', CURRENT_TIMESTAMP, NULL, NULL, NULL),
  ('TEMPLATE', 'ACCOUNT_PENDING_EMAIL_CHANGED_OLD_NOTICE', 'REQUIRED', 'SUPPORT_CONTACT', 1, 1, 0, 0, 0, NULL, NULL, 'NONE', CURRENT_TIMESTAMP, NULL, NULL, NULL),
  ('TEMPLATE', 'ACCOUNT_EMAIL_CHANGED_OLD_NOTICE', 'REQUIRED', 'SUPPORT_CONTACT', 1, 1, 0, 0, 0, NULL, NULL, 'NONE', CURRENT_TIMESTAMP, NULL, NULL, NULL),
  ('TEMPLATE', 'ACCOUNT_ACTIVATED', 'REQUIRED', 'CAMPUS_DEFAULT', 1, 1, 0, 1, 0, NULL, NULL, 'NONE', CURRENT_TIMESTAMP, NULL, NULL, NULL),
  ('TEMPLATE', 'ACCOUNT_EMAIL_CHANGED_NEW_NOTICE', 'REQUIRED', 'CAMPUS_DEFAULT', 1, 1, 0, 1, 0, NULL, NULL, 'NONE', CURRENT_TIMESTAMP, NULL, NULL, NULL),
  ('TEMPLATE', 'ACCOUNT_ROLE_CHANGED', 'OPTIONAL', 'CAMPUS_DEFAULT', 1, 1, 0, 1, 0, NULL, NULL, 'NONE', CURRENT_TIMESTAMP, NULL, NULL, NULL),
  ('TEMPLATE', 'ACCOUNT_STAFF_LEADER_ASSIGNED', 'OPTIONAL', 'CAMPUS_DEFAULT', 1, 1, 0, 1, 0, NULL, NULL, 'NONE', CURRENT_TIMESTAMP, NULL, NULL, NULL),
  ('TEMPLATE', 'ACCOUNT_STAFF_LEADER_REPLACED', 'REQUIRED', 'SUPPORT_CONTACT', 1, 1, 0, 0, 0, NULL, NULL, 'NONE', CURRENT_TIMESTAMP, NULL, NULL, NULL),
  ('TEMPLATE', 'DEPT_PERSONNEL_ACCOUNT_DISABLED', 'REQUIRED', 'DEPARTMENT_DEFAULT', 1, 1, 1, 0, 0, NULL, NULL, 'NONE', CURRENT_TIMESTAMP, NULL, NULL, NULL),
  ('TEMPLATE', 'DEPT_PERSONNEL_ACCOUNT_ENABLED', 'OPTIONAL', 'DEPARTMENT_DEFAULT', 1, 1, 1, 0, 0, NULL, NULL, 'NONE', CURRENT_TIMESTAMP, NULL, NULL, NULL),
  ('TEMPLATE', 'DEPT_LEADERSHIP_GRANTED', 'OPTIONAL', 'DEPARTMENT_DEFAULT', 1, 1, 1, 0, 0, NULL, NULL, 'NONE', CURRENT_TIMESTAMP, NULL, NULL, NULL),
  ('TEMPLATE', 'DEPT_LEADERSHIP_HANDED_OVER', 'OPTIONAL', 'DEPARTMENT_DEFAULT', 1, 1, 1, 0, 0, NULL, NULL, 'NONE', CURRENT_TIMESTAMP, NULL, NULL, NULL),
  ('TEMPLATE', 'VISIT_CONTACT_CLAIM', 'OPTIONAL', 'CAMPUS_DEFAULT', 1, 1, 0, 1, 0, NULL, NULL, 'NONE', CURRENT_TIMESTAMP, NULL, NULL, NULL),
  ('TEMPLATE', 'VISIT_CONTACT_TRANSFER', 'OPTIONAL', 'CAMPUS_DEFAULT', 1, 1, 0, 1, 0, NULL, NULL, 'NONE', CURRENT_TIMESTAMP, NULL, NULL, NULL),
  ('TEMPLATE', 'VISIT_PARTICIPANT_INVITATION', 'REQUIRED', 'HOST', 1, 1, 0, 1, 0, NULL, NULL, 'CONTACT', CURRENT_TIMESTAMP, NULL, NULL, NULL),
  ('TEMPLATE', 'VISIT_STUDENT_INVITATION', 'REQUIRED', 'HOST', 1, 1, 0, 1, 0, NULL, NULL, 'CONTACT', CURRENT_TIMESTAMP, NULL, NULL, NULL),
  ('TEMPLATE', 'VISIT_DEPARTMENT_LEADER_INVITATION', 'REQUIRED', 'HOST', 1, 1, 0, 1, 0, NULL, NULL, 'CONTACT', CURRENT_TIMESTAMP, NULL, NULL, NULL),
  ('TEMPLATE', 'VISIT_DEPARTMENT_STAFF_ASSIGNMENT', 'REQUIRED', 'HOST', 1, 1, 0, 1, 0, NULL, NULL, 'CONTACT', CURRENT_TIMESTAMP, NULL, NULL, NULL),
  ('TEMPLATE', 'VISIT_REMINDER_PARTICIPANTS', 'REQUIRED', 'HOST', 1, 1, 0, 1, 0, NULL, NULL, 'CONTACT', CURRENT_TIMESTAMP, NULL, NULL, NULL),
  ('TEMPLATE', 'LOGISTICS_REQUEST_TO_DEPARTMENT', 'REQUIRED', 'HOST_THEN_SENDER', 1, 1, 0, 1, 0, NULL, NULL, 'CONTACT', CURRENT_TIMESTAMP, NULL, NULL, NULL),
  ('TEMPLATE', 'LOGISTICS_ASSIGNEE_ASSIGNMENT', 'OPTIONAL', 'DEPARTMENT_DEFAULT', 1, 1, 1, 0, 0, NULL, NULL, 'NONE', CURRENT_TIMESTAMP, NULL, NULL, NULL),
  ('TEMPLATE', 'LOGISTICS_CHANGE_PROPOSAL_TO_HOST', 'REQUIRED', 'DEPARTMENT_DEFAULT', 1, 1, 1, 0, 0, NULL, NULL, 'CONTACT', CURRENT_TIMESTAMP, NULL, NULL, NULL),
  ('TEMPLATE', 'LOGISTICS_EXPENSE_REPORT_REMINDER', 'OPTIONAL', 'SENDER', 1, 1, 0, 0, 1, NULL, NULL, 'NONE', CURRENT_TIMESTAMP, NULL, NULL, NULL),
  ('TEMPLATE', 'VISIT_SETUP_PROGRESS_UPDATE', 'REQUIRED', 'HOST', 1, 1, 0, 1, 0, NULL, NULL, 'CONTACT', CURRENT_TIMESTAMP, NULL, NULL, NULL),
  ('TEMPLATE', 'REPORT_CAMPUS_OPERATION', 'OPTIONAL', 'SENDER', 1, 1, 0, 0, 1, NULL, NULL, 'NONE', CURRENT_TIMESTAMP, NULL, NULL, NULL),
  ('TEMPLATE', 'REPORT_DEPARTMENT_COLLABORATION', 'OPTIONAL', 'SENDER', 1, 1, 0, 0, 1, NULL, NULL, 'NONE', CURRENT_TIMESTAMP, NULL, NULL, NULL),
  ('TEMPLATE', 'REPORT_DEPARTMENT_INVOICE', 'OPTIONAL', 'SENDER', 1, 1, 0, 0, 1, NULL, NULL, 'NONE', CURRENT_TIMESTAMP, NULL, NULL, NULL),
  ('TEMPLATE', 'REPORT_PERSONNEL_PERFORMANCE', 'OPTIONAL', 'SENDER', 1, 1, 0, 0, 1, NULL, NULL, 'NONE', CURRENT_TIMESTAMP, NULL, NULL, NULL);

INSERT IGNORE INTO email_contact_policies
  (scope_type, scope_key, requirement, contact_source,
   show_email, show_phone, show_department, show_campus, show_sender,
   heading_vi, heading_en, reply_to_source,
   created_at, created_by, updated_at, updated_by)
VALUES
  ('SYSTEM', NULL, NULL, NULL, 1, 1, 0, 0, 0,
   'Thông tin liên hệ', 'Contact information', NULL,
   CURRENT_TIMESTAMP, NULL, NULL, NULL);


-- ── 3. Chèn khối vào body của 14 template REQUIRED ─────────────────────────
-- Ba điều kiện trên mỗi UPDATE, cả ba đều cần thiết:
--   * body chưa có khối          → chạy lần hai không làm gì;
--   * body còn chứa đoạn đóng thư canonical → nhận dạng bản chưa bị sửa;
--   * template_code nằm trong danh sách     → không đụng template ngoài ma trận.
--
-- Chèn NGAY TRƯỚC đoạn đóng thư: đó là vị trí trong bản canonical, nên một
-- database chạy patch và một database import mới cho ra cùng một chuỗi.

SET @sign_vi := '<p style="color:#6b7280;font-size:12px">Trân trọng,';
SET @sign_en := '<p style="color:#6b7280;font-size:12px">Best regards,';
SET @blk     := '{{contactInformationBlock}}';

-- VISIT_SETUP_PROGRESS_UPDATE in địa chỉ Host ngay trong câu văn. Khối thay chỗ
-- đó, nên câu văn phải bỏ phần địa chỉ trước — nếu không người nhận đọc cùng một
-- hộp thư hai lần, lần đầu không kèm vai trò lẫn số điện thoại.
UPDATE email_templates
SET body_vi = REPLACE(body_vi, ' qua địa chỉ <strong>{{hostEmail}}</strong> để được cập nhật kịp thời.', '.'),
    body_en = REPLACE(body_en, ' directly at <strong>{{hostEmail}}</strong>.', '.')
WHERE template_code = 'VISIT_SETUP_PROGRESS_UPDATE'
  AND body_vi NOT LIKE CONCAT('%', @blk, '%');

UPDATE email_templates
SET body_vi = REPLACE(body_vi, @sign_vi, CONCAT(@blk, @sign_vi))
WHERE body_vi NOT LIKE CONCAT('%', @blk, '%')
  AND body_vi LIKE CONCAT('%', @sign_vi, '%')
  AND template_code IN (
    'ACCOUNT_ACTIVATED','ACCOUNT_PENDING_EMAIL_CHANGED_OLD_NOTICE',
    'ACCOUNT_EMAIL_CHANGED_OLD_NOTICE','ACCOUNT_EMAIL_CHANGED_NEW_NOTICE',
    'ACCOUNT_STAFF_LEADER_REPLACED','DEPT_PERSONNEL_ACCOUNT_DISABLED',
    'VISIT_PARTICIPANT_INVITATION','VISIT_STUDENT_INVITATION',
    'VISIT_DEPARTMENT_LEADER_INVITATION','VISIT_DEPARTMENT_STAFF_ASSIGNMENT',
    'VISIT_REMINDER_PARTICIPANTS','LOGISTICS_REQUEST_TO_DEPARTMENT',
    'LOGISTICS_CHANGE_PROPOSAL_TO_HOST','VISIT_SETUP_PROGRESS_UPDATE');

UPDATE email_templates
SET body_en = REPLACE(body_en, @sign_en, CONCAT(@blk, @sign_en))
WHERE body_en NOT LIKE CONCAT('%', @blk, '%')
  AND body_en LIKE CONCAT('%', @sign_en, '%')
  AND template_code IN (
    'ACCOUNT_ACTIVATED','ACCOUNT_PENDING_EMAIL_CHANGED_OLD_NOTICE',
    'ACCOUNT_EMAIL_CHANGED_OLD_NOTICE','ACCOUNT_EMAIL_CHANGED_NEW_NOTICE',
    'ACCOUNT_STAFF_LEADER_REPLACED','DEPT_PERSONNEL_ACCOUNT_DISABLED',
    'VISIT_PARTICIPANT_INVITATION','VISIT_STUDENT_INVITATION',
    'VISIT_DEPARTMENT_LEADER_INVITATION','VISIT_DEPARTMENT_STAFF_ASSIGNMENT',
    'VISIT_REMINDER_PARTICIPANTS','LOGISTICS_REQUEST_TO_DEPARTMENT',
    'LOGISTICS_CHANGE_PROPOSAL_TO_HOST','VISIT_SETUP_PROGRESS_UPDATE');


-- ── Verdict ────────────────────────────────────────────────────────────────
SELECT '=== VERDICT ===' AS step;

SELECT
  (SELECT COUNT(*) FROM email_contact_policies WHERE scope_type='TEMPLATE') AS policy_rows_template,
  (SELECT COUNT(*) FROM email_contact_policies WHERE scope_type='SYSTEM')   AS policy_rows_system,
  (SELECT COUNT(*) FROM email_templates
    WHERE body_vi LIKE '%{{contactInformationBlock}}%')                     AS bodies_vi_with_block_after,
  (SELECT COUNT(*) FROM email_templates
    WHERE body_en LIKE '%{{contactInformationBlock}}%')                     AS bodies_en_with_block_after;

SELECT 'AFTER' AS phase,
       MD5(GROUP_CONCAT(template_code, ':', MD5(body_vi), ':', MD5(body_en)
                        ORDER BY template_code SEPARATOR '|')) AS bodies_digest
FROM email_templates;

-- Kỳ vọng: policy_rows_template = 31, policy_rows_system = 1,
--          bodies_vi_with_block_after = 14, bodies_en_with_block_after = 14.
--
-- Nếu con số body < 14: những template dưới đây đã bị biên tập lại nên patch cố
-- ý không đụng vào. Người vận hành cần tự thêm {{contactInformationBlock}} vào
-- nội dung (hoặc hạ mức bắt buộc trong màn cấu hình) — cho tới lúc đó chúng sẽ
-- từ chối gửi.
SELECT template_code,
       CASE WHEN body_vi NOT LIKE '%{{contactInformationBlock}}%' THEN 'VI thiếu khối' ELSE '' END AS vi_status,
       CASE WHEN body_en NOT LIKE '%{{contactInformationBlock}}%' THEN 'EN thiếu khối' ELSE '' END AS en_status
FROM email_templates
WHERE template_code IN (
    'ACCOUNT_ACTIVATED','ACCOUNT_PENDING_EMAIL_CHANGED_OLD_NOTICE',
    'ACCOUNT_EMAIL_CHANGED_OLD_NOTICE','ACCOUNT_EMAIL_CHANGED_NEW_NOTICE',
    'ACCOUNT_STAFF_LEADER_REPLACED','DEPT_PERSONNEL_ACCOUNT_DISABLED',
    'VISIT_PARTICIPANT_INVITATION','VISIT_STUDENT_INVITATION',
    'VISIT_DEPARTMENT_LEADER_INVITATION','VISIT_DEPARTMENT_STAFF_ASSIGNMENT',
    'VISIT_REMINDER_PARTICIPANTS','LOGISTICS_REQUEST_TO_DEPARTMENT',
    'LOGISTICS_CHANGE_PROPOSAL_TO_HOST','VISIT_SETUP_PROGRESS_UPDATE')
  AND (body_vi NOT LIKE '%{{contactInformationBlock}}%'
       OR body_en NOT LIKE '%{{contactInformationBlock}}%');


-- ===========================================================================
-- ROLLBACK
--
--   UPDATE email_templates
--   SET body_vi = REPLACE(body_vi, '{{contactInformationBlock}}', ''),
--       body_en = REPLACE(body_en, '{{contactInformationBlock}}', '');
--   DROP TABLE email_contact_policies;
--
-- Giới hạn cần biết: câu văn của VISIT_SETUP_PROGRESS_UPDATE ("qua địa chỉ
-- {{hostEmail}} …") KHÔNG được khôi phục bởi lệnh trên — nó đã bị thay bằng dấu
-- chấm. Muốn về đúng bản cũ thì restore body của riêng template đó từ backup,
-- hoặc dùng chức năng "Khôi phục mặc định" trên màn quản lý template sau khi
-- rollback code về bản trước.
-- ===========================================================================
