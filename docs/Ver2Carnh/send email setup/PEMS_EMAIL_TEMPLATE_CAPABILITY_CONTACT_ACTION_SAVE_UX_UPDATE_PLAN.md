# PEMS — KẾ HOẠCH CẬP NHẬT LOGIC QUẢN LÝ MẪU EMAIL

## 1. Mục tiêu

Cập nhật màn hình:

```text
Quản lý email → Cấu hình mẫu email → Chỉnh sửa nội dung mẫu email
```

để xử lý dứt điểm các vấn đề sau:

1. `{{actionBlock}}` đang bị mô tả trùng lặp.
2. Một số template không có action thực tế nhưng UI vẫn mô tả như sẽ có nút.
3. Template không hỗ trợ `{{contactInformationBlock}}` vẫn hiện đầy đủ form cấu hình liên hệ.
4. Người dùng có thể chọn cấu hình liên hệ nhưng khi thêm block lại bị:
   ```text
   EMAIL_TEMPLATE_SYSTEM_BLOCK_NOT_ALLOWED
   ```
5. `Mô tả quản trị` và một số trường văn bản bị cắt, không xem được đầy đủ.
6. Nút `Cập nhật` chưa nói rõ lưu phần nào.
7. Nội dung mẫu và cấu hình liên hệ là hai nhóm lưu riêng nhưng UI chưa thể hiện rõ trạng thái chưa lưu.
8. Phần khôi phục nội dung và khôi phục cấu hình liên hệ cần tách biệt.
9. Không được sửa backend theo hướng mở contact block cho toàn bộ template một cách cơ học.

---

# 2. Quyết định nghiệp vụ

## 2.1. Không hỗ trợ thông tin liên hệ cho tất cả template

Không được sửa backend để toàn bộ email đều có thể dùng:

```text
{{contactInformationBlock}}
```

Lý do:

- Email OTP/token hoặc liên kết dùng một lần không nên mở rộng dữ liệu bị lộ khi bị chuyển tiếp.
- Một số email gửi cho chính người phụ trách, khối liên hệ là dư thừa.
- Một số email không có đủ context để resolve Host, campus hoặc department.
- `REQUIRED` có thể chặn các luồng gửi quan trọng nếu không tìm được đầu mối.
- Bật đại trà làm thay đổi nghiệp vụ và bảo mật toàn bộ catalog.

Phải phân loại theo capability thật của từng template.

## 2.2. Ba trạng thái contact capability

Backend cần thể hiện rõ một trong ba trạng thái:

```text
UNSUPPORTED
SUPPORTED
REQUIRED
```

Tên enum/DTO cuối cùng có thể khác, nhưng ý nghĩa phải tương đương.

### UNSUPPORTED

- Template không cho dùng `{{contactInformationBlock}}`.
- Không hiển thị form cấu hình liên hệ.
- Không cho chèn block.
- Validator từ chối nếu body chứa block.
- UI chỉ hiện lý do ngắn gọn.

Ví dụ:

```text
ACCOUNT_EMAIL_CONFIRMATION
```

### SUPPORTED

- Cho phép `NONE`, `OPTIONAL`, `REQUIRED` theo policy.
- Hiển thị đầy đủ form cấu hình.
- Cho phép lưu và phục hồi cấu hình liên hệ.

### REQUIRED

- Template bắt buộc có contact block theo nghiệp vụ.
- Không cho lưu body nếu thiếu block.
- Có thể khóa lựa chọn `Không hiển thị`, hoặc disable kèm lý do.

---

# 3. Audit trước khi sửa

Phải audit toàn bộ catalog hiện tại, không chỉ riêng một template.

Tạo bảng:

| Template code | Có action thực tế | Action bắt buộc | Có contact support | Contact bắt buộc | Sensitive/token | Send point thật | Verdict |
|---|---:|---:|---:|---:|---:|---:|---|

Tối thiểu kiểm tra:

```text
ACCOUNT_EMAIL_CONFIRMATION
ACCOUNT_ACTIVATED
DEPT_PERSONNEL_ACCOUNT_ENABLED
VISIT_PARTICIPANT_INVITATION
VISIT_STUDENT_INVITATION
LOGISTICS_REQUEST_TO_DEPARTMENT
LOGISTICS_EXPENSE_REPORT_REMINDER
```

Không được suy luận theo tên template. Phải dựa trên:

- registry;
- contract;
- action metadata/spec;
- contact policy;
- body VI/EN;
- send point;
- preview;
- runtime email thật/file sink.

---

# 4. Cập nhật `actionBlock`

## 4.1. Vấn đề hiện tại

UI đang hiển thị đồng thời hai mô tả:

```text
{{actionBlock}} bắt buộc giữ
Nút "Xác nhận email" ... sẽ được hệ thống tự gắn khi gửi.
```

và:

```text
{{actionBlock}} bắt buộc giữ
Hệ thống gắn các nút (đồng ý / từ chối / xem chi tiết) ...
```

Đây là trùng lặp giữa:

- mô tả action cụ thể từ backend;
- mô tả generic ở frontend.

## 4.2. Rule mới

Mỗi system block chỉ hiển thị một lần.

```text
Có metadata cụ thể từ backend
→ dùng metadata cụ thể

Không có metadata cụ thể
→ mới dùng mô tả generic
```

Không nối hai mô tả lại.

## 4.3. UI mong muốn

Với `ACCOUNT_EMAIL_CONFIRMATION`:

```text
{{actionBlock}} — Bắt buộc giữ

Khi gửi, hệ thống sẽ chèn nút “Xác nhận email”.
Liên kết chỉ dùng một lần và hết hiệu lực theo thời hạn của email.
```

Không hiện thêm:

```text
Hệ thống gắn các nút đồng ý / từ chối / xem chi tiết...
```

## 4.4. Template không có action thật

Nếu send point không cung cấp action:

- Không hiện `{{actionBlock}}`.
- Không cho chèn `{{actionBlock}}`.
- Preview không render nút.
- Validator từ chối nếu người dùng tự thêm block.
- Không tự thêm nút mới chỉ để hợp thức hóa contract.

## 4.5. Backend contract đề xuất

Contract cần trả metadata rõ ràng, ví dụ:

```json
{
  "actionBlock": {
    "supported": true,
    "required": true,
    "labelsVi": ["Xác nhận email"],
    "labelsEn": ["Confirm email"],
    "descriptionVi": "Liên kết chỉ dùng một lần.",
    "descriptionEn": "The link can only be used once.",
    "previewHtml": "<span>...</span>"
  }
}
```

Không bắt buộc dùng đúng schema này, nhưng phải phân biệt:

```text
allowed
supported
required
has runtime action spec
body contains block
```

Frontend không được hard-code danh sách template có action.

---

# 5. Cập nhật `contactInformationBlock`

## 5.1. Template không hỗ trợ contact block

Ví dụ:

```text
ACCOUNT_EMAIL_CONFIRMATION
```

Card số 4 chỉ hiển thị:

```text
4. Thông tin liên hệ

Mẫu này không sử dụng khối thông tin liên hệ vì email
chứa liên kết xác nhận dùng một lần.

Không có cấu hình cần chỉnh sửa.
```

Không hiển thị:

- radio `Không hiển thị / Tùy chọn / Bắt buộc`;
- nguồn đầu mối;
- checkbox trường hiển thị;
- Reply-To;
- nút lưu;
- nút phục hồi cấu hình liên hệ.

## 5.2. Template hỗ trợ contact block

Hiển thị đầy đủ:

- Mức hiển thị.
- Nguồn đầu mối.
- Email.
- Số điện thoại.
- Phòng ban.
- Cơ sở.
- Dòng “Được gửi bởi”.
- Tiêu đề VI/EN.
- Reply-To.
- Lưu cấu hình liên hệ.
- Phục hồi cấu hình liên hệ.

## 5.3. Template bắt buộc contact block

- Body VI/EN phải có:
  ```text
  {{contactInformationBlock}}
  ```
