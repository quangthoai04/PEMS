# 01 — Audit hai chiều: caller ↔ template (Giai đoạn 1)

> Nguồn yêu cầu: `docs/Ver2Carnh/canh/email/PEMS_EMAIL_TEMPLATE_CC_BCC_IMPLEMENTATION_PLAN.md` Mục 10.
> Đo trên HEAD `06c73b94` (nhánh `Canh-Iter1`), ngày 2026-07-26.
> Mọi dòng dưới đây được xác lập **bằng code tại HEAD**, không lấy lại từ báo cáo audit cũ.

---

## 0. Kết luận sớm — audit cũ sai ở đâu

Kế hoạch Mục 10.6 liệt kê 27 mã template làm "baseline cần đối chiếu" và tự ghi rõ đó là *giả thuyết*. Đối chiếu với seed thật cho kết quả: **20 trong 27 mã đó KHÔNG TỒN TẠI** trong database.

| Nhóm trong kế hoạch | Mã trong kế hoạch | Thực tế trong seed |
|---|---|---|
| Account và bảo mật | 9 mã (`ACCOUNT_EMAIL_CONFIRMATION`, `ACCOUNT_ACTIVATED`, `AUTH_PASSWORD_RESET_OTP`, …) | **0/9 tồn tại** |
| Visit request/đầu mối | `VISIT_REQUEST_OTP`, `VISIT_CONTACT_CLAIM`, `VISIT_CONTACT_TRANSFER` | **0/3 tồn tại** (seed có `OTP_VISIT_REQUEST` — mã khác, không caller) |
| Thành phần tham gia | 4 mã | **3/4 tồn tại**; `VISIT_DEPARTMENT_STAFF_ASSIGNMENT` không tồn tại |
| Nhắc lịch | `VISIT_REMINDER_HOST`, `VISIT_REMINDER_PARTICIPANTS` | **2/2 tồn tại** |
| Hậu cần | 4 mã | **2/4 tồn tại**; `LOGISTICS_CHANGE_PROPOSAL_TO_HOST` và `LOGISTICS_EXPENSE_REPORT_REMINDER` không tồn tại |
| Báo cáo | 5 mã `REPORT_*` | **0/5 tồn tại** |

Hệ quả cho các giai đoạn sau: **Giai đoạn 4 phần lớn là "tạo mới template + chuyển caller sang dùng nó", không phải "sửa template có sẵn"**. Và Giai đoạn 7 phải viết seed từ đầu chứ không chỉ dọn.

---

## 1. Chiều Seed → Caller: phân loại 16 template có trong SQL canonical

Seed nằm ở 2 khối `INSERT` (dòng 5393, 5403 của canonical SQL), `email_template_id` cứng 1..16, **tất cả `status='ACTIVE'`**.

| ID | `template_code` | `purpose` | Production caller? | Phân loại | Xử lý đề xuất |
|---|---|---|---|---|---|
| 1 | `ACCOUNT_CREATED_INTERNAL` | ACCOUNT | ❌ không | `DEAD_CODE_NO_CALLER` | Không seed active. Nội dung "đăng nhập ngay" **mâu thuẫn** luồng P0 (tài khoản mới ở `PENDING_EMAIL_CONFIRMATION`, chưa được login) |
| 2 | `VISIT_REQUEST_APPROVED` | VISIT_REQUEST | ❌ không | `NOTIFICATION_ONLY` | Duyệt đơn hiện chỉ tạo in-app notification. **Cấm** tự thêm caller (D-05) |
| 3 | `VISIT_REQUEST_REJECTED` | VISIT_REQUEST | ❌ không | `NOTIFICATION_ONLY` | như trên |
| 4 | `VISIT_CANCELLED` | CANCELLATION | ❌ không | `NOTIFICATION_ONLY` | như trên |
| 5 | `HOST_ASSIGNMENT` | HOST_ASSIGNMENT | ❌ không | `NOTIFICATION_ONLY` | như trên |
| 6 | `LOGISTICS_REQUEST` | LOGISTICS | ❌ không | `DEAD_CODE_NO_CALLER` | Trùng nghĩa với #11 `LOGISTICS_REQUEST_TO_DEPARTMENT` (mã cũ) |
| 7 | `OTP_VISIT_REQUEST` | OTP | ❌ không | `DEAD_CODE_NO_CALLER` | OTP visit hiện hard-code trong `EmailService`. Là **ứng viên mã đích** cho Batch 3 (xem Mục 4) |
| 8 | `VISIT_PARTICIPANT_INVITATION` | VISIT_PARTICIPANT | ⚠️ **một phần** | `ACTIVE_AUTOMATED_TEMPLATE` | Caller lưu `email_template_id` nhưng **body lấy từ code** trừ khi host sửa qua preview |
| 9 | `VISIT_DEPARTMENT_LEADER_INVITATION` | VISIT_PARTICIPANT | ⚠️ **một phần** | `ACTIVE_AUTOMATED_TEMPLATE` | như trên |
| 10 | `VISIT_STUDENT_INVITATION` | VISIT_PARTICIPANT | ⚠️ **một phần** | `ACTIVE_AUTOMATED_TEMPLATE` | như trên |
| 11 | `LOGISTICS_REQUEST_TO_DEPARTMENT` | LOGISTICS | ✅ **có, dùng thật** | `ACTIVE_AUTOMATED_TEMPLATE` | Caller **thực sự render** `subject_vi`/`body_vi` — nhưng vẫn có fallback cứng |
| 12 | `LOGISTICS_ASSIGNEE_ASSIGNMENT` | LOGISTICS | ⚠️ **một phần** | `ACTIVE_AUTOMATED_TEMPLATE` | chỉ lấy `email_template_id`; body từ code |
| 13 | `VISIT_REMINDER_HOST` | VISIT_REMINDER | ✅ **có, dùng thật** | `ACTIVE_AUTOMATED_TEMPLATE` | Hosted service render `SubjectVi`/`BodyVi`, có fallback cứng |
| 14 | `VISIT_REMINDER_PARTICIPANTS` | VISIT_REMINDER | ✅ **có, dùng thật** | `ACTIVE_AUTOMATED_TEMPLATE` | như trên |
| 15 | `VISIT_REQUEST_SUBMITTED_NOTIFY` | VISIT_REQUEST | ❌ không | `NOTIFICATION_ONLY` | **Cấm** tự thêm caller |
| 16 | `LOGISTICS_REQUEST_SUBMITTED_NOTIFY` | LOGISTICS | ❌ không | `DEAD_CODE_NO_CALLER` | |

