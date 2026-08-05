# PEMS — Kế hoạch loại bỏ Contact, dùng Sender Variables và Preview → Edit → Final Preview → Send

## 0. Quyết định cuối cùng

Tài liệu này thay thế kế hoạch **structured contact override** trước đó.

Mục tiêu mới:

- Bỏ toàn bộ khái niệm `contact`/“đầu mối liên hệ” khỏi giao diện cấu hình và gửi email.
- Chỉ sử dụng thông tin của **người thực hiện gửi email** (`sender`).
- Thông tin người gửi được cấu hình bằng các biến bình thường của mẫu email.
- Biểu tượng mắt mở bản email hoàn chỉnh đã parse ở chế độ chỉ đọc.
- Chỉ khi bấm **Chỉnh sửa**, nội dung đã parse mới được đưa vào editor.
- Người dùng được sửa tự do nội dung thông thường, bao gồm phần thông tin người gửi đã được điền.
- OTP, token, action URL, nút Chấp nhận/Từ chối và attachment bắt buộc vẫn do hệ thống khóa.
- Người dùng phải xem lại **Final Preview** trước khi gửi.
- Email thực gửi phải giống nội dung Final Preview.
- Không tạo bảng mới và không khôi phục các bảng email draft cũ.

---

## 1. Trạng thái hiện tại và câu trả lời về “mail hỗ trợ”

### 1.1 Baseline local

```text
Branch: Canh-Iter1
HEAD trước: adcae824
HEAD local hiện tại: 31caebe5
Số commit local chưa push: 5
```

```text
ccc6f42a  test(db): unblock the integration suite after the canonical script changed
7a8fc541  fix(email): put the message's own Reply-To on the SMTP envelope
a169fb60  feat(email): add structured per-message contact override
838d4445  feat(frontend): add a controlled contact editor to the email preview
31caebe5  test(email): verify preview-send contact parity
```

### 1.2 Contact hiện tại có giới hạn theo template không?

Cần phân biệt hai cách hiểu:

#### “Chỉ template có hỗ trợ contact mới được cấu hình contact”

**Có.** Thiết kế hiện tại có capability guard để:

- ẩn panel contact ở template không hỗ trợ;
- cấm lưu `{{contactInformationBlock}}` vào template không hỗ trợ;
- cấm runtime override trên template không hỗ trợ.

Nhóm không hỗ trợ được báo cáo gồm 4 template:

```text
ACCOUNT_EMAIL_CONFIRMATION
AUTH_PASSWORD_RESET_OTP
VISIT_REQUEST_OTP
VISIT_REMINDER_HOST
```

#### “Contact chỉ được cấu hình cho các email của bộ phận hỗ trợ”

**Không.** Contact hiện tại không chỉ áp dụng cho email hỗ trợ. Audit gần nhất báo cáo:

```text
31 template
4 NONE/unsupported
13 OPTIONAL
14 REQUIRED
```

Tức là contact đang áp dụng cho 27/31 template, gồm logistics, invitation, assignment, setup-progress và các thông báo khác.

### 1.3 Sau kế hoạch mới

Sau khi triển khai tài liệu này:

- không còn “template hỗ trợ contact”;
- không còn contact policy, source, fallback, candidate hoặc override;
- chỉ còn `sender variables`;
- sender variables chỉ được chèn vào những template có capability phù hợp;
- email nhạy cảm/tự động không cho người dùng chỉnh sửa runtime.

---

## 2. Mô hình dữ liệu nghiệp vụ mới

### 2.1 Chỉ còn Sender

`sender` là tài khoản thực sự thực hiện thao tác gửi email trong PEMS.

Các biến chuẩn:

```text
{{senderName}}
{{senderRole}}
{{senderEmail}}
{{senderPhone}}
{{senderDepartment}}
{{senderCampus}}
```

Loại bỏ:

```text
{{contactName}}
{{contactRole}}
{{contactEmail}}
{{contactPhone}}
{{contactDepartment}}
{{contactCampus}}
{{contactInformationBlock}}
```

Loại bỏ các nguồn contact:

```text
HOST
SENDER
HOST_THEN_SENDER
CAMPUS_DEFAULT
DEPARTMENT_DEFAULT
SUPPORT_CONTACT
NONE
```

### 2.2 Sender hiển thị khác SMTP From

**Sender hiển thị trong body** là người bấm gửi, ví dụ:

```text
Nguyễn Văn An
Host
Phòng Hợp tác Quốc tế
an.nv@fpt.edu.vn
```

**SMTP From** vẫn có thể là:

```text
PEMS <no-reply@pems-fpt.site>
```

Không đổi SMTP From thành email cá nhân nếu provider/domain không cho phép.

### 2.3 Email tự động

Với OTP, confirmation, password reset hoặc reminder chạy nền, không có người bấm gửi. Dùng system sender:

```text
senderName       = Bộ phận hỗ trợ PEMS
senderRole       = Hệ thống PEMS
senderEmail      = email hỗ trợ trong cấu hình hệ thống
senderPhone      = số hỗ trợ nếu có
senderDepartment = PEMS
senderCampus     = trống hoặc Toàn hệ thống
```

Không nhận sender fields từ frontend.

---

## 3. Capability mới cho Sender Variables

Không dùng lại capability của contact. Tạo capability rõ nghĩa:

```text
SenderVariableCapability
```

Ba mức:

```text
NOT_AVAILABLE
AVAILABLE_READ_ONLY_RUNTIME
AVAILABLE_EDITABLE_RUNTIME
```

### 3.1 `NOT_AVAILABLE`

Dùng cho template không cần hoặc không được phép chèn thông tin người gửi.

Ví dụ:

- OTP;
- password reset;
- email xác nhận tài khoản;
- template nhạy cảm không cần người gửi.

Quy tắc:

- UI quản lý mẫu không hiện nhóm biến Sender.
- Backend từ chối lưu body/subject có `{{sender*}}`.
- Runtime không có editor.

### 3.2 `AVAILABLE_READ_ONLY_RUNTIME`

Template được dùng sender variables nhưng được gửi tự động, không cho người dùng chỉnh runtime.

Ví dụ:

- reminder tự động;
- system notice;
- thông báo nền cần chữ ký đơn vị gửi.

Quy tắc:

- Admin có thể chèn sender variables.
- Runtime chỉ gửi theo template đã render.

### 3.3 `AVAILABLE_EDITABLE_RUNTIME`

Template do người dùng chủ động chuẩn bị/gửi.

Ví dụ:

- logistics request;
- invitation;
- department assignment;
- setup-progress email;
- manual compose có context nghiệp vụ.

Quy tắc:

- Admin có thể chèn sender variables.
- Preview hiển thị sender thật.
- Người dùng được bấm Chỉnh sửa.

### 3.4 Không suy quyền từ placeholder

Không dùng:

```text
body có {{senderName}} thì cho chỉnh sửa
```

Phải dùng:

```text
template capability
+ actor permission
+ send-flow capability
```

---

## 4. Màn quản lý mẫu email

### 4.1 Xóa Contact Settings

Loại bỏ:

- Nguồn thông tin liên hệ.
- NONE/OPTIONAL/REQUIRED của contact.
- Host/Sender/Campus/Department/Support contact.
- Contact heading.
- Checkbox field contact.
- Contact fallback.
- Reply-To theo contact.
- `{{contactInformationBlock}}`.
- Nút lưu/khôi phục cấu hình contact riêng.

### 4.2 Thêm nhóm biến “Thông tin người gửi”

Hiển thị trong variable picker:

```text
Thông tin người gửi

Họ tên người gửi          {{senderName}}
Vai trò người gửi         {{senderRole}}
Email người gửi           {{senderEmail}}
Số điện thoại người gửi   {{senderPhone}}
Phòng ban người gửi       {{senderDepartment}}
Cơ sở người gửi           {{senderCampus}}
```

