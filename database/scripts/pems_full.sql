-- =====================================================================
--  PEMS — Partnership Engagement Management System  (FPT Education)
--  FILE TỔNG HỢP DUY NHẤT (CANONICAL) — chạy 1 lượt ra trạng thái cuối.
--  Gộp từ: pems_schema.sql + pems_migration_4roles.sql + pems_security_upgrade.sql
--
--  MySQL 8.0+ | InnoDB | utf8mb4 | utf8mb4_unicode_ci
--
--  ĐÃ TÍCH HỢP SẴN (không còn ALTER tạo-rồi-sửa):
--   • users.title + composite index (role_id,sub_role)/(department_id,sub_role)
--     → phân biệt 4 vai trò STAFF/DEPT × Leader/Staff.
--   • Cột chuẩn created_by/updated_by/deleted_at (soft-delete) cho bảng trọng yếu.
--   • UNIQUE(student_code) + CHECK (rating 1..5, pax>=0).
--   • 3 bảng log: audit_logs, login_logs, visit_status_logs.
--   • Index list/filter/sort + FULLTEXT search.
--   • FK_users_departments = ON DELETE RESTRICT (lưới an toàn cho soft-delete phòng ban).
--   • Trigger ép toàn vẹn vai trò + View v_users_effective_role.
--
--  QUY ƯỚC: PK nghiệp vụ = CHAR(36) UUID (sinh ở tầng app). Bảng log PK = BIGINT.
-- =====================================================================

SET NAMES utf8mb4;
SET FOREIGN_KEY_CHECKS = 0;

CREATE DATABASE IF NOT EXISTS pems_db
  CHARACTER SET utf8mb4
  COLLATE utf8mb4_unicode_ci;
USE pems_db;

-- =====================================================================
--  A. PHÂN QUYỀN (RBAC)
-- =====================================================================

