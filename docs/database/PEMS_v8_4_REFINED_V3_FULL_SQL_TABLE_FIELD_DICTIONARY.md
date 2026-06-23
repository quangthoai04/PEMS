# PEMS v8.4 Refined V3 — FULL SQL TABLE & FIELD DICTIONARY

Tài liệu trình bày đầy đủ các bảng SQL, tác dụng, màn hình sử dụng và toàn bộ trường dữ liệu theo file SQL mới nhất đã cập nhật.

- Source SQL: `pems_full_seed_logic_v8_4_refined_v3.sql`
- Tổng số bảng parse từ SQL: **50**
- Tổng số cột parse từ SQL: **729**
- Ghi chú: Ý nghĩa trường ưu tiên COMMENT trong SQL; nếu cột không có COMMENT, tài liệu bổ sung mô tả nghiệp vụ theo tên trường, khóa, enum/check và module PEMS.

## 1. Tổng quan các bảng

| # | Bảng | Số cột | Tác dụng chính | Màn hình/UC liên quan |
|---:|---|---:|---|---|
| 1 | `roles` | 8 | Danh mục vai trò gốc của hệ thống. | Role Management, Account Management, Login/RBAC, Permission Matrix |
| 2 | `permissions` | 7 | Danh mục quyền theo UC/action. | Permission Matrix, Role Management, menu/action authorization |
| 3 | `role_permissions` | 7 | Ma trận phân quyền theo role, sub-role và permission. | Permission Matrix, Role Management, API authorization, frontend route/action guard |
| 4 | `campuses` | 13 | Danh mục cơ sở FPTU và thông tin hành chính của từng campus. | Campus Management, Internal Login campus selection, Visit Request campus scope |
| 5 | `departments` | 10 | Danh mục phòng ban theo campus, tách IC và phòng ban general. | Department Management, User Management, logistics/participant assignment |
| 6 | `users` | 25 | Tài khoản người dùng nội bộ, student và visitor. | Login, Profile, Account Management, RBAC, Visit Workflow |
| 7 | `user_auth_providers` | 8 | Nguồn đăng nhập của user: Google SSO, FEID hoặc local password dev/test. | Login, SSO linking, DEV/test login |
| 8 | `user_sessions` | 15 | Phiên đăng nhập, refresh token hash và trạng thái thu hồi session. | Login, Logout, token refresh, session monitoring |
| 9 | `otp_tokens` | 14 | OTP/magic link phục vụ xác minh email hoặc hành động nhạy cảm. | Submit Visit Request public, OTP verification, account verification |
| 10 | `login_logs` | 12 | Nhật ký đăng nhập phục vụ audit và debug đăng nhập. | Login monitoring, security audit |
| 11 | `security_events` | 15 | Sự kiện bảo mật SSO-only và vòng đời session. | Internal/Visitor Portal login, SSO validation, security audit |
| 12 | `files` | 14 | Metadata file lưu ngoài DB; DB chỉ lưu provider/path/link. | Document, Gallery, News, Partner logo/cover, Minutes, Logistics attachments |
| 13 | `partners` | 24 | Hồ sơ đối tác và trạng thái hiển thị/duyệt hồ sơ. | Partner Management, Public Partners, Partner Detail, Partner Creation Request |
| 14 | `partner_contacts` | 17 | Người liên hệ của đối tác, bao gồm dữ liệu từ business card OCR. | Partner Contact Management, Scan Business Card |
| 15 | `documents` | 14 | Tài liệu nghiệp vụ dùng chung theo owner_type. | Document List/Search, Visit/Partner/Minutes/News/Logistics documents |
| 16 | `visit_requests` | 43 | Đơn đăng ký tham quan/công tác cấp tổng. | Public Submit Visit Request, Internal Create Guest Delegation, Delegation List/Detail, Approval/Cancel |
| 17 | `visit_request_campuses` | 26 | Instance theo từng campus trong một visit request. | HO approval, Staff Leader campus processing, Campus visit lifecycle |
| 18 | `visit_guest_members` | 12 | Danh sách khách và external support của đoàn. | Visitor List, Support Team, Delegation Detail, Meeting Minutes participant source |
| 19 | `visit_participants` | 16 | Người nội bộ tham gia xử lý/đón tiếp một campus visit instance. | Host Assignment, Participant Confirmation, Staff/Department task assignment |
| 20 | `visit_agendas` | 13 | Lịch trình cụ thể của visit instance. | Agenda Planning, Delegation Detail, Visit Logistics preparation |
| 21 | `visit_logistics_items` | 45 | Yêu cầu hậu cần/resource cho visit. | Prepare Visit Logistics, Update Logistics, Approve Resource Request, Propose Modification |
| 22 | `minutes` | 15 | Biên bản làm việc của chuyến thăm. | Create/Edit/View Meeting Minutes, Close Delegation |
| 23 | `minute_participants` | 12 | Danh sách người tham gia biên bản dạng snapshot và điểm danh. | Create/Edit Meeting Minutes, View Minutes Detail, Attendance checklist |
| 24 | `minute_action_items` | 12 | Đầu việc phát sinh sau meeting minutes. | Meeting Minutes, follow-up tracking |
| 25 | `feedbacks` | 16 | Feedback tổng giữa các bên trong một visit. | Submit Delegation Feedback, Feedback Summary |
| 26 | `feedback_rating_items` | 7 | Điểm feedback theo từng tiêu chí. | Feedback Form, Feedback Analytics/Summary |
| 27 | `news` | 17 | Metadata bài news và workflow duyệt/xuất bản. | News Management, Public News, Visit story/news from visit |
| 28 | `news_translations` | 11 | Nội dung tiêu đề/tóm tắt/SEO theo ngôn ngữ. | Create/Edit News, Public News multilingual display, AI translation output |
| 29 | `news_content_sections` | 9 | Các section nội dung chi tiết của bài news. | News Editor, Public News Detail |
| 30 | `news_section_files` | 6 | File/ảnh gắn vào từng section bài news. | News Editor media management |
| 31 | `faqs` | 12 | FAQ một ngôn ngữ với trạng thái hiển thị công khai. | FAQ Management, Public FAQ, Search FAQ |
| 32 | `galleries` | 18 | Gallery địa điểm/khu vực trong campus. | Gallery Management, Public Gallery, Visit FPTU |
| 33 | `gallery_images` | 15 | Ảnh/video thuộc gallery. | Gallery Item List, Add/Update Gallery Item, Public Gallery |
| 34 | `photo_face_tags` | 17 | Metadata tag khuôn mặt đã xác nhận, không lưu vector sinh trắc. | Tag Faces on Photos, Visit Photos, Gallery moderation |
| 35 | `email_templates` | 16 | Mẫu email với nội dung VI/EN explicit. | Email Template List/Detail/Create/Update |
| 36 | `sent_emails` | 16 | Lịch sử gửi email tổng. | Send Email, Sent Email List/Detail, Email delivery tracking |
| 37 | `sent_email_recipients` | 12 | Người nhận của từng email đã gửi. | Sent Email Detail, Email delivery tracking per recipient |
| 38 | `notifications` | 10 | Thông báo in-app tới user. | Notification Center, dashboard alerts |
| 39 | `calendar_events` | 22 | Sự kiện lịch cá nhân/visit/logistics/deadline. | View My Events, Department Calendar, Personal Event CRUD |
| 40 | `calendar_event_attendees` | 8 | Người tham dự sự kiện lịch. | Calendar Event Detail, attendee response tracking |
| 41 | `calendar_event_reminders` | 8 | Nhắc lịch của sự kiện. | Calendar reminder scheduling |
| 42 | `api_configurations` | 33 | Cấu hình API external, credential explicit và kiểm thử kết nối. | API Management, View API Configuration, Test API Connection |
| 43 | `api_configuration_headers` | 6 | Header request API tách khỏi JSON. | API Configuration Detail, Test API Connection |
| 44 | `api_usage_quotas` | 12 | Quota và counter API theo campus/tháng. | API Management, quota monitoring |
| 45 | `api_request_logs` | 16 | Log request gọi API external. | API Logs, integration debugging |
| 46 | `agenda_templates` | 12 | Header mẫu agenda theo campus/scope. | Agenda Template Management, Create Guest Delegation |
| 47 | `agenda_template_items` | 8 | Các dòng timeline trong mẫu agenda. | Agenda Template Detail, Delegation Agenda generation |
| 48 | `audit_logs` | 10 | Audit log tổng cho hành động nghiệp vụ. | Admin Audit Log, system audit trail |
| 49 | `audit_log_changes` | 6 | Chi tiết thay đổi từng field của audit log. | Audit Log Detail, field-level diff |
| 50 | `visit_status_logs` | 9 | Timeline trạng thái của request hoặc campus instance. | Delegation Detail, status timeline, audit of workflow transitions |

## 2. Các thay đổi schema chính trong bản Refined V3

- `departments.department_code` đã bị loại bỏ; phòng ban được quản lý theo `department_id`, `campus_id`, `name`, `department_type`.
- `visit_request_campuses.instance_code` đã bị loại bỏ; campus instance được xác định bằng `visit_instance_id` hoặc cặp `visit_request_id + campus_id`.
- `visit_requests.expected_guest_count` đã bị loại bỏ; số lượng khách được đếm từ `visit_guest_members` với `member_type = GUEST`.
- `visit_requests.interpreter_note` đã bị loại bỏ; hệ thống chỉ giữ `working_language` với `VI/EN` và UI hướng dẫn khách tự chuẩn bị phiên dịch nếu cần ngôn ngữ khác.
- `visit_guest_members` đã gọn lại theo bảng nhập khách/team hỗ trợ: giữ họ tên, đơn vị, chức vụ/phòng ban, quốc tịch; bỏ email, phone, is_representative, note.
- `minute_participants` bổ sung các trường điểm danh: `attendance_status`, `attendance_note`, `checked_at`, `checked_by`.
- `minutes.finalized_by` và `minutes.finalized_at` đã bị loại bỏ; thay bằng nhóm edit lock: `edit_locked_by`, `edit_locked_at`, `edit_lock_expires_at`, `edit_lock_token`, `row_version`.
- `minutes.status` đổi sang `DRAFT/SAVED`; trạng thái khóa sửa cuối cùng dựa trên `visit_request_campuses.status = CLOSED`.
- `news_translations.language_code` đổi từ ENUM cố định sang `VARCHAR(20)` để hỗ trợ dropdown/config linh động và AI translation.

## 3. Chi tiết từng bảng và trường dữ liệu

### 3.1. `roles`

- **Tác dụng:** Danh mục vai trò gốc của hệ thống.
- **Màn hình/UC dùng:** Role Management, Account Management, Login/RBAC, Permission Matrix
- **Quan hệ chính:** Không có FOREIGN KEY trực tiếp trong CREATE TABLE.

| Trường | Loại dữ liệu | Key / Null / Default | Enum / Check | Ý nghĩa / Vì sao có trường này |
|---|---|---|---|---|
| `role_id` | `BIGINT UNSIGNED` | PK; NOT NULL; AUTO_INCREMENT |  | Khóa chính định danh duy nhất bản ghi trong bảng. |
| `role_code` | `VARCHAR(30)` | UNIQUE: uq_roles_code; NOT NULL | CHECK: role_code IN ('ADMIN','HO','STAFF','DEPARTMENT','STUDENT','VISITOR') | ADMIN, HO, STAFF, DEPARTMENT, STUDENT, VISITOR |
| `name` | `VARCHAR(100)` | NOT NULL |  | Tên hiển thị/chính thức của bản ghi, dùng trên danh sách, dropdown và màn chi tiết. |
| `description` | `VARCHAR(255)` | NULL |  | Mô tả chi tiết, giúp người dùng hiểu nội dung bản ghi. |
| `status` | `ENUM('ACTIVE','INACTIVE')` | IDX: idx_roles_status_deleted; NOT NULL; DEFAULT 'ACTIVE' | ENUM: ('ACTIVE','INACTIVE') | Trạng thái vòng đời của bản ghi, dùng cho filter, hiển thị badge và chặn/hạn chế thao tác. |
| `created_at` | `DATETIME` | NOT NULL; DEFAULT CURRENT_TIMESTAMP |  | Thời điểm tạo bản ghi, cần cho audit, sắp xếp và truy vết lịch sử. |
| `deleted_at` | `DATETIME` | IDX: idx_roles_status_deleted; NULL |  | Soft delete supported by UC-121 Disable/Delete Role |
| `deleted_by` | `BIGINT UNSIGNED` | NULL |  | User who soft-deleted this role; no FK here because roles is created before users |

### 3.2. `permissions`

- **Tác dụng:** Danh mục quyền theo UC/action.
- **Màn hình/UC dùng:** Permission Matrix, Role Management, menu/action authorization
- **Quan hệ chính:** Không có FOREIGN KEY trực tiếp trong CREATE TABLE.

| Trường | Loại dữ liệu | Key / Null / Default | Enum / Check | Ý nghĩa / Vì sao có trường này |
|---|---|---|---|---|
| `permission_id` | `BIGINT UNSIGNED` | PK; NOT NULL; AUTO_INCREMENT |  | Khóa chính định danh duy nhất bản ghi trong bảng. |
| `permission_code` | `VARCHAR(100)` | UNIQUE: uq_permissions_code; IDX: idx_permissions_group_code; NOT NULL |  | Example: UC-17.SUBMIT_VISIT_REQUEST |
| `name` | `VARCHAR(150)` | NOT NULL |  | Tên hiển thị/chính thức của bản ghi, dùng trên danh sách, dropdown và màn chi tiết. |
| `permission_group` | `VARCHAR(60)` | IDX: idx_permissions_group, idx_permissions_group_code; NOT NULL |  | Lưu thông tin `permission group` của bảng `permissions` để phục vụ màn hình và logic nghiệp vụ liên quan. |
| `description` | `VARCHAR(500)` | NULL |  | Mô tả chi tiết, giúp người dùng hiểu nội dung bản ghi. |
| `is_system` | `BOOLEAN` | NOT NULL; DEFAULT FALSE |  | Cờ boolean dùng để bật/tắt hoặc đánh dấu một trạng thái nghiệp vụ. |
| `created_at` | `DATETIME` | NOT NULL; DEFAULT CURRENT_TIMESTAMP |  | Thời điểm tạo bản ghi, cần cho audit, sắp xếp và truy vết lịch sử. |

### 3.3. `role_permissions`

- **Tác dụng:** Ma trận phân quyền theo role, sub-role và permission.
- **Màn hình/UC dùng:** Permission Matrix, Role Management, API authorization, frontend route/action guard
- **Quan hệ chính:** role_id -> roles(role_id); permission_id -> permissions(permission_id)

| Trường | Loại dữ liệu | Key / Null / Default | Enum / Check | Ý nghĩa / Vì sao có trường này |
|---|---|---|---|---|
| `role_permission_id` | `BIGINT UNSIGNED` | PK; NOT NULL; AUTO_INCREMENT |  | Khóa chính định danh duy nhất bản ghi trong bảng. |
| `role_id` | `BIGINT UNSIGNED` | UNIQUE: uq_role_permissions_role_sub_permission; IDX: idx_role_permissions_role_sub_role; FK: role_id -> roles(role_id) [fk_role_permissions_role]; NOT NULL |  | Khóa ngoại liên kết tới roles(role_id), dùng để join dữ liệu và đảm bảo toàn vẹn tham chiếu. |
| `sub_role` | `ENUM('NONE','LEADER','STAFF')` | UNIQUE: uq_role_permissions_role_sub_permission; IDX: idx_role_permissions_role_sub_role; NOT NULL; DEFAULT 'NONE' | ENUM: ('NONE','LEADER','STAFF') | NONE for ADMIN/HO/STUDENT/VISITOR; LEADER/STAFF for STAFF and DEPARTMENT |
| `permission_id` | `BIGINT UNSIGNED` | UNIQUE: uq_role_permissions_role_sub_permission; IDX: idx_role_permissions_permission; FK: permission_id -> permissions(permission_id) [fk_role_permissions_permission]; NOT NULL |  | Khóa ngoại liên kết tới permissions(permission_id), dùng để join dữ liệu và đảm bảo toàn vẹn tham chiếu. |
| `permission_level` | `ENUM('F','E','R','O')` | NOT NULL | ENUM: ('F','E','R','O') | F=Full, E=Execute/Edit, R=Read, O=Own |
| `granted_at` | `DATETIME` | NOT NULL; DEFAULT CURRENT_TIMESTAMP |  | Mốc thời gian nghiệp vụ, phục vụ audit, filter, timeline hoặc SLA. |
| `granted_by` | `BIGINT UNSIGNED` | NULL |  | User thực hiện hành động liên quan, phục vụ audit và phân quyền. |

### 3.4. `campuses`

- **Tác dụng:** Danh mục cơ sở FPTU và thông tin hành chính của từng campus.
- **Màn hình/UC dùng:** Campus Management, Internal Login campus selection, Visit Request campus scope
- **Quan hệ chính:** Không có FOREIGN KEY trực tiếp trong CREATE TABLE.

| Trường | Loại dữ liệu | Key / Null / Default | Enum / Check | Ý nghĩa / Vì sao có trường này |
|---|---|---|---|---|
| `campus_id` | `BIGINT UNSIGNED` | PK; NOT NULL; AUTO_INCREMENT |  | Khóa chính định danh duy nhất bản ghi trong bảng. |
| `campus_code` | `VARCHAR(20)` | UNIQUE: uq_campuses_code; NOT NULL |  | HN, HCM, DN, CT, QN |
| `name` | `VARCHAR(150)` | NOT NULL |  | Tên hiển thị/chính thức của bản ghi, dùng trên danh sách, dropdown và màn chi tiết. |
| `city` | `VARCHAR(100)` | IDX: idx_campuses_city_status; NULL |  | Thành phố/khu vực địa lý, dùng hiển thị và filter. |
| `address` | `VARCHAR(255)` | NULL |  | Địa chỉ chi tiết, phục vụ hiển thị và liên hệ. |
| `phone` | `VARCHAR(30)` | NULL |  | Số điện thoại liên hệ, hỗ trợ trao đổi nghiệp vụ. |
| `email` | `VARCHAR(150)` | NULL |  | Email liên hệ/đăng nhập/thông báo, cần validate định dạng và gửi thông báo. |
| `ic_head_user_id` | `BIGINT UNSIGNED` | IDX: idx_campuses_ic_head; NULL |  | FK added after users table |
| `status` | `ENUM('ACTIVE','INACTIVE')` | IDX: idx_campuses_status, idx_campuses_city_status; NOT NULL; DEFAULT 'ACTIVE' | ENUM: ('ACTIVE','INACTIVE') | Trạng thái vòng đời của bản ghi, dùng cho filter, hiển thị badge và chặn/hạn chế thao tác. |
| `created_at` | `DATETIME` | NOT NULL; DEFAULT CURRENT_TIMESTAMP |  | Thời điểm tạo bản ghi, cần cho audit, sắp xếp và truy vết lịch sử. |
| `created_by` | `BIGINT UNSIGNED` | NULL |  | User tạo bản ghi, dùng cho ownership, audit và phân quyền theo người tạo. |
| `updated_at` | `DATETIME` | NULL; DEFAULT NULL; ON UPDATE CURRENT_TIMESTAMP |  | Thời điểm cập nhật gần nhất, giúp UI/backend xác định dữ liệu mới nhất. |
| `updated_by` | `BIGINT UNSIGNED` | NULL |  | User cập nhật bản ghi gần nhất, phục vụ audit và kiểm tra thay đổi. |

### 3.5. `departments`

- **Tác dụng:** Danh mục phòng ban theo campus, tách IC và phòng ban general.
- **Màn hình/UC dùng:** Department Management, User Management, logistics/participant assignment
- **Quan hệ chính:** campus_id -> campuses(campus_id)

| Trường | Loại dữ liệu | Key / Null / Default | Enum / Check | Ý nghĩa / Vì sao có trường này |
|---|---|---|---|---|
| `department_id` | `BIGINT UNSIGNED` | PK; NOT NULL; AUTO_INCREMENT |  | Khóa chính định danh duy nhất bản ghi trong bảng. |
| `campus_id` | `BIGINT UNSIGNED` | UNIQUE: uq_departments_campus_name; IDX: idx_departments_campus_type; FK: campus_id -> campuses(campus_id) [fk_departments_campus]; NOT NULL |  | Khóa ngoại liên kết tới campuses(campus_id), dùng để join dữ liệu và đảm bảo toàn vẹn tham chiếu. |
| `name` | `VARCHAR(150)` | UNIQUE: uq_departments_campus_name; NOT NULL |  | Tên hiển thị/chính thức của bản ghi, dùng trên danh sách, dropdown và màn chi tiết. |
| `department_type` | `ENUM('IC','GENERAL')` | IDX: idx_departments_campus_type; NOT NULL | ENUM: ('IC','GENERAL') | IC=International Cooperation; GENERAL=other departments |
| `head_user_id` | `BIGINT UNSIGNED` | IDX: idx_departments_head; NULL |  | FK added after users table |
| `status` | `ENUM('ACTIVE','INACTIVE')` | IDX: idx_departments_status; NOT NULL; DEFAULT 'ACTIVE' | ENUM: ('ACTIVE','INACTIVE') | Trạng thái vòng đời của bản ghi, dùng cho filter, hiển thị badge và chặn/hạn chế thao tác. |
| `created_at` | `DATETIME` | NOT NULL; DEFAULT CURRENT_TIMESTAMP |  | Thời điểm tạo bản ghi, cần cho audit, sắp xếp và truy vết lịch sử. |
| `created_by` | `BIGINT UNSIGNED` | NULL |  | User tạo bản ghi, dùng cho ownership, audit và phân quyền theo người tạo. |
| `updated_at` | `DATETIME` | NULL; DEFAULT NULL; ON UPDATE CURRENT_TIMESTAMP |  | Thời điểm cập nhật gần nhất, giúp UI/backend xác định dữ liệu mới nhất. |
| `updated_by` | `BIGINT UNSIGNED` | NULL |  | User cập nhật bản ghi gần nhất, phục vụ audit và kiểm tra thay đổi. |

### 3.6. `users`

- **Tác dụng:** Tài khoản người dùng nội bộ, student và visitor.
- **Màn hình/UC dùng:** Login, Profile, Account Management, RBAC, Visit Workflow
- **Quan hệ chính:** role_id -> roles(role_id); primary_campus_id -> campuses(campus_id); department_id -> departments(department_id)

| Trường | Loại dữ liệu | Key / Null / Default | Enum / Check | Ý nghĩa / Vì sao có trường này |
|---|---|---|---|---|
| `user_id` | `BIGINT UNSIGNED` | PK; NOT NULL; AUTO_INCREMENT |  | Khóa chính định danh duy nhất bản ghi trong bảng. |
| `full_name` | `VARCHAR(150)` | NOT NULL |  | Họ và tên đầy đủ của người liên quan. |
| `email` | `VARCHAR(150)` | UNIQUE: uq_users_email; IDX: idx_users_email_status; NOT NULL |  | Email liên hệ/đăng nhập/thông báo, cần validate định dạng và gửi thông báo. |
| `phone` | `VARCHAR(30)` | NULL |  | Số điện thoại liên hệ, hỗ trợ trao đổi nghiệp vụ. |
| `nationality` | `VARCHAR(100)` | IDX: idx_users_nationality; NULL |  | Quốc tịch của user/visitor |
| `password_hash` | `VARCHAR(255)` | NULL |  | DEV/local password hash only. Production SSO-only accounts keep this NULL. |
| `role_id` | `BIGINT UNSIGNED` | IDX: idx_users_role_sub_role, idx_users_campus_role_status; FK: role_id -> roles(role_id) [fk_users_role]; NOT NULL |  | Khóa ngoại liên kết tới roles(role_id), dùng để join dữ liệu và đảm bảo toàn vẹn tham chiếu. |
| `sub_role` | `ENUM('LEADER','STAFF')` | IDX: idx_users_role_sub_role; NULL | ENUM: ('LEADER','STAFF') | Only for STAFF/DEPARTMENT |
| `primary_campus_id` | `BIGINT UNSIGNED` | IDX: idx_users_primary_campus, idx_users_campus_role_status; FK: primary_campus_id -> campuses(campus_id) [fk_users_primary_campus]; NULL |  | Campus duy nhất của user nội bộ. VISITOR phải NULL. |
| `department_id` | `BIGINT UNSIGNED` | IDX: idx_users_department, idx_users_department_status; FK: department_id -> departments(department_id) [fk_users_department]; NULL |  | STAFF = IC department; DEPARTMENT = GENERAL department |
| `gender` | `ENUM('MALE','FEMALE','OTHER','UNKNOWN')` | NULL | ENUM: ('MALE','FEMALE','OTHER','UNKNOWN') | Lưu thông tin `gender` của bảng `users` để phục vụ màn hình và logic nghiệp vụ liên quan. |
| `avatar_url` | `VARCHAR(500)` | NULL |  | Lưu thông tin `avatar url` của bảng `users` để phục vụ màn hình và logic nghiệp vụ liên quan. |
| `student_code` | `VARCHAR(30)` | UNIQUE: uq_users_student_code; NULL |  | Mã nghiệp vụ/technical code ổn định, dùng cho tìm kiếm, mapping và tránh phụ thuộc tên hiển thị. |
| `fe_id` | `VARCHAR(100)` | UNIQUE: uq_users_fe_id; NULL |  | Mã định danh bản ghi liên quan, dùng cho join, phân quyền hoặc truy xuất nghiệp vụ. |
| `status` | `ENUM('ACTIVE','INACTIVE','LOCKED')` | IDX: idx_users_status, idx_users_email_status, idx_users_campus_role_status, idx_users_department_status; NOT NULL; DEFAULT 'ACTIVE' | ENUM: ('ACTIVE','INACTIVE','LOCKED') | ACTIVE=hoạt động, INACTIVE=tạm ngưng, LOCKED=bị khóa |
| `email_verified_at` | `DATETIME` | NULL |  | Thời điểm email được xác thực qua SSO lần đầu hoặc xác nhận bởi hệ thống |
| `failed_login_count` | `INT UNSIGNED` | NOT NULL; DEFAULT 0 |  | Số lần đăng nhập sai local password liên tiếp; reset khi login thành công |
| `locked_until` | `DATETIME` | NULL |  | Thời điểm hết khóa tạm thời nếu bị lock |
| `created_via` | `ENUM('MANUAL_CREATED','VISITOR_FORM','SSO_AUTO_PROVISION')` | IDX: idx_users_created_via; NOT NULL; DEFAULT 'MANUAL_CREATED' | ENUM: ('MANUAL_CREATED','VISITOR_FORM','SSO_AUTO_PROVISION') | MANUAL_CREATED=HO/Staff Leader tạo, VISITOR_FORM=tạo từ form visitor, SSO_AUTO_PROVISION=tạo tự động khi đăng nhập SSO ở cổng Visitor |
| `first_login_at` | `DATETIME` | NULL |  | Mốc thời gian nghiệp vụ, phục vụ audit, filter, timeline hoặc SLA. |
| `last_login_at` | `DATETIME` | IDX: idx_users_last_login; NULL |  | Mốc thời gian nghiệp vụ, phục vụ audit, filter, timeline hoặc SLA. |
| `created_at` | `DATETIME` | NOT NULL; DEFAULT CURRENT_TIMESTAMP |  | Thời điểm tạo bản ghi, cần cho audit, sắp xếp và truy vết lịch sử. |
| `created_by` | `BIGINT UNSIGNED` | NULL |  | User tạo bản ghi, dùng cho ownership, audit và phân quyền theo người tạo. |
| `updated_at` | `DATETIME` | NULL; DEFAULT NULL; ON UPDATE CURRENT_TIMESTAMP |  | Thời điểm cập nhật gần nhất, giúp UI/backend xác định dữ liệu mới nhất. |
| `updated_by` | `BIGINT UNSIGNED` | NULL |  | User cập nhật bản ghi gần nhất, phục vụ audit và kiểm tra thay đổi. |