**Tổng kết chiều seed:** 16 active · **6 có caller** (3 dùng thật, 3 chỉ lưu ID) · **5 dead code** · **5 notification-only**. **Không còn mục nào "unknown".**

### 1.1. `purpose` thực tế đang dùng trong seed

`ACCOUNT`, `VISIT_REQUEST`, `CANCELLATION`, `HOST_ASSIGNMENT`, `LOGISTICS`, `OTP`, `VISIT_PARTICIPANT`, `VISIT_REMINDER` — **8 giá trị**.

### 1.2. Seed phụ thuộc numeric ID

| Phụ thuộc | Vị trí | Ghi chú |
|---|---|---|
| `email_templates` UPDATE theo `email_template_id = N` | 16 khối, dòng 9752 → 10097 | Phải bỏ (Giai đoạn 7) |
| `sent_emails.email_template_id` | 4 khối INSERT (5414, 8477, 9571, 9688) | Thay bằng lookup theo `template_code`, hoặc `NULL` cho email lịch sử/manual |
| Patch `CASE template_code` | dòng 11459 | Chứa mã **không tồn tại**: `VISIT_INVITATION`, `NEWS_REVIEW`. Chỉ `VISIT_CANCELLED` và `LOGISTICS_REQUEST` khớp. Phải bỏ |
| `email_drafts` | **không có seed** | Không phải xử lý |

---

## 2. Chiều Caller → Template: ma trận đầy đủ

Ma trận được chia làm 2 bảng dùng chung cột **ID** để giữ đủ 19 trường mà vẫn đọc được. Điểm gửi được liệt kê là **mọi** lời gọi `IEmailService` tìm thấy tại HEAD (30 điểm, 27 file).

### 2.A — Định danh và điều kiện gửi

