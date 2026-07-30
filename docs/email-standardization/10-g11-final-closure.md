# 10 — G11 final closure: Restore Default, concurrency, Reply All

> Đóng ba khoản còn mở sau G12 (`R-165`, `R-166`, `R-167`), mở rộng G11-H sang các đường chưa có
> bằng chứng, và đóng một lỗ phân quyền tìm thấy khi audit.
>
> Đọc cùng: `09-g12-contact-guard-and-template-contract.md` (contract biến + catalog cố định),
> `06-deployment-readiness-runbook.md` (thứ tự deploy).

---

## 1. Vì sao ba khoản kia chưa đóng được ở G11

Cả ba đều là **thiếu, không phải sai**. Không có gì hỏng khi đó; có ba chỗ mà lời hứa của tính năng
lớn hơn thứ đã dựng:

| Khoản | Thực trạng cuối G11 |
|---|---|
| `R-165` | Catalog cố định (không tạo/xoá) nhưng **không có đường quay lại**. Người vận hành sửa hỏng một mẫu thì cách duy nhất là nhờ người có quyền DB chạy lại `02_sync_templates.sql`. Đó không phải chức năng. |
| `R-166` | Token concurrency là `updated_at` — `DATETIME` không có phần giây lẻ. Hai lần lưu **trong cùng một giây** ghi ra cùng một mốc, so sánh bằng nhau, lần sau đè lần trước im lặng. Điểm mù nằm đúng chỗ hai người thật sự đụng nhau. |
| `R-167` | Một lần đỏ không tất định ở reminder race test, chưa truy được nguyên nhân. |

---

## 2. Restore Default — nguồn mặc định ở đâu

### 2.1. Ràng buộc

Chủ dự án chốt: mặc định phải đến từ **canonical backend registry có thẩm quyền**, KHÔNG lấy nội dung
DB hiện tại, KHÔNG coi "chạy lại SQL" là giải pháp, và **không tạo hai nguồn sự thật dễ drift**. Cùng lúc:
ưu tiên giải pháp **ít schema nhất**.

Hai ràng buộc đó kéo ngược nhau, và cách giải quyết nằm ở chỗ phân biệt **nơi soạn** với **nơi đọc**.

### 2.2. Quyết định

Nội dung 30 mẫu được **soạn ở canonical SQL seed và chỉ ở đó** (quyết định D-01 — đó là lý do
`SystemEmailTemplates` cố tình không chứa một câu tiếng Việt nào). Chép tay 30 × 4 trường nội dung vào C#
sẽ tạo **tác giả thứ hai** cho cùng một đoạn văn, và hai bên sẽ lệch ngay lần đầu ai đó sửa một lỗi chính tả
ở một bên.

Nên:

```
canonical SQL seed  ──(import sạch)──▶  MySQL  ──(export)──▶  email-template-defaults.json
       (nơi soạn duy nhất)                                      (embedded resource, backend đọc)
                         ▲                                                │
                         └──────── parity test so lại mỗi lần chạy ───────┘
```

* **Trích bằng MySQL, không bằng parser tự viết.** Seed dùng `CONCAT('...','...')` với escape của MySQL.
  Để chính MySQL đánh giá biểu thức đó là cách duy nhất chắc chắn đúng; một parser SQL tự viết sẽ sai
  escape sớm hay muộn, và sai kiểu đó **không lộ ra** — nó chỉ làm nội dung phục hồi khác nội dung gốc.
* **Embedded resource, không phải bảng mới.** Không thêm cột, không thêm bảng, không thêm seed. Restore
  chạy được ở mọi môi trường mà không cần đường dẫn file.
* **Không thành nguồn thứ hai** vì `EmailTemplateDefaultsParityTests` dựng lại từ bản import sạch mỗi lần
  chạy integration và so **từng trường từng mẫu**. Sửa seed mà quên sinh lại resource → test đỏ, không
  phải drift im lặng.

### 2.3. Sáu trường — ánh xạ thật trong schema này

Yêu cầu nêu sáu trường `subject_vi/en`, `body_html_vi/en`, `body_text_vi/en`. **Schema PEMS không có cặp
html/text tách rời**: bảng có `subject_vi`, `body_vi`, `subject_en`, `body_en` cộng một cột phân biệt
`body_format` (`PLAIN_TEXT | HTML`, hiện 30/30 là HTML). Sáu trường sửa được thật sự là:

```
name · description · subject_vi · subject_en · body_vi · body_en
```

Đúng bằng `EmailTemplateContracts.EditableFieldNames`. Restore phục hồi **đúng sáu trường update sửa
được** — không hơn: nếu restore hẹp hơn update, người vận hành sẽ có trường sửa hỏng mà không có đường về.

`body_format`, `variables_text`, `template_code`, `purpose`, `campus_id`, `status` **không** do người vận
hành sửa nên không thể lệch; restore không đụng vào. `variables_text` được ghi lại từ registry như update
làm, vì nó là **hình chiếu** của registry chứ không phải nội dung.

### 2.4. Fail-closed

Nội dung mặc định được **validate theo contract trước khi ghi**. Nếu một lần sửa seed sau này đưa vào một
biến không caller nào cấp, restore **từ chối và nói biến nào sai**, thay vì ghi đè nội dung của người vận
hành bằng thứ chắc chắn hỏng.