### 3.7. `user_auth_providers`

- **Tác dụng:** Nguồn đăng nhập của user: Google SSO, FEID hoặc local password dev/test.
- **Màn hình/UC dùng:** Login, SSO linking, DEV/test login
- **Quan hệ chính:** user_id -> users(user_id)

| Trường | Loại dữ liệu | Key / Null / Default | Enum / Check | Ý nghĩa / Vì sao có trường này |
|---|---|---|---|---|
| `auth_provider_id` | `BIGINT UNSIGNED` | PK; NOT NULL; AUTO_INCREMENT |  | Khóa chính định danh duy nhất bản ghi trong bảng. |
| `user_id` | `BIGINT UNSIGNED` | UNIQUE: uq_user_auth_provider_type; FK: user_id -> users(user_id) [fk_auth_providers_user]; NOT NULL |  | Khóa ngoại liên kết tới users(user_id), dùng để join dữ liệu và đảm bảo toàn vẹn tham chiếu. |
| `provider_type` | `ENUM('LOCAL_PASSWORD','GOOGLE_SSO','FEID')` | UNIQUE: uq_user_auth_provider_type, uq_auth_provider_subject; IDX: idx_auth_provider_type_email_enabled; NOT NULL | ENUM: ('LOCAL_PASSWORD','GOOGLE_SSO','FEID') | Loại nghiệp vụ của bản ghi, dùng để phân nhánh xử lý và filter UI. |
| `provider_subject` | `VARCHAR(255)` | UNIQUE: uq_auth_provider_subject; NULL |  | Required for GOOGLE_SSO/FEID |
| `provider_email` | `VARCHAR(150)` | IDX: idx_auth_provider_email, idx_auth_provider_type_email_enabled; NULL |  | Lưu thông tin `provider email` của bảng `user_auth_providers` để phục vụ màn hình và logic nghiệp vụ liên quan. |
| `is_enabled` | `BOOLEAN` | IDX: idx_auth_provider_type_email_enabled; NOT NULL; DEFAULT TRUE |  | Cờ boolean dùng để bật/tắt hoặc đánh dấu một trạng thái nghiệp vụ. |
| `linked_at` | `DATETIME` | NOT NULL; DEFAULT CURRENT_TIMESTAMP |  | Mốc thời gian nghiệp vụ, phục vụ audit, filter, timeline hoặc SLA. |
| `last_used_at` | `DATETIME` | NULL |  | Mốc thời gian nghiệp vụ, phục vụ audit, filter, timeline hoặc SLA. |

### 3.8. `user_sessions`

- **Tác dụng:** Phiên đăng nhập, refresh token hash và trạng thái thu hồi session.
- **Màn hình/UC dùng:** Login, Logout, token refresh, session monitoring
- **Quan hệ chính:** user_id -> users(user_id); selected_campus_id -> campuses(campus_id); auth_provider_id -> user_auth_providers(auth_provider_id); revoked_by -> users(user_id)

| Trường | Loại dữ liệu | Key / Null / Default | Enum / Check | Ý nghĩa / Vì sao có trường này |
|---|---|---|---|---|
| `session_id` | `BIGINT UNSIGNED` | PK; NOT NULL; AUTO_INCREMENT |  | Khóa chính định danh duy nhất bản ghi trong bảng. |
| `user_id` | `BIGINT UNSIGNED` | IDX: idx_sessions_user_active; FK: user_id -> users(user_id) [fk_sessions_user]; NOT NULL |  | Khóa ngoại liên kết tới users(user_id), dùng để join dữ liệu và đảm bảo toàn vẹn tham chiếu. |
| `login_portal` | `ENUM('VISITOR','INTERNAL')` | IDX: idx_sessions_portal_campus; NOT NULL | ENUM: ('VISITOR','INTERNAL') | Lưu thông tin `login portal` của bảng `user_sessions` để phục vụ màn hình và logic nghiệp vụ liên quan. |
| `selected_campus_id` | `BIGINT UNSIGNED` | IDX: idx_sessions_portal_campus; FK: selected_campus_id -> campuses(campus_id) [fk_sessions_selected_campus]; NULL |  | Auto set to users.primary_campus_id for INTERNAL, NULL for VISITOR |
| `auth_provider_id` | `BIGINT UNSIGNED` | FK: auth_provider_id -> user_auth_providers(auth_provider_id) [fk_sessions_auth_provider]; NULL |  | Khóa ngoại liên kết tới user_auth_providers(auth_provider_id), dùng để join dữ liệu và đảm bảo toàn vẹn tham chiếu. |
| `refresh_token_hash` | `VARCHAR(255)` | UNIQUE: uq_sessions_refresh_hash; IDX: idx_sessions_refresh_active; NULL |  | Refresh token hash merged into session |
| `refresh_expires_at` | `DATETIME` | IDX: idx_sessions_refresh_active; NULL |  | Mốc thời gian nghiệp vụ, phục vụ audit, filter, timeline hoặc SLA. |
| `refresh_revoked_at` | `DATETIME` | IDX: idx_sessions_refresh_active; NULL |  | Mốc thời gian nghiệp vụ, phục vụ audit, filter, timeline hoặc SLA. |
| `ip_address` | `VARCHAR(45)` | IDX: idx_sessions_ip_time; NULL |  | Lưu thông tin `ip address` của bảng `user_sessions` để phục vụ màn hình và logic nghiệp vụ liên quan. |
| `user_agent` | `VARCHAR(500)` | NULL |  | Lưu thông tin `user agent` của bảng `user_sessions` để phục vụ màn hình và logic nghiệp vụ liên quan. |
| `created_at` | `DATETIME` | IDX: idx_sessions_ip_time; NOT NULL; DEFAULT CURRENT_TIMESTAMP |  | Thời điểm tạo bản ghi, cần cho audit, sắp xếp và truy vết lịch sử. |
| `expires_at` | `DATETIME` | IDX: idx_sessions_user_active, idx_sessions_expires_at; NOT NULL |  | Mốc thời gian nghiệp vụ, phục vụ audit, filter, timeline hoặc SLA. |
| `revoked_at` | `DATETIME` | IDX: idx_sessions_user_active, idx_sessions_revoked_at; NULL |  | Mốc thời gian nghiệp vụ, phục vụ audit, filter, timeline hoặc SLA. |
| `revoked_by` | `BIGINT UNSIGNED` | FK: revoked_by -> users(user_id) [fk_sessions_revoked_by]; NULL |  | Khóa ngoại liên kết tới users(user_id), dùng để join dữ liệu và đảm bảo toàn vẹn tham chiếu. |
| `revoked_reason` | `VARCHAR(255)` | NULL |  | Lưu thông tin `revoked reason` của bảng `user_sessions` để phục vụ màn hình và logic nghiệp vụ liên quan. |

### 3.9. `otp_tokens`

- **Tác dụng:** OTP/magic link phục vụ xác minh email hoặc hành động nhạy cảm.
- **Màn hình/UC dùng:** Submit Visit Request public, OTP verification, account verification
- **Quan hệ chính:** user_id -> users(user_id)

| Trường | Loại dữ liệu | Key / Null / Default | Enum / Check | Ý nghĩa / Vì sao có trường này |
|---|---|---|---|---|
| `otp_token_id` | `BIGINT UNSIGNED` | PK; NOT NULL; AUTO_INCREMENT |  | Khóa chính định danh duy nhất bản ghi trong bảng. |
| `user_id` | `BIGINT UNSIGNED` | IDX: idx_otp_user_purpose_active; FK: user_id -> users(user_id) [fk_otp_tokens_user]; NULL |  | Khóa ngoại liên kết tới users(user_id), dùng để join dữ liệu và đảm bảo toàn vẹn tham chiếu. |
| `email` | `VARCHAR(150)` | IDX: idx_otp_email_purpose_time, idx_otp_email_purpose_active; NOT NULL |  | Email liên hệ/đăng nhập/thông báo, cần validate định dạng và gửi thông báo. |
| `token_type` | `ENUM('OTP_CODE','MAGIC_LINK')` | NOT NULL; DEFAULT 'OTP_CODE' | ENUM: ('OTP_CODE','MAGIC_LINK') | Loại nghiệp vụ của bản ghi, dùng để phân nhánh xử lý và filter UI. |
| `purpose` | `ENUM('VISIT_REQUEST_VERIFY','CHANGE_SENSITIVE_ACTION')` | IDX: idx_otp_email_purpose_time, idx_otp_email_purpose_active, idx_otp_user_purpose_active; NOT NULL | ENUM: ('VISIT_REQUEST_VERIFY','CHANGE_SENSITIVE_ACTION') | Lưu thông tin `purpose` của bảng `otp_tokens` để phục vụ màn hình và logic nghiệp vụ liên quan. |
| `token_hash` | `VARCHAR(255)` | UNIQUE: uq_otp_tokens_hash; NOT NULL |  | Lưu thông tin `token hash` của bảng `otp_tokens` để phục vụ màn hình và logic nghiệp vụ liên quan. |
| `expires_at` | `DATETIME` | IDX: idx_otp_email_purpose_active, idx_otp_user_purpose_active; NOT NULL |  | Mốc thời gian nghiệp vụ, phục vụ audit, filter, timeline hoặc SLA. |
| `used_at` | `DATETIME` | IDX: idx_otp_email_purpose_active, idx_otp_user_purpose_active; NULL |  | Mốc thời gian nghiệp vụ, phục vụ audit, filter, timeline hoặc SLA. |
| `attempt_count` | `INT UNSIGNED` | NOT NULL; DEFAULT 0 |  | Lưu thông tin `attempt count` của bảng `otp_tokens` để phục vụ màn hình và logic nghiệp vụ liên quan. |
| `max_attempts` | `INT UNSIGNED` | NOT NULL; DEFAULT 5 |  | Lưu thông tin `max attempts` của bảng `otp_tokens` để phục vụ màn hình và logic nghiệp vụ liên quan. |
| `resend_count` | `INT UNSIGNED` | NOT NULL; DEFAULT 0 |  | Lưu thông tin `resend count` của bảng `otp_tokens` để phục vụ màn hình và logic nghiệp vụ liên quan. |
| `ip_address` | `VARCHAR(45)` | IDX: idx_otp_ip_time; NULL |  | Lưu thông tin `ip address` của bảng `otp_tokens` để phục vụ màn hình và logic nghiệp vụ liên quan. |
| `user_agent` | `VARCHAR(500)` | NULL |  | Lưu thông tin `user agent` của bảng `otp_tokens` để phục vụ màn hình và logic nghiệp vụ liên quan. |
| `created_at` | `DATETIME` | IDX: idx_otp_email_purpose_time, idx_otp_ip_time; NOT NULL; DEFAULT CURRENT_TIMESTAMP |  | Thời điểm tạo bản ghi, cần cho audit, sắp xếp và truy vết lịch sử. |

### 3.10. `login_logs`

- **Tác dụng:** Nhật ký đăng nhập phục vụ audit và debug đăng nhập.
- **Màn hình/UC dùng:** Login monitoring, security audit
- **Quan hệ chính:** user_id -> users(user_id); selected_campus_id -> campuses(campus_id)

| Trường | Loại dữ liệu | Key / Null / Default | Enum / Check | Ý nghĩa / Vì sao có trường này |
|---|---|---|---|---|
| `login_log_id` | `BIGINT UNSIGNED` | PK; NOT NULL; AUTO_INCREMENT |  | Khóa chính định danh duy nhất bản ghi trong bảng. |
| `user_id` | `BIGINT UNSIGNED` | IDX: idx_login_logs_user_time; FK: user_id -> users(user_id) [fk_login_logs_user]; NULL |  | Khóa ngoại liên kết tới users(user_id), dùng để join dữ liệu và đảm bảo toàn vẹn tham chiếu. |
| `email` | `VARCHAR(150)` | IDX: idx_login_logs_email_status_time; NOT NULL |  | Email liên hệ/đăng nhập/thông báo, cần validate định dạng và gửi thông báo. |
| `login_portal` | `ENUM('VISITOR','INTERNAL')` | IDX: idx_login_logs_portal_campus; NOT NULL | ENUM: ('VISITOR','INTERNAL') | Lưu thông tin `login portal` của bảng `login_logs` để phục vụ màn hình và logic nghiệp vụ liên quan. |
| `selected_campus_id` | `BIGINT UNSIGNED` | IDX: idx_login_logs_portal_campus; FK: selected_campus_id -> campuses(campus_id) [fk_login_logs_campus]; NULL |  | Khóa ngoại liên kết tới campuses(campus_id), dùng để join dữ liệu và đảm bảo toàn vẹn tham chiếu. |
| `provider_type` | `ENUM('LOCAL_PASSWORD','GOOGLE_SSO','FEID')` | IDX: idx_login_logs_provider_time; NULL | ENUM: ('LOCAL_PASSWORD','GOOGLE_SSO','FEID') | Loại nghiệp vụ của bản ghi, dùng để phân nhánh xử lý và filter UI. |
| `status` | `ENUM('SUCCESS','FAILED','BLOCKED')` | IDX: idx_login_logs_email_status_time, idx_login_logs_ip_status_time; NOT NULL | ENUM: ('SUCCESS','FAILED','BLOCKED') | Trạng thái vòng đời của bản ghi, dùng cho filter, hiển thị badge và chặn/hạn chế thao tác. |
| `failure_reason` | `VARCHAR(255)` | NULL |  | Lưu thông tin `failure reason` của bảng `login_logs` để phục vụ màn hình và logic nghiệp vụ liên quan. |
| `ip_address` | `VARCHAR(45)` | IDX: idx_login_logs_ip_status_time; NULL |  | Lưu thông tin `ip address` của bảng `login_logs` để phục vụ màn hình và logic nghiệp vụ liên quan. |
| `user_agent` | `VARCHAR(500)` | NULL |  | Lưu thông tin `user agent` của bảng `login_logs` để phục vụ màn hình và logic nghiệp vụ liên quan. |
| `session_id` | `BIGINT UNSIGNED` | NULL |  | Mã định danh bản ghi liên quan, dùng cho join, phân quyền hoặc truy xuất nghiệp vụ. |
| `created_at` | `DATETIME` | IDX: idx_login_logs_user_time, idx_login_logs_email_status_time, idx_login_logs_ip_status_time, idx_login_logs_provider_time; NOT NULL; DEFAULT CURRENT_TIMESTAMP |  | Thời điểm tạo bản ghi, cần cho audit, sắp xếp và truy vết lịch sử. |

### 3.11. `security_events`

- **Tác dụng:** Sự kiện bảo mật SSO-only và vòng đời session.
- **Màn hình/UC dùng:** Internal/Visitor Portal login, SSO validation, security audit
- **Quan hệ chính:** user_id -> users(user_id); selected_campus_id -> campuses(campus_id); session_id -> user_sessions(session_id)

| Trường | Loại dữ liệu | Key / Null / Default | Enum / Check | Ý nghĩa / Vì sao có trường này |
|---|---|---|---|---|
| `security_event_id` | `BIGINT UNSIGNED` | PK; NOT NULL; AUTO_INCREMENT |  | Khóa chính định danh duy nhất bản ghi trong bảng. |
| `user_id` | `BIGINT UNSIGNED` | IDX: idx_security_user_time; FK: user_id -> users(user_id) [fk_security_events_user]; NULL |  | Khóa ngoại liên kết tới users(user_id), dùng để join dữ liệu và đảm bảo toàn vẹn tham chiếu. |
| `email_snapshot` | `VARCHAR(150)` | IDX: idx_security_email_time; NULL |  | Email nhận từ SSO hoặc email đang được kiểm tra tại thời điểm xảy ra sự kiện |
| `event_type` | `ENUM(
    'SSO_LOGIN',
    'PORTAL_VALIDATION',
    'CAMPUS_VALIDATION',
    'VISITOR_AUTO_PROVISION',
    'SESSION_CREATED',
    'SESSION_REVOKED',
    'SESSION_EXPIRED',
    'TOKEN_REFRESH',
    'SECURITY_POLICY_CHECK'
  )` | IDX: idx_security_type_result_time; NOT NULL | ENUM: (
    'SSO_LOGIN',
    'PORTAL_VALIDATION',
    'CAMPUS_VALIDATION',
    'VISITOR_AUTO_PROVISION',
    'SESSION_CREATED',
    'SESSION_REVOKED',
    'SESSION_EXPIRED',
    'TOKEN_REFRESH',
    'SECURITY_POLICY_CHECK'
  ) | Loại sự kiện bảo mật theo mô hình SSO-only |
| `result` | `ENUM('SUCCESS','FAILED','BLOCKED')` | IDX: idx_security_type_result_time; NOT NULL; DEFAULT 'SUCCESS' | ENUM: ('SUCCESS','FAILED','BLOCKED') | Kết quả xử lý sự kiện |
| `failure_reason_code` | `ENUM(
    'ACCOUNT_NOT_FOUND',
    'ACCOUNT_DISABLED',
    'PORTAL_MISMATCH',
    'CAMPUS_MISMATCH',
    'ROLE_MISMATCH',
    'SSO_PROVIDER_ERROR',
    'INVALID_SSO_CLAIMS',
    'VISITOR_AUTO_PROVISION_DISABLED',
    'SESSION_EXPIRED',
    'TOKEN_REVOKED',
    'SUSPICIOUS_IP',
    'UNKNOWN'
  )` | IDX: idx_security_failure_reason_time; NULL | ENUM: (
    'ACCOUNT_NOT_FOUND',
    'ACCOUNT_DISABLED',
    'PORTAL_MISMATCH',
    'CAMPUS_MISMATCH',
    'ROLE_MISMATCH',
    'SSO_PROVIDER_ERROR',
    'INVALID_SSO_CLAIMS',
    'VISITOR_AUTO_PROVISION_DISABLED',
    'SESSION_EXPIRED',
    'TOKEN_REVOKED',
    'SUSPICIOUS_IP',
    'UNKNOWN'
  ) | Mã lý do thất bại/chặn; NULL khi SUCCESS |
| `severity` | `ENUM('LOW','MEDIUM','HIGH','CRITICAL')` | IDX: idx_security_severity_time; NOT NULL; DEFAULT 'LOW' | ENUM: ('LOW','MEDIUM','HIGH','CRITICAL') | Lưu thông tin `severity` của bảng `security_events` để phục vụ màn hình và logic nghiệp vụ liên quan. |
| `login_portal` | `ENUM('VISITOR','INTERNAL')` | IDX: idx_security_portal_campus_time; NULL | ENUM: ('VISITOR','INTERNAL') | Portal được dùng khi phát sinh sự kiện |
| `selected_campus_id` | `BIGINT UNSIGNED` | IDX: idx_security_portal_campus_time; FK: selected_campus_id -> campuses(campus_id) [fk_security_events_selected_campus]; NULL |  | Campus người dùng chọn ở Internal Portal; NULL với Visitor Portal |
| `provider_type` | `ENUM('GOOGLE_SSO','FEID')` | NULL | ENUM: ('GOOGLE_SSO','FEID') | Nguồn định danh SSO; không dùng LOCAL_PASSWORD trong security_events |
| `ip_address` | `VARCHAR(45)` | IDX: idx_security_ip_time; NULL |  | Lưu thông tin `ip address` của bảng `security_events` để phục vụ màn hình và logic nghiệp vụ liên quan. |
| `user_agent` | `VARCHAR(500)` | NULL |  | Lưu thông tin `user agent` của bảng `security_events` để phục vụ màn hình và logic nghiệp vụ liên quan. |
| `session_id` | `BIGINT UNSIGNED` | IDX: idx_security_session_time; FK: session_id -> user_sessions(session_id) [fk_security_events_session]; NULL |  | Khóa ngoại liên kết tới user_sessions(session_id), dùng để join dữ liệu và đảm bảo toàn vẹn tham chiếu. |
| `detail_text` | `TEXT` | NULL |  | Ghi chú debug ngắn, không lưu JSON metadata |
| `created_at` | `DATETIME` | IDX: idx_security_user_time, idx_security_email_time, idx_security_type_result_time, idx_security_portal_campus_time, idx_security_failure_reason_time, idx_security_ip_time, idx_security_severity_time, idx_security_session_time; NOT NULL; DEFAULT CURRENT_TIMESTAMP |  | Thời điểm tạo bản ghi, cần cho audit, sắp xếp và truy vết lịch sử. |

### 3.12. `files`

- **Tác dụng:** Metadata file lưu ngoài DB; DB chỉ lưu provider/path/link.
- **Màn hình/UC dùng:** Document, Gallery, News, Partner logo/cover, Minutes, Logistics attachments
- **Quan hệ chính:** uploaded_by -> users(user_id)

| Trường | Loại dữ liệu | Key / Null / Default | Enum / Check | Ý nghĩa / Vì sao có trường này |
|---|---|---|---|---|
| `file_id` | `BIGINT UNSIGNED` | PK; NOT NULL; AUTO_INCREMENT |  | Khóa chính định danh duy nhất bản ghi trong bảng. |
| `storage_provider` | `ENUM('LOCAL','S3','AZURE','GCS','GOOGLE_DRIVE','OTHER')` | NOT NULL; DEFAULT 'LOCAL' | ENUM: ('LOCAL','S3','AZURE','GCS','GOOGLE_DRIVE','OTHER') | Lưu thông tin `storage provider` của bảng `files` để phục vụ màn hình và logic nghiệp vụ liên quan. |
| `bucket_name` | `VARCHAR(150)` | NULL |  | Lưu thông tin `bucket name` của bảng `files` để phục vụ màn hình và logic nghiệp vụ liên quan. |
| `object_key` | `VARCHAR(700)` | UNIQUE: uq_files_object_key; NOT NULL |  | Max 700 chars to keep UNIQUE index safe under utf8mb4 |
| `original_filename` | `VARCHAR(255)` | NOT NULL |  | Lưu thông tin `original filename` của bảng `files` để phục vụ màn hình và logic nghiệp vụ liên quan. |
| `mime_type` | `VARCHAR(150)` | IDX: idx_files_mime_time; NULL |  | Loại nghiệp vụ của bản ghi, dùng để phân nhánh xử lý và filter UI. |
| `file_size` | `BIGINT UNSIGNED` | NULL |  | Lưu thông tin `file size` của bảng `files` để phục vụ màn hình và logic nghiệp vụ liên quan. |
| `uploaded_by` | `BIGINT UNSIGNED` | IDX: idx_files_uploaded_by; FK: uploaded_by -> users(user_id) [fk_files_uploaded_by]; NULL |  | Khóa ngoại liên kết tới users(user_id), dùng để join dữ liệu và đảm bảo toàn vẹn tham chiếu. |
| `uploaded_at` | `DATETIME` | IDX: idx_files_uploaded_by, idx_files_mime_time, idx_files_purpose_time; NOT NULL; DEFAULT CURRENT_TIMESTAMP |  | Mốc thời gian nghiệp vụ, phục vụ audit, filter, timeline hoặc SLA. |
| `external_file_id` | `VARCHAR(255)` | IDX: idx_files_external_file_id; NULL |  | External provider file id, e.g., Google Drive file id |
| `web_view_url` | `VARCHAR(700)` | NULL |  | Open/view URL from external storage provider |
| `download_url` | `VARCHAR(700)` | NULL |  | Direct download URL when provider allows it |
| `thumbnail_url` | `VARCHAR(700)` | NULL |  | Thumbnail URL for image/video preview |
| `file_purpose` | `VARCHAR(100)` | IDX: idx_files_purpose_time; NULL |  | Technical/business file purpose used by referencing entity |

