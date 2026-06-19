# PEMS Database Schema — FULL v8.2 Cancel Delegation
> **Generated from:** `pems_full_sql_42tables_final_v8_2_cancel_delegation_full_create.sql`  
> **Purpose:** Full developer-facing database schema reference. This file is generated as a full replacement, not a shortened summary.  
> **Preservation note:** This version restores full table-level details and adds UC-136 cancellation fields without `external_confirmation_note`.

> **SQL-match correction:** This version was rechecked against the uploaded v8.2 SQL. It removes `INTERNAL_DECISION` from `cancellation_source`, keeps no `external_confirmation_note`, adds missing `registrant_full_name` and `delegation_name`, and moves FULLTEXT definitions from the column table into index lists.

## 1. Overview
| Item | Value |
|---|---|
| Database | `pems_db` |
| Engine | MySQL 8.0 / InnoDB |
| Charset / Collation | `utf8mb4` / `utf8mb4_unicode_ci` |
| Schema Version | `PEMS v8.2 cancel-delegation full create` |
| Base Table Count | `42` |
| Primary Key Strategy | `BIGINT UNSIGNED AUTO_INCREMENT` for base-table PKs |
| Visit Request Status | `PENDING_APPROVAL`, `APPROVED`, `REJECTED`, `CANCELLED` only |
| Campus Visit Status | `WAITING_REQUEST_APPROVAL`, `ASSIGNED`, `BEFORE_VISIT`, `DURING_VISIT`, `AFTER_VISIT`, `CLOSED`, `CANCELLED` |
| Cancellation UC | `UC-136.CANCEL_VISIT_REQUEST`, belongs to Delegation Reception Management |
| External Confirmation | No `external_confirmation_note`; use `cancellation_reason` |

## 2. V8.2 Cancellation Rules

## V8.2 Addendum — UC-136 Cancel Visit Request thuộc Delegation Reception Management

> Phần này là nội dung bổ sung, không xóa nội dung gốc. Nếu nội dung gốc có flow cũ như “đã duyệt nhưng chưa có host” hoặc “mỗi cơ sở duyệt lại sau HO”, hãy ưu tiên rule V8.2 trong phần addendum này.

### 1. Feature ownership

UC hủy đơn thăm thuộc **FE-02 — Quản lý Tiếp đón Đoàn khách / Delegation Reception Management** vì đây là thao tác trên vòng đời đoàn/visit request, không phải bước submit form.

```text
Feature: FE-02 Delegation Reception Management
UC: UC-136 Cancel Visit Request
Permission code: UC-136.CANCEL_VISIT_REQUEST
```

### 2. Không dùng `external_confirmation_note`

Không tạo cột `external_confirmation_note`. Khi Host hủy thay khách dựa trên xác nhận ngoài hệ thống, toàn bộ thông tin xác nhận được ghi vào `cancellation_reason`.

```text
cancellation_source = EXTERNAL_CONFIRMATION
cancellation_reason = "Khách xác nhận hủy qua email/điện thoại/Zalo..., thời gian..., người xác nhận..., lý do..."
```

### 3. Cancellation metadata chuẩn

Áp dụng cho `visit_requests` và `visit_request_campuses`:

```sql
cancelled_by BIGINT UNSIGNED NULL,
cancelled_at DATETIME NULL,
cancellation_actor_type ENUM('VISITOR','HOST','STAFF_LEADER','HO','SYSTEM') NULL,
cancellation_source ENUM('SELF_SERVICE','EXTERNAL_CONFIRMATION') NULL,
cancellation_reason TEXT NULL
```

### 4. Meaning của `cancellation_source`

| Value | Meaning | Khi dùng |
|---|---|---|
| `SELF_SERVICE` | Người dùng tự thao tác trên hệ thống | Visitor tự hủy đơn của chính họ |
| `EXTERNAL_CONFIRMATION` | Hủy dựa trên xác nhận ngoài hệ thống | Host hủy thay khách sau khi khách xác nhận qua email/điện thoại/Zalo/gặp trực tiếp |

### 5. Rule hủy theo role

| Actor | Scope | Nguồn hủy hợp lệ | Ghi chú |
|---|---|---|---|
| Visitor | Đơn của chính họ | `SELF_SERVICE` | Chỉ hủy khi chưa vào giai đoạn `DURING_VISIT`, `AFTER_VISIT`, `CLOSED` |
| Host | Campus instance mình đang phụ trách | `EXTERNAL_CONFIRMATION` | Bắt buộc nhập `cancellation_reason` rõ kênh/thời điểm/người xác nhận |
| Staff Leader | Đơn/campus thuộc campus mình | `EXTERNAL_CONFIRMATION` | Không xử lý campus khác |
| HO | `MULTI_CAMPUS` | `EXTERNAL_CONFIRMATION` | Có thể hủy request tổng liên cơ sở nếu nghiệp vụ cho phép |
| Admin | Không có quyền nghiệp vụ visit/delegation | Không áp dụng | ADMIN không được hủy delegation |

### 6. Rule trạng thái

- `visit_requests.status = CANCELLED` dùng khi hủy request/delegation tổng.
- `visit_request_campuses.status = CANCELLED` dùng khi hủy một campus instance.
- Không cho hủy campus instance nếu đã vào `DURING_VISIT`, `AFTER_VISIT`, hoặc `CLOSED`.
- Không dùng `CANCELLED` thay cho `REJECTED`. Nếu đơn đang `PENDING_APPROVAL` và người duyệt không chấp nhận, dùng reject flow.

### 7. Vị trí code Clean Architecture

```text
PEMS.Application/Delegations/Commands/CancelVisitRequest/
├── CancelVisitRequestCommand.cs
├── CancelVisitRequestCommandHandler.cs
├── CancelVisitRequestCommandValidator.cs
└── CancelVisitRequestResponse.cs
```

Controller chỉ nhận request và gọi `IMediator`. Logic kiểm tra scope, current host, request/campus status, và cancellation metadata nằm trong Handler/Domain Entity.


## 3. Table List
| # | Table |
|---:|---|
| 1 | `roles` |
| 2 | `permissions` |
| 3 | `role_permissions` |
| 4 | `campuses` |
| 5 | `departments` |
| 6 | `users` |
| 7 | `user_auth_providers` |
| 8 | `user_sessions` |
| 9 | `otp_tokens` |
| 10 | `login_logs` |
| 11 | `security_events` |
| 12 | `partners` |
| 13 | `partner_contacts` |
| 14 | `files` |
| 15 | `documents` |
| 16 | `visit_requests` |
| 17 | `visit_request_campuses` |
| 18 | `visit_guest_members` |
| 19 | `visit_participants` |
| 20 | `visit_agendas` |
| 21 | `visit_logistics_items` |
| 22 | `minutes` |
| 23 | `minute_action_items` |
| 24 | `feedbacks` |
| 25 | `news` |
| 26 | `news_translations` |
| 27 | `news_content_sections` |
| 28 | `news_section_files` |
| 29 | `faqs` |
| 30 | `galleries` |
| 31 | `gallery_images` |
| 32 | `photo_face_tags` |
| 33 | `email_templates` |
| 34 | `sent_emails` |
| 35 | `notifications` |
| 36 | `calendar_events` |
| 37 | `api_configurations` |
| 38 | `api_usage_quotas` |
| 39 | `api_request_logs` |
| 40 | `agenda_templates` |
| 41 | `audit_logs` |
| 42 | `visit_status_logs` |

## 4. Table Details
### 4.1. `roles`

**Columns:**

| Column | Type | Null | Default | Notes |
|---|---|---:|---|---|
| `role_id` | `BIGINT UNSIGNED` | NO | `` |  |
| `role_code` | `VARCHAR(30)` | NO | `` | ADMIN, HO, STAFF, DEPT, STUDENT, VISITOR |
| `name` | `VARCHAR(100)` | NO | `` |  |
| `description` | `VARCHAR(255)` | YES | `` |  |
| `status` | `ENUM('ACTIVE','INACTIVE')` | NO | `'ACTIVE'` |  |
| `created_at` | `DATETIME` | NO | `CURRENT_TIMESTAMP` |  |
| `deleted_at` | `DATETIME` | YES | `` | Soft delete supported by UC-121 Disable/Delete Role |
| `deleted_by` | `BIGINT UNSIGNED` | YES | `` | User who soft-deleted this role; no FK here because roles is created before users |

**Primary Key:**
- `PRIMARY KEY (role_id)`

**Unique Constraints:**
- `UNIQUE KEY uq_roles_code (role_code)`

**Indexes:**
- `KEY idx_roles_status_deleted (status, deleted_at)`

**Check Constraints:**
- `CHECK (role_code IN ('ADMIN','HO','STAFF','DEPT','STUDENT','VISITOR'))`

### 4.2. `permissions`

**Columns:**

| Column | Type | Null | Default | Notes |
|---|---|---:|---|---|
| `permission_id` | `BIGINT UNSIGNED` | NO | `` |  |
| `permission_code` | `VARCHAR(100)` | NO | `` | Example: UC-17.SUBMIT_VISIT_REQUEST |
| `name` | `VARCHAR(150)` | NO | `` |  |
| `permission_group` | `VARCHAR(60)` | NO | `` |  |
| `description` | `VARCHAR(500)` | YES | `` |  |
| `is_system` | `BOOLEAN` | NO | `FALSE` |  |
| `created_at` | `DATETIME` | NO | `CURRENT_TIMESTAMP` |  |

**Primary Key:**
- `PRIMARY KEY (permission_id)`

**Unique Constraints:**
- `UNIQUE KEY uq_permissions_code (permission_code)`

**Indexes:**
- `KEY idx_permissions_group (permission_group)`
- `KEY idx_permissions_group_code (permission_group, permission_code)`

### 4.3. `role_permissions`

**Columns:**

| Column | Type | Null | Default | Notes |
|---|---|---:|---|---|
| `role_permission_id` | `BIGINT UNSIGNED` | NO | `` |  |
| `role_id` | `BIGINT UNSIGNED` | NO | `` |  |
| `sub_role` | `ENUM('NONE','Leader','Staff')` | NO | `'NONE'` | NONE for ADMIN/HO/STUDENT/VISITOR; Leader/Staff for STAFF and DEPT |
| `permission_id` | `BIGINT UNSIGNED` | NO | `` |  |
| `permission_level` | `ENUM('F','E','R','O')` | NO | `` | F=Full, E=Execute/Edit, R=Read, O=Own |
| `granted_at` | `DATETIME` | NO | `CURRENT_TIMESTAMP` |  |
| `granted_by` | `BIGINT UNSIGNED` | YES | `` |  |

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

