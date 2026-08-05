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

---

# 21. Logic bổ sung — Chuẩn hóa và viết lại nội dung mặc định của 31 email template

## 21.1 Kết quả audit nội dung hiện tại

Nguồn audit:

- `Đã dán mã (1).json`: 31 template mặc định VI/EN.
- `PEMS_FULL_VS_31_07_NEW(4).sql`: canonical SQL chứa seed tương ứng.

Kết quả tổng quát:

```text
Tổng template:                  31
Template có {{actionBlock}}:    13
Template có sender block:       14
Template dùng danh sách <ul>:   10
Template dùng bảng <table>:      0
Template lặp card “NGƯỜI GỬI”: 14
```

Nội dung hiện tại chạy được về mặt kỹ thuật nhưng chưa đồng đều và chưa đủ chuyên nghiệp:

1. Nhiều email chỉ là các đoạn `<p>` nối tiếp, thiếu phân cấp thị giác.
2. Thông tin nghiệp vụ thường trình bày bằng bullet list đơn giản, khó quét nhanh.
3. Card `NGƯỜI GỬI/SENDER` bị lặp nguyên mẫu ở nhiều email, nhìn nặng và máy móc.
4. Một số email có sender block, một số email cùng nhóm nghiệp vụ lại không có.
5. Action block có vị trí trong body nhưng chưa có khu vực “Yêu cầu phản hồi” rõ ràng.
6. Nội dung Việt/Anh đúng ý chính nhưng giọng văn chưa đồng nhất giữa các nhóm.
7. Email báo cáo quá ngắn, chưa nói rõ tệp nào được đính kèm và người nhận cần làm gì.
8. Email thay đổi tài khoản/chức vụ chưa luôn nêu rõ:
   - điều gì vừa thay đổi;
   - hiệu lực từ khi nào;
   - người nhận cần làm gì tiếp theo;
   - cần liên hệ ai nếu thông tin sai.
9. Một số câu mang tính nội bộ hoặc kỹ thuật hơn là ngôn ngữ hướng tới người nhận.
10. Chưa có quy chuẩn chung cho tiêu đề, khoảng cách, summary card, note, footer và action area.

## 21.2 Mục tiêu viết lại template

Mỗi email phải giúp người nhận trả lời nhanh năm câu hỏi:

```text
1. Email này nói về việc gì?
2. Việc này liên quan tới ai/chuyến thăm nào?
3. Thông tin quan trọng nhất là gì?
4. Tôi cần làm gì tiếp theo?
5. Khi cần trao đổi, tôi trả lời về đâu?
```

Nội dung mới phải:

- chuyên nghiệp;
- ngắn gọn nhưng đủ bối cảnh;
- dễ đọc trên desktop và mobile;
- có bố cục rõ ràng;
- dùng đúng thuật ngữ PEMS;
- đồng nhất VI/EN;
- giữ nguyên biến nghiệp vụ;
- giữ đúng action/security policy;
- cho phép admin tiếp tục chỉnh sửa template.

---

# 22. Hệ thống bố cục email mặc định

Không ép mọi email dùng một body giống nhau. Chuẩn hóa theo các khối có thể kết hợp.

## 22.1 Khối mở đầu

```html
<p style="margin:0 0 16px">
  Kính gửi <strong>{{recipientName}}</strong>,
</p>
```

Quy tắc:

- Dùng `Kính gửi` cho khách/đối tác và thông báo trang trọng.
- Dùng `Xin chào` cho thông báo nội bộ thông thường.
- Không dùng tên placeholder không phù hợp với đối tượng nhận.
- Không lặp lại lời chào trong nhiều section.

## 22.2 Khối tóm tắt nghiệp vụ

Dùng bảng HTML tương thích email thay cho danh sách dài:

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

Dùng `role="presentation"` để tránh làm screen reader hiểu đây là bảng dữ liệu phức tạp.

Không dùng CSS hiện đại không ổn định trong email như:

- flex/grid cho bố cục chính;
- position fixed/sticky;
- JavaScript;
- external stylesheet phụ thuộc;
- animation.

## 22.3 Khối yêu cầu hành động

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

