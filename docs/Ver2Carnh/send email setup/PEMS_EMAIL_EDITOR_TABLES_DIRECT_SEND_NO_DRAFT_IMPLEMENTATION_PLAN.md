# PEMS — KẾ HOẠCH CẬP NHẬT EMAIL EDITOR, HTML TABLE VÀ XÓA CHỨC NĂNG NHÁP

## 1. Mục tiêu

Cập nhật module email theo 4 nhóm:

1. Hiển thị rõ lỗi validation của cả Tiếng Việt và English.
2. Chèn biến đúng vị trí con trỏ trong Subject và Body.
3. Sửa toàn bộ bảng HTML của email **Gửi cập nhật chuẩn bị**.
4. Xóa hoàn toàn chức năng lưu nháp email và chuyển sang preview/send trực tiếp.

Dự án đang ở giai đoạn dev, dữ liệu hiện tại là seed/demo nên được phép:

- Xóa dữ liệu nháp hiện có.
- Xóa seed liên quan đến nháp.
- Cập nhật canonical SQL trực tiếp.
- Drop các bảng nháp sau khi xác nhận không còn consumer.
- Không cần migrate dữ liệu nháp cũ.

Không thay đổi các nghiệp vụ email ngoài phạm vi trên.

---

## 2. Kết quả mong muốn

### 2.1 Template editor

- Nút lưu không bị khóa mà không có lý do rõ ràng.
- Nếu English thiếu `{{contactInformationBlock}}`, người dùng đang ở tab Tiếng Việt vẫn nhìn thấy lỗi.
- Có action chuyển thẳng đến tab và editor đang lỗi.
- Bấm biến sẽ chèn tại vị trí con trỏ gần nhất, không nối vào cuối nội dung.

### 2.2 Email cập nhật chuẩn bị

- Bảng lịch trình chỉ còn đúng 4 cột:
  - Thời gian.
  - Nội dung.
  - Địa điểm.
  - Phụ trách.
- Không còn cột hoặc ô riêng `FPT University`.
- Các bảng khác không bị dính tiêu đề, mất border hoặc lệch cột.
- Compose, preview và email gửi thật dùng cùng HTML.

### 2.3 Gửi email

Luồng mới:

```text
Mở form
→ chỉnh sửa trong state frontend
→ xem trước
→ gửi trực tiếp
```

Không còn:

- Tự động lưu nháp.
- Tab `Nháp`.
- Danh sách nháp.
- `Đã lưu nháp lúc...`.
- Khôi phục nháp.
- Hủy nháp.
- `draftId`.
- Lỗi `EmailDraft (...) was not found`.
- Chuỗi `createDraft → updateDraft → sendDraft`.

---

## 3. Phạm vi thực hiện

### Frontend

Cập nhật:

```text
TemplateManagement
EmailComposeModal
EmailManagement
VisitProcess
Reply email
Manual compose
Email preview
```

Xóa:

```text
DraftsPanel
openDraftId
initialDraftId
draft autosave
draft hydrate
draft load error UI
draft list/filter
draft API calls
```

### Backend

Cập nhật:

```text
Preview manual email
Send manual email
Reply email
Prepare setup-progress email
Refresh setup-progress email
Send setup-progress email
Attachment validation
Idempotency
Sent-email history
```

Xóa consumer của:

```text
CreateEmailDraft
UpdateEmailDraft
GetEmailDraft
ListEmailDrafts
DiscardEmailDraft
SendEmailDraft
IEmailDraftDispatcher
```

### Database

Sau khi xác nhận không còn consumer:

```text
email_draft_attachments
email_draft_recipients
email_drafts
```

Đồng thời:

- Xóa seed draft demo.
- Xóa FK/index/trigger chỉ phục vụ draft.
- Cập nhật canonical SQL.
- Cập nhật expected SHA/hash gate.
- Không cần giữ hoặc migrate dữ liệu draft cũ.

---

