# PEMS — Data Dictionary (Từ điển dữ liệu)

**Hệ thống:** Partnership Engagement Management System — Quản lý Tiếp đón Đoàn khách & Hợp tác Quốc tế, Đại học FPT.
**CSDL:** `pems_db` · MySQL 8.0+ · InnoDB · `utf8mb4` / `utf8mb4_unicode_ci`.
**Nguồn:** Đặc tả giao diện ver10 (đối chiếu domain model backend ver8).
**File liên quan:** DDL `pems_schema.sql` · ERD `pems_erd.md` (Mermaid) · `pems_erd.dbml` (dbdiagram.io).

---

## 1. Quy ước thiết kế

| Hạng mục | Quy ước |
|---|---|
| Khóa chính (PK) | `CHAR(36)` lưu UUID, sinh ở tầng ứng dụng (`UUID()`). |
| Tên bảng | snake_case, số nhiều (vd `visit_requests`). |
| Tên cột | snake_case. |
| Khóa ngoại (FK) | `FK_<bảng>_<bảng_tham_chiếu>`. |
| Trạng thái | Kiểu `ENUM` để tự-mô-tả và đảm bảo toàn vẹn. |
| Thời gian | `DATETIME` (mốc hệ thống) / `DATE` / `TIME` theo ngữ nghĩa. |
| Cờ boolean | `TINYINT(1)` (0/1). |
| Xóa dữ liệu | Con của 1 cha → `CASCADE`; tham chiếu danh mục/người dùng → `SET NULL`/`RESTRICT` để giữ lịch sử. |

**Tổng quan:** 37 bảng, chia 8 nhóm — RBAC, Tổ chức, Đối tác, Đoàn khách, Công việc, Biên bản/Đánh giá, Truyền thông/Email, Nội dung/Hỗ trợ.

---

## 2. Tác nhân (Actors) & Phân quyền

**Vai trò (`roles.role_code`):**

| Mã | Vai trò | Ghi chú |
|---|---|---|
| `ADMIN` | Quản trị viên | Quản lý tài khoản, phân quyền, campus, department. |
| `HO` | Head Office (FEHO) | Duyệt yêu cầu **liên cơ sở**, giám sát toàn hệ thống. `campus_id` = NULL. |
| `STAFF` | Cán bộ IC/Đối ngoại | Tạo & điều phối đoàn khách tại cơ sở. Có `sub_role` Leader/Staff. |
| `DEPT` | Phòng ban phối hợp | Nhận & xác nhận task hậu cần. Có `sub_role` Leader/Staff. |
| `STUDENT` | Sinh viên | Hỗ trợ/tham gia tiếp đón. |
| `VISITOR` | Khách | Đăng ký lịch tham quan, gửi feedback. `campus_id` = NULL. |

**Nhóm quyền (`permissions.permission_group`):** `guest` (quản lý đoàn khách), `doc` (tài liệu), `user` (người dùng & hệ thống). Gán qua bảng bắc cầu `role_permissions`.

---

## 3. Vòng đời & Business Rules cốt lõi

**Vòng đời đoàn khách (`visit_requests.status`):**
```
Chờ duyệt ──► Đã duyệt ──► Đang chuẩn bị ──► Trong tiếp khách ──► Chờ đóng đoàn ──► Đã đóng đoàn ──► Đã kết thúc
   └──► Từ chối (kèm reject_reason)
```
- **BR-1 (liên cơ sở):** `visit_mode = 'multiple'` → bắt buộc **HO** phê duyệt.
- **BR-2 (campus check):** trừ HO & VISITOR (`campus_id` NULL), người dùng chỉ thao tác trong cơ sở của mình.
- **BR-3 (chốt chặn đóng đoàn):** không cho chuyển sang `Đã đóng đoàn` nếu còn `action_items.status` ∈ {Open, InProgress} hoặc `tasks.status` chưa `done`/`confirmed`.
- **BR-4 (task hậu cần):** mỗi đoàn sinh các `tasks` theo `task_type` (led/car/room/tea); phòng ban phụ trách xác nhận → ký qua `task_actions`.
- **BR-5 (duyệt nội dung):** `news.status` và `partners.status` theo luồng Chờ duyệt → Đã duyệt/Từ chối; bản nháp = Draft/Ẩn.

