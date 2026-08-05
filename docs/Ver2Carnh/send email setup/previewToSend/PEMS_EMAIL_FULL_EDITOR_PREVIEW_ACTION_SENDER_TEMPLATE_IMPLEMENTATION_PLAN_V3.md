# PEMS — Kế hoạch triển khai đầy đủ hệ thống Email Template, Rich-text Editor, Preview, Action Block và Exact Send

## 0. Mục đích tài liệu

Tài liệu này tổng hợp và thay thế các kế hoạch rời rạc trước đó về:

- loại bỏ kiến trúc `contact` trong email;
- chỉ sử dụng `sender variables`;
- chuẩn hóa nội dung mặc định của 31 template;
- dùng chung một rich-text editor cho màn quản lý mẫu và màn soạn email trước khi gửi;
- đưa action block vào đúng vị trí trong nội dung email;
- cho phép di chuyển action block nhưng không cho sửa chức năng/token;
- bảo đảm `Editor → Preview → Final Preview → Email thực gửi` đồng nhất;
- bảo vệ hệ thống khỏi XSS, HTML injection, token tampering, URL injection và các lỗi bố cục email;
- đồng bộ defaults JSON, canonical SQL, patch SQL và script sync;
- thêm guard chống chạy nhầm canonical SQL vào `pems_db`;
- hoàn thiện test, browser smoke và commit strategy.

Đây là tài liệu triển khai cuối cùng cho phạm vi email hiện tại.

---

# 1. Quyết định nghiệp vụ cuối cùng

## 1.1 Chỉ còn `sender`, không còn `contact`

Không còn:

```text
{{contactName}}
{{contactRole}}
{{contactEmail}}
{{contactPhone}}
{{contactDepartment}}
{{contactCampus}}
{{contactInformationBlock}}
```

Không còn:

```text
HOST
SENDER
HOST_THEN_SENDER
CAMPUS_DEFAULT
DEPARTMENT_DEFAULT
SUPPORT_CONTACT
NONE
```

Không còn:

- contact picker;
- contact manual override;
- contact candidates;
- contact preview route;
- contact source;
- contact fallback;
- contact policy UI;
- contact trusted block.

Chỉ còn:

```text
{{senderName}}
{{senderRole}}
{{senderEmail}}
{{senderPhone}}
{{senderDepartment}}
{{senderCampus}}
```

## 1.2 Sender là ai?

### Email do người dùng chủ động gửi

`sender` là actor đang thực hiện thao tác gửi email.

Backend resolve từ:

```text
CurrentUser.UserId
→ users
→ role/subrole
→ department
→ campus
```

Frontend không được gửi sender fields lên để tự khai báo.

### Email tự động

Dùng system sender:

```text
senderName       = Bộ phận hỗ trợ PEMS
senderRole       = Hệ thống PEMS
senderEmail      = SupportContact.Email
senderPhone      = SupportContact.Phone
senderDepartment = PEMS
senderCampus     = trống hoặc Toàn hệ thống
```

Không tạo bảng mới.

## 1.3 SMTP From và sender hiển thị là hai khái niệm khác nhau

SMTP From có thể vẫn là:

```text
PEMS <no-reply@pems-fpt.site>
```

Sender hiển thị trong body có thể là:

```text
IC Staff Hà Nội
IC Staff
staff.hn@fpt.edu.vn
```

Không tự đổi SMTP From thành email cá nhân nếu provider không hỗ trợ.

---

# 2. Trạng thái hiện tại cần bảo toàn

Baseline local gần nhất:

```text
Branch: Canh-Iter1
HEAD: 31caebe5
5 commit local chưa push
WIP lớn chưa commit
9 stash cũ không được đụng
```

Các commit phải bảo toàn:

```text
ccc6f42a  test(db): unblock the integration suite after the canonical script changed
7a8fc541  fix(email): put the message's own Reply-To on the SMTP envelope
a169fb60  feat(email): add structured per-message contact override
838d4445  feat(frontend): add a controlled contact editor to the email preview
31caebe5  test(email): verify preview-send contact parity
```

