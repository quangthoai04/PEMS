-- =====================================================================
-- PEMS DEV SCENARIO SEED FOR 8 REQUESTED ACCOUNTS - NEW COPY
-- File: pems_seed_dev_8_accounts_new.sql
-- Target DB: pems_db
-- Target schema: PEMS v4.5 INT AUTO_INCREMENT / v8.2 CANCEL_DELEGATION
--
-- Run order:
--   1) Run your pems_full.sql / schema file first.
--   2) Run canonical RBAC seed if you keep it separate:
--      roles.sql -> permissions.sql -> permission_matrix.sql -> campuses/departments/dev_accounts if any.
--   3) Run this file.
--
-- Notes:
--   - This file is idempotent for its own seed namespace.
--   - It does NOT drop the database.
--   - It upserts the 8 requested accounts and then seeds scenario data around them.
--   - LOCAL_PASSWORD is for DEV/test only. Shared seed password hash matches Admin@123
--     from your existing dev seed baseline.
--   - ADMIN is intentionally not attached to visit/delegation business records.
-- =====================================================================

USE pems_db;
SET NAMES utf8mb4;
SET @OLD_SQL_SAFE_UPDATES = @@SQL_SAFE_UPDATES;
SET SQL_SAFE_UPDATES = 0;
SET @seed_now = NOW();
SET @pwd_hash = '$2a$12$cRpFAxEt9VdUg0orDrPRL.oesxu8ID8WSI2YTsNclVZjRtwi57PFi';

START TRANSACTION;

-- =====================================================================
-- 0. CLEAN ONLY THIS SEED NAMESPACE, IN FK-SAFE ORDER
-- =====================================================================

DELETE vsl
FROM visit_status_logs vsl
LEFT JOIN visit_requests vr ON vr.visit_request_id = vsl.visit_request_id
LEFT JOIN visit_request_campuses vrc ON vrc.visit_instance_id = vsl.visit_instance_id
WHERE vr.request_code IN ('PEMS-VR-MULTI-PENDING','PEMS-VR-SINGLE-APPROVED','PEMS-VR-SINGLE-CLOSED','PEMS-VR-SINGLE-CANCELLED')
   OR vrc.instance_code IN ('PEMS-VI-MULTI-HN-PENDING','PEMS-VI-MULTI-HCM-PENDING','PEMS-VI-SINGLE-HN-APPROVED','PEMS-VI-SINGLE-HN-CLOSED','PEMS-VI-SINGLE-HN-CANCELLED')
   OR vsl.reason LIKE 'PEMS seed:%';

DELETE FROM audit_logs
WHERE request_id LIKE 'seed-pems-8-accounts-%'
   OR (entity_type IN ('VISIT_REQUEST','VISIT_INSTANCE','NEWS','GALLERY','LOGISTICS_ITEM') AND action LIKE 'SEED_%');

DELETE arl
FROM api_request_logs arl
JOIN api_configurations ac ON ac.api_config_id = arl.api_config_id
WHERE ac.api_code IN ('PEMS_DEV_GOOGLE_SSO','PEMS_DEV_EMAIL_GATEWAY')
   OR arl.endpoint LIKE '%/seed/pems/%';

DELETE auq
FROM api_usage_quotas auq
JOIN api_configurations ac ON ac.api_config_id = auq.api_config_id
WHERE ac.api_code IN ('PEMS_DEV_GOOGLE_SSO','PEMS_DEV_EMAIL_GATEWAY');

DELETE FROM api_configurations
WHERE api_code IN ('PEMS_DEV_GOOGLE_SSO','PEMS_DEV_EMAIL_GATEWAY');

DELETE FROM calendar_events
WHERE title LIKE 'PEMS Seed:%'
   OR description LIKE 'PEMS seed:%';

DELETE FROM notifications
WHERE title LIKE 'PEMS Seed:%'
   OR message LIKE 'PEMS seed:%';

DELETE FROM sent_emails
WHERE subject LIKE 'PEMS Seed:%'
   OR related_type = 'PEMS_SEED';

DELETE nsf
FROM news_section_files nsf
JOIN news_content_sections ncs ON ncs.section_id = nsf.section_id
JOIN news_translations nt ON nt.news_translation_id = ncs.news_translation_id
WHERE nt.slug IN ('pems-seed-campus-tour-recap','pems-seed-student-pending-story');

DELETE ncs
FROM news_content_sections ncs
JOIN news_translations nt ON nt.news_translation_id = ncs.news_translation_id
WHERE nt.slug IN ('pems-seed-campus-tour-recap','pems-seed-student-pending-story');

DELETE nt
FROM news_translations nt
WHERE nt.slug IN ('pems-seed-campus-tour-recap','pems-seed-student-pending-story');

DELETE n
FROM news n
LEFT JOIN visit_request_campuses vrc ON vrc.visit_instance_id = n.visit_instance_id
WHERE vrc.instance_code IN ('PEMS-VI-SINGLE-HN-CLOSED','PEMS-VI-SINGLE-HN-APPROVED')
   OR n.created_by IS NULL AND n.submitted_at >= DATE_SUB(@seed_now, INTERVAL 1 DAY);

DELETE ft
FROM photo_face_tags ft
JOIN gallery_images gi ON gi.image_id = ft.image_id
JOIN files f ON f.file_id = gi.file_id
WHERE f.object_key LIKE 'seed/pems/8-accounts/%';

DELETE gi
FROM gallery_images gi
JOIN files f ON f.file_id = gi.file_id
WHERE f.object_key LIKE 'seed/pems/8-accounts/%';

DELETE FROM galleries
WHERE title LIKE 'PEMS Seed:%'
   OR location_name LIKE 'PEMS Seed:%';

DELETE FROM faqs
WHERE category = 'PEMS Seed';

DELETE mai
FROM minute_action_items mai
JOIN minutes m ON m.minutes_id = mai.minutes_id
JOIN visit_request_campuses vrc ON vrc.visit_instance_id = m.visit_instance_id
WHERE vrc.instance_code IN ('PEMS-VI-SINGLE-HN-CLOSED','PEMS-VI-SINGLE-HN-APPROVED');

DELETE m
FROM minutes m
JOIN visit_request_campuses vrc ON vrc.visit_instance_id = m.visit_instance_id
WHERE vrc.instance_code IN ('PEMS-VI-SINGLE-HN-CLOSED','PEMS-VI-SINGLE-HN-APPROVED');

DELETE fb
FROM feedbacks fb
JOIN visit_requests vr ON vr.visit_request_id = fb.visit_request_id
WHERE vr.request_code IN ('PEMS-VR-SINGLE-CLOSED','PEMS-VR-SINGLE-APPROVED');

DELETE vli
FROM visit_logistics_items vli
JOIN visit_request_campuses vrc ON vrc.visit_instance_id = vli.visit_instance_id
WHERE vrc.instance_code IN ('PEMS-VI-SINGLE-HN-APPROVED','PEMS-VI-SINGLE-HN-CLOSED');

DELETE va
FROM visit_agendas va
JOIN visit_request_campuses vrc ON vrc.visit_instance_id = va.visit_instance_id
WHERE vrc.instance_code IN ('PEMS-VI-SINGLE-HN-APPROVED','PEMS-VI-SINGLE-HN-CLOSED');

DELETE vp
FROM visit_participants vp
JOIN visit_request_campuses vrc ON vrc.visit_instance_id = vp.visit_instance_id
WHERE vrc.instance_code IN ('PEMS-VI-SINGLE-HN-APPROVED','PEMS-VI-SINGLE-HN-CLOSED');

DELETE vgm
FROM visit_guest_members vgm
JOIN visit_requests vr ON vr.visit_request_id = vgm.visit_request_id
WHERE vr.request_code IN ('PEMS-VR-MULTI-PENDING','PEMS-VR-SINGLE-APPROVED','PEMS-VR-SINGLE-CLOSED','PEMS-VR-SINGLE-CANCELLED');

DELETE vrc
FROM visit_request_campuses vrc
JOIN visit_requests vr ON vr.visit_request_id = vrc.visit_request_id
WHERE vr.request_code IN ('PEMS-VR-MULTI-PENDING','PEMS-VR-SINGLE-APPROVED','PEMS-VR-SINGLE-CLOSED','PEMS-VR-SINGLE-CANCELLED');

DELETE FROM visit_requests
WHERE request_code IN ('PEMS-VR-MULTI-PENDING','PEMS-VR-SINGLE-APPROVED','PEMS-VR-SINGLE-CLOSED','PEMS-VR-SINGLE-CANCELLED');

DELETE FROM documents
WHERE title LIKE 'PEMS Seed:%';

DELETE pc
FROM partner_contacts pc
JOIN partners p ON p.partner_id = pc.partner_id
WHERE p.partner_code IN ('PEMS-SEED-KNU','PEMS-SEED-GTC');

DELETE FROM partners
WHERE partner_code IN ('PEMS-SEED-KNU','PEMS-SEED-GTC');

DELETE FROM files
WHERE object_key LIKE 'seed/pems/8-accounts/%';

DELETE FROM user_sessions
WHERE refresh_token_hash LIKE 'seed-refresh-pems-8-%';

DELETE FROM login_logs
WHERE user_agent = 'PEMS seed data';

DELETE FROM otp_tokens
WHERE token_hash LIKE 'seed-token-pems-8-%';

DELETE FROM security_events
WHERE event_type LIKE 'PEMS_SEED_%';

DELETE FROM agenda_templates
WHERE name LIKE 'PEMS Seed:%';

-- =====================================================================
-- 1. BASELINE ROLES, CAMPUSES, DEPARTMENTS
-- =====================================================================

INSERT INTO roles (role_code, name, description, status, created_at, deleted_at, deleted_by)
VALUES
  ('ADMIN',   'Admin',       'Quản trị kỹ thuật hệ thống', 'ACTIVE', @seed_now, NULL, NULL),
  ('HO',      'Head Office', 'Quản lý cấp Head Office', 'ACTIVE', @seed_now, NULL, NULL),
  ('STAFF',   'IC Staff',    'Nhân sự phòng Hợp tác Quốc tế; phân biệt Leader/Staff bằng users.sub_role', 'ACTIVE', @seed_now, NULL, NULL),
  ('DEPT',    'Department',  'Nhân sự phòng ban khác; phân biệt Leader/Staff bằng users.sub_role', 'ACTIVE', @seed_now, NULL, NULL),
  ('STUDENT', 'Student',     'Sinh viên hỗ trợ', 'ACTIVE', @seed_now, NULL, NULL),
  ('VISITOR', 'Visitor',     'Khách gửi và theo dõi visit request', 'ACTIVE', @seed_now, NULL, NULL)
ON DUPLICATE KEY UPDATE
  name = VALUES(name),
  description = VALUES(description),
  status = 'ACTIVE',
  deleted_at = NULL,
  deleted_by = NULL;

SELECT role_id INTO @role_admin   FROM roles WHERE role_code = 'ADMIN'   AND deleted_at IS NULL LIMIT 1;
SELECT role_id INTO @role_ho      FROM roles WHERE role_code = 'HO'      AND deleted_at IS NULL LIMIT 1;
SELECT role_id INTO @role_staff   FROM roles WHERE role_code = 'STAFF'   AND deleted_at IS NULL LIMIT 1;
SELECT role_id INTO @role_dept    FROM roles WHERE role_code = 'DEPT'    AND deleted_at IS NULL LIMIT 1;
SELECT role_id INTO @role_student FROM roles WHERE role_code = 'STUDENT' AND deleted_at IS NULL LIMIT 1;
SELECT role_id INTO @role_visitor FROM roles WHERE role_code = 'VISITOR' AND deleted_at IS NULL LIMIT 1;

INSERT INTO campuses (campus_code, name, city, address, phone, email, status, created_at)
VALUES
  ('HN',  'FPT University Hà Nội',          'Hà Nội',           'Khu Giáo dục và Đào tạo, Khu Công nghệ cao Hòa Lạc, Thạch Thất, Hà Nội', '02473001866', 'ic.hn@fpt.edu.vn',  'ACTIVE', @seed_now),
  ('HCM', 'FPT University TP. Hồ Chí Minh', 'TP. Hồ Chí Minh',  'Lô E2a-7, Đường D1, Khu Công nghệ cao, TP. Thủ Đức',                     '02873005588', 'ic.hcm@fpt.edu.vn', 'ACTIVE', @seed_now)