CREATE TABLE roles (
  role_id      CHAR(36)     NOT NULL COMMENT 'Khóa chính (UUID)',
  role_code    VARCHAR(30)  NOT NULL COMMENT 'Mã vai trò: ADMIN, HO, STAFF, DEPT, STUDENT, VISITOR',
  name         VARCHAR(100) NOT NULL COMMENT 'Tên hiển thị',
  description  VARCHAR(255) NULL     COMMENT 'Mô tả vai trò',
  status       ENUM('active','inactive') NOT NULL DEFAULT 'active',
  created_at   DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (role_id),
  UNIQUE KEY uq_roles_code (role_code)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
  COMMENT='Danh mục vai trò người dùng (RBAC)';

CREATE TABLE permissions (
  permission_id    CHAR(36)     NOT NULL COMMENT 'Khóa chính (UUID)',
  permission_code  VARCHAR(60)  NOT NULL COMMENT 'Mã quyền',
  name             VARCHAR(150) NOT NULL COMMENT 'Tên quyền (tiếng Việt)',
  permission_group VARCHAR(40)  NOT NULL COMMENT 'Nhóm quyền: guest, doc, user...',
  description      VARCHAR(255) NULL,
  PRIMARY KEY (permission_id),
  UNIQUE KEY uq_permissions_code (permission_code),
  KEY idx_permissions_group (permission_group)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
  COMMENT='Danh mục quyền truy cập chi tiết';

CREATE TABLE role_permissions (
  role_id        CHAR(36) NOT NULL,
  permission_id  CHAR(36) NOT NULL,
  granted_at     DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (role_id, permission_id),
  KEY idx_rp_permission (permission_id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
  COMMENT='Gán quyền cho vai trò (ma trận phân quyền)';

-- =====================================================================
--  B. TỔ CHỨC — campuses / departments / users
-- =====================================================================

CREATE TABLE campuses (
  campus_id    CHAR(36)     NOT NULL COMMENT 'Khóa chính (UUID)',
  campus_code  VARCHAR(10)  NOT NULL COMMENT 'Mã cơ sở: HN, HCM, DN, CT, QN',
  name         VARCHAR(150) NOT NULL,
  location     VARCHAR(150) NULL     COMMENT 'Tỉnh/Thành phố',
  address      VARCHAR(255) NULL,
  ic_head_user_id CHAR(36)  NULL     COMMENT 'Trưởng phòng IC/Đối ngoại (FK users)',
  capacity     INT          NULL,
  status       ENUM('active','inactive') NOT NULL DEFAULT 'active',
  created_at   DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (campus_id),
  UNIQUE KEY uq_campuses_code (campus_code),
  KEY idx_campuses_ichead (ic_head_user_id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
  COMMENT='Danh mục cơ sở đào tạo FPT';

CREATE TABLE departments (
  department_id  CHAR(36)     NOT NULL COMMENT 'Khóa chính (UUID)',
  name           VARCHAR(150) NOT NULL,
  campus_id      CHAR(36)     NOT NULL COMMENT 'Cơ sở (FK campuses)',
  head_user_id   CHAR(36)     NULL     COMMENT 'Trưởng phòng (FK users)',
  status         ENUM('active','inactive') NOT NULL DEFAULT 'active' COMMENT 'inactive = soft-delete phòng ban',
  created_at     DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (department_id),
  KEY idx_departments_campus (campus_id),
  KEY idx_departments_head (head_user_id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
  COMMENT='Phòng ban chức năng theo cơ sở';

-- users: ĐÃ TÍCH HỢP title + cột chuẩn + composite index + UNIQUE(student_code)
CREATE TABLE users (
  user_id        CHAR(36)     NOT NULL COMMENT 'Khóa chính (UUID)',
  full_name      VARCHAR(150) NOT NULL COMMENT 'Họ và tên',
  email          VARCHAR(150) NOT NULL COMMENT 'Email đăng nhập (duy nhất)',
  phone          VARCHAR(20)  NULL,
  password_hash  VARCHAR(255) NULL     COMMENT 'BCrypt cho Guest, NULL = chi SSO',
  role_id        CHAR(36)     NOT NULL COMMENT 'Vai trò (FK roles)',
  sub_role       ENUM('Leader','Staff') NULL COMMENT 'Cấp con cho STAFF/DEPT: Leader(Trưởng phòng)/Staff(Nhân viên)',
  title          VARCHAR(100) NULL     COMMENT 'Chức danh hiển thị tự do; NULL với role không thuộc phòng ban',
  campus_id      CHAR(36)     NULL     COMMENT 'Cơ sở (NULL với HO & Guest) - FK campuses',
  department_id  CHAR(36)     NULL     COMMENT 'Phòng ban (FK departments)',
  gender         ENUM('Nam','Nu','Khac') NULL,
  avatar_url     VARCHAR(500) NULL,
  status         ENUM('PendingApproval','Active','Inactive','Rejected') NOT NULL DEFAULT 'PendingApproval',
  login_status   ENUM('NeverLoggedIn','LoggedIn') NOT NULL DEFAULT 'NeverLoggedIn',
  student_code   VARCHAR(20)  NULL     COMMENT 'Mã SV (HExxxxxx) - chỉ STUDENT',
  major          VARCHAR(150) NULL     COMMENT 'Chuyên ngành - chỉ STUDENT',
  nationality    VARCHAR(100) NULL     COMMENT 'Quốc tịch - chỉ VISITOR',
  organization   VARCHAR(200) NULL     COMMENT 'Tổ chức - chỉ VISITOR',
  manage_scope   VARCHAR(255) NULL     COMMENT 'Phạm vi quản lý - ADMIN/HO',
  created_at     DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP,
  updated_at     DATETIME     NULL ON UPDATE CURRENT_TIMESTAMP,
  created_by     CHAR(36)     NULL     COMMENT 'FK users - người tạo',
  updated_by     CHAR(36)     NULL     COMMENT 'FK users - người sửa gần nhất',
  deleted_at     DATETIME     NULL     COMMENT 'Soft delete: NULL = còn hiệu lực',
  PRIMARY KEY (user_id),
  UNIQUE KEY uq_users_email (email),
  UNIQUE KEY uq_users_student_code (student_code),
  KEY idx_users_role_subrole (role_id, sub_role),
  KEY idx_users_campus (campus_id),
  KEY idx_users_dept_subrole (department_id, sub_role),
  KEY idx_users_campus_role_subrole (campus_id, role_id, sub_role),
  KEY idx_users_status (status),
  KEY idx_users_deleted_at (deleted_at)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
  COMMENT='Tài khoản người dùng (mọi vai trò)';

-- =====================================================================
--  C. ĐỐI TÁC
-- =====================================================================

-- partners: ĐÃ TÍCH HỢP updated_by + deleted_at
CREATE TABLE partners (
  partner_id   CHAR(36)     NOT NULL COMMENT 'Khóa chính (UUID)',
  code         VARCHAR(50)  NOT NULL COMMENT 'Mã đối tác',
  name         VARCHAR(200) NOT NULL,
  country      VARCHAR(100) NULL,
  status       ENUM('Draft','Pending','Approved','Rejected') NOT NULL DEFAULT 'Draft',
  created_by   CHAR(36)     NULL COMMENT 'Người tạo (FK users)',
  campus_id    CHAR(36)     NULL COMMENT 'Cơ sở phụ trách (FK campuses)',
  website      VARCHAR(255) NULL,
  address      VARCHAR(255) NULL,
  description  TEXT         NULL,
  logo_url     VARCHAR(500) NULL,
  cover_url    VARCHAR(500) NULL,
  created_at   DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP,
  updated_at   DATETIME     NULL ON UPDATE CURRENT_TIMESTAMP,
  updated_by   CHAR(36)     NULL COMMENT 'FK users',
  deleted_at   DATETIME     NULL COMMENT 'Soft delete',
  PRIMARY KEY (partner_id),
  UNIQUE KEY uq_partners_code (code),
  KEY idx_partners_createdby (created_by),
  KEY idx_partners_campus (campus_id),
  KEY idx_partners_status_campus (status, campus_id),
  KEY idx_partners_deleted_at (deleted_at)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
  COMMENT='Đối tác hợp tác quốc tế';

CREATE TABLE partner_contacts (
  contact_id   CHAR(36)     NOT NULL COMMENT 'Khóa chính (UUID)',
  partner_id   CHAR(36)     NOT NULL COMMENT 'Đối tác (FK partners)',
  name         VARCHAR(150) NOT NULL,
  email        VARCHAR(150) NULL,
  phone        VARCHAR(20)  NULL,
  role_title   VARCHAR(120) NULL COMMENT 'Chức vụ',
  department   VARCHAR(150) NULL COMMENT 'Phòng ban/Bộ phận',
  address      VARCHAR(255) NULL,
  PRIMARY KEY (contact_id),
  KEY idx_pcontacts_partner (partner_id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
  COMMENT='Đầu mối liên hệ phía đối tác';

CREATE TABLE partner_histories (
  history_id   CHAR(36)     NOT NULL COMMENT 'Khóa chính (UUID)',
  partner_id   CHAR(36)     NOT NULL COMMENT 'Đối tác (FK partners)',
  event_date   DATE         NOT NULL,
  event        VARCHAR(255) NOT NULL,
  PRIMARY KEY (history_id),
  KEY idx_phist_partner (partner_id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
  COMMENT='Lịch sử hợp tác của đối tác';

CREATE TABLE partner_documents (
  doc_id       CHAR(36)     NOT NULL COMMENT 'Khóa chính (UUID)',
  partner_id   CHAR(36)     NOT NULL COMMENT 'Đối tác (FK partners)',
  file_name    VARCHAR(255) NOT NULL,
  file_size    VARCHAR(20)  NULL,
  file_type    VARCHAR(20)  NULL,
  file_url     VARCHAR(500) NOT NULL,
  upload_date  DATE         NOT NULL,
  uploaded_by  CHAR(36)     NULL COMMENT 'FK users',
  PRIMARY KEY (doc_id),
  KEY idx_pdocs_partner (partner_id),
  KEY idx_pdocs_uploader (uploaded_by)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
  COMMENT='Văn bản ký kết với đối tác (MoU, MoA...)';

CREATE TABLE partner_sync_logs (
  sync_id        CHAR(36) NOT NULL COMMENT 'Khóa chính (UUID)',
  partner_id     CHAR(36) NULL COMMENT 'FK partners',
  sync_direction ENUM('PUSH_TO_OUTBOUND','PULL_PROGRAM_FROM_OUTBOUND') NOT NULL,
  sync_status    ENUM('SUCCESS','FAILED') NOT NULL,
  message        VARCHAR(500) NULL,
  synced_at      DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (sync_id),
  KEY idx_psync_partner (partner_id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
  COMMENT='Nhật ký đồng bộ API với trang Outbound';

-- =====================================================================
--  D. ĐOÀN KHÁCH / TIẾP ĐÓN
-- =====================================================================

-- visit_requests: ĐÃ TÍCH HỢP updated_by + deleted_at + CHECK(pax) + composite index
CREATE TABLE visit_requests (
  visit_id      CHAR(36)     NOT NULL COMMENT 'Khóa chính (UUID)',
  title         VARCHAR(200) NOT NULL COMMENT 'Tên đoàn khách',
  guest_org     VARCHAR(200) NULL,
  guest_name    VARCHAR(200) NULL,
  visit_mode    ENUM('single','multiple') NOT NULL DEFAULT 'single',
  visit_types   SET('Campus Tour','Hop trao doi','Khac') NULL,
  purpose       VARCHAR(500) NULL,
  work_content  TEXT         NULL,
  pax           INT          NULL     COMMENT 'Số lượng khách',
  campus_id     CHAR(36)     NOT NULL COMMENT 'Cơ sở chính (FK campuses)',
  partner_id    CHAR(36)     NULL,
  host_user_id  CHAR(36)     NULL,
  sender_user_id CHAR(36)    NULL,
  status        ENUM('Cho duyet','Tu choi','Da duyet','Dang chuan bi',
                     'Trong tiep khach','Cho dong doan','Da dong doan','Da ket thuc')
                NOT NULL DEFAULT 'Cho duyet',
  reject_reason VARCHAR(500) NULL,
  scheduled_time DATETIME    NULL,
  created_by    CHAR(36)     NULL,
  created_at    DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP,
  updated_at    DATETIME     NULL ON UPDATE CURRENT_TIMESTAMP,
  updated_by    CHAR(36)     NULL COMMENT 'FK users',
  deleted_at    DATETIME     NULL COMMENT 'Soft delete',
  PRIMARY KEY (visit_id),
  KEY idx_visit_campus_status_time (campus_id, status, scheduled_time),
  KEY idx_visit_partner_status (partner_id, status),
  KEY idx_visit_host (host_user_id),
  KEY idx_visit_sender (sender_user_id),
  KEY idx_visit_status (status),
  KEY idx_visit_deleted_at (deleted_at),
  CONSTRAINT chk_visit_pax CHECK (pax IS NULL OR pax >= 0)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
  COMMENT='Yêu cầu tiếp khách / Đoàn khách (bảng trung tâm)';

CREATE TABLE visit_details (
  detail_id    CHAR(36) NOT NULL COMMENT 'Khóa chính (UUID)',
  visit_id     CHAR(36) NOT NULL COMMENT 'FK visit_requests',
  campus_id    CHAR(36) NOT NULL COMMENT 'FK campuses',
  visit_date   DATE     NULL,
  start_time   TIME     NULL,
  end_time     TIME     NULL,
  time_zone    VARCHAR(20) NULL DEFAULT 'GMT+7',
  PRIMARY KEY (detail_id),
  KEY idx_vdetail_visit (visit_id),
  KEY idx_vdetail_campus (campus_id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
  COMMENT='Lịch tiếp đón chi tiết theo cơ sở';

CREATE TABLE visit_participants (
  participant_id CHAR(36) NOT NULL COMMENT 'Khóa chính (UUID)',
  visit_id       CHAR(36) NOT NULL COMMENT 'FK visit_requests',
  user_id        CHAR(36) NULL     COMMENT 'FK users (NULL nếu khách ngoài)',
  external_name  VARCHAR(150) NULL,
  participant_role ENUM('Host','Supporter','OtherDept','Student') NOT NULL DEFAULT 'Supporter',
  is_host        TINYINT(1) NOT NULL DEFAULT 0,
  confirmed      TINYINT(1) NOT NULL DEFAULT 0,
  PRIMARY KEY (participant_id),
  KEY idx_vpart_visit (visit_id),
  KEY idx_vpart_user (user_id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
  COMMENT='Người tham gia tiếp đón đoàn';

CREATE TABLE visit_agendas (
  agenda_id      CHAR(36) NOT NULL COMMENT 'Khóa chính (UUID)',
  visit_id       CHAR(36) NOT NULL COMMENT 'FK visit_requests',
  start_time     TIME     NULL,
  end_time       TIME     NULL,
  content        VARCHAR(500) NOT NULL,
  sequence_order INT      NOT NULL DEFAULT 0,
  PRIMARY KEY (agenda_id),
  KEY idx_vagenda_visit (visit_id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
  COMMENT='Lịch trình chi tiết của đoàn khách';

CREATE TABLE agenda_templates (
  template_id  CHAR(36)     NOT NULL COMMENT 'Khóa chính (UUID)',
  name         VARCHAR(150) NOT NULL,
  description  VARCHAR(500) NULL,
  created_by   CHAR(36)     NULL COMMENT 'FK users',
  created_at   DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (template_id),
  KEY idx_atpl_createdby (created_by)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
  COMMENT='Mẫu lịch trình (Agenda Template)';

CREATE TABLE agenda_template_items (
  item_id        CHAR(36) NOT NULL COMMENT 'Khóa chính (UUID)',
  template_id    CHAR(36) NOT NULL COMMENT 'FK agenda_templates',
  start_time     TIME     NULL,
  end_time       TIME     NULL,
  content        VARCHAR(500) NOT NULL,
  sequence_order INT      NOT NULL DEFAULT 0,
  PRIMARY KEY (item_id),
  KEY idx_atplitem_tpl (template_id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
  COMMENT='Mục trong mẫu lịch trình';

-- =====================================================================
--  E. CÔNG VIỆC HẬU CẦN
-- =====================================================================

-- tasks: ĐÃ TÍCH HỢP updated_by + deleted_at + composite (department_id,status)
CREATE TABLE tasks (
  task_id            CHAR(36) NOT NULL COMMENT 'Khóa chính (UUID)',
  visit_id           CHAR(36) NOT NULL COMMENT 'FK visit_requests',
  task_type          ENUM('led','car','room','tea','other') NOT NULL,
  title              VARCHAR(200) NOT NULL,
  description        TEXT         NULL,
  assigned_to_user_id CHAR(36)    NULL COMMENT 'FK users',
  department_id      CHAR(36)     NULL COMMENT 'FK departments',
  status             ENUM('pending','confirmed','rejected','waiting_for_approval','done')
                     NOT NULL DEFAULT 'pending',
  proposed_time      VARCHAR(100) NULL,
  proposed_content   VARCHAR(500) NULL,
  proposed_by        ENUM('HO','STAFF','DEPT') NULL,
  reject_reason      VARCHAR(500) NULL,
  created_by         CHAR(36)     NULL COMMENT 'FK users',
  created_at         DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP,
  updated_at         DATETIME     NULL ON UPDATE CURRENT_TIMESTAMP,
  updated_by         CHAR(36)     NULL COMMENT 'FK users',
  deleted_at         DATETIME     NULL COMMENT 'Soft delete',
  PRIMARY KEY (task_id),
  KEY idx_tasks_visit (visit_id),
  KEY idx_tasks_assignee (assigned_to_user_id),
  KEY idx_tasks_dept_status (department_id, status),
  KEY idx_tasks_status (status),
  KEY idx_tasks_deleted_at (deleted_at)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
  COMMENT='Công việc/yêu cầu hậu cần cho đoàn khách';

CREATE TABLE task_actions (
  action_id    CHAR(36) NOT NULL COMMENT 'Khóa chính (UUID)',
  task_id      CHAR(36) NOT NULL COMMENT 'FK tasks',
  action_type  ENUM('bg1_signed','bg2_signed','nt1_signed','nt2_signed') NOT NULL,
  approved_by  CHAR(36) NULL COMMENT 'Người ký (FK users)',
  signature_date DATETIME NULL,
  note         VARCHAR(500) NULL,
  PRIMARY KEY (action_id),
  KEY idx_taction_task (task_id),
  KEY idx_taction_user (approved_by)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
  COMMENT='Hành động/chữ ký phê duyệt công việc';

CREATE TABLE action_items (
  action_item_id CHAR(36) NOT NULL COMMENT 'Khóa chính (UUID)',
  visit_id       CHAR(36) NOT NULL COMMENT 'FK visit_requests',
  title          VARCHAR(200) NOT NULL,
  description    TEXT     NULL,
  assignee_user_id CHAR(36) NULL COMMENT 'FK users',
  due_date       DATE     NULL,
  status         ENUM('Open','InProgress','Done','Cancelled') NOT NULL DEFAULT 'Open',
  created_by     CHAR(36) NULL COMMENT 'FK users',
  created_at     DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (action_item_id),
  KEY idx_aitem_visit (visit_id),
  KEY idx_aitem_assignee (assignee_user_id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
  COMMENT='Đầu việc phát sinh sau họp (chốt chặn đóng đoàn)';

-- =====================================================================
--  F. BIÊN BẢN & ĐÁNH GIÁ
-- =====================================================================

CREATE TABLE minutes (
  minute_id   CHAR(36)     NOT NULL COMMENT 'Khóa chính (UUID)',
  visit_id    CHAR(36)     NOT NULL COMMENT 'FK visit_requests',
  name        VARCHAR(200) NOT NULL,
  guest_name  VARCHAR(200) NULL,
  file_url    VARCHAR(500) NULL,
  upload_date DATE         NULL,
  is_draft    TINYINT(1)   NOT NULL DEFAULT 1,
  created_by  CHAR(36)     NULL COMMENT 'FK users',
  created_at  DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (minute_id),
  KEY idx_minutes_visit (visit_id),
  KEY idx_minutes_createdby (created_by)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
  COMMENT='Biên bản cuộc họp với đoàn';

CREATE TABLE minute_participants (
  mp_id        CHAR(36)     NOT NULL COMMENT 'Khóa chính (UUID)',
  minute_id    CHAR(36)     NOT NULL COMMENT 'FK minutes',
  user_id      CHAR(36)     NULL COMMENT 'FK users (nếu nội bộ)',
  name         VARCHAR(150) NOT NULL,
  role_title   VARCHAR(120) NULL,
  organization VARCHAR(200) NULL,
  is_internal  TINYINT(1)   NOT NULL DEFAULT 0,
  is_partner   TINYINT(1)   NOT NULL DEFAULT 0,
  confirmed    TINYINT(1)   NOT NULL DEFAULT 0,
  PRIMARY KEY (mp_id),
  KEY idx_mpart_minute (minute_id),
  KEY idx_mpart_user (user_id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
  COMMENT='Người tham gia ký biên bản';

-- feedbacks: ĐÃ TÍCH HỢP CHECK(average_rating)
CREATE TABLE feedbacks (
  feedback_id    CHAR(36) NOT NULL COMMENT 'Khóa chính (UUID)',
  visit_id       CHAR(36) NOT NULL COMMENT 'FK visit_requests',
  guest_name     VARCHAR(200) NULL,
  average_rating DECIMAL(2,1) NULL COMMENT 'Điểm trung bình (1.0-5.0)',
  feedback_date  DATE     NULL,
  PRIMARY KEY (feedback_id),
  KEY idx_feedback_visit (visit_id),
  CONSTRAINT chk_fb_avg CHECK (average_rating IS NULL OR average_rating BETWEEN 0 AND 5)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
  COMMENT='Tổng hợp đánh giá của đoàn khách';

-- feedback_items: ĐÃ TÍCH HỢP CHECK(rating 1..5)
CREATE TABLE feedback_items (
  item_id          CHAR(36) NOT NULL COMMENT 'Khóa chính (UUID)',
  feedback_id      CHAR(36) NOT NULL COMMENT 'FK feedbacks',
  reviewer_name    VARCHAR(150) NULL,
  reviewer_user_id CHAR(36) NULL COMMENT 'FK users (nếu nội bộ)',
  rating           TINYINT  NULL COMMENT 'Điểm tổng (1-5)',
  space_rating     TINYINT  NULL COMMENT 'Điểm không gian (1-5)',
  support_rating   TINYINT  NULL COMMENT 'Điểm hỗ trợ (1-5)',
  comment          TEXT     NULL,
  item_date        DATE     NULL,
  PRIMARY KEY (item_id),
  KEY idx_fitem_feedback (feedback_id),
  KEY idx_fitem_reviewer (reviewer_user_id),
  CONSTRAINT chk_fi_rating         CHECK (rating         IS NULL OR rating         BETWEEN 1 AND 5),
  CONSTRAINT chk_fi_space_rating   CHECK (space_rating   IS NULL OR space_rating   BETWEEN 1 AND 5),
  CONSTRAINT chk_fi_support_rating CHECK (support_rating IS NULL OR support_rating BETWEEN 1 AND 5)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
  COMMENT='Từng phiếu đánh giá chi tiết';

-- =====================================================================
--  G. TRUYỀN THÔNG & EMAIL
-- =====================================================================

-- news: ĐÃ TÍCH HỢP updated_by + deleted_at + composite (status,campus_id,published_date)
CREATE TABLE news (
  news_id        CHAR(36)     NOT NULL COMMENT 'Khóa chính (UUID)',
  news_type      ENUM('News','Review') NOT NULL DEFAULT 'News',
  title          VARCHAR(255) NOT NULL,
  summary        VARCHAR(500) NULL,
  body           LONGTEXT     NULL,
  image_url      VARCHAR(500) NULL,
  created_by     CHAR(36)     NULL COMMENT 'FK users',
  campus_id      CHAR(36)     NULL COMMENT 'FK campuses',
  status         ENUM('Cho Duyet','Da Duyet','Tu Choi','An') NOT NULL DEFAULT 'Cho Duyet',
  published_date DATE         NULL,
  created_at     DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP,
  updated_at     DATETIME     NULL ON UPDATE CURRENT_TIMESTAMP,
  updated_by     CHAR(36)     NULL COMMENT 'FK users',
  deleted_at     DATETIME     NULL COMMENT 'Soft delete',
  PRIMARY KEY (news_id),
  KEY idx_news_createdby (created_by),
  KEY idx_news_campus (campus_id),
  KEY idx_news_status_campus_date (status, campus_id, published_date),
  KEY idx_news_deleted_at (deleted_at)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
  COMMENT='Tin tức / Bài review truyền thông';

CREATE TABLE email_templates (
  template_id  CHAR(36)     NOT NULL COMMENT 'Khóa chính (UUID)',
  name         VARCHAR(200) NOT NULL,
  subject      VARCHAR(255) NOT NULL,
  description  VARCHAR(500) NULL,
  body         LONGTEXT     NULL,
  created_by   CHAR(36)     NULL COMMENT 'FK users',
  campus_id    CHAR(36)     NULL COMMENT 'FK campuses (NULL=Toàn quốc)',
  status       ENUM('InUse','NotInUse') NOT NULL DEFAULT 'InUse',
  created_at   DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (template_id),
  KEY idx_etpl_createdby (created_by),
  KEY idx_etpl_campus (campus_id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
  COMMENT='Mẫu email';

CREATE TABLE sent_emails (
  email_id       CHAR(36)     NOT NULL COMMENT 'Khóa chính (UUID)',
  program        VARCHAR(255) NULL,
  visit_id       CHAR(36)     NULL COMMENT 'FK visit_requests',
  subject        VARCHAR(255) NOT NULL,
  body           LONGTEXT     NULL,
  sender_user_id CHAR(36)     NULL COMMENT 'FK users',
  campus_id      CHAR(36)     NULL COMMENT 'FK campuses',
  send_time      DATETIME     NULL,
  status         ENUM('Thanh cong','Dang xu ly','That bai') NOT NULL DEFAULT 'Dang xu ly',
  has_new_reply  TINYINT(1)   NOT NULL DEFAULT 0,
  PRIMARY KEY (email_id),
  KEY idx_semail_visit (visit_id),
  KEY idx_semail_sender (sender_user_id),
  KEY idx_semail_campus (campus_id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
  COMMENT='Email đã gửi đi';

CREATE TABLE sent_email_recipients (
  recipient_id      CHAR(36)     NOT NULL COMMENT 'Khóa chính (UUID)',
  email_id          CHAR(36)     NOT NULL COMMENT 'FK sent_emails',
  email             VARCHAR(150) NOT NULL,
  name              VARCHAR(150) NULL,
  partner_contact_id CHAR(36)    NULL COMMENT 'FK partner_contacts',
  delivery_status   ENUM('Thanh cong','Dang xu ly','That bai') NOT NULL DEFAULT 'Dang xu ly',
  PRIMARY KEY (recipient_id),
  KEY idx_serecip_email (email_id),
  KEY idx_serecip_contact (partner_contact_id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
  COMMENT='Người nhận của email đã gửi';

-- =====================================================================
--  H. NỘI DUNG & HỖ TRỢ
-- =====================================================================

-- documents: ĐÃ TÍCH HỢP updated_at + deleted_at + composite (category,campus_id)
CREATE TABLE documents (
  document_id  CHAR(36)     NOT NULL COMMENT 'Khóa chính (UUID)',
  file_name    VARCHAR(255) NOT NULL,
  file_size    VARCHAR(20)  NULL,
  file_type    VARCHAR(20)  NULL,
  file_url     VARCHAR(500) NOT NULL,
  category     VARCHAR(60)  NULL COMMENT 'Visit / Partner / General...',
  description  VARCHAR(500) NULL,
  uploaded_by  CHAR(36)     NULL COMMENT 'FK users',
  campus_id    CHAR(36)     NULL COMMENT 'FK campuses',
  visit_id     CHAR(36)     NULL COMMENT 'FK visit_requests',
  upload_date  DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP,
  updated_at   DATETIME     NULL ON UPDATE CURRENT_TIMESTAMP,
  deleted_at   DATETIME     NULL COMMENT 'Soft delete',
  PRIMARY KEY (document_id),
  KEY idx_docs_uploader (uploaded_by),
  KEY idx_docs_campus (campus_id),
  KEY idx_docs_visit (visit_id),
  KEY idx_docs_category_campus (category, campus_id),
  KEY idx_docs_deleted_at (deleted_at)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
  COMMENT='Tài liệu hệ thống';

CREATE TABLE galleries (
  gallery_id   CHAR(36)     NOT NULL COMMENT 'Khóa chính (UUID)',
  name         VARCHAR(200) NOT NULL,
  description  VARCHAR(500) NULL,
  uploaded_by  CHAR(36)     NULL COMMENT 'FK users',
  campus_id    CHAR(36)     NULL COMMENT 'FK campuses',
  created_at   DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (gallery_id),
  KEY idx_gallery_uploader (uploaded_by),
  KEY idx_gallery_campus (campus_id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
  COMMENT='Album thư viện ảnh';

CREATE TABLE gallery_images (
  image_id    CHAR(36)     NOT NULL COMMENT 'Khóa chính (UUID)',
  gallery_id  CHAR(36)     NOT NULL COMMENT 'FK galleries',
  url         VARCHAR(500) NOT NULL,
  caption     VARCHAR(255) NULL,
  upload_date DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (image_id),
  KEY idx_gimg_gallery (gallery_id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
  COMMENT='Ảnh trong album';

CREATE TABLE gallery_locations (
  location_id CHAR(36)     NOT NULL COMMENT 'Khóa chính (UUID)',
  name        VARCHAR(200) NOT NULL,
  description VARCHAR(500) NULL,
  campus_id   CHAR(36)     NULL COMMENT 'FK campuses',
  PRIMARY KEY (location_id),
  KEY idx_gloc_campus (campus_id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
  COMMENT='Địa điểm chụp ảnh trong cơ sở';

CREATE TABLE gallery_location_images (
  gli_id      CHAR(36)     NOT NULL COMMENT 'Khóa chính (UUID)',
  location_id CHAR(36)     NOT NULL COMMENT 'FK gallery_locations',
  url         VARCHAR(500) NOT NULL,
  caption     VARCHAR(255) NULL,
  PRIMARY KEY (gli_id),
  KEY idx_glimg_loc (location_id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
  COMMENT='Ảnh theo địa điểm';

CREATE TABLE faqs (
  faq_id      CHAR(36)     NOT NULL COMMENT 'Khóa chính (UUID)',
  question    VARCHAR(500) NOT NULL,
  answer      TEXT         NOT NULL,
  category    VARCHAR(60)  NULL,
  status      ENUM('Draft','Published','Archived') NOT NULL DEFAULT 'Draft',
  created_by  CHAR(36)     NULL COMMENT 'FK users',
  created_at  DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP,
  updated_at  DATETIME     NULL ON UPDATE CURRENT_TIMESTAMP,
  PRIMARY KEY (faq_id),
  KEY idx_faqs_createdby (created_by),
  KEY idx_faqs_status (status)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
  COMMENT='Câu hỏi thường gặp';

CREATE TABLE reports (
  report_id    CHAR(36)     NOT NULL COMMENT 'Khóa chính (UUID)',
  title        VARCHAR(200) NOT NULL,
  period       VARCHAR(50)  NULL,
  report_type  ENUM('Visit','Task','Partner','Combined') NOT NULL DEFAULT 'Combined',
  campus_id    CHAR(36)     NULL COMMENT 'FK campuses',
  data_json    JSON         NULL,
  generated_by CHAR(36)     NULL COMMENT 'FK users',
  generated_at DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (report_id),
  KEY idx_reports_campus (campus_id),
  KEY idx_reports_generatedby (generated_by)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
  COMMENT='Báo cáo thống kê';

-- notifications: ĐÃ TÍCH HỢP composite (user_id,is_read,created_at)
CREATE TABLE notifications (
  notification_id     CHAR(36)     NOT NULL COMMENT 'Khóa chính (UUID)',
  user_id             CHAR(36)     NOT NULL COMMENT 'Người nhận (FK users)',
  title               VARCHAR(200) NOT NULL,
  message             VARCHAR(500) NULL,
  type                VARCHAR(40)  NULL,
  related_entity_type VARCHAR(40)  NULL,
  related_entity_id   CHAR(36)     NULL,
  is_read             TINYINT(1)   NOT NULL DEFAULT 0,
  created_at          DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (notification_id),
  KEY idx_notif_user_read_time (user_id, is_read, created_at),
  KEY idx_notif_isread (is_read)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
  COMMENT='Thông báo gửi tới người dùng';

-- =====================================================================
--  I. NHẬT KÝ / AUDIT (bản gọn 3 bảng)
-- =====================================================================

CREATE TABLE audit_logs (
  id             BIGINT       NOT NULL AUTO_INCREMENT COMMENT 'PK (volume lớn → BIGINT)',
  actor_user_id  CHAR(36)     NULL COMMENT 'FK users - người thực hiện',
  action         VARCHAR(100) NOT NULL COMMENT 'CREATE/UPDATE/DELETE/APPROVE/REJECT/STATUS_CHANGE/ROLE_CHANGE...',
  entity_type    VARCHAR(100) NOT NULL COMMENT 'Tên bảng/entity bị tác động',
  entity_id      CHAR(36)     NULL COMMENT 'UUID bản ghi (polymorphic → không FK)',
  old_values     JSON         NULL COMMENT 'Giá trị cũ (đã mask field nhạy cảm)',
  new_values     JSON         NULL COMMENT 'Giá trị mới (đã mask field nhạy cảm)',
  ip_address     VARCHAR(45)  NULL,
  user_agent     VARCHAR(500) NULL,
  request_id     VARCHAR(100) NULL,
  campus_id      CHAR(36)     NULL COMMENT 'FK campuses',
  status         ENUM('SUCCESS','FAILED') NOT NULL DEFAULT 'SUCCESS',
  error_message  TEXT         NULL,
  created_at     DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (id),
  KEY idx_audit_actor_time  (actor_user_id, created_at),
  KEY idx_audit_entity      (entity_type, entity_id),
  KEY idx_audit_action_time (action, created_at),
  KEY idx_audit_request_id  (request_id),
  KEY idx_audit_campus_time (campus_id, created_at)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
  COMMENT='Nhật ký kiểm toán hành động quan trọng';

CREATE TABLE login_logs (
  id             BIGINT       NOT NULL AUTO_INCREMENT,
  user_id        CHAR(36)     NULL COMMENT 'FK users - NULL nếu email không tồn tại',
  email          VARCHAR(150) NULL,
  ip_address     VARCHAR(45)  NULL,
  user_agent     VARCHAR(500) NULL,
  status         ENUM('SUCCESS','FAILED') NOT NULL,
  failure_reason VARCHAR(255) NULL COMMENT 'WRONG_PASSWORD/USER_NOT_FOUND/LOCKED/INACTIVE...',
  session_id     VARCHAR(100) NULL,
  logout_at      DATETIME     NULL,
  created_at     DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (id),
  KEY idx_login_user_time   (user_id, created_at),
  KEY idx_login_email_time  (email, created_at),
  KEY idx_login_status_time (status, created_at),
  KEY idx_login_ip_time     (ip_address, created_at)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
  COMMENT='Nhật ký đăng nhập/đăng xuất';

CREATE TABLE visit_status_logs (
  id           BIGINT       NOT NULL AUTO_INCREMENT,
  visit_id     CHAR(36)     NOT NULL COMMENT 'FK visit_requests',
  old_status   VARCHAR(40)  NULL,
  new_status   VARCHAR(40)  NOT NULL,
  changed_by   CHAR(36)     NULL COMMENT 'FK users',
  reason       VARCHAR(500) NULL,
  changed_at   DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (id),
  KEY idx_vstatus_visit_time (visit_id, changed_at),
  KEY idx_vstatus_newstatus  (new_status, changed_at),
  KEY idx_vstatus_changedby  (changed_by)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
  COMMENT='Lịch sử chuyển trạng thái đoàn khách (BR-3)';

-- =====================================================================
--  J. KHÓA NGOẠI (FOREIGN KEYS) — tập trung
-- =====================================================================

ALTER TABLE role_permissions
  ADD CONSTRAINT FK_role_permissions_roles       FOREIGN KEY (role_id)       REFERENCES roles(role_id)             ON DELETE CASCADE,
  ADD CONSTRAINT FK_role_permissions_permissions FOREIGN KEY (permission_id) REFERENCES permissions(permission_id) ON DELETE CASCADE;

ALTER TABLE campuses
  ADD CONSTRAINT FK_campuses_users FOREIGN KEY (ic_head_user_id) REFERENCES users(user_id) ON DELETE SET NULL;

ALTER TABLE departments
  ADD CONSTRAINT FK_departments_campuses FOREIGN KEY (campus_id)    REFERENCES campuses(campus_id) ON DELETE RESTRICT,
  ADD CONSTRAINT FK_departments_users    FOREIGN KEY (head_user_id) REFERENCES users(user_id)      ON DELETE SET NULL;

-- users: FK_users_departments = RESTRICT (lưới an toàn cho soft-delete phòng ban) + vết created_by/updated_by
ALTER TABLE users
  ADD CONSTRAINT FK_users_roles        FOREIGN KEY (role_id)       REFERENCES roles(role_id)             ON DELETE RESTRICT,
  ADD CONSTRAINT FK_users_campuses     FOREIGN KEY (campus_id)     REFERENCES campuses(campus_id)        ON DELETE SET NULL,
  ADD CONSTRAINT FK_users_departments  FOREIGN KEY (department_id) REFERENCES departments(department_id) ON DELETE RESTRICT ON UPDATE CASCADE,
  ADD CONSTRAINT FK_users_created_by   FOREIGN KEY (created_by)    REFERENCES users(user_id)             ON DELETE SET NULL,
  ADD CONSTRAINT FK_users_updated_by   FOREIGN KEY (updated_by)    REFERENCES users(user_id)             ON DELETE SET NULL;

ALTER TABLE partners
  ADD CONSTRAINT FK_partners_users      FOREIGN KEY (created_by) REFERENCES users(user_id)      ON DELETE SET NULL,
  ADD CONSTRAINT FK_partners_campuses   FOREIGN KEY (campus_id)  REFERENCES campuses(campus_id) ON DELETE SET NULL,
  ADD CONSTRAINT FK_partners_updated_by FOREIGN KEY (updated_by) REFERENCES users(user_id)      ON DELETE SET NULL;
ALTER TABLE partner_contacts
  ADD CONSTRAINT FK_partner_contacts_partners  FOREIGN KEY (partner_id) REFERENCES partners(partner_id) ON DELETE CASCADE;
ALTER TABLE partner_histories
  ADD CONSTRAINT FK_partner_histories_partners FOREIGN KEY (partner_id) REFERENCES partners(partner_id) ON DELETE CASCADE;
ALTER TABLE partner_documents
  ADD CONSTRAINT FK_partner_documents_partners FOREIGN KEY (partner_id)  REFERENCES partners(partner_id) ON DELETE CASCADE,
  ADD CONSTRAINT FK_partner_documents_users    FOREIGN KEY (uploaded_by) REFERENCES users(user_id)       ON DELETE SET NULL;
ALTER TABLE partner_sync_logs
  ADD CONSTRAINT FK_partner_sync_logs_partners FOREIGN KEY (partner_id)  REFERENCES partners(partner_id) ON DELETE SET NULL;

ALTER TABLE visit_requests
  ADD CONSTRAINT FK_visit_requests_campuses FOREIGN KEY (campus_id)      REFERENCES campuses(campus_id)  ON DELETE RESTRICT,
  ADD CONSTRAINT FK_visit_requests_partners FOREIGN KEY (partner_id)     REFERENCES partners(partner_id) ON DELETE SET NULL,
  ADD CONSTRAINT FK_visit_requests_host     FOREIGN KEY (host_user_id)   REFERENCES users(user_id)       ON DELETE SET NULL,
  ADD CONSTRAINT FK_visit_requests_sender   FOREIGN KEY (sender_user_id) REFERENCES users(user_id)       ON DELETE SET NULL,
  ADD CONSTRAINT FK_visit_requests_creator  FOREIGN KEY (created_by)     REFERENCES users(user_id)       ON DELETE SET NULL,
  ADD CONSTRAINT FK_visit_updated_by        FOREIGN KEY (updated_by)     REFERENCES users(user_id)       ON DELETE SET NULL;
ALTER TABLE visit_details
  ADD CONSTRAINT FK_visit_details_visit    FOREIGN KEY (visit_id)  REFERENCES visit_requests(visit_id) ON DELETE CASCADE,
  ADD CONSTRAINT FK_visit_details_campuses FOREIGN KEY (campus_id) REFERENCES campuses(campus_id)      ON DELETE RESTRICT;
ALTER TABLE visit_participants
  ADD CONSTRAINT FK_visit_participants_visit FOREIGN KEY (visit_id) REFERENCES visit_requests(visit_id) ON DELETE CASCADE,
  ADD CONSTRAINT FK_visit_participants_users FOREIGN KEY (user_id)  REFERENCES users(user_id)           ON DELETE SET NULL;
ALTER TABLE visit_agendas
  ADD CONSTRAINT FK_visit_agendas_visit FOREIGN KEY (visit_id) REFERENCES visit_requests(visit_id) ON DELETE CASCADE;
ALTER TABLE agenda_templates
  ADD CONSTRAINT FK_agenda_templates_users FOREIGN KEY (created_by) REFERENCES users(user_id) ON DELETE SET NULL;
ALTER TABLE agenda_template_items
  ADD CONSTRAINT FK_agenda_template_items_tpl FOREIGN KEY (template_id) REFERENCES agenda_templates(template_id) ON DELETE CASCADE;

ALTER TABLE tasks
  ADD CONSTRAINT FK_tasks_visit       FOREIGN KEY (visit_id)            REFERENCES visit_requests(visit_id)   ON DELETE CASCADE,
  ADD CONSTRAINT FK_tasks_assignee    FOREIGN KEY (assigned_to_user_id) REFERENCES users(user_id)             ON DELETE SET NULL,
  ADD CONSTRAINT FK_tasks_departments FOREIGN KEY (department_id)       REFERENCES departments(department_id) ON DELETE SET NULL,
  ADD CONSTRAINT FK_tasks_creator     FOREIGN KEY (created_by)          REFERENCES users(user_id)             ON DELETE SET NULL,
  ADD CONSTRAINT FK_tasks_updated_by  FOREIGN KEY (updated_by)          REFERENCES users(user_id)             ON DELETE SET NULL;
ALTER TABLE task_actions
  ADD CONSTRAINT FK_task_actions_tasks FOREIGN KEY (task_id)     REFERENCES tasks(task_id) ON DELETE CASCADE,
  ADD CONSTRAINT FK_task_actions_users FOREIGN KEY (approved_by) REFERENCES users(user_id) ON DELETE SET NULL;
ALTER TABLE action_items
  ADD CONSTRAINT FK_action_items_visit    FOREIGN KEY (visit_id)         REFERENCES visit_requests(visit_id) ON DELETE CASCADE,
  ADD CONSTRAINT FK_action_items_assignee FOREIGN KEY (assignee_user_id) REFERENCES users(user_id)           ON DELETE SET NULL,
  ADD CONSTRAINT FK_action_items_creator  FOREIGN KEY (created_by)       REFERENCES users(user_id)           ON DELETE SET NULL;

ALTER TABLE minutes
  ADD CONSTRAINT FK_minutes_visit   FOREIGN KEY (visit_id)   REFERENCES visit_requests(visit_id) ON DELETE CASCADE,
  ADD CONSTRAINT FK_minutes_creator FOREIGN KEY (created_by) REFERENCES users(user_id)           ON DELETE SET NULL;
ALTER TABLE minute_participants
  ADD CONSTRAINT FK_minute_participants_minutes FOREIGN KEY (minute_id) REFERENCES minutes(minute_id) ON DELETE CASCADE,
  ADD CONSTRAINT FK_minute_participants_users   FOREIGN KEY (user_id)   REFERENCES users(user_id)     ON DELETE SET NULL;
ALTER TABLE feedbacks
  ADD CONSTRAINT FK_feedbacks_visit FOREIGN KEY (visit_id) REFERENCES visit_requests(visit_id) ON DELETE CASCADE;
ALTER TABLE feedback_items
  ADD CONSTRAINT FK_feedback_items_feedbacks FOREIGN KEY (feedback_id)      REFERENCES feedbacks(feedback_id) ON DELETE CASCADE,
  ADD CONSTRAINT FK_feedback_items_users     FOREIGN KEY (reviewer_user_id) REFERENCES users(user_id)         ON DELETE SET NULL;

ALTER TABLE news
  ADD CONSTRAINT FK_news_users      FOREIGN KEY (created_by) REFERENCES users(user_id)      ON DELETE SET NULL,
  ADD CONSTRAINT FK_news_campuses   FOREIGN KEY (campus_id)  REFERENCES campuses(campus_id) ON DELETE SET NULL,
  ADD CONSTRAINT FK_news_updated_by FOREIGN KEY (updated_by) REFERENCES users(user_id)      ON DELETE SET NULL;
ALTER TABLE email_templates
  ADD CONSTRAINT FK_email_templates_users    FOREIGN KEY (created_by) REFERENCES users(user_id)      ON DELETE SET NULL,
  ADD CONSTRAINT FK_email_templates_campuses FOREIGN KEY (campus_id)  REFERENCES campuses(campus_id) ON DELETE SET NULL;
ALTER TABLE sent_emails
  ADD CONSTRAINT FK_sent_emails_visit    FOREIGN KEY (visit_id)       REFERENCES visit_requests(visit_id) ON DELETE SET NULL,
  ADD CONSTRAINT FK_sent_emails_sender   FOREIGN KEY (sender_user_id) REFERENCES users(user_id)           ON DELETE SET NULL,
  ADD CONSTRAINT FK_sent_emails_campuses FOREIGN KEY (campus_id)      REFERENCES campuses(campus_id)      ON DELETE SET NULL;
ALTER TABLE sent_email_recipients
  ADD CONSTRAINT FK_sent_email_recipients_email   FOREIGN KEY (email_id)           REFERENCES sent_emails(email_id)         ON DELETE CASCADE,
  ADD CONSTRAINT FK_sent_email_recipients_contact FOREIGN KEY (partner_contact_id) REFERENCES partner_contacts(contact_id) ON DELETE SET NULL;

ALTER TABLE documents
  ADD CONSTRAINT FK_documents_users    FOREIGN KEY (uploaded_by) REFERENCES users(user_id)           ON DELETE SET NULL,
  ADD CONSTRAINT FK_documents_campuses FOREIGN KEY (campus_id)   REFERENCES campuses(campus_id)      ON DELETE SET NULL,
  ADD CONSTRAINT FK_documents_visit    FOREIGN KEY (visit_id)    REFERENCES visit_requests(visit_id) ON DELETE SET NULL;
ALTER TABLE galleries
  ADD CONSTRAINT FK_galleries_users    FOREIGN KEY (uploaded_by) REFERENCES users(user_id)      ON DELETE SET NULL,
  ADD CONSTRAINT FK_galleries_campuses FOREIGN KEY (campus_id)   REFERENCES campuses(campus_id) ON DELETE SET NULL;
ALTER TABLE gallery_images
  ADD CONSTRAINT FK_gallery_images_galleries FOREIGN KEY (gallery_id) REFERENCES galleries(gallery_id) ON DELETE CASCADE;
ALTER TABLE gallery_locations
  ADD CONSTRAINT FK_gallery_locations_campuses FOREIGN KEY (campus_id) REFERENCES campuses(campus_id) ON DELETE SET NULL;
ALTER TABLE gallery_location_images
  ADD CONSTRAINT FK_gallery_location_images_loc FOREIGN KEY (location_id) REFERENCES gallery_locations(location_id) ON DELETE CASCADE;
ALTER TABLE faqs
  ADD CONSTRAINT FK_faqs_users FOREIGN KEY (created_by) REFERENCES users(user_id) ON DELETE SET NULL;
ALTER TABLE reports
  ADD CONSTRAINT FK_reports_campuses FOREIGN KEY (campus_id)    REFERENCES campuses(campus_id) ON DELETE SET NULL,
  ADD CONSTRAINT FK_reports_users    FOREIGN KEY (generated_by) REFERENCES users(user_id)      ON DELETE SET NULL;
ALTER TABLE notifications
  ADD CONSTRAINT FK_notifications_users FOREIGN KEY (user_id) REFERENCES users(user_id) ON DELETE CASCADE;

-- FK cho 3 bảng log (giữ log khi user bị gỡ)
ALTER TABLE audit_logs
  ADD CONSTRAINT FK_audit_logs_actor  FOREIGN KEY (actor_user_id) REFERENCES users(user_id)      ON DELETE SET NULL,
  ADD CONSTRAINT FK_audit_logs_campus FOREIGN KEY (campus_id)     REFERENCES campuses(campus_id) ON DELETE SET NULL;
ALTER TABLE login_logs
  ADD CONSTRAINT FK_login_logs_user FOREIGN KEY (user_id) REFERENCES users(user_id) ON DELETE SET NULL;
ALTER TABLE visit_status_logs
  ADD CONSTRAINT FK_vstatus_visit FOREIGN KEY (visit_id)   REFERENCES visit_requests(visit_id) ON DELETE CASCADE,
  ADD CONSTRAINT FK_vstatus_user  FOREIGN KEY (changed_by) REFERENCES users(user_id)           ON DELETE SET NULL;

SET FOREIGN_KEY_CHECKS = 1;

-- =====================================================================
--  K. FULLTEXT INDEX (màn tìm kiếm text)
-- =====================================================================
CREATE FULLTEXT INDEX ft_partners_name_desc  ON partners (name, description);
CREATE FULLTEXT INDEX ft_news_title_summary   ON news (title, summary, body);
CREATE FULLTEXT INDEX ft_documents_name_desc  ON documents (file_name, description);
CREATE FULLTEXT INDEX ft_faqs_question_answer ON faqs (question, answer);

-- =====================================================================
--  L. TRIGGER ÉP TOÀN VẸN VAI TRÒ (STAFF/DEPT phải có sub_role + department_id)
-- =====================================================================
DROP PROCEDURE IF EXISTS sp_assert_user_role_consistency;
DROP TRIGGER  IF EXISTS trg_users_role_bi;
DROP TRIGGER  IF EXISTS trg_users_role_bu;

DELIMITER $$

CREATE PROCEDURE sp_assert_user_role_consistency(
    IN p_role_id CHAR(36), IN p_sub_role VARCHAR(10), IN p_department_id CHAR(36)
)
BEGIN
    DECLARE v_role_code VARCHAR(30);
    SELECT role_code INTO v_role_code FROM roles WHERE role_id = p_role_id;
    IF v_role_code IS NULL THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'role_id khong ton tai trong bang roles';
    END IF;
    IF v_role_code IN ('STAFF','DEPT') THEN
        IF p_sub_role IS NULL THEN
            SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'sub_role (Leader/Staff) la BAT BUOC voi vai tro STAFF/DEPT';
        END IF;
        IF p_department_id IS NULL THEN
            SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'department_id la BAT BUOC voi vai tro STAFF/DEPT';
        END IF;
    ELSE
        IF p_sub_role IS NOT NULL THEN
            SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'sub_role chi ap dung cho vai tro STAFF/DEPT';
        END IF;
    END IF;
END$$

CREATE TRIGGER trg_users_role_bi BEFORE INSERT ON users FOR EACH ROW
BEGIN
    CALL sp_assert_user_role_consistency(NEW.role_id, NEW.sub_role, NEW.department_id);
END$$

CREATE TRIGGER trg_users_role_bu BEFORE UPDATE ON users FOR EACH ROW
BEGIN
    CALL sp_assert_user_role_consistency(NEW.role_id, NEW.sub_role, NEW.department_id);
END$$

DELIMITER ;

-- =====================================================================
--  M. VIEW — phơi bày "effective_role" để truy vấn 4 vai trò bằng 1 cột
-- =====================================================================
CREATE OR REPLACE VIEW v_users_effective_role AS
SELECT
    u.user_id, u.full_name, u.email, u.campus_id, u.department_id,
    r.role_code, u.sub_role, u.title,
    CASE
        WHEN r.role_code = 'STAFF' AND u.sub_role = 'Leader' THEN 'STAFF_LEADER'
        WHEN r.role_code = 'STAFF' AND u.sub_role = 'Staff'  THEN 'STAFF_MEMBER'
        WHEN r.role_code = 'DEPT'  AND u.sub_role = 'Leader' THEN 'DEPT_LEADER'
        WHEN r.role_code = 'DEPT'  AND u.sub_role = 'Staff'  THEN 'DEPT_PERSONNEL'
        ELSE r.role_code
    END AS effective_role
FROM users u
JOIN roles r ON r.role_id = u.role_id;

-- =====================================================================
--  N. SEED DỮ LIỆU DANH MỤC TỐI THIỂU
-- =====================================================================
INSERT INTO roles (role_id, role_code, name, description) VALUES
  (UUID(),'ADMIN','Quản trị viên','Quản trị toàn hệ thống'),
  (UUID(),'HO','Head Office','Ban điều phối tổng (FEHO) - duyệt liên cơ sở'),
  (UUID(),'STAFF','Cán bộ IC/Đối ngoại','Cán bộ phòng hợp tác quốc tế tại cơ sở'),
  (UUID(),'DEPT','Phòng ban phối hợp','Phòng ban xử lý task hậu cần'),
  (UUID(),'STUDENT','Sinh viên','Sinh viên hỗ trợ/tham gia'),
  (UUID(),'VISITOR','Khách','Khách đăng ký lịch tham quan');

INSERT INTO permissions (permission_id, permission_code, name, permission_group) VALUES
  (UUID(),'guest_create','Đăng ký lịch tham quan','guest'),
  (UUID(),'guest_approve','Phê duyệt yêu cầu liên cơ sở','guest'),
  (UUID(),'guest_close','Đóng dữ liệu đoàn khách','guest'),
  (UUID(),'doc_view','Xem tài liệu','doc'),
  (UUID(),'doc_upload','Tải lên tài liệu','doc'),
  (UUID(),'doc_delete','Xóa tài liệu toàn hệ thống','doc'),
  (UUID(),'user_view','Xem danh sách người dùng','user'),
  (UUID(),'user_manage','Phân quyền & Chỉnh sửa tài khoản','user');

INSERT INTO campuses (campus_id, campus_code, name, location) VALUES
  (UUID(),'HN','FPT University Hà Nội','Hà Nội'),
  (UUID(),'HCM','FPT University Hồ Chí Minh','TP. Hồ Chí Minh'),
  (UUID(),'DN','FPT University Đà Nẵng','Đà Nẵng'),
  (UUID(),'CT','FPT University Cần Thơ','Cần Thơ'),
  (UUID(),'QN','FPT University Quy Nhơn','Bình Định');

-- =====================================================================
--  O. DB USER LEAST PRIVILEGE (mở comment & chỉnh theo môi trường)
-- =====================================================================
-- CREATE USER 'pems_app'@'%' IDENTIFIED BY '<dat-mat-khau-manh>';
-- GRANT SELECT, INSERT, UPDATE, DELETE ON pems_db.* TO 'pems_app'@'%';
-- -- KHÔNG cấp: DROP, ALTER, CREATE, GRANT, FILE, SUPER.
-- FLUSH PRIVILEGES;

-- =====================================================================
--  HẾT — pems_full.sql  (39 bảng + 1 view)
-- =====================================================================
