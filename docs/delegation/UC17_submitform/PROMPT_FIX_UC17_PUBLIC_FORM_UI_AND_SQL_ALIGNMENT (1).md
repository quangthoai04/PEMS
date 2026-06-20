# PROMPT_FIX_UC17_PUBLIC_FORM_UI_AND_SQL_ALIGNMENT

## Mục tiêu

Fix tối ưu giao diện public form **Đăng ký tham quan trường / UC-17 Submit Visit Request** để:

```text
1. UI đẹp, gọn, không bị đè icon, không lệch layout khi validation error.
2. Form field và payload khớp với SQL full mới nhất `pems_full(3).sql`.
3. Không giữ các trường frontend không có trong database/backend DTO.
4. Không sửa backend flow UC-17 đã ổn nếu không cần.
5. Không thêm cột vào SQL chỉ để chiều frontend.
```

Bối cảnh hiện tại:

```text
- UC-17 backend đã đồng bộ theo SQL v8.3.
- Real flow đang dùng:
  POST /api/visit-requests/initiate
  POST /api/visit-requests/verify
  POST /api/visit-requests/resend-otp

- Frontend public form đang có một số lỗi UI:
  + Field Email người đăng ký chưa có note rõ đây là email dùng để nhận mã OTP.
  + Dropdown/select bị đè icon: vừa có chevron/clear, vừa có check xanh valid.
  + Date/time error message làm lệch hàng input thời gian.
  + Bảng khách đang có trường "Số HC/CMND" nhưng SQL `visit_guest_members` không có cột này.
```

---

## 0. Nguyên tắc bắt buộc

### Không được làm

```text
- Không sửa lại backend UC-17 flow nếu không cần.
- Không đổi route frontend đang dùng.
- Không thêm cột CCCD/CMND/passport vào SQL.
- Không giữ field identity/CCCD/CMND ẩn trong form state rồi vẫn gửi lên backend.
- Không làm dropdown vừa có chevron vừa có check xanh đè nhau.
- Không để validation message làm nhảy/lệch layout.
- Không sửa lung tung toàn bộ design system nếu chỉ cần sửa form/component liên quan.
```

### Được làm

```text
- Refactor component field UI nếu đang dùng chung sai.
- Thêm prop để tắt valid icon cho Select/Combobox/DateTime.
- Chuẩn hóa helper text / error slot.
- Xóa field "Số HC/CMND" khỏi guest table, form state, validation, payload, Excel template nếu có.
- Cập nhật type/interface để khớp SQL/backend DTO.
- Cập nhật docs/changelog nếu project có quy định.
```

---

# 1. Source of truth: SQL full mới nhất

Dùng file SQL mới nhất làm nguồn sự thật:

```text
database/scripts/pems_full(3).sql
```

Hoặc file SQL full chuẩn nhất hiện có trong repo.

Cần kiểm tra bảng:

```sql
visit_guest_members
visit_requests
visit_request_campuses
otp_tokens
users
campuses
```

## 1.1 Rule riêng cho `visit_guest_members`

Theo SQL mới nhất, `visit_guest_members` chỉ nên dùng các field nghiệp vụ kiểu:

```text
guest_member_id
visit_request_id
full_name
organization
job_title
nationality
email
phone
is_representative
note
created_at
created_by
updated_at
updated_by
```

Không được yêu cầu frontend nhập các field không có trong SQL:

```text
Số HC/CMND
CCCD
CMND
Passport No
identityNumber
citizenId
passportNo
idNumber
identity
documentNumber
```

Nếu backend DTO hiện tại cũng không có field này, phải xóa hoàn toàn khỏi frontend.

Nếu backend DTO còn field này nhưng SQL không có, phải báo mismatch và đề xuất xóa khỏi DTO/mapping, không thêm vào SQL.

---

# 2. Vấn đề UI cần fix

## 2.1 Email người đăng ký cần note OTP

### Vấn đề

Field **Email** ở step 1 là email dùng để xác thực OTP trước khi gửi form, nhưng UI chưa giải thích rõ. Người dùng có thể nhập email không truy cập được.

### Yêu cầu

Thêm note/helper text dưới label hoặc dưới input Email:

```text
Email này sẽ được dùng để nhận mã xác thực OTP trước khi gửi form đăng ký.
Vui lòng nhập email bạn có thể truy cập.
```

Có thể dùng bản ngắn:

```text
Email này dùng để nhận mã OTP xác thực trước khi gửi form.
```

### UI spec

```text
- Helper text nhỏ, màu slate/gray.
- Không dùng màu đỏ/cam như warning.
- Không làm tăng chiều cao quá nhiều.
- Khi email có lỗi, helper text và error text không chồng nhau.
- Error text vẫn nằm dưới input và ưu tiên rõ hơn helper text.
```

### Gợi ý implement

```tsx
<FormField
  label="Email"
  required
  helperText="Email này dùng để nhận mã OTP xác thực trước khi gửi form."
  error={errors.registrantEmail}
/>
```

Nếu tự viết JSX:

```tsx
<div className="space-y-1.5">
  <label className="text-sm font-semibold">
    Email <span className="text-red-500">*</span>
  </label>

  <p className="text-xs text-slate-500">
    Email này dùng để nhận mã OTP xác thực trước khi gửi form.
  </p>

  <Input ... />

  <div className="min-h-[18px]">
    {error && <p className="text-xs text-red-600">{error}</p>}
  </div>
</div>
```

---

## 2.2 Dropdown/select không được hiện check xanh đè icon

### Vấn đề

Các dropdown/select đang bị xấu vì cùng lúc hiển thị:

```text
- icon dropdown chevron
- icon clear x
- icon check xanh valid
```

Các icon chen nhau ở bên phải input.

Các field bị ảnh hưởng có thể gồm:

```text
- Quốc tịch
- Loại đối tác / kiểu đối tác
- Phạm vi tham quan
- Cơ sở
- Timezone nếu là select
```

### Rule UI mới

```text
Input text/email/phone:
- Có thể hiển thị check xanh nếu valid và không gây đè.

Select/Combobox/Dropdown:
- KHÔNG hiển thị check xanh bên trong ô.
- Chỉ giữ chevron dropdown.
- Nếu có clear icon, phải tính padding-right đủ.
- Valid state của select chỉ dùng border xanh nhẹ hoặc không hiển thị gì.
- Error state dùng border đỏ + message dưới field.
```

### Cách sửa tối ưu

Nếu project có component chung kiểu `ValidatedField`, `FormInput`, `FormSelect`, hãy thêm prop:

```tsx
showValidIcon?: boolean;
```

Mặc định:

```tsx
showValidIcon = true
```

Nhưng với Select/Combobox/DateTime:

```tsx
showValidIcon={false}
```

Hoặc tự động theo type:

```tsx
const shouldShowValidIcon =
  showValidIcon !== false &&
  isValid &&
  touched &&
  !error &&
  !["select", "combobox", "date", "datetime", "time"].includes(fieldType);
```

### Với component Select

```tsx
<FormSelect
  label="Quốc tịch"
  required
  value={value}
  onChange={onChange}
  error={error}
  showValidIcon={false}
/>
```

### CSS yêu cầu

```text
- Select trigger có `relative`.
- Chevron ở right-3.
- Nếu có clear icon, clear ở right-9, chevron ở right-3.
- Text padding-right phù hợp:
  + chỉ chevron: pr-10
  + clear + chevron: pr-16
- Không render check icon trong select trigger.
```

Ví dụ:

```tsx
<div className="relative">
  <button className="w-full pr-10 ...">
    <span className="truncate">{selectedLabel}</span>
    <ChevronDown className="absolute right-3 top-1/2 -translate-y-1/2" />
  </button>
</div>
```

Nếu có clear:

```tsx
<div className="relative">
  <button className="w-full pr-16 ...">
    <span className="truncate">{selectedLabel}</span>
  </button>

  {value && (
    <button type="button" className="absolute right-9 top-1/2 -translate-y-1/2">
      <X className="h-4 w-4" />
    </button>
  )}

  <ChevronDown className="absolute right-3 top-1/2 -translate-y-1/2" />
</div>
```

---

## 2.3 Date/time error không được làm lệch row

### Vấn đề