## 4. Giai đoạn 0 — Audit trước khi sửa

### Git safety

Ghi nhận:

```text
branch
HEAD
git status --short
stash count
ahead/behind
WIP hiện tại
```

Không reset, amend, rebase hoặc làm mất WIP.

### Audit draft consumer

Search toàn project:

```text
EmailDraft
email_drafts
draftId
initialDraftId
createDraft
updateDraft
getDraft
sendDraft
discardDraft
DraftsPanel
IEmailDraftDispatcher
```

Lập danh sách consumer và hướng thay thế trước khi xóa code hoặc bảng.

---

## 5. Giai đoạn 1 — Validation đa ngôn ngữ

### Vấn đề

Ví dụ:

```text
Contact requirement = REQUIRED
VI có {{contactInformationBlock}}
EN thiếu {{contactInformationBlock}}
```

Nút `Lưu thay đổi` bị disable nhưng người dùng đang ở VI nên không nhìn thấy lỗi EN.

### Cách sửa

#### Badge trên tab

```text
Tiếng Việt
English ●
```

Tab có lỗi cần có:

- Dấu đỏ hoặc icon lỗi.
- `aria-invalid`.
- Tooltip ngắn.

#### Validation summary

Hiển thị ngay phía trên footer:

```text
Không thể lưu vì English đang thiếu {{contactInformationBlock}}.

[Chuyển sang English]
```

Nếu cả hai ngôn ngữ lỗi, hiển thị đủ hai lỗi.

#### Checklist tại card liên hệ

```text
✓ Tiếng Việt đã có khối thông tin liên hệ
✕ English chưa có khối thông tin liên hệ
```

#### Chuyển tới lỗi

Khi bấm `Chuyển sang English`:

```text
Set active language = EN
→ scroll tới body editor
→ focus editor
```

Không tự động chèn block nếu người dùng chưa yêu cầu.

### Quy tắc validation

```text
NONE:
- VI/EN không được chứa contact block.

OPTIONAL:
- Có hoặc không có block đều hợp lệ.

REQUIRED:
- VI và EN đều phải có contact block.

UNSUPPORTED:
- Không hiển thị contact settings.
- VI/EN không được chứa contact block.
```

Backend vẫn là nguồn validation cuối.

---

## 6. Giai đoạn 2 — Chèn biến tại vị trí con trỏ

### Phạm vi

```text
Subject VI
Subject EN
Body VI
Body EN
```

### Subject

Lưu:

```text
selectionStart
selectionEnd
active subject field
```

Khi bấm biến:

```text
prefix + token + suffix
```

Nếu đang bôi đen text, thay thế đoạn bôi đen.

Sau khi chèn:

```text
focus input
đặt caret sau token
đánh dấu form dirty
```

### Body ReactQuill

Lưu selection cuối theo ngôn ngữ:

```ts
{
  vi: { index, length },
  en: { index, length }
}
```

Khi bấm biến:

```text
lấy editor đúng ngôn ngữ
→ focus
→ insertText tại selection cũ
→ thay selection nếu length > 0
→ đặt caret sau token
→ đồng bộ form state
```

Fallback duy nhất:

```text
Nếu chưa từng focus editor, chèn tại đầu body hoặc vị trí mặc định rõ ràng.
```

Không mặc định nối cuối nội dung.

### Test

- Chèn giữa câu.
- Chèn đầu nội dung.
- Chèn cuối nội dung.
- Thay đoạn đang bôi đen.
- Chuyển VI/EN không dùng nhầm caret.
- Bấm chip làm editor mất focus vẫn chèn đúng vị trí cũ.
- Subject và Body không dùng chung selection.

---

## 7. Giai đoạn 3 — Sửa HTML table của setup-progress

### 7.1 Bảng lịch trình

Cấu trúc chuẩn:

| Thời gian | Nội dung | Địa điểm | Phụ trách |
|---|---|---|---|

Mỗi row đúng 4 ô:

```text
1. Thời gian
2. Nội dung = title + description
3. Địa điểm
4. Phụ trách
```

Xóa hoàn toàn ô riêng:

```text
FPT University
```

Không dùng fallback cứng:

```text
responsibleName ?? "FPT University"
```

Nếu chưa có người phụ trách, hiển thị `—` hoặc label thống nhất đang có trong hệ thống.

### 7.2 Nội dung cột `Nội dung`

Render trong cùng một `<td>`:

```html
<strong>Tiêu đề lịch trình</strong>
<div>Mô tả chi tiết</div>
```

Không tách title và description thành hai cột.

### 7.3 Tỷ lệ cột

```text
Thời gian: 18%
Nội dung: 42%
Địa điểm: 22%
Phụ trách: 18%
```

Dùng `colgroup`:

```html
<colgroup>
  <col style="width:18%">
  <col style="width:42%">
  <col style="width:22%">
  <col style="width:18%">
</colgroup>
```

### 7.4 Style chung

```html
<table style="width:100%;border-collapse:collapse;table-layout:fixed">
```

Header/cell:

```html
style="
  border:1px solid #374151;
  padding:6px 8px;
  text-align:left;
  vertical-align:top;
  white-space:normal;
  overflow-wrap:break-word;
  word-break:normal;
"
```

Không dùng:

```css
word-break: break-all;
```

### 7.5 Audit toàn bộ bảng

Kiểm tra:

```text
Thông tin chung
Danh sách khách
Thành phần phía FPT
Lịch trình chi tiết
Trạng thái chuẩn bị
Yêu cầu bổ sung
Thông tin liên hệ
```

Với mỗi bảng:

- Số `<th>` bằng số `<td>` mỗi row.
- Không có cột trống.
- Không có border thừa.
- Header không dính chữ.
- Nội dung dài không phá width.
- HTML compose và preview giống nhau.

### 7.6 Sanitizer

Đảm bảo allow-list giữ:

```text
table
thead
tbody
tr
th
td
colgroup
col
strong
div
br
style cần thiết
```

Không mở toàn bộ style không kiểm soát.

---

## 8. Giai đoạn 4 — Xóa hoàn toàn chức năng nháp

### 8.1 Frontend state

`EmailComposeModal` chỉ giữ dữ liệu trong phiên:

```text
TO
CC
BCC
Subject
Body
Attachments
Selected template
Related type/id
Idempotency key
```

Không gọi API lưu nháp.

### 8.2 Đóng form

Nếu form chưa thay đổi:

```text
Đóng ngay.
```

Nếu đã thay đổi:

```text
Nội dung email chưa được gửi và sẽ không được lưu.

[Tiếp tục chỉnh sửa]
[Đóng và hủy]
```

Không có nút `Lưu nháp`.

### 8.3 Provider lỗi

Nếu send lỗi:

```text
Giữ modal mở
Giữ nguyên state
Hiển thị lỗi
Cho phép thử lại
```

Không đóng modal và không mất nội dung.

### 8.4 Manual compose

```text
Mở form trống
→ nhập dữ liệu
→ preview trực tiếp
→ send trực tiếp
```

Mở lại sau khi đóng là form mới.

### 8.5 Reply

```text
Mở reply
→ dựng recipient + subject
→ người dùng chỉnh sửa
→ preview
→ send trực tiếp
```

Không tạo draft.

### 8.6 Setup-progress

```text
Bấm Gửi cập nhật chuẩn bị
→ chọn ngôn ngữ
→ backend sinh body + report
→ frontend giữ payload trong state
→ người dùng chỉnh sửa
→ preview
→ send trực tiếp
```

Không trả hoặc nhận `draftId`.

---

## 9. API mục tiêu

### Manual preview

```http
POST /api/emails/preview
```

Payload gồm:

```text
subject
bodyContent
bodyFormat
recipients
attachments
relatedType
relatedId
```

