# PEMS Database Schema — FULL v8.4 Fresh Create Only Idempotent Seed

> **Generated from:** `pems_full_seed_logic_v8_4_fresh_create_only_idempotent_seed.sql`  
> **Purpose:** Full developer-facing database schema reference generated directly from the latest SQL source of truth.  
> **Replacement note:** Use this file to replace the old `docs/database/DATABASE_SCHEMA.md` that still describes the v8.2 / 42-table schema.  
> **SQL style:** Fresh create-only schema; no migration/backfill `ALTER TABLE` or `UPDATE` logic is used in this schema reference.  

## 1. Overview
| Item | Value |
|---|---|
| Database | `pems_db` |
| Engine | MySQL 8.0 / InnoDB |
| Charset / Collation | `utf8mb4` / `utf8mb4_unicode_ci` |
| Schema Version | `PEMS v8.4 fresh-create-only idempotent seed` |
| Base Table Count | `50` |
| Total Column Count | `724` |
| Primary Key Strategy | `BIGINT UNSIGNED AUTO_INCREMENT` for base-table PKs |
| Visit Request Status | `PENDING_APPROVAL`, `APPROVED`, `REJECTED`, `CANCELLED` only |
| Campus Visit Status | `WAITING_REQUEST_APPROVAL`, `ASSIGNED`, `BEFORE_VISIT`, `DURING_VISIT`, `AFTER_VISIT`, `CLOSED`, `CANCELLED` |
| Cancellation Source | `SELF_SERVICE`, `EXTERNAL_CONFIRMATION`, `INTERNAL_DECISION` |
| JSON Data Type Columns | `0` — normalized into explicit columns or child tables |
| New v8.4 Tables | `minute_participants`, `feedback_rating_items`, `sent_email_recipients`, `calendar_event_attendees`, `calendar_event_reminders`, `api_configuration_headers`, `agenda_template_items`, `audit_log_changes` |

## 2. v8.4 Normalization / Override Notes

- SQL v8.4 is the current source of truth. If older docs mention v8.2/v8.3 tables or JSON columns, prefer this schema.
- `public_content_blocks` and `gallery_locations` are not part of the schema.
- `files.visibility` is not used. File visibility must be inferred from the business entity that references `files.file_id`.
- The following legacy JSON columns are not present as table columns: `support_team_json`, `contact_person_json`, `participants_json`, `translations_json`, `variables_json`, `recipients_json`, `metadata_json`, `attendees_json`, `reminders_json`, `credentials_json`, `headers_json`, `body_template_json`, `settings_json`, `items_json`, `old_values_json`, `new_values_json`.
- `visit_requests.working_language` only supports `VI` and `EN`. There is no `working_language_other`.
- `security_events` is SSO/session oriented and does not use free-form JSON metadata columns.
- `visit_guest_members.member_type` distinguishes `GUEST` from `EXTERNAL_SUPPORT`; support members are optional and must not be stored as JSON.

## 3. Table List
| # | Table | Module / Main Screens | Column Count |
|---:|---|---|---:|
| 1 | `roles` | RBAC / Account Management | 8 |
| 2 | `permissions` | RBAC / Permission Management | 7 |
| 3 | `role_permissions` | RBAC / Permission Matrix | 7 |
| 4 | `campuses` | Campus Management / Internal Portal Login | 13 |
| 5 | `departments` | Department Management / Logistics Assignment | 11 |
| 6 | `users` | Account Management / Auth / RBAC | 25 |
| 7 | `user_auth_providers` | Authentication / SSO / Dev Login | 8 |
| 8 | `user_sessions` | Authentication / Session Management | 15 |
| 9 | `otp_tokens` | OTP Verification / Visit Request Verify | 14 |
| 10 | `login_logs` | Authentication Audit | 12 |
| 11 | `security_events` | Security Audit / SSO Event Log | 15 |
| 12 | `files` | File Storage Metadata | 14 |
| 13 | `partners` | Partner Management / Public Partners / Visit Request Partner Link | 23 |
| 14 | `partner_contacts` | Partner Contact / Business Card Scan | 17 |
| 15 | `documents` | Document Management | 13 |
| 16 | `visit_requests` | Public Submit Visit Request / Internal Delegation / Approval / Cancel | 46 |
| 17 | `visit_request_campuses` | Delegation Campus Instance / Host Assignment / Status Flow | 27 |
| 18 | `visit_guest_members` | Guest List / External Support Members | 16 |
| 19 | `visit_participants` | Internal Participants / Hosts / Student Support | 16 |
| 20 | `visit_agendas` | Visit Agenda / Itinerary | 13 |
| 21 | `visit_logistics_items` | Visit Logistics / Resource Request / Service Report | 45 |
| 22 | `minutes` | Meeting Minutes | 11 |
| 23 | `minute_participants` | Meeting Minutes Participants | 10 |
| 24 | `minute_action_items` | Meeting Minutes Action Items | 12 |
| 25 | `feedbacks` | Delegation Feedback | 14 |
| 26 | `feedback_rating_items` | Feedback Criteria Ratings | 7 |
| 27 | `news` | News Management | 17 |
| 28 | `news_translations` | News Multilingual Content | 10 |
| 29 | `news_content_sections` | News Rich Content Sections | 8 |
| 30 | `news_section_files` | News Attachments / Inline Images | 6 |
| 31 | `faqs` | FAQ Management / Public FAQ | 11 |
| 32 | `galleries` | Gallery Management / Public Gallery | 18 |
| 33 | `gallery_images` | Gallery Media Items | 15 |
| 34 | `photo_face_tags` | Photo Face Tagging | 17 |
| 35 | `email_templates` | Email Template Management | 16 |
| 36 | `sent_emails` | Sent Email Log | 16 |
| 37 | `sent_email_recipients` | Sent Email Recipients | 11 |
| 38 | `notifications` | Notification Center | 10 |
| 39 | `calendar_events` | Calendar / My Events / Department Calendar | 22 |
| 40 | `calendar_event_attendees` | Calendar Attendees | 8 |
| 41 | `calendar_event_reminders` | Calendar Reminders | 8 |
| 42 | `api_configurations` | API Configuration Management | 33 |
| 43 | `api_configuration_headers` | API Request Headers | 6 |
| 44 | `api_usage_quotas` | API Quota Management | 12 |
| 45 | `api_request_logs` | API Request Logs | 16 |
| 46 | `agenda_templates` | Agenda Template Management | 12 |
| 47 | `agenda_template_items` | Agenda Template Timeline Items | 8 |
| 48 | `audit_logs` | Audit Trail | 10 |
| 49 | `audit_log_changes` | Field-Level Audit Changes | 6 |
| 50 | `visit_status_logs` | Visit Status History | 9 |

## 4. Table Details

### 4.1. `roles`

**Purpose / Table Comment:** 6 role chính của hệ thống

**Main Screens / UC Area:** RBAC / Account Management

**Columns:**

| Column | Type | Null | Default | Extra | Enum Values | Notes |
|---|---|---:|---|---|---|---|
| `role_id` | `BIGINT UNSIGNED` | NO | `` | AUTO_INCREMENT |  |  |
| `role_code` | `VARCHAR(30)` | NO | `` |  |  | ADMIN, HO, STAFF, DEPARTMENT, STUDENT, VISITOR |
| `name` | `VARCHAR(100)` | NO | `` |  |  |  |
| `description` | `VARCHAR(255)` | YES | `` |  |  |  |
| `status` | `ENUM('ACTIVE','INACTIVE')` | NO | `'ACTIVE'` |  | `ACTIVE`, `INACTIVE` |  |
| `created_at` | `DATETIME` | NO | `CURRENT_TIMESTAMP` |  |  |  |
| `deleted_at` | `DATETIME` | YES | `` |  |  | Soft delete supported by UC-121 Disable/Delete Role |
| `deleted_by` | `BIGINT UNSIGNED` | YES | `` |  |  | User who soft-deleted this role; no FK here because roles is created before users |

**Primary Key:**
- `PRIMARY KEY (role_id)`

**Unique Constraints:**
- `UNIQUE KEY uq_roles_code (role_code)`

**Indexes:**
- `KEY idx_roles_status_deleted (status, deleted_at)`

**Check Constraints:**
- `CHECK (role_code IN ('ADMIN','HO','STAFF','DEPARTMENT','STUDENT','VISITOR'))`

### 4.2. `permissions`

**Purpose / Table Comment:** Danh mục quyền theo UC/action

**Main Screens / UC Area:** RBAC / Permission Management

**Columns:**

| Column | Type | Null | Default | Extra | Enum Values | Notes |
|---|---|---:|---|---|---|---|
| `permission_id` | `BIGINT UNSIGNED` | NO | `` | AUTO_INCREMENT |  |  |
| `permission_code` | `VARCHAR(100)` | NO | `` |  |  | Example: UC-17.SUBMIT_VISIT_REQUEST |
| `name` | `VARCHAR(150)` | NO | `` |  |  |  |
| `permission_group` | `VARCHAR(60)` | NO | `` |  |  |  |
| `description` | `VARCHAR(500)` | YES | `` |  |  |  |
| `is_system` | `BOOLEAN` | NO | `FALSE` |  |  |  |
| `created_at` | `DATETIME` | NO | `CURRENT_TIMESTAMP` |  |  |  |

**Primary Key:**
- `PRIMARY KEY (permission_id)`

**Unique Constraints:**
- `UNIQUE KEY uq_permissions_code (permission_code)`

**Indexes:**
- `KEY idx_permissions_group (permission_group)`
- `KEY idx_permissions_group_code (permission_group, permission_code)`

### 4.3. `role_permissions`

**Purpose / Table Comment:** Ma trận phân quyền theo role + sub_role + permission

**Main Screens / UC Area:** RBAC / Permission Matrix

**Columns:**

| Column | Type | Null | Default | Extra | Enum Values | Notes |
|---|---|---:|---|---|---|---|
| `role_permission_id` | `BIGINT UNSIGNED` | NO | `` | AUTO_INCREMENT |  |  |
| `role_id` | `BIGINT UNSIGNED` | NO | `` |  |  |  |
| `sub_role` | `ENUM('NONE','LEADER','STAFF')` | NO | `'NONE'` |  | `NONE`, `LEADER`, `STAFF` | NONE for ADMIN/HO/STUDENT/VISITOR; LEADER/STAFF for STAFF and DEPARTMENT |
| `permission_id` | `BIGINT UNSIGNED` | NO | `` |  |  |  |
| `permission_level` | `ENUM('F','E','R','O')` | NO | `` |  | `F`, `E`, `R`, `O` | F=Full, E=Execute/Edit, R=Read, O=Own |
| `granted_at` | `DATETIME` | NO | `CURRENT_TIMESTAMP` |  |  |  |
| `granted_by` | `BIGINT UNSIGNED` | YES | `` |  |  |  |

**Primary Key:**
- `PRIMARY KEY (role_permission_id)`

**Unique Constraints:**
- `UNIQUE KEY uq_role_permissions_role_sub_permission (role_id, sub_role, permission_id)`

**Indexes:**
- `KEY idx_role_permissions_permission (permission_id)`
- `KEY idx_role_permissions_role_sub_role (role_id, sub_role)`

**Foreign Keys:**
- `CONSTRAINT fk_role_permissions_role FOREIGN KEY (role_id) REFERENCES roles(role_id) ON UPDATE CASCADE ON DELETE CASCADE`
- `CONSTRAINT fk_role_permissions_permission FOREIGN KEY (permission_id) REFERENCES permissions(permission_id) ON UPDATE CASCADE ON DELETE CASCADE`

### 4.4. `campuses`

**Purpose / Table Comment:** Danh mục campus

**Main Screens / UC Area:** Campus Management / Internal Portal Login

**Columns:**

| Column | Type | Null | Default | Extra | Enum Values | Notes |
|---|---|---:|---|---|---|---|
| `campus_id` | `BIGINT UNSIGNED` | NO | `` | AUTO_INCREMENT |  |  |
| `campus_code` | `VARCHAR(20)` | NO | `` |  |  | HN, HCM, DN, CT, QN |
| `name` | `VARCHAR(150)` | NO | `` |  |  |  |
| `city` | `VARCHAR(100)` | YES | `` |  |  |  |
| `address` | `VARCHAR(255)` | YES | `` |  |  |  |
| `phone` | `VARCHAR(30)` | YES | `` |  |  |  |
| `email` | `VARCHAR(150)` | YES | `` |  |  |  |
| `ic_head_user_id` | `BIGINT UNSIGNED` | YES | `` |  |  | FK added after users table |
| `status` | `ENUM('ACTIVE','INACTIVE')` | NO | `'ACTIVE'` |  | `ACTIVE`, `INACTIVE` |  |
| `created_at` | `DATETIME` | NO | `CURRENT_TIMESTAMP` |  |  |  |
| `created_by` | `BIGINT UNSIGNED` | YES | `` |  |  |  |
| `updated_at` | `DATETIME` | YES | `NULL` | ON UPDATE CURRENT_TIMESTAMP |  |  |
| `updated_by` | `BIGINT UNSIGNED` | YES | `` |  |  |  |

**Primary Key:**
- `PRIMARY KEY (campus_id)`

**Unique Constraints:**
- `UNIQUE KEY uq_campuses_code (campus_code)`

**Indexes:**
- `KEY idx_campuses_status (status)`
- `KEY idx_campuses_city_status (city, status)`
- `KEY idx_campuses_ic_head (ic_head_user_id)`

### 4.5. `departments`

**Purpose / Table Comment:** Phòng ban theo campus. STAFF thuộc IC, DEPARTMENT thuộc GENERAL

**Main Screens / UC Area:** Department Management / Logistics Assignment

**Columns:**

| Column | Type | Null | Default | Extra | Enum Values | Notes |
|---|---|---:|---|---|---|---|
| `department_id` | `BIGINT UNSIGNED` | NO | `` | AUTO_INCREMENT |  |  |
| `campus_id` | `BIGINT UNSIGNED` | NO | `` |  |  |  |
| `department_code` | `VARCHAR(50)` | NO | `` |  |  |  |
| `name` | `VARCHAR(150)` | NO | `` |  |  |  |
| `department_type` | `ENUM('IC','GENERAL')` | NO | `` |  | `IC`, `GENERAL` | IC=International Cooperation; GENERAL=other departments |
| `head_user_id` | `BIGINT UNSIGNED` | YES | `` |  |  | FK added after users table |
| `status` | `ENUM('ACTIVE','INACTIVE')` | NO | `'ACTIVE'` |  | `ACTIVE`, `INACTIVE` |  |
| `created_at` | `DATETIME` | NO | `CURRENT_TIMESTAMP` |  |  |  |
| `created_by` | `BIGINT UNSIGNED` | YES | `` |  |  |  |
| `updated_at` | `DATETIME` | YES | `NULL` | ON UPDATE CURRENT_TIMESTAMP |  |  |
| `updated_by` | `BIGINT UNSIGNED` | YES | `` |  |  |  |

**Primary Key:**
- `PRIMARY KEY (department_id)`

**Unique Constraints:**
- `UNIQUE KEY uq_departments_campus_code (campus_id, department_code)`
- `UNIQUE KEY uq_departments_campus_name (campus_id, name)`

**Indexes:**
- `KEY idx_departments_campus_type (campus_id, department_type)`
- `KEY idx_departments_status (status)`
- `KEY idx_departments_head (head_user_id)`

**Foreign Keys:**
- `CONSTRAINT fk_departments_campus FOREIGN KEY (campus_id) REFERENCES campuses(campus_id) ON UPDATE CASCADE ON DELETE RESTRICT`

### 4.6. `users`

**Purpose / Table Comment:** Account Management / Auth / RBAC

**Main Screens / UC Area:** Account Management / Auth / RBAC

**Columns:**

| Column | Type | Null | Default | Extra | Enum Values | Notes |
|---|---|---:|---|---|---|---|
| `user_id` | `BIGINT UNSIGNED` | NO | `` | AUTO_INCREMENT |  |  |
| `full_name` | `VARCHAR(150)` | NO | `` |  |  |  |
| `email` | `VARCHAR(150)` | NO | `` |  |  |  |
| `phone` | `VARCHAR(30)` | YES | `` |  |  |  |
| `nationality` | `VARCHAR(100)` | YES | `` |  |  | Quốc tịch của user/visitor |
| `password_hash` | `VARCHAR(255)` | YES | `` |  |  | DEV/local password hash only. Production SSO-only accounts keep this NULL. |
| `role_id` | `BIGINT UNSIGNED` | NO | `` |  |  |  |
| `sub_role` | `ENUM('LEADER','STAFF')` | YES | `` |  | `LEADER`, `STAFF` | Only for STAFF/DEPARTMENT |
| `primary_campus_id` | `BIGINT UNSIGNED` | YES | `` |  |  | Campus duy nhất của user nội bộ. VISITOR phải NULL. |
| `department_id` | `BIGINT UNSIGNED` | YES | `` |  |  | STAFF = IC department; DEPARTMENT = GENERAL department |
| `gender` | `ENUM('MALE','FEMALE','OTHER','UNKNOWN')` | YES | `` |  | `MALE`, `FEMALE`, `OTHER`, `UNKNOWN` |  |
| `avatar_url` | `VARCHAR(500)` | YES | `` |  |  |  |
| `student_code` | `VARCHAR(30)` | YES | `` |  |  |  |
| `fe_id` | `VARCHAR(100)` | YES | `` |  |  |  |
| `status` | `ENUM('ACTIVE','INACTIVE','LOCKED')` | NO | `'ACTIVE'` |  | `ACTIVE`, `INACTIVE`, `LOCKED` | ACTIVE=hoạt động, INACTIVE=tạm ngưng, LOCKED=bị khóa |
| `email_verified_at` | `DATETIME` | YES | `` |  |  | Thời điểm email được xác thực qua SSO lần đầu hoặc xác nhận bởi hệ thống |
| `failed_login_count` | `INT UNSIGNED` | NO | `0` |  |  | Số lần đăng nhập sai local password liên tiếp; reset khi login thành công |
| `locked_until` | `DATETIME` | YES | `` |  |  | Thời điểm hết khóa tạm thời nếu bị lock |
| `created_via` | `ENUM('MANUAL_CREATED','VISITOR_FORM','SSO_AUTO_PROVISION')` | NO | `'MANUAL_CREATED'` |  | `MANUAL_CREATED`, `VISITOR_FORM`, `SSO_AUTO_PROVISION` | MANUAL_CREATED=HO/Staff Leader tạo, VISITOR_FORM=tạo từ form visitor, SSO_AUTO_PROVISION=tạo tự động khi đăng nhập SSO ở cổng Visitor |
| `first_login_at` | `DATETIME` | YES | `` |  |  |  |
| `last_login_at` | `DATETIME` | YES | `` |  |  |  |
| `created_at` | `DATETIME` | NO | `CURRENT_TIMESTAMP` |  |  |  |
| `created_by` | `BIGINT UNSIGNED` | YES | `` |  |  |  |
| `updated_at` | `DATETIME` | YES | `NULL` | ON UPDATE CURRENT_TIMESTAMP |  |  |
| `updated_by` | `BIGINT UNSIGNED` | YES | `` |  |  |  |

**Primary Key:**
- `PRIMARY KEY (user_id)`

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

### 4.7. `user_auth_providers`

**Purpose / Table Comment:** Authentication / SSO / Dev Login

**Main Screens / UC Area:** Authentication / SSO / Dev Login

**Columns:**