- Không cho lưu mức `REQUIRED` nếu body thiếu block.
- Nếu người dùng xóa block, nút lưu nội dung phải bị chặn.
- Thông báo phải nói đúng việc cần làm.

## 5.4. Xử lý block không hợp lệ

Nếu body chứa:

```text
{{contactInformationBlock}}
```

nhưng template không hỗ trợ:

Hiển thị:

```text
Khối thông tin liên hệ không được hỗ trợ ở mẫu này.
Hãy xóa {{contactInformationBlock}} khỏi nội dung.
```

Có thể thêm nút:

```text
Xóa khối không hợp lệ
```

Không tự xóa nếu chưa xác nhận.

---

# 6. Bỏ thông tin kế thừa không có giá trị thao tác

Bỏ khỏi UI:

```text
N trường chưa đặt riêng cho mẫu này...
Đang kế thừa · cấu hình hệ thống
```

Backend vẫn được giữ:

- provenance;
- cascade;
- system baseline;
- shipped default.

Frontend chỉ hiển thị giá trị đang có hiệu lực.

Chỉ hiển thị thông tin kế thừa trở lại nếu sau này có đủ:

- màn hình sửa cấu hình SYSTEM;
- nút quay lại kế thừa;
- thao tác rõ ràng cho người dùng.

---

# 7. Cập nhật “Mô tả quản trị” và bố cục thông tin chung

## 7.1. Vấn đề

`Mô tả quản trị` đang dùng input một dòng, chiều rộng hẹp nên bị cắt chữ.

## 7.2. Bố cục mới

```text
Mã mẫu                         Trạng thái
[ACCOUNT_EMAIL_CONFIRMATION]   [Đang hoạt động]

Tên mẫu
[Xác nhận email để kích hoạt tài khoản                     ]

Mô tả quản trị
[ Gửi cho tài khoản vừa tạo ở trạng thái chờ xác nhận      ]
[ email. Liên kết chỉ dùng một lần...                       ]
```

Yêu cầu:

- `Tên mẫu` dùng toàn chiều rộng.
- `Mô tả quản trị` dùng `textarea`.
- Tự giãn tối thiểu 2 dòng, tối đa 4–6 dòng.
- Có wrap.
- Không ellipsis trong màn hình chỉnh sửa.
- Có `maxLength` đúng với backend/schema.
- Có bộ đếm ký tự nếu đã có design pattern tương tự.
- Không làm layout tràn ngang.

Trong danh sách template có thể dùng line-clamp 2 dòng và tooltip.

---

# 8. Tách rõ hai nhóm lưu

## 8.1. Lưu nội dung mẫu

Đổi nút:

```text
Cập nhật
```

thành:

```text
Lưu thay đổi mẫu
```

Nút này chỉ lưu:

- Tên mẫu.
- Mô tả quản trị.
- Subject VI.
- Body VI.
- Subject EN.
- Body EN.

## 8.2. Lưu cấu hình liên hệ

Nút trong card 4:

```text
Lưu cấu hình liên hệ
```

chỉ lưu:

- Requirement.
- Contact source.
- Visibility fields.
- Heading VI/EN.
- Reply-To.

Không tạo nút `Lưu tất cả` nếu backend chưa có transaction chung.

---

# 9. Dirty state và cảnh báo khi đóng

## 9.1. Nội dung mẫu

Khi có thay đổi chưa lưu:

```text
● Nội dung mẫu có thay đổi chưa lưu
```

Nút `Lưu thay đổi mẫu` được bật.

## 9.2. Cấu hình liên hệ

Khi có thay đổi chưa lưu:

```text
● Cấu hình liên hệ có thay đổi chưa lưu
```

Nút `Lưu cấu hình liên hệ` được bật.

## 9.3. Đóng màn hình

Nếu còn thay đổi:

```text
Bạn có thay đổi chưa lưu ở:
• Nội dung mẫu
• Cấu hình liên hệ
```

Chỉ liệt kê nhóm thực sự dirty.

Không dùng câu chung chung nếu có thể xác định chính xác.

---

# 10. Phục hồi mặc định

## 10.1. Phục hồi nội dung mẫu

Đổi nhãn:

```text
Phục hồi nội dung mẫu
```

Chỉ phục hồi:

- name;
- description;
- subjectVi;
- bodyVi;
- subjectEn;
- bodyEn.

Không thay đổi contact policy.

Hộp xác nhận:

```text
Tên mẫu, mô tả quản trị, tiêu đề và nội dung VI/EN sẽ
trở về bản mặc định. Cấu hình thông tin liên hệ không thay đổi.
```

## 10.2. Phục hồi cấu hình liên hệ

Chỉ hiển thị với template hỗ trợ contact block.

Endpoint riêng đề xuất:

```http
POST /api/email-templates/{templateCode}/contact-settings/restore-default
```

Chỉ phục hồi:

- requirement;
- contact source;
- visibility fields;
- heading VI/EN;
- Reply-To.

Nguồn mặc định phải lấy từ:

```text
EmailContactPolicyDefaults.For(templateCode)
```

Không sửa nội dung template.

## 10.3. Validation khi phục hồi contact

Nếu default là `REQUIRED` nhưng body hiện tại thiếu:

```text
{{contactInformationBlock}}
```

thì phải từ chối và báo:

```text
Không thể phục hồi cấu hình liên hệ về mức Bắt buộc vì
nội dung email chưa có {{contactInformationBlock}}.
```

Không tự sửa body.

---

# 11. Backend changes

## 11.1. Contract capability

Bổ sung metadata capability cho:

- action block;
- contact block.

Không chỉ dựa vào:

```text
requiredSystemBlocks
optionalSystemBlocks
```

Contract cần trả rõ:

```text
actionSupported
actionRequired
contactSupported
contactRequired
contactSettingsEditable
reasonCode/reasonText
```

Tên cuối cùng tùy architecture hiện tại.

## 11.2. Validator

Phải fail-closed:

- action không hỗ trợ nhưng body có block → `SYSTEM_BLOCK_NOT_ALLOWED`;
- contact không hỗ trợ nhưng body có block → `SYSTEM_BLOCK_NOT_ALLOWED`;
- required action bị thiếu → lỗi;
- required contact bị thiếu → lỗi;
- system block không được đặt trong subject.

## 11.3. Contact settings endpoints

- GET không trả form edit giả cho template unsupported.
- PUT phải từ chối template unsupported.
- Preview không render contact block cho template unsupported.
- Restore default chỉ áp dụng template supported.

## 11.4. Authorization

- Chỉ HO được sửa.
- Các role khác chỉ đọc khi nghiệp vụ cần.
- Không mở rộng quyền chỉ để UI hoạt động.

---

# 12. Frontend changes

Ưu tiên audit và cập nhật:

```text
TemplateManagement.tsx
ContactSettingsPanel.tsx
templateContract.ts
emailsApi.ts
TemplateManagement.test.tsx
contact settings tests
```

Yêu cầu:

- Không hard-code template capability.
- Không nối generic hint và specific hint.
- Không render card contact edit cho unsupported template.
- Không giữ state contract của template trước khi chuyển template.
- Textarea full width cho mô tả.
- Dirty state riêng.
- Nút lưu rõ phạm vi.
- Restore rõ phạm vi.
- Error message ngắn, đúng nghiệp vụ.

---

# 13. Tests bắt buộc

## 13.1. Action block

1. `ACCOUNT_EMAIL_CONFIRMATION` chỉ hiện một mô tả action.
2. Không còn generic hint trùng với specific hint.
3. Template không có action không hiện action block.
4. Template không có action không cho chèn block.
5. Preview dùng nhãn VI/EN đúng.
6. Preview không có token/URL thật.
7. Validator chặn block không hỗ trợ.

## 13.2. Contact capability

1. `ACCOUNT_EMAIL_CONFIRMATION` không hiện form cấu hình contact.
2. Unsupported template không có nút lưu/phục hồi contact.
3. Unsupported template có block trong body bị lỗi.
4. Supported template hiện đầy đủ form.
5. Required template thiếu block bị chặn.
6. PUT contact-settings từ chối unsupported template.
7. Restore contact từ chối unsupported template.
8. Preview unsupported trả rỗng hoặc lỗi capability rõ ràng.

