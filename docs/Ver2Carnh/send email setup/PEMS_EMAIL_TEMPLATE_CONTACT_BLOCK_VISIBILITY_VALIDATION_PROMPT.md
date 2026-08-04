# PEMS — PROMPT BỔ SUNG VALIDATION GIỮA MỨC HIỂN THỊ VÀ `{{contactInformationBlock}}`

## Vai trò

Bạn là Senior Full-stack Engineer phụ trách module **Quản lý mẫu email** của PEMS.

Hãy kiểm tra source code hiện tại trước khi sửa. Không giả định tên file, route, DTO hoặc validator nếu code thực tế khác tài liệu này.

Prompt này là phần bổ sung cho kế hoạch **atomic save/restore** trước đó.

---

## 1. Mục tiêu

Xử lý dứt điểm trạng thái cấu hình mâu thuẫn:

```text
Mức hiển thị = Không hiển thị
nhưng Body VI hoặc Body EN vẫn chứa:
{{contactInformationBlock}}
```

Hệ thống phải chặn lưu ngay từ màn hình chỉnh sửa và backend phải fail-closed.

Không được để runtime gửi email còn placeholder chưa được thay thế.

---

## 2. Quy tắc nghiệp vụ

Áp dụng ma trận sau:

| Mức hiển thị | Block trong VI/EN | Kết quả |
|---|---:|---|
| `NONE` / Không hiển thị | Không có | Hợp lệ |
| `NONE` / Không hiển thị | Có ở VI hoặc EN | Không hợp lệ, chặn lưu |
| `OPTIONAL` / Tùy chọn | Không có | Hợp lệ |
| `OPTIONAL` / Tùy chọn | Có | Hợp lệ |
| `REQUIRED` / Bắt buộc | Thiếu ở VI hoặc EN | Không hợp lệ, chặn lưu |
| `REQUIRED` / Bắt buộc | Có ở cả VI và EN | Hợp lệ |

Không tự suy diễn theo tên template. Dựa trên capability và policy thật từ backend.

---

## 3. Frontend — validation tức thời

Khi:

```text
contactRequirement = NONE
```

và một trong hai body chứa:

```text
{{contactInformationBlock}}
```

phải hiển thị lỗi cạnh editor:

```text
Khối thông tin liên hệ vẫn tồn tại trong nội dung,
nhưng mức hiển thị đang là “Không hiển thị”.

Hãy xóa khối khỏi nội dung hoặc chọn lại “Tùy chọn/Bắt buộc”.
```

Kèm action:

```text
[Xóa khối khỏi nội dung]
```

Nút `Lưu thay đổi` phải disabled cho tới khi hết mâu thuẫn.

Không nối raw error code vào message UI.

---

## 4. Khi người dùng đổi sang “Không hiển thị”

Nếu Body VI hoặc Body EN đang có block, không được âm thầm xóa.

Hiển thị modal xác nhận:

```text
Nội dung hiện có khối {{contactInformationBlock}}.

Chuyển sang “Không hiển thị” yêu cầu xóa khối này
khỏi cả nội dung tiếng Việt và tiếng Anh.
```

Nút:

```text
[Giữ cấu hình hiện tại]
[Xóa khối và chuyển sang Không hiển thị]
```

### Hành vi

Nếu chọn `Giữ cấu hình hiện tại`:

```text
Không đổi requirement
Không sửa body
Đóng modal
```

Nếu chọn `Xóa khối và chuyển sang Không hiển thị`:

```text
Xóa mọi occurrence hợp lệ của {{contactInformationBlock}} khỏi Body VI
Xóa mọi occurrence hợp lệ của {{contactInformationBlock}} khỏi Body EN
Set requirement = NONE
Đánh dấu form dirty
Không tự lưu
```

Không xóa text lân cận ngoài phạm vi block.

Không gọi API ngay khi chọn radio.

---

## 5. Nút “Xóa khối khỏi nội dung”

Action này phải:

```text
Xóa block trong cả VI và EN
Giữ nguyên requirement hiện tại
Đánh dấu dirty
Cập nhật validation ngay
```

