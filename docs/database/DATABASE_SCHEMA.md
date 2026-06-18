# PEMS Database Schema
> **Generated from:** PEMS v4.5 NEW BASE MySQL 8.0 Schema — **42 tables**.  
> **Purpose:** Developer-facing database schema reference for backend/entity/EF Core alignment.

---

## 1. Overview
| Item | Value |
|---|---|
| Database | `pems_db` |
| Engine | MySQL 8.0 / InnoDB |
| Charset / Collation | `utf8mb4` / `utf8mb4_unicode_ci` |
| Schema Version | PEMS v4.5 NEW BASE |
| Table Count | 42 |
| Auth Model | SSO-first; `LOCAL_PASSWORD` kept for DEV/test accounts |
| Campus Model | Visitor has no campus; internal user has one `primary_campus_id` |
| Soft Delete Policy | Only tables with explicit Delete/Remove UC keep `deleted_at/deleted_by` |
| Main Visit Approval | Request-level approval only; campus instances do not approve/reject separately |

### Key design changes in this schema

- Removed `user_campuses`; internal users now use `users.primary_campus_id`.
- Removed `tasks/task_actions`; logistics/resource workflow is handled by `visit_logistics_items`.
- Added `role_permissions.sub_role` to support STAFF/DEPT Leader vs Staff permissions without over-granting.
- Simplified minutes: `minutes` contains the main minutes; `minute_action_items` stores action items separately.
- Simplified feedback: one row per feedback target; submitter and target are both system users.
- Revised news: metadata in `news`, translations in `news_translations`, rich content sections in `news_content_sections`, and files/images in `news_section_files`.
- Binary files are stored outside DB; `files` keeps metadata only.
- Visit requests are created only after OTP/email verification; `visit_requests.status` starts at `PENDING_APPROVAL`.
- Removed `public_contents` because there is no public static-content management UC/module in the current scope.

---

## 2. High-Level Module Grouping

| Module | Tables |
|---|---|
| RBAC | `roles`, `permissions`, `role_permissions` |
| Organization | `campuses`, `departments` |
| Users & Authentication | `users`, `user_auth_providers`, `user_sessions`, `otp_tokens`, `login_logs`, `security_events` |
| Partner & File | `partners`, `partner_contacts`, `files`, `documents` |
| Visit / Delegation | `visit_requests`, `visit_request_campuses`, `visit_guest_members`, `visit_participants`, `visit_agendas`, `visit_logistics_items` |
| Minutes & Feedback | `minutes`, `minute_action_items`, `feedbacks` |
| News & FAQ | `news`, `news_translations`, `news_content_sections`, `news_section_files`, `faqs` |
| Gallery & Face Tagging | `galleries`, `gallery_images`, `photo_face_tags` |
| Email & Notification | `email_templates`, `sent_emails`, `notifications` |
| Calendar / API / Agenda Template | `calendar_events`, `api_configurations`, `api_usage_quotas`, `api_request_logs`, `agenda_templates` |
| Audit | `audit_logs`, `visit_status_logs` |

---

## 3. Entity Relationship Summary

| Area | Relationship |
|---|---|
| RBAC | `roles` → `users`; `roles` + `sub_role` + `permissions` → `role_permissions`. |
| Organization | `campuses` → `departments` → `users`. Each campus may have one active IC department enforced by trigger. |
| Users/Auth | `users` → `user_auth_providers`, `user_sessions`, `otp_tokens`, `login_logs`, `security_events`. |
| Partner/File | `partners` → `partner_contacts`; `files` → `documents`, `gallery_images`, `news.cover_file_id`, `news_section_files`. |
| Visit | `visit_requests` → `visit_request_campuses`; request has guest members; each campus instance has participants, agendas, logistics items, minutes, calendar events. |
| Minutes | `minutes` → `minute_action_items`. Action items have note/deadline/status, no assignee. |
| Feedback | `feedbacks` links submitter user and target user within a visit request/instance. |
| News | `news` → `news_translations` → `news_content_sections` → `news_section_files`. |
| Gallery | `galleries` → `gallery_images` → `photo_face_tags`. |
| API | `api_configurations` → `api_usage_quotas` and `api_request_logs`. |
| Audit | `audit_logs` tracks general entity changes; `visit_status_logs` tracks visit status timeline. |

---

## 4. Business Rules Enforced by Database

- **Visitor portal:** only VISITOR users can log in; `selected_campus_id` must be NULL.
- **Internal portal:** only non-VISITOR users can log in; `selected_campus_id` must match `users.primary_campus_id`.
- **User role/campus/department:** VISITOR must not have sub-role, campus, or department; STAFF/DEPT must have sub-role and department; STAFF department must be IC; DEPT department must be GENERAL.
- **One IC department per campus:** each campus can have only one active IC department.
- **Visit submission:** OTP/email verification happens before a visit request row is created; first request status is `PENDING_APPROVAL`.
- **Visit approval:** SINGLE_CAMPUS request is decided by STAFF_LEADER; MULTI_CAMPUS request is decided by HO; SYSTEM is allowed for system transitions.
- **Visit campus assignment:** campus instance cannot move to operational statuses before the main request is APPROVED; current host is required after approval.
- **Scope key normalization:** API quota and agenda template scope keys are automatically set to campus ID or `GLOBAL`.

---

## 5. Table Details

## 5.1. RBAC

### `roles`

**Purpose:** 6 role chính của hệ thống

**Primary Key:**
- `PRIMARY KEY (role_id)`

**Columns:**

| Column | Type | Null | Default | Notes |
|---|---|---:|---|---|
| `role_id` | `CHAR(36)` | NO | `` |  |
| `role_code` | `VARCHAR(30)` | NO | `` | ADMIN, HO, STAFF, DEPT, STUDENT, VISITOR |
| `name` | `VARCHAR(100)` | NO | `` |  |
| `description` | `VARCHAR(255)` | YES | `` |  |
| `status` | `ENUM('ACTIVE','INACTIVE')` | NO | `'ACTIVE'` | Enum: `ACTIVE`, `INACTIVE` |
| `created_at` | `DATETIME` | NO | `CURRENT_TIMESTAMP` |  |
| `deleted_at` | `DATETIME` | YES | `` | Soft delete supported by UC-121 Disable/Delete Role |
| `deleted_by` | `CHAR(36)` | YES | `` | User who soft-deleted this role; no FK here because roles is created before users |

**Unique Constraints:**
- `UNIQUE KEY uq_roles_code (role_code)`

**Indexes:**
- `KEY idx_roles_status_deleted (status, deleted_at)`

**Foreign Keys:**

_None._

**Check Constraints:**
- `CHECK (role_code IN ('ADMIN','HO','STAFF','DEPT','STUDENT','VISITOR'))`

**Implementation Notes:**
- Supports UC-based soft delete via `deleted_at/deleted_by`.


### `permissions`

**Purpose:** Danh mục quyền theo UC/action

**Primary Key:**
- `PRIMARY KEY (permission_id)`

**Columns:**

| Column | Type | Null | Default | Notes |
|---|---|---:|---|---|
| `permission_id` | `CHAR(36)` | NO | `` |  |
| `permission_code` | `VARCHAR(100)` | NO | `` | Example: UC-17.SUBMIT_VISIT_REQUEST |
| `name` | `VARCHAR(150)` | NO | `` |  |
| `permission_group` | `VARCHAR(60)` | NO | `` |  |
| `description` | `VARCHAR(500)` | YES | `` |  |
| `is_system` | `BOOLEAN` | NO | `FALSE` |  |
| `created_at` | `DATETIME` | NO | `CURRENT_TIMESTAMP` |  |

**Unique Constraints:**
- `UNIQUE KEY uq_permissions_code (permission_code)`

**Indexes:**
- `KEY idx_permissions_group (permission_group)`
- `KEY idx_permissions_group_code (permission_group, permission_code)`

**Foreign Keys:**

_None._


### `role_permissions`

**Purpose:** Ma trận phân quyền theo role + sub_role + permission

**Primary Key:**
- `PRIMARY KEY (role_id, sub_role, permission_id)`

**Columns:**

| Column | Type | Null | Default | Notes |
|---|---|---:|---|---|
| `role_id` | `CHAR(36)` | NO | `` |  |
| `sub_role` | `ENUM('NONE','Leader','Staff')` | NO | `'NONE'` | NONE for ADMIN/HO/STUDENT/VISITOR; Leader/Staff for STAFF and DEPT<br>Enum: `NONE`, `Leader`, `Staff` |
| `permission_id` | `CHAR(36)` | NO | `` |  |
| `permission_level` | `ENUM('F','E','R','O')` | NO | `` | F=Full, E=Execute/Edit, R=Read, O=Own<br>Enum: `F`, `E`, `R`, `O` |
| `granted_at` | `DATETIME` | NO | `CURRENT_TIMESTAMP` |  |
| `granted_by` | `CHAR(36)` | YES | `` |  |

**Unique Constraints:**
_None._

**Indexes:**
- `KEY idx_role_permissions_permission (permission_id)`
- `KEY idx_role_permissions_role_sub_role (role_id, sub_role)`

**Foreign Keys:**

| Constraint | Local Column(s) | References | Actions |
|---|---|---|---|
| `fk_role_permissions_role` | `role_id` | `roles(role_id)` | ON UPDATE CASCADE ON DELETE CASCADE |
| `fk_role_permissions_permission` | `permission_id` | `permissions(permission_id)` | ON UPDATE CASCADE ON DELETE CASCADE |


## 5.2. Organization

### `campuses`

**Purpose:** Danh mục campus

**Primary Key:**
- `PRIMARY KEY (campus_id)`

**Columns:**

| Column | Type | Null | Default | Notes |
|---|---|---:|---|---|
| `campus_id` | `CHAR(36)` | NO | `` |  |
| `campus_code` | `VARCHAR(20)` | NO | `` | HN, HCM, DN, CT, QN |
| `name` | `VARCHAR(150)` | NO | `` |  |
| `city` | `VARCHAR(100)` | YES | `` |  |
| `address` | `VARCHAR(255)` | YES | `` |  |
| `phone` | `VARCHAR(30)` | YES | `` |  |
| `email` | `VARCHAR(150)` | YES | `` |  |
| `ic_head_user_id` | `CHAR(36)` | YES | `` | FK added after users table |
| `status` | `ENUM('ACTIVE','INACTIVE')` | NO | `'ACTIVE'` | Enum: `ACTIVE`, `INACTIVE` |
| `created_at` | `DATETIME` | NO | `CURRENT_TIMESTAMP` |  |
| `created_by` | `CHAR(36)` | YES | `` |  |
| `updated_at` | `DATETIME` | YES | `NULL` |  |
| `updated_by` | `CHAR(36)` | YES | `` |  |

**Unique Constraints:**
- `UNIQUE KEY uq_campuses_code (campus_code)`

**Indexes:**
- `KEY idx_campuses_status (status)`
- `KEY idx_campuses_city_status (city, status)`
- `KEY idx_campuses_ic_head (ic_head_user_id)`

**Foreign Keys:**

_None._


### `departments`

**Purpose:** Phòng ban theo campus. STAFF thuộc IC, DEPT thuộc GENERAL

**Primary Key:**
- `PRIMARY KEY (department_id)`

**Columns:**

| Column | Type | Null | Default | Notes |
|---|---|---:|---|---|
| `department_id` | `CHAR(36)` | NO | `` |  |
| `campus_id` | `CHAR(36)` | NO | `` |  |
| `department_code` | `VARCHAR(50)` | NO | `` |  |
| `name` | `VARCHAR(150)` | NO | `` |  |
| `department_type` | `ENUM('IC','GENERAL')` | NO | `` | IC=International Cooperation; GENERAL=other departments<br>Enum: `IC`, `GENERAL` |
| `head_user_id` | `CHAR(36)` | YES | `` | FK added after users table |
| `status` | `ENUM('ACTIVE','INACTIVE')` | NO | `'ACTIVE'` | Enum: `ACTIVE`, `INACTIVE` |
| `created_at` | `DATETIME` | NO | `CURRENT_TIMESTAMP` |  |
| `created_by` | `CHAR(36)` | YES | `` |  |
| `updated_at` | `DATETIME` | YES | `NULL` |  |
| `updated_by` | `CHAR(36)` | YES | `` |  |

**Unique Constraints:**
- `UNIQUE KEY uq_departments_campus_code (campus_id, department_code)`
- `UNIQUE KEY uq_departments_campus_name (campus_id, name)`

**Indexes:**
- `KEY idx_departments_campus_type (campus_id, department_type)`
- `KEY idx_departments_status (status)`
- `KEY idx_departments_head (head_user_id)`

**Foreign Keys:**

| Constraint | Local Column(s) | References | Actions |
|---|---|---|---|
| `fk_departments_campus` | `campus_id` | `campuses(campus_id)` | ON UPDATE CASCADE ON DELETE RESTRICT |


## 5.3. Users & Authentication

### `users`

**Purpose:** Tài khoản chính. Production dùng SSO; LOCAL_PASSWORD chỉ dùng DEV/test.

**Primary Key:**
- `PRIMARY KEY (user_id)`

**Columns:**

| Column | Type | Null | Default | Notes |
|---|---|---:|---|---|
| `user_id` | `CHAR(36)` | NO | `` |  |
| `full_name` | `VARCHAR(150)` | NO | `` |  |
| `email` | `VARCHAR(150)` | NO | `` |  |
| `phone` | `VARCHAR(30)` | YES | `` |  |
| `nationality` | `VARCHAR(100)` | YES | `` | Quốc tịch của user/visitor |
| `password_hash` | `VARCHAR(255)` | YES | `` | DEV/local password hash only. Production SSO-only accounts keep this NULL. |
| `role_id` | `CHAR(36)` | NO | `` |  |
| `sub_role` | `ENUM('Leader','Staff')` | YES | `` | Only for STAFF/DEPT<br>Enum: `Leader`, `Staff` |
| `primary_campus_id` | `CHAR(36)` | YES | `` | Campus duy nhất của user nội bộ. VISITOR phải NULL. |
| `department_id` | `CHAR(36)` | YES | `` | STAFF = IC department; DEPT = GENERAL department |
| `gender` | `ENUM('MALE','FEMALE','OTHER','UNKNOWN')` | YES | `` | Enum: `MALE`, `FEMALE`, `OTHER`, `UNKNOWN` |
| `avatar_url` | `VARCHAR(500)` | YES | `` |  |
| `student_code` | `VARCHAR(30)` | YES | `` |  |
| `fe_id` | `VARCHAR(100)` | YES | `` |  |
| `status` | `ENUM('ACTIVE','INACTIVE','LOCKED')` | NO | `'ACTIVE'` | ACTIVE=hoạt động, INACTIVE=tạm ngưng, LOCKED=bị khóa<br>Enum: `ACTIVE`, `INACTIVE`, `LOCKED` |
| `email_verified_at` | `DATETIME` | YES | `` | Thời điểm email được xác thực qua SSO lần đầu hoặc xác nhận bởi hệ thống |
| `failed_login_count` | `INT UNSIGNED` | NO | `0` | Số lần đăng nhập sai local password liên tiếp; reset khi login thành công |
| `locked_until` | `DATETIME` | YES | `` | Thời điểm hết khóa tạm thời nếu bị lock |
| `created_via` | `ENUM('MANUAL_CREATED','VISITOR_FORM')` | NO | `'MANUAL_CREATED'` | MANUAL_CREATED=HO/Staff Leader tạo, VISITOR_FORM=tạo từ form visitor<br>Enum: `MANUAL_CREATED`, `VISITOR_FORM` |
| `first_login_at` | `DATETIME` | YES | `` |  |
| `last_login_at` | `DATETIME` | YES | `` |  |
| `created_at` | `DATETIME` | NO | `CURRENT_TIMESTAMP` |  |
| `created_by` | `CHAR(36)` | YES | `` |  |
| `updated_at` | `DATETIME` | YES | `NULL` |  |
| `updated_by` | `CHAR(36)` | YES | `` |  |

**Unique Constraints:**
- `UNIQUE KEY uq_users_email (email)`
- `UNIQUE KEY uq_users_student_code (student_code)`
- `UNIQUE KEY uq_users_fe_id (fe_id)`