## 13.3. Mô tả quản trị

1. Dùng textarea.
2. Hiện đầy đủ nội dung nhiều dòng.
3. Không bị ellipsis trong editor.
4. Max length đúng backend.
5. Lưu và mở lại giữ nguyên nội dung.

## 13.4. Save state

1. Sửa tên → nội dung mẫu dirty.
2. Sửa mô tả → nội dung mẫu dirty.
3. Sửa subject/body → nội dung mẫu dirty.
4. Sửa contact → contact dirty.
5. Lưu nội dung không làm mất contact dirty.
6. Lưu contact không làm mất content dirty.
7. Đóng form cảnh báo đúng nhóm chưa lưu.
8. Nút `Lưu thay đổi mẫu` chỉ bật khi content dirty.
9. Nút `Lưu cấu hình liên hệ` chỉ bật khi contact dirty.

## 13.5. Restore

1. Restore content không đổi contact.
2. Restore contact không đổi content.
3. Hai hộp xác nhận nói đúng phạm vi.
4. Preview reload đúng sau restore.
5. Có audit log.
6. Concurrency conflict không overwrite âm thầm.

---

# 14. Runtime verification

Chạy với:

```text
Smtp__Enabled=false
```

Kiểm tra tối thiểu:

1. `ACCOUNT_EMAIL_CONFIRMATION`
   - chỉ có một mô tả action;
   - không có form contact;
   - preview có nút “Xác nhận email”;
   - không có contact block.

2. `DEPT_PERSONNEL_ACCOUNT_ENABLED`
   - chỉ hiện action nếu runtime thật có action;
   - contact card theo capability thật.

3. `VISIT_PARTICIPANT_INVITATION`
   - action labels đúng;
   - contact form hiện;
   - preview contact thay đổi theo draft.

4. Sửa tên/mô tả
   - dirty state xuất hiện;
   - lưu thành công;
   - mở lại giữ đúng dữ liệu.

5. Restore
   - content và contact độc lập.

Không gửi SMTP thật.

---

# 15. Acceptance criteria

Chỉ coi task hoàn thành khi:

- Không còn action hint trùng lặp.
- Template không có action không hiện action.
- Template không hỗ trợ contact không hiện form contact.
- Không mở contact support cho toàn bộ catalog.
- Capability lấy từ backend, không hard-code frontend.
- `Mô tả quản trị` hiển thị đầy đủ.
- Nút lưu nói rõ phạm vi.
- Dirty state riêng cho content/contact.
- Restore content/contact tách biệt.
- Validator fail-closed.
- Targeted tests xanh.
- Typecheck/build xanh.
- Runtime verification đạt.
- SMTP tắt.
- Không push nếu chưa được yêu cầu.

---

# 16. Commit đề xuất

Tạo tối đa ba commit:

```text
fix(email): expose action and contact capabilities per template
fix(email-ui): align system block and contact settings behavior
fix(email-ui): improve template editor fields and save states
```

Không amend commit cũ.

Không AI trailer.

Không push.

---

# 17. Báo cáo cuối

## Root cause

```text
Action hint duplication:
Unsupported contact form:
Description truncation:
Ambiguous save behavior:
```

## Audit summary

```text
Templates with action:
Templates without action:
Templates supporting contact:
Templates not supporting contact:
Templates requiring contact:
```

## Before/after

```text
ACCOUNT_EMAIL_CONFIRMATION:
DEPT_PERSONNEL_ACCOUNT_ENABLED:
VISIT_PARTICIPANT_INVITATION:
```

## Tests

```text
Backend build:
Unit:
Integration:
Frontend typecheck:
Frontend build:
Frontend targeted:
Frontend full:
Runtime:
```

## Safety

```text
SMTP disabled:
Database not fresh-imported:
WIP hashes preserved:
Stash count preserved:
Commit SHA:
Not pushed:
```

Không báo DONE nếu chỉ ẩn lỗi bằng frontend nhưng backend capability/validator vẫn lệch.
