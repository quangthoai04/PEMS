> [!WARNING]
> **LEGACY ARCHITECTURE NOTE (Campus-independent Approval Update)**
> This document has been updated to reflect the new Campus-independent Approval architecture.
> - **HO is now monitor/read-only.** There is no centralized multi-campus approval by HO.
> - **Staff Leader approval is per-campus.** Each Staff Leader directly receives and approves/rejects their own campus instance right after submission.
> - **Self-hosting is supported.** Staff Leaders can assign themselves as the host during approval.
> - **Approving a campus requires naming its host in the same act.** There is no "approved but
>   nobody hosting" state. `ASSIGNED` is very much still in the lifecycle: it is where a campus sits
>   once it has a host, until that host explicitly starts preparation (`ASSIGNED → BEFORE_VISIT`).
> - **Per-campus operational contact + confirmation gate.** A request first sits at
>   `PENDING_CONTACT_CONFIRMATION` while each campus waits for its OWN guest-side contact to
>   confirm. Nothing is assigned and no setup data may be written until the LAST one confirms.
> - **Proposed host.** An internal creator may record who should host their own campus
>   (`host_selection_mode` = SELF / SELECTED / WAIT_FOR_LATER). That is an intention, not an
>   assignment: it is revalidated and activated only when the gate opens, and falls back to
>   `WAITING_REQUEST_APPROVAL` if it no longer holds. Nobody is ever auto-substituted.
> - **New statuses:** `PENDING_CONTACT_CONFIRMATION` and `PARTIALLY_APPROVED` (request level),
>   `WAITING_CONTACT_CONFIRMATION` and `REJECTED` (campus level).
> - **Cancel logic:** Visitors can cancel requests in `PENDING_CONTACT_CONFIRMATION`,
>   `PENDING_APPROVAL` or `PARTIALLY_APPROVED` states.
> - **Transportation:** the per-campus `transportation_note` replaced the older request-level note.
>
> Canonical source for the two rules above: `PEMS_CANONICAL_BUSINESS_RULES` Mục 6.3 and Mục 8.
> Please refer to the latest codebase and SQL schema for the current implementation.

# PEMS Database Schema — FULL v8.4 Refined V6 v10 No Dynamic Permissions

> **Generated from:** `pems_full_create_manual_wide_coverage_seed_v8_4_refined_v6_v10_clean_logistics_handover_fields.sql`  
> **Purpose:** Developer-facing database schema reference for the latest PEMS SQL source of truth after v10 updates.  
> **SQL style:** Fresh create-only schema; this document describes the final `CREATE TABLE` state and does not use migration/backfill `ALTER TABLE` logic.  

## 1. Overview

| Item | Value |
|---|---|
| Database | `pems_db` |
| Engine | MySQL 8.0 / InnoDB |
| Charset / Collation | `utf8mb4` / `utf8mb4_unicode_ci` |
| Schema Version | `PEMS v8.4 refined v6 v10 clean logistics handover fields` |
| Base Table Count | `49` |
| Total Column Count | `719` |
| Dynamic Permission Tables | `permissions` and `role_permissions` removed / not present |
| Fixed Role Source | `roles` table only stores fixed role classification: `ADMIN`, `HO`, `STAFF`, `DEPARTMENT`, `STUDENT`, `VISITOR` |
| Authorization Strategy | Backend/frontend fixed policy using `role_code`, `sub_role`, effectiveRole, campus/department scope, ownership and record status |
| Email Inbox Scope | No inbox/mail-receiving tables in v10. Email replies are not synced from mailbox in this phase. |
| Email Action Scope | One-time button responses are stored in `email_action_tokens` and then applied to business tables. |
| Logistics Handover Scope | Signing/borrow/return data is stored only in `visit_logistics_item_handovers`; old signing fields were removed from `visit_logistics_items`. |

## 2. V10 Changes Compared with v8.4 refined v6

1. `faqs`: removed `language_code`; FAQ is Vietnamese-only. `faq_type` was simplified to system-related values: `ACCOUNT_ACCESS`, `VISIT_REQUEST`, `DELEGATION_MANAGEMENT`, `LOGISTICS_RESOURCE`, `DOCUMENT_MEDIA`, `NOTIFICATION_EMAIL`, `OTHER`.
2. `partners`: added `owner_campus_id` with FK to `campuses`; Staff Leader partner approval must be scoped by this campus.
3. `visit_logistics_items`: removed old signing fields: `handover_confirmed_by`, `handover_confirmed_at`, `handover_note`, `service_report_signed_by`, `service_report_signed_at`, `service_report_file_id`.
4. `visit_logistics_item_handovers`: new table for BORROW/RETURN signing. It stores borrower/provider signatures, signature timestamps, item condition and attachment.
5. `email_action_tokens`: new table for one-time email button actions such as accept, decline, negotiate, proposal response, and handover confirmation.
6. No `email_threads`, `email_messages`, or `email_message_recipients` are added in v10. Email-reply/inbox sync is intentionally out of scope for this phase.
7. No logistics assignment-transfer table is added. Reassignment/chuyển nhiệm vụ is not supported; backend must prevent changing `assigned_to_user_id` after assignment if the business state says the task is already assigned.

## 3. No Dynamic Permissions Notes

- `permissions` table is intentionally removed.
- `role_permissions` table is intentionally removed.
- Permission seed and permission matrix seed are intentionally removed.
- Runtime authorization must use fixed role policy, effectiveRole and data-scope checks.
- Do not recreate DB-backed dynamic permissions in backend, frontend, SQL seed, or docs.

## 4. Table List

| # | Table | Module / Main Screens | Column Count |
|---:|---|---|---:|
| 1 | `roles` | Authentication / Account Management / Fixed Role Policy | 6 |
| 2 | `campuses` | Campus Management / Visit Request / Account Management | 13 |
| 3 | `departments` | Department Management / Account Management / Logistics | 10 |
| 4 | `users` | Login / Profile / Account Management / Visitor Portal | 25 |
| 5 | `user_auth_providers` | Login / SSO / FEID / Local password DEV | 8 |
| 6 | `user_sessions` | Login / Logout / Refresh Token / Session Security | 15 |
| 7 | `otp_tokens` | OTP Verification / Submit Visit Request / Forgot Password | 14 |
| 8 | `login_logs` | Security audit / Login monitoring | 12 |
| 9 | `security_events` | Security audit / Auth hardening | 15 |
| 10 | `files` | File metadata / Documents / Gallery / News / Partner / Minutes | 15 |
| 11 | `partners` | Partner Management / Public Partners / Staff Leader Approval Scope | 24 |
| 12 | `partner_contacts` | Partner Contact / Scan Business Card | 17 |
| 13 | `documents` | Document Management / Archive | 13 |
| 14 | `visit_requests` | Public Submit Visit Request / Delegation Management / Approval / Cancellation | 40 |
| 15 | `visit_request_campuses` | Campus Visit Instance / Staff Leader Processing / Host Assignment / Lifecycle | 25 |
| 16 | `visit_guest_members` | Guest Member List / Delegation Detail / Minutes Source | 12 |
| 17 | `visit_participants` | Internal Participants / Host / Department / Student Assignments / Email Action Responses | 16 |
| 18 | `visit_agendas` | Visit Agenda / Logistics Preparation / Delegation Detail | 14 |
| 19 | `visit_logistics_items` | Logistics / Resource Request / Assignment / Negotiation / Completion | 39 |
| 20 | `visit_logistics_item_handovers` | Logistics Borrow/Return Handover Signatures | 12 |
| 21 | `minutes` | Meeting Minutes / Close Delegation | 14 |
| 22 | `minute_participants` | Minutes Attendance / Participant Snapshot | 14 |
| 23 | `minute_action_items` | Minutes Action Items / Follow-up | 12 |
| 24 | `feedbacks` | Delegation Feedback / Feedback Summary | 14 |
| 25 | `feedback_rating_items` | Feedback Rating Criteria / Analytics | 7 |
| 26 | `news` | News Management / Public News / Approval / Publish | 17 |
| 27 | `news_translations` | Multilingual News / AI Translation | 10 |
| 28 | `news_content_sections` | News Editor / Content Sections | 8 |
| 29 | `news_section_files` | News Media Attachments | 6 |
| 30 | `faqs` | Vietnamese FAQ Management / Public FAQ | 10 |
| 31 | `galleries` | Gallery Management / Public Gallery | 18 |
| 32 | `gallery_images` | Gallery Item Management / Public Gallery | 15 |
| 33 | `photo_face_tags` | Photo Tagging / Gallery Moderation | 17 |
| 34 | `email_templates` | Email Template Management | 16 |
| 35 | `sent_emails` | Email Outbox / Delivery Tracking | 16 |
| 36 | `sent_email_recipients` | Email Delivery Tracking per Recipient | 11 |
| 37 | `email_action_tokens` | One-time Email Button Actions / Public Token Responses | 19 |
| 38 | `notifications` | Notification Center / Dashboard Alerts | 10 |
| 39 | `calendar_events` | Calendar / Visit / Personal Events / Deadlines | 22 |
| 40 | `calendar_event_attendees` | Calendar Event Attendees | 8 |
| 41 | `calendar_event_reminders` | Calendar Reminders | 8 |
| 42 | `api_configurations` | External API Management / Integration Settings | 33 |
| 43 | `api_configuration_headers` | External API Headers | 6 |
| 44 | `api_usage_quotas` | API Quota / Usage Monitoring | 12 |
| 45 | `api_request_logs` | API Logs / Debugging | 16 |
| 46 | `agenda_templates` | Agenda Template Management (by visit_type + campus/GLOBAL scope) | 13 |
| 47 | `agenda_template_items` | Agenda Template Timeline Items (relative offset + duration) | 13 |
| 47b | `agenda_template_defaults` | Default agenda template mapping by (campus_scope_key, visit_type) | 9 |
| 48 | `audit_logs` | System Audit / Business Audit | 10 |
| 49 | `audit_log_changes` | Audit Field-level Changes | 6 |

## 5. Table Details

### 5.1. `roles`
**Purpose / Table Comment:** Fixed system roles for account classification. No DB-backed dynamic permission matrix.

**Main Screens / UC Area:** Authentication / Account Management / Fixed Role Policy

**Column Count:** `6`

**Columns:**

| Column | Type | Null | Default | Extra | Key / FK / Index | Enum Values | Notes / Meaning |
|---|---|---:|---|---|---|---|---|
| `role_id` | `BIGINT UNSIGNED` | NO |  | AUTO_INCREMENT | PK |  | Identifier/reference field used to join or scope `roles` records. |
| `role_code` | `VARCHAR(30)` | NO |  |  | UNIQUE: uq_roles_code |  | ADMIN, HO, STAFF, DEPARTMENT, STUDENT, VISITOR |
| `name` | `VARCHAR(100)` | NO |  |  |  |  | Field `name` used by `roles` business logic and screens. |
| `description` | `VARCHAR(255)` | YES |  |  |  |  | Field `description` used by `roles` business logic and screens. |
| `status` | `ENUM('ACTIVE','INACTIVE')` | NO | 'ACTIVE' |  | IDX: idx_roles_status | ACTIVE, INACTIVE | Status field used for workflow state, filtering and UI badges. |
| `created_at` | `DATETIME` | NO | CURRENT_TIMESTAMP |  |  |  | Business/audit timestamp used for timeline, SLA, filtering, or history. |

**Primary Key:**
- `PRIMARY KEY (role_id)`

**Unique Constraints:**
- `UNIQUE KEY uq_roles_code (role_code)`

**Indexes:**
- `KEY idx_roles_status (status)`

**Check Constraints:**
- `CHECK (role_code IN ('ADMIN','HO','STAFF','DEPARTMENT','STUDENT','VISITOR'))`

### 5.2. `campuses`
**Purpose / Table Comment:** Danh mục campus

**Main Screens / UC Area:** Campus Management / Visit Request / Account Management

**Column Count:** `13`

**Columns:**

| Column | Type | Null | Default | Extra | Key / FK / Index | Enum Values | Notes / Meaning |
|---|---|---:|---|---|---|---|---|
| `campus_id` | `BIGINT UNSIGNED` | NO |  | AUTO_INCREMENT | PK |  | Identifier/reference field used to join or scope `campuses` records. |
| `campus_code` | `VARCHAR(20)` | NO |  |  | UNIQUE: uq_campuses_code |  | HN, HCM, DN, CT, QN |
| `name` | `VARCHAR(150)` | NO |  |  |  |  | Field `name` used by `campuses` business logic and screens. |
| `city` | `VARCHAR(100)` | YES |  |  | IDX: idx_campuses_city_status |  | Field `city` used by `campuses` business logic and screens. |
| `address` | `VARCHAR(255)` | YES |  |  |  |  | Field `address` used by `campuses` business logic and screens. |
| `phone` | `VARCHAR(30)` | YES |  |  |  |  | Field `phone` used by `campuses` business logic and screens. |
| `email` | `VARCHAR(150)` | YES |  |  |  |  | Field `email` used by `campuses` business logic and screens. |
| `ic_head_user_id` | `BIGINT UNSIGNED` | YES |  |  | IDX: idx_campuses_ic_head |  | FK added after users table |
| `status` | `ENUM('ACTIVE','INACTIVE')` | NO | 'ACTIVE' |  | IDX: idx_campuses_status; IDX: idx_campuses_city_status | ACTIVE, INACTIVE | Status field used for workflow state, filtering and UI badges. |
| `created_at` | `DATETIME` | NO | CURRENT_TIMESTAMP |  |  |  | Business/audit timestamp used for timeline, SLA, filtering, or history. |
| `created_by` | `BIGINT UNSIGNED` | YES |  |  |  |  | User reference used for audit and accountability. |
| `updated_at` | `DATETIME` | YES | NULL ON UPDATE CURRENT_TIMESTAMP | ON UPDATE CURRENT_TIMESTAMP |  |  | Business/audit timestamp used for timeline, SLA, filtering, or history. |
| `updated_by` | `BIGINT UNSIGNED` | YES |  |  |  |  | User reference used for audit and accountability. |

**Primary Key:**
- `PRIMARY KEY (campus_id)`

**Unique Constraints:**
- `UNIQUE KEY uq_campuses_code (campus_code)`

**Indexes:**
- `KEY idx_campuses_status (status)`
- `KEY idx_campuses_city_status (city, status)`
- `KEY idx_campuses_ic_head (ic_head_user_id)`

### 5.3. `departments`
**Purpose / Table Comment:** Phòng ban theo campus. STAFF thuộc IC, DEPARTMENT thuộc GENERAL

**Main Screens / UC Area:** Department Management / Account Management / Logistics

**Column Count:** `10`

**Columns:**

| Column | Type | Null | Default | Extra | Key / FK / Index | Enum Values | Notes / Meaning |
|---|---|---:|---|---|---|---|---|
| `department_id` | `BIGINT UNSIGNED` | NO |  | AUTO_INCREMENT | PK |  | Identifier/reference field used to join or scope `departments` records. |
| `campus_id` | `BIGINT UNSIGNED` | NO |  |  | UNIQUE: uq_departments_campus_name; IDX: idx_departments_campus_type; FK: campuses(campus_id) |  | Identifier/reference field used to join or scope `departments` records. |
| `name` | `VARCHAR(150)` | NO |  |  | UNIQUE: uq_departments_campus_name |  | Field `name` used by `departments` business logic and screens. |
| `department_type` | `ENUM('IC','GENERAL')` | NO |  |  | IDX: idx_departments_campus_type | IC, GENERAL | IC=International Cooperation; GENERAL=other departments |
| `head_user_id` | `BIGINT UNSIGNED` | YES |  |  | IDX: idx_departments_head |  | FK added after users table |
| `status` | `ENUM('ACTIVE','INACTIVE')` | NO | 'ACTIVE' |  | IDX: idx_departments_status | ACTIVE, INACTIVE | Status field used for workflow state, filtering and UI badges. |
| `created_at` | `DATETIME` | NO | CURRENT_TIMESTAMP |  |  |  | Business/audit timestamp used for timeline, SLA, filtering, or history. |
| `created_by` | `BIGINT UNSIGNED` | YES |  |  |  |  | User reference used for audit and accountability. |
| `updated_at` | `DATETIME` | YES | NULL ON UPDATE CURRENT_TIMESTAMP | ON UPDATE CURRENT_TIMESTAMP |  |  | Business/audit timestamp used for timeline, SLA, filtering, or history. |
| `updated_by` | `BIGINT UNSIGNED` | YES |  |  |  |  | User reference used for audit and accountability. |

**Primary Key:**
- `PRIMARY KEY (department_id)`

**Unique Constraints:**
- `UNIQUE KEY uq_departments_campus_name (campus_id, name)`

**Indexes:**
- `KEY idx_departments_campus_type (campus_id, department_type)`
- `KEY idx_departments_status (status)`
- `KEY idx_departments_head (head_user_id)`

**Foreign Keys:**
- `CONSTRAINT fk_departments_campus FOREIGN KEY (campus_id) REFERENCES campuses(campus_id) ON UPDATE CASCADE ON DELETE RESTRICT`

### 5.4. `users`
**Purpose / Table Comment:** Tài khoản chính. Production dùng SSO; LOCAL_PASSWORD chỉ dùng DEV/test.

**Main Screens / UC Area:** Login / Profile / Account Management / Visitor Portal

**Column Count:** `25`

**Columns:**

| Column | Type | Null | Default | Extra | Key / FK / Index | Enum Values | Notes / Meaning |
|---|---|---:|---|---|---|---|---|
| `user_id` | `BIGINT UNSIGNED` | NO |  | AUTO_INCREMENT | PK |  | Identifier/reference field used to join or scope `users` records. |
| `full_name` | `VARCHAR(150)` | NO |  |  |  |  | Field `full_name` used by `users` business logic and screens. |
| `email` | `VARCHAR(150)` | NO |  |  | UNIQUE: uq_users_email; IDX: idx_users_email_status |  | Field `email` used by `users` business logic and screens. |
| `phone` | `VARCHAR(30)` | YES |  |  |  |  | Field `phone` used by `users` business logic and screens. |
| `nationality` | `VARCHAR(100)` | YES |  |  | IDX: idx_users_nationality |  | Quốc tịch của user/visitor |
| `password_hash` | `VARCHAR(255)` | YES |  |  |  |  | DEV/local password hash only. Production SSO-only accounts keep this NULL. |
| `role_id` | `BIGINT UNSIGNED` | NO |  |  | IDX: idx_users_role_sub_role; IDX: idx_users_campus_role_status; FK: roles(role_id) |  | Identifier/reference field used to join or scope `users` records. |
| `sub_role` | `ENUM('LEADER','STAFF')` | YES |  |  | IDX: idx_users_role_sub_role | LEADER, STAFF | Only for STAFF/DEPARTMENT |
| `primary_campus_id` | `BIGINT UNSIGNED` | YES |  |  | IDX: idx_users_primary_campus; IDX: idx_users_campus_role_status; FK: campuses(campus_id) |  | Campus duy nhất của user nội bộ. VISITOR phải NULL. |
| `department_id` | `BIGINT UNSIGNED` | YES |  |  | IDX: idx_users_department; IDX: idx_users_department_status; FK: departments(department_id) |  | STAFF = IC department; DEPARTMENT = GENERAL department |
| `gender` | `ENUM('MALE','FEMALE','OTHER')` | YES |  |  |  | MALE, FEMALE, OTHER | NULL=chưa cung cấp; OTHER=khác Nam/Nữ |
| `avatar_url` | `VARCHAR(500)` | YES |  |  |  |  | Field `avatar_url` used by `users` business logic and screens. |
| `student_code` | `VARCHAR(30)` | YES |  |  | UNIQUE: uq_users_student_code |  | Field `student_code` used by `users` business logic and screens. |
| `fe_id` | `VARCHAR(100)` | YES |  |  | UNIQUE: uq_users_fe_id |  | Identifier/reference field used to join or scope `users` records. |
| `status` | `ENUM('ACTIVE','INACTIVE','LOCKED')` | NO | 'ACTIVE' |  | IDX: idx_users_status; IDX: idx_users_email_status; IDX: idx_users_campus_role_status; IDX: idx_users_department_status | ACTIVE, INACTIVE, LOCKED | ACTIVE=hoạt động, INACTIVE=tạm ngưng, LOCKED=bị khóa |
| `email_verified_at` | `DATETIME` | YES |  |  |  |  | Thời điểm email được xác thực qua SSO lần đầu hoặc xác nhận bởi hệ thống |
| `failed_login_count` | `INT UNSIGNED` | NO | 0 |  |  |  | Số lần đăng nhập sai local password liên tiếp; reset khi login thành công |
| `locked_until` | `DATETIME` | YES |  |  |  |  | Thời điểm hết khóa tạm thời nếu bị lock |
| `created_via` | `ENUM('MANUAL_CREATED','VISITOR_FORM','SSO_AUTO_PROVISION')` | NO | 'MANUAL_CREATED' |  | IDX: idx_users_created_via | MANUAL_CREATED, VISITOR_FORM, SSO_AUTO_PROVISION | MANUAL_CREATED=HO/Staff Leader tạo, VISITOR_FORM=tạo từ form visitor, SSO_AUTO_PROVISION=tạo tự động khi đăng nhập SSO ở cổng Visitor |
| `first_login_at` | `DATETIME` | YES |  |  |  |  | Business/audit timestamp used for timeline, SLA, filtering, or history. |
| `last_login_at` | `DATETIME` | YES |  |  | IDX: idx_users_last_login |  | Business/audit timestamp used for timeline, SLA, filtering, or history. |
| `created_at` | `DATETIME` | NO | CURRENT_TIMESTAMP |  |  |  | Business/audit timestamp used for timeline, SLA, filtering, or history. |
| `created_by` | `BIGINT UNSIGNED` | YES |  |  |  |  | User reference used for audit and accountability. |
| `updated_at` | `DATETIME` | YES | NULL ON UPDATE CURRENT_TIMESTAMP | ON UPDATE CURRENT_TIMESTAMP |  |  | Business/audit timestamp used for timeline, SLA, filtering, or history. |
| `updated_by` | `BIGINT UNSIGNED` | YES |  |  |  |  | User reference used for audit and accountability. |

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

### 5.5. `user_auth_providers`
**Purpose / Table Comment:** Provider đăng nhập của user. Production dùng GOOGLE_SSO/FEID; LOCAL_PASSWORD chỉ dùng DEV/test.

**Main Screens / UC Area:** Login / SSO / FEID / Local password DEV

**Column Count:** `8`

**Columns:**

| Column | Type | Null | Default | Extra | Key / FK / Index | Enum Values | Notes / Meaning |
|---|---|---:|---|---|---|---|---|
| `auth_provider_id` | `BIGINT UNSIGNED` | NO |  | AUTO_INCREMENT | PK |  | Identifier/reference field used to join or scope `user_auth_providers` records. |
| `user_id` | `BIGINT UNSIGNED` | NO |  |  | UNIQUE: uq_user_auth_provider_type; FK: users(user_id) |  | Identifier/reference field used to join or scope `user_auth_providers` records. |
| `provider_type` | `ENUM('LOCAL_PASSWORD','GOOGLE_SSO','FEID')` | NO |  |  | UNIQUE: uq_user_auth_provider_type; UNIQUE: uq_auth_provider_subject; IDX: idx_auth_provider_type_email_enabled | LOCAL_PASSWORD, GOOGLE_SSO, FEID | Field `provider_type` used by `user_auth_providers` business logic and screens. |
| `provider_subject` | `VARCHAR(255)` | YES |  |  | UNIQUE: uq_auth_provider_subject |  | Required for GOOGLE_SSO/FEID |
| `provider_email` | `VARCHAR(150)` | YES |  |  | IDX: idx_auth_provider_email; IDX: idx_auth_provider_type_email_enabled |  | Field `provider_email` used by `user_auth_providers` business logic and screens. |
| `is_enabled` | `BOOLEAN` | NO | TRUE |  | IDX: idx_auth_provider_type_email_enabled |  | Field `is_enabled` used by `user_auth_providers` business logic and screens. |
| `linked_at` | `DATETIME` | NO | CURRENT_TIMESTAMP |  |  |  | Business/audit timestamp used for timeline, SLA, filtering, or history. |
| `last_used_at` | `DATETIME` | YES |  |  |  |  | Business/audit timestamp used for timeline, SLA, filtering, or history. |

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

