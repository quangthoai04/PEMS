# PEMS Database Schema — FULL v8.3 Cancel After Approval
> **Generated from:** `pems_full_sql_42tables_final_v8_3_cancel_after_approval_full_create.sql`  
> **Purpose:** Full developer-facing database schema reference. This is not a delta/patch summary.  
> **Main correction:** `CANCELLED` is only for cancellation **after approval**. Before approval, withdrawal is handled by reject flow (`REJECTED`).
## 1. Overview
| Item | Value |
|---|---|
| Database | `pems_db` |
| Engine | MySQL 8.0 / InnoDB |
| Charset / Collation | `utf8mb4` / `utf8mb4_unicode_ci` |
| Schema Version | `PEMS v8.3 cancel-after-approval full create` |
| Base Table Count | `42` |
| View Count | `6` |
| Trigger Count | `19` |
| Primary Key Strategy | `BIGINT UNSIGNED AUTO_INCREMENT` for base-table PKs |
| Foreign Key Strategy | Matching FK/reference columns use `BIGINT UNSIGNED` without `AUTO_INCREMENT` |
| Visit Request Status | `PENDING_APPROVAL`, `APPROVED`, `REJECTED`, `CANCELLED` only |
| Campus Visit Status | `WAITING_REQUEST_APPROVAL`, `ASSIGNED`, `BEFORE_VISIT`, `DURING_VISIT`, `AFTER_VISIT`, `CLOSED`, `CANCELLED` |
| Cancel Rule | UC-136 cancel applies only after approval; before approval use reject flow |
| External Confirmation Note | Not used; external-confirmation details are stored in `cancellation_reason` |

## 2. Key Business Rules Reflected in Schema
- `visit_requests.status` stores only the request/approval decision state, not operational visit progress.
- `visit_request_campuses.status` stores each campus instance operational status.
- `WAITING_REQUEST_APPROVAL` means the campus was selected but the main request has not been approved yet; it is not a host-waiting state.
- Host is assigned immediately when approval happens: `AUTO_STAFF_LEADER` for multi-campus HO approval; `MANUAL_APPROVAL` for single-campus Staff Leader approval; `TRANSFERRED` after host transfer.
- UC-136 `CANCEL_VISIT_REQUEST` is under Delegation Reception Management. It is used only after `visit_requests.status = APPROVED`.
- If a visitor withdraws before approval, HO/Staff Leader uses reject flow and writes the reason in `decision_note`.
- No `external_confirmation_note` column exists. For host-cancel-after-external-confirmation, write details into `cancellation_reason`.
- `actual_start_at` and `actual_end_at` are not columns in `visit_request_campuses`.

## 3. Module Grouping
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

## 4. Tables
### `roles`

**Purpose:** 6 role chính của hệ thống

**Primary Key:**
- `PRIMARY KEY (role_id)`

**Columns:**

| Column | Type | Null | Default | Auto Inc | Notes |
|---|---|---:|---|---:|---|
| `role_id` | `BIGINT UNSIGNED` | NO | `` | YES |  |
| `role_code` | `VARCHAR(30)` | NO | `` |  | ADMIN, HO, STAFF, DEPT, STUDENT, VISITOR |
| `name` | `VARCHAR(100)` | NO | `` |  |  |
| `description` | `VARCHAR(255)` | YES | `` |  |  |
| `status` | `ENUM('ACTIVE','INACTIVE')` | NO | `'ACTIVE'` |  |  |
| `created_at` | `DATETIME` | NO | `CURRENT_TIMESTAMP` |  |  |
| `deleted_at` | `DATETIME` | YES | `` |  | Soft delete supported by UC-121 Disable/Delete Role |
| `deleted_by` | `BIGINT UNSIGNED` | YES | `` |  | User who soft-deleted this role; no FK here because roles is created before users |

**Unique Constraints:**
- `UNIQUE KEY uq_roles_code (role_code)`

**Indexes:**
- `KEY idx_roles_status_deleted (status, deleted_at)`

**Check Constraints:**
- `CHECK (role_code IN ('ADMIN','HO','STAFF','DEPT','STUDENT','VISITOR'))`

### `permissions`

**Purpose:** Danh mục quyền theo UC/action

**Primary Key:**
- `PRIMARY KEY (permission_id)`

**Columns:**

| Column | Type | Null | Default | Auto Inc | Notes |
|---|---|---:|---|---:|---|
| `permission_id` | `BIGINT UNSIGNED` | NO | `` | YES |  |
| `permission_code` | `VARCHAR(100)` | NO | `` |  | Example: UC-17.SUBMIT_VISIT_REQUEST |
| `name` | `VARCHAR(150)` | NO | `` |  |  |
| `permission_group` | `VARCHAR(60)` | NO | `` |  |  |
| `description` | `VARCHAR(500)` | YES | `` |  |  |
| `is_system` | `BOOLEAN` | NO | `FALSE` |  |  |
| `created_at` | `DATETIME` | NO | `CURRENT_TIMESTAMP` |  |  |

**Unique Constraints:**
- `UNIQUE KEY uq_permissions_code (permission_code)`

**Indexes:**
- `KEY idx_permissions_group (permission_group)`
- `KEY idx_permissions_group_code (permission_group, permission_code)`

### `role_permissions`

**Purpose:** Ma trận phân quyền theo role + sub_role + permission

**Primary Key:**
- `PRIMARY KEY (role_permission_id)`

**Columns:**

| Column | Type | Null | Default | Auto Inc | Notes |
|---|---|---:|---|---:|---|
| `role_permission_id` | `BIGINT UNSIGNED` | NO | `` | YES |  |
| `role_id` | `BIGINT UNSIGNED` | NO | `` |  |  |
| `sub_role` | `ENUM('NONE','Leader','Staff')` | NO | `'NONE'` |  | NONE for ADMIN/HO/STUDENT/VISITOR; Leader/Staff for STAFF and DEPT |
| `permission_id` | `BIGINT UNSIGNED` | NO | `` |  |  |
| `permission_level` | `ENUM('F','E','R','O')` | NO | `` |  | F=Full, E=Execute/Edit, R=Read, O=Own |
| `granted_at` | `DATETIME` | NO | `CURRENT_TIMESTAMP` |  |  |
| `granted_by` | `BIGINT UNSIGNED` | YES | `` |  |  |

**Unique Constraints:**
- `UNIQUE KEY uq_role_permissions_role_sub_permission (role_id, sub_role, permission_id)`

**Indexes:**
- `KEY idx_role_permissions_permission (permission_id)`
- `KEY idx_role_permissions_role_sub_role (role_id, sub_role)`

**Foreign Keys:**
- `CONSTRAINT fk_role_permissions_role FOREIGN KEY (role_id) REFERENCES roles(role_id) ON UPDATE CASCADE ON DELETE CASCADE`
- `CONSTRAINT fk_role_permissions_permission FOREIGN KEY (permission_id) REFERENCES permissions(permission_id) ON UPDATE CASCADE ON DELETE CASCADE`

### `campuses`

**Purpose:** Danh mục campus

**Primary Key:**
- `PRIMARY KEY (campus_id)`

**Columns:**

| Column | Type | Null | Default | Auto Inc | Notes |
|---|---|---:|---|---:|---|
| `campus_id` | `BIGINT UNSIGNED` | NO | `` | YES |  |
| `campus_code` | `VARCHAR(20)` | NO | `` |  | HN, HCM, DN, CT, QN |
| `name` | `VARCHAR(150)` | NO | `` |  |  |
| `city` | `VARCHAR(100)` | YES | `` |  |  |
| `address` | `VARCHAR(255)` | YES | `` |  |  |
| `phone` | `VARCHAR(30)` | YES | `` |  |  |
| `email` | `VARCHAR(150)` | YES | `` |  |  |
| `ic_head_user_id` | `BIGINT UNSIGNED` | YES | `` |  | FK added after users table |
| `status` | `ENUM('ACTIVE','INACTIVE')` | NO | `'ACTIVE'` |  |  |
| `created_at` | `DATETIME` | NO | `CURRENT_TIMESTAMP` |  |  |
| `created_by` | `BIGINT UNSIGNED` | YES | `` |  |  |
| `updated_at` | `DATETIME` | YES | `NULL` |  |  |
| `updated_by` | `BIGINT UNSIGNED` | YES | `` |  |  |

**Unique Constraints:**
- `UNIQUE KEY uq_campuses_code (campus_code)`

**Indexes:**
- `KEY idx_campuses_status (status)`
- `KEY idx_campuses_city_status (city, status)`
- `KEY idx_campuses_ic_head (ic_head_user_id)`

### `departments`

**Purpose:** Phòng ban theo campus. STAFF thuộc IC, DEPT thuộc GENERAL

**Primary Key:**
- `PRIMARY KEY (department_id)`

**Columns:**

| Column | Type | Null | Default | Auto Inc | Notes |
|---|---|---:|---|---:|---|
| `department_id` | `BIGINT UNSIGNED` | NO | `` | YES |  |
| `campus_id` | `BIGINT UNSIGNED` | NO | `` |  |  |
| `department_code` | `VARCHAR(50)` | NO | `` |  |  |
| `name` | `VARCHAR(150)` | NO | `` |  |  |
| `department_type` | `ENUM('IC','GENERAL')` | NO | `` |  | IC=International Cooperation; GENERAL=other departments |
| `head_user_id` | `BIGINT UNSIGNED` | YES | `` |  | FK added after users table |
| `status` | `ENUM('ACTIVE','INACTIVE')` | NO | `'ACTIVE'` |  |  |
| `created_at` | `DATETIME` | NO | `CURRENT_TIMESTAMP` |  |  |
| `created_by` | `BIGINT UNSIGNED` | YES | `` |  |  |
| `updated_at` | `DATETIME` | YES | `NULL` |  |  |
| `updated_by` | `BIGINT UNSIGNED` | YES | `` |  |  |

**Unique Constraints:**
- `UNIQUE KEY uq_departments_campus_code (campus_id, department_code)`
- `UNIQUE KEY uq_departments_campus_name (campus_id, name)`

**Indexes:**
- `KEY idx_departments_campus_type (campus_id, department_type)`
- `KEY idx_departments_status (status)`
- `KEY idx_departments_head (head_user_id)`

**Foreign Keys:**
- `CONSTRAINT fk_departments_campus FOREIGN KEY (campus_id) REFERENCES campuses(campus_id) ON UPDATE CASCADE ON DELETE RESTRICT`

### `users`

**Primary Key:**
- `PRIMARY KEY (user_id)`

**Columns:**

| Column | Type | Null | Default | Auto Inc | Notes |
|---|---|---:|---|---:|---|
| `user_id` | `BIGINT UNSIGNED` | NO | `` | YES |  |
| `full_name` | `VARCHAR(150)` | NO | `` |  |  |
| `email` | `VARCHAR(150)` | NO | `` |  |  |
| `phone` | `VARCHAR(30)` | YES | `` |  |  |
| `nationality` | `VARCHAR(100)` | YES | `` |  | Quốc tịch của user/visitor |
| `password_hash` | `VARCHAR(255)` | YES | `` |  | DEV/local password hash only. Production SSO-only accounts keep this NULL. |
| `role_id` | `BIGINT UNSIGNED` | NO | `` |  |  |
| `sub_role` | `ENUM('Leader','Staff')` | YES | `` |  | Only for STAFF/DEPT |
| `primary_campus_id` | `BIGINT UNSIGNED` | YES | `` |  | Campus duy nhất của user nội bộ. VISITOR phải NULL. |
| `department_id` | `BIGINT UNSIGNED` | YES | `` |  | STAFF = IC department; DEPT = GENERAL department |
| `gender` | `ENUM('MALE','FEMALE','OTHER','UNKNOWN')` | YES | `` |  |  |
| `avatar_url` | `VARCHAR(500)` | YES | `` |  |  |
| `student_code` | `VARCHAR(30)` | YES | `` |  |  |
| `fe_id` | `VARCHAR(100)` | YES | `` |  |  |
| `status` | `ENUM('ACTIVE','INACTIVE','LOCKED')` | NO | `'ACTIVE'` |  | ACTIVE=hoạt động, INACTIVE=tạm ngưng, LOCKED=bị khóa |
| `email_verified_at` | `DATETIME` | YES | `` |  | Thời điểm email được xác thực qua SSO lần đầu hoặc xác nhận bởi hệ thống |
| `failed_login_count` | `INT UNSIGNED` | NO | `0` |  | Số lần đăng nhập sai local password liên tiếp; reset khi login thành công |
| `locked_until` | `DATETIME` | YES | `` |  | Thời điểm hết khóa tạm thời nếu bị lock |
| `created_via` | `ENUM('MANUAL_CREATED','VISITOR_FORM','SSO_AUTO_PROVISION')` | NO | `'MANUAL_CREATED'` |  | MANUAL_CREATED=HO/Staff Leader tạo, VISITOR_FORM=tạo từ form visitor, SSO_AUTO_PROVISION=tạo tự động khi đăng nhập SSO ở cổng Visitor |
| `first_login_at` | `DATETIME` | YES | `` |  |  |
| `last_login_at` | `DATETIME` | YES | `` |  |  |
| `created_at` | `DATETIME` | NO | `CURRENT_TIMESTAMP` |  |  |
| `created_by` | `BIGINT UNSIGNED` | YES | `` |  |  |
| `updated_at` | `DATETIME` | YES | `NULL` |  |  |
| `updated_by` | `BIGINT UNSIGNED` | YES | `` |  |  |

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
- `CONSTRAINT fk_users_role FOREIGN KEY (role_id) REFERENCES roles(role_id) ON UPDATE CASCADE ON DELETE RESTRICT`
- `CONSTRAINT fk_users_primary_campus FOREIGN KEY (primary_campus_id) REFERENCES campuses(campus_id) ON UPDATE CASCADE ON DELETE RESTRICT`
- `CONSTRAINT fk_users_department FOREIGN KEY (department_id) REFERENCES departments(department_id) ON UPDATE CASCADE ON DELETE RESTRICT`

### `user_auth_providers`

**Primary Key:**
- `PRIMARY KEY (auth_provider_id)`

**Columns:**

| Column | Type | Null | Default | Auto Inc | Notes |
|---|---|---:|---|---:|---|
| `auth_provider_id` | `BIGINT UNSIGNED` | NO | `` | YES |  |
| `user_id` | `BIGINT UNSIGNED` | NO | `` |  |  |
| `provider_type` | `ENUM('LOCAL_PASSWORD','GOOGLE_SSO','FEID')` | NO | `` |  |  |
| `provider_subject` | `VARCHAR(255)` | YES | `` |  | Required for GOOGLE_SSO/FEID |
| `provider_email` | `VARCHAR(150)` | YES | `` |  |  |
| `is_enabled` | `BOOLEAN` | NO | `TRUE` |  |  |
| `linked_at` | `DATETIME` | NO | `CURRENT_TIMESTAMP` |  |  |
| `last_used_at` | `DATETIME` | YES | `` |  |  |

