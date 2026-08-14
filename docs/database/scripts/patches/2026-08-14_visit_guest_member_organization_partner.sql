-- =============================================================================================
-- PART-01 — Mỗi thành viên trong đoàn giữ được DANH TÍNH ỔN ĐỊNH của tổ chức
--
-- Vấn đề: `visit_guest_members.organization` chỉ là CHUỖI. Người đăng ký chọn một đối tác có sẵn
-- trong dropdown, nhưng thứ được gửi lên và lưu lại chỉ là tên hiển thị — `partner_id` rơi mất
-- ngay ở tầng combobox. Sang biên bản, hệ thống không còn cách nào biết dòng này ĐÃ được chọn từ
-- hồ sơ nào, nên vẫn hiện "Chưa liên kết / Tạo · liên kết" và bắt người dùng làm lại việc họ vừa
-- làm. Suy ngược bằng cách so tên là sai nguyên tắc: tên đối tác đổi được, và hai tổ chức khác
-- nhau vẫn có thể viết giống nhau.
--
-- Patch này thêm quan hệ còn thiếu. Snapshot `organization` GIỮ NGUYÊN và vẫn là thứ hiển thị
-- trên đơn: nó ghi lại đúng những gì người đăng ký đã gửi tại thời điểm đó, nên đối tác có đổi
-- tên về sau thì lịch sử đơn cũ vẫn đọc đúng. Cột mới trả lời câu hỏi khác: "người dùng đã thực
-- sự chọn hồ sơ nào".
--
-- Tính chất:
--   • ADDITIVE — không sửa/không xoá cột nào đang có, không đổi kiểu dữ liệu.
--   • IDEMPOTENT — chạy lại nhiều lần vẫn an toàn (kiểm tra information_schema trước).
--   • NULLABLE — NULL là câu trả lời hợp lệ và là mặc định: tổ chức gõ tay (chưa có hồ sơ) là
--     trường hợp bình thường, và mọi dòng cũ đều bắt đầu ở NULL.
--   • ON DELETE SET NULL — xoá hồ sơ đối tác thì dòng thành viên tụt về mức snapshot, KHÔNG kéo
--     theo việc mất thành viên khỏi đoàn.
--   • KHÔNG BACKFILL TỰ ĐỘNG — xem §4.
--
-- Cách chạy (LƯU Ý: luôn kèm --default-character-set=utf8mb4, nếu không mọi chữ tiếng Việt
-- patch ghi vào sẽ bị mojibake):
--   mysql --default-character-set=utf8mb4 -u root -p <ten_db> < 2026-08-14_visit_guest_member_organization_partner.sql
-- =============================================================================================

-- ── §1. Thêm cột (idempotent) ────────────────────────────────────────────────────────────────
SET @col_exists := (
  SELECT COUNT(*) FROM information_schema.COLUMNS
  WHERE TABLE_SCHEMA = DATABASE()
    AND TABLE_NAME = 'visit_guest_members'
    AND COLUMN_NAME = 'organization_partner_id');

SET @sql := IF(@col_exists = 0,
  'ALTER TABLE visit_guest_members
     ADD COLUMN organization_partner_id BIGINT UNSIGNED NULL
       COMMENT ''Thành viên này đã được người đăng ký chọn từ hồ sơ đối tác nào (partners). NULL = tổ chức gõ tay hoặc chưa xác định — matcher có thể gợi ý sau. Cột organization ở trên vẫn là snapshot hiển thị và KHÔNG chạy theo đối tác đổi tên.''
       AFTER organization',
  'SELECT ''[skip] cột organization_partner_id đã tồn tại''');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

-- ── §2. Index + khoá ngoại (idempotent) ──────────────────────────────────────────────────────
SET @idx_exists := (
  SELECT COUNT(*) FROM information_schema.STATISTICS
  WHERE TABLE_SCHEMA = DATABASE()
    AND TABLE_NAME = 'visit_guest_members'
    AND INDEX_NAME = 'idx_vgm_organization_partner');