ON DUPLICATE KEY UPDATE
  name = VALUES(name),
  city = VALUES(city),
  address = VALUES(address),
  phone = VALUES(phone),
  email = VALUES(email),
  status = 'ACTIVE';

SELECT campus_id INTO @campus_hn  FROM campuses WHERE campus_code = 'HN'  LIMIT 1;
SELECT campus_id INTO @campus_hcm FROM campuses WHERE campus_code = 'HCM' LIMIT 1;

INSERT INTO departments (campus_id, department_code, name, department_type, status, created_at)
VALUES
  (@campus_hn,  'IC',       'International Cooperation', 'IC',      'ACTIVE', @seed_now),
  (@campus_hn,  'ACADEMIC', 'Academic Department',       'GENERAL', 'ACTIVE', @seed_now),
  (@campus_hcm, 'IC',       'International Cooperation', 'IC',      'ACTIVE', @seed_now)
ON DUPLICATE KEY UPDATE
  name = VALUES(name),
  department_type = VALUES(department_type),
  status = 'ACTIVE';

SELECT department_id INTO @dept_hn_ic       FROM departments WHERE campus_id = @campus_hn  AND department_code = 'IC'       LIMIT 1;
SELECT department_id INTO @dept_hn_academic FROM departments WHERE campus_id = @campus_hn  AND department_code = 'ACADEMIC' LIMIT 1;
SELECT department_id INTO @dept_hcm_ic      FROM departments WHERE campus_id = @campus_hcm AND department_code = 'IC'       LIMIT 1;

-- =====================================================================
-- 2. REQUESTED DEV ACCOUNTS
-- =====================================================================

INSERT INTO users
  (full_name, email, phone, nationality, password_hash, role_id, sub_role, primary_campus_id, department_id,
   gender, student_code, fe_id, status, email_verified_at, failed_login_count, locked_until, created_via,
   first_login_at, last_login_at, created_at, created_by, updated_at, updated_by)
VALUES
  ('System Administrator',        'admin@fpt.edu.vn',            '0900000001', NULL,       @pwd_hash, @role_admin,   NULL,     @campus_hn, NULL,              'UNKNOWN', NULL,       'PEMS-DEV-ADMIN',       'ACTIVE', @seed_now, 0, NULL, 'MANUAL_CREATED', DATE_SUB(@seed_now, INTERVAL 7 DAY), DATE_SUB(@seed_now, INTERVAL 1 DAY), @seed_now, NULL, @seed_now, NULL),
  ('Head Office Manager',         'ho@fpt.edu.vn',               '0900000002', NULL,       @pwd_hash, @role_ho,      NULL,     @campus_hn, NULL,              'UNKNOWN', NULL,       'PEMS-DEV-HO',          'ACTIVE', @seed_now, 0, NULL, 'MANUAL_CREATED', DATE_SUB(@seed_now, INTERVAL 6 DAY), DATE_SUB(@seed_now, INTERVAL 2 HOUR), @seed_now, NULL, @seed_now, NULL),
  ('IC Staff Leader HN',          'staff.leader.hn@fpt.edu.vn',  '0900000003', NULL,       @pwd_hash, @role_staff,   'Leader', @campus_hn, @dept_hn_ic,       'UNKNOWN', NULL,       'PEMS-DEV-STAFF-L-HN',  'ACTIVE', @seed_now, 0, NULL, 'MANUAL_CREATED', DATE_SUB(@seed_now, INTERVAL 6 DAY), DATE_SUB(@seed_now, INTERVAL 1 HOUR), @seed_now, NULL, @seed_now, NULL),
  ('IC Staff HN',                 'staff.hn@fpt.edu.vn',         '0900000004', NULL,       @pwd_hash, @role_staff,   'Staff',  @campus_hn, @dept_hn_ic,       'UNKNOWN', NULL,       'PEMS-DEV-STAFF-HN',    'ACTIVE', @seed_now, 0, NULL, 'MANUAL_CREATED', DATE_SUB(@seed_now, INTERVAL 5 DAY), DATE_SUB(@seed_now, INTERVAL 30 MINUTE), @seed_now, NULL, @seed_now, NULL),
  ('Department Leader HN',        'dept.leader.hn@fpt.edu.vn',   '0900000005', NULL,       @pwd_hash, @role_dept,    'Leader', @campus_hn, @dept_hn_academic, 'UNKNOWN', NULL,       'PEMS-DEV-DEPT-L-HN',   'ACTIVE', @seed_now, 0, NULL, 'MANUAL_CREATED', DATE_SUB(@seed_now, INTERVAL 5 DAY), DATE_SUB(@seed_now, INTERVAL 1 DAY), @seed_now, NULL, @seed_now, NULL),
  ('Department Staff HN',         'dept.hn@fpt.edu.vn',          '0900000006', NULL,       @pwd_hash, @role_dept,    'Staff',  @campus_hn, @dept_hn_academic, 'UNKNOWN', NULL,       'PEMS-DEV-DEPT-HN',     'ACTIVE', @seed_now, 0, NULL, 'MANUAL_CREATED', DATE_SUB(@seed_now, INTERVAL 4 DAY), DATE_SUB(@seed_now, INTERVAL 2 DAY), @seed_now, NULL, @seed_now, NULL),
  ('Support Student HN',          'student@fpt.edu.vn',          '0900000007', NULL,       @pwd_hash, @role_student, NULL,     @campus_hn, NULL,              'UNKNOWN', 'SE000001', 'PEMS-DEV-STUDENT',     'ACTIVE', @seed_now, 0, NULL, 'MANUAL_CREATED', DATE_SUB(@seed_now, INTERVAL 3 DAY), DATE_SUB(@seed_now, INTERVAL 3 HOUR), @seed_now, NULL, @seed_now, NULL),
  ('External Visitor Example',    'visitor@example.com',         '+84900000008', 'Việt Nam', @pwd_hash, @role_visitor, NULL, NULL, NULL,                         'UNKNOWN', NULL,       NULL,                   'ACTIVE', @seed_now, 0, NULL, 'VISITOR_FORM',   DATE_SUB(@seed_now, INTERVAL 2 DAY), DATE_SUB(@seed_now, INTERVAL 2 HOUR), @seed_now, NULL, @seed_now, NULL)
ON DUPLICATE KEY UPDATE
  full_name = VALUES(full_name),
  phone = VALUES(phone),
  nationality = VALUES(nationality),
  password_hash = VALUES(password_hash),
  role_id = VALUES(role_id),
  sub_role = VALUES(sub_role),
  primary_campus_id = VALUES(primary_campus_id),
  department_id = VALUES(department_id),
  gender = VALUES(gender),
  student_code = VALUES(student_code),
  fe_id = VALUES(fe_id),
  status = 'ACTIVE',
  email_verified_at = COALESCE(users.email_verified_at, VALUES(email_verified_at)),
  failed_login_count = 0,
  locked_until = NULL,
  created_via = VALUES(created_via),
  updated_at = @seed_now;

SELECT user_id INTO @u_admin       FROM users WHERE email = 'admin@fpt.edu.vn' LIMIT 1;
SELECT user_id INTO @u_ho          FROM users WHERE email = 'ho@fpt.edu.vn' LIMIT 1;
SELECT user_id INTO @u_stafflead   FROM users WHERE email = 'staff.leader.hn@fpt.edu.vn' LIMIT 1;
SELECT user_id INTO @u_staff       FROM users WHERE email = 'staff.hn@fpt.edu.vn' LIMIT 1;
SELECT user_id INTO @u_deptlead    FROM users WHERE email = 'dept.leader.hn@fpt.edu.vn' LIMIT 1;
SELECT user_id INTO @u_dept        FROM users WHERE email = 'dept.hn@fpt.edu.vn' LIMIT 1;
SELECT user_id INTO @u_student     FROM users WHERE email = 'student@fpt.edu.vn' LIMIT 1;
SELECT user_id INTO @u_visitor     FROM users WHERE email = 'visitor@example.com' LIMIT 1;

UPDATE campuses SET ic_head_user_id = @u_stafflead, updated_at = @seed_now, updated_by = @u_admin WHERE campus_id = @campus_hn;
UPDATE departments SET head_user_id = @u_stafflead, updated_at = @seed_now, updated_by = @u_admin WHERE department_id = @dept_hn_ic;
UPDATE departments SET head_user_id = @u_deptlead,  updated_at = @seed_now, updated_by = @u_admin WHERE department_id = @dept_hn_academic;

-- Auth providers: LOCAL for dev login + GOOGLE_SSO/FEID placeholders for SSO-first flow.
INSERT INTO user_auth_providers (user_id, provider_type, provider_subject, provider_email, is_enabled, linked_at, last_used_at)
VALUES
  (@u_admin,     'LOCAL_PASSWORD', NULL, 'admin@fpt.edu.vn',            TRUE, @seed_now, DATE_SUB(@seed_now, INTERVAL 1 DAY)),
  (@u_ho,        'LOCAL_PASSWORD', NULL, 'ho@fpt.edu.vn',               TRUE, @seed_now, DATE_SUB(@seed_now, INTERVAL 2 HOUR)),
  (@u_stafflead, 'LOCAL_PASSWORD', NULL, 'staff.leader.hn@fpt.edu.vn',  TRUE, @seed_now, DATE_SUB(@seed_now, INTERVAL 1 HOUR)),
  (@u_staff,     'LOCAL_PASSWORD', NULL, 'staff.hn@fpt.edu.vn',         TRUE, @seed_now, DATE_SUB(@seed_now, INTERVAL 30 MINUTE)),
  (@u_deptlead,  'LOCAL_PASSWORD', NULL, 'dept.leader.hn@fpt.edu.vn',   TRUE, @seed_now, DATE_SUB(@seed_now, INTERVAL 1 DAY)),
  (@u_dept,      'LOCAL_PASSWORD', NULL, 'dept.hn@fpt.edu.vn',          TRUE, @seed_now, DATE_SUB(@seed_now, INTERVAL 2 DAY)),
  (@u_student,   'LOCAL_PASSWORD', NULL, 'student@fpt.edu.vn',          TRUE, @seed_now, DATE_SUB(@seed_now, INTERVAL 3 HOUR)),
  (@u_visitor,   'LOCAL_PASSWORD', NULL, 'visitor@example.com',         TRUE, @seed_now, DATE_SUB(@seed_now, INTERVAL 2 HOUR)),

  (@u_admin,     'GOOGLE_SSO', 'seed-google:admin@fpt.edu.vn',           'admin@fpt.edu.vn',            TRUE, @seed_now, NULL),
  (@u_ho,        'GOOGLE_SSO', 'seed-google:ho@fpt.edu.vn',              'ho@fpt.edu.vn',               TRUE, @seed_now, NULL),
  (@u_stafflead, 'GOOGLE_SSO', 'seed-google:staff.leader.hn@fpt.edu.vn', 'staff.leader.hn@fpt.edu.vn',  TRUE, @seed_now, NULL),
  (@u_staff,     'GOOGLE_SSO', 'seed-google:staff.hn@fpt.edu.vn',        'staff.hn@fpt.edu.vn',         TRUE, @seed_now, NULL),
  (@u_deptlead,  'GOOGLE_SSO', 'seed-google:dept.leader.hn@fpt.edu.vn',  'dept.leader.hn@fpt.edu.vn',   TRUE, @seed_now, NULL),
  (@u_dept,      'GOOGLE_SSO', 'seed-google:dept.hn@fpt.edu.vn',         'dept.hn@fpt.edu.vn',          TRUE, @seed_now, NULL),
  (@u_student,   'GOOGLE_SSO', 'seed-google:student@fpt.edu.vn',         'student@fpt.edu.vn',          TRUE, @seed_now, NULL),
  (@u_visitor,   'GOOGLE_SSO', 'seed-google:visitor@example.com',        'visitor@example.com',         TRUE, @seed_now, NULL)
ON DUPLICATE KEY UPDATE
  provider_email = VALUES(provider_email),
  is_enabled = TRUE,
  last_used_at = VALUES(last_used_at);

