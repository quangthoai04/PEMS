-- =====================================================================
-- PEMS — Development / test accounts (DEV ONLY — DO NOT RUN IN PRODUCTION)
-- Run AFTER pems_full.sql + permissions.sql + permission_matrix.sql.
-- Idempotent (INSERT IGNORE on unique email). Mirrors DevAccountSeed.cs /
-- the backend startup seeder (which is the preferred mechanism).
--
-- All accounts share password: Admin@123  (BCrypt hash below).
-- =====================================================================
USE pems_db;

SET @pwd_hash = '$2a$12$VZUmvJruqj01vtokbs7bk./uw4.L42lXXeuuhuQs2sUdvXBVBGU/6';

SET @role_admin   = (SELECT role_id FROM roles WHERE role_code = 'ADMIN'   AND deleted_at IS NULL LIMIT 1);
SET @role_ho      = (SELECT role_id FROM roles WHERE role_code = 'HO'      AND deleted_at IS NULL LIMIT 1);
SET @role_staff   = (SELECT role_id FROM roles WHERE role_code = 'STAFF'   AND deleted_at IS NULL LIMIT 1);
SET @role_dept    = (SELECT role_id FROM roles WHERE role_code = 'DEPT'    AND deleted_at IS NULL LIMIT 1);
SET @role_student = (SELECT role_id FROM roles WHERE role_code = 'STUDENT' AND deleted_at IS NULL LIMIT 1);
SET @role_visitor = (SELECT role_id FROM roles WHERE role_code = 'VISITOR' AND deleted_at IS NULL LIMIT 1);

SET @campus_hn   = (SELECT campus_id FROM campuses WHERE campus_code = 'HN' LIMIT 1);
SET @dept_ic_hn  = (SELECT department_id FROM departments WHERE campus_id = @campus_hn AND department_code = 'IC' LIMIT 1);
SET @dept_aca_hn = (SELECT department_id FROM departments WHERE campus_id = @campus_hn AND department_code = 'ACADEMIC' LIMIT 1);

-- ── Users ────────────────────────────────────────────────────────────────────
INSERT IGNORE INTO users
  (user_id, full_name, email, password_hash, role_id, sub_role, primary_campus_id, department_id,
   status, email_verified_at, must_set_password, must_change_password, created_via, created_at)
VALUES
  (UUID(), 'System Administrator',      'admin@fpt.edu.vn',            @pwd_hash, @role_admin,   NULL,     @campus_hn, NULL,         'ACTIVE', NOW(), 0, 0, 'ADMIN_CREATED', NOW()),
  (UUID(), 'Head Office Manager',       'ho@fpt.edu.vn',               @pwd_hash, @role_ho,      NULL,     @campus_hn, NULL,         'ACTIVE', NOW(), 0, 0, 'ADMIN_CREATED', NOW()),
  (UUID(), 'IC Staff Leader (HN)',      'staff.leader.hn@fpt.edu.vn',  @pwd_hash, @role_staff,   'Leader', @campus_hn, @dept_ic_hn,  'ACTIVE', NOW(), 0, 0, 'ADMIN_CREATED', NOW()),
  (UUID(), 'IC Staff (HN)',             'staff.hn@fpt.edu.vn',         @pwd_hash, @role_staff,   'Staff',  @campus_hn, @dept_ic_hn,  'ACTIVE', NOW(), 0, 0, 'ADMIN_CREATED', NOW()),
  (UUID(), 'Department Lead (HN)',      'dept.leader.hn@fpt.edu.vn',   @pwd_hash, @role_dept,    'Leader', @campus_hn, @dept_aca_hn, 'ACTIVE', NOW(), 0, 0, 'ADMIN_CREATED', NOW()),
  (UUID(), 'Department Personnel (HN)', 'dept.hn@fpt.edu.vn',          @pwd_hash, @role_dept,    'Staff',  @campus_hn, @dept_aca_hn, 'ACTIVE', NOW(), 0, 0, 'ADMIN_CREATED', NOW()),
  (UUID(), 'Support Student',           'student@fpt.edu.vn',          @pwd_hash, @role_student, NULL,     @campus_hn, NULL,         'ACTIVE', NOW(), 0, 0, 'ADMIN_CREATED', NOW()),
  (UUID(), 'External Visitor',          'visitor@example.com',         @pwd_hash, @role_visitor, NULL,     NULL,       NULL,         'ACTIVE', NOW(), 0, 0, 'ADMIN_CREATED', NOW());

-- ── Local-password auth providers for the seeded users ───────────────────────
INSERT IGNORE INTO user_auth_providers
  (auth_provider_id, user_id, provider_type, provider_email, is_enabled, linked_at)
SELECT UUID(), u.user_id, 'LOCAL_PASSWORD', u.email, 1, NOW()
FROM users u
WHERE u.email IN (
    'admin@fpt.edu.vn','ho@fpt.edu.vn','staff.leader.hn@fpt.edu.vn','staff.hn@fpt.edu.vn',
    'dept.leader.hn@fpt.edu.vn','dept.hn@fpt.edu.vn','student@fpt.edu.vn','visitor@example.com')
  AND NOT EXISTS (
    SELECT 1 FROM user_auth_providers ap
    WHERE ap.user_id = u.user_id AND ap.provider_type = 'LOCAL_PASSWORD');