SET @sql := IF(@idx_exists = 0,
  'ALTER TABLE visit_guest_members
     ADD KEY idx_vgm_organization_partner (organization_partner_id)',
  'SELECT ''[skip] idx_vgm_organization_partner đã tồn tại''');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @fk_exists := (
  SELECT COUNT(*) FROM information_schema.TABLE_CONSTRAINTS
  WHERE TABLE_SCHEMA = DATABASE()
    AND TABLE_NAME = 'visit_guest_members'
    AND CONSTRAINT_NAME = 'fk_vgm_organization_partner');

SET @sql := IF(@fk_exists = 0,
  'ALTER TABLE visit_guest_members
     ADD CONSTRAINT fk_vgm_organization_partner
       FOREIGN KEY (organization_partner_id)
       REFERENCES partners (partner_id)
       ON UPDATE CASCADE ON DELETE SET NULL',
  'SELECT ''[skip] fk_vgm_organization_partner đã tồn tại''');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

-- ── §2b. match_source: thêm 'REGISTRATION_SELECTED' (idempotent) ─────────────────────────────
-- Liên kết sinh ra vì NGƯỜI ĐĂNG KÝ đã chọn hồ sơ ngay trên form khác về bản chất với 'MANUAL'
-- (nhân viên bấm liên kết ở màn biên bản) và với 'AUTO_NAME' (hệ thống suy từ tên). Gộp cả ba vào
-- một giá trị thì về sau không ai trả lời được "quan hệ này do ai quyết" — mà đó chính là câu hỏi
-- phân biệt một quyết định với một phỏng đoán.
--
-- ADDITIVE: chỉ nối thêm giá trị vào cuối ENUM, không đổi/không xoá giá trị nào đang dùng, nên mọi
-- dòng hiện có giữ nguyên. Đặt CUỐI danh sách để không xáo trộn thứ tự nội bộ của các giá trị cũ.
SET @has_regsel := (
  SELECT COUNT(*) FROM information_schema.COLUMNS
  WHERE TABLE_SCHEMA = DATABASE()
    AND TABLE_NAME = 'visit_guest_partner_links'
    AND COLUMN_NAME = 'match_source'
    AND COLUMN_TYPE LIKE '%REGISTRATION_SELECTED%');

SET @sql := IF(@has_regsel = 0,
  'ALTER TABLE visit_guest_partner_links
     MODIFY COLUMN match_source ENUM(
       ''AUTO_NAME'',
       ''AUTO_EMAIL_DOMAIN'',
       ''MANUAL'',
       ''CREATED_FROM_GUEST'',
       ''BUSINESS_CARD_OCR'',
       ''REGISTRATION_SELECTED''
     ) NOT NULL DEFAULT ''MANUAL''
     COMMENT ''Nguồn của quan hệ. REGISTRATION_SELECTED = người đăng ký tự chọn hồ sơ đối tác ngay trên form (danh tính ổn định, không phải máy suy). AUTO_NAME/AUTO_EMAIL_DOMAIN = hệ thống suy ra. MANUAL = người dùng bấm liên kết ở màn biên bản.''',
  'SELECT ''[skip] match_source đã có REGISTRATION_SELECTED''');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

-- ── §2c. Một thành viên chỉ có TỐI ĐA MỘT dòng liên kết (idempotent + có điều kiện) ───────────
-- Ứng dụng đã bảo đảm điều này (tái dùng dòng cũ thay vì thêm dòng mới), nhưng ràng buộc ở DB mới
-- là thứ chặn được cả những đường ghi chưa lường tới. MySQL cho phép nhiều NULL trong UNIQUE nên
-- dòng gắn với minute_participant (guest_member_id NULL) không bị ảnh hưởng.
--
-- CHỈ thêm khi dữ liệu hiện tại đã sạch. Nếu đang có trùng, patch KHÔNG ép: ALTER sẽ fail giữa
-- chừng và để lại một lần chạy dở — thà báo ra để người xử lý trước.
SET @dup_guest := (
  SELECT COUNT(*) FROM (
    SELECT guest_member_id FROM visit_guest_partner_links
    WHERE guest_member_id IS NOT NULL
    GROUP BY guest_member_id HAVING COUNT(*) > 1) d);