| Column | Type | Null | Default | Extra | Enum Values | Notes |
|---|---|---:|---|---|---|---|
| `auth_provider_id` | `BIGINT UNSIGNED` | NO | `` | AUTO_INCREMENT |  |  |
| `user_id` | `BIGINT UNSIGNED` | NO | `` |  |  |  |
| `provider_type` | `ENUM('LOCAL_PASSWORD','GOOGLE_SSO','FEID')` | NO | `` |  | `LOCAL_PASSWORD`, `GOOGLE_SSO`, `FEID` |  |
| `provider_subject` | `VARCHAR(255)` | YES | `` |  |  | Required for GOOGLE_SSO/FEID |
| `provider_email` | `VARCHAR(150)` | YES | `` |  |  |  |
| `is_enabled` | `BOOLEAN` | NO | `TRUE` |  |  |  |
| `linked_at` | `DATETIME` | NO | `CURRENT_TIMESTAMP` |  |  |  |
| `last_used_at` | `DATETIME` | YES | `` |  |  |  |

**Primary Key:**
- `PRIMARY KEY (auth_provider_id)`

**Unique Constraints:**
- `UNIQUE KEY uq_user_auth_provider_type (user_id, provider_type)`
- `UNIQUE KEY uq_auth_provider_subject (provider_type, provider_subject)`

**Indexes:**
- `KEY idx_auth_provider_email (provider_email)`
- `KEY idx_auth_provider_type_email_enabled (provider_type, provider_email, is_enabled)`

**Foreign Keys:**
- `CONSTRAINT fk_auth_providers_user FOREIGN KEY (user_id) REFERENCES users(user_id) ON UPDATE CASCADE ON DELETE CASCADE`

### 4.8. `user_sessions`

**Purpose / Table Comment:** Session + refresh token hash

**Main Screens / UC Area:** Authentication / Session Management

**Columns:**

| Column | Type | Null | Default | Extra | Enum Values | Notes |
|---|---|---:|---|---|---|---|
| `session_id` | `BIGINT UNSIGNED` | NO | `` | AUTO_INCREMENT |  |  |
| `user_id` | `BIGINT UNSIGNED` | NO | `` |  |  |  |
| `login_portal` | `ENUM('VISITOR','INTERNAL')` | NO | `` |  | `VISITOR`, `INTERNAL` |  |
| `selected_campus_id` | `BIGINT UNSIGNED` | YES | `` |  |  | Auto set to users.primary_campus_id for INTERNAL, NULL for VISITOR |
| `auth_provider_id` | `BIGINT UNSIGNED` | YES | `` |  |  |  |
| `refresh_token_hash` | `VARCHAR(255)` | YES | `` |  |  | Refresh token hash merged into session |
| `refresh_expires_at` | `DATETIME` | YES | `` |  |  |  |
| `refresh_revoked_at` | `DATETIME` | YES | `` |  |  |  |
| `ip_address` | `VARCHAR(45)` | YES | `` |  |  |  |
| `user_agent` | `VARCHAR(500)` | YES | `` |  |  |  |
| `created_at` | `DATETIME` | NO | `CURRENT_TIMESTAMP` |  |  |  |
| `expires_at` | `DATETIME` | NO | `` |  |  |  |
| `revoked_at` | `DATETIME` | YES | `` |  |  |  |
| `revoked_by` | `BIGINT UNSIGNED` | YES | `` |  |  |  |
| `revoked_reason` | `VARCHAR(255)` | YES | `` |  |  |  |

**Primary Key:**
- `PRIMARY KEY (session_id)`

**Unique Constraints:**
- `UNIQUE KEY uq_sessions_refresh_hash (refresh_token_hash)`

**Indexes:**
- `KEY idx_sessions_user_active (user_id, revoked_at, expires_at)`
- `KEY idx_sessions_portal_campus (login_portal, selected_campus_id)`
- `KEY idx_sessions_refresh_active (refresh_token_hash, refresh_revoked_at, refresh_expires_at)`
- `KEY idx_sessions_ip_time (ip_address, created_at)`
- `KEY idx_sessions_expires_at (expires_at)`
- `KEY idx_sessions_revoked_at (revoked_at)`

**Foreign Keys:**
- `CONSTRAINT fk_sessions_user FOREIGN KEY (user_id) REFERENCES users(user_id) ON UPDATE CASCADE ON DELETE CASCADE`
- `CONSTRAINT fk_sessions_selected_campus FOREIGN KEY (selected_campus_id) REFERENCES campuses(campus_id) ON UPDATE CASCADE ON DELETE RESTRICT`
- `CONSTRAINT fk_sessions_auth_provider FOREIGN KEY (auth_provider_id) REFERENCES user_auth_providers(auth_provider_id) ON UPDATE CASCADE ON DELETE SET NULL`
- `CONSTRAINT fk_sessions_revoked_by FOREIGN KEY (revoked_by) REFERENCES users(user_id) ON UPDATE CASCADE ON DELETE SET NULL`

### 4.9. `otp_tokens`

**Purpose / Table Comment:** OTP, magic link, set password token, reset password token

**Main Screens / UC Area:** OTP Verification / Visit Request Verify

**Columns:**

| Column | Type | Null | Default | Extra | Enum Values | Notes |
|---|---|---:|---|---|---|---|
| `otp_token_id` | `BIGINT UNSIGNED` | NO | `` | AUTO_INCREMENT |  |  |
| `user_id` | `BIGINT UNSIGNED` | YES | `` |  |  |  |
| `email` | `VARCHAR(150)` | NO | `` |  |  |  |
| `token_type` | `ENUM('OTP_CODE','MAGIC_LINK')` | NO | `'OTP_CODE'` |  | `OTP_CODE`, `MAGIC_LINK` |  |
| `purpose` | `ENUM('VISIT_REQUEST_VERIFY','CHANGE_SENSITIVE_ACTION')` | NO | `` |  | `VISIT_REQUEST_VERIFY`, `CHANGE_SENSITIVE_ACTION` |  |
| `token_hash` | `VARCHAR(255)` | NO | `` |  |  |  |
| `expires_at` | `DATETIME` | NO | `` |  |  |  |
| `used_at` | `DATETIME` | YES | `` |  |  |  |
| `attempt_count` | `INT UNSIGNED` | NO | `0` |  |  |  |
| `max_attempts` | `INT UNSIGNED` | NO | `5` |  |  |  |
| `resend_count` | `INT UNSIGNED` | NO | `0` |  |  |  |
| `ip_address` | `VARCHAR(45)` | YES | `` |  |  |  |
| `user_agent` | `VARCHAR(500)` | YES | `` |  |  |  |
| `created_at` | `DATETIME` | NO | `CURRENT_TIMESTAMP` |  |  |  |

**Primary Key:**
- `PRIMARY KEY (otp_token_id)`

**Unique Constraints:**
- `UNIQUE KEY uq_otp_tokens_hash (token_hash)`

**Indexes:**
- `KEY idx_otp_email_purpose_time (email, purpose, created_at)`
- `KEY idx_otp_email_purpose_active (email, purpose, used_at, expires_at)`
- `KEY idx_otp_user_purpose_active (user_id, purpose, used_at, expires_at)`
- `KEY idx_otp_ip_time (ip_address, created_at)`

**Foreign Keys:**
- `CONSTRAINT fk_otp_tokens_user FOREIGN KEY (user_id) REFERENCES users(user_id) ON UPDATE CASCADE ON DELETE CASCADE`

### 4.10. `login_logs`

**Purpose / Table Comment:** Lịch sử đăng nhập

**Main Screens / UC Area:** Authentication Audit

**Columns:**

| Column | Type | Null | Default | Extra | Enum Values | Notes |
|---|---|---:|---|---|---|---|
| `login_log_id` | `BIGINT UNSIGNED` | NO | `` | AUTO_INCREMENT |  |  |
| `user_id` | `BIGINT UNSIGNED` | YES | `` |  |  |  |
| `email` | `VARCHAR(150)` | NO | `` |  |  |  |
| `login_portal` | `ENUM('VISITOR','INTERNAL')` | NO | `` |  | `VISITOR`, `INTERNAL` |  |
| `selected_campus_id` | `BIGINT UNSIGNED` | YES | `` |  |  |  |
| `provider_type` | `ENUM('LOCAL_PASSWORD','GOOGLE_SSO','FEID')` | YES | `` |  | `LOCAL_PASSWORD`, `GOOGLE_SSO`, `FEID` |  |
| `status` | `ENUM('SUCCESS','FAILED','BLOCKED')` | NO | `` |  | `SUCCESS`, `FAILED`, `BLOCKED` |  |
| `failure_reason` | `VARCHAR(255)` | YES | `` |  |  |  |
| `ip_address` | `VARCHAR(45)` | YES | `` |  |  |  |
| `user_agent` | `VARCHAR(500)` | YES | `` |  |  |  |
| `session_id` | `BIGINT UNSIGNED` | YES | `` |  |  |  |
| `created_at` | `DATETIME` | NO | `CURRENT_TIMESTAMP` |  |  |  |

**Primary Key:**
- `PRIMARY KEY (login_log_id)`

**Indexes:**
- `KEY idx_login_logs_user_time (user_id, created_at)`
- `KEY idx_login_logs_email_status_time (email, status, created_at)`
- `KEY idx_login_logs_ip_status_time (ip_address, status, created_at)`
- `KEY idx_login_logs_portal_campus (login_portal, selected_campus_id)`
- `KEY idx_login_logs_provider_time (provider_type, created_at)`

**Foreign Keys:**
- `CONSTRAINT fk_login_logs_user FOREIGN KEY (user_id) REFERENCES users(user_id) ON UPDATE CASCADE ON DELETE SET NULL`
- `CONSTRAINT fk_login_logs_campus FOREIGN KEY (selected_campus_id) REFERENCES campuses(campus_id) ON UPDATE CASCADE ON DELETE SET NULL`

### 4.11. `security_events`

**Purpose / Table Comment:** SSO-only security events: portal/campus validation, Visitor auto-provisioning, and session lifecycle. No local password tracking and no metadata JSON.

**Main Screens / UC Area:** Security Audit / SSO Event Log

**Columns:**

| Column | Type | Null | Default | Extra | Enum Values | Notes |
|---|---|---:|---|---|---|---|
| `security_event_id` | `BIGINT UNSIGNED` | NO | `` | AUTO_INCREMENT |  |  |
| `user_id` | `BIGINT UNSIGNED` | YES | `` |  |  |  |
| `email_snapshot` | `VARCHAR(150)` | YES | `` |  |  | Email nhận từ SSO hoặc email đang được kiểm tra tại thời điểm xảy ra sự kiện |
| `event_type` | `ENUM( 'SSO_LOGIN', 'PORTAL_VALIDATION', 'CAMPUS_VALIDATION', 'VISITOR_AUTO_PROVISION', 'SESSION_CREATED', 'SESSION_REVOKED', 'SESSION_EXPIRED', 'TOKEN_REFRESH', 'SECURITY_POLICY_CHECK' )` | NO | `` |  | `SSO_LOGIN`, `PORTAL_VALIDATION`, `CAMPUS_VALIDATION`, `VISITOR_AUTO_PROVISION`, `SESSION_CREATED`, `SESSION_REVOKED`, `SESSION_EXPIRED`, `TOKEN_REFRESH`, `SECURITY_POLICY_CHECK` | Loại sự kiện bảo mật theo mô hình SSO-only |
| `result` | `ENUM('SUCCESS','FAILED','BLOCKED')` | NO | `'SUCCESS'` |  | `SUCCESS`, `FAILED`, `BLOCKED` | Kết quả xử lý sự kiện |
| `failure_reason_code` | `ENUM( 'ACCOUNT_NOT_FOUND', 'ACCOUNT_DISABLED', 'PORTAL_MISMATCH', 'CAMPUS_MISMATCH', 'ROLE_MISMATCH', 'SSO_PROVIDER_ERROR', 'INVALID_SSO_CLAIMS', 'VISITOR_AUTO_PROVISION_DISABLED', 'SESSION_EXPIRED', 'TOKEN_REVOKED', 'SUSPICIOUS_IP', 'UNKNOWN' )` | YES | `` |  | `ACCOUNT_NOT_FOUND`, `ACCOUNT_DISABLED`, `PORTAL_MISMATCH`, `CAMPUS_MISMATCH`, `ROLE_MISMATCH`, `SSO_PROVIDER_ERROR`, `INVALID_SSO_CLAIMS`, `VISITOR_AUTO_PROVISION_DISABLED`, `SESSION_EXPIRED`, `TOKEN_REVOKED`, `SUSPICIOUS_IP`, `UNKNOWN` | Mã lý do thất bại/chặn; NULL khi SUCCESS |
| `severity` | `ENUM('LOW','MEDIUM','HIGH','CRITICAL')` | NO | `'LOW'` |  | `LOW`, `MEDIUM`, `HIGH`, `CRITICAL` |  |
| `login_portal` | `ENUM('VISITOR','INTERNAL')` | YES | `` |  | `VISITOR`, `INTERNAL` | Portal được dùng khi phát sinh sự kiện |
| `selected_campus_id` | `BIGINT UNSIGNED` | YES | `` |  |  | Campus người dùng chọn ở Internal Portal; NULL với Visitor Portal |
| `provider_type` | `ENUM('GOOGLE_SSO','FEID')` | YES | `` |  | `GOOGLE_SSO`, `FEID` | Nguồn định danh SSO; không dùng LOCAL_PASSWORD trong security_events |
| `ip_address` | `VARCHAR(45)` | YES | `` |  |  |  |
| `user_agent` | `VARCHAR(500)` | YES | `` |  |  |  |
| `session_id` | `BIGINT UNSIGNED` | YES | `` |  |  |  |
| `detail_text` | `TEXT` | YES | `` |  |  | Ghi chú debug ngắn, không lưu JSON metadata |
| `created_at` | `DATETIME` | NO | `CURRENT_TIMESTAMP` |  |  |  |

**Primary Key:**
- `PRIMARY KEY (security_event_id)`

**Indexes:**
- `KEY idx_security_user_time (user_id, created_at)`
- `KEY idx_security_email_time (email_snapshot, created_at)`
- `KEY idx_security_type_result_time (event_type, result, created_at)`
- `KEY idx_security_portal_campus_time (login_portal, selected_campus_id, created_at)`
- `KEY idx_security_failure_reason_time (failure_reason_code, created_at)`
- `KEY idx_security_ip_time (ip_address, created_at)`
- `KEY idx_security_severity_time (severity, created_at)`
- `KEY idx_security_session_time (session_id, created_at)`

**Foreign Keys:**
- `CONSTRAINT fk_security_events_user FOREIGN KEY (user_id) REFERENCES users(user_id) ON UPDATE CASCADE ON DELETE SET NULL`
- `CONSTRAINT fk_security_events_selected_campus FOREIGN KEY (selected_campus_id) REFERENCES campuses(campus_id) ON UPDATE CASCADE ON DELETE SET NULL`
- `CONSTRAINT fk_security_events_session FOREIGN KEY (session_id) REFERENCES user_sessions(session_id) ON UPDATE CASCADE ON DELETE SET NULL`

### 4.12. `files`

**Purpose / Table Comment:** File metadata only. Binary file is stored outside DB.

**Main Screens / UC Area:** File Storage Metadata

**Columns:**

| Column | Type | Null | Default | Extra | Enum Values | Notes |
|---|---|---:|---|---|---|---|
| `file_id` | `BIGINT UNSIGNED` | NO | `` | AUTO_INCREMENT |  |  |
| `storage_provider` | `ENUM('LOCAL','S3','AZURE','GCS','GOOGLE_DRIVE','OTHER')` | NO | `'LOCAL'` |  | `LOCAL`, `S3`, `AZURE`, `GCS`, `GOOGLE_DRIVE`, `OTHER` |  |
| `bucket_name` | `VARCHAR(150)` | YES | `` |  |  |  |
| `object_key` | `VARCHAR(700)` | NO | `` |  |  | Max 700 chars to keep UNIQUE index safe under utf8mb4 |
| `original_filename` | `VARCHAR(255)` | NO | `` |  |  |  |
| `mime_type` | `VARCHAR(150)` | YES | `` |  |  |  |
| `file_size` | `BIGINT UNSIGNED` | YES | `` |  |  |  |
| `uploaded_by` | `BIGINT UNSIGNED` | YES | `` |  |  |  |
| `uploaded_at` | `DATETIME` | NO | `CURRENT_TIMESTAMP` |  |  |  |
| `external_file_id` | `VARCHAR(255)` | YES | `` |  |  | External provider file id, e.g., Google Drive file id |
| `web_view_url` | `VARCHAR(700)` | YES | `` |  |  | Open/view URL from external storage provider |
| `download_url` | `VARCHAR(700)` | YES | `` |  |  | Direct download URL when provider allows it |
| `thumbnail_url` | `VARCHAR(700)` | YES | `` |  |  | Thumbnail URL for image/video preview |
| `file_purpose` | `VARCHAR(100)` | YES | `` |  |  | Technical/business file purpose used by referencing entity |

**Primary Key:**
- `PRIMARY KEY (file_id)`

**Unique Constraints:**
- `UNIQUE KEY uq_files_object_key (object_key)`

**Indexes:**
- `KEY idx_files_uploaded_by (uploaded_by, uploaded_at)`
- `KEY idx_files_mime_time (mime_type, uploaded_at)`
- `KEY idx_files_checksum (checksum_sha256)`
- `KEY idx_files_external_file_id (external_file_id)`
- `KEY idx_files_purpose_time (file_purpose, uploaded_at)`

**Foreign Keys:**
- `CONSTRAINT fk_files_uploaded_by FOREIGN KEY (uploaded_by) REFERENCES users(user_id) ON UPDATE CASCADE ON DELETE SET NULL`

**Check Constraints:**
- `checksum_sha256 CHAR(64) NULL`

### 4.13. `partners`

**Purpose / Table Comment:** Hồ sơ đối tác

**Main Screens / UC Area:** Partner Management / Public Partners / Visit Request Partner Link

**Columns:**

| Column | Type | Null | Default | Extra | Enum Values | Notes |
|---|---|---:|---|---|---|---|
| `partner_id` | `BIGINT UNSIGNED` | NO | `` | AUTO_INCREMENT |  |  |
| `partner_code` | `VARCHAR(50)` | YES | `` |  |  |  |
| `name` | `VARCHAR(200)` | NO | `` |  |  |  |
| `short_name` | `VARCHAR(100)` | YES | `` |  |  |  |
| `country` | `VARCHAR(100)` | YES | `` |  |  |  |
| `city` | `VARCHAR(100)` | YES | `` |  |  |  |
| `website_url` | `VARCHAR(500)` | YES | `` |  |  |  |
| `partner_type` | `ENUM('UNIVERSITY','COMPANY','GOVERNMENT','NGO','OTHER')` | NO | `'UNIVERSITY'` |  | `UNIVERSITY`, `COMPANY`, `GOVERNMENT`, `NGO`, `OTHER` |  |
| `cooperation_status` | `ENUM('POTENTIAL','ACTIVE','INACTIVE','BLACKLISTED')` | NO | `'POTENTIAL'` |  | `POTENTIAL`, `ACTIVE`, `INACTIVE`, `BLACKLISTED` |  |
| `description` | `TEXT` | YES | `` |  |  |  |
| `logo_file_id` | `BIGINT UNSIGNED` | YES | `` |  |  | Partner logo file, references files.file_id |
| `cover_file_id` | `BIGINT UNSIGNED` | YES | `` |  |  | Partner cover/banner file, references files.file_id |
| `address` | `VARCHAR(500)` | YES | `` |  |  |  |
| `public_slug` | `VARCHAR(180)` | YES | `` |  |  | Public URL slug for partner profile |
| `profile_status` | `ENUM('DRAFT','PENDING_APPROVAL','APPROVED','REJECTED')` | NO | `'APPROVED'` |  | `DRAFT`, `PENDING_APPROVAL`, `APPROVED`, `REJECTED` |  |
| `review_note` | `TEXT` | YES | `` |  |  |  |
| `reviewed_by` | `BIGINT UNSIGNED` | YES | `` |  |  |  |
| `reviewed_at` | `DATETIME` | YES | `` |  |  |  |
| `visibility` | `ENUM('PRIVATE','INTERNAL','PUBLIC')` | NO | `'PUBLIC'` |  | `PRIVATE`, `INTERNAL`, `PUBLIC` |  |
| `created_at` | `DATETIME` | NO | `CURRENT_TIMESTAMP` |  |  |  |
| `created_by` | `BIGINT UNSIGNED` | YES | `` |  |  |  |
| `updated_at` | `DATETIME` | YES | `NULL` | ON UPDATE CURRENT_TIMESTAMP |  |  |
| `updated_by` | `BIGINT UNSIGNED` | YES | `` |  |  |  |