### 3.13. `partners`

- **Tác dụng:** Hồ sơ đối tác và trạng thái hiển thị/duyệt hồ sơ.
- **Màn hình/UC dùng:** Partner Management, Public Partners, Partner Detail, Partner Creation Request
- **Quan hệ chính:** logo_file_id -> files(file_id); cover_file_id -> files(file_id); reviewed_by -> users(user_id)

| Trường | Loại dữ liệu | Key / Null / Default | Enum / Check | Ý nghĩa / Vì sao có trường này |
|---|---|---|---|---|
| `partner_id` | `BIGINT UNSIGNED` | PK; NOT NULL; AUTO_INCREMENT |  | Khóa chính định danh duy nhất bản ghi trong bảng. |
| `partner_code` | `VARCHAR(50)` | UNIQUE: uq_partners_code; NULL |  | Mã nghiệp vụ/technical code ổn định, dùng cho tìm kiếm, mapping và tránh phụ thuộc tên hiển thị. |
| `name` | `VARCHAR(200)` | NOT NULL |  | Tên hiển thị/chính thức của bản ghi, dùng trên danh sách, dropdown và màn chi tiết. |
| `short_name` | `VARCHAR(100)` | NULL |  | Lưu thông tin `short name` của bảng `partners` để phục vụ màn hình và logic nghiệp vụ liên quan. |
| `country` | `VARCHAR(100)` | IDX: idx_partners_country; NULL |  | Lưu thông tin `country` của bảng `partners` để phục vụ màn hình và logic nghiệp vụ liên quan. |
| `city` | `VARCHAR(100)` | NULL |  | Thành phố/khu vực địa lý, dùng hiển thị và filter. |
| `website_url` | `VARCHAR(500)` | NULL |  | Lưu thông tin `website url` của bảng `partners` để phục vụ màn hình và logic nghiệp vụ liên quan. |
| `partner_type` | `ENUM('UNIVERSITY','COMPANY','GOVERNMENT','NGO','OTHER')` | IDX: idx_partners_type_status; NOT NULL; DEFAULT 'UNIVERSITY' | ENUM: ('UNIVERSITY','COMPANY','GOVERNMENT','NGO','OTHER') | Loại nghiệp vụ của bản ghi, dùng để phân nhánh xử lý và filter UI. |
| `cooperation_status` | `ENUM('POTENTIAL','ACTIVE','INACTIVE','BLACKLISTED')` | IDX: idx_partners_status, idx_partners_type_status; NOT NULL; DEFAULT 'POTENTIAL' | ENUM: ('POTENTIAL','ACTIVE','INACTIVE','BLACKLISTED') | Trạng thái nghiệp vụ chi tiết, dùng để hiển thị badge, filter và kiểm soát luồng xử lý. |
| `description` | `TEXT` | NULL |  | Mô tả chi tiết, giúp người dùng hiểu nội dung bản ghi. |
| `logo_file_id` | `BIGINT UNSIGNED` | IDX: idx_partners_logo_file; FK: logo_file_id -> files(file_id) [fk_partners_logo_file]; NULL |  | Partner logo file, references files.file_id |
| `cover_file_id` | `BIGINT UNSIGNED` | IDX: idx_partners_cover_file; FK: cover_file_id -> files(file_id) [fk_partners_cover_file]; NULL |  | Partner cover/banner file, references files.file_id |
| `address` | `VARCHAR(500)` | NULL |  | Địa chỉ chi tiết, phục vụ hiển thị và liên hệ. |
| `public_slug` | `VARCHAR(180)` | UNIQUE: uq_partners_public_slug; NULL |  | Public URL slug for partner profile |
| `profile_status` | `ENUM('DRAFT','PENDING_APPROVAL','APPROVED','REJECTED')` | IDX: idx_partners_profile_status; NOT NULL; DEFAULT 'APPROVED' | ENUM: ('DRAFT','PENDING_APPROVAL','APPROVED','REJECTED') | Trạng thái nghiệp vụ chi tiết, dùng để hiển thị badge, filter và kiểm soát luồng xử lý. |
| `review_note` | `TEXT` | NULL |  | Lưu thông tin `review note` của bảng `partners` để phục vụ màn hình và logic nghiệp vụ liên quan. |
| `reviewed_by` | `BIGINT UNSIGNED` | IDX: idx_partners_reviewed_by; FK: reviewed_by -> users(user_id) [fk_partners_reviewed_by]; NULL |  | Khóa ngoại liên kết tới users(user_id), dùng để join dữ liệu và đảm bảo toàn vẹn tham chiếu. |
| `reviewed_at` | `DATETIME` | IDX: idx_partners_reviewed_by; NULL |  | Mốc thời gian nghiệp vụ, phục vụ audit, filter, timeline hoặc SLA. |
| `visibility` | `ENUM('PRIVATE','INTERNAL','PUBLIC')` | IDX: idx_partners_visibility; NOT NULL; DEFAULT 'PUBLIC' | ENUM: ('PRIVATE','INTERNAL','PUBLIC') | Lưu thông tin `visibility` của bảng `partners` để phục vụ màn hình và logic nghiệp vụ liên quan. |
| `created_at` | `DATETIME` | IDX: idx_partners_created_at; NOT NULL; DEFAULT CURRENT_TIMESTAMP |  | Thời điểm tạo bản ghi, cần cho audit, sắp xếp và truy vết lịch sử. |
| `created_by` | `BIGINT UNSIGNED` | NULL |  | User tạo bản ghi, dùng cho ownership, audit và phân quyền theo người tạo. |
| `updated_at` | `DATETIME` | NULL; DEFAULT NULL; ON UPDATE CURRENT_TIMESTAMP |  | Thời điểm cập nhật gần nhất, giúp UI/backend xác định dữ liệu mới nhất. |
| `updated_by` | `BIGINT UNSIGNED` | NULL |  | User cập nhật bản ghi gần nhất, phục vụ audit và kiểm tra thay đổi. |
| `FULLTEXT` | `KEY ft_partners_search (name, short_name, description)` | NULL |  | Lưu thông tin `FULLTEXT` của bảng `partners` để phục vụ màn hình và logic nghiệp vụ liên quan. |

### 3.14. `partner_contacts`

- **Tác dụng:** Người liên hệ của đối tác, bao gồm dữ liệu từ business card OCR.
- **Màn hình/UC dùng:** Partner Contact Management, Scan Business Card
- **Quan hệ chính:** partner_id -> partners(partner_id); scanned_card_file_id -> files(file_id)

| Trường | Loại dữ liệu | Key / Null / Default | Enum / Check | Ý nghĩa / Vì sao có trường này |
|---|---|---|---|---|
| `contact_id` | `BIGINT UNSIGNED` | PK; NOT NULL; AUTO_INCREMENT |  | Khóa chính định danh duy nhất bản ghi trong bảng. |
| `partner_id` | `BIGINT UNSIGNED` | UNIQUE: uq_partner_contacts_partner_email; IDX: idx_partner_contacts_partner; FK: partner_id -> partners(partner_id) [fk_partner_contacts_partner]; NOT NULL |  | Khóa ngoại liên kết tới partners(partner_id), dùng để join dữ liệu và đảm bảo toàn vẹn tham chiếu. |
| `full_name` | `VARCHAR(150)` | NOT NULL |  | Họ và tên đầy đủ của người liên quan. |
| `email` | `VARCHAR(150)` | UNIQUE: uq_partner_contacts_partner_email; IDX: idx_partner_contacts_email; NULL |  | Email liên hệ/đăng nhập/thông báo, cần validate định dạng và gửi thông báo. |
| `phone` | `VARCHAR(50)` | NULL |  | Số điện thoại liên hệ, hỗ trợ trao đổi nghiệp vụ. |
| `job_title` | `VARCHAR(150)` | NULL |  | Chức vụ hoặc phòng ban của người liên quan. |
| `department_name` | `VARCHAR(150)` | NULL |  | Lưu thông tin `department name` của bảng `partner_contacts` để phục vụ màn hình và logic nghiệp vụ liên quan. |
| `source_type` | `ENUM('MANUAL','BUSINESS_CARD_OCR','IMPORT')` | IDX: idx_partner_contacts_source_type; NOT NULL; DEFAULT 'MANUAL' | ENUM: ('MANUAL','BUSINESS_CARD_OCR','IMPORT') | Loại nghiệp vụ của bản ghi, dùng để phân nhánh xử lý và filter UI. |
| `scanned_card_file_id` | `BIGINT UNSIGNED` | IDX: idx_partner_contacts_scanned_card; FK: scanned_card_file_id -> files(file_id) [fk_partner_contacts_scanned_card]; NULL |  | Khóa ngoại liên kết tới files(file_id), dùng để join dữ liệu và đảm bảo toàn vẹn tham chiếu. |
| `ocr_confidence` | `DECIMAL(5,2)` | NULL |  | Lưu thông tin `ocr confidence` của bảng `partner_contacts` để phục vụ màn hình và logic nghiệp vụ liên quan. |
| `note` | `TEXT` | NULL |  | Ghi chú nghiệp vụ bổ sung. |
| `is_primary` | `BOOLEAN` | NOT NULL; DEFAULT FALSE |  | Cờ boolean dùng để bật/tắt hoặc đánh dấu một trạng thái nghiệp vụ. |
| `status` | `ENUM('ACTIVE','INACTIVE')` | IDX: idx_partner_contacts_status; NOT NULL; DEFAULT 'ACTIVE' | ENUM: ('ACTIVE','INACTIVE') | Trạng thái vòng đời của bản ghi, dùng cho filter, hiển thị badge và chặn/hạn chế thao tác. |
| `created_at` | `DATETIME` | NOT NULL; DEFAULT CURRENT_TIMESTAMP |  | Thời điểm tạo bản ghi, cần cho audit, sắp xếp và truy vết lịch sử. |
| `created_by` | `BIGINT UNSIGNED` | NULL |  | User tạo bản ghi, dùng cho ownership, audit và phân quyền theo người tạo. |
| `updated_at` | `DATETIME` | NULL; DEFAULT NULL; ON UPDATE CURRENT_TIMESTAMP |  | Thời điểm cập nhật gần nhất, giúp UI/backend xác định dữ liệu mới nhất. |
| `updated_by` | `BIGINT UNSIGNED` | NULL |  | User cập nhật bản ghi gần nhất, phục vụ audit và kiểm tra thay đổi. |

### 3.15. `documents`

- **Tác dụng:** Tài liệu nghiệp vụ dùng chung theo owner_type.
- **Màn hình/UC dùng:** Document List/Search, Visit/Partner/Minutes/News/Logistics documents
- **Quan hệ chính:** file_id -> files(file_id); campus_id -> campuses(campus_id); created_by -> users(user_id)

| Trường | Loại dữ liệu | Key / Null / Default | Enum / Check | Ý nghĩa / Vì sao có trường này |
|---|---|---|---|---|
| `document_id` | `BIGINT UNSIGNED` | PK; NOT NULL; AUTO_INCREMENT |  | Khóa chính định danh duy nhất bản ghi trong bảng. |
| `file_id` | `BIGINT UNSIGNED` | FK: file_id -> files(file_id) [fk_documents_file]; NOT NULL |  | Khóa ngoại liên kết tới files(file_id), dùng để join dữ liệu và đảm bảo toàn vẹn tham chiếu. |
| `owner_type` | `ENUM('GENERAL','VISIT','PARTNER','MINUTES','NEWS','LOGISTICS','REPORT')` | IDX: idx_documents_owner; NOT NULL; DEFAULT 'GENERAL' | ENUM: ('GENERAL','VISIT','PARTNER','MINUTES','NEWS','LOGISTICS','REPORT') | Loại nghiệp vụ của bản ghi, dùng để phân nhánh xử lý và filter UI. |
| `owner_id` | `BIGINT UNSIGNED` | IDX: idx_documents_owner; NULL |  | Mã định danh bản ghi liên quan, dùng cho join, phân quyền hoặc truy xuất nghiệp vụ. |
| `campus_id` | `BIGINT UNSIGNED` | IDX: idx_documents_campus_status; FK: campus_id -> campuses(campus_id) [fk_documents_campus]; NULL |  | Khóa ngoại liên kết tới campuses(campus_id), dùng để join dữ liệu và đảm bảo toàn vẹn tham chiếu. |
| `title` | `VARCHAR(255)` | NOT NULL |  | Tiêu đề hiển thị của bản ghi/nội dung. |
| `description` | `TEXT` | NULL |  | Mô tả chi tiết, giúp người dùng hiểu nội dung bản ghi. |
| `document_category` | `VARCHAR(100)` | IDX: idx_documents_category_status; NULL |  | Lưu thông tin `document category` của bảng `documents` để phục vụ màn hình và logic nghiệp vụ liên quan. |
| `status` | `ENUM('DRAFT','PUBLISHED','ARCHIVED')` | IDX: idx_documents_campus_status, idx_documents_category_status; NOT NULL; DEFAULT 'DRAFT' | ENUM: ('DRAFT','PUBLISHED','ARCHIVED') | Trạng thái vòng đời của bản ghi, dùng cho filter, hiển thị badge và chặn/hạn chế thao tác. |
| `created_at` | `DATETIME` | IDX: idx_documents_created_by_time; NOT NULL; DEFAULT CURRENT_TIMESTAMP |  | Thời điểm tạo bản ghi, cần cho audit, sắp xếp và truy vết lịch sử. |
| `created_by` | `BIGINT UNSIGNED` | IDX: idx_documents_created_by_time; FK: created_by -> users(user_id) [fk_documents_created_by]; NULL |  | Khóa ngoại liên kết tới users(user_id), dùng để join dữ liệu và đảm bảo toàn vẹn tham chiếu. |
| `updated_at` | `DATETIME` | NULL; DEFAULT NULL; ON UPDATE CURRENT_TIMESTAMP |  | Thời điểm cập nhật gần nhất, giúp UI/backend xác định dữ liệu mới nhất. |
| `updated_by` | `BIGINT UNSIGNED` | NULL |  | User cập nhật bản ghi gần nhất, phục vụ audit và kiểm tra thay đổi. |
| `FULLTEXT` | `KEY ft_documents_search (title, description)` | NULL |  | Lưu thông tin `FULLTEXT` của bảng `documents` để phục vụ màn hình và logic nghiệp vụ liên quan. |

### 3.16. `visit_requests`

- **Tác dụng:** Đơn đăng ký tham quan/công tác cấp tổng.
- **Màn hình/UC dùng:** Public Submit Visit Request, Internal Create Guest Delegation, Delegation List/Detail, Approval/Cancel
- **Quan hệ chính:** visitor_user_id -> users(user_id); partner_id -> partners(partner_id); decided_by -> users(user_id); cancelled_by -> users(user_id)

| Trường | Loại dữ liệu | Key / Null / Default | Enum / Check | Ý nghĩa / Vì sao có trường này |
|---|---|---|---|---|
| `visit_request_id` | `BIGINT UNSIGNED` | PK; NOT NULL; AUTO_INCREMENT |  | Khóa chính định danh duy nhất bản ghi trong bảng. |
| `request_code` | `VARCHAR(50)` | UNIQUE: uq_visit_requests_code; NOT NULL |  | Mã nghiệp vụ/technical code ổn định, dùng cho tìm kiếm, mapping và tránh phụ thuộc tên hiển thị. |
| `visitor_user_id` | `BIGINT UNSIGNED` | IDX: idx_visit_requests_visitor; FK: visitor_user_id -> users(user_id) [fk_visit_requests_visitor_user]; NULL |  | Visitor user/account created or linked for the registrant |
| `partner_id` | `BIGINT UNSIGNED` | IDX: idx_visit_requests_partner; FK: partner_id -> partners(partner_id) [fk_visit_requests_partner]; NULL |  | Khóa ngoại liên kết tới partners(partner_id), dùng để join dữ liệu và đảm bảo toàn vẹn tham chiếu. |
| `created_source` | `ENUM('VISITOR_SUBMITTED','STAFF_CREATED')` | IDX: idx_visit_requests_created_source; NOT NULL; DEFAULT 'VISITOR_SUBMITTED' | ENUM: ('VISITOR_SUBMITTED','STAFF_CREATED') | Lưu thông tin `created source` của bảng `visit_requests` để phục vụ màn hình và logic nghiệp vụ liên quan. |
| `registrant_organization` | `VARCHAR(200)` | NOT NULL |  | Đơn vị công tác người đăng ký |
| `registrant_job_title` | `VARCHAR(150)` | NOT NULL | CHECK: TRIM(registrant_job_title) <> '' | Chức danh/phòng ban người đăng ký |
| `registrant_phone` | `VARCHAR(50)` | NOT NULL | CHECK: TRIM(registrant_phone) <> '' | SĐT người đăng ký |
| `registrant_email` | `VARCHAR(150)` | IDX: idx_visit_requests_registrant_email; NOT NULL |  | Email người đăng ký |
| `registrant_nationality` | `VARCHAR(100)` | NOT NULL | CHECK: TRIM(registrant_nationality) <> '' | Quốc tịch người đăng ký |
| `visit_scope` | `ENUM('SINGLE_CAMPUS','MULTI_CAMPUS')` | IDX: idx_visit_requests_scope_status, idx_visit_requests_visibility_scope_status_decision; NOT NULL; DEFAULT 'SINGLE_CAMPUS' | ENUM: ('SINGLE_CAMPUS','MULTI_CAMPUS'); CHECK: decision_actor_role IS NULL OR status NOT IN ('APPROVED','REJECTED') OR ( visit_scope = 'SINGLE_CAMPUS' AND decision_actor_role IN ('STAFF_LEADER','SYSTEM') ) OR ( visit_scope = 'MULTI_CAMPUS' AND decision_actor_role IN ('HO','SYSTEM') ) | SINGLE_CAMPUS: Staff Leader duyệt request tổng; MULTI_CAMPUS: HO duyệt request tổng. Frontend/backend suy ra người duyệt từ cột này. |
| `visit_type` | `ENUM('CAMPUS_TOUR','MEETING','WORKSHOP','SIGNING_CEREMONY','EXCHANGE','OTHER')` | IDX: idx_visit_requests_visit_type; NOT NULL; DEFAULT 'CAMPUS_TOUR' | ENUM: ('CAMPUS_TOUR','MEETING','WORKSHOP','SIGNING_CEREMONY','EXCHANGE','OTHER') | Loại nghiệp vụ của bản ghi, dùng để phân nhánh xử lý và filter UI. |
| `visit_type_other` | `VARCHAR(255)` | NULL |  | Lưu thông tin `visit type other` của bảng `visit_requests` để phục vụ màn hình và logic nghiệp vụ liên quan. |
| `purpose` | `TEXT` | NOT NULL |  | Mục đích thăm FPTU |
| `working_content` | `TEXT` | NULL |  | Nội dung làm việc tại FPTU |
| `contact_person_full_name` | `VARCHAR(150)` | NOT NULL | CHECK: TRIM(contact_person_full_name) <> '' | Lưu thông tin `contact person full name` của bảng `visit_requests` để phục vụ màn hình và logic nghiệp vụ liên quan. |
| `contact_person_organization` | `VARCHAR(255)` | NOT NULL | CHECK: TRIM(contact_person_organization) <> '' | Lưu thông tin `contact person organization` của bảng `visit_requests` để phục vụ màn hình và logic nghiệp vụ liên quan. |
| `contact_person_phone` | `VARCHAR(50)` | NOT NULL | CHECK: TRIM(contact_person_phone) <> '' | Lưu thông tin `contact person phone` của bảng `visit_requests` để phục vụ màn hình và logic nghiệp vụ liên quan. |
| `contact_person_email` | `VARCHAR(150)` | IDX: idx_visit_requests_contact_email; NOT NULL | CHECK: TRIM(contact_person_email) <> '' | Lưu thông tin `contact person email` của bảng `visit_requests` để phục vụ màn hình và logic nghiệp vụ liên quan. |
| `working_language` | `ENUM('VI','EN')` | NOT NULL; DEFAULT 'EN' | ENUM: ('VI','EN') | Ngôn ngữ sử dụng trong visit. Chỉ dùng VI/EN theo frontend hiện tại, không có lựa chọn OTHER |
| `transportation_type` | `ENUM('SELF_ARRANGED','FPTU_SUPPORT','UNKNOWN','OTHER')` | NOT NULL; DEFAULT 'UNKNOWN' | ENUM: ('SELF_ARRANGED','FPTU_SUPPORT','UNKNOWN','OTHER') | Loại nghiệp vụ của bản ghi, dùng để phân nhánh xử lý và filter UI. |
| `transportation_detail` | `VARCHAR(500)` | NULL |  | Lưu thông tin `transportation detail` của bảng `visit_requests` để phục vụ màn hình và logic nghiệp vụ liên quan. |
| `media_consent_status` | `ENUM('AGREED','DECLINED','UNKNOWN')` | IDX: idx_visit_requests_media_consent; NOT NULL; DEFAULT 'UNKNOWN' | ENUM: ('AGREED','DECLINED','UNKNOWN') | Trạng thái nghiệp vụ chi tiết, dùng để hiển thị badge, filter và kiểm soát luồng xử lý. |
| `media_consent_note` | `TEXT` | NULL |  | Lưu thông tin `media consent note` của bảng `visit_requests` để phục vụ màn hình và logic nghiệp vụ liên quan. |
| `note_to_fptu` | `TEXT` | NULL |  | Ghi chú cho FPTU |
| `status` | `ENUM('PENDING_APPROVAL','APPROVED','REJECTED','CANCELLED')` | IDX: idx_visit_requests_status_submitted, idx_visit_requests_scope_status, idx_visit_requests_visibility_scope_status_decision; NOT NULL; DEFAULT 'PENDING_APPROVAL' | ENUM: ('PENDING_APPROVAL','APPROVED','REJECTED','CANCELLED'); CHECK: decision_actor_role IS NULL OR status NOT IN ('APPROVED','REJECTED') OR ( visit_scope = 'SINGLE_CAMPUS' AND decision_actor_role IN ('STAFF_LEADER','SYSTEM') ) OR ( visit_scope = 'MULTI_CAMPUS' AND decision_actor_role IN ('HO','SYSTEM') ) | Request decision status only. Visit progress is derived from visit_request_campuses.status |
| `submitted_at` | `DATETIME` | IDX: idx_visit_requests_status_submitted; NOT NULL; DEFAULT CURRENT_TIMESTAMP |  | Mốc thời gian nghiệp vụ, phục vụ audit, filter, timeline hoặc SLA. |
| `email_verified_at` | `DATETIME` | NULL |  | Mốc thời gian nghiệp vụ, phục vụ audit, filter, timeline hoặc SLA. |
| `decided_by` | `BIGINT UNSIGNED` | IDX: idx_visit_requests_decision; FK: decided_by -> users(user_id) [fk_visit_requests_decided_by]; NULL |  | Người approve/reject request tổng |
| `decided_at` | `DATETIME` | IDX: idx_visit_requests_visibility_scope_status_decision, idx_visit_requests_decision, idx_visit_requests_decision_role; NULL |  | Thời điểm xử lý request tổng |
| `decision_actor_role` | `ENUM('HO','STAFF_LEADER','SYSTEM')` | IDX: idx_visit_requests_visibility_scope_status_decision, idx_visit_requests_decision_role; NULL | ENUM: ('HO','STAFF_LEADER','SYSTEM'); CHECK: decision_actor_role IS NULL OR status NOT IN ('APPROVED','REJECTED') OR ( visit_scope = 'SINGLE_CAMPUS' AND decision_actor_role IN ('STAFF_LEADER','SYSTEM') ) OR ( visit_scope = 'MULTI_CAMPUS' AND decision_actor_role IN ('HO','SYSTEM') ) | Vai trò người xử lý tại thời điểm quyết định |
| `decision_note` | `TEXT` | NULL |  | Lý do/ghi chú khi approve hoặc reject |
| `cancelled_by` | `BIGINT UNSIGNED` | IDX: idx_visit_requests_cancelled; FK: cancelled_by -> users(user_id) [fk_visit_requests_cancelled_by]; NULL |  | Người thực hiện hủy request/delegation |
| `cancelled_at` | `DATETIME` | IDX: idx_visit_requests_cancelled, idx_visit_requests_cancel_actor; NULL |  | Thời điểm hủy request/delegation |
| `cancellation_actor_type` | `ENUM('VISITOR','HOST','STAFF_LEADER','HO','SYSTEM')` | IDX: idx_visit_requests_cancel_actor; NULL | ENUM: ('VISITOR','HOST','STAFF_LEADER','HO','SYSTEM') | Vai trò thực hiện thao tác hủy |
| `cancellation_source` | `ENUM('SELF_SERVICE','EXTERNAL_CONFIRMATION','INTERNAL_DECISION')` | NULL | ENUM: ('SELF_SERVICE','EXTERNAL_CONFIRMATION','INTERNAL_DECISION') | SELF_SERVICE=Visitor tự hủy sau khi đơn đã duyệt; EXTERNAL_CONFIRMATION=Host hủy sau khi khách xác nhận ngoài hệ thống |
| `cancellation_reason` | `TEXT` | NULL |  | Lý do hủy; nếu EXTERNAL_CONFIRMATION thì ghi rõ kênh xác nhận, thời điểm, người xác nhận và lý do. |
| `row_version` | `INT UNSIGNED` | NOT NULL; DEFAULT 0 |  | Optimistic concurrency token |
| `created_at` | `DATETIME` | NOT NULL; DEFAULT CURRENT_TIMESTAMP |  | Thời điểm tạo bản ghi, cần cho audit, sắp xếp và truy vết lịch sử. |
| `created_by` | `BIGINT UNSIGNED` | NULL |  | User tạo bản ghi, dùng cho ownership, audit và phân quyền theo người tạo. |
| `updated_at` | `DATETIME` | NULL; DEFAULT NULL; ON UPDATE CURRENT_TIMESTAMP |  | Thời điểm cập nhật gần nhất, giúp UI/backend xác định dữ liệu mới nhất. |
| `updated_by` | `BIGINT UNSIGNED` | NULL |  | User cập nhật bản ghi gần nhất, phục vụ audit và kiểm tra thay đổi. |
| `FULLTEXT` | `KEY ft_visit_requests_frontend_search (request_code, delegation_name, registrant_full_name, registrant_organization, registrant_email, contact_person_full_name, contact_person_organization, contact_person_email)` | NULL |  | Lưu thông tin `FULLTEXT` của bảng `visit_requests` để phục vụ màn hình và logic nghiệp vụ liên quan. |

