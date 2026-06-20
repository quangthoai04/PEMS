-- =====================================================================
-- PATCH NOTE: Canonical role_code DEPARTMENT and uppercase sub_role values LEADER/STAFF/NONE.
-- PEMS v4.5 - FINAL INT AUTO_INCREMENT BUILD v8.2 CANCEL_DELEGATION
-- Generated from FINAL STRICT VISIBILITY BUILD v5.
-- Changes in this file:
--   + PATCH: visit_participants participant_role reduced to IC_HOST/IC_SUPPORT/DEPT_SUPPORT/STUDENT.
--   + Converted UUID/CHAR(36) primary keys to BIGINT UNSIGNED AUTO_INCREMENT.
--   + FIX v7.1: AUTO_INCREMENT is kept only on the real primary-key column; FK columns are plain BIGINT UNSIGNED.
--   + Converted matching FK/id columns to BIGINT UNSIGNED.
--   + role_permissions now has role_permission_id as AUTO_INCREMENT PK and
--     keeps UNIQUE(role_id, sub_role, permission_id).
--   + Rewrote seed IDs so MySQL can use numeric IDs instead of UUID strings.
--   + Preserved seed content and demo/business scenario data from the v5 SQL.
--   + Kept strict visit visibility guards and views.
--   + Auth hardening: added user_sessions cleanup indexes (expires_at, revoked_at).
-- =====================================================================

-- =====================================================================
-- PEMS v4.5 - FINAL INT AUTO_INCREMENT BUILD (v8.2 request-status + host-assignment + cancel delegation)
-- Generated rule set:
--   + 42 CREATE TABLE statements.
--   + NO temporary pre-verification visit-form table.
--   + visit_requests.registrant_nationality is kept.
--   + visit_requests.status stores request decision status only:
--       PENDING_APPROVAL, APPROVED, REJECTED, CANCELLED.
--   + Visit progress is derived from visit_request_campuses.status.
--   + UC-136.CANCEL_VISIT_REQUEST is included under Delegation Reception Management.
--   + Cancellation is a post-approval Delegation action only. Before approval, guest withdrawal is handled by reject. cancellation_reason stores both reason and external-confirmation details; no separate external note column is created.
--   + UC-48.VIEW_EMAIL is Own scope (O).
--   + ADMIN must NOT view Visit Request / Delegation business records.
--   + HO decides MULTI_CAMPUS visit requests; HO also SEES SINGLE_CAMPUS read-only (monitor) — chốt 2026-06. HO never processes SINGLE_CAMPUS (no approve/reject/assign/cancel).
--   + STAFF Leader sees:
--       - SINGLE_CAMPUS requests for their own campus;
--       - MULTI_CAMPUS requests only after HO approval, only for campuses included in the request.
-- =====================================================================

-- =====================================================================
-- PEMS v4.5
-- Revision v4-final: 42-table schema; no temporary visit-request table; visit form is created only after OTP/email verification. - NEW BASE MySQL 8.0 Schema
-- Version: 42 tables - SSO-first auth; no temporary visit-request table; revised User/Gallery/FAQ/News modules; updated Feedback/Minutes/Action Items
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
-- - Updated visit request, host assignment, simplified minutes/action items, simplified feedback, logistics proposal workflow, and news review fields.
-- - Removed temporary visit-request storage table; frontend keeps draft form temporarily until OTP/email verification succeeds.
-- - Removed user_campuses; each internal user has exactly one primary_campus_id.
-- - Removed redundant approval helper columns; backend derives approval display data from visit_scope.
-- - Removed redundant visit_request_campuses.assigned_by/assigned_at; approval actor/time already stored in visit_requests + logs.
-- - Added role_permissions.sub_role to support STAFF LEADER/STAFF and DEPARTMENT LEADER/STAFF RBAC without overgrant.
-- - Production auth is SSO-first; LOCAL_PASSWORD is kept only for DEV/test accounts.
-- - Added users.created_via='SSO_AUTO_PROVISION' for Visitor portal auto-provisioning on first SSO login.
-- - Locked approval flow:
--   + SINGLE_CAMPUS request tổng chỉ được STAFF_LEADER quyết định.
--   + MULTI_CAMPUS request tổng chỉ được HO quyết định.
--   + Campus không còn duyệt/từ chối instance sau khi request tổng được duyệt.
--   + Sau khi request tổng được duyệt, mỗi campus instance chuyển ASSIGNED và có current_host_user_id.
--   + Host mặc định là Staff Leader của campus; Staff Leader/current host có thể chuyển host cho IC Staff khác.
-- =====================================================================


-- Insert fix note:
-- - INT build converts UUID/CHAR(36) PK/FK columns to BIGINT UNSIGNED.
-- - Seed NULL calls are converted to numeric AUTO_INCREMENT-safe values or NULL where the DB should generate IDs.
-- - Final departments seed is trigger-safe: update existing rows first, then insert missing rows only.
-- - feedbacks not-self rule moved from CHECK to triggers to satisfy MySQL FK/CHECK restriction.

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
DROP TRIGGER IF EXISTS trg_visit_requests_cancel_validate_bu;
DROP TRIGGER IF EXISTS trg_visit_campuses_cancel_validate_bu;
DROP TRIGGER IF EXISTS trg_api_usage_quotas_scope_bi;
DROP TRIGGER IF EXISTS trg_api_usage_quotas_scope_bu;
DROP TRIGGER IF EXISTS trg_agenda_templates_scope_bi;
DROP TRIGGER IF EXISTS trg_agenda_templates_scope_bu;
DROP TRIGGER IF EXISTS trg_feedbacks_not_self_bi;
DROP TRIGGER IF EXISTS trg_feedbacks_not_self_bu;

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
DROP TABLE IF EXISTS faqs;
DROP TABLE IF EXISTS news_section_files;
DROP TABLE IF EXISTS news_content_sections;
DROP TABLE IF EXISTS news_translations;
DROP TABLE IF EXISTS news;
DROP TABLE IF EXISTS minute_action_items;
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
  role_id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
  role_code VARCHAR(30) NOT NULL COMMENT 'ADMIN, HO, STAFF, DEPARTMENT, STUDENT, VISITOR',
  name VARCHAR(100) NOT NULL,
  description VARCHAR(255) NULL,
  status ENUM('ACTIVE','INACTIVE') NOT NULL DEFAULT 'ACTIVE',
  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  deleted_at DATETIME NULL COMMENT 'Soft delete supported by UC-121 Disable/Delete Role',
  deleted_by BIGINT UNSIGNED NULL COMMENT 'User who soft-deleted this role; no FK here because roles is created before users',
  PRIMARY KEY (role_id),
  UNIQUE KEY uq_roles_code (role_code),
  KEY idx_roles_status_deleted (status, deleted_at),
  CHECK (role_code IN ('ADMIN','HO','STAFF','DEPARTMENT','STUDENT','VISITOR'))) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
COMMENT='6 role chính của hệ thống';

CREATE TABLE permissions (
  permission_id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
  permission_code VARCHAR(100) NOT NULL COMMENT 'Example: UC-17.SUBMIT_VISIT_REQUEST',
  name VARCHAR(150) NOT NULL,
  permission_group VARCHAR(60) NOT NULL,
  description VARCHAR(500) NULL,
  is_system BOOLEAN NOT NULL DEFAULT FALSE,
  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (permission_id),
  UNIQUE KEY uq_permissions_code (permission_code),
  KEY idx_permissions_group (permission_group),
  KEY idx_permissions_group_code (permission_group, permission_code)) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
COMMENT='Danh mục quyền theo UC/action';

CREATE TABLE role_permissions (
  role_permission_id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
  role_id BIGINT UNSIGNED NOT NULL,
  sub_role ENUM('NONE','LEADER','STAFF') NOT NULL DEFAULT 'NONE' COMMENT 'NONE for ADMIN/HO/STUDENT/VISITOR; LEADER/STAFF for STAFF and DEPARTMENT',
  permission_id BIGINT UNSIGNED NOT NULL,
  permission_level ENUM('F','E','R','O') NOT NULL COMMENT 'F=Full, E=Execute/Edit, R=Read, O=Own',
  granted_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  granted_by BIGINT UNSIGNED NULL,
  PRIMARY KEY (role_permission_id),
  UNIQUE KEY uq_role_permissions_role_sub_permission (role_id, sub_role, permission_id),
  KEY idx_role_permissions_permission (permission_id),
  KEY idx_role_permissions_role_sub_role (role_id, sub_role),
  CONSTRAINT fk_role_permissions_role
    FOREIGN KEY (role_id) REFERENCES roles(role_id)
    ON UPDATE CASCADE ON DELETE CASCADE,
  CONSTRAINT fk_role_permissions_permission
    FOREIGN KEY (permission_id) REFERENCES permissions(permission_id)
    ON UPDATE CASCADE ON DELETE CASCADE) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
COMMENT='Ma trận phân quyền theo role + sub_role + permission';

-- =====================================================================
-- 2. ORGANIZATION
-- =====================================================================

CREATE TABLE campuses (
  campus_id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
  campus_code VARCHAR(20) NOT NULL COMMENT 'HN, HCM, DN, CT, QN',
  name VARCHAR(150) NOT NULL,
  city VARCHAR(100) NULL,
  address VARCHAR(255) NULL,
  phone VARCHAR(30) NULL,
  email VARCHAR(150) NULL,
  ic_head_user_id BIGINT UNSIGNED NULL COMMENT 'FK added after users table',
  status ENUM('ACTIVE','INACTIVE') NOT NULL DEFAULT 'ACTIVE',
  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  created_by BIGINT UNSIGNED NULL,
  updated_at DATETIME NULL DEFAULT NULL ON UPDATE CURRENT_TIMESTAMP,
  updated_by BIGINT UNSIGNED NULL,
  PRIMARY KEY (campus_id),
  UNIQUE KEY uq_campuses_code (campus_code),
  KEY idx_campuses_status (status),
  KEY idx_campuses_city_status (city, status),
  KEY idx_campuses_ic_head (ic_head_user_id)) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
COMMENT='Danh mục campus';

CREATE TABLE departments (
  department_id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
  campus_id BIGINT UNSIGNED NOT NULL,
  department_code VARCHAR(50) NOT NULL,
  name VARCHAR(150) NOT NULL,
  department_type ENUM('IC','GENERAL') NOT NULL COMMENT 'IC=International Cooperation; GENERAL=other departments',
  head_user_id BIGINT UNSIGNED NULL COMMENT 'FK added after users table',
  status ENUM('ACTIVE','INACTIVE') NOT NULL DEFAULT 'ACTIVE',
  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  created_by BIGINT UNSIGNED NULL,
  updated_at DATETIME NULL DEFAULT NULL ON UPDATE CURRENT_TIMESTAMP,
  updated_by BIGINT UNSIGNED NULL,
  PRIMARY KEY (department_id),
  UNIQUE KEY uq_departments_campus_code (campus_id, department_code),
  UNIQUE KEY uq_departments_campus_name (campus_id, name),
  KEY idx_departments_campus_type (campus_id, department_type),
  KEY idx_departments_status (status),
  KEY idx_departments_head (head_user_id),
  CONSTRAINT fk_departments_campus
    FOREIGN KEY (campus_id) REFERENCES campuses(campus_id)
    ON UPDATE CASCADE ON DELETE RESTRICT) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
COMMENT='Phòng ban theo campus. STAFF thuộc IC, DEPARTMENT thuộc GENERAL';

-- =====================================================================
-- 3. USERS + AUTH
-- =====================================================================

CREATE TABLE users (
  user_id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
  full_name VARCHAR(150) NOT NULL,
  email VARCHAR(150) NOT NULL,
  phone VARCHAR(30) NULL,
  nationality VARCHAR(100) NULL COMMENT 'Quốc tịch của user/visitor',
  password_hash VARCHAR(255) NULL COMMENT 'DEV/local password hash only. Production SSO-only accounts keep this NULL.',
  role_id BIGINT UNSIGNED NOT NULL,
  sub_role ENUM('LEADER','STAFF') NULL COMMENT 'Only for STAFF/DEPARTMENT',
  primary_campus_id BIGINT UNSIGNED NULL COMMENT 'Campus duy nhất của user nội bộ. VISITOR phải NULL.',
  department_id BIGINT UNSIGNED NULL COMMENT 'STAFF = IC department; DEPARTMENT = GENERAL department',
  gender ENUM('MALE','FEMALE','OTHER','UNKNOWN') NULL,
  avatar_url VARCHAR(500) NULL,
  student_code VARCHAR(30) NULL,
  fe_id VARCHAR(100) NULL,
  status ENUM('ACTIVE','INACTIVE','LOCKED') NOT NULL DEFAULT 'ACTIVE' COMMENT 'ACTIVE=hoạt động, INACTIVE=tạm ngưng, LOCKED=bị khóa',
  email_verified_at DATETIME NULL COMMENT 'Thời điểm email được xác thực qua SSO lần đầu hoặc xác nhận bởi hệ thống',
  failed_login_count INT UNSIGNED NOT NULL DEFAULT 0 COMMENT 'Số lần đăng nhập sai local password liên tiếp; reset khi login thành công',
  locked_until DATETIME NULL COMMENT 'Thời điểm hết khóa tạm thời nếu bị lock',
  created_via ENUM('MANUAL_CREATED','VISITOR_FORM','SSO_AUTO_PROVISION') NOT NULL DEFAULT 'MANUAL_CREATED' COMMENT 'MANUAL_CREATED=HO/Staff Leader tạo, VISITOR_FORM=tạo từ form visitor, SSO_AUTO_PROVISION=tạo tự động khi đăng nhập SSO ở cổng Visitor',
  first_login_at DATETIME NULL,
  last_login_at DATETIME NULL,
  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  created_by BIGINT UNSIGNED NULL,
  updated_at DATETIME NULL DEFAULT NULL ON UPDATE CURRENT_TIMESTAMP,
  updated_by BIGINT UNSIGNED NULL,
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
  KEY idx_users_nationality (nationality),
  CONSTRAINT fk_users_role
    FOREIGN KEY (role_id) REFERENCES roles(role_id)
    ON UPDATE CASCADE ON DELETE RESTRICT,
  CONSTRAINT fk_users_primary_campus
    FOREIGN KEY (primary_campus_id) REFERENCES campuses(campus_id)
    ON UPDATE CASCADE ON DELETE RESTRICT,
  CONSTRAINT fk_users_department
    FOREIGN KEY (department_id) REFERENCES departments(department_id)
    ON UPDATE CASCADE ON DELETE RESTRICT) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
COMMENT='Tài khoản chính. Production dùng SSO; LOCAL_PASSWORD chỉ dùng DEV/test.';

ALTER TABLE campuses
  ADD CONSTRAINT fk_campuses_ic_head
  FOREIGN KEY (ic_head_user_id) REFERENCES users(user_id)
  ON UPDATE CASCADE ON DELETE SET NULL;

ALTER TABLE departments
  ADD CONSTRAINT fk_departments_head
  FOREIGN KEY (head_user_id) REFERENCES users(user_id)
  ON UPDATE CASCADE ON DELETE SET NULL;


CREATE TABLE user_auth_providers (
  auth_provider_id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
  user_id BIGINT UNSIGNED NOT NULL,
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
    ON UPDATE CASCADE ON DELETE CASCADE) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
COMMENT='Provider đăng nhập của user. Production dùng GOOGLE_SSO/FEID; LOCAL_PASSWORD chỉ dùng DEV/test.';

CREATE TABLE user_sessions (
  session_id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
  user_id BIGINT UNSIGNED NOT NULL,
  login_portal ENUM('VISITOR','INTERNAL') NOT NULL,
  selected_campus_id BIGINT UNSIGNED NULL COMMENT 'Auto set to users.primary_campus_id for INTERNAL, NULL for VISITOR',
  auth_provider_id BIGINT UNSIGNED NULL,
  refresh_token_hash VARCHAR(255) NULL COMMENT 'Refresh token hash merged into session',
  refresh_expires_at DATETIME NULL,
  refresh_revoked_at DATETIME NULL,
  ip_address VARCHAR(45) NULL,
  user_agent VARCHAR(500) NULL,
  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  expires_at DATETIME NOT NULL,
  revoked_at DATETIME NULL,
  revoked_by BIGINT UNSIGNED NULL,
  revoked_reason VARCHAR(255) NULL,
  PRIMARY KEY (session_id),
  UNIQUE KEY uq_sessions_refresh_hash (refresh_token_hash),
  KEY idx_sessions_user_active (user_id, revoked_at, expires_at),
  KEY idx_sessions_portal_campus (login_portal, selected_campus_id),
  KEY idx_sessions_refresh_active (refresh_token_hash, refresh_revoked_at, refresh_expires_at),
  KEY idx_sessions_ip_time (ip_address, created_at),
  KEY idx_sessions_expires_at (expires_at),
  KEY idx_sessions_revoked_at (revoked_at),
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
    ON UPDATE CASCADE ON DELETE SET NULL) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
COMMENT='Session + refresh token hash';

CREATE TABLE otp_tokens (
  otp_token_id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
  user_id BIGINT UNSIGNED NULL,
  email VARCHAR(150) NOT NULL,
  token_type ENUM('OTP_CODE','MAGIC_LINK') NOT NULL DEFAULT 'OTP_CODE',
  purpose ENUM('VISIT_REQUEST_VERIFY','CHANGE_SENSITIVE_ACTION') NOT NULL,
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
    ON UPDATE CASCADE ON DELETE CASCADE) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
COMMENT='OTP, magic link, set password token, reset password token';

CREATE TABLE login_logs (
  login_log_id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
  user_id BIGINT UNSIGNED NULL,
  email VARCHAR(150) NOT NULL,
  login_portal ENUM('VISITOR','INTERNAL') NOT NULL,
  selected_campus_id BIGINT UNSIGNED NULL,
  provider_type ENUM('LOCAL_PASSWORD','GOOGLE_SSO','FEID') NULL,
  status ENUM('SUCCESS','FAILED','BLOCKED') NOT NULL,
  failure_reason VARCHAR(255) NULL,
  ip_address VARCHAR(45) NULL,
  user_agent VARCHAR(500) NULL,
  session_id BIGINT UNSIGNED NULL,
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
    ON UPDATE CASCADE ON DELETE SET NULL) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
COMMENT='Lịch sử đăng nhập';

CREATE TABLE security_events (
  security_event_id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
  user_id BIGINT UNSIGNED NULL,
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
    ON UPDATE CASCADE ON DELETE SET NULL) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
COMMENT='Security, abuse, lockout events';

-- =====================================================================
-- 4. PARTNER + FILE
-- =====================================================================

CREATE TABLE partners (
  partner_id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
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
  created_by BIGINT UNSIGNED NULL,
  updated_at DATETIME NULL DEFAULT NULL ON UPDATE CURRENT_TIMESTAMP,
  updated_by BIGINT UNSIGNED NULL,
  PRIMARY KEY (partner_id),
  UNIQUE KEY uq_partners_code (partner_code),
  KEY idx_partners_country (country),
  KEY idx_partners_status (cooperation_status),
  KEY idx_partners_type_status (partner_type, cooperation_status),
  KEY idx_partners_created_at (created_at),
  FULLTEXT KEY ft_partners_search (name, short_name, description)) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
COMMENT='Hồ sơ đối tác';

CREATE TABLE partner_contacts (
  contact_id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
  partner_id BIGINT UNSIGNED NOT NULL,
  full_name VARCHAR(150) NOT NULL,
  email VARCHAR(150) NULL,
  phone VARCHAR(50) NULL,
  job_title VARCHAR(150) NULL,
  department_name VARCHAR(150) NULL,
  note TEXT NULL,
  is_primary BOOLEAN NOT NULL DEFAULT FALSE,
  status ENUM('ACTIVE','INACTIVE') NOT NULL DEFAULT 'ACTIVE',
  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  created_by BIGINT UNSIGNED NULL,
  updated_at DATETIME NULL DEFAULT NULL ON UPDATE CURRENT_TIMESTAMP,
  updated_by BIGINT UNSIGNED NULL,
  PRIMARY KEY (contact_id),
  UNIQUE KEY uq_partner_contacts_partner_email (partner_id, email),
  KEY idx_partner_contacts_partner (partner_id),
  KEY idx_partner_contacts_email (email),
  KEY idx_partner_contacts_status (status),
  CONSTRAINT fk_partner_contacts_partner
    FOREIGN KEY (partner_id) REFERENCES partners(partner_id)
    ON UPDATE CASCADE ON DELETE RESTRICT) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
COMMENT='Người liên hệ đối tác. OCR final confirmed data saved here.';

CREATE TABLE files (
  file_id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
  storage_provider ENUM('LOCAL','S3','AZURE','GCS','GOOGLE_DRIVE','OTHER') NOT NULL DEFAULT 'LOCAL',
  bucket_name VARCHAR(150) NULL,
  object_key VARCHAR(700) NOT NULL COMMENT 'Max 700 chars to keep UNIQUE index safe under utf8mb4',
  original_filename VARCHAR(255) NOT NULL,
  mime_type VARCHAR(150) NULL,
  file_size BIGINT UNSIGNED NULL,
  checksum_sha256 CHAR(64) NULL,
  visibility ENUM('PRIVATE','INTERNAL','PUBLIC') NOT NULL DEFAULT 'PRIVATE',
  uploaded_by BIGINT UNSIGNED NULL,
  uploaded_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (file_id),
  UNIQUE KEY uq_files_object_key (object_key),
  KEY idx_files_uploaded_by (uploaded_by, uploaded_at),
  KEY idx_files_visibility (visibility),
  KEY idx_files_mime_time (mime_type, uploaded_at),
  KEY idx_files_checksum (checksum_sha256),
  CONSTRAINT fk_files_uploaded_by
    FOREIGN KEY (uploaded_by) REFERENCES users(user_id)
    ON UPDATE CASCADE ON DELETE SET NULL) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
COMMENT='File metadata only. Binary file is stored outside DB.';

CREATE TABLE documents (
  document_id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
  file_id BIGINT UNSIGNED NOT NULL,
  owner_type ENUM('GENERAL','VISIT','PARTNER','MINUTES','NEWS','LOGISTICS','REPORT') NOT NULL DEFAULT 'GENERAL',
  owner_id BIGINT UNSIGNED NULL,
  campus_id BIGINT UNSIGNED NULL,
  title VARCHAR(255) NOT NULL,
  description TEXT NULL,
  document_category VARCHAR(100) NULL,
  status ENUM('DRAFT','PUBLISHED','ARCHIVED') NOT NULL DEFAULT 'DRAFT',
  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  created_by BIGINT UNSIGNED NULL,
  updated_at DATETIME NULL DEFAULT NULL ON UPDATE CURRENT_TIMESTAMP,
  updated_by BIGINT UNSIGNED NULL,
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
    ON UPDATE CASCADE ON DELETE SET NULL) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
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
  visit_request_id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
  request_code VARCHAR(50) NOT NULL,
  visitor_user_id BIGINT UNSIGNED NOT NULL COMMENT 'Visitor user/account created or linked for the registrant',
  partner_id BIGINT UNSIGNED NULL,

  -- 1. Registrant information from the Campus Visit form
  registrant_full_name VARCHAR(150) NOT NULL COMMENT 'Họ và tên người đăng ký',
  registrant_organization VARCHAR(200) NOT NULL COMMENT 'Đơn vị công tác người đăng ký',
  registrant_job_title VARCHAR(150) NULL COMMENT 'Chức danh/phòng ban người đăng ký',
  registrant_phone VARCHAR(50) NULL COMMENT 'SĐT người đăng ký',
  registrant_email VARCHAR(150) NOT NULL COMMENT 'Email người đăng ký',
  registrant_nationality VARCHAR(100) NULL COMMENT 'Quốc tịch người đăng ký',

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

  status ENUM('PENDING_APPROVAL','APPROVED','REJECTED','CANCELLED') NOT NULL DEFAULT 'PENDING_APPROVAL' COMMENT 'Request decision status only. Visit progress is derived from visit_request_campuses.status',
  submitted_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  email_verified_at DATETIME NULL,

  decided_by BIGINT UNSIGNED NULL COMMENT 'Người approve/reject request tổng',
  decided_at DATETIME NULL COMMENT 'Thời điểm xử lý request tổng',
  decision_actor_role ENUM('HO','STAFF_LEADER','SYSTEM') NULL COMMENT 'Vai trò người xử lý tại thời điểm quyết định',
  decision_note TEXT NULL COMMENT 'Lý do/ghi chú khi approve hoặc reject',

  cancelled_by BIGINT UNSIGNED NULL COMMENT 'Người thực hiện hủy request/delegation',
  cancelled_at DATETIME NULL COMMENT 'Thời điểm hủy request/delegation',
  cancellation_actor_type ENUM('VISITOR','HOST','STAFF_LEADER','HO','SYSTEM') NULL COMMENT 'Vai trò thực hiện thao tác hủy',
  cancellation_source ENUM('SELF_SERVICE','EXTERNAL_CONFIRMATION') NULL COMMENT 'SELF_SERVICE=Visitor tự hủy sau khi đơn đã duyệt; EXTERNAL_CONFIRMATION=Host hủy sau khi khách xác nhận ngoài hệ thống',
  cancellation_reason TEXT NULL COMMENT 'Lý do hủy; nếu EXTERNAL_CONFIRMATION thì ghi rõ kênh xác nhận, thời điểm, người xác nhận và lý do.',

  row_version INT UNSIGNED NOT NULL DEFAULT 0 COMMENT 'Optimistic concurrency token',

  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  created_by BIGINT UNSIGNED NULL,
  updated_at DATETIME NULL DEFAULT NULL ON UPDATE CURRENT_TIMESTAMP,
  updated_by BIGINT UNSIGNED NULL,

  PRIMARY KEY (visit_request_id),
  UNIQUE KEY uq_visit_requests_code (request_code),
  KEY idx_visit_requests_visitor (visitor_user_id),
  KEY idx_visit_requests_partner (partner_id),
  KEY idx_visit_requests_status_submitted (status, submitted_at),
  KEY idx_visit_requests_registrant_email (registrant_email),
  KEY idx_visit_requests_scope_status (visit_scope, status),
  KEY idx_visit_requests_decision (decided_by, decided_at),
  KEY idx_visit_requests_decision_role (decision_actor_role, decided_at),
  KEY idx_visit_requests_cancelled (cancelled_by, cancelled_at),
  KEY idx_visit_requests_cancel_actor (cancellation_actor_type, cancelled_at),

  CHECK (expected_guest_count >= 1),
  CHECK (
    decision_actor_role IS NULL
    OR status NOT IN ('APPROVED','REJECTED')
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
    ON UPDATE CASCADE ON DELETE SET NULL,
  CONSTRAINT fk_visit_requests_cancelled_by
    FOREIGN KEY (cancelled_by) REFERENCES users(user_id)
    ON UPDATE CASCADE ON DELETE SET NULL) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
COMMENT='Đơn đăng ký tham quan. Record chỉ được tạo sau khi email/OTP đã xác minh; nội dung form không sửa sau khi submit vào PENDING_APPROVAL; tiến trình thực tế theo visit_request_campuses.';

CREATE TABLE visit_request_campuses (
  visit_instance_id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
  visit_request_id BIGINT UNSIGNED NOT NULL,
  campus_id BIGINT UNSIGNED NOT NULL,
  instance_code VARCHAR(60) NULL,

  planned_start_at DATETIME NOT NULL COMMENT 'Ngày giờ bắt đầu dự kiến tại campus',
  planned_end_at DATETIME NOT NULL COMMENT 'Ngày giờ kết thúc dự kiến tại campus',

  status ENUM(
    'WAITING_REQUEST_APPROVAL',
    'ASSIGNED',
    'BEFORE_VISIT',
    'DURING_VISIT',
    'AFTER_VISIT',
    'CLOSED',
    'CANCELLED'
  ) NOT NULL DEFAULT 'WAITING_REQUEST_APPROVAL',

  current_host_user_id BIGINT UNSIGNED NULL
    COMMENT 'Host hiện tại chịu trách nhiệm campus instance. Sau khi request tổng được duyệt thì phải có host; nếu đổi host dùng chức năng Transfer Host',

  host_assigned_by BIGINT UNSIGNED NULL COMMENT 'Người gây ra thao tác gán host: HO khi auto gán Staff Leader cho multi-campus, Staff Leader khi duyệt single-campus, hoặc người chuyển host',
  host_assigned_at DATETIME NULL COMMENT 'Thời điểm host được gán',
  host_assignment_source ENUM('AUTO_STAFF_LEADER','MANUAL_APPROVAL','TRANSFERRED') NULL
    COMMENT 'AUTO_STAFF_LEADER=HO duyệt liên cơ sở và hệ thống tự gán Staff Leader; MANUAL_APPROVAL=Staff Leader duyệt đơn một cơ sở và chọn host; TRANSFERRED=host được chuyển sau đó',

  host_transferred_by BIGINT UNSIGNED NULL COMMENT 'Người chuyển host gần nhất',
  host_transferred_at DATETIME NULL COMMENT 'Thời điểm chuyển host gần nhất',
  host_transfer_note TEXT NULL COMMENT 'Ghi chú/lý do chuyển host gần nhất',

  closed_by BIGINT UNSIGNED NULL,
  closed_at DATETIME NULL,
  close_note TEXT NULL,

  cancelled_by BIGINT UNSIGNED NULL COMMENT 'Người thực hiện hủy campus instance',
  cancelled_at DATETIME NULL COMMENT 'Thời điểm hủy campus instance',
  cancellation_actor_type ENUM('VISITOR','HOST','STAFF_LEADER','HO','SYSTEM') NULL COMMENT 'Vai trò thực hiện thao tác hủy campus instance',
  cancellation_source ENUM('SELF_SERVICE','EXTERNAL_CONFIRMATION') NULL COMMENT 'SELF_SERVICE=Visitor tự hủy sau khi đơn đã duyệt; EXTERNAL_CONFIRMATION=Host hủy sau khi khách xác nhận ngoài hệ thống',
  cancellation_reason TEXT NULL COMMENT 'Lý do hủy; nếu EXTERNAL_CONFIRMATION thì ghi rõ kênh xác nhận, thời điểm, người xác nhận và lý do.',

  row_version INT UNSIGNED NOT NULL DEFAULT 0 COMMENT 'Optimistic concurrency token',

  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  created_by BIGINT UNSIGNED NULL,
  updated_at DATETIME NULL DEFAULT NULL ON UPDATE CURRENT_TIMESTAMP,
  updated_by BIGINT UNSIGNED NULL,

  PRIMARY KEY (visit_instance_id),
  UNIQUE KEY uq_visit_instance_request_campus (visit_request_id, campus_id),
  UNIQUE KEY uq_visit_instance_code (instance_code),
  KEY idx_visit_instances_campus_status_time (campus_id, status, planned_start_at),
  KEY idx_visit_instances_request (visit_request_id),
  KEY idx_visit_instances_status_time (status, planned_start_at),
  KEY idx_visit_instances_current_host (current_host_user_id, status),
  KEY idx_visit_instances_host_assigned (host_assigned_by, host_assigned_at),
  KEY idx_visit_instances_assignment_source (host_assignment_source, host_assigned_at),
  KEY idx_visit_instances_host_transfer (host_transferred_by, host_transferred_at),
  KEY idx_visit_instances_cancelled (cancelled_by, cancelled_at),
  KEY idx_visit_instances_cancel_actor (cancellation_actor_type, cancelled_at),

  CHECK (planned_end_at > planned_start_at),

  CONSTRAINT fk_visit_instances_request
    FOREIGN KEY (visit_request_id) REFERENCES visit_requests(visit_request_id)
    ON UPDATE CASCADE ON DELETE RESTRICT,
  CONSTRAINT fk_visit_instances_campus
    FOREIGN KEY (campus_id) REFERENCES campuses(campus_id)
    ON UPDATE CASCADE ON DELETE RESTRICT,
  CONSTRAINT fk_visit_instances_current_host
    FOREIGN KEY (current_host_user_id) REFERENCES users(user_id)
    ON UPDATE CASCADE ON DELETE SET NULL,
  CONSTRAINT fk_visit_instances_host_assigned_by
    FOREIGN KEY (host_assigned_by) REFERENCES users(user_id)
    ON UPDATE CASCADE ON DELETE SET NULL,
  CONSTRAINT fk_visit_instances_host_transferred_by
    FOREIGN KEY (host_transferred_by) REFERENCES users(user_id)
    ON UPDATE CASCADE ON DELETE SET NULL,
  CONSTRAINT fk_visit_instances_closed_by
    FOREIGN KEY (closed_by) REFERENCES users(user_id)
    ON UPDATE CASCADE ON DELETE SET NULL,
  CONSTRAINT fk_visit_instances_cancelled_by
    FOREIGN KEY (cancelled_by) REFERENCES users(user_id)
    ON UPDATE CASCADE ON DELETE SET NULL) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
COMMENT='Mỗi campus trong request có một instance riêng. WAITING_REQUEST_APPROVAL chỉ dùng trước khi đơn tổng được duyệt; sau duyệt phải gán host ngay và chuyển ASSIGNED. Không lưu actual_start_at/actual_end_at.';

CREATE TABLE visit_guest_members (
  guest_member_id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
  visit_request_id BIGINT UNSIGNED NOT NULL,
  full_name VARCHAR(150) NOT NULL,
  organization VARCHAR(200) NULL,
  job_title VARCHAR(150) NULL,
  nationality VARCHAR(100) NULL,
  email VARCHAR(150) NULL,
  phone VARCHAR(50) NULL,
  is_representative BOOLEAN NOT NULL DEFAULT FALSE,
  note TEXT NULL,
  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  created_by BIGINT UNSIGNED NULL,
  updated_at DATETIME NULL DEFAULT NULL ON UPDATE CURRENT_TIMESTAMP,
  updated_by BIGINT UNSIGNED NULL,
  PRIMARY KEY (guest_member_id),
  KEY idx_guest_members_request (visit_request_id),
  KEY idx_guest_members_email (email),
  KEY idx_guest_members_representative (visit_request_id, is_representative),
  CONSTRAINT fk_guest_members_request
    FOREIGN KEY (visit_request_id) REFERENCES visit_requests(visit_request_id)
    ON UPDATE CASCADE ON DELETE RESTRICT) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
COMMENT='Danh sách từng người trong đoàn khách. Không lưu consent hình ảnh vì form đã bỏ phần xác nhận sử dụng hình ảnh/thông tin.';

CREATE TABLE visit_participants (
  participant_id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
  visit_instance_id BIGINT UNSIGNED NOT NULL,
  user_id BIGINT UNSIGNED NOT NULL,
  participant_role ENUM('IC_HOST','IC_SUPPORT','DEPT_SUPPORT','STUDENT') NOT NULL DEFAULT 'IC_SUPPORT',
  is_host BOOLEAN NOT NULL DEFAULT FALSE,
  status ENUM('INVITED','ACCEPTED','DECLINED','ASSIGNED','REMOVED') NOT NULL DEFAULT 'INVITED',
  invited_by BIGINT UNSIGNED NULL,
  invited_at DATETIME NULL,
  responded_at DATETIME NULL,
  assigned_by BIGINT UNSIGNED NULL,
  assigned_at DATETIME NULL,
  note TEXT NULL,
  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  created_by BIGINT UNSIGNED NULL,
  updated_at DATETIME NULL DEFAULT NULL ON UPDATE CURRENT_TIMESTAMP,
  updated_by BIGINT UNSIGNED NULL,
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
    ON UPDATE CASCADE ON DELETE SET NULL) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
COMMENT='Người nội bộ tham gia visit instance. Chỉ gồm IC_HOST, IC_SUPPORT, DEPT_SUPPORT, STUDENT. Host chính lưu bằng is_host.';

CREATE TABLE visit_agendas (
  agenda_id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
  visit_instance_id BIGINT UNSIGNED NOT NULL,
  sequence_order INT UNSIGNED NOT NULL,
  title VARCHAR(255) NOT NULL,
  description TEXT NULL,
  start_time DATETIME NOT NULL,
  end_time DATETIME NULL,
  location VARCHAR(255) NULL,
  responsible_user_id BIGINT UNSIGNED NULL,
  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  created_by BIGINT UNSIGNED NULL,
  updated_at DATETIME NULL DEFAULT NULL ON UPDATE CURRENT_TIMESTAMP,
  updated_by BIGINT UNSIGNED NULL,
  PRIMARY KEY (agenda_id),
  UNIQUE KEY uq_visit_agendas_order (visit_instance_id, sequence_order),
  KEY idx_visit_agendas_time (visit_instance_id, start_time),
  KEY idx_visit_agendas_responsible (responsible_user_id, start_time),
  CONSTRAINT fk_visit_agendas_instance
    FOREIGN KEY (visit_instance_id) REFERENCES visit_request_campuses(visit_instance_id)
    ON UPDATE CASCADE ON DELETE RESTRICT,
  CONSTRAINT fk_visit_agendas_responsible_user
    FOREIGN KEY (responsible_user_id) REFERENCES users(user_id)
    ON UPDATE CASCADE ON DELETE SET NULL) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
COMMENT='Lịch trình tiếp khách';

CREATE TABLE visit_logistics_items (
  logistics_item_id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
  visit_instance_id BIGINT UNSIGNED NOT NULL,

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

  requested_by BIGINT UNSIGNED NULL COMMENT 'Người gửi yêu cầu hậu cần/resource',
  requested_to_department_id BIGINT UNSIGNED NULL COMMENT 'Phòng ban được yêu cầu xử lý',
  requested_at DATETIME NULL COMMENT 'Thời điểm gửi yêu cầu',

  received_by BIGINT UNSIGNED NULL COMMENT 'Trưởng phòng/người tiếp nhận yêu cầu',
  received_at DATETIME NULL COMMENT 'Thời điểm tiếp nhận yêu cầu',

  assigned_to_user_id BIGINT UNSIGNED NULL COMMENT 'Nhân viên được giao xử lý chính',
  assigned_by BIGINT UNSIGNED NULL COMMENT 'Người phân công',
  assigned_at DATETIME NULL COMMENT 'Thời điểm phân công',

  assignee_accepted_at DATETIME NULL COMMENT 'Thời điểm nhân viên xác nhận nhận nhiệm vụ',
  assignee_response_note TEXT NULL COMMENT 'Ghi chú khi nhân viên nhận/từ chối nếu có',

  due_at DATETIME NULL COMMENT 'Deadline hoàn thành hạng mục',
  completed_at DATETIME NULL COMMENT 'Thời điểm hoàn thành',

  proposed_by BIGINT UNSIGNED NULL COMMENT 'Người gửi đề xuất thay đổi',
  proposed_at DATETIME NULL COMMENT 'Thời điểm gửi đề xuất thay đổi',
  proposed_quantity INT UNSIGNED NULL COMMENT 'Số lượng được đề xuất thay đổi',
  proposed_usage_start_at DATETIME NULL COMMENT 'Thời gian bắt đầu sử dụng được đề xuất',
  proposed_usage_end_at DATETIME NULL COMMENT 'Thời gian kết thúc sử dụng được đề xuất',
  proposed_description TEXT NULL COMMENT 'Nội dung chi tiết công việc được đề xuất thay đổi',
  proposal_note TEXT NULL COMMENT 'Lý do/ghi chú đề xuất thay đổi',

  proposal_responded_by BIGINT UNSIGNED NULL COMMENT 'Người xác nhận/từ chối đề xuất',
  proposal_responded_at DATETIME NULL COMMENT 'Thời điểm xác nhận/từ chối đề xuất',
  proposal_response ENUM('ACCEPTED','REJECTED') NULL COMMENT 'Kết quả phản hồi đề xuất',
  proposal_response_note TEXT NULL COMMENT 'Ghi chú phản hồi đề xuất',

  decision_note TEXT NULL COMMENT 'Lý do reject/cancel hoặc ghi chú xử lý',

  row_version INT UNSIGNED NOT NULL DEFAULT 0 COMMENT 'Optimistic concurrency token',

  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  created_by BIGINT UNSIGNED NULL,
  updated_at DATETIME NULL DEFAULT NULL ON UPDATE CURRENT_TIMESTAMP,
  updated_by BIGINT UNSIGNED NULL,

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
    ON UPDATE CASCADE ON DELETE SET NULL) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
COMMENT='Yêu cầu hậu cần/resource cho visit: gửi yêu cầu, đề xuất thay đổi, tiếp nhận, phân công, xác nhận và hoàn thành. Thay thế tasks cho logistics/resource.';

-- =====================================================================
-- 6. MINUTES + FEEDBACK
-- =====================================================================
-- Final simplified design:
-- - minutes: main meeting minutes only; no embedded attachment/action JSON fields.
-- - minute_action_items: separate CRUD for action items with note, deadline, status.
-- - feedbacks: one row per submitted feedback, because host clicks each target separately.
-- - All submitters/targets must be system users, so feedback only stores submitted_by_user_id and target_user_id.

CREATE TABLE minutes (
  minutes_id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
  visit_instance_id BIGINT UNSIGNED NOT NULL,

  title VARCHAR(255) NOT NULL,
  content LONGTEXT NULL,

  participants_json JSON NULL COMMENT 'Danh sách người tham gia trong biên bản, lưu dạng snapshot nếu cần hiển thị lại',

  status ENUM('DRAFT','FINAL') NOT NULL DEFAULT 'DRAFT'
    COMMENT 'DRAFT=đang soạn, FINAL=đã chốt',

  finalized_by BIGINT UNSIGNED NULL COMMENT 'Người chốt biên bản',
  finalized_at DATETIME NULL COMMENT 'Thời điểm chốt biên bản',

  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  created_by BIGINT UNSIGNED NULL,
  updated_at DATETIME NULL DEFAULT NULL ON UPDATE CURRENT_TIMESTAMP,
  updated_by BIGINT UNSIGNED NULL,

  PRIMARY KEY (minutes_id),

  KEY idx_minutes_visit_status (visit_instance_id, status),
  KEY idx_minutes_created_by_time (created_by, created_at),
  KEY idx_minutes_finalized_by_time (finalized_by, finalized_at),

  FULLTEXT KEY ft_minutes_search (title, content),

  CONSTRAINT fk_minutes_visit_instance
    FOREIGN KEY (visit_instance_id) REFERENCES visit_request_campuses(visit_instance_id)
    ON UPDATE CASCADE ON DELETE RESTRICT,

  CONSTRAINT fk_minutes_created_by
    FOREIGN KEY (created_by) REFERENCES users(user_id)
    ON UPDATE CASCADE ON DELETE SET NULL,

  CONSTRAINT fk_minutes_updated_by
    FOREIGN KEY (updated_by) REFERENCES users(user_id)
    ON UPDATE CASCADE ON DELETE SET NULL,

  CONSTRAINT fk_minutes_finalized_by
    FOREIGN KEY (finalized_by) REFERENCES users(user_id)
    ON UPDATE CASCADE ON DELETE SET NULL) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
COMMENT='Biên bản chuyến thăm. Không lưu file đính kèm và không lưu action item dạng JSON; action item tách bảng riêng.';

CREATE TABLE minute_action_items (
  action_item_id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
  minutes_id BIGINT UNSIGNED NOT NULL,

  title VARCHAR(255) NOT NULL COMMENT 'Tên đầu việc',
  note TEXT NULL COMMENT 'Ghi chú thêm cho đầu việc',

  due_date DATE NULL COMMENT 'Deadline của đầu việc',

  status ENUM('TODO','IN_PROGRESS','DONE','CANCELLED') NOT NULL DEFAULT 'TODO'
    COMMENT 'TODO=chưa làm, IN_PROGRESS=đang làm, DONE=hoàn thành, CANCELLED=đã hủy/không cần làm nữa',

  completed_at DATETIME NULL COMMENT 'Thời điểm hoàn thành; backend tự set khi status chuyển sang DONE',

  display_order INT UNSIGNED NOT NULL DEFAULT 1 COMMENT 'Thứ tự hiển thị trong biên bản',

  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  created_by BIGINT UNSIGNED NULL,
  updated_at DATETIME NULL DEFAULT NULL ON UPDATE CURRENT_TIMESTAMP,
  updated_by BIGINT UNSIGNED NULL,

  PRIMARY KEY (action_item_id),

  KEY idx_action_items_minutes (minutes_id),
  KEY idx_action_items_status_due (status, due_date),
  KEY idx_action_items_order (minutes_id, display_order),
  KEY idx_action_items_created_by_time (created_by, created_at),

  CONSTRAINT fk_action_items_minutes
    FOREIGN KEY (minutes_id) REFERENCES minutes(minutes_id)
    ON UPDATE CASCADE ON DELETE CASCADE,

  CONSTRAINT fk_action_items_created_by
    FOREIGN KEY (created_by) REFERENCES users(user_id)
    ON UPDATE CASCADE ON DELETE SET NULL,

  CONSTRAINT fk_action_items_updated_by
    FOREIGN KEY (updated_by) REFERENCES users(user_id)
    ON UPDATE CASCADE ON DELETE SET NULL) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
COMMENT='Các đầu việc sau biên bản. Không gán người phụ trách; chỉ có note, deadline và trạng thái hoàn thành.';

CREATE TABLE feedbacks (
  feedback_id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,

  visit_request_id BIGINT UNSIGNED NOT NULL,
  visit_instance_id BIGINT UNSIGNED NULL,

  submitted_by_user_id BIGINT UNSIGNED NOT NULL COMMENT 'User gửi feedback; khách/host/logistics đều phải có tài khoản hệ thống',
  submitter_role ENUM('VISITOR','HOST','LOGISTICS') NOT NULL COMMENT 'Vai trò người gửi trong chuyến thăm',
  submitter_context VARCHAR(120) NOT NULL DEFAULT ''
    COMMENT 'Ngữ cảnh vai trò người gửi, ví dụ: Host chính, Xe điện, Teabreak, Khách đại diện',
  submitter_name_snapshot VARCHAR(255) NOT NULL
    COMMENT 'Tên người gửi tại thời điểm gửi feedback',

  target_user_id BIGINT UNSIGNED NOT NULL COMMENT 'User được đánh giá',
  target_role ENUM('VISITOR','HOST','LOGISTICS') NOT NULL COMMENT 'Vai trò người được đánh giá trong chuyến thăm',
  target_context VARCHAR(120) NOT NULL DEFAULT ''
    COMMENT 'Ngữ cảnh đối tượng được đánh giá, ví dụ: Host chính, Đoàn khách, Xe điện, Teabreak',
  target_name_snapshot VARCHAR(255) NOT NULL
    COMMENT 'Tên người được đánh giá tại thời điểm gửi feedback',

  rating TINYINT UNSIGNED NOT NULL COMMENT 'Số sao từ 1 đến 5',
  comment TEXT NOT NULL COMMENT 'Nội dung feedback',

  submitted_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,

  PRIMARY KEY (feedback_id),

  KEY idx_feedbacks_visit_request (visit_request_id),
  KEY idx_feedbacks_visit_instance (visit_instance_id),
  KEY idx_feedbacks_submitter (submitted_by_user_id),
  KEY idx_feedbacks_target (target_user_id),
  KEY idx_feedbacks_roles (submitter_role, target_role),
  KEY idx_feedbacks_rating (rating),
  KEY idx_feedbacks_submitted_at (submitted_at),

  CONSTRAINT chk_feedbacks_rating
    CHECK (rating BETWEEN 1 AND 5),


  CONSTRAINT chk_feedbacks_role_flow
    CHECK (
      (submitter_role IN ('VISITOR','LOGISTICS') AND target_role = 'HOST')
      OR
      (submitter_role = 'HOST' AND target_role IN ('VISITOR','LOGISTICS'))
    ),

  CONSTRAINT fk_feedbacks_visit_request
    FOREIGN KEY (visit_request_id) REFERENCES visit_requests(visit_request_id)
    ON UPDATE CASCADE ON DELETE RESTRICT,

  CONSTRAINT fk_feedbacks_visit_instance
    FOREIGN KEY (visit_instance_id) REFERENCES visit_request_campuses(visit_instance_id)
    ON UPDATE CASCADE ON DELETE SET NULL,

  CONSTRAINT fk_feedbacks_submitter
    FOREIGN KEY (submitted_by_user_id) REFERENCES users(user_id)
    ON UPDATE CASCADE ON DELETE RESTRICT,

  CONSTRAINT fk_feedbacks_target
    FOREIGN KEY (target_user_id) REFERENCES users(user_id)
    ON UPDATE CASCADE ON DELETE RESTRICT) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
COMMENT='Feedback đơn giản: mỗi dòng là một đánh giá giữa hai user trong một visit. Khách/logistics đánh giá host; host đánh giá khách hoặc logistics.';

-- =====================================================================
-- 7. PUBLIC CONTENT
-- =====================================================================

CREATE TABLE news (
  news_id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
  campus_id BIGINT UNSIGNED NULL COMMENT 'Campus liên quan đến bài viết. NULL nếu bài toàn hệ thống',
  visit_instance_id BIGINT UNSIGNED NULL COMMENT 'Visit instance liên quan nếu bài viết được tạo từ một chuyến tiếp đón',
  author_user_id BIGINT UNSIGNED NOT NULL COMMENT 'Người tạo/viết bài',
  cover_file_id BIGINT UNSIGNED NULL COMMENT 'Ảnh bìa bài viết, trỏ tới files.file_id',
  status ENUM('PENDING_REVIEW','REJECTED','PUBLISHED','HIDDEN') NOT NULL DEFAULT 'PENDING_REVIEW'
    COMMENT 'PENDING_REVIEW=chờ host duyệt, REJECTED=bị từ chối, PUBLISHED=đã đăng, HIDDEN=ẩn khỏi trang tin',
  submitted_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT 'Thời điểm người viết gửi bài cho host duyệt',

  reviewed_by BIGINT UNSIGNED NULL COMMENT 'Host duyệt hoặc từ chối bài viết',
  reviewed_at DATETIME NULL COMMENT 'Thời điểm host duyệt hoặc từ chối',
  review_note TEXT NULL COMMENT 'Ghi chú duyệt hoặc lý do từ chối',

  published_at DATETIME NULL COMMENT 'Thời điểm bài viết được đăng',
  is_featured BOOLEAN NOT NULL DEFAULT FALSE COMMENT 'Bài viết nổi bật',
  row_version INT UNSIGNED NOT NULL DEFAULT 0 COMMENT 'Optimistic concurrency token, chống ghi đè khi cập nhật đồng thời',

  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  created_by BIGINT UNSIGNED NULL,
  updated_at DATETIME NULL DEFAULT NULL ON UPDATE CURRENT_TIMESTAMP,
  updated_by BIGINT UNSIGNED NULL,

  PRIMARY KEY (news_id),
  KEY idx_news_public (status, campus_id, published_at),
  KEY idx_news_author_status (author_user_id, status),
  KEY idx_news_visit_instance_status (visit_instance_id, status),
  KEY idx_news_review (reviewed_by, reviewed_at),
  KEY idx_news_featured (is_featured, status, published_at),

  CONSTRAINT fk_news_campus
    FOREIGN KEY (campus_id) REFERENCES campuses(campus_id)
    ON UPDATE CASCADE ON DELETE SET NULL,
  CONSTRAINT fk_news_visit_instance
    FOREIGN KEY (visit_instance_id) REFERENCES visit_request_campuses(visit_instance_id)
    ON UPDATE CASCADE ON DELETE SET NULL,
  CONSTRAINT fk_news_author
    FOREIGN KEY (author_user_id) REFERENCES users(user_id)
    ON UPDATE CASCADE ON DELETE RESTRICT,
  CONSTRAINT fk_news_cover_file
    FOREIGN KEY (cover_file_id) REFERENCES files(file_id)
    ON UPDATE CASCADE ON DELETE SET NULL,
  CONSTRAINT fk_news_reviewed_by
    FOREIGN KEY (reviewed_by) REFERENCES users(user_id)
    ON UPDATE CASCADE ON DELETE SET NULL) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
COMMENT='News metadata. Người tham gia gửi bài, host duyệt/từ chối; nội dung chia theo section.';

CREATE TABLE news_translations (
  news_translation_id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
  news_id BIGINT UNSIGNED NOT NULL,
  language_code ENUM('vi','en','zh','ja','ko') NOT NULL DEFAULT 'vi',
  title VARCHAR(255) NOT NULL COMMENT 'Tiêu đề chính của bài viết',
  slug VARCHAR(255) NOT NULL COMMENT 'Đường dẫn SEO của bài viết',
  summary TEXT NULL COMMENT 'Tóm tắt bài viết',
  seo_title VARCHAR(255) NULL,
  seo_description VARCHAR(500) NULL,
  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  updated_at DATETIME NULL DEFAULT NULL ON UPDATE CURRENT_TIMESTAMP,
  PRIMARY KEY (news_translation_id),
  UNIQUE KEY uq_news_translation_lang (news_id, language_code),
  UNIQUE KEY uq_news_translation_slug_lang (slug, language_code),
  KEY idx_news_translations_lang (language_code),
  FULLTEXT KEY ft_news_translations_search (title, summary),
  CONSTRAINT fk_news_translations_news
    FOREIGN KEY (news_id) REFERENCES news(news_id)
    ON UPDATE CASCADE ON DELETE CASCADE) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
COMMENT='Tiêu đề, slug, tóm tắt và SEO của bài viết theo ngôn ngữ';

CREATE TABLE news_content_sections (
  section_id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
  news_translation_id BIGINT UNSIGNED NOT NULL,
  section_order TINYINT UNSIGNED NOT NULL COMMENT 'Thứ tự section, từ 1 đến 10',
  section_title VARCHAR(255) NOT NULL COMMENT 'Tiêu đề section',
  section_body_html LONGTEXT NOT NULL COMMENT 'Nội dung rich text dạng HTML đã sanitize, có thể chứa paragraph, bold, italic, color, link, image',
  section_body_text TEXT NULL COMMENT 'Plain text tách từ HTML để search hoặc preview',
  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  updated_at DATETIME NULL DEFAULT NULL ON UPDATE CURRENT_TIMESTAMP,
  PRIMARY KEY (section_id),
  UNIQUE KEY uq_news_section_order (news_translation_id, section_order),
  KEY idx_news_sections_translation (news_translation_id),
  FULLTEXT KEY ft_news_sections_search (section_title, section_body_text),
  CHECK (section_order BETWEEN 1 AND 10),
  CONSTRAINT fk_news_sections_translation
    FOREIGN KEY (news_translation_id) REFERENCES news_translations(news_translation_id)
    ON UPDATE CASCADE ON DELETE CASCADE) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
COMMENT='Các khối nội dung chi tiết của bài viết, tối đa 10 section mỗi bản dịch';

CREATE TABLE news_section_files (
  section_file_id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
  section_id BIGINT UNSIGNED NOT NULL,
  file_id BIGINT UNSIGNED NOT NULL,
  usage_type ENUM('INLINE_IMAGE','ATTACHMENT') NOT NULL DEFAULT 'INLINE_IMAGE'
    COMMENT 'INLINE_IMAGE=ảnh chèn trong nội dung, ATTACHMENT=file đính kèm',
  display_order INT UNSIGNED NOT NULL DEFAULT 0,
  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (section_file_id),
  UNIQUE KEY uq_news_section_file (section_id, file_id),
  KEY idx_news_section_files_section (section_id),
  KEY idx_news_section_files_file (file_id),
  CONSTRAINT fk_news_section_files_section
    FOREIGN KEY (section_id) REFERENCES news_content_sections(section_id)
    ON UPDATE CASCADE ON DELETE CASCADE,
  CONSTRAINT fk_news_section_files_file
    FOREIGN KEY (file_id) REFERENCES files(file_id)
    ON UPDATE CASCADE ON DELETE RESTRICT) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
COMMENT='File/ảnh được dùng trong từng section của bài news';

CREATE TABLE faqs (
  faq_id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
  category VARCHAR(100) NULL COMMENT 'Nhóm FAQ, ví dụ: Visit Request, Security, Logistics',
  question VARCHAR(500) NOT NULL COMMENT 'Câu hỏi FAQ',
  answer TEXT NOT NULL COMMENT 'Câu trả lời FAQ',
  display_order INT UNSIGNED NOT NULL DEFAULT 0,
  status ENUM('PUBLISHED','HIDDEN') NOT NULL DEFAULT 'HIDDEN'
    COMMENT 'PUBLISHED=hiển thị trên trang FAQ, HIDDEN=ẩn khỏi người xem thường nhưng người quản lý vẫn thấy',
  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  created_by BIGINT UNSIGNED NULL,
  updated_at DATETIME NULL DEFAULT NULL ON UPDATE CURRENT_TIMESTAMP,
  updated_by BIGINT UNSIGNED NULL,
  PRIMARY KEY (faq_id),
  KEY idx_faqs_status_order (status, display_order),
  KEY idx_faqs_category_status (category, status),
  FULLTEXT KEY ft_faqs_search (question, answer)) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
COMMENT='FAQ một ngôn ngữ, chỉ dùng PUBLISHED/HIDDEN';

-- =====================================================================
-- 8. GALLERY + FACE TAGGING
-- =====================================================================

CREATE TABLE galleries (
  gallery_id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
  campus_id BIGINT UNSIGNED NOT NULL,
  location_name VARCHAR(150) NOT NULL COMMENT 'Tên địa điểm trong campus, ví dụ: Sảnh Alpha, Green Lab, Thư viện',
  title VARCHAR(255) NOT NULL COMMENT 'Tên hiển thị của gallery/địa điểm',
  description TEXT NULL COMMENT 'Mô tả ngắn về địa điểm',
  story_content TEXT NULL COMMENT 'Ý nghĩa hoặc câu chuyện giới thiệu về địa điểm',
  status ENUM('DRAFT','PUBLISHED','HIDDEN') NOT NULL DEFAULT 'DRAFT'
    COMMENT 'DRAFT=nháp, PUBLISHED=hiển thị theo visibility, HIDDEN=ẩn khỏi người xem thường nhưng Staff Leader vẫn quản lý được',
  visibility ENUM('PRIVATE','INTERNAL','PUBLIC') NOT NULL DEFAULT 'INTERNAL'
    COMMENT 'Phạm vi xem khi status=PUBLISHED: PRIVATE=chỉ quản lý, INTERNAL=user nội bộ, PUBLIC=công khai',
  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  created_by BIGINT UNSIGNED NULL,
  updated_at DATETIME NULL DEFAULT NULL ON UPDATE CURRENT_TIMESTAMP,
  updated_by BIGINT UNSIGNED NULL,
  deleted_at DATETIME NULL,
  deleted_by BIGINT UNSIGNED NULL,
  PRIMARY KEY (gallery_id),
  KEY idx_galleries_campus_status (campus_id, status, deleted_at),
  KEY idx_galleries_location_name (location_name),
  KEY idx_galleries_visibility_status (visibility, status),
  CONSTRAINT fk_galleries_campus
    FOREIGN KEY (campus_id) REFERENCES campuses(campus_id)
    ON UPDATE CASCADE ON DELETE RESTRICT) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
COMMENT='Gallery địa điểm trong campus, có mô tả và câu chuyện';

CREATE TABLE gallery_images (
  image_id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
  gallery_id BIGINT UNSIGNED NOT NULL,
  file_id BIGINT UNSIGNED NOT NULL,
  caption VARCHAR(500) NULL COMMENT 'Chú thích riêng cho từng ảnh',
  display_order INT UNSIGNED NOT NULL DEFAULT 0,
  taken_at DATETIME NULL,
  status ENUM('ACTIVE','HIDDEN') NOT NULL DEFAULT 'ACTIVE'
    COMMENT 'ACTIVE=ảnh đang dùng, HIDDEN=ảnh bị ẩn khỏi gallery thường',
  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  created_by BIGINT UNSIGNED NULL,
  updated_at DATETIME NULL DEFAULT NULL ON UPDATE CURRENT_TIMESTAMP,
  updated_by BIGINT UNSIGNED NULL,
  deleted_at DATETIME NULL,
  deleted_by BIGINT UNSIGNED NULL,
  PRIMARY KEY (image_id),
  UNIQUE KEY uq_gallery_images_file (file_id),
  KEY idx_gallery_images_gallery_order (gallery_id, display_order),
  KEY idx_gallery_images_status_time (status, taken_at),
  CONSTRAINT fk_gallery_images_gallery
    FOREIGN KEY (gallery_id) REFERENCES galleries(gallery_id)
    ON UPDATE CASCADE ON DELETE RESTRICT,
  CONSTRAINT fk_gallery_images_file
    FOREIGN KEY (file_id) REFERENCES files(file_id)
    ON UPDATE CASCADE ON DELETE RESTRICT) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
COMMENT='Ảnh thuộc gallery địa điểm campus';

CREATE TABLE photo_face_tags (
  face_tag_id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
  image_id BIGINT UNSIGNED NOT NULL,
  visit_request_id BIGINT UNSIGNED NULL,
  guest_member_id BIGINT UNSIGNED NULL,
  partner_contact_id BIGINT UNSIGNED NULL,
  display_name VARCHAR(150) NOT NULL,
  bounding_box_x DECIMAL(8,4) NULL,
  bounding_box_y DECIMAL(8,4) NULL,
  bounding_box_width DECIMAL(8,4) NULL,
  bounding_box_height DECIMAL(8,4) NULL,
  tag_status ENUM('MANUALLY_TAGGED','CONFIRMED','REMOVED') NOT NULL DEFAULT 'MANUALLY_TAGGED',
  confirmed_by BIGINT UNSIGNED NULL,
  confirmed_at DATETIME NULL,
  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  created_by BIGINT UNSIGNED NULL,
  removed_at DATETIME NULL,
  removed_by BIGINT UNSIGNED NULL,
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
    ON UPDATE CASCADE ON DELETE SET NULL) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
COMMENT='Confirmed face tag metadata only. No biometric vector.';

-- =====================================================================
-- 9. EMAIL + NOTIFICATION
-- =====================================================================

CREATE TABLE email_templates (
  email_template_id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
  template_code VARCHAR(100) NOT NULL,
  name VARCHAR(150) NOT NULL,
  purpose VARCHAR(100) NOT NULL,
  status ENUM('ACTIVE','INACTIVE') NOT NULL DEFAULT 'ACTIVE',
  translations_json JSON NOT NULL COMMENT 'Merged email_template_translations table',
  variables_json JSON NULL COMMENT 'Allowed variables: FullName, OtpCode, Link...',
  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  created_by BIGINT UNSIGNED NULL,
  updated_at DATETIME NULL DEFAULT NULL ON UPDATE CURRENT_TIMESTAMP,
  updated_by BIGINT UNSIGNED NULL,
  PRIMARY KEY (email_template_id),
  UNIQUE KEY uq_email_templates_code (template_code),
  KEY idx_email_templates_status (status),
  KEY idx_email_templates_purpose_status (purpose, status)) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
COMMENT='Email templates with translations_json';

CREATE TABLE sent_emails (
  sent_email_id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
  email_template_id BIGINT UNSIGNED NULL,
  related_type VARCHAR(80) NULL,
  related_id BIGINT UNSIGNED NULL,
  subject VARCHAR(255) NOT NULL,
  body_snapshot LONGTEXT NULL,
  recipients_json JSON NOT NULL COMMENT 'Merged sent_email_recipients table',
  metadata_json JSON NULL COMMENT 'provider message id, retry count, etc.',
  status ENUM('QUEUED','SENT','FAILED') NOT NULL DEFAULT 'QUEUED',
  error_message TEXT NULL,
  sent_by BIGINT UNSIGNED NULL,
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
    ON UPDATE CASCADE ON DELETE SET NULL) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
COMMENT='Sent email log with recipients_json';

CREATE TABLE notifications (
  notification_id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
  recipient_user_id BIGINT UNSIGNED NOT NULL,
  title VARCHAR(255) NOT NULL,
  message TEXT NULL,
  notification_type VARCHAR(80) NOT NULL,
  related_type VARCHAR(80) NULL,
  related_id BIGINT UNSIGNED NULL,
  is_read BOOLEAN NOT NULL DEFAULT FALSE,
  read_at DATETIME NULL,
  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (notification_id),
  KEY idx_notifications_user_read_time (recipient_user_id, is_read, created_at),
  KEY idx_notifications_related (related_type, related_id),
  KEY idx_notifications_type_time (notification_type, created_at),
  CONSTRAINT fk_notifications_user
    FOREIGN KEY (recipient_user_id) REFERENCES users(user_id)
    ON UPDATE CASCADE ON DELETE CASCADE) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
COMMENT='In-app notifications';

-- =====================================================================
-- 10. CALENDAR + API + AGENDA TEMPLATE
-- =====================================================================

CREATE TABLE calendar_events (
  calendar_event_id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
  owner_user_id BIGINT UNSIGNED NOT NULL,
  campus_id BIGINT UNSIGNED NULL,
  visit_instance_id BIGINT UNSIGNED NULL,
  logistics_item_id BIGINT UNSIGNED NULL,
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
  created_by BIGINT UNSIGNED NULL,
  updated_at DATETIME NULL DEFAULT NULL ON UPDATE CURRENT_TIMESTAMP,
  updated_by BIGINT UNSIGNED NULL,
  deleted_at DATETIME NULL,
  deleted_by BIGINT UNSIGNED NULL,
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
    ON UPDATE CASCADE ON DELETE SET NULL) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
COMMENT='Calendar events. Personal/visit/logistics/deadline events. Attendees/reminders merged into JSON fields.';

CREATE TABLE api_configurations (
  api_config_id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
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
  created_by BIGINT UNSIGNED NULL,
  updated_at DATETIME NULL DEFAULT NULL ON UPDATE CURRENT_TIMESTAMP,
  updated_by BIGINT UNSIGNED NULL,
  deleted_at DATETIME NULL,
  deleted_by BIGINT UNSIGNED NULL,
  PRIMARY KEY (api_config_id),
  UNIQUE KEY uq_api_config_code (api_code),
  KEY idx_api_config_status (status),
  KEY idx_api_provider_status (provider_name, status)) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
COMMENT='API config + encrypted credentials JSON';

CREATE TABLE api_usage_quotas (
  api_usage_quota_id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
  api_config_id BIGINT UNSIGNED NOT NULL,
  campus_id BIGINT UNSIGNED NULL COMMENT 'NULL = global quota',
  campus_scope_key VARCHAR(36) NOT NULL DEFAULT 'GLOBAL',
  period_yyyymm CHAR(6) NOT NULL COMMENT 'YYYYMM',
  monthly_limit INT UNSIGNED NOT NULL,
  used_count INT UNSIGNED NOT NULL DEFAULT 0 COMMENT 'Merged api_usage_counters table',
  last_used_at DATETIME NULL,
  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  created_by BIGINT UNSIGNED NULL,
  updated_at DATETIME NULL DEFAULT NULL ON UPDATE CURRENT_TIMESTAMP,
  updated_by BIGINT UNSIGNED NULL,
  PRIMARY KEY (api_usage_quota_id),
  UNIQUE KEY uq_api_quota_config_scope_period (api_config_id, campus_scope_key, period_yyyymm),
  KEY idx_api_quota_campus_period (campus_id, period_yyyymm),
  KEY idx_api_quota_period (period_yyyymm),
  CONSTRAINT fk_api_quota_config
    FOREIGN KEY (api_config_id) REFERENCES api_configurations(api_config_id)
    ON UPDATE CASCADE ON DELETE CASCADE,
  CONSTRAINT fk_api_quota_campus
    FOREIGN KEY (campus_id) REFERENCES campuses(campus_id)
    ON UPDATE CASCADE ON DELETE CASCADE) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
COMMENT='API quota + counter per campus/month';

CREATE TABLE api_request_logs (
  api_request_log_id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
  api_config_id BIGINT UNSIGNED NOT NULL,
  campus_id BIGINT UNSIGNED NULL,
  requested_by BIGINT UNSIGNED NULL,
  related_type VARCHAR(80) NULL,
  related_id BIGINT UNSIGNED NULL,
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
    ON UPDATE CASCADE ON DELETE SET NULL) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
COMMENT='External API request logs. Never log full secret/token.';

CREATE TABLE agenda_templates (
  agenda_template_id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
  campus_id BIGINT UNSIGNED NULL,
  campus_scope_key VARCHAR(36) NOT NULL DEFAULT 'GLOBAL',
  name VARCHAR(150) NOT NULL,
  description TEXT NULL,
  items_json JSON NOT NULL COMMENT 'Merged agenda_template_items table',
  status ENUM('ACTIVE','INACTIVE') NOT NULL DEFAULT 'ACTIVE',
  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  created_by BIGINT UNSIGNED NULL,
  updated_at DATETIME NULL DEFAULT NULL ON UPDATE CURRENT_TIMESTAMP,
  updated_by BIGINT UNSIGNED NULL,
  deleted_at DATETIME NULL,
  deleted_by BIGINT UNSIGNED NULL,
  PRIMARY KEY (agenda_template_id),
  UNIQUE KEY uq_agenda_template_scope_name (campus_scope_key, name),
  KEY idx_agenda_templates_status (status),
  KEY idx_agenda_templates_campus_status (campus_id, status),
  CONSTRAINT fk_agenda_templates_campus
    FOREIGN KEY (campus_id) REFERENCES campuses(campus_id)
    ON UPDATE CASCADE ON DELETE SET NULL) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
COMMENT='Agenda template with items_json';

-- =====================================================================
-- 11. AUDIT
-- =====================================================================

CREATE TABLE audit_logs (
  audit_log_id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
  actor_user_id BIGINT UNSIGNED NULL,
  campus_id BIGINT UNSIGNED NULL,
  action VARCHAR(100) NOT NULL,
  entity_type VARCHAR(100) NOT NULL,
  entity_id BIGINT UNSIGNED NULL,
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
    ON UPDATE CASCADE ON DELETE SET NULL) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
COMMENT='General audit log';

CREATE TABLE visit_status_logs (
  visit_status_log_id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
  visit_request_id BIGINT UNSIGNED NULL,
  visit_instance_id BIGINT UNSIGNED NULL,
  status_owner_type ENUM('REQUEST','CAMPUS_INSTANCE') NOT NULL DEFAULT 'CAMPUS_INSTANCE' COMMENT 'REQUEST=visit_requests.status, CAMPUS_INSTANCE=visit_request_campuses.status',
  old_status VARCHAR(50) NULL,
  new_status VARCHAR(50) NOT NULL,
  changed_by BIGINT UNSIGNED NULL,
  reason TEXT NULL,
  changed_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (visit_status_log_id),
  KEY idx_visit_status_request_time (visit_request_id, changed_at),
  KEY idx_visit_status_instance_time (visit_instance_id, changed_at),
  KEY idx_visit_status_owner_time (status_owner_type, changed_at),
  KEY idx_visit_status_changed_by_time (changed_by, changed_at),
  CONSTRAINT fk_visit_status_logs_request
    FOREIGN KEY (visit_request_id) REFERENCES visit_requests(visit_request_id)
    ON UPDATE CASCADE ON DELETE SET NULL,
  CONSTRAINT fk_visit_status_logs_instance
    FOREIGN KEY (visit_instance_id) REFERENCES visit_request_campuses(visit_instance_id)
    ON UPDATE CASCADE ON DELETE SET NULL,
  CONSTRAINT fk_visit_status_logs_changed_by
    FOREIGN KEY (changed_by) REFERENCES users(user_id)
    ON UPDATE CASCADE ON DELETE SET NULL) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
COMMENT='Timeline trạng thái visit. Log rõ cấp REQUEST hoặc CAMPUS_INSTANCE để không nhầm request_status với campus_status.';

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
  DECLARE v_department_campus_id BIGINT UNSIGNED;

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
  ELSEIF v_role_code IN ('STAFF','DEPARTMENT') THEN
    IF NEW.sub_role IS NULL THEN
      SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'STAFF/DEPARTMENT must have sub_role';
    END IF;
    IF NEW.department_id IS NULL THEN
      SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'STAFF/DEPARTMENT must have department_id';
    END IF;

    SELECT department_type, campus_id
      INTO v_department_type, v_department_campus_id
    FROM departments
    WHERE department_id = NEW.department_id;

    IF v_role_code = 'STAFF' AND v_department_type <> 'IC' THEN
      SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'STAFF must belong to IC department';
    END IF;

    IF v_role_code = 'DEPARTMENT' AND v_department_type <> 'GENERAL' THEN
      SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'DEPARTMENT must belong to GENERAL department';
    END IF;

    IF NEW.primary_campus_id IS NULL THEN
      SET NEW.primary_campus_id = v_department_campus_id;
    ELSEIF NEW.primary_campus_id <> v_department_campus_id THEN
      SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'primary_campus_id must match department campus';
    END IF;
  ELSE
    IF NEW.sub_role IS NOT NULL THEN
      SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Only STAFF/DEPARTMENT may have sub_role';
    END IF;
    IF NEW.department_id IS NOT NULL THEN
      SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Only STAFF/DEPARTMENT may have department_id';
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
  DECLARE v_department_campus_id BIGINT UNSIGNED;

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
  ELSEIF v_role_code IN ('STAFF','DEPARTMENT') THEN
    IF NEW.sub_role IS NULL THEN
      SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'STAFF/DEPARTMENT must have sub_role';
    END IF;
    IF NEW.department_id IS NULL THEN
      SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'STAFF/DEPARTMENT must have department_id';
    END IF;

    SELECT department_type, campus_id
      INTO v_department_type, v_department_campus_id
    FROM departments
    WHERE department_id = NEW.department_id;

    IF v_role_code = 'STAFF' AND v_department_type <> 'IC' THEN
      SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'STAFF must belong to IC department';
    END IF;

    IF v_role_code = 'DEPARTMENT' AND v_department_type <> 'GENERAL' THEN
      SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'DEPARTMENT must belong to GENERAL department';
    END IF;

    IF NEW.primary_campus_id IS NULL THEN
      SET NEW.primary_campus_id = v_department_campus_id;
    ELSEIF NEW.primary_campus_id <> v_department_campus_id THEN
      SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'primary_campus_id must match department campus';
    END IF;
  ELSE
    IF NEW.sub_role IS NOT NULL THEN
      SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Only STAFF/DEPARTMENT may have sub_role';
    END IF;
    IF NEW.department_id IS NOT NULL THEN
      SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Only STAFF/DEPARTMENT may have department_id';
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
  DECLARE v_primary_campus_id BIGINT UNSIGNED;

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
-- Visit request decision rules: APPROVED/REJECTED only.
-- CANCELLED is handled by cancellation metadata, not decision fields.
-- ---------------------------------------------------------------------



CREATE TRIGGER trg_visit_requests_decision_validate_bi
BEFORE INSERT ON visit_requests
FOR EACH ROW
BEGIN
  DECLARE v_actor_role_code VARCHAR(30);
  DECLARE v_actor_sub_role VARCHAR(30);

  IF NEW.status IN ('APPROVED','REJECTED') THEN
    IF NEW.decision_actor_role IS NULL THEN
      SIGNAL SQLSTATE '45000'
        SET MESSAGE_TEXT = 'decision_actor_role is required when visit request is approved/rejected';
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
         AND NOT (v_actor_role_code = 'STAFF' AND v_actor_sub_role = 'LEADER') THEN
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

  IF NEW.status IN ('APPROVED','REJECTED') THEN
    IF NEW.decision_actor_role IS NULL THEN
      SIGNAL SQLSTATE '45000'
        SET MESSAGE_TEXT = 'decision_actor_role is required when visit request is approved/rejected';
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
         AND NOT (v_actor_role_code = 'STAFF' AND v_actor_sub_role = 'LEADER') THEN
        SIGNAL SQLSTATE '45000'
          SET MESSAGE_TEXT = 'decision_actor_role STAFF_LEADER requires STAFF Leader user';
      END IF;
    END IF;
  END IF;
END$$

-- ---------------------------------------------------------------------
-- Cancellation validation triggers.
-- These triggers validate required metadata only. Detailed scope such as
-- ownership/current-host/campus filters must still be checked in backend.
-- ---------------------------------------------------------------------


CREATE TRIGGER trg_visit_requests_cancel_validate_bu
BEFORE UPDATE ON visit_requests
FOR EACH ROW
BEGIN
  IF NEW.status = 'CANCELLED' AND OLD.status <> 'CANCELLED' THEN
    IF OLD.status <> 'APPROVED' THEN
      SIGNAL SQLSTATE '45000'
        SET MESSAGE_TEXT = 'Only approved request/delegation can be cancelled; pending requests must be rejected instead';
    END IF;

    IF NEW.cancellation_actor_type IS NULL
       OR NEW.cancellation_source IS NULL
       OR NEW.cancelled_at IS NULL THEN
      SIGNAL SQLSTATE '45000'
        SET MESSAGE_TEXT = 'cancellation_actor_type, cancellation_source and cancelled_at are required when request is cancelled';
    END IF;

    IF NEW.cancellation_actor_type <> 'SYSTEM' AND NEW.cancelled_by IS NULL THEN
      SIGNAL SQLSTATE '45000'
        SET MESSAGE_TEXT = 'cancelled_by is required for non-system cancellation';
    END IF;

    IF NEW.cancellation_reason IS NULL OR TRIM(NEW.cancellation_reason) = '' THEN
      SIGNAL SQLSTATE '45000'
        SET MESSAGE_TEXT = 'cancellation_reason is required when approved request/delegation is cancelled';
    END IF;

    IF NEW.cancellation_actor_type = 'VISITOR'
       AND NEW.cancellation_source <> 'SELF_SERVICE' THEN
      SIGNAL SQLSTATE '45000'
        SET MESSAGE_TEXT = 'VISITOR cancellation must use SELF_SERVICE source';
    END IF;

    IF NEW.cancellation_actor_type = 'HOST'
       AND NEW.cancellation_source <> 'EXTERNAL_CONFIRMATION' THEN
      SIGNAL SQLSTATE '45000'
        SET MESSAGE_TEXT = 'HOST cancellation on behalf of visitor must use EXTERNAL_CONFIRMATION source';
    END IF;
  END IF;
END$$

CREATE TRIGGER trg_visit_campuses_cancel_validate_bu
BEFORE UPDATE ON visit_request_campuses
FOR EACH ROW
BEGIN
  DECLARE v_request_status VARCHAR(30);

  IF NEW.status = 'CANCELLED' AND OLD.status <> 'CANCELLED' THEN
    SELECT status INTO v_request_status
    FROM visit_requests
    WHERE visit_request_id = NEW.visit_request_id;

    IF v_request_status <> 'APPROVED' THEN
      SIGNAL SQLSTATE '45000'
        SET MESSAGE_TEXT = 'Campus instance can be cancelled only after the main request is approved; pending request must be rejected instead';
    END IF;

    IF OLD.status IN ('WAITING_REQUEST_APPROVAL','DURING_VISIT','AFTER_VISIT','CLOSED') THEN
      SIGNAL SQLSTATE '45000'
        SET MESSAGE_TEXT = 'Campus instance can be cancelled only after approval and before/during preparation; pending/during/after/closed instances cannot be cancelled';
    END IF;

    IF NEW.cancellation_actor_type IS NULL
       OR NEW.cancellation_source IS NULL
       OR NEW.cancelled_at IS NULL THEN
      SIGNAL SQLSTATE '45000'
        SET MESSAGE_TEXT = 'cancellation_actor_type, cancellation_source and cancelled_at are required when campus instance is cancelled';
    END IF;

    IF NEW.cancellation_actor_type <> 'SYSTEM' AND NEW.cancelled_by IS NULL THEN
      SIGNAL SQLSTATE '45000'
        SET MESSAGE_TEXT = 'cancelled_by is required for non-system campus cancellation';
    END IF;

    IF NEW.cancellation_reason IS NULL OR TRIM(NEW.cancellation_reason) = '' THEN
      SIGNAL SQLSTATE '45000'
        SET MESSAGE_TEXT = 'cancellation_reason is required when approved campus instance is cancelled';
    END IF;

    IF NEW.cancellation_actor_type = 'VISITOR'
       AND NEW.cancellation_source <> 'SELF_SERVICE' THEN
      SIGNAL SQLSTATE '45000'
        SET MESSAGE_TEXT = 'VISITOR campus cancellation must use SELF_SERVICE source';
    END IF;

    IF NEW.cancellation_actor_type = 'HOST'
       AND NEW.cancellation_source <> 'EXTERNAL_CONFIRMATION' THEN
      SIGNAL SQLSTATE '45000'
        SET MESSAGE_TEXT = 'HOST campus cancellation must use EXTERNAL_CONFIRMATION source';
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
  DECLARE v_request_scope VARCHAR(30);
  DECLARE v_host_role_code VARCHAR(30);
  DECLARE v_host_sub_role VARCHAR(30);
  DECLARE v_host_campus_id BIGINT UNSIGNED;
  DECLARE v_assigned_by_role_code VARCHAR(30);
  DECLARE v_assigned_by_sub_role VARCHAR(30);
  DECLARE v_assigned_by_campus_id BIGINT UNSIGNED;
  DECLARE v_transfer_role_code VARCHAR(30);
  DECLARE v_transfer_sub_role VARCHAR(30);
  DECLARE v_transfer_campus_id BIGINT UNSIGNED;

  SELECT status, visit_scope
    INTO v_request_status, v_request_scope
  FROM visit_requests
  WHERE visit_request_id = NEW.visit_request_id;

  IF NEW.status = 'WAITING_REQUEST_APPROVAL' THEN
    IF NEW.current_host_user_id IS NOT NULL
       OR NEW.host_assigned_by IS NOT NULL
       OR NEW.host_assigned_at IS NOT NULL
       OR NEW.host_assignment_source IS NOT NULL THEN
      SIGNAL SQLSTATE '45000'
        SET MESSAGE_TEXT = 'WAITING_REQUEST_APPROVAL campus instance must not have host assignment data yet';
    END IF;
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
    IF NEW.host_assignment_source IS NULL OR NEW.host_assigned_at IS NULL THEN
      SIGNAL SQLSTATE '45000'
        SET MESSAGE_TEXT = 'host_assignment_source and host_assigned_at are required when campus instance has host';
    END IF;
  END IF;

  IF NEW.current_host_user_id IS NOT NULL THEN
    SELECT r.role_code, u.sub_role, u.primary_campus_id
      INTO v_host_role_code, v_host_sub_role, v_host_campus_id
    FROM users u
    JOIN roles r ON r.role_id = u.role_id
    WHERE u.user_id = NEW.current_host_user_id;
    IF NOT (v_host_role_code = 'STAFF' AND v_host_sub_role IN ('LEADER','STAFF')) THEN
      SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'current_host_user_id must be a STAFF user';
    END IF;
    IF v_host_campus_id <> NEW.campus_id THEN
      SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'current_host_user_id must belong to the same campus instance';
    END IF;
    IF NEW.host_assignment_source = 'AUTO_STAFF_LEADER'
       AND NOT (v_host_role_code = 'STAFF' AND v_host_sub_role = 'LEADER') THEN
      SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'AUTO_STAFF_LEADER assignment requires current_host_user_id to be Staff Leader';
    END IF;
  END IF;

  IF NEW.host_assignment_source = 'MANUAL_APPROVAL' THEN
    IF v_request_scope <> 'SINGLE_CAMPUS' THEN
      SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'MANUAL_APPROVAL host assignment is only for SINGLE_CAMPUS request';
    END IF;
    IF NEW.host_assigned_by IS NULL THEN
      SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'host_assigned_by is required for MANUAL_APPROVAL host assignment';
    END IF;
  END IF;

  IF NEW.host_assignment_source = 'AUTO_STAFF_LEADER' THEN
    IF v_request_scope <> 'MULTI_CAMPUS' THEN
      SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'AUTO_STAFF_LEADER host assignment is only for MULTI_CAMPUS request';
    END IF;
  END IF;

  IF NEW.host_assigned_by IS NOT NULL THEN
    SELECT r.role_code, u.sub_role, u.primary_campus_id
      INTO v_assigned_by_role_code, v_assigned_by_sub_role, v_assigned_by_campus_id
    FROM users u
    JOIN roles r ON r.role_id = u.role_id
    WHERE u.user_id = NEW.host_assigned_by;
    IF NEW.host_assignment_source = 'MANUAL_APPROVAL'
       AND NOT (v_assigned_by_role_code = 'STAFF' AND v_assigned_by_sub_role = 'LEADER' AND v_assigned_by_campus_id = NEW.campus_id) THEN
      SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'MANUAL_APPROVAL host_assigned_by must be Staff Leader of the same campus';
    END IF;
    IF NEW.host_assignment_source = 'AUTO_STAFF_LEADER'
       AND v_assigned_by_role_code <> 'HO' THEN
      SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'AUTO_STAFF_LEADER host_assigned_by must be HO when provided';
    END IF;
  END IF;

  IF NEW.host_assignment_source = 'TRANSFERRED'
     OR NEW.host_transferred_at IS NOT NULL
     OR NEW.host_transferred_by IS NOT NULL THEN
    IF NEW.host_transferred_by IS NULL OR NEW.host_transferred_at IS NULL THEN
      SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'host_transferred_by and host_transferred_at are required when transferring host';
    END IF;
  END IF;

  IF NEW.host_transferred_by IS NOT NULL THEN
    SELECT r.role_code, u.sub_role, u.primary_campus_id
      INTO v_transfer_role_code, v_transfer_sub_role, v_transfer_campus_id
    FROM users u
    JOIN roles r ON r.role_id = u.role_id
    WHERE u.user_id = NEW.host_transferred_by;
    IF NOT (v_transfer_role_code = 'STAFF' AND v_transfer_sub_role IN ('LEADER','STAFF')) THEN
      SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'host_transferred_by must be a STAFF user';
    END IF;
    IF v_transfer_campus_id <> NEW.campus_id THEN
      SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'host_transferred_by must belong to the same campus instance';
    END IF;
  END IF;
END$$

CREATE TRIGGER trg_visit_campuses_assignment_validate_bu
BEFORE UPDATE ON visit_request_campuses
FOR EACH ROW
BEGIN
  DECLARE v_request_status VARCHAR(30);
  DECLARE v_request_scope VARCHAR(30);
  DECLARE v_host_role_code VARCHAR(30);
  DECLARE v_host_sub_role VARCHAR(30);
  DECLARE v_host_campus_id BIGINT UNSIGNED;
  DECLARE v_assigned_by_role_code VARCHAR(30);
  DECLARE v_assigned_by_sub_role VARCHAR(30);
  DECLARE v_assigned_by_campus_id BIGINT UNSIGNED;
  DECLARE v_transfer_role_code VARCHAR(30);
  DECLARE v_transfer_sub_role VARCHAR(30);
  DECLARE v_transfer_campus_id BIGINT UNSIGNED;

  SELECT status, visit_scope
    INTO v_request_status, v_request_scope
  FROM visit_requests
  WHERE visit_request_id = NEW.visit_request_id;

  IF NEW.status = 'WAITING_REQUEST_APPROVAL' THEN
    IF NEW.current_host_user_id IS NOT NULL
       OR NEW.host_assigned_by IS NOT NULL
       OR NEW.host_assigned_at IS NOT NULL
       OR NEW.host_assignment_source IS NOT NULL THEN
      SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'WAITING_REQUEST_APPROVAL campus instance must not have host assignment data yet';
    END IF;
  END IF;

  IF NEW.status NOT IN ('WAITING_REQUEST_APPROVAL','CANCELLED') THEN
    IF v_request_status <> 'APPROVED' THEN
      SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Campus instance can move to operational status only after main visit request is APPROVED';
    END IF;
    IF NEW.current_host_user_id IS NULL THEN
      SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'current_host_user_id is required after main visit request is approved';
    END IF;
    IF NEW.host_assignment_source IS NULL OR NEW.host_assigned_at IS NULL THEN
      SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'host_assignment_source and host_assigned_at are required when campus instance has host';
    END IF;
  END IF;

  IF NEW.current_host_user_id IS NOT NULL THEN
    SELECT r.role_code, u.sub_role, u.primary_campus_id
      INTO v_host_role_code, v_host_sub_role, v_host_campus_id
    FROM users u
    JOIN roles r ON r.role_id = u.role_id
    WHERE u.user_id = NEW.current_host_user_id;
    IF NOT (v_host_role_code = 'STAFF' AND v_host_sub_role IN ('LEADER','STAFF')) THEN
      SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'current_host_user_id must be a STAFF user';
    END IF;
    IF v_host_campus_id <> NEW.campus_id THEN
      SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'current_host_user_id must belong to the same campus instance';
    END IF;
    IF NEW.host_assignment_source = 'AUTO_STAFF_LEADER'
       AND NOT (v_host_role_code = 'STAFF' AND v_host_sub_role = 'LEADER') THEN
      SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'AUTO_STAFF_LEADER assignment requires current_host_user_id to be Staff Leader';
    END IF;
  END IF;

  IF NOT (NEW.current_host_user_id <=> OLD.current_host_user_id)
     AND OLD.current_host_user_id IS NOT NULL THEN
    IF NEW.host_assignment_source <> 'TRANSFERRED'
       OR NEW.host_transferred_by IS NULL
       OR NEW.host_transferred_at IS NULL THEN
      SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'host transfer must set host_assignment_source=TRANSFERRED, host_transferred_by and host_transferred_at';
    END IF;
  END IF;

  IF NEW.host_assignment_source = 'MANUAL_APPROVAL' THEN
    IF v_request_scope <> 'SINGLE_CAMPUS' THEN
      SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'MANUAL_APPROVAL host assignment is only for SINGLE_CAMPUS request';
    END IF;
    IF NEW.host_assigned_by IS NULL THEN
      SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'host_assigned_by is required for MANUAL_APPROVAL host assignment';
    END IF;
  END IF;

  IF NEW.host_assignment_source = 'AUTO_STAFF_LEADER' THEN
    IF v_request_scope <> 'MULTI_CAMPUS' THEN
      SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'AUTO_STAFF_LEADER host assignment is only for MULTI_CAMPUS request';
    END IF;
  END IF;

  IF NEW.host_assigned_by IS NOT NULL THEN
    SELECT r.role_code, u.sub_role, u.primary_campus_id
      INTO v_assigned_by_role_code, v_assigned_by_sub_role, v_assigned_by_campus_id
    FROM users u
    JOIN roles r ON r.role_id = u.role_id
    WHERE u.user_id = NEW.host_assigned_by;
    IF NEW.host_assignment_source = 'MANUAL_APPROVAL'
       AND NOT (v_assigned_by_role_code = 'STAFF' AND v_assigned_by_sub_role = 'LEADER' AND v_assigned_by_campus_id = NEW.campus_id) THEN
      SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'MANUAL_APPROVAL host_assigned_by must be Staff Leader of the same campus';
    END IF;
    IF NEW.host_assignment_source = 'AUTO_STAFF_LEADER'
       AND v_assigned_by_role_code <> 'HO' THEN
      SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'AUTO_STAFF_LEADER host_assigned_by must be HO when provided';
    END IF;
  END IF;

  IF NEW.host_assignment_source = 'TRANSFERRED'
     OR NEW.host_transferred_at IS NOT NULL
     OR NEW.host_transferred_by IS NOT NULL THEN
    IF NEW.host_transferred_by IS NULL OR NEW.host_transferred_at IS NULL THEN
      SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'host_transferred_by and host_transferred_at are required when transferring host';
    END IF;
  END IF;

  IF NEW.host_transferred_by IS NOT NULL THEN
    SELECT r.role_code, u.sub_role, u.primary_campus_id
      INTO v_transfer_role_code, v_transfer_sub_role, v_transfer_campus_id
    FROM users u
    JOIN roles r ON r.role_id = u.role_id
    WHERE u.user_id = NEW.host_transferred_by;
    IF NOT (v_transfer_role_code = 'STAFF' AND v_transfer_sub_role IN ('LEADER','STAFF')) THEN
      SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'host_transferred_by must be a STAFF user';
    END IF;
    IF v_transfer_campus_id <> NEW.campus_id THEN
      SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'host_transferred_by must belong to the same campus instance';
    END IF;
  END IF;
END$$


CREATE TRIGGER trg_api_usage_quotas_scope_bi
BEFORE INSERT ON api_usage_quotas
FOR EACH ROW
BEGIN
  SET NEW.campus_scope_key = IFNULL(CAST(NEW.campus_id AS CHAR), 'GLOBAL');
END$$

CREATE TRIGGER trg_api_usage_quotas_scope_bu
BEFORE UPDATE ON api_usage_quotas
FOR EACH ROW
BEGIN
  SET NEW.campus_scope_key = IFNULL(CAST(NEW.campus_id AS CHAR), 'GLOBAL');
END$$

CREATE TRIGGER trg_agenda_templates_scope_bi
BEFORE INSERT ON agenda_templates
FOR EACH ROW
BEGIN
  SET NEW.campus_scope_key = IFNULL(CAST(NEW.campus_id AS CHAR), 'GLOBAL');
END$$

CREATE TRIGGER trg_agenda_templates_scope_bu
BEFORE UPDATE ON agenda_templates
FOR EACH ROW
BEGIN
  SET NEW.campus_scope_key = IFNULL(CAST(NEW.campus_id AS CHAR), 'GLOBAL');
END$$

-- MySQL 8.0 does not allow a CHECK constraint on FK columns when those
-- columns are also used by FK referential actions. Keep the same business
-- rule with triggers instead of chk_feedbacks_not_self.
CREATE TRIGGER trg_feedbacks_not_self_bi
BEFORE INSERT ON feedbacks
FOR EACH ROW
BEGIN
  IF NEW.submitted_by_user_id = NEW.target_user_id THEN
    SIGNAL SQLSTATE '45000'
      SET MESSAGE_TEXT = 'Feedback submitter and target user cannot be the same';
  END IF;
END$$

CREATE TRIGGER trg_feedbacks_not_self_bu
BEFORE UPDATE ON feedbacks
FOR EACH ROW
BEGIN
  IF NEW.submitted_by_user_id = NEW.target_user_id THEN
    SIGNAL SQLSTATE '45000'
      SET MESSAGE_TEXT = 'Feedback submitter and target user cannot be the same';
  END IF;
END$$

DELIMITER ;

-- =====================================================================
-- 13. SEED BASIC DATA
-- =====================================================================

INSERT INTO roles (role_id, role_code, name, description)
VALUES
  (NULL, 'ADMIN', 'Admin', 'Quản trị kỹ thuật hệ thống'),
  (NULL, 'HO', 'Head Office', 'Quản lý cấp Head Office'),
  (NULL, 'STAFF', 'IC Staff', 'Nhân sự phòng Hợp tác Quốc tế, dùng sub_role LEADER/STAFF'),
  (NULL, 'DEPARTMENT', 'Department', 'Nhân sự phòng ban khác, dùng sub_role LEADER/STAFF'),
  (NULL, 'STUDENT', 'Student', 'Sinh viên hỗ trợ'),
  (NULL, 'VISITOR', 'Visitor', 'Khách gửi visit request và theo dõi thông tin của mình');

SET @campus_hn = 100000;
SET @campus_hcm = 100001;
SET @campus_dn = 100002;
SET @campus_ct = 100003;
SET @campus_qn = 100004;

INSERT INTO campuses (campus_id, campus_code, name, city, status)
VALUES
  (@campus_hn, 'HN', 'FPT University Hà Nội', 'Hà Nội', 'ACTIVE'),
  (@campus_hcm, 'HCM', 'FPT University TP. Hồ Chí Minh', 'TP. Hồ Chí Minh', 'ACTIVE'),
  (@campus_dn, 'DN', 'FPT University Đà Nẵng', 'Đà Nẵng', 'ACTIVE'),
  (@campus_ct, 'CT', 'FPT University Cần Thơ', 'Cần Thơ', 'ACTIVE'),
  (@campus_qn, 'QN', 'FPT University Quy Nhơn', 'Quy Nhơn', 'ACTIVE');

INSERT INTO departments (department_id, campus_id, department_code, name, department_type, status)
VALUES
  (NULL, @campus_hn, 'IC', 'International Cooperation', 'IC', 'ACTIVE'),
  (NULL, @campus_hcm, 'IC', 'International Cooperation', 'IC', 'ACTIVE'),
  (NULL, @campus_dn, 'IC', 'International Cooperation', 'IC', 'ACTIVE'),
  (NULL, @campus_ct, 'IC', 'International Cooperation', 'IC', 'ACTIVE'),
  (NULL, @campus_qn, 'IC', 'International Cooperation', 'IC', 'ACTIVE'),
  (NULL, @campus_hn, 'ACADEMIC', 'Academic Department', 'GENERAL', 'ACTIVE'),
  (NULL, @campus_hn, 'MARKETING', 'Marketing Department', 'GENERAL', 'ACTIVE'),
  (NULL, @campus_hn, 'ADMISSION', 'Admission Department', 'GENERAL', 'ACTIVE'),
  (NULL, @campus_hn, 'IT', 'IT Department', 'GENERAL', 'ACTIVE');

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


-- =====================================================================
-- BEGIN HIGH QUALITY SCENARIO SEED - FINAL CHECKED
-- =====================================================================

-- =====================================================================
-- PEMS v4.5 High Quality Scenario Seed Data
-- Run after PEMS_v4_5_schema_uc_based_soft_delete.sql on a clean database.
-- Dynamic dates: all timestamps use @seed_now = NOW().
-- =====================================================================
USE pems_db;
SET NAMES utf8mb4;
SET @OLD_SQL_SAFE_UPDATES = @@SQL_SAFE_UPDATES;
SET SQL_SAFE_UPDATES = 0;
SET @seed_now = NOW();

SELECT role_id INTO @role_admin FROM roles WHERE role_code='ADMIN' LIMIT 1;
SELECT role_id INTO @role_ho FROM roles WHERE role_code='HO' LIMIT 1;
SELECT role_id INTO @role_staff FROM roles WHERE role_code='STAFF' LIMIT 1;
SELECT role_id INTO @role_department FROM roles WHERE role_code='DEPARTMENT' LIMIT 1;
SELECT role_id INTO @role_student FROM roles WHERE role_code='STUDENT' LIMIT 1;
SELECT role_id INTO @role_visitor FROM roles WHERE role_code='VISITOR' LIMIT 1;
SELECT campus_id INTO @campus_hn FROM campuses WHERE campus_code='HN' LIMIT 1;
SELECT campus_id INTO @campus_hcm FROM campuses WHERE campus_code='HCM' LIMIT 1;
SELECT campus_id INTO @campus_dn FROM campuses WHERE campus_code='DN' LIMIT 1;
SELECT campus_id INTO @campus_ct FROM campuses WHERE campus_code='CT' LIMIT 1;
SELECT campus_id INTO @campus_qn FROM campuses WHERE campus_code='QN' LIMIT 1;

UPDATE campuses SET address='Khu Giáo dục và Đào tạo, Khu Công nghệ cao Hòa Lạc, Thạch Thất, Hà Nội', phone='02473001866', email='ic.hn@company.vn', status='ACTIVE', updated_at=@seed_now WHERE campus_code='HN';
UPDATE campuses SET address='Lô E2a-7, Đường D1, Khu Công nghệ cao, TP. Thủ Đức, TP. Hồ Chí Minh', phone='02873005588', email='ic.hcm@company.vn', status='ACTIVE', updated_at=@seed_now WHERE campus_code='HCM';
UPDATE campuses SET address='Khu đô thị công nghệ FPT Đà Nẵng, phường Hòa Hải, quận Ngũ Hành Sơn, Đà Nẵng', phone='02367300999', email='ic.dn@company.vn', status='ACTIVE', updated_at=@seed_now WHERE campus_code='DN';
UPDATE campuses SET address='Khu đô thị Nam Cần Thơ, phường Hưng Thạnh, quận Cái Răng, Cần Thơ', phone='02927300999', email='ic.ct@company.vn', status='ACTIVE', updated_at=@seed_now WHERE campus_code='CT';
UPDATE campuses SET address='Khu đô thị giáo dục FPT Quy Nhơn, phường Nhơn Bình, TP. Quy Nhơn, Bình Định', phone='02567300999', email='ic.qn@company.vn', status='ACTIVE', updated_at=@seed_now WHERE campus_code='QN';
UPDATE departments SET name='Phòng Hợp tác Quốc tế' WHERE department_code='IC';

INSERT INTO departments (department_id,campus_id,department_code,name,department_type,status,created_at) SELECT NULL, @campus_hn, 'ADMIN', 'Phòng Hành chính', 'GENERAL', 'ACTIVE', DATE_SUB(@seed_now, INTERVAL 330 DAY) WHERE NOT EXISTS (SELECT 1 FROM departments WHERE campus_id=@campus_hn AND department_code='ADMIN');

INSERT INTO departments (department_id,campus_id,department_code,name,department_type,status,created_at) SELECT NULL, @campus_hn, 'PLANNING', 'Phòng Kế hoạch', 'GENERAL', 'ACTIVE', DATE_SUB(@seed_now, INTERVAL 330 DAY) WHERE NOT EXISTS (SELECT 1 FROM departments WHERE campus_id=@campus_hn AND department_code='PLANNING');

INSERT INTO departments (department_id,campus_id,department_code,name,department_type,status,created_at) SELECT NULL, @campus_hn, 'IT', 'Phòng CNTT', 'GENERAL', 'ACTIVE', DATE_SUB(@seed_now, INTERVAL 330 DAY) WHERE NOT EXISTS (SELECT 1 FROM departments WHERE campus_id=@campus_hn AND department_code='IT');

INSERT INTO departments (department_id,campus_id,department_code,name,department_type,status,created_at) SELECT NULL, @campus_hn, 'FINANCE', 'Phòng Tài chính', 'GENERAL', 'ACTIVE', DATE_SUB(@seed_now, INTERVAL 330 DAY) WHERE NOT EXISTS (SELECT 1 FROM departments WHERE campus_id=@campus_hn AND department_code='FINANCE');

INSERT INTO departments (department_id,campus_id,department_code,name,department_type,status,created_at) SELECT NULL, @campus_hn, 'OPERATIONS', 'Ban Điều hành', 'GENERAL', 'ACTIVE', DATE_SUB(@seed_now, INTERVAL 330 DAY) WHERE NOT EXISTS (SELECT 1 FROM departments WHERE campus_id=@campus_hn AND department_code='OPERATIONS');

INSERT INTO departments (department_id,campus_id,department_code,name,department_type,status,created_at) SELECT NULL, @campus_hn, 'ACADEMIC', 'Phòng Đào tạo', 'GENERAL', 'ACTIVE', DATE_SUB(@seed_now, INTERVAL 330 DAY) WHERE NOT EXISTS (SELECT 1 FROM departments WHERE campus_id=@campus_hn AND department_code='ACADEMIC');

INSERT INTO departments (department_id,campus_id,department_code,name,department_type,status,created_at) SELECT NULL, @campus_hn, 'MARKETING', 'Phòng Truyền thông', 'GENERAL', 'ACTIVE', DATE_SUB(@seed_now, INTERVAL 330 DAY) WHERE NOT EXISTS (SELECT 1 FROM departments WHERE campus_id=@campus_hn AND department_code='MARKETING');

INSERT INTO departments (department_id,campus_id,department_code,name,department_type,status,created_at) SELECT NULL, @campus_hcm, 'ADMIN', 'Phòng Hành chính', 'GENERAL', 'ACTIVE', DATE_SUB(@seed_now, INTERVAL 330 DAY) WHERE NOT EXISTS (SELECT 1 FROM departments WHERE campus_id=@campus_hcm AND department_code='ADMIN');

INSERT INTO departments (department_id,campus_id,department_code,name,department_type,status,created_at) SELECT NULL, @campus_hcm, 'PLANNING', 'Phòng Kế hoạch', 'GENERAL', 'ACTIVE', DATE_SUB(@seed_now, INTERVAL 330 DAY) WHERE NOT EXISTS (SELECT 1 FROM departments WHERE campus_id=@campus_hcm AND department_code='PLANNING');

INSERT INTO departments (department_id,campus_id,department_code,name,department_type,status,created_at) SELECT NULL, @campus_hcm, 'IT', 'Phòng CNTT', 'GENERAL', 'ACTIVE', DATE_SUB(@seed_now, INTERVAL 330 DAY) WHERE NOT EXISTS (SELECT 1 FROM departments WHERE campus_id=@campus_hcm AND department_code='IT');

INSERT INTO departments (department_id,campus_id,department_code,name,department_type,status,created_at) SELECT NULL, @campus_hcm, 'FINANCE', 'Phòng Tài chính', 'GENERAL', 'ACTIVE', DATE_SUB(@seed_now, INTERVAL 330 DAY) WHERE NOT EXISTS (SELECT 1 FROM departments WHERE campus_id=@campus_hcm AND department_code='FINANCE');

INSERT INTO departments (department_id,campus_id,department_code,name,department_type,status,created_at) SELECT NULL, @campus_hcm, 'OPERATIONS', 'Ban Điều hành', 'GENERAL', 'ACTIVE', DATE_SUB(@seed_now, INTERVAL 330 DAY) WHERE NOT EXISTS (SELECT 1 FROM departments WHERE campus_id=@campus_hcm AND department_code='OPERATIONS');

INSERT INTO departments (department_id,campus_id,department_code,name,department_type,status,created_at) SELECT NULL, @campus_hcm, 'ACADEMIC', 'Phòng Đào tạo', 'GENERAL', 'ACTIVE', DATE_SUB(@seed_now, INTERVAL 330 DAY) WHERE NOT EXISTS (SELECT 1 FROM departments WHERE campus_id=@campus_hcm AND department_code='ACADEMIC');

INSERT INTO departments (department_id,campus_id,department_code,name,department_type,status,created_at) SELECT NULL, @campus_hcm, 'MARKETING', 'Phòng Truyền thông', 'GENERAL', 'ACTIVE', DATE_SUB(@seed_now, INTERVAL 330 DAY) WHERE NOT EXISTS (SELECT 1 FROM departments WHERE campus_id=@campus_hcm AND department_code='MARKETING');

INSERT INTO departments (department_id,campus_id,department_code,name,department_type,status,created_at) SELECT NULL, @campus_dn, 'ADMIN', 'Phòng Hành chính', 'GENERAL', 'ACTIVE', DATE_SUB(@seed_now, INTERVAL 330 DAY) WHERE NOT EXISTS (SELECT 1 FROM departments WHERE campus_id=@campus_dn AND department_code='ADMIN');

INSERT INTO departments (department_id,campus_id,department_code,name,department_type,status,created_at) SELECT NULL, @campus_dn, 'PLANNING', 'Phòng Kế hoạch', 'GENERAL', 'ACTIVE', DATE_SUB(@seed_now, INTERVAL 330 DAY) WHERE NOT EXISTS (SELECT 1 FROM departments WHERE campus_id=@campus_dn AND department_code='PLANNING');

INSERT INTO departments (department_id,campus_id,department_code,name,department_type,status,created_at) SELECT NULL, @campus_dn, 'IT', 'Phòng CNTT', 'GENERAL', 'ACTIVE', DATE_SUB(@seed_now, INTERVAL 330 DAY) WHERE NOT EXISTS (SELECT 1 FROM departments WHERE campus_id=@campus_dn AND department_code='IT');

INSERT INTO departments (department_id,campus_id,department_code,name,department_type,status,created_at) SELECT NULL, @campus_dn, 'FINANCE', 'Phòng Tài chính', 'GENERAL', 'ACTIVE', DATE_SUB(@seed_now, INTERVAL 330 DAY) WHERE NOT EXISTS (SELECT 1 FROM departments WHERE campus_id=@campus_dn AND department_code='FINANCE');

INSERT INTO departments (department_id,campus_id,department_code,name,department_type,status,created_at) SELECT NULL, @campus_dn, 'OPERATIONS', 'Ban Điều hành', 'GENERAL', 'ACTIVE', DATE_SUB(@seed_now, INTERVAL 330 DAY) WHERE NOT EXISTS (SELECT 1 FROM departments WHERE campus_id=@campus_dn AND department_code='OPERATIONS');

INSERT INTO departments (department_id,campus_id,department_code,name,department_type,status,created_at) SELECT NULL, @campus_dn, 'ACADEMIC', 'Phòng Đào tạo', 'GENERAL', 'ACTIVE', DATE_SUB(@seed_now, INTERVAL 330 DAY) WHERE NOT EXISTS (SELECT 1 FROM departments WHERE campus_id=@campus_dn AND department_code='ACADEMIC');

INSERT INTO departments (department_id,campus_id,department_code,name,department_type,status,created_at) SELECT NULL, @campus_dn, 'MARKETING', 'Phòng Truyền thông', 'GENERAL', 'ACTIVE', DATE_SUB(@seed_now, INTERVAL 330 DAY) WHERE NOT EXISTS (SELECT 1 FROM departments WHERE campus_id=@campus_dn AND department_code='MARKETING');

INSERT INTO departments (department_id,campus_id,department_code,name,department_type,status,created_at) SELECT NULL, @campus_ct, 'ADMIN', 'Phòng Hành chính', 'GENERAL', 'ACTIVE', DATE_SUB(@seed_now, INTERVAL 330 DAY) WHERE NOT EXISTS (SELECT 1 FROM departments WHERE campus_id=@campus_ct AND department_code='ADMIN');

INSERT INTO departments (department_id,campus_id,department_code,name,department_type,status,created_at) SELECT NULL, @campus_ct, 'PLANNING', 'Phòng Kế hoạch', 'GENERAL', 'ACTIVE', DATE_SUB(@seed_now, INTERVAL 330 DAY) WHERE NOT EXISTS (SELECT 1 FROM departments WHERE campus_id=@campus_ct AND department_code='PLANNING');

INSERT INTO departments (department_id,campus_id,department_code,name,department_type,status,created_at) SELECT NULL, @campus_ct, 'IT', 'Phòng CNTT', 'GENERAL', 'ACTIVE', DATE_SUB(@seed_now, INTERVAL 330 DAY) WHERE NOT EXISTS (SELECT 1 FROM departments WHERE campus_id=@campus_ct AND department_code='IT');

INSERT INTO departments (department_id,campus_id,department_code,name,department_type,status,created_at) SELECT NULL, @campus_ct, 'FINANCE', 'Phòng Tài chính', 'GENERAL', 'ACTIVE', DATE_SUB(@seed_now, INTERVAL 330 DAY) WHERE NOT EXISTS (SELECT 1 FROM departments WHERE campus_id=@campus_ct AND department_code='FINANCE');

INSERT INTO departments (department_id,campus_id,department_code,name,department_type,status,created_at) SELECT NULL, @campus_ct, 'OPERATIONS', 'Ban Điều hành', 'GENERAL', 'ACTIVE', DATE_SUB(@seed_now, INTERVAL 330 DAY) WHERE NOT EXISTS (SELECT 1 FROM departments WHERE campus_id=@campus_ct AND department_code='OPERATIONS');

INSERT INTO departments (department_id,campus_id,department_code,name,department_type,status,created_at) SELECT NULL, @campus_ct, 'ACADEMIC', 'Phòng Đào tạo', 'GENERAL', 'ACTIVE', DATE_SUB(@seed_now, INTERVAL 330 DAY) WHERE NOT EXISTS (SELECT 1 FROM departments WHERE campus_id=@campus_ct AND department_code='ACADEMIC');

INSERT INTO departments (department_id,campus_id,department_code,name,department_type,status,created_at) SELECT NULL, @campus_ct, 'MARKETING', 'Phòng Truyền thông', 'GENERAL', 'ACTIVE', DATE_SUB(@seed_now, INTERVAL 330 DAY) WHERE NOT EXISTS (SELECT 1 FROM departments WHERE campus_id=@campus_ct AND department_code='MARKETING');

INSERT INTO departments (department_id,campus_id,department_code,name,department_type,status,created_at) SELECT NULL, @campus_qn, 'ADMIN', 'Phòng Hành chính', 'GENERAL', 'ACTIVE', DATE_SUB(@seed_now, INTERVAL 330 DAY) WHERE NOT EXISTS (SELECT 1 FROM departments WHERE campus_id=@campus_qn AND department_code='ADMIN');

INSERT INTO departments (department_id,campus_id,department_code,name,department_type,status,created_at) SELECT NULL, @campus_qn, 'PLANNING', 'Phòng Kế hoạch', 'GENERAL', 'ACTIVE', DATE_SUB(@seed_now, INTERVAL 330 DAY) WHERE NOT EXISTS (SELECT 1 FROM departments WHERE campus_id=@campus_qn AND department_code='PLANNING');

INSERT INTO departments (department_id,campus_id,department_code,name,department_type,status,created_at) SELECT NULL, @campus_qn, 'IT', 'Phòng CNTT', 'GENERAL', 'ACTIVE', DATE_SUB(@seed_now, INTERVAL 330 DAY) WHERE NOT EXISTS (SELECT 1 FROM departments WHERE campus_id=@campus_qn AND department_code='IT');

INSERT INTO departments (department_id,campus_id,department_code,name,department_type,status,created_at) SELECT NULL, @campus_qn, 'FINANCE', 'Phòng Tài chính', 'GENERAL', 'ACTIVE', DATE_SUB(@seed_now, INTERVAL 330 DAY) WHERE NOT EXISTS (SELECT 1 FROM departments WHERE campus_id=@campus_qn AND department_code='FINANCE');

INSERT INTO departments (department_id,campus_id,department_code,name,department_type,status,created_at) SELECT NULL, @campus_qn, 'OPERATIONS', 'Ban Điều hành', 'GENERAL', 'ACTIVE', DATE_SUB(@seed_now, INTERVAL 330 DAY) WHERE NOT EXISTS (SELECT 1 FROM departments WHERE campus_id=@campus_qn AND department_code='OPERATIONS');

INSERT INTO departments (department_id,campus_id,department_code,name,department_type,status,created_at) SELECT NULL, @campus_qn, 'ACADEMIC', 'Phòng Đào tạo', 'GENERAL', 'ACTIVE', DATE_SUB(@seed_now, INTERVAL 330 DAY) WHERE NOT EXISTS (SELECT 1 FROM departments WHERE campus_id=@campus_qn AND department_code='ACADEMIC');

INSERT INTO departments (department_id,campus_id,department_code,name,department_type,status,created_at) SELECT NULL, @campus_qn, 'MARKETING', 'Phòng Truyền thông', 'GENERAL', 'ACTIVE', DATE_SUB(@seed_now, INTERVAL 330 DAY) WHERE NOT EXISTS (SELECT 1 FROM departments WHERE campus_id=@campus_qn AND department_code='MARKETING');

INSERT INTO departments (department_id,campus_id,department_code,name,department_type,status,created_at) SELECT NULL, @campus_qn, 'ARCHIVE_FINANCE', 'Phòng Tài chính lưu trữ', 'GENERAL', 'INACTIVE', DATE_SUB(@seed_now, INTERVAL 280 DAY) WHERE NOT EXISTS (SELECT 1 FROM departments WHERE campus_id=@campus_qn AND department_code='ARCHIVE_FINANCE');

SELECT department_id INTO @dept_hn_ic FROM departments WHERE campus_id=@campus_hn AND department_code='IC' LIMIT 1;

SELECT department_id INTO @dept_hn_admin FROM departments WHERE campus_id=@campus_hn AND department_code='ADMIN' LIMIT 1;

SELECT department_id INTO @dept_hn_planning FROM departments WHERE campus_id=@campus_hn AND department_code='PLANNING' LIMIT 1;

SELECT department_id INTO @dept_hn_it FROM departments WHERE campus_id=@campus_hn AND department_code='IT' LIMIT 1;

SELECT department_id INTO @dept_hn_finance FROM departments WHERE campus_id=@campus_hn AND department_code='FINANCE' LIMIT 1;

SELECT department_id INTO @dept_hn_operations FROM departments WHERE campus_id=@campus_hn AND department_code='OPERATIONS' LIMIT 1;

SELECT department_id INTO @dept_hn_academic FROM departments WHERE campus_id=@campus_hn AND department_code='ACADEMIC' LIMIT 1;

SELECT department_id INTO @dept_hn_marketing FROM departments WHERE campus_id=@campus_hn AND department_code='MARKETING' LIMIT 1;

SELECT department_id INTO @dept_hcm_ic FROM departments WHERE campus_id=@campus_hcm AND department_code='IC' LIMIT 1;

SELECT department_id INTO @dept_hcm_admin FROM departments WHERE campus_id=@campus_hcm AND department_code='ADMIN' LIMIT 1;

SELECT department_id INTO @dept_hcm_planning FROM departments WHERE campus_id=@campus_hcm AND department_code='PLANNING' LIMIT 1;

SELECT department_id INTO @dept_hcm_it FROM departments WHERE campus_id=@campus_hcm AND department_code='IT' LIMIT 1;

SELECT department_id INTO @dept_hcm_finance FROM departments WHERE campus_id=@campus_hcm AND department_code='FINANCE' LIMIT 1;

SELECT department_id INTO @dept_hcm_operations FROM departments WHERE campus_id=@campus_hcm AND department_code='OPERATIONS' LIMIT 1;

SELECT department_id INTO @dept_hcm_academic FROM departments WHERE campus_id=@campus_hcm AND department_code='ACADEMIC' LIMIT 1;

SELECT department_id INTO @dept_hcm_marketing FROM departments WHERE campus_id=@campus_hcm AND department_code='MARKETING' LIMIT 1;

SELECT department_id INTO @dept_dn_ic FROM departments WHERE campus_id=@campus_dn AND department_code='IC' LIMIT 1;

SELECT department_id INTO @dept_dn_admin FROM departments WHERE campus_id=@campus_dn AND department_code='ADMIN' LIMIT 1;

SELECT department_id INTO @dept_dn_planning FROM departments WHERE campus_id=@campus_dn AND department_code='PLANNING' LIMIT 1;

SELECT department_id INTO @dept_dn_it FROM departments WHERE campus_id=@campus_dn AND department_code='IT' LIMIT 1;

SELECT department_id INTO @dept_dn_finance FROM departments WHERE campus_id=@campus_dn AND department_code='FINANCE' LIMIT 1;

SELECT department_id INTO @dept_dn_operations FROM departments WHERE campus_id=@campus_dn AND department_code='OPERATIONS' LIMIT 1;

SELECT department_id INTO @dept_dn_academic FROM departments WHERE campus_id=@campus_dn AND department_code='ACADEMIC' LIMIT 1;

SELECT department_id INTO @dept_dn_marketing FROM departments WHERE campus_id=@campus_dn AND department_code='MARKETING' LIMIT 1;

SELECT department_id INTO @dept_ct_ic FROM departments WHERE campus_id=@campus_ct AND department_code='IC' LIMIT 1;

SELECT department_id INTO @dept_ct_admin FROM departments WHERE campus_id=@campus_ct AND department_code='ADMIN' LIMIT 1;

SELECT department_id INTO @dept_ct_planning FROM departments WHERE campus_id=@campus_ct AND department_code='PLANNING' LIMIT 1;

SELECT department_id INTO @dept_ct_it FROM departments WHERE campus_id=@campus_ct AND department_code='IT' LIMIT 1;

SELECT department_id INTO @dept_ct_finance FROM departments WHERE campus_id=@campus_ct AND department_code='FINANCE' LIMIT 1;

SELECT department_id INTO @dept_ct_operations FROM departments WHERE campus_id=@campus_ct AND department_code='OPERATIONS' LIMIT 1;

SELECT department_id INTO @dept_ct_academic FROM departments WHERE campus_id=@campus_ct AND department_code='ACADEMIC' LIMIT 1;

SELECT department_id INTO @dept_ct_marketing FROM departments WHERE campus_id=@campus_ct AND department_code='MARKETING' LIMIT 1;

SELECT department_id INTO @dept_qn_ic FROM departments WHERE campus_id=@campus_qn AND department_code='IC' LIMIT 1;

SELECT department_id INTO @dept_qn_admin FROM departments WHERE campus_id=@campus_qn AND department_code='ADMIN' LIMIT 1;

SELECT department_id INTO @dept_qn_planning FROM departments WHERE campus_id=@campus_qn AND department_code='PLANNING' LIMIT 1;

SELECT department_id INTO @dept_qn_it FROM departments WHERE campus_id=@campus_qn AND department_code='IT' LIMIT 1;

SELECT department_id INTO @dept_qn_finance FROM departments WHERE campus_id=@campus_qn AND department_code='FINANCE' LIMIT 1;

SELECT department_id INTO @dept_qn_operations FROM departments WHERE campus_id=@campus_qn AND department_code='OPERATIONS' LIMIT 1;

SELECT department_id INTO @dept_qn_academic FROM departments WHERE campus_id=@campus_qn AND department_code='ACADEMIC' LIMIT 1;

SELECT department_id INTO @dept_qn_marketing FROM departments WHERE campus_id=@campus_qn AND department_code='MARKETING' LIMIT 1;

SELECT department_id INTO @dept_qn_archive_finance FROM departments WHERE campus_id=@campus_qn AND department_code='ARCHIVE_FINANCE' LIMIT 1;

SET @u_admin_minh = 100005;

SET @u_ho_ha = 100006;

SET @u_ho_linh = 100007;

SET @u_stafflead_hn = 100008;

SET @u_staff_hn = 100009;

SET @u_stafflead_hcm = 100010;

SET @u_staff_hcm = 100011;

SET @u_stafflead_dn = 100012;

SET @u_staff_dn = 100013;

SET @u_stafflead_ct = 100014;

SET @u_staff_ct = 100015;

SET @u_stafflead_qn = 100016;

SET @u_staff_qn = 100017;

SET @u_deptlead_it_hn = 100018;

SET @u_dept_it_hn = 100019;

SET @u_deptlead_finance_hcm = 100020;

SET @u_dept_finance_hcm = 100021;

SET @u_deptlead_admin_ct = 100022;

SET @u_dept_admin_ct = 100023;

SET @u_student_anh = 100024;

SET @u_student_bao = 100025;

SET @u_student_long = 100026;

SET @u_locked_staff = 100027;

SET @u_inactive_dept = 100028;

SET @u_pending_internal = 100029;

SET @u_rejected_internal = 100030;

SET @v_kim = 100031;

SET @v_lee = 100032;

SET @v_tanaka = 100033;

SET @v_smith = 100034;

SET @v_nguyen_no_dau = 100035;

SET @v_pending_approval_seed = 100036;

SET @v_pending_approval = 100037;

SET @v_short_name = 100038;

SET @v_long_name = 100039;

SET @pwd_hash = '$2a$12$cRpFAxEt9VdUg0orDrPRL.oesxu8ID8WSI2YTsNclVZjRtwi57PFi';

INSERT INTO users (user_id, full_name, email, phone, nationality, password_hash, role_id, sub_role, primary_campus_id, department_id, gender, avatar_url, student_code, fe_id, status, email_verified_at, failed_login_count, locked_until, created_via, first_login_at, last_login_at, created_at, created_by, updated_at, updated_by)
VALUES
  (@u_admin_minh, 'System Administrator', 'admin@fpt.edu.vn', '0901123456', NULL, @pwd_hash, @role_admin, NULL, @campus_hn, NULL, 'UNKNOWN', NULL, NULL, 'FE-SEED-000', 'ACTIVE', DATE_SUB(@seed_now, INTERVAL 80 DAY), '0', NULL, 'MANUAL_CREATED', DATE_SUB(@seed_now, INTERVAL 70 DAY), DATE_SUB(@seed_now, INTERVAL 1 DAY), DATE_SUB(@seed_now, INTERVAL 120 DAY), NULL, @seed_now, NULL),
  (@u_ho_ha, 'Head Office Manager', 'ho@fpt.edu.vn', '0912345678', NULL, @pwd_hash, @role_ho, NULL, @campus_hn, NULL, 'UNKNOWN', NULL, NULL, 'FE-SEED-001', 'ACTIVE', DATE_SUB(@seed_now, INTERVAL 81 DAY), '0', NULL, 'MANUAL_CREATED', DATE_SUB(@seed_now, INTERVAL 71 DAY), DATE_SUB(@seed_now, INTERVAL 2 DAY), DATE_SUB(@seed_now, INTERVAL 121 DAY), @u_admin_minh, @seed_now, @u_admin_minh),
  (@u_ho_linh, 'Đỗ Gia Linh', 'linh.do@company.vn', '0902233445', NULL, @pwd_hash, @role_ho, NULL, @campus_hcm, NULL, 'UNKNOWN', NULL, NULL, 'FE-SEED-002', 'ACTIVE', DATE_SUB(@seed_now, INTERVAL 82 DAY), '0', NULL, 'MANUAL_CREATED', DATE_SUB(@seed_now, INTERVAL 72 DAY), DATE_SUB(@seed_now, INTERVAL 3 DAY), DATE_SUB(@seed_now, INTERVAL 122 DAY), @u_admin_minh, @seed_now, @u_admin_minh),
  (@u_stafflead_hn, 'IC Staff Leader (HN)', 'staff.leader.hn@fpt.edu.vn', '0934567890', NULL, @pwd_hash, @role_staff, 'LEADER', @campus_hn, @dept_hn_ic, 'UNKNOWN', NULL, NULL, 'FE-SEED-003', 'ACTIVE', DATE_SUB(@seed_now, INTERVAL 83 DAY), '0', NULL, 'MANUAL_CREATED', DATE_SUB(@seed_now, INTERVAL 73 DAY), DATE_SUB(@seed_now, INTERVAL 4 DAY), DATE_SUB(@seed_now, INTERVAL 123 DAY), @u_admin_minh, @seed_now, @u_admin_minh),
  (@u_staff_hn, 'IC Staff (HN)', 'staff.hn@fpt.edu.vn', '0945678901', NULL, @pwd_hash, @role_staff, 'STAFF', @campus_hn, @dept_hn_ic, 'UNKNOWN', NULL, NULL, 'FE-SEED-004', 'ACTIVE', DATE_SUB(@seed_now, INTERVAL 84 DAY), '0', NULL, 'MANUAL_CREATED', DATE_SUB(@seed_now, INTERVAL 74 DAY), DATE_SUB(@seed_now, INTERVAL 5 DAY), DATE_SUB(@seed_now, INTERVAL 124 DAY), @u_admin_minh, @seed_now, @u_admin_minh),
  (@u_stafflead_hcm, 'Vũ Lan Anh', 'anh.vu@company.vn', '0976543210', NULL, @pwd_hash, @role_staff, 'LEADER', @campus_hcm, @dept_hcm_ic, 'FEMALE', NULL, NULL, 'FE-SEED-005', 'ACTIVE', DATE_SUB(@seed_now, INTERVAL 85 DAY), '0', NULL, 'MANUAL_CREATED', DATE_SUB(@seed_now, INTERVAL 75 DAY), DATE_SUB(@seed_now, INTERVAL 6 DAY), DATE_SUB(@seed_now, INTERVAL 125 DAY), @u_admin_minh, @seed_now, @u_admin_minh),
  (@u_staff_hcm, 'Nguyễn Văn Nam', 'nam.nguyen@company.vn', '0987654321', NULL, @pwd_hash, @role_staff, 'STAFF', @campus_hcm, @dept_hcm_ic, 'MALE', NULL, NULL, 'FE-SEED-006', 'ACTIVE', DATE_SUB(@seed_now, INTERVAL 86 DAY), '0', NULL, 'MANUAL_CREATED', DATE_SUB(@seed_now, INTERVAL 76 DAY), DATE_SUB(@seed_now, INTERVAL 7 DAY), DATE_SUB(@seed_now, INTERVAL 126 DAY), @u_admin_minh, @seed_now, @u_admin_minh),
  (@u_stafflead_dn, 'Nguyễn Nam', 'nguyen.nam@company.vn', '0961234567', NULL, @pwd_hash, @role_staff, 'LEADER', @campus_dn, @dept_dn_ic, 'MALE', NULL, NULL, 'FE-SEED-007', 'ACTIVE', DATE_SUB(@seed_now, INTERVAL 87 DAY), '0', NULL, 'MANUAL_CREATED', DATE_SUB(@seed_now, INTERVAL 77 DAY), DATE_SUB(@seed_now, INTERVAL 8 DAY), DATE_SUB(@seed_now, INTERVAL 127 DAY), @u_admin_minh, @seed_now, @u_admin_minh),
  (@u_staff_dn, 'Nam Nguyen', 'nam.nguyen.dn@company.vn', '0967654321', NULL, @pwd_hash, @role_staff, 'STAFF', @campus_dn, @dept_dn_ic, 'MALE', NULL, NULL, 'FE-SEED-008', 'ACTIVE', DATE_SUB(@seed_now, INTERVAL 88 DAY), '0', NULL, 'MANUAL_CREATED', DATE_SUB(@seed_now, INTERVAL 78 DAY), DATE_SUB(@seed_now, INTERVAL 9 DAY), DATE_SUB(@seed_now, INTERVAL 128 DAY), @u_admin_minh, @seed_now, @u_admin_minh),
  (@u_stafflead_ct, 'Trương Mỹ Duyên', 'duyen.truong@company.vn', '0923456789', NULL, @pwd_hash, @role_staff, 'LEADER', @campus_ct, @dept_ct_ic, 'FEMALE', NULL, NULL, 'FE-SEED-009', 'ACTIVE', DATE_SUB(@seed_now, INTERVAL 89 DAY), '0', NULL, 'MANUAL_CREATED', DATE_SUB(@seed_now, INTERVAL 79 DAY), DATE_SUB(@seed_now, INTERVAL 10 DAY), DATE_SUB(@seed_now, INTERVAL 129 DAY), @u_admin_minh, @seed_now, @u_admin_minh),
  (@u_staff_ct, 'Đặng Minh Châu', 'chau.dang@company.vn', '0925566778', NULL, @pwd_hash, @role_staff, 'STAFF', @campus_ct, @dept_ct_ic, 'OTHER', NULL, NULL, 'FE-SEED-010', 'ACTIVE', DATE_SUB(@seed_now, INTERVAL 90 DAY), '0', NULL, 'MANUAL_CREATED', DATE_SUB(@seed_now, INTERVAL 80 DAY), DATE_SUB(@seed_now, INTERVAL 11 DAY), DATE_SUB(@seed_now, INTERVAL 130 DAY), @u_admin_minh, @seed_now, @u_admin_minh),
  (@u_stafflead_qn, 'Hoàng Minh Quân', 'quan.hoang@company.vn', '0911002003', NULL, @pwd_hash, @role_staff, 'LEADER', @campus_qn, @dept_qn_ic, 'MALE', NULL, NULL, 'FE-SEED-011', 'ACTIVE', DATE_SUB(@seed_now, INTERVAL 91 DAY), '0', NULL, 'MANUAL_CREATED', DATE_SUB(@seed_now, INTERVAL 81 DAY), DATE_SUB(@seed_now, INTERVAL 12 DAY), DATE_SUB(@seed_now, INTERVAL 131 DAY), @u_admin_minh, @seed_now, @u_admin_minh),
  (@u_staff_qn, 'Lý Thanh Mai', 'mai.ly@company.vn', '0911222333', NULL, @pwd_hash, @role_staff, 'STAFF', @campus_qn, @dept_qn_ic, 'FEMALE', NULL, NULL, 'FE-SEED-012', 'ACTIVE', DATE_SUB(@seed_now, INTERVAL 92 DAY), '0', NULL, 'MANUAL_CREATED', DATE_SUB(@seed_now, INTERVAL 82 DAY), DATE_SUB(@seed_now, INTERVAL 13 DAY), DATE_SUB(@seed_now, INTERVAL 132 DAY), @u_admin_minh, @seed_now, @u_admin_minh),
  (@u_deptlead_it_hn, 'Department Lead (HN)', 'dept.leader.hn@fpt.edu.vn', '0909988776', NULL, @pwd_hash, @role_department, 'LEADER', @campus_hn, @dept_hn_it, 'UNKNOWN', NULL, NULL, 'FE-SEED-013', 'ACTIVE', DATE_SUB(@seed_now, INTERVAL 93 DAY), '0', NULL, 'MANUAL_CREATED', DATE_SUB(@seed_now, INTERVAL 83 DAY), DATE_SUB(@seed_now, INTERVAL 14 DAY), DATE_SUB(@seed_now, INTERVAL 133 DAY), @u_admin_minh, @seed_now, @u_admin_minh),
  (@u_dept_it_hn, 'Department Personnel (HN)', 'dept.hn@fpt.edu.vn', '0903344556', NULL, @pwd_hash, @role_department, 'STAFF', @campus_hn, @dept_hn_it, 'UNKNOWN', NULL, NULL, 'FE-SEED-014', 'ACTIVE', DATE_SUB(@seed_now, INTERVAL 94 DAY), '0', NULL, 'MANUAL_CREATED', DATE_SUB(@seed_now, INTERVAL 84 DAY), DATE_SUB(@seed_now, INTERVAL 15 DAY), DATE_SUB(@seed_now, INTERVAL 134 DAY), @u_admin_minh, @seed_now, @u_admin_minh),
  (@u_deptlead_finance_hcm, 'Ngô Thanh Hương', 'huong.ngo@company.vn', '0906677889', NULL, @pwd_hash, @role_department, 'LEADER', @campus_hcm, @dept_hcm_finance, 'FEMALE', NULL, NULL, 'FE-SEED-015', 'ACTIVE', DATE_SUB(@seed_now, INTERVAL 95 DAY), '0', NULL, 'MANUAL_CREATED', DATE_SUB(@seed_now, INTERVAL 85 DAY), DATE_SUB(@seed_now, INTERVAL 16 DAY), DATE_SUB(@seed_now, INTERVAL 135 DAY), @u_admin_minh, @seed_now, @u_admin_minh),
  (@u_dept_finance_hcm, 'Mai Anh Tuấn', 'tuan.mai@company.vn', '0907788990', NULL, @pwd_hash, @role_department, 'STAFF', @campus_hcm, @dept_hcm_finance, 'MALE', NULL, NULL, 'FE-SEED-016', 'ACTIVE', DATE_SUB(@seed_now, INTERVAL 96 DAY), '0', NULL, 'MANUAL_CREATED', DATE_SUB(@seed_now, INTERVAL 86 DAY), DATE_SUB(@seed_now, INTERVAL 17 DAY), DATE_SUB(@seed_now, INTERVAL 136 DAY), @u_admin_minh, @seed_now, @u_admin_minh),
  (@u_deptlead_admin_ct, 'Lâm Khánh Vy', 'vy.lam@company.vn', '0913456780', NULL, @pwd_hash, @role_department, 'LEADER', @campus_ct, @dept_ct_admin, 'FEMALE', NULL, NULL, 'FE-SEED-017', 'ACTIVE', DATE_SUB(@seed_now, INTERVAL 97 DAY), '0', NULL, 'MANUAL_CREATED', DATE_SUB(@seed_now, INTERVAL 87 DAY), DATE_SUB(@seed_now, INTERVAL 18 DAY), DATE_SUB(@seed_now, INTERVAL 137 DAY), @u_admin_minh, @seed_now, @u_admin_minh),
  (@u_dept_admin_ct, 'Phan Gia Phúc', 'phuc.phan@company.vn', '0919988776', NULL, @pwd_hash, @role_department, 'STAFF', @campus_ct, @dept_ct_admin, 'MALE', NULL, NULL, 'FE-SEED-018', 'ACTIVE', DATE_SUB(@seed_now, INTERVAL 98 DAY), '0', NULL, 'MANUAL_CREATED', DATE_SUB(@seed_now, INTERVAL 88 DAY), DATE_SUB(@seed_now, INTERVAL 19 DAY), DATE_SUB(@seed_now, INTERVAL 138 DAY), @u_admin_minh, @seed_now, @u_admin_minh),
  (@u_student_anh, 'Support Student', 'student@fpt.edu.vn', '0866123456', NULL, @pwd_hash, @role_student, NULL, @campus_hn, NULL, 'UNKNOWN', NULL, 'SE190019', 'FE-SEED-019', 'ACTIVE', DATE_SUB(@seed_now, INTERVAL 99 DAY), '0', NULL, 'MANUAL_CREATED', DATE_SUB(@seed_now, INTERVAL 89 DAY), DATE_SUB(@seed_now, INTERVAL 20 DAY), DATE_SUB(@seed_now, INTERVAL 139 DAY), @u_admin_minh, @seed_now, @u_admin_minh),
  (@u_student_bao, 'Phạm Quốc Bảo Student', 'bao.student@company.vn', '0866543210', NULL, @pwd_hash, @role_student, NULL, @campus_hcm, NULL, 'MALE', NULL, 'SE190020', 'FE-SEED-020', 'ACTIVE', DATE_SUB(@seed_now, INTERVAL 100 DAY), '0', NULL, 'MANUAL_CREATED', DATE_SUB(@seed_now, INTERVAL 90 DAY), DATE_SUB(@seed_now, INTERVAL 1 DAY), DATE_SUB(@seed_now, INTERVAL 140 DAY), @u_admin_minh, @seed_now, @u_admin_minh),
  (@u_student_long, 'Nguyễn Thị Minh Châu Hồng Phúc Gia Bảo Hoàng Anh Tuấn Kiệt', 'long.name.student@company.vn', '0866000001', NULL, @pwd_hash, @role_student, NULL, @campus_ct, NULL, 'UNKNOWN', NULL, 'SE190021', 'FE-SEED-021', 'ACTIVE', DATE_SUB(@seed_now, INTERVAL 101 DAY), '0', NULL, 'MANUAL_CREATED', DATE_SUB(@seed_now, INTERVAL 91 DAY), DATE_SUB(@seed_now, INTERVAL 2 DAY), DATE_SUB(@seed_now, INTERVAL 141 DAY), @u_admin_minh, @seed_now, @u_admin_minh),
  (@u_locked_staff, 'Tạ Quang Huy', 'huy.locked@company.vn', '0918000111', NULL, @pwd_hash, @role_staff, 'STAFF', @campus_hn, @dept_hn_ic, 'MALE', NULL, NULL, 'FE-SEED-022', 'LOCKED', DATE_SUB(@seed_now, INTERVAL 102 DAY), '7', DATE_ADD(@seed_now, INTERVAL 30 MINUTE), 'MANUAL_CREATED', DATE_SUB(@seed_now, INTERVAL 92 DAY), DATE_SUB(@seed_now, INTERVAL 3 DAY), DATE_SUB(@seed_now, INTERVAL 142 DAY), @u_admin_minh, @seed_now, @u_admin_minh),
  (@u_inactive_dept, 'Trịnh Hà My', 'my.inactive@company.vn', '0918111222', NULL, @pwd_hash, @role_department, 'STAFF', @campus_hcm, @dept_hcm_finance, 'FEMALE', NULL, NULL, 'FE-SEED-023', 'INACTIVE', DATE_SUB(@seed_now, INTERVAL 103 DAY), '0', NULL, 'MANUAL_CREATED', DATE_SUB(@seed_now, INTERVAL 93 DAY), DATE_SUB(@seed_now, INTERVAL 4 DAY), DATE_SUB(@seed_now, INTERVAL 143 DAY), @u_admin_minh, @seed_now, @u_admin_minh),
  (@u_pending_internal, 'Nguyễn Quốc Khánh', 'khanh.pending@company.vn', '0918222333', NULL, @pwd_hash, @role_staff, 'STAFF', @campus_dn, @dept_dn_ic, 'MALE', NULL, NULL, 'FE-SEED-024', 'ACTIVE', DATE_SUB(@seed_now, INTERVAL 104 DAY), '0', NULL, 'MANUAL_CREATED', NULL, NULL, DATE_SUB(@seed_now, INTERVAL 144 DAY), @u_admin_minh, @seed_now, @u_admin_minh),
  (@u_rejected_internal, 'Lê Thảo Chi', 'chi.rejected@company.vn', '0918333444', NULL, @pwd_hash, @role_department, 'STAFF', @campus_qn, @dept_qn_archive_finance, 'FEMALE', NULL, NULL, 'FE-SEED-025', 'INACTIVE', DATE_SUB(@seed_now, INTERVAL 105 DAY), '0', NULL, 'MANUAL_CREATED', NULL, NULL, DATE_SUB(@seed_now, INTERVAL 145 DAY), @u_admin_minh, @seed_now, @u_admin_minh),
  (@v_kim, 'External Visitor', 'visitor@example.com', '+821012345678', 'Việt Nam', @pwd_hash, @role_visitor, NULL, NULL, NULL, 'UNKNOWN', NULL, NULL, NULL, 'ACTIVE', DATE_SUB(@seed_now, INTERVAL 106 DAY), '0', NULL, 'VISITOR_FORM', DATE_SUB(@seed_now, INTERVAL 96 DAY), DATE_SUB(@seed_now, INTERVAL 7 DAY), DATE_SUB(@seed_now, INTERVAL 146 DAY), @u_admin_minh, @seed_now, @u_admin_minh),
  (@v_lee, 'Lee Joon Ho', 'lee.joonho@seoultech.example', '+821055512345', 'Hàn Quốc', @pwd_hash, @role_visitor, NULL, NULL, NULL, 'MALE', NULL, NULL, NULL, 'ACTIVE', DATE_SUB(@seed_now, INTERVAL 107 DAY), '0', NULL, 'VISITOR_FORM', DATE_SUB(@seed_now, INTERVAL 97 DAY), DATE_SUB(@seed_now, INTERVAL 8 DAY), DATE_SUB(@seed_now, INTERVAL 147 DAY), @u_admin_minh, @seed_now, @u_admin_minh),
  (@v_tanaka, 'Tanaka Aoi', 'aoi.tanaka@kyoto-global.example', '+819012345678', 'Nhật Bản', @pwd_hash, @role_visitor, NULL, NULL, NULL, 'FEMALE', NULL, NULL, NULL, 'ACTIVE', DATE_SUB(@seed_now, INTERVAL 108 DAY), '0', NULL, 'VISITOR_FORM', DATE_SUB(@seed_now, INTERVAL 98 DAY), DATE_SUB(@seed_now, INTERVAL 9 DAY), DATE_SUB(@seed_now, INTERVAL 148 DAY), @u_admin_minh, @seed_now, @u_admin_minh),
  (@v_smith, 'Emily Smith', 'emily.smith@greentech.example', '+6591234567', 'Singapore', @pwd_hash, @role_visitor, NULL, NULL, NULL, 'FEMALE', NULL, NULL, NULL, 'ACTIVE', DATE_SUB(@seed_now, INTERVAL 109 DAY), '0', NULL, 'VISITOR_FORM', DATE_SUB(@seed_now, INTERVAL 99 DAY), DATE_SUB(@seed_now, INTERVAL 10 DAY), DATE_SUB(@seed_now, INTERVAL 149 DAY), @u_admin_minh, @seed_now, @u_admin_minh),
  (@v_nguyen_no_dau, 'Nguyen Van Nam', 'nguyen.van.nam@partner.example', '0909000009', 'Việt Nam', @pwd_hash, @role_visitor, NULL, NULL, NULL, 'MALE', NULL, NULL, NULL, 'ACTIVE', DATE_SUB(@seed_now, INTERVAL 110 DAY), '0', NULL, 'VISITOR_FORM', DATE_SUB(@seed_now, INTERVAL 100 DAY), DATE_SUB(@seed_now, INTERVAL 11 DAY), DATE_SUB(@seed_now, INTERVAL 150 DAY), @u_admin_minh, @seed_now, @u_admin_minh),
  (@v_pending_approval_seed, 'Nguyễn Thảo My', 'thaomy.pending.approval@partner.example', '0909555001', 'Việt Nam', @pwd_hash, @role_visitor, NULL, NULL, NULL, 'FEMALE', NULL, NULL, NULL, 'ACTIVE', DATE_SUB(@seed_now, INTERVAL 17 DAY), '0', NULL, 'VISITOR_FORM', NULL, NULL, DATE_SUB(@seed_now, INTERVAL 151 DAY), @u_admin_minh, @seed_now, @u_admin_minh),
  (@v_pending_approval, 'Nguyễn Văn Nam', 'nam.pending.approval@partner.example', '0909555002', 'Việt Nam', @pwd_hash, @role_visitor, NULL, NULL, NULL, 'MALE', NULL, NULL, NULL, 'ACTIVE', DATE_SUB(@seed_now, INTERVAL 112 DAY), '0', NULL, 'VISITOR_FORM', NULL, NULL, DATE_SUB(@seed_now, INTERVAL 152 DAY), @u_admin_minh, @seed_now, @u_admin_minh),
  (@v_short_name, 'An', 'an.short@partner.example', '0909555003', 'Việt Nam', @pwd_hash, @role_visitor, NULL, NULL, NULL, 'UNKNOWN', NULL, NULL, NULL, 'ACTIVE', DATE_SUB(@seed_now, INTERVAL 113 DAY), '0', NULL, 'VISITOR_FORM', DATE_SUB(@seed_now, INTERVAL 103 DAY), DATE_SUB(@seed_now, INTERVAL 14 DAY), DATE_SUB(@seed_now, INTERVAL 153 DAY), @u_admin_minh, @seed_now, @u_admin_minh),
  (@v_long_name, 'Nguyễn Thị Minh Anh Phương Khánh Linh Hoàng Bảo Trân Quốc Việt', 'long.name.visitor@partner.example', '0909555004', 'Việt Nam', @pwd_hash, @role_visitor, NULL, NULL, NULL, 'FEMALE', NULL, NULL, NULL, 'ACTIVE', DATE_SUB(@seed_now, INTERVAL 114 DAY), '0', NULL, 'VISITOR_FORM', DATE_SUB(@seed_now, INTERVAL 104 DAY), DATE_SUB(@seed_now, INTERVAL 15 DAY), DATE_SUB(@seed_now, INTERVAL 154 DAY), @u_admin_minh, @seed_now, @u_admin_minh);

UPDATE campuses SET ic_head_user_id=@u_stafflead_hn WHERE campus_id=@campus_hn;
UPDATE campuses SET ic_head_user_id=@u_stafflead_hcm WHERE campus_id=@campus_hcm;
UPDATE campuses SET ic_head_user_id=@u_stafflead_dn WHERE campus_id=@campus_dn;
UPDATE campuses SET ic_head_user_id=@u_stafflead_ct WHERE campus_id=@campus_ct;
UPDATE campuses SET ic_head_user_id=@u_stafflead_qn WHERE campus_id=@campus_qn;
UPDATE departments SET head_user_id=@u_stafflead_hn WHERE department_id=@dept_hn_ic;
UPDATE departments SET head_user_id=@u_stafflead_hcm WHERE department_id=@dept_hcm_ic;
UPDATE departments SET head_user_id=@u_stafflead_dn WHERE department_id=@dept_dn_ic;
UPDATE departments SET head_user_id=@u_stafflead_ct WHERE department_id=@dept_ct_ic;
UPDATE departments SET head_user_id=@u_stafflead_qn WHERE department_id=@dept_qn_ic;
UPDATE departments SET head_user_id=@u_deptlead_it_hn WHERE department_id=@dept_hn_it;
UPDATE departments SET head_user_id=@u_deptlead_finance_hcm WHERE department_id=@dept_hcm_finance;
UPDATE departments SET head_user_id=@u_deptlead_admin_ct WHERE department_id=@dept_ct_admin;


INSERT INTO permissions (permission_id, permission_code, name, permission_group, description, is_system, created_at)
VALUES
  (NULL, 'UC-01.VIEW_HOMEPAGE', 'UC-01 - View Homepage', 'Common', 'Seeded from Role & Permission Matrix: View Homepage', TRUE, DATE_SUB(@seed_now, INTERVAL 300 DAY)),
  (NULL, 'UC-02.SEARCH_INFORMATION', 'UC-02 - Search Information', 'Common', 'Seeded from Role & Permission Matrix: Search Information', TRUE, DATE_SUB(@seed_now, INTERVAL 300 DAY)),
  (NULL, 'UC-03.VIEW_CONTACT_INFO', 'UC-03 - View Contact Info', 'Common', 'Seeded from Role & Permission Matrix: View Contact Info', TRUE, DATE_SUB(@seed_now, INTERVAL 300 DAY)),
  (NULL, 'UC-04.VIEW_POLICY_AND_TERMS', 'UC-04 - View Policy & Terms', 'Common', 'Seeded from Role & Permission Matrix: View Policy & Terms', TRUE, DATE_SUB(@seed_now, INTERVAL 300 DAY)),
  (NULL, 'UC-05.VIEW_FAQ', 'UC-05 - View FAQ', 'Common', 'Seeded from Role & Permission Matrix: View FAQ', TRUE, DATE_SUB(@seed_now, INTERVAL 300 DAY)),
  (NULL, 'UC-06.VIEW_NEWS', 'UC-06 - View News', 'Common', 'Seeded from Role & Permission Matrix: View News', TRUE, DATE_SUB(@seed_now, INTERVAL 300 DAY)),
  (NULL, 'UC-07.VIEW_PARTNERS', 'UC-07 - View Partners', 'Common', 'Seeded from Role & Permission Matrix: View Partners', TRUE, DATE_SUB(@seed_now, INTERVAL 300 DAY)),
  (NULL, 'UC-08.VIEW_GALLERY', 'UC-08 - View Gallery', 'Common', 'Seeded from Role & Permission Matrix: View Gallery', TRUE, DATE_SUB(@seed_now, INTERVAL 300 DAY)),
  (NULL, 'UC-09.VIEW_NOTIFICATIONS', 'UC-09 - View Notifications', 'Common', 'Seeded from Role & Permission Matrix: View Notifications', TRUE, DATE_SUB(@seed_now, INTERVAL 300 DAY)),
  (NULL, 'UC-10.LOGIN_VIA_SSO', 'UC-10 - Login via SSO', 'Authentication', 'Seeded from Role & Permission Matrix: Login via SSO', TRUE, DATE_SUB(@seed_now, INTERVAL 300 DAY)),
  (NULL, 'UC-11.LOGIN_VIA_CREDENTIALS', 'UC-11 - Login via Credentials', 'Authentication', 'Seeded from Role & Permission Matrix: Login via Credentials', TRUE, DATE_SUB(@seed_now, INTERVAL 300 DAY)),
  (NULL, 'UC-12.LOGOUT', 'UC-12 - Logout', 'Authentication', 'Seeded from Role & Permission Matrix: Logout', TRUE, DATE_SUB(@seed_now, INTERVAL 300 DAY)),
  (NULL, 'UC-13.FORGOT_PASSWORD', 'UC-13 - Forgot Password', 'Authentication', 'Seeded from Role & Permission Matrix: Forgot Password', TRUE, DATE_SUB(@seed_now, INTERVAL 300 DAY)),
  (NULL, 'UC-14.VIEW_PROFILE', 'UC-14 - View Profile', 'Profile Management', 'Seeded from Role & Permission Matrix: View Profile', TRUE, DATE_SUB(@seed_now, INTERVAL 300 DAY)),
  (NULL, 'UC-15.UPDATE_PROFILE', 'UC-15 - Update Profile', 'Profile Management', 'Seeded from Role & Permission Matrix: Update Profile', TRUE, DATE_SUB(@seed_now, INTERVAL 300 DAY)),
  (NULL, 'UC-16.CHANGE_PASSWORD', 'UC-16 - Change Password', 'Profile Management', 'Seeded from Role & Permission Matrix: Change Password', TRUE, DATE_SUB(@seed_now, INTERVAL 300 DAY)),
  (NULL, 'UC-17.SUBMIT_VISIT_REQUEST', 'UC-17 - Submit Visit Request', 'Delegation Reception Management', 'Seeded from Role & Permission Matrix: Submit Visit Request', TRUE, DATE_SUB(@seed_now, INTERVAL 300 DAY)),
  (NULL, 'UC-18.APPROVE_CROSS_CAMPUS_REQUEST', 'UC-18 - Approve Cross-Campus Request', 'Delegation Reception Management', 'Seeded from Role & Permission Matrix: Approve Cross-Campus Request', TRUE, DATE_SUB(@seed_now, INTERVAL 300 DAY)),
  (NULL, 'UC-19.VIEW_GUEST_DELEGATION_DETAILS', 'UC-19 - View Guest Delegation Details', 'Delegation Reception Management', 'Seeded from Role & Permission Matrix: View Guest Delegation Details', TRUE, DATE_SUB(@seed_now, INTERVAL 300 DAY)),
  (NULL, 'UC-20.VIEW_GUEST_DELEGATION_LIST', 'UC-20 - View Guest Delegation List', 'Delegation Reception Management', 'Seeded from Role & Permission Matrix: View Guest Delegation List', TRUE, DATE_SUB(@seed_now, INTERVAL 300 DAY)),
  (NULL, 'UC-21.SEARCH_DELEGATIONS', 'UC-21 - Search Delegations', 'Delegation Reception Management', 'Seeded from Role & Permission Matrix: Search Delegations', TRUE, DATE_SUB(@seed_now, INTERVAL 300 DAY)),
  (NULL, 'UC-22.PROCESS_VISIT_REQUEST', 'UC-22 - Process Visit Request', 'Delegation Reception Management', 'Seeded from Role & Permission Matrix: Process Visit Request', TRUE, DATE_SUB(@seed_now, INTERVAL 300 DAY)),
  (NULL, 'UC-23.CREATE_GUEST_DELEGATION', 'UC-23 - Create Guest Delegation', 'Delegation Reception Management', 'Seeded from Role & Permission Matrix: Create Guest Delegation', TRUE, DATE_SUB(@seed_now, INTERVAL 300 DAY)),
  (NULL, 'UC-24.UPDATE_GUEST_DELEGATION', 'UC-24 - Update Guest Delegation', 'Delegation Reception Management', 'Seeded from Role & Permission Matrix: Update Guest Delegation', TRUE, DATE_SUB(@seed_now, INTERVAL 300 DAY)),
  (NULL, 'UC-25.PREPARE_VISIT_LOGISTICS', 'UC-25 - Prepare Visit Logistics', 'Delegation Reception Management', 'Seeded from Role & Permission Matrix: Prepare Visit Logistics', TRUE, DATE_SUB(@seed_now, INTERVAL 300 DAY)),
  (NULL, 'UC-26.UPDATE_VISIT_LOGISTICS', 'UC-26 - Update Visit Logistics', 'Delegation Reception Management', 'Seeded from Role & Permission Matrix: Update Visit Logistics', TRUE, DATE_SUB(@seed_now, INTERVAL 300 DAY)),
  (NULL, 'UC-27.CONFIRM_PARTICIPATION', 'UC-27 - Confirm Participation', 'Delegation Reception Management', 'Seeded from Role & Permission Matrix: Confirm Participation', TRUE, DATE_SUB(@seed_now, INTERVAL 300 DAY)),
  (NULL, 'UC-28.APPROVE_RESOURCE_REQUEST', 'UC-28 - Approve Resource Request', 'Delegation Reception Management', 'Seeded from Role & Permission Matrix: Approve Resource Request', TRUE, DATE_SUB(@seed_now, INTERVAL 300 DAY)),
  (NULL, 'UC-29.PROPOSE_RESOURCE_MODIFICATION', 'UC-29 - Propose Resource Modification', 'Delegation Reception Management', 'Seeded from Role & Permission Matrix: Propose Resource Modification', TRUE, DATE_SUB(@seed_now, INTERVAL 300 DAY)),
  (NULL, 'UC-30.CONFIRM_THE_CHANGE_PROPOSAL', 'UC-30 - Confirm The Change Proposal', 'Delegation Reception Management', 'Seeded from Role & Permission Matrix: Confirm The Change Proposal', TRUE, DATE_SUB(@seed_now, INTERVAL 300 DAY)),
  (NULL, 'UC-31.CREATE_MEETING_MINUTES', 'UC-31 - Create Meeting Minutes', 'Delegation Reception Management', 'Seeded from Role & Permission Matrix: Create Meeting Minutes', TRUE, DATE_SUB(@seed_now, INTERVAL 300 DAY)),
  (NULL, 'UC-32.EDIT_MEETING_MINUTES', 'UC-32 - Edit Meeting Minutes', 'Delegation Reception Management', 'Seeded from Role & Permission Matrix: Edit Meeting Minutes', TRUE, DATE_SUB(@seed_now, INTERVAL 300 DAY)),
  (NULL, 'UC-33.VIEW_MEETING_MINUTES_DETAILS', 'UC-33 - View Meeting Minutes Details', 'Delegation Reception Management', 'Seeded from Role & Permission Matrix: View Meeting Minutes Details', TRUE, DATE_SUB(@seed_now, INTERVAL 300 DAY)),
  (NULL, 'UC-34.SUBMIT_DELEGATION_FEEDBACK', 'UC-34 - Submit Delegation Feedback', 'Delegation Reception Management', 'Seeded from Role & Permission Matrix: Submit Delegation Feedback', TRUE, DATE_SUB(@seed_now, INTERVAL 300 DAY)),
  (NULL, 'UC-35.SCAN_BUSINESS_CARD', 'UC-35 - Scan Business Card', 'Delegation Reception Management', 'Seeded from Role & Permission Matrix: Scan Business Card', TRUE, DATE_SUB(@seed_now, INTERVAL 300 DAY)),
  (NULL, 'UC-36.CREATE_PARTNER_PROFILE', 'UC-36 - Create Partner Profile', 'Delegation Reception Management', 'Seeded from Role & Permission Matrix: Create Partner Profile', TRUE, DATE_SUB(@seed_now, INTERVAL 300 DAY)),
  (NULL, 'UC-37.UPLOAD_ATTACHED_DOCUMENTS', 'UC-37 - Upload Attached Documents', 'Delegation Reception Management', 'Seeded from Role & Permission Matrix: Upload Attached Documents', TRUE, DATE_SUB(@seed_now, INTERVAL 300 DAY)),
  (NULL, 'UC-38.UPLOAD_VISIT_PHOTOS', 'UC-38 - Upload Visit Photos', 'Delegation Reception Management', 'Seeded from Role & Permission Matrix: Upload Visit Photos', TRUE, DATE_SUB(@seed_now, INTERVAL 300 DAY)),
  (NULL, 'UC-39.TAG_FACES_ON_PHOTOS', 'UC-39 - Tag Faces on Photos', 'Delegation Reception Management', 'Seeded from Role & Permission Matrix: Tag Faces on Photos', TRUE, DATE_SUB(@seed_now, INTERVAL 300 DAY)),
  (NULL, 'UC-40.CREATE_NEWS_ARTICLE', 'UC-40 - Create News Article', 'Delegation Reception Management', 'Seeded from Role & Permission Matrix: Create News Article', TRUE, DATE_SUB(@seed_now, INTERVAL 300 DAY)),
  (NULL, 'UC-41.CLOSE_DELEGATION', 'UC-41 - Close Delegation', 'Delegation Reception Management', 'Seeded from Role & Permission Matrix: Close Delegation', TRUE, DATE_SUB(@seed_now, INTERVAL 300 DAY)),
  (NULL, 'UC-42.VIEW_EMAIL_TEMPLATE_LIST', 'UC-42 - View Email Template List', 'Email Management', 'Seeded from Role & Permission Matrix: View Email Template List', TRUE, DATE_SUB(@seed_now, INTERVAL 300 DAY)),
  (NULL, 'UC-43.VIEW_EMAIL_TEMPLATE_DETAIL', 'UC-43 - View Email Template Detail', 'Email Management', 'Seeded from Role & Permission Matrix: View Email Template Detail', TRUE, DATE_SUB(@seed_now, INTERVAL 300 DAY)),
  (NULL, 'UC-44.UPDATE_EMAIL_TEMPLATE', 'UC-44 - Update Email Template', 'Email Management', 'Seeded from Role & Permission Matrix: Update Email Template', TRUE, DATE_SUB(@seed_now, INTERVAL 300 DAY)),
  (NULL, 'UC-45.CREATE_EMAIL_TEMPLATE', 'UC-45 - Create Email Template', 'Email Management', 'Seeded from Role & Permission Matrix: Create Email Template', TRUE, DATE_SUB(@seed_now, INTERVAL 300 DAY)),
  (NULL, 'UC-46.EDIT_EMAIL_CONTENT', 'UC-46 - Edit Email Content', 'Email Management', 'Seeded from Role & Permission Matrix: Edit Email Content', TRUE, DATE_SUB(@seed_now, INTERVAL 300 DAY)),
  (NULL, 'UC-47.SEND_EMAIL', 'UC-47 - Send Email', 'Email Management', 'Seeded from Role & Permission Matrix: Send Email', TRUE, DATE_SUB(@seed_now, INTERVAL 300 DAY)),
  (NULL, 'UC-48.VIEW_EMAIL', 'UC-48 - View Email', 'Email Management', 'Seeded from Role & Permission Matrix: View Email', TRUE, DATE_SUB(@seed_now, INTERVAL 300 DAY)),
  (NULL, 'UC-49.REPLY_TO_EMAIL', 'UC-49 - Reply to Email', 'Email Management', 'Seeded from Role & Permission Matrix: Reply to Email', TRUE, DATE_SUB(@seed_now, INTERVAL 300 DAY)),
  (NULL, 'UC-50.PROCESS_PARTNER_CREATION_REQUEST', 'UC-50 - Process Partner Creation Request', 'Partner Management', 'Seeded from Role & Permission Matrix: Process Partner Creation Request', TRUE, DATE_SUB(@seed_now, INTERVAL 300 DAY)),
  (NULL, 'UC-51.EDIT_PARTNER_INFORMATION', 'UC-51 - Edit Partner Information', 'Partner Management', 'Seeded from Role & Permission Matrix: Edit Partner Information', TRUE, DATE_SUB(@seed_now, INTERVAL 300 DAY)),
  (NULL, 'UC-52.VIEW_PARTNER_LISTS', 'UC-52 - View Partner Lists', 'Partner Management', 'Seeded from Role & Permission Matrix: View Partner Lists', TRUE, DATE_SUB(@seed_now, INTERVAL 300 DAY)),
  (NULL, 'UC-53.SEARCH_PARTNERS', 'UC-53 - Search Partners', 'Partner Management', 'Seeded from Role & Permission Matrix: Search Partners', TRUE, DATE_SUB(@seed_now, INTERVAL 300 DAY)),
  (NULL, 'UC-54.VIEW_PARTNER_DETAILS', 'UC-54 - View Partner Details', 'Partner Management', 'Seeded from Role & Permission Matrix: View Partner Details', TRUE, DATE_SUB(@seed_now, INTERVAL 300 DAY)),
  (NULL, 'UC-55.VIEW_DOCUMENT_LIST', 'UC-55 - View Document List', 'Document Management', 'Seeded from Role & Permission Matrix: View Document List', TRUE, DATE_SUB(@seed_now, INTERVAL 300 DAY)),
  (NULL, 'UC-56.SEARCH_DOCUMENTS', 'UC-56 - Search Documents', 'Document Management', 'Seeded from Role & Permission Matrix: Search Documents', TRUE, DATE_SUB(@seed_now, INTERVAL 300 DAY)),
  (NULL, 'UC-57.VIEW_GALLERY_ITEM_LIST', 'UC-57 - View Gallery Item List', 'Gallery Management', 'Seeded from Role & Permission Matrix: View Gallery Item List', TRUE, DATE_SUB(@seed_now, INTERVAL 300 DAY)),
  (NULL, 'UC-58.SEARCH_GALLERY_ITEMS', 'UC-58 - Search Gallery Items', 'Gallery Management', 'Seeded from Role & Permission Matrix: Search Gallery Items', TRUE, DATE_SUB(@seed_now, INTERVAL 300 DAY)),
  (NULL, 'UC-59.ADD_GALLERY_ITEM', 'UC-59 - Add Gallery Item', 'Gallery Management', 'Seeded from Role & Permission Matrix: Add Gallery Item', TRUE, DATE_SUB(@seed_now, INTERVAL 300 DAY)),
  (NULL, 'UC-60.UPDATE_GALLERY_ITEM', 'UC-60 - Update Gallery Item', 'Gallery Management', 'Seeded from Role & Permission Matrix: Update Gallery Item', TRUE, DATE_SUB(@seed_now, INTERVAL 300 DAY)),
  (NULL, 'UC-61.DELETE_GALLERY_ITEM', 'UC-61 - Delete Gallery Item', 'Gallery Management', 'Seeded from Role & Permission Matrix: Delete Gallery Item', TRUE, DATE_SUB(@seed_now, INTERVAL 300 DAY)),
  (NULL, 'UC-62.VIEW_MINUTES_LIST', 'UC-62 - View Minutes List', 'Minutes Management', 'Seeded from Role & Permission Matrix: View Minutes List', TRUE, DATE_SUB(@seed_now, INTERVAL 300 DAY)),
  (NULL, 'UC-63.SEARCH_FILTER_MINUTES', 'UC-63 - Search/Filter Minutes', 'Minutes Management', 'Seeded from Role & Permission Matrix: Search/Filter Minutes', TRUE, DATE_SUB(@seed_now, INTERVAL 300 DAY)),
  (NULL, 'UC-64.VIEW_LIST_FAQ', 'UC-64 - View List FAQ', 'FAQ Management', 'Seeded from Role & Permission Matrix: View List FAQ', TRUE, DATE_SUB(@seed_now, INTERVAL 300 DAY)),
  (NULL, 'UC-65.CREATE_FAQ', 'UC-65 - Create FAQ', 'FAQ Management', 'Seeded from Role & Permission Matrix: Create FAQ', TRUE, DATE_SUB(@seed_now, INTERVAL 300 DAY)),
  (NULL, 'UC-66.UPDATE_FAQ', 'UC-66 - Update FAQ', 'FAQ Management', 'Seeded from Role & Permission Matrix: Update FAQ', TRUE, DATE_SUB(@seed_now, INTERVAL 300 DAY)),
  (NULL, 'UC-67.CHANGE_FAQ_VISIBILITY', 'UC-67 - Change FAQ Visibility', 'FAQ Management', 'Seeded from Role & Permission Matrix: Change FAQ Visibility', TRUE, DATE_SUB(@seed_now, INTERVAL 300 DAY)),
  (NULL, 'UC-68.SEARCH_FAQ', 'UC-68 - Search FAQ', 'FAQ Management', 'Seeded from Role & Permission Matrix: Search FAQ', TRUE, DATE_SUB(@seed_now, INTERVAL 300 DAY)),
  (NULL, 'UC-69.VIEW_DASHBOARD_STATISTICS', 'UC-69 - View Dashboard Statistics', 'Report Management', 'Seeded from Role & Permission Matrix: View Dashboard Statistics', TRUE, DATE_SUB(@seed_now, INTERVAL 300 DAY)),
  (NULL, 'UC-70.EXPORT_STATISTICS_REPORT', 'UC-70 - Export Statistics Report', 'Report Management', 'Seeded from Role & Permission Matrix: Export Statistics Report', TRUE, DATE_SUB(@seed_now, INTERVAL 300 DAY)),
  (NULL, 'UC-71.FILTER_DASHBOARD_BY_TIME', 'UC-71 - Filter Dashboard By Time', 'Report Management', 'Seeded from Role & Permission Matrix: Filter Dashboard By Time', TRUE, DATE_SUB(@seed_now, INTERVAL 300 DAY)),
  (NULL, 'UC-72.VIEW_MY_EVENTS', 'UC-72 - View My Events', 'Calendar Management', 'Seeded from Role & Permission Matrix: View My Events', TRUE, DATE_SUB(@seed_now, INTERVAL 300 DAY)),
  (NULL, 'UC-73.VIEW_DEPARTMENT_CALENDAR', 'UC-73 - View Department Calendar', 'Calendar Management', 'Seeded from Role & Permission Matrix: View Department Calendar', TRUE, DATE_SUB(@seed_now, INTERVAL 300 DAY)),
  (NULL, 'UC-74.SWITCH_VIEW_MODE', 'UC-74 - Switch View Mode', 'Calendar Management', 'Seeded from Role & Permission Matrix: Switch View Mode', TRUE, DATE_SUB(@seed_now, INTERVAL 300 DAY)),
  (NULL, 'UC-75.ADD_PERSONAL_EVENT', 'UC-75 - Add Personal Event', 'Calendar Management', 'Seeded from Role & Permission Matrix: Add Personal Event', TRUE, DATE_SUB(@seed_now, INTERVAL 300 DAY)),
  (NULL, 'UC-76.DELETE_PERSONAL_EVENT', 'UC-76 - Delete Personal Event', 'Calendar Management', 'Seeded from Role & Permission Matrix: Delete Personal Event', TRUE, DATE_SUB(@seed_now, INTERVAL 300 DAY)),
  (NULL, 'UC-77.UPDATE_PERSONAL_EVENT', 'UC-77 - Update Personal Event', 'Calendar Management', 'Seeded from Role & Permission Matrix: Update Personal Event', TRUE, DATE_SUB(@seed_now, INTERVAL 300 DAY)),
  (NULL, 'UC-78.VIEW_EVENT_DETAILS', 'UC-78 - View Event Details', 'Calendar Management', 'Seeded from Role & Permission Matrix: View Event Details', TRUE, DATE_SUB(@seed_now, INTERVAL 300 DAY)),
  (NULL, 'UC-79.SEARCH_FILTER_FEEDBACK', 'UC-79 - Search/Filter Feedback', 'Feedback Management', 'Seeded from Role & Permission Matrix: Search/Filter Feedback', TRUE, DATE_SUB(@seed_now, INTERVAL 300 DAY)),
  (NULL, 'UC-80.VIEW_FEEDBACK_SUMMARY', 'UC-80 - View Feedback Summary', 'Feedback Management', 'Seeded from Role & Permission Matrix: View Feedback Summary', TRUE, DATE_SUB(@seed_now, INTERVAL 300 DAY)),
  (NULL, 'UC-81.ADD_NEW_CAMPUS', 'UC-81 - Add New Campus', 'Campus Management', 'Seeded from Role & Permission Matrix: Add New Campus', TRUE, DATE_SUB(@seed_now, INTERVAL 300 DAY)),
  (NULL, 'UC-82.VIEW_CAMPUS_LIST', 'UC-82 - View Campus List', 'Campus Management', 'Seeded from Role & Permission Matrix: View Campus List', TRUE, DATE_SUB(@seed_now, INTERVAL 300 DAY)),
  (NULL, 'UC-83.SEARCH_AND_FILTER_CAMPUS', 'UC-83 - Search and Filter Campus', 'Campus Management', 'Seeded from Role & Permission Matrix: Search and Filter Campus', TRUE, DATE_SUB(@seed_now, INTERVAL 300 DAY)),
  (NULL, 'UC-84.VIEW_CAMPUS_DETAILS', 'UC-84 - View Campus Details', 'Campus Management', 'Seeded from Role & Permission Matrix: View Campus Details', TRUE, DATE_SUB(@seed_now, INTERVAL 300 DAY)),
  (NULL, 'UC-85.UPDATE_CAMPUS', 'UC-85 - Update Campus', 'Campus Management', 'Seeded from Role & Permission Matrix: Update Campus', TRUE, DATE_SUB(@seed_now, INTERVAL 300 DAY)),
  (NULL, 'UC-86.MANAGE_CAMPUS_STATUS', 'UC-86 - Manage Campus Status', 'Campus Management', 'Seeded from Role & Permission Matrix: Manage Campus Status', TRUE, DATE_SUB(@seed_now, INTERVAL 300 DAY)),
  (NULL, 'UC-87.ASSIGN_CAMPUS_LEAD', 'UC-87 - Assign Campus Lead', 'Campus Management', 'Seeded from Role & Permission Matrix: Assign Campus Lead', TRUE, DATE_SUB(@seed_now, INTERVAL 300 DAY)),
  (NULL, 'UC-88.APPROVE_NEWS', 'UC-88 - Approve News', 'News Management', 'Seeded from Role & Permission Matrix: Approve News', TRUE, DATE_SUB(@seed_now, INTERVAL 300 DAY)),
  (NULL, 'UC-89.PUBLISH_NEWS', 'UC-89 - Publish News', 'News Management', 'Seeded from Role & Permission Matrix: Publish News', TRUE, DATE_SUB(@seed_now, INTERVAL 300 DAY)),
  (NULL, 'UC-90.VIEW_NEWS_LIST', 'UC-90 - View News List', 'News Management', 'Seeded from Role & Permission Matrix: View News List', TRUE, DATE_SUB(@seed_now, INTERVAL 300 DAY)),
  (NULL, 'UC-91.VIEW_NEWS_DETAILS', 'UC-91 - View News Details', 'News Management', 'Seeded from Role & Permission Matrix: View News Details', TRUE, DATE_SUB(@seed_now, INTERVAL 300 DAY)),
  (NULL, 'UC-92.ADD_MULTILINGUAL_NEWS', 'UC-92 - Add Multilingual News', 'News Management', 'Seeded from Role & Permission Matrix: Add Multilingual News', TRUE, DATE_SUB(@seed_now, INTERVAL 300 DAY)),
  (NULL, 'UC-93.MANAGE_NEWS_VISIBILITY', 'UC-93 - Manage News Visibility', 'News Management', 'Seeded from Role & Permission Matrix: Manage News Visibility', TRUE, DATE_SUB(@seed_now, INTERVAL 300 DAY)),
  (NULL, 'UC-94.EDIT_NEWS', 'UC-94 - Edit News', 'News Management', 'Seeded from Role & Permission Matrix: Edit News', TRUE, DATE_SUB(@seed_now, INTERVAL 300 DAY)),
  (NULL, 'UC-95.VIEW_ACCOUNT_LIST', 'UC-95 - View Account List', 'Account Management', 'Seeded from Role & Permission Matrix: View Account List', TRUE, DATE_SUB(@seed_now, INTERVAL 300 DAY)),
  (NULL, 'UC-96.CREATE_ACCOUNT', 'UC-96 - Create Account', 'Account Management', 'Seeded from Role & Permission Matrix: Create Account', TRUE, DATE_SUB(@seed_now, INTERVAL 300 DAY)),
  (NULL, 'UC-97.MANAGE_ACCOUNT_STATUS', 'UC-97 - Manage Account Status', 'Account Management', 'Seeded from Role & Permission Matrix: Manage Account Status', TRUE, DATE_SUB(@seed_now, INTERVAL 300 DAY)),
  (NULL, 'UC-98.VIEW_ACCOUNT_DETAILS', 'UC-98 - View Account Details', 'Account Management', 'Seeded from Role & Permission Matrix: View Account Details', TRUE, DATE_SUB(@seed_now, INTERVAL 300 DAY)),
  (NULL, 'UC-99.SEARCH_AND_FILTER_ACCOUNTS', 'UC-99 - Search and Filter Accounts', 'Account Management', 'Seeded from Role & Permission Matrix: Search and Filter Accounts', TRUE, DATE_SUB(@seed_now, INTERVAL 300 DAY)),
  (NULL, 'UC-100.UPDATE_ACCOUNT_ROLE', 'UC-100 - Update Account Role', 'Account Management', 'Seeded from Role & Permission Matrix: Update Account Role', TRUE, DATE_SUB(@seed_now, INTERVAL 300 DAY)),
  (NULL, 'UC-101.ADD_NEW_DEPARTMENT', 'UC-101 - Add New Department', 'Department Management', 'Seeded from Role & Permission Matrix: Add New Department', TRUE, DATE_SUB(@seed_now, INTERVAL 300 DAY)),
  (NULL, 'UC-102.UPDATE_DEPARTMENT', 'UC-102 - Update Department', 'Department Management', 'Seeded from Role & Permission Matrix: Update Department', TRUE, DATE_SUB(@seed_now, INTERVAL 300 DAY)),
  (NULL, 'UC-103.SEARCH_AND_FILTER_DEPARTMENTS', 'UC-103 - Search and Filter Departments', 'Department Management', 'Seeded from Role & Permission Matrix: Search and Filter Departments', TRUE, DATE_SUB(@seed_now, INTERVAL 300 DAY)),
  (NULL, 'UC-104.VIEW_DEPARTMENT_LIST', 'UC-104 - View Department List', 'Department Management', 'Seeded from Role & Permission Matrix: View Department List', TRUE, DATE_SUB(@seed_now, INTERVAL 300 DAY)),
  (NULL, 'UC-105.VIEW_DEPARTMENT_DETAILS', 'UC-105 - View Department Details', 'Department Management', 'Seeded from Role & Permission Matrix: View Department Details', TRUE, DATE_SUB(@seed_now, INTERVAL 300 DAY)),
  (NULL, 'UC-106.MANAGE_DEPARTMENT_STATUS', 'UC-106 - Manage Department Status', 'Department Management', 'Seeded from Role & Permission Matrix: Manage Department Status', TRUE, DATE_SUB(@seed_now, INTERVAL 300 DAY)),
  (NULL, 'UC-107.ADD_DEPARTMENT_PERSONNEL', 'UC-107 - Add Department Personnel', 'Department Management', 'Seeded from Role & Permission Matrix: Add Department Personnel', TRUE, DATE_SUB(@seed_now, INTERVAL 300 DAY)),
  (NULL, 'UC-108.VIEW_PERSONNEL_DETAILS', 'UC-108 - View Personnel Details', 'Department Management', 'Seeded from Role & Permission Matrix: View Personnel Details', TRUE, DATE_SUB(@seed_now, INTERVAL 300 DAY)),
  (NULL, 'UC-109.SEARCH_PERSONNEL', 'UC-109 - Search Personnel', 'Department Management', 'Seeded from Role & Permission Matrix: Search Personnel', TRUE, DATE_SUB(@seed_now, INTERVAL 300 DAY)),
  (NULL, 'UC-110.REVIEW_ASSIGNED_TASKS', 'UC-110 - Review Assigned Tasks', 'Department Management', 'Seeded from Role & Permission Matrix: Review Assigned Tasks', TRUE, DATE_SUB(@seed_now, INTERVAL 300 DAY)),
  (NULL, 'UC-111.ASSIGN_TASKS', 'UC-111 - Assign Tasks', 'Department Management', 'Seeded from Role & Permission Matrix: Assign Tasks', TRUE, DATE_SUB(@seed_now, INTERVAL 300 DAY)),
  (NULL, 'UC-112.SIGN_THE_SERVICE_DELIVERY_REPORT', 'UC-112 - Sign The Service Delivery Report', 'Department Management', 'Seeded from Role & Permission Matrix: Sign The Service Delivery Report', TRUE, DATE_SUB(@seed_now, INTERVAL 300 DAY)),
  (NULL, 'UC-113.REMOVE_PERSONNEL', 'UC-113 - Remove Personnel', 'Department Management', 'Seeded from Role & Permission Matrix: Remove Personnel', TRUE, DATE_SUB(@seed_now, INTERVAL 300 DAY)),
  (NULL, 'UC-114.VIEW_COORDINATION_TASKS', 'UC-114 - View Coordination Tasks', 'Department Management', 'Seeded from Role & Permission Matrix: View Coordination Tasks', TRUE, DATE_SUB(@seed_now, INTERVAL 300 DAY)),
  (NULL, 'UC-115.SEARCH_COORDINATION_TASKS', 'UC-115 - Search Coordination Tasks', 'Department Management', 'Seeded from Role & Permission Matrix: Search Coordination Tasks', TRUE, DATE_SUB(@seed_now, INTERVAL 300 DAY)),
  (NULL, 'UC-116.REASSIGN_DEPARTMENT_LEAD', 'UC-116 - Reassign Department Lead', 'Department Management', 'Seeded from Role & Permission Matrix: Reassign Department Lead', TRUE, DATE_SUB(@seed_now, INTERVAL 300 DAY)),
  (NULL, 'UC-117.VIEW_ROLE_LIST', 'UC-117 - View Role List', 'Role & Permission Management', 'Seeded from Role & Permission Matrix: View Role List', TRUE, DATE_SUB(@seed_now, INTERVAL 300 DAY)),
  (NULL, 'UC-118.CREATE_NEW_ROLE', 'UC-118 - Create New Role', 'Role & Permission Management', 'Seeded from Role & Permission Matrix: Create New Role', TRUE, DATE_SUB(@seed_now, INTERVAL 300 DAY)),
  (NULL, 'UC-119.CONFIGURE_ROLE_PERMISSIONS', 'UC-119 - Configure Role Permissions', 'Role & Permission Management', 'Seeded from Role & Permission Matrix: Configure Role Permissions', TRUE, DATE_SUB(@seed_now, INTERVAL 300 DAY)),
  (NULL, 'UC-120.UPDATE_ROLE_DETAILS', 'UC-120 - Update Role Details', 'Role & Permission Management', 'Seeded from Role & Permission Matrix: Update Role Details', TRUE, DATE_SUB(@seed_now, INTERVAL 300 DAY)),
  (NULL, 'UC-121.DISABLE_DELETE_ROLE', 'UC-121 - Disable/Delete Role', 'Role & Permission Management', 'Seeded from Role & Permission Matrix: Disable/Delete Role', TRUE, DATE_SUB(@seed_now, INTERVAL 300 DAY)),
  (NULL, 'UC-122.VIEW_API_CONFIGURATION', 'UC-122 - View API Configuration', 'API Management', 'Seeded from Role & Permission Matrix: View API Configuration', TRUE, DATE_SUB(@seed_now, INTERVAL 300 DAY)),
  (NULL, 'UC-123.CREATE_API_CONFIGURATION', 'UC-123 - Create API Configuration', 'API Management', 'Seeded from Role & Permission Matrix: Create API Configuration', TRUE, DATE_SUB(@seed_now, INTERVAL 300 DAY)),
  (NULL, 'UC-124.UPDATE_API_CONFIGURATION', 'UC-124 - Update API Configuration', 'API Management', 'Seeded from Role & Permission Matrix: Update API Configuration', TRUE, DATE_SUB(@seed_now, INTERVAL 300 DAY)),
  (NULL, 'UC-125.DELETE_API_CONFIGURATION', 'UC-125 - Delete API Configuration', 'API Management', 'Seeded from Role & Permission Matrix: Delete API Configuration', TRUE, DATE_SUB(@seed_now, INTERVAL 300 DAY)),
  (NULL, 'UC-126.TEST_API_CONNECTION', 'UC-126 - Test API Connection', 'API Management', 'Seeded from Role & Permission Matrix: Test API Connection', TRUE, DATE_SUB(@seed_now, INTERVAL 300 DAY)),
  (NULL, 'UC-127.MANAGE_API_STATUS', 'UC-127 - Manage API Status', 'API Management', 'Seeded from Role & Permission Matrix: Manage API Status', TRUE, DATE_SUB(@seed_now, INTERVAL 300 DAY)),
  (NULL, 'UC-128.CONFIGURE_REQUEST_LIMIT', 'UC-128 - Configure Request Limit', 'API Management', 'Seeded from Role & Permission Matrix: Configure Request Limit', TRUE, DATE_SUB(@seed_now, INTERVAL 300 DAY)),
  (NULL, 'UC-129.VIEW_API_LOGS', 'UC-129 - View API Logs', 'API Management', 'Seeded from Role & Permission Matrix: View API Logs', TRUE, DATE_SUB(@seed_now, INTERVAL 300 DAY)),
  (NULL, 'UC-130.SEARCH_API_LOGS', 'UC-130 - Search API Logs', 'API Management', 'Seeded from Role & Permission Matrix: Search API Logs', TRUE, DATE_SUB(@seed_now, INTERVAL 300 DAY)),
  (NULL, 'UC-131.CREATE_AGENDA_TEMPLATE', 'UC-131 - Create Agenda Template', 'Agenda Templates Management', 'Seeded from Role & Permission Matrix: Create Agenda Template', TRUE, DATE_SUB(@seed_now, INTERVAL 300 DAY)),
  (NULL, 'UC-132.UPDATE_AGENDA_TEMPLATE', 'UC-132 - Update Agenda Template', 'Agenda Templates Management', 'Seeded from Role & Permission Matrix: Update Agenda Template', TRUE, DATE_SUB(@seed_now, INTERVAL 300 DAY)),
  (NULL, 'UC-133.DELETE_AGENDA_TEMPLATE', 'UC-133 - Delete Agenda Template', 'Agenda Templates Management', 'Seeded from Role & Permission Matrix: Delete Agenda Template', TRUE, DATE_SUB(@seed_now, INTERVAL 300 DAY)),
  (NULL, 'UC-134.VIEW_AGENDA_TEMPLATE_LIST', 'UC-134 - View Agenda Template List', 'Agenda Templates Management', 'Seeded from Role & Permission Matrix: View Agenda Template List', TRUE, DATE_SUB(@seed_now, INTERVAL 300 DAY)),
  (NULL, 'UC-135.VIEW_AGENDA_TEMPLATE_DETAIL', 'UC-135 - View Agenda Template Detail', 'Agenda Templates Management', 'Seeded from Role & Permission Matrix: View Agenda Template Detail', TRUE, DATE_SUB(@seed_now, INTERVAL 300 DAY));

-- =====================================================================
-- RBAC Permission Matrix v0.2 seed
-- Source of truth: Role & Permission Matrix v0.2.
-- IMPORTANT: No merged-role overgrant. STAFF/DEPARTMENT permissions are split by sub_role.
--   STAFF + Leader = Staff Leader
--   STAFF + Staff  = Staff
--   DEPARTMENT  + Leader = Department Lead
--   DEPARTMENT  + Staff  = Department
--   ADMIN/HO/STUDENT/VISITOR use sub_role = 'NONE'.
-- =====================================================================

INSERT INTO role_permissions
  (role_id, sub_role, permission_id, permission_level, granted_at, granted_by)
SELECT
  r.role_id,
  x.sub_role,
  p.permission_id,
  x.permission_level,
  @seed_now,
  @u_admin_minh
FROM (
  SELECT 'HO' AS role_code, 'NONE' AS sub_role, 'UC-01.VIEW_HOMEPAGE' AS permission_code, 'R' AS permission_level
  UNION ALL SELECT 'ADMIN' AS role_code, 'NONE' AS sub_role, 'UC-01.VIEW_HOMEPAGE' AS permission_code, 'R' AS permission_level
  UNION ALL SELECT 'STAFF' AS role_code, 'LEADER' AS sub_role, 'UC-01.VIEW_HOMEPAGE' AS permission_code, 'R' AS permission_level
  UNION ALL SELECT 'STAFF' AS role_code, 'STAFF' AS sub_role, 'UC-01.VIEW_HOMEPAGE' AS permission_code, 'R' AS permission_level
  UNION ALL SELECT 'DEPARTMENT' AS role_code, 'LEADER' AS sub_role, 'UC-01.VIEW_HOMEPAGE' AS permission_code, 'R' AS permission_level
  UNION ALL SELECT 'DEPARTMENT' AS role_code, 'STAFF' AS sub_role, 'UC-01.VIEW_HOMEPAGE' AS permission_code, 'R' AS permission_level
  UNION ALL SELECT 'STUDENT' AS role_code, 'NONE' AS sub_role, 'UC-01.VIEW_HOMEPAGE' AS permission_code, 'R' AS permission_level
  UNION ALL SELECT 'VISITOR' AS role_code, 'NONE' AS sub_role, 'UC-01.VIEW_HOMEPAGE' AS permission_code, 'R' AS permission_level
  UNION ALL SELECT 'HO' AS role_code, 'NONE' AS sub_role, 'UC-02.SEARCH_INFORMATION' AS permission_code, 'R' AS permission_level
  UNION ALL SELECT 'ADMIN' AS role_code, 'NONE' AS sub_role, 'UC-02.SEARCH_INFORMATION' AS permission_code, 'R' AS permission_level
  UNION ALL SELECT 'STAFF' AS role_code, 'LEADER' AS sub_role, 'UC-02.SEARCH_INFORMATION' AS permission_code, 'R' AS permission_level
  UNION ALL SELECT 'STAFF' AS role_code, 'STAFF' AS sub_role, 'UC-02.SEARCH_INFORMATION' AS permission_code, 'R' AS permission_level
  UNION ALL SELECT 'DEPARTMENT' AS role_code, 'LEADER' AS sub_role, 'UC-02.SEARCH_INFORMATION' AS permission_code, 'R' AS permission_level
  UNION ALL SELECT 'DEPARTMENT' AS role_code, 'STAFF' AS sub_role, 'UC-02.SEARCH_INFORMATION' AS permission_code, 'R' AS permission_level
  UNION ALL SELECT 'STUDENT' AS role_code, 'NONE' AS sub_role, 'UC-02.SEARCH_INFORMATION' AS permission_code, 'R' AS permission_level
  UNION ALL SELECT 'VISITOR' AS role_code, 'NONE' AS sub_role, 'UC-02.SEARCH_INFORMATION' AS permission_code, 'R' AS permission_level
  UNION ALL SELECT 'HO' AS role_code, 'NONE' AS sub_role, 'UC-03.VIEW_CONTACT_INFO' AS permission_code, 'R' AS permission_level
  UNION ALL SELECT 'ADMIN' AS role_code, 'NONE' AS sub_role, 'UC-03.VIEW_CONTACT_INFO' AS permission_code, 'R' AS permission_level
  UNION ALL SELECT 'STAFF' AS role_code, 'LEADER' AS sub_role, 'UC-03.VIEW_CONTACT_INFO' AS permission_code, 'R' AS permission_level
  UNION ALL SELECT 'STAFF' AS role_code, 'STAFF' AS sub_role, 'UC-03.VIEW_CONTACT_INFO' AS permission_code, 'R' AS permission_level
  UNION ALL SELECT 'DEPARTMENT' AS role_code, 'LEADER' AS sub_role, 'UC-03.VIEW_CONTACT_INFO' AS permission_code, 'R' AS permission_level
  UNION ALL SELECT 'DEPARTMENT' AS role_code, 'STAFF' AS sub_role, 'UC-03.VIEW_CONTACT_INFO' AS permission_code, 'R' AS permission_level
  UNION ALL SELECT 'STUDENT' AS role_code, 'NONE' AS sub_role, 'UC-03.VIEW_CONTACT_INFO' AS permission_code, 'R' AS permission_level
  UNION ALL SELECT 'VISITOR' AS role_code, 'NONE' AS sub_role, 'UC-03.VIEW_CONTACT_INFO' AS permission_code, 'R' AS permission_level
  UNION ALL SELECT 'HO' AS role_code, 'NONE' AS sub_role, 'UC-04.VIEW_POLICY_AND_TERMS' AS permission_code, 'R' AS permission_level
  UNION ALL SELECT 'ADMIN' AS role_code, 'NONE' AS sub_role, 'UC-04.VIEW_POLICY_AND_TERMS' AS permission_code, 'R' AS permission_level
  UNION ALL SELECT 'STAFF' AS role_code, 'LEADER' AS sub_role, 'UC-04.VIEW_POLICY_AND_TERMS' AS permission_code, 'R' AS permission_level
  UNION ALL SELECT 'STAFF' AS role_code, 'STAFF' AS sub_role, 'UC-04.VIEW_POLICY_AND_TERMS' AS permission_code, 'R' AS permission_level
  UNION ALL SELECT 'DEPARTMENT' AS role_code, 'LEADER' AS sub_role, 'UC-04.VIEW_POLICY_AND_TERMS' AS permission_code, 'R' AS permission_level
  UNION ALL SELECT 'DEPARTMENT' AS role_code, 'STAFF' AS sub_role, 'UC-04.VIEW_POLICY_AND_TERMS' AS permission_code, 'R' AS permission_level
  UNION ALL SELECT 'STUDENT' AS role_code, 'NONE' AS sub_role, 'UC-04.VIEW_POLICY_AND_TERMS' AS permission_code, 'R' AS permission_level
  UNION ALL SELECT 'VISITOR' AS role_code, 'NONE' AS sub_role, 'UC-04.VIEW_POLICY_AND_TERMS' AS permission_code, 'R' AS permission_level
  UNION ALL SELECT 'HO' AS role_code, 'NONE' AS sub_role, 'UC-05.VIEW_FAQ' AS permission_code, 'R' AS permission_level
  UNION ALL SELECT 'ADMIN' AS role_code, 'NONE' AS sub_role, 'UC-05.VIEW_FAQ' AS permission_code, 'R' AS permission_level
  UNION ALL SELECT 'STAFF' AS role_code, 'LEADER' AS sub_role, 'UC-05.VIEW_FAQ' AS permission_code, 'R' AS permission_level
  UNION ALL SELECT 'STAFF' AS role_code, 'STAFF' AS sub_role, 'UC-05.VIEW_FAQ' AS permission_code, 'R' AS permission_level
  UNION ALL SELECT 'DEPARTMENT' AS role_code, 'LEADER' AS sub_role, 'UC-05.VIEW_FAQ' AS permission_code, 'R' AS permission_level
  UNION ALL SELECT 'DEPARTMENT' AS role_code, 'STAFF' AS sub_role, 'UC-05.VIEW_FAQ' AS permission_code, 'R' AS permission_level
  UNION ALL SELECT 'STUDENT' AS role_code, 'NONE' AS sub_role, 'UC-05.VIEW_FAQ' AS permission_code, 'R' AS permission_level
  UNION ALL SELECT 'VISITOR' AS role_code, 'NONE' AS sub_role, 'UC-05.VIEW_FAQ' AS permission_code, 'R' AS permission_level
  UNION ALL SELECT 'HO' AS role_code, 'NONE' AS sub_role, 'UC-06.VIEW_NEWS' AS permission_code, 'R' AS permission_level
  UNION ALL SELECT 'ADMIN' AS role_code, 'NONE' AS sub_role, 'UC-06.VIEW_NEWS' AS permission_code, 'R' AS permission_level
  UNION ALL SELECT 'STAFF' AS role_code, 'LEADER' AS sub_role, 'UC-06.VIEW_NEWS' AS permission_code, 'R' AS permission_level
  UNION ALL SELECT 'STAFF' AS role_code, 'STAFF' AS sub_role, 'UC-06.VIEW_NEWS' AS permission_code, 'R' AS permission_level
  UNION ALL SELECT 'DEPARTMENT' AS role_code, 'LEADER' AS sub_role, 'UC-06.VIEW_NEWS' AS permission_code, 'R' AS permission_level
  UNION ALL SELECT 'DEPARTMENT' AS role_code, 'STAFF' AS sub_role, 'UC-06.VIEW_NEWS' AS permission_code, 'R' AS permission_level
  UNION ALL SELECT 'STUDENT' AS role_code, 'NONE' AS sub_role, 'UC-06.VIEW_NEWS' AS permission_code, 'R' AS permission_level
  UNION ALL SELECT 'VISITOR' AS role_code, 'NONE' AS sub_role, 'UC-06.VIEW_NEWS' AS permission_code, 'R' AS permission_level
  UNION ALL SELECT 'HO' AS role_code, 'NONE' AS sub_role, 'UC-07.VIEW_PARTNERS' AS permission_code, 'R' AS permission_level
  UNION ALL SELECT 'ADMIN' AS role_code, 'NONE' AS sub_role, 'UC-07.VIEW_PARTNERS' AS permission_code, 'R' AS permission_level
  UNION ALL SELECT 'STAFF' AS role_code, 'LEADER' AS sub_role, 'UC-07.VIEW_PARTNERS' AS permission_code, 'R' AS permission_level
  UNION ALL SELECT 'STAFF' AS role_code, 'STAFF' AS sub_role, 'UC-07.VIEW_PARTNERS' AS permission_code, 'R' AS permission_level
  UNION ALL SELECT 'DEPARTMENT' AS role_code, 'LEADER' AS sub_role, 'UC-07.VIEW_PARTNERS' AS permission_code, 'R' AS permission_level
  UNION ALL SELECT 'DEPARTMENT' AS role_code, 'STAFF' AS sub_role, 'UC-07.VIEW_PARTNERS' AS permission_code, 'R' AS permission_level
  UNION ALL SELECT 'STUDENT' AS role_code, 'NONE' AS sub_role, 'UC-07.VIEW_PARTNERS' AS permission_code, 'R' AS permission_level
  UNION ALL SELECT 'VISITOR' AS role_code, 'NONE' AS sub_role, 'UC-07.VIEW_PARTNERS' AS permission_code, 'R' AS permission_level
  UNION ALL SELECT 'HO' AS role_code, 'NONE' AS sub_role, 'UC-08.VIEW_GALLERY' AS permission_code, 'R' AS permission_level
  UNION ALL SELECT 'ADMIN' AS role_code, 'NONE' AS sub_role, 'UC-08.VIEW_GALLERY' AS permission_code, 'R' AS permission_level
  UNION ALL SELECT 'STAFF' AS role_code, 'LEADER' AS sub_role, 'UC-08.VIEW_GALLERY' AS permission_code, 'R' AS permission_level
  UNION ALL SELECT 'STAFF' AS role_code, 'STAFF' AS sub_role, 'UC-08.VIEW_GALLERY' AS permission_code, 'R' AS permission_level
  UNION ALL SELECT 'DEPARTMENT' AS role_code, 'LEADER' AS sub_role, 'UC-08.VIEW_GALLERY' AS permission_code, 'R' AS permission_level
  UNION ALL SELECT 'DEPARTMENT' AS role_code, 'STAFF' AS sub_role, 'UC-08.VIEW_GALLERY' AS permission_code, 'R' AS permission_level
  UNION ALL SELECT 'STUDENT' AS role_code, 'NONE' AS sub_role, 'UC-08.VIEW_GALLERY' AS permission_code, 'R' AS permission_level
  UNION ALL SELECT 'VISITOR' AS role_code, 'NONE' AS sub_role, 'UC-08.VIEW_GALLERY' AS permission_code, 'R' AS permission_level
  UNION ALL SELECT 'HO' AS role_code, 'NONE' AS sub_role, 'UC-09.VIEW_NOTIFICATIONS' AS permission_code, 'R' AS permission_level
  UNION ALL SELECT 'ADMIN' AS role_code, 'NONE' AS sub_role, 'UC-09.VIEW_NOTIFICATIONS' AS permission_code, 'R' AS permission_level
  UNION ALL SELECT 'STAFF' AS role_code, 'LEADER' AS sub_role, 'UC-09.VIEW_NOTIFICATIONS' AS permission_code, 'R' AS permission_level
  UNION ALL SELECT 'STAFF' AS role_code, 'STAFF' AS sub_role, 'UC-09.VIEW_NOTIFICATIONS' AS permission_code, 'R' AS permission_level
  UNION ALL SELECT 'DEPARTMENT' AS role_code, 'LEADER' AS sub_role, 'UC-09.VIEW_NOTIFICATIONS' AS permission_code, 'R' AS permission_level
  UNION ALL SELECT 'DEPARTMENT' AS role_code, 'STAFF' AS sub_role, 'UC-09.VIEW_NOTIFICATIONS' AS permission_code, 'R' AS permission_level
  UNION ALL SELECT 'STUDENT' AS role_code, 'NONE' AS sub_role, 'UC-09.VIEW_NOTIFICATIONS' AS permission_code, 'R' AS permission_level
  UNION ALL SELECT 'VISITOR' AS role_code, 'NONE' AS sub_role, 'UC-09.VIEW_NOTIFICATIONS' AS permission_code, 'R' AS permission_level
  UNION ALL SELECT 'HO' AS role_code, 'NONE' AS sub_role, 'UC-10.LOGIN_VIA_SSO' AS permission_code, 'O' AS permission_level
  UNION ALL SELECT 'ADMIN' AS role_code, 'NONE' AS sub_role, 'UC-10.LOGIN_VIA_SSO' AS permission_code, 'O' AS permission_level
  UNION ALL SELECT 'STAFF' AS role_code, 'LEADER' AS sub_role, 'UC-10.LOGIN_VIA_SSO' AS permission_code, 'O' AS permission_level
  UNION ALL SELECT 'STAFF' AS role_code, 'STAFF' AS sub_role, 'UC-10.LOGIN_VIA_SSO' AS permission_code, 'O' AS permission_level
  UNION ALL SELECT 'DEPARTMENT' AS role_code, 'LEADER' AS sub_role, 'UC-10.LOGIN_VIA_SSO' AS permission_code, 'O' AS permission_level
  UNION ALL SELECT 'DEPARTMENT' AS role_code, 'STAFF' AS sub_role, 'UC-10.LOGIN_VIA_SSO' AS permission_code, 'O' AS permission_level
  UNION ALL SELECT 'STUDENT' AS role_code, 'NONE' AS sub_role, 'UC-10.LOGIN_VIA_SSO' AS permission_code, 'O' AS permission_level
  UNION ALL SELECT 'VISITOR' AS role_code, 'NONE' AS sub_role, 'UC-10.LOGIN_VIA_SSO' AS permission_code, 'O' AS permission_level
  UNION ALL SELECT 'HO' AS role_code, 'NONE' AS sub_role, 'UC-11.LOGIN_VIA_CREDENTIALS' AS permission_code, 'O' AS permission_level
  UNION ALL SELECT 'ADMIN' AS role_code, 'NONE' AS sub_role, 'UC-11.LOGIN_VIA_CREDENTIALS' AS permission_code, 'O' AS permission_level
  UNION ALL SELECT 'STAFF' AS role_code, 'LEADER' AS sub_role, 'UC-11.LOGIN_VIA_CREDENTIALS' AS permission_code, 'O' AS permission_level
  UNION ALL SELECT 'STAFF' AS role_code, 'STAFF' AS sub_role, 'UC-11.LOGIN_VIA_CREDENTIALS' AS permission_code, 'O' AS permission_level
  UNION ALL SELECT 'DEPARTMENT' AS role_code, 'LEADER' AS sub_role, 'UC-11.LOGIN_VIA_CREDENTIALS' AS permission_code, 'O' AS permission_level
  UNION ALL SELECT 'DEPARTMENT' AS role_code, 'STAFF' AS sub_role, 'UC-11.LOGIN_VIA_CREDENTIALS' AS permission_code, 'O' AS permission_level
  UNION ALL SELECT 'STUDENT' AS role_code, 'NONE' AS sub_role, 'UC-11.LOGIN_VIA_CREDENTIALS' AS permission_code, 'O' AS permission_level
  UNION ALL SELECT 'VISITOR' AS role_code, 'NONE' AS sub_role, 'UC-11.LOGIN_VIA_CREDENTIALS' AS permission_code, 'O' AS permission_level
  UNION ALL SELECT 'HO' AS role_code, 'NONE' AS sub_role, 'UC-12.LOGOUT' AS permission_code, 'O' AS permission_level
  UNION ALL SELECT 'ADMIN' AS role_code, 'NONE' AS sub_role, 'UC-12.LOGOUT' AS permission_code, 'O' AS permission_level
  UNION ALL SELECT 'STAFF' AS role_code, 'LEADER' AS sub_role, 'UC-12.LOGOUT' AS permission_code, 'O' AS permission_level
  UNION ALL SELECT 'STAFF' AS role_code, 'STAFF' AS sub_role, 'UC-12.LOGOUT' AS permission_code, 'O' AS permission_level
  UNION ALL SELECT 'DEPARTMENT' AS role_code, 'LEADER' AS sub_role, 'UC-12.LOGOUT' AS permission_code, 'O' AS permission_level
  UNION ALL SELECT 'DEPARTMENT' AS role_code, 'STAFF' AS sub_role, 'UC-12.LOGOUT' AS permission_code, 'O' AS permission_level
  UNION ALL SELECT 'STUDENT' AS role_code, 'NONE' AS sub_role, 'UC-12.LOGOUT' AS permission_code, 'O' AS permission_level
  UNION ALL SELECT 'VISITOR' AS role_code, 'NONE' AS sub_role, 'UC-12.LOGOUT' AS permission_code, 'O' AS permission_level
  UNION ALL SELECT 'HO' AS role_code, 'NONE' AS sub_role, 'UC-13.FORGOT_PASSWORD' AS permission_code, 'O' AS permission_level
  UNION ALL SELECT 'ADMIN' AS role_code, 'NONE' AS sub_role, 'UC-13.FORGOT_PASSWORD' AS permission_code, 'O' AS permission_level
  UNION ALL SELECT 'STAFF' AS role_code, 'LEADER' AS sub_role, 'UC-13.FORGOT_PASSWORD' AS permission_code, 'O' AS permission_level
  UNION ALL SELECT 'STAFF' AS role_code, 'STAFF' AS sub_role, 'UC-13.FORGOT_PASSWORD' AS permission_code, 'O' AS permission_level
  UNION ALL SELECT 'DEPARTMENT' AS role_code, 'LEADER' AS sub_role, 'UC-13.FORGOT_PASSWORD' AS permission_code, 'O' AS permission_level
  UNION ALL SELECT 'DEPARTMENT' AS role_code, 'STAFF' AS sub_role, 'UC-13.FORGOT_PASSWORD' AS permission_code, 'O' AS permission_level
  UNION ALL SELECT 'STUDENT' AS role_code, 'NONE' AS sub_role, 'UC-13.FORGOT_PASSWORD' AS permission_code, 'O' AS permission_level
  UNION ALL SELECT 'VISITOR' AS role_code, 'NONE' AS sub_role, 'UC-13.FORGOT_PASSWORD' AS permission_code, 'O' AS permission_level
  UNION ALL SELECT 'HO' AS role_code, 'NONE' AS sub_role, 'UC-14.VIEW_PROFILE' AS permission_code, 'O' AS permission_level
  UNION ALL SELECT 'ADMIN' AS role_code, 'NONE' AS sub_role, 'UC-14.VIEW_PROFILE' AS permission_code, 'O' AS permission_level
  UNION ALL SELECT 'STAFF' AS role_code, 'LEADER' AS sub_role, 'UC-14.VIEW_PROFILE' AS permission_code, 'O' AS permission_level
  UNION ALL SELECT 'STAFF' AS role_code, 'STAFF' AS sub_role, 'UC-14.VIEW_PROFILE' AS permission_code, 'O' AS permission_level
  UNION ALL SELECT 'DEPARTMENT' AS role_code, 'LEADER' AS sub_role, 'UC-14.VIEW_PROFILE' AS permission_code, 'O' AS permission_level
  UNION ALL SELECT 'DEPARTMENT' AS role_code, 'STAFF' AS sub_role, 'UC-14.VIEW_PROFILE' AS permission_code, 'O' AS permission_level
  UNION ALL SELECT 'STUDENT' AS role_code, 'NONE' AS sub_role, 'UC-14.VIEW_PROFILE' AS permission_code, 'O' AS permission_level
  UNION ALL SELECT 'VISITOR' AS role_code, 'NONE' AS sub_role, 'UC-14.VIEW_PROFILE' AS permission_code, 'O' AS permission_level
  UNION ALL SELECT 'HO' AS role_code, 'NONE' AS sub_role, 'UC-15.UPDATE_PROFILE' AS permission_code, 'O' AS permission_level
  UNION ALL SELECT 'ADMIN' AS role_code, 'NONE' AS sub_role, 'UC-15.UPDATE_PROFILE' AS permission_code, 'O' AS permission_level
  UNION ALL SELECT 'STAFF' AS role_code, 'LEADER' AS sub_role, 'UC-15.UPDATE_PROFILE' AS permission_code, 'O' AS permission_level
  UNION ALL SELECT 'STAFF' AS role_code, 'STAFF' AS sub_role, 'UC-15.UPDATE_PROFILE' AS permission_code, 'O' AS permission_level
  UNION ALL SELECT 'DEPARTMENT' AS role_code, 'LEADER' AS sub_role, 'UC-15.UPDATE_PROFILE' AS permission_code, 'O' AS permission_level
  UNION ALL SELECT 'DEPARTMENT' AS role_code, 'STAFF' AS sub_role, 'UC-15.UPDATE_PROFILE' AS permission_code, 'O' AS permission_level
  UNION ALL SELECT 'STUDENT' AS role_code, 'NONE' AS sub_role, 'UC-15.UPDATE_PROFILE' AS permission_code, 'O' AS permission_level
  UNION ALL SELECT 'VISITOR' AS role_code, 'NONE' AS sub_role, 'UC-15.UPDATE_PROFILE' AS permission_code, 'O' AS permission_level
  UNION ALL SELECT 'HO' AS role_code, 'NONE' AS sub_role, 'UC-16.CHANGE_PASSWORD' AS permission_code, 'O' AS permission_level
  UNION ALL SELECT 'ADMIN' AS role_code, 'NONE' AS sub_role, 'UC-16.CHANGE_PASSWORD' AS permission_code, 'O' AS permission_level
  UNION ALL SELECT 'STAFF' AS role_code, 'LEADER' AS sub_role, 'UC-16.CHANGE_PASSWORD' AS permission_code, 'O' AS permission_level
  UNION ALL SELECT 'STAFF' AS role_code, 'STAFF' AS sub_role, 'UC-16.CHANGE_PASSWORD' AS permission_code, 'O' AS permission_level
  UNION ALL SELECT 'DEPARTMENT' AS role_code, 'LEADER' AS sub_role, 'UC-16.CHANGE_PASSWORD' AS permission_code, 'O' AS permission_level
  UNION ALL SELECT 'DEPARTMENT' AS role_code, 'STAFF' AS sub_role, 'UC-16.CHANGE_PASSWORD' AS permission_code, 'O' AS permission_level
  UNION ALL SELECT 'STUDENT' AS role_code, 'NONE' AS sub_role, 'UC-16.CHANGE_PASSWORD' AS permission_code, 'O' AS permission_level
  UNION ALL SELECT 'VISITOR' AS role_code, 'NONE' AS sub_role, 'UC-16.CHANGE_PASSWORD' AS permission_code, 'O' AS permission_level
  UNION ALL SELECT 'VISITOR' AS role_code, 'NONE' AS sub_role, 'UC-17.SUBMIT_VISIT_REQUEST' AS permission_code, 'F' AS permission_level
  UNION ALL SELECT 'HO' AS role_code, 'NONE' AS sub_role, 'UC-18.APPROVE_CROSS_CAMPUS_REQUEST' AS permission_code, 'E' AS permission_level
  UNION ALL SELECT 'HO' AS role_code, 'NONE' AS sub_role, 'UC-19.VIEW_GUEST_DELEGATION_DETAILS' AS permission_code, 'R' AS permission_level
  UNION ALL SELECT 'STAFF' AS role_code, 'LEADER' AS sub_role, 'UC-19.VIEW_GUEST_DELEGATION_DETAILS' AS permission_code, 'R' AS permission_level
  UNION ALL SELECT 'STAFF' AS role_code, 'STAFF' AS sub_role, 'UC-19.VIEW_GUEST_DELEGATION_DETAILS' AS permission_code, 'R' AS permission_level
  UNION ALL SELECT 'DEPARTMENT' AS role_code, 'LEADER' AS sub_role, 'UC-19.VIEW_GUEST_DELEGATION_DETAILS' AS permission_code, 'R' AS permission_level
  UNION ALL SELECT 'DEPARTMENT' AS role_code, 'STAFF' AS sub_role, 'UC-19.VIEW_GUEST_DELEGATION_DETAILS' AS permission_code, 'R' AS permission_level
  UNION ALL SELECT 'STUDENT' AS role_code, 'NONE' AS sub_role, 'UC-19.VIEW_GUEST_DELEGATION_DETAILS' AS permission_code, 'R' AS permission_level
  UNION ALL SELECT 'VISITOR' AS role_code, 'NONE' AS sub_role, 'UC-19.VIEW_GUEST_DELEGATION_DETAILS' AS permission_code, 'R' AS permission_level
  UNION ALL SELECT 'HO' AS role_code, 'NONE' AS sub_role, 'UC-20.VIEW_GUEST_DELEGATION_LIST' AS permission_code, 'R' AS permission_level
  UNION ALL SELECT 'STAFF' AS role_code, 'LEADER' AS sub_role, 'UC-20.VIEW_GUEST_DELEGATION_LIST' AS permission_code, 'R' AS permission_level
  UNION ALL SELECT 'STAFF' AS role_code, 'STAFF' AS sub_role, 'UC-20.VIEW_GUEST_DELEGATION_LIST' AS permission_code, 'R' AS permission_level
  UNION ALL SELECT 'DEPARTMENT' AS role_code, 'LEADER' AS sub_role, 'UC-20.VIEW_GUEST_DELEGATION_LIST' AS permission_code, 'R' AS permission_level
  UNION ALL SELECT 'DEPARTMENT' AS role_code, 'STAFF' AS sub_role, 'UC-20.VIEW_GUEST_DELEGATION_LIST' AS permission_code, 'R' AS permission_level
  UNION ALL SELECT 'STUDENT' AS role_code, 'NONE' AS sub_role, 'UC-20.VIEW_GUEST_DELEGATION_LIST' AS permission_code, 'R' AS permission_level
  UNION ALL SELECT 'VISITOR' AS role_code, 'NONE' AS sub_role, 'UC-20.VIEW_GUEST_DELEGATION_LIST' AS permission_code, 'R' AS permission_level
  UNION ALL SELECT 'HO' AS role_code, 'NONE' AS sub_role, 'UC-21.SEARCH_DELEGATIONS' AS permission_code, 'R' AS permission_level
  UNION ALL SELECT 'STAFF' AS role_code, 'LEADER' AS sub_role, 'UC-21.SEARCH_DELEGATIONS' AS permission_code, 'R' AS permission_level
  UNION ALL SELECT 'STAFF' AS role_code, 'STAFF' AS sub_role, 'UC-21.SEARCH_DELEGATIONS' AS permission_code, 'R' AS permission_level
  UNION ALL SELECT 'DEPARTMENT' AS role_code, 'LEADER' AS sub_role, 'UC-21.SEARCH_DELEGATIONS' AS permission_code, 'R' AS permission_level
  UNION ALL SELECT 'DEPARTMENT' AS role_code, 'STAFF' AS sub_role, 'UC-21.SEARCH_DELEGATIONS' AS permission_code, 'R' AS permission_level
  UNION ALL SELECT 'STUDENT' AS role_code, 'NONE' AS sub_role, 'UC-21.SEARCH_DELEGATIONS' AS permission_code, 'R' AS permission_level
  UNION ALL SELECT 'VISITOR' AS role_code, 'NONE' AS sub_role, 'UC-21.SEARCH_DELEGATIONS' AS permission_code, 'R' AS permission_level
  UNION ALL SELECT 'STAFF' AS role_code, 'LEADER' AS sub_role, 'UC-22.PROCESS_VISIT_REQUEST' AS permission_code, 'E' AS permission_level
  UNION ALL SELECT 'STAFF' AS role_code, 'STAFF' AS sub_role, 'UC-23.CREATE_GUEST_DELEGATION' AS permission_code, 'F' AS permission_level
  UNION ALL SELECT 'STAFF' AS role_code, 'STAFF' AS sub_role, 'UC-24.UPDATE_GUEST_DELEGATION' AS permission_code, 'F' AS permission_level
  UNION ALL SELECT 'STAFF' AS role_code, 'LEADER' AS sub_role, 'UC-25.PREPARE_VISIT_LOGISTICS' AS permission_code, 'R' AS permission_level
  UNION ALL SELECT 'STAFF' AS role_code, 'STAFF' AS sub_role, 'UC-25.PREPARE_VISIT_LOGISTICS' AS permission_code, 'F' AS permission_level
  UNION ALL SELECT 'STAFF' AS role_code, 'LEADER' AS sub_role, 'UC-26.UPDATE_VISIT_LOGISTICS' AS permission_code, 'R' AS permission_level
  UNION ALL SELECT 'STAFF' AS role_code, 'STAFF' AS sub_role, 'UC-26.UPDATE_VISIT_LOGISTICS' AS permission_code, 'F' AS permission_level
  UNION ALL SELECT 'STAFF' AS role_code, 'STAFF' AS sub_role, 'UC-27.CONFIRM_PARTICIPATION' AS permission_code, 'E' AS permission_level
  UNION ALL SELECT 'DEPARTMENT' AS role_code, 'LEADER' AS sub_role, 'UC-27.CONFIRM_PARTICIPATION' AS permission_code, 'E' AS permission_level
  UNION ALL SELECT 'DEPARTMENT' AS role_code, 'STAFF' AS sub_role, 'UC-27.CONFIRM_PARTICIPATION' AS permission_code, 'E' AS permission_level
  UNION ALL SELECT 'STUDENT' AS role_code, 'NONE' AS sub_role, 'UC-27.CONFIRM_PARTICIPATION' AS permission_code, 'E' AS permission_level
  UNION ALL SELECT 'DEPARTMENT' AS role_code, 'LEADER' AS sub_role, 'UC-28.APPROVE_RESOURCE_REQUEST' AS permission_code, 'F' AS permission_level
  UNION ALL SELECT 'DEPARTMENT' AS role_code, 'LEADER' AS sub_role, 'UC-29.PROPOSE_RESOURCE_MODIFICATION' AS permission_code, 'F' AS permission_level
  UNION ALL SELECT 'DEPARTMENT' AS role_code, 'STAFF' AS sub_role, 'UC-29.PROPOSE_RESOURCE_MODIFICATION' AS permission_code, 'F' AS permission_level
  UNION ALL SELECT 'STAFF' AS role_code, 'STAFF' AS sub_role, 'UC-30.CONFIRM_THE_CHANGE_PROPOSAL' AS permission_code, 'E' AS permission_level
  UNION ALL SELECT 'DEPARTMENT' AS role_code, 'LEADER' AS sub_role, 'UC-30.CONFIRM_THE_CHANGE_PROPOSAL' AS permission_code, 'R' AS permission_level
  UNION ALL SELECT 'DEPARTMENT' AS role_code, 'STAFF' AS sub_role, 'UC-30.CONFIRM_THE_CHANGE_PROPOSAL' AS permission_code, 'R' AS permission_level
  UNION ALL SELECT 'STAFF' AS role_code, 'STAFF' AS sub_role, 'UC-31.CREATE_MEETING_MINUTES' AS permission_code, 'F' AS permission_level
  UNION ALL SELECT 'DEPARTMENT' AS role_code, 'LEADER' AS sub_role, 'UC-31.CREATE_MEETING_MINUTES' AS permission_code, 'F' AS permission_level
  UNION ALL SELECT 'DEPARTMENT' AS role_code, 'STAFF' AS sub_role, 'UC-31.CREATE_MEETING_MINUTES' AS permission_code, 'F' AS permission_level
  UNION ALL SELECT 'STUDENT' AS role_code, 'NONE' AS sub_role, 'UC-31.CREATE_MEETING_MINUTES' AS permission_code, 'F' AS permission_level
  UNION ALL SELECT 'STAFF' AS role_code, 'STAFF' AS sub_role, 'UC-32.EDIT_MEETING_MINUTES' AS permission_code, 'F' AS permission_level
  UNION ALL SELECT 'DEPARTMENT' AS role_code, 'LEADER' AS sub_role, 'UC-32.EDIT_MEETING_MINUTES' AS permission_code, 'F' AS permission_level
  UNION ALL SELECT 'DEPARTMENT' AS role_code, 'STAFF' AS sub_role, 'UC-32.EDIT_MEETING_MINUTES' AS permission_code, 'F' AS permission_level
  UNION ALL SELECT 'STUDENT' AS role_code, 'NONE' AS sub_role, 'UC-32.EDIT_MEETING_MINUTES' AS permission_code, 'F' AS permission_level
  UNION ALL SELECT 'HO' AS role_code, 'NONE' AS sub_role, 'UC-33.VIEW_MEETING_MINUTES_DETAILS' AS permission_code, 'R' AS permission_level
  UNION ALL SELECT 'STAFF' AS role_code, 'LEADER' AS sub_role, 'UC-33.VIEW_MEETING_MINUTES_DETAILS' AS permission_code, 'R' AS permission_level
  UNION ALL SELECT 'STAFF' AS role_code, 'STAFF' AS sub_role, 'UC-33.VIEW_MEETING_MINUTES_DETAILS' AS permission_code, 'R' AS permission_level
  UNION ALL SELECT 'DEPARTMENT' AS role_code, 'LEADER' AS sub_role, 'UC-33.VIEW_MEETING_MINUTES_DETAILS' AS permission_code, 'R' AS permission_level
  UNION ALL SELECT 'DEPARTMENT' AS role_code, 'STAFF' AS sub_role, 'UC-33.VIEW_MEETING_MINUTES_DETAILS' AS permission_code, 'R' AS permission_level
  UNION ALL SELECT 'STUDENT' AS role_code, 'NONE' AS sub_role, 'UC-33.VIEW_MEETING_MINUTES_DETAILS' AS permission_code, 'R' AS permission_level
  UNION ALL SELECT 'STAFF' AS role_code, 'STAFF' AS sub_role, 'UC-34.SUBMIT_DELEGATION_FEEDBACK' AS permission_code, 'F' AS permission_level
  UNION ALL SELECT 'DEPARTMENT' AS role_code, 'LEADER' AS sub_role, 'UC-34.SUBMIT_DELEGATION_FEEDBACK' AS permission_code, 'F' AS permission_level
  UNION ALL SELECT 'DEPARTMENT' AS role_code, 'STAFF' AS sub_role, 'UC-34.SUBMIT_DELEGATION_FEEDBACK' AS permission_code, 'F' AS permission_level
  UNION ALL SELECT 'STUDENT' AS role_code, 'NONE' AS sub_role, 'UC-34.SUBMIT_DELEGATION_FEEDBACK' AS permission_code, 'F' AS permission_level
  UNION ALL SELECT 'STAFF' AS role_code, 'STAFF' AS sub_role, 'UC-35.SCAN_BUSINESS_CARD' AS permission_code, 'F' AS permission_level
  UNION ALL SELECT 'STAFF' AS role_code, 'STAFF' AS sub_role, 'UC-36.CREATE_PARTNER_PROFILE' AS permission_code, 'F' AS permission_level
  UNION ALL SELECT 'STAFF' AS role_code, 'STAFF' AS sub_role, 'UC-37.UPLOAD_ATTACHED_DOCUMENTS' AS permission_code, 'F' AS permission_level
  UNION ALL SELECT 'STAFF' AS role_code, 'STAFF' AS sub_role, 'UC-38.UPLOAD_VISIT_PHOTOS' AS permission_code, 'F' AS permission_level
  UNION ALL SELECT 'STUDENT' AS role_code, 'NONE' AS sub_role, 'UC-38.UPLOAD_VISIT_PHOTOS' AS permission_code, 'F' AS permission_level
  UNION ALL SELECT 'STAFF' AS role_code, 'STAFF' AS sub_role, 'UC-39.TAG_FACES_ON_PHOTOS' AS permission_code, 'F' AS permission_level
  UNION ALL SELECT 'STAFF' AS role_code, 'STAFF' AS sub_role, 'UC-40.CREATE_NEWS_ARTICLE' AS permission_code, 'F' AS permission_level
  UNION ALL SELECT 'STUDENT' AS role_code, 'NONE' AS sub_role, 'UC-40.CREATE_NEWS_ARTICLE' AS permission_code, 'F' AS permission_level
  UNION ALL SELECT 'STAFF' AS role_code, 'STAFF' AS sub_role, 'UC-41.CLOSE_DELEGATION' AS permission_code, 'F' AS permission_level
  UNION ALL SELECT 'HO' AS role_code, 'NONE' AS sub_role, 'UC-42.VIEW_EMAIL_TEMPLATE_LIST' AS permission_code, 'R' AS permission_level
  UNION ALL SELECT 'HO' AS role_code, 'NONE' AS sub_role, 'UC-43.VIEW_EMAIL_TEMPLATE_DETAIL' AS permission_code, 'R' AS permission_level
  UNION ALL SELECT 'HO' AS role_code, 'NONE' AS sub_role, 'UC-44.UPDATE_EMAIL_TEMPLATE' AS permission_code, 'E' AS permission_level
  UNION ALL SELECT 'HO' AS role_code, 'NONE' AS sub_role, 'UC-45.CREATE_EMAIL_TEMPLATE' AS permission_code, 'F' AS permission_level
  UNION ALL SELECT 'HO' AS role_code, 'NONE' AS sub_role, 'UC-46.EDIT_EMAIL_CONTENT' AS permission_code, 'O' AS permission_level
  UNION ALL SELECT 'STAFF' AS role_code, 'LEADER' AS sub_role, 'UC-46.EDIT_EMAIL_CONTENT' AS permission_code, 'O' AS permission_level
  UNION ALL SELECT 'STAFF' AS role_code, 'STAFF' AS sub_role, 'UC-46.EDIT_EMAIL_CONTENT' AS permission_code, 'O' AS permission_level
  UNION ALL SELECT 'DEPARTMENT' AS role_code, 'LEADER' AS sub_role, 'UC-46.EDIT_EMAIL_CONTENT' AS permission_code, 'O' AS permission_level
  UNION ALL SELECT 'DEPARTMENT' AS role_code, 'STAFF' AS sub_role, 'UC-46.EDIT_EMAIL_CONTENT' AS permission_code, 'O' AS permission_level
  UNION ALL SELECT 'STUDENT' AS role_code, 'NONE' AS sub_role, 'UC-46.EDIT_EMAIL_CONTENT' AS permission_code, 'O' AS permission_level
  UNION ALL SELECT 'VISITOR' AS role_code, 'NONE' AS sub_role, 'UC-46.EDIT_EMAIL_CONTENT' AS permission_code, 'O' AS permission_level
  UNION ALL SELECT 'HO' AS role_code, 'NONE' AS sub_role, 'UC-47.SEND_EMAIL' AS permission_code, 'O' AS permission_level
  UNION ALL SELECT 'STAFF' AS role_code, 'LEADER' AS sub_role, 'UC-47.SEND_EMAIL' AS permission_code, 'O' AS permission_level
  UNION ALL SELECT 'STAFF' AS role_code, 'STAFF' AS sub_role, 'UC-47.SEND_EMAIL' AS permission_code, 'O' AS permission_level
  UNION ALL SELECT 'DEPARTMENT' AS role_code, 'LEADER' AS sub_role, 'UC-47.SEND_EMAIL' AS permission_code, 'O' AS permission_level
  UNION ALL SELECT 'DEPARTMENT' AS role_code, 'STAFF' AS sub_role, 'UC-47.SEND_EMAIL' AS permission_code, 'O' AS permission_level
  UNION ALL SELECT 'STUDENT' AS role_code, 'NONE' AS sub_role, 'UC-47.SEND_EMAIL' AS permission_code, 'O' AS permission_level
  UNION ALL SELECT 'VISITOR' AS role_code, 'NONE' AS sub_role, 'UC-47.SEND_EMAIL' AS permission_code, 'O' AS permission_level
  UNION ALL SELECT 'HO' AS role_code, 'NONE' AS sub_role, 'UC-48.VIEW_EMAIL' AS permission_code, 'O' AS permission_level
  UNION ALL SELECT 'STAFF' AS role_code, 'LEADER' AS sub_role, 'UC-48.VIEW_EMAIL' AS permission_code, 'O' AS permission_level
  UNION ALL SELECT 'STAFF' AS role_code, 'STAFF' AS sub_role, 'UC-48.VIEW_EMAIL' AS permission_code, 'O' AS permission_level
  UNION ALL SELECT 'DEPARTMENT' AS role_code, 'LEADER' AS sub_role, 'UC-48.VIEW_EMAIL' AS permission_code, 'O' AS permission_level
  UNION ALL SELECT 'DEPARTMENT' AS role_code, 'STAFF' AS sub_role, 'UC-48.VIEW_EMAIL' AS permission_code, 'O' AS permission_level
  UNION ALL SELECT 'STUDENT' AS role_code, 'NONE' AS sub_role, 'UC-48.VIEW_EMAIL' AS permission_code, 'O' AS permission_level
  UNION ALL SELECT 'VISITOR' AS role_code, 'NONE' AS sub_role, 'UC-48.VIEW_EMAIL' AS permission_code, 'O' AS permission_level
  UNION ALL SELECT 'HO' AS role_code, 'NONE' AS sub_role, 'UC-49.REPLY_TO_EMAIL' AS permission_code, 'O' AS permission_level
  UNION ALL SELECT 'STAFF' AS role_code, 'LEADER' AS sub_role, 'UC-49.REPLY_TO_EMAIL' AS permission_code, 'O' AS permission_level
  UNION ALL SELECT 'STAFF' AS role_code, 'STAFF' AS sub_role, 'UC-49.REPLY_TO_EMAIL' AS permission_code, 'O' AS permission_level
  UNION ALL SELECT 'DEPARTMENT' AS role_code, 'LEADER' AS sub_role, 'UC-49.REPLY_TO_EMAIL' AS permission_code, 'O' AS permission_level
  UNION ALL SELECT 'DEPARTMENT' AS role_code, 'STAFF' AS sub_role, 'UC-49.REPLY_TO_EMAIL' AS permission_code, 'O' AS permission_level
  UNION ALL SELECT 'STUDENT' AS role_code, 'NONE' AS sub_role, 'UC-49.REPLY_TO_EMAIL' AS permission_code, 'O' AS permission_level
  UNION ALL SELECT 'VISITOR' AS role_code, 'NONE' AS sub_role, 'UC-49.REPLY_TO_EMAIL' AS permission_code, 'O' AS permission_level
  UNION ALL SELECT 'STAFF' AS role_code, 'LEADER' AS sub_role, 'UC-50.PROCESS_PARTNER_CREATION_REQUEST' AS permission_code, 'E' AS permission_level
  UNION ALL SELECT 'STAFF' AS role_code, 'STAFF' AS sub_role, 'UC-51.EDIT_PARTNER_INFORMATION' AS permission_code, 'E' AS permission_level
  UNION ALL SELECT 'STAFF' AS role_code, 'LEADER' AS sub_role, 'UC-52.VIEW_PARTNER_LISTS' AS permission_code, 'R' AS permission_level
  UNION ALL SELECT 'STAFF' AS role_code, 'STAFF' AS sub_role, 'UC-52.VIEW_PARTNER_LISTS' AS permission_code, 'R' AS permission_level
  UNION ALL SELECT 'STAFF' AS role_code, 'LEADER' AS sub_role, 'UC-53.SEARCH_PARTNERS' AS permission_code, 'R' AS permission_level
  UNION ALL SELECT 'STAFF' AS role_code, 'STAFF' AS sub_role, 'UC-53.SEARCH_PARTNERS' AS permission_code, 'R' AS permission_level
  UNION ALL SELECT 'STAFF' AS role_code, 'LEADER' AS sub_role, 'UC-54.VIEW_PARTNER_DETAILS' AS permission_code, 'R' AS permission_level
  UNION ALL SELECT 'STAFF' AS role_code, 'STAFF' AS sub_role, 'UC-54.VIEW_PARTNER_DETAILS' AS permission_code, 'R' AS permission_level
  UNION ALL SELECT 'STAFF' AS role_code, 'LEADER' AS sub_role, 'UC-55.VIEW_DOCUMENT_LIST' AS permission_code, 'R' AS permission_level
  UNION ALL SELECT 'STAFF' AS role_code, 'STAFF' AS sub_role, 'UC-55.VIEW_DOCUMENT_LIST' AS permission_code, 'R' AS permission_level
  UNION ALL SELECT 'STAFF' AS role_code, 'LEADER' AS sub_role, 'UC-56.SEARCH_DOCUMENTS' AS permission_code, 'R' AS permission_level
  UNION ALL SELECT 'STAFF' AS role_code, 'STAFF' AS sub_role, 'UC-56.SEARCH_DOCUMENTS' AS permission_code, 'R' AS permission_level
  UNION ALL SELECT 'STAFF' AS role_code, 'LEADER' AS sub_role, 'UC-57.VIEW_GALLERY_ITEM_LIST' AS permission_code, 'R' AS permission_level
  UNION ALL SELECT 'STAFF' AS role_code, 'LEADER' AS sub_role, 'UC-58.SEARCH_GALLERY_ITEMS' AS permission_code, 'R' AS permission_level
  UNION ALL SELECT 'STAFF' AS role_code, 'LEADER' AS sub_role, 'UC-59.ADD_GALLERY_ITEM' AS permission_code, 'F' AS permission_level
  UNION ALL SELECT 'STAFF' AS role_code, 'LEADER' AS sub_role, 'UC-60.UPDATE_GALLERY_ITEM' AS permission_code, 'E' AS permission_level
  UNION ALL SELECT 'STAFF' AS role_code, 'LEADER' AS sub_role, 'UC-61.DELETE_GALLERY_ITEM' AS permission_code, 'F' AS permission_level
  UNION ALL SELECT 'STAFF' AS role_code, 'LEADER' AS sub_role, 'UC-62.VIEW_MINUTES_LIST' AS permission_code, 'R' AS permission_level
  UNION ALL SELECT 'STAFF' AS role_code, 'STAFF' AS sub_role, 'UC-62.VIEW_MINUTES_LIST' AS permission_code, 'R' AS permission_level
  UNION ALL SELECT 'STAFF' AS role_code, 'LEADER' AS sub_role, 'UC-63.SEARCH_FILTER_MINUTES' AS permission_code, 'R' AS permission_level
  UNION ALL SELECT 'STAFF' AS role_code, 'STAFF' AS sub_role, 'UC-63.SEARCH_FILTER_MINUTES' AS permission_code, 'R' AS permission_level
  UNION ALL SELECT 'HO' AS role_code, 'NONE' AS sub_role, 'UC-64.VIEW_LIST_FAQ' AS permission_code, 'R' AS permission_level
  UNION ALL SELECT 'HO' AS role_code, 'NONE' AS sub_role, 'UC-65.CREATE_FAQ' AS permission_code, 'F' AS permission_level
  UNION ALL SELECT 'HO' AS role_code, 'NONE' AS sub_role, 'UC-66.UPDATE_FAQ' AS permission_code, 'E' AS permission_level
  UNION ALL SELECT 'HO' AS role_code, 'NONE' AS sub_role, 'UC-67.CHANGE_FAQ_VISIBILITY' AS permission_code, 'E' AS permission_level
  UNION ALL SELECT 'HO' AS role_code, 'NONE' AS sub_role, 'UC-68.SEARCH_FAQ' AS permission_code, 'R' AS permission_level
  UNION ALL SELECT 'HO' AS role_code, 'NONE' AS sub_role, 'UC-69.VIEW_DASHBOARD_STATISTICS' AS permission_code, 'R' AS permission_level
  UNION ALL SELECT 'STAFF' AS role_code, 'LEADER' AS sub_role, 'UC-69.VIEW_DASHBOARD_STATISTICS' AS permission_code, 'R' AS permission_level
  UNION ALL SELECT 'DEPARTMENT' AS role_code, 'LEADER' AS sub_role, 'UC-69.VIEW_DASHBOARD_STATISTICS' AS permission_code, 'R' AS permission_level
  UNION ALL SELECT 'HO' AS role_code, 'NONE' AS sub_role, 'UC-70.EXPORT_STATISTICS_REPORT' AS permission_code, 'E' AS permission_level
  UNION ALL SELECT 'STAFF' AS role_code, 'LEADER' AS sub_role, 'UC-70.EXPORT_STATISTICS_REPORT' AS permission_code, 'E' AS permission_level
  UNION ALL SELECT 'DEPARTMENT' AS role_code, 'LEADER' AS sub_role, 'UC-70.EXPORT_STATISTICS_REPORT' AS permission_code, 'E' AS permission_level
  UNION ALL SELECT 'HO' AS role_code, 'NONE' AS sub_role, 'UC-71.FILTER_DASHBOARD_BY_TIME' AS permission_code, 'R' AS permission_level
  UNION ALL SELECT 'STAFF' AS role_code, 'LEADER' AS sub_role, 'UC-71.FILTER_DASHBOARD_BY_TIME' AS permission_code, 'R' AS permission_level
  UNION ALL SELECT 'DEPARTMENT' AS role_code, 'LEADER' AS sub_role, 'UC-71.FILTER_DASHBOARD_BY_TIME' AS permission_code, 'R' AS permission_level
  UNION ALL SELECT 'STAFF' AS role_code, 'LEADER' AS sub_role, 'UC-72.VIEW_MY_EVENTS' AS permission_code, 'O' AS permission_level
  UNION ALL SELECT 'STAFF' AS role_code, 'STAFF' AS sub_role, 'UC-72.VIEW_MY_EVENTS' AS permission_code, 'O' AS permission_level
  UNION ALL SELECT 'DEPARTMENT' AS role_code, 'LEADER' AS sub_role, 'UC-72.VIEW_MY_EVENTS' AS permission_code, 'O' AS permission_level
  UNION ALL SELECT 'DEPARTMENT' AS role_code, 'STAFF' AS sub_role, 'UC-72.VIEW_MY_EVENTS' AS permission_code, 'O' AS permission_level
  UNION ALL SELECT 'STUDENT' AS role_code, 'NONE' AS sub_role, 'UC-72.VIEW_MY_EVENTS' AS permission_code, 'O' AS permission_level
  UNION ALL SELECT 'STAFF' AS role_code, 'LEADER' AS sub_role, 'UC-73.VIEW_DEPARTMENT_CALENDAR' AS permission_code, 'R' AS permission_level
  UNION ALL SELECT 'STAFF' AS role_code, 'STAFF' AS sub_role, 'UC-73.VIEW_DEPARTMENT_CALENDAR' AS permission_code, 'R' AS permission_level
  UNION ALL SELECT 'STAFF' AS role_code, 'LEADER' AS sub_role, 'UC-74.SWITCH_VIEW_MODE' AS permission_code, 'R' AS permission_level
  UNION ALL SELECT 'STAFF' AS role_code, 'STAFF' AS sub_role, 'UC-74.SWITCH_VIEW_MODE' AS permission_code, 'R' AS permission_level
  UNION ALL SELECT 'DEPARTMENT' AS role_code, 'LEADER' AS sub_role, 'UC-74.SWITCH_VIEW_MODE' AS permission_code, 'R' AS permission_level
  UNION ALL SELECT 'DEPARTMENT' AS role_code, 'STAFF' AS sub_role, 'UC-74.SWITCH_VIEW_MODE' AS permission_code, 'R' AS permission_level
  UNION ALL SELECT 'STUDENT' AS role_code, 'NONE' AS sub_role, 'UC-74.SWITCH_VIEW_MODE' AS permission_code, 'R' AS permission_level
  UNION ALL SELECT 'STAFF' AS role_code, 'LEADER' AS sub_role, 'UC-75.ADD_PERSONAL_EVENT' AS permission_code, 'O' AS permission_level
  UNION ALL SELECT 'STAFF' AS role_code, 'STAFF' AS sub_role, 'UC-75.ADD_PERSONAL_EVENT' AS permission_code, 'O' AS permission_level
  UNION ALL SELECT 'STAFF' AS role_code, 'LEADER' AS sub_role, 'UC-76.DELETE_PERSONAL_EVENT' AS permission_code, 'O' AS permission_level
  UNION ALL SELECT 'STAFF' AS role_code, 'STAFF' AS sub_role, 'UC-76.DELETE_PERSONAL_EVENT' AS permission_code, 'O' AS permission_level
  UNION ALL SELECT 'STAFF' AS role_code, 'LEADER' AS sub_role, 'UC-77.UPDATE_PERSONAL_EVENT' AS permission_code, 'O' AS permission_level
  UNION ALL SELECT 'STAFF' AS role_code, 'STAFF' AS sub_role, 'UC-77.UPDATE_PERSONAL_EVENT' AS permission_code, 'O' AS permission_level
  UNION ALL SELECT 'STAFF' AS role_code, 'LEADER' AS sub_role, 'UC-78.VIEW_EVENT_DETAILS' AS permission_code, 'R' AS permission_level
  UNION ALL SELECT 'STAFF' AS role_code, 'STAFF' AS sub_role, 'UC-78.VIEW_EVENT_DETAILS' AS permission_code, 'R' AS permission_level
  UNION ALL SELECT 'DEPARTMENT' AS role_code, 'LEADER' AS sub_role, 'UC-78.VIEW_EVENT_DETAILS' AS permission_code, 'R' AS permission_level
  UNION ALL SELECT 'DEPARTMENT' AS role_code, 'STAFF' AS sub_role, 'UC-78.VIEW_EVENT_DETAILS' AS permission_code, 'R' AS permission_level
  UNION ALL SELECT 'STUDENT' AS role_code, 'NONE' AS sub_role, 'UC-78.VIEW_EVENT_DETAILS' AS permission_code, 'R' AS permission_level
  UNION ALL SELECT 'STAFF' AS role_code, 'LEADER' AS sub_role, 'UC-79.SEARCH_FILTER_FEEDBACK' AS permission_code, 'R' AS permission_level
  UNION ALL SELECT 'STAFF' AS role_code, 'STAFF' AS sub_role, 'UC-79.SEARCH_FILTER_FEEDBACK' AS permission_code, 'R' AS permission_level
  UNION ALL SELECT 'STAFF' AS role_code, 'LEADER' AS sub_role, 'UC-80.VIEW_FEEDBACK_SUMMARY' AS permission_code, 'R' AS permission_level
  UNION ALL SELECT 'STAFF' AS role_code, 'STAFF' AS sub_role, 'UC-80.VIEW_FEEDBACK_SUMMARY' AS permission_code, 'R' AS permission_level
  UNION ALL SELECT 'HO' AS role_code, 'NONE' AS sub_role, 'UC-81.ADD_NEW_CAMPUS' AS permission_code, 'F' AS permission_level
  UNION ALL SELECT 'HO' AS role_code, 'NONE' AS sub_role, 'UC-82.VIEW_CAMPUS_LIST' AS permission_code, 'R' AS permission_level
  UNION ALL SELECT 'HO' AS role_code, 'NONE' AS sub_role, 'UC-83.SEARCH_AND_FILTER_CAMPUS' AS permission_code, 'R' AS permission_level
  UNION ALL SELECT 'HO' AS role_code, 'NONE' AS sub_role, 'UC-84.VIEW_CAMPUS_DETAILS' AS permission_code, 'R' AS permission_level
  UNION ALL SELECT 'HO' AS role_code, 'NONE' AS sub_role, 'UC-85.UPDATE_CAMPUS' AS permission_code, 'E' AS permission_level
  UNION ALL SELECT 'HO' AS role_code, 'NONE' AS sub_role, 'UC-86.MANAGE_CAMPUS_STATUS' AS permission_code, 'E' AS permission_level
  UNION ALL SELECT 'HO' AS role_code, 'NONE' AS sub_role, 'UC-87.ASSIGN_CAMPUS_LEAD' AS permission_code, 'F' AS permission_level
  UNION ALL SELECT 'STAFF' AS role_code, 'LEADER' AS sub_role, 'UC-88.APPROVE_NEWS' AS permission_code, 'E' AS permission_level
  UNION ALL SELECT 'STAFF' AS role_code, 'STAFF' AS sub_role, 'UC-89.PUBLISH_NEWS' AS permission_code, 'F' AS permission_level
  UNION ALL SELECT 'STAFF' AS role_code, 'LEADER' AS sub_role, 'UC-90.VIEW_NEWS_LIST' AS permission_code, 'R' AS permission_level
  UNION ALL SELECT 'STAFF' AS role_code, 'STAFF' AS sub_role, 'UC-90.VIEW_NEWS_LIST' AS permission_code, 'R' AS permission_level
  UNION ALL SELECT 'STUDENT' AS role_code, 'NONE' AS sub_role, 'UC-90.VIEW_NEWS_LIST' AS permission_code, 'R' AS permission_level
  UNION ALL SELECT 'STAFF' AS role_code, 'LEADER' AS sub_role, 'UC-91.VIEW_NEWS_DETAILS' AS permission_code, 'R' AS permission_level
  UNION ALL SELECT 'STAFF' AS role_code, 'STAFF' AS sub_role, 'UC-91.VIEW_NEWS_DETAILS' AS permission_code, 'R' AS permission_level
  UNION ALL SELECT 'STUDENT' AS role_code, 'NONE' AS sub_role, 'UC-91.VIEW_NEWS_DETAILS' AS permission_code, 'R' AS permission_level
  UNION ALL SELECT 'STAFF' AS role_code, 'STAFF' AS sub_role, 'UC-92.ADD_MULTILINGUAL_NEWS' AS permission_code, 'F' AS permission_level
  UNION ALL SELECT 'STUDENT' AS role_code, 'NONE' AS sub_role, 'UC-92.ADD_MULTILINGUAL_NEWS' AS permission_code, 'F' AS permission_level
  UNION ALL SELECT 'STAFF' AS role_code, 'LEADER' AS sub_role, 'UC-93.MANAGE_NEWS_VISIBILITY' AS permission_code, 'E' AS permission_level
  UNION ALL SELECT 'STAFF' AS role_code, 'STAFF' AS sub_role, 'UC-94.EDIT_NEWS' AS permission_code, 'E' AS permission_level
  UNION ALL SELECT 'STUDENT' AS role_code, 'NONE' AS sub_role, 'UC-94.EDIT_NEWS' AS permission_code, 'E' AS permission_level
  UNION ALL SELECT 'HO' AS role_code, 'NONE' AS sub_role, 'UC-95.VIEW_ACCOUNT_LIST' AS permission_code, 'R' AS permission_level
  UNION ALL SELECT 'STAFF' AS role_code, 'LEADER' AS sub_role, 'UC-95.VIEW_ACCOUNT_LIST' AS permission_code, 'R' AS permission_level
  UNION ALL SELECT 'HO' AS role_code, 'NONE' AS sub_role, 'UC-96.CREATE_ACCOUNT' AS permission_code, 'F' AS permission_level
  UNION ALL SELECT 'STAFF' AS role_code, 'LEADER' AS sub_role, 'UC-96.CREATE_ACCOUNT' AS permission_code, 'F' AS permission_level
  UNION ALL SELECT 'HO' AS role_code, 'NONE' AS sub_role, 'UC-97.MANAGE_ACCOUNT_STATUS' AS permission_code, 'E' AS permission_level
  UNION ALL SELECT 'STAFF' AS role_code, 'LEADER' AS sub_role, 'UC-97.MANAGE_ACCOUNT_STATUS' AS permission_code, 'E' AS permission_level
  UNION ALL SELECT 'HO' AS role_code, 'NONE' AS sub_role, 'UC-98.VIEW_ACCOUNT_DETAILS' AS permission_code, 'R' AS permission_level
  UNION ALL SELECT 'STAFF' AS role_code, 'LEADER' AS sub_role, 'UC-98.VIEW_ACCOUNT_DETAILS' AS permission_code, 'R' AS permission_level
  UNION ALL SELECT 'HO' AS role_code, 'NONE' AS sub_role, 'UC-99.SEARCH_AND_FILTER_ACCOUNTS' AS permission_code, 'R' AS permission_level
  UNION ALL SELECT 'STAFF' AS role_code, 'LEADER' AS sub_role, 'UC-99.SEARCH_AND_FILTER_ACCOUNTS' AS permission_code, 'R' AS permission_level
  UNION ALL SELECT 'STAFF' AS role_code, 'LEADER' AS sub_role, 'UC-100.UPDATE_ACCOUNT_ROLE' AS permission_code, 'E' AS permission_level
  UNION ALL SELECT 'STAFF' AS role_code, 'LEADER' AS sub_role, 'UC-101.ADD_NEW_DEPARTMENT' AS permission_code, 'F' AS permission_level
  UNION ALL SELECT 'STAFF' AS role_code, 'LEADER' AS sub_role, 'UC-102.UPDATE_DEPARTMENT' AS permission_code, 'F' AS permission_level
  UNION ALL SELECT 'STAFF' AS role_code, 'LEADER' AS sub_role, 'UC-103.SEARCH_AND_FILTER_DEPARTMENTS' AS permission_code, 'R' AS permission_level
  UNION ALL SELECT 'STAFF' AS role_code, 'LEADER' AS sub_role, 'UC-104.VIEW_DEPARTMENT_LIST' AS permission_code, 'R' AS permission_level
  UNION ALL SELECT 'STAFF' AS role_code, 'LEADER' AS sub_role, 'UC-105.VIEW_DEPARTMENT_DETAILS' AS permission_code, 'R' AS permission_level
  UNION ALL SELECT 'DEPARTMENT' AS role_code, 'LEADER' AS sub_role, 'UC-105.VIEW_DEPARTMENT_DETAILS' AS permission_code, 'R' AS permission_level
  UNION ALL SELECT 'DEPARTMENT' AS role_code, 'STAFF' AS sub_role, 'UC-105.VIEW_DEPARTMENT_DETAILS' AS permission_code, 'R' AS permission_level
  UNION ALL SELECT 'STAFF' AS role_code, 'LEADER' AS sub_role, 'UC-106.MANAGE_DEPARTMENT_STATUS' AS permission_code, 'E' AS permission_level
  UNION ALL SELECT 'DEPARTMENT' AS role_code, 'LEADER' AS sub_role, 'UC-107.ADD_DEPARTMENT_PERSONNEL' AS permission_code, 'F' AS permission_level
  UNION ALL SELECT 'DEPARTMENT' AS role_code, 'LEADER' AS sub_role, 'UC-108.VIEW_PERSONNEL_DETAILS' AS permission_code, 'R' AS permission_level
  UNION ALL SELECT 'DEPARTMENT' AS role_code, 'STAFF' AS sub_role, 'UC-108.VIEW_PERSONNEL_DETAILS' AS permission_code, 'R' AS permission_level
  UNION ALL SELECT 'DEPARTMENT' AS role_code, 'LEADER' AS sub_role, 'UC-109.SEARCH_PERSONNEL' AS permission_code, 'R' AS permission_level
  UNION ALL SELECT 'DEPARTMENT' AS role_code, 'STAFF' AS sub_role, 'UC-109.SEARCH_PERSONNEL' AS permission_code, 'R' AS permission_level
  UNION ALL SELECT 'DEPARTMENT' AS role_code, 'STAFF' AS sub_role, 'UC-110.REVIEW_ASSIGNED_TASKS' AS permission_code, 'E' AS permission_level
  UNION ALL SELECT 'DEPARTMENT' AS role_code, 'LEADER' AS sub_role, 'UC-111.ASSIGN_TASKS' AS permission_code, 'F' AS permission_level
  UNION ALL SELECT 'STAFF' AS role_code, 'STAFF' AS sub_role, 'UC-112.SIGN_THE_SERVICE_DELIVERY_REPORT' AS permission_code, 'E' AS permission_level
  UNION ALL SELECT 'DEPARTMENT' AS role_code, 'LEADER' AS sub_role, 'UC-112.SIGN_THE_SERVICE_DELIVERY_REPORT' AS permission_code, 'E' AS permission_level
  UNION ALL SELECT 'DEPARTMENT' AS role_code, 'STAFF' AS sub_role, 'UC-112.SIGN_THE_SERVICE_DELIVERY_REPORT' AS permission_code, 'E' AS permission_level
  UNION ALL SELECT 'DEPARTMENT' AS role_code, 'LEADER' AS sub_role, 'UC-113.REMOVE_PERSONNEL' AS permission_code, 'F' AS permission_level
  UNION ALL SELECT 'DEPARTMENT' AS role_code, 'LEADER' AS sub_role, 'UC-114.VIEW_COORDINATION_TASKS' AS permission_code, 'R' AS permission_level
  UNION ALL SELECT 'DEPARTMENT' AS role_code, 'STAFF' AS sub_role, 'UC-114.VIEW_COORDINATION_TASKS' AS permission_code, 'R' AS permission_level
  UNION ALL SELECT 'DEPARTMENT' AS role_code, 'LEADER' AS sub_role, 'UC-115.SEARCH_COORDINATION_TASKS' AS permission_code, 'R' AS permission_level
  UNION ALL SELECT 'DEPARTMENT' AS role_code, 'STAFF' AS sub_role, 'UC-115.SEARCH_COORDINATION_TASKS' AS permission_code, 'R' AS permission_level
  UNION ALL SELECT 'DEPARTMENT' AS role_code, 'LEADER' AS sub_role, 'UC-116.REASSIGN_DEPARTMENT_LEAD' AS permission_code, 'F' AS permission_level
  UNION ALL SELECT 'ADMIN' AS role_code, 'NONE' AS sub_role, 'UC-117.VIEW_ROLE_LIST' AS permission_code, 'R' AS permission_level
  UNION ALL SELECT 'ADMIN' AS role_code, 'NONE' AS sub_role, 'UC-118.CREATE_NEW_ROLE' AS permission_code, 'F' AS permission_level
  UNION ALL SELECT 'ADMIN' AS role_code, 'NONE' AS sub_role, 'UC-119.CONFIGURE_ROLE_PERMISSIONS' AS permission_code, 'F' AS permission_level
  UNION ALL SELECT 'ADMIN' AS role_code, 'NONE' AS sub_role, 'UC-120.UPDATE_ROLE_DETAILS' AS permission_code, 'F' AS permission_level
  UNION ALL SELECT 'ADMIN' AS role_code, 'NONE' AS sub_role, 'UC-121.DISABLE_DELETE_ROLE' AS permission_code, 'F' AS permission_level
  UNION ALL SELECT 'ADMIN' AS role_code, 'NONE' AS sub_role, 'UC-122.VIEW_API_CONFIGURATION' AS permission_code, 'R' AS permission_level
  UNION ALL SELECT 'ADMIN' AS role_code, 'NONE' AS sub_role, 'UC-123.CREATE_API_CONFIGURATION' AS permission_code, 'F' AS permission_level
  UNION ALL SELECT 'ADMIN' AS role_code, 'NONE' AS sub_role, 'UC-124.UPDATE_API_CONFIGURATION' AS permission_code, 'E' AS permission_level
  UNION ALL SELECT 'ADMIN' AS role_code, 'NONE' AS sub_role, 'UC-125.DELETE_API_CONFIGURATION' AS permission_code, 'E' AS permission_level
  UNION ALL SELECT 'ADMIN' AS role_code, 'NONE' AS sub_role, 'UC-126.TEST_API_CONNECTION' AS permission_code, 'F' AS permission_level
  UNION ALL SELECT 'ADMIN' AS role_code, 'NONE' AS sub_role, 'UC-127.MANAGE_API_STATUS' AS permission_code, 'F' AS permission_level
  UNION ALL SELECT 'ADMIN' AS role_code, 'NONE' AS sub_role, 'UC-128.CONFIGURE_REQUEST_LIMIT' AS permission_code, 'F' AS permission_level
  UNION ALL SELECT 'ADMIN' AS role_code, 'NONE' AS sub_role, 'UC-129.VIEW_API_LOGS' AS permission_code, 'R' AS permission_level
  UNION ALL SELECT 'ADMIN' AS role_code, 'NONE' AS sub_role, 'UC-130.SEARCH_API_LOGS' AS permission_code, 'R' AS permission_level
  UNION ALL SELECT 'HO' AS role_code, 'NONE' AS sub_role, 'UC-131.CREATE_AGENDA_TEMPLATE' AS permission_code, 'F' AS permission_level
  UNION ALL SELECT 'HO' AS role_code, 'NONE' AS sub_role, 'UC-132.UPDATE_AGENDA_TEMPLATE' AS permission_code, 'E' AS permission_level
  UNION ALL SELECT 'HO' AS role_code, 'NONE' AS sub_role, 'UC-133.DELETE_AGENDA_TEMPLATE' AS permission_code, 'F' AS permission_level
  UNION ALL SELECT 'HO' AS role_code, 'NONE' AS sub_role, 'UC-134.VIEW_AGENDA_TEMPLATE_LIST' AS permission_code, 'R' AS permission_level
  UNION ALL SELECT 'HO' AS role_code, 'NONE' AS sub_role, 'UC-135.VIEW_AGENDA_TEMPLATE_DETAIL' AS permission_code, 'R' AS permission_level
) x
JOIN roles r ON r.role_code = x.role_code
JOIN permissions p ON p.permission_code = x.permission_code
ON DUPLICATE KEY UPDATE
  permission_level = VALUES(permission_level),
  granted_at = VALUES(granted_at),
  granted_by = VALUES(granted_by);

-- Verification: permission totals by role + sub_role after matrix seed.
SELECT r.role_code, rp.sub_role, COUNT(*) AS total_permissions
FROM role_permissions rp
JOIN roles r ON r.role_id = rp.role_id
GROUP BY r.role_code, rp.sub_role
ORDER BY r.role_code, rp.sub_role;

SET @ap_admin_local = 100040;

SET @ap_ho_google = 100041;

SET @ap_staff_feid = 100042;

SET @ap_visitor_local = 100043;

SET @ap_student_feid = 100044;

SET @ap_disabled_google = 100045;

SET @sess_admin = 100046;

SET @sess_ho_revoked = 100047;

SET @sess_visitor = 100048;

SET @sess_expired = 100049;

SET @sess_staff = 100050;

INSERT INTO user_auth_providers (auth_provider_id, user_id, provider_type, provider_subject, provider_email, is_enabled, linked_at, last_used_at)
VALUES
  (@ap_admin_local, @u_admin_minh, 'LOCAL_PASSWORD', NULL, 'admin@fpt.edu.vn', TRUE, DATE_SUB(@seed_now, INTERVAL 365 DAY), DATE_SUB(@seed_now, INTERVAL 1 DAY)),
  (@ap_ho_google, @u_ho_ha, 'GOOGLE_SSO', 'google-oauth2|ho-fpt', 'ho@fpt.edu.vn', TRUE, DATE_SUB(@seed_now, INTERVAL 350 DAY), DATE_SUB(@seed_now, INTERVAL 1 DAY)),
  (@ap_staff_feid, @u_stafflead_hn, 'FEID', 'FE-STAFF-LEADER-HN', 'staff.leader.hn@fpt.edu.vn', TRUE, DATE_SUB(@seed_now, INTERVAL 330 DAY), DATE_SUB(@seed_now, INTERVAL 1 DAY)),
  (@ap_visitor_local, @v_kim, 'LOCAL_PASSWORD', NULL, 'visitor@example.com', TRUE, DATE_SUB(@seed_now, INTERVAL 75 DAY), DATE_SUB(@seed_now, INTERVAL 5 DAY)),
  (@ap_student_feid, @u_student_anh, 'FEID', 'FE-STUDENT-HN', 'student@fpt.edu.vn', TRUE, DATE_SUB(@seed_now, INTERVAL 190 DAY), DATE_SUB(@seed_now, INTERVAL 1 DAY)),
  (@ap_disabled_google, @u_inactive_dept, 'GOOGLE_SSO', 'google-oauth2|inactive', 'my.inactive@company.vn', FALSE, DATE_SUB(@seed_now, INTERVAL 120 DAY), DATE_SUB(@seed_now, INTERVAL 60 DAY));


-- DEV local-password auth providers for the seeded test accounts.
INSERT IGNORE INTO user_auth_providers
  (auth_provider_id, user_id, provider_type, provider_subject, provider_email, is_enabled, linked_at)
SELECT NULL, u.user_id, 'LOCAL_PASSWORD', NULL, u.email, TRUE, NOW()
FROM users u
WHERE u.email IN (
    'admin@fpt.edu.vn',
    'ho@fpt.edu.vn',
    'staff.leader.hn@fpt.edu.vn',
    'staff.hn@fpt.edu.vn',
    'dept.leader.hn@fpt.edu.vn',
    'dept.hn@fpt.edu.vn',
    'student@fpt.edu.vn',
    'visitor@example.com'
  )
  AND NOT EXISTS (
    SELECT 1
    FROM user_auth_providers ap
    WHERE ap.user_id = u.user_id
      AND ap.provider_type = 'LOCAL_PASSWORD'
  );

INSERT INTO user_sessions (session_id, user_id, login_portal, selected_campus_id, auth_provider_id, refresh_token_hash, refresh_expires_at, refresh_revoked_at, ip_address, user_agent, created_at, expires_at, revoked_at, revoked_by, revoked_reason)
VALUES
  (@sess_admin, @u_admin_minh, 'INTERNAL', NULL, @ap_admin_local, 'hash-admin', DATE_ADD(@seed_now, INTERVAL 7 DAY), NULL, '10.10.1.15', 'Mozilla/5.0 Admin', DATE_SUB(@seed_now, INTERVAL 1 HOUR), DATE_ADD(@seed_now, INTERVAL 8 HOUR), NULL, NULL, NULL),
  (@sess_ho_revoked, @u_ho_ha, 'INTERNAL', NULL, @ap_ho_google, 'hash-ho', DATE_ADD(@seed_now, INTERVAL 5 DAY), DATE_SUB(@seed_now, INTERVAL 1 HOUR), '10.10.1.16', 'Mozilla/5.0 HO', DATE_SUB(@seed_now, INTERVAL 2 DAY), DATE_ADD(@seed_now, INTERVAL 1 DAY), DATE_SUB(@seed_now, INTERVAL 1 HOUR), @u_ho_ha, 'User logout'),
  (@sess_visitor, @v_kim, 'VISITOR', NULL, @ap_visitor_local, 'hash-vis', DATE_ADD(@seed_now, INTERVAL 14 DAY), NULL, '203.113.10.21', 'Mozilla/5.0 Visitor', DATE_SUB(@seed_now, INTERVAL 5 DAY), DATE_ADD(@seed_now, INTERVAL 2 DAY), NULL, NULL, NULL),
  (@sess_expired, @v_lee, 'VISITOR', NULL, NULL, 'hash-expired', DATE_SUB(@seed_now, INTERVAL 1 DAY), NULL, '203.113.10.22', 'Mozilla/5.0 Visitor', DATE_SUB(@seed_now, INTERVAL 10 DAY), DATE_SUB(@seed_now, INTERVAL 1 DAY), NULL, NULL, NULL),
  (@sess_staff, @u_stafflead_hn, 'INTERNAL', NULL, @ap_staff_feid, 'hash-staff', DATE_ADD(@seed_now, INTERVAL 7 DAY), NULL, '10.10.2.15', 'Mozilla/5.0 Staff', DATE_SUB(@seed_now, INTERVAL 20 MINUTE), DATE_ADD(@seed_now, INTERVAL 8 HOUR), NULL, NULL, NULL);

INSERT INTO otp_tokens (otp_token_id, user_id, email, token_type, purpose, token_hash, expires_at, used_at, attempt_count, max_attempts, resend_count, ip_address, user_agent, created_at)
VALUES
  (NULL, @v_pending_approval_seed, 'thaomy.pending.approval@partner.example', 'OTP_CODE', 'VISIT_REQUEST_VERIFY', '636df26e46eb19a12ab4f2aaab155d160c2e19384eb1e8163980b5f157499bc8', DATE_ADD(DATE_SUB(@seed_now, INTERVAL 17 DAY), INTERVAL 10 MINUTE), DATE_ADD(DATE_SUB(@seed_now, INTERVAL 17 DAY), INTERVAL 5 MINUTE), 1, 5, 0, '203.113.11.10', 'Mozilla/5.0', DATE_SUB(@seed_now, INTERVAL 17 DAY)),
  (NULL, @u_admin_minh, 'admin@fpt.edu.vn', 'OTP_CODE', 'CHANGE_SENSITIVE_ACTION', '12ea12eace7d655f471ce55e34f89b1b77a3d9d05a445ca82877dd2235beaa51', DATE_ADD(@seed_now, INTERVAL 5 MINUTE), NULL, 0, 3, 0, '10.10.1.15', 'Mozilla/5.0', DATE_SUB(@seed_now, INTERVAL 5 MINUTE));

INSERT INTO login_logs (user_id, email, login_portal, selected_campus_id, provider_type, status, failure_reason, ip_address, user_agent, session_id, created_at)
VALUES
  
  (@u_admin_minh, 'admin@fpt.edu.vn', 'INTERNAL', @campus_hn, 'LOCAL_PASSWORD', 'SUCCESS', NULL, '10.10.1.15', 'Mozilla/5.0', @sess_admin, DATE_SUB(@seed_now, INTERVAL 1 HOUR)),
  (@u_ho_ha, 'ho@fpt.edu.vn', 'INTERNAL', @campus_hn, 'GOOGLE_SSO', 'SUCCESS', NULL, '10.10.1.16', 'Mozilla/5.0', @sess_ho_revoked, DATE_SUB(@seed_now, INTERVAL 2 DAY)),
  (@v_kim, 'visitor@example.com', 'VISITOR', NULL, 'LOCAL_PASSWORD', 'SUCCESS', NULL, '203.113.10.21', 'Mozilla/5.0', @sess_visitor, DATE_SUB(@seed_now, INTERVAL 5 DAY)),
  (@u_locked_staff, 'huy.locked@company.vn', 'INTERNAL', @campus_hn, 'LOCAL_PASSWORD', 'BLOCKED', 'Account locked', '10.10.1.18', 'Mozilla/5.0', NULL, DATE_SUB(@seed_now, INTERVAL 20 MINUTE)),
  (NULL, 'unknown.person@company.vn', 'INTERNAL', NULL, 'LOCAL_PASSWORD', 'FAILED', 'Invalid credentials', '198.51.100.10', 'curl/8.0', NULL, DATE_SUB(@seed_now, INTERVAL 15 MINUTE)),
  (@u_student_anh, 'student@fpt.edu.vn', 'INTERNAL', @campus_hn, 'FEID', 'SUCCESS', NULL, '10.10.1.19', 'Mozilla/5.0', NULL, DATE_SUB(@seed_now, INTERVAL 1 DAY));

INSERT INTO security_events (user_id, email, event_type, severity, ip_address, user_agent, metadata, created_at)
VALUES
  
  (@u_admin_minh, 'admin@fpt.edu.vn', 'LOGIN_SUCCESS_REVIEWED', 'LOW', '10.10.1.15', 'Mozilla/5.0', JSON_OBJECT('portal','INTERNAL'), DATE_SUB(@seed_now, INTERVAL 1 HOUR)),
  (@v_smith, 'emily.smith@greentech.example', 'OTP_FAILED', 'MEDIUM', '203.113.11.11', 'Mozilla/5.0', JSON_OBJECT('attempt_count',5), DATE_SUB(@seed_now, INTERVAL 30 MINUTE)),
  (@u_locked_staff, 'huy.locked@company.vn', 'LOGIN_LOCKED', 'HIGH', '10.10.1.18', 'Mozilla/5.0', JSON_OBJECT('failed_login_count',7), DATE_SUB(@seed_now, INTERVAL 20 MINUTE)),
  (NULL, 'unknown.person@company.vn', 'SUSPICIOUS_IP', 'CRITICAL', '198.51.100.10', 'curl/8.0', JSON_OBJECT('blocked',true), DATE_SUB(@seed_now, INTERVAL 10 MINUTE));

SET @p_seoul = 100051;

SET @p_green = 100052;

SET @p_ministry = 100053;

SET @p_asean = 100054;

SET @p_legacy = 100055;

SET @c_kim = 100056;

SET @c_lee = 100057;

SET @c_smith = 100058;

SET @c_tanaka = 100059;

SET @c_ied = 100060;

SET @c_inactive = 100061;

INSERT INTO partners (partner_id, partner_code, name, short_name, country, city, website_url, partner_type, cooperation_status, description, created_at, created_by, updated_at, updated_by)
VALUES
  (@p_seoul, 'UNI-KR-SEOUTECH', 'Đại học Công nghệ Seoul', 'SeoulTech', 'Hàn Quốc', 'Seoul', 'https://seoultech.example', 'UNIVERSITY', 'ACTIVE', 'Đối tác trao đổi học thuật và nghiên cứu AI ứng dụng trong giáo dục.', DATE_SUB(@seed_now, INTERVAL 320 DAY), @u_stafflead_hn, @seed_now, @u_stafflead_hn),
  (@p_green, 'COM-SG-GREENTECH', 'GreenTech Asia Pte. Ltd.', 'GreenTech Asia', 'Singapore', 'Singapore', 'https://greentech.example', 'COMPANY', 'POTENTIAL', 'Doanh nghiệp công nghệ xanh đang thảo luận chương trình internship và campus tour.', DATE_SUB(@seed_now, INTERVAL 80 DAY), @u_staff_hcm, @seed_now, @u_staff_hcm),
  (@p_ministry, 'GOV-VN-IED', 'Viện Phát triển Giáo dục Quốc tế', 'IED Vietnam', 'Việt Nam', 'Hà Nội', 'https://ied.example.vn', 'GOVERNMENT', 'ACTIVE', 'Đơn vị quản lý chương trình hợp tác giáo dục quốc tế.', DATE_SUB(@seed_now, INTERVAL 250 DAY), @u_ho_ha, @seed_now, @u_ho_ha),
  (@p_asean, 'NGO-ASEAN-FSF', 'ASEAN Future Skills Foundation', 'AFSF', 'Thái Lan', 'Bangkok', 'https://fsf-asean.example', 'NGO', 'INACTIVE', 'Tổ chức từng phối hợp hội thảo kỹ năng số, hiện tạm dừng.', DATE_SUB(@seed_now, INTERVAL 600 DAY), @u_ho_linh, DATE_SUB(@seed_now, INTERVAL 60 DAY), @u_ho_linh),
  (@p_legacy, 'OTH-LEGACY-0001', 'Legacy Vendor Liaison Office', 'LVLO', 'Malaysia', 'Kuala Lumpur', 'https://legacy-vendor.example', 'OTHER', 'BLACKLISTED', 'Đơn vị bị chặn do vi phạm quy trình xác nhận tài liệu.', DATE_SUB(@seed_now, INTERVAL 700 DAY), @u_admin_minh, DATE_SUB(@seed_now, INTERVAL 200 DAY), @u_admin_minh);

INSERT INTO partner_contacts (contact_id, partner_id, full_name, email, phone, job_title, department_name, note, is_primary, status, created_at, created_by, updated_at, updated_by)
VALUES
  (@c_kim, @p_seoul, 'Kim Min Seo', 'kim.minseo@seoultech.example', '+821012345678', 'International Relations Manager', 'Office of Global Affairs', 'Người phụ trách chính đoàn thăm Hà Nội.', TRUE, 'ACTIVE', DATE_SUB(@seed_now, INTERVAL 70 DAY), @u_stafflead_hn, @seed_now, @u_stafflead_hn),
  (@c_lee, @p_seoul, 'Lee Joon Ho', 'lee.joonho@seoultech.example', '+821055512345', 'Associate Professor', 'Computer Science', 'Phụ trách nội dung học thuật AI.', FALSE, 'ACTIVE', DATE_SUB(@seed_now, INTERVAL 65 DAY), @u_staff_hn, @seed_now, @u_staff_hn),
  (@c_smith, @p_green, 'Emily Smith', 'emily.smith@greentech.example', '+6591234567', 'Partnership Director', 'Regional Partnership', 'Đầu mối GreenTech.', TRUE, 'ACTIVE', DATE_SUB(@seed_now, INTERVAL 80 DAY), @u_staff_hcm, @seed_now, @u_staff_hcm),
  (@c_tanaka, @p_seoul, 'Tanaka Aoi', 'aoi.tanaka@kyoto-global.example', '+819012345678', 'Study Tour Coordinator', 'Global Learning Office', 'OCR từ danh thiếp.', FALSE, 'ACTIVE', DATE_SUB(@seed_now, INTERVAL 60 DAY), @u_staff_ct, @seed_now, @u_staff_ct),
  (@c_ied, @p_ministry, 'Nguyễn Văn Nam', 'nguyen.van.nam@partner.example', '0909000009', 'Chuyên viên hợp tác quốc tế', 'Phòng Kế hoạch', 'Tên kiểm thử search dấu/không dấu.', TRUE, 'ACTIVE', DATE_SUB(@seed_now, INTERVAL 50 DAY), @u_ho_ha, @seed_now, @u_ho_ha),
  (@c_inactive, @p_asean, 'Somchai Anan', 'anan.somchai@fsf-asean.example', '+6625550000', 'Former Program Officer', 'Digital Skills', 'Liên hệ cũ.', FALSE, 'INACTIVE', DATE_SUB(@seed_now, INTERVAL 580 DAY), @u_ho_linh, DATE_SUB(@seed_now, INTERVAL 60 DAY), @u_ho_linh);

SET @file_news_ai = 100062;

SET @file_news_green = 100063;

SET @file_news_policy = 100064;

SET @file_gallery_hn = 100065;

SET @file_gallery_hcm = 100066;

SET @file_gallery_hidden = 100067;

SET @file_doc_general = 100068;

SET @file_doc_partner = 100069;

SET @file_doc_visit = 100070;

SET @file_doc_minutes = 100071;

SET @file_doc_logistics = 100072;

SET @file_doc_report = 100073;

SET @file_edge_small = 100074;

SET @file_edge_large = 100075;

INSERT INTO files (file_id, storage_provider, bucket_name, object_key, original_filename, mime_type, file_size, checksum_sha256, visibility, uploaded_by, uploaded_at)
VALUES
  (@file_news_ai, 'LOCAL', NULL, 'public/news/ai-campus-tour-cover.jpg', 'ai-campus-tour-cover.jpg', 'image/jpeg', 524288, 'f586c69188453c3f2100f7a7d245e9a964fd09d12466c8a04988add5036ba001', 'PUBLIC', @u_staff_hn, DATE_SUB(@seed_now, INTERVAL 10 DAY)),
  (@file_news_green, 'S3', 'pems-public', 'public/news/greentech-workshop.jpg', 'greentech-workshop.jpg', 'image/jpeg', 734003, '4f992863674222075dde7fd8314540c98b3a8f39953473fd55a32c375f62d2d5', 'PUBLIC', @u_staff_hcm, DATE_SUB(@seed_now, INTERVAL 11 DAY)),
  (@file_news_policy, 'AZURE', 'pems-internal', 'internal/news/policy-update.pdf', 'policy-update.pdf', 'application/pdf', 102400, '138dbb72c41b237734e8dca2c79e09459f1b79e68617d1da4d79ef06c549d62f', 'INTERNAL', @u_ho_ha, DATE_SUB(@seed_now, INTERVAL 12 DAY)),
  (@file_gallery_hn, 'GCS', 'pems-gallery', 'gallery/hn/innovation-hall-01.jpg', 'innovation-hall-01.jpg', 'image/jpeg', 2048576, '39cc86536893f391a16f5ed0311e7ab08f33ffd7fbc5143c8927584e077d38e6', 'PUBLIC', @u_staff_hn, DATE_SUB(@seed_now, INTERVAL 13 DAY)),
  (@file_gallery_hcm, 'GOOGLE_DRIVE', 'pems-gallery', 'gallery/hcm/green-lab-01.jpg', 'green-lab-01.jpg', 'image/jpeg', 1900000, 'af843ca41b7aeb9bdd2e19840b067eee9127e4aa62ba0a4e1d0fa7087669d7ec', 'PUBLIC', @u_staff_hcm, DATE_SUB(@seed_now, INTERVAL 14 DAY)),
  (@file_gallery_hidden, 'OTHER', 'legacy-drive', 'gallery/archive/private-briefing.jpg', 'private-briefing.jpg', 'image/jpeg', 777777, 'd785237c7b3067120caaa6926f1abbb7916af31557d16562c2edf9694cd53bd7', 'PRIVATE', @u_stafflead_hn, DATE_SUB(@seed_now, INTERVAL 15 DAY)),
  (@file_doc_general, 'LOCAL', NULL, 'documents/process/quy-trinh-tiep-doan-quoc-te.pdf', 'quy-trinh-tiep-doan-quoc-te.pdf', 'application/pdf', 409600, '9eb10c831b3e4b91cf347ac576794a27dbe2a3c2b21a3eab20800dc9430f2bac', 'INTERNAL', @u_ho_ha, DATE_SUB(@seed_now, INTERVAL 17 DAY)),
  (@file_doc_partner, 'S3', 'pems-private', 'partners/seoultech/mou-draft-v3.docx', 'mou-draft-v3.docx', 'application/vnd.openxmlformats-officedocument.wordprocessingml.document', 256000, '059a10ad5a54b2632b692ca1edb742ec6deed22bb6f89a49d3729516caaf4cc1', 'PRIVATE', @u_stafflead_hn, DATE_SUB(@seed_now, INTERVAL 18 DAY)),
  (@file_doc_visit, 'LOCAL', NULL, 'visits/vr-approved-single/agenda-final.xlsx', 'agenda-final.xlsx', 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet', 88000, '5f79b35474e431e6c8b0fc191c3afde8cfa0f95d50538c1073bc5b335b02ab78', 'INTERNAL', @u_staff_hn, DATE_SUB(@seed_now, INTERVAL 19 DAY)),
  (@file_doc_minutes, 'LOCAL', NULL, 'minutes/closed-visit/bien-ban-hop-final.pdf', 'bien-ban-hop-final.pdf', 'application/pdf', 180000, 'a78dbdcb521c741795e9114016d03f454964678e51d999824122d6d89496433f', 'INTERNAL', @u_staff_hn, DATE_SUB(@seed_now, INTERVAL 20 DAY)),
  (@file_doc_logistics, 'LOCAL', NULL, 'logistics/room-layout-a101.png', 'room-layout-a101.png', 'image/png', 130000, '17a5a8bb0cd9f5912937e9f442b5a0de7a3cc1085d3f6a67d6639d4a297d0484', 'INTERNAL', @u_dept_it_hn, DATE_SUB(@seed_now, INTERVAL 21 DAY)),
  (@file_doc_report, 'LOCAL', NULL, 'reports/monthly/bao-cao-doan-khach.xlsx', 'bao-cao-doan-khach.xlsx', 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet', 210000, '8c6254acb936079ce44fceee7b364bb963b8bc1b7a5227b0c7975d3a25c90f88', 'INTERNAL', @u_ho_ha, DATE_SUB(@seed_now, INTERVAL 22 DAY)),
  (@file_edge_small, 'LOCAL', NULL, 'edge/empty-note.txt', 'empty-note.txt', 'text/plain', 0, '7fc40513bc72b25d2d77ae6fb014e837461925fcb31af067b121c247dba8a635', 'PRIVATE', @u_admin_minh, DATE_SUB(@seed_now, INTERVAL 23 DAY)),
  (@file_edge_large, 'S3', 'pems-archive', 'edge/near-limit-object-key-long-name-for-testing-search-and-storage-behaviour.bin', 'near-limit-archive.bin', 'application/octet-stream', 4294967295, '40ade8b889b7733e8080a26a43c583fd5178b92017f3af0e50031fb613d6b0d5', 'PRIVATE', @u_admin_minh, DATE_SUB(@seed_now, INTERVAL 24 DAY));

INSERT INTO faqs (faq_id, category, question, answer, display_order, status, created_at, created_by, updated_at, updated_by)
VALUES
  (NULL, 'Visit Request', 'Làm thế nào để gửi yêu cầu thăm quan campus?', 'Khách chọn mục Gửi yêu cầu thăm quan, điền thông tin và xác thực email bằng OTP.', 1, 'PUBLISHED', DATE_SUB(@seed_now, INTERVAL 90 DAY), @u_staff_hn, @seed_now, @u_staff_hn),
  (NULL, 'Visit Request', 'How can I update a submitted delegation request?', 'Only requests in editable workflow status can be updated.', 2, 'PUBLISHED', DATE_SUB(@seed_now, INTERVAL 88 DAY), @u_staff_hcm, @seed_now, @u_staff_hcm),
  (NULL, 'Security', 'Khách có cần mang giấy tờ tùy thân không?', 'Có. Thành viên đoàn cần mang hộ chiếu hoặc căn cước.', 3, 'HIDDEN', DATE_SUB(@seed_now, INTERVAL 85 DAY), @u_stafflead_hn, @seed_now, @u_stafflead_hn),
  (NULL, 'Logistics', 'Xe đoàn khách có được vào trong campus không?', 'Tùy từng campus và lịch bảo vệ.', 4, 'HIDDEN', DATE_SUB(@seed_now, INTERVAL 2 DAY), @u_staff_ct, NULL, NULL),
  (NULL, 'Visit Request', 'Câu hỏi cũ về biểu mẫu giấy có còn áp dụng không?', 'Không. Quy trình đã chuyển sang biểu mẫu trực tuyến.', 99, 'HIDDEN', DATE_SUB(@seed_now, INTERVAL 400 DAY), @u_ho_ha, DATE_SUB(@seed_now, INTERVAL 200 DAY), @u_ho_ha);

SET @news_pending = 100076;
SET @news_rejected = 100077;
SET @news_published = 100078;
SET @news_hidden = 100079;

SET @ntr_pending_vi = 100080;
SET @ntr_rejected_vi = 100081;
SET @ntr_published_vi = 100082;
SET @ntr_published_en = 100083;
SET @ntr_hidden_vi = 100084;

SET @sec_pending_1 = 100085;
SET @sec_rejected_1 = 100086;
SET @sec_published_1 = 100087;
SET @sec_published_2 = 100088;
SET @sec_hidden_1 = 100089;

INSERT INTO news (news_id, campus_id, visit_instance_id, author_user_id, cover_file_id, status, submitted_at, reviewed_by, reviewed_at, review_note, published_at, is_featured, row_version, created_at, created_by, updated_at, updated_by)
VALUES
  (@news_pending, @campus_hcm, NULL, @u_staff_hcm, @file_news_green, 'PENDING_REVIEW', DATE_SUB(@seed_now, INTERVAL 2 DAY), NULL, NULL, NULL, NULL, FALSE, 1, DATE_SUB(@seed_now, INTERVAL 2 DAY), @u_staff_hcm, @seed_now, @u_staff_hcm),
  (@news_rejected, @campus_dn, NULL, @u_staff_dn, @file_news_policy, 'REJECTED', DATE_SUB(@seed_now, INTERVAL 10 DAY), @u_stafflead_dn, DATE_SUB(@seed_now, INTERVAL 1 DAY), 'Cần bổ sung ảnh có bản quyền rõ ràng.', NULL, FALSE, 2, DATE_SUB(@seed_now, INTERVAL 10 DAY), @u_staff_dn, DATE_SUB(@seed_now, INTERVAL 1 DAY), @u_stafflead_dn),
  (@news_published, @campus_hn, NULL, @u_staff_hn, @file_news_ai, 'PUBLISHED', DATE_SUB(@seed_now, INTERVAL 35 DAY), @u_stafflead_hn, DATE_SUB(@seed_now, INTERVAL 31 DAY), 'Bài viết đủ điều kiện công khai.', DATE_SUB(@seed_now, INTERVAL 30 DAY), TRUE, 4, DATE_SUB(@seed_now, INTERVAL 35 DAY), @u_staff_hn, DATE_SUB(@seed_now, INTERVAL 30 DAY), @u_stafflead_hn),
  (@news_hidden, @campus_hcm, NULL, @u_staff_hcm, @file_news_green, 'HIDDEN', DATE_SUB(@seed_now, INTERVAL 70 DAY), @u_stafflead_hcm, DATE_SUB(@seed_now, INTERVAL 61 DAY), 'Ẩn tạm thời.', DATE_SUB(@seed_now, INTERVAL 60 DAY), FALSE, 5, DATE_SUB(@seed_now, INTERVAL 70 DAY), @u_staff_hcm, DATE_SUB(@seed_now, INTERVAL 10 DAY), @u_stafflead_hcm);

INSERT INTO news_translations (news_translation_id, news_id, language_code, title, slug, summary, seo_title, seo_description, created_at, updated_at)
VALUES
  (@ntr_pending_vi, @news_pending, 'vi', 'GreenTech Asia đề xuất workshop bền vững', 'greentech-asia-de-xuat-workshop-ben-vung', 'Bài viết đang chờ host duyệt trước khi công khai.', 'GreenTech Asia đề xuất workshop bền vững', 'SEO GreenTech Asia đề xuất workshop bền vững', DATE_SUB(@seed_now, INTERVAL 2 DAY), @seed_now),
  (@ntr_rejected_vi, @news_rejected, 'vi', 'Bài viết cần bổ sung quyền hình ảnh', 'bai-viet-can-bo-sung-quyen-hinh-anh', 'Bài viết bị từ chối để tác giả bổ sung minh chứng quyền ảnh.', 'Bài viết cần bổ sung quyền hình ảnh', 'SEO Bài viết cần bổ sung quyền hình ảnh', DATE_SUB(@seed_now, INTERVAL 10 DAY), @seed_now),
  (@ntr_published_vi, @news_published, 'vi', 'FPTU đón đoàn Đại học Công nghệ Seoul', 'fptu-don-doan-seoultech', 'Hoạt động trao đổi học thuật về AI trong giáo dục.', 'FPTU đón đoàn Đại học Công nghệ Seoul', 'SEO description', DATE_SUB(@seed_now, INTERVAL 30 DAY), @seed_now),
  (@ntr_published_en, @news_published, 'en', 'FPTU welcomes SeoulTech delegation', 'fptu-welcomes-seoultech', 'Academic exchange activities about AI in education.', 'FPTU welcomes SeoulTech delegation', 'SEO description', DATE_SUB(@seed_now, INTERVAL 30 DAY), @seed_now),
  (@ntr_hidden_vi, @news_hidden, 'vi', 'Thông báo sự kiện đang cập nhật', 'thong-bao-su-kien-dang-cap-nhat', 'Bài viết đã được ẩn khỏi trang tin thường.', 'Thông báo sự kiện đang cập nhật', 'SEO Thông báo sự kiện đang cập nhật', DATE_SUB(@seed_now, INTERVAL 5 DAY), @seed_now);

INSERT INTO news_content_sections (section_id, news_translation_id, section_order, section_title, section_body_html, section_body_text, created_at, updated_at)
VALUES
  (@sec_pending_1, @ntr_pending_vi, 1, 'Đề xuất nội dung workshop', '<p><strong>GreenTech Asia</strong> đề xuất tổ chức workshop về phát triển bền vững tại campus.</p>', 'GreenTech Asia đề xuất tổ chức workshop về phát triển bền vững tại campus.', DATE_SUB(@seed_now, INTERVAL 2 DAY), @seed_now),
  (@sec_rejected_1, @ntr_rejected_vi, 1, 'Nội dung cần bổ sung', '<p>Bài viết cần bổ sung thông tin bản quyền hình ảnh trước khi đăng.</p>', 'Bài viết cần bổ sung thông tin bản quyền hình ảnh trước khi đăng.', DATE_SUB(@seed_now, INTERVAL 10 DAY), @seed_now),
  (@sec_published_1, @ntr_published_vi, 1, 'Không khí đón tiếp', '<p><strong>Đoàn khách</strong> được đón tiếp tại sảnh Alpha.</p><img src="/api/files/FILE_ID/view" alt="Đoàn khách tại sảnh Alpha" />', 'Đoàn khách được đón tiếp tại sảnh Alpha.', DATE_SUB(@seed_now, INTERVAL 30 DAY), @seed_now),
  (@sec_published_2, @ntr_published_vi, 2, 'Trao đổi học thuật', '<p>Hai bên trao đổi về cơ hội hợp tác trong lĩnh vực <em>AI in Education</em>.</p>', 'Hai bên trao đổi về cơ hội hợp tác trong lĩnh vực AI in Education.', DATE_SUB(@seed_now, INTERVAL 30 DAY), @seed_now),
  (@sec_hidden_1, @ntr_hidden_vi, 1, 'Nội dung đang cập nhật', '<p>Nội dung đang được cập nhật lại trước khi hiển thị.</p>', 'Nội dung đang được cập nhật lại trước khi hiển thị.', DATE_SUB(@seed_now, INTERVAL 5 DAY), @seed_now);

INSERT INTO news_section_files (section_file_id, section_id, file_id, usage_type, display_order, created_at)
VALUES
  (NULL, @sec_published_1, @file_news_ai, 'INLINE_IMAGE', 1, DATE_SUB(@seed_now, INTERVAL 30 DAY)),
  (NULL, @sec_pending_1, @file_news_green, 'INLINE_IMAGE', 1, DATE_SUB(@seed_now, INTERVAL 2 DAY));

SET @tpl_verify = 100090;

SET @tpl_approved = 100091;

SET @tpl_rejected = 100092;

SET @tpl_task = 100093;

SET @tpl_inactive = 100094;

INSERT INTO email_templates (email_template_id, template_code, name, purpose, status, translations_json, variables_json, created_at, created_by, updated_at, updated_by)
VALUES
  (@tpl_verify, 'VISIT_REQUEST_VERIFY', 'Xác thực email yêu cầu thăm quan', 'VISIT_REQUEST_VERIFY', 'ACTIVE', JSON_OBJECT('vi',JSON_OBJECT('subject','Mã xác thực yêu cầu thăm quan','body','OTP {{OtpCode}}'),'en',JSON_OBJECT('subject','Verify visit request','body','OTP {{OtpCode}}')), JSON_ARRAY('FullName','OtpCode'), DATE_SUB(@seed_now, INTERVAL 200 DAY), @u_admin_minh, @seed_now, @u_admin_minh),
  (@tpl_approved, 'VISIT_REQUEST_APPROVED', 'Thông báo duyệt visit request', 'VISIT_DECISION', 'ACTIVE', JSON_OBJECT('vi',JSON_OBJECT('subject','Yêu cầu đã được duyệt','body','Request {{RequestCode}} approved')), JSON_ARRAY('RequestCode'), DATE_SUB(@seed_now, INTERVAL 200 DAY), @u_admin_minh, @seed_now, @u_admin_minh),
  (@tpl_rejected, 'VISIT_REQUEST_REJECTED', 'Thông báo từ chối visit request', 'VISIT_DECISION', 'ACTIVE', JSON_OBJECT('vi',JSON_OBJECT('subject','Yêu cầu chưa được duyệt','body','Reason {{Reason}}')), JSON_ARRAY('RequestCode','Reason'), DATE_SUB(@seed_now, INTERVAL 200 DAY), @u_admin_minh, @seed_now, @u_admin_minh),
  (@tpl_task, 'LOGISTICS_TASK_ASSIGNED', 'Thông báo phân công hậu cần', 'LOGISTICS', 'ACTIVE', JSON_OBJECT('vi',JSON_OBJECT('subject','Bạn được phân công hậu cần','body','Task {{ItemTitle}}')), JSON_ARRAY('ItemTitle'), DATE_SUB(@seed_now, INTERVAL 180 DAY), @u_admin_minh, @seed_now, @u_admin_minh),
  (@tpl_inactive, 'OLD_WEEKLY_DIGEST', 'Mẫu tổng hợp tuần cũ', 'DIGEST', 'INACTIVE', JSON_OBJECT('vi',JSON_OBJECT('subject','Tổng hợp tuần cũ','body','Inactive')), JSON_ARRAY('WeekRange'), DATE_SUB(@seed_now, INTERVAL 500 DAY), @u_admin_minh, DATE_SUB(@seed_now, INTERVAL 300 DAY), @u_admin_minh);

SET @vr_pending_approval_seed = 100095;

SET @vi_pending_approval_hcm = 100096;

SET @vr_pending_approval_multi = 100097;

SET @vi_pa_hn = 100098;

SET @vi_pa_ct = 100099;

SET @vr_approved_single_before = 100100;

SET @vi_as_hn = 100101;

SET @vr_approved_multi_during = 100102;

SET @vi_am_hn = 100103;

SET @vi_am_hcm = 100104;

SET @vr_rejected_single = 100105;

SET @vi_rs_hcm = 100106;

SET @vr_rejected_multi = 100107;

SET @vi_rm_dn = 100108;

SET @vi_rm_ct = 100109;

SET @vr_cancelled = 100110;

SET @vi_cn_hn = 100111;

SET @vr_after_visit = 100112;

SET @vi_av_ct = 100113;

SET @vr_closed = 100114;

SET @vi_cl_hn = 100115;

SET @vr_cancelled_instance = 100116;

SET @vi_ci_hcm = 100117;

SET @vi_ci_hn = 100118;

SET @vr_assigned_only = 100119;

SET @vi_ao_dn = 100120;

SET @vr_hist_01 = 100121; SET @vi_hist_01 = 100122;

SET @vr_hist_02 = 100123; SET @vi_hist_02 = 100124;

SET @vr_hist_03 = 100125; SET @vi_hist_03 = 100126;

SET @vr_hist_04 = 100127; SET @vi_hist_04 = 100128;

SET @vr_hist_05 = 100129; SET @vi_hist_05 = 100130;

SET @vr_hist_06 = 100131; SET @vi_hist_06 = 100132;

SET @vr_hist_07 = 100133; SET @vi_hist_07 = 100134;

SET @vr_hist_08 = 100135; SET @vi_hist_08 = 100136;

SET @vr_hist_09 = 100137; SET @vi_hist_09 = 100138;

SET @vr_hist_10 = 100139; SET @vi_hist_10 = 100140;

SET @vr_hist_11 = 100141; SET @vi_hist_11 = 100142;

SET @vr_hist_12 = 100143; SET @vi_hist_12 = 100144;

SET @vr_hist_13 = 100145; SET @vi_hist_13 = 100146;

SET @vr_hist_14 = 100147; SET @vi_hist_14 = 100148;

SET @vr_hist_15 = 100149; SET @vi_hist_15 = 100150;

SET @vr_hist_16 = 100151; SET @vi_hist_16 = 100152;

SET @vr_hist_17 = 100153; SET @vi_hist_17 = 100154;

SET @vr_hist_18 = 100155; SET @vi_hist_18 = 100156;

SET @vr_hist_19 = 100157; SET @vi_hist_19 = 100158;

SET @vr_hist_20 = 100159; SET @vi_hist_20 = 100160;

SET @vr_hist_21 = 100161; SET @vi_hist_21 = 100162;

SET @vr_hist_22 = 100163; SET @vi_hist_22 = 100164;

SET @vr_hist_23 = 100165; SET @vi_hist_23 = 100166;

SET @vr_hist_24 = 100167; SET @vi_hist_24 = 100168;

SET @vr_hist_25 = 100169; SET @vi_hist_25 = 100170;

SET @vr_hist_26 = 100171; SET @vi_hist_26 = 100172;

SET @vr_hist_27 = 100173; SET @vi_hist_27 = 100174;

SET @vr_hist_28 = 100175; SET @vi_hist_28 = 100176;

SET @vr_hist_29 = 100177; SET @vi_hist_29 = 100178;

SET @vr_hist_30 = 100179; SET @vi_hist_30 = 100180;

SET @vr_hist_31 = 100181; SET @vi_hist_31 = 100182;

SET @vr_hist_32 = 100183; SET @vi_hist_32 = 100184;

SET @vr_hist_33 = 100185; SET @vi_hist_33 = 100186;

SET @vr_hist_34 = 100187; SET @vi_hist_34 = 100188;

SET @vr_hist_35 = 100189; SET @vi_hist_35 = 100190;

SET @vr_hist_36 = 100191; SET @vi_hist_36 = 100192;

SET @vr_hist_37 = 100193; SET @vi_hist_37 = 100194;

SET @vr_hist_38 = 100195; SET @vi_hist_38 = 100196;

SET @vr_hist_39 = 100197; SET @vi_hist_39 = 100198;

SET @vr_hist_40 = 100199; SET @vi_hist_40 = 100200;

SET @vr_hist_41 = 100201; SET @vi_hist_41 = 100202;

SET @vr_hist_42 = 100203; SET @vi_hist_42 = 100204;

SET @vr_hist_43 = 100205; SET @vi_hist_43 = 100206;

SET @vr_hist_44 = 100207; SET @vi_hist_44 = 100208;

INSERT INTO visit_requests (visit_request_id, request_code, visitor_user_id, partner_id, registrant_full_name, registrant_organization, registrant_job_title, registrant_phone, registrant_email, delegation_name, visit_scope, purpose, working_content, expected_guest_count, support_team_json, contact_person_json, working_language, interpreter_note, transportation_note, note_to_fptu, status, submitted_at, email_verified_at, decided_by, decided_at, decision_actor_role, decision_note, row_version, created_at, created_by, updated_at, updated_by)
VALUES
  (@vr_pending_approval_seed, 'VR-PA-001', @v_pending_approval_seed, @p_green, 'Nguyễn Thảo My', 'Tổ chức đối tác quốc tế', 'Điều phối đoàn khách', '0900000000', 'thaomy.pending.approval@partner.example', 'Đoàn GreenTech Asia khảo sát workshop bền vững', 'SINGLE_CAMPUS', 'Trao đổi hợp tác, tham quan campus và làm việc với các phòng ban.', 'Làm việc với IC Office, tham quan khu học tập, phòng lab và trao đổi hợp tác.', 3, JSON_ARRAY(JSON_OBJECT('full_name','Điều phối viên đoàn','role','Coordinator')), JSON_OBJECT('full_name','Nguyễn Thảo My','organization','Tổ chức đối tác quốc tế','phone','0900000000','email','thaomy.pending.approval@partner.example'), 'EN', NULL, 'Đoàn tự túc xe 16 chỗ.', 'Vui lòng hỗ trợ bảng chào mừng và phòng họp.', 'PENDING_APPROVAL', DATE_SUB(@seed_now, INTERVAL 17 DAY), DATE_ADD(DATE_SUB(@seed_now, INTERVAL 17 DAY), INTERVAL 5 MINUTE), NULL, NULL, NULL, 'Đã xác thực email, chờ duyệt request.', 0, DATE_SUB(@seed_now, INTERVAL 17 DAY), @v_pending_approval_seed, @seed_now, NULL),
  (@vr_pending_approval_multi, 'VR-PA-002', @v_pending_approval, @p_ministry, 'Nguyễn Văn Nam', 'Tổ chức đối tác quốc tế', 'Điều phối đoàn khách', '0900000000', 'nam.pending.approval@partner.example', 'Đoàn Viện Phát triển Giáo dục Quốc tế làm việc liên cơ sở', 'MULTI_CAMPUS', 'Trao đổi hợp tác, tham quan campus và làm việc với các phòng ban.', 'Làm việc với IC Office, tham quan khu học tập, phòng lab và trao đổi hợp tác.', 4, JSON_ARRAY(JSON_OBJECT('full_name','Điều phối viên đoàn','role','Coordinator')), JSON_OBJECT('full_name','Nguyễn Văn Nam','organization','Tổ chức đối tác quốc tế','phone','0900000000','email','nam.pending.approval@partner.example'), 'EN', NULL, 'Đoàn tự túc xe 16 chỗ.', 'Vui lòng hỗ trợ bảng chào mừng và phòng họp.', 'PENDING_APPROVAL', DATE_SUB(@seed_now, INTERVAL 24 DAY), DATE_SUB(@seed_now, INTERVAL 23 DAY), NULL, NULL, NULL, 'Chờ HO duyệt.', 0, DATE_SUB(@seed_now, INTERVAL 24 DAY), @v_pending_approval, @seed_now, NULL),
  (@vr_approved_single_before, 'VR-AS-003', @v_kim, @p_seoul, 'Kim Min Seo', 'Tổ chức đối tác quốc tế', 'Điều phối đoàn khách', '0900000000', 'kim.minseo@seoultech.example', 'Đoàn SeoulTech trao đổi học thuật tại Hà Nội', 'SINGLE_CAMPUS', 'Trao đổi hợp tác, tham quan campus và làm việc với các phòng ban.', 'Làm việc với IC Office, tham quan khu học tập, phòng lab và trao đổi hợp tác.', 3, JSON_ARRAY(JSON_OBJECT('full_name','Điều phối viên đoàn','role','Coordinator')), JSON_OBJECT('full_name','Kim Min Seo','organization','Tổ chức đối tác quốc tế','phone','0900000000','email','kim.minseo@seoultech.example'), 'EN', NULL, 'Đoàn tự túc xe 16 chỗ.', 'Vui lòng hỗ trợ bảng chào mừng và phòng họp.', 'APPROVED', DATE_SUB(@seed_now, INTERVAL 29 DAY), DATE_SUB(@seed_now, INTERVAL 28 DAY), @u_stafflead_hn, DATE_SUB(@seed_now, INTERVAL 26 DAY), 'STAFF_LEADER', 'Đủ thông tin đoàn, lịch phù hợp.', 0, DATE_SUB(@seed_now, INTERVAL 29 DAY), @v_kim, @seed_now, @u_stafflead_hn),
  (@vr_approved_multi_during, 'VR-AM-004', @v_lee, @p_seoul, 'Lee Joon Ho', 'Tổ chức đối tác quốc tế', 'Điều phối đoàn khách', '0900000000', 'lee.joonho@seoultech.example', 'Đoàn SeoulTech tham quan Hà Nội và TP.HCM', 'MULTI_CAMPUS', 'Trao đổi hợp tác, tham quan campus và làm việc với các phòng ban.', 'Làm việc với IC Office, tham quan khu học tập, phòng lab và trao đổi hợp tác.', 4, JSON_ARRAY(JSON_OBJECT('full_name','Điều phối viên đoàn','role','Coordinator')), JSON_OBJECT('full_name','Lee Joon Ho','organization','Tổ chức đối tác quốc tế','phone','0900000000','email','lee.joonho@seoultech.example'), 'EN', NULL, 'Đoàn tự túc xe 16 chỗ.', 'Vui lòng hỗ trợ bảng chào mừng và phòng họp.', 'APPROVED', DATE_SUB(@seed_now, INTERVAL 15 DAY), DATE_SUB(@seed_now, INTERVAL 14 DAY), @u_ho_ha, DATE_SUB(@seed_now, INTERVAL 12 DAY), 'HO', 'HO duyệt request liên cơ sở.', 0, DATE_SUB(@seed_now, INTERVAL 15 DAY), @v_lee, @seed_now, @u_ho_ha),
  (@vr_rejected_single, 'VR-RS-005', @v_smith, @p_green, 'Emily Smith', 'Tổ chức đối tác quốc tế', 'Điều phối đoàn khách', '0900000000', 'emily.smith@greentech.example', 'Đoàn GreenTech Asia đề xuất lịch gấp', 'SINGLE_CAMPUS', 'Trao đổi hợp tác, tham quan campus và làm việc với các phòng ban.', 'Làm việc với IC Office, tham quan khu học tập, phòng lab và trao đổi hợp tác.', 3, JSON_ARRAY(JSON_OBJECT('full_name','Điều phối viên đoàn','role','Coordinator')), JSON_OBJECT('full_name','Emily Smith','organization','Tổ chức đối tác quốc tế','phone','0900000000','email','emily.smith@greentech.example'), 'EN', NULL, 'Đoàn tự túc xe 16 chỗ.', 'Vui lòng hỗ trợ bảng chào mừng và phòng họp.', 'REJECTED', DATE_SUB(@seed_now, INTERVAL 20 DAY), DATE_SUB(@seed_now, INTERVAL 19 DAY), @u_stafflead_hcm, DATE_SUB(@seed_now, INTERVAL 17 DAY), 'STAFF_LEADER', 'Ngày đề xuất quá sát.', 0, DATE_SUB(@seed_now, INTERVAL 20 DAY), @v_smith, @seed_now, @u_stafflead_hcm),
  (@vr_rejected_multi, 'VR-RM-006', @v_tanaka, @p_seoul, 'Tanaka Aoi', 'Tổ chức đối tác quốc tế', 'Điều phối đoàn khách', '0900000000', 'aoi.tanaka@kyoto-global.example', 'Đoàn học tập liên cơ sở chưa đủ hồ sơ', 'MULTI_CAMPUS', 'Trao đổi hợp tác, tham quan campus và làm việc với các phòng ban.', 'Làm việc với IC Office, tham quan khu học tập, phòng lab và trao đổi hợp tác.', 4, JSON_ARRAY(JSON_OBJECT('full_name','Điều phối viên đoàn','role','Coordinator')), JSON_OBJECT('full_name','Tanaka Aoi','organization','Tổ chức đối tác quốc tế','phone','0900000000','email','aoi.tanaka@kyoto-global.example'), 'EN', NULL, 'Đoàn tự túc xe 16 chỗ.', 'Vui lòng hỗ trợ bảng chào mừng và phòng họp.', 'REJECTED', DATE_SUB(@seed_now, INTERVAL 35 DAY), DATE_SUB(@seed_now, INTERVAL 34 DAY), @u_ho_linh, DATE_SUB(@seed_now, INTERVAL 32 DAY), 'HO', 'Thiếu danh sách thành viên.', 0, DATE_SUB(@seed_now, INTERVAL 35 DAY), @v_tanaka, @seed_now, @u_ho_linh),
  (@vr_cancelled, 'VR-CN-007', @v_nguyen_no_dau, @p_ministry, 'Nguyen Van Nam', 'Tổ chức đối tác quốc tế', 'Điều phối đoàn khách', '0900000000', 'nguyen.van.nam@partner.example', 'Đoàn IED thay đổi kế hoạch công tác', 'SINGLE_CAMPUS', 'Trao đổi hợp tác, tham quan campus và làm việc với các phòng ban.', 'Làm việc với IC Office, tham quan khu học tập, phòng lab và trao đổi hợp tác.', 3, JSON_ARRAY(JSON_OBJECT('full_name','Điều phối viên đoàn','role','Coordinator')), JSON_OBJECT('full_name','Nguyen Van Nam','organization','Tổ chức đối tác quốc tế','phone','0900000000','email','nguyen.van.nam@partner.example'), 'EN', NULL, 'Đoàn tự túc xe 16 chỗ.', 'Vui lòng hỗ trợ bảng chào mừng và phòng họp.', 'CANCELLED', DATE_SUB(@seed_now, INTERVAL 45 DAY), DATE_SUB(@seed_now, INTERVAL 44 DAY), NULL, DATE_SUB(@seed_now, INTERVAL 42 DAY), 'SYSTEM', 'Visitor hủy do đổi lịch bay.', 0, DATE_SUB(@seed_now, INTERVAL 45 DAY), @v_nguyen_no_dau, @seed_now, NULL),
  (@vr_after_visit, 'VR-AV-008', @v_short_name, @p_ministry, 'An', 'Tổ chức đối tác quốc tế', 'Điều phối đoàn khách', '0900000000', 'an.short@partner.example', 'Đoàn chuyên đề tuyển sinh quốc tế đã hoàn tất', 'SINGLE_CAMPUS', 'Trao đổi hợp tác, tham quan campus và làm việc với các phòng ban.', 'Làm việc với IC Office, tham quan khu học tập, phòng lab và trao đổi hợp tác.', 3, JSON_ARRAY(JSON_OBJECT('full_name','Điều phối viên đoàn','role','Coordinator')), JSON_OBJECT('full_name','An','organization','Tổ chức đối tác quốc tế','phone','0900000000','email','an.short@partner.example'), 'EN', NULL, 'Đoàn tự túc xe 16 chỗ.', 'Vui lòng hỗ trợ bảng chào mừng và phòng họp.', 'APPROVED', DATE_SUB(@seed_now, INTERVAL 22 DAY), DATE_SUB(@seed_now, INTERVAL 21 DAY), @u_stafflead_ct, DATE_SUB(@seed_now, INTERVAL 20 DAY), 'STAFF_LEADER', 'Đang tổng hợp biên bản.', 0, DATE_SUB(@seed_now, INTERVAL 22 DAY), @v_short_name, @seed_now, @u_stafflead_ct),
  (@vr_closed, 'VR-CL-009', @v_long_name, @p_seoul, 'Nguyễn Thị Minh Anh Phương Khánh Linh Hoàng Bảo Trân Quốc Việt', 'Tổ chức đối tác quốc tế', 'Điều phối đoàn khách', '0900000000', 'long.name.visitor@partner.example', 'Đoàn nghiên cứu AI đã đóng hồ sơ', 'SINGLE_CAMPUS', 'Trao đổi hợp tác, tham quan campus và làm việc với các phòng ban.', 'Làm việc với IC Office, tham quan khu học tập, phòng lab và trao đổi hợp tác.', 3, JSON_ARRAY(JSON_OBJECT('full_name','Điều phối viên đoàn','role','Coordinator')), JSON_OBJECT('full_name','Nguyễn Thị Minh Anh Phương Khánh Linh Hoàng Bảo Trân Quốc Việt','organization','Tổ chức đối tác quốc tế','phone','0900000000','email','long.name.visitor@partner.example'), 'EN', NULL, 'Đoàn tự túc xe 16 chỗ.', 'Vui lòng hỗ trợ bảng chào mừng và phòng họp.', 'APPROVED', DATE_SUB(@seed_now, INTERVAL 50 DAY), DATE_SUB(@seed_now, INTERVAL 49 DAY), @u_stafflead_hn, DATE_SUB(@seed_now, INTERVAL 48 DAY), 'STAFF_LEADER', 'Hồ sơ đã hoàn tất.', 0, DATE_SUB(@seed_now, INTERVAL 50 DAY), @v_long_name, @seed_now, @u_stafflead_hn),
  (@vr_cancelled_instance, 'VR-CI-010', @v_smith, @p_green, 'Emily Smith', 'Tổ chức đối tác quốc tế', 'Điều phối đoàn khách', '0900000000', 'emily.smith@greentech.example', 'Đoàn GreenTech chỉ giữ lịch TP.HCM', 'MULTI_CAMPUS', 'Trao đổi hợp tác, tham quan campus và làm việc với các phòng ban.', 'Làm việc với IC Office, tham quan khu học tập, phòng lab và trao đổi hợp tác.', 4, JSON_ARRAY(JSON_OBJECT('full_name','Điều phối viên đoàn','role','Coordinator')), JSON_OBJECT('full_name','Emily Smith','organization','Tổ chức đối tác quốc tế','phone','0900000000','email','emily.smith@greentech.example'), 'EN', NULL, 'Đoàn tự túc xe 16 chỗ.', 'Vui lòng hỗ trợ bảng chào mừng và phòng họp.', 'APPROVED', DATE_SUB(@seed_now, INTERVAL 40 DAY), DATE_SUB(@seed_now, INTERVAL 39 DAY), @u_ho_ha, DATE_SUB(@seed_now, INTERVAL 37 DAY), 'HO', 'Duyệt liên cơ sở; hủy instance Hà Nội.', 0, DATE_SUB(@seed_now, INTERVAL 40 DAY), @v_smith, @seed_now, @u_ho_ha),
  (@vr_assigned_only, 'VR-AO-011', @v_lee, @p_seoul, 'Lee Joon Ho', 'Tổ chức đối tác quốc tế', 'Điều phối đoàn khách', '0900000000', 'lee.joonho@seoultech.example', 'Đoàn SeoulTech chờ host chốt kế hoạch', 'SINGLE_CAMPUS', 'Trao đổi hợp tác, tham quan campus và làm việc với các phòng ban.', 'Làm việc với IC Office, tham quan khu học tập, phòng lab và trao đổi hợp tác.', 3, JSON_ARRAY(JSON_OBJECT('full_name','Điều phối viên đoàn','role','Coordinator')), JSON_OBJECT('full_name','Lee Joon Ho','organization','Tổ chức đối tác quốc tế','phone','0900000000','email','lee.joonho@seoultech.example'), 'EN', NULL, 'Đoàn tự túc xe 16 chỗ.', 'Vui lòng hỗ trợ bảng chào mừng và phòng họp.', 'APPROVED', DATE_SUB(@seed_now, INTERVAL 26 DAY), DATE_SUB(@seed_now, INTERVAL 25 DAY), @u_stafflead_dn, DATE_SUB(@seed_now, INTERVAL 23 DAY), 'STAFF_LEADER', 'Đã gán host.', 0, DATE_SUB(@seed_now, INTERVAL 26 DAY), @v_lee, @seed_now, @u_stafflead_dn),
  (@vr_hist_01, 'VR-HIST-001', @v_kim, @p_seoul, 'Kim Min Seo', 'Tổ chức đối tác quốc tế', 'Điều phối đoàn khách', '0900000000', 'kim.minseo@seoultech.example', 'Seoul Future University - chuyến thăm campus HN', 'SINGLE_CAMPUS', 'Trao đổi hợp tác, tham quan campus và làm việc với các phòng ban.', 'Làm việc với IC Office, tham quan khu học tập, phòng lab và trao đổi hợp tác.', 3, JSON_ARRAY(JSON_OBJECT('full_name','Điều phối viên đoàn','role','Coordinator')), JSON_OBJECT('full_name','Kim Min Seo','organization','Tổ chức đối tác quốc tế','phone','0900000000','email','kim.minseo@seoultech.example'), 'EN', NULL, 'Đoàn tự túc xe 16 chỗ.', 'Vui lòng hỗ trợ bảng chào mừng và phòng họp.', 'APPROVED', DATE_SUB(@seed_now, INTERVAL 28 DAY), DATE_SUB(@seed_now, INTERVAL 27 DAY), @u_stafflead_hn, DATE_SUB(@seed_now, INTERVAL 26 DAY), 'STAFF_LEADER', 'Hồ sơ lịch sử phục vụ dashboard và báo cáo.', 0, DATE_SUB(@seed_now, INTERVAL 28 DAY), @v_kim, @seed_now, @u_stafflead_hn),
  (@vr_hist_02, 'VR-HIST-002', @v_smith, @p_green, 'Emily Smith', 'Tổ chức đối tác quốc tế', 'Điều phối đoàn khách', '0900000000', 'emily.smith@greentech.example', 'GreenTech Asia Pte. Ltd. - chuyến thăm campus HCM', 'SINGLE_CAMPUS', 'Trao đổi hợp tác, tham quan campus và làm việc với các phòng ban.', 'Làm việc với IC Office, tham quan khu học tập, phòng lab và trao đổi hợp tác.', 3, JSON_ARRAY(JSON_OBJECT('full_name','Điều phối viên đoàn','role','Coordinator')), JSON_OBJECT('full_name','Emily Smith','organization','Tổ chức đối tác quốc tế','phone','0900000000','email','emily.smith@greentech.example'), 'EN', NULL, 'Đoàn tự túc xe 16 chỗ.', 'Vui lòng hỗ trợ bảng chào mừng và phòng họp.', 'APPROVED', DATE_SUB(@seed_now, INTERVAL 36 DAY), DATE_SUB(@seed_now, INTERVAL 35 DAY), @u_stafflead_hcm, DATE_SUB(@seed_now, INTERVAL 34 DAY), 'STAFF_LEADER', 'Hồ sơ lịch sử phục vụ dashboard và báo cáo.', 0, DATE_SUB(@seed_now, INTERVAL 36 DAY), @v_smith, @seed_now, @u_stafflead_hcm),
  (@vr_hist_03, 'VR-HIST-003', @v_nguyen_no_dau, @p_ministry, 'Nguyen Van Nam', 'Tổ chức đối tác quốc tế', 'Điều phối đoàn khách', '0900000000', 'nguyen.van.nam@partner.example', 'Viện Phát triển Giáo dục Quốc tế - chuyến thăm campus DN', 'SINGLE_CAMPUS', 'Trao đổi hợp tác, tham quan campus và làm việc với các phòng ban.', 'Làm việc với IC Office, tham quan khu học tập, phòng lab và trao đổi hợp tác.', 3, JSON_ARRAY(JSON_OBJECT('full_name','Điều phối viên đoàn','role','Coordinator')), JSON_OBJECT('full_name','Nguyen Van Nam','organization','Tổ chức đối tác quốc tế','phone','0900000000','email','nguyen.van.nam@partner.example'), 'EN', NULL, 'Đoàn tự túc xe 16 chỗ.', 'Vui lòng hỗ trợ bảng chào mừng và phòng họp.', 'REJECTED', DATE_SUB(@seed_now, INTERVAL 44 DAY), DATE_SUB(@seed_now, INTERVAL 43 DAY), @u_stafflead_dn, DATE_SUB(@seed_now, INTERVAL 42 DAY), 'STAFF_LEADER', 'Hồ sơ lịch sử phục vụ dashboard và báo cáo.', 0, DATE_SUB(@seed_now, INTERVAL 44 DAY), @v_nguyen_no_dau, @seed_now, @u_stafflead_dn),
  (@vr_hist_04, 'VR-HIST-004', @v_lee, @p_asean, 'Lee Joon Ho', 'Tổ chức đối tác quốc tế', 'Điều phối đoàn khách', '0900000000', 'lee.joonho@seoultech.example', 'ASEAN Future Skills Foundation - chuyến thăm campus CT', 'SINGLE_CAMPUS', 'Trao đổi hợp tác, tham quan campus và làm việc với các phòng ban.', 'Làm việc với IC Office, tham quan khu học tập, phòng lab và trao đổi hợp tác.', 3, JSON_ARRAY(JSON_OBJECT('full_name','Điều phối viên đoàn','role','Coordinator')), JSON_OBJECT('full_name','Lee Joon Ho','organization','Tổ chức đối tác quốc tế','phone','0900000000','email','lee.joonho@seoultech.example'), 'EN', NULL, 'Đoàn tự túc xe 16 chỗ.', 'Vui lòng hỗ trợ bảng chào mừng và phòng họp.', 'CANCELLED', DATE_SUB(@seed_now, INTERVAL 52 DAY), DATE_SUB(@seed_now, INTERVAL 51 DAY), NULL, DATE_SUB(@seed_now, INTERVAL 50 DAY), 'SYSTEM', 'Hồ sơ lịch sử phục vụ dashboard và báo cáo.', 0, DATE_SUB(@seed_now, INTERVAL 52 DAY), @v_lee, @seed_now, NULL),
  (@vr_hist_05, 'VR-HIST-005', @v_kim, @p_seoul, 'Kim Min Seo', 'Tổ chức đối tác quốc tế', 'Điều phối đoàn khách', '0900000000', 'kim.minseo@seoultech.example', 'Seoul Future University - chuyến thăm campus QN', 'SINGLE_CAMPUS', 'Trao đổi hợp tác, tham quan campus và làm việc với các phòng ban.', 'Làm việc với IC Office, tham quan khu học tập, phòng lab và trao đổi hợp tác.', 3, JSON_ARRAY(JSON_OBJECT('full_name','Điều phối viên đoàn','role','Coordinator')), JSON_OBJECT('full_name','Kim Min Seo','organization','Tổ chức đối tác quốc tế','phone','0900000000','email','kim.minseo@seoultech.example'), 'EN', NULL, 'Đoàn tự túc xe 16 chỗ.', 'Vui lòng hỗ trợ bảng chào mừng và phòng họp.', 'PENDING_APPROVAL', DATE_SUB(@seed_now, INTERVAL 60 DAY), DATE_SUB(@seed_now, INTERVAL 59 DAY), NULL, NULL, NULL, 'Hồ sơ lịch sử phục vụ dashboard và báo cáo.', 0, DATE_SUB(@seed_now, INTERVAL 60 DAY), @v_kim, @seed_now, NULL),
  (@vr_hist_06, 'VR-HIST-006', @v_smith, @p_green, 'Emily Smith', 'Tổ chức đối tác quốc tế', 'Điều phối đoàn khách', '0900000000', 'emily.smith@greentech.example', 'GreenTech Asia Pte. Ltd. - chuyến thăm campus HN', 'SINGLE_CAMPUS', 'Trao đổi hợp tác, tham quan campus và làm việc với các phòng ban.', 'Làm việc với IC Office, tham quan khu học tập, phòng lab và trao đổi hợp tác.', 3, JSON_ARRAY(JSON_OBJECT('full_name','Điều phối viên đoàn','role','Coordinator')), JSON_OBJECT('full_name','Emily Smith','organization','Tổ chức đối tác quốc tế','phone','0900000000','email','emily.smith@greentech.example'), 'EN', NULL, 'Đoàn tự túc xe 16 chỗ.', 'Vui lòng hỗ trợ bảng chào mừng và phòng họp.', 'APPROVED', DATE_SUB(@seed_now, INTERVAL 68 DAY), DATE_SUB(@seed_now, INTERVAL 67 DAY), @u_stafflead_hn, DATE_SUB(@seed_now, INTERVAL 66 DAY), 'STAFF_LEADER', 'Hồ sơ lịch sử phục vụ dashboard và báo cáo.', 0, DATE_SUB(@seed_now, INTERVAL 68 DAY), @v_smith, @seed_now, @u_stafflead_hn),
  (@vr_hist_07, 'VR-HIST-007', @v_nguyen_no_dau, @p_ministry, 'Nguyen Van Nam', 'Tổ chức đối tác quốc tế', 'Điều phối đoàn khách', '0900000000', 'nguyen.van.nam@partner.example', 'Viện Phát triển Giáo dục Quốc tế - chuyến thăm campus HCM', 'SINGLE_CAMPUS', 'Trao đổi hợp tác, tham quan campus và làm việc với các phòng ban.', 'Làm việc với IC Office, tham quan khu học tập, phòng lab và trao đổi hợp tác.', 3, JSON_ARRAY(JSON_OBJECT('full_name','Điều phối viên đoàn','role','Coordinator')), JSON_OBJECT('full_name','Nguyen Van Nam','organization','Tổ chức đối tác quốc tế','phone','0900000000','email','nguyen.van.nam@partner.example'), 'EN', NULL, 'Đoàn tự túc xe 16 chỗ.', 'Vui lòng hỗ trợ bảng chào mừng và phòng họp.', 'APPROVED', DATE_SUB(@seed_now, INTERVAL 76 DAY), DATE_SUB(@seed_now, INTERVAL 75 DAY), @u_stafflead_hcm, DATE_SUB(@seed_now, INTERVAL 74 DAY), 'STAFF_LEADER', 'Hồ sơ lịch sử phục vụ dashboard và báo cáo.', 0, DATE_SUB(@seed_now, INTERVAL 76 DAY), @v_nguyen_no_dau, @seed_now, @u_stafflead_hcm),
  (@vr_hist_08, 'VR-HIST-008', @v_lee, @p_asean, 'Lee Joon Ho', 'Tổ chức đối tác quốc tế', 'Điều phối đoàn khách', '0900000000', 'lee.joonho@seoultech.example', 'ASEAN Future Skills Foundation - chuyến thăm campus DN', 'SINGLE_CAMPUS', 'Trao đổi hợp tác, tham quan campus và làm việc với các phòng ban.', 'Làm việc với IC Office, tham quan khu học tập, phòng lab và trao đổi hợp tác.', 3, JSON_ARRAY(JSON_OBJECT('full_name','Điều phối viên đoàn','role','Coordinator')), JSON_OBJECT('full_name','Lee Joon Ho','organization','Tổ chức đối tác quốc tế','phone','0900000000','email','lee.joonho@seoultech.example'), 'EN', NULL, 'Đoàn tự túc xe 16 chỗ.', 'Vui lòng hỗ trợ bảng chào mừng và phòng họp.', 'REJECTED', DATE_SUB(@seed_now, INTERVAL 84 DAY), DATE_SUB(@seed_now, INTERVAL 83 DAY), @u_stafflead_dn, DATE_SUB(@seed_now, INTERVAL 82 DAY), 'STAFF_LEADER', 'Hồ sơ lịch sử phục vụ dashboard và báo cáo.', 0, DATE_SUB(@seed_now, INTERVAL 84 DAY), @v_lee, @seed_now, @u_stafflead_dn),
  (@vr_hist_09, 'VR-HIST-009', @v_kim, @p_seoul, 'Kim Min Seo', 'Tổ chức đối tác quốc tế', 'Điều phối đoàn khách', '0900000000', 'kim.minseo@seoultech.example', 'Seoul Future University - chuyến thăm campus CT', 'SINGLE_CAMPUS', 'Trao đổi hợp tác, tham quan campus và làm việc với các phòng ban.', 'Làm việc với IC Office, tham quan khu học tập, phòng lab và trao đổi hợp tác.', 3, JSON_ARRAY(JSON_OBJECT('full_name','Điều phối viên đoàn','role','Coordinator')), JSON_OBJECT('full_name','Kim Min Seo','organization','Tổ chức đối tác quốc tế','phone','0900000000','email','kim.minseo@seoultech.example'), 'EN', NULL, 'Đoàn tự túc xe 16 chỗ.', 'Vui lòng hỗ trợ bảng chào mừng và phòng họp.', 'CANCELLED', DATE_SUB(@seed_now, INTERVAL 92 DAY), DATE_SUB(@seed_now, INTERVAL 91 DAY), NULL, DATE_SUB(@seed_now, INTERVAL 90 DAY), 'SYSTEM', 'Hồ sơ lịch sử phục vụ dashboard và báo cáo.', 0, DATE_SUB(@seed_now, INTERVAL 92 DAY), @v_kim, @seed_now, NULL),
  (@vr_hist_10, 'VR-HIST-010', @v_smith, @p_green, 'Emily Smith', 'Tổ chức đối tác quốc tế', 'Điều phối đoàn khách', '0900000000', 'emily.smith@greentech.example', 'GreenTech Asia Pte. Ltd. - chuyến thăm campus QN', 'SINGLE_CAMPUS', 'Trao đổi hợp tác, tham quan campus và làm việc với các phòng ban.', 'Làm việc với IC Office, tham quan khu học tập, phòng lab và trao đổi hợp tác.', 3, JSON_ARRAY(JSON_OBJECT('full_name','Điều phối viên đoàn','role','Coordinator')), JSON_OBJECT('full_name','Emily Smith','organization','Tổ chức đối tác quốc tế','phone','0900000000','email','emily.smith@greentech.example'), 'EN', NULL, 'Đoàn tự túc xe 16 chỗ.', 'Vui lòng hỗ trợ bảng chào mừng và phòng họp.', 'PENDING_APPROVAL', DATE_SUB(@seed_now, INTERVAL 100 DAY), DATE_SUB(@seed_now, INTERVAL 99 DAY), NULL, NULL, NULL, 'Hồ sơ lịch sử phục vụ dashboard và báo cáo.', 0, DATE_SUB(@seed_now, INTERVAL 100 DAY), @v_smith, @seed_now, NULL),
  (@vr_hist_11, 'VR-HIST-011', @v_nguyen_no_dau, @p_ministry, 'Nguyen Van Nam', 'Tổ chức đối tác quốc tế', 'Điều phối đoàn khách', '0900000000', 'nguyen.van.nam@partner.example', 'Viện Phát triển Giáo dục Quốc tế - chuyến thăm campus HN', 'SINGLE_CAMPUS', 'Trao đổi hợp tác, tham quan campus và làm việc với các phòng ban.', 'Làm việc với IC Office, tham quan khu học tập, phòng lab và trao đổi hợp tác.', 3, JSON_ARRAY(JSON_OBJECT('full_name','Điều phối viên đoàn','role','Coordinator')), JSON_OBJECT('full_name','Nguyen Van Nam','organization','Tổ chức đối tác quốc tế','phone','0900000000','email','nguyen.van.nam@partner.example'), 'EN', NULL, 'Đoàn tự túc xe 16 chỗ.', 'Vui lòng hỗ trợ bảng chào mừng và phòng họp.', 'APPROVED', DATE_SUB(@seed_now, INTERVAL 108 DAY), DATE_SUB(@seed_now, INTERVAL 107 DAY), @u_stafflead_hn, DATE_SUB(@seed_now, INTERVAL 106 DAY), 'STAFF_LEADER', 'Hồ sơ lịch sử phục vụ dashboard và báo cáo.', 0, DATE_SUB(@seed_now, INTERVAL 108 DAY), @v_nguyen_no_dau, @seed_now, @u_stafflead_hn),
  (@vr_hist_12, 'VR-HIST-012', @v_lee, @p_asean, 'Lee Joon Ho', 'Tổ chức đối tác quốc tế', 'Điều phối đoàn khách', '0900000000', 'lee.joonho@seoultech.example', 'ASEAN Future Skills Foundation - chuyến thăm campus HCM', 'SINGLE_CAMPUS', 'Trao đổi hợp tác, tham quan campus và làm việc với các phòng ban.', 'Làm việc với IC Office, tham quan khu học tập, phòng lab và trao đổi hợp tác.', 3, JSON_ARRAY(JSON_OBJECT('full_name','Điều phối viên đoàn','role','Coordinator')), JSON_OBJECT('full_name','Lee Joon Ho','organization','Tổ chức đối tác quốc tế','phone','0900000000','email','lee.joonho@seoultech.example'), 'EN', NULL, 'Đoàn tự túc xe 16 chỗ.', 'Vui lòng hỗ trợ bảng chào mừng và phòng họp.', 'APPROVED', DATE_SUB(@seed_now, INTERVAL 116 DAY), DATE_SUB(@seed_now, INTERVAL 115 DAY), @u_stafflead_hcm, DATE_SUB(@seed_now, INTERVAL 114 DAY), 'STAFF_LEADER', 'Hồ sơ lịch sử phục vụ dashboard và báo cáo.', 0, DATE_SUB(@seed_now, INTERVAL 116 DAY), @v_lee, @seed_now, @u_stafflead_hcm),
  (@vr_hist_13, 'VR-HIST-013', @v_kim, @p_seoul, 'Kim Min Seo', 'Tổ chức đối tác quốc tế', 'Điều phối đoàn khách', '0900000000', 'kim.minseo@seoultech.example', 'Seoul Future University - chuyến thăm campus DN', 'SINGLE_CAMPUS', 'Trao đổi hợp tác, tham quan campus và làm việc với các phòng ban.', 'Làm việc với IC Office, tham quan khu học tập, phòng lab và trao đổi hợp tác.', 3, JSON_ARRAY(JSON_OBJECT('full_name','Điều phối viên đoàn','role','Coordinator')), JSON_OBJECT('full_name','Kim Min Seo','organization','Tổ chức đối tác quốc tế','phone','0900000000','email','kim.minseo@seoultech.example'), 'EN', NULL, 'Đoàn tự túc xe 16 chỗ.', 'Vui lòng hỗ trợ bảng chào mừng và phòng họp.', 'REJECTED', DATE_SUB(@seed_now, INTERVAL 124 DAY), DATE_SUB(@seed_now, INTERVAL 123 DAY), @u_stafflead_dn, DATE_SUB(@seed_now, INTERVAL 122 DAY), 'STAFF_LEADER', 'Hồ sơ lịch sử phục vụ dashboard và báo cáo.', 0, DATE_SUB(@seed_now, INTERVAL 124 DAY), @v_kim, @seed_now, @u_stafflead_dn),
  (@vr_hist_14, 'VR-HIST-014', @v_smith, @p_green, 'Emily Smith', 'Tổ chức đối tác quốc tế', 'Điều phối đoàn khách', '0900000000', 'emily.smith@greentech.example', 'GreenTech Asia Pte. Ltd. - chuyến thăm campus CT', 'SINGLE_CAMPUS', 'Trao đổi hợp tác, tham quan campus và làm việc với các phòng ban.', 'Làm việc với IC Office, tham quan khu học tập, phòng lab và trao đổi hợp tác.', 3, JSON_ARRAY(JSON_OBJECT('full_name','Điều phối viên đoàn','role','Coordinator')), JSON_OBJECT('full_name','Emily Smith','organization','Tổ chức đối tác quốc tế','phone','0900000000','email','emily.smith@greentech.example'), 'EN', NULL, 'Đoàn tự túc xe 16 chỗ.', 'Vui lòng hỗ trợ bảng chào mừng và phòng họp.', 'CANCELLED', DATE_SUB(@seed_now, INTERVAL 132 DAY), DATE_SUB(@seed_now, INTERVAL 131 DAY), NULL, DATE_SUB(@seed_now, INTERVAL 130 DAY), 'SYSTEM', 'Hồ sơ lịch sử phục vụ dashboard và báo cáo.', 0, DATE_SUB(@seed_now, INTERVAL 132 DAY), @v_smith, @seed_now, NULL),
  (@vr_hist_15, 'VR-HIST-015', @v_nguyen_no_dau, @p_ministry, 'Nguyen Van Nam', 'Tổ chức đối tác quốc tế', 'Điều phối đoàn khách', '0900000000', 'nguyen.van.nam@partner.example', 'Viện Phát triển Giáo dục Quốc tế - chuyến thăm campus QN', 'SINGLE_CAMPUS', 'Trao đổi hợp tác, tham quan campus và làm việc với các phòng ban.', 'Làm việc với IC Office, tham quan khu học tập, phòng lab và trao đổi hợp tác.', 3, JSON_ARRAY(JSON_OBJECT('full_name','Điều phối viên đoàn','role','Coordinator')), JSON_OBJECT('full_name','Nguyen Van Nam','organization','Tổ chức đối tác quốc tế','phone','0900000000','email','nguyen.van.nam@partner.example'), 'EN', NULL, 'Đoàn tự túc xe 16 chỗ.', 'Vui lòng hỗ trợ bảng chào mừng và phòng họp.', 'PENDING_APPROVAL', DATE_SUB(@seed_now, INTERVAL 140 DAY), DATE_SUB(@seed_now, INTERVAL 139 DAY), NULL, NULL, NULL, 'Hồ sơ lịch sử phục vụ dashboard và báo cáo.', 0, DATE_SUB(@seed_now, INTERVAL 140 DAY), @v_nguyen_no_dau, @seed_now, NULL),
  (@vr_hist_16, 'VR-HIST-016', @v_lee, @p_asean, 'Lee Joon Ho', 'Tổ chức đối tác quốc tế', 'Điều phối đoàn khách', '0900000000', 'lee.joonho@seoultech.example', 'ASEAN Future Skills Foundation - chuyến thăm campus HN', 'SINGLE_CAMPUS', 'Trao đổi hợp tác, tham quan campus và làm việc với các phòng ban.', 'Làm việc với IC Office, tham quan khu học tập, phòng lab và trao đổi hợp tác.', 3, JSON_ARRAY(JSON_OBJECT('full_name','Điều phối viên đoàn','role','Coordinator')), JSON_OBJECT('full_name','Lee Joon Ho','organization','Tổ chức đối tác quốc tế','phone','0900000000','email','lee.joonho@seoultech.example'), 'EN', NULL, 'Đoàn tự túc xe 16 chỗ.', 'Vui lòng hỗ trợ bảng chào mừng và phòng họp.', 'APPROVED', DATE_SUB(@seed_now, INTERVAL 148 DAY), DATE_SUB(@seed_now, INTERVAL 147 DAY), @u_stafflead_hn, DATE_SUB(@seed_now, INTERVAL 146 DAY), 'STAFF_LEADER', 'Hồ sơ lịch sử phục vụ dashboard và báo cáo.', 0, DATE_SUB(@seed_now, INTERVAL 148 DAY), @v_lee, @seed_now, @u_stafflead_hn),
  (@vr_hist_17, 'VR-HIST-017', @v_kim, @p_seoul, 'Kim Min Seo', 'Tổ chức đối tác quốc tế', 'Điều phối đoàn khách', '0900000000', 'kim.minseo@seoultech.example', 'Seoul Future University - chuyến thăm campus HCM', 'SINGLE_CAMPUS', 'Trao đổi hợp tác, tham quan campus và làm việc với các phòng ban.', 'Làm việc với IC Office, tham quan khu học tập, phòng lab và trao đổi hợp tác.', 3, JSON_ARRAY(JSON_OBJECT('full_name','Điều phối viên đoàn','role','Coordinator')), JSON_OBJECT('full_name','Kim Min Seo','organization','Tổ chức đối tác quốc tế','phone','0900000000','email','kim.minseo@seoultech.example'), 'EN', NULL, 'Đoàn tự túc xe 16 chỗ.', 'Vui lòng hỗ trợ bảng chào mừng và phòng họp.', 'APPROVED', DATE_SUB(@seed_now, INTERVAL 156 DAY), DATE_SUB(@seed_now, INTERVAL 155 DAY), @u_stafflead_hcm, DATE_SUB(@seed_now, INTERVAL 154 DAY), 'STAFF_LEADER', 'Hồ sơ lịch sử phục vụ dashboard và báo cáo.', 0, DATE_SUB(@seed_now, INTERVAL 156 DAY), @v_kim, @seed_now, @u_stafflead_hcm),
  (@vr_hist_18, 'VR-HIST-018', @v_smith, @p_green, 'Emily Smith', 'Tổ chức đối tác quốc tế', 'Điều phối đoàn khách', '0900000000', 'emily.smith@greentech.example', 'GreenTech Asia Pte. Ltd. - chuyến thăm campus DN', 'SINGLE_CAMPUS', 'Trao đổi hợp tác, tham quan campus và làm việc với các phòng ban.', 'Làm việc với IC Office, tham quan khu học tập, phòng lab và trao đổi hợp tác.', 3, JSON_ARRAY(JSON_OBJECT('full_name','Điều phối viên đoàn','role','Coordinator')), JSON_OBJECT('full_name','Emily Smith','organization','Tổ chức đối tác quốc tế','phone','0900000000','email','emily.smith@greentech.example'), 'EN', NULL, 'Đoàn tự túc xe 16 chỗ.', 'Vui lòng hỗ trợ bảng chào mừng và phòng họp.', 'REJECTED', DATE_SUB(@seed_now, INTERVAL 164 DAY), DATE_SUB(@seed_now, INTERVAL 163 DAY), @u_stafflead_dn, DATE_SUB(@seed_now, INTERVAL 162 DAY), 'STAFF_LEADER', 'Hồ sơ lịch sử phục vụ dashboard và báo cáo.', 0, DATE_SUB(@seed_now, INTERVAL 164 DAY), @v_smith, @seed_now, @u_stafflead_dn),
  (@vr_hist_19, 'VR-HIST-019', @v_nguyen_no_dau, @p_ministry, 'Nguyen Van Nam', 'Tổ chức đối tác quốc tế', 'Điều phối đoàn khách', '0900000000', 'nguyen.van.nam@partner.example', 'Viện Phát triển Giáo dục Quốc tế - chuyến thăm campus CT', 'SINGLE_CAMPUS', 'Trao đổi hợp tác, tham quan campus và làm việc với các phòng ban.', 'Làm việc với IC Office, tham quan khu học tập, phòng lab và trao đổi hợp tác.', 3, JSON_ARRAY(JSON_OBJECT('full_name','Điều phối viên đoàn','role','Coordinator')), JSON_OBJECT('full_name','Nguyen Van Nam','organization','Tổ chức đối tác quốc tế','phone','0900000000','email','nguyen.van.nam@partner.example'), 'EN', NULL, 'Đoàn tự túc xe 16 chỗ.', 'Vui lòng hỗ trợ bảng chào mừng và phòng họp.', 'CANCELLED', DATE_SUB(@seed_now, INTERVAL 172 DAY), DATE_SUB(@seed_now, INTERVAL 171 DAY), NULL, DATE_SUB(@seed_now, INTERVAL 170 DAY), 'SYSTEM', 'Hồ sơ lịch sử phục vụ dashboard và báo cáo.', 0, DATE_SUB(@seed_now, INTERVAL 172 DAY), @v_nguyen_no_dau, @seed_now, NULL),
  (@vr_hist_20, 'VR-HIST-020', @v_lee, @p_asean, 'Lee Joon Ho', 'Tổ chức đối tác quốc tế', 'Điều phối đoàn khách', '0900000000', 'lee.joonho@seoultech.example', 'ASEAN Future Skills Foundation - chuyến thăm campus QN', 'SINGLE_CAMPUS', 'Trao đổi hợp tác, tham quan campus và làm việc với các phòng ban.', 'Làm việc với IC Office, tham quan khu học tập, phòng lab và trao đổi hợp tác.', 3, JSON_ARRAY(JSON_OBJECT('full_name','Điều phối viên đoàn','role','Coordinator')), JSON_OBJECT('full_name','Lee Joon Ho','organization','Tổ chức đối tác quốc tế','phone','0900000000','email','lee.joonho@seoultech.example'), 'EN', NULL, 'Đoàn tự túc xe 16 chỗ.', 'Vui lòng hỗ trợ bảng chào mừng và phòng họp.', 'PENDING_APPROVAL', DATE_SUB(@seed_now, INTERVAL 180 DAY), DATE_SUB(@seed_now, INTERVAL 179 DAY), NULL, NULL, NULL, 'Hồ sơ lịch sử phục vụ dashboard và báo cáo.', 0, DATE_SUB(@seed_now, INTERVAL 180 DAY), @v_lee, @seed_now, NULL),
  (@vr_hist_21, 'VR-HIST-021', @v_kim, @p_seoul, 'Kim Min Seo', 'Tổ chức đối tác quốc tế', 'Điều phối đoàn khách', '0900000000', 'kim.minseo@seoultech.example', 'Seoul Future University - chuyến thăm campus HN', 'SINGLE_CAMPUS', 'Trao đổi hợp tác, tham quan campus và làm việc với các phòng ban.', 'Làm việc với IC Office, tham quan khu học tập, phòng lab và trao đổi hợp tác.', 3, JSON_ARRAY(JSON_OBJECT('full_name','Điều phối viên đoàn','role','Coordinator')), JSON_OBJECT('full_name','Kim Min Seo','organization','Tổ chức đối tác quốc tế','phone','0900000000','email','kim.minseo@seoultech.example'), 'EN', NULL, 'Đoàn tự túc xe 16 chỗ.', 'Vui lòng hỗ trợ bảng chào mừng và phòng họp.', 'APPROVED', DATE_SUB(@seed_now, INTERVAL 188 DAY), DATE_SUB(@seed_now, INTERVAL 187 DAY), @u_stafflead_hn, DATE_SUB(@seed_now, INTERVAL 186 DAY), 'STAFF_LEADER', 'Hồ sơ lịch sử phục vụ dashboard và báo cáo.', 0, DATE_SUB(@seed_now, INTERVAL 188 DAY), @v_kim, @seed_now, @u_stafflead_hn),
  (@vr_hist_22, 'VR-HIST-022', @v_smith, @p_green, 'Emily Smith', 'Tổ chức đối tác quốc tế', 'Điều phối đoàn khách', '0900000000', 'emily.smith@greentech.example', 'GreenTech Asia Pte. Ltd. - chuyến thăm campus HCM', 'SINGLE_CAMPUS', 'Trao đổi hợp tác, tham quan campus và làm việc với các phòng ban.', 'Làm việc với IC Office, tham quan khu học tập, phòng lab và trao đổi hợp tác.', 3, JSON_ARRAY(JSON_OBJECT('full_name','Điều phối viên đoàn','role','Coordinator')), JSON_OBJECT('full_name','Emily Smith','organization','Tổ chức đối tác quốc tế','phone','0900000000','email','emily.smith@greentech.example'), 'EN', NULL, 'Đoàn tự túc xe 16 chỗ.', 'Vui lòng hỗ trợ bảng chào mừng và phòng họp.', 'APPROVED', DATE_SUB(@seed_now, INTERVAL 196 DAY), DATE_SUB(@seed_now, INTERVAL 195 DAY), @u_stafflead_hcm, DATE_SUB(@seed_now, INTERVAL 194 DAY), 'STAFF_LEADER', 'Hồ sơ lịch sử phục vụ dashboard và báo cáo.', 0, DATE_SUB(@seed_now, INTERVAL 196 DAY), @v_smith, @seed_now, @u_stafflead_hcm),
  (@vr_hist_23, 'VR-HIST-023', @v_nguyen_no_dau, @p_ministry, 'Nguyen Van Nam', 'Tổ chức đối tác quốc tế', 'Điều phối đoàn khách', '0900000000', 'nguyen.van.nam@partner.example', 'Viện Phát triển Giáo dục Quốc tế - chuyến thăm campus DN', 'SINGLE_CAMPUS', 'Trao đổi hợp tác, tham quan campus và làm việc với các phòng ban.', 'Làm việc với IC Office, tham quan khu học tập, phòng lab và trao đổi hợp tác.', 3, JSON_ARRAY(JSON_OBJECT('full_name','Điều phối viên đoàn','role','Coordinator')), JSON_OBJECT('full_name','Nguyen Van Nam','organization','Tổ chức đối tác quốc tế','phone','0900000000','email','nguyen.van.nam@partner.example'), 'EN', NULL, 'Đoàn tự túc xe 16 chỗ.', 'Vui lòng hỗ trợ bảng chào mừng và phòng họp.', 'REJECTED', DATE_SUB(@seed_now, INTERVAL 204 DAY), DATE_SUB(@seed_now, INTERVAL 203 DAY), @u_stafflead_dn, DATE_SUB(@seed_now, INTERVAL 202 DAY), 'STAFF_LEADER', 'Hồ sơ lịch sử phục vụ dashboard và báo cáo.', 0, DATE_SUB(@seed_now, INTERVAL 204 DAY), @v_nguyen_no_dau, @seed_now, @u_stafflead_dn),
  (@vr_hist_24, 'VR-HIST-024', @v_lee, @p_asean, 'Lee Joon Ho', 'Tổ chức đối tác quốc tế', 'Điều phối đoàn khách', '0900000000', 'lee.joonho@seoultech.example', 'ASEAN Future Skills Foundation - chuyến thăm campus CT', 'SINGLE_CAMPUS', 'Trao đổi hợp tác, tham quan campus và làm việc với các phòng ban.', 'Làm việc với IC Office, tham quan khu học tập, phòng lab và trao đổi hợp tác.', 3, JSON_ARRAY(JSON_OBJECT('full_name','Điều phối viên đoàn','role','Coordinator')), JSON_OBJECT('full_name','Lee Joon Ho','organization','Tổ chức đối tác quốc tế','phone','0900000000','email','lee.joonho@seoultech.example'), 'EN', NULL, 'Đoàn tự túc xe 16 chỗ.', 'Vui lòng hỗ trợ bảng chào mừng và phòng họp.', 'CANCELLED', DATE_SUB(@seed_now, INTERVAL 212 DAY), DATE_SUB(@seed_now, INTERVAL 211 DAY), NULL, DATE_SUB(@seed_now, INTERVAL 210 DAY), 'SYSTEM', 'Hồ sơ lịch sử phục vụ dashboard và báo cáo.', 0, DATE_SUB(@seed_now, INTERVAL 212 DAY), @v_lee, @seed_now, NULL),
  (@vr_hist_25, 'VR-HIST-025', @v_kim, @p_seoul, 'Kim Min Seo', 'Tổ chức đối tác quốc tế', 'Điều phối đoàn khách', '0900000000', 'kim.minseo@seoultech.example', 'Seoul Future University - chuyến thăm campus QN', 'SINGLE_CAMPUS', 'Trao đổi hợp tác, tham quan campus và làm việc với các phòng ban.', 'Làm việc với IC Office, tham quan khu học tập, phòng lab và trao đổi hợp tác.', 3, JSON_ARRAY(JSON_OBJECT('full_name','Điều phối viên đoàn','role','Coordinator')), JSON_OBJECT('full_name','Kim Min Seo','organization','Tổ chức đối tác quốc tế','phone','0900000000','email','kim.minseo@seoultech.example'), 'EN', NULL, 'Đoàn tự túc xe 16 chỗ.', 'Vui lòng hỗ trợ bảng chào mừng và phòng họp.', 'PENDING_APPROVAL', DATE_SUB(@seed_now, INTERVAL 220 DAY), DATE_SUB(@seed_now, INTERVAL 219 DAY), NULL, NULL, NULL, 'Hồ sơ lịch sử phục vụ dashboard và báo cáo.', 0, DATE_SUB(@seed_now, INTERVAL 220 DAY), @v_kim, @seed_now, NULL),
  (@vr_hist_26, 'VR-HIST-026', @v_smith, @p_green, 'Emily Smith', 'Tổ chức đối tác quốc tế', 'Điều phối đoàn khách', '0900000000', 'emily.smith@greentech.example', 'GreenTech Asia Pte. Ltd. - chuyến thăm campus HN', 'SINGLE_CAMPUS', 'Trao đổi hợp tác, tham quan campus và làm việc với các phòng ban.', 'Làm việc với IC Office, tham quan khu học tập, phòng lab và trao đổi hợp tác.', 3, JSON_ARRAY(JSON_OBJECT('full_name','Điều phối viên đoàn','role','Coordinator')), JSON_OBJECT('full_name','Emily Smith','organization','Tổ chức đối tác quốc tế','phone','0900000000','email','emily.smith@greentech.example'), 'EN', NULL, 'Đoàn tự túc xe 16 chỗ.', 'Vui lòng hỗ trợ bảng chào mừng và phòng họp.', 'APPROVED', DATE_SUB(@seed_now, INTERVAL 228 DAY), DATE_SUB(@seed_now, INTERVAL 227 DAY), @u_stafflead_hn, DATE_SUB(@seed_now, INTERVAL 226 DAY), 'STAFF_LEADER', 'Hồ sơ lịch sử phục vụ dashboard và báo cáo.', 0, DATE_SUB(@seed_now, INTERVAL 228 DAY), @v_smith, @seed_now, @u_stafflead_hn),
  (@vr_hist_27, 'VR-HIST-027', @v_nguyen_no_dau, @p_ministry, 'Nguyen Van Nam', 'Tổ chức đối tác quốc tế', 'Điều phối đoàn khách', '0900000000', 'nguyen.van.nam@partner.example', 'Viện Phát triển Giáo dục Quốc tế - chuyến thăm campus HCM', 'SINGLE_CAMPUS', 'Trao đổi hợp tác, tham quan campus và làm việc với các phòng ban.', 'Làm việc với IC Office, tham quan khu học tập, phòng lab và trao đổi hợp tác.', 3, JSON_ARRAY(JSON_OBJECT('full_name','Điều phối viên đoàn','role','Coordinator')), JSON_OBJECT('full_name','Nguyen Van Nam','organization','Tổ chức đối tác quốc tế','phone','0900000000','email','nguyen.van.nam@partner.example'), 'EN', NULL, 'Đoàn tự túc xe 16 chỗ.', 'Vui lòng hỗ trợ bảng chào mừng và phòng họp.', 'APPROVED', DATE_SUB(@seed_now, INTERVAL 236 DAY), DATE_SUB(@seed_now, INTERVAL 235 DAY), @u_stafflead_hcm, DATE_SUB(@seed_now, INTERVAL 234 DAY), 'STAFF_LEADER', 'Hồ sơ lịch sử phục vụ dashboard và báo cáo.', 0, DATE_SUB(@seed_now, INTERVAL 236 DAY), @v_nguyen_no_dau, @seed_now, @u_stafflead_hcm),
  (@vr_hist_28, 'VR-HIST-028', @v_lee, @p_asean, 'Lee Joon Ho', 'Tổ chức đối tác quốc tế', 'Điều phối đoàn khách', '0900000000', 'lee.joonho@seoultech.example', 'ASEAN Future Skills Foundation - chuyến thăm campus DN', 'SINGLE_CAMPUS', 'Trao đổi hợp tác, tham quan campus và làm việc với các phòng ban.', 'Làm việc với IC Office, tham quan khu học tập, phòng lab và trao đổi hợp tác.', 3, JSON_ARRAY(JSON_OBJECT('full_name','Điều phối viên đoàn','role','Coordinator')), JSON_OBJECT('full_name','Lee Joon Ho','organization','Tổ chức đối tác quốc tế','phone','0900000000','email','lee.joonho@seoultech.example'), 'EN', NULL, 'Đoàn tự túc xe 16 chỗ.', 'Vui lòng hỗ trợ bảng chào mừng và phòng họp.', 'REJECTED', DATE_SUB(@seed_now, INTERVAL 244 DAY), DATE_SUB(@seed_now, INTERVAL 243 DAY), @u_stafflead_dn, DATE_SUB(@seed_now, INTERVAL 242 DAY), 'STAFF_LEADER', 'Hồ sơ lịch sử phục vụ dashboard và báo cáo.', 0, DATE_SUB(@seed_now, INTERVAL 244 DAY), @v_lee, @seed_now, @u_stafflead_dn),
  (@vr_hist_29, 'VR-HIST-029', @v_kim, @p_seoul, 'Kim Min Seo', 'Tổ chức đối tác quốc tế', 'Điều phối đoàn khách', '0900000000', 'kim.minseo@seoultech.example', 'Seoul Future University - chuyến thăm campus CT', 'SINGLE_CAMPUS', 'Trao đổi hợp tác, tham quan campus và làm việc với các phòng ban.', 'Làm việc với IC Office, tham quan khu học tập, phòng lab và trao đổi hợp tác.', 3, JSON_ARRAY(JSON_OBJECT('full_name','Điều phối viên đoàn','role','Coordinator')), JSON_OBJECT('full_name','Kim Min Seo','organization','Tổ chức đối tác quốc tế','phone','0900000000','email','kim.minseo@seoultech.example'), 'EN', NULL, 'Đoàn tự túc xe 16 chỗ.', 'Vui lòng hỗ trợ bảng chào mừng và phòng họp.', 'CANCELLED', DATE_SUB(@seed_now, INTERVAL 252 DAY), DATE_SUB(@seed_now, INTERVAL 251 DAY), NULL, DATE_SUB(@seed_now, INTERVAL 250 DAY), 'SYSTEM', 'Hồ sơ lịch sử phục vụ dashboard và báo cáo.', 0, DATE_SUB(@seed_now, INTERVAL 252 DAY), @v_kim, @seed_now, NULL),
  (@vr_hist_30, 'VR-HIST-030', @v_smith, @p_green, 'Emily Smith', 'Tổ chức đối tác quốc tế', 'Điều phối đoàn khách', '0900000000', 'emily.smith@greentech.example', 'GreenTech Asia Pte. Ltd. - chuyến thăm campus QN', 'SINGLE_CAMPUS', 'Trao đổi hợp tác, tham quan campus và làm việc với các phòng ban.', 'Làm việc với IC Office, tham quan khu học tập, phòng lab và trao đổi hợp tác.', 3, JSON_ARRAY(JSON_OBJECT('full_name','Điều phối viên đoàn','role','Coordinator')), JSON_OBJECT('full_name','Emily Smith','organization','Tổ chức đối tác quốc tế','phone','0900000000','email','emily.smith@greentech.example'), 'EN', NULL, 'Đoàn tự túc xe 16 chỗ.', 'Vui lòng hỗ trợ bảng chào mừng và phòng họp.', 'PENDING_APPROVAL', DATE_SUB(@seed_now, INTERVAL 260 DAY), DATE_SUB(@seed_now, INTERVAL 259 DAY), NULL, NULL, NULL, 'Hồ sơ lịch sử phục vụ dashboard và báo cáo.', 0, DATE_SUB(@seed_now, INTERVAL 260 DAY), @v_smith, @seed_now, NULL),
  (@vr_hist_31, 'VR-HIST-031', @v_nguyen_no_dau, @p_ministry, 'Nguyen Van Nam', 'Tổ chức đối tác quốc tế', 'Điều phối đoàn khách', '0900000000', 'nguyen.van.nam@partner.example', 'Viện Phát triển Giáo dục Quốc tế - chuyến thăm campus HN', 'SINGLE_CAMPUS', 'Trao đổi hợp tác, tham quan campus và làm việc với các phòng ban.', 'Làm việc với IC Office, tham quan khu học tập, phòng lab và trao đổi hợp tác.', 3, JSON_ARRAY(JSON_OBJECT('full_name','Điều phối viên đoàn','role','Coordinator')), JSON_OBJECT('full_name','Nguyen Van Nam','organization','Tổ chức đối tác quốc tế','phone','0900000000','email','nguyen.van.nam@partner.example'), 'EN', NULL, 'Đoàn tự túc xe 16 chỗ.', 'Vui lòng hỗ trợ bảng chào mừng và phòng họp.', 'APPROVED', DATE_SUB(@seed_now, INTERVAL 268 DAY), DATE_SUB(@seed_now, INTERVAL 267 DAY), @u_stafflead_hn, DATE_SUB(@seed_now, INTERVAL 266 DAY), 'STAFF_LEADER', 'Hồ sơ lịch sử phục vụ dashboard và báo cáo.', 0, DATE_SUB(@seed_now, INTERVAL 268 DAY), @v_nguyen_no_dau, @seed_now, @u_stafflead_hn),
  (@vr_hist_32, 'VR-HIST-032', @v_lee, @p_asean, 'Lee Joon Ho', 'Tổ chức đối tác quốc tế', 'Điều phối đoàn khách', '0900000000', 'lee.joonho@seoultech.example', 'ASEAN Future Skills Foundation - chuyến thăm campus HCM', 'SINGLE_CAMPUS', 'Trao đổi hợp tác, tham quan campus và làm việc với các phòng ban.', 'Làm việc với IC Office, tham quan khu học tập, phòng lab và trao đổi hợp tác.', 3, JSON_ARRAY(JSON_OBJECT('full_name','Điều phối viên đoàn','role','Coordinator')), JSON_OBJECT('full_name','Lee Joon Ho','organization','Tổ chức đối tác quốc tế','phone','0900000000','email','lee.joonho@seoultech.example'), 'EN', NULL, 'Đoàn tự túc xe 16 chỗ.', 'Vui lòng hỗ trợ bảng chào mừng và phòng họp.', 'APPROVED', DATE_SUB(@seed_now, INTERVAL 276 DAY), DATE_SUB(@seed_now, INTERVAL 275 DAY), @u_stafflead_hcm, DATE_SUB(@seed_now, INTERVAL 274 DAY), 'STAFF_LEADER', 'Hồ sơ lịch sử phục vụ dashboard và báo cáo.', 0, DATE_SUB(@seed_now, INTERVAL 276 DAY), @v_lee, @seed_now, @u_stafflead_hcm),
  (@vr_hist_33, 'VR-HIST-033', @v_kim, @p_seoul, 'Kim Min Seo', 'Tổ chức đối tác quốc tế', 'Điều phối đoàn khách', '0900000000', 'kim.minseo@seoultech.example', 'Seoul Future University - chuyến thăm campus DN', 'SINGLE_CAMPUS', 'Trao đổi hợp tác, tham quan campus và làm việc với các phòng ban.', 'Làm việc với IC Office, tham quan khu học tập, phòng lab và trao đổi hợp tác.', 3, JSON_ARRAY(JSON_OBJECT('full_name','Điều phối viên đoàn','role','Coordinator')), JSON_OBJECT('full_name','Kim Min Seo','organization','Tổ chức đối tác quốc tế','phone','0900000000','email','kim.minseo@seoultech.example'), 'EN', NULL, 'Đoàn tự túc xe 16 chỗ.', 'Vui lòng hỗ trợ bảng chào mừng và phòng họp.', 'REJECTED', DATE_SUB(@seed_now, INTERVAL 284 DAY), DATE_SUB(@seed_now, INTERVAL 283 DAY), @u_stafflead_dn, DATE_SUB(@seed_now, INTERVAL 282 DAY), 'STAFF_LEADER', 'Hồ sơ lịch sử phục vụ dashboard và báo cáo.', 0, DATE_SUB(@seed_now, INTERVAL 284 DAY), @v_kim, @seed_now, @u_stafflead_dn),
  (@vr_hist_34, 'VR-HIST-034', @v_smith, @p_green, 'Emily Smith', 'Tổ chức đối tác quốc tế', 'Điều phối đoàn khách', '0900000000', 'emily.smith@greentech.example', 'GreenTech Asia Pte. Ltd. - chuyến thăm campus CT', 'SINGLE_CAMPUS', 'Trao đổi hợp tác, tham quan campus và làm việc với các phòng ban.', 'Làm việc với IC Office, tham quan khu học tập, phòng lab và trao đổi hợp tác.', 3, JSON_ARRAY(JSON_OBJECT('full_name','Điều phối viên đoàn','role','Coordinator')), JSON_OBJECT('full_name','Emily Smith','organization','Tổ chức đối tác quốc tế','phone','0900000000','email','emily.smith@greentech.example'), 'EN', NULL, 'Đoàn tự túc xe 16 chỗ.', 'Vui lòng hỗ trợ bảng chào mừng và phòng họp.', 'CANCELLED', DATE_SUB(@seed_now, INTERVAL 292 DAY), DATE_SUB(@seed_now, INTERVAL 291 DAY), NULL, DATE_SUB(@seed_now, INTERVAL 290 DAY), 'SYSTEM', 'Hồ sơ lịch sử phục vụ dashboard và báo cáo.', 0, DATE_SUB(@seed_now, INTERVAL 292 DAY), @v_smith, @seed_now, NULL),
  (@vr_hist_35, 'VR-HIST-035', @v_nguyen_no_dau, @p_ministry, 'Nguyen Van Nam', 'Tổ chức đối tác quốc tế', 'Điều phối đoàn khách', '0900000000', 'nguyen.van.nam@partner.example', 'Viện Phát triển Giáo dục Quốc tế - chuyến thăm campus QN', 'SINGLE_CAMPUS', 'Trao đổi hợp tác, tham quan campus và làm việc với các phòng ban.', 'Làm việc với IC Office, tham quan khu học tập, phòng lab và trao đổi hợp tác.', 3, JSON_ARRAY(JSON_OBJECT('full_name','Điều phối viên đoàn','role','Coordinator')), JSON_OBJECT('full_name','Nguyen Van Nam','organization','Tổ chức đối tác quốc tế','phone','0900000000','email','nguyen.van.nam@partner.example'), 'EN', NULL, 'Đoàn tự túc xe 16 chỗ.', 'Vui lòng hỗ trợ bảng chào mừng và phòng họp.', 'PENDING_APPROVAL', DATE_SUB(@seed_now, INTERVAL 300 DAY), DATE_SUB(@seed_now, INTERVAL 299 DAY), NULL, NULL, NULL, 'Hồ sơ lịch sử phục vụ dashboard và báo cáo.', 0, DATE_SUB(@seed_now, INTERVAL 300 DAY), @v_nguyen_no_dau, @seed_now, NULL),
  (@vr_hist_36, 'VR-HIST-036', @v_lee, @p_asean, 'Lee Joon Ho', 'Tổ chức đối tác quốc tế', 'Điều phối đoàn khách', '0900000000', 'lee.joonho@seoultech.example', 'ASEAN Future Skills Foundation - chuyến thăm campus HN', 'SINGLE_CAMPUS', 'Trao đổi hợp tác, tham quan campus và làm việc với các phòng ban.', 'Làm việc với IC Office, tham quan khu học tập, phòng lab và trao đổi hợp tác.', 3, JSON_ARRAY(JSON_OBJECT('full_name','Điều phối viên đoàn','role','Coordinator')), JSON_OBJECT('full_name','Lee Joon Ho','organization','Tổ chức đối tác quốc tế','phone','0900000000','email','lee.joonho@seoultech.example'), 'EN', NULL, 'Đoàn tự túc xe 16 chỗ.', 'Vui lòng hỗ trợ bảng chào mừng và phòng họp.', 'APPROVED', DATE_SUB(@seed_now, INTERVAL 308 DAY), DATE_SUB(@seed_now, INTERVAL 307 DAY), @u_stafflead_hn, DATE_SUB(@seed_now, INTERVAL 306 DAY), 'STAFF_LEADER', 'Hồ sơ lịch sử phục vụ dashboard và báo cáo.', 0, DATE_SUB(@seed_now, INTERVAL 308 DAY), @v_lee, @seed_now, @u_stafflead_hn),
  (@vr_hist_37, 'VR-HIST-037', @v_kim, @p_seoul, 'Kim Min Seo', 'Tổ chức đối tác quốc tế', 'Điều phối đoàn khách', '0900000000', 'kim.minseo@seoultech.example', 'Seoul Future University - chuyến thăm campus HCM', 'SINGLE_CAMPUS', 'Trao đổi hợp tác, tham quan campus và làm việc với các phòng ban.', 'Làm việc với IC Office, tham quan khu học tập, phòng lab và trao đổi hợp tác.', 3, JSON_ARRAY(JSON_OBJECT('full_name','Điều phối viên đoàn','role','Coordinator')), JSON_OBJECT('full_name','Kim Min Seo','organization','Tổ chức đối tác quốc tế','phone','0900000000','email','kim.minseo@seoultech.example'), 'EN', NULL, 'Đoàn tự túc xe 16 chỗ.', 'Vui lòng hỗ trợ bảng chào mừng và phòng họp.', 'APPROVED', DATE_SUB(@seed_now, INTERVAL 316 DAY), DATE_SUB(@seed_now, INTERVAL 315 DAY), @u_stafflead_hcm, DATE_SUB(@seed_now, INTERVAL 314 DAY), 'STAFF_LEADER', 'Hồ sơ lịch sử phục vụ dashboard và báo cáo.', 0, DATE_SUB(@seed_now, INTERVAL 316 DAY), @v_kim, @seed_now, @u_stafflead_hcm),
  (@vr_hist_38, 'VR-HIST-038', @v_smith, @p_green, 'Emily Smith', 'Tổ chức đối tác quốc tế', 'Điều phối đoàn khách', '0900000000', 'emily.smith@greentech.example', 'GreenTech Asia Pte. Ltd. - chuyến thăm campus DN', 'SINGLE_CAMPUS', 'Trao đổi hợp tác, tham quan campus và làm việc với các phòng ban.', 'Làm việc với IC Office, tham quan khu học tập, phòng lab và trao đổi hợp tác.', 3, JSON_ARRAY(JSON_OBJECT('full_name','Điều phối viên đoàn','role','Coordinator')), JSON_OBJECT('full_name','Emily Smith','organization','Tổ chức đối tác quốc tế','phone','0900000000','email','emily.smith@greentech.example'), 'EN', NULL, 'Đoàn tự túc xe 16 chỗ.', 'Vui lòng hỗ trợ bảng chào mừng và phòng họp.', 'REJECTED', DATE_SUB(@seed_now, INTERVAL 324 DAY), DATE_SUB(@seed_now, INTERVAL 323 DAY), @u_stafflead_dn, DATE_SUB(@seed_now, INTERVAL 322 DAY), 'STAFF_LEADER', 'Hồ sơ lịch sử phục vụ dashboard và báo cáo.', 0, DATE_SUB(@seed_now, INTERVAL 324 DAY), @v_smith, @seed_now, @u_stafflead_dn),
  (@vr_hist_39, 'VR-HIST-039', @v_nguyen_no_dau, @p_ministry, 'Nguyen Van Nam', 'Tổ chức đối tác quốc tế', 'Điều phối đoàn khách', '0900000000', 'nguyen.van.nam@partner.example', 'Viện Phát triển Giáo dục Quốc tế - chuyến thăm campus CT', 'SINGLE_CAMPUS', 'Trao đổi hợp tác, tham quan campus và làm việc với các phòng ban.', 'Làm việc với IC Office, tham quan khu học tập, phòng lab và trao đổi hợp tác.', 3, JSON_ARRAY(JSON_OBJECT('full_name','Điều phối viên đoàn','role','Coordinator')), JSON_OBJECT('full_name','Nguyen Van Nam','organization','Tổ chức đối tác quốc tế','phone','0900000000','email','nguyen.van.nam@partner.example'), 'EN', NULL, 'Đoàn tự túc xe 16 chỗ.', 'Vui lòng hỗ trợ bảng chào mừng và phòng họp.', 'CANCELLED', DATE_SUB(@seed_now, INTERVAL 332 DAY), DATE_SUB(@seed_now, INTERVAL 331 DAY), NULL, DATE_SUB(@seed_now, INTERVAL 330 DAY), 'SYSTEM', 'Hồ sơ lịch sử phục vụ dashboard và báo cáo.', 0, DATE_SUB(@seed_now, INTERVAL 332 DAY), @v_nguyen_no_dau, @seed_now, NULL),
  (@vr_hist_40, 'VR-HIST-040', @v_lee, @p_asean, 'Lee Joon Ho', 'Tổ chức đối tác quốc tế', 'Điều phối đoàn khách', '0900000000', 'lee.joonho@seoultech.example', 'ASEAN Future Skills Foundation - chuyến thăm campus QN', 'SINGLE_CAMPUS', 'Trao đổi hợp tác, tham quan campus và làm việc với các phòng ban.', 'Làm việc với IC Office, tham quan khu học tập, phòng lab và trao đổi hợp tác.', 3, JSON_ARRAY(JSON_OBJECT('full_name','Điều phối viên đoàn','role','Coordinator')), JSON_OBJECT('full_name','Lee Joon Ho','organization','Tổ chức đối tác quốc tế','phone','0900000000','email','lee.joonho@seoultech.example'), 'EN', NULL, 'Đoàn tự túc xe 16 chỗ.', 'Vui lòng hỗ trợ bảng chào mừng và phòng họp.', 'PENDING_APPROVAL', DATE_SUB(@seed_now, INTERVAL 340 DAY), DATE_SUB(@seed_now, INTERVAL 339 DAY), NULL, NULL, NULL, 'Hồ sơ lịch sử phục vụ dashboard và báo cáo.', 0, DATE_SUB(@seed_now, INTERVAL 340 DAY), @v_lee, @seed_now, NULL),
  (@vr_hist_41, 'VR-HIST-041', @v_kim, @p_seoul, 'Kim Min Seo', 'Tổ chức đối tác quốc tế', 'Điều phối đoàn khách', '0900000000', 'kim.minseo@seoultech.example', 'Seoul Future University - chuyến thăm campus HN', 'SINGLE_CAMPUS', 'Trao đổi hợp tác, tham quan campus và làm việc với các phòng ban.', 'Làm việc với IC Office, tham quan khu học tập, phòng lab và trao đổi hợp tác.', 3, JSON_ARRAY(JSON_OBJECT('full_name','Điều phối viên đoàn','role','Coordinator')), JSON_OBJECT('full_name','Kim Min Seo','organization','Tổ chức đối tác quốc tế','phone','0900000000','email','kim.minseo@seoultech.example'), 'EN', NULL, 'Đoàn tự túc xe 16 chỗ.', 'Vui lòng hỗ trợ bảng chào mừng và phòng họp.', 'APPROVED', DATE_SUB(@seed_now, INTERVAL 348 DAY), DATE_SUB(@seed_now, INTERVAL 347 DAY), @u_stafflead_hn, DATE_SUB(@seed_now, INTERVAL 346 DAY), 'STAFF_LEADER', 'Hồ sơ lịch sử phục vụ dashboard và báo cáo.', 0, DATE_SUB(@seed_now, INTERVAL 348 DAY), @v_kim, @seed_now, @u_stafflead_hn),
  (@vr_hist_42, 'VR-HIST-042', @v_smith, @p_green, 'Emily Smith', 'Tổ chức đối tác quốc tế', 'Điều phối đoàn khách', '0900000000', 'emily.smith@greentech.example', 'GreenTech Asia Pte. Ltd. - chuyến thăm campus HCM', 'SINGLE_CAMPUS', 'Trao đổi hợp tác, tham quan campus và làm việc với các phòng ban.', 'Làm việc với IC Office, tham quan khu học tập, phòng lab và trao đổi hợp tác.', 3, JSON_ARRAY(JSON_OBJECT('full_name','Điều phối viên đoàn','role','Coordinator')), JSON_OBJECT('full_name','Emily Smith','organization','Tổ chức đối tác quốc tế','phone','0900000000','email','emily.smith@greentech.example'), 'EN', NULL, 'Đoàn tự túc xe 16 chỗ.', 'Vui lòng hỗ trợ bảng chào mừng và phòng họp.', 'APPROVED', DATE_SUB(@seed_now, INTERVAL 356 DAY), DATE_SUB(@seed_now, INTERVAL 355 DAY), @u_stafflead_hcm, DATE_SUB(@seed_now, INTERVAL 354 DAY), 'STAFF_LEADER', 'Hồ sơ lịch sử phục vụ dashboard và báo cáo.', 0, DATE_SUB(@seed_now, INTERVAL 356 DAY), @v_smith, @seed_now, @u_stafflead_hcm),
  (@vr_hist_43, 'VR-HIST-043', @v_nguyen_no_dau, @p_ministry, 'Nguyen Van Nam', 'Tổ chức đối tác quốc tế', 'Điều phối đoàn khách', '0900000000', 'nguyen.van.nam@partner.example', 'Viện Phát triển Giáo dục Quốc tế - chuyến thăm campus DN', 'SINGLE_CAMPUS', 'Trao đổi hợp tác, tham quan campus và làm việc với các phòng ban.', 'Làm việc với IC Office, tham quan khu học tập, phòng lab và trao đổi hợp tác.', 3, JSON_ARRAY(JSON_OBJECT('full_name','Điều phối viên đoàn','role','Coordinator')), JSON_OBJECT('full_name','Nguyen Van Nam','organization','Tổ chức đối tác quốc tế','phone','0900000000','email','nguyen.van.nam@partner.example'), 'EN', NULL, 'Đoàn tự túc xe 16 chỗ.', 'Vui lòng hỗ trợ bảng chào mừng và phòng họp.', 'REJECTED', DATE_SUB(@seed_now, INTERVAL 364 DAY), DATE_SUB(@seed_now, INTERVAL 363 DAY), @u_stafflead_dn, DATE_SUB(@seed_now, INTERVAL 362 DAY), 'STAFF_LEADER', 'Hồ sơ lịch sử phục vụ dashboard và báo cáo.', 0, DATE_SUB(@seed_now, INTERVAL 364 DAY), @v_nguyen_no_dau, @seed_now, @u_stafflead_dn),
  (@vr_hist_44, 'VR-HIST-044', @v_lee, @p_asean, 'Lee Joon Ho', 'Tổ chức đối tác quốc tế', 'Điều phối đoàn khách', '0900000000', 'lee.joonho@seoultech.example', 'ASEAN Future Skills Foundation - chuyến thăm campus CT', 'SINGLE_CAMPUS', 'Trao đổi hợp tác, tham quan campus và làm việc với các phòng ban.', 'Làm việc với IC Office, tham quan khu học tập, phòng lab và trao đổi hợp tác.', 3, JSON_ARRAY(JSON_OBJECT('full_name','Điều phối viên đoàn','role','Coordinator')), JSON_OBJECT('full_name','Lee Joon Ho','organization','Tổ chức đối tác quốc tế','phone','0900000000','email','lee.joonho@seoultech.example'), 'EN', NULL, 'Đoàn tự túc xe 16 chỗ.', 'Vui lòng hỗ trợ bảng chào mừng và phòng họp.', 'CANCELLED', DATE_SUB(@seed_now, INTERVAL 372 DAY), DATE_SUB(@seed_now, INTERVAL 371 DAY), NULL, DATE_SUB(@seed_now, INTERVAL 370 DAY), 'SYSTEM', 'Hồ sơ lịch sử phục vụ dashboard và báo cáo.', 0, DATE_SUB(@seed_now, INTERVAL 372 DAY), @v_lee, @seed_now, NULL);

INSERT INTO visit_request_campuses (visit_instance_id, visit_request_id, campus_id, instance_code, planned_start_at, planned_end_at, status, current_host_user_id, host_assigned_by, host_assigned_at, host_assignment_source, host_transferred_by, host_transferred_at, host_transfer_note, closed_by, closed_at, close_note, row_version, created_at, created_by, updated_at, updated_by)
VALUES
  (@vi_pending_approval_hcm, @vr_pending_approval_seed, @campus_hcm, 'VR-PA-001-HCM', DATE_ADD(DATE_ADD(DATE(@seed_now), INTERVAL 2 DAY), INTERVAL 540 MINUTE), DATE_ADD(DATE_ADD(DATE(@seed_now), INTERVAL 2 DAY), INTERVAL 990 MINUTE), 'WAITING_REQUEST_APPROVAL', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, 0, DATE_SUB(@seed_now, INTERVAL 17 DAY), @v_pending_approval_seed, @seed_now, @v_pending_approval_seed),
  (@vi_pa_hn, @vr_pending_approval_multi, @campus_hn, 'VR-PA-002-HN', DATE_ADD(DATE_ADD(DATE(@seed_now), INTERVAL 9 DAY), INTERVAL 540 MINUTE), DATE_ADD(DATE_ADD(DATE(@seed_now), INTERVAL 9 DAY), INTERVAL 990 MINUTE), 'WAITING_REQUEST_APPROVAL', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, 0, DATE_SUB(@seed_now, INTERVAL 24 DAY), @v_pending_approval, @seed_now, @v_pending_approval),
  (@vi_pa_ct, @vr_pending_approval_multi, @campus_ct, 'VR-PA-002-CT', DATE_ADD(DATE_ADD(DATE(@seed_now), INTERVAL 9 DAY), INTERVAL 540 MINUTE), DATE_ADD(DATE_ADD(DATE(@seed_now), INTERVAL 9 DAY), INTERVAL 990 MINUTE), 'WAITING_REQUEST_APPROVAL', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, 0, DATE_SUB(@seed_now, INTERVAL 24 DAY), @v_pending_approval, @seed_now, @v_pending_approval),
  (@vi_as_hn, @vr_approved_single_before, @campus_hn, 'VR-AS-003-HN', DATE_ADD(DATE_ADD(DATE(@seed_now), INTERVAL 14 DAY), INTERVAL 540 MINUTE), DATE_ADD(DATE_ADD(DATE(@seed_now), INTERVAL 14 DAY), INTERVAL 990 MINUTE), 'BEFORE_VISIT', @u_stafflead_hn, @u_stafflead_hn, DATE_SUB(@seed_now, INTERVAL 29 DAY), 'MANUAL_APPROVAL', NULL, NULL, NULL, NULL, NULL, NULL, 0, DATE_SUB(@seed_now, INTERVAL 29 DAY), @v_kim, @seed_now, @u_stafflead_hn),
  (@vi_am_hn, @vr_approved_multi_during, @campus_hn, 'VR-AM-004-HN', DATE_ADD(DATE_ADD(DATE(@seed_now), INTERVAL 0 DAY), INTERVAL 540 MINUTE), DATE_ADD(DATE_ADD(DATE(@seed_now), INTERVAL 0 DAY), INTERVAL 990 MINUTE), 'DURING_VISIT', @u_stafflead_hn, @u_ho_ha, DATE_SUB(@seed_now, INTERVAL 15 DAY), 'AUTO_STAFF_LEADER', NULL, NULL, NULL, NULL, NULL, NULL, 0, DATE_SUB(@seed_now, INTERVAL 15 DAY), @v_lee, @seed_now, @u_stafflead_hn),
  (@vi_am_hcm, @vr_approved_multi_during, @campus_hcm, 'VR-AM-004-HCM', DATE_ADD(DATE_ADD(DATE(@seed_now), INTERVAL 0 DAY), INTERVAL 540 MINUTE), DATE_ADD(DATE_ADD(DATE(@seed_now), INTERVAL 0 DAY), INTERVAL 990 MINUTE), 'ASSIGNED', @u_stafflead_hcm, @u_ho_ha, DATE_SUB(@seed_now, INTERVAL 15 DAY), 'AUTO_STAFF_LEADER', NULL, NULL, NULL, NULL, NULL, NULL, 0, DATE_SUB(@seed_now, INTERVAL 15 DAY), @v_lee, @seed_now, @u_stafflead_hcm),
  (@vi_rs_hcm, @vr_rejected_single, @campus_hcm, 'VR-RS-005-HCM', DATE_ADD(DATE_ADD(DATE(@seed_now), INTERVAL 5 DAY), INTERVAL 540 MINUTE), DATE_ADD(DATE_ADD(DATE(@seed_now), INTERVAL 5 DAY), INTERVAL 990 MINUTE), 'WAITING_REQUEST_APPROVAL', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, 0, DATE_SUB(@seed_now, INTERVAL 20 DAY), @v_smith, @seed_now, @v_smith),
  (@vi_rm_dn, @vr_rejected_multi, @campus_dn, 'VR-RM-006-DN', DATE_ADD(DATE_ADD(DATE(@seed_now), INTERVAL 20 DAY), INTERVAL 540 MINUTE), DATE_ADD(DATE_ADD(DATE(@seed_now), INTERVAL 20 DAY), INTERVAL 990 MINUTE), 'WAITING_REQUEST_APPROVAL', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, 0, DATE_SUB(@seed_now, INTERVAL 35 DAY), @v_tanaka, @seed_now, @v_tanaka),
  (@vi_rm_ct, @vr_rejected_multi, @campus_ct, 'VR-RM-006-CT', DATE_ADD(DATE_ADD(DATE(@seed_now), INTERVAL 20 DAY), INTERVAL 540 MINUTE), DATE_ADD(DATE_ADD(DATE(@seed_now), INTERVAL 20 DAY), INTERVAL 990 MINUTE), 'WAITING_REQUEST_APPROVAL', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, 0, DATE_SUB(@seed_now, INTERVAL 35 DAY), @v_tanaka, @seed_now, @v_tanaka),
  (@vi_cn_hn, @vr_cancelled, @campus_hn, 'VR-CN-007-HN', DATE_ADD(DATE_ADD(DATE(@seed_now), INTERVAL 30 DAY), INTERVAL 540 MINUTE), DATE_ADD(DATE_ADD(DATE(@seed_now), INTERVAL 30 DAY), INTERVAL 990 MINUTE), 'CANCELLED', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, 0, DATE_SUB(@seed_now, INTERVAL 45 DAY), @v_nguyen_no_dau, @seed_now, @v_nguyen_no_dau),
  (@vi_av_ct, @vr_after_visit, @campus_ct, 'VR-AV-008-CT', DATE_ADD(DATE_ADD(DATE(@seed_now), INTERVAL -2 DAY), INTERVAL 540 MINUTE), DATE_ADD(DATE_ADD(DATE(@seed_now), INTERVAL -2 DAY), INTERVAL 990 MINUTE), 'AFTER_VISIT', @u_stafflead_ct, @u_stafflead_ct, DATE_SUB(@seed_now, INTERVAL 22 DAY), 'MANUAL_APPROVAL', NULL, NULL, NULL, NULL, NULL, NULL, 0, DATE_SUB(@seed_now, INTERVAL 22 DAY), @v_short_name, @seed_now, @u_stafflead_ct),
  (@vi_cl_hn, @vr_closed, @campus_hn, 'VR-CL-009-HN', DATE_ADD(DATE_ADD(DATE(@seed_now), INTERVAL -30 DAY), INTERVAL 540 MINUTE), DATE_ADD(DATE_ADD(DATE(@seed_now), INTERVAL -30 DAY), INTERVAL 990 MINUTE), 'CLOSED', @u_stafflead_hn, @u_stafflead_hn, DATE_SUB(@seed_now, INTERVAL 50 DAY), 'MANUAL_APPROVAL', NULL, NULL, NULL, @u_stafflead_hn, DATE_ADD(DATE_ADD(DATE(@seed_now), INTERVAL -29 DAY), INTERVAL 600 MINUTE), 'Đã hoàn tất hồ sơ.', 0, DATE_SUB(@seed_now, INTERVAL 50 DAY), @v_long_name, @seed_now, @u_stafflead_hn),
  (@vi_ci_hcm, @vr_cancelled_instance, @campus_hcm, 'VR-CI-010-HCM', DATE_ADD(DATE_ADD(DATE(@seed_now), INTERVAL 25 DAY), INTERVAL 540 MINUTE), DATE_ADD(DATE_ADD(DATE(@seed_now), INTERVAL 25 DAY), INTERVAL 990 MINUTE), 'BEFORE_VISIT', @u_staff_hcm, @u_stafflead_hcm, DATE_SUB(@seed_now, INTERVAL 2 DAY), 'TRANSFERRED', @u_stafflead_hcm, DATE_SUB(@seed_now, INTERVAL 2 DAY), 'Chuyển host cho IC Staff phụ trách GreenTech.', NULL, NULL, NULL, 0, DATE_SUB(@seed_now, INTERVAL 40 DAY), @v_smith, @seed_now, @u_staff_hcm),
  (@vi_ci_hn, @vr_cancelled_instance, @campus_hn, 'VR-CI-010-HN', DATE_ADD(DATE_ADD(DATE(@seed_now), INTERVAL 25 DAY), INTERVAL 540 MINUTE), DATE_ADD(DATE_ADD(DATE(@seed_now), INTERVAL 25 DAY), INTERVAL 990 MINUTE), 'CANCELLED', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, 0, DATE_SUB(@seed_now, INTERVAL 40 DAY), @v_smith, @seed_now, @v_smith),
  (@vi_ao_dn, @vr_assigned_only, @campus_dn, 'VR-AO-011-DN', DATE_ADD(DATE_ADD(DATE(@seed_now), INTERVAL 11 DAY), INTERVAL 540 MINUTE), DATE_ADD(DATE_ADD(DATE(@seed_now), INTERVAL 11 DAY), INTERVAL 990 MINUTE), 'ASSIGNED', @u_stafflead_dn, @u_stafflead_dn, DATE_SUB(@seed_now, INTERVAL 26 DAY), 'MANUAL_APPROVAL', NULL, NULL, NULL, NULL, NULL, NULL, 0, DATE_SUB(@seed_now, INTERVAL 26 DAY), @v_lee, @seed_now, @u_stafflead_dn),
  (@vi_hist_01, @vr_hist_01, @campus_hn, 'VR-HIST-001-HN', DATE_ADD(DATE_ADD(DATE(@seed_now), INTERVAL -8 DAY), INTERVAL 540 MINUTE), DATE_ADD(DATE_ADD(DATE(@seed_now), INTERVAL -8 DAY), INTERVAL 990 MINUTE), 'CLOSED', @u_stafflead_hn, @u_stafflead_hn, DATE_SUB(@seed_now, INTERVAL 28 DAY), 'MANUAL_APPROVAL', NULL, NULL, NULL, @u_stafflead_hn, DATE_ADD(DATE_ADD(DATE(@seed_now), INTERVAL -7 DAY), INTERVAL 600 MINUTE), 'Đã hoàn tất hồ sơ.', 0, DATE_SUB(@seed_now, INTERVAL 28 DAY), @v_kim, @seed_now, @u_stafflead_hn),
  (@vi_hist_02, @vr_hist_02, @campus_hcm, 'VR-HIST-002-HCM', DATE_ADD(DATE_ADD(DATE(@seed_now), INTERVAL -16 DAY), INTERVAL 540 MINUTE), DATE_ADD(DATE_ADD(DATE(@seed_now), INTERVAL -16 DAY), INTERVAL 990 MINUTE), 'AFTER_VISIT', @u_stafflead_hcm, @u_stafflead_hcm, DATE_SUB(@seed_now, INTERVAL 36 DAY), 'MANUAL_APPROVAL', NULL, NULL, NULL, NULL, NULL, NULL, 0, DATE_SUB(@seed_now, INTERVAL 36 DAY), @v_smith, @seed_now, @u_stafflead_hcm),
  (@vi_hist_03, @vr_hist_03, @campus_dn, 'VR-HIST-003-DN', DATE_ADD(DATE_ADD(DATE(@seed_now), INTERVAL -24 DAY), INTERVAL 540 MINUTE), DATE_ADD(DATE_ADD(DATE(@seed_now), INTERVAL -24 DAY), INTERVAL 990 MINUTE), 'WAITING_REQUEST_APPROVAL', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, 0, DATE_SUB(@seed_now, INTERVAL 44 DAY), @v_nguyen_no_dau, @seed_now, @v_nguyen_no_dau),
  (@vi_hist_04, @vr_hist_04, @campus_ct, 'VR-HIST-004-CT', DATE_ADD(DATE_ADD(DATE(@seed_now), INTERVAL -32 DAY), INTERVAL 540 MINUTE), DATE_ADD(DATE_ADD(DATE(@seed_now), INTERVAL -32 DAY), INTERVAL 990 MINUTE), 'CANCELLED', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, 0, DATE_SUB(@seed_now, INTERVAL 52 DAY), @v_lee, @seed_now, @v_lee),
  (@vi_hist_05, @vr_hist_05, @campus_qn, 'VR-HIST-005-QN', DATE_ADD(DATE_ADD(DATE(@seed_now), INTERVAL -40 DAY), INTERVAL 540 MINUTE), DATE_ADD(DATE_ADD(DATE(@seed_now), INTERVAL -40 DAY), INTERVAL 990 MINUTE), 'WAITING_REQUEST_APPROVAL', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, 0, DATE_SUB(@seed_now, INTERVAL 60 DAY), @v_kim, @seed_now, @v_kim),
  (@vi_hist_06, @vr_hist_06, @campus_hn, 'VR-HIST-006-HN', DATE_ADD(DATE_ADD(DATE(@seed_now), INTERVAL -48 DAY), INTERVAL 540 MINUTE), DATE_ADD(DATE_ADD(DATE(@seed_now), INTERVAL -48 DAY), INTERVAL 990 MINUTE), 'AFTER_VISIT', @u_stafflead_hn, @u_stafflead_hn, DATE_SUB(@seed_now, INTERVAL 68 DAY), 'MANUAL_APPROVAL', NULL, NULL, NULL, NULL, NULL, NULL, 0, DATE_SUB(@seed_now, INTERVAL 68 DAY), @v_smith, @seed_now, @u_stafflead_hn),
  (@vi_hist_07, @vr_hist_07, @campus_hcm, 'VR-HIST-007-HCM', DATE_ADD(DATE_ADD(DATE(@seed_now), INTERVAL -56 DAY), INTERVAL 540 MINUTE), DATE_ADD(DATE_ADD(DATE(@seed_now), INTERVAL -56 DAY), INTERVAL 990 MINUTE), 'CLOSED', @u_stafflead_hcm, @u_stafflead_hcm, DATE_SUB(@seed_now, INTERVAL 76 DAY), 'MANUAL_APPROVAL', NULL, NULL, NULL, @u_stafflead_hcm, DATE_ADD(DATE_ADD(DATE(@seed_now), INTERVAL -55 DAY), INTERVAL 600 MINUTE), 'Đã hoàn tất hồ sơ.', 0, DATE_SUB(@seed_now, INTERVAL 76 DAY), @v_nguyen_no_dau, @seed_now, @u_stafflead_hcm),
  (@vi_hist_08, @vr_hist_08, @campus_dn, 'VR-HIST-008-DN', DATE_ADD(DATE_ADD(DATE(@seed_now), INTERVAL -64 DAY), INTERVAL 540 MINUTE), DATE_ADD(DATE_ADD(DATE(@seed_now), INTERVAL -64 DAY), INTERVAL 990 MINUTE), 'WAITING_REQUEST_APPROVAL', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, 0, DATE_SUB(@seed_now, INTERVAL 84 DAY), @v_lee, @seed_now, @v_lee),
  (@vi_hist_09, @vr_hist_09, @campus_ct, 'VR-HIST-009-CT', DATE_ADD(DATE_ADD(DATE(@seed_now), INTERVAL -72 DAY), INTERVAL 540 MINUTE), DATE_ADD(DATE_ADD(DATE(@seed_now), INTERVAL -72 DAY), INTERVAL 990 MINUTE), 'CANCELLED', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, 0, DATE_SUB(@seed_now, INTERVAL 92 DAY), @v_kim, @seed_now, @v_kim),
  (@vi_hist_10, @vr_hist_10, @campus_qn, 'VR-HIST-010-QN', DATE_ADD(DATE_ADD(DATE(@seed_now), INTERVAL -80 DAY), INTERVAL 540 MINUTE), DATE_ADD(DATE_ADD(DATE(@seed_now), INTERVAL -80 DAY), INTERVAL 990 MINUTE), 'WAITING_REQUEST_APPROVAL', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, 0, DATE_SUB(@seed_now, INTERVAL 100 DAY), @v_smith, @seed_now, @v_smith),
  (@vi_hist_11, @vr_hist_11, @campus_hn, 'VR-HIST-011-HN', DATE_ADD(DATE_ADD(DATE(@seed_now), INTERVAL -88 DAY), INTERVAL 540 MINUTE), DATE_ADD(DATE_ADD(DATE(@seed_now), INTERVAL -88 DAY), INTERVAL 990 MINUTE), 'CLOSED', @u_stafflead_hn, @u_stafflead_hn, DATE_SUB(@seed_now, INTERVAL 108 DAY), 'MANUAL_APPROVAL', NULL, NULL, NULL, @u_stafflead_hn, DATE_ADD(DATE_ADD(DATE(@seed_now), INTERVAL -87 DAY), INTERVAL 600 MINUTE), 'Đã hoàn tất hồ sơ.', 0, DATE_SUB(@seed_now, INTERVAL 108 DAY), @v_nguyen_no_dau, @seed_now, @u_stafflead_hn),
  (@vi_hist_12, @vr_hist_12, @campus_hcm, 'VR-HIST-012-HCM', DATE_ADD(DATE_ADD(DATE(@seed_now), INTERVAL -96 DAY), INTERVAL 540 MINUTE), DATE_ADD(DATE_ADD(DATE(@seed_now), INTERVAL -96 DAY), INTERVAL 990 MINUTE), 'AFTER_VISIT', @u_stafflead_hcm, @u_stafflead_hcm, DATE_SUB(@seed_now, INTERVAL 116 DAY), 'MANUAL_APPROVAL', NULL, NULL, NULL, NULL, NULL, NULL, 0, DATE_SUB(@seed_now, INTERVAL 116 DAY), @v_lee, @seed_now, @u_stafflead_hcm),
  (@vi_hist_13, @vr_hist_13, @campus_dn, 'VR-HIST-013-DN', DATE_ADD(DATE_ADD(DATE(@seed_now), INTERVAL -104 DAY), INTERVAL 540 MINUTE), DATE_ADD(DATE_ADD(DATE(@seed_now), INTERVAL -104 DAY), INTERVAL 990 MINUTE), 'WAITING_REQUEST_APPROVAL', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, 0, DATE_SUB(@seed_now, INTERVAL 124 DAY), @v_kim, @seed_now, @v_kim),
  (@vi_hist_14, @vr_hist_14, @campus_ct, 'VR-HIST-014-CT', DATE_ADD(DATE_ADD(DATE(@seed_now), INTERVAL -112 DAY), INTERVAL 540 MINUTE), DATE_ADD(DATE_ADD(DATE(@seed_now), INTERVAL -112 DAY), INTERVAL 990 MINUTE), 'CANCELLED', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, 0, DATE_SUB(@seed_now, INTERVAL 132 DAY), @v_smith, @seed_now, @v_smith),
  (@vi_hist_15, @vr_hist_15, @campus_qn, 'VR-HIST-015-QN', DATE_ADD(DATE_ADD(DATE(@seed_now), INTERVAL -120 DAY), INTERVAL 540 MINUTE), DATE_ADD(DATE_ADD(DATE(@seed_now), INTERVAL -120 DAY), INTERVAL 990 MINUTE), 'WAITING_REQUEST_APPROVAL', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, 0, DATE_SUB(@seed_now, INTERVAL 140 DAY), @v_nguyen_no_dau, @seed_now, @v_nguyen_no_dau),
  (@vi_hist_16, @vr_hist_16, @campus_hn, 'VR-HIST-016-HN', DATE_ADD(DATE_ADD(DATE(@seed_now), INTERVAL -128 DAY), INTERVAL 540 MINUTE), DATE_ADD(DATE_ADD(DATE(@seed_now), INTERVAL -128 DAY), INTERVAL 990 MINUTE), 'AFTER_VISIT', @u_stafflead_hn, @u_stafflead_hn, DATE_SUB(@seed_now, INTERVAL 148 DAY), 'MANUAL_APPROVAL', NULL, NULL, NULL, NULL, NULL, NULL, 0, DATE_SUB(@seed_now, INTERVAL 148 DAY), @v_lee, @seed_now, @u_stafflead_hn),
  (@vi_hist_17, @vr_hist_17, @campus_hcm, 'VR-HIST-017-HCM', DATE_ADD(DATE_ADD(DATE(@seed_now), INTERVAL -136 DAY), INTERVAL 540 MINUTE), DATE_ADD(DATE_ADD(DATE(@seed_now), INTERVAL -136 DAY), INTERVAL 990 MINUTE), 'CLOSED', @u_stafflead_hcm, @u_stafflead_hcm, DATE_SUB(@seed_now, INTERVAL 156 DAY), 'MANUAL_APPROVAL', NULL, NULL, NULL, @u_stafflead_hcm, DATE_ADD(DATE_ADD(DATE(@seed_now), INTERVAL -135 DAY), INTERVAL 600 MINUTE), 'Đã hoàn tất hồ sơ.', 0, DATE_SUB(@seed_now, INTERVAL 156 DAY), @v_kim, @seed_now, @u_stafflead_hcm),
  (@vi_hist_18, @vr_hist_18, @campus_dn, 'VR-HIST-018-DN', DATE_ADD(DATE_ADD(DATE(@seed_now), INTERVAL -144 DAY), INTERVAL 540 MINUTE), DATE_ADD(DATE_ADD(DATE(@seed_now), INTERVAL -144 DAY), INTERVAL 990 MINUTE), 'WAITING_REQUEST_APPROVAL', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, 0, DATE_SUB(@seed_now, INTERVAL 164 DAY), @v_smith, @seed_now, @v_smith),
  (@vi_hist_19, @vr_hist_19, @campus_ct, 'VR-HIST-019-CT', DATE_ADD(DATE_ADD(DATE(@seed_now), INTERVAL -152 DAY), INTERVAL 540 MINUTE), DATE_ADD(DATE_ADD(DATE(@seed_now), INTERVAL -152 DAY), INTERVAL 990 MINUTE), 'CANCELLED', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, 0, DATE_SUB(@seed_now, INTERVAL 172 DAY), @v_nguyen_no_dau, @seed_now, @v_nguyen_no_dau),
  (@vi_hist_20, @vr_hist_20, @campus_qn, 'VR-HIST-020-QN', DATE_ADD(DATE_ADD(DATE(@seed_now), INTERVAL -160 DAY), INTERVAL 540 MINUTE), DATE_ADD(DATE_ADD(DATE(@seed_now), INTERVAL -160 DAY), INTERVAL 990 MINUTE), 'WAITING_REQUEST_APPROVAL', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, 0, DATE_SUB(@seed_now, INTERVAL 180 DAY), @v_lee, @seed_now, @v_lee),
  (@vi_hist_21, @vr_hist_21, @campus_hn, 'VR-HIST-021-HN', DATE_ADD(DATE_ADD(DATE(@seed_now), INTERVAL -168 DAY), INTERVAL 540 MINUTE), DATE_ADD(DATE_ADD(DATE(@seed_now), INTERVAL -168 DAY), INTERVAL 990 MINUTE), 'CLOSED', @u_stafflead_hn, @u_stafflead_hn, DATE_SUB(@seed_now, INTERVAL 188 DAY), 'MANUAL_APPROVAL', NULL, NULL, NULL, @u_stafflead_hn, DATE_ADD(DATE_ADD(DATE(@seed_now), INTERVAL -167 DAY), INTERVAL 600 MINUTE), 'Đã hoàn tất hồ sơ.', 0, DATE_SUB(@seed_now, INTERVAL 188 DAY), @v_kim, @seed_now, @u_stafflead_hn),
  (@vi_hist_22, @vr_hist_22, @campus_hcm, 'VR-HIST-022-HCM', DATE_ADD(DATE_ADD(DATE(@seed_now), INTERVAL -176 DAY), INTERVAL 540 MINUTE), DATE_ADD(DATE_ADD(DATE(@seed_now), INTERVAL -176 DAY), INTERVAL 990 MINUTE), 'AFTER_VISIT', @u_stafflead_hcm, @u_stafflead_hcm, DATE_SUB(@seed_now, INTERVAL 196 DAY), 'MANUAL_APPROVAL', NULL, NULL, NULL, NULL, NULL, NULL, 0, DATE_SUB(@seed_now, INTERVAL 196 DAY), @v_smith, @seed_now, @u_stafflead_hcm),
  (@vi_hist_23, @vr_hist_23, @campus_dn, 'VR-HIST-023-DN', DATE_ADD(DATE_ADD(DATE(@seed_now), INTERVAL -184 DAY), INTERVAL 540 MINUTE), DATE_ADD(DATE_ADD(DATE(@seed_now), INTERVAL -184 DAY), INTERVAL 990 MINUTE), 'WAITING_REQUEST_APPROVAL', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, 0, DATE_SUB(@seed_now, INTERVAL 204 DAY), @v_nguyen_no_dau, @seed_now, @v_nguyen_no_dau),
  (@vi_hist_24, @vr_hist_24, @campus_ct, 'VR-HIST-024-CT', DATE_ADD(DATE_ADD(DATE(@seed_now), INTERVAL -192 DAY), INTERVAL 540 MINUTE), DATE_ADD(DATE_ADD(DATE(@seed_now), INTERVAL -192 DAY), INTERVAL 990 MINUTE), 'CANCELLED', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, 0, DATE_SUB(@seed_now, INTERVAL 212 DAY), @v_lee, @seed_now, @v_lee),
  (@vi_hist_25, @vr_hist_25, @campus_qn, 'VR-HIST-025-QN', DATE_ADD(DATE_ADD(DATE(@seed_now), INTERVAL -200 DAY), INTERVAL 540 MINUTE), DATE_ADD(DATE_ADD(DATE(@seed_now), INTERVAL -200 DAY), INTERVAL 990 MINUTE), 'WAITING_REQUEST_APPROVAL', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, 0, DATE_SUB(@seed_now, INTERVAL 220 DAY), @v_kim, @seed_now, @v_kim),
  (@vi_hist_26, @vr_hist_26, @campus_hn, 'VR-HIST-026-HN', DATE_ADD(DATE_ADD(DATE(@seed_now), INTERVAL -208 DAY), INTERVAL 540 MINUTE), DATE_ADD(DATE_ADD(DATE(@seed_now), INTERVAL -208 DAY), INTERVAL 990 MINUTE), 'AFTER_VISIT', @u_stafflead_hn, @u_stafflead_hn, DATE_SUB(@seed_now, INTERVAL 228 DAY), 'MANUAL_APPROVAL', NULL, NULL, NULL, NULL, NULL, NULL, 0, DATE_SUB(@seed_now, INTERVAL 228 DAY), @v_smith, @seed_now, @u_stafflead_hn),
  (@vi_hist_27, @vr_hist_27, @campus_hcm, 'VR-HIST-027-HCM', DATE_ADD(DATE_ADD(DATE(@seed_now), INTERVAL -216 DAY), INTERVAL 540 MINUTE), DATE_ADD(DATE_ADD(DATE(@seed_now), INTERVAL -216 DAY), INTERVAL 990 MINUTE), 'CLOSED', @u_stafflead_hcm, @u_stafflead_hcm, DATE_SUB(@seed_now, INTERVAL 236 DAY), 'MANUAL_APPROVAL', NULL, NULL, NULL, @u_stafflead_hcm, DATE_ADD(DATE_ADD(DATE(@seed_now), INTERVAL -215 DAY), INTERVAL 600 MINUTE), 'Đã hoàn tất hồ sơ.', 0, DATE_SUB(@seed_now, INTERVAL 236 DAY), @v_nguyen_no_dau, @seed_now, @u_stafflead_hcm),
  (@vi_hist_28, @vr_hist_28, @campus_dn, 'VR-HIST-028-DN', DATE_ADD(DATE_ADD(DATE(@seed_now), INTERVAL -224 DAY), INTERVAL 540 MINUTE), DATE_ADD(DATE_ADD(DATE(@seed_now), INTERVAL -224 DAY), INTERVAL 990 MINUTE), 'WAITING_REQUEST_APPROVAL', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, 0, DATE_SUB(@seed_now, INTERVAL 244 DAY), @v_lee, @seed_now, @v_lee),
  (@vi_hist_29, @vr_hist_29, @campus_ct, 'VR-HIST-029-CT', DATE_ADD(DATE_ADD(DATE(@seed_now), INTERVAL -232 DAY), INTERVAL 540 MINUTE), DATE_ADD(DATE_ADD(DATE(@seed_now), INTERVAL -232 DAY), INTERVAL 990 MINUTE), 'CANCELLED', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, 0, DATE_SUB(@seed_now, INTERVAL 252 DAY), @v_kim, @seed_now, @v_kim),
  (@vi_hist_30, @vr_hist_30, @campus_qn, 'VR-HIST-030-QN', DATE_ADD(DATE_ADD(DATE(@seed_now), INTERVAL -240 DAY), INTERVAL 540 MINUTE), DATE_ADD(DATE_ADD(DATE(@seed_now), INTERVAL -240 DAY), INTERVAL 990 MINUTE), 'WAITING_REQUEST_APPROVAL', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, 0, DATE_SUB(@seed_now, INTERVAL 260 DAY), @v_smith, @seed_now, @v_smith),
  (@vi_hist_31, @vr_hist_31, @campus_hn, 'VR-HIST-031-HN', DATE_ADD(DATE_ADD(DATE(@seed_now), INTERVAL -248 DAY), INTERVAL 540 MINUTE), DATE_ADD(DATE_ADD(DATE(@seed_now), INTERVAL -248 DAY), INTERVAL 990 MINUTE), 'CLOSED', @u_stafflead_hn, @u_stafflead_hn, DATE_SUB(@seed_now, INTERVAL 268 DAY), 'MANUAL_APPROVAL', NULL, NULL, NULL, @u_stafflead_hn, DATE_ADD(DATE_ADD(DATE(@seed_now), INTERVAL -247 DAY), INTERVAL 600 MINUTE), 'Đã hoàn tất hồ sơ.', 0, DATE_SUB(@seed_now, INTERVAL 268 DAY), @v_nguyen_no_dau, @seed_now, @u_stafflead_hn),
  (@vi_hist_32, @vr_hist_32, @campus_hcm, 'VR-HIST-032-HCM', DATE_ADD(DATE_ADD(DATE(@seed_now), INTERVAL -256 DAY), INTERVAL 540 MINUTE), DATE_ADD(DATE_ADD(DATE(@seed_now), INTERVAL -256 DAY), INTERVAL 990 MINUTE), 'AFTER_VISIT', @u_stafflead_hcm, @u_stafflead_hcm, DATE_SUB(@seed_now, INTERVAL 276 DAY), 'MANUAL_APPROVAL', NULL, NULL, NULL, NULL, NULL, NULL, 0, DATE_SUB(@seed_now, INTERVAL 276 DAY), @v_lee, @seed_now, @u_stafflead_hcm),
  (@vi_hist_33, @vr_hist_33, @campus_dn, 'VR-HIST-033-DN', DATE_ADD(DATE_ADD(DATE(@seed_now), INTERVAL -264 DAY), INTERVAL 540 MINUTE), DATE_ADD(DATE_ADD(DATE(@seed_now), INTERVAL -264 DAY), INTERVAL 990 MINUTE), 'WAITING_REQUEST_APPROVAL', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, 0, DATE_SUB(@seed_now, INTERVAL 284 DAY), @v_kim, @seed_now, @v_kim),
  (@vi_hist_34, @vr_hist_34, @campus_ct, 'VR-HIST-034-CT', DATE_ADD(DATE_ADD(DATE(@seed_now), INTERVAL -272 DAY), INTERVAL 540 MINUTE), DATE_ADD(DATE_ADD(DATE(@seed_now), INTERVAL -272 DAY), INTERVAL 990 MINUTE), 'CANCELLED', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, 0, DATE_SUB(@seed_now, INTERVAL 292 DAY), @v_smith, @seed_now, @v_smith),
  (@vi_hist_35, @vr_hist_35, @campus_qn, 'VR-HIST-035-QN', DATE_ADD(DATE_ADD(DATE(@seed_now), INTERVAL -280 DAY), INTERVAL 540 MINUTE), DATE_ADD(DATE_ADD(DATE(@seed_now), INTERVAL -280 DAY), INTERVAL 990 MINUTE), 'WAITING_REQUEST_APPROVAL', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, 0, DATE_SUB(@seed_now, INTERVAL 300 DAY), @v_nguyen_no_dau, @seed_now, @v_nguyen_no_dau),
  (@vi_hist_36, @vr_hist_36, @campus_hn, 'VR-HIST-036-HN', DATE_ADD(DATE_ADD(DATE(@seed_now), INTERVAL -288 DAY), INTERVAL 540 MINUTE), DATE_ADD(DATE_ADD(DATE(@seed_now), INTERVAL -288 DAY), INTERVAL 990 MINUTE), 'AFTER_VISIT', @u_stafflead_hn, @u_stafflead_hn, DATE_SUB(@seed_now, INTERVAL 308 DAY), 'MANUAL_APPROVAL', NULL, NULL, NULL, NULL, NULL, NULL, 0, DATE_SUB(@seed_now, INTERVAL 308 DAY), @v_lee, @seed_now, @u_stafflead_hn),
  (@vi_hist_37, @vr_hist_37, @campus_hcm, 'VR-HIST-037-HCM', DATE_ADD(DATE_ADD(DATE(@seed_now), INTERVAL -296 DAY), INTERVAL 540 MINUTE), DATE_ADD(DATE_ADD(DATE(@seed_now), INTERVAL -296 DAY), INTERVAL 990 MINUTE), 'CLOSED', @u_stafflead_hcm, @u_stafflead_hcm, DATE_SUB(@seed_now, INTERVAL 316 DAY), 'MANUAL_APPROVAL', NULL, NULL, NULL, @u_stafflead_hcm, DATE_ADD(DATE_ADD(DATE(@seed_now), INTERVAL -295 DAY), INTERVAL 600 MINUTE), 'Đã hoàn tất hồ sơ.', 0, DATE_SUB(@seed_now, INTERVAL 316 DAY), @v_kim, @seed_now, @u_stafflead_hcm),
  (@vi_hist_38, @vr_hist_38, @campus_dn, 'VR-HIST-038-DN', DATE_ADD(DATE_ADD(DATE(@seed_now), INTERVAL -304 DAY), INTERVAL 540 MINUTE), DATE_ADD(DATE_ADD(DATE(@seed_now), INTERVAL -304 DAY), INTERVAL 990 MINUTE), 'WAITING_REQUEST_APPROVAL', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, 0, DATE_SUB(@seed_now, INTERVAL 324 DAY), @v_smith, @seed_now, @v_smith),
  (@vi_hist_39, @vr_hist_39, @campus_ct, 'VR-HIST-039-CT', DATE_ADD(DATE_ADD(DATE(@seed_now), INTERVAL -312 DAY), INTERVAL 540 MINUTE), DATE_ADD(DATE_ADD(DATE(@seed_now), INTERVAL -312 DAY), INTERVAL 990 MINUTE), 'CANCELLED', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, 0, DATE_SUB(@seed_now, INTERVAL 332 DAY), @v_nguyen_no_dau, @seed_now, @v_nguyen_no_dau),
  (@vi_hist_40, @vr_hist_40, @campus_qn, 'VR-HIST-040-QN', DATE_ADD(DATE_ADD(DATE(@seed_now), INTERVAL -320 DAY), INTERVAL 540 MINUTE), DATE_ADD(DATE_ADD(DATE(@seed_now), INTERVAL -320 DAY), INTERVAL 990 MINUTE), 'WAITING_REQUEST_APPROVAL', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, 0, DATE_SUB(@seed_now, INTERVAL 340 DAY), @v_lee, @seed_now, @v_lee),
  (@vi_hist_41, @vr_hist_41, @campus_hn, 'VR-HIST-041-HN', DATE_ADD(DATE_ADD(DATE(@seed_now), INTERVAL -328 DAY), INTERVAL 540 MINUTE), DATE_ADD(DATE_ADD(DATE(@seed_now), INTERVAL -328 DAY), INTERVAL 990 MINUTE), 'CLOSED', @u_stafflead_hn, @u_stafflead_hn, DATE_SUB(@seed_now, INTERVAL 348 DAY), 'MANUAL_APPROVAL', NULL, NULL, NULL, @u_stafflead_hn, DATE_ADD(DATE_ADD(DATE(@seed_now), INTERVAL -327 DAY), INTERVAL 600 MINUTE), 'Đã hoàn tất hồ sơ.', 0, DATE_SUB(@seed_now, INTERVAL 348 DAY), @v_kim, @seed_now, @u_stafflead_hn),
  (@vi_hist_42, @vr_hist_42, @campus_hcm, 'VR-HIST-042-HCM', DATE_ADD(DATE_ADD(DATE(@seed_now), INTERVAL -336 DAY), INTERVAL 540 MINUTE), DATE_ADD(DATE_ADD(DATE(@seed_now), INTERVAL -336 DAY), INTERVAL 990 MINUTE), 'AFTER_VISIT', @u_stafflead_hcm, @u_stafflead_hcm, DATE_SUB(@seed_now, INTERVAL 356 DAY), 'MANUAL_APPROVAL', NULL, NULL, NULL, NULL, NULL, NULL, 0, DATE_SUB(@seed_now, INTERVAL 356 DAY), @v_smith, @seed_now, @u_stafflead_hcm),
  (@vi_hist_43, @vr_hist_43, @campus_dn, 'VR-HIST-043-DN', DATE_ADD(DATE_ADD(DATE(@seed_now), INTERVAL -344 DAY), INTERVAL 540 MINUTE), DATE_ADD(DATE_ADD(DATE(@seed_now), INTERVAL -344 DAY), INTERVAL 990 MINUTE), 'WAITING_REQUEST_APPROVAL', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, 0, DATE_SUB(@seed_now, INTERVAL 364 DAY), @v_nguyen_no_dau, @seed_now, @v_nguyen_no_dau),
  (@vi_hist_44, @vr_hist_44, @campus_ct, 'VR-HIST-044-CT', DATE_ADD(DATE_ADD(DATE(@seed_now), INTERVAL -352 DAY), INTERVAL 540 MINUTE), DATE_ADD(DATE_ADD(DATE(@seed_now), INTERVAL -352 DAY), INTERVAL 990 MINUTE), 'CANCELLED', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, 0, DATE_SUB(@seed_now, INTERVAL 372 DAY), @v_lee, @seed_now, @v_lee);

INSERT INTO visit_guest_members (guest_member_id, visit_request_id, full_name, organization, job_title, nationality, email, phone, is_representative, note, created_at, created_by, updated_at, updated_by)
VALUES
  (NULL, @vr_pending_approval_seed, 'Nguyễn Thảo My', 'Tổ chức đối tác quốc tế', 'Trưởng đoàn', 'Quốc tế', 'thaomy.pending.approval@partner.example', '0900000000', TRUE, 'Đại diện chính của đoàn.', DATE_SUB(@seed_now, INTERVAL 17 DAY), @v_pending_approval_seed, @seed_now, @v_pending_approval_seed),
  (NULL, @vr_pending_approval_seed, 'Trợ lý điều phối Nguyễn', 'Tổ chức đối tác quốc tế', 'Coordinator', 'Quốc tế', NULL, NULL, FALSE, 'Khách phụ kiểm thử null hợp lệ.', DATE_SUB(@seed_now, INTERVAL 17 DAY), @v_pending_approval_seed, @seed_now, @v_pending_approval_seed),
  (NULL, @vr_pending_approval_multi, 'Nguyễn Văn Nam', 'Tổ chức đối tác quốc tế', 'Trưởng đoàn', 'Quốc tế', 'nam.pending.approval@partner.example', '0900000000', TRUE, 'Đại diện chính của đoàn.', DATE_SUB(@seed_now, INTERVAL 24 DAY), @v_pending_approval, @seed_now, @v_pending_approval),
  (NULL, @vr_pending_approval_multi, 'Trợ lý điều phối Nguyễn', 'Tổ chức đối tác quốc tế', 'Coordinator', 'Quốc tế', NULL, NULL, FALSE, 'Khách phụ kiểm thử null hợp lệ.', DATE_SUB(@seed_now, INTERVAL 24 DAY), @v_pending_approval, @seed_now, @v_pending_approval),
  (NULL, @vr_approved_single_before, 'Kim Min Seo', 'Tổ chức đối tác quốc tế', 'Trưởng đoàn', 'Quốc tế', 'kim.minseo@seoultech.example', '0900000000', TRUE, 'Đại diện chính của đoàn.', DATE_SUB(@seed_now, INTERVAL 29 DAY), @v_kim, @seed_now, @v_kim),
  (NULL, @vr_approved_single_before, 'Trợ lý điều phối Kim', 'Tổ chức đối tác quốc tế', 'Coordinator', 'Quốc tế', NULL, NULL, FALSE, 'Khách phụ kiểm thử null hợp lệ.', DATE_SUB(@seed_now, INTERVAL 29 DAY), @v_kim, @seed_now, @v_kim),
  (NULL, @vr_approved_multi_during, 'Lee Joon Ho', 'Tổ chức đối tác quốc tế', 'Trưởng đoàn', 'Quốc tế', 'lee.joonho@seoultech.example', '0900000000', TRUE, 'Đại diện chính của đoàn.', DATE_SUB(@seed_now, INTERVAL 15 DAY), @v_lee, @seed_now, @v_lee),
  (NULL, @vr_approved_multi_during, 'Trợ lý điều phối Lee', 'Tổ chức đối tác quốc tế', 'Coordinator', 'Quốc tế', NULL, NULL, FALSE, 'Khách phụ kiểm thử null hợp lệ.', DATE_SUB(@seed_now, INTERVAL 15 DAY), @v_lee, @seed_now, @v_lee),
  (NULL, @vr_rejected_single, 'Emily Smith', 'Tổ chức đối tác quốc tế', 'Trưởng đoàn', 'Quốc tế', 'emily.smith@greentech.example', '0900000000', TRUE, 'Đại diện chính của đoàn.', DATE_SUB(@seed_now, INTERVAL 20 DAY), @v_smith, @seed_now, @v_smith),
  (NULL, @vr_rejected_single, 'Trợ lý điều phối Emily', 'Tổ chức đối tác quốc tế', 'Coordinator', 'Quốc tế', NULL, NULL, FALSE, 'Khách phụ kiểm thử null hợp lệ.', DATE_SUB(@seed_now, INTERVAL 20 DAY), @v_smith, @seed_now, @v_smith),
  (NULL, @vr_rejected_multi, 'Tanaka Aoi', 'Tổ chức đối tác quốc tế', 'Trưởng đoàn', 'Quốc tế', 'aoi.tanaka@kyoto-global.example', '0900000000', TRUE, 'Đại diện chính của đoàn.', DATE_SUB(@seed_now, INTERVAL 35 DAY), @v_tanaka, @seed_now, @v_tanaka),
  (NULL, @vr_rejected_multi, 'Trợ lý điều phối Tanaka', 'Tổ chức đối tác quốc tế', 'Coordinator', 'Quốc tế', NULL, NULL, FALSE, 'Khách phụ kiểm thử null hợp lệ.', DATE_SUB(@seed_now, INTERVAL 35 DAY), @v_tanaka, @seed_now, @v_tanaka),
  (NULL, @vr_cancelled, 'Nguyen Van Nam', 'Tổ chức đối tác quốc tế', 'Trưởng đoàn', 'Quốc tế', 'nguyen.van.nam@partner.example', '0900000000', TRUE, 'Đại diện chính của đoàn.', DATE_SUB(@seed_now, INTERVAL 45 DAY), @v_nguyen_no_dau, @seed_now, @v_nguyen_no_dau),
  (NULL, @vr_cancelled, 'Trợ lý điều phối Nguyen', 'Tổ chức đối tác quốc tế', 'Coordinator', 'Quốc tế', NULL, NULL, FALSE, 'Khách phụ kiểm thử null hợp lệ.', DATE_SUB(@seed_now, INTERVAL 45 DAY), @v_nguyen_no_dau, @seed_now, @v_nguyen_no_dau),
  (NULL, @vr_after_visit, 'An', 'Tổ chức đối tác quốc tế', 'Trưởng đoàn', 'Quốc tế', 'an.short@partner.example', '0900000000', TRUE, 'Đại diện chính của đoàn.', DATE_SUB(@seed_now, INTERVAL 22 DAY), @v_short_name, @seed_now, @v_short_name),
  (NULL, @vr_after_visit, 'Trợ lý điều phối An', 'Tổ chức đối tác quốc tế', 'Coordinator', 'Quốc tế', NULL, NULL, FALSE, 'Khách phụ kiểm thử null hợp lệ.', DATE_SUB(@seed_now, INTERVAL 22 DAY), @v_short_name, @seed_now, @v_short_name),
  (NULL, @vr_closed, 'Nguyễn Thị Minh Anh Phương Khánh Linh Hoàng Bảo Trân Quốc Việt', 'Tổ chức đối tác quốc tế', 'Trưởng đoàn', 'Quốc tế', 'long.name.visitor@partner.example', '0900000000', TRUE, 'Đại diện chính của đoàn.', DATE_SUB(@seed_now, INTERVAL 50 DAY), @v_long_name, @seed_now, @v_long_name),
  (NULL, @vr_closed, 'Trợ lý điều phối Nguyễn', 'Tổ chức đối tác quốc tế', 'Coordinator', 'Quốc tế', NULL, NULL, FALSE, 'Khách phụ kiểm thử null hợp lệ.', DATE_SUB(@seed_now, INTERVAL 50 DAY), @v_long_name, @seed_now, @v_long_name),
  (NULL, @vr_cancelled_instance, 'Emily Smith', 'Tổ chức đối tác quốc tế', 'Trưởng đoàn', 'Quốc tế', 'emily.smith@greentech.example', '0900000000', TRUE, 'Đại diện chính của đoàn.', DATE_SUB(@seed_now, INTERVAL 40 DAY), @v_smith, @seed_now, @v_smith),
  (NULL, @vr_cancelled_instance, 'Trợ lý điều phối Emily', 'Tổ chức đối tác quốc tế', 'Coordinator', 'Quốc tế', NULL, NULL, FALSE, 'Khách phụ kiểm thử null hợp lệ.', DATE_SUB(@seed_now, INTERVAL 40 DAY), @v_smith, @seed_now, @v_smith),
  (NULL, @vr_assigned_only, 'Lee Joon Ho', 'Tổ chức đối tác quốc tế', 'Trưởng đoàn', 'Quốc tế', 'lee.joonho@seoultech.example', '0900000000', TRUE, 'Đại diện chính của đoàn.', DATE_SUB(@seed_now, INTERVAL 26 DAY), @v_lee, @seed_now, @v_lee),
  (NULL, @vr_assigned_only, 'Trợ lý điều phối Lee', 'Tổ chức đối tác quốc tế', 'Coordinator', 'Quốc tế', NULL, NULL, FALSE, 'Khách phụ kiểm thử null hợp lệ.', DATE_SUB(@seed_now, INTERVAL 26 DAY), @v_lee, @seed_now, @v_lee),
  (NULL, @vr_hist_01, 'Kim Min Seo', 'Tổ chức đối tác quốc tế', 'Trưởng đoàn', 'Quốc tế', 'kim.minseo@seoultech.example', '0900000000', TRUE, 'Đại diện chính của đoàn.', DATE_SUB(@seed_now, INTERVAL 28 DAY), @v_kim, @seed_now, @v_kim),
  (NULL, @vr_hist_01, 'Trợ lý điều phối Kim', 'Tổ chức đối tác quốc tế', 'Coordinator', 'Quốc tế', NULL, NULL, FALSE, 'Khách phụ kiểm thử null hợp lệ.', DATE_SUB(@seed_now, INTERVAL 28 DAY), @v_kim, @seed_now, @v_kim),
  (NULL, @vr_hist_02, 'Emily Smith', 'Tổ chức đối tác quốc tế', 'Trưởng đoàn', 'Quốc tế', 'emily.smith@greentech.example', '0900000000', TRUE, 'Đại diện chính của đoàn.', DATE_SUB(@seed_now, INTERVAL 36 DAY), @v_smith, @seed_now, @v_smith),
  (NULL, @vr_hist_02, 'Trợ lý điều phối Emily', 'Tổ chức đối tác quốc tế', 'Coordinator', 'Quốc tế', NULL, NULL, FALSE, 'Khách phụ kiểm thử null hợp lệ.', DATE_SUB(@seed_now, INTERVAL 36 DAY), @v_smith, @seed_now, @v_smith),
  (NULL, @vr_hist_03, 'Nguyen Van Nam', 'Tổ chức đối tác quốc tế', 'Trưởng đoàn', 'Quốc tế', 'nguyen.van.nam@partner.example', '0900000000', TRUE, 'Đại diện chính của đoàn.', DATE_SUB(@seed_now, INTERVAL 44 DAY), @v_nguyen_no_dau, @seed_now, @v_nguyen_no_dau),
  (NULL, @vr_hist_03, 'Trợ lý điều phối Nguyen', 'Tổ chức đối tác quốc tế', 'Coordinator', 'Quốc tế', NULL, NULL, FALSE, 'Khách phụ kiểm thử null hợp lệ.', DATE_SUB(@seed_now, INTERVAL 44 DAY), @v_nguyen_no_dau, @seed_now, @v_nguyen_no_dau),
  (NULL, @vr_hist_04, 'Lee Joon Ho', 'Tổ chức đối tác quốc tế', 'Trưởng đoàn', 'Quốc tế', 'lee.joonho@seoultech.example', '0900000000', TRUE, 'Đại diện chính của đoàn.', DATE_SUB(@seed_now, INTERVAL 52 DAY), @v_lee, @seed_now, @v_lee),
  (NULL, @vr_hist_04, 'Trợ lý điều phối Lee', 'Tổ chức đối tác quốc tế', 'Coordinator', 'Quốc tế', NULL, NULL, FALSE, 'Khách phụ kiểm thử null hợp lệ.', DATE_SUB(@seed_now, INTERVAL 52 DAY), @v_lee, @seed_now, @v_lee),
  (NULL, @vr_hist_05, 'Kim Min Seo', 'Tổ chức đối tác quốc tế', 'Trưởng đoàn', 'Quốc tế', 'kim.minseo@seoultech.example', '0900000000', TRUE, 'Đại diện chính của đoàn.', DATE_SUB(@seed_now, INTERVAL 60 DAY), @v_kim, @seed_now, @v_kim),
  (NULL, @vr_hist_05, 'Trợ lý điều phối Kim', 'Tổ chức đối tác quốc tế', 'Coordinator', 'Quốc tế', NULL, NULL, FALSE, 'Khách phụ kiểm thử null hợp lệ.', DATE_SUB(@seed_now, INTERVAL 60 DAY), @v_kim, @seed_now, @v_kim),
  (NULL, @vr_hist_06, 'Emily Smith', 'Tổ chức đối tác quốc tế', 'Trưởng đoàn', 'Quốc tế', 'emily.smith@greentech.example', '0900000000', TRUE, 'Đại diện chính của đoàn.', DATE_SUB(@seed_now, INTERVAL 68 DAY), @v_smith, @seed_now, @v_smith),
  (NULL, @vr_hist_06, 'Trợ lý điều phối Emily', 'Tổ chức đối tác quốc tế', 'Coordinator', 'Quốc tế', NULL, NULL, FALSE, 'Khách phụ kiểm thử null hợp lệ.', DATE_SUB(@seed_now, INTERVAL 68 DAY), @v_smith, @seed_now, @v_smith),
  (NULL, @vr_hist_07, 'Nguyen Van Nam', 'Tổ chức đối tác quốc tế', 'Trưởng đoàn', 'Quốc tế', 'nguyen.van.nam@partner.example', '0900000000', TRUE, 'Đại diện chính của đoàn.', DATE_SUB(@seed_now, INTERVAL 76 DAY), @v_nguyen_no_dau, @seed_now, @v_nguyen_no_dau),
  (NULL, @vr_hist_07, 'Trợ lý điều phối Nguyen', 'Tổ chức đối tác quốc tế', 'Coordinator', 'Quốc tế', NULL, NULL, FALSE, 'Khách phụ kiểm thử null hợp lệ.', DATE_SUB(@seed_now, INTERVAL 76 DAY), @v_nguyen_no_dau, @seed_now, @v_nguyen_no_dau),
  (NULL, @vr_hist_08, 'Lee Joon Ho', 'Tổ chức đối tác quốc tế', 'Trưởng đoàn', 'Quốc tế', 'lee.joonho@seoultech.example', '0900000000', TRUE, 'Đại diện chính của đoàn.', DATE_SUB(@seed_now, INTERVAL 84 DAY), @v_lee, @seed_now, @v_lee),
  (NULL, @vr_hist_08, 'Trợ lý điều phối Lee', 'Tổ chức đối tác quốc tế', 'Coordinator', 'Quốc tế', NULL, NULL, FALSE, 'Khách phụ kiểm thử null hợp lệ.', DATE_SUB(@seed_now, INTERVAL 84 DAY), @v_lee, @seed_now, @v_lee),
  (NULL, @vr_hist_09, 'Kim Min Seo', 'Tổ chức đối tác quốc tế', 'Trưởng đoàn', 'Quốc tế', 'kim.minseo@seoultech.example', '0900000000', TRUE, 'Đại diện chính của đoàn.', DATE_SUB(@seed_now, INTERVAL 92 DAY), @v_kim, @seed_now, @v_kim),
  (NULL, @vr_hist_09, 'Trợ lý điều phối Kim', 'Tổ chức đối tác quốc tế', 'Coordinator', 'Quốc tế', NULL, NULL, FALSE, 'Khách phụ kiểm thử null hợp lệ.', DATE_SUB(@seed_now, INTERVAL 92 DAY), @v_kim, @seed_now, @v_kim),
  (NULL, @vr_hist_10, 'Emily Smith', 'Tổ chức đối tác quốc tế', 'Trưởng đoàn', 'Quốc tế', 'emily.smith@greentech.example', '0900000000', TRUE, 'Đại diện chính của đoàn.', DATE_SUB(@seed_now, INTERVAL 100 DAY), @v_smith, @seed_now, @v_smith),
  (NULL, @vr_hist_10, 'Trợ lý điều phối Emily', 'Tổ chức đối tác quốc tế', 'Coordinator', 'Quốc tế', NULL, NULL, FALSE, 'Khách phụ kiểm thử null hợp lệ.', DATE_SUB(@seed_now, INTERVAL 100 DAY), @v_smith, @seed_now, @v_smith),
  (NULL, @vr_hist_11, 'Nguyen Van Nam', 'Tổ chức đối tác quốc tế', 'Trưởng đoàn', 'Quốc tế', 'nguyen.van.nam@partner.example', '0900000000', TRUE, 'Đại diện chính của đoàn.', DATE_SUB(@seed_now, INTERVAL 108 DAY), @v_nguyen_no_dau, @seed_now, @v_nguyen_no_dau),
  (NULL, @vr_hist_11, 'Trợ lý điều phối Nguyen', 'Tổ chức đối tác quốc tế', 'Coordinator', 'Quốc tế', NULL, NULL, FALSE, 'Khách phụ kiểm thử null hợp lệ.', DATE_SUB(@seed_now, INTERVAL 108 DAY), @v_nguyen_no_dau, @seed_now, @v_nguyen_no_dau),
  (NULL, @vr_hist_12, 'Lee Joon Ho', 'Tổ chức đối tác quốc tế', 'Trưởng đoàn', 'Quốc tế', 'lee.joonho@seoultech.example', '0900000000', TRUE, 'Đại diện chính của đoàn.', DATE_SUB(@seed_now, INTERVAL 116 DAY), @v_lee, @seed_now, @v_lee),
  (NULL, @vr_hist_12, 'Trợ lý điều phối Lee', 'Tổ chức đối tác quốc tế', 'Coordinator', 'Quốc tế', NULL, NULL, FALSE, 'Khách phụ kiểm thử null hợp lệ.', DATE_SUB(@seed_now, INTERVAL 116 DAY), @v_lee, @seed_now, @v_lee),
  (NULL, @vr_hist_13, 'Kim Min Seo', 'Tổ chức đối tác quốc tế', 'Trưởng đoàn', 'Quốc tế', 'kim.minseo@seoultech.example', '0900000000', TRUE, 'Đại diện chính của đoàn.', DATE_SUB(@seed_now, INTERVAL 124 DAY), @v_kim, @seed_now, @v_kim),
  (NULL, @vr_hist_13, 'Trợ lý điều phối Kim', 'Tổ chức đối tác quốc tế', 'Coordinator', 'Quốc tế', NULL, NULL, FALSE, 'Khách phụ kiểm thử null hợp lệ.', DATE_SUB(@seed_now, INTERVAL 124 DAY), @v_kim, @seed_now, @v_kim),
  (NULL, @vr_hist_14, 'Emily Smith', 'Tổ chức đối tác quốc tế', 'Trưởng đoàn', 'Quốc tế', 'emily.smith@greentech.example', '0900000000', TRUE, 'Đại diện chính của đoàn.', DATE_SUB(@seed_now, INTERVAL 132 DAY), @v_smith, @seed_now, @v_smith),
  (NULL, @vr_hist_14, 'Trợ lý điều phối Emily', 'Tổ chức đối tác quốc tế', 'Coordinator', 'Quốc tế', NULL, NULL, FALSE, 'Khách phụ kiểm thử null hợp lệ.', DATE_SUB(@seed_now, INTERVAL 132 DAY), @v_smith, @seed_now, @v_smith),
  (NULL, @vr_hist_15, 'Nguyen Van Nam', 'Tổ chức đối tác quốc tế', 'Trưởng đoàn', 'Quốc tế', 'nguyen.van.nam@partner.example', '0900000000', TRUE, 'Đại diện chính của đoàn.', DATE_SUB(@seed_now, INTERVAL 140 DAY), @v_nguyen_no_dau, @seed_now, @v_nguyen_no_dau),
  (NULL, @vr_hist_15, 'Trợ lý điều phối Nguyen', 'Tổ chức đối tác quốc tế', 'Coordinator', 'Quốc tế', NULL, NULL, FALSE, 'Khách phụ kiểm thử null hợp lệ.', DATE_SUB(@seed_now, INTERVAL 140 DAY), @v_nguyen_no_dau, @seed_now, @v_nguyen_no_dau),
  (NULL, @vr_hist_16, 'Lee Joon Ho', 'Tổ chức đối tác quốc tế', 'Trưởng đoàn', 'Quốc tế', 'lee.joonho@seoultech.example', '0900000000', TRUE, 'Đại diện chính của đoàn.', DATE_SUB(@seed_now, INTERVAL 148 DAY), @v_lee, @seed_now, @v_lee),
  (NULL, @vr_hist_16, 'Trợ lý điều phối Lee', 'Tổ chức đối tác quốc tế', 'Coordinator', 'Quốc tế', NULL, NULL, FALSE, 'Khách phụ kiểm thử null hợp lệ.', DATE_SUB(@seed_now, INTERVAL 148 DAY), @v_lee, @seed_now, @v_lee),
  (NULL, @vr_hist_17, 'Kim Min Seo', 'Tổ chức đối tác quốc tế', 'Trưởng đoàn', 'Quốc tế', 'kim.minseo@seoultech.example', '0900000000', TRUE, 'Đại diện chính của đoàn.', DATE_SUB(@seed_now, INTERVAL 156 DAY), @v_kim, @seed_now, @v_kim),
  (NULL, @vr_hist_17, 'Trợ lý điều phối Kim', 'Tổ chức đối tác quốc tế', 'Coordinator', 'Quốc tế', NULL, NULL, FALSE, 'Khách phụ kiểm thử null hợp lệ.', DATE_SUB(@seed_now, INTERVAL 156 DAY), @v_kim, @seed_now, @v_kim),
  (NULL, @vr_hist_18, 'Emily Smith', 'Tổ chức đối tác quốc tế', 'Trưởng đoàn', 'Quốc tế', 'emily.smith@greentech.example', '0900000000', TRUE, 'Đại diện chính của đoàn.', DATE_SUB(@seed_now, INTERVAL 164 DAY), @v_smith, @seed_now, @v_smith),
  (NULL, @vr_hist_18, 'Trợ lý điều phối Emily', 'Tổ chức đối tác quốc tế', 'Coordinator', 'Quốc tế', NULL, NULL, FALSE, 'Khách phụ kiểm thử null hợp lệ.', DATE_SUB(@seed_now, INTERVAL 164 DAY), @v_smith, @seed_now, @v_smith),
  (NULL, @vr_hist_19, 'Nguyen Van Nam', 'Tổ chức đối tác quốc tế', 'Trưởng đoàn', 'Quốc tế', 'nguyen.van.nam@partner.example', '0900000000', TRUE, 'Đại diện chính của đoàn.', DATE_SUB(@seed_now, INTERVAL 172 DAY), @v_nguyen_no_dau, @seed_now, @v_nguyen_no_dau),
  (NULL, @vr_hist_19, 'Trợ lý điều phối Nguyen', 'Tổ chức đối tác quốc tế', 'Coordinator', 'Quốc tế', NULL, NULL, FALSE, 'Khách phụ kiểm thử null hợp lệ.', DATE_SUB(@seed_now, INTERVAL 172 DAY), @v_nguyen_no_dau, @seed_now, @v_nguyen_no_dau),
  (NULL, @vr_hist_20, 'Lee Joon Ho', 'Tổ chức đối tác quốc tế', 'Trưởng đoàn', 'Quốc tế', 'lee.joonho@seoultech.example', '0900000000', TRUE, 'Đại diện chính của đoàn.', DATE_SUB(@seed_now, INTERVAL 180 DAY), @v_lee, @seed_now, @v_lee),
  (NULL, @vr_hist_20, 'Trợ lý điều phối Lee', 'Tổ chức đối tác quốc tế', 'Coordinator', 'Quốc tế', NULL, NULL, FALSE, 'Khách phụ kiểm thử null hợp lệ.', DATE_SUB(@seed_now, INTERVAL 180 DAY), @v_lee, @seed_now, @v_lee),
  (NULL, @vr_hist_21, 'Kim Min Seo', 'Tổ chức đối tác quốc tế', 'Trưởng đoàn', 'Quốc tế', 'kim.minseo@seoultech.example', '0900000000', TRUE, 'Đại diện chính của đoàn.', DATE_SUB(@seed_now, INTERVAL 188 DAY), @v_kim, @seed_now, @v_kim),
  (NULL, @vr_hist_21, 'Trợ lý điều phối Kim', 'Tổ chức đối tác quốc tế', 'Coordinator', 'Quốc tế', NULL, NULL, FALSE, 'Khách phụ kiểm thử null hợp lệ.', DATE_SUB(@seed_now, INTERVAL 188 DAY), @v_kim, @seed_now, @v_kim),
  (NULL, @vr_hist_22, 'Emily Smith', 'Tổ chức đối tác quốc tế', 'Trưởng đoàn', 'Quốc tế', 'emily.smith@greentech.example', '0900000000', TRUE, 'Đại diện chính của đoàn.', DATE_SUB(@seed_now, INTERVAL 196 DAY), @v_smith, @seed_now, @v_smith),
  (NULL, @vr_hist_22, 'Trợ lý điều phối Emily', 'Tổ chức đối tác quốc tế', 'Coordinator', 'Quốc tế', NULL, NULL, FALSE, 'Khách phụ kiểm thử null hợp lệ.', DATE_SUB(@seed_now, INTERVAL 196 DAY), @v_smith, @seed_now, @v_smith),
  (NULL, @vr_hist_23, 'Nguyen Van Nam', 'Tổ chức đối tác quốc tế', 'Trưởng đoàn', 'Quốc tế', 'nguyen.van.nam@partner.example', '0900000000', TRUE, 'Đại diện chính của đoàn.', DATE_SUB(@seed_now, INTERVAL 204 DAY), @v_nguyen_no_dau, @seed_now, @v_nguyen_no_dau),
  (NULL, @vr_hist_23, 'Trợ lý điều phối Nguyen', 'Tổ chức đối tác quốc tế', 'Coordinator', 'Quốc tế', NULL, NULL, FALSE, 'Khách phụ kiểm thử null hợp lệ.', DATE_SUB(@seed_now, INTERVAL 204 DAY), @v_nguyen_no_dau, @seed_now, @v_nguyen_no_dau),
  (NULL, @vr_hist_24, 'Lee Joon Ho', 'Tổ chức đối tác quốc tế', 'Trưởng đoàn', 'Quốc tế', 'lee.joonho@seoultech.example', '0900000000', TRUE, 'Đại diện chính của đoàn.', DATE_SUB(@seed_now, INTERVAL 212 DAY), @v_lee, @seed_now, @v_lee),
  (NULL, @vr_hist_24, 'Trợ lý điều phối Lee', 'Tổ chức đối tác quốc tế', 'Coordinator', 'Quốc tế', NULL, NULL, FALSE, 'Khách phụ kiểm thử null hợp lệ.', DATE_SUB(@seed_now, INTERVAL 212 DAY), @v_lee, @seed_now, @v_lee),
  (NULL, @vr_hist_25, 'Kim Min Seo', 'Tổ chức đối tác quốc tế', 'Trưởng đoàn', 'Quốc tế', 'kim.minseo@seoultech.example', '0900000000', TRUE, 'Đại diện chính của đoàn.', DATE_SUB(@seed_now, INTERVAL 220 DAY), @v_kim, @seed_now, @v_kim),
  (NULL, @vr_hist_25, 'Trợ lý điều phối Kim', 'Tổ chức đối tác quốc tế', 'Coordinator', 'Quốc tế', NULL, NULL, FALSE, 'Khách phụ kiểm thử null hợp lệ.', DATE_SUB(@seed_now, INTERVAL 220 DAY), @v_kim, @seed_now, @v_kim),
  (NULL, @vr_hist_26, 'Emily Smith', 'Tổ chức đối tác quốc tế', 'Trưởng đoàn', 'Quốc tế', 'emily.smith@greentech.example', '0900000000', TRUE, 'Đại diện chính của đoàn.', DATE_SUB(@seed_now, INTERVAL 228 DAY), @v_smith, @seed_now, @v_smith),
  (NULL, @vr_hist_26, 'Trợ lý điều phối Emily', 'Tổ chức đối tác quốc tế', 'Coordinator', 'Quốc tế', NULL, NULL, FALSE, 'Khách phụ kiểm thử null hợp lệ.', DATE_SUB(@seed_now, INTERVAL 228 DAY), @v_smith, @seed_now, @v_smith),
  (NULL, @vr_hist_27, 'Nguyen Van Nam', 'Tổ chức đối tác quốc tế', 'Trưởng đoàn', 'Quốc tế', 'nguyen.van.nam@partner.example', '0900000000', TRUE, 'Đại diện chính của đoàn.', DATE_SUB(@seed_now, INTERVAL 236 DAY), @v_nguyen_no_dau, @seed_now, @v_nguyen_no_dau),
  (NULL, @vr_hist_27, 'Trợ lý điều phối Nguyen', 'Tổ chức đối tác quốc tế', 'Coordinator', 'Quốc tế', NULL, NULL, FALSE, 'Khách phụ kiểm thử null hợp lệ.', DATE_SUB(@seed_now, INTERVAL 236 DAY), @v_nguyen_no_dau, @seed_now, @v_nguyen_no_dau),
  (NULL, @vr_hist_28, 'Lee Joon Ho', 'Tổ chức đối tác quốc tế', 'Trưởng đoàn', 'Quốc tế', 'lee.joonho@seoultech.example', '0900000000', TRUE, 'Đại diện chính của đoàn.', DATE_SUB(@seed_now, INTERVAL 244 DAY), @v_lee, @seed_now, @v_lee),
  (NULL, @vr_hist_28, 'Trợ lý điều phối Lee', 'Tổ chức đối tác quốc tế', 'Coordinator', 'Quốc tế', NULL, NULL, FALSE, 'Khách phụ kiểm thử null hợp lệ.', DATE_SUB(@seed_now, INTERVAL 244 DAY), @v_lee, @seed_now, @v_lee),
  (NULL, @vr_hist_29, 'Kim Min Seo', 'Tổ chức đối tác quốc tế', 'Trưởng đoàn', 'Quốc tế', 'kim.minseo@seoultech.example', '0900000000', TRUE, 'Đại diện chính của đoàn.', DATE_SUB(@seed_now, INTERVAL 252 DAY), @v_kim, @seed_now, @v_kim),
  (NULL, @vr_hist_29, 'Trợ lý điều phối Kim', 'Tổ chức đối tác quốc tế', 'Coordinator', 'Quốc tế', NULL, NULL, FALSE, 'Khách phụ kiểm thử null hợp lệ.', DATE_SUB(@seed_now, INTERVAL 252 DAY), @v_kim, @seed_now, @v_kim),
  (NULL, @vr_hist_30, 'Emily Smith', 'Tổ chức đối tác quốc tế', 'Trưởng đoàn', 'Quốc tế', 'emily.smith@greentech.example', '0900000000', TRUE, 'Đại diện chính của đoàn.', DATE_SUB(@seed_now, INTERVAL 260 DAY), @v_smith, @seed_now, @v_smith),
  (NULL, @vr_hist_30, 'Trợ lý điều phối Emily', 'Tổ chức đối tác quốc tế', 'Coordinator', 'Quốc tế', NULL, NULL, FALSE, 'Khách phụ kiểm thử null hợp lệ.', DATE_SUB(@seed_now, INTERVAL 260 DAY), @v_smith, @seed_now, @v_smith),
  (NULL, @vr_hist_31, 'Nguyen Van Nam', 'Tổ chức đối tác quốc tế', 'Trưởng đoàn', 'Quốc tế', 'nguyen.van.nam@partner.example', '0900000000', TRUE, 'Đại diện chính của đoàn.', DATE_SUB(@seed_now, INTERVAL 268 DAY), @v_nguyen_no_dau, @seed_now, @v_nguyen_no_dau),
  (NULL, @vr_hist_31, 'Trợ lý điều phối Nguyen', 'Tổ chức đối tác quốc tế', 'Coordinator', 'Quốc tế', NULL, NULL, FALSE, 'Khách phụ kiểm thử null hợp lệ.', DATE_SUB(@seed_now, INTERVAL 268 DAY), @v_nguyen_no_dau, @seed_now, @v_nguyen_no_dau),
  (NULL, @vr_hist_32, 'Lee Joon Ho', 'Tổ chức đối tác quốc tế', 'Trưởng đoàn', 'Quốc tế', 'lee.joonho@seoultech.example', '0900000000', TRUE, 'Đại diện chính của đoàn.', DATE_SUB(@seed_now, INTERVAL 276 DAY), @v_lee, @seed_now, @v_lee),
  (NULL, @vr_hist_32, 'Trợ lý điều phối Lee', 'Tổ chức đối tác quốc tế', 'Coordinator', 'Quốc tế', NULL, NULL, FALSE, 'Khách phụ kiểm thử null hợp lệ.', DATE_SUB(@seed_now, INTERVAL 276 DAY), @v_lee, @seed_now, @v_lee),
  (NULL, @vr_hist_33, 'Kim Min Seo', 'Tổ chức đối tác quốc tế', 'Trưởng đoàn', 'Quốc tế', 'kim.minseo@seoultech.example', '0900000000', TRUE, 'Đại diện chính của đoàn.', DATE_SUB(@seed_now, INTERVAL 284 DAY), @v_kim, @seed_now, @v_kim),
  (NULL, @vr_hist_33, 'Trợ lý điều phối Kim', 'Tổ chức đối tác quốc tế', 'Coordinator', 'Quốc tế', NULL, NULL, FALSE, 'Khách phụ kiểm thử null hợp lệ.', DATE_SUB(@seed_now, INTERVAL 284 DAY), @v_kim, @seed_now, @v_kim),
  (NULL, @vr_hist_34, 'Emily Smith', 'Tổ chức đối tác quốc tế', 'Trưởng đoàn', 'Quốc tế', 'emily.smith@greentech.example', '0900000000', TRUE, 'Đại diện chính của đoàn.', DATE_SUB(@seed_now, INTERVAL 292 DAY), @v_smith, @seed_now, @v_smith),
  (NULL, @vr_hist_34, 'Trợ lý điều phối Emily', 'Tổ chức đối tác quốc tế', 'Coordinator', 'Quốc tế', NULL, NULL, FALSE, 'Khách phụ kiểm thử null hợp lệ.', DATE_SUB(@seed_now, INTERVAL 292 DAY), @v_smith, @seed_now, @v_smith),
  (NULL, @vr_hist_35, 'Nguyen Van Nam', 'Tổ chức đối tác quốc tế', 'Trưởng đoàn', 'Quốc tế', 'nguyen.van.nam@partner.example', '0900000000', TRUE, 'Đại diện chính của đoàn.', DATE_SUB(@seed_now, INTERVAL 300 DAY), @v_nguyen_no_dau, @seed_now, @v_nguyen_no_dau),
  (NULL, @vr_hist_35, 'Trợ lý điều phối Nguyen', 'Tổ chức đối tác quốc tế', 'Coordinator', 'Quốc tế', NULL, NULL, FALSE, 'Khách phụ kiểm thử null hợp lệ.', DATE_SUB(@seed_now, INTERVAL 300 DAY), @v_nguyen_no_dau, @seed_now, @v_nguyen_no_dau),
  (NULL, @vr_hist_36, 'Lee Joon Ho', 'Tổ chức đối tác quốc tế', 'Trưởng đoàn', 'Quốc tế', 'lee.joonho@seoultech.example', '0900000000', TRUE, 'Đại diện chính của đoàn.', DATE_SUB(@seed_now, INTERVAL 308 DAY), @v_lee, @seed_now, @v_lee),
  (NULL, @vr_hist_36, 'Trợ lý điều phối Lee', 'Tổ chức đối tác quốc tế', 'Coordinator', 'Quốc tế', NULL, NULL, FALSE, 'Khách phụ kiểm thử null hợp lệ.', DATE_SUB(@seed_now, INTERVAL 308 DAY), @v_lee, @seed_now, @v_lee),
  (NULL, @vr_hist_37, 'Kim Min Seo', 'Tổ chức đối tác quốc tế', 'Trưởng đoàn', 'Quốc tế', 'kim.minseo@seoultech.example', '0900000000', TRUE, 'Đại diện chính của đoàn.', DATE_SUB(@seed_now, INTERVAL 316 DAY), @v_kim, @seed_now, @v_kim),
  (NULL, @vr_hist_37, 'Trợ lý điều phối Kim', 'Tổ chức đối tác quốc tế', 'Coordinator', 'Quốc tế', NULL, NULL, FALSE, 'Khách phụ kiểm thử null hợp lệ.', DATE_SUB(@seed_now, INTERVAL 316 DAY), @v_kim, @seed_now, @v_kim),
  (NULL, @vr_hist_38, 'Emily Smith', 'Tổ chức đối tác quốc tế', 'Trưởng đoàn', 'Quốc tế', 'emily.smith@greentech.example', '0900000000', TRUE, 'Đại diện chính của đoàn.', DATE_SUB(@seed_now, INTERVAL 324 DAY), @v_smith, @seed_now, @v_smith),
  (NULL, @vr_hist_38, 'Trợ lý điều phối Emily', 'Tổ chức đối tác quốc tế', 'Coordinator', 'Quốc tế', NULL, NULL, FALSE, 'Khách phụ kiểm thử null hợp lệ.', DATE_SUB(@seed_now, INTERVAL 324 DAY), @v_smith, @seed_now, @v_smith),
  (NULL, @vr_hist_39, 'Nguyen Van Nam', 'Tổ chức đối tác quốc tế', 'Trưởng đoàn', 'Quốc tế', 'nguyen.van.nam@partner.example', '0900000000', TRUE, 'Đại diện chính của đoàn.', DATE_SUB(@seed_now, INTERVAL 332 DAY), @v_nguyen_no_dau, @seed_now, @v_nguyen_no_dau),
  (NULL, @vr_hist_39, 'Trợ lý điều phối Nguyen', 'Tổ chức đối tác quốc tế', 'Coordinator', 'Quốc tế', NULL, NULL, FALSE, 'Khách phụ kiểm thử null hợp lệ.', DATE_SUB(@seed_now, INTERVAL 332 DAY), @v_nguyen_no_dau, @seed_now, @v_nguyen_no_dau),
  (NULL, @vr_hist_40, 'Lee Joon Ho', 'Tổ chức đối tác quốc tế', 'Trưởng đoàn', 'Quốc tế', 'lee.joonho@seoultech.example', '0900000000', TRUE, 'Đại diện chính của đoàn.', DATE_SUB(@seed_now, INTERVAL 340 DAY), @v_lee, @seed_now, @v_lee),
  (NULL, @vr_hist_40, 'Trợ lý điều phối Lee', 'Tổ chức đối tác quốc tế', 'Coordinator', 'Quốc tế', NULL, NULL, FALSE, 'Khách phụ kiểm thử null hợp lệ.', DATE_SUB(@seed_now, INTERVAL 340 DAY), @v_lee, @seed_now, @v_lee),
  (NULL, @vr_hist_41, 'Kim Min Seo', 'Tổ chức đối tác quốc tế', 'Trưởng đoàn', 'Quốc tế', 'kim.minseo@seoultech.example', '0900000000', TRUE, 'Đại diện chính của đoàn.', DATE_SUB(@seed_now, INTERVAL 348 DAY), @v_kim, @seed_now, @v_kim),
  (NULL, @vr_hist_41, 'Trợ lý điều phối Kim', 'Tổ chức đối tác quốc tế', 'Coordinator', 'Quốc tế', NULL, NULL, FALSE, 'Khách phụ kiểm thử null hợp lệ.', DATE_SUB(@seed_now, INTERVAL 348 DAY), @v_kim, @seed_now, @v_kim),
  (NULL, @vr_hist_42, 'Emily Smith', 'Tổ chức đối tác quốc tế', 'Trưởng đoàn', 'Quốc tế', 'emily.smith@greentech.example', '0900000000', TRUE, 'Đại diện chính của đoàn.', DATE_SUB(@seed_now, INTERVAL 356 DAY), @v_smith, @seed_now, @v_smith),
  (NULL, @vr_hist_42, 'Trợ lý điều phối Emily', 'Tổ chức đối tác quốc tế', 'Coordinator', 'Quốc tế', NULL, NULL, FALSE, 'Khách phụ kiểm thử null hợp lệ.', DATE_SUB(@seed_now, INTERVAL 356 DAY), @v_smith, @seed_now, @v_smith),
  (NULL, @vr_hist_43, 'Nguyen Van Nam', 'Tổ chức đối tác quốc tế', 'Trưởng đoàn', 'Quốc tế', 'nguyen.van.nam@partner.example', '0900000000', TRUE, 'Đại diện chính của đoàn.', DATE_SUB(@seed_now, INTERVAL 364 DAY), @v_nguyen_no_dau, @seed_now, @v_nguyen_no_dau),
  (NULL, @vr_hist_43, 'Trợ lý điều phối Nguyen', 'Tổ chức đối tác quốc tế', 'Coordinator', 'Quốc tế', NULL, NULL, FALSE, 'Khách phụ kiểm thử null hợp lệ.', DATE_SUB(@seed_now, INTERVAL 364 DAY), @v_nguyen_no_dau, @seed_now, @v_nguyen_no_dau),
  (NULL, @vr_hist_44, 'Lee Joon Ho', 'Tổ chức đối tác quốc tế', 'Trưởng đoàn', 'Quốc tế', 'lee.joonho@seoultech.example', '0900000000', TRUE, 'Đại diện chính của đoàn.', DATE_SUB(@seed_now, INTERVAL 372 DAY), @v_lee, @seed_now, @v_lee),
  (NULL, @vr_hist_44, 'Trợ lý điều phối Lee', 'Tổ chức đối tác quốc tế', 'Coordinator', 'Quốc tế', NULL, NULL, FALSE, 'Khách phụ kiểm thử null hợp lệ.', DATE_SUB(@seed_now, INTERVAL 372 DAY), @v_lee, @seed_now, @v_lee);

INSERT INTO visit_participants (participant_id, visit_instance_id, user_id, participant_role, is_host, status, invited_by, invited_at, responded_at, assigned_by, assigned_at, note, created_at, created_by, updated_at, updated_by)
VALUES
  (NULL, @vi_as_hn, @u_stafflead_hn, 'IC_HOST', TRUE, 'ACCEPTED', @u_stafflead_hn, DATE_SUB(@seed_now, INTERVAL 10 DAY), DATE_SUB(@seed_now, INTERVAL 9 DAY), @u_stafflead_hn, DATE_SUB(@seed_now, INTERVAL 10 DAY), 'Host chính.', DATE_SUB(@seed_now, INTERVAL 10 DAY), @u_stafflead_hn, @seed_now, @u_stafflead_hn),
  (NULL, @vi_as_hn, @u_staff_hn, 'IC_SUPPORT', FALSE, 'INVITED', @u_stafflead_hn, DATE_SUB(@seed_now, INTERVAL 2 DAY), NULL, NULL, NULL, 'Chờ xác nhận.', DATE_SUB(@seed_now, INTERVAL 2 DAY), @u_stafflead_hn, NULL, NULL),
  (NULL, @vi_as_hn, @u_dept_it_hn, 'DEPT_SUPPORT', FALSE, 'ASSIGNED', @u_stafflead_hn, DATE_SUB(@seed_now, INTERVAL 9 DAY), NULL, @u_deptlead_it_hn, DATE_SUB(@seed_now, INTERVAL 8 DAY), 'Chuẩn bị thiết bị.', DATE_SUB(@seed_now, INTERVAL 9 DAY), @u_stafflead_hn, @seed_now, @u_deptlead_it_hn),
  (NULL, @vi_as_hn, @u_student_anh, 'STUDENT', FALSE, 'ACCEPTED', @u_staff_hn, DATE_SUB(@seed_now, INTERVAL 7 DAY), DATE_SUB(@seed_now, INTERVAL 6 DAY), @u_staff_hn, DATE_SUB(@seed_now, INTERVAL 7 DAY), 'Sinh viên hỗ trợ.', DATE_SUB(@seed_now, INTERVAL 7 DAY), @u_staff_hn, @seed_now, @u_student_anh),
  (NULL, @vi_am_hn, @u_student_bao, 'STUDENT', FALSE, 'ACCEPTED', @u_stafflead_hn, DATE_SUB(@seed_now, INTERVAL 4 DAY), DATE_SUB(@seed_now, INTERVAL 3 DAY), @u_stafflead_hn, DATE_SUB(@seed_now, INTERVAL 4 DAY), 'Sinh viên hỗ trợ.', DATE_SUB(@seed_now, INTERVAL 4 DAY), @u_stafflead_hn, @seed_now, @u_student_bao),
  (NULL, @vi_am_hcm, @u_dept_finance_hcm, 'DEPT_SUPPORT', FALSE, 'DECLINED', @u_stafflead_hcm, DATE_SUB(@seed_now, INTERVAL 3 DAY), DATE_SUB(@seed_now, INTERVAL 2 DAY), NULL, NULL, 'Trùng lịch.', DATE_SUB(@seed_now, INTERVAL 3 DAY), @u_stafflead_hcm, @seed_now, @u_dept_finance_hcm),
  (NULL, @vi_ci_hcm, @u_student_long, 'STUDENT', FALSE, 'REMOVED', @u_staff_hcm, DATE_SUB(@seed_now, INTERVAL 5 DAY), DATE_SUB(@seed_now, INTERVAL 4 DAY), @u_staff_hcm, DATE_SUB(@seed_now, INTERVAL 5 DAY), 'Đã gỡ khỏi danh sách sinh viên hỗ trợ.', DATE_SUB(@seed_now, INTERVAL 5 DAY), @u_staff_hcm, @seed_now, @u_staff_hcm);

INSERT INTO visit_agendas (agenda_id, visit_instance_id, sequence_order, title, description, start_time, end_time, location, responsible_user_id, created_at, created_by, updated_at, updated_by)
VALUES
  (NULL, @vi_as_hn, 1, 'Đón đoàn tại cổng chính', 'IC host và student buddy đón đoàn.', DATE_ADD(DATE_ADD(DATE(@seed_now), INTERVAL 14 DAY), INTERVAL 525 MINUTE), DATE_ADD(DATE_ADD(DATE(@seed_now), INTERVAL 14 DAY), INTERVAL 555 MINUTE), 'Cổng chính FPTU Hà Nội', @u_stafflead_hn, DATE_SUB(@seed_now, INTERVAL 7 DAY), @u_stafflead_hn, @seed_now, @u_stafflead_hn),
  (NULL, @vi_as_hn, 2, 'Giới thiệu FPTU và chương trình hợp tác', 'Trình bày tổng quan.', DATE_ADD(DATE_ADD(DATE(@seed_now), INTERVAL 14 DAY), INTERVAL 570 MINUTE), DATE_ADD(DATE_ADD(DATE(@seed_now), INTERVAL 14 DAY), INTERVAL 660 MINUTE), 'Phòng họp Alpha', @u_staff_hn, DATE_SUB(@seed_now, INTERVAL 7 DAY), @u_stafflead_hn, @seed_now, @u_stafflead_hn),
  (NULL, @vi_am_hn, 1, 'Check-in đoàn liên cơ sở', 'Đang diễn ra tại Hà Nội.', DATE_ADD(DATE_ADD(DATE(@seed_now), INTERVAL 0 DAY), INTERVAL 540 MINUTE), DATE_ADD(DATE_ADD(DATE(@seed_now), INTERVAL 0 DAY), INTERVAL 600 MINUTE), 'Sảnh Alpha', @u_stafflead_hn, DATE_SUB(@seed_now, INTERVAL 3 DAY), @u_stafflead_hn, @seed_now, @u_stafflead_hn),
  (NULL, @vi_av_ct, 1, 'Tổng kết sau visit Cần Thơ', 'Chờ gửi biên bản.', DATE_ADD(DATE_ADD(DATE(@seed_now), INTERVAL -2 DAY), INTERVAL 900 MINUTE), DATE_ADD(DATE_ADD(DATE(@seed_now), INTERVAL -2 DAY), INTERVAL 960 MINUTE), 'Phòng họp Cần Thơ 2', @u_stafflead_ct, DATE_SUB(@seed_now, INTERVAL 5 DAY), @u_stafflead_ct, @seed_now, @u_stafflead_ct);

SET @log_1 = 100209;

SET @log_2 = 100210;

SET @log_3 = 100211;

SET @log_4 = 100212;

SET @log_5 = 100213;

SET @log_6 = 100214;

SET @log_7 = 100215;

SET @log_8 = 100216;

SET @log_9 = 100217;

SET @log_10 = 100218;

SET @log_11 = 100219;

INSERT INTO visit_logistics_items (logistics_item_id, visit_instance_id, item_type, title, description, quantity, usage_start_at, usage_end_at, status, priority, requested_by, requested_to_department_id, requested_at, received_by, received_at, assigned_to_user_id, assigned_by, assigned_at, assignee_accepted_at, assignee_response_note, due_at, completed_at, proposed_by, proposed_at, proposed_quantity, proposed_usage_start_at, proposed_usage_end_at, proposed_description, proposal_note, proposal_responded_by, proposal_responded_at, proposal_response, proposal_response_note, decision_note, row_version, created_at, created_by, updated_at, updated_by)
VALUES
  (@log_1, @vi_as_hn, 'ROOM', 'Hạng mục hậu cần PLANNED', 'Nội dung thực tế cho trạng thái PLANNED.', 1, DATE_ADD(DATE_ADD(DATE(@seed_now), INTERVAL 14 DAY), INTERVAL 540 MINUTE), DATE_ADD(DATE_ADD(DATE(@seed_now), INTERVAL 14 DAY), INTERVAL 960 MINUTE), 'PLANNED', 'MEDIUM', @u_staff_hn, @dept_hn_it, DATE_SUB(@seed_now, INTERVAL 7 DAY), NULL, NULL, NULL, NULL, NULL, NULL, NULL, DATE_ADD(DATE_ADD(DATE(@seed_now), INTERVAL 13 DAY), INTERVAL 1020 MINUTE), NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, 1, DATE_SUB(@seed_now, INTERVAL 7 DAY), @u_staff_hn, @seed_now, @u_staff_hn),
  (@log_2, @vi_as_hn, 'TRANSPORT', 'Hạng mục hậu cần REQUESTED', 'Nội dung thực tế cho trạng thái REQUESTED.', 2, DATE_ADD(DATE_ADD(DATE(@seed_now), INTERVAL 14 DAY), INTERVAL 540 MINUTE), DATE_ADD(DATE_ADD(DATE(@seed_now), INTERVAL 14 DAY), INTERVAL 960 MINUTE), 'REQUESTED', 'HIGH', @u_staff_hn, @dept_hn_it, DATE_SUB(@seed_now, INTERVAL 7 DAY), NULL, NULL, NULL, NULL, NULL, NULL, NULL, DATE_ADD(DATE_ADD(DATE(@seed_now), INTERVAL 13 DAY), INTERVAL 1020 MINUTE), NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, 2, DATE_SUB(@seed_now, INTERVAL 7 DAY), @u_staff_hn, @seed_now, @u_staff_hn),
  (@log_3, @vi_ci_hcm, 'MEAL', 'Hạng mục hậu cần CHANGE_PROPOSED', 'Nội dung thực tế cho trạng thái CHANGE_PROPOSED.', 3, DATE_ADD(DATE_ADD(DATE(@seed_now), INTERVAL 14 DAY), INTERVAL 540 MINUTE), DATE_ADD(DATE_ADD(DATE(@seed_now), INTERVAL 14 DAY), INTERVAL 960 MINUTE), 'CHANGE_PROPOSED', 'URGENT', @u_staff_hn, @dept_hn_it, DATE_SUB(@seed_now, INTERVAL 7 DAY), @u_deptlead_it_hn, DATE_SUB(@seed_now, INTERVAL 6 DAY), NULL, NULL, NULL, NULL, NULL, DATE_ADD(DATE_ADD(DATE(@seed_now), INTERVAL 13 DAY), INTERVAL 1020 MINUTE), NULL, @u_dept_finance_hcm, DATE_SUB(@seed_now, INTERVAL 2 DAY), 2, DATE_ADD(DATE_ADD(DATE(@seed_now), INTERVAL 25 DAY), INTERVAL 660 MINUTE), DATE_ADD(DATE_ADD(DATE(@seed_now), INTERVAL 25 DAY), INTERVAL 960 MINUTE), 'Đề xuất đổi phòng Lab B.', 'Lab A bảo trì.', @u_staff_hcm, DATE_SUB(@seed_now, INTERVAL 1 DAY), 'ACCEPTED', 'Đồng ý đổi phòng.', NULL, 0, DATE_SUB(@seed_now, INTERVAL 7 DAY), @u_staff_hn, @seed_now, @u_staff_hn),
  (@log_4, @vi_as_hn, 'EQUIPMENT', 'Hạng mục hậu cần RECEIVED', 'Nội dung thực tế cho trạng thái RECEIVED.', 4, DATE_ADD(DATE_ADD(DATE(@seed_now), INTERVAL 14 DAY), INTERVAL 540 MINUTE), DATE_ADD(DATE_ADD(DATE(@seed_now), INTERVAL 14 DAY), INTERVAL 960 MINUTE), 'RECEIVED', 'LOW', @u_staff_hn, @dept_hn_it, DATE_SUB(@seed_now, INTERVAL 7 DAY), @u_deptlead_it_hn, DATE_SUB(@seed_now, INTERVAL 6 DAY), NULL, NULL, NULL, NULL, NULL, DATE_ADD(DATE_ADD(DATE(@seed_now), INTERVAL 13 DAY), INTERVAL 1020 MINUTE), NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, 1, DATE_SUB(@seed_now, INTERVAL 7 DAY), @u_staff_hn, @seed_now, @u_staff_hn),
  (@log_5, @vi_as_hn, 'BANNER', 'Hạng mục hậu cần ASSIGNED', 'Nội dung thực tế cho trạng thái ASSIGNED.', 5, DATE_ADD(DATE_ADD(DATE(@seed_now), INTERVAL 14 DAY), INTERVAL 540 MINUTE), DATE_ADD(DATE_ADD(DATE(@seed_now), INTERVAL 14 DAY), INTERVAL 960 MINUTE), 'ASSIGNED', 'MEDIUM', @u_staff_hn, @dept_hn_it, DATE_SUB(@seed_now, INTERVAL 7 DAY), @u_deptlead_it_hn, DATE_SUB(@seed_now, INTERVAL 6 DAY), @u_dept_it_hn, @u_deptlead_it_hn, DATE_SUB(@seed_now, INTERVAL 5 DAY), NULL, NULL, DATE_ADD(DATE_ADD(DATE(@seed_now), INTERVAL 13 DAY), INTERVAL 1020 MINUTE), NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, 2, DATE_SUB(@seed_now, INTERVAL 7 DAY), @u_staff_hn, @seed_now, @u_staff_hn),
  (@log_6, @vi_as_hn, 'LED', 'Hạng mục hậu cần ACCEPTED', 'Nội dung thực tế cho trạng thái ACCEPTED.', 6, DATE_ADD(DATE_ADD(DATE(@seed_now), INTERVAL 14 DAY), INTERVAL 540 MINUTE), DATE_ADD(DATE_ADD(DATE(@seed_now), INTERVAL 14 DAY), INTERVAL 960 MINUTE), 'ACCEPTED', 'HIGH', @u_staff_hn, @dept_hn_it, DATE_SUB(@seed_now, INTERVAL 7 DAY), @u_deptlead_it_hn, DATE_SUB(@seed_now, INTERVAL 6 DAY), @u_dept_it_hn, @u_deptlead_it_hn, DATE_SUB(@seed_now, INTERVAL 5 DAY), DATE_SUB(@seed_now, INTERVAL 4 DAY), 'Đã nhận nhiệm vụ.', DATE_ADD(DATE_ADD(DATE(@seed_now), INTERVAL 13 DAY), INTERVAL 1020 MINUTE), NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, 0, DATE_SUB(@seed_now, INTERVAL 7 DAY), @u_staff_hn, @seed_now, @u_staff_hn),
  (@log_7, @vi_am_hn, 'OTHER', 'Hạng mục hậu cần IN_PROGRESS', 'Nội dung thực tế cho trạng thái IN_PROGRESS.', 7, DATE_ADD(DATE_ADD(DATE(@seed_now), INTERVAL -2 DAY), INTERVAL 540 MINUTE), DATE_ADD(DATE_ADD(DATE(@seed_now), INTERVAL -2 DAY), INTERVAL 960 MINUTE), 'IN_PROGRESS', 'URGENT', @u_staff_hn, @dept_hn_it, DATE_SUB(@seed_now, INTERVAL 7 DAY), @u_deptlead_it_hn, DATE_SUB(@seed_now, INTERVAL 6 DAY), @u_dept_it_hn, @u_deptlead_it_hn, DATE_SUB(@seed_now, INTERVAL 5 DAY), DATE_SUB(@seed_now, INTERVAL 4 DAY), 'Đã nhận nhiệm vụ.', DATE_ADD(DATE_ADD(DATE(@seed_now), INTERVAL 13 DAY), INTERVAL 1020 MINUTE), NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, 1, DATE_SUB(@seed_now, INTERVAL 7 DAY), @u_staff_hn, @seed_now, @u_staff_hn),
  (@log_8, @vi_av_ct, 'ROOM', 'Hạng mục hậu cần READY', 'Nội dung thực tế cho trạng thái READY.', 8, DATE_ADD(DATE_ADD(DATE(@seed_now), INTERVAL -2 DAY), INTERVAL 540 MINUTE), DATE_ADD(DATE_ADD(DATE(@seed_now), INTERVAL -2 DAY), INTERVAL 960 MINUTE), 'READY', 'LOW', @u_staff_hn, @dept_hn_it, DATE_SUB(@seed_now, INTERVAL 7 DAY), @u_deptlead_it_hn, DATE_SUB(@seed_now, INTERVAL 6 DAY), @u_dept_it_hn, @u_deptlead_it_hn, DATE_SUB(@seed_now, INTERVAL 5 DAY), DATE_SUB(@seed_now, INTERVAL 4 DAY), 'Đã nhận nhiệm vụ.', DATE_ADD(DATE_ADD(DATE(@seed_now), INTERVAL 13 DAY), INTERVAL 1020 MINUTE), NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, 2, DATE_SUB(@seed_now, INTERVAL 7 DAY), @u_staff_hn, @seed_now, @u_staff_hn),
  (@log_9, @vi_cl_hn, 'EQUIPMENT', 'Hạng mục hậu cần DONE', 'Nội dung thực tế cho trạng thái DONE.', 9, DATE_ADD(DATE_ADD(DATE(@seed_now), INTERVAL -2 DAY), INTERVAL 540 MINUTE), DATE_ADD(DATE_ADD(DATE(@seed_now), INTERVAL -2 DAY), INTERVAL 960 MINUTE), 'DONE', 'MEDIUM', @u_staff_hn, @dept_hn_it, DATE_SUB(@seed_now, INTERVAL 7 DAY), @u_deptlead_it_hn, DATE_SUB(@seed_now, INTERVAL 6 DAY), @u_dept_it_hn, @u_deptlead_it_hn, DATE_SUB(@seed_now, INTERVAL 5 DAY), DATE_SUB(@seed_now, INTERVAL 4 DAY), 'Đã nhận nhiệm vụ.', DATE_ADD(DATE_ADD(DATE(@seed_now), INTERVAL 13 DAY), INTERVAL 1020 MINUTE), DATE_ADD(DATE_ADD(DATE(@seed_now), INTERVAL -30 DAY), INTERVAL 720 MINUTE), NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, 0, DATE_SUB(@seed_now, INTERVAL 7 DAY), @u_staff_hn, @seed_now, @u_staff_hn),
  (@log_10, @vi_rs_hcm, 'MEAL', 'Hạng mục hậu cần REJECTED', 'Nội dung thực tế cho trạng thái REJECTED.', 10, DATE_ADD(DATE_ADD(DATE(@seed_now), INTERVAL -2 DAY), INTERVAL 540 MINUTE), DATE_ADD(DATE_ADD(DATE(@seed_now), INTERVAL -2 DAY), INTERVAL 960 MINUTE), 'REJECTED', 'HIGH', @u_staff_hn, @dept_hn_it, DATE_SUB(@seed_now, INTERVAL 7 DAY), @u_deptlead_it_hn, DATE_SUB(@seed_now, INTERVAL 6 DAY), NULL, NULL, NULL, NULL, NULL, DATE_ADD(DATE_ADD(DATE(@seed_now), INTERVAL 13 DAY), INTERVAL 1020 MINUTE), NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, 'Ghi chú quyết định.', 1, DATE_SUB(@seed_now, INTERVAL 7 DAY), @u_staff_hn, @seed_now, @u_staff_hn),
  (@log_11, @vi_cn_hn, 'TRANSPORT', 'Hạng mục hậu cần CANCELLED', 'Nội dung thực tế cho trạng thái CANCELLED.', 11, DATE_ADD(DATE_ADD(DATE(@seed_now), INTERVAL -2 DAY), INTERVAL 540 MINUTE), DATE_ADD(DATE_ADD(DATE(@seed_now), INTERVAL -2 DAY), INTERVAL 960 MINUTE), 'CANCELLED', 'URGENT', @u_staff_hn, @dept_hn_it, DATE_SUB(@seed_now, INTERVAL 7 DAY), NULL, NULL, NULL, NULL, NULL, NULL, NULL, DATE_ADD(DATE_ADD(DATE(@seed_now), INTERVAL 13 DAY), INTERVAL 1020 MINUTE), NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, 'Ghi chú quyết định.', 2, DATE_SUB(@seed_now, INTERVAL 7 DAY), @u_staff_hn, @seed_now, @u_staff_hn);

SET @min_draft = 100220;
SET @min_final = 100221;

INSERT INTO minutes (
  minutes_id, visit_instance_id, title, content, participants_json,
  status, finalized_by, finalized_at,
  created_at, created_by, updated_at, updated_by
)
VALUES
  (
    @min_draft,
    @vi_av_ct,
    'Biên bản dự thảo sau chuyến thăm Cần Thơ',
    'Nội dung đang rà soát.',
    JSON_ARRAY(JSON_OBJECT('name','An','role','IC Support')),
    'DRAFT',
    NULL,
    NULL,
    DATE_SUB(@seed_now, INTERVAL 1 DAY),
    @u_stafflead_ct,
    @seed_now,
    @u_stafflead_ct
  ),
  (
    @min_final,
    @vi_cl_hn,
    'Biên bản chính thức đoàn nghiên cứu AI',
    'Hai bên thống nhất tiếp tục trao đổi về MOU.',
    JSON_ARRAY(JSON_OBJECT('name','Lê Hoàng Nam','role','Host')),
    'FINAL',
    @u_stafflead_hn,
    DATE_SUB(@seed_now, INTERVAL 28 DAY),
    DATE_SUB(@seed_now, INTERVAL 30 DAY),
    @u_stafflead_hn,
    DATE_SUB(@seed_now, INTERVAL 28 DAY),
    @u_stafflead_hn
  );

INSERT INTO minute_action_items (
  action_item_id, minutes_id, title, note, due_date, status, completed_at,
  display_order, created_at, created_by, updated_at, updated_by
)
VALUES
  (
    NULL,
    @min_draft,
    'Gửi email cảm ơn',
    'Gửi cho đại diện đoàn khách trong ngày.',
    DATE_ADD(DATE(@seed_now), INTERVAL 2 DAY),
    'TODO',
    NULL,
    1,
    DATE_SUB(@seed_now, INTERVAL 1 DAY),
    @u_stafflead_ct,
    @seed_now,
    @u_stafflead_ct
  ),
  (
    NULL,
    @min_draft,
    'Gửi hình ảnh sau sự kiện',
    'Chọn ảnh phù hợp trước khi gửi.',
    DATE_ADD(DATE(@seed_now), INTERVAL 3 DAY),
    'IN_PROGRESS',
    NULL,
    2,
    DATE_SUB(@seed_now, INTERVAL 1 DAY),
    @u_stafflead_ct,
    @seed_now,
    @u_stafflead_ct
  ),
  (
    NULL,
    @min_final,
    'Gửi MOU bản nháp',
    'Gửi file MOU cho đối tác để hai bên cùng góp ý.',
    DATE_SUB(DATE(@seed_now), INTERVAL 25 DAY),
    'DONE',
    DATE_SUB(@seed_now, INTERVAL 25 DAY),
    1,
    DATE_SUB(@seed_now, INTERVAL 30 DAY),
    @u_stafflead_hn,
    DATE_SUB(@seed_now, INTERVAL 25 DAY),
    @u_stafflead_hn
  );

INSERT INTO feedbacks (
  feedback_id,
  visit_request_id,
  visit_instance_id,
  submitted_by_user_id,
  submitter_role,
  submitter_context,
  submitter_name_snapshot,
  target_user_id,
  target_role,
  target_context,
  target_name_snapshot,
  rating,
  comment,
  submitted_at
)
VALUES
  (
    NULL,
    @vr_after_visit,
    @vi_av_ct,
    @v_short_name,
    'VISITOR',
    'Khách đại diện',
    'Visitor Short Name',
    @u_stafflead_ct,
    'HOST',
    'Host chính',
    'Nguyễn Thảo My',
    4,
    'Host đón tiếp chu đáo, nhưng cần gửi agenda sớm hơn.',
    DATE_SUB(@seed_now, INTERVAL 1 DAY)
  ),
  (
    NULL,
    @vr_closed,
    @vi_cl_hn,
    @u_dept_it_hn,
    'LOGISTICS',
    'Thiết bị/phòng lab',
    'Trần Văn IT',
    @u_stafflead_hn,
    'HOST',
    'Host chính',
    'Lê Hoàng Nam',
    4,
    'Host phối hợp tốt, nhưng gửi thông tin setup hơi sát giờ.',
    DATE_SUB(@seed_now, INTERVAL 27 DAY)
  ),
  (
    NULL,
    @vr_closed,
    @vi_cl_hn,
    @u_stafflead_hn,
    'HOST',
    'Host chính',
    'Lê Hoàng Nam',
    @v_long_name,
    'VISITOR',
    'Đại diện đoàn khách',
    'Visitor Long Name',
    5,
    'Khách đúng giờ, thiện chí và hợp tác tốt.',
    DATE_SUB(@seed_now, INTERVAL 27 DAY)
  ),
  (
    NULL,
    @vr_closed,
    @vi_cl_hn,
    @u_stafflead_hn,
    'HOST',
    'Host chính',
    'Lê Hoàng Nam',
    @u_dept_it_hn,
    'LOGISTICS',
    'Thiết bị/phòng lab',
    'Trần Văn IT',
    3,
    'Thiết bị chuẩn bị đủ nhưng hoàn tất hơi muộn.',
    DATE_SUB(@seed_now, INTERVAL 27 DAY)
  );

SET @gal_public = 100222;
SET @gal_internal = 100223;
SET @gal_hidden = 100224;

SET @img_public = 100225;
SET @img_hcm = 100226;
SET @img_hidden = 100227;

INSERT INTO galleries (gallery_id, campus_id, location_name, title, description, story_content, status, visibility, created_at, created_by, updated_at, updated_by, deleted_at, deleted_by)
VALUES
  (@gal_public, @campus_hn, 'Sảnh Alpha', 'Sảnh Alpha', 'Không gian đón tiếp chính tại campus Hà Nội.', 'Sảnh Alpha là điểm chạm đầu tiên của nhiều đoàn khách khi đến FPT University, thể hiện tinh thần cởi mở và kết nối quốc tế.', 'PUBLISHED', 'PUBLIC', DATE_SUB(@seed_now, INTERVAL 27 DAY), @u_stafflead_hn, @seed_now, @u_stafflead_hn, NULL, NULL),
  (@gal_internal, @campus_hcm, 'Green Lab', 'Green Lab', 'Không gian lab phục vụ hoạt động học tập và trải nghiệm công nghệ.', 'Green Lab thể hiện định hướng học qua trải nghiệm, đổi mới sáng tạo và phát triển bền vững tại campus.', 'DRAFT', 'INTERNAL', DATE_SUB(@seed_now, INTERVAL 3 DAY), @u_stafflead_hcm, NULL, NULL, NULL, NULL),
  (@gal_hidden, @campus_hn, 'Phòng briefing', 'Phòng briefing', 'Không gian chuẩn bị và trao đổi nhanh trước buổi làm việc.', 'Đây là nơi host và team hỗ trợ thống nhất lịch trình, thông tin đoàn và các lưu ý trước khi tiếp đón.', 'HIDDEN', 'PRIVATE', DATE_SUB(@seed_now, INTERVAL 8 DAY), @u_stafflead_hn, @seed_now, @u_stafflead_hn, NULL, NULL);

INSERT INTO gallery_images (image_id, gallery_id, file_id, caption, display_order, taken_at, status, created_at, created_by, updated_at, updated_by, deleted_at, deleted_by)
VALUES
  (@img_public, @gal_public, @file_gallery_hn, 'Không gian sảnh Alpha dùng để đón khách và chụp ảnh lưu niệm.', 1, DATE_SUB(@seed_now, INTERVAL 30 DAY), 'ACTIVE', DATE_SUB(@seed_now, INTERVAL 27 DAY), @u_stafflead_hn, @seed_now, @u_stafflead_hn, NULL, NULL),
  (@img_hcm, @gal_internal, @file_gallery_hcm, 'Góc lab dự kiến giới thiệu trong campus tour.', 1, DATE_SUB(@seed_now, INTERVAL 3 DAY), 'ACTIVE', DATE_SUB(@seed_now, INTERVAL 3 DAY), @u_stafflead_hcm, @seed_now, @u_stafflead_hcm, NULL, NULL),
  (@img_hidden, @gal_hidden, @file_gallery_hidden, 'Ảnh ẩn do chứa thông tin lịch trình nội bộ.', 1, DATE_SUB(@seed_now, INTERVAL 8 DAY), 'HIDDEN', DATE_SUB(@seed_now, INTERVAL 8 DAY), @u_stafflead_hn, @seed_now, @u_stafflead_hn, NULL, NULL);

INSERT INTO photo_face_tags (face_tag_id, image_id, visit_request_id, guest_member_id, partner_contact_id, display_name, bounding_box_x, bounding_box_y, bounding_box_width, bounding_box_height, tag_status, confirmed_by, confirmed_at, created_at, created_by, removed_at, removed_by)
VALUES
  (NULL, @img_public, @vr_closed, NULL, @c_kim, 'Kim Min Seo', 0.102, 0.18, 0.22, 0.28, 'MANUALLY_TAGGED', NULL, NULL, DATE_SUB(@seed_now, INTERVAL 27 DAY), @u_staff_hn, NULL, NULL),
  (NULL, @img_public, @vr_closed, NULL, @c_lee, 'Lee Joon Ho', 0.42, 0.2, 0.18, 0.26, 'CONFIRMED', @u_stafflead_hn, DATE_SUB(@seed_now, INTERVAL 26 DAY), DATE_SUB(@seed_now, INTERVAL 27 DAY), @u_staff_hn, NULL, NULL),
  (NULL, @img_hidden, NULL, NULL, NULL, 'Khách không xác định', NULL, NULL, NULL, NULL, 'REMOVED', NULL, NULL, DATE_SUB(@seed_now, INTERVAL 8 DAY), @u_stafflead_hn, DATE_SUB(@seed_now, INTERVAL 7 DAY), @u_stafflead_hn);

INSERT INTO documents (document_id, file_id, owner_type, owner_id, campus_id, title, description, document_category, status, created_at, created_by, updated_at, updated_by)
VALUES
  (NULL, @file_doc_general, 'GENERAL', NULL, @campus_hn, 'Quy trình tiếp đoàn khách quốc tế', 'Tài liệu SOP cho IC Office.', 'SOP', 'PUBLISHED', DATE_SUB(@seed_now, INTERVAL 100 DAY), @u_ho_ha, @seed_now, @u_ho_ha),
  (NULL, @file_doc_visit, 'VISIT', @vr_approved_single_before, @campus_hn, 'Agenda chính thức đoàn SeoulTech Hà Nội', 'File agenda visit sắp tới.', 'AGENDA', 'PUBLISHED', DATE_SUB(@seed_now, INTERVAL 7 DAY), @u_staff_hn, @seed_now, @u_staff_hn),
  (NULL, @file_doc_partner, 'PARTNER', @p_seoul, @campus_hn, 'MOU dự thảo với SeoulTech', 'Tài liệu đối tác ở trạng thái nháp.', 'MOU', 'DRAFT', DATE_SUB(@seed_now, INTERVAL 70 DAY), @u_stafflead_hn, @seed_now, @u_stafflead_hn),
  (NULL, @file_doc_minutes, 'MINUTES', @min_final, @campus_hn, 'Biên bản chính thức đoàn nghiên cứu AI', 'PDF biên bản đã chốt.', 'MINUTES', 'PUBLISHED', DATE_SUB(@seed_now, INTERVAL 28 DAY), @u_stafflead_hn, @seed_now, @u_stafflead_hn),
  (NULL, @file_news_policy, 'NEWS', @news_hidden, NULL, 'Tài liệu minh chứng bản tin ẩn', 'Tài liệu liên quan bản tin đang ẩn.', 'NEWS_ATTACHMENT', 'PUBLISHED', DATE_SUB(@seed_now, INTERVAL 180 DAY), @u_ho_ha, DATE_SUB(@seed_now, INTERVAL 100 DAY), @u_ho_ha),
  (NULL, @file_doc_logistics, 'LOGISTICS', @log_4, @campus_hn, 'Sơ đồ bố trí phòng họp Alpha', 'Sơ đồ hạng mục thiết bị.', 'ROOM_LAYOUT', 'PUBLISHED', DATE_SUB(@seed_now, INTERVAL 5 DAY), @u_dept_it_hn, @seed_now, @u_dept_it_hn),
  (NULL, @file_doc_report, 'REPORT', NULL, NULL, 'Báo cáo dashboard đoàn khách theo tháng', 'Tệp xuất báo cáo.', 'DASHBOARD_REPORT', 'PUBLISHED', DATE_SUB(@seed_now, INTERVAL 1 DAY), @u_ho_ha, @seed_now, @u_ho_ha),
  (NULL, @file_edge_small, 'GENERAL', NULL, NULL, 'Tệp biên chú rỗng', 'Edge case file_size = 0.', 'EDGE_CASE', 'DRAFT', DATE_SUB(@seed_now, INTERVAL 2 DAY), @u_admin_minh, NULL, NULL);

SET @cal_personal = 100228;

SET @cal_visit = 100229;

SET @cal_logistics = 100230;

SET @cal_deadline = 100231;

SET @cal_cancelled = 100232;

SET @cal_deleted = 100233;

INSERT INTO calendar_events (calendar_event_id, owner_user_id, campus_id, visit_instance_id, logistics_item_id, source_type, title, description, location, start_at, end_at, timezone, visibility, attendees_json, reminders_json, status, created_at, created_by, updated_at, updated_by, deleted_at, deleted_by)
VALUES
  (@cal_personal, @u_stafflead_hn, @campus_hn, NULL, NULL, 'PERSONAL', 'Chuẩn bị briefing SeoulTech', 'Ghi chú cá nhân.', 'Online', DATE_ADD(DATE_ADD(DATE(@seed_now), INTERVAL 13 DAY), INTERVAL 840 MINUTE), DATE_ADD(DATE_ADD(DATE(@seed_now), INTERVAL 13 DAY), INTERVAL 900 MINUTE), 'Asia/Ho_Chi_Minh', 'PRIVATE', JSON_ARRAY(), JSON_ARRAY(JSON_OBJECT('method','popup','minutes',30)), 'ACTIVE', DATE_SUB(@seed_now, INTERVAL 2 DAY), @u_stafflead_hn, @seed_now, @u_stafflead_hn, NULL, NULL),
  (@cal_visit, @u_stafflead_hn, @campus_hn, @vi_as_hn, NULL, 'VISIT', 'Đón đoàn SeoulTech tại Hà Nội', 'Sự kiện visit chính thức.', 'FPTU Hà Nội', DATE_ADD(DATE_ADD(DATE(@seed_now), INTERVAL 14 DAY), INTERVAL 540 MINUTE), DATE_ADD(DATE_ADD(DATE(@seed_now), INTERVAL 14 DAY), INTERVAL 990 MINUTE), 'Asia/Ho_Chi_Minh', 'INTERNAL', JSON_ARRAY(JSON_OBJECT('user_id', @u_staff_hn)), JSON_ARRAY(JSON_OBJECT('method','email','minutes',1440)), 'ACTIVE', DATE_SUB(@seed_now, INTERVAL 7 DAY), @u_stafflead_hn, @seed_now, @u_stafflead_hn, NULL, NULL),
  (@cal_logistics, @u_dept_it_hn, @campus_hn, @vi_as_hn, @log_4, 'LOGISTICS', 'Kiểm tra màn hình và Wi-Fi khách', 'Deadline kỹ thuật.', 'AI Lab', DATE_ADD(DATE_ADD(DATE(@seed_now), INTERVAL 13 DAY), INTERVAL 900 MINUTE), DATE_ADD(DATE_ADD(DATE(@seed_now), INTERVAL 13 DAY), INTERVAL 960 MINUTE), 'Asia/Ho_Chi_Minh', 'INTERNAL', JSON_ARRAY(JSON_OBJECT('user_id', @u_deptlead_it_hn)), JSON_ARRAY(JSON_OBJECT('method','popup','minutes',120)), 'ACTIVE', DATE_SUB(@seed_now, INTERVAL 5 DAY), @u_deptlead_it_hn, @seed_now, @u_deptlead_it_hn, NULL, NULL),
  (@cal_deadline, @u_stafflead_ct, @campus_ct, @vi_av_ct, NULL, 'DEADLINE', 'Chốt biên bản sau visit Cần Thơ', 'Hoàn thiện biên bản.', 'IC Office Cần Thơ', DATE_ADD(DATE_ADD(DATE(@seed_now), INTERVAL 1 DAY), INTERVAL 1020 MINUTE), DATE_ADD(DATE_ADD(DATE(@seed_now), INTERVAL 1 DAY), INTERVAL 1080 MINUTE), 'Asia/Ho_Chi_Minh', 'INTERNAL', JSON_ARRAY(), JSON_ARRAY(JSON_OBJECT('method','email','minutes',240)), 'DONE', DATE_SUB(@seed_now, INTERVAL 1 DAY), @u_stafflead_ct, @seed_now, @u_stafflead_ct, NULL, NULL),
  (@cal_cancelled, @u_staff_hn, @campus_hn, @vi_cn_hn, @log_11, 'VISIT', 'Lịch đã hủy - đoàn IED', 'Calendar hủy theo request.', 'FPTU Hà Nội', DATE_ADD(DATE_ADD(DATE(@seed_now), INTERVAL 30 DAY), INTERVAL 540 MINUTE), DATE_ADD(DATE_ADD(DATE(@seed_now), INTERVAL 30 DAY), INTERVAL 960 MINUTE), 'Asia/Ho_Chi_Minh', 'INTERNAL', JSON_ARRAY(), JSON_ARRAY(), 'CANCELLED', DATE_SUB(@seed_now, INTERVAL 20 DAY), @u_staff_hn, @seed_now, @u_staff_hn, NULL, NULL),
  (@cal_deleted, @u_admin_minh, @campus_hn, NULL, NULL, 'PERSONAL', 'Sự kiện cá nhân đã xóa mềm', 'Kiểm thử deleted_at.', 'Online', DATE_ADD(DATE_ADD(DATE(@seed_now), INTERVAL -5 DAY), INTERVAL 540 MINUTE), DATE_ADD(DATE_ADD(DATE(@seed_now), INTERVAL -5 DAY), INTERVAL 600 MINUTE), 'Asia/Ho_Chi_Minh', 'PRIVATE', JSON_ARRAY(), JSON_ARRAY(), 'CANCELLED', DATE_SUB(@seed_now, INTERVAL 10 DAY), @u_admin_minh, DATE_SUB(@seed_now, INTERVAL 5 DAY), @u_admin_minh, DATE_SUB(@seed_now, INTERVAL 4 DAY), @u_admin_minh);

INSERT INTO sent_emails (sent_email_id, email_template_id, related_type, related_id, subject, body_snapshot, recipients_json, metadata_json, status, error_message, sent_by, sent_at, created_at)
VALUES
  (NULL, @tpl_verify, 'VISIT_REQUEST', @vr_pending_approval_seed, 'Mã xác thực email trước khi gửi yêu cầu thăm quan VR-PA-001', 'OTP sẽ hết hạn sau 10 phút.', JSON_ARRAY(JSON_OBJECT('email','thaomy.pending.approval@partner.example')), JSON_OBJECT('provider','SMTP'), 'QUEUED', NULL, NULL, NULL, DATE_SUB(@seed_now, INTERVAL 1 HOUR)),
  (NULL, @tpl_approved, 'VISIT_REQUEST', @vr_approved_single_before, 'Yêu cầu VR-AS-003 đã được duyệt', 'FPTU xác nhận lịch thăm Hà Nội.', JSON_ARRAY(JSON_OBJECT('email','kim.minseo@seoultech.example')), JSON_OBJECT('provider','SMTP','message_id','seed-approved-001'), 'SENT', NULL, @u_stafflead_hn, DATE_SUB(@seed_now, INTERVAL 12 DAY), DATE_SUB(@seed_now, INTERVAL 12 DAY)),
  (NULL, @tpl_rejected, 'VISIT_REQUEST', @vr_rejected_single, 'Yêu cầu VR-RS-005 chưa được duyệt', 'Lý do: lịch quá sát.', JSON_ARRAY(JSON_OBJECT('email','emily.smith@greentech.example')), JSON_OBJECT('provider','SMTP','retry_count',3), 'FAILED', 'Mailbox temporarily unavailable', @u_stafflead_hcm, NULL, DATE_SUB(@seed_now, INTERVAL 8 DAY));

INSERT INTO notifications (notification_id, recipient_user_id, title, message, notification_type, related_type, related_id, is_read, read_at, created_at)
VALUES
  (NULL, @u_stafflead_hn, 'Yêu cầu thăm quan mới cần xử lý', 'Visitor Kim Min Seo gửi VR-AS-003.', 'VISIT_REQUEST', 'VISIT_REQUEST', @vr_approved_single_before, TRUE, DATE_SUB(@seed_now, INTERVAL 12 DAY), DATE_SUB(@seed_now, INTERVAL 13 DAY)),
  (NULL, @u_ho_ha, 'Yêu cầu liên cơ sở cần HO duyệt', 'VR-PA-002 đang chờ HO.', 'APPROVAL', 'VISIT_REQUEST', @vr_pending_approval_multi, FALSE, NULL, DATE_SUB(@seed_now, INTERVAL 1 DAY)),
  (NULL, @u_dept_it_hn, 'Bạn được phân công thiết bị', 'Chuẩn bị thiết bị cho VR-AS-003.', 'LOGISTICS', 'LOGISTICS_ITEM', @log_4, FALSE, NULL, DATE_SUB(@seed_now, INTERVAL 5 DAY)),
  (NULL, @v_smith, 'Yêu cầu chưa được duyệt', 'VR-RS-005 chưa được duyệt.', 'VISIT_DECISION', 'VISIT_REQUEST', @vr_rejected_single, TRUE, DATE_SUB(@seed_now, INTERVAL 8 DAY), DATE_SUB(@seed_now, INTERVAL 8 DAY));

SET @api_email = 100234;

SET @api_ocr = 100235;

SET @api_calendar = 100236;

SET @api_sms = 100237;

SET @api_report = 100238;

SET @api_deleted = 100239;

SET @agt_global = 100240;

SET @agt_hn = 100241;

SET @agt_inactive = 100242;

SET @agt_deleted = 100243;

INSERT INTO api_configurations (api_config_id, api_code, name, provider_name, purpose, base_url, default_method, auth_type, credentials_json, headers_json, body_template_json, settings_json, timeout_seconds, status, created_at, created_by, updated_at, updated_by, deleted_at, deleted_by)
VALUES
  (@api_email, 'SMTP_PRIMARY', 'Primary SMTP Gateway', 'Internal SMTP', 'Send transactional email', 'https://smtp-api.example/send', 'POST', 'API_KEY', JSON_OBJECT('api_key','***masked***'), JSON_OBJECT('Content-Type','application/json'), JSON_OBJECT('to','{{To}}'), JSON_OBJECT('retry',3), 30, 'ACTIVE', DATE_SUB(@seed_now, INTERVAL 250 DAY), @u_admin_minh, @seed_now, @u_admin_minh, NULL, NULL),
  (@api_ocr, 'BUSINESS_CARD_OCR', 'Business Card OCR', 'OCR Provider', 'Extract contact', 'https://ocr.example/v1/cards', 'POST', 'BEARER_TOKEN', JSON_OBJECT('token','***masked***'), JSON_OBJECT('Content-Type','multipart/form-data'), JSON_OBJECT('language','vi,en'), JSON_OBJECT('timeoutProfile','standard'), 60, 'ACTIVE', DATE_SUB(@seed_now, INTERVAL 200 DAY), @u_admin_minh, @seed_now, @u_admin_minh, NULL, NULL),
  (@api_calendar, 'GOOGLE_CALENDAR_SYNC', 'Google Calendar Sync', 'Google Calendar', 'Sync visit event', 'https://calendar.example/events', 'PATCH', 'OAUTH2', JSON_OBJECT('client_id','***masked***'), JSON_OBJECT('Content-Type','application/json'), JSON_OBJECT('summary','{{Title}}'), JSON_OBJECT('scope','calendar.events'), 45, 'ACTIVE', DATE_SUB(@seed_now, INTERVAL 180 DAY), @u_admin_minh, @seed_now, @u_admin_minh, NULL, NULL),
  (@api_sms, 'SMS_OTP_BACKUP', 'SMS OTP Backup', 'SMS Provider', 'Backup OTP', 'https://sms.example/messages', 'PUT', 'BASIC', JSON_OBJECT('username','***masked***'), JSON_OBJECT('Content-Type','application/json'), JSON_OBJECT('phone','{{Phone}}'), JSON_OBJECT('enabledFor','criticalOnly'), 20, 'INACTIVE', DATE_SUB(@seed_now, INTERVAL 150 DAY), @u_admin_minh, @seed_now, @u_admin_minh, NULL, NULL),
  (@api_report, 'REPORT_EXPORT', 'Report Export Service', 'Internal Reporting', 'Generate dashboard export', 'https://report.example/export', 'GET', 'NONE', NULL, JSON_OBJECT('Accept','application/json'), NULL, JSON_OBJECT('cacheSeconds',60), 15, 'ACTIVE', DATE_SUB(@seed_now, INTERVAL 120 DAY), @u_admin_minh, @seed_now, @u_admin_minh, NULL, NULL),
  (@api_deleted, 'OLD_FEID_SYNC', 'Old FEID Sync API', 'Legacy FEID', 'Legacy account sync', 'https://legacy-feid.example/sync', 'DELETE', 'CUSTOM', JSON_OBJECT('customSecret','***masked***'), JSON_OBJECT('X-Legacy','true'), JSON_OBJECT('userId','{{UserId}}'), JSON_OBJECT('deprecated',true), 10, 'DISABLED', DATE_SUB(@seed_now, INTERVAL 500 DAY), @u_admin_minh, DATE_SUB(@seed_now, INTERVAL 200 DAY), @u_admin_minh, DATE_SUB(@seed_now, INTERVAL 90 DAY), @u_admin_minh);

INSERT INTO api_usage_quotas (api_usage_quota_id, api_config_id, campus_id, campus_scope_key, period_yyyymm, monthly_limit, used_count, last_used_at, created_at, created_by, updated_at, updated_by)
VALUES
  (NULL, @api_email, NULL, 'GLOBAL', DATE_FORMAT(@seed_now, '%Y%m'), 5000, 843, DATE_SUB(@seed_now, INTERVAL 10 MINUTE), DATE_SUB(@seed_now, INTERVAL 30 DAY), @u_admin_minh, @seed_now, @u_admin_minh),
  (NULL, @api_ocr, @campus_hn, @campus_hn, DATE_FORMAT(@seed_now, '%Y%m'), 300, 42, DATE_SUB(@seed_now, INTERVAL 2 DAY), DATE_SUB(@seed_now, INTERVAL 30 DAY), @u_admin_minh, @seed_now, @u_admin_minh),
  (NULL, @api_ocr, @campus_hcm, @campus_hcm, DATE_FORMAT(@seed_now, '%Y%m'), 300, 299, DATE_SUB(@seed_now, INTERVAL 1 HOUR), DATE_SUB(@seed_now, INTERVAL 30 DAY), @u_admin_minh, @seed_now, @u_admin_minh),
  (NULL, @api_report, NULL, 'GLOBAL', DATE_FORMAT(DATE_SUB(@seed_now, INTERVAL 1 MONTH), '%Y%m'), 1000, 1000, DATE_SUB(@seed_now, INTERVAL 30 DAY), DATE_SUB(@seed_now, INTERVAL 60 DAY), @u_admin_minh, @seed_now, @u_admin_minh);

INSERT INTO api_request_logs (api_config_id, campus_id, requested_by, related_type, related_id, endpoint, method, http_status, response_time_ms, request_size_bytes, response_size_bytes, success, error_code, error_message, created_at)
VALUES
  
  (@api_email, NULL, @u_stafflead_hn, 'SENT_EMAIL', NULL, '/send', 'POST', 202, 180, 2048, 512, TRUE, NULL, NULL, DATE_SUB(@seed_now, INTERVAL 10 MINUTE)),
  (@api_ocr, @campus_hn, @u_staff_hn, 'PARTNER_CONTACT', @c_kim, '/v1/cards', 'POST', 200, 950, 1048576, 4096, TRUE, NULL, NULL, DATE_SUB(@seed_now, INTERVAL 2 DAY)),
  (@api_calendar, @campus_hn, @u_stafflead_hn, 'CALENDAR_EVENT', @cal_visit, '/events/seed', 'PATCH', 200, 320, 4096, 2048, TRUE, NULL, NULL, DATE_SUB(@seed_now, INTERVAL 1 DAY)),
  (@api_sms, NULL, @u_admin_minh, 'OTP', NULL, '/messages', 'PUT', 503, 1200, 512, 128, FALSE, 'PROVIDER_TIMEOUT', 'SMS provider timeout.', DATE_SUB(@seed_now, INTERVAL 30 MINUTE)),
  (@api_report, NULL, @u_ho_ha, 'REPORT', NULL, '/export?range=quarter', 'GET', 200, 240, 128, 65536, TRUE, NULL, NULL, DATE_SUB(@seed_now, INTERVAL 1 DAY)),
  (@api_deleted, NULL, @u_admin_minh, 'USER', @u_inactive_dept, '/sync', 'DELETE', 410, 80, 128, 64, FALSE, 'API_DISABLED', 'Legacy FEID sync disabled.', DATE_SUB(@seed_now, INTERVAL 90 DAY));

INSERT INTO agenda_templates (agenda_template_id, campus_id, campus_scope_key, name, description, items_json, status, created_at, created_by, updated_at, updated_by, deleted_at, deleted_by)
VALUES
  (@agt_global, NULL, 'GLOBAL', 'Mẫu agenda chuẩn cho đoàn quốc tế', 'Mẫu dùng chung.', JSON_ARRAY(JSON_OBJECT('order',1,'title','Welcome','durationMinutes',30)), 'ACTIVE', DATE_SUB(@seed_now, INTERVAL 180 DAY), @u_ho_ha, @seed_now, @u_ho_ha, NULL, NULL),
  (@agt_hn, @campus_hn, @campus_hn, 'Mẫu Hà Nội - AI Lab focus', 'Mẫu cho đoàn quan tâm AI.', JSON_ARRAY(JSON_OBJECT('order',1,'title','AI Lab visit','durationMinutes',90)), 'ACTIVE', DATE_SUB(@seed_now, INTERVAL 120 DAY), @u_stafflead_hn, @seed_now, @u_stafflead_hn, NULL, NULL),
  (@agt_inactive, @campus_hcm, @campus_hcm, 'Mẫu workshop cũ', 'Không còn dùng.', JSON_ARRAY(JSON_OBJECT('order',1,'title','Old workshop','durationMinutes',120)), 'INACTIVE', DATE_SUB(@seed_now, INTERVAL 360 DAY), @u_stafflead_hcm, DATE_SUB(@seed_now, INTERVAL 200 DAY), @u_stafflead_hcm, NULL, NULL),
  (@agt_deleted, NULL, 'GLOBAL', 'Mẫu đã xóa mềm', 'Dùng kiểm thử soft delete.', JSON_ARRAY(JSON_OBJECT('order',1,'title','Deprecated','durationMinutes',30)), 'INACTIVE', DATE_SUB(@seed_now, INTERVAL 500 DAY), @u_admin_minh, DATE_SUB(@seed_now, INTERVAL 200 DAY), @u_admin_minh, DATE_SUB(@seed_now, INTERVAL 100 DAY), @u_admin_minh);

INSERT INTO audit_logs (actor_user_id, campus_id, action, entity_type, entity_id, old_values_json, new_values_json, ip_address, user_agent, request_id, created_at)
VALUES
  
  (@u_admin_minh, @campus_hn, 'CREATE', 'USER', @u_stafflead_hn, NULL, JSON_OBJECT('email','staff.leader.hn@fpt.edu.vn'), '10.10.1.15', 'Mozilla/5.0', 'req-seed-create-user', DATE_SUB(@seed_now, INTERVAL 340 DAY)),
  (@u_stafflead_hn, @campus_hn, 'SUBMIT', 'VISIT_REQUEST', @vr_approved_single_before, JSON_OBJECT('status','PENDING_APPROVAL'), JSON_OBJECT('status','PENDING_APPROVAL'), '10.10.2.15', 'Mozilla/5.0', 'req-seed-submit', DATE_SUB(@seed_now, INTERVAL 13 DAY)),
  (@u_stafflead_hn, @campus_hn, 'APPROVE', 'VISIT_REQUEST', @vr_approved_single_before, JSON_OBJECT('status','PENDING_APPROVAL'), JSON_OBJECT('status','APPROVED'), '10.10.2.15', 'Mozilla/5.0', 'req-seed-approve', DATE_SUB(@seed_now, INTERVAL 12 DAY)),
  (@u_ho_ha, NULL, 'APPROVE', 'VISIT_REQUEST', @vr_approved_multi_during, JSON_OBJECT('status','PENDING_APPROVAL'), JSON_OBJECT('status','APPROVED','scope','MULTI_CAMPUS'), '10.10.1.16', 'Mozilla/5.0', 'req-seed-approve-multi', DATE_SUB(@seed_now, INTERVAL 12 DAY)),
  (@u_stafflead_hcm, @campus_hcm, 'REJECT', 'VISIT_REQUEST', @vr_rejected_single, JSON_OBJECT('status','PENDING_APPROVAL'), JSON_OBJECT('status','REJECTED'), '10.10.2.16', 'Mozilla/5.0', 'req-seed-reject', DATE_SUB(@seed_now, INTERVAL 8 DAY)),
  (@u_staff_hcm, @campus_hcm, 'UPDATE', 'VISIT_LOGISTICS_ITEM', @log_3, JSON_OBJECT('status','ASSIGNED'), JSON_OBJECT('status','CHANGE_PROPOSED'), '10.10.2.17', 'Mozilla/5.0', 'req-seed-update', DATE_SUB(@seed_now, INTERVAL 2 DAY)),
  (@u_admin_minh, @campus_hn, 'DELETE', 'CALENDAR_EVENT', @cal_deleted, JSON_OBJECT('deleted_at',NULL), JSON_OBJECT('deleted_at','relative'), '10.10.1.15', 'Mozilla/5.0', 'req-seed-delete', DATE_SUB(@seed_now, INTERVAL 4 DAY)),
  (@u_admin_minh, NULL, 'LOGIN', 'USER_SESSION', @sess_admin, NULL, JSON_OBJECT('status','SUCCESS'), '10.10.1.15', 'Mozilla/5.0', 'req-seed-login', DATE_SUB(@seed_now, INTERVAL 1 DAY)),
  (@u_ho_ha, @campus_hn, 'LOGOUT', 'USER_SESSION', @sess_ho_revoked, JSON_OBJECT('revoked_at',NULL), JSON_OBJECT('revoked_reason','User logout'), '10.10.1.16', 'Mozilla/5.0', 'req-seed-logout', DATE_SUB(@seed_now, INTERVAL 1 DAY)),
  (@u_admin_minh, @campus_hn, 'LOCK_ACCOUNT', 'USER', @u_locked_staff, JSON_OBJECT('status','ACTIVE'), JSON_OBJECT('status','LOCKED'), '10.10.1.15', 'Mozilla/5.0', 'req-seed-lock', DATE_SUB(@seed_now, INTERVAL 1 DAY)),
  (@u_admin_minh, @campus_hn, 'UNLOCK_ACCOUNT', 'USER', @u_locked_staff, JSON_OBJECT('status','LOCKED'), JSON_OBJECT('status','ACTIVE'), '10.10.1.15', 'Mozilla/5.0', 'req-seed-unlock', DATE_SUB(@seed_now, INTERVAL 1 DAY));

INSERT INTO visit_status_logs (visit_request_id, visit_instance_id, status_owner_type, old_status, new_status, changed_by, reason, changed_at)
VALUES
  (@vr_pending_approval_seed, @vi_pending_approval_hcm, 'CAMPUS_INSTANCE', NULL, 'WAITING_REQUEST_APPROVAL', @v_pending_approval_seed, 'Email verified, visitor submitted request; pending approval.', DATE_SUB(@seed_now, INTERVAL 2 HOUR)),
  (@vr_pending_approval_multi, @vi_pa_hn, 'CAMPUS_INSTANCE', NULL, 'WAITING_REQUEST_APPROVAL', @v_pending_approval, 'Email verified, pending HO approval.', DATE_SUB(@seed_now, INTERVAL 1 DAY)),
  (@vr_approved_single_before, @vi_as_hn, 'CAMPUS_INSTANCE', 'WAITING_REQUEST_APPROVAL', 'BEFORE_VISIT', @u_stafflead_hn, 'Staff Leader approved and started preparation.', DATE_SUB(@seed_now, INTERVAL 12 DAY)),
  (@vr_approved_multi_during, @vi_am_hn, 'CAMPUS_INSTANCE', 'BEFORE_VISIT', 'DURING_VISIT', @u_stafflead_hn, 'Visit started at Hà Nội.', DATE_SUB(@seed_now, INTERVAL 1 HOUR)),
  (@vr_after_visit, @vi_av_ct, 'CAMPUS_INSTANCE', 'DURING_VISIT', 'AFTER_VISIT', @u_stafflead_ct, 'Visit finished, preparing minutes.', DATE_SUB(@seed_now, INTERVAL 1 DAY)),
  (@vr_closed, @vi_cl_hn, 'CAMPUS_INSTANCE', 'AFTER_VISIT', 'CLOSED', @u_stafflead_hn, 'Closed after minutes and feedback.', DATE_SUB(@seed_now, INTERVAL 28 DAY)),
  (@vr_cancelled, @vi_cn_hn, 'CAMPUS_INSTANCE', 'WAITING_REQUEST_APPROVAL', 'CANCELLED', @v_nguyen_no_dau, 'Visitor cancelled.', DATE_SUB(@seed_now, INTERVAL 18 DAY)),
  (@vr_assigned_only, @vi_ao_dn, 'CAMPUS_INSTANCE', 'WAITING_REQUEST_APPROVAL', 'ASSIGNED', @u_stafflead_dn, 'Host assigned.', DATE_SUB(@seed_now, INTERVAL 3 DAY));




-- DEV account quick check: all listed accounts should be ACTIVE and use @pwd_hash.
SELECT email, full_name, status, role_id, sub_role
FROM users
WHERE email IN (
  'admin@fpt.edu.vn','ho@fpt.edu.vn','staff.leader.hn@fpt.edu.vn','staff.hn@fpt.edu.vn',
  'dept.leader.hn@fpt.edu.vn','dept.hn@fpt.edu.vn','student@fpt.edu.vn','visitor@example.com'
)
ORDER BY email;

-- RBAC quick check: expected effective-role buckets are ADMIN/NONE, HO/NONE,
-- STAFF/Leader, STAFF/Staff, DEPARTMENT/Leader, DEPARTMENT/Staff, STUDENT/NONE, VISITOR/NONE.
SELECT r.role_code, rp.sub_role, COUNT(*) AS total_permissions
FROM role_permissions rp
JOIN roles r ON r.role_id = rp.role_id
GROUP BY r.role_code, rp.sub_role
ORDER BY r.role_code, rp.sub_role;

SET SQL_SAFE_UPDATES = @OLD_SQL_SAFE_UPDATES;

-- =====================================================================
-- Coverage verification queries
-- =====================================================================
SELECT 'PEMS v4.5 high-quality scenario seed completed' AS message, @seed_now AS seed_runtime;
SELECT 'users' table_name, COUNT(*) row_count FROM users
UNION ALL SELECT 'permissions', COUNT(*) FROM permissions
UNION ALL SELECT 'role_permissions', COUNT(*) FROM role_permissions
UNION ALL SELECT 'partners', COUNT(*) FROM partners
UNION ALL SELECT 'visit_requests', COUNT(*) FROM visit_requests
UNION ALL SELECT 'visit_request_campuses', COUNT(*) FROM visit_request_campuses
UNION ALL SELECT 'visit_logistics_items', COUNT(*) FROM visit_logistics_items
UNION ALL SELECT 'news', COUNT(*) FROM news
UNION ALL SELECT 'audit_logs', COUNT(*) FROM audit_logs;
SELECT status, COUNT(*) total FROM users GROUP BY status ORDER BY status;
SELECT status, COUNT(*) total FROM visit_requests GROUP BY status ORDER BY status;
SELECT status, COUNT(*) total FROM visit_request_campuses GROUP BY status ORDER BY status;
SELECT status, COUNT(*) total FROM visit_logistics_items GROUP BY status ORDER BY status;
SELECT r.role_code, COUNT(u.user_id) user_count FROM roles r LEFT JOIN users u ON u.role_id=r.role_id GROUP BY r.role_code ORDER BY r.role_code;
SELECT DATE_FORMAT(submitted_at, '%Y-%m') submitted_month, status, COUNT(*) total FROM visit_requests GROUP BY DATE_FORMAT(submitted_at, '%Y-%m'), status ORDER BY submitted_month, status;



-- =====================================================================
-- FINAL STANDARD MANUAL SEED SYNCHRONIZATION
-- Source: /database/seed/run_all_dev_seed.sql
--
-- Purpose:
-- - Re-apply canonical manual seed files after the full scenario seed.
-- - Keep RBAC, dev accounts, campuses, departments and permissions aligned
--   with Permission Matrix v0.2.
-- - This block is idempotent and safe for local/dev reruns.
--
-- Important:
-- - The full file above may contain rich scenario seed data.
-- - This final block does NOT drop scenario data.
-- - It only upserts the canonical manual seed baseline.
-- =====================================================================

-- =====================================================================
-- PEMS — Run all manual seed scripts for DEV/LOCAL
-- This is a convenience concatenation. Do not run dev_accounts on production.
-- =====================================================================


-- >>> BEGIN roles.sql

-- =====================================================================
-- PEMS — Roles seed (idempotent)
-- Run AFTER pems_full.sql and BEFORE permissions/permission_matrix/dev_accounts.
-- =====================================================================
USE pems_db;

START TRANSACTION;

INSERT INTO roles (role_id, role_code, name, description, status, created_at, deleted_at, deleted_by)
VALUES
  (NULL, 'ADMIN',   'Admin',       'Quản trị kỹ thuật hệ thống', 'ACTIVE', NOW(), NULL, NULL),
  (NULL, 'HO',      'Head Office', 'Quản lý cấp Head Office', 'ACTIVE', NOW(), NULL, NULL),
  (NULL, 'STAFF',   'IC Staff',    'Nhân sự phòng Hợp tác Quốc tế, dùng users.sub_role = LEADER/STAFF', 'ACTIVE', NOW(), NULL, NULL),
  (NULL, 'DEPARTMENT',    'Department',  'Nhân sự phòng ban khác, dùng users.sub_role = LEADER/STAFF', 'ACTIVE', NOW(), NULL, NULL),
  (NULL, 'STUDENT', 'Student',     'Sinh viên hỗ trợ', 'ACTIVE', NOW(), NULL, NULL),
  (NULL, 'VISITOR', 'Visitor',     'Khách gửi visit request và theo dõi thông tin của mình', 'ACTIVE', NOW(), NULL, NULL)
ON DUPLICATE KEY UPDATE
  name = VALUES(name),
  description = VALUES(description),
  status = VALUES(status),
  deleted_at = NULL,
  deleted_by = NULL;

SELECT role_code, name, status, deleted_at
FROM roles
WHERE role_code IN ('ADMIN','HO','STAFF','DEPARTMENT','STUDENT','VISITOR')
ORDER BY FIELD(role_code, 'ADMIN','HO','STAFF','DEPARTMENT','STUDENT','VISITOR');

COMMIT;


-- <<< END roles.sql


-- >>> BEGIN campuses.sql

-- =====================================================================
-- PEMS — Campuses seed (idempotent)
-- Seeds 5 FPT campuses only. Run before departments.sql and dev_accounts.sql.
-- =====================================================================
USE pems_db;

START TRANSACTION;

INSERT INTO campuses (campus_id, campus_code, name, city, status, created_at)
VALUES
  (NULL, 'HN',  'FPT University Hà Nội',          'Hà Nội',          'ACTIVE', NOW()),
  (NULL, 'HCM', 'FPT University TP. Hồ Chí Minh', 'TP. Hồ Chí Minh', 'ACTIVE', NOW()),
  (NULL, 'DN',  'FPT University Đà Nẵng',         'Đà Nẵng',         'ACTIVE', NOW()),
  (NULL, 'CT',  'FPT University Cần Thơ',         'Cần Thơ',         'ACTIVE', NOW()),
  (NULL, 'QN',  'FPT University Quy Nhơn',        'Quy Nhơn',        'ACTIVE', NOW())
ON DUPLICATE KEY UPDATE
  name = VALUES(name),
  city = VALUES(city),
  status = VALUES(status);

SELECT campus_code, name, city, status
FROM campuses
WHERE campus_code IN ('HN','HCM','DN','CT','QN')
ORDER BY FIELD(campus_code, 'HN','HCM','DN','CT','QN');

COMMIT;


-- <<< END campuses.sql


-- >>> BEGIN departments.sql

-- =====================================================================
-- PEMS — Core departments seed (trigger-safe, idempotent)
-- Run AFTER campuses.sql and BEFORE dev_accounts.sql.
-- IMPORTANT:
-- - Do NOT re-insert existing ACTIVE IC departments with ON DUPLICATE.
-- - The departments trigger allows only one ACTIVE IC per campus and can fire
--   before duplicate-key handling. Therefore existing rows are updated first,
--   then only missing rows are inserted.
-- =====================================================================
USE pems_db;

START TRANSACTION;

DROP TEMPORARY TABLE IF EXISTS desired_departments;
CREATE TEMPORARY TABLE desired_departments (
  campus_code VARCHAR(20) NOT NULL,
  department_code VARCHAR(50) NOT NULL,
  name VARCHAR(150) NOT NULL,
  department_type ENUM('IC','GENERAL') NOT NULL,
  status ENUM('ACTIVE','INACTIVE') NOT NULL,
  PRIMARY KEY (campus_code, department_code)
);

INSERT INTO desired_departments (campus_code, department_code, name, department_type, status)
VALUES
  ('HN',  'IC',        'Phòng Hợp tác Quốc tế', 'IC',      'ACTIVE'),
  ('HN',  'ACADEMIC',  'Phòng Đào tạo',         'GENERAL', 'ACTIVE'),
  ('HN',  'MARKETING', 'Phòng Truyền thông',    'GENERAL', 'ACTIVE'),
  ('HN',  'ADMISSION', 'Phòng Tuyển sinh',      'GENERAL', 'ACTIVE'),
  ('HN',  'IT',        'Phòng CNTT',            'GENERAL', 'ACTIVE'),
  ('HCM', 'IC',        'Phòng Hợp tác Quốc tế', 'IC',      'ACTIVE'),
  ('HCM', 'ACADEMIC',  'Phòng Đào tạo',         'GENERAL', 'ACTIVE'),
  ('HCM', 'MARKETING', 'Phòng Truyền thông',    'GENERAL', 'ACTIVE'),
  ('HCM', 'ADMISSION', 'Phòng Tuyển sinh',      'GENERAL', 'ACTIVE'),
  ('HCM', 'IT',        'Phòng CNTT',            'GENERAL', 'ACTIVE'),
  ('DN',  'IC',        'Phòng Hợp tác Quốc tế', 'IC',      'ACTIVE'),
  ('DN',  'ACADEMIC',  'Phòng Đào tạo',         'GENERAL', 'ACTIVE'),
  ('DN',  'MARKETING', 'Phòng Truyền thông',    'GENERAL', 'ACTIVE'),
  ('DN',  'ADMISSION', 'Phòng Tuyển sinh',      'GENERAL', 'ACTIVE'),
  ('DN',  'IT',        'Phòng CNTT',            'GENERAL', 'ACTIVE'),
  ('CT',  'IC',        'Phòng Hợp tác Quốc tế', 'IC',      'ACTIVE'),
  ('CT',  'ACADEMIC',  'Phòng Đào tạo',         'GENERAL', 'ACTIVE'),
  ('CT',  'MARKETING', 'Phòng Truyền thông',    'GENERAL', 'ACTIVE'),
  ('CT',  'ADMISSION', 'Phòng Tuyển sinh',      'GENERAL', 'ACTIVE'),
  ('CT',  'IT',        'Phòng CNTT',            'GENERAL', 'ACTIVE'),
  ('QN',  'IC',        'Phòng Hợp tác Quốc tế', 'IC',      'ACTIVE'),
  ('QN',  'ACADEMIC',  'Phòng Đào tạo',         'GENERAL', 'ACTIVE'),
  ('QN',  'MARKETING', 'Phòng Truyền thông',    'GENERAL', 'ACTIVE'),
  ('QN',  'ADMISSION', 'Phòng Tuyển sinh',      'GENERAL', 'ACTIVE'),
  ('QN',  'IT',        'Phòng CNTT',            'GENERAL', 'ACTIVE');

-- Update rows that already exist by natural key (campus_id + department_code).
UPDATE departments d
JOIN campuses c ON c.campus_id = d.campus_id
JOIN desired_departments dd
  ON dd.campus_code = c.campus_code
 AND dd.department_code = d.department_code
SET
  d.name = dd.name,
  d.department_type = dd.department_type,
  d.status = dd.status,
  d.updated_at = NOW();

-- Insert only rows that do not exist yet. This avoids firing the one-active-IC
-- trigger for duplicate IC departments.
INSERT INTO departments (department_id, campus_id, department_code, name, department_type, status, created_at)
SELECT NULL, c.campus_id, dd.department_code, dd.name, dd.department_type, dd.status, NOW()
FROM desired_departments dd
JOIN campuses c ON c.campus_code = dd.campus_code
WHERE NOT EXISTS (
  SELECT 1
  FROM departments d
  WHERE d.campus_id = c.campus_id
    AND d.department_code = dd.department_code
);

SELECT c.campus_code, d.department_code, d.name, d.department_type, d.status
FROM departments d
JOIN campuses c ON c.campus_id = d.campus_id
WHERE c.campus_code IN ('HN','HCM','DN','CT','QN')
  AND d.department_code IN ('IC','ACADEMIC','MARKETING','ADMISSION','IT')
ORDER BY c.campus_code, d.department_code;

DROP TEMPORARY TABLE IF EXISTS desired_departments;

COMMIT;


-- <<< END departments.sql


-- >>> BEGIN permissions.sql

-- =====================================================================
-- PEMS — Permissions seed from docs/permissions/PERMISSION_MATRIX.md v0.2
-- Idempotent: permission_code is unique, so rerun updates name/group/description.
-- Run AFTER roles.sql and BEFORE permission_matrix.sql.
-- =====================================================================
USE pems_db;

START TRANSACTION;

INSERT INTO permissions
  (permission_id, permission_code, name, permission_group, description, is_system, created_at)
VALUES
  (NULL, 'UC-01.VIEW_HOMEPAGE', 'UC-01 - View Homepage', 'Common', 'Seeded from Permission Matrix v0.2: View Homepage', TRUE, NOW()),
  (NULL, 'UC-02.SEARCH_INFORMATION', 'UC-02 - Search Information', 'Common', 'Seeded from Permission Matrix v0.2: Search Information', TRUE, NOW()),
  (NULL, 'UC-03.VIEW_CONTACT_INFO', 'UC-03 - View Contact Info', 'Common', 'Seeded from Permission Matrix v0.2: View Contact Info', TRUE, NOW()),
  (NULL, 'UC-04.VIEW_POLICY_AND_TERMS', 'UC-04 - View Policy & Terms', 'Common', 'Seeded from Permission Matrix v0.2: View Policy & Terms', TRUE, NOW()),
  (NULL, 'UC-05.VIEW_FAQ', 'UC-05 - View FAQ', 'Common', 'Seeded from Permission Matrix v0.2: View FAQ', TRUE, NOW()),
  (NULL, 'UC-06.VIEW_NEWS', 'UC-06 - View News', 'Common', 'Seeded from Permission Matrix v0.2: View News', TRUE, NOW()),
  (NULL, 'UC-07.VIEW_PARTNERS', 'UC-07 - View Partners', 'Common', 'Seeded from Permission Matrix v0.2: View Partners', TRUE, NOW()),
  (NULL, 'UC-08.VIEW_GALLERY', 'UC-08 - View Gallery', 'Common', 'Seeded from Permission Matrix v0.2: View Gallery', TRUE, NOW()),
  (NULL, 'UC-09.VIEW_NOTIFICATIONS', 'UC-09 - View Notifications', 'Common', 'Seeded from Permission Matrix v0.2: View Notifications', TRUE, NOW()),
  (NULL, 'UC-10.LOGIN_VIA_SSO', 'UC-10 - Login via SSO', 'Authentication', 'Seeded from Permission Matrix v0.2: Login via SSO', TRUE, NOW()),
  (NULL, 'UC-11.LOGIN_VIA_CREDENTIALS', 'UC-11 - Login via Credentials', 'Authentication', 'Seeded from Permission Matrix v0.2: Login via Credentials', TRUE, NOW()),
  (NULL, 'UC-12.LOGOUT', 'UC-12 - Logout', 'Authentication', 'Seeded from Permission Matrix v0.2: Logout', TRUE, NOW()),
  (NULL, 'UC-13.FORGOT_PASSWORD', 'UC-13 - Forgot Password', 'Authentication', 'Seeded from Permission Matrix v0.2: Forgot Password', TRUE, NOW()),
  (NULL, 'UC-14.VIEW_PROFILE', 'UC-14 - View Profile', 'Profile Management', 'Seeded from Permission Matrix v0.2: View Profile', TRUE, NOW()),
  (NULL, 'UC-15.UPDATE_PROFILE', 'UC-15 - Update Profile', 'Profile Management', 'Seeded from Permission Matrix v0.2: Update Profile', TRUE, NOW()),
  (NULL, 'UC-16.CHANGE_PASSWORD', 'UC-16 - Change Password', 'Profile Management', 'Seeded from Permission Matrix v0.2: Change Password', TRUE, NOW()),
  (NULL, 'UC-17.SUBMIT_VISIT_REQUEST', 'UC-17 - Submit Visit Request', 'Delegation Reception Management', 'Seeded from Permission Matrix v0.2: Submit Visit Request', TRUE, NOW()),
  (NULL, 'UC-18.APPROVE_CROSS_CAMPUS_REQUEST', 'UC-18 - Approve Cross-Campus Request', 'Delegation Reception Management', 'Seeded from Permission Matrix v0.2: Approve Cross-Campus Request', TRUE, NOW()),
  (NULL, 'UC-19.VIEW_GUEST_DELEGATION_DETAILS', 'UC-19 - View Guest Delegation Details', 'Delegation Reception Management', 'Seeded from Permission Matrix v0.2: View Guest Delegation Details', TRUE, NOW()),
  (NULL, 'UC-20.VIEW_GUEST_DELEGATION_LIST', 'UC-20 - View Guest Delegation List', 'Delegation Reception Management', 'Seeded from Permission Matrix v0.2: View Guest Delegation List', TRUE, NOW()),
  (NULL, 'UC-21.SEARCH_DELEGATIONS', 'UC-21 - Search Delegations', 'Delegation Reception Management', 'Seeded from Permission Matrix v0.2: Search Delegations', TRUE, NOW()),
  (NULL, 'UC-22.PROCESS_VISIT_REQUEST', 'UC-22 - Process Visit Request', 'Delegation Reception Management', 'Seeded from Permission Matrix v0.2: Process Visit Request', TRUE, NOW()),
  (NULL, 'UC-23.CREATE_GUEST_DELEGATION', 'UC-23 - Create Guest Delegation', 'Delegation Reception Management', 'Seeded from Permission Matrix v0.2: Create Guest Delegation', TRUE, NOW()),
  (NULL, 'UC-24.UPDATE_GUEST_DELEGATION', 'UC-24 - Update Guest Delegation', 'Delegation Reception Management', 'Seeded from Permission Matrix v0.2: Update Guest Delegation', TRUE, NOW()),
  (NULL, 'UC-25.PREPARE_VISIT_LOGISTICS', 'UC-25 - Prepare Visit Logistics', 'Delegation Reception Management', 'Seeded from Permission Matrix v0.2: Prepare Visit Logistics', TRUE, NOW()),
  (NULL, 'UC-26.UPDATE_VISIT_LOGISTICS', 'UC-26 - Update Visit Logistics', 'Delegation Reception Management', 'Seeded from Permission Matrix v0.2: Update Visit Logistics', TRUE, NOW()),
  (NULL, 'UC-27.CONFIRM_PARTICIPATION', 'UC-27 - Confirm Participation', 'Delegation Reception Management', 'Seeded from Permission Matrix v0.2: Confirm Participation', TRUE, NOW()),
  (NULL, 'UC-28.APPROVE_RESOURCE_REQUEST', 'UC-28 - Approve Resource Request', 'Delegation Reception Management', 'Seeded from Permission Matrix v0.2: Approve Resource Request', TRUE, NOW()),
  (NULL, 'UC-29.PROPOSE_RESOURCE_MODIFICATION', 'UC-29 - Propose Resource Modification', 'Delegation Reception Management', 'Seeded from Permission Matrix v0.2: Propose Resource Modification', TRUE, NOW()),
  (NULL, 'UC-30.CONFIRM_THE_CHANGE_PROPOSAL', 'UC-30 - Confirm The Change Proposal', 'Delegation Reception Management', 'Seeded from Permission Matrix v0.2: Confirm The Change Proposal', TRUE, NOW()),
  (NULL, 'UC-31.CREATE_MEETING_MINUTES', 'UC-31 - Create Meeting Minutes', 'Delegation Reception Management', 'Seeded from Permission Matrix v0.2: Create Meeting Minutes', TRUE, NOW()),
  (NULL, 'UC-32.EDIT_MEETING_MINUTES', 'UC-32 - Edit Meeting Minutes', 'Delegation Reception Management', 'Seeded from Permission Matrix v0.2: Edit Meeting Minutes', TRUE, NOW()),
  (NULL, 'UC-33.VIEW_MEETING_MINUTES_DETAILS', 'UC-33 - View Meeting Minutes Details', 'Delegation Reception Management', 'Seeded from Permission Matrix v0.2: View Meeting Minutes Details', TRUE, NOW()),
  (NULL, 'UC-34.SUBMIT_DELEGATION_FEEDBACK', 'UC-34 - Submit Delegation Feedback', 'Delegation Reception Management', 'Seeded from Permission Matrix v0.2: Submit Delegation Feedback', TRUE, NOW()),
  (NULL, 'UC-35.SCAN_BUSINESS_CARD', 'UC-35 - Scan Business Card', 'Delegation Reception Management', 'Seeded from Permission Matrix v0.2: Scan Business Card', TRUE, NOW()),
  (NULL, 'UC-36.CREATE_PARTNER_PROFILE', 'UC-36 - Create Partner Profile', 'Delegation Reception Management', 'Seeded from Permission Matrix v0.2: Create Partner Profile', TRUE, NOW()),
  (NULL, 'UC-37.UPLOAD_ATTACHED_DOCUMENTS', 'UC-37 - Upload Attached Documents', 'Delegation Reception Management', 'Seeded from Permission Matrix v0.2: Upload Attached Documents', TRUE, NOW()),
  (NULL, 'UC-38.UPLOAD_VISIT_PHOTOS', 'UC-38 - Upload Visit Photos', 'Delegation Reception Management', 'Seeded from Permission Matrix v0.2: Upload Visit Photos', TRUE, NOW()),
  (NULL, 'UC-39.TAG_FACES_ON_PHOTOS', 'UC-39 - Tag Faces on Photos', 'Delegation Reception Management', 'Seeded from Permission Matrix v0.2: Tag Faces on Photos', TRUE, NOW()),
  (NULL, 'UC-40.CREATE_NEWS_ARTICLE', 'UC-40 - Create News Article', 'Delegation Reception Management', 'Seeded from Permission Matrix v0.2: Create News Article', TRUE, NOW()),
  (NULL, 'UC-41.CLOSE_DELEGATION', 'UC-41 - Close Delegation', 'Delegation Reception Management', 'Seeded from Permission Matrix v0.2: Close Delegation', TRUE, NOW()),
  (NULL, 'UC-42.VIEW_EMAIL_TEMPLATE_LIST', 'UC-42 - View Email Template List', 'Email Management', 'Seeded from Permission Matrix v0.2: View Email Template List', TRUE, NOW()),
  (NULL, 'UC-43.VIEW_EMAIL_TEMPLATE_DETAIL', 'UC-43 - View Email Template Detail', 'Email Management', 'Seeded from Permission Matrix v0.2: View Email Template Detail', TRUE, NOW()),
  (NULL, 'UC-44.UPDATE_EMAIL_TEMPLATE', 'UC-44 - Update Email Template', 'Email Management', 'Seeded from Permission Matrix v0.2: Update Email Template', TRUE, NOW()),
  (NULL, 'UC-45.CREATE_EMAIL_TEMPLATE', 'UC-45 - Create Email Template', 'Email Management', 'Seeded from Permission Matrix v0.2: Create Email Template', TRUE, NOW()),
  (NULL, 'UC-46.EDIT_EMAIL_CONTENT', 'UC-46 - Edit Email Content', 'Email Management', 'Seeded from Permission Matrix v0.2: Edit Email Content', TRUE, NOW()),
  (NULL, 'UC-47.SEND_EMAIL', 'UC-47 - Send Email', 'Email Management', 'Seeded from Permission Matrix v0.2: Send Email', TRUE, NOW()),
  (NULL, 'UC-48.VIEW_EMAIL', 'UC-48 - View Email', 'Email Management', 'Seeded from Permission Matrix v0.2: View Email', TRUE, NOW()),
  (NULL, 'UC-49.REPLY_TO_EMAIL', 'UC-49 - Reply to Email', 'Email Management', 'Seeded from Permission Matrix v0.2: Reply to Email', TRUE, NOW()),
  (NULL, 'UC-50.PROCESS_PARTNER_CREATION_REQUEST', 'UC-50 - Process Partner Creation Request', 'Partner Management', 'Seeded from Permission Matrix v0.2: Process Partner Creation Request', TRUE, NOW()),
  (NULL, 'UC-51.EDIT_PARTNER_INFORMATION', 'UC-51 - Edit Partner Information', 'Partner Management', 'Seeded from Permission Matrix v0.2: Edit Partner Information', TRUE, NOW()),
  (NULL, 'UC-52.VIEW_PARTNER_LISTS', 'UC-52 - View Partner Lists', 'Partner Management', 'Seeded from Permission Matrix v0.2: View Partner Lists', TRUE, NOW()),
  (NULL, 'UC-53.SEARCH_PARTNERS', 'UC-53 - Search Partners', 'Partner Management', 'Seeded from Permission Matrix v0.2: Search Partners', TRUE, NOW()),
  (NULL, 'UC-54.VIEW_PARTNER_DETAILS', 'UC-54 - View Partner Details', 'Partner Management', 'Seeded from Permission Matrix v0.2: View Partner Details', TRUE, NOW()),
  (NULL, 'UC-55.VIEW_DOCUMENT_LIST', 'UC-55 - View Document List', 'Document Management', 'Seeded from Permission Matrix v0.2: View Document List', TRUE, NOW()),
  (NULL, 'UC-56.SEARCH_DOCUMENTS', 'UC-56 - Search Documents', 'Document Management', 'Seeded from Permission Matrix v0.2: Search Documents', TRUE, NOW()),
  (NULL, 'UC-57.VIEW_GALLERY_ITEM_LIST', 'UC-57 - View Gallery Item List', 'Gallery Management', 'Seeded from Permission Matrix v0.2: View Gallery Item List', TRUE, NOW()),
  (NULL, 'UC-58.SEARCH_GALLERY_ITEMS', 'UC-58 - Search Gallery Items', 'Gallery Management', 'Seeded from Permission Matrix v0.2: Search Gallery Items', TRUE, NOW()),
  (NULL, 'UC-59.ADD_GALLERY_ITEM', 'UC-59 - Add Gallery Item', 'Gallery Management', 'Seeded from Permission Matrix v0.2: Add Gallery Item', TRUE, NOW()),
  (NULL, 'UC-60.UPDATE_GALLERY_ITEM', 'UC-60 - Update Gallery Item', 'Gallery Management', 'Seeded from Permission Matrix v0.2: Update Gallery Item', TRUE, NOW()),
  (NULL, 'UC-61.DELETE_GALLERY_ITEM', 'UC-61 - Delete Gallery Item', 'Gallery Management', 'Seeded from Permission Matrix v0.2: Delete Gallery Item', TRUE, NOW()),
  (NULL, 'UC-62.VIEW_MINUTES_LIST', 'UC-62 - View Minutes List', 'Minutes Management', 'Seeded from Permission Matrix v0.2: View Minutes List', TRUE, NOW()),
  (NULL, 'UC-63.SEARCH_FILTER_MINUTES', 'UC-63 - Search/Filter Minutes', 'Minutes Management', 'Seeded from Permission Matrix v0.2: Search/Filter Minutes', TRUE, NOW()),
  (NULL, 'UC-64.VIEW_LIST_FAQ', 'UC-64 - View List FAQ', 'FAQ Management', 'Seeded from Permission Matrix v0.2: View List FAQ', TRUE, NOW()),
  (NULL, 'UC-65.CREATE_FAQ', 'UC-65 - Create FAQ', 'FAQ Management', 'Seeded from Permission Matrix v0.2: Create FAQ', TRUE, NOW()),
  (NULL, 'UC-66.UPDATE_FAQ', 'UC-66 - Update FAQ', 'FAQ Management', 'Seeded from Permission Matrix v0.2: Update FAQ', TRUE, NOW()),
  (NULL, 'UC-67.CHANGE_FAQ_VISIBILITY', 'UC-67 - Change FAQ Visibility', 'FAQ Management', 'Seeded from Permission Matrix v0.2: Change FAQ Visibility', TRUE, NOW()),
  (NULL, 'UC-68.SEARCH_FAQ', 'UC-68 - Search FAQ', 'FAQ Management', 'Seeded from Permission Matrix v0.2: Search FAQ', TRUE, NOW()),
  (NULL, 'UC-69.VIEW_DASHBOARD_STATISTICS', 'UC-69 - View Dashboard Statistics', 'Report Management', 'Seeded from Permission Matrix v0.2: View Dashboard Statistics', TRUE, NOW()),
  (NULL, 'UC-70.EXPORT_STATISTICS_REPORT', 'UC-70 - Export Statistics Report', 'Report Management', 'Seeded from Permission Matrix v0.2: Export Statistics Report', TRUE, NOW()),
  (NULL, 'UC-71.FILTER_DASHBOARD_BY_TIME', 'UC-71 - Filter Dashboard By Time', 'Report Management', 'Seeded from Permission Matrix v0.2: Filter Dashboard By Time', TRUE, NOW()),
  (NULL, 'UC-72.VIEW_MY_EVENTS', 'UC-72 - View My Events', 'Calendar Management', 'Seeded from Permission Matrix v0.2: View My Events', TRUE, NOW()),
  (NULL, 'UC-73.VIEW_DEPARTMENT_CALENDAR', 'UC-73 - View Department Calendar', 'Calendar Management', 'Seeded from Permission Matrix v0.2: View Department Calendar', TRUE, NOW()),
  (NULL, 'UC-74.SWITCH_VIEW_MODE', 'UC-74 - Switch View Mode', 'Calendar Management', 'Seeded from Permission Matrix v0.2: Switch View Mode', TRUE, NOW()),
  (NULL, 'UC-75.ADD_PERSONAL_EVENT', 'UC-75 - Add Personal Event', 'Calendar Management', 'Seeded from Permission Matrix v0.2: Add Personal Event', TRUE, NOW()),
  (NULL, 'UC-76.DELETE_PERSONAL_EVENT', 'UC-76 - Delete Personal Event', 'Calendar Management', 'Seeded from Permission Matrix v0.2: Delete Personal Event', TRUE, NOW()),
  (NULL, 'UC-77.UPDATE_PERSONAL_EVENT', 'UC-77 - Update Personal Event', 'Calendar Management', 'Seeded from Permission Matrix v0.2: Update Personal Event', TRUE, NOW()),
  (NULL, 'UC-78.VIEW_EVENT_DETAILS', 'UC-78 - View Event Details', 'Calendar Management', 'Seeded from Permission Matrix v0.2: View Event Details', TRUE, NOW()),
  (NULL, 'UC-79.SEARCH_FILTER_FEEDBACK', 'UC-79 - Search/Filter Feedback', 'Feedback Management', 'Seeded from Permission Matrix v0.2: Search/Filter Feedback', TRUE, NOW()),
  (NULL, 'UC-80.VIEW_FEEDBACK_SUMMARY', 'UC-80 - View Feedback Summary', 'Feedback Management', 'Seeded from Permission Matrix v0.2: View Feedback Summary', TRUE, NOW()),
  (NULL, 'UC-81.ADD_NEW_CAMPUS', 'UC-81 - Add New Campus', 'Campus Management', 'Seeded from Permission Matrix v0.2: Add New Campus', TRUE, NOW()),
  (NULL, 'UC-82.VIEW_CAMPUS_LIST', 'UC-82 - View Campus List', 'Campus Management', 'Seeded from Permission Matrix v0.2: View Campus List', TRUE, NOW()),
  (NULL, 'UC-83.SEARCH_AND_FILTER_CAMPUS', 'UC-83 - Search and Filter Campus', 'Campus Management', 'Seeded from Permission Matrix v0.2: Search and Filter Campus', TRUE, NOW()),
  (NULL, 'UC-84.VIEW_CAMPUS_DETAILS', 'UC-84 - View Campus Details', 'Campus Management', 'Seeded from Permission Matrix v0.2: View Campus Details', TRUE, NOW()),
  (NULL, 'UC-85.UPDATE_CAMPUS', 'UC-85 - Update Campus', 'Campus Management', 'Seeded from Permission Matrix v0.2: Update Campus', TRUE, NOW()),
  (NULL, 'UC-86.MANAGE_CAMPUS_STATUS', 'UC-86 - Manage Campus Status', 'Campus Management', 'Seeded from Permission Matrix v0.2: Manage Campus Status', TRUE, NOW()),
  (NULL, 'UC-87.ASSIGN_CAMPUS_LEAD', 'UC-87 - Assign Campus Lead', 'Campus Management', 'Seeded from Permission Matrix v0.2: Assign Campus Lead', TRUE, NOW()),
  (NULL, 'UC-88.APPROVE_NEWS', 'UC-88 - Approve News', 'News Management', 'Seeded from Permission Matrix v0.2: Approve News', TRUE, NOW()),
  (NULL, 'UC-89.PUBLISH_NEWS', 'UC-89 - Publish News', 'News Management', 'Seeded from Permission Matrix v0.2: Publish News', TRUE, NOW()),
  (NULL, 'UC-90.VIEW_NEWS_LIST', 'UC-90 - View News List', 'News Management', 'Seeded from Permission Matrix v0.2: View News List', TRUE, NOW()),
  (NULL, 'UC-91.VIEW_NEWS_DETAILS', 'UC-91 - View News Details', 'News Management', 'Seeded from Permission Matrix v0.2: View News Details', TRUE, NOW()),
  (NULL, 'UC-92.ADD_MULTILINGUAL_NEWS', 'UC-92 - Add Multilingual News', 'News Management', 'Seeded from Permission Matrix v0.2: Add Multilingual News', TRUE, NOW()),
  (NULL, 'UC-93.MANAGE_NEWS_VISIBILITY', 'UC-93 - Manage News Visibility', 'News Management', 'Seeded from Permission Matrix v0.2: Manage News Visibility', TRUE, NOW()),
  (NULL, 'UC-94.EDIT_NEWS', 'UC-94 - Edit News', 'News Management', 'Seeded from Permission Matrix v0.2: Edit News', TRUE, NOW()),
  (NULL, 'UC-95.VIEW_ACCOUNT_LIST', 'UC-95 - View Account List', 'Account Management', 'Seeded from Permission Matrix v0.2: View Account List', TRUE, NOW()),
  (NULL, 'UC-96.CREATE_ACCOUNT', 'UC-96 - Create Account', 'Account Management', 'Seeded from Permission Matrix v0.2: Create Account', TRUE, NOW()),
  (NULL, 'UC-97.MANAGE_ACCOUNT_STATUS', 'UC-97 - Manage Account Status', 'Account Management', 'Seeded from Permission Matrix v0.2: Manage Account Status', TRUE, NOW()),
  (NULL, 'UC-98.VIEW_ACCOUNT_DETAILS', 'UC-98 - View Account Details', 'Account Management', 'Seeded from Permission Matrix v0.2: View Account Details', TRUE, NOW()),
  (NULL, 'UC-99.SEARCH_AND_FILTER_ACCOUNTS', 'UC-99 - Search and Filter Accounts', 'Account Management', 'Seeded from Permission Matrix v0.2: Search and Filter Accounts', TRUE, NOW()),
  (NULL, 'UC-100.UPDATE_ACCOUNT_ROLE', 'UC-100 - Update Account Role', 'Account Management', 'Seeded from Permission Matrix v0.2: Update Account Role', TRUE, NOW()),
  (NULL, 'UC-101.ADD_NEW_DEPARTMENT', 'UC-101 - Add New Department', 'Department Management', 'Seeded from Permission Matrix v0.2: Add New Department', TRUE, NOW()),
  (NULL, 'UC-102.UPDATE_DEPARTMENT', 'UC-102 - Update Department', 'Department Management', 'Seeded from Permission Matrix v0.2: Update Department', TRUE, NOW()),
  (NULL, 'UC-103.SEARCH_AND_FILTER_DEPARTMENTS', 'UC-103 - Search and Filter Departments', 'Department Management', 'Seeded from Permission Matrix v0.2: Search and Filter Departments', TRUE, NOW()),
  (NULL, 'UC-104.VIEW_DEPARTMENT_LIST', 'UC-104 - View Department List', 'Department Management', 'Seeded from Permission Matrix v0.2: View Department List', TRUE, NOW()),
  (NULL, 'UC-105.VIEW_DEPARTMENT_DETAILS', 'UC-105 - View Department Details', 'Department Management', 'Seeded from Permission Matrix v0.2: View Department Details', TRUE, NOW()),
  (NULL, 'UC-106.MANAGE_DEPARTMENT_STATUS', 'UC-106 - Manage Department Status', 'Department Management', 'Seeded from Permission Matrix v0.2: Manage Department Status', TRUE, NOW()),
  (NULL, 'UC-107.ADD_DEPARTMENT_PERSONNEL', 'UC-107 - Add Department Personnel', 'Department Management', 'Seeded from Permission Matrix v0.2: Add Department Personnel', TRUE, NOW()),
  (NULL, 'UC-108.VIEW_PERSONNEL_DETAILS', 'UC-108 - View Personnel Details', 'Department Management', 'Seeded from Permission Matrix v0.2: View Personnel Details', TRUE, NOW()),
  (NULL, 'UC-109.SEARCH_PERSONNEL', 'UC-109 - Search Personnel', 'Department Management', 'Seeded from Permission Matrix v0.2: Search Personnel', TRUE, NOW()),
  (NULL, 'UC-110.REVIEW_ASSIGNED_TASKS', 'UC-110 - Review Assigned Tasks', 'Department Management', 'Seeded from Permission Matrix v0.2: Review Assigned Tasks', TRUE, NOW()),
  (NULL, 'UC-111.ASSIGN_TASKS', 'UC-111 - Assign Tasks', 'Department Management', 'Seeded from Permission Matrix v0.2: Assign Tasks', TRUE, NOW()),
  (NULL, 'UC-112.SIGN_THE_SERVICE_DELIVERY_REPORT', 'UC-112 - Sign The Service Delivery Report', 'Department Management', 'Seeded from Permission Matrix v0.2: Sign The Service Delivery Report', TRUE, NOW()),
  (NULL, 'UC-113.REMOVE_PERSONNEL', 'UC-113 - Remove Personnel', 'Department Management', 'Seeded from Permission Matrix v0.2: Remove Personnel', TRUE, NOW()),
  (NULL, 'UC-114.VIEW_COORDINATION_TASKS', 'UC-114 - View Coordination Tasks', 'Department Management', 'Seeded from Permission Matrix v0.2: View Coordination Tasks', TRUE, NOW()),
  (NULL, 'UC-115.SEARCH_COORDINATION_TASKS', 'UC-115 - Search Coordination Tasks', 'Department Management', 'Seeded from Permission Matrix v0.2: Search Coordination Tasks', TRUE, NOW()),
  (NULL, 'UC-116.REASSIGN_DEPARTMENT_LEAD', 'UC-116 - Reassign Department Lead', 'Department Management', 'Seeded from Permission Matrix v0.2: Reassign Department Lead', TRUE, NOW()),
  (NULL, 'UC-117.VIEW_ROLE_LIST', 'UC-117 - View Role List', 'Role & Permission Management', 'Seeded from Permission Matrix v0.2: View Role List', TRUE, NOW()),
  (NULL, 'UC-118.CREATE_NEW_ROLE', 'UC-118 - Create New Role', 'Role & Permission Management', 'Seeded from Permission Matrix v0.2: Create New Role', TRUE, NOW()),
  (NULL, 'UC-119.CONFIGURE_ROLE_PERMISSIONS', 'UC-119 - Configure Role Permissions', 'Role & Permission Management', 'Seeded from Permission Matrix v0.2: Configure Role Permissions', TRUE, NOW()),
  (NULL, 'UC-120.UPDATE_ROLE_DETAILS', 'UC-120 - Update Role Details', 'Role & Permission Management', 'Seeded from Permission Matrix v0.2: Update Role Details', TRUE, NOW()),
  (NULL, 'UC-121.DISABLE_DELETE_ROLE', 'UC-121 - Disable/Delete Role', 'Role & Permission Management', 'Seeded from Permission Matrix v0.2: Disable/Delete Role', TRUE, NOW()),
  (NULL, 'UC-122.VIEW_API_CONFIGURATION', 'UC-122 - View API Configuration', 'API Management', 'Seeded from Permission Matrix v0.2: View API Configuration', TRUE, NOW()),
  (NULL, 'UC-123.CREATE_API_CONFIGURATION', 'UC-123 - Create API Configuration', 'API Management', 'Seeded from Permission Matrix v0.2: Create API Configuration', TRUE, NOW()),
  (NULL, 'UC-124.UPDATE_API_CONFIGURATION', 'UC-124 - Update API Configuration', 'API Management', 'Seeded from Permission Matrix v0.2: Update API Configuration', TRUE, NOW()),
  (NULL, 'UC-125.DELETE_API_CONFIGURATION', 'UC-125 - Delete API Configuration', 'API Management', 'Seeded from Permission Matrix v0.2: Delete API Configuration', TRUE, NOW()),
  (NULL, 'UC-126.TEST_API_CONNECTION', 'UC-126 - Test API Connection', 'API Management', 'Seeded from Permission Matrix v0.2: Test API Connection', TRUE, NOW()),
  (NULL, 'UC-127.MANAGE_API_STATUS', 'UC-127 - Manage API Status', 'API Management', 'Seeded from Permission Matrix v0.2: Manage API Status', TRUE, NOW()),
  (NULL, 'UC-128.CONFIGURE_REQUEST_LIMIT', 'UC-128 - Configure Request Limit', 'API Management', 'Seeded from Permission Matrix v0.2: Configure Request Limit', TRUE, NOW()),
  (NULL, 'UC-129.VIEW_API_LOGS', 'UC-129 - View API Logs', 'API Management', 'Seeded from Permission Matrix v0.2: View API Logs', TRUE, NOW()),
  (NULL, 'UC-130.SEARCH_API_LOGS', 'UC-130 - Search API Logs', 'API Management', 'Seeded from Permission Matrix v0.2: Search API Logs', TRUE, NOW()),
  (NULL, 'UC-131.CREATE_AGENDA_TEMPLATE', 'UC-131 - Create Agenda Template', 'Agenda Templates Management', 'Seeded from Permission Matrix v0.2: Create Agenda Template', TRUE, NOW()),
  (NULL, 'UC-132.UPDATE_AGENDA_TEMPLATE', 'UC-132 - Update Agenda Template', 'Agenda Templates Management', 'Seeded from Permission Matrix v0.2: Update Agenda Template', TRUE, NOW()),
  (NULL, 'UC-133.DELETE_AGENDA_TEMPLATE', 'UC-133 - Delete Agenda Template', 'Agenda Templates Management', 'Seeded from Permission Matrix v0.2: Delete Agenda Template', TRUE, NOW()),
  (NULL, 'UC-134.VIEW_AGENDA_TEMPLATE_LIST', 'UC-134 - View Agenda Template List', 'Agenda Templates Management', 'Seeded from Permission Matrix v0.2: View Agenda Template List', TRUE, NOW()),
  (NULL, 'UC-135.VIEW_AGENDA_TEMPLATE_DETAIL', 'UC-135 - View Agenda Template Detail', 'Agenda Templates Management', 'Seeded from Permission Matrix v0.2: View Agenda Template Detail', TRUE, NOW())
ON DUPLICATE KEY UPDATE
  name = VALUES(name),
  permission_group = VALUES(permission_group),
  description = VALUES(description),
  is_system = VALUES(is_system);

SELECT COUNT(*) AS total_uc_permissions
FROM permissions
WHERE permission_code REGEXP '^UC-[0-9]+\.';

SELECT permission_group, COUNT(*) AS total_permissions
FROM permissions
WHERE permission_code REGEXP '^UC-[0-9]+\.'
GROUP BY permission_group
ORDER BY permission_group;

COMMIT;


-- v8.2: UC-136 Cancel Visit Request under Delegation Reception Management. Cancel is only allowed after approval; pending requests are ended by reject.
INSERT INTO permissions
  (permission_id, permission_code, name, permission_group, description, is_system, created_at)
VALUES
  (NULL, 'UC-136.CANCEL_VISIT_REQUEST', 'UC-136 - Cancel Visit Request', 'Delegation Reception Management',
   'Cancel an approved visit request/delegation within valid scope. Before approval, withdrawal is handled by reject in UC-18/UC-22. Visitor cancels own approved request; current Host cancels after external confirmation from guest.', TRUE, NOW())
ON DUPLICATE KEY UPDATE
  name = VALUES(name),
  permission_group = VALUES(permission_group),
  description = VALUES(description),
  is_system = VALUES(is_system);


-- <<< END permissions.sql


-- >>> BEGIN permission_matrix.sql

-- =====================================================================
-- PEMS — Role ⇄ Permission Matrix seed from docs/permissions/PERMISSION_MATRIX.md v0.2
-- Idempotent, manual-review friendly, Database-First RBAC.
--
-- Run order:
--   1) roles.sql
--   2) permissions.sql
--   3) permission_matrix.sql
--
-- RBAC convention:
--   role_permissions.sub_role = 'NONE' for ADMIN/HO/STUDENT/VISITOR.
--   role_permissions.sub_role = 'LEADER' or 'STAFF' for STAFF/DEPARTMENT.
--   users.sub_role remains NULL for ADMIN/HO/STUDENT/VISITOR.
-- =====================================================================
USE pems_db;

START TRANSACTION;

-- 1. Ensure canonical roles exist. This does not replace roles.sql; it makes this seed safer to rerun.
INSERT INTO roles (role_id, role_code, name, description, status, created_at, deleted_at, deleted_by)
VALUES
  (NULL, 'ADMIN',   'Admin',       'Quản trị kỹ thuật hệ thống', 'ACTIVE', NOW(), NULL, NULL),
  (NULL, 'HO',      'Head Office', 'Quản lý cấp Head Office', 'ACTIVE', NOW(), NULL, NULL),
  (NULL, 'STAFF',   'IC Staff',    'Nhân sự phòng Hợp tác Quốc tế, dùng users.sub_role = LEADER/STAFF', 'ACTIVE', NOW(), NULL, NULL),
  (NULL, 'DEPARTMENT',    'Department',  'Nhân sự phòng ban khác, dùng users.sub_role = LEADER/STAFF', 'ACTIVE', NOW(), NULL, NULL),
  (NULL, 'STUDENT', 'Student',     'Sinh viên hỗ trợ', 'ACTIVE', NOW(), NULL, NULL),
  (NULL, 'VISITOR', 'Visitor',     'Khách gửi visit request và theo dõi thông tin của mình', 'ACTIVE', NOW(), NULL, NULL)
ON DUPLICATE KEY UPDATE
  name = VALUES(name),
  description = VALUES(description),
  status = VALUES(status),
  deleted_at = NULL,
  deleted_by = NULL;

-- 2. Desired role-permission matrix. Explicit VALUES for easy visual review.
DROP TEMPORARY TABLE IF EXISTS desired_role_permissions;
CREATE TEMPORARY TABLE desired_role_permissions (
  role_code VARCHAR(50) NOT NULL,
  sub_role VARCHAR(50) NOT NULL,
  permission_code VARCHAR(100) NOT NULL,
  permission_level CHAR(1) NOT NULL,
  PRIMARY KEY (role_code, sub_role, permission_code)
);

INSERT INTO desired_role_permissions
  (role_code, sub_role, permission_code, permission_level)
VALUES
  ('HO', 'NONE', 'UC-01.VIEW_HOMEPAGE', 'R'),
  ('ADMIN', 'NONE', 'UC-01.VIEW_HOMEPAGE', 'R'),
  ('STAFF', 'LEADER', 'UC-01.VIEW_HOMEPAGE', 'R'),
  ('STAFF', 'STAFF', 'UC-01.VIEW_HOMEPAGE', 'R'),
  ('DEPARTMENT', 'LEADER', 'UC-01.VIEW_HOMEPAGE', 'R'),
  ('DEPARTMENT', 'STAFF', 'UC-01.VIEW_HOMEPAGE', 'R'),
  ('STUDENT', 'NONE', 'UC-01.VIEW_HOMEPAGE', 'R'),
  ('VISITOR', 'NONE', 'UC-01.VIEW_HOMEPAGE', 'R'),
  ('HO', 'NONE', 'UC-02.SEARCH_INFORMATION', 'R'),
  ('ADMIN', 'NONE', 'UC-02.SEARCH_INFORMATION', 'R'),
  ('STAFF', 'LEADER', 'UC-02.SEARCH_INFORMATION', 'R'),
  ('STAFF', 'STAFF', 'UC-02.SEARCH_INFORMATION', 'R'),
  ('DEPARTMENT', 'LEADER', 'UC-02.SEARCH_INFORMATION', 'R'),
  ('DEPARTMENT', 'STAFF', 'UC-02.SEARCH_INFORMATION', 'R'),
  ('STUDENT', 'NONE', 'UC-02.SEARCH_INFORMATION', 'R'),
  ('VISITOR', 'NONE', 'UC-02.SEARCH_INFORMATION', 'R'),
  ('HO', 'NONE', 'UC-03.VIEW_CONTACT_INFO', 'R'),
  ('ADMIN', 'NONE', 'UC-03.VIEW_CONTACT_INFO', 'R'),
  ('STAFF', 'LEADER', 'UC-03.VIEW_CONTACT_INFO', 'R'),
  ('STAFF', 'STAFF', 'UC-03.VIEW_CONTACT_INFO', 'R'),
  ('DEPARTMENT', 'LEADER', 'UC-03.VIEW_CONTACT_INFO', 'R'),
  ('DEPARTMENT', 'STAFF', 'UC-03.VIEW_CONTACT_INFO', 'R'),
  ('STUDENT', 'NONE', 'UC-03.VIEW_CONTACT_INFO', 'R'),
  ('VISITOR', 'NONE', 'UC-03.VIEW_CONTACT_INFO', 'R'),
  ('HO', 'NONE', 'UC-04.VIEW_POLICY_AND_TERMS', 'R'),
  ('ADMIN', 'NONE', 'UC-04.VIEW_POLICY_AND_TERMS', 'R'),
  ('STAFF', 'LEADER', 'UC-04.VIEW_POLICY_AND_TERMS', 'R'),
  ('STAFF', 'STAFF', 'UC-04.VIEW_POLICY_AND_TERMS', 'R'),
  ('DEPARTMENT', 'LEADER', 'UC-04.VIEW_POLICY_AND_TERMS', 'R'),
  ('DEPARTMENT', 'STAFF', 'UC-04.VIEW_POLICY_AND_TERMS', 'R'),
  ('STUDENT', 'NONE', 'UC-04.VIEW_POLICY_AND_TERMS', 'R'),
  ('VISITOR', 'NONE', 'UC-04.VIEW_POLICY_AND_TERMS', 'R'),
  ('HO', 'NONE', 'UC-05.VIEW_FAQ', 'R'),
  ('ADMIN', 'NONE', 'UC-05.VIEW_FAQ', 'R'),
  ('STAFF', 'LEADER', 'UC-05.VIEW_FAQ', 'R'),
  ('STAFF', 'STAFF', 'UC-05.VIEW_FAQ', 'R'),
  ('DEPARTMENT', 'LEADER', 'UC-05.VIEW_FAQ', 'R'),
  ('DEPARTMENT', 'STAFF', 'UC-05.VIEW_FAQ', 'R'),
  ('STUDENT', 'NONE', 'UC-05.VIEW_FAQ', 'R'),
  ('VISITOR', 'NONE', 'UC-05.VIEW_FAQ', 'R'),
  ('HO', 'NONE', 'UC-06.VIEW_NEWS', 'R'),
  ('ADMIN', 'NONE', 'UC-06.VIEW_NEWS', 'R'),
  ('STAFF', 'LEADER', 'UC-06.VIEW_NEWS', 'R'),
  ('STAFF', 'STAFF', 'UC-06.VIEW_NEWS', 'R'),
  ('DEPARTMENT', 'LEADER', 'UC-06.VIEW_NEWS', 'R'),
  ('DEPARTMENT', 'STAFF', 'UC-06.VIEW_NEWS', 'R'),
  ('STUDENT', 'NONE', 'UC-06.VIEW_NEWS', 'R'),
  ('VISITOR', 'NONE', 'UC-06.VIEW_NEWS', 'R'),
  ('HO', 'NONE', 'UC-07.VIEW_PARTNERS', 'R'),
  ('ADMIN', 'NONE', 'UC-07.VIEW_PARTNERS', 'R'),
  ('STAFF', 'LEADER', 'UC-07.VIEW_PARTNERS', 'R'),
  ('STAFF', 'STAFF', 'UC-07.VIEW_PARTNERS', 'R'),
  ('DEPARTMENT', 'LEADER', 'UC-07.VIEW_PARTNERS', 'R'),
  ('DEPARTMENT', 'STAFF', 'UC-07.VIEW_PARTNERS', 'R'),
  ('STUDENT', 'NONE', 'UC-07.VIEW_PARTNERS', 'R'),
  ('VISITOR', 'NONE', 'UC-07.VIEW_PARTNERS', 'R'),
  ('HO', 'NONE', 'UC-08.VIEW_GALLERY', 'R'),
  ('ADMIN', 'NONE', 'UC-08.VIEW_GALLERY', 'R'),
  ('STAFF', 'LEADER', 'UC-08.VIEW_GALLERY', 'R'),
  ('STAFF', 'STAFF', 'UC-08.VIEW_GALLERY', 'R'),
  ('DEPARTMENT', 'LEADER', 'UC-08.VIEW_GALLERY', 'R'),
  ('DEPARTMENT', 'STAFF', 'UC-08.VIEW_GALLERY', 'R'),
  ('STUDENT', 'NONE', 'UC-08.VIEW_GALLERY', 'R'),
  ('VISITOR', 'NONE', 'UC-08.VIEW_GALLERY', 'R'),
  ('HO', 'NONE', 'UC-09.VIEW_NOTIFICATIONS', 'R'),
  ('ADMIN', 'NONE', 'UC-09.VIEW_NOTIFICATIONS', 'R'),
  ('STAFF', 'LEADER', 'UC-09.VIEW_NOTIFICATIONS', 'R'),
  ('STAFF', 'STAFF', 'UC-09.VIEW_NOTIFICATIONS', 'R'),
  ('DEPARTMENT', 'LEADER', 'UC-09.VIEW_NOTIFICATIONS', 'R'),
  ('DEPARTMENT', 'STAFF', 'UC-09.VIEW_NOTIFICATIONS', 'R'),
  ('STUDENT', 'NONE', 'UC-09.VIEW_NOTIFICATIONS', 'R'),
  ('VISITOR', 'NONE', 'UC-09.VIEW_NOTIFICATIONS', 'R'),
  ('HO', 'NONE', 'UC-10.LOGIN_VIA_SSO', 'O'),
  ('ADMIN', 'NONE', 'UC-10.LOGIN_VIA_SSO', 'O'),
  ('STAFF', 'LEADER', 'UC-10.LOGIN_VIA_SSO', 'O'),
  ('STAFF', 'STAFF', 'UC-10.LOGIN_VIA_SSO', 'O'),
  ('DEPARTMENT', 'LEADER', 'UC-10.LOGIN_VIA_SSO', 'O'),
  ('DEPARTMENT', 'STAFF', 'UC-10.LOGIN_VIA_SSO', 'O'),
  ('STUDENT', 'NONE', 'UC-10.LOGIN_VIA_SSO', 'O'),
  ('VISITOR', 'NONE', 'UC-10.LOGIN_VIA_SSO', 'O'),
  ('HO', 'NONE', 'UC-11.LOGIN_VIA_CREDENTIALS', 'O'),
  ('ADMIN', 'NONE', 'UC-11.LOGIN_VIA_CREDENTIALS', 'O'),
  ('STAFF', 'LEADER', 'UC-11.LOGIN_VIA_CREDENTIALS', 'O'),
  ('STAFF', 'STAFF', 'UC-11.LOGIN_VIA_CREDENTIALS', 'O'),
  ('DEPARTMENT', 'LEADER', 'UC-11.LOGIN_VIA_CREDENTIALS', 'O'),
  ('DEPARTMENT', 'STAFF', 'UC-11.LOGIN_VIA_CREDENTIALS', 'O'),
  ('STUDENT', 'NONE', 'UC-11.LOGIN_VIA_CREDENTIALS', 'O'),
  ('VISITOR', 'NONE', 'UC-11.LOGIN_VIA_CREDENTIALS', 'O'),
  ('HO', 'NONE', 'UC-12.LOGOUT', 'O'),
  ('ADMIN', 'NONE', 'UC-12.LOGOUT', 'O'),
  ('STAFF', 'LEADER', 'UC-12.LOGOUT', 'O'),
  ('STAFF', 'STAFF', 'UC-12.LOGOUT', 'O'),
  ('DEPARTMENT', 'LEADER', 'UC-12.LOGOUT', 'O'),
  ('DEPARTMENT', 'STAFF', 'UC-12.LOGOUT', 'O'),
  ('STUDENT', 'NONE', 'UC-12.LOGOUT', 'O'),
  ('VISITOR', 'NONE', 'UC-12.LOGOUT', 'O'),
  ('HO', 'NONE', 'UC-13.FORGOT_PASSWORD', 'O'),
  ('ADMIN', 'NONE', 'UC-13.FORGOT_PASSWORD', 'O'),
  ('STAFF', 'LEADER', 'UC-13.FORGOT_PASSWORD', 'O'),
  ('STAFF', 'STAFF', 'UC-13.FORGOT_PASSWORD', 'O'),
  ('DEPARTMENT', 'LEADER', 'UC-13.FORGOT_PASSWORD', 'O'),
  ('DEPARTMENT', 'STAFF', 'UC-13.FORGOT_PASSWORD', 'O'),
  ('STUDENT', 'NONE', 'UC-13.FORGOT_PASSWORD', 'O'),
  ('VISITOR', 'NONE', 'UC-13.FORGOT_PASSWORD', 'O'),
  ('HO', 'NONE', 'UC-14.VIEW_PROFILE', 'O'),
  ('ADMIN', 'NONE', 'UC-14.VIEW_PROFILE', 'O'),
  ('STAFF', 'LEADER', 'UC-14.VIEW_PROFILE', 'O'),
  ('STAFF', 'STAFF', 'UC-14.VIEW_PROFILE', 'O'),
  ('DEPARTMENT', 'LEADER', 'UC-14.VIEW_PROFILE', 'O'),
  ('DEPARTMENT', 'STAFF', 'UC-14.VIEW_PROFILE', 'O'),
  ('STUDENT', 'NONE', 'UC-14.VIEW_PROFILE', 'O'),
  ('VISITOR', 'NONE', 'UC-14.VIEW_PROFILE', 'O'),
  ('HO', 'NONE', 'UC-15.UPDATE_PROFILE', 'O'),
  ('ADMIN', 'NONE', 'UC-15.UPDATE_PROFILE', 'O'),
  ('STAFF', 'LEADER', 'UC-15.UPDATE_PROFILE', 'O'),
  ('STAFF', 'STAFF', 'UC-15.UPDATE_PROFILE', 'O'),
  ('DEPARTMENT', 'LEADER', 'UC-15.UPDATE_PROFILE', 'O'),
  ('DEPARTMENT', 'STAFF', 'UC-15.UPDATE_PROFILE', 'O'),
  ('STUDENT', 'NONE', 'UC-15.UPDATE_PROFILE', 'O'),
  ('VISITOR', 'NONE', 'UC-15.UPDATE_PROFILE', 'O'),
  ('HO', 'NONE', 'UC-16.CHANGE_PASSWORD', 'O'),
  ('ADMIN', 'NONE', 'UC-16.CHANGE_PASSWORD', 'O'),
  ('STAFF', 'LEADER', 'UC-16.CHANGE_PASSWORD', 'O'),
  ('STAFF', 'STAFF', 'UC-16.CHANGE_PASSWORD', 'O'),
  ('DEPARTMENT', 'LEADER', 'UC-16.CHANGE_PASSWORD', 'O'),
  ('DEPARTMENT', 'STAFF', 'UC-16.CHANGE_PASSWORD', 'O'),
  ('STUDENT', 'NONE', 'UC-16.CHANGE_PASSWORD', 'O'),
  ('VISITOR', 'NONE', 'UC-16.CHANGE_PASSWORD', 'O'),
  ('VISITOR', 'NONE', 'UC-17.SUBMIT_VISIT_REQUEST', 'F'),
  ('HO', 'NONE', 'UC-18.APPROVE_CROSS_CAMPUS_REQUEST', 'E'),
  ('HO', 'NONE', 'UC-19.VIEW_GUEST_DELEGATION_DETAILS', 'R'),
  ('STAFF', 'LEADER', 'UC-19.VIEW_GUEST_DELEGATION_DETAILS', 'R'),
  ('STAFF', 'STAFF', 'UC-19.VIEW_GUEST_DELEGATION_DETAILS', 'R'),
  ('DEPARTMENT', 'LEADER', 'UC-19.VIEW_GUEST_DELEGATION_DETAILS', 'R'),
  ('DEPARTMENT', 'STAFF', 'UC-19.VIEW_GUEST_DELEGATION_DETAILS', 'R'),
  ('STUDENT', 'NONE', 'UC-19.VIEW_GUEST_DELEGATION_DETAILS', 'R'),
  ('VISITOR', 'NONE', 'UC-19.VIEW_GUEST_DELEGATION_DETAILS', 'R'),
  ('HO', 'NONE', 'UC-20.VIEW_GUEST_DELEGATION_LIST', 'R'),
  ('STAFF', 'LEADER', 'UC-20.VIEW_GUEST_DELEGATION_LIST', 'R'),
  ('STAFF', 'STAFF', 'UC-20.VIEW_GUEST_DELEGATION_LIST', 'R'),
  ('DEPARTMENT', 'LEADER', 'UC-20.VIEW_GUEST_DELEGATION_LIST', 'R'),
  ('DEPARTMENT', 'STAFF', 'UC-20.VIEW_GUEST_DELEGATION_LIST', 'R'),
  ('STUDENT', 'NONE', 'UC-20.VIEW_GUEST_DELEGATION_LIST', 'R'),
  ('VISITOR', 'NONE', 'UC-20.VIEW_GUEST_DELEGATION_LIST', 'R'),
  ('HO', 'NONE', 'UC-21.SEARCH_DELEGATIONS', 'R'),
  ('STAFF', 'LEADER', 'UC-21.SEARCH_DELEGATIONS', 'R'),
  ('STAFF', 'STAFF', 'UC-21.SEARCH_DELEGATIONS', 'R'),
  ('DEPARTMENT', 'LEADER', 'UC-21.SEARCH_DELEGATIONS', 'R'),
  ('DEPARTMENT', 'STAFF', 'UC-21.SEARCH_DELEGATIONS', 'R'),
  ('STUDENT', 'NONE', 'UC-21.SEARCH_DELEGATIONS', 'R'),
  ('VISITOR', 'NONE', 'UC-21.SEARCH_DELEGATIONS', 'R'),
  ('STAFF', 'LEADER', 'UC-22.PROCESS_VISIT_REQUEST', 'E'),
  ('STAFF', 'STAFF', 'UC-23.CREATE_GUEST_DELEGATION', 'F'),
  ('STAFF', 'STAFF', 'UC-24.UPDATE_GUEST_DELEGATION', 'F'),
  ('STAFF', 'LEADER', 'UC-25.PREPARE_VISIT_LOGISTICS', 'R'),
  ('STAFF', 'STAFF', 'UC-25.PREPARE_VISIT_LOGISTICS', 'F'),
  ('STAFF', 'LEADER', 'UC-26.UPDATE_VISIT_LOGISTICS', 'R'),
  ('STAFF', 'STAFF', 'UC-26.UPDATE_VISIT_LOGISTICS', 'F'),
  ('STAFF', 'STAFF', 'UC-27.CONFIRM_PARTICIPATION', 'E'),
  ('DEPARTMENT', 'LEADER', 'UC-27.CONFIRM_PARTICIPATION', 'E'),
  ('DEPARTMENT', 'STAFF', 'UC-27.CONFIRM_PARTICIPATION', 'E'),
  ('STUDENT', 'NONE', 'UC-27.CONFIRM_PARTICIPATION', 'E'),
  ('DEPARTMENT', 'LEADER', 'UC-28.APPROVE_RESOURCE_REQUEST', 'F'),
  ('DEPARTMENT', 'LEADER', 'UC-29.PROPOSE_RESOURCE_MODIFICATION', 'F'),
  ('DEPARTMENT', 'STAFF', 'UC-29.PROPOSE_RESOURCE_MODIFICATION', 'F'),
  ('STAFF', 'STAFF', 'UC-30.CONFIRM_THE_CHANGE_PROPOSAL', 'E'),
  ('DEPARTMENT', 'LEADER', 'UC-30.CONFIRM_THE_CHANGE_PROPOSAL', 'R'),
  ('DEPARTMENT', 'STAFF', 'UC-30.CONFIRM_THE_CHANGE_PROPOSAL', 'R'),
  ('STAFF', 'STAFF', 'UC-31.CREATE_MEETING_MINUTES', 'F'),
  ('DEPARTMENT', 'LEADER', 'UC-31.CREATE_MEETING_MINUTES', 'F'),
  ('DEPARTMENT', 'STAFF', 'UC-31.CREATE_MEETING_MINUTES', 'F'),
  ('STUDENT', 'NONE', 'UC-31.CREATE_MEETING_MINUTES', 'F'),
  ('STAFF', 'STAFF', 'UC-32.EDIT_MEETING_MINUTES', 'F'),
  ('DEPARTMENT', 'LEADER', 'UC-32.EDIT_MEETING_MINUTES', 'F'),
  ('DEPARTMENT', 'STAFF', 'UC-32.EDIT_MEETING_MINUTES', 'F'),
  ('STUDENT', 'NONE', 'UC-32.EDIT_MEETING_MINUTES', 'F'),
  ('HO', 'NONE', 'UC-33.VIEW_MEETING_MINUTES_DETAILS', 'R'),
  ('STAFF', 'LEADER', 'UC-33.VIEW_MEETING_MINUTES_DETAILS', 'R'),
  ('STAFF', 'STAFF', 'UC-33.VIEW_MEETING_MINUTES_DETAILS', 'R'),
  ('DEPARTMENT', 'LEADER', 'UC-33.VIEW_MEETING_MINUTES_DETAILS', 'R'),
  ('DEPARTMENT', 'STAFF', 'UC-33.VIEW_MEETING_MINUTES_DETAILS', 'R'),
  ('STUDENT', 'NONE', 'UC-33.VIEW_MEETING_MINUTES_DETAILS', 'R'),
  ('STAFF', 'STAFF', 'UC-34.SUBMIT_DELEGATION_FEEDBACK', 'F'),
  ('DEPARTMENT', 'LEADER', 'UC-34.SUBMIT_DELEGATION_FEEDBACK', 'F'),
  ('DEPARTMENT', 'STAFF', 'UC-34.SUBMIT_DELEGATION_FEEDBACK', 'F'),
  ('STUDENT', 'NONE', 'UC-34.SUBMIT_DELEGATION_FEEDBACK', 'F'),
  ('STAFF', 'STAFF', 'UC-35.SCAN_BUSINESS_CARD', 'F'),
  ('STAFF', 'STAFF', 'UC-36.CREATE_PARTNER_PROFILE', 'F'),
  ('STAFF', 'STAFF', 'UC-37.UPLOAD_ATTACHED_DOCUMENTS', 'F'),
  ('STAFF', 'STAFF', 'UC-38.UPLOAD_VISIT_PHOTOS', 'F'),
  ('STUDENT', 'NONE', 'UC-38.UPLOAD_VISIT_PHOTOS', 'F'),
  ('STAFF', 'STAFF', 'UC-39.TAG_FACES_ON_PHOTOS', 'F'),
  ('STAFF', 'STAFF', 'UC-40.CREATE_NEWS_ARTICLE', 'F'),
  ('STUDENT', 'NONE', 'UC-40.CREATE_NEWS_ARTICLE', 'F'),
  ('STAFF', 'STAFF', 'UC-41.CLOSE_DELEGATION', 'F'),
  ('HO', 'NONE', 'UC-42.VIEW_EMAIL_TEMPLATE_LIST', 'R'),
  ('HO', 'NONE', 'UC-43.VIEW_EMAIL_TEMPLATE_DETAIL', 'R'),
  ('HO', 'NONE', 'UC-44.UPDATE_EMAIL_TEMPLATE', 'E'),
  ('HO', 'NONE', 'UC-45.CREATE_EMAIL_TEMPLATE', 'F'),
  ('HO', 'NONE', 'UC-46.EDIT_EMAIL_CONTENT', 'O'),
  ('STAFF', 'LEADER', 'UC-46.EDIT_EMAIL_CONTENT', 'O'),
  ('STAFF', 'STAFF', 'UC-46.EDIT_EMAIL_CONTENT', 'O'),
  ('DEPARTMENT', 'LEADER', 'UC-46.EDIT_EMAIL_CONTENT', 'O'),
  ('DEPARTMENT', 'STAFF', 'UC-46.EDIT_EMAIL_CONTENT', 'O'),
  ('STUDENT', 'NONE', 'UC-46.EDIT_EMAIL_CONTENT', 'O'),
  ('VISITOR', 'NONE', 'UC-46.EDIT_EMAIL_CONTENT', 'O'),
  ('HO', 'NONE', 'UC-47.SEND_EMAIL', 'O'),
  ('STAFF', 'LEADER', 'UC-47.SEND_EMAIL', 'O'),
  ('STAFF', 'STAFF', 'UC-47.SEND_EMAIL', 'O'),
  ('DEPARTMENT', 'LEADER', 'UC-47.SEND_EMAIL', 'O'),
  ('DEPARTMENT', 'STAFF', 'UC-47.SEND_EMAIL', 'O'),
  ('STUDENT', 'NONE', 'UC-47.SEND_EMAIL', 'O'),
  ('VISITOR', 'NONE', 'UC-47.SEND_EMAIL', 'O'),
  ('HO', 'NONE', 'UC-48.VIEW_EMAIL', 'O'),
  ('STAFF', 'LEADER', 'UC-48.VIEW_EMAIL', 'O'),
  ('STAFF', 'STAFF', 'UC-48.VIEW_EMAIL', 'O'),
  ('DEPARTMENT', 'LEADER', 'UC-48.VIEW_EMAIL', 'O'),
  ('DEPARTMENT', 'STAFF', 'UC-48.VIEW_EMAIL', 'O'),
  ('STUDENT', 'NONE', 'UC-48.VIEW_EMAIL', 'O'),
  ('VISITOR', 'NONE', 'UC-48.VIEW_EMAIL', 'O'),
  ('HO', 'NONE', 'UC-49.REPLY_TO_EMAIL', 'O'),
  ('STAFF', 'LEADER', 'UC-49.REPLY_TO_EMAIL', 'O'),
  ('STAFF', 'STAFF', 'UC-49.REPLY_TO_EMAIL', 'O'),
  ('DEPARTMENT', 'LEADER', 'UC-49.REPLY_TO_EMAIL', 'O'),
  ('DEPARTMENT', 'STAFF', 'UC-49.REPLY_TO_EMAIL', 'O'),
  ('STUDENT', 'NONE', 'UC-49.REPLY_TO_EMAIL', 'O'),
  ('VISITOR', 'NONE', 'UC-49.REPLY_TO_EMAIL', 'O'),
  ('STAFF', 'LEADER', 'UC-50.PROCESS_PARTNER_CREATION_REQUEST', 'E'),
  ('STAFF', 'STAFF', 'UC-51.EDIT_PARTNER_INFORMATION', 'E'),
  ('STAFF', 'LEADER', 'UC-52.VIEW_PARTNER_LISTS', 'R'),
  ('STAFF', 'STAFF', 'UC-52.VIEW_PARTNER_LISTS', 'R'),
  ('STAFF', 'LEADER', 'UC-53.SEARCH_PARTNERS', 'R'),
  ('STAFF', 'STAFF', 'UC-53.SEARCH_PARTNERS', 'R'),
  ('STAFF', 'LEADER', 'UC-54.VIEW_PARTNER_DETAILS', 'R'),
  ('STAFF', 'STAFF', 'UC-54.VIEW_PARTNER_DETAILS', 'R'),
  ('STAFF', 'LEADER', 'UC-55.VIEW_DOCUMENT_LIST', 'R'),
  ('STAFF', 'STAFF', 'UC-55.VIEW_DOCUMENT_LIST', 'R'),
  ('STAFF', 'LEADER', 'UC-56.SEARCH_DOCUMENTS', 'R'),
  ('STAFF', 'STAFF', 'UC-56.SEARCH_DOCUMENTS', 'R'),
  ('STAFF', 'LEADER', 'UC-57.VIEW_GALLERY_ITEM_LIST', 'R'),
  ('STAFF', 'LEADER', 'UC-58.SEARCH_GALLERY_ITEMS', 'R'),
  ('STAFF', 'LEADER', 'UC-59.ADD_GALLERY_ITEM', 'F'),
  ('STAFF', 'LEADER', 'UC-60.UPDATE_GALLERY_ITEM', 'E'),
  ('STAFF', 'LEADER', 'UC-61.DELETE_GALLERY_ITEM', 'F'),
  ('STAFF', 'LEADER', 'UC-62.VIEW_MINUTES_LIST', 'R'),
  ('STAFF', 'STAFF', 'UC-62.VIEW_MINUTES_LIST', 'R'),
  ('STAFF', 'LEADER', 'UC-63.SEARCH_FILTER_MINUTES', 'R'),
  ('STAFF', 'STAFF', 'UC-63.SEARCH_FILTER_MINUTES', 'R'),
  ('HO', 'NONE', 'UC-64.VIEW_LIST_FAQ', 'R'),
  ('HO', 'NONE', 'UC-65.CREATE_FAQ', 'F'),
  ('HO', 'NONE', 'UC-66.UPDATE_FAQ', 'E'),
  ('HO', 'NONE', 'UC-67.CHANGE_FAQ_VISIBILITY', 'E'),
  ('HO', 'NONE', 'UC-68.SEARCH_FAQ', 'R'),
  ('HO', 'NONE', 'UC-69.VIEW_DASHBOARD_STATISTICS', 'R'),
  ('STAFF', 'LEADER', 'UC-69.VIEW_DASHBOARD_STATISTICS', 'R'),
  ('DEPARTMENT', 'LEADER', 'UC-69.VIEW_DASHBOARD_STATISTICS', 'R'),
  ('HO', 'NONE', 'UC-70.EXPORT_STATISTICS_REPORT', 'E'),
  ('STAFF', 'LEADER', 'UC-70.EXPORT_STATISTICS_REPORT', 'E'),
  ('DEPARTMENT', 'LEADER', 'UC-70.EXPORT_STATISTICS_REPORT', 'E'),
  ('HO', 'NONE', 'UC-71.FILTER_DASHBOARD_BY_TIME', 'R'),
  ('STAFF', 'LEADER', 'UC-71.FILTER_DASHBOARD_BY_TIME', 'R'),
  ('DEPARTMENT', 'LEADER', 'UC-71.FILTER_DASHBOARD_BY_TIME', 'R'),
  ('STAFF', 'LEADER', 'UC-72.VIEW_MY_EVENTS', 'O'),
  ('STAFF', 'STAFF', 'UC-72.VIEW_MY_EVENTS', 'O'),
  ('DEPARTMENT', 'LEADER', 'UC-72.VIEW_MY_EVENTS', 'O'),
  ('DEPARTMENT', 'STAFF', 'UC-72.VIEW_MY_EVENTS', 'O'),
  ('STUDENT', 'NONE', 'UC-72.VIEW_MY_EVENTS', 'O'),
  ('STAFF', 'LEADER', 'UC-73.VIEW_DEPARTMENT_CALENDAR', 'R'),
  ('STAFF', 'STAFF', 'UC-73.VIEW_DEPARTMENT_CALENDAR', 'R'),
  ('STAFF', 'LEADER', 'UC-74.SWITCH_VIEW_MODE', 'R'),
  ('STAFF', 'STAFF', 'UC-74.SWITCH_VIEW_MODE', 'R'),
  ('DEPARTMENT', 'LEADER', 'UC-74.SWITCH_VIEW_MODE', 'R'),
  ('DEPARTMENT', 'STAFF', 'UC-74.SWITCH_VIEW_MODE', 'R'),
  ('STUDENT', 'NONE', 'UC-74.SWITCH_VIEW_MODE', 'R'),
  ('STAFF', 'LEADER', 'UC-75.ADD_PERSONAL_EVENT', 'O'),
  ('STAFF', 'STAFF', 'UC-75.ADD_PERSONAL_EVENT', 'O'),
  ('STAFF', 'LEADER', 'UC-76.DELETE_PERSONAL_EVENT', 'O'),
  ('STAFF', 'STAFF', 'UC-76.DELETE_PERSONAL_EVENT', 'O'),
  ('STAFF', 'LEADER', 'UC-77.UPDATE_PERSONAL_EVENT', 'O'),
  ('STAFF', 'STAFF', 'UC-77.UPDATE_PERSONAL_EVENT', 'O'),
  ('STAFF', 'LEADER', 'UC-78.VIEW_EVENT_DETAILS', 'R'),
  ('STAFF', 'STAFF', 'UC-78.VIEW_EVENT_DETAILS', 'R'),
  ('DEPARTMENT', 'LEADER', 'UC-78.VIEW_EVENT_DETAILS', 'R'),
  ('DEPARTMENT', 'STAFF', 'UC-78.VIEW_EVENT_DETAILS', 'R'),
  ('STUDENT', 'NONE', 'UC-78.VIEW_EVENT_DETAILS', 'R'),
  ('STAFF', 'LEADER', 'UC-79.SEARCH_FILTER_FEEDBACK', 'R'),
  ('STAFF', 'STAFF', 'UC-79.SEARCH_FILTER_FEEDBACK', 'R'),
  ('STAFF', 'LEADER', 'UC-80.VIEW_FEEDBACK_SUMMARY', 'R'),
  ('STAFF', 'STAFF', 'UC-80.VIEW_FEEDBACK_SUMMARY', 'R'),
  ('HO', 'NONE', 'UC-81.ADD_NEW_CAMPUS', 'F'),
  ('HO', 'NONE', 'UC-82.VIEW_CAMPUS_LIST', 'R'),
  ('HO', 'NONE', 'UC-83.SEARCH_AND_FILTER_CAMPUS', 'R'),
  ('HO', 'NONE', 'UC-84.VIEW_CAMPUS_DETAILS', 'R'),
  ('HO', 'NONE', 'UC-85.UPDATE_CAMPUS', 'E'),
  ('HO', 'NONE', 'UC-86.MANAGE_CAMPUS_STATUS', 'E'),
  ('HO', 'NONE', 'UC-87.ASSIGN_CAMPUS_LEAD', 'F'),
  ('STAFF', 'LEADER', 'UC-88.APPROVE_NEWS', 'E'),
  ('STAFF', 'STAFF', 'UC-89.PUBLISH_NEWS', 'F'),
  ('STAFF', 'LEADER', 'UC-90.VIEW_NEWS_LIST', 'R'),
  ('STAFF', 'STAFF', 'UC-90.VIEW_NEWS_LIST', 'R'),
  ('STUDENT', 'NONE', 'UC-90.VIEW_NEWS_LIST', 'R'),
  ('STAFF', 'LEADER', 'UC-91.VIEW_NEWS_DETAILS', 'R'),
  ('STAFF', 'STAFF', 'UC-91.VIEW_NEWS_DETAILS', 'R'),
  ('STUDENT', 'NONE', 'UC-91.VIEW_NEWS_DETAILS', 'R'),
  ('STAFF', 'STAFF', 'UC-92.ADD_MULTILINGUAL_NEWS', 'F'),
  ('STUDENT', 'NONE', 'UC-92.ADD_MULTILINGUAL_NEWS', 'F'),
  ('STAFF', 'LEADER', 'UC-93.MANAGE_NEWS_VISIBILITY', 'E'),
  ('STAFF', 'STAFF', 'UC-94.EDIT_NEWS', 'E'),
  ('STUDENT', 'NONE', 'UC-94.EDIT_NEWS', 'E'),
  ('HO', 'NONE', 'UC-95.VIEW_ACCOUNT_LIST', 'R'),
  ('STAFF', 'LEADER', 'UC-95.VIEW_ACCOUNT_LIST', 'R'),
  ('HO', 'NONE', 'UC-96.CREATE_ACCOUNT', 'F'),
  ('STAFF', 'LEADER', 'UC-96.CREATE_ACCOUNT', 'F'),
  ('HO', 'NONE', 'UC-97.MANAGE_ACCOUNT_STATUS', 'E'),
  ('STAFF', 'LEADER', 'UC-97.MANAGE_ACCOUNT_STATUS', 'E'),
  ('HO', 'NONE', 'UC-98.VIEW_ACCOUNT_DETAILS', 'R'),
  ('STAFF', 'LEADER', 'UC-98.VIEW_ACCOUNT_DETAILS', 'R'),
  ('HO', 'NONE', 'UC-99.SEARCH_AND_FILTER_ACCOUNTS', 'R'),
  ('STAFF', 'LEADER', 'UC-99.SEARCH_AND_FILTER_ACCOUNTS', 'R'),
  ('STAFF', 'LEADER', 'UC-100.UPDATE_ACCOUNT_ROLE', 'E'),
  ('STAFF', 'LEADER', 'UC-101.ADD_NEW_DEPARTMENT', 'F'),
  ('STAFF', 'LEADER', 'UC-102.UPDATE_DEPARTMENT', 'F'),
  ('STAFF', 'LEADER', 'UC-103.SEARCH_AND_FILTER_DEPARTMENTS', 'R'),
  ('STAFF', 'LEADER', 'UC-104.VIEW_DEPARTMENT_LIST', 'R'),
  ('STAFF', 'LEADER', 'UC-105.VIEW_DEPARTMENT_DETAILS', 'R'),
  ('DEPARTMENT', 'LEADER', 'UC-105.VIEW_DEPARTMENT_DETAILS', 'R'),
  ('DEPARTMENT', 'STAFF', 'UC-105.VIEW_DEPARTMENT_DETAILS', 'R'),
  ('STAFF', 'LEADER', 'UC-106.MANAGE_DEPARTMENT_STATUS', 'E'),
  ('DEPARTMENT', 'LEADER', 'UC-107.ADD_DEPARTMENT_PERSONNEL', 'F'),
  ('DEPARTMENT', 'LEADER', 'UC-108.VIEW_PERSONNEL_DETAILS', 'R'),
  ('DEPARTMENT', 'STAFF', 'UC-108.VIEW_PERSONNEL_DETAILS', 'R'),
  ('DEPARTMENT', 'LEADER', 'UC-109.SEARCH_PERSONNEL', 'R'),
  ('DEPARTMENT', 'STAFF', 'UC-109.SEARCH_PERSONNEL', 'R'),
  ('DEPARTMENT', 'STAFF', 'UC-110.REVIEW_ASSIGNED_TASKS', 'E'),
  ('DEPARTMENT', 'LEADER', 'UC-111.ASSIGN_TASKS', 'F'),
  ('STAFF', 'STAFF', 'UC-112.SIGN_THE_SERVICE_DELIVERY_REPORT', 'E'),
  ('DEPARTMENT', 'LEADER', 'UC-112.SIGN_THE_SERVICE_DELIVERY_REPORT', 'E'),
  ('DEPARTMENT', 'STAFF', 'UC-112.SIGN_THE_SERVICE_DELIVERY_REPORT', 'E'),
  ('DEPARTMENT', 'LEADER', 'UC-113.REMOVE_PERSONNEL', 'F'),
  ('DEPARTMENT', 'LEADER', 'UC-114.VIEW_COORDINATION_TASKS', 'R'),
  ('DEPARTMENT', 'STAFF', 'UC-114.VIEW_COORDINATION_TASKS', 'R'),
  ('DEPARTMENT', 'LEADER', 'UC-115.SEARCH_COORDINATION_TASKS', 'R'),
  ('DEPARTMENT', 'STAFF', 'UC-115.SEARCH_COORDINATION_TASKS', 'R'),
  ('DEPARTMENT', 'LEADER', 'UC-116.REASSIGN_DEPARTMENT_LEAD', 'F'),
  ('ADMIN', 'NONE', 'UC-117.VIEW_ROLE_LIST', 'R'),
  ('ADMIN', 'NONE', 'UC-118.CREATE_NEW_ROLE', 'F'),
  ('ADMIN', 'NONE', 'UC-119.CONFIGURE_ROLE_PERMISSIONS', 'F'),
  ('ADMIN', 'NONE', 'UC-120.UPDATE_ROLE_DETAILS', 'F'),
  ('ADMIN', 'NONE', 'UC-121.DISABLE_DELETE_ROLE', 'F'),
  ('ADMIN', 'NONE', 'UC-122.VIEW_API_CONFIGURATION', 'R'),
  ('ADMIN', 'NONE', 'UC-123.CREATE_API_CONFIGURATION', 'F'),
  ('ADMIN', 'NONE', 'UC-124.UPDATE_API_CONFIGURATION', 'E'),
  ('ADMIN', 'NONE', 'UC-125.DELETE_API_CONFIGURATION', 'E'),
  ('ADMIN', 'NONE', 'UC-126.TEST_API_CONNECTION', 'F'),
  ('ADMIN', 'NONE', 'UC-127.MANAGE_API_STATUS', 'F'),
  ('ADMIN', 'NONE', 'UC-128.CONFIGURE_REQUEST_LIMIT', 'F'),
  ('ADMIN', 'NONE', 'UC-129.VIEW_API_LOGS', 'R'),
  ('ADMIN', 'NONE', 'UC-130.SEARCH_API_LOGS', 'R'),
  ('HO', 'NONE', 'UC-131.CREATE_AGENDA_TEMPLATE', 'F'),
  ('HO', 'NONE', 'UC-132.UPDATE_AGENDA_TEMPLATE', 'E'),
  ('HO', 'NONE', 'UC-133.DELETE_AGENDA_TEMPLATE', 'F'),
  ('HO', 'NONE', 'UC-134.VIEW_AGENDA_TEMPLATE_LIST', 'R'),
  ('HO', 'NONE', 'UC-135.VIEW_AGENDA_TEMPLATE_DETAIL', 'R');



-- v8.2: desired grants for UC-136 Cancel Visit Request.
INSERT INTO desired_role_permissions
  (role_code, sub_role, permission_code, permission_level)
VALUES
  ('VISITOR', 'NONE', 'UC-136.CANCEL_VISIT_REQUEST', 'O'),
  ('STAFF', 'LEADER', 'UC-136.CANCEL_VISIT_REQUEST', 'E'),
  ('STAFF', 'STAFF',  'UC-136.CANCEL_VISIT_REQUEST', 'O'),
  ('HO',    'NONE',   'UC-136.CANCEL_VISIT_REQUEST', 'E')
ON DUPLICATE KEY UPDATE
  permission_level = VALUES(permission_level);

-- 3. Preview desired matrix before applying.
SELECT
  'PREVIEW_DESIRED_MATRIX' AS check_name,
  d.role_code,
  d.sub_role,
  d.permission_code,
  d.permission_level
FROM desired_role_permissions d
ORDER BY d.role_code, d.sub_role, d.permission_code;

SELECT 'TOTAL_DESIRED_GRANTS' AS check_name, COUNT(*) AS total_rows
FROM desired_role_permissions;

-- 4. Missing role check. Must return zero rows.
SELECT d.*, 'MISSING_ROLE' AS status
FROM desired_role_permissions d
LEFT JOIN roles r
  ON r.role_code = d.role_code
 AND r.deleted_at IS NULL
WHERE r.role_id IS NULL;

-- 5. Missing permission check. Must return zero rows.
-- Note: permissions table in current schema does NOT have deleted_at.
SELECT d.*, 'MISSING_PERMISSION' AS status
FROM desired_role_permissions d
LEFT JOIN permissions p
  ON p.permission_code = d.permission_code
WHERE p.permission_id IS NULL;

-- 6. Upsert matrix into role_permissions.
INSERT INTO role_permissions
  (role_id, sub_role, permission_id, permission_level, granted_at)
SELECT
  r.role_id,
  d.sub_role,
  p.permission_id,
  d.permission_level,
  NOW()
FROM desired_role_permissions d
JOIN roles r
  ON r.role_code = d.role_code
 AND r.deleted_at IS NULL
JOIN permissions p
  ON p.permission_code = d.permission_code
ON DUPLICATE KEY UPDATE
  permission_level = VALUES(permission_level),
  granted_at = VALUES(granted_at);

-- 7. Preview excess permissions. Review this before running cleanup.
SELECT
  r.role_code,
  rp.sub_role,
  p.permission_code,
  rp.permission_level AS current_db_level,
  'WILL_BE_DELETED_IF_CLEANUP_RUNS' AS action
FROM role_permissions rp
JOIN roles r ON r.role_id = rp.role_id
JOIN permissions p ON p.permission_id = rp.permission_id
LEFT JOIN desired_role_permissions d
  ON d.role_code = r.role_code
 AND d.sub_role = rp.sub_role
 AND d.permission_code = p.permission_code
WHERE d.permission_code IS NULL
  AND r.role_code IN ('ADMIN', 'HO', 'STAFF', 'DEPARTMENT', 'STUDENT', 'VISITOR')
  AND p.permission_code REGEXP '^UC-[0-9]+\.'
ORDER BY r.role_code, rp.sub_role, p.permission_code;

-- 8. Optional cleanup. Keep commented by default for safety.
/*
DELETE rp
FROM role_permissions rp
JOIN roles r ON r.role_id = rp.role_id
JOIN permissions p ON p.permission_id = rp.permission_id
LEFT JOIN desired_role_permissions d
  ON d.role_code = r.role_code
 AND d.sub_role = rp.sub_role
 AND d.permission_code = p.permission_code
WHERE d.permission_code IS NULL
  AND r.role_code IN ('ADMIN', 'HO', 'STAFF', 'DEPARTMENT', 'STUDENT', 'VISITOR')
  AND p.permission_code REGEXP '^UC-[0-9]+\.';
*/

-- 9. Final verification.
SELECT
  r.role_code,
  rp.sub_role,
  COUNT(*) AS total_permissions
FROM role_permissions rp
JOIN roles r ON r.role_id = rp.role_id
GROUP BY r.role_code, rp.sub_role
ORDER BY FIELD(r.role_code, 'ADMIN','HO','STAFF','DEPARTMENT','STUDENT','VISITOR'), rp.sub_role;

COMMIT;


-- <<< END permission_matrix.sql




-- <<< VISIT VISIBILITY RULES / DERIVED DISPLAY STATUS >>>
-- =====================================================================
-- PEMS Visit Visibility Patch
-- Rule:
-- - MULTI_CAMPUS pending requests are visible only to HO.
-- - Staff Leader cannot see MULTI_CAMPUS requests before HO approval.
-- - After HO approves, only Staff Leader of campuses included in that request can see their campus instance.
-- - SINGLE_CAMPUS requests are visible/actionable only to Staff Leader of that campus.
-- - "HO_APPROVED" is a display/approval badge, not a lifecycle status.
-- =====================================================================

-- Extra indexes for visibility queries. Keep existing indexes; these optimize the new access rules.
ALTER TABLE visit_requests
  ADD KEY idx_visit_requests_visibility_scope_status_decision
    (visit_scope, status, decision_actor_role, decided_at);

ALTER TABLE visit_request_campuses
  ADD KEY idx_visit_instances_visibility_campus_request
    (campus_id, visit_request_id, status, current_host_user_id);

-- HO view: HO can see all inter-campus/multi-campus requests, including PENDING_APPROVAL.
CREATE OR REPLACE VIEW vw_visit_requests_for_ho AS
SELECT
  vr.visit_request_id,
  vr.request_code,
  vr.delegation_name,
  vr.visit_scope,
  vr.status AS request_status,
  vr.submitted_at,
  vr.decided_by,
  vr.decided_at,
  vr.decision_actor_role,
  vr.decision_note,
  CASE
    WHEN vr.visit_scope = 'MULTI_CAMPUS' AND vr.status = 'PENDING_APPROVAL'
      THEN 'WAITING_HO_APPROVAL'
    WHEN vr.visit_scope = 'MULTI_CAMPUS'
      AND vr.status = 'APPROVED'
      AND vr.decision_actor_role = 'HO'
      THEN 'HO_APPROVED'
    WHEN vr.status = 'REJECTED'
      THEN 'REJECTED'
    WHEN vr.status = 'CANCELLED'
      THEN 'CANCELLED'
    ELSE vr.status
  END AS approval_display_status
FROM visit_requests vr
WHERE vr.visit_scope = 'MULTI_CAMPUS';

-- Staff Leader view: backend must still filter visible_campus_id = current user's primary_campus_id.
-- This view deliberately excludes MULTI_CAMPUS requests before HO approval.
CREATE OR REPLACE VIEW vw_visit_requests_for_staff_leader AS
SELECT
  vr.visit_request_id,
  vrc.visit_instance_id,
  vrc.campus_id AS visible_campus_id,
  vrc.current_host_user_id,
  vrc.host_assigned_by,
  vrc.host_assigned_at,
  vrc.host_assignment_source,
  vrc.host_transferred_by,
  vrc.host_transferred_at,
  vr.request_code,
  vr.delegation_name,
  vr.visit_scope,
  vr.status AS request_status,
  vrc.status AS campus_status,
  vr.submitted_at,
  vr.decided_by,
  vr.decided_at,
  vr.decision_actor_role,
  vr.decision_note,
  CASE
    WHEN vr.visit_scope = 'SINGLE_CAMPUS' AND vr.status = 'PENDING_APPROVAL'
      THEN 'WAITING_STAFF_LEADER_APPROVAL'
    WHEN vr.visit_scope = 'SINGLE_CAMPUS'
      AND vr.status = 'APPROVED'
      AND vr.decision_actor_role = 'STAFF_LEADER'
      THEN 'STAFF_LEADER_APPROVED'
    WHEN vr.visit_scope = 'MULTI_CAMPUS'
      AND vr.status = 'APPROVED'
      AND vr.decision_actor_role = 'HO'
      THEN 'HO_APPROVED'
    WHEN vr.status = 'REJECTED'
      THEN 'REJECTED'
    WHEN vr.status = 'CANCELLED'
      THEN 'CANCELLED'
    ELSE vr.status
  END AS approval_display_status,
  CASE
    WHEN vr.visit_scope = 'SINGLE_CAMPUS' AND vr.status = 'PENDING_APPROVAL'
      THEN 1
    ELSE 0
  END AS can_staff_leader_decide,
  CASE
    WHEN vr.visit_scope = 'MULTI_CAMPUS'
      AND vr.status = 'APPROVED'
      AND vr.decision_actor_role = 'HO'
      THEN 1
    ELSE 0
  END AS is_released_by_ho
FROM visit_requests vr
JOIN visit_request_campuses vrc
  ON vrc.visit_request_id = vr.visit_request_id
WHERE
  -- Single-campus: Staff Leader of that campus may see pending and later states.
  vr.visit_scope = 'SINGLE_CAMPUS'
  OR
  -- Multi-campus: Staff Leader may see only after HO has approved/released it.
  (
    vr.visit_scope = 'MULTI_CAMPUS'
    AND vr.status = 'APPROVED'
    AND vr.decision_actor_role = 'HO'
    AND vr.decided_at IS NOT NULL
  );

-- <<< END VISIT VISIBILITY RULES >>>


-- =====================================================================
-- FINAL STANDARD VERIFICATION AFTER MANUAL SEED SYNCHRONIZATION
-- =====================================================================

SELECT 'FINAL_MANUAL_SEED_STANDARDIZATION_COMPLETED' AS message, NOW() AS verified_at;

SELECT
  r.role_code,
  rp.sub_role,
  COUNT(*) AS total_permissions
FROM role_permissions rp
JOIN roles r ON r.role_id = rp.role_id
WHERE r.role_code IN ('ADMIN','HO','STAFF','DEPARTMENT','STUDENT','VISITOR')
GROUP BY r.role_code, rp.sub_role
ORDER BY FIELD(r.role_code, 'ADMIN','HO','STAFF','DEPARTMENT','STUDENT','VISITOR'), rp.sub_role;

SELECT
  u.email,
  u.full_name,
  r.role_code,
  u.sub_role AS users_sub_role,
  u.status
FROM users u
JOIN roles r ON r.role_id = u.role_id
WHERE u.email IN (
  'admin@fpt.edu.vn',
  'ho@fpt.edu.vn',
  'staff.leader.hn@fpt.edu.vn',
  'staff.hn@fpt.edu.vn',
  'dept.leader.hn@fpt.edu.vn',
  'dept.hn@fpt.edu.vn',
  'student@fpt.edu.vn',
  'visitor@example.com'
)
ORDER BY FIELD(u.email,
  'admin@fpt.edu.vn',
  'ho@fpt.edu.vn',
  'staff.leader.hn@fpt.edu.vn',
  'staff.hn@fpt.edu.vn',
  'dept.leader.hn@fpt.edu.vn',
  'dept.hn@fpt.edu.vn',
  'student@fpt.edu.vn',
  'visitor@example.com'
);

-- =====================================================================
-- FINAL STRICT BUSINESS VISIBILITY GUARDS
-- =====================================================================
-- These guards intentionally live at the end of the file so they win over
-- older scenario/manual seed blocks above.
--
-- Final visit/delegation access rule:
--   ADMIN: no visit request/delegation business access.
--   HO: only MULTI_CAMPUS requests.
--   STAFF Leader: own campus SINGLE_CAMPUS requests; and own-campus instances
--                 of MULTI_CAMPUS requests only after HO approval.
-- =====================================================================

-- 1) Hard-clean accidental ADMIN permissions for visit/delegation business UCs.
-- ADMIN remains a technical/system administrator, not a visit workflow actor.
DELETE rp
FROM role_permissions rp
JOIN roles r
  ON r.role_id = rp.role_id
JOIN permissions p
  ON p.permission_id = rp.permission_id
WHERE r.role_code = 'ADMIN'
  AND (
    p.permission_group = 'Delegation Reception Management'
    OR p.permission_code REGEXP '^UC-(17|18|19|20|21|22|23|24|25|26|27|28|29|30|31|32|33|34|35|36|37|38|39|40|41|42|43|44|45|46|47|48)\\.'
  );

-- 2) HO list/detail source: HO sees ONLY MULTI_CAMPUS requests.
CREATE OR REPLACE VIEW vw_visit_requests_for_ho AS
SELECT
  vr.visit_request_id,
  vr.request_code,
  vr.visitor_user_id,
  vr.partner_id,
  vr.registrant_full_name,
  vr.registrant_organization,
  vr.registrant_job_title,
  vr.registrant_phone,
  vr.registrant_email,
  vr.registrant_nationality,
  vr.delegation_name,
  vr.visit_scope,
  vr.purpose,
  vr.working_content,
  vr.expected_guest_count,
  vr.working_language,
  vr.status AS request_status,
  vr.submitted_at,
  vr.email_verified_at,
  vr.decided_by,
  vr.decided_at,
  vr.decision_actor_role,
  vr.decision_note,
  vr.row_version,
  vr.created_at,
  vr.updated_at,
  CASE
    WHEN vr.status = 'PENDING_APPROVAL'
      THEN 'WAITING_HO_APPROVAL'
    WHEN vr.status = 'APPROVED'
      AND vr.decision_actor_role = 'HO'
      THEN 'HO_APPROVED'
    WHEN vr.status = 'REJECTED'
      THEN 'REJECTED'
    WHEN vr.status = 'CANCELLED'
      THEN 'CANCELLED'
    ELSE vr.status
  END AS approval_display_status,
  CASE
    WHEN vr.status = 'PENDING_APPROVAL'
      THEN 1 ELSE 0
  END AS can_ho_decide
FROM visit_requests vr
WHERE vr.visit_scope = 'MULTI_CAMPUS';

-- 3) STAFF Leader list/detail source.
-- Backend must still filter this view with:
--   WHERE visible_campus_id = @CurrentUserPrimaryCampusId
-- and must only expose this route to STAFF sub_role = 'LEADER'.
CREATE OR REPLACE VIEW vw_visit_requests_for_staff_leader AS
SELECT
  vr.visit_request_id,
  vrc.visit_instance_id,
  vrc.campus_id AS visible_campus_id,
  vrc.current_host_user_id,
  vrc.host_assigned_by,
  vrc.host_assigned_at,
  vrc.host_assignment_source,
  vrc.host_transferred_by,
  vrc.host_transferred_at,
  vr.request_code,
  vr.visitor_user_id,
  vr.partner_id,
  vr.registrant_full_name,
  vr.registrant_organization,
  vr.registrant_job_title,
  vr.registrant_phone,
  vr.registrant_email,
  vr.registrant_nationality,
  vr.delegation_name,
  vr.visit_scope,
  vr.purpose,
  vr.working_content,
  vr.expected_guest_count,
  vr.working_language,
  vr.status AS request_status,
  vrc.status AS campus_status,
  vrc.planned_start_at,
  vrc.planned_end_at,
  vr.submitted_at,
  vr.email_verified_at,
  vr.decided_by,
  vr.decided_at,
  vr.decision_actor_role,
  vr.decision_note,
  vr.row_version AS request_row_version,
  vrc.row_version AS campus_row_version,
  vr.created_at,
  vr.updated_at,
  CASE
    WHEN vr.visit_scope = 'SINGLE_CAMPUS'
      AND vr.status = 'PENDING_APPROVAL'
      THEN 'WAITING_STAFF_LEADER_APPROVAL'
    WHEN vr.visit_scope = 'SINGLE_CAMPUS'
      AND vr.status = 'APPROVED'
      AND vr.decision_actor_role = 'STAFF_LEADER'
      THEN 'STAFF_LEADER_APPROVED'
    WHEN vr.visit_scope = 'MULTI_CAMPUS'
      AND vr.status = 'APPROVED'
      AND vr.decision_actor_role = 'HO'
      THEN 'HO_APPROVED'
    WHEN vr.status = 'REJECTED'
      THEN 'REJECTED'
    WHEN vr.status = 'CANCELLED'
      THEN 'CANCELLED'
    ELSE vr.status
  END AS approval_display_status,
  CASE
    WHEN vr.visit_scope = 'SINGLE_CAMPUS'
      AND vr.status = 'PENDING_APPROVAL'
      THEN 1 ELSE 0
  END AS can_staff_leader_decide,
  CASE
    WHEN vr.visit_scope = 'MULTI_CAMPUS'
      AND vr.status = 'APPROVED'
      AND vr.decision_actor_role = 'HO'
      AND vr.decided_at IS NOT NULL
      THEN 1 ELSE 0
  END AS is_released_by_ho
FROM visit_requests vr
JOIN visit_request_campuses vrc
  ON vrc.visit_request_id = vr.visit_request_id
WHERE
  (
    vr.visit_scope = 'SINGLE_CAMPUS'
  )
  OR
  (
    vr.visit_scope = 'MULTI_CAMPUS'
    AND vr.status = 'APPROVED'
    AND vr.decision_actor_role = 'HO'
    AND vr.decided_at IS NOT NULL
  );

-- 4) Empty ADMIN view to prevent accidental reuse of a privileged admin path.
-- If a repository/service tries to use an admin visit view, it will return zero rows.
CREATE OR REPLACE VIEW vw_visit_requests_for_admin AS
SELECT
  vr.visit_request_id,
  vr.request_code,
  vr.visit_scope,
  vr.status AS request_status,
  vr.submitted_at,
  vr.decided_by,
  vr.decided_at,
  vr.decision_actor_role,
  'ADMIN_NO_VISIT_ACCESS' AS approval_display_status
FROM visit_requests vr
WHERE 1 = 0;


-- 5) Progress summary view: request_status stays approval-only; progress_status is derived from campus statuses.
CREATE OR REPLACE VIEW vw_visit_request_progress_summary AS
SELECT
  vr.visit_request_id,
  vr.request_code,
  vr.visit_scope,
  vr.status AS request_status,
  CASE
    WHEN vr.status = 'PENDING_APPROVAL' THEN 'WAITING_APPROVAL'
    WHEN vr.status = 'REJECTED' THEN 'REJECTED'
    WHEN vr.status = 'CANCELLED' THEN 'CANCELLED'
    WHEN COUNT(vrc.visit_instance_id) = 0 THEN 'APPROVED'
    WHEN SUM(vrc.status = 'DURING_VISIT') > 0 THEN 'IN_PROGRESS'
    WHEN SUM(vrc.status = 'AFTER_VISIT') > 0 THEN 'AFTER_VISIT'
    WHEN SUM(vrc.status = 'BEFORE_VISIT') > 0 THEN 'PREPARING'
    WHEN SUM(vrc.status = 'ASSIGNED') > 0 THEN 'ASSIGNED'
    WHEN SUM(vrc.status = 'CLOSED') = COUNT(vrc.visit_instance_id) THEN 'COMPLETED'
    ELSE 'APPROVED'
  END AS progress_status,
  COUNT(vrc.visit_instance_id) AS campus_count,
  SUM(vrc.status = 'WAITING_REQUEST_APPROVAL') AS waiting_campus_count,
  SUM(vrc.status = 'ASSIGNED') AS assigned_campus_count,
  SUM(vrc.status = 'BEFORE_VISIT') AS before_visit_campus_count,
  SUM(vrc.status = 'DURING_VISIT') AS during_visit_campus_count,
  SUM(vrc.status = 'AFTER_VISIT') AS after_visit_campus_count,
  SUM(vrc.status = 'CLOSED') AS closed_campus_count,
  SUM(vrc.status = 'CANCELLED') AS cancelled_campus_count
FROM visit_requests vr
LEFT JOIN visit_request_campuses vrc
  ON vrc.visit_request_id = vr.visit_request_id
GROUP BY
  vr.visit_request_id,
  vr.request_code,
  vr.visit_scope,
  vr.status;

-- 6) Verification checks for the final rule set.
SELECT 'FINAL STRICT VISIBILITY BUILD v8.2 CANCEL_DELEGATION' AS build_name;

SELECT 'create_table_count' AS check_name, COUNT(*) AS value
FROM information_schema.tables
WHERE table_schema = DATABASE()
  AND table_type = 'BASE TABLE';


SELECT 'admin_delegation_permissions' AS check_name, COUNT(*) AS value
FROM role_permissions rp
JOIN roles r ON r.role_id = rp.role_id
JOIN permissions p ON p.permission_id = rp.permission_id
WHERE r.role_code = 'ADMIN'
  AND (
    p.permission_group = 'Delegation Reception Management'
    OR p.permission_code REGEXP '^UC-(17|18|19|20|21|22|23|24|25|26|27|28|29|30|31|32|33|34|35|36|37|38|39|40|41|42|43|44|45|46|47|48)\\.'
  );

SELECT 'uc48_non_own_permissions' AS check_name, COUNT(*) AS value
FROM role_permissions rp
JOIN permissions p ON p.permission_id = rp.permission_id
WHERE p.permission_code = 'UC-48.VIEW_EMAIL'
  AND rp.permission_level <> 'O';

SELECT 'ho_view_single_campus_rows' AS check_name, COUNT(*) AS value
FROM vw_visit_requests_for_ho
WHERE visit_scope <> 'MULTI_CAMPUS';

SELECT 'admin_view_rows' AS check_name, COUNT(*) AS value
FROM vw_visit_requests_for_admin;



-- v8.2 verification: UC-136 and cancellation metadata.
SELECT 'uc136_permission_seeded' AS check_name, COUNT(*) AS value
FROM permissions
WHERE permission_code = 'UC-136.CANCEL_VISIT_REQUEST';

SELECT 'uc136_role_grants' AS check_name, COUNT(*) AS value
FROM role_permissions rp
JOIN permissions p ON p.permission_id = rp.permission_id
WHERE p.permission_code = 'UC-136.CANCEL_VISIT_REQUEST';

SELECT 'cancellation_columns_present' AS check_name, COUNT(*) AS value
FROM information_schema.columns
WHERE table_schema = DATABASE()
  AND table_name IN ('visit_requests','visit_request_campuses')
  AND column_name IN ('cancelled_by','cancelled_at','cancellation_actor_type','cancellation_source','cancellation_reason');


-- =====================================================================
-- FINAL INT BUILD VERIFICATION
-- =====================================================================
SELECT 'FINAL INT AUTO_INCREMENT BUILD v8.2 CANCEL_DELEGATION' AS build_name;

SELECT 'char36_columns_remaining' AS check_name, COUNT(*) AS value
FROM information_schema.columns
WHERE table_schema = DATABASE()
  AND column_type LIKE 'char(36)%';

SELECT 'auto_increment_primary_keys' AS check_name, COUNT(*) AS value
FROM information_schema.columns
WHERE table_schema = DATABASE()
  AND extra LIKE '%auto_increment%';

SELECT 'role_permissions_has_surrogate_pk' AS check_name, COUNT(*) AS value
FROM information_schema.columns
WHERE table_schema = DATABASE()
  AND table_name = 'role_permissions'
  AND column_name = 'role_permission_id'
  AND extra LIKE '%auto_increment%';


SELECT 'visit_request_status_values' AS check_name,
       COLUMN_TYPE AS value
FROM information_schema.columns
WHERE table_schema = DATABASE()
  AND table_name = 'visit_requests'
  AND column_name = 'status';

SELECT 'actual_datetime_columns_removed' AS check_name, COUNT(*) AS value
FROM information_schema.columns
WHERE table_schema = DATABASE()
  AND table_name = 'visit_request_campuses'
  AND column_name IN ('actual_start_at','actual_end_at');

SELECT 'host_assignment_columns_present' AS check_name, COUNT(*) AS value
FROM information_schema.columns
WHERE table_schema = DATABASE()
  AND table_name = 'visit_request_campuses'
  AND column_name IN ('host_assigned_by','host_assigned_at','host_assignment_source');

-- =====================================================================

-- =====================================================================
-- 1. Explicit Staff Leader seed accounts

-- =====================================================================
-- 1. Explicit Staff Leader seed accounts
-- =====================================================================
UPDATE users 
SET sub_role = 'LEADER'
WHERE email IN (
  'staff.leader.hn@fpt.edu.vn',
  'anh.vu@company.vn',
  'nguyen.nam@company.vn',
  'duyen.truong@company.vn',
  'quan.hoang@company.vn'
);

-- =====================================================================
-- 2. Remaining STAFF users are normal Staff
-- =====================================================================
UPDATE users u
JOIN roles r ON r.role_id = u.role_id
SET u.sub_role = 'STAFF'
WHERE r.role_code = 'STAFF'
  AND (u.sub_role IS NULL OR u.sub_role <> 'LEADER');

