---
type: email-audit
feature: email-standardization
status: draft
updated: 2026-08-03
---

# Rà soát toàn bộ danh mục email template

> Phạm vi: **toàn bộ** catalog, không chỉ mẫu đang báo lỗi trên màn hình. Đối chiếu 6 nguồn —
> registry trong code, `email-template-defaults.json`, canonical SQL, database thật, các send point,
> và chính sách liên hệ — theo cả hai chiều.

## 0. Kết luận ngắn

Một lỗi duy nhất giải thích gần như toàn bộ hiện tượng: **canonical SQL không import được**.

Commit `2f0fe8ec` dán khối `INSERT INTO email_contact_policies` **vào giữa** chuỗi ký tự
`body_vi` của dòng 70030, cắt đôi câu lệnh `INSERT INTO email_templates` 31 dòng. MySQL dừng ở
`ERROR 1064` và **mọi câu lệnh sau đó không chạy** — gồm cả khối dựng lại catalog, khối seed chính
sách liên hệ và các `ALTER` cuối file. Import mới vì thế dừng lại ở danh mục demo 16 mẫu của mục 7,
và đó đúng là thứ đang nằm trong `pems_db`.

Hệ quả đo được trên máy thật, trước khi sửa:

| Nguồn | Số template |
|---|--:|
| Registry (`SystemEmailTemplates`) | 31 |
| `email-template-defaults.json` | 31 |
| Canonical SQL — import mới (sau khi sửa) | 31 |
| `pems_db` (database phát triển) | 16 |
| Có send point thật trong code | 31 |

24 mẫu ứng dụng cần thì **không có** trong database; 9 mẫu trong database thì **không mã nào gửi**.
`VISIT_REQUEST_APPROVED` — mẫu người dùng nhìn thấy lỗi — là một trong 9 mẫu đó.

## 1. Nguồn chuẩn (source of truth)

| Vai trò | File | Ghi chú |
|---|---|---|
| Registry (chuẩn) | `backend/PEMS.Application/Emails/Common/SystemEmailTemplates.cs` | 31 mã, mỗi mã có send point thật |
| Nội dung mặc định | `.../Emails/Common/Assets/email-template-defaults.json` | **artefact sinh ra** từ canonical SQL, không phải nguồn |
| Nội dung chuẩn | `docs/database/scripts/PEMS_FULL_VS_31_07_NEW.sql` (khối R0) | nguồn thật của nội dung |
| Chính sách liên hệ | `.../Emails/Contact/EmailContactPolicyDefaults.cs` | 31 template + 1 dòng SYSTEM |
| Hợp đồng biến | `.../Emails/Common/EmailTemplateContract.cs` | `Describe()` trả null cho mã không đăng ký |
| Renderer | `backend/PEMS.Infrastructure/Email/EmailTemplateRenderer.cs` | |
| Màn quản lý | `frontend/pems-react/src/pages/dashboard/emails/TemplateManagement.tsx` | |

**Database KHÔNG phải nguồn chuẩn.** Một dòng trong `email_templates` mà registry không có thì
không có mã nào gửi nó, dù nội dung trông hợp lý đến đâu.

## 2. Ma trận đối chiếu — toàn bộ 40 mã

Cột **DB** đo trên `pems_db` *trước* khi chạy patch. Cột **SQL** đo bằng import thật vào database
dùng một lần, sau khi sửa lỗi cú pháp.