**Indexes:**
- `KEY idx_users_role_sub_role (role_id, sub_role)`
- `KEY idx_users_primary_campus (primary_campus_id)`
- `KEY idx_users_department (department_id)`
- `KEY idx_users_status (status)`
- `KEY idx_users_email_status (email, status)`
- `KEY idx_users_campus_role_status (primary_campus_id, role_id, status)`
- `KEY idx_users_department_status (department_id, status)`
- `KEY idx_users_created_via (created_via)`
- `KEY idx_users_last_login (last_login_at)`
- `KEY idx_users_nationality (nationality)`

**Foreign Keys:**

| Constraint | Local Column(s) | References | Actions |
|---|---|---|---|
| `fk_users_role` | `role_id` | `roles(role_id)` | ON UPDATE CASCADE ON DELETE RESTRICT |
| `fk_users_primary_campus` | `primary_campus_id` | `campuses(campus_id)` | ON UPDATE CASCADE ON DELETE RESTRICT |
| `fk_users_department` | `department_id` | `departments(department_id)` | ON UPDATE CASCADE ON DELETE RESTRICT |


### `user_auth_providers`

**Purpose:** Provider đăng nhập của user. Production dùng GOOGLE_SSO/FEID; LOCAL_PASSWORD chỉ dùng DEV/test.

**Primary Key:**
- `PRIMARY KEY (auth_provider_id)`

**Columns:**

| Column | Type | Null | Default | Notes |
|---|---|---:|---|---|
| `auth_provider_id` | `CHAR(36)` | NO | `` |  |
| `user_id` | `CHAR(36)` | NO | `` |  |
| `provider_type` | `ENUM('LOCAL_PASSWORD','GOOGLE_SSO','FEID')` | NO | `` | Enum: `LOCAL_PASSWORD`, `GOOGLE_SSO`, `FEID` |
| `provider_subject` | `VARCHAR(255)` | YES | `` | Required for GOOGLE_SSO/FEID |
| `provider_email` | `VARCHAR(150)` | YES | `` |  |
| `is_enabled` | `BOOLEAN` | NO | `TRUE` |  |
| `linked_at` | `DATETIME` | NO | `CURRENT_TIMESTAMP` |  |
| `last_used_at` | `DATETIME` | YES | `` |  |

**Unique Constraints:**
- `UNIQUE KEY uq_user_auth_provider_type (user_id, provider_type)`
- `UNIQUE KEY uq_auth_provider_subject (provider_type, provider_subject)`

**Indexes:**
- `KEY idx_auth_provider_email (provider_email)`
- `KEY idx_auth_provider_type_email_enabled (provider_type, provider_email, is_enabled)`

**Foreign Keys:**

| Constraint | Local Column(s) | References | Actions |
|---|---|---|---|
| `fk_auth_providers_user` | `user_id` | `users(user_id)` | ON UPDATE CASCADE ON DELETE CASCADE |


### `user_sessions`

**Purpose:** Session + refresh token hash

**Primary Key:**
- `PRIMARY KEY (session_id)`

**Columns:**

| Column | Type | Null | Default | Notes |
|---|---|---:|---|---|
| `session_id` | `CHAR(36)` | NO | `` |  |
| `user_id` | `CHAR(36)` | NO | `` |  |
| `login_portal` | `ENUM('VISITOR','INTERNAL')` | NO | `` | Enum: `VISITOR`, `INTERNAL` |
| `selected_campus_id` | `CHAR(36)` | YES | `` | Auto set to users.primary_campus_id for INTERNAL, NULL for VISITOR |
| `auth_provider_id` | `CHAR(36)` | YES | `` |  |
| `refresh_token_hash` | `VARCHAR(255)` | YES | `` | Refresh token hash merged into session |
| `refresh_expires_at` | `DATETIME` | YES | `` |  |
| `refresh_revoked_at` | `DATETIME` | YES | `` |  |
| `ip_address` | `VARCHAR(45)` | YES | `` |  |
| `user_agent` | `VARCHAR(500)` | YES | `` |  |
| `created_at` | `DATETIME` | NO | `CURRENT_TIMESTAMP` |  |
| `expires_at` | `DATETIME` | NO | `` |  |
| `revoked_at` | `DATETIME` | YES | `` |  |
| `revoked_by` | `CHAR(36)` | YES | `` |  |
| `revoked_reason` | `VARCHAR(255)` | YES | `` |  |

**Unique Constraints:**
- `UNIQUE KEY uq_sessions_refresh_hash (refresh_token_hash)`

**Indexes:**
- `KEY idx_sessions_user_active (user_id, revoked_at, expires_at)`
- `KEY idx_sessions_portal_campus (login_portal, selected_campus_id)`
- `KEY idx_sessions_refresh_active (refresh_token_hash, refresh_revoked_at, refresh_expires_at)`
- `KEY idx_sessions_ip_time (ip_address, created_at)`

**Foreign Keys:**

| Constraint | Local Column(s) | References | Actions |
|---|---|---|---|
| `fk_sessions_user` | `user_id` | `users(user_id)` | ON UPDATE CASCADE ON DELETE CASCADE |
| `fk_sessions_selected_campus` | `selected_campus_id` | `campuses(campus_id)` | ON UPDATE CASCADE ON DELETE RESTRICT |
| `fk_sessions_auth_provider` | `auth_provider_id` | `user_auth_providers(auth_provider_id)` | ON UPDATE CASCADE ON DELETE SET NULL |
| `fk_sessions_revoked_by` | `revoked_by` | `users(user_id)` | ON UPDATE CASCADE ON DELETE SET NULL |


### `otp_tokens`

**Purpose:** OTP, magic link, set password token, reset password token

**Primary Key:**
- `PRIMARY KEY (otp_token_id)`

**Columns:**

| Column | Type | Null | Default | Notes |
|---|---|---:|---|---|
| `otp_token_id` | `CHAR(36)` | NO | `` |  |
| `user_id` | `CHAR(36)` | YES | `` |  |
| `email` | `VARCHAR(150)` | NO | `` |  |
| `token_type` | `ENUM('OTP_CODE','MAGIC_LINK')` | NO | `'OTP_CODE'` | Enum: `OTP_CODE`, `MAGIC_LINK` |
| `purpose` | `ENUM('VISIT_REQUEST_VERIFY','CHANGE_SENSITIVE_ACTION')` | NO | `` | Enum: `VISIT_REQUEST_VERIFY`, `CHANGE_SENSITIVE_ACTION` |
| `token_hash` | `VARCHAR(255)` | NO | `` |  |
| `expires_at` | `DATETIME` | NO | `` |  |
| `used_at` | `DATETIME` | YES | `` |  |
| `attempt_count` | `INT UNSIGNED` | NO | `0` |  |
| `max_attempts` | `INT UNSIGNED` | NO | `5` |  |
| `resend_count` | `INT UNSIGNED` | NO | `0` |  |
| `ip_address` | `VARCHAR(45)` | YES | `` |  |
| `user_agent` | `VARCHAR(500)` | YES | `` |  |
| `created_at` | `DATETIME` | NO | `CURRENT_TIMESTAMP` |  |

**Unique Constraints:**
- `UNIQUE KEY uq_otp_tokens_hash (token_hash)`

**Indexes:**
- `KEY idx_otp_email_purpose_time (email, purpose, created_at)`
- `KEY idx_otp_email_purpose_active (email, purpose, used_at, expires_at)`
- `KEY idx_otp_user_purpose_active (user_id, purpose, used_at, expires_at)`
- `KEY idx_otp_ip_time (ip_address, created_at)`

**Foreign Keys:**

| Constraint | Local Column(s) | References | Actions |
|---|---|---|---|
| `fk_otp_tokens_user` | `user_id` | `users(user_id)` | ON UPDATE CASCADE ON DELETE CASCADE |


### `login_logs`

**Purpose:** Lịch sử đăng nhập

**Primary Key:**
- `PRIMARY KEY (login_log_id)`

**Columns:**

| Column | Type | Null | Default | Notes |
|---|---|---:|---|---|
| `login_log_id` | `BIGINT UNSIGNED` | NO | `` |  |
| `user_id` | `CHAR(36)` | YES | `` |  |
| `email` | `VARCHAR(150)` | NO | `` |  |
| `login_portal` | `ENUM('VISITOR','INTERNAL')` | NO | `` | Enum: `VISITOR`, `INTERNAL` |
| `selected_campus_id` | `CHAR(36)` | YES | `` |  |
| `provider_type` | `ENUM('LOCAL_PASSWORD','GOOGLE_SSO','FEID')` | YES | `` | Enum: `LOCAL_PASSWORD`, `GOOGLE_SSO`, `FEID` |
| `status` | `ENUM('SUCCESS','FAILED','BLOCKED')` | NO | `` | Enum: `SUCCESS`, `FAILED`, `BLOCKED` |
| `failure_reason` | `VARCHAR(255)` | YES | `` |  |
| `ip_address` | `VARCHAR(45)` | YES | `` |  |
| `user_agent` | `VARCHAR(500)` | YES | `` |  |
| `session_id` | `CHAR(36)` | YES | `` |  |
| `created_at` | `DATETIME` | NO | `CURRENT_TIMESTAMP` |  |

**Unique Constraints:**
_None._

**Indexes:**
- `KEY idx_login_logs_user_time (user_id, created_at)`
- `KEY idx_login_logs_email_status_time (email, status, created_at)`
- `KEY idx_login_logs_ip_status_time (ip_address, status, created_at)`
- `KEY idx_login_logs_portal_campus (login_portal, selected_campus_id)`
- `KEY idx_login_logs_provider_time (provider_type, created_at)`

**Foreign Keys:**

| Constraint | Local Column(s) | References | Actions |
|---|---|---|---|
| `fk_login_logs_user` | `user_id` | `users(user_id)` | ON UPDATE CASCADE ON DELETE SET NULL |
| `fk_login_logs_campus` | `selected_campus_id` | `campuses(campus_id)` | ON UPDATE CASCADE ON DELETE SET NULL |


### `security_events`

**Purpose:** Security, abuse, lockout events

**Primary Key:**
- `PRIMARY KEY (security_event_id)`

**Columns:**

| Column | Type | Null | Default | Notes |
|---|---|---:|---|---|
| `security_event_id` | `BIGINT UNSIGNED` | NO | `` |  |
| `user_id` | `CHAR(36)` | YES | `` |  |
| `email` | `VARCHAR(150)` | YES | `` |  |
| `event_type` | `VARCHAR(80)` | NO | `` | LOGIN_LOCKED, OTP_FAILED, SUSPICIOUS_IP... |
| `severity` | `ENUM('LOW','MEDIUM','HIGH','CRITICAL')` | NO | `'LOW'` | Enum: `LOW`, `MEDIUM`, `HIGH`, `CRITICAL` |
| `ip_address` | `VARCHAR(45)` | YES | `` |  |
| `user_agent` | `VARCHAR(500)` | YES | `` |  |
| `metadata` | `JSON` | YES | `` |  |
| `created_at` | `DATETIME` | NO | `CURRENT_TIMESTAMP` |  |

**Unique Constraints:**
_None._

**Indexes:**
- `KEY idx_security_user_time (user_id, created_at)`
- `KEY idx_security_email_time (email, created_at)`
- `KEY idx_security_type_time (event_type, created_at)`
- `KEY idx_security_ip_time (ip_address, created_at)`
- `KEY idx_security_severity_time (severity, created_at)`

**Foreign Keys:**

| Constraint | Local Column(s) | References | Actions |
|---|---|---|---|
| `fk_security_events_user` | `user_id` | `users(user_id)` | ON UPDATE CASCADE ON DELETE SET NULL |

**Implementation Notes:**
- Contains JSON column(s); keep API DTO validation strict because DB does not enforce JSON shape.


## 5.4. Partner & File

### `partners`

**Purpose:** Hồ sơ đối tác

**Primary Key:**
- `PRIMARY KEY (partner_id)`

**Columns:**

| Column | Type | Null | Default | Notes |
|---|---|---:|---|---|
| `partner_id` | `CHAR(36)` | NO | `` |  |
| `partner_code` | `VARCHAR(50)` | YES | `` |  |
| `name` | `VARCHAR(200)` | NO | `` |  |
| `short_name` | `VARCHAR(100)` | YES | `` |  |
| `country` | `VARCHAR(100)` | YES | `` |  |
| `city` | `VARCHAR(100)` | YES | `` |  |
| `website_url` | `VARCHAR(500)` | YES | `` |  |
| `partner_type` | `ENUM('UNIVERSITY','COMPANY','GOVERNMENT','NGO','OTHER')` | NO | `'UNIVERSITY'` | Enum: `UNIVERSITY`, `COMPANY`, `GOVERNMENT`, `NGO`, `OTHER` |
| `cooperation_status` | `ENUM('POTENTIAL','ACTIVE','INACTIVE','BLACKLISTED')` | NO | `'POTENTIAL'` | Enum: `POTENTIAL`, `ACTIVE`, `INACTIVE`, `BLACKLISTED` |
| `description` | `TEXT` | YES | `` |  |
| `created_at` | `DATETIME` | NO | `CURRENT_TIMESTAMP` |  |
| `created_by` | `CHAR(36)` | YES | `` |  |
| `updated_at` | `DATETIME` | YES | `NULL` |  |
| `updated_by` | `CHAR(36)` | YES | `` |  |

**Unique Constraints:**
- `UNIQUE KEY uq_partners_code (partner_code)`

**Indexes:**
- `KEY idx_partners_country (country)`
- `KEY idx_partners_status (cooperation_status)`
- `KEY idx_partners_type_status (partner_type, cooperation_status)`
- `KEY idx_partners_created_at (created_at)`
- `FULLTEXT KEY ft_partners_search (name, short_name, description)`

**Foreign Keys:**

_None._


### `partner_contacts`

**Purpose:** Người liên hệ đối tác. OCR final confirmed data saved here.

**Primary Key:**
- `PRIMARY KEY (contact_id)`

**Columns:**

| Column | Type | Null | Default | Notes |
|---|---|---:|---|---|
| `contact_id` | `CHAR(36)` | NO | `` |  |
| `partner_id` | `CHAR(36)` | NO | `` |  |
| `full_name` | `VARCHAR(150)` | NO | `` |  |
| `email` | `VARCHAR(150)` | YES | `` |  |
| `phone` | `VARCHAR(50)` | YES | `` |  |
| `job_title` | `VARCHAR(150)` | YES | `` |  |
| `department_name` | `VARCHAR(150)` | YES | `` |  |
| `note` | `TEXT` | YES | `` |  |
| `is_primary` | `BOOLEAN` | NO | `FALSE` |  |
| `status` | `ENUM('ACTIVE','INACTIVE')` | NO | `'ACTIVE'` | Enum: `ACTIVE`, `INACTIVE` |
| `created_at` | `DATETIME` | NO | `CURRENT_TIMESTAMP` |  |
| `created_by` | `CHAR(36)` | YES | `` |  |
| `updated_at` | `DATETIME` | YES | `NULL` |  |
| `updated_by` | `CHAR(36)` | YES | `` |  |

**Unique Constraints:**
- `UNIQUE KEY uq_partner_contacts_partner_email (partner_id, email)`

**Indexes:**
- `KEY idx_partner_contacts_partner (partner_id)`
- `KEY idx_partner_contacts_email (email)`
- `KEY idx_partner_contacts_status (status)`

**Foreign Keys:**

| Constraint | Local Column(s) | References | Actions |
|---|---|---|---|
| `fk_partner_contacts_partner` | `partner_id` | `partners(partner_id)` | ON UPDATE CASCADE ON DELETE RESTRICT |


### `files`

**Purpose:** File metadata only. Binary file is stored outside DB.

**Primary Key:**
- `PRIMARY KEY (file_id)`

**Columns:**