**Columns:**

| Column | Type | Null | Default | Notes |
|---|---|---:|---|---|
| `campus_id` | `BIGINT UNSIGNED` | NO | `` |  |
| `campus_code` | `VARCHAR(20)` | NO | `` | HN, HCM, DN, CT, QN |
| `name` | `VARCHAR(150)` | NO | `` |  |
| `city` | `VARCHAR(100)` | YES | `` |  |
| `address` | `VARCHAR(255)` | YES | `` |  |
| `phone` | `VARCHAR(30)` | YES | `` |  |
| `email` | `VARCHAR(150)` | YES | `` |  |
| `ic_head_user_id` | `BIGINT UNSIGNED` | YES | `` | FK added after users table |
| `status` | `ENUM('ACTIVE','INACTIVE')` | NO | `'ACTIVE'` |  |
| `created_at` | `DATETIME` | NO | `CURRENT_TIMESTAMP` |  |
| `created_by` | `BIGINT UNSIGNED` | YES | `` |  |
| `updated_at` | `DATETIME` | YES | `NULL ON UPDATE CURRENT_TIMESTAMP` |  |
| `updated_by` | `BIGINT UNSIGNED` | YES | `` |  |

**Primary Key:**
- `PRIMARY KEY (campus_id)`

**Unique Constraints:**
- `UNIQUE KEY uq_campuses_code (campus_code)`

**Indexes:**
- `KEY idx_campuses_status (status)`
- `KEY idx_campuses_city_status (city, status)`
- `KEY idx_campuses_ic_head (ic_head_user_id)`

### 4.5. `departments`

**Columns:**

| Column | Type | Null | Default | Notes |
|---|---|---:|---|---|
| `department_id` | `BIGINT UNSIGNED` | NO | `` |  |
| `campus_id` | `BIGINT UNSIGNED` | NO | `` |  |
| `department_code` | `VARCHAR(50)` | NO | `` |  |
| `name` | `VARCHAR(150)` | NO | `` |  |
| `department_type` | `ENUM('IC','GENERAL')` | NO | `` | IC=International Cooperation; GENERAL=other departments |
| `head_user_id` | `BIGINT UNSIGNED` | YES | `` | FK added after users table |
| `status` | `ENUM('ACTIVE','INACTIVE')` | NO | `'ACTIVE'` |  |
| `created_at` | `DATETIME` | NO | `CURRENT_TIMESTAMP` |  |
| `created_by` | `BIGINT UNSIGNED` | YES | `` |  |
| `updated_at` | `DATETIME` | YES | `NULL ON UPDATE CURRENT_TIMESTAMP` |  |
| `updated_by` | `BIGINT UNSIGNED` | YES | `` |  |

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

**Columns:**

| Column | Type | Null | Default | Notes |
|---|---|---:|---|---|
| `user_id` | `BIGINT UNSIGNED` | NO | `` |  |
| `full_name` | `VARCHAR(150)` | NO | `` |  |
| `email` | `VARCHAR(150)` | NO | `` |  |
| `phone` | `VARCHAR(30)` | YES | `` |  |
| `nationality` | `VARCHAR(100)` | YES | `` | Quốc tịch của user/visitor |
| `password_hash` | `VARCHAR(255)` | YES | `` | DEV/local password hash only. Production SSO-only accounts keep this NULL. |
| `role_id` | `BIGINT UNSIGNED` | NO | `` |  |
| `sub_role` | `ENUM('Leader','Staff')` | YES | `` | Only for STAFF/DEPT |
| `primary_campus_id` | `BIGINT UNSIGNED` | YES | `` | Campus duy nhất của user nội bộ. VISITOR phải NULL. |
| `department_id` | `BIGINT UNSIGNED` | YES | `` | STAFF = IC department; DEPT = GENERAL department |
| `gender` | `ENUM('MALE','FEMALE','OTHER','UNKNOWN')` | YES | `` |  |
| `avatar_url` | `VARCHAR(500)` | YES | `` |  |
| `student_code` | `VARCHAR(30)` | YES | `` |  |
| `fe_id` | `VARCHAR(100)` | YES | `` |  |
| `status` | `ENUM('ACTIVE','INACTIVE','LOCKED')` | NO | `'ACTIVE'` | ACTIVE=hoạt động, INACTIVE=tạm ngưng, LOCKED=bị khóa |
| `email_verified_at` | `DATETIME` | YES | `` | Thời điểm email được xác thực qua SSO lần đầu hoặc xác nhận bởi hệ thống |
| `failed_login_count` | `INT UNSIGNED` | NO | `0` | Số lần đăng nhập sai local password liên tiếp; reset khi login thành công |
| `locked_until` | `DATETIME` | YES | `` | Thời điểm hết khóa tạm thời nếu bị lock |
| `created_via` | `ENUM('MANUAL_CREATED','VISITOR_FORM','SSO_AUTO_PROVISION')` | NO | `'MANUAL_CREATED'` | MANUAL_CREATED=HO/Staff Leader tạo, VISITOR_FORM=tạo từ form visitor, SSO_AUTO_PROVISION=tạo tự động khi đăng nhập SSO ở cổng Visitor |
| `first_login_at` | `DATETIME` | YES | `` |  |
| `last_login_at` | `DATETIME` | YES | `` |  |
| `created_at` | `DATETIME` | NO | `CURRENT_TIMESTAMP` |  |
| `created_by` | `BIGINT UNSIGNED` | YES | `` |  |
| `updated_at` | `DATETIME` | YES | `NULL ON UPDATE CURRENT_TIMESTAMP` |  |
| `updated_by` | `BIGINT UNSIGNED` | YES | `` |  |

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

**Columns:**

| Column | Type | Null | Default | Notes |
|---|---|---:|---|---|
| `auth_provider_id` | `BIGINT UNSIGNED` | NO | `` |  |
| `user_id` | `BIGINT UNSIGNED` | NO | `` |  |
| `provider_type` | `ENUM('LOCAL_PASSWORD','GOOGLE_SSO','FEID')` | NO | `` |  |
| `provider_subject` | `VARCHAR(255)` | YES | `` | Required for GOOGLE_SSO/FEID |
| `provider_email` | `VARCHAR(150)` | YES | `` |  |
| `is_enabled` | `BOOLEAN` | NO | `TRUE` |  |
| `linked_at` | `DATETIME` | NO | `CURRENT_TIMESTAMP` |  |
| `last_used_at` | `DATETIME` | YES | `` |  |

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

**Columns:**

| Column | Type | Null | Default | Notes |
|---|---|---:|---|---|
| `session_id` | `BIGINT UNSIGNED` | NO | `` |  |
| `user_id` | `BIGINT UNSIGNED` | NO | `` |  |
| `login_portal` | `ENUM('VISITOR','INTERNAL')` | NO | `` |  |
| `selected_campus_id` | `BIGINT UNSIGNED` | YES | `` | Auto set to users.primary_campus_id for INTERNAL, NULL for VISITOR |
| `auth_provider_id` | `BIGINT UNSIGNED` | YES | `` |  |
| `refresh_token_hash` | `VARCHAR(255)` | YES | `` | Refresh token hash merged into session |
| `refresh_expires_at` | `DATETIME` | YES | `` |  |
| `refresh_revoked_at` | `DATETIME` | YES | `` |  |
| `ip_address` | `VARCHAR(45)` | YES | `` |  |
| `user_agent` | `VARCHAR(500)` | YES | `` |  |
| `created_at` | `DATETIME` | NO | `CURRENT_TIMESTAMP` |  |
| `expires_at` | `DATETIME` | NO | `` |  |
| `revoked_at` | `DATETIME` | YES | `` |  |
| `revoked_by` | `BIGINT UNSIGNED` | YES | `` |  |
| `revoked_reason` | `VARCHAR(255)` | YES | `` |  |

**Primary Key:**
- `PRIMARY KEY (session_id)`

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

### 4.9. `otp_tokens`

**Columns:**

| Column | Type | Null | Default | Notes |
|---|---|---:|---|---|
| `otp_token_id` | `BIGINT UNSIGNED` | NO | `` |  |
| `user_id` | `BIGINT UNSIGNED` | YES | `` |  |
| `email` | `VARCHAR(150)` | NO | `` |  |
| `token_type` | `ENUM('OTP_CODE','MAGIC_LINK')` | NO | `'OTP_CODE'` |  |
| `purpose` | `ENUM('VISIT_REQUEST_VERIFY','CHANGE_SENSITIVE_ACTION')` | NO | `` |  |
| `token_hash` | `VARCHAR(255)` | NO | `` |  |
| `expires_at` | `DATETIME` | NO | `` |  |
| `used_at` | `DATETIME` | YES | `` |  |
| `attempt_count` | `INT UNSIGNED` | NO | `0` |  |
| `max_attempts` | `INT UNSIGNED` | NO | `5` |  |
| `resend_count` | `INT UNSIGNED` | NO | `0` |  |
| `ip_address` | `VARCHAR(45)` | YES | `` |  |
| `user_agent` | `VARCHAR(500)` | YES | `` |  |
| `created_at` | `DATETIME` | NO | `CURRENT_TIMESTAMP` |  |

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

**Columns:**

| Column | Type | Null | Default | Notes |
|---|---|---:|---|---|
| `login_log_id` | `BIGINT UNSIGNED` | NO | `` |  |
| `user_id` | `BIGINT UNSIGNED` | YES | `` |  |
| `email` | `VARCHAR(150)` | NO | `` |  |
| `login_portal` | `ENUM('VISITOR','INTERNAL')` | NO | `` |  |
| `selected_campus_id` | `BIGINT UNSIGNED` | YES | `` |  |
| `provider_type` | `ENUM('LOCAL_PASSWORD','GOOGLE_SSO','FEID')` | YES | `` |  |
| `status` | `ENUM('SUCCESS','FAILED','BLOCKED')` | NO | `` |  |
| `failure_reason` | `VARCHAR(255)` | YES | `` |  |
| `ip_address` | `VARCHAR(45)` | YES | `` |  |
| `user_agent` | `VARCHAR(500)` | YES | `` |  |
| `session_id` | `BIGINT UNSIGNED` | YES | `` |  |
| `created_at` | `DATETIME` | NO | `CURRENT_TIMESTAMP` |  |

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

**Columns:**