Trong preview/edit pipeline, action block phải được xử lý bằng system node có thể di chuyển nhưng không thể sửa chức năng.

## 22.4 Khối ghi chú/cảnh báo

Dùng cho OTP, thay đổi email, khóa tài khoản và link dùng một lần:

```html
<div style="margin:18px 0;padding:14px 16px;
            background:#fff7ed;border:1px solid #fed7aa;
            border-radius:8px;color:#9a3412">
  <strong>Lưu ý bảo mật:</strong>
  Không chia sẻ mã hoặc liên kết này với bất kỳ ai.
</div>
```

Không dùng màu sắc là tín hiệu duy nhất; nội dung phải nói rõ đây là cảnh báo.

## 22.5 Khối người gửi

Sender block chỉ xuất hiện khi template thật sự cần.

Đổi từ nhãn máy móc:

```text
NGƯỜI GỬI
```

thành:

```text
Thông tin người gửi
```

Bố cục đề xuất:

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

Không bắt buộc hiển thị tất cả sáu biến. Template chỉ dùng trường cần thiết.

## 22.6 Footer

Footer ngắn, thống nhất:

```html
<p style="margin:22px 0 0;color:#64748b;font-size:12px;line-height:1.6">
  Trân trọng,<br/>
  <strong>PEMS – FPT University</strong>
</p>
```

Email tự động có thể thêm:

```text
Đây là email tự động từ PEMS. Vui lòng không chia sẻ mã hoặc liên kết bảo mật.
```

Không ghi “không trả lời email này” nếu Reply-To vẫn được cấu hình để nhận phản hồi.

---

# 23. Phân nhóm và cấu trúc nội dung bắt buộc

## 23.1 Email OTP và bảo mật

Template:

- `AUTH_PASSWORD_RESET_OTP`
- `VISIT_REQUEST_OTP`
- `ACCOUNT_EMAIL_CONFIRMATION`

Bố cục:

```text
Lời chào
→ Mục đích của mã/liên kết
→ OTP hoặc action block nổi bật
→ Thời hạn
→ Cảnh báo bảo mật
→ Footer
```

Yêu cầu:

- Không sender cá nhân.
- Không nội dung dài.
- Không CC/BCC.
- Không nút hoặc link thừa.
- Nêu rõ thời hạn và dùng một lần.
- Không tiết lộ thông tin tài khoản không cần thiết.

## 23.2 Email tài khoản và phân quyền

Template nhóm:

- account activated;
- email changed;
- role changed;
- Staff Leader assigned/replaced;
- department leadership granted/handed over;
- personnel enabled/disabled.

Bố cục:

```text
Trạng thái mới
→ Chi tiết thay đổi
→ Thời điểm hiệu lực
→ Ảnh hưởng tới quyền/đăng nhập
→ Việc cần làm tiếp theo
→ Thông tin người gửi nếu cần
→ Footer
```

Không chỉ nói “đã thay đổi”; phải nêu rõ tác động.

## 23.3 Logistics request/proposal/assignment

Template nhóm:

- `LOGISTICS_REQUEST_TO_DEPARTMENT`
- `LOGISTICS_CHANGE_PROPOSAL_TO_HOST`
- `LOGISTICS_ASSIGNEE_ASSIGNMENT`
- `LOGISTICS_EXPENSE_REPORT_REMINDER`

Bố cục:

```text
Lời chào
→ Một câu tóm tắt yêu cầu
→ Bảng thông tin hạng mục
→ Nội dung/ghi chú chi tiết
→ Khu vực phản hồi
→ Action block
→ Sender
→ Footer
```

Thông tin thời gian phải cùng định dạng và dễ quét.

## 23.4 Invitation và assignment

Template nhóm:

- participant invitation;
- student invitation;
- department leader invitation;
- department staff assignment;
- contact claim/transfer.

Bố cục:

```text
Lời chào
→ Lý do người nhận được mời/phân công
→ Bảng chuyến thăm
→ Vai trò/trách nhiệm
→ Tin nhắn của Host nếu có
→ Hạn hoặc yêu cầu phản hồi
→ Action block
→ Sender nếu phù hợp
→ Footer
```