| Template code | Reg | Def | SQL | DB | Send point | Body vars | Contact | Nhạy cảm | Verdict |
|---|:-:|:-:|:-:|:-:|---|---|---|:-:|---|
| `ACCOUNT_ACTIVATED` | ✅ | ✅ | ✅ | — | 1× `Commands/ConfirmAccountEmail` | 5 | REQUIRED/CAMPUS_DEFAULT/RT:— | · | MISSING_IN_DATABASE |
| `ACCOUNT_CREATED_INTERNAL` | — | — | — | ✅ | — | 4 | — | — | ORPHAN_DATABASE_TEMPLATE |
| `ACCOUNT_EMAIL_CHANGED_NEW_NOTICE` | ✅ | ✅ | ✅ | — | 3× `Commands/UpdateAccountRole` | 3 | REQUIRED/CAMPUS_DEFAULT/RT:— | · | MISSING_IN_DATABASE |
| `ACCOUNT_EMAIL_CHANGED_OLD_NOTICE` | ✅ | ✅ | ✅ | — | 3× `Commands/UpdateAccountRole` | 1 | REQUIRED/SUPPORT_CONTACT/RT:— | · | MISSING_IN_DATABASE |
| `ACCOUNT_EMAIL_CONFIRMATION` | ✅ | ✅ | ✅ | — | 7× `Commands/CreateAccount` | 5 | NONE/—/RT:NONE | 🔒 | MISSING_IN_DATABASE |
| `ACCOUNT_PENDING_EMAIL_CHANGED_OLD_NOTICE` | ✅ | ✅ | ✅ | — | 2× `Accounts/Common` | 1 | REQUIRED/SUPPORT_CONTACT/RT:— | · | MISSING_IN_DATABASE |
| `ACCOUNT_ROLE_CHANGED` | ✅ | ✅ | ✅ | — | 1× `Commands/UpdateAccountRole` | 4 | OPTIONAL/CAMPUS_DEFAULT/RT:— | · | MISSING_IN_DATABASE |
| `ACCOUNT_STAFF_LEADER_ASSIGNED` | ✅ | ✅ | ✅ | — | 1× `Commands/ReplaceStaffLeader` | 4 | OPTIONAL/CAMPUS_DEFAULT/RT:— | · | MISSING_IN_DATABASE |
| `ACCOUNT_STAFF_LEADER_REPLACED` | ✅ | ✅ | ✅ | — | 1× `Commands/ReplaceStaffLeader` | 6 | REQUIRED/SUPPORT_CONTACT/RT:— | · | MISSING_IN_DATABASE |
| `AUTH_PASSWORD_RESET_OTP` | ✅ | ✅ | ✅ | — | 1× `Commands/ForgotPassword` | 3 | NONE/—/RT:NONE | 🔒 | MISSING_IN_DATABASE |
| `DEPT_LEADERSHIP_GRANTED` | ✅ | ✅ | ✅ | — | 1× `Commands/TransferDepartmentLeadership` | 2 | OPTIONAL/DEPARTMENT_DEFAULT/RT:— | · | MISSING_IN_DATABASE |
| `DEPT_LEADERSHIP_HANDED_OVER` | ✅ | ✅ | ✅ | — | 1× `Commands/TransferDepartmentLeadership` | 2 | OPTIONAL/DEPARTMENT_DEFAULT/RT:— | · | MISSING_IN_DATABASE |
| `DEPT_PERSONNEL_ACCOUNT_DISABLED` | ✅ | ✅ | ✅ | — | 1× `Commands/ChangePersonnelStatus` | 4 | REQUIRED/DEPARTMENT_DEFAULT/RT:— | · | MISSING_IN_DATABASE |
| `DEPT_PERSONNEL_ACCOUNT_ENABLED` | ✅ | ✅ | ✅ | — | 1× `Commands/ChangePersonnelStatus` | 2 | OPTIONAL/DEPARTMENT_DEFAULT/RT:— | · | MISSING_IN_DATABASE |
| `HOST_ASSIGNMENT` | — | — | — | ✅ | — | 6 | — | — | ORPHAN_DATABASE_TEMPLATE |
| `LOGISTICS_ASSIGNEE_ASSIGNMENT` | ✅ | ✅ | ✅ | ✅ | 1× `Commands/AssignRequestAssignee` | 6 | OPTIONAL/DEPARTMENT_DEFAULT/RT:— | 🔒 | CANONICAL_OK |
| `LOGISTICS_CHANGE_PROPOSAL_TO_HOST` | ✅ | ✅ | ✅ | — | 1× `Commands/ProposeRequestChange` | 12 | REQUIRED/DEPARTMENT_DEFAULT/RT:CONTACT | 🔒 | MISSING_IN_DATABASE |
| `LOGISTICS_EXPENSE_REPORT_REMINDER` | ✅ | ✅ | ✅ | — | 1× `Commands/RemindExpenseReports` | 5 | OPTIONAL/SENDER/RT:— | · | MISSING_IN_DATABASE |
| `LOGISTICS_REQUEST` | — | — | — | ✅ | — | 10 | — | — | ORPHAN_DATABASE_TEMPLATE, VI_EN_CONTENT_MISMATCH |
| `LOGISTICS_REQUEST_SUBMITTED_NOTIFY` | — | — | — | ✅ | — | 7 | — | — | ORPHAN_DATABASE_TEMPLATE |
| `LOGISTICS_REQUEST_TO_DEPARTMENT` | ✅ | ✅ | ✅ | ✅ | 1× `Commands/PrepareVisitLogistics` | 11 | REQUIRED/HOST_THEN_SENDER/RT:CONTACT | 🔒 | CANONICAL_OK |
| `OTP_VISIT_REQUEST` | — | — | — | ✅ | — | 3 | — | — | ORPHAN_DATABASE_TEMPLATE |
| `REPORT_CAMPUS_OPERATION` | ✅ | ✅ | ✅ | — | 1× `Commands/SendHoCampusReport` | 4 | OPTIONAL/SENDER/RT:— | · | MISSING_IN_DATABASE |
| `REPORT_DEPARTMENT_COLLABORATION` | ✅ | ✅ | ✅ | — | 1× `Commands/SendStaffLeaderDepartmentReport` | 4 | OPTIONAL/SENDER/RT:— | · | MISSING_IN_DATABASE |
| `REPORT_DEPARTMENT_INVOICE` | ✅ | ✅ | ✅ | — | 2× `Commands/SendDeptLeaderInvoiceToStaffLeader` | 4 | OPTIONAL/SENDER/RT:— | · | MISSING_IN_DATABASE |
| `REPORT_PERSONNEL_PERFORMANCE` | ✅ | ✅ | ✅ | — | 2× `Commands/SendDeptLeaderPersonnelReport` | 4 | OPTIONAL/SENDER/RT:— | · | MISSING_IN_DATABASE |
| `VISIT_CANCELLED` | — | — | — | ✅ | — | 6 | — | — | ORPHAN_DATABASE_TEMPLATE, VI_EN_CONTENT_MISMATCH |
| `VISIT_CONTACT_CLAIM` | ✅ | ✅ | ✅ | — | 1× `Services` | 4 | OPTIONAL/CAMPUS_DEFAULT/RT:— | 🔒 | MISSING_IN_DATABASE |
| `VISIT_CONTACT_TRANSFER` | ✅ | ✅ | ✅ | — | 1× `Services` | 5 | OPTIONAL/CAMPUS_DEFAULT/RT:— | 🔒 | MISSING_IN_DATABASE |
| `VISIT_DEPARTMENT_LEADER_INVITATION` | ✅ | ✅ | ✅ | ✅ | 1× `Commands/InviteVisitParticipant` | 9 | REQUIRED/HOST/RT:CONTACT | 🔒 | CANONICAL_OK |
| `VISIT_DEPARTMENT_STAFF_ASSIGNMENT` | ✅ | ✅ | ✅ | — | 1× `Commands/AssignDepartmentStaff` | 7 | REQUIRED/HOST/RT:CONTACT | 🔒 | MISSING_IN_DATABASE |
| `VISIT_PARTICIPANT_INVITATION` | ✅ | ✅ | ✅ | ✅ | 1× `Commands/InviteVisitParticipant` | 9 | REQUIRED/HOST/RT:CONTACT | 🔒 | CANONICAL_OK |
| `VISIT_REMINDER_HOST` | ✅ | ✅ | ✅ | ✅ | 1× `Delegations/Reminders` | 6 | NONE/—/RT:NONE | · | CANONICAL_OK |
| `VISIT_REMINDER_PARTICIPANTS` | ✅ | ✅ | ✅ | ✅ | 1× `Delegations/Reminders` | 7 | REQUIRED/HOST/RT:CONTACT | · | CANONICAL_OK |
| `VISIT_REQUEST_APPROVED` | — | — | — | ✅ | — | 4 | — | — | ORPHAN_DATABASE_TEMPLATE |
| `VISIT_REQUEST_OTP` | ✅ | ✅ | ✅ | — | 1× `Emails/Common` | 3 | NONE/—/RT:NONE | 🔒 | MISSING_IN_DATABASE |
| `VISIT_REQUEST_REJECTED` | — | — | — | ✅ | — | 4 | — | — | ORPHAN_DATABASE_TEMPLATE |
| `VISIT_REQUEST_SUBMITTED_NOTIFY` | — | — | — | ✅ | — | 5 | — | — | ORPHAN_DATABASE_TEMPLATE |
| `VISIT_SETUP_PROGRESS_UPDATE` | ✅ | ✅ | ✅ | — | 1× `Delegations/SetupProgressEmail` | 7 | REQUIRED/HOST/RT:CONTACT | · | MISSING_IN_DATABASE |
| `VISIT_STUDENT_INVITATION` | ✅ | ✅ | ✅ | ✅ | 1× `Commands/InviteVisitParticipant` | 9 | REQUIRED/HOST/RT:CONTACT | 🔒 | CANONICAL_OK |