| Column | Type | Null | Default | Notes |
|---|---|---:|---|---|
| `security_event_id` | `BIGINT UNSIGNED` | NO | `` |  |
| `user_id` | `BIGINT UNSIGNED` | YES | `` |  |
| `email` | `VARCHAR(150)` | YES | `` |  |
| `event_type` | `VARCHAR(80)` | NO | `` | LOGIN_LOCKED, OTP_FAILED, SUSPICIOUS_IP... |
| `severity` | `ENUM('LOW','MEDIUM','HIGH','CRITICAL')` | NO | `'LOW'` |  |
| `ip_address` | `VARCHAR(45)` | YES | `` |  |
| `user_agent` | `VARCHAR(500)` | YES | `` |  |
| `metadata` | `JSON` | YES | `` |  |
| `created_at` | `DATETIME` | NO | `CURRENT_TIMESTAMP` |  |

**Primary Key:**
- `PRIMARY KEY (security_event_id)`

**Indexes:**
- `KEY idx_security_user_time (user_id, created_at)`
- `KEY idx_security_email_time (email, created_at)`
- `KEY idx_security_type_time (event_type, created_at)`
- `KEY idx_security_ip_time (ip_address, created_at)`
- `KEY idx_security_severity_time (severity, created_at)`

**Foreign Keys:**
- `CONSTRAINT fk_security_events_user FOREIGN KEY (user_id) REFERENCES users(user_id) ON UPDATE CASCADE ON DELETE SET NULL`

### 4.12. `partners`

**Columns:**

| Column | Type | Null | Default | Notes |
|---|---|---:|---|---|
| `partner_id` | `BIGINT UNSIGNED` | NO | `` |  |
| `partner_code` | `VARCHAR(50)` | YES | `` |  |
| `name` | `VARCHAR(200)` | NO | `` |  |
| `short_name` | `VARCHAR(100)` | YES | `` |  |
| `country` | `VARCHAR(100)` | YES | `` |  |
| `city` | `VARCHAR(100)` | YES | `` |  |
| `website_url` | `VARCHAR(500)` | YES | `` |  |
| `partner_type` | `ENUM('UNIVERSITY','COMPANY','GOVERNMENT','NGO','OTHER')` | NO | `'UNIVERSITY'` |  |
| `cooperation_status` | `ENUM('POTENTIAL','ACTIVE','INACTIVE','BLACKLISTED')` | NO | `'POTENTIAL'` |  |
| `description` | `TEXT` | YES | `` |  |
| `created_at` | `DATETIME` | NO | `CURRENT_TIMESTAMP` |  |
| `created_by` | `BIGINT UNSIGNED` | YES | `` |  |
| `updated_at` | `DATETIME` | YES | `NULL ON UPDATE CURRENT_TIMESTAMP` |  |
| `updated_by` | `BIGINT UNSIGNED` | YES | `` |  |

**Primary Key:**
- `PRIMARY KEY (partner_id)`

**Unique Constraints:**
- `UNIQUE KEY uq_partners_code (partner_code)`

**Indexes:**
- `FULLTEXT KEY ft_partners_search (name, short_name, description)`
- `KEY idx_partners_country (country)`
- `KEY idx_partners_status (cooperation_status)`
- `KEY idx_partners_type_status (partner_type, cooperation_status)`
- `KEY idx_partners_created_at (created_at)`

### 4.13. `partner_contacts`

**Columns:**

| Column | Type | Null | Default | Notes |
|---|---|---:|---|---|
| `contact_id` | `BIGINT UNSIGNED` | NO | `` |  |
| `partner_id` | `BIGINT UNSIGNED` | NO | `` |  |
| `full_name` | `VARCHAR(150)` | NO | `` |  |
| `email` | `VARCHAR(150)` | YES | `` |  |
| `phone` | `VARCHAR(50)` | YES | `` |  |
| `job_title` | `VARCHAR(150)` | YES | `` |  |
| `department_name` | `VARCHAR(150)` | YES | `` |  |
| `note` | `TEXT` | YES | `` |  |
| `is_primary` | `BOOLEAN` | NO | `FALSE` |  |
| `status` | `ENUM('ACTIVE','INACTIVE')` | NO | `'ACTIVE'` |  |
| `created_at` | `DATETIME` | NO | `CURRENT_TIMESTAMP` |  |
| `created_by` | `BIGINT UNSIGNED` | YES | `` |  |
| `updated_at` | `DATETIME` | YES | `NULL ON UPDATE CURRENT_TIMESTAMP` |  |
| `updated_by` | `BIGINT UNSIGNED` | YES | `` |  |

**Primary Key:**
- `PRIMARY KEY (contact_id)`

**Unique Constraints:**
- `UNIQUE KEY uq_partner_contacts_partner_email (partner_id, email)`

**Indexes:**
- `KEY idx_partner_contacts_partner (partner_id)`
- `KEY idx_partner_contacts_email (email)`
- `KEY idx_partner_contacts_status (status)`

**Foreign Keys:**
- `CONSTRAINT fk_partner_contacts_partner FOREIGN KEY (partner_id) REFERENCES partners(partner_id) ON UPDATE CASCADE ON DELETE RESTRICT`

### 4.14. `files`

**Columns:**

| Column | Type | Null | Default | Notes |
|---|---|---:|---|---|
| `file_id` | `BIGINT UNSIGNED` | NO | `` |  |
| `storage_provider` | `ENUM('LOCAL','S3','AZURE','GCS','GOOGLE_DRIVE','OTHER')` | NO | `'LOCAL'` |  |
| `bucket_name` | `VARCHAR(150)` | YES | `` |  |
| `object_key` | `VARCHAR(700)` | NO | `` | Max 700 chars to keep UNIQUE index safe under utf8mb4 |
| `original_filename` | `VARCHAR(255)` | NO | `` |  |
| `mime_type` | `VARCHAR(150)` | YES | `` |  |
| `file_size` | `BIGINT UNSIGNED` | YES | `` |  |
| `checksum_sha256` | `CHAR(64)` | YES | `` |  |
| `visibility` | `ENUM('PRIVATE','INTERNAL','PUBLIC')` | NO | `'PRIVATE'` |  |
| `uploaded_by` | `BIGINT UNSIGNED` | YES | `` |  |
| `uploaded_at` | `DATETIME` | NO | `CURRENT_TIMESTAMP` |  |

**Primary Key:**
- `PRIMARY KEY (file_id)`

**Unique Constraints:**
- `UNIQUE KEY uq_files_object_key (object_key)`

**Indexes:**
- `KEY idx_files_uploaded_by (uploaded_by, uploaded_at)`
- `KEY idx_files_visibility (visibility)`
- `KEY idx_files_mime_time (mime_type, uploaded_at)`
- `KEY idx_files_checksum (checksum_sha256)`

**Foreign Keys:**
- `CONSTRAINT fk_files_uploaded_by FOREIGN KEY (uploaded_by) REFERENCES users(user_id) ON UPDATE CASCADE ON DELETE SET NULL`

### 4.15. `documents`

**Columns:**

| Column | Type | Null | Default | Notes |
|---|---|---:|---|---|
| `document_id` | `BIGINT UNSIGNED` | NO | `` |  |
| `file_id` | `BIGINT UNSIGNED` | NO | `` |  |
| `owner_type` | `ENUM('GENERAL','VISIT','PARTNER','MINUTES','NEWS','LOGISTICS','REPORT')` | NO | `'GENERAL'` |  |
| `owner_id` | `BIGINT UNSIGNED` | YES | `` |  |
| `campus_id` | `BIGINT UNSIGNED` | YES | `` |  |
| `title` | `VARCHAR(255)` | NO | `` |  |
| `description` | `TEXT` | YES | `` |  |
| `document_category` | `VARCHAR(100)` | YES | `` |  |
| `status` | `ENUM('DRAFT','PUBLISHED','ARCHIVED')` | NO | `'DRAFT'` |  |
| `created_at` | `DATETIME` | NO | `CURRENT_TIMESTAMP` |  |
| `created_by` | `BIGINT UNSIGNED` | YES | `` |  |
| `updated_at` | `DATETIME` | YES | `NULL ON UPDATE CURRENT_TIMESTAMP` |  |
| `updated_by` | `BIGINT UNSIGNED` | YES | `` |  |

**Primary Key:**
- `PRIMARY KEY (document_id)`

**Indexes:**
- `FULLTEXT KEY ft_documents_search (title, description)`
- `KEY idx_documents_owner (owner_type, owner_id)`
- `KEY idx_documents_campus_status (campus_id, status)`
- `KEY idx_documents_category_status (document_category, status)`
- `KEY idx_documents_created_by_time (created_by, created_at)`

**Foreign Keys:**
- `CONSTRAINT fk_documents_file FOREIGN KEY (file_id) REFERENCES files(file_id) ON UPDATE CASCADE ON DELETE RESTRICT`
- `CONSTRAINT fk_documents_campus FOREIGN KEY (campus_id) REFERENCES campuses(campus_id) ON UPDATE CASCADE ON DELETE SET NULL`
- `CONSTRAINT fk_documents_created_by FOREIGN KEY (created_by) REFERENCES users(user_id) ON UPDATE CASCADE ON DELETE SET NULL`

### 4.16. `visit_requests`

**Columns:**