---

## 3. Sinh lại `email-template-defaults.json`

Chạy khi và chỉ khi nội dung seed trong canonical SQL đổi.

1. Import canonical SQL vào **một database dùng-một-lần** (không bao giờ `pems_db`).
2. Export 30 hàng ra JSON, thứ tự trường cố định, sắp theo `templateCode`:

```sql
SELECT JSON_ARRAYAGG(JSON_OBJECT(
  'templateCode', template_code, 'name', name, 'description', description,
  'subjectVi', subject_vi, 'bodyVi', body_vi,
  'subjectEn', subject_en, 'bodyEn', body_en, 'bodyFormat', body_format))
FROM (SELECT * FROM email_templates ORDER BY template_code) t;
```

3. Ghi vào `backend/PEMS.Application/Emails/Common/Assets/email-template-defaults.json`, UTF-8 **không
   BOM**, thụt 2 dấu cách, kết thúc bằng một dòng trống.
4. Drop database vừa tạo.
5. Chạy `EmailTemplateDefaultsParityTests` — đây mới là bước chứng minh, không phải bước 3.

Thứ tự trường và thứ tự hàng **cố định** để file tái lập được từng byte; nếu không, parity test không phân
biệt được "nội dung đổi thật" với "serialise khác đi".

---

## 4. Optimistic concurrency

### 4.1. Cột

```sql
revision INT UNSIGNED NOT NULL DEFAULT 1
```

Additive, không index, không trigger. Migration: `docs/database/scripts/email_template_revision/`.

### 4.2. Vì sao không dùng concurrency token của EF

EF đặt giá trị **nó vừa đọc** vào mệnh đề WHERE. Giá trị phải kiểm tra là giá trị **người vận hành đang
nhìn thấy**. Người mở revision 4, để form đó trong lúc đồng nghiệp lưu thành 5, rồi bấm Lưu — phải bị từ
chối; với token của EF thì **không**, vì lần đọc mới của handler đã thấy 5.

Nên cả update lẫn restore đi qua **một** hàm ghi duy nhất, phát một câu UPDATE có điều kiện:

```sql
UPDATE email_templates
   SET ..., revision = revision + 1, updated_at = ?, updated_by = NULLIF(?, 0)
 WHERE email_template_id = ? AND revision = ?
```

`rowsAffected = 0` **chính là** xung đột. Không có khoảng trống giữa lúc quyết định và lúc ghi, vì không có
lúc quyết định tách rời — database quyết.

> Ghi chú kỹ thuật: tham số NULL đi qua `NULLIF(?, '')` chứ không phải `DBNull.Value` — bộ dựng tham số
> raw-SQL của EF không có store type cho `DBNull` và ném lỗi trước khi câu lệnh được gửi đi.

### 4.3. Frontend

Màn hình giữ `revision` **đúng như server trả**, gửi lại khi lưu và khi restore, và cập nhật từ response.
Khi 409: báo dữ liệu đã bị người khác đổi và yêu cầu tải lại — **không tự thử lại**. Thử lại mạnh hơn sẽ đè
đúng thay đổi mà cảnh báo đang nói tới.

---

## 5. G11-H — những gì audit tìm ra

### 5.1. Một đường vòng qua phân quyền còn mở

G11-I đã chuyển các thao tác ghi trên `EmailTemplatesController` sang `[RoleAuthorize(Ho)]` theo
PERMISSION_MATRIX §5.5. Nhưng `EmailsController` **còn hai route trùng lặp**:

```
POST /api/Emails/updateemailtemplate      ← sửa được nội dung mẫu hệ thống
POST /api/Emails/createemailtemplate
```

và chúng chỉ thừa hưởng `[RoleAuthorize]` cấp class liệt kê **năm** vai trò. Nghĩa là **Staff, Department và
Staff Leader vẫn sửa được nội dung mẫu hệ thống** qua một route màn hình không bao giờ hiện. Ẩn nút không
thay đổi gì; có một cánh cửa thứ hai đang mở.

Không có caller nào — frontend không gọi, test không gọi. Đã gắn `[RoleAuthorize(Ho)]` cho từng action và
đánh dấu DEPRECATED. **Không xoá route** — bỏ một route là quyết định của chủ dự án.

### 5.2. Reply All chưa tồn tại

Không có ở đâu trong codebase. Đã dựng, và nguy hiểm lớn nhất của nó là điều hiển nhiên nhất: cách cài đặt
tự nhiên là đọc các hàng recipient của thư gốc rồi chép sang thư mới — mà các hàng đó **bao gồm cả BCC**.
Làm vậy là công bố cho mọi người nhận hiện danh biết ai đã được đưa vào âm thầm.

Nên:

* **BCC của thư gốc không phải là input của hàm lập danh sách.** Không lọc muộn, không "loại ở cuối" —
  các hàng BCC bị bỏ **trước khi** `ReplyRecipientPlanner` được gọi. Không có thứ tự nào làm sai được.
* **Client gửi một chế độ, không gửi danh sách địa chỉ.** Nếu client gửi danh sách, nó đang khẳng định "đây
  là những người đã ở trên thư gốc" — và một client khẳng định được recipient thì khẳng định được cả người
  vốn ở BCC. Server tự đọc từ thư gốc.
