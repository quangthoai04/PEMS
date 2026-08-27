-- =============================================================================================
-- 2026-08-28 — VisitProcess "Nhắc nhở chuyến thăm": tách legacy target_group HOST_AND_PARTICIPANTS
--
-- VẤN ĐỀ
--   UI hiện tại (2 card Người phụ trách / Thành phần tham gia) chỉ còn tạo target_group HOST hoặc
--   PARTICIPANTS — không bao giờ tạo HOST_AND_PARTICIPANTS nữa (validator cũng vừa chặn ghi mới giá
--   trị này, xem SaveVisitInstanceReminderSettingsCommandValidator). Nhưng cột/enum HOST_AND_PARTICIPANTS
--   vẫn còn trong schema và trong dữ liệu demo/legacy (PEMS_FULL_VS_31_07_NEW.sql seed 3 dòng dùng nó).
--
--   Nếu một visit_instance_id có SẴN 1 dòng PENDING HOST_AND_PARTICIPANTS (từ trước khi validator
--   chặn) VÀ Host dùng UI mới lưu thêm dòng HOST/PARTICIPANTS cho cùng instance đó, cả 2 dòng đều
--   PENDING và VisitReminderDispatchService.ResolveRecipientsAsync sẽ include Host ở CẢ HAI dòng
--   (HOST_AND_PARTICIPANTS include Host, và dòng HOST cũng include Host) → Host nhận 2 thông báo/email
--   giống nhau cho cùng 1 mốc.
--
-- THAY ĐỔI
--   Chỉ đụng dòng PENDING HOST_AND_PARTICIPANTS (SENT/FAILED/CANCELLED là lịch sử, giữ nguyên — patch
--   này không xóa và không viết lại lịch sử).
--
--   §1: với mỗi dòng PENDING HOST_AND_PARTICIPANTS, tạo dòng HOST tương ứng (cùng channel, cùng
--       offset_minutes/scheduled_at, PENDING) NẾU visit_instance_id đó CHƯA có dòng HOST cho channel
--       đó — merge rule: dòng canonical có sẵn LUÔN thắng, không tạo trùng, không ghi đè lịch đã có.
--   §2: tương tự cho PARTICIPANTS.
--   §3: mọi dòng HOST_AND_PARTICIPANTS còn PENDING sau §1/§2 được chuyển CANCELLED (không xóa) với lý
--       do LEGACY_TARGET_GROUP_SPLIT — nó sẽ không bao giờ được dispatch lại (due-query chỉ nhìn
--       PENDING), nên "cancel" ở đây là dọn trạng thái cho đúng sự thật, không có gì bị hủy thật.
--
-- KHÔNG TẠO DUPLICATE
--   Unique key (visit_instance_id, channel, target_group) tự bảo vệ: §1/§2 chỉ INSERT khi
--   NOT EXISTS dòng canonical cùng khóa. Chạy lại lần 2: §1/§2 không còn dòng nguồn PENDING nào (đã bị
--   §3 chuyển CANCELLED ở lần chạy trước) nên toàn bộ script là no-op — idempotent thật, không chỉ
--   "không lỗi khi chạy lại".
--
-- WHICH FILE TO RUN ON AN EXISTING DATABASE
--   THIS ONE. Không liên quan schema — PEMS_FULL_VS_31_07_NEW.sql là fresh-install script, seed data
--   demo của nó vẫn cố ý giữ HOST_AND_PARTICIPANTS làm ví dụ; chạy lại nó vào DB sống sẽ dựng lại toàn
--   bộ schema, không phải cách patch dữ liệu.
--
-- IDEMPOTENT — xem "KHÔNG TẠO DUPLICATE" ở trên. An toàn chạy lại nhiều lần.
--
-- ROLLBACK — xem §5 cuối file.
-- =============================================================================================
SET @pems_old_safe_updates := @@SESSION.SQL_SAFE_UPDATES;
SET SESSION SQL_SAFE_UPDATES = 0;

-- ── §1. Tách HOST từ mọi HOST_AND_PARTICIPANTS còn PENDING, chỉ khi instance đó chưa có dòng HOST ──
INSERT INTO visit_instance_reminder_settings
  (visit_instance_id, channel, target_group, offset_minutes, scheduled_at, status, created_at, created_by)
SELECT
  legacy.visit_instance_id, legacy.channel, 'HOST', legacy.offset_minutes, legacy.scheduled_at,
  'PENDING', NOW(), legacy.created_by
