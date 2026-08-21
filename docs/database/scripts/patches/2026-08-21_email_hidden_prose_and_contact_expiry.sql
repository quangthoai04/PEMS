-- =====================================================================
-- 2026-08-21 — email template fidelity plan, Phase B: hidden recipient-facing prose
-- removed from runtime action-block builders, moved into the templates that own it
--
-- WHAT THIS IS FOR
--   EmailComposition.cs's runtime action-block builders (AcceptDeclineBlock, DetailLinkBlock,
--   LogisticsActionBlock, LogisticsAssigneeActionBlock, VisitDetailBlock, ContactRoleInvitationBlock)
--   used to append hard-coded recipient-facing sentences — an expiry notice, a "this needs sign-in"
--   explanation — that the DB template never authored and an operator editing the template could
--   never see, move or remove. Ownership rule going forward: a runtime action block owns the
--   button/link/token only; any sentence a recipient needs to understand the action is the template's
--   own business prose.
--
--   VISIT_CONTACT_CLAIM / VISIT_CONTACT_TRANSFER additionally gain a real template variable,
--   contactExpiresAt: the operational-contact invitation's runtime block used to print the exact
--   expiry moment itself (a value that differs per send), so the previous generic "the links have an
--   expiry" sentence is now precise — "valid until {{contactExpiresAt}}" — sourced from the same
--   per-send timestamp the backend always had (OperationalContactInvitationService), not a second,
--   independent guess at it.
--
-- WHICH FILE TO RUN ON AN EXISTING DATABASE
--   THIS ONE, and only this one. docs/database/scripts/PEMS_FULL_VS_31_07_NEW.sql carries the same
--   corrected copy so a FRESH import is right from the start, but it is a create-from-scratch script:
--   running it against a live database would rebuild the schema and reseed business data. Nothing
--   here drops, creates, truncates or reseeds anything.
--
-- SCOPE
--   12 email_templates rows get a body_vi/body_en REPLACE (surgical — only the missing sentence is
--   inserted, every other byte an operator may have hand-edited is left exactly as it stands).
--   2 of those 12 (VISIT_CONTACT_CLAIM, VISIT_CONTACT_TRANSFER) also get their existing generic
--   "expiry" wording replaced with the specific {{contactExpiresAt}} form, and their variables_text
--   gains the new declared name. No DDL, no INSERT, no DELETE, no other template touched.
--
-- IDEMPOTENT
--   Each UPDATE's WHERE clause checks the sentence being inserted is not already present, so a second
--   run matches zero rows: no further edit, and revision does not climb on a repeat run. Each template
--   is rewritten by exactly ONE statement covering both languages, so one real migration bumps
--   revision by exactly one — never once per language.
--
-- WHY REPLACE() AND NOT A WHOLE-BODY OVERWRITE
--   An operator may have edited this prose through the template editor. Replacing the whole body would
--   silently discard that. Only the specific missing sentence is inserted (anchored on the text
--   immediately before/after it); every other byte of the live body — including anything an operator
--   wrote — is left exactly as it stands.
--
-- WHAT IS DELIBERATELY NOT TOUCHED
--   ACCOUNT_ACTIVATED and ACCOUNT_EMAIL_CONFIRMATION needed no body edit at all: both templates
--   already said everything their runtime block used to add as hidden prose (the confirm-email
--   template already has its own expiry sentence; the activated-account template already tells the
--   reader they can sign in with this same email) — so only EmailComposition.cs changed for those two,
--   nothing here. subject_vi, subject_en, name, description, status, and {{actionBlock}} /
--   {{senderXxx}} placeholders are untouched everywhere.
--
-- ROLLBACK
--   Reverse each REPLACE pair (swap the two string arguments) and run again; or restore the affected
--   rows from a backup. Nothing else in the database depends on this wording.
-- =====================================================================

SET NAMES utf8mb4;

START TRANSACTION;