**Primary Key:**
- `PRIMARY KEY (partner_id)`

**Unique Constraints:**
- `UNIQUE KEY uq_partners_code (partner_code)`
- `UNIQUE KEY uq_partners_public_slug (public_slug)`

**Indexes:**
- `KEY idx_partners_country (country)`
- `KEY idx_partners_status (cooperation_status)`
- `KEY idx_partners_type_status (partner_type, cooperation_status)`
- `KEY idx_partners_created_at (created_at)`
- `KEY idx_partners_profile_status (profile_status)`
- `KEY idx_partners_visibility (visibility)`
- `KEY idx_partners_logo_file (logo_file_id)`
- `KEY idx_partners_cover_file (cover_file_id)`
- `KEY idx_partners_reviewed_by (reviewed_by, reviewed_at)`
- `FULLTEXT KEY ft_partners_search (name, short_name, description)`

**Foreign Keys:**
- `CONSTRAINT fk_partners_logo_file FOREIGN KEY (logo_file_id) REFERENCES files(file_id) ON UPDATE CASCADE ON DELETE SET NULL`
- `CONSTRAINT fk_partners_cover_file FOREIGN KEY (cover_file_id) REFERENCES files(file_id) ON UPDATE CASCADE ON DELETE SET NULL`
- `CONSTRAINT fk_partners_reviewed_by FOREIGN KEY (reviewed_by) REFERENCES users(user_id) ON UPDATE CASCADE ON DELETE SET NULL`

### 4.14. `partner_contacts`

**Purpose / Table Comment:** Người liên hệ đối tác. OCR final confirmed data saved here.

**Main Screens / UC Area:** Partner Contact / Business Card Scan

**Columns:**

| Column | Type | Null | Default | Extra | Enum Values | Notes |
|---|---|---:|---|---|---|---|
| `contact_id` | `BIGINT UNSIGNED` | NO | `` | AUTO_INCREMENT |  |  |
| `partner_id` | `BIGINT UNSIGNED` | NO | `` |  |  |  |
| `full_name` | `VARCHAR(150)` | NO | `` |  |  |  |
| `email` | `VARCHAR(150)` | YES | `` |  |  |  |
| `phone` | `VARCHAR(50)` | YES | `` |  |  |  |
| `job_title` | `VARCHAR(150)` | YES | `` |  |  |  |
| `department_name` | `VARCHAR(150)` | YES | `` |  |  |  |
| `source_type` | `ENUM('MANUAL','BUSINESS_CARD_OCR','IMPORT')` | NO | `'MANUAL'` |  | `MANUAL`, `BUSINESS_CARD_OCR`, `IMPORT` |  |
| `scanned_card_file_id` | `BIGINT UNSIGNED` | YES | `` |  |  |  |
| `ocr_confidence` | `DECIMAL(5,2)` | YES | `` |  |  |  |
| `note` | `TEXT` | YES | `` |  |  |  |
| `is_primary` | `BOOLEAN` | NO | `FALSE` |  |  |  |
| `status` | `ENUM('ACTIVE','INACTIVE')` | NO | `'ACTIVE'` |  | `ACTIVE`, `INACTIVE` |  |
| `created_at` | `DATETIME` | NO | `CURRENT_TIMESTAMP` |  |  |  |
| `created_by` | `BIGINT UNSIGNED` | YES | `` |  |  |  |
| `updated_at` | `DATETIME` | YES | `NULL` | ON UPDATE CURRENT_TIMESTAMP |  |  |
| `updated_by` | `BIGINT UNSIGNED` | YES | `` |  |  |  |

**Primary Key:**
- `PRIMARY KEY (contact_id)`

**Unique Constraints:**
- `UNIQUE KEY uq_partner_contacts_partner_email (partner_id, email)`

**Indexes:**
- `KEY idx_partner_contacts_partner (partner_id)`
- `KEY idx_partner_contacts_email (email)`
- `KEY idx_partner_contacts_status (status)`
- `KEY idx_partner_contacts_source_type (source_type)`
- `KEY idx_partner_contacts_scanned_card (scanned_card_file_id)`

**Foreign Keys:**
- `CONSTRAINT fk_partner_contacts_partner FOREIGN KEY (partner_id) REFERENCES partners(partner_id) ON UPDATE CASCADE ON DELETE RESTRICT`
- `CONSTRAINT fk_partner_contacts_scanned_card FOREIGN KEY (scanned_card_file_id) REFERENCES files(file_id) ON UPDATE CASCADE ON DELETE SET NULL`

### 4.15. `documents`

**Purpose / Table Comment:** Tài liệu nghiệp vụ. partner_documents/reports/logistics documents merged by owner_type.

**Main Screens / UC Area:** Document Management

**Columns:**

| Column | Type | Null | Default | Extra | Enum Values | Notes |
|---|---|---:|---|---|---|---|
| `document_id` | `BIGINT UNSIGNED` | NO | `` | AUTO_INCREMENT |  |  |
| `file_id` | `BIGINT UNSIGNED` | NO | `` |  |  |  |
| `owner_type` | `ENUM('GENERAL','VISIT','PARTNER','MINUTES','NEWS','LOGISTICS','REPORT')` | NO | `'GENERAL'` |  | `GENERAL`, `VISIT`, `PARTNER`, `MINUTES`, `NEWS`, `LOGISTICS`, `REPORT` |  |
| `owner_id` | `BIGINT UNSIGNED` | YES | `` |  |  |  |
| `campus_id` | `BIGINT UNSIGNED` | YES | `` |  |  |  |
| `title` | `VARCHAR(255)` | NO | `` |  |  |  |
| `description` | `TEXT` | YES | `` |  |  |  |
| `document_category` | `VARCHAR(100)` | YES | `` |  |  |  |
| `status` | `ENUM('DRAFT','PUBLISHED','ARCHIVED')` | NO | `'DRAFT'` |  | `DRAFT`, `PUBLISHED`, `ARCHIVED` |  |
| `created_at` | `DATETIME` | NO | `CURRENT_TIMESTAMP` |  |  |  |
| `created_by` | `BIGINT UNSIGNED` | YES | `` |  |  |  |
| `updated_at` | `DATETIME` | YES | `NULL` | ON UPDATE CURRENT_TIMESTAMP |  |  |
| `updated_by` | `BIGINT UNSIGNED` | YES | `` |  |  |  |

**Primary Key:**
- `PRIMARY KEY (document_id)`

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

### 4.16. `visit_requests`

**Purpose / Table Comment:** Public Submit Visit Request / Internal Delegation / Approval / Cancel

**Main Screens / UC Area:** Public Submit Visit Request / Internal Delegation / Approval / Cancel

**Columns:**

| Column | Type | Null | Default | Extra | Enum Values | Notes |
|---|---|---:|---|---|---|---|
| `visit_request_id` | `BIGINT UNSIGNED` | NO | `` | AUTO_INCREMENT |  |  |
| `request_code` | `VARCHAR(50)` | NO | `` |  |  |  |
| `visitor_user_id` | `BIGINT UNSIGNED` | YES | `` |  |  | Visitor user/account created or linked for the registrant |
| `partner_id` | `BIGINT UNSIGNED` | YES | `` |  |  |  |
| `created_source` | `ENUM('VISITOR_SUBMITTED','STAFF_CREATED')` | NO | `'VISITOR_SUBMITTED'` |  | `VISITOR_SUBMITTED`, `STAFF_CREATED` |  |
| `registrant_full_name` | `VARCHAR(150)` | NO | `` |  |  | Họ và tên người đăng ký |
| `registrant_organization` | `VARCHAR(200)` | NO | `` |  |  | Đơn vị công tác người đăng ký |
| `registrant_job_title` | `VARCHAR(150)` | YES | `` |  |  | Chức danh/phòng ban người đăng ký |
| `registrant_phone` | `VARCHAR(50)` | YES | `` |  |  | SĐT người đăng ký |
| `registrant_email` | `VARCHAR(150)` | NO | `` |  |  | Email người đăng ký |
| `registrant_nationality` | `VARCHAR(100)` | YES | `` |  |  | Quốc tịch người đăng ký |
| `delegation_name` | `VARCHAR(200)` | NO | `` |  |  | Tên đoàn khách |
| `visit_scope` | `ENUM('SINGLE_CAMPUS','MULTI_CAMPUS')` | NO | `'SINGLE_CAMPUS'` |  | `SINGLE_CAMPUS`, `MULTI_CAMPUS` | SINGLE_CAMPUS: Staff Leader duyệt request tổng; MULTI_CAMPUS: HO duyệt request tổng. Frontend/backend suy ra người duyệt từ cột này. |
| `visit_type` | `ENUM('CAMPUS_TOUR','MEETING','WORKSHOP','SIGNING_CEREMONY','EXCHANGE','OTHER')` | NO | `'CAMPUS_TOUR'` |  | `CAMPUS_TOUR`, `MEETING`, `WORKSHOP`, `SIGNING_CEREMONY`, `EXCHANGE`, `OTHER` |  |
| `visit_type_other` | `VARCHAR(255)` | YES | `` |  |  |  |
| `purpose` | `TEXT` | NO | `` |  |  | Mục đích thăm FPTU |
| `working_content` | `TEXT` | YES | `` |  |  | Nội dung làm việc tại FPTU |
| `expected_guest_count` | `INT UNSIGNED` | NO | `1` |  |  | Số khách dự kiến; có thể đồng bộ từ danh sách khách |
| `contact_person_full_name` | `VARCHAR(150)` | YES | `` |  |  |  |
| `contact_person_organization` | `VARCHAR(255)` | YES | `` |  |  |  |
| `contact_person_phone` | `VARCHAR(50)` | YES | `` |  |  |  |
| `contact_person_email` | `VARCHAR(150)` | YES | `` |  |  |  |
| `working_language` | `ENUM('VI','EN')` | NO | `'EN'` |  | `VI`, `EN` | Ngôn ngữ sử dụng trong visit. Chỉ dùng VI/EN theo frontend hiện tại, không có lựa chọn OTHER |
| `interpreter_note` | `TEXT` | YES | `` |  |  | Ghi chú nếu ngôn ngữ khác VI/EN và đầu mối cần tự bố trí phiên dịch |
| `transportation_type` | `ENUM('SELF_ARRANGED','FPTU_SUPPORT','UNKNOWN','OTHER')` | NO | `'UNKNOWN'` |  | `SELF_ARRANGED`, `FPTU_SUPPORT`, `UNKNOWN`, `OTHER` |  |
| `transportation_detail` | `VARCHAR(500)` | YES | `` |  |  |  |
| `media_consent_status` | `ENUM('AGREED','DECLINED','UNKNOWN')` | NO | `'UNKNOWN'` |  | `AGREED`, `DECLINED`, `UNKNOWN` |  |
| `media_consent_note` | `TEXT` | YES | `` |  |  |  |
| `note_to_fptu` | `TEXT` | YES | `` |  |  | Ghi chú cho FPTU |
| `status` | `ENUM('PENDING_APPROVAL','APPROVED','REJECTED','CANCELLED')` | NO | `'PENDING_APPROVAL'` |  | `PENDING_APPROVAL`, `APPROVED`, `REJECTED`, `CANCELLED` | Request decision status only. Visit progress is derived from visit_request_campuses.status |
| `submitted_at` | `DATETIME` | NO | `CURRENT_TIMESTAMP` |  |  |  |
| `email_verified_at` | `DATETIME` | YES | `` |  |  |  |
| `decided_by` | `BIGINT UNSIGNED` | YES | `` |  |  | Người approve/reject request tổng |
| `decided_at` | `DATETIME` | YES | `` |  |  | Thời điểm xử lý request tổng |
| `decision_actor_role` | `ENUM('HO','STAFF_LEADER','SYSTEM')` | YES | `` |  | `HO`, `STAFF_LEADER`, `SYSTEM` | Vai trò người xử lý tại thời điểm quyết định |
| `decision_note` | `TEXT` | YES | `` |  |  | Lý do/ghi chú khi approve hoặc reject |
| `cancelled_by` | `BIGINT UNSIGNED` | YES | `` |  |  | Người thực hiện hủy request/delegation |
| `cancelled_at` | `DATETIME` | YES | `` |  |  | Thời điểm hủy request/delegation |
| `cancellation_actor_type` | `ENUM('VISITOR','HOST','STAFF_LEADER','HO','SYSTEM')` | YES | `` |  | `VISITOR`, `HOST`, `STAFF_LEADER`, `HO`, `SYSTEM` | Vai trò thực hiện thao tác hủy |
| `cancellation_source` | `ENUM('SELF_SERVICE','EXTERNAL_CONFIRMATION','INTERNAL_DECISION')` | YES | `` |  | `SELF_SERVICE`, `EXTERNAL_CONFIRMATION`, `INTERNAL_DECISION` | SELF_SERVICE=Visitor tự hủy sau khi đơn đã duyệt; EXTERNAL_CONFIRMATION=Host hủy sau khi khách xác nhận ngoài hệ thống |
| `cancellation_reason` | `TEXT` | YES | `` |  |  | Lý do hủy; nếu EXTERNAL_CONFIRMATION thì ghi rõ kênh xác nhận, thời điểm, người xác nhận và lý do. |
| `row_version` | `INT UNSIGNED` | NO | `0` |  |  | Optimistic concurrency token |
| `created_at` | `DATETIME` | NO | `CURRENT_TIMESTAMP` |  |  |  |
| `created_by` | `BIGINT UNSIGNED` | YES | `` |  |  |  |
| `updated_at` | `DATETIME` | YES | `NULL` | ON UPDATE CURRENT_TIMESTAMP |  |  |
| `updated_by` | `BIGINT UNSIGNED` | YES | `` |  |  |  |

**Primary Key:**
- `PRIMARY KEY (visit_request_id)`

**Unique Constraints:**
- `UNIQUE KEY uq_visit_requests_code (request_code)`

**Indexes:**
- `KEY idx_visit_requests_visitor (visitor_user_id)`
- `KEY idx_visit_requests_partner (partner_id)`
- `KEY idx_visit_requests_status_submitted (status, submitted_at)`
- `KEY idx_visit_requests_registrant_email (registrant_email)`
- `KEY idx_visit_requests_scope_status (visit_scope, status)`
- `KEY idx_visit_requests_created_source (created_source)`
- `KEY idx_visit_requests_visit_type (visit_type)`
- `KEY idx_visit_requests_contact_email (contact_person_email)`
- `KEY idx_visit_requests_media_consent (media_consent_status)`
- `KEY idx_visit_requests_visibility_scope_status_decision (visit_scope, status, decision_actor_role, decided_at)`
- `KEY idx_visit_requests_decision (decided_by, decided_at)`
- `KEY idx_visit_requests_decision_role (decision_actor_role, decided_at)`
- `KEY idx_visit_requests_cancelled (cancelled_by, cancelled_at)`
- `KEY idx_visit_requests_cancel_actor (cancellation_actor_type, cancelled_at)`
- `FULLTEXT KEY ft_visit_requests_frontend_search (request_code, delegation_name, registrant_full_name, registrant_organization, registrant_email, contact_person_full_name, contact_person_organization, contact_person_email)`

**Foreign Keys:**
- `CONSTRAINT fk_visit_requests_visitor_user FOREIGN KEY (visitor_user_id) REFERENCES users(user_id) ON UPDATE CASCADE ON DELETE RESTRICT`
- `CONSTRAINT fk_visit_requests_partner FOREIGN KEY (partner_id) REFERENCES partners(partner_id) ON UPDATE CASCADE ON DELETE SET NULL`
- `CONSTRAINT fk_visit_requests_decided_by FOREIGN KEY (decided_by) REFERENCES users(user_id) ON UPDATE CASCADE ON DELETE SET NULL`
- `CONSTRAINT fk_visit_requests_cancelled_by FOREIGN KEY (cancelled_by) REFERENCES users(user_id) ON UPDATE CASCADE ON DELETE SET NULL`

**Check Constraints:**
- `CHECK (expected_guest_count >= 1)`
- `CHECK ( decision_actor_role IS NULL OR status NOT IN ('APPROVED','REJECTED') OR ( visit_scope = 'SINGLE_CAMPUS' AND decision_actor_role IN ('STAFF_LEADER','SYSTEM') ) OR ( visit_scope = 'MULTI_CAMPUS' AND decision_actor_role IN ('HO','SYSTEM') ) )`

### 4.17. `visit_request_campuses`

**Purpose / Table Comment:** Delegation Campus Instance / Host Assignment / Status Flow

**Main Screens / UC Area:** Delegation Campus Instance / Host Assignment / Status Flow

**Columns:**