SELECT auth_provider_id INTO @ap_admin_local     FROM user_auth_providers WHERE user_id = @u_admin     AND provider_type = 'LOCAL_PASSWORD' LIMIT 1;
SELECT auth_provider_id INTO @ap_ho_local        FROM user_auth_providers WHERE user_id = @u_ho        AND provider_type = 'LOCAL_PASSWORD' LIMIT 1;
SELECT auth_provider_id INTO @ap_stafflead_local FROM user_auth_providers WHERE user_id = @u_stafflead AND provider_type = 'LOCAL_PASSWORD' LIMIT 1;
SELECT auth_provider_id INTO @ap_staff_local     FROM user_auth_providers WHERE user_id = @u_staff     AND provider_type = 'LOCAL_PASSWORD' LIMIT 1;
SELECT auth_provider_id INTO @ap_visitor_local   FROM user_auth_providers WHERE user_id = @u_visitor   AND provider_type = 'LOCAL_PASSWORD' LIMIT 1;

-- Sessions obey portal rules: internal users use INTERNAL + selected campus; Visitor uses VISITOR + NULL campus.
INSERT INTO user_sessions
  (user_id, login_portal, selected_campus_id, auth_provider_id, refresh_token_hash, refresh_expires_at,
   ip_address, user_agent, created_at, expires_at)
VALUES
  (@u_admin,     'INTERNAL', @campus_hn, @ap_admin_local,     'seed-refresh-pems-8-admin',       DATE_ADD(@seed_now, INTERVAL 7 DAY), '10.0.0.10', 'PEMS seed data', DATE_SUB(@seed_now, INTERVAL 1 DAY), DATE_ADD(@seed_now, INTERVAL 7 DAY)),
  (@u_ho,        'INTERNAL', @campus_hn, @ap_ho_local,        'seed-refresh-pems-8-ho',          DATE_ADD(@seed_now, INTERVAL 7 DAY), '10.0.0.11', 'PEMS seed data', DATE_SUB(@seed_now, INTERVAL 2 HOUR), DATE_ADD(@seed_now, INTERVAL 7 DAY)),
  (@u_stafflead, 'INTERNAL', @campus_hn, @ap_stafflead_local, 'seed-refresh-pems-8-staffleader', DATE_ADD(@seed_now, INTERVAL 7 DAY), '10.0.0.12', 'PEMS seed data', DATE_SUB(@seed_now, INTERVAL 1 HOUR), DATE_ADD(@seed_now, INTERVAL 7 DAY)),
  (@u_staff,     'INTERNAL', @campus_hn, @ap_staff_local,     'seed-refresh-pems-8-staff',       DATE_ADD(@seed_now, INTERVAL 7 DAY), '10.0.0.13', 'PEMS seed data', DATE_SUB(@seed_now, INTERVAL 30 MINUTE), DATE_ADD(@seed_now, INTERVAL 7 DAY)),
  (@u_visitor,   'VISITOR',  NULL,       @ap_visitor_local,   'seed-refresh-pems-8-visitor',     DATE_ADD(@seed_now, INTERVAL 7 DAY), '203.0.113.8', 'PEMS seed data', DATE_SUB(@seed_now, INTERVAL 2 HOUR), DATE_ADD(@seed_now, INTERVAL 7 DAY));

SELECT session_id INTO @sess_ho      FROM user_sessions WHERE refresh_token_hash = 'seed-refresh-pems-8-ho' LIMIT 1;
SELECT session_id INTO @sess_staff   FROM user_sessions WHERE refresh_token_hash = 'seed-refresh-pems-8-staff' LIMIT 1;
SELECT session_id INTO @sess_visitor FROM user_sessions WHERE refresh_token_hash = 'seed-refresh-pems-8-visitor' LIMIT 1;

INSERT INTO login_logs
  (user_id, email, login_portal, selected_campus_id, provider_type, status, failure_reason, ip_address, user_agent, session_id, created_at)
VALUES
  (@u_ho,        'ho@fpt.edu.vn',              'INTERNAL', @campus_hn, 'LOCAL_PASSWORD', 'SUCCESS', NULL, '10.0.0.11', 'PEMS seed data', @sess_ho,      DATE_SUB(@seed_now, INTERVAL 2 HOUR)),
  (@u_staff,     'staff.hn@fpt.edu.vn',        'INTERNAL', @campus_hn, 'LOCAL_PASSWORD', 'SUCCESS', NULL, '10.0.0.13', 'PEMS seed data', @sess_staff,   DATE_SUB(@seed_now, INTERVAL 30 MINUTE)),
  (@u_visitor,   'visitor@example.com',        'VISITOR',  NULL,       'LOCAL_PASSWORD', 'SUCCESS', NULL, '203.0.113.8', 'PEMS seed data', @sess_visitor, DATE_SUB(@seed_now, INTERVAL 2 HOUR)),
  (NULL,         'visitor@example.com',        'INTERNAL', @campus_hn, 'LOCAL_PASSWORD', 'FAILED',  'Visitor attempted internal portal', '203.0.113.8', 'PEMS seed data', NULL, DATE_SUB(@seed_now, INTERVAL 1 HOUR));

INSERT INTO otp_tokens
  (user_id, email, token_type, purpose, token_hash, expires_at, used_at, attempt_count, max_attempts, resend_count, ip_address, user_agent, created_at)
VALUES
  (@u_visitor, 'visitor@example.com', 'OTP_CODE', 'VISIT_REQUEST_VERIFY',    'seed-token-pems-8-visit-visitor-used', DATE_ADD(@seed_now, INTERVAL 15 MINUTE), DATE_SUB(@seed_now, INTERVAL 2 DAY), 1, 5, 0, '203.0.113.8', 'PEMS seed data', DATE_SUB(@seed_now, INTERVAL 2 DAY)),
  (@u_admin,   'admin@fpt.edu.vn',    'OTP_CODE', 'CHANGE_SENSITIVE_ACTION', 'seed-token-pems-8-admin-sensitive',     DATE_ADD(@seed_now, INTERVAL 10 MINUTE), NULL,                              0, 5, 0, '10.0.0.10',   'PEMS seed data', @seed_now);

INSERT INTO security_events
  (user_id, email, event_type, severity, ip_address, user_agent, metadata, created_at)
VALUES
  (@u_visitor, 'visitor@example.com', 'PEMS_SEED_PORTAL_MISMATCH', 'LOW',    '203.0.113.8', 'PEMS seed data', JSON_OBJECT('reason','Visitor tried internal portal in seed scenario'), DATE_SUB(@seed_now, INTERVAL 1 HOUR)),
  (@u_admin,   'admin@fpt.edu.vn',    'PEMS_SEED_ADMIN_OTP',       'LOW',    '10.0.0.10',   'PEMS seed data', JSON_OBJECT('purpose','CHANGE_SENSITIVE_ACTION'), @seed_now);

-- =====================================================================
-- 3. PARTNER, FILES, DOCUMENTS
-- =====================================================================

INSERT INTO partners
  (partner_code, name, short_name, country, city, website_url, partner_type, cooperation_status, description, created_at, created_by, updated_at, updated_by)
VALUES
  ('PEMS-SEED-KNU', 'Korea National University', 'KNU', 'South Korea', 'Seoul', 'https://knu.example.edu', 'UNIVERSITY', 'ACTIVE', 'PEMS seed partner for multi-campus pending request.', DATE_SUB(@seed_now, INTERVAL 40 DAY), @u_ho, @seed_now, @u_ho),
  ('PEMS-SEED-GTC', 'GreenTech Collaboration Ltd.', 'GreenTech', 'Singapore', 'Singapore', 'https://greentech.example.com', 'COMPANY', 'POTENTIAL', 'PEMS seed partner for single-campus visit workflow.', DATE_SUB(@seed_now, INTERVAL 30 DAY), @u_stafflead, @seed_now, @u_stafflead)
ON DUPLICATE KEY UPDATE
  name = VALUES(name),
  short_name = VALUES(short_name),
  country = VALUES(country),
  city = VALUES(city),
  website_url = VALUES(website_url),
  partner_type = VALUES(partner_type),
  cooperation_status = VALUES(cooperation_status),
  description = VALUES(description),
  updated_at = @seed_now,
  updated_by = VALUES(updated_by);

SELECT partner_id INTO @partner_knu FROM partners WHERE partner_code = 'PEMS-SEED-KNU' LIMIT 1;
SELECT partner_id INTO @partner_gtc FROM partners WHERE partner_code = 'PEMS-SEED-GTC' LIMIT 1;

INSERT INTO partner_contacts
  (partner_id, full_name, email, phone, job_title, department_name, note, is_primary, status, created_at, created_by, updated_at, updated_by)
VALUES
  (@partner_knu, 'Prof. Kim Min Seo', 'kim.minseo@knu.example.edu', '+821012345678', 'Director of International Affairs', 'Global Cooperation Office', 'Primary contact for HO multi-campus pending request.', TRUE, 'ACTIVE', @seed_now, @u_ho, @seed_now, @u_ho),
  (@partner_gtc, 'Emily Smith', 'emily.smith@greentech.example.com', '+6591234567', 'Partnership Manager', 'Education Solutions', 'Primary contact for HN single-campus visit.', TRUE, 'ACTIVE', @seed_now, @u_stafflead, @seed_now, @u_stafflead)
ON DUPLICATE KEY UPDATE
  full_name = VALUES(full_name),
  phone = VALUES(phone),
  job_title = VALUES(job_title),
  department_name = VALUES(department_name),
  note = VALUES(note),
  is_primary = VALUES(is_primary),
  status = 'ACTIVE',
  updated_at = @seed_now;

SELECT contact_id INTO @contact_knu FROM partner_contacts WHERE partner_id = @partner_knu AND email = 'kim.minseo@knu.example.edu' LIMIT 1;
SELECT contact_id INTO @contact_gtc FROM partner_contacts WHERE partner_id = @partner_gtc AND email = 'emily.smith@greentech.example.com' LIMIT 1;

INSERT INTO files
  (storage_provider, bucket_name, object_key, original_filename, mime_type, file_size, checksum_sha256, visibility, uploaded_by, uploaded_at)
VALUES
  ('LOCAL', NULL, 'seed/pems/8-accounts/visit/invitation-letter.pdf',       'invitation-letter.pdf',       'application/pdf', 204800, REPEAT('a',64), 'PRIVATE',  @u_visitor,   DATE_SUB(@seed_now, INTERVAL 5 DAY)),
  ('LOCAL', NULL, 'seed/pems/8-accounts/visit/agenda-draft.docx',           'agenda-draft.docx',           'application/vnd.openxmlformats-officedocument.wordprocessingml.document', 102400, REPEAT('b',64), 'INTERNAL', @u_staff,     DATE_SUB(@seed_now, INTERVAL 4 DAY)),
  ('LOCAL', NULL, 'seed/pems/8-accounts/news/cover-campus-tour.jpg',        'cover-campus-tour.jpg',        'image/jpeg', 512000, REPEAT('c',64), 'PUBLIC',   @u_staff,     DATE_SUB(@seed_now, INTERVAL 3 DAY)),
  ('LOCAL', NULL, 'seed/pems/8-accounts/news/inline-lab.jpg',               'inline-lab.jpg',               'image/jpeg', 500000, REPEAT('d',64), 'PUBLIC',   @u_staff,     DATE_SUB(@seed_now, INTERVAL 3 DAY)),
  ('LOCAL', NULL, 'seed/pems/8-accounts/gallery/hn-library.jpg',            'hn-library.jpg',               'image/jpeg', 600000, REPEAT('e',64), 'PUBLIC',   @u_stafflead, DATE_SUB(@seed_now, INTERVAL 20 DAY)),
  ('LOCAL', NULL, 'seed/pems/8-accounts/gallery/hn-green-lab.jpg',          'hn-green-lab.jpg',             'image/jpeg', 620000, REPEAT('f',64), 'PUBLIC',   @u_stafflead, DATE_SUB(@seed_now, INTERVAL 20 DAY)),
  ('LOCAL', NULL, 'seed/pems/8-accounts/logistics/teabreak-plan.pdf',       'teabreak-plan.pdf',            'application/pdf', 98000,  REPEAT('1',64), 'INTERNAL', @u_dept,      DATE_SUB(@seed_now, INTERVAL 2 DAY)),
  ('LOCAL', NULL, 'seed/pems/8-accounts/minutes/final-minutes.pdf',         'final-minutes.pdf',            'application/pdf', 128000, REPEAT('2',64), 'INTERNAL', @u_staff,     DATE_SUB(@seed_now, INTERVAL 1 DAY))