**Unique Constraints:**
- `UNIQUE KEY uq_user_auth_provider_type (user_id, provider_type)`
- `UNIQUE KEY uq_auth_provider_subject (provider_type, provider_subject)`

**Indexes:**
- `KEY idx_auth_provider_email (provider_email)`
- `KEY idx_auth_provider_type_email_enabled (provider_type, provider_email, is_enabled)`

**Foreign Keys:**
- `CONSTRAINT fk_auth_providers_user FOREIGN KEY (user_id) REFERENCES users(user_id) ON UPDATE CASCADE ON DELETE CASCADE`

### `user_sessions`

**Purpose:** Session + refresh token hash

**Primary Key:**
- `PRIMARY KEY (session_id)`

**Columns:**

| Column | Type | Null | Default | Auto Inc | Notes |
|---|---|---:|---|---:|---|
| `session_id` | `BIGINT UNSIGNED` | NO | `` | YES |  |
| `user_id` | `BIGINT UNSIGNED` | NO | `` |  |  |
| `login_portal` | `ENUM('VISITOR','INTERNAL')` | NO | `` |  |  |
| `selected_campus_id` | `BIGINT UNSIGNED` | YES | `` |  | Auto set to users.primary_campus_id for INTERNAL, NULL for VISITOR |
| `auth_provider_id` | `BIGINT UNSIGNED` | YES | `` |  |  |
| `refresh_token_hash` | `VARCHAR(255)` | YES | `` |  | Refresh token hash merged into session |
| `refresh_expires_at` | `DATETIME` | YES | `` |  |  |
| `refresh_revoked_at` | `DATETIME` | YES | `` |  |  |
| `ip_address` | `VARCHAR(45)` | YES | `` |  |  |
| `user_agent` | `VARCHAR(500)` | YES | `` |  |  |
| `created_at` | `DATETIME` | NO | `CURRENT_TIMESTAMP` |  |  |
| `expires_at` | `DATETIME` | NO | `` |  |  |
| `revoked_at` | `DATETIME` | YES | `` |  |  |
| `revoked_by` | `BIGINT UNSIGNED` | YES | `` |  |  |
| `revoked_reason` | `VARCHAR(255)` | YES | `` |  |  |

**Unique Constraints:**
- `UNIQUE KEY uq_sessions_refresh_hash (refresh_token_hash)`

**Indexes:**
- `KEY idx_sessions_user_active (user_id, revoked_at, expires_at)`
- `KEY idx_sessions_portal_campus (login_portal, selected_campus_id)`
- `KEY idx_sessions_refresh_active (refresh_token_hash, refresh_revoked_at, refresh_expires_at)`
- `KEY idx_sessions_ip_time (ip_address, created_at)`

**Foreign Keys:**
- `CONSTRAINT fk_sessions_user FOREIGN KEY (user_id) REFERENCES users(user_id) ON UPDATE CASCADE ON DELETE CASCADE`
- `CONSTRAINT fk_sessions_selected_campus FOREIGN KEY (selected_campus_id) REFERENCES campuses(campus_id) ON UPDATE CASCADE ON DELETE RESTRICT`
- `CONSTRAINT fk_sessions_auth_provider FOREIGN KEY (auth_provider_id) REFERENCES user_auth_providers(auth_provider_id) ON UPDATE CASCADE ON DELETE SET NULL`
- `CONSTRAINT fk_sessions_revoked_by FOREIGN KEY (revoked_by) REFERENCES users(user_id) ON UPDATE CASCADE ON DELETE SET NULL`

### `otp_tokens`

**Purpose:** OTP, magic link, set password token, reset password token

**Primary Key:**
- `PRIMARY KEY (otp_token_id)`

**Columns:**

| Column | Type | Null | Default | Auto Inc | Notes |
|---|---|---:|---|---:|---|
| `otp_token_id` | `BIGINT UNSIGNED` | NO | `` | YES |  |
| `user_id` | `BIGINT UNSIGNED` | YES | `` |  |  |
| `email` | `VARCHAR(150)` | NO | `` |  |  |
| `token_type` | `ENUM('OTP_CODE','MAGIC_LINK')` | NO | `'OTP_CODE'` |  |  |
| `purpose` | `ENUM('VISIT_REQUEST_VERIFY','CHANGE_SENSITIVE_ACTION')` | NO | `` |  |  |
| `token_hash` | `VARCHAR(255)` | NO | `` |  |  |
| `expires_at` | `DATETIME` | NO | `` |  |  |
| `used_at` | `DATETIME` | YES | `` |  |  |
| `attempt_count` | `INT UNSIGNED` | NO | `0` |  |  |
| `max_attempts` | `INT UNSIGNED` | NO | `5` |  |  |
| `resend_count` | `INT UNSIGNED` | NO | `0` |  |  |
| `ip_address` | `VARCHAR(45)` | YES | `` |  |  |
| `user_agent` | `VARCHAR(500)` | YES | `` |  |  |
| `created_at` | `DATETIME` | NO | `CURRENT_TIMESTAMP` |  |  |

**Unique Constraints:**
- `UNIQUE KEY uq_otp_tokens_hash (token_hash)`

**Indexes:**
- `KEY idx_otp_email_purpose_time (email, purpose, created_at)`
- `KEY idx_otp_email_purpose_active (email, purpose, used_at, expires_at)`
- `KEY idx_otp_user_purpose_active (user_id, purpose, used_at, expires_at)`
- `KEY idx_otp_ip_time (ip_address, created_at)`

**Foreign Keys:**
- `CONSTRAINT fk_otp_tokens_user FOREIGN KEY (user_id) REFERENCES users(user_id) ON UPDATE CASCADE ON DELETE CASCADE`

### `login_logs`

**Purpose:** Lịch sử đăng nhập

**Primary Key:**
- `PRIMARY KEY (login_log_id)`

**Columns:**

| Column | Type | Null | Default | Auto Inc | Notes |
|---|---|---:|---|---:|---|
| `login_log_id` | `BIGINT UNSIGNED` | NO | `` | YES |  |
| `user_id` | `BIGINT UNSIGNED` | YES | `` |  |  |
| `email` | `VARCHAR(150)` | NO | `` |  |  |
| `login_portal` | `ENUM('VISITOR','INTERNAL')` | NO | `` |  |  |
| `selected_campus_id` | `BIGINT UNSIGNED` | YES | `` |  |  |
| `provider_type` | `ENUM('LOCAL_PASSWORD','GOOGLE_SSO','FEID')` | YES | `` |  |  |
| `status` | `ENUM('SUCCESS','FAILED','BLOCKED')` | NO | `` |  |  |
| `failure_reason` | `VARCHAR(255)` | YES | `` |  |  |
| `ip_address` | `VARCHAR(45)` | YES | `` |  |  |
| `user_agent` | `VARCHAR(500)` | YES | `` |  |  |
| `session_id` | `BIGINT UNSIGNED` | YES | `` |  |  |
| `created_at` | `DATETIME` | NO | `CURRENT_TIMESTAMP` |  |  |

**Indexes:**
- `KEY idx_login_logs_user_time (user_id, created_at)`
- `KEY idx_login_logs_email_status_time (email, status, created_at)`
- `KEY idx_login_logs_ip_status_time (ip_address, status, created_at)`
- `KEY idx_login_logs_portal_campus (login_portal, selected_campus_id)`
- `KEY idx_login_logs_provider_time (provider_type, created_at)`

**Foreign Keys:**
- `CONSTRAINT fk_login_logs_user FOREIGN KEY (user_id) REFERENCES users(user_id) ON UPDATE CASCADE ON DELETE SET NULL`
- `CONSTRAINT fk_login_logs_campus FOREIGN KEY (selected_campus_id) REFERENCES campuses(campus_id) ON UPDATE CASCADE ON DELETE SET NULL`

### `security_events`

**Purpose:** Security, abuse, lockout events

**Primary Key:**
- `PRIMARY KEY (security_event_id)`

**Columns:**

| Column | Type | Null | Default | Auto Inc | Notes |
|---|---|---:|---|---:|---|
| `security_event_id` | `BIGINT UNSIGNED` | NO | `` | YES |  |
| `user_id` | `BIGINT UNSIGNED` | YES | `` |  |  |
| `email` | `VARCHAR(150)` | YES | `` |  |  |
| `event_type` | `VARCHAR(80)` | NO | `` |  | LOGIN_LOCKED, OTP_FAILED, SUSPICIOUS_IP... |
| `severity` | `ENUM('LOW','MEDIUM','HIGH','CRITICAL')` | NO | `'LOW'` |  |  |
| `ip_address` | `VARCHAR(45)` | YES | `` |  |  |
| `user_agent` | `VARCHAR(500)` | YES | `` |  |  |
| `metadata` | `JSON` | YES | `` |  |  |
| `created_at` | `DATETIME` | NO | `CURRENT_TIMESTAMP` |  |  |

**Indexes:**
- `KEY idx_security_user_time (user_id, created_at)`
- `KEY idx_security_email_time (email, created_at)`
- `KEY idx_security_type_time (event_type, created_at)`
- `KEY idx_security_ip_time (ip_address, created_at)`
- `KEY idx_security_severity_time (severity, created_at)`

**Foreign Keys:**
- `CONSTRAINT fk_security_events_user FOREIGN KEY (user_id) REFERENCES users(user_id) ON UPDATE CASCADE ON DELETE SET NULL`

### `partners`

**Purpose:** Hồ sơ đối tác

**Primary Key:**
- `PRIMARY KEY (partner_id)`

**Columns:**

| Column | Type | Null | Default | Auto Inc | Notes |
|---|---|---:|---|---:|---|
| `partner_id` | `BIGINT UNSIGNED` | NO | `` | YES |  |
| `partner_code` | `VARCHAR(50)` | YES | `` |  |  |
| `name` | `VARCHAR(200)` | NO | `` |  |  |
| `short_name` | `VARCHAR(100)` | YES | `` |  |  |
| `country` | `VARCHAR(100)` | YES | `` |  |  |
| `city` | `VARCHAR(100)` | YES | `` |  |  |
| `website_url` | `VARCHAR(500)` | YES | `` |  |  |
| `partner_type` | `ENUM('UNIVERSITY','COMPANY','GOVERNMENT','NGO','OTHER')` | NO | `'UNIVERSITY'` |  |  |
| `cooperation_status` | `ENUM('POTENTIAL','ACTIVE','INACTIVE','BLACKLISTED')` | NO | `'POTENTIAL'` |  |  |
| `description` | `TEXT` | YES | `` |  |  |
| `created_at` | `DATETIME` | NO | `CURRENT_TIMESTAMP` |  |  |
| `created_by` | `BIGINT UNSIGNED` | YES | `` |  |  |
| `updated_at` | `DATETIME` | YES | `NULL` |  |  |
| `updated_by` | `BIGINT UNSIGNED` | YES | `` |  |  |

**Unique Constraints:**
- `UNIQUE KEY uq_partners_code (partner_code)`

**Indexes:**
- `KEY idx_partners_country (country)`
- `KEY idx_partners_status (cooperation_status)`
- `KEY idx_partners_type_status (partner_type, cooperation_status)`
- `KEY idx_partners_created_at (created_at)`
- `FULLTEXT KEY ft_partners_search (name, short_name, description)`

### `partner_contacts`

**Purpose:** Người liên hệ đối tác. OCR final confirmed data saved here.

**Primary Key:**
- `PRIMARY KEY (contact_id)`

**Columns:**

| Column | Type | Null | Default | Auto Inc | Notes |
|---|---|---:|---|---:|---|
| `contact_id` | `BIGINT UNSIGNED` | NO | `` | YES |  |
| `partner_id` | `BIGINT UNSIGNED` | NO | `` |  |  |
| `full_name` | `VARCHAR(150)` | NO | `` |  |  |
| `email` | `VARCHAR(150)` | YES | `` |  |  |
| `phone` | `VARCHAR(50)` | YES | `` |  |  |
| `job_title` | `VARCHAR(150)` | YES | `` |  |  |
| `department_name` | `VARCHAR(150)` | YES | `` |  |  |
| `note` | `TEXT` | YES | `` |  |  |
| `is_primary` | `BOOLEAN` | NO | `FALSE` |  |  |
| `status` | `ENUM('ACTIVE','INACTIVE')` | NO | `'ACTIVE'` |  |  |
| `created_at` | `DATETIME` | NO | `CURRENT_TIMESTAMP` |  |  |
| `created_by` | `BIGINT UNSIGNED` | YES | `` |  |  |
| `updated_at` | `DATETIME` | YES | `NULL` |  |  |
| `updated_by` | `BIGINT UNSIGNED` | YES | `` |  |  |

**Unique Constraints:**
- `UNIQUE KEY uq_partner_contacts_partner_email (partner_id, email)`

**Indexes:**
- `KEY idx_partner_contacts_partner (partner_id)`
- `KEY idx_partner_contacts_email (email)`
- `KEY idx_partner_contacts_status (status)`

**Foreign Keys:**
- `CONSTRAINT fk_partner_contacts_partner FOREIGN KEY (partner_id) REFERENCES partners(partner_id) ON UPDATE CASCADE ON DELETE RESTRICT`

### `files`

**Purpose:** File metadata only. Binary file is stored outside DB.

**Primary Key:**
- `PRIMARY KEY (file_id)`

**Columns:**

| Column | Type | Null | Default | Auto Inc | Notes |
|---|---|---:|---|---:|---|
| `file_id` | `BIGINT UNSIGNED` | NO | `` | YES |  |
| `storage_provider` | `ENUM('LOCAL','S3','AZURE','GCS','GOOGLE_DRIVE','OTHER')` | NO | `'LOCAL'` |  |  |
| `bucket_name` | `VARCHAR(150)` | YES | `` |  |  |
| `object_key` | `VARCHAR(700)` | NO | `` |  | Max 700 chars to keep UNIQUE index safe under utf8mb4 |
| `original_filename` | `VARCHAR(255)` | NO | `` |  |  |
| `mime_type` | `VARCHAR(150)` | YES | `` |  |  |
| `file_size` | `BIGINT UNSIGNED` | YES | `` |  |  |
| `visibility` | `ENUM('PRIVATE','INTERNAL','PUBLIC')` | NO | `'PRIVATE'` |  |  |
| `uploaded_by` | `BIGINT UNSIGNED` | YES | `` |  |  |
| `uploaded_at` | `DATETIME` | NO | `CURRENT_TIMESTAMP` |  |  |