| Column | Type | Null | Default | Notes |
|---|---|---:|---|---|
| `visit_request_id` | `BIGINT UNSIGNED` | NO | `` |  |
| `request_code` | `VARCHAR(50)` | NO | `` |  |
| `visitor_user_id` | `BIGINT UNSIGNED` | NO | `` | Visitor user/account created or linked for the registrant |
| `partner_id` | `BIGINT UNSIGNED` | YES | `` |  |
| `registrant_full_name` | `VARCHAR(150)` | NO | `` | Họ và tên người đăng ký |
| `registrant_organization` | `VARCHAR(200)` | NO | `` | Đơn vị công tác người đăng ký |
| `registrant_job_title` | `VARCHAR(150)` | YES | `` | Chức danh/phòng ban người đăng ký |
| `registrant_phone` | `VARCHAR(50)` | YES | `` | SĐT người đăng ký |
| `registrant_email` | `VARCHAR(150)` | NO | `` | Email người đăng ký |
| `registrant_nationality` | `VARCHAR(100)` | YES | `` | Quốc tịch người đăng ký |
| `delegation_name` | `VARCHAR(200)` | NO | `` | Tên đoàn khách |
| `visit_scope` | `ENUM('SINGLE_CAMPUS','MULTI_CAMPUS')` | NO | `'SINGLE_CAMPUS'` | SINGLE_CAMPUS: Staff Leader duyệt request tổng; MULTI_CAMPUS: HO duyệt request tổng. Frontend/backend suy ra người duyệt từ cột này. |
| `purpose` | `TEXT` | NO | `` | Mục đích thăm FPTU |
| `working_content` | `TEXT` | YES | `` | Nội dung làm việc tại FPTU |
| `expected_guest_count` | `INT UNSIGNED` | NO | `1` | Số khách dự kiến; có thể đồng bộ từ danh sách khách |
| `support_team_json` | `JSON` | YES | `` | Danh sách team hỗ trợ khách từ phía đoàn/đơn vị gửi |
| `contact_person_json` | `JSON` | YES | `` | Thông tin đầu mối liên hệ: full_name, organization, phone, email |
| `working_language` | `ENUM('VI','EN','OTHER')` | NO | `'EN'` | Ngôn ngữ sử dụng trong visit |
| `interpreter_note` | `TEXT` | YES | `` | Ghi chú nếu ngôn ngữ khác VI/EN và đầu mối cần tự bố trí phiên dịch |
| `transportation_note` | `TEXT` | YES | `` | Nhận diện phương tiện di chuyển tới FPTU |
| `note_to_fptu` | `TEXT` | YES | `` | Ghi chú cho FPTU |
| `status` | `ENUM('PENDING_APPROVAL','APPROVED','REJECTED','CANCELLED')` | NO | `'PENDING_APPROVAL'` | Request decision status only. Visit progress is derived from visit_request_campuses.status |
| `submitted_at` | `DATETIME` | NO | `CURRENT_TIMESTAMP` |  |
| `email_verified_at` | `DATETIME` | YES | `` |  |
| `decided_by` | `BIGINT UNSIGNED` | YES | `` | Người approve/reject request tổng |
| `decided_at` | `DATETIME` | YES | `` | Thời điểm xử lý request tổng |
| `decision_actor_role` | `ENUM('HO','STAFF_LEADER','SYSTEM')` | YES | `` | Vai trò người xử lý tại thời điểm quyết định |
| `decision_note` | `TEXT` | YES | `` | Lý do/ghi chú khi approve hoặc reject |
| `cancelled_by` | `BIGINT UNSIGNED` | YES | `` | Người thực hiện hủy request/delegation |
| `cancelled_at` | `DATETIME` | YES | `` | Thời điểm hủy request/delegation |
| `cancellation_actor_type` | `ENUM('VISITOR','HOST','STAFF_LEADER','HO','SYSTEM')` | YES | `` | Vai trò thực hiện thao tác hủy |
| `cancellation_source` | `ENUM('SELF_SERVICE','EXTERNAL_CONFIRMATION')` | YES | `` | SELF_SERVICE=Visitor tự hủy; EXTERNAL_CONFIRMATION=hủy sau xác nhận ngoài hệ thống |
| `cancellation_reason` | `TEXT` | YES | `` | Lý do hủy. Nếu EXTERNAL_CONFIRMATION thì ghi rõ kênh xác nhận, thời điểm, người xác nhận và lý do. |
| `row_version` | `INT UNSIGNED` | NO | `0` | Optimistic concurrency token |
| `created_at` | `DATETIME` | NO | `CURRENT_TIMESTAMP` |  |
| `created_by` | `BIGINT UNSIGNED` | YES | `` |  |
| `updated_at` | `DATETIME` | YES | `NULL ON UPDATE CURRENT_TIMESTAMP` |  |
| `updated_by` | `BIGINT UNSIGNED` | YES | `` |  |

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

### 4.17. `visit_request_campuses`

**Columns:**

| Column | Type | Null | Default | Notes |
|---|---|---:|---|---|
| `visit_instance_id` | `BIGINT UNSIGNED` | NO | `` |  |
| `visit_request_id` | `BIGINT UNSIGNED` | NO | `` |  |
| `campus_id` | `BIGINT UNSIGNED` | NO | `` |  |
| `instance_code` | `VARCHAR(60)` | YES | `` |  |
| `planned_start_at` | `DATETIME` | NO | `` | Ngày giờ bắt đầu dự kiến tại campus |
| `planned_end_at` | `DATETIME` | NO | `` | Ngày giờ kết thúc dự kiến tại campus |
| `status` | `ENUM( 'WAITING_REQUEST_APPROVAL', 'ASSIGNED', 'BEFORE_VISIT', 'DURING_VISIT', 'AFTER_VISIT', 'CLOSED', 'CANCELLED' )` | NO | `'WAITING_REQUEST_APPROVAL'` |  |
| `current_host_user_id` | `BIGINT UNSIGNED` | YES | `` | Host hiện tại chịu trách nhiệm campus instance. Sau khi request tổng được duyệt thì phải có host; nếu đổi host dùng chức năng Transfer Host |
| `host_assigned_by` | `BIGINT UNSIGNED` | YES | `` | Người gây ra thao tác gán host: HO khi auto gán Staff Leader cho multi-campus, Staff Leader khi duyệt single-campus, hoặc người chuyển host |
| `host_assigned_at` | `DATETIME` | YES | `` | Thời điểm host được gán |
| `host_assignment_source` | `ENUM('AUTO_STAFF_LEADER','MANUAL_APPROVAL','TRANSFERRED')` | YES | `` | AUTO_STAFF_LEADER=HO duyệt liên cơ sở và hệ thống tự gán Staff Leader; MANUAL_APPROVAL=Staff Leader duyệt đơn một cơ sở và chọn host; TRANSFERRED=host được chuyển sau đó |
| `host_transferred_by` | `BIGINT UNSIGNED` | YES | `` | Người chuyển host gần nhất |
| `host_transferred_at` | `DATETIME` | YES | `` | Thời điểm chuyển host gần nhất |
| `host_transfer_note` | `TEXT` | YES | `` | Ghi chú/lý do chuyển host gần nhất |
| `closed_by` | `BIGINT UNSIGNED` | YES | `` |  |
| `closed_at` | `DATETIME` | YES | `` |  |
| `close_note` | `TEXT` | YES | `` |  |
| `cancelled_by` | `BIGINT UNSIGNED` | YES | `` | Người thực hiện hủy campus instance |
| `cancelled_at` | `DATETIME` | YES | `` | Thời điểm hủy campus instance |
| `cancellation_actor_type` | `ENUM('VISITOR','HOST','STAFF_LEADER','HO','SYSTEM')` | YES | `` | Vai trò thực hiện thao tác hủy campus instance |
| `cancellation_source` | `ENUM('SELF_SERVICE','EXTERNAL_CONFIRMATION')` | YES | `` | Nguồn hủy campus instance |
| `cancellation_reason` | `TEXT` | YES | `` | Lý do hủy. Nếu EXTERNAL_CONFIRMATION thì ghi rõ kênh xác nhận, thời điểm, người xác nhận và lý do. |
| `row_version` | `INT UNSIGNED` | NO | `0` | Optimistic concurrency token |
| `created_at` | `DATETIME` | NO | `CURRENT_TIMESTAMP` |  |
| `created_by` | `BIGINT UNSIGNED` | YES | `` |  |
| `updated_at` | `DATETIME` | YES | `NULL ON UPDATE CURRENT_TIMESTAMP` |  |
| `updated_by` | `BIGINT UNSIGNED` | YES | `` |  |

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

**Columns:**

| Column | Type | Null | Default | Notes |
|---|---|---:|---|---|
| `guest_member_id` | `BIGINT UNSIGNED` | NO | `` |  |
| `visit_request_id` | `BIGINT UNSIGNED` | NO | `` |  |
| `full_name` | `VARCHAR(150)` | NO | `` |  |
| `organization` | `VARCHAR(200)` | YES | `` |  |
| `job_title` | `VARCHAR(150)` | YES | `` |  |
| `nationality` | `VARCHAR(100)` | YES | `` |  |
| `email` | `VARCHAR(150)` | YES | `` |  |
| `phone` | `VARCHAR(50)` | YES | `` |  |
| `is_representative` | `BOOLEAN` | NO | `FALSE` |  |
| `note` | `TEXT` | YES | `` |  |
| `created_at` | `DATETIME` | NO | `CURRENT_TIMESTAMP` |  |
| `created_by` | `BIGINT UNSIGNED` | YES | `` |  |
| `updated_at` | `DATETIME` | YES | `NULL ON UPDATE CURRENT_TIMESTAMP` |  |
| `updated_by` | `BIGINT UNSIGNED` | YES | `` |  |

**Primary Key:**
- `PRIMARY KEY (guest_member_id)`

**Indexes:**
- `KEY idx_guest_members_request (visit_request_id)`
- `KEY idx_guest_members_email (email)`
- `KEY idx_guest_members_representative (visit_request_id, is_representative)`

**Foreign Keys:**
- `CONSTRAINT fk_guest_members_request FOREIGN KEY (visit_request_id) REFERENCES visit_requests(visit_request_id) ON UPDATE CASCADE ON DELETE RESTRICT`

### 4.19. `visit_participants`

**Columns:**

| Column | Type | Null | Default | Notes |
|---|---|---:|---|---|
| `participant_id` | `BIGINT UNSIGNED` | NO | `` |  |
| `visit_instance_id` | `BIGINT UNSIGNED` | NO | `` |  |
| `user_id` | `BIGINT UNSIGNED` | NO | `` |  |
| `participant_role` | `ENUM('IC_HOST','IC_SUPPORT','DEPT_SUPPORT','STUDENT_BUDDY','MEDIA','INTERPRETER','OTHER')` | NO | `'OTHER'` |  |
| `is_host` | `BOOLEAN` | NO | `FALSE` |  |
| `status` | `ENUM('INVITED','ACCEPTED','DECLINED','ASSIGNED','REMOVED')` | NO | `'INVITED'` |  |
| `invited_by` | `BIGINT UNSIGNED` | YES | `` |  |
| `invited_at` | `DATETIME` | YES | `` |  |
| `responded_at` | `DATETIME` | YES | `` |  |
| `assigned_by` | `BIGINT UNSIGNED` | YES | `` |  |
| `assigned_at` | `DATETIME` | YES | `` |  |
| `note` | `TEXT` | YES | `` |  |
| `created_at` | `DATETIME` | NO | `CURRENT_TIMESTAMP` |  |
| `created_by` | `BIGINT UNSIGNED` | YES | `` |  |
| `updated_at` | `DATETIME` | YES | `NULL ON UPDATE CURRENT_TIMESTAMP` |  |
| `updated_by` | `BIGINT UNSIGNED` | YES | `` |  |

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

