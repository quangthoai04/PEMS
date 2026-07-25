# PEMS — IMPLEMENTATION PROMPT
## Báo cáo kết quả nhập Excel, bộ chọn ngày giờ kiểu Google Calendar, validation độ dài và ô nhập tự mở rộng

## 0. Bối cảnh

Đây là phần triển khai tiếp theo sau khi các nhiệm vụ trước đã đạt gate:

```text
Backend build · Architecture · Unit: 0 lỗi · 14/14 · 1052/1052
Integration: 622/622
Frontend lint · unit · build: 0 lỗi · 554/554 · built
Real-stack E2E: 24/24
git diff --check: sạch
```

Không làm lại các phần đã hoàn thành: registrant identity/OTP, draft persistence qua lỗi OTP, contact capabilities, history actor name, UI xem đơn và toast đã sửa.

Phần mới gồm ba nhóm:

1. Hoàn thiện báo cáo kết quả khi nhập Excel danh sách khách/nhân sự hỗ trợ.
2. Thiết kế lại UI chọn ngày giờ theo cơ chế giống Google Calendar.
3. Chuẩn hóa giới hạn độ dài, thông báo vượt giới hạn và tự mở rộng ô nhập.

---

# 1. Vai trò và nguyên tắc

Bạn là Senior Full-stack Engineer phụ trách PEMS: React + TypeScript, React Hook Form + Zod, .NET + FluentValidation, MySQL/Pure V2 per-campus, accessibility, i18n VI/EN và kiểm thử end-to-end.

Mọi thay đổi phải đồng bộ:

```text
Database capacity
→ Backend DTO/validator
→ Frontend schema
→ UI control
→ Excel validator
→ Draft restore
→ Edit/resubmit
→ Test
```

Không được:

- Chỉ sửa frontend nhưng để backend rule lệch.
- Tự tăng giới hạn vượt sức chứa DB.
- Im lặng cắt dữ liệu khi vượt giới hạn.
- Hiển thị chỉ lỗi Excel đầu tiên.
- Để import âm thầm xóa dữ liệu đang nhập tay.
- Dùng thời gian UTC làm lệch Vietnam wall-clock.
- Tạo date/time contract mới nếu DTO hiện tại vẫn dùng được.
- Reset/rebase/force-push hoặc xóa WIP.

---

# 2. Preflight bắt buộc

```bash
git status
git branch --show-current
git rev-parse HEAD
git log -n 15 --oneline
git diff --check
```

Lưu ý: thay đổi mới nhất có thể còn trong local working tree và chưa có trên GitHub. Không tự ghi đè hoặc làm lại phần đã hoàn thành.

Audit tối thiểu:

```text
CampusVisitCard.tsx
Excel validator/download/template
visitRequestV2.schema.ts
Backend V2 validators
VisitRequestV2Modal / full-page create
Edit/resubmit page
Draft storage
Vietnam time utilities
FormField
AutoGrowTextarea
Person tables
All registerInfo/contactPoint/campusVisits fields
```

---

# PHẦN A — BÁO CÁO KẾT QUẢ NHẬP EXCEL

# 3. Audit hiện trạng

Xác minh bằng file/hàm/dòng:

```text
[ ] Import có chỉ hiện một message chung hay không.
[ ] File có nhiều lỗi nhưng UI chỉ lấy errors[0] hay không.
[ ] Khách và support có dùng chung một state message hay không.
[ ] Report có totalRows/errorRows/skippedDuplicates nhưng UI chưa dùng hay không.
[ ] Import đang replace hay append.
[ ] Duplicate có so với dữ liệu đang có trên form hay chỉ trong file.
[ ] Vượt 200 dòng được xử lý thế nào.
[ ] Workbook parse exception có được catch hay không.
```

# 4. Báo cáo import bắt buộc

Mỗi section có state riêng:

```text
Danh sách khách
Nhân sự hỗ trợ
```

Không dùng một message chung ở cuối campus card.

## 4.1 Loading

```text
Đang kiểm tra file danh-sach-khach.xlsx...
```

- Có spinner.
- Disable nút import của đúng section đang xử lý.
- Không khóa toàn form.
- Catch mọi lỗi đọc/parse.

## 4.2 Thành công

Panel xanh:

```text
Nhập Excel thành công
Tên file: danh-sach-khach.xlsx
Tổng số dòng dữ liệu: 25
Đã nhập: 23
Bỏ qua do trùng: 2
Danh sách hiện tại: 30/200 người
```

## 4.3 Có lỗi

Panel hoặc modal/drawer:

```text
Không thể nhập file
Tổng số dòng: 25
Dòng hợp lệ: 20
Dòng có lỗi: 5
Dòng trùng: 0
```