Quy tắc:

- Không reset.
- Không checkout làm mất WIP.
- Không stash pop bừa.
- Không push.
- Không commit khi project chưa runnable.
- Phần structured contact override phải được refactor hoặc loại bỏ, không giữ kiến trúc cũ song song.

---

# 3. Capability của template

## 3.1 Sender capability

Dùng enum:

```text
NOT_AVAILABLE
AVAILABLE_READ_ONLY_RUNTIME
AVAILABLE_EDITABLE_RUNTIME
```

### `NOT_AVAILABLE`

Template không dùng sender variables và không chỉnh runtime.

Tối thiểu:

- `ACCOUNT_EMAIL_CONFIRMATION`
- `AUTH_PASSWORD_RESET_OTP`
- `VISIT_REQUEST_OTP`

### `AVAILABLE_READ_ONLY_RUNTIME`

Template có thể dùng sender variables nhưng không cho người dùng chỉnh runtime.

Ví dụ:

- reminder tự động;
- account/system notice;
- report tự động;
- notification background.

### `AVAILABLE_EDITABLE_RUNTIME`

Template do actor chủ động prepare và gửi.

Tối thiểu:

- `LOGISTICS_REQUEST_TO_DEPARTMENT`
- `LOGISTICS_CHANGE_PROPOSAL_TO_HOST`
- `VISIT_PARTICIPANT_INVITATION`
- `VISIT_STUDENT_INVITATION`
- `VISIT_DEPARTMENT_LEADER_INVITATION`
- `VISIT_DEPARTMENT_STAFF_ASSIGNMENT`
- `VISIT_SETUP_PROGRESS_UPDATE`

## 3.2 Capability không được suy ra từ placeholder

Không dùng:

```text
body có {{senderName}} → cho chỉnh
```

Phải dùng:

```text
templateCode
→ capability registry
→ actor permission
→ send-flow permission
```

---

# 4. Một editor dùng chung cho cả hai màn

## 4.1 Component chung

Tạo một component:

```tsx
<EmailRichTextEditor />
```

Không tạo hai editor độc lập với hai thư viện/renderer khác nhau.

Hai mode:

```text
TEMPLATE
COMPOSE
```

Ví dụ:

```tsx
<EmailRichTextEditor
  mode="TEMPLATE"
  capabilities={...}
/>
```

```tsx
<EmailRichTextEditor
  mode="COMPOSE"
  capabilities={...}
/>
```

## 4.2 Điểm giống nhau

Cùng:

- toolbar;
- HTML serializer;
- HTML normalizer;
- sanitizer policy;
- style allow-list;
- variable/system-node model;
- preview renderer;
- final renderer;
- dirty-state canonicalizer;
- paste sanitizer;
- link validation;
- image insertion logic.

## 4.3 Điểm khác nhau

### TEMPLATE mode

Cho phép:

- chèn sender variables;
- chèn business variables;
- đặt `{{actionBlock}}`;
- chọn VI/EN;
- sửa nội dung mặc định;
- thiết kế bố cục dùng chung;
- preview bằng sample data.

### COMPOSE mode

Cho phép:

- sửa subject;
- sửa body đã parse;
- sửa nội dung sender đã parse;
- di chuyển action system node;
- thêm link/ảnh/bảng;
- sửa attachment nếu flow cho phép;
- sửa Reply-To nếu actor có quyền.

Không cho:

- chèn biến mới;
- tự tạo action block mới;
- sửa token/action URL;
- sửa OTP/token system;
- chèn raw trusted block placeholder.

---

# 5. Toolbar rich-text editor cần hỗ trợ

## 5.1 Chức năng tối thiểu

```text
Undo
Redo
Font
Font size
Bold
Italic
Underline
Strikethrough
Text color
Background color
Align left
Align center
Align right
Numbered list
Bullet list
Increase indent
Decrease indent
Insert link
Insert image
Insert table
Insert divider
Clear formatting
Fullscreen
Insert variable
Insert/move system block
```