### 3.17. `visit_request_campuses`

- **Tác dụng:** Instance theo từng campus trong một visit request.
- **Màn hình/UC dùng:** HO approval, Staff Leader campus processing, Campus visit lifecycle
- **Quan hệ chính:** visit_request_id -> visit_requests(visit_request_id); campus_id -> campuses(campus_id); current_host_user_id -> users(user_id); host_assigned_by -> users(user_id); host_transferred_by -> users(user_id); closed_by -> users(user_id); cancelled_by -> users(user_id)

| Trường | Loại dữ liệu | Key / Null / Default | Enum / Check | Ý nghĩa / Vì sao có trường này |
|---|---|---|---|---|
| `visit_instance_id` | `BIGINT UNSIGNED` | PK; NOT NULL; AUTO_INCREMENT |  | Khóa chính định danh duy nhất bản ghi trong bảng. |
| `visit_request_id` | `BIGINT UNSIGNED` | UNIQUE: uq_visit_instance_request_campus; IDX: idx_visit_instances_request, idx_visit_instances_visibility_campus_request; FK: visit_request_id -> visit_requests(visit_request_id) [fk_visit_instances_request]; NOT NULL |  | Khóa ngoại liên kết tới visit_requests(visit_request_id), dùng để join dữ liệu và đảm bảo toàn vẹn tham chiếu. |
| `campus_id` | `BIGINT UNSIGNED` | UNIQUE: uq_visit_instance_request_campus; IDX: idx_visit_instances_campus_status_time, idx_visit_instances_visibility_campus_request; FK: campus_id -> campuses(campus_id) [fk_visit_instances_campus]; NOT NULL |  | Khóa ngoại liên kết tới campuses(campus_id), dùng để join dữ liệu và đảm bảo toàn vẹn tham chiếu. |
| `planned_start_at` | `DATETIME` | IDX: idx_visit_instances_campus_status_time, idx_visit_instances_status_time; NOT NULL | CHECK: planned_end_at > planned_start_at | Ngày giờ bắt đầu dự kiến tại campus |
| `planned_end_at` | `DATETIME` | NOT NULL | CHECK: planned_end_at > planned_start_at | Ngày giờ kết thúc dự kiến tại campus |
| `status` | `ENUM(
    'WAITING_REQUEST_APPROVAL',
    'ASSIGNED',
    'BEFORE_VISIT',
    'DURING_VISIT',
    'AFTER_VISIT',
    'CLOSED',
    'CANCELLED'
  )` | IDX: idx_visit_instances_campus_status_time, idx_visit_instances_status_time, idx_visit_instances_current_host, idx_visit_instances_visibility_campus_request; NOT NULL; DEFAULT 'WAITING_REQUEST_APPROVAL' | ENUM: (
    'WAITING_REQUEST_APPROVAL',
    'ASSIGNED',
    'BEFORE_VISIT',
    'DURING_VISIT',
    'AFTER_VISIT',
    'CLOSED',
    'CANCELLED'
  ) | Trạng thái vòng đời của bản ghi, dùng cho filter, hiển thị badge và chặn/hạn chế thao tác. |
| `current_host_user_id` | `BIGINT UNSIGNED` | IDX: idx_visit_instances_current_host, idx_visit_instances_visibility_campus_request; FK: current_host_user_id -> users(user_id) [fk_visit_instances_current_host]; NULL |  | Host hiện tại chịu trách nhiệm campus instance. Sau khi request tổng được duyệt thì phải có host; nếu đổi host dùng chức năng Transfer Host |
| `host_assigned_by` | `BIGINT UNSIGNED` | IDX: idx_visit_instances_host_assigned; FK: host_assigned_by -> users(user_id) [fk_visit_instances_host_assigned_by]; NULL |  | Người gây ra thao tác gán host: HO khi auto gán Staff Leader cho multi-campus, Staff Leader khi duyệt single-campus, hoặc người chuyển host |
| `host_assigned_at` | `DATETIME` | IDX: idx_visit_instances_host_assigned, idx_visit_instances_assignment_source; NULL |  | Thời điểm host được gán |
| `host_assignment_source` | `ENUM('AUTO_STAFF_LEADER','MANUAL_APPROVAL','TRANSFERRED')` | IDX: idx_visit_instances_assignment_source; NULL | ENUM: ('AUTO_STAFF_LEADER','MANUAL_APPROVAL','TRANSFERRED') | AUTO_STAFF_LEADER=HO duyệt liên cơ sở và hệ thống tự gán Staff Leader; MANUAL_APPROVAL=Staff Leader duyệt đơn một cơ sở và chọn host; TRANSFERRED=host được chuyển sau đó |
| `host_transferred_by` | `BIGINT UNSIGNED` | IDX: idx_visit_instances_host_transfer; FK: host_transferred_by -> users(user_id) [fk_visit_instances_host_transferred_by]; NULL |  | Người chuyển host gần nhất |
| `host_transferred_at` | `DATETIME` | IDX: idx_visit_instances_host_transfer; NULL |  | Thời điểm chuyển host gần nhất |
| `host_transfer_note` | `TEXT` | NULL |  | Ghi chú/lý do chuyển host gần nhất |
| `closed_by` | `BIGINT UNSIGNED` | FK: closed_by -> users(user_id) [fk_visit_instances_closed_by]; NULL |  | Khóa ngoại liên kết tới users(user_id), dùng để join dữ liệu và đảm bảo toàn vẹn tham chiếu. |
| `closed_at` | `DATETIME` | NULL |  | Mốc thời gian nghiệp vụ, phục vụ audit, filter, timeline hoặc SLA. |
| `close_note` | `TEXT` | NULL |  | Lưu thông tin `close note` của bảng `visit_request_campuses` để phục vụ màn hình và logic nghiệp vụ liên quan. |
| `cancelled_by` | `BIGINT UNSIGNED` | IDX: idx_visit_instances_cancelled; FK: cancelled_by -> users(user_id) [fk_visit_instances_cancelled_by]; NULL |  | Người thực hiện hủy campus instance |
| `cancelled_at` | `DATETIME` | IDX: idx_visit_instances_cancelled, idx_visit_instances_cancel_actor; NULL |  | Thời điểm hủy campus instance |
| `cancellation_actor_type` | `ENUM('VISITOR','HOST','STAFF_LEADER','HO','SYSTEM')` | IDX: idx_visit_instances_cancel_actor; NULL | ENUM: ('VISITOR','HOST','STAFF_LEADER','HO','SYSTEM') | Vai trò thực hiện thao tác hủy campus instance |
| `cancellation_source` | `ENUM('SELF_SERVICE','EXTERNAL_CONFIRMATION','INTERNAL_DECISION')` | NULL | ENUM: ('SELF_SERVICE','EXTERNAL_CONFIRMATION','INTERNAL_DECISION') | SELF_SERVICE=Visitor tự hủy sau khi đơn đã duyệt; EXTERNAL_CONFIRMATION=Host hủy sau khi khách xác nhận ngoài hệ thống |
| `cancellation_reason` | `TEXT` | NULL |  | Lý do hủy; nếu EXTERNAL_CONFIRMATION thì ghi rõ kênh xác nhận, thời điểm, người xác nhận và lý do. |
| `row_version` | `INT UNSIGNED` | NOT NULL; DEFAULT 0 |  | Optimistic concurrency token |
| `created_at` | `DATETIME` | NOT NULL; DEFAULT CURRENT_TIMESTAMP |  | Thời điểm tạo bản ghi, cần cho audit, sắp xếp và truy vết lịch sử. |
| `created_by` | `BIGINT UNSIGNED` | NULL |  | User tạo bản ghi, dùng cho ownership, audit và phân quyền theo người tạo. |
| `updated_at` | `DATETIME` | NULL; DEFAULT NULL; ON UPDATE CURRENT_TIMESTAMP |  | Thời điểm cập nhật gần nhất, giúp UI/backend xác định dữ liệu mới nhất. |
| `updated_by` | `BIGINT UNSIGNED` | NULL |  | User cập nhật bản ghi gần nhất, phục vụ audit và kiểm tra thay đổi. |

### 3.18. `visit_guest_members`

- **Tác dụng:** Danh sách khách và external support của đoàn.
- **Màn hình/UC dùng:** Visitor List, Support Team, Delegation Detail, Meeting Minutes participant source
- **Quan hệ chính:** visit_request_id -> visit_requests(visit_request_id)

| Trường | Loại dữ liệu | Key / Null / Default | Enum / Check | Ý nghĩa / Vì sao có trường này |
|---|---|---|---|---|
| `guest_member_id` | `BIGINT UNSIGNED` | PK; NOT NULL; AUTO_INCREMENT |  | Khóa chính định danh duy nhất bản ghi trong bảng. |
| `visit_request_id` | `BIGINT UNSIGNED` | IDX: idx_guest_members_request, idx_guest_members_type_order; FK: visit_request_id -> visit_requests(visit_request_id) [fk_guest_members_request]; NOT NULL |  | Khóa ngoại liên kết tới visit_requests(visit_request_id), dùng để join dữ liệu và đảm bảo toàn vẹn tham chiếu. |
| `member_type` | `ENUM('GUEST','EXTERNAL_SUPPORT')` | IDX: idx_guest_members_type_order; NOT NULL; DEFAULT 'GUEST' | ENUM: ('GUEST','EXTERNAL_SUPPORT') | Loại nghiệp vụ của bản ghi, dùng để phân nhánh xử lý và filter UI. |
| `full_name` | `VARCHAR(150)` | NOT NULL | CHECK: TRIM(full_name) <> '' | Họ và tên đầy đủ của người liên quan. |
| `organization` | `VARCHAR(200)` | NOT NULL | CHECK: TRIM(organization) <> '' | Đơn vị công tác/tổ chức của người liên quan. |
| `job_title` | `VARCHAR(150)` | NOT NULL | CHECK: TRIM(job_title) <> '' | Chức vụ hoặc phòng ban của người liên quan. |
| `nationality` | `VARCHAR(100)` | NOT NULL | CHECK: TRIM(nationality) <> '' | Quốc tịch của người liên quan. |
| `display_order` | `INT UNSIGNED` | IDX: idx_guest_members_type_order; NOT NULL; DEFAULT 0 |  | Thứ tự hiển thị trên UI, giúp sắp xếp danh sách ổn định. |
| `created_at` | `DATETIME` | NOT NULL; DEFAULT CURRENT_TIMESTAMP |  | Thời điểm tạo bản ghi, cần cho audit, sắp xếp và truy vết lịch sử. |
| `created_by` | `BIGINT UNSIGNED` | NULL |  | User tạo bản ghi, dùng cho ownership, audit và phân quyền theo người tạo. |
| `updated_at` | `DATETIME` | NULL; DEFAULT NULL; ON UPDATE CURRENT_TIMESTAMP |  | Thời điểm cập nhật gần nhất, giúp UI/backend xác định dữ liệu mới nhất. |
| `updated_by` | `BIGINT UNSIGNED` | NULL |  | User cập nhật bản ghi gần nhất, phục vụ audit và kiểm tra thay đổi. |

### 3.19. `visit_participants`

- **Tác dụng:** Người nội bộ tham gia xử lý/đón tiếp một campus visit instance.
- **Màn hình/UC dùng:** Host Assignment, Participant Confirmation, Staff/Department task assignment
- **Quan hệ chính:** visit_instance_id -> visit_request_campuses(visit_instance_id); user_id -> users(user_id); invited_by -> users(user_id); assigned_by -> users(user_id)

| Trường | Loại dữ liệu | Key / Null / Default | Enum / Check | Ý nghĩa / Vì sao có trường này |
|---|---|---|---|---|
| `participant_id` | `BIGINT UNSIGNED` | PK; NOT NULL; AUTO_INCREMENT |  | Khóa chính định danh duy nhất bản ghi trong bảng. |
| `visit_instance_id` | `BIGINT UNSIGNED` | UNIQUE: uq_visit_participants_user; IDX: idx_visit_participants_one_host_lookup, idx_visit_participants_instance; FK: visit_instance_id -> visit_request_campuses(visit_instance_id) [fk_visit_participants_instance]; NOT NULL |  | Khóa ngoại liên kết tới visit_request_campuses(visit_instance_id), dùng để join dữ liệu và đảm bảo toàn vẹn tham chiếu. |
| `user_id` | `BIGINT UNSIGNED` | UNIQUE: uq_visit_participants_user; IDX: idx_visit_participants_user_status; FK: user_id -> users(user_id) [fk_visit_participants_user]; NOT NULL |  | Khóa ngoại liên kết tới users(user_id), dùng để join dữ liệu và đảm bảo toàn vẹn tham chiếu. |
| `participant_role` | `ENUM('IC_HOST','IC_SUPPORT','DEPT_SUPPORT','STUDENT')` | IDX: idx_visit_participants_role_status; NOT NULL; DEFAULT 'IC_SUPPORT' | ENUM: ('IC_HOST','IC_SUPPORT','DEPT_SUPPORT','STUDENT') | Lưu thông tin `participant role` của bảng `visit_participants` để phục vụ màn hình và logic nghiệp vụ liên quan. |
| `is_host` | `BOOLEAN` | IDX: idx_visit_participants_one_host_lookup; NOT NULL; DEFAULT FALSE |  | Cờ boolean dùng để bật/tắt hoặc đánh dấu một trạng thái nghiệp vụ. |
| `status` | `ENUM('INVITED','ACCEPTED','DECLINED','ASSIGNED','REMOVED')` | IDX: idx_visit_participants_user_status, idx_visit_participants_role_status; NOT NULL; DEFAULT 'INVITED' | ENUM: ('INVITED','ACCEPTED','DECLINED','ASSIGNED','REMOVED') | Trạng thái vòng đời của bản ghi, dùng cho filter, hiển thị badge và chặn/hạn chế thao tác. |
| `invited_by` | `BIGINT UNSIGNED` | FK: invited_by -> users(user_id) [fk_visit_participants_invited_by]; NULL |  | Khóa ngoại liên kết tới users(user_id), dùng để join dữ liệu và đảm bảo toàn vẹn tham chiếu. |
| `invited_at` | `DATETIME` | NULL |  | Mốc thời gian nghiệp vụ, phục vụ audit, filter, timeline hoặc SLA. |
| `responded_at` | `DATETIME` | NULL |  | Mốc thời gian nghiệp vụ, phục vụ audit, filter, timeline hoặc SLA. |
| `assigned_by` | `BIGINT UNSIGNED` | FK: assigned_by -> users(user_id) [fk_visit_participants_assigned_by]; NULL |  | Khóa ngoại liên kết tới users(user_id), dùng để join dữ liệu và đảm bảo toàn vẹn tham chiếu. |
| `assigned_at` | `DATETIME` | NULL |  | Mốc thời gian nghiệp vụ, phục vụ audit, filter, timeline hoặc SLA. |
| `note` | `TEXT` | NULL |  | Ghi chú nghiệp vụ bổ sung. |
| `created_at` | `DATETIME` | NOT NULL; DEFAULT CURRENT_TIMESTAMP |  | Thời điểm tạo bản ghi, cần cho audit, sắp xếp và truy vết lịch sử. |
| `created_by` | `BIGINT UNSIGNED` | NULL |  | User tạo bản ghi, dùng cho ownership, audit và phân quyền theo người tạo. |
| `updated_at` | `DATETIME` | NULL; DEFAULT NULL; ON UPDATE CURRENT_TIMESTAMP |  | Thời điểm cập nhật gần nhất, giúp UI/backend xác định dữ liệu mới nhất. |
| `updated_by` | `BIGINT UNSIGNED` | NULL |  | User cập nhật bản ghi gần nhất, phục vụ audit và kiểm tra thay đổi. |

### 3.20. `visit_agendas`

- **Tác dụng:** Lịch trình cụ thể của visit instance.
- **Màn hình/UC dùng:** Agenda Planning, Delegation Detail, Visit Logistics preparation
- **Quan hệ chính:** visit_instance_id -> visit_request_campuses(visit_instance_id); responsible_user_id -> users(user_id)

| Trường | Loại dữ liệu | Key / Null / Default | Enum / Check | Ý nghĩa / Vì sao có trường này |
|---|---|---|---|---|
| `agenda_id` | `BIGINT UNSIGNED` | PK; NOT NULL; AUTO_INCREMENT |  | Khóa chính định danh duy nhất bản ghi trong bảng. |
| `visit_instance_id` | `BIGINT UNSIGNED` | UNIQUE: uq_visit_agendas_order; IDX: idx_visit_agendas_time; FK: visit_instance_id -> visit_request_campuses(visit_instance_id) [fk_visit_agendas_instance]; NOT NULL |  | Khóa ngoại liên kết tới visit_request_campuses(visit_instance_id), dùng để join dữ liệu và đảm bảo toàn vẹn tham chiếu. |
| `sequence_order` | `INT UNSIGNED` | UNIQUE: uq_visit_agendas_order; NOT NULL |  | Lưu thông tin `sequence order` của bảng `visit_agendas` để phục vụ màn hình và logic nghiệp vụ liên quan. |
| `title` | `VARCHAR(255)` | NOT NULL |  | Tiêu đề hiển thị của bản ghi/nội dung. |
| `description` | `TEXT` | NULL |  | Mô tả chi tiết, giúp người dùng hiểu nội dung bản ghi. |
| `start_time` | `DATETIME` | IDX: idx_visit_agendas_time, idx_visit_agendas_responsible; NOT NULL |  | Lưu thông tin `start time` của bảng `visit_agendas` để phục vụ màn hình và logic nghiệp vụ liên quan. |
| `end_time` | `DATETIME` | NULL |  | Lưu thông tin `end time` của bảng `visit_agendas` để phục vụ màn hình và logic nghiệp vụ liên quan. |
| `location` | `VARCHAR(255)` | NULL |  | Lưu thông tin `location` của bảng `visit_agendas` để phục vụ màn hình và logic nghiệp vụ liên quan. |
| `responsible_user_id` | `BIGINT UNSIGNED` | IDX: idx_visit_agendas_responsible; FK: responsible_user_id -> users(user_id) [fk_visit_agendas_responsible_user]; NULL |  | Khóa ngoại liên kết tới users(user_id), dùng để join dữ liệu và đảm bảo toàn vẹn tham chiếu. |
| `created_at` | `DATETIME` | NOT NULL; DEFAULT CURRENT_TIMESTAMP |  | Thời điểm tạo bản ghi, cần cho audit, sắp xếp và truy vết lịch sử. |
| `created_by` | `BIGINT UNSIGNED` | NULL |  | User tạo bản ghi, dùng cho ownership, audit và phân quyền theo người tạo. |
| `updated_at` | `DATETIME` | NULL; DEFAULT NULL; ON UPDATE CURRENT_TIMESTAMP |  | Thời điểm cập nhật gần nhất, giúp UI/backend xác định dữ liệu mới nhất. |
| `updated_by` | `BIGINT UNSIGNED` | NULL |  | User cập nhật bản ghi gần nhất, phục vụ audit và kiểm tra thay đổi. |

### 3.21. `visit_logistics_items`

- **Tác dụng:** Yêu cầu hậu cần/resource cho visit.
- **Màn hình/UC dùng:** Prepare Visit Logistics, Update Logistics, Approve Resource Request, Propose Modification
- **Quan hệ chính:** visit_instance_id -> visit_request_campuses(visit_instance_id); requested_by -> users(user_id); requested_to_department_id -> departments(department_id); received_by -> users(user_id); assigned_to_user_id -> users(user_id); assigned_by -> users(user_id); proposed_by -> users(user_id); proposal_responded_by -> users(user_id); handover_confirmed_by -> users(user_id); service_report_signed_by -> users(user_id); service_report_file_id -> files(file_id)

| Trường | Loại dữ liệu | Key / Null / Default | Enum / Check | Ý nghĩa / Vì sao có trường này |
|---|---|---|---|---|
| `logistics_item_id` | `BIGINT UNSIGNED` | PK; NOT NULL; AUTO_INCREMENT |  | Khóa chính định danh duy nhất bản ghi trong bảng. |
| `visit_instance_id` | `BIGINT UNSIGNED` | IDX: idx_logistics_instance_status; FK: visit_instance_id -> visit_request_campuses(visit_instance_id) [fk_logistics_instance]; NOT NULL |  | Khóa ngoại liên kết tới visit_request_campuses(visit_instance_id), dùng để join dữ liệu và đảm bảo toàn vẹn tham chiếu. |
| `item_type` | `ENUM('ROOM','TRANSPORT','MEAL','EQUIPMENT','BANNER','LED','OTHER')` | IDX: idx_logistics_item_status; NOT NULL | ENUM: ('ROOM','TRANSPORT','MEAL','EQUIPMENT','BANNER','LED','OTHER') | Loại nghiệp vụ của bản ghi, dùng để phân nhánh xử lý và filter UI. |
| `title` | `VARCHAR(255)` | NOT NULL |  | Tiêu đề hiển thị của bản ghi/nội dung. |
| `description` | `TEXT` | NULL |  | Nội dung chi tiết công việc gốc |
| `quantity` | `INT UNSIGNED` | NULL | CHECK: quantity IS NULL OR quantity >= 1 | Số lượng yêu cầu gốc |
| `usage_start_at` | `DATETIME` | IDX: idx_logistics_usage_time; NULL | CHECK: usage_end_at IS NULL OR usage_start_at IS NULL OR usage_end_at > usage_start_at | Thời gian bắt đầu sử dụng resource |
| `usage_end_at` | `DATETIME` | IDX: idx_logistics_usage_time; NULL | CHECK: usage_end_at IS NULL OR usage_start_at IS NULL OR usage_end_at > usage_start_at | Thời gian kết thúc sử dụng resource |
| `status` | `ENUM(
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
  )` | IDX: idx_logistics_instance_status, idx_logistics_item_status, idx_logistics_department_status, idx_logistics_assignee_status; NOT NULL; DEFAULT 'PLANNED' | ENUM: (
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
  ) | Trạng thái vòng đời của bản ghi, dùng cho filter, hiển thị badge và chặn/hạn chế thao tác. |