-- VISIT_CONTACT_CLAIM — generic expiry wording becomes the specific {{contactExpiresAt}} form; the
-- variable joins the declared set.
UPDATE email_templates
   SET body_vi = REPLACE(body_vi,
           'Các liên kết có thời hạn và mỗi liên kết chỉ dùng được một lần.',
           'Các liên kết có hiệu lực đến <strong>{{contactExpiresAt}}</strong> và mỗi liên kết chỉ dùng được một lần.'),
       body_en = REPLACE(body_en,
           'The links expire and each can be used once.',
           'The links are valid until <strong>{{contactExpiresAt}}</strong> and each can be used once.'),
       variables_text = REPLACE(variables_text,
           'contactFullName,requestCode,delegationName,campusName,plannedTime,senderName',
           'contactFullName,requestCode,delegationName,campusName,plannedTime,contactExpiresAt,senderName'),
       revision = revision + 1
 WHERE template_code = 'VISIT_CONTACT_CLAIM'
   AND body_vi NOT LIKE '%{{contactExpiresAt}}%';

-- VISIT_CONTACT_TRANSFER — same change.
UPDATE email_templates
   SET body_vi = REPLACE(body_vi,
           'Các liên kết có thời hạn và mỗi liên kết chỉ dùng được một lần.',
           'Các liên kết có hiệu lực đến <strong>{{contactExpiresAt}}</strong> và mỗi liên kết chỉ dùng được một lần.'),
       body_en = REPLACE(body_en,
           'The links expire and each can be used once.',
           'The links are valid until <strong>{{contactExpiresAt}}</strong> and each can be used once.'),
       variables_text = REPLACE(variables_text,
           'contactFullName,currentContactName,requestCode,delegationName,campusName,plannedTime,senderName',
           'contactFullName,currentContactName,requestCode,delegationName,campusName,plannedTime,contactExpiresAt,senderName'),
       revision = revision + 1
 WHERE template_code = 'VISIT_CONTACT_TRANSFER'
   AND body_vi NOT LIKE '%{{contactExpiresAt}}%';

-- VISIT_PARTICIPANT_INVITATION — AcceptDeclineBlock's expiry note, moved in.
UPDATE email_templates
   SET body_vi = REPLACE(body_vi,
           'Vui lòng chọn một phương án bên dưới để chúng tôi chốt danh sách nhân sự tham gia.</p>{{actionBlock}}</div><div style="margin:20px 0 0;',
           'Vui lòng chọn một phương án bên dưới để chúng tôi chốt danh sách nhân sự tham gia.</p>{{actionBlock}}</div><p style="margin:0 0 12px;color:#64748b;font-size:12px;line-height:1.6">Liên kết phản hồi sẽ hết hạn sau 14 ngày và chỉ sử dụng được một lần.</p><div style="margin:20px 0 0;'),
       body_en = REPLACE(body_en,
           'Please choose one of the options below so we can confirm the supporting team.</p>{{actionBlock}}</div><div style="margin:20px 0 0;',
           'Please choose one of the options below so we can confirm the supporting team.</p>{{actionBlock}}</div><p style="margin:0 0 12px;color:#64748b;font-size:12px;line-height:1.6">The response link expires in 14 days and can be used once.</p><div style="margin:20px 0 0;'),
       revision = revision + 1
 WHERE template_code = 'VISIT_PARTICIPANT_INVITATION'
   AND body_vi NOT LIKE '%hết hạn sau 14 ngày%';

-- VISIT_STUDENT_INVITATION — same expiry note.
UPDATE email_templates
   SET body_vi = REPLACE(body_vi,
           'Nếu bạn nhận lời, thông tin tập trung và hướng dẫn chi tiết sẽ được gửi trước ngày diễn ra.</p>{{actionBlock}}</div><div style="margin:20px 0 0;',
           'Nếu bạn nhận lời, thông tin tập trung và hướng dẫn chi tiết sẽ được gửi trước ngày diễn ra.</p>{{actionBlock}}</div><p style="margin:0 0 12px;color:#64748b;font-size:12px;line-height:1.6">Liên kết phản hồi sẽ hết hạn sau 14 ngày và chỉ sử dụng được một lần.</p><div style="margin:20px 0 0;'),
       body_en = REPLACE(body_en,
           'If you accept, the meeting point and detailed instructions will be sent before the day.</p>{{actionBlock}}</div><div style="margin:20px 0 0;',
           'If you accept, the meeting point and detailed instructions will be sent before the day.</p>{{actionBlock}}</div><p style="margin:0 0 12px;color:#64748b;font-size:12px;line-height:1.6">The response link expires in 14 days and can be used once.</p><div style="margin:20px 0 0;'),
       revision = revision + 1
 WHERE template_code = 'VISIT_STUDENT_INVITATION'
   AND body_vi NOT LIKE '%hết hạn sau 14 ngày%';

