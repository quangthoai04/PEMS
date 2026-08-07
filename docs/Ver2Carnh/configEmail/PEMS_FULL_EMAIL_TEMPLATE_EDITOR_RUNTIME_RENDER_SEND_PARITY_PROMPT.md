# PEMS — FULL EMAIL TEMPLATE EDITOR, PREVIEW, RUNTIME RENDER & SEND PARITY IMPLEMENTATION PROMPT

## 0. Vai trò và nguyên tắc thực hiện

Bạn đang làm việc trên dự án **PEMS**, nhánh:

```text
Dev
```

Mục tiêu của task này là **đóng toàn bộ chuỗi cấu hình email**, không chỉ sửa UI editor.

Phải đảm bảo:

```text
Template Editor
→ Canonical Template Content
→ Validation
→ Template Preview
→ Save
→ Reload
→ Runtime Context Resolution
→ Variable Parsing
→ System Block Rendering
→ HTML Formatting Preservation
→ Final Preview
→ Send
→ Delivered Email
```

Tất cả phải nhất quán.

Không được coi task hoàn thành chỉ vì:

```text
editor hiển thị đúng
```

hoặc:

```text
preview trong màn hình quản lý template nhìn đúng
```

Task chỉ hoàn thành khi **template đã chỉnh sửa có thể được runtime render bằng dữ liệu thật và gửi ra với đúng variable + đúng format + đúng system block**.

---

# 1. Quy tắc bắt buộc trước khi code

## 1.1 Preflight

Trước khi thay đổi code:

```bash
git status
git branch --show-current
git rev-parse HEAD
```

Xác nhận:

```text
branch = Dev
```

Không reset/stash/xóa local changes ngoài ý muốn.

Hiện có thể đã tồn tại local changes từ phase sửa:

```text
Subject/Body variable target
Quill caret guards
table selection/index
table dialog
controlled render normalization
```

Phải audit và giữ các sửa đúng đã có.

Không viết lại từ đầu nếu không cần.

---

## 1.2 Đọc code trước khi sửa

Đọc tối thiểu:

```text
frontend/pems-react/src/pages/dashboard/emails/TemplateManagement.tsx

frontend/pems-react/src/features/emails/components/EmailRichTextEditor.tsx
frontend/pems-react/src/features/emails/components/EmailTableDialog.tsx

frontend/pems-react/src/features/emails/types/templateContract.ts

frontend/pems-react/src/features/emails/utils/emailEditorFormats.ts
frontend/pems-react/src/features/emails/utils/emailEditorSystemNodes.ts
frontend/pems-react/src/features/emails/utils/emailEditorVariableChips.ts
frontend/pems-react/src/features/emails/utils/emailEditorTable.ts
frontend/pems-react/src/features/emails/utils/emailHtmlCanonicalizer.ts
frontend/pems-react/src/features/emails/utils/emailEditorPaste.ts

frontend/pems-react/src/features/emails/api/emailsApi.ts
frontend/pems-react/src/shared/security/sanitizeHtml.ts
```

Backend phải đọc các phần liên quan:

```text
backend/PEMS.Application/Emails/Common/
backend/PEMS.Application/Emails/Sender/
backend/PEMS.Application/Emails/Queries/PreviewEmailTemplate/
backend/PEMS.Application/Emails/Commands/BuildFinalEmailPreview/
backend/PEMS.Application/Emails/
backend/PEMS.Api/Controllers/EmailTemplatesController.cs
```

Tìm chính xác:

```text
EmailTemplateContentValidator
EmailTemplateRenderer
EmailPreviewComposition
SystemEmailDispatcher
sender variable resolver
system block renderer
final-preview handler
actual send path
HTML sanitizer
```

Không đoán tên nếu code thực tế khác.

---

# 2. Mục tiêu cuối cùng

Một template ví dụ:

```html
<p style="color:#e11d48;font-size:18px;text-align:center">
  Xin chào {{recipientName}}
</p>

<hr style="border:none;border-top:1px solid #e2e8f0;margin:20px 0">

<table role="presentation" style="border-collapse:collapse;width:100%">
  <tbody>
    <tr>
      <td style="border:1px solid #dbe4ee;padding:8px">
        {{delegationName}}
      </td>
    </tr>
  </tbody>
</table>

<p>
  Liên hệ: {{senderName}} - {{senderEmail}}
</p>

{{actionBlock}}
```