| Column | Type | Null | Default | Extra | Enum Values | Notes |
|---|---|---:|---|---|---|---|
| `visit_instance_id` | `BIGINT UNSIGNED` | NO | `` | AUTO_INCREMENT |  |  |
| `visit_request_id` | `BIGINT UNSIGNED` | NO | `` |  |  |  |
| `campus_id` | `BIGINT UNSIGNED` | NO | `` |  |  |  |
| `instance_code` | `VARCHAR(60)` | YES | `` |  |  |  |
| `planned_start_at` | `DATETIME` | NO | `` |  |  | Ngày giờ bắt đầu dự kiến tại campus |
| `planned_end_at` | `DATETIME` | NO | `` |  |  | Ngày giờ kết thúc dự kiến tại campus |
| `status` | `ENUM( 'WAITING_REQUEST_APPROVAL', 'ASSIGNED', 'BEFORE_VISIT', 'DURING_VISIT', 'AFTER_VISIT', 'CLOSED', 'CANCELLED' )` | NO | `'WAITING_REQUEST_APPROVAL'` |  | `WAITING_REQUEST_APPROVAL`, `ASSIGNED`, `BEFORE_VISIT`, `DURING_VISIT`, `AFTER_VISIT`, `CLOSED`, `CANCELLED` |  |
| `current_host_user_id` | `BIGINT UNSIGNED` | YES | `` |  |  | Host hiện tại chịu trách nhiệm campus instance. Sau khi request tổng được duyệt thì phải có host; nếu đổi host dùng chức năng Transfer Host |
| `host_assigned_by` | `BIGINT UNSIGNED` | YES | `` |  |  | Người gây ra thao tác gán host: HO khi auto gán Staff Leader cho multi-campus, Staff Leader khi duyệt single-campus, hoặc người chuyển host |
| `host_assigned_at` | `DATETIME` | YES | `` |  |  | Thời điểm host được gán |
| `host_assignment_source` | `ENUM('AUTO_STAFF_LEADER','MANUAL_APPROVAL','TRANSFERRED')` | YES | `` |  | `AUTO_STAFF_LEADER`, `MANUAL_APPROVAL`, `TRANSFERRED` | AUTO_STAFF_LEADER=HO duyệt liên cơ sở và hệ thống tự gán Staff Leader; MANUAL_APPROVAL=Staff Leader duyệt đơn một cơ sở và chọn host; TRANSFERRED=host được chuyển sau đó |
| `host_transferred_by` | `BIGINT UNSIGNED` | YES | `` |  |  | Người chuyển host gần nhất |
| `host_transferred_at` | `DATETIME` | YES | `` |  |  | Thời điểm chuyển host gần nhất |
| `host_transfer_note` | `TEXT` | YES | `` |  |  | Ghi chú/lý do chuyển host gần nhất |
| `closed_by` | `BIGINT UNSIGNED` | YES | `` |  |  |  |
| `closed_at` | `DATETIME` | YES | `` |  |  |  |
| `close_note` | `TEXT` | YES | `` |  |  |  |
| `cancelled_by` | `BIGINT UNSIGNED` | YES | `` |  |  | Người thực hiện hủy campus instance |
| `cancelled_at` | `DATETIME` | YES | `` |  |  | Thời điểm hủy campus instance |
| `cancellation_actor_type` | `ENUM('VISITOR','HOST','STAFF_LEADER','HO','SYSTEM')` | YES | `` |  | `VISITOR`, `HOST`, `STAFF_LEADER`, `HO`, `SYSTEM` | Vai trò thực hiện thao tác hủy campus instance |
| `cancellation_source` | `ENUM('SELF_SERVICE','EXTERNAL_CONFIRMATION','INTERNAL_DECISION')` | YES | `` |  | `SELF_SERVICE`, `EXTERNAL_CONFIRMATION`, `INTERNAL_DECISION` | SELF_SERVICE=Visitor tự hủy sau khi đơn đã duyệt; EXTERNAL_CONFIRMATION=Host hủy sau khi khách xác nhận ngoài hệ thống |
| `cancellation_reason` | `TEXT` | YES | `` |  |  | Lý do hủy; nếu EXTERNAL_CONFIRMATION thì ghi rõ kênh xác nhận, thời điểm, người xác nhận và lý do. |
| `row_version` | `INT UNSIGNED` | NO | `0` |  |  | Optimistic concurrency token |
| `created_at` | `DATETIME` | NO | `CURRENT_TIMESTAMP` |  |  |  |
| `created_by` | `BIGINT UNSIGNED` | YES | `` |  |  |  |
| `updated_at` | `DATETIME` | YES | `NULL` | ON UPDATE CURRENT_TIMESTAMP |  |  |
| `updated_by` | `BIGINT UNSIGNED` | YES | `` |  |  |  |

**Primary Key:**
- `PRIMARY KEY (visit_instance_id)`

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
- `KEY idx_visit_instances_visibility_campus_request (campus_id, visit_request_id, status, current_host_user_id)`

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

### 4.18. `visit_guest_members`

**Purpose / Table Comment:** Danh sách từng người trong đoàn khách. Không lưu consent hình ảnh vì form đã bỏ phần xác nhận sử dụng hình ảnh/thông tin.

**Main Screens / UC Area:** Guest List / External Support Members

**Columns:**

| Column | Type | Null | Default | Extra | Enum Values | Notes |
|---|---|---:|---|---|---|---|
| `guest_member_id` | `BIGINT UNSIGNED` | NO | `` | AUTO_INCREMENT |  |  |
| `visit_request_id` | `BIGINT UNSIGNED` | NO | `` |  |  |  |
| `member_type` | `ENUM('GUEST','EXTERNAL_SUPPORT')` | NO | `'GUEST'` |  | `GUEST`, `EXTERNAL_SUPPORT` |  |
| `full_name` | `VARCHAR(150)` | NO | `` |  |  |  |
| `organization` | `VARCHAR(200)` | YES | `` |  |  |  |
| `job_title` | `VARCHAR(150)` | YES | `` |  |  |  |
| `nationality` | `VARCHAR(100)` | YES | `` |  |  |  |
| `email` | `VARCHAR(150)` | YES | `` |  |  |  |
| `phone` | `VARCHAR(50)` | YES | `` |  |  |  |
| `is_representative` | `BOOLEAN` | NO | `FALSE` |  |  |  |
| `note` | `TEXT` | YES | `` |  |  |  |
| `display_order` | `INT UNSIGNED` | NO | `0` |  |  |  |
| `created_at` | `DATETIME` | NO | `CURRENT_TIMESTAMP` |  |  |  |
| `created_by` | `BIGINT UNSIGNED` | YES | `` |  |  |  |
| `updated_at` | `DATETIME` | YES | `NULL` | ON UPDATE CURRENT_TIMESTAMP |  |  |
| `updated_by` | `BIGINT UNSIGNED` | YES | `` |  |  |  |

**Primary Key:**
- `PRIMARY KEY (guest_member_id)`

**Indexes:**
- `KEY idx_guest_members_request (visit_request_id)`
- `KEY idx_guest_members_type_order (visit_request_id, member_type, display_order)`
- `KEY idx_guest_members_email (email)`
- `KEY idx_guest_members_representative (visit_request_id, is_representative)`

**Foreign Keys:**
- `CONSTRAINT fk_guest_members_request FOREIGN KEY (visit_request_id) REFERENCES visit_requests(visit_request_id) ON UPDATE CASCADE ON DELETE RESTRICT`

### 4.19. `visit_participants`

**Purpose / Table Comment:** Người nội bộ tham gia visit instance. Chỉ gồm IC_HOST, IC_SUPPORT, DEPT_SUPPORT, STUDENT. Host chính lưu bằng is_host.

**Main Screens / UC Area:** Internal Participants / Hosts / Student Support

**Columns:**

| Column | Type | Null | Default | Extra | Enum Values | Notes |
|---|---|---:|---|---|---|---|
| `participant_id` | `BIGINT UNSIGNED` | NO | `` | AUTO_INCREMENT |  |  |
| `visit_instance_id` | `BIGINT UNSIGNED` | NO | `` |  |  |  |
| `user_id` | `BIGINT UNSIGNED` | NO | `` |  |  |  |
| `participant_role` | `ENUM('IC_HOST','IC_SUPPORT','DEPT_SUPPORT','STUDENT')` | NO | `'IC_SUPPORT'` |  | `IC_HOST`, `IC_SUPPORT`, `DEPT_SUPPORT`, `STUDENT` |  |
| `is_host` | `BOOLEAN` | NO | `FALSE` |  |  |  |
| `status` | `ENUM('INVITED','ACCEPTED','DECLINED','ASSIGNED','REMOVED')` | NO | `'INVITED'` |  | `INVITED`, `ACCEPTED`, `DECLINED`, `ASSIGNED`, `REMOVED` |  |
| `invited_by` | `BIGINT UNSIGNED` | YES | `` |  |  |  |
| `invited_at` | `DATETIME` | YES | `` |  |  |  |
| `responded_at` | `DATETIME` | YES | `` |  |  |  |
| `assigned_by` | `BIGINT UNSIGNED` | YES | `` |  |  |  |
| `assigned_at` | `DATETIME` | YES | `` |  |  |  |
| `note` | `TEXT` | YES | `` |  |  |  |
| `created_at` | `DATETIME` | NO | `CURRENT_TIMESTAMP` |  |  |  |
| `created_by` | `BIGINT UNSIGNED` | YES | `` |  |  |  |
| `updated_at` | `DATETIME` | YES | `NULL` | ON UPDATE CURRENT_TIMESTAMP |  |  |
| `updated_by` | `BIGINT UNSIGNED` | YES | `` |  |  |  |

**Primary Key:**
- `PRIMARY KEY (participant_id)`

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

### 4.20. `visit_agendas`

**Purpose / Table Comment:** Lịch trình tiếp khách

**Main Screens / UC Area:** Visit Agenda / Itinerary

**Columns:**

| Column | Type | Null | Default | Extra | Enum Values | Notes |
|---|---|---:|---|---|---|---|
| `agenda_id` | `BIGINT UNSIGNED` | NO | `` | AUTO_INCREMENT |  |  |
| `visit_instance_id` | `BIGINT UNSIGNED` | NO | `` |  |  |  |
| `sequence_order` | `INT UNSIGNED` | NO | `` |  |  |  |
| `title` | `VARCHAR(255)` | NO | `` |  |  |  |
| `description` | `TEXT` | YES | `` |  |  |  |
| `start_time` | `DATETIME` | NO | `` |  |  |  |
| `end_time` | `DATETIME` | YES | `` |  |  |  |
| `location` | `VARCHAR(255)` | YES | `` |  |  |  |
| `responsible_user_id` | `BIGINT UNSIGNED` | YES | `` |  |  |  |
| `created_at` | `DATETIME` | NO | `CURRENT_TIMESTAMP` |  |  |  |
| `created_by` | `BIGINT UNSIGNED` | YES | `` |  |  |  |
| `updated_at` | `DATETIME` | YES | `NULL` | ON UPDATE CURRENT_TIMESTAMP |  |  |
| `updated_by` | `BIGINT UNSIGNED` | YES | `` |  |  |  |

**Primary Key:**
- `PRIMARY KEY (agenda_id)`

**Unique Constraints:**
- `UNIQUE KEY uq_visit_agendas_order (visit_instance_id, sequence_order)`

**Indexes:**
- `KEY idx_visit_agendas_time (visit_instance_id, start_time)`
- `KEY idx_visit_agendas_responsible (responsible_user_id, start_time)`

**Foreign Keys:**
- `CONSTRAINT fk_visit_agendas_instance FOREIGN KEY (visit_instance_id) REFERENCES visit_request_campuses(visit_instance_id) ON UPDATE CASCADE ON DELETE RESTRICT`
- `CONSTRAINT fk_visit_agendas_responsible_user FOREIGN KEY (responsible_user_id) REFERENCES users(user_id) ON UPDATE CASCADE ON DELETE SET NULL`

### 4.21. `visit_logistics_items`

**Purpose / Table Comment:** Yêu cầu hậu cần/resource cho visit: gửi yêu cầu, đề xuất thay đổi, tiếp nhận, phân công, xác nhận và hoàn thành. Thay thế tasks cho logistics/resource.

**Main Screens / UC Area:** Visit Logistics / Resource Request / Service Report

**Columns:**

| Column | Type | Null | Default | Extra | Enum Values | Notes |
|---|---|---:|---|---|---|---|
| `logistics_item_id` | `BIGINT UNSIGNED` | NO | `` | AUTO_INCREMENT |  |  |
| `visit_instance_id` | `BIGINT UNSIGNED` | NO | `` |  |  |  |
| `item_type` | `ENUM('ROOM','TRANSPORT','MEAL','EQUIPMENT','BANNER','LED','OTHER')` | NO | `` |  | `ROOM`, `TRANSPORT`, `MEAL`, `EQUIPMENT`, `BANNER`, `LED`, `OTHER` |  |
| `title` | `VARCHAR(255)` | NO | `` |  |  |  |
| `description` | `TEXT` | YES | `` |  |  | Nội dung chi tiết công việc gốc |
| `quantity` | `INT UNSIGNED` | YES | `` |  |  | Số lượng yêu cầu gốc |
| `usage_start_at` | `DATETIME` | YES | `` |  |  | Thời gian bắt đầu sử dụng resource |
| `usage_end_at` | `DATETIME` | YES | `` |  |  | Thời gian kết thúc sử dụng resource |
| `status` | `ENUM( 'PLANNED', 'REQUESTED', 'CHANGE_PROPOSED', 'RECEIVED', 'ASSIGNED', 'ACCEPTED', 'IN_PROGRESS', 'READY', 'DONE', 'REJECTED', 'CANCELLED' )` | NO | `'PLANNED'` |  | `PLANNED`, `REQUESTED`, `CHANGE_PROPOSED`, `RECEIVED`, `ASSIGNED`, `ACCEPTED`, `IN_PROGRESS`, `READY`, `DONE`, `REJECTED`, `CANCELLED` |  |
| `priority` | `ENUM('LOW','MEDIUM','HIGH','URGENT')` | NO | `'MEDIUM'` |  | `LOW`, `MEDIUM`, `HIGH`, `URGENT` |  |
| `requested_by` | `BIGINT UNSIGNED` | YES | `` |  |  | Người gửi yêu cầu hậu cần/resource |
| `requested_to_department_id` | `BIGINT UNSIGNED` | YES | `` |  |  | Phòng ban được yêu cầu xử lý |
| `requested_at` | `DATETIME` | YES | `` |  |  | Thời điểm gửi yêu cầu |
| `received_by` | `BIGINT UNSIGNED` | YES | `` |  |  | Trưởng phòng/người tiếp nhận yêu cầu |
| `received_at` | `DATETIME` | YES | `` |  |  | Thời điểm tiếp nhận yêu cầu |
| `assigned_to_user_id` | `BIGINT UNSIGNED` | YES | `` |  |  | Nhân viên được giao xử lý chính |
| `assigned_by` | `BIGINT UNSIGNED` | YES | `` |  |  | Người phân công |
| `assigned_at` | `DATETIME` | YES | `` |  |  | Thời điểm phân công |
| `assignee_accepted_at` | `DATETIME` | YES | `` |  |  | Thời điểm nhân viên xác nhận nhận nhiệm vụ |
| `assignee_response_note` | `TEXT` | YES | `` |  |  | Ghi chú khi nhân viên nhận/từ chối nếu có |
| `due_at` | `DATETIME` | YES | `` |  |  | Deadline hoàn thành hạng mục |
| `completed_at` | `DATETIME` | YES | `` |  |  | Thời điểm hoàn thành |
| `proposed_by` | `BIGINT UNSIGNED` | YES | `` |  |  | Người gửi đề xuất thay đổi |
| `proposed_at` | `DATETIME` | YES | `` |  |  | Thời điểm gửi đề xuất thay đổi |
| `proposed_quantity` | `INT UNSIGNED` | YES | `` |  |  | Số lượng được đề xuất thay đổi |
| `proposed_usage_start_at` | `DATETIME` | YES | `` |  |  | Thời gian bắt đầu sử dụng được đề xuất |
| `proposed_usage_end_at` | `DATETIME` | YES | `` |  |  | Thời gian kết thúc sử dụng được đề xuất |
| `proposed_description` | `TEXT` | YES | `` |  |  | Nội dung chi tiết công việc được đề xuất thay đổi |
| `proposal_note` | `TEXT` | YES | `` |  |  | Lý do/ghi chú đề xuất thay đổi |
| `proposal_responded_by` | `BIGINT UNSIGNED` | YES | `` |  |  | Người xác nhận/từ chối đề xuất |
| `proposal_responded_at` | `DATETIME` | YES | `` |  |  | Thời điểm xác nhận/từ chối đề xuất |
| `proposal_response` | `ENUM('ACCEPTED','REJECTED')` | YES | `` |  | `ACCEPTED`, `REJECTED` | Kết quả phản hồi đề xuất |
| `proposal_response_note` | `TEXT` | YES | `` |  |  | Ghi chú phản hồi đề xuất |
| `handover_confirmed_by` | `BIGINT UNSIGNED` | YES | `` |  |  |  |
| `handover_confirmed_at` | `DATETIME` | YES | `` |  |  |  |
| `handover_note` | `TEXT` | YES | `` |  |  |  |
| `service_report_signed_by` | `BIGINT UNSIGNED` | YES | `` |  |  |  |
| `service_report_signed_at` | `DATETIME` | YES | `` |  |  |  |
| `service_report_file_id` | `BIGINT UNSIGNED` | YES | `` |  |  |  |
| `decision_note` | `TEXT` | YES | `` |  |  | Lý do reject/cancel hoặc ghi chú xử lý |
| `row_version` | `INT UNSIGNED` | NO | `0` |  |  | Optimistic concurrency token |
| `created_at` | `DATETIME` | NO | `CURRENT_TIMESTAMP` |  |  |  |
| `created_by` | `BIGINT UNSIGNED` | YES | `` |  |  |  |
| `updated_at` | `DATETIME` | YES | `NULL` | ON UPDATE CURRENT_TIMESTAMP |  |  |
| `updated_by` | `BIGINT UNSIGNED` | YES | `` |  |  |  |

**Primary Key:**
- `PRIMARY KEY (logistics_item_id)`

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
- `KEY idx_logistics_handover (handover_confirmed_by, handover_confirmed_at)`
- `KEY idx_logistics_service_report (service_report_signed_by, service_report_signed_at)`
- `KEY idx_logistics_service_report_file (service_report_file_id)`

**Foreign Keys:**
- `CONSTRAINT fk_logistics_instance FOREIGN KEY (visit_instance_id) REFERENCES visit_request_campuses(visit_instance_id) ON UPDATE CASCADE ON DELETE RESTRICT`
- `CONSTRAINT fk_logistics_requested_by FOREIGN KEY (requested_by) REFERENCES users(user_id) ON UPDATE CASCADE ON DELETE SET NULL`
- `CONSTRAINT fk_logistics_requested_to_department FOREIGN KEY (requested_to_department_id) REFERENCES departments(department_id) ON UPDATE CASCADE ON DELETE SET NULL`
- `CONSTRAINT fk_logistics_received_by FOREIGN KEY (received_by) REFERENCES users(user_id) ON UPDATE CASCADE ON DELETE SET NULL`
- `CONSTRAINT fk_logistics_assigned_to FOREIGN KEY (assigned_to_user_id) REFERENCES users(user_id) ON UPDATE CASCADE ON DELETE SET NULL`
- `CONSTRAINT fk_logistics_assigned_by FOREIGN KEY (assigned_by) REFERENCES users(user_id) ON UPDATE CASCADE ON DELETE SET NULL`
- `CONSTRAINT fk_logistics_proposed_by FOREIGN KEY (proposed_by) REFERENCES users(user_id) ON UPDATE CASCADE ON DELETE SET NULL`
- `CONSTRAINT fk_logistics_proposal_responded_by FOREIGN KEY (proposal_responded_by) REFERENCES users(user_id) ON UPDATE CASCADE ON DELETE SET NULL`
- `CONSTRAINT fk_logistics_handover_by FOREIGN KEY (handover_confirmed_by) REFERENCES users(user_id) ON UPDATE CASCADE ON DELETE SET NULL`
- `CONSTRAINT fk_logistics_service_report_signed_by FOREIGN KEY (service_report_signed_by) REFERENCES users(user_id) ON UPDATE CASCADE ON DELETE SET NULL`
- `CONSTRAINT fk_logistics_service_report_file FOREIGN KEY (service_report_file_id) REFERENCES files(file_id) ON UPDATE CASCADE ON DELETE SET NULL`

**Check Constraints:**
- `CHECK (quantity IS NULL OR quantity >= 1)`
- `CHECK (usage_end_at IS NULL OR usage_start_at IS NULL OR usage_end_at > usage_start_at)`
- `CHECK (proposed_quantity IS NULL OR proposed_quantity >= 1)`
- `CHECK (proposed_usage_end_at IS NULL OR proposed_usage_start_at IS NULL OR proposed_usage_end_at > proposed_usage_start_at)`