Tổng hợp verdict: `CANONICAL_OK` 7 · `MISSING_IN_DATABASE` 24 · `ORPHAN_DATABASE_TEMPLATE` 9 ·
`VI_EN_CONTENT_MISMATCH` 2.

Không có mã nào rơi vào `MISSING_IN_REGISTRY`, `MISSING_IN_DEFAULTS`, `MISSING_IN_SQL`,
`SEND_POINT_WITHOUT_TEMPLATE`, `UNUSED_REGISTERED_TEMPLATE`, `DUPLICATE_TEMPLATE_CODE`.

## 3. Đối chiếu placeholder (ba tập hợp)

Với mỗi mã: **A** = biến khai báo trong registry · **B** = biến thật trong subject/body ·
**C** = biến send point cung cấp.

- **B − A = ∅** cho cả 31 mã canonical. Không mẫu nào dùng biến chưa khai báo.
  (Ba khối tin cậy — `actionBlock`, `contactInformationBlock`, `setupSummaryBlock` — do renderer
  gắn, không nằm trong danh sách biến của người vận hành, và được loại trừ khi đối chiếu.)
- **A − B = ∅** cho cả 31 mã. Không có biến khai báo mà không dùng.
- **C**: mọi send point đều gọi qua hằng `SystemEmailTemplates.*`, không có chuỗi mã viết tay,
  nên không tồn tại send point trỏ tới mã không có thật.
