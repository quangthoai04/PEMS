# PEMS — MASTER IMPLEMENTATION PLAN V4
## Email Sender Variables, Shared Secure Editor, Inline Action Block, Professional Templates, Preview Fidelity và Exact Send

---

## 0. Phạm vi tài liệu

Tài liệu này hợp nhất, chuẩn hóa và loại bỏ trùng lặp từ bốn tài liệu:

1. `PEMS_REMOVE_CONTACT_USE_SENDER_VARIABLES_PREVIEW_EDIT_FINAL_SEND_IMPLEMENTATION_PLAN.md`
2. `PEMS_EMAIL_PREVIEW_EDIT_ACTION_BLOCK_FINAL_SEND_MISSING_LOGIC_PLAN.md`
3. `PEMS_EMAIL_PREVIEW_EDIT_ACTION_BLOCK_FINAL_SEND_AND_TEMPLATE_CONTENT_PLAN_V2.md`
4. `PEMS_EMAIL_FULL_EDITOR_PREVIEW_ACTION_SENDER_TEMPLATE_IMPLEMENTATION_PLAN_V3.md`

Tài liệu này là nguồn triển khai ưu tiên cao nhất cho phạm vi email hiện tại.

Nếu nội dung giữa bốn tài liệu cũ khác nhau, áp dụng quyết định cuối cùng trong tài liệu V4 này:

```text
Không còn email contact architecture.
Chỉ dùng sender variables.
Dùng một rich-text editor chung cho TEMPLATE và COMPOSE.
Action block nằm trong body và có thể di chuyển nhưng không thể sửa chức năng.
Mọi email đã chỉnh phải đi qua FINAL_PREVIEW trước khi gửi.
Email thực gửi phải khớp Final Preview.
Không tạo bảng draft mới.
Không thay đổi schema trong scope này.
```

---

# 1. Mục tiêu tổng thể

Hoàn thiện hệ thống email PEMS theo luồng:

```text
TEMPLATE CONFIGURATION
→ PREPARE
→ VIEW
→ EDIT
→ FINAL_PREVIEW
→ SEND
```

Các mục tiêu bắt buộc:

1. Loại bỏ hoàn toàn khái niệm `contact` khỏi email template và compose UI.
2. Chỉ sử dụng thông tin người gửi thông qua `sender variables`.
3. Dùng một editor chung có khả năng soạn gần giống email client thông thường.
4. Editor phải đủ chức năng nhưng không cho raw HTML/CSS tùy ý.
5. Preview phải hiển thị đúng email hoàn chỉnh.
6. Action block phải nằm trong nội dung email tại đúng vị trí.
7. Người dùng được di chuyển action block nhưng không sửa token, URL hoặc chức năng.
8. Preview, Final Preview và email gửi thật phải cùng một bố cục.
9. Viết lại nội dung mặc định của 31 template theo tiêu chuẩn chuyên nghiệp.
10. Đồng bộ JSON defaults, canonical SQL, patch SQL và sync script.
11. Thêm guard chống import canonical SQL nhầm vào `pems_db`.
12. Hoàn thành toàn bộ backend, frontend, integration và browser smoke trước khi commit/push.

---

# 2. Quyết định nghiệp vụ cuối cùng

## 2.1 Loại bỏ `contact`

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

Không còn các chức năng email:

- chọn contact;
- nhập contact thủ công;
- contact candidates;
- contact preview;
- contact override;
- contact source;
- contact fallback;
- contact requirement;
- contact policy UI;
- locked contact block;
- trusted contact block.

## 2.2 Chỉ còn `sender`

Các biến chuẩn:

```text
{{senderName}}
{{senderRole}}
{{senderEmail}}
{{senderPhone}}
{{senderDepartment}}
{{senderCampus}}
```

## 2.3 Sender của email do người dùng gửi

`sender` là actor thực hiện thao tác gửi email.

Backend resolve:

```text
CurrentUser.UserId
→ users
→ role/subrole
→ department
→ campus
```

Không nhận sender fields từ frontend.

## 2.4 Sender của email tự động

Nếu không có actor trực tiếp:

```text
senderName       = Bộ phận hỗ trợ PEMS
senderRole       = Hệ thống PEMS
senderEmail      = SupportContact.Email
senderPhone      = SupportContact.Phone
senderDepartment = PEMS
senderCampus     = trống hoặc Toàn hệ thống
```

Tái sử dụng cấu hình `SupportContact` hiện có.

Không tạo bảng mới.

## 2.5 Sender hiển thị khác SMTP From

Sender trong body:

```text
IC Staff Hà Nội
IC Staff
staff.hn@fpt.edu.vn
```

SMTP From có thể vẫn là:

```text
PEMS <no-reply@pems-fpt.site>
```

Không tự đổi SMTP From thành địa chỉ cá nhân.

## 2.6 Reply-To

Mặc định:

```text
Email do actor gửi      → Reply-To = senderEmail
Email tự động           → Reply-To = system support email
```

Reply-To là metadata riêng.

Không đọc body HTML để suy ra Reply-To.

---

# 3. Trạng thái baseline cần bảo toàn

Baseline gần nhất:

```text
Branch: Canh-Iter1
HEAD: 31caebe5
5 commit local chưa push
WIP lớn chưa commit
9 stash cũ không được đụng
```

Các commit local:

```text
ccc6f42a  test(db): unblock the integration suite after the canonical script changed
7a8fc541  fix(email): put the message's own Reply-To on the SMTP envelope
a169fb60  feat(email): add structured per-message contact override
838d4445  feat(frontend): add a controlled contact editor to the email preview
31caebe5  test(email): verify preview-send contact parity
```

Quy tắc:

- Giữ `ccc6f42a`.
- Giữ `7a8fc541`.
- Refactor hoặc loại bỏ logic contact trong ba commit còn lại.
- Không reset.
- Không checkout làm mất WIP.
- Không stash pop không kiểm soát.
- Không push.
- Không commit checkpoint không runnable.

---

# 4. Capability của template

## 4.1 Enum

```text
NOT_AVAILABLE
AVAILABLE_READ_ONLY_RUNTIME
AVAILABLE_EDITABLE_RUNTIME
```

## 4.2 `NOT_AVAILABLE`

Không dùng sender variables, không runtime edit.

Tối thiểu:

- `ACCOUNT_EMAIL_CONFIRMATION`
- `AUTH_PASSWORD_RESET_OTP`
- `VISIT_REQUEST_OTP`

Lưu ý:

`ACCOUNT_EMAIL_CONFIRMATION` có thể dùng system footer tĩnh nhưng không dùng sender cá nhân.

## 4.3 `AVAILABLE_READ_ONLY_RUNTIME`

Có thể dùng sender variables hoặc system sender nhưng không cho runtime edit.

Ví dụ:

- reminders;
- account notices;
- role notices;
- reports;
- background notifications;
- automated system messages.

## 4.4 `AVAILABLE_EDITABLE_RUNTIME`

Cho runtime `VIEW → EDIT → FINAL_PREVIEW → SEND`.

Tối thiểu:

- `LOGISTICS_REQUEST_TO_DEPARTMENT`
- `LOGISTICS_CHANGE_PROPOSAL_TO_HOST`
- `VISIT_PARTICIPANT_INVITATION`
- `VISIT_STUDENT_INVITATION`
- `VISIT_DEPARTMENT_LEADER_INVITATION`
- `VISIT_DEPARTMENT_STAFF_ASSIGNMENT`
- `VISIT_SETUP_PROGRESS_UPDATE`

## 4.5 Quyền chỉnh sửa

Điều kiện:

```text
actor có quyền thực hiện nghiệp vụ gửi
AND
template capability = AVAILABLE_EDITABLE_RUNTIME
```

Không chặn chỉnh sửa riêng theo role nếu actor đã có quyền gửi command đó.

## 4.6 Không suy capability từ body

Không dùng:

```text
body chứa {{senderName}} → editable
```

Phải dùng registry theo `templateCode`.

---

# 5. Một rich-text editor dùng chung

## 5.1 Component chung

Tạo:

```tsx
<EmailRichTextEditor />
```

Hai mode:

```text
TEMPLATE
COMPOSE
```

Không tạo hai editor với:

- thư viện khác nhau;
- toolbar khác nhau;
- serializer khác nhau;
- sanitizer khác nhau;
- renderer khác nhau.

## 5.2 TEMPLATE mode

Dùng trong quản lý mẫu.

Cho phép:

- sửa subject VI/EN;
- sửa body VI/EN;
- chèn business variables;
- chèn sender variables;
- đặt action system node;
- thiết kế bảng;
- thiết kế sender card;
- chèn ảnh được kiểm soát;
- preview bằng sample data;
- lưu nội dung mặc định.

## 5.3 COMPOSE mode

Dùng trước khi gửi email cụ thể.

Cho phép:

- sửa subject;
- sửa nội dung đã parse;
- sửa/xóa nội dung sender đã parse;
- định dạng văn bản;
- chèn link;
- chèn ảnh;
- chèn bảng;
- di chuyển action block;
- chỉnh attachment nếu flow cho phép;
- chỉnh Reply-To nếu actor có quyền.

Không cho:

- chèn variable mới;
- chèn raw trusted placeholder;
- tạo action block mới;
- sửa action URL/token;
- sửa OTP/token;
- sửa system tracking marker;
- sửa attachment bắt buộc trái quyền.

