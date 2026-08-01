# 03 — System template catalog (Giai đoạn 2)

> Danh mục **chốt** cho `email_templates`. Là nguồn đối chiếu của: `SystemEmailTemplates` registry (code), khối seed canonical (SQL), script đồng bộ, và test hợp đồng "registry ↔ active seed khớp".
> Quyết định nền: `02-decisions-and-contracts.md` (DL-02, DL-03, DL-04).

**Quy ước đọc bảng**

- **Recipient policy** — `1-TO/cấm-copy` = đúng một `TO`, cấm CC/BCC, gửi riêng từng người. `caller` = số lượng do caller quyết, CC/BCC chỉ khi caller truyền rõ.
- **Nhạy cảm** — có OTP, token một lần, action URL riêng, hoặc nội dung cá nhân hoá không được gộp.
- **Biến** — đúng nội dung `variables_text`, lower camelCase. `{{actionBlock}}` **không** nằm trong `variables_text` (là `TrustedHtmlBlocks`, xem C-08).
- Mọi template: `campus_id = NULL` (không có bằng chứng nội dung khác nhau theo campus), `body_format = HTML`, `status = ACTIVE`, có **đủ** `subject_vi/body_vi/subject_en/body_en` (DL-04).

---

## 1. Nhóm ACCOUNT (8 template)

| # | Code | Caller | Recipient policy | Nhạy cảm | Biến (`variables_text`) | Action block |
|---|---|---|---|---|---|---|
| 1 | `ACCOUNT_EMAIL_CONFIRMATION` | C-01, C-02, C-03, C-08, C-10 | 1-TO/cấm-copy | ✅ token xác nhận | `fullName, roleName, campusName, expiresInHours` | `confirmEmail` |
| 2 | `ACCOUNT_PENDING_EMAIL_CHANGED_OLD_NOTICE` | C-04 | 1-TO/cấm-copy | ⚠️ không token | *(không biến)* | — |
| 3 | `ACCOUNT_ACTIVATED` | C-05 | 1-TO/cấm-copy | ❌ | `fullName, roleName, campusName` | `login` |
| 4 | `ACCOUNT_EMAIL_CHANGED_OLD_NOTICE` | C-06a | 1-TO/cấm-copy | ⚠️ | *(không biến)* | — |
| 5 | `ACCOUNT_EMAIL_CHANGED_NEW_NOTICE` | C-06b | 1-TO/cấm-copy | ⚠️ | `fullName, oldEmailMasked` | — |
| 6 | `ACCOUNT_ROLE_CHANGED` | C-07 | 1-TO/cấm-copy | ❌ | `fullName, oldRoleName, newRoleName, campusName` | — |
| 7 | `ACCOUNT_STAFF_LEADER_ASSIGNED` | C-09a | 1-TO/cấm-copy | ❌ | `fullName, campusName, effectiveDate, reason` | — |
| 8 | `ACCOUNT_STAFF_LEADER_REPLACED` | C-09b | 1-TO/cấm-copy | ❌ | `fullName, campusName, successorName, effectiveDate, reason` | — |

