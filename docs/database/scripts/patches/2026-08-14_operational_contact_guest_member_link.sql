-- =============================================================================================
-- NP-03 — Đầu mối đoàn khách có DANH TÍNH ỔN ĐỊNH (stable identity)
--
-- Vấn đề: `visit_instance_form_details.operational_contact_*` chỉ là SNAPSHOT (tên/đơn vị/chức
-- vụ/SĐT/email). Không có quan hệ nào nối đầu mối với một thành viên trong đoàn, nên:
--   • đầu mối chỉ khai ở khối "Đầu mối" thì KHÔNG bao giờ tự xuất hiện trong biên bản
--     (MinuteAutoFill chỉ lấy Host + participant ACCEPTED + guest của instance);
--   • đầu mối vừa khai ở khối "Đầu mối" vừa nằm trong danh sách đoàn thì chỉ còn cách so
--     CHUỖI để biết có phải một người — trùng tên là trùng người, khác một dấu cách là hai
--     người.
--
-- Patch này thêm quan hệ còn thiếu. Snapshot GIỮ NGUYÊN (là bản ghi kiểm chứng những gì đã
-- thoả thuận, không được chạy theo chỉnh sửa sau này của dòng thành viên); cột mới trả lời
-- thẳng câu hỏi "có phải cùng một người không".
--
-- Tính chất:
--   • ADDITIVE — không sửa/không xoá cột nào đang có, không đổi kiểu dữ liệu.
--   • IDEMPOTENT — chạy lại nhiều lần vẫn an toàn (kiểm tra information_schema trước).
--   • NULLABLE — NULL là câu trả lời hợp lệ: đầu mối không đi cùng đoàn là chuyện bình thường,
--     và mọi dòng cũ đều bắt đầu ở NULL. Backfill ở §3 chỉ nối những trường hợp KHỚP CHẮC CHẮN.
--   • ON DELETE SET NULL — xoá một thành viên đoàn thì đầu mối tụt về mức snapshot, KHÔNG kéo
--     theo việc mất đầu mối của cơ sở.
--
-- Cách chạy (LƯU Ý: luôn kèm --default-character-set=utf8mb4, nếu không mọi chữ tiếng Việt
-- patch ghi vào sẽ bị mojibake):
--   mysql --default-character-set=utf8mb4 -u root -p <ten_db> < 2026-08-14_operational_contact_guest_member_link.sql
-- =============================================================================================

-- ── §1. Thêm cột (idempotent) ────────────────────────────────────────────────────────────────
SET @col_exists := (
  SELECT COUNT(*) FROM information_schema.COLUMNS
  WHERE TABLE_SCHEMA = DATABASE()
    AND TABLE_NAME = 'visit_instance_form_details'
    AND COLUMN_NAME = 'operational_contact_guest_member_id');

SET @sql := IF(@col_exists = 0,
  'ALTER TABLE visit_instance_form_details
     ADD COLUMN operational_contact_guest_member_id BIGINT UNSIGNED NULL
       COMMENT ''Đầu mối của cơ sở này LÀ thành viên nào trong đoàn (visit_guest_members). NULL = chưa nối hoặc đầu mối không đi cùng đoàn — biên bản khi đó lấy từ snapshot. Snapshot operational_contact_* vẫn giữ nguyên để phục vụ kiểm chứng.''
       AFTER operational_contact_email',
  'SELECT ''[skip] cột operational_contact_guest_member_id đã tồn tại''');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

-- ── §2. Index + khoá ngoại (idempotent) ──────────────────────────────────────────────────────
SET @idx_exists := (
  SELECT COUNT(*) FROM information_schema.STATISTICS
  WHERE TABLE_SCHEMA = DATABASE()
    AND TABLE_NAME = 'visit_instance_form_details'
    AND INDEX_NAME = 'idx_vifd_op_contact_member');

SET @sql := IF(@idx_exists = 0,
  'ALTER TABLE visit_instance_form_details
     ADD KEY idx_vifd_op_contact_member (operational_contact_guest_member_id)',
  'SELECT ''[skip] idx_vifd_op_contact_member đã tồn tại''');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @fk_exists := (
  SELECT COUNT(*) FROM information_schema.TABLE_CONSTRAINTS
  WHERE TABLE_SCHEMA = DATABASE()
    AND TABLE_NAME = 'visit_instance_form_details'
    AND CONSTRAINT_NAME = 'fk_vifd_op_contact_member');

SET @sql := IF(@fk_exists = 0,
  'ALTER TABLE visit_instance_form_details
     ADD CONSTRAINT fk_vifd_op_contact_member
       FOREIGN KEY (operational_contact_guest_member_id)
       REFERENCES visit_guest_members (guest_member_id)
       ON UPDATE CASCADE ON DELETE SET NULL',
  'SELECT ''[skip] fk_vifd_op_contact_member đã tồn tại''');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