**Unique Constraints:**
- `UNIQUE KEY uq_files_object_key (object_key)`

**Indexes:**
- `KEY idx_files_uploaded_by (uploaded_by, uploaded_at)`
- `KEY idx_files_visibility (visibility)`
- `KEY idx_files_mime_time (mime_type, uploaded_at)`
- `KEY idx_files_checksum (checksum_sha256)`

**Foreign Keys:**
- `CONSTRAINT fk_files_uploaded_by FOREIGN KEY (uploaded_by) REFERENCES users(user_id) ON UPDATE CASCADE ON DELETE SET NULL`

**Check Constraints:**
- `checksum_sha256 CHAR(64) NULL`

### `documents`

**Purpose:** Tài liệu nghiệp vụ. partner_documents/reports/logistics documents merged by owner_type.

**Primary Key:**
- `PRIMARY KEY (document_id)`

**Columns:**

| Column | Type | Null | Default | Auto Inc | Notes |
|---|---|---:|---|---:|---|
| `document_id` | `BIGINT UNSIGNED` | NO | `` | YES |  |
| `file_id` | `BIGINT UNSIGNED` | NO | `` |  |  |
| `owner_type` | `ENUM('GENERAL','VISIT','PARTNER','MINUTES','NEWS','LOGISTICS','REPORT')` | NO | `'GENERAL'` |  |  |
| `owner_id` | `BIGINT UNSIGNED` | YES | `` |  |  |
| `campus_id` | `BIGINT UNSIGNED` | YES | `` |  |  |
| `title` | `VARCHAR(255)` | NO | `` |  |  |
| `description` | `TEXT` | YES | `` |  |  |
| `document_category` | `VARCHAR(100)` | YES | `` |  |  |
| `status` | `ENUM('DRAFT','PUBLISHED','ARCHIVED')` | NO | `'DRAFT'` |  |  |
| `created_at` | `DATETIME` | NO | `CURRENT_TIMESTAMP` |  |  |
| `created_by` | `BIGINT UNSIGNED` | YES | `` |  |  |
| `updated_at` | `DATETIME` | YES | `NULL` |  |  |
| `updated_by` | `BIGINT UNSIGNED` | YES | `` |  |  |

**Indexes:**
- `KEY idx_documents_owner (owner_type, owner_id)`
- `KEY idx_documents_campus_status (campus_id, status)`
- `KEY idx_documents_category_status (document_category, status)`
- `KEY idx_documents_created_by_time (created_by, created_at)`
- `FULLTEXT KEY ft_documents_search (title, description)`

**Foreign Keys:**
- `CONSTRAINT fk_documents_file FOREIGN KEY (file_id) REFERENCES files(file_id) ON UPDATE CASCADE ON DELETE RESTRICT`
- `CONSTRAINT fk_documents_campus FOREIGN KEY (campus_id) REFERENCES campuses(campus_id) ON UPDATE CASCADE ON DELETE SET NULL`
- `CONSTRAINT fk_documents_created_by FOREIGN KEY (created_by) REFERENCES users(user_id) ON UPDATE CASCADE ON DELETE SET NULL`

### `visit_requests`

**Primary Key:**
- `PRIMARY KEY (visit_request_id)`

**Columns:**

| Column | Type | Null | Default | Auto Inc | Notes |
|---|---|---:|---|---:|---|
| `visit_request_id` | `BIGINT UNSIGNED` | NO | `` | YES |  |
| `request_code` | `VARCHAR(50)` | NO | `` |  |  |
| `visitor_user_id` | `BIGINT UNSIGNED` | NO | `` |  | Visitor user/account created or linked for the registrant |
| `partner_id` | `BIGINT UNSIGNED` | YES | `` |  |  |
| `registrant_organization` | `VARCHAR(200)` | NO | `` |  | Đơn vị công tác người đăng ký |
| `registrant_job_title` | `VARCHAR(150)` | YES | `` |  | Chức danh/phòng ban người đăng ký |
| `registrant_phone` | `VARCHAR(50)` | YES | `` |  | SĐT người đăng ký |
| `registrant_email` | `VARCHAR(150)` | NO | `` |  | Email người đăng ký |
| `registrant_nationality` | `VARCHAR(100)` | YES | `` |  | Quốc tịch người đăng ký |
| `visit_scope` | `ENUM('SINGLE_CAMPUS','MULTI_CAMPUS')` | NO | `'SINGLE_CAMPUS'` |  | SINGLE_CAMPUS: Staff Leader duyệt request tổng; MULTI_CAMPUS: HO duyệt request tổng. Frontend/backend suy ra người duyệt từ cột này. |
| `purpose` | `TEXT` | NO | `` |  | Mục đích thăm FPTU |
| `working_content` | `TEXT` | YES | `` |  | Nội dung làm việc tại FPTU |
| `expected_guest_count` | `INT UNSIGNED` | NO | `1` |  | Số khách dự kiến; có thể đồng bộ từ danh sách khách |
| `support_team_json` | `JSON` | YES | `` |  | Danh sách team hỗ trợ khách từ phía đoàn/đơn vị gửi |
| `contact_person_json` | `JSON` | YES | `` |  | Thông tin đầu mối liên hệ: full_name, organization, phone, email |
| `working_language` | `ENUM('VI','EN','OTHER')` | NO | `'EN'` |  | Ngôn ngữ sử dụng trong visit |
| `interpreter_note` | `TEXT` | YES | `` |  | Ghi chú nếu ngôn ngữ khác VI/EN và đầu mối cần tự bố trí phiên dịch |
| `transportation_note` | `TEXT` | YES | `` |  | Nhận diện phương tiện di chuyển tới FPTU |
| `note_to_fptu` | `TEXT` | YES | `` |  | Ghi chú cho FPTU |
| `status` | `ENUM('PENDING_APPROVAL','APPROVED','REJECTED','CANCELLED')` | NO | `'PENDING_APPROVAL'` |  | Request decision status only. Visit progress is derived from visit_request_campuses.status |
| `submitted_at` | `DATETIME` | NO | `CURRENT_TIMESTAMP` |  |  |
| `email_verified_at` | `DATETIME` | YES | `` |  |  |
| `decided_by` | `BIGINT UNSIGNED` | YES | `` |  | Người approve/reject request tổng |
| `decided_at` | `DATETIME` | YES | `` |  | Thời điểm xử lý request tổng |
| `decision_actor_role` | `ENUM('HO','STAFF_LEADER','SYSTEM')` | YES | `` |  | Vai trò người xử lý tại thời điểm quyết định |
| `decision_note` | `TEXT` | YES | `` |  | Lý do/ghi chú khi approve hoặc reject |
| `cancelled_by` | `BIGINT UNSIGNED` | YES | `` |  | Người thực hiện hủy request/delegation |
| `cancelled_at` | `DATETIME` | YES | `` |  | Thời điểm hủy request/delegation |
| `cancellation_actor_type` | `ENUM('VISITOR','HOST','STAFF_LEADER','HO','SYSTEM')` | YES | `` |  | Vai trò thực hiện thao tác hủy |
| `cancellation_source` | `ENUM('SELF_SERVICE','EXTERNAL_CONFIRMATION')` | YES | `` |  | SELF_SERVICE=Visitor tự hủy sau khi đơn đã duyệt; EXTERNAL_CONFIRMATION=Host hủy sau khi khách xác nhận ngoài hệ thống |
| `cancellation_reason` | `TEXT` | YES | `` |  | Lý do hủy; nếu EXTERNAL_CONFIRMATION thì ghi rõ kênh xác nhận, thời điểm, người xác nhận và lý do. |
| `row_version` | `INT UNSIGNED` | NO | `0` |  | Optimistic concurrency token |
| `created_at` | `DATETIME` | NO | `CURRENT_TIMESTAMP` |  |  |
| `created_by` | `BIGINT UNSIGNED` | YES | `` |  |  |
| `updated_at` | `DATETIME` | YES | `NULL` |  |  |
| `updated_by` | `BIGINT UNSIGNED` | YES | `` |  |  |

**Unique Constraints:**
- `UNIQUE KEY uq_visit_requests_code (request_code)`

**Indexes:**
- `-- 1. Registrant information from the Campus Visit form registrant_full_name VARCHAR(150) NOT NULL COMMENT 'Họ và tên người đăng ký'`
- `-- 2. Delegation information delegation_name VARCHAR(200) NOT NULL COMMENT 'Tên đoàn khách'`
- `KEY idx_visit_requests_visitor (visitor_user_id)`
- `KEY idx_visit_requests_partner (partner_id)`
- `KEY idx_visit_requests_status_submitted (status, submitted_at)`
- `KEY idx_visit_requests_registrant_email (registrant_email)`
- `KEY idx_visit_requests_scope_status (visit_scope, status)`
- `KEY idx_visit_requests_decision (decided_by, decided_at)`
- `KEY idx_visit_requests_decision_role (decision_actor_role, decided_at)`
- `KEY idx_visit_requests_cancelled (cancelled_by, cancelled_at)`
- `KEY idx_visit_requests_cancel_actor (cancellation_actor_type, cancelled_at)`

**Foreign Keys:**
- `CONSTRAINT fk_visit_requests_visitor_user FOREIGN KEY (visitor_user_id) REFERENCES users(user_id) ON UPDATE CASCADE ON DELETE RESTRICT`
- `CONSTRAINT fk_visit_requests_partner FOREIGN KEY (partner_id) REFERENCES partners(partner_id) ON UPDATE CASCADE ON DELETE SET NULL`
- `CONSTRAINT fk_visit_requests_decided_by FOREIGN KEY (decided_by) REFERENCES users(user_id) ON UPDATE CASCADE ON DELETE SET NULL`
- `CONSTRAINT fk_visit_requests_cancelled_by FOREIGN KEY (cancelled_by) REFERENCES users(user_id) ON UPDATE CASCADE ON DELETE SET NULL`

**Check Constraints:**
- `CHECK (expected_guest_count >= 1)`
- `CHECK ( decision_actor_role IS NULL OR status NOT IN ('APPROVED','REJECTED') OR ( visit_scope = 'SINGLE_CAMPUS' AND decision_actor_role IN ('STAFF_LEADER','SYSTEM') ) OR ( visit_scope = 'MULTI_CAMPUS' AND decision_actor_role IN ('HO','SYSTEM') ) )`

### `visit_request_campuses`

**Primary Key:**
- `PRIMARY KEY (visit_instance_id)`

**Columns:**

| Column | Type | Null | Default | Auto Inc | Notes |
|---|---|---:|---|---:|---|
| `visit_instance_id` | `BIGINT UNSIGNED` | NO | `` | YES |  |
| `visit_request_id` | `BIGINT UNSIGNED` | NO | `` |  |  |
| `campus_id` | `BIGINT UNSIGNED` | NO | `` |  |  |
| `instance_code` | `VARCHAR(60)` | YES | `` |  |  |
| `planned_start_at` | `DATETIME` | NO | `` |  | Ngày giờ bắt đầu dự kiến tại campus |
| `planned_end_at` | `DATETIME` | NO | `` |  | Ngày giờ kết thúc dự kiến tại campus |
| `status` | `ENUM( 'WAITING_REQUEST_APPROVAL', 'ASSIGNED', 'BEFORE_VISIT', 'DURING_VISIT', 'AFTER_VISIT', 'CLOSED', 'CANCELLED' )` | NO | `'WAITING_REQUEST_APPROVAL'` |  |  |
| `current_host_user_id` | `BIGINT UNSIGNED` | YES | `` |  | Host hiện tại chịu trách nhiệm campus instance. Sau khi request tổng được duyệt thì phải có host; nếu đổi host dùng chức năng Transfer Host |
| `host_assigned_by` | `BIGINT UNSIGNED` | YES | `` |  | Người gây ra thao tác gán host: HO khi auto gán Staff Leader cho multi-campus, Staff Leader khi duyệt single-campus, hoặc người chuyển host |
| `host_assigned_at` | `DATETIME` | YES | `` |  | Thời điểm host được gán |
| `host_assignment_source` | `ENUM('AUTO_STAFF_LEADER','MANUAL_APPROVAL','TRANSFERRED')` | YES | `` |  | AUTO_STAFF_LEADER=HO duyệt liên cơ sở và hệ thống tự gán Staff Leader; MANUAL_APPROVAL=Staff Leader duyệt đơn một cơ sở và chọn host; TRANSFERRED=host được chuyển sau đó |
| `host_transferred_by` | `BIGINT UNSIGNED` | YES | `` |  | Người chuyển host gần nhất |
| `host_transferred_at` | `DATETIME` | YES | `` |  | Thời điểm chuyển host gần nhất |
| `host_transfer_note` | `TEXT` | YES | `` |  | Ghi chú/lý do chuyển host gần nhất |
| `closed_by` | `BIGINT UNSIGNED` | YES | `` |  |  |
| `closed_at` | `DATETIME` | YES | `` |  |  |
| `close_note` | `TEXT` | YES | `` |  |  |
| `cancelled_by` | `BIGINT UNSIGNED` | YES | `` |  | Người thực hiện hủy campus instance |
| `cancelled_at` | `DATETIME` | YES | `` |  | Thời điểm hủy campus instance |
| `cancellation_actor_type` | `ENUM('VISITOR','HOST','STAFF_LEADER','HO','SYSTEM')` | YES | `` |  | Vai trò thực hiện thao tác hủy campus instance |
| `cancellation_source` | `ENUM('SELF_SERVICE','EXTERNAL_CONFIRMATION')` | YES | `` |  | SELF_SERVICE=Visitor tự hủy sau khi đơn đã duyệt; EXTERNAL_CONFIRMATION=Host hủy sau khi khách xác nhận ngoài hệ thống |
| `cancellation_reason` | `TEXT` | YES | `` |  | Lý do hủy; nếu EXTERNAL_CONFIRMATION thì ghi rõ kênh xác nhận, thời điểm, người xác nhận và lý do. |
| `row_version` | `INT UNSIGNED` | NO | `0` |  | Optimistic concurrency token |
| `created_at` | `DATETIME` | NO | `CURRENT_TIMESTAMP` |  |  |
| `created_by` | `BIGINT UNSIGNED` | YES | `` |  |  |
| `updated_at` | `DATETIME` | YES | `NULL` |  |  |
| `updated_by` | `BIGINT UNSIGNED` | YES | `` |  |  |

**Unique Constraints:**
- `UNIQUE KEY uq_visit_instance_request_campus (visit_request_id, campus_id)`
- `UNIQUE KEY uq_visit_instance_code (instance_code)`