## 5.2 Font an toàn

Chỉ cho danh sách font an toàn:

```text
Arial
Verdana
Tahoma
Trebuchet MS
Georgia
Times New Roman
Courier New
```

Không cho nhập font tùy ý.

## 5.3 Cỡ chữ

Danh sách cố định:

```text
12px
14px
16px
18px
20px
24px
28px
32px
```

Không cho arbitrary CSS.

## 5.4 Căn chỉnh và thụt lề

Không dùng nhiều dấu cách thường để căn chỉnh.

Dùng:

```html
<p style="text-align:center">...</p>
```

hoặc:

```html
<p style="margin-left:32px">...</p>
```

Mức indent:

```text
0px
16px
32px
48px
```

## 5.5 Bảng

Cho bảng đơn giản:

- 2–6 cột;
- không nested table tùy ý;
- có `role="presentation"` khi chỉ dùng layout;
- inline style;
- không vượt container;
- không fixed width lớn.

## 5.6 Paste từ Word/Gmail/Web

Khi paste:

- giữ bold/italic/list/table/link cơ bản;
- loại script/style nguy hiểm;
- loại hidden element;
- loại tracking pixel lạ;
- normalize font;
- loại absolute positioning;
- không giữ arbitrary class/style ngoài allow-list.

---

# 6. Không dùng khoảng trắng thường để layout

## 6.1 Vấn đề

HTML gom nhiều khoảng trắng liên tiếp thành một.

Ví dụ:

```html
<p>       {{senderPhone}}</p>
```

không đảm bảo thụt sang phải.

## 6.2 Quy tắc

Dấu cách vẫn dùng bình thường giữa các từ.

Không dùng:

```text
Số điện thoại:             {{senderPhone}}
```

để căn cột.

Dùng:

- indent;
- text-align;
- table layout.

## 6.3 UX

Nếu người dùng gõ nhiều khoảng trắng liên tiếp:

- normalize thành một;
- hoặc cảnh báo:

```text
Dấu cách liên tiếp không được dùng để căn chỉnh trong email.
Vui lòng dùng công cụ thụt lề hoặc căn lề.
```

Không tự chuyển hàng loạt sang `&nbsp;`.

---

# 7. Variable model

## 7.1 Variable hiển thị như chip

Trong TEMPLATE mode, biến nên hiển thị như token/chip:

```text
[Họ tên người gửi]
[Email người gửi]
[Tên đoàn]
```

Nội bộ map tới:

```text
{{senderName}}
{{senderEmail}}
{{delegationName}}
```

Tránh người dùng sửa mất một dấu ngoặc.

## 7.2 One-pass substitution

Pipeline:

```text
Template HTML
→ substitute variable đúng một lần
→ encode data
→ không parse lại output
```

Tên user chứa:

```text
Nguyễn {{visitCode}} Văn A
```

phải hiển thị nguyên văn, không render lần hai.

## 7.3 Style quanh biến phải được giữ nguyên

Input:

```html
<p style="text-align:right;margin-left:32px">
  {{senderPhone}}
</p>
```

Output:

```html
<p style="text-align:right;margin-left:32px">
  0901234567
</p>
```

Substitution chỉ thay text node/value, không thay wrapper/style.

---

# 8. Action block inline và có thể di chuyển

## 8.1 Action block nằm trong body

Template dùng:

```text
{{actionBlock}}
```

Preview phải hiển thị action đúng vị trí trong body.

Không tách action thành section riêng ngoài email.

## 8.2 Action node trong editor

Không đưa URL/token thật vào editor.

Dùng node:

```html
<div data-system-block="action"></div>
```

hoặc editor node:

```json
{
  "type": "system-action-block",
  "blockId": "PRIMARY_ACTION_BLOCK"
}
```

## 8.3 Người dùng được phép

- kéo block lên/xuống;
- cắt/dán block;
- đặt trước/sau sender block;
- thêm nội dung quanh block;
- thay đổi khoảng cách ngoài block.

## 8.4 Người dùng không được phép