| Column | Type | Null | Default | Notes |
|---|---|---:|---|---|
| `file_id` | `CHAR(36)` | NO | `` |  |
| `storage_provider` | `ENUM('LOCAL','S3','AZURE','GCS','GOOGLE_DRIVE','OTHER')` | NO | `'LOCAL'` | Enum: `LOCAL`, `S3`, `AZURE`, `GCS`, `GOOGLE_DRIVE`, `OTHER` |
| `bucket_name` | `VARCHAR(150)` | YES | `` |  |
| `object_key` | `VARCHAR(700)` | NO | `` | Max 700 chars to keep UNIQUE index safe under utf8mb4 |
| `original_filename` | `VARCHAR(255)` | NO | `` |  |
| `mime_type` | `VARCHAR(150)` | YES | `` |  |
| `file_size` | `BIGINT UNSIGNED` | YES | `` |  |
| `checksum_sha256` | `CHAR(64)` | YES | `` | SHA-256 checksum for file integrity/deduplication |
| `visibility` | `ENUM('PRIVATE','INTERNAL','PUBLIC')` | NO | `'PRIVATE'` | Enum: `PRIVATE`, `INTERNAL`, `PUBLIC` |
| `uploaded_by` | `CHAR(36)` | YES | `` |  |
| `uploaded_at` | `DATETIME` | NO | `CURRENT_TIMESTAMP` |  |

**Unique Constraints:**
- `UNIQUE KEY uq_files_object_key (object_key)`

**Indexes:**
- `KEY idx_files_uploaded_by (uploaded_by, uploaded_at)`
- `KEY idx_files_visibility (visibility)`
- `KEY idx_files_mime_time (mime_type, uploaded_at)`
- `KEY idx_files_checksum (checksum_sha256)`

**Foreign Keys:**

| Constraint | Local Column(s) | References | Actions |
|---|---|---|---|
| `fk_files_uploaded_by` | `uploaded_by` | `users(user_id)` | ON UPDATE CASCADE ON DELETE SET NULL |


### `documents`

**Purpose:** Tài liệu nghiệp vụ. partner_documents/reports/logistics documents merged by owner_type.

**Primary Key:**
- `PRIMARY KEY (document_id)`

**Columns:**

| Column | Type | Null | Default | Notes |
|---|---|---:|---|---|
| `document_id` | `CHAR(36)` | NO | `` |  |
| `file_id` | `CHAR(36)` | NO | `` |  |
| `owner_type` | `ENUM('GENERAL','VISIT','PARTNER','MINUTES','NEWS','LOGISTICS','REPORT')` | NO | `'GENERAL'` | Enum: `GENERAL`, `VISIT`, `PARTNER`, `MINUTES`, `NEWS`, `LOGISTICS`, `REPORT` |
| `owner_id` | `CHAR(36)` | YES | `` |  |
| `campus_id` | `CHAR(36)` | YES | `` |  |
| `title` | `VARCHAR(255)` | NO | `` |  |
| `description` | `TEXT` | YES | `` |  |
| `document_category` | `VARCHAR(100)` | YES | `` |  |
| `status` | `ENUM('DRAFT','PUBLISHED','ARCHIVED')` | NO | `'DRAFT'` | Enum: `DRAFT`, `PUBLISHED`, `ARCHIVED` |
| `created_at` | `DATETIME` | NO | `CURRENT_TIMESTAMP` |  |
| `created_by` | `CHAR(36)` | YES | `` |  |
| `updated_at` | `DATETIME` | YES | `NULL` |  |
| `updated_by` | `CHAR(36)` | YES | `` |  |

**Unique Constraints:**
_None._

**Indexes:**
- `KEY idx_documents_owner (owner_type, owner_id)`
- `KEY idx_documents_campus_status (campus_id, status)`
- `KEY idx_documents_category_status (document_category, status)`
- `KEY idx_documents_created_by_time (created_by, created_at)`
- `FULLTEXT KEY ft_documents_search (title, description)`

**Foreign Keys:**

| Constraint | Local Column(s) | References | Actions |
|---|---|---|---|
| `fk_documents_file` | `file_id` | `files(file_id)` | ON UPDATE CASCADE ON DELETE RESTRICT |
| `fk_documents_campus` | `campus_id` | `campuses(campus_id)` | ON UPDATE CASCADE ON DELETE SET NULL |
| `fk_documents_created_by` | `created_by` | `users(user_id)` | ON UPDATE CASCADE ON DELETE SET NULL |


## 5.5. Visit / Delegation

### `visit_requests`

**Purpose:** Đơn đăng ký tham quan. Nội dung không được sửa sau khi chuyển sang PENDING_APPROVAL; thời gian/campus lưu ở visit_request_campuses.

**Primary Key:**
- `PRIMARY KEY (visit_request_id)`

**Columns:**

| Column | Type | Null | Default | Notes |
|---|---|---:|---|---|
| `visit_request_id` | `CHAR(36)` | NO | `` |  |
| `request_code` | `VARCHAR(50)` | NO | `` |  |
| `visitor_user_id` | `CHAR(36)` | NO | `` | Visitor user/account created or linked for the registrant |
| `partner_id` | `CHAR(36)` | YES | `` |  |
| `registrant_full_name` | `VARCHAR(150)` | NO | `` | Họ và tên người đăng ký |
| `registrant_organization` | `VARCHAR(200)` | NO | `` | Đơn vị công tác người đăng ký |
| `registrant_job_title` | `VARCHAR(150)` | YES | `` | Chức danh/phòng ban người đăng ký |
| `registrant_phone` | `VARCHAR(50)` | YES | `` | SĐT người đăng ký |
| `registrant_email` | `VARCHAR(150)` | NO | `` | Email người đăng ký |
| `delegation_name` | `VARCHAR(200)` | NO | `` | Tên đoàn khách |
| `visit_scope` | `ENUM('SINGLE_CAMPUS','MULTI_CAMPUS')` | NO | `'SINGLE_CAMPUS'` | SINGLE_CAMPUS: Staff Leader duyệt request tổng; MULTI_CAMPUS: HO duyệt request tổng. Frontend/backend suy ra người duyệt từ cột này.<br>Enum: `SINGLE_CAMPUS`, `MULTI_CAMPUS` |
| `purpose` | `TEXT` | NO | `` | Mục đích thăm FPTU |
| `working_content` | `TEXT` | YES | `` | Nội dung làm việc tại FPTU |
| `expected_guest_count` | `INT UNSIGNED` | NO | `1` | Số khách dự kiến; có thể đồng bộ từ danh sách khách |
| `support_team_json` | `JSON` | YES | `` | Danh sách team hỗ trợ khách từ phía đoàn/đơn vị gửi |
| `contact_person_json` | `JSON` | YES | `` | Thông tin đầu mối liên hệ: full_name, organization, phone, email |
| `working_language` | `ENUM('VI','EN','OTHER')` | NO | `'EN'` | Ngôn ngữ sử dụng trong visit<br>Enum: `VI`, `EN`, `OTHER` |
| `interpreter_note` | `TEXT` | YES | `` | Ghi chú nếu ngôn ngữ khác VI/EN và đầu mối cần tự bố trí phiên dịch |
| `transportation_note` | `TEXT` | YES | `` | Nhận diện phương tiện di chuyển tới FPTU |
| `note_to_fptu` | `TEXT` | YES | `` | Ghi chú cho FPTU |
| `status` | `ENUM('PENDING_APPROVAL','REJECTED','APPROVED','CANCELLED')` | NO | `'PENDING_APPROVAL'` | Enum: `PENDING_APPROVAL`, `REJECTED`, `APPROVED`, `CANCELLED` |
| `submitted_at` | `DATETIME` | NO | `CURRENT_TIMESTAMP` |  |
| `email_verified_at` | `DATETIME` | YES | `` |  |
| `decided_by` | `CHAR(36)` | YES | `` | Người approve/reject/cancel request tổng |
| `decided_at` | `DATETIME` | YES | `` | Thời điểm xử lý request tổng |
| `decision_actor_role` | `ENUM('HO','STAFF_LEADER','SYSTEM')` | YES | `` | Vai trò người xử lý tại thời điểm quyết định<br>Enum: `HO`, `STAFF_LEADER`, `SYSTEM` |
| `decision_note` | `TEXT` | YES | `` | Lý do/ghi chú khi approve, reject hoặc cancel |
| `row_version` | `INT UNSIGNED` | NO | `0` | Optimistic concurrency token |
| `created_at` | `DATETIME` | NO | `CURRENT_TIMESTAMP` |  |
| `created_by` | `CHAR(36)` | YES | `` |  |
| `updated_at` | `DATETIME` | YES | `NULL` |  |
| `updated_by` | `CHAR(36)` | YES | `` |  |

**Unique Constraints:**
- `UNIQUE KEY uq_visit_requests_code (request_code)`

**Indexes:**
- `KEY idx_visit_requests_visitor (visitor_user_id)`
- `KEY idx_visit_requests_partner (partner_id)`
- `KEY idx_visit_requests_status_submitted (status, submitted_at)`
- `KEY idx_visit_requests_registrant_email (registrant_email)`
- `KEY idx_visit_requests_scope_status (visit_scope, status)`
- `KEY idx_visit_requests_decision (decided_by, decided_at)`
- `KEY idx_visit_requests_decision_role (decision_actor_role, decided_at)`

**Foreign Keys:**

| Constraint | Local Column(s) | References | Actions |
|---|---|---|---|
| `fk_visit_requests_visitor_user` | `visitor_user_id` | `users(user_id)` | ON UPDATE CASCADE ON DELETE RESTRICT |
| `fk_visit_requests_partner` | `partner_id` | `partners(partner_id)` | ON UPDATE CASCADE ON DELETE SET NULL |
| `fk_visit_requests_decided_by` | `decided_by` | `users(user_id)` | ON UPDATE CASCADE ON DELETE SET NULL |

**Check Constraints:**
- `CHECK (expected_guest_count >= 1)`
- `CHECK ( decision_actor_role IS NULL OR status NOT IN ('APPROVED','REJECTED','CANCELLED') OR ( visit_scope = 'SINGLE_CAMPUS' AND decision_actor_role IN ('STAFF_LEADER','SYSTEM') ) OR ( visit_scope = 'MULTI_CAMPUS' AND decision_actor_role IN ('HO','SYSTEM') ) )`

**Implementation Notes:**
- Uses `row_version` as an optimistic concurrency token.
- Contains JSON column(s); keep API DTO validation strict because DB does not enforce JSON shape.


### `visit_request_campuses`

**Purpose:** Mỗi campus trong request có một instance riêng. Campus không duyệt/từ chối riêng; sau khi request tổng được duyệt, backend gán current_host_user_id và chuyển status=ASSIGNED.

**Primary Key:**
- `PRIMARY KEY (visit_instance_id)`

**Columns:**

| Column | Type | Null | Default | Notes |
|---|---|---:|---|---|
| `visit_instance_id` | `CHAR(36)` | NO | `` |  |
| `visit_request_id` | `CHAR(36)` | NO | `` |  |
| `campus_id` | `CHAR(36)` | NO | `` |  |
| `instance_code` | `VARCHAR(60)` | YES | `` |  |
| `planned_start_at` | `DATETIME` | NO | `` | Ngày giờ bắt đầu dự kiến tại campus |
| `planned_end_at` | `DATETIME` | NO | `` | Ngày giờ kết thúc dự kiến tại campus |
| `actual_start_at` | `DATETIME` | YES | `` | Ngày giờ bắt đầu thực tế |
| `actual_end_at` | `DATETIME` | YES | `` | Ngày giờ kết thúc thực tế |
| `status` | `ENUM( 'WAITING_REQUEST_APPROVAL', 'ASSIGNED', 'BEFORE_VISIT', 'DURING_VISIT', 'AFTER_VISIT', 'CLOSED', 'CANCELLED' )` | NO | `'WAITING_REQUEST_APPROVAL'` | Enum: `WAITING_REQUEST_APPROVAL`, `ASSIGNED`, `BEFORE_VISIT`, `DURING_VISIT`, `AFTER_VISIT`, `CLOSED`, `CANCELLED` |
| `current_host_user_id` | `CHAR(36)` | YES | `` | Host hiện tại chịu trách nhiệm campus instance. Mặc định là Staff Leader của campus sau khi request tổng được duyệt; có thể chuyển cho IC Staff khác cùng campus |
| `host_transferred_by` | `CHAR(36)` | YES | `` | Người chuyển host gần nhất |
| `host_transferred_at` | `DATETIME` | YES | `` | Thời điểm chuyển host gần nhất |
| `host_transfer_note` | `TEXT` | YES | `` | Ghi chú/lý do chuyển host gần nhất |
| `closed_by` | `CHAR(36)` | YES | `` |  |
| `closed_at` | `DATETIME` | YES | `` |  |
| `close_note` | `TEXT` | YES | `` |  |
| `row_version` | `INT UNSIGNED` | NO | `0` | Optimistic concurrency token |
| `created_at` | `DATETIME` | NO | `CURRENT_TIMESTAMP` |  |
| `created_by` | `CHAR(36)` | YES | `` |  |
| `updated_at` | `DATETIME` | YES | `NULL` |  |
| `updated_by` | `CHAR(36)` | YES | `` |  |

**Unique Constraints:**
- `UNIQUE KEY uq_visit_instance_request_campus (visit_request_id, campus_id)`
- `UNIQUE KEY uq_visit_instance_code (instance_code)`

**Indexes:**
- `KEY idx_visit_instances_campus_status_time (campus_id, status, planned_start_at)`
- `KEY idx_visit_instances_request (visit_request_id)`
- `KEY idx_visit_instances_status_time (status, planned_start_at)`
- `KEY idx_visit_instances_current_host (current_host_user_id, status)`
- `KEY idx_visit_instances_host_transfer (host_transferred_by, host_transferred_at)`

**Foreign Keys:**

| Constraint | Local Column(s) | References | Actions |
|---|---|---|---|
| `fk_visit_instances_request` | `visit_request_id` | `visit_requests(visit_request_id)` | ON UPDATE CASCADE ON DELETE RESTRICT |
| `fk_visit_instances_campus` | `campus_id` | `campuses(campus_id)` | ON UPDATE CASCADE ON DELETE RESTRICT |
| `fk_visit_instances_current_host` | `current_host_user_id` | `users(user_id)` | ON UPDATE CASCADE ON DELETE SET NULL |
| `fk_visit_instances_host_transferred_by` | `host_transferred_by` | `users(user_id)` | ON UPDATE CASCADE ON DELETE SET NULL |
| `fk_visit_instances_closed_by` | `closed_by` | `users(user_id)` | ON UPDATE CASCADE ON DELETE SET NULL |

**Check Constraints:**
- `CHECK (planned_end_at > planned_start_at)`
- `CHECK (actual_end_at IS NULL OR actual_start_at IS NULL OR actual_end_at > actual_start_at)`

**Implementation Notes:**
- Uses `row_version` as an optimistic concurrency token.


### `visit_guest_members`

**Purpose:** Danh sách từng người trong đoàn khách. Không lưu consent hình ảnh vì form đã bỏ phần xác nhận sử dụng hình ảnh/thông tin.

**Primary Key:**
- `PRIMARY KEY (guest_member_id)`

**Columns:**

| Column | Type | Null | Default | Notes |
|---|---|---:|---|---|
| `guest_member_id` | `CHAR(36)` | NO | `` |  |
| `visit_request_id` | `CHAR(36)` | NO | `` |  |
| `full_name` | `VARCHAR(150)` | NO | `` |  |
| `organization` | `VARCHAR(200)` | YES | `` |  |
| `job_title` | `VARCHAR(150)` | YES | `` |  |
| `nationality` | `VARCHAR(100)` | YES | `` |  |
| `email` | `VARCHAR(150)` | YES | `` |  |
| `phone` | `VARCHAR(50)` | YES | `` |  |
| `is_representative` | `BOOLEAN` | NO | `FALSE` |  |
| `note` | `TEXT` | YES | `` |  |
| `created_at` | `DATETIME` | NO | `CURRENT_TIMESTAMP` |  |
| `created_by` | `CHAR(36)` | YES | `` |  |
| `updated_at` | `DATETIME` | YES | `NULL` |  |
| `updated_by` | `CHAR(36)` | YES | `` |  |

**Unique Constraints:**
_None._

**Indexes:**
- `KEY idx_guest_members_request (visit_request_id)`
- `KEY idx_guest_members_email (email)`
- `KEY idx_guest_members_representative (visit_request_id, is_representative)`

**Foreign Keys:**

| Constraint | Local Column(s) | References | Actions |
|---|---|---|---|
| `fk_guest_members_request` | `visit_request_id` | `visit_requests(visit_request_id)` | ON UPDATE CASCADE ON DELETE RESTRICT |


### `visit_participants`