Phải phân biệt rõ:

- “được mời”;
- “được phân công”;
- “đề nghị tiếp nhận vai trò”.

## 23.5 Reminder

Template:

- `VISIT_REMINDER_HOST`
- `VISIT_REMINDER_PARTICIPANTS`

Bố cục:

```text
Lời nhắc ngắn
→ Thời gian/địa điểm
→ Việc cần kiểm tra hoặc chuẩn bị
→ Action nếu thật sự có hành động
→ Footer
```

Không dùng action block chỉ để mở trang nếu không có hành động nghiệp vụ rõ ràng.

## 23.6 Report và invoice

Template:

- `REPORT_CAMPUS_OPERATION`
- `REPORT_DEPARTMENT_COLLABORATION`
- `REPORT_DEPARTMENT_INVOICE`
- `REPORT_PERSONNEL_PERFORMANCE`

Bố cục:

```text
Lời chào
→ Tên báo cáo
→ Phạm vi thời gian
→ Nội dung chính của tệp
→ Tệp đính kèm
→ Việc người nhận cần làm
→ Footer
```

Ví dụ phải nói rõ:

```text
Tệp đính kèm: Báo cáo vận hành campus – PDF
Vui lòng xem và phản hồi trước ... nếu phát hiện số liệu chưa chính xác.
```

Chỉ thêm deadline nếu có biến/nghiệp vụ thật; không tự bịa ngày.

## 23.7 Setup progress

Template:

- `VISIT_SETUP_PROGRESS_UPDATE`

Bố cục:

```text
Lời chào trang trọng
→ Tóm tắt chuyến thăm
→ setupSummaryBlock
→ Trạng thái/các điểm cần lưu ý
→ Thông tin tệp Schedule Report
→ Hướng dẫn phản hồi
→ Sender
→ Footer
```

Không lặp `hostName` và sender nếu cùng một người theo cách gây rối. Nội dung chỉ cần nói rõ người gửi thực tế và Reply-To.

---

# 24. Quy chuẩn viết subject

Subject phải:

- bắt đầu bằng `[PEMS]`;
- nêu đúng loại sự kiện;
- chứa một định danh hữu ích;
- không dài quá mức;
- không dùng từ mơ hồ như “Thông báo mới”;
- không đưa dữ liệu nhạy cảm;
- VI/EN tương đương về ý nghĩa.

Mẫu:

```text
[PEMS] Yêu cầu hậu cần cần phản hồi – {{logisticsTitle}}
[PEMS] Lời mời hỗ trợ tiếp đoàn – {{delegationName}}
[PEMS] Cập nhật chuẩn bị chuyến thăm – {{delegationName}}
[PEMS] Báo cáo vận hành campus – {{campusName}} | {{periodFrom}}–{{periodTo}}
```

Không tự đổi toàn bộ subject nếu biến hiện có không hỗ trợ nội dung mới.

---

# 25. Quy chuẩn ngôn ngữ Việt/Anh

## 25.1 Tiếng Việt

- Ưu tiên câu ngắn và chủ động.
- Dùng nhất quán:
  - “chuyến thăm”;
  - “đoàn khách”;
  - “người phụ trách tiếp đón”;
  - “hạng mục hậu cần”;
  - “phòng ban phụ trách”.
- Tránh trộn tiếng Anh không cần thiết trong body.
- Các vai trò chính thức có thể giữ tên hệ thống nếu dự án đã chuẩn hóa.

## 25.2 Tiếng Anh

- Không dịch từng chữ từ tiếng Việt.
- Dùng cùng mức trang trọng.
- Dùng nhất quán:
  - delegation visit;
  - reception owner/visit host theo glossary đã chốt;
  - logistics item;
  - department;
  - campus.
- Không dùng “When” ở template này nhưng “Time” ở template khác nếu cùng cấu trúc.
- Không làm mất thông tin có ở bản VI.

## 25.3 Parity

Mỗi cặp VI/EN phải có:

- cùng số section nghiệp vụ;
- cùng action block;
- cùng cảnh báo;
- cùng attachment note;
- cùng sender fields;
- cùng điều kiện bảo mật.