ON DUPLICATE KEY UPDATE
  original_filename = VALUES(original_filename),
  mime_type = VALUES(mime_type),
  file_size = VALUES(file_size),
  checksum_sha256 = VALUES(checksum_sha256),
  visibility = VALUES(visibility),
  uploaded_by = VALUES(uploaded_by),
  uploaded_at = VALUES(uploaded_at);

SELECT file_id INTO @file_invitation     FROM files WHERE object_key = 'seed/pems/8-accounts/visit/invitation-letter.pdf' LIMIT 1;
SELECT file_id INTO @file_agenda_draft   FROM files WHERE object_key = 'seed/pems/8-accounts/visit/agenda-draft.docx' LIMIT 1;
SELECT file_id INTO @file_news_cover     FROM files WHERE object_key = 'seed/pems/8-accounts/news/cover-campus-tour.jpg' LIMIT 1;
SELECT file_id INTO @file_news_inline    FROM files WHERE object_key = 'seed/pems/8-accounts/news/inline-lab.jpg' LIMIT 1;
SELECT file_id INTO @file_gallery_1      FROM files WHERE object_key = 'seed/pems/8-accounts/gallery/hn-library.jpg' LIMIT 1;
SELECT file_id INTO @file_gallery_2      FROM files WHERE object_key = 'seed/pems/8-accounts/gallery/hn-green-lab.jpg' LIMIT 1;
SELECT file_id INTO @file_logistics_plan FROM files WHERE object_key = 'seed/pems/8-accounts/logistics/teabreak-plan.pdf' LIMIT 1;
SELECT file_id INTO @file_minutes_pdf    FROM files WHERE object_key = 'seed/pems/8-accounts/minutes/final-minutes.pdf' LIMIT 1;

INSERT INTO documents
  (file_id, owner_type, owner_id, campus_id, title, description, document_category, status, created_at, created_by, updated_at, updated_by)
VALUES
  (@file_invitation,     'PARTNER', @partner_gtc, @campus_hn, 'PEMS Seed: Partner invitation letter', 'Invitation letter uploaded by visitor before request submission.', 'INVITATION', 'PUBLISHED', DATE_SUB(@seed_now, INTERVAL 5 DAY), @u_visitor, @seed_now, @u_stafflead),
  (@file_agenda_draft,   'VISIT',   NULL,         @campus_hn, 'PEMS Seed: Draft visit agenda',        'Draft agenda prepared by IC Staff HN.', 'AGENDA', 'PUBLISHED', DATE_SUB(@seed_now, INTERVAL 4 DAY), @u_staff, @seed_now, @u_staff),
  (@file_logistics_plan, 'LOGISTICS', NULL,       @campus_hn, 'PEMS Seed: Teabreak logistics plan',   'Department logistics plan for approved visit.', 'LOGISTICS_PLAN', 'PUBLISHED', DATE_SUB(@seed_now, INTERVAL 2 DAY), @u_dept, @seed_now, @u_deptlead),
  (@file_minutes_pdf,    'MINUTES', NULL,         @campus_hn, 'PEMS Seed: Final minutes attachment',  'PDF export of final meeting minutes.', 'MINUTES_EXPORT', 'PUBLISHED', DATE_SUB(@seed_now, INTERVAL 1 DAY), @u_staff, @seed_now, @u_stafflead);

-- =====================================================================
-- 4. VISIT / DELEGATION WORKFLOW
-- =====================================================================

INSERT INTO visit_requests
  (request_code, visitor_user_id, partner_id, registrant_full_name, registrant_organization, registrant_job_title,
   registrant_phone, registrant_email, registrant_nationality, delegation_name, visit_scope, purpose, working_content,
   expected_guest_count, support_team_json, contact_person_json, working_language, interpreter_note, transportation_note,
   note_to_fptu, status, submitted_at, email_verified_at, decided_by, decided_at, decision_actor_role, decision_note,
   cancelled_by, cancelled_at, cancellation_actor_type, cancellation_source, cancellation_reason,
   row_version, created_at, created_by, updated_at, updated_by)
VALUES
  ('PEMS-VR-MULTI-PENDING', @u_visitor, @partner_knu, 'External Visitor Example', 'Korea National University', 'International Coordinator',
   '+84900000008', 'visitor@example.com', 'Việt Nam', 'KNU Academic Exchange Delegation', 'MULTI_CAMPUS',
   'Trao đổi cơ hội hợp tác học thuật giữa nhiều campus FPT University.', 'Làm việc với HO và tham quan campus Hà Nội, TP.HCM.',
   3, JSON_ARRAY(JSON_OBJECT('name','KNU Global Team','email','global@knu.example.edu')), JSON_OBJECT('full_name','Prof. Kim Min Seo','phone','+821012345678','email','kim.minseo@knu.example.edu'),
   'EN', NULL, 'Đoàn tự di chuyển bằng xe thuê riêng.', 'Đề nghị hỗ trợ phòng họp và campus tour.',
   'PENDING_APPROVAL', DATE_SUB(@seed_now, INTERVAL 2 DAY), DATE_SUB(@seed_now, INTERVAL 2 DAY), NULL, NULL, NULL, NULL,
   NULL, NULL, NULL, NULL, NULL,
   0, DATE_SUB(@seed_now, INTERVAL 2 DAY), @u_visitor, @seed_now, @u_visitor),

  ('PEMS-VR-SINGLE-APPROVED', @u_visitor, @partner_gtc, 'External Visitor Example', 'GreenTech Collaboration Ltd.', 'Partnership Manager',
   '+84900000008', 'visitor@example.com', 'Việt Nam', 'GreenTech EdTech Demo Visit', 'SINGLE_CAMPUS',
   'Tham quan FPTU Hà Nội và demo giải pháp EdTech.', 'Demo sản phẩm, gặp IC Office và Academic Department.',
   2, JSON_ARRAY(JSON_OBJECT('name','GreenTech Product Team','email','product@greentech.example.com')), JSON_OBJECT('full_name','Emily Smith','phone','+6591234567','email','emily.smith@greentech.example.com'),
   'EN', NULL, 'Xe riêng đến cổng campus.', 'Cần phòng demo có màn hình LED.',
   'APPROVED', DATE_SUB(@seed_now, INTERVAL 5 DAY), DATE_SUB(@seed_now, INTERVAL 5 DAY), @u_stafflead, DATE_SUB(@seed_now, INTERVAL 4 DAY), 'STAFF_LEADER', 'Staff Leader HN approved and assigned host.',
   NULL, NULL, NULL, NULL, NULL,
   1, DATE_SUB(@seed_now, INTERVAL 5 DAY), @u_visitor, @seed_now, @u_stafflead),

  ('PEMS-VR-SINGLE-CLOSED', @u_visitor, @partner_gtc, 'External Visitor Example', 'GreenTech Collaboration Ltd.', 'Partnership Manager',
   '+84900000008', 'visitor@example.com', 'Việt Nam', 'GreenTech Follow-up Working Session', 'SINGLE_CAMPUS',
   'Làm việc sau chuyến thăm để thống nhất đầu việc hợp tác.', 'Tổng kết nội dung trao đổi, ghi minutes và feedback.',
   2, JSON_ARRAY(JSON_OBJECT('name','GreenTech Education Team','email','education@greentech.example.com')), JSON_OBJECT('full_name','Emily Smith','phone','+6591234567','email','emily.smith@greentech.example.com'),
   'EN', NULL, 'Khách tự di chuyển.', 'Đã hoàn tất chuyến thăm, cần lưu biên bản và feedback.',
   'APPROVED', DATE_SUB(@seed_now, INTERVAL 16 DAY), DATE_SUB(@seed_now, INTERVAL 16 DAY), @u_stafflead, DATE_SUB(@seed_now, INTERVAL 15 DAY), 'STAFF_LEADER', 'Approved for follow-up working session.',
   NULL, NULL, NULL, NULL, NULL,
   3, DATE_SUB(@seed_now, INTERVAL 16 DAY), @u_visitor, @seed_now, @u_stafflead),

  ('PEMS-VR-SINGLE-CANCELLED', @u_visitor, @partner_gtc, 'External Visitor Example', 'GreenTech Collaboration Ltd.', 'Partnership Manager',
   '+84900000008', 'visitor@example.com', 'Việt Nam', 'GreenTech Cancelled Campus Visit', 'SINGLE_CAMPUS',
   'Chuyến thăm đã được duyệt nhưng khách hủy sau đó.', 'Lưu tình huống hủy sau khi được duyệt để test cancel delegation.',
   1, JSON_ARRAY(), JSON_OBJECT('full_name','Emily Smith','phone','+6591234567','email','emily.smith@greentech.example.com'),
   'EN', NULL, 'Khách tự di chuyển.', 'Seed tình huống cancellation post-approval.',
   'CANCELLED', DATE_SUB(@seed_now, INTERVAL 10 DAY), DATE_SUB(@seed_now, INTERVAL 10 DAY), @u_stafflead, DATE_SUB(@seed_now, INTERVAL 9 DAY), 'STAFF_LEADER', 'Approved before later cancellation.',
   @u_visitor, DATE_SUB(@seed_now, INTERVAL 7 DAY), 'VISITOR', 'SELF_SERVICE', 'Visitor cancelled after approval due to schedule conflict.',
   2, DATE_SUB(@seed_now, INTERVAL 10 DAY), @u_visitor, @seed_now, @u_visitor);

SELECT visit_request_id INTO @vr_multi_pending   FROM visit_requests WHERE request_code = 'PEMS-VR-MULTI-PENDING' LIMIT 1;
SELECT visit_request_id INTO @vr_single_approved FROM visit_requests WHERE request_code = 'PEMS-VR-SINGLE-APPROVED' LIMIT 1;
SELECT visit_request_id INTO @vr_single_closed   FROM visit_requests WHERE request_code = 'PEMS-VR-SINGLE-CLOSED' LIMIT 1;
SELECT visit_request_id INTO @vr_single_cancel   FROM visit_requests WHERE request_code = 'PEMS-VR-SINGLE-CANCELLED' LIMIT 1;

INSERT INTO visit_request_campuses
  (visit_request_id, campus_id, instance_code, planned_start_at, planned_end_at, status,
   current_host_user_id, host_assigned_by, host_assigned_at, host_assignment_source,
   closed_by, closed_at, close_note,
   cancelled_by, cancelled_at, cancellation_actor_type, cancellation_source, cancellation_reason,
   row_version, created_at, created_by, updated_at, updated_by)
VALUES
  (@vr_multi_pending, @campus_hn,  'PEMS-VI-MULTI-HN-PENDING',  DATE_ADD(@seed_now, INTERVAL 14 DAY), DATE_ADD(DATE_ADD(@seed_now, INTERVAL 14 DAY), INTERVAL 3 HOUR), 'WAITING_REQUEST_APPROVAL',
   NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, 0, DATE_SUB(@seed_now, INTERVAL 2 DAY), @u_visitor, @seed_now, @u_visitor),
  (@vr_multi_pending, @campus_hcm, 'PEMS-VI-MULTI-HCM-PENDING', DATE_ADD(@seed_now, INTERVAL 16 DAY), DATE_ADD(DATE_ADD(@seed_now, INTERVAL 16 DAY), INTERVAL 3 HOUR), 'WAITING_REQUEST_APPROVAL',
   NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, 0, DATE_SUB(@seed_now, INTERVAL 2 DAY), @u_visitor, @seed_now, @u_visitor),

  (@vr_single_approved, @campus_hn, 'PEMS-VI-SINGLE-HN-APPROVED', DATE_ADD(@seed_now, INTERVAL 3 DAY), DATE_ADD(DATE_ADD(@seed_now, INTERVAL 3 DAY), INTERVAL 4 HOUR), 'BEFORE_VISIT',
   @u_staff, @u_stafflead, DATE_SUB(@seed_now, INTERVAL 4 DAY), 'MANUAL_APPROVAL', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, 1, DATE_SUB(@seed_now, INTERVAL 5 DAY), @u_visitor, @seed_now, @u_stafflead),

  (@vr_single_closed, @campus_hn, 'PEMS-VI-SINGLE-HN-CLOSED', DATE_SUB(@seed_now, INTERVAL 13 DAY), DATE_ADD(DATE_SUB(@seed_now, INTERVAL 13 DAY), INTERVAL 4 HOUR), 'CLOSED',
   @u_staff, @u_stafflead, DATE_SUB(@seed_now, INTERVAL 15 DAY), 'MANUAL_APPROVAL', @u_stafflead, DATE_SUB(@seed_now, INTERVAL 12 DAY), 'Closed after final minutes and feedback.', NULL, NULL, NULL, NULL, NULL, 3, DATE_SUB(@seed_now, INTERVAL 16 DAY), @u_visitor, @seed_now, @u_stafflead),

  (@vr_single_cancel, @campus_hn, 'PEMS-VI-SINGLE-HN-CANCELLED', DATE_ADD(@seed_now, INTERVAL 5 DAY), DATE_ADD(DATE_ADD(@seed_now, INTERVAL 5 DAY), INTERVAL 2 HOUR), 'CANCELLED',
   @u_stafflead, @u_stafflead, DATE_SUB(@seed_now, INTERVAL 9 DAY), 'MANUAL_APPROVAL', NULL, NULL, NULL, @u_visitor, DATE_SUB(@seed_now, INTERVAL 7 DAY), 'VISITOR', 'SELF_SERVICE', 'Visitor cancelled after approval due to schedule conflict.', 2, DATE_SUB(@seed_now, INTERVAL 10 DAY), @u_visitor, @seed_now, @u_visitor);