Chỉ hiện khi capability khác `NOT_AVAILABLE`.

### 4.3 Admin tự thiết kế bố cục

Ví dụ dạng đoạn văn:

```html
<p>
  Nếu cần trao đổi thêm, vui lòng liên hệ
  <strong>{{senderName}}</strong>
  qua email {{senderEmail}}
  hoặc số điện thoại {{senderPhone}}.
</p>
```

Ví dụ dạng bảng:

```html
<h3>Thông tin người gửi</h3>
<table>
  <tr><th>Họ tên</th><td>{{senderName}}</td></tr>
  <tr><th>Vai trò</th><td>{{senderRole}}</td></tr>
  <tr><th>Phòng ban</th><td>{{senderDepartment}}</td></tr>
  <tr><th>Cơ sở</th><td>{{senderCampus}}</td></tr>
  <tr><th>Email</th><td>{{senderEmail}}</td></tr>
  <tr><th>Điện thoại</th><td>{{senderPhone}}</td></tr>
</table>
```

Admin được:

- đổi tiêu đề;
- đổi thứ tự;
- bỏ field không cần;
- đổi bố cục;
- đặt sender variables ở vị trí phù hợp;
- không dùng sender variables nếu template không cần.

### 4.4 Preview trong quản lý mẫu

Do chưa có actor thật, dùng sample data:

```text
Nguyễn Văn An
Host
an.nguyen@example.invalid
0901234567
Phòng Hợp tác Quốc tế
FPT University Hà Nội
```

Ghi rõ:

```text
Dữ liệu minh họa trong màn xem trước mẫu
```

Không dùng câu “Hệ thống sẽ điền đầu mối khi gửi email thật”.

---

## 5. Sender Variable Resolver

### 5.1 Interface đề xuất

```csharp
public interface IEmailSenderVariableResolver
{
    Task<EmailSenderVariables> ResolveAsync(
        ulong? actorUserId,
        string templateCode,
        CancellationToken cancellationToken);
}
```

```csharp
public sealed record EmailSenderVariables(
    string Name,
    string? Role,
    string? Email,
    string? Phone,
    string? Department,
    string? Campus,
    bool IsSystemSender);
```

### 5.2 Email do người dùng gửi

Backend lấy từ actor hiện tại:

```text
CurrentUser.UserId
→ users
→ role/subrole
→ department
→ campus
```

Frontend không được truyền sender identity.

### 5.3 Email tự động

Resolver dùng system sender từ cấu hình hiện có.

Không tạo bảng mới.

### 5.4 Chỉ render biến một lần

Dữ liệu sender từ DB phải được coi là data, không parse thành template lần hai.

Ví dụ tên user:

```text
Nguyễn {{visitCode}} Văn A
```

phải hiển thị nguyên văn đã encode, không thay `{{visitCode}}`.

Pipeline:

```text
Parse placeholder trong template một lần
→ encode giá trị sender
→ không parse lại output
```

---

## 6. Preview bằng biểu tượng mắt

### 6.1 Mặc định là VIEW chỉ đọc

Luồng:

```text
Template
→ resolve variables
→ render editable body
→ ghép locked blocks
→ tạo preview token
→ hiển thị email hoàn chỉnh
```

Preview phải có:

- TO/CC/BCC;
- subject đã parse;
- body đã parse;
- sender thật nếu template dùng;
- action buttons;
- attachments;
- Reply-To;
- ngôn ngữ đúng.

Không được có:

- placeholder chưa parse;
- contact stand-in;
- khung nét đứt;
- `{{contactInformationBlock}}`;
- `{{senderName}}` còn nguyên.

### 6.2 Contract đề xuất