## 5.4 Capability props

```ts
type EmailEditorMode = 'TEMPLATE' | 'COMPOSE';

type EmailEditorCapabilities = {
  allowVariables: boolean;
  allowImages: boolean;
  allowTables: boolean;
  allowLinks: boolean;
  allowSystemBlockInsert: boolean;
  allowSystemBlockMove: boolean;
  allowSystemBlockDelete: boolean;
  allowRawHtml: false;
};
```

---

# 6. Toolbar đầy đủ nhưng có kiểm soát

## 6.1 Chức năng

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
Decrease indent
Increase indent
Insert link
Insert image
Insert table
Insert divider
Clear formatting
Fullscreen
Insert variable
Insert/move system block
```

## 6.2 Font an toàn

Chỉ cho:

```text
Arial
Verdana
Tahoma
Trebuchet MS
Georgia
Times New Roman
Courier New
```

## 6.3 Cỡ chữ

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

## 6.4 Không raw HTML

Không cho user thường:

- mở source HTML;
- nhập `<script>`;
- nhập CSS tùy ý;
- chèn iframe;
- chèn form;
- tạo button tùy ý;
- sửa system node HTML.

---

# 7. Căn chỉnh và khoảng trắng

## 7.1 Không dùng nhiều dấu cách để layout

HTML gom nhiều khoảng trắng liên tiếp.

Ví dụ:

```html
<p>       {{senderPhone}}</p>
```

không đảm bảo dịch sang phải.

## 7.2 Dùng công cụ đúng

Căn lề:

```html
<p style="text-align:right">...</p>
```

Thụt lề:

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

## 7.3 Bảng thay cho căn cột bằng dấu cách

```html
<table role="presentation">
  <tr>
    <td>Số điện thoại</td>
    <td>{{senderPhone}}</td>
  </tr>
</table>
```

## 7.4 UX cho nhiều dấu cách

Khi nhập nhiều dấu cách liên tiếp:

- normalize về một dấu cách;
- hoặc hiển thị cảnh báo:

```text
Dấu cách liên tiếp không được dùng để căn chỉnh trong email.
Vui lòng dùng căn lề, thụt lề hoặc bảng.
```

Không tự đổi hàng loạt thành `&nbsp;`.

---

# 8. Variable model

## 8.1 Variable chip

Trong TEMPLATE mode, biến hiển thị như chip:

```text
[Họ tên người gửi]
[Email người gửi]
[Tên đoàn]
[Thời gian bắt đầu]
```

Không để user sửa trực tiếp dấu `{{ }}`.

## 8.2 One-pass substitution

Pipeline:

```text
Template HTML
→ Match placeholders
→ Encode data
→ Replace đúng một lần
→ Không scan lại output
```

Ví dụ tên trong DB:

```text
Nguyễn {{visitCode}} Văn A
```

phải hiển thị nguyên văn.

## 8.3 Giữ wrapper và style

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

Không strip style hợp lệ quanh variable.

## 8.4 Declared-but-unused

Sender variables có thể được khai báo trong `variables_text` nhưng chưa dùng trong body để admin chèn sau.

Test contract phải cho phép điều này khi sender capability hợp lệ.

Không bỏ validation cho các variable khác.

---

# 9. Action block inline

## 9.1 Vị trí action trong template

Template có:

```text
{{actionBlock}}
```

Action phải xuất hiện đúng vị trí trong body.

Không tách thành section riêng ngoài email.

## 9.2 Action system node trong editor

Không đưa URL/token thật vào editor.

Dùng:

```html
<div data-system-block="action"></div>
```

hoặc:

```json
{
  "type": "system-action-block",
  "blockId": "PRIMARY_ACTION_BLOCK"
}
```

## 9.3 Có thể di chuyển

Người dùng được:

- kéo lên/xuống;
- cắt/dán;
- đặt trước/sau sender section;
- thêm text trước/sau;
- thay đổi spacing bên ngoài.

## 9.4 Không thể sửa chức năng

Không được:

- sửa URL;
- sửa token;
- sửa action ID;
- sửa action type;
- nhân đôi;
- tạo block giả;
- sửa HTML bên trong;
- xóa block bắt buộc.

## 9.5 Count validation

Template action bắt buộc:

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

Template không action:

- không được chèn action node.

Template action optional:

- chỉ xóa nếu backend capability cho phép.

## 9.6 Action preview

Trong preview:

- đúng label;
- đúng màu;
- đúng kích thước;
- đúng vị trí;
- không token thật;
- không action thật;
- disabled hoặc safe preview behavior.

---

# 10. Luồng Preview

## 10.1 VIEW

Bấm biểu tượng mắt mở email hoàn chỉnh chỉ đọc:

```text
TO
CC
BCC
Subject

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