---

## 4. Từ điển dữ liệu theo bảng

> Ký hiệu: **PK** khóa chính · **FK** khóa ngoại · **UK** duy nhất · *NULL?* cho phép rỗng.

### Nhóm A — Phân quyền (RBAC)

**`roles`** — Danh mục vai trò.

| Cột | Kiểu | NULL? | Mô tả |
|---|---|---|---|
| role_id | CHAR(36) | N | **PK** UUID. |
| role_code | VARCHAR(30) | N | **UK** mã vai trò. |
| name | VARCHAR(100) | N | Tên hiển thị. |
| description | VARCHAR(255) | Y | Mô tả. |
| status | ENUM(active,inactive) | N | Mặc định active. |
| created_at | DATETIME | N | Mốc tạo. |

**`permissions`** — Quyền chi tiết.

| Cột | Kiểu | NULL? | Mô tả |
|---|---|---|---|
| permission_id | CHAR(36) | N | **PK** UUID. |
| permission_code | VARCHAR(60) | N | **UK** mã quyền. |
| name | VARCHAR(150) | N | Tên quyền (tiếng Việt). |
| permission_group | VARCHAR(40) | N | Nhóm: guest/doc/user. |
| description | VARCHAR(255) | Y | Mô tả. |

**`role_permissions`** — Bắc cầu vai trò × quyền (M-N).

| Cột | Kiểu | NULL? | Mô tả |
|---|---|---|---|
| role_id | CHAR(36) | N | **PK/FK** → roles. |
| permission_id | CHAR(36) | N | **PK/FK** → permissions. |
| granted_at | DATETIME | N | Thời điểm gán. |

### Nhóm B — Tổ chức

**`campuses`** — Cơ sở đào tạo.

| Cột | Kiểu | NULL? | Mô tả |
|---|---|---|---|
| campus_id | CHAR(36) | N | **PK** UUID. |
| campus_code | VARCHAR(10) | N | **UK** HN/HCM/DN/CT/QN. |
| name | VARCHAR(150) | N | Tên cơ sở. |
| location | VARCHAR(150) | Y | Tỉnh/Thành. |
| address | VARCHAR(255) | Y | Địa chỉ. |
| ic_head_user_id | CHAR(36) | Y | **FK** → users (Trưởng IC). |
| capacity | INT | Y | Sức chứa. |
| status | ENUM(active,inactive) | N | Hoạt động/Ngừng. |
| created_at | DATETIME | N | Mốc tạo. |

**`departments`** — Phòng ban.

| Cột | Kiểu | NULL? | Mô tả |
|---|---|---|---|
| department_id | CHAR(36) | N | **PK** UUID. |
| name | VARCHAR(150) | N | Tên phòng ban. |
| campus_id | CHAR(36) | N | **FK** → campuses. |
| head_user_id | CHAR(36) | Y | **FK** → users (Trưởng phòng). |
| status | ENUM(active,inactive) | N | Trạng thái. |
| created_at | DATETIME | N | Mốc tạo. |

**`users`** — Tài khoản người dùng.