- sửa token;
- sửa URL;
- sửa action ID;
- sửa chức năng;
- nhân đôi;
- chèn action giả;
- sửa HTML bên trong;
- xóa nếu action bắt buộc.

## 8.5 Count validation

Với action bắt buộc:

```text
action block count = 1
```

Nếu 0:

```text
Email này cần nút phản hồi để người nhận xử lý yêu cầu.
Bạn có thể thay đổi vị trí nhưng không thể xóa khối này.
```

Nếu >1:

```text
Mỗi email chỉ được có một khối nút phản hồi.
```

## 8.6 Preview action

Trong preview:

- hiển thị đúng label;
- đúng màu/kích thước;
- đúng vị trí;
- disabled hoặc safe preview URL;
- không chứa token thật.

---

# 9. Luồng VIEW → EDIT → FINAL_PREVIEW → SEND

## 9.1 VIEW

Bấm mắt mở preview chỉ đọc:

```text
Người nhận
CC/BCC
Tiêu đề

Body đã parse
Action block đúng vị trí
Sender section
Footer

Attachments
Reply-To
```

Nút:

```text
[Đóng] [Chỉnh sửa] [Gửi với nội dung này]
```

`Chỉnh sửa` chỉ hiện khi:

```text
actor có quyền gửi
AND
template = AVAILABLE_EDITABLE_RUNTIME
```

## 9.2 EDIT

Nút:

```text
[Hủy thay đổi]
[Khôi phục từ mẫu]
[Xem trước kết quả]
```

Không có nút gửi trực tiếp.

Mọi thay đổi làm invalid approval token cũ.

## 9.3 FINAL_PREVIEW

Hiển thị đúng nội dung sẽ gửi.

Nút:

```text
[Quay lại chỉnh sửa]
[Gửi email]
```

## 9.4 SEND

Send chỉ dùng `finalPreviewToken`.

Không nhận lại raw body như nguồn quyết định cuối nếu token đã duyệt nội dung.

---

# 10. Signed preview pipeline

## 10.1 Prepare token

Bind:

```text
actorUserId
templateCode
templateRevision
relatedEntityType
relatedEntityId
scope key
recipient hash
subject hash
body hash
attachment hash
replyTo
expiresAt
```

HMAC-SHA256.

Không tạo draft tables.

## 10.2 Final preview token

Sau sanitize và render final:

```text
finalPreviewToken
```

Bind nội dung cuối.

## 10.3 Invalidation

Token cũ mất hiệu lực khi:

- subject đổi;
- body đổi;
- action block di chuyển;
- attachment đổi;
- Reply-To đổi;
- recipients đổi;
- template revision đổi;
- entity state đổi;
- action config đổi.

## 10.4 Stale response

Trả:

```text
EMAIL_PREVIEW_STALE
```

Không âm thầm render lại nội dung khác với thứ người dùng đã xem.

---

# 11. Exact send parity

Backend khi gửi không được:

- parse lại sender;
- append action block ở cuối;
- restore template;
- chèn contact block;
- tự đổi bố cục;
- tự đổi Reply-To;
- thay attachments;
- thay recipient list.

Acceptance:

```text
finalPreviewHtml
≈
HTML body trong .eml/provider output
```

Chỉ chấp nhận khác biệt wrapper MIME/provider.

---

# 12. Reply-To

## 12.1 Mặc định

Email do người dùng gửi:

```text
Reply-To = senderEmail
```

Email tự động:

```text
Reply-To = system support email
```

## 12.2 Field riêng

Không suy ra từ body HTML.

Preview hiển thị:

```text
Khi người nhận bấm “Trả lời”, email sẽ gửi tới:
staff.hn@fpt.edu.vn
```

## 12.3 Validation

- email hợp lệ;
- không CR/LF;
- không header injection;
- SMTP và Resend cùng giá trị;
- thay Reply-To invalidate token.

Giữ fix từ commit `7a8fc541`.

---

# 13. Security model

## 13.1 Không tin HTML frontend

Pipeline:

```text
Editor HTML
→ backend normalize
→ backend sanitize
→ backend validate
→ final preview
→ signed token
→ send
```

## 13.2 Thẻ HTML allow-list

Cho phép:

```text
p
br
strong
em
u
s
ul
ol
li
blockquote
table
tbody
thead
tr
td
th
a
img
h1
h2
h3
h4
div
span
hr
```

Cấm:

```text
script
iframe
form
input
button tùy ý
object
embed
video
audio
svg
canvas
style
link
meta
```

## 13.3 Attribute allow-list

Cho:

```text
href
src
alt
title
width
height
colspan
rowspan
role
style
data-system-block
```

Chỉ với context phù hợp.

Cấm:

```text
onclick
onerror
onload
onmouseover
srcdoc
formaction
```

## 13.4 CSS allow-list

Cho:

```text
text-align
font-size
font-family
font-weight
font-style
text-decoration
color
background-color
line-height
margin
margin-left
margin-right
margin-top
margin-bottom
padding
padding-left
padding-right
padding-top
padding-bottom
border
border-radius
width
max-width
```

Cấm:

```text
position
z-index
transform
animation
transition
behavior
expression
filter
clip-path
external url()
```

## 13.5 Link validation

Chỉ cho:

```text
https:
http:
mailto:
tel:
```

Cấm:

```text
javascript:
data:
file:
vbscript:
```

## 13.6 Image upload

Không cho raw image HTML tùy ý.

Flow:

```text
Upload
→ verify MIME thật
→ size limit
→ malware/file scan nếu có
→ authorized storage
→ CID hoặc approved URL
→ insert
```

Không tin extension.

## 13.7 Tracking/hidden content

Chặn:

- 1x1 tracking pixel ngoài allow-list;
- opacity 0;
- display none;
- visibility hidden;
- font-size 0;
- off-screen positioning.

---

# 14. Editor ↔ Preview ↔ Email fidelity

## 14.1 Một renderer dùng chung

Không được:

```text
Editor renderer A
Admin preview renderer B
Runtime preview renderer C
Email send renderer D
```

Phải:

```text
Editor serializer
→ shared normalizer
→ shared sanitizer
→ shared variable renderer
→ shared system block renderer
→ preview/send
```

## 14.2 Dirty-state canonicalizer

Load rồi không sửa không được báo dirty.

Canonicalizer cần normalize:

- `<br>` vs `<br/>`;
- style ordering;
- quote style;
- insignificant whitespace;
- empty paragraph;
- tag casing;
- editor-added wrapper.

So sánh semantic HTML, không so raw string.

## 14.3 Save/reload round trip

```text
load HTML
→ editor model
→ save HTML
→ reload
```

phải canonical-equivalent.

---

# 15. Chuẩn hóa nội dung mặc định của 31 template

## 15.1 Mục tiêu

Mỗi email phải trả lời:

```text
1. Email nói về việc gì?
2. Liên quan tới ai/chuyến nào?
3. Thông tin quan trọng là gì?
4. Người nhận cần làm gì?
5. Trả lời về đâu?
```

## 15.2 Bố cục chuẩn theo nhóm

### OTP/security

```text
Lời chào
→ mục đích
→ OTP/action nổi bật
→ expiry
→ security note
→ footer
```

### Account/role

```text
Trạng thái mới
→ chi tiết thay đổi
→ hiệu lực
→ ảnh hưởng quyền/đăng nhập
→ next step
→ sender nếu cần
→ footer
```

### Logistics

```text
Lời chào
→ tóm tắt yêu cầu
→ bảng thông tin
→ mô tả chi tiết
→ khu vực phản hồi
→ action block
→ sender
→ footer
```

### Invitation/assignment

```text
Lời chào
→ lý do được mời/phân công
→ bảng chuyến thăm
→ vai trò
→ host message
→ yêu cầu phản hồi
→ action block
→ sender
→ footer
```

### Reminder

```text
Lời nhắc ngắn
→ thời gian/địa điểm
→ checklist
→ action nếu thật sự cần
→ footer
```

### Report/invoice