SELECT visit_instance_id INTO @vi_multi_hn        FROM visit_request_campuses WHERE instance_code = 'PEMS-VI-MULTI-HN-PENDING' LIMIT 1;
SELECT visit_instance_id INTO @vi_multi_hcm       FROM visit_request_campuses WHERE instance_code = 'PEMS-VI-MULTI-HCM-PENDING' LIMIT 1;
SELECT visit_instance_id INTO @vi_single_approved FROM visit_request_campuses WHERE instance_code = 'PEMS-VI-SINGLE-HN-APPROVED' LIMIT 1;
SELECT visit_instance_id INTO @vi_single_closed   FROM visit_request_campuses WHERE instance_code = 'PEMS-VI-SINGLE-HN-CLOSED' LIMIT 1;
SELECT visit_instance_id INTO @vi_single_cancel   FROM visit_request_campuses WHERE instance_code = 'PEMS-VI-SINGLE-HN-CANCELLED' LIMIT 1;

INSERT INTO visit_guest_members
  (visit_request_id, full_name, organization, job_title, nationality, email, phone, is_representative, note, created_at, created_by)
VALUES
  (@vr_multi_pending, 'Prof. Kim Min Seo', 'Korea National University', 'Director', 'South Korea', 'kim.minseo@knu.example.edu', '+821012345678', TRUE, 'Representative for pending HO approval.', DATE_SUB(@seed_now, INTERVAL 2 DAY), @u_visitor),
  (@vr_multi_pending, 'Lee Joon Ho', 'Korea National University', 'Program Manager', 'South Korea', 'lee.joonho@knu.example.edu', '+821055512345', FALSE, 'Campus tour participant.', DATE_SUB(@seed_now, INTERVAL 2 DAY), @u_visitor),
  (@vr_multi_pending, 'Park Hana', 'Korea National University', 'Research Coordinator', 'South Korea', 'park.hana@knu.example.edu', '+821077712345', FALSE, 'Campus tour participant.', DATE_SUB(@seed_now, INTERVAL 2 DAY), @u_visitor),
  (@vr_single_approved, 'Emily Smith', 'GreenTech Collaboration Ltd.', 'Partnership Manager', 'Singapore', 'emily.smith@greentech.example.com', '+6591234567', TRUE, 'Main guest for upcoming approved visit.', DATE_SUB(@seed_now, INTERVAL 5 DAY), @u_visitor),
  (@vr_single_approved, 'Daniel Tan', 'GreenTech Collaboration Ltd.', 'Product Lead', 'Singapore', 'daniel.tan@greentech.example.com', '+6597654321', FALSE, 'Demo product lead.', DATE_SUB(@seed_now, INTERVAL 5 DAY), @u_visitor),
  (@vr_single_closed, 'Emily Smith', 'GreenTech Collaboration Ltd.', 'Partnership Manager', 'Singapore', 'emily.smith@greentech.example.com', '+6591234567', TRUE, 'Guest who submitted feedback.', DATE_SUB(@seed_now, INTERVAL 16 DAY), @u_visitor),
  (@vr_single_cancel, 'Emily Smith', 'GreenTech Collaboration Ltd.', 'Partnership Manager', 'Singapore', 'emily.smith@greentech.example.com', '+6591234567', TRUE, 'Cancelled visit guest.', DATE_SUB(@seed_now, INTERVAL 10 DAY), @u_visitor);

SELECT guest_member_id INTO @guest_closed_emily FROM visit_guest_members WHERE visit_request_id = @vr_single_closed AND email = 'emily.smith@greentech.example.com' LIMIT 1;

INSERT INTO visit_participants
  (visit_instance_id, user_id, participant_role, is_host, status, invited_by, invited_at, responded_at, assigned_by, assigned_at, note, created_at, created_by)
VALUES
  (@vi_single_approved, @u_staff,     'IC_HOST',       TRUE,  'ASSIGNED', @u_stafflead, DATE_SUB(@seed_now, INTERVAL 4 DAY), DATE_SUB(@seed_now, INTERVAL 4 DAY), @u_stafflead, DATE_SUB(@seed_now, INTERVAL 4 DAY), 'Main host for upcoming approved visit.', DATE_SUB(@seed_now, INTERVAL 4 DAY), @u_stafflead),
  (@vi_single_approved, @u_stafflead, 'IC_SUPPORT',    FALSE, 'ASSIGNED', @u_stafflead, DATE_SUB(@seed_now, INTERVAL 4 DAY), DATE_SUB(@seed_now, INTERVAL 4 DAY), @u_stafflead, DATE_SUB(@seed_now, INTERVAL 4 DAY), 'Staff Leader supervises preparation.', DATE_SUB(@seed_now, INTERVAL 4 DAY), @u_stafflead),
  (@vi_single_approved, @u_deptlead,  'DEPT_SUPPORT',  FALSE, 'ACCEPTED', @u_stafflead, DATE_SUB(@seed_now, INTERVAL 4 DAY), DATE_SUB(@seed_now, INTERVAL 3 DAY), @u_stafflead, DATE_SUB(@seed_now, INTERVAL 4 DAY), 'Academic Department lead joins demo discussion.', DATE_SUB(@seed_now, INTERVAL 4 DAY), @u_stafflead),
  (@vi_single_approved, @u_dept,      'DEPT_SUPPORT',  FALSE, 'ASSIGNED', @u_deptlead,  DATE_SUB(@seed_now, INTERVAL 3 DAY), DATE_SUB(@seed_now, INTERVAL 3 DAY), @u_deptlead,  DATE_SUB(@seed_now, INTERVAL 3 DAY), 'Department staff handles room/logistics coordination.', DATE_SUB(@seed_now, INTERVAL 3 DAY), @u_deptlead),
  (@vi_single_approved, @u_student,   'STUDENT_BUDDY', FALSE, 'ACCEPTED', @u_stafflead, DATE_SUB(@seed_now, INTERVAL 3 DAY), DATE_SUB(@seed_now, INTERVAL 2 DAY), @u_stafflead, DATE_SUB(@seed_now, INTERVAL 3 DAY), 'Student buddy for campus tour.', DATE_SUB(@seed_now, INTERVAL 3 DAY), @u_stafflead),

  (@vi_single_closed, @u_staff,       'IC_HOST',       TRUE,  'ASSIGNED', @u_stafflead, DATE_SUB(@seed_now, INTERVAL 15 DAY), DATE_SUB(@seed_now, INTERVAL 15 DAY), @u_stafflead, DATE_SUB(@seed_now, INTERVAL 15 DAY), 'Main host of closed visit.', DATE_SUB(@seed_now, INTERVAL 15 DAY), @u_stafflead),
  (@vi_single_closed, @u_dept,        'DEPT_SUPPORT',  FALSE, 'ASSIGNED', @u_deptlead,  DATE_SUB(@seed_now, INTERVAL 15 DAY), DATE_SUB(@seed_now, INTERVAL 15 DAY), @u_deptlead,  DATE_SUB(@seed_now, INTERVAL 15 DAY), 'Logistics support of closed visit.', DATE_SUB(@seed_now, INTERVAL 15 DAY), @u_deptlead);

INSERT INTO visit_agendas
  (visit_instance_id, sequence_order, title, description, start_time, end_time, location, responsible_user_id, created_at, created_by)
VALUES
  (@vi_single_approved, 1, 'Welcome and introduction', 'IC Office welcomes GreenTech delegation.', DATE_ADD(@seed_now, INTERVAL 3 DAY), DATE_ADD(DATE_ADD(@seed_now, INTERVAL 3 DAY), INTERVAL 45 MINUTE), 'Alpha Building - Meeting Room 201', @u_staff, DATE_SUB(@seed_now, INTERVAL 4 DAY), @u_staff),
  (@vi_single_approved, 2, 'EdTech product demo', 'GreenTech presents product and FPTU Academic Department comments.', DATE_ADD(DATE_ADD(@seed_now, INTERVAL 3 DAY), INTERVAL 1 HOUR), DATE_ADD(DATE_ADD(@seed_now, INTERVAL 3 DAY), INTERVAL 2 HOUR), 'Alpha Building - Demo Room', @u_deptlead, DATE_SUB(@seed_now, INTERVAL 4 DAY), @u_staff),
  (@vi_single_approved, 3, 'Campus tour', 'Student buddy guides library and learning spaces.', DATE_ADD(DATE_ADD(@seed_now, INTERVAL 3 DAY), INTERVAL 2 HOUR), DATE_ADD(DATE_ADD(@seed_now, INTERVAL 3 DAY), INTERVAL 3 HOUR), 'Campus Hòa Lạc', @u_student, DATE_SUB(@seed_now, INTERVAL 4 DAY), @u_staff),
  (@vi_single_closed, 1, 'Follow-up meeting', 'Closed seed visit meeting already completed.', DATE_SUB(@seed_now, INTERVAL 13 DAY), DATE_ADD(DATE_SUB(@seed_now, INTERVAL 13 DAY), INTERVAL 2 HOUR), 'Alpha Building - Meeting Room 203', @u_staff, DATE_SUB(@seed_now, INTERVAL 15 DAY), @u_staff);

INSERT INTO visit_logistics_items
  (visit_instance_id, item_type, title, description, quantity, usage_start_at, usage_end_at, status, priority,
   requested_by, requested_to_department_id, requested_at, received_by, received_at, assigned_to_user_id, assigned_by, assigned_at,
   assignee_accepted_at, assignee_response_note, due_at, completed_at,
   proposed_by, proposed_at, proposed_quantity, proposed_usage_start_at, proposed_usage_end_at, proposed_description, proposal_note,
   proposal_responded_by, proposal_responded_at, proposal_response, proposal_response_note, decision_note,
   row_version, created_at, created_by, updated_at, updated_by)