**Columns:**

| Column | Type | Null | Default | Notes |
|---|---|---:|---|---|
| `agenda_id` | `BIGINT UNSIGNED` | NO | `` |  |
| `visit_instance_id` | `BIGINT UNSIGNED` | NO | `` |  |
| `sequence_order` | `INT UNSIGNED` | NO | `` |  |
| `title` | `VARCHAR(255)` | NO | `` |  |
| `description` | `TEXT` | YES | `` |  |
| `start_time` | `DATETIME` | NO | `` |  |
| `end_time` | `DATETIME` | YES | `` |  |
| `location` | `VARCHAR(255)` | YES | `` |  |
| `responsible_user_id` | `BIGINT UNSIGNED` | YES | `` |  |
| `created_at` | `DATETIME` | NO | `CURRENT_TIMESTAMP` |  |
| `created_by` | `BIGINT UNSIGNED` | YES | `` |  |
| `updated_at` | `DATETIME` | YES | `NULL ON UPDATE CURRENT_TIMESTAMP` |  |
| `updated_by` | `BIGINT UNSIGNED` | YES | `` |  |

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

**Columns:**

| Column | Type | Null | Default | Notes |
|---|---|---:|---|---|
| `logistics_item_id` | `BIGINT UNSIGNED` | NO | `` |  |
| `visit_instance_id` | `BIGINT UNSIGNED` | NO | `` |  |
| `item_type` | `ENUM('ROOM','TRANSPORT','MEAL','EQUIPMENT','BANNER','LED','OTHER')` | NO | `` |  |
| `title` | `VARCHAR(255)` | NO | `` |  |
| `description` | `TEXT` | YES | `` | Nội dung chi tiết công việc gốc |
| `quantity` | `INT UNSIGNED` | YES | `` | Số lượng yêu cầu gốc |
| `usage_start_at` | `DATETIME` | YES | `` | Thời gian bắt đầu sử dụng resource |
| `usage_end_at` | `DATETIME` | YES | `` | Thời gian kết thúc sử dụng resource |
| `status` | `ENUM( 'PLANNED', 'REQUESTED', 'CHANGE_PROPOSED', 'RECEIVED', 'ASSIGNED', 'ACCEPTED', 'IN_PROGRESS', 'READY', 'DONE', 'REJECTED', 'CANCELLED' )` | NO | `'PLANNED'` |  |
| `priority` | `ENUM('LOW','MEDIUM','HIGH','URGENT')` | NO | `'MEDIUM'` |  |
| `requested_by` | `BIGINT UNSIGNED` | YES | `` | Người gửi yêu cầu hậu cần/resource |
| `requested_to_department_id` | `BIGINT UNSIGNED` | YES | `` | Phòng ban được yêu cầu xử lý |
| `requested_at` | `DATETIME` | YES | `` | Thời điểm gửi yêu cầu |
| `received_by` | `BIGINT UNSIGNED` | YES | `` | Trưởng phòng/người tiếp nhận yêu cầu |
| `received_at` | `DATETIME` | YES | `` | Thời điểm tiếp nhận yêu cầu |
| `assigned_to_user_id` | `BIGINT UNSIGNED` | YES | `` | Nhân viên được giao xử lý chính |
| `assigned_by` | `BIGINT UNSIGNED` | YES | `` | Người phân công |
| `assigned_at` | `DATETIME` | YES | `` | Thời điểm phân công |
| `assignee_accepted_at` | `DATETIME` | YES | `` | Thời điểm nhân viên xác nhận nhận nhiệm vụ |
| `assignee_response_note` | `TEXT` | YES | `` | Ghi chú khi nhân viên nhận/từ chối nếu có |
| `due_at` | `DATETIME` | YES | `` | Deadline hoàn thành hạng mục |
| `completed_at` | `DATETIME` | YES | `` | Thời điểm hoàn thành |
| `proposed_by` | `BIGINT UNSIGNED` | YES | `` | Người gửi đề xuất thay đổi |
| `proposed_at` | `DATETIME` | YES | `` | Thời điểm gửi đề xuất thay đổi |
| `proposed_quantity` | `INT UNSIGNED` | YES | `` | Số lượng được đề xuất thay đổi |
| `proposed_usage_start_at` | `DATETIME` | YES | `` | Thời gian bắt đầu sử dụng được đề xuất |
| `proposed_usage_end_at` | `DATETIME` | YES | `` | Thời gian kết thúc sử dụng được đề xuất |
| `proposed_description` | `TEXT` | YES | `` | Nội dung chi tiết công việc được đề xuất thay đổi |
| `proposal_note` | `TEXT` | YES | `` | Lý do/ghi chú đề xuất thay đổi |
| `proposal_responded_by` | `BIGINT UNSIGNED` | YES | `` | Người xác nhận/từ chối đề xuất |
| `proposal_responded_at` | `DATETIME` | YES | `` | Thời điểm xác nhận/từ chối đề xuất |
| `proposal_response` | `ENUM('ACCEPTED','REJECTED')` | YES | `` | Kết quả phản hồi đề xuất |
| `proposal_response_note` | `TEXT` | YES | `` | Ghi chú phản hồi đề xuất |
| `decision_note` | `TEXT` | YES | `` | Lý do reject/cancel hoặc ghi chú xử lý |
| `row_version` | `INT UNSIGNED` | NO | `0` | Optimistic concurrency token |
| `created_at` | `DATETIME` | NO | `CURRENT_TIMESTAMP` |  |
| `created_by` | `BIGINT UNSIGNED` | YES | `` |  |
| `updated_at` | `DATETIME` | YES | `NULL ON UPDATE CURRENT_TIMESTAMP` |  |
| `updated_by` | `BIGINT UNSIGNED` | YES | `` |  |

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

### 4.22. `minutes`

**Columns:**

| Column | Type | Null | Default | Notes |
|---|---|---:|---|---|
| `minutes_id` | `BIGINT UNSIGNED` | NO | `` |  |
| `visit_instance_id` | `BIGINT UNSIGNED` | NO | `` |  |
| `title` | `VARCHAR(255)` | NO | `` |  |
| `content` | `LONGTEXT` | YES | `` |  |
| `participants_json` | `JSON` | YES | `` | Danh sách người tham gia trong biên bản, lưu dạng snapshot nếu cần hiển thị lại |
| `status` | `ENUM('DRAFT','FINAL')` | NO | `'DRAFT'` | DRAFT=đang soạn, FINAL=đã chốt |
| `finalized_by` | `BIGINT UNSIGNED` | YES | `` | Người chốt biên bản |
| `finalized_at` | `DATETIME` | YES | `` | Thời điểm chốt biên bản |
| `created_at` | `DATETIME` | NO | `CURRENT_TIMESTAMP` |  |
| `created_by` | `BIGINT UNSIGNED` | YES | `` |  |
| `updated_at` | `DATETIME` | YES | `NULL ON UPDATE CURRENT_TIMESTAMP` |  |
| `updated_by` | `BIGINT UNSIGNED` | YES | `` |  |

**Primary Key:**
- `PRIMARY KEY (minutes_id)`

**Indexes:**
- `FULLTEXT KEY ft_minutes_search (title, content)`
- `KEY idx_minutes_visit_status (visit_instance_id, status)`
- `KEY idx_minutes_created_by_time (created_by, created_at)`
- `KEY idx_minutes_finalized_by_time (finalized_by, finalized_at)`

**Foreign Keys:**
- `CONSTRAINT fk_minutes_visit_instance FOREIGN KEY (visit_instance_id) REFERENCES visit_request_campuses(visit_instance_id) ON UPDATE CASCADE ON DELETE RESTRICT`
- `CONSTRAINT fk_minutes_created_by FOREIGN KEY (created_by) REFERENCES users(user_id) ON UPDATE CASCADE ON DELETE SET NULL`
- `CONSTRAINT fk_minutes_updated_by FOREIGN KEY (updated_by) REFERENCES users(user_id) ON UPDATE CASCADE ON DELETE SET NULL`
- `CONSTRAINT fk_minutes_finalized_by FOREIGN KEY (finalized_by) REFERENCES users(user_id) ON UPDATE CASCADE ON DELETE SET NULL`

### 4.23. `minute_action_items`

**Columns:**

| Column | Type | Null | Default | Notes |
|---|---|---:|---|---|
| `action_item_id` | `BIGINT UNSIGNED` | NO | `` |  |
| `minutes_id` | `BIGINT UNSIGNED` | NO | `` |  |
| `title` | `VARCHAR(255)` | NO | `` | Tên đầu việc |
| `note` | `TEXT` | YES | `` | Ghi chú thêm cho đầu việc |
| `due_date` | `DATE` | YES | `` | Deadline của đầu việc |
| `status` | `ENUM('TODO','IN_PROGRESS','DONE','CANCELLED')` | NO | `'TODO'` | TODO=chưa làm, IN_PROGRESS=đang làm, DONE=hoàn thành, CANCELLED=đã hủy/không cần làm nữa |
| `completed_at` | `DATETIME` | YES | `` | Thời điểm hoàn thành; backend tự set khi status chuyển sang DONE |
| `display_order` | `INT UNSIGNED` | NO | `1` | Thứ tự hiển thị trong biên bản |
| `created_at` | `DATETIME` | NO | `CURRENT_TIMESTAMP` |  |
| `created_by` | `BIGINT UNSIGNED` | YES | `` |  |
| `updated_at` | `DATETIME` | YES | `NULL ON UPDATE CURRENT_TIMESTAMP` |  |
| `updated_by` | `BIGINT UNSIGNED` | YES | `` |  |

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

### 4.24. `feedbacks`

**Columns:**