**Purpose:** Người nội bộ tham gia. HOST lưu bằng is_host. One-host rule should be enforced by backend/audit for portability.

**Primary Key:**
- `PRIMARY KEY (participant_id)`

**Columns:**

| Column | Type | Null | Default | Notes |
|---|---|---:|---|---|
| `participant_id` | `CHAR(36)` | NO | `` |  |
| `visit_instance_id` | `CHAR(36)` | NO | `` |  |
| `user_id` | `CHAR(36)` | NO | `` |  |
| `participant_role` | `ENUM('IC_HOST','IC_SUPPORT','DEPT_SUPPORT','STUDENT_BUDDY','MEDIA','INTERPRETER','OTHER')` | NO | `'OTHER'` | Enum: `IC_HOST`, `IC_SUPPORT`, `DEPT_SUPPORT`, `STUDENT_BUDDY`, `MEDIA`, `INTERPRETER`, `OTHER` |
| `is_host` | `BOOLEAN` | NO | `FALSE` |  |
| `status` | `ENUM('INVITED','ACCEPTED','DECLINED','ASSIGNED','REMOVED')` | NO | `'INVITED'` | Enum: `INVITED`, `ACCEPTED`, `DECLINED`, `ASSIGNED`, `REMOVED` |
| `invited_by` | `CHAR(36)` | YES | `` |  |
| `invited_at` | `DATETIME` | YES | `` |  |
| `responded_at` | `DATETIME` | YES | `` |  |
| `assigned_by` | `CHAR(36)` | YES | `` |  |
| `assigned_at` | `DATETIME` | YES | `` |  |
| `note` | `TEXT` | YES | `` |  |
| `created_at` | `DATETIME` | NO | `CURRENT_TIMESTAMP` |  |
| `created_by` | `CHAR(36)` | YES | `` |  |
| `updated_at` | `DATETIME` | YES | `NULL` |  |
| `updated_by` | `CHAR(36)` | YES | `` |  |

**Unique Constraints:**
- `UNIQUE KEY uq_visit_participants_user (visit_instance_id, user_id)`

**Indexes:**
- `KEY idx_visit_participants_one_host_lookup (visit_instance_id, is_host)`
- `KEY idx_visit_participants_user_status (user_id, status)`
- `KEY idx_visit_participants_instance (visit_instance_id)`
- `KEY idx_visit_participants_role_status (participant_role, status)`

**Foreign Keys:**

| Constraint | Local Column(s) | References | Actions |
|---|---|---|---|
| `fk_visit_participants_instance` | `visit_instance_id` | `visit_request_campuses(visit_instance_id)` | ON UPDATE CASCADE ON DELETE RESTRICT |
| `fk_visit_participants_user` | `user_id` | `users(user_id)` | ON UPDATE CASCADE ON DELETE RESTRICT |
| `fk_visit_participants_invited_by` | `invited_by` | `users(user_id)` | ON UPDATE CASCADE ON DELETE SET NULL |
| `fk_visit_participants_assigned_by` | `assigned_by` | `users(user_id)` | ON UPDATE CASCADE ON DELETE SET NULL |


### `visit_agendas`

**Purpose:** Lịch trình tiếp khách

**Primary Key:**
- `PRIMARY KEY (agenda_id)`

**Columns:**

| Column | Type | Null | Default | Notes |
|---|---|---:|---|---|
| `agenda_id` | `CHAR(36)` | NO | `` |  |
| `visit_instance_id` | `CHAR(36)` | NO | `` |  |
| `sequence_order` | `INT UNSIGNED` | NO | `` |  |
| `title` | `VARCHAR(255)` | NO | `` |  |
| `description` | `TEXT` | YES | `` |  |
| `start_time` | `DATETIME` | NO | `` |  |
| `end_time` | `DATETIME` | YES | `` |  |
| `location` | `VARCHAR(255)` | YES | `` |  |
| `responsible_user_id` | `CHAR(36)` | YES | `` |  |
| `created_at` | `DATETIME` | NO | `CURRENT_TIMESTAMP` |  |
| `created_by` | `CHAR(36)` | YES | `` |  |
| `updated_at` | `DATETIME` | YES | `NULL` |  |
| `updated_by` | `CHAR(36)` | YES | `` |  |

**Unique Constraints:**
- `UNIQUE KEY uq_visit_agendas_order (visit_instance_id, sequence_order)`

**Indexes:**
- `KEY idx_visit_agendas_time (visit_instance_id, start_time)`
- `KEY idx_visit_agendas_responsible (responsible_user_id, start_time)`

**Foreign Keys:**

| Constraint | Local Column(s) | References | Actions |
|---|---|---|---|
| `fk_visit_agendas_instance` | `visit_instance_id` | `visit_request_campuses(visit_instance_id)` | ON UPDATE CASCADE ON DELETE RESTRICT |
| `fk_visit_agendas_responsible_user` | `responsible_user_id` | `users(user_id)` | ON UPDATE CASCADE ON DELETE SET NULL |


### `visit_logistics_items`

**Purpose:** Yêu cầu hậu cần/resource cho visit: gửi yêu cầu, đề xuất thay đổi, tiếp nhận, phân công, xác nhận và hoàn thành. Thay thế tasks cho logistics/resource.

**Primary Key:**
- `PRIMARY KEY (logistics_item_id)`

**Columns:**

| Column | Type | Null | Default | Notes |
|---|---|---:|---|---|
| `logistics_item_id` | `CHAR(36)` | NO | `` |  |
| `visit_instance_id` | `CHAR(36)` | NO | `` |  |
| `item_type` | `ENUM('ROOM','TRANSPORT','MEAL','EQUIPMENT','BANNER','LED','OTHER')` | NO | `` | Enum: `ROOM`, `TRANSPORT`, `MEAL`, `EQUIPMENT`, `BANNER`, `LED`, `OTHER` |
| `title` | `VARCHAR(255)` | NO | `` |  |
| `description` | `TEXT` | YES | `` | Nội dung chi tiết công việc gốc |
| `quantity` | `INT UNSIGNED` | YES | `` | Số lượng yêu cầu gốc |
| `usage_start_at` | `DATETIME` | YES | `` | Thời gian bắt đầu sử dụng resource |
| `usage_end_at` | `DATETIME` | YES | `` | Thời gian kết thúc sử dụng resource |
| `status` | `ENUM( 'PLANNED', 'REQUESTED', 'CHANGE_PROPOSED', 'RECEIVED', 'ASSIGNED', 'ACCEPTED', 'IN_PROGRESS', 'READY', 'DONE', 'REJECTED', 'CANCELLED' )` | NO | `'PLANNED'` | Enum: `PLANNED`, `REQUESTED`, `CHANGE_PROPOSED`, `RECEIVED`, `ASSIGNED`, `ACCEPTED`, `IN_PROGRESS`, `READY`, `DONE`, `REJECTED`, `CANCELLED` |
| `priority` | `ENUM('LOW','MEDIUM','HIGH','URGENT')` | NO | `'MEDIUM'` | Enum: `LOW`, `MEDIUM`, `HIGH`, `URGENT` |
| `requested_by` | `CHAR(36)` | YES | `` | Người gửi yêu cầu hậu cần/resource |
| `requested_to_department_id` | `CHAR(36)` | YES | `` | Phòng ban được yêu cầu xử lý |
| `requested_at` | `DATETIME` | YES | `` | Thời điểm gửi yêu cầu |
| `received_by` | `CHAR(36)` | YES | `` | Trưởng phòng/người tiếp nhận yêu cầu |
| `received_at` | `DATETIME` | YES | `` | Thời điểm tiếp nhận yêu cầu |
| `assigned_to_user_id` | `CHAR(36)` | YES | `` | Nhân viên được giao xử lý chính |
| `assigned_by` | `CHAR(36)` | YES | `` | Người phân công |
| `assigned_at` | `DATETIME` | YES | `` | Thời điểm phân công |
| `assignee_accepted_at` | `DATETIME` | YES | `` | Thời điểm nhân viên xác nhận nhận nhiệm vụ |
| `assignee_response_note` | `TEXT` | YES | `` | Ghi chú khi nhân viên nhận/từ chối nếu có |
| `due_at` | `DATETIME` | YES | `` | Deadline hoàn thành hạng mục |
| `completed_at` | `DATETIME` | YES | `` | Thời điểm hoàn thành |
| `proposed_by` | `CHAR(36)` | YES | `` | Người gửi đề xuất thay đổi |
| `proposed_at` | `DATETIME` | YES | `` | Thời điểm gửi đề xuất thay đổi |
| `proposed_quantity` | `INT UNSIGNED` | YES | `` | Số lượng được đề xuất thay đổi |
| `proposed_usage_start_at` | `DATETIME` | YES | `` | Thời gian bắt đầu sử dụng được đề xuất |
| `proposed_usage_end_at` | `DATETIME` | YES | `` | Thời gian kết thúc sử dụng được đề xuất |
| `proposed_description` | `TEXT` | YES | `` | Nội dung chi tiết công việc được đề xuất thay đổi |
| `proposal_note` | `TEXT` | YES | `` | Lý do/ghi chú đề xuất thay đổi |
| `proposal_responded_by` | `CHAR(36)` | YES | `` | Người xác nhận/từ chối đề xuất |
| `proposal_responded_at` | `DATETIME` | YES | `` | Thời điểm xác nhận/từ chối đề xuất |
| `proposal_response` | `ENUM('ACCEPTED','REJECTED')` | YES | `` | Kết quả phản hồi đề xuất<br>Enum: `ACCEPTED`, `REJECTED` |
| `proposal_response_note` | `TEXT` | YES | `` | Ghi chú phản hồi đề xuất |
| `decision_note` | `TEXT` | YES | `` | Lý do reject/cancel hoặc ghi chú xử lý |
| `row_version` | `INT UNSIGNED` | NO | `0` | Optimistic concurrency token |
| `created_at` | `DATETIME` | NO | `CURRENT_TIMESTAMP` |  |
| `created_by` | `CHAR(36)` | YES | `` |  |
| `updated_at` | `DATETIME` | YES | `NULL` |  |
| `updated_by` | `CHAR(36)` | YES | `` |  |

**Unique Constraints:**
_None._

**Indexes:**
- `KEY idx_logistics_instance_status (visit_instance_id, status)`
- `KEY idx_logistics_item_status (item_type, status)`
- `KEY idx_logistics_department_status (requested_to_department_id, status)`
- `KEY idx_logistics_assignee_status (assigned_to_user_id, status)`
- `KEY idx_logistics_requested_by_time (requested_by, requested_at)`
- `KEY idx_logistics_received_by_time (received_by, received_at)`
- `KEY idx_logistics_usage_time (usage_start_at, usage_end_at)`
- `KEY idx_logistics_due (due_at)`
- `KEY idx_logistics_priority_due (priority, due_at)`
- `KEY idx_logistics_proposed_by_time (proposed_by, proposed_at)`

**Foreign Keys:**

| Constraint | Local Column(s) | References | Actions |
|---|---|---|---|
| `fk_logistics_instance` | `visit_instance_id` | `visit_request_campuses(visit_instance_id)` | ON UPDATE CASCADE ON DELETE RESTRICT |
| `fk_logistics_requested_by` | `requested_by` | `users(user_id)` | ON UPDATE CASCADE ON DELETE SET NULL |
| `fk_logistics_requested_to_department` | `requested_to_department_id` | `departments(department_id)` | ON UPDATE CASCADE ON DELETE SET NULL |
| `fk_logistics_received_by` | `received_by` | `users(user_id)` | ON UPDATE CASCADE ON DELETE SET NULL |
| `fk_logistics_assigned_to` | `assigned_to_user_id` | `users(user_id)` | ON UPDATE CASCADE ON DELETE SET NULL |
| `fk_logistics_assigned_by` | `assigned_by` | `users(user_id)` | ON UPDATE CASCADE ON DELETE SET NULL |
| `fk_logistics_proposed_by` | `proposed_by` | `users(user_id)` | ON UPDATE CASCADE ON DELETE SET NULL |
| `fk_logistics_proposal_responded_by` | `proposal_responded_by` | `users(user_id)` | ON UPDATE CASCADE ON DELETE SET NULL |

**Check Constraints:**
- `CHECK (quantity IS NULL OR quantity >= 1)`
- `CHECK (usage_end_at IS NULL OR usage_start_at IS NULL OR usage_end_at > usage_start_at)`
- `CHECK (proposed_quantity IS NULL OR proposed_quantity >= 1)`
- `CHECK (proposed_usage_end_at IS NULL OR proposed_usage_start_at IS NULL OR proposed_usage_end_at > proposed_usage_start_at)`

**Implementation Notes:**
- Uses `row_version` as an optimistic concurrency token.


## 5.6. Minutes & Feedback

### `minutes`

**Purpose:** Biên bản chuyến thăm. Không lưu file đính kèm và không lưu action item dạng JSON; action item tách bảng riêng.

**Primary Key:**
- `PRIMARY KEY (minutes_id)`

**Columns:**

| Column | Type | Null | Default | Notes |
|---|---|---:|---|---|
| `minutes_id` | `CHAR(36)` | NO | `` |  |
| `visit_instance_id` | `CHAR(36)` | NO | `` |  |
| `title` | `VARCHAR(255)` | NO | `` |  |
| `content` | `LONGTEXT` | YES | `` |  |
| `participants_json` | `JSON` | YES | `` | Danh sách người tham gia trong biên bản, lưu dạng snapshot nếu cần hiển thị lại |
| `status` | `ENUM('DRAFT','FINAL')` | NO | `'DRAFT'` | DRAFT=đang soạn, FINAL=đã chốt<br>Enum: `DRAFT`, `FINAL` |
| `finalized_by` | `CHAR(36)` | YES | `` | Người chốt biên bản |
| `finalized_at` | `DATETIME` | YES | `` | Thời điểm chốt biên bản |
| `created_at` | `DATETIME` | NO | `CURRENT_TIMESTAMP` |  |
| `created_by` | `CHAR(36)` | YES | `` |  |
| `updated_at` | `DATETIME` | YES | `NULL` |  |
| `updated_by` | `CHAR(36)` | YES | `` |  |

**Unique Constraints:**
_None._

**Indexes:**
- `KEY idx_minutes_visit_status (visit_instance_id, status)`
- `KEY idx_minutes_created_by_time (created_by, created_at)`
- `KEY idx_minutes_finalized_by_time (finalized_by, finalized_at)`
- `FULLTEXT KEY ft_minutes_search (title, content)`

**Foreign Keys:**

| Constraint | Local Column(s) | References | Actions |
|---|---|---|---|
| `fk_minutes_visit_instance` | `visit_instance_id` | `visit_request_campuses(visit_instance_id)` | ON UPDATE CASCADE ON DELETE RESTRICT |
| `fk_minutes_created_by` | `created_by` | `users(user_id)` | ON UPDATE CASCADE ON DELETE SET NULL |
| `fk_minutes_updated_by` | `updated_by` | `users(user_id)` | ON UPDATE CASCADE ON DELETE SET NULL |
| `fk_minutes_finalized_by` | `finalized_by` | `users(user_id)` | ON UPDATE CASCADE ON DELETE SET NULL |

**Implementation Notes:**
- Contains JSON column(s); keep API DTO validation strict because DB does not enforce JSON shape.


### `minute_action_items`

**Purpose:** Các đầu việc sau biên bản. Không gán người phụ trách; chỉ có note, deadline và trạng thái hoàn thành.

**Primary Key:**
- `PRIMARY KEY (action_item_id)`

**Columns:**

| Column | Type | Null | Default | Notes |
|---|---|---:|---|---|
| `action_item_id` | `CHAR(36)` | NO | `` |  |
| `minutes_id` | `CHAR(36)` | NO | `` |  |
| `title` | `VARCHAR(255)` | NO | `` | Tên đầu việc |
| `note` | `TEXT` | YES | `` | Ghi chú thêm cho đầu việc |
| `due_date` | `DATE` | YES | `` | Deadline của đầu việc |
| `status` | `ENUM('TODO','IN_PROGRESS','DONE','CANCELLED')` | NO | `'TODO'` | TODO=chưa làm, IN_PROGRESS=đang làm, DONE=hoàn thành, CANCELLED=đã hủy/không cần làm nữa<br>Enum: `TODO`, `IN_PROGRESS`, `DONE`, `CANCELLED` |
| `completed_at` | `DATETIME` | YES | `` | Thời điểm hoàn thành; backend tự set khi status chuyển sang DONE |
| `display_order` | `INT UNSIGNED` | NO | `1` | Thứ tự hiển thị trong biên bản |
| `created_at` | `DATETIME` | NO | `CURRENT_TIMESTAMP` |  |
| `created_by` | `CHAR(36)` | YES | `` |  |
| `updated_at` | `DATETIME` | YES | `NULL` |  |
| `updated_by` | `CHAR(36)` | YES | `` |  |