-- VISIT_DEPARTMENT_STAFF_ASSIGNMENT — same expiry note (VI text distinguishes "nhiệm vụ" from the
-- logistics templates' "hạng mục", so the anchor stays unique to this template).
UPDATE email_templates
   SET body_vi = REPLACE(body_vi,
           'Vui lòng chọn một phương án bên dưới để phòng ban biết bạn có nhận nhiệm vụ này hay không.</p>{{actionBlock}}</div><div style="margin:20px 0 0;',
           'Vui lòng chọn một phương án bên dưới để phòng ban biết bạn có nhận nhiệm vụ này hay không.</p>{{actionBlock}}</div><p style="margin:0 0 12px;color:#64748b;font-size:12px;line-height:1.6">Liên kết phản hồi sẽ hết hạn sau 14 ngày và chỉ sử dụng được một lần.</p><div style="margin:20px 0 0;'),
       body_en = REPLACE(body_en,
           'Department</td><td style="padding:10px 14px;font-weight:600;color:#334155">{{departmentName}}</td></tr></tbody></table><div style="margin:20px 0;padding:16px 18px;background:#eff6ff;border:1px solid #bfdbfe;border-radius:8px"><p style="margin:0 0 12px;font-weight:700;color:#0f3d67">Your response is needed</p><p style="margin:0 0 14px;color:#334155;line-height:1.6">Please choose one of the options below so your department knows whether you are taking this on.</p>{{actionBlock}}</div><div style="margin:20px 0 0;',
           'Department</td><td style="padding:10px 14px;font-weight:600;color:#334155">{{departmentName}}</td></tr></tbody></table><div style="margin:20px 0;padding:16px 18px;background:#eff6ff;border:1px solid #bfdbfe;border-radius:8px"><p style="margin:0 0 12px;font-weight:700;color:#0f3d67">Your response is needed</p><p style="margin:0 0 14px;color:#334155;line-height:1.6">Please choose one of the options below so your department knows whether you are taking this on.</p>{{actionBlock}}</div><p style="margin:0 0 12px;color:#64748b;font-size:12px;line-height:1.6">The response link expires in 14 days and can be used once.</p><div style="margin:20px 0 0;'),
       revision = revision + 1
 WHERE template_code = 'VISIT_DEPARTMENT_STAFF_ASSIGNMENT'
   AND body_vi NOT LIKE '%hết hạn sau 14 ngày%';

-- VISIT_DEPARTMENT_LEADER_INVITATION — expiry note PLUS the assign-login note (this is the only
-- template with the "Gán nhân sự" button).
UPDATE email_templates
   SET body_vi = REPLACE(body_vi,
           'Vui lòng chọn một phương án bên dưới. Bạn cũng có thể gán trực tiếp một nhân sự của phòng ban để tiếp nhận công việc này.</p>{{actionBlock}}</div><div style="margin:20px 0 0;',
           'Vui lòng chọn một phương án bên dưới. Bạn cũng có thể gán trực tiếp một nhân sự của phòng ban để tiếp nhận công việc này.</p>{{actionBlock}}</div><p style="margin:0 0 12px;color:#64748b;font-size:12px;line-height:1.6">Liên kết phản hồi sẽ hết hạn sau 14 ngày và chỉ sử dụng được một lần. Thao tác Gán nhân sự yêu cầu đăng nhập hệ thống.</p><div style="margin:20px 0 0;'),
       body_en = REPLACE(body_en,
           'Please choose one of the options below. You can also assign a member of your department to take this on.</p>{{actionBlock}}</div><div style="margin:20px 0 0;',
           'Please choose one of the options below. You can also assign a member of your department to take this on.</p>{{actionBlock}}</div><p style="margin:0 0 12px;color:#64748b;font-size:12px;line-height:1.6">The response link expires in 14 days and can be used once. Assigning a staff member requires signing in to the system.</p><div style="margin:20px 0 0;'),
       revision = revision + 1
 WHERE template_code = 'VISIT_DEPARTMENT_LEADER_INVITATION'
   AND body_vi NOT LIKE '%hết hạn sau 14 ngày%';

-- LOGISTICS_ASSIGNEE_ASSIGNMENT — the real send is a 3-button block (Accept/Decline/Detail), so both
-- the login-required note (for the detail/propose-change button) and the expiry note are added.
UPDATE email_templates
   SET body_vi = REPLACE(body_vi,
           'Vui lòng chọn một phương án bên dưới để phòng ban biết bạn có nhận hạng mục này hay không.</p>{{actionBlock}}</div><div style="margin:20px 0 0;',
           'Vui lòng chọn một phương án bên dưới để phòng ban biết bạn có nhận hạng mục này hay không.</p>{{actionBlock}}</div><p style="margin:0 0 12px;color:#64748b;font-size:12px;line-height:1.6">Thao tác Xem chi tiết / Đề xuất thay đổi yêu cầu đăng nhập hệ thống. Liên kết phản hồi trực tiếp sẽ hết hạn sau 14 ngày và chỉ sử dụng được một lần.</p><div style="margin:20px 0 0;'),
       body_en = REPLACE(body_en,
           'Please choose one of the options below so your department knows whether you are taking this on.</p>{{actionBlock}}</div><div style="margin:20px 0 0;',
           'Please choose one of the options below so your department knows whether you are taking this on.</p>{{actionBlock}}</div><p style="margin:0 0 12px;color:#64748b;font-size:12px;line-height:1.6">Viewing details or proposing a change requires signing in to the system. The direct response link expires in 14 days and can be used once.</p><div style="margin:20px 0 0;'),
       revision = revision + 1
 WHERE template_code = 'LOGISTICS_ASSIGNEE_ASSIGNMENT'
   AND body_vi NOT LIKE '%hết hạn sau 14 ngày%';

-- LOGISTICS_REQUEST_TO_DEPARTMENT — LogisticsActionBlock's direct-vs-login-required explanation, plus
-- expiry.
UPDATE email_templates
   SET body_vi = REPLACE(body_vi,
           'Nếu cần điều chỉnh số lượng hoặc thời gian, hãy chọn thao tác trong hệ thống để gửi đề xuất thay đổi.</p>{{actionBlock}}</div><div style="margin:20px 0 0;',
           'Nếu cần điều chỉnh số lượng hoặc thời gian, hãy chọn thao tác trong hệ thống để gửi đề xuất thay đổi.</p>{{actionBlock}}</div><p style="margin:0 0 12px;color:#64748b;font-size:12px;line-height:1.6">Đồng ý / Từ chối là thao tác trực tiếp, không yêu cầu đăng nhập. Hành động khác (như gán nhân sự, thảo luận thêm) yêu cầu đăng nhập hệ thống. Liên kết phản hồi trực tiếp sẽ hết hạn sau 14 ngày và chỉ sử dụng được một lần.</p><div style="margin:20px 0 0;'),
       body_en = REPLACE(body_en,
           'If the quantity or the timing needs to change, use the in-system action to send a counter-proposal.</p>{{actionBlock}}</div><div style="margin:20px 0 0;',
           'If the quantity or the timing needs to change, use the in-system action to send a counter-proposal.</p>{{actionBlock}}</div><p style="margin:0 0 12px;color:#64748b;font-size:12px;line-height:1.6">Accept / Decline are direct actions and do not require signing in. Other action (such as assigning staff or further discussion) requires signing in to the system. The direct response link expires in 14 days and can be used once.</p><div style="margin:20px 0 0;'),
       revision = revision + 1
 WHERE template_code = 'LOGISTICS_REQUEST_TO_DEPARTMENT'
   AND body_vi NOT LIKE '%hết hạn sau 14 ngày%';

-- LOGISTICS_CHANGE_PROPOSAL_TO_HOST — DetailLinkBlock's login-required note (was a custom `note` arg,
-- now template content).
UPDATE email_templates
   SET body_vi = REPLACE(body_vi,
           'Vui lòng chọn một phương án bên dưới. Nếu bạn chấp nhận, hạng mục sẽ được cập nhật theo nội dung đề xuất.</p>{{actionBlock}}</div><div style="margin:20px 0 0;',
           'Vui lòng chọn một phương án bên dưới. Nếu bạn chấp nhận, hạng mục sẽ được cập nhật theo nội dung đề xuất.</p>{{actionBlock}}</div><p style="margin:0 0 12px;color:#64748b;font-size:12px;line-height:1.6">Đăng nhập để xem chi tiết đề xuất và quyết định Chấp nhận / Từ chối trong hệ thống.</p><div style="margin:20px 0 0;'),
       body_en = REPLACE(body_en,
           'Please choose one of the options below. If you accept, the item will be updated to match the proposal.</p>{{actionBlock}}</div><div style="margin:20px 0 0;',
           'Please choose one of the options below. If you accept, the item will be updated to match the proposal.</p>{{actionBlock}}</div><p style="margin:0 0 12px;color:#64748b;font-size:12px;line-height:1.6">Sign in to review the proposal in detail and decide to Accept / Reject it in the system.</p><div style="margin:20px 0 0;'),
       revision = revision + 1
 WHERE template_code = 'LOGISTICS_CHANGE_PROPOSAL_TO_HOST'
   AND body_vi NOT LIKE '%Đăng nhập để xem chi tiết đề xuất%';