### 5.6. `user_sessions`
**Purpose / Table Comment:** Session + refresh token hash

**Main Screens / UC Area:** Login / Logout / Refresh Token / Session Security

**Column Count:** `15`

**Columns:**

| Column | Type | Null | Default | Extra | Key / FK / Index | Enum Values | Notes / Meaning |
|---|---|---:|---|---|---|---|---|
| `session_id` | `BIGINT UNSIGNED` | NO |  | AUTO_INCREMENT | PK |  | Identifier/reference field used to join or scope `user_sessions` records. |
| `user_id` | `BIGINT UNSIGNED` | NO |  |  | IDX: idx_sessions_user_active; FK: users(user_id) |  | Identifier/reference field used to join or scope `user_sessions` records. |
| `login_portal` | `ENUM('VISITOR','INTERNAL')` | NO |  |  | IDX: idx_sessions_portal_campus | VISITOR, INTERNAL | Field `login_portal` used by `user_sessions` business logic and screens. |
| `selected_campus_id` | `BIGINT UNSIGNED` | YES |  |  | IDX: idx_sessions_portal_campus; FK: campuses(campus_id) |  | Auto set to users.primary_campus_id for INTERNAL, NULL for VISITOR |
| `auth_provider_id` | `BIGINT UNSIGNED` | YES |  |  | FK: user_auth_providers(auth_provider_id) |  | Identifier/reference field used to join or scope `user_sessions` records. |
| `refresh_token_hash` | `VARCHAR(255)` | YES |  |  | UNIQUE: uq_sessions_refresh_hash; IDX: idx_sessions_refresh_active |  | Refresh token hash merged into session |
| `refresh_expires_at` | `DATETIME` | YES |  |  | IDX: idx_sessions_refresh_active |  | Business/audit timestamp used for timeline, SLA, filtering, or history. |
| `refresh_revoked_at` | `DATETIME` | YES |  |  | IDX: idx_sessions_refresh_active |  | Business/audit timestamp used for timeline, SLA, filtering, or history. |
| `ip_address` | `VARCHAR(45)` | YES |  |  | IDX: idx_sessions_ip_time |  | Field `ip_address` used by `user_sessions` business logic and screens. |
| `user_agent` | `VARCHAR(500)` | YES |  |  |  |  | Field `user_agent` used by `user_sessions` business logic and screens. |
| `created_at` | `DATETIME` | NO | CURRENT_TIMESTAMP |  | IDX: idx_sessions_ip_time |  | Business/audit timestamp used for timeline, SLA, filtering, or history. |
| `expires_at` | `DATETIME` | NO |  |  | IDX: idx_sessions_user_active; IDX: idx_sessions_expires_at |  | Business/audit timestamp used for timeline, SLA, filtering, or history. |
| `revoked_at` | `DATETIME` | YES |  |  | IDX: idx_sessions_user_active; IDX: idx_sessions_revoked_at |  | Business/audit timestamp used for timeline, SLA, filtering, or history. |
| `revoked_by` | `BIGINT UNSIGNED` | YES |  |  | FK: users(user_id) |  | User reference used for audit and accountability. |
| `revoked_reason` | `VARCHAR(255)` | YES |  |  |  |  | Business note/reason used for explanation and audit. |

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

### 5.7. `otp_tokens`
**Purpose / Table Comment:** OTP, magic link, set password token, reset password token

**Main Screens / UC Area:** OTP Verification / Submit Visit Request / Forgot Password

**Column Count:** `14`

**Columns:**

| Column | Type | Null | Default | Extra | Key / FK / Index | Enum Values | Notes / Meaning |
|---|---|---:|---|---|---|---|---|
| `otp_token_id` | `BIGINT UNSIGNED` | NO |  | AUTO_INCREMENT | PK |  | Identifier/reference field used to join or scope `otp_tokens` records. |
| `user_id` | `BIGINT UNSIGNED` | YES |  |  | IDX: idx_otp_user_purpose_active; FK: users(user_id) |  | Identifier/reference field used to join or scope `otp_tokens` records. |
| `email` | `VARCHAR(150)` | NO |  |  | IDX: idx_otp_email_purpose_time; IDX: idx_otp_email_purpose_active |  | Field `email` used by `otp_tokens` business logic and screens. |
| `token_type` | `ENUM('OTP_CODE','MAGIC_LINK')` | NO | 'OTP_CODE' |  |  | OTP_CODE, MAGIC_LINK | Field `token_type` used by `otp_tokens` business logic and screens. |
| `purpose` | `ENUM('VISIT_REQUEST_VERIFY','CHANGE_SENSITIVE_ACTION')` | NO |  |  | IDX: idx_otp_email_purpose_time; IDX: idx_otp_email_purpose_active; IDX: idx_otp_user_purpose_active | VISIT_REQUEST_VERIFY, CHANGE_SENSITIVE_ACTION | Field `purpose` used by `otp_tokens` business logic and screens. |
| `token_hash` | `VARCHAR(255)` | NO |  |  | UNIQUE: uq_otp_tokens_hash |  | Field `token_hash` used by `otp_tokens` business logic and screens. |
| `expires_at` | `DATETIME` | NO |  |  | IDX: idx_otp_email_purpose_active; IDX: idx_otp_user_purpose_active |  | Business/audit timestamp used for timeline, SLA, filtering, or history. |
| `used_at` | `DATETIME` | YES |  |  | IDX: idx_otp_email_purpose_active; IDX: idx_otp_user_purpose_active |  | Business/audit timestamp used for timeline, SLA, filtering, or history. |
| `attempt_count` | `INT UNSIGNED` | NO | 0 |  |  |  | Field `attempt_count` used by `otp_tokens` business logic and screens. |
| `max_attempts` | `INT UNSIGNED` | NO | 5 |  |  |  | Field `max_attempts` used by `otp_tokens` business logic and screens. |
| `resend_count` | `INT UNSIGNED` | NO | 0 |  |  |  | Field `resend_count` used by `otp_tokens` business logic and screens. |
| `ip_address` | `VARCHAR(45)` | YES |  |  | IDX: idx_otp_ip_time |  | Field `ip_address` used by `otp_tokens` business logic and screens. |
| `user_agent` | `VARCHAR(500)` | YES |  |  |  |  | Field `user_agent` used by `otp_tokens` business logic and screens. |
| `created_at` | `DATETIME` | NO | CURRENT_TIMESTAMP |  | IDX: idx_otp_email_purpose_time; IDX: idx_otp_ip_time |  | Business/audit timestamp used for timeline, SLA, filtering, or history. |

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

### 5.8. `login_logs`
**Purpose / Table Comment:** Lịch sử đăng nhập

**Main Screens / UC Area:** Security audit / Login monitoring

**Column Count:** `12`

**Columns:**

| Column | Type | Null | Default | Extra | Key / FK / Index | Enum Values | Notes / Meaning |
|---|---|---:|---|---|---|---|---|
| `login_log_id` | `BIGINT UNSIGNED` | NO |  | AUTO_INCREMENT | PK |  | Identifier/reference field used to join or scope `login_logs` records. |
| `user_id` | `BIGINT UNSIGNED` | YES |  |  | IDX: idx_login_logs_user_time; FK: users(user_id) |  | Identifier/reference field used to join or scope `login_logs` records. |
| `email` | `VARCHAR(150)` | NO |  |  | IDX: idx_login_logs_email_status_time |  | Field `email` used by `login_logs` business logic and screens. |
| `login_portal` | `ENUM('VISITOR','INTERNAL')` | NO |  |  | IDX: idx_login_logs_portal_campus | VISITOR, INTERNAL | Field `login_portal` used by `login_logs` business logic and screens. |
| `selected_campus_id` | `BIGINT UNSIGNED` | YES |  |  | IDX: idx_login_logs_portal_campus; FK: campuses(campus_id) |  | Identifier/reference field used to join or scope `login_logs` records. |
| `provider_type` | `ENUM('LOCAL_PASSWORD','GOOGLE_SSO','FEID')` | YES |  |  | IDX: idx_login_logs_provider_time | LOCAL_PASSWORD, GOOGLE_SSO, FEID | Field `provider_type` used by `login_logs` business logic and screens. |
| `status` | `ENUM('SUCCESS','FAILED','BLOCKED')` | NO |  |  | IDX: idx_login_logs_email_status_time; IDX: idx_login_logs_ip_status_time | SUCCESS, FAILED, BLOCKED | Status field used for workflow state, filtering and UI badges. |
| `failure_reason` | `VARCHAR(255)` | YES |  |  |  |  | Business note/reason used for explanation and audit. |
| `ip_address` | `VARCHAR(45)` | YES |  |  | IDX: idx_login_logs_ip_status_time |  | Field `ip_address` used by `login_logs` business logic and screens. |
| `user_agent` | `VARCHAR(500)` | YES |  |  |  |  | Field `user_agent` used by `login_logs` business logic and screens. |
| `session_id` | `BIGINT UNSIGNED` | YES |  |  |  |  | Identifier/reference field used to join or scope `login_logs` records. |
| `created_at` | `DATETIME` | NO | CURRENT_TIMESTAMP |  | IDX: idx_login_logs_user_time; IDX: idx_login_logs_email_status_time; IDX: idx_login_logs_ip_status_time; IDX: idx_login_logs_provider_time |  | Business/audit timestamp used for timeline, SLA, filtering, or history. |

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

### 5.9. `security_events`
**Purpose / Table Comment:** SSO-only security events: portal/campus validation, Visitor auto-provisioning, and session lifecycle. No local password tracking and no metadata JSON.

**Main Screens / UC Area:** Security audit / Auth hardening

**Column Count:** `15`

**Columns:**

| Column | Type | Null | Default | Extra | Key / FK / Index | Enum Values | Notes / Meaning |
|---|---|---:|---|---|---|---|---|
| `security_event_id` | `BIGINT UNSIGNED` | NO |  | AUTO_INCREMENT | PK |  | Identifier/reference field used to join or scope `security_events` records. |
| `user_id` | `BIGINT UNSIGNED` | YES |  |  | IDX: idx_security_user_time; FK: users(user_id) |  | Identifier/reference field used to join or scope `security_events` records. |
| `email_snapshot` | `VARCHAR(150)` | YES |  |  | IDX: idx_security_email_time |  | Email nhận từ SSO hoặc email đang được kiểm tra tại thời điểm xảy ra sự kiện |
| `event_type` | `ENUM(<br>    'SSO_LOGIN',<br>    'PORTAL_VALIDATION',<br>    'CAMPUS_VALIDATION',<br>    'VISITOR_AUTO_PROVISION',<br>    'SESSION_CREATED',<br>    'SESSION_REVOKED',<br>    'SESSION_EXPIRED',<br>    'TOKEN_REFRESH',<br>    'SECURITY_POLICY_CHECK'<br>  )` | NO |  |  | IDX: idx_security_type_result_time | SSO_LOGIN, PORTAL_VALIDATION, CAMPUS_VALIDATION, VISITOR_AUTO_PROVISION, SESSION_CREATED, SESSION_REVOKED, SESSION_EXPIRED, TOKEN_REFRESH, SECURITY_POLICY_CHECK | Loại sự kiện bảo mật theo mô hình SSO-only |
| `result` | `ENUM('SUCCESS','FAILED','BLOCKED')` | NO | 'SUCCESS' |  | IDX: idx_security_type_result_time | SUCCESS, FAILED, BLOCKED | Kết quả xử lý sự kiện |
| `failure_reason_code` | `ENUM(<br>    'ACCOUNT_NOT_FOUND',<br>    'ACCOUNT_DISABLED',<br>    'PORTAL_MISMATCH',<br>    'CAMPUS_MISMATCH',<br>    'ROLE_MISMATCH',<br>    'SSO_PROVIDER_ERROR',<br>    'INVALID_SSO_CLAIMS',<br>    'VISITOR_AUTO_PROVISION_DISABLED',<br>    'SESSION_EXPIRED',<br>    'TOKEN_REVOKED',<br>    'SUSPICIOUS_IP',<br>    'UNKNOWN'<br>  )` | YES |  |  | IDX: idx_security_failure_reason_time | ACCOUNT_NOT_FOUND, ACCOUNT_DISABLED, PORTAL_MISMATCH, CAMPUS_MISMATCH, ROLE_MISMATCH, SSO_PROVIDER_ERROR, INVALID_SSO_CLAIMS, VISITOR_AUTO_PROVISION_DISABLED, SESSION_EXPIRED, TOKEN_REVOKED, SUSPICIOUS_IP, UNKNOWN | Mã lý do thất bại/chặn; NULL khi SUCCESS |
| `severity` | `ENUM('LOW','MEDIUM','HIGH','CRITICAL')` | NO | 'LOW' |  | IDX: idx_security_severity_time | LOW, MEDIUM, HIGH, CRITICAL | Field `severity` used by `security_events` business logic and screens. |
| `login_portal` | `ENUM('VISITOR','INTERNAL')` | YES |  |  | IDX: idx_security_portal_campus_time | VISITOR, INTERNAL | Portal được dùng khi phát sinh sự kiện |
| `selected_campus_id` | `BIGINT UNSIGNED` | YES |  |  | IDX: idx_security_portal_campus_time; FK: campuses(campus_id) |  | Campus người dùng chọn ở Internal Portal; NULL với Visitor Portal |
| `provider_type` | `ENUM('GOOGLE_SSO','FEID')` | YES |  |  |  | GOOGLE_SSO, FEID | Nguồn định danh SSO; không dùng LOCAL_PASSWORD trong security_events |
| `ip_address` | `VARCHAR(45)` | YES |  |  | IDX: idx_security_ip_time |  | Field `ip_address` used by `security_events` business logic and screens. |
| `user_agent` | `VARCHAR(500)` | YES |  |  |  |  | Field `user_agent` used by `security_events` business logic and screens. |
| `session_id` | `BIGINT UNSIGNED` | YES |  |  | IDX: idx_security_session_time; FK: user_sessions(session_id) |  | Identifier/reference field used to join or scope `security_events` records. |
| `detail_text` | `TEXT` | YES |  |  |  |  | Ghi chú debug ngắn, không lưu JSON metadata |
| `created_at` | `DATETIME` | NO | CURRENT_TIMESTAMP |  | IDX: idx_security_user_time; IDX: idx_security_email_time; IDX: idx_security_type_result_time; IDX: idx_security_portal_campus_time; IDX: idx_security_failure_reason_time; IDX: idx_security_ip_time; IDX: idx_security_severity_time; IDX: idx_security_session_time |  | Business/audit timestamp used for timeline, SLA, filtering, or history. |

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

### 5.10. `files`
**Purpose / Table Comment:** File metadata only. Binary file is stored outside DB.

**Main Screens / UC Area:** File metadata / Documents / Gallery / News / Partner / Minutes

**Column Count:** `15`

**Columns:**

| Column | Type | Null | Default | Extra | Key / FK / Index | Enum Values | Notes / Meaning |
|---|---|---:|---|---|---|---|---|
| `file_id` | `BIGINT UNSIGNED` | NO |  | AUTO_INCREMENT | PK |  | Identifier/reference field used to join or scope `files` records. |
| `storage_provider` | `ENUM('LOCAL','S3','AZURE','GCS','GOOGLE_DRIVE','OTHER')` | NO | 'LOCAL' |  |  | LOCAL, S3, AZURE, GCS, GOOGLE_DRIVE, OTHER | Field `storage_provider` used by `files` business logic and screens. |
| `bucket_name` | `VARCHAR(150)` | YES |  |  |  |  | Field `bucket_name` used by `files` business logic and screens. |
| `object_key` | `VARCHAR(700)` | NO |  |  | UNIQUE: uq_files_object_key |  | Max 700 chars to keep UNIQUE index safe under utf8mb4 |
| `original_filename` | `VARCHAR(255)` | NO |  |  |  |  | Field `original_filename` used by `files` business logic and screens. |
| `mime_type` | `VARCHAR(150)` | YES |  |  | IDX: idx_files_mime_time |  | Field `mime_type` used by `files` business logic and screens. |
| `file_size` | `BIGINT UNSIGNED` | YES |  |  |  |  | Field `file_size` used by `files` business logic and screens. |
| `checksum_sha256` | `CHAR(64)` | YES |  |  | IDX: idx_files_checksum |  | Field `checksum_sha256` used by `files` business logic and screens. |
| `uploaded_by` | `BIGINT UNSIGNED` | YES |  |  | IDX: idx_files_uploaded_by; FK: users(user_id) |  | User reference used for audit and accountability. |
| `uploaded_at` | `DATETIME` | NO | CURRENT_TIMESTAMP |  | IDX: idx_files_uploaded_by; IDX: idx_files_mime_time; IDX: idx_files_purpose_time |  | Business/audit timestamp used for timeline, SLA, filtering, or history. |
| `external_file_id` | `VARCHAR(255)` | YES |  |  | IDX: idx_files_external_file_id |  | External provider file id, e.g., Google Drive file id |
| `web_view_url` | `VARCHAR(700)` | YES |  |  |  |  | Open/view URL from external storage provider |
| `download_url` | `VARCHAR(700)` | YES |  |  |  |  | Direct download URL when provider allows it |
| `thumbnail_url` | `VARCHAR(700)` | YES |  |  |  |  | Thumbnail URL for image/video preview |
| `file_purpose` | `VARCHAR(100)` | YES |  |  | IDX: idx_files_purpose_time |  | Technical/business file purpose used by referencing entity |

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

### 5.11. `partners`
**Purpose / Table Comment:** Hồ sơ đối tác; owner_campus_id dùng để Staff Leader duyệt đúng campus

**Main Screens / UC Area:** Partner Management / Public Partners / Staff Leader Approval Scope

**Column Count:** `24`

**Columns:**

| Column | Type | Null | Default | Extra | Key / FK / Index | Enum Values | Notes / Meaning |
|---|---|---:|---|---|---|---|---|
| `partner_id` | `BIGINT UNSIGNED` | NO |  | AUTO_INCREMENT | PK |  | Identifier/reference field used to join or scope `partners` records. |
| `owner_campus_id` | `BIGINT UNSIGNED` | NO |  |  | IDX: idx_partners_owner_status; IDX: idx_partners_owner_created; FK: campuses(campus_id) |  | Campus sở hữu/quản lý partner request; dùng để Staff Leader duyệt đúng campus |
| `partner_code` | `VARCHAR(50)` | YES |  |  | UNIQUE: uq_partners_code |  | Field `partner_code` used by `partners` business logic and screens. |
| `name` | `VARCHAR(200)` | NO |  |  | IDX: ft_partners_search |  | Field `name` used by `partners` business logic and screens. |
| `short_name` | `VARCHAR(100)` | YES |  |  | IDX: ft_partners_search |  | Field `short_name` used by `partners` business logic and screens. |
| `country` | `VARCHAR(100)` | YES |  |  | IDX: idx_partners_country |  | Field `country` used by `partners` business logic and screens. |
| `city` | `VARCHAR(100)` | YES |  |  |  |  | Field `city` used by `partners` business logic and screens. |
| `website_url` | `VARCHAR(500)` | YES |  |  |  |  | Field `website_url` used by `partners` business logic and screens. |
| `partner_type` | `ENUM('UNIVERSITY','COMPANY','GOVERNMENT','NGO','OTHER')` | NO | 'UNIVERSITY' |  | IDX: idx_partners_type_status | UNIVERSITY, COMPANY, GOVERNMENT, NGO, OTHER | Field `partner_type` used by `partners` business logic and screens. |
| `cooperation_status` | `ENUM('POTENTIAL','ACTIVE','INACTIVE','BLACKLISTED')` | NO | 'POTENTIAL' |  | IDX: idx_partners_status; IDX: idx_partners_type_status | POTENTIAL, ACTIVE, INACTIVE, BLACKLISTED | Status field used for workflow state, filtering and UI badges. |
| `description` | `TEXT` | YES |  |  | IDX: ft_partners_search |  | Field `description` used by `partners` business logic and screens. |
| `logo_file_id` | `BIGINT UNSIGNED` | YES |  |  | IDX: idx_partners_logo_file; FK: files(file_id) |  | Partner logo file, references files.file_id |
| `cover_file_id` | `BIGINT UNSIGNED` | YES |  |  | IDX: idx_partners_cover_file; FK: files(file_id) |  | Partner cover/banner file, references files.file_id |
| `address` | `VARCHAR(500)` | YES |  |  |  |  | Field `address` used by `partners` business logic and screens. |
| `public_slug` | `VARCHAR(180)` | YES |  |  | UNIQUE: uq_partners_public_slug |  | Public URL slug for partner profile |
| `profile_status` | `ENUM('DRAFT','PENDING_APPROVAL','APPROVED','REJECTED')` | NO | 'APPROVED' |  | IDX: idx_partners_owner_status; IDX: idx_partners_profile_status | DRAFT, PENDING_APPROVAL, APPROVED, REJECTED | Status field used for workflow state, filtering and UI badges. |
| `review_note` | `TEXT` | YES |  |  |  |  | Business note/reason used for explanation and audit. |
| `reviewed_by` | `BIGINT UNSIGNED` | YES |  |  | IDX: idx_partners_reviewed_by; FK: users(user_id) |  | User reference used for audit and accountability. |
| `reviewed_at` | `DATETIME` | YES |  |  | IDX: idx_partners_reviewed_by |  | Business/audit timestamp used for timeline, SLA, filtering, or history. |
| `visibility` | `ENUM('PRIVATE','INTERNAL','PUBLIC')` | NO | 'PUBLIC' |  | IDX: idx_partners_visibility | PRIVATE, INTERNAL, PUBLIC | Field `visibility` used by `partners` business logic and screens. |
| `created_at` | `DATETIME` | NO | CURRENT_TIMESTAMP |  | IDX: idx_partners_owner_created; IDX: idx_partners_created_at |  | Business/audit timestamp used for timeline, SLA, filtering, or history. |
| `created_by` | `BIGINT UNSIGNED` | YES |  |  |  |  | User reference used for audit and accountability. |
| `updated_at` | `DATETIME` | YES | NULL ON UPDATE CURRENT_TIMESTAMP | ON UPDATE CURRENT_TIMESTAMP |  |  | Business/audit timestamp used for timeline, SLA, filtering, or history. |
| `updated_by` | `BIGINT UNSIGNED` | YES |  |  |  |  | User reference used for audit and accountability. |

**Primary Key:**
- `PRIMARY KEY (partner_id)`

**Unique Constraints:**
- `UNIQUE KEY uq_partners_code (partner_code)`
- `UNIQUE KEY uq_partners_public_slug (public_slug)`

**Indexes:**
- `KEY idx_partners_owner_status (owner_campus_id, profile_status)`
- `KEY idx_partners_owner_created (owner_campus_id, created_at)`
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
- `CONSTRAINT fk_partners_owner_campus FOREIGN KEY (owner_campus_id) REFERENCES campuses(campus_id) ON UPDATE CASCADE ON DELETE RESTRICT`
- `CONSTRAINT fk_partners_logo_file FOREIGN KEY (logo_file_id) REFERENCES files(file_id) ON UPDATE CASCADE ON DELETE SET NULL`
- `CONSTRAINT fk_partners_cover_file FOREIGN KEY (cover_file_id) REFERENCES files(file_id) ON UPDATE CASCADE ON DELETE SET NULL`
- `CONSTRAINT fk_partners_reviewed_by FOREIGN KEY (reviewed_by) REFERENCES users(user_id) ON UPDATE CASCADE ON DELETE SET NULL`

**V10 Usage Notes:**
- Staff/IC creates partner profiles under `owner_campus_id`. Staff Leader may approve/reject only partners where `owner_campus_id = currentUser.primary_campus_id`. `profile_status` stores approval state; no separate review-history table exists in v10.

### 5.12. `partner_contacts`
**Purpose / Table Comment:** Người liên hệ đối tác. OCR final confirmed data saved here.

**Main Screens / UC Area:** Partner Contact / Scan Business Card

**Column Count:** `17`

**Columns:**