> **Sửa 7a-fix (đã duyệt).** Hai thông báo gửi tới **địa chỉ cũ** (#2, #4) bỏ hết biến: địa chỉ vừa bị gỡ có thể là địa chỉ gõ nhầm của một người không liên quan, nêu tên chủ tài khoản cho họ chính là rò rỉ. Hai thông báo Staff Leader (#7, #8) thêm `reason` — code cũ đã in lý do thay thế, bỏ đi là im lặng cắt thông tin người nhận đang dựa vào.

**Subject**

| # | VI | EN |
|---|---|---|
| 1 | `[PEMS] Xác nhận email để kích hoạt tài khoản` | `[PEMS] Confirm your email to activate your account` |
| 2 | `[PEMS] Email đăng ký của bạn đã được thay đổi` | `[PEMS] Your registration email has been changed` |
| 3 | `[PEMS] Tài khoản của bạn đã được kích hoạt` | `[PEMS] Your account has been activated` |
| 4 | `[PEMS] Email tài khoản đã được thay đổi` | `[PEMS] Your account email has been changed` |
| 5 | `[PEMS] Email này đã trở thành email tài khoản PEMS` | `[PEMS] This address is now your PEMS account email` |
| 6 | `[PEMS] Vai trò tài khoản của bạn đã thay đổi` | `[PEMS] Your account role has changed` |
| 7 | `[PEMS] Bạn được phân công làm Staff Leader {{campusName}}` | `[PEMS] You have been assigned Staff Leader of {{campusName}}` |
| 8 | `[PEMS] Bạn không còn là Staff Leader {{campusName}}` | `[PEMS] You are no longer Staff Leader of {{campusName}}` |

**Ràng buộc nội dung bắt buộc — template #1**
Nội dung **không được** mời đăng nhập ngay. Tài khoản mới ở `PENDING_EMAIL_CONFIRMATION`; login trước khi xác nhận bị chặn. Đây chính là lỗi nghiệp vụ của template legacy `ACCOUNT_CREATED_INTERNAL` ("Bạn có thể đăng nhập và sử dụng các chức năng được phân quyền tại…"), là một trong các lý do bỏ mã đó khỏi fresh seed. Test Batch 1 kiểm chuỗi này không xuất hiện.

---

## 2. Nhóm AUTH (1 template)

| # | Code | Caller | Recipient policy | Nhạy cảm | Biến |
|---|---|---|---|---|---|
| 9 | `AUTH_PASSWORD_RESET_OTP` | C-11 | 1-TO/cấm-copy | ✅ **OTP** | `fullName, otpCode, expireMinutes` |

| Subject VI | Subject EN |
|---|---|
| `[PEMS] Mã đặt lại mật khẩu` | `[PEMS] Your password reset code` |

`expireMinutes` lấy từ config `Otp:CodeMinutes` (hiện 15), **không** hard-code trong template.

---

## 3. Nhóm VISIT_REQUEST (3 template)

| # | Code | Caller | Recipient policy | Nhạy cảm | Biến | Action block |
|---|---|---|---|---|---|---|
| 10 | `VISIT_REQUEST_OTP` | C-12, C-13, C-14 | 1-TO/cấm-copy | ✅ **OTP** | `fullName, otpCode, expireMinutes` | — |
| 11 | `VISIT_CONTACT_CLAIM` | C-15 | 1-TO/cấm-copy | ✅ action URL riêng | `contactFullName, requestCode, delegationName` | `claim` |
| 12 | `VISIT_CONTACT_TRANSFER` | C-16 | 1-TO/cấm-copy | ✅ action URL riêng | `contactFullName, requestCode, delegationName, currentContactName` | `transfer` |

| # | Subject VI | Subject EN |
|---|---|---|
| 10 | `[PEMS] Mã xác thực đăng ký tham quan` | `[PEMS] Campus visit registration verification code` |
| 11 | `[PEMS] Xác nhận vai trò đầu mối liên hệ — {{requestCode}}` | `[PEMS] Confirm your primary contact role — {{requestCode}}` |
| 12 | `[PEMS] Lời mời nhận vai trò đầu mối liên hệ — {{requestCode}}` | `[PEMS] Invitation to take the primary contact role — {{requestCode}}` |

`expireMinutes` của #10 lấy từ `Otp:VisitRequestCodeMinutes` (hiện 5). Giá trị 5 phút hiện đang hard-code trong HTML của `EmailService.SendVisitRequestOtpAsync` — sau khi chuyển sang template nó thành biến, đúng nguồn cấu hình.

---

## 4. Nhóm VISIT_PARTICIPANT (4 template)

| # | Code | Caller | Recipient policy | Nhạy cảm | Biến | Action block |
|---|---|---|---|---|---|---|
| 13 | `VISIT_PARTICIPANT_INVITATION` | C-17 (IC staff) | 1-TO/cấm-copy | ✅ accept/decline token | `recipientName, delegationName, campusName, plannedTime, hostName, roleLabel, hostMessage` | accept + decline |
| 14 | `VISIT_STUDENT_INVITATION` | C-17 (student) | 1-TO/cấm-copy | ✅ | như #13 | accept + decline |
| 15 | `VISIT_DEPARTMENT_LEADER_INVITATION` | C-17 (dept leader) | 1-TO/cấm-copy | ✅ | như #13 | accept + decline + **assign** |
| 16 | `VISIT_DEPARTMENT_STAFF_ASSIGNMENT` | C-18 | 1-TO/cấm-copy | ✅ | `recipientName, delegationName, campusName, plannedTime, departmentName` | accept + decline |

| # | Subject VI | Subject EN |
|---|---|---|
| 13 | `[PEMS] Lời mời tham gia hỗ trợ tiếp khách — {{delegationName}}` | `[PEMS] Invitation to support a delegation visit — {{delegationName}}` |
| 14 | `[PEMS] Lời mời sinh viên hỗ trợ tiếp khách — {{delegationName}}` | `[PEMS] Student invitation to support a delegation visit — {{delegationName}}` |
| 15 | `[PEMS] Yêu cầu phòng ban hỗ trợ tiếp khách — {{delegationName}}` | `[PEMS] Department support request — {{delegationName}}` |
| 16 | `[PEMS] Bạn được phân công hỗ trợ tiếp khách — {{delegationName}}` | `[PEMS] You have been assigned to support a delegation visit — {{delegationName}}` |

**#16 sửa lỗi mượn mã.** `AssignDepartmentStaffCommandHandler` hiện lưu `email_template_id` của `VISIT_PARTICIPANT_INVITATION` cho một nghiệp vụ khác (gán, không phải mời). Từ nay dùng mã riêng.

**#14 sửa D-18.** `PreviewEmailTemplateQueryHandler` còn nhắc mã cũ `VISIT_STUDENT_SUPPORT_INVITATION`; mã đúng là `VISIT_STUDENT_INVITATION` (kế hoạch Mục 13.6). Tham chiếu cũ bị xoá.

`hostMessage` là lời nhắn tuỳ chọn của host. Khi host không nhập, caller **vẫn phải truyền** chuỗi rỗng — renderer không có fallback (C-03 bước 6). Template dùng nó trong một khối luôn hiện; chuỗi rỗng cho ra khối trống, chấp nhận được.

---

## 5. Nhóm VISIT_REMINDER (2 template)

| # | Code | Caller | Recipient policy | Nhạy cảm | Biến | Action block |
|---|---|---|---|---|---|---|
| 17 | `VISIT_REMINDER_HOST` | C-23 | 1-TO/cấm-copy | ❌ | `hostName, delegationName, campusName, plannedStart, plannedEnd` | detail link |
| 18 | `VISIT_REMINDER_PARTICIPANTS` | C-23 | 1-TO/cấm-copy | ❌ | `recipientName, delegationName, campusName, plannedStart, plannedEnd` | detail link |

| # | Subject VI | Subject EN |
|---|---|---|
| 17 | `[PEMS] Nhắc lịch tiếp khách — {{delegationName}}` | `[PEMS] Visit reminder — {{delegationName}}` |
| 18 | `[PEMS] Nhắc lịch tham gia hỗ trợ — {{delegationName}}` | `[PEMS] Reminder: you are supporting a visit — {{delegationName}}` |

**#18: cấm lộ danh sách người khác.** Nội dung chỉ nói về người nhận; không liệt kê participant khác. Gửi riêng từng người (kế hoạch Mục 13.9). Test kiểm mỗi message có đúng 1 `TO`, 0 `CC`, 0 `BCC`.

---

## 6. Nhóm LOGISTICS (4 template)

| # | Code | Caller | Recipient policy | Nhạy cảm | Biến | Action block |
|---|---|---|---|---|---|---|
| 19 | `LOGISTICS_REQUEST_TO_DEPARTMENT` | C-19 | 1-TO/cấm-copy | ✅ accept/decline/detail | `departmentLeaderName, requesterName, logisticsTitle, logisticsItemType, quantity, usageStartAt, usageEndAt, dueAt, coordinationNote` | logistics (3 nút) |
| 20 | `LOGISTICS_ASSIGNEE_ASSIGNMENT` | C-20 | 1-TO/cấm-copy | ✅ | `assigneeName, logisticsTitle, dueAt, campusName, delegationName` | accept + decline + detail |
| 21 | `LOGISTICS_CHANGE_PROPOSAL_TO_HOST` | C-21 | 1-TO/cấm-copy | ✅ approve/reject | `hostName, logisticsTitle, departmentName, proposalNote` | proposal (3 nút) |
| 22 | `LOGISTICS_EXPENSE_REPORT_REMINDER` | C-22 | **caller** | ❌ | `recipientName, itemTitle, dueAt, delegationName` | detail link |

| # | Subject VI | Subject EN |
|---|---|---|
| 19 | `[PEMS] Yêu cầu hậu cần mới — {{logisticsTitle}}` | `[PEMS] New logistics request — {{logisticsTitle}}` |
| 20 | `[PEMS] Bạn được phân công xử lý — {{logisticsTitle}}` | `[PEMS] You have been assigned — {{logisticsTitle}}` |
| 21 | `[PEMS] Đề xuất thay đổi yêu cầu hậu cần — {{logisticsTitle}}` | `[PEMS] Change proposal for a logistics request — {{logisticsTitle}}` |
| 22 | `[PEMS] Nhắc kê khai chi phí — {{itemTitle}}` | `[PEMS] Expense report reminder — {{itemTitle}}` |

**#19 bỏ fallback im lặng.** Hiện `PrepareVisitLogisticsCommandHandler:250` có `template.SubjectVi ?? "[PEMS] Yêu cầu hậu cần mới — {item.Title}"`. Sau chuẩn hoá: template thiếu → `EMAIL_TEMPLATE_LANGUAGE_CONTENT_MISSING`, không fallback.

**`coordinationNote` không còn fallback đặc biệt.** `EmailComposition.RenderTemplate` hiện có một bảng fallback riêng cho `coordinationNote`/`quantity`/`departmentName`… (`"Chưa có thông tin"`, `"Chưa chọn phòng ban"`). Bảng này bị **xoá**; caller phải truyền giá trị hiển thị (kể cả chuỗi "Không có ghi chú phối hợp." nếu nghiệp vụ muốn vậy). Quyết định hiển thị thuộc caller, không thuộc renderer.

---

## 7. Nhóm REPORT (5 template)

| # | Code | Caller | Recipient policy | Nhạy cảm | Biến | Attachment |
|---|---|---|---|---|---|---|
| 23 | `REPORT_CAMPUS_OPERATION` | C-24 | **caller** | ❌ | `recipientName, campusName, periodFrom, periodTo` | ✅ PDF |
| 24 | `REPORT_DEPARTMENT_COLLABORATION` | C-25 | **caller** | ❌ | `recipientName, departmentName, periodFrom, periodTo` | ✅ PDF |
| 25 | `REPORT_DEPARTMENT_INVOICE` | C-26, C-29 | **caller** | ❌ | `recipientName, departmentName, periodFrom, periodTo` | ✅ PDF |
| 26 | `REPORT_PERSONNEL_PERFORMANCE` | C-27, C-28 | **caller** | ❌ | `personName, scopeLabel, periodFrom, periodTo` | ✅ PDF |
| 31 | `VISIT_SETUP_PROGRESS_UPDATE` | C-31 | **caller** | ❌ | `delegationName, campusName, plannedStart, plannedEnd, hostName` | ✅ PDF (bắt buộc) |

| # | Subject VI | Subject EN |
|---|---|---|
| 23 | `[PEMS] Báo cáo vận hành campus — {{campusName}} ({{periodFrom}} – {{periodTo}})` | `[PEMS] Campus operations report — {{campusName}} ({{periodFrom}} – {{periodTo}})` |
| 24 | `[PEMS] Báo cáo phối hợp tiếp khách — {{departmentName}} ({{periodFrom}} – {{periodTo}})` | `[PEMS] Visit collaboration report — {{departmentName}} ({{periodFrom}} – {{periodTo}})` |
| 25 | `[PEMS] Hóa đơn hậu cần tiếp khách — {{departmentName}} ({{periodFrom}} – {{periodTo}})` | `[PEMS] Visit logistics invoice — {{departmentName}} ({{periodFrom}} – {{periodTo}})` |
| 26 | `[PEMS] Báo cáo hiệu suất {{scopeLabel}} — {{personName}} ({{periodFrom}} – {{periodTo}})` | `[PEMS] Performance report {{scopeLabel}} — {{personName}} ({{periodFrom}} – {{periodTo}})` |
| 31 | `[PEMS] Cập nhật công tác chuẩn bị — {{delegationName}} tại {{campusName}}` | `[PEMS] Preparation update — {{delegationName}} at {{campusName}}` |

**#31 nằm ở nhóm REPORT, không phải VISIT_PARTICIPANT.** Đây là việc **phát hành một tài liệu** (Báo cáo Lịch trình) tới danh sách người nhận do Host kiểm soát — khách ở `TO`, thành phần tham gia đã `ACCEPTED` ở `CC` — chứ không phải lời mời. Không có token, không có liên kết hành động dùng một lần, nên `CC` là an toàn. Nếu tái dùng `VISIT_PARTICIPANT_INVITATION` (#15) thì luồng này thừa hưởng chính sách 1-TO/cấm-copy của nó: mỗi người một email, không `CC` — trái hẳn mục đích.

**#31 PDF là bắt buộc, không phải tùy chọn.** Tệp được sinh server-side bằng `IScheduleReportArtifactService` (dùng chung với nút tải "Báo cáo Lịch trình" trên VisitProcess), lưu qua pipeline file hiện có rồi liên kết vào `email_draft_attachments`. Composer không cho xóa tệp này; lệnh gửi từ chối nếu draft không còn nó. Nhận diện tệp bắt buộc bằng cách join sang `documents` (`document_category = 'SCHEDULE_REPORT'`, `owner_id` = request), **không** theo tên tệp — để Host tải lên một PDF trùng tên cũng không tạo ra tệp không xóa được.

**#31 chỉ Host hiện tại gửi được.** Cờ `canSendSetupProgressEmail` trong process-permissions, và mọi route đều kiểm tra lại host + cửa sổ chuẩn bị **tại thời điểm gọi** — bàn giao Host hoặc chuyển giai đoạn giữa lúc soạn và lúc gửi đều bị từ chối.

**Không chèn `preparation_note` vào #31.** Ghi chú chuẩn bị là nội dung vận hành nội bộ (briefing, phân công), không dành cho khách. Host tự viết phần phù hợp trong composer nếu muốn.

**Gộp #25 (C-26 + C-29):** subject hiện tại của hai caller **giống hệt từng ký tự**; khác nhau chỉ ở người nhận (người-nhận-cấu-hình vs Staff Leader) — thuộc phong bì, không thuộc nội dung.

**Gộp #26 (C-27 + C-28):** khác duy nhất là chuỗi mô tả scope — `"tham gia tiếp khách"` (Staff Leader gửi cho student) vs `"phụ trách đoàn khách"` (các trường hợp còn lại) vs `"nhiệm vụ tiếp khách"` (Dept Leader gửi). Đưa vào biến `scopeLabel`, caller quyết định.
Bản EN của `scopeLabel` do caller truyền (`"visit support"` / `"delegation hosting"` / `"visit assignments"`) — caller chọn theo `Language`.

**#23–26 giữ `Mandatory`.** Sáu caller report hiện gọi `SendAsync` **không bọc try/catch** (D-20): lỗi SMTP làm hỏng cả lệnh. Giữ nguyên hành vi mandatory này — người dùng bấm "gửi báo cáo" cần biết ngay là thất bại. Ghi rõ để không ai "sửa" thành best-effort.

---

## 8. Template bị loại khỏi fresh seed (9 mã)

Theo DL-03: **không** có trong khối seed canonical mới; script đồng bộ chuyển `status='INACTIVE'` ở DB đang tồn tại (không `DELETE`, vì `sent_emails` seed đang FK tới `email_template_id` 1..16).

| Mã | Phân loại | Lý do |
|---|---|---|
| `ACCOUNT_CREATED_INTERNAL` | dead code | Không caller; nội dung mời đăng nhập ngay **mâu thuẫn** luồng `PENDING_EMAIL_CONFIRMATION` |
| `VISIT_REQUEST_APPROVED` | notification-only | Duyệt đơn chỉ tạo in-app notification. Cấm thêm caller (D-05) |
| `VISIT_REQUEST_REJECTED` | notification-only | như trên |
| `VISIT_CANCELLED` | notification-only | như trên |
| `HOST_ASSIGNMENT` | notification-only | như trên |
| `VISIT_REQUEST_SUBMITTED_NOTIFY` | notification-only | như trên |
| `LOGISTICS_REQUEST` | dead code | Mã cũ, trùng nghĩa `LOGISTICS_REQUEST_TO_DEPARTMENT` |
| `LOGISTICS_REQUEST_SUBMITTED_NOTIFY` | dead code | Không caller |
| `OTP_VISIT_REQUEST` | dead code | Mã cũ; nghiệp vụ chuyển sang `VISIT_REQUEST_OTP` (#10) |

**Không xoá dữ liệu lịch sử.** `sent_emails.body_snapshot` giữ nguyên (Mục 16.6 điểm 7). Template do người dùng tự tạo (không nằm trong 16 mã seed và không nằm trong catalog này) **không bị đụng đến** (Mục 16.6 điểm 4).

---

## 9. Bảng đối chiếu registry ↔ seed (test hợp đồng)

Test bắt buộc ở Giai đoạn 3 (`SystemEmailTemplateContractTests`):

| Khẳng định | Cách kiểm |
|---|---|
| Mọi mã trong registry đều có hàng `ACTIVE` trong DB | so tập hợp |
| Mọi hàng `ACTIVE` trong DB đều có mã trong registry | so tập hợp (chiều ngược) — bắt "active seed không caller" |
| `purpose` của mỗi hàng ∈ `EmailTemplatePurposes.All` | so tập hợp |
| `variables_text` khớp **chính xác** tập placeholder có trong `subject_vi+body_vi+subject_en+body_en` | parse + so sánh |
| `variables_text` khớp `DeclaredVariables` của registry | so sánh |
| Không mã nào thiếu VI hoặc EN | kiểm null/rỗng 4 trường |
| Không placeholder PascalCase | regex `\{\{\s*[A-Z]` không khớp |
| Không action URL nằm trong `variables_text` | giao với danh sách C-08 phải rỗng |
| Mọi mã render được VI và EN, 0 placeholder sót | gọi renderer với biến giả |

Số lượng kỳ vọng: **26 template `ACTIVE`**, 0 `INACTIVE` trong fresh DB.