| ID | Group | Trigger | Caller (class:line) | File | Condition | Mandatory |
|---|---|---|---|---|---|---|
| C-01 | Account | Tạo tài khoản nội bộ | `CreateAccountCommandHandler:451` | `Accounts/Commands/CreateAccount/` | Luôn, sau khi user commit ở `PENDING_EMAIL_CONFIRMATION` | Best-effort (bọc try/catch, trả `SENT`/`SKIPPED`/`FAILED`) |
| C-02 | Account | Gửi lại email xác nhận | `ResendAccountEmailConfirmationCommandHandler:73` | `Accounts/Commands/ResendAccountEmailConfirmation/` | User còn pending | Best-effort |
| C-03 | Account | Sửa email đang chờ xác nhận — gửi email MỚI | `EditPendingAccountEmailCommandHandler:70` | `Accounts/Commands/EditPendingAccountEmail/` | Luôn | Best-effort |
| C-04 | Account | Sửa email đang chờ — báo email CŨ | `EditPendingAccountEmailCommandHandler:78` | như trên | Chỉ khi email cũ khác email mới | Best-effort |
| C-05 | Account | Xác nhận email thành công | `ConfirmAccountEmailCommandHandler:139` | `Accounts/Commands/ConfirmAccountEmail/` | Sau khi token hợp lệ | Best-effort |
| C-06 | Account | Đổi email tài khoản đã kích hoạt | `UpdateBasicAccountInfoCommandHandler:226` | `Accounts/Commands/UpdateBasicAccountInfo/` | Khi email thay đổi | Best-effort |
| C-07 | Account | Đổi vai trò | `UpdateAccountRoleCommandHandler:302` | `Accounts/Commands/UpdateAccountRole/` | Khi role thay đổi | Best-effort |
| C-08 | Account | Thay Staff Leader — xác nhận email người mới | `ReplaceStaffLeaderCommandHandler:292` | `Accounts/Commands/ReplaceStaffLeader/` | Khi tạo tài khoản leader mới | Best-effort |
| C-09 | Account | Thay Staff Leader — thông báo (2 điểm) | `ReplaceStaffLeaderCommandHandler:322, :350` | như trên | Theo nhánh gán/thu hồi | Best-effort |
| C-10 | Account | Thêm nhân sự phòng ban | `AddDepartmentPersonnelCommandHandler:149` | `Departments/Commands/AddDepartmentPersonnel/` | Khi tạo user mới | Best-effort |
| C-11 | Auth | Quên mật khẩu | `ForgotPasswordCommandHandler:55` | `Authentication/Commands/ForgotPassword/` | Email tồn tại (chống enumeration ở tầng response) | Best-effort |
| C-12 | Visit | OTP đăng ký tham quan (công khai) | `InitiateVisitRequestV2CommandHandler:107` | `Delegations/Commands/InitiateVisitRequestV2/` | Mỗi lần phát OTP | Best-effort |
| C-13 | Visit | Gửi lại OTP | `ResendVisitRequestOtpCommandHandler:43` | `Delegations/Commands/ResendVisitRequestOtp/` | Trong hạn mức resend | Best-effort |
| C-14 | Visit | Khôi phục OTP | `RecoverVisitRequestOtpCommandHandler:63` | `Delegations/Commands/RecoverVisitRequestOtp/` | Qua Turnstile | Best-effort |
| C-15 | Visit | Xác nhận vai trò đầu mối (claim) | `VisitContactClaimService:98` | `Infrastructure/Services/` | Khi tạo claim | Best-effort |
| C-16 | Visit | Mời nhận vai trò đầu mối (transfer) | `VisitContactClaimService:170` | như trên | Khi tạo transfer | Best-effort |
| C-17 | Participant | Mời IC/Student tham gia | `InviteVisitParticipantCommandHandler:294` | `Delegations/Commands/InviteVisitParticipant/` | Host mời từng người | Best-effort (đã persist participant trước) |
| C-18 | Participant | Gán nhân sự phòng ban | `AssignDepartmentStaffCommandHandler:240` | `Delegations/Commands/AssignDepartmentStaff/` | Dept Leader gán | Best-effort |
| C-19 | Logistics | Gửi yêu cầu hậu cần tới phòng ban | `PrepareVisitLogisticsCommandHandler:357` | `Delegations/Commands/PrepareVisitLogistics/` | Khi coordination_mode = email | Best-effort |
| C-20 | Logistics | Phân công người xử lý | `AssignRequestAssigneeCommand:243` | `DepartmentReceptionTasks/Commands/AssignRequestAssignee/` | Khi gán assignee | Best-effort |
| C-21 | Logistics | Đề xuất thay đổi gửi Host | `ProposeRequestChangeCommand:201` | `DepartmentReceptionTasks/Commands/ProposeRequestChange/` | Khi phòng ban đề xuất | Best-effort |
| C-22 | Logistics | Nhắc kê khai chi phí | `RemindExpenseReportsCommandHandler:188` | `Delegations/VisitExpenses/Commands/RemindExpenseReports/` | Job/lệnh nhắc | Best-effort |
| C-23 | Reminder | Nhắc lịch Host + Participants | `VisitReminderDispatchHostedService:~180` | `Infrastructure/BackgroundJobs/` | Theo `visit_instance_reminder_settings` | Best-effort, có chống trùng |
| C-24 | Report | Báo cáo vận hành campus (HO) | `SendHoCampusReportCommand:102` | `Reports/Commands/SendHoCampusReport/` | HO bấm gửi | Mandatory (lỗi ném ra caller) |
| C-25 | Report | Báo cáo phối hợp phòng ban | `SendStaffLeaderDepartmentReportCommand:183` | `Reports/Commands/SendStaffLeaderDepartmentReport/` | Staff Leader bấm gửi | Mandatory |
| C-26 | Report | Hoá đơn hậu cần (Staff Leader gửi) | `SendStaffLeaderDeptInvoiceCommand:130` | `Reports/Commands/SendStaffLeaderDeptInvoice/` | Staff Leader bấm gửi | Mandatory |
| C-27 | Report | Hiệu suất nhân sự (Staff Leader gửi) | `SendStaffLeaderPersonnelReportCommand:160` | `Reports/Commands/SendStaffLeaderPersonnelReport/` | Staff Leader bấm gửi | Mandatory |
| C-28 | Report | Hiệu suất nhân sự (Dept Leader gửi) | `SendDeptLeaderPersonnelReportCommand:158` | `Reports/Commands/SendDeptLeaderPersonnelReport/` | Dept Leader bấm gửi | Mandatory |
| C-29 | Report | Hoá đơn gửi lên Staff Leader | `SendDeptLeaderInvoiceToStaffLeaderCommand:128` | `Reports/Commands/SendDeptLeaderInvoiceToStaffLeader/` | Dept Leader bấm gửi | Mandatory |
| C-30 | Manual | Gửi email soạn tay | `SendEmailCommandHandler:71` | `Emails/Commands/SendEmail/` | User bấm gửi | Mandatory |
| C-31 | Manual | Gửi email từ nháp | `SendEmailDraftCommandHandler:148` | `Emails/Commands/SendEmailDraft/` | User bấm gửi | Mandatory |
| C-32 | Manual | Phản hồi email | `ReplytoEmailCommandHandler:94` | `Emails/Commands/ReplytoEmail/` | User bấm phản hồi | Mandatory |
| C-33 | Preview | Xem trước template | `PreviewEmailTemplateQueryHandler` | `Emails/Queries/PreviewEmailTemplate/` | Không gửi mail | — |