FROM visit_instance_reminder_settings legacy
WHERE legacy.target_group = 'HOST_AND_PARTICIPANTS'
  AND legacy.status = 'PENDING'
  AND NOT EXISTS (
    SELECT 1 FROM visit_instance_reminder_settings host_row
    WHERE host_row.visit_instance_id = legacy.visit_instance_id
      AND host_row.channel = legacy.channel
      AND host_row.target_group = 'HOST');

-- ── §2. Tách PARTICIPANTS tương tự ──
INSERT INTO visit_instance_reminder_settings
  (visit_instance_id, channel, target_group, offset_minutes, scheduled_at, status, created_at, created_by)
SELECT
  legacy.visit_instance_id, legacy.channel, 'PARTICIPANTS', legacy.offset_minutes, legacy.scheduled_at,
  'PENDING', NOW(), legacy.created_by
FROM visit_instance_reminder_settings legacy
WHERE legacy.target_group = 'HOST_AND_PARTICIPANTS'
  AND legacy.status = 'PENDING'
  AND NOT EXISTS (
    SELECT 1 FROM visit_instance_reminder_settings part_row
    WHERE part_row.visit_instance_id = legacy.visit_instance_id
      AND part_row.channel = legacy.channel
      AND part_row.target_group = 'PARTICIPANTS');

-- ── §3. Retire mọi dòng HOST_AND_PARTICIPANTS còn PENDING — CANCELLED, không xóa ──
UPDATE visit_instance_reminder_settings
SET status = 'CANCELLED',
    error_message = 'LEGACY_TARGET_GROUP_SPLIT: Đã tách thành dòng HOST/PARTICIPANTS riêng; nhóm HOST_AND_PARTICIPANTS không còn được UI hỗ trợ.',
    updated_at = NOW()
WHERE target_group = 'HOST_AND_PARTICIPANTS'
  AND status = 'PENDING';

-- ── §4. Verify ───────────────────────────────────────────────────────────────────────────────
-- Kỳ vọng: pending_legacy_remaining = 0.
SELECT COUNT(*) AS pending_legacy_remaining
FROM visit_instance_reminder_settings
WHERE target_group = 'HOST_AND_PARTICIPANTS' AND status = 'PENDING';

-- Không instance nào có cả HOST và HOST_AND_PARTICIPANTS cùng PENDING (hoặc PARTICIPANTS tương ứng).
-- Kỳ vọng: 0 dòng — §3 đã cancel hết legacy PENDING nên phép JOIN này luôn rỗng sau khi patch chạy.
SELECT a.visit_instance_id, a.channel, a.target_group, b.target_group AS overlapping_with
FROM visit_instance_reminder_settings a
JOIN visit_instance_reminder_settings b
  ON a.visit_instance_id = b.visit_instance_id AND a.channel = b.channel
WHERE a.status = 'PENDING' AND b.status = 'PENDING'
  AND a.target_group = 'HOST_AND_PARTICIPANTS'
  AND b.target_group IN ('HOST', 'PARTICIPANTS');

-- Số dòng HOST/PARTICIPANTS mới được tạo từ legacy (để đối chiếu thủ công nếu cần).
SELECT COUNT(*) AS split_rows_created
FROM visit_instance_reminder_settings
WHERE target_group IN ('HOST', 'PARTICIPANTS')
  AND status = 'PENDING'
  AND created_at >= (SELECT COALESCE(MIN(updated_at), NOW()) FROM visit_instance_reminder_settings
                      WHERE target_group = 'HOST_AND_PARTICIPANTS' AND status = 'CANCELLED'
                        AND error_message LIKE 'LEGACY_TARGET_GROUP_SPLIT%');

SET SESSION SQL_SAFE_UPDATES = @pems_old_safe_updates;

-- ── §5. Rollback (chạy tay nếu cần) ──────────────────────────────────────────────────────────
-- Không thể tự động hoàn tác an toàn: §3 không phân biệt được dòng CANCELLED nào do patch này gây ra
-- với một dòng đã CANCELLED từ trước bởi lý do khác (cùng chung status). Khôi phục từ backup trước
-- khi chạy patch nếu cần đảo ngược thật; hoặc thủ công theo error_message LIKE 'LEGACY_TARGET_GROUP_SPLIT%':
--   UPDATE visit_instance_reminder_settings SET status = 'PENDING', error_message = NULL
--   WHERE target_group = 'HOST_AND_PARTICIPANTS' AND error_message LIKE 'LEGACY_TARGET_GROUP_SPLIT%';
--   -- rồi xóa tay các dòng HOST/PARTICIPANTS mà §1/§2 vừa tạo (nhận diện qua created_at cỡ lúc chạy patch).
