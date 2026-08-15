-- =============================================================================================
-- MIN-01..MIN-04 — Biên bản phải NHỚ nguồn của từng người tham gia
--
-- Vấn đề hiện tại: `minute_participants` chỉ có 2 cột nguồn (`user_id`, `guest_member_id`) và
-- KHÔNG có gì khác. Hệ quả:
--
--   • MIN-02 — Không biết một dòng khách là GUEST hay EXTERNAL_SUPPORT. `guest_member_id != NULL`
--     bị quy hết về nhãn "Khách", nên phiên dịch / trợ lý đi cùng đoàn hiển thị sai loại. Đọc
--     ngược `visit_guest_members.member_type` lúc hiển thị KHÔNG thay thế được cột snapshot: biên
--     bản đã chốt là bản ghi lịch sử, đổi loại thành viên sau đó không được sửa quá khứ.
--
--   • MIN-03 — Xóa một người NGUỒN khỏi biên bản là DELETE thật. Lần "Đồng bộ người mới" sau đó
--     thấy họ vẫn nằm trong danh sách đoàn/participant nên thêm lại — quyết định của Host bị quên.
--     Cần một trạng thái "đã loại khỏi biên bản" tồn tại được, thay vì xóa dòng.
--
--   • MIN-01 — Không có ràng buộc nào ở DB chặn 2 dòng cùng `guest_member_id` trong 1 biên bản.
--     Dedupe hiện chỉ nằm ở tầng ứng dụng, nên 2 request đồng thời (hoặc gọi API trực tiếp) vẫn
--     tạo được trùng.
--
--   • Đầu mối đoàn — không biết dòng nào đang giữ vai trò đầu mối, nên UI không gắn được nhãn bổ
--     sung "· Đầu mối" mà không phải suy lại từ form detail mỗi lần render.
--
-- Tính chất:
--   • ADDITIVE — chỉ THÊM cột/index. Không sửa, không xoá, không đổi kiểu cột nào đang có.
--   • IDEMPOTENT — chạy lại nhiều lần vẫn an toàn (kiểm tra information_schema trước mỗi bước).
--   • KHÔNG TỰ GỘP/XOÁ dữ liệu trùng cũ. §4 chỉ BÁO CÁO; quyết định gộp là của người dùng.
--   • Backfill ở §3 chỉ điền những gì suy được CHẮC CHẮN từ khoá ngoại đang có.
--
-- Cách chạy (LƯU Ý: luôn kèm --default-character-set=utf8mb4, nếu không mọi chữ tiếng Việt
-- patch ghi vào sẽ bị mojibake):
--   mysql --default-character-set=utf8mb4 -u root -p <ten_db> < 2026-08-15_minute_participant_source_identity.sql
-- =============================================================================================
SET @pems_old_safe_updates := @@SESSION.SQL_SAFE_UPDATES;
SET SESSION SQL_SAFE_UPDATES = 0;
-- ── §1. Thêm cột (idempotent) ────────────────────────────────────────────────────────────────

-- 1.1 — Loại nguồn tại thời điểm đồng bộ (SNAPSHOT, không phải khoá ngoại).
SET @col_exists := (
  SELECT COUNT(*) FROM information_schema.COLUMNS
  WHERE TABLE_SCHEMA = DATABASE()
    AND TABLE_NAME = 'minute_participants'
    AND COLUMN_NAME = 'source_member_type');

SET @sql := IF(@col_exists = 0,
  'ALTER TABLE minute_participants
     ADD COLUMN source_member_type VARCHAR(30) NULL
       COMMENT ''Loại thành viên nguồn khi được đưa vào biên bản: GUEST | EXTERNAL_SUPPORT. NULL = dòng nội bộ (user_id) hoặc dòng nhập tay. Là SNAPSHOT: biên bản đã chốt không đổi theo việc phân loại lại thành viên về sau.''
       AFTER guest_member_id',
  'SELECT ''minute_participants.source_member_type: đã có, bỏ qua'' AS note');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

-- 1.2 — Dòng này có phải đầu mối của cơ sở không (vai trò BỔ SUNG, không thay loại nguồn).
SET @col_exists := (
  SELECT COUNT(*) FROM information_schema.COLUMNS
  WHERE TABLE_SCHEMA = DATABASE()
    AND TABLE_NAME = 'minute_participants'
    AND COLUMN_NAME = 'is_operational_contact');