* **Thứ tự ưu tiên khi trùng: TO > CC > BCC.** Validator từ chối một địa chỉ nằm ở hai nhóm (đúng — một thư
  không thể vừa hiện vừa giấu cùng một người), nên trùng phải được giải trước đó; giải **lên trên** giữ
  người nhận ít nhất cũng hiện như thư gốc đã hiện.
* Reply và Reply All **đặt chỗ dưới hai operation code khác nhau**, nên dùng lại một key giữa hai bên là hai
  reservation độc lập chứ không phải một lần "đã gửi rồi" sai.

### 5.3. Compose và reply vào diện idempotency

`§7.4` đòi recipient set đã normalize tham gia fingerprint. Sáu route report/invoice **không mang** TO/CC/BCC
— recipient của chúng do backend suy từ id đã nằm trong fingerprint. Đường duy nhất **client chọn người
nhận** là compose và reply, và đó cũng là chỗ một lần bấm nhầm gửi thêm một bức thư người-viết tới người
thật.

Nên compose (`SendEmailCommand`) và reply (`ReplytoEmailCommand`) nay là idempotent send. Recipient được
chuẩn hoá **trim → lowercase → bỏ trùng → sắp xếp** trước khi vào fingerprint:

* đổi thứ tự chip / hoa-thường / khoảng trắng → **cùng** request (retry không thành thư thứ hai);
* thêm/bớt/chuyển nhóm một địa chỉ → **khác** request (bị từ chối thay vì trả lời "đã gửi rồi" cho người
  vừa thêm một người nhận, khiến người đó không bao giờ nhận được gì).

Số command idempotent: 6 → **8 kiểu / 9 operation code** (`ReplytoEmailCommand` trả về hai code tuỳ chế độ).

> `SendEmailDraftCommand` **không** dùng fingerprint: nó đã có cơ chế mạnh hơn — chuyển `DRAFT → SENT` bằng
> một câu UPDATE có điều kiện, do database phân định. Giữ nguyên, không chồng hai cơ chế lên một đường.

### 5.4. `/api/Emails/sendemail` là route API-only

Không màn hình nào gọi — compose trong UI lưu draft rồi gửi draft. Vẫn được bảo vệ, vì một route gửi thư
không được bảo vệ thì vẫn là không được bảo vệ, bất kể UI hiện tại có dùng hay không.

---

## 6. Reminder concurrency — nguyên nhân và cách đóng

### 6.1. Không phải lỗi sản phẩm

`VisitReminderDispatchIdempotencyTests.Two_workers_racing_the_same_reminder_produce_one_set_of_messages`
đỏ **một lần** ở lần chạy full đầu tiên của G12, xanh ở lần 2 và 3. Cơ chế claim là một câu UPDATE có điều
kiện; nó chưa bao giờ sai.

### 6.2. Nguyên nhân: fixture ghi vào hàng dùng chung

`SeedAsync` chọn **"Staff Leader ACTIVE đầu tiên"** — một vị ngữ **không duy nhất** — rồi **ghi đè email của
hàng users đó** bằng marker của suite trong suốt thời gian test chạy, và trả lại ở cleanup.

Database integration là **một, dùng chung**, và xUnit chạy **các test class song song**. Nên trong lúc một
test ở đây đang chạy, **mọi suite khác đọc Staff Leader đó đều thấy** `batch8-idempotency@partner.example.com`.
Có gây hại hay không phụ thuộc lớp nào tình cờ chồng lên nhau — đúng hình dạng của một lỗi hiện một lần rồi
biến mất.

Suite còn mượn cả **user 1 (`admin@fpt.edu.vn`)** làm participant và viết đè email của quản trị viên.

### 6.3. Cách sửa

Suite **tự tạo user của mình** và xoá lúc dọn; không ghi vào hàng nào nó không tạo ra.

* Host là user riêng, `sub_role = 'STAFF'` — **cố ý không phải LEADER**: mỗi campus phải có đúng một Staff
  Leader (BR-86-19/20), thêm người thứ hai sẽ làm campus thành cấu hình sai và làm hỏng mọi test tạo lịch
  tiếp khách trên campus đó. Trigger gán host chỉ đòi các trường host có giá trị, không đòi vai trò nào.
* `HostAssignedBy` / `DecidedBy` vẫn là Staff Leader thật (chỉ **đọc**) — đúng hình dạng production.
* Participant cũng là user riêng, không mượn `admin`.
* Không tắt parallel của suite nào: nguyên nhân là hàng dùng chung, không phải song song.

### 6.4. Gate

* `Racing_workers_produce_one_set_of_messages_on_every_one_of_twenty_attempts` — **20/20** lần liên tiếp.
* `Seeding_never_changes_a_user_it_did_not_create` — chụp toàn bộ `users` trước và sau khi seed, so từng
  hàng. Ghim thành test vì một dòng comment sẽ không ngăn được việc này quay lại.
* Assertion trạng thái reminder được kiểm **trước** số lượng thư, để "không vào batch" hiện ra đúng như thế
  thay vì hiện ra thành "0 thư" — `DispatchDueAsync` quét 50 reminder đến hạn cũ nhất trên toàn database,
  nên trên DB dùng chung điều đó là khả năng thật và đáng gọi tên trong assertion.