### 2.B — Nội dung, người nhận và nghiệm thu

| ID | To | Cc | Bcc | Nguồn nội dung hiện tại | Template code hiện tại | **Template code đích** | Variables | Lang | Nhạy cảm | Attach | Ghi `sent_emails` | Phân loại |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| C-01 | user mới (1) | — | — | **CODE** `AccountConfirmationEmail` | (không) | `ACCOUNT_EMAIL_CONFIRMATION` | fullName, roleName, campusName, expiresInHours | VI | ✅ token xác nhận | ❌ | ❌ | `ACTIVE_AUTOMATED_TEMPLATE` |
| C-02 | user pending (1) | — | — | **CODE** | (không) | `ACCOUNT_EMAIL_CONFIRMATION` | như C-01 | VI | ✅ token | ❌ | ❌ | như trên |
| C-03 | email mới (1) | — | — | **CODE** | (không) | `ACCOUNT_EMAIL_CONFIRMATION` | như C-01 | VI | ✅ token | ❌ | ❌ | như trên |
| C-04 | email cũ (1) | — | — | **CODE** `EmailChangedNoticeSubject` | (không) | `ACCOUNT_PENDING_EMAIL_CHANGED_OLD_NOTICE` | fullName, newEmailMasked | VI | ⚠️ không token | ❌ | ❌ | như trên |
| C-05 | user (1) | — | — | **CODE** | (không) | `ACCOUNT_ACTIVATED` | fullName, loginUrl | VI | ❌ | ❌ | ❌ | như trên |
| C-06 | email cũ + mới | — | — | **CODE** | (không) | `ACCOUNT_EMAIL_CHANGED_OLD_NOTICE` / `..._NEW_NOTICE` | fullName, oldEmailMasked, newEmailMasked | VI | ⚠️ | ❌ | ❌ | như trên |
| C-07 | user (1) | — | — | **CODE** | (không) | `ACCOUNT_ROLE_CHANGED` | fullName, oldRoleName, newRoleName | VI | ❌ | ❌ | ❌ | như trên |
| C-08 | leader mới (1) | — | — | **CODE** | (không) | `ACCOUNT_EMAIL_CONFIRMATION` | như C-01 | VI | ✅ token | ❌ | ❌ | như trên |
| C-09 | leader cũ/mới (1) | — | — | **CODE** | (không) | `ACCOUNT_STAFF_LEADER_ASSIGNED` / `..._REPLACED` | fullName, campusName, effectiveDate | VI | ❌ | ❌ | ❌ | như trên |
| C-10 | nhân sự mới (1) | — | — | **CODE** | (không) | `ACCOUNT_EMAIL_CONFIRMATION` | như C-01 | VI | ✅ token | ❌ | ❌ | như trên |
| C-11 | user (1) | — | — | **CODE** `EmailService.SendPasswordResetAsync` | (không) | `AUTH_PASSWORD_RESET_OTP` | fullName, otpCode, expireMinutes | VI | ✅ **OTP** | ❌ | ❌ | như trên |
| C-12 | người đăng ký (1) | — | — | **CODE** `EmailService.SendVisitRequestOtpAsync` | (không) | `VISIT_REQUEST_OTP` | fullName, otpCode, expireMinutes | VI | ✅ **OTP** | ❌ | ❌ | như trên |
| C-13 | như C-12 | — | — | **CODE** (cùng hàm) | (không) | `VISIT_REQUEST_OTP` | như trên | VI | ✅ **OTP** | ❌ | ❌ | như trên |
| C-14 | như C-12 | — | — | **CODE** (cùng hàm) | (không) | `VISIT_REQUEST_OTP` | như trên | VI | ✅ **OTP** | ❌ | ❌ | như trên |
| C-15 | đầu mối (1) | — | — | **CODE** | (không) | `VISIT_CONTACT_CLAIM` | requestCode, contactName, delegationName | VI | ✅ action URL riêng | ❌ | ❌ | như trên |
| C-16 | đầu mối mới (1) | — | — | **CODE** | (không) | `VISIT_CONTACT_TRANSFER` | như trên | VI | ✅ action URL riêng | ❌ | ❌ | như trên |
| C-17 | người được mời (1) | — | — | **DB một phần**: lấy `email_template_id`, nhưng body từ `ParticipantInvitationEmailBuilder` trừ khi host sửa | `VISIT_PARTICIPANT_INVITATION` / `VISIT_STUDENT_INVITATION` | giữ nguyên | recipientName, delegationName, campusName, plannedTime, hostName, roleLabel | VI | ✅ accept/decline token | ✅ | ✅ | như trên |
| C-18 | staff được gán (1) | — | — | **DB một phần**: lấy ID của `VISIT_PARTICIPANT_INVITATION`; body từ `DefaultContentHtml` | `VISIT_PARTICIPANT_INVITATION` (**mượn sai mã**) | `VISIT_DEPARTMENT_STAFF_ASSIGNMENT` (mã mới) | recipientName, delegationName, campusName, plannedTime | VI | ✅ token | ✅ | ✅ | như trên |
| C-19 | trưởng phòng ban (1) | — | — | **DB thật** (`RenderTemplate(template.SubjectVi/BodyVi)`) + fallback cứng | `LOGISTICS_REQUEST_TO_DEPARTMENT` | giữ nguyên | departmentLeaderName, requesterName, logisticsTitle, quantity, usageStartAt, usageEndAt, dueAt, coordinationNote | VI | ✅ accept/decline/detail token | ✅ | ✅ | như trên |
| C-20 | assignee (1) | — | — | **DB một phần**: chỉ lấy ID; body từ code | `LOGISTICS_ASSIGNEE_ASSIGNMENT` | giữ nguyên | assigneeName, logisticsTitle, dueAt, campusName | VI | ✅ token | ✅ | ✅ | như trên |
| C-21 | host (1) | — | — | **CODE** (`BrandedShell` + block) | (không) | `LOGISTICS_CHANGE_PROPOSAL_TO_HOST` | hostName, logisticsTitle, proposalNote | VI | ✅ approve/reject token | ✅ | ✅ | như trên |
| C-22 | người phải kê khai (1) | — | — | **CODE** (`BrandedShell`) | (không) | `LOGISTICS_EXPENSE_REPORT_REMINDER` | recipientName, itemTitle, dueAt | VI | ❌ | ❌ | ✅ | như trên |
| C-23 | host / từng participant | — | — | **DB thật** + fallback cứng | `VISIT_REMINDER_HOST` / `VISIT_REMINDER_PARTICIPANTS` | giữ nguyên | hostName / recipientName, delegationName, campusName, plannedStart, detailUrl | VI | ❌ | ❌ | ✅ | như trên |
| C-24 | staff leader campus (1) | — | — | **CODE** | (không) | `REPORT_CAMPUS_OPERATION` | recipientName, campusName, periodFrom, periodTo | VI | ❌ | ✅ PDF | ❌ | như trên |
| C-25 | trưởng phòng ban (1..n, vòng lặp) | — | — | **CODE** | (không) | `REPORT_DEPARTMENT_COLLABORATION` | recipientName, departmentName, periodFrom, periodTo | VI | ❌ | ✅ PDF | ❌ | như trên |
| C-26 | người nhận cấu hình (1) | — | — | **CODE** | (không) | `REPORT_DEPARTMENT_INVOICE` | recipientName, departmentName, periodFrom, periodTo | VI | ❌ | ✅ PDF | ❌ | như trên |
| C-27 | từng nhân sự (1) | — | — | **CODE** | (không) | `REPORT_PERSONNEL_PERFORMANCE` | personName, periodFrom, periodTo, scopeLabel | VI | ❌ | ✅ PDF | ❌ | như trên |
| C-28 | từng nhân sự (1) | — | — | **CODE** | (không) | `REPORT_PERSONNEL_PERFORMANCE` (**dùng chung C-27**) | như C-27 | VI | ❌ | ✅ PDF | ❌ | như trên |
| C-29 | staff leader (1) | — | — | **CODE** | (không) | `REPORT_DEPARTMENT_INVOICE` (**dùng chung C-26**) | như C-26 | VI | ❌ | ✅ PDF | ❌ | như trên |
| C-30 | n người, **ép hết thành TO** | ❌ mất | ❌ mất | **USER-AUTHORED** | tuỳ chọn | — | — | — | ❌ | ❌ | ✅ | `USER_AUTHORED` |
| C-31 | theo draft (TO/CC/BCC lưu đúng) | ⚠️ lưu nhưng gửi sai | ⚠️ lưu nhưng gửi sai | **USER-AUTHORED** | tuỳ chọn | — | — | — | ❌ | ✅ | ✅ | `USER_AUTHORED` |
| C-32 | người gửi gốc (1) | ⚠️ **lưu DB, KHÔNG gửi** | ⚠️ **lưu DB, KHÔNG gửi** | **USER-AUTHORED** | — | — | — | — | ❌ | ❌ | ✅ | `USER_AUTHORED` |
| C-33 | — | — | — | **DB** (`RenderTemplate`) | mọi mã | — | — | VI/EN | ❌ (dùng action giả) | — | — | `USER_AUTHORED` (hỗ trợ soạn) |