Nếu requirement đang là `REQUIRED`, sau khi xóa block phải chuyển sang lỗi:

```text
Mẫu bắt buộc thông tin liên hệ nhưng nội dung đang thiếu block.
```

Không tự đổi `REQUIRED` thành `NONE`.

---

## 6. Normalize và detect block

Tạo helper dùng chung, không lặp regex rải rác:

```ts
containsContactInformationBlock(body: string): boolean
removeContactInformationBlock(body: string): string
```

Yêu cầu:

- Nhận diện đúng literal `{{contactInformationBlock}}`.
- Không match các biến có tên gần giống.
- Xử lý nhiều occurrence.
- Sau khi xóa không tạo quá nhiều dòng trống.
- Không phá HTML editor output.
- Dùng chung cho VI và EN.

Nếu backend đã có parser system block chuẩn, frontend phải bám theo cùng literal/canonical token.

---

## 7. Backend — validation atomic

Trong API lưu tổng hợp, validate trước khi mutation:

```text
requirement = NONE
AND (bodyVi chứa block OR bodyEn chứa block)
→ Reject toàn bộ request
```

Mã lỗi đề xuất:

```text
EMAIL_TEMPLATE_CONTACT_BLOCK_NOT_ALLOWED_WHEN_HIDDEN
```

Message:

```text
Không thể lưu mẫu vì mức hiển thị là “Không hiển thị”
nhưng nội dung vẫn chứa khối thông tin liên hệ.
```

Khi lỗi:

```text
Không lưu content
Không lưu contact settings
Không tăng revision
Không ghi partial audit
Rollback transaction
```

Không chỉ dựa vào frontend.

---

## 8. Backend — rule REQUIRED

Trong cùng validator:

```text
requirement = REQUIRED
AND bodyVi thiếu block
→ Reject

requirement = REQUIRED
AND bodyEn thiếu block
→ Reject
```

Mã lỗi có thể dùng error hiện tại nếu đã tồn tại. Không tạo duplicate code không cần thiết.

Message phải chỉ rõ ngôn ngữ đang thiếu:

```text
Nội dung tiếng Việt thiếu khối thông tin liên hệ.
Nội dung tiếng Anh thiếu khối thông tin liên hệ.
```

---

## 9. Template không hỗ trợ contact

Nếu:

```text
contactSupported = false
```

thì:

- Không render contact form.
- Body VI/EN không được chứa block.
- Nếu dữ liệu cũ còn block, hiển thị cảnh báo và action xóa.
- Backend từ chối lưu nếu block còn tồn tại.
- Restore mặc định phải trả body không chứa block.
- Không dùng rule `NONE` để giả lập unsupported.

`UNSUPPORTED` và `NONE` là hai trạng thái khác nhau:

```text
UNSUPPORTED = template không có capability contact
NONE = template có capability nhưng người quản trị chọn không hiển thị
```

---

## 10. Preview

Preview phải:

- Không render contact block khi requirement = `NONE`.
- Hiển thị trạng thái validation lỗi nếu body vẫn có block.
- Không coi việc preview tự bỏ block là lý do cho phép lưu.
- Không để placeholder literal xuất hiện trong preview cuối cùng.

---

## 11. Runtime renderer guard

Renderer phải có guard cuối:

```text
Không được gửi email nếu sau render vẫn còn
{{contactInformationBlock}}
```

Nếu trạng thái invalid lọt qua:

```text
Fail send với error rõ ràng
Không gửi nội dung chứa placeholder
Không silently replace bằng chuỗi rỗng nếu điều đó che lỗi cấu hình
```

Ngoại lệ duy nhất là flow preview có chủ đích và đã được đánh dấu sample data; không áp dụng cho send thật.

---

## 12. Dirty state

Các thao tác sau phải tạo dirty:

```text
Chọn NONE
Xóa block
Đổi OPTIONAL/REQUIRED
Undo/redo nội dung
```

Nếu người dùng hoàn tác chính xác về baseline:

```text
isDirty = false
```

Mở editor hoặc đổi tab VI/EN không tạo dirty.

---

## 13. Error UX