VALUES
  (@vi_single_approved, 'ROOM', 'Demo room reservation', 'Reserve demo room with LED screen and HDMI adapter.', 1,
   DATE_ADD(@seed_now, INTERVAL 3 DAY), DATE_ADD(DATE_ADD(@seed_now, INTERVAL 3 DAY), INTERVAL 2 HOUR), 'READY', 'HIGH',
   @u_stafflead, @dept_hn_academic, DATE_SUB(@seed_now, INTERVAL 4 DAY), @u_deptlead, DATE_SUB(@seed_now, INTERVAL 3 DAY), @u_dept, @u_deptlead, DATE_SUB(@seed_now, INTERVAL 3 DAY),
   DATE_SUB(@seed_now, INTERVAL 3 DAY), 'Room checked and ready.', DATE_ADD(@seed_now, INTERVAL 2 DAY), NULL,
   NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL,
   1, DATE_SUB(@seed_now, INTERVAL 4 DAY), @u_stafflead, @seed_now, @u_dept),

  (@vi_single_approved, 'MEAL', 'Teabreak for GreenTech delegation', 'Prepare light teabreak for 8 internal/external participants.', 8,
   DATE_ADD(DATE_ADD(@seed_now, INTERVAL 3 DAY), INTERVAL 45 MINUTE), DATE_ADD(DATE_ADD(@seed_now, INTERVAL 3 DAY), INTERVAL 1 HOUR), 'IN_PROGRESS', 'MEDIUM',
   @u_stafflead, @dept_hn_academic, DATE_SUB(@seed_now, INTERVAL 4 DAY), @u_deptlead, DATE_SUB(@seed_now, INTERVAL 3 DAY), @u_dept, @u_deptlead, DATE_SUB(@seed_now, INTERVAL 3 DAY),
   DATE_SUB(@seed_now, INTERVAL 3 DAY), 'Accepted; preparing supplier confirmation.', DATE_ADD(@seed_now, INTERVAL 2 DAY), NULL,
   @u_dept, DATE_SUB(@seed_now, INTERVAL 2 DAY), 10, DATE_ADD(DATE_ADD(@seed_now, INTERVAL 3 DAY), INTERVAL 40 MINUTE), DATE_ADD(DATE_ADD(@seed_now, INTERVAL 3 DAY), INTERVAL 1 HOUR), 'Increase quantity because Academic Department added attendees.', 'Need 2 extra portions.',
   @u_stafflead, DATE_SUB(@seed_now, INTERVAL 1 DAY), 'ACCEPTED', 'Approved additional quantity.', NULL,
   2, DATE_SUB(@seed_now, INTERVAL 4 DAY), @u_stafflead, @seed_now, @u_dept),

  (@vi_single_closed, 'OTHER', 'Closed visit support summary', 'Completed logistics support for closed visit.', 1,
   DATE_SUB(@seed_now, INTERVAL 13 DAY), DATE_ADD(DATE_SUB(@seed_now, INTERVAL 13 DAY), INTERVAL 2 HOUR), 'DONE', 'LOW',
   @u_stafflead, @dept_hn_academic, DATE_SUB(@seed_now, INTERVAL 15 DAY), @u_deptlead, DATE_SUB(@seed_now, INTERVAL 14 DAY), @u_dept, @u_deptlead, DATE_SUB(@seed_now, INTERVAL 14 DAY),
   DATE_SUB(@seed_now, INTERVAL 14 DAY), 'Completed as planned.', DATE_SUB(@seed_now, INTERVAL 13 DAY), DATE_ADD(DATE_SUB(@seed_now, INTERVAL 13 DAY), INTERVAL 1 HOUR),
   NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, 'Closed logistics item.',
   1, DATE_SUB(@seed_now, INTERVAL 15 DAY), @u_stafflead, @seed_now, @u_dept);

SELECT logistics_item_id INTO @log_room
FROM visit_logistics_items WHERE visit_instance_id = @vi_single_approved AND title = 'Demo room reservation' LIMIT 1;
SELECT logistics_item_id INTO @log_teabreak
FROM visit_logistics_items WHERE visit_instance_id = @vi_single_approved AND title = 'Teabreak for GreenTech delegation' LIMIT 1;

-- =====================================================================
-- 5. MINUTES + FEEDBACK
-- =====================================================================

INSERT INTO minutes
  (visit_instance_id, title, content, participants_json, status, finalized_by, finalized_at, created_at, created_by, updated_at, updated_by)
VALUES
  (@vi_single_closed, 'PEMS Seed: Final minutes - GreenTech Follow-up Working Session',
   '<p>Hai bên thống nhất tiếp tục trao đổi về khả năng thử nghiệm giải pháp EdTech trong học kỳ tới.</p><p>IC Staff HN phụ trách đầu mối, Academic Department hỗ trợ đánh giá chuyên môn.</p>',
   JSON_ARRAY(JSON_OBJECT('name','IC Staff HN','role','Host'), JSON_OBJECT('name','Emily Smith','role','Visitor'), JSON_OBJECT('name','Department Staff HN','role','Logistics')),
   'FINAL', @u_stafflead, DATE_SUB(@seed_now, INTERVAL 12 DAY), DATE_SUB(@seed_now, INTERVAL 13 DAY), @u_staff, @seed_now, @u_stafflead);

SELECT minutes_id INTO @minutes_closed FROM minutes WHERE visit_instance_id = @vi_single_closed LIMIT 1;

INSERT INTO minute_action_items
  (minutes_id, title, note, due_date, status, completed_at, display_order, created_at, created_by, updated_at, updated_by)
VALUES
  (@minutes_closed, 'Send partnership proposal draft', 'IC Staff prepares the first proposal draft for GreenTech.', DATE_ADD(CURDATE(), INTERVAL 7 DAY), 'IN_PROGRESS', NULL, 1, DATE_SUB(@seed_now, INTERVAL 12 DAY), @u_staff, @seed_now, @u_staff),
  (@minutes_closed, 'Collect academic feedback', 'Academic Department collects feedback from lecturers about pilot feasibility.', DATE_ADD(CURDATE(), INTERVAL 10 DAY), 'TODO', NULL, 2, DATE_SUB(@seed_now, INTERVAL 12 DAY), @u_deptlead, @seed_now, @u_deptlead),
  (@minutes_closed, 'Upload signed minutes PDF', 'Final exported minutes attached in document list.', DATE_SUB(CURDATE(), INTERVAL 11 DAY), 'DONE', DATE_SUB(@seed_now, INTERVAL 11 DAY), 3, DATE_SUB(@seed_now, INTERVAL 12 DAY), @u_staff, @seed_now, @u_stafflead);

INSERT INTO feedbacks
  (visit_request_id, visit_instance_id, submitted_by_user_id, submitter_role, submitter_context, submitter_name_snapshot,
   target_user_id, target_role, target_context, target_name_snapshot, rating, comment, submitted_at)
VALUES
  (@vr_single_closed, @vi_single_closed, @u_visitor, 'VISITOR', 'Khách đại diện', 'External Visitor Example',
   @u_staff, 'HOST', 'Host chính', 'IC Staff HN', 5, 'Host hỗ trợ rất rõ ràng, lịch trình đúng giờ và giao tiếp chuyên nghiệp.', DATE_SUB(@seed_now, INTERVAL 11 DAY)),
  (@vr_single_closed, @vi_single_closed, @u_staff, 'HOST', 'Host chính', 'IC Staff HN',
   @u_visitor, 'VISITOR', 'Đoàn khách', 'External Visitor Example', 5, 'Đoàn khách chuẩn bị nội dung đầy đủ và phản hồi nhanh sau buổi làm việc.', DATE_SUB(@seed_now, INTERVAL 11 DAY)),
  (@vr_single_closed, @vi_single_closed, @u_staff, 'HOST', 'Host chính', 'IC Staff HN',
   @u_dept, 'LOGISTICS', 'Hỗ trợ hậu cần', 'Department Staff HN', 4, 'Hỗ trợ phòng họp và hậu cần đúng tiến độ.', DATE_SUB(@seed_now, INTERVAL 11 DAY));

-- =====================================================================
-- 6. NEWS, FAQ, GALLERY, FACE TAGGING
-- =====================================================================

INSERT INTO news
  (campus_id, visit_instance_id, author_user_id, cover_file_id, status, submitted_at,
   reviewed_by, reviewed_at, review_note, published_at, is_featured, row_version, created_at, created_by, updated_at, updated_by)
VALUES
  (@campus_hn, @vi_single_closed, @u_staff, @file_news_cover, 'PUBLISHED', DATE_SUB(@seed_now, INTERVAL 10 DAY),
   @u_stafflead, DATE_SUB(@seed_now, INTERVAL 9 DAY), 'Approved by host for public news page.', DATE_SUB(@seed_now, INTERVAL 9 DAY), TRUE, 1, DATE_SUB(@seed_now, INTERVAL 10 DAY), @u_staff, @seed_now, @u_stafflead),
  (@campus_hn, @vi_single_approved, @u_student, NULL, 'PENDING_REVIEW', DATE_SUB(@seed_now, INTERVAL 1 DAY),
   NULL, NULL, NULL, NULL, FALSE, 0, DATE_SUB(@seed_now, INTERVAL 1 DAY), @u_student, @seed_now, @u_student);

SELECT news_id INTO @news_published FROM news WHERE visit_instance_id = @vi_single_closed AND author_user_id = @u_staff LIMIT 1;
SELECT news_id INTO @news_pending   FROM news WHERE visit_instance_id = @vi_single_approved AND author_user_id = @u_student LIMIT 1;

INSERT INTO news_translations
  (news_id, language_code, title, slug, summary, seo_title, seo_description, created_at, updated_at)
VALUES
  (@news_published, 'vi', 'PEMS Seed: GreenTech làm việc cùng FPT University Hà Nội', 'pems-seed-campus-tour-recap',
   'Bài viết seed mô phỏng tin đã được host duyệt sau chuyến làm việc với đối tác.', 'GreenTech làm việc cùng FPTU Hà Nội', 'Seed news article for PEMS published workflow.', DATE_SUB(@seed_now, INTERVAL 10 DAY), @seed_now),
  (@news_pending, 'vi', 'PEMS Seed: Ghi chú sinh viên về chuyến tham quan sắp tới', 'pems-seed-student-pending-story',
   'Bài viết seed ở trạng thái chờ host duyệt.', 'Ghi chú sinh viên về chuyến tham quan', 'Seed pending news article for host review workflow.', DATE_SUB(@seed_now, INTERVAL 1 DAY), @seed_now)
ON DUPLICATE KEY UPDATE
  title = VALUES(title),
  summary = VALUES(summary),
  seo_title = VALUES(seo_title),
  seo_description = VALUES(seo_description),
  updated_at = @seed_now;

SELECT news_translation_id INTO @nt_published FROM news_translations WHERE slug = 'pems-seed-campus-tour-recap' LIMIT 1;
SELECT news_translation_id INTO @nt_pending   FROM news_translations WHERE slug = 'pems-seed-student-pending-story' LIMIT 1;

INSERT INTO news_content_sections
  (news_translation_id, section_order, section_title, section_body_html, section_body_text, created_at, updated_at)
VALUES
  (@nt_published, 1, 'Buổi làm việc hợp tác', '<p>Đại diện GreenTech và FPT University Hà Nội trao đổi về khả năng thử nghiệm giải pháp học tập số.</p>', 'Đại diện GreenTech và FPT University Hà Nội trao đổi về khả năng thử nghiệm giải pháp học tập số.', DATE_SUB(@seed_now, INTERVAL 10 DAY), @seed_now),
  (@nt_published, 2, 'Campus tour và định hướng tiếp theo', '<p>Đoàn khách tham quan không gian học tập, thư viện và khu demo công nghệ trước khi thống nhất các đầu việc sau buổi họp.</p>', 'Đoàn khách tham quan không gian học tập, thư viện và khu demo công nghệ trước khi thống nhất các đầu việc sau buổi họp.', DATE_SUB(@seed_now, INTERVAL 10 DAY), @seed_now),
  (@nt_pending, 1, 'Góc nhìn sinh viên hỗ trợ', '<p>Sinh viên buddy chuẩn bị lộ trình campus tour và ghi chú các điểm cần hỗ trợ khách.</p>', 'Sinh viên buddy chuẩn bị lộ trình campus tour và ghi chú các điểm cần hỗ trợ khách.', DATE_SUB(@seed_now, INTERVAL 1 DAY), @seed_now)
ON DUPLICATE KEY UPDATE
  section_title = VALUES(section_title),
  section_body_html = VALUES(section_body_html),
  section_body_text = VALUES(section_body_text),
  updated_at = @seed_now;

SELECT section_id INTO @section_published_2 FROM news_content_sections WHERE news_translation_id = @nt_published AND section_order = 2 LIMIT 1;

INSERT INTO news_section_files
  (section_id, file_id, usage_type, display_order, created_at)
VALUES
  (@section_published_2, @file_news_inline, 'INLINE_IMAGE', 1, @seed_now)
ON DUPLICATE KEY UPDATE
  usage_type = VALUES(usage_type),
  display_order = VALUES(display_order);

INSERT INTO faqs
  (category, question, answer, display_order, status, created_at, created_by, updated_at, updated_by)
VALUES
  ('PEMS Seed', 'Visitor có cần chọn campus khi đăng nhập không?', 'Không. Visitor portal không chọn campus; campus được xác định trong từng visit request.', 10, 'PUBLISHED', @seed_now, @u_stafflead, @seed_now, @u_stafflead),
  ('PEMS Seed', 'Staff Leader HN thấy những visit nào?', 'Staff Leader HN thấy single-campus của HN và multi-campus sau khi HO duyệt/release có chứa campus HN.', 20, 'PUBLISHED', @seed_now, @u_stafflead, @seed_now, @u_stafflead),
  ('PEMS Seed', 'Admin có xem dữ liệu tiếp đoàn không?', 'Không. Admin là vai trò kỹ thuật, không được seed dữ liệu nghiệp vụ visit/delegation.', 30, 'PUBLISHED', @seed_now, @u_admin, @seed_now, @u_admin);

