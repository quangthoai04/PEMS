# PEMS — Kế hoạch hoàn thiện Preview / Edit / Final Preview / Action Block / Sender Variables

## 1. Mục tiêu

Hoàn thiện toàn bộ luồng soạn và gửi email theo nguyên tắc:

```text
VIEW
→ EDIT
→ FINAL_PREVIEW
→ SEND
```

Trong đó:

- Khi bấm biểu tượng mắt, người dùng phải xem được email hoàn chỉnh gần giống email thực tế sẽ nhận.
- Nếu template được phép chỉnh sửa, modal phải có nút **Chỉnh sửa**.
- Nội dung đã parse biến người gửi được phép sửa tự do.
- Action block phải nằm đúng trong bố cục email, không tách thành một khu vực riêng bên ngoài nội dung.
- Người dùng được phép đổi vị trí action block nhưng không được sửa URL, token hoặc chức năng của nút.
- Người dùng phải xem lại Final Preview trước khi gửi.
- Email thực gửi phải khớp với Final Preview.
- Không khôi phục kiến trúc contact cũ.
- Không tạo bảng draft mới.
- Không push cho tới khi toàn bộ gates và browser smoke đều xanh.

---

## 2. Trạng thái hiện tại

Các phần đã có:

- Sender variables:
  - `{{senderName}}`
  - `{{senderRole}}`
  - `{{senderEmail}}`
  - `{{senderPhone}}`
  - `{{senderDepartment}}`
  - `{{senderCampus}}`
- Backend prepare → final-preview → send pipeline dùng HMAC token.
- Modal đã có khái niệm `VIEW`, `EDIT`, `FINAL_PREVIEW`.
- Reply-To đã được sửa để SMTP dùng đúng giá trị.
- Contact architecture phần lớn đã được loại bỏ.
- Canonical SQL, defaults JSON và patch sender variables đã được cập nhật.

Các phần còn thiếu hoặc chưa đúng:

1. Nút **Chỉnh sửa** chưa xuất hiện ở các template editable.
2. Action block đang bị tách ra khỏi nội dung email.
3. Action block chưa thể di chuyển vị trí trong editor.
4. Preview chưa phản ánh đúng bố cục email cuối.
5. Final Preview chưa được xác nhận khớp tuyệt đối với email thực gửi.
6. Setup-progress và một số call site còn cần đồng bộ.
7. Một số code/contact/test cũ còn sót.
8. `02_sync_templates.sql` còn nội dung cũ.
9. Canonical SQL runner chưa có guard chống chạy nhầm vào `pems_db`.
10. Integration test chưa xanh hoàn toàn.

---

# 3. Logic 1 — Hiển thị nút Chỉnh sửa đúng

## 3.1 Template editable

Các template sau phải có capability:

```text
AVAILABLE_EDITABLE_RUNTIME
```

Tối thiểu:

- Logistics request.
- Logistics change proposal.
- Participant invitation.
- Student invitation.
- Department leader invitation.
- Department staff assignment.
- Setup progress update.

## 3.2 Quy tắc hiển thị

Trong `VIEW`:

```text
[Đóng] [Chỉnh sửa] [Gửi với nội dung này]
```

Điều kiện:

```text
actor có quyền gửi email
AND
template capability = AVAILABLE_EDITABLE_RUNTIME
```

Không được giới hạn chỉnh sửa chỉ theo role nếu actor đã có quyền gửi action đó.

## 3.3 Kiểm tra chuỗi dữ liệu

Phải kiểm tra từ backend tới frontend:

```text
templateCode thực tế
→ EmailSenderVariableCapabilities
→ prepare-preview response
→ canEdit
→ EmailPreviewModal render condition
```

Response chuẩn:

```json
{
  "templateCode": "LOGISTICS_REQUEST_TO_DEPARTMENT",
  "canEdit": true
}
```

Nếu `canEdit = true` mà nút vẫn không hiện, lỗi nằm ở frontend.

---

# 4. Logic 2 — Action block phải nằm trong nội dung email