| Column | Type | Null | Default | Extra | Key / FK / Index | Enum Values | Notes / Meaning |
|---|---|---:|---|---|---|---|---|
| `contact_id` | `BIGINT UNSIGNED` | NO |  | AUTO_INCREMENT | PK |  | Identifier/reference field used to join or scope `partner_contacts` records. |
| `partner_id` | `BIGINT UNSIGNED` | NO |  |  | UNIQUE: uq_partner_contacts_partner_email; IDX: idx_partner_contacts_partner; FK: partners(partner_id) |  | Identifier/reference field used to join or scope `partner_contacts` records. |
| `full_name` | `VARCHAR(150)` | NO |  |  |  |  | Field `full_name` used by `partner_contacts` business logic and screens. |
| `email` | `VARCHAR(150)` | YES |  |  | UNIQUE: uq_partner_contacts_partner_email; IDX: idx_partner_contacts_email |  | Field `email` used by `partner_contacts` business logic and screens. |
| `phone` | `VARCHAR(50)` | YES |  |  |  |  | Field `phone` used by `partner_contacts` business logic and screens. |
| `job_title` | `VARCHAR(150)` | YES |  |  |  |  | Field `job_title` used by `partner_contacts` business logic and screens. |
| `department_name` | `VARCHAR(150)` | YES |  |  |  |  | Field `department_name` used by `partner_contacts` business logic and screens. |
| `source_type` | `ENUM('MANUAL','BUSINESS_CARD_OCR','IMPORT')` | NO | 'MANUAL' |  | IDX: idx_partner_contacts_source_type | MANUAL, BUSINESS_CARD_OCR, IMPORT | Field `source_type` used by `partner_contacts` business logic and screens. |
| `scanned_card_file_id` | `BIGINT UNSIGNED` | YES |  |  | IDX: idx_partner_contacts_scanned_card; FK: files(file_id) |  | Identifier/reference field used to join or scope `partner_contacts` records. |
| `ocr_confidence` | `DECIMAL(5,2)` | YES |  |  |  |  | Field `ocr_confidence` used by `partner_contacts` business logic and screens. |
| `note` | `TEXT` | YES |  |  |  |  | Business note/reason used for explanation and audit. |
| `is_primary` | `BOOLEAN` | NO | FALSE |  |  |  | Field `is_primary` used by `partner_contacts` business logic and screens. |
| `status` | `ENUM('ACTIVE','INACTIVE')` | NO | 'ACTIVE' |  | IDX: idx_partner_contacts_status | ACTIVE, INACTIVE | Status field used for workflow state, filtering and UI badges. |
| `created_at` | `DATETIME` | NO | CURRENT_TIMESTAMP |  |  |  | Business/audit timestamp used for timeline, SLA, filtering, or history. |
| `created_by` | `BIGINT UNSIGNED` | YES |  |  |  |  | User reference used for audit and accountability. |
| `updated_at` | `DATETIME` | YES | NULL ON UPDATE CURRENT_TIMESTAMP | ON UPDATE CURRENT_TIMESTAMP |  |  | Business/audit timestamp used for timeline, SLA, filtering, or history. |
| `updated_by` | `BIGINT UNSIGNED` | YES |  |  |  |  | User reference used for audit and accountability. |

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

### 5.13. `documents`
**Purpose / Table Comment:** Tài liệu nghiệp vụ. partner_documents/reports/logistics documents merged by owner_type.

**Main Screens / UC Area:** Document Management / Archive

**Column Count:** `13`

**Columns:**

| Column | Type | Null | Default | Extra | Key / FK / Index | Enum Values | Notes / Meaning |
|---|---|---:|---|---|---|---|---|
| `document_id` | `BIGINT UNSIGNED` | NO |  | AUTO_INCREMENT | PK |  | Identifier/reference field used to join or scope `documents` records. |
| `file_id` | `BIGINT UNSIGNED` | NO |  |  | FK: files(file_id) |  | Identifier/reference field used to join or scope `documents` records. |
| `owner_type` | `ENUM('GENERAL','VISIT','PARTNER','MINUTES','NEWS','LOGISTICS','REPORT')` | NO | 'GENERAL' |  | IDX: idx_documents_owner | GENERAL, VISIT, PARTNER, MINUTES, NEWS, LOGISTICS, REPORT | Field `owner_type` used by `documents` business logic and screens. |
| `owner_id` | `BIGINT UNSIGNED` | YES |  |  | IDX: idx_documents_owner |  | Identifier/reference field used to join or scope `documents` records. |
| `campus_id` | `BIGINT UNSIGNED` | YES |  |  | IDX: idx_documents_campus_status; FK: campuses(campus_id) |  | Identifier/reference field used to join or scope `documents` records. |
| `title` | `VARCHAR(255)` | NO |  |  | IDX: ft_documents_search |  | Field `title` used by `documents` business logic and screens. |
| `description` | `TEXT` | YES |  |  | IDX: ft_documents_search |  | Field `description` used by `documents` business logic and screens. |
| `document_category` | `VARCHAR(100)` | YES |  |  | IDX: idx_documents_category_status |  | Field `document_category` used by `documents` business logic and screens. |
| `status` | `ENUM('DRAFT','PUBLISHED','ARCHIVED')` | NO | 'DRAFT' |  | IDX: idx_documents_campus_status; IDX: idx_documents_category_status | DRAFT, PUBLISHED, ARCHIVED | Status field used for workflow state, filtering and UI badges. |
| `created_at` | `DATETIME` | NO | CURRENT_TIMESTAMP |  | IDX: idx_documents_created_by_time |  | Business/audit timestamp used for timeline, SLA, filtering, or history. |
| `created_by` | `BIGINT UNSIGNED` | YES |  |  | IDX: idx_documents_created_by_time; FK: users(user_id) |  | User reference used for audit and accountability. |
| `updated_at` | `DATETIME` | YES | NULL ON UPDATE CURRENT_TIMESTAMP | ON UPDATE CURRENT_TIMESTAMP |  |  | Business/audit timestamp used for timeline, SLA, filtering, or history. |
| `updated_by` | `BIGINT UNSIGNED` | YES |  |  |  |  | User reference used for audit and accountability. |

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

### 5.14. `visit_requests`
**Purpose / Table Comment:** Đơn đăng ký tham quan. Record chỉ được tạo sau khi email/OTP đã xác minh; bảng tổng chỉ cho Visitor tự hủy toàn bộ đơn; tiến trình thực tế theo visit_request_campuses.

**Main Screens / UC Area:** Public Submit Visit Request / Delegation Management / Approval / Cancellation

**Column Count:** `40`

**Columns:**

| Column | Type | Null | Default | Extra | Key / FK / Index | Enum Values | Notes / Meaning |
|---|---|---:|---|---|---|---|---|
| `visit_request_id` | `BIGINT UNSIGNED` | NO |  | AUTO_INCREMENT | PK |  | Identifier/reference field used to join or scope `visit_requests` records. |
| `request_code` | `VARCHAR(50)` | NO |  |  | UNIQUE: uq_visit_requests_code; IDX: ft_visit_requests_frontend_search |  | Field `request_code` used by `visit_requests` business logic and screens. |
| `visitor_user_id` | `BIGINT UNSIGNED` | YES |  |  | IDX: idx_visit_requests_visitor; FK: users(user_id) |  | Visitor user/account created or linked for the registrant |
| `partner_id` | `BIGINT UNSIGNED` | YES |  |  | IDX: idx_visit_requests_partner; FK: partners(partner_id) |  | Identifier/reference field used to join or scope `visit_requests` records. |
| `created_source` | `ENUM('VISITOR_SUBMITTED','STAFF_CREATED')` | NO | 'VISITOR_SUBMITTED' |  | IDX: idx_visit_requests_created_source | VISITOR_SUBMITTED, STAFF_CREATED | Field `created_source` used by `visit_requests` business logic and screens. |
| `registrant_organization` | `VARCHAR(200)` | NO |  |  | IDX: ft_visit_requests_frontend_search |  | Đơn vị công tác người đăng ký |
| `registrant_job_title` | `VARCHAR(150)` | NO |  |  |  |  | Chức danh/phòng ban người đăng ký |
| `registrant_phone` | `VARCHAR(50)` | NO |  |  |  |  | SĐT người đăng ký |
| `registrant_email` | `VARCHAR(150)` | NO |  |  | IDX: idx_visit_requests_registrant_email; IDX: ft_visit_requests_frontend_search |  | Email người đăng ký |
| `registrant_nationality` | `VARCHAR(100)` | NO |  |  |  |  | Quốc tịch người đăng ký |
| `visit_scope` | `ENUM('SINGLE_CAMPUS','MULTI_CAMPUS')` | NO | 'SINGLE_CAMPUS' |  | IDX: idx_visit_requests_scope_status; IDX: idx_visit_requests_visibility_scope_status_decision | SINGLE_CAMPUS, MULTI_CAMPUS | SINGLE_CAMPUS: Staff Leader duyệt request tổng; MULTI_CAMPUS: HO duyệt request tổng. Frontend/backend suy ra người duyệt từ cột này. |
| `visit_type` | `ENUM('CAMPUS_TOUR','MEETING','WORKSHOP','SIGNING_CEREMONY','EXCHANGE','OTHER')` | NO | 'CAMPUS_TOUR' |  | IDX: idx_visit_requests_visit_type | CAMPUS_TOUR, MEETING, WORKSHOP, SIGNING_CEREMONY, EXCHANGE, OTHER | Field `visit_type` used by `visit_requests` business logic and screens. |
| `visit_type_other` | `VARCHAR(255)` | YES |  |  |  |  | Field `visit_type_other` used by `visit_requests` business logic and screens. |
| `purpose` | `TEXT` | NO |  |  |  |  | Mục đích thăm FPTU |
| `working_content` | `TEXT` | YES |  |  |  |  | Nội dung làm việc tại FPTU |
| `contact_person_full_name` | `VARCHAR(150)` | NO |  |  | IDX: ft_visit_requests_frontend_search |  | Field `contact_person_full_name` used by `visit_requests` business logic and screens. |
| `contact_person_organization` | `VARCHAR(255)` | NO |  |  | IDX: ft_visit_requests_frontend_search |  | Field `contact_person_organization` used by `visit_requests` business logic and screens. |
| `contact_person_phone` | `VARCHAR(50)` | NO |  |  |  |  | Field `contact_person_phone` used by `visit_requests` business logic and screens. |
| `contact_person_email` | `VARCHAR(150)` | NO |  |  | IDX: idx_visit_requests_contact_email; IDX: ft_visit_requests_frontend_search |  | Field `contact_person_email` used by `visit_requests` business logic and screens. |
| `working_language` | `ENUM('VI','EN')` | NO | 'EN' |  |  | VI, EN | Ngôn ngữ sử dụng trong visit. Chỉ dùng VI/EN theo frontend hiện tại, không có lựa chọn OTHER |
| `transportation_note` | `ENUM('SELF_ARRANGED','FPTU_SUPPORT','UNKNOWN','OTHER')` | NO | 'UNKNOWN' |  |  | SELF_ARRANGED, FPTU_SUPPORT, UNKNOWN, OTHER | Field `transportation_note` used by `visit_requests` business logic and screens. |
| `transportation_note` | `VARCHAR(500)` | YES |  |  |  |  | Field `transportation_note` used by `visit_requests` business logic and screens. |
| `media_consent_status` | `ENUM('AGREED','DECLINED')` | NO | 'DECLINED' |  | IDX: idx_visit_requests_media_consent | AGREED, DECLINED | Status field used for workflow state, filtering and UI badges. |
| `media_consent_note` | `TEXT` | YES |  |  |  |  | Business note/reason used for explanation and audit. |
| `note_to_fptu` | `TEXT` | YES |  |  |  |  | Ghi chú cho FPTU |
| `status` | `ENUM('PENDING_APPROVAL','APPROVED','REJECTED','CANCELLED')` | NO | 'PENDING_APPROVAL' |  | IDX: idx_visit_requests_status_submitted; IDX: idx_visit_requests_scope_status; IDX: idx_visit_requests_visibility_scope_status_decision | PENDING_APPROVAL, APPROVED, REJECTED, CANCELLED | Request decision status only. Visit progress is derived from visit_request_campuses.status |
| `submitted_at` | `DATETIME` | NO | CURRENT_TIMESTAMP |  | IDX: idx_visit_requests_status_submitted |  | Business/audit timestamp used for timeline, SLA, filtering, or history. |
| `email_verified_at` | `DATETIME` | YES |  |  |  |  | Business/audit timestamp used for timeline, SLA, filtering, or history. |
| `decided_by` | `BIGINT UNSIGNED` | YES |  |  | IDX: idx_visit_requests_decision; FK: users(user_id) |  | Người approve/reject request tổng |
| `decided_at` | `DATETIME` | YES |  |  | IDX: idx_visit_requests_visibility_scope_status_decision; IDX: idx_visit_requests_decision; IDX: idx_visit_requests_decision_role |  | Thời điểm xử lý request tổng |
| `decision_actor_role` | `ENUM('HO','STAFF_LEADER')` | YES |  |  | IDX: idx_visit_requests_visibility_scope_status_decision; IDX: idx_visit_requests_decision_role | HO, STAFF_LEADER | Vai trò người xử lý tại thời điểm quyết định |
| `decision_note` | `TEXT` | YES |  |  |  |  | Lý do/ghi chú khi approve hoặc reject |
| `cancelled_by` | `BIGINT UNSIGNED` | YES |  |  | IDX: idx_visit_requests_cancelled; FK: users(user_id) |  | Visitor hủy toàn bộ request/delegation |
| `cancelled_at` | `DATETIME` | YES |  |  | IDX: idx_visit_requests_cancelled |  | Thời điểm visitor hủy toàn bộ request/delegation |
| `cancellation_reason` | `TEXT` | YES |  |  |  |  | Lý do visitor nhập khi tự hủy toàn bộ request/delegation. Bảng tổng không lưu actor/source vì chỉ Visitor được hủy tổng. |
| `row_version` | `INT UNSIGNED` | NO | 0 |  |  |  | Optimistic concurrency token |
| `created_at` | `DATETIME` | NO | CURRENT_TIMESTAMP |  |  |  | Business/audit timestamp used for timeline, SLA, filtering, or history. |
| `created_by` | `BIGINT UNSIGNED` | YES |  |  |  |  | User reference used for audit and accountability. |
| `updated_at` | `DATETIME` | YES | NULL ON UPDATE CURRENT_TIMESTAMP | ON UPDATE CURRENT_TIMESTAMP |  |  | Business/audit timestamp used for timeline, SLA, filtering, or history. |
| `updated_by` | `BIGINT UNSIGNED` | YES |  |  |  |  | User reference used for audit and accountability. |

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
- `FULLTEXT KEY ft_visit_requests_frontend_search (request_code, delegation_name, registrant_full_name, registrant_organization, registrant_email, contact_person_full_name, contact_person_organization, contact_person_email)`

**Foreign Keys:**
- `CONSTRAINT fk_visit_requests_visitor FOREIGN KEY (visitor_user_id) REFERENCES users(user_id) ON UPDATE CASCADE ON DELETE SET NULL`
- `CONSTRAINT fk_visit_requests_partner FOREIGN KEY (partner_id) REFERENCES partners(partner_id) ON UPDATE CASCADE ON DELETE SET NULL`
- `CONSTRAINT fk_visit_requests_decided_by FOREIGN KEY (decided_by) REFERENCES users(user_id) ON UPDATE CASCADE ON DELETE SET NULL`
- `CONSTRAINT fk_visit_requests_cancelled_by FOREIGN KEY (cancelled_by) REFERENCES users(user_id) ON UPDATE CASCADE ON DELETE SET NULL`

**Check Constraints:**
- `CHECK (TRIM(registrant_job_title) <> '')`
- `CHECK (TRIM(registrant_phone) <> '')`
- `CHECK (TRIM(registrant_nationality) <> '')`
- `CHECK (TRIM(contact_person_full_name) <> '')`
- `CHECK (TRIM(contact_person_phone) <> '')`
- `CHECK (TRIM(contact_person_email) <> '')`
- `CHECK (visit_type <> 'OTHER' OR (visit_type_other IS NOT NULL AND TRIM(visit_type_other) <> ''))`

### 5.15. `visit_request_campuses`
**Purpose / Table Comment:** Mỗi campus trong request có một instance riêng. HO duyệt MULTI_CAMPUS thì chuyển ASSIGNED và gán Staff Leader làm coordinator; Staff Leader gán Staff làm host chính thức một lần; không hỗ trợ transfer host.

**Main Screens / UC Area:** Campus Visit Instance / Staff Leader Processing / Host Assignment / Lifecycle

**Column Count:** `25`

**Columns:**

| Column | Type | Null | Default | Extra | Key / FK / Index | Enum Values | Notes / Meaning |
|---|---|---:|---|---|---|---|---|
| `visit_instance_id` | `BIGINT UNSIGNED` | NO |  | AUTO_INCREMENT | PK |  | Identifier/reference field used to join or scope `visit_request_campuses` records. |
| `visit_request_id` | `BIGINT UNSIGNED` | NO |  |  | UNIQUE: uq_visit_instance_request_campus; IDX: idx_visit_instances_request; IDX: idx_visit_instances_visibility_campus_request; FK: visit_requests(visit_request_id) |  | Identifier/reference field used to join or scope `visit_request_campuses` records. |
| `campus_id` | `BIGINT UNSIGNED` | NO |  |  | UNIQUE: uq_visit_instance_request_campus; IDX: idx_visit_instances_campus_status_time; IDX: idx_visit_instances_visibility_campus_request; FK: campuses(campus_id) |  | Identifier/reference field used to join or scope `visit_request_campuses` records. |
| `planned_start_at` | `DATETIME` | NO |  |  | IDX: idx_visit_instances_campus_status_time; IDX: idx_visit_instances_status_time |  | Ngày giờ bắt đầu dự kiến tại campus |
| `planned_end_at` | `DATETIME` | NO |  |  |  |  | Ngày giờ kết thúc dự kiến tại campus |
| `status` | `ENUM(<br>    'WAITING_REQUEST_APPROVAL',<br>    'ASSIGNED',<br>    'ASSIGNED',<br>    'BEFORE_VISIT',<br>    'DURING_VISIT',<br>    'AFTER_VISIT',<br>    'CLOSED',<br>    'CANCELLED'<br>  )` | NO | 'WAITING_REQUEST_APPROVAL' |  | IDX: idx_visit_instances_campus_status_time; IDX: idx_visit_instances_status_time; IDX: idx_visit_instances_coordinator; IDX: idx_visit_instances_current_host; IDX: idx_visit_instances_visibility_campus_request | WAITING_REQUEST_APPROVAL, ASSIGNED, BEFORE_VISIT, DURING_VISIT, AFTER_VISIT, CLOSED, CANCELLED, REJECTED, ASSIGNED, BEFORE_VISIT, DURING_VISIT, AFTER_VISIT, CLOSED, CANCELLED | WAITING_REQUEST_APPROVAL=chờ duyệt request; ASSIGNED=đã duyệt, Staff Leader đang điều phối và chờ gán host chính thức; ASSIGNED=đã có Staff host chính thức. |
| `coordinator_user_id` | `BIGINT UNSIGNED` | YES |  |  | IDX: idx_visit_instances_coordinator; FK: users(user_id) |  | Staff Leader điều phối campus instance. Với MULTI_CAMPUS, HO duyệt xong thì hệ thống gán Staff Leader campus vào đây. |
| `coordinator_assigned_by` | `BIGINT UNSIGNED` | YES |  |  | IDX: idx_visit_instances_coordinator_assigned; FK: users(user_id) |  | Người gán coordinator, thường là HO khi duyệt MULTI_CAMPUS |
| `coordinator_assigned_at` | `DATETIME` | YES |  |  | IDX: idx_visit_instances_coordinator_assigned |  | Thời điểm gán coordinator |
| `current_host_user_id` | `BIGINT UNSIGNED` | YES |  |  | IDX: idx_visit_instances_current_host; IDX: idx_visit_instances_visibility_campus_request; FK: users(user_id) |  | Staff host chính thức của campus instance. Chỉ set một lần khi Staff Leader gán host; không hỗ trợ chuyển host. |
| `host_assigned_by` | `BIGINT UNSIGNED` | YES |  |  | IDX: idx_visit_instances_host_assigned; FK: users(user_id) |  | Staff Leader gán host chính thức |
| `host_assigned_at` | `DATETIME` | YES |  |  | IDX: idx_visit_instances_host_assigned |  | Thời điểm host chính thức được gán |
| `closed_by` | `BIGINT UNSIGNED` | YES |  |  | FK: users(user_id) |  | User reference used for audit and accountability. |
| `closed_at` | `DATETIME` | YES |  |  |  |  | Business/audit timestamp used for timeline, SLA, filtering, or history. |
| `close_note` | `TEXT` | YES |  |  |  |  | Business note/reason used for explanation and audit. |
| `cancelled_by` | `BIGINT UNSIGNED` | YES |  |  | IDX: idx_visit_instances_cancelled; FK: users(user_id) |  | Visitor hoặc Host thực hiện hủy campus instance |
| `cancelled_at` | `DATETIME` | YES |  |  | IDX: idx_visit_instances_cancelled; IDX: idx_visit_instances_cancel_actor |  | Thời điểm hủy campus instance |
| `cancellation_actor_type` | `ENUM('VISITOR','HOST')` | YES |  |  | IDX: idx_visit_instances_cancel_actor | VISITOR, HOST | VISITOR=khách tự hủy; HOST=Staff được gán làm host hủy thay khách theo xác nhận ngoài hệ thống |
| `cancellation_source` | `ENUM('SELF_SERVICE','EXTERNAL_CONFIRMATION')` | YES |  |  |  | SELF_SERVICE, EXTERNAL_CONFIRMATION | SELF_SERVICE=Visitor tự hủy; EXTERNAL_CONFIRMATION=Host hủy sau khi khách xác nhận ngoài hệ thống |
| `cancellation_reason` | `TEXT` | YES |  |  |  |  | Lý do hủy; nếu EXTERNAL_CONFIRMATION thì ghi rõ kênh xác nhận, thời điểm, người xác nhận và lý do. |
| `row_version` | `INT UNSIGNED` | NO | 0 |  |  |  | Optimistic concurrency token |
| `created_at` | `DATETIME` | NO | CURRENT_TIMESTAMP |  |  |  | Business/audit timestamp used for timeline, SLA, filtering, or history. |
| `created_by` | `BIGINT UNSIGNED` | YES |  |  |  |  | User reference used for audit and accountability. |
| `updated_at` | `DATETIME` | YES | NULL ON UPDATE CURRENT_TIMESTAMP | ON UPDATE CURRENT_TIMESTAMP |  |  | Business/audit timestamp used for timeline, SLA, filtering, or history. |
| `updated_by` | `BIGINT UNSIGNED` | YES |  |  |  |  | User reference used for audit and accountability. |

**Primary Key:**
- `PRIMARY KEY (visit_instance_id)`

**Unique Constraints:**
- `UNIQUE KEY uq_visit_instance_request_campus (visit_request_id, campus_id)`

**Indexes:**
- `KEY idx_visit_instances_campus_status_time (campus_id, status, planned_start_at)`
- `KEY idx_visit_instances_request (visit_request_id)`
- `KEY idx_visit_instances_status_time (status, planned_start_at)`
- `KEY idx_visit_instances_coordinator (coordinator_user_id, status)`
- `KEY idx_visit_instances_coordinator_assigned (coordinator_assigned_by, coordinator_assigned_at)`
- `KEY idx_visit_instances_current_host (current_host_user_id, status)`
- `KEY idx_visit_instances_host_assigned (host_assigned_by, host_assigned_at)`
- `KEY idx_visit_instances_cancelled (cancelled_by, cancelled_at)`
- `KEY idx_visit_instances_cancel_actor (cancellation_actor_type, cancelled_at)`
- `KEY idx_visit_instances_visibility_campus_request (campus_id, visit_request_id, status, current_host_user_id)`