**Unique Constraints:**
_None._

**Indexes:**
- `KEY idx_action_items_minutes (minutes_id)`
- `KEY idx_action_items_status_due (status, due_date)`
- `KEY idx_action_items_order (minutes_id, display_order)`
- `KEY idx_action_items_created_by_time (created_by, created_at)`

**Foreign Keys:**

| Constraint | Local Column(s) | References | Actions |
|---|---|---|---|
| `fk_action_items_minutes` | `minutes_id` | `minutes(minutes_id)` | ON UPDATE CASCADE ON DELETE CASCADE |
| `fk_action_items_created_by` | `created_by` | `users(user_id)` | ON UPDATE CASCADE ON DELETE SET NULL |
| `fk_action_items_updated_by` | `updated_by` | `users(user_id)` | ON UPDATE CASCADE ON DELETE SET NULL |


### `feedbacks`

**Purpose:** Feedback đơn giản: mỗi dòng là một đánh giá giữa hai user trong một visit. Khách/logistics đánh giá host; host đánh giá khách hoặc logistics.

**Primary Key:**
- `PRIMARY KEY (feedback_id)`

**Columns:**

| Column | Type | Null | Default | Notes |
|---|---|---:|---|---|
| `feedback_id` | `CHAR(36)` | NO | `` |  |
| `visit_request_id` | `CHAR(36)` | NO | `` |  |
| `visit_instance_id` | `CHAR(36)` | YES | `` |  |
| `submitted_by_user_id` | `CHAR(36)` | NO | `` | User gửi feedback; khách/host/logistics đều phải có tài khoản hệ thống |
| `submitter_role` | `ENUM('VISITOR','HOST','LOGISTICS')` | NO | `` | Vai trò người gửi trong chuyến thăm<br>Enum: `VISITOR`, `HOST`, `LOGISTICS` |
| `submitter_context` | `VARCHAR(120)` | NO | `''` | Ngữ cảnh vai trò người gửi, ví dụ: Host chính, Xe điện, Teabreak, Khách đại diện |
| `submitter_name_snapshot` | `VARCHAR(255)` | NO | `` | Tên người gửi tại thời điểm gửi feedback |
| `target_user_id` | `CHAR(36)` | NO | `` | User được đánh giá |
| `target_role` | `ENUM('VISITOR','HOST','LOGISTICS')` | NO | `` | Vai trò người được đánh giá trong chuyến thăm<br>Enum: `VISITOR`, `HOST`, `LOGISTICS` |
| `target_context` | `VARCHAR(120)` | NO | `''` | Ngữ cảnh đối tượng được đánh giá, ví dụ: Host chính, Đoàn khách, Xe điện, Teabreak |
| `target_name_snapshot` | `VARCHAR(255)` | NO | `` | Tên người được đánh giá tại thời điểm gửi feedback |
| `rating` | `TINYINT UNSIGNED` | NO | `` | Số sao từ 1 đến 5 |
| `comment` | `TEXT` | NO | `` | Nội dung feedback |
| `submitted_at` | `DATETIME` | NO | `CURRENT_TIMESTAMP` |  |

**Unique Constraints:**
_None._

**Indexes:**
- `KEY idx_feedbacks_visit_request (visit_request_id)`
- `KEY idx_feedbacks_visit_instance (visit_instance_id)`
- `KEY idx_feedbacks_submitter (submitted_by_user_id)`
- `KEY idx_feedbacks_target (target_user_id)`
- `KEY idx_feedbacks_roles (submitter_role, target_role)`
- `KEY idx_feedbacks_rating (rating)`
- `KEY idx_feedbacks_submitted_at (submitted_at)`

**Foreign Keys:**

| Constraint | Local Column(s) | References | Actions |
|---|---|---|---|
| `fk_feedbacks_visit_request` | `visit_request_id` | `visit_requests(visit_request_id)` | ON UPDATE CASCADE ON DELETE RESTRICT |
| `fk_feedbacks_visit_instance` | `visit_instance_id` | `visit_request_campuses(visit_instance_id)` | ON UPDATE CASCADE ON DELETE SET NULL |
| `fk_feedbacks_submitter` | `submitted_by_user_id` | `users(user_id)` | ON UPDATE CASCADE ON DELETE RESTRICT |
| `fk_feedbacks_target` | `target_user_id` | `users(user_id)` | ON UPDATE CASCADE ON DELETE RESTRICT |

**Check Constraints:**
- `CONSTRAINT chk_feedbacks_rating CHECK (rating BETWEEN 1 AND 5)`
- `CONSTRAINT chk_feedbacks_role_flow CHECK ( (submitter_role IN ('VISITOR','LOGISTICS') AND target_role = 'HOST') OR (submitter_role = 'HOST' AND target_role IN ('VISITOR','LOGISTICS')) )`

**Implementation Notes:**
- `submitted_by_user_id <> target_user_id` is enforced by triggers `trg_feedbacks_not_self_bi` and `trg_feedbacks_not_self_bu`, not by CHECK, to avoid MySQL FK/CHECK restriction with referential actions.


## 5.7. News & FAQ

### `news`

**Purpose:** News metadata. Người tham gia gửi bài, host duyệt/từ chối; nội dung chia theo section.

**Primary Key:**
- `PRIMARY KEY (news_id)`

**Columns:**

| Column | Type | Null | Default | Notes |
|---|---|---:|---|---|
| `news_id` | `CHAR(36)` | NO | `` |  |
| `campus_id` | `CHAR(36)` | YES | `` | Campus liên quan đến bài viết. NULL nếu bài toàn hệ thống |
| `visit_instance_id` | `CHAR(36)` | YES | `` | Visit instance liên quan nếu bài viết được tạo từ một chuyến tiếp đón |
| `author_user_id` | `CHAR(36)` | NO | `` | Người tạo/viết bài |
| `cover_file_id` | `CHAR(36)` | YES | `` | Ảnh bìa bài viết, trỏ tới files.file_id |
| `status` | `ENUM('PENDING_REVIEW','REJECTED','PUBLISHED','HIDDEN')` | NO | `'PENDING_REVIEW'` | PENDING_REVIEW=chờ host duyệt, REJECTED=bị từ chối, PUBLISHED=đã đăng, HIDDEN=ẩn khỏi trang tin<br>Enum: `PENDING_REVIEW`, `REJECTED`, `PUBLISHED`, `HIDDEN` |
| `submitted_at` | `DATETIME` | NO | `CURRENT_TIMESTAMP` | Thời điểm người viết gửi bài cho host duyệt |
| `reviewed_by` | `CHAR(36)` | YES | `` | Host duyệt hoặc từ chối bài viết |
| `reviewed_at` | `DATETIME` | YES | `` | Thời điểm host duyệt hoặc từ chối |
| `review_note` | `TEXT` | YES | `` | Ghi chú duyệt hoặc lý do từ chối |
| `published_at` | `DATETIME` | YES | `` | Thời điểm bài viết được đăng |
| `is_featured` | `BOOLEAN` | NO | `FALSE` | Bài viết nổi bật |
| `row_version` | `INT UNSIGNED` | NO | `0` | Optimistic concurrency token, chống ghi đè khi cập nhật đồng thời |
| `created_at` | `DATETIME` | NO | `CURRENT_TIMESTAMP` |  |
| `created_by` | `CHAR(36)` | YES | `` |  |
| `updated_at` | `DATETIME` | YES | `NULL` |  |
| `updated_by` | `CHAR(36)` | YES | `` |  |

**Unique Constraints:**
_None._

**Indexes:**
- `KEY idx_news_public (status, campus_id, published_at)`
- `KEY idx_news_author_status (author_user_id, status)`
- `KEY idx_news_visit_instance_status (visit_instance_id, status)`
- `KEY idx_news_review (reviewed_by, reviewed_at)`
- `KEY idx_news_featured (is_featured, status, published_at)`

**Foreign Keys:**

| Constraint | Local Column(s) | References | Actions |
|---|---|---|---|
| `fk_news_campus` | `campus_id` | `campuses(campus_id)` | ON UPDATE CASCADE ON DELETE SET NULL |
| `fk_news_visit_instance` | `visit_instance_id` | `visit_request_campuses(visit_instance_id)` | ON UPDATE CASCADE ON DELETE SET NULL |
| `fk_news_author` | `author_user_id` | `users(user_id)` | ON UPDATE CASCADE ON DELETE RESTRICT |
| `fk_news_cover_file` | `cover_file_id` | `files(file_id)` | ON UPDATE CASCADE ON DELETE SET NULL |
| `fk_news_reviewed_by` | `reviewed_by` | `users(user_id)` | ON UPDATE CASCADE ON DELETE SET NULL |

**Implementation Notes:**
- Uses `row_version` as an optimistic concurrency token.


### `news_translations`

**Purpose:** Tiêu đề, slug, tóm tắt và SEO của bài viết theo ngôn ngữ

**Primary Key:**
- `PRIMARY KEY (news_translation_id)`

**Columns:**

| Column | Type | Null | Default | Notes |
|---|---|---:|---|---|
| `news_translation_id` | `CHAR(36)` | NO | `` |  |
| `news_id` | `CHAR(36)` | NO | `` |  |
| `language_code` | `ENUM('vi','en','zh','ja','ko')` | NO | `'vi'` | Enum: `vi`, `en`, `zh`, `ja`, `ko` |
| `title` | `VARCHAR(255)` | NO | `` | Tiêu đề chính của bài viết |
| `slug` | `VARCHAR(255)` | NO | `` | Đường dẫn SEO của bài viết |
| `summary` | `TEXT` | YES | `` | Tóm tắt bài viết |
| `seo_title` | `VARCHAR(255)` | YES | `` |  |
| `seo_description` | `VARCHAR(500)` | YES | `` |  |
| `created_at` | `DATETIME` | NO | `CURRENT_TIMESTAMP` |  |
| `updated_at` | `DATETIME` | YES | `NULL` |  |

**Unique Constraints:**
- `UNIQUE KEY uq_news_translation_lang (news_id, language_code)`
- `UNIQUE KEY uq_news_translation_slug_lang (slug, language_code)`

**Indexes:**
- `KEY idx_news_translations_lang (language_code)`
- `FULLTEXT KEY ft_news_translations_search (title, summary)`

**Foreign Keys:**

| Constraint | Local Column(s) | References | Actions |
|---|---|---|---|
| `fk_news_translations_news` | `news_id` | `news(news_id)` | ON UPDATE CASCADE ON DELETE CASCADE |


### `news_content_sections`

**Purpose:** Các khối nội dung chi tiết của bài viết, tối đa 10 section mỗi bản dịch

**Primary Key:**
- `PRIMARY KEY (section_id)`

**Columns:**

| Column | Type | Null | Default | Notes |
|---|---|---:|---|---|
| `section_id` | `CHAR(36)` | NO | `` |  |
| `news_translation_id` | `CHAR(36)` | NO | `` |  |
| `section_order` | `TINYINT UNSIGNED` | NO | `` | Thứ tự section, từ 1 đến 10 |
| `section_title` | `VARCHAR(255)` | NO | `` | Tiêu đề section |
| `section_body_html` | `LONGTEXT` | NO | `` | Nội dung rich text dạng HTML đã sanitize, có thể chứa paragraph, bold, italic, color, link, image |
| `section_body_text` | `TEXT` | YES | `` | Plain text tách từ HTML để search hoặc preview |
| `created_at` | `DATETIME` | NO | `CURRENT_TIMESTAMP` |  |
| `updated_at` | `DATETIME` | YES | `NULL` |  |

**Unique Constraints:**
- `UNIQUE KEY uq_news_section_order (news_translation_id, section_order)`

**Indexes:**
- `KEY idx_news_sections_translation (news_translation_id)`
- `FULLTEXT KEY ft_news_sections_search (section_title, section_body_text)`

**Foreign Keys:**

| Constraint | Local Column(s) | References | Actions |
|---|---|---|---|
| `fk_news_sections_translation` | `news_translation_id` | `news_translations(news_translation_id)` | ON UPDATE CASCADE ON DELETE CASCADE |

**Check Constraints:**
- `CHECK (section_order BETWEEN 1 AND 10)`


### `news_section_files`

**Purpose:** File/ảnh được dùng trong từng section của bài news

**Primary Key:**
- `PRIMARY KEY (section_file_id)`

**Columns:**

| Column | Type | Null | Default | Notes |
|---|---|---:|---|---|
| `section_file_id` | `CHAR(36)` | NO | `` |  |
| `section_id` | `CHAR(36)` | NO | `` |  |
| `file_id` | `CHAR(36)` | NO | `` |  |
| `usage_type` | `ENUM('INLINE_IMAGE','ATTACHMENT')` | NO | `'INLINE_IMAGE'` | INLINE_IMAGE=ảnh chèn trong nội dung, ATTACHMENT=file đính kèm<br>Enum: `INLINE_IMAGE`, `ATTACHMENT` |
| `display_order` | `INT UNSIGNED` | NO | `0` |  |
| `created_at` | `DATETIME` | NO | `CURRENT_TIMESTAMP` |  |

**Unique Constraints:**
- `UNIQUE KEY uq_news_section_file (section_id, file_id)`

**Indexes:**
- `KEY idx_news_section_files_section (section_id)`
- `KEY idx_news_section_files_file (file_id)`

**Foreign Keys:**

| Constraint | Local Column(s) | References | Actions |
|---|---|---|---|
| `fk_news_section_files_section` | `section_id` | `news_content_sections(section_id)` | ON UPDATE CASCADE ON DELETE CASCADE |
| `fk_news_section_files_file` | `file_id` | `files(file_id)` | ON UPDATE CASCADE ON DELETE RESTRICT |


### `faqs`

**Purpose:** FAQ một ngôn ngữ, chỉ dùng PUBLISHED/HIDDEN

**Primary Key:**
- `PRIMARY KEY (faq_id)`

**Columns:**

| Column | Type | Null | Default | Notes |
|---|---|---:|---|---|
| `faq_id` | `CHAR(36)` | NO | `` |  |
| `category` | `VARCHAR(100)` | YES | `` | Nhóm FAQ, ví dụ: Visit Request, Security, Logistics |
| `question` | `VARCHAR(500)` | NO | `` | Câu hỏi FAQ |
| `answer` | `TEXT` | NO | `` | Câu trả lời FAQ |
| `display_order` | `INT UNSIGNED` | NO | `0` |  |
| `status` | `ENUM('PUBLISHED','HIDDEN')` | NO | `'HIDDEN'` | PUBLISHED=hiển thị trên trang FAQ, HIDDEN=ẩn khỏi người xem thường nhưng người quản lý vẫn thấy<br>Enum: `PUBLISHED`, `HIDDEN` |
| `created_at` | `DATETIME` | NO | `CURRENT_TIMESTAMP` |  |
| `created_by` | `CHAR(36)` | YES | `` |  |
| `updated_at` | `DATETIME` | YES | `NULL` |  |
| `updated_by` | `CHAR(36)` | YES | `` |  |

**Unique Constraints:**
_None._

**Indexes:**
- `KEY idx_faqs_status_order (status, display_order)`
- `KEY idx_faqs_category_status (category, status)`
- `FULLTEXT KEY ft_faqs_search (question, answer)`

**Foreign Keys:**

_None._


## 5.8. Gallery & Face Tagging

### `galleries`

**Purpose:** Gallery địa điểm trong campus, có mô tả và câu chuyện

**Primary Key:**
- `PRIMARY KEY (gallery_id)`

**Columns:**