-- LOGISTICS_EXPENSE_REPORT_REMINDER — DetailLinkBlock's login-required note (was a custom `note` arg).
UPDATE email_templates
   SET body_vi = REPLACE(body_vi,
           'Mở biên bản trong hệ thống để kê khai chi phí thực tế của hạng mục này.</p>{{actionBlock}}</div><div style="margin:20px 0 0;',
           'Mở biên bản trong hệ thống để kê khai chi phí thực tế của hạng mục này.</p>{{actionBlock}}</div><p style="margin:0 0 12px;color:#64748b;font-size:12px;line-height:1.6">Sau khi đăng nhập, bạn có thể nhập chi phí hoặc xác nhận "Không có chi phí" cho hạng mục này.</p><div style="margin:20px 0 0;'),
       body_en = REPLACE(body_en,
           'Open the record in the system and enter the actual expenses for this item.</p>{{actionBlock}}</div><div style="margin:20px 0 0;',
           'Open the record in the system and enter the actual expenses for this item.</p>{{actionBlock}}</div><p style="margin:0 0 12px;color:#64748b;font-size:12px;line-height:1.6">After signing in, you can enter the expenses or confirm "No cost" for this item.</p><div style="margin:20px 0 0;'),
       revision = revision + 1
 WHERE template_code = 'LOGISTICS_EXPENSE_REPORT_REMINDER'
   AND body_vi NOT LIKE '%Sau khi đăng nhập, bạn có thể nhập chi phí%';