**Foreign Keys:**
- `CONSTRAINT fk_visit_instances_request FOREIGN KEY (visit_request_id) REFERENCES visit_requests(visit_request_id) ON UPDATE CASCADE ON DELETE RESTRICT`
- `CONSTRAINT fk_visit_instances_campus FOREIGN KEY (campus_id) REFERENCES campuses(campus_id) ON UPDATE CASCADE ON DELETE RESTRICT`
- `CONSTRAINT fk_visit_instances_coordinator FOREIGN KEY (coordinator_user_id) REFERENCES users(user_id) ON UPDATE CASCADE ON DELETE SET NULL`
- `CONSTRAINT fk_visit_instances_coordinator_assigned_by FOREIGN KEY (coordinator_assigned_by) REFERENCES users(user_id) ON UPDATE CASCADE ON DELETE SET NULL`
- `CONSTRAINT fk_visit_instances_current_host FOREIGN KEY (current_host_user_id) REFERENCES users(user_id) ON UPDATE CASCADE ON DELETE SET NULL`
- `CONSTRAINT fk_visit_instances_host_assigned_by FOREIGN KEY (host_assigned_by) REFERENCES users(user_id) ON UPDATE CASCADE ON DELETE SET NULL`
- `CONSTRAINT fk_visit_instances_closed_by FOREIGN KEY (closed_by) REFERENCES users(user_id) ON UPDATE CASCADE ON DELETE SET NULL`
- `CONSTRAINT fk_visit_instances_cancelled_by FOREIGN KEY (cancelled_by) REFERENCES users(user_id) ON UPDATE CASCADE ON DELETE SET NULL`

**Check Constraints:**
- `CHECK (planned_end_at > planned_start_at)`

### 5.16. `visit_guest_members`
**Purpose / Table Comment:** Danh sách từng người trong đoàn khách. Không lưu consent hình ảnh vì form đã bỏ phần xác nhận sử dụng hình ảnh/thông tin.

**Main Screens / UC Area:** Guest Member List / Delegation Detail / Minutes Source

**Column Count:** `12`

**Columns:**

| Column | Type | Null | Default | Extra | Key / FK / Index | Enum Values | Notes / Meaning |
|---|---|---:|---|---|---|---|---|
| `guest_member_id` | `BIGINT UNSIGNED` | NO |  | AUTO_INCREMENT | PK |  | Identifier/reference field used to join or scope `visit_guest_members` records. |
| `visit_request_id` | `BIGINT UNSIGNED` | NO |  |  | IDX: idx_guest_members_request; IDX: idx_guest_members_type_order; FK: visit_requests(visit_request_id) |  | Identifier/reference field used to join or scope `visit_guest_members` records. |
| `member_type` | `ENUM('GUEST','EXTERNAL_SUPPORT')` | NO | 'GUEST' |  | IDX: idx_guest_members_type_order | GUEST, EXTERNAL_SUPPORT | Field `member_type` used by `visit_guest_members` business logic and screens. |
| `full_name` | `VARCHAR(150)` | NO |  |  |  |  | Field `full_name` used by `visit_guest_members` business logic and screens. |
| `organization` | `VARCHAR(200)` | NO |  |  |  |  | Field `organization` used by `visit_guest_members` business logic and screens. |
| `job_title` | `VARCHAR(150)` | NO |  |  |  |  | Field `job_title` used by `visit_guest_members` business logic and screens. |
| `nationality` | `VARCHAR(100)` | NO |  |  |  |  | Field `nationality` used by `visit_guest_members` business logic and screens. |
| `display_order` | `INT UNSIGNED` | NO | 0 |  | IDX: idx_guest_members_type_order |  | Field `display_order` used by `visit_guest_members` business logic and screens. |
| `created_at` | `DATETIME` | NO | CURRENT_TIMESTAMP |  |  |  | Business/audit timestamp used for timeline, SLA, filtering, or history. |
| `created_by` | `BIGINT UNSIGNED` | YES |  |  |  |  | User reference used for audit and accountability. |
| `updated_at` | `DATETIME` | YES | NULL ON UPDATE CURRENT_TIMESTAMP | ON UPDATE CURRENT_TIMESTAMP |  |  | Business/audit timestamp used for timeline, SLA, filtering, or history. |
| `updated_by` | `BIGINT UNSIGNED` | YES |  |  |  |  | User reference used for audit and accountability. |

**Primary Key:**
- `PRIMARY KEY (guest_member_id)`

**Indexes:**
- `KEY idx_guest_members_request (visit_request_id)`
- `KEY idx_guest_members_type_order (visit_request_id, member_type, display_order)`

**Foreign Keys:**
- `CONSTRAINT fk_guest_members_request FOREIGN KEY (visit_request_id) REFERENCES visit_requests(visit_request_id) ON UPDATE CASCADE ON DELETE RESTRICT`

**Check Constraints:**
- `CHECK (TRIM(full_name) <> '')`
- `CHECK (TRIM(organization) <> '')`
- `CHECK (TRIM(job_title) <> '')`
- `CHECK (TRIM(nationality) <> '')`

### 5.17. `visit_participants`
**Purpose / Table Comment:** Người nội bộ tham gia visit instance. Chỉ gồm IC_HOST, IC_SUPPORT, DEPT_SUPPORT, STUDENT. Host chính lưu bằng is_host.

**Main Screens / UC Area:** Internal Participants / Host / Department / Student Assignments / Email Action Responses

**Column Count:** `16`

**Columns:**

| Column | Type | Null | Default | Extra | Key / FK / Index | Enum Values | Notes / Meaning |
|---|---|---:|---|---|---|---|---|
| `participant_id` | `BIGINT UNSIGNED` | NO |  | AUTO_INCREMENT | PK |  | Identifier/reference field used to join or scope `visit_participants` records. |
| `visit_instance_id` | `BIGINT UNSIGNED` | NO |  |  | UNIQUE: uq_visit_participants_user; IDX: idx_visit_participants_one_host_lookup; IDX: idx_visit_participants_instance; FK: visit_request_campuses(visit_instance_id) |  | Identifier/reference field used to join or scope `visit_participants` records. |
| `user_id` | `BIGINT UNSIGNED` | NO |  |  | UNIQUE: uq_visit_participants_user; IDX: idx_visit_participants_user_status; FK: users(user_id) |  | Identifier/reference field used to join or scope `visit_participants` records. |
| `participant_role` | `ENUM('IC_HOST','IC_SUPPORT','DEPT_SUPPORT','STUDENT')` | NO | 'IC_SUPPORT' |  | IDX: idx_visit_participants_role_status | IC_HOST, IC_SUPPORT, DEPT_SUPPORT, STUDENT | Field `participant_role` used by `visit_participants` business logic and screens. |
| `is_host` | `BOOLEAN` | NO | FALSE |  | IDX: idx_visit_participants_one_host_lookup |  | Field `is_host` used by `visit_participants` business logic and screens. |
| `status` | `ENUM('INVITED','ACCEPTED','DECLINED','ASSIGNED','REMOVED')` | NO | 'INVITED' |  | IDX: idx_visit_participants_user_status; IDX: idx_visit_participants_role_status | INVITED, ACCEPTED, DECLINED, ASSIGNED, REMOVED | Status field used for workflow state, filtering and UI badges. |
| `invited_by` | `BIGINT UNSIGNED` | YES |  |  | FK: users(user_id) |  | User reference used for audit and accountability. |
| `invited_at` | `DATETIME` | YES |  |  |  |  | Business/audit timestamp used for timeline, SLA, filtering, or history. |
| `responded_at` | `DATETIME` | YES |  |  |  |  | Business/audit timestamp used for timeline, SLA, filtering, or history. |
| `assigned_by` | `BIGINT UNSIGNED` | YES |  |  | FK: users(user_id) |  | User reference used for audit and accountability. |
| `assigned_at` | `DATETIME` | YES |  |  |  |  | Business/audit timestamp used for timeline, SLA, filtering, or history. |
| `note` | `TEXT` | YES |  |  |  |  | Business note/reason used for explanation and audit. |
| `created_at` | `DATETIME` | NO | CURRENT_TIMESTAMP |  |  |  | Business/audit timestamp used for timeline, SLA, filtering, or history. |
| `created_by` | `BIGINT UNSIGNED` | YES |  |  |  |  | User reference used for audit and accountability. |
| `updated_at` | `DATETIME` | YES | NULL ON UPDATE CURRENT_TIMESTAMP | ON UPDATE CURRENT_TIMESTAMP |  |  | Business/audit timestamp used for timeline, SLA, filtering, or history. |
| `updated_by` | `BIGINT UNSIGNED` | YES |  |  |  |  | User reference used for audit and accountability. |

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

### 5.18. `visit_agendas`
**Purpose / Table Comment:** Concrete visit agenda rows per campus instance. `start_time`/`end_time` are absolute `DATETIME` computed from `visit_request_campuses.planned_start_at` + agenda template offsets when a template is applied (Apply Agenda Template), or entered manually. The source template (when applied) is traced via `source_template_item_id → agenda_template_items → agenda_templates`; `visit_request_campuses` never stores the applied template.

**Main Screens / UC Area:** Visit Agenda / Logistics Preparation / Delegation Detail

**Column Count:** `14`

**Columns:**

| Column | Type | Null | Default | Extra | Key / FK / Index | Enum Values | Notes / Meaning |
|---|---|---:|---|---|---|---|---|
| `agenda_id` | `BIGINT UNSIGNED` | NO |  | AUTO_INCREMENT | PK |  | Identifier/reference field used to join or scope `visit_agendas` records. |
| `visit_instance_id` | `BIGINT UNSIGNED` | NO |  |  | UNIQUE: uq_visit_agendas_order; IDX: idx_visit_agendas_time; FK: visit_request_campuses(visit_instance_id) |  | Identifier/reference field used to join or scope `visit_agendas` records. |
| `sequence_order` | `INT UNSIGNED` | NO |  |  | UNIQUE: uq_visit_agendas_order |  | Field `sequence_order` used by `visit_agendas` business logic and screens. |
| `title` | `VARCHAR(255)` | NO |  |  |  |  | Field `title` used by `visit_agendas` business logic and screens. |
| `description` | `TEXT` | YES |  |  |  |  | Field `description` used by `visit_agendas` business logic and screens. |
| `start_time` | `DATETIME` | NO |  |  | IDX: idx_visit_agendas_time; IDX: idx_visit_agendas_responsible |  | Field `start_time` used by `visit_agendas` business logic and screens. |
| `end_time` | `DATETIME` | YES |  |  |  |  | Field `end_time` used by `visit_agendas` business logic and screens. |
| `location` | `VARCHAR(255)` | YES |  |  |  |  | Field `location` used by `visit_agendas` business logic and screens. |
| `responsible_user_id` | `BIGINT UNSIGNED` | YES |  |  | IDX: idx_visit_agendas_responsible; FK: users(user_id) |  | Identifier/reference field used to join or scope `visit_agendas` records. |
| `source_template_item_id` | `BIGINT UNSIGNED` | YES |  |  | IDX: idx_visit_agendas_source_template_item; FK: agenda_template_items(agenda_template_item_id) |  | The template item this row was generated from (Apply Agenda Template), or NULL for manual rows. Used to trace the source template. |
| `created_at` | `DATETIME` | NO | CURRENT_TIMESTAMP |  |  |  | Business/audit timestamp used for timeline, SLA, filtering, or history. |
| `created_by` | `BIGINT UNSIGNED` | YES |  |  |  |  | User reference used for audit and accountability. |
| `updated_at` | `DATETIME` | YES | NULL ON UPDATE CURRENT_TIMESTAMP | ON UPDATE CURRENT_TIMESTAMP |  |  | Business/audit timestamp used for timeline, SLA, filtering, or history. |
| `updated_by` | `BIGINT UNSIGNED` | YES |  |  |  |  | User reference used for audit and accountability. |

**Primary Key:**
- `PRIMARY KEY (agenda_id)`

**Unique Constraints:**
- `UNIQUE KEY uq_visit_agendas_order (visit_instance_id, sequence_order)`

**Indexes:**
- `KEY idx_visit_agendas_time (visit_instance_id, start_time)`
- `KEY idx_visit_agendas_responsible (responsible_user_id, start_time)`
- `KEY idx_visit_agendas_source_template_item (source_template_item_id)`

**Foreign Keys:**
- `CONSTRAINT fk_visit_agendas_instance FOREIGN KEY (visit_instance_id) REFERENCES visit_request_campuses(visit_instance_id) ON UPDATE CASCADE ON DELETE CASCADE`
- `CONSTRAINT fk_visit_agendas_responsible_user FOREIGN KEY (responsible_user_id) REFERENCES users(user_id) ON UPDATE CASCADE ON DELETE SET NULL`
- `CONSTRAINT fk_visit_agendas_source_template_item FOREIGN KEY (source_template_item_id) REFERENCES agenda_template_items(agenda_template_item_id) ON UPDATE CASCADE ON DELETE SET NULL`
- `CONSTRAINT fk_visit_agendas_created_by FOREIGN KEY (created_by) REFERENCES users(user_id) ON UPDATE CASCADE ON DELETE SET NULL`
- `CONSTRAINT fk_visit_agendas_updated_by FOREIGN KEY (updated_by) REFERENCES users(user_id) ON UPDATE CASCADE ON DELETE SET NULL`

**Check Constraints:**
- `CHECK (end_time IS NULL OR end_time > start_time)`

### 5.19. `visit_logistics_items`
**Purpose / Table Comment:** Yêu cầu hậu cần/resource cho visit: gửi yêu cầu, đề xuất thay đổi, tiếp nhận, phân công, xác nhận và hoàn thành. Ký mượn/ký trả lưu ở visit_logistics_item_handovers.

**Main Screens / UC Area:** Logistics / Resource Request / Assignment / Negotiation / Completion

**Column Count:** `39`

**Columns:**

| Column | Type | Null | Default | Extra | Key / FK / Index | Enum Values | Notes / Meaning |
|---|---|---:|---|---|---|---|---|
| `logistics_item_id` | `BIGINT UNSIGNED` | NO |  | AUTO_INCREMENT | PK |  | Identifier/reference field used to join or scope `visit_logistics_items` records. |
| `visit_instance_id` | `BIGINT UNSIGNED` | NO |  |  | IDX: idx_logistics_instance_status; FK: visit_request_campuses(visit_instance_id) |  | Identifier/reference field used to join or scope `visit_logistics_items` records. |
| `item_type` | `ENUM('ROOM','TRANSPORT','MEAL','EQUIPMENT','BANNER','LED','OTHER')` | NO |  |  | IDX: idx_logistics_item_status | ROOM, TRANSPORT, MEAL, EQUIPMENT, BANNER, LED, OTHER | Field `item_type` used by `visit_logistics_items` business logic and screens. |
| `title` | `VARCHAR(255)` | NO |  |  |  |  | Field `title` used by `visit_logistics_items` business logic and screens. |
| `description` | `TEXT` | YES |  |  |  |  | Nội dung chi tiết công việc gốc |
| `quantity` | `INT UNSIGNED` | YES |  |  |  |  | Số lượng yêu cầu gốc |
| `usage_start_at` | `DATETIME` | YES |  |  | IDX: idx_logistics_usage_time |  | Thời gian bắt đầu sử dụng resource |
| `usage_end_at` | `DATETIME` | YES |  |  | IDX: idx_logistics_usage_time |  | Thời gian kết thúc sử dụng resource |
| `status` | `ENUM(<br>    'PLANNED',<br>    'REQUESTED',<br>    'CHANGE_PROPOSED',<br>    'RECEIVED',<br>    'ASSIGNED',<br>    'ACCEPTED',<br>    'IN_PROGRESS',<br>    'READY',<br>    'DONE',<br>    'REJECTED',<br>    'CANCELLED'<br>  )` | NO | 'PLANNED' |  | IDX: idx_logistics_instance_status; IDX: idx_logistics_item_status; IDX: idx_logistics_department_status; IDX: idx_logistics_assignee_status | PLANNED, REQUESTED, CHANGE_PROPOSED, RECEIVED, ASSIGNED, ACCEPTED, IN_PROGRESS, READY, DONE, REJECTED, CANCELLED | Status field used for workflow state, filtering and UI badges. |
| `priority` | `ENUM('LOW','MEDIUM','HIGH','URGENT')` | NO | 'MEDIUM' |  | IDX: idx_logistics_priority_due | LOW, MEDIUM, HIGH, URGENT | Field `priority` used by `visit_logistics_items` business logic and screens. |
| `requested_by` | `BIGINT UNSIGNED` | YES |  |  | IDX: idx_logistics_requested_by_time; FK: users(user_id) |  | Người gửi yêu cầu hậu cần/resource |
| `requested_to_department_id` | `BIGINT UNSIGNED` | YES |  |  | IDX: idx_logistics_department_status; FK: departments(department_id) |  | Phòng ban được yêu cầu xử lý |
| `requested_at` | `DATETIME` | YES |  |  | IDX: idx_logistics_requested_by_time |  | Thời điểm gửi yêu cầu |
| `received_by` | `BIGINT UNSIGNED` | YES |  |  | IDX: idx_logistics_received_by_time; FK: users(user_id) |  | Trưởng phòng/người tiếp nhận yêu cầu |
| `received_at` | `DATETIME` | YES |  |  | IDX: idx_logistics_received_by_time |  | Thời điểm tiếp nhận yêu cầu |
| `assigned_to_user_id` | `BIGINT UNSIGNED` | YES |  |  | IDX: idx_logistics_assignee_status; FK: users(user_id) |  | Nhân viên được giao xử lý chính |
| `assigned_by` | `BIGINT UNSIGNED` | YES |  |  | FK: users(user_id) |  | Người phân công |
| `assigned_at` | `DATETIME` | YES |  |  |  |  | Thời điểm phân công |
| `assignee_accepted_at` | `DATETIME` | YES |  |  |  |  | Thời điểm nhân viên xác nhận nhận nhiệm vụ |
| `assignee_response_note` | `TEXT` | YES |  |  |  |  | Ghi chú khi nhân viên nhận/từ chối nếu có |
| `due_at` | `DATETIME` | YES |  |  | IDX: idx_logistics_due; IDX: idx_logistics_priority_due |  | Deadline hoàn thành hạng mục |
| `completed_at` | `DATETIME` | YES |  |  |  |  | Thời điểm hoàn thành |
| `proposed_by` | `BIGINT UNSIGNED` | YES |  |  | IDX: idx_logistics_proposed_by_time; FK: users(user_id) |  | Người gửi đề xuất thay đổi |
| `proposed_at` | `DATETIME` | YES |  |  | IDX: idx_logistics_proposed_by_time |  | Thời điểm gửi đề xuất thay đổi |
| `proposed_quantity` | `INT UNSIGNED` | YES |  |  |  |  | Số lượng được đề xuất thay đổi |
| `proposed_usage_start_at` | `DATETIME` | YES |  |  |  |  | Thời gian bắt đầu sử dụng được đề xuất |
| `proposed_usage_end_at` | `DATETIME` | YES |  |  |  |  | Thời gian kết thúc sử dụng được đề xuất |
| `proposed_description` | `TEXT` | YES |  |  |  |  | Nội dung chi tiết công việc được đề xuất thay đổi |
| `proposal_note` | `TEXT` | YES |  |  |  |  | Lý do/ghi chú đề xuất thay đổi |
| `proposal_responded_by` | `BIGINT UNSIGNED` | YES |  |  | FK: users(user_id) |  | Người xác nhận/từ chối đề xuất |
| `proposal_responded_at` | `DATETIME` | YES |  |  |  |  | Thời điểm xác nhận/từ chối đề xuất |
| `proposal_response` | `ENUM('ACCEPTED','REJECTED')` | YES |  |  |  | ACCEPTED, REJECTED | Kết quả phản hồi đề xuất |
| `proposal_response_note` | `TEXT` | YES |  |  |  |  | Ghi chú phản hồi đề xuất |
| `decision_note` | `TEXT` | YES |  |  |  |  | Lý do reject/cancel hoặc ghi chú xử lý |
| `row_version` | `INT UNSIGNED` | NO | 0 |  |  |  | Optimistic concurrency token |
| `created_at` | `DATETIME` | NO | CURRENT_TIMESTAMP |  |  |  | Business/audit timestamp used for timeline, SLA, filtering, or history. |
| `created_by` | `BIGINT UNSIGNED` | YES |  |  |  |  | User reference used for audit and accountability. |
| `updated_at` | `DATETIME` | YES | NULL ON UPDATE CURRENT_TIMESTAMP | ON UPDATE CURRENT_TIMESTAMP |  |  | Business/audit timestamp used for timeline, SLA, filtering, or history. |
| `updated_by` | `BIGINT UNSIGNED` | YES |  |  |  |  | User reference used for audit and accountability. |

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

**V10 Usage Notes:**
- This is the main logistics/resource workflow table. It stores request, department receiving, assignment, assignee response, negotiation/proposal and completion. It no longer stores signing/hand-over fields; signatures must be read from `visit_logistics_item_handovers`. Chuyển nhiệm vụ/reassignment is not supported.

### 5.20. `visit_logistics_item_handovers`
**Purpose / Table Comment:** Bảng lưu ký nhận/ký trả đồ mượn cho logistics item

**Main Screens / UC Area:** Logistics Borrow/Return Handover Signatures

**Column Count:** `12`

**Columns:**

| Column | Type | Null | Default | Extra | Key / FK / Index | Enum Values | Notes / Meaning |
|---|---|---:|---|---|---|---|---|
| `handover_id` | `BIGINT UNSIGNED` | NO |  | AUTO_INCREMENT | PK |  | Identifier/reference field used to join or scope `visit_logistics_item_handovers` records. |
| `logistics_item_id` | `BIGINT UNSIGNED` | NO |  |  | UNIQUE: uq_logistics_handover_type; IDX: idx_handover_item_type; FK: visit_logistics_items(logistics_item_id) |  | Identifier/reference field used to join or scope `visit_logistics_item_handovers` records. |
| `handover_type` | `ENUM('BORROW','RETURN')` | NO |  |  | UNIQUE: uq_logistics_handover_type; IDX: idx_handover_item_type | BORROW, RETURN | BORROW=lúc giao/mượn đồ, RETURN=lúc trả/nhận lại đồ |
| `borrower_signed_by` | `BIGINT UNSIGNED` | YES |  |  | IDX: idx_handover_borrower_signed; FK: users(user_id) |  | Bên mượn ký: BORROW=ký nhận, RETURN=ký trả |
| `borrower_signed_at` | `DATETIME` | YES |  |  | IDX: idx_handover_borrower_signed |  | Business/audit timestamp used for timeline, SLA, filtering, or history. |
| `provider_signed_by` | `BIGINT UNSIGNED` | YES |  |  | IDX: idx_handover_provider_signed; FK: users(user_id) |  | Bên cho mượn ký: BORROW=ký bàn giao, RETURN=ký nhận lại |
| `provider_signed_at` | `DATETIME` | YES |  |  | IDX: idx_handover_provider_signed |  | Business/audit timestamp used for timeline, SLA, filtering, or history. |
| `item_condition` | `ENUM('GOOD','DAMAGED','MISSING','OTHER')` | YES |  |  |  | GOOD, DAMAGED, MISSING, OTHER | Field `item_condition` used by `visit_logistics_item_handovers` business logic and screens. |
| `condition_note` | `TEXT` | YES |  |  |  |  | Business note/reason used for explanation and audit. |
| `attachment_file_id` | `BIGINT UNSIGNED` | YES |  |  | IDX: idx_handover_attachment_file; FK: files(file_id) |  | Identifier/reference field used to join or scope `visit_logistics_item_handovers` records. |
| `created_at` | `DATETIME` | NO | CURRENT_TIMESTAMP |  |  |  | Business/audit timestamp used for timeline, SLA, filtering, or history. |
| `created_by` | `BIGINT UNSIGNED` | YES |  |  | IDX: idx_handover_created_by; FK: users(user_id) |  | User reference used for audit and accountability. |

**Primary Key:**
- `PRIMARY KEY (handover_id)`

**Unique Constraints:**
- `UNIQUE KEY uq_logistics_handover_type (logistics_item_id, handover_type)`

**Indexes:**
- `KEY idx_handover_item_type (logistics_item_id, handover_type)`
- `KEY idx_handover_borrower_signed (borrower_signed_by, borrower_signed_at)`
- `KEY idx_handover_provider_signed (provider_signed_by, provider_signed_at)`
- `KEY idx_handover_attachment_file (attachment_file_id)`
- `KEY idx_handover_created_by (created_by)`