| Column | Type | Null | Default | Notes |
|---|---|---:|---|---|
| `gallery_id` | `CHAR(36)` | NO | `` |  |
| `campus_id` | `CHAR(36)` | NO | `` |  |
| `location_name` | `VARCHAR(150)` | NO | `` | Tên địa điểm trong campus, ví dụ: Sảnh Alpha, Green Lab, Thư viện |
| `title` | `VARCHAR(255)` | NO | `` | Tên hiển thị của gallery/địa điểm |
| `description` | `TEXT` | YES | `` | Mô tả ngắn về địa điểm |
| `story_content` | `TEXT` | YES | `` | Ý nghĩa hoặc câu chuyện giới thiệu về địa điểm |
| `status` | `ENUM('DRAFT','PUBLISHED','HIDDEN')` | NO | `'DRAFT'` | DRAFT=nháp, PUBLISHED=hiển thị theo visibility, HIDDEN=ẩn khỏi người xem thường nhưng Staff Leader vẫn quản lý được<br>Enum: `DRAFT`, `PUBLISHED`, `HIDDEN` |
| `visibility` | `ENUM('PRIVATE','INTERNAL','PUBLIC')` | NO | `'INTERNAL'` | Phạm vi xem khi status=PUBLISHED: PRIVATE=chỉ quản lý, INTERNAL=user nội bộ, PUBLIC=công khai<br>Enum: `PRIVATE`, `INTERNAL`, `PUBLIC` |
| `created_at` | `DATETIME` | NO | `CURRENT_TIMESTAMP` |  |
| `created_by` | `CHAR(36)` | YES | `` |  |
| `updated_at` | `DATETIME` | YES | `NULL` |  |
| `updated_by` | `CHAR(36)` | YES | `` |  |
| `deleted_at` | `DATETIME` | YES | `` |  |
| `deleted_by` | `CHAR(36)` | YES | `` |  |

**Unique Constraints:**
_None._

**Indexes:**
- `KEY idx_galleries_campus_status (campus_id, status, deleted_at)`
- `KEY idx_galleries_location_name (location_name)`
- `KEY idx_galleries_visibility_status (visibility, status)`

**Foreign Keys:**

| Constraint | Local Column(s) | References | Actions |
|---|---|---|---|
| `fk_galleries_campus` | `campus_id` | `campuses(campus_id)` | ON UPDATE CASCADE ON DELETE RESTRICT |

**Implementation Notes:**
- Supports UC-based soft delete via `deleted_at/deleted_by`.


### `gallery_images`

**Purpose:** Ảnh thuộc gallery địa điểm campus

**Primary Key:**
- `PRIMARY KEY (image_id)`

**Columns:**

| Column | Type | Null | Default | Notes |
|---|---|---:|---|---|
| `image_id` | `CHAR(36)` | NO | `` |  |
| `gallery_id` | `CHAR(36)` | NO | `` |  |
| `file_id` | `CHAR(36)` | NO | `` |  |
| `caption` | `VARCHAR(500)` | YES | `` | Chú thích riêng cho từng ảnh |
| `display_order` | `INT UNSIGNED` | NO | `0` |  |
| `taken_at` | `DATETIME` | YES | `` |  |
| `status` | `ENUM('ACTIVE','HIDDEN')` | NO | `'ACTIVE'` | ACTIVE=ảnh đang dùng, HIDDEN=ảnh bị ẩn khỏi gallery thường<br>Enum: `ACTIVE`, `HIDDEN` |
| `created_at` | `DATETIME` | NO | `CURRENT_TIMESTAMP` |  |
| `created_by` | `CHAR(36)` | YES | `` |  |
| `updated_at` | `DATETIME` | YES | `NULL` |  |
| `updated_by` | `CHAR(36)` | YES | `` |  |
| `deleted_at` | `DATETIME` | YES | `` |  |
| `deleted_by` | `CHAR(36)` | YES | `` |  |

**Unique Constraints:**
- `UNIQUE KEY uq_gallery_images_file (file_id)`

**Indexes:**
- `KEY idx_gallery_images_gallery_order (gallery_id, display_order)`
- `KEY idx_gallery_images_status_time (status, taken_at)`

**Foreign Keys:**

| Constraint | Local Column(s) | References | Actions |
|---|---|---|---|
| `fk_gallery_images_gallery` | `gallery_id` | `galleries(gallery_id)` | ON UPDATE CASCADE ON DELETE RESTRICT |
| `fk_gallery_images_file` | `file_id` | `files(file_id)` | ON UPDATE CASCADE ON DELETE RESTRICT |

**Implementation Notes:**
- Supports UC-based soft delete via `deleted_at/deleted_by`.


### `photo_face_tags`

**Purpose:** Confirmed face tag metadata only. No biometric vector.

**Primary Key:**
- `PRIMARY KEY (face_tag_id)`

**Columns:**

| Column | Type | Null | Default | Notes |
|---|---|---:|---|---|
| `face_tag_id` | `CHAR(36)` | NO | `` |  |
| `image_id` | `CHAR(36)` | NO | `` |  |
| `visit_request_id` | `CHAR(36)` | YES | `` |  |
| `guest_member_id` | `CHAR(36)` | YES | `` |  |
| `partner_contact_id` | `CHAR(36)` | YES | `` |  |
| `display_name` | `VARCHAR(150)` | NO | `` |  |
| `bounding_box_x` | `DECIMAL(8,4)` | YES | `` |  |
| `bounding_box_y` | `DECIMAL(8,4)` | YES | `` |  |
| `bounding_box_width` | `DECIMAL(8,4)` | YES | `` |  |
| `bounding_box_height` | `DECIMAL(8,4)` | YES | `` |  |
| `tag_status` | `ENUM('MANUALLY_TAGGED','CONFIRMED','REMOVED')` | NO | `'MANUALLY_TAGGED'` | Enum: `MANUALLY_TAGGED`, `CONFIRMED`, `REMOVED` |
| `confirmed_by` | `CHAR(36)` | YES | `` |  |
| `confirmed_at` | `DATETIME` | YES | `` |  |
| `created_at` | `DATETIME` | NO | `CURRENT_TIMESTAMP` |  |
| `created_by` | `CHAR(36)` | YES | `` |  |
| `removed_at` | `DATETIME` | YES | `` |  |
| `removed_by` | `CHAR(36)` | YES | `` |  |

**Unique Constraints:**
_None._

**Indexes:**
- `KEY idx_face_tags_image (image_id)`
- `KEY idx_face_tags_guest (guest_member_id)`
- `KEY idx_face_tags_partner_contact (partner_contact_id)`
- `KEY idx_face_tags_status (tag_status)`

**Foreign Keys:**

| Constraint | Local Column(s) | References | Actions |
|---|---|---|---|
| `fk_face_tags_image` | `image_id` | `gallery_images(image_id)` | ON UPDATE CASCADE ON DELETE CASCADE |
| `fk_face_tags_visit_request` | `visit_request_id` | `visit_requests(visit_request_id)` | ON UPDATE CASCADE ON DELETE SET NULL |
| `fk_face_tags_guest` | `guest_member_id` | `visit_guest_members(guest_member_id)` | ON UPDATE CASCADE ON DELETE SET NULL |
| `fk_face_tags_partner_contact` | `partner_contact_id` | `partner_contacts(contact_id)` | ON UPDATE CASCADE ON DELETE SET NULL |
| `fk_face_tags_confirmed_by` | `confirmed_by` | `users(user_id)` | ON UPDATE CASCADE ON DELETE SET NULL |


## 5.9. Email & Notification

### `email_templates`

**Purpose:** Email templates with translations_json

**Primary Key:**
- `PRIMARY KEY (email_template_id)`

**Columns:**

| Column | Type | Null | Default | Notes |
|---|---|---:|---|---|
| `email_template_id` | `CHAR(36)` | NO | `` |  |
| `template_code` | `VARCHAR(100)` | NO | `` |  |
| `name` | `VARCHAR(150)` | NO | `` |  |
| `purpose` | `VARCHAR(100)` | NO | `` |  |
| `status` | `ENUM('ACTIVE','INACTIVE')` | NO | `'ACTIVE'` | Enum: `ACTIVE`, `INACTIVE` |
| `translations_json` | `JSON` | NO | `` | Merged email_template_translations table |
| `variables_json` | `JSON` | YES | `` | Allowed variables: FullName, OtpCode, Link... |
| `created_at` | `DATETIME` | NO | `CURRENT_TIMESTAMP` |  |
| `created_by` | `CHAR(36)` | YES | `` |  |
| `updated_at` | `DATETIME` | YES | `NULL` |  |
| `updated_by` | `CHAR(36)` | YES | `` |  |

**Unique Constraints:**
- `UNIQUE KEY uq_email_templates_code (template_code)`

**Indexes:**
- `KEY idx_email_templates_status (status)`
- `KEY idx_email_templates_purpose_status (purpose, status)`

**Foreign Keys:**

_None._

**Implementation Notes:**
- Contains JSON column(s); keep API DTO validation strict because DB does not enforce JSON shape.


### `sent_emails`

**Purpose:** Sent email log with recipients_json

**Primary Key:**
- `PRIMARY KEY (sent_email_id)`

**Columns:**

| Column | Type | Null | Default | Notes |
|---|---|---:|---|---|
| `sent_email_id` | `CHAR(36)` | NO | `` |  |
| `email_template_id` | `CHAR(36)` | YES | `` |  |
| `related_type` | `VARCHAR(80)` | YES | `` |  |
| `related_id` | `CHAR(36)` | YES | `` |  |
| `subject` | `VARCHAR(255)` | NO | `` |  |
| `body_snapshot` | `LONGTEXT` | YES | `` |  |
| `recipients_json` | `JSON` | NO | `` | Merged sent_email_recipients table |
| `metadata_json` | `JSON` | YES | `` | provider message id, retry count, etc. |
| `status` | `ENUM('QUEUED','SENT','FAILED')` | NO | `'QUEUED'` | Enum: `QUEUED`, `SENT`, `FAILED` |
| `error_message` | `TEXT` | YES | `` |  |
| `sent_by` | `CHAR(36)` | YES | `` |  |
| `sent_at` | `DATETIME` | YES | `` |  |
| `created_at` | `DATETIME` | NO | `CURRENT_TIMESTAMP` |  |

**Unique Constraints:**
_None._

**Indexes:**
- `KEY idx_sent_emails_template (email_template_id)`
- `KEY idx_sent_emails_related (related_type, related_id)`
- `KEY idx_sent_emails_status_time (status, created_at)`
- `KEY idx_sent_emails_sent_by_time (sent_by, sent_at)`

**Foreign Keys:**

| Constraint | Local Column(s) | References | Actions |
|---|---|---|---|
| `fk_sent_emails_template` | `email_template_id` | `email_templates(email_template_id)` | ON UPDATE CASCADE ON DELETE SET NULL |
| `fk_sent_emails_sent_by` | `sent_by` | `users(user_id)` | ON UPDATE CASCADE ON DELETE SET NULL |

**Implementation Notes:**
- Contains JSON column(s); keep API DTO validation strict because DB does not enforce JSON shape.


### `notifications`

**Purpose:** In-app notifications

**Primary Key:**
- `PRIMARY KEY (notification_id)`

**Columns:**

| Column | Type | Null | Default | Notes |
|---|---|---:|---|---|
| `notification_id` | `CHAR(36)` | NO | `` |  |
| `recipient_user_id` | `CHAR(36)` | NO | `` |  |
| `title` | `VARCHAR(255)` | NO | `` |  |
| `message` | `TEXT` | YES | `` |  |
| `notification_type` | `VARCHAR(80)` | NO | `` |  |
| `related_type` | `VARCHAR(80)` | YES | `` |  |
| `related_id` | `CHAR(36)` | YES | `` |  |
| `is_read` | `BOOLEAN` | NO | `FALSE` |  |
| `read_at` | `DATETIME` | YES | `` |  |
| `created_at` | `DATETIME` | NO | `CURRENT_TIMESTAMP` |  |

**Unique Constraints:**
_None._

**Indexes:**
- `KEY idx_notifications_user_read_time (recipient_user_id, is_read, created_at)`
- `KEY idx_notifications_related (related_type, related_id)`
- `KEY idx_notifications_type_time (notification_type, created_at)`

**Foreign Keys:**

| Constraint | Local Column(s) | References | Actions |
|---|---|---|---|
| `fk_notifications_user` | `recipient_user_id` | `users(user_id)` | ON UPDATE CASCADE ON DELETE CASCADE |


## 5.10. Calendar / API / Agenda Template

### `calendar_events`

**Purpose:** Calendar events. Personal/visit/logistics/deadline events. Attendees/reminders merged into JSON fields.

**Primary Key:**
- `PRIMARY KEY (calendar_event_id)`

**Columns:**

| Column | Type | Null | Default | Notes |
|---|---|---:|---|---|
| `calendar_event_id` | `CHAR(36)` | NO | `` |  |
| `owner_user_id` | `CHAR(36)` | NO | `` |  |
| `campus_id` | `CHAR(36)` | YES | `` |  |
| `visit_instance_id` | `CHAR(36)` | YES | `` |  |
| `logistics_item_id` | `CHAR(36)` | YES | `` |  |
| `source_type` | `ENUM('PERSONAL','VISIT','LOGISTICS','DEADLINE')` | NO | `'PERSONAL'` | Enum: `PERSONAL`, `VISIT`, `LOGISTICS`, `DEADLINE` |
| `title` | `VARCHAR(255)` | NO | `` |  |
| `description` | `TEXT` | YES | `` |  |
| `location` | `VARCHAR(255)` | YES | `` |  |
| `start_at` | `DATETIME` | NO | `` |  |
| `end_at` | `DATETIME` | NO | `` |  |
| `timezone` | `VARCHAR(50)` | NO | `'Asia/Ho_Chi_Minh'` |  |
| `visibility` | `ENUM('PRIVATE','INTERNAL')` | NO | `'PRIVATE'` | Enum: `PRIVATE`, `INTERNAL` |
| `attendees_json` | `JSON` | YES | `` | Merged calendar_event_attendees table |
| `reminders_json` | `JSON` | YES | `` | Merged calendar_event_reminders table |
| `status` | `ENUM('ACTIVE','CANCELLED','DONE')` | NO | `'ACTIVE'` | Enum: `ACTIVE`, `CANCELLED`, `DONE` |
| `created_at` | `DATETIME` | NO | `CURRENT_TIMESTAMP` |  |
| `created_by` | `CHAR(36)` | YES | `` |  |
| `updated_at` | `DATETIME` | YES | `NULL` |  |
| `updated_by` | `CHAR(36)` | YES | `` |  |
| `deleted_at` | `DATETIME` | YES | `` |  |
| `deleted_by` | `CHAR(36)` | YES | `` |  |

**Unique Constraints:**
_None._

**Indexes:**
- `KEY idx_calendar_owner_time (owner_user_id, start_at)`
- `KEY idx_calendar_campus_time (campus_id, start_at)`
- `KEY idx_calendar_visit (visit_instance_id)`
- `KEY idx_calendar_logistics (logistics_item_id)`
- `KEY idx_calendar_source_status_time (source_type, status, start_at)`

**Foreign Keys:**

| Constraint | Local Column(s) | References | Actions |
|---|---|---|---|
| `fk_calendar_owner` | `owner_user_id` | `users(user_id)` | ON UPDATE CASCADE ON DELETE CASCADE |
| `fk_calendar_campus` | `campus_id` | `campuses(campus_id)` | ON UPDATE CASCADE ON DELETE SET NULL |
| `fk_calendar_visit` | `visit_instance_id` | `visit_request_campuses(visit_instance_id)` | ON UPDATE CASCADE ON DELETE SET NULL |
| `fk_calendar_logistics` | `logistics_item_id` | `visit_logistics_items(logistics_item_id)` | ON UPDATE CASCADE ON DELETE SET NULL |

**Check Constraints:**
- `CHECK (end_at > start_at)`

**Implementation Notes:**
- Supports UC-based soft delete via `deleted_at/deleted_by`.
- Contains JSON column(s); keep API DTO validation strict because DB does not enforce JSON shape.


### `api_configurations`

**Purpose:** API config + encrypted credentials JSON

**Primary Key:**
- `PRIMARY KEY (api_config_id)`

**Columns:**