INSERT INTO galleries
  (campus_id, location_name, title, description, story_content, status, visibility, created_at, created_by, updated_at, updated_by)
VALUES
  (@campus_hn, 'PEMS Seed: FPTU HN Library', 'PEMS Seed: Không gian thư viện FPTU Hà Nội',
   'Gallery seed cho địa điểm trong campus.', 'Thư viện là điểm dừng chính trong campus tour cho đối tác và sinh viên hỗ trợ.', 'PUBLISHED', 'PUBLIC', DATE_SUB(@seed_now, INTERVAL 20 DAY), @u_stafflead, @seed_now, @u_stafflead),
  (@campus_hn, 'PEMS Seed: Green Lab', 'PEMS Seed: Green Lab FPTU Hà Nội',
   'Gallery seed cho không gian học tập và demo.', 'Green Lab được dùng để giới thiệu môi trường học tập ứng dụng công nghệ.', 'PUBLISHED', 'INTERNAL', DATE_SUB(@seed_now, INTERVAL 20 DAY), @u_stafflead, @seed_now, @u_stafflead);

SELECT gallery_id INTO @gallery_library FROM galleries WHERE campus_id = @campus_hn AND location_name = 'PEMS Seed: FPTU HN Library' LIMIT 1;
SELECT gallery_id INTO @gallery_greenlab FROM galleries WHERE campus_id = @campus_hn AND location_name = 'PEMS Seed: Green Lab' LIMIT 1;

INSERT INTO gallery_images
  (gallery_id, file_id, caption, display_order, taken_at, status, created_at, created_by, updated_at, updated_by)
VALUES
  (@gallery_library, @file_gallery_1, 'Không gian thư viện trong campus tour.', 1, DATE_SUB(@seed_now, INTERVAL 20 DAY), 'ACTIVE', @seed_now, @u_stafflead, @seed_now, @u_stafflead),
  (@gallery_greenlab, @file_gallery_2, 'Green Lab dùng cho hoạt động demo công nghệ.', 1, DATE_SUB(@seed_now, INTERVAL 20 DAY), 'ACTIVE', @seed_now, @u_stafflead, @seed_now, @u_stafflead);

SELECT image_id INTO @image_library FROM gallery_images WHERE file_id = @file_gallery_1 LIMIT 1;
SELECT image_id INTO @image_greenlab FROM gallery_images WHERE file_id = @file_gallery_2 LIMIT 1;

INSERT INTO photo_face_tags
  (image_id, visit_request_id, guest_member_id, partner_contact_id, display_name,
   bounding_box_x, bounding_box_y, bounding_box_width, bounding_box_height, tag_status,
   confirmed_by, confirmed_at, created_at, created_by)
VALUES
  (@image_library, @vr_single_closed, @guest_closed_emily, @contact_gtc, 'Emily Smith', 0.2100, 0.1800, 0.1200, 0.1800, 'CONFIRMED', @u_stafflead, DATE_SUB(@seed_now, INTERVAL 11 DAY), DATE_SUB(@seed_now, INTERVAL 12 DAY), @u_staff),
  (@image_greenlab, @vr_single_closed, NULL, NULL, 'IC Staff HN', 0.4200, 0.2000, 0.1000, 0.1600, 'MANUALLY_TAGGED', NULL, NULL, DATE_SUB(@seed_now, INTERVAL 12 DAY), @u_staff);

-- =====================================================================
-- 7. EMAIL, NOTIFICATION, CALENDAR
-- =====================================================================

INSERT INTO email_templates
  (template_code, name, purpose, status, translations_json, variables_json, created_at, created_by, updated_at, updated_by)
VALUES
  ('PEMS_SEED_VISIT_SUBMITTED', 'PEMS Seed: Visit request submitted', 'VISIT_REQUEST_SUBMITTED', 'ACTIVE',
   JSON_OBJECT('vi', JSON_OBJECT('subject','[PEMS] Có đơn visit mới', 'bodyHtml','<p>Xin chào {{FullName}}, hệ thống có đơn visit mới: {{RequestCode}}</p>'),
               'en', JSON_OBJECT('subject','[PEMS] New visit request', 'bodyHtml','<p>Hello {{FullName}}, a new visit request is available: {{RequestCode}}</p>')),
   JSON_ARRAY('FullName','RequestCode','VisitScope'), @seed_now, @u_admin, @seed_now, @u_admin),
  ('PEMS_SEED_VISIT_APPROVED', 'PEMS Seed: Visit request approved', 'VISIT_REQUEST_APPROVED', 'ACTIVE',
   JSON_OBJECT('vi', JSON_OBJECT('subject','[PEMS] Đơn visit đã được duyệt', 'bodyHtml','<p>Đơn {{RequestCode}} đã được duyệt.</p>'),
               'en', JSON_OBJECT('subject','[PEMS] Visit request approved', 'bodyHtml','<p>Request {{RequestCode}} has been approved.</p>')),
   JSON_ARRAY('FullName','RequestCode','HostName'), @seed_now, @u_admin, @seed_now, @u_admin)
ON DUPLICATE KEY UPDATE
  name = VALUES(name),
  purpose = VALUES(purpose),
  status = 'ACTIVE',
  translations_json = VALUES(translations_json),
  variables_json = VALUES(variables_json),
  updated_at = @seed_now,
  updated_by = VALUES(updated_by);

SELECT email_template_id INTO @tpl_submitted FROM email_templates WHERE template_code = 'PEMS_SEED_VISIT_SUBMITTED' LIMIT 1;
SELECT email_template_id INTO @tpl_approved  FROM email_templates WHERE template_code = 'PEMS_SEED_VISIT_APPROVED' LIMIT 1;

INSERT INTO sent_emails
  (email_template_id, related_type, related_id, subject, body_snapshot, recipients_json, metadata_json, status, error_message, sent_by, sent_at, created_at)
VALUES
  (@tpl_submitted, 'VISIT_REQUEST', @vr_multi_pending, 'PEMS Seed: New multi-campus visit request waiting for HO approval',
   '<p>Request PEMS-VR-MULTI-PENDING is waiting for HO approval.</p>', JSON_ARRAY(JSON_OBJECT('email','ho@fpt.edu.vn','name','Head Office Manager','type','TO')), JSON_OBJECT('provider','seed','messageId','seed-email-multi-pending'), 'SENT', NULL, @u_stafflead, DATE_SUB(@seed_now, INTERVAL 2 DAY), DATE_SUB(@seed_now, INTERVAL 2 DAY)),
  (@tpl_approved, 'VISIT_REQUEST', @vr_single_approved, 'PEMS Seed: Single-campus visit request approved',
   '<p>Request PEMS-VR-SINGLE-APPROVED has been approved and assigned to IC Staff HN.</p>', JSON_ARRAY(JSON_OBJECT('email','visitor@example.com','name','External Visitor Example','type','TO')), JSON_OBJECT('provider','seed','messageId','seed-email-single-approved'), 'SENT', NULL, @u_stafflead, DATE_SUB(@seed_now, INTERVAL 4 DAY), DATE_SUB(@seed_now, INTERVAL 4 DAY));

INSERT INTO notifications
  (recipient_user_id, title, message, notification_type, related_type, related_id, is_read, read_at, created_at)
VALUES
  (@u_ho, 'PEMS Seed: Multi-campus request cần HO duyệt', 'PEMS seed: PEMS-VR-MULTI-PENDING đang chờ HO duyệt.', 'VISIT_PENDING_APPROVAL', 'VISIT_REQUEST', @vr_multi_pending, FALSE, NULL, DATE_SUB(@seed_now, INTERVAL 2 DAY)),
  (@u_stafflead, 'PEMS Seed: Single-campus request đã duyệt', 'PEMS seed: PEMS-VR-SINGLE-APPROVED đã duyệt và giao host.', 'VISIT_APPROVED', 'VISIT_REQUEST', @vr_single_approved, TRUE, DATE_SUB(@seed_now, INTERVAL 4 DAY), DATE_SUB(@seed_now, INTERVAL 4 DAY)),
  (@u_deptlead, 'PEMS Seed: Có yêu cầu hậu cần mới', 'PEMS seed: Demo room và teabreak cần phòng ban xử lý.', 'LOGISTICS_REQUESTED', 'LOGISTICS_ITEM', @log_teabreak, FALSE, NULL, DATE_SUB(@seed_now, INTERVAL 3 DAY)),
  (@u_visitor, 'PEMS Seed: Đơn visit đã được duyệt', 'PEMS seed: PEMS-VR-SINGLE-APPROVED đã được duyệt.', 'VISIT_APPROVED', 'VISIT_REQUEST', @vr_single_approved, TRUE, DATE_SUB(@seed_now, INTERVAL 4 DAY), DATE_SUB(@seed_now, INTERVAL 4 DAY));

INSERT INTO calendar_events
  (owner_user_id, campus_id, visit_instance_id, logistics_item_id, source_type, title, description, location,
   start_at, end_at, timezone, visibility, attendees_json, reminders_json, status, created_at, created_by, updated_at, updated_by)
VALUES
  (@u_staff, @campus_hn, @vi_single_approved, NULL, 'VISIT', 'PEMS Seed: Host GreenTech visit', 'PEMS seed: calendar event for IC host.', 'Alpha Building - Meeting Room 201',
   DATE_ADD(@seed_now, INTERVAL 3 DAY), DATE_ADD(DATE_ADD(@seed_now, INTERVAL 3 DAY), INTERVAL 4 HOUR), 'Asia/Ho_Chi_Minh', 'INTERNAL',
   JSON_ARRAY(JSON_OBJECT('email','staff.hn@fpt.edu.vn','role','HOST'), JSON_OBJECT('email','dept.leader.hn@fpt.edu.vn','role','ATTENDEE')), JSON_ARRAY(JSON_OBJECT('method','popup','minutes',60)), 'ACTIVE', @seed_now, @u_stafflead, @seed_now, @u_stafflead),
  (@u_dept, @campus_hn, @vi_single_approved, @log_teabreak, 'LOGISTICS', 'PEMS Seed: Prepare teabreak logistics', 'PEMS seed: logistics deadline event.', 'Alpha Building pantry',
   DATE_ADD(@seed_now, INTERVAL 2 DAY), DATE_ADD(DATE_ADD(@seed_now, INTERVAL 2 DAY), INTERVAL 1 HOUR), 'Asia/Ho_Chi_Minh', 'PRIVATE',
   JSON_ARRAY(JSON_OBJECT('email','dept.hn@fpt.edu.vn','role','ASSIGNEE')), JSON_ARRAY(JSON_OBJECT('method','popup','minutes',120)), 'ACTIVE', @seed_now, @u_deptlead, @seed_now, @u_deptlead),
  (@u_student, @campus_hn, @vi_single_approved, NULL, 'PERSONAL', 'PEMS Seed: Review campus tour route', 'PEMS seed: student buddy personal preparation event.', 'FPTU HN campus',
   DATE_ADD(DATE_ADD(@seed_now, INTERVAL 2 DAY), INTERVAL 2 HOUR), DATE_ADD(DATE_ADD(@seed_now, INTERVAL 2 DAY), INTERVAL 3 HOUR), 'Asia/Ho_Chi_Minh', 'PRIVATE',
   JSON_ARRAY(JSON_OBJECT('email','student@fpt.edu.vn','role','OWNER')), JSON_ARRAY(JSON_OBJECT('method','popup','minutes',30)), 'ACTIVE', @seed_now, @u_student, @seed_now, @u_student);

-- =====================================================================
-- 8. API CONFIG, QUOTA, AGENDA TEMPLATE, AUDIT
-- =====================================================================

INSERT INTO api_configurations
  (api_code, name, provider_name, purpose, base_url, default_method, auth_type, credentials_json, headers_json, body_template_json, settings_json,
   timeout_seconds, status, created_at, created_by, updated_at, updated_by)