**Foreign Keys:**
- `CONSTRAINT fk_lh_item FOREIGN KEY (logistics_item_id) REFERENCES visit_logistics_items(logistics_item_id) ON UPDATE CASCADE ON DELETE CASCADE`
- `CONSTRAINT fk_lh_borrower_signed_by FOREIGN KEY (borrower_signed_by) REFERENCES users(user_id) ON UPDATE CASCADE ON DELETE SET NULL`
- `CONSTRAINT fk_lh_provider_signed_by FOREIGN KEY (provider_signed_by) REFERENCES users(user_id) ON UPDATE CASCADE ON DELETE SET NULL`
- `CONSTRAINT fk_lh_attachment_file FOREIGN KEY (attachment_file_id) REFERENCES files(file_id) ON UPDATE CASCADE ON DELETE SET NULL`
- `CONSTRAINT fk_lh_created_by FOREIGN KEY (created_by) REFERENCES users(user_id) ON UPDATE CASCADE ON DELETE SET NULL`

**V10 Usage Notes:**
- Each logistics item can have one `BORROW` record and one `RETURN` record. `BORROW`: borrower signs receipt and provider signs handover. `RETURN`: borrower signs return and provider signs receipt back. This table supports 4 signatures and 4 signature timestamps.

### 5.21. `minutes`
**Purpose / Table Comment:** Biên bản chuyến thăm. Không lưu file đính kèm và không lưu action item dạng JSON; action item tách bảng riêng.

**Main Screens / UC Area:** Meeting Minutes / Close Delegation

**Column Count:** `14`

**Columns:**

| Column | Type | Null | Default | Extra | Key / FK / Index | Enum Values | Notes / Meaning |
|---|---|---:|---|---|---|---|---|
| `minutes_id` | `BIGINT UNSIGNED` | NO |  | AUTO_INCREMENT | PK |  | Identifier/reference field used to join or scope `minutes` records. |
| `visit_instance_id` | `BIGINT UNSIGNED` | NO |  |  | IDX: idx_minutes_visit_status; FK: visit_request_campuses(visit_instance_id) |  | Identifier/reference field used to join or scope `minutes` records. |
| `title` | `VARCHAR(255)` | NO |  |  | IDX: ft_minutes_search |  | Field `title` used by `minutes` business logic and screens. |
| `content` | `LONGTEXT` | YES |  |  | IDX: ft_minutes_search |  | Field `content` used by `minutes` business logic and screens. |
| `status` | `ENUM('DRAFT','SAVED')` | NO | 'DRAFT' |  | IDX: idx_minutes_visit_status | DRAFT, SAVED | DRAFT=biên bản nháp, SAVED=đã lưu nội dung; quyền sửa bị khóa khi visit instance CLOSED |
| `edit_locked_by` | `BIGINT UNSIGNED` | YES |  |  | IDX: idx_minutes_edit_lock; FK: users(user_id) |  | User hiện đang giữ quyền sửa biên bản |
| `edit_locked_at` | `DATETIME` | YES |  |  |  |  | Thời điểm bắt đầu giữ quyền sửa |
| `edit_lock_expires_at` | `DATETIME` | YES |  |  | IDX: idx_minutes_edit_lock |  | Thời điểm lock sửa hết hạn |
| `edit_lock_token` | `CHAR(36)` | YES |  |  |  |  | Token phiên sửa, dùng để xác nhận đúng người đang giữ lock |
| `row_version` | `INT UNSIGNED` | NO | 0 |  |  |  | Version chống ghi đè khi cập nhật đồng thời |
| `created_at` | `DATETIME` | NO | CURRENT_TIMESTAMP |  | IDX: idx_minutes_created_by_time |  | Business/audit timestamp used for timeline, SLA, filtering, or history. |
| `created_by` | `BIGINT UNSIGNED` | YES |  |  | IDX: idx_minutes_created_by_time; FK: users(user_id) |  | User reference used for audit and accountability. |
| `updated_at` | `DATETIME` | YES | NULL ON UPDATE CURRENT_TIMESTAMP | ON UPDATE CURRENT_TIMESTAMP |  |  | Business/audit timestamp used for timeline, SLA, filtering, or history. |
| `updated_by` | `BIGINT UNSIGNED` | YES |  |  | FK: users(user_id) |  | User reference used for audit and accountability. |

**Primary Key:**
- `PRIMARY KEY (minutes_id)`

**Indexes:**
- `KEY idx_minutes_visit_status (visit_instance_id, status)`
- `KEY idx_minutes_created_by_time (created_by, created_at)`
- `KEY idx_minutes_edit_lock (edit_locked_by, edit_lock_expires_at)`
- `FULLTEXT KEY ft_minutes_search (title, content)`

**Foreign Keys:**
- `CONSTRAINT fk_minutes_visit_instance FOREIGN KEY (visit_instance_id) REFERENCES visit_request_campuses(visit_instance_id) ON UPDATE CASCADE ON DELETE RESTRICT`
- `CONSTRAINT fk_minutes_created_by FOREIGN KEY (created_by) REFERENCES users(user_id) ON UPDATE CASCADE ON DELETE SET NULL`
- `CONSTRAINT fk_minutes_updated_by FOREIGN KEY (updated_by) REFERENCES users(user_id) ON UPDATE CASCADE ON DELETE SET NULL`
- `CONSTRAINT fk_minutes_edit_locked_by FOREIGN KEY (edit_locked_by) REFERENCES users(user_id) ON UPDATE CASCADE ON DELETE SET NULL`

### 5.22. `minute_participants`
**Purpose / Table Comment:** Snapshot participant list for meeting minutes; replaces minutes.participants_json.

**Main Screens / UC Area:** Minutes Attendance / Participant Snapshot

**Column Count:** `14`

**Columns:**

| Column | Type | Null | Default | Extra | Key / FK / Index | Enum Values | Notes / Meaning |
|---|---|---:|---|---|---|---|---|
| `minute_participant_id` | `BIGINT UNSIGNED` | NO |  | AUTO_INCREMENT | PK |  | Identifier/reference field used to join or scope `minute_participants` records. |
| `minutes_id` | `BIGINT UNSIGNED` | NO |  |  | IDX: idx_minute_participants_minutes_order; IDX: idx_minute_participants_attendance; FK: minutes(minutes_id) |  | Identifier/reference field used to join or scope `minute_participants` records. |
| `user_id` | `BIGINT UNSIGNED` | YES |  |  | IDX: idx_minute_participants_user; FK: users(user_id) |  | Identifier/reference field used to join or scope `minute_participants` records. |
| `guest_member_id` | `BIGINT UNSIGNED` | YES |  |  | IDX: idx_minute_participants_guest_member; FK: visit_guest_members(guest_member_id) |  | Identifier/reference field used to join or scope `minute_participants` records. |
| `full_name_snapshot` | `VARCHAR(255)` | NO |  |  |  |  | Field `full_name_snapshot` used by `minute_participants` business logic and screens. |
| `role_snapshot` | `VARCHAR(120)` | YES |  |  |  |  | Field `role_snapshot` used by `minute_participants` business logic and screens. |
| `organization_snapshot` | `VARCHAR(255)` | YES |  |  |  |  | Field `organization_snapshot` used by `minute_participants` business logic and screens. |
| `email_snapshot` | `VARCHAR(150)` | YES |  |  |  |  | Field `email_snapshot` used by `minute_participants` business logic and screens. |
| `attendance_status` | `ENUM('PRESENT','ABSENT','EXCUSED')` | NO | 'PRESENT' |  | IDX: idx_minute_participants_attendance | PRESENT, ABSENT, EXCUSED | PRESENT=có mặt, ABSENT=vắng mặt, EXCUSED=vắng có lý do |
| `attendance_note` | `TEXT` | YES |  |  |  |  | Ghi chú điểm danh/lý do vắng nếu có |
| `checked_at` | `DATETIME` | YES |  |  |  |  | Thời điểm ghi nhận điểm danh |
| `checked_by` | `BIGINT UNSIGNED` | YES |  |  | IDX: idx_minute_participants_checked_by; FK: users(user_id) |  | Người thực hiện điểm danh |
| `display_order` | `INT UNSIGNED` | NO | 0 |  | IDX: idx_minute_participants_minutes_order |  | Field `display_order` used by `minute_participants` business logic and screens. |
| `created_at` | `DATETIME` | NO | CURRENT_TIMESTAMP |  |  |  | Business/audit timestamp used for timeline, SLA, filtering, or history. |

**Primary Key:**
- `PRIMARY KEY (minute_participant_id)`

**Indexes:**
- `KEY idx_minute_participants_minutes_order (minutes_id, display_order)`
- `KEY idx_minute_participants_user (user_id)`
- `KEY idx_minute_participants_guest_member (guest_member_id)`
- `KEY idx_minute_participants_attendance (minutes_id, attendance_status)`
- `KEY idx_minute_participants_checked_by (checked_by)`

**Foreign Keys:**
- `CONSTRAINT fk_minute_participants_minutes FOREIGN KEY (minutes_id) REFERENCES minutes(minutes_id) ON UPDATE CASCADE ON DELETE CASCADE`
- `CONSTRAINT fk_minute_participants_user FOREIGN KEY (user_id) REFERENCES users(user_id) ON UPDATE CASCADE ON DELETE SET NULL`
- `CONSTRAINT fk_minute_participants_guest_member FOREIGN KEY (guest_member_id) REFERENCES visit_guest_members(guest_member_id) ON UPDATE CASCADE ON DELETE SET NULL`
- `CONSTRAINT fk_minute_participants_checked_by FOREIGN KEY (checked_by) REFERENCES users(user_id) ON UPDATE CASCADE ON DELETE SET NULL`

### 5.23. `minute_action_items`
**Purpose / Table Comment:** Các đầu việc sau biên bản. Không gán người phụ trách; chỉ có note, deadline và trạng thái hoàn thành.

**Main Screens / UC Area:** Minutes Action Items / Follow-up

**Column Count:** `12`

**Columns:**

| Column | Type | Null | Default | Extra | Key / FK / Index | Enum Values | Notes / Meaning |
|---|---|---:|---|---|---|---|---|
| `action_item_id` | `BIGINT UNSIGNED` | NO |  | AUTO_INCREMENT | PK |  | Identifier/reference field used to join or scope `minute_action_items` records. |
| `minutes_id` | `BIGINT UNSIGNED` | NO |  |  | IDX: idx_action_items_minutes; IDX: idx_action_items_order; FK: minutes(minutes_id) |  | Identifier/reference field used to join or scope `minute_action_items` records. |
| `title` | `VARCHAR(255)` | NO |  |  |  |  | Tên đầu việc |
| `note` | `TEXT` | YES |  |  |  |  | Ghi chú thêm cho đầu việc |
| `due_date` | `DATETIME` | YES |  |  | IDX: idx_action_items_status_due |  | Deadline ngày giờ của đầu việc |
| `status` | `ENUM('TODO','IN_PROGRESS','DONE','CANCELLED')` | NO | 'TODO' |  | IDX: idx_action_items_status_due | TODO, IN_PROGRESS, DONE, CANCELLED | TODO=chưa làm, IN_PROGRESS=đang làm, DONE=hoàn thành, CANCELLED=đã hủy/không cần làm nữa |
| `completed_at` | `DATETIME` | YES |  |  |  |  | Thời điểm hoàn thành; backend tự set khi status chuyển sang DONE |
| `display_order` | `INT UNSIGNED` | NO | 1 |  | IDX: idx_action_items_order |  | Thứ tự hiển thị trong biên bản |
| `created_at` | `DATETIME` | NO | CURRENT_TIMESTAMP |  | IDX: idx_action_items_created_by_time |  | Business/audit timestamp used for timeline, SLA, filtering, or history. |
| `created_by` | `BIGINT UNSIGNED` | YES |  |  | IDX: idx_action_items_created_by_time; FK: users(user_id) |  | User reference used for audit and accountability. |
| `updated_at` | `DATETIME` | YES | NULL ON UPDATE CURRENT_TIMESTAMP | ON UPDATE CURRENT_TIMESTAMP |  |  | Business/audit timestamp used for timeline, SLA, filtering, or history. |
| `updated_by` | `BIGINT UNSIGNED` | YES |  |  | FK: users(user_id) |  | User reference used for audit and accountability. |

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

### 5.24. `feedbacks`
**Purpose / Table Comment:** Feedback đơn giản: mỗi dòng là một đánh giá giữa hai user trong một visit. Khách/logistics đánh giá host; host đánh giá khách hoặc logistics.

**Main Screens / UC Area:** Delegation Feedback / Feedback Summary

**Column Count:** `14`

**Columns:**

| Column | Type | Null | Default | Extra | Key / FK / Index | Enum Values | Notes / Meaning |
|---|---|---:|---|---|---|---|---|
| `feedback_id` | `BIGINT UNSIGNED` | NO |  | AUTO_INCREMENT | PK |  | Identifier/reference field used to join or scope `feedbacks` records. |
| `visit_request_id` | `BIGINT UNSIGNED` | NO |  |  | IDX: idx_feedbacks_visit_request; FK: visit_requests(visit_request_id) |  | Identifier/reference field used to join or scope `feedbacks` records. |
| `visit_instance_id` | `BIGINT UNSIGNED` | YES |  |  | IDX: idx_feedbacks_visit_instance; FK: visit_request_campuses(visit_instance_id) |  | Identifier/reference field used to join or scope `feedbacks` records. |
| `submitted_by_user_id` | `BIGINT UNSIGNED` | NO |  |  | IDX: idx_feedbacks_submitter; FK: users(user_id) |  | User gửi feedback; khách/host/logistics đều phải có tài khoản hệ thống |
| `submitter_role` | `ENUM('VISITOR','HOST','LOGISTICS')` | NO |  |  | IDX: idx_feedbacks_roles | VISITOR, HOST, LOGISTICS | Vai trò người gửi trong chuyến thăm |
| `submitter_context` | `VARCHAR(120)` | NO | '' |  |  |  | Ngữ cảnh vai trò người gửi, ví dụ: Host chính, Xe điện, Teabreak, Khách đại diện |
| `submitter_name_snapshot` | `VARCHAR(255)` | NO |  |  |  |  | Tên người gửi tại thời điểm gửi feedback |
| `target_user_id` | `BIGINT UNSIGNED` | NO |  |  | IDX: idx_feedbacks_target; FK: users(user_id) |  | User được đánh giá |
| `target_role` | `ENUM('VISITOR','HOST','LOGISTICS')` | NO |  |  | IDX: idx_feedbacks_roles | VISITOR, HOST, LOGISTICS | Vai trò người được đánh giá trong chuyến thăm |
| `target_context` | `VARCHAR(120)` | NO | '' |  |  |  | Ngữ cảnh đối tượng được đánh giá, ví dụ: Host chính, Đoàn khách, Xe điện, Teabreak |
| `target_name_snapshot` | `VARCHAR(255)` | NO |  |  |  |  | Tên người được đánh giá tại thời điểm gửi feedback |
| `rating` | `TINYINT UNSIGNED` | NO |  |  | IDX: idx_feedbacks_rating |  | Số sao từ 1 đến 5 |
| `comment` | `TEXT` | NO |  |  |  |  | Nội dung feedback |
| `submitted_at` | `DATETIME` | NO | CURRENT_TIMESTAMP |  | IDX: idx_feedbacks_submitted_at |  | Business/audit timestamp used for timeline, SLA, filtering, or history. |

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

### 5.25. `feedback_rating_items`
**Purpose / Table Comment:** Normalized per-criterion ratings for a feedback submission.

**Main Screens / UC Area:** Feedback Rating Criteria / Analytics

**Column Count:** `7`

**Columns:**

| Column | Type | Null | Default | Extra | Key / FK / Index | Enum Values | Notes / Meaning |
|---|---|---:|---|---|---|---|---|
| `feedback_rating_item_id` | `BIGINT UNSIGNED` | NO |  | AUTO_INCREMENT | PK |  | Identifier/reference field used to join or scope `feedback_rating_items` records. |
| `feedback_id` | `BIGINT UNSIGNED` | NO |  |  | UNIQUE: uq_feedback_rating_criterion; IDX: idx_feedback_rating_feedback; FK: feedbacks(feedback_id) |  | Identifier/reference field used to join or scope `feedback_rating_items` records. |
| `criterion_code` | `VARCHAR(80)` | NO |  |  | UNIQUE: uq_feedback_rating_criterion |  | Field `criterion_code` used by `feedback_rating_items` business logic and screens. |
| `criterion_label` | `VARCHAR(150)` | NO |  |  |  |  | Field `criterion_label` used by `feedback_rating_items` business logic and screens. |
| `rating` | `TINYINT UNSIGNED` | NO |  |  |  |  | Field `rating` used by `feedback_rating_items` business logic and screens. |
| `display_order` | `INT UNSIGNED` | NO | 0 |  |  |  | Field `display_order` used by `feedback_rating_items` business logic and screens. |
| `created_at` | `DATETIME` | NO | CURRENT_TIMESTAMP |  |  |  | Business/audit timestamp used for timeline, SLA, filtering, or history. |

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

### 5.26. `news`
**Purpose / Table Comment:** News metadata. Người tham gia gửi bài, host duyệt/từ chối; nội dung chia theo section.

**Main Screens / UC Area:** News Management / Public News / Approval / Publish

**Column Count:** `17`

**Columns:**

| Column | Type | Null | Default | Extra | Key / FK / Index | Enum Values | Notes / Meaning |
|---|---|---:|---|---|---|---|---|
| `news_id` | `BIGINT UNSIGNED` | NO |  | AUTO_INCREMENT | PK |  | Identifier/reference field used to join or scope `news` records. |
| `campus_id` | `BIGINT UNSIGNED` | YES |  |  | IDX: idx_news_public; FK: campuses(campus_id) |  | Campus liên quan đến bài viết. NULL nếu bài toàn hệ thống |
| `visit_instance_id` | `BIGINT UNSIGNED` | YES |  |  | IDX: idx_news_visit_instance_status; FK: visit_request_campuses(visit_instance_id) |  | Visit instance liên quan nếu bài viết được tạo từ một chuyến tiếp đón |
| `author_user_id` | `BIGINT UNSIGNED` | NO |  |  | IDX: idx_news_author_status; FK: users(user_id) |  | Người tạo/viết bài |
| `cover_file_id` | `BIGINT UNSIGNED` | YES |  |  | FK: files(file_id) |  | Ảnh bìa bài viết, trỏ tới files.file_id |
| `status` | `ENUM('PENDING_REVIEW','REJECTED','PUBLISHED','HIDDEN')` | NO | 'PENDING_REVIEW' |  | IDX: idx_news_public; IDX: idx_news_author_status; IDX: idx_news_visit_instance_status; IDX: idx_news_featured | PENDING_REVIEW, REJECTED, PUBLISHED, HIDDEN | PENDING_REVIEW=chờ host duyệt, REJECTED=bị từ chối, PUBLISHED=đã đăng, HIDDEN=ẩn khỏi trang tin |
| `submitted_at` | `DATETIME` | NO | CURRENT_TIMESTAMP |  |  |  | Thời điểm người viết gửi bài cho host duyệt |
| `reviewed_by` | `BIGINT UNSIGNED` | YES |  |  | IDX: idx_news_review; FK: users(user_id) |  | Host duyệt hoặc từ chối bài viết |
| `reviewed_at` | `DATETIME` | YES |  |  | IDX: idx_news_review |  | Thời điểm host duyệt hoặc từ chối |
| `review_note` | `TEXT` | YES |  |  |  |  | Ghi chú duyệt hoặc lý do từ chối |
| `published_at` | `DATETIME` | YES |  |  | IDX: idx_news_public; IDX: idx_news_featured |  | Thời điểm bài viết được đăng |
| `is_featured` | `BOOLEAN` | NO | FALSE |  | IDX: idx_news_featured |  | Bài viết nổi bật |
| `row_version` | `INT UNSIGNED` | NO | 0 |  |  |  | Optimistic concurrency token, chống ghi đè khi cập nhật đồng thời |
| `created_at` | `DATETIME` | NO | CURRENT_TIMESTAMP |  |  |  | Business/audit timestamp used for timeline, SLA, filtering, or history. |
| `created_by` | `BIGINT UNSIGNED` | YES |  |  |  |  | User reference used for audit and accountability. |
| `updated_at` | `DATETIME` | YES | NULL ON UPDATE CURRENT_TIMESTAMP | ON UPDATE CURRENT_TIMESTAMP |  |  | Business/audit timestamp used for timeline, SLA, filtering, or history. |
| `updated_by` | `BIGINT UNSIGNED` | YES |  |  |  |  | User reference used for audit and accountability. |

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

### 5.27. `news_translations`
**Purpose / Table Comment:** Tiêu đề, slug, tóm tắt và SEO của bài viết theo ngôn ngữ

**Main Screens / UC Area:** Multilingual News / AI Translation

**Column Count:** `10`

**Columns:**

| Column | Type | Null | Default | Extra | Key / FK / Index | Enum Values | Notes / Meaning |
|---|---|---:|---|---|---|---|---|
| `news_translation_id` | `BIGINT UNSIGNED` | NO |  | AUTO_INCREMENT | PK |  | Identifier/reference field used to join or scope `news_translations` records. |
| `news_id` | `BIGINT UNSIGNED` | NO |  |  | UNIQUE: uq_news_translation_lang; FK: news(news_id) |  | Identifier/reference field used to join or scope `news_translations` records. |
| `language_code` | `VARCHAR(20)` | NO | 'vi' |  | UNIQUE: uq_news_translation_lang; UNIQUE: uq_news_translation_slug_lang; IDX: idx_news_translations_lang |  | Field `language_code` used by `news_translations` business logic and screens. |
| `title` | `VARCHAR(255)` | NO |  |  | IDX: ft_news_translations_search |  | Tiêu đề chính của bài viết |
| `slug` | `VARCHAR(255)` | NO |  |  | UNIQUE: uq_news_translation_slug_lang |  | Đường dẫn SEO của bài viết |
| `summary` | `TEXT` | YES |  |  | IDX: ft_news_translations_search |  | Tóm tắt bài viết |
| `seo_title` | `VARCHAR(255)` | YES |  |  |  |  | Field `seo_title` used by `news_translations` business logic and screens. |
| `seo_description` | `VARCHAR(500)` | YES |  |  |  |  | Field `seo_description` used by `news_translations` business logic and screens. |
| `created_at` | `DATETIME` | NO | CURRENT_TIMESTAMP |  |  |  | Business/audit timestamp used for timeline, SLA, filtering, or history. |
| `updated_at` | `DATETIME` | YES | NULL ON UPDATE CURRENT_TIMESTAMP | ON UPDATE CURRENT_TIMESTAMP |  |  | Business/audit timestamp used for timeline, SLA, filtering, or history. |

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

### 5.28. `news_content_sections`
**Purpose / Table Comment:** Các khối nội dung chi tiết của bài viết, tối đa 10 section mỗi bản dịch

**Main Screens / UC Area:** News Editor / Content Sections

**Column Count:** `8`

**Columns:**

| Column | Type | Null | Default | Extra | Key / FK / Index | Enum Values | Notes / Meaning |
|---|---|---:|---|---|---|---|---|
| `section_id` | `BIGINT UNSIGNED` | NO |  | AUTO_INCREMENT | PK |  | Identifier/reference field used to join or scope `news_content_sections` records. |
| `news_translation_id` | `BIGINT UNSIGNED` | NO |  |  | UNIQUE: uq_news_section_order; IDX: idx_news_sections_translation; FK: news_translations(news_translation_id) |  | Identifier/reference field used to join or scope `news_content_sections` records. |
| `section_order` | `TINYINT UNSIGNED` | NO |  |  | UNIQUE: uq_news_section_order |  | Thứ tự section, từ 1 đến 10 |
| `section_title` | `VARCHAR(255)` | NO |  |  | IDX: ft_news_sections_search |  | Tiêu đề section |
| `section_body_html` | `LONGTEXT` | NO |  |  |  |  | Nội dung rich text dạng HTML đã sanitize, có thể chứa paragraph, bold, italic, color, link, image |
| `section_body_text` | `TEXT` | YES |  |  | IDX: ft_news_sections_search |  | Plain text tách từ HTML để search hoặc preview |
| `created_at` | `DATETIME` | NO | CURRENT_TIMESTAMP |  |  |  | Business/audit timestamp used for timeline, SLA, filtering, or history. |
| `updated_at` | `DATETIME` | YES | NULL ON UPDATE CURRENT_TIMESTAMP | ON UPDATE CURRENT_TIMESTAMP |  |  | Business/audit timestamp used for timeline, SLA, filtering, or history. |

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