-- VISIT_REMINDER_HOST — VisitDetailBlock's login-required note.
UPDATE email_templates
   SET body_vi = REPLACE(body_vi,
           'Mở chuyến tiếp khách trong hệ thống để kiểm tra và cập nhật phần còn thiếu.</p>{{actionBlock}}</div><div style="margin:20px 0 0;',
           'Mở chuyến tiếp khách trong hệ thống để kiểm tra và cập nhật phần còn thiếu.</p>{{actionBlock}}</div><p style="margin:0 0 12px;color:#64748b;font-size:12px;line-height:1.6">Liên kết yêu cầu đăng nhập hệ thống PEMS.</p><div style="margin:20px 0 0;'),
       body_en = REPLACE(body_en,
           'Open the visit in the system to review it and fill in whatever is still missing.</p>{{actionBlock}}</div><div style="margin:20px 0 0;',
           'Open the visit in the system to review it and fill in whatever is still missing.</p>{{actionBlock}}</div><p style="margin:0 0 12px;color:#64748b;font-size:12px;line-height:1.6">The link requires signing in to PEMS.</p><div style="margin:20px 0 0;'),
       revision = revision + 1
 WHERE template_code = 'VISIT_REMINDER_HOST'
   AND body_vi NOT LIKE '%Liên kết yêu cầu đăng nhập hệ thống PEMS%';