| Cột | Kiểu | NULL? | Mô tả |
|---|---|---|---|
| user_id | CHAR(36) | N | **PK** UUID. |
| full_name | VARCHAR(150) | N | Họ tên. |
| email | VARCHAR(150) | N | **UK** email đăng nhập. |
| phone | VARCHAR(20) | Y | SĐT. |
| password_hash | VARCHAR(255) | Y | BCrypt (Guest); NULL với nội bộ (demo `Fpt@12345` → SSO). |
| role_id | CHAR(36) | N | **FK** → roles. |
| sub_role | ENUM(Leader,Staff) | Y | Cấp con cho STAFF/DEPT. |
| campus_id | CHAR(36) | Y | **FK** → campuses (NULL với HO/VISITOR). |
| department_id | CHAR(36) | Y | **FK** → departments. |
| gender | ENUM(Nam,Nu,Khac) | Y | Giới tính. |
| avatar_url | VARCHAR(500) | Y | Ảnh đại diện. |
| status | ENUM(PendingApproval,Active,Inactive,Rejected) | N | Trạng thái tài khoản. |
| login_status | ENUM(NeverLoggedIn,LoggedIn) | N | Tình trạng đăng nhập. |
| student_code | VARCHAR(20) | Y | Mã SV (HExxxxxx). |
| major | VARCHAR(150) | Y | Chuyên ngành. |
| nationality | VARCHAR(100) | Y | Quốc tịch (VISITOR). |
| organization | VARCHAR(200) | Y | Tổ chức (VISITOR). |
| manage_scope | VARCHAR(255) | Y | Phạm vi quản lý (ADMIN/HO). |
| created_at / updated_at | DATETIME | N/Y | Mốc tạo/cập nhật. |

### Nhóm C — Đối tác

**`partners`** — Đối tác hợp tác quốc tế.

| Cột | Kiểu | NULL? | Mô tả |
|---|---|---|---|
| partner_id | CHAR(36) | N | **PK** UUID. |
| code | VARCHAR(50) | N | **UK** mã đối tác. |
| name | VARCHAR(200) | N | Tên. |
| country | VARCHAR(100) | Y | Quốc gia. |
| status | ENUM(Draft,Pending,Approved,Rejected) | N | Trạng thái duyệt. |
| created_by | CHAR(36) | Y | **FK** → users. |
| campus_id | CHAR(36) | Y | **FK** → campuses. |
| website / address | VARCHAR | Y | Thông tin chung. |
| description | TEXT | Y | Mô tả. |
| logo_url / cover_url | VARCHAR(500) | Y | Ảnh. |
| created_at / updated_at | DATETIME | N/Y | Mốc thời gian. |

**`partner_contacts`** — Đầu mối liên hệ. `contact_id` **PK**; `partner_id` **FK** → partners; name, email, phone, role_title, department, address.

**`partner_histories`** — Lịch sử hợp tác. `history_id` **PK**; `partner_id` **FK**; event_date (DATE), event.

**`partner_documents`** — Văn bản ký kết. `doc_id` **PK**; `partner_id` **FK**; file_name, file_size, file_type, file_url, upload_date, `uploaded_by` **FK** → users.

**`partner_sync_logs`** — Nhật ký đồng bộ API Outbound. `sync_id` **PK**; `partner_id` **FK**; sync_direction ENUM(PUSH_TO_OUTBOUND, PULL_PROGRAM_FROM_OUTBOUND); sync_status ENUM(SUCCESS, FAILED); message; synced_at.

### Nhóm D — Đoàn khách / Tiếp đón

**`visit_requests`** — Bảng trung tâm.