-- ── §3. Backfill — CHỈ nối khi chắc chắn ─────────────────────────────────────────────────────
-- Quy tắc: cùng họ tên + chức vụ + đơn vị (đã chuẩn hoá: trim, lower, gộp khoảng trắng) VÀ
-- trong cơ sở đó chỉ có ĐÚNG MỘT thành viên khớp. Có từ 2 người khớp trở lên thì để NULL —
-- đoán bừa ở đây là gán nhầm danh tính, và NULL vẫn chạy đúng (biên bản dùng snapshot).
--
-- KHÔNG dùng riêng họ tên để khớp: trùng tên trong một đoàn là chuyện thường.
-- Chỉ xét thành viên GUEST: EXTERNAL_SUPPORT là người phía FPTU thu xếp, không phải đầu mối
-- của đoàn khách.
--
-- Chuẩn hoá phải GIỐNG HỆT `PersonIdentity.Key` phía ứng dụng (trim + lower + gộp khoảng trắng
-- giữa các từ), nếu không backfill và runtime sẽ trả lời khác nhau cho cùng một cặp dữ liệu:
-- "Trần  Thị B" (2 dấu cách do copy-paste) sẽ khớp trong C# nhưng trượt ở đây, để lại NULL cho
-- đúng những dòng lẽ ra nối được. MySQL 8 có REGEXP_REPLACE nên gộp được khoảng trắng ngay trong
-- câu lệnh. KHÔNG bỏ dấu tiếng Việt: "Nguyễn Văn An" và "Nguyen Van Ân" là hai người.
UPDATE visit_instance_form_details d
JOIN (
  SELECT
    l.visit_instance_id,
    MIN(g.guest_member_id) AS guest_member_id,
    COUNT(*)               AS match_count
  FROM visit_instance_guest_members l
  JOIN visit_guest_members g
    ON g.guest_member_id = l.guest_member_id
   AND g.member_type = 'GUEST'
  JOIN visit_instance_form_details fd
    ON fd.visit_instance_id = l.visit_instance_id
  WHERE LOWER(TRIM(REGEXP_REPLACE(g.full_name, '[[:space:]]+', ' ')))
        = LOWER(TRIM(REGEXP_REPLACE(fd.operational_contact_full_name, '[[:space:]]+', ' ')))
    AND LOWER(TRIM(REGEXP_REPLACE(g.job_title, '[[:space:]]+', ' ')))
        = LOWER(TRIM(REGEXP_REPLACE(fd.operational_contact_job_title, '[[:space:]]+', ' ')))
    AND LOWER(TRIM(REGEXP_REPLACE(g.organization, '[[:space:]]+', ' ')))
        = LOWER(TRIM(REGEXP_REPLACE(COALESCE(fd.operational_contact_organization, ''), '[[:space:]]+', ' ')))
  GROUP BY l.visit_instance_id
) m ON m.visit_instance_id = d.visit_instance_id
SET d.operational_contact_guest_member_id = m.guest_member_id
WHERE m.match_count = 1
  AND d.operational_contact_guest_member_id IS NULL;

-- ── §4. Verify ───────────────────────────────────────────────────────────────────────────────
SELECT
  COUNT(*)                                                        AS total_form_details,
  SUM(operational_contact_guest_member_id IS NOT NULL)             AS linked_to_delegation_member,
  SUM(operational_contact_guest_member_id IS NULL)                 AS unlinked_uses_snapshot
FROM visit_instance_form_details;

-- Không dòng nào được trỏ tới một thành viên của REQUEST KHÁC. Kỳ vọng: 0 dòng.
SELECT d.visit_instance_id, d.operational_contact_guest_member_id
FROM visit_instance_form_details d
JOIN visit_request_campuses c ON c.visit_instance_id = d.visit_instance_id
JOIN visit_guest_members g    ON g.guest_member_id = d.operational_contact_guest_member_id
WHERE d.operational_contact_guest_member_id IS NOT NULL
  AND g.visit_request_id <> c.visit_request_id;

-- Không dòng nào được trỏ tới thành viên KHÔNG thuộc chính cơ sở đó. Kỳ vọng: 0 dòng.
SELECT d.visit_instance_id, d.operational_contact_guest_member_id
FROM visit_instance_form_details d
WHERE d.operational_contact_guest_member_id IS NOT NULL
  AND NOT EXISTS (
    SELECT 1 FROM visit_instance_guest_members l
    WHERE l.visit_instance_id = d.visit_instance_id
      AND l.guest_member_id = d.operational_contact_guest_member_id);

-- ── §5. Rollback (chạy tay nếu cần) ──────────────────────────────────────────────────────────
-- ALTER TABLE visit_instance_form_details DROP FOREIGN KEY fk_vifd_op_contact_member;
-- ALTER TABLE visit_instance_form_details DROP KEY idx_vifd_op_contact_member;
-- ALTER TABLE visit_instance_form_details DROP COLUMN operational_contact_guest_member_id;
