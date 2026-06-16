-- =====================================================================
-- PEMS v4.5 - NEW BASE MySQL 8.0 Schema
-- Version: 40 tables - HO-final approval flow; UC-based soft delete only; one campus per internal user
--
-- What is fixed in this version:
-- - Added DROP TRIGGER IF EXISTS and DROP TABLE IF EXISTS in dependency order.
-- - Removed generated-column unique tricks that may cause compatibility/FK issues.
-- - Replaced those rules with normal columns + triggers.
-- - Added/kept optimized indexes for login, campus, visit, logistics, audit, API, search.
-- - Fixed MySQL key length issue: files.object_key reduced to VARCHAR(700).
-- - Soft delete is kept only for tables that have explicit Delete/Remove UC in the current UC list.
-- - Kept 2 login portals:
--   + VISITOR portal: no campus, only VISITOR
--   + INTERNAL portal: auto uses user primary campus, only non-VISITOR
-- - Kept LOCAL_PASSWORD + GOOGLE_SSO + FEID from the current phase.
-- - Removed tasks/task_actions; logistics/resource workflow is handled by visit_logistics_items.
-- - Removed user_campuses; every non-VISITOR user has exactly one primary_campus_id.
-- - Updated visit request, host assignment, minutes soft lock, logistics proposal workflow, and news approval fields.
-- - Removed user_campuses; each internal user has exactly one primary_campus_id.
-- - Removed redundant approval helper columns; backend derives approval display data from visit_scope.
-- - Removed redundant visit_request_campuses.assigned_by/assigned_at; approval actor/time already stored in visit_requests + logs.
-- - Locked approval flow:
--   + SINGLE_CAMPUS request tổng chỉ được STAFF_LEADER quyết định.
--   + MULTI_CAMPUS request tổng chỉ được HO quyết định.
--   + Campus không còn duyệt/từ chối instance sau khi request tổng được duyệt.
--   + Sau khi request tổng được duyệt, mỗi campus instance chuyển ASSIGNED và có current_host_user_id.
--   + Host mặc định là Staff Leader của campus; Staff Leader/current host có thể chuyển host cho IC Staff khác.
-- =====================================================================

SET NAMES utf8mb4;
SET SQL_MODE = 'STRICT_TRANS_TABLES,ERROR_FOR_DIVISION_BY_ZERO,NO_ENGINE_SUBSTITUTION';

DROP DATABASE IF EXISTS pems_db;
CREATE DATABASE IF NOT EXISTS pems_db
  CHARACTER SET utf8mb4
  COLLATE utf8mb4_unicode_ci;

USE pems_db;

SET FOREIGN_KEY_CHECKS = 0;

DROP TRIGGER IF EXISTS trg_departments_one_ic_bi;
DROP TRIGGER IF EXISTS trg_departments_one_ic_bu;
DROP TRIGGER IF EXISTS trg_users_validate_bi;
DROP TRIGGER IF EXISTS trg_users_validate_bu;
DROP TRIGGER IF EXISTS trg_auth_providers_validate_bi;
DROP TRIGGER IF EXISTS trg_auth_providers_validate_bu;
DROP TRIGGER IF EXISTS trg_sessions_validate_bi;
DROP TRIGGER IF EXISTS trg_visit_requests_decision_validate_bi;
DROP TRIGGER IF EXISTS trg_visit_requests_decision_validate_bu;
DROP TRIGGER IF EXISTS trg_visit_campuses_assignment_validate_bi;
DROP TRIGGER IF EXISTS trg_visit_campuses_assignment_validate_bu;
DROP TRIGGER IF EXISTS trg_public_contents_scope_bi;
DROP TRIGGER IF EXISTS trg_public_contents_scope_bu;
DROP TRIGGER IF EXISTS trg_api_usage_quotas_scope_bi;
DROP TRIGGER IF EXISTS trg_api_usage_quotas_scope_bu;
DROP TRIGGER IF EXISTS trg_agenda_templates_scope_bi;
DROP TRIGGER IF EXISTS trg_agenda_templates_scope_bu;

DROP TABLE IF EXISTS visit_status_logs;
DROP TABLE IF EXISTS audit_logs;
DROP TABLE IF EXISTS agenda_templates;
DROP TABLE IF EXISTS api_request_logs;
DROP TABLE IF EXISTS api_usage_quotas;
DROP TABLE IF EXISTS api_configurations;
DROP TABLE IF EXISTS calendar_events;
DROP TABLE IF EXISTS notifications;
DROP TABLE IF EXISTS sent_emails;
DROP TABLE IF EXISTS email_templates;
DROP TABLE IF EXISTS photo_face_tags;
DROP TABLE IF EXISTS gallery_images;
DROP TABLE IF EXISTS galleries;
DROP TABLE IF EXISTS public_contents;
DROP TABLE IF EXISTS faqs;
DROP TABLE IF EXISTS news_translations;
DROP TABLE IF EXISTS news;
DROP TABLE IF EXISTS feedbacks;
DROP TABLE IF EXISTS minutes;
DROP TABLE IF EXISTS visit_logistics_items;
DROP TABLE IF EXISTS visit_agendas;
DROP TABLE IF EXISTS visit_participants;
DROP TABLE IF EXISTS visit_guest_members;
DROP TABLE IF EXISTS visit_request_campuses;
DROP TABLE IF EXISTS visit_requests;
DROP TABLE IF EXISTS documents;
DROP TABLE IF EXISTS files;
DROP TABLE IF EXISTS partner_contacts;
DROP TABLE IF EXISTS partners;
DROP TABLE IF EXISTS security_events;
DROP TABLE IF EXISTS login_logs;
DROP TABLE IF EXISTS otp_tokens;
DROP TABLE IF EXISTS user_sessions;
DROP TABLE IF EXISTS user_auth_providers;
DROP TABLE IF EXISTS users;
DROP TABLE IF EXISTS departments;
DROP TABLE IF EXISTS campuses;
DROP TABLE IF EXISTS role_permissions;
DROP TABLE IF EXISTS permissions;
DROP TABLE IF EXISTS roles;

SET FOREIGN_KEY_CHECKS = 1;

-- =====================================================================
-- 1. RBAC
-- =====================================================================