Nếu read-only:

```text
[Đóng] [Gửi với nội dung này]
```

## 10.2 Nút Chỉnh sửa

Phải kiểm tra chuỗi:

```text
templateCode thực tế
→ capability registry
→ prepare response canEdit
→ frontend DTO
→ EmailPreviewModal render
```

Response:

```json
{
  "templateCode": "LOGISTICS_REQUEST_TO_DEPARTMENT",
  "canEdit": true
}
```

## 10.3 EDIT

Nút:

```text
[Hủy thay đổi]
[Khôi phục từ mẫu]
[Xem trước kết quả]
```

Không có nút gửi trực tiếp.

## 10.4 FINAL_PREVIEW

Hiển thị đúng kết quả cuối.

Nút:

```text
[Quay lại chỉnh sửa]
[Gửi email]
```

---

# 11. Signed prepare → final-preview → send pipeline

## 11.1 Prepare response

Đề xuất:

```ts
interface PreparedEmailPreview {
  previewToken: string;
  templateCode: string;
  templateRevision: number;
  subject: string;
  editableBodyHtml: string;
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

## 11.2 HMAC prepare token

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
system block hash
attachment hash
replyTo
expiresAt
```

HMAC-SHA256.

## 11.3 Finalize request

```ts
{
  previewToken: string;
  subject: string;
  editableBodyHtml: string;
  replyToEmail?: string;
  attachments: EmailAttachmentInput[];
}
```

Backend:

1. Verify actor.
2. Verify token.
3. Normalize.
4. Sanitize.
5. Validate variable/system nodes.
6. Validate action count.
7. Render system action.
8. Validate attachments.
9. Validate Reply-To.
10. Build `finalPreviewHtml`.
11. Issue `finalPreviewToken`.

## 11.4 Send request

```ts
{
  finalPreviewToken: string;
}
```

Không cần gửi lại raw body làm nguồn quyết định cuối.

## 11.5 Token invalidation

Invalidate khi:

- subject đổi;
- body đổi;
- action block di chuyển;
- attachment đổi;
- Reply-To đổi;
- recipient đổi;
- template revision đổi;
- entity state đổi;
- action configuration đổi.

## 11.6 Stale

Trả:

```text
EMAIL_PREVIEW_STALE
```

Không tự render lại rồi gửi nội dung khác.

## 11.7 Không draft tables

Không tạo lại:

```text
email_drafts
email_draft_recipients
email_draft_attachments
```

---

# 12. Exact send parity

Khi gửi, backend không được:

- parse lại sender;
- append action block ở cuối;
- append contact block;
- khôi phục template;
- tự đổi spacing;
- tự đổi attachment;
- tự đổi recipient;
- tự đổi Reply-To;
- render body theo pipeline khác.

Acceptance:

```text
finalPreviewHtml
≈
HTML body trong .eml hoặc provider output
```

Chỉ chấp nhận khác wrapper MIME/provider.

---

# 13. Reply-To

## 13.1 Hiển thị trong preview

```text
Khi người nhận bấm “Trả lời”, email sẽ gửi tới:
staff.hn@fpt.edu.vn
```

## 13.2 Validation

- email hợp lệ;
- không CR/LF;
- không header injection;
- SMTP và Resend cùng giá trị;
- thay đổi Reply-To invalidate token.

## 13.3 Transport parity

Giữ sửa lỗi SMTP từ commit:

```text
7a8fc541
```

Bổ sung test SMTP/Resend parity.

---

# 14. Security model

## 14.1 Không tin HTML từ frontend

```text
Editor HTML
→ Backend normalizer
→ Backend sanitizer
→ Backend validator
→ Final Preview
→ Signed token
→ Send
```

## 14.2 HTML allow-list

Cho:

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
thead
tbody
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

## 14.3 Attribute allow-list

Cho theo context:

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

Cấm:

```text
onclick
onerror
onload
onmouseover
srcdoc
formaction
```

## 14.4 CSS allow-list

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

## 14.5 Link

Chỉ:

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

## 14.6 Image upload

```text
Upload
→ Verify MIME thật
→ Size limit
→ Scan/validate
→ Authorized storage
→ CID hoặc approved URL
→ Insert
```

Không tin extension.

## 14.7 Chặn nội dung ẩn

Chặn:

- tracking pixel ngoài allow-list;
- `opacity:0`;
- `display:none`;
- `visibility:hidden`;
- `font-size:0`;
- off-screen positioning;
- hidden links;
- invisible text.

## 14.8 Paste sanitizer

Paste từ Word/Gmail/Web:

- giữ basic formatting;
- giữ table đơn giản;
- giữ link hợp lệ;
- loại script;
- loại event handler;
- loại external CSS;
- loại unsupported font;
- loại absolute/fixed positioning;
- loại hidden/tracking content.

---

# 15. Editor, Preview và Email fidelity

## 15.1 Một pipeline chung

Phải:

```text
Editor serializer
→ Shared normalizer
→ Shared sanitizer
→ Shared variable renderer
→ Shared system block renderer
→ Admin Preview / Runtime Preview / Final Preview / Send
```

Không dùng renderer khác nhau.

## 15.2 Dirty-state canonicalizer

Normalize:

- `<br>` vs `<br/>`;
- style order;
- quote style;
- tag casing;
- insignificant whitespace;
- empty paragraph;
- editor wrapper;
- equivalent paragraph/newline form.

So sánh semantic HTML.

## 15.3 Save/reload round trip

```text
load
→ editor
→ serialize
→ save
→ reload
```

phải canonical-equivalent.

Không vừa mở đã báo dirty.

## 15.4 Style fidelity

Test:

- text-align;
- margin-left;
- padding;
- table;
- sender block;
- action block;
- footer;
- variable wrapper;
- VI/EN.

---

# 16. Chuẩn hóa nội dung 31 template

## 16.1 Mục tiêu

Mỗi email trả lời:

1. Email này nói về việc gì?
2. Liên quan tới ai/chuyến nào?
3. Thông tin quan trọng nhất là gì?
4. Người nhận cần làm gì?
5. Khi cần phản hồi thì gửi về đâu?

## 16.2 Nguyên tắc chung

- câu ngắn;
- giọng văn chuyên nghiệp;
- hierarchy rõ;
- không lặp thông tin;
- không lộ thuật ngữ kỹ thuật;
- mobile-friendly;
- VI/EN parity;
- action rõ;
- attachment rõ;
- sender chỉ khi cần.

## 16.3 Khối mở đầu

```html
<p style="margin:0 0 16px">
  Kính gửi <strong>{{recipientName}}</strong>,
</p>
```

Dùng:

- `Kính gửi` cho khách/đối tác;
- `Xin chào` cho nội bộ.

## 16.4 Bảng tóm tắt

```html
<table role="presentation"
       width="100%"
       cellpadding="0"
       cellspacing="0"
       style="border-collapse:collapse;margin:18px 0;
              border:1px solid #dbe4ee;border-radius:8px">
  <tr>
    <td style="padding:10px 14px;color:#64748b;width:34%;
               border-bottom:1px solid #e5e7eb">
      Hạng mục
    </td>
    <td style="padding:10px 14px;font-weight:600;
               border-bottom:1px solid #e5e7eb">
      {{logisticsTitle}}
    </td>
  </tr>
</table>
```

## 16.5 Action section

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

## 16.6 Warning block

```html
<div style="margin:18px 0;padding:14px 16px;
            background:#fff7ed;border:1px solid #fed7aa;
            border-radius:8px;color:#9a3412">
  <strong>Lưu ý bảo mật:</strong>
  Không chia sẻ mã hoặc liên kết này với bất kỳ ai.
</div>
```

## 16.7 Sender card

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

## 16.8 Footer

```html
<p style="margin:22px 0 0;color:#64748b;font-size:12px;line-height:1.6">
  Trân trọng,<br/>
  <strong>PEMS – FPT University</strong>
</p>
```

## 16.9 Nhóm OTP/security

```text
Lời chào
→ Mục đích
→ OTP/action
→ Expiry
→ Security note
→ Footer
```

Không sender cá nhân.

## 16.10 Nhóm account/role

```text
Trạng thái mới
→ Chi tiết thay đổi
→ Hiệu lực
→ Ảnh hưởng quyền
→ Next step
→ Sender nếu cần
→ Footer
```

## 16.11 Nhóm logistics

```text
Lời chào
→ Tóm tắt
→ Bảng hạng mục
→ Mô tả
→ Action section
→ Sender
→ Footer
```

## 16.12 Invitation/assignment

```text
Lời chào
→ Lý do được mời/phân công
→ Bảng chuyến thăm
→ Vai trò
→ Host message
→ Action
→ Sender
→ Footer
```

Phân biệt:

- invitation;
- assignment;
- transfer;
- claim.

## 16.13 Reminder

```text
Lời nhắc
→ Thời gian/địa điểm
→ Checklist
→ Action nếu có nghiệp vụ thực
→ Footer
```

## 16.14 Report/invoice

```text
Lời chào
→ Tên báo cáo
→ Kỳ báo cáo
→ Nội dung file
→ Attachment note
→ Việc cần làm
→ Footer
```

Không bịa deadline nếu không có biến.