SET @sql := IF(@col_exists = 0,
  'ALTER TABLE minute_participants
     ADD COLUMN is_operational_contact TINYINT(1) NOT NULL DEFAULT 0
       COMMENT ''1 = người này là đầu mối của cơ sở. Là vai trò BỔ SUNG: một dòng vừa là GUEST vừa là đầu mối, hiển thị "Khách · Đầu mối". Không thay source_member_type.''
       AFTER source_member_type',
  'SELECT ''minute_participants.is_operational_contact: đã có, bỏ qua'' AS note');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

-- 1.3 — Trạng thái đồng bộ (MIN-03). EXCLUDED = Host đã loại khỏi biên bản; dòng VẪN TỒN TẠI để
--       lần đồng bộ sau biết mà không thêm lại, và để khôi phục được.
SET @col_exists := (
  SELECT COUNT(*) FROM information_schema.COLUMNS
  WHERE TABLE_SCHEMA = DATABASE()
    AND TABLE_NAME = 'minute_participants'
    AND COLUMN_NAME = 'sync_state');

SET @sql := IF(@col_exists = 0,
  'ALTER TABLE minute_participants
     ADD COLUMN sync_state ENUM(''ACTIVE'',''EXCLUDED'') NOT NULL DEFAULT ''ACTIVE''
       COMMENT ''ACTIVE = đang trong biên bản. EXCLUDED = đã loại khỏi biên bản nhưng GIỮ dòng, để "Đồng bộ người mới" không thêm lại và để khôi phục. Dòng nhập tay vẫn được xoá hẳn.''
       AFTER is_operational_contact',
  'SELECT ''minute_participants.sync_state: đã có, bỏ qua'' AS note');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

-- ── §2. Index tra cứu theo trạng thái đồng bộ (idempotent) ───────────────────────────────────
SET @idx_exists := (
  SELECT COUNT(*) FROM information_schema.STATISTICS
  WHERE TABLE_SCHEMA = DATABASE()
    AND TABLE_NAME = 'minute_participants'
    AND INDEX_NAME = 'idx_minute_participants_sync_state');

SET @sql := IF(@idx_exists = 0,
  'ALTER TABLE minute_participants
     ADD KEY idx_minute_participants_sync_state (minutes_id, sync_state)',
  'SELECT ''idx_minute_participants_sync_state: đã có, bỏ qua'' AS note');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

-- ── §3. Backfill — chỉ những gì suy được CHẮC CHẮN ───────────────────────────────────────────

-- 3.1 — Loại nguồn của các dòng khách đã có, lấy từ chính thành viên mà dòng đang trỏ tới.
--       Dòng nội bộ / nhập tay giữ NULL (đúng nghĩa: không có thành viên nguồn).
UPDATE minute_participants mp
  JOIN visit_guest_members g ON g.guest_member_id = mp.guest_member_id
SET mp.source_member_type = g.member_type
WHERE mp.guest_member_id IS NOT NULL
  AND mp.source_member_type IS NULL;

-- 3.2 — Đánh dấu đầu mối: dòng có guest_member_id trùng với đầu mối đã liên kết của chính cơ sở
--       mà biên bản này thuộc về. Chỉ dựa vào QUAN HỆ (operational_contact_guest_member_id),
--       KHÔNG so chuỗi tên — trùng tên không phải trùng người.
UPDATE minute_participants mp
  JOIN minutes m ON m.minutes_id = mp.minutes_id
  JOIN visit_instance_form_details d ON d.visit_instance_id = m.visit_instance_id
SET mp.is_operational_contact = 1
WHERE mp.guest_member_id IS NOT NULL
  AND d.operational_contact_guest_member_id IS NOT NULL
  AND mp.guest_member_id = d.operational_contact_guest_member_id;

-- ── §4. Ràng buộc chống trùng nguồn trong 1 biên bản (MIN-01) ────────────────────────────────
--
-- MySQL cho phép NHIỀU NULL trong UNIQUE index, nên ràng buộc này chỉ áp cho dòng có nguồn khách
-- — dòng nội bộ và dòng nhập tay (guest_member_id NULL) vẫn thêm được thoải mái. Đó chính xác là
-- ngữ nghĩa cần: "một thành viên đoàn chỉ xuất hiện một lần trong một biên bản".
--
-- KHÔNG tạo index nếu dữ liệu hiện tại đã có trùng: lệnh sẽ lỗi, và quan trọng hơn — gộp/xoá dòng
-- trùng cũ là quyết định nghiệp vụ của người dùng, không phải việc của migration. Trường hợp đó
-- patch BÁO CÁO ra danh sách để xử lý thủ công rồi chạy lại.