```text
Lời chào
→ tên báo cáo
→ kỳ báo cáo
→ nội dung file
→ attachment note
→ việc cần làm
→ footer
```

### Setup progress

```text
Lời chào
→ tóm tắt chuyến
→ setupSummaryBlock
→ điểm cần lưu ý
→ attachment note
→ hướng dẫn phản hồi
→ sender
→ footer
```

## 15.3 Sender card chuẩn

```html
<div style="margin:20px 0 0;padding:14px 16px;
            background:#f8fafc;border:1px solid #e2e8f0;
            border-radius:8px">
  <p style="margin:0 0 8px;font-size:12px;
            font-weight:700;color:#475569">
    Thông tin người gửi
  </p>
  <p style="margin:0;line-height:1.65;color:#334155">
    <strong>{{senderName}}</strong><br/>
    {{senderRole}}<br/>
    {{senderDepartment}}<br/>
    {{senderEmail}}
  </p>
</div>
```

Chỉ dùng khi cần.

## 15.4 Action section chuẩn

```html
<div style="margin:20px 0;padding:16px 18px;
            background:#eff6ff;border:1px solid #bfdbfe;
            border-radius:8px">
  <p style="margin:0 0 12px;font-weight:700;color:#0f3d67">
    Phản hồi được yêu cầu
  </p>
  <p style="margin:0 0 14px;color:#334155">
    Vui lòng chọn một phương án bên dưới để chúng tôi tiếp tục xử lý.
  </p>
  {{actionBlock}}
</div>
```

## 15.5 Bảng summary

Dùng table inline style, không dùng flex/grid.

## 15.6 Subject standard

- bắt đầu `[PEMS]`;
- rõ loại email;
- có identifier hữu ích;
- không mơ hồ;
- VI/EN tương đương;
- không lộ dữ liệu nhạy cảm.

---

# 16. HTML email compatibility

Bắt buộc:

- inline CSS;
- table layout cho summary;
- `border-collapse`;
- font fallback;
- responsive width hợp lý;
- không JS;
- không form;
- không external stylesheet;
- không flex/grid cho layout chính;
- không animation;
- không video embed;
- không CSS tùy ý.

Test:

- Gmail;
- Outlook;
- mobile width.

---

# 17. Frontend changes

## 17.1 Shared editor

Tạo:

```text
EmailRichTextEditor.tsx
```

Và config:

```text
emailEditorCapabilities.ts
emailEditorSanitizerPolicy.ts
emailEditorSystemNodes.ts
emailEditorVariableChips.ts
```

## 17.2 TemplateManagement

- dùng shared editor;
- variable picker;
- VI/EN tabs;
- action system node;
- sample preview;
- canonical dirty-state comparison.

## 17.3 EmailPreviewModal

State:

```text
VIEW
EDIT
FINAL_PREVIEW
SENDING
```

- `VIEW` read-only;
- `EDIT` dùng shared editor;
- `FINAL_PREVIEW` exact render;
- invalidation khi sửa;
- không send trong EDIT.

## 17.4 Call sites

Migrate:

- `LogisticsRequestSection`
- `ParticipantInvitationSection`
- `SharedDashboardView`
- `VisitSetupProgressComposer`
- `EmailComposeModal`
- `ManualEmailSender`

---

# 18. Backend changes

## 18.1 Sender

- capability registry;
- sender resolver;
- system sender fallback;
- one-pass substitution.

## 18.2 Preview

- prepare endpoint;
- final preview endpoint;
- signed token;
- stale detection;
- exact send.

## 18.3 Sanitizer

Dùng một policy chung cho:

- save template;
- admin preview;
- runtime preview;
- authored body;
- final preview;
- send.

## 18.4 System node renderer

- action node;
- setup summary block nếu cần;
- tokenized action render;
- count validation.

## 18.5 Audit

Lưu:

```text
actorUserId
templateCode
templateRevision
relatedEntityType
relatedEntityId
wasEdited
replyTo
finalPreviewTokenHash
sentEmailId
sentAt
```