- `VI_EN_CONTENT_MISMATCH` chỉ xảy ra ở 2 mẫu **legacy** (`LOGISTICS_REQUEST`, `VISIT_CANCELLED`):
  bản tiếng Việt dùng camelCase còn bản tiếng Anh dùng PascalCase. Chính sự lệch đó là bằng chứng
  nội dung này có từ trước khi có registry.

Biến PascalCase (`{{RecipientName}}`, `{{RequestCode}}`…) **chỉ** xuất hiện trong 9 mẫu legacy.
Catalog hiện tại dùng camelCase tuyệt đối. Đây là dấu hiệu nhận dạng đáng tin, và patch dùng nó.

## 4. Mẫu legacy / orphan — có gì tham chiếu?

Đo trên `pems_db` bằng FK thật (`sent_emails.email_template_id`, `email_drafts.email_template_id`):

| Template code | sent_emails | email_drafts | Xử lý | Verdict của patch |
|---|--:|--:|---|---|
| `ACCOUNT_CREATED_INTERNAL` | 4 | 0 | Giữ dòng, chuyển `status=INACTIVE` | `DEACTIVATED_LEGACY_WITH_HISTORY` |
| `HOST_ASSIGNMENT` | 2 | 0 | Giữ dòng, chuyển `status=INACTIVE` | `DEACTIVATED_LEGACY_WITH_HISTORY` |
| `LOGISTICS_REQUEST` | 1 | 0 | Giữ dòng, chuyển `status=INACTIVE` | `DEACTIVATED_LEGACY_WITH_HISTORY` |
| `LOGISTICS_REQUEST_SUBMITTED_NOTIFY` | 0 | 0 | Xoá khỏi bảng | `REMOVED_UNUSED_LEGACY` |
| `OTP_VISIT_REQUEST` | 0 | 0 | Xoá khỏi bảng | `REMOVED_UNUSED_LEGACY` |
| `VISIT_CANCELLED` | 8 | 0 | Giữ dòng, chuyển `status=INACTIVE` | `DEACTIVATED_LEGACY_WITH_HISTORY` |
| `VISIT_REQUEST_APPROVED` | 1 | 0 | Giữ dòng, chuyển `status=INACTIVE` | `DEACTIVATED_LEGACY_WITH_HISTORY` |
| `VISIT_REQUEST_REJECTED` | 1 | 0 | Giữ dòng, chuyển `status=INACTIVE` | `DEACTIVATED_LEGACY_WITH_HISTORY` |
| `VISIT_REQUEST_SUBMITTED_NOTIFY` | 0 | 0 | Xoá khỏi bảng | `REMOVED_UNUSED_LEGACY` |