**Không có dòng nào ở trạng thái "unknown".** Điểm gửi dùng bởi test: `FileSinkEmailService` (`TEST_ONLY`) và `EmailServiceDeliveryOutcomeTests` / `EmailServiceSensitiveLoggingTests`.

---

## 3. Giải quyết sai lệch bắt buộc (kế hoạch Mục 10.7)

**Phát biểu cần giải quyết:** "audit cũ nói có 6 luồng gửi report/invoice nhưng baseline chỉ có 5 template report."

**Kết luận: audit cũ đếm ĐÚNG số caller (6), nhưng danh sách 5 template là ĐỀ XUẤT chưa từng tồn tại trong seed.**

Bằng chứng — 6 caller thật, tìm bằng `SentAsync` trong `PEMS.Application/Reports/Commands/`:

| # | Command | Route/trigger | Recipient | Attachment |
|---|---|---|---|---|
| 1 | `SendHoCampusReportCommand:102` | HO gửi báo cáo vận hành campus | Staff Leader của campus | PDF `PEMS_BaoCao_HeThong_*` |
| 2 | `SendStaffLeaderDepartmentReportCommand:183` | Staff Leader gửi báo cáo phối hợp | Trưởng từng phòng ban (vòng lặp) | PDF |
| 3 | `SendStaffLeaderDeptInvoiceCommand:130` | Staff Leader gửi hoá đơn hậu cần | Người nhận cấu hình | PDF `PEMS_Department_Invoice_*` |
| 4 | `SendStaffLeaderPersonnelReportCommand:160` | Staff Leader gửi báo cáo hiệu suất | Từng nhân sự | PDF |
| 5 | `SendDeptLeaderPersonnelReportCommand:158` | Dept Leader gửi báo cáo hiệu suất | Từng nhân sự | PDF |
| 6 | `SendDeptLeaderInvoiceToStaffLeaderCommand:128` | Dept Leader gửi hoá đơn lên trên | Staff Leader | PDF |

