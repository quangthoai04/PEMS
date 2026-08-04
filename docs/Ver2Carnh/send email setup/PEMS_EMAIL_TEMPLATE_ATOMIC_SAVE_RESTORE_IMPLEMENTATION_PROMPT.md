# PEMS — PROMPT GỘP LƯU VÀ KHÔI PHỤC MẪU EMAIL ATOMIC

## Vai trò

Bạn là Senior Full-stack Engineer phụ trách module **Quản lý mẫu email** của PEMS.

Phải kiểm tra source code hiện tại trước khi sửa. Không giả định tên file, route, DTO hoặc transaction nếu code thực tế khác tài liệu này.

---

## 1. Mục tiêu

Cập nhật màn hình:

```text
Quản lý email
→ Cấu hình mẫu email
→ Chỉnh sửa mẫu
```

để người quản trị chỉ cần:

```text
1 lần lưu
1 lần khôi phục mặc định
1 dirty state tổng
```

cho toàn bộ dữ liệu của một mẫu:

- Thông tin chung.
- Subject/body tiếng Việt.
- Subject/body tiếng Anh.
- Cấu hình thông tin liên hệ.
- Reply-To.

Lưu và khôi phục phải **atomic**:

```text
Tất cả thành công
hoặc
Không phần nào được thay đổi
```

Không được chỉ gộp nút ở frontend rồi gọi tuần tự hai API độc lập.

---

## 2. Hiện trạng cần loại bỏ

UI hiện có:

```text
Lưu cấu hình liên hệ
Lưu thay đổi mẫu
Phục hồi nội dung mẫu
Phục hồi về cấu hình mặc định của mẫu
```

Vấn đề:

1. Người dùng phải nhớ lưu hai lần.
2. Có thể lưu nội dung thành công nhưng contact settings thất bại.
3. Có thể khôi phục một phần.
4. Validation giữa body và `{{contactInformationBlock}}` bị chia cắt.
5. Dirty state và cảnh báo đóng khó hiểu.
6. Revision/concurrency có thể tăng riêng từng nhóm.

---

## 3. Quyết định bắt buộc

### 3.1 Một nút lưu

Footer chỉ giữ:

```text
[Khôi phục toàn bộ mặc định]        ● Có thay đổi chưa lưu        [Hủy] [Lưu thay đổi]
```

Bỏ nút lưu riêng trong card thông tin liên hệ.

`Lưu thay đổi` lưu toàn bộ snapshot đang chỉnh sửa.

### 3.2 Một thao tác khôi phục

Bỏ:

```text
Phục hồi nội dung mẫu
Phục hồi cấu hình liên hệ riêng
```

Thay bằng:

```text
Khôi phục toàn bộ mặc định
```

### 3.3 Một dirty state tổng

```ts
const isDirty =
  isGeneralDirty ||
  isContentDirty ||
  isContactSettingsDirty;
```

Không báo dirty khi vừa mở editor, hydrate dữ liệu hoặc đổi tab VI/EN.

### 3.4 Không đổi schema

- Không tạo bảng mới.
- Không thay database schema.
- Nếu dữ liệu nằm ở nhiều bảng, cập nhật trong cùng transaction.

---

## 4. Audit trước khi code

Xác định chính xác:

```text
Frontend page/component hiện tại
State nội dung mẫu
State contact settings
API lưu nội dung
API lưu contact settings
API restore nội dung
API restore contact settings
Revision/concurrency token
Backend validator
Capability contract
Transaction boundary
```

Báo ngắn gọn file nào, API nào và dữ liệu nào đang được lưu riêng.

---

## 5. Backend — API lưu tổng hợp

Ưu tiên mở rộng endpoint update template hiện có, ví dụ:

```http
PUT /api/email-templates/{templateCode}
```

Payload tổng hợp, bám theo DTO hiện tại:

```json
{
  "revision": 3,
  "name": "Thay đổi vai trò tài khoản",
  "description": "Gửi khi vai trò tài khoản được thay đổi",
  "content": {
    "vi": { "subject": "...", "body": "..." },
    "en": { "subject": "...", "body": "..." }
  },
  "contactSettings": {
    "requirement": "REQUIRED",
    "source": "SYSTEM_ADMINISTRATION",
    "showWorkEmail": true,
    "showPhone": true,
    "showDepartment": false,
    "showCampus": false,
    "showSentBy": false,
    "headingVi": "Thông tin liên hệ",
    "headingEn": "Contact information",
    "replyToMode": "RESOLVED_CONTACT"
  }
}
```

Tên field phải theo code hiện tại. Không đổi public contract ngoài mức cần thiết.

### Transaction bắt buộc

```text
Load template + saved contact policy
→ Check revision
→ Resolve capability
→ Validate general information
→ Validate VI/EN
→ Validate system blocks
→ Validate contact settings
→ Validate quan hệ body/contact
→ Update content
→ Update contact settings
→ Increase revision một lần
→ Save một lần
→ Commit
```

Nếu lỗi:

```text
Rollback toàn bộ
Không tăng revision
Không lưu một phần
```

Không gọi hai command mỗi command tự commit.

---

## 6. Capability rules

### 6.1 Template không hỗ trợ contact

Khi:

```text
contactSupported = false
```

Backend phải:

- Cho phép `contactSettings = null` hoặc không có field.
- Không tạo/update contact configuration.
- Từ chối nếu body chứa `{{contactInformationBlock}}`.
- Không đổi capability để hợp thức hóa UI.

Frontend không render:

- Mức hiển thị.
- Nguồn thông tin liên hệ.
- Checkbox field.
- Heading VI/EN.
- Reply-To.
- Nút lưu/restore riêng.

Chỉ hiện:

```text
Mẫu này không sử dụng khối thông tin liên hệ.
Không có cấu hình cần chỉnh sửa.
```

### 6.2 Template hỗ trợ, không bắt buộc

Cho phép `NONE`, `OPTIONAL`, `REQUIRED` nếu policy hiện tại hỗ trợ.

### 6.3 Template bắt buộc contact

Khi:

```text
contactSupported = true
contactRequired = true
```

Validate cùng request:

```text
Body VI có {{contactInformationBlock}}
Body EN có {{contactInformationBlock}}
Requirement không phải NONE
Nguồn contact hợp lệ
Có ít nhất một trường liên hệ được bật
Heading VI/EN hợp lệ
Reply-To hợp lệ
```

Nếu lỗi, reject toàn bộ request.

---

## 7. Concurrency

Dùng revision/token hiện tại.

Một lần lưu tổng hợp chỉ tăng revision **một lần**.

Nếu revision stale:

```text
Trả conflict theo convention hiện tại
Không lưu phần nào
```

---

## 8. Backend — khôi phục mặc định tổng hợp

Tạo hoặc mở rộng endpoint:

```http
POST /api/email-templates/{templateCode}/restore-defaults
```

Trong một transaction:

```text
Load shipped default
→ Check revision
→ Restore name/description nếu thuộc phạm vi restore
→ Restore subject/body VI
→ Restore subject/body EN
→ Restore contact settings nếu supported
→ Restore Reply-To
→ Increase revision một lần
→ Save một lần
→ Commit
```

Template unsupported:

- Không tạo contact settings giả.
- Không báo `CONTACT_NOT_SUPPORTED`.
- Chỉ restore phần template thực sự hỗ trợ.

Nếu lỗi ở bất kỳ bước nào, rollback toàn bộ.

Response phải trả snapshot đầy đủ sau restore để frontend dùng làm baseline mới.

---

## 9. Tương thích API cũ

Kiểm tra mọi consumer của endpoint cũ.

Nếu chỉ màn editor dùng:

```text
Chuyển editor sang API tổng hợp
Xóa hoặc deprecate API cũ nếu an toàn
```

Nếu còn consumer khác:

```text
Giữ tạm
Không dùng trong editor mới
Ghi rõ consumer và lý do
```

Không phá flow ngoài scope.

---

## 10. Frontend — form và baseline tổng

Dùng một snapshot tổng hợp tương đương:

```ts
type EmailTemplateEditorForm = {
  revision: number;
  name: string;
  description: string;
  contentVi: { subject: string; body: string };
  contentEn: { subject: string; body: string };
  contactSettings: ContactSettingsForm | null;
};
```

Sau GET:

```text
Normalize response
→ Set form
→ Set baseline
→ isDirty = false
```

Dirty:

```ts
const isDirty = !deepEqual(
  normalizeEditorForm(currentForm),
  baselineRef.current
);
```

Normalize tối thiểu:

- `null`/`undefined`.
- Line ending.
- Trailing spaces không có ý nghĩa.
- HTML editor tương đương.
- Boolean/default field.
- `contactSettings = null` với unsupported template.

Không dùng timeout để tắt dirty.

Sau save/restore thành công:

```text
Set form = response snapshot
Set baseline = response snapshot
Update revision
isDirty = false
```

---

## 11. Frontend — UI

### Card contact

Bỏ:

```text
Lưu cấu hình liên hệ
Phục hồi về cấu hình mặc định của mẫu
```

Card chỉ chứa field.

### Footer

```text
[Khôi phục toàn bộ mặc định]        ● Có thay đổi chưa lưu        [Hủy] [Lưu thay đổi]
```

Yêu cầu:

- Nút cùng chiều cao.
- Không xuống dòng chữ.
- Footer không che nội dung.
- Responsive vẫn rõ nhóm hành động.

### Trạng thái nút

`Lưu thay đổi` disabled khi:

- Chưa hydrate.
- Không dirty.
- Đang lưu.
- Có lỗi validation client rõ ràng.

`Khôi phục toàn bộ mặc định` disabled khi đang save/restore.

Không disable restore chỉ vì form chưa dirty.

---

## 12. Xác nhận khôi phục

Modal:

```text
Khôi phục toàn bộ mẫu?

Thao tác này sẽ khôi phục:
• Tên và mô tả mẫu
• Tiêu đề và nội dung tiếng Việt
• Tiêu đề và nội dung tiếng Anh
• Cấu hình thông tin liên hệ và Reply-To nếu mẫu hỗ trợ

Các tùy chỉnh hiện tại sẽ bị thay thế.
```

Nút:

```text
[Hủy] [Khôi phục mặc định]
```

---

## 13. Cảnh báo đóng

Nếu `isDirty = true`, chặn:

- Nút X.
- Nút Hủy.
- Chuyển template.
- Chuyển route.
- Reload/đóng tab nếu project đã có pattern.

Thông báo:

```text
Bạn có thay đổi chưa lưu. Rời khỏi trang sẽ làm mất các thay đổi này.
```

Không hỏi khi `isDirty = false`.

---

## 14. Error mapping

Xử lý rõ:

```text
Revision conflict
Unsupported system block
Contact not supported
Required contact block missing
No visible contact field selected
Invalid contact source
Invalid Reply-To
Invalid subject/body
Restore default unavailable
```

Không nối raw error code vào câu UI. Message và action phải tách riêng.

---

## 15. Test bắt buộc

### Backend unit

- Lưu content + contact thành công atomic.
- Content hợp lệ, contact lỗi → content không đổi.
- Contact hợp lệ, content lỗi → contact không đổi.
- Revision tăng một lần.
- Revision stale → không lưu gì.
- Unsupported + `contactSettings = null` → lưu content thành công.
- Unsupported + contact block → reject toàn bộ.
- Required thiếu block VI/EN → reject toàn bộ.
- Required không chọn field → reject toàn bộ.
- Restore tổng hợp thành công.
- Restore lỗi giữa chừng → rollback.

### Integration

- GET → sửa content/contact → PUT → GET lại đồng nhất.
- PUT lỗi contact → content cũ còn nguyên.
- Restore → GET lại đúng shipped defaults.
- Unsupported restore không tạo contact config.
- Hai editor cạnh tranh revision.
- Không gửi email thật.

### Frontend

- Mở editor không dirty.
- Đổi tab không dirty.
- Sửa content/contact → một dirty state tổng.
- Hoàn tác về baseline → hết dirty.
- Chỉ còn một nút save.
- Chỉ còn một nút restore.
- Save gọi đúng một API tổng hợp.
- Restore gọi đúng một API tổng hợp.
- Save lỗi không reset baseline.
- Save/restore thành công reset baseline.
- Unsupported không render contact form.
- Required không cho chọn NONE.
- Leave guard chỉ hiện khi dirty.

### Runtime smoke

Chạy với:

```text
Smtp__Enabled=false
```

Kiểm tra:

```text
ACCOUNT_EMAIL_CONFIRMATION
ACCOUNT_ROLE_CHANGED
ACCOUNT_PENDING_EMAIL_CHANGED_OLD_NOTICE
VISIT_PARTICIPANT_INVITATION
```

Xác nhận không partial save, preview đúng và reload không dirty.

---

## 16. Safety

Không được:

- Fresh-import database thật.
- Gửi email thật.
- Tạo bảng mới.
- Đổi schema.
- Bật SMTP.
- Mở contact support đại trà.
- Refactor toàn module.
- Làm mất WIP/stash.
- Push khi chưa được yêu cầu.

---

## 17. Thứ tự triển khai

1. Preflight branch/WIP/baseline.
2. Audit API/state/transaction.
3. Thiết kế request/response tổng hợp tối thiểu.
4. Implement backend atomic update.
5. Implement backend atomic restore.
6. Đồng bộ validator/concurrency.
7. Chuyển frontend sang snapshot tổng.
8. Xóa save/restore riêng trong contact card.
9. Thêm footer một save + một restore.
10. Thêm leave guard.
11. Chạy unit/integration/frontend tests.
12. Runtime smoke với SMTP tắt.
13. Báo cáo evidence và commit.

---

## 18. Definition of Done

```text
[ ] Chỉ có một nút Lưu thay đổi.
[ ] Chỉ có một nút Khôi phục toàn bộ mặc định.
[ ] Không còn save/restore riêng trong contact card.
[ ] Save content + contact là một transaction.
[ ] Restore content + contact là một transaction.
[ ] Lỗi một phần không tạo partial update.
[ ] Revision chỉ tăng một lần.
[ ] Mở editor không dirty.
[ ] Dirty reset đúng sau save/restore.
[ ] Unsupported không render contact form.
[ ] Required được validate cùng body VI/EN.
[ ] Editor không gọi API cũ.
[ ] Backend build xanh.
[ ] Frontend typecheck/build xanh.
[ ] Targeted tests xanh.
[ ] Runtime smoke đạt với outbound email tắt.
[ ] Không đổi schema và không fresh-import DB thật.
```

---

## 19. Báo cáo cuối

```text
Root cause
Files changed
API before/after
Transaction boundary
Validation rules
UI before/after
Tests
Runtime evidence
Safety
Commits
Not pushed / pushed
Remaining debt
```
