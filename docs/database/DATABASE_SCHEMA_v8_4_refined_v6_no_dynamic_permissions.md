# PEMS Database Schema — FULL v8.4 Refined V6 No Dynamic Permissions

> **Generated from:** `pems_full_seed_logic_v8_4_refined_v6_no_dynamic_permissions.sql`
> **Purpose:** Developer-facing database schema reference for the latest PEMS SQL source of truth after removing DB-backed dynamic permissions.
> **SQL style:** Fresh create-only schema; no migration/backfill `ALTER TABLE` or `UPDATE` logic is described here.

## 1. Overview
| Item | Value |
|---|---|
| Database | `pems_db` |
| Engine | MySQL 8.0 / InnoDB |
| Charset / Collation | `utf8mb4` / `utf8mb4_unicode_ci` |
| Schema Version | `PEMS v8.4 refined v6 no dynamic permissions` |
| Base Table Count | `47` |
| Total Column Count | `694` |
| ENUM Column Count | `79` |
| Primary Key Strategy | `BIGINT UNSIGNED AUTO_INCREMENT` for base-table PKs |
| Dynamic Permission Tables | `permissions` and `role_permissions` removed / not present |
| Fixed Role Source | `roles` table only stores fixed role classification: `ADMIN`, `HO`, `STAFF`, `DEPARTMENT`, `STUDENT`, `VISITOR` |
| Authorization Strategy | Backend/frontend fixed policy using `role_code`, `sub_role`, effectiveRole, campus/department scope, ownership and record status |
| Visit Request Status | `PENDING_APPROVAL`, `APPROVED`, `REJECTED`, `CANCELLED` |
| Campus Visit Status | `WAITING_REQUEST_APPROVAL`, `WAITING_HOST_ASSIGNMENT`, `ASSIGNED`, `BEFORE_VISIT`, `DURING_VISIT`, `AFTER_VISIT`, `CLOSED`, `CANCELLED` |
| Status History | `visit_status_logs` removed; status history should use `audit_logs` + `audit_log_changes` |

## 2. No Dynamic Permissions Notes
- `permissions` table is intentionally removed.
- `role_permissions` table is intentionally removed.
- Permission seed and permission matrix seed are intentionally removed.
- UC-117 → UC-121 Role/Permission Management động không còn là runtime DB-backed feature.
- `roles` is kept as fixed account classification only; role update/configuration must not recreate dynamic permission tables.
- Backend authorization must use fixed role policy / `[RoleAuthorize]` / effectiveRole and data-scope checks, not `permission_code` or `permission_level`.

## 3. Table List
| # | Table | Module / Main Screens | Column Count |
|---:|---|---|---:|
| 1 | `roles` | Authentication / Account Management / Fixed RBAC | 6 |
| 2 | `campuses` | Campus Management / Account Management / Visit Request | 13 |
| 3 | `departments` | Department Management / Account Management / Logistics | 10 |
| 4 | `users` | Authentication / Account Management / Profile / RBAC | 25 |
| 5 | `user_auth_providers` | Authentication / SSO / FEID / Local DEV Login | 8 |
| 6 | `user_sessions` | Authentication / Logout / Refresh Token / Session Security | 15 |
| 7 | `otp_tokens` | OTP / Submit Visit Request / Sensitive Action Verification | 14 |
| 8 | `login_logs` | Security Audit / Login Monitoring | 12 |
| 9 | `security_events` | Security Audit / SSO Portal Validation | 15 |
| 10 | `files` | File Metadata / Documents / Gallery / News / Minutes | 15 |
| 11 | `partners` | Partner Management / Public Partners | 23 |
| 12 | `partner_contacts` | Partner Contact / Business Card OCR | 17 |
| 13 | `documents` | Document Management / Archive | 13 |
| 14 | `visit_requests` | Submit Visit Request / Delegation Management / Approval / Cancellation | 40 |
| 15 | `visit_request_campuses` | Campus Visit Instance / Staff Leader Processing / Host Assignment | 25 |
| 16 | `visit_guest_members` | Delegation Guest List / Minutes Source | 12 |
| 17 | `visit_participants` | Internal Participants / Host / Department / Student Assignment | 16 |
| 18 | `visit_agendas` | Visit Agenda / Delegation Detail | 13 |
| 19 | `visit_logistics_items` | Logistics / Resource Request / Task Flow | 45 |
| 20 | `minutes` | Meeting Minutes / Close Delegation | 14 |
| 21 | `minute_participants` | Meeting Minutes Attendance | 14 |
| 22 | `minute_action_items` | Meeting Minutes Follow-up | 12 |
| 23 | `feedbacks` | Feedback Management | 14 |
| 24 | `feedback_rating_items` | Feedback Criteria Ratings | 7 |
| 25 | `news` | News Management / Review / Publish | 17 |
| 26 | `news_translations` | Multilingual News Metadata | 10 |
| 27 | `news_content_sections` | News Editor Sections | 8 |
| 28 | `news_section_files` | News Section Media | 6 |
| 29 | `faqs` | FAQ Management / Public FAQ | 11 |
| 30 | `galleries` | Gallery Management / Public Gallery | 18 |
| 31 | `gallery_images` | Gallery Images / Videos | 15 |
| 32 | `photo_face_tags` | Photo Tagging Metadata | 17 |
| 33 | `email_templates` | Email Template Management | 16 |
| 34 | `sent_emails` | Email Outbox / Delivery Tracking | 16 |
| 35 | `sent_email_recipients` | Email Delivery Tracking per Recipient | 11 |
| 36 | `notifications` | Notification Center | 10 |
| 37 | `calendar_events` | Calendar / Visit / Logistics / Personal Events | 22 |
| 38 | `calendar_event_attendees` | Calendar Attendees | 8 |
| 39 | `calendar_event_reminders` | Calendar Reminders | 8 |
| 40 | `api_configurations` | External API Management | 33 |
| 41 | `api_configuration_headers` | External API Headers | 6 |
| 42 | `api_usage_quotas` | External API Quotas | 12 |
| 43 | `api_request_logs` | External API Request Logs | 16 |
| 44 | `agenda_templates` | Agenda Template Management | 12 |
| 45 | `agenda_template_items` | Agenda Template Items | 8 |
| 46 | `audit_logs` | Audit Trail | 10 |
| 47 | `audit_log_changes` | Audit Field-level Changes | 6 |

## 4. Table Details

### 4.1. `roles`

**Purpose / Table Comment:** Fixed system roles for account classification. No DB-backed dynamic permission matrix.

**Main Screens / UC Area:** Authentication / Account Management / Fixed RBAC

**Columns:**

| Column | Type | Null | Default | Extra | Key / FK / Index | Enum Values | Notes / Meaning |
|---|---|---:|---|---|---|---|---|
| `role_id` | `BIGINT UNSIGNED` | NO |  | AUTO_INCREMENT | PK |  | Khóa chính định danh duy nhất bản ghi trong bảng `roles`. |
| `role_code` | `VARCHAR(30)` | NO |  |  | UNIQUE: uq_roles_code |  | ADMIN, HO, STAFF, DEPARTMENT, STUDENT, VISITOR |
| `name` | `VARCHAR(100)` | NO |  |  |  |  | Tên hiển thị/chính thức của bản ghi. |
| `description` | `VARCHAR(255)` | YES |  |  |  |  | Mô tả chi tiết của bản ghi. |
| `status` | `ENUM('ACTIVE','INACTIVE')` | NO | 'ACTIVE' |  | IDX: idx_roles_status | ACTIVE, INACTIVE | Trạng thái vòng đời của bản ghi; dùng cho filter, badge và chặn thao tác không hợp lệ. |
| `created_at` | `DATETIME` | NO | CURRENT_TIMESTAMP |  |  |  | Thời điểm tạo bản ghi; phục vụ audit, sắp xếp và truy vết lịch sử. |

**Primary Key:**
- `PRIMARY KEY (role_id)`

**Unique Constraints:**
- `uq_roles_code` (role_code)

**Indexes:**
- `idx_roles_status` (status)

**Check Constraints:**
- `CHECK (role_code IN ('ADMIN','HO','STAFF','DEPARTMENT','STUDENT','VISITOR'))`

### 4.2. `campuses`

**Purpose / Table Comment:** Danh mục campus

**Main Screens / UC Area:** Campus Management / Account Management / Visit Request

**Columns:**

| Column | Type | Null | Default | Extra | Key / FK / Index | Enum Values | Notes / Meaning |
|---|---|---:|---|---|---|---|---|
| `campus_id` | `BIGINT UNSIGNED` | NO |  | AUTO_INCREMENT | PK |  | Khóa chính định danh duy nhất bản ghi trong bảng `campuses`. |
| `campus_code` | `VARCHAR(20)` | NO |  |  | UNIQUE: uq_campuses_code |  | HN, HCM, DN, CT, QN |
| `name` | `VARCHAR(150)` | NO |  |  |  |  | Tên hiển thị/chính thức của bản ghi. |
| `city` | `VARCHAR(100)` | YES |  |  | IDX: idx_campuses_city_status |  | Lưu thông tin `city` của bảng `campuses` để phục vụ màn hình và logic nghiệp vụ liên quan. |
| `address` | `VARCHAR(255)` | YES |  |  |  |  | Lưu thông tin `address` của bảng `campuses` để phục vụ màn hình và logic nghiệp vụ liên quan. |
| `phone` | `VARCHAR(30)` | YES |  |  |  |  | Số điện thoại liên hệ. |
| `email` | `VARCHAR(150)` | YES |  |  |  |  | Email liên hệ/đăng nhập/thông báo; cần validate định dạng trước khi lưu. |
| `ic_head_user_id` | `BIGINT UNSIGNED` | YES |  |  | IDX: idx_campuses_ic_head |  | FK added after users table |
| `status` | `ENUM('ACTIVE','INACTIVE')` | NO | 'ACTIVE' |  | IDX: idx_campuses_status, idx_campuses_city_status | ACTIVE, INACTIVE | Trạng thái vòng đời của bản ghi; dùng cho filter, badge và chặn thao tác không hợp lệ. |
| `created_at` | `DATETIME` | NO | CURRENT_TIMESTAMP |  |  |  | Thời điểm tạo bản ghi; phục vụ audit, sắp xếp và truy vết lịch sử. |
| `created_by` | `BIGINT UNSIGNED` | YES |  |  |  |  | User tạo bản ghi; phục vụ audit và kiểm tra trách nhiệm thao tác. |
| `updated_at` | `DATETIME` | YES | NULL | ON UPDATE CURRENT_TIMESTAMP |  |  | Thời điểm cập nhật gần nhất; tự động cập nhật khi SQL có ON UPDATE CURRENT_TIMESTAMP. |
| `updated_by` | `BIGINT UNSIGNED` | YES |  |  |  |  | User cập nhật gần nhất; phục vụ audit. |

**Primary Key:**
- `PRIMARY KEY (campus_id)`

**Unique Constraints:**
- `uq_campuses_code` (campus_code)

**Indexes:**
- `idx_campuses_status` (status)
- `idx_campuses_city_status` (city, status)
- `idx_campuses_ic_head` (ic_head_user_id)

### 4.3. `departments`

**Purpose / Table Comment:** Phòng ban theo campus. STAFF thuộc IC, DEPARTMENT thuộc GENERAL

**Main Screens / UC Area:** Department Management / Account Management / Logistics

**Columns:**

| Column | Type | Null | Default | Extra | Key / FK / Index | Enum Values | Notes / Meaning |
|---|---|---:|---|---|---|---|---|
| `department_id` | `BIGINT UNSIGNED` | NO |  | AUTO_INCREMENT | PK |  | Phòng ban của user. STAFF thuộc phòng IC, DEPARTMENT thuộc phòng GENERAL; dùng cho department scope. |
| `campus_id` | `BIGINT UNSIGNED` | NO |  |  | UNIQUE: uq_departments_campus_name; IDX: idx_departments_campus_type; FK: campuses(campus_id) |  | Khóa ngoại liên kết tới campuses(campus_id); dùng để join dữ liệu và đảm bảo toàn vẹn tham chiếu. |
| `name` | `VARCHAR(150)` | NO |  |  | UNIQUE: uq_departments_campus_name |  | Tên hiển thị/chính thức của bản ghi. |
| `department_type` | `ENUM('IC','GENERAL')` | NO |  |  | IDX: idx_departments_campus_type | IC, GENERAL | IC=International Cooperation; GENERAL=other departments |
| `head_user_id` | `BIGINT UNSIGNED` | YES |  |  | IDX: idx_departments_head |  | FK added after users table |
| `status` | `ENUM('ACTIVE','INACTIVE')` | NO | 'ACTIVE' |  | IDX: idx_departments_status | ACTIVE, INACTIVE | Trạng thái vòng đời của bản ghi; dùng cho filter, badge và chặn thao tác không hợp lệ. |
| `created_at` | `DATETIME` | NO | CURRENT_TIMESTAMP |  |  |  | Thời điểm tạo bản ghi; phục vụ audit, sắp xếp và truy vết lịch sử. |
| `created_by` | `BIGINT UNSIGNED` | YES |  |  |  |  | User tạo bản ghi; phục vụ audit và kiểm tra trách nhiệm thao tác. |
| `updated_at` | `DATETIME` | YES | NULL | ON UPDATE CURRENT_TIMESTAMP |  |  | Thời điểm cập nhật gần nhất; tự động cập nhật khi SQL có ON UPDATE CURRENT_TIMESTAMP. |
| `updated_by` | `BIGINT UNSIGNED` | YES |  |  |  |  | User cập nhật gần nhất; phục vụ audit. |

**Primary Key:**
- `PRIMARY KEY (department_id)`

**Unique Constraints:**
- `uq_departments_campus_name` (campus_id, name)

**Indexes:**
- `idx_departments_campus_type` (campus_id, department_type)
- `idx_departments_status` (status)
- `idx_departments_head` (head_user_id)

**Foreign Keys:**
- `fk_departments_campus`: (campus_id) → `campuses`(campus_id) ON UPDATE CASCADE ON DELETE RESTRICT

### 4.4. `users`

**Purpose / Table Comment:** Authentication / Account Management / Profile / RBAC

**Main Screens / UC Area:** Authentication / Account Management / Profile / RBAC

**Columns:**

| Column | Type | Null | Default | Extra | Key / FK / Index | Enum Values | Notes / Meaning |
|---|---|---:|---|---|---|---|---|
| `user_id` | `BIGINT UNSIGNED` | NO |  | AUTO_INCREMENT | PK |  | Khóa chính định danh duy nhất bản ghi trong bảng `users`. |
| `full_name` | `VARCHAR(150)` | NO |  |  |  |  | Họ tên đầy đủ của người dùng/người tham gia. |
| `email` | `VARCHAR(150)` | NO |  |  | UNIQUE: uq_users_email; IDX: idx_users_email_status |  | Email liên hệ/đăng nhập/thông báo; cần validate định dạng trước khi lưu. |
| `phone` | `VARCHAR(30)` | YES |  |  |  |  | Số điện thoại liên hệ. |
| `nationality` | `VARCHAR(100)` | YES |  |  | IDX: idx_users_nationality |  | Quốc tịch của user/visitor |
| `password_hash` | `VARCHAR(255)` | YES |  |  |  |  | DEV/local password hash only. Production SSO-only accounts keep this NULL. |
| `role_id` | `BIGINT UNSIGNED` | NO |  |  | IDX: idx_users_role_sub_role, idx_users_campus_role_status; FK: roles(role_id) |  | Khóa ngoại liên kết tới roles(role_id); dùng để join dữ liệu và đảm bảo toàn vẹn tham chiếu. |
| `sub_role` | `ENUM('LEADER','STAFF')` | YES |  |  | IDX: idx_users_role_sub_role | LEADER, STAFF | Only for STAFF/DEPARTMENT |
| `primary_campus_id` | `BIGINT UNSIGNED` | YES |  |  | IDX: idx_users_primary_campus, idx_users_campus_role_status; FK: campuses(campus_id) |  | Campus duy nhất của user nội bộ. VISITOR phải NULL. |
| `department_id` | `BIGINT UNSIGNED` | YES |  |  | IDX: idx_users_department, idx_users_department_status; FK: departments(department_id) |  | STAFF = IC department; DEPARTMENT = GENERAL department |
| `gender` | `ENUM('MALE','FEMALE','OTHER')` | YES |  |  |  | MALE, FEMALE, OTHER | NULL=chưa cung cấp; OTHER=khác Nam/Nữ |
| `avatar_url` | `VARCHAR(500)` | YES |  |  |  |  | URL/đường dẫn phục vụ hiển thị hoặc truy cập tài nguyên. |
| `student_code` | `VARCHAR(30)` | YES |  |  | UNIQUE: uq_users_student_code |  | Mã nghiệp vụ ngắn, ổn định, dùng cho seed, filter, rule và tích hợp. |
| `fe_id` | `VARCHAR(100)` | YES |  |  | UNIQUE: uq_users_fe_id |  | Mã định danh/tham chiếu tới `fe` hoặc entity liên quan. |
| `status` | `ENUM('ACTIVE','INACTIVE','LOCKED')` | NO | 'ACTIVE' |  | IDX: idx_users_status, idx_users_email_status, idx_users_campus_role_status, idx_users_department_status | ACTIVE, INACTIVE, LOCKED | ACTIVE=hoạt động, INACTIVE=tạm ngưng, LOCKED=bị khóa |
| `email_verified_at` | `DATETIME` | YES |  |  |  |  | Thời điểm email được xác thực qua SSO lần đầu hoặc xác nhận bởi hệ thống |
| `failed_login_count` | `INT UNSIGNED` | NO | 0 |  |  |  | Số lần đăng nhập sai local password liên tiếp; reset khi login thành công |
| `locked_until` | `DATETIME` | YES |  |  |  |  | Thời điểm hết khóa tạm thời nếu bị lock |
| `created_via` | `ENUM('MANUAL_CREATED','VISITOR_FORM','SSO_AUTO_PROVISION')` | NO | 'MANUAL_CREATED' |  | IDX: idx_users_created_via | MANUAL_CREATED, VISITOR_FORM, SSO_AUTO_PROVISION | MANUAL_CREATED=HO/Staff Leader tạo, VISITOR_FORM=tạo từ form visitor, SSO_AUTO_PROVISION=tạo tự động khi đăng nhập SSO ở cổng Visitor |
| `first_login_at` | `DATETIME` | YES |  |  |  |  | Mốc thời gian nghiệp vụ, dùng cho audit, filter, timeline hoặc SLA. |
| `last_login_at` | `DATETIME` | YES |  |  | IDX: idx_users_last_login |  | Mốc thời gian nghiệp vụ, dùng cho audit, filter, timeline hoặc SLA. |
| `created_at` | `DATETIME` | NO | CURRENT_TIMESTAMP |  |  |  | Thời điểm tạo bản ghi; phục vụ audit, sắp xếp và truy vết lịch sử. |
| `created_by` | `BIGINT UNSIGNED` | YES |  |  |  |  | User tạo bản ghi; phục vụ audit và kiểm tra trách nhiệm thao tác. |
| `updated_at` | `DATETIME` | YES | NULL | ON UPDATE CURRENT_TIMESTAMP |  |  | Thời điểm cập nhật gần nhất; tự động cập nhật khi SQL có ON UPDATE CURRENT_TIMESTAMP. |
| `updated_by` | `BIGINT UNSIGNED` | YES |  |  |  |  | User cập nhật gần nhất; phục vụ audit. |

**Primary Key:**
- `PRIMARY KEY (user_id)`

**Unique Constraints:**
- `uq_users_email` (email)
- `uq_users_student_code` (student_code)
- `uq_users_fe_id` (fe_id)

**Indexes:**
- `idx_users_role_sub_role` (role_id, sub_role)
- `idx_users_primary_campus` (primary_campus_id)
- `idx_users_department` (department_id)
- `idx_users_status` (status)
- `idx_users_email_status` (email, status)
- `idx_users_campus_role_status` (primary_campus_id, role_id, status)
- `idx_users_department_status` (department_id, status)
- `idx_users_created_via` (created_via)
- `idx_users_last_login` (last_login_at)
- `idx_users_nationality` (nationality)

**Foreign Keys:**
- `fk_users_role`: (role_id) → `roles`(role_id) ON UPDATE CASCADE ON DELETE RESTRICT
- `fk_users_primary_campus`: (primary_campus_id) → `campuses`(campus_id) ON UPDATE CASCADE ON DELETE RESTRICT
- `fk_users_department`: (department_id) → `departments`(department_id) ON UPDATE CASCADE ON DELETE RESTRICT

### 4.5. `user_auth_providers`

**Purpose / Table Comment:** Authentication / SSO / FEID / Local DEV Login

**Main Screens / UC Area:** Authentication / SSO / FEID / Local DEV Login

**Columns:**

| Column | Type | Null | Default | Extra | Key / FK / Index | Enum Values | Notes / Meaning |
|---|---|---:|---|---|---|---|---|
| `auth_provider_id` | `BIGINT UNSIGNED` | NO |  | AUTO_INCREMENT | PK |  | Khóa chính định danh duy nhất bản ghi trong bảng `user_auth_providers`. |
| `user_id` | `BIGINT UNSIGNED` | NO |  |  | UNIQUE: uq_user_auth_provider_type; FK: users(user_id) |  | Khóa ngoại liên kết tới users(user_id); dùng để join dữ liệu và đảm bảo toàn vẹn tham chiếu. |
| `provider_type` | `ENUM('LOCAL_PASSWORD','GOOGLE_SSO','FEID')` | NO |  |  | UNIQUE: uq_user_auth_provider_type, uq_auth_provider_subject; IDX: idx_auth_provider_type_email_enabled | LOCAL_PASSWORD, GOOGLE_SSO, FEID | Loại/phân nhóm nghiệp vụ của bản ghi, dùng cho filter, rule và UI. |
| `provider_subject` | `VARCHAR(255)` | YES |  |  | UNIQUE: uq_auth_provider_subject |  | Required for GOOGLE_SSO/FEID |
| `provider_email` | `VARCHAR(150)` | YES |  |  | IDX: idx_auth_provider_email, idx_auth_provider_type_email_enabled |  | Lưu thông tin `provider_email` của bảng `user_auth_providers` để phục vụ màn hình và logic nghiệp vụ liên quan. |
| `is_enabled` | `BOOLEAN` | NO | TRUE |  | IDX: idx_auth_provider_type_email_enabled |  | Cờ boolean đánh dấu trạng thái hoặc điều kiện nghiệp vụ. |
| `linked_at` | `DATETIME` | NO | CURRENT_TIMESTAMP |  |  |  | Mốc thời gian nghiệp vụ, dùng cho audit, filter, timeline hoặc SLA. |
| `last_used_at` | `DATETIME` | YES |  |  |  |  | Mốc thời gian nghiệp vụ, dùng cho audit, filter, timeline hoặc SLA. |

**Primary Key:**
- `PRIMARY KEY (auth_provider_id)`

**Unique Constraints:**
- `uq_user_auth_provider_type` (user_id, provider_type)
- `uq_auth_provider_subject` (provider_type, provider_subject)

**Indexes:**
- `idx_auth_provider_email` (provider_email)
- `idx_auth_provider_type_email_enabled` (provider_type, provider_email, is_enabled)

**Foreign Keys:**
- `fk_auth_providers_user`: (user_id) → `users`(user_id) ON UPDATE CASCADE ON DELETE CASCADE

### 4.6. `user_sessions`

**Purpose / Table Comment:** Session + refresh token hash

**Main Screens / UC Area:** Authentication / Logout / Refresh Token / Session Security

**Columns:**

| Column | Type | Null | Default | Extra | Key / FK / Index | Enum Values | Notes / Meaning |
|---|---|---:|---|---|---|---|---|
| `session_id` | `BIGINT UNSIGNED` | NO |  | AUTO_INCREMENT | PK |  | Khóa chính định danh duy nhất bản ghi trong bảng `user_sessions`. |
| `user_id` | `BIGINT UNSIGNED` | NO |  |  | IDX: idx_sessions_user_active; FK: users(user_id) |  | Khóa ngoại liên kết tới users(user_id); dùng để join dữ liệu và đảm bảo toàn vẹn tham chiếu. |
| `login_portal` | `ENUM('VISITOR','INTERNAL')` | NO |  |  | IDX: idx_sessions_portal_campus | VISITOR, INTERNAL | Lưu thông tin `login_portal` của bảng `user_sessions` để phục vụ màn hình và logic nghiệp vụ liên quan. |
| `selected_campus_id` | `BIGINT UNSIGNED` | YES |  |  | IDX: idx_sessions_portal_campus; FK: campuses(campus_id) |  | Auto set to users.primary_campus_id for INTERNAL, NULL for VISITOR |
| `auth_provider_id` | `BIGINT UNSIGNED` | YES |  |  | FK: user_auth_providers(auth_provider_id) |  | Khóa ngoại liên kết tới user_auth_providers(auth_provider_id); dùng để join dữ liệu và đảm bảo toàn vẹn tham chiếu. |
| `refresh_token_hash` | `VARCHAR(255)` | YES |  |  | UNIQUE: uq_sessions_refresh_hash; IDX: idx_sessions_refresh_active |  | Refresh token hash merged into session |
| `refresh_expires_at` | `DATETIME` | YES |  |  | IDX: idx_sessions_refresh_active |  | Mốc thời gian nghiệp vụ, dùng cho audit, filter, timeline hoặc SLA. |
| `refresh_revoked_at` | `DATETIME` | YES |  |  | IDX: idx_sessions_refresh_active |  | Mốc thời gian nghiệp vụ, dùng cho audit, filter, timeline hoặc SLA. |
| `ip_address` | `VARCHAR(45)` | YES |  |  | IDX: idx_sessions_ip_time |  | Địa chỉ IP của client, phục vụ audit/security log. |
| `user_agent` | `VARCHAR(500)` | YES |  |  |  |  | Thông tin trình duyệt/thiết bị, phục vụ audit/security log. |
| `created_at` | `DATETIME` | NO | CURRENT_TIMESTAMP |  | IDX: idx_sessions_ip_time |  | Thời điểm tạo bản ghi; phục vụ audit, sắp xếp và truy vết lịch sử. |
| `expires_at` | `DATETIME` | NO |  |  | IDX: idx_sessions_user_active, idx_sessions_expires_at |  | Mốc thời gian nghiệp vụ, dùng cho audit, filter, timeline hoặc SLA. |
| `revoked_at` | `DATETIME` | YES |  |  | IDX: idx_sessions_user_active, idx_sessions_revoked_at |  | Mốc thời gian nghiệp vụ, dùng cho audit, filter, timeline hoặc SLA. |
| `revoked_by` | `BIGINT UNSIGNED` | YES |  |  | FK: users(user_id) |  | Khóa ngoại liên kết tới users(user_id); dùng để join dữ liệu và đảm bảo toàn vẹn tham chiếu. |
| `revoked_reason` | `VARCHAR(255)` | YES |  |  |  |  | Lý do nghiệp vụ, dùng để giải thích quyết định/thao tác và phục vụ audit. |

**Primary Key:**
- `PRIMARY KEY (session_id)`

**Unique Constraints:**
- `uq_sessions_refresh_hash` (refresh_token_hash)

**Indexes:**
- `idx_sessions_user_active` (user_id, revoked_at, expires_at)
- `idx_sessions_portal_campus` (login_portal, selected_campus_id)
- `idx_sessions_refresh_active` (refresh_token_hash, refresh_revoked_at, refresh_expires_at)
- `idx_sessions_ip_time` (ip_address, created_at)
- `idx_sessions_expires_at` (expires_at)
- `idx_sessions_revoked_at` (revoked_at)

**Foreign Keys:**
- `fk_sessions_user`: (user_id) → `users`(user_id) ON UPDATE CASCADE ON DELETE CASCADE
- `fk_sessions_selected_campus`: (selected_campus_id) → `campuses`(campus_id) ON UPDATE CASCADE ON DELETE RESTRICT
- `fk_sessions_auth_provider`: (auth_provider_id) → `user_auth_providers`(auth_provider_id) ON UPDATE CASCADE ON DELETE SET NULL
- `fk_sessions_revoked_by`: (revoked_by) → `users`(user_id) ON UPDATE CASCADE ON DELETE SET NULL

### 4.7. `otp_tokens`

**Purpose / Table Comment:** OTP, magic link, set password token, reset password token

**Main Screens / UC Area:** OTP / Submit Visit Request / Sensitive Action Verification

**Columns:**

| Column | Type | Null | Default | Extra | Key / FK / Index | Enum Values | Notes / Meaning |
|---|---|---:|---|---|---|---|---|
| `otp_token_id` | `BIGINT UNSIGNED` | NO |  | AUTO_INCREMENT | PK |  | Khóa chính định danh duy nhất bản ghi trong bảng `otp_tokens`. |
| `user_id` | `BIGINT UNSIGNED` | YES |  |  | IDX: idx_otp_user_purpose_active; FK: users(user_id) |  | Khóa ngoại liên kết tới users(user_id); dùng để join dữ liệu và đảm bảo toàn vẹn tham chiếu. |
| `email` | `VARCHAR(150)` | NO |  |  | IDX: idx_otp_email_purpose_time, idx_otp_email_purpose_active |  | Email liên hệ/đăng nhập/thông báo; cần validate định dạng trước khi lưu. |
| `token_type` | `ENUM('OTP_CODE','MAGIC_LINK')` | NO | 'OTP_CODE' |  |  | OTP_CODE, MAGIC_LINK | Loại/phân nhóm nghiệp vụ của bản ghi, dùng cho filter, rule và UI. |
| `purpose` | `ENUM('VISIT_REQUEST_VERIFY','CHANGE_SENSITIVE_ACTION')` | NO |  |  | IDX: idx_otp_email_purpose_time, idx_otp_email_purpose_active, idx_otp_user_purpose_active | VISIT_REQUEST_VERIFY, CHANGE_SENSITIVE_ACTION | Lưu thông tin `purpose` của bảng `otp_tokens` để phục vụ màn hình và logic nghiệp vụ liên quan. |
| `token_hash` | `VARCHAR(255)` | NO |  |  | UNIQUE: uq_otp_tokens_hash |  | Token/mã xác thực hoặc khóa tạm; phải bảo vệ và giới hạn thời hạn sử dụng. |
| `expires_at` | `DATETIME` | NO |  |  | IDX: idx_otp_email_purpose_active, idx_otp_user_purpose_active |  | Mốc thời gian nghiệp vụ, dùng cho audit, filter, timeline hoặc SLA. |
| `used_at` | `DATETIME` | YES |  |  | IDX: idx_otp_email_purpose_active, idx_otp_user_purpose_active |  | Mốc thời gian nghiệp vụ, dùng cho audit, filter, timeline hoặc SLA. |
| `attempt_count` | `INT UNSIGNED` | NO | 0 |  |  |  | Số lượng/counter nghiệp vụ, dùng cho thống kê hoặc giới hạn. |
| `max_attempts` | `INT UNSIGNED` | NO | 5 |  |  |  | Lưu thông tin `max_attempts` của bảng `otp_tokens` để phục vụ màn hình và logic nghiệp vụ liên quan. |
| `resend_count` | `INT UNSIGNED` | NO | 0 |  |  |  | Số lượng/counter nghiệp vụ, dùng cho thống kê hoặc giới hạn. |
| `ip_address` | `VARCHAR(45)` | YES |  |  | IDX: idx_otp_ip_time |  | Địa chỉ IP của client, phục vụ audit/security log. |
| `user_agent` | `VARCHAR(500)` | YES |  |  |  |  | Thông tin trình duyệt/thiết bị, phục vụ audit/security log. |
| `created_at` | `DATETIME` | NO | CURRENT_TIMESTAMP |  | IDX: idx_otp_email_purpose_time, idx_otp_ip_time |  | Thời điểm tạo bản ghi; phục vụ audit, sắp xếp và truy vết lịch sử. |