### Manual send

```http
POST /api/emails/send
Idempotency-Key: <uuid>
```

Backend:

```text
Authorize
→ validate recipient
→ validate content
→ sanitize
→ validate attachment fail-closed
→ claim idempotency
→ send provider
→ write sent email/history
```

### Prepare setup-progress

```http
POST /api/delegations/{requestId}/campuses/{instanceId}/setup-progress-email/prepare
```

Response:

```text
subject
bodyHtml
recipients
reportFileId
reportFileName
generatedAt
languageCode
warnings
```

Không có `draftId`.

### Refresh setup-progress

```http
POST /api/delegations/{requestId}/campuses/{instanceId}/setup-progress-email/refresh
```

Request chỉ cần `languageCode`.

Nếu body đã được sửa, frontend phải xác nhận trước:

```text
Đồng bộ sẽ thay thế nội dung đã chỉnh sửa.

[Hủy]
[Đồng bộ và thay thế]
```

### Send setup-progress

```http
POST /api/delegations/{requestId}/campuses/{instanceId}/setup-progress-email/send
Idempotency-Key: <uuid>
```

Payload gồm:

```text
subject
bodyHtml
recipients
attachments
languageCode
```

Backend phải re-check:

```text
Host hiện tại
Visit stage
Recipient rules
Mandatory report
Attachment readable
HTML valid
Idempotency
```

---

## 10. Attachment sau khi bỏ draft

### Upload

File vẫn upload bằng API file hiện tại và trả `fileId`.

Frontend giữ `fileId` trong state.

### Send

Send API validate:

```text
file thuộc user hoặc user được quyền dùng
file còn tồn tại
file đọc được
mime/size hợp lệ
```

Nếu một file lỗi:

```text
Chặn toàn bộ send
Không gọi provider
Nêu tên file lỗi
```

Không silently bỏ file.

### Đóng mà chưa gửi

Vì đang ở dev, dùng cách đơn giản:

- Frontend gọi best-effort delete cho file được upload trong phiên nhưng chưa gửi.
- Nếu browser đóng đột ngột và còn file demo thì không chặn task này.
- Không tạo cleanup service mới trong phạm vi hiện tại.

---

## 11. Chống gửi trùng

Sau khi bỏ draft, không còn `DRAFT → SENT` để claim.

Dùng `Idempotency-Key` hiện có của project.

Frontend:

```text
Mỗi lần mở composer sinh một UUID
Retry cùng phiên giữ cùng key
Mở composer mới sinh key mới
```

Backend:

```text
Cùng user + endpoint + key
→ chỉ xử lý một lần
→ request lặp trả lại kết quả cũ
```

Không tạo bảng mới nếu project đã có `email_send_idempotency`.

---

## 12. Xóa draft code

### Backend

Sau khi direct send hoạt động, xóa:

```text
Commands/CreateEmailDraft
Commands/UpdateEmailDraft
Commands/DiscardEmailDraft
Commands/SendEmailDraft
Queries/GetEmailDraft
Queries/ListEmailDrafts
EmailDraftDispatcher
EmailDraftMapper
Draft DTO/model không còn dùng
Draft controller endpoints
DI registrations
```

Chỉ giữ helper validation còn được direct-send tái sử dụng.

### Frontend

Xóa:

```text
DraftsPanel
draft API client
draft types
openDraftId
draftsRefreshToken
mailboxFilter = drafts
initialDraftId
hydrating state
autosave timer
savedAt/saving draft UI
discard draft UI
draft 404/403/409 UI
```

Search toàn frontend để chắc chắn không còn gọi draft API.

---

## 13. Database và canonical SQL

Sau khi code không còn consumer:

```sql
DROP TABLE IF EXISTS email_draft_attachments;
DROP TABLE IF EXISTS email_draft_recipients;
DROP TABLE IF EXISTS email_drafts;
```

Cập nhật:

```text
canonical SQL hiện tại
seed email data
EF model/configuration
DbSet
entity classes không còn dùng
tests expected schema
ExpectedSha256
database docs
```

Vì dữ liệu chỉ là demo:

- Không cần export/migrate draft.
- Không cần compatibility endpoint.
- Không cần deprecation phase.
- Không giữ bảng trống để rollback.

Fresh-import chỉ chạy trên database test/dev riêng, không import đè database đang dùng nếu chưa xác nhận.

---

## 14. Thứ tự triển khai

### Phase A — UI editor

1. Validation summary VI/EN.
2. Badge lỗi trên tab.
3. Chuyển tab/focus lỗi.
4. Caret tracking Subject/Body.
5. Insert variable tại caret.

### Phase B — HTML setup-progress

6. Sửa bảng lịch trình 4 cột.
7. Bỏ ô `FPT University`.
8. Gộp title + description.
9. Chuẩn hóa style tất cả bảng.
10. Đồng bộ sanitizer/preview/file-sink tests.

### Phase C — Direct send

11. Tạo request model preview/send dùng chung.
12. Chuyển manual compose sang direct send.
13. Chuyển reply sang direct send.
14. Chuyển setup-progress prepare/refresh/send không draft.
15. Giữ attachment fail-closed.
16. Dùng idempotency key.

### Phase D — Xóa draft

17. Xóa draft UI.
18. Xóa draft API frontend.
19. Xóa draft controller/handlers/backend service.
20. Xóa entity/config/DbSet draft.
21. Drop 3 bảng draft trong canonical SQL.
22. Xóa seed draft.
23. Bump canonical hash.

### Phase E — Test và closure

24. Targeted tests.
25. Full backend/frontend tests.
26. Fresh-import canonical SQL vào DB test.
27. Runtime smoke với outbound tắt.
28. Commit theo nhóm, không push nếu chưa được yêu cầu.

---

## 15. Files dự kiến ảnh hưởng

Tên file phải kiểm tra lại theo code hiện tại.

### Frontend

```text
pages/dashboard/emails/TemplateManagement.tsx
features/emails/components/EmailComposeModal.tsx
features/emails/components/DraftsPanel.tsx        // xóa
pages/dashboard/emails/EmailManagement.tsx
pages/dashboard/visit/VisitProcess.tsx
features/emails/api/emailsApi.ts
features/emails/api/emailDraftsApi.ts             // xóa
shared/api/endpoints.ts
features/emails/types/*
template editor tests
compose/reply/setup-progress tests
```

### Backend

```text
EmailsController
manual send command/handler
reply command/handler
setup-progress prepare/refresh/send handlers
EmailDraftDispatcher                              // xóa
Create/Update/Get/List/Discard/Send draft         // xóa
email direct-send validation helpers
idempotency handler
attachment loader/guard
VisitSetupEmailHtml
HTML sanitizer
ApplicationDbContext
email draft entities/configurations               // xóa
```

### Database

```text
canonical SQL
seed email data
ExpectedSha256
schema verification tests
```

---

## 16. Tests bắt buộc

### Template editor

- VI đúng, EN thiếu block.
- EN đúng, VI thiếu block.
- Cả hai thiếu block.
- Badge đúng tab.
- Validation summary luôn nhìn thấy.
- Action chuyển tab/focus đúng editor.
- Insert variable Subject VI/EN tại caret.
- Insert variable Body VI/EN tại caret.
- Replace selection.
- Không nối cuối ngoài ý muốn.

### HTML table

- Mỗi bảng có số header/cell đúng.
- Lịch trình đúng 4 cột.
- Không có cell `FPT University` dư.
- Title/description cùng cell.
- `colgroup` đúng tỷ lệ.
- Không `word-break: break-all`.
- Sanitizer giữ cấu trúc bảng.
- Compose/preview/file-sink có cùng HTML structure.

### Direct send