Sau runtime render phải thành nội dung tương đương:

```html
<p style="color:#e11d48;font-size:18px;text-align:center">
  Xin chào Nguyễn Văn Bình
</p>

<hr ...>

<table ...>
  <tbody>
    <tr>
      <td ...>
        Đoàn Trường THPT ABC
      </td>
    </tr>
  </tbody>
</table>

<p>
  Liên hệ: Nguyễn Văn An - an.nguyen@fpt.edu.vn
</p>

[REAL ACTION BLOCK]
```

Và không còn:

```text
{{recipientName}}
{{delegationName}}
{{senderName}}
{{senderEmail}}
{{actionBlock}}
```

trong email cuối.

---

# 3. Kiến trúc representation phải thống nhất

Có 5 lớp:

```text
1. Stored template
2. Editor DOM
3. Canonical frontend formData
4. Preview
5. Runtime rendered email
```

Phải định nghĩa rõ representation cho từng loại content.

---

# 4. Data Variable

Ví dụ:

```text
{{recipientName}}
{{delegationName}}
{{senderName}}
```

## Stored/canonical form

Luôn là:

```text
{{variableName}}
```

## Editor form

Hiển thị dưới dạng chip thân thiện:

```text
[Họ tên người nhận]
[Tên đoàn]
[Họ tên người gửi]
```

## Runtime

Phải resolve thành dữ liệu thật.

Không được lưu chip HTML vào DB.

Không được gửi chip label ra email.

---

# 5. System Block

Các system block hiện có tối thiểu:

```text
actionBlock
setupSummaryBlock
```

Không coi system block là ordinary variable.

## TEMPLATE stored form

```text
{{actionBlock}}
{{setupSummaryBlock}}
```

## TEMPLATE editor form

Protected object:

```text
[Khối nút phản hồi]
[Bảng thông tin chuẩn bị]
```

Không editable bên trong.

Có thể move nếu contract cho phép.

## Template Management Preview

Dùng:

```text
contract.systemBlockPreviews[name]
```

Đây là sample/inert preview.

## Runtime

Phải render bằng backend real block:

```text
actionBlock
→ real action buttons/token/link

setupSummaryBlock
→ real visit/setup data
```

Không dùng sample data khi gửi thật.

---

# 6. Không được để system block bị convert thành variable chip

Audit:

```text
variablesToChips()
```

Không được bắt:

```text
{{actionBlock}}
{{setupSummaryBlock}}
```

thành `pemsVariable`.

Thứ tự parse đúng:

```text
system block detection
→ system block node

data variable detection
→ variable chip
```

---

# 7. TEMPLATE và COMPOSE phải tách canonical representation

## TEMPLATE

Editor system block:

```text
protected blot
```

Serialize:

```text
{{actionBlock}}
```

## COMPOSE / runtime edit

Nếu flow hiện tại dùng:

```html
<div data-system-block="action"></div>
```

để giữ vị trí live system block, giữ logic đó riêng cho COMPOSE.

Không để node runtime này trở thành stored representation của template.

---

# 8. Editor conversion boundary

Thiết kế rõ:

```ts
toEditorHtml(...)
fromEditorHtml(...)
```

hoặc tên tương đương.

## toEditorHtml TEMPLATE

Thứ tự:

```text
stored table
→ table node

system block placeholder
→ system block node

data variable placeholder
→ variable chip

divider
→ preserved divider

list
→ editor-compatible list

normalize Quill trailing block
```

## fromEditorHtml TEMPLATE

Thứ tự:

```text
variable chip
→ {{variableName}}

system block node
→ {{systemBlockName}}

table wrapper
→ bare <table>

Quill list UI
→ canonical UL/OL

editor-only attrs
→ remove

divider
→ keep

safe inline styles
→ keep
```

Output này phải là **canonical source duy nhất** cho:

```text
formData
validation
preview
save
```

---

# 9. Subject/Body variable target

Giữ fix đã có.

Requirement:

```text
Subject → Body → add variable
→ variable vào Body

Body → Subject → add variable
→ variable vào Subject
```

`EmailRichTextEditor` chỉ quản lý caret Body.

`TemplateManagement` chỉ quản lý active target:

```text
subject/body
```

Không duplicate caret logic giữa parent/editor.

---

# 10. Adjacent variable chips