-- VISIT_REMINDER_PARTICIPANTS — same login-required note.
UPDATE email_templates
   SET body_vi = REPLACE(body_vi,
           'Mở chuyến tiếp khách trong hệ thống để xem lịch trình chi tiết và điểm tập trung.</p>{{actionBlock}}</div><div style="margin:20px 0 0;',
           'Mở chuyến tiếp khách trong hệ thống để xem lịch trình chi tiết và điểm tập trung.</p>{{actionBlock}}</div><p style="margin:0 0 12px;color:#64748b;font-size:12px;line-height:1.6">Liên kết yêu cầu đăng nhập hệ thống PEMS.</p><div style="margin:20px 0 0;'),
       body_en = REPLACE(body_en,
           'Open the visit in the system to see the detailed agenda and the meeting point.</p>{{actionBlock}}</div><div style="margin:20px 0 0;',
           'Open the visit in the system to see the detailed agenda and the meeting point.</p>{{actionBlock}}</div><p style="margin:0 0 12px;color:#64748b;font-size:12px;line-height:1.6">The link requires signing in to PEMS.</p><div style="margin:20px 0 0;'),
       revision = revision + 1
 WHERE template_code = 'VISIT_REMINDER_PARTICIPANTS'
   AND body_vi NOT LIKE '%Liên kết yêu cầu đăng nhập hệ thống PEMS%';

COMMIT;

-- ── Verification ─────────────────────────────────────────────────────────────
-- Expected for all 14 rows: action_block_vi = 1 and action_block_en = 1 (the placeholder/trusted block
-- survived). Expected new_text_vi = 1 for every row except ACCOUNT_ACTIVATED/ACCOUNT_EMAIL_CONFIRMATION,
-- which are not part of this patch (their templates needed no body edit — see header).
SELECT
    template_code,
    revision,
    body_vi LIKE '%{{actionBlock}}%' AS action_block_vi,
    body_en LIKE '%{{actionBlock}}%' AS action_block_en,
    (body_vi LIKE '%hết hạn sau 14 ngày%'
      OR body_vi LIKE '%{{contactExpiresAt}}%'
      OR body_vi LIKE '%Đăng nhập để xem chi tiết đề xuất%'
      OR body_vi LIKE '%Sau khi đăng nhập, bạn có thể nhập chi phí%'
      OR body_vi LIKE '%Liên kết yêu cầu đăng nhập hệ thống PEMS%') AS new_text_vi
FROM email_templates
WHERE template_code IN (
    'VISIT_CONTACT_CLAIM', 'VISIT_CONTACT_TRANSFER',
    'VISIT_PARTICIPANT_INVITATION', 'VISIT_STUDENT_INVITATION',
    'VISIT_DEPARTMENT_STAFF_ASSIGNMENT', 'VISIT_DEPARTMENT_LEADER_INVITATION',
    'LOGISTICS_ASSIGNEE_ASSIGNMENT', 'LOGISTICS_REQUEST_TO_DEPARTMENT',
    'LOGISTICS_CHANGE_PROPOSAL_TO_HOST', 'LOGISTICS_EXPENSE_REPORT_REMINDER',
    'VISIT_REMINDER_HOST', 'VISIT_REMINDER_PARTICIPANTS'
);