Ở phần **Thời gian dự kiến thăm FPTU**, lỗi kiểu:

```text
Thời gian kết thúc không được để trống
```

đang hiện ngay dưới input và làm row bị lệch, input kết thúc cao/thấp khác các ô còn lại.

### Yêu cầu

```text
- Cơ sở / Thời gian bắt đầu / Thời gian kết thúc / Timezone phải align đồng đều.
- Error message không được làm các input trong row lệch nhau.
- Khi lỗi xuất hiện, layout không jump mạnh.
```

### Option ưu tiên A — Error slot cố định cho mọi field

Mỗi field có vùng error cố định:

```tsx
<div className="space-y-1">
  <label>Thời gian kết thúc</label>
  <DateTimeInput ... />

  <div className="min-h-[20px] mt-1">
    {error && (
      <p className="text-xs text-red-600 leading-5">
        {error}
      </p>
    )}
  </div>
</div>
```

Ưu điểm:

```text
- Ít sửa.
- Giữ error theo từng field.
- Không làm lệch row vì field nào cũng có min-height.
```

### Option ưu tiên B — Group-level error cho date/time

Trong card thời gian, không show lỗi ngay dưới từng input. Gom lỗi ở dưới cả group:

```tsx
<div className="grid grid-cols-1 lg:grid-cols-[1fr_1.3fr_1.3fr_0.8fr] gap-4 items-start">
  ...
</div>

<div className="min-h-[22px] mt-2">
  {timeError && (
    <p className="text-xs text-red-600">
      {timeError}
    </p>
  )}
</div>
```

Message gợi ý:

```text
Vui lòng nhập đầy đủ thời gian bắt đầu và kết thúc.
```

hoặc:

```text
Thời gian kết thúc phải sau thời gian bắt đầu.
```

### Cách chọn

```text
- Nếu muốn sửa nhanh, dùng Option A.
- Nếu muốn UI đẹp nhất, dùng Option B cho nhóm date/time.
```

### Layout đề xuất cho date/time group

```text
Desktop:
Cơ sở | Thời gian bắt đầu | Thời gian kết thúc | Timezone

Tablet:
Cơ sở | Thời gian bắt đầu
Thời gian kết thúc | Timezone

Mobile:
Mỗi field 1 dòng
```

Tailwind gợi ý:

```tsx
<div className="grid grid-cols-1 md:grid-cols-2 xl:grid-cols-[1fr_1.35fr_1.35fr_0.85fr] gap-4 items-start">
  ...
</div>
```

---

## 2.4 Xóa trường “Số HC/CMND” khỏi step 2

### Vấn đề

Step 2 bảng **Danh sách khách** đang có cột:

```text
Số HC/CMND
```

Field này không khớp SQL `visit_guest_members`. Nếu giữ lại:

```text
- Frontend ép người dùng nhập thông tin không cần thiết.
- Payload có field backend/database không lưu.
- Dễ gây lỗi validation/mapping.
- UI bảng bị dài và xấu.
```

### Yêu cầu xóa triệt để

Phải xóa khỏi:

```text
[ ] UI table column.
[ ] Guest member form state.
[ ] Type/interface GuestMemberFormValue.
[ ] Initial empty guest row.
[ ] Validation schema.
[ ] Payload mapper.
[ ] Excel template tải mẫu nếu có.
[ ] Excel import parser nếu có.
[ ] Any label/message "Số HC/CMND", "CMND", "CCCD", "passport".
```

### Search keywords

```text
Số HC/CMND
HC/CMND
CMND
CCCD
Căn cước
passport
identity
identityNumber
citizenId
idNumber
documentNumber
guestMembers
Danh sách khách
```

### Bảng sau khi sửa

Đề xuất bản tối ưu, khớp SQL và không quá dài:

```text
STT
Họ và tên *
Email
Quốc tịch
Chức vụ
Điện thoại
Đại diện
Ghi chú
Thao tác
```

Nếu modal/table đang quá chật, dùng bản gọn:

```text
STT
Họ và tên *
Email
Quốc tịch
Chức vụ
Thao tác
```

Các field phụ như điện thoại, ghi chú, đại diện đoàn có thể đặt trong:

```text
- expandable row
- drawer/modal sửa khách
- row detail dưới dòng chính
```

### Layout table đề xuất

```text
STT: 56px
Họ và tên: minmax(220px, 1.3fr)
Email: minmax(240px, 1.3fr)
Quốc tịch: minmax(180px, 0.9fr)
Chức vụ: minmax(180px, 0.9fr)
Thao tác: 64px
```

CSS grid gợi ý:

```tsx
grid-template-columns:
  56px
  minmax(220px, 1.3fr)
  minmax(240px, 1.3fr)
  minmax(180px, 0.9fr)
  minmax(180px, 0.9fr)
  64px;
```

Nếu vẫn dùng table HTML:

```text
- Không set width cứng quá nhỏ cho email.
- Email nên truncate với title.
- Table container có overflow-x auto nhưng không làm modal overflow toàn trang.
```

---

# 3. Payload phải khớp backend/SQL

## 3.1 Không gửi field identity

Payload `guestMembers` sau khi sửa không được có:

```json
{
  "identityNumber": "...",
  "citizenId": "...",
  "passportNo": "...",
  "idNumber": "...",
  "documentNumber": "..."
}
```

## 3.2 Payload đúng

Field nên map theo DTO backend hiện tại. Nếu backend DTO dùng camelCase, payload nên kiểu:

```json
{
  "fullName": "Nguyen Van A",
  "organization": "ABC University",
  "jobTitle": "Lecturer",
  "nationality": "Vietnam",
  "email": "a@example.com",
  "phone": "0900000000",
  "isRepresentative": true,
  "note": "..."
}
```

Nếu DTO dùng tên khác như `name` / `position`, phải map đúng DTO đang chạy, nhưng không thêm identity field.

## 3.3 Kiểm tra request payload thật

Mở DevTools Network và verify:

```text
POST /api/visit-requests/initiate
POST /api/visit-requests/verify
```

Trong request body:

```text
[ ] registrantEmail có.
[ ] guestMembers có danh sách khách.
[ ] Không có identityNumber/citizenId/passportNo/idNumber/documentNumber.
[ ] visitScope đúng SINGLE_CAMPUS/MULTI_CAMPUS.
[ ] campuses đúng schema backend.
```

---

# 4. File/khu vực cần kiểm tra

Search trong frontend:

```text
frontend/pems-react/src
```

Các khu vực có khả năng cần sửa:

```text
src/pages/**
src/features/visit-requests/**
src/features/public/**
src/components/**
src/shared/components/**
src/shared/form/**
src/shared/ui/**
```

Search command gợi ý:

```bash
grep -R "Số HC/CMND\|CMND\|CCCD\|passport\|identityNumber\|citizenId\|idNumber\|documentNumber" frontend/pems-react/src
grep -R "CheckCircle\|validIcon\|showValidIcon\|isValid" frontend/pems-react/src
grep -R "pems.visitRequestDraft\|guestMembers\|visit-requests/initiate\|visit-requests/verify" frontend/pems-react/src
```

Nếu dùng Windows PowerShell:

```powershell
Select-String -Path "frontend/pems-react/src/**/*.*" -Pattern "Số HC/CMND","CMND","CCCD","passport","identityNumber","citizenId","idNumber","documentNumber"
Select-String -Path "frontend/pems-react/src/**/*.*" -Pattern "CheckCircle","validIcon","showValidIcon","isValid"
```

Nếu có template Excel:

```text
public/templates/**
src/assets/**
src/templates/**
```

Cũng search và sửa.

---

# 5. UI/UX chuẩn sau khi fix

## 5.1 Step 1 — Thông tin đăng ký

Sau khi fix phải đạt:

```text
[ ] Header/modal không đổi style chính.
[ ] Email có helper text về OTP.
[ ] Helper text không giống error.
[ ] Dropdown quốc tịch không bị icon check xanh đè.
[ ] Dropdown loại/phạm vi/campus không bị icon check xanh đè.
[ ] Date/time row không lệch khi thiếu thời gian kết thúc.
[ ] Error message hiển thị gọn, không làm input nhảy.
[ ] Sticky footer không che mất field cuối.
[ ] Button "Tiếp theo" vẫn nằm dưới, không che validation message.
```