Giữ fix dựa trên Quill guard behavior nếu implementation hiện tại đã đúng.

Phải hỗ trợ:

```text
[Variable A][Variable B]
```

User có thể click/gõ giữa:

```text
{{A}}/{{B}}
{{A}} - {{B}}
```

Không được lưu editor-only:

```text
\uFEFF
zero width garbage
chip label
```

Placeholder phải luôn atomic.

---

# 11. Toolbar phải hoạt động thật

Audit toàn bộ toolbar.

Từng control phải có:

```text
Editor effect
Canonical HTML
Preview effect
Save/Reload persistence
Runtime render persistence
```

---

# 12. Selection-dependent formatting

Các control:

```text
bold
italic
underline
strike
font
size
text color
background color
link
clear formatting
```

phải áp dụng vào `lastRange`.

Các select/color picker có thể blur Quill.

Không làm:

```ts
q.focus();
q.format(...)
```

mà không restore selection.

Dùng flow tương đương:

```ts
const range = lastRange.current;

q.focus();

if (range) {
    q.setSelection(range.index, range.length, 'silent');
}

q.format(format, value, 'user');
```

---

# 13. Block/caret formatting

Các chức năng:

```text
align
ordered list
bullet list
indent
divider
table
variable
system block
```

phải dùng remembered caret/range.

Không phụ thuộc `getSelection()` sau khi toolbar đã blur editor.

---

# 14. Style phải survive toàn pipeline

Các style email-safe tối thiểu:

```text
font-weight
font-style
text-decoration

font-family
font-size

color
background-color

text-align
margin-left
```

Phải survive:

```text
Editor
→ formData
→ Preview
→ Save
→ Reload
→ Backend renderer
→ Backend sanitizer
→ Final Preview
→ Send
```

Không mở arbitrary CSS.

---

# 15. Divider

Divider:

```html
<hr style="border:none;border-top:1px solid #e2e8f0;margin:20px 0">
```

phải survive toàn pipeline.

Test:

```text
insert
→ preview
→ save
→ reload
→ runtime render
→ final preview
→ send
```

---

# 16. List canonicalization

Quill có thể emit:

```html
<ol>
  <li data-list="bullet">
    <span class="ql-ui"></span>
```

Không được lưu representation phụ thuộc Quill CSS.

Canonical output:

```html
<ul>
  <li>...</li>
</ul>
```

hoặc:

```html
<ol>
  <li>...</li>
</ol>
```

Remove:

```text
span.ql-ui
data-list editor-only
```

Không chỉ normalize cho dirty comparison.

Nếu cần tạo:

```ts
normalizeEmailEditorOutput(html)
```

thì tạo helper riêng.

Không biến equality canonicalizer thành sanitizer/storage writer.

---

# 17. Table

Giữ atomic email-safe table.

Không dùng native Quill table nếu làm mất:

```text
border
padding
width
row/column structure
role=presentation
```

Full flow:

```text
insert table
→ dialog
→ apply
→ editor node
→ canonical <table>
→ preview
→ save
→ reload
→ runtime render
```

---

# 18. Table dialog

Phải hỗ trợ:

```text
edit cell
add row
remove row
add column
remove column
header row
align
width
insert variable in cell
```

Variable picker:

```text
disabled khi chưa focus cell
```

Hiển thị:

```text
Chọn một ô trước
```

hoặc:

```text
Ô hàng X cột Y
```

Không silently no-op.

---

# 19. Table selection stale DOM

Controlled Quill có thể rebuild DOM.

Không giữ `HTMLElement` như source duy nhất.

Giữ thêm:

```text
Quill index / stable position
```

Nếu node detach:

```text
resolve lại current table
```

Nếu không resolve được:

```text
clear selection
show notice
không edit nhầm table khác
```

---

# 20. Hai bảng

Test bắt buộc:

```text
Table A
text
Table B
```

Edit B:

```text
A unchanged
B changed
```

---

# 21. Preview trong Template Management

Không ghép logic rải rác trong component.

Tạo một pipeline thuần:

```ts
buildTemplateDraftPreview(...)
```

Input:

```text
canonical formData
contract
```

Flow:

```text
variable sample substitution
→ system block sample substitution
→ HTML normalization
→ sanitize
→ preview
```

Không đọc trực tiếp:

```text
.ql-editor.innerHTML
```