* Ba lần chạy full integration không filter, liên tiếp: **1277 / 1277** mỗi lần.

---

## 7. Số đo

| Gate | Trước lượt này | Sau |
|---|---|---|
| Build | 0 error / 208 warning | **0 error / 208 warning** |
| Unit | 1820 | **1853** (+33) |
| Architecture | 14 | **14** |
| Integration | 1242 | **1277** (+35) |
| Frontend | 957 / 71 file | **972 / 71 file** (+15) |
| `tsc --noEmit` | 0 | **0** |
| `vite build` | 0 | **0** |

Canonical SQL: `edf88cbd…6d0bf29c` → **`16010f54de2282aa0cbaa11909000b74c59d27b7184715f72a7875bdb854f2f0`**.
Chỉ một cột additive; 83 bảng / 32 trigger / 254 FK / 30 template không đổi, contact guard vẫn **0 / 0**.

---

## 8. G11-H — ma trận truy vết 16 đường

Lập lại từ code, không copy từ báo cáo trước. Cột "Đường" giống nhau ở hai bảng.

### 8.1 Vào — màn hình, endpoint, handler, phân quyền

| Đường | Frontend entry point | API endpoint | Command / handler | Authorization |
|---|---|---|---|---|
| Compose | `EmailManagement.tsx` nút "Soạn email" → `EmailComposeModal` | `POST /api/Emails/drafts` | `CreateEmailDraftCommandHandler` | class `[RoleAuthorize]` 5 role + owner trong handler |
| Preview (compose) | `EmailComposeModal` nút "Xem trước" | **không có** — dựng trong browser | `handlePreview` (client) | n/a |
| Preview (mẫu) | `TemplateManagement.tsx` | `POST /api/email-templates/preview` | `PreviewEmailTemplateQueryHandler` | class 5 role |
| Save draft / autosave | `EmailComposeModal` `scheduleSave` (debounce 1200ms) | `POST /api/Emails/drafts` · `PUT /api/Emails/drafts/{id}` | `CreateEmailDraft…` · `UpdateEmailDraftCommandHandler` | owner-scoped trong handler |
| Open draft | `DraftsPanel` (tab "Nháp") → `initialDraftId` | `GET /api/Emails/drafts/{draftId}` | `GetEmailDraftQueryHandler` | owner-scoped |
| Edit draft | `EmailComposeModal` sau khi hydrate | `PUT /api/Emails/drafts/{draftId}` | `UpdateEmailDraftCommandHandler` | owner + status `DRAFT` |
| Send draft | `EmailComposeModal` "Xác nhận gửi" | `POST /api/Emails/drafts/{draftId}/send` | `SendEmailDraftCommandHandler` | owner + claim `DRAFT→SENT` |
| Direct send | **không có caller** (đo trên toàn `src/`) | `POST /api/Emails/sendemail` | `SendEmailCommandHandler` | class 5 role + `Idempotency-Key` bắt buộc |
| Reply | `SentEmailDetail.tsx` "Phản hồi" → `ReplyComposer` | `POST /api/Emails/replytoemail` | `ReplytoEmailCommandHandler` (`ReplyAll=false` do controller ép) | quan hệ envelope trong handler |
| Reply All | `SentEmailDetail.tsx` "Phản hồi tất cả" (`data-testid="reply-all"`) | `POST /api/Emails/replyalltoemail` | cùng handler (`ReplyAll=true` do controller ép) | cùng kiểm tra như Reply |
| Retry / idempotency replay | `useIdempotentSend` (`keyFor`/`complete`/`attemptIsOver`) | mọi route send, qua header `Idempotency-Key` | `EmailSendIdempotencyBehaviour` (MediatR pipeline) | như route gốc |
| Report | `HoReportManagement` · `StaffLeaderReportManagement` ×2 · `DeptReportManagement` | 4 × `POST …/send-*-report` | 4 command → `ReportEmailSender` | `[RoleAuthorize(Ho)]` / `(StaffLeader)` / `(DepartmentLead)` |
| Invoice | **không có caller** — R-105 BLOCKED | `…/departments/{id}/send-invoice` · `…/dept-leader-report-v2/send-invoice` | `SendStaffLeaderDeptInvoice…` · `SendDeptLeaderInvoiceToStaffLeader…` | `(StaffLeader)` / `(DepartmentLead)` |
| Logistics | màn Chuẩn bị / phân công của department | `PrepareVisitLogistics` · `AssignRequestAssignee` · `ProposeRequestChange` | 3 handler → `SystemEmailDispatcher` | scope department trong handler |
| Visit-related / manual | mời người tham dự · nhắc lịch · nhắc quyết toán · phân công staff | 4 handler + hosted job | `InviteVisitParticipant` · `VisitReminderDispatchService` · `RemindExpenseReports` · `AssignDepartmentStaff` | Host / scope department |
| History / detail API | `SentEmailDetail.tsx` · `EmailManagement.tsx` danh sách | `GET /api/Emails/viewemail` · `GET /api/Emails/viewemaillist` | `ViewEmailQueryHandler` · `ViewEmailListQueryHandler` | `SentEmailAccess.Resolve` (quan hệ, KHÔNG theo role) |