**Indexes:**
- `KEY idx_visit_instances_campus_status_time (campus_id, status, planned_start_at)`
- `KEY idx_visit_instances_request (visit_request_id)`
- `KEY idx_visit_instances_status_time (status, planned_start_at)`
- `KEY idx_visit_instances_current_host (current_host_user_id, status)`
- `KEY idx_visit_instances_host_assigned (host_assigned_by, host_assigned_at)`
- `KEY idx_visit_instances_assignment_source (host_assignment_source, host_assigned_at)`
- `KEY idx_visit_instances_host_transfer (host_transferred_by, host_transferred_at)`
- `KEY idx_visit_instances_cancelled (cancelled_by, cancelled_at)`
- `KEY idx_visit_instances_cancel_actor (cancellation_actor_type, cancelled_at)`

**Foreign Keys:**
- `CONSTRAINT fk_visit_instances_request FOREIGN KEY (visit_request_id) REFERENCES visit_requests(visit_request_id) ON UPDATE CASCADE ON DELETE RESTRICT`
- `CONSTRAINT fk_visit_instances_campus FOREIGN KEY (campus_id) REFERENCES campuses(campus_id) ON UPDATE CASCADE ON DELETE RESTRICT`
- `CONSTRAINT fk_visit_instances_current_host FOREIGN KEY (current_host_user_id) REFERENCES users(user_id) ON UPDATE CASCADE ON DELETE SET NULL`
- `CONSTRAINT fk_visit_instances_host_assigned_by FOREIGN KEY (host_assigned_by) REFERENCES users(user_id) ON UPDATE CASCADE ON DELETE SET NULL`
- `CONSTRAINT fk_visit_instances_host_transferred_by FOREIGN KEY (host_transferred_by) REFERENCES users(user_id) ON UPDATE CASCADE ON DELETE SET NULL`
- `CONSTRAINT fk_visit_instances_closed_by FOREIGN KEY (closed_by) REFERENCES users(user_id) ON UPDATE CASCADE ON DELETE SET NULL`
- `CONSTRAINT fk_visit_instances_cancelled_by FOREIGN KEY (cancelled_by) REFERENCES users(user_id) ON UPDATE CASCADE ON DELETE SET NULL`

**Check Constraints:**
- `CHECK (planned_end_at > planned_start_at)`

### `visit_guest_members`

**Purpose:** Danh sách từng người trong đoàn khách. Không lưu consent hình ảnh vì form đã bỏ phần xác nhận sử dụng hình ảnh/thông tin.

**Primary Key:**
- `PRIMARY KEY (guest_member_id)`

**Columns:**

| Column | Type | Null | Default | Auto Inc | Notes |
|---|---|---:|---|---:|---|
| `guest_member_id` | `BIGINT UNSIGNED` | NO | `` | YES |  |
| `visit_request_id` | `BIGINT UNSIGNED` | NO | `` |  |  |
| `full_name` | `VARCHAR(150)` | NO | `` |  |  |
| `organization` | `VARCHAR(200)` | YES | `` |  |  |
| `job_title` | `VARCHAR(150)` | YES | `` |  |  |
| `nationality` | `VARCHAR(100)` | YES | `` |  |  |
| `email` | `VARCHAR(150)` | YES | `` |  |  |
| `phone` | `VARCHAR(50)` | YES | `` |  |  |
| `is_representative` | `BOOLEAN` | NO | `FALSE` |  |  |
| `note` | `TEXT` | YES | `` |  |  |
| `created_at` | `DATETIME` | NO | `CURRENT_TIMESTAMP` |  |  |
| `created_by` | `BIGINT UNSIGNED` | YES | `` |  |  |
| `updated_at` | `DATETIME` | YES | `NULL` |  |  |
| `updated_by` | `BIGINT UNSIGNED` | YES | `` |  |  |

**Indexes:**
- `KEY idx_guest_members_request (visit_request_id)`
- `KEY idx_guest_members_email (email)`
- `KEY idx_guest_members_representative (visit_request_id, is_representative)`

**Foreign Keys:**
- `CONSTRAINT fk_guest_members_request FOREIGN KEY (visit_request_id) REFERENCES visit_requests(visit_request_id) ON UPDATE CASCADE ON DELETE RESTRICT`

### `visit_participants`

**Purpose:** Người nội bộ tham gia. HOST lưu bằng is_host. One-host rule should be enforced by backend/audit for portability.

**Primary Key:**
- `PRIMARY KEY (participant_id)`

**Columns:**

| Column | Type | Null | Default | Auto Inc | Notes |
|---|---|---:|---|---:|---|
| `participant_id` | `BIGINT UNSIGNED` | NO | `` | YES |  |
| `visit_instance_id` | `BIGINT UNSIGNED` | NO | `` |  |  |
| `user_id` | `BIGINT UNSIGNED` | NO | `` |  |  |
| `participant_role` | `ENUM('IC_HOST','IC_SUPPORT','DEPT_SUPPORT','STUDENT_BUDDY','MEDIA','INTERPRETER','OTHER')` | NO | `'OTHER'` |  |  |
| `is_host` | `BOOLEAN` | NO | `FALSE` |  |  |
| `status` | `ENUM('INVITED','ACCEPTED','DECLINED','ASSIGNED','REMOVED')` | NO | `'INVITED'` |  |  |
| `invited_by` | `BIGINT UNSIGNED` | YES | `` |  |  |
| `invited_at` | `DATETIME` | YES | `` |  |  |
| `responded_at` | `DATETIME` | YES | `` |  |  |
| `assigned_by` | `BIGINT UNSIGNED` | YES | `` |  |  |
| `assigned_at` | `DATETIME` | YES | `` |  |  |
| `note` | `TEXT` | YES | `` |  |  |
| `created_at` | `DATETIME` | NO | `CURRENT_TIMESTAMP` |  |  |
| `created_by` | `BIGINT UNSIGNED` | YES | `` |  |  |
| `updated_at` | `DATETIME` | YES | `NULL` |  |  |
| `updated_by` | `BIGINT UNSIGNED` | YES | `` |  |  |

**Unique Constraints:**
- `UNIQUE KEY uq_visit_participants_user (visit_instance_id, user_id)`

**Indexes:**
- `KEY idx_visit_participants_one_host_lookup (visit_instance_id, is_host)`
- `KEY idx_visit_participants_user_status (user_id, status)`
- `KEY idx_visit_participants_instance (visit_instance_id)`
- `KEY idx_visit_participants_role_status (participant_role, status)`

**Foreign Keys:**
- `CONSTRAINT fk_visit_participants_instance FOREIGN KEY (visit_instance_id) REFERENCES visit_request_campuses(visit_instance_id) ON UPDATE CASCADE ON DELETE RESTRICT`
- `CONSTRAINT fk_visit_participants_user FOREIGN KEY (user_id) REFERENCES users(user_id) ON UPDATE CASCADE ON DELETE RESTRICT`
- `CONSTRAINT fk_visit_participants_invited_by FOREIGN KEY (invited_by) REFERENCES users(user_id) ON UPDATE CASCADE ON DELETE SET NULL`
- `CONSTRAINT fk_visit_participants_assigned_by FOREIGN KEY (assigned_by) REFERENCES users(user_id) ON UPDATE CASCADE ON DELETE SET NULL`

### `visit_agendas`

**Purpose:** Lịch trình tiếp khách

**Primary Key:**
- `PRIMARY KEY (agenda_id)`

**Columns:**

| Column | Type | Null | Default | Auto Inc | Notes |
|---|---|---:|---|---:|---|
| `agenda_id` | `BIGINT UNSIGNED` | NO | `` | YES |  |
| `visit_instance_id` | `BIGINT UNSIGNED` | NO | `` |  |  |
| `sequence_order` | `INT UNSIGNED` | NO | `` |  |  |
| `title` | `VARCHAR(255)` | NO | `` |  |  |
| `description` | `TEXT` | YES | `` |  |  |
| `start_time` | `DATETIME` | NO | `` |  |  |
| `end_time` | `DATETIME` | YES | `` |  |  |
| `location` | `VARCHAR(255)` | YES | `` |  |  |
| `responsible_user_id` | `BIGINT UNSIGNED` | YES | `` |  |  |
| `created_at` | `DATETIME` | NO | `CURRENT_TIMESTAMP` |  |  |
| `created_by` | `BIGINT UNSIGNED` | YES | `` |  |  |
| `updated_at` | `DATETIME` | YES | `NULL` |  |  |
| `updated_by` | `BIGINT UNSIGNED` | YES | `` |  |  |

**Unique Constraints:**
- `UNIQUE KEY uq_visit_agendas_order (visit_instance_id, sequence_order)`

**Indexes:**
- `KEY idx_visit_agendas_time (visit_instance_id, start_time)`
- `KEY idx_visit_agendas_responsible (responsible_user_id, start_time)`

**Foreign Keys:**
- `CONSTRAINT fk_visit_agendas_instance FOREIGN KEY (visit_instance_id) REFERENCES visit_request_campuses(visit_instance_id) ON UPDATE CASCADE ON DELETE RESTRICT`
- `CONSTRAINT fk_visit_agendas_responsible_user FOREIGN KEY (responsible_user_id) REFERENCES users(user_id) ON UPDATE CASCADE ON DELETE SET NULL`

### `visit_logistics_items`

**Purpose:** Yêu cầu hậu cần/resource cho visit: gửi yêu cầu, đề xuất thay đổi, tiếp nhận, phân công, xác nhận và hoàn thành. Thay thế tasks cho logistics/resource.

**Primary Key:**
- `PRIMARY KEY (logistics_item_id)`

**Columns:**

| Column | Type | Null | Default | Auto Inc | Notes |
|---|---|---:|---|---:|---|
| `logistics_item_id` | `BIGINT UNSIGNED` | NO | `` | YES |  |
| `visit_instance_id` | `BIGINT UNSIGNED` | NO | `` |  |  |
| `item_type` | `ENUM('ROOM','TRANSPORT','MEAL','EQUIPMENT','BANNER','LED','OTHER')` | NO | `` |  |  |
| `title` | `VARCHAR(255)` | NO | `` |  |  |
| `description` | `TEXT` | YES | `` |  | Nội dung chi tiết công việc gốc |
| `quantity` | `INT UNSIGNED` | YES | `` |  | Số lượng yêu cầu gốc |
| `usage_start_at` | `DATETIME` | YES | `` |  | Thời gian bắt đầu sử dụng resource |
| `usage_end_at` | `DATETIME` | YES | `` |  | Thời gian kết thúc sử dụng resource |
| `status` | `ENUM( 'PLANNED', 'REQUESTED', 'CHANGE_PROPOSED', 'RECEIVED', 'ASSIGNED', 'ACCEPTED', 'IN_PROGRESS', 'READY', 'DONE', 'REJECTED', 'CANCELLED' )` | NO | `'PLANNED'` |  |  |
| `priority` | `ENUM('LOW','MEDIUM','HIGH','URGENT')` | NO | `'MEDIUM'` |  |  |
| `requested_by` | `BIGINT UNSIGNED` | YES | `` |  | Người gửi yêu cầu hậu cần/resource |
| `requested_to_department_id` | `BIGINT UNSIGNED` | YES | `` |  | Phòng ban được yêu cầu xử lý |
| `requested_at` | `DATETIME` | YES | `` |  | Thời điểm gửi yêu cầu |
| `received_by` | `BIGINT UNSIGNED` | YES | `` |  | Trưởng phòng/người tiếp nhận yêu cầu |
| `received_at` | `DATETIME` | YES | `` |  | Thời điểm tiếp nhận yêu cầu |
| `assigned_to_user_id` | `BIGINT UNSIGNED` | YES | `` |  | Nhân viên được giao xử lý chính |
| `assigned_by` | `BIGINT UNSIGNED` | YES | `` |  | Người phân công |
| `assigned_at` | `DATETIME` | YES | `` |  | Thời điểm phân công |
| `assignee_accepted_at` | `DATETIME` | YES | `` |  | Thời điểm nhân viên xác nhận nhận nhiệm vụ |
| `assignee_response_note` | `TEXT` | YES | `` |  | Ghi chú khi nhân viên nhận/từ chối nếu có |
| `due_at` | `DATETIME` | YES | `` |  | Deadline hoàn thành hạng mục |
| `completed_at` | `DATETIME` | YES | `` |  | Thời điểm hoàn thành |
| `proposed_by` | `BIGINT UNSIGNED` | YES | `` |  | Người gửi đề xuất thay đổi |
| `proposed_at` | `DATETIME` | YES | `` |  | Thời điểm gửi đề xuất thay đổi |
| `proposed_quantity` | `INT UNSIGNED` | YES | `` |  | Số lượng được đề xuất thay đổi |
| `proposed_usage_start_at` | `DATETIME` | YES | `` |  | Thời gian bắt đầu sử dụng được đề xuất |
| `proposed_usage_end_at` | `DATETIME` | YES | `` |  | Thời gian kết thúc sử dụng được đề xuất |
| `proposed_description` | `TEXT` | YES | `` |  | Nội dung chi tiết công việc được đề xuất thay đổi |
| `proposal_note` | `TEXT` | YES | `` |  | Lý do/ghi chú đề xuất thay đổi |
| `proposal_responded_by` | `BIGINT UNSIGNED` | YES | `` |  | Người xác nhận/từ chối đề xuất |
| `proposal_responded_at` | `DATETIME` | YES | `` |  | Thời điểm xác nhận/từ chối đề xuất |
| `proposal_response` | `ENUM('ACCEPTED','REJECTED')` | YES | `` |  | Kết quả phản hồi đề xuất |
| `proposal_response_note` | `TEXT` | YES | `` |  | Ghi chú phản hồi đề xuất |
| `decision_note` | `TEXT` | YES | `` |  | Lý do reject/cancel hoặc ghi chú xử lý |
| `row_version` | `INT UNSIGNED` | NO | `0` |  | Optimistic concurrency token |
| `created_at` | `DATETIME` | NO | `CURRENT_TIMESTAMP` |  |  |
| `created_by` | `BIGINT UNSIGNED` | YES | `` |  |  |
| `updated_at` | `DATETIME` | YES | `NULL` |  |  |
| `updated_by` | `BIGINT UNSIGNED` | YES | `` |  |  |

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
- `CONSTRAINT fk_logistics_instance FOREIGN KEY (visit_instance_id) REFERENCES visit_request_campuses(visit_instance_id) ON UPDATE CASCADE ON DELETE RESTRICT`
- `CONSTRAINT fk_logistics_requested_by FOREIGN KEY (requested_by) REFERENCES users(user_id) ON UPDATE CASCADE ON DELETE SET NULL`
- `CONSTRAINT fk_logistics_requested_to_department FOREIGN KEY (requested_to_department_id) REFERENCES departments(department_id) ON UPDATE CASCADE ON DELETE SET NULL`
- `CONSTRAINT fk_logistics_received_by FOREIGN KEY (received_by) REFERENCES users(user_id) ON UPDATE CASCADE ON DELETE SET NULL`
- `CONSTRAINT fk_logistics_assigned_to FOREIGN KEY (assigned_to_user_id) REFERENCES users(user_id) ON UPDATE CASCADE ON DELETE SET NULL`
- `CONSTRAINT fk_logistics_assigned_by FOREIGN KEY (assigned_by) REFERENCES users(user_id) ON UPDATE CASCADE ON DELETE SET NULL`
- `CONSTRAINT fk_logistics_proposed_by FOREIGN KEY (proposed_by) REFERENCES users(user_id) ON UPDATE CASCADE ON DELETE SET NULL`
- `CONSTRAINT fk_logistics_proposal_responded_by FOREIGN KEY (proposal_responded_by) REFERENCES users(user_id) ON UPDATE CASCADE ON DELETE SET NULL`