SET @uq_exists := (
  SELECT COUNT(*) FROM information_schema.STATISTICS
  WHERE TABLE_SCHEMA = DATABASE()
    AND TABLE_NAME = 'visit_guest_partner_links'
    AND INDEX_NAME = 'uq_vgpl_guest_member');

SET @sql := IF(@uq_exists > 0,
  'SELECT ''[skip] uq_vgpl_guest_member đã tồn tại''',
  IF(@dup_guest = 0,
    'ALTER TABLE visit_guest_partner_links
       ADD UNIQUE KEY uq_vgpl_guest_member (guest_member_id)',
    'SELECT ''[BỎ QUA] còn thành viên có nhiều dòng liên kết — dọn trùng rồi chạy lại patch để thêm uq_vgpl_guest_member'' AS warning'));
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

-- Danh sách trùng (nếu có) để xử lý tay.
SELECT guest_member_id, COUNT(*) AS link_rows,
       GROUP_CONCAT(CONCAT(link_id, ':', partner_id, ':', match_status) ORDER BY link_id) AS rows_detail
FROM visit_guest_partner_links
WHERE guest_member_id IS NOT NULL
GROUP BY guest_member_id
HAVING COUNT(*) > 1;

-- ── §3. Audit dữ liệu hiện có (READ-ONLY — chạy trước khi quyết định backfill) ────────────────
-- Ba con số dưới đây là đầu vào cho quyết định ở §4. Không câu nào ghi dữ liệu.

-- 3.1 — Thành viên đã có liên kết CONFIRMED sẵn: đây là nhóm backfill được AN TOÀN, vì quan hệ
--       đã do người dùng xác nhận chứ không phải máy đoán.
SELECT COUNT(*) AS members_with_confirmed_link
FROM visit_guest_members g
JOIN visit_guest_partner_links l
  ON l.guest_member_id = g.guest_member_id
 AND l.match_status = 'CONFIRMED'
WHERE g.organization_partner_id IS NULL;

-- 3.2 — Thành viên có tên tổ chức khớp CHÍNH XÁC đúng MỘT hồ sơ đối tác (chuẩn hoá: trim, lower,
--       gộp khoảng trắng). Khớp từ 2 hồ sơ trở lên là mơ hồ → không backfill, đưa vào §5.
SELECT COUNT(*) AS members_exact_unique_name_match
FROM visit_guest_members g
WHERE g.organization_partner_id IS NULL
  AND (SELECT COUNT(*) FROM partners p
       WHERE LOWER(TRIM(REGEXP_REPLACE(p.name, '[[:space:]]+', ' ')))
           = LOWER(TRIM(REGEXP_REPLACE(g.organization, '[[:space:]]+', ' ')))) = 1;

-- 3.3 — Liên kết CONFIRMED đang trỏ tới hồ sơ đã bị TỪ CHỐI. Đây là nợ dữ liệu có thật, phải do
--       người quyết định — patch KHÔNG tự sửa (xem §5).
SELECT COUNT(*) AS confirmed_links_to_rejected_profiles
FROM visit_guest_partner_links l
JOIN partners p ON p.partner_id = l.partner_id
WHERE l.match_status = 'CONFIRMED' AND p.profile_status = 'REJECTED';

