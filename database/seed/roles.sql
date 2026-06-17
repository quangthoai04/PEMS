-- =====================================================================
-- PEMS — Roles seed
-- The 6 roles (ADMIN, HO, STAFF, DEPT, STUDENT, VISITOR) are already created
-- by database/scripts/pems_full.sql. This file is kept for documentation /
-- re-seeding parity; running it again is a no-op (INSERT IGNORE on role_code).
-- =====================================================================
USE pems_db;

INSERT IGNORE INTO roles (role_id, role_code, name, description) VALUES
  (UUID(), 'ADMIN',   'Admin',       'Quản trị kỹ thuật hệ thống'),
  (UUID(), 'HO',      'Head Office', 'Quản lý cấp Head Office'),
  (UUID(), 'STAFF',   'IC Staff',    'Nhân sự phòng Hợp tác Quốc tế, dùng sub_role Leader/Staff'),
  (UUID(), 'DEPT',    'Department',  'Nhân sự phòng ban khác, dùng sub_role Leader/Staff'),
  (UUID(), 'STUDENT', 'Student',     'Sinh viên hỗ trợ'),
  (UUID(), 'VISITOR', 'Visitor',     'Khách gửi visit request và theo dõi thông tin của mình');