Không mẫu legacy nào còn bản nháp trỏ tới, nên không mã nào rơi vào
`SKIPPED_LEGACY_HAS_DEPENDENCY` trên database này — nhánh đó vẫn có trong patch cho database khác.

## 5. Phân nhóm và quyết định

| Nhóm | Số mã | Quyết định |
|---|--:|---|
| **A** — canonical, đang dùng | 31 | Đồng bộ registry ↔ defaults ↔ SQL ↔ DB ↔ payload ↔ policy. Đã xanh cả 6. |
| **B** — có send point nhưng thiếu catalog | 0 | Không có. Mọi send point đều trỏ tới mã đã đăng ký. |
| **C** — có trong DB nhưng không còn send point | 9 | Xoá nếu không gì tham chiếu; giữ + `INACTIVE` nếu còn lịch sử; để yên nếu còn bản nháp. Màn quản lý hiển thị read-only. |
| **D** — có registry nhưng không có send point | 0 | Không có. |

Nhóm C **không bị xoá theo tên giống nhau**: `OTP_VISIT_REQUEST` bị xoá vì không có gì tham chiếu,
chứ không phải vì "đã có `VISIT_REQUEST_OTP` thay thế". `VISIT_REQUEST_APPROVED` được **giữ lại**
dù đã có mã canonical khác, vì còn 1 email trong lịch sử trỏ tới nó.

## 6. Màn quản lý mẫu — canonical và historical

Lỗi: mở `VISIT_REQUEST_APPROVED` thì mọi biến trong nội dung đều bị báo sai.

Nguyên nhân: API trả hợp đồng cho mã không đăng ký với `isSystemTemplate: false` và **danh sách biến
rỗng**; màn hình vẫn đem nội dung đối chiếu với danh sách rỗng đó, nên mọi `{{...}}` thành
`EMAIL_TEMPLATE_VARIABLE_UNKNOWN`, và số lỗi đó lại khoá luôn nút **Cập nhật**.

Đã sửa:

- `validateContent` trả về rỗng khi `isSystemTemplate === false`. Không phải nới lỏng: backend từ
  chối lưu mẫu không thuộc catalog bằng `EMAIL_TEMPLATE_CATALOG_FIXED`, nên không có nội dung nào
  lọt qua nhờ thay đổi này.
- Mẫu lịch sử hiển thị **read-only** (tên, mô tả, tiêu đề, nội dung đều khoá) đúng như API sẽ xử sự.
- Cảnh báo nói rõ ba điều: đây là mẫu lịch sử, chỉ xem được, và **vì sao** biến không được kiểm tra.
- Không hiện phần **Cấu hình thông tin liên hệ** cho mẫu lịch sử — chính sách liên hệ khoá theo mã đã
  đăng ký, hiện ra ở đây là bày một cấu hình không bao giờ có tác dụng.
- Mẫu hệ thống vẫn **fail-closed** y như cũ.

## 7. Canonical SQL và patch

### 7.1 Sửa canonical SQL

Khối chính sách được **di chuyển** ra sau dấu kết thúc của `INSERT INTO email_templates`. Không thêm,
không bớt, không viết lại: file sau khi sửa là một hoán vị **từng ký tự** của file trước.

Kiểm chứng bằng import thật (MySQL 8.0.46, database dùng một lần):

| Kiểm tra | Trước | Sau |
|---|---|---|
| `mysql` exit code | 1 (`ERROR 1064`) | 0 |
| `email_templates` | 16 (danh mục demo) | 31 |
| `email_contact_policies` | 0 | 32 (31 TEMPLATE + 1 SYSTEM) |
| Nội dung khớp `email-template-defaults.json` | 0/16 | **31/31 byte-identical** |

### 7.2 Patch mới — `2026-08-03_email_template_catalog_alignment.sql`

Đưa một database đang chạy từ danh mục 16 mẫu về danh mục canonical. Nội dung 31 dòng được **trích
nguyên văn** từ canonical SQL bằng script sinh, nên hai file không thể lệch nhau.

Chạy thử trên **bản sao đầy đủ của `pems_db`**:

| Lần chạy | canonical | legacy còn lại | `sent_emails` | digest nội dung |
|---|--:|--:|--:|---|
| Trước | 7 | 9 | 17 | `47e657c3…` |
| Sau lần 1 | 31 | 6 | 17 | `fd794329…` |
| Sau lần 2 | 31 | 6 | 17 | `fd794329…` |

Digest không đổi giữa hai lần chạy → **idempotent**, đo chứ không suy luận.

### 7.3 Hai lỗi trong patch cũ `2026-08-03_email_contact_information_block.sql`

Cả hai chỉ lộ ra khi thực sự chạy patch:

1. **`ERROR 1267 Illegal mix of collations`** — biến người dùng (`SET @blk := …`) nhận collation mặc
   định của kết nối (`utf8mb4_0900_ai_ci`) còn cột `body_vi` là `utf8mb4_unicode_ci`; phép `LIKE`
   giữa hai bên dừng ngay. Patch **không chạy được dòng nào**. Đã sửa bằng `COLLATE` tường minh.
2. **Dòng SYSTEM nhân bản** — khoá `uq_email_contact_policies_scope` là `(scope_type, scope_key)`
   mà dòng SYSTEM có `scope_key = NULL`; MySQL coi mỗi NULL là một giá trị khác nhau nên
   `INSERT IGNORE` **không** chặn trùng. Đo thật: chạy 3 lần ra 3 dòng SYSTEM, và chuỗi kế thừa mất
   đáy xác định. Đã đổi sang `INSERT … WHERE NOT EXISTS` kèm bước dọn dòng thừa.

Thứ tự chạy: `catalog_alignment` **trước**, rồi `contact_information_block`.

## 8. Chính sách liên hệ — toàn catalog

31/31 mã canonical có chính sách; không chính sách nào trỏ tới mã không tồn tại.

| Mức | Số mã | Nguồn đầu mối |
|---|--:|---|
| `REQUIRED` | 14 | HOST (6) · SUPPORT_CONTACT (3) · CAMPUS_DEFAULT (2) · DEPARTMENT_DEFAULT (2) · HOST_THEN_SENDER (1) |
| `OPTIONAL` | 13 | SENDER (5) · CAMPUS_DEFAULT (4) · DEPARTMENT_DEFAULT (4) |
| `NONE` | 4 | — (`ACCOUNT_EMAIL_CONFIRMATION`, `AUTH_PASSWORD_RESET_OTP`, `VISIT_REMINDER_HOST`, `VISIT_REQUEST_OTP`) |

`Reply-To: CONTACT` đặt ở 8 mã.

Các điểm đã kiểm:

- Mọi mã dùng `HOST` đều là email trong ngữ cảnh một chuyến thăm; `EmailContactResolver` tra Host
  theo `visit_request_campuses.current_host_user_id` của **instance**, không có nhánh nào nhận
  request id thay cho instance/campus.
- Không mã `ACCOUNT_*` hay `AUTH_*` nào dùng `HOST` — chúng dùng `CAMPUS_DEFAULT`,
  `DEPARTMENT_DEFAULT` hoặc `SUPPORT_CONTACT`, vì lúc gửi chưa có chuyến thăm nào.
- Hai thông báo gửi tới địa chỉ **vừa bị gỡ khỏi tài khoản** dùng `SUPPORT_CONTACT`, tắt campus và
  department: người nhận có thể là người lạ do gõ nhầm địa chỉ.
- `REQUIRED` mà không tra ra đầu mối thì **chặn gửi**; `OPTIONAL` thì bỏ khối, không làm hỏng lần gửi.
- `Reply-To: CONTACT` chỉ đặt ở mã có `showEmail`; lệnh lưu cấu hình từ chối tổ hợp mâu thuẫn.

## 9. Chính sách nhạy cảm

- 12/31 mã mang `HasSensitiveAction` → `SingleRecipientNoCopies`: một người một thư, cấm CC/BCC.
- Không mã nào đặt biến nhạy cảm trong subject; guard subject vẫn nguyên, không nới để test qua.
- Khối liên hệ **không** vào subject, và chỉ in các trường lấy từ `users`/`campuses`/`departments` —
  không có đường nào đưa OTP hay token vào đó.
- Bảng `email_contact_policies` cố ý **không có** cột tên/địa chỉ/điện thoại/user_id: người vận hành
  chọn *hiển thị trường nào*, không nhập *nội dung là gì*, nên không thể gán một hộp thư tự gõ cho
  người khác.