VALUES
  ('PEMS_DEV_GOOGLE_SSO', 'PEMS Dev Google SSO Config', 'Google', 'SSO_LOGIN', 'https://accounts.google.com', 'POST', 'OAUTH2',
   JSON_OBJECT('clientId','masked-dev-client-id.apps.googleusercontent.com','clientSecret','***'), JSON_OBJECT('Content-Type','application/json'), NULL, JSON_OBJECT('environment','DEV','seed','8-accounts'), 30, 'ACTIVE', @seed_now, @u_admin, @seed_now, @u_admin),
  ('PEMS_DEV_EMAIL_GATEWAY', 'PEMS Dev Email Gateway', 'SMTP Seed', 'SEND_EMAIL', 'https://email-gateway.example.local', 'POST', 'API_KEY',
   JSON_OBJECT('apiKey','***'), JSON_OBJECT('Content-Type','application/json'), JSON_OBJECT('to','{{To}}','subject','{{Subject}}','body','{{Body}}'), JSON_OBJECT('environment','DEV','seed','8-accounts'), 30, 'ACTIVE', @seed_now, @u_admin, @seed_now, @u_admin)
ON DUPLICATE KEY UPDATE
  name = VALUES(name),
  provider_name = VALUES(provider_name),
  purpose = VALUES(purpose),
  base_url = VALUES(base_url),
  default_method = VALUES(default_method),
  auth_type = VALUES(auth_type),
  credentials_json = VALUES(credentials_json),
  headers_json = VALUES(headers_json),
  body_template_json = VALUES(body_template_json),
  settings_json = VALUES(settings_json),
  timeout_seconds = VALUES(timeout_seconds),
  status = 'ACTIVE',
  updated_at = @seed_now,
  updated_by = VALUES(updated_by);

SELECT api_config_id INTO @api_google FROM api_configurations WHERE api_code = 'PEMS_DEV_GOOGLE_SSO' LIMIT 1;
SELECT api_config_id INTO @api_email  FROM api_configurations WHERE api_code = 'PEMS_DEV_EMAIL_GATEWAY' LIMIT 1;

INSERT INTO api_usage_quotas
  (api_config_id, campus_id, campus_scope_key, period_yyyymm, monthly_limit, used_count, last_used_at, created_at, created_by, updated_at, updated_by)
VALUES
  (@api_google, NULL,       'GLOBAL', DATE_FORMAT(@seed_now, '%Y%m'), 10000, 15, DATE_SUB(@seed_now, INTERVAL 1 HOUR), @seed_now, @u_admin, @seed_now, @u_admin),
  (@api_email,  @campus_hn, 'GLOBAL', DATE_FORMAT(@seed_now, '%Y%m'), 5000,  8,  DATE_SUB(@seed_now, INTERVAL 2 HOUR), @seed_now, @u_admin, @seed_now, @u_admin)
ON DUPLICATE KEY UPDATE
  monthly_limit = VALUES(monthly_limit),
  used_count = VALUES(used_count),
  last_used_at = VALUES(last_used_at),
  updated_at = @seed_now,
  updated_by = VALUES(updated_by);

INSERT INTO api_request_logs
  (api_config_id, campus_id, requested_by, related_type, related_id, endpoint, method, http_status,
   response_time_ms, request_size_bytes, response_size_bytes, success, error_code, error_message, created_at)
VALUES
  (@api_google, NULL,       @u_visitor, 'AUTH', @u_visitor, '/oauth2/v3/token/seed/pems/visitor', 'POST', 200, 180, 1024, 2048, TRUE, NULL, NULL, DATE_SUB(@seed_now, INTERVAL 2 HOUR)),
  (@api_email,  @campus_hn, @u_stafflead, 'VISIT_REQUEST', @vr_single_approved, '/send/seed/pems/visit-approved', 'POST', 202, 250, 2048, 1024, TRUE, NULL, NULL, DATE_SUB(@seed_now, INTERVAL 4 DAY));

INSERT INTO agenda_templates
  (campus_id, campus_scope_key, name, description, items_json, status, created_at, created_by, updated_at, updated_by)
VALUES
  (@campus_hn, 'GLOBAL', 'PEMS Seed: Standard partner visit agenda', 'Standard HN agenda template for partner visits.',
   JSON_ARRAY(
     JSON_OBJECT('order',1,'title','Welcome','durationMinutes',45,'location','Meeting room'),
     JSON_OBJECT('order',2,'title','Working session','durationMinutes',75,'location','Demo room'),
     JSON_OBJECT('order',3,'title','Campus tour','durationMinutes',60,'location','Campus')
   ), 'ACTIVE', @seed_now, @u_stafflead, @seed_now, @u_stafflead)
ON DUPLICATE KEY UPDATE
  description = VALUES(description),
  items_json = VALUES(items_json),
  status = 'ACTIVE',
  updated_at = @seed_now,
  updated_by = VALUES(updated_by);

INSERT INTO audit_logs
  (actor_user_id, campus_id, action, entity_type, entity_id, old_values_json, new_values_json, ip_address, user_agent, request_id, created_at)
VALUES
  (@u_admin, NULL, 'SEED_ACCOUNTS_UPSERTED', 'USER', NULL, NULL, JSON_OBJECT('emails', JSON_ARRAY('admin@fpt.edu.vn','ho@fpt.edu.vn','staff.leader.hn@fpt.edu.vn','staff.hn@fpt.edu.vn','dept.leader.hn@fpt.edu.vn','dept.hn@fpt.edu.vn','student@fpt.edu.vn','visitor@example.com')), '10.0.0.10', 'PEMS seed data', 'seed-pems-8-accounts-users', @seed_now),
  (@u_stafflead, @campus_hn, 'SEED_SINGLE_REQUEST_APPROVED', 'VISIT_REQUEST', @vr_single_approved, JSON_OBJECT('status','PENDING_APPROVAL'), JSON_OBJECT('status','APPROVED','hostUserId',@u_staff), '10.0.0.12', 'PEMS seed data', 'seed-pems-8-accounts-visit-approved', DATE_SUB(@seed_now, INTERVAL 4 DAY)),
  (@u_ho, @campus_hn, 'SEED_MULTI_REQUEST_PENDING_FOR_HO', 'VISIT_REQUEST', @vr_multi_pending, NULL, JSON_OBJECT('status','PENDING_APPROVAL','scope','MULTI_CAMPUS'), '10.0.0.11', 'PEMS seed data', 'seed-pems-8-accounts-multi-pending', DATE_SUB(@seed_now, INTERVAL 2 DAY)),
  (@u_stafflead, @campus_hn, 'SEED_NEWS_REVIEWED', 'NEWS', @news_published, JSON_OBJECT('status','PENDING_REVIEW'), JSON_OBJECT('status','PUBLISHED'), '10.0.0.12', 'PEMS seed data', 'seed-pems-8-accounts-news', DATE_SUB(@seed_now, INTERVAL 9 DAY)),
  (@u_deptlead, @campus_hn, 'SEED_LOGISTICS_ASSIGNED', 'LOGISTICS_ITEM', @log_teabreak, JSON_OBJECT('status','REQUESTED'), JSON_OBJECT('status','IN_PROGRESS','assignedTo',@u_dept), '10.0.0.15', 'PEMS seed data', 'seed-pems-8-accounts-logistics', DATE_SUB(@seed_now, INTERVAL 3 DAY));

INSERT INTO visit_status_logs
  (visit_request_id, visit_instance_id, status_owner_type, old_status, new_status, changed_by, reason, changed_at)
VALUES
  (@vr_multi_pending, NULL, 'REQUEST', NULL, 'PENDING_APPROVAL', @u_visitor, 'PEMS seed: Visitor submitted multi-campus request after email verification.', DATE_SUB(@seed_now, INTERVAL 2 DAY)),
  (@vr_single_approved, NULL, 'REQUEST', 'PENDING_APPROVAL', 'APPROVED', @u_stafflead, 'PEMS seed: Staff Leader approved single-campus request.', DATE_SUB(@seed_now, INTERVAL 4 DAY)),
  (@vr_single_approved, @vi_single_approved, 'CAMPUS_INSTANCE', 'WAITING_REQUEST_APPROVAL', 'BEFORE_VISIT', @u_stafflead, 'PEMS seed: HN instance assigned to IC Staff host.', DATE_SUB(@seed_now, INTERVAL 4 DAY)),
  (@vr_single_closed, @vi_single_closed, 'CAMPUS_INSTANCE', 'AFTER_VISIT', 'CLOSED', @u_stafflead, 'PEMS seed: Closed after final minutes and feedback.', DATE_SUB(@seed_now, INTERVAL 12 DAY)),
  (@vr_single_cancel, NULL, 'REQUEST', 'APPROVED', 'CANCELLED', @u_visitor, 'PEMS seed: Visitor cancelled approved delegation.', DATE_SUB(@seed_now, INTERVAL 7 DAY)),
  (@vr_single_cancel, @vi_single_cancel, 'CAMPUS_INSTANCE', 'ASSIGNED', 'CANCELLED', @u_visitor, 'PEMS seed: Campus instance cancelled together with request.', DATE_SUB(@seed_now, INTERVAL 7 DAY));

-- =====================================================================
-- 9. VERIFICATION QUERIES
-- =====================================================================

SELECT 'PEMS 8-account scenario seed completed' AS message, @seed_now AS seed_runtime;

SELECT u.email, r.role_code, u.sub_role, c.campus_code, d.department_code, u.status, u.created_via
FROM users u
JOIN roles r ON r.role_id = u.role_id
LEFT JOIN campuses c ON c.campus_id = u.primary_campus_id
LEFT JOIN departments d ON d.department_id = u.department_id
WHERE u.email IN (
  'admin@fpt.edu.vn', 'ho@fpt.edu.vn', 'staff.leader.hn@fpt.edu.vn', 'staff.hn@fpt.edu.vn',
  'dept.leader.hn@fpt.edu.vn', 'dept.hn@fpt.edu.vn', 'student@fpt.edu.vn', 'visitor@example.com'
)
ORDER BY FIELD(u.email,
  'admin@fpt.edu.vn', 'ho@fpt.edu.vn', 'staff.leader.hn@fpt.edu.vn', 'staff.hn@fpt.edu.vn',
  'dept.leader.hn@fpt.edu.vn', 'dept.hn@fpt.edu.vn', 'student@fpt.edu.vn', 'visitor@example.com'
);

SELECT vr.request_code, vr.visit_scope, vr.status AS request_status, vrc.instance_code, c.campus_code,
       vrc.status AS campus_status, host.email AS current_host_email
FROM visit_requests vr
JOIN visit_request_campuses vrc ON vrc.visit_request_id = vr.visit_request_id
JOIN campuses c ON c.campus_id = vrc.campus_id
LEFT JOIN users host ON host.user_id = vrc.current_host_user_id
WHERE vr.request_code IN ('PEMS-VR-MULTI-PENDING','PEMS-VR-SINGLE-APPROVED','PEMS-VR-SINGLE-CLOSED','PEMS-VR-SINGLE-CANCELLED')
ORDER BY vr.request_code, c.campus_code;

SELECT 'module_counts' AS section, 'partners' AS table_name, COUNT(*) AS total FROM partners WHERE partner_code IN ('PEMS-SEED-KNU','PEMS-SEED-GTC')
UNION ALL SELECT 'module_counts', 'files', COUNT(*) FROM files WHERE object_key LIKE 'seed/pems/8-accounts/%'
UNION ALL SELECT 'module_counts', 'visit_requests', COUNT(*) FROM visit_requests WHERE request_code LIKE 'PEMS-VR-%'
UNION ALL SELECT 'module_counts', 'visit_logistics_items', COUNT(*) FROM visit_logistics_items WHERE visit_instance_id IN (@vi_single_approved, @vi_single_closed)
UNION ALL SELECT 'module_counts', 'minutes', COUNT(*) FROM minutes WHERE visit_instance_id = @vi_single_closed
UNION ALL SELECT 'module_counts', 'feedbacks', COUNT(*) FROM feedbacks WHERE visit_request_id = @vr_single_closed
UNION ALL SELECT 'module_counts', 'news', COUNT(*) FROM news WHERE news_id IN (@news_published, @news_pending)
UNION ALL SELECT 'module_counts', 'galleries', COUNT(*) FROM galleries WHERE title LIKE 'PEMS Seed:%'
UNION ALL SELECT 'module_counts', 'notifications', COUNT(*) FROM notifications WHERE title LIKE 'PEMS Seed:%'
UNION ALL SELECT 'module_counts', 'audit_logs', COUNT(*) FROM audit_logs WHERE request_id LIKE 'seed-pems-8-accounts-%';

COMMIT;
SET SQL_SAFE_UPDATES = @OLD_SQL_SAFE_UPDATES;