### 4.22. `minutes`

**Purpose / Table Comment:** Meeting Minutes

**Main Screens / UC Area:** Meeting Minutes

**Columns:**

| Column | Type | Null | Default | Extra | Enum Values | Notes |
|---|---|---:|---|---|---|---|
| `minutes_id` | `BIGINT UNSIGNED` | NO | `` | AUTO_INCREMENT |  |  |
| `visit_instance_id` | `BIGINT UNSIGNED` | NO | `` |  |  |  |
| `title` | `VARCHAR(255)` | NO | `` |  |  |  |
| `content` | `LONGTEXT` | YES | `` |  |  |  |
| `status` | `ENUM('DRAFT','FINAL')` | NO | `'DRAFT'` |  | `DRAFT`, `FINAL` | DRAFT=đang soạn, FINAL=đã chốt |
| `finalized_by` | `BIGINT UNSIGNED` | YES | `` |  |  | Người chốt biên bản |
| `finalized_at` | `DATETIME` | YES | `` |  |  | Thời điểm chốt biên bản |
| `created_at` | `DATETIME` | NO | `CURRENT_TIMESTAMP` |  |  |  |
| `created_by` | `BIGINT UNSIGNED` | YES | `` |  |  |  |
| `updated_at` | `DATETIME` | YES | `NULL` | ON UPDATE CURRENT_TIMESTAMP |  |  |
| `updated_by` | `BIGINT UNSIGNED` | YES | `` |  |  |  |

**Primary Key:**
- `PRIMARY KEY (minutes_id)`

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

### 4.23. `minute_participants`

**Purpose / Table Comment:** Meeting Minutes Participants

**Main Screens / UC Area:** Meeting Minutes Participants

**Columns:**

| Column | Type | Null | Default | Extra | Enum Values | Notes |
|---|---|---:|---|---|---|---|
| `minute_participant_id` | `BIGINT UNSIGNED` | NO | `` | AUTO_INCREMENT |  |  |
| `minutes_id` | `BIGINT UNSIGNED` | NO | `` |  |  |  |
| `user_id` | `BIGINT UNSIGNED` | YES | `` |  |  |  |
| `guest_member_id` | `BIGINT UNSIGNED` | YES | `` |  |  |  |
| `full_name_snapshot` | `VARCHAR(255)` | NO | `` |  |  |  |
| `role_snapshot` | `VARCHAR(120)` | YES | `` |  |  |  |
| `organization_snapshot` | `VARCHAR(255)` | YES | `` |  |  |  |
| `email_snapshot` | `VARCHAR(150)` | YES | `` |  |  |  |
| `display_order` | `INT UNSIGNED` | NO | `0` |  |  |  |
| `created_at` | `DATETIME` | NO | `CURRENT_TIMESTAMP` |  |  |  |

**Primary Key:**
- `PRIMARY KEY (minute_participant_id)`

**Indexes:**
- `KEY idx_minute_participants_minutes_order (minutes_id, display_order)`
- `KEY idx_minute_participants_user (user_id)`
- `KEY idx_minute_participants_guest_member (guest_member_id)`

**Foreign Keys:**
- `CONSTRAINT fk_minute_participants_minutes FOREIGN KEY (minutes_id) REFERENCES minutes(minutes_id) ON UPDATE CASCADE ON DELETE CASCADE`
- `CONSTRAINT fk_minute_participants_user FOREIGN KEY (user_id) REFERENCES users(user_id) ON UPDATE CASCADE ON DELETE SET NULL`
- `CONSTRAINT fk_minute_participants_guest_member FOREIGN KEY (guest_member_id) REFERENCES visit_guest_members(guest_member_id) ON UPDATE CASCADE ON DELETE SET NULL`

### 4.24. `minute_action_items`

**Purpose / Table Comment:** Meeting Minutes Action Items

**Main Screens / UC Area:** Meeting Minutes Action Items

**Columns:**

| Column | Type | Null | Default | Extra | Enum Values | Notes |
|---|---|---:|---|---|---|---|
| `action_item_id` | `BIGINT UNSIGNED` | NO | `` | AUTO_INCREMENT |  |  |
| `minutes_id` | `BIGINT UNSIGNED` | NO | `` |  |  |  |
| `title` | `VARCHAR(255)` | NO | `` |  |  | Tên đầu việc |
| `note` | `TEXT` | YES | `` |  |  | Ghi chú thêm cho đầu việc |
| `due_date` | `DATE` | YES | `` |  |  | Deadline của đầu việc |
| `status` | `ENUM('TODO','IN_PROGRESS','DONE','CANCELLED')` | NO | `'TODO'` |  | `TODO`, `IN_PROGRESS`, `DONE`, `CANCELLED` | TODO=chưa làm, IN_PROGRESS=đang làm, DONE=hoàn thành, CANCELLED=đã hủy/không cần làm nữa |
| `completed_at` | `DATETIME` | YES | `` |  |  | Thời điểm hoàn thành; backend tự set khi status chuyển sang DONE |
| `display_order` | `INT UNSIGNED` | NO | `1` |  |  | Thứ tự hiển thị trong biên bản |
| `created_at` | `DATETIME` | NO | `CURRENT_TIMESTAMP` |  |  |  |
| `created_by` | `BIGINT UNSIGNED` | YES | `` |  |  |  |
| `updated_at` | `DATETIME` | YES | `NULL` | ON UPDATE CURRENT_TIMESTAMP |  |  |
| `updated_by` | `BIGINT UNSIGNED` | YES | `` |  |  |  |

**Primary Key:**
- `PRIMARY KEY (action_item_id)`

**Indexes:**
- `KEY idx_action_items_minutes (minutes_id)`
- `KEY idx_action_items_status_due (status, due_date)`
- `KEY idx_action_items_order (minutes_id, display_order)`
- `KEY idx_action_items_created_by_time (created_by, created_at)`

**Foreign Keys:**
- `CONSTRAINT fk_action_items_minutes FOREIGN KEY (minutes_id) REFERENCES minutes(minutes_id) ON UPDATE CASCADE ON DELETE CASCADE`
- `CONSTRAINT fk_action_items_created_by FOREIGN KEY (created_by) REFERENCES users(user_id) ON UPDATE CASCADE ON DELETE SET NULL`
- `CONSTRAINT fk_action_items_updated_by FOREIGN KEY (updated_by) REFERENCES users(user_id) ON UPDATE CASCADE ON DELETE SET NULL`

### 4.25. `feedbacks`

**Purpose / Table Comment:** Delegation Feedback

**Main Screens / UC Area:** Delegation Feedback

**Columns:**

| Column | Type | Null | Default | Extra | Enum Values | Notes |
|---|---|---:|---|---|---|---|
| `feedback_id` | `BIGINT UNSIGNED` | NO | `` | AUTO_INCREMENT |  |  |
| `visit_request_id` | `BIGINT UNSIGNED` | NO | `` |  |  |  |
| `visit_instance_id` | `BIGINT UNSIGNED` | YES | `` |  |  |  |
| `submitted_by_user_id` | `BIGINT UNSIGNED` | NO | `` |  |  | User gửi feedback; khách/host/logistics đều phải có tài khoản hệ thống |
| `submitter_role` | `ENUM('VISITOR','HOST','LOGISTICS')` | NO | `` |  | `VISITOR`, `HOST`, `LOGISTICS` | Vai trò người gửi trong chuyến thăm |
| `submitter_context` | `VARCHAR(120)` | NO | `''` |  |  | Ngữ cảnh vai trò người gửi, ví dụ: Host chính, Xe điện, Teabreak, Khách đại diện |
| `submitter_name_snapshot` | `VARCHAR(255)` | NO | `` |  |  | Tên người gửi tại thời điểm gửi feedback |
| `target_user_id` | `BIGINT UNSIGNED` | NO | `` |  |  | User được đánh giá |
| `target_role` | `ENUM('VISITOR','HOST','LOGISTICS')` | NO | `` |  | `VISITOR`, `HOST`, `LOGISTICS` | Vai trò người được đánh giá trong chuyến thăm |
| `target_context` | `VARCHAR(120)` | NO | `''` |  |  | Ngữ cảnh đối tượng được đánh giá, ví dụ: Host chính, Đoàn khách, Xe điện, Teabreak |
| `target_name_snapshot` | `VARCHAR(255)` | NO | `` |  |  | Tên người được đánh giá tại thời điểm gửi feedback |
| `rating` | `TINYINT UNSIGNED` | NO | `` |  |  | Số sao từ 1 đến 5 |
| `comment` | `TEXT` | NO | `` |  |  | Nội dung feedback |
| `submitted_at` | `DATETIME` | NO | `CURRENT_TIMESTAMP` |  |  |  |

**Primary Key:**
- `PRIMARY KEY (feedback_id)`

**Indexes:**
- `KEY idx_feedbacks_visit_request (visit_request_id)`
- `KEY idx_feedbacks_visit_instance (visit_instance_id)`
- `KEY idx_feedbacks_submitter (submitted_by_user_id)`
- `KEY idx_feedbacks_target (target_user_id)`
- `KEY idx_feedbacks_roles (submitter_role, target_role)`
- `KEY idx_feedbacks_rating (rating)`
- `KEY idx_feedbacks_submitted_at (submitted_at)`

**Foreign Keys:**
- `CONSTRAINT fk_feedbacks_visit_request FOREIGN KEY (visit_request_id) REFERENCES visit_requests(visit_request_id) ON UPDATE CASCADE ON DELETE RESTRICT`
- `CONSTRAINT fk_feedbacks_visit_instance FOREIGN KEY (visit_instance_id) REFERENCES visit_request_campuses(visit_instance_id) ON UPDATE CASCADE ON DELETE SET NULL`
- `CONSTRAINT fk_feedbacks_submitter FOREIGN KEY (submitted_by_user_id) REFERENCES users(user_id) ON UPDATE CASCADE ON DELETE RESTRICT`
- `CONSTRAINT fk_feedbacks_target FOREIGN KEY (target_user_id) REFERENCES users(user_id) ON UPDATE CASCADE ON DELETE RESTRICT`

**Check Constraints:**
- `CONSTRAINT chk_feedbacks_rating CHECK (rating BETWEEN 1 AND 5)`
- `CONSTRAINT chk_feedbacks_role_flow CHECK ( (submitter_role IN ('VISITOR','LOGISTICS') AND target_role = 'HOST') OR (submitter_role = 'HOST' AND target_role IN ('VISITOR','LOGISTICS')) )`

### 4.26. `feedback_rating_items`

**Purpose / Table Comment:** Normalized per-criterion ratings for a feedback submission.

**Main Screens / UC Area:** Feedback Criteria Ratings

**Columns:**

| Column | Type | Null | Default | Extra | Enum Values | Notes |
|---|---|---:|---|---|---|---|
| `feedback_rating_item_id` | `BIGINT UNSIGNED` | NO | `` | AUTO_INCREMENT |  |  |
| `feedback_id` | `BIGINT UNSIGNED` | NO | `` |  |  |  |
| `criterion_code` | `VARCHAR(80)` | NO | `` |  |  |  |
| `criterion_label` | `VARCHAR(150)` | NO | `` |  |  |  |
| `rating` | `TINYINT UNSIGNED` | NO | `` |  |  |  |
| `display_order` | `INT UNSIGNED` | NO | `0` |  |  |  |
| `created_at` | `DATETIME` | NO | `CURRENT_TIMESTAMP` |  |  |  |

**Primary Key:**
- `PRIMARY KEY (feedback_rating_item_id)`

**Unique Constraints:**
- `UNIQUE KEY uq_feedback_rating_criterion (feedback_id, criterion_code)`

**Indexes:**
- `KEY idx_feedback_rating_feedback (feedback_id)`

**Foreign Keys:**
- `CONSTRAINT fk_feedback_rating_items_feedback FOREIGN KEY (feedback_id) REFERENCES feedbacks(feedback_id) ON UPDATE CASCADE ON DELETE CASCADE`

**Check Constraints:**
- `CHECK (rating BETWEEN 1 AND 5)`

### 4.27. `news`

**Purpose / Table Comment:** News Management

**Main Screens / UC Area:** News Management

**Columns:**

| Column | Type | Null | Default | Extra | Enum Values | Notes |
|---|---|---:|---|---|---|---|
| `news_id` | `BIGINT UNSIGNED` | NO | `` | AUTO_INCREMENT |  |  |
| `campus_id` | `BIGINT UNSIGNED` | YES | `` |  |  | Campus liên quan đến bài viết. NULL nếu bài toàn hệ thống |
| `visit_instance_id` | `BIGINT UNSIGNED` | YES | `` |  |  | Visit instance liên quan nếu bài viết được tạo từ một chuyến tiếp đón |
| `author_user_id` | `BIGINT UNSIGNED` | NO | `` |  |  | Người tạo/viết bài |
| `cover_file_id` | `BIGINT UNSIGNED` | YES | `` |  |  | Ảnh bìa bài viết, trỏ tới files.file_id |
| `status` | `ENUM('PENDING_REVIEW','REJECTED','PUBLISHED','HIDDEN')` | NO | `'PENDING_REVIEW'` |  | `PENDING_REVIEW`, `REJECTED`, `PUBLISHED`, `HIDDEN` | PENDING_REVIEW=chờ host duyệt, REJECTED=bị từ chối, PUBLISHED=đã đăng, HIDDEN=ẩn khỏi trang tin |
| `submitted_at` | `DATETIME` | NO | `CURRENT_TIMESTAMP` |  |  | Thời điểm người viết gửi bài cho host duyệt |
| `reviewed_by` | `BIGINT UNSIGNED` | YES | `` |  |  | Host duyệt hoặc từ chối bài viết |
| `reviewed_at` | `DATETIME` | YES | `` |  |  | Thời điểm host duyệt hoặc từ chối |
| `review_note` | `TEXT` | YES | `` |  |  | Ghi chú duyệt hoặc lý do từ chối |
| `published_at` | `DATETIME` | YES | `` |  |  | Thời điểm bài viết được đăng |
| `is_featured` | `BOOLEAN` | NO | `FALSE` |  |  | Bài viết nổi bật |
| `row_version` | `INT UNSIGNED` | NO | `0` |  |  | Optimistic concurrency token, chống ghi đè khi cập nhật đồng thời |
| `created_at` | `DATETIME` | NO | `CURRENT_TIMESTAMP` |  |  |  |
| `created_by` | `BIGINT UNSIGNED` | YES | `` |  |  |  |
| `updated_at` | `DATETIME` | YES | `NULL` | ON UPDATE CURRENT_TIMESTAMP |  |  |
| `updated_by` | `BIGINT UNSIGNED` | YES | `` |  |  |  |

**Primary Key:**
- `PRIMARY KEY (news_id)`

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

### 4.28. `news_translations`

**Purpose / Table Comment:** Tiêu đề, slug, tóm tắt và SEO của bài viết theo ngôn ngữ

**Main Screens / UC Area:** News Multilingual Content

**Columns:**

| Column | Type | Null | Default | Extra | Enum Values | Notes |
|---|---|---:|---|---|---|---|
| `news_translation_id` | `BIGINT UNSIGNED` | NO | `` | AUTO_INCREMENT |  |  |
| `news_id` | `BIGINT UNSIGNED` | NO | `` |  |  |  |
| `language_code` | `ENUM('vi','en','zh','ja','ko')` | NO | `'vi'` |  | `vi`, `en`, `zh`, `ja`, `ko` |  |
| `title` | `VARCHAR(255)` | NO | `` |  |  | Tiêu đề chính của bài viết |
| `slug` | `VARCHAR(255)` | NO | `` |  |  | Đường dẫn SEO của bài viết |
| `summary` | `TEXT` | YES | `` |  |  | Tóm tắt bài viết |
| `seo_title` | `VARCHAR(255)` | YES | `` |  |  |  |
| `seo_description` | `VARCHAR(500)` | YES | `` |  |  |  |
| `created_at` | `DATETIME` | NO | `CURRENT_TIMESTAMP` |  |  |  |
| `updated_at` | `DATETIME` | YES | `NULL` | ON UPDATE CURRENT_TIMESTAMP |  |  |

**Primary Key:**
- `PRIMARY KEY (news_translation_id)`

**Unique Constraints:**
- `UNIQUE KEY uq_news_translation_lang (news_id, language_code)`
- `UNIQUE KEY uq_news_translation_slug_lang (slug, language_code)`

**Indexes:**
- `KEY idx_news_translations_lang (language_code)`
- `FULLTEXT KEY ft_news_translations_search (title, summary)`

**Foreign Keys:**
- `CONSTRAINT fk_news_translations_news FOREIGN KEY (news_id) REFERENCES news(news_id) ON UPDATE CASCADE ON DELETE CASCADE`

### 4.29. `news_content_sections`

**Purpose / Table Comment:** Các khối nội dung chi tiết của bài viết, tối đa 10 section mỗi bản dịch

**Main Screens / UC Area:** News Rich Content Sections

**Columns:**

| Column | Type | Null | Default | Extra | Enum Values | Notes |
|---|---|---:|---|---|---|---|
| `section_id` | `BIGINT UNSIGNED` | NO | `` | AUTO_INCREMENT |  |  |
| `news_translation_id` | `BIGINT UNSIGNED` | NO | `` |  |  |  |
| `section_order` | `TINYINT UNSIGNED` | NO | `` |  |  | Thứ tự section, từ 1 đến 10 |
| `section_title` | `VARCHAR(255)` | NO | `` |  |  | Tiêu đề section |
| `section_body_html` | `LONGTEXT` | NO | `` |  |  | Nội dung rich text dạng HTML đã sanitize, có thể chứa paragraph, bold, italic, color, link, image |
| `section_body_text` | `TEXT` | YES | `` |  |  | Plain text tách từ HTML để search hoặc preview |
| `created_at` | `DATETIME` | NO | `CURRENT_TIMESTAMP` |  |  |  |
| `updated_at` | `DATETIME` | YES | `NULL` | ON UPDATE CURRENT_TIMESTAMP |  |  |

**Primary Key:**
- `PRIMARY KEY (section_id)`

**Unique Constraints:**
- `UNIQUE KEY uq_news_section_order (news_translation_id, section_order)`

**Indexes:**
- `KEY idx_news_sections_translation (news_translation_id)`
- `FULLTEXT KEY ft_news_sections_search (section_title, section_body_text)`

**Foreign Keys:**
- `CONSTRAINT fk_news_sections_translation FOREIGN KEY (news_translation_id) REFERENCES news_translations(news_translation_id) ON UPDATE CASCADE ON DELETE CASCADE`

**Check Constraints:**
- `CHECK (section_order BETWEEN 1 AND 10)`

### 4.30. `news_section_files`

**Purpose / Table Comment:** File/ảnh được dùng trong từng section của bài news

**Main Screens / UC Area:** News Attachments / Inline Images

**Columns:**

| Column | Type | Null | Default | Extra | Enum Values | Notes |
|---|---|---:|---|---|---|---|
| `section_file_id` | `BIGINT UNSIGNED` | NO | `` | AUTO_INCREMENT |  |  |
| `section_id` | `BIGINT UNSIGNED` | NO | `` |  |  |  |
| `file_id` | `BIGINT UNSIGNED` | NO | `` |  |  |  |
| `usage_type` | `ENUM('INLINE_IMAGE','ATTACHMENT')` | NO | `'INLINE_IMAGE'` |  | `INLINE_IMAGE`, `ATTACHMENT` | INLINE_IMAGE=ảnh chèn trong nội dung, ATTACHMENT=file đính kèm |
| `display_order` | `INT UNSIGNED` | NO | `0` |  |  |  |
| `created_at` | `DATETIME` | NO | `CURRENT_TIMESTAMP` |  |  |  |