### 8.2 Ra — người nhận, lưu trữ, dispatcher, MIME, hiển thị lại, bằng chứng

| Đường | Chuẩn hoá người nhận | Persistence | Dispatcher | MIME / envelope | History / redaction | Test / evidence |
|---|---|---|---|---|---|---|
| Compose | `types/recipients.ts` (bản sao của `EmailRecipientValidator`), 3 nhóm rời | — | — | — | — | Journey A · `EmailComposeModal.recipients.test.tsx` |
| Preview (compose) | dùng lại state 3 nhóm; `preview-TO/CC/BCC` | — | — | — | tác giả xem cả BCC của chính mình | Journey A |
| Preview (mẫu) | không có người nhận | — | — | — | — | `EmailPreviewCoverageTests` · `EmailPreviewSampleModeTests` |
| Save draft / autosave | `EmailDraftWriter.ValidateRecipients(requireTo: false)` | `email_drafts` + `email_draft_recipients.recipient_type` | — | — | — | Journey A (đọc thẳng DB) · `EmailDraftRecipientContractTests` |
| Open draft | `envelopeFromDraft` map `recipient_type` → nhóm; loại lạ ⇒ **chặn**, không đoán | đọc | — | — | owner-scoped | Journey A (reload rồi mở lại) · `EmailDraftListAuthorizationTests` |
| Edit draft | như autosave | ghi lại đúng 3 nhóm | — | — | — | Journey A |
| Send draft | `ValidateRecipients(requireTo: true)` **lại lúc gửi** | `sent_emails` + `sent_email_recipients` (TO/CC/BCC) | `ManualEmailSender` → `IEmailService` | **1 message** cho cả envelope; `TemplateCode = null` | `delivered_at` để NULL | Journey A · `ManualEmailPipelineTests` |
| Direct send | `EmailRecipientValidator.Validate` | như trên | `ManualEmailSender` | như trên | như trên | `EmailSendIdempotencyTests` |
| Reply | TO do server suy từ `originalEmailId`; CC/BCC là của người trả lời | như trên | `ManualEmailSender` | thêm `In-Reply-To` / `References` | như trên | Journey B · `ReplyRecipientPlannerTests` |
| Reply All | `ReplyRecipientPlanner.Plan(All)`; **BCC bản gốc bị lọc TRƯỚC khi vào planner**; ưu tiên TO > CC > BCC | như trên | `ManualEmailSender` | như trên | như trên | Journey C · `ReplyAllJourneyTests` |
| Retry / replay | tập người nhận đã normalize nằm trong fingerprint | `email_send_idempotency` | không gửi lại — trả lời cũ | — | — | `EmailSendIdempotencyContractTests` · `EmailSendRecipientFingerprintTests` |
| Report | **chỉ 1 `To`** — `SystemEmailRequest` không có field Cc/Bcc | `sent_emails` + 1 hàng TO + `sent_email_attachments` | `SystemEmailDispatcher` | 1 message, có `TemplateCode` ⇒ chịu `EmailRecipientPolicyEnforcer` | scope theo đối tượng | `ReportEmailEndToEndTests` |
| Invoice | như Report | như Report | như Report | như Report | như Report | `ReportInvoiceRouteTests` |
| Logistics | như Report | như Report | như Report | như Report | như Report | `LogisticsEmailEndToEndTests` |
| Visit-related / manual | như Report | như Report | như Report | như Report | như Report | `VisitReminderEmailEndToEndTests` · `ParticipantInvitationLinkageTests` |
| History / detail API | — | đọc | — | — | `SentEmailAccess.FilterRecipients`: Sender thấy tất; TO/CC và linked-object thấy TO+CC; BCC thấy TO+CC + hàng của chính mình | Journey A2 · `SentEmailAccessTests` · `SentEmailHistoryAuthorizationTests` |

### 8.3 Bốn khoản "không tồn tại theo thiết kế" — chứng minh, không ghi N/A

1. **Compose preview không có endpoint.** Nhánh `showPreview` của `EmailComposeModal` chỉ đọc lại state; không có lời gọi mạng nào trong nhánh đó. Preview là bản nháp của chính tác giả, nên nó hiển thị cả BCC — điều phải giữ kín là BCC không tới người nhận khác và không vào history của người khác, và đó là việc của server.
2. **`POST /api/Emails/sendemail` không có caller frontend.** Đo trên toàn `frontend/pems-react/src`: chỉ có định nghĩa trong `emailsApi.ts`, không nơi nào gọi. Compose luôn đi qua draft. Route vẫn được bảo vệ vì một route send không được bảo vệ thì vẫn là không được bảo vệ, dù UI hiện tại có dùng hay không.
3. **Đường system (report / invoice / logistics / visit) không thể mang CC/BCC.** Không phải vì chưa ai truyền, mà vì `SystemEmailRequest` **không có field Cc/Bcc** — kiểu dữ liệu không cho phép. Đây là chứng minh cấu trúc, mạnh hơn mọi bài test.
4. **Không có đường thứ ba xuống SMTP.** Toàn codebase chỉ có **hai** nơi gọi `IEmailService`: `ManualEmailSender.cs:144` (compose / draft / reply / reply-all) và `SystemEmailDispatcher.cs:137` (30 lời gọi `SystemEmailRequest`). Nên `EmailRecipientPolicyEnforcer` không có đường vòng nào; và `FileSinkEmailService.Gate` áp **đúng ba** kiểm tra như `EmailService`, được ghim bởi `FileSinkPolicyParityTests` — nếu test double lỏng hơn thứ nó thay thế thì bằng chứng real-stack sẽ nói về một hệ thống không tồn tại.