**Primary Key:**
- `PRIMARY KEY (otp_token_id)`

**Unique Constraints:**
- `uq_otp_tokens_hash` (token_hash)

**Indexes:**
- `idx_otp_email_purpose_time` (email, purpose, created_at)
- `idx_otp_email_purpose_active` (email, purpose, used_at, expires_at)
- `idx_otp_user_purpose_active` (user_id, purpose, used_at, expires_at)
- `idx_otp_ip_time` (ip_address, created_at)

**Foreign Keys:**
- `fk_otp_tokens_user`: (user_id) → `users`(user_id) ON UPDATE CASCADE ON DELETE CASCADE

### 4.8. `login_logs`

**Purpose / Table Comment:** Lịch sử đăng nhập

**Main Screens / UC Area:** Security Audit / Login Monitoring

**Columns:**

| Column | Type | Null | Default | Extra | Key / FK / Index | Enum Values | Notes / Meaning |
|---|---|---:|---|---|---|---|---|
| `login_log_id` | `BIGINT UNSIGNED` | NO |  | AUTO_INCREMENT | PK |  | Khóa chính định danh duy nhất bản ghi trong bảng `login_logs`. |
| `user_id` | `BIGINT UNSIGNED` | YES |  |  | IDX: idx_login_logs_user_time; FK: users(user_id) |  | Khóa ngoại liên kết tới users(user_id); dùng để join dữ liệu và đảm bảo toàn vẹn tham chiếu. |
| `email` | `VARCHAR(150)` | NO |  |  | IDX: idx_login_logs_email_status_time |  | Email liên hệ/đăng nhập/thông báo; cần validate định dạng trước khi lưu. |
| `login_portal` | `ENUM('VISITOR','INTERNAL')` | NO |  |  | IDX: idx_login_logs_portal_campus | VISITOR, INTERNAL | Lưu thông tin `login_portal` của bảng `login_logs` để phục vụ màn hình và logic nghiệp vụ liên quan. |
| `selected_campus_id` | `BIGINT UNSIGNED` | YES |  |  | IDX: idx_login_logs_portal_campus; FK: campuses(campus_id) |  | Khóa ngoại liên kết tới campuses(campus_id); dùng để join dữ liệu và đảm bảo toàn vẹn tham chiếu. |
| `provider_type` | `ENUM('LOCAL_PASSWORD','GOOGLE_SSO','FEID')` | YES |  |  | IDX: idx_login_logs_provider_time | LOCAL_PASSWORD, GOOGLE_SSO, FEID | Loại/phân nhóm nghiệp vụ của bản ghi, dùng cho filter, rule và UI. |
| `status` | `ENUM('SUCCESS','FAILED','BLOCKED')` | NO |  |  | IDX: idx_login_logs_email_status_time, idx_login_logs_ip_status_time | SUCCESS, FAILED, BLOCKED | Trạng thái vòng đời của bản ghi; dùng cho filter, badge và chặn thao tác không hợp lệ. |
| `failure_reason` | `VARCHAR(255)` | YES |  |  |  |  | Lý do nghiệp vụ, dùng để giải thích quyết định/thao tác và phục vụ audit. |
| `ip_address` | `VARCHAR(45)` | YES |  |  | IDX: idx_login_logs_ip_status_time |  | Địa chỉ IP của client, phục vụ audit/security log. |
| `user_agent` | `VARCHAR(500)` | YES |  |  |  |  | Thông tin trình duyệt/thiết bị, phục vụ audit/security log. |
| `session_id` | `BIGINT UNSIGNED` | YES |  |  |  |  | Mã định danh/tham chiếu tới `session` hoặc entity liên quan. |
| `created_at` | `DATETIME` | NO | CURRENT_TIMESTAMP |  | IDX: idx_login_logs_user_time, idx_login_logs_email_status_time, idx_login_logs_ip_status_time, idx_login_logs_provider_time |  | Thời điểm tạo bản ghi; phục vụ audit, sắp xếp và truy vết lịch sử. |

**Primary Key:**
- `PRIMARY KEY (login_log_id)`

**Indexes:**
- `idx_login_logs_user_time` (user_id, created_at)
- `idx_login_logs_email_status_time` (email, status, created_at)
- `idx_login_logs_ip_status_time` (ip_address, status, created_at)
- `idx_login_logs_portal_campus` (login_portal, selected_campus_id)
- `idx_login_logs_provider_time` (provider_type, created_at)

**Foreign Keys:**
- `fk_login_logs_user`: (user_id) → `users`(user_id) ON UPDATE CASCADE ON DELETE SET NULL
- `fk_login_logs_campus`: (selected_campus_id) → `campuses`(campus_id) ON UPDATE CASCADE ON DELETE SET NULL

### 4.9. `security_events`

**Purpose / Table Comment:** SSO-only security events: portal/campus validation, Visitor auto-provisioning, and session lifecycle. No local password tracking and no metadata JSON.

**Main Screens / UC Area:** Security Audit / SSO Portal Validation

**Columns:**

| Column | Type | Null | Default | Extra | Key / FK / Index | Enum Values | Notes / Meaning |
|---|---|---:|---|---|---|---|---|
| `security_event_id` | `BIGINT UNSIGNED` | NO |  | AUTO_INCREMENT | PK |  | Khóa chính định danh duy nhất bản ghi trong bảng `security_events`. |
| `user_id` | `BIGINT UNSIGNED` | YES |  |  | IDX: idx_security_user_time; FK: users(user_id) |  | Khóa ngoại liên kết tới users(user_id); dùng để join dữ liệu và đảm bảo toàn vẹn tham chiếu. |
| `email_snapshot` | `VARCHAR(150)` | YES |  |  | IDX: idx_security_email_time |  | Email nhận từ SSO hoặc email đang được kiểm tra tại thời điểm xảy ra sự kiện |
| `event_type` | `ENUM(<br>    'SSO_LOGIN',<br>    'PORTAL_VALIDATION',<br>    'CAMPUS_VALIDATION',<br>    'VISITOR_AUTO_PROVISION',<br>    'SESSION_CREATED',<br>    'SESSION_REVOKED',<br>    'SESSION_EXPIRED',<br>    'TOKEN_REFRESH',<br>    'SECURITY_POLICY_CHECK'<br>  )` | NO |  |  | IDX: idx_security_type_result_time | SSO_LOGIN, PORTAL_VALIDATION, CAMPUS_VALIDATION, VISITOR_AUTO_PROVISION, SESSION_CREATED, SESSION_REVOKED, SESSION_EXPIRED, TOKEN_REFRESH, SECURITY_POLICY_CHECK | Loại sự kiện bảo mật theo mô hình SSO-only |
| `result` | `ENUM('SUCCESS','FAILED','BLOCKED')` | NO | 'SUCCESS' |  | IDX: idx_security_type_result_time | SUCCESS, FAILED, BLOCKED | Kết quả xử lý sự kiện |
| `failure_reason_code` | `ENUM(<br>    'ACCOUNT_NOT_FOUND',<br>    'ACCOUNT_DISABLED',<br>    'PORTAL_MISMATCH',<br>    'CAMPUS_MISMATCH',<br>    'ROLE_MISMATCH',<br>    'SSO_PROVIDER_ERROR',<br>    'INVALID_SSO_CLAIMS',<br>    'VISITOR_AUTO_PROVISION_DISABLED',<br>    'SESSION_EXPIRED',<br>    'TOKEN_REVOKED',<br>    'SUSPICIOUS_IP',<br>    'UNKNOWN'<br>  )` | YES |  |  | IDX: idx_security_failure_reason_time | ACCOUNT_NOT_FOUND, ACCOUNT_DISABLED, PORTAL_MISMATCH, CAMPUS_MISMATCH, ROLE_MISMATCH, SSO_PROVIDER_ERROR, INVALID_SSO_CLAIMS, VISITOR_AUTO_PROVISION_DISABLED, SESSION_EXPIRED, TOKEN_REVOKED, SUSPICIOUS_IP, UNKNOWN | Mã lý do thất bại/chặn; NULL khi SUCCESS |
| `severity` | `ENUM('LOW','MEDIUM','HIGH','CRITICAL')` | NO | 'LOW' |  | IDX: idx_security_severity_time | LOW, MEDIUM, HIGH, CRITICAL | Lưu thông tin `severity` của bảng `security_events` để phục vụ màn hình và logic nghiệp vụ liên quan. |
| `login_portal` | `ENUM('VISITOR','INTERNAL')` | YES |  |  | IDX: idx_security_portal_campus_time | VISITOR, INTERNAL | Portal được dùng khi phát sinh sự kiện |
| `selected_campus_id` | `BIGINT UNSIGNED` | YES |  |  | IDX: idx_security_portal_campus_time; FK: campuses(campus_id) |  | Campus người dùng chọn ở Internal Portal; NULL với Visitor Portal |
| `provider_type` | `ENUM('GOOGLE_SSO','FEID')` | YES |  |  |  | GOOGLE_SSO, FEID | Nguồn định danh SSO; không dùng LOCAL_PASSWORD trong security_events |
| `ip_address` | `VARCHAR(45)` | YES |  |  | IDX: idx_security_ip_time |  | Địa chỉ IP của client, phục vụ audit/security log. |
| `user_agent` | `VARCHAR(500)` | YES |  |  |  |  | Thông tin trình duyệt/thiết bị, phục vụ audit/security log. |
| `session_id` | `BIGINT UNSIGNED` | YES |  |  | IDX: idx_security_session_time; FK: user_sessions(session_id) |  | Khóa ngoại liên kết tới user_sessions(session_id); dùng để join dữ liệu và đảm bảo toàn vẹn tham chiếu. |
| `detail_text` | `TEXT` | YES |  |  |  |  | Ghi chú debug ngắn, không lưu JSON metadata |
| `created_at` | `DATETIME` | NO | CURRENT_TIMESTAMP |  | IDX: idx_security_user_time, idx_security_email_time, idx_security_type_result_time, idx_security_portal_campus_time, idx_security_failure_reason_time, idx_security_ip_time, idx_security_severity_time, idx_security_session_time |  | Thời điểm tạo bản ghi; phục vụ audit, sắp xếp và truy vết lịch sử. |

**Primary Key:**
- `PRIMARY KEY (security_event_id)`

**Indexes:**
- `idx_security_user_time` (user_id, created_at)
- `idx_security_email_time` (email_snapshot, created_at)
- `idx_security_type_result_time` (event_type, result, created_at)
- `idx_security_portal_campus_time` (login_portal, selected_campus_id, created_at)
- `idx_security_failure_reason_time` (failure_reason_code, created_at)
- `idx_security_ip_time` (ip_address, created_at)
- `idx_security_severity_time` (severity, created_at)
- `idx_security_session_time` (session_id, created_at)

**Foreign Keys:**
- `fk_security_events_user`: (user_id) → `users`(user_id) ON UPDATE CASCADE ON DELETE SET NULL
- `fk_security_events_selected_campus`: (selected_campus_id) → `campuses`(campus_id) ON UPDATE CASCADE ON DELETE SET NULL
- `fk_security_events_session`: (session_id) → `user_sessions`(session_id) ON UPDATE CASCADE ON DELETE SET NULL

### 4.10. `files`

**Purpose / Table Comment:** File metadata only. Binary file is stored outside DB.

**Main Screens / UC Area:** File Metadata / Documents / Gallery / News / Minutes

**Columns:**

| Column | Type | Null | Default | Extra | Key / FK / Index | Enum Values | Notes / Meaning |
|---|---|---:|---|---|---|---|---|
| `file_id` | `BIGINT UNSIGNED` | NO |  | AUTO_INCREMENT | PK |  | Khóa chính định danh duy nhất bản ghi trong bảng `files`. |
| `storage_provider` | `ENUM('LOCAL','S3','AZURE','GCS','GOOGLE_DRIVE','OTHER')` | NO | 'LOCAL' |  |  | LOCAL, S3, AZURE, GCS, GOOGLE_DRIVE, OTHER | Lưu thông tin `storage_provider` của bảng `files` để phục vụ màn hình và logic nghiệp vụ liên quan. |
| `bucket_name` | `VARCHAR(150)` | YES |  |  |  |  | Lưu thông tin `bucket_name` của bảng `files` để phục vụ màn hình và logic nghiệp vụ liên quan. |
| `object_key` | `VARCHAR(700)` | NO |  |  | UNIQUE: uq_files_object_key |  | Max 700 chars to keep UNIQUE index safe under utf8mb4 |
| `original_filename` | `VARCHAR(255)` | NO |  |  |  |  | Lưu thông tin `original_filename` của bảng `files` để phục vụ màn hình và logic nghiệp vụ liên quan. |
| `mime_type` | `VARCHAR(150)` | YES |  |  | IDX: idx_files_mime_time |  | Loại/phân nhóm nghiệp vụ của bản ghi, dùng cho filter, rule và UI. |
| `file_size` | `BIGINT UNSIGNED` | YES |  |  |  |  | Lưu thông tin `file_size` của bảng `files` để phục vụ màn hình và logic nghiệp vụ liên quan. |
| `checksum_sha256` | `CHAR(64)` | YES |  |  | IDX: idx_files_checksum |  | Lưu thông tin `checksum_sha256` của bảng `files` để phục vụ màn hình và logic nghiệp vụ liên quan. |
| `uploaded_by` | `BIGINT UNSIGNED` | YES |  |  | IDX: idx_files_uploaded_by; FK: users(user_id) |  | Khóa ngoại liên kết tới users(user_id); dùng để join dữ liệu và đảm bảo toàn vẹn tham chiếu. |
| `uploaded_at` | `DATETIME` | NO | CURRENT_TIMESTAMP |  | IDX: idx_files_uploaded_by, idx_files_mime_time, idx_files_purpose_time |  | Mốc thời gian nghiệp vụ, dùng cho audit, filter, timeline hoặc SLA. |
| `external_file_id` | `VARCHAR(255)` | YES |  |  | IDX: idx_files_external_file_id |  | External provider file id, e.g., Google Drive file id |
| `web_view_url` | `VARCHAR(700)` | YES |  |  |  |  | Open/view URL from external storage provider |
| `download_url` | `VARCHAR(700)` | YES |  |  |  |  | Direct download URL when provider allows it |
| `thumbnail_url` | `VARCHAR(700)` | YES |  |  |  |  | Thumbnail URL for image/video preview |
| `file_purpose` | `VARCHAR(100)` | YES |  |  | IDX: idx_files_purpose_time |  | Technical/business file purpose used by referencing entity |

**Primary Key:**
- `PRIMARY KEY (file_id)`

**Unique Constraints:**
- `uq_files_object_key` (object_key)

**Indexes:**
- `idx_files_uploaded_by` (uploaded_by, uploaded_at)
- `idx_files_mime_time` (mime_type, uploaded_at)
- `idx_files_checksum` (checksum_sha256)
- `idx_files_external_file_id` (external_file_id)
- `idx_files_purpose_time` (file_purpose, uploaded_at)

**Foreign Keys:**
- `fk_files_uploaded_by`: (uploaded_by) → `users`(user_id) ON UPDATE CASCADE ON DELETE SET NULL

### 4.11. `partners`

**Purpose / Table Comment:** Hồ sơ đối tác

**Main Screens / UC Area:** Partner Management / Public Partners

**Columns:**

| Column | Type | Null | Default | Extra | Key / FK / Index | Enum Values | Notes / Meaning |
|---|---|---:|---|---|---|---|---|
| `partner_id` | `BIGINT UNSIGNED` | NO |  | AUTO_INCREMENT | PK |  | Khóa chính định danh duy nhất bản ghi trong bảng `partners`. |
| `partner_code` | `VARCHAR(50)` | YES |  |  | UNIQUE: uq_partners_code |  | Mã nghiệp vụ ngắn, ổn định, dùng cho seed, filter, rule và tích hợp. |
| `name` | `VARCHAR(200)` | NO |  |  | IDX: ft_partners_search |  | Tên hiển thị/chính thức của bản ghi. |
| `short_name` | `VARCHAR(100)` | YES |  |  | IDX: ft_partners_search |  | Lưu thông tin `short_name` của bảng `partners` để phục vụ màn hình và logic nghiệp vụ liên quan. |
| `country` | `VARCHAR(100)` | YES |  |  | IDX: idx_partners_country |  | Số lượng/counter nghiệp vụ, dùng cho thống kê hoặc giới hạn. |
| `city` | `VARCHAR(100)` | YES |  |  |  |  | Lưu thông tin `city` của bảng `partners` để phục vụ màn hình và logic nghiệp vụ liên quan. |
| `website_url` | `VARCHAR(500)` | YES |  |  |  |  | URL/đường dẫn phục vụ hiển thị hoặc truy cập tài nguyên. |
| `partner_type` | `ENUM('UNIVERSITY','COMPANY','GOVERNMENT','NGO','OTHER')` | NO | 'UNIVERSITY' |  | IDX: idx_partners_type_status | UNIVERSITY, COMPANY, GOVERNMENT, NGO, OTHER | Loại/phân nhóm nghiệp vụ của bản ghi, dùng cho filter, rule và UI. |
| `cooperation_status` | `ENUM('POTENTIAL','ACTIVE','INACTIVE','BLACKLISTED')` | NO | 'POTENTIAL' |  | IDX: idx_partners_status, idx_partners_type_status | POTENTIAL, ACTIVE, INACTIVE, BLACKLISTED | Trạng thái nghiệp vụ của bản ghi, dùng để kiểm soát flow/action/badge. |
| `description` | `TEXT` | YES |  |  | IDX: ft_partners_search |  | Mô tả chi tiết của bản ghi. |
| `logo_file_id` | `BIGINT UNSIGNED` | YES |  |  | IDX: idx_partners_logo_file; FK: files(file_id) |  | Partner logo file, references files.file_id |
| `cover_file_id` | `BIGINT UNSIGNED` | YES |  |  | IDX: idx_partners_cover_file; FK: files(file_id) |  | Partner cover/banner file, references files.file_id |
| `address` | `VARCHAR(500)` | YES |  |  |  |  | Lưu thông tin `address` của bảng `partners` để phục vụ màn hình và logic nghiệp vụ liên quan. |
| `public_slug` | `VARCHAR(180)` | YES |  |  | UNIQUE: uq_partners_public_slug |  | Public URL slug for partner profile |
| `profile_status` | `ENUM('DRAFT','PENDING_APPROVAL','APPROVED','REJECTED')` | NO | 'APPROVED' |  | IDX: idx_partners_profile_status | DRAFT, PENDING_APPROVAL, APPROVED, REJECTED | Trạng thái nghiệp vụ của bản ghi, dùng để kiểm soát flow/action/badge. |
| `review_note` | `TEXT` | YES |  |  |  |  | Lưu thông tin `review_note` của bảng `partners` để phục vụ màn hình và logic nghiệp vụ liên quan. |
| `reviewed_by` | `BIGINT UNSIGNED` | YES |  |  | IDX: idx_partners_reviewed_by; FK: users(user_id) |  | Khóa ngoại liên kết tới users(user_id); dùng để join dữ liệu và đảm bảo toàn vẹn tham chiếu. |
| `reviewed_at` | `DATETIME` | YES |  |  | IDX: idx_partners_reviewed_by |  | Mốc thời gian nghiệp vụ, dùng cho audit, filter, timeline hoặc SLA. |
| `visibility` | `ENUM('PRIVATE','INTERNAL','PUBLIC')` | NO | 'PUBLIC' |  | IDX: idx_partners_visibility | PRIVATE, INTERNAL, PUBLIC | Lưu thông tin `visibility` của bảng `partners` để phục vụ màn hình và logic nghiệp vụ liên quan. |
| `created_at` | `DATETIME` | NO | CURRENT_TIMESTAMP |  | IDX: idx_partners_created_at |  | Thời điểm tạo bản ghi; phục vụ audit, sắp xếp và truy vết lịch sử. |
| `created_by` | `BIGINT UNSIGNED` | YES |  |  |  |  | User tạo bản ghi; phục vụ audit và kiểm tra trách nhiệm thao tác. |
| `updated_at` | `DATETIME` | YES | NULL | ON UPDATE CURRENT_TIMESTAMP |  |  | Thời điểm cập nhật gần nhất; tự động cập nhật khi SQL có ON UPDATE CURRENT_TIMESTAMP. |
| `updated_by` | `BIGINT UNSIGNED` | YES |  |  |  |  | User cập nhật gần nhất; phục vụ audit. |

**Primary Key:**
- `PRIMARY KEY (partner_id)`

**Unique Constraints:**
- `uq_partners_code` (partner_code)
- `uq_partners_public_slug` (public_slug)

**Indexes:**
- `idx_partners_country` (country)
- `idx_partners_status` (cooperation_status)
- `idx_partners_type_status` (partner_type, cooperation_status)
- `idx_partners_created_at` (created_at)
- `idx_partners_profile_status` (profile_status)
- `idx_partners_visibility` (visibility)
- `idx_partners_logo_file` (logo_file_id)
- `idx_partners_cover_file` (cover_file_id)
- `idx_partners_reviewed_by` (reviewed_by, reviewed_at)
- `ft_partners_search` (name, short_name, description)

**Foreign Keys:**
- `fk_partners_logo_file`: (logo_file_id) → `files`(file_id) ON UPDATE CASCADE ON DELETE SET NULL
- `fk_partners_cover_file`: (cover_file_id) → `files`(file_id) ON UPDATE CASCADE ON DELETE SET NULL
- `fk_partners_reviewed_by`: (reviewed_by) → `users`(user_id) ON UPDATE CASCADE ON DELETE SET NULL

### 4.12. `partner_contacts`

**Purpose / Table Comment:** Người liên hệ đối tác. OCR final confirmed data saved here.

**Main Screens / UC Area:** Partner Contact / Business Card OCR

**Columns:**

| Column | Type | Null | Default | Extra | Key / FK / Index | Enum Values | Notes / Meaning |
|---|---|---:|---|---|---|---|---|
| `contact_id` | `BIGINT UNSIGNED` | NO |  | AUTO_INCREMENT | PK |  | Khóa chính định danh duy nhất bản ghi trong bảng `partner_contacts`. |
| `partner_id` | `BIGINT UNSIGNED` | NO |  |  | UNIQUE: uq_partner_contacts_partner_email; IDX: idx_partner_contacts_partner; FK: partners(partner_id) |  | Khóa ngoại liên kết tới partners(partner_id); dùng để join dữ liệu và đảm bảo toàn vẹn tham chiếu. |
| `full_name` | `VARCHAR(150)` | NO |  |  |  |  | Họ tên đầy đủ của người dùng/người tham gia. |
| `email` | `VARCHAR(150)` | YES |  |  | UNIQUE: uq_partner_contacts_partner_email; IDX: idx_partner_contacts_email |  | Email liên hệ/đăng nhập/thông báo; cần validate định dạng trước khi lưu. |
| `phone` | `VARCHAR(50)` | YES |  |  |  |  | Số điện thoại liên hệ. |
| `job_title` | `VARCHAR(150)` | YES |  |  |  |  | Lưu thông tin `job_title` của bảng `partner_contacts` để phục vụ màn hình và logic nghiệp vụ liên quan. |
| `department_name` | `VARCHAR(150)` | YES |  |  |  |  | Lưu thông tin `department_name` của bảng `partner_contacts` để phục vụ màn hình và logic nghiệp vụ liên quan. |
| `source_type` | `ENUM('MANUAL','BUSINESS_CARD_OCR','IMPORT')` | NO | 'MANUAL' |  | IDX: idx_partner_contacts_source_type | MANUAL, BUSINESS_CARD_OCR, IMPORT | Loại/phân nhóm nghiệp vụ của bản ghi, dùng cho filter, rule và UI. |
| `scanned_card_file_id` | `BIGINT UNSIGNED` | YES |  |  | IDX: idx_partner_contacts_scanned_card; FK: files(file_id) |  | Khóa ngoại liên kết tới files(file_id); dùng để join dữ liệu và đảm bảo toàn vẹn tham chiếu. |
| `ocr_confidence` | `DECIMAL(5,2)` | YES |  |  |  |  | Lưu thông tin `ocr_confidence` của bảng `partner_contacts` để phục vụ màn hình và logic nghiệp vụ liên quan. |
| `note` | `TEXT` | YES |  |  |  |  | Ghi chú nghiệp vụ bổ sung. |
| `is_primary` | `BOOLEAN` | NO | FALSE |  |  |  | Cờ boolean đánh dấu trạng thái hoặc điều kiện nghiệp vụ. |
| `status` | `ENUM('ACTIVE','INACTIVE')` | NO | 'ACTIVE' |  | IDX: idx_partner_contacts_status | ACTIVE, INACTIVE | Trạng thái vòng đời của bản ghi; dùng cho filter, badge và chặn thao tác không hợp lệ. |
| `created_at` | `DATETIME` | NO | CURRENT_TIMESTAMP |  |  |  | Thời điểm tạo bản ghi; phục vụ audit, sắp xếp và truy vết lịch sử. |
| `created_by` | `BIGINT UNSIGNED` | YES |  |  |  |  | User tạo bản ghi; phục vụ audit và kiểm tra trách nhiệm thao tác. |
| `updated_at` | `DATETIME` | YES | NULL | ON UPDATE CURRENT_TIMESTAMP |  |  | Thời điểm cập nhật gần nhất; tự động cập nhật khi SQL có ON UPDATE CURRENT_TIMESTAMP. |
| `updated_by` | `BIGINT UNSIGNED` | YES |  |  |  |  | User cập nhật gần nhất; phục vụ audit. |

**Primary Key:**
- `PRIMARY KEY (contact_id)`

**Unique Constraints:**
- `uq_partner_contacts_partner_email` (partner_id, email)

**Indexes:**
- `idx_partner_contacts_partner` (partner_id)
- `idx_partner_contacts_email` (email)
- `idx_partner_contacts_status` (status)
- `idx_partner_contacts_source_type` (source_type)
- `idx_partner_contacts_scanned_card` (scanned_card_file_id)

**Foreign Keys:**
- `fk_partner_contacts_partner`: (partner_id) → `partners`(partner_id) ON UPDATE CASCADE ON DELETE RESTRICT
- `fk_partner_contacts_scanned_card`: (scanned_card_file_id) → `files`(file_id) ON UPDATE CASCADE ON DELETE SET NULL

### 4.13. `documents`

**Purpose / Table Comment:** Tài liệu nghiệp vụ. partner_documents/reports/logistics documents merged by owner_type.

**Main Screens / UC Area:** Document Management / Archive

**Columns:**

| Column | Type | Null | Default | Extra | Key / FK / Index | Enum Values | Notes / Meaning |
|---|---|---:|---|---|---|---|---|
| `document_id` | `BIGINT UNSIGNED` | NO |  | AUTO_INCREMENT | PK |  | Khóa chính định danh duy nhất bản ghi trong bảng `documents`. |
| `file_id` | `BIGINT UNSIGNED` | NO |  |  | FK: files(file_id) |  | Khóa ngoại liên kết tới files(file_id); dùng để join dữ liệu và đảm bảo toàn vẹn tham chiếu. |
| `owner_type` | `ENUM('GENERAL','VISIT','PARTNER','MINUTES','NEWS','LOGISTICS','REPORT')` | NO | 'GENERAL' |  | IDX: idx_documents_owner | GENERAL, VISIT, PARTNER, MINUTES, NEWS, LOGISTICS, REPORT | Loại/phân nhóm nghiệp vụ của bản ghi, dùng cho filter, rule và UI. |
| `owner_id` | `BIGINT UNSIGNED` | YES |  |  | IDX: idx_documents_owner |  | Mã định danh/tham chiếu tới `owner` hoặc entity liên quan. |
| `campus_id` | `BIGINT UNSIGNED` | YES |  |  | IDX: idx_documents_campus_status; FK: campuses(campus_id) |  | Khóa ngoại liên kết tới campuses(campus_id); dùng để join dữ liệu và đảm bảo toàn vẹn tham chiếu. |
| `title` | `VARCHAR(255)` | NO |  |  | IDX: ft_documents_search |  | Tiêu đề/nội dung chính hiển thị trên UI. |
| `description` | `TEXT` | YES |  |  | IDX: ft_documents_search |  | Mô tả chi tiết của bản ghi. |
| `document_category` | `VARCHAR(100)` | YES |  |  | IDX: idx_documents_category_status |  | Lưu thông tin `document_category` của bảng `documents` để phục vụ màn hình và logic nghiệp vụ liên quan. |
| `status` | `ENUM('DRAFT','PUBLISHED','ARCHIVED')` | NO | 'DRAFT' |  | IDX: idx_documents_campus_status, idx_documents_category_status | DRAFT, PUBLISHED, ARCHIVED | Trạng thái vòng đời của bản ghi; dùng cho filter, badge và chặn thao tác không hợp lệ. |
| `created_at` | `DATETIME` | NO | CURRENT_TIMESTAMP |  | IDX: idx_documents_created_by_time |  | Thời điểm tạo bản ghi; phục vụ audit, sắp xếp và truy vết lịch sử. |
| `created_by` | `BIGINT UNSIGNED` | YES |  |  | IDX: idx_documents_created_by_time; FK: users(user_id) |  | User tạo bản ghi; phục vụ audit và kiểm tra trách nhiệm thao tác. |
| `updated_at` | `DATETIME` | YES | NULL | ON UPDATE CURRENT_TIMESTAMP |  |  | Thời điểm cập nhật gần nhất; tự động cập nhật khi SQL có ON UPDATE CURRENT_TIMESTAMP. |
| `updated_by` | `BIGINT UNSIGNED` | YES |  |  |  |  | User cập nhật gần nhất; phục vụ audit. |