| Column | Type | Null | Default | Notes |
|---|---|---:|---|---|
| `api_config_id` | `CHAR(36)` | NO | `` |  |
| `api_code` | `VARCHAR(100)` | NO | `` |  |
| `name` | `VARCHAR(150)` | NO | `` |  |
| `provider_name` | `VARCHAR(150)` | YES | `` |  |
| `purpose` | `VARCHAR(150)` | YES | `` |  |
| `base_url` | `VARCHAR(500)` | NO | `` |  |
| `default_method` | `ENUM('GET','POST','PUT','PATCH','DELETE')` | NO | `'POST'` | Enum: `GET`, `POST`, `PUT`, `PATCH`, `DELETE` |
| `auth_type` | `ENUM('NONE','API_KEY','BEARER_TOKEN','BASIC','OAUTH2','CUSTOM')` | NO | `'NONE'` | Enum: `NONE`, `API_KEY`, `BEARER_TOKEN`, `BASIC`, `OAUTH2`, `CUSTOM` |
| `credentials_json` | `JSON` | YES | `` | Encrypted/masked credentials. Merged api_credentials table. |
| `headers_json` | `JSON` | YES | `` |  |
| `body_template_json` | `JSON` | YES | `` |  |
| `settings_json` | `JSON` | YES | `` |  |
| `timeout_seconds` | `INT UNSIGNED` | NO | `30` |  |
| `status` | `ENUM('ACTIVE','INACTIVE','DISABLED')` | NO | `'ACTIVE'` | Enum: `ACTIVE`, `INACTIVE`, `DISABLED` |
| `created_at` | `DATETIME` | NO | `CURRENT_TIMESTAMP` |  |
| `created_by` | `CHAR(36)` | YES | `` |  |
| `updated_at` | `DATETIME` | YES | `NULL` |  |
| `updated_by` | `CHAR(36)` | YES | `` |  |
| `deleted_at` | `DATETIME` | YES | `` |  |
| `deleted_by` | `CHAR(36)` | YES | `` |  |

**Unique Constraints:**
- `UNIQUE KEY uq_api_config_code (api_code)`

**Indexes:**
- `KEY idx_api_config_status (status)`
- `KEY idx_api_provider_status (provider_name, status)`

**Foreign Keys:**

_None._

**Implementation Notes:**
- Supports UC-based soft delete via `deleted_at/deleted_by`.
- Contains JSON column(s); keep API DTO validation strict because DB does not enforce JSON shape.


### `api_usage_quotas`

**Purpose:** API quota + counter per campus/month

**Primary Key:**
- `PRIMARY KEY (api_usage_quota_id)`

**Columns:**

| Column | Type | Null | Default | Notes |
|---|---|---:|---|---|
| `api_usage_quota_id` | `CHAR(36)` | NO | `` |  |
| `api_config_id` | `CHAR(36)` | NO | `` |  |
| `campus_id` | `CHAR(36)` | YES | `` | NULL = global quota |
| `campus_scope_key` | `VARCHAR(36)` | NO | `'GLOBAL'` |  |
| `period_yyyymm` | `CHAR(6)` | NO | `` | YYYYMM |
| `monthly_limit` | `INT UNSIGNED` | NO | `` |  |
| `used_count` | `INT UNSIGNED` | NO | `0` | Merged api_usage_counters table |
| `last_used_at` | `DATETIME` | YES | `` |  |
| `created_at` | `DATETIME` | NO | `CURRENT_TIMESTAMP` |  |
| `created_by` | `CHAR(36)` | YES | `` |  |
| `updated_at` | `DATETIME` | YES | `NULL` |  |
| `updated_by` | `CHAR(36)` | YES | `` |  |

**Unique Constraints:**
- `UNIQUE KEY uq_api_quota_config_scope_period (api_config_id, campus_scope_key, period_yyyymm)`

**Indexes:**
- `KEY idx_api_quota_campus_period (campus_id, period_yyyymm)`
- `KEY idx_api_quota_period (period_yyyymm)`

**Foreign Keys:**

| Constraint | Local Column(s) | References | Actions |
|---|---|---|---|
| `fk_api_quota_config` | `api_config_id` | `api_configurations(api_config_id)` | ON UPDATE CASCADE ON DELETE CASCADE |
| `fk_api_quota_campus` | `campus_id` | `campuses(campus_id)` | ON UPDATE CASCADE ON DELETE CASCADE |


### `api_request_logs`

**Purpose:** External API request logs. Never log full secret/token.

**Primary Key:**
- `PRIMARY KEY (api_request_log_id)`

**Columns:**

| Column | Type | Null | Default | Notes |
|---|---|---:|---|---|
| `api_request_log_id` | `BIGINT UNSIGNED` | NO | `` |  |
| `api_config_id` | `CHAR(36)` | NO | `` |  |
| `campus_id` | `CHAR(36)` | YES | `` |  |
| `requested_by` | `CHAR(36)` | YES | `` |  |
| `related_type` | `VARCHAR(80)` | YES | `` |  |
| `related_id` | `CHAR(36)` | YES | `` |  |
| `endpoint` | `VARCHAR(500)` | NO | `` |  |
| `method` | `ENUM('GET','POST','PUT','PATCH','DELETE')` | NO | `` | Enum: `GET`, `POST`, `PUT`, `PATCH`, `DELETE` |
| `http_status` | `INT` | YES | `` |  |
| `response_time_ms` | `INT UNSIGNED` | YES | `` |  |
| `request_size_bytes` | `BIGINT UNSIGNED` | YES | `` |  |
| `response_size_bytes` | `BIGINT UNSIGNED` | YES | `` |  |
| `success` | `BOOLEAN` | NO | `FALSE` |  |
| `error_code` | `VARCHAR(100)` | YES | `` |  |
| `error_message` | `TEXT` | YES | `` |  |
| `created_at` | `DATETIME` | NO | `CURRENT_TIMESTAMP` |  |

**Unique Constraints:**
_None._

**Indexes:**
- `KEY idx_api_logs_config_time (api_config_id, created_at)`
- `KEY idx_api_logs_campus_time (campus_id, created_at)`
- `KEY idx_api_logs_user_time (requested_by, created_at)`
- `KEY idx_api_logs_success_time (success, created_at)`
- `KEY idx_api_logs_related (related_type, related_id)`

**Foreign Keys:**

| Constraint | Local Column(s) | References | Actions |
|---|---|---|---|
| `fk_api_logs_config` | `api_config_id` | `api_configurations(api_config_id)` | ON UPDATE CASCADE ON DELETE RESTRICT |
| `fk_api_logs_campus` | `campus_id` | `campuses(campus_id)` | ON UPDATE CASCADE ON DELETE SET NULL |
| `fk_api_logs_user` | `requested_by` | `users(user_id)` | ON UPDATE CASCADE ON DELETE SET NULL |


### `agenda_templates`

**Purpose:** Agenda template with items_json

**Primary Key:**
- `PRIMARY KEY (agenda_template_id)`

**Columns:**

| Column | Type | Null | Default | Notes |
|---|---|---:|---|---|
| `agenda_template_id` | `CHAR(36)` | NO | `` |  |
| `campus_id` | `CHAR(36)` | YES | `` |  |
| `campus_scope_key` | `VARCHAR(36)` | NO | `'GLOBAL'` |  |
| `name` | `VARCHAR(150)` | NO | `` |  |
| `description` | `TEXT` | YES | `` |  |
| `items_json` | `JSON` | NO | `` | Merged agenda_template_items table |
| `status` | `ENUM('ACTIVE','INACTIVE')` | NO | `'ACTIVE'` | Enum: `ACTIVE`, `INACTIVE` |
| `created_at` | `DATETIME` | NO | `CURRENT_TIMESTAMP` |  |
| `created_by` | `CHAR(36)` | YES | `` |  |
| `updated_at` | `DATETIME` | YES | `NULL` |  |
| `updated_by` | `CHAR(36)` | YES | `` |  |
| `deleted_at` | `DATETIME` | YES | `` |  |
| `deleted_by` | `CHAR(36)` | YES | `` |  |

**Unique Constraints:**
- `UNIQUE KEY uq_agenda_template_scope_name (campus_scope_key, name)`

**Indexes:**
- `KEY idx_agenda_templates_status (status)`
- `KEY idx_agenda_templates_campus_status (campus_id, status)`

**Foreign Keys:**

| Constraint | Local Column(s) | References | Actions |
|---|---|---|---|
| `fk_agenda_templates_campus` | `campus_id` | `campuses(campus_id)` | ON UPDATE CASCADE ON DELETE SET NULL |

**Implementation Notes:**
- Supports UC-based soft delete via `deleted_at/deleted_by`.
- Contains JSON column(s); keep API DTO validation strict because DB does not enforce JSON shape.


## 5.11. Audit

### `audit_logs`

**Purpose:** General audit log

**Primary Key:**
- `PRIMARY KEY (audit_log_id)`

**Columns:**

| Column | Type | Null | Default | Notes |
|---|---|---:|---|---|
| `audit_log_id` | `BIGINT UNSIGNED` | NO | `` |  |
| `actor_user_id` | `CHAR(36)` | YES | `` |  |
| `campus_id` | `CHAR(36)` | YES | `` |  |
| `action` | `VARCHAR(100)` | NO | `` |  |
| `entity_type` | `VARCHAR(100)` | NO | `` |  |
| `entity_id` | `CHAR(36)` | YES | `` |  |
| `old_values_json` | `JSON` | YES | `` |  |
| `new_values_json` | `JSON` | YES | `` |  |
| `ip_address` | `VARCHAR(45)` | YES | `` |  |
| `user_agent` | `VARCHAR(500)` | YES | `` |  |
| `request_id` | `VARCHAR(100)` | YES | `` |  |
| `created_at` | `DATETIME` | NO | `CURRENT_TIMESTAMP` |  |

**Unique Constraints:**
_None._

**Indexes:**
- `KEY idx_audit_actor_time (actor_user_id, created_at)`
- `KEY idx_audit_entity (entity_type, entity_id)`
- `KEY idx_audit_action_time (action, created_at)`
- `KEY idx_audit_campus_time (campus_id, created_at)`
- `KEY idx_audit_request (request_id)`

**Foreign Keys:**

| Constraint | Local Column(s) | References | Actions |
|---|---|---|---|
| `fk_audit_actor` | `actor_user_id` | `users(user_id)` | ON UPDATE CASCADE ON DELETE SET NULL |
| `fk_audit_campus` | `campus_id` | `campuses(campus_id)` | ON UPDATE CASCADE ON DELETE SET NULL |

**Implementation Notes:**
- Contains JSON column(s); keep API DTO validation strict because DB does not enforce JSON shape.


### `visit_status_logs`

**Purpose:** Timeline trạng thái visit

**Primary Key:**
- `PRIMARY KEY (visit_status_log_id)`

**Columns:**

| Column | Type | Null | Default | Notes |
|---|---|---:|---|---|
| `visit_status_log_id` | `BIGINT UNSIGNED` | NO | `` |  |
| `visit_request_id` | `CHAR(36)` | YES | `` |  |
| `visit_instance_id` | `CHAR(36)` | YES | `` |  |
| `old_status` | `VARCHAR(50)` | YES | `` |  |
| `new_status` | `VARCHAR(50)` | NO | `` |  |
| `changed_by` | `CHAR(36)` | YES | `` |  |
| `reason` | `TEXT` | YES | `` |  |
| `changed_at` | `DATETIME` | NO | `CURRENT_TIMESTAMP` |  |

**Unique Constraints:**
_None._

**Indexes:**
- `KEY idx_visit_status_request_time (visit_request_id, changed_at)`
- `KEY idx_visit_status_instance_time (visit_instance_id, changed_at)`
- `KEY idx_visit_status_changed_by_time (changed_by, changed_at)`

**Foreign Keys:**

| Constraint | Local Column(s) | References | Actions |
|---|---|---|---|
| `fk_visit_status_logs_request` | `visit_request_id` | `visit_requests(visit_request_id)` | ON UPDATE CASCADE ON DELETE SET NULL |
| `fk_visit_status_logs_instance` | `visit_instance_id` | `visit_request_campuses(visit_instance_id)` | ON UPDATE CASCADE ON DELETE SET NULL |
| `fk_visit_status_logs_changed_by` | `changed_by` | `users(user_id)` | ON UPDATE CASCADE ON DELETE SET NULL |


---

## 6. JSON Columns

| Table | Column | Purpose / Notes |
|---|---|---|
| `security_events` | `metadata` | JSON data/configuration field |
| `visit_requests` | `support_team_json` | Danh sách team hỗ trợ khách từ phía đoàn/đơn vị gửi |
| `visit_requests` | `contact_person_json` | Thông tin đầu mối liên hệ: full_name, organization, phone, email |
| `minutes` | `participants_json` | Danh sách người tham gia trong biên bản, lưu dạng snapshot nếu cần hiển thị lại |
| `email_templates` | `translations_json` | Merged email_template_translations table |
| `email_templates` | `variables_json` | Allowed variables: FullName, OtpCode, Link... |
| `sent_emails` | `recipients_json` | Merged sent_email_recipients table |
| `sent_emails` | `metadata_json` | provider message id, retry count, etc. |
| `calendar_events` | `attendees_json` | Merged calendar_event_attendees table |
| `calendar_events` | `reminders_json` | Merged calendar_event_reminders table |
| `api_configurations` | `credentials_json` | Encrypted/masked credentials. Merged api_credentials table. |
| `api_configurations` | `headers_json` | JSON data/configuration field |
| `api_configurations` | `body_template_json` | JSON data/configuration field |
| `api_configurations` | `settings_json` | JSON data/configuration field |
| `agenda_templates` | `items_json` | Merged agenda_template_items table |
| `audit_logs` | `old_values_json` | JSON data/configuration field |
| `audit_logs` | `new_values_json` | JSON data/configuration field |

---

## 7. Enum Fields