Preview luôn dựa vào:

```text
formData
```

---

# 22. Template preview chỉ là sample

Template Management Preview dùng:

```text
sample variable values
inert system block
```

Không được coi đây là bằng chứng runtime send đúng.

Phải test runtime riêng.

---

# 23. Runtime Variable Resolution

Audit tất cả variable có thể được template dùng.

Với từng template contract:

```text
allowedVariables
requiredVariables
optionalVariables
senderVariables
requiredSystemBlocks
optionalSystemBlocks
```

Phải chứng minh:

```text
variable được UI cho phép chèn
→ runtime send path có resolver/context tương ứng
```

Không được có:

```text
UI cho chèn
→ save thành công
→ runtime không có value
```

---

# 24. Contract ↔ Runtime Resolver parity

Tạo audit/test:

```text
contract variable
↔ runtime resolver
```

Mọi variable được phép dùng phải thuộc một trong:

```text
business context variable
sender variable
system block
```

Nếu không resolve được:

```text
không offer trong editor
hoặc
runtime phải fail closed
```

Không silently empty trừ khi contract nghiệp vụ xác định variable optional và empty là hợp lệ.

---

# 25. Required runtime variable phải fail closed

Sau runtime rendering:

```text
subject
body
```

không được còn raw placeholder required.

Nếu thiếu:

```text
EMAIL_TEMPLATE_RUNTIME_VARIABLE_MISSING
```

hoặc existing equivalent.

Không gửi email.

Không để:

```text
Xin chào {{recipientName}}
```

ra recipient.

---

# 26. Unresolved placeholder gate

Trước provider/send, detect:

```text
{{...}}
%7B%7B...%7D%7D
```

Phân loại:

```text
known optional
known required
system block
unknown malformed
```

Required/unknown unresolved:

```text
FAIL CLOSED
```

Không gửi.

Không chỉ log rồi tiếp tục.

---

# 27. Sender variables

Audit:

```text
{{senderName}}
{{senderRole}}
{{senderEmail}}
{{senderPhone}}
{{senderDepartment}}
{{senderCampus}}
```

Runtime:

```text
actor bấm gửi
→ resolve actor thật
```

Không dùng sample.

System-generated email:

```text
không có actor
→ configured system sender theo logic hiện tại
```

Test cả 2 case.

---

# 28. System block runtime

## actionBlock

Template:

```text
{{actionBlock}}
```

Template preview:

```text
disabled sample
```

Runtime:

```text
real action HTML
real token/link
```

## setupSummaryBlock

Template:

```text
{{setupSummaryBlock}}
```

Template preview:

```text
sample block
```

Runtime:

```text
real visit/setup data
```

Không dùng sample trong send.

---

# 29. Backend HTML sanitizer parity

Audit sanitizer backend.

Không được strip các format editor vừa cho phép nếu chúng là email-safe.

Test:

```text
font-family
font-size
color
background-color
text-align
margin-left
font-weight
font-style
text-decoration
table style
hr style
```

Nếu backend sanitizer strip:

```text
Editor button không được offer
```

hoặc:

```text
sanitizer phải allow đúng safe style
```

Hai phía phải cùng contract.

---

# 30. Final Preview ↔ Send parity

Đây là gate bắt buộc.

Final Preview và Send phải dùng cùng render/composition pipeline.

Không có:

```text
preview renderer A
send renderer B
```

Nếu code hiện tại có shared:

```text
EmailPreviewComposition
PrepareAuthoredAsync
renderer
```

thì giữ và dùng một nguồn.

---

# 31. Final Preview nội dung

Final Preview phải reflect:

```text
actual variables
actual sender variables
actual formatting
actual tables
actual divider
system block đúng vị trí
```

Action link preview có thể là inert/non-live.

Delivered email có real token URL.

Được phép khác đúng phần token/link động.

Ngoài phần đó:

```text
Final Preview semantic
==
Delivered Email semantic
```

---

# 32. Save → Runtime Render test

Tạo integration test tối thiểu:

```text
1. Update một template test với:
   - variable
   - sender variable
   - color
   - font size
   - align
   - hr
   - table
   - system block

2. Save.

3. Reload DB/template.

4. Runtime render với real context.

5. Assert:
   - variable resolved
   - sender variable resolved
   - block rendered
   - no unresolved placeholder
   - format preserved
   - table preserved
   - divider preserved
```