**Primary Key:**
- `PRIMARY KEY (document_id)`

**Indexes:**
- `idx_documents_owner` (owner_type, owner_id)
- `idx_documents_campus_status` (campus_id, status)
- `idx_documents_category_status` (document_category, status)
- `idx_documents_created_by_time` (created_by, created_at)
- `ft_documents_search` (title, description)

**Foreign Keys:**
- `fk_documents_file`: (file_id) → `files`(file_id) ON UPDATE CASCADE ON DELETE RESTRICT
- `fk_documents_campus`: (campus_id) → `campuses`(campus_id) ON UPDATE CASCADE ON DELETE SET NULL
- `fk_documents_created_by`: (created_by) → `users`(user_id) ON UPDATE CASCADE ON DELETE SET NULL

### 4.14. `visit_requests`

**Purpose / Table Comment:** Submit Visit Request / Delegation Management / Approval / Cancellation

**Main Screens / UC Area:** Submit Visit Request / Delegation Management / Approval / Cancellation

**Columns:**

| Column | Type | Null | Default | Extra | Key / FK / Index | Enum Values | Notes / Meaning |
|---|---|---:|---|---|---|---|---|
| `visit_request_id` | `BIGINT UNSIGNED` | NO |  | AUTO_INCREMENT | PK |  | Khóa chính định danh duy nhất bản ghi trong bảng `visit_requests`. |
| `request_code` | `VARCHAR(50)` | NO |  |  | UNIQUE: uq_visit_requests_code; IDX: ft_visit_requests_frontend_search |  | Mã nghiệp vụ ngắn, ổn định, dùng cho seed, filter, rule và tích hợp. |
| `visitor_user_id` | `BIGINT UNSIGNED` | YES |  |  | IDX: idx_visit_requests_visitor; FK: users(user_id) |  | Visitor user/account created or linked for the registrant |
| `partner_id` | `BIGINT UNSIGNED` | YES |  |  | IDX: idx_visit_requests_partner; FK: partners(partner_id) |  | Khóa ngoại liên kết tới partners(partner_id); dùng để join dữ liệu và đảm bảo toàn vẹn tham chiếu. |
| `created_source` | `ENUM('VISITOR_SUBMITTED','STAFF_CREATED')` | NO | 'VISITOR_SUBMITTED' |  | IDX: idx_visit_requests_created_source | VISITOR_SUBMITTED, STAFF_CREATED | Lưu thông tin `created_source` của bảng `visit_requests` để phục vụ màn hình và logic nghiệp vụ liên quan. |
| `registrant_organization` | `VARCHAR(200)` | NO |  |  | IDX: ft_visit_requests_frontend_search |  | Đơn vị công tác người đăng ký |
| `registrant_job_title` | `VARCHAR(150)` | NO |  |  |  |  | Chức danh/phòng ban người đăng ký |
| `registrant_phone` | `VARCHAR(50)` | NO |  |  |  |  | SĐT người đăng ký |
| `registrant_email` | `VARCHAR(150)` | NO |  |  | IDX: idx_visit_requests_registrant_email, ft_visit_requests_frontend_search |  | Email người đăng ký |
| `registrant_nationality` | `VARCHAR(100)` | NO |  |  |  |  | Quốc tịch người đăng ký |
| `visit_scope` | `ENUM('SINGLE_CAMPUS','MULTI_CAMPUS')` | NO | 'SINGLE_CAMPUS' |  | IDX: idx_visit_requests_scope_status, idx_visit_requests_visibility_scope_status_decision | SINGLE_CAMPUS, MULTI_CAMPUS | SINGLE_CAMPUS: Staff Leader duyệt request tổng; MULTI_CAMPUS: HO duyệt request tổng. Frontend/backend suy ra người duyệt từ cột này. |
| `visit_type` | `ENUM('CAMPUS_TOUR','MEETING','WORKSHOP','SIGNING_CEREMONY','EXCHANGE','OTHER')` | NO | 'CAMPUS_TOUR' |  | IDX: idx_visit_requests_visit_type | CAMPUS_TOUR, MEETING, WORKSHOP, SIGNING_CEREMONY, EXCHANGE, OTHER | Loại/phân nhóm nghiệp vụ của bản ghi, dùng cho filter, rule và UI. |
| `visit_type_other` | `VARCHAR(255)` | YES |  |  |  |  | Lưu thông tin `visit_type_other` của bảng `visit_requests` để phục vụ màn hình và logic nghiệp vụ liên quan. |
| `purpose` | `TEXT` | NO |  |  |  |  | Mục đích thăm FPTU |
| `working_content` | `TEXT` | YES |  |  |  |  | Nội dung làm việc tại FPTU |
| `contact_person_full_name` | `VARCHAR(150)` | NO |  |  | IDX: ft_visit_requests_frontend_search |  | Lưu thông tin `contact_person_full_name` của bảng `visit_requests` để phục vụ màn hình và logic nghiệp vụ liên quan. |
| `contact_person_organization` | `VARCHAR(255)` | NO |  |  | IDX: ft_visit_requests_frontend_search |  | Lưu thông tin `contact_person_organization` của bảng `visit_requests` để phục vụ màn hình và logic nghiệp vụ liên quan. |
| `contact_person_phone` | `VARCHAR(50)` | NO |  |  |  |  | Lưu thông tin `contact_person_phone` của bảng `visit_requests` để phục vụ màn hình và logic nghiệp vụ liên quan. |
| `contact_person_email` | `VARCHAR(150)` | NO |  |  | IDX: idx_visit_requests_contact_email, ft_visit_requests_frontend_search |  | Lưu thông tin `contact_person_email` của bảng `visit_requests` để phục vụ màn hình và logic nghiệp vụ liên quan. |
| `working_language` | `ENUM('VI','EN')` | NO | 'EN' |  |  | VI, EN | Ngôn ngữ sử dụng trong visit. Chỉ dùng VI/EN theo frontend hiện tại, không có lựa chọn OTHER |
| `transportation_type` | `ENUM('SELF_ARRANGED','FPTU_SUPPORT','UNKNOWN','OTHER')` | NO | 'UNKNOWN' |  |  | SELF_ARRANGED, FPTU_SUPPORT, UNKNOWN, OTHER | Loại/phân nhóm nghiệp vụ của bản ghi, dùng cho filter, rule và UI. |
| `transportation_detail` | `VARCHAR(500)` | YES |  |  |  |  | Lưu thông tin `transportation_detail` của bảng `visit_requests` để phục vụ màn hình và logic nghiệp vụ liên quan. |
| `media_consent_status` | `ENUM('AGREED','DECLINED')` | NO | 'DECLINED' |  | IDX: idx_visit_requests_media_consent | AGREED, DECLINED | Trạng thái nghiệp vụ của bản ghi, dùng để kiểm soát flow/action/badge. |
| `media_consent_note` | `TEXT` | YES |  |  |  |  | Lưu thông tin `media_consent_note` của bảng `visit_requests` để phục vụ màn hình và logic nghiệp vụ liên quan. |
| `note_to_fptu` | `TEXT` | YES |  |  |  |  | Ghi chú cho FPTU |
| `status` | `ENUM('PENDING_APPROVAL','APPROVED','REJECTED','CANCELLED')` | NO | 'PENDING_APPROVAL' |  | IDX: idx_visit_requests_status_submitted, idx_visit_requests_scope_status, idx_visit_requests_visibility_scope_status_decision | PENDING_APPROVAL, APPROVED, REJECTED, CANCELLED | Request decision status only. Visit progress is derived from visit_request_campuses.status |
| `submitted_at` | `DATETIME` | NO | CURRENT_TIMESTAMP |  | IDX: idx_visit_requests_status_submitted |  | Mốc thời gian nghiệp vụ, dùng cho audit, filter, timeline hoặc SLA. |
| `email_verified_at` | `DATETIME` | YES |  |  |  |  | Mốc thời gian nghiệp vụ, dùng cho audit, filter, timeline hoặc SLA. |
| `decided_by` | `BIGINT UNSIGNED` | YES |  |  | IDX: idx_visit_requests_decision; FK: users(user_id) |  | Người approve/reject request tổng |
| `decided_at` | `DATETIME` | YES |  |  | IDX: idx_visit_requests_visibility_scope_status_decision, idx_visit_requests_decision, idx_visit_requests_decision_role |  | Thời điểm xử lý request tổng |
| `decision_actor_role` | `ENUM('HO','STAFF_LEADER')` | YES |  |  | IDX: idx_visit_requests_visibility_scope_status_decision, idx_visit_requests_decision_role | HO, STAFF_LEADER | Vai trò người xử lý tại thời điểm quyết định |
| `decision_note` | `TEXT` | YES |  |  |  |  | Lý do/ghi chú khi approve hoặc reject |
| `cancelled_by` | `BIGINT UNSIGNED` | YES |  |  | IDX: idx_visit_requests_cancelled; FK: users(user_id) |  | Visitor hủy toàn bộ request/delegation |
| `cancelled_at` | `DATETIME` | YES |  |  | IDX: idx_visit_requests_cancelled |  | Thời điểm visitor hủy toàn bộ request/delegation |
| `cancellation_reason` | `TEXT` | YES |  |  |  |  | Lý do visitor nhập khi tự hủy toàn bộ request/delegation. Bảng tổng không lưu actor/source vì chỉ Visitor được hủy tổng. |
| `row_version` | `INT UNSIGNED` | NO | 0 |  |  |  | Optimistic concurrency token |
| `created_at` | `DATETIME` | NO | CURRENT_TIMESTAMP |  |  |  | Thời điểm tạo bản ghi; phục vụ audit, sắp xếp và truy vết lịch sử. |
| `created_by` | `BIGINT UNSIGNED` | YES |  |  |  |  | User tạo bản ghi; phục vụ audit và kiểm tra trách nhiệm thao tác. |
| `updated_at` | `DATETIME` | YES | NULL | ON UPDATE CURRENT_TIMESTAMP |  |  | Thời điểm cập nhật gần nhất; tự động cập nhật khi SQL có ON UPDATE CURRENT_TIMESTAMP. |
| `updated_by` | `BIGINT UNSIGNED` | YES |  |  |  |  | User cập nhật gần nhất; phục vụ audit. |

**Primary Key:**
- `PRIMARY KEY (visit_request_id)`

**Unique Constraints:**
- `uq_visit_requests_code` (request_code)

**Indexes:**
- `idx_visit_requests_visitor` (visitor_user_id)
- `idx_visit_requests_partner` (partner_id)
- `idx_visit_requests_status_submitted` (status, submitted_at)
- `idx_visit_requests_registrant_email` (registrant_email)
- `idx_visit_requests_scope_status` (visit_scope, status)
- `idx_visit_requests_created_source` (created_source)
- `idx_visit_requests_visit_type` (visit_type)
- `idx_visit_requests_contact_email` (contact_person_email)
- `idx_visit_requests_media_consent` (media_consent_status)
- `idx_visit_requests_visibility_scope_status_decision` (visit_scope, status, decision_actor_role, decided_at)
- `idx_visit_requests_decision` (decided_by, decided_at)
- `idx_visit_requests_decision_role` (decision_actor_role, decided_at)
- `idx_visit_requests_cancelled` (cancelled_by, cancelled_at)
- `ft_visit_requests_frontend_search` (request_code, delegation_name, registrant_full_name, registrant_organization, registrant_email, contact_person_full_name, contact_person_organization, contact_person_email)

**Foreign Keys:**
- `fk_visit_requests_visitor`: (visitor_user_id) → `users`(user_id) ON UPDATE CASCADE ON DELETE SET NULL
- `fk_visit_requests_partner`: (partner_id) → `partners`(partner_id) ON UPDATE CASCADE ON DELETE SET NULL
- `fk_visit_requests_decided_by`: (decided_by) → `users`(user_id) ON UPDATE CASCADE ON DELETE SET NULL
- `fk_visit_requests_cancelled_by`: (cancelled_by) → `users`(user_id) ON UPDATE CASCADE ON DELETE SET NULL

**Check Constraints:**
- `CHECK (TRIM(registrant_job_title) <> '')`
- `CHECK (TRIM(registrant_phone) <> '')`
- `CHECK (TRIM(registrant_nationality) <> '')`
- `CHECK (TRIM(contact_person_full_name) <> '')`
- `CHECK (TRIM(contact_person_phone) <> '')`
- `CHECK (TRIM(contact_person_email) <> '')`
- `CHECK (visit_type <> 'OTHER' OR (visit_type_other IS NOT NULL AND TRIM(visit_type_other) <> ''))`

### 4.15. `visit_request_campuses`

**Purpose / Table Comment:** Campus Visit Instance / Staff Leader Processing / Host Assignment

**Main Screens / UC Area:** Campus Visit Instance / Staff Leader Processing / Host Assignment

**Columns:**

| Column | Type | Null | Default | Extra | Key / FK / Index | Enum Values | Notes / Meaning |
|---|---|---:|---|---|---|---|---|
| `visit_instance_id` | `BIGINT UNSIGNED` | NO |  | AUTO_INCREMENT | PK |  | Khóa chính định danh duy nhất bản ghi trong bảng `visit_request_campuses`. |
| `visit_request_id` | `BIGINT UNSIGNED` | NO |  |  | UNIQUE: uq_visit_instance_request_campus; IDX: idx_visit_instances_request, idx_visit_instances_visibility_campus_request; FK: visit_requests(visit_request_id) |  | Khóa ngoại liên kết tới visit_requests(visit_request_id); dùng để join dữ liệu và đảm bảo toàn vẹn tham chiếu. |
| `campus_id` | `BIGINT UNSIGNED` | NO |  |  | UNIQUE: uq_visit_instance_request_campus; IDX: idx_visit_instances_campus_status_time, idx_visit_instances_visibility_campus_request; FK: campuses(campus_id) |  | Khóa ngoại liên kết tới campuses(campus_id); dùng để join dữ liệu và đảm bảo toàn vẹn tham chiếu. |
| `planned_start_at` | `DATETIME` | NO |  |  | IDX: idx_visit_instances_campus_status_time, idx_visit_instances_status_time |  | Ngày giờ bắt đầu dự kiến tại campus |
| `planned_end_at` | `DATETIME` | NO |  |  |  |  | Ngày giờ kết thúc dự kiến tại campus |
| `status` | `ENUM(<br>    'WAITING_REQUEST_APPROVAL',<br>    'WAITING_HOST_ASSIGNMENT',<br>    'ASSIGNED',<br>    'BEFORE_VISIT',<br>    'DURING_VISIT',<br>    'AFTER_VISIT',<br>    'CLOSED',<br>    'CANCELLED'<br>  )` | NO | 'WAITING_REQUEST_APPROVAL' |  | IDX: idx_visit_instances_campus_status_time, idx_visit_instances_status_time, idx_visit_instances_coordinator, idx_visit_instances_current_host, idx_visit_instances_visibility_campus_request | WAITING_REQUEST_APPROVAL, WAITING_HOST_ASSIGNMENT, ASSIGNED, BEFORE_VISIT, DURING_VISIT, AFTER_VISIT, CLOSED, CANCELLED | WAITING_REQUEST_APPROVAL=chờ duyệt request; WAITING_HOST_ASSIGNMENT=đã duyệt, Staff Leader đang điều phối và chờ gán host chính thức; ASSIGNED=đã có Staff host chính thức. |
| `coordinator_user_id` | `BIGINT UNSIGNED` | YES |  |  | IDX: idx_visit_instances_coordinator; FK: users(user_id) |  | Staff Leader điều phối campus instance. Với MULTI_CAMPUS, HO duyệt xong thì hệ thống gán Staff Leader campus vào đây. |
| `coordinator_assigned_by` | `BIGINT UNSIGNED` | YES |  |  | IDX: idx_visit_instances_coordinator_assigned; FK: users(user_id) |  | Người gán coordinator, thường là HO khi duyệt MULTI_CAMPUS |
| `coordinator_assigned_at` | `DATETIME` | YES |  |  | IDX: idx_visit_instances_coordinator_assigned |  | Thời điểm gán coordinator |
| `current_host_user_id` | `BIGINT UNSIGNED` | YES |  |  | IDX: idx_visit_instances_current_host, idx_visit_instances_visibility_campus_request; FK: users(user_id) |  | Staff host chính thức của campus instance. Chỉ set một lần khi Staff Leader gán host; không hỗ trợ chuyển host. |
| `host_assigned_by` | `BIGINT UNSIGNED` | YES |  |  | IDX: idx_visit_instances_host_assigned; FK: users(user_id) |  | Staff Leader gán host chính thức |
| `host_assigned_at` | `DATETIME` | YES |  |  | IDX: idx_visit_instances_host_assigned |  | Thời điểm host chính thức được gán |
| `closed_by` | `BIGINT UNSIGNED` | YES |  |  | FK: users(user_id) |  | Khóa ngoại liên kết tới users(user_id); dùng để join dữ liệu và đảm bảo toàn vẹn tham chiếu. |
| `closed_at` | `DATETIME` | YES |  |  |  |  | Mốc thời gian nghiệp vụ, dùng cho audit, filter, timeline hoặc SLA. |
| `close_note` | `TEXT` | YES |  |  |  |  | Lưu thông tin `close_note` của bảng `visit_request_campuses` để phục vụ màn hình và logic nghiệp vụ liên quan. |
| `cancelled_by` | `BIGINT UNSIGNED` | YES |  |  | IDX: idx_visit_instances_cancelled; FK: users(user_id) |  | Visitor hoặc Host thực hiện hủy campus instance |
| `cancelled_at` | `DATETIME` | YES |  |  | IDX: idx_visit_instances_cancelled, idx_visit_instances_cancel_actor |  | Thời điểm hủy campus instance |
| `cancellation_actor_type` | `ENUM('VISITOR','HOST')` | YES |  |  | IDX: idx_visit_instances_cancel_actor | VISITOR, HOST | VISITOR=khách tự hủy; HOST=Staff được gán làm host hủy thay khách theo xác nhận ngoài hệ thống |
| `cancellation_source` | `ENUM('SELF_SERVICE','EXTERNAL_CONFIRMATION')` | YES |  |  |  | SELF_SERVICE, EXTERNAL_CONFIRMATION | SELF_SERVICE=Visitor tự hủy; EXTERNAL_CONFIRMATION=Host hủy sau khi khách xác nhận ngoài hệ thống |
| `cancellation_reason` | `TEXT` | YES |  |  |  |  | Lý do hủy; nếu EXTERNAL_CONFIRMATION thì ghi rõ kênh xác nhận, thời điểm, người xác nhận và lý do. |
| `row_version` | `INT UNSIGNED` | NO | 0 |  |  |  | Optimistic concurrency token |
| `created_at` | `DATETIME` | NO | CURRENT_TIMESTAMP |  |  |  | Thời điểm tạo bản ghi; phục vụ audit, sắp xếp và truy vết lịch sử. |
| `created_by` | `BIGINT UNSIGNED` | YES |  |  |  |  | User tạo bản ghi; phục vụ audit và kiểm tra trách nhiệm thao tác. |
| `updated_at` | `DATETIME` | YES | NULL | ON UPDATE CURRENT_TIMESTAMP |  |  | Thời điểm cập nhật gần nhất; tự động cập nhật khi SQL có ON UPDATE CURRENT_TIMESTAMP. |
| `updated_by` | `BIGINT UNSIGNED` | YES |  |  |  |  | User cập nhật gần nhất; phục vụ audit. |

**Primary Key:**
- `PRIMARY KEY (visit_instance_id)`

**Unique Constraints:**
- `uq_visit_instance_request_campus` (visit_request_id, campus_id)

**Indexes:**
- `idx_visit_instances_campus_status_time` (campus_id, status, planned_start_at)
- `idx_visit_instances_request` (visit_request_id)
- `idx_visit_instances_status_time` (status, planned_start_at)
- `idx_visit_instances_coordinator` (coordinator_user_id, status)
- `idx_visit_instances_coordinator_assigned` (coordinator_assigned_by, coordinator_assigned_at)
- `idx_visit_instances_current_host` (current_host_user_id, status)
- `idx_visit_instances_host_assigned` (host_assigned_by, host_assigned_at)
- `idx_visit_instances_cancelled` (cancelled_by, cancelled_at)
- `idx_visit_instances_cancel_actor` (cancellation_actor_type, cancelled_at)
- `idx_visit_instances_visibility_campus_request` (campus_id, visit_request_id, status, current_host_user_id)

**Foreign Keys:**
- `fk_visit_instances_request`: (visit_request_id) → `visit_requests`(visit_request_id) ON UPDATE CASCADE ON DELETE RESTRICT
- `fk_visit_instances_campus`: (campus_id) → `campuses`(campus_id) ON UPDATE CASCADE ON DELETE RESTRICT
- `fk_visit_instances_coordinator`: (coordinator_user_id) → `users`(user_id) ON UPDATE CASCADE ON DELETE SET NULL
- `fk_visit_instances_coordinator_assigned_by`: (coordinator_assigned_by) → `users`(user_id) ON UPDATE CASCADE ON DELETE SET NULL
- `fk_visit_instances_current_host`: (current_host_user_id) → `users`(user_id) ON UPDATE CASCADE ON DELETE SET NULL
- `fk_visit_instances_host_assigned_by`: (host_assigned_by) → `users`(user_id) ON UPDATE CASCADE ON DELETE SET NULL
- `fk_visit_instances_closed_by`: (closed_by) → `users`(user_id) ON UPDATE CASCADE ON DELETE SET NULL
- `fk_visit_instances_cancelled_by`: (cancelled_by) → `users`(user_id) ON UPDATE CASCADE ON DELETE SET NULL

**Check Constraints:**
- `CHECK (planned_end_at > planned_start_at)`

### 4.16. `visit_guest_members`

**Purpose / Table Comment:** Danh sách từng người trong đoàn khách. Không lưu consent hình ảnh vì form đã bỏ phần xác nhận sử dụng hình ảnh/thông tin.

**Main Screens / UC Area:** Delegation Guest List / Minutes Source

**Columns:**

| Column | Type | Null | Default | Extra | Key / FK / Index | Enum Values | Notes / Meaning |
|---|---|---:|---|---|---|---|---|
| `guest_member_id` | `BIGINT UNSIGNED` | NO |  | AUTO_INCREMENT | PK |  | Khóa chính định danh duy nhất bản ghi trong bảng `visit_guest_members`. |
| `visit_request_id` | `BIGINT UNSIGNED` | NO |  |  | IDX: idx_guest_members_request, idx_guest_members_type_order; FK: visit_requests(visit_request_id) |  | Khóa ngoại liên kết tới visit_requests(visit_request_id); dùng để join dữ liệu và đảm bảo toàn vẹn tham chiếu. |
| `member_type` | `ENUM('GUEST','EXTERNAL_SUPPORT')` | NO | 'GUEST' |  | IDX: idx_guest_members_type_order | GUEST, EXTERNAL_SUPPORT | Loại/phân nhóm nghiệp vụ của bản ghi, dùng cho filter, rule và UI. |
| `full_name` | `VARCHAR(150)` | NO |  |  |  |  | Họ tên đầy đủ của người dùng/người tham gia. |
| `organization` | `VARCHAR(200)` | NO |  |  |  |  | Lưu thông tin `organization` của bảng `visit_guest_members` để phục vụ màn hình và logic nghiệp vụ liên quan. |
| `job_title` | `VARCHAR(150)` | NO |  |  |  |  | Lưu thông tin `job_title` của bảng `visit_guest_members` để phục vụ màn hình và logic nghiệp vụ liên quan. |
| `nationality` | `VARCHAR(100)` | NO |  |  |  |  | Lưu thông tin `nationality` của bảng `visit_guest_members` để phục vụ màn hình và logic nghiệp vụ liên quan. |
| `display_order` | `INT UNSIGNED` | NO | 0 |  | IDX: idx_guest_members_type_order |  | Thứ tự hiển thị trên UI. |
| `created_at` | `DATETIME` | NO | CURRENT_TIMESTAMP |  |  |  | Thời điểm tạo bản ghi; phục vụ audit, sắp xếp và truy vết lịch sử. |
| `created_by` | `BIGINT UNSIGNED` | YES |  |  |  |  | User tạo bản ghi; phục vụ audit và kiểm tra trách nhiệm thao tác. |
| `updated_at` | `DATETIME` | YES | NULL | ON UPDATE CURRENT_TIMESTAMP |  |  | Thời điểm cập nhật gần nhất; tự động cập nhật khi SQL có ON UPDATE CURRENT_TIMESTAMP. |
| `updated_by` | `BIGINT UNSIGNED` | YES |  |  |  |  | User cập nhật gần nhất; phục vụ audit. |

**Primary Key:**
- `PRIMARY KEY (guest_member_id)`

**Indexes:**
- `idx_guest_members_request` (visit_request_id)
- `idx_guest_members_type_order` (visit_request_id, member_type, display_order)

**Foreign Keys:**
- `fk_guest_members_request`: (visit_request_id) → `visit_requests`(visit_request_id) ON UPDATE CASCADE ON DELETE RESTRICT

**Check Constraints:**
- `CHECK (TRIM(full_name) <> '')`
- `CHECK (TRIM(organization) <> '')`
- `CHECK (TRIM(job_title) <> '')`
- `CHECK (TRIM(nationality) <> '')`

### 4.17. `visit_participants`

**Purpose / Table Comment:** Người nội bộ tham gia visit instance. Chỉ gồm IC_HOST, IC_SUPPORT, DEPT_SUPPORT, STUDENT. Host chính lưu bằng is_host.

**Main Screens / UC Area:** Internal Participants / Host / Department / Student Assignment

**Columns:**

| Column | Type | Null | Default | Extra | Key / FK / Index | Enum Values | Notes / Meaning |
|---|---|---:|---|---|---|---|---|
| `participant_id` | `BIGINT UNSIGNED` | NO |  | AUTO_INCREMENT | PK |  | Khóa chính định danh duy nhất bản ghi trong bảng `visit_participants`. |
| `visit_instance_id` | `BIGINT UNSIGNED` | NO |  |  | UNIQUE: uq_visit_participants_user; IDX: idx_visit_participants_one_host_lookup, idx_visit_participants_instance; FK: visit_request_campuses(visit_instance_id) |  | Khóa ngoại liên kết tới visit_request_campuses(visit_instance_id); dùng để join dữ liệu và đảm bảo toàn vẹn tham chiếu. |
| `user_id` | `BIGINT UNSIGNED` | NO |  |  | UNIQUE: uq_visit_participants_user; IDX: idx_visit_participants_user_status; FK: users(user_id) |  | Khóa ngoại liên kết tới users(user_id); dùng để join dữ liệu và đảm bảo toàn vẹn tham chiếu. |
| `participant_role` | `ENUM('IC_HOST','IC_SUPPORT','DEPT_SUPPORT','STUDENT')` | NO | 'IC_SUPPORT' |  | IDX: idx_visit_participants_role_status | IC_HOST, IC_SUPPORT, DEPT_SUPPORT, STUDENT | Lưu thông tin `participant_role` của bảng `visit_participants` để phục vụ màn hình và logic nghiệp vụ liên quan. |
| `is_host` | `BOOLEAN` | NO | FALSE |  | IDX: idx_visit_participants_one_host_lookup |  | Cờ boolean đánh dấu trạng thái hoặc điều kiện nghiệp vụ. |
| `status` | `ENUM('INVITED','ACCEPTED','DECLINED','ASSIGNED','REMOVED')` | NO | 'INVITED' |  | IDX: idx_visit_participants_user_status, idx_visit_participants_role_status | INVITED, ACCEPTED, DECLINED, ASSIGNED, REMOVED | Trạng thái vòng đời của bản ghi; dùng cho filter, badge và chặn thao tác không hợp lệ. |
| `invited_by` | `BIGINT UNSIGNED` | YES |  |  | FK: users(user_id) |  | Khóa ngoại liên kết tới users(user_id); dùng để join dữ liệu và đảm bảo toàn vẹn tham chiếu. |
| `invited_at` | `DATETIME` | YES |  |  |  |  | Mốc thời gian nghiệp vụ, dùng cho audit, filter, timeline hoặc SLA. |
| `responded_at` | `DATETIME` | YES |  |  |  |  | Mốc thời gian nghiệp vụ, dùng cho audit, filter, timeline hoặc SLA. |
| `assigned_by` | `BIGINT UNSIGNED` | YES |  |  | FK: users(user_id) |  | Khóa ngoại liên kết tới users(user_id); dùng để join dữ liệu và đảm bảo toàn vẹn tham chiếu. |
| `assigned_at` | `DATETIME` | YES |  |  |  |  | Mốc thời gian nghiệp vụ, dùng cho audit, filter, timeline hoặc SLA. |
| `note` | `TEXT` | YES |  |  |  |  | Ghi chú nghiệp vụ bổ sung. |
| `created_at` | `DATETIME` | NO | CURRENT_TIMESTAMP |  |  |  | Thời điểm tạo bản ghi; phục vụ audit, sắp xếp và truy vết lịch sử. |
| `created_by` | `BIGINT UNSIGNED` | YES |  |  |  |  | User tạo bản ghi; phục vụ audit và kiểm tra trách nhiệm thao tác. |
| `updated_at` | `DATETIME` | YES | NULL | ON UPDATE CURRENT_TIMESTAMP |  |  | Thời điểm cập nhật gần nhất; tự động cập nhật khi SQL có ON UPDATE CURRENT_TIMESTAMP. |
| `updated_by` | `BIGINT UNSIGNED` | YES |  |  |  |  | User cập nhật gần nhất; phục vụ audit. |