| `priority` | `ENUM('LOW','MEDIUM','HIGH','URGENT')` | IDX: idx_logistics_priority_due; NOT NULL; DEFAULT 'MEDIUM' | ENUM: ('LOW','MEDIUM','HIGH','URGENT') | Lưu thông tin `priority` của bảng `visit_logistics_items` để phục vụ màn hình và logic nghiệp vụ liên quan. |
| `requested_by` | `BIGINT UNSIGNED` | IDX: idx_logistics_requested_by_time; FK: requested_by -> users(user_id) [fk_logistics_requested_by]; NULL |  | Người gửi yêu cầu hậu cần/resource |
| `requested_to_department_id` | `BIGINT UNSIGNED` | IDX: idx_logistics_department_status; FK: requested_to_department_id -> departments(department_id) [fk_logistics_requested_to_department]; NULL |  | Phòng ban được yêu cầu xử lý |
| `requested_at` | `DATETIME` | IDX: idx_logistics_requested_by_time; NULL |  | Thời điểm gửi yêu cầu |
| `received_by` | `BIGINT UNSIGNED` | IDX: idx_logistics_received_by_time; FK: received_by -> users(user_id) [fk_logistics_received_by]; NULL |  | Trưởng phòng/người tiếp nhận yêu cầu |
| `received_at` | `DATETIME` | IDX: idx_logistics_received_by_time; NULL |  | Thời điểm tiếp nhận yêu cầu |
| `assigned_to_user_id` | `BIGINT UNSIGNED` | IDX: idx_logistics_assignee_status; FK: assigned_to_user_id -> users(user_id) [fk_logistics_assigned_to]; NULL |  | Nhân viên được giao xử lý chính |
| `assigned_by` | `BIGINT UNSIGNED` | FK: assigned_by -> users(user_id) [fk_logistics_assigned_by]; NULL |  | Người phân công |
| `assigned_at` | `DATETIME` | NULL |  | Thời điểm phân công |
| `assignee_accepted_at` | `DATETIME` | NULL |  | Thời điểm nhân viên xác nhận nhận nhiệm vụ |
| `assignee_response_note` | `TEXT` | NULL |  | Ghi chú khi nhân viên nhận/từ chối nếu có |
| `due_at` | `DATETIME` | IDX: idx_logistics_due, idx_logistics_priority_due; NULL |  | Deadline hoàn thành hạng mục |
| `completed_at` | `DATETIME` | NULL |  | Thời điểm hoàn thành |
| `proposed_by` | `BIGINT UNSIGNED` | IDX: idx_logistics_proposed_by_time; FK: proposed_by -> users(user_id) [fk_logistics_proposed_by]; NULL |  | Người gửi đề xuất thay đổi |
| `proposed_at` | `DATETIME` | IDX: idx_logistics_proposed_by_time; NULL |  | Thời điểm gửi đề xuất thay đổi |
| `proposed_quantity` | `INT UNSIGNED` | NULL | CHECK: proposed_quantity IS NULL OR proposed_quantity >= 1 | Số lượng được đề xuất thay đổi |
| `proposed_usage_start_at` | `DATETIME` | NULL | CHECK: proposed_usage_end_at IS NULL OR proposed_usage_start_at IS NULL OR proposed_usage_end_at > proposed_usage_start_at | Thời gian bắt đầu sử dụng được đề xuất |
| `proposed_usage_end_at` | `DATETIME` | NULL | CHECK: proposed_usage_end_at IS NULL OR proposed_usage_start_at IS NULL OR proposed_usage_end_at > proposed_usage_start_at | Thời gian kết thúc sử dụng được đề xuất |
| `proposed_description` | `TEXT` | NULL |  | Nội dung chi tiết công việc được đề xuất thay đổi |
| `proposal_note` | `TEXT` | NULL |  | Lý do/ghi chú đề xuất thay đổi |
| `proposal_responded_by` | `BIGINT UNSIGNED` | FK: proposal_responded_by -> users(user_id) [fk_logistics_proposal_responded_by]; NULL |  | Người xác nhận/từ chối đề xuất |
| `proposal_responded_at` | `DATETIME` | NULL |  | Thời điểm xác nhận/từ chối đề xuất |
| `proposal_response` | `ENUM('ACCEPTED','REJECTED')` | NULL | ENUM: ('ACCEPTED','REJECTED') | Kết quả phản hồi đề xuất |
| `proposal_response_note` | `TEXT` | NULL |  | Ghi chú phản hồi đề xuất |
| `handover_confirmed_by` | `BIGINT UNSIGNED` | IDX: idx_logistics_handover; FK: handover_confirmed_by -> users(user_id) [fk_logistics_handover_by]; NULL |  | Khóa ngoại liên kết tới users(user_id), dùng để join dữ liệu và đảm bảo toàn vẹn tham chiếu. |
| `handover_confirmed_at` | `DATETIME` | IDX: idx_logistics_handover; NULL |  | Mốc thời gian nghiệp vụ, phục vụ audit, filter, timeline hoặc SLA. |
| `handover_note` | `TEXT` | NULL |  | Lưu thông tin `handover note` của bảng `visit_logistics_items` để phục vụ màn hình và logic nghiệp vụ liên quan. |
| `service_report_signed_by` | `BIGINT UNSIGNED` | IDX: idx_logistics_service_report; FK: service_report_signed_by -> users(user_id) [fk_logistics_service_report_signed_by]; NULL |  | Khóa ngoại liên kết tới users(user_id), dùng để join dữ liệu và đảm bảo toàn vẹn tham chiếu. |
| `service_report_signed_at` | `DATETIME` | IDX: idx_logistics_service_report; NULL |  | Mốc thời gian nghiệp vụ, phục vụ audit, filter, timeline hoặc SLA. |
| `service_report_file_id` | `BIGINT UNSIGNED` | IDX: idx_logistics_service_report_file; FK: service_report_file_id -> files(file_id) [fk_logistics_service_report_file]; NULL |  | Khóa ngoại liên kết tới files(file_id), dùng để join dữ liệu và đảm bảo toàn vẹn tham chiếu. |
| `decision_note` | `TEXT` | NULL |  | Lý do reject/cancel hoặc ghi chú xử lý |
| `row_version` | `INT UNSIGNED` | NOT NULL; DEFAULT 0 |  | Optimistic concurrency token |
| `created_at` | `DATETIME` | NOT NULL; DEFAULT CURRENT_TIMESTAMP |  | Thời điểm tạo bản ghi, cần cho audit, sắp xếp và truy vết lịch sử. |
| `created_by` | `BIGINT UNSIGNED` | NULL |  | User tạo bản ghi, dùng cho ownership, audit và phân quyền theo người tạo. |
| `updated_at` | `DATETIME` | NULL; DEFAULT NULL; ON UPDATE CURRENT_TIMESTAMP |  | Thời điểm cập nhật gần nhất, giúp UI/backend xác định dữ liệu mới nhất. |
| `updated_by` | `BIGINT UNSIGNED` | NULL |  | User cập nhật bản ghi gần nhất, phục vụ audit và kiểm tra thay đổi. |

### 3.22. `minutes`

- **Tác dụng:** Biên bản làm việc của chuyến thăm.
- **Màn hình/UC dùng:** Create/Edit/View Meeting Minutes, Close Delegation
- **Quan hệ chính:** visit_instance_id -> visit_request_campuses(visit_instance_id); created_by -> users(user_id); updated_by -> users(user_id); edit_locked_by -> users(user_id)

| Trường | Loại dữ liệu | Key / Null / Default | Enum / Check | Ý nghĩa / Vì sao có trường này |
|---|---|---|---|---|
| `minutes_id` | `BIGINT UNSIGNED` | PK; NOT NULL; AUTO_INCREMENT |  | Khóa chính định danh duy nhất bản ghi trong bảng. |
| `visit_instance_id` | `BIGINT UNSIGNED` | IDX: idx_minutes_visit_status; FK: visit_instance_id -> visit_request_campuses(visit_instance_id) [fk_minutes_visit_instance]; NOT NULL |  | Khóa ngoại liên kết tới visit_request_campuses(visit_instance_id), dùng để join dữ liệu và đảm bảo toàn vẹn tham chiếu. |
| `title` | `VARCHAR(255)` | NOT NULL |  | Tiêu đề hiển thị của bản ghi/nội dung. |
| `content` | `LONGTEXT` | NULL |  | Nội dung chính của bản ghi. |
| `status` | `ENUM('DRAFT','SAVED')` | IDX: idx_minutes_visit_status; NOT NULL; DEFAULT 'DRAFT' | ENUM: ('DRAFT','SAVED') | DRAFT=biên bản nháp, SAVED=đã lưu nội dung; quyền sửa bị khóa khi visit instance CLOSED |
| `edit_locked_by` | `BIGINT UNSIGNED` | IDX: idx_minutes_edit_lock; FK: edit_locked_by -> users(user_id) [fk_minutes_edit_locked_by]; NULL |  | User hiện đang giữ quyền sửa biên bản |
| `edit_locked_at` | `DATETIME` | NULL |  | Thời điểm bắt đầu giữ quyền sửa |
| `edit_lock_expires_at` | `DATETIME` | IDX: idx_minutes_edit_lock; NULL |  | Thời điểm lock sửa hết hạn |
| `edit_lock_token` | `CHAR(36)` | NULL |  | Token phiên sửa, dùng để xác nhận đúng người đang giữ lock |
| `row_version` | `INT UNSIGNED` | NOT NULL; DEFAULT 0 |  | Version chống ghi đè khi cập nhật đồng thời |
| `created_at` | `DATETIME` | IDX: idx_minutes_created_by_time; NOT NULL; DEFAULT CURRENT_TIMESTAMP |  | Thời điểm tạo bản ghi, cần cho audit, sắp xếp và truy vết lịch sử. |
| `created_by` | `BIGINT UNSIGNED` | IDX: idx_minutes_created_by_time; FK: created_by -> users(user_id) [fk_minutes_created_by]; NULL |  | Khóa ngoại liên kết tới users(user_id), dùng để join dữ liệu và đảm bảo toàn vẹn tham chiếu. |
| `updated_at` | `DATETIME` | NULL; DEFAULT NULL; ON UPDATE CURRENT_TIMESTAMP |  | Thời điểm cập nhật gần nhất, giúp UI/backend xác định dữ liệu mới nhất. |
| `updated_by` | `BIGINT UNSIGNED` | FK: updated_by -> users(user_id) [fk_minutes_updated_by]; NULL |  | Khóa ngoại liên kết tới users(user_id), dùng để join dữ liệu và đảm bảo toàn vẹn tham chiếu. |
| `FULLTEXT` | `KEY ft_minutes_search (title, content)` | NULL |  | Lưu thông tin `FULLTEXT` của bảng `minutes` để phục vụ màn hình và logic nghiệp vụ liên quan. |

### 3.23. `minute_participants`

- **Tác dụng:** Danh sách người tham gia biên bản dạng snapshot và điểm danh.
- **Màn hình/UC dùng:** Create/Edit Meeting Minutes, View Minutes Detail, Attendance checklist
- **Quan hệ chính:** minutes_id -> minutes(minutes_id); user_id -> users(user_id); guest_member_id -> visit_guest_members(guest_member_id); checked_by -> users(user_id)

| Trường | Loại dữ liệu | Key / Null / Default | Enum / Check | Ý nghĩa / Vì sao có trường này |
|---|---|---|---|---|
| `minute_participant_id` | `BIGINT UNSIGNED` | PK; NOT NULL; AUTO_INCREMENT |  | Khóa chính định danh duy nhất bản ghi trong bảng. |
| `minutes_id` | `BIGINT UNSIGNED` | IDX: idx_minute_participants_minutes_order, idx_minute_participants_attendance; FK: minutes_id -> minutes(minutes_id) [fk_minute_participants_minutes]; NOT NULL |  | Khóa ngoại liên kết tới minutes(minutes_id), dùng để join dữ liệu và đảm bảo toàn vẹn tham chiếu. |
| `user_id` | `BIGINT UNSIGNED` | IDX: idx_minute_participants_user; FK: user_id -> users(user_id) [fk_minute_participants_user]; NULL |  | Khóa ngoại liên kết tới users(user_id), dùng để join dữ liệu và đảm bảo toàn vẹn tham chiếu. |
| `guest_member_id` | `BIGINT UNSIGNED` | IDX: idx_minute_participants_guest_member; FK: guest_member_id -> visit_guest_members(guest_member_id) [fk_minute_participants_guest_member]; NULL |  | Khóa ngoại liên kết tới visit_guest_members(guest_member_id), dùng để join dữ liệu và đảm bảo toàn vẹn tham chiếu. |
| `full_name_snapshot` | `VARCHAR(255)` | NOT NULL |  | Lưu thông tin `full name snapshot` của bảng `minute_participants` để phục vụ màn hình và logic nghiệp vụ liên quan. |
| `role_snapshot` | `VARCHAR(120)` | NULL |  | Lưu thông tin `role snapshot` của bảng `minute_participants` để phục vụ màn hình và logic nghiệp vụ liên quan. |
| `organization_snapshot` | `VARCHAR(255)` | NULL |  | Lưu thông tin `organization snapshot` của bảng `minute_participants` để phục vụ màn hình và logic nghiệp vụ liên quan. |
| `email_snapshot` | `VARCHAR(150)` | NULL |  | Lưu thông tin `email snapshot` của bảng `minute_participants` để phục vụ màn hình và logic nghiệp vụ liên quan. |
| `attendance_status` | `ENUM('PRESENT','ABSENT','EXCUSED')` | IDX: idx_minute_participants_attendance; NOT NULL; DEFAULT 'PRESENT' | ENUM: ('PRESENT','ABSENT','EXCUSED') | PRESENT=có mặt, ABSENT=vắng mặt, EXCUSED=vắng có lý do |
| `attendance_note` | `TEXT` | NULL |  | Ghi chú điểm danh/lý do vắng nếu có |
| `display_order` | `INT UNSIGNED` | IDX: idx_minute_participants_minutes_order; NOT NULL; DEFAULT 0 |  | Thứ tự hiển thị trên UI, giúp sắp xếp danh sách ổn định. |
| `created_at` | `DATETIME` | NOT NULL; DEFAULT CURRENT_TIMESTAMP |  | Thời điểm tạo bản ghi, cần cho audit, sắp xếp và truy vết lịch sử. |

### 3.24. `minute_action_items`

- **Tác dụng:** Đầu việc phát sinh sau meeting minutes.
- **Màn hình/UC dùng:** Meeting Minutes, follow-up tracking
- **Quan hệ chính:** minutes_id -> minutes(minutes_id); created_by -> users(user_id); updated_by -> users(user_id)

| Trường | Loại dữ liệu | Key / Null / Default | Enum / Check | Ý nghĩa / Vì sao có trường này |
|---|---|---|---|---|
| `action_item_id` | `BIGINT UNSIGNED` | PK; NOT NULL; AUTO_INCREMENT |  | Khóa chính định danh duy nhất bản ghi trong bảng. |
| `minutes_id` | `BIGINT UNSIGNED` | IDX: idx_action_items_minutes, idx_action_items_order; FK: minutes_id -> minutes(minutes_id) [fk_action_items_minutes]; NOT NULL |  | Khóa ngoại liên kết tới minutes(minutes_id), dùng để join dữ liệu và đảm bảo toàn vẹn tham chiếu. |
| `title` | `VARCHAR(255)` | NOT NULL |  | Tên đầu việc |
| `note` | `TEXT` | NULL |  | Ghi chú thêm cho đầu việc |
| `due_date` | `DATE` | IDX: idx_action_items_status_due; NULL |  | Deadline của đầu việc |
| `status` | `ENUM('TODO','IN_PROGRESS','DONE','CANCELLED')` | IDX: idx_action_items_status_due; NOT NULL; DEFAULT 'TODO' | ENUM: ('TODO','IN_PROGRESS','DONE','CANCELLED') | TODO=chưa làm, IN_PROGRESS=đang làm, DONE=hoàn thành, CANCELLED=đã hủy/không cần làm nữa |
| `completed_at` | `DATETIME` | NULL |  | Thời điểm hoàn thành; backend tự set khi status chuyển sang DONE |
| `display_order` | `INT UNSIGNED` | IDX: idx_action_items_order; NOT NULL; DEFAULT 1 |  | Thứ tự hiển thị trong biên bản |
| `created_at` | `DATETIME` | IDX: idx_action_items_created_by_time; NOT NULL; DEFAULT CURRENT_TIMESTAMP |  | Thời điểm tạo bản ghi, cần cho audit, sắp xếp và truy vết lịch sử. |
| `created_by` | `BIGINT UNSIGNED` | IDX: idx_action_items_created_by_time; FK: created_by -> users(user_id) [fk_action_items_created_by]; NULL |  | Khóa ngoại liên kết tới users(user_id), dùng để join dữ liệu và đảm bảo toàn vẹn tham chiếu. |
| `updated_at` | `DATETIME` | NULL; DEFAULT NULL; ON UPDATE CURRENT_TIMESTAMP |  | Thời điểm cập nhật gần nhất, giúp UI/backend xác định dữ liệu mới nhất. |
| `updated_by` | `BIGINT UNSIGNED` | FK: updated_by -> users(user_id) [fk_action_items_updated_by]; NULL |  | Khóa ngoại liên kết tới users(user_id), dùng để join dữ liệu và đảm bảo toàn vẹn tham chiếu. |

### 3.25. `feedbacks`

- **Tác dụng:** Feedback tổng giữa các bên trong một visit.
- **Màn hình/UC dùng:** Submit Delegation Feedback, Feedback Summary
- **Quan hệ chính:** visit_request_id -> visit_requests(visit_request_id); visit_instance_id -> visit_request_campuses(visit_instance_id); submitted_by_user_id -> users(user_id); target_user_id -> users(user_id)