Trong seed canonical: **0 template nào có `purpose='REPORT'` hoặc mã bắt đầu `REPORT_`**. Cả 6 caller đều hard-code subject/body.

**Quyết định catalog đích: 4 template report (không phải 5, không phải 6).** Lý do gộp:

- #4 và #5 gửi **cùng nội dung nghiệp vụ** (báo cáo hiệu suất một cá nhân) chỉ khác người bấm gửi và scope dữ liệu. Chênh lệch duy nhất là chuỗi mô tả scope ("tham gia tiếp khách" vs "phụ trách đoàn khách") — đưa vào biến `scopeLabel`. → dùng chung `REPORT_PERSONNEL_PERFORMANCE`.
- #3 và #6 cùng là **hoá đơn hậu cần phòng ban**, subject hiện tại **giống hệt nhau từng ký tự** (`[PEMS] Hóa đơn hậu cần tiếp khách — {dept} ({from} – {to})`). → dùng chung `REPORT_DEPARTMENT_INVOICE`.

Catalog report đích: `REPORT_CAMPUS_OPERATION`, `REPORT_DEPARTMENT_COLLABORATION`, `REPORT_DEPARTMENT_INVOICE`, `REPORT_PERSONNEL_PERFORMANCE`.

**Sai lệch đã đóng.** Được phép viết final catalog ở Giai đoạn 2.

---

## 4. Hai nội dung nghi dead code (kế hoạch Mục 10.8)

| Nội dung | Hàm | Có production caller? | Kết luận |
|---|---|---|---|
| "Xác nhận đã gửi đơn cho primary contact" | `IEmailService.SendVisitorAccountCreatedOrLinkedEmailAsync` (`EmailService.cs:252`) | **KHÔNG** — không lời gọi nào trong `backend/` ngoài chính interface và `FileSinkEmailService` | `DEAD_CODE_NO_CALLER` |
| "Xác nhận đã gửi đơn cho registrant khi khác primary contact" | `IEmailService.SendRegistrantConfirmationAsync` (`EmailService.cs:317`) | **KHÔNG** | `DEAD_CODE_NO_CALLER` |

Xử lý theo kế hoạch: **không seed active, không tự thêm caller.** Việc xoá code chỉ làm khi chứng minh không còn reference hợp lệ — hiện `FileSinkEmailService` vẫn implement 2 hàm này để thoả interface, nên xoá phải xoá đồng bộ ở cả `IEmailService`, `EmailService`, `FileSinkEmailService`. Đưa vào Giai đoạn 3 (khi tái cấu trúc `IEmailService`).

---

## 5. Khiếm khuyết kỹ thuật đã xác lập bằng code

Đây là danh sách "phải sửa", mỗi mục kèm chứng cứ. Không mục nào là suy đoán.