---

# 33. Final Preview → Send parity test

Flow:

```text
saved template
→ real context
→ final preview
→ send
→ read delivered .eml/file-sink
```

Compare semantic HTML.

Allowed difference:

```text
action token URL
provider-specific outer headers
```

Not allowed difference:

```text
text
variable value
format
table
divider
block position
sender info
```

---

# 34. Audit tất cả registered templates

Không chỉ test một template.

Chạy registry-wide contract audit.

Với từng template + language:

```text
placeholder used in stored body/subject
⊆ allowed contract

required variable exists

required block exists

sender variable capability correct

runtime send path resolves required variables

final rendered content has no unresolved required placeholder
```

---

# 35. Template variable availability audit

Tạo matrix/report:

```text
TemplateCode
Variable
Allowed?
Required?
SubjectAllowed?
RuntimeResolver?
RuntimeSource?
TestedSendPoint?
```

Không cần tạo tài liệu dài nếu test/code evidence đủ, nhưng final report phải nêu các mismatch nếu có.

---

# 36. Editor variable picker

Picker chỉ offer variable từ contract.

Không hard-code global variable list.

Không offer:

```text
variable runtime không resolve
system block như ordinary variable
forbidden sender variable
```

---

# 37. Subject validation

Subject:

```text
plain text + allowed variables
```

Không cho:

```text
table
hr
HTML block
system block
sensitive variable bị forbidden
```

Fail sớm ở UI.

Backend vẫn revalidate.

---

# 38. Save payload contract

Save payload không được chứa:

```text
pems-variable-chip
pems-system-block
pems-email-table
data-selected
contenteditable
ql-ui
editor label
zero-width guard
```

Được phép chứa:

```text
{{variable}}
{{actionBlock}}
{{setupSummaryBlock}}
<table>
<hr>
safe inline style
```

---

# 39. Reload contract

Sau save/reload:

```text
variables
→ chips

system blocks
→ protected objects

tables
→ atomic table

hr
→ divider blot

formats
→ visually preserved
```

Dirty:

```text
false
```

---

# 40. Controlled Quill stability

Giữ fix tránh rebuild loop.

Test:

```text
editor mounted
→ unrelated parent rerender
→ no document rebuild
→ caret unchanged
→ no fake onChange
→ no fake dirty state
```

---

# 41. Restore Default

Sau restore:

```text
content = shipped default
baseline = same
dirty = false
```

Phải reset:

```text
subject selection
body target
table selection
stale caret
```

Reload system block/variable/table đúng representation.

---

# 42. Undo/Redo

Test:

```text
variable
divider
system block
formatting
```

Undo/Redo phải không corrupt canonical output.

Table dialog internal undo không bắt buộc.

---

# 43. Test matrix frontend

| Feature | Editor | Canonical | Template Preview | Save/Reload |
|---|---:|---:|---:|---:|
| Bold | ✓ | ✓ | ✓ | ✓ |
| Italic | ✓ | ✓ | ✓ | ✓ |
| Underline | ✓ | ✓ | ✓ | ✓ |
| Strike | ✓ | ✓ | ✓ | ✓ |
| Font | ✓ | ✓ | ✓ | ✓ |
| Size | ✓ | ✓ | ✓ | ✓ |
| Text color | ✓ | ✓ | ✓ | ✓ |
| Background | ✓ | ✓ | ✓ | ✓ |
| Align | ✓ | ✓ | ✓ | ✓ |
| Ordered list | ✓ | ✓ | ✓ | ✓ |
| Bullet list | ✓ | ✓ | ✓ | ✓ |
| Indent | ✓ | ✓ | ✓ | ✓ |
| Link | ✓ | ✓ | ✓ | ✓ |
| Divider | ✓ | ✓ | ✓ | ✓ |
| Variable | ✓ | placeholder | sample | ✓ |
| Sender variable | ✓ | placeholder | sample | ✓ |
| Action block | ✓ | placeholder | inert sample | ✓ |
| Setup block | ✓ | placeholder | sample | ✓ |
| Table insert | ✓ | table | table | ✓ |
| Table edit | ✓ | table | updated table | ✓ |

Không hoàn thành nếu còn ô fail.

---

# 44. Test matrix runtime