**Check Constraints:**
- `CHECK (quantity IS NULL OR quantity >= 1)`
- `CHECK (usage_end_at IS NULL OR usage_start_at IS NULL OR usage_end_at > usage_start_at)`
- `CHECK (proposed_quantity IS NULL OR proposed_quantity >= 1)`
- `CHECK (proposed_usage_end_at IS NULL OR proposed_usage_start_at IS NULL OR proposed_usage_end_at > proposed_usage_start_at)`

### `minutes`

**Primary Key:**
- `PRIMARY KEY (minutes_id)`

**Columns:**

| Column | Type | Null | Default | Auto Inc | Notes |
|---|---|---:|---|---:|---|
| `minutes_id` | `BIGINT UNSIGNED` | NO | `` | YES |  |
| `visit_instance_id` | `BIGINT UNSIGNED` | NO | `` |  |  |
| `title` | `VARCHAR(255)` | NO | `` |  |  |
| `content` | `LONGTEXT` | YES | `` |  |  |
| `participants_json` | `JSON` | YES | `` |  | Danh sách người tham gia trong biên bản, lưu dạng snapshot nếu cần hiển thị lại |
| `status` | `ENUM('DRAFT','FINAL')` | NO | `'DRAFT'` |  | DRAFT=đang soạn, FINAL=đã chốt |
| `finalized_by` | `BIGINT UNSIGNED` | YES | `` |  | Người chốt biên bản |
| `finalized_at` | `DATETIME` | YES | `` |  | Thời điểm chốt biên bản |
| `created_at` | `DATETIME` | NO | `CURRENT_TIMESTAMP` |  |  |
| `created_by` | `BIGINT UNSIGNED` | YES | `` |  |  |
| `updated_at` | `DATETIME` | YES | `NULL` |  |  |
| `updated_by` | `BIGINT UNSIGNED` | YES | `` |  |  |

**Indexes:**
- `KEY idx_minutes_visit_status (visit_instance_id, status)`
- `KEY idx_minutes_created_by_time (created_by, created_at)`
- `KEY idx_minutes_finalized_by_time (finalized_by, finalized_at)`
- `FULLTEXT KEY ft_minutes_search (title, content)`

**Foreign Keys:**
- `CONSTRAINT fk_minutes_visit_instance FOREIGN KEY (visit_instance_id) REFERENCES visit_request_campuses(visit_instance_id) ON UPDATE CASCADE ON DELETE RESTRICT`
- `CONSTRAINT fk_minutes_created_by FOREIGN KEY (created_by) REFERENCES users(user_id) ON UPDATE CASCADE ON DELETE SET NULL`
- `CONSTRAINT fk_minutes_updated_by FOREIGN KEY (updated_by) REFERENCES users(user_id) ON UPDATE CASCADE ON DELETE SET NULL`
- `CONSTRAINT fk_minutes_finalized_by FOREIGN KEY (finalized_by) REFERENCES users(user_id) ON UPDATE CASCADE ON DELETE SET NULL`

### `minute_action_items`

**Primary Key:**
- `PRIMARY KEY (action_item_id)`

**Columns:**

| Column | Type | Null | Default | Auto Inc | Notes |
|---|---|---:|---|---:|---|
| `action_item_id` | `BIGINT UNSIGNED` | NO | `` | YES |  |
| `minutes_id` | `BIGINT UNSIGNED` | NO | `` |  |  |
| `title` | `VARCHAR(255)` | NO | `` |  | Tên đầu việc |
| `note` | `TEXT` | YES | `` |  | Ghi chú thêm cho đầu việc |
| `due_date` | `DATE` | YES | `` |  | Deadline của đầu việc |
| `status` | `ENUM('TODO','IN_PROGRESS','DONE','CANCELLED')` | NO | `'TODO'` |  | TODO=chưa làm, IN_PROGRESS=đang làm, DONE=hoàn thành, CANCELLED=đã hủy/không cần làm nữa |
| `completed_at` | `DATETIME` | YES | `` |  | Thời điểm hoàn thành; backend tự set khi status chuyển sang DONE |
| `display_order` | `INT UNSIGNED` | NO | `1` |  | Thứ tự hiển thị trong biên bản |
| `created_at` | `DATETIME` | NO | `CURRENT_TIMESTAMP` |  |  |
| `created_by` | `BIGINT UNSIGNED` | YES | `` |  |  |
| `updated_at` | `DATETIME` | YES | `NULL` |  |  |
| `updated_by` | `BIGINT UNSIGNED` | YES | `` |  |  |

**Indexes:**
- `KEY idx_action_items_minutes (minutes_id)`
- `KEY idx_action_items_status_due (status, due_date)`
- `KEY idx_action_items_order (minutes_id, display_order)`
- `KEY idx_action_items_created_by_time (created_by, created_at)`

**Foreign Keys:**
- `CONSTRAINT fk_action_items_minutes FOREIGN KEY (minutes_id) REFERENCES minutes(minutes_id) ON UPDATE CASCADE ON DELETE CASCADE`
- `CONSTRAINT fk_action_items_created_by FOREIGN KEY (created_by) REFERENCES users(user_id) ON UPDATE CASCADE ON DELETE SET NULL`
- `CONSTRAINT fk_action_items_updated_by FOREIGN KEY (updated_by) REFERENCES users(user_id) ON UPDATE CASCADE ON DELETE SET NULL`

### `feedbacks`

**Primary Key:**
- `PRIMARY KEY (feedback_id)`

**Columns:**

| Column | Type | Null | Default | Auto Inc | Notes |
|---|---|---:|---|---:|---|
| `feedback_id` | `BIGINT UNSIGNED` | NO | `` | YES |  |
| `visit_request_id` | `BIGINT UNSIGNED` | NO | `` |  |  |
| `visit_instance_id` | `BIGINT UNSIGNED` | YES | `` |  |  |
| `submitted_by_user_id` | `BIGINT UNSIGNED` | NO | `` |  | User gửi feedback; khách/host/logistics đều phải có tài khoản hệ thống |
| `submitter_role` | `ENUM('VISITOR','HOST','LOGISTICS')` | NO | `` |  | Vai trò người gửi trong chuyến thăm |
| `submitter_context` | `VARCHAR(120)` | NO | `''` |  | Ngữ cảnh vai trò người gửi, ví dụ: Host chính, Xe điện, Teabreak, Khách đại diện |
| `submitter_name_snapshot` | `VARCHAR(255)` | NO | `` |  | Tên người gửi tại thời điểm gửi feedback |
| `target_user_id` | `BIGINT UNSIGNED` | NO | `` |  | User được đánh giá |
| `target_role` | `ENUM('VISITOR','HOST','LOGISTICS')` | NO | `` |  | Vai trò người được đánh giá trong chuyến thăm |
| `target_context` | `VARCHAR(120)` | NO | `''` |  | Ngữ cảnh đối tượng được đánh giá, ví dụ: Host chính, Đoàn khách, Xe điện, Teabreak |
| `target_name_snapshot` | `VARCHAR(255)` | NO | `` |  | Tên người được đánh giá tại thời điểm gửi feedback |
| `rating` | `TINYINT UNSIGNED` | NO | `` |  | Số sao từ 1 đến 5 |
| `comment` | `TEXT` | NO | `` |  | Nội dung feedback |
| `submitted_at` | `DATETIME` | NO | `CURRENT_TIMESTAMP` |  |  |

**Indexes:**
- `KEY idx_feedbacks_visit_request (visit_request_id)`
- `KEY idx_feedbacks_visit_instance (visit_instance_id)`
- `KEY idx_feedbacks_submitter (submitted_by_user_id)`
- `KEY idx_feedbacks_target (target_user_id)`
- `KEY idx_feedbacks_roles (submitter_role, target_role)`
- `KEY idx_feedbacks_rating (rating)`
- `KEY idx_feedbacks_submitted_at (submitted_at)`

**Foreign Keys:**
- `CONSTRAINT chk_feedbacks_rating CHECK (rating BETWEEN 1 AND 5)`
- `CONSTRAINT chk_feedbacks_role_flow CHECK ( (submitter_role IN ('VISITOR','LOGISTICS') AND target_role = 'HOST') OR (submitter_role = 'HOST' AND target_role IN ('VISITOR','LOGISTICS')) )`
- `CONSTRAINT fk_feedbacks_visit_request FOREIGN KEY (visit_request_id) REFERENCES visit_requests(visit_request_id) ON UPDATE CASCADE ON DELETE RESTRICT`
- `CONSTRAINT fk_feedbacks_visit_instance FOREIGN KEY (visit_instance_id) REFERENCES visit_request_campuses(visit_instance_id) ON UPDATE CASCADE ON DELETE SET NULL`
- `CONSTRAINT fk_feedbacks_submitter FOREIGN KEY (submitted_by_user_id) REFERENCES users(user_id) ON UPDATE CASCADE ON DELETE RESTRICT`
- `CONSTRAINT fk_feedbacks_target FOREIGN KEY (target_user_id) REFERENCES users(user_id) ON UPDATE CASCADE ON DELETE RESTRICT`

### `news`

**Primary Key:**
- `PRIMARY KEY (news_id)`

**Columns:**

| Column | Type | Null | Default | Auto Inc | Notes |
|---|---|---:|---|---:|---|
| `news_id` | `BIGINT UNSIGNED` | NO | `` | YES |  |
| `campus_id` | `BIGINT UNSIGNED` | YES | `` |  | Campus liên quan đến bài viết. NULL nếu bài toàn hệ thống |
| `visit_instance_id` | `BIGINT UNSIGNED` | YES | `` |  | Visit instance liên quan nếu bài viết được tạo từ một chuyến tiếp đón |
| `author_user_id` | `BIGINT UNSIGNED` | NO | `` |  | Người tạo/viết bài |
| `cover_file_id` | `BIGINT UNSIGNED` | YES | `` |  | Ảnh bìa bài viết, trỏ tới files.file_id |
| `status` | `ENUM('PENDING_REVIEW','REJECTED','PUBLISHED','HIDDEN')` | NO | `'PENDING_REVIEW'` |  | PENDING_REVIEW=chờ host duyệt, REJECTED=bị từ chối, PUBLISHED=đã đăng, HIDDEN=ẩn khỏi trang tin |
| `submitted_at` | `DATETIME` | NO | `CURRENT_TIMESTAMP` |  | Thời điểm người viết gửi bài cho host duyệt |
| `reviewed_by` | `BIGINT UNSIGNED` | YES | `` |  | Host duyệt hoặc từ chối bài viết |
| `reviewed_at` | `DATETIME` | YES | `` |  | Thời điểm host duyệt hoặc từ chối |
| `review_note` | `TEXT` | YES | `` |  | Ghi chú duyệt hoặc lý do từ chối |
| `published_at` | `DATETIME` | YES | `` |  | Thời điểm bài viết được đăng |
| `is_featured` | `BOOLEAN` | NO | `FALSE` |  | Bài viết nổi bật |
| `row_version` | `INT UNSIGNED` | NO | `0` |  | Optimistic concurrency token, chống ghi đè khi cập nhật đồng thời |
| `created_at` | `DATETIME` | NO | `CURRENT_TIMESTAMP` |  |  |
| `created_by` | `BIGINT UNSIGNED` | YES | `` |  |  |
| `updated_at` | `DATETIME` | YES | `NULL` |  |  |
| `updated_by` | `BIGINT UNSIGNED` | YES | `` |  |  |

**Indexes:**
- `KEY idx_news_public (status, campus_id, published_at)`
- `KEY idx_news_author_status (author_user_id, status)`
- `KEY idx_news_visit_instance_status (visit_instance_id, status)`
- `KEY idx_news_review (reviewed_by, reviewed_at)`
- `KEY idx_news_featured (is_featured, status, published_at)`

**Foreign Keys:**
- `CONSTRAINT fk_news_campus FOREIGN KEY (campus_id) REFERENCES campuses(campus_id) ON UPDATE CASCADE ON DELETE SET NULL`
- `CONSTRAINT fk_news_visit_instance FOREIGN KEY (visit_instance_id) REFERENCES visit_request_campuses(visit_instance_id) ON UPDATE CASCADE ON DELETE SET NULL`
- `CONSTRAINT fk_news_author FOREIGN KEY (author_user_id) REFERENCES users(user_id) ON UPDATE CASCADE ON DELETE RESTRICT`
- `CONSTRAINT fk_news_cover_file FOREIGN KEY (cover_file_id) REFERENCES files(file_id) ON UPDATE CASCADE ON DELETE SET NULL`
- `CONSTRAINT fk_news_reviewed_by FOREIGN KEY (reviewed_by) REFERENCES users(user_id) ON UPDATE CASCADE ON DELETE SET NULL`

### `news_translations`

**Purpose:** Tiêu đề, slug, tóm tắt và SEO của bài viết theo ngôn ngữ

**Primary Key:**
- `PRIMARY KEY (news_translation_id)`

**Columns:**

| Column | Type | Null | Default | Auto Inc | Notes |
|---|---|---:|---|---:|---|
| `news_translation_id` | `BIGINT UNSIGNED` | NO | `` | YES |  |
| `news_id` | `BIGINT UNSIGNED` | NO | `` |  |  |
| `language_code` | `ENUM('vi','en','zh','ja','ko')` | NO | `'vi'` |  |  |
| `title` | `VARCHAR(255)` | NO | `` |  | Tiêu đề chính của bài viết |
| `slug` | `VARCHAR(255)` | NO | `` |  | Đường dẫn SEO của bài viết |
| `summary` | `TEXT` | YES | `` |  | Tóm tắt bài viết |
| `seo_title` | `VARCHAR(255)` | YES | `` |  |  |
| `seo_description` | `VARCHAR(500)` | YES | `` |  |  |
| `created_at` | `DATETIME` | NO | `CURRENT_TIMESTAMP` |  |  |
| `updated_at` | `DATETIME` | YES | `NULL` |  |  |