| # | Khiếm khuyết | Chứng cứ | Vi phạm quyết định |
|---|---|---|---|
| D-1 | `OutboundEmail` chỉ có `ToEmail` — **không có trường CC/BCC** | `Common/Interfaces/IEmailService.cs:12` | Mục 11.8 |
| D-2 | Reply **lưu CC/BCC vào DB nhưng chỉ gửi cho `toEmail`** | `ReplytoEmailCommandHandler.cs:74-83` (lưu) vs `:94` (gửi) | Mục 14.3 |
| D-3 | `ViewEmail` **không kiểm tra quyền** và trả **toàn bộ recipient kể cả BCC** cho mọi user đăng nhập | `ViewEmailQueryHandler.cs:41-42` (comment tự nhận đã bỏ filter), `:77-87` | D-07, Mục 11.12 |
| D-4 | Compose ép mọi recipient thành `TO`; gửi **N MIME riêng** thay vì 1 | `SendEmailCommandHandler.cs:55`, `:66-81` | Mục 14.2 |
| D-5 | `DeliveryStatus="DELIVERED"` **ngay sau SMTP accept** | `SendEmailCommandHandler.cs:72`, `SendEmailDraftCommandHandler.cs:156` | **D-08** |
| D-6 | Draft gửi **1 MIME/recipient**, CC/BCC nhận email ở vị trí `To` | `SendEmailDraftCommandHandler.cs:143-165` | Mục 14.2 |
| D-7 | `EmailTemplateRenderer.cs` là **class rỗng** (`namespace PEMS.Shared; public class EmailTemplateRenderer {}`) | `Infrastructure/Email/EmailTemplateRenderer.cs` | D-03 |
| D-8 | `SmtpEmailSender.cs` cũng là **class rỗng** | `Infrastructure/Email/SmtpEmailSender.cs` | — (rác) |
| D-9 | Renderer thật (`EmailComposition.RenderTemplate`) **fallback im lặng** `"Chưa có thông tin"` cho mọi biến thiếu | `EmailComposition.cs:315-319` | D-01, Mục 11.3 |
| D-10 | Renderer **không HTML-encode** giá trị biến | `EmailComposition.cs:286-292` trả `kvp.Value` thô | Mục 11.6 |
| D-11 | Renderer **không validate** tập biến khai báo, **không phát hiện placeholder còn sót** | không có logic tương ứng | Mục 11.3 |
| D-12 | Subject **không chặn CR/LF** ở bất kỳ đâu | không có logic | Mục 11.9, header injection |
| D-13 | `AllowedPurposes` của email template lấy từ **enum OTP** (`OtpPurpose.VisitRequestVerify`, `OtpPurpose.ChangeSensitiveAction`) → API tạo/sửa template **từ chối mọi purpose thật** | `CreateEmailTemplateCommandValidator.cs:9-13`; mirrored ở `UpdateEmailTemplateCommandValidator.cs` | **Mục 4.4**, 11.13 |
| D-14 | Không validate email format / duplicate cùng nhóm / duplicate chéo nhóm / giới hạn tổng recipient | `EmailDraftWriter.NormalizeRecipientType:32-38` chỉ kiểm tra 3 chuỗi TO/CC/BCC | Mục 11.9 |
| D-15 | Frontend compose **không có UI CC/BCC**; hard-code `recipientType: 'TO'` | `EmailComposeModal.tsx:150` | Mục 15.2 |
| D-16 | `useLocalEmailDraft` có field `cc`/`bcc` nhưng **không nối vào API** | `useLocalEmailDraft.ts:13-14` | Mục 15.2 |
| D-17 | Placeholder trộn PascalCase (`{{FullName}}`, `{{RequestCode}}`) và camelCase (`{{recipientName}}`, `{{logisticsTitle}}`) trong cùng seed | seed template 1-7 vs 8-16 | Mục 11.5 |
| D-18 | Preview strip "coordination note" bằng **regex vá lỗi template cũ**, và còn tham chiếu mã đã đổi tên `VISIT_STUDENT_SUPPORT_INVITATION` | `PreviewEmailTemplateQueryHandler.cs:56-62` | Mục 13.6 |
| D-19 | Patch SQL dòng 11459 update `CASE template_code` cho mã **không tồn tại** (`VISIT_INVITATION`, `NEWS_REVIEW`) | canonical SQL | Mục 16.2 |
| D-20 | Caller báo cáo (C-24..C-29) `SendAsync` **không bọc try/catch** → lỗi SMTP làm hỏng cả lệnh gửi báo cáo | các file `Reports/Commands/Send*` | cần chốt mandatory/best-effort ở Mục 11 |

---

## 6. Danh mục template mục tiêu (đề xuất, chốt ở Giai đoạn 2)

**26 template**, tất cả đều có production caller thật. Đối chiếu với 16 seed hiện tại: **giữ 7 · bỏ 9 khỏi fresh seed · tạo mới 19**.