```ts
interface PreparedEmailPreview {
  previewToken: string;
  templateCode: string;
  templateRevision: number;
  subject: string;
  editableBodyHtml: string;
  lockedActionBlockHtml?: string;
  finalPreviewHtml: string;
  recipients: {
    to: string[];
    cc: string[];
    bcc: string[];
  };
  replyToEmail?: string;
  attachments: EmailAttachment[];
  canEdit: boolean;
  expiresAt: string;
}
```

### 6.3 Không tạo lại draft table

Không tạo:

```text
email_drafts
email_draft_recipients
email_draft_attachments
```

Dùng `previewToken` ngắn hạn do backend ký HMAC.

Token phải bind:

- actorUserId;
- templateCode/revision;
- related entity;
- recipient hash;
- subject/body hash;
- locked block hash;
- attachment hash;
- Reply-To;
- expiry.

---

## 7. Chế độ chỉnh sửa

### 7.1 State frontend

```text
VIEW
EDIT
FINAL_PREVIEW
SENDING
```

Luồng:

```text
Bấm mắt
→ VIEW
→ Chỉnh sửa
→ EDIT
→ Xem trước kết quả
→ FINAL_PREVIEW
→ Gửi
```

### 7.2 Nội dung sender sau parse được sửa tự do

Trong EDIT, body đã chứa dữ liệu thật:

```text
Nguyễn Văn An
Host
an.nv@fpt.edu.vn
```

Người dùng có thể:

- sửa câu chữ;
- sửa tên/vai trò/email/điện thoại hiển thị trong body;
- xóa phần sender;
- thêm hướng dẫn;
- đổi bảng thành đoạn văn;
- đổi bố cục và định dạng.

Sau parse, sender text trong body là nội dung bình thường.

### 7.3 Phần vẫn khóa

Không cho chỉnh:

- OTP;
- token;
- URL xác nhận/reset;
- nút Chấp nhận/Từ chối;
- tracking marker;
- attachment bắt buộc;
- metadata nội bộ;
- recipient ngoài quyền actor.

Thiết kế:

```text
editableBodyHtml
+
lockedActionBlockHtml
```

Không còn `lockedContactBlockHtml`.

### 7.4 Nút trong editor

```text
[Hủy thay đổi]
[Khôi phục từ mẫu]
[Xem trước kết quả]
```

Không có nút gửi trực tiếp trong EDIT.

---

## 8. Final Preview và exact send

### 8.1 Final-preview endpoint

Khi bấm **Xem trước kết quả**:

1. Backend kiểm tra quyền.
2. Validate subject/body/attachments/recipients.
3. Sanitize body.
4. Ghép locked blocks.
5. Sinh `finalPreviewHtml`.
6. Phát hành `finalPreviewToken` mới.

Request:

```ts
{
  previewToken: string;
  subject: string;
  editableBodyHtml: string;
  replyToEmail?: string;
  attachments: EmailAttachmentInput[];
}
```

Response:

```ts
{
  finalPreviewToken: string;
  subject: string;
  finalPreviewHtml: string;
  replyToEmail?: string;
  recipients: {...};
  attachments: [...];
  expiresAt: string;
}
```

### 8.2 Send

Send request:

```ts
{
  finalPreviewToken: string;
}
```

Backend phải:

- verify actor;
- verify expiry;
- verify template revision;
- verify entity state;
- verify recipient/attachment authorization;
- gửi đúng subject/body/reply-to đã final-preview;
- không parse sender lại;
- không append contact;
- không tự khôi phục template.

### 8.3 Preview stale

Nếu dữ liệu ảnh hưởng bảo mật hoặc recipient/action token đã đổi:

```text
EMAIL_PREVIEW_STALE
```

Yêu cầu tạo preview mới. Không âm thầm render lại rồi gửi nội dung khác.

---

## 9. Reply-To

### 9.1 Mặc định

Email do người dùng gửi:

```text
Reply-To = senderEmail
```

Email tự động:

```text
Reply-To = system support email
```

### 9.2 Field riêng