CREATE TABLE roles (
  role_id CHAR(36) NOT NULL,
  role_code VARCHAR(30) NOT NULL COMMENT 'ADMIN, HO, STAFF, DEPT, STUDENT, VISITOR',
  name VARCHAR(100) NOT NULL,
  description VARCHAR(255) NULL,
  status ENUM('ACTIVE','INACTIVE') NOT NULL DEFAULT 'ACTIVE',
  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  deleted_at DATETIME NULL COMMENT 'Soft delete supported by UC-121 Disable/Delete Role',
  deleted_by CHAR(36) NULL COMMENT 'User who soft-deleted this role; no FK here because roles is created before users',
  PRIMARY KEY (role_id),
  UNIQUE KEY uq_roles_code (role_code),
  KEY idx_roles_status_deleted (status, deleted_at),
  CHECK (role_code IN ('ADMIN','HO','STAFF','DEPT','STUDENT','VISITOR'))
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
COMMENT='6 role chính của hệ thống';

CREATE TABLE permissions (
  permission_id CHAR(36) NOT NULL,
  permission_code VARCHAR(100) NOT NULL COMMENT 'Example: UC-017.SUBMIT_VISIT_REQUEST',
  name VARCHAR(150) NOT NULL,
  permission_group VARCHAR(60) NOT NULL,
  description VARCHAR(500) NULL,
  is_system BOOLEAN NOT NULL DEFAULT FALSE,
  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (permission_id),
  UNIQUE KEY uq_permissions_code (permission_code),
  KEY idx_permissions_group (permission_group),
  KEY idx_permissions_group_code (permission_group, permission_code)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
COMMENT='Danh mục quyền theo UC/action';

CREATE TABLE role_permissions (
  role_id CHAR(36) NOT NULL,
  permission_id CHAR(36) NOT NULL,
  permission_level ENUM('F','E','R','O') NOT NULL COMMENT 'F=Full, E=Execute/Edit, R=Read, O=Own',
  granted_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  granted_by CHAR(36) NULL,
  PRIMARY KEY (role_id, permission_id),
  KEY idx_role_permissions_permission (permission_id),
  CONSTRAINT fk_role_permissions_role
    FOREIGN KEY (role_id) REFERENCES roles(role_id)
    ON UPDATE CASCADE ON DELETE CASCADE,
  CONSTRAINT fk_role_permissions_permission
    FOREIGN KEY (permission_id) REFERENCES permissions(permission_id)
    ON UPDATE CASCADE ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
COMMENT='Ma trận phân quyền role-permission';

-- =====================================================================
-- 2. ORGANIZATION
-- =====================================================================

CREATE TABLE campuses (
  campus_id CHAR(36) NOT NULL,
  campus_code VARCHAR(20) NOT NULL COMMENT 'HN, HCM, DN, CT, QN',
  name VARCHAR(150) NOT NULL,
  city VARCHAR(100) NULL,
  address VARCHAR(255) NULL,
  phone VARCHAR(30) NULL,
  email VARCHAR(150) NULL,
  ic_head_user_id CHAR(36) NULL COMMENT 'FK added after users table',
  capacity INT UNSIGNED NULL,
  status ENUM('ACTIVE','INACTIVE') NOT NULL DEFAULT 'ACTIVE',
  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  created_by CHAR(36) NULL,
  updated_at DATETIME NULL DEFAULT NULL ON UPDATE CURRENT_TIMESTAMP,
  updated_by CHAR(36) NULL,
  PRIMARY KEY (campus_id),
  UNIQUE KEY uq_campuses_code (campus_code),
  KEY idx_campuses_status (status),
  KEY idx_campuses_city_status (city, status),
  KEY idx_campuses_ic_head (ic_head_user_id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
COMMENT='Danh mục campus';

CREATE TABLE departments (
  department_id CHAR(36) NOT NULL,
  campus_id CHAR(36) NOT NULL,
  department_code VARCHAR(50) NOT NULL,
  name VARCHAR(150) NOT NULL,
  department_type ENUM('IC','GENERAL') NOT NULL COMMENT 'IC=International Cooperation; GENERAL=other departments',
  head_user_id CHAR(36) NULL COMMENT 'FK added after users table',
  status ENUM('ACTIVE','INACTIVE') NOT NULL DEFAULT 'ACTIVE',
  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  created_by CHAR(36) NULL,
  updated_at DATETIME NULL DEFAULT NULL ON UPDATE CURRENT_TIMESTAMP,
  updated_by CHAR(36) NULL,
  PRIMARY KEY (department_id),
  UNIQUE KEY uq_departments_campus_code (campus_id, department_code),
  UNIQUE KEY uq_departments_campus_name (campus_id, name),
  KEY idx_departments_campus_type (campus_id, department_type),
  KEY idx_departments_status (status),
  KEY idx_departments_head (head_user_id),
  CONSTRAINT fk_departments_campus
    FOREIGN KEY (campus_id) REFERENCES campuses(campus_id)
    ON UPDATE CASCADE ON DELETE RESTRICT
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
COMMENT='Phòng ban theo campus. STAFF thuộc IC, DEPT thuộc GENERAL';

-- =====================================================================
-- 3. USERS + AUTH
-- =====================================================================

CREATE TABLE users (
  user_id CHAR(36) NOT NULL,
  full_name VARCHAR(150) NOT NULL,
  email VARCHAR(150) NOT NULL,
  phone VARCHAR(30) NULL,
  password_hash VARCHAR(255) NULL COMMENT 'Hash for local password. NULL if not set or SSO-only.',
  role_id CHAR(36) NOT NULL,
  sub_role ENUM('Leader','Staff') NULL COMMENT 'Only for STAFF/DEPT',
  primary_campus_id CHAR(36) NULL COMMENT 'Campus duy nhất của user nội bộ. VISITOR phải NULL.',
  department_id CHAR(36) NULL COMMENT 'STAFF = IC department; DEPT = GENERAL department',
  gender ENUM('MALE','FEMALE','OTHER','UNKNOWN') NULL,
  avatar_url VARCHAR(500) NULL,
  student_code VARCHAR(30) NULL,
  fe_id VARCHAR(100) NULL,
  status ENUM('PENDING_EMAIL_VERIFICATION','PENDING_APPROVAL','ACTIVE','INACTIVE','REJECTED','LOCKED') NOT NULL DEFAULT 'PENDING_APPROVAL',
  email_verified_at DATETIME NULL,
  must_set_password BOOLEAN NOT NULL DEFAULT FALSE,
  must_change_password BOOLEAN NOT NULL DEFAULT FALSE,
  failed_login_count INT UNSIGNED NOT NULL DEFAULT 0,
  locked_until DATETIME NULL,
  created_via ENUM('ADMIN_CREATED','VISITOR_FORM','SSO_PROVISIONED') NOT NULL DEFAULT 'ADMIN_CREATED',
  first_login_at DATETIME NULL,
  last_login_at DATETIME NULL,
  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  created_by CHAR(36) NULL,
  updated_at DATETIME NULL DEFAULT NULL ON UPDATE CURRENT_TIMESTAMP,
  updated_by CHAR(36) NULL,
  PRIMARY KEY (user_id),
  UNIQUE KEY uq_users_email (email),
  UNIQUE KEY uq_users_student_code (student_code),
  UNIQUE KEY uq_users_fe_id (fe_id),
  KEY idx_users_role_sub_role (role_id, sub_role),
  KEY idx_users_primary_campus (primary_campus_id),
  KEY idx_users_department (department_id),
  KEY idx_users_status (status),
  KEY idx_users_email_status (email, status),
  KEY idx_users_campus_role_status (primary_campus_id, role_id, status),
  KEY idx_users_department_status (department_id, status),
  KEY idx_users_created_via (created_via),
  KEY idx_users_last_login (last_login_at),
  CONSTRAINT fk_users_role
    FOREIGN KEY (role_id) REFERENCES roles(role_id)
    ON UPDATE CASCADE ON DELETE RESTRICT,
  CONSTRAINT fk_users_primary_campus
    FOREIGN KEY (primary_campus_id) REFERENCES campuses(campus_id)
    ON UPDATE CASCADE ON DELETE RESTRICT,
  CONSTRAINT fk_users_department
    FOREIGN KEY (department_id) REFERENCES departments(department_id)
    ON UPDATE CASCADE ON DELETE RESTRICT
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
COMMENT='Tài khoản chính';

ALTER TABLE campuses
  ADD CONSTRAINT fk_campuses_ic_head
  FOREIGN KEY (ic_head_user_id) REFERENCES users(user_id)
  ON UPDATE CASCADE ON DELETE SET NULL;

ALTER TABLE departments
  ADD CONSTRAINT fk_departments_head
  FOREIGN KEY (head_user_id) REFERENCES users(user_id)
  ON UPDATE CASCADE ON DELETE SET NULL;


CREATE TABLE user_auth_providers (
  auth_provider_id CHAR(36) NOT NULL,
  user_id CHAR(36) NOT NULL,
  provider_type ENUM('LOCAL_PASSWORD','GOOGLE_SSO','FEID') NOT NULL,
  provider_subject VARCHAR(255) NULL COMMENT 'Required for GOOGLE_SSO/FEID',
  provider_email VARCHAR(150) NULL,
  is_enabled BOOLEAN NOT NULL DEFAULT TRUE,
  linked_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  last_used_at DATETIME NULL,
  PRIMARY KEY (auth_provider_id),
  UNIQUE KEY uq_user_auth_provider_type (user_id, provider_type),
  UNIQUE KEY uq_auth_provider_subject (provider_type, provider_subject),
  KEY idx_auth_provider_email (provider_email),
  KEY idx_auth_provider_type_email_enabled (provider_type, provider_email, is_enabled),
  CONSTRAINT fk_auth_providers_user
    FOREIGN KEY (user_id) REFERENCES users(user_id)
    ON UPDATE CASCADE ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
COMMENT='Một user có thể login bằng password, Google SSO, FEID';

CREATE TABLE user_sessions (
  session_id CHAR(36) NOT NULL,
  user_id CHAR(36) NOT NULL,
  login_portal ENUM('VISITOR','INTERNAL') NOT NULL,
  selected_campus_id CHAR(36) NULL COMMENT 'Auto set to users.primary_campus_id for INTERNAL, NULL for VISITOR',
  auth_provider_id CHAR(36) NULL,
  refresh_token_hash VARCHAR(255) NULL COMMENT 'Refresh token hash merged into session',
  refresh_expires_at DATETIME NULL,
  refresh_revoked_at DATETIME NULL,
  ip_address VARCHAR(45) NULL,
  user_agent VARCHAR(500) NULL,
  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  expires_at DATETIME NOT NULL,
  revoked_at DATETIME NULL,
  revoked_by CHAR(36) NULL,
  revoked_reason VARCHAR(255) NULL,
  PRIMARY KEY (session_id),
  UNIQUE KEY uq_sessions_refresh_hash (refresh_token_hash),
  KEY idx_sessions_user_active (user_id, revoked_at, expires_at),
  KEY idx_sessions_portal_campus (login_portal, selected_campus_id),
  KEY idx_sessions_refresh_active (refresh_token_hash, refresh_revoked_at, refresh_expires_at),
  KEY idx_sessions_ip_time (ip_address, created_at),
  CONSTRAINT fk_sessions_user
    FOREIGN KEY (user_id) REFERENCES users(user_id)
    ON UPDATE CASCADE ON DELETE CASCADE,
  CONSTRAINT fk_sessions_selected_campus
    FOREIGN KEY (selected_campus_id) REFERENCES campuses(campus_id)
    ON UPDATE CASCADE ON DELETE RESTRICT,
  CONSTRAINT fk_sessions_auth_provider
    FOREIGN KEY (auth_provider_id) REFERENCES user_auth_providers(auth_provider_id)
    ON UPDATE CASCADE ON DELETE SET NULL,
  CONSTRAINT fk_sessions_revoked_by
    FOREIGN KEY (revoked_by) REFERENCES users(user_id)
    ON UPDATE CASCADE ON DELETE SET NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
COMMENT='Session + refresh token hash';

CREATE TABLE otp_tokens (
  otp_token_id CHAR(36) NOT NULL,
  user_id CHAR(36) NULL,
  email VARCHAR(150) NOT NULL,
  token_type ENUM('OTP_CODE','MAGIC_LINK') NOT NULL DEFAULT 'OTP_CODE',
  purpose ENUM('VISIT_REQUEST_VERIFY','VERIFY_EMAIL','SET_PASSWORD','LOGIN','FORGOT_PASSWORD','CHANGE_SENSITIVE_ACTION') NOT NULL,
  token_hash VARCHAR(255) NOT NULL,
  expires_at DATETIME NOT NULL,
  used_at DATETIME NULL,
  attempt_count INT UNSIGNED NOT NULL DEFAULT 0,
  max_attempts INT UNSIGNED NOT NULL DEFAULT 5,
  resend_count INT UNSIGNED NOT NULL DEFAULT 0,
  ip_address VARCHAR(45) NULL,
  user_agent VARCHAR(500) NULL,
  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (otp_token_id),
  UNIQUE KEY uq_otp_tokens_hash (token_hash),
  KEY idx_otp_email_purpose_time (email, purpose, created_at),
  KEY idx_otp_email_purpose_active (email, purpose, used_at, expires_at),
  KEY idx_otp_user_purpose_active (user_id, purpose, used_at, expires_at),
  KEY idx_otp_ip_time (ip_address, created_at),
  CONSTRAINT fk_otp_tokens_user
    FOREIGN KEY (user_id) REFERENCES users(user_id)
    ON UPDATE CASCADE ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
COMMENT='OTP, magic link, set password token, reset password token';

CREATE TABLE login_logs (
  login_log_id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
  user_id CHAR(36) NULL,
  email VARCHAR(150) NOT NULL,
  login_portal ENUM('VISITOR','INTERNAL') NOT NULL,
  selected_campus_id CHAR(36) NULL,
  provider_type ENUM('LOCAL_PASSWORD','GOOGLE_SSO','FEID') NULL,
  status ENUM('SUCCESS','FAILED','BLOCKED') NOT NULL,
  failure_reason VARCHAR(255) NULL,
  ip_address VARCHAR(45) NULL,
  user_agent VARCHAR(500) NULL,
  session_id CHAR(36) NULL,
  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (login_log_id),
  KEY idx_login_logs_user_time (user_id, created_at),
  KEY idx_login_logs_email_status_time (email, status, created_at),
  KEY idx_login_logs_ip_status_time (ip_address, status, created_at),
  KEY idx_login_logs_portal_campus (login_portal, selected_campus_id),
  KEY idx_login_logs_provider_time (provider_type, created_at),
  CONSTRAINT fk_login_logs_user
    FOREIGN KEY (user_id) REFERENCES users(user_id)
    ON UPDATE CASCADE ON DELETE SET NULL,
  CONSTRAINT fk_login_logs_campus
    FOREIGN KEY (selected_campus_id) REFERENCES campuses(campus_id)
    ON UPDATE CASCADE ON DELETE SET NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
COMMENT='Lịch sử đăng nhập';

CREATE TABLE security_events (
  security_event_id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
  user_id CHAR(36) NULL,
  email VARCHAR(150) NULL,
  event_type VARCHAR(80) NOT NULL COMMENT 'LOGIN_LOCKED, OTP_FAILED, SUSPICIOUS_IP...',
  severity ENUM('LOW','MEDIUM','HIGH','CRITICAL') NOT NULL DEFAULT 'LOW',
  ip_address VARCHAR(45) NULL,
  user_agent VARCHAR(500) NULL,
  metadata JSON NULL,
  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (security_event_id),
  KEY idx_security_user_time (user_id, created_at),
  KEY idx_security_email_time (email, created_at),
  KEY idx_security_type_time (event_type, created_at),
  KEY idx_security_ip_time (ip_address, created_at),
  KEY idx_security_severity_time (severity, created_at),
  CONSTRAINT fk_security_events_user
    FOREIGN KEY (user_id) REFERENCES users(user_id)
    ON UPDATE CASCADE ON DELETE SET NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
COMMENT='Security, abuse, lockout events';

-- =====================================================================
-- 4. PARTNER + FILE
-- =====================================================================

CREATE TABLE partners (
  partner_id CHAR(36) NOT NULL,
  partner_code VARCHAR(50) NULL,
  name VARCHAR(200) NOT NULL,
  short_name VARCHAR(100) NULL,
  country VARCHAR(100) NULL,
  city VARCHAR(100) NULL,
  website_url VARCHAR(500) NULL,
  partner_type ENUM('UNIVERSITY','COMPANY','GOVERNMENT','NGO','OTHER') NOT NULL DEFAULT 'UNIVERSITY',
  cooperation_status ENUM('POTENTIAL','ACTIVE','INACTIVE','BLACKLISTED') NOT NULL DEFAULT 'POTENTIAL',
  description TEXT NULL,
  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  created_by CHAR(36) NULL,
  updated_at DATETIME NULL DEFAULT NULL ON UPDATE CURRENT_TIMESTAMP,
  updated_by CHAR(36) NULL,
  PRIMARY KEY (partner_id),
  UNIQUE KEY uq_partners_code (partner_code),
  KEY idx_partners_country (country),
  KEY idx_partners_status (cooperation_status),
  KEY idx_partners_type_status (partner_type, cooperation_status),
  KEY idx_partners_created_at (created_at),
  FULLTEXT KEY ft_partners_search (name, short_name, description)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
COMMENT='Hồ sơ đối tác';

CREATE TABLE partner_contacts (
  contact_id CHAR(36) NOT NULL,
  partner_id CHAR(36) NOT NULL,
  full_name VARCHAR(150) NOT NULL,
  email VARCHAR(150) NULL,
  phone VARCHAR(50) NULL,
  job_title VARCHAR(150) NULL,
  department_name VARCHAR(150) NULL,
  note TEXT NULL,
  is_primary BOOLEAN NOT NULL DEFAULT FALSE,
  status ENUM('ACTIVE','INACTIVE') NOT NULL DEFAULT 'ACTIVE',
  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  created_by CHAR(36) NULL,
  updated_at DATETIME NULL DEFAULT NULL ON UPDATE CURRENT_TIMESTAMP,
  updated_by CHAR(36) NULL,
  PRIMARY KEY (contact_id),
  UNIQUE KEY uq_partner_contacts_partner_email (partner_id, email),
  KEY idx_partner_contacts_partner (partner_id),
  KEY idx_partner_contacts_email (email),
  KEY idx_partner_contacts_status (status),
  CONSTRAINT fk_partner_contacts_partner
    FOREIGN KEY (partner_id) REFERENCES partners(partner_id)
    ON UPDATE CASCADE ON DELETE RESTRICT
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
COMMENT='Người liên hệ đối tác. OCR final confirmed data saved here.';

CREATE TABLE files (
  file_id CHAR(36) NOT NULL,
  storage_provider ENUM('LOCAL','S3','AZURE','GCS','GOOGLE_DRIVE','OTHER') NOT NULL DEFAULT 'LOCAL',
  bucket_name VARCHAR(150) NULL,
  object_key VARCHAR(700) NOT NULL COMMENT 'Max 700 chars to keep UNIQUE index safe under utf8mb4',
  original_filename VARCHAR(255) NOT NULL,
  mime_type VARCHAR(150) NULL,
  file_size BIGINT UNSIGNED NULL,
  checksum_sha256 CHAR(64) NULL,
  visibility ENUM('PRIVATE','INTERNAL','PUBLIC') NOT NULL DEFAULT 'PRIVATE',
  uploaded_by CHAR(36) NULL,
  uploaded_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (file_id),
  UNIQUE KEY uq_files_object_key (object_key),
  KEY idx_files_uploaded_by (uploaded_by, uploaded_at),
  KEY idx_files_visibility (visibility),
  KEY idx_files_mime_time (mime_type, uploaded_at),
  KEY idx_files_checksum (checksum_sha256),
  CONSTRAINT fk_files_uploaded_by
    FOREIGN KEY (uploaded_by) REFERENCES users(user_id)
    ON UPDATE CASCADE ON DELETE SET NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
COMMENT='File metadata only. Binary file is stored outside DB.';

CREATE TABLE documents (
  document_id CHAR(36) NOT NULL,
  file_id CHAR(36) NOT NULL,
  owner_type ENUM('GENERAL','VISIT','PARTNER','MINUTES','NEWS','LOGISTICS','REPORT') NOT NULL DEFAULT 'GENERAL',
  owner_id CHAR(36) NULL,
  campus_id CHAR(36) NULL,
  title VARCHAR(255) NOT NULL,
  description TEXT NULL,
  document_category VARCHAR(100) NULL,
  status ENUM('DRAFT','PUBLISHED','ARCHIVED') NOT NULL DEFAULT 'DRAFT',
  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  created_by CHAR(36) NULL,
  updated_at DATETIME NULL DEFAULT NULL ON UPDATE CURRENT_TIMESTAMP,
  updated_by CHAR(36) NULL,
  PRIMARY KEY (document_id),
  KEY idx_documents_owner (owner_type, owner_id),
  KEY idx_documents_campus_status (campus_id, status),
  KEY idx_documents_category_status (document_category, status),
  KEY idx_documents_created_by_time (created_by, created_at),
  FULLTEXT KEY ft_documents_search (title, description),
  CONSTRAINT fk_documents_file
    FOREIGN KEY (file_id) REFERENCES files(file_id)
    ON UPDATE CASCADE ON DELETE RESTRICT,
  CONSTRAINT fk_documents_campus
    FOREIGN KEY (campus_id) REFERENCES campuses(campus_id)
    ON UPDATE CASCADE ON DELETE SET NULL,
  CONSTRAINT fk_documents_created_by
    FOREIGN KEY (created_by) REFERENCES users(user_id)
    ON UPDATE CASCADE ON DELETE SET NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
COMMENT='Tài liệu nghiệp vụ. partner_documents/reports/logistics documents merged by owner_type.';

-- =====================================================================
-- 5. VISIT / DELEGATION
-- =====================================================================
-- Approval flow locked in v4.3:
-- SINGLE_CAMPUS: Staff Leader of the selected campus approves/rejects the main visit request.
-- MULTI_CAMPUS: HO approves/rejects the main visit request.
-- Campus instances do NOT have their own approve/reject step anymore.
-- After the main request is approved, backend assigns each campus instance to its Staff Leader by setting current_host_user_id and status='ASSIGNED'.
-- Staff Leader/current host may transfer host to another IC Staff in the same campus.

CREATE TABLE visit_requests (
  visit_request_id CHAR(36) NOT NULL,
  request_code VARCHAR(50) NOT NULL,
  visitor_user_id CHAR(36) NOT NULL COMMENT 'Visitor user/account created or linked for the registrant',
  partner_id CHAR(36) NULL,

  -- 1. Registrant information from the Campus Visit form
  registrant_full_name VARCHAR(150) NOT NULL COMMENT 'Họ và tên người đăng ký',
  registrant_nationality VARCHAR(100) NULL COMMENT 'Quốc tịch người đăng ký',
  registrant_organization VARCHAR(200) NOT NULL COMMENT 'Đơn vị công tác người đăng ký',
  registrant_job_title VARCHAR(150) NULL COMMENT 'Chức danh/phòng ban người đăng ký',
  registrant_phone VARCHAR(50) NULL COMMENT 'SĐT người đăng ký',
  registrant_email VARCHAR(150) NOT NULL COMMENT 'Email người đăng ký',

  -- 2. Delegation information
  delegation_name VARCHAR(200) NOT NULL COMMENT 'Tên đoàn khách',
  visit_scope ENUM('SINGLE_CAMPUS','MULTI_CAMPUS') NOT NULL DEFAULT 'SINGLE_CAMPUS'
    COMMENT 'SINGLE_CAMPUS: Staff Leader duyệt request tổng; MULTI_CAMPUS: HO duyệt request tổng. Frontend/backend suy ra người duyệt từ cột này.',
  purpose TEXT NOT NULL COMMENT 'Mục đích thăm FPTU',
  working_content TEXT NULL COMMENT 'Nội dung làm việc tại FPTU',
  expected_guest_count INT UNSIGNED NOT NULL DEFAULT 1 COMMENT 'Số khách dự kiến; có thể đồng bộ từ danh sách khách',

  support_team_json JSON NULL COMMENT 'Danh sách team hỗ trợ khách từ phía đoàn/đơn vị gửi',
  contact_person_json JSON NULL COMMENT 'Thông tin đầu mối liên hệ: full_name, organization, phone, email',

  working_language ENUM('VI','EN','OTHER') NOT NULL DEFAULT 'EN' COMMENT 'Ngôn ngữ sử dụng trong visit',
  interpreter_note TEXT NULL COMMENT 'Ghi chú nếu ngôn ngữ khác VI/EN và đầu mối cần tự bố trí phiên dịch',
  transportation_note TEXT NULL COMMENT 'Nhận diện phương tiện di chuyển tới FPTU',
  note_to_fptu TEXT NULL COMMENT 'Ghi chú cho FPTU',

  status ENUM('PENDING_EMAIL_VERIFICATION','PENDING_APPROVAL','REJECTED','APPROVED','CANCELLED') NOT NULL DEFAULT 'PENDING_EMAIL_VERIFICATION',
  submitted_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  email_verified_at DATETIME NULL,

  decided_by CHAR(36) NULL COMMENT 'Người approve/reject/cancel request tổng',
  decided_at DATETIME NULL COMMENT 'Thời điểm xử lý request tổng',
  decision_actor_role ENUM('HO','STAFF_LEADER','SYSTEM') NULL COMMENT 'Vai trò người xử lý tại thời điểm quyết định',
  decision_note TEXT NULL COMMENT 'Lý do/ghi chú khi approve, reject hoặc cancel',

  row_version INT UNSIGNED NOT NULL DEFAULT 0 COMMENT 'Optimistic concurrency token',

  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  created_by CHAR(36) NULL,
  updated_at DATETIME NULL DEFAULT NULL ON UPDATE CURRENT_TIMESTAMP,
  updated_by CHAR(36) NULL,

  PRIMARY KEY (visit_request_id),
  UNIQUE KEY uq_visit_requests_code (request_code),
  KEY idx_visit_requests_visitor (visitor_user_id),
  KEY idx_visit_requests_partner (partner_id),
  KEY idx_visit_requests_status_submitted (status, submitted_at),
  KEY idx_visit_requests_registrant_email (registrant_email),
  KEY idx_visit_requests_scope_status (visit_scope, status),
  KEY idx_visit_requests_decision (decided_by, decided_at),
  KEY idx_visit_requests_decision_role (decision_actor_role, decided_at),

  CHECK (expected_guest_count >= 1),
  CHECK (
    decision_actor_role IS NULL
    OR status NOT IN ('APPROVED','REJECTED','CANCELLED')
    OR (
      visit_scope = 'SINGLE_CAMPUS'
      AND decision_actor_role IN ('STAFF_LEADER','SYSTEM')
    )
    OR (
      visit_scope = 'MULTI_CAMPUS'
      AND decision_actor_role IN ('HO','SYSTEM')
    )
  ),

  CONSTRAINT fk_visit_requests_visitor_user
    FOREIGN KEY (visitor_user_id) REFERENCES users(user_id)
    ON UPDATE CASCADE ON DELETE RESTRICT,
  CONSTRAINT fk_visit_requests_partner
    FOREIGN KEY (partner_id) REFERENCES partners(partner_id)
    ON UPDATE CASCADE ON DELETE SET NULL,
  CONSTRAINT fk_visit_requests_decided_by
    FOREIGN KEY (decided_by) REFERENCES users(user_id)
    ON UPDATE CASCADE ON DELETE SET NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
COMMENT='Đơn đăng ký tham quan. Nội dung không được sửa sau khi chuyển sang PENDING_APPROVAL; thời gian/campus lưu ở visit_request_campuses.';

CREATE TABLE visit_request_campuses (
  visit_instance_id CHAR(36) NOT NULL,
  visit_request_id CHAR(36) NOT NULL,
  campus_id CHAR(36) NOT NULL,
  instance_code VARCHAR(60) NULL,

  planned_start_at DATETIME NOT NULL COMMENT 'Ngày giờ bắt đầu dự kiến tại campus',
  planned_end_at DATETIME NOT NULL COMMENT 'Ngày giờ kết thúc dự kiến tại campus',
  actual_start_at DATETIME NULL COMMENT 'Ngày giờ bắt đầu thực tế',
  actual_end_at DATETIME NULL COMMENT 'Ngày giờ kết thúc thực tế',

  status ENUM(
    'WAITING_REQUEST_APPROVAL',
    'ASSIGNED',
    'BEFORE_VISIT',
    'DURING_VISIT',
    'AFTER_VISIT',
    'CLOSED',
    'CANCELLED'
  ) NOT NULL DEFAULT 'WAITING_REQUEST_APPROVAL',

  current_host_user_id CHAR(36) NULL
    COMMENT 'Host hiện tại chịu trách nhiệm campus instance. Mặc định là Staff Leader của campus sau khi request tổng được duyệt; có thể chuyển cho IC Staff khác cùng campus',


  host_transferred_by CHAR(36) NULL COMMENT 'Người chuyển host gần nhất',
  host_transferred_at DATETIME NULL COMMENT 'Thời điểm chuyển host gần nhất',
  host_transfer_note TEXT NULL COMMENT 'Ghi chú/lý do chuyển host gần nhất',

  closed_by CHAR(36) NULL,
  closed_at DATETIME NULL,
  close_note TEXT NULL,

  row_version INT UNSIGNED NOT NULL DEFAULT 0 COMMENT 'Optimistic concurrency token',

  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  created_by CHAR(36) NULL,
  updated_at DATETIME NULL DEFAULT NULL ON UPDATE CURRENT_TIMESTAMP,
  updated_by CHAR(36) NULL,

  PRIMARY KEY (visit_instance_id),
  UNIQUE KEY uq_visit_instance_request_campus (visit_request_id, campus_id),
  UNIQUE KEY uq_visit_instance_code (instance_code),
  KEY idx_visit_instances_campus_status_time (campus_id, status, planned_start_at),
  KEY idx_visit_instances_request (visit_request_id),
  KEY idx_visit_instances_status_time (status, planned_start_at),
  KEY idx_visit_instances_current_host (current_host_user_id, status),
  KEY idx_visit_instances_host_transfer (host_transferred_by, host_transferred_at),

  CHECK (planned_end_at > planned_start_at),
  CHECK (actual_end_at IS NULL OR actual_start_at IS NULL OR actual_end_at > actual_start_at),

  CONSTRAINT fk_visit_instances_request
    FOREIGN KEY (visit_request_id) REFERENCES visit_requests(visit_request_id)
    ON UPDATE CASCADE ON DELETE RESTRICT,
  CONSTRAINT fk_visit_instances_campus
    FOREIGN KEY (campus_id) REFERENCES campuses(campus_id)
    ON UPDATE CASCADE ON DELETE RESTRICT,
  CONSTRAINT fk_visit_instances_current_host
    FOREIGN KEY (current_host_user_id) REFERENCES users(user_id)
    ON UPDATE CASCADE ON DELETE SET NULL,
  CONSTRAINT fk_visit_instances_host_transferred_by
    FOREIGN KEY (host_transferred_by) REFERENCES users(user_id)
    ON UPDATE CASCADE ON DELETE SET NULL,
  CONSTRAINT fk_visit_instances_closed_by
    FOREIGN KEY (closed_by) REFERENCES users(user_id)
    ON UPDATE CASCADE ON DELETE SET NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
COMMENT='Mỗi campus trong request có một instance riêng. Campus không duyệt/từ chối riêng; sau khi request tổng được duyệt, backend gán current_host_user_id và chuyển status=ASSIGNED.';

CREATE TABLE visit_guest_members (
  guest_member_id CHAR(36) NOT NULL,
  visit_request_id CHAR(36) NOT NULL,
  full_name VARCHAR(150) NOT NULL,
  organization VARCHAR(200) NULL,
  job_title VARCHAR(150) NULL,
  nationality VARCHAR(100) NULL,
  email VARCHAR(150) NULL,
  phone VARCHAR(50) NULL,
  is_representative BOOLEAN NOT NULL DEFAULT FALSE,
  note TEXT NULL,
  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  created_by CHAR(36) NULL,
  updated_at DATETIME NULL DEFAULT NULL ON UPDATE CURRENT_TIMESTAMP,
  updated_by CHAR(36) NULL,
  PRIMARY KEY (guest_member_id),
  KEY idx_guest_members_request (visit_request_id),
  KEY idx_guest_members_email (email),
  KEY idx_guest_members_representative (visit_request_id, is_representative),
  CONSTRAINT fk_guest_members_request
    FOREIGN KEY (visit_request_id) REFERENCES visit_requests(visit_request_id)
    ON UPDATE CASCADE ON DELETE RESTRICT
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
COMMENT='Danh sách từng người trong đoàn khách. Không lưu consent hình ảnh vì form đã bỏ phần xác nhận sử dụng hình ảnh/thông tin.';

CREATE TABLE visit_participants (
  participant_id CHAR(36) NOT NULL,
  visit_instance_id CHAR(36) NOT NULL,
  user_id CHAR(36) NOT NULL,
  participant_role ENUM('IC_HOST','IC_SUPPORT','DEPT_SUPPORT','STUDENT_BUDDY','MEDIA','INTERPRETER','OTHER') NOT NULL DEFAULT 'OTHER',
  is_host BOOLEAN NOT NULL DEFAULT FALSE,
  status ENUM('INVITED','ACCEPTED','DECLINED','ASSIGNED','REMOVED') NOT NULL DEFAULT 'INVITED',
  invited_by CHAR(36) NULL,
  invited_at DATETIME NULL,
  responded_at DATETIME NULL,
  assigned_by CHAR(36) NULL,
  assigned_at DATETIME NULL,
  note TEXT NULL,
  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  created_by CHAR(36) NULL,
  updated_at DATETIME NULL DEFAULT NULL ON UPDATE CURRENT_TIMESTAMP,
  updated_by CHAR(36) NULL,
  PRIMARY KEY (participant_id),
  UNIQUE KEY uq_visit_participants_user (visit_instance_id, user_id),
  KEY idx_visit_participants_one_host_lookup (visit_instance_id, is_host),
  KEY idx_visit_participants_user_status (user_id, status),
  KEY idx_visit_participants_instance (visit_instance_id),
  KEY idx_visit_participants_role_status (participant_role, status),
  CONSTRAINT fk_visit_participants_instance
    FOREIGN KEY (visit_instance_id) REFERENCES visit_request_campuses(visit_instance_id)
    ON UPDATE CASCADE ON DELETE RESTRICT,
  CONSTRAINT fk_visit_participants_user
    FOREIGN KEY (user_id) REFERENCES users(user_id)
    ON UPDATE CASCADE ON DELETE RESTRICT,
  CONSTRAINT fk_visit_participants_invited_by
    FOREIGN KEY (invited_by) REFERENCES users(user_id)
    ON UPDATE CASCADE ON DELETE SET NULL,
  CONSTRAINT fk_visit_participants_assigned_by
    FOREIGN KEY (assigned_by) REFERENCES users(user_id)
    ON UPDATE CASCADE ON DELETE SET NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
COMMENT='Người nội bộ tham gia. HOST lưu bằng is_host. One-host rule should be enforced by backend/audit for portability.';

CREATE TABLE visit_agendas (
  agenda_id CHAR(36) NOT NULL,
  visit_instance_id CHAR(36) NOT NULL,
  sequence_order INT UNSIGNED NOT NULL,
  title VARCHAR(255) NOT NULL,
  description TEXT NULL,
  start_time DATETIME NOT NULL,
  end_time DATETIME NULL,
  location VARCHAR(255) NULL,
  responsible_user_id CHAR(36) NULL,
  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  created_by CHAR(36) NULL,
  updated_at DATETIME NULL DEFAULT NULL ON UPDATE CURRENT_TIMESTAMP,
  updated_by CHAR(36) NULL,
  PRIMARY KEY (agenda_id),
  UNIQUE KEY uq_visit_agendas_order (visit_instance_id, sequence_order),
  KEY idx_visit_agendas_time (visit_instance_id, start_time),
  KEY idx_visit_agendas_responsible (responsible_user_id, start_time),
  CONSTRAINT fk_visit_agendas_instance
    FOREIGN KEY (visit_instance_id) REFERENCES visit_request_campuses(visit_instance_id)
    ON UPDATE CASCADE ON DELETE RESTRICT,
  CONSTRAINT fk_visit_agendas_responsible_user
    FOREIGN KEY (responsible_user_id) REFERENCES users(user_id)
    ON UPDATE CASCADE ON DELETE SET NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
COMMENT='Lịch trình tiếp khách';

CREATE TABLE visit_logistics_items (
  logistics_item_id CHAR(36) NOT NULL,
  visit_instance_id CHAR(36) NOT NULL,

  item_type ENUM('ROOM','TRANSPORT','MEAL','EQUIPMENT','BANNER','LED','OTHER') NOT NULL,
  title VARCHAR(255) NOT NULL,
  description TEXT NULL COMMENT 'Nội dung chi tiết công việc gốc',
  quantity INT UNSIGNED NULL COMMENT 'Số lượng yêu cầu gốc',

  usage_start_at DATETIME NULL COMMENT 'Thời gian bắt đầu sử dụng resource',
  usage_end_at DATETIME NULL COMMENT 'Thời gian kết thúc sử dụng resource',

  status ENUM(
    'PLANNED',
    'REQUESTED',
    'CHANGE_PROPOSED',
    'RECEIVED',
    'ASSIGNED',
    'ACCEPTED',
    'IN_PROGRESS',
    'READY',
    'DONE',
    'REJECTED',
    'CANCELLED'
  ) NOT NULL DEFAULT 'PLANNED',

  priority ENUM('LOW','MEDIUM','HIGH','URGENT') NOT NULL DEFAULT 'MEDIUM',

  requested_by CHAR(36) NULL COMMENT 'Người gửi yêu cầu hậu cần/resource',
  requested_to_department_id CHAR(36) NULL COMMENT 'Phòng ban được yêu cầu xử lý',
  requested_at DATETIME NULL COMMENT 'Thời điểm gửi yêu cầu',

  received_by CHAR(36) NULL COMMENT 'Trưởng phòng/người tiếp nhận yêu cầu',
  received_at DATETIME NULL COMMENT 'Thời điểm tiếp nhận yêu cầu',

  assigned_to_user_id CHAR(36) NULL COMMENT 'Nhân viên được giao xử lý chính',
  assigned_by CHAR(36) NULL COMMENT 'Người phân công',
  assigned_at DATETIME NULL COMMENT 'Thời điểm phân công',

  assignee_accepted_at DATETIME NULL COMMENT 'Thời điểm nhân viên xác nhận nhận nhiệm vụ',
  assignee_response_note TEXT NULL COMMENT 'Ghi chú khi nhân viên nhận/từ chối nếu có',

  due_at DATETIME NULL COMMENT 'Deadline hoàn thành hạng mục',
  completed_at DATETIME NULL COMMENT 'Thời điểm hoàn thành',

  proposed_by CHAR(36) NULL COMMENT 'Người gửi đề xuất thay đổi',
  proposed_at DATETIME NULL COMMENT 'Thời điểm gửi đề xuất thay đổi',
  proposed_quantity INT UNSIGNED NULL COMMENT 'Số lượng được đề xuất thay đổi',
  proposed_usage_start_at DATETIME NULL COMMENT 'Thời gian bắt đầu sử dụng được đề xuất',
  proposed_usage_end_at DATETIME NULL COMMENT 'Thời gian kết thúc sử dụng được đề xuất',
  proposed_description TEXT NULL COMMENT 'Nội dung chi tiết công việc được đề xuất thay đổi',
  proposal_note TEXT NULL COMMENT 'Lý do/ghi chú đề xuất thay đổi',

  proposal_responded_by CHAR(36) NULL COMMENT 'Người xác nhận/từ chối đề xuất',
  proposal_responded_at DATETIME NULL COMMENT 'Thời điểm xác nhận/từ chối đề xuất',
  proposal_response ENUM('ACCEPTED','REJECTED') NULL COMMENT 'Kết quả phản hồi đề xuất',
  proposal_response_note TEXT NULL COMMENT 'Ghi chú phản hồi đề xuất',

  decision_note TEXT NULL COMMENT 'Lý do reject/cancel hoặc ghi chú xử lý',

  row_version INT UNSIGNED NOT NULL DEFAULT 0 COMMENT 'Optimistic concurrency token',

  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  created_by CHAR(36) NULL,
  updated_at DATETIME NULL DEFAULT NULL ON UPDATE CURRENT_TIMESTAMP,
  updated_by CHAR(36) NULL,

  PRIMARY KEY (logistics_item_id),

  KEY idx_logistics_instance_status (visit_instance_id, status),
  KEY idx_logistics_item_status (item_type, status),
  KEY idx_logistics_department_status (requested_to_department_id, status),
  KEY idx_logistics_assignee_status (assigned_to_user_id, status),
  KEY idx_logistics_requested_by_time (requested_by, requested_at),
  KEY idx_logistics_received_by_time (received_by, received_at),
  KEY idx_logistics_usage_time (usage_start_at, usage_end_at),
  KEY idx_logistics_due (due_at),
  KEY idx_logistics_priority_due (priority, due_at),
  KEY idx_logistics_proposed_by_time (proposed_by, proposed_at),

  CHECK (quantity IS NULL OR quantity >= 1),
  CHECK (usage_end_at IS NULL OR usage_start_at IS NULL OR usage_end_at > usage_start_at),
  CHECK (proposed_quantity IS NULL OR proposed_quantity >= 1),
  CHECK (proposed_usage_end_at IS NULL OR proposed_usage_start_at IS NULL OR proposed_usage_end_at > proposed_usage_start_at),

  CONSTRAINT fk_logistics_instance
    FOREIGN KEY (visit_instance_id) REFERENCES visit_request_campuses(visit_instance_id)
    ON UPDATE CASCADE ON DELETE RESTRICT,
  CONSTRAINT fk_logistics_requested_by
    FOREIGN KEY (requested_by) REFERENCES users(user_id)
    ON UPDATE CASCADE ON DELETE SET NULL,
  CONSTRAINT fk_logistics_requested_to_department
    FOREIGN KEY (requested_to_department_id) REFERENCES departments(department_id)
    ON UPDATE CASCADE ON DELETE SET NULL,
  CONSTRAINT fk_logistics_received_by
    FOREIGN KEY (received_by) REFERENCES users(user_id)
    ON UPDATE CASCADE ON DELETE SET NULL,
  CONSTRAINT fk_logistics_assigned_to
    FOREIGN KEY (assigned_to_user_id) REFERENCES users(user_id)
    ON UPDATE CASCADE ON DELETE SET NULL,
  CONSTRAINT fk_logistics_assigned_by
    FOREIGN KEY (assigned_by) REFERENCES users(user_id)
    ON UPDATE CASCADE ON DELETE SET NULL,
  CONSTRAINT fk_logistics_proposed_by
    FOREIGN KEY (proposed_by) REFERENCES users(user_id)
    ON UPDATE CASCADE ON DELETE SET NULL,
  CONSTRAINT fk_logistics_proposal_responded_by
    FOREIGN KEY (proposal_responded_by) REFERENCES users(user_id)
    ON UPDATE CASCADE ON DELETE SET NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
COMMENT='Yêu cầu hậu cần/resource cho visit: gửi yêu cầu, đề xuất thay đổi, tiếp nhận, phân công, xác nhận và hoàn thành. Thay thế tasks cho logistics/resource.';

-- =====================================================================
-- 6. MINUTES + FEEDBACK
-- =====================================================================

CREATE TABLE minutes (
  minutes_id CHAR(36) NOT NULL,
  visit_instance_id CHAR(36) NOT NULL,
  title VARCHAR(255) NOT NULL,
  content LONGTEXT NULL,
  participants_json JSON NULL COMMENT 'Danh sách người tham gia trong biên bản',
  attachments_json JSON NULL COMMENT 'File/ảnh/tài liệu đính kèm biên bản',
  action_items_json JSON NULL COMMENT 'Các đầu việc ghi nhận trong biên bản; nếu cần theo dõi riêng có thể xử lý ở module sau',

  status ENUM('DRAFT','FINAL','ARCHIVED') NOT NULL DEFAULT 'DRAFT',

  finalized_by CHAR(36) NULL COMMENT 'Người chốt biên bản',
  finalized_at DATETIME NULL COMMENT 'Thời điểm chốt biên bản',

  editing_by CHAR(36) NULL COMMENT 'Người đang giữ quyền sửa biên bản',
  editing_started_at DATETIME NULL COMMENT 'Thời điểm bắt đầu sửa',
  editing_until DATETIME NULL COMMENT 'Thời điểm hết hạn khóa sửa',
  edit_lock_token CHAR(36) NULL COMMENT 'Token phiên sửa, tránh mở khóa nhầm',

  row_version INT UNSIGNED NOT NULL DEFAULT 0 COMMENT 'Optimistic concurrency token',

  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  created_by CHAR(36) NULL,
  updated_at DATETIME NULL DEFAULT NULL ON UPDATE CURRENT_TIMESTAMP,
  updated_by CHAR(36) NULL,

  PRIMARY KEY (minutes_id),
  KEY idx_minutes_visit_status (visit_instance_id, status),
  KEY idx_minutes_created_by_time (created_by, created_at),
  KEY idx_minutes_finalized_by_time (finalized_by, finalized_at),
  KEY idx_minutes_editing (editing_by, editing_until),
  KEY idx_minutes_version (minutes_id, row_version),
  FULLTEXT KEY ft_minutes_search (title, content),

  CONSTRAINT fk_minutes_visit
    FOREIGN KEY (visit_instance_id) REFERENCES visit_request_campuses(visit_instance_id)
    ON UPDATE CASCADE ON DELETE RESTRICT,
  CONSTRAINT fk_minutes_created_by
    FOREIGN KEY (created_by) REFERENCES users(user_id)
    ON UPDATE CASCADE ON DELETE SET NULL,
  CONSTRAINT fk_minutes_finalized_by
    FOREIGN KEY (finalized_by) REFERENCES users(user_id)
    ON UPDATE CASCADE ON DELETE SET NULL,
  CONSTRAINT fk_minutes_editing_by
    FOREIGN KEY (editing_by) REFERENCES users(user_id)
    ON UPDATE CASCADE ON DELETE SET NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
COMMENT='Biên bản chuyến thăm. Không có duyệt; dùng draft/final/archive và khóa sửa mềm để một thời điểm chỉ một người sửa.';

CREATE TABLE feedbacks (
  feedback_id CHAR(36) NOT NULL,
  visit_request_id CHAR(36) NOT NULL,
  visit_instance_id CHAR(36) NULL,
  submitted_by_user_id CHAR(36) NULL,
  guest_member_id CHAR(36) NULL,
  rating TINYINT UNSIGNED NULL,
  comment TEXT NULL,
  answers_json JSON NULL COMMENT 'Merged feedback_items table',
  rating_details_json JSON NULL,
  status ENUM('SUBMITTED','REVIEWED','ARCHIVED') NOT NULL DEFAULT 'SUBMITTED',
  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  reviewed_by CHAR(36) NULL,
  reviewed_at DATETIME NULL,
  PRIMARY KEY (feedback_id),
  KEY idx_feedbacks_request (visit_request_id),
  KEY idx_feedbacks_instance (visit_instance_id),
  KEY idx_feedbacks_user (submitted_by_user_id),
  KEY idx_feedbacks_status_time (status, created_at),
  CHECK (rating IS NULL OR rating BETWEEN 1 AND 5),
  CONSTRAINT fk_feedbacks_request
    FOREIGN KEY (visit_request_id) REFERENCES visit_requests(visit_request_id)
    ON UPDATE CASCADE ON DELETE RESTRICT,
  CONSTRAINT fk_feedbacks_instance
    FOREIGN KEY (visit_instance_id) REFERENCES visit_request_campuses(visit_instance_id)
    ON UPDATE CASCADE ON DELETE SET NULL,
  CONSTRAINT fk_feedbacks_user
    FOREIGN KEY (submitted_by_user_id) REFERENCES users(user_id)
    ON UPDATE CASCADE ON DELETE SET NULL,
  CONSTRAINT fk_feedbacks_guest
    FOREIGN KEY (guest_member_id) REFERENCES visit_guest_members(guest_member_id)
    ON UPDATE CASCADE ON DELETE SET NULL,
  CONSTRAINT fk_feedbacks_reviewed_by
    FOREIGN KEY (reviewed_by) REFERENCES users(user_id)
    ON UPDATE CASCADE ON DELETE SET NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
COMMENT='Feedback. Detailed answers merged into answers_json.';

-- =====================================================================
-- 7. PUBLIC CONTENT
-- =====================================================================

CREATE TABLE news (
  news_id CHAR(36) NOT NULL,
  campus_id CHAR(36) NULL,
  author_user_id CHAR(36) NOT NULL,
  cover_file_id CHAR(36) NULL,
  status ENUM('DRAFT','PENDING_APPROVAL','REJECTED','APPROVED','PUBLISHED','HIDDEN','ARCHIVED') NOT NULL DEFAULT 'DRAFT',
  published_at DATETIME NULL,

  decided_by CHAR(36) NULL COMMENT 'Người approve/reject bài viết',
  decided_at DATETIME NULL COMMENT 'Thời điểm approve/reject bài viết',
  decision_note TEXT NULL COMMENT 'Lý do reject hoặc ghi chú duyệt',

  is_featured BOOLEAN NOT NULL DEFAULT FALSE,
  row_version INT UNSIGNED NOT NULL DEFAULT 0 COMMENT 'Optimistic concurrency token',

  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  created_by CHAR(36) NULL,
  updated_at DATETIME NULL DEFAULT NULL ON UPDATE CURRENT_TIMESTAMP,
  updated_by CHAR(36) NULL,

  PRIMARY KEY (news_id),
  KEY idx_news_public (status, campus_id, published_at),
  KEY idx_news_author (author_user_id),
  KEY idx_news_featured (is_featured, status, published_at),
  KEY idx_news_decision (decided_by, decided_at),

  CONSTRAINT fk_news_campus
    FOREIGN KEY (campus_id) REFERENCES campuses(campus_id)
    ON UPDATE CASCADE ON DELETE SET NULL,
  CONSTRAINT fk_news_author
    FOREIGN KEY (author_user_id) REFERENCES users(user_id)
    ON UPDATE CASCADE ON DELETE RESTRICT,
  CONSTRAINT fk_news_cover_file
    FOREIGN KEY (cover_file_id) REFERENCES files(file_id)
    ON UPDATE CASCADE ON DELETE SET NULL,
  CONSTRAINT fk_news_decided_by
    FOREIGN KEY (decided_by) REFERENCES users(user_id)
    ON UPDATE CASCADE ON DELETE SET NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
COMMENT='News metadata. Có quy trình duyệt bài bằng decided_by/decided_at/decision_note. Long multilingual body stored in news_translations.';

CREATE TABLE news_translations (
  news_translation_id CHAR(36) NOT NULL,
  news_id CHAR(36) NOT NULL,
  language_code ENUM('vi','en','zh','ja','ko') NOT NULL DEFAULT 'vi',
  title VARCHAR(255) NOT NULL,
  slug VARCHAR(255) NOT NULL,
  summary TEXT NULL,
  body LONGTEXT NULL,
  seo_title VARCHAR(255) NULL,
  seo_description VARCHAR(500) NULL,
  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  updated_at DATETIME NULL DEFAULT NULL ON UPDATE CURRENT_TIMESTAMP,
  PRIMARY KEY (news_translation_id),
  UNIQUE KEY uq_news_translation_lang (news_id, language_code),
  UNIQUE KEY uq_news_translation_slug_lang (slug, language_code),
  KEY idx_news_translations_lang (language_code),
  FULLTEXT KEY ft_news_translations_search (title, summary, body),
  CONSTRAINT fk_news_translations_news
    FOREIGN KEY (news_id) REFERENCES news(news_id)
    ON UPDATE CASCADE ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
COMMENT='Bản dịch tin tức. Kept separate for search/SEO.';

CREATE TABLE faqs (
  faq_id CHAR(36) NOT NULL,
  category VARCHAR(100) NULL,
  question VARCHAR(500) NOT NULL COMMENT 'Câu hỏi FAQ, không còn dùng bản dịch đa ngôn ngữ',
  answer TEXT NOT NULL COMMENT 'Câu trả lời FAQ, không còn dùng bản dịch đa ngôn ngữ',
  display_order INT UNSIGNED NOT NULL DEFAULT 0,
  status ENUM('DRAFT','PUBLISHED','HIDDEN','ARCHIVED') NOT NULL DEFAULT 'DRAFT',
  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  created_by CHAR(36) NULL,
  updated_at DATETIME NULL DEFAULT NULL ON UPDATE CURRENT_TIMESTAMP,
  updated_by CHAR(36) NULL,
  PRIMARY KEY (faq_id),
  KEY idx_faqs_status_order (status, display_order),
  KEY idx_faqs_category_status (category, status),
  FULLTEXT KEY ft_faqs_search (question, answer)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
COMMENT='FAQ một ngôn ngữ, không dùng bảng dịch riêng';

CREATE TABLE public_contents (
  public_content_id CHAR(36) NOT NULL,
  block_key VARCHAR(100) NOT NULL COMMENT 'HOME_HERO, CONTACT_INFO, POLICY_TERMS...',
  campus_id CHAR(36) NULL,
  campus_scope_key VARCHAR(36) NOT NULL DEFAULT 'GLOBAL',
  block_type ENUM('HOME','CONTACT','POLICY','TERMS','ABOUT','CUSTOM') NOT NULL DEFAULT 'CUSTOM',
  status ENUM('DRAFT','PUBLISHED','HIDDEN','ARCHIVED') NOT NULL DEFAULT 'DRAFT',
  display_order INT UNSIGNED NOT NULL DEFAULT 0,
  translations_json JSON NOT NULL,
  metadata_json JSON NULL COMMENT 'Buttons, links, layout config',
  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  created_by CHAR(36) NULL,
  updated_at DATETIME NULL DEFAULT NULL ON UPDATE CURRENT_TIMESTAMP,
  updated_by CHAR(36) NULL,
  PRIMARY KEY (public_content_id),
  UNIQUE KEY uq_public_contents_key_scope (block_key, campus_scope_key),
  KEY idx_public_contents_status_order (status, display_order),
  KEY idx_public_contents_type_status (block_type, status),
  CONSTRAINT fk_public_contents_campus
    FOREIGN KEY (campus_id) REFERENCES campuses(campus_id)
    ON UPDATE CASCADE ON DELETE SET NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
COMMENT='Public pages/blocks with translations_json';

-- =====================================================================
-- 8. GALLERY + FACE TAGGING
-- =====================================================================

CREATE TABLE galleries (
  gallery_id CHAR(36) NOT NULL,
  campus_id CHAR(36) NULL,
  visit_instance_id CHAR(36) NULL,
  title VARCHAR(255) NOT NULL,
  description TEXT NULL,
  status ENUM('DRAFT','PUBLISHED','HIDDEN','ARCHIVED') NOT NULL DEFAULT 'DRAFT',
  visibility ENUM('PRIVATE','INTERNAL','PUBLIC') NOT NULL DEFAULT 'INTERNAL',
  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  created_by CHAR(36) NULL,
  updated_at DATETIME NULL DEFAULT NULL ON UPDATE CURRENT_TIMESTAMP,
  updated_by CHAR(36) NULL,
  deleted_at DATETIME NULL,
  deleted_by CHAR(36) NULL,
  PRIMARY KEY (gallery_id),
  KEY idx_galleries_campus_status (campus_id, status, deleted_at),
  KEY idx_galleries_visit (visit_instance_id),
  KEY idx_galleries_visibility_status (visibility, status),
  CONSTRAINT fk_galleries_campus
    FOREIGN KEY (campus_id) REFERENCES campuses(campus_id)
    ON UPDATE CASCADE ON DELETE SET NULL,
  CONSTRAINT fk_galleries_visit
    FOREIGN KEY (visit_instance_id) REFERENCES visit_request_campuses(visit_instance_id)
    ON UPDATE CASCADE ON DELETE SET NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
COMMENT='Album ảnh';

CREATE TABLE gallery_images (
  image_id CHAR(36) NOT NULL,
  gallery_id CHAR(36) NOT NULL,
  file_id CHAR(36) NOT NULL,
  location_name VARCHAR(150) NULL COMMENT 'Merged gallery_locations table',
  caption VARCHAR(500) NULL,
  display_order INT UNSIGNED NOT NULL DEFAULT 0,
  taken_at DATETIME NULL,
  status ENUM('ACTIVE','HIDDEN','ARCHIVED') NOT NULL DEFAULT 'ACTIVE',
  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  created_by CHAR(36) NULL,
  updated_at DATETIME NULL DEFAULT NULL ON UPDATE CURRENT_TIMESTAMP,
  updated_by CHAR(36) NULL,
  deleted_at DATETIME NULL,
  deleted_by CHAR(36) NULL,
  PRIMARY KEY (image_id),
  UNIQUE KEY uq_gallery_images_file (file_id),
  KEY idx_gallery_images_gallery_order (gallery_id, display_order),
  KEY idx_gallery_images_status_time (status, taken_at),
  CONSTRAINT fk_gallery_images_gallery
    FOREIGN KEY (gallery_id) REFERENCES galleries(gallery_id)
    ON UPDATE CASCADE ON DELETE RESTRICT,
  CONSTRAINT fk_gallery_images_file
    FOREIGN KEY (file_id) REFERENCES files(file_id)
    ON UPDATE CASCADE ON DELETE RESTRICT
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
COMMENT='Ảnh trong album. Location simplified into location_name.';

CREATE TABLE photo_face_tags (
  face_tag_id CHAR(36) NOT NULL,
  image_id CHAR(36) NOT NULL,
  visit_request_id CHAR(36) NULL,
  guest_member_id CHAR(36) NULL,
  partner_contact_id CHAR(36) NULL,
  display_name VARCHAR(150) NOT NULL,
  bounding_box_x DECIMAL(8,4) NULL,
  bounding_box_y DECIMAL(8,4) NULL,
  bounding_box_width DECIMAL(8,4) NULL,
  bounding_box_height DECIMAL(8,4) NULL,
  tag_status ENUM('MANUALLY_TAGGED','CONFIRMED','REMOVED') NOT NULL DEFAULT 'MANUALLY_TAGGED',
  confirmed_by CHAR(36) NULL,
  confirmed_at DATETIME NULL,
  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  created_by CHAR(36) NULL,
  removed_at DATETIME NULL,
  removed_by CHAR(36) NULL,
  PRIMARY KEY (face_tag_id),
  KEY idx_face_tags_image (image_id),
  KEY idx_face_tags_guest (guest_member_id),
  KEY idx_face_tags_partner_contact (partner_contact_id),
  KEY idx_face_tags_status (tag_status),
  CONSTRAINT fk_face_tags_image
    FOREIGN KEY (image_id) REFERENCES gallery_images(image_id)
    ON UPDATE CASCADE ON DELETE CASCADE,
  CONSTRAINT fk_face_tags_visit_request
    FOREIGN KEY (visit_request_id) REFERENCES visit_requests(visit_request_id)
    ON UPDATE CASCADE ON DELETE SET NULL,
  CONSTRAINT fk_face_tags_guest
    FOREIGN KEY (guest_member_id) REFERENCES visit_guest_members(guest_member_id)
    ON UPDATE CASCADE ON DELETE SET NULL,
  CONSTRAINT fk_face_tags_partner_contact
    FOREIGN KEY (partner_contact_id) REFERENCES partner_contacts(contact_id)
    ON UPDATE CASCADE ON DELETE SET NULL,
  CONSTRAINT fk_face_tags_confirmed_by
    FOREIGN KEY (confirmed_by) REFERENCES users(user_id)
    ON UPDATE CASCADE ON DELETE SET NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
COMMENT='Confirmed face tag metadata only. No biometric vector.';

-- =====================================================================
-- 9. EMAIL + NOTIFICATION
-- =====================================================================

CREATE TABLE email_templates (
  email_template_id CHAR(36) NOT NULL,
  template_code VARCHAR(100) NOT NULL,
  name VARCHAR(150) NOT NULL,
  purpose VARCHAR(100) NOT NULL,
  status ENUM('ACTIVE','INACTIVE') NOT NULL DEFAULT 'ACTIVE',
  translations_json JSON NOT NULL COMMENT 'Merged email_template_translations table',
  variables_json JSON NULL COMMENT 'Allowed variables: FullName, OtpCode, Link...',
  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  created_by CHAR(36) NULL,
  updated_at DATETIME NULL DEFAULT NULL ON UPDATE CURRENT_TIMESTAMP,
  updated_by CHAR(36) NULL,
  PRIMARY KEY (email_template_id),
  UNIQUE KEY uq_email_templates_code (template_code),
  KEY idx_email_templates_status (status),
  KEY idx_email_templates_purpose_status (purpose, status)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
COMMENT='Email templates with translations_json';

CREATE TABLE sent_emails (
  sent_email_id CHAR(36) NOT NULL,
  email_template_id CHAR(36) NULL,
  related_type VARCHAR(80) NULL,
  related_id CHAR(36) NULL,
  subject VARCHAR(255) NOT NULL,
  body_snapshot LONGTEXT NULL,
  recipients_json JSON NOT NULL COMMENT 'Merged sent_email_recipients table',
  metadata_json JSON NULL COMMENT 'provider message id, retry count, etc.',
  status ENUM('QUEUED','SENT','FAILED') NOT NULL DEFAULT 'QUEUED',
  error_message TEXT NULL,
  sent_by CHAR(36) NULL,
  sent_at DATETIME NULL,
  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (sent_email_id),
  KEY idx_sent_emails_template (email_template_id),
  KEY idx_sent_emails_related (related_type, related_id),
  KEY idx_sent_emails_status_time (status, created_at),
  KEY idx_sent_emails_sent_by_time (sent_by, sent_at),
  CONSTRAINT fk_sent_emails_template
    FOREIGN KEY (email_template_id) REFERENCES email_templates(email_template_id)
    ON UPDATE CASCADE ON DELETE SET NULL,
  CONSTRAINT fk_sent_emails_sent_by
    FOREIGN KEY (sent_by) REFERENCES users(user_id)
    ON UPDATE CASCADE ON DELETE SET NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
COMMENT='Sent email log with recipients_json';

CREATE TABLE notifications (
  notification_id CHAR(36) NOT NULL,
  recipient_user_id CHAR(36) NOT NULL,
  title VARCHAR(255) NOT NULL,
  message TEXT NULL,
  notification_type VARCHAR(80) NOT NULL,
  related_type VARCHAR(80) NULL,
  related_id CHAR(36) NULL,
  is_read BOOLEAN NOT NULL DEFAULT FALSE,
  read_at DATETIME NULL,
  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (notification_id),
  KEY idx_notifications_user_read_time (recipient_user_id, is_read, created_at),
  KEY idx_notifications_related (related_type, related_id),
  KEY idx_notifications_type_time (notification_type, created_at),
  CONSTRAINT fk_notifications_user
    FOREIGN KEY (recipient_user_id) REFERENCES users(user_id)
    ON UPDATE CASCADE ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
COMMENT='In-app notifications';

-- =====================================================================
-- 10. CALENDAR + API + AGENDA TEMPLATE
-- =====================================================================

CREATE TABLE calendar_events (
  calendar_event_id CHAR(36) NOT NULL,
  owner_user_id CHAR(36) NOT NULL,
  campus_id CHAR(36) NULL,
  visit_instance_id CHAR(36) NULL,
  logistics_item_id CHAR(36) NULL,
  source_type ENUM('PERSONAL','VISIT','LOGISTICS','DEADLINE') NOT NULL DEFAULT 'PERSONAL',
  title VARCHAR(255) NOT NULL,
  description TEXT NULL,
  location VARCHAR(255) NULL,
  start_at DATETIME NOT NULL,
  end_at DATETIME NOT NULL,
  timezone VARCHAR(50) NOT NULL DEFAULT 'Asia/Ho_Chi_Minh',
  visibility ENUM('PRIVATE','INTERNAL') NOT NULL DEFAULT 'PRIVATE',
  attendees_json JSON NULL COMMENT 'Merged calendar_event_attendees table',
  reminders_json JSON NULL COMMENT 'Merged calendar_event_reminders table',
  status ENUM('ACTIVE','CANCELLED','DONE') NOT NULL DEFAULT 'ACTIVE',
  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  created_by CHAR(36) NULL,
  updated_at DATETIME NULL DEFAULT NULL ON UPDATE CURRENT_TIMESTAMP,
  updated_by CHAR(36) NULL,
  deleted_at DATETIME NULL,
  deleted_by CHAR(36) NULL,
  PRIMARY KEY (calendar_event_id),
  KEY idx_calendar_owner_time (owner_user_id, start_at),
  KEY idx_calendar_campus_time (campus_id, start_at),
  KEY idx_calendar_visit (visit_instance_id),
  KEY idx_calendar_logistics (logistics_item_id),
  KEY idx_calendar_source_status_time (source_type, status, start_at),
  CHECK (end_at > start_at),
  CONSTRAINT fk_calendar_owner
    FOREIGN KEY (owner_user_id) REFERENCES users(user_id)
    ON UPDATE CASCADE ON DELETE CASCADE,
  CONSTRAINT fk_calendar_campus
    FOREIGN KEY (campus_id) REFERENCES campuses(campus_id)
    ON UPDATE CASCADE ON DELETE SET NULL,
  CONSTRAINT fk_calendar_visit
    FOREIGN KEY (visit_instance_id) REFERENCES visit_request_campuses(visit_instance_id)
    ON UPDATE CASCADE ON DELETE SET NULL,
  CONSTRAINT fk_calendar_logistics
    FOREIGN KEY (logistics_item_id) REFERENCES visit_logistics_items(logistics_item_id)
    ON UPDATE CASCADE ON DELETE SET NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
COMMENT='Calendar events. Personal/visit/logistics/deadline events. Attendees/reminders merged into JSON fields.';

CREATE TABLE api_configurations (
  api_config_id CHAR(36) NOT NULL,
  api_code VARCHAR(100) NOT NULL,
  name VARCHAR(150) NOT NULL,
  provider_name VARCHAR(150) NULL,
  purpose VARCHAR(150) NULL,
  base_url VARCHAR(500) NOT NULL,
  default_method ENUM('GET','POST','PUT','PATCH','DELETE') NOT NULL DEFAULT 'POST',
  auth_type ENUM('NONE','API_KEY','BEARER_TOKEN','BASIC','OAUTH2','CUSTOM') NOT NULL DEFAULT 'NONE',
  credentials_json JSON NULL COMMENT 'Encrypted/masked credentials. Merged api_credentials table.',
  headers_json JSON NULL,
  body_template_json JSON NULL,
  settings_json JSON NULL,
  timeout_seconds INT UNSIGNED NOT NULL DEFAULT 30,
  status ENUM('ACTIVE','INACTIVE','DISABLED') NOT NULL DEFAULT 'ACTIVE',
  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  created_by CHAR(36) NULL,
  updated_at DATETIME NULL DEFAULT NULL ON UPDATE CURRENT_TIMESTAMP,
  updated_by CHAR(36) NULL,
  deleted_at DATETIME NULL,
  deleted_by CHAR(36) NULL,
  PRIMARY KEY (api_config_id),
  UNIQUE KEY uq_api_config_code (api_code),
  KEY idx_api_config_status (status),
  KEY idx_api_provider_status (provider_name, status)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
COMMENT='API config + encrypted credentials JSON';

CREATE TABLE api_usage_quotas (
  api_usage_quota_id CHAR(36) NOT NULL,
  api_config_id CHAR(36) NOT NULL,
  campus_id CHAR(36) NULL COMMENT 'NULL = global quota',
  campus_scope_key VARCHAR(36) NOT NULL DEFAULT 'GLOBAL',
  period_yyyymm CHAR(6) NOT NULL COMMENT 'YYYYMM',
  monthly_limit INT UNSIGNED NOT NULL,
  used_count INT UNSIGNED NOT NULL DEFAULT 0 COMMENT 'Merged api_usage_counters table',
  last_used_at DATETIME NULL,
  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  created_by CHAR(36) NULL,
  updated_at DATETIME NULL DEFAULT NULL ON UPDATE CURRENT_TIMESTAMP,
  updated_by CHAR(36) NULL,
  PRIMARY KEY (api_usage_quota_id),
  UNIQUE KEY uq_api_quota_config_scope_period (api_config_id, campus_scope_key, period_yyyymm),
  KEY idx_api_quota_campus_period (campus_id, period_yyyymm),
  KEY idx_api_quota_period (period_yyyymm),
  CONSTRAINT fk_api_quota_config
    FOREIGN KEY (api_config_id) REFERENCES api_configurations(api_config_id)
    ON UPDATE CASCADE ON DELETE CASCADE,
  CONSTRAINT fk_api_quota_campus
    FOREIGN KEY (campus_id) REFERENCES campuses(campus_id)
    ON UPDATE CASCADE ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
COMMENT='API quota + counter per campus/month';

CREATE TABLE api_request_logs (
  api_request_log_id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
  api_config_id CHAR(36) NOT NULL,
  campus_id CHAR(36) NULL,
  requested_by CHAR(36) NULL,
  related_type VARCHAR(80) NULL,
  related_id CHAR(36) NULL,
  endpoint VARCHAR(500) NOT NULL,
  method ENUM('GET','POST','PUT','PATCH','DELETE') NOT NULL,
  http_status INT NULL,
  response_time_ms INT UNSIGNED NULL,
  request_size_bytes BIGINT UNSIGNED NULL,
  response_size_bytes BIGINT UNSIGNED NULL,
  success BOOLEAN NOT NULL DEFAULT FALSE,
  error_code VARCHAR(100) NULL,
  error_message TEXT NULL,
  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (api_request_log_id),
  KEY idx_api_logs_config_time (api_config_id, created_at),
  KEY idx_api_logs_campus_time (campus_id, created_at),
  KEY idx_api_logs_user_time (requested_by, created_at),
  KEY idx_api_logs_success_time (success, created_at),
  KEY idx_api_logs_related (related_type, related_id),
  CONSTRAINT fk_api_logs_config
    FOREIGN KEY (api_config_id) REFERENCES api_configurations(api_config_id)
    ON UPDATE CASCADE ON DELETE RESTRICT,
  CONSTRAINT fk_api_logs_campus
    FOREIGN KEY (campus_id) REFERENCES campuses(campus_id)
    ON UPDATE CASCADE ON DELETE SET NULL,
  CONSTRAINT fk_api_logs_user
    FOREIGN KEY (requested_by) REFERENCES users(user_id)
    ON UPDATE CASCADE ON DELETE SET NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
COMMENT='External API request logs. Never log full secret/token.';

CREATE TABLE agenda_templates (
  agenda_template_id CHAR(36) NOT NULL,
  campus_id CHAR(36) NULL,
  campus_scope_key VARCHAR(36) NOT NULL DEFAULT 'GLOBAL',
  name VARCHAR(150) NOT NULL,
  description TEXT NULL,
  items_json JSON NOT NULL COMMENT 'Merged agenda_template_items table',
  status ENUM('ACTIVE','INACTIVE') NOT NULL DEFAULT 'ACTIVE',
  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  created_by CHAR(36) NULL,
  updated_at DATETIME NULL DEFAULT NULL ON UPDATE CURRENT_TIMESTAMP,
  updated_by CHAR(36) NULL,
  deleted_at DATETIME NULL,
  deleted_by CHAR(36) NULL,
  PRIMARY KEY (agenda_template_id),
  UNIQUE KEY uq_agenda_template_scope_name (campus_scope_key, name),
  KEY idx_agenda_templates_status (status),
  KEY idx_agenda_templates_campus_status (campus_id, status),
  CONSTRAINT fk_agenda_templates_campus
    FOREIGN KEY (campus_id) REFERENCES campuses(campus_id)
    ON UPDATE CASCADE ON DELETE SET NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
COMMENT='Agenda template with items_json';

-- =====================================================================
-- 11. AUDIT
-- =====================================================================

CREATE TABLE audit_logs (
  audit_log_id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
  actor_user_id CHAR(36) NULL,
  campus_id CHAR(36) NULL,
  action VARCHAR(100) NOT NULL,
  entity_type VARCHAR(100) NOT NULL,
  entity_id CHAR(36) NULL,
  old_values_json JSON NULL,
  new_values_json JSON NULL,
  ip_address VARCHAR(45) NULL,
  user_agent VARCHAR(500) NULL,
  request_id VARCHAR(100) NULL,
  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (audit_log_id),
  KEY idx_audit_actor_time (actor_user_id, created_at),
  KEY idx_audit_entity (entity_type, entity_id),
  KEY idx_audit_action_time (action, created_at),
  KEY idx_audit_campus_time (campus_id, created_at),
  KEY idx_audit_request (request_id),
  CONSTRAINT fk_audit_actor
    FOREIGN KEY (actor_user_id) REFERENCES users(user_id)
    ON UPDATE CASCADE ON DELETE SET NULL,
  CONSTRAINT fk_audit_campus
    FOREIGN KEY (campus_id) REFERENCES campuses(campus_id)
    ON UPDATE CASCADE ON DELETE SET NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
COMMENT='General audit log';

CREATE TABLE visit_status_logs (
  visit_status_log_id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
  visit_request_id CHAR(36) NULL,
  visit_instance_id CHAR(36) NULL,
  old_status VARCHAR(50) NULL,
  new_status VARCHAR(50) NOT NULL,
  changed_by CHAR(36) NULL,
  reason TEXT NULL,
  changed_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (visit_status_log_id),
  KEY idx_visit_status_request_time (visit_request_id, changed_at),
  KEY idx_visit_status_instance_time (visit_instance_id, changed_at),
  KEY idx_visit_status_changed_by_time (changed_by, changed_at),
  CONSTRAINT fk_visit_status_logs_request
    FOREIGN KEY (visit_request_id) REFERENCES visit_requests(visit_request_id)
    ON UPDATE CASCADE ON DELETE SET NULL,
  CONSTRAINT fk_visit_status_logs_instance
    FOREIGN KEY (visit_instance_id) REFERENCES visit_request_campuses(visit_instance_id)
    ON UPDATE CASCADE ON DELETE SET NULL,
  CONSTRAINT fk_visit_status_logs_changed_by
    FOREIGN KEY (changed_by) REFERENCES users(user_id)
    ON UPDATE CASCADE ON DELETE SET NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
COMMENT='Timeline trạng thái visit';

-- =====================================================================
-- 12. VALIDATION TRIGGERS
-- =====================================================================

DELIMITER $$

CREATE TRIGGER trg_departments_one_ic_bi
BEFORE INSERT ON departments
FOR EACH ROW
BEGIN
  DECLARE v_exists INT DEFAULT 0;

  IF NEW.department_type = 'IC' AND NEW.status = 'ACTIVE' THEN
    SELECT COUNT(*) INTO v_exists
    FROM departments
    WHERE campus_id = NEW.campus_id
      AND department_type = 'IC'
      AND status = 'ACTIVE';

    IF v_exists > 0 THEN
      SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Each campus can have only one active IC department';
    END IF;
  END IF;
END$$

CREATE TRIGGER trg_departments_one_ic_bu
BEFORE UPDATE ON departments
FOR EACH ROW
BEGIN
  DECLARE v_exists INT DEFAULT 0;

  IF NEW.department_type = 'IC' AND NEW.status = 'ACTIVE' THEN
    SELECT COUNT(*) INTO v_exists
    FROM departments
    WHERE campus_id = NEW.campus_id
      AND department_type = 'IC'
      AND status = 'ACTIVE'
      AND department_id <> NEW.department_id;

    IF v_exists > 0 THEN
      SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Each campus can have only one active IC department';
    END IF;
  END IF;
END$$

CREATE TRIGGER trg_users_validate_bi
BEFORE INSERT ON users
FOR EACH ROW
BEGIN
  DECLARE v_role_code VARCHAR(30);
  DECLARE v_department_type VARCHAR(20);
  DECLARE v_department_campus_id CHAR(36);

  SELECT role_code INTO v_role_code
  FROM roles
  WHERE role_id = NEW.role_id
    AND deleted_at IS NULL;

  IF v_role_code IS NULL THEN
    SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Invalid role_id';
  END IF;

  IF v_role_code = 'VISITOR' THEN
    IF NEW.sub_role IS NOT NULL THEN
      SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'VISITOR must not have sub_role';
    END IF;
    IF NEW.department_id IS NOT NULL THEN
      SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'VISITOR must not have department_id';
    END IF;
    IF NEW.primary_campus_id IS NOT NULL THEN
      SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'VISITOR must not have primary_campus_id';
    END IF;
  ELSEIF v_role_code IN ('STAFF','DEPT') THEN
    IF NEW.sub_role IS NULL THEN
      SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'STAFF/DEPT must have sub_role';
    END IF;
    IF NEW.department_id IS NULL THEN
      SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'STAFF/DEPT must have department_id';
    END IF;

    SELECT department_type, campus_id
      INTO v_department_type, v_department_campus_id
    FROM departments
    WHERE department_id = NEW.department_id;

    IF v_role_code = 'STAFF' AND v_department_type <> 'IC' THEN
      SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'STAFF must belong to IC department';
    END IF;

    IF v_role_code = 'DEPT' AND v_department_type <> 'GENERAL' THEN
      SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'DEPT must belong to GENERAL department';
    END IF;

    IF NEW.primary_campus_id IS NULL THEN
      SET NEW.primary_campus_id = v_department_campus_id;
    ELSEIF NEW.primary_campus_id <> v_department_campus_id THEN
      SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'primary_campus_id must match department campus';
    END IF;
  ELSE
    IF NEW.sub_role IS NOT NULL THEN
      SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Only STAFF/DEPT may have sub_role';
    END IF;
    IF NEW.department_id IS NOT NULL THEN
      SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Only STAFF/DEPT may have department_id';
    END IF;
    IF NEW.primary_campus_id IS NULL THEN
      SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Internal user must have primary_campus_id';
    END IF;
  END IF;
END$$

CREATE TRIGGER trg_users_validate_bu
BEFORE UPDATE ON users
FOR EACH ROW
BEGIN
  DECLARE v_role_code VARCHAR(30);
  DECLARE v_department_type VARCHAR(20);
  DECLARE v_department_campus_id CHAR(36);

  SELECT role_code INTO v_role_code
  FROM roles
  WHERE role_id = NEW.role_id
    AND deleted_at IS NULL;

  IF v_role_code IS NULL THEN
    SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Invalid role_id';
  END IF;

  IF v_role_code = 'VISITOR' THEN
    IF NEW.sub_role IS NOT NULL THEN
      SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'VISITOR must not have sub_role';
    END IF;
    IF NEW.department_id IS NOT NULL THEN
      SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'VISITOR must not have department_id';
    END IF;
    IF NEW.primary_campus_id IS NOT NULL THEN
      SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'VISITOR must not have primary_campus_id';
    END IF;
  ELSEIF v_role_code IN ('STAFF','DEPT') THEN
    IF NEW.sub_role IS NULL THEN
      SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'STAFF/DEPT must have sub_role';
    END IF;
    IF NEW.department_id IS NULL THEN
      SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'STAFF/DEPT must have department_id';
    END IF;

    SELECT department_type, campus_id
      INTO v_department_type, v_department_campus_id
    FROM departments
    WHERE department_id = NEW.department_id;

    IF v_role_code = 'STAFF' AND v_department_type <> 'IC' THEN
      SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'STAFF must belong to IC department';
    END IF;

    IF v_role_code = 'DEPT' AND v_department_type <> 'GENERAL' THEN
      SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'DEPT must belong to GENERAL department';
    END IF;

    IF NEW.primary_campus_id IS NULL THEN
      SET NEW.primary_campus_id = v_department_campus_id;
    ELSEIF NEW.primary_campus_id <> v_department_campus_id THEN
      SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'primary_campus_id must match department campus';
    END IF;
  ELSE
    IF NEW.sub_role IS NOT NULL THEN
      SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Only STAFF/DEPT may have sub_role';
    END IF;
    IF NEW.department_id IS NOT NULL THEN
      SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Only STAFF/DEPT may have department_id';
    END IF;
    IF NEW.primary_campus_id IS NULL THEN
      SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Internal user must have primary_campus_id';
    END IF;
  END IF;
END$$

CREATE TRIGGER trg_auth_providers_validate_bi
BEFORE INSERT ON user_auth_providers
FOR EACH ROW
BEGIN
  IF NEW.provider_type IN ('GOOGLE_SSO','FEID')
     AND (NEW.provider_subject IS NULL OR NEW.provider_subject = '') THEN
    SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'SSO/FEID provider_subject is required';
  END IF;
END$$

CREATE TRIGGER trg_auth_providers_validate_bu
BEFORE UPDATE ON user_auth_providers
FOR EACH ROW
BEGIN
  IF NEW.provider_type IN ('GOOGLE_SSO','FEID')
     AND (NEW.provider_subject IS NULL OR NEW.provider_subject = '') THEN
    SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'SSO/FEID provider_subject is required';
  END IF;
END$$

CREATE TRIGGER trg_sessions_validate_bi
BEFORE INSERT ON user_sessions
FOR EACH ROW
BEGIN
  DECLARE v_role_code VARCHAR(30);
  DECLARE v_primary_campus_id CHAR(36);

  SELECT r.role_code, u.primary_campus_id
    INTO v_role_code, v_primary_campus_id
  FROM users u
  JOIN roles r ON r.role_id = u.role_id
  WHERE u.user_id = NEW.user_id;

  IF NEW.login_portal = 'VISITOR' THEN
    IF v_role_code <> 'VISITOR' THEN
      SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Only VISITOR can login via Visitor Portal';
    END IF;
    IF NEW.selected_campus_id IS NOT NULL THEN
      SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Visitor Portal must not have selected_campus_id';
    END IF;
  ELSEIF NEW.login_portal = 'INTERNAL' THEN
    IF v_role_code = 'VISITOR' THEN
      SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'VISITOR cannot login via Internal Portal';
    END IF;
    IF v_primary_campus_id IS NULL THEN
      SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Internal user must have primary_campus_id';
    END IF;
    IF NEW.selected_campus_id IS NULL THEN
      SET NEW.selected_campus_id = v_primary_campus_id;
    ELSEIF NEW.selected_campus_id <> v_primary_campus_id THEN
      SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Internal user can only login to their own primary campus';
    END IF;
  END IF;
END$$


-- ---------------------------------------------------------------------
-- Visit request decision rules
-- ---------------------------------------------------------------------

CREATE TRIGGER trg_visit_requests_decision_validate_bi
BEFORE INSERT ON visit_requests
FOR EACH ROW
BEGIN
  DECLARE v_actor_role_code VARCHAR(30);
  DECLARE v_actor_sub_role VARCHAR(30);

  IF NEW.status IN ('APPROVED','REJECTED','CANCELLED') THEN
    IF NEW.decision_actor_role IS NULL THEN
      SIGNAL SQLSTATE '45000'
        SET MESSAGE_TEXT = 'decision_actor_role is required when visit request is decided';
    END IF;

    IF NEW.decision_actor_role <> 'SYSTEM' AND NEW.decided_by IS NULL THEN
      SIGNAL SQLSTATE '45000'
        SET MESSAGE_TEXT = 'decided_by is required for non-system visit request decision';
    END IF;

    IF NEW.visit_scope = 'SINGLE_CAMPUS'
       AND NEW.decision_actor_role NOT IN ('STAFF_LEADER','SYSTEM') THEN
      SIGNAL SQLSTATE '45000'
        SET MESSAGE_TEXT = 'Only STAFF_LEADER can decide SINGLE_CAMPUS request';
    END IF;

    IF NEW.visit_scope = 'MULTI_CAMPUS'
       AND NEW.decision_actor_role NOT IN ('HO','SYSTEM') THEN
      SIGNAL SQLSTATE '45000'
        SET MESSAGE_TEXT = 'Only HO can decide MULTI_CAMPUS request';
    END IF;

    IF NEW.decision_actor_role <> 'SYSTEM' THEN
      SELECT r.role_code, u.sub_role
        INTO v_actor_role_code, v_actor_sub_role
      FROM users u
      JOIN roles r ON r.role_id = u.role_id
      WHERE u.user_id = NEW.decided_by;

      IF NEW.decision_actor_role = 'HO' AND v_actor_role_code <> 'HO' THEN
        SIGNAL SQLSTATE '45000'
          SET MESSAGE_TEXT = 'decision_actor_role HO requires decided_by user with HO role';
      END IF;

      IF NEW.decision_actor_role = 'STAFF_LEADER'
         AND NOT (v_actor_role_code = 'STAFF' AND v_actor_sub_role = 'Leader') THEN
        SIGNAL SQLSTATE '45000'
          SET MESSAGE_TEXT = 'decision_actor_role STAFF_LEADER requires STAFF Leader user';
      END IF;
    END IF;
  END IF;
END$$

CREATE TRIGGER trg_visit_requests_decision_validate_bu
BEFORE UPDATE ON visit_requests
FOR EACH ROW
BEGIN
  DECLARE v_actor_role_code VARCHAR(30);
  DECLARE v_actor_sub_role VARCHAR(30);

  IF NEW.status IN ('APPROVED','REJECTED','CANCELLED') THEN
    IF NEW.decision_actor_role IS NULL THEN
      SIGNAL SQLSTATE '45000'
        SET MESSAGE_TEXT = 'decision_actor_role is required when visit request is decided';
    END IF;

    IF NEW.decision_actor_role <> 'SYSTEM' AND NEW.decided_by IS NULL THEN
      SIGNAL SQLSTATE '45000'
        SET MESSAGE_TEXT = 'decided_by is required for non-system visit request decision';
    END IF;

    IF NEW.visit_scope = 'SINGLE_CAMPUS'
       AND NEW.decision_actor_role NOT IN ('STAFF_LEADER','SYSTEM') THEN
      SIGNAL SQLSTATE '45000'
        SET MESSAGE_TEXT = 'Only STAFF_LEADER can decide SINGLE_CAMPUS request';
    END IF;

    IF NEW.visit_scope = 'MULTI_CAMPUS'
       AND NEW.decision_actor_role NOT IN ('HO','SYSTEM') THEN
      SIGNAL SQLSTATE '45000'
        SET MESSAGE_TEXT = 'Only HO can decide MULTI_CAMPUS request';
    END IF;

    IF NEW.decision_actor_role <> 'SYSTEM' THEN
      SELECT r.role_code, u.sub_role
        INTO v_actor_role_code, v_actor_sub_role
      FROM users u
      JOIN roles r ON r.role_id = u.role_id
      WHERE u.user_id = NEW.decided_by;

      IF NEW.decision_actor_role = 'HO' AND v_actor_role_code <> 'HO' THEN
        SIGNAL SQLSTATE '45000'
          SET MESSAGE_TEXT = 'decision_actor_role HO requires decided_by user with HO role';
      END IF;

      IF NEW.decision_actor_role = 'STAFF_LEADER'
         AND NOT (v_actor_role_code = 'STAFF' AND v_actor_sub_role = 'Leader') THEN
        SIGNAL SQLSTATE '45000'
          SET MESSAGE_TEXT = 'decision_actor_role STAFF_LEADER requires STAFF Leader user';
      END IF;
    END IF;
  END IF;
END$$

-- ---------------------------------------------------------------------
-- Campus instance assignment/host rules
-- ---------------------------------------------------------------------

CREATE TRIGGER trg_visit_campuses_assignment_validate_bi
BEFORE INSERT ON visit_request_campuses
FOR EACH ROW
BEGIN
  DECLARE v_request_status VARCHAR(30);
  DECLARE v_host_role_code VARCHAR(30);
  DECLARE v_host_sub_role VARCHAR(30);
  DECLARE v_host_campus_id CHAR(36);
  DECLARE v_transfer_role_code VARCHAR(30);
  DECLARE v_transfer_sub_role VARCHAR(30);
  DECLARE v_transfer_campus_id CHAR(36);

  SELECT status
    INTO v_request_status
  FROM visit_requests
  WHERE visit_request_id = NEW.visit_request_id;

  IF NEW.status = 'WAITING_REQUEST_APPROVAL' AND NEW.current_host_user_id IS NOT NULL THEN
    SIGNAL SQLSTATE '45000'
      SET MESSAGE_TEXT = 'WAITING_REQUEST_APPROVAL campus instance must not have current_host_user_id yet';
  END IF;

  IF NEW.status NOT IN ('WAITING_REQUEST_APPROVAL','CANCELLED') THEN
    IF v_request_status <> 'APPROVED' THEN
      SIGNAL SQLSTATE '45000'
        SET MESSAGE_TEXT = 'Campus instance can move to operational status only after main visit request is APPROVED';
    END IF;

    IF NEW.current_host_user_id IS NULL THEN
      SIGNAL SQLSTATE '45000'
        SET MESSAGE_TEXT = 'current_host_user_id is required after main visit request is approved';
    END IF;
  END IF;

  IF NEW.current_host_user_id IS NOT NULL THEN
    SELECT r.role_code, u.sub_role, u.primary_campus_id
      INTO v_host_role_code, v_host_sub_role, v_host_campus_id
    FROM users u
    JOIN roles r ON r.role_id = u.role_id
    WHERE u.user_id = NEW.current_host_user_id;

    IF NOT (v_host_role_code = 'STAFF' AND v_host_sub_role IN ('Leader','Staff')) THEN
      SIGNAL SQLSTATE '45000'
        SET MESSAGE_TEXT = 'current_host_user_id must be a STAFF user';
    END IF;

    IF v_host_campus_id <> NEW.campus_id THEN
      SIGNAL SQLSTATE '45000'
        SET MESSAGE_TEXT = 'current_host_user_id must belong to the same campus instance';
    END IF;
  END IF;

  IF NEW.host_transferred_at IS NOT NULL AND NEW.host_transferred_by IS NULL THEN
    SIGNAL SQLSTATE '45000'
      SET MESSAGE_TEXT = 'host_transferred_by is required when host_transferred_at is set';
  END IF;

  IF NEW.host_transferred_by IS NOT NULL THEN
    SELECT r.role_code, u.sub_role, u.primary_campus_id
      INTO v_transfer_role_code, v_transfer_sub_role, v_transfer_campus_id
    FROM users u
    JOIN roles r ON r.role_id = u.role_id
    WHERE u.user_id = NEW.host_transferred_by;

    IF NOT (v_transfer_role_code = 'STAFF' AND v_transfer_sub_role IN ('Leader','Staff')) THEN
      SIGNAL SQLSTATE '45000'
        SET MESSAGE_TEXT = 'host_transferred_by must be a STAFF user';
    END IF;

    IF v_transfer_campus_id <> NEW.campus_id THEN
      SIGNAL SQLSTATE '45000'
        SET MESSAGE_TEXT = 'host_transferred_by must belong to the same campus instance';
    END IF;
  END IF;
END$$

CREATE TRIGGER trg_visit_campuses_assignment_validate_bu
BEFORE UPDATE ON visit_request_campuses
FOR EACH ROW
BEGIN
  DECLARE v_request_status VARCHAR(30);
  DECLARE v_host_role_code VARCHAR(30);
  DECLARE v_host_sub_role VARCHAR(30);
  DECLARE v_host_campus_id CHAR(36);
  DECLARE v_transfer_role_code VARCHAR(30);
  DECLARE v_transfer_sub_role VARCHAR(30);
  DECLARE v_transfer_campus_id CHAR(36);

  SELECT status
    INTO v_request_status
  FROM visit_requests
  WHERE visit_request_id = NEW.visit_request_id;

  IF NEW.status = 'WAITING_REQUEST_APPROVAL' AND NEW.current_host_user_id IS NOT NULL THEN
    SIGNAL SQLSTATE '45000'
      SET MESSAGE_TEXT = 'WAITING_REQUEST_APPROVAL campus instance must not have current_host_user_id yet';
  END IF;

  IF NEW.status NOT IN ('WAITING_REQUEST_APPROVAL','CANCELLED') THEN
    IF v_request_status <> 'APPROVED' THEN
      SIGNAL SQLSTATE '45000'
        SET MESSAGE_TEXT = 'Campus instance can move to operational status only after main visit request is APPROVED';
    END IF;

    IF NEW.current_host_user_id IS NULL THEN
      SIGNAL SQLSTATE '45000'
        SET MESSAGE_TEXT = 'current_host_user_id is required after main visit request is approved';
    END IF;
  END IF;

  IF NEW.current_host_user_id IS NOT NULL THEN
    SELECT r.role_code, u.sub_role, u.primary_campus_id
      INTO v_host_role_code, v_host_sub_role, v_host_campus_id
    FROM users u
    JOIN roles r ON r.role_id = u.role_id
    WHERE u.user_id = NEW.current_host_user_id;

    IF NOT (v_host_role_code = 'STAFF' AND v_host_sub_role IN ('Leader','Staff')) THEN
      SIGNAL SQLSTATE '45000'
        SET MESSAGE_TEXT = 'current_host_user_id must be a STAFF user';
    END IF;

    IF v_host_campus_id <> NEW.campus_id THEN
      SIGNAL SQLSTATE '45000'
        SET MESSAGE_TEXT = 'current_host_user_id must belong to the same campus instance';
    END IF;
  END IF;

  IF NOT (NEW.current_host_user_id <=> OLD.current_host_user_id)
     AND OLD.current_host_user_id IS NOT NULL THEN
    IF NEW.host_transferred_by IS NULL OR NEW.host_transferred_at IS NULL THEN
      SIGNAL SQLSTATE '45000'
        SET MESSAGE_TEXT = 'host_transferred_by and host_transferred_at are required when transferring host';
    END IF;
  END IF;

  IF NEW.host_transferred_at IS NOT NULL AND NEW.host_transferred_by IS NULL THEN
    SIGNAL SQLSTATE '45000'
      SET MESSAGE_TEXT = 'host_transferred_by is required when host_transferred_at is set';
  END IF;

  IF NEW.host_transferred_by IS NOT NULL THEN
    SELECT r.role_code, u.sub_role, u.primary_campus_id
      INTO v_transfer_role_code, v_transfer_sub_role, v_transfer_campus_id
    FROM users u
    JOIN roles r ON r.role_id = u.role_id
    WHERE u.user_id = NEW.host_transferred_by;

    IF NOT (v_transfer_role_code = 'STAFF' AND v_transfer_sub_role IN ('Leader','Staff')) THEN
      SIGNAL SQLSTATE '45000'
        SET MESSAGE_TEXT = 'host_transferred_by must be a STAFF user';
    END IF;

    IF v_transfer_campus_id <> NEW.campus_id THEN
      SIGNAL SQLSTATE '45000'
        SET MESSAGE_TEXT = 'host_transferred_by must belong to the same campus instance';
    END IF;
  END IF;
END$$

CREATE TRIGGER trg_public_contents_scope_bi
BEFORE INSERT ON public_contents
FOR EACH ROW
BEGIN
  SET NEW.campus_scope_key = IFNULL(NEW.campus_id, 'GLOBAL');
END$$

CREATE TRIGGER trg_public_contents_scope_bu
BEFORE UPDATE ON public_contents
FOR EACH ROW
BEGIN
  SET NEW.campus_scope_key = IFNULL(NEW.campus_id, 'GLOBAL');
END$$

CREATE TRIGGER trg_api_usage_quotas_scope_bi
BEFORE INSERT ON api_usage_quotas
FOR EACH ROW
BEGIN
  SET NEW.campus_scope_key = IFNULL(NEW.campus_id, 'GLOBAL');
END$$

CREATE TRIGGER trg_api_usage_quotas_scope_bu
BEFORE UPDATE ON api_usage_quotas
FOR EACH ROW
BEGIN
  SET NEW.campus_scope_key = IFNULL(NEW.campus_id, 'GLOBAL');
END$$

CREATE TRIGGER trg_agenda_templates_scope_bi
BEFORE INSERT ON agenda_templates
FOR EACH ROW
BEGIN
  SET NEW.campus_scope_key = IFNULL(NEW.campus_id, 'GLOBAL');
END$$

CREATE TRIGGER trg_agenda_templates_scope_bu
BEFORE UPDATE ON agenda_templates
FOR EACH ROW
BEGIN
  SET NEW.campus_scope_key = IFNULL(NEW.campus_id, 'GLOBAL');
END$$

DELIMITER ;

-- =====================================================================
-- 13. SEED BASIC DATA
-- =====================================================================

INSERT INTO roles (role_id, role_code, name, description)
VALUES
  (UUID(), 'ADMIN', 'Admin', 'Quản trị kỹ thuật hệ thống'),
  (UUID(), 'HO', 'Head Office', 'Quản lý cấp Head Office'),
  (UUID(), 'STAFF', 'IC Staff', 'Nhân sự phòng Hợp tác Quốc tế, dùng sub_role Leader/Staff'),
  (UUID(), 'DEPT', 'Department', 'Nhân sự phòng ban khác, dùng sub_role Leader/Staff'),
  (UUID(), 'STUDENT', 'Student', 'Sinh viên hỗ trợ'),
  (UUID(), 'VISITOR', 'Visitor', 'Khách gửi visit request và theo dõi thông tin của mình');

SET @campus_hn = UUID();
SET @campus_hcm = UUID();
SET @campus_dn = UUID();
SET @campus_ct = UUID();
SET @campus_qn = UUID();

INSERT INTO campuses (campus_id, campus_code, name, city, status)
VALUES
  (@campus_hn, 'HN', 'FPT University Hà Nội', 'Hà Nội', 'ACTIVE'),
  (@campus_hcm, 'HCM', 'FPT University TP. Hồ Chí Minh', 'TP. Hồ Chí Minh', 'ACTIVE'),
  (@campus_dn, 'DN', 'FPT University Đà Nẵng', 'Đà Nẵng', 'ACTIVE'),
  (@campus_ct, 'CT', 'FPT University Cần Thơ', 'Cần Thơ', 'ACTIVE'),
  (@campus_qn, 'QN', 'FPT University Quy Nhơn', 'Quy Nhơn', 'ACTIVE');

INSERT INTO departments (department_id, campus_id, department_code, name, department_type, status)
VALUES
  (UUID(), @campus_hn, 'IC', 'International Cooperation', 'IC', 'ACTIVE'),
  (UUID(), @campus_hcm, 'IC', 'International Cooperation', 'IC', 'ACTIVE'),
  (UUID(), @campus_dn, 'IC', 'International Cooperation', 'IC', 'ACTIVE'),
  (UUID(), @campus_ct, 'IC', 'International Cooperation', 'IC', 'ACTIVE'),
  (UUID(), @campus_qn, 'IC', 'International Cooperation', 'IC', 'ACTIVE'),
  (UUID(), @campus_hn, 'ACADEMIC', 'Academic Department', 'GENERAL', 'ACTIVE'),
  (UUID(), @campus_hn, 'MARKETING', 'Marketing Department', 'GENERAL', 'ACTIVE'),
  (UUID(), @campus_hn, 'ADMISSION', 'Admission Department', 'GENERAL', 'ACTIVE'),
  (UUID(), @campus_hn, 'IT', 'IT Department', 'GENERAL', 'ACTIVE');

-- =====================================================================
-- 15. CHECK QUERIES
-- =====================================================================

SELECT 'PEMS v4.5 base schema created successfully - UC-based soft delete, clean HO final approval, host assignment, FAQ simple' AS message;

SELECT COUNT(*) AS table_count
FROM information_schema.tables
WHERE table_schema = DATABASE()
  AND table_type = 'BASE TABLE';

SELECT COUNT(*) AS trigger_count
FROM information_schema.triggers
WHERE trigger_schema = DATABASE();

SELECT table_name
FROM information_schema.tables
WHERE table_schema = DATABASE()
ORDER BY table_name;

-- =====================================================================
-- END OF PEMS v4.5 NEW BASE SCHEMA
-- =====================================================================