**Unique Constraints:**
- `UNIQUE KEY uq_news_translation_lang (news_id, language_code)`
- `UNIQUE KEY uq_news_translation_slug_lang (slug, language_code)`

**Indexes:**
- `KEY idx_news_translations_lang (language_code)`
- `FULLTEXT KEY ft_news_translations_search (title, summary)`

**Foreign Keys:**
- `CONSTRAINT fk_news_translations_news FOREIGN KEY (news_id) REFERENCES news(news_id) ON UPDATE CASCADE ON DELETE CASCADE`

### `news_content_sections`

**Purpose:** Các khối nội dung chi tiết của bài viết, tối đa 10 section mỗi bản dịch

**Primary Key:**
- `PRIMARY KEY (section_id)`

**Columns:**

| Column | Type | Null | Default | Auto Inc | Notes |
|---|---|---:|---|---:|---|
| `section_id` | `BIGINT UNSIGNED` | NO | `` | YES |  |
| `news_translation_id` | `BIGINT UNSIGNED` | NO | `` |  |  |
| `section_order` | `TINYINT UNSIGNED` | NO | `` |  | Thứ tự section, từ 1 đến 10 |
| `section_title` | `VARCHAR(255)` | NO | `` |  | Tiêu đề section |
| `section_body_html` | `LONGTEXT` | NO | `` |  | Nội dung rich text dạng HTML đã sanitize, có thể chứa paragraph, bold, italic, color, link, image |
| `section_body_text` | `TEXT` | YES | `` |  | Plain text tách từ HTML để search hoặc preview |
| `created_at` | `DATETIME` | NO | `CURRENT_TIMESTAMP` |  |  |
| `updated_at` | `DATETIME` | YES | `NULL` |  |  |

**Unique Constraints:**
- `UNIQUE KEY uq_news_section_order (news_translation_id, section_order)`

**Indexes:**
- `KEY idx_news_sections_translation (news_translation_id)`
- `FULLTEXT KEY ft_news_sections_search (section_title, section_body_text)`

**Foreign Keys:**
- `CONSTRAINT fk_news_sections_translation FOREIGN KEY (news_translation_id) REFERENCES news_translations(news_translation_id) ON UPDATE CASCADE ON DELETE CASCADE`

**Check Constraints:**
- `CHECK (section_order BETWEEN 1 AND 10)`

### `news_section_files`

**Purpose:** File/ảnh được dùng trong từng section của bài news

**Primary Key:**
- `PRIMARY KEY (section_file_id)`

**Columns:**

| Column | Type | Null | Default | Auto Inc | Notes |
|---|---|---:|---|---:|---|
| `section_file_id` | `BIGINT UNSIGNED` | NO | `` | YES |  |
| `section_id` | `BIGINT UNSIGNED` | NO | `` |  |  |
| `file_id` | `BIGINT UNSIGNED` | NO | `` |  |  |
| `usage_type` | `ENUM('INLINE_IMAGE','ATTACHMENT')` | NO | `'INLINE_IMAGE'` |  | INLINE_IMAGE=ảnh chèn trong nội dung, ATTACHMENT=file đính kèm |
| `display_order` | `INT UNSIGNED` | NO | `0` |  |  |
| `created_at` | `DATETIME` | NO | `CURRENT_TIMESTAMP` |  |  |

**Unique Constraints:**
- `UNIQUE KEY uq_news_section_file (section_id, file_id)`

**Indexes:**
- `KEY idx_news_section_files_section (section_id)`
- `KEY idx_news_section_files_file (file_id)`

**Foreign Keys:**
- `CONSTRAINT fk_news_section_files_section FOREIGN KEY (section_id) REFERENCES news_content_sections(section_id) ON UPDATE CASCADE ON DELETE CASCADE`
- `CONSTRAINT fk_news_section_files_file FOREIGN KEY (file_id) REFERENCES files(file_id) ON UPDATE CASCADE ON DELETE RESTRICT`

### `faqs`

**Purpose:** FAQ một ngôn ngữ, chỉ dùng PUBLISHED/HIDDEN

**Primary Key:**
- `PRIMARY KEY (faq_id)`

**Columns:**

| Column | Type | Null | Default | Auto Inc | Notes |
|---|---|---:|---|---:|---|
| `faq_id` | `BIGINT UNSIGNED` | NO | `` | YES |  |
| `category` | `VARCHAR(100)` | YES | `` |  | Nhóm FAQ, ví dụ: Visit Request, Security, Logistics |
| `question` | `VARCHAR(500)` | NO | `` |  | Câu hỏi FAQ |
| `answer` | `TEXT` | NO | `` |  | Câu trả lời FAQ |
| `display_order` | `INT UNSIGNED` | NO | `0` |  |  |
| `status` | `ENUM('PUBLISHED','HIDDEN')` | NO | `'HIDDEN'` |  | PUBLISHED=hiển thị trên trang FAQ, HIDDEN=ẩn khỏi người xem thường nhưng người quản lý vẫn thấy |
| `created_at` | `DATETIME` | NO | `CURRENT_TIMESTAMP` |  |  |
| `created_by` | `BIGINT UNSIGNED` | YES | `` |  |  |
| `updated_at` | `DATETIME` | YES | `NULL` |  |  |
| `updated_by` | `BIGINT UNSIGNED` | YES | `` |  |  |

**Indexes:**
- `KEY idx_faqs_status_order (status, display_order)`
- `KEY idx_faqs_category_status (category, status)`
- `FULLTEXT KEY ft_faqs_search (question, answer)`

### `galleries`

**Purpose:** Gallery địa điểm trong campus, có mô tả và câu chuyện

**Primary Key:**
- `PRIMARY KEY (gallery_id)`

**Columns:**

| Column | Type | Null | Default | Auto Inc | Notes |
|---|---|---:|---|---:|---|
| `gallery_id` | `BIGINT UNSIGNED` | NO | `` | YES |  |
| `campus_id` | `BIGINT UNSIGNED` | NO | `` |  |  |
| `location_name` | `VARCHAR(150)` | NO | `` |  | Tên địa điểm trong campus, ví dụ: Sảnh Alpha, Green Lab, Thư viện |
| `title` | `VARCHAR(255)` | NO | `` |  | Tên hiển thị của gallery/địa điểm |
| `description` | `TEXT` | YES | `` |  | Mô tả ngắn về địa điểm |
| `story_content` | `TEXT` | YES | `` |  | Ý nghĩa hoặc câu chuyện giới thiệu về địa điểm |
| `status` | `ENUM('DRAFT','PUBLISHED','HIDDEN')` | NO | `'DRAFT'` |  | DRAFT=nháp, PUBLISHED=hiển thị theo visibility, HIDDEN=ẩn khỏi người xem thường nhưng Staff Leader vẫn quản lý được |
| `visibility` | `ENUM('PRIVATE','INTERNAL','PUBLIC')` | NO | `'INTERNAL'` |  | Phạm vi xem khi status=PUBLISHED: PRIVATE=chỉ quản lý, INTERNAL=user nội bộ, PUBLIC=công khai |
| `created_at` | `DATETIME` | NO | `CURRENT_TIMESTAMP` |  |  |
| `created_by` | `BIGINT UNSIGNED` | YES | `` |  |  |
| `updated_at` | `DATETIME` | YES | `NULL` |  |  |
| `updated_by` | `BIGINT UNSIGNED` | YES | `` |  |  |
| `deleted_at` | `DATETIME` | YES | `` |  |  |
| `deleted_by` | `BIGINT UNSIGNED` | YES | `` |  |  |

**Indexes:**
- `KEY idx_galleries_campus_status (campus_id, status, deleted_at)`
- `KEY idx_galleries_location_name (location_name)`
- `KEY idx_galleries_visibility_status (visibility, status)`

**Foreign Keys:**
- `CONSTRAINT fk_galleries_campus FOREIGN KEY (campus_id) REFERENCES campuses(campus_id) ON UPDATE CASCADE ON DELETE RESTRICT`

### `gallery_images`

**Purpose:** Ảnh thuộc gallery địa điểm campus

**Primary Key:**
- `PRIMARY KEY (image_id)`

**Columns:**

| Column | Type | Null | Default | Auto Inc | Notes |
|---|---|---:|---|---:|---|
| `image_id` | `BIGINT UNSIGNED` | NO | `` | YES |  |
| `gallery_id` | `BIGINT UNSIGNED` | NO | `` |  |  |
| `file_id` | `BIGINT UNSIGNED` | NO | `` |  |  |
| `caption` | `VARCHAR(500)` | YES | `` |  | Chú thích riêng cho từng ảnh |
| `display_order` | `INT UNSIGNED` | NO | `0` |  |  |
| `taken_at` | `DATETIME` | YES | `` |  |  |
| `status` | `ENUM('ACTIVE','HIDDEN')` | NO | `'ACTIVE'` |  | ACTIVE=ảnh đang dùng, HIDDEN=ảnh bị ẩn khỏi gallery thường |
| `created_at` | `DATETIME` | NO | `CURRENT_TIMESTAMP` |  |  |
| `created_by` | `BIGINT UNSIGNED` | YES | `` |  |  |
| `updated_at` | `DATETIME` | YES | `NULL` |  |  |
| `updated_by` | `BIGINT UNSIGNED` | YES | `` |  |  |
| `deleted_at` | `DATETIME` | YES | `` |  |  |
| `deleted_by` | `BIGINT UNSIGNED` | YES | `` |  |  |

**Unique Constraints:**
- `UNIQUE KEY uq_gallery_images_file (file_id)`

**Indexes:**
- `KEY idx_gallery_images_gallery_order (gallery_id, display_order)`
- `KEY idx_gallery_images_status_time (status, taken_at)`

**Foreign Keys:**
- `CONSTRAINT fk_gallery_images_gallery FOREIGN KEY (gallery_id) REFERENCES galleries(gallery_id) ON UPDATE CASCADE ON DELETE RESTRICT`
- `CONSTRAINT fk_gallery_images_file FOREIGN KEY (file_id) REFERENCES files(file_id) ON UPDATE CASCADE ON DELETE RESTRICT`

### `photo_face_tags`

**Purpose:** Confirmed face tag metadata only. No biometric vector.

**Primary Key:**
- `PRIMARY KEY (face_tag_id)`

**Columns:**

| Column | Type | Null | Default | Auto Inc | Notes |
|---|---|---:|---|---:|---|
| `face_tag_id` | `BIGINT UNSIGNED` | NO | `` | YES |  |
| `image_id` | `BIGINT UNSIGNED` | NO | `` |  |  |
| `visit_request_id` | `BIGINT UNSIGNED` | YES | `` |  |  |
| `guest_member_id` | `BIGINT UNSIGNED` | YES | `` |  |  |
| `partner_contact_id` | `BIGINT UNSIGNED` | YES | `` |  |  |
| `display_name` | `VARCHAR(150)` | NO | `` |  |  |
| `bounding_box_x` | `DECIMAL(8,4)` | YES | `` |  |  |
| `bounding_box_y` | `DECIMAL(8,4)` | YES | `` |  |  |
| `bounding_box_width` | `DECIMAL(8,4)` | YES | `` |  |  |
| `bounding_box_height` | `DECIMAL(8,4)` | YES | `` |  |  |
| `tag_status` | `ENUM('MANUALLY_TAGGED','CONFIRMED','REMOVED')` | NO | `'MANUALLY_TAGGED'` |  |  |
| `confirmed_by` | `BIGINT UNSIGNED` | YES | `` |  |  |
| `confirmed_at` | `DATETIME` | YES | `` |  |  |
| `created_at` | `DATETIME` | NO | `CURRENT_TIMESTAMP` |  |  |
| `created_by` | `BIGINT UNSIGNED` | YES | `` |  |  |
| `removed_at` | `DATETIME` | YES | `` |  |  |
| `removed_by` | `BIGINT UNSIGNED` | YES | `` |  |  |

**Indexes:**
- `KEY idx_face_tags_image (image_id)`
- `KEY idx_face_tags_guest (guest_member_id)`
- `KEY idx_face_tags_partner_contact (partner_contact_id)`
- `KEY idx_face_tags_status (tag_status)`

**Foreign Keys:**
- `CONSTRAINT fk_face_tags_image FOREIGN KEY (image_id) REFERENCES gallery_images(image_id) ON UPDATE CASCADE ON DELETE CASCADE`
- `CONSTRAINT fk_face_tags_visit_request FOREIGN KEY (visit_request_id) REFERENCES visit_requests(visit_request_id) ON UPDATE CASCADE ON DELETE SET NULL`
- `CONSTRAINT fk_face_tags_guest FOREIGN KEY (guest_member_id) REFERENCES visit_guest_members(guest_member_id) ON UPDATE CASCADE ON DELETE SET NULL`
- `CONSTRAINT fk_face_tags_partner_contact FOREIGN KEY (partner_contact_id) REFERENCES partner_contacts(contact_id) ON UPDATE CASCADE ON DELETE SET NULL`
- `CONSTRAINT fk_face_tags_confirmed_by FOREIGN KEY (confirmed_by) REFERENCES users(user_id) ON UPDATE CASCADE ON DELETE SET NULL`

### `email_templates`

**Purpose:** Email templates with translations_json

**Primary Key:**
- `PRIMARY KEY (email_template_id)`

**Columns:**

| Column | Type | Null | Default | Auto Inc | Notes |
|---|---|---:|---|---:|---|
| `email_template_id` | `BIGINT UNSIGNED` | NO | `` | YES |  |
| `template_code` | `VARCHAR(100)` | NO | `` |  |  |
| `name` | `VARCHAR(150)` | NO | `` |  |  |
| `purpose` | `VARCHAR(100)` | NO | `` |  |  |
| `status` | `ENUM('ACTIVE','INACTIVE')` | NO | `'ACTIVE'` |  |  |
| `translations_json` | `JSON` | NO | `` |  | Merged email_template_translations table |
| `variables_json` | `JSON` | YES | `` |  | Allowed variables: FullName, OtpCode, Link... |
| `created_at` | `DATETIME` | NO | `CURRENT_TIMESTAMP` |  |  |
| `created_by` | `BIGINT UNSIGNED` | YES | `` |  |  |
| `updated_at` | `DATETIME` | YES | `NULL` |  |  |
| `updated_by` | `BIGINT UNSIGNED` | YES | `` |  |  |