**Primary Key:**
- `PRIMARY KEY (section_file_id)`

**Unique Constraints:**
- `UNIQUE KEY uq_news_section_file (section_id, file_id)`

**Indexes:**
- `KEY idx_news_section_files_section (section_id)`
- `KEY idx_news_section_files_file (file_id)`

**Foreign Keys:**
- `CONSTRAINT fk_news_section_files_section FOREIGN KEY (section_id) REFERENCES news_content_sections(section_id) ON UPDATE CASCADE ON DELETE CASCADE`
- `CONSTRAINT fk_news_section_files_file FOREIGN KEY (file_id) REFERENCES files(file_id) ON UPDATE CASCADE ON DELETE RESTRICT`

### 4.31. `faqs`

**Purpose / Table Comment:** FAQ một ngôn ngữ, chỉ dùng PUBLISHED/HIDDEN

**Main Screens / UC Area:** FAQ Management / Public FAQ

**Columns:**

| Column | Type | Null | Default | Extra | Enum Values | Notes |
|---|---|---:|---|---|---|---|
| `faq_id` | `BIGINT UNSIGNED` | NO | `` | AUTO_INCREMENT |  |  |
| `faq_type` | `ENUM('PROGRAM','TUITION_FEE','VISA','DORMITORY','VISIT_REQUEST','SECURITY','LOGISTICS','OTHER')` | NO | `'OTHER'` |  | `PROGRAM`, `TUITION_FEE`, `VISA`, `DORMITORY`, `VISIT_REQUEST`, `SECURITY`, `LOGISTICS`, `OTHER` |  |
| `language_code` | `ENUM('vi','en')` | NO | `'vi'` |  | `vi`, `en` |  |
| `question` | `VARCHAR(500)` | NO | `` |  |  | Câu hỏi FAQ |
| `answer` | `TEXT` | NO | `` |  |  | Câu trả lời FAQ |
| `display_order` | `INT UNSIGNED` | NO | `0` |  |  |  |
| `status` | `ENUM('PUBLISHED','HIDDEN')` | NO | `'HIDDEN'` |  | `PUBLISHED`, `HIDDEN` | PUBLISHED=hiển thị trên trang FAQ, HIDDEN=ẩn khỏi người xem thường nhưng người quản lý vẫn thấy |
| `created_at` | `DATETIME` | NO | `CURRENT_TIMESTAMP` |  |  |  |
| `created_by` | `BIGINT UNSIGNED` | YES | `` |  |  |  |
| `updated_at` | `DATETIME` | YES | `NULL` | ON UPDATE CURRENT_TIMESTAMP |  |  |
| `updated_by` | `BIGINT UNSIGNED` | YES | `` |  |  |  |

**Primary Key:**
- `PRIMARY KEY (faq_id)`

**Indexes:**
- `KEY idx_faqs_status_order (status, display_order)`
- `KEY idx_faqs_type_status (faq_type, status)`
- `KEY idx_faqs_language_status (language_code, status)`
- `FULLTEXT KEY ft_faqs_search (question, answer)`

### 4.32. `galleries`

**Purpose / Table Comment:** Gallery địa điểm trong campus, có mô tả và câu chuyện

**Main Screens / UC Area:** Gallery Management / Public Gallery

**Columns:**

| Column | Type | Null | Default | Extra | Enum Values | Notes |
|---|---|---:|---|---|---|---|
| `gallery_id` | `BIGINT UNSIGNED` | NO | `` | AUTO_INCREMENT |  |  |
| `campus_id` | `BIGINT UNSIGNED` | NO | `` |  |  |  |
| `area_name` | `VARCHAR(150)` | NO | `'Campus'` |  |  | Khu vực trong campus, ví dụ: Academic Area, Lobby, Lab Zone |
| `specific_location_name` | `VARCHAR(150)` | NO | `'Campus location'` |  |  | Vị trí cụ thể trong khu vực, ví dụ: Sảnh Alpha, Green Lab |
| `location_description` | `TEXT` | YES | `` |  |  | Mô tả vị trí/khu vực hiển thị ở Gallery/Visit FPTU |
| `title` | `VARCHAR(255)` | NO | `` |  |  | Tên hiển thị của gallery/địa điểm |
| `description` | `TEXT` | YES | `` |  |  | Mô tả ngắn về địa điểm |
| `story_content` | `TEXT` | YES | `` |  |  | Ý nghĩa hoặc câu chuyện giới thiệu về địa điểm |
| `status` | `ENUM('DRAFT','PUBLISHED','HIDDEN')` | NO | `'DRAFT'` |  | `DRAFT`, `PUBLISHED`, `HIDDEN` | DRAFT=nháp, PUBLISHED=hiển thị theo visibility, HIDDEN=ẩn khỏi người xem thường nhưng Staff Leader vẫn quản lý được |
| `visibility` | `ENUM('PRIVATE','INTERNAL','PUBLIC')` | NO | `'INTERNAL'` |  | `PRIVATE`, `INTERNAL`, `PUBLIC` | Phạm vi xem khi status=PUBLISHED: PRIVATE=chỉ quản lý, INTERNAL=user nội bộ, PUBLIC=công khai |
| `hero_file_id` | `BIGINT UNSIGNED` | YES | `` |  |  |  |
| `virtual_tour_url` | `VARCHAR(700)` | YES | `` |  |  |  |
| `created_at` | `DATETIME` | NO | `CURRENT_TIMESTAMP` |  |  |  |
| `created_by` | `BIGINT UNSIGNED` | YES | `` |  |  |  |
| `updated_at` | `DATETIME` | YES | `NULL` | ON UPDATE CURRENT_TIMESTAMP |  |  |
| `updated_by` | `BIGINT UNSIGNED` | YES | `` |  |  |  |
| `deleted_at` | `DATETIME` | YES | `` |  |  |  |
| `deleted_by` | `BIGINT UNSIGNED` | YES | `` |  |  |  |

**Primary Key:**
- `PRIMARY KEY (gallery_id)`

**Indexes:**
- `KEY idx_galleries_campus_status (campus_id, status, deleted_at)`
- `KEY idx_galleries_area_specific (campus_id, area_name, specific_location_name)`
- `KEY idx_galleries_visibility_status (visibility, status)`
- `KEY idx_galleries_hero_file (hero_file_id)`

**Foreign Keys:**
- `CONSTRAINT fk_galleries_campus FOREIGN KEY (campus_id) REFERENCES campuses(campus_id) ON UPDATE CASCADE ON DELETE RESTRICT`
- `CONSTRAINT fk_galleries_hero_file FOREIGN KEY (hero_file_id) REFERENCES files(file_id) ON UPDATE CASCADE ON DELETE SET NULL`

### 4.33. `gallery_images`

**Purpose / Table Comment:** Ảnh thuộc gallery địa điểm campus

**Main Screens / UC Area:** Gallery Media Items

**Columns:**

| Column | Type | Null | Default | Extra | Enum Values | Notes |
|---|---|---:|---|---|---|---|
| `image_id` | `BIGINT UNSIGNED` | NO | `` | AUTO_INCREMENT |  |  |
| `gallery_id` | `BIGINT UNSIGNED` | NO | `` |  |  |  |
| `file_id` | `BIGINT UNSIGNED` | NO | `` |  |  |  |
| `media_type` | `ENUM('IMAGE','VIDEO')` | NO | `'IMAGE'` |  | `IMAGE`, `VIDEO` |  |
| `thumbnail_file_id` | `BIGINT UNSIGNED` | YES | `` |  |  |  |
| `caption` | `VARCHAR(500)` | YES | `` |  |  | Chú thích riêng cho từng ảnh |
| `display_order` | `INT UNSIGNED` | NO | `0` |  |  |  |
| `taken_at` | `DATETIME` | YES | `` |  |  |  |
| `status` | `ENUM('ACTIVE','HIDDEN')` | NO | `'ACTIVE'` |  | `ACTIVE`, `HIDDEN` | ACTIVE=ảnh đang dùng, HIDDEN=ảnh bị ẩn khỏi gallery thường |
| `created_at` | `DATETIME` | NO | `CURRENT_TIMESTAMP` |  |  |  |
| `created_by` | `BIGINT UNSIGNED` | YES | `` |  |  |  |
| `updated_at` | `DATETIME` | YES | `NULL` | ON UPDATE CURRENT_TIMESTAMP |  |  |
| `updated_by` | `BIGINT UNSIGNED` | YES | `` |  |  |  |
| `deleted_at` | `DATETIME` | YES | `` |  |  |  |
| `deleted_by` | `BIGINT UNSIGNED` | YES | `` |  |  |  |

**Primary Key:**
- `PRIMARY KEY (image_id)`

**Unique Constraints:**
- `UNIQUE KEY uq_gallery_images_file (file_id)`

**Indexes:**
- `KEY idx_gallery_images_gallery_order (gallery_id, display_order)`
- `KEY idx_gallery_images_status_time (status, taken_at)`
- `KEY idx_gallery_images_media_type (media_type)`
- `KEY idx_gallery_images_thumbnail_file (thumbnail_file_id)`

**Foreign Keys:**
- `CONSTRAINT fk_gallery_images_gallery FOREIGN KEY (gallery_id) REFERENCES galleries(gallery_id) ON UPDATE CASCADE ON DELETE RESTRICT`
- `CONSTRAINT fk_gallery_images_file FOREIGN KEY (file_id) REFERENCES files(file_id) ON UPDATE CASCADE ON DELETE RESTRICT`
- `CONSTRAINT fk_gallery_images_thumbnail_file FOREIGN KEY (thumbnail_file_id) REFERENCES files(file_id) ON UPDATE CASCADE ON DELETE SET NULL`

### 4.34. `photo_face_tags`

**Purpose / Table Comment:** Confirmed face tag metadata only. No biometric vector.

**Main Screens / UC Area:** Photo Face Tagging

**Columns:**

| Column | Type | Null | Default | Extra | Enum Values | Notes |
|---|---|---:|---|---|---|---|
| `face_tag_id` | `BIGINT UNSIGNED` | NO | `` | AUTO_INCREMENT |  |  |
| `image_id` | `BIGINT UNSIGNED` | NO | `` |  |  |  |
| `visit_request_id` | `BIGINT UNSIGNED` | YES | `` |  |  |  |
| `guest_member_id` | `BIGINT UNSIGNED` | YES | `` |  |  |  |
| `partner_contact_id` | `BIGINT UNSIGNED` | YES | `` |  |  |  |
| `display_name` | `VARCHAR(150)` | NO | `` |  |  |  |
| `bounding_box_x` | `DECIMAL(8,4)` | YES | `` |  |  |  |
| `bounding_box_y` | `DECIMAL(8,4)` | YES | `` |  |  |  |
| `bounding_box_width` | `DECIMAL(8,4)` | YES | `` |  |  |  |
| `bounding_box_height` | `DECIMAL(8,4)` | YES | `` |  |  |  |
| `tag_status` | `ENUM('MANUALLY_TAGGED','CONFIRMED','REMOVED')` | NO | `'MANUALLY_TAGGED'` |  | `MANUALLY_TAGGED`, `CONFIRMED`, `REMOVED` |  |
| `confirmed_by` | `BIGINT UNSIGNED` | YES | `` |  |  |  |
| `confirmed_at` | `DATETIME` | YES | `` |  |  |  |
| `created_at` | `DATETIME` | NO | `CURRENT_TIMESTAMP` |  |  |  |
| `created_by` | `BIGINT UNSIGNED` | YES | `` |  |  |  |
| `removed_at` | `DATETIME` | YES | `` |  |  |  |
| `removed_by` | `BIGINT UNSIGNED` | YES | `` |  |  |  |

**Primary Key:**
- `PRIMARY KEY (face_tag_id)`

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

### 4.35. `email_templates`

**Purpose / Table Comment:** Email templates with explicit VI/EN subject/body fields

**Main Screens / UC Area:** Email Template Management

**Columns:**

| Column | Type | Null | Default | Extra | Enum Values | Notes |
|---|---|---:|---|---|---|---|
| `email_template_id` | `BIGINT UNSIGNED` | NO | `` | AUTO_INCREMENT |  |  |
| `template_code` | `VARCHAR(100)` | NO | `` |  |  |  |
| `name` | `VARCHAR(150)` | NO | `` |  |  |  |
| `purpose` | `VARCHAR(100)` | NO | `` |  |  |  |
| `campus_id` | `BIGINT UNSIGNED` | YES | `` |  |  |  |
| `description` | `VARCHAR(500)` | YES | `` |  |  |  |
| `status` | `ENUM('ACTIVE','INACTIVE')` | NO | `'ACTIVE'` |  | `ACTIVE`, `INACTIVE` |  |
| `subject_vi` | `VARCHAR(255)` | YES | `` |  |  |  |
| `body_vi` | `LONGTEXT` | YES | `` |  |  |  |
| `subject_en` | `VARCHAR(255)` | YES | `` |  |  |  |
| `body_en` | `LONGTEXT` | YES | `` |  |  |  |
| `variables_text` | `VARCHAR(700)` | YES | `` |  |  |  |
| `created_at` | `DATETIME` | NO | `CURRENT_TIMESTAMP` |  |  |  |
| `created_by` | `BIGINT UNSIGNED` | YES | `` |  |  |  |
| `updated_at` | `DATETIME` | YES | `NULL` | ON UPDATE CURRENT_TIMESTAMP |  |  |
| `updated_by` | `BIGINT UNSIGNED` | YES | `` |  |  |  |

**Primary Key:**
- `PRIMARY KEY (email_template_id)`

**Unique Constraints:**
- `UNIQUE KEY uq_email_templates_code (template_code)`

**Indexes:**
- `KEY idx_email_templates_status (status)`
- `KEY idx_email_templates_purpose_status (purpose, status)`
- `KEY idx_email_templates_campus_status (campus_id, status)`

**Foreign Keys:**
- `CONSTRAINT fk_email_templates_campus FOREIGN KEY (campus_id) REFERENCES campuses(campus_id) ON UPDATE CASCADE ON DELETE SET NULL`

### 4.36. `sent_emails`

**Purpose / Table Comment:** Sent Email Log

**Main Screens / UC Area:** Sent Email Log

**Columns:**

| Column | Type | Null | Default | Extra | Enum Values | Notes |
|---|---|---:|---|---|---|---|
| `sent_email_id` | `BIGINT UNSIGNED` | NO | `` | AUTO_INCREMENT |  |  |
| `email_template_id` | `BIGINT UNSIGNED` | YES | `` |  |  |  |
| `related_type` | `VARCHAR(80)` | YES | `` |  |  |  |
| `related_id` | `BIGINT UNSIGNED` | YES | `` |  |  |  |
| `subject` | `VARCHAR(255)` | NO | `` |  |  |  |
| `body_snapshot` | `LONGTEXT` | YES | `` |  |  |  |
| `provider_thread_id` | `VARCHAR(255)` | YES | `` |  |  |  |
| `provider_message_id` | `VARCHAR(255)` | YES | `` |  |  |  |
| `retry_count` | `INT UNSIGNED` | NO | `0` |  |  |  |
| `last_attempt_at` | `DATETIME` | YES | `` |  |  |  |
| `delivered_at` | `DATETIME` | YES | `` |  |  |  |
| `status` | `ENUM('QUEUED','SENT','FAILED')` | NO | `'QUEUED'` |  | `QUEUED`, `SENT`, `FAILED` |  |
| `error_message` | `TEXT` | YES | `` |  |  |  |
| `sent_by` | `BIGINT UNSIGNED` | YES | `` |  |  |  |
| `sent_at` | `DATETIME` | YES | `` |  |  |  |
| `created_at` | `DATETIME` | NO | `CURRENT_TIMESTAMP` |  |  |  |

**Primary Key:**
- `PRIMARY KEY (sent_email_id)`

**Indexes:**
- `KEY idx_sent_emails_template (email_template_id)`
- `KEY idx_sent_emails_related (related_type, related_id)`
- `KEY idx_sent_emails_status_time (status, created_at)`
- `KEY idx_sent_emails_sent_by_time (sent_by, sent_at)`
- `KEY idx_sent_emails_provider_thread (provider_thread_id)`
- `KEY idx_sent_emails_provider_message (provider_message_id)`

**Foreign Keys:**
- `CONSTRAINT fk_sent_emails_template FOREIGN KEY (email_template_id) REFERENCES email_templates(email_template_id) ON UPDATE CASCADE ON DELETE SET NULL`
- `CONSTRAINT fk_sent_emails_sent_by FOREIGN KEY (sent_by) REFERENCES users(user_id) ON UPDATE CASCADE ON DELETE SET NULL`

### 4.37. `sent_email_recipients`

**Purpose / Table Comment:** Sent Email Recipients

**Main Screens / UC Area:** Sent Email Recipients

**Columns:**

| Column | Type | Null | Default | Extra | Enum Values | Notes |
|---|---|---:|---|---|---|---|
| `sent_email_recipient_id` | `BIGINT UNSIGNED` | NO | `` | AUTO_INCREMENT |  |  |
| `sent_email_id` | `BIGINT UNSIGNED` | NO | `` |  |  |  |
| `recipient_email` | `VARCHAR(150)` | NO | `` |  |  |  |
| `recipient_name` | `VARCHAR(150)` | YES | `` |  |  |  |
| `recipient_type` | `ENUM('TO','CC','BCC')` | NO | `'TO'` |  | `TO`, `CC`, `BCC` |  |
| `delivery_status` | `ENUM('QUEUED','SENT','DELIVERED','FAILED','BOUNCED')` | NO | `'QUEUED'` |  | `QUEUED`, `SENT`, `DELIVERED`, `FAILED`, `BOUNCED` |  |
| `provider_message_id` | `VARCHAR(255)` | YES | `` |  |  |  |
| `error_message` | `TEXT` | YES | `` |  |  |  |
| `sent_at` | `DATETIME` | YES | `` |  |  |  |
| `delivered_at` | `DATETIME` | YES | `` |  |  |  |
| `created_at` | `DATETIME` | NO | `CURRENT_TIMESTAMP` |  |  |  |

**Primary Key:**
- `PRIMARY KEY (sent_email_recipient_id)`

**Indexes:**
- `KEY idx_sent_email_recipients_sent_email (sent_email_id)`
- `KEY idx_sent_email_recipients_email_status (recipient_email, delivery_status)`
- `FULLTEXT KEY ft_sent_email_recipients_search (recipient_email, recipient_name)`

**Foreign Keys:**
- `CONSTRAINT fk_sent_email_recipients_email FOREIGN KEY (sent_email_id) REFERENCES sent_emails(sent_email_id) ON UPDATE CASCADE ON DELETE CASCADE`

### 4.38. `notifications`

**Purpose / Table Comment:** In-app notifications

**Main Screens / UC Area:** Notification Center

**Columns:**