**Primary Key:**
- `PRIMARY KEY (participant_id)`

**Unique Constraints:**
- `uq_visit_participants_user` (visit_instance_id, user_id)

**Indexes:**
- `idx_visit_participants_one_host_lookup` (visit_instance_id, is_host)
- `idx_visit_participants_user_status` (user_id, status)
- `idx_visit_participants_instance` (visit_instance_id)
- `idx_visit_participants_role_status` (participant_role, status)

**Foreign Keys:**
- `fk_visit_participants_instance`: (visit_instance_id) → `visit_request_campuses`(visit_instance_id) ON UPDATE CASCADE ON DELETE RESTRICT
- `fk_visit_participants_user`: (user_id) → `users`(user_id) ON UPDATE CASCADE ON DELETE RESTRICT
- `fk_visit_participants_invited_by`: (invited_by) → `users`(user_id) ON UPDATE CASCADE ON DELETE SET NULL
- `fk_visit_participants_assigned_by`: (assigned_by) → `users`(user_id) ON UPDATE CASCADE ON DELETE SET NULL

### 4.18. `visit_agendas`

**Purpose / Table Comment:** Lịch trình tiếp khách

**Main Screens / UC Area:** Visit Agenda / Delegation Detail

**Columns:**

| Column | Type | Null | Default | Extra | Key / FK / Index | Enum Values | Notes / Meaning |
|---|---|---:|---|---|---|---|---|
| `agenda_id` | `BIGINT UNSIGNED` | NO |  | AUTO_INCREMENT | PK |  | Khóa chính định danh duy nhất bản ghi trong bảng `visit_agendas`. |
| `visit_instance_id` | `BIGINT UNSIGNED` | NO |  |  | UNIQUE: uq_visit_agendas_order; IDX: idx_visit_agendas_time; FK: visit_request_campuses(visit_instance_id) |  | Khóa ngoại liên kết tới visit_request_campuses(visit_instance_id); dùng để join dữ liệu và đảm bảo toàn vẹn tham chiếu. |
| `sequence_order` | `INT UNSIGNED` | NO |  |  | UNIQUE: uq_visit_agendas_order |  | Lưu thông tin `sequence_order` của bảng `visit_agendas` để phục vụ màn hình và logic nghiệp vụ liên quan. |
| `title` | `VARCHAR(255)` | NO |  |  |  |  | Tiêu đề/nội dung chính hiển thị trên UI. |
| `description` | `TEXT` | YES |  |  |  |  | Mô tả chi tiết của bản ghi. |
| `start_time` | `DATETIME` | NO |  |  | IDX: idx_visit_agendas_time, idx_visit_agendas_responsible |  | Lưu thông tin `start_time` của bảng `visit_agendas` để phục vụ màn hình và logic nghiệp vụ liên quan. |
| `end_time` | `DATETIME` | YES |  |  |  |  | Lưu thông tin `end_time` của bảng `visit_agendas` để phục vụ màn hình và logic nghiệp vụ liên quan. |
| `location` | `VARCHAR(255)` | YES |  |  |  |  | Lưu thông tin `location` của bảng `visit_agendas` để phục vụ màn hình và logic nghiệp vụ liên quan. |
| `responsible_user_id` | `BIGINT UNSIGNED` | YES |  |  | IDX: idx_visit_agendas_responsible; FK: users(user_id) |  | Khóa ngoại liên kết tới users(user_id); dùng để join dữ liệu và đảm bảo toàn vẹn tham chiếu. |
| `created_at` | `DATETIME` | NO | CURRENT_TIMESTAMP |  |  |  | Thời điểm tạo bản ghi; phục vụ audit, sắp xếp và truy vết lịch sử. |
| `created_by` | `BIGINT UNSIGNED` | YES |  |  |  |  | User tạo bản ghi; phục vụ audit và kiểm tra trách nhiệm thao tác. |
| `updated_at` | `DATETIME` | YES | NULL | ON UPDATE CURRENT_TIMESTAMP |  |  | Thời điểm cập nhật gần nhất; tự động cập nhật khi SQL có ON UPDATE CURRENT_TIMESTAMP. |
| `updated_by` | `BIGINT UNSIGNED` | YES |  |  |  |  | User cập nhật gần nhất; phục vụ audit. |

**Primary Key:**
- `PRIMARY KEY (agenda_id)`

**Unique Constraints:**
- `uq_visit_agendas_order` (visit_instance_id, sequence_order)

**Indexes:**
- `idx_visit_agendas_time` (visit_instance_id, start_time)
- `idx_visit_agendas_responsible` (responsible_user_id, start_time)

**Foreign Keys:**
- `fk_visit_agendas_instance`: (visit_instance_id) → `visit_request_campuses`(visit_instance_id) ON UPDATE CASCADE ON DELETE RESTRICT
- `fk_visit_agendas_responsible_user`: (responsible_user_id) → `users`(user_id) ON UPDATE CASCADE ON DELETE SET NULL

### 4.19. `visit_logistics_items`

**Purpose / Table Comment:** Yêu cầu hậu cần/resource cho visit: gửi yêu cầu, đề xuất thay đổi, tiếp nhận, phân công, xác nhận và hoàn thành. Thay thế tasks cho logistics/resource.

**Main Screens / UC Area:** Logistics / Resource Request / Task Flow

**Columns:**

| Column | Type | Null | Default | Extra | Key / FK / Index | Enum Values | Notes / Meaning |
|---|---|---:|---|---|---|---|---|
| `logistics_item_id` | `BIGINT UNSIGNED` | NO |  | AUTO_INCREMENT | PK |  | Khóa chính định danh duy nhất bản ghi trong bảng `visit_logistics_items`. |
| `visit_instance_id` | `BIGINT UNSIGNED` | NO |  |  | IDX: idx_logistics_instance_status; FK: visit_request_campuses(visit_instance_id) |  | Khóa ngoại liên kết tới visit_request_campuses(visit_instance_id); dùng để join dữ liệu và đảm bảo toàn vẹn tham chiếu. |
| `item_type` | `ENUM('ROOM','TRANSPORT','MEAL','EQUIPMENT','BANNER','LED','OTHER')` | NO |  |  | IDX: idx_logistics_item_status | ROOM, TRANSPORT, MEAL, EQUIPMENT, BANNER, LED, OTHER | Loại/phân nhóm nghiệp vụ của bản ghi, dùng cho filter, rule và UI. |
| `title` | `VARCHAR(255)` | NO |  |  |  |  | Tiêu đề/nội dung chính hiển thị trên UI. |
| `description` | `TEXT` | YES |  |  |  |  | Nội dung chi tiết công việc gốc |
| `quantity` | `INT UNSIGNED` | YES |  |  |  |  | Số lượng yêu cầu gốc |
| `usage_start_at` | `DATETIME` | YES |  |  | IDX: idx_logistics_usage_time |  | Thời gian bắt đầu sử dụng resource |
| `usage_end_at` | `DATETIME` | YES |  |  | IDX: idx_logistics_usage_time |  | Thời gian kết thúc sử dụng resource |
| `status` | `ENUM(<br>    'PLANNED',<br>    'REQUESTED',<br>    'CHANGE_PROPOSED',<br>    'RECEIVED',<br>    'ASSIGNED',<br>    'ACCEPTED',<br>    'IN_PROGRESS',<br>    'READY',<br>    'DONE',<br>    'REJECTED',<br>    'CANCELLED'<br>  )` | NO | 'PLANNED' |  | IDX: idx_logistics_instance_status, idx_logistics_item_status, idx_logistics_department_status, idx_logistics_assignee_status | PLANNED, REQUESTED, CHANGE_PROPOSED, RECEIVED, ASSIGNED, ACCEPTED, IN_PROGRESS, READY, DONE, REJECTED, CANCELLED | Trạng thái vòng đời của bản ghi; dùng cho filter, badge và chặn thao tác không hợp lệ. |
| `priority` | `ENUM('LOW','MEDIUM','HIGH','URGENT')` | NO | 'MEDIUM' |  | IDX: idx_logistics_priority_due | LOW, MEDIUM, HIGH, URGENT | Lưu thông tin `priority` của bảng `visit_logistics_items` để phục vụ màn hình và logic nghiệp vụ liên quan. |
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
| `due_at` | `DATETIME` | YES |  |  | IDX: idx_logistics_due, idx_logistics_priority_due |  | Deadline hoàn thành hạng mục |
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
| `handover_confirmed_by` | `BIGINT UNSIGNED` | YES |  |  | IDX: idx_logistics_handover; FK: users(user_id) |  | Khóa ngoại liên kết tới users(user_id); dùng để join dữ liệu và đảm bảo toàn vẹn tham chiếu. |
| `handover_confirmed_at` | `DATETIME` | YES |  |  | IDX: idx_logistics_handover |  | Mốc thời gian nghiệp vụ, dùng cho audit, filter, timeline hoặc SLA. |
| `handover_note` | `TEXT` | YES |  |  |  |  | Lưu thông tin `handover_note` của bảng `visit_logistics_items` để phục vụ màn hình và logic nghiệp vụ liên quan. |
| `service_report_signed_by` | `BIGINT UNSIGNED` | YES |  |  | IDX: idx_logistics_service_report; FK: users(user_id) |  | Khóa ngoại liên kết tới users(user_id); dùng để join dữ liệu và đảm bảo toàn vẹn tham chiếu. |
| `service_report_signed_at` | `DATETIME` | YES |  |  | IDX: idx_logistics_service_report |  | Mốc thời gian nghiệp vụ, dùng cho audit, filter, timeline hoặc SLA. |
| `service_report_file_id` | `BIGINT UNSIGNED` | YES |  |  | IDX: idx_logistics_service_report_file; FK: files(file_id) |  | Khóa ngoại liên kết tới files(file_id); dùng để join dữ liệu và đảm bảo toàn vẹn tham chiếu. |
| `decision_note` | `TEXT` | YES |  |  |  |  | Lý do reject/cancel hoặc ghi chú xử lý |
| `row_version` | `INT UNSIGNED` | NO | 0 |  |  |  | Optimistic concurrency token |
| `created_at` | `DATETIME` | NO | CURRENT_TIMESTAMP |  |  |  | Thời điểm tạo bản ghi; phục vụ audit, sắp xếp và truy vết lịch sử. |
| `created_by` | `BIGINT UNSIGNED` | YES |  |  |  |  | User tạo bản ghi; phục vụ audit và kiểm tra trách nhiệm thao tác. |
| `updated_at` | `DATETIME` | YES | NULL | ON UPDATE CURRENT_TIMESTAMP |  |  | Thời điểm cập nhật gần nhất; tự động cập nhật khi SQL có ON UPDATE CURRENT_TIMESTAMP. |
| `updated_by` | `BIGINT UNSIGNED` | YES |  |  |  |  | User cập nhật gần nhất; phục vụ audit. |

**Primary Key:**
- `PRIMARY KEY (logistics_item_id)`

**Indexes:**
- `idx_logistics_instance_status` (visit_instance_id, status)
- `idx_logistics_item_status` (item_type, status)
- `idx_logistics_department_status` (requested_to_department_id, status)
- `idx_logistics_assignee_status` (assigned_to_user_id, status)
- `idx_logistics_requested_by_time` (requested_by, requested_at)
- `idx_logistics_received_by_time` (received_by, received_at)
- `idx_logistics_usage_time` (usage_start_at, usage_end_at)
- `idx_logistics_due` (due_at)
- `idx_logistics_priority_due` (priority, due_at)
- `idx_logistics_proposed_by_time` (proposed_by, proposed_at)
- `idx_logistics_handover` (handover_confirmed_by, handover_confirmed_at)
- `idx_logistics_service_report` (service_report_signed_by, service_report_signed_at)
- `idx_logistics_service_report_file` (service_report_file_id)

**Foreign Keys:**
- `fk_logistics_instance`: (visit_instance_id) → `visit_request_campuses`(visit_instance_id) ON UPDATE CASCADE ON DELETE RESTRICT
- `fk_logistics_requested_by`: (requested_by) → `users`(user_id) ON UPDATE CASCADE ON DELETE SET NULL
- `fk_logistics_requested_to_department`: (requested_to_department_id) → `departments`(department_id) ON UPDATE CASCADE ON DELETE SET NULL
- `fk_logistics_received_by`: (received_by) → `users`(user_id) ON UPDATE CASCADE ON DELETE SET NULL
- `fk_logistics_assigned_to`: (assigned_to_user_id) → `users`(user_id) ON UPDATE CASCADE ON DELETE SET NULL
- `fk_logistics_assigned_by`: (assigned_by) → `users`(user_id) ON UPDATE CASCADE ON DELETE SET NULL
- `fk_logistics_proposed_by`: (proposed_by) → `users`(user_id) ON UPDATE CASCADE ON DELETE SET NULL
- `fk_logistics_proposal_responded_by`: (proposal_responded_by) → `users`(user_id) ON UPDATE CASCADE ON DELETE SET NULL
- `fk_logistics_handover_by`: (handover_confirmed_by) → `users`(user_id) ON UPDATE CASCADE ON DELETE SET NULL
- `fk_logistics_service_report_signed_by`: (service_report_signed_by) → `users`(user_id) ON UPDATE CASCADE ON DELETE SET NULL
- `fk_logistics_service_report_file`: (service_report_file_id) → `files`(file_id) ON UPDATE CASCADE ON DELETE SET NULL

**Check Constraints:**
- `CHECK (quantity IS NULL OR quantity >= 1)`
- `CHECK (usage_end_at IS NULL OR usage_start_at IS NULL OR usage_end_at > usage_start_at)`
- `CHECK (proposed_quantity IS NULL OR proposed_quantity >= 1)`
- `CHECK (proposed_usage_end_at IS NULL OR proposed_usage_start_at IS NULL OR proposed_usage_end_at > proposed_usage_start_at)`

### 4.20. `minutes`

**Purpose / Table Comment:** Meeting Minutes / Close Delegation

**Main Screens / UC Area:** Meeting Minutes / Close Delegation

**Columns:**

| Column | Type | Null | Default | Extra | Key / FK / Index | Enum Values | Notes / Meaning |
|---|---|---:|---|---|---|---|---|
| `minutes_id` | `BIGINT UNSIGNED` | NO |  | AUTO_INCREMENT | PK |  | Khóa chính định danh duy nhất bản ghi trong bảng `minutes`. |
| `visit_instance_id` | `BIGINT UNSIGNED` | NO |  |  | IDX: idx_minutes_visit_status; FK: visit_request_campuses(visit_instance_id) |  | Khóa ngoại liên kết tới visit_request_campuses(visit_instance_id); dùng để join dữ liệu và đảm bảo toàn vẹn tham chiếu. |
| `title` | `VARCHAR(255)` | NO |  |  | IDX: ft_minutes_search |  | Tiêu đề/nội dung chính hiển thị trên UI. |
| `content` | `LONGTEXT` | YES |  |  | IDX: ft_minutes_search |  | Lưu thông tin `content` của bảng `minutes` để phục vụ màn hình và logic nghiệp vụ liên quan. |
| `status` | `ENUM('DRAFT','SAVED')` | NO | 'DRAFT' |  | IDX: idx_minutes_visit_status | DRAFT, SAVED | DRAFT=biên bản nháp, SAVED=đã lưu nội dung; quyền sửa bị khóa khi visit instance CLOSED |
| `edit_locked_by` | `BIGINT UNSIGNED` | YES |  |  | IDX: idx_minutes_edit_lock; FK: users(user_id) |  | User hiện đang giữ quyền sửa biên bản |
| `edit_locked_at` | `DATETIME` | YES |  |  |  |  | Thời điểm bắt đầu giữ quyền sửa |
| `edit_lock_expires_at` | `DATETIME` | YES |  |  | IDX: idx_minutes_edit_lock |  | Thời điểm lock sửa hết hạn |
| `edit_lock_token` | `CHAR(36)` | YES |  |  |  |  | Token phiên sửa, dùng để xác nhận đúng người đang giữ lock |
| `row_version` | `INT UNSIGNED` | NO | 0 |  |  |  | Version chống ghi đè khi cập nhật đồng thời |
| `created_at` | `DATETIME` | NO | CURRENT_TIMESTAMP |  | IDX: idx_minutes_created_by_time |  | Thời điểm tạo bản ghi; phục vụ audit, sắp xếp và truy vết lịch sử. |
| `created_by` | `BIGINT UNSIGNED` | YES |  |  | IDX: idx_minutes_created_by_time; FK: users(user_id) |  | User tạo bản ghi; phục vụ audit và kiểm tra trách nhiệm thao tác. |
| `updated_at` | `DATETIME` | YES | NULL | ON UPDATE CURRENT_TIMESTAMP |  |  | Thời điểm cập nhật gần nhất; tự động cập nhật khi SQL có ON UPDATE CURRENT_TIMESTAMP. |
| `updated_by` | `BIGINT UNSIGNED` | YES |  |  | FK: users(user_id) |  | User cập nhật gần nhất; phục vụ audit. |

**Primary Key:**
- `PRIMARY KEY (minutes_id)`

**Indexes:**
- `idx_minutes_visit_status` (visit_instance_id, status)
- `idx_minutes_created_by_time` (created_by, created_at)
- `idx_minutes_edit_lock` (edit_locked_by, edit_lock_expires_at)
- `ft_minutes_search` (title, content)

**Foreign Keys:**
- `fk_minutes_visit_instance`: (visit_instance_id) → `visit_request_campuses`(visit_instance_id) ON UPDATE CASCADE ON DELETE RESTRICT
- `fk_minutes_created_by`: (created_by) → `users`(user_id) ON UPDATE CASCADE ON DELETE SET NULL
- `fk_minutes_updated_by`: (updated_by) → `users`(user_id) ON UPDATE CASCADE ON DELETE SET NULL
- `fk_minutes_edit_locked_by`: (edit_locked_by) → `users`(user_id) ON UPDATE CASCADE ON DELETE SET NULL

### 4.21. `minute_participants`

**Purpose / Table Comment:** Meeting Minutes Attendance

**Main Screens / UC Area:** Meeting Minutes Attendance

**Columns:**

| Column | Type | Null | Default | Extra | Key / FK / Index | Enum Values | Notes / Meaning |
|---|---|---:|---|---|---|---|---|
| `minute_participant_id` | `BIGINT UNSIGNED` | NO |  | AUTO_INCREMENT | PK |  | Khóa chính định danh duy nhất bản ghi trong bảng `minute_participants`. |
| `minutes_id` | `BIGINT UNSIGNED` | NO |  |  | IDX: idx_minute_participants_minutes_order, idx_minute_participants_attendance; FK: minutes(minutes_id) |  | Khóa ngoại liên kết tới minutes(minutes_id); dùng để join dữ liệu và đảm bảo toàn vẹn tham chiếu. |
| `user_id` | `BIGINT UNSIGNED` | YES |  |  | IDX: idx_minute_participants_user; FK: users(user_id) |  | Khóa ngoại liên kết tới users(user_id); dùng để join dữ liệu và đảm bảo toàn vẹn tham chiếu. |
| `guest_member_id` | `BIGINT UNSIGNED` | YES |  |  | IDX: idx_minute_participants_guest_member; FK: visit_guest_members(guest_member_id) |  | Khóa ngoại liên kết tới visit_guest_members(guest_member_id); dùng để join dữ liệu và đảm bảo toàn vẹn tham chiếu. |
| `full_name_snapshot` | `VARCHAR(255)` | NO |  |  |  |  | Lưu thông tin `full_name_snapshot` của bảng `minute_participants` để phục vụ màn hình và logic nghiệp vụ liên quan. |
| `role_snapshot` | `VARCHAR(120)` | YES |  |  |  |  | Lưu thông tin `role_snapshot` của bảng `minute_participants` để phục vụ màn hình và logic nghiệp vụ liên quan. |
| `organization_snapshot` | `VARCHAR(255)` | YES |  |  |  |  | Lưu thông tin `organization_snapshot` của bảng `minute_participants` để phục vụ màn hình và logic nghiệp vụ liên quan. |
| `email_snapshot` | `VARCHAR(150)` | YES |  |  |  |  | Lưu thông tin `email_snapshot` của bảng `minute_participants` để phục vụ màn hình và logic nghiệp vụ liên quan. |
| `attendance_status` | `ENUM('PRESENT','ABSENT','EXCUSED')` | NO | 'PRESENT' |  | IDX: idx_minute_participants_attendance | PRESENT, ABSENT, EXCUSED | PRESENT=có mặt, ABSENT=vắng mặt, EXCUSED=vắng có lý do |
| `attendance_note` | `TEXT` | YES |  |  |  |  | Ghi chú điểm danh/lý do vắng nếu có |
| `checked_at` | `DATETIME` | YES |  |  |  |  | Thời điểm ghi nhận điểm danh |
| `checked_by` | `BIGINT UNSIGNED` | YES |  |  | IDX: idx_minute_participants_checked_by; FK: users(user_id) |  | Người thực hiện điểm danh |
| `display_order` | `INT UNSIGNED` | NO | 0 |  | IDX: idx_minute_participants_minutes_order |  | Thứ tự hiển thị trên UI. |
| `created_at` | `DATETIME` | NO | CURRENT_TIMESTAMP |  |  |  | Thời điểm tạo bản ghi; phục vụ audit, sắp xếp và truy vết lịch sử. |

**Primary Key:**
- `PRIMARY KEY (minute_participant_id)`

**Indexes:**
- `idx_minute_participants_minutes_order` (minutes_id, display_order)
- `idx_minute_participants_user` (user_id)
- `idx_minute_participants_guest_member` (guest_member_id)
- `idx_minute_participants_attendance` (minutes_id, attendance_status)
- `idx_minute_participants_checked_by` (checked_by)

**Foreign Keys:**
- `fk_minute_participants_minutes`: (minutes_id) → `minutes`(minutes_id) ON UPDATE CASCADE ON DELETE CASCADE
- `fk_minute_participants_user`: (user_id) → `users`(user_id) ON UPDATE CASCADE ON DELETE SET NULL
- `fk_minute_participants_guest_member`: (guest_member_id) → `visit_guest_members`(guest_member_id) ON UPDATE CASCADE ON DELETE SET NULL
- `fk_minute_participants_checked_by`: (checked_by) → `users`(user_id) ON UPDATE CASCADE ON DELETE SET NULL

### 4.22. `minute_action_items`

**Purpose / Table Comment:** Meeting Minutes Follow-up

**Main Screens / UC Area:** Meeting Minutes Follow-up

**Columns:**

| Column | Type | Null | Default | Extra | Key / FK / Index | Enum Values | Notes / Meaning |
|---|---|---:|---|---|---|---|---|
| `action_item_id` | `BIGINT UNSIGNED` | NO |  | AUTO_INCREMENT | PK |  | Khóa chính định danh duy nhất bản ghi trong bảng `minute_action_items`. |
| `minutes_id` | `BIGINT UNSIGNED` | NO |  |  | IDX: idx_action_items_minutes, idx_action_items_order; FK: minutes(minutes_id) |  | Khóa ngoại liên kết tới minutes(minutes_id); dùng để join dữ liệu và đảm bảo toàn vẹn tham chiếu. |
| `title` | `VARCHAR(255)` | NO |  |  |  |  | Tên đầu việc |
| `note` | `TEXT` | YES |  |  |  |  | Ghi chú thêm cho đầu việc |
| `due_date` | `DATE` | YES |  |  | IDX: idx_action_items_status_due |  | Deadline của đầu việc |
| `status` | `ENUM('TODO','IN_PROGRESS','DONE','CANCELLED')` | NO | 'TODO' |  | IDX: idx_action_items_status_due | TODO, IN_PROGRESS, DONE, CANCELLED | TODO=chưa làm, IN_PROGRESS=đang làm, DONE=hoàn thành, CANCELLED=đã hủy/không cần làm nữa |
| `completed_at` | `DATETIME` | YES |  |  |  |  | Thời điểm hoàn thành; backend tự set khi status chuyển sang DONE |
| `display_order` | `INT UNSIGNED` | NO | 1 |  | IDX: idx_action_items_order |  | Thứ tự hiển thị trong biên bản |
| `created_at` | `DATETIME` | NO | CURRENT_TIMESTAMP |  | IDX: idx_action_items_created_by_time |  | Thời điểm tạo bản ghi; phục vụ audit, sắp xếp và truy vết lịch sử. |
| `created_by` | `BIGINT UNSIGNED` | YES |  |  | IDX: idx_action_items_created_by_time; FK: users(user_id) |  | User tạo bản ghi; phục vụ audit và kiểm tra trách nhiệm thao tác. |
| `updated_at` | `DATETIME` | YES | NULL | ON UPDATE CURRENT_TIMESTAMP |  |  | Thời điểm cập nhật gần nhất; tự động cập nhật khi SQL có ON UPDATE CURRENT_TIMESTAMP. |
| `updated_by` | `BIGINT UNSIGNED` | YES |  |  | FK: users(user_id) |  | User cập nhật gần nhất; phục vụ audit. |

**Primary Key:**
- `PRIMARY KEY (action_item_id)`

**Indexes:**
- `idx_action_items_minutes` (minutes_id)
- `idx_action_items_status_due` (status, due_date)
- `idx_action_items_order` (minutes_id, display_order)
- `idx_action_items_created_by_time` (created_by, created_at)

**Foreign Keys:**
- `fk_action_items_minutes`: (minutes_id) → `minutes`(minutes_id) ON UPDATE CASCADE ON DELETE CASCADE
- `fk_action_items_created_by`: (created_by) → `users`(user_id) ON UPDATE CASCADE ON DELETE SET NULL
- `fk_action_items_updated_by`: (updated_by) → `users`(user_id) ON UPDATE CASCADE ON DELETE SET NULL

### 4.23. `feedbacks`

**Purpose / Table Comment:** Feedback Management

**Main Screens / UC Area:** Feedback Management

**Columns:**

| Column | Type | Null | Default | Extra | Key / FK / Index | Enum Values | Notes / Meaning |
|---|---|---:|---|---|---|---|---|
| `feedback_id` | `BIGINT UNSIGNED` | NO |  | AUTO_INCREMENT | PK |  | Khóa chính định danh duy nhất bản ghi trong bảng `feedbacks`. |
| `visit_request_id` | `BIGINT UNSIGNED` | NO |  |  | IDX: idx_feedbacks_visit_request; FK: visit_requests(visit_request_id) |  | Khóa ngoại liên kết tới visit_requests(visit_request_id); dùng để join dữ liệu và đảm bảo toàn vẹn tham chiếu. |
| `visit_instance_id` | `BIGINT UNSIGNED` | YES |  |  | IDX: idx_feedbacks_visit_instance; FK: visit_request_campuses(visit_instance_id) |  | Khóa ngoại liên kết tới visit_request_campuses(visit_instance_id); dùng để join dữ liệu và đảm bảo toàn vẹn tham chiếu. |
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
| `submitted_at` | `DATETIME` | NO | CURRENT_TIMESTAMP |  | IDX: idx_feedbacks_submitted_at |  | Mốc thời gian nghiệp vụ, dùng cho audit, filter, timeline hoặc SLA. |

**Primary Key:**
- `PRIMARY KEY (feedback_id)`

**Indexes:**
- `idx_feedbacks_visit_request` (visit_request_id)
- `idx_feedbacks_visit_instance` (visit_instance_id)
- `idx_feedbacks_submitter` (submitted_by_user_id)
- `idx_feedbacks_target` (target_user_id)
- `idx_feedbacks_roles` (submitter_role, target_role)
- `idx_feedbacks_rating` (rating)
- `idx_feedbacks_submitted_at` (submitted_at)

**Foreign Keys:**
- `fk_feedbacks_visit_request`: (visit_request_id) → `visit_requests`(visit_request_id) ON UPDATE CASCADE ON DELETE RESTRICT
- `fk_feedbacks_visit_instance`: (visit_instance_id) → `visit_request_campuses`(visit_instance_id) ON UPDATE CASCADE ON DELETE SET NULL
- `fk_feedbacks_submitter`: (submitted_by_user_id) → `users`(user_id) ON UPDATE CASCADE ON DELETE RESTRICT
- `fk_feedbacks_target`: (target_user_id) → `users`(user_id) ON UPDATE CASCADE ON DELETE RESTRICT

**Check Constraints:**
- `CHECK (rating BETWEEN 1 AND 5)`
- `CHECK ( (submitter_role IN ('VISITOR','LOGISTICS') AND target_role = 'HOST') OR (submitter_role = 'HOST' AND target_role IN ('VISITOR','LOGISTICS')) )`

### 4.24. `feedback_rating_items`

**Purpose / Table Comment:** Normalized per-criterion ratings for a feedback submission.

**Main Screens / UC Area:** Feedback Criteria Ratings

**Columns:**

| Column | Type | Null | Default | Extra | Key / FK / Index | Enum Values | Notes / Meaning |
|---|---|---:|---|---|---|---|---|
| `feedback_rating_item_id` | `BIGINT UNSIGNED` | NO |  | AUTO_INCREMENT | PK |  | Khóa chính định danh duy nhất bản ghi trong bảng `feedback_rating_items`. |
| `feedback_id` | `BIGINT UNSIGNED` | NO |  |  | UNIQUE: uq_feedback_rating_criterion; IDX: idx_feedback_rating_feedback; FK: feedbacks(feedback_id) |  | Khóa ngoại liên kết tới feedbacks(feedback_id); dùng để join dữ liệu và đảm bảo toàn vẹn tham chiếu. |
| `criterion_code` | `VARCHAR(80)` | NO |  |  | UNIQUE: uq_feedback_rating_criterion |  | Mã nghiệp vụ ngắn, ổn định, dùng cho seed, filter, rule và tích hợp. |
| `criterion_label` | `VARCHAR(150)` | NO |  |  |  |  | Lưu thông tin `criterion_label` của bảng `feedback_rating_items` để phục vụ màn hình và logic nghiệp vụ liên quan. |
| `rating` | `TINYINT UNSIGNED` | NO |  |  |  |  | Lưu thông tin `rating` của bảng `feedback_rating_items` để phục vụ màn hình và logic nghiệp vụ liên quan. |
| `display_order` | `INT UNSIGNED` | NO | 0 |  |  |  | Thứ tự hiển thị trên UI. |
| `created_at` | `DATETIME` | NO | CURRENT_TIMESTAMP |  |  |  | Thời điểm tạo bản ghi; phục vụ audit, sắp xếp và truy vết lịch sử. |