### 5.29. `news_section_files`
**Purpose / Table Comment:** File/ảnh được dùng trong từng section của bài news

**Main Screens / UC Area:** News Media Attachments

**Column Count:** `6`

**Columns:**

| Column | Type | Null | Default | Extra | Key / FK / Index | Enum Values | Notes / Meaning |
|---|---|---:|---|---|---|---|---|
| `section_file_id` | `BIGINT UNSIGNED` | NO |  | AUTO_INCREMENT | PK |  | Identifier/reference field used to join or scope `news_section_files` records. |
| `section_id` | `BIGINT UNSIGNED` | NO |  |  | UNIQUE: uq_news_section_file; IDX: idx_news_section_files_section; FK: news_content_sections(section_id) |  | Identifier/reference field used to join or scope `news_section_files` records. |
| `file_id` | `BIGINT UNSIGNED` | NO |  |  | UNIQUE: uq_news_section_file; IDX: idx_news_section_files_file; FK: files(file_id) |  | Identifier/reference field used to join or scope `news_section_files` records. |
| `usage_type` | `ENUM('INLINE_IMAGE','ATTACHMENT')` | NO | 'INLINE_IMAGE' |  |  | INLINE_IMAGE, ATTACHMENT | INLINE_IMAGE=ảnh chèn trong nội dung, ATTACHMENT=file đính kèm |
| `display_order` | `INT UNSIGNED` | NO | 0 |  |  |  | Field `display_order` used by `news_section_files` business logic and screens. |
| `created_at` | `DATETIME` | NO | CURRENT_TIMESTAMP |  |  |  | Business/audit timestamp used for timeline, SLA, filtering, or history. |

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

### 5.30. `faqs`
**Purpose / Table Comment:** FAQ tiếng Việt theo nhóm chức năng hệ thống PEMS

**Main Screens / UC Area:** Vietnamese FAQ Management / Public FAQ

**Column Count:** `10`

**Columns:**

| Column | Type | Null | Default | Extra | Key / FK / Index | Enum Values | Notes / Meaning |
|---|---|---:|---|---|---|---|---|
| `faq_id` | `BIGINT UNSIGNED` | NO |  | AUTO_INCREMENT | PK |  | Identifier/reference field used to join or scope `faqs` records. |
| `faq_type` | `ENUM(<br>    'ACCOUNT_ACCESS',<br>    'VISIT_REQUEST',<br>    'DELEGATION_MANAGEMENT',<br>    'LOGISTICS_RESOURCE',<br>    'DOCUMENT_MEDIA',<br>    'NOTIFICATION_EMAIL',<br>    'OTHER'<br>  )` | NO | 'OTHER' |  | IDX: idx_faqs_type_status | ACCOUNT_ACCESS, VISIT_REQUEST, DELEGATION_MANAGEMENT, LOGISTICS_RESOURCE, DOCUMENT_MEDIA, NOTIFICATION_EMAIL, OTHER | Loại FAQ theo nhóm chức năng hệ thống PEMS |
| `question` | `VARCHAR(500)` | NO |  |  | IDX: ft_faqs_search |  | Câu hỏi FAQ |
| `answer` | `TEXT` | NO |  |  | IDX: ft_faqs_search |  | Câu trả lời FAQ |
| `display_order` | `INT UNSIGNED` | NO | 0 |  | IDX: idx_faqs_status_order |  | Field `display_order` used by `faqs` business logic and screens. |
| `status` | `ENUM('PUBLISHED','HIDDEN')` | NO | 'HIDDEN' |  | IDX: idx_faqs_status_order; IDX: idx_faqs_type_status | PUBLISHED, HIDDEN | PUBLISHED=hiển thị trên trang FAQ, HIDDEN=ẩn khỏi người xem thường nhưng người quản lý vẫn thấy |
| `created_at` | `DATETIME` | NO | CURRENT_TIMESTAMP |  |  |  | Business/audit timestamp used for timeline, SLA, filtering, or history. |
| `created_by` | `BIGINT UNSIGNED` | YES |  |  |  |  | User reference used for audit and accountability. |
| `updated_at` | `DATETIME` | YES | NULL ON UPDATE CURRENT_TIMESTAMP | ON UPDATE CURRENT_TIMESTAMP |  |  | Business/audit timestamp used for timeline, SLA, filtering, or history. |
| `updated_by` | `BIGINT UNSIGNED` | YES |  |  |  |  | User reference used for audit and accountability. |

**Primary Key:**
- `PRIMARY KEY (faq_id)`

**Indexes:**
- `KEY idx_faqs_status_order (status, display_order)`
- `KEY idx_faqs_type_status (faq_type, status)`
- `FULLTEXT KEY ft_faqs_search (question, answer)`

**V10 Usage Notes:**
- FAQ is Vietnamese-only in v10. Do not expose a language selector. Public page must query `status = PUBLISHED`; admin screens may manage `PUBLISHED/HIDDEN` and filter by the simplified system-related `faq_type`.

### 5.31. `galleries`
**Purpose / Table Comment:** Gallery địa điểm trong campus, có mô tả và câu chuyện

**Main Screens / UC Area:** Gallery Management / Public Gallery

**Column Count:** `18`

**Columns:**

| Column | Type | Null | Default | Extra | Key / FK / Index | Enum Values | Notes / Meaning |
|---|---|---:|---|---|---|---|---|
| `gallery_id` | `BIGINT UNSIGNED` | NO |  | AUTO_INCREMENT | PK |  | Identifier/reference field used to join or scope `galleries` records. |
| `campus_id` | `BIGINT UNSIGNED` | NO |  |  | IDX: idx_galleries_campus_status; IDX: idx_galleries_area_specific; FK: campuses(campus_id) |  | Identifier/reference field used to join or scope `galleries` records. |
| `area_name` | `VARCHAR(150)` | NO | 'Campus' |  | IDX: idx_galleries_area_specific |  | Khu vực trong campus, ví dụ: Academic Area, Lobby, Lab Zone |
| `specific_location_name` | `VARCHAR(150)` | NO | 'Campus location' |  | IDX: idx_galleries_area_specific |  | Vị trí cụ thể trong khu vực, ví dụ: Sảnh Alpha, Green Lab |
| `location_description` | `TEXT` | YES |  |  |  |  | Mô tả vị trí/khu vực hiển thị ở Gallery/Visit FPTU |
| `title` | `VARCHAR(255)` | NO |  |  |  |  | Tên hiển thị của gallery/địa điểm |
| `description` | `TEXT` | YES |  |  |  |  | Mô tả ngắn về địa điểm |
| `story_content` | `TEXT` | YES |  |  |  |  | Ý nghĩa hoặc câu chuyện giới thiệu về địa điểm |
| `status` | `ENUM('PUBLISHED','HIDDEN')` | NO | 'HIDDEN' |  | IDX: idx_galleries_campus_status; IDX: idx_galleries_visibility_status | PUBLISHED, HIDDEN | PUBLISHED=hiển thị theo visibility, HIDDEN=ẩn khỏi public/người xem thường nhưng Staff Leader vẫn quản lý được |
| `visibility` | `ENUM('PRIVATE','INTERNAL','PUBLIC')` | NO | 'INTERNAL' |  | IDX: idx_galleries_visibility_status | PRIVATE, INTERNAL, PUBLIC | Phạm vi xem khi status=PUBLISHED: PRIVATE=chỉ quản lý, INTERNAL=user nội bộ, PUBLIC=công khai |
| `hero_file_id` | `BIGINT UNSIGNED` | YES |  |  | IDX: idx_galleries_hero_file; FK: files(file_id) |  | Identifier/reference field used to join or scope `galleries` records. |
| `virtual_tour_url` | `VARCHAR(700)` | YES |  |  |  |  | Field `virtual_tour_url` used by `galleries` business logic and screens. |
| `created_at` | `DATETIME` | NO | CURRENT_TIMESTAMP |  |  |  | Business/audit timestamp used for timeline, SLA, filtering, or history. |
| `created_by` | `BIGINT UNSIGNED` | YES |  |  |  |  | User reference used for audit and accountability. |
| `updated_at` | `DATETIME` | YES | NULL ON UPDATE CURRENT_TIMESTAMP | ON UPDATE CURRENT_TIMESTAMP |  |  | Business/audit timestamp used for timeline, SLA, filtering, or history. |
| `updated_by` | `BIGINT UNSIGNED` | YES |  |  |  |  | User reference used for audit and accountability. |
| `deleted_at` | `DATETIME` | YES |  |  | IDX: idx_galleries_campus_status |  | Business/audit timestamp used for timeline, SLA, filtering, or history. |
| `deleted_by` | `BIGINT UNSIGNED` | YES |  |  |  |  | User reference used for audit and accountability. |

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

### 5.32. `gallery_images`
**Purpose / Table Comment:** Ảnh thuộc gallery địa điểm campus

**Main Screens / UC Area:** Gallery Item Management / Public Gallery

**Column Count:** `15`

**Columns:**

| Column | Type | Null | Default | Extra | Key / FK / Index | Enum Values | Notes / Meaning |
|---|---|---:|---|---|---|---|---|
| `image_id` | `BIGINT UNSIGNED` | NO |  | AUTO_INCREMENT | PK |  | Identifier/reference field used to join or scope `gallery_images` records. |
| `gallery_id` | `BIGINT UNSIGNED` | NO |  |  | IDX: idx_gallery_images_gallery_order; FK: galleries(gallery_id) |  | Identifier/reference field used to join or scope `gallery_images` records. |
| `file_id` | `BIGINT UNSIGNED` | NO |  |  | UNIQUE: uq_gallery_images_file; FK: files(file_id) |  | Identifier/reference field used to join or scope `gallery_images` records. |
| `media_type` | `ENUM('IMAGE','VIDEO')` | NO | 'IMAGE' |  | IDX: idx_gallery_images_media_type | IMAGE, VIDEO | Field `media_type` used by `gallery_images` business logic and screens. |
| `thumbnail_file_id` | `BIGINT UNSIGNED` | YES |  |  | IDX: idx_gallery_images_thumbnail_file; FK: files(file_id) |  | Identifier/reference field used to join or scope `gallery_images` records. |
| `caption` | `VARCHAR(500)` | YES |  |  |  |  | Chú thích riêng cho từng ảnh |
| `display_order` | `INT UNSIGNED` | NO | 0 |  | IDX: idx_gallery_images_gallery_order |  | Field `display_order` used by `gallery_images` business logic and screens. |
| `taken_at` | `DATETIME` | YES |  |  | IDX: idx_gallery_images_status_time |  | Business/audit timestamp used for timeline, SLA, filtering, or history. |
| `status` | `ENUM('ACTIVE','HIDDEN')` | NO | 'ACTIVE' |  | IDX: idx_gallery_images_status_time | ACTIVE, HIDDEN | ACTIVE=ảnh đang dùng, HIDDEN=ảnh bị ẩn khỏi gallery thường |
| `created_at` | `DATETIME` | NO | CURRENT_TIMESTAMP |  |  |  | Business/audit timestamp used for timeline, SLA, filtering, or history. |
| `created_by` | `BIGINT UNSIGNED` | YES |  |  |  |  | User reference used for audit and accountability. |
| `updated_at` | `DATETIME` | YES | NULL ON UPDATE CURRENT_TIMESTAMP | ON UPDATE CURRENT_TIMESTAMP |  |  | Business/audit timestamp used for timeline, SLA, filtering, or history. |
| `updated_by` | `BIGINT UNSIGNED` | YES |  |  |  |  | User reference used for audit and accountability. |
| `deleted_at` | `DATETIME` | YES |  |  |  |  | Business/audit timestamp used for timeline, SLA, filtering, or history. |
| `deleted_by` | `BIGINT UNSIGNED` | YES |  |  |  |  | User reference used for audit and accountability. |

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

### 5.33. `photo_face_tags`
**Purpose / Table Comment:** Confirmed face tag metadata only. No biometric vector.

**Main Screens / UC Area:** Photo Tagging / Gallery Moderation

**Column Count:** `17`

**Columns:**

| Column | Type | Null | Default | Extra | Key / FK / Index | Enum Values | Notes / Meaning |
|---|---|---:|---|---|---|---|---|
| `face_tag_id` | `BIGINT UNSIGNED` | NO |  | AUTO_INCREMENT | PK |  | Identifier/reference field used to join or scope `photo_face_tags` records. |
| `image_id` | `BIGINT UNSIGNED` | NO |  |  | IDX: idx_face_tags_image; FK: gallery_images(image_id) |  | Identifier/reference field used to join or scope `photo_face_tags` records. |
| `visit_request_id` | `BIGINT UNSIGNED` | YES |  |  | FK: visit_requests(visit_request_id) |  | Identifier/reference field used to join or scope `photo_face_tags` records. |
| `guest_member_id` | `BIGINT UNSIGNED` | YES |  |  | IDX: idx_face_tags_guest; FK: visit_guest_members(guest_member_id) |  | Identifier/reference field used to join or scope `photo_face_tags` records. |
| `partner_contact_id` | `BIGINT UNSIGNED` | YES |  |  | IDX: idx_face_tags_partner_contact; FK: partner_contacts(contact_id) |  | Identifier/reference field used to join or scope `photo_face_tags` records. |
| `display_name` | `VARCHAR(150)` | NO |  |  |  |  | Field `display_name` used by `photo_face_tags` business logic and screens. |
| `bounding_box_x` | `DECIMAL(8,4)` | YES |  |  |  |  | Field `bounding_box_x` used by `photo_face_tags` business logic and screens. |
| `bounding_box_y` | `DECIMAL(8,4)` | YES |  |  |  |  | Field `bounding_box_y` used by `photo_face_tags` business logic and screens. |
| `bounding_box_width` | `DECIMAL(8,4)` | YES |  |  |  |  | Field `bounding_box_width` used by `photo_face_tags` business logic and screens. |
| `bounding_box_height` | `DECIMAL(8,4)` | YES |  |  |  |  | Field `bounding_box_height` used by `photo_face_tags` business logic and screens. |
| `tag_status` | `ENUM('MANUALLY_TAGGED','CONFIRMED','REMOVED')` | NO | 'MANUALLY_TAGGED' |  | IDX: idx_face_tags_status | MANUALLY_TAGGED, CONFIRMED, REMOVED | Status field used for workflow state, filtering and UI badges. |
| `confirmed_by` | `BIGINT UNSIGNED` | YES |  |  | FK: users(user_id) |  | User reference used for audit and accountability. |
| `confirmed_at` | `DATETIME` | YES |  |  |  |  | Business/audit timestamp used for timeline, SLA, filtering, or history. |
| `created_at` | `DATETIME` | NO | CURRENT_TIMESTAMP |  |  |  | Business/audit timestamp used for timeline, SLA, filtering, or history. |
| `created_by` | `BIGINT UNSIGNED` | YES |  |  |  |  | User reference used for audit and accountability. |
| `removed_at` | `DATETIME` | YES |  |  |  |  | Business/audit timestamp used for timeline, SLA, filtering, or history. |
| `removed_by` | `BIGINT UNSIGNED` | YES |  |  |  |  | User reference used for audit and accountability. |

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

### 5.34. `email_templates`
**Purpose / Table Comment:** Email templates with explicit VI/EN subject/body fields

**Main Screens / UC Area:** Email Template Management

**Column Count:** `16`

**Columns:**

| Column | Type | Null | Default | Extra | Key / FK / Index | Enum Values | Notes / Meaning |
|---|---|---:|---|---|---|---|---|
| `email_template_id` | `BIGINT UNSIGNED` | NO |  | AUTO_INCREMENT | PK |  | Identifier/reference field used to join or scope `email_templates` records. |
| `template_code` | `VARCHAR(100)` | NO |  |  | UNIQUE: uq_email_templates_code |  | Field `template_code` used by `email_templates` business logic and screens. |
| `name` | `VARCHAR(150)` | NO |  |  |  |  | Field `name` used by `email_templates` business logic and screens. |
| `purpose` | `VARCHAR(100)` | NO |  |  | IDX: idx_email_templates_purpose_status |  | Field `purpose` used by `email_templates` business logic and screens. |
| `campus_id` | `BIGINT UNSIGNED` | YES |  |  | IDX: idx_email_templates_campus_status; FK: campuses(campus_id) |  | Identifier/reference field used to join or scope `email_templates` records. |
| `description` | `VARCHAR(500)` | YES |  |  |  |  | Field `description` used by `email_templates` business logic and screens. |
| `status` | `ENUM('ACTIVE','INACTIVE')` | NO | 'ACTIVE' |  | IDX: idx_email_templates_status; IDX: idx_email_templates_purpose_status; IDX: idx_email_templates_campus_status | ACTIVE, INACTIVE | Status field used for workflow state, filtering and UI badges. |
| `subject_vi` | `VARCHAR(255)` | YES |  |  |  |  | Field `subject_vi` used by `email_templates` business logic and screens. |
| `body_vi` | `LONGTEXT` | YES |  |  |  |  | Field `body_vi` used by `email_templates` business logic and screens. |
| `subject_en` | `VARCHAR(255)` | YES |  |  |  |  | Field `subject_en` used by `email_templates` business logic and screens. |
| `body_en` | `LONGTEXT` | YES |  |  |  |  | Field `body_en` used by `email_templates` business logic and screens. |
| `variables_text` | `VARCHAR(700)` | YES |  |  |  |  | Field `variables_text` used by `email_templates` business logic and screens. |
| `created_at` | `DATETIME` | NO | CURRENT_TIMESTAMP |  |  |  | Business/audit timestamp used for timeline, SLA, filtering, or history. |
| `created_by` | `BIGINT UNSIGNED` | YES |  |  |  |  | User reference used for audit and accountability. |
| `updated_at` | `DATETIME` | YES | NULL ON UPDATE CURRENT_TIMESTAMP | ON UPDATE CURRENT_TIMESTAMP |  |  | Business/audit timestamp used for timeline, SLA, filtering, or history. |
| `updated_by` | `BIGINT UNSIGNED` | YES |  |  |  |  | User reference used for audit and accountability. |

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

### 5.35. `sent_emails`
**Purpose / Table Comment:** Sent email log; recipients stored in sent_email_recipients

**Main Screens / UC Area:** Email Outbox / Delivery Tracking

**Column Count:** `16`

**Columns:**

| Column | Type | Null | Default | Extra | Key / FK / Index | Enum Values | Notes / Meaning |
|---|---|---:|---|---|---|---|---|
| `sent_email_id` | `BIGINT UNSIGNED` | NO |  | AUTO_INCREMENT | PK |  | Identifier/reference field used to join or scope `sent_emails` records. |
| `email_template_id` | `BIGINT UNSIGNED` | YES |  |  | IDX: idx_sent_emails_template; FK: email_templates(email_template_id) |  | Identifier/reference field used to join or scope `sent_emails` records. |
| `related_type` | `VARCHAR(80)` | YES |  |  | IDX: idx_sent_emails_related |  | Field `related_type` used by `sent_emails` business logic and screens. |
| `related_id` | `BIGINT UNSIGNED` | YES |  |  | IDX: idx_sent_emails_related |  | Identifier/reference field used to join or scope `sent_emails` records. |
| `subject` | `VARCHAR(255)` | NO |  |  |  |  | Field `subject` used by `sent_emails` business logic and screens. |
| `body_snapshot` | `LONGTEXT` | YES |  |  |  |  | Field `body_snapshot` used by `sent_emails` business logic and screens. |
| `provider_thread_id` | `VARCHAR(255)` | YES |  |  | IDX: idx_sent_emails_provider_thread |  | Identifier/reference field used to join or scope `sent_emails` records. |
| `provider_message_id` | `VARCHAR(255)` | YES |  |  | IDX: idx_sent_emails_provider_message |  | Identifier/reference field used to join or scope `sent_emails` records. |
| `retry_count` | `INT UNSIGNED` | NO | 0 |  |  |  | Field `retry_count` used by `sent_emails` business logic and screens. |
| `last_attempt_at` | `DATETIME` | YES |  |  |  |  | Business/audit timestamp used for timeline, SLA, filtering, or history. |
| `delivered_at` | `DATETIME` | YES |  |  |  |  | Business/audit timestamp used for timeline, SLA, filtering, or history. |
| `status` | `ENUM('QUEUED','SENT','FAILED')` | NO | 'QUEUED' |  | IDX: idx_sent_emails_status_time | QUEUED, SENT, FAILED | Status field used for workflow state, filtering and UI badges. |
| `error_message` | `TEXT` | YES |  |  |  |  | Field `error_message` used by `sent_emails` business logic and screens. |
| `sent_by` | `BIGINT UNSIGNED` | YES |  |  | IDX: idx_sent_emails_sent_by_time; FK: users(user_id) |  | User reference used for audit and accountability. |
| `sent_at` | `DATETIME` | YES |  |  | IDX: idx_sent_emails_sent_by_time |  | Business/audit timestamp used for timeline, SLA, filtering, or history. |
| `created_at` | `DATETIME` | NO | CURRENT_TIMESTAMP |  | IDX: idx_sent_emails_status_time |  | Business/audit timestamp used for timeline, SLA, filtering, or history. |

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

**V10 Usage Notes:**
- Stores outbound email snapshot and provider IDs only. It is not an inbox table.

### 5.36. `sent_email_recipients`
**Purpose / Table Comment:** One row per recipient; replaces sent_emails.recipients_json.

**Main Screens / UC Area:** Email Delivery Tracking per Recipient

**Column Count:** `11`

**Columns:**

| Column | Type | Null | Default | Extra | Key / FK / Index | Enum Values | Notes / Meaning |
|---|---|---:|---|---|---|---|---|
| `sent_email_recipient_id` | `BIGINT UNSIGNED` | NO |  | AUTO_INCREMENT | PK |  | Identifier/reference field used to join or scope `sent_email_recipients` records. |
| `sent_email_id` | `BIGINT UNSIGNED` | NO |  |  | IDX: idx_sent_email_recipients_sent_email; FK: sent_emails(sent_email_id) |  | Identifier/reference field used to join or scope `sent_email_recipients` records. |
| `recipient_email` | `VARCHAR(150)` | NO |  |  | IDX: idx_sent_email_recipients_email_status; IDX: ft_sent_email_recipients_search |  | Field `recipient_email` used by `sent_email_recipients` business logic and screens. |
| `recipient_name` | `VARCHAR(150)` | YES |  |  | IDX: ft_sent_email_recipients_search |  | Field `recipient_name` used by `sent_email_recipients` business logic and screens. |
| `recipient_type` | `ENUM('TO','CC','BCC')` | NO | 'TO' |  |  | TO, CC, BCC | Field `recipient_type` used by `sent_email_recipients` business logic and screens. |
| `delivery_status` | `ENUM('QUEUED','SENT','DELIVERED','FAILED','BOUNCED')` | NO | 'QUEUED' |  | IDX: idx_sent_email_recipients_email_status | QUEUED, SENT, DELIVERED, FAILED, BOUNCED | Status field used for workflow state, filtering and UI badges. |
| `provider_message_id` | `VARCHAR(255)` | YES |  |  |  |  | Identifier/reference field used to join or scope `sent_email_recipients` records. |
| `error_message` | `TEXT` | YES |  |  |  |  | Field `error_message` used by `sent_email_recipients` business logic and screens. |
| `sent_at` | `DATETIME` | YES |  |  |  |  | Business/audit timestamp used for timeline, SLA, filtering, or history. |
| `delivered_at` | `DATETIME` | YES |  |  |  |  | Business/audit timestamp used for timeline, SLA, filtering, or history. |
| `created_at` | `DATETIME` | NO | CURRENT_TIMESTAMP |  |  |  | Business/audit timestamp used for timeline, SLA, filtering, or history. |

**Primary Key:**
- `PRIMARY KEY (sent_email_recipient_id)`

**Indexes:**
- `KEY idx_sent_email_recipients_sent_email (sent_email_id)`
- `KEY idx_sent_email_recipients_email_status (recipient_email, delivery_status)`
- `FULLTEXT KEY ft_sent_email_recipients_search (recipient_email, recipient_name)`

**Foreign Keys:**
- `CONSTRAINT fk_sent_email_recipients_email FOREIGN KEY (sent_email_id) REFERENCES sent_emails(sent_email_id) ON UPDATE CASCADE ON DELETE CASCADE`

**V10 Usage Notes:**
- Stores per-recipient delivery tracking for outbound emails. Combine with `email_action_tokens` to show button-response status.