---

# 26. Quy chuẩn khả năng tương thích email

HTML mặc định phải hoạt động tốt với Gmail, Outlook và các client phổ biến.

Bắt buộc:

- CSS inline.
- Dùng table layout cho bảng thông tin.
- `border-collapse:collapse`.
- Font fallback an toàn.
- Link có label rõ.
- Không dựa vào hover.
- Màu chữ đủ tương phản.
- Nút có padding đủ lớn.
- Có nội dung văn bản ngoài màu sắc/icon.
- Không dùng JavaScript.
- Không dùng form/input bên trong email.
- Không dùng CSS class phụ thuộc stylesheet ngoài.
- Không dùng ảnh cho nội dung chữ quan trọng.

Action buttons vẫn do renderer hệ thống tạo.

---

# 27. Cách triển khai việc viết lại 31 template

## G-T1 — Audit từng template

Tạo matrix:

| Template | Nhóm | Người nhận | Mục tiêu | Hành động | Attachment | Sender | Editable |
|---|---|---|---|---|---|---|---|

Không viết lại hàng loạt bằng replace mù.

## G-T2 — Tạo style guide và snippet chuẩn

Tạo tài liệu/source nội bộ cho:

- summary table;
- action section;
- warning note;
- sender card;
- attachment note;
- footer.

Không biến chúng thành trusted block mới. Đây chỉ là HTML mặc định để admin vẫn chỉnh được.

## G-T3 — Viết lại theo nhóm

Thứ tự:

1. Logistics và invitation — ảnh hưởng trực tiếp modal đang kiểm tra.
2. Setup progress.
3. Account/role notices.
4. Reports/invoices.
5. Reminder.
6. OTP/security.

## G-T4 — Đồng bộ nguồn

Mỗi thay đổi phải cập nhật đồng thời:

- `email-template-defaults.json`;
- canonical SQL;
- patch SQL idempotent;
- `02_sync_templates.sql`;
- test fixtures/snapshots;
- canonical hash khi file SQL đổi.

## G-T5 — Preview thực tế

Preview ít nhất:

- desktop;
- modal width hẹp;
- Gmail-like width 600px;
- nội dung dài;
- biến trống;
- VI có dấu;
- EN;
- action block;
- sender block;
- attachment note.

## G-T6 — Browser và email-client smoke

Gửi `.eml` hoặc email test để kiểm tra:

- khoảng cách;
- line-height;
- bảng;
- action button;
- wrap subject;
- mobile width;
- Outlook/Gmail degradation hợp lý.

---

# 28. Test bắt buộc cho template content

## 28.1 Contract tests

- 31 template có VI/EN.
- Không có body rỗng.
- Không có mojibake.
- Không có `{{contactInformationBlock}}` trong body hoạt động.
- Không có placeholder ngoài registry.
- `{{actionBlock}}` đúng capability.
- Template action bắt buộc có đúng một action placeholder.
- Template không action không có action placeholder.
- Sender variables đúng capability.
- Không có `<script>`, `<form>`, event handler hoặc URL không an toàn.

## 28.2 Layout tests

- Summary table có `role="presentation"`.
- Không có CSS grid/flex phụ thuộc.
- Không có stylesheet ngoài.
- Không có chiều rộng cố định vượt wrapper.
- Action block không bị append ngoài vị trí template.
- Sender block không lặp hai lần.
- Footer chỉ xuất hiện một lần.

## 28.3 Content tests

- Email action-required có câu hướng dẫn rõ trước nút.
- OTP có thời hạn và cảnh báo không chia sẻ.
- Report có attachment note.
- Account deactivation có reason và next step.
- Invitation phân biệt invite/assignment.
- VI/EN có cùng action và section quan trọng.

Không nên kiểm tra nguyên câu chữ quá cứng nếu admin được phép chỉnh. Kiểm tra contract và section marker quan trọng.

---

# 29. Acceptance criteria bổ sung