| Trường | Loại dữ liệu | Key / Null / Default | Enum / Check | Ý nghĩa / Vì sao có trường này |
|---|---|---|---|---|
| `feedback_id` | `BIGINT UNSIGNED` | PK; NOT NULL; AUTO_INCREMENT |  | Khóa chính định danh duy nhất bản ghi trong bảng. |
| `visit_request_id` | `BIGINT UNSIGNED` | IDX: idx_feedbacks_visit_request; FK: visit_request_id -> visit_requests(visit_request_id) [fk_feedbacks_visit_request]; NOT NULL |  | Khóa ngoại liên kết tới visit_requests(visit_request_id), dùng để join dữ liệu và đảm bảo toàn vẹn tham chiếu. |
| `visit_instance_id` | `BIGINT UNSIGNED` | IDX: idx_feedbacks_visit_instance; FK: visit_instance_id -> visit_request_campuses(visit_instance_id) [fk_feedbacks_visit_instance]; NULL |  | Khóa ngoại liên kết tới visit_request_campuses(visit_instance_id), dùng để join dữ liệu và đảm bảo toàn vẹn tham chiếu. |
| `submitted_by_user_id` | `BIGINT UNSIGNED` | IDX: idx_feedbacks_submitter; FK: submitted_by_user_id -> users(user_id) [fk_feedbacks_submitter]; NOT NULL |  | User gửi feedback; khách/host/logistics đều phải có tài khoản hệ thống |
| `submitter_role` | `ENUM('VISITOR','HOST','LOGISTICS')` | IDX: idx_feedbacks_roles; NOT NULL | ENUM: ('VISITOR','HOST','LOGISTICS') | Vai trò người gửi trong chuyến thăm |
| `submitter_context` | `VARCHAR(120)` | NOT NULL; DEFAULT '' |  | Ngữ cảnh vai trò người gửi, ví dụ: Host chính, Xe điện, Teabreak, Khách đại diện |
| `submitter_name_snapshot` | `VARCHAR(255)` | NOT NULL |  | Tên người gửi tại thời điểm gửi feedback |
| `target_user_id` | `BIGINT UNSIGNED` | IDX: idx_feedbacks_target; FK: target_user_id -> users(user_id) [fk_feedbacks_target]; NOT NULL |  | User được đánh giá |
| `target_role` | `ENUM('VISITOR','HOST','LOGISTICS')` | IDX: idx_feedbacks_roles; NOT NULL | ENUM: ('VISITOR','HOST','LOGISTICS') | Vai trò người được đánh giá trong chuyến thăm |
| `target_context` | `VARCHAR(120)` | NOT NULL; DEFAULT '' |  | Ngữ cảnh đối tượng được đánh giá, ví dụ: Host chính, Đoàn khách, Xe điện, Teabreak |
| `target_name_snapshot` | `VARCHAR(255)` | NOT NULL |  | Tên người được đánh giá tại thời điểm gửi feedback |
| `rating` | `TINYINT UNSIGNED` | IDX: idx_feedbacks_rating; NOT NULL |  | Số sao từ 1 đến 5 |
| `comment` | `TEXT` | NOT NULL |  | Nội dung feedback |
| `submitted_at` | `DATETIME` | IDX: idx_feedbacks_submitted_at; NOT NULL; DEFAULT CURRENT_TIMESTAMP |  | Mốc thời gian nghiệp vụ, phục vụ audit, filter, timeline hoặc SLA. |
| `CONSTRAINT` | `chk_feedbacks_rating` | NULL | CHECK: (submitter_role IN ('VISITOR','LOGISTICS' | Lưu thông tin `CONSTRAINT` của bảng `feedbacks` để phục vụ màn hình và logic nghiệp vụ liên quan. |
| `CONSTRAINT` | `chk_feedbacks_role_flow` | NULL | CHECK: (submitter_role IN ('VISITOR','LOGISTICS' | Lưu thông tin `CONSTRAINT` của bảng `feedbacks` để phục vụ màn hình và logic nghiệp vụ liên quan. |

### 3.26. `feedback_rating_items`

- **Tác dụng:** Điểm feedback theo từng tiêu chí.
- **Màn hình/UC dùng:** Feedback Form, Feedback Analytics/Summary
- **Quan hệ chính:** feedback_id -> feedbacks(feedback_id)

| Trường | Loại dữ liệu | Key / Null / Default | Enum / Check | Ý nghĩa / Vì sao có trường này |
|---|---|---|---|---|
| `feedback_rating_item_id` | `BIGINT UNSIGNED` | PK; NOT NULL; AUTO_INCREMENT |  | Khóa chính định danh duy nhất bản ghi trong bảng. |
| `feedback_id` | `BIGINT UNSIGNED` | UNIQUE: uq_feedback_rating_criterion; IDX: idx_feedback_rating_feedback; FK: feedback_id -> feedbacks(feedback_id) [fk_feedback_rating_items_feedback]; NOT NULL |  | Khóa ngoại liên kết tới feedbacks(feedback_id), dùng để join dữ liệu và đảm bảo toàn vẹn tham chiếu. |
| `criterion_code` | `VARCHAR(80)` | UNIQUE: uq_feedback_rating_criterion; NOT NULL |  | Mã nghiệp vụ/technical code ổn định, dùng cho tìm kiếm, mapping và tránh phụ thuộc tên hiển thị. |
| `criterion_label` | `VARCHAR(150)` | NOT NULL |  | Lưu thông tin `criterion label` của bảng `feedback_rating_items` để phục vụ màn hình và logic nghiệp vụ liên quan. |
| `rating` | `TINYINT UNSIGNED` | NOT NULL | CHECK: rating BETWEEN 1 AND 5 | Lưu thông tin `rating` của bảng `feedback_rating_items` để phục vụ màn hình và logic nghiệp vụ liên quan. |
| `display_order` | `INT UNSIGNED` | NOT NULL; DEFAULT 0 |  | Thứ tự hiển thị trên UI, giúp sắp xếp danh sách ổn định. |
| `created_at` | `DATETIME` | NOT NULL; DEFAULT CURRENT_TIMESTAMP |  | Thời điểm tạo bản ghi, cần cho audit, sắp xếp và truy vết lịch sử. |

### 3.27. `news`

- **Tác dụng:** Metadata bài news và workflow duyệt/xuất bản.
- **Màn hình/UC dùng:** News Management, Public News, Visit story/news from visit
- **Quan hệ chính:** campus_id -> campuses(campus_id); visit_instance_id -> visit_request_campuses(visit_instance_id); author_user_id -> users(user_id); cover_file_id -> files(file_id); reviewed_by -> users(user_id)

| Trường | Loại dữ liệu | Key / Null / Default | Enum / Check | Ý nghĩa / Vì sao có trường này |
|---|---|---|---|---|
| `news_id` | `BIGINT UNSIGNED` | PK; NOT NULL; AUTO_INCREMENT |  | Khóa chính định danh duy nhất bản ghi trong bảng. |
| `campus_id` | `BIGINT UNSIGNED` | IDX: idx_news_public; FK: campus_id -> campuses(campus_id) [fk_news_campus]; NULL |  | Campus liên quan đến bài viết. NULL nếu bài toàn hệ thống |
| `visit_instance_id` | `BIGINT UNSIGNED` | IDX: idx_news_visit_instance_status; FK: visit_instance_id -> visit_request_campuses(visit_instance_id) [fk_news_visit_instance]; NULL |  | Visit instance liên quan nếu bài viết được tạo từ một chuyến tiếp đón |
| `author_user_id` | `BIGINT UNSIGNED` | IDX: idx_news_author_status; FK: author_user_id -> users(user_id) [fk_news_author]; NOT NULL |  | Người tạo/viết bài |
| `cover_file_id` | `BIGINT UNSIGNED` | FK: cover_file_id -> files(file_id) [fk_news_cover_file]; NULL |  | Ảnh bìa bài viết, trỏ tới files.file_id |
| `status` | `ENUM('PENDING_REVIEW','REJECTED','PUBLISHED','HIDDEN')` | IDX: idx_news_public, idx_news_author_status, idx_news_visit_instance_status, idx_news_featured; NOT NULL; DEFAULT 'PENDING_REVIEW' | ENUM: ('PENDING_REVIEW','REJECTED','PUBLISHED','HIDDEN') | PENDING_REVIEW=chờ host duyệt, REJECTED=bị từ chối, PUBLISHED=đã đăng, HIDDEN=ẩn khỏi trang tin |
| `submitted_at` | `DATETIME` | NOT NULL; DEFAULT CURRENT_TIMESTAMP |  | Thời điểm người viết gửi bài cho host duyệt |
| `reviewed_by` | `BIGINT UNSIGNED` | IDX: idx_news_review; FK: reviewed_by -> users(user_id) [fk_news_reviewed_by]; NULL |  | Host duyệt hoặc từ chối bài viết |
| `reviewed_at` | `DATETIME` | IDX: idx_news_review; NULL |  | Thời điểm host duyệt hoặc từ chối |
| `review_note` | `TEXT` | NULL |  | Ghi chú duyệt hoặc lý do từ chối |
| `published_at` | `DATETIME` | IDX: idx_news_public, idx_news_featured; NULL |  | Thời điểm bài viết được đăng |
| `is_featured` | `BOOLEAN` | IDX: idx_news_featured; NOT NULL; DEFAULT FALSE |  | Bài viết nổi bật |
| `row_version` | `INT UNSIGNED` | NOT NULL; DEFAULT 0 |  | Optimistic concurrency token, chống ghi đè khi cập nhật đồng thời |
| `created_at` | `DATETIME` | NOT NULL; DEFAULT CURRENT_TIMESTAMP |  | Thời điểm tạo bản ghi, cần cho audit, sắp xếp và truy vết lịch sử. |
| `created_by` | `BIGINT UNSIGNED` | NULL |  | User tạo bản ghi, dùng cho ownership, audit và phân quyền theo người tạo. |
| `updated_at` | `DATETIME` | NULL; DEFAULT NULL; ON UPDATE CURRENT_TIMESTAMP |  | Thời điểm cập nhật gần nhất, giúp UI/backend xác định dữ liệu mới nhất. |
| `updated_by` | `BIGINT UNSIGNED` | NULL |  | User cập nhật bản ghi gần nhất, phục vụ audit và kiểm tra thay đổi. |

### 3.28. `news_translations`

- **Tác dụng:** Nội dung tiêu đề/tóm tắt/SEO theo ngôn ngữ.
- **Màn hình/UC dùng:** Create/Edit News, Public News multilingual display, AI translation output
- **Quan hệ chính:** news_id -> news(news_id)

| Trường | Loại dữ liệu | Key / Null / Default | Enum / Check | Ý nghĩa / Vì sao có trường này |
|---|---|---|---|---|
| `news_translation_id` | `BIGINT UNSIGNED` | PK; NOT NULL; AUTO_INCREMENT |  | Khóa chính định danh duy nhất bản ghi trong bảng. |
| `news_id` | `BIGINT UNSIGNED` | UNIQUE: uq_news_translation_lang; FK: news_id -> news(news_id) [fk_news_translations_news]; NOT NULL |  | Khóa ngoại liên kết tới news(news_id), dùng để join dữ liệu và đảm bảo toàn vẹn tham chiếu. |
| `language_code` | `VARCHAR(20)` | UNIQUE: uq_news_translation_lang, uq_news_translation_slug_lang; IDX: idx_news_translations_lang; NOT NULL; DEFAULT 'vi' |  | Mã ngôn ngữ của bản dịch/nội dung, dùng cho dropdown, public display và tích hợp AI translation nếu có. |
| `title` | `VARCHAR(255)` | NOT NULL |  | Tiêu đề chính của bài viết |
| `slug` | `VARCHAR(255)` | UNIQUE: uq_news_translation_slug_lang; NOT NULL |  | Đường dẫn SEO của bài viết |
| `summary` | `TEXT` | NULL |  | Tóm tắt bài viết |
| `seo_title` | `VARCHAR(255)` | NULL |  | Lưu thông tin `seo title` của bảng `news_translations` để phục vụ màn hình và logic nghiệp vụ liên quan. |
| `seo_description` | `VARCHAR(500)` | NULL |  | Lưu thông tin `seo description` của bảng `news_translations` để phục vụ màn hình và logic nghiệp vụ liên quan. |
| `created_at` | `DATETIME` | NOT NULL; DEFAULT CURRENT_TIMESTAMP |  | Thời điểm tạo bản ghi, cần cho audit, sắp xếp và truy vết lịch sử. |
| `updated_at` | `DATETIME` | NULL; DEFAULT NULL; ON UPDATE CURRENT_TIMESTAMP |  | Thời điểm cập nhật gần nhất, giúp UI/backend xác định dữ liệu mới nhất. |
| `FULLTEXT` | `KEY ft_news_translations_search (title, summary)` | NULL |  | Lưu thông tin `FULLTEXT` của bảng `news_translations` để phục vụ màn hình và logic nghiệp vụ liên quan. |

### 3.29. `news_content_sections`

- **Tác dụng:** Các section nội dung chi tiết của bài news.
- **Màn hình/UC dùng:** News Editor, Public News Detail
- **Quan hệ chính:** news_translation_id -> news_translations(news_translation_id)

| Trường | Loại dữ liệu | Key / Null / Default | Enum / Check | Ý nghĩa / Vì sao có trường này |
|---|---|---|---|---|
| `section_id` | `BIGINT UNSIGNED` | PK; NOT NULL; AUTO_INCREMENT |  | Khóa chính định danh duy nhất bản ghi trong bảng. |
| `news_translation_id` | `BIGINT UNSIGNED` | UNIQUE: uq_news_section_order; IDX: idx_news_sections_translation; FK: news_translation_id -> news_translations(news_translation_id) [fk_news_sections_translation]; NOT NULL |  | Khóa ngoại liên kết tới news_translations(news_translation_id), dùng để join dữ liệu và đảm bảo toàn vẹn tham chiếu. |
| `section_order` | `TINYINT UNSIGNED` | UNIQUE: uq_news_section_order; NOT NULL | CHECK: section_order BETWEEN 1 AND 10 | Thứ tự section, từ 1 đến 10 |
| `section_title` | `VARCHAR(255)` | NOT NULL |  | Tiêu đề section |
| `section_body_html` | `LONGTEXT` | NOT NULL |  | Nội dung rich text dạng HTML đã sanitize, có thể chứa paragraph, bold, italic, color, link, image |
| `section_body_text` | `TEXT` | NULL |  | Plain text tách từ HTML để search hoặc preview |
| `created_at` | `DATETIME` | NOT NULL; DEFAULT CURRENT_TIMESTAMP |  | Thời điểm tạo bản ghi, cần cho audit, sắp xếp và truy vết lịch sử. |
| `updated_at` | `DATETIME` | NULL; DEFAULT NULL; ON UPDATE CURRENT_TIMESTAMP |  | Thời điểm cập nhật gần nhất, giúp UI/backend xác định dữ liệu mới nhất. |
| `FULLTEXT` | `KEY ft_news_sections_search (section_title, section_body_text)` | NULL |  | Lưu thông tin `FULLTEXT` của bảng `news_content_sections` để phục vụ màn hình và logic nghiệp vụ liên quan. |

### 3.30. `news_section_files`

- **Tác dụng:** File/ảnh gắn vào từng section bài news.
- **Màn hình/UC dùng:** News Editor media management
- **Quan hệ chính:** section_id -> news_content_sections(section_id); file_id -> files(file_id)

| Trường | Loại dữ liệu | Key / Null / Default | Enum / Check | Ý nghĩa / Vì sao có trường này |
|---|---|---|---|---|
| `section_file_id` | `BIGINT UNSIGNED` | PK; NOT NULL; AUTO_INCREMENT |  | Khóa chính định danh duy nhất bản ghi trong bảng. |
| `section_id` | `BIGINT UNSIGNED` | UNIQUE: uq_news_section_file; IDX: idx_news_section_files_section; FK: section_id -> news_content_sections(section_id) [fk_news_section_files_section]; NOT NULL |  | Khóa ngoại liên kết tới news_content_sections(section_id), dùng để join dữ liệu và đảm bảo toàn vẹn tham chiếu. |
| `file_id` | `BIGINT UNSIGNED` | UNIQUE: uq_news_section_file; IDX: idx_news_section_files_file; FK: file_id -> files(file_id) [fk_news_section_files_file]; NOT NULL |  | Khóa ngoại liên kết tới files(file_id), dùng để join dữ liệu và đảm bảo toàn vẹn tham chiếu. |
| `usage_type` | `ENUM('INLINE_IMAGE','ATTACHMENT')` | NOT NULL; DEFAULT 'INLINE_IMAGE' | ENUM: ('INLINE_IMAGE','ATTACHMENT') | INLINE_IMAGE=ảnh chèn trong nội dung, ATTACHMENT=file đính kèm |
| `display_order` | `INT UNSIGNED` | NOT NULL; DEFAULT 0 |  | Thứ tự hiển thị trên UI, giúp sắp xếp danh sách ổn định. |
| `created_at` | `DATETIME` | NOT NULL; DEFAULT CURRENT_TIMESTAMP |  | Thời điểm tạo bản ghi, cần cho audit, sắp xếp và truy vết lịch sử. |

### 3.31. `faqs`

- **Tác dụng:** FAQ một ngôn ngữ với trạng thái hiển thị công khai.
- **Màn hình/UC dùng:** FAQ Management, Public FAQ, Search FAQ
- **Quan hệ chính:** Không có FOREIGN KEY trực tiếp trong CREATE TABLE.

| Trường | Loại dữ liệu | Key / Null / Default | Enum / Check | Ý nghĩa / Vì sao có trường này |
|---|---|---|---|---|
| `faq_id` | `BIGINT UNSIGNED` | PK; NOT NULL; AUTO_INCREMENT |  | Khóa chính định danh duy nhất bản ghi trong bảng. |
| `faq_type` | `ENUM('PROGRAM','TUITION_FEE','VISA','DORMITORY','VISIT_REQUEST','SECURITY','LOGISTICS','OTHER')` | IDX: idx_faqs_type_status; NOT NULL; DEFAULT 'OTHER' | ENUM: ('PROGRAM','TUITION_FEE','VISA','DORMITORY','VISIT_REQUEST','SECURITY','LOGISTICS','OTHER') | Loại nghiệp vụ của bản ghi, dùng để phân nhánh xử lý và filter UI. |
| `language_code` | `ENUM('vi','en')` | IDX: idx_faqs_language_status; NOT NULL; DEFAULT 'vi' | ENUM: ('vi','en') | Mã ngôn ngữ của bản dịch/nội dung, dùng cho dropdown, public display và tích hợp AI translation nếu có. |
| `question` | `VARCHAR(500)` | NOT NULL |  | Câu hỏi FAQ |
| `answer` | `TEXT` | NOT NULL |  | Câu trả lời FAQ |
| `display_order` | `INT UNSIGNED` | IDX: idx_faqs_status_order; NOT NULL; DEFAULT 0 |  | Thứ tự hiển thị trên UI, giúp sắp xếp danh sách ổn định. |
| `status` | `ENUM('PUBLISHED','HIDDEN')` | IDX: idx_faqs_status_order, idx_faqs_type_status, idx_faqs_language_status; NOT NULL; DEFAULT 'HIDDEN' | ENUM: ('PUBLISHED','HIDDEN') | PUBLISHED=hiển thị trên trang FAQ, HIDDEN=ẩn khỏi người xem thường nhưng người quản lý vẫn thấy |
| `created_at` | `DATETIME` | NOT NULL; DEFAULT CURRENT_TIMESTAMP |  | Thời điểm tạo bản ghi, cần cho audit, sắp xếp và truy vết lịch sử. |
| `created_by` | `BIGINT UNSIGNED` | NULL |  | User tạo bản ghi, dùng cho ownership, audit và phân quyền theo người tạo. |
| `updated_at` | `DATETIME` | NULL; DEFAULT NULL; ON UPDATE CURRENT_TIMESTAMP |  | Thời điểm cập nhật gần nhất, giúp UI/backend xác định dữ liệu mới nhất. |
| `updated_by` | `BIGINT UNSIGNED` | NULL |  | User cập nhật bản ghi gần nhất, phục vụ audit và kiểm tra thay đổi. |
| `FULLTEXT` | `KEY ft_faqs_search (question, answer)` | NULL |  | Lưu thông tin `FULLTEXT` của bảng `faqs` để phục vụ màn hình và logic nghiệp vụ liên quan. |

### 3.32. `galleries`

- **Tác dụng:** Gallery địa điểm/khu vực trong campus.
- **Màn hình/UC dùng:** Gallery Management, Public Gallery, Visit FPTU
- **Quan hệ chính:** campus_id -> campuses(campus_id); hero_file_id -> files(file_id)

| Trường | Loại dữ liệu | Key / Null / Default | Enum / Check | Ý nghĩa / Vì sao có trường này |
|---|---|---|---|---|
| `gallery_id` | `BIGINT UNSIGNED` | PK; NOT NULL; AUTO_INCREMENT |  | Khóa chính định danh duy nhất bản ghi trong bảng. |
| `campus_id` | `BIGINT UNSIGNED` | IDX: idx_galleries_campus_status, idx_galleries_area_specific; FK: campus_id -> campuses(campus_id) [fk_galleries_campus]; NOT NULL |  | Khóa ngoại liên kết tới campuses(campus_id), dùng để join dữ liệu và đảm bảo toàn vẹn tham chiếu. |
| `area_name` | `VARCHAR(150)` | IDX: idx_galleries_area_specific; NOT NULL; DEFAULT 'Campus' |  | Khu vực trong campus, ví dụ: Academic Area, Lobby, Lab Zone |
| `specific_location_name` | `VARCHAR(150)` | IDX: idx_galleries_area_specific; NOT NULL; DEFAULT 'Campus location' |  | Vị trí cụ thể trong khu vực, ví dụ: Sảnh Alpha, Green Lab |
| `location_description` | `TEXT` | NULL |  | Mô tả vị trí/khu vực hiển thị ở Gallery/Visit FPTU |
| `title` | `VARCHAR(255)` | NOT NULL |  | Tên hiển thị của gallery/địa điểm |
| `description` | `TEXT` | NULL |  | Mô tả ngắn về địa điểm |
| `story_content` | `TEXT` | NULL |  | Ý nghĩa hoặc câu chuyện giới thiệu về địa điểm |
| `status` | `ENUM('DRAFT','PUBLISHED','HIDDEN')` | IDX: idx_galleries_campus_status, idx_galleries_visibility_status; NOT NULL; DEFAULT 'DRAFT' | ENUM: ('DRAFT','PUBLISHED','HIDDEN') | DRAFT=nháp, PUBLISHED=hiển thị theo visibility, HIDDEN=ẩn khỏi người xem thường nhưng Staff Leader vẫn quản lý được |
| `visibility` | `ENUM('PRIVATE','INTERNAL','PUBLIC')` | IDX: idx_galleries_visibility_status; NOT NULL; DEFAULT 'INTERNAL' | ENUM: ('PRIVATE','INTERNAL','PUBLIC') | Phạm vi xem khi status=PUBLISHED: PRIVATE=chỉ quản lý, INTERNAL=user nội bộ, PUBLIC=công khai |
| `hero_file_id` | `BIGINT UNSIGNED` | IDX: idx_galleries_hero_file; FK: hero_file_id -> files(file_id) [fk_galleries_hero_file]; NULL |  | Khóa ngoại liên kết tới files(file_id), dùng để join dữ liệu và đảm bảo toàn vẹn tham chiếu. |
| `virtual_tour_url` | `VARCHAR(700)` | NULL |  | Lưu thông tin `virtual tour url` của bảng `galleries` để phục vụ màn hình và logic nghiệp vụ liên quan. |
| `created_at` | `DATETIME` | NOT NULL; DEFAULT CURRENT_TIMESTAMP |  | Thời điểm tạo bản ghi, cần cho audit, sắp xếp và truy vết lịch sử. |
| `created_by` | `BIGINT UNSIGNED` | NULL |  | User tạo bản ghi, dùng cho ownership, audit và phân quyền theo người tạo. |
| `updated_at` | `DATETIME` | NULL; DEFAULT NULL; ON UPDATE CURRENT_TIMESTAMP |  | Thời điểm cập nhật gần nhất, giúp UI/backend xác định dữ liệu mới nhất. |
| `updated_by` | `BIGINT UNSIGNED` | NULL |  | User cập nhật bản ghi gần nhất, phục vụ audit và kiểm tra thay đổi. |
| `deleted_at` | `DATETIME` | IDX: idx_galleries_campus_status; NULL |  | Thời điểm soft delete bản ghi. |
| `deleted_by` | `BIGINT UNSIGNED` | NULL |  | User thực hiện soft delete bản ghi. |

### 3.33. `gallery_images`

- **Tác dụng:** Ảnh/video thuộc gallery.
- **Màn hình/UC dùng:** Gallery Item List, Add/Update Gallery Item, Public Gallery
- **Quan hệ chính:** gallery_id -> galleries(gallery_id); file_id -> files(file_id); thumbnail_file_id -> files(file_id)

| Trường | Loại dữ liệu | Key / Null / Default | Enum / Check | Ý nghĩa / Vì sao có trường này |
|---|---|---|---|---|
| `image_id` | `BIGINT UNSIGNED` | PK; NOT NULL; AUTO_INCREMENT |  | Khóa chính định danh duy nhất bản ghi trong bảng. |
| `gallery_id` | `BIGINT UNSIGNED` | IDX: idx_gallery_images_gallery_order; FK: gallery_id -> galleries(gallery_id) [fk_gallery_images_gallery]; NOT NULL |  | Khóa ngoại liên kết tới galleries(gallery_id), dùng để join dữ liệu và đảm bảo toàn vẹn tham chiếu. |
| `file_id` | `BIGINT UNSIGNED` | UNIQUE: uq_gallery_images_file; FK: file_id -> files(file_id) [fk_gallery_images_file]; NOT NULL |  | Khóa ngoại liên kết tới files(file_id), dùng để join dữ liệu và đảm bảo toàn vẹn tham chiếu. |
| `media_type` | `ENUM('IMAGE','VIDEO')` | IDX: idx_gallery_images_media_type; NOT NULL; DEFAULT 'IMAGE' | ENUM: ('IMAGE','VIDEO') | Loại nghiệp vụ của bản ghi, dùng để phân nhánh xử lý và filter UI. |
| `thumbnail_file_id` | `BIGINT UNSIGNED` | IDX: idx_gallery_images_thumbnail_file; FK: thumbnail_file_id -> files(file_id) [fk_gallery_images_thumbnail_file]; NULL |  | Khóa ngoại liên kết tới files(file_id), dùng để join dữ liệu và đảm bảo toàn vẹn tham chiếu. |
| `caption` | `VARCHAR(500)` | NULL |  | Chú thích riêng cho từng ảnh |
| `display_order` | `INT UNSIGNED` | IDX: idx_gallery_images_gallery_order; NOT NULL; DEFAULT 0 |  | Thứ tự hiển thị trên UI, giúp sắp xếp danh sách ổn định. |
| `taken_at` | `DATETIME` | IDX: idx_gallery_images_status_time; NULL |  | Mốc thời gian nghiệp vụ, phục vụ audit, filter, timeline hoặc SLA. |
| `status` | `ENUM('ACTIVE','HIDDEN')` | IDX: idx_gallery_images_status_time; NOT NULL; DEFAULT 'ACTIVE' | ENUM: ('ACTIVE','HIDDEN') | ACTIVE=ảnh đang dùng, HIDDEN=ảnh bị ẩn khỏi gallery thường |
| `created_at` | `DATETIME` | NOT NULL; DEFAULT CURRENT_TIMESTAMP |  | Thời điểm tạo bản ghi, cần cho audit, sắp xếp và truy vết lịch sử. |
| `created_by` | `BIGINT UNSIGNED` | NULL |  | User tạo bản ghi, dùng cho ownership, audit và phân quyền theo người tạo. |
| `updated_at` | `DATETIME` | NULL; DEFAULT NULL; ON UPDATE CURRENT_TIMESTAMP |  | Thời điểm cập nhật gần nhất, giúp UI/backend xác định dữ liệu mới nhất. |
| `updated_by` | `BIGINT UNSIGNED` | NULL |  | User cập nhật bản ghi gần nhất, phục vụ audit và kiểm tra thay đổi. |
| `deleted_at` | `DATETIME` | NULL |  | Thời điểm soft delete bản ghi. |
| `deleted_by` | `BIGINT UNSIGNED` | NULL |  | User thực hiện soft delete bản ghi. |

### 3.34. `photo_face_tags`

- **Tác dụng:** Metadata tag khuôn mặt đã xác nhận, không lưu vector sinh trắc.
- **Màn hình/UC dùng:** Tag Faces on Photos, Visit Photos, Gallery moderation
- **Quan hệ chính:** image_id -> gallery_images(image_id); visit_request_id -> visit_requests(visit_request_id); guest_member_id -> visit_guest_members(guest_member_id); partner_contact_id -> partner_contacts(contact_id); confirmed_by -> users(user_id)

| Trường | Loại dữ liệu | Key / Null / Default | Enum / Check | Ý nghĩa / Vì sao có trường này |
|---|---|---|---|---|
| `face_tag_id` | `BIGINT UNSIGNED` | PK; NOT NULL; AUTO_INCREMENT |  | Khóa chính định danh duy nhất bản ghi trong bảng. |
| `image_id` | `BIGINT UNSIGNED` | IDX: idx_face_tags_image; FK: image_id -> gallery_images(image_id) [fk_face_tags_image]; NOT NULL |  | Khóa ngoại liên kết tới gallery_images(image_id), dùng để join dữ liệu và đảm bảo toàn vẹn tham chiếu. |
| `visit_request_id` | `BIGINT UNSIGNED` | FK: visit_request_id -> visit_requests(visit_request_id) [fk_face_tags_visit_request]; NULL |  | Khóa ngoại liên kết tới visit_requests(visit_request_id), dùng để join dữ liệu và đảm bảo toàn vẹn tham chiếu. |
| `guest_member_id` | `BIGINT UNSIGNED` | IDX: idx_face_tags_guest; FK: guest_member_id -> visit_guest_members(guest_member_id) [fk_face_tags_guest]; NULL |  | Khóa ngoại liên kết tới visit_guest_members(guest_member_id), dùng để join dữ liệu và đảm bảo toàn vẹn tham chiếu. |
| `partner_contact_id` | `BIGINT UNSIGNED` | IDX: idx_face_tags_partner_contact; FK: partner_contact_id -> partner_contacts(contact_id) [fk_face_tags_partner_contact]; NULL |  | Khóa ngoại liên kết tới partner_contacts(contact_id), dùng để join dữ liệu và đảm bảo toàn vẹn tham chiếu. |
| `display_name` | `VARCHAR(150)` | NOT NULL |  | Lưu thông tin `display name` của bảng `photo_face_tags` để phục vụ màn hình và logic nghiệp vụ liên quan. |
| `bounding_box_x` | `DECIMAL(8,4)` | NULL |  | Lưu thông tin `bounding box x` của bảng `photo_face_tags` để phục vụ màn hình và logic nghiệp vụ liên quan. |
| `bounding_box_y` | `DECIMAL(8,4)` | NULL |  | Lưu thông tin `bounding box y` của bảng `photo_face_tags` để phục vụ màn hình và logic nghiệp vụ liên quan. |
| `bounding_box_width` | `DECIMAL(8,4)` | NULL |  | Lưu thông tin `bounding box width` của bảng `photo_face_tags` để phục vụ màn hình và logic nghiệp vụ liên quan. |
| `bounding_box_height` | `DECIMAL(8,4)` | NULL |  | Lưu thông tin `bounding box height` của bảng `photo_face_tags` để phục vụ màn hình và logic nghiệp vụ liên quan. |
| `tag_status` | `ENUM('MANUALLY_TAGGED','CONFIRMED','REMOVED')` | IDX: idx_face_tags_status; NOT NULL; DEFAULT 'MANUALLY_TAGGED' | ENUM: ('MANUALLY_TAGGED','CONFIRMED','REMOVED') | Trạng thái nghiệp vụ chi tiết, dùng để hiển thị badge, filter và kiểm soát luồng xử lý. |
| `confirmed_by` | `BIGINT UNSIGNED` | FK: confirmed_by -> users(user_id) [fk_face_tags_confirmed_by]; NULL |  | Khóa ngoại liên kết tới users(user_id), dùng để join dữ liệu và đảm bảo toàn vẹn tham chiếu. |
| `confirmed_at` | `DATETIME` | NULL |  | Mốc thời gian nghiệp vụ, phục vụ audit, filter, timeline hoặc SLA. |
| `created_at` | `DATETIME` | NOT NULL; DEFAULT CURRENT_TIMESTAMP |  | Thời điểm tạo bản ghi, cần cho audit, sắp xếp và truy vết lịch sử. |
| `created_by` | `BIGINT UNSIGNED` | NULL |  | User tạo bản ghi, dùng cho ownership, audit và phân quyền theo người tạo. |
| `removed_at` | `DATETIME` | NULL |  | Mốc thời gian nghiệp vụ, phục vụ audit, filter, timeline hoặc SLA. |
| `removed_by` | `BIGINT UNSIGNED` | NULL |  | User thực hiện hành động liên quan, phục vụ audit và phân quyền. |

### 3.35. `email_templates`

- **Tác dụng:** Mẫu email với nội dung VI/EN explicit.
- **Màn hình/UC dùng:** Email Template List/Detail/Create/Update
- **Quan hệ chính:** campus_id -> campuses(campus_id)

| Trường | Loại dữ liệu | Key / Null / Default | Enum / Check | Ý nghĩa / Vì sao có trường này |
|---|---|---|---|---|
| `email_template_id` | `BIGINT UNSIGNED` | PK; NOT NULL; AUTO_INCREMENT |  | Khóa chính định danh duy nhất bản ghi trong bảng. |
| `template_code` | `VARCHAR(100)` | UNIQUE: uq_email_templates_code; NOT NULL |  | Mã nghiệp vụ/technical code ổn định, dùng cho tìm kiếm, mapping và tránh phụ thuộc tên hiển thị. |
| `name` | `VARCHAR(150)` | NOT NULL |  | Tên hiển thị/chính thức của bản ghi, dùng trên danh sách, dropdown và màn chi tiết. |
| `purpose` | `VARCHAR(100)` | IDX: idx_email_templates_purpose_status; NOT NULL |  | Lưu thông tin `purpose` của bảng `email_templates` để phục vụ màn hình và logic nghiệp vụ liên quan. |
| `campus_id` | `BIGINT UNSIGNED` | IDX: idx_email_templates_campus_status; FK: campus_id -> campuses(campus_id) [fk_email_templates_campus]; NULL |  | Khóa ngoại liên kết tới campuses(campus_id), dùng để join dữ liệu và đảm bảo toàn vẹn tham chiếu. |
| `description` | `VARCHAR(500)` | NULL |  | Mô tả chi tiết, giúp người dùng hiểu nội dung bản ghi. |
| `status` | `ENUM('ACTIVE','INACTIVE')` | IDX: idx_email_templates_status, idx_email_templates_purpose_status, idx_email_templates_campus_status; NOT NULL; DEFAULT 'ACTIVE' | ENUM: ('ACTIVE','INACTIVE') | Trạng thái vòng đời của bản ghi, dùng cho filter, hiển thị badge và chặn/hạn chế thao tác. |
| `subject_vi` | `VARCHAR(255)` | NULL |  | Lưu thông tin `subject vi` của bảng `email_templates` để phục vụ màn hình và logic nghiệp vụ liên quan. |
| `body_vi` | `LONGTEXT` | NULL |  | Lưu thông tin `body vi` của bảng `email_templates` để phục vụ màn hình và logic nghiệp vụ liên quan. |
| `subject_en` | `VARCHAR(255)` | NULL |  | Lưu thông tin `subject en` của bảng `email_templates` để phục vụ màn hình và logic nghiệp vụ liên quan. |
| `body_en` | `LONGTEXT` | NULL |  | Lưu thông tin `body en` của bảng `email_templates` để phục vụ màn hình và logic nghiệp vụ liên quan. |
| `variables_text` | `VARCHAR(700)` | NULL |  | Lưu thông tin `variables text` của bảng `email_templates` để phục vụ màn hình và logic nghiệp vụ liên quan. |
| `created_at` | `DATETIME` | NOT NULL; DEFAULT CURRENT_TIMESTAMP |  | Thời điểm tạo bản ghi, cần cho audit, sắp xếp và truy vết lịch sử. |
| `created_by` | `BIGINT UNSIGNED` | NULL |  | User tạo bản ghi, dùng cho ownership, audit và phân quyền theo người tạo. |
| `updated_at` | `DATETIME` | NULL; DEFAULT NULL; ON UPDATE CURRENT_TIMESTAMP |  | Thời điểm cập nhật gần nhất, giúp UI/backend xác định dữ liệu mới nhất. |
| `updated_by` | `BIGINT UNSIGNED` | NULL |  | User cập nhật bản ghi gần nhất, phục vụ audit và kiểm tra thay đổi. |

### 3.36. `sent_emails`

- **Tác dụng:** Lịch sử gửi email tổng.
- **Màn hình/UC dùng:** Send Email, Sent Email List/Detail, Email delivery tracking
- **Quan hệ chính:** email_template_id -> email_templates(email_template_id); sent_by -> users(user_id)

| Trường | Loại dữ liệu | Key / Null / Default | Enum / Check | Ý nghĩa / Vì sao có trường này |
|---|---|---|---|---|
| `sent_email_id` | `BIGINT UNSIGNED` | PK; NOT NULL; AUTO_INCREMENT |  | Khóa chính định danh duy nhất bản ghi trong bảng. |
| `email_template_id` | `BIGINT UNSIGNED` | IDX: idx_sent_emails_template; FK: email_template_id -> email_templates(email_template_id) [fk_sent_emails_template]; NULL |  | Khóa ngoại liên kết tới email_templates(email_template_id), dùng để join dữ liệu và đảm bảo toàn vẹn tham chiếu. |
| `related_type` | `VARCHAR(80)` | IDX: idx_sent_emails_related; NULL |  | Loại nghiệp vụ của bản ghi, dùng để phân nhánh xử lý và filter UI. |
| `related_id` | `BIGINT UNSIGNED` | IDX: idx_sent_emails_related; NULL |  | Mã định danh bản ghi liên quan, dùng cho join, phân quyền hoặc truy xuất nghiệp vụ. |
| `subject` | `VARCHAR(255)` | NOT NULL |  | Lưu thông tin `subject` của bảng `sent_emails` để phục vụ màn hình và logic nghiệp vụ liên quan. |
| `body_snapshot` | `LONGTEXT` | NULL |  | Lưu thông tin `body snapshot` của bảng `sent_emails` để phục vụ màn hình và logic nghiệp vụ liên quan. |
| `provider_thread_id` | `VARCHAR(255)` | IDX: idx_sent_emails_provider_thread; NULL |  | Mã định danh bản ghi liên quan, dùng cho join, phân quyền hoặc truy xuất nghiệp vụ. |
| `provider_message_id` | `VARCHAR(255)` | IDX: idx_sent_emails_provider_message; NULL |  | Mã định danh bản ghi liên quan, dùng cho join, phân quyền hoặc truy xuất nghiệp vụ. |
| `retry_count` | `INT UNSIGNED` | NOT NULL; DEFAULT 0 |  | Lưu thông tin `retry count` của bảng `sent_emails` để phục vụ màn hình và logic nghiệp vụ liên quan. |
| `last_attempt_at` | `DATETIME` | NULL |  | Mốc thời gian nghiệp vụ, phục vụ audit, filter, timeline hoặc SLA. |
| `delivered_at` | `DATETIME` | NULL |  | Mốc thời gian nghiệp vụ, phục vụ audit, filter, timeline hoặc SLA. |
| `status` | `ENUM('QUEUED','SENT','FAILED')` | IDX: idx_sent_emails_status_time; NOT NULL; DEFAULT 'QUEUED' | ENUM: ('QUEUED','SENT','FAILED') | Trạng thái vòng đời của bản ghi, dùng cho filter, hiển thị badge và chặn/hạn chế thao tác. |
| `error_message` | `TEXT` | NULL |  | Lưu thông tin `error message` của bảng `sent_emails` để phục vụ màn hình và logic nghiệp vụ liên quan. |
| `sent_by` | `BIGINT UNSIGNED` | IDX: idx_sent_emails_sent_by_time; FK: sent_by -> users(user_id) [fk_sent_emails_sent_by]; NULL |  | Khóa ngoại liên kết tới users(user_id), dùng để join dữ liệu và đảm bảo toàn vẹn tham chiếu. |
| `sent_at` | `DATETIME` | IDX: idx_sent_emails_sent_by_time; NULL |  | Mốc thời gian nghiệp vụ, phục vụ audit, filter, timeline hoặc SLA. |
| `created_at` | `DATETIME` | IDX: idx_sent_emails_status_time; NOT NULL; DEFAULT CURRENT_TIMESTAMP |  | Thời điểm tạo bản ghi, cần cho audit, sắp xếp và truy vết lịch sử. |

### 3.37. `sent_email_recipients`

- **Tác dụng:** Người nhận của từng email đã gửi.
- **Màn hình/UC dùng:** Sent Email Detail, Email delivery tracking per recipient
- **Quan hệ chính:** sent_email_id -> sent_emails(sent_email_id)

| Trường | Loại dữ liệu | Key / Null / Default | Enum / Check | Ý nghĩa / Vì sao có trường này |
|---|---|---|---|---|
| `sent_email_recipient_id` | `BIGINT UNSIGNED` | PK; NOT NULL; AUTO_INCREMENT |  | Khóa chính định danh duy nhất bản ghi trong bảng. |
| `sent_email_id` | `BIGINT UNSIGNED` | IDX: idx_sent_email_recipients_sent_email; FK: sent_email_id -> sent_emails(sent_email_id) [fk_sent_email_recipients_email]; NOT NULL |  | Khóa ngoại liên kết tới sent_emails(sent_email_id), dùng để join dữ liệu và đảm bảo toàn vẹn tham chiếu. |
| `recipient_email` | `VARCHAR(150)` | IDX: idx_sent_email_recipients_email_status; NOT NULL |  | Lưu thông tin `recipient email` của bảng `sent_email_recipients` để phục vụ màn hình và logic nghiệp vụ liên quan. |
| `recipient_name` | `VARCHAR(150)` | NULL |  | Lưu thông tin `recipient name` của bảng `sent_email_recipients` để phục vụ màn hình và logic nghiệp vụ liên quan. |
| `recipient_type` | `ENUM('TO','CC','BCC')` | NOT NULL; DEFAULT 'TO' | ENUM: ('TO','CC','BCC') | Loại nghiệp vụ của bản ghi, dùng để phân nhánh xử lý và filter UI. |
| `delivery_status` | `ENUM('QUEUED','SENT','DELIVERED','FAILED','BOUNCED')` | IDX: idx_sent_email_recipients_email_status; NOT NULL; DEFAULT 'QUEUED' | ENUM: ('QUEUED','SENT','DELIVERED','FAILED','BOUNCED') | Trạng thái nghiệp vụ chi tiết, dùng để hiển thị badge, filter và kiểm soát luồng xử lý. |
| `provider_message_id` | `VARCHAR(255)` | NULL |  | Mã định danh bản ghi liên quan, dùng cho join, phân quyền hoặc truy xuất nghiệp vụ. |
| `error_message` | `TEXT` | NULL |  | Lưu thông tin `error message` của bảng `sent_email_recipients` để phục vụ màn hình và logic nghiệp vụ liên quan. |
| `sent_at` | `DATETIME` | NULL |  | Mốc thời gian nghiệp vụ, phục vụ audit, filter, timeline hoặc SLA. |
| `delivered_at` | `DATETIME` | NULL |  | Mốc thời gian nghiệp vụ, phục vụ audit, filter, timeline hoặc SLA. |
| `created_at` | `DATETIME` | NOT NULL; DEFAULT CURRENT_TIMESTAMP |  | Thời điểm tạo bản ghi, cần cho audit, sắp xếp và truy vết lịch sử. |
| `FULLTEXT` | `KEY ft_sent_email_recipients_search (recipient_email, recipient_name)` | NULL |  | Lưu thông tin `FULLTEXT` của bảng `sent_email_recipients` để phục vụ màn hình và logic nghiệp vụ liên quan. |

### 3.38. `notifications`

- **Tác dụng:** Thông báo in-app tới user.
- **Màn hình/UC dùng:** Notification Center, dashboard alerts
- **Quan hệ chính:** recipient_user_id -> users(user_id)

| Trường | Loại dữ liệu | Key / Null / Default | Enum / Check | Ý nghĩa / Vì sao có trường này |
|---|---|---|---|---|
| `notification_id` | `BIGINT UNSIGNED` | PK; NOT NULL; AUTO_INCREMENT |  | Khóa chính định danh duy nhất bản ghi trong bảng. |
| `recipient_user_id` | `BIGINT UNSIGNED` | IDX: idx_notifications_user_read_time; FK: recipient_user_id -> users(user_id) [fk_notifications_user]; NOT NULL |  | Khóa ngoại liên kết tới users(user_id), dùng để join dữ liệu và đảm bảo toàn vẹn tham chiếu. |
| `title` | `VARCHAR(255)` | NOT NULL |  | Tiêu đề hiển thị của bản ghi/nội dung. |
| `message` | `TEXT` | NULL |  | Lưu thông tin `message` của bảng `notifications` để phục vụ màn hình và logic nghiệp vụ liên quan. |
| `notification_type` | `VARCHAR(80)` | IDX: idx_notifications_type_time; NOT NULL |  | Loại nghiệp vụ của bản ghi, dùng để phân nhánh xử lý và filter UI. |
| `related_type` | `VARCHAR(80)` | IDX: idx_notifications_related; NULL |  | Loại nghiệp vụ của bản ghi, dùng để phân nhánh xử lý và filter UI. |
| `related_id` | `BIGINT UNSIGNED` | IDX: idx_notifications_related; NULL |  | Mã định danh bản ghi liên quan, dùng cho join, phân quyền hoặc truy xuất nghiệp vụ. |
| `is_read` | `BOOLEAN` | IDX: idx_notifications_user_read_time; NOT NULL; DEFAULT FALSE |  | Cờ boolean dùng để bật/tắt hoặc đánh dấu một trạng thái nghiệp vụ. |
| `read_at` | `DATETIME` | NULL |  | Mốc thời gian nghiệp vụ, phục vụ audit, filter, timeline hoặc SLA. |
| `created_at` | `DATETIME` | IDX: idx_notifications_user_read_time, idx_notifications_type_time; NOT NULL; DEFAULT CURRENT_TIMESTAMP |  | Thời điểm tạo bản ghi, cần cho audit, sắp xếp và truy vết lịch sử. |

### 3.39. `calendar_events`

- **Tác dụng:** Sự kiện lịch cá nhân/visit/logistics/deadline.
- **Màn hình/UC dùng:** View My Events, Department Calendar, Personal Event CRUD
- **Quan hệ chính:** owner_user_id -> users(user_id); campus_id -> campuses(campus_id); visit_instance_id -> visit_request_campuses(visit_instance_id); logistics_item_id -> visit_logistics_items(logistics_item_id)

| Trường | Loại dữ liệu | Key / Null / Default | Enum / Check | Ý nghĩa / Vì sao có trường này |
|---|---|---|---|---|
| `calendar_event_id` | `BIGINT UNSIGNED` | PK; NOT NULL; AUTO_INCREMENT |  | Khóa chính định danh duy nhất bản ghi trong bảng. |
| `owner_user_id` | `BIGINT UNSIGNED` | IDX: idx_calendar_owner_time; FK: owner_user_id -> users(user_id) [fk_calendar_owner]; NOT NULL |  | Khóa ngoại liên kết tới users(user_id), dùng để join dữ liệu và đảm bảo toàn vẹn tham chiếu. |
| `campus_id` | `BIGINT UNSIGNED` | IDX: idx_calendar_campus_time; FK: campus_id -> campuses(campus_id) [fk_calendar_campus]; NULL |  | Khóa ngoại liên kết tới campuses(campus_id), dùng để join dữ liệu và đảm bảo toàn vẹn tham chiếu. |
| `visit_instance_id` | `BIGINT UNSIGNED` | IDX: idx_calendar_visit; FK: visit_instance_id -> visit_request_campuses(visit_instance_id) [fk_calendar_visit]; NULL |  | Khóa ngoại liên kết tới visit_request_campuses(visit_instance_id), dùng để join dữ liệu và đảm bảo toàn vẹn tham chiếu. |
| `logistics_item_id` | `BIGINT UNSIGNED` | IDX: idx_calendar_logistics; FK: logistics_item_id -> visit_logistics_items(logistics_item_id) [fk_calendar_logistics]; NULL |  | Khóa ngoại liên kết tới visit_logistics_items(logistics_item_id), dùng để join dữ liệu và đảm bảo toàn vẹn tham chiếu. |
| `source_type` | `ENUM('PERSONAL','VISIT','LOGISTICS','DEADLINE')` | IDX: idx_calendar_source_status_time; NOT NULL; DEFAULT 'PERSONAL' | ENUM: ('PERSONAL','VISIT','LOGISTICS','DEADLINE') | Loại nghiệp vụ của bản ghi, dùng để phân nhánh xử lý và filter UI. |
| `title` | `VARCHAR(255)` | NOT NULL |  | Tiêu đề hiển thị của bản ghi/nội dung. |
| `description` | `TEXT` | NULL |  | Mô tả chi tiết, giúp người dùng hiểu nội dung bản ghi. |
| `location` | `VARCHAR(255)` | NULL |  | Lưu thông tin `location` của bảng `calendar_events` để phục vụ màn hình và logic nghiệp vụ liên quan. |
| `start_at` | `DATETIME` | IDX: idx_calendar_owner_time, idx_calendar_campus_time, idx_calendar_source_status_time; NOT NULL | CHECK: end_at > start_at | Mốc thời gian nghiệp vụ, phục vụ audit, filter, timeline hoặc SLA. |
| `end_at` | `DATETIME` | NOT NULL | CHECK: end_at > start_at | Mốc thời gian nghiệp vụ, phục vụ audit, filter, timeline hoặc SLA. |
| `timezone` | `VARCHAR(50)` | NOT NULL; DEFAULT 'Asia/Ho_Chi_Minh' |  | Lưu thông tin `timezone` của bảng `calendar_events` để phục vụ màn hình và logic nghiệp vụ liên quan. |
| `is_all_day` | `BOOLEAN` | NOT NULL; DEFAULT FALSE |  | Cờ boolean dùng để bật/tắt hoặc đánh dấu một trạng thái nghiệp vụ. |
| `recurrence_rule` | `VARCHAR(500)` | NULL |  | Lưu thông tin `recurrence rule` của bảng `calendar_events` để phục vụ màn hình và logic nghiệp vụ liên quan. |
| `visibility` | `ENUM('PRIVATE','INTERNAL')` | NOT NULL; DEFAULT 'PRIVATE' | ENUM: ('PRIVATE','INTERNAL') | Lưu thông tin `visibility` của bảng `calendar_events` để phục vụ màn hình và logic nghiệp vụ liên quan. |
| `status` | `ENUM('ACTIVE','CANCELLED','DONE')` | IDX: idx_calendar_source_status_time; NOT NULL; DEFAULT 'ACTIVE' | ENUM: ('ACTIVE','CANCELLED','DONE') | Trạng thái vòng đời của bản ghi, dùng cho filter, hiển thị badge và chặn/hạn chế thao tác. |
| `created_at` | `DATETIME` | NOT NULL; DEFAULT CURRENT_TIMESTAMP |  | Thời điểm tạo bản ghi, cần cho audit, sắp xếp và truy vết lịch sử. |
| `created_by` | `BIGINT UNSIGNED` | NULL |  | User tạo bản ghi, dùng cho ownership, audit và phân quyền theo người tạo. |
| `updated_at` | `DATETIME` | NULL; DEFAULT NULL; ON UPDATE CURRENT_TIMESTAMP |  | Thời điểm cập nhật gần nhất, giúp UI/backend xác định dữ liệu mới nhất. |
| `updated_by` | `BIGINT UNSIGNED` | NULL |  | User cập nhật bản ghi gần nhất, phục vụ audit và kiểm tra thay đổi. |
| `deleted_at` | `DATETIME` | NULL |  | Thời điểm soft delete bản ghi. |
| `deleted_by` | `BIGINT UNSIGNED` | NULL |  | User thực hiện soft delete bản ghi. |

### 3.40. `calendar_event_attendees`

- **Tác dụng:** Người tham dự sự kiện lịch.
- **Màn hình/UC dùng:** Calendar Event Detail, attendee response tracking
- **Quan hệ chính:** calendar_event_id -> calendar_events(calendar_event_id); user_id -> users(user_id)

| Trường | Loại dữ liệu | Key / Null / Default | Enum / Check | Ý nghĩa / Vì sao có trường này |
|---|---|---|---|---|
| `calendar_event_attendee_id` | `BIGINT UNSIGNED` | PK; NOT NULL; AUTO_INCREMENT |  | Khóa chính định danh duy nhất bản ghi trong bảng. |
| `calendar_event_id` | `BIGINT UNSIGNED` | IDX: idx_calendar_attendees_event; FK: calendar_event_id -> calendar_events(calendar_event_id) [fk_calendar_attendees_event]; NOT NULL |  | Khóa ngoại liên kết tới calendar_events(calendar_event_id), dùng để join dữ liệu và đảm bảo toàn vẹn tham chiếu. |
| `user_id` | `BIGINT UNSIGNED` | IDX: idx_calendar_attendees_user; FK: user_id -> users(user_id) [fk_calendar_attendees_user]; NULL |  | Khóa ngoại liên kết tới users(user_id), dùng để join dữ liệu và đảm bảo toàn vẹn tham chiếu. |
| `attendee_email` | `VARCHAR(150)` | IDX: idx_calendar_attendees_email; NULL |  | Lưu thông tin `attendee email` của bảng `calendar_event_attendees` để phục vụ màn hình và logic nghiệp vụ liên quan. |
| `attendee_name` | `VARCHAR(150)` | NULL |  | Lưu thông tin `attendee name` của bảng `calendar_event_attendees` để phục vụ màn hình và logic nghiệp vụ liên quan. |
| `attendee_role` | `VARCHAR(80)` | NULL |  | Lưu thông tin `attendee role` của bảng `calendar_event_attendees` để phục vụ màn hình và logic nghiệp vụ liên quan. |
| `response_status` | `ENUM('NEEDS_ACTION','ACCEPTED','DECLINED','TENTATIVE')` | NOT NULL; DEFAULT 'NEEDS_ACTION' | ENUM: ('NEEDS_ACTION','ACCEPTED','DECLINED','TENTATIVE') | Trạng thái nghiệp vụ chi tiết, dùng để hiển thị badge, filter và kiểm soát luồng xử lý. |
| `created_at` | `DATETIME` | NOT NULL; DEFAULT CURRENT_TIMESTAMP |  | Thời điểm tạo bản ghi, cần cho audit, sắp xếp và truy vết lịch sử. |

### 3.41. `calendar_event_reminders`

- **Tác dụng:** Nhắc lịch của sự kiện.
- **Màn hình/UC dùng:** Calendar reminder scheduling
- **Quan hệ chính:** calendar_event_id -> calendar_events(calendar_event_id)

| Trường | Loại dữ liệu | Key / Null / Default | Enum / Check | Ý nghĩa / Vì sao có trường này |
|---|---|---|---|---|
| `calendar_event_reminder_id` | `BIGINT UNSIGNED` | PK; NOT NULL; AUTO_INCREMENT |  | Khóa chính định danh duy nhất bản ghi trong bảng. |
| `calendar_event_id` | `BIGINT UNSIGNED` | IDX: idx_calendar_reminders_event; FK: calendar_event_id -> calendar_events(calendar_event_id) [fk_calendar_reminders_event]; NOT NULL |  | Khóa ngoại liên kết tới calendar_events(calendar_event_id), dùng để join dữ liệu và đảm bảo toàn vẹn tham chiếu. |
| `reminder_type` | `ENUM('EMAIL','POPUP','IN_APP')` | NOT NULL; DEFAULT 'IN_APP' | ENUM: ('EMAIL','POPUP','IN_APP') | Loại nghiệp vụ của bản ghi, dùng để phân nhánh xử lý và filter UI. |
| `minutes_before` | `INT UNSIGNED` | NOT NULL; DEFAULT 0 |  | Lưu thông tin `minutes before` của bảng `calendar_event_reminders` để phục vụ màn hình và logic nghiệp vụ liên quan. |
| `scheduled_at` | `DATETIME` | IDX: idx_calendar_reminders_status_schedule; NULL |  | Mốc thời gian nghiệp vụ, phục vụ audit, filter, timeline hoặc SLA. |
| `sent_at` | `DATETIME` | NULL |  | Mốc thời gian nghiệp vụ, phục vụ audit, filter, timeline hoặc SLA. |
| `status` | `ENUM('PENDING','SENT','CANCELLED','FAILED')` | IDX: idx_calendar_reminders_status_schedule; NOT NULL; DEFAULT 'PENDING' | ENUM: ('PENDING','SENT','CANCELLED','FAILED') | Trạng thái vòng đời của bản ghi, dùng cho filter, hiển thị badge và chặn/hạn chế thao tác. |
| `created_at` | `DATETIME` | NOT NULL; DEFAULT CURRENT_TIMESTAMP |  | Thời điểm tạo bản ghi, cần cho audit, sắp xếp và truy vết lịch sử. |

### 3.42. `api_configurations`

- **Tác dụng:** Cấu hình API external, credential explicit và kiểm thử kết nối.
- **Màn hình/UC dùng:** API Management, View API Configuration, Test API Connection
- **Quan hệ chính:** Không có FOREIGN KEY trực tiếp trong CREATE TABLE.

| Trường | Loại dữ liệu | Key / Null / Default | Enum / Check | Ý nghĩa / Vì sao có trường này |
|---|---|---|---|---|
| `api_config_id` | `BIGINT UNSIGNED` | PK; NOT NULL; AUTO_INCREMENT |  | Khóa chính định danh duy nhất bản ghi trong bảng. |
| `api_code` | `VARCHAR(100)` | UNIQUE: uq_api_config_code; NOT NULL |  | Mã nghiệp vụ/technical code ổn định, dùng cho tìm kiếm, mapping và tránh phụ thuộc tên hiển thị. |
| `name` | `VARCHAR(150)` | NOT NULL |  | Tên hiển thị/chính thức của bản ghi, dùng trên danh sách, dropdown và màn chi tiết. |
| `provider_name` | `VARCHAR(150)` | IDX: idx_api_provider_status; NULL |  | Lưu thông tin `provider name` của bảng `api_configurations` để phục vụ màn hình và logic nghiệp vụ liên quan. |
| `purpose` | `VARCHAR(150)` | NULL |  | Lưu thông tin `purpose` của bảng `api_configurations` để phục vụ màn hình và logic nghiệp vụ liên quan. |
| `base_url` | `VARCHAR(500)` | NOT NULL |  | Lưu thông tin `base url` của bảng `api_configurations` để phục vụ màn hình và logic nghiệp vụ liên quan. |
| `default_method` | `ENUM('GET','POST','PUT','PATCH','DELETE')` | NOT NULL; DEFAULT 'POST' | ENUM: ('GET','POST','PUT','PATCH','DELETE') | Lưu thông tin `default method` của bảng `api_configurations` để phục vụ màn hình và logic nghiệp vụ liên quan. |
| `auth_type` | `ENUM('NONE','API_KEY','BEARER_TOKEN','BASIC','OAUTH2','CUSTOM')` | NOT NULL; DEFAULT 'NONE' | ENUM: ('NONE','API_KEY','BEARER_TOKEN','BASIC','OAUTH2','CUSTOM') | Loại nghiệp vụ của bản ghi, dùng để phân nhánh xử lý và filter UI. |
| `api_key_encrypted` | `VARCHAR(700)` | NULL |  | Lưu thông tin `api key encrypted` của bảng `api_configurations` để phục vụ màn hình và logic nghiệp vụ liên quan. |
| `bearer_token_encrypted` | `VARCHAR(700)` | NULL |  | Lưu thông tin `bearer token encrypted` của bảng `api_configurations` để phục vụ màn hình và logic nghiệp vụ liên quan. |
| `basic_username` | `VARCHAR(150)` | NULL |  | Lưu thông tin `basic username` của bảng `api_configurations` để phục vụ màn hình và logic nghiệp vụ liên quan. |
| `basic_password_encrypted` | `VARCHAR(700)` | NULL |  | Lưu thông tin `basic password encrypted` của bảng `api_configurations` để phục vụ màn hình và logic nghiệp vụ liên quan. |
| `oauth_client_id` | `VARCHAR(255)` | NULL |  | Mã định danh bản ghi liên quan, dùng cho join, phân quyền hoặc truy xuất nghiệp vụ. |
| `oauth_client_secret_encrypted` | `VARCHAR(700)` | NULL |  | Lưu thông tin `oauth client secret encrypted` của bảng `api_configurations` để phục vụ màn hình và logic nghiệp vụ liên quan. |
| `oauth_token_url` | `VARCHAR(700)` | NULL |  | Lưu thông tin `oauth token url` của bảng `api_configurations` để phục vụ màn hình và logic nghiệp vụ liên quan. |
| `oauth_scope` | `VARCHAR(500)` | NULL |  | Lưu thông tin `oauth scope` của bảng `api_configurations` để phục vụ màn hình và logic nghiệp vụ liên quan. |
| `body_template_text` | `LONGTEXT` | NULL |  | Lưu thông tin `body template text` của bảng `api_configurations` để phục vụ màn hình và logic nghiệp vụ liên quan. |
| `rate_limit_per_minute` | `INT UNSIGNED` | NULL |  | Lưu thông tin `rate limit per minute` của bảng `api_configurations` để phục vụ màn hình và logic nghiệp vụ liên quan. |
| `monthly_quota` | `INT UNSIGNED` | NULL |  | Lưu thông tin `monthly quota` của bảng `api_configurations` để phục vụ màn hình và logic nghiệp vụ liên quan. |
| `retry_enabled` | `BOOLEAN` | NOT NULL; DEFAULT FALSE |  | Cờ boolean dùng để bật/tắt hoặc đánh dấu một trạng thái nghiệp vụ. |
| `max_retries` | `INT UNSIGNED` | NOT NULL; DEFAULT 0 |  | Lưu thông tin `max retries` của bảng `api_configurations` để phục vụ màn hình và logic nghiệp vụ liên quan. |
| `cache_ttl_seconds` | `INT UNSIGNED` | NULL |  | Lưu thông tin `cache ttl seconds` của bảng `api_configurations` để phục vụ màn hình và logic nghiệp vụ liên quan. |
| `last_test_status` | `ENUM('SUCCESS','FAILED')` | IDX: idx_api_config_test_status; NULL | ENUM: ('SUCCESS','FAILED') | Trạng thái nghiệp vụ chi tiết, dùng để hiển thị badge, filter và kiểm soát luồng xử lý. |
| `last_tested_at` | `DATETIME` | IDX: idx_api_config_test_status; NULL |  | Mốc thời gian nghiệp vụ, phục vụ audit, filter, timeline hoặc SLA. |
| `last_test_message` | `TEXT` | NULL |  | Lưu thông tin `last test message` của bảng `api_configurations` để phục vụ màn hình và logic nghiệp vụ liên quan. |
| `timeout_seconds` | `INT UNSIGNED` | NOT NULL; DEFAULT 30 |  | Lưu thông tin `timeout seconds` của bảng `api_configurations` để phục vụ màn hình và logic nghiệp vụ liên quan. |
| `status` | `ENUM('ACTIVE','INACTIVE','DISABLED')` | IDX: idx_api_config_status, idx_api_provider_status; NOT NULL; DEFAULT 'ACTIVE' | ENUM: ('ACTIVE','INACTIVE','DISABLED') | Trạng thái vòng đời của bản ghi, dùng cho filter, hiển thị badge và chặn/hạn chế thao tác. |
| `created_at` | `DATETIME` | NOT NULL; DEFAULT CURRENT_TIMESTAMP |  | Thời điểm tạo bản ghi, cần cho audit, sắp xếp và truy vết lịch sử. |
| `created_by` | `BIGINT UNSIGNED` | NULL |  | User tạo bản ghi, dùng cho ownership, audit và phân quyền theo người tạo. |
| `updated_at` | `DATETIME` | NULL; DEFAULT NULL; ON UPDATE CURRENT_TIMESTAMP |  | Thời điểm cập nhật gần nhất, giúp UI/backend xác định dữ liệu mới nhất. |
| `updated_by` | `BIGINT UNSIGNED` | NULL |  | User cập nhật bản ghi gần nhất, phục vụ audit và kiểm tra thay đổi. |
| `deleted_at` | `DATETIME` | NULL |  | Thời điểm soft delete bản ghi. |
| `deleted_by` | `BIGINT UNSIGNED` | NULL |  | User thực hiện soft delete bản ghi. |

### 3.43. `api_configuration_headers`

- **Tác dụng:** Header request API tách khỏi JSON.
- **Màn hình/UC dùng:** API Configuration Detail, Test API Connection
- **Quan hệ chính:** api_config_id -> api_configurations(api_config_id)

| Trường | Loại dữ liệu | Key / Null / Default | Enum / Check | Ý nghĩa / Vì sao có trường này |
|---|---|---|---|---|
| `api_configuration_header_id` | `BIGINT UNSIGNED` | PK; NOT NULL; AUTO_INCREMENT |  | Khóa chính định danh duy nhất bản ghi trong bảng. |
| `api_config_id` | `BIGINT UNSIGNED` | UNIQUE: uq_api_header_name; IDX: idx_api_headers_config; FK: api_config_id -> api_configurations(api_config_id) [fk_api_headers_config]; NOT NULL |  | Khóa ngoại liên kết tới api_configurations(api_config_id), dùng để join dữ liệu và đảm bảo toàn vẹn tham chiếu. |
| `header_name` | `VARCHAR(150)` | UNIQUE: uq_api_header_name; NOT NULL |  | Lưu thông tin `header name` của bảng `api_configuration_headers` để phục vụ màn hình và logic nghiệp vụ liên quan. |
| `header_value_encrypted` | `VARCHAR(1000)` | NULL |  | Lưu thông tin `header value encrypted` của bảng `api_configuration_headers` để phục vụ màn hình và logic nghiệp vụ liên quan. |
| `is_secret` | `BOOLEAN` | NOT NULL; DEFAULT TRUE |  | Cờ boolean dùng để bật/tắt hoặc đánh dấu một trạng thái nghiệp vụ. |
| `created_at` | `DATETIME` | NOT NULL; DEFAULT CURRENT_TIMESTAMP |  | Thời điểm tạo bản ghi, cần cho audit, sắp xếp và truy vết lịch sử. |

### 3.44. `api_usage_quotas`

- **Tác dụng:** Quota và counter API theo campus/tháng.
- **Màn hình/UC dùng:** API Management, quota monitoring
- **Quan hệ chính:** api_config_id -> api_configurations(api_config_id); campus_id -> campuses(campus_id)

| Trường | Loại dữ liệu | Key / Null / Default | Enum / Check | Ý nghĩa / Vì sao có trường này |
|---|---|---|---|---|
| `api_usage_quota_id` | `BIGINT UNSIGNED` | PK; NOT NULL; AUTO_INCREMENT |  | Khóa chính định danh duy nhất bản ghi trong bảng. |
| `api_config_id` | `BIGINT UNSIGNED` | UNIQUE: uq_api_quota_config_scope_period; FK: api_config_id -> api_configurations(api_config_id) [fk_api_quota_config]; NOT NULL |  | Khóa ngoại liên kết tới api_configurations(api_config_id), dùng để join dữ liệu và đảm bảo toàn vẹn tham chiếu. |
| `campus_id` | `BIGINT UNSIGNED` | IDX: idx_api_quota_campus_period; FK: campus_id -> campuses(campus_id) [fk_api_quota_campus]; NULL |  | NULL = global quota |
| `campus_scope_key` | `VARCHAR(36)` | UNIQUE: uq_api_quota_config_scope_period; NOT NULL; DEFAULT 'GLOBAL' |  | Lưu thông tin `campus scope key` của bảng `api_usage_quotas` để phục vụ màn hình và logic nghiệp vụ liên quan. |
| `period_yyyymm` | `CHAR(6)` | UNIQUE: uq_api_quota_config_scope_period; IDX: idx_api_quota_campus_period, idx_api_quota_period; NOT NULL |  | YYYYMM |
| `monthly_limit` | `INT UNSIGNED` | NOT NULL |  | Lưu thông tin `monthly limit` của bảng `api_usage_quotas` để phục vụ màn hình và logic nghiệp vụ liên quan. |
| `used_count` | `INT UNSIGNED` | NOT NULL; DEFAULT 0 |  | Merged api_usage_counters table |
| `last_used_at` | `DATETIME` | NULL |  | Mốc thời gian nghiệp vụ, phục vụ audit, filter, timeline hoặc SLA. |
| `created_at` | `DATETIME` | NOT NULL; DEFAULT CURRENT_TIMESTAMP |  | Thời điểm tạo bản ghi, cần cho audit, sắp xếp và truy vết lịch sử. |
| `created_by` | `BIGINT UNSIGNED` | NULL |  | User tạo bản ghi, dùng cho ownership, audit và phân quyền theo người tạo. |
| `updated_at` | `DATETIME` | NULL; DEFAULT NULL; ON UPDATE CURRENT_TIMESTAMP |  | Thời điểm cập nhật gần nhất, giúp UI/backend xác định dữ liệu mới nhất. |
| `updated_by` | `BIGINT UNSIGNED` | NULL |  | User cập nhật bản ghi gần nhất, phục vụ audit và kiểm tra thay đổi. |

### 3.45. `api_request_logs`

- **Tác dụng:** Log request gọi API external.
- **Màn hình/UC dùng:** API Logs, integration debugging
- **Quan hệ chính:** api_config_id -> api_configurations(api_config_id); campus_id -> campuses(campus_id); requested_by -> users(user_id)

| Trường | Loại dữ liệu | Key / Null / Default | Enum / Check | Ý nghĩa / Vì sao có trường này |
|---|---|---|---|---|
| `api_request_log_id` | `BIGINT UNSIGNED` | PK; NOT NULL; AUTO_INCREMENT |  | Khóa chính định danh duy nhất bản ghi trong bảng. |
| `api_config_id` | `BIGINT UNSIGNED` | IDX: idx_api_logs_config_time; FK: api_config_id -> api_configurations(api_config_id) [fk_api_logs_config]; NOT NULL |  | Khóa ngoại liên kết tới api_configurations(api_config_id), dùng để join dữ liệu và đảm bảo toàn vẹn tham chiếu. |
| `campus_id` | `BIGINT UNSIGNED` | IDX: idx_api_logs_campus_time; FK: campus_id -> campuses(campus_id) [fk_api_logs_campus]; NULL |  | Khóa ngoại liên kết tới campuses(campus_id), dùng để join dữ liệu và đảm bảo toàn vẹn tham chiếu. |
| `requested_by` | `BIGINT UNSIGNED` | IDX: idx_api_logs_user_time; FK: requested_by -> users(user_id) [fk_api_logs_user]; NULL |  | Khóa ngoại liên kết tới users(user_id), dùng để join dữ liệu và đảm bảo toàn vẹn tham chiếu. |
| `related_type` | `VARCHAR(80)` | IDX: idx_api_logs_related; NULL |  | Loại nghiệp vụ của bản ghi, dùng để phân nhánh xử lý và filter UI. |
| `related_id` | `BIGINT UNSIGNED` | IDX: idx_api_logs_related; NULL |  | Mã định danh bản ghi liên quan, dùng cho join, phân quyền hoặc truy xuất nghiệp vụ. |
| `endpoint` | `VARCHAR(500)` | NOT NULL |  | Lưu thông tin `endpoint` của bảng `api_request_logs` để phục vụ màn hình và logic nghiệp vụ liên quan. |
| `method` | `ENUM('GET','POST','PUT','PATCH','DELETE')` | NOT NULL | ENUM: ('GET','POST','PUT','PATCH','DELETE') | Lưu thông tin `method` của bảng `api_request_logs` để phục vụ màn hình và logic nghiệp vụ liên quan. |
| `http_status` | `INT` | NULL |  | Trạng thái nghiệp vụ chi tiết, dùng để hiển thị badge, filter và kiểm soát luồng xử lý. |
| `response_time_ms` | `INT UNSIGNED` | NULL |  | Lưu thông tin `response time ms` của bảng `api_request_logs` để phục vụ màn hình và logic nghiệp vụ liên quan. |
| `request_size_bytes` | `BIGINT UNSIGNED` | NULL |  | Lưu thông tin `request size bytes` của bảng `api_request_logs` để phục vụ màn hình và logic nghiệp vụ liên quan. |
| `response_size_bytes` | `BIGINT UNSIGNED` | NULL |  | Lưu thông tin `response size bytes` của bảng `api_request_logs` để phục vụ màn hình và logic nghiệp vụ liên quan. |
| `success` | `BOOLEAN` | IDX: idx_api_logs_success_time; NOT NULL; DEFAULT FALSE |  | Cờ boolean dùng để bật/tắt hoặc đánh dấu một trạng thái nghiệp vụ. |
| `error_code` | `VARCHAR(100)` | NULL |  | Mã nghiệp vụ/technical code ổn định, dùng cho tìm kiếm, mapping và tránh phụ thuộc tên hiển thị. |
| `error_message` | `TEXT` | NULL |  | Lưu thông tin `error message` của bảng `api_request_logs` để phục vụ màn hình và logic nghiệp vụ liên quan. |
| `created_at` | `DATETIME` | IDX: idx_api_logs_config_time, idx_api_logs_campus_time, idx_api_logs_user_time, idx_api_logs_success_time; NOT NULL; DEFAULT CURRENT_TIMESTAMP |  | Thời điểm tạo bản ghi, cần cho audit, sắp xếp và truy vết lịch sử. |

### 3.46. `agenda_templates`

- **Tác dụng:** Header mẫu agenda theo campus/scope.
- **Màn hình/UC dùng:** Agenda Template Management, Create Guest Delegation
- **Quan hệ chính:** campus_id -> campuses(campus_id)

| Trường | Loại dữ liệu | Key / Null / Default | Enum / Check | Ý nghĩa / Vì sao có trường này |
|---|---|---|---|---|
| `agenda_template_id` | `BIGINT UNSIGNED` | PK; NOT NULL; AUTO_INCREMENT |  | Khóa chính định danh duy nhất bản ghi trong bảng. |
| `campus_id` | `BIGINT UNSIGNED` | IDX: idx_agenda_templates_campus_status; FK: campus_id -> campuses(campus_id) [fk_agenda_templates_campus]; NULL |  | Khóa ngoại liên kết tới campuses(campus_id), dùng để join dữ liệu và đảm bảo toàn vẹn tham chiếu. |
| `campus_scope_key` | `VARCHAR(36)` | UNIQUE: uq_agenda_template_scope_name; NOT NULL; DEFAULT 'GLOBAL' |  | Lưu thông tin `campus scope key` của bảng `agenda_templates` để phục vụ màn hình và logic nghiệp vụ liên quan. |
| `name` | `VARCHAR(150)` | UNIQUE: uq_agenda_template_scope_name; NOT NULL |  | Tên hiển thị/chính thức của bản ghi, dùng trên danh sách, dropdown và màn chi tiết. |
| `description` | `TEXT` | NULL |  | Mô tả chi tiết, giúp người dùng hiểu nội dung bản ghi. |
| `status` | `ENUM('ACTIVE','INACTIVE')` | IDX: idx_agenda_templates_status, idx_agenda_templates_campus_status; NOT NULL; DEFAULT 'ACTIVE' | ENUM: ('ACTIVE','INACTIVE') | Trạng thái vòng đời của bản ghi, dùng cho filter, hiển thị badge và chặn/hạn chế thao tác. |
| `created_at` | `DATETIME` | NOT NULL; DEFAULT CURRENT_TIMESTAMP |  | Thời điểm tạo bản ghi, cần cho audit, sắp xếp và truy vết lịch sử. |
| `created_by` | `BIGINT UNSIGNED` | NULL |  | User tạo bản ghi, dùng cho ownership, audit và phân quyền theo người tạo. |
| `updated_at` | `DATETIME` | NULL; DEFAULT NULL; ON UPDATE CURRENT_TIMESTAMP |  | Thời điểm cập nhật gần nhất, giúp UI/backend xác định dữ liệu mới nhất. |
| `updated_by` | `BIGINT UNSIGNED` | NULL |  | User cập nhật bản ghi gần nhất, phục vụ audit và kiểm tra thay đổi. |
| `deleted_at` | `DATETIME` | NULL |  | Thời điểm soft delete bản ghi. |
| `deleted_by` | `BIGINT UNSIGNED` | NULL |  | User thực hiện soft delete bản ghi. |

### 3.47. `agenda_template_items`

- **Tác dụng:** Các dòng timeline trong mẫu agenda.
- **Màn hình/UC dùng:** Agenda Template Detail, Delegation Agenda generation
- **Quan hệ chính:** agenda_template_id -> agenda_templates(agenda_template_id)

| Trường | Loại dữ liệu | Key / Null / Default | Enum / Check | Ý nghĩa / Vì sao có trường này |
|---|---|---|---|---|
| `agenda_template_item_id` | `BIGINT UNSIGNED` | PK; NOT NULL; AUTO_INCREMENT |  | Khóa chính định danh duy nhất bản ghi trong bảng. |
| `agenda_template_id` | `BIGINT UNSIGNED` | IDX: idx_agenda_template_items_template_order; FK: agenda_template_id -> agenda_templates(agenda_template_id) [fk_agenda_template_items_template]; NOT NULL |  | Khóa ngoại liên kết tới agenda_templates(agenda_template_id), dùng để join dữ liệu và đảm bảo toàn vẹn tham chiếu. |
| `display_order` | `INT UNSIGNED` | IDX: idx_agenda_template_items_template_order; NOT NULL; DEFAULT 0 |  | Thứ tự hiển thị trên UI, giúp sắp xếp danh sách ổn định. |
| `start_time` | `TIME` | NULL | CHECK: end_time IS NULL OR start_time IS NULL OR end_time > start_time | Lưu thông tin `start time` của bảng `agenda_template_items` để phục vụ màn hình và logic nghiệp vụ liên quan. |
| `end_time` | `TIME` | NULL | CHECK: end_time IS NULL OR start_time IS NULL OR end_time > start_time | Lưu thông tin `end time` của bảng `agenda_template_items` để phục vụ màn hình và logic nghiệp vụ liên quan. |
| `title` | `VARCHAR(255)` | NOT NULL |  | Tiêu đề hiển thị của bản ghi/nội dung. |
| `description` | `TEXT` | NULL |  | Mô tả chi tiết, giúp người dùng hiểu nội dung bản ghi. |
| `created_at` | `DATETIME` | NOT NULL; DEFAULT CURRENT_TIMESTAMP |  | Thời điểm tạo bản ghi, cần cho audit, sắp xếp và truy vết lịch sử. |

### 3.48. `audit_logs`

- **Tác dụng:** Audit log tổng cho hành động nghiệp vụ.
- **Màn hình/UC dùng:** Admin Audit Log, system audit trail
- **Quan hệ chính:** actor_user_id -> users(user_id); campus_id -> campuses(campus_id)

| Trường | Loại dữ liệu | Key / Null / Default | Enum / Check | Ý nghĩa / Vì sao có trường này |
|---|---|---|---|---|
| `audit_log_id` | `BIGINT UNSIGNED` | PK; NOT NULL; AUTO_INCREMENT |  | Khóa chính định danh duy nhất bản ghi trong bảng. |
| `actor_user_id` | `BIGINT UNSIGNED` | IDX: idx_audit_actor_time; FK: actor_user_id -> users(user_id) [fk_audit_actor]; NULL |  | Khóa ngoại liên kết tới users(user_id), dùng để join dữ liệu và đảm bảo toàn vẹn tham chiếu. |
| `campus_id` | `BIGINT UNSIGNED` | IDX: idx_audit_campus_time; FK: campus_id -> campuses(campus_id) [fk_audit_campus]; NULL |  | Khóa ngoại liên kết tới campuses(campus_id), dùng để join dữ liệu và đảm bảo toàn vẹn tham chiếu. |
| `action` | `VARCHAR(100)` | IDX: idx_audit_action_time; NOT NULL |  | Lưu thông tin `action` của bảng `audit_logs` để phục vụ màn hình và logic nghiệp vụ liên quan. |
| `entity_type` | `VARCHAR(100)` | IDX: idx_audit_entity; NOT NULL |  | Loại nghiệp vụ của bản ghi, dùng để phân nhánh xử lý và filter UI. |
| `entity_id` | `BIGINT UNSIGNED` | IDX: idx_audit_entity; NULL |  | Mã định danh bản ghi liên quan, dùng cho join, phân quyền hoặc truy xuất nghiệp vụ. |
| `ip_address` | `VARCHAR(45)` | NULL |  | Lưu thông tin `ip address` của bảng `audit_logs` để phục vụ màn hình và logic nghiệp vụ liên quan. |
| `user_agent` | `VARCHAR(500)` | NULL |  | Lưu thông tin `user agent` của bảng `audit_logs` để phục vụ màn hình và logic nghiệp vụ liên quan. |
| `request_id` | `VARCHAR(100)` | IDX: idx_audit_request; NULL |  | Mã định danh bản ghi liên quan, dùng cho join, phân quyền hoặc truy xuất nghiệp vụ. |
| `created_at` | `DATETIME` | IDX: idx_audit_actor_time, idx_audit_action_time, idx_audit_campus_time; NOT NULL; DEFAULT CURRENT_TIMESTAMP |  | Thời điểm tạo bản ghi, cần cho audit, sắp xếp và truy vết lịch sử. |

### 3.49. `audit_log_changes`

- **Tác dụng:** Chi tiết thay đổi từng field của audit log.
- **Màn hình/UC dùng:** Audit Log Detail, field-level diff
- **Quan hệ chính:** audit_log_id -> audit_logs(audit_log_id)

| Trường | Loại dữ liệu | Key / Null / Default | Enum / Check | Ý nghĩa / Vì sao có trường này |
|---|---|---|---|---|
| `audit_log_change_id` | `BIGINT UNSIGNED` | PK; NOT NULL; AUTO_INCREMENT |  | Khóa chính định danh duy nhất bản ghi trong bảng. |
| `audit_log_id` | `BIGINT UNSIGNED` | IDX: idx_audit_changes_log; FK: audit_log_id -> audit_logs(audit_log_id) [fk_audit_changes_log]; NOT NULL |  | Khóa ngoại liên kết tới audit_logs(audit_log_id), dùng để join dữ liệu và đảm bảo toàn vẹn tham chiếu. |
| `field_name` | `VARCHAR(150)` | IDX: idx_audit_changes_field; NOT NULL |  | Lưu thông tin `field name` của bảng `audit_log_changes` để phục vụ màn hình và logic nghiệp vụ liên quan. |
| `old_value_text` | `LONGTEXT` | NULL |  | Lưu thông tin `old value text` của bảng `audit_log_changes` để phục vụ màn hình và logic nghiệp vụ liên quan. |
| `new_value_text` | `LONGTEXT` | NULL |  | Lưu thông tin `new value text` của bảng `audit_log_changes` để phục vụ màn hình và logic nghiệp vụ liên quan. |
| `created_at` | `DATETIME` | NOT NULL; DEFAULT CURRENT_TIMESTAMP |  | Thời điểm tạo bản ghi, cần cho audit, sắp xếp và truy vết lịch sử. |

### 3.50. `visit_status_logs`

- **Tác dụng:** Timeline trạng thái của request hoặc campus instance.
- **Màn hình/UC dùng:** Delegation Detail, status timeline, audit of workflow transitions
- **Quan hệ chính:** visit_request_id -> visit_requests(visit_request_id); visit_instance_id -> visit_request_campuses(visit_instance_id); changed_by -> users(user_id)

| Trường | Loại dữ liệu | Key / Null / Default | Enum / Check | Ý nghĩa / Vì sao có trường này |
|---|---|---|---|---|
| `visit_status_log_id` | `BIGINT UNSIGNED` | PK; NOT NULL; AUTO_INCREMENT |  | Khóa chính định danh duy nhất bản ghi trong bảng. |
| `visit_request_id` | `BIGINT UNSIGNED` | IDX: idx_visit_status_request_time; FK: visit_request_id -> visit_requests(visit_request_id) [fk_visit_status_logs_request]; NULL |  | Khóa ngoại liên kết tới visit_requests(visit_request_id), dùng để join dữ liệu và đảm bảo toàn vẹn tham chiếu. |
| `visit_instance_id` | `BIGINT UNSIGNED` | IDX: idx_visit_status_instance_time; FK: visit_instance_id -> visit_request_campuses(visit_instance_id) [fk_visit_status_logs_instance]; NULL |  | Khóa ngoại liên kết tới visit_request_campuses(visit_instance_id), dùng để join dữ liệu và đảm bảo toàn vẹn tham chiếu. |
| `status_owner_type` | `ENUM('REQUEST','CAMPUS_INSTANCE')` | IDX: idx_visit_status_owner_time; NOT NULL; DEFAULT 'CAMPUS_INSTANCE' | ENUM: ('REQUEST','CAMPUS_INSTANCE') | REQUEST=visit_requests.status, CAMPUS_INSTANCE=visit_request_campuses.status |
| `old_status` | `VARCHAR(50)` | NULL |  | Trạng thái nghiệp vụ chi tiết, dùng để hiển thị badge, filter và kiểm soát luồng xử lý. |
| `new_status` | `VARCHAR(50)` | NOT NULL |  | Trạng thái nghiệp vụ chi tiết, dùng để hiển thị badge, filter và kiểm soát luồng xử lý. |
| `changed_by` | `BIGINT UNSIGNED` | IDX: idx_visit_status_changed_by_time; FK: changed_by -> users(user_id) [fk_visit_status_logs_changed_by]; NULL |  | Khóa ngoại liên kết tới users(user_id), dùng để join dữ liệu và đảm bảo toàn vẹn tham chiếu. |
| `reason` | `TEXT` | NULL |  | Lưu thông tin `reason` của bảng `visit_status_logs` để phục vụ màn hình và logic nghiệp vụ liên quan. |
| `changed_at` | `DATETIME` | IDX: idx_visit_status_request_time, idx_visit_status_instance_time, idx_visit_status_owner_time, idx_visit_status_changed_by_time; NOT NULL; DEFAULT CURRENT_TIMESTAMP |  | Mốc thời gian nghiệp vụ, phục vụ audit, filter, timeline hoặc SLA. |