## 9b. Hai lỗi chỉ lộ ra khi chạy thật với database

Cả hai đến từ cùng một chỗ: phần chính sách liên hệ **chưa từng được chạy** với MySQL thật, vì dự án
integration test không biên dịch được kể từ commit `2f0fe8ec` (một lời gọi `BuildVariables` còn 5 tham
số sau khi biến `hostEmail` bị bỏ).

### 9b.1 Entity `EmailContactPolicy` không có ánh xạ tên bảng — **lỗi chạy thật**

Mọi entity trong dự án đều khai báo `[Table("...")]` + `[Column("...")]` tường minh. `EmailContactPolicy`
**không có cái nào**, nên EF tự đặt tên `emailcontactpolicies` với cột PascalCase — không khớp gì với
schema. Truy vấn đầu tiên dừng ở:

```
MySqlException: Table 'pems_....emailcontactpolicies' doesn't exist
```

Hệ quả nếu không sửa: `EmailContactResolver` ném lỗi ở **mọi** lần gửi, và 14 template mức `REQUIRED`
không gửi được. Đây là lỗi sản phẩm, không phải lỗi test — DI đã đăng ký resolver, nên đường chạy thật
đi đúng vào chỗ hỏng. Unit test không bắt được vì chúng dùng stub trong bộ nhớ, nơi tên bảng không cần
khớp với bất cứ thứ gì. Đã sửa bằng cách khai báo đủ `[Table]` + 16 `[Column]`.

### 9b.2 `SystemEmailDispatcher` nhận resolver là tham số **tuỳ chọn** — rủi ro còn lại

Constructor để `IEmailContactResolver? contacts = null` và `ResolveContactBlockAsync` trả `null` ngay
khi thiếu. Thiếu resolver vì thế **không báo gì**: dispatcher chỉ đơn giản không đóng góp khối, rồi
renderer từ chối với "còn placeholder chưa thay thế" — một thông báo không hề nhắc tới nguyên nhân.
Đó là cách 65 test end-to-end cùng hỏng khi harness dựng dispatcher thiếu resolver.

Hiện tại DI có đăng ký nên đường chạy thật đúng, và harness integration đã được sửa để dựng
**resolver thật + policy store thật** trên database test. Nhưng tham số vẫn là tuỳ chọn: bỏ một dòng
đăng ký DI sẽ làm 14 template ngừng gửi mà không có thông báo nào chỉ đúng chỗ. Đề xuất (chưa làm, vì
đụng khoảng 30 nơi gọi): đổi thành tham số bắt buộc.

## 10. Còn lại

- **Integration email suite chưa xanh: 566/603 đạt, còn 37 chưa đạt.** Trước phiên này con số là
  0/603 (dự án không biên dịch). Nhóm còn lại theo lớp: `VisitSetupProgressRenderTests` (13),
  `EmailTemplateRendererTests` (10), `SystemEmailDispatcherBoundaryTests` (7),
  `EmailTemplateRestoreDefaultTests` (3), `EmailTemplateConcurrencyTests` (3),
  `EmailTemplateDefaultsParityTests` (1). Phần lớn là fixture dựng renderer trực tiếp (không qua
  dispatcher) nên không có khối liên hệ, cộng thêm một số body test bị ghi đè mất placeholder. Riêng
  `EmailTemplateDefaultsParityTests` chỉ hỏng khi chạy cả namespace — chạy riêng thì 63/63 đạt, vì
  `EmailTemplateCatalogTests` sửa nội dung mẫu rồi mới khôi phục ở `Dispose`.
- `pems_db` **chưa chạy patch** — bản chạy thử làm trên bản sao. Chạy hai patch theo thứ tự ở Mục 7
  thì database phát triển mới có đủ 31 mẫu.
- **Chưa xem preview lúc chạy thật.** Server API đang chạy với `Smtp.Enabled=true`, nên không khởi động
  thêm tiến trình nào để tránh gửi thư thật.
- `appsettings.json` vẫn để `Smtp.Enabled=true` kèm mật khẩu ứng dụng Gmail commit trong repo.
- 9 mẫu legacy sau khi patch nằm ở `INACTIVE` hoặc bị xoá; nếu sau này lịch sử email được lưu trữ
  riêng, có thể xoá nốt 6 mẫu còn lại.