| Column | Type | Null | Default | Extra | Enum Values | Notes |
|---|---|---:|---|---|---|---|
| `notification_id` | `BIGINT UNSIGNED` | NO | `` | AUTO_INCREMENT |  |  |
| `recipient_user_id` | `BIGINT UNSIGNED` | NO | `` |  |  |  |
| `title` | `VARCHAR(255)` | NO | `` |  |  |  |
| `message` | `TEXT` | YES | `` |  |  |  |
| `notification_type` | `VARCHAR(80)` | NO | `` |  |  |  |
| `related_type` | `VARCHAR(80)` | YES | `` |  |  |  |
| `related_id` | `BIGINT UNSIGNED` | YES | `` |  |  |  |
| `is_read` | `BOOLEAN` | NO | `FALSE` |  |  |  |
| `read_at` | `DATETIME` | YES | `` |  |  |  |
| `created_at` | `DATETIME` | NO | `CURRENT_TIMESTAMP` |  |  |  |

**Primary Key:**
- `PRIMARY KEY (notification_id)`

**Indexes:**
- `KEY idx_notifications_user_read_time (recipient_user_id, is_read, created_at)`
- `KEY idx_notifications_related (related_type, related_id)`
- `KEY idx_notifications_type_time (notification_type, created_at)`

**Foreign Keys:**
- `CONSTRAINT fk_notifications_user FOREIGN KEY (recipient_user_id) REFERENCES users(user_id) ON UPDATE CASCADE ON DELETE CASCADE`

### 4.39. `calendar_events`

**Purpose / Table Comment:** Calendar events. Attendees/reminders are normalized in child tables.

**Main Screens / UC Area:** Calendar / My Events / Department Calendar

**Columns:**

| Column | Type | Null | Default | Extra | Enum Values | Notes |
|---|---|---:|---|---|---|---|
| `calendar_event_id` | `BIGINT UNSIGNED` | NO | `` | AUTO_INCREMENT |  |  |
| `owner_user_id` | `BIGINT UNSIGNED` | NO | `` |  |  |  |
| `campus_id` | `BIGINT UNSIGNED` | YES | `` |  |  |  |
| `visit_instance_id` | `BIGINT UNSIGNED` | YES | `` |  |  |  |
| `logistics_item_id` | `BIGINT UNSIGNED` | YES | `` |  |  |  |
| `source_type` | `ENUM('PERSONAL','VISIT','LOGISTICS','DEADLINE')` | NO | `'PERSONAL'` |  | `PERSONAL`, `VISIT`, `LOGISTICS`, `DEADLINE` |  |
| `title` | `VARCHAR(255)` | NO | `` |  |  |  |
| `description` | `TEXT` | YES | `` |  |  |  |
| `location` | `VARCHAR(255)` | YES | `` |  |  |  |
| `start_at` | `DATETIME` | NO | `` |  |  |  |
| `end_at` | `DATETIME` | NO | `` |  |  |  |
| `timezone` | `VARCHAR(50)` | NO | `'Asia/Ho_Chi_Minh'` |  |  |  |
| `is_all_day` | `BOOLEAN` | NO | `FALSE` |  |  |  |
| `recurrence_rule` | `VARCHAR(500)` | YES | `` |  |  |  |
| `visibility` | `ENUM('PRIVATE','INTERNAL')` | NO | `'PRIVATE'` |  | `PRIVATE`, `INTERNAL` |  |
| `status` | `ENUM('ACTIVE','CANCELLED','DONE')` | NO | `'ACTIVE'` |  | `ACTIVE`, `CANCELLED`, `DONE` |  |
| `created_at` | `DATETIME` | NO | `CURRENT_TIMESTAMP` |  |  |  |
| `created_by` | `BIGINT UNSIGNED` | YES | `` |  |  |  |
| `updated_at` | `DATETIME` | YES | `NULL` | ON UPDATE CURRENT_TIMESTAMP |  |  |
| `updated_by` | `BIGINT UNSIGNED` | YES | `` |  |  |  |
| `deleted_at` | `DATETIME` | YES | `` |  |  |  |
| `deleted_by` | `BIGINT UNSIGNED` | YES | `` |  |  |  |

**Primary Key:**
- `PRIMARY KEY (calendar_event_id)`

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

### 4.40. `calendar_event_attendees`

**Purpose / Table Comment:** Calendar Attendees

**Main Screens / UC Area:** Calendar Attendees

**Columns:**

| Column | Type | Null | Default | Extra | Enum Values | Notes |
|---|---|---:|---|---|---|---|
| `calendar_event_attendee_id` | `BIGINT UNSIGNED` | NO | `` | AUTO_INCREMENT |  |  |
| `calendar_event_id` | `BIGINT UNSIGNED` | NO | `` |  |  |  |
| `user_id` | `BIGINT UNSIGNED` | YES | `` |  |  |  |
| `attendee_email` | `VARCHAR(150)` | YES | `` |  |  |  |
| `attendee_name` | `VARCHAR(150)` | YES | `` |  |  |  |
| `attendee_role` | `VARCHAR(80)` | YES | `` |  |  |  |
| `response_status` | `ENUM('NEEDS_ACTION','ACCEPTED','DECLINED','TENTATIVE')` | NO | `'NEEDS_ACTION'` |  | `NEEDS_ACTION`, `ACCEPTED`, `DECLINED`, `TENTATIVE` |  |
| `created_at` | `DATETIME` | NO | `CURRENT_TIMESTAMP` |  |  |  |

**Primary Key:**
- `PRIMARY KEY (calendar_event_attendee_id)`

**Indexes:**
- `KEY idx_calendar_attendees_event (calendar_event_id)`
- `KEY idx_calendar_attendees_user (user_id)`
- `KEY idx_calendar_attendees_email (attendee_email)`

**Foreign Keys:**
- `CONSTRAINT fk_calendar_attendees_event FOREIGN KEY (calendar_event_id) REFERENCES calendar_events(calendar_event_id) ON UPDATE CASCADE ON DELETE CASCADE`
- `CONSTRAINT fk_calendar_attendees_user FOREIGN KEY (user_id) REFERENCES users(user_id) ON UPDATE CASCADE ON DELETE SET NULL`

### 4.41. `calendar_event_reminders`

**Purpose / Table Comment:** Calendar Reminders

**Main Screens / UC Area:** Calendar Reminders

**Columns:**

| Column | Type | Null | Default | Extra | Enum Values | Notes |
|---|---|---:|---|---|---|---|
| `calendar_event_reminder_id` | `BIGINT UNSIGNED` | NO | `` | AUTO_INCREMENT |  |  |
| `calendar_event_id` | `BIGINT UNSIGNED` | NO | `` |  |  |  |
| `reminder_type` | `ENUM('EMAIL','POPUP','IN_APP')` | NO | `'IN_APP'` |  | `EMAIL`, `POPUP`, `IN_APP` |  |
| `minutes_before` | `INT UNSIGNED` | NO | `0` |  |  |  |
| `scheduled_at` | `DATETIME` | YES | `` |  |  |  |
| `sent_at` | `DATETIME` | YES | `` |  |  |  |
| `status` | `ENUM('PENDING','SENT','CANCELLED','FAILED')` | NO | `'PENDING'` |  | `PENDING`, `SENT`, `CANCELLED`, `FAILED` |  |
| `created_at` | `DATETIME` | NO | `CURRENT_TIMESTAMP` |  |  |  |

**Primary Key:**
- `PRIMARY KEY (calendar_event_reminder_id)`

**Indexes:**
- `KEY idx_calendar_reminders_event (calendar_event_id)`
- `KEY idx_calendar_reminders_status_schedule (status, scheduled_at)`

**Foreign Keys:**
- `CONSTRAINT fk_calendar_reminders_event FOREIGN KEY (calendar_event_id) REFERENCES calendar_events(calendar_event_id) ON UPDATE CASCADE ON DELETE CASCADE`

### 4.42. `api_configurations`

**Purpose / Table Comment:** API config + encrypted credentials JSON

**Main Screens / UC Area:** API Configuration Management

**Columns:**

| Column | Type | Null | Default | Extra | Enum Values | Notes |
|---|---|---:|---|---|---|---|
| `api_config_id` | `BIGINT UNSIGNED` | NO | `` | AUTO_INCREMENT |  |  |
| `api_code` | `VARCHAR(100)` | NO | `` |  |  |  |
| `name` | `VARCHAR(150)` | NO | `` |  |  |  |
| `provider_name` | `VARCHAR(150)` | YES | `` |  |  |  |
| `purpose` | `VARCHAR(150)` | YES | `` |  |  |  |
| `base_url` | `VARCHAR(500)` | NO | `` |  |  |  |
| `default_method` | `ENUM('GET','POST','PUT','PATCH','DELETE')` | NO | `'POST'` |  | `GET`, `POST`, `PUT`, `PATCH`, `DELETE` |  |
| `auth_type` | `ENUM('NONE','API_KEY','BEARER_TOKEN','BASIC','OAUTH2','CUSTOM')` | NO | `'NONE'` |  | `NONE`, `API_KEY`, `BEARER_TOKEN`, `BASIC`, `OAUTH2`, `CUSTOM` |  |
| `api_key_encrypted` | `VARCHAR(700)` | YES | `` |  |  |  |
| `bearer_token_encrypted` | `VARCHAR(700)` | YES | `` |  |  |  |
| `basic_username` | `VARCHAR(150)` | YES | `` |  |  |  |
| `basic_password_encrypted` | `VARCHAR(700)` | YES | `` |  |  |  |
| `oauth_client_id` | `VARCHAR(255)` | YES | `` |  |  |  |
| `oauth_client_secret_encrypted` | `VARCHAR(700)` | YES | `` |  |  |  |
| `oauth_token_url` | `VARCHAR(700)` | YES | `` |  |  |  |
| `oauth_scope` | `VARCHAR(500)` | YES | `` |  |  |  |
| `body_template_text` | `LONGTEXT` | YES | `` |  |  |  |
| `rate_limit_per_minute` | `INT UNSIGNED` | YES | `` |  |  |  |
| `monthly_quota` | `INT UNSIGNED` | YES | `` |  |  |  |
| `retry_enabled` | `BOOLEAN` | NO | `FALSE` |  |  |  |
| `max_retries` | `INT UNSIGNED` | NO | `0` |  |  |  |
| `cache_ttl_seconds` | `INT UNSIGNED` | YES | `` |  |  |  |
| `last_test_status` | `ENUM('SUCCESS','FAILED')` | YES | `` |  | `SUCCESS`, `FAILED` |  |
| `last_tested_at` | `DATETIME` | YES | `` |  |  |  |
| `last_test_message` | `TEXT` | YES | `` |  |  |  |
| `timeout_seconds` | `INT UNSIGNED` | NO | `30` |  |  |  |
| `status` | `ENUM('ACTIVE','INACTIVE','DISABLED')` | NO | `'ACTIVE'` |  | `ACTIVE`, `INACTIVE`, `DISABLED` |  |
| `created_at` | `DATETIME` | NO | `CURRENT_TIMESTAMP` |  |  |  |
| `created_by` | `BIGINT UNSIGNED` | YES | `` |  |  |  |
| `updated_at` | `DATETIME` | YES | `NULL` | ON UPDATE CURRENT_TIMESTAMP |  |  |
| `updated_by` | `BIGINT UNSIGNED` | YES | `` |  |  |  |
| `deleted_at` | `DATETIME` | YES | `` |  |  |  |
| `deleted_by` | `BIGINT UNSIGNED` | YES | `` |  |  |  |

**Primary Key:**
- `PRIMARY KEY (api_config_id)`

**Unique Constraints:**
- `UNIQUE KEY uq_api_config_code (api_code)`

**Indexes:**
- `KEY idx_api_config_status (status)`
- `KEY idx_api_config_test_status (last_test_status, last_tested_at)`
- `KEY idx_api_provider_status (provider_name, status)`

### 4.43. `api_configuration_headers`

**Purpose / Table Comment:** API Request Headers

**Main Screens / UC Area:** API Request Headers

**Columns:**

| Column | Type | Null | Default | Extra | Enum Values | Notes |
|---|---|---:|---|---|---|---|
| `api_configuration_header_id` | `BIGINT UNSIGNED` | NO | `` | AUTO_INCREMENT |  |  |
| `api_config_id` | `BIGINT UNSIGNED` | NO | `` |  |  |  |
| `header_name` | `VARCHAR(150)` | NO | `` |  |  |  |
| `header_value_encrypted` | `VARCHAR(1000)` | YES | `` |  |  |  |
| `is_secret` | `BOOLEAN` | NO | `TRUE` |  |  |  |
| `created_at` | `DATETIME` | NO | `CURRENT_TIMESTAMP` |  |  |  |

**Primary Key:**
- `PRIMARY KEY (api_configuration_header_id)`

**Unique Constraints:**
- `UNIQUE KEY uq_api_header_name (api_config_id, header_name)`

**Indexes:**
- `KEY idx_api_headers_config (api_config_id)`

**Foreign Keys:**
- `CONSTRAINT fk_api_headers_config FOREIGN KEY (api_config_id) REFERENCES api_configurations(api_config_id) ON UPDATE CASCADE ON DELETE CASCADE`

### 4.44. `api_usage_quotas`

**Purpose / Table Comment:** API quota + counter per campus/month

**Main Screens / UC Area:** API Quota Management

**Columns:**

| Column | Type | Null | Default | Extra | Enum Values | Notes |
|---|---|---:|---|---|---|---|
| `api_usage_quota_id` | `BIGINT UNSIGNED` | NO | `` | AUTO_INCREMENT |  |  |
| `api_config_id` | `BIGINT UNSIGNED` | NO | `` |  |  |  |
| `campus_id` | `BIGINT UNSIGNED` | YES | `` |  |  | NULL = global quota |
| `campus_scope_key` | `VARCHAR(36)` | NO | `'GLOBAL'` |  |  |  |
| `period_yyyymm` | `CHAR(6)` | NO | `` |  |  | YYYYMM |
| `monthly_limit` | `INT UNSIGNED` | NO | `` |  |  |  |
| `used_count` | `INT UNSIGNED` | NO | `0` |  |  | Merged api_usage_counters table |
| `last_used_at` | `DATETIME` | YES | `` |  |  |  |
| `created_at` | `DATETIME` | NO | `CURRENT_TIMESTAMP` |  |  |  |
| `created_by` | `BIGINT UNSIGNED` | YES | `` |  |  |  |
| `updated_at` | `DATETIME` | YES | `NULL` | ON UPDATE CURRENT_TIMESTAMP |  |  |
| `updated_by` | `BIGINT UNSIGNED` | YES | `` |  |  |  |

**Primary Key:**
- `PRIMARY KEY (api_usage_quota_id)`

**Unique Constraints:**
- `UNIQUE KEY uq_api_quota_config_scope_period (api_config_id, campus_scope_key, period_yyyymm)`

**Indexes:**
- `KEY idx_api_quota_campus_period (campus_id, period_yyyymm)`
- `KEY idx_api_quota_period (period_yyyymm)`

**Foreign Keys:**
- `CONSTRAINT fk_api_quota_config FOREIGN KEY (api_config_id) REFERENCES api_configurations(api_config_id) ON UPDATE CASCADE ON DELETE CASCADE`
- `CONSTRAINT fk_api_quota_campus FOREIGN KEY (campus_id) REFERENCES campuses(campus_id) ON UPDATE CASCADE ON DELETE CASCADE`

### 4.45. `api_request_logs`

**Purpose / Table Comment:** External API request logs. Never log full secret/token.

**Main Screens / UC Area:** API Request Logs

**Columns:**

| Column | Type | Null | Default | Extra | Enum Values | Notes |
|---|---|---:|---|---|---|---|
| `api_request_log_id` | `BIGINT UNSIGNED` | NO | `` | AUTO_INCREMENT |  |  |
| `api_config_id` | `BIGINT UNSIGNED` | NO | `` |  |  |  |
| `campus_id` | `BIGINT UNSIGNED` | YES | `` |  |  |  |
| `requested_by` | `BIGINT UNSIGNED` | YES | `` |  |  |  |
| `related_type` | `VARCHAR(80)` | YES | `` |  |  |  |
| `related_id` | `BIGINT UNSIGNED` | YES | `` |  |  |  |
| `endpoint` | `VARCHAR(500)` | NO | `` |  |  |  |
| `method` | `ENUM('GET','POST','PUT','PATCH','DELETE')` | NO | `` |  | `GET`, `POST`, `PUT`, `PATCH`, `DELETE` |  |
| `http_status` | `INT` | YES | `` |  |  |  |
| `response_time_ms` | `INT UNSIGNED` | YES | `` |  |  |  |
| `request_size_bytes` | `BIGINT UNSIGNED` | YES | `` |  |  |  |
| `response_size_bytes` | `BIGINT UNSIGNED` | YES | `` |  |  |  |
| `success` | `BOOLEAN` | NO | `FALSE` |  |  |  |
| `error_code` | `VARCHAR(100)` | YES | `` |  |  |  |
| `error_message` | `TEXT` | YES | `` |  |  |  |
| `created_at` | `DATETIME` | NO | `CURRENT_TIMESTAMP` |  |  |  |

**Primary Key:**
- `PRIMARY KEY (api_request_log_id)`

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

### 4.46. `agenda_templates`

**Purpose / Table Comment:** Agenda Template Management

**Main Screens / UC Area:** Agenda Template Management

**Columns:**

| Column | Type | Null | Default | Extra | Enum Values | Notes |
|---|---|---:|---|---|---|---|
| `agenda_template_id` | `BIGINT UNSIGNED` | NO | `` | AUTO_INCREMENT |  |  |
| `campus_id` | `BIGINT UNSIGNED` | YES | `` |  |  |  |
| `campus_scope_key` | `VARCHAR(36)` | NO | `'GLOBAL'` |  |  |  |
| `name` | `VARCHAR(150)` | NO | `` |  |  |  |
| `description` | `TEXT` | YES | `` |  |  |  |
| `status` | `ENUM('ACTIVE','INACTIVE')` | NO | `'ACTIVE'` |  | `ACTIVE`, `INACTIVE` |  |
| `created_at` | `DATETIME` | NO | `CURRENT_TIMESTAMP` |  |  |  |
| `created_by` | `BIGINT UNSIGNED` | YES | `` |  |  |  |
| `updated_at` | `DATETIME` | YES | `NULL` | ON UPDATE CURRENT_TIMESTAMP |  |  |
| `updated_by` | `BIGINT UNSIGNED` | YES | `` |  |  |  |
| `deleted_at` | `DATETIME` | YES | `` |  |  |  |
| `deleted_by` | `BIGINT UNSIGNED` | YES | `` |  |  |  |

**Primary Key:**
- `PRIMARY KEY (agenda_template_id)`

**Unique Constraints:**
- `UNIQUE KEY uq_agenda_template_scope_name (campus_scope_key, name)`

**Indexes:**
- `KEY idx_agenda_templates_status (status)`
- `KEY idx_agenda_templates_campus_status (campus_id, status)`

**Foreign Keys:**
- `CONSTRAINT fk_agenda_templates_campus FOREIGN KEY (campus_id) REFERENCES campuses(campus_id) ON UPDATE CASCADE ON DELETE SET NULL`

### 4.47. `agenda_template_items`

**Purpose / Table Comment:** Agenda Template Timeline Items

**Main Screens / UC Area:** Agenda Template Timeline Items

**Columns:**

| Column | Type | Null | Default | Extra | Enum Values | Notes |
|---|---|---:|---|---|---|---|
| `agenda_template_item_id` | `BIGINT UNSIGNED` | NO | `` | AUTO_INCREMENT |  |  |
| `agenda_template_id` | `BIGINT UNSIGNED` | NO | `` |  |  |  |
| `display_order` | `INT UNSIGNED` | NO | `0` |  |  |  |
| `start_time` | `TIME` | YES | `` |  |  |  |
| `end_time` | `TIME` | YES | `` |  |  |  |
| `title` | `VARCHAR(255)` | NO | `` |  |  |  |
| `description` | `TEXT` | YES | `` |  |  |  |
| `created_at` | `DATETIME` | NO | `CURRENT_TIMESTAMP` |  |  |  |