| Cột | Kiểu | NULL? | Mô tả |
|---|---|---|---|
| visit_id | CHAR(36) | N | **PK** UUID. |
| title | VARCHAR(200) | N | Tên đoàn. |
| guest_org / guest_name | VARCHAR(200) | Y | Tổ chức / đại diện khách. |
| visit_mode | ENUM(single,multiple) | N | Một cơ sở / Liên cơ sở. |
| visit_types | SET(Campus Tour,Hop trao doi,Khac) | Y | Loại hình (đa chọn). |
| purpose | VARCHAR(500) | Y | Mục đích. |
| work_content | TEXT | Y | Nội dung làm việc. |
| pax | INT | Y | Số khách. |
| campus_id | CHAR(36) | N | **FK** → campuses. |
| partner_id | CHAR(36) | Y | **FK** → partners. |
| host_user_id | CHAR(36) | Y | **FK** → users (chủ trì). |
| sender_user_id | CHAR(36) | Y | **FK** → users (người gửi). |
| status | ENUM(8 trạng thái) | N | Vòng đời (mục 3). |
| reject_reason | VARCHAR(500) | Y | Lý do từ chối. |
| scheduled_time | DATETIME | Y | Thời gian dự kiến. |
| created_by | CHAR(36) | Y | **FK** → users. |
| created_at / updated_at | DATETIME | N/Y | Mốc thời gian. |

**`visit_details`** — Lịch theo cơ sở (đoàn liên cơ sở). `detail_id` **PK**; `visit_id` **FK**; `campus_id` **FK**; visit_date, start_time, end_time, time_zone.

**`visit_participants`** — Người tham gia. `participant_id` **PK**; `visit_id` **FK**; `user_id` **FK** (NULL nếu khách ngoài); external_name; participant_role ENUM(Host,Supporter,OtherDept,Student); is_host, confirmed.

**`visit_agendas`** — Lịch trình đoàn. `agenda_id` **PK**; `visit_id` **FK**; start_time, end_time, content, sequence_order.

**`agenda_templates`** — Mẫu lịch trình. `template_id` **PK**; name, description, `created_by` **FK** → users, created_at.

**`agenda_template_items`** — Mục của mẫu. `item_id` **PK**; `template_id` **FK**; start_time, end_time, content, sequence_order.

### Nhóm E — Công việc hậu cần

**`tasks`** — Công việc/yêu cầu hậu cần.

| Cột | Kiểu | NULL? | Mô tả |
|---|---|---|---|
| task_id | CHAR(36) | N | **PK** UUID. |
| visit_id | CHAR(36) | N | **FK** → visit_requests. |
| task_type | ENUM(led,car,room,tea,other) | N | Loại task. |
| title | VARCHAR(200) | N | Tiêu đề. |
| description | TEXT | Y | Nội dung. |
| assigned_to_user_id | CHAR(36) | Y | **FK** → users. |
| department_id | CHAR(36) | Y | **FK** → departments. |
| status | ENUM(pending,confirmed,rejected,waiting_for_approval,done) | N | Trạng thái. |
| proposed_time / proposed_content | VARCHAR | Y | Đề xuất. |
| proposed_by | ENUM(HO,STAFF,DEPT) | Y | Bên đề xuất. |
| reject_reason | VARCHAR(500) | Y | Lý do từ chối. |
| created_by | CHAR(36) | Y | **FK** → users. |
| created_at / updated_at | DATETIME | N/Y | Mốc thời gian. |

**`task_actions`** — Chữ ký phê duyệt. `action_id` **PK**; `task_id` **FK**; action_type ENUM(bg1_signed,bg2_signed,nt1_signed,nt2_signed); `approved_by` **FK** → users; signature_date; note.

**`action_items`** — Đầu việc chốt chặn đóng đoàn. `action_item_id` **PK**; `visit_id` **FK**; title, description, `assignee_user_id` **FK** → users, due_date, status ENUM(Open,InProgress,Done,Cancelled), `created_by` **FK**, created_at.

### Nhóm F — Biên bản & Đánh giá

**`minutes`** — Biên bản họp. `minute_id` **PK**; `visit_id` **FK**; name, guest_name, file_url, upload_date, is_draft, `created_by` **FK**, created_at.

**`minute_participants`** — Người ký biên bản. `mp_id` **PK**; `minute_id` **FK**; `user_id` **FK** (nếu nội bộ); name, role_title, organization, is_internal, is_partner, confirmed.

**`feedbacks`** — Tổng hợp đánh giá. `feedback_id` **PK**; `visit_id` **FK**; guest_name, average_rating DECIMAL(2,1), feedback_date.