Không dùng body sửa để xác định actor.

---

# 19. Cleanup contact architecture

Xóa/loại bỏ:

```text
Emails/Contact/
EmailContactPolicy
ContactSettingsPanel
EmailContactOverrideSection
contact-preview
contact-candidates
contactOverride
lockedContactBlockHtml
contactInformationBlock
contact translations
contact tests cũ
```

Không đụng:

```text
VisitContactClaim
VisitContactTransfer
Primary Contact phía khách
```

Đây là nghiệp vụ khác.

---

# 20. SQL/default/template sync

Nếu body thay đổi, cập nhật đồng thời:

```text
email-template-defaults.json
canonical SQL
2026-08-05_email_sender_variables.sql
02_sync_templates.sql
test fixtures
parity snapshots
CanonicalSqlScript.ExpectedSha256
```

Không để một nguồn body cũ restore lại contact block.

Task này:

- không tạo bảng;
- không xóa bảng contact policy;
- không restore draft tables.

---

# 21. Guard chống xóa nhầm database

Canonical SQL có hard-code `USE pems_db`.

Bắt buộc:

1. test/import tooling tạo DB disposable;
2. preprocess `CREATE DATABASE/USE`;
3. fail nếu target là `pems_db`;
4. log target;
5. dừng trước `DROP TABLE`;
6. có regression test;
7. không phụ thuộc selected schema trong Workbench.

---

# 22. Test strategy

## 22.1 Backend unit

- sender resolve;
- system sender;
- capability;
- one-pass substitution;
- sanitizer;
- URL/image validation;
- system node count;
- preview token;
- stale token;
- Reply-To;
- SMTP/Resend parity;
- semantic HTML canonicalization.

## 22.2 Frontend unit

- shared editor toolbar;
- TEMPLATE/COMPOSE capability;
- variable chip;
- action node move;
- action node non-editable;
- multiple spaces normalization;
- indent/alignment;
- dirty-state;
- VIEW/EDIT/FINAL_PREVIEW;
- token invalidation.

## 22.3 Integration

- logistics;
- invitation;
- department assignment;
- setup progress;
- automated sender;
- final preview vs `.eml`;
- action position;
- attachments;
- Reply-To;
- VI/EN;
- template SQL parity.

## 22.4 Browser smoke

Tối thiểu:

1. Logistics/Teabreak.
2. Participant invitation.
3. Department assignment.
4. Setup progress.
5. Template management VI/EN.

Kiểm tra:

- toolbar;
- indent;
- alignment;
- table;
- link;
- image;
- action move;
- final preview;
- email output;
- Gmail/Outlook/mobile.

---

# 23. Integration failures cần xử lý

Kết quả gần nhất:

```text
1362 / 1377 PASS
15 FAIL
```

Nhóm:

- declared-but-unused sender vars;
- `02_sync_templates.sql`;
- authored/logistics/report E2E;
- VisitContactClaim/Transfer baseline;
- các lỗi còn lại theo output thật.

Không sửa test chỉ để xanh.

---

# 24. Thứ tự triển khai

## G0 — Preflight

- branch/HEAD;
- WIP/stash;
- backup diff;
- no reset;
- no push;
- outbound mail safety.

## G1 — Shared editor foundation

- component;
- toolbar;
- serializer;
- normalizer;
- sanitizer;
- canonicalizer.

## G2 — Template mode

- variables;
- VI/EN;
- action node;
- preview sample;
- dirty-state.

## G3 — Compose mode

- VIEW/EDIT/FINAL_PREVIEW;
- canEdit;
- action move;
- sender edit;
- Reply-To;
- attachment.

## G4 — Backend exact preview/send

- prepare token;
- final token;
- stale;
- exact send;
- audit.

## G5 — Action inline parity

- position;
- count;
- preview;
- send.

## G6 — Template content rewrite

- logistics/invitation;
- setup progress;
- account;
- reports;
- reminders;
- OTP/security.

## G7 — Call site migration

- logistics;
- invitations;
- shared dashboard;
- setup progress;
- manual sender.