**Unique Constraints:**
- `UNIQUE KEY uq_email_templates_code (template_code)`

**Indexes:**
- `KEY idx_email_templates_status (status)`
- `KEY idx_email_templates_purpose_status (purpose, status)`

### `sent_emails`

**Purpose:** Sent email log with recipients_json

**Primary Key:**
- `PRIMARY KEY (sent_email_id)`

**Columns:**

| Column | Type | Null | Default | Auto Inc | Notes |
|---|---|---:|---|---:|---|
| `sent_email_id` | `BIGINT UNSIGNED` | NO | `` | YES |  |
| `email_template_id` | `BIGINT UNSIGNED` | YES | `` |  |  |
| `related_type` | `VARCHAR(80)` | YES | `` |  |  |
| `related_id` | `BIGINT UNSIGNED` | YES | `` |  |  |
| `subject` | `VARCHAR(255)` | NO | `` |  |  |
| `body_snapshot` | `LONGTEXT` | YES | `` |  |  |
| `recipients_json` | `JSON` | NO | `` |  | Merged sent_email_recipients table |
| `metadata_json` | `JSON` | YES | `` |  | provider message id, retry count, etc. |
| `status` | `ENUM('QUEUED','SENT','FAILED')` | NO | `'QUEUED'` |  |  |
| `error_message` | `TEXT` | YES | `` |  |  |
| `sent_by` | `BIGINT UNSIGNED` | YES | `` |  |  |
| `sent_at` | `DATETIME` | YES | `` |  |  |
| `created_at` | `DATETIME` | NO | `CURRENT_TIMESTAMP` |  |  |

**Indexes:**
- `KEY idx_sent_emails_template (email_template_id)`
- `KEY idx_sent_emails_related (related_type, related_id)`
- `KEY idx_sent_emails_status_time (status, created_at)`
- `KEY idx_sent_emails_sent_by_time (sent_by, sent_at)`

**Foreign Keys:**
- `CONSTRAINT fk_sent_emails_template FOREIGN KEY (email_template_id) REFERENCES email_templates(email_template_id) ON UPDATE CASCADE ON DELETE SET NULL`
- `CONSTRAINT fk_sent_emails_sent_by FOREIGN KEY (sent_by) REFERENCES users(user_id) ON UPDATE CASCADE ON DELETE SET NULL`

### `notifications`

**Purpose:** In-app notifications

**Primary Key:**
- `PRIMARY KEY (notification_id)`

**Columns:**

| Column | Type | Null | Default | Auto Inc | Notes |
|---|---|---:|---|---:|---|
| `notification_id` | `BIGINT UNSIGNED` | NO | `` | YES |  |
| `recipient_user_id` | `BIGINT UNSIGNED` | NO | `` |  |  |
| `title` | `VARCHAR(255)` | NO | `` |  |  |
| `message` | `TEXT` | YES | `` |  |  |
| `notification_type` | `VARCHAR(80)` | NO | `` |  |  |
| `related_type` | `VARCHAR(80)` | YES | `` |  |  |
| `related_id` | `BIGINT UNSIGNED` | YES | `` |  |  |
| `is_read` | `BOOLEAN` | NO | `FALSE` |  |  |
| `read_at` | `DATETIME` | YES | `` |  |  |
| `created_at` | `DATETIME` | NO | `CURRENT_TIMESTAMP` |  |  |

**Indexes:**
- `KEY idx_notifications_user_read_time (recipient_user_id, is_read, created_at)`
- `KEY idx_notifications_related (related_type, related_id)`
- `KEY idx_notifications_type_time (notification_type, created_at)`

**Foreign Keys:**
- `CONSTRAINT fk_notifications_user FOREIGN KEY (recipient_user_id) REFERENCES users(user_id) ON UPDATE CASCADE ON DELETE CASCADE`

### `calendar_events`

**Purpose:** Calendar events. Personal/visit/logistics/deadline events. Attendees/reminders merged into JSON fields.

**Primary Key:**
- `PRIMARY KEY (calendar_event_id)`

**Columns:**

| Column | Type | Null | Default | Auto Inc | Notes |
|---|---|---:|---|---:|---|
| `calendar_event_id` | `BIGINT UNSIGNED` | NO | `` | YES |  |
| `owner_user_id` | `BIGINT UNSIGNED` | NO | `` |  |  |
| `campus_id` | `BIGINT UNSIGNED` | YES | `` |  |  |
| `visit_instance_id` | `BIGINT UNSIGNED` | YES | `` |  |  |
| `logistics_item_id` | `BIGINT UNSIGNED` | YES | `` |  |  |
| `source_type` | `ENUM('PERSONAL','VISIT','LOGISTICS','DEADLINE')` | NO | `'PERSONAL'` |  |  |
| `title` | `VARCHAR(255)` | NO | `` |  |  |
| `description` | `TEXT` | YES | `` |  |  |
| `location` | `VARCHAR(255)` | YES | `` |  |  |
| `start_at` | `DATETIME` | NO | `` |  |  |
| `end_at` | `DATETIME` | NO | `` |  |  |
| `timezone` | `VARCHAR(50)` | NO | `'Asia/Ho_Chi_Minh'` |  |  |
| `visibility` | `ENUM('PRIVATE','INTERNAL')` | NO | `'PRIVATE'` |  |  |
| `attendees_json` | `JSON` | YES | `` |  | Merged calendar_event_attendees table |
| `reminders_json` | `JSON` | YES | `` |  | Merged calendar_event_reminders table |
| `status` | `ENUM('ACTIVE','CANCELLED','DONE')` | NO | `'ACTIVE'` |  |  |
| `created_at` | `DATETIME` | NO | `CURRENT_TIMESTAMP` |  |  |
| `created_by` | `BIGINT UNSIGNED` | YES | `` |  |  |
| `updated_at` | `DATETIME` | YES | `NULL` |  |  |
| `updated_by` | `BIGINT UNSIGNED` | YES | `` |  |  |
| `deleted_at` | `DATETIME` | YES | `` |  |  |
| `deleted_by` | `BIGINT UNSIGNED` | YES | `` |  |  |

**Indexes:**
- `KEY idx_calendar_owner_time (owner_user_id, start_at)`
- `KEY idx_calendar_campus_time (campus_id, start_at)`
- `KEY idx_calendar_visit (visit_instance_id)`
- `KEY idx_calendar_logistics (logistics_item_id)`
- `KEY idx_calendar_source_status_time (source_type, status, start_at)`

**Foreign Keys:**
- `CONSTRAINT fk_calendar_owner FOREIGN KEY (owner_user_id) REFERENCES users(user_id) ON UPDATE CASCADE ON DELETE CASCADE`
- `CONSTRAINT fk_calendar_campus FOREIGN KEY (campus_id) REFERENCES campuses(campus_id) ON UPDATE CASCADE ON DELETE SET NULL`
- `CONSTRAINT fk_calendar_visit FOREIGN KEY (visit_instance_id) REFERENCES visit_request_campuses(visit_instance_id) ON UPDATE CASCADE ON DELETE SET NULL`
- `CONSTRAINT fk_calendar_logistics FOREIGN KEY (logistics_item_id) REFERENCES visit_logistics_items(logistics_item_id) ON UPDATE CASCADE ON DELETE SET NULL`

**Check Constraints:**
- `CHECK (end_at > start_at)`

### `api_configurations`

**Purpose:** API config + encrypted credentials JSON

**Primary Key:**
- `PRIMARY KEY (api_config_id)`

**Columns:**

| Column | Type | Null | Default | Auto Inc | Notes |
|---|---|---:|---|---:|---|
| `api_config_id` | `BIGINT UNSIGNED` | NO | `` | YES |  |
| `api_code` | `VARCHAR(100)` | NO | `` |  |  |
| `name` | `VARCHAR(150)` | NO | `` |  |  |
| `provider_name` | `VARCHAR(150)` | YES | `` |  |  |
| `purpose` | `VARCHAR(150)` | YES | `` |  |  |
| `base_url` | `VARCHAR(500)` | NO | `` |  |  |
| `default_method` | `ENUM('GET','POST','PUT','PATCH','DELETE')` | NO | `'POST'` |  |  |
| `auth_type` | `ENUM('NONE','API_KEY','BEARER_TOKEN','BASIC','OAUTH2','CUSTOM')` | NO | `'NONE'` |  |  |
| `credentials_json` | `JSON` | YES | `` |  | Encrypted/masked credentials. Merged api_credentials table. |
| `headers_json` | `JSON` | YES | `` |  |  |
| `body_template_json` | `JSON` | YES | `` |  |  |
| `settings_json` | `JSON` | YES | `` |  |  |
| `timeout_seconds` | `INT UNSIGNED` | NO | `30` |  |  |
| `status` | `ENUM('ACTIVE','INACTIVE','DISABLED')` | NO | `'ACTIVE'` |  |  |
| `created_at` | `DATETIME` | NO | `CURRENT_TIMESTAMP` |  |  |
| `created_by` | `BIGINT UNSIGNED` | YES | `` |  |  |
| `updated_at` | `DATETIME` | YES | `NULL` |  |  |
| `updated_by` | `BIGINT UNSIGNED` | YES | `` |  |  |
| `deleted_at` | `DATETIME` | YES | `` |  |  |
| `deleted_by` | `BIGINT UNSIGNED` | YES | `` |  |  |

**Unique Constraints:**
- `UNIQUE KEY uq_api_config_code (api_code)`

**Indexes:**
- `KEY idx_api_config_status (status)`
- `KEY idx_api_provider_status (provider_name, status)`

### `api_usage_quotas`

**Purpose:** API quota + counter per campus/month

**Primary Key:**
- `PRIMARY KEY (api_usage_quota_id)`

**Columns:**

| Column | Type | Null | Default | Auto Inc | Notes |
|---|---|---:|---|---:|---|
| `api_usage_quota_id` | `BIGINT UNSIGNED` | NO | `` | YES |  |
| `api_config_id` | `BIGINT UNSIGNED` | NO | `` |  |  |
| `campus_id` | `BIGINT UNSIGNED` | YES | `` |  | NULL = global quota |
| `campus_scope_key` | `VARCHAR(36)` | NO | `'GLOBAL'` |  |  |
| `period_yyyymm` | `CHAR(6)` | NO | `` |  | YYYYMM |
| `monthly_limit` | `INT UNSIGNED` | NO | `` |  |  |
| `used_count` | `INT UNSIGNED` | NO | `0` |  | Merged api_usage_counters table |
| `last_used_at` | `DATETIME` | YES | `` |  |  |
| `created_at` | `DATETIME` | NO | `CURRENT_TIMESTAMP` |  |  |
| `created_by` | `BIGINT UNSIGNED` | YES | `` |  |  |
| `updated_at` | `DATETIME` | YES | `NULL` |  |  |
| `updated_by` | `BIGINT UNSIGNED` | YES | `` |  |  |

**Unique Constraints:**
- `UNIQUE KEY uq_api_quota_config_scope_period (api_config_id, campus_scope_key, period_yyyymm)`

**Indexes:**
- `KEY idx_api_quota_campus_period (campus_id, period_yyyymm)`
- `KEY idx_api_quota_period (period_yyyymm)`

**Foreign Keys:**
- `CONSTRAINT fk_api_quota_config FOREIGN KEY (api_config_id) REFERENCES api_configurations(api_config_id) ON UPDATE CASCADE ON DELETE CASCADE`
- `CONSTRAINT fk_api_quota_campus FOREIGN KEY (campus_id) REFERENCES campuses(campus_id) ON UPDATE CASCADE ON DELETE CASCADE`

### `api_request_logs`

**Purpose:** External API request logs. Never log full secret/token.

**Primary Key:**
- `PRIMARY KEY (api_request_log_id)`

**Columns:**

| Column | Type | Null | Default | Auto Inc | Notes |
|---|---|---:|---|---:|---|
| `api_request_log_id` | `BIGINT UNSIGNED` | NO | `` | YES |  |
| `api_config_id` | `BIGINT UNSIGNED` | NO | `` |  |  |
| `campus_id` | `BIGINT UNSIGNED` | YES | `` |  |  |
| `requested_by` | `BIGINT UNSIGNED` | YES | `` |  |  |
| `related_type` | `VARCHAR(80)` | YES | `` |  |  |
| `related_id` | `BIGINT UNSIGNED` | YES | `` |  |  |
| `endpoint` | `VARCHAR(500)` | NO | `` |  |  |
| `method` | `ENUM('GET','POST','PUT','PATCH','DELETE')` | NO | `` |  |  |
| `http_status` | `INT` | YES | `` |  |  |
| `response_time_ms` | `INT UNSIGNED` | YES | `` |  |  |
| `request_size_bytes` | `BIGINT UNSIGNED` | YES | `` |  |  |
| `response_size_bytes` | `BIGINT UNSIGNED` | YES | `` |  |  |
| `success` | `BOOLEAN` | NO | `FALSE` |  |  |
| `error_code` | `VARCHAR(100)` | YES | `` |  |  |
| `error_message` | `TEXT` | YES | `` |  |  |
| `created_at` | `DATETIME` | NO | `CURRENT_TIMESTAMP` |  |  |

**Indexes:**
- `KEY idx_api_logs_config_time (api_config_id, created_at)`
- `KEY idx_api_logs_campus_time (campus_id, created_at)`
- `KEY idx_api_logs_user_time (requested_by, created_at)`
- `KEY idx_api_logs_success_time (success, created_at)`
- `KEY idx_api_logs_related (related_type, related_id)`

**Foreign Keys:**
- `CONSTRAINT fk_api_logs_config FOREIGN KEY (api_config_id) REFERENCES api_configurations(api_config_id) ON UPDATE CASCADE ON DELETE RESTRICT`
- `CONSTRAINT fk_api_logs_campus FOREIGN KEY (campus_id) REFERENCES campuses(campus_id) ON UPDATE CASCADE ON DELETE SET NULL`
- `CONSTRAINT fk_api_logs_user FOREIGN KEY (requested_by) REFERENCES users(user_id) ON UPDATE CASCADE ON DELETE SET NULL`

### `agenda_templates`

**Purpose:** Agenda template with items_json

**Primary Key:**
- `PRIMARY KEY (agenda_template_id)`

**Columns:**