23. Toàn bộ 31 template đã được audit theo nhóm.
24. Nội dung mặc định VI/EN chuyên nghiệp và nhất quán.
25. Logistics/invitation có summary table dễ đọc.
26. Email có action hiển thị khu vực phản hồi rõ ràng.
27. Action block nằm đúng vị trí trong body.
28. Sender card chỉ xuất hiện khi cần và dùng wording thống nhất.
29. Email báo cáo nói rõ tệp đính kèm và mục đích.
30. Email bảo mật ngắn gọn, có expiry và safety note.
31. Không dùng HTML/CSS không tương thích email phổ biến.
32. Defaults JSON, canonical SQL, patch và sync script byte-consistent về body.
33. VI/EN parity tests xanh.
34. Browser/email-client smoke đạt.
35. Nội dung mới không thay đổi nghiệp vụ, người nhận, token hoặc quyền gửi.

---

# 30. Commit đề xuất cho phần template

```text
docs(email): define the professional template content and layout standard
refactor(email): rewrite default transactional email templates
fix(db): synchronize professional email template seed content
test(email): verify bilingual template content and layout contracts
```

Không gộp việc viết lại 31 template vào commit action-block nếu diff quá lớn. Tách riêng để review được nội dung VI/EN.

---

# 31. Logic bổ sung — Đồng bộ tuyệt đối Editor ↔ Preview ↔ Email thực gửi

## 31.1 Vấn đề hiện tại

Màn chỉnh sửa template và phần **Xem trước hiển thị** đang không phản ánh cùng một kết quả.

Ví dụ:

- Người dùng thêm nhiều dấu cách trước `{{senderPhone}}` để đẩy nội dung sang phải.
- Editor vẫn hiển thị khoảng trắng hoặc vị trí con trỏ như đã nhập.
- Preview lại gom các dấu cách thành một dấu cách.
- Email thực gửi có thể tiếp tục khác preview tùy renderer/sanitizer/email client.

Nguyên nhân chính:

```text
HTML mặc định gom nhiều dấu cách liên tiếp thành một dấu cách.
```

Ví dụ:

```html
<p>       {{senderPhone}}</p>
```

sẽ được trình duyệt/email client hiển thị gần giống:

```text
0901234567
```

chứ không thụt vào theo số dấu cách đã nhập.

Đây không chỉ là lỗi UI nhỏ. Nó làm người cấu hình không biết bố cục cuối cùng có đúng như mong muốn hay không.

## 31.2 Nguyên tắc bắt buộc

Không dùng dấu cách thường để căn chỉnh bố cục.

Editor, preview và email thực gửi phải cùng dùng một pipeline:

```text
Editor HTML
→ normalize
→ sanitize
→ substitute variables
→ render preview
→ render final email
```

Không được có ba cách render khác nhau.

Acceptance:

```text
Editor được lưu
≈ Preview quản trị
≈ Final Preview khi gửi
≈ HTML thực tế trong .eml/provider output
```

Sai khác chỉ được phép ở wrapper MIME hoặc các khác biệt nhỏ do email client, không được thay đổi thứ tự, thụt lề, căn chỉnh hoặc khoảng cách chính.

## 31.3 Không hỗ trợ căn chỉnh bằng dấu cách thường

Khi người dùng nhập nhiều dấu cách liên tiếp:

- Editor không được giả vờ rằng khoảng cách đó sẽ được giữ nguyên.
- Có thể normalize thành một dấu cách ngay trong editor.
- Hoặc hiển thị cảnh báo:

```text
Dấu cách liên tiếp không được dùng để căn chỉnh trong email.
Vui lòng dùng công cụ thụt lề hoặc căn lề.
```

Không tự chuyển hàng loạt dấu cách thành `&nbsp;` vì:

- dễ tạo nội dung khó wrap trên mobile;
- có thể làm email tràn ngang;
- khó chỉnh sửa;
- dễ tạo khác biệt giữa Gmail và Outlook.

## 31.4 Bổ sung công cụ căn chỉnh đúng

Toolbar editor cần hỗ trợ rõ ràng:

```text
Căn trái
Căn giữa
Căn phải
Tăng thụt lề
Giảm thụt lề
Danh sách
Bảng thông tin
```

### Căn lề

Sinh HTML inline an toàn:

```html
<p style="text-align:right">...</p>
```

### Thụt lề

Không dùng nhiều dấu cách.

Dùng:

```html
<p style="margin-left:24px">...</p>
```

hoặc wrapper table tương thích email:

```html
<table role="presentation" width="100%" cellpadding="0" cellspacing="0">
  <tr>
    <td style="padding-left:24px">
      ...
    </td>
  </tr>
</table>
```

Ưu tiên mức thụt lề cố định:

```text
0px
16px
32px
48px
```

Không cho nhập arbitrary CSS.

## 31.5 Preview phải dùng đúng HTML đã lưu

Preview quản trị không được dựng lại body bằng plain text hoặc parser khác.

Flow đúng:

```text
Rich-text editor xuất HTML
→ backend sanitize/normalize
→ backend substitute sample variables
→ frontend hiển thị chính HTML backend trả về
```

Không được:

- lấy `innerText`;
- tự convert `<p>` thành newline;
- tự strip style hợp lệ;
- tự nối biến bằng string khác;
- dùng một renderer riêng chỉ cho preview.

## 31.6 Biến phải giữ nguyên style của vị trí chèn

Ví dụ:

```html
<p style="text-align:right;margin-left:32px">
  {{senderPhone}}
</p>
```

Sau parse:

```html
<p style="text-align:right;margin-left:32px">
  0901234567
</p>
```

Không được biến thành:

```html
<p>0901234567</p>
```

Substitution chỉ thay giá trị biến, không thay wrapper, style hoặc vị trí.

## 31.7 Sanitizer phải có allow-list thống nhất

Cho phép tối thiểu:

```text
text-align
margin-left
margin-right
margin-top
margin-bottom
padding
padding-left
padding-right
font-weight
font-size
line-height
color
background-color
border
border-radius
width
```

Chỉ cho phép giá trị an toàn và giới hạn.

Cấm:

```text
position
z-index
javascript:
expression()
behavior
external CSS
event handler
```

Allow-list phải được dùng chung cho:

- save template;
- preview template;
- final preview;
- authored content;
- send.

## 31.8 Dirty-state và round-trip fidelity

Khi mở template rồi không sửa:

```text
loaded HTML
→ editor internal model
→ serialized HTML
```

phải canonical-equivalent.

Không được vừa mở đã báo dirty do:

- editor thêm/bớt `<p>`;
- đổi `<br>` thành `<p>`;
- đổi thứ tự style;
- bỏ khoảng trắng không có nghĩa;
- normalize quote khác nhau.

Cần canonicalizer trước khi so sánh dirty state.

## 31.9 Test bắt buộc

### Editor → Preview

- Căn trái/giữa/phải giữ nguyên.
- Thụt lề 16/32/48px giữ nguyên.
- Biến trong đoạn có style giữ nguyên wrapper.
- Nhiều dấu cách thường được normalize hoặc cảnh báo.
- Không tự biến thành bố cục khác.

### Preview → Send

- `finalPreviewHtml` và MIME body giữ cùng:
  - text alignment;
  - margin/padding;
  - action block position;
  - sender block position;
  - bảng;
  - khoảng cách section.

### Round trip

- Load template rồi save không sửa không làm thay đổi semantic HTML.
- Không phát sinh dirty state giả.
- VI/EN cùng behavior.

### Email client smoke

- Gmail.
- Outlook.
- Mobile width.

Kiểm tra đặc biệt:

- thụt lề không làm tràn ngang;
- bảng không vượt container;
- `&nbsp;` không bị lạm dụng;
- nội dung vẫn wrap.

## 31.10 Acceptance criteria bổ sung

36. Editor không dùng dấu cách thường như công cụ layout.
37. Có nút căn lề và thụt lề rõ ràng.
38. Preview quản trị dùng cùng renderer với final email.
39. Style hợp lệ quanh biến không bị mất sau parse.
40. Save → reload không làm đổi semantic HTML.
41. Không còn dirty state giả do normalize HTML.
42. Final Preview và email thực gửi giữ nguyên bố cục chính.
43. Có test Gmail/Outlook/mobile cho alignment và indentation.