- Manual compose gửi không draft.
- Reply gửi không draft.
- Setup-progress VI/EN gửi không draft.
- Preview không cần draft ID.
- Attachment unreadable chặn send.
- Provider lỗi giữ form.
- Double-click chỉ một sent email.
- TO/CC/BCC giữ đúng.
- History ghi đúng.
- Không unresolved placeholder.

### Close behavior

- Form chưa dirty đóng ngay.
- Form dirty hiện confirm.
- `Tiếp tục chỉnh sửa` giữ state.
- `Đóng và hủy` xóa state.
- Mở lại là form mới.
- Không có lựa chọn lưu nháp.

### Database

- Fresh import thành công.
- Không còn 3 bảng draft.
- Không còn FK/index/trigger draft.
- Không còn seed draft.
- Canonical hash gate xanh.
- Search code không còn runtime consumer `EmailDraft`.

---

## 17. Runtime smoke

Chạy với:

```text
Smtp__Enabled=false
File sink hoặc fake provider
Database dev/test
```

Các ca:

```text
1. Manual compose không attachment
2. Manual compose có attachment
3. Reply
4. Setup-progress VI
5. Setup-progress EN
6. Đồng bộ setup-progress
7. Đóng form dirty
8. Provider failure
9. Attachment missing
10. Double-click send
11. Template REQUIRED thiếu EN block
12. Chèn biến giữa nội dung
```

Đối chiếu:

```text
sent_emails
sent_email_recipients
sent_email_attachments
file sink
không có email_drafts
không có outbound thật
```

---

## 18. Commit đề xuất

```text
fix(email-editor): surface multilingual errors and insert variables at caret
fix(email-html): align setup-progress tables and remove extra schedule column
refactor(email): send compose and reply without drafts
refactor(setup-email): prepare refresh and send without draft state
chore(db): remove email draft schema and demo seed
test(email): cover direct-send and no-draft behavior
```

Không gom thành một fat commit nếu thay đổi lớn.

---

## 19. Definition of Done

```text
[ ] Lỗi VI/EN luôn nhìn thấy dù đang ở tab khác.
[ ] Có action chuyển tới đúng ngôn ngữ đang lỗi.
[ ] Variable chèn đúng caret ở Subject và Body.
[ ] Bảng lịch trình đúng 4 cột.
[ ] Không còn ô FPT University dư.
[ ] Các bảng khác không lệch cột/border.
[ ] Compose, preview và email thật dùng cùng HTML.
[ ] Không còn tab/danh sách/trạng thái nháp.
[ ] Không còn request draft API.
[ ] Không còn draftId trong frontend/backend contract.
[ ] Manual compose gửi trực tiếp.
[ ] Reply gửi trực tiếp.
[ ] Setup-progress prepare/refresh/send không draft.
[ ] Attachment lỗi chặn send.
[ ] Idempotency chống gửi trùng.
[ ] Provider lỗi không làm mất form.
[ ] Không còn code runtime EmailDraft.
[ ] Không còn 3 bảng draft trong canonical SQL.
[ ] Không còn seed draft demo.
[ ] Backend build/test xanh.
[ ] Frontend typecheck/build/test xanh.
[ ] Canonical SQL fresh-import xanh.
[ ] Runtime smoke đạt với outbound tắt.
```

---

## 20. Báo cáo cuối

```text
ROOT CAUSE
- Validation đa ngôn ngữ
- Caret insertion
- HTML table
- Draft complexity

FILES CHANGED
- file
- thay đổi gì

API BEFORE / AFTER
- manual compose
- reply
- setup-progress

DATABASE
- tables dropped
- seed removed
- canonical hash

TESTS
- backend
- integration
- frontend
- SQL import
- runtime smoke

SAFETY
- SMTP/provider
- DB used
- WIP/stash
- push status

COMMITS

REMAINING DEBT
```

Không báo hoàn thành nếu vẫn còn draft consumer hoặc một trong ba bảng draft còn được runtime sử dụng.