Bảng:

| Dòng | Cột | Nội dung lỗi |
|---:|---|---|
| 3 | Họ và tên | Không được để trống |
| 8 | Quốc tịch | Vượt quá 100 ký tự |
| 14 | Đơn vị công tác | Vượt quá 200 ký tự |

Action:

```text
[Tải báo cáo lỗi]
[Chọn file khác]
[Đóng]
```

## 4.4 Import một phần

Không tự thay đổi semantics hiện tại.

Mặc định an toàn:

```text
Nếu file có bất kỳ dòng lỗi nào → không thay đổi form.
```

Chỉ thêm nút `Nhập các dòng hợp lệ` khi đã được duyệt rõ. Không âm thầm bỏ dòng lỗi.

# 5. Nội dung report tải xuống

Report phải có:

```text
Tên file
Loại dữ liệu: Khách / Nhân sự hỗ trợ
Campus
Thời gian kiểm tra
Tổng số dòng
Số dòng hợp lệ
Số dòng lỗi
Số dòng trùng
Số dòng vượt giới hạn
Danh sách row/column/message
```

Ưu tiên `.xlsx`; có thể dùng CSV UTF-8 BOM nếu shared export infrastructure chưa hỗ trợ Excel writer.

# 6. Duplicate và giới hạn 200

Kiểm tra duplicate:

1. Trong file.
2. So với dữ liệu đang có trên form.
3. Sau normalize: trim, collapse whitespace, case-insensitive.
4. Khóa theo `fullName + jobTitle + organization + nationality`.

Không truyền `[]` làm existing data nếu form đã có người.

Nếu tổng sau import vượt 200:

- Không cắt âm thầm.
- Báo số chỗ còn lại.
- Mặc định không import.

# 7. Append/replace

Khuyến nghị:

```text
Nút “Nhập Excel” mặc định thêm vào danh sách hiện tại.
```

Không xóa dữ liệu nhập tay.

Nếu cần replace, tạo action riêng:

```text
[Thay thế toàn bộ danh sách bằng file]
```

và confirmation rõ ràng.

# 8. Validation trong Excel

Dùng cùng giới hạn với form/backend:

- Required.
- Maximum length.
- File type/size.
- Missing columns.
- Empty/header-only.
- Duplicate.
- Maximum members.
- Parse exception.

Mọi message qua i18n VI/EN.

---

# PHẦN B — BỘ CHỌN NGÀY GIỜ KIỂU GOOGLE CALENDAR

# 9. Mục tiêu UX

Không sao chép thương hiệu Google Calendar. Chỉ áp dụng cơ chế quen thuộc:

- Một ngày + giờ bắt đầu + giờ kết thúc.
- Dropdown giờ kết thúc có gợi ý thời lượng.
- Mặc định cùng ngày.
- Có chế độ kết thúc ngày khác.
- Validate realtime.
- Hỗ trợ keyboard.

# 10. Layout cùng ngày

```text
THỜI GIAN THAM QUAN
Ngày:       [31/07/2026]
Bắt đầu:    [08:00]
Kết thúc:   [09:00 ▼]       Thời lượng: 1 giờ

[ ] Kết thúc vào ngày khác
```

Desktop có thể hiển thị:

```text
[31/07/2026] [08:00] — [09:00] [1 giờ]
```

Mobile stack hợp lý.

# 11. Time dropdown

- Bước gợi ý 15 phút, trừ khi codebase đã có chuẩn khác.
- Cho nhập thủ công.
- Locale Việt Nam dùng `HH:mm`.
- Option end hiển thị duration:

```text
08:30 (30 phút)
08:45 (45 phút)
09:00 (1 giờ)
09:30 (1 giờ 30 phút)
10:00 (2 giờ)
```

- Keyboard navigation.
- Popup không bị modal overflow cắt.

# 12. Default end time

Khi start thay đổi và end chưa được user chủ động chỉnh hoặc đang không hợp lệ:

```text
end = start + 1 giờ
```

Không ghi đè một end hợp lệ mà user đã chọn.

Minimum duration vẫn là 30 phút.

# 13. Chế độ khác ngày

Khi bật:

```text
Bắt đầu
[31/07/2026] [22:00]

Kết thúc
[01/08/2026] [01:00]

Thời lượng: 3 giờ
```

Có thể dùng toggle:

```text
Cùng ngày | Khác ngày
```

Không thêm “Cả ngày” nếu business rule chưa có.

# 14. Date picker