## 16.15 Setup progress

```text
Lời chào
→ Tóm tắt chuyến
→ setupSummaryBlock
→ Điểm cần lưu ý
→ Schedule Report attachment
→ Hướng dẫn phản hồi
→ Sender
→ Footer
```

## 16.16 Subject

- bắt đầu `[PEMS]`;
- rõ loại email;
- có identifier hữu ích;
- không mơ hồ;
- không dữ liệu nhạy cảm;
- VI/EN tương đương.

---

# 17. HTML email compatibility

Bắt buộc:

- inline CSS;
- table layout;
- `border-collapse`;
- font fallback;
- safe width;
- no JS;
- no form;
- no external stylesheet;
- no flex/grid cho layout chính;
- no animation;
- no video embed;
- no arbitrary CSS.

Test:

- Gmail;
- Outlook;
- mobile width.

---

# 18. Frontend implementation

## 18.1 File/component chính

Đề xuất:

```text
EmailRichTextEditor.tsx
emailEditorCapabilities.ts
emailEditorSanitizerPolicy.ts
emailEditorSystemNodes.ts
emailEditorVariableChips.ts
emailHtmlCanonicalizer.ts
```

## 18.2 TemplateManagement

- shared editor;
- VI/EN tabs;
- variable picker;
- action node;
- sample preview;
- semantic dirty check;
- save/reload parity.

## 18.3 EmailPreviewModal

State:

```text
VIEW
EDIT
FINAL_PREVIEW
SENDING
```

- VIEW read-only;
- EDIT shared editor;
- FINAL_PREVIEW exact;
- invalidation khi sửa;
- no send in EDIT.

## 18.4 Call sites

Migrate:

- `LogisticsRequestSection`
- `ParticipantInvitationSection`
- `SharedDashboardView`
- `VisitSetupProgressComposer`
- `EmailComposeModal`
- `ManualEmailSender`

## 18.5 Setup-progress

Không là ngoại lệ.

Phải dùng cùng pipeline.

---

# 19. Backend implementation

## 19.1 Sender

- capability registry;
- sender resolver;
- system sender;
- one-pass substitution.

## 19.2 Preview services

- prepare preview;
- finalize preview;
- signed token;
- stale detection;
- exact send.

## 19.3 Sanitizer

Một policy dùng chung cho:

- save template;
- admin preview;
- runtime preview;
- edited body;
- final preview;
- send.

## 19.4 System node renderer

- action system node;
- setup summary system block nếu cần;
- validate count;
- render tokenized action cuối cùng.

## 19.5 Approved content

Các business command dùng approved/final content thay cho legacy `EmailOverride`.

Không làm side-effect nghiệp vụ trong generic preview service.

## 19.6 Audit

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

Không xác định actor từ body đã sửa.

---

# 20. Cleanup contact architecture

Xóa/refactor:

```text
Emails/Contact/
EmailContactPolicy entity
EmailContactPolicy DbSet
EmailContactPolicy EF mapping
EmailContactBlockText
EmailContactEnums
ContactScope
ContactOverride
EmailContactOverrideInput
EmailContactOverrideValidator
EmailContactCandidates
EmailContactPreviewResult
ResolveEmailContactPreviewQuery
IEmailContactCandidateService
ContactSettingsPanel
EmailContactOverrideSection
contactBlock.ts
contact-preview routes
contact-candidates routes
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

Schema contact cũ có thể giữ tạm nhưng code không đọc/ghi.

Không drop schema trong task này.

---

# 21. SQL, defaults và template synchronization

Nếu body thay đổi, cập nhật cùng lúc:

```text
email-template-defaults.json
canonical SQL
2026-08-05_email_sender_variables.sql
02_sync_templates.sql
test fixtures
parity snapshots
CanonicalSqlScript.ExpectedSha256
```

Yêu cầu:

- 31 templates;
- VI/EN;
- 28 templates có sender variables nếu capability cho phép;
- 0 `{{contactInformationBlock}}`;
- patch idempotent;
- không mojibake;
- second run byte-equivalent;
- sync script không restore body cũ.

Không tạo/xóa bảng.

---

# 22. Guard chống xóa nhầm database

Canonical SQL có hard-code:

```sql
USE pems_db;
```

Đã từng rebuild local `pems_db`.

Bắt buộc:

1. Import tool tạo database disposable tên ngẫu nhiên.
2. Preprocess `CREATE DATABASE` và `USE`.
3. Fail nếu target = `pems_db`.
4. Log target rõ ràng.
5. Dừng trước mọi `DROP TABLE`.
6. Có regression test.
7. Không phụ thuộc current schema trong Workbench.
8. Scratch DB được drop sau test.
9. Không chạy canonical file trực tiếp bằng test command không guard.

---

# 23. Integration failures hiện tại cần xử lý

Kết quả gần nhất:

```text
1362 / 1377 PASS
15 FAIL
```

## 23.1 Declared-but-unused sender variables

Cho exemption có điều kiện.

## 23.2 `02_sync_templates.sql`

Cập nhật body mới.

## 23.3 Authored/logistics/report E2E

Chẩn đoán từ output thật:

- scope key;
- approved content;
- action placement;
- attachment hash;
- Reply-To;
- legacy override expectation;
- final body parity.

## 23.4 VisitContactClaim/Transfer

Thiết lập baseline sạch.

Không sửa guest Primary Contact chỉ để xanh.

---

# 24. Test strategy

## 24.1 Backend unit

- capability;
- sender resolver;
- system sender;
- one-pass substitution;
- sender value có `{{ }}`;
- HTML sanitizer;
- CSS sanitizer;
- URL validation;
- image validation;
- hidden content;
- action count;
- token HMAC;
- token expiry;
- stale token;
- Reply-To;
- SMTP/Resend parity;
- HTML canonicalizer.

## 24.2 Frontend unit

- shared editor;
- toolbar;
- font;
- size;
- alignment;
- indent;
- table;
- link;
- image;
- paste sanitize;
- variable chip;
- action node;
- action move;
- action non-editable;
- action duplicate prevention;
- dirty state;
- VIEW;
- EDIT;
- FINAL_PREVIEW;
- token invalidation.

## 24.3 Integration

- logistics;
- invitation;
- department assignment;
- setup progress;
- automated sender;
- final preview vs `.eml`;
- action position;
- sender body;
- attachments;
- Reply-To;
- VI/EN;
- SQL parity;
- sync parity;
- patch idempotence.

## 24.4 Browser smoke

Tối thiểu:

1. Logistics/Teabreak.
2. Participant invitation.
3. Department assignment.
4. Setup progress.
5. Template management VI/EN.

Kiểm tra:

- toolbar;
- spacing;
- alignment;
- indent;
- table;
- image;
- link;
- variable;
- action move;
- preview;
- final preview;
- real email;
- Gmail;
- Outlook;
- mobile.

---

# 25. Thứ tự triển khai thống nhất

## G0 — Preflight

- branch/HEAD;
- WIP/stash;
- save diff;
- no reset;
- no push;
- outbound mail safety;
- DB target safety.

## G1 — Audit

- 31 template matrix;
- capability matrix;
- editable/read-only flows;
- contact leftovers;
- call sites;
- current test failures.

## G2 — Shared editor foundation

- component;
- toolbar;
- serializer;
- normalizer;
- sanitizer;
- canonicalizer;
- paste;
- links;
- images.

## G3 — Template mode

- variable chips;
- VI/EN;
- action node;
- sample preview;
- dirty state;
- save/reload.

## G4 — Compose mode

- canEdit;
- VIEW;
- EDIT;
- FINAL_PREVIEW;
- action move;
- sender edit;
- Reply-To;
- attachments.

## G5 — Backend exact preview/send

- prepare token;
- finalize token;
- stale;
- approved content;
- exact send;
- audit.

## G6 — Action parity

- inline action;
- count validation;
- no duplicate;
- final position;
- token protection.

## G7 — Template rewrite

Thứ tự:

1. Logistics.
2. Invitations.
3. Setup progress.
4. Account/role.
5. Reports/invoices.
6. Reminders.
7. OTP/security.

## G8 — Call site migration

- logistics;
- invitation;
- shared dashboard;
- setup progress;
- manual compose;
- automated flows audit.

## G9 — Cleanup contact

- backend;
- frontend;
- routes;
- DI;
- tests;
- translations.

## G10 — SQL/default sync và DB guard

- defaults;
- canonical;
- patch;
- sync script;
- hash;
- disposable DB guard.

## G11 — Full tests

- build;
- unit;
- architecture;
- integration;
- frontend;
- browser;
- `.eml`;
- provider parity.

## G12 — Commit

Đề xuất:

```text
refactor(email): remove contact policies and add sender variables
refactor(email): add the shared secure rich-text editor
feat(email): render movable action blocks inline
feat(email): enforce final-preview exact-send parity
refactor(email): rewrite professional default templates
fix(db): synchronize email template seeds and guard canonical imports
test(email): cover editor preview and send parity
```

Không push.

---

# 26. Acceptance criteria

1. Không còn email contact UI.
2. Không còn email contact runtime architecture.
3. Không ảnh hưởng guest Primary Contact.
4. Chỉ còn sender variables.
5. Sender từ backend, không từ frontend.
6. Một shared editor cho TEMPLATE và COMPOSE.
7. Toolbar đủ chức năng email cơ bản.
8. Không raw HTML cho user thường.
9. Không căn layout bằng nhiều dấu cách.
10. Có alignment, indent và table.
11. Variables hiển thị như chip.
12. One-pass substitution.
13. Style quanh variable được giữ.
14. Action block nằm trong body.
15. Action block di chuyển được.
16. Action block không sửa token/chức năng.
17. Action bắt buộc có đúng một block.
18. Preview là email hoàn chỉnh.
19. Template editable có nút Chỉnh sửa.
20. EDIT không có nút gửi.
21. Final Preview bắt buộc sau khi sửa.
22. Thay đổi invalidate token.
23. Send đúng Final Preview.
24. Reply-To đúng SMTP và Resend.
25. Backend sanitizer chặn XSS/injection.
26. Links được validate.
27. Images được validate.
28. Paste được sanitize.
29. Hidden/tracking content bị chặn.
30. Editor/preview/send cùng pipeline.
31. Không dirty state giả.
32. Save/reload canonical-equivalent.
33. 31 template được audit.
34. Nội dung mặc định chuyên nghiệp.
35. VI/EN parity.
36. Action placement nhất quán.
37. Report có attachment note.
38. OTP có expiry/security note.
39. Setup progress dùng cùng pipeline.
40. Defaults/canonical/patch/sync đồng bộ.
41. Patch idempotent.
42. Không mojibake.
43. Canonical import không thể chạy nhầm vào `pems_db`.
44. Không tạo bảng mới.
45. Không restore draft tables.
46. Backend build xanh.
47. Backend unit xanh.
48. Architecture xanh.
49. Integration xanh.
50. Frontend typecheck/lint/build xanh.
51. Frontend tests xanh.
52. Browser smoke xanh.
53. `.eml`/provider parity xanh.
54. Không push trước khi được yêu cầu.

---

# 27. Format báo cáo cuối

```text
1. Preflight
- Branch:
- HEAD before/after:
- WIP/stash:
- Local commits preserved:
- Database target safety:

2. Audit
- Templates:
- Sender capability:
- Editable flows:
- Read-only flows:
- Contact leftovers:
- Call sites:

3. Shared editor
- Component:
- Toolbar:
- Template mode:
- Compose mode:
- Serializer:
- Normalizer:
- Canonicalizer:
- Dirty-state:

4. Security
- HTML allow-list:
- Attribute allow-list:
- CSS allow-list:
- Links:
- Images:
- Paste:
- Hidden content:
- Backend re-sanitize:

5. Sender variables
- Registry:
- User sender:
- System sender:
- One-pass:
- Variable chips:
- Declared-unused policy:

6. Action block
- Template placement:
- Editor node:
- Move behavior:
- Count validation:
- Token protection:
- Preview rendering:
- Send rendering:

7. Preview pipeline
- VIEW:
- EDIT:
- FINAL_PREVIEW:
- Prepare token:
- Final token:
- Stale:
- Exact send:

8. Reply-To
- Default:
- Editable:
- Validation:
- SMTP:
- Resend:

9. Template rewrite
- 31-template audit:
- Logistics:
- Invitations:
- Setup progress:
- Account/role:
- Reports:
- Reminders:
- OTP/security:
- VI/EN:
- Gmail/Outlook/mobile:

10. Call sites
- Logistics:
- Invitations:
- Department:
- Setup progress:
- Manual compose:
- Automated flows:

11. Contact cleanup
- Backend:
- Frontend:
- Routes:
- DI:
- Tests:
- Translations:
- Guest Primary Contact unaffected:

12. SQL/default sync
- Defaults JSON:
- Canonical:
- Patch:
- 02_sync_templates:
- Hash:
- Idempotence:
- Mojibake:
- DB guard:

13. Tests
- Backend build:
- Backend unit:
- Architecture:
- Integration:
- Frontend typecheck:
- Frontend lint:
- Frontend unit:
- Frontend build:
- Browser smoke:
- .eml/provider parity:

14. Schema
- Schema changed: NO
- New tables: NO
- Draft tables restored: NO

15. Commits
- SHA:
- Message:

16. Remaining debt
- Chỉ ghi debt có bằng chứng.
```

---

# 28. Source requirement traceability

| Tài liệu nguồn | Yêu cầu chính đã được hợp nhất |
|---|---|
| Remove Contact / Sender Variables | §2, §4, §8, §10–13, §19–21 |
| Missing Logic Plan | §9–13, §18, §21–25 |
| Action + Template Content V2 | §7, §15–17, §21–24 |
| Full Editor V3 | §5–8, §14–18, §24–27 |

Tài liệu V4 này thay thế việc triển khai riêng lẻ theo bốn file cũ.