| Feature | Runtime Resolve | Final Preview | Delivered |
|---|---:|---:|---:|
| Business variable | ✓ | real value | real value |
| Sender variable | ✓ | real value | real value |
| Required variable missing | BLOCK | BLOCK | NOT SENT |
| Unknown variable | BLOCK | BLOCK | NOT SENT |
| Action block | real block | inert equivalent | real links |
| Setup block | real data | real data | real data |
| Font | preserve | preserve | preserve |
| Size | preserve | preserve | preserve |
| Color | preserve | preserve | preserve |
| Background | preserve | preserve | preserve |
| Align | preserve | preserve | preserve |
| Table | preserve | preserve | preserve |
| Divider | preserve | preserve | preserve |
| Link | sanitized | sanitized | sanitized |

---

# 45. Frontend tests cần bổ sung

## EmailRichTextEditor

Ít nhất:

```text
Subject→Body variable
Body→Subject variable
adjacent chips
font selection
size selection
color selection
background selection
divider
bullet list
ordered list
system block round-trip
system block not variable chip
table insert
table edit
two tables
save/reload round-trip
parent rerender stability
undo/redo
```

---

# 46. TemplateManagement tests

Ít nhất:

```text
format → preview
divider → preview
variable → preview sample
action block → preview sample
setup block → preview sample
table insert → preview
table edit → preview
VI/EN isolation
save payload canonical
reload dirty=false
restore resets state
```

---

# 47. Backend unit/integration tests

Tạo hoặc mở rộng tests cho:

```text
contract ↔ runtime resolver parity
runtime missing variable fail closed
sender resolver
system block render
format preservation
table preservation
divider preservation
final preview/send parity
```

Không chỉ test string tồn tại.

Test semantic HTML khi phù hợp.

---

# 48. Runtime unresolved detection

Có test:

```text
template contains {{missingVariable}}
runtime context missing it
```

Expected:

```text
send rejected
no sent email
no provider/file-sink delivery
```

---

# 49. Security

Không được vì parity mà nới sanitizer quá mức.

Vẫn chặn:

```text
script
iframe
object
embed
event handlers
javascript:
unsafe data:
arbitrary positioning
hidden content
```

Chỉ allow format toolbar thực sự support.

---

# 50. Không thay DB schema

Không:

```text
create table
alter table
new migration
```

trừ khi có blocker nghiệp vụ thật và phải dừng báo trước.

Task này theo thiết kế hiện tại phải giải quyết được ở code/config/render pipeline.

---

# 51. Không refactor ngoài scope

Không:

```text
rewrite email module
replace Quill
introduce editor framework mới
change template API unnecessarily
change email schema
change unrelated visit flows
```

Giữ thay đổi nhỏ, nhất quán codebase hiện tại.

---

# 52. Thứ tự triển khai

## Phase A — Preflight

1. Git status/HEAD.
2. Audit local changes.
3. Baseline targeted tests.

## Phase B — Canonical representation

4. Fix system block vs variable chip.
5. TEMPLATE system block serialize placeholder.
6. Canonical list output.
7. Verify table/divider output.

## Phase C — Toolbar

8. Fix font/size/color/background remembered selection.
9. Verify remaining toolbar controls.
10. Add editor test matrix.

## Phase D — Template Preview

11. Build one draft preview pipeline.
12. Preview from canonical formData only.
13. Add TemplateManagement parity tests.

## Phase E — Runtime Resolver

14. Audit contract variables.
15. Map every allowed variable to runtime source.
16. Add unresolved placeholder fail-closed gate.
17. Test sender variables.
18. Test system blocks real render.

## Phase F — Backend format preservation

19. Audit sanitizer allow-list.
20. Test all editor-supported formats through backend.

## Phase G — Final Preview / Send

21. Confirm shared render pipeline.
22. Add final-preview/send parity tests.
23. Verify .eml/file-sink output.

## Phase H — Registry-wide audit

24. Run all template contracts.
25. Report mismatches.
26. Fix only real mismatches.

## Phase I — Full gates

27. Frontend targeted tests.
28. Backend targeted tests.
29. Frontend full tests.
30. Backend unit/integration relevant suite.
31. Build/typecheck/lint.
32. Final report.
33. Do not commit unless explicitly approved.

---

# 53. Gates

Frontend:

```bash
cd frontend/pems-react

npm run lint
npm run build
npx vitest run src/features/emails
npx vitest run
```