| Column | Type | Null | Default | Auto Inc | Notes |
|---|---|---:|---|---:|---|
| `agenda_template_id` | `BIGINT UNSIGNED` | NO | `` | YES |  |
| `campus_id` | `BIGINT UNSIGNED` | YES | `` |  |  |
| `campus_scope_key` | `VARCHAR(36)` | NO | `'GLOBAL'` |  |  |
| `name` | `VARCHAR(150)` | NO | `` |  |  |
| `description` | `TEXT` | YES | `` |  |  |
| `items_json` | `JSON` | NO | `` |  | Merged agenda_template_items table |
| `status` | `ENUM('ACTIVE','INACTIVE')` | NO | `'ACTIVE'` |  |  |
| `created_at` | `DATETIME` | NO | `CURRENT_TIMESTAMP` |  |  |
| `created_by` | `BIGINT UNSIGNED` | YES | `` |  |  |
| `updated_at` | `DATETIME` | YES | `NULL` |  |  |
| `updated_by` | `BIGINT UNSIGNED` | YES | `` |  |  |
| `deleted_at` | `DATETIME` | YES | `` |  |  |
| `deleted_by` | `BIGINT UNSIGNED` | YES | `` |  |  |

**Unique Constraints:**
- `UNIQUE KEY uq_agenda_template_scope_name (campus_scope_key, name)`

**Indexes:**
- `KEY idx_agenda_templates_status (status)`
- `KEY idx_agenda_templates_campus_status (campus_id, status)`

**Foreign Keys:**
- `CONSTRAINT fk_agenda_templates_campus FOREIGN KEY (campus_id) REFERENCES campuses(campus_id) ON UPDATE CASCADE ON DELETE SET NULL`

### `audit_logs`

**Purpose:** General audit log

**Primary Key:**
- `PRIMARY KEY (audit_log_id)`

**Columns:**

| Column | Type | Null | Default | Auto Inc | Notes |
|---|---|---:|---|---:|---|
| `audit_log_id` | `BIGINT UNSIGNED` | NO | `` | YES |  |
| `actor_user_id` | `BIGINT UNSIGNED` | YES | `` |  |  |
| `campus_id` | `BIGINT UNSIGNED` | YES | `` |  |  |
| `action` | `VARCHAR(100)` | NO | `` |  |  |
| `entity_type` | `VARCHAR(100)` | NO | `` |  |  |
| `entity_id` | `BIGINT UNSIGNED` | YES | `` |  |  |
| `old_values_json` | `JSON` | YES | `` |  |  |
| `new_values_json` | `JSON` | YES | `` |  |  |
| `ip_address` | `VARCHAR(45)` | YES | `` |  |  |
| `user_agent` | `VARCHAR(500)` | YES | `` |  |  |
| `request_id` | `VARCHAR(100)` | YES | `` |  |  |
| `created_at` | `DATETIME` | NO | `CURRENT_TIMESTAMP` |  |  |

**Indexes:**
- `KEY idx_audit_actor_time (actor_user_id, created_at)`
- `KEY idx_audit_entity (entity_type, entity_id)`
- `KEY idx_audit_action_time (action, created_at)`
- `KEY idx_audit_campus_time (campus_id, created_at)`
- `KEY idx_audit_request (request_id)`

**Foreign Keys:**
- `CONSTRAINT fk_audit_actor FOREIGN KEY (actor_user_id) REFERENCES users(user_id) ON UPDATE CASCADE ON DELETE SET NULL`
- `CONSTRAINT fk_audit_campus FOREIGN KEY (campus_id) REFERENCES campuses(campus_id) ON UPDATE CASCADE ON DELETE SET NULL`

### `visit_status_logs`

**Purpose:** Timeline trạng thái visit. Log rõ cấp REQUEST hoặc CAMPUS_INSTANCE để không nhầm request_status với campus_status.

**Primary Key:**
- `PRIMARY KEY (visit_status_log_id)`

**Columns:**

| Column | Type | Null | Default | Auto Inc | Notes |
|---|---|---:|---|---:|---|
| `visit_status_log_id` | `BIGINT UNSIGNED` | NO | `` | YES |  |
| `visit_request_id` | `BIGINT UNSIGNED` | YES | `` |  |  |
| `visit_instance_id` | `BIGINT UNSIGNED` | YES | `` |  |  |
| `status_owner_type` | `ENUM('REQUEST','CAMPUS_INSTANCE')` | NO | `'CAMPUS_INSTANCE'` |  | REQUEST=visit_requests.status, CAMPUS_INSTANCE=visit_request_campuses.status |
| `old_status` | `VARCHAR(50)` | YES | `` |  |  |
| `new_status` | `VARCHAR(50)` | NO | `` |  |  |
| `changed_by` | `BIGINT UNSIGNED` | YES | `` |  |  |
| `reason` | `TEXT` | YES | `` |  |  |
| `changed_at` | `DATETIME` | NO | `CURRENT_TIMESTAMP` |  |  |

**Indexes:**
- `KEY idx_visit_status_request_time (visit_request_id, changed_at)`
- `KEY idx_visit_status_instance_time (visit_instance_id, changed_at)`
- `KEY idx_visit_status_owner_time (status_owner_type, changed_at)`
- `KEY idx_visit_status_changed_by_time (changed_by, changed_at)`

**Foreign Keys:**
- `CONSTRAINT fk_visit_status_logs_request FOREIGN KEY (visit_request_id) REFERENCES visit_requests(visit_request_id) ON UPDATE CASCADE ON DELETE SET NULL`
- `CONSTRAINT fk_visit_status_logs_instance FOREIGN KEY (visit_instance_id) REFERENCES visit_request_campuses(visit_instance_id) ON UPDATE CASCADE ON DELETE SET NULL`
- `CONSTRAINT fk_visit_status_logs_changed_by FOREIGN KEY (changed_by) REFERENCES users(user_id) ON UPDATE CASCADE ON DELETE SET NULL`

## 5. Views

### `vw_visit_requests_for_ho`

```sql
SELECT vr.visit_request_id, vr.request_code, vr.delegation_name, vr.visit_scope, vr.status AS request_status, vr.submitted_at, vr.decided_by, vr.decided_at, vr.decision_actor_role, vr.decision_note, CASE WHEN vr.visit_scope = 'MULTI_CAMPUS' AND vr.status = 'PENDING_APPROVAL' THEN 'WAITING_HO_APPROVAL' WHEN vr.visit_scope = 'MULTI_CAMPUS' AND vr.status = 'APPROVED' AND vr.decision_actor_role = 'HO' THEN 'HO_APPROVED' WHEN vr.status = 'REJECTED' THEN 'REJECTED' WHEN vr.status = 'CANCELLED' THEN 'CANCELLED' ELSE vr.status END AS approval_display_status FROM visit_requests vr WHERE vr.visit_scope = 'MULTI_CAMPUS'
```

### `vw_visit_requests_for_staff_leader`

```sql
SELECT vr.visit_request_id, vrc.visit_instance_id, vrc.campus_id AS visible_campus_id, vrc.current_host_user_id, vrc.host_assigned_by, vrc.host_assigned_at, vrc.host_assignment_source, vrc.host_transferred_by, vrc.host_transferred_at, vr.request_code, vr.delegation_name, vr.visit_scope, vr.status AS request_status, vrc.status AS campus_status, vr.submitted_at, vr.decided_by, vr.decided_at, vr.decision_actor_role, vr.decision_note, CASE WHEN vr.visit_scope = 'SINGLE_CAMPUS' AND vr.status = 'PENDING_APPROVAL' THEN 'WAITING_STAFF_LEADER_APPROVAL' WHEN vr.visit_scope = 'SINGLE_CAMPUS' AND vr.status = 'APPROVED' AND vr.decision_actor_role = 'STAFF_LEADER' THEN 'STAFF_LEADER_APPROVED' WHEN vr.visit_scope = 'MULTI_CAMPUS' AND vr.status = 'APPROVED' AND vr.decision_actor_role = 'HO' THEN 'HO_APPRO ...
```

### `vw_visit_requests_for_ho`

```sql
SELECT vr.visit_request_id, vr.request_code, vr.visitor_user_id, vr.partner_id, vr.registrant_full_name, vr.registrant_organization, vr.registrant_job_title, vr.registrant_phone, vr.registrant_email, vr.registrant_nationality, vr.delegation_name, vr.visit_scope, vr.purpose, vr.working_content, vr.expected_guest_count, vr.working_language, vr.status AS request_status, vr.submitted_at, vr.email_verified_at, vr.decided_by, vr.decided_at, vr.decision_actor_role, vr.decision_note, vr.row_version, vr.created_at, vr.updated_at, CASE WHEN vr.status = 'PENDING_APPROVAL' THEN 'WAITING_HO_APPROVAL' WHEN vr.status = 'APPROVED' AND vr.decision_actor_role = 'HO' THEN 'HO_APPROVED' WHEN vr.status = 'REJECTED' THEN 'REJECTED' WHEN vr.status = 'CANCELLED' THEN 'CANCELLED' ELSE vr.status END AS approval_dis ...
```

### `vw_visit_requests_for_staff_leader`

```sql
SELECT vr.visit_request_id, vrc.visit_instance_id, vrc.campus_id AS visible_campus_id, vrc.current_host_user_id, vrc.host_assigned_by, vrc.host_assigned_at, vrc.host_assignment_source, vrc.host_transferred_by, vrc.host_transferred_at, vr.request_code, vr.visitor_user_id, vr.partner_id, vr.registrant_full_name, vr.registrant_organization, vr.registrant_job_title, vr.registrant_phone, vr.registrant_email, vr.registrant_nationality, vr.delegation_name, vr.visit_scope, vr.purpose, vr.working_content, vr.expected_guest_count, vr.working_language, vr.status AS request_status, vrc.status AS campus_status, vrc.planned_start_at, vrc.planned_end_at, vr.submitted_at, vr.email_verified_at, vr.decided_by, vr.decided_at, vr.decision_actor_role, vr.decision_note, vr.row_version AS request_row_version, vr ...
```

### `vw_visit_requests_for_admin`

```sql
SELECT vr.visit_request_id, vr.request_code, vr.visit_scope, vr.status AS request_status, vr.submitted_at, vr.decided_by, vr.decided_at, vr.decision_actor_role, 'ADMIN_NO_VISIT_ACCESS' AS approval_display_status FROM visit_requests vr WHERE 1 = 0
```

### `vw_visit_request_progress_summary`

```sql
SELECT vr.visit_request_id, vr.request_code, vr.visit_scope, vr.status AS request_status, CASE WHEN vr.status = 'PENDING_APPROVAL' THEN 'WAITING_APPROVAL' WHEN vr.status = 'REJECTED' THEN 'REJECTED' WHEN vr.status = 'CANCELLED' THEN 'CANCELLED' WHEN COUNT(vrc.visit_instance_id) = 0 THEN 'APPROVED' WHEN SUM(vrc.status = 'DURING_VISIT') > 0 THEN 'IN_PROGRESS' WHEN SUM(vrc.status = 'AFTER_VISIT') > 0 THEN 'AFTER_VISIT' WHEN SUM(vrc.status = 'BEFORE_VISIT') > 0 THEN 'PREPARING' WHEN SUM(vrc.status = 'ASSIGNED') > 0 THEN 'ASSIGNED' WHEN SUM(vrc.status = 'CLOSED') = COUNT(vrc.visit_instance_id) THEN 'COMPLETED' ELSE 'APPROVED' END AS progress_status, COUNT(vrc.visit_instance_id) AS campus_count, SUM(vrc.status = 'WAITING_REQUEST_APPROVAL') AS waiting_campus_count, SUM(vrc.status = 'ASSIGNED') AS ...
```

## 6. Triggers

| Trigger | Timing | Event | Table |
|---|---|---|---|
| `trg_departments_one_ic_bi` | BEFORE | INSERT | `departments` |
| `trg_departments_one_ic_bu` | BEFORE | UPDATE | `departments` |
| `trg_users_validate_bi` | BEFORE | INSERT | `users` |
| `trg_users_validate_bu` | BEFORE | UPDATE | `users` |
| `trg_auth_providers_validate_bi` | BEFORE | INSERT | `user_auth_providers` |
| `trg_auth_providers_validate_bu` | BEFORE | UPDATE | `user_auth_providers` |
| `trg_sessions_validate_bi` | BEFORE | INSERT | `user_sessions` |
| `trg_visit_requests_decision_validate_bi` | BEFORE | INSERT | `visit_requests` |
| `trg_visit_requests_decision_validate_bu` | BEFORE | UPDATE | `visit_requests` |
| `trg_visit_requests_cancel_validate_bu` | BEFORE | UPDATE | `visit_requests` |
| `trg_visit_campuses_cancel_validate_bu` | BEFORE | UPDATE | `visit_request_campuses` |
| `trg_visit_campuses_assignment_validate_bi` | BEFORE | INSERT | `visit_request_campuses` |
| `trg_visit_campuses_assignment_validate_bu` | BEFORE | UPDATE | `visit_request_campuses` |
| `trg_api_usage_quotas_scope_bi` | BEFORE | INSERT | `api_usage_quotas` |
| `trg_api_usage_quotas_scope_bu` | BEFORE | UPDATE | `api_usage_quotas` |
| `trg_agenda_templates_scope_bi` | BEFORE | INSERT | `agenda_templates` |
| `trg_agenda_templates_scope_bu` | BEFORE | UPDATE | `agenda_templates` |
| `trg_feedbacks_not_self_bi` | BEFORE | INSERT | `feedbacks` |
| `trg_feedbacks_not_self_bu` | BEFORE | UPDATE | `feedbacks` |

## 7. Verification Queries

Run these after importing the SQL full-create file.

```sql
SELECT COUNT(*) AS base_table_count
FROM information_schema.tables
WHERE table_schema = DATABASE() AND table_type = 'BASE TABLE';

SELECT table_name, column_name, column_type, extra
FROM information_schema.columns
WHERE table_schema = DATABASE()
  AND extra LIKE '%auto_increment%'
ORDER BY table_name;

SELECT table_name, column_name
FROM information_schema.columns
WHERE table_schema = DATABASE()
  AND column_type LIKE 'char(36)%';

SELECT table_name, column_name
FROM information_schema.columns
WHERE table_schema = DATABASE()
  AND column_name IN ('actual_start_at','actual_end_at','external_confirmation_note');

SELECT permission_code, permission_group
FROM permissions
WHERE permission_code = 'UC-136.CANCEL_VISIT_REQUEST';
```

## 8. Static Generation Check

| Check | Result |
|---|---:|
| Parsed base tables | `42` |
| Parsed views | `6` |
| Parsed triggers | `19` |
| `external_confirmation_note` occurrences in SQL | `0` |
| `UC-136.CANCEL_VISIT_REQUEST` occurrences in SQL | `8` |
| `CHAR(36)` occurrences in SQL | `4` |