---

## 9. Ba journey real-stack — bằng chứng

Chuỗi thật, không mock mạng, không SMTP thật, không email thật:

```
Chromium thật → React (Vite) thật → .NET API thật (Testing, E2E auth fail-closed)
  → MySQL disposable `pems_e2e_realstack` (tạo từ canonical SQL) → dispatcher thật → history thật
```

Chạy bằng `frontend/pems-react/scripts/run-realstack-e2e.mjs`; spec ở
`frontend/pems-react/tests-realstack/email-envelope.realstack.spec.ts`.

**Vì sao cần browser khi các suite API đã xanh.** Mọi tính chất ở đây là tính chất của một **lần trao tay**: màn hình giữ ba nhóm, endpoint draft lưu đúng nhóm, mở lại đặt đúng chỗ, dispatcher nhận vẫn còn rời nhau. Một integration test post payload đã đúng dạng thì chứng minh nửa phía server và **giả định** nửa còn lại — nơi lỗi thật xảy ra. Một CC bị UI gộp thành TO trên đường ra là vô hình với nó, vì payload nó post chưa từng bị gộp.

**Danh tính dùng trong journey** — bốn hòm thư khác nhau, không phải ba: TO, CC, BCC phải là ba người, và người trả lời phải KHÁC người gửi (`SentEmailAccess.CanOfferReply` từ chối trả lời thư của chính mình).

| | Vai | Hòm thư |
|---|---|---|
| Người gửi | HO | `ho@fpt.edu.vn` |
| TO | Staff Leader HN | `staff.leader.hn@fpt.edu.vn` |
| CC | Dept Leader HN | `dept.leader.hn@fpt.edu.vn` |
| BCC | Facilities Leader HN | `facilities.leader.hn@fpt.edu.vn` |
| Người ngoài | Staff Leader HCM | `staff.leader.hcm@fpt.edu.vn` |

### 9.1 Journey A — compose → preview → draft → mở lại → sửa → gửi

Chứng minh, theo thứ tự spec thực hiện:

* Địa chỉ sai định dạng bị từ chối tại field, **không** thành chip.
* Nhập `   STAFF.LEADER.HN@FPT.EDU.VN   ` (thừa khoảng trắng, viết hoa) → ra **một** chip bình thường.
* Nhập lại cùng hòm thư khác hoa-thường → báo trùng, không thành người nhận thứ hai.
* Cùng địa chỉ đó ở CC → "chỉ được thuộc một mục", CC vẫn rỗng.
* Xoá chip TO duy nhất rồi bấm Xem trước (CC/BCC vẫn còn) → bị từ chối "ít nhất một người nhận", **không** vào được preview. "Có vài người nhận" không phải "envelope hợp lệ".
* Draft lưu đúng nhóm: đọc thẳng `email_draft_recipients` — 1 TO, 1 CC, 1 BCC, đúng 3 hàng.
* Preview hiện đủ ba nhóm, còn tách rời.
* **Reload cả trang**, mở lại từ tab Nháp → ba nhóm về đúng chỗ (CC không thành TO, BCC không thành CC). Reload là cố ý: thứ được phục hồi phải đến từ server, không phải từ state browser còn giữ.
* Sửa tiêu đề rồi gửi → dispatcher nhận `to=[leader]`, `cc=[dept_leader]`, `bcc=[facilities_leader]`.
* Lưu trữ: `sent_email_recipients` đúng ba loại. Địa chỉ giữ **nguyên hoa-thường như người nhập** (`STAFF.LEADER.HN@FPT.EDU.VN`) — so sánh thì không phân biệt hoa thường, lưu thì có; cái bị gấp về chữ thường là bản trao cho transport.
* `delivered_at` **NULL** và không hàng nào `DELIVERED`: provider nhận không phải là hòm thư nhận, và PEMS không có webhook nào để biết điều đó.
* Draft chuyển `SENT` và trỏ đúng `sent_email_id` nó trở thành.

### 9.2 Journey A2 — history theo từng người xem

Đọc `GET /api/Emails/viewemail` bằng bốn danh tính thật:

| Người xem | Kết quả |
|---|---|
| Người gửi (HO) | thấy cả 3 — họ chọn BCC, che đi là che chính việc họ làm |
| Người nhận TO | chỉ TO + CC. Toàn bộ JSON trả về **không chứa** địa chỉ BCC — không đếm, không cờ, không tổng |
| Người bị BCC | TO + CC + hàng của chính mình, `recipientType = BCC` |
| Người ngoài (Staff Leader HCM) | **403** |