**Primary Key:**
- `PRIMARY KEY (feedback_rating_item_id)`

**Unique Constraints:**
- `uq_feedback_rating_criterion` (feedback_id, criterion_code)

**Indexes:**
- `idx_feedback_rating_feedback` (feedback_id)

**Foreign Keys:**
- `fk_feedback_rating_items_feedback`: (feedback_id) → `feedbacks`(feedback_id) ON UPDATE CASCADE ON DELETE CASCADE

**Check Constraints:**
- `CHECK (rating BETWEEN 1 AND 5)`

### 4.25. `news`

**Purpose / Table Comment:** News Management / Review / Publish

**Main Screens / UC Area:** News Management / Review / Publish

**Columns:**

| Column | Type | Null | Default | Extra | Key / FK / Index | Enum Values | Notes / Meaning |
|---|---|---:|---|---|---|---|---|
| `news_id` | `BIGINT UNSIGNED` | NO |  | AUTO_INCREMENT | PK |  | Khóa chính định danh duy nhất bản ghi trong bảng `news`. |
| `campus_id` | `BIGINT UNSIGNED` | YES |  |  | IDX: idx_news_public; FK: campuses(campus_id) |  | Campus liên quan đến bài viết. NULL nếu bài toàn hệ thống |
| `visit_instance_id` | `BIGINT UNSIGNED` | YES |  |  | IDX: idx_news_visit_instance_status; FK: visit_request_campuses(visit_instance_id) |  | Visit instance liên quan nếu bài viết được tạo từ một chuyến tiếp đón |
| `author_user_id` | `BIGINT UNSIGNED` | NO |  |  | IDX: idx_news_author_status; FK: users(user_id) |  | Người tạo/viết bài |
| `cover_file_id` | `BIGINT UNSIGNED` | YES |  |  | FK: files(file_id) |  | Ảnh bìa bài viết, trỏ tới files.file_id |
| `status` | `ENUM('PENDING_REVIEW','REJECTED','PUBLISHED','HIDDEN')` | NO | 'PENDING_REVIEW' |  | IDX: idx_news_public, idx_news_author_status, idx_news_visit_instance_status, idx_news_featured | PENDING_REVIEW, REJECTED, PUBLISHED, HIDDEN | PENDING_REVIEW=chờ host duyệt, REJECTED=bị từ chối, PUBLISHED=đã đăng, HIDDEN=ẩn khỏi trang tin |
| `submitted_at` | `DATETIME` | NO | CURRENT_TIMESTAMP |  |  |  | Thời điểm người viết gửi bài cho host duyệt |
| `reviewed_by` | `BIGINT UNSIGNED` | YES |  |  | IDX: idx_news_review; FK: users(user_id) |  | Host duyệt hoặc từ chối bài viết |
| `reviewed_at` | `DATETIME` | YES |  |  | IDX: idx_news_review |  | Thời điểm host duyệt hoặc từ chối |
| `review_note` | `TEXT` | YES |  |  |  |  | Ghi chú duyệt hoặc lý do từ chối |
| `published_at` | `DATETIME` | YES |  |  | IDX: idx_news_public, idx_news_featured |  | Thời điểm bài viết được đăng |
| `is_featured` | `BOOLEAN` | NO | FALSE |  | IDX: idx_news_featured |  | Bài viết nổi bật |
| `row_version` | `INT UNSIGNED` | NO | 0 |  |  |  | Optimistic concurrency token, chống ghi đè khi cập nhật đồng thời |
| `created_at` | `DATETIME` | NO | CURRENT_TIMESTAMP |  |  |  | Thời điểm tạo bản ghi; phục vụ audit, sắp xếp và truy vết lịch sử. |
| `created_by` | `BIGINT UNSIGNED` | YES |  |  |  |  | User tạo bản ghi; phục vụ audit và kiểm tra trách nhiệm thao tác. |
| `updated_at` | `DATETIME` | YES | NULL | ON UPDATE CURRENT_TIMESTAMP |  |  | Thời điểm cập nhật gần nhất; tự động cập nhật khi SQL có ON UPDATE CURRENT_TIMESTAMP. |
| `updated_by` | `BIGINT UNSIGNED` | YES |  |  |  |  | User cập nhật gần nhất; phục vụ audit. |

**Primary Key:**
- `PRIMARY KEY (news_id)`

**Indexes:**
- `idx_news_public` (status, campus_id, published_at)
- `idx_news_author_status` (author_user_id, status)
- `idx_news_visit_instance_status` (visit_instance_id, status)
- `idx_news_review` (reviewed_by, reviewed_at)
- `idx_news_featured` (is_featured, status, published_at)

**Foreign Keys:**
- `fk_news_campus`: (campus_id) → `campuses`(campus_id) ON UPDATE CASCADE ON DELETE SET NULL
- `fk_news_visit_instance`: (visit_instance_id) → `visit_request_campuses`(visit_instance_id) ON UPDATE CASCADE ON DELETE SET NULL
- `fk_news_author`: (author_user_id) → `users`(user_id) ON UPDATE CASCADE ON DELETE RESTRICT
- `fk_news_cover_file`: (cover_file_id) → `files`(file_id) ON UPDATE CASCADE ON DELETE SET NULL
- `fk_news_reviewed_by`: (reviewed_by) → `users`(user_id) ON UPDATE CASCADE ON DELETE SET NULL

### 4.26. `news_translations`

**Purpose / Table Comment:** Tiêu đề, slug, tóm tắt và SEO của bài viết theo ngôn ngữ

**Main Screens / UC Area:** Multilingual News Metadata

**Columns:**

| Column | Type | Null | Default | Extra | Key / FK / Index | Enum Values | Notes / Meaning |
|---|---|---:|---|---|---|---|---|
| `news_translation_id` | `BIGINT UNSIGNED` | NO |  | AUTO_INCREMENT | PK |  | Khóa chính định danh duy nhất bản ghi trong bảng `news_translations`. |
| `news_id` | `BIGINT UNSIGNED` | NO |  |  | UNIQUE: uq_news_translation_lang; FK: news(news_id) |  | Khóa ngoại liên kết tới news(news_id); dùng để join dữ liệu và đảm bảo toàn vẹn tham chiếu. |
| `language_code` | `VARCHAR(20)` | NO | 'vi' |  | UNIQUE: uq_news_translation_lang, uq_news_translation_slug_lang; IDX: idx_news_translations_lang |  | Mã nghiệp vụ ngắn, ổn định, dùng cho seed, filter, rule và tích hợp. |
| `title` | `VARCHAR(255)` | NO |  |  | IDX: ft_news_translations_search |  | Tiêu đề chính của bài viết |
| `slug` | `VARCHAR(255)` | NO |  |  | UNIQUE: uq_news_translation_slug_lang |  | Đường dẫn SEO của bài viết |
| `summary` | `TEXT` | YES |  |  | IDX: ft_news_translations_search |  | Tóm tắt bài viết |
| `seo_title` | `VARCHAR(255)` | YES |  |  |  |  | Lưu thông tin `seo_title` của bảng `news_translations` để phục vụ màn hình và logic nghiệp vụ liên quan. |
| `seo_description` | `VARCHAR(500)` | YES |  |  |  |  | Lưu thông tin `seo_description` của bảng `news_translations` để phục vụ màn hình và logic nghiệp vụ liên quan. |
| `created_at` | `DATETIME` | NO | CURRENT_TIMESTAMP |  |  |  | Thời điểm tạo bản ghi; phục vụ audit, sắp xếp và truy vết lịch sử. |
| `updated_at` | `DATETIME` | YES | NULL | ON UPDATE CURRENT_TIMESTAMP |  |  | Thời điểm cập nhật gần nhất; tự động cập nhật khi SQL có ON UPDATE CURRENT_TIMESTAMP. |

**Primary Key:**
- `PRIMARY KEY (news_translation_id)`

**Unique Constraints:**
- `uq_news_translation_lang` (news_id, language_code)
- `uq_news_translation_slug_lang` (slug, language_code)

**Indexes:**
- `idx_news_translations_lang` (language_code)
- `ft_news_translations_search` (title, summary)

**Foreign Keys:**
- `fk_news_translations_news`: (news_id) → `news`(news_id) ON UPDATE CASCADE ON DELETE CASCADE

### 4.27. `news_content_sections`

**Purpose / Table Comment:** Các khối nội dung chi tiết của bài viết, tối đa 10 section mỗi bản dịch

**Main Screens / UC Area:** News Editor Sections

**Columns:**

| Column | Type | Null | Default | Extra | Key / FK / Index | Enum Values | Notes / Meaning |
|---|---|---:|---|---|---|---|---|
| `section_id` | `BIGINT UNSIGNED` | NO |  | AUTO_INCREMENT | PK |  | Khóa chính định danh duy nhất bản ghi trong bảng `news_content_sections`. |
| `news_translation_id` | `BIGINT UNSIGNED` | NO |  |  | UNIQUE: uq_news_section_order; IDX: idx_news_sections_translation; FK: news_translations(news_translation_id) |  | Khóa ngoại liên kết tới news_translations(news_translation_id); dùng để join dữ liệu và đảm bảo toàn vẹn tham chiếu. |
| `section_order` | `TINYINT UNSIGNED` | NO |  |  | UNIQUE: uq_news_section_order |  | Thứ tự section, từ 1 đến 10 |
| `section_title` | `VARCHAR(255)` | NO |  |  | IDX: ft_news_sections_search |  | Tiêu đề section |
| `section_body_html` | `LONGTEXT` | NO |  |  |  |  | Nội dung rich text dạng HTML đã sanitize, có thể chứa paragraph, bold, italic, color, link, image |
| `section_body_text` | `TEXT` | YES |  |  | IDX: ft_news_sections_search |  | Plain text tách từ HTML để search hoặc preview |
| `created_at` | `DATETIME` | NO | CURRENT_TIMESTAMP |  |  |  | Thời điểm tạo bản ghi; phục vụ audit, sắp xếp và truy vết lịch sử. |
| `updated_at` | `DATETIME` | YES | NULL | ON UPDATE CURRENT_TIMESTAMP |  |  | Thời điểm cập nhật gần nhất; tự động cập nhật khi SQL có ON UPDATE CURRENT_TIMESTAMP. |

**Primary Key:**
- `PRIMARY KEY (section_id)`

**Unique Constraints:**
- `uq_news_section_order` (news_translation_id, section_order)

**Indexes:**
- `idx_news_sections_translation` (news_translation_id)
- `ft_news_sections_search` (section_title, section_body_text)

**Foreign Keys:**
- `fk_news_sections_translation`: (news_translation_id) → `news_translations`(news_translation_id) ON UPDATE CASCADE ON DELETE CASCADE

**Check Constraints:**
- `CHECK (section_order BETWEEN 1 AND 10)`

### 4.28. `news_section_files`

**Purpose / Table Comment:** File/ảnh được dùng trong từng section của bài news

**Main Screens / UC Area:** News Section Media

**Columns:**

| Column | Type | Null | Default | Extra | Key / FK / Index | Enum Values | Notes / Meaning |
|---|---|---:|---|---|---|---|---|
| `section_file_id` | `BIGINT UNSIGNED` | NO |  | AUTO_INCREMENT | PK |  | Khóa chính định danh duy nhất bản ghi trong bảng `news_section_files`. |
| `section_id` | `BIGINT UNSIGNED` | NO |  |  | UNIQUE: uq_news_section_file; IDX: idx_news_section_files_section; FK: news_content_sections(section_id) |  | Khóa ngoại liên kết tới news_content_sections(section_id); dùng để join dữ liệu và đảm bảo toàn vẹn tham chiếu. |
| `file_id` | `BIGINT UNSIGNED` | NO |  |  | UNIQUE: uq_news_section_file; IDX: idx_news_section_files_file; FK: files(file_id) |  | Khóa ngoại liên kết tới files(file_id); dùng để join dữ liệu và đảm bảo toàn vẹn tham chiếu. |
| `usage_type` | `ENUM('INLINE_IMAGE','ATTACHMENT')` | NO | 'INLINE_IMAGE' |  |  | INLINE_IMAGE, ATTACHMENT | INLINE_IMAGE=ảnh chèn trong nội dung, ATTACHMENT=file đính kèm |
| `display_order` | `INT UNSIGNED` | NO | 0 |  |  |  | Thứ tự hiển thị trên UI. |
| `created_at` | `DATETIME` | NO | CURRENT_TIMESTAMP |  |  |  | Thời điểm tạo bản ghi; phục vụ audit, sắp xếp và truy vết lịch sử. |

**Primary Key:**
- `PRIMARY KEY (section_file_id)`

**Unique Constraints:**
- `uq_news_section_file` (section_id, file_id)

**Indexes:**
- `idx_news_section_files_section` (section_id)
- `idx_news_section_files_file` (file_id)

**Foreign Keys:**
- `fk_news_section_files_section`: (section_id) → `news_content_sections`(section_id) ON UPDATE CASCADE ON DELETE CASCADE
- `fk_news_section_files_file`: (file_id) → `files`(file_id) ON UPDATE CASCADE ON DELETE RESTRICT

### 4.29. `faqs`

**Purpose / Table Comment:** FAQ một ngôn ngữ, chỉ dùng PUBLISHED/HIDDEN

**Main Screens / UC Area:** FAQ Management / Public FAQ

**Columns:**

| Column | Type | Null | Default | Extra | Key / FK / Index | Enum Values | Notes / Meaning |
|---|---|---:|---|---|---|---|---|
| `faq_id` | `BIGINT UNSIGNED` | NO |  | AUTO_INCREMENT | PK |  | Khóa chính định danh duy nhất bản ghi trong bảng `faqs`. |
| `faq_type` | `ENUM('PROGRAM','TUITION_FEE','VISA','DORMITORY','VISIT_REQUEST','SECURITY','LOGISTICS','OTHER')` | NO | 'OTHER' |  | IDX: idx_faqs_type_status | PROGRAM, TUITION_FEE, VISA, DORMITORY, VISIT_REQUEST, SECURITY, LOGISTICS, OTHER | Loại/phân nhóm nghiệp vụ của bản ghi, dùng cho filter, rule và UI. |
| `language_code` | `ENUM('vi','en')` | NO | 'vi' |  | IDX: idx_faqs_language_status | vi, en | Mã nghiệp vụ ngắn, ổn định, dùng cho seed, filter, rule và tích hợp. |
| `question` | `VARCHAR(500)` | NO |  |  | IDX: ft_faqs_search |  | Câu hỏi FAQ |
| `answer` | `TEXT` | NO |  |  | IDX: ft_faqs_search |  | Câu trả lời FAQ |
| `display_order` | `INT UNSIGNED` | NO | 0 |  | IDX: idx_faqs_status_order |  | Thứ tự hiển thị trên UI. |
| `status` | `ENUM('PUBLISHED','HIDDEN')` | NO | 'HIDDEN' |  | IDX: idx_faqs_status_order, idx_faqs_type_status, idx_faqs_language_status | PUBLISHED, HIDDEN | PUBLISHED=hiển thị trên trang FAQ, HIDDEN=ẩn khỏi người xem thường nhưng người quản lý vẫn thấy |
| `created_at` | `DATETIME` | NO | CURRENT_TIMESTAMP |  |  |  | Thời điểm tạo bản ghi; phục vụ audit, sắp xếp và truy vết lịch sử. |
| `created_by` | `BIGINT UNSIGNED` | YES |  |  |  |  | User tạo bản ghi; phục vụ audit và kiểm tra trách nhiệm thao tác. |
| `updated_at` | `DATETIME` | YES | NULL | ON UPDATE CURRENT_TIMESTAMP |  |  | Thời điểm cập nhật gần nhất; tự động cập nhật khi SQL có ON UPDATE CURRENT_TIMESTAMP. |
| `updated_by` | `BIGINT UNSIGNED` | YES |  |  |  |  | User cập nhật gần nhất; phục vụ audit. |

**Primary Key:**
- `PRIMARY KEY (faq_id)`

**Indexes:**
- `idx_faqs_status_order` (status, display_order)
- `idx_faqs_type_status` (faq_type, status)
- `idx_faqs_language_status` (language_code, status)
- `ft_faqs_search` (question, answer)

### 4.30. `galleries`

**Purpose / Table Comment:** Gallery địa điểm trong campus, có mô tả và câu chuyện

**Main Screens / UC Area:** Gallery Management / Public Gallery

**Columns:**

| Column | Type | Null | Default | Extra | Key / FK / Index | Enum Values | Notes / Meaning |
|---|---|---:|---|---|---|---|---|
| `gallery_id` | `BIGINT UNSIGNED` | NO |  | AUTO_INCREMENT | PK |  | Khóa chính định danh duy nhất bản ghi trong bảng `galleries`. |
| `campus_id` | `BIGINT UNSIGNED` | NO |  |  | IDX: idx_galleries_campus_status, idx_galleries_area_specific; FK: campuses(campus_id) |  | Khóa ngoại liên kết tới campuses(campus_id); dùng để join dữ liệu và đảm bảo toàn vẹn tham chiếu. |
| `area_name` | `VARCHAR(150)` | NO | 'Campus' |  | IDX: idx_galleries_area_specific |  | Khu vực trong campus, ví dụ: Academic Area, Lobby, Lab Zone |
| `specific_location_name` | `VARCHAR(150)` | NO | 'Campus location' |  | IDX: idx_galleries_area_specific |  | Vị trí cụ thể trong khu vực, ví dụ: Sảnh Alpha, Green Lab |
| `location_description` | `TEXT` | YES |  |  |  |  | Mô tả vị trí/khu vực hiển thị ở Gallery/Visit FPTU |
| `title` | `VARCHAR(255)` | NO |  |  |  |  | Tên hiển thị của gallery/địa điểm |
| `description` | `TEXT` | YES |  |  |  |  | Mô tả ngắn về địa điểm |
| `story_content` | `TEXT` | YES |  |  |  |  | Ý nghĩa hoặc câu chuyện giới thiệu về địa điểm |
| `status` | `ENUM('PUBLISHED','HIDDEN')` | NO | 'HIDDEN' |  | IDX: idx_galleries_campus_status, idx_galleries_visibility_status | PUBLISHED, HIDDEN | PUBLISHED=hiển thị theo visibility, HIDDEN=ẩn khỏi public/người xem thường nhưng Staff Leader vẫn quản lý được |
| `visibility` | `ENUM('PRIVATE','INTERNAL','PUBLIC')` | NO | 'INTERNAL' |  | IDX: idx_galleries_visibility_status | PRIVATE, INTERNAL, PUBLIC | Phạm vi xem khi status=PUBLISHED: PRIVATE=chỉ quản lý, INTERNAL=user nội bộ, PUBLIC=công khai |
| `hero_file_id` | `BIGINT UNSIGNED` | YES |  |  | IDX: idx_galleries_hero_file; FK: files(file_id) |  | Khóa ngoại liên kết tới files(file_id); dùng để join dữ liệu và đảm bảo toàn vẹn tham chiếu. |
| `virtual_tour_url` | `VARCHAR(700)` | YES |  |  |  |  | URL/đường dẫn phục vụ hiển thị hoặc truy cập tài nguyên. |
| `created_at` | `DATETIME` | NO | CURRENT_TIMESTAMP |  |  |  | Thời điểm tạo bản ghi; phục vụ audit, sắp xếp và truy vết lịch sử. |
| `created_by` | `BIGINT UNSIGNED` | YES |  |  |  |  | User tạo bản ghi; phục vụ audit và kiểm tra trách nhiệm thao tác. |
| `updated_at` | `DATETIME` | YES | NULL | ON UPDATE CURRENT_TIMESTAMP |  |  | Thời điểm cập nhật gần nhất; tự động cập nhật khi SQL có ON UPDATE CURRENT_TIMESTAMP. |
| `updated_by` | `BIGINT UNSIGNED` | YES |  |  |  |  | User cập nhật gần nhất; phục vụ audit. |
| `deleted_at` | `DATETIME` | YES |  |  | IDX: idx_galleries_campus_status |  | Thời điểm soft delete; NULL nghĩa là bản ghi chưa bị xóa mềm. |
| `deleted_by` | `BIGINT UNSIGNED` | YES |  |  |  |  | User thực hiện soft delete. |

**Primary Key:**
- `PRIMARY KEY (gallery_id)`

**Indexes:**
- `idx_galleries_campus_status` (campus_id, status, deleted_at)
- `idx_galleries_area_specific` (campus_id, area_name, specific_location_name)
- `idx_galleries_visibility_status` (visibility, status)
- `idx_galleries_hero_file` (hero_file_id)

**Foreign Keys:**
- `fk_galleries_campus`: (campus_id) → `campuses`(campus_id) ON UPDATE CASCADE ON DELETE RESTRICT
- `fk_galleries_hero_file`: (hero_file_id) → `files`(file_id) ON UPDATE CASCADE ON DELETE SET NULL

### 4.31. `gallery_images`

**Purpose / Table Comment:** Ảnh thuộc gallery địa điểm campus

**Main Screens / UC Area:** Gallery Images / Videos

**Columns:**

| Column | Type | Null | Default | Extra | Key / FK / Index | Enum Values | Notes / Meaning |
|---|---|---:|---|---|---|---|---|
| `image_id` | `BIGINT UNSIGNED` | NO |  | AUTO_INCREMENT | PK |  | Khóa chính định danh duy nhất bản ghi trong bảng `gallery_images`. |
| `gallery_id` | `BIGINT UNSIGNED` | NO |  |  | IDX: idx_gallery_images_gallery_order; FK: galleries(gallery_id) |  | Khóa ngoại liên kết tới galleries(gallery_id); dùng để join dữ liệu và đảm bảo toàn vẹn tham chiếu. |
| `file_id` | `BIGINT UNSIGNED` | NO |  |  | UNIQUE: uq_gallery_images_file; FK: files(file_id) |  | Khóa ngoại liên kết tới files(file_id); dùng để join dữ liệu và đảm bảo toàn vẹn tham chiếu. |
| `media_type` | `ENUM('IMAGE','VIDEO')` | NO | 'IMAGE' |  | IDX: idx_gallery_images_media_type | IMAGE, VIDEO | Loại/phân nhóm nghiệp vụ của bản ghi, dùng cho filter, rule và UI. |
| `thumbnail_file_id` | `BIGINT UNSIGNED` | YES |  |  | IDX: idx_gallery_images_thumbnail_file; FK: files(file_id) |  | Khóa ngoại liên kết tới files(file_id); dùng để join dữ liệu và đảm bảo toàn vẹn tham chiếu. |
| `caption` | `VARCHAR(500)` | YES |  |  |  |  | Chú thích riêng cho từng ảnh |
| `display_order` | `INT UNSIGNED` | NO | 0 |  | IDX: idx_gallery_images_gallery_order |  | Thứ tự hiển thị trên UI. |
| `taken_at` | `DATETIME` | YES |  |  | IDX: idx_gallery_images_status_time |  | Mốc thời gian nghiệp vụ, dùng cho audit, filter, timeline hoặc SLA. |
| `status` | `ENUM('ACTIVE','HIDDEN')` | NO | 'ACTIVE' |  | IDX: idx_gallery_images_status_time | ACTIVE, HIDDEN | ACTIVE=ảnh đang dùng, HIDDEN=ảnh bị ẩn khỏi gallery thường |
| `created_at` | `DATETIME` | NO | CURRENT_TIMESTAMP |  |  |  | Thời điểm tạo bản ghi; phục vụ audit, sắp xếp và truy vết lịch sử. |
| `created_by` | `BIGINT UNSIGNED` | YES |  |  |  |  | User tạo bản ghi; phục vụ audit và kiểm tra trách nhiệm thao tác. |
| `updated_at` | `DATETIME` | YES | NULL | ON UPDATE CURRENT_TIMESTAMP |  |  | Thời điểm cập nhật gần nhất; tự động cập nhật khi SQL có ON UPDATE CURRENT_TIMESTAMP. |
| `updated_by` | `BIGINT UNSIGNED` | YES |  |  |  |  | User cập nhật gần nhất; phục vụ audit. |
| `deleted_at` | `DATETIME` | YES |  |  |  |  | Thời điểm soft delete; NULL nghĩa là bản ghi chưa bị xóa mềm. |
| `deleted_by` | `BIGINT UNSIGNED` | YES |  |  |  |  | User thực hiện soft delete. |

**Primary Key:**
- `PRIMARY KEY (image_id)`

**Unique Constraints:**
- `uq_gallery_images_file` (file_id)

**Indexes:**
- `idx_gallery_images_gallery_order` (gallery_id, display_order)
- `idx_gallery_images_status_time` (status, taken_at)
- `idx_gallery_images_media_type` (media_type)
- `idx_gallery_images_thumbnail_file` (thumbnail_file_id)

**Foreign Keys:**
- `fk_gallery_images_gallery`: (gallery_id) → `galleries`(gallery_id) ON UPDATE CASCADE ON DELETE RESTRICT
- `fk_gallery_images_file`: (file_id) → `files`(file_id) ON UPDATE CASCADE ON DELETE RESTRICT
- `fk_gallery_images_thumbnail_file`: (thumbnail_file_id) → `files`(file_id) ON UPDATE CASCADE ON DELETE SET NULL

### 4.32. `photo_face_tags`

**Purpose / Table Comment:** Confirmed face tag metadata only. No biometric vector.

**Main Screens / UC Area:** Photo Tagging Metadata

**Columns:**

| Column | Type | Null | Default | Extra | Key / FK / Index | Enum Values | Notes / Meaning |
|---|---|---:|---|---|---|---|---|
| `face_tag_id` | `BIGINT UNSIGNED` | NO |  | AUTO_INCREMENT | PK |  | Khóa chính định danh duy nhất bản ghi trong bảng `photo_face_tags`. |
| `image_id` | `BIGINT UNSIGNED` | NO |  |  | IDX: idx_face_tags_image; FK: gallery_images(image_id) |  | Khóa ngoại liên kết tới gallery_images(image_id); dùng để join dữ liệu và đảm bảo toàn vẹn tham chiếu. |
| `visit_request_id` | `BIGINT UNSIGNED` | YES |  |  | FK: visit_requests(visit_request_id) |  | Khóa ngoại liên kết tới visit_requests(visit_request_id); dùng để join dữ liệu và đảm bảo toàn vẹn tham chiếu. |
| `guest_member_id` | `BIGINT UNSIGNED` | YES |  |  | IDX: idx_face_tags_guest; FK: visit_guest_members(guest_member_id) |  | Khóa ngoại liên kết tới visit_guest_members(guest_member_id); dùng để join dữ liệu và đảm bảo toàn vẹn tham chiếu. |
| `partner_contact_id` | `BIGINT UNSIGNED` | YES |  |  | IDX: idx_face_tags_partner_contact; FK: partner_contacts(contact_id) |  | Khóa ngoại liên kết tới partner_contacts(contact_id); dùng để join dữ liệu và đảm bảo toàn vẹn tham chiếu. |
| `display_name` | `VARCHAR(150)` | NO |  |  |  |  | Lưu thông tin `display_name` của bảng `photo_face_tags` để phục vụ màn hình và logic nghiệp vụ liên quan. |
| `bounding_box_x` | `DECIMAL(8,4)` | YES |  |  |  |  | Lưu thông tin `bounding_box_x` của bảng `photo_face_tags` để phục vụ màn hình và logic nghiệp vụ liên quan. |
| `bounding_box_y` | `DECIMAL(8,4)` | YES |  |  |  |  | Lưu thông tin `bounding_box_y` của bảng `photo_face_tags` để phục vụ màn hình và logic nghiệp vụ liên quan. |
| `bounding_box_width` | `DECIMAL(8,4)` | YES |  |  |  |  | Lưu thông tin `bounding_box_width` của bảng `photo_face_tags` để phục vụ màn hình và logic nghiệp vụ liên quan. |
| `bounding_box_height` | `DECIMAL(8,4)` | YES |  |  |  |  | Lưu thông tin `bounding_box_height` của bảng `photo_face_tags` để phục vụ màn hình và logic nghiệp vụ liên quan. |
| `tag_status` | `ENUM('MANUALLY_TAGGED','CONFIRMED','REMOVED')` | NO | 'MANUALLY_TAGGED' |  | IDX: idx_face_tags_status | MANUALLY_TAGGED, CONFIRMED, REMOVED | Trạng thái nghiệp vụ của bản ghi, dùng để kiểm soát flow/action/badge. |
| `confirmed_by` | `BIGINT UNSIGNED` | YES |  |  | FK: users(user_id) |  | Khóa ngoại liên kết tới users(user_id); dùng để join dữ liệu và đảm bảo toàn vẹn tham chiếu. |
| `confirmed_at` | `DATETIME` | YES |  |  |  |  | Mốc thời gian nghiệp vụ, dùng cho audit, filter, timeline hoặc SLA. |
| `created_at` | `DATETIME` | NO | CURRENT_TIMESTAMP |  |  |  | Thời điểm tạo bản ghi; phục vụ audit, sắp xếp và truy vết lịch sử. |
| `created_by` | `BIGINT UNSIGNED` | YES |  |  |  |  | User tạo bản ghi; phục vụ audit và kiểm tra trách nhiệm thao tác. |
| `removed_at` | `DATETIME` | YES |  |  |  |  | Mốc thời gian nghiệp vụ, dùng cho audit, filter, timeline hoặc SLA. |
| `removed_by` | `BIGINT UNSIGNED` | YES |  |  |  |  | User thực hiện hành động liên quan, dùng cho audit và phân quyền theo người thao tác. |

**Primary Key:**
- `PRIMARY KEY (face_tag_id)`

**Indexes:**
- `idx_face_tags_image` (image_id)
- `idx_face_tags_guest` (guest_member_id)
- `idx_face_tags_partner_contact` (partner_contact_id)
- `idx_face_tags_status` (tag_status)