-- ── §4. Backfill — CHỈ từ quan hệ ĐÃ ĐƯỢC XÁC NHẬN ───────────────────────────────────────────
-- Quy tắc: chỉ điền `organization_partner_id` khi thành viên đó ĐÃ có một liên kết CONFIRMED, và
-- chỉ có đúng MỘT đối tác trong số các liên kết đó. Nguồn ở đây là quyết định của con người, nên
-- chép lại là ghi nhận chứ không phải suy đoán.
--
-- KHÔNG backfill bằng cách so tên (kể cả khớp chính xác, kể cả duy nhất). Cột này mang nghĩa
-- "người dùng ĐÃ CHỌN hồ sơ này"; điền bằng suy luận chuỗi là đặt điều thay người dùng, và về sau
-- không ai phân biệt được đâu là lựa chọn thật đâu là máy đoán. Số ở §3.2 để product cân nhắc một
-- đợt backfill riêng có người duyệt, không phải để chạy tự động ở đây.
--
-- KHÔNG backfill từ liên kết trỏ tới hồ sơ REJECTED/DRAFT: chép một quan hệ không còn hợp lệ vào
-- cột mới chỉ làm nợ dữ liệu lan sang chỗ khác.
UPDATE visit_guest_members g
JOIN (
  SELECT
    l.guest_member_id,
    MIN(l.partner_id)             AS partner_id,
    COUNT(DISTINCT l.partner_id)  AS partner_count
  FROM visit_guest_partner_links l
  JOIN partners p ON p.partner_id = l.partner_id
  WHERE l.match_status = 'CONFIRMED'
    AND l.guest_member_id IS NOT NULL
    AND p.profile_status IN ('APPROVED', 'PENDING_APPROVAL')
  GROUP BY l.guest_member_id
) m ON m.guest_member_id = g.guest_member_id
SET g.organization_partner_id = m.partner_id
WHERE m.partner_count = 1
  AND g.organization_partner_id IS NULL;

-- ── §5. Report cần người quyết định (READ-ONLY) ──────────────────────────────────────────────
-- Không tự xoá/sửa. Staff Leader hoặc product owner quyết từng trường hợp: gỡ liên kết, sửa hồ sơ
-- rồi gửi duyệt lại, hay giữ nguyên như lịch sử.
SELECT
  l.link_id, l.visit_request_id, l.visit_instance_id,
  l.guest_member_id, l.minute_participant_id,
  l.partner_id, p.name AS partner_name, p.profile_status,
  l.created_by, l.created_at
FROM visit_guest_partner_links l
JOIN partners p ON p.partner_id = l.partner_id
WHERE l.match_status = 'CONFIRMED'
  AND p.profile_status IN ('REJECTED', 'DRAFT')
ORDER BY l.created_at;

-- Thành viên có tên tổ chức khớp NHIỀU hồ sơ — mơ hồ, cần người chọn.
SELECT
  g.guest_member_id, g.visit_request_id, g.organization,
  (SELECT COUNT(*) FROM partners p
   WHERE LOWER(TRIM(REGEXP_REPLACE(p.name, '[[:space:]]+', ' ')))
       = LOWER(TRIM(REGEXP_REPLACE(g.organization, '[[:space:]]+', ' ')))) AS matching_profiles
FROM visit_guest_members g
WHERE g.organization_partner_id IS NULL
HAVING matching_profiles > 1
ORDER BY g.visit_request_id;

-- ── §6. Verify ───────────────────────────────────────────────────────────────────────────────
SELECT
  COUNT(*)                                        AS total_guest_members,
  SUM(organization_partner_id IS NOT NULL)        AS with_stable_partner_id,
  SUM(organization_partner_id IS NULL)            AS free_text_or_undetermined
FROM visit_guest_members;

-- Không dòng nào được trỏ tới partner_id không tồn tại. Kỳ vọng: 0 dòng (FK đã chặn, đây là chốt lại).
SELECT g.guest_member_id, g.organization_partner_id
FROM visit_guest_members g
WHERE g.organization_partner_id IS NOT NULL
  AND NOT EXISTS (SELECT 1 FROM partners p WHERE p.partner_id = g.organization_partner_id);

-- ── §7. Rollback (chạy tay nếu cần) ──────────────────────────────────────────────────────────
-- ALTER TABLE visit_guest_members DROP FOREIGN KEY fk_vgm_organization_partner;
-- ALTER TABLE visit_guest_members DROP KEY idx_vgm_organization_partner;
-- ALTER TABLE visit_guest_members DROP COLUMN organization_partner_id;