**`feedback_items`** — Phiếu đánh giá. `item_id` **PK**; `feedback_id` **FK**; reviewer_name, `reviewer_user_id` **FK**, rating, space_rating, support_rating (1-5), comment, item_date.

### Nhóm G — Truyền thông & Email

**`news`** — Tin tức/Review. `news_id` **PK**; news_type ENUM(News,Review); title, summary, body (LONGTEXT), image_url, `created_by` **FK**, `campus_id` **FK**, status ENUM(Cho Duyet,Da Duyet,Tu Choi,An), published_date, created_at/updated_at.

**`email_templates`** — Mẫu email. `template_id` **PK**; name, subject, description, body, `created_by` **FK**, `campus_id` **FK**, status ENUM(InUse,NotInUse), created_at.

**`sent_emails`** — Email đã gửi. `email_id` **PK**; program, `visit_id` **FK**, subject, body, `sender_user_id` **FK**, `campus_id` **FK**, send_time, status ENUM(Thanh cong,Dang xu ly,That bai), has_new_reply.

**`sent_email_recipients`** — Người nhận. `recipient_id` **PK**; `email_id` **FK**; email, name, `partner_contact_id` **FK**, delivery_status ENUM(Thanh cong,Dang xu ly,That bai).

### Nhóm H — Nội dung & Hỗ trợ

**`documents`** — Tài liệu hệ thống. `document_id` **PK**; file_name, file_size, file_type, file_url, category, description, `uploaded_by` **FK**, `campus_id` **FK**, `visit_id` **FK**, upload_date.

**`galleries`** — Album ảnh. `gallery_id` **PK**; name, description, `uploaded_by` **FK**, `campus_id` **FK**, created_at.

**`gallery_images`** — Ảnh album. `image_id` **PK**; `gallery_id` **FK**; url, caption, upload_date.

**`gallery_locations`** — Địa điểm chụp. `location_id` **PK**; name, description, `campus_id` **FK**.

**`gallery_location_images`** — Ảnh theo địa điểm. `gli_id` **PK**; `location_id` **FK**; url, caption.

**`faqs`** — Câu hỏi thường gặp. `faq_id` **PK**; question, answer, category, status ENUM(Draft,Published,Archived), `created_by` **FK**, created_at/updated_at.

**`reports`** — Báo cáo thống kê. `report_id` **PK**; title, period, report_type ENUM(Visit,Task,Partner,Combined), `campus_id` **FK**, data_json (JSON), `generated_by` **FK**, generated_at.

**`notifications`** — Thông báo. `notification_id` **PK**; `user_id` **FK**; title, message, type, related_entity_type, related_entity_id, is_read, created_at.

---

## 5. Ghi chú & điểm cần chốt
1. **Campus code:** schema dùng bộ `HN, HCM, DN, CT, QN`. Backend ver8 từng dùng `HL` (Hòa Lạc) và lẫn `qn`. → Cần chốt thống nhất trước khi seed (xem `claude_setup/memory.md` Open Question #1).
2. **PK kiểu UUID `CHAR(36)`** theo convention PEMS; nếu backend chuyển `BIGINT AUTO_INCREMENT` cần cập nhật lại toàn bộ FK.
3. **ENUM tiếng Việt không dấu** (vd `Cho duyet`, `Thanh cong`) để an toàn với mọi client/collation; nhãn hiển thị tiếng Việt có dấu xử lý ở tầng UI.
4. **Logistics:** ver10 gộp yêu cầu hậu cần (xe/phòng/teabreak/LED của ver8) vào bảng `tasks` qua `task_type`; không tách bảng `resource_requests` riêng.
5. **`reports.data_json`** dùng kiểu JSON cho số liệu thống kê linh hoạt (biểu đồ theo tháng, loại khách, tỉ lệ hoàn thành...).