| Column | Type | Null | Default | Notes |
|---|---|---:|---|---|
| `feedback_id` | `BIGINT UNSIGNED` | NO | `` |  |
| `visit_request_id` | `BIGINT UNSIGNED` | NO | `` |  |
| `visit_instance_id` | `BIGINT UNSIGNED` | YES | `` |  |
| `submitted_by_user_id` | `BIGINT UNSIGNED` | NO | `` | User gửi feedback; khách/host/logistics đều phải có tài khoản hệ thống |
| `submitter_role` | `ENUM('VISITOR','HOST','LOGISTICS')` | NO | `` | Vai trò người gửi trong chuyến thăm |
| `submitter_context` | `VARCHAR(120)` | NO | `''` | Ngữ cảnh vai trò người gửi, ví dụ: Host chính, Xe điện, Teabreak, Khách đại diện |
| `submitter_name_snapshot` | `VARCHAR(255)` | NO | `` | Tên người gửi tại thời điểm gửi feedback |
| `target_user_id` | `BIGINT UNSIGNED` | NO | `` | User được đánh giá |
| `target_role` | `ENUM('VISITOR','HOST','LOGISTICS')` | NO | `` | Vai trò người được đánh giá trong chuyến thăm |
| `target_context` | `VARCHAR(120)` | NO | `''` | Ngữ cảnh đối tượng được đánh giá, ví dụ: Host chính, Đoàn khách, Xe điện, Teabreak |
| `target_name_snapshot` | `VARCHAR(255)` | NO | `` | Tên người được đánh giá tại thời điểm gửi feedback |
| `rating` | `TINYINT UNSIGNED` | NO | `` | Số sao từ 1 đến 5 |
| `comment` | `TEXT` | NO | `` | Nội dung feedback |
| `submitted_at` | `DATETIME` | NO | `CURRENT_TIMESTAMP` |  |

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

### 4.25. `news`

**Columns:**

| Column | Type | Null | Default | Notes |
|---|---|---:|---|---|
| `news_id` | `BIGINT UNSIGNED` | NO | `` |  |
| `campus_id` | `BIGINT UNSIGNED` | YES | `` | Campus liên quan đến bài viết. NULL nếu bài toàn hệ thống |
| `visit_instance_id` | `BIGINT UNSIGNED` | YES | `` | Visit instance liên quan nếu bài viết được tạo từ một chuyến tiếp đón |
| `author_user_id` | `BIGINT UNSIGNED` | NO | `` | Người tạo/viết bài |
| `cover_file_id` | `BIGINT UNSIGNED` | YES | `` | Ảnh bìa bài viết, trỏ tới files.file_id |
| `status` | `ENUM('PENDING_REVIEW','REJECTED','PUBLISHED','HIDDEN')` | NO | `'PENDING_REVIEW'` | PENDING_REVIEW=chờ host duyệt, REJECTED=bị từ chối, PUBLISHED=đã đăng, HIDDEN=ẩn khỏi trang tin |
| `submitted_at` | `DATETIME` | NO | `CURRENT_TIMESTAMP` | Thời điểm người viết gửi bài cho host duyệt |
| `reviewed_by` | `BIGINT UNSIGNED` | YES | `` | Host duyệt hoặc từ chối bài viết |
| `reviewed_at` | `DATETIME` | YES | `` | Thời điểm host duyệt hoặc từ chối |
| `review_note` | `TEXT` | YES | `` | Ghi chú duyệt hoặc lý do từ chối |
| `published_at` | `DATETIME` | YES | `` | Thời điểm bài viết được đăng |
| `is_featured` | `BOOLEAN` | NO | `FALSE` | Bài viết nổi bật |
| `row_version` | `INT UNSIGNED` | NO | `0` | Optimistic concurrency token, chống ghi đè khi cập nhật đồng thời |
| `created_at` | `DATETIME` | NO | `CURRENT_TIMESTAMP` |  |
| `created_by` | `BIGINT UNSIGNED` | YES | `` |  |
| `updated_at` | `DATETIME` | YES | `NULL ON UPDATE CURRENT_TIMESTAMP` |  |
| `updated_by` | `BIGINT UNSIGNED` | YES | `` |  |

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

### 4.26. `news_translations`

**Columns:**

| Column | Type | Null | Default | Notes |
|---|---|---:|---|---|
| `news_translation_id` | `BIGINT UNSIGNED` | NO | `` |  |
| `news_id` | `BIGINT UNSIGNED` | NO | `` |  |
| `language_code` | `ENUM('vi','en','zh','ja','ko')` | NO | `'vi'` |  |
| `title` | `VARCHAR(255)` | NO | `` | Tiêu đề chính của bài viết |
| `slug` | `VARCHAR(255)` | NO | `` | Đường dẫn SEO của bài viết |
| `summary` | `TEXT` | YES | `` | Tóm tắt bài viết |
| `seo_title` | `VARCHAR(255)` | YES | `` |  |
| `seo_description` | `VARCHAR(500)` | YES | `` |  |
| `created_at` | `DATETIME` | NO | `CURRENT_TIMESTAMP` |  |
| `updated_at` | `DATETIME` | YES | `NULL ON UPDATE CURRENT_TIMESTAMP` |  |

**Primary Key:**
- `PRIMARY KEY (news_translation_id)`

**Unique Constraints:**
- `UNIQUE KEY uq_news_translation_lang (news_id, language_code)`
- `UNIQUE KEY uq_news_translation_slug_lang (slug, language_code)`

**Indexes:**
- `FULLTEXT KEY ft_news_translations_search (title, summary)`
- `KEY idx_news_translations_lang (language_code)`

**Foreign Keys:**
- `CONSTRAINT fk_news_translations_news FOREIGN KEY (news_id) REFERENCES news(news_id) ON UPDATE CASCADE ON DELETE CASCADE`

### 4.27. `news_content_sections`

**Columns:**

| Column | Type | Null | Default | Notes |
|---|---|---:|---|---|
| `section_id` | `BIGINT UNSIGNED` | NO | `` |  |
| `news_translation_id` | `BIGINT UNSIGNED` | NO | `` |  |
| `section_order` | `TINYINT UNSIGNED` | NO | `` | Thứ tự section, từ 1 đến 10 |
| `section_title` | `VARCHAR(255)` | NO | `` | Tiêu đề section |
| `section_body_html` | `LONGTEXT` | NO | `` | Nội dung rich text dạng HTML đã sanitize, có thể chứa paragraph, bold, italic, color, link, image |
| `section_body_text` | `TEXT` | YES | `` | Plain text tách từ HTML để search hoặc preview |
| `created_at` | `DATETIME` | NO | `CURRENT_TIMESTAMP` |  |
| `updated_at` | `DATETIME` | YES | `NULL ON UPDATE CURRENT_TIMESTAMP` |  |

**Primary Key:**
- `PRIMARY KEY (section_id)`

**Unique Constraints:**
- `UNIQUE KEY uq_news_section_order (news_translation_id, section_order)`

**Indexes:**
- `FULLTEXT KEY ft_news_sections_search (section_title, section_body_text)`
- `KEY idx_news_sections_translation (news_translation_id)`

**Foreign Keys:**
- `CONSTRAINT fk_news_sections_translation FOREIGN KEY (news_translation_id) REFERENCES news_translations(news_translation_id) ON UPDATE CASCADE ON DELETE CASCADE`

**Check Constraints:**
- `CHECK (section_order BETWEEN 1 AND 10)`

### 4.28. `news_section_files`

**Columns:**

| Column | Type | Null | Default | Notes |
|---|---|---:|---|---|
| `section_file_id` | `BIGINT UNSIGNED` | NO | `` |  |
| `section_id` | `BIGINT UNSIGNED` | NO | `` |  |
| `file_id` | `BIGINT UNSIGNED` | NO | `` |  |
| `usage_type` | `ENUM('INLINE_IMAGE','ATTACHMENT')` | NO | `'INLINE_IMAGE'` | INLINE_IMAGE=ảnh chèn trong nội dung, ATTACHMENT=file đính kèm |
| `display_order` | `INT UNSIGNED` | NO | `0` |  |
| `created_at` | `DATETIME` | NO | `CURRENT_TIMESTAMP` |  |

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

### 4.29. `faqs`

**Columns:**

| Column | Type | Null | Default | Notes |
|---|---|---:|---|---|
| `faq_id` | `BIGINT UNSIGNED` | NO | `` |  |
| `category` | `VARCHAR(100)` | YES | `` | Nhóm FAQ, ví dụ: Visit Request, Security, Logistics |
| `question` | `VARCHAR(500)` | NO | `` | Câu hỏi FAQ |
| `answer` | `TEXT` | NO | `` | Câu trả lời FAQ |
| `display_order` | `INT UNSIGNED` | NO | `0` |  |
| `status` | `ENUM('PUBLISHED','HIDDEN')` | NO | `'HIDDEN'` | PUBLISHED=hiển thị trên trang FAQ, HIDDEN=ẩn khỏi người xem thường nhưng người quản lý vẫn thấy |
| `created_at` | `DATETIME` | NO | `CURRENT_TIMESTAMP` |  |
| `created_by` | `BIGINT UNSIGNED` | YES | `` |  |
| `updated_at` | `DATETIME` | YES | `NULL ON UPDATE CURRENT_TIMESTAMP` |  |
| `updated_by` | `BIGINT UNSIGNED` | YES | `` |  |

**Primary Key:**
- `PRIMARY KEY (faq_id)`

**Indexes:**
- `FULLTEXT KEY ft_faqs_search (question, answer)`
- `KEY idx_faqs_status_order (status, display_order)`
- `KEY idx_faqs_category_status (category, status)`

### 4.30. `galleries`

**Columns:**

| Column | Type | Null | Default | Notes |
|---|---|---:|---|---|
| `gallery_id` | `BIGINT UNSIGNED` | NO | `` |  |
| `campus_id` | `BIGINT UNSIGNED` | NO | `` |  |
| `location_name` | `VARCHAR(150)` | NO | `` | Tên địa điểm trong campus, ví dụ: Sảnh Alpha, Green Lab, Thư viện |
| `title` | `VARCHAR(255)` | NO | `` | Tên hiển thị của gallery/địa điểm |
| `description` | `TEXT` | YES | `` | Mô tả ngắn về địa điểm |
| `story_content` | `TEXT` | YES | `` | Ý nghĩa hoặc câu chuyện giới thiệu về địa điểm |
| `status` | `ENUM('DRAFT','PUBLISHED','HIDDEN')` | NO | `'DRAFT'` | DRAFT=nháp, PUBLISHED=hiển thị theo visibility, HIDDEN=ẩn khỏi người xem thường nhưng Staff Leader vẫn quản lý được |
| `visibility` | `ENUM('PRIVATE','INTERNAL','PUBLIC')` | NO | `'INTERNAL'` | Phạm vi xem khi status=PUBLISHED: PRIVATE=chỉ quản lý, INTERNAL=user nội bộ, PUBLIC=công khai |
| `created_at` | `DATETIME` | NO | `CURRENT_TIMESTAMP` |  |
| `created_by` | `BIGINT UNSIGNED` | YES | `` |  |
| `updated_at` | `DATETIME` | YES | `NULL ON UPDATE CURRENT_TIMESTAMP` |  |
| `updated_by` | `BIGINT UNSIGNED` | YES | `` |  |
| `deleted_at` | `DATETIME` | YES | `` |  |
| `deleted_by` | `BIGINT UNSIGNED` | YES | `` |  |