### 5.37. `email_action_tokens`
**Purpose / Table Comment:** One-time action tokens for email buttons: accept, decline, negotiate, handover signature.

**Main Screens / UC Area:** One-time Email Button Actions / Public Token Responses

**Column Count:** `19`

**Columns:**

| Column | Type | Null | Default | Extra | Key / FK / Index | Enum Values | Notes / Meaning |
|---|---|---:|---|---|---|---|---|
| `email_action_token_id` | `BIGINT UNSIGNED` | NO |  | AUTO_INCREMENT | PK |  | Identifier/reference field used to join or scope `email_action_tokens` records. |
| `token_hash` | `VARCHAR(255)` | NO |  |  | UNIQUE: uq_email_action_token_hash |  | Hash của token trong link email; không lưu token raw |
| `action_group_key` | `VARCHAR(180)` | NO |  |  | IDX: idx_email_action_group_used |  | Nhóm các nút cùng một quyết định, ví dụ PARTICIPATION:123:user@example.com |
| `action_context` | `ENUM(<br>    'PARTICIPATION_RESPONSE',<br>    'LOGISTICS_ASSIGNEE_RESPONSE',<br>    'LOGISTICS_NEGOTIATION',<br>    'LOGISTICS_PROPOSAL_RESPONSE',<br>    'LOGISTICS_HANDOVER_SIGNATURE'<br>  )` | NO |  |  | IDX: idx_email_action_context_status | PARTICIPATION_RESPONSE, LOGISTICS_ASSIGNEE_RESPONSE, LOGISTICS_NEGOTIATION, LOGISTICS_PROPOSAL_RESPONSE, LOGISTICS_HANDOVER_SIGNATURE | Field `action_context` used by `email_action_tokens` business logic and screens. |
| `target_type` | `ENUM(<br>    'VISIT_PARTICIPANT',<br>    'LOGISTICS_ITEM',<br>    'LOGISTICS_HANDOVER'<br>  )` | NO |  |  | IDX: idx_email_action_target | VISIT_PARTICIPANT, LOGISTICS_ITEM, LOGISTICS_HANDOVER | Field `target_type` used by `email_action_tokens` business logic and screens. |
| `target_id` | `BIGINT UNSIGNED` | NO |  |  | IDX: idx_email_action_target |  | Identifier/reference field used to join or scope `email_action_tokens` records. |
| `intended_action` | `ENUM(<br>    'ACCEPT',<br>    'DECLINE',<br>    'NEGOTIATE',<br>    'APPROVE_PROPOSAL',<br>    'REJECT_PROPOSAL',<br>    'CONFIRM_BORROW',<br>    'CONFIRM_RETURN'<br>  )` | NO |  |  |  | ACCEPT, DECLINE, NEGOTIATE, APPROVE_PROPOSAL, REJECT_PROPOSAL, CONFIRM_BORROW, CONFIRM_RETURN | Field `intended_action` used by `email_action_tokens` business logic and screens. |
| `recipient_user_id` | `BIGINT UNSIGNED` | YES |  |  | IDX: idx_email_action_recipient_user; FK: users(user_id) |  | Identifier/reference field used to join or scope `email_action_tokens` records. |
| `recipient_email` | `VARCHAR(150)` | NO |  |  | IDX: idx_email_action_recipient |  | Field `recipient_email` used by `email_action_tokens` business logic and screens. |
| `sent_email_id` | `BIGINT UNSIGNED` | YES |  |  | IDX: idx_email_action_sent_email; FK: sent_emails(sent_email_id) |  | Identifier/reference field used to join or scope `email_action_tokens` records. |
| `sent_email_recipient_id` | `BIGINT UNSIGNED` | YES |  |  | IDX: idx_email_action_sent_recipient; FK: sent_email_recipients(sent_email_recipient_id) |  | Identifier/reference field used to join or scope `email_action_tokens` records. |
| `expires_at` | `DATETIME` | NO |  |  | IDX: idx_email_action_expires |  | Business/audit timestamp used for timeline, SLA, filtering, or history. |
| `used_at` | `DATETIME` | YES |  |  | IDX: idx_email_action_group_used |  | Business/audit timestamp used for timeline, SLA, filtering, or history. |
| `used_action` | `VARCHAR(50)` | YES |  |  |  |  | Field `used_action` used by `email_action_tokens` business logic and screens. |
| `result_status` | `ENUM(<br>    'PENDING',<br>    'SUCCESS',<br>    'ALREADY_RESPONDED',<br>    'EXPIRED',<br>    'INVALID',<br>    'FAILED'<br>  )` | NO | 'PENDING' |  | IDX: idx_email_action_context_status | PENDING, SUCCESS, ALREADY_RESPONDED, EXPIRED, INVALID, FAILED | Status field used for workflow state, filtering and UI badges. |
| `result_message` | `VARCHAR(500)` | YES |  |  |  |  | Field `result_message` used by `email_action_tokens` business logic and screens. |
| `used_ip` | `VARCHAR(45)` | YES |  |  |  |  | Field `used_ip` used by `email_action_tokens` business logic and screens. |
| `used_user_agent` | `VARCHAR(500)` | YES |  |  |  |  | Field `used_user_agent` used by `email_action_tokens` business logic and screens. |
| `created_at` | `DATETIME` | NO | CURRENT_TIMESTAMP |  |  |  | Business/audit timestamp used for timeline, SLA, filtering, or history. |

**Primary Key:**
- `PRIMARY KEY (email_action_token_id)`

**Unique Constraints:**
- `UNIQUE KEY uq_email_action_token_hash (token_hash)`

**Indexes:**
- `KEY idx_email_action_group_used (action_group_key, used_at)`
- `KEY idx_email_action_target (target_type, target_id)`
- `KEY idx_email_action_recipient (recipient_email)`
- `KEY idx_email_action_context_status (action_context, result_status)`
- `KEY idx_email_action_expires (expires_at)`
- `KEY idx_email_action_sent_email (sent_email_id)`
- `KEY idx_email_action_sent_recipient (sent_email_recipient_id)`
- `KEY idx_email_action_recipient_user (recipient_user_id)`

**Foreign Keys:**
- `CONSTRAINT fk_email_action_recipient_user FOREIGN KEY (recipient_user_id) REFERENCES users(user_id) ON UPDATE CASCADE ON DELETE SET NULL`
- `CONSTRAINT fk_email_action_sent_email FOREIGN KEY (sent_email_id) REFERENCES sent_emails(sent_email_id) ON UPDATE CASCADE ON DELETE SET NULL`
- `CONSTRAINT fk_email_action_sent_recipient FOREIGN KEY (sent_email_recipient_id) REFERENCES sent_email_recipients(sent_email_recipient_id) ON UPDATE CASCADE ON DELETE SET NULL`

**V10 Usage Notes:**
- Used for one-time email buttons without logging in: accept/decline participation, accept/decline logistics, negotiate, approve/reject proposal, confirm borrow/return. Store only `token_hash`, never raw token. Backend validates `target_type + target_id` because this is a polymorphic reference. If a user clicks another button after responding, return ALREADY_RESPONDED and do not update business state again.

### 5.38. `notifications`
**Purpose / Table Comment:** In-app notifications

**Main Screens / UC Area:** Notification Center / Dashboard Alerts

**Column Count:** `10`

**Columns:**

| Column | Type | Null | Default | Extra | Key / FK / Index | Enum Values | Notes / Meaning |
|---|---|---:|---|---|---|---|---|
| `notification_id` | `BIGINT UNSIGNED` | NO |  | AUTO_INCREMENT | PK |  | Identifier/reference field used to join or scope `notifications` records. |
| `recipient_user_id` | `BIGINT UNSIGNED` | NO |  |  | IDX: idx_notifications_user_read_time; FK: users(user_id) |  | Identifier/reference field used to join or scope `notifications` records. |
| `title` | `VARCHAR(255)` | NO |  |  |  |  | Field `title` used by `notifications` business logic and screens. |
| `message` | `TEXT` | YES |  |  |  |  | Field `message` used by `notifications` business logic and screens. |
| `notification_type` | `VARCHAR(80)` | NO |  |  | IDX: idx_notifications_type_time |  | Field `notification_type` used by `notifications` business logic and screens. |
| `related_type` | `VARCHAR(80)` | YES |  |  | IDX: idx_notifications_related |  | Field `related_type` used by `notifications` business logic and screens. |
| `related_id` | `BIGINT UNSIGNED` | YES |  |  | IDX: idx_notifications_related |  | Identifier/reference field used to join or scope `notifications` records. |
| `is_read` | `BOOLEAN` | NO | FALSE |  | IDX: idx_notifications_user_read_time |  | Field `is_read` used by `notifications` business logic and screens. |
| `read_at` | `DATETIME` | YES |  |  |  |  | Business/audit timestamp used for timeline, SLA, filtering, or history. |
| `created_at` | `DATETIME` | NO | CURRENT_TIMESTAMP |  | IDX: idx_notifications_user_read_time; IDX: idx_notifications_type_time |  | Business/audit timestamp used for timeline, SLA, filtering, or history. |

**Primary Key:**
- `PRIMARY KEY (notification_id)`

**Indexes:**
- `KEY idx_notifications_user_read_time (recipient_user_id, is_read, created_at)`
- `KEY idx_notifications_related (related_type, related_id)`
- `KEY idx_notifications_type_time (notification_type, created_at)`

**Foreign Keys:**
- `CONSTRAINT fk_notifications_user FOREIGN KEY (recipient_user_id) REFERENCES users(user_id) ON UPDATE CASCADE ON DELETE CASCADE`

### 5.39. `calendar_events`
**Purpose / Table Comment:** Calendar events. Attendees/reminders are normalized in child tables.

**Main Screens / UC Area:** Calendar / Visit / Personal Events / Deadlines

**Column Count:** `22`

**Columns:**

| Column | Type | Null | Default | Extra | Key / FK / Index | Enum Values | Notes / Meaning |
|---|---|---:|---|---|---|---|---|
| `calendar_event_id` | `BIGINT UNSIGNED` | NO |  | AUTO_INCREMENT | PK |  | Identifier/reference field used to join or scope `calendar_events` records. |
| `owner_user_id` | `BIGINT UNSIGNED` | NO |  |  | IDX: idx_calendar_owner_time; FK: users(user_id) |  | Identifier/reference field used to join or scope `calendar_events` records. |
| `campus_id` | `BIGINT UNSIGNED` | YES |  |  | IDX: idx_calendar_campus_time; FK: campuses(campus_id) |  | Identifier/reference field used to join or scope `calendar_events` records. |
| `visit_instance_id` | `BIGINT UNSIGNED` | YES |  |  | IDX: idx_calendar_visit; FK: visit_request_campuses(visit_instance_id) |  | Identifier/reference field used to join or scope `calendar_events` records. |
| `logistics_item_id` | `BIGINT UNSIGNED` | YES |  |  | IDX: idx_calendar_logistics; FK: visit_logistics_items(logistics_item_id) |  | Identifier/reference field used to join or scope `calendar_events` records. |
| `source_type` | `ENUM('PERSONAL','VISIT','LOGISTICS','DEADLINE')` | NO | 'PERSONAL' |  | IDX: idx_calendar_source_status_time | PERSONAL, VISIT, LOGISTICS, DEADLINE | Field `source_type` used by `calendar_events` business logic and screens. |
| `title` | `VARCHAR(255)` | NO |  |  |  |  | Field `title` used by `calendar_events` business logic and screens. |
| `description` | `TEXT` | YES |  |  |  |  | Field `description` used by `calendar_events` business logic and screens. |
| `location` | `VARCHAR(255)` | YES |  |  |  |  | Field `location` used by `calendar_events` business logic and screens. |
| `start_at` | `DATETIME` | NO |  |  | IDX: idx_calendar_owner_time; IDX: idx_calendar_campus_time; IDX: idx_calendar_source_status_time |  | Business/audit timestamp used for timeline, SLA, filtering, or history. |
| `end_at` | `DATETIME` | NO |  |  |  |  | Business/audit timestamp used for timeline, SLA, filtering, or history. |
| `timezone` | `VARCHAR(50)` | NO | 'Asia/Ho_Chi_Minh' |  |  |  | Field `timezone` used by `calendar_events` business logic and screens. |
| `is_all_day` | `BOOLEAN` | NO | FALSE |  |  |  | Field `is_all_day` used by `calendar_events` business logic and screens. |
| `recurrence_rule` | `VARCHAR(500)` | YES |  |  |  |  | Field `recurrence_rule` used by `calendar_events` business logic and screens. |
| `visibility` | `ENUM('PRIVATE','INTERNAL')` | NO | 'PRIVATE' |  |  | PRIVATE, INTERNAL | Field `visibility` used by `calendar_events` business logic and screens. |
| `status` | `ENUM('ACTIVE','CANCELLED','DONE')` | NO | 'ACTIVE' |  | IDX: idx_calendar_source_status_time | ACTIVE, CANCELLED, DONE | Status field used for workflow state, filtering and UI badges. |
| `created_at` | `DATETIME` | NO | CURRENT_TIMESTAMP |  |  |  | Business/audit timestamp used for timeline, SLA, filtering, or history. |
| `created_by` | `BIGINT UNSIGNED` | YES |  |  |  |  | User reference used for audit and accountability. |
| `updated_at` | `DATETIME` | YES | NULL ON UPDATE CURRENT_TIMESTAMP | ON UPDATE CURRENT_TIMESTAMP |  |  | Business/audit timestamp used for timeline, SLA, filtering, or history. |
| `updated_by` | `BIGINT UNSIGNED` | YES |  |  |  |  | User reference used for audit and accountability. |
| `deleted_at` | `DATETIME` | YES |  |  |  |  | Business/audit timestamp used for timeline, SLA, filtering, or history. |
| `deleted_by` | `BIGINT UNSIGNED` | YES |  |  |  |  | User reference used for audit and accountability. |

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

### 5.40. `calendar_event_attendees`
**Purpose / Table Comment:** Calendar attendees; replaces calendar_events.attendees_json.

**Main Screens / UC Area:** Calendar Event Attendees

**Column Count:** `8`

**Columns:**

| Column | Type | Null | Default | Extra | Key / FK / Index | Enum Values | Notes / Meaning |
|---|---|---:|---|---|---|---|---|
| `calendar_event_attendee_id` | `BIGINT UNSIGNED` | NO |  | AUTO_INCREMENT | PK |  | Identifier/reference field used to join or scope `calendar_event_attendees` records. |
| `calendar_event_id` | `BIGINT UNSIGNED` | NO |  |  | IDX: idx_calendar_attendees_event; FK: calendar_events(calendar_event_id) |  | Identifier/reference field used to join or scope `calendar_event_attendees` records. |
| `user_id` | `BIGINT UNSIGNED` | YES |  |  | IDX: idx_calendar_attendees_user; FK: users(user_id) |  | Identifier/reference field used to join or scope `calendar_event_attendees` records. |
| `attendee_email` | `VARCHAR(150)` | YES |  |  | IDX: idx_calendar_attendees_email |  | Field `attendee_email` used by `calendar_event_attendees` business logic and screens. |
| `attendee_name` | `VARCHAR(150)` | YES |  |  |  |  | Field `attendee_name` used by `calendar_event_attendees` business logic and screens. |
| `attendee_role` | `VARCHAR(80)` | YES |  |  |  |  | Field `attendee_role` used by `calendar_event_attendees` business logic and screens. |
| `response_status` | `ENUM('NEEDS_ACTION','ACCEPTED','DECLINED','TENTATIVE')` | NO | 'NEEDS_ACTION' |  |  | NEEDS_ACTION, ACCEPTED, DECLINED, TENTATIVE | Status field used for workflow state, filtering and UI badges. |
| `created_at` | `DATETIME` | NO | CURRENT_TIMESTAMP |  |  |  | Business/audit timestamp used for timeline, SLA, filtering, or history. |

**Primary Key:**
- `PRIMARY KEY (calendar_event_attendee_id)`

**Indexes:**
- `KEY idx_calendar_attendees_event (calendar_event_id)`
- `KEY idx_calendar_attendees_user (user_id)`
- `KEY idx_calendar_attendees_email (attendee_email)`

**Foreign Keys:**
- `CONSTRAINT fk_calendar_attendees_event FOREIGN KEY (calendar_event_id) REFERENCES calendar_events(calendar_event_id) ON UPDATE CASCADE ON DELETE CASCADE`
- `CONSTRAINT fk_calendar_attendees_user FOREIGN KEY (user_id) REFERENCES users(user_id) ON UPDATE CASCADE ON DELETE SET NULL`

### 5.41. `calendar_event_reminders`
**Purpose / Table Comment:** Calendar reminders; replaces calendar_events.reminders_json.

**Main Screens / UC Area:** Calendar Reminders

**Column Count:** `8`

**Columns:**

| Column | Type | Null | Default | Extra | Key / FK / Index | Enum Values | Notes / Meaning |
|---|---|---:|---|---|---|---|---|
| `calendar_event_reminder_id` | `BIGINT UNSIGNED` | NO |  | AUTO_INCREMENT | PK |  | Identifier/reference field used to join or scope `calendar_event_reminders` records. |
| `calendar_event_id` | `BIGINT UNSIGNED` | NO |  |  | IDX: idx_calendar_reminders_event; FK: calendar_events(calendar_event_id) |  | Identifier/reference field used to join or scope `calendar_event_reminders` records. |
| `reminder_type` | `ENUM('EMAIL','POPUP','IN_APP')` | NO | 'IN_APP' |  |  | EMAIL, POPUP, IN_APP | Field `reminder_type` used by `calendar_event_reminders` business logic and screens. |
| `minutes_before` | `INT UNSIGNED` | NO | 0 |  |  |  | Field `minutes_before` used by `calendar_event_reminders` business logic and screens. |
| `scheduled_at` | `DATETIME` | YES |  |  | IDX: idx_calendar_reminders_status_schedule |  | Business/audit timestamp used for timeline, SLA, filtering, or history. |
| `sent_at` | `DATETIME` | YES |  |  |  |  | Business/audit timestamp used for timeline, SLA, filtering, or history. |
| `status` | `ENUM('PENDING','SENT','CANCELLED','FAILED')` | NO | 'PENDING' |  | IDX: idx_calendar_reminders_status_schedule | PENDING, SENT, CANCELLED, FAILED | Status field used for workflow state, filtering and UI badges. |
| `created_at` | `DATETIME` | NO | CURRENT_TIMESTAMP |  |  |  | Business/audit timestamp used for timeline, SLA, filtering, or history. |

**Primary Key:**
- `PRIMARY KEY (calendar_event_reminder_id)`

**Indexes:**
- `KEY idx_calendar_reminders_event (calendar_event_id)`
- `KEY idx_calendar_reminders_status_schedule (status, scheduled_at)`

**Foreign Keys:**
- `CONSTRAINT fk_calendar_reminders_event FOREIGN KEY (calendar_event_id) REFERENCES calendar_events(calendar_event_id) ON UPDATE CASCADE ON DELETE CASCADE`

### 5.42. `api_configurations`
**Purpose / Table Comment:** API config + encrypted credentials JSON

**Main Screens / UC Area:** External API Management / Integration Settings

**Column Count:** `33`

**Columns:**

| Column | Type | Null | Default | Extra | Key / FK / Index | Enum Values | Notes / Meaning |
|---|---|---:|---|---|---|---|---|
| `api_config_id` | `BIGINT UNSIGNED` | NO |  | AUTO_INCREMENT | PK |  | Identifier/reference field used to join or scope `api_configurations` records. |
| `api_code` | `VARCHAR(100)` | NO |  |  | UNIQUE: uq_api_config_code |  | Field `api_code` used by `api_configurations` business logic and screens. |
| `name` | `VARCHAR(150)` | NO |  |  |  |  | Field `name` used by `api_configurations` business logic and screens. |
| `provider_name` | `VARCHAR(150)` | YES |  |  | IDX: idx_api_provider_status |  | Field `provider_name` used by `api_configurations` business logic and screens. |
| `purpose` | `VARCHAR(150)` | YES |  |  |  |  | Field `purpose` used by `api_configurations` business logic and screens. |
| `base_url` | `VARCHAR(500)` | NO |  |  |  |  | Field `base_url` used by `api_configurations` business logic and screens. |
| `default_method` | `ENUM('GET','POST','PUT','PATCH','DELETE')` | NO | 'POST' |  |  | GET, POST, PUT, PATCH, DELETE | Field `default_method` used by `api_configurations` business logic and screens. |
| `auth_type` | `ENUM('NONE','API_KEY','BEARER_TOKEN','BASIC','OAUTH2','CUSTOM')` | NO | 'NONE' |  |  | NONE, API_KEY, BEARER_TOKEN, BASIC, OAUTH2, CUSTOM | Field `auth_type` used by `api_configurations` business logic and screens. |
| `api_key_encrypted` | `VARCHAR(700)` | YES |  |  |  |  | Field `api_key_encrypted` used by `api_configurations` business logic and screens. |
| `bearer_token_encrypted` | `VARCHAR(700)` | YES |  |  |  |  | Field `bearer_token_encrypted` used by `api_configurations` business logic and screens. |
| `basic_username` | `VARCHAR(150)` | YES |  |  |  |  | Field `basic_username` used by `api_configurations` business logic and screens. |
| `basic_password_encrypted` | `VARCHAR(700)` | YES |  |  |  |  | Field `basic_password_encrypted` used by `api_configurations` business logic and screens. |
| `oauth_client_id` | `VARCHAR(255)` | YES |  |  |  |  | Identifier/reference field used to join or scope `api_configurations` records. |
| `oauth_client_secret_encrypted` | `VARCHAR(700)` | YES |  |  |  |  | Field `oauth_client_secret_encrypted` used by `api_configurations` business logic and screens. |
| `oauth_token_url` | `VARCHAR(700)` | YES |  |  |  |  | Field `oauth_token_url` used by `api_configurations` business logic and screens. |
| `oauth_scope` | `VARCHAR(500)` | YES |  |  |  |  | Field `oauth_scope` used by `api_configurations` business logic and screens. |
| `body_template_text` | `LONGTEXT` | YES |  |  |  |  | Field `body_template_text` used by `api_configurations` business logic and screens. |
| `rate_limit_per_minute` | `INT UNSIGNED` | YES |  |  |  |  | Field `rate_limit_per_minute` used by `api_configurations` business logic and screens. |
| `monthly_quota` | `INT UNSIGNED` | YES |  |  |  |  | Field `monthly_quota` used by `api_configurations` business logic and screens. |
| `retry_enabled` | `BOOLEAN` | NO | FALSE |  |  |  | Field `retry_enabled` used by `api_configurations` business logic and screens. |
| `max_retries` | `INT UNSIGNED` | NO | 0 |  |  |  | Field `max_retries` used by `api_configurations` business logic and screens. |
| `cache_ttl_seconds` | `INT UNSIGNED` | YES |  |  |  |  | Field `cache_ttl_seconds` used by `api_configurations` business logic and screens. |
| `last_test_status` | `ENUM('SUCCESS','FAILED')` | YES |  |  | IDX: idx_api_config_test_status | SUCCESS, FAILED | Status field used for workflow state, filtering and UI badges. |
| `last_tested_at` | `DATETIME` | YES |  |  | IDX: idx_api_config_test_status |  | Business/audit timestamp used for timeline, SLA, filtering, or history. |
| `last_test_message` | `TEXT` | YES |  |  |  |  | Field `last_test_message` used by `api_configurations` business logic and screens. |
| `timeout_seconds` | `INT UNSIGNED` | NO | 30 |  |  |  | Field `timeout_seconds` used by `api_configurations` business logic and screens. |
| `status` | `ENUM('ACTIVE','INACTIVE','DISABLED')` | NO | 'ACTIVE' |  | IDX: idx_api_config_status; IDX: idx_api_provider_status | ACTIVE, INACTIVE, DISABLED | Status field used for workflow state, filtering and UI badges. |
| `created_at` | `DATETIME` | NO | CURRENT_TIMESTAMP |  |  |  | Business/audit timestamp used for timeline, SLA, filtering, or history. |
| `created_by` | `BIGINT UNSIGNED` | YES |  |  |  |  | User reference used for audit and accountability. |
| `updated_at` | `DATETIME` | YES | NULL ON UPDATE CURRENT_TIMESTAMP | ON UPDATE CURRENT_TIMESTAMP |  |  | Business/audit timestamp used for timeline, SLA, filtering, or history. |
| `updated_by` | `BIGINT UNSIGNED` | YES |  |  |  |  | User reference used for audit and accountability. |
| `deleted_at` | `DATETIME` | YES |  |  |  |  | Business/audit timestamp used for timeline, SLA, filtering, or history. |
| `deleted_by` | `BIGINT UNSIGNED` | YES |  |  |  |  | User reference used for audit and accountability. |