**Primary Key:**
- `PRIMARY KEY (agenda_template_item_id)`

**Indexes:**
- `KEY idx_agenda_template_items_template_order (agenda_template_id, display_order)`

**Foreign Keys:**
- `CONSTRAINT fk_agenda_template_items_template FOREIGN KEY (agenda_template_id) REFERENCES agenda_templates(agenda_template_id) ON UPDATE CASCADE ON DELETE CASCADE`

**Check Constraints:**
- `CHECK (end_time IS NULL OR start_time IS NULL OR end_time > start_time)`

### 4.48. `audit_logs`

**Purpose / Table Comment:** General audit log

**Main Screens / UC Area:** Audit Trail

**Columns:**

| Column | Type | Null | Default | Extra | Enum Values | Notes |
|---|---|---:|---|---|---|---|
| `audit_log_id` | `BIGINT UNSIGNED` | NO | `` | AUTO_INCREMENT |  |  |
| `actor_user_id` | `BIGINT UNSIGNED` | YES | `` |  |  |  |
| `campus_id` | `BIGINT UNSIGNED` | YES | `` |  |  |  |
| `action` | `VARCHAR(100)` | NO | `` |  |  |  |
| `entity_type` | `VARCHAR(100)` | NO | `` |  |  |  |
| `entity_id` | `BIGINT UNSIGNED` | YES | `` |  |  |  |
| `ip_address` | `VARCHAR(45)` | YES | `` |  |  |  |
| `user_agent` | `VARCHAR(500)` | YES | `` |  |  |  |
| `request_id` | `VARCHAR(100)` | YES | `` |  |  |  |
| `created_at` | `DATETIME` | NO | `CURRENT_TIMESTAMP` |  |  |  |

**Primary Key:**
- `PRIMARY KEY (audit_log_id)`

**Indexes:**
- `KEY idx_audit_actor_time (actor_user_id, created_at)`
- `KEY idx_audit_entity (entity_type, entity_id)`
- `KEY idx_audit_action_time (action, created_at)`
- `KEY idx_audit_campus_time (campus_id, created_at)`
- `KEY idx_audit_request (request_id)`

**Foreign Keys:**
- `CONSTRAINT fk_audit_actor FOREIGN KEY (actor_user_id) REFERENCES users(user_id) ON UPDATE CASCADE ON DELETE SET NULL`
- `CONSTRAINT fk_audit_campus FOREIGN KEY (campus_id) REFERENCES campuses(campus_id) ON UPDATE CASCADE ON DELETE SET NULL`

### 4.49. `audit_log_changes`

**Purpose / Table Comment:** Field-Level Audit Changes

**Main Screens / UC Area:** Field-Level Audit Changes

**Columns:**

| Column | Type | Null | Default | Extra | Enum Values | Notes |
|---|---|---:|---|---|---|---|
| `audit_log_change_id` | `BIGINT UNSIGNED` | NO | `` | AUTO_INCREMENT |  |  |
| `audit_log_id` | `BIGINT UNSIGNED` | NO | `` |  |  |  |
| `field_name` | `VARCHAR(150)` | NO | `` |  |  |  |
| `old_value_text` | `LONGTEXT` | YES | `` |  |  |  |
| `new_value_text` | `LONGTEXT` | YES | `` |  |  |  |
| `created_at` | `DATETIME` | NO | `CURRENT_TIMESTAMP` |  |  |  |

**Primary Key:**
- `PRIMARY KEY (audit_log_change_id)`

**Indexes:**
- `KEY idx_audit_changes_log (audit_log_id)`
- `KEY idx_audit_changes_field (field_name)`

**Foreign Keys:**
- `CONSTRAINT fk_audit_changes_log FOREIGN KEY (audit_log_id) REFERENCES audit_logs(audit_log_id) ON UPDATE CASCADE ON DELETE CASCADE`

### 4.50. `visit_status_logs`

**Purpose / Table Comment:** Timeline trạng thái visit. Log rõ cấp REQUEST hoặc CAMPUS_INSTANCE để không nhầm request_status với campus_status.

**Main Screens / UC Area:** Visit Status History

**Columns:**

| Column | Type | Null | Default | Extra | Enum Values | Notes |
|---|---|---:|---|---|---|---|
| `visit_status_log_id` | `BIGINT UNSIGNED` | NO | `` | AUTO_INCREMENT |  |  |
| `visit_request_id` | `BIGINT UNSIGNED` | YES | `` |  |  |  |
| `visit_instance_id` | `BIGINT UNSIGNED` | YES | `` |  |  |  |
| `status_owner_type` | `ENUM('REQUEST','CAMPUS_INSTANCE')` | NO | `'CAMPUS_INSTANCE'` |  | `REQUEST`, `CAMPUS_INSTANCE` | REQUEST=visit_requests.status, CAMPUS_INSTANCE=visit_request_campuses.status |
| `old_status` | `VARCHAR(50)` | YES | `` |  |  |  |
| `new_status` | `VARCHAR(50)` | NO | `` |  |  |  |
| `changed_by` | `BIGINT UNSIGNED` | YES | `` |  |  |  |
| `reason` | `TEXT` | YES | `` |  |  |  |
| `changed_at` | `DATETIME` | NO | `CURRENT_TIMESTAMP` |  |  |  |

**Primary Key:**
- `PRIMARY KEY (visit_status_log_id)`

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

The SQL source contains view definitions for visit visibility and progress. Some views are re-created later in the file; the later definition should be treated as the effective one when executing the SQL.

| # | View |
|---:|---|
| 1 | `vw_visit_requests_for_ho` |
| 2 | `vw_visit_requests_for_staff_leader` |
| 3 | `vw_visit_requests_for_admin` |
| 4 | `vw_visit_request_progress_summary` |

## 6. Triggers

| # | Trigger | Main Rule Area |
|---:|---|---|
| 1 | `trg_departments_one_ic_bi` | Ensure one IC department per campus |
| 2 | `trg_departments_one_ic_bu` | Ensure one IC department per campus |
| 3 | `trg_users_validate_bi` | User role/sub-role/campus/department validation |
| 4 | `trg_users_validate_bu` | User role/sub-role/campus/department validation |
| 5 | `trg_auth_providers_validate_bi` | Authentication provider validation |
| 6 | `trg_auth_providers_validate_bu` | Authentication provider validation |
| 7 | `trg_sessions_validate_bi` | Session portal/campus validation |
| 8 | `trg_visit_requests_decision_validate_bi` | Visit request decision actor/scope validation |
| 9 | `trg_visit_requests_decision_validate_bu` | Visit request decision actor/scope validation |
| 10 | `trg_visit_requests_cancel_validate_bu` | Cancellation status/source validation |
| 11 | `trg_visit_campuses_cancel_validate_bu` | Cancellation status/source validation |
| 12 | `trg_visit_campuses_assignment_validate_bi` | Host assignment validation |
| 13 | `trg_visit_campuses_assignment_validate_bu` | Host assignment validation |
| 14 | `trg_api_usage_quotas_scope_bi` | API quota scope key validation |
| 15 | `trg_api_usage_quotas_scope_bu` | API quota scope key validation |
| 16 | `trg_agenda_templates_scope_bi` | Agenda template scope key validation |
| 17 | `trg_agenda_templates_scope_bu` | Agenda template scope key validation |
| 18 | `trg_feedbacks_not_self_bi` | Prevent invalid self feedback |
| 19 | `trg_feedbacks_not_self_bu` | Prevent invalid self feedback |

## 7. Enum Catalog

| # | Table.Column | Enum Values |
|---:|---|---|
| 1 | `roles.status` | `ACTIVE`, `INACTIVE` |
| 2 | `role_permissions.sub_role` | `NONE`, `LEADER`, `STAFF` |
| 3 | `role_permissions.permission_level` | `F`, `E`, `R`, `O` |
| 4 | `campuses.status` | `ACTIVE`, `INACTIVE` |
| 5 | `departments.department_type` | `IC`, `GENERAL` |
| 6 | `departments.status` | `ACTIVE`, `INACTIVE` |
| 7 | `users.sub_role` | `LEADER`, `STAFF` |
| 8 | `users.gender` | `MALE`, `FEMALE`, `OTHER`, `UNKNOWN` |
| 9 | `users.status` | `ACTIVE`, `INACTIVE`, `LOCKED` |
| 10 | `users.created_via` | `MANUAL_CREATED`, `VISITOR_FORM`, `SSO_AUTO_PROVISION` |
| 11 | `user_auth_providers.provider_type` | `LOCAL_PASSWORD`, `GOOGLE_SSO`, `FEID` |
| 12 | `user_sessions.login_portal` | `VISITOR`, `INTERNAL` |
| 13 | `otp_tokens.token_type` | `OTP_CODE`, `MAGIC_LINK` |
| 14 | `otp_tokens.purpose` | `VISIT_REQUEST_VERIFY`, `CHANGE_SENSITIVE_ACTION` |
| 15 | `login_logs.login_portal` | `VISITOR`, `INTERNAL` |
| 16 | `login_logs.provider_type` | `LOCAL_PASSWORD`, `GOOGLE_SSO`, `FEID` |
| 17 | `login_logs.status` | `SUCCESS`, `FAILED`, `BLOCKED` |
| 18 | `security_events.event_type` | `SSO_LOGIN`, `PORTAL_VALIDATION`, `CAMPUS_VALIDATION`, `VISITOR_AUTO_PROVISION`, `SESSION_CREATED`, `SESSION_REVOKED`, `SESSION_EXPIRED`, `TOKEN_REFRESH`, `SECURITY_POLICY_CHECK` |
| 19 | `security_events.result` | `SUCCESS`, `FAILED`, `BLOCKED` |
| 20 | `security_events.failure_reason_code` | `ACCOUNT_NOT_FOUND`, `ACCOUNT_DISABLED`, `PORTAL_MISMATCH`, `CAMPUS_MISMATCH`, `ROLE_MISMATCH`, `SSO_PROVIDER_ERROR`, `INVALID_SSO_CLAIMS`, `VISITOR_AUTO_PROVISION_DISABLED`, `SESSION_EXPIRED`, `TOKEN_REVOKED`, `SUSPICIOUS_IP`, `UNKNOWN` |
| 21 | `security_events.severity` | `LOW`, `MEDIUM`, `HIGH`, `CRITICAL` |
| 22 | `security_events.login_portal` | `VISITOR`, `INTERNAL` |
| 23 | `security_events.provider_type` | `GOOGLE_SSO`, `FEID` |
| 24 | `files.storage_provider` | `LOCAL`, `S3`, `AZURE`, `GCS`, `GOOGLE_DRIVE`, `OTHER` |
| 25 | `partners.partner_type` | `UNIVERSITY`, `COMPANY`, `GOVERNMENT`, `NGO`, `OTHER` |
| 26 | `partners.cooperation_status` | `POTENTIAL`, `ACTIVE`, `INACTIVE`, `BLACKLISTED` |
| 27 | `partners.profile_status` | `DRAFT`, `PENDING_APPROVAL`, `APPROVED`, `REJECTED` |
| 28 | `partners.visibility` | `PRIVATE`, `INTERNAL`, `PUBLIC` |
| 29 | `partner_contacts.source_type` | `MANUAL`, `BUSINESS_CARD_OCR`, `IMPORT` |
| 30 | `partner_contacts.status` | `ACTIVE`, `INACTIVE` |
| 31 | `documents.owner_type` | `GENERAL`, `VISIT`, `PARTNER`, `MINUTES`, `NEWS`, `LOGISTICS`, `REPORT` |
| 32 | `documents.status` | `DRAFT`, `PUBLISHED`, `ARCHIVED` |
| 33 | `visit_requests.created_source` | `VISITOR_SUBMITTED`, `STAFF_CREATED` |
| 34 | `visit_requests.visit_scope` | `SINGLE_CAMPUS`, `MULTI_CAMPUS` |
| 35 | `visit_requests.visit_type` | `CAMPUS_TOUR`, `MEETING`, `WORKSHOP`, `SIGNING_CEREMONY`, `EXCHANGE`, `OTHER` |
| 36 | `visit_requests.working_language` | `VI`, `EN` |
| 37 | `visit_requests.transportation_type` | `SELF_ARRANGED`, `FPTU_SUPPORT`, `UNKNOWN`, `OTHER` |
| 38 | `visit_requests.media_consent_status` | `AGREED`, `DECLINED`, `UNKNOWN` |
| 39 | `visit_requests.status` | `PENDING_APPROVAL`, `APPROVED`, `REJECTED`, `CANCELLED` |
| 40 | `visit_requests.decision_actor_role` | `HO`, `STAFF_LEADER`, `SYSTEM` |
| 41 | `visit_requests.cancellation_actor_type` | `VISITOR`, `HOST`, `STAFF_LEADER`, `HO`, `SYSTEM` |
| 42 | `visit_requests.cancellation_source` | `SELF_SERVICE`, `EXTERNAL_CONFIRMATION`, `INTERNAL_DECISION` |
| 43 | `visit_request_campuses.status` | `WAITING_REQUEST_APPROVAL`, `ASSIGNED`, `BEFORE_VISIT`, `DURING_VISIT`, `AFTER_VISIT`, `CLOSED`, `CANCELLED` |
| 44 | `visit_request_campuses.host_assignment_source` | `AUTO_STAFF_LEADER`, `MANUAL_APPROVAL`, `TRANSFERRED` |
| 45 | `visit_request_campuses.cancellation_actor_type` | `VISITOR`, `HOST`, `STAFF_LEADER`, `HO`, `SYSTEM` |
| 46 | `visit_request_campuses.cancellation_source` | `SELF_SERVICE`, `EXTERNAL_CONFIRMATION`, `INTERNAL_DECISION` |
| 47 | `visit_guest_members.member_type` | `GUEST`, `EXTERNAL_SUPPORT` |
| 48 | `visit_participants.participant_role` | `IC_HOST`, `IC_SUPPORT`, `DEPT_SUPPORT`, `STUDENT` |
| 49 | `visit_participants.status` | `INVITED`, `ACCEPTED`, `DECLINED`, `ASSIGNED`, `REMOVED` |
| 50 | `visit_logistics_items.item_type` | `ROOM`, `TRANSPORT`, `MEAL`, `EQUIPMENT`, `BANNER`, `LED`, `OTHER` |
| 51 | `visit_logistics_items.status` | `PLANNED`, `REQUESTED`, `CHANGE_PROPOSED`, `RECEIVED`, `ASSIGNED`, `ACCEPTED`, `IN_PROGRESS`, `READY`, `DONE`, `REJECTED`, `CANCELLED` |
| 52 | `visit_logistics_items.priority` | `LOW`, `MEDIUM`, `HIGH`, `URGENT` |
| 53 | `visit_logistics_items.proposal_response` | `ACCEPTED`, `REJECTED` |
| 54 | `minutes.status` | `DRAFT`, `FINAL` |
| 55 | `minute_action_items.status` | `TODO`, `IN_PROGRESS`, `DONE`, `CANCELLED` |
| 56 | `feedbacks.submitter_role` | `VISITOR`, `HOST`, `LOGISTICS` |
| 57 | `feedbacks.target_role` | `VISITOR`, `HOST`, `LOGISTICS` |
| 58 | `news.status` | `PENDING_REVIEW`, `REJECTED`, `PUBLISHED`, `HIDDEN` |
| 59 | `news_translations.language_code` | `vi`, `en`, `zh`, `ja`, `ko` |
| 60 | `news_section_files.usage_type` | `INLINE_IMAGE`, `ATTACHMENT` |
| 61 | `faqs.faq_type` | `PROGRAM`, `TUITION_FEE`, `VISA`, `DORMITORY`, `VISIT_REQUEST`, `SECURITY`, `LOGISTICS`, `OTHER` |
| 62 | `faqs.language_code` | `vi`, `en` |
| 63 | `faqs.status` | `PUBLISHED`, `HIDDEN` |
| 64 | `galleries.status` | `DRAFT`, `PUBLISHED`, `HIDDEN` |
| 65 | `galleries.visibility` | `PRIVATE`, `INTERNAL`, `PUBLIC` |
| 66 | `gallery_images.media_type` | `IMAGE`, `VIDEO` |
| 67 | `gallery_images.status` | `ACTIVE`, `HIDDEN` |
| 68 | `photo_face_tags.tag_status` | `MANUALLY_TAGGED`, `CONFIRMED`, `REMOVED` |
| 69 | `email_templates.status` | `ACTIVE`, `INACTIVE` |
| 70 | `sent_emails.status` | `QUEUED`, `SENT`, `FAILED` |
| 71 | `sent_email_recipients.recipient_type` | `TO`, `CC`, `BCC` |
| 72 | `sent_email_recipients.delivery_status` | `QUEUED`, `SENT`, `DELIVERED`, `FAILED`, `BOUNCED` |
| 73 | `calendar_events.source_type` | `PERSONAL`, `VISIT`, `LOGISTICS`, `DEADLINE` |
| 74 | `calendar_events.visibility` | `PRIVATE`, `INTERNAL` |
| 75 | `calendar_events.status` | `ACTIVE`, `CANCELLED`, `DONE` |
| 76 | `calendar_event_attendees.response_status` | `NEEDS_ACTION`, `ACCEPTED`, `DECLINED`, `TENTATIVE` |
| 77 | `calendar_event_reminders.reminder_type` | `EMAIL`, `POPUP`, `IN_APP` |
| 78 | `calendar_event_reminders.status` | `PENDING`, `SENT`, `CANCELLED`, `FAILED` |
| 79 | `api_configurations.default_method` | `GET`, `POST`, `PUT`, `PATCH`, `DELETE` |
| 80 | `api_configurations.auth_type` | `NONE`, `API_KEY`, `BEARER_TOKEN`, `BASIC`, `OAUTH2`, `CUSTOM` |
| 81 | `api_configurations.last_test_status` | `SUCCESS`, `FAILED` |
| 82 | `api_configurations.status` | `ACTIVE`, `INACTIVE`, `DISABLED` |
| 83 | `api_request_logs.method` | `GET`, `POST`, `PUT`, `PATCH`, `DELETE` |
| 84 | `agenda_templates.status` | `ACTIVE`, `INACTIVE` |
| 85 | `visit_status_logs.status_owner_type` | `REQUEST`, `CAMPUS_INSTANCE` |

## 8. Legacy / Removed Column Verification

These columns/tables were explicitly checked against the parsed `CREATE TABLE` definitions.

| Check | Result |
|---|---|
| Legacy JSON/business columns still present as columns | `None` |
| `public_content_blocks` table | `Not present` |
| `gallery_locations` table | `Not present` |
| `files.visibility` column | `Not present` |
| `working_language_other` column | `Not present` |

## 9. Replacement Checklist

When replacing the old schema document in the repository:

1. Replace `docs/database/DATABASE_SCHEMA.md` with this file content.
2. Ensure code/entity/enum/EF Core mapping references `pems_full_seed_logic_v8_4_fresh_create_only_idempotent_seed.sql`.
3. Search and remove runtime references to old JSON columns and dropped tables.
4. Re-run backend/frontend build and at least one real submit test for Public Visit Request.