## 5.2 Step 2 — Thành phần tham dự

Sau khi fix phải đạt:

```text
[ ] Không còn cột Số HC/CMND.
[ ] Bảng gọn hơn.
[ ] Email không bị cắt xấu; nếu dài thì truncate có title.
[ ] Dropdown quốc tịch trong row không bị đè icon.
[ ] Error trong row không làm vỡ row quá lớn.
[ ] Thêm khách / xóa khách hoạt động.
[ ] Upload danh sách nếu có hoạt động với template mới.
```

## 5.3 Responsive

Kiểm tra tối thiểu:

```text
[ ] Desktop 1366px.
[ ] Desktop 1920px.
[ ] Tablet width khoảng 768px.
[ ] Mobile nếu modal public hỗ trợ.
```

Ở desktop, modal không nên có horizontal scroll toàn màn hình. Nếu bảng cần overflow-x, chỉ table container được scroll.

---

# 6. Component design guideline

Nếu hiện tại form đang dùng component field chung, cần chuẩn hóa như sau.

## 6.1 BaseFormField

Nên có cấu trúc:

```tsx
type BaseFormFieldProps = {
  label: string;
  required?: boolean;
  helperText?: string;
  error?: string;
  touched?: boolean;
  children: React.ReactNode;
};
```

Render:

```tsx
<div className="space-y-1.5">
  <label className="text-sm font-semibold text-slate-900">
    {label}
    {required && <span className="text-red-500 ml-1">*</span>}
  </label>

  {helperText && !error && (
    <p className="text-xs text-slate-500 leading-5">{helperText}</p>
  )}

  {children}

  <div className="min-h-[20px]">
    {error && (
      <p className="text-xs text-red-600 leading-5">{error}</p>
    )}
  </div>
</div>
```

Nếu muốn helper luôn hiện kể cả khi error, đặt helper trước input, error sau input.

## 6.2 Valid icon rule

```tsx
type ValidatedInputProps = {
  showValidIcon?: boolean;
  fieldType?: "text" | "email" | "phone" | "select" | "combobox" | "date" | "datetime";
};
```

Logic:

```tsx
const canShowValidIcon =
  showValidIcon !== false &&
  !["select", "combobox", "date", "datetime"].includes(fieldType);
```

Không render check xanh nếu:

```text
fieldType = select
fieldType = combobox
fieldType = date
fieldType = datetime
```

## 6.3 Error slot rule

Mọi field trong cùng grid row phải có error slot cố định:

```tsx
<div className="min-h-[20px]">
  {error && <ErrorText>{error}</ErrorText>}
</div>
```

Không để field có lỗi cao hơn field không lỗi nếu gây lệch xấu.

---

# 7. Validation sau khi fix

## 7.1 Email người đăng ký

```text
[ ] Required.
[ ] Email format.
[ ] Note OTP hiển thị.
[ ] Error chỉ hiện khi blur hoặc submit.
```

## 7.2 Date/time

```text
[ ] Start required.
[ ] End required.
[ ] End > Start.
[ ] Nếu backend có rule không quá khứ hoặc 72h advance thì frontend nên match hoặc ít nhất không conflict.
[ ] Error không làm lệch row.
```

## 7.3 Guest members

Không còn identity validation.

Rules đề xuất:

```text
[ ] fullName required.
[ ] email optional hoặc required theo backend DTO hiện tại.
[ ] nationality optional hoặc required theo backend DTO hiện tại.
[ ] jobTitle optional hoặc required theo backend DTO hiện tại.
[ ] phone optional.
[ ] organization optional nếu guest dùng chung organization đăng ký.
[ ] note optional.
```

Quan trọng: frontend validation phải match backend. Không tự bắt required nhiều hơn backend nếu gây khó nhập.

---

# 8. Build/test commands

Sau khi sửa frontend:

```bash
cd frontend/pems-react
npm install
npm run build
```

Nếu có lint:

```bash
npm run lint
```

Nếu có typecheck riêng:

```bash
npm run typecheck
```

Nếu có test:

```bash
npm test
```

Backend không cần build nếu chỉ sửa frontend. Nếu có sửa DTO/backend mapping thì chạy:

```bash
dotnet build backend/PEMS.Api/PEMS.Api.csproj -p:BaseOutputPath=./.tmp-build/
```

---

# 9. Manual test checklist

## 9.1 Step 1

```text
[ ] Mở form đăng ký tham quan.
[ ] Email field có note OTP.
[ ] Nhập email sai → hiện lỗi đẹp.
[ ] Nhập email đúng → không có icon đè nhau.
[ ] Chọn quốc tịch → không hiện check xanh đè clear/chevron.
[ ] Chọn phạm vi/campus → dropdown đẹp, không icon đè.
[ ] Bỏ trống thời gian kết thúc → lỗi không làm lệch row.
[ ] Nhập end <= start → lỗi không làm lệch row.
[ ] Điền đủ thông tin → qua step 2 được.
```

## 9.2 Step 2

```text
[ ] Không còn cột Số HC/CMND.
[ ] Không còn input Số HC/CMND.
[ ] Không còn lỗi required cho Số HC/CMND.
[ ] Thêm khách mới hoạt động.
[ ] Xóa khách hoạt động.
[ ] Dropdown quốc tịch trong bảng không bị đè icon.
[ ] Email dài hiển thị/truncate đẹp.
[ ] Không có horizontal scroll toàn modal nếu không cần.
```

## 9.3 Payload

```text
[ ] Network request initiate không có identityNumber/citizenId/passportNo/idNumber/documentNumber.
[ ] Network request verify không có identityNumber/citizenId/passportNo/idNumber/documentNumber.
[ ] guestMembers chỉ gửi field backend nhận.
[ ] Submit vẫn gửi OTP/initiate/verify đúng flow.
```

## 9.4 Regression

```text
[ ] Public form vẫn mở/đóng modal được.
[ ] Nút Hủy hoạt động.
[ ] Nút Quay lại hoạt động.
[ ] Nút Tiếp theo không bị disabled sai.
[ ] Progress stepper vẫn đúng step.
[ ] sessionStorage draft nếu có vẫn hoạt động.
[ ] Không có console error.
```

---

# 10. Output report bắt buộc

Sau khi fix xong, trả report:

```md
# UC-17 Public Visit Request Form UI + SQL Alignment Fix Report

## Summary
- Fixed email OTP helper note.
- Removed valid check icon from select/dropdown/date-time fields.
- Fixed date/time validation layout shift.
- Removed HC/CMND/CCCD identity field from guest list because it does not exist in pems_full(3).sql.
- Payload now matches backend/SQL fields.

## SQL Alignment
- Source: database/scripts/pems_full(3).sql
- Table checked: visit_guest_members
- Removed fields:
  - identityNumber
  - citizenId
  - passportNo
  - idNumber
  - documentNumber

## Files Changed
- ...

## UI Details
- ...

## Payload Verification
- ...

## Commands Run
```bash
npm run build
```

## Manual Tests
- ...

## Remaining Notes
- ...
```

---

# 11. Definition of Done

Chỉ coi là xong khi:

```text
[ ] Email người đăng ký có note OTP rõ ràng.
[ ] Dropdown/select không còn check xanh đè chevron/clear.
[ ] Date/time error không làm lệch row input.
[ ] Step 2 không còn cột Số HC/CMND.
[ ] Form state không còn identity/CCCD/CMND/passport field.
[ ] Validation không còn bắt nhập identity/CCCD/CMND.
[ ] Payload không gửi identity/CCCD/CMND/passport field.
[ ] Excel template/import nếu có đã bỏ cột identity.
[ ] UI desktop kiểm tra đẹp, không vỡ modal.
[ ] npm run build pass.
[ ] Không sửa SQL để thêm field không cần thiết.
[ ] Không phá UC-17 initiate/verify/resend-otp flow.
```

---

## Kết luận

Đây là task **frontend UI + SQL alignment**, không phải task refactor backend.

Kết quả mong muốn:

```text
Form đăng ký tham quan đẹp hơn
+ dễ hiểu hơn ở bước xác thực email
+ dropdown/date-time không vỡ layout
+ danh sách khách khớp database
+ payload sạch, không gửi field không tồn tại
```