SET @dup_count := (
  SELECT COUNT(*) FROM (
    SELECT minutes_id, guest_member_id
    FROM minute_participants
    WHERE guest_member_id IS NOT NULL
    GROUP BY minutes_id, guest_member_id
    HAVING COUNT(*) > 1
  ) AS dups);

SET @idx_exists := (
  SELECT COUNT(*) FROM information_schema.STATISTICS
  WHERE TABLE_SCHEMA = DATABASE()
    AND TABLE_NAME = 'minute_participants'
    AND INDEX_NAME = 'uq_minute_participants_minute_guest');

SET @sql := IF(@idx_exists > 0,
  'SELECT ''uq_minute_participants_minute_guest: đã có, bỏ qua'' AS note',
  IF(@dup_count = 0,
    'ALTER TABLE minute_participants
       ADD UNIQUE KEY uq_minute_participants_minute_guest (minutes_id, guest_member_id)',
    'SELECT ''BỎ QUA tạo unique index: đang có thành viên đoàn xuất hiện nhiều lần trong cùng 1 biên bản. Xem danh sách ở truy vấn kiểm tra bên dưới, xử lý thủ công rồi chạy lại patch này.'' AS warning'));
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

-- ── §5. Truy vấn kiểm tra (chạy tay sau khi patch) ───────────────────────────────────────────
--
-- 5.1 — Thành viên đoàn bị lặp trong cùng 1 biên bản (phải RỖNG thì unique index ở §4 mới tạo được):
--
--   SELECT mp.minutes_id, mp.guest_member_id, COUNT(*) AS so_dong,
--          GROUP_CONCAT(mp.minute_participant_id) AS cac_dong,
--          MAX(mp.full_name_snapshot) AS ten
--   FROM minute_participants mp
--   WHERE mp.guest_member_id IS NOT NULL
--   GROUP BY mp.minutes_id, mp.guest_member_id
--   HAVING COUNT(*) > 1;
--
-- 5.2 — Nghi vấn trùng người NHƯNG khác id (di sản MIN-01: cùng người từng bị tạo 2 guest_member_id).
--       KHÔNG tự gộp — đây là danh sách để hỏi lại người dùng:
--
--   SELECT minutes_id, full_name_snapshot, role_snapshot, organization_snapshot,
--          COUNT(*) AS so_dong, GROUP_CONCAT(minute_participant_id) AS cac_dong
--   FROM minute_participants
--   GROUP BY minutes_id, LOWER(TRIM(full_name_snapshot)), LOWER(TRIM(COALESCE(role_snapshot,''))),
--            LOWER(TRIM(COALESCE(organization_snapshot,'')))
--   HAVING COUNT(*) > 1;
--
-- 5.3 — Dòng khách chưa có loại nguồn (phải RỖNG sau §3.1; còn dòng nào nghĩa là guest_member_id
--       trỏ tới thành viên đã bị xoá — ON DELETE SET NULL sẽ dọn, hoặc dữ liệu cần xem lại):
--
--   SELECT minute_participant_id, minutes_id, guest_member_id, full_name_snapshot
--   FROM minute_participants
--   WHERE guest_member_id IS NOT NULL AND source_member_type IS NULL;
--
-- 5.4 — Đầu mối được đánh dấu:
--
--   SELECT minutes_id, COUNT(*) AS so_dau_moi
--   FROM minute_participants WHERE is_operational_contact = 1 GROUP BY minutes_id;
--
-- ── §6. Hoàn tác ─────────────────────────────────────────────────────────────────────────────
--
-- Patch chỉ THÊM, nên hoàn tác là bỏ đúng những gì đã thêm (dữ liệu cũ không đổi):
--
--   ALTER TABLE minute_participants DROP INDEX uq_minute_participants_minute_guest;
--   ALTER TABLE minute_participants DROP INDEX idx_minute_participants_sync_state;
--   ALTER TABLE minute_participants DROP COLUMN sync_state;
--   ALTER TABLE minute_participants DROP COLUMN is_operational_contact;
--   ALTER TABLE minute_participants DROP COLUMN source_member_type;
--
-- LƯU Ý: hoàn tác sau khi người dùng đã "loại khỏi biên bản" ai đó sẽ làm MẤT quyết định đó —
-- các dòng EXCLUDED trở lại thành dòng bình thường và sẽ hiện lại trong biên bản.
-- =============================================================================================
SET SESSION SQL_SAFE_UPDATES = @pems_old_safe_updates;