- Locale `vi-VN`.
- Không cho chọn trước min advance.
- Create và edit/resubmit giữ đúng min advance riêng.
- Calendar popup không bị che bởi modal footer.
- Parse theo Vietnam wall-clock.
- Không shift UTC sai ngày.

# 15. Validation ngày giờ

Realtime và submit:

```text
Ngày/giờ bắt đầu bắt buộc.
Ngày/giờ kết thúc bắt buộc.
Bắt đầu phải đạt thời gian báo trước.
Kết thúc phải sau bắt đầu.
Thời lượng tối thiểu 30 phút.
Ngày giờ phải hợp lệ.
```

Nếu end trước start như screenshot, phải báo ngay:

```text
Thời gian kết thúc phải sau thời gian bắt đầu.
```

Không auto-swap mà không thông báo.

# 16. Contract và shared component

Không đổi API nếu không cần. UI có thể tách date/time nhưng map lại:

```text
startDatetime
endDatetime
```

Tạo shared component:

```text
VisitDateTimeRangePicker
```

Áp dụng cho:

- Public create.
- Authenticated create.
- Dashboard modal.
- Full-page create.
- Pending edit.
- Resubmit.
- Amendment nếu dùng cùng schedule model.

Draft cũ phải restore/migrate an toàn.

---

# PHẦN C — ĐỘ DÀI VÀ AUTO-GROW

# 17. Lập field matrix

Rà soát toàn bộ:

```text
registerInfo
contactPoint
campusVisits
visitors
supportTeam
operationalContact
additional requirements
transfer form
edit/resubmit
safe edit
amendment
Excel import
```

Lập bảng:

| Field | DB capacity | Backend max | Frontend max | Counter | Auto-grow | Kết luận |
|---|---:|---:|---:|---|---|---|

Không tăng frontend vượt DB/backend.

# 18. Giới hạn cần đối chiếu

Hiện cần kiểm tra các mức:

```text
Tên người: 150
Chức vụ: 150
Đơn vị: 200
Quốc tịch: 100
Email: 150
Tên đoàn: 200
Loại hình khác: 200
Mục đích: 2000
Nội dung làm việc: 4000
Ghi chú phương tiện: 2000
Ghi chú truyền thông: 2000
Ghi chú chung: 2000
```

Mọi `.max(...)` phải có message i18n:

```ts
.max(200, t('maxLength', { max: 200 }))
```

Không để `.max(200)` không message.

# 19. “Cho nhập khá nhiều”

Không tự tăng tùy ý.

Quy trình:

1. Kiểm tra DB type/capacity.
2. Kiểm tra FluentValidation.
3. Xác định nhu cầu nghiệp vụ.
4. Nếu DB đủ nhưng validator thấp, tăng backend/frontend cùng nhau.
5. Nếu DB không đủ, đề xuất migration riêng và đánh giá index/constraint.

Field mô tả dài có thể cho nhập nhiều hơn. Identity fields như email/phone/quốc tịch không cần hàng nghìn ký tự.

# 20. Counter và lỗi

Mọi field có max length:

- Counter cho field dài.
- Counter đỏ khi vượt.
- Message ngay dưới field.
- Không silent truncate.
- Paste dài phải báo lỗi.
- Backend giữ guard cuối.

Ví dụ:

```text
205/200
Nội dung vượt quá 200 ký tự.
```

# 21. Auto-grow theo loại field

## 21.1 Field mô tả dài

Dùng `AutoGrowTextarea` cho:

```text
Mục đích
Nội dung làm việc
Ghi chú phương tiện
Ghi chú truyền thông
Ghi chú cho FPTU
Lý do chuyển giao
Lý do amendment/reject/cancel
```

## 21.2 Field ngắn nhưng có thể wrap

Tạo `AutoGrowTextField` dạng textarea một dòng, tự tăng 2–4 dòng cho:

```text
Tên đoàn
Họ tên
Chức vụ
Đơn vị công tác
```

Không dùng cho:

```text
Email
Phone
Date
Time
Select
Country
```

## 21.3 Bảng khách/support

- `fullName`, `jobTitle`, `organization` mở rộng chiều cao row khi text dài.
- Không cắt text đã nhập.
- Không horizontal scroll toàn trang.
- Nationality vẫn select/combobox.
- Mobile giữ đủ dữ liệu.

---

# PHẦN D — TEST

# 22. Frontend tests — Excel

```text
1. Loading state.
2. Success report đúng total/imported/duplicate/current count.
3. Nhiều lỗi hiển thị đầy đủ.
4. Visitor/support có report state riêng.
5. Parse exception không crash.
6. Duplicate trong file.
7. Duplicate với form hiện tại.
8. Vượt 200 không cắt âm thầm.
9. File lỗi không thay đổi form.
10. Import mặc định không xóa dữ liệu nhập tay.
11. Download error report.
12. VI/EN.
```