| Table | Column | Values | Notes |
|---|---|---|---|
| `roles` | `status` | `ACTIVE`, `INACTIVE` |  |
| `role_permissions` | `sub_role` | `NONE`, `Leader`, `Staff` | NONE for ADMIN/HO/STUDENT/VISITOR; Leader/Staff for STAFF and DEPT |
| `role_permissions` | `permission_level` | `F`, `E`, `R`, `O` | F=Full, E=Execute/Edit, R=Read, O=Own |
| `campuses` | `status` | `ACTIVE`, `INACTIVE` |  |
| `departments` | `department_type` | `IC`, `GENERAL` | IC=International Cooperation; GENERAL=other departments |
| `departments` | `status` | `ACTIVE`, `INACTIVE` |  |
| `users` | `sub_role` | `Leader`, `Staff` | Only for STAFF/DEPT |
| `users` | `gender` | `MALE`, `FEMALE`, `OTHER`, `UNKNOWN` |  |
| `users` | `status` | `ACTIVE`, `INACTIVE`, `LOCKED` | ACTIVE=hoạt động, INACTIVE=tạm ngưng, LOCKED=bị khóa |
| `users` | `created_via` | `MANUAL_CREATED`, `VISITOR_FORM` | MANUAL_CREATED=HO/Staff Leader tạo, VISITOR_FORM=tạo từ form visitor |
| `user_auth_providers` | `provider_type` | `LOCAL_PASSWORD`, `GOOGLE_SSO`, `FEID` |  |
| `user_sessions` | `login_portal` | `VISITOR`, `INTERNAL` |  |
| `otp_tokens` | `token_type` | `OTP_CODE`, `MAGIC_LINK` |  |
| `otp_tokens` | `purpose` | `VISIT_REQUEST_VERIFY`, `CHANGE_SENSITIVE_ACTION` |  |
| `login_logs` | `login_portal` | `VISITOR`, `INTERNAL` |  |
| `login_logs` | `provider_type` | `LOCAL_PASSWORD`, `GOOGLE_SSO`, `FEID` |  |
| `login_logs` | `status` | `SUCCESS`, `FAILED`, `BLOCKED` |  |
| `security_events` | `severity` | `LOW`, `MEDIUM`, `HIGH`, `CRITICAL` |  |
| `partners` | `partner_type` | `UNIVERSITY`, `COMPANY`, `GOVERNMENT`, `NGO`, `OTHER` |  |
| `partners` | `cooperation_status` | `POTENTIAL`, `ACTIVE`, `INACTIVE`, `BLACKLISTED` |  |
| `partner_contacts` | `status` | `ACTIVE`, `INACTIVE` |  |
| `files` | `storage_provider` | `LOCAL`, `S3`, `AZURE`, `GCS`, `GOOGLE_DRIVE`, `OTHER` |  |
| `files` | `visibility` | `PRIVATE`, `INTERNAL`, `PUBLIC` |  |
| `documents` | `owner_type` | `GENERAL`, `VISIT`, `PARTNER`, `MINUTES`, `NEWS`, `LOGISTICS`, `REPORT` |  |
| `documents` | `status` | `DRAFT`, `PUBLISHED`, `ARCHIVED` |  |
| `visit_requests` | `visit_scope` | `SINGLE_CAMPUS`, `MULTI_CAMPUS` | SINGLE_CAMPUS: Staff Leader duyệt request tổng; MULTI_CAMPUS: HO duyệt request tổng. Frontend/backend suy ra người duyệt từ cột này. |
| `visit_requests` | `working_language` | `VI`, `EN`, `OTHER` | Ngôn ngữ sử dụng trong visit |
| `visit_requests` | `status` | `PENDING_APPROVAL`, `REJECTED`, `APPROVED`, `CANCELLED` | Request row exists only after OTP/email verification. |
| `visit_requests` | `decision_actor_role` | `HO`, `STAFF_LEADER`, `SYSTEM` | Vai trò người xử lý tại thời điểm quyết định |
| `visit_request_campuses` | `status` | `WAITING_REQUEST_APPROVAL`, `ASSIGNED`, `BEFORE_VISIT`, `DURING_VISIT`, `AFTER_VISIT`, `CLOSED`, `CANCELLED` |  |
| `visit_participants` | `participant_role` | `IC_HOST`, `IC_SUPPORT`, `DEPT_SUPPORT`, `STUDENT_BUDDY`, `MEDIA`, `INTERPRETER`, `OTHER` |  |
| `visit_participants` | `status` | `INVITED`, `ACCEPTED`, `DECLINED`, `ASSIGNED`, `REMOVED` |  |
| `visit_logistics_items` | `item_type` | `ROOM`, `TRANSPORT`, `MEAL`, `EQUIPMENT`, `BANNER`, `LED`, `OTHER` |  |
| `visit_logistics_items` | `status` | `PLANNED`, `REQUESTED`, `CHANGE_PROPOSED`, `RECEIVED`, `ASSIGNED`, `ACCEPTED`, `IN_PROGRESS`, `READY`, `DONE`, `REJECTED`, `CANCELLED` |  |
| `visit_logistics_items` | `priority` | `LOW`, `MEDIUM`, `HIGH`, `URGENT` |  |
| `visit_logistics_items` | `proposal_response` | `ACCEPTED`, `REJECTED` | Kết quả phản hồi đề xuất |
| `minutes` | `status` | `DRAFT`, `FINAL` | DRAFT=đang soạn, FINAL=đã chốt |
| `minute_action_items` | `status` | `TODO`, `IN_PROGRESS`, `DONE`, `CANCELLED` | TODO=chưa làm, IN_PROGRESS=đang làm, DONE=hoàn thành, CANCELLED=đã hủy/không cần làm nữa |
| `feedbacks` | `submitter_role` | `VISITOR`, `HOST`, `LOGISTICS` | Vai trò người gửi trong chuyến thăm |
| `feedbacks` | `target_role` | `VISITOR`, `HOST`, `LOGISTICS` | Vai trò người được đánh giá trong chuyến thăm |
| `news` | `status` | `PENDING_REVIEW`, `REJECTED`, `PUBLISHED`, `HIDDEN` | PENDING_REVIEW=chờ host duyệt, REJECTED=bị từ chối, PUBLISHED=đã đăng, HIDDEN=ẩn khỏi trang tin |
| `news_translations` | `language_code` | `vi`, `en`, `zh`, `ja`, `ko` |  |
| `news_section_files` | `usage_type` | `INLINE_IMAGE`, `ATTACHMENT` | INLINE_IMAGE=ảnh chèn trong nội dung, ATTACHMENT=file đính kèm |
| `faqs` | `status` | `PUBLISHED`, `HIDDEN` | PUBLISHED=hiển thị trên trang FAQ, HIDDEN=ẩn khỏi người xem thường nhưng người quản lý vẫn thấy |
| `galleries` | `status` | `DRAFT`, `PUBLISHED`, `HIDDEN` | DRAFT=nháp, PUBLISHED=hiển thị theo visibility, HIDDEN=ẩn khỏi người xem thường nhưng Staff Leader vẫn quản lý được |
| `galleries` | `visibility` | `PRIVATE`, `INTERNAL`, `PUBLIC` | Phạm vi xem khi status=PUBLISHED: PRIVATE=chỉ quản lý, INTERNAL=user nội bộ, PUBLIC=công khai |
| `gallery_images` | `status` | `ACTIVE`, `HIDDEN` | ACTIVE=ảnh đang dùng, HIDDEN=ảnh bị ẩn khỏi gallery thường |
| `photo_face_tags` | `tag_status` | `MANUALLY_TAGGED`, `CONFIRMED`, `REMOVED` |  |
| `email_templates` | `status` | `ACTIVE`, `INACTIVE` |  |
| `sent_emails` | `status` | `QUEUED`, `SENT`, `FAILED` |  |
| `calendar_events` | `source_type` | `PERSONAL`, `VISIT`, `LOGISTICS`, `DEADLINE` |  |
| `calendar_events` | `visibility` | `PRIVATE`, `INTERNAL` |  |
| `calendar_events` | `status` | `ACTIVE`, `CANCELLED`, `DONE` |  |
| `api_configurations` | `default_method` | `GET`, `POST`, `PUT`, `PATCH`, `DELETE` |  |
| `api_configurations` | `auth_type` | `NONE`, `API_KEY`, `BEARER_TOKEN`, `BASIC`, `OAUTH2`, `CUSTOM` |  |
| `api_configurations` | `status` | `ACTIVE`, `INACTIVE`, `DISABLED` |  |
| `api_request_logs` | `method` | `GET`, `POST`, `PUT`, `PATCH`, `DELETE` |  |
| `agenda_templates` | `status` | `ACTIVE`, `INACTIVE` |  |

---

## 8. Soft Delete Strategy

Soft delete is intentionally limited to tables with explicit Delete/Remove UC. Tables with soft delete fields:

- `roles`
- `galleries`
- `gallery_images`
- `calendar_events`
- `api_configurations`
- `agenda_templates`

**Important:** do not add soft delete fields to every table by default. Follow UC requirements only.

---

## 9. Trigger-Based Validation

| Trigger | Timing | Table | Rule Summary |
|---|---|---|---|
| `trg_departments_one_ic_bi` | BEFORE INSERT | `departments` | Each campus can have only one active IC department |
| `trg_departments_one_ic_bu` | BEFORE UPDATE | `departments` | Each campus can have only one active IC department |
| `trg_users_validate_bi` | BEFORE INSERT | `users` | Invalid role_id; VISITOR must not have sub_role; VISITOR must not have department_id; VISITOR must not have primary_campus_id; STAFF/DEPT must have sub_role; STAFF/DEPT must have department_id; STAFF must belong to IC department; DEPT must belong to GENERAL department; ... (+4 more rules) |
| `trg_users_validate_bu` | BEFORE UPDATE | `users` | Invalid role_id; VISITOR must not have sub_role; VISITOR must not have department_id; VISITOR must not have primary_campus_id; STAFF/DEPT must have sub_role; STAFF/DEPT must have department_id; STAFF must belong to IC department; DEPT must belong to GENERAL department; ... (+4 more rules) |
| `trg_auth_providers_validate_bi` | BEFORE INSERT | `user_auth_providers` | SSO/FEID provider_subject is required |
| `trg_auth_providers_validate_bu` | BEFORE UPDATE | `user_auth_providers` | SSO/FEID provider_subject is required |
| `trg_sessions_validate_bi` | BEFORE INSERT | `user_sessions` | Only VISITOR can login via Visitor Portal; Visitor Portal must not have selected_campus_id; VISITOR cannot login via Internal Portal; Internal user must have primary_campus_id; Internal user can only login to their own primary campus |
| `trg_visit_requests_decision_validate_bi` | BEFORE INSERT | `visit_requests` | decision_actor_role is required when visit request is decided; decided_by is required for non-system visit request decision; Only STAFF_LEADER can decide SINGLE_CAMPUS request; Only HO can decide MULTI_CAMPUS request; decision_actor_role HO requires decided_by user with HO role; decision_actor_role STAFF_LEADER requires STAFF Leader user |
| `trg_visit_requests_decision_validate_bu` | BEFORE UPDATE | `visit_requests` | decision_actor_role is required when visit request is decided; decided_by is required for non-system visit request decision; Only STAFF_LEADER can decide SINGLE_CAMPUS request; Only HO can decide MULTI_CAMPUS request; decision_actor_role HO requires decided_by user with HO role; decision_actor_role STAFF_LEADER requires STAFF Leader user |
| `trg_visit_campuses_assignment_validate_bi` | BEFORE INSERT | `visit_request_campuses` | WAITING_REQUEST_APPROVAL campus instance must not have current_host_user_id yet; Campus instance can move to operational status only after main visit request is APPROVED; current_host_user_id is required after main visit request is approved; current_host_user_id must be a STAFF user; current_host_user_id must belong to the same campus instance; host_transferred_by is required when host_transferred_at is set; host_transferred_by must be a STAFF user; host_transferred_by must belong to the same campus instance |
| `trg_visit_campuses_assignment_validate_bu` | BEFORE UPDATE | `visit_request_campuses` | WAITING_REQUEST_APPROVAL campus instance must not have current_host_user_id yet; Campus instance can move to operational status only after main visit request is APPROVED; current_host_user_id is required after main visit request is approved; current_host_user_id must be a STAFF user; current_host_user_id must belong to the same campus instance; host_transferred_by and host_transferred_at are required when transferring host; host_transferred_by is required when host_transferred_at is set; host_transferred_by must be a STAFF user; ... (+1 more rules) |
| `trg_feedbacks_not_self_bi` | BEFORE INSERT | `feedbacks` | Prevent submitter from evaluating themself. |
| `trg_feedbacks_not_self_bu` | BEFORE UPDATE | `feedbacks` | Prevent submitter from evaluating themself. |
| `trg_api_usage_quotas_scope_bi` | BEFORE INSERT | `api_usage_quotas` | Auto-set `campus_scope_key` to campus_id or `GLOBAL` when campus_id is NULL. |
| `trg_api_usage_quotas_scope_bu` | BEFORE UPDATE | `api_usage_quotas` | Auto-set `campus_scope_key` to campus_id or `GLOBAL` when campus_id is NULL. |
| `trg_agenda_templates_scope_bi` | BEFORE INSERT | `agenda_templates` | Auto-set `campus_scope_key` to campus_id or `GLOBAL` when campus_id is NULL. |
| `trg_agenda_templates_scope_bu` | BEFORE UPDATE | `agenda_templates` | Auto-set `campus_scope_key` to campus_id or `GLOBAL` when campus_id is NULL. |

---

## 10. Removed / Merged Tables from Previous Schema

| Old Table / Old Design | New Replacement | Reason |
|---|---|---|
| `user_campuses` | `users.primary_campus_id` | Every non-VISITOR internal user has exactly one primary campus. |
| `tasks / task_actions` | `visit_logistics_items` | Logistics/resource workflow is centralized in visit logistics items. |
| `feedback_items` | `feedbacks.rating/comment` plus one feedback row per target | Feedback is simplified; host clicks each target separately. |
| `action item JSON inside minutes` | `minute_action_items` | Action items need CRUD/status/deadline, so they are separated. |
| `news body-only design` | `news_translations` + `news_content_sections` + `news_section_files` | News supports translations and rich content sections/files. |
| `email_template_translations` | `email_templates.translations_json` | Merged into JSON for template translation payload. |
| `sent_email_recipients` | `sent_emails.recipients_json` | Merged recipients into JSON snapshot. |
| `calendar_event_attendees` | `calendar_events.attendees_json` | Merged attendees into JSON snapshot. |
| `calendar_event_reminders` | `calendar_events.reminders_json` | Merged reminders into JSON snapshot. |
| `api_credentials` | `api_configurations.credentials_json` | Credentials are stored as encrypted/masked JSON metadata. |
| `api_usage_counters` | `api_usage_quotas.used_count` | Counter merged into monthly quota row. |
| `agenda_template_items` | `agenda_templates.items_json` | Template item details are stored as JSON. |
| `gallery_locations` | `gallery_images.location_name` | Location simplified to image-level location name. |
| `partner_documents / reports-specific document tables` | `documents.owner_type` | Generic document owner model replaces multiple specialized document tables. |
| `physical reports table` | No physical table; Reports is read-model/dashboard/export | Statistics are derived from operational tables. |
| `public_contents` | No physical table; public/static copy stays in frontend/config unless future UC adds content management | Removed because current PEMS scope has no public static-content management function. |

---

## 11. Backend Developer Notes

- Do **not** recreate `user_campuses`; use `users.primary_campus_id`.
- Do **not** recreate `tasks` or `task_actions`; use `visit_logistics_items`.
- Do **not** store minutes action items as JSON; use `minute_action_items`.
- Do **not** use guest/logistics submitter ID variants for feedback; feedback submitter and target are system users.
- Do **not** put full rich news content directly into `news`; use translations, sections, and section files.
- Do **not** recreate `public_contents` unless a future UC explicitly adds public/static content management.
- Do **not** create `visit_requests` before OTP/email verification; after successful form submit, use `PENDING_APPROVAL` as the first status.
- Do **not** store binary files in the database; store files externally and keep metadata in `files`.
- Do **not** allow campus instance-level approve/reject after request approval; approval is request-level.
- Reports/dashboard/export should be implemented as read models over operational tables, not as a physical `reports` table unless a future SQL revision adds one.
- For EF Core mapping, pay attention to composite key `role_permissions(role_id, sub_role, permission_id)`.
- For MySQL `CHAR(36)` IDs, keep project-wide ID convention consistent (`string` or `Guid` with explicit conversion).

---

## 12. Table Count Checklist

| # | Table | Module |
|---:|---|---|
| 1 | `roles` | RBAC |
| 2 | `permissions` | RBAC |
| 3 | `role_permissions` | RBAC |
| 4 | `campuses` | Organization |
| 5 | `departments` | Organization |
| 6 | `users` | Users & Authentication |
| 7 | `user_auth_providers` | Users & Authentication |
| 8 | `user_sessions` | Users & Authentication |
| 9 | `otp_tokens` | Users & Authentication |
| 10 | `login_logs` | Users & Authentication |
| 11 | `security_events` | Users & Authentication |
| 12 | `partners` | Partner & File |
| 13 | `partner_contacts` | Partner & File |
| 14 | `files` | Partner & File |
| 15 | `documents` | Partner & File |
| 16 | `visit_requests` | Visit / Delegation |
| 17 | `visit_request_campuses` | Visit / Delegation |
| 18 | `visit_guest_members` | Visit / Delegation |
| 19 | `visit_participants` | Visit / Delegation |
| 20 | `visit_agendas` | Visit / Delegation |
| 21 | `visit_logistics_items` | Visit / Delegation |
| 22 | `minutes` | Minutes & Feedback |
| 23 | `minute_action_items` | Minutes & Feedback |
| 24 | `feedbacks` | Minutes & Feedback |
| 25 | `news` | News & FAQ |
| 26 | `news_translations` | News & FAQ |
| 27 | `news_content_sections` | News & FAQ |
| 28 | `news_section_files` | News & FAQ |
| 29 | `faqs` | News & FAQ |
| 30 | `galleries` | Gallery & Face Tagging |
| 31 | `gallery_images` | Gallery & Face Tagging |
| 32 | `photo_face_tags` | Gallery & Face Tagging |
| 33 | `email_templates` | Email & Notification |
| 34 | `sent_emails` | Email & Notification |
| 35 | `notifications` | Email & Notification |
| 36 | `calendar_events` | Calendar / API / Agenda Template |
| 37 | `api_configurations` | Calendar / API / Agenda Template |
| 38 | `api_usage_quotas` | Calendar / API / Agenda Template |
| 39 | `api_request_logs` | Calendar / API / Agenda Template |
| 40 | `agenda_templates` | Calendar / API / Agenda Template |
| 41 | `audit_logs` | Audit |
| 42 | `visit_status_logs` | Audit |