| # | Code | Purpose | Caller | CC/BCC | Gửi riêng |
|---|---|---|---|---|---|
| 1 | `ACCOUNT_EMAIL_CONFIRMATION` | ACCOUNT | C-01, C-02, C-03, C-08, C-10 | Cấm | Bắt buộc |
| 2 | `ACCOUNT_PENDING_EMAIL_CHANGED_OLD_NOTICE` | ACCOUNT | C-04 | Cấm | Bắt buộc |
| 3 | `ACCOUNT_ACTIVATED` | ACCOUNT | C-05 | Cấm | Bắt buộc |
| 4 | `ACCOUNT_EMAIL_CHANGED_OLD_NOTICE` | ACCOUNT | C-06 | Cấm | Bắt buộc |
| 5 | `ACCOUNT_EMAIL_CHANGED_NEW_NOTICE` | ACCOUNT | C-06 | Cấm | Bắt buộc |
| 6 | `ACCOUNT_ROLE_CHANGED` | ACCOUNT | C-07 | Cấm | Bắt buộc |
| 7 | `ACCOUNT_STAFF_LEADER_ASSIGNED` | ACCOUNT | C-09 | Cấm | Bắt buộc |
| 8 | `ACCOUNT_STAFF_LEADER_REPLACED` | ACCOUNT | C-09 | Cấm | Bắt buộc |
| 9 | `AUTH_PASSWORD_RESET_OTP` | AUTH | C-11 | Cấm | Bắt buộc |
| 10 | `VISIT_REQUEST_OTP` | VISIT_REQUEST | C-12, C-13, C-14 | Cấm | Bắt buộc |
| 11 | `VISIT_CONTACT_CLAIM` | VISIT_REQUEST | C-15 | Cấm | Bắt buộc |
| 12 | `VISIT_CONTACT_TRANSFER` | VISIT_REQUEST | C-16 | Cấm | Bắt buộc |
| 13 | `VISIT_PARTICIPANT_INVITATION` | VISIT_PARTICIPANT | C-17 | Cấm | Bắt buộc |
| 14 | `VISIT_STUDENT_INVITATION` | VISIT_PARTICIPANT | C-17 | Cấm | Bắt buộc |
| 15 | `VISIT_DEPARTMENT_LEADER_INVITATION` | VISIT_PARTICIPANT | C-17 (nhánh dept) | Cấm | Bắt buộc |
| 16 | `VISIT_DEPARTMENT_STAFF_ASSIGNMENT` | VISIT_PARTICIPANT | C-18 | Cấm | Bắt buộc |
| 17 | `VISIT_REMINDER_HOST` | VISIT_REMINDER | C-23 | Cấm mặc định | Bắt buộc |
| 18 | `VISIT_REMINDER_PARTICIPANTS` | VISIT_REMINDER | C-23 | Cấm mặc định | Bắt buộc (không lộ danh sách) |
| 19 | `LOGISTICS_REQUEST_TO_DEPARTMENT` | LOGISTICS | C-19 | Cấm | Bắt buộc |
| 20 | `LOGISTICS_ASSIGNEE_ASSIGNMENT` | LOGISTICS | C-20 | Cấm | Bắt buộc |
| 21 | `LOGISTICS_CHANGE_PROPOSAL_TO_HOST` | LOGISTICS | C-21 | Cấm | Bắt buộc |
| 22 | `LOGISTICS_EXPENSE_REPORT_REMINDER` | LOGISTICS | C-22 | Theo caller | Không bắt buộc |
| 23 | `REPORT_CAMPUS_OPERATION` | REPORT | C-24 | Theo caller | Không |
| 24 | `REPORT_DEPARTMENT_COLLABORATION` | REPORT | C-25 | Theo caller | Không |
| 25 | `REPORT_DEPARTMENT_INVOICE` | REPORT | C-26, C-29 | Theo caller | Không |
| 26 | `REPORT_PERSONNEL_PERFORMANCE` | REPORT | C-27, C-28 | Theo caller | Không |

**Template giữ lại từ seed hiện tại** (5): `VISIT_PARTICIPANT_INVITATION`, `VISIT_STUDENT_INVITATION`, `VISIT_DEPARTMENT_LEADER_INVITATION`, `LOGISTICS_REQUEST_TO_DEPARTMENT`, `LOGISTICS_ASSIGNEE_ASSIGNMENT`, `VISIT_REMINDER_HOST`, `VISIT_REMINDER_PARTICIPANTS` — **7 mã** (5 mã nhóm invitation/logistics + 2 mã reminder).

**Template bị loại khỏi fresh seed** (giữ `INACTIVE` nếu DB đang tồn tại có FK): `ACCOUNT_CREATED_INTERNAL`, `VISIT_REQUEST_APPROVED`, `VISIT_REQUEST_REJECTED`, `VISIT_CANCELLED`, `HOST_ASSIGNMENT`, `LOGISTICS_REQUEST`, `OTP_VISIT_REQUEST`, `VISIT_REQUEST_SUBMITTED_NOTIFY`, `LOGISTICS_REQUEST_SUBMITTED_NOTIFY` — **9 mã**.

---

## 7. Gate G1 — kết luận

| Điều kiện G1 | Trạng thái |
|---|---|
| 100% điểm gửi email được đưa vào ma trận | ✅ 33 mục C-01..C-33, phủ 27 file, gồm cả hosted service và preview |
| 100% seed template được phân loại | ✅ 16/16, không còn "unknown" |
| Report discrepancy đã có kết luận bằng evidence | ✅ Mục 3 — 6 caller thật, 0 template, gộp còn 4 mã đích |
| Không còn background job/helper/builder chưa phân loại | ✅ `VisitReminderDispatchHostedService`, `ParticipantInvitationEmailBuilder`, `AccountConfirmationEmail`, `EmailComposition`, `EmailActionTemplates` đều đã xếp loại |
| Danh mục template mục tiêu đã được review | ⏳ đề xuất ở Mục 6 — **cần owner duyệt trước khi chốt ở Giai đoạn 2** |

**G1 ĐẠT về mặt dữ liệu.** Điều kiện cuối chờ owner duyệt catalog.