**Primary Key:**
- `PRIMARY KEY (gallery_id)`

**Indexes:**
- `KEY idx_galleries_campus_status (campus_id, status, deleted_at)`
- `KEY idx_galleries_location_name (location_name)`
- `KEY idx_galleries_visibility_status (visibility, status)`

**Foreign Keys:**
- `CONSTRAINT fk_galleries_campus FOREIGN KEY (campus_id) REFERENCES campuses(campus_id) ON UPDATE CASCADE ON DELETE RESTRICT`

### 4.31. `gallery_images`

**Columns:**

| Column | Type | Null | Default | Notes |
|---|---|---:|---|---|
| `image_id` | `BIGINT UNSIGNED` | NO | `` |  |
| `gallery_id` | `BIGINT UNSIGNED` | NO | `` |  |
| `file_id` | `BIGINT UNSIGNED` | NO | `` |  |
| `caption` | `VARCHAR(500)` | YES | `` | Chú thích riêng cho từng ảnh |
| `display_order` | `INT UNSIGNED` | NO | `0` |  |
| `taken_at` | `DATETIME` | YES | `` |  |
| `status` | `ENUM('ACTIVE','HIDDEN')` | NO | `'ACTIVE'` | ACTIVE=ảnh đang dùng, HIDDEN=ảnh bị ẩn khỏi gallery thường |
| `created_at` | `DATETIME` | NO | `CURRENT_TIMESTAMP` |  |
| `created_by` | `BIGINT UNSIGNED` | YES | `` |  |
| `updated_at` | `DATETIME` | YES | `NULL ON UPDATE CURRENT_TIMESTAMP` |  |
| `updated_by` | `BIGINT UNSIGNED` | YES | `` |  |
| `deleted_at` | `DATETIME` | YES | `` |  |
| `deleted_by` | `BIGINT UNSIGNED` | YES | `` |  |

**Primary Key:**
- `PRIMARY KEY (image_id)`

**Unique Constraints:**
- `UNIQUE KEY uq_gallery_images_file (file_id)`

**Indexes:**
- `KEY idx_gallery_images_gallery_order (gallery_id, display_order)`
- `KEY idx_gallery_images_status_time (status, taken_at)`

**Foreign Keys:**
- `CONSTRAINT fk_gallery_images_gallery FOREIGN KEY (gallery_id) REFERENCES galleries(gallery_id) ON UPDATE CASCADE ON DELETE RESTRICT`
- `CONSTRAINT fk_gallery_images_file FOREIGN KEY (file_id) REFERENCES files(file_id) ON UPDATE CASCADE ON DELETE RESTRICT`

### 4.32. `photo_face_tags`

**Columns:**

| Column | Type | Null | Default | Notes |
|---|---|---:|---|---|
| `face_tag_id` | `BIGINT UNSIGNED` | NO | `` |  |
| `image_id` | `BIGINT UNSIGNED` | NO | `` |  |
| `visit_request_id` | `BIGINT UNSIGNED` | YES | `` |  |
| `guest_member_id` | `BIGINT UNSIGNED` | YES | `` |  |
| `partner_contact_id` | `BIGINT UNSIGNED` | YES | `` |  |
| `display_name` | `VARCHAR(150)` | NO | `` |  |
| `bounding_box_x` | `DECIMAL(8,4)` | YES | `` |  |
| `bounding_box_y` | `DECIMAL(8,4)` | YES | `` |  |
| `bounding_box_width` | `DECIMAL(8,4)` | YES | `` |  |
| `bounding_box_height` | `DECIMAL(8,4)` | YES | `` |  |
| `tag_status` | `ENUM('MANUALLY_TAGGED','CONFIRMED','REMOVED')` | NO | `'MANUALLY_TAGGED'` |  |
| `confirmed_by` | `BIGINT UNSIGNED` | YES | `` |  |
| `confirmed_at` | `DATETIME` | YES | `` |  |
| `created_at` | `DATETIME` | NO | `CURRENT_TIMESTAMP` |  |
| `created_by` | `BIGINT UNSIGNED` | YES | `` |  |
| `removed_at` | `DATETIME` | YES | `` |  |
| `removed_by` | `BIGINT UNSIGNED` | YES | `` |  |

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

### 4.33. `email_templates`

**Columns:**

| Column | Type | Null | Default | Notes |
|---|---|---:|---|---|
| `email_template_id` | `BIGINT UNSIGNED` | NO | `` |  |
| `template_code` | `VARCHAR(100)` | NO | `` |  |
| `name` | `VARCHAR(150)` | NO | `` |  |
| `purpose` | `VARCHAR(100)` | NO | `` |  |
| `status` | `ENUM('ACTIVE','INACTIVE')` | NO | `'ACTIVE'` |  |
| `translations_json` | `JSON` | NO | `` | Merged email_template_translations table |
| `variables_json` | `JSON` | YES | `` | Allowed variables: FullName, OtpCode, Link... |
| `created_at` | `DATETIME` | NO | `CURRENT_TIMESTAMP` |  |
| `created_by` | `BIGINT UNSIGNED` | YES | `` |  |
| `updated_at` | `DATETIME` | YES | `NULL ON UPDATE CURRENT_TIMESTAMP` |  |
| `updated_by` | `BIGINT UNSIGNED` | YES | `` |  |

**Primary Key:**
- `PRIMARY KEY (email_template_id)`

**Unique Constraints:**
- `UNIQUE KEY uq_email_templates_code (template_code)`

**Indexes:**
- `KEY idx_email_templates_status (status)`
- `KEY idx_email_templates_purpose_status (purpose, status)`

### 4.34. `sent_emails`

**Columns:**

| Column | Type | Null | Default | Notes |
|---|---|---:|---|---|
| `sent_email_id` | `BIGINT UNSIGNED` | NO | `` |  |
| `email_template_id` | `BIGINT UNSIGNED` | YES | `` |  |
| `related_type` | `VARCHAR(80)` | YES | `` |  |
| `related_id` | `BIGINT UNSIGNED` | YES | `` |  |
| `subject` | `VARCHAR(255)` | NO | `` |  |
| `body_snapshot` | `LONGTEXT` | YES | `` |  |
| `recipients_json` | `JSON` | NO | `` | Merged sent_email_recipients table |
| `metadata_json` | `JSON` | YES | `` | provider message id, retry count, etc. |
| `status` | `ENUM('QUEUED','SENT','FAILED')` | NO | `'QUEUED'` |  |
| `error_message` | `TEXT` | YES | `` |  |
| `sent_by` | `BIGINT UNSIGNED` | YES | `` |  |
| `sent_at` | `DATETIME` | YES | `` |  |
| `created_at` | `DATETIME` | NO | `CURRENT_TIMESTAMP` |  |

**Primary Key:**
- `PRIMARY KEY (sent_email_id)`

**Indexes:**
- `KEY idx_sent_emails_template (email_template_id)`
- `KEY idx_sent_emails_related (related_type, related_id)`
- `KEY idx_sent_emails_status_time (status, created_at)`
- `KEY idx_sent_emails_sent_by_time (sent_by, sent_at)`

**Foreign Keys:**
- `CONSTRAINT fk_sent_emails_template FOREIGN KEY (email_template_id) REFERENCES email_templates(email_template_id) ON UPDATE CASCADE ON DELETE SET NULL`
- `CONSTRAINT fk_sent_emails_sent_by FOREIGN KEY (sent_by) REFERENCES users(user_id) ON UPDATE CASCADE ON DELETE SET NULL`

### 4.35. `notifications`

**Columns:**

| Column | Type | Null | Default | Notes |
|---|---|---:|---|---|
| `notification_id` | `BIGINT UNSIGNED` | NO | `` |  |
| `recipient_user_id` | `BIGINT UNSIGNED` | NO | `` |  |
| `title` | `VARCHAR(255)` | NO | `` |  |
| `message` | `TEXT` | YES | `` |  |
| `notification_type` | `VARCHAR(80)` | NO | `` |  |
| `related_type` | `VARCHAR(80)` | YES | `` |  |
| `related_id` | `BIGINT UNSIGNED` | YES | `` |  |
| `is_read` | `BOOLEAN` | NO | `FALSE` |  |
| `read_at` | `DATETIME` | YES | `` |  |
| `created_at` | `DATETIME` | NO | `CURRENT_TIMESTAMP` |  |

**Primary Key:**
- `PRIMARY KEY (notification_id)`

**Indexes:**
- `KEY idx_notifications_user_read_time (recipient_user_id, is_read, created_at)`
- `KEY idx_notifications_related (related_type, related_id)`
- `KEY idx_notifications_type_time (notification_type, created_at)`

**Foreign Keys:**
- `CONSTRAINT fk_notifications_user FOREIGN KEY (recipient_user_id) REFERENCES users(user_id) ON UPDATE CASCADE ON DELETE CASCADE`

### 4.36. `calendar_events`

**Columns:**

| Column | Type | Null | Default | Notes |
|---|---|---:|---|---|
| `calendar_event_id` | `BIGINT UNSIGNED` | NO | `` |  |
| `owner_user_id` | `BIGINT UNSIGNED` | NO | `` |  |
| `campus_id` | `BIGINT UNSIGNED` | YES | `` |  |
| `visit_instance_id` | `BIGINT UNSIGNED` | YES | `` |  |
| `logistics_item_id` | `BIGINT UNSIGNED` | YES | `` |  |
| `source_type` | `ENUM('PERSONAL','VISIT','LOGISTICS','DEADLINE')` | NO | `'PERSONAL'` |  |
| `title` | `VARCHAR(255)` | NO | `` |  |
| `description` | `TEXT` | YES | `` |  |
| `location` | `VARCHAR(255)` | YES | `` |  |
| `start_at` | `DATETIME` | NO | `` |  |
| `end_at` | `DATETIME` | NO | `` |  |
| `timezone` | `VARCHAR(50)` | NO | `'Asia/Ho_Chi_Minh'` |  |
| `visibility` | `ENUM('PRIVATE','INTERNAL')` | NO | `'PRIVATE'` |  |
| `attendees_json` | `JSON` | YES | `` | Merged calendar_event_attendees table |
| `reminders_json` | `JSON` | YES | `` | Merged calendar_event_reminders table |
| `status` | `ENUM('ACTIVE','CANCELLED','DONE')` | NO | `'ACTIVE'` |  |
| `created_at` | `DATETIME` | NO | `CURRENT_TIMESTAMP` |  |
| `created_by` | `BIGINT UNSIGNED` | YES | `` |  |
| `updated_at` | `DATETIME` | YES | `NULL ON UPDATE CURRENT_TIMESTAMP` |  |
| `updated_by` | `BIGINT UNSIGNED` | YES | `` |  |
| `deleted_at` | `DATETIME` | YES | `` |  |
| `deleted_by` | `BIGINT UNSIGNED` | YES | `` |  |

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

