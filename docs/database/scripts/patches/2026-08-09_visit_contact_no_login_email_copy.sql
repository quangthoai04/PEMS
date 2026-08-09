-- =====================================================================
-- 2026-08-09 — operational-contact invitation copy: drop the Google-login sentence
--
-- WHAT THIS IS FOR
--   The operational-contact invitation stopped requiring a sign-in some time ago. Each email now
--   carries TWO one-time, action-bound links (one ACCEPT, one DECLINE); either opens a confirmation
--   page that mutates nothing on GET, and the reader confirms from there without a PEMS account.
--   The template copy never caught up: VISIT_CONTACT_CLAIM and VISIT_CONTACT_TRANSFER still told the
--   recipient the page "yêu cầu đăng nhập bằng đúng tài khoản Google". That sentence now describes a
--   requirement that does not exist, and it is the single most likely reason an external guest
--   abandons the invitation — which leaves their campus behind the contact gate indefinitely.
--
-- WHICH FILE TO RUN ON AN EXISTING DATABASE
--   THIS ONE, and only this one. docs/database/scripts/PEMS_FULL_VS_31_07_NEW.sql carries the same
--   corrected copy so that a FRESH import is right from the start, but it is a create-from-scratch
--   script: running it against a live database to pick up two sentences would rebuild the schema and
--   reseed business data. Nothing here drops, creates, truncates or reseeds anything.
--
-- SCOPE
--   Two rows of email_templates. No DDL, no INSERT, no DELETE, no other template touched.
--
-- IDEMPOTENT
--   The WHERE clause is the stale sentence itself. After a successful run neither body contains it,
--   so a second run matches zero rows: no further edit, and revision does not climb. Each template
--   is rewritten by exactly ONE statement covering both languages, so one real migration bumps
--   revision by exactly one — never once per language.
--
-- WHY REPLACE() AND NOT A WHOLE-BODY OVERWRITE
--   An Admin may have edited this prose through the template editor. Replacing the whole body would
--   silently discard that. Only the stale sentences are substituted; every other byte of the live
--   body — including anything an operator wrote — is left exactly as it stands.
--
-- WHAT IS DELIBERATELY NOT TOUCHED
--   variables_text, subject_vi, subject_en, name, description, status, and {{actionBlock}}. The
--   action block stays a placeholder because the accept/decline URLs are CREDENTIALS: the backend
--   mints the one-time tokens at send time and injects a trusted block. Putting raw {{acceptUrl}} /
--   {{declineUrl}} into an editable template would hand token composition to whoever can edit copy.
--
-- ROLLBACK
--   Reverse the two REPLACE pairs (swap each argument order) and run again; or restore the two rows
--   from a backup. Nothing else in the database depends on this wording.
-- =====================================================================

SET NAMES utf8mb4;

START TRANSACTION;

-- VISIT_CONTACT_CLAIM
UPDATE email_templates
   SET body_vi = REPLACE(REPLACE(body_vi,
           'Vui lòng dùng nút bên dưới để chấp nhận hoặc từ chối vai trò đầu mối liên hệ. Trang xác nhận yêu cầu đăng nhập bằng đúng tài khoản Google của địa chỉ email này.',
           'Vui lòng dùng các nút bên dưới để chấp nhận hoặc từ chối vai trò đầu mối liên hệ. Quý vị không cần đăng nhập PEMS. Mỗi nút mở một trang xác nhận bằng liên kết dùng một lần để Quý vị xem thông tin chuyến thăm mới nhất trước khi quyết định.'),
           '<strong>Lưu ý bảo mật:</strong> Liên kết có thời hạn và chỉ dùng được một lần. Vui lòng không chuyển tiếp email này cho người khác.',
           '<strong>Lưu ý bảo mật:</strong> Các liên kết có thời hạn và mỗi liên kết chỉ dùng được một lần. Vui lòng không chuyển tiếp email hoặc các liên kết này cho người khác.'),
       body_en = REPLACE(REPLACE(body_en,
           'Open the confirmation page below to accept or decline the contact role. The page asks you to sign in with the Google account of this email address.',
           'Use the buttons below to accept or decline the contact role. You do not need to sign in to PEMS. Each button opens a confirmation page using a one-time link, so you can review the latest visit details before deciding.'),
           '<strong>Security note:</strong> The link expires and can be used once. Please do not forward this email.',
           '<strong>Security note:</strong> The links expire and each can be used once. Do not forward the email or its links.'),
       revision = revision + 1
 WHERE template_code = 'VISIT_CONTACT_CLAIM'
   AND (body_vi LIKE '%đăng nhập bằng đúng tài khoản Google%'
     OR body_en LIKE '%sign in with the Google account%');

-- VISIT_CONTACT_TRANSFER
UPDATE email_templates
   SET body_vi = REPLACE(REPLACE(body_vi,
           'Vui lòng dùng nút bên dưới để chấp nhận hoặc từ chối. Nếu Quý vị chấp nhận, các trao đổi tiếp theo về yêu cầu này sẽ được gửi tới địa chỉ email này.',
           'Vui lòng dùng các nút bên dưới để chấp nhận hoặc từ chối. Quý vị không cần đăng nhập PEMS để phản hồi lời mời này. Nếu Quý vị chấp nhận, các trao đổi tiếp theo về yêu cầu này sẽ được gửi tới địa chỉ email này.'),
           '<strong>Lưu ý bảo mật:</strong> Liên kết có thời hạn và chỉ dùng được một lần. Trang xác nhận yêu cầu đăng nhập bằng đúng tài khoản Google của địa chỉ email này.',
           '<strong>Lưu ý bảo mật:</strong> Các liên kết có thời hạn và mỗi liên kết chỉ dùng được một lần. Vui lòng không chuyển tiếp email hoặc các liên kết này cho người khác.'),
       body_en = REPLACE(REPLACE(body_en,
           'Open the confirmation page below to accept or decline. If you accept, further correspondence about this request will be sent to this address.',
           'Use the buttons below to accept or decline. You do not need to sign in to PEMS to respond. If you accept, further correspondence about this request will be sent to this address.'),
           '<strong>Security note:</strong> The link expires and can be used once. The page asks you to sign in with the Google account of this email address.',
           '<strong>Security note:</strong> The links expire and each can be used once. Please do not forward the email or its links.'),
       revision = revision + 1
 WHERE template_code = 'VISIT_CONTACT_TRANSFER'
   AND (body_vi LIKE '%đăng nhập bằng đúng tài khoản Google%'
     OR body_en LIKE '%sign in with the Google account%');

COMMIT;

-- ── Verification ─────────────────────────────────────────────────────────────
-- Expected for BOTH rows: stale_vi = 0, stale_en = 0, action_block_vi = 1, action_block_en = 1.
-- A stale_* of 1 means the row was not matched (already hand-edited into a third wording — compare
-- it with the canonical seed before doing anything else). An action_block_* of 0 means the body has
-- lost its action block and the template can no longer render its buttons.
SELECT
    template_code,
    revision,
    body_vi LIKE '%đăng nhập bằng đúng tài khoản Google%' AS stale_vi,
    body_en LIKE '%sign in with the Google account%' AS stale_en,
    body_vi LIKE '%{{actionBlock}}%' AS action_block_vi,
    body_en LIKE '%{{actionBlock}}%' AS action_block_en
FROM email_templates
WHERE template_code IN (
    'VISIT_CONTACT_CLAIM',
    'VISIT_CONTACT_TRANSFER'
);