Không hiển thị dạng:

```text
...EMAIL_TEMPLATE_SYSTEM_BLOCK_NOT_ALLOWEDXóa khối không hợp lệ
```

Phải tách:

```text
[Icon lỗi] Message
[Button action]
```

Ví dụ:

```text
Khối thông tin liên hệ không được phép khi mức hiển thị là “Không hiển thị”.

[Xóa khối khỏi nội dung]
```

---

## 14. Test bắt buộc

### Frontend

1. `NONE` + không có block → hợp lệ.
2. `NONE` + block ở VI → lỗi, disable save.
3. `NONE` + block ở EN → lỗi, disable save.
4. `NONE` + block ở cả hai → lỗi.
5. Đổi từ `OPTIONAL` sang `NONE` khi có block → hiện modal.
6. Chọn giữ cấu hình → không đổi state.
7. Chọn xóa block → xóa ở VI/EN, set `NONE`, dirty = true.
8. Action xóa block không tự save.
9. `REQUIRED` thiếu VI → lỗi.
10. `REQUIRED` thiếu EN → lỗi.
11. `REQUIRED` đủ hai ngôn ngữ → hợp lệ.
12. Hoàn tác về baseline → hết dirty.
13. Unsupported template không render form nhưng vẫn cảnh báo stale block.

### Backend unit

1. `NONE` + block VI → reject atomic.
2. `NONE` + block EN → reject atomic.
3. `NONE` + block cả hai → reject atomic.
4. `NONE` + không block → pass.
5. `OPTIONAL` + không block → pass.
6. `OPTIONAL` + block → pass.
7. `REQUIRED` thiếu VI → reject.
8. `REQUIRED` thiếu EN → reject.
9. `REQUIRED` đủ VI/EN → pass.
10. Reject không tăng revision.
11. Reject không lưu content/contact.
12. Unsupported + block → reject.

### Integration

1. GET template → set NONE nhưng giữ block → PUT trả validation error.
2. GET lại → dữ liệu cũ nguyên vẹn.
3. Xóa block + set NONE → PUT thành công.
4. Save thành công → reload không dirty.
5. Preview không còn placeholder.
6. Runtime send không gửi placeholder.

---

## 15. Safety

Không được:

- Tạo bảng mới.
- Đổi schema.
- Fresh-import database thật.
- Gửi email thật khi test.
- Mở contact capability cho template unsupported.
- Tự xóa block khi người dùng chưa xác nhận.
- Lưu một phần.
- Push khi chưa được yêu cầu.

Chạy runtime smoke với:

```text
Smtp__Enabled=false
```

---

## 16. Thứ tự triển khai

1. Audit helper/parser hiện tại.
2. Thêm helper detect/remove block dùng chung.
3. Thêm validation frontend.
4. Thêm modal khi đổi sang NONE.
5. Disable save khi invalid.
6. Bổ sung backend atomic validator.
7. Bổ sung preview guard.
8. Bổ sung runtime renderer guard.
9. Viết frontend/backend/integration tests.
10. Runtime smoke với SMTP tắt.
11. Báo cáo evidence.

---

## 17. Definition of Done

```text
[ ] NONE + block bị chặn ở frontend.
[ ] NONE + block bị chặn ở backend.
[ ] Đổi sang NONE khi có block phải xác nhận.
[ ] Không âm thầm xóa nội dung.
[ ] Action xóa block xử lý cả VI và EN.
[ ] REQUIRED thiếu block bị chặn đúng ngôn ngữ.
[ ] Unsupported không dùng NONE để giả lập.
[ ] Save invalid không tăng revision.
[ ] Save invalid không tạo partial update.
[ ] Preview không để placeholder literal.
[ ] Runtime không gửi placeholder literal.
[ ] Dirty state đúng.
[ ] Tests xanh.
[ ] Không đổi schema và không gửi email thật.
```

---

## 18. Báo cáo cuối

```text
Root cause
Files changed
Validation matrix
Frontend behavior
Backend atomic behavior
Preview/runtime guard
Tests
Runtime evidence
Safety
Commits
Not pushed / pushed
Remaining debt
```