Người ngoài là Staff Leader HCM có lý do: từ chối phải đến từ **quan hệ** (không gửi, không được gửi tới, không có đối tượng liên kết), nên người xem phải vượt qua role gate của controller trước — một VISITOR bị chặn vì sai role và không chứng minh gì về luật này.

### 9.3 Journey B — Reply

* Màn hình người nhận TO **không hề** nhắc địa chỉ BCC ở bất kỳ đâu (kiểm trên toàn `body` của trang).
* Nút "Phản hồi" xuất hiện do server quyết (`canReply`), không do client đoán.
* Không có field TO nào để nhập — TO là quyết định của server, hiện ra dạng đọc-thôi kèm nhãn "hệ thống xác định".
* Thư trả lời: `to=[ho]`, `cc=[]`. Reply **không** kế thừa CC của bản gốc, và không hồi sinh BCC.
* Đúng **một** hàng người nhận trong history; `provider_thread_id` khác NULL nên chuỗi hội thoại là dữ kiện chứ không phải suy đoán.

### 9.4 Journey C — Reply All

* Quan sát **request thật browser gửi**: đúng route `replyalltoemail`, và body **không có** `to`, `cc`, `bcc` nào. Điều được kiểm không chỉ là "đúng người nhận" mà là "client chưa từng nêu tên họ" — client nêu được tên thì nêu được cả người từng ở BCC, và server không có cách nào phân biệt điều đó với một CC.
* Kết quả: `to=[ho]` (người gửi gốc + TO gốc, trừ chính mình), `cc=[dept_leader]` (CC gốc được mang theo, BCC gốc thì không).
* Người trả lời không tự gửi cho mình, ở cả TO và CC.
* Địa chỉ BCC không xuất hiện ở đâu trong thư trả lời.
* Quét toàn bộ history của marker: địa chỉ BCC được gửi tới **đúng một lần**, trên bản gốc, loại `BCC`, và hàng đó không thuộc thư `Re:`.
* `replyIdFor` khẳng định có **đúng một** bản gốc + **một** thư trả lời — "hàng mới nhất trông giống reply" sẽ pass y như vậy khi có hai thư trả lời, tức đúng hình dạng của một lần gửi đôi.

---

## 10. Hai nhân chứng: file-sink và SMTP pickup

"Đã được gửi tới" và "không ai thấy" là hai tính chất **ngược nhau**; một hiện vật thể hiện được cả hai thì chẳng chứng minh gì cả. Nên cùng bộ journey chạy hai chế độ:

| | file-sink (mặc định) | SMTP pickup (`PEMS_E2E_SMTP_PICKUP=1`) |
|---|---|---|
| Dispatcher | `FileSinkEmailService` | `EmailService` thật, `SpecifiedPickupDirectory` |
| Hiện vật | 1 dòng JSON / thư, ba nhóm rời | 1 file `.eml` thật |
| Trả lời được | BCC **đã được gửi tới** | BCC **không có trong header nào** |
| Mạng | không | không — pickup ghi file, không mở kết nối |

**`X-Receiver` không phải rò rỉ.** Qua kết nối thật, envelope đi trong `MAIL FROM` / `RCPT TO` và không bao giờ vào thư. Pickup không có kết nối, nên .NET ghi đúng envelope đó thành `X-Sender` + một `X-Receiver` mỗi người nhận ở **đầu file**, để dịch vụ pickup đọc rồi bỏ đi trước khi truyền. Vì vậy một BCC xuất hiện trong khối đó là **đúng** — đó là cách viết `RCPT TO` của chế độ pickup, và nếu nó vắng mặt thì nghĩa là BCC không hề được gửi. Helper tách khối này ra khỏi thư: `raw` là thư người nhận thật sự nhận, `envelopeRcpt` là envelope transport. Lẫn hai thứ sẽ đánh trượt một hệ thống đang chạy đúng; bỏ qua phân biệt đó sẽ cho một header `Bcc:` thật lọt qua.

Nhờ tách đúng, chế độ pickup trả lời được **cả hai**: `bcc` được suy ra bằng chính cách một máy chủ nhận có thể suy — *được transport gửi tới, không nằm trong header nào* — và đó là phát biểu mạnh hơn việc đọc một field `Bcc`, vì không có field `Bcc` nào để đọc.

**Giới hạn phải nói rõ:** chế độ pickup chỉ dùng cho spec email. Các spec OTP / link đọc OTP từ sink JSON, nên chạy chúng ở chế độ pickup sẽ đỏ — đúng như thiết kế, không phải lỗi.

---

## 11. Khiếm khuyết tìm được trong lượt này

**`queryDb` cắt dòng bằng `'\n'` thay vì `/\r?\n/`.** `mysql.exe` kết thúc mỗi dòng bằng CRLF, nên khi cắt bằng `'\n'`, **cột cuối của mọi hàng trừ hàng cuối** còn dính `\r` — `.trim()` trên toàn output chỉ dọn được hàng cuối. Kết quả: helper chính xác với câu trả lời một hàng và **âm thầm sai** với câu trả lời nhiều hàng. Cụ thể `recipient_type` trả về `"TO\r"` ở mọi hàng trừ hàng cuối, nên lọc `=== 'TO'` không thấy gì trong khi dữ liệu hoàn toàn đúng.