Không đọc HTML body để suy Reply-To.

Preview hiển thị:

```text
Khi người nhận bấm “Trả lời”, email sẽ gửi tới:
an.nv@fpt.edu.vn
```

Nếu actor có quyền, cho sửa ở field riêng.

Validation:

- email hợp lệ;
- không CR/LF;
- không header injection;
- SMTP và Resend dùng cùng giá trị.

### 9.3 Giữ sửa lỗi SMTP

Giữ logic của commit:

```text
7a8fc541  fix(email): put the message's own Reply-To on the SMTP envelope
```

---

## 10. Xử lý 5 commit local hiện tại

### 10.1 Giữ

```text
ccc6f42a  canonical SQL hash fix
7a8fc541  SMTP Reply-To fix
```

### 10.2 Gỡ/refactor backend contact

Loại bỏ khi không còn caller:

- `EmailContactOverrideInput`
- `EmailContactOverrideValidator`
- `EmailContactCandidates`
- `EmailContactPreviewResult`
- `ResolveEmailContactPreviewQuery`
- `IEmailContactCandidateService`
- contact candidate route
- contact preview route
- `EmailOverride.ContactOverride`
- contact override audit
- contact append trong dispatcher
- contact capability/policy runtime

Không xóa schema trong task này.

Nếu bảng/config contact không còn dùng:

1. dừng đọc/ghi trong code;
2. đánh dấu deprecated;
3. chạy regression;
4. xóa schema ở task riêng sau khi được duyệt.

### 10.3 Gỡ/refactor frontend contact

- `EmailContactOverrideSection.tsx`
- contact mode picker
- user contact picker
- manual contact form
- `lockedContactBlockHtml`
- contact translations
- `ContactSettingsPanel`

Thay bằng:

- sender variables trong template editor;
- VIEW/EDIT/FINAL_PREVIEW;
- Reply-To field riêng.

### 10.4 Viết lại test

Từ:

```text
contact override parity
```

sang:

```text
sender variable preview-edit-final-preview-send parity
```

---

## 11. Các luồng phải migrate

Search toàn repository:

```text
contactInformationBlock
EmailContact
ContactSettingsPanel
contactOverride
contact-preview
contact-candidates
lockedContactBlockHtml
previewEmailTemplate
EmailPreviewModal
EmailComposeModal
ManualEmailSender
useEditedContent
SystemEmailContent.AuthoredByUser
```

Tối thiểu gồm:

### Logistics

- `LogisticsRequestSection.tsx`
- logistics request/change/assignee templates

### Invitations

- `ParticipantInvitationSection.tsx`
- staff/student/department invitation

### Department dashboard

- `SharedDashboardView.tsx`
- assignment/invitation flows

### Setup progress

```text
VisitSetupProgressComposer
→ EmailComposeModal
→ ManualEmailSender
```

Phải migrate để không còn ngoại lệ sửa contact block trực tiếp.

### Automated flows

- OTP
- account confirmation
- password reset
- reminders
- account/system notices

Không mở runtime editor, nhưng phải audit system sender và Reply-To.

---

## 12. Template migration

Lập matrix 31 template:

| Template code | Sender capability | Có sender vars | Runtime edit | Actor/System sender | Reply-To | Cần migrate |
|---|---|---:|---:|---|---|---:|

Quy tắc:

- Không tự thay mọi `{{contactInformationBlock}}` bằng cùng một bảng sender.
- Đọc body VI/EN của từng template.
- Chỉ thêm sender variables vào template thực sự cần.
- Email nhạy cảm không chèn sender cá nhân nếu không có yêu cầu.
- Manual compose dùng `AVAILABLE_EDITABLE_RUNTIME`.
- Automated template dùng `AVAILABLE_READ_ONLY_RUNTIME` hoặc `NOT_AVAILABLE`.

Nếu thay template defaults:

- cập nhật `email-template-defaults.json`;
- cập nhật canonical SQL seed;
- tạo patch idempotent;
- cập nhật parity tests;
- bump canonical SQL hash nếu SQL thay đổi.

Không để defaults/SQL/DB lệch nhau.

---

## 13. Frontend UX cuối cùng

### VIEW

```text
XEM TRƯỚC EMAIL

TO / CC / BCC
Tiêu đề
Nội dung hoàn chỉnh đã parse
Action buttons chỉ đọc
Attachments
Reply-To

[Đóng] [Chỉnh sửa] [Gửi email]
```

Template không editable:

```text
[Đóng] [Gửi email]
```

### EDIT

```text
CHỈNH SỬA EMAIL

Tiêu đề
Rich-text editor
Reply-To nếu có quyền
Attachments

[Hủy thay đổi] [Khôi phục từ mẫu] [Xem trước kết quả]
```

### FINAL_PREVIEW

```text
XEM TRƯỚC KẾT QUẢ CUỐI

Nội dung đúng như sẽ gửi
Action buttons
Attachments
Reply-To

[Quay lại chỉnh sửa] [Gửi email]
```

---

## 14. Bảo mật và audit

### Edited body

- sanitize bằng pipeline hiện có;
- chặn script/event handlers/unsafe URL;
- cấm trusted placeholders;
- cấm action token thủ công;
- không render template lần hai.

### Sender identity

- lấy từ DB/backend config;
- không tin sender fields từ client;
- không dùng HTML để xác định actor.

### Preview token

- ký HMAC;
- thời hạn ngắn;
- bind actor/template/entity;
- chống replay bằng idempotency hiện có;
- không chứa secret/action token dạng dễ đọc.

### Audit

Lưu tối thiểu:

```text
actorUserId
templateCode
relatedEntityType
relatedEntityId
templateRevision
wasEdited
replyTo
preview/final token identifier hash
sentEmailId
sentAt
```

Dù người dùng sửa tên/email hiển thị trong body, actor thật vẫn lấy từ authentication/audit.

---

## 15. Test bắt buộc

### Backend unit

- resolve sender đúng actor;
- resolve system sender khi không có actor;
- capability chặn sender vars trên `NOT_AVAILABLE`;
- missing optional sender fields không crash;
- sender data có `{{ }}` không bị parse lần hai;
- preview token bind đúng actor/template/revision;
- stale token bị từ chối;
- edited body không bị render lại;
- Reply-To validation;
- SMTP/Resend parity.

### Integration

- logistics prepare → view → send;
- logistics prepare → edit → final preview → send;
- invitation;
- department assignment;
- setup-progress;
- automated email dùng system sender;
- `.eml`/fake provider output khớp Final Preview;
- không còn contact block/stand-in;
- không còn literal sender placeholder;
- VI và EN.

### Frontend

- biểu tượng mắt mở VIEW;
- sender thật đã parse;
- template editable có nút Chỉnh sửa;
- template read-only không có nút Chỉnh sửa;
- EDIT không mất recipients/attachments;
- sender text trong body sửa tự do;
- action block không vào editor;
- không gửi trực tiếp trong EDIT;
- Final Preview đúng;
- send dùng final token;
- stale/error giữ modal và thông báo rõ.

### Browser smoke

Chạy trực tiếp:

1. Logistics/Teabreak.
2. Participant invitation.
3. Department assignment.
4. Setup-progress email.

Kiểm tra:

- VIEW đúng bố cục;
- EDIT tự do;
- FINAL_PREVIEW đúng;
- email nhận/pickup giống preview;
- Reply-To đúng;
- không còn UI contact.

---

## 16. Thứ tự triển khai

### G0 — Preflight

- branch/HEAD;
- WIP/stash;
- ghi nhận 5 commit local;
- không push;
- baseline tests;
- outbound email safety.

### G1 — Audit

- toàn bộ contact code;
- matrix 31 template;
- sender capability;
- editable/read-only flow.