**Foreign Keys:**
- `fk_face_tags_image`: (image_id) → `gallery_images`(image_id) ON UPDATE CASCADE ON DELETE CASCADE
- `fk_face_tags_visit_request`: (visit_request_id) → `visit_requests`(visit_request_id) ON UPDATE CASCADE ON DELETE SET NULL
- `fk_face_tags_guest`: (guest_member_id) → `visit_guest_members`(guest_member_id) ON UPDATE CASCADE ON DELETE SET NULL
- `fk_face_tags_partner_contact`: (partner_contact_id) → `partner_contacts`(contact_id) ON UPDATE CASCADE ON DELETE SET NULL
- `fk_face_tags_confirmed_by`: (confirmed_by) → `users`(user_id) ON UPDATE CASCADE ON DELETE SET NULL

### 4.33. `email_templates`

**Purpose / Table Comment:** Email templates with explicit VI/EN subject/body fields

**Main Screens / UC Area:** Email Template Management

**Columns:**

| Column | Type | Null | Default | Extra | Key / FK / Index | Enum Values | Notes / Meaning |
|---|---|---:|---|---|---|---|---|
| `email_template_id` | `BIGINT UNSIGNED` | NO |  | AUTO_INCREMENT | PK |  | Khóa chính định danh duy nhất bản ghi trong bảng `email_templates`. |
| `template_code` | `VARCHAR(100)` | NO |  |  | UNIQUE: uq_email_templates_code |  | Mã nghiệp vụ ngắn, ổn định, dùng cho seed, filter, rule và tích hợp. |
| `name` | `VARCHAR(150)` | NO |  |  |  |  | Tên hiển thị/chính thức của bản ghi. |
| `purpose` | `VARCHAR(100)` | NO |  |  | IDX: idx_email_templates_purpose_status |  | Lưu thông tin `purpose` của bảng `email_templates` để phục vụ màn hình và logic nghiệp vụ liên quan. |
| `campus_id` | `BIGINT UNSIGNED` | YES |  |  | IDX: idx_email_templates_campus_status; FK: campuses(campus_id) |  | Khóa ngoại liên kết tới campuses(campus_id); dùng để join dữ liệu và đảm bảo toàn vẹn tham chiếu. |
| `description` | `VARCHAR(500)` | YES |  |  |  |  | Mô tả chi tiết của bản ghi. |
| `status` | `ENUM('ACTIVE','INACTIVE')` | NO | 'ACTIVE' |  | IDX: idx_email_templates_status, idx_email_templates_purpose_status, idx_email_templates_campus_status | ACTIVE, INACTIVE | Trạng thái vòng đời của bản ghi; dùng cho filter, badge và chặn thao tác không hợp lệ. |
| `subject_vi` | `VARCHAR(255)` | YES |  |  |  |  | Lưu thông tin `subject_vi` của bảng `email_templates` để phục vụ màn hình và logic nghiệp vụ liên quan. |
| `body_vi` | `LONGTEXT` | YES |  |  |  |  | Lưu thông tin `body_vi` của bảng `email_templates` để phục vụ màn hình và logic nghiệp vụ liên quan. |
| `subject_en` | `VARCHAR(255)` | YES |  |  |  |  | Lưu thông tin `subject_en` của bảng `email_templates` để phục vụ màn hình và logic nghiệp vụ liên quan. |
| `body_en` | `LONGTEXT` | YES |  |  |  |  | Lưu thông tin `body_en` của bảng `email_templates` để phục vụ màn hình và logic nghiệp vụ liên quan. |
| `variables_text` | `VARCHAR(700)` | YES |  |  |  |  | Lưu thông tin `variables_text` của bảng `email_templates` để phục vụ màn hình và logic nghiệp vụ liên quan. |
| `created_at` | `DATETIME` | NO | CURRENT_TIMESTAMP |  |  |  | Thời điểm tạo bản ghi; phục vụ audit, sắp xếp và truy vết lịch sử. |
| `created_by` | `BIGINT UNSIGNED` | YES |  |  |  |  | User tạo bản ghi; phục vụ audit và kiểm tra trách nhiệm thao tác. |
| `updated_at` | `DATETIME` | YES | NULL | ON UPDATE CURRENT_TIMESTAMP |  |  | Thời điểm cập nhật gần nhất; tự động cập nhật khi SQL có ON UPDATE CURRENT_TIMESTAMP. |
| `updated_by` | `BIGINT UNSIGNED` | YES |  |  |  |  | User cập nhật gần nhất; phục vụ audit. |

**Primary Key:**
- `PRIMARY KEY (email_template_id)`

**Unique Constraints:**
- `uq_email_templates_code` (template_code)

**Indexes:**
- `idx_email_templates_status` (status)
- `idx_email_templates_purpose_status` (purpose, status)
- `idx_email_templates_campus_status` (campus_id, status)

**Foreign Keys:**
- `fk_email_templates_campus`: (campus_id) → `campuses`(campus_id) ON UPDATE CASCADE ON DELETE SET NULL

### 4.34. `sent_emails`

**Purpose / Table Comment:** Email Outbox / Delivery Tracking

**Main Screens / UC Area:** Email Outbox / Delivery Tracking

**Columns:**

| Column | Type | Null | Default | Extra | Key / FK / Index | Enum Values | Notes / Meaning |
|---|---|---:|---|---|---|---|---|
| `sent_email_id` | `BIGINT UNSIGNED` | NO |  | AUTO_INCREMENT | PK |  | Khóa chính định danh duy nhất bản ghi trong bảng `sent_emails`. |
| `email_template_id` | `BIGINT UNSIGNED` | YES |  |  | IDX: idx_sent_emails_template; FK: email_templates(email_template_id) |  | Khóa ngoại liên kết tới email_templates(email_template_id); dùng để join dữ liệu và đảm bảo toàn vẹn tham chiếu. |
| `related_type` | `VARCHAR(80)` | YES |  |  | IDX: idx_sent_emails_related |  | Loại/phân nhóm nghiệp vụ của bản ghi, dùng cho filter, rule và UI. |
| `related_id` | `BIGINT UNSIGNED` | YES |  |  | IDX: idx_sent_emails_related |  | Mã định danh/tham chiếu tới `related` hoặc entity liên quan. |
| `subject` | `VARCHAR(255)` | NO |  |  |  |  | Lưu thông tin `subject` của bảng `sent_emails` để phục vụ màn hình và logic nghiệp vụ liên quan. |
| `body_snapshot` | `LONGTEXT` | YES |  |  |  |  | Lưu thông tin `body_snapshot` của bảng `sent_emails` để phục vụ màn hình và logic nghiệp vụ liên quan. |
| `provider_thread_id` | `VARCHAR(255)` | YES |  |  | IDX: idx_sent_emails_provider_thread |  | Mã định danh/tham chiếu tới `provider_thread` hoặc entity liên quan. |
| `provider_message_id` | `VARCHAR(255)` | YES |  |  | IDX: idx_sent_emails_provider_message |  | Mã định danh/tham chiếu tới `provider_message` hoặc entity liên quan. |
| `retry_count` | `INT UNSIGNED` | NO | 0 |  |  |  | Số lượng/counter nghiệp vụ, dùng cho thống kê hoặc giới hạn. |
| `last_attempt_at` | `DATETIME` | YES |  |  |  |  | Mốc thời gian nghiệp vụ, dùng cho audit, filter, timeline hoặc SLA. |
| `delivered_at` | `DATETIME` | YES |  |  |  |  | Mốc thời gian nghiệp vụ, dùng cho audit, filter, timeline hoặc SLA. |
| `status` | `ENUM('QUEUED','SENT','FAILED')` | NO | 'QUEUED' |  | IDX: idx_sent_emails_status_time | QUEUED, SENT, FAILED | Trạng thái vòng đời của bản ghi; dùng cho filter, badge và chặn thao tác không hợp lệ. |
| `error_message` | `TEXT` | YES |  |  |  |  | Lưu thông tin `error_message` của bảng `sent_emails` để phục vụ màn hình và logic nghiệp vụ liên quan. |
| `sent_by` | `BIGINT UNSIGNED` | YES |  |  | IDX: idx_sent_emails_sent_by_time; FK: users(user_id) |  | Khóa ngoại liên kết tới users(user_id); dùng để join dữ liệu và đảm bảo toàn vẹn tham chiếu. |
| `sent_at` | `DATETIME` | YES |  |  | IDX: idx_sent_emails_sent_by_time |  | Mốc thời gian nghiệp vụ, dùng cho audit, filter, timeline hoặc SLA. |
| `created_at` | `DATETIME` | NO | CURRENT_TIMESTAMP |  | IDX: idx_sent_emails_status_time |  | Thời điểm tạo bản ghi; phục vụ audit, sắp xếp và truy vết lịch sử. |

**Primary Key:**
- `PRIMARY KEY (sent_email_id)`

**Indexes:**
- `idx_sent_emails_template` (email_template_id)
- `idx_sent_emails_related` (related_type, related_id)
- `idx_sent_emails_status_time` (status, created_at)
- `idx_sent_emails_sent_by_time` (sent_by, sent_at)
- `idx_sent_emails_provider_thread` (provider_thread_id)
- `idx_sent_emails_provider_message` (provider_message_id)

**Foreign Keys:**
- `fk_sent_emails_template`: (email_template_id) → `email_templates`(email_template_id) ON UPDATE CASCADE ON DELETE SET NULL
- `fk_sent_emails_sent_by`: (sent_by) → `users`(user_id) ON UPDATE CASCADE ON DELETE SET NULL

### 4.35. `sent_email_recipients`

**Purpose / Table Comment:** Email Delivery Tracking per Recipient

**Main Screens / UC Area:** Email Delivery Tracking per Recipient

**Columns:**

| Column | Type | Null | Default | Extra | Key / FK / Index | Enum Values | Notes / Meaning |
|---|---|---:|---|---|---|---|---|
| `sent_email_recipient_id` | `BIGINT UNSIGNED` | NO |  | AUTO_INCREMENT | PK |  | Khóa chính định danh duy nhất bản ghi trong bảng `sent_email_recipients`. |
| `sent_email_id` | `BIGINT UNSIGNED` | NO |  |  | IDX: idx_sent_email_recipients_sent_email; FK: sent_emails(sent_email_id) |  | Khóa ngoại liên kết tới sent_emails(sent_email_id); dùng để join dữ liệu và đảm bảo toàn vẹn tham chiếu. |
| `recipient_email` | `VARCHAR(150)` | NO |  |  | IDX: idx_sent_email_recipients_email_status, ft_sent_email_recipients_search |  | Lưu thông tin `recipient_email` của bảng `sent_email_recipients` để phục vụ màn hình và logic nghiệp vụ liên quan. |
| `recipient_name` | `VARCHAR(150)` | YES |  |  | IDX: ft_sent_email_recipients_search |  | Lưu thông tin `recipient_name` của bảng `sent_email_recipients` để phục vụ màn hình và logic nghiệp vụ liên quan. |
| `recipient_type` | `ENUM('TO','CC','BCC')` | NO | 'TO' |  |  | TO, CC, BCC | Loại/phân nhóm nghiệp vụ của bản ghi, dùng cho filter, rule và UI. |
| `delivery_status` | `ENUM('QUEUED','SENT','DELIVERED','FAILED','BOUNCED')` | NO | 'QUEUED' |  | IDX: idx_sent_email_recipients_email_status | QUEUED, SENT, DELIVERED, FAILED, BOUNCED | Trạng thái nghiệp vụ của bản ghi, dùng để kiểm soát flow/action/badge. |
| `provider_message_id` | `VARCHAR(255)` | YES |  |  |  |  | Mã định danh/tham chiếu tới `provider_message` hoặc entity liên quan. |
| `error_message` | `TEXT` | YES |  |  |  |  | Lưu thông tin `error_message` của bảng `sent_email_recipients` để phục vụ màn hình và logic nghiệp vụ liên quan. |
| `sent_at` | `DATETIME` | YES |  |  |  |  | Mốc thời gian nghiệp vụ, dùng cho audit, filter, timeline hoặc SLA. |
| `delivered_at` | `DATETIME` | YES |  |  |  |  | Mốc thời gian nghiệp vụ, dùng cho audit, filter, timeline hoặc SLA. |
| `created_at` | `DATETIME` | NO | CURRENT_TIMESTAMP |  |  |  | Thời điểm tạo bản ghi; phục vụ audit, sắp xếp và truy vết lịch sử. |

**Primary Key:**
- `PRIMARY KEY (sent_email_recipient_id)`

**Indexes:**
- `idx_sent_email_recipients_sent_email` (sent_email_id)
- `idx_sent_email_recipients_email_status` (recipient_email, delivery_status)
- `ft_sent_email_recipients_search` (recipient_email, recipient_name)

**Foreign Keys:**
- `fk_sent_email_recipients_email`: (sent_email_id) → `sent_emails`(sent_email_id) ON UPDATE CASCADE ON DELETE CASCADE

### 4.36. `notifications`

**Purpose / Table Comment:** In-app notifications

**Main Screens / UC Area:** Notification Center

**Columns:**

| Column | Type | Null | Default | Extra | Key / FK / Index | Enum Values | Notes / Meaning |
|---|---|---:|---|---|---|---|---|
| `notification_id` | `BIGINT UNSIGNED` | NO |  | AUTO_INCREMENT | PK |  | Khóa chính định danh duy nhất bản ghi trong bảng `notifications`. |
| `recipient_user_id` | `BIGINT UNSIGNED` | NO |  |  | IDX: idx_notifications_user_read_time; FK: users(user_id) |  | Khóa ngoại liên kết tới users(user_id); dùng để join dữ liệu và đảm bảo toàn vẹn tham chiếu. |
| `title` | `VARCHAR(255)` | NO |  |  |  |  | Tiêu đề/nội dung chính hiển thị trên UI. |
| `message` | `TEXT` | YES |  |  |  |  | Lưu thông tin `message` của bảng `notifications` để phục vụ màn hình và logic nghiệp vụ liên quan. |
| `notification_type` | `VARCHAR(80)` | NO |  |  | IDX: idx_notifications_type_time |  | Loại/phân nhóm nghiệp vụ của bản ghi, dùng cho filter, rule và UI. |
| `related_type` | `VARCHAR(80)` | YES |  |  | IDX: idx_notifications_related |  | Loại/phân nhóm nghiệp vụ của bản ghi, dùng cho filter, rule và UI. |
| `related_id` | `BIGINT UNSIGNED` | YES |  |  | IDX: idx_notifications_related |  | Mã định danh/tham chiếu tới `related` hoặc entity liên quan. |
| `is_read` | `BOOLEAN` | NO | FALSE |  | IDX: idx_notifications_user_read_time |  | Cờ boolean đánh dấu trạng thái hoặc điều kiện nghiệp vụ. |
| `read_at` | `DATETIME` | YES |  |  |  |  | Mốc thời gian nghiệp vụ, dùng cho audit, filter, timeline hoặc SLA. |
| `created_at` | `DATETIME` | NO | CURRENT_TIMESTAMP |  | IDX: idx_notifications_user_read_time, idx_notifications_type_time |  | Thời điểm tạo bản ghi; phục vụ audit, sắp xếp và truy vết lịch sử. |

**Primary Key:**
- `PRIMARY KEY (notification_id)`

**Indexes:**
- `idx_notifications_user_read_time` (recipient_user_id, is_read, created_at)
- `idx_notifications_related` (related_type, related_id)
- `idx_notifications_type_time` (notification_type, created_at)

**Foreign Keys:**
- `fk_notifications_user`: (recipient_user_id) → `users`(user_id) ON UPDATE CASCADE ON DELETE CASCADE

### 4.37. `calendar_events`

**Purpose / Table Comment:** Calendar events. Attendees/reminders are normalized in child tables.

**Main Screens / UC Area:** Calendar / Visit / Logistics / Personal Events

**Columns:**

| Column | Type | Null | Default | Extra | Key / FK / Index | Enum Values | Notes / Meaning |
|---|---|---:|---|---|---|---|---|
| `calendar_event_id` | `BIGINT UNSIGNED` | NO |  | AUTO_INCREMENT | PK |  | Khóa chính định danh duy nhất bản ghi trong bảng `calendar_events`. |
| `owner_user_id` | `BIGINT UNSIGNED` | NO |  |  | IDX: idx_calendar_owner_time; FK: users(user_id) |  | Khóa ngoại liên kết tới users(user_id); dùng để join dữ liệu và đảm bảo toàn vẹn tham chiếu. |
| `campus_id` | `BIGINT UNSIGNED` | YES |  |  | IDX: idx_calendar_campus_time; FK: campuses(campus_id) |  | Khóa ngoại liên kết tới campuses(campus_id); dùng để join dữ liệu và đảm bảo toàn vẹn tham chiếu. |
| `visit_instance_id` | `BIGINT UNSIGNED` | YES |  |  | IDX: idx_calendar_visit; FK: visit_request_campuses(visit_instance_id) |  | Khóa ngoại liên kết tới visit_request_campuses(visit_instance_id); dùng để join dữ liệu và đảm bảo toàn vẹn tham chiếu. |
| `logistics_item_id` | `BIGINT UNSIGNED` | YES |  |  | IDX: idx_calendar_logistics; FK: visit_logistics_items(logistics_item_id) |  | Khóa ngoại liên kết tới visit_logistics_items(logistics_item_id); dùng để join dữ liệu và đảm bảo toàn vẹn tham chiếu. |
| `source_type` | `ENUM('PERSONAL','VISIT','LOGISTICS','DEADLINE')` | NO | 'PERSONAL' |  | IDX: idx_calendar_source_status_time | PERSONAL, VISIT, LOGISTICS, DEADLINE | Loại/phân nhóm nghiệp vụ của bản ghi, dùng cho filter, rule và UI. |
| `title` | `VARCHAR(255)` | NO |  |  |  |  | Tiêu đề/nội dung chính hiển thị trên UI. |
| `description` | `TEXT` | YES |  |  |  |  | Mô tả chi tiết của bản ghi. |
| `location` | `VARCHAR(255)` | YES |  |  |  |  | Lưu thông tin `location` của bảng `calendar_events` để phục vụ màn hình và logic nghiệp vụ liên quan. |
| `start_at` | `DATETIME` | NO |  |  | IDX: idx_calendar_owner_time, idx_calendar_campus_time, idx_calendar_source_status_time |  | Mốc thời gian nghiệp vụ, dùng cho audit, filter, timeline hoặc SLA. |
| `end_at` | `DATETIME` | NO |  |  |  |  | Mốc thời gian nghiệp vụ, dùng cho audit, filter, timeline hoặc SLA. |
| `timezone` | `VARCHAR(50)` | NO | 'Asia/Ho_Chi_Minh' |  |  |  | Lưu thông tin `timezone` của bảng `calendar_events` để phục vụ màn hình và logic nghiệp vụ liên quan. |
| `is_all_day` | `BOOLEAN` | NO | FALSE |  |  |  | Cờ boolean đánh dấu trạng thái hoặc điều kiện nghiệp vụ. |
| `recurrence_rule` | `VARCHAR(500)` | YES |  |  |  |  | Lưu thông tin `recurrence_rule` của bảng `calendar_events` để phục vụ màn hình và logic nghiệp vụ liên quan. |
| `visibility` | `ENUM('PRIVATE','INTERNAL')` | NO | 'PRIVATE' |  |  | PRIVATE, INTERNAL | Lưu thông tin `visibility` của bảng `calendar_events` để phục vụ màn hình và logic nghiệp vụ liên quan. |
| `status` | `ENUM('ACTIVE','CANCELLED','DONE')` | NO | 'ACTIVE' |  | IDX: idx_calendar_source_status_time | ACTIVE, CANCELLED, DONE | Trạng thái vòng đời của bản ghi; dùng cho filter, badge và chặn thao tác không hợp lệ. |
| `created_at` | `DATETIME` | NO | CURRENT_TIMESTAMP |  |  |  | Thời điểm tạo bản ghi; phục vụ audit, sắp xếp và truy vết lịch sử. |
| `created_by` | `BIGINT UNSIGNED` | YES |  |  |  |  | User tạo bản ghi; phục vụ audit và kiểm tra trách nhiệm thao tác. |
| `updated_at` | `DATETIME` | YES | NULL | ON UPDATE CURRENT_TIMESTAMP |  |  | Thời điểm cập nhật gần nhất; tự động cập nhật khi SQL có ON UPDATE CURRENT_TIMESTAMP. |
| `updated_by` | `BIGINT UNSIGNED` | YES |  |  |  |  | User cập nhật gần nhất; phục vụ audit. |
| `deleted_at` | `DATETIME` | YES |  |  |  |  | Thời điểm soft delete; NULL nghĩa là bản ghi chưa bị xóa mềm. |
| `deleted_by` | `BIGINT UNSIGNED` | YES |  |  |  |  | User thực hiện soft delete. |

**Primary Key:**
- `PRIMARY KEY (calendar_event_id)`

**Indexes:**
- `idx_calendar_owner_time` (owner_user_id, start_at)
- `idx_calendar_campus_time` (campus_id, start_at)
- `idx_calendar_visit` (visit_instance_id)
- `idx_calendar_logistics` (logistics_item_id)
- `idx_calendar_source_status_time` (source_type, status, start_at)

**Foreign Keys:**
- `fk_calendar_owner`: (owner_user_id) → `users`(user_id) ON UPDATE CASCADE ON DELETE CASCADE
- `fk_calendar_campus`: (campus_id) → `campuses`(campus_id) ON UPDATE CASCADE ON DELETE SET NULL
- `fk_calendar_visit`: (visit_instance_id) → `visit_request_campuses`(visit_instance_id) ON UPDATE CASCADE ON DELETE SET NULL
- `fk_calendar_logistics`: (logistics_item_id) → `visit_logistics_items`(logistics_item_id) ON UPDATE CASCADE ON DELETE SET NULL

**Check Constraints:**
- `CHECK (end_at > start_at)`

### 4.38. `calendar_event_attendees`

**Purpose / Table Comment:** Calendar Attendees

**Main Screens / UC Area:** Calendar Attendees

**Columns:**

| Column | Type | Null | Default | Extra | Key / FK / Index | Enum Values | Notes / Meaning |
|---|---|---:|---|---|---|---|---|
| `calendar_event_attendee_id` | `BIGINT UNSIGNED` | NO |  | AUTO_INCREMENT | PK |  | Khóa chính định danh duy nhất bản ghi trong bảng `calendar_event_attendees`. |
| `calendar_event_id` | `BIGINT UNSIGNED` | NO |  |  | IDX: idx_calendar_attendees_event; FK: calendar_events(calendar_event_id) |  | Khóa ngoại liên kết tới calendar_events(calendar_event_id); dùng để join dữ liệu và đảm bảo toàn vẹn tham chiếu. |
| `user_id` | `BIGINT UNSIGNED` | YES |  |  | IDX: idx_calendar_attendees_user; FK: users(user_id) |  | Khóa ngoại liên kết tới users(user_id); dùng để join dữ liệu và đảm bảo toàn vẹn tham chiếu. |
| `attendee_email` | `VARCHAR(150)` | YES |  |  | IDX: idx_calendar_attendees_email |  | Lưu thông tin `attendee_email` của bảng `calendar_event_attendees` để phục vụ màn hình và logic nghiệp vụ liên quan. |
| `attendee_name` | `VARCHAR(150)` | YES |  |  |  |  | Lưu thông tin `attendee_name` của bảng `calendar_event_attendees` để phục vụ màn hình và logic nghiệp vụ liên quan. |
| `attendee_role` | `VARCHAR(80)` | YES |  |  |  |  | Lưu thông tin `attendee_role` của bảng `calendar_event_attendees` để phục vụ màn hình và logic nghiệp vụ liên quan. |
| `response_status` | `ENUM('NEEDS_ACTION','ACCEPTED','DECLINED','TENTATIVE')` | NO | 'NEEDS_ACTION' |  |  | NEEDS_ACTION, ACCEPTED, DECLINED, TENTATIVE | Trạng thái nghiệp vụ của bản ghi, dùng để kiểm soát flow/action/badge. |
| `created_at` | `DATETIME` | NO | CURRENT_TIMESTAMP |  |  |  | Thời điểm tạo bản ghi; phục vụ audit, sắp xếp và truy vết lịch sử. |

**Primary Key:**
- `PRIMARY KEY (calendar_event_attendee_id)`

**Indexes:**
- `idx_calendar_attendees_event` (calendar_event_id)
- `idx_calendar_attendees_user` (user_id)
- `idx_calendar_attendees_email` (attendee_email)

**Foreign Keys:**
- `fk_calendar_attendees_event`: (calendar_event_id) → `calendar_events`(calendar_event_id) ON UPDATE CASCADE ON DELETE CASCADE
- `fk_calendar_attendees_user`: (user_id) → `users`(user_id) ON UPDATE CASCADE ON DELETE SET NULL

### 4.39. `calendar_event_reminders`

**Purpose / Table Comment:** Calendar Reminders

**Main Screens / UC Area:** Calendar Reminders

**Columns:**

| Column | Type | Null | Default | Extra | Key / FK / Index | Enum Values | Notes / Meaning |
|---|---|---:|---|---|---|---|---|
| `calendar_event_reminder_id` | `BIGINT UNSIGNED` | NO |  | AUTO_INCREMENT | PK |  | Khóa chính định danh duy nhất bản ghi trong bảng `calendar_event_reminders`. |
| `calendar_event_id` | `BIGINT UNSIGNED` | NO |  |  | IDX: idx_calendar_reminders_event; FK: calendar_events(calendar_event_id) |  | Khóa ngoại liên kết tới calendar_events(calendar_event_id); dùng để join dữ liệu và đảm bảo toàn vẹn tham chiếu. |
| `reminder_type` | `ENUM('EMAIL','POPUP','IN_APP')` | NO | 'IN_APP' |  |  | EMAIL, POPUP, IN_APP | Loại/phân nhóm nghiệp vụ của bản ghi, dùng cho filter, rule và UI. |
| `minutes_before` | `INT UNSIGNED` | NO | 0 |  |  |  | Lưu thông tin `minutes_before` của bảng `calendar_event_reminders` để phục vụ màn hình và logic nghiệp vụ liên quan. |
| `scheduled_at` | `DATETIME` | YES |  |  | IDX: idx_calendar_reminders_status_schedule |  | Mốc thời gian nghiệp vụ, dùng cho audit, filter, timeline hoặc SLA. |
| `sent_at` | `DATETIME` | YES |  |  |  |  | Mốc thời gian nghiệp vụ, dùng cho audit, filter, timeline hoặc SLA. |
| `status` | `ENUM('PENDING','SENT','CANCELLED','FAILED')` | NO | 'PENDING' |  | IDX: idx_calendar_reminders_status_schedule | PENDING, SENT, CANCELLED, FAILED | Trạng thái vòng đời của bản ghi; dùng cho filter, badge và chặn thao tác không hợp lệ. |
| `created_at` | `DATETIME` | NO | CURRENT_TIMESTAMP |  |  |  | Thời điểm tạo bản ghi; phục vụ audit, sắp xếp và truy vết lịch sử. |

**Primary Key:**
- `PRIMARY KEY (calendar_event_reminder_id)`

**Indexes:**
- `idx_calendar_reminders_event` (calendar_event_id)
- `idx_calendar_reminders_status_schedule` (status, scheduled_at)

**Foreign Keys:**
- `fk_calendar_reminders_event`: (calendar_event_id) → `calendar_events`(calendar_event_id) ON UPDATE CASCADE ON DELETE CASCADE

### 4.40. `api_configurations`

**Purpose / Table Comment:** API config + encrypted credentials JSON

**Main Screens / UC Area:** External API Management

**Columns:**