# 23. Frontend tests — Date/time

```text
1. Mặc định cùng ngày.
2. Start auto-gợi ý end +1 giờ khi end untouched.
3. End dropdown có duration.
4. End trước start báo lỗi realtime.
5. 29m59s fail, 30m pass.
6. Bật khác ngày.
7. Multi-day duration đúng.
8. Create/edit dùng min advance khác nhau.
9. Vietnam wall-clock không shift.
10. Keyboard navigation.
11. Popup không bị clipping.
12. Draft restore giữ range.
```

# 24. Frontend tests — Length/auto-grow

```text
1. Mọi max rule có message.
2. Paste vượt limit báo lỗi.
3. Counter đỏ khi vượt.
4. Auto-grow khi nhập/restore/copy campus.
5. Table row mở rộng với text dài.
6. Email/phone vẫn single-line.
7. Excel length validation khớp form.
```

# 25. Backend/integration tests

```text
1. Length boundary từng field.
2. Purpose 2000 boundary.
3. Working content 4000 boundary.
4. Notes 2000 boundary.
5. Person fields boundary.
6. End <= start bị reject.
7. Minimum 30 phút.
8. Min advance create/edit.
9. Multi-day persist/read đúng.
10. Không timezone drift.
```

# 26. Real-stack E2E

## Excel success

```text
Import file hợp lệ
→ report đúng
→ table có dữ liệu
→ submit
→ backend lưu đúng
```

## Excel error

```text
Import file nhiều lỗi
→ report có bảng lỗi
→ tải report
→ form không đổi
```

## Same-day

```text
Ngày 31/07
→ 08:00–09:00
→ duration 1 giờ
→ submit/read đúng
```

## Multi-day

```text
31/07 22:00
→ 01/08 01:00
→ duration 3 giờ
→ submit/read đúng
```

## Length

```text
Paste vượt limit
→ counter đỏ + lỗi
→ submit bị chặn
→ sửa hợp lệ
→ submit thành công
```

---

# PHẦN E — GATE VÀ COMMIT

# 27. Gate

```bash
dotnet build
dotnet test tests/PEMS.ArchitectureTests
dotnet test tests/PEMS.UnitTests
dotnet test tests/PEMS.IntegrationTests
npm run lint
npm run test
npm run build
git diff --check
```

Real-stack dùng disposable DB.

Nếu `mysql` không có trong PATH:

```bash
MYSQL_BIN="C:/Program Files/MySQL/MySQL Server 8.0/bin/mysql.exe"
```

# 28. Commit đề xuất

```text
feat(visit-ui): add detailed excel import result reporting
feat(visit-ui): add calendar-style visit date time range picker
fix(visit-validation): align field length limits and messages
refactor(visit-ui): auto-grow long text fields and person rows
test(visit): cover excel schedule and long-text journeys
docs(visit): document import schedule and validation behavior
```

Không push trước khi gate xanh.

# 29. Definition of Done

```text
[ ] Excel có loading và report riêng cho khách/support.
[ ] Report có total/imported/error/duplicate.
[ ] Có bảng row/column/message và tải report lỗi.
[ ] Không chỉ hiện lỗi đầu tiên.
[ ] Import không âm thầm xóa dữ liệu tay.
[ ] Vượt 200 không bị cắt âm thầm.
[ ] Time picker mặc định cùng ngày.
[ ] End dropdown có duration.
[ ] Có chế độ khác ngày.
[ ] End trước start báo ngay.
[ ] Min duration/min advance đúng.
[ ] Không timezone drift.
[ ] Mọi max rule có message.
[ ] Field dài có counter và auto-grow.
[ ] Tên/chức vụ/đơn vị trong table không bị cắt.
[ ] Backend/frontend/Excel limits đồng bộ.
[ ] Draft create/edit/resubmit không hỏng.
[ ] Unit/integration/E2E xanh.
[ ] Build và diff-check sạch.
```

# 30. Báo cáo cuối

```text
1. Branch và HEAD trước/sau.
2. WIP/commit hiện có.
3. Files changed.
4. Excel semantics: append/replace/partial/fail-whole.
5. Report fields và định dạng file.
6. Date/time component và timezone behavior.
7. Same-day/multi-day rules.
8. Field length matrix DB/backend/frontend.
9. Field được tăng giới hạn, nếu có.
10. Auto-grow coverage.
11. Tests/gates/E2E.
12. Database migration impact.
13. Known limitations.
14. Resume point.
```

Mọi kết luận phải có evidence từ code, test hoặc runtime.