Backend: dùng đúng commands hiện tại của solution.

Tối thiểu:

```text
PEMS.Application build
PEMS.Api build
PEMS.UnitTests
relevant PEMS.IntegrationTests
```

Nếu integration cần MySQL và môi trường không có:

```text
report BLOCKED
```

không giả vờ pass.

---

# 54. Baseline failures

Nếu full suite có lỗi unrelated:

1. stash/snapshot thay đổi an toàn;
2. chạy baseline cùng test;
3. chứng minh lỗi có sẵn;
4. ghi:

```text
PRE-EXISTING
```

Không sửa ngoài scope.

---

# 55. Definition of Done — Editor

- [ ] Subject/Body variable target đúng.
- [ ] Adjacent variable chips editable around boundaries.
- [ ] System block không thành variable chip.
- [ ] Action block TEMPLATE serialize `{{actionBlock}}`.
- [ ] Setup block TEMPLATE serialize `{{setupSummaryBlock}}`.
- [ ] Font hoạt động.
- [ ] Size hoạt động.
- [ ] Text color hoạt động.
- [ ] Background hoạt động.
- [ ] Align hoạt động.
- [ ] List hoạt động.
- [ ] Indent hoạt động.
- [ ] Divider hoạt động.
- [ ] Table insert hoạt động.
- [ ] Table edit hoạt động.
- [ ] Hai bảng không edit nhầm.
- [ ] Undo/Redo không corrupt content.
- [ ] Controlled rerender không làm mất caret.

---

# 56. Definition of Done — Preview/Save

- [ ] Preview dùng canonical formData.
- [ ] Variable preview dùng sample đúng contract.
- [ ] System block preview dùng inert sample.
- [ ] Formatting preview khớp editor.
- [ ] Table preview khớp editor.
- [ ] Divider preview khớp editor.
- [ ] Save payload không chứa editor-only markup.
- [ ] Save/reload giữ formatting.
- [ ] Save/reload giữ variables.
- [ ] Save/reload giữ system blocks.
- [ ] Save/reload giữ tables.
- [ ] Reload `dirty=false`.

---

# 57. Definition of Done — Runtime

- [ ] Mọi allowed variable có runtime source/resolver.
- [ ] Mọi required variable resolve ở đúng send path.
- [ ] Missing required runtime variable → send blocked.
- [ ] Unknown unresolved placeholder → send blocked.
- [ ] Không email delivered nào còn raw `{{...}}`.
- [ ] Sender variables dùng actor thật.
- [ ] System sender fallback đúng.
- [ ] actionBlock runtime dùng real action block.
- [ ] setupSummaryBlock runtime dùng real data.
- [ ] Formatting survive backend sanitizer.
- [ ] Tables survive backend.
- [ ] Divider survive backend.
- [ ] Final Preview semantic = Delivered semantic.
- [ ] Chỉ token/link động được phép khác giữa final preview và delivered.

---

# 58. Definition of Done — Registry

- [ ] Tất cả registered templates được audit.
- [ ] Template placeholder ⊆ contract.
- [ ] Required variable presence hợp lệ.
- [ ] Required system block presence hợp lệ.
- [ ] Runtime resolver parity hợp lệ.
- [ ] Không template nào cho phép variable không resolve được.
- [ ] Không runtime send path nào gửi unresolved required placeholder.

---

# 59. Final report bắt buộc

Báo cáo ngắn gọn theo format:

```text
1. Baseline
2. Root causes
3. Files changed
4. Editor parity result
5. Runtime variable parity result
6. Backend format preservation result
7. Final Preview ↔ Send parity result
8. Registry-wide audit
9. Tests/gates
10. Pre-existing failures
11. Remaining limitations
12. Commit status
```

Phải nêu rõ:

```text
đã commit hay chưa
```

Mặc định:

```text
CHƯA COMMIT
```

trừ khi có lệnh rõ ràng.

---

# 60. Nguyên tắc kết thúc

Không kết luận:

```text
"đã fix xong"
```

chỉ vì toolbar click được.

Chỉ kết luận hoàn tất khi đã chứng minh:

```text
Configured template
→ saved canonical
→ parsed with real values
→ rendered with real system blocks
→ preserved formatting
→ final preview
→ delivered email
```

và:

```text
NO unresolved required variable
NO editor-only markup
NO preview/send semantic drift
```