| Column | Type | Null | Default | Extra | Key / FK / Index | Enum Values | Notes / Meaning |
|---|---|---:|---|---|---|---|---|
| `api_config_id` | `BIGINT UNSIGNED` | NO |  | AUTO_INCREMENT | PK |  | Khóa chính định danh duy nhất bản ghi trong bảng `api_configurations`. |
| `api_code` | `VARCHAR(100)` | NO |  |  | UNIQUE: uq_api_config_code |  | Mã nghiệp vụ ngắn, ổn định, dùng cho seed, filter, rule và tích hợp. |
| `name` | `VARCHAR(150)` | NO |  |  |  |  | Tên hiển thị/chính thức của bản ghi. |
| `provider_name` | `VARCHAR(150)` | YES |  |  | IDX: idx_api_provider_status |  | Lưu thông tin `provider_name` của bảng `api_configurations` để phục vụ màn hình và logic nghiệp vụ liên quan. |
| `purpose` | `VARCHAR(150)` | YES |  |  |  |  | Lưu thông tin `purpose` của bảng `api_configurations` để phục vụ màn hình và logic nghiệp vụ liên quan. |
| `base_url` | `VARCHAR(500)` | NO |  |  |  |  | URL/đường dẫn phục vụ hiển thị hoặc truy cập tài nguyên. |
| `default_method` | `ENUM('GET','POST','PUT','PATCH','DELETE')` | NO | 'POST' |  |  | GET, POST, PUT, PATCH, DELETE | Lưu thông tin `default_method` của bảng `api_configurations` để phục vụ màn hình và logic nghiệp vụ liên quan. |
| `auth_type` | `ENUM('NONE','API_KEY','BEARER_TOKEN','BASIC','OAUTH2','CUSTOM')` | NO | 'NONE' |  |  | NONE, API_KEY, BEARER_TOKEN, BASIC, OAUTH2, CUSTOM | Loại/phân nhóm nghiệp vụ của bản ghi, dùng cho filter, rule và UI. |
| `api_key_encrypted` | `VARCHAR(700)` | YES |  |  |  |  | Lưu thông tin `api_key_encrypted` của bảng `api_configurations` để phục vụ màn hình và logic nghiệp vụ liên quan. |
| `bearer_token_encrypted` | `VARCHAR(700)` | YES |  |  |  |  | Token/mã xác thực hoặc khóa tạm; phải bảo vệ và giới hạn thời hạn sử dụng. |
| `basic_username` | `VARCHAR(150)` | YES |  |  |  |  | Lưu thông tin `basic_username` của bảng `api_configurations` để phục vụ màn hình và logic nghiệp vụ liên quan. |
| `basic_password_encrypted` | `VARCHAR(700)` | YES |  |  |  |  | Lưu thông tin `basic_password_encrypted` của bảng `api_configurations` để phục vụ màn hình và logic nghiệp vụ liên quan. |
| `oauth_client_id` | `VARCHAR(255)` | YES |  |  |  |  | Mã định danh/tham chiếu tới `oauth_client` hoặc entity liên quan. |
| `oauth_client_secret_encrypted` | `VARCHAR(700)` | YES |  |  |  |  | Lưu thông tin `oauth_client_secret_encrypted` của bảng `api_configurations` để phục vụ màn hình và logic nghiệp vụ liên quan. |
| `oauth_token_url` | `VARCHAR(700)` | YES |  |  |  |  | URL/đường dẫn phục vụ hiển thị hoặc truy cập tài nguyên. |
| `oauth_scope` | `VARCHAR(500)` | YES |  |  |  |  | Lưu thông tin `oauth_scope` của bảng `api_configurations` để phục vụ màn hình và logic nghiệp vụ liên quan. |
| `body_template_text` | `LONGTEXT` | YES |  |  |  |  | Lưu thông tin `body_template_text` của bảng `api_configurations` để phục vụ màn hình và logic nghiệp vụ liên quan. |
| `rate_limit_per_minute` | `INT UNSIGNED` | YES |  |  |  |  | Lưu thông tin `rate_limit_per_minute` của bảng `api_configurations` để phục vụ màn hình và logic nghiệp vụ liên quan. |
| `monthly_quota` | `INT UNSIGNED` | YES |  |  |  |  | Lưu thông tin `monthly_quota` của bảng `api_configurations` để phục vụ màn hình và logic nghiệp vụ liên quan. |
| `retry_enabled` | `BOOLEAN` | NO | FALSE |  |  |  | Lưu thông tin `retry_enabled` của bảng `api_configurations` để phục vụ màn hình và logic nghiệp vụ liên quan. |
| `max_retries` | `INT UNSIGNED` | NO | 0 |  |  |  | Lưu thông tin `max_retries` của bảng `api_configurations` để phục vụ màn hình và logic nghiệp vụ liên quan. |
| `cache_ttl_seconds` | `INT UNSIGNED` | YES |  |  |  |  | Lưu thông tin `cache_ttl_seconds` của bảng `api_configurations` để phục vụ màn hình và logic nghiệp vụ liên quan. |
| `last_test_status` | `ENUM('SUCCESS','FAILED')` | YES |  |  | IDX: idx_api_config_test_status | SUCCESS, FAILED | Trạng thái nghiệp vụ của bản ghi, dùng để kiểm soát flow/action/badge. |
| `last_tested_at` | `DATETIME` | YES |  |  | IDX: idx_api_config_test_status |  | Mốc thời gian nghiệp vụ, dùng cho audit, filter, timeline hoặc SLA. |
| `last_test_message` | `TEXT` | YES |  |  |  |  | Lưu thông tin `last_test_message` của bảng `api_configurations` để phục vụ màn hình và logic nghiệp vụ liên quan. |
| `timeout_seconds` | `INT UNSIGNED` | NO | 30 |  |  |  | Lưu thông tin `timeout_seconds` của bảng `api_configurations` để phục vụ màn hình và logic nghiệp vụ liên quan. |
| `status` | `ENUM('ACTIVE','INACTIVE','DISABLED')` | NO | 'ACTIVE' |  | IDX: idx_api_config_status, idx_api_provider_status | ACTIVE, INACTIVE, DISABLED | Trạng thái vòng đời của bản ghi; dùng cho filter, badge và chặn thao tác không hợp lệ. |
| `created_at` | `DATETIME` | NO | CURRENT_TIMESTAMP |  |  |  | Thời điểm tạo bản ghi; phục vụ audit, sắp xếp và truy vết lịch sử. |
| `created_by` | `BIGINT UNSIGNED` | YES |  |  |  |  | User tạo bản ghi; phục vụ audit và kiểm tra trách nhiệm thao tác. |
| `updated_at` | `DATETIME` | YES | NULL | ON UPDATE CURRENT_TIMESTAMP |  |  | Thời điểm cập nhật gần nhất; tự động cập nhật khi SQL có ON UPDATE CURRENT_TIMESTAMP. |
| `updated_by` | `BIGINT UNSIGNED` | YES |  |  |  |  | User cập nhật gần nhất; phục vụ audit. |
| `deleted_at` | `DATETIME` | YES |  |  |  |  | Thời điểm soft delete; NULL nghĩa là bản ghi chưa bị xóa mềm. |
| `deleted_by` | `BIGINT UNSIGNED` | YES |  |  |  |  | User thực hiện soft delete. |

**Primary Key:**
- `PRIMARY KEY (api_config_id)`

**Unique Constraints:**
- `uq_api_config_code` (api_code)

**Indexes:**
- `idx_api_config_status` (status)
- `idx_api_config_test_status` (last_test_status, last_tested_at)
- `idx_api_provider_status` (provider_name, status)

### 4.41. `api_configuration_headers`

**Purpose / Table Comment:** External API Headers

**Main Screens / UC Area:** External API Headers

**Columns:**

| Column | Type | Null | Default | Extra | Key / FK / Index | Enum Values | Notes / Meaning |
|---|---|---:|---|---|---|---|---|
| `api_configuration_header_id` | `BIGINT UNSIGNED` | NO |  | AUTO_INCREMENT | PK |  | Khóa chính định danh duy nhất bản ghi trong bảng `api_configuration_headers`. |
| `api_config_id` | `BIGINT UNSIGNED` | NO |  |  | UNIQUE: uq_api_header_name; IDX: idx_api_headers_config; FK: api_configurations(api_config_id) |  | Khóa ngoại liên kết tới api_configurations(api_config_id); dùng để join dữ liệu và đảm bảo toàn vẹn tham chiếu. |
| `header_name` | `VARCHAR(150)` | NO |  |  | UNIQUE: uq_api_header_name |  | Lưu thông tin `header_name` của bảng `api_configuration_headers` để phục vụ màn hình và logic nghiệp vụ liên quan. |
| `header_value_encrypted` | `VARCHAR(1000)` | YES |  |  |  |  | Lưu thông tin `header_value_encrypted` của bảng `api_configuration_headers` để phục vụ màn hình và logic nghiệp vụ liên quan. |
| `is_secret` | `BOOLEAN` | NO | TRUE |  |  |  | Cờ boolean đánh dấu trạng thái hoặc điều kiện nghiệp vụ. |
| `created_at` | `DATETIME` | NO | CURRENT_TIMESTAMP |  |  |  | Thời điểm tạo bản ghi; phục vụ audit, sắp xếp và truy vết lịch sử. |

**Primary Key:**
- `PRIMARY KEY (api_configuration_header_id)`

**Unique Constraints:**
- `uq_api_header_name` (api_config_id, header_name)

**Indexes:**
- `idx_api_headers_config` (api_config_id)

**Foreign Keys:**
- `fk_api_headers_config`: (api_config_id) → `api_configurations`(api_config_id) ON UPDATE CASCADE ON DELETE CASCADE

### 4.42. `api_usage_quotas`

**Purpose / Table Comment:** API quota + counter per campus/month

**Main Screens / UC Area:** External API Quotas

**Columns:**

| Column | Type | Null | Default | Extra | Key / FK / Index | Enum Values | Notes / Meaning |
|---|---|---:|---|---|---|---|---|
| `api_usage_quota_id` | `BIGINT UNSIGNED` | NO |  | AUTO_INCREMENT | PK |  | Khóa chính định danh duy nhất bản ghi trong bảng `api_usage_quotas`. |
| `api_config_id` | `BIGINT UNSIGNED` | NO |  |  | UNIQUE: uq_api_quota_config_scope_period; FK: api_configurations(api_config_id) |  | Khóa ngoại liên kết tới api_configurations(api_config_id); dùng để join dữ liệu và đảm bảo toàn vẹn tham chiếu. |
| `campus_id` | `BIGINT UNSIGNED` | YES |  |  | IDX: idx_api_quota_campus_period; FK: campuses(campus_id) |  | NULL = global quota |
| `campus_scope_key` | `VARCHAR(36)` | NO | 'GLOBAL' |  | UNIQUE: uq_api_quota_config_scope_period |  | Lưu thông tin `campus_scope_key` của bảng `api_usage_quotas` để phục vụ màn hình và logic nghiệp vụ liên quan. |
| `period_yyyymm` | `CHAR(6)` | NO |  |  | UNIQUE: uq_api_quota_config_scope_period; IDX: idx_api_quota_campus_period, idx_api_quota_period |  | YYYYMM |
| `monthly_limit` | `INT UNSIGNED` | NO |  |  |  |  | Lưu thông tin `monthly_limit` của bảng `api_usage_quotas` để phục vụ màn hình và logic nghiệp vụ liên quan. |
| `used_count` | `INT UNSIGNED` | NO | 0 |  |  |  | Merged api_usage_counters table |
| `last_used_at` | `DATETIME` | YES |  |  |  |  | Mốc thời gian nghiệp vụ, dùng cho audit, filter, timeline hoặc SLA. |
| `created_at` | `DATETIME` | NO | CURRENT_TIMESTAMP |  |  |  | Thời điểm tạo bản ghi; phục vụ audit, sắp xếp và truy vết lịch sử. |
| `created_by` | `BIGINT UNSIGNED` | YES |  |  |  |  | User tạo bản ghi; phục vụ audit và kiểm tra trách nhiệm thao tác. |
| `updated_at` | `DATETIME` | YES | NULL | ON UPDATE CURRENT_TIMESTAMP |  |  | Thời điểm cập nhật gần nhất; tự động cập nhật khi SQL có ON UPDATE CURRENT_TIMESTAMP. |
| `updated_by` | `BIGINT UNSIGNED` | YES |  |  |  |  | User cập nhật gần nhất; phục vụ audit. |

**Primary Key:**
- `PRIMARY KEY (api_usage_quota_id)`

**Unique Constraints:**
- `uq_api_quota_config_scope_period` (api_config_id, campus_scope_key, period_yyyymm)

**Indexes:**
- `idx_api_quota_campus_period` (campus_id, period_yyyymm)
- `idx_api_quota_period` (period_yyyymm)

**Foreign Keys:**
- `fk_api_quota_config`: (api_config_id) → `api_configurations`(api_config_id) ON UPDATE CASCADE ON DELETE CASCADE
- `fk_api_quota_campus`: (campus_id) → `campuses`(campus_id) ON UPDATE CASCADE ON DELETE CASCADE

### 4.43. `api_request_logs`

**Purpose / Table Comment:** External API request logs. Never log full secret/token.

**Main Screens / UC Area:** External API Request Logs

**Columns:**

| Column | Type | Null | Default | Extra | Key / FK / Index | Enum Values | Notes / Meaning |
|---|---|---:|---|---|---|---|---|
| `api_request_log_id` | `BIGINT UNSIGNED` | NO |  | AUTO_INCREMENT | PK |  | Khóa chính định danh duy nhất bản ghi trong bảng `api_request_logs`. |
| `api_config_id` | `BIGINT UNSIGNED` | NO |  |  | IDX: idx_api_logs_config_time; FK: api_configurations(api_config_id) |  | Khóa ngoại liên kết tới api_configurations(api_config_id); dùng để join dữ liệu và đảm bảo toàn vẹn tham chiếu. |
| `campus_id` | `BIGINT UNSIGNED` | YES |  |  | IDX: idx_api_logs_campus_time; FK: campuses(campus_id) |  | Khóa ngoại liên kết tới campuses(campus_id); dùng để join dữ liệu và đảm bảo toàn vẹn tham chiếu. |
| `requested_by` | `BIGINT UNSIGNED` | YES |  |  | IDX: idx_api_logs_user_time; FK: users(user_id) |  | Khóa ngoại liên kết tới users(user_id); dùng để join dữ liệu và đảm bảo toàn vẹn tham chiếu. |
| `related_type` | `VARCHAR(80)` | YES |  |  | IDX: idx_api_logs_related |  | Loại/phân nhóm nghiệp vụ của bản ghi, dùng cho filter, rule và UI. |
| `related_id` | `BIGINT UNSIGNED` | YES |  |  | IDX: idx_api_logs_related |  | Mã định danh/tham chiếu tới `related` hoặc entity liên quan. |
| `endpoint` | `VARCHAR(500)` | NO |  |  |  |  | Lưu thông tin `endpoint` của bảng `api_request_logs` để phục vụ màn hình và logic nghiệp vụ liên quan. |
| `method` | `ENUM('GET','POST','PUT','PATCH','DELETE')` | NO |  |  |  | GET, POST, PUT, PATCH, DELETE | Lưu thông tin `method` của bảng `api_request_logs` để phục vụ màn hình và logic nghiệp vụ liên quan. |
| `http_status` | `INT` | YES |  |  |  |  | Trạng thái nghiệp vụ của bản ghi, dùng để kiểm soát flow/action/badge. |
| `response_time_ms` | `INT UNSIGNED` | YES |  |  |  |  | Lưu thông tin `response_time_ms` của bảng `api_request_logs` để phục vụ màn hình và logic nghiệp vụ liên quan. |
| `request_size_bytes` | `BIGINT UNSIGNED` | YES |  |  |  |  | Lưu thông tin `request_size_bytes` của bảng `api_request_logs` để phục vụ màn hình và logic nghiệp vụ liên quan. |
| `response_size_bytes` | `BIGINT UNSIGNED` | YES |  |  |  |  | Lưu thông tin `response_size_bytes` của bảng `api_request_logs` để phục vụ màn hình và logic nghiệp vụ liên quan. |
| `success` | `BOOLEAN` | NO | FALSE |  | IDX: idx_api_logs_success_time |  | Lưu thông tin `success` của bảng `api_request_logs` để phục vụ màn hình và logic nghiệp vụ liên quan. |
| `error_code` | `VARCHAR(100)` | YES |  |  |  |  | Mã nghiệp vụ ngắn, ổn định, dùng cho seed, filter, rule và tích hợp. |
| `error_message` | `TEXT` | YES |  |  |  |  | Lưu thông tin `error_message` của bảng `api_request_logs` để phục vụ màn hình và logic nghiệp vụ liên quan. |
| `created_at` | `DATETIME` | NO | CURRENT_TIMESTAMP |  | IDX: idx_api_logs_config_time, idx_api_logs_campus_time, idx_api_logs_user_time, idx_api_logs_success_time |  | Thời điểm tạo bản ghi; phục vụ audit, sắp xếp và truy vết lịch sử. |

**Primary Key:**
- `PRIMARY KEY (api_request_log_id)`

**Indexes:**
- `idx_api_logs_config_time` (api_config_id, created_at)
- `idx_api_logs_campus_time` (campus_id, created_at)
- `idx_api_logs_user_time` (requested_by, created_at)
- `idx_api_logs_success_time` (success, created_at)
- `idx_api_logs_related` (related_type, related_id)

**Foreign Keys:**
- `fk_api_logs_config`: (api_config_id) → `api_configurations`(api_config_id) ON UPDATE CASCADE ON DELETE RESTRICT
- `fk_api_logs_campus`: (campus_id) → `campuses`(campus_id) ON UPDATE CASCADE ON DELETE SET NULL
- `fk_api_logs_user`: (requested_by) → `users`(user_id) ON UPDATE CASCADE ON DELETE SET NULL

### 4.44. `agenda_templates`

**Purpose / Table Comment:** Agenda Template Management

**Main Screens / UC Area:** Agenda Template Management

**Columns:**

| Column | Type | Null | Default | Extra | Key / FK / Index | Enum Values | Notes / Meaning |
|---|---|---:|---|---|---|---|---|
| `agenda_template_id` | `BIGINT UNSIGNED` | NO |  | AUTO_INCREMENT | PK |  | Khóa chính định danh duy nhất bản ghi trong bảng `agenda_templates`. |
| `campus_id` | `BIGINT UNSIGNED` | YES |  |  | IDX: idx_agenda_templates_campus_status; FK: campuses(campus_id) |  | Khóa ngoại liên kết tới campuses(campus_id); dùng để join dữ liệu và đảm bảo toàn vẹn tham chiếu. |
| `campus_scope_key` | `VARCHAR(36)` | NO | 'GLOBAL' |  | UNIQUE: uq_agenda_template_scope_name |  | Lưu thông tin `campus_scope_key` của bảng `agenda_templates` để phục vụ màn hình và logic nghiệp vụ liên quan. |
| `name` | `VARCHAR(150)` | NO |  |  | UNIQUE: uq_agenda_template_scope_name |  | Tên hiển thị/chính thức của bản ghi. |
| `description` | `TEXT` | YES |  |  |  |  | Mô tả chi tiết của bản ghi. |
| `status` | `ENUM('ACTIVE','INACTIVE')` | NO | 'ACTIVE' |  | IDX: idx_agenda_templates_status, idx_agenda_templates_campus_status | ACTIVE, INACTIVE | Trạng thái vòng đời của bản ghi; dùng cho filter, badge và chặn thao tác không hợp lệ. |
| `created_at` | `DATETIME` | NO | CURRENT_TIMESTAMP |  |  |  | Thời điểm tạo bản ghi; phục vụ audit, sắp xếp và truy vết lịch sử. |
| `created_by` | `BIGINT UNSIGNED` | YES |  |  |  |  | User tạo bản ghi; phục vụ audit và kiểm tra trách nhiệm thao tác. |
| `updated_at` | `DATETIME` | YES | NULL | ON UPDATE CURRENT_TIMESTAMP |  |  | Thời điểm cập nhật gần nhất; tự động cập nhật khi SQL có ON UPDATE CURRENT_TIMESTAMP. |
| `updated_by` | `BIGINT UNSIGNED` | YES |  |  |  |  | User cập nhật gần nhất; phục vụ audit. |
| `deleted_at` | `DATETIME` | YES |  |  |  |  | Thời điểm soft delete; NULL nghĩa là bản ghi chưa bị xóa mềm. |
| `deleted_by` | `BIGINT UNSIGNED` | YES |  |  |  |  | User thực hiện soft delete. |

**Primary Key:**
- `PRIMARY KEY (agenda_template_id)`

**Unique Constraints:**
- `uq_agenda_template_scope_name` (campus_scope_key, name)

**Indexes:**
- `idx_agenda_templates_status` (status)
- `idx_agenda_templates_campus_status` (campus_id, status)

**Foreign Keys:**
- `fk_agenda_templates_campus`: (campus_id) → `campuses`(campus_id) ON UPDATE CASCADE ON DELETE SET NULL

### 4.45. `agenda_template_items`

**Purpose / Table Comment:** Agenda Template Items

**Main Screens / UC Area:** Agenda Template Items

**Columns:**

| Column | Type | Null | Default | Extra | Key / FK / Index | Enum Values | Notes / Meaning |
|---|---|---:|---|---|---|---|---|
| `agenda_template_item_id` | `BIGINT UNSIGNED` | NO |  | AUTO_INCREMENT | PK |  | Khóa chính định danh duy nhất bản ghi trong bảng `agenda_template_items`. |
| `agenda_template_id` | `BIGINT UNSIGNED` | NO |  |  | IDX: idx_agenda_template_items_template_order; FK: agenda_templates(agenda_template_id) |  | Khóa ngoại liên kết tới agenda_templates(agenda_template_id); dùng để join dữ liệu và đảm bảo toàn vẹn tham chiếu. |
| `display_order` | `INT UNSIGNED` | NO | 0 |  | IDX: idx_agenda_template_items_template_order |  | Thứ tự hiển thị trên UI. |
| `start_time` | `TIME` | YES |  |  |  |  | Lưu thông tin `start_time` của bảng `agenda_template_items` để phục vụ màn hình và logic nghiệp vụ liên quan. |
| `end_time` | `TIME` | YES |  |  |  |  | Lưu thông tin `end_time` của bảng `agenda_template_items` để phục vụ màn hình và logic nghiệp vụ liên quan. |
| `title` | `VARCHAR(255)` | NO |  |  |  |  | Tiêu đề/nội dung chính hiển thị trên UI. |
| `description` | `TEXT` | YES |  |  |  |  | Mô tả chi tiết của bản ghi. |
| `created_at` | `DATETIME` | NO | CURRENT_TIMESTAMP |  |  |  | Thời điểm tạo bản ghi; phục vụ audit, sắp xếp và truy vết lịch sử. |

**Primary Key:**
- `PRIMARY KEY (agenda_template_item_id)`

**Indexes:**
- `idx_agenda_template_items_template_order` (agenda_template_id, display_order)

**Foreign Keys:**
- `fk_agenda_template_items_template`: (agenda_template_id) → `agenda_templates`(agenda_template_id) ON UPDATE CASCADE ON DELETE CASCADE

**Check Constraints:**
- `CHECK (end_time IS NULL OR start_time IS NULL OR end_time > start_time)`

### 4.46. `audit_logs`

**Purpose / Table Comment:** General audit log

**Main Screens / UC Area:** Audit Trail

**Columns:**

| Column | Type | Null | Default | Extra | Key / FK / Index | Enum Values | Notes / Meaning |
|---|---|---:|---|---|---|---|---|
| `audit_log_id` | `BIGINT UNSIGNED` | NO |  | AUTO_INCREMENT | PK |  | Khóa chính định danh duy nhất bản ghi trong bảng `audit_logs`. |
| `actor_user_id` | `BIGINT UNSIGNED` | YES |  |  | IDX: idx_audit_actor_time; FK: users(user_id) |  | Khóa ngoại liên kết tới users(user_id); dùng để join dữ liệu và đảm bảo toàn vẹn tham chiếu. |
| `campus_id` | `BIGINT UNSIGNED` | YES |  |  | IDX: idx_audit_campus_time; FK: campuses(campus_id) |  | Khóa ngoại liên kết tới campuses(campus_id); dùng để join dữ liệu và đảm bảo toàn vẹn tham chiếu. |
| `action` | `VARCHAR(100)` | NO |  |  | IDX: idx_audit_action_time |  | Lưu thông tin `action` của bảng `audit_logs` để phục vụ màn hình và logic nghiệp vụ liên quan. |
| `entity_type` | `VARCHAR(100)` | NO |  |  | IDX: idx_audit_entity |  | Loại/phân nhóm nghiệp vụ của bản ghi, dùng cho filter, rule và UI. |
| `entity_id` | `BIGINT UNSIGNED` | YES |  |  | IDX: idx_audit_entity |  | Mã định danh/tham chiếu tới `entity` hoặc entity liên quan. |
| `ip_address` | `VARCHAR(45)` | YES |  |  |  |  | Địa chỉ IP của client, phục vụ audit/security log. |
| `user_agent` | `VARCHAR(500)` | YES |  |  |  |  | Thông tin trình duyệt/thiết bị, phục vụ audit/security log. |
| `request_id` | `VARCHAR(100)` | YES |  |  | IDX: idx_audit_request |  | Mã định danh/tham chiếu tới `request` hoặc entity liên quan. |
| `created_at` | `DATETIME` | NO | CURRENT_TIMESTAMP |  | IDX: idx_audit_actor_time, idx_audit_action_time, idx_audit_campus_time |  | Thời điểm tạo bản ghi; phục vụ audit, sắp xếp và truy vết lịch sử. |

**Primary Key:**
- `PRIMARY KEY (audit_log_id)`

**Indexes:**
- `idx_audit_actor_time` (actor_user_id, created_at)
- `idx_audit_entity` (entity_type, entity_id)
- `idx_audit_action_time` (action, created_at)
- `idx_audit_campus_time` (campus_id, created_at)
- `idx_audit_request` (request_id)

**Foreign Keys:**
- `fk_audit_actor`: (actor_user_id) → `users`(user_id) ON UPDATE CASCADE ON DELETE SET NULL
- `fk_audit_campus`: (campus_id) → `campuses`(campus_id) ON UPDATE CASCADE ON DELETE SET NULL

### 4.47. `audit_log_changes`

**Purpose / Table Comment:** Audit Field-level Changes

**Main Screens / UC Area:** Audit Field-level Changes

**Columns:**

| Column | Type | Null | Default | Extra | Key / FK / Index | Enum Values | Notes / Meaning |
|---|---|---:|---|---|---|---|---|
| `audit_log_change_id` | `BIGINT UNSIGNED` | NO |  | AUTO_INCREMENT | PK |  | Khóa chính định danh duy nhất bản ghi trong bảng `audit_log_changes`. |
| `audit_log_id` | `BIGINT UNSIGNED` | NO |  |  | IDX: idx_audit_changes_log; FK: audit_logs(audit_log_id) |  | Khóa ngoại liên kết tới audit_logs(audit_log_id); dùng để join dữ liệu và đảm bảo toàn vẹn tham chiếu. |
| `field_name` | `VARCHAR(150)` | NO |  |  | IDX: idx_audit_changes_field |  | Lưu thông tin `field_name` của bảng `audit_log_changes` để phục vụ màn hình và logic nghiệp vụ liên quan. |
| `old_value_text` | `LONGTEXT` | YES |  |  |  |  | Lưu thông tin `old_value_text` của bảng `audit_log_changes` để phục vụ màn hình và logic nghiệp vụ liên quan. |
| `new_value_text` | `LONGTEXT` | YES |  |  |  |  | Lưu thông tin `new_value_text` của bảng `audit_log_changes` để phục vụ màn hình và logic nghiệp vụ liên quan. |
| `created_at` | `DATETIME` | NO | CURRENT_TIMESTAMP |  |  |  | Thời điểm tạo bản ghi; phục vụ audit, sắp xếp và truy vết lịch sử. |

**Primary Key:**
- `PRIMARY KEY (audit_log_change_id)`

**Indexes:**
- `idx_audit_changes_log` (audit_log_id)
- `idx_audit_changes_field` (field_name)

**Foreign Keys:**
- `fk_audit_changes_log`: (audit_log_id) → `audit_logs`(audit_log_id) ON UPDATE CASCADE ON DELETE CASCADE

## 5. Views

| # | View | Defined Line |
|---:|---|---:|
| 1 | `vw_visit_requests_for_ho` | 4193 |
| 2 | `vw_visit_requests_for_staff_leader` | 4223 |
| 3 | `vw_visit_requests_for_ho` | 4344 |
| 4 | `vw_visit_requests_for_staff_leader` | 4394 |
| 5 | `vw_visit_requests_for_admin` | 4480 |
| 6 | `vw_visit_request_progress_summary` | 4496 |

## 6. Triggers

| # | Trigger | Timing / Event | Table | Line |
|---:|---|---|---|---:|
| 1 | `trg_departments_one_ic_bi` | `BEFORE INSERT` | `departments` | 1883 |
| 2 | `trg_departments_one_ic_bu` | `BEFORE UPDATE` | `departments` | 1902 |
| 3 | `trg_users_validate_bi` | `BEFORE INSERT` | `users` | 1922 |
| 4 | `trg_users_validate_bu` | `BEFORE UPDATE` | `users` | 1988 |
| 5 | `trg_auth_providers_validate_bi` | `BEFORE INSERT` | `user_auth_providers` | 2054 |
| 6 | `trg_auth_providers_validate_bu` | `BEFORE UPDATE` | `user_auth_providers` | 2064 |
| 7 | `trg_sessions_validate_bi` | `BEFORE INSERT` | `user_sessions` | 2074 |
| 8 | `trg_visit_requests_decision_validate_bi` | `BEFORE INSERT` | `visit_requests` | 2117 |
| 9 | `trg_visit_requests_decision_validate_bu` | `BEFORE UPDATE` | `visit_requests` | 2166 |
| 10 | `trg_visit_requests_cancel_validate_bu` | `BEFORE UPDATE` | `visit_requests` | 2221 |
| 11 | `trg_visit_campuses_cancel_validate_bu` | `BEFORE UPDATE` | `visit_request_campuses` | 2255 |
| 12 | `trg_visit_campuses_assignment_validate_bi` | `BEFORE INSERT` | `visit_request_campuses` | 2313 |
| 13 | `trg_visit_campuses_assignment_validate_bu` | `BEFORE UPDATE` | `visit_request_campuses` | 2399 |
| 14 | `trg_api_usage_quotas_scope_bi` | `BEFORE INSERT` | `api_usage_quotas` | 2489 |
| 15 | `trg_api_usage_quotas_scope_bu` | `BEFORE UPDATE` | `api_usage_quotas` | 2496 |
| 16 | `trg_agenda_templates_scope_bi` | `BEFORE INSERT` | `agenda_templates` | 2503 |
| 17 | `trg_agenda_templates_scope_bu` | `BEFORE UPDATE` | `agenda_templates` | 2510 |
| 18 | `trg_feedbacks_not_self_bi` | `BEFORE INSERT` | `feedbacks` | 2520 |
| 19 | `trg_feedbacks_not_self_bu` | `BEFORE UPDATE` | `feedbacks` | 2530 |

## 7. Enum Catalog