Đây là defect trong **hạ tầng test** (`tests-realstack/departmentRealstackHelpers.ts`, có sẵn trên Dev), không phải trong production. Nhưng nó làm yếu assertion mà không báo, nên đã sửa ở cả hai file. Không có spec nào đang xanh nhờ bug này: các spec cũ dùng `scalar()` (một giá trị, đúng là hàng cuối).

---

## 12. G11-I mở lại — màn quản lý mẫu chỉ tải 10/30

**Chủ dự án phát hiện; xác minh lại thì đúng.** Đây là defect production, và nó vô hiệu hoá phần lớn G11-I trên thực tế.

`TemplateManagement.tsx` gọi `emailsApi.getEmailTemplateList()` **không tham số**. `ViewEmailTemplateListQuery` mặc định `Page = 1, PageSize = 10`, danh mục có **30** mẫu → màn hình chỉ nhận 10 mẫu đầu.

**Vì sao không phải chuyện phân trang cho đẹp:**

* Màn này **lọc phía trình duyệt** (`data.filter(...)`), nên tìm một trong 20 mẫu còn lại trả về *"Không tìm thấy mẫu email nào"* — đọc thành **"không có mẫu đó"** chứ không phải "chưa tải".
* Màn này không có điều khiển phân trang, nên 20 mẫu kia không có đường nào tới.
* Đây là **surface duy nhất** có "Chỉnh sửa" và "Phục hồi mặc định", cả hai đều **chỉ HO**. Nên 20 mẫu hệ thống không ai sửa được và không ai phục hồi được — đúng con đường sửa chữa mà danh mục cố định (G11-I) sinh ra để bảo đảm. Một mẫu bị hỏng nội dung: không tạo mới được, không xoá được, và giờ cũng không phục hồi được.

**Vì sao lượt kiểm chứng trước không bắt được.** Bằng chứng G11-I là unit + component test, và component test **mock** `getEmailTemplateList` — mock trả về đủ số mẫu bất kể tham số, nên phân trang thật không bao giờ tham gia. Ba journey real-stack chạy trên màn *soạn email*, nơi `EmailComposeModal` đã truyền sẵn `pageSize: 100`. Không surface nào trong bộ bằng chứng chạm vào lời gọi sai.

### 12.1 Cách sửa

```ts
const CATALOG_PAGE_SIZE = 200;   // trần rộng, không phải đúng 30
const res = await emailsApi.getEmailTemplateList({ page: 1, pageSize: CATALOG_PAGE_SIZE });
```

Cộng thêm **kiểm tra đầy đủ**: nếu `totalItems` server báo lớn hơn số mẫu nhận được, màn hình hiện cảnh báo `role="alert"` (`data-testid="catalog-truncated"`) nói rõ *"Chỉ tải được N/M mẫu… danh sách CHƯA đầy đủ"*, thay vì render một tập con trông như đầy đủ. Danh sách vẫn hiện — giấu đi thì mất luôn phần đã tải được — nhưng được dán nhãn thiếu, để *"không tìm thấy"* không bao giờ bị đọc nhầm thành *"không tồn tại"* nữa.

Trần 200 thay vì đúng 30 là cố ý: thêm mẫu ở release sau không bị ẩn im lặng, và nếu danh mục vượt 200 thì kiểm tra đầy đủ sẽ nói ra.

### 12.2 Test — và bằng chứng test bắt được lỗi

5 test mới trong `TemplateManagement.test.tsx`, mục *"the whole catalog is loaded"*:

| Test | Khẳng định |
|---|---|
| `asks for a page big enough…` | request phải mang `pageSize >= 30` |
| `renders every template…` | mẫu thứ **30** có mặt (mẫu mà PageSize=10 đánh mất) |
| `finds a template that a ten-row page would never have loaded` | tìm `TEMPLATE_27` ra kết quả, không ra "Không tìm thấy" |
| `says so on screen when the server reports more…` | cảnh báo hiện, có "10/30" |
| `shows no warning when the catalog arrived whole` | không cảnh báo giả |

Mock của list **mô phỏng đúng server, gồm cả mặc định 10 khi không ai truyền page size**. Điểm này quan trọng: mock cũ trả đủ 30 bất kể tham số, nên test viết trên nó sẽ **xanh ngay trên chính con bug**. Đã kiểm chứng bằng cách tạm hoàn nguyên đúng một dòng sửa: **4/5 test đỏ**; khôi phục thì **31/31 xanh**. (Test thứ 5 kiểm tấm chắn truncation nên đúng ra vẫn xanh cả hai chiều.)

### 12.3 Phạm vi — đã quét, không suy đoán

Quét mọi query có phân trang trong `PEMS.Application` (29 query) rồi đối chiếu caller frontend. `TemplateManagement.tsx` là **trường hợp duy nhất** hội đủ ba điều kiện: endpoint có phân trang + gọi không tham số + lọc phía client mà không có điều khiển phân trang. `EmailComposeModal` đã truyền `pageSize: 100`; `EmailManagement` phân trang có chủ đích kèm `totalItems`. Các lời gọi không-tham-số khác đều tới endpoint **không** phân trang (campus đang hoạt động, ứng viên, tuỳ chọn lọc, hồ sơ cá nhân).