### G2 — Sender registry/resolver

- variables;
- VI/EN labels;
- capability;
- sample data;
- actor/system sender;
- one-pass rendering.

### G3 — Template management

- bỏ contact panel;
- thêm sender variables;
- capability guard;
- sample preview.

### G4 — Preview pipeline

- prepare preview;
- final preview;
- signed token;
- stale detection;
- exact send.

### G5 — Frontend modal

- VIEW;
- EDIT;
- FINAL_PREVIEW;
- Reply-To;
- restore template.

### G6 — Migrate call sites

- logistics;
- invitations;
- dashboard;
- setup progress;
- automated sends.

### G7 — Remove dead contact code

- backend;
- frontend;
- routes;
- translations;
- tests;
- docs.

Không xóa schema.

### G8 — Template/default/SQL sync

Chỉ khi body template cần migration.

### G9 — Full gates và browser smoke

- backend unit;
- architecture;
- integration;
- frontend unit;
- typecheck/lint/build;
- browser smoke;
- `.eml` parity.

### G10 — Commit

Đề xuất:

```text
refactor(email): replace contact policies with sender template variables
feat(email): add exact final-preview snapshot sending
refactor(frontend): split email view edit and final-preview modes
test(email): verify sender preview-edit-send parity
```

Không push trước khi browser smoke xanh và được yêu cầu.

---

## 17. Acceptance criteria

Hoàn thành khi:

1. Không còn contact trên UI quản lý mẫu.
2. Không còn contact picker/manual override.
3. Không còn `{{contactInformationBlock}}` trong template đang hoạt động.
4. Sender variables chỉ hiện trên template có capability phù hợp.
5. Template `NOT_AVAILABLE` không lưu được sender vars.
6. Biểu tượng mắt mở email hoàn chỉnh chỉ đọc.
7. Sender variables đã parse thành dữ liệu thật.
8. Người dùng chỉ chỉnh sau khi bấm Chỉnh sửa.
9. Sender text đã parse được sửa tự do.
10. Action/token vẫn khóa.
11. Không gửi trực tiếp trong editor.
12. Có Final Preview trước khi gửi.
13. Email thực gửi giống Final Preview.
14. Reply-To mặc định đúng sender/system sender.
15. SMTP và Resend đồng nhất.
16. Setup-progress dùng cùng kiến trúc.
17. Không tạo bảng mới.
18. Không khôi phục draft tables.
19. Audit vẫn ghi actor thật.
20. Toàn bộ gates xanh.
21. Browser smoke xanh.
22. Chưa push cho tới khi được yêu cầu.

---

## 18. Format báo cáo bắt buộc

```text
1. Preflight
- Branch:
- HEAD trước/sau:
- WIP/stash:
- Local commits preserved/refactored:

2. Current-state audit
- Contact-supported templates:
- Contact-unsupported templates:
- Sender capability matrix:
- Editable/read-only flows:

3. Removed contact architecture
- Backend:
- Frontend:
- Routes:
- Templates:
- Dead code remaining:

4. Sender implementation
- Variables:
- User sender resolution:
- System sender resolution:
- One-pass rendering:
- Reply-To:

5. Preview pipeline
- Prepare:
- VIEW:
- EDIT:
- FINAL_PREVIEW:
- Signed token:
- Stale handling:
- Exact send:

6. Migrated call sites
- Logistics:
- Invitations:
- Department assignment:
- Setup progress:
- Automated emails:

7. Template sync
- Defaults:
- Canonical SQL:
- Patch:
- Hash:

8. Tests
- Backend unit:
- Architecture:
- Integration:
- Frontend unit:
- Typecheck/lint/build:
- Browser smoke:
- .eml/provider parity:

9. Schema
- Schema changed: NO
- New tables: NO
- Draft tables restored: NO

10. Commits
- SHA/message:

11. Remaining debt
- Chỉ ghi debt có bằng chứng và nằm ngoài scope.
```