## 4.1 Placeholder chuẩn

Template action dùng:

```text
{{actionBlock}}
```

Ví dụ:

```html
<p>Vui lòng kiểm tra thông tin yêu cầu dưới đây.</p>

{{actionBlock}}

<p>Nếu cần trao đổi thêm, vui lòng phản hồi email này.</p>

<p>Trân trọng,<br>PEMS - FPT University</p>
```

## 4.2 Preview đúng

Preview phải hiển thị:

```text
Nội dung đầu email

[Đồng ý] [Từ chối] [Hành động khác]

Thông tin người gửi

Chữ ký
```

Không hiển thị action block trong một section riêng kiểu:

```text
NÚT PHẢN HỒI HỆ THỐNG (KHÔNG SỬA ĐƯỢC)
```

Section cảnh báo kỹ thuật chỉ nên xuất hiện trong editor dưới dạng chú thích nhỏ.

## 4.3 Preview button state

Trong preview:

- Nút hiển thị đúng màu, kích thước, label và vị trí.
- Nút phải disabled hoặc không có href thực để người gửi không thao tác nhầm.
- Không đưa action token thật xuống frontend preview.

---

# 5. Logic 3 — Cho phép di chuyển action block trong editor

## 5.1 Không đưa HTML token thật vào editor

Không đưa trực tiếp:

```html
<a href="https://...token=...">Đồng ý</a>
```

Thay bằng một system node:

```html
<div data-system-block="action"></div>
```

Hoặc node editor:

```json
{
  "type": "system-action-block",
  "blockId": "PRIMARY_ACTION_BLOCK"
}
```

## 5.2 Người dùng được phép

- Kéo action block lên hoặc xuống.
- Cắt/dán block sang vị trí khác.
- Đặt trước hoặc sau phần sender.
- Thêm đoạn văn trước hoặc sau block.
- Thay đổi khoảng cách xung quanh block.

## 5.3 Người dùng không được phép

- Sửa URL.
- Sửa token.
- Sửa action ID.
- Sửa chức năng của nút.
- Nhân đôi block.
- Chèn block giả.
- Chuyển nút Đồng ý thành action khác.
- Sửa HTML bên trong system block.

## 5.4 Validation số lượng block

Với template action bắt buộc:

```text
action block count = 1
```

Nếu bị xóa:

```text
Email này cần nút phản hồi để người nhận xử lý yêu cầu.
Bạn có thể thay đổi vị trí nhưng không thể xóa khối này.
```

Nếu bị nhân đôi:

```text
Mỗi email chỉ được có một khối nút phản hồi.
```

Với template không có action:

- Không cho chèn action block.

Với template action tùy chọn:

- Chỉ cho xóa nếu capability backend cho phép.

---

# 6. Logic 4 — Preview phải là email hoàn chỉnh

## 6.1 VIEW

Khi bấm biểu tượng mắt, phải hiển thị một bố cục thống nhất:

```text
Người nhận
CC/BCC
Tiêu đề

Nội dung đã parse
Action block đúng vị trí
Thông tin người gửi
Chữ ký

Tệp đính kèm
Reply-To
```

Không để người dùng phải tự suy đoán vị trí action block.

## 6.2 Sender variables

Các biến sender phải được parse trước khi preview:

```text
{{senderName}}       → IC Staff Hà Nội
{{senderRole}}       → IC Staff
{{senderEmail}}      → staff.hn@fpt.edu.vn
{{senderPhone}}      → ...
{{senderDepartment}} → ...
{{senderCampus}}     → ...
```

Không để sót literal placeholder trong preview.

## 6.3 Reply-To

Preview phải hiển thị rõ:

```text
Khi người nhận bấm “Trả lời”, email sẽ gửi tới:
staff.hn@fpt.edu.vn
```

Reply-To là metadata riêng, không suy ra từ body.

---

# 7. Logic 5 — Chế độ chỉnh sửa

## 7.1 EDIT

Khi bấm **Chỉnh sửa**:

- Subject có thể chỉnh sửa.
- `editableBodyHtml` được đưa vào editor.
- Sender data đã parse trở thành nội dung bình thường.
- Action block hiển thị như system node có thể di chuyển.
- Action URL/token không xuất hiện trong editor.

Các nút:

```text
[Hủy thay đổi]
[Khôi phục từ mẫu]
[Xem trước kết quả]
```

Không có nút gửi trực tiếp trong EDIT.

## 7.2 Sender content được sửa tự do

Người dùng có thể:

- Sửa tên hiển thị.
- Sửa vai trò hiển thị.
- Sửa email/số điện thoại trong nội dung.
- Xóa phần thông tin người gửi.
- Thay đổi bảng thành đoạn văn.
- Viết lại toàn bộ đoạn liên hệ.
- Đổi bố cục và định dạng.

Backend không được render lại sender variables sau khi người dùng đã sửa.

---

# 8. Logic 6 — Final Preview

## 8.1 Khi bấm Xem trước kết quả

Backend phải:

1. Kiểm tra actor.
2. Kiểm tra preview token cũ.
3. Validate subject.
4. Sanitize body.
5. Validate action block count.
6. Render action block thật tại vị trí system node.
7. Kiểm tra attachments.
8. Kiểm tra Reply-To.
9. Sinh `finalPreviewHtml`.
10. Cấp `finalPreviewToken`.

## 8.2 UI Final Preview

Hiển thị:

```text
XEM TRƯỚC KẾT QUẢ CUỐI

Nội dung chính xác sẽ gửi
Action buttons đúng vị trí
Attachments
Reply-To
```

Các nút:

```text
[Quay lại chỉnh sửa]
[Gửi email]
```

---

# 9. Logic 7 — Gửi đúng Final Preview

Send request chỉ dùng final token:

```json
{
  "finalPreviewToken": "..."
}
```

Backend phải gửi đúng:

- Subject đã duyệt.
- Body đã duyệt.
- Action block đúng vị trí.
- Attachment đã duyệt.
- Reply-To đã duyệt.
- Recipients đã duyệt.

Không được:

- Parse lại sender.
- Append thêm action block ở cuối.
- Restore template.
- Chèn thêm contact block.
- Thay đổi vị trí action block.
- Gửi body khác với final preview.

Acceptance chính:

```text
finalPreviewHtml ≈ body HTML trong email .eml/provider output
```

Sai khác chỉ được phép ở wrapper kỹ thuật của MIME/provider, không được thay đổi nội dung và thứ tự block.

---

# 10. Logic 8 — Invalidate approval token khi nội dung đổi

Bất kỳ thay đổi nào sau đây phải làm final token cũ mất hiệu lực:

- Sửa subject.
- Sửa body.
- Di chuyển action block.
- Thêm/xóa attachment.
- Thay Reply-To.
- Thay recipient nếu flow cho phép.
- Template revision thay đổi.
- Entity state thay đổi.
- Action configuration thay đổi.

Sau thay đổi, người dùng phải tạo Final Preview mới.

---

# 11. Logic 9 — Setup-progress và các call site

Tất cả call site phải dùng cùng pipeline:

```text
VIEW
→ EDIT
→ FINAL_PREVIEW
→ SEND
```

Tối thiểu:

- `LogisticsRequestSection`
- `ParticipantInvitationSection`
- `SharedDashboardView`
- `VisitSetupProgressComposer`
- `EmailComposeModal`
- `ManualEmailSender`

Setup-progress không được là ngoại lệ dùng editor hoặc send path khác.

Mỗi call site phải truyền scope key để backend recompute và verify.

---

# 12. Logic 10 — Dọn contact architecture cũ

Audit và loại bỏ các phần còn sót:

```text
contactInformationBlock
ContactSettingsPanel
EmailContactOverrideSection
contact-preview
contact-candidates
contactOverride
lockedContactBlockHtml
EmailContact*
contact translations
contact tests cũ
```

Không được xóa hoặc sửa nghiệp vụ:

```text
VisitContactClaim
VisitContactTransfer
Primary Contact phía khách
```

Đây là nghiệp vụ khác, không liên quan email contact.

---

# 13. Logic 11 — Đồng bộ template và SQL

## 13.1 Script cũ cần sửa

Phải cập nhật:

```text
docs/database/scripts/email_template_cc_bcc_sync/02_sync_templates.sql
```

Không để script này:

- Khôi phục `{{contactInformationBlock}}`.
- Ghi body trước migration.
- Làm lệch defaults JSON, canonical SQL và DB.

## 13.2 Các nguồn phải đồng bộ

Nếu body template thay đổi:

- `email-template-defaults.json`
- canonical SQL
- patch SQL idempotent
- `02_sync_templates.sql`
- parity tests
- canonical SQL hash

## 13.3 Không đổi schema

Task này:

- Không tạo bảng.
- Không xóa bảng contact policy.
- Không tạo draft tables.
- Chỉ sửa seed/template data và code.

---

# 14. Logic 12 — Guard chống xóa nhầm database

Canonical SQL đang hard-code:

```sql
USE pems_db;
```

Đã từng làm mất dữ liệu local.

Phải thêm guard trong test/import tooling:

1. Test runner phải tạo database disposable với tên ngẫu nhiên.
2. Không được chạy canonical script trực tiếp vào `pems_db`.
3. Nếu target database là `pems_db`, dừng trước bất kỳ `DROP TABLE`.
4. Log rõ database target.
5. Có test hồi quy cho guard.
6. Không phụ thuộc current selected schema của MySQL Workbench.
7. Có thể preprocess script để thay `CREATE DATABASE/USE pems_db` bằng database disposable.

---

# 15. Logic 13 — Integration tests còn thiếu

Kết quả gần nhất:

```text
1362 / 1377 PASS
15 FAIL
```

Cần xử lý từng nhóm:

## 15.1 Declared-but-unused sender variables

Hai test cũ bắt buộc tất cả biến khai báo phải xuất hiện trong body.

Cập nhật rule:

- Sender variables được phép khai báo nhưng chưa dùng.
- Chỉ exemption với sender capability hợp lệ.
- Không bỏ toàn bộ validation.

## 15.2 `02_sync_templates.sql`

Sửa body cũ và chạy parity.

## 15.3 Authored-content / logistics / report E2E

Phải đọc failure thực tế.

Không sửa test nếu production behavior sai.

Kiểm tra:

- Final preview hash.
- Scope key.
- Action block placement.
- Attachment hash.
- Reply-To.
- Approved content resolution.
- Legacy `EmailOverride` expectation.

## 15.4 VisitContactClaim/Transfer tests

Thiết lập baseline trên commit sạch.

Nếu fail sẵn:

- Ghi bằng chứng pre-existing.

Nếu baseline xanh:

- Tìm regression thật.

Không sửa nghiệp vụ guest-side contact chỉ để test xanh.

---

# 16. Browser smoke bắt buộc

Chạy trực tiếp:

1. Logistics / Teabreak.
2. Participant invitation.
3. Department assignment.
4. Setup-progress.

Với mỗi flow:

- Bấm mắt.
- Xác nhận có nút Chỉnh sửa nếu editable.
- Xác nhận action block nằm trong body.
- Vào Edit.
- Di chuyển action block.
- Sửa sender content.
- Xem Final Preview.
- Gửi.
- Mở `.eml` hoặc email thật.
- So sánh bố cục, action vị trí, Reply-To, attachments.

---

# 17. Gates hoàn thành

Chỉ hoàn thành khi:

```text
Backend build: PASS
Backend unit: PASS
Architecture: PASS
Integration: PASS
Frontend typecheck: PASS
Frontend lint: PASS
Frontend unit: PASS
Frontend build: PASS
Browser smoke: PASS
.eml/provider parity: PASS
```

Không commit nếu dự án đang không runnable.

Không push nếu chưa được yêu cầu.

---