| # | Table.Column | Enum Values | Null | Default |
|---:|---|---|---:|---|
| 1 | `roles.status` | `ACTIVE, INACTIVE` | NO | 'ACTIVE' |
| 2 | `campuses.status` | `ACTIVE, INACTIVE` | NO | 'ACTIVE' |
| 3 | `departments.department_type` | `IC, GENERAL` | NO |  |
| 4 | `departments.status` | `ACTIVE, INACTIVE` | NO | 'ACTIVE' |
| 5 | `users.sub_role` | `LEADER, STAFF` | YES |  |
| 6 | `users.gender` | `MALE, FEMALE, OTHER` | YES |  |
| 7 | `users.status` | `ACTIVE, INACTIVE, LOCKED` | NO | 'ACTIVE' |
| 8 | `users.created_via` | `MANUAL_CREATED, VISITOR_FORM, SSO_AUTO_PROVISION` | NO | 'MANUAL_CREATED' |
| 9 | `user_auth_providers.provider_type` | `LOCAL_PASSWORD, GOOGLE_SSO, FEID` | NO |  |
| 10 | `user_sessions.login_portal` | `VISITOR, INTERNAL` | NO |  |
| 11 | `otp_tokens.token_type` | `OTP_CODE, MAGIC_LINK` | NO | 'OTP_CODE' |
| 12 | `otp_tokens.purpose` | `VISIT_REQUEST_VERIFY, CHANGE_SENSITIVE_ACTION` | NO |  |
| 13 | `login_logs.login_portal` | `VISITOR, INTERNAL` | NO |  |
| 14 | `login_logs.provider_type` | `LOCAL_PASSWORD, GOOGLE_SSO, FEID` | YES |  |
| 15 | `login_logs.status` | `SUCCESS, FAILED, BLOCKED` | NO |  |
| 16 | `security_events.event_type` | `SSO_LOGIN, PORTAL_VALIDATION, CAMPUS_VALIDATION, VISITOR_AUTO_PROVISION, SESSION_CREATED, SESSION_REVOKED, SESSION_EXPIRED, TOKEN_REFRESH, SECURITY_POLICY_CHECK` | NO |  |
| 17 | `security_events.result` | `SUCCESS, FAILED, BLOCKED` | NO | 'SUCCESS' |
| 18 | `security_events.failure_reason_code` | `ACCOUNT_NOT_FOUND, ACCOUNT_DISABLED, PORTAL_MISMATCH, CAMPUS_MISMATCH, ROLE_MISMATCH, SSO_PROVIDER_ERROR, INVALID_SSO_CLAIMS, VISITOR_AUTO_PROVISION_DISABLED, SESSION_EXPIRED, TOKEN_REVOKED, SUSPICIOUS_IP, UNKNOWN` | YES |  |
| 19 | `security_events.severity` | `LOW, MEDIUM, HIGH, CRITICAL` | NO | 'LOW' |
| 20 | `security_events.login_portal` | `VISITOR, INTERNAL` | YES |  |
| 21 | `security_events.provider_type` | `GOOGLE_SSO, FEID` | YES |  |
| 22 | `files.storage_provider` | `LOCAL, S3, AZURE, GCS, GOOGLE_DRIVE, OTHER` | NO | 'LOCAL' |
| 23 | `partners.partner_type` | `UNIVERSITY, COMPANY, GOVERNMENT, NGO, OTHER` | NO | 'UNIVERSITY' |
| 24 | `partners.cooperation_status` | `POTENTIAL, ACTIVE, INACTIVE, BLACKLISTED` | NO | 'POTENTIAL' |
| 25 | `partners.profile_status` | `DRAFT, PENDING_APPROVAL, APPROVED, REJECTED` | NO | 'APPROVED' |
| 26 | `partners.visibility` | `PRIVATE, INTERNAL, PUBLIC` | NO | 'PUBLIC' |
| 27 | `partner_contacts.source_type` | `MANUAL, BUSINESS_CARD_OCR, IMPORT` | NO | 'MANUAL' |
| 28 | `partner_contacts.status` | `ACTIVE, INACTIVE` | NO | 'ACTIVE' |
| 29 | `documents.owner_type` | `GENERAL, VISIT, PARTNER, MINUTES, NEWS, LOGISTICS, REPORT` | NO | 'GENERAL' |
| 30 | `documents.status` | `DRAFT, PUBLISHED, ARCHIVED` | NO | 'DRAFT' |
| 31 | `visit_requests.created_source` | `VISITOR_SUBMITTED, STAFF_CREATED` | NO | 'VISITOR_SUBMITTED' |
| 32 | `visit_requests.visit_scope` | `SINGLE_CAMPUS, MULTI_CAMPUS` | NO | 'SINGLE_CAMPUS' |
| 33 | `visit_requests.visit_type` | `CAMPUS_TOUR, MEETING, WORKSHOP, SIGNING_CEREMONY, EXCHANGE, OTHER` | NO | 'CAMPUS_TOUR' |
| 34 | `visit_requests.working_language` | `VI, EN` | NO | 'EN' |
| 35 | `visit_requests.transportation_type` | `SELF_ARRANGED, FPTU_SUPPORT, UNKNOWN, OTHER` | NO | 'UNKNOWN' |
| 36 | `visit_requests.media_consent_status` | `AGREED, DECLINED` | NO | 'DECLINED' |
| 37 | `visit_requests.status` | `PENDING_APPROVAL, APPROVED, REJECTED, CANCELLED` | NO | 'PENDING_APPROVAL' |
| 38 | `visit_requests.decision_actor_role` | `HO, STAFF_LEADER` | YES |  |
| 39 | `visit_request_campuses.status` | `WAITING_REQUEST_APPROVAL, WAITING_HOST_ASSIGNMENT, ASSIGNED, BEFORE_VISIT, DURING_VISIT, AFTER_VISIT, CLOSED, CANCELLED` | NO | 'WAITING_REQUEST_APPROVAL' |
| 40 | `visit_request_campuses.cancellation_actor_type` | `VISITOR, HOST` | YES |  |
| 41 | `visit_request_campuses.cancellation_source` | `SELF_SERVICE, EXTERNAL_CONFIRMATION` | YES |  |
| 42 | `visit_guest_members.member_type` | `GUEST, EXTERNAL_SUPPORT` | NO | 'GUEST' |
| 43 | `visit_participants.participant_role` | `IC_HOST, IC_SUPPORT, DEPT_SUPPORT, STUDENT` | NO | 'IC_SUPPORT' |
| 44 | `visit_participants.status` | `INVITED, ACCEPTED, DECLINED, ASSIGNED, REMOVED` | NO | 'INVITED' |
| 45 | `visit_logistics_items.item_type` | `ROOM, TRANSPORT, MEAL, EQUIPMENT, BANNER, LED, OTHER` | NO |  |
| 46 | `visit_logistics_items.status` | `PLANNED, REQUESTED, CHANGE_PROPOSED, RECEIVED, ASSIGNED, ACCEPTED, IN_PROGRESS, READY, DONE, REJECTED, CANCELLED` | NO | 'PLANNED' |
| 47 | `visit_logistics_items.priority` | `LOW, MEDIUM, HIGH, URGENT` | NO | 'MEDIUM' |
| 48 | `visit_logistics_items.proposal_response` | `ACCEPTED, REJECTED` | YES |  |
| 49 | `minutes.status` | `DRAFT, SAVED` | NO | 'DRAFT' |
| 50 | `minute_participants.attendance_status` | `PRESENT, ABSENT, EXCUSED` | NO | 'PRESENT' |
| 51 | `minute_action_items.status` | `TODO, IN_PROGRESS, DONE, CANCELLED` | NO | 'TODO' |
| 52 | `feedbacks.submitter_role` | `VISITOR, HOST, LOGISTICS` | NO |  |
| 53 | `feedbacks.target_role` | `VISITOR, HOST, LOGISTICS` | NO |  |
| 54 | `news.status` | `PENDING_REVIEW, REJECTED, PUBLISHED, HIDDEN` | NO | 'PENDING_REVIEW' |
| 55 | `news_section_files.usage_type` | `INLINE_IMAGE, ATTACHMENT` | NO | 'INLINE_IMAGE' |
| 56 | `faqs.faq_type` | `PROGRAM, TUITION_FEE, VISA, DORMITORY, VISIT_REQUEST, SECURITY, LOGISTICS, OTHER` | NO | 'OTHER' |
| 57 | `faqs.language_code` | `vi, en` | NO | 'vi' |
| 58 | `faqs.status` | `PUBLISHED, HIDDEN` | NO | 'HIDDEN' |
| 59 | `galleries.status` | `PUBLISHED, HIDDEN` | NO | 'HIDDEN' |
| 60 | `galleries.visibility` | `PRIVATE, INTERNAL, PUBLIC` | NO | 'INTERNAL' |
| 61 | `gallery_images.media_type` | `IMAGE, VIDEO` | NO | 'IMAGE' |
| 62 | `gallery_images.status` | `ACTIVE, HIDDEN` | NO | 'ACTIVE' |
| 63 | `photo_face_tags.tag_status` | `MANUALLY_TAGGED, CONFIRMED, REMOVED` | NO | 'MANUALLY_TAGGED' |
| 64 | `email_templates.status` | `ACTIVE, INACTIVE` | NO | 'ACTIVE' |
| 65 | `sent_emails.status` | `QUEUED, SENT, FAILED` | NO | 'QUEUED' |
| 66 | `sent_email_recipients.recipient_type` | `TO, CC, BCC` | NO | 'TO' |
| 67 | `sent_email_recipients.delivery_status` | `QUEUED, SENT, DELIVERED, FAILED, BOUNCED` | NO | 'QUEUED' |
| 68 | `calendar_events.source_type` | `PERSONAL, VISIT, LOGISTICS, DEADLINE` | NO | 'PERSONAL' |
| 69 | `calendar_events.visibility` | `PRIVATE, INTERNAL` | NO | 'PRIVATE' |
| 70 | `calendar_events.status` | `ACTIVE, CANCELLED, DONE` | NO | 'ACTIVE' |
| 71 | `calendar_event_attendees.response_status` | `NEEDS_ACTION, ACCEPTED, DECLINED, TENTATIVE` | NO | 'NEEDS_ACTION' |
| 72 | `calendar_event_reminders.reminder_type` | `EMAIL, POPUP, IN_APP` | NO | 'IN_APP' |
| 73 | `calendar_event_reminders.status` | `PENDING, SENT, CANCELLED, FAILED` | NO | 'PENDING' |
| 74 | `api_configurations.default_method` | `GET, POST, PUT, PATCH, DELETE` | NO | 'POST' |
| 75 | `api_configurations.auth_type` | `NONE, API_KEY, BEARER_TOKEN, BASIC, OAUTH2, CUSTOM` | NO | 'NONE' |
| 76 | `api_configurations.last_test_status` | `SUCCESS, FAILED` | YES |  |
| 77 | `api_configurations.status` | `ACTIVE, INACTIVE, DISABLED` | NO | 'ACTIVE' |
| 78 | `api_request_logs.method` | `GET, POST, PUT, PATCH, DELETE` | NO |  |
| 79 | `agenda_templates.status` | `ACTIVE, INACTIVE` | NO | 'ACTIVE' |

## 8. Foreign Key Catalog

| # | Table | FK Name | Source Column(s) | References | Actions |
|---:|---|---|---|---|---|
| 1 | `departments` | `fk_departments_campus` | `campus_id` | `campuses(campus_id)` | ON UPDATE CASCADE ON DELETE RESTRICT |
| 2 | `users` | `fk_users_role` | `role_id` | `roles(role_id)` | ON UPDATE CASCADE ON DELETE RESTRICT |
| 3 | `users` | `fk_users_primary_campus` | `primary_campus_id` | `campuses(campus_id)` | ON UPDATE CASCADE ON DELETE RESTRICT |
| 4 | `users` | `fk_users_department` | `department_id` | `departments(department_id)` | ON UPDATE CASCADE ON DELETE RESTRICT |
| 5 | `user_auth_providers` | `fk_auth_providers_user` | `user_id` | `users(user_id)` | ON UPDATE CASCADE ON DELETE CASCADE |
| 6 | `user_sessions` | `fk_sessions_user` | `user_id` | `users(user_id)` | ON UPDATE CASCADE ON DELETE CASCADE |
| 7 | `user_sessions` | `fk_sessions_selected_campus` | `selected_campus_id` | `campuses(campus_id)` | ON UPDATE CASCADE ON DELETE RESTRICT |
| 8 | `user_sessions` | `fk_sessions_auth_provider` | `auth_provider_id` | `user_auth_providers(auth_provider_id)` | ON UPDATE CASCADE ON DELETE SET NULL |
| 9 | `user_sessions` | `fk_sessions_revoked_by` | `revoked_by` | `users(user_id)` | ON UPDATE CASCADE ON DELETE SET NULL |
| 10 | `otp_tokens` | `fk_otp_tokens_user` | `user_id` | `users(user_id)` | ON UPDATE CASCADE ON DELETE CASCADE |
| 11 | `login_logs` | `fk_login_logs_user` | `user_id` | `users(user_id)` | ON UPDATE CASCADE ON DELETE SET NULL |
| 12 | `login_logs` | `fk_login_logs_campus` | `selected_campus_id` | `campuses(campus_id)` | ON UPDATE CASCADE ON DELETE SET NULL |
| 13 | `security_events` | `fk_security_events_user` | `user_id` | `users(user_id)` | ON UPDATE CASCADE ON DELETE SET NULL |
| 14 | `security_events` | `fk_security_events_selected_campus` | `selected_campus_id` | `campuses(campus_id)` | ON UPDATE CASCADE ON DELETE SET NULL |
| 15 | `security_events` | `fk_security_events_session` | `session_id` | `user_sessions(session_id)` | ON UPDATE CASCADE ON DELETE SET NULL |
| 16 | `files` | `fk_files_uploaded_by` | `uploaded_by` | `users(user_id)` | ON UPDATE CASCADE ON DELETE SET NULL |
| 17 | `partners` | `fk_partners_logo_file` | `logo_file_id` | `files(file_id)` | ON UPDATE CASCADE ON DELETE SET NULL |
| 18 | `partners` | `fk_partners_cover_file` | `cover_file_id` | `files(file_id)` | ON UPDATE CASCADE ON DELETE SET NULL |
| 19 | `partners` | `fk_partners_reviewed_by` | `reviewed_by` | `users(user_id)` | ON UPDATE CASCADE ON DELETE SET NULL |
| 20 | `partner_contacts` | `fk_partner_contacts_partner` | `partner_id` | `partners(partner_id)` | ON UPDATE CASCADE ON DELETE RESTRICT |
| 21 | `partner_contacts` | `fk_partner_contacts_scanned_card` | `scanned_card_file_id` | `files(file_id)` | ON UPDATE CASCADE ON DELETE SET NULL |
| 22 | `documents` | `fk_documents_file` | `file_id` | `files(file_id)` | ON UPDATE CASCADE ON DELETE RESTRICT |
| 23 | `documents` | `fk_documents_campus` | `campus_id` | `campuses(campus_id)` | ON UPDATE CASCADE ON DELETE SET NULL |
| 24 | `documents` | `fk_documents_created_by` | `created_by` | `users(user_id)` | ON UPDATE CASCADE ON DELETE SET NULL |
| 25 | `visit_requests` | `fk_visit_requests_visitor` | `visitor_user_id` | `users(user_id)` | ON UPDATE CASCADE ON DELETE SET NULL |
| 26 | `visit_requests` | `fk_visit_requests_partner` | `partner_id` | `partners(partner_id)` | ON UPDATE CASCADE ON DELETE SET NULL |
| 27 | `visit_requests` | `fk_visit_requests_decided_by` | `decided_by` | `users(user_id)` | ON UPDATE CASCADE ON DELETE SET NULL |
| 28 | `visit_requests` | `fk_visit_requests_cancelled_by` | `cancelled_by` | `users(user_id)` | ON UPDATE CASCADE ON DELETE SET NULL |
| 29 | `visit_request_campuses` | `fk_visit_instances_request` | `visit_request_id` | `visit_requests(visit_request_id)` | ON UPDATE CASCADE ON DELETE RESTRICT |
| 30 | `visit_request_campuses` | `fk_visit_instances_campus` | `campus_id` | `campuses(campus_id)` | ON UPDATE CASCADE ON DELETE RESTRICT |
| 31 | `visit_request_campuses` | `fk_visit_instances_coordinator` | `coordinator_user_id` | `users(user_id)` | ON UPDATE CASCADE ON DELETE SET NULL |
| 32 | `visit_request_campuses` | `fk_visit_instances_coordinator_assigned_by` | `coordinator_assigned_by` | `users(user_id)` | ON UPDATE CASCADE ON DELETE SET NULL |
| 33 | `visit_request_campuses` | `fk_visit_instances_current_host` | `current_host_user_id` | `users(user_id)` | ON UPDATE CASCADE ON DELETE SET NULL |
| 34 | `visit_request_campuses` | `fk_visit_instances_host_assigned_by` | `host_assigned_by` | `users(user_id)` | ON UPDATE CASCADE ON DELETE SET NULL |
| 35 | `visit_request_campuses` | `fk_visit_instances_closed_by` | `closed_by` | `users(user_id)` | ON UPDATE CASCADE ON DELETE SET NULL |
| 36 | `visit_request_campuses` | `fk_visit_instances_cancelled_by` | `cancelled_by` | `users(user_id)` | ON UPDATE CASCADE ON DELETE SET NULL |
| 37 | `visit_guest_members` | `fk_guest_members_request` | `visit_request_id` | `visit_requests(visit_request_id)` | ON UPDATE CASCADE ON DELETE RESTRICT |
| 38 | `visit_participants` | `fk_visit_participants_instance` | `visit_instance_id` | `visit_request_campuses(visit_instance_id)` | ON UPDATE CASCADE ON DELETE RESTRICT |
| 39 | `visit_participants` | `fk_visit_participants_user` | `user_id` | `users(user_id)` | ON UPDATE CASCADE ON DELETE RESTRICT |
| 40 | `visit_participants` | `fk_visit_participants_invited_by` | `invited_by` | `users(user_id)` | ON UPDATE CASCADE ON DELETE SET NULL |
| 41 | `visit_participants` | `fk_visit_participants_assigned_by` | `assigned_by` | `users(user_id)` | ON UPDATE CASCADE ON DELETE SET NULL |
| 42 | `visit_agendas` | `fk_visit_agendas_instance` | `visit_instance_id` | `visit_request_campuses(visit_instance_id)` | ON UPDATE CASCADE ON DELETE RESTRICT |
| 43 | `visit_agendas` | `fk_visit_agendas_responsible_user` | `responsible_user_id` | `users(user_id)` | ON UPDATE CASCADE ON DELETE SET NULL |
| 44 | `visit_logistics_items` | `fk_logistics_instance` | `visit_instance_id` | `visit_request_campuses(visit_instance_id)` | ON UPDATE CASCADE ON DELETE RESTRICT |
| 45 | `visit_logistics_items` | `fk_logistics_requested_by` | `requested_by` | `users(user_id)` | ON UPDATE CASCADE ON DELETE SET NULL |
| 46 | `visit_logistics_items` | `fk_logistics_requested_to_department` | `requested_to_department_id` | `departments(department_id)` | ON UPDATE CASCADE ON DELETE SET NULL |
| 47 | `visit_logistics_items` | `fk_logistics_received_by` | `received_by` | `users(user_id)` | ON UPDATE CASCADE ON DELETE SET NULL |
| 48 | `visit_logistics_items` | `fk_logistics_assigned_to` | `assigned_to_user_id` | `users(user_id)` | ON UPDATE CASCADE ON DELETE SET NULL |
| 49 | `visit_logistics_items` | `fk_logistics_assigned_by` | `assigned_by` | `users(user_id)` | ON UPDATE CASCADE ON DELETE SET NULL |
| 50 | `visit_logistics_items` | `fk_logistics_proposed_by` | `proposed_by` | `users(user_id)` | ON UPDATE CASCADE ON DELETE SET NULL |
| 51 | `visit_logistics_items` | `fk_logistics_proposal_responded_by` | `proposal_responded_by` | `users(user_id)` | ON UPDATE CASCADE ON DELETE SET NULL |
| 52 | `visit_logistics_items` | `fk_logistics_handover_by` | `handover_confirmed_by` | `users(user_id)` | ON UPDATE CASCADE ON DELETE SET NULL |
| 53 | `visit_logistics_items` | `fk_logistics_service_report_signed_by` | `service_report_signed_by` | `users(user_id)` | ON UPDATE CASCADE ON DELETE SET NULL |
| 54 | `visit_logistics_items` | `fk_logistics_service_report_file` | `service_report_file_id` | `files(file_id)` | ON UPDATE CASCADE ON DELETE SET NULL |
| 55 | `minutes` | `fk_minutes_visit_instance` | `visit_instance_id` | `visit_request_campuses(visit_instance_id)` | ON UPDATE CASCADE ON DELETE RESTRICT |
| 56 | `minutes` | `fk_minutes_created_by` | `created_by` | `users(user_id)` | ON UPDATE CASCADE ON DELETE SET NULL |
| 57 | `minutes` | `fk_minutes_updated_by` | `updated_by` | `users(user_id)` | ON UPDATE CASCADE ON DELETE SET NULL |
| 58 | `minutes` | `fk_minutes_edit_locked_by` | `edit_locked_by` | `users(user_id)` | ON UPDATE CASCADE ON DELETE SET NULL |
| 59 | `minute_participants` | `fk_minute_participants_minutes` | `minutes_id` | `minutes(minutes_id)` | ON UPDATE CASCADE ON DELETE CASCADE |
| 60 | `minute_participants` | `fk_minute_participants_user` | `user_id` | `users(user_id)` | ON UPDATE CASCADE ON DELETE SET NULL |
| 61 | `minute_participants` | `fk_minute_participants_guest_member` | `guest_member_id` | `visit_guest_members(guest_member_id)` | ON UPDATE CASCADE ON DELETE SET NULL |
| 62 | `minute_participants` | `fk_minute_participants_checked_by` | `checked_by` | `users(user_id)` | ON UPDATE CASCADE ON DELETE SET NULL |
| 63 | `minute_action_items` | `fk_action_items_minutes` | `minutes_id` | `minutes(minutes_id)` | ON UPDATE CASCADE ON DELETE CASCADE |
| 64 | `minute_action_items` | `fk_action_items_created_by` | `created_by` | `users(user_id)` | ON UPDATE CASCADE ON DELETE SET NULL |
| 65 | `minute_action_items` | `fk_action_items_updated_by` | `updated_by` | `users(user_id)` | ON UPDATE CASCADE ON DELETE SET NULL |
| 66 | `feedbacks` | `fk_feedbacks_visit_request` | `visit_request_id` | `visit_requests(visit_request_id)` | ON UPDATE CASCADE ON DELETE RESTRICT |
| 67 | `feedbacks` | `fk_feedbacks_visit_instance` | `visit_instance_id` | `visit_request_campuses(visit_instance_id)` | ON UPDATE CASCADE ON DELETE SET NULL |
| 68 | `feedbacks` | `fk_feedbacks_submitter` | `submitted_by_user_id` | `users(user_id)` | ON UPDATE CASCADE ON DELETE RESTRICT |
| 69 | `feedbacks` | `fk_feedbacks_target` | `target_user_id` | `users(user_id)` | ON UPDATE CASCADE ON DELETE RESTRICT |
| 70 | `feedback_rating_items` | `fk_feedback_rating_items_feedback` | `feedback_id` | `feedbacks(feedback_id)` | ON UPDATE CASCADE ON DELETE CASCADE |
| 71 | `news` | `fk_news_campus` | `campus_id` | `campuses(campus_id)` | ON UPDATE CASCADE ON DELETE SET NULL |
| 72 | `news` | `fk_news_visit_instance` | `visit_instance_id` | `visit_request_campuses(visit_instance_id)` | ON UPDATE CASCADE ON DELETE SET NULL |
| 73 | `news` | `fk_news_author` | `author_user_id` | `users(user_id)` | ON UPDATE CASCADE ON DELETE RESTRICT |
| 74 | `news` | `fk_news_cover_file` | `cover_file_id` | `files(file_id)` | ON UPDATE CASCADE ON DELETE SET NULL |
| 75 | `news` | `fk_news_reviewed_by` | `reviewed_by` | `users(user_id)` | ON UPDATE CASCADE ON DELETE SET NULL |
| 76 | `news_translations` | `fk_news_translations_news` | `news_id` | `news(news_id)` | ON UPDATE CASCADE ON DELETE CASCADE |
| 77 | `news_content_sections` | `fk_news_sections_translation` | `news_translation_id` | `news_translations(news_translation_id)` | ON UPDATE CASCADE ON DELETE CASCADE |
| 78 | `news_section_files` | `fk_news_section_files_section` | `section_id` | `news_content_sections(section_id)` | ON UPDATE CASCADE ON DELETE CASCADE |
| 79 | `news_section_files` | `fk_news_section_files_file` | `file_id` | `files(file_id)` | ON UPDATE CASCADE ON DELETE RESTRICT |
| 80 | `galleries` | `fk_galleries_campus` | `campus_id` | `campuses(campus_id)` | ON UPDATE CASCADE ON DELETE RESTRICT |
| 81 | `galleries` | `fk_galleries_hero_file` | `hero_file_id` | `files(file_id)` | ON UPDATE CASCADE ON DELETE SET NULL |
| 82 | `gallery_images` | `fk_gallery_images_gallery` | `gallery_id` | `galleries(gallery_id)` | ON UPDATE CASCADE ON DELETE RESTRICT |
| 83 | `gallery_images` | `fk_gallery_images_file` | `file_id` | `files(file_id)` | ON UPDATE CASCADE ON DELETE RESTRICT |
| 84 | `gallery_images` | `fk_gallery_images_thumbnail_file` | `thumbnail_file_id` | `files(file_id)` | ON UPDATE CASCADE ON DELETE SET NULL |
| 85 | `photo_face_tags` | `fk_face_tags_image` | `image_id` | `gallery_images(image_id)` | ON UPDATE CASCADE ON DELETE CASCADE |
| 86 | `photo_face_tags` | `fk_face_tags_visit_request` | `visit_request_id` | `visit_requests(visit_request_id)` | ON UPDATE CASCADE ON DELETE SET NULL |
| 87 | `photo_face_tags` | `fk_face_tags_guest` | `guest_member_id` | `visit_guest_members(guest_member_id)` | ON UPDATE CASCADE ON DELETE SET NULL |
| 88 | `photo_face_tags` | `fk_face_tags_partner_contact` | `partner_contact_id` | `partner_contacts(contact_id)` | ON UPDATE CASCADE ON DELETE SET NULL |
| 89 | `photo_face_tags` | `fk_face_tags_confirmed_by` | `confirmed_by` | `users(user_id)` | ON UPDATE CASCADE ON DELETE SET NULL |
| 90 | `email_templates` | `fk_email_templates_campus` | `campus_id` | `campuses(campus_id)` | ON UPDATE CASCADE ON DELETE SET NULL |
| 91 | `sent_emails` | `fk_sent_emails_template` | `email_template_id` | `email_templates(email_template_id)` | ON UPDATE CASCADE ON DELETE SET NULL |
| 92 | `sent_emails` | `fk_sent_emails_sent_by` | `sent_by` | `users(user_id)` | ON UPDATE CASCADE ON DELETE SET NULL |
| 93 | `sent_email_recipients` | `fk_sent_email_recipients_email` | `sent_email_id` | `sent_emails(sent_email_id)` | ON UPDATE CASCADE ON DELETE CASCADE |
| 94 | `notifications` | `fk_notifications_user` | `recipient_user_id` | `users(user_id)` | ON UPDATE CASCADE ON DELETE CASCADE |
| 95 | `calendar_events` | `fk_calendar_owner` | `owner_user_id` | `users(user_id)` | ON UPDATE CASCADE ON DELETE CASCADE |
| 96 | `calendar_events` | `fk_calendar_campus` | `campus_id` | `campuses(campus_id)` | ON UPDATE CASCADE ON DELETE SET NULL |
| 97 | `calendar_events` | `fk_calendar_visit` | `visit_instance_id` | `visit_request_campuses(visit_instance_id)` | ON UPDATE CASCADE ON DELETE SET NULL |
| 98 | `calendar_events` | `fk_calendar_logistics` | `logistics_item_id` | `visit_logistics_items(logistics_item_id)` | ON UPDATE CASCADE ON DELETE SET NULL |
| 99 | `calendar_event_attendees` | `fk_calendar_attendees_event` | `calendar_event_id` | `calendar_events(calendar_event_id)` | ON UPDATE CASCADE ON DELETE CASCADE |
| 100 | `calendar_event_attendees` | `fk_calendar_attendees_user` | `user_id` | `users(user_id)` | ON UPDATE CASCADE ON DELETE SET NULL |
| 101 | `calendar_event_reminders` | `fk_calendar_reminders_event` | `calendar_event_id` | `calendar_events(calendar_event_id)` | ON UPDATE CASCADE ON DELETE CASCADE |
| 102 | `api_configuration_headers` | `fk_api_headers_config` | `api_config_id` | `api_configurations(api_config_id)` | ON UPDATE CASCADE ON DELETE CASCADE |
| 103 | `api_usage_quotas` | `fk_api_quota_config` | `api_config_id` | `api_configurations(api_config_id)` | ON UPDATE CASCADE ON DELETE CASCADE |
| 104 | `api_usage_quotas` | `fk_api_quota_campus` | `campus_id` | `campuses(campus_id)` | ON UPDATE CASCADE ON DELETE CASCADE |
| 105 | `api_request_logs` | `fk_api_logs_config` | `api_config_id` | `api_configurations(api_config_id)` | ON UPDATE CASCADE ON DELETE RESTRICT |
| 106 | `api_request_logs` | `fk_api_logs_campus` | `campus_id` | `campuses(campus_id)` | ON UPDATE CASCADE ON DELETE SET NULL |
| 107 | `api_request_logs` | `fk_api_logs_user` | `requested_by` | `users(user_id)` | ON UPDATE CASCADE ON DELETE SET NULL |
| 108 | `agenda_templates` | `fk_agenda_templates_campus` | `campus_id` | `campuses(campus_id)` | ON UPDATE CASCADE ON DELETE SET NULL |
| 109 | `agenda_template_items` | `fk_agenda_template_items_template` | `agenda_template_id` | `agenda_templates(agenda_template_id)` | ON UPDATE CASCADE ON DELETE CASCADE |
| 110 | `audit_logs` | `fk_audit_actor` | `actor_user_id` | `users(user_id)` | ON UPDATE CASCADE ON DELETE SET NULL |
| 111 | `audit_logs` | `fk_audit_campus` | `campus_id` | `campuses(campus_id)` | ON UPDATE CASCADE ON DELETE SET NULL |
| 112 | `audit_log_changes` | `fk_audit_changes_log` | `audit_log_id` | `audit_logs(audit_log_id)` | ON UPDATE CASCADE ON DELETE CASCADE |

## 9. Removed / Legacy Verification

| Check | Result |
|---|---|
| permissions table | Removed / Not present |
| role_permissions table | Removed / Not present |
| roles.deleted_at | Removed / Not present |
| roles.deleted_by | Removed / Not present |
| visit_status_logs table | Removed / Not present |
| users.gender UNKNOWN | Removed / Not present |
| visit_requests.decision_actor_role SYSTEM | Removed / Not present |
| galleries.status DRAFT | Removed / Not present |

## 10. Replacement Checklist
1. Replace the old `DATABASE_SCHEMA_v8_4_refined_v6.md` with this file if the project has switched to `pems_full_seed_logic_v8_4_refined_v6_no_dynamic_permissions.sql`.
2. Do not regenerate `permissions` / `role_permissions` in SQL, backend entities, EF Core DbSet, frontend types, or seed files.
3. Keep `roles` as fixed role classification only; map effectiveRole in backend/frontend from `role_code` + `sub_role`.
4. Re-run backend/frontend build after updating schema references.
5. Re-test login and direct API authorization by role and data scope.