**Primary Key:**
- `PRIMARY KEY (api_config_id)`

**Unique Constraints:**
- `UNIQUE KEY uq_api_config_code (api_code)`

**Indexes:**
- `KEY idx_api_config_status (status)`
- `KEY idx_api_config_test_status (last_test_status, last_tested_at)`
- `KEY idx_api_provider_status (provider_name, status)`

### 5.43. `api_configuration_headers`
**Purpose / Table Comment:** Explicit API request headers; replaces api_configurations.headers_json.

**Main Screens / UC Area:** External API Headers

**Column Count:** `6`

**Columns:**

| Column | Type | Null | Default | Extra | Key / FK / Index | Enum Values | Notes / Meaning |
|---|---|---:|---|---|---|---|---|
| `api_configuration_header_id` | `BIGINT UNSIGNED` | NO |  | AUTO_INCREMENT | PK |  | Identifier/reference field used to join or scope `api_configuration_headers` records. |
| `api_config_id` | `BIGINT UNSIGNED` | NO |  |  | UNIQUE: uq_api_header_name; IDX: idx_api_headers_config; FK: api_configurations(api_config_id) |  | Identifier/reference field used to join or scope `api_configuration_headers` records. |
| `header_name` | `VARCHAR(150)` | NO |  |  | UNIQUE: uq_api_header_name |  | Field `header_name` used by `api_configuration_headers` business logic and screens. |
| `header_value_encrypted` | `VARCHAR(1000)` | YES |  |  |  |  | Field `header_value_encrypted` used by `api_configuration_headers` business logic and screens. |
| `is_secret` | `BOOLEAN` | NO | TRUE |  |  |  | Field `is_secret` used by `api_configuration_headers` business logic and screens. |
| `created_at` | `DATETIME` | NO | CURRENT_TIMESTAMP |  |  |  | Business/audit timestamp used for timeline, SLA, filtering, or history. |

**Primary Key:**
- `PRIMARY KEY (api_configuration_header_id)`

**Unique Constraints:**
- `UNIQUE KEY uq_api_header_name (api_config_id, header_name)`

**Indexes:**
- `KEY idx_api_headers_config (api_config_id)`

**Foreign Keys:**
- `CONSTRAINT fk_api_headers_config FOREIGN KEY (api_config_id) REFERENCES api_configurations(api_config_id) ON UPDATE CASCADE ON DELETE CASCADE`

### 5.44. `api_usage_quotas`
**Purpose / Table Comment:** API quota + counter per campus/month

**Main Screens / UC Area:** API Quota / Usage Monitoring

**Column Count:** `12`

**Columns:**

| Column | Type | Null | Default | Extra | Key / FK / Index | Enum Values | Notes / Meaning |
|---|---|---:|---|---|---|---|---|
| `api_usage_quota_id` | `BIGINT UNSIGNED` | NO |  | AUTO_INCREMENT | PK |  | Identifier/reference field used to join or scope `api_usage_quotas` records. |
| `api_config_id` | `BIGINT UNSIGNED` | NO |  |  | UNIQUE: uq_api_quota_config_scope_period; FK: api_configurations(api_config_id) |  | Identifier/reference field used to join or scope `api_usage_quotas` records. |
| `campus_id` | `BIGINT UNSIGNED` | YES |  |  | IDX: idx_api_quota_campus_period; FK: campuses(campus_id) |  | NULL = global quota |
| `campus_scope_key` | `VARCHAR(36)` | NO | 'GLOBAL' |  | UNIQUE: uq_api_quota_config_scope_period |  | Field `campus_scope_key` used by `api_usage_quotas` business logic and screens. |
| `period_yyyymm` | `CHAR(6)` | NO |  |  | UNIQUE: uq_api_quota_config_scope_period; IDX: idx_api_quota_campus_period; IDX: idx_api_quota_period |  | YYYYMM |
| `monthly_limit` | `INT UNSIGNED` | NO |  |  |  |  | Field `monthly_limit` used by `api_usage_quotas` business logic and screens. |
| `used_count` | `INT UNSIGNED` | NO | 0 |  |  |  | Merged api_usage_counters table |
| `last_used_at` | `DATETIME` | YES |  |  |  |  | Business/audit timestamp used for timeline, SLA, filtering, or history. |
| `created_at` | `DATETIME` | NO | CURRENT_TIMESTAMP |  |  |  | Business/audit timestamp used for timeline, SLA, filtering, or history. |
| `created_by` | `BIGINT UNSIGNED` | YES |  |  |  |  | User reference used for audit and accountability. |
| `updated_at` | `DATETIME` | YES | NULL ON UPDATE CURRENT_TIMESTAMP | ON UPDATE CURRENT_TIMESTAMP |  |  | Business/audit timestamp used for timeline, SLA, filtering, or history. |
| `updated_by` | `BIGINT UNSIGNED` | YES |  |  |  |  | User reference used for audit and accountability. |

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

### 5.45. `api_request_logs`
**Purpose / Table Comment:** External API request logs. Never log full secret/token.

**Main Screens / UC Area:** API Logs / Debugging

**Column Count:** `16`

**Columns:**

| Column | Type | Null | Default | Extra | Key / FK / Index | Enum Values | Notes / Meaning |
|---|---|---:|---|---|---|---|---|
| `api_request_log_id` | `BIGINT UNSIGNED` | NO |  | AUTO_INCREMENT | PK |  | Identifier/reference field used to join or scope `api_request_logs` records. |
| `api_config_id` | `BIGINT UNSIGNED` | NO |  |  | IDX: idx_api_logs_config_time; FK: api_configurations(api_config_id) |  | Identifier/reference field used to join or scope `api_request_logs` records. |
| `campus_id` | `BIGINT UNSIGNED` | YES |  |  | IDX: idx_api_logs_campus_time; FK: campuses(campus_id) |  | Identifier/reference field used to join or scope `api_request_logs` records. |
| `requested_by` | `BIGINT UNSIGNED` | YES |  |  | IDX: idx_api_logs_user_time; FK: users(user_id) |  | User reference used for audit and accountability. |
| `related_type` | `VARCHAR(80)` | YES |  |  | IDX: idx_api_logs_related |  | Field `related_type` used by `api_request_logs` business logic and screens. |
| `related_id` | `BIGINT UNSIGNED` | YES |  |  | IDX: idx_api_logs_related |  | Identifier/reference field used to join or scope `api_request_logs` records. |
| `endpoint` | `VARCHAR(500)` | NO |  |  |  |  | Field `endpoint` used by `api_request_logs` business logic and screens. |
| `method` | `ENUM('GET','POST','PUT','PATCH','DELETE')` | NO |  |  |  | GET, POST, PUT, PATCH, DELETE | Field `method` used by `api_request_logs` business logic and screens. |
| `http_status` | `INT` | YES |  |  |  |  | Status field used for workflow state, filtering and UI badges. |
| `response_time_ms` | `INT UNSIGNED` | YES |  |  |  |  | Field `response_time_ms` used by `api_request_logs` business logic and screens. |
| `request_size_bytes` | `BIGINT UNSIGNED` | YES |  |  |  |  | Field `request_size_bytes` used by `api_request_logs` business logic and screens. |
| `response_size_bytes` | `BIGINT UNSIGNED` | YES |  |  |  |  | Field `response_size_bytes` used by `api_request_logs` business logic and screens. |
| `success` | `BOOLEAN` | NO | FALSE |  | IDX: idx_api_logs_success_time |  | Field `success` used by `api_request_logs` business logic and screens. |
| `error_code` | `VARCHAR(100)` | YES |  |  |  |  | Field `error_code` used by `api_request_logs` business logic and screens. |
| `error_message` | `TEXT` | YES |  |  |  |  | Field `error_message` used by `api_request_logs` business logic and screens. |
| `created_at` | `DATETIME` | NO | CURRENT_TIMESTAMP |  | IDX: idx_api_logs_config_time; IDX: idx_api_logs_campus_time; IDX: idx_api_logs_user_time; IDX: idx_api_logs_success_time |  | Business/audit timestamp used for timeline, SLA, filtering, or history. |

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

### 5.46. `agenda_templates`
**Purpose / Table Comment:** Agenda template header by campus/global scope and visit type. `campus_scope_key` is derived by trigger (`GLOBAL` when `campus_id IS NULL`, else the campus id as text); the `campus_id ⇔ GLOBAL` invariant is enforced by the trigger (NOT a CHECK) because `campus_id` carries FK referential actions (MySQL 8 restriction).

**Main Screens / UC Area:** Agenda Template Management (configurable per `visit_type`)

**Column Count:** `13`

**Columns:**

| Column | Type | Null | Default | Extra | Key / FK / Index | Enum Values | Notes / Meaning |
|---|---|---:|---|---|---|---|---|
| `agenda_template_id` | `BIGINT UNSIGNED` | NO |  | AUTO_INCREMENT | PK |  | Identifier/reference field used to join or scope `agenda_templates` records. |
| `campus_id` | `BIGINT UNSIGNED` | YES |  |  | IDX: idx_agenda_templates_campus_status; FK: campuses(campus_id) |  | Identifier/reference field used to join or scope `agenda_templates` records. |
| `campus_scope_key` | `VARCHAR(36)` | NO | 'GLOBAL' |  | UNIQUE: uq_agenda_template_scope_type_name; IDX: idx_agenda_templates_scope_type_status |  | `GLOBAL` or the campus id as text; derived by trigger from `campus_id`. |
| `visit_type` | `ENUM('CAMPUS_TOUR','MEETING','WORKSHOP','SIGNING_CEREMONY','EXCHANGE','OTHER')` | NO |  |  | UNIQUE: uq_agenda_template_scope_type_name; IDX: idx_agenda_templates_scope_type_status; IDX: idx_agenda_templates_campus_type_status | CAMPUS_TOUR, MEETING, WORKSHOP, SIGNING_CEREMONY, EXCHANGE, OTHER | Visit type the template applies to. Mirrors `visit_requests.visit_type`. |
| `name` | `VARCHAR(150)` | NO |  |  | UNIQUE: uq_agenda_template_scope_type_name |  | Field `name` used by `agenda_templates` business logic and screens. |
| `description` | `TEXT` | YES |  |  |  |  | Field `description` used by `agenda_templates` business logic and screens. |
| `status` | `ENUM('ACTIVE','INACTIVE')` | NO | 'ACTIVE' |  | IDX: idx_agenda_templates_status; IDX: idx_agenda_templates_scope_type_status; IDX: idx_agenda_templates_campus_type_status | ACTIVE, INACTIVE | Status field used for workflow state, filtering and UI badges. |
| `created_at` | `DATETIME` | NO | CURRENT_TIMESTAMP |  |  |  | Business/audit timestamp used for timeline, SLA, filtering, or history. |
| `created_by` | `BIGINT UNSIGNED` | YES |  |  |  |  | User reference used for audit and accountability. |
| `updated_at` | `DATETIME` | YES | NULL ON UPDATE CURRENT_TIMESTAMP | ON UPDATE CURRENT_TIMESTAMP |  |  | Business/audit timestamp used for timeline, SLA, filtering, or history. |
| `updated_by` | `BIGINT UNSIGNED` | YES |  |  |  |  | User reference used for audit and accountability. |
| `deleted_at` | `DATETIME` | YES |  |  |  |  | Business/audit timestamp used for timeline, SLA, filtering, or history. |
| `deleted_by` | `BIGINT UNSIGNED` | YES |  |  |  |  | User reference used for audit and accountability. |

**Primary Key:**
- `PRIMARY KEY (agenda_template_id)`

**Unique Constraints:**
- `UNIQUE KEY uq_agenda_template_scope_type_name (campus_scope_key, visit_type, name)`

**Indexes:**
- `KEY idx_agenda_templates_status (status)`
- `KEY idx_agenda_templates_scope_type_status (campus_scope_key, visit_type, status)`
- `KEY idx_agenda_templates_campus_type_status (campus_id, visit_type, status)`

**Foreign Keys:**
- `CONSTRAINT fk_agenda_templates_campus FOREIGN KEY (campus_id) REFERENCES campuses(campus_id) ON UPDATE CASCADE ON DELETE SET NULL`
- `CONSTRAINT fk_agenda_templates_created_by FOREIGN KEY (created_by) REFERENCES users(user_id) ON UPDATE CASCADE ON DELETE SET NULL`
- `CONSTRAINT fk_agenda_templates_updated_by FOREIGN KEY (updated_by) REFERENCES users(user_id) ON UPDATE CASCADE ON DELETE SET NULL`
- `CONSTRAINT fk_agenda_templates_deleted_by FOREIGN KEY (deleted_by) REFERENCES users(user_id) ON UPDATE CASCADE ON DELETE SET NULL`

**Scope trigger:** `trg_agenda_templates_scope_bi` / `_bu` set `campus_scope_key = IFNULL(CAST(campus_id AS CHAR), 'GLOBAL')` before insert/update.

### 5.47. `agenda_template_items`
**Purpose / Table Comment:** Agenda template timeline items using relative offset from campus `planned_start_at`. No absolute `TIME` columns — `start_offset_minutes` + `duration_minutes` only.

**Main Screens / UC Area:** Agenda Template Timeline Items

**Column Count:** `13`

**Columns:**

| Column | Type | Null | Default | Extra | Key / FK / Index | Enum Values | Notes / Meaning |
|---|---|---:|---|---|---|---|---|
| `agenda_template_item_id` | `BIGINT UNSIGNED` | NO |  | AUTO_INCREMENT | PK |  | Identifier/reference field used to join or scope `agenda_template_items` records. |
| `agenda_template_id` | `BIGINT UNSIGNED` | NO |  |  | IDX: idx_agenda_template_items_template_order; FK: agenda_templates(agenda_template_id) |  | Identifier/reference field used to join or scope `agenda_template_items` records. |
| `display_order` | `INT UNSIGNED` | NO | 0 |  | UNIQUE: uq_agenda_template_items_order |  | Display order within the template (unique per template). |
| `start_offset_minutes` | `INT UNSIGNED` | NO | 0 |  | IDX: idx_agenda_template_items_template_offset |  | Minutes from the campus `planned_start_at` when this item begins. |
| `duration_minutes` | `INT UNSIGNED` | NO |  |  |  |  | Length of this item in minutes (CHECK > 0). |
| `title` | `VARCHAR(255)` | NO |  |  |  |  | Field `title` used by `agenda_template_items` business logic and screens. |
| `description` | `TEXT` | YES |  |  |  |  | Field `description` used by `agenda_template_items` business logic and screens. |
| `location` | `VARCHAR(255)` | YES |  |  |  |  | Optional default location for the item. |
| `responsible_role_label` | `VARCHAR(150)` | YES |  |  |  |  | Default responsible-role label (free text, e.g. "IC Host"). |
| `created_at` | `DATETIME` | NO | CURRENT_TIMESTAMP |  |  |  | Business/audit timestamp used for timeline, SLA, filtering, or history. |
| `created_by` | `BIGINT UNSIGNED` | YES |  |  | FK: users(user_id) |  | User reference used for audit and accountability. |
| `updated_at` | `DATETIME` | YES |  | ON UPDATE CURRENT_TIMESTAMP |  |  | Last-updated timestamp. |
| `updated_by` | `BIGINT UNSIGNED` | YES |  |  | FK: users(user_id) |  | User reference used for audit and accountability. |

**Primary Key:**
- `PRIMARY KEY (agenda_template_item_id)`

**Unique Constraints:**
- `UNIQUE KEY uq_agenda_template_items_order (agenda_template_id, display_order)`

**Indexes:**
- `KEY idx_agenda_template_items_template_offset (agenda_template_id, start_offset_minutes)`

**Foreign Keys:**
- `CONSTRAINT fk_agenda_template_items_template FOREIGN KEY (agenda_template_id) REFERENCES agenda_templates(agenda_template_id) ON UPDATE CASCADE ON DELETE CASCADE`
- `CONSTRAINT fk_agenda_template_items_created_by FOREIGN KEY (created_by) REFERENCES users(user_id) ON UPDATE CASCADE ON DELETE SET NULL`
- `CONSTRAINT fk_agenda_template_items_updated_by FOREIGN KEY (updated_by) REFERENCES users(user_id) ON UPDATE CASCADE ON DELETE SET NULL`

**Check Constraints:**
- `CHECK (duration_minutes > 0)`

### 5.47b. `agenda_template_defaults`
**Purpose / Table Comment:** Default agenda template mapping by campus/global scope and visit type. `campus_scope_key` is derived by trigger (`trg_agenda_template_defaults_scope_bi`/`_bu`). When a host opens agenda setup, the default is resolved campus-scope first, then GLOBAL fallback (ACTIVE, non-deleted templates only).

**Main Screens / UC Area:** Agenda Template Default Management / Visit Agenda Setup

**Column Count:** `9`

**Columns:**

| Column | Type | Null | Default | Extra | Key / FK / Index | Enum Values | Notes / Meaning |
|---|---|---:|---|---|---|---|---|
| `agenda_template_default_id` | `BIGINT UNSIGNED` | NO |  | AUTO_INCREMENT | PK |  | Identifier of the default mapping row. |
| `campus_id` | `BIGINT UNSIGNED` | YES |  |  | IDX: idx_agenda_template_defaults_campus_type; FK: campuses(campus_id) |  | NULL = GLOBAL default; otherwise the campus the default applies to. |
| `campus_scope_key` | `VARCHAR(36)` | NO | 'GLOBAL' |  | UNIQUE: uq_agenda_template_default_scope_type |  | `GLOBAL` or campus id as text; derived by trigger. |
| `visit_type` | `ENUM('CAMPUS_TOUR','MEETING','WORKSHOP','SIGNING_CEREMONY','EXCHANGE','OTHER')` | NO |  |  | UNIQUE: uq_agenda_template_default_scope_type; IDX: idx_agenda_template_defaults_campus_type | CAMPUS_TOUR, MEETING, WORKSHOP, SIGNING_CEREMONY, EXCHANGE, OTHER | Visit type this default is for. |
| `agenda_template_id` | `BIGINT UNSIGNED` | NO |  |  | IDX: idx_agenda_template_defaults_template; FK: agenda_templates(agenda_template_id) |  | The default template for the (scope, visit_type). |
| `created_at` | `DATETIME` | NO | CURRENT_TIMESTAMP |  |  |  | Created timestamp. |
| `created_by` | `BIGINT UNSIGNED` | YES |  |  | FK: users(user_id) |  | Audit. |
| `updated_at` | `DATETIME` | YES |  | ON UPDATE CURRENT_TIMESTAMP |  |  | Last-updated timestamp. |
| `updated_by` | `BIGINT UNSIGNED` | YES |  |  | FK: users(user_id) |  | Audit. |

**Primary Key:**
- `PRIMARY KEY (agenda_template_default_id)`

**Unique Constraints:**
- `UNIQUE KEY uq_agenda_template_default_scope_type (campus_scope_key, visit_type)`

**Indexes:**
- `KEY idx_agenda_template_defaults_template (agenda_template_id)`
- `KEY idx_agenda_template_defaults_campus_type (campus_id, visit_type)`

**Foreign Keys:**
- `CONSTRAINT fk_agenda_template_defaults_template FOREIGN KEY (agenda_template_id) REFERENCES agenda_templates(agenda_template_id) ON UPDATE CASCADE ON DELETE RESTRICT`
- `CONSTRAINT fk_agenda_template_defaults_campus FOREIGN KEY (campus_id) REFERENCES campuses(campus_id) ON UPDATE CASCADE ON DELETE SET NULL`
- `CONSTRAINT fk_agenda_template_defaults_created_by FOREIGN KEY (created_by) REFERENCES users(user_id) ON UPDATE CASCADE ON DELETE SET NULL`
- `CONSTRAINT fk_agenda_template_defaults_updated_by FOREIGN KEY (updated_by) REFERENCES users(user_id) ON UPDATE CASCADE ON DELETE SET NULL`

### 5.48. `audit_logs`
**Purpose / Table Comment:** General audit log

**Main Screens / UC Area:** System Audit / Business Audit

**Column Count:** `10`

**Columns:**

| Column | Type | Null | Default | Extra | Key / FK / Index | Enum Values | Notes / Meaning |
|---|---|---:|---|---|---|---|---|
| `audit_log_id` | `BIGINT UNSIGNED` | NO |  | AUTO_INCREMENT | PK |  | Identifier/reference field used to join or scope `audit_logs` records. |
| `actor_user_id` | `BIGINT UNSIGNED` | YES |  |  | IDX: idx_audit_actor_time; FK: users(user_id) |  | Identifier/reference field used to join or scope `audit_logs` records. |
| `campus_id` | `BIGINT UNSIGNED` | YES |  |  | IDX: idx_audit_campus_time; FK: campuses(campus_id) |  | Identifier/reference field used to join or scope `audit_logs` records. |
| `action` | `VARCHAR(100)` | NO |  |  | IDX: idx_audit_action_time |  | Field `action` used by `audit_logs` business logic and screens. |
| `entity_type` | `VARCHAR(100)` | NO |  |  | IDX: idx_audit_entity |  | Field `entity_type` used by `audit_logs` business logic and screens. |
| `entity_id` | `BIGINT UNSIGNED` | YES |  |  | IDX: idx_audit_entity |  | Identifier/reference field used to join or scope `audit_logs` records. |
| `ip_address` | `VARCHAR(45)` | YES |  |  |  |  | Field `ip_address` used by `audit_logs` business logic and screens. |
| `user_agent` | `VARCHAR(500)` | YES |  |  |  |  | Field `user_agent` used by `audit_logs` business logic and screens. |
| `request_id` | `VARCHAR(100)` | YES |  |  | IDX: idx_audit_request |  | Identifier/reference field used to join or scope `audit_logs` records. |
| `created_at` | `DATETIME` | NO | CURRENT_TIMESTAMP |  | IDX: idx_audit_actor_time; IDX: idx_audit_action_time; IDX: idx_audit_campus_time |  | Business/audit timestamp used for timeline, SLA, filtering, or history. |

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

### 5.49. `audit_log_changes`
**Purpose / Table Comment:** Field-level audit changes; replaces audit_logs old/new JSON values.

**Main Screens / UC Area:** Audit Field-level Changes

**Column Count:** `6`

**Columns:**

| Column | Type | Null | Default | Extra | Key / FK / Index | Enum Values | Notes / Meaning |
|---|---|---:|---|---|---|---|---|
| `audit_log_change_id` | `BIGINT UNSIGNED` | NO |  | AUTO_INCREMENT | PK |  | Identifier/reference field used to join or scope `audit_log_changes` records. |
| `audit_log_id` | `BIGINT UNSIGNED` | NO |  |  | IDX: idx_audit_changes_log; FK: audit_logs(audit_log_id) |  | Identifier/reference field used to join or scope `audit_log_changes` records. |
| `field_name` | `VARCHAR(150)` | NO |  |  | IDX: idx_audit_changes_field |  | Field `field_name` used by `audit_log_changes` business logic and screens. |
| `old_value_text` | `LONGTEXT` | YES |  |  |  |  | Field `old_value_text` used by `audit_log_changes` business logic and screens. |
| `new_value_text` | `LONGTEXT` | YES |  |  |  |  | Field `new_value_text` used by `audit_log_changes` business logic and screens. |
| `created_at` | `DATETIME` | NO | CURRENT_TIMESTAMP |  |  |  | Business/audit timestamp used for timeline, SLA, filtering, or history. |

**Primary Key:**
- `PRIMARY KEY (audit_log_change_id)`

**Indexes:**
- `KEY idx_audit_changes_log (audit_log_id)`
- `KEY idx_audit_changes_field (field_name)`

**Foreign Keys:**
- `CONSTRAINT fk_audit_changes_log FOREIGN KEY (audit_log_id) REFERENCES audit_logs(audit_log_id) ON UPDATE CASCADE ON DELETE CASCADE`