# 18. Thứ tự triển khai

## G0 — Preflight

- Branch/HEAD.
- WIP/stash.
- Không reset.
- Không checkout làm mất WIP.
- Backup patch hiện tại nếu cần.
- Ghi nhận local commits.
- Outbound email safety.

## G1 — Fix `canEdit`

- Audit template code.
- Audit capability.
- Audit response DTO.
- Audit frontend render.
- Test STAFF có quyền gửi vẫn edit được.

## G2 — Action block inline

- Template placeholder.
- Preview render đúng vị trí.
- Bỏ section action riêng.

## G3 — Editor system node

- Atomic node.
- Move/cut/paste.
- Count validation.
- Prevent edit/duplicate/fake block.

## G4 — Final Preview parity

- Finalize endpoint.
- Action rendering.
- Signed token.
- Stale detection.
- Exact send.

## G5 — Call site migration

- Logistics.
- Invitation.
- Department.
- Setup progress.

## G6 — Cleanup contact leftovers

- Backend.
- Frontend.
- Routes.
- Tests.
- Translations.

## G7 — SQL sync + DB guard

- `02_sync_templates.sql`.
- Defaults.
- Canonical SQL.
- Patch.
- Hash.
- Disposable DB guard.

## G8 — Tests

- Unit.
- Integration.
- Frontend.
- E2E.
- Browser smoke.

## G9 — Commit

Tách commit theo logic:

```text
fix(email): expose edit mode for editable templates
feat(email): render movable action blocks inside email content
feat(email): enforce final-preview exact-send parity
fix(db): guard canonical imports from the local pems database
test(email): cover action placement and preview-send parity
```

Không push.

---

# 19. Acceptance criteria

1. Email editable luôn có nút Chỉnh sửa.
2. Action block nằm trong nội dung preview.
3. Action block nằm đúng vị trí template.
4. Action block có thể di chuyển trong editor.
5. Action block không thể sửa URL/token/chức năng.
6. Action block không thể bị nhân đôi.
7. Template bắt buộc action không thể xóa block.
8. Sender variables parse đúng.
9. Sender text sau parse được sửa tự do.
10. Không gửi trực tiếp từ EDIT.
11. Final Preview bắt buộc trước khi gửi nội dung đã sửa.
12. Gửi đúng nội dung Final Preview.
13. Di chuyển action invalidates token cũ.
14. Reply-To đúng trên preview, SMTP và Resend.
15. Setup-progress dùng cùng pipeline.
16. Không còn email contact UI.
17. Không ảnh hưởng guest Primary Contact.
18. `02_sync_templates.sql` không restore body cũ.
19. Canonical importer không thể xóa nhầm `pems_db`.
20. Tất cả tests và browser smoke xanh.
21. Không tạo bảng mới.
22. Không push trước khi được yêu cầu.

---

# 20. Format báo cáo bắt buộc

```text
1. Preflight
- Branch:
- HEAD:
- WIP/stash:
- Local commits:

2. canEdit fix
- Affected templates:
- Root cause:
- Backend response:
- Frontend condition:

3. Action block
- Template placeholder:
- Preview placement:
- Editor node:
- Count validation:
- Security:

4. Preview pipeline
- VIEW:
- EDIT:
- FINAL_PREVIEW:
- Token invalidation:
- Exact send:

5. Call sites
- Logistics:
- Invitation:
- Department assignment:
- Setup progress:

6. Cleanup
- Contact backend removed:
- Contact frontend removed:
- Routes removed:
- Dead tests/translations:

7. SQL and DB safety
- 02_sync_templates:
- Defaults/canonical/patch:
- Hash:
- Disposable DB guard:

8. Tests
- Backend unit:
- Architecture:
- Integration:
- Frontend:
- Browser smoke:
- .eml/provider parity:

9. Schema
- Changed: NO
- New tables: NO
- Draft tables restored: NO

10. Commits
- SHA:
- Message:

11. Remaining debt
- Chỉ ghi debt có bằng chứng.
```