## G8 — Cleanup contact

- backend;
- frontend;
- API;
- tests;
- translations.

## G9 — SQL sync and DB guard

- defaults;
- canonical;
- patch;
- sync script;
- hash;
- disposable DB guard.

## G10 — Tests

- backend;
- architecture;
- integration;
- frontend;
- browser;
- `.eml`;
- provider parity.

## G11 — Commits

Đề xuất:

```text
refactor(email): add the shared secure rich-text email editor
feat(email): render movable action blocks inline
feat(email): enforce final-preview exact-send parity
refactor(email): rewrite professional default templates
fix(db): synchronize email template seeds and guard canonical imports
test(email): cover editor preview and send parity
```

Không push.

---

# 25. Acceptance criteria

1. Một editor dùng chung cho template và compose.
2. Toolbar đủ chức năng cơ bản như email client.
3. Không dùng raw HTML editor cho user thường.
4. Không dùng khoảng trắng thường để layout.
5. Có align/indent/table đúng.
6. Variable chip không bị sửa hỏng.
7. Sender one-pass render.
8. Action block nằm trong body.
9. Action block di chuyển được.
10. Action block không sửa token/chức năng.
11. Action bắt buộc có đúng một block.
12. Preview bằng mắt là email hoàn chỉnh.
13. Email editable có nút Chỉnh sửa.
14. EDIT không có nút send.
15. FINAL_PREVIEW bắt buộc sau thay đổi.
16. Gửi đúng final preview.
17. Thay đổi invalidate token.
18. Reply-To đúng SMTP/Resend.
19. Sanitizer backend chặn XSS/injection.
20. Link/image validation an toàn.
21. Paste từ Word/Web được sanitize.
22. Editor/preview/send cùng renderer.
23. Không dirty-state giả.
24. Save/reload canonical-equivalent.
25. 31 template được audit và viết lại chuyên nghiệp.
26. VI/EN parity.
27. Defaults/SQL/patch/sync đồng bộ.
28. Canonical import không thể xóa nhầm `pems_db`.
29. Setup progress dùng cùng pipeline.
30. Không còn email contact UI/backend.
31. Không ảnh hưởng guest Primary Contact.
32. Không tạo bảng mới.
33. Full tests xanh.
34. Browser smoke xanh.
35. `.eml`/provider output khớp final preview.
36. Không push trước khi được yêu cầu.

---

# 26. Format báo cáo bắt buộc

```text
1. Preflight
- Branch:
- HEAD:
- WIP/stash:
- Local commits:

2. Shared editor
- Component:
- Toolbar:
- Template mode:
- Compose mode:
- Serializer/normalizer:
- Dirty-state:

3. Security
- HTML allow-list:
- CSS allow-list:
- Link validation:
- Image validation:
- Paste sanitizer:
- System node protection:

4. Variables
- Sender resolver:
- System sender:
- One-pass rendering:
- Variable chips:

5. Action block
- Inline placement:
- Move behavior:
- Count validation:
- Token protection:
- Final render:

6. Preview pipeline
- VIEW:
- EDIT:
- FINAL_PREVIEW:
- HMAC token:
- Stale handling:
- Exact send:

7. Template rewrite
- 31-template audit:
- Groups rewritten:
- VI/EN parity:
- Gmail/Outlook/mobile:

8. Frontend call sites
- Logistics:
- Invitation:
- Department:
- Setup progress:
- Manual compose:

9. Cleanup
- Contact backend:
- Contact frontend:
- Routes:
- Tests/translations:
- Guest Primary Contact unaffected:

10. SQL and DB safety
- Defaults:
- Canonical:
- Patch:
- 02_sync_templates:
- Hash:
- DB guard:

11. Tests
- Backend unit:
- Architecture:
- Integration:
- Frontend:
- Browser smoke:
- .eml/provider parity:

12. Schema
- Changed: NO
- New tables: NO
- Draft tables restored: NO

13. Commits
- SHA/message:

14. Remaining debt
- Chỉ ghi debt có bằng chứng.
```