### 4.37. `api_configurations`

**Columns:**

| Column | Type | Null | Default | Notes |
|---|---|---:|---|---|
| `api_config_id` | `BIGINT UNSIGNED` | NO | `` |  |
| `api_code` | `VARCHAR(100)` | NO | `` |  |
| `name` | `VARCHAR(150)` | NO | `` |  |
| `provider_name` | `VARCHAR(150)` | YES | `` |  |
| `purpose` | `VARCHAR(150)` | YES | `` |  |
| `base_url` | `VARCHAR(500)` | NO | `` |  |
| `default_method` | `ENUM('GET','POST','PUT','PATCH','DELETE')` | NO | `'POST'` |  |
| `auth_type` | `ENUM('NONE','API_KEY','BEARER_TOKEN','BASIC','OAUTH2','CUSTOM')` | NO | `'NONE'` |  |
| `credentials_json` | `JSON` | YES | `` | Encrypted/masked credentials. Merged api_credentials table. |
| `headers_json` | `JSON` | YES | `` |  |
| `body_template_json` | `JSON` | YES | `` |  |
| `settings_json` | `JSON` | YES | `` |  |
| `timeout_seconds` | `INT UNSIGNED` | NO | `30` |  |
| `status` | `ENUM('ACTIVE','INACTIVE','DISABLED')` | NO | `'ACTIVE'` |  |
| `created_at` | `DATETIME` | NO | `CURRENT_TIMESTAMP` |  |
| `created_by` | `BIGINT UNSIGNED` | YES | `` |  |
| `updated_at` | `DATETIME` | YES | `NULL ON UPDATE CURRENT_TIMESTAMP` |  |
| `updated_by` | `BIGINT UNSIGNED` | YES | `` |  |
| `deleted_at` | `DATETIME` | YES | `` |  |
| `deleted_by` | `BIGINT UNSIGNED` | YES | `` |  |

**Primary Key:**
- `PRIMARY KEY (api_config_id)`

**Unique Constraints:**
- `UNIQUE KEY uq_api_config_code (api_code)`

**Indexes:**
- `KEY idx_api_config_status (status)`
- `KEY idx_api_provider_status (provider_name, status)`

### 4.38. `api_usage_quotas`

**Columns:**

| Column | Type | Null | Default | Notes |
|---|---|---:|---|---|
| `api_usage_quota_id` | `BIGINT UNSIGNED` | NO | `` |  |
| `api_config_id` | `BIGINT UNSIGNED` | NO | `` |  |
| `campus_id` | `BIGINT UNSIGNED` | YES | `` | NULL = global quota |
| `campus_scope_key` | `VARCHAR(36)` | NO | `'GLOBAL'` |  |
| `period_yyyymm` | `CHAR(6)` | NO | `` | YYYYMM |
| `monthly_limit` | `INT UNSIGNED` | NO | `` |  |
| `used_count` | `INT UNSIGNED` | NO | `0` | Merged api_usage_counters table |
| `last_used_at` | `DATETIME` | YES | `` |  |
| `created_at` | `DATETIME` | NO | `CURRENT_TIMESTAMP` |  |
| `created_by` | `BIGINT UNSIGNED` | YES | `` |  |
| `updated_at` | `DATETIME` | YES | `NULL ON UPDATE CURRENT_TIMESTAMP` |  |
| `updated_by` | `BIGINT UNSIGNED` | YES | `` |  |

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

### 4.39. `api_request_logs`

**Columns:**

| Column | Type | Null | Default | Notes |
|---|---|---:|---|---|
| `api_request_log_id` | `BIGINT UNSIGNED` | NO | `` |  |
| `api_config_id` | `BIGINT UNSIGNED` | NO | `` |  |
| `campus_id` | `BIGINT UNSIGNED` | YES | `` |  |
| `requested_by` | `BIGINT UNSIGNED` | YES | `` |  |
| `related_type` | `VARCHAR(80)` | YES | `` |  |
| `related_id` | `BIGINT UNSIGNED` | YES | `` |  |
| `endpoint` | `VARCHAR(500)` | NO | `` |  |
| `method` | `ENUM('GET','POST','PUT','PATCH','DELETE')` | NO | `` |  |
| `http_status` | `INT` | YES | `` |  |
| `response_time_ms` | `INT UNSIGNED` | YES | `` |  |
| `request_size_bytes` | `BIGINT UNSIGNED` | YES | `` |  |
| `response_size_bytes` | `BIGINT UNSIGNED` | YES | `` |  |
| `success` | `BOOLEAN` | NO | `FALSE` |  |
| `error_code` | `VARCHAR(100)` | YES | `` |  |
| `error_message` | `TEXT` | YES | `` |  |
| `created_at` | `DATETIME` | NO | `CURRENT_TIMESTAMP` |  |

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

### 4.40. `agenda_templates`

**Columns:**

| Column | Type | Null | Default | Notes |
|---|---|---:|---|---|
| `agenda_template_id` | `BIGINT UNSIGNED` | NO | `` |  |
| `campus_id` | `BIGINT UNSIGNED` | YES | `` |  |
| `campus_scope_key` | `VARCHAR(36)` | NO | `'GLOBAL'` |  |
| `name` | `VARCHAR(150)` | NO | `` |  |
| `description` | `TEXT` | YES | `` |  |
| `items_json` | `JSON` | NO | `` | Merged agenda_template_items table |
| `status` | `ENUM('ACTIVE','INACTIVE')` | NO | `'ACTIVE'` |  |
| `created_at` | `DATETIME` | NO | `CURRENT_TIMESTAMP` |  |
| `created_by` | `BIGINT UNSIGNED` | YES | `` |  |
| `updated_at` | `DATETIME` | YES | `NULL ON UPDATE CURRENT_TIMESTAMP` |  |
| `updated_by` | `BIGINT UNSIGNED` | YES | `` |  |
| `deleted_at` | `DATETIME` | YES | `` |  |
| `deleted_by` | `BIGINT UNSIGNED` | YES | `` |  |

**Primary Key:**
- `PRIMARY KEY (agenda_template_id)`

**Unique Constraints:**
- `UNIQUE KEY uq_agenda_template_scope_name (campus_scope_key, name)`

**Indexes:**
- `KEY idx_agenda_templates_status (status)`
- `KEY idx_agenda_templates_campus_status (campus_id, status)`

**Foreign Keys:**
- `CONSTRAINT fk_agenda_templates_campus FOREIGN KEY (campus_id) REFERENCES campuses(campus_id) ON UPDATE CASCADE ON DELETE SET NULL`

### 4.41. `audit_logs`

**Columns:**

| Column | Type | Null | Default | Notes |
|---|---|---:|---|---|
| `audit_log_id` | `BIGINT UNSIGNED` | NO | `` |  |
| `actor_user_id` | `BIGINT UNSIGNED` | YES | `` |  |
| `campus_id` | `BIGINT UNSIGNED` | YES | `` |  |
| `action` | `VARCHAR(100)` | NO | `` |  |
| `entity_type` | `VARCHAR(100)` | NO | `` |  |
| `entity_id` | `BIGINT UNSIGNED` | YES | `` |  |
| `old_values_json` | `JSON` | YES | `` |  |
| `new_values_json` | `JSON` | YES | `` |  |
| `ip_address` | `VARCHAR(45)` | YES | `` |  |
| `user_agent` | `VARCHAR(500)` | YES | `` |  |
| `request_id` | `VARCHAR(100)` | YES | `` |  |
| `created_at` | `DATETIME` | NO | `CURRENT_TIMESTAMP` |  |

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

### 4.42. `visit_status_logs`

**Columns:**

| Column | Type | Null | Default | Notes |
|---|---|---:|---|---|
| `visit_status_log_id` | `BIGINT UNSIGNED` | NO | `` |  |
| `visit_request_id` | `BIGINT UNSIGNED` | YES | `` |  |
| `visit_instance_id` | `BIGINT UNSIGNED` | YES | `` |  |
| `status_owner_type` | `ENUM('REQUEST','CAMPUS_INSTANCE')` | NO | `'CAMPUS_INSTANCE'` | REQUEST=visit_requests.status, CAMPUS_INSTANCE=visit_request_campuses.status |
| `old_status` | `VARCHAR(50)` | YES | `` |  |
| `new_status` | `VARCHAR(50)` | NO | `` |  |
| `changed_by` | `BIGINT UNSIGNED` | YES | `` |  |
| `reason` | `TEXT` | YES | `` |  |
| `changed_at` | `DATETIME` | NO | `CURRENT_TIMESTAMP` |  |

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

## 5. Views and Trigger Notes

### Views
- `vw_visit_requests_for_ho`
- `vw_visit_requests_for_staff_leader`
- `vw_visit_requests_for_ho`
- `vw_visit_requests_for_staff_leader`
- `vw_visit_requests_for_admin`
- `vw_visit_request_progress_summary`

### Triggers
- `trg_departments_one_ic_bi`
- `trg_departments_one_ic_bu`
- `trg_users_validate_bi`
- `trg_users_validate_bu`
- `trg_auth_providers_validate_bi`
- `trg_auth_providers_validate_bu`
- `trg_sessions_validate_bi`
- `trg_visit_requests_decision_validate_bi`
- `trg_visit_requests_decision_validate_bu`
- `trg_visit_requests_cancel_validate_bu`
- `trg_visit_campuses_cancel_validate_bu`
- `trg_visit_campuses_assignment_validate_bi`
- `trg_visit_campuses_assignment_validate_bu`
- `trg_api_usage_quotas_scope_bi`
- `trg_api_usage_quotas_scope_bu`
- `trg_agenda_templates_scope_bi`
- `trg_agenda_templates_scope_bu`
- `trg_feedbacks_not_self_bi`
- `trg_feedbacks_not_self_bu`

## 6. Replacement Note
This file is intentionally full-length. It should replace shortened schema summaries when backend/entity/EF Core alignment needs table-level detail.
