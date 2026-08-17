# PEMS — Kế hoạch chuẩn hóa Typography cho toàn bộ Form Controls

## 1. Mục tiêu

Chuẩn hóa typography cho toàn bộ các trường nhập liệu trong PEMS để dữ liệu người dùng nhập hoặc giá trị được chọn **không bị bold/semibold/medium quá mức**.

Quy tắc chính:

- Page title / section title / card title có thể dùng `font-semibold` hoặc `font-bold`.
- Label của field có thể dùng `font-medium` / `font-semibold`.
- **Giá trị người dùng nhập trong `input` phải là `font-normal`.**
- **Giá trị người dùng nhập trong `textarea` phải là `font-normal`.**
- **Giá trị đang được chọn trong `select` phải là `font-normal`.**
- **Giá trị trong custom form control như `react-select`, combobox, autocomplete... phải là `font-normal`.**
- Placeholder phải là `font-normal`.
- Readonly / disabled value mặc định vẫn là `font-normal`, phân biệt trạng thái bằng màu nền, border, opacity... thay vì tăng độ đậm.
- Helper text / validation error mặc định là `font-normal`.
- Không làm ảnh hưởng đến heading, label, button, badge, table header, rich-text formatting hoặc các text được chủ đích nhấn mạnh.

Mục tiêu cuối cùng là giải quyết vấn đề ở **toàn hệ thống**, không chỉ sửa một vài màn đã được feedback.

---

# 2. Code baseline

Thực hiện trên code mới nhất của PEMS nhánh `Dev`.

Baseline đã rà soát khi lập kế hoạch:

```text
Branch: Dev
HEAD: b010de68411c35951bce9b5a9347ef82419c6de0
```

Trước khi bắt đầu triển khai thực tế:

1. Checkout/pull nhánh `Dev` mới nhất.
2. Ghi lại HEAD mới nhất.
3. Nếu HEAD đã thay đổi so với baseline trên, vẫn áp dụng kế hoạch này nhưng phải audit lại các file mới/thay đổi liên quan form controls.
4. Không triển khai dựa trên file cũ hoặc code ở branch khác.

---

# 3. Kết luận về yêu cầu cần confirm

## Không cần confirm thêm về nghiệp vụ

Yêu cầu đã đủ rõ để triển khai:

> Các giá trị nằm bên trong các form controls cần dùng text bình thường. Độ đậm dùng cho hierarchy của giao diện như tiêu đề, label, button, badge hoặc text nhấn mạnh có chủ đích.

Không cần hỏi lại stakeholder cho các trường hợp sau:

- `<input>`
- `<textarea>`
- `<select>`
- search box
- filter dropdown
- editable text field
- editable number field
- editable phone/email
- date/time picker text
- combobox
- autocomplete
- `react-select`
- custom dropdown
- selected value trong form control
- readonly form field
- disabled form field

### Ngoại lệ không thuộc scope

Không tự động đổi độ đậm của:

- `h1` / `h2` / `h3`
- section heading
- card heading
- `<label>`
- button text
- badge/status
- table header
- breadcrumb
- tên người dùng ở chế độ display nếu đang đóng vai trò title
- rich text do người dùng chủ động format
- warning / alert text có chủ đích nhấn mạnh
- text trong table/list không phải editable control

---

# 4. Bằng chứng đã xác nhận trong code hiện tại

Đây không phải danh sách cuối cùng, mà là các target đã xác nhận có vấn đề hoặc có nguy cơ cần xử lý.

## 4.1 Visit Request — `AutoGrowTextField`

File:

```text
frontend/pems-react/src/features/visit-request/components/shared/AutoGrowTextField.tsx
```

Hiện tại `<textarea>` có `font-semibold`.

### Vấn đề

Text người dùng nhập bị semibold trực tiếp từ shared component.

### Hướng xử lý

- Bỏ `font-semibold` khỏi editable textarea.
- Thiết lập `font-normal`.
- Kiểm tra tất cả nơi sử dụng `AutoGrowTextField`.
- Không sửa từng caller nếu có thể sửa đúng ở shared component.

---

## 4.2 Visit Request — `CountrySelect`

File:

```text
frontend/pems-react/src/features/visit-request/components/shared/CountrySelect.tsx
```

Component sử dụng `react-select`.

Code hiện tại có style tương đương:

```text
fontWeight: 500
```

cho control/selected value.

### Vấn đề

Global CSS dành cho native `<select>` không tác động đầy đủ đến `react-select`.

### Hướng xử lý

Chuẩn hóa:

```text
control
input
singleValue
placeholder
option
```

về weight `400` đối với phần value/text nhập.

Không dùng bold để biểu thị selected state.

Selected state có thể phân biệt bằng:

- background
- color
- check icon
- hover state

---

## 4.3 Profile

File:

```text
frontend/pems-react/src/pages/dashboard/profile/Profile.tsx
```

Đã thấy editable fields có:

```text
font-bold
font-medium
```

Ví dụ:

- Họ tên
- Số điện thoại

### Hướng xử lý

Phân biệt rõ:

```text
VIEW MODE:
Tên có thể bold nếu là heading/display title.

EDIT MODE:
<input value="..."> => font-normal.
```

Không để style từ display mode lan sang editable input.

---

## 4.4 Gallery

File:

```text
frontend/pems-react/src/pages/dashboard/gallery/GalleryUpsertModal.tsx
```

Đã thấy editable input có `font-medium`.

### Hướng xử lý

Value trong input chuyển về `font-normal`.

Label/title của modal vẫn giữ hierarchy hiện tại nếu hợp lý.

---

## 4.5 Account Management

File:

```text
frontend/pems-react/src/pages/dashboard/accounts/AccountManagement.tsx
```

Nhiều `<select>` filter hiện sử dụng:

```text
font-medium
```

Ví dụ:

- Campus
- Account type
- Role
- Status
- page size

### Hướng xử lý

Selected value trong các filter dropdown chuyển về `font-normal`.

Không thay đổi:

- table heading
- account name trong display cell
- button
- status badge
- section title

trừ khi đó thực sự là form control editable.

---

## 4.6 Native auth form

Một số auth form hiện không ép `font-medium/font-bold` lên input.

Đây là pattern tốt:

```text
Label      => semibold
Input      => normal
Button     => semibold/bold
```

Nên dùng làm reference khi normalize các form khác.

---

# 5. Root Cause

## RC-01 — Chưa có form typography contract toàn hệ thống

Hiện PEMS chưa có một rule rõ ràng bắt buộc:

```text
editable value = font-normal
```

Do đó mỗi feature tự quyết định.

---

## RC-02 — Nhiều màn viết native form control trực tiếp

Pattern hiện tại có nhiều:

```tsx
<input className="..." />
<textarea className="..." />
<select className="..." />
```

và mỗi màn tự thêm:

```text
font-medium
font-semibold
font-bold
```

---

## RC-03 — Shared component của từng feature có style riêng

Ví dụ:

```text
AutoGrowTextField
CountrySelect
UnitPriceInput
...
```

Nếu chỉ sửa page-level sẽ dễ bỏ sót component dùng chung.

---

## RC-04 — Custom controls không tuân theo native CSS

Ví dụ:

```text
react-select
```

Không thể chỉ thêm:

```css
select {
  font-weight: 400;
}
```

rồi coi như hoàn thành.

---

## RC-05 — Chưa có automated regression guard

Code mới sau này vẫn có thể thêm lại:

```tsx
<input className="font-semibold" />
```

nếu không có audit/lint guard.

---

# 6. Chiến lược triển khai

Không sửa theo từng feedback hoặc từng page một cách độc lập.

Thứ tự bắt buộc:

```text
1. Global baseline
2. Shared controls
3. Custom controls
4. Repo-wide audit
5. Feature-level cleanup
6. Regression guard
7. Automated + manual verification
```

---

# 7. PHASE 1 — Global native form typography baseline

Target:

```text
frontend/pems-react/src/index.css
```

Thiết lập baseline cho native form controls.

Ví dụ hướng triển khai:

```css
@layer base {
  input:not([type="button"]):not([type="submit"]):not([type="reset"]),
  textarea,
  select {
    font-weight: 400;
  }
}
```

## Mục đích

Nếu developer tạo:

```tsx
<input className="..." />
```

mà không chỉ định weight thì mặc định value là normal.

## Không được dùng

```css
font-weight: 400 !important;
```

### Lý do

`!important` có thể:

- phá các component đặc thù
- khiến override hợp lệ khó thực hiện
- che giấu lỗi thiết kế component
- tạo technical debt mới

Global rule chỉ là baseline.

---

# 8. PHASE 2 — Audit toàn bộ native controls

Audit toàn:

```text
frontend/pems-react/src/**
```

Tìm:

```text
<input
<textarea
<select
```

Sau đó rà các class:

```text
font-medium
font-semibold
font-bold
font-extrabold
font-black
```

## Quy tắc

### Sửa

```tsx
<input className="font-bold" />
<input className="font-semibold" />
<input className="font-medium" />

<textarea className="font-semibold" />

<select className="font-medium" />
```

khi class đó đang tác động đến value/text nhập.

### Không sửa chỉ vì thấy bold

```tsx
<label className="font-semibold">
<h2 className="font-bold">
<button className="font-semibold">
<span className="badge font-semibold">
<th className="font-semibold">
```

---

# 9. PHASE 3 — Shared controls trước page-level controls

Phải tìm và xử lý shared/reusable components trước.

Ưu tiên kiểm tra:

```text
src/shared/**
src/components/common/**
src/features/**/components/shared/**
```

Các target đã biết:

```text
AutoGrowTextField
CountrySelect
UnitPriceInput
```

Sau đó tìm thêm:

```text
Input
TextField
SearchInput
PhoneInput
EmailInput
NumberInput
CurrencyInput
DateInput
TimeInput
Select
Dropdown
Combobox
Autocomplete
RichText
Textarea
```

## Quy tắc

Nếu 10 màn dùng một shared component:

```text
Không sửa 10 màn trước.

Sửa shared component đúng một lần.
Sau đó kiểm tra caller có override hay không.
```

---

# 10. PHASE 4 — Audit custom controls

Không dừng ở native HTML controls.

Tìm toàn repo các package/component:

```text
react-select
contentEditable
combobox
autocomplete
date picker
time picker
phone input
custom dropdown
search-select
tag/chip input
currency/price input
auto-grow textarea
```

## Đối với `react-select`

Kiểm tra tối thiểu:

```text
control
input
singleValue
placeholder
option
valueContainer
```

Value/input text:

```text
fontWeight = 400
```

Không bắt buộc menu option phải bold chỉ vì selected.

---

# 11. PHASE 5 — Feature coverage

Sau khi xử lý global/shared/custom, phải rà từng feature để bắt local override.

## Authentication

- Login
- Forgot Password
- Reset Password
- Change Password
- OTP / verification controls

## Profile

- full name
- phone
- nationality
- editable personal data
- password forms

## Visit Request

- Create request
- Edit Pending
- Quick Edit
- Amendment
- registrant fields
- guest list
- external support
- operational contact
- campus fields
- notes
- language/media/vehicle
- consent-related editable controls

## Visit Management

- search
- filters
- detail → edit
- modal forms
- status filters

## Delegation

- guest
- external support
- operational contact
- member picker
- member editing

## Partner

- create
- edit
- search
- filters
- relationship controls
- approval form

## Account Management

- search
- campus
- role
- account type
- status
- create account
- edit account
- security/role modals

## Student

- invitation
- accept/decline
- note
- search/filter

## Gallery

- create
- edit
- title
- location
- description
- metadata
- filters

## Agenda

- create/edit
- template
- reminder
- agenda content controls

## Minutes

- editable minute data
- participant entry
- note
- role/member controls

## News

- create
- edit
- filters
- search
- editor metadata

## FAQ

- create/edit
- search
- filters

## Email

- recipient
- subject
- compose
- email configuration form
- search/filter

## Documents

- search
- filters
- metadata form

## Notifications

- filters
- notification settings if any

## Admin / configuration

- API management
- configuration
- security
- filters
- editable config fields

## Public pages

- search
- contact form
- other editable public controls

---

# 12. Không được mass replace toàn bộ `font-medium`

Tuyệt đối không chạy kiểu:

```text
Replace all:
font-medium -> font-normal
```

trên toàn repo.

### Vì sẽ phá

- labels
- buttons
- badges
- table headers
- headings
- menu items
- intentional emphasis
- cards
- breadcrumbs

Audit phải dựa trên semantic context của element.

---

# 13. Không thay đổi business logic

Task này là UI typography normalization.

Không được tiện tay thay đổi:

```text
API
payload
validation
business rule
authorization
role
permission
workflow
status
database
DTO
entity
form field order
business wording
request processing
```

Nếu trong quá trình sửa phát hiện bug nghiệp vụ khác:

```text
Ghi nhận riêng.
Không trộn vào PR typography này.
```

---

# 14. Shared form contract

Sau khi cleanup hiện tại, tạo chuẩn dùng cho code mới.

Có thể chọn một trong hai hướng.

## Hướng A — Shared primitives

Ví dụ:

```text
src/shared/components/form/
    FormInput.tsx
    FormTextarea.tsx
    FormSelect.tsx
```

Base style bắt buộc:

```text
font-normal
```

cùng các chuẩn:

```text
text size
border
focus
disabled
error
```

---

## Hướng B — Shared style tokens

Nếu chưa muốn refactor component lớn:

```text
src/shared/styles/formStyles.ts
```

Ví dụ:

```ts
export const FORM_CONTROL_TEXT = 'font-normal';
```

Shared/custom controls sử dụng chung token này.

---

# 15. Không refactor toàn app sang FormInput trong một lần

Không nên biến task typography thành mega-refactor.

Triển khai an toàn:

```text
1. Global baseline
2. Shared component cleanup
3. Custom control cleanup
4. Local override cleanup
5. Add new primitives
6. Code mới bắt buộc dùng primitives
7. Migrate code cũ dần khi phù hợp
```

---

# 16. Regression guard

PEMS đã có các audit/test scripts.

Nên bổ sung:

```text
audit:form-typography
```

Ví dụ script:

```text
scripts/audit-form-typography.mjs
```

## Script cần phát hiện

Các pattern đáng ngờ như:

```tsx
<input ... font-medium ...>
<input ... font-semibold ...>
<input ... font-bold ...>

<textarea ... font-medium ...>
<textarea ... font-semibold ...>
<textarea ... font-bold ...>

<select ... font-medium ...>
<select ... font-semibold ...>
<select ... font-bold ...>
```

Và custom controls đã biết.

## Không fail mù quáng

Nếu có ngoại lệ hợp lệ:

```text
whitelist chính xác component/line/pattern.
```

Không whitelist nguyên feature.

---

# 17. Test matrix

Sau sửa cần kiểm tra các trạng thái sau.

## Native input

```text
Empty
Typing
Focused
Blurred
Validation error
Disabled
Readonly
Autofilled
Hydrated from API
Edit existing data
```

Expected:

```text
Value text = font-normal
```

---

## Textarea

```text
Empty
Typing
Multi-line
Auto grow
Validation
Disabled
Readonly
Existing data
```

Expected:

```text
Content = font-normal
```

---

## Select

```text
Placeholder
Selected value
Change value
Disabled
Readonly-like presentation
Validation
```

Expected:

```text
Selected value = font-normal
```

---

## Custom select / react-select

```text
Placeholder
Typing search
Selected value
Dropdown option
Focused
Disabled
Clear
Re-select
```

Expected:

```text
User-entered/selected value = font-normal
```

---

# 18. User-flow regression tests

Spot check các flow:

```text
Create
Edit
View -> Edit
Quick Edit
Amendment
Modal edit
Search
Filter
Autofill
Profile hydration
Clone/copy form
Import data
```

Mục tiêu:

Không có trường hợp:

```text
Typing manually = normal
Autofilled from API = bold
```

hoặc ngược lại.

Cùng một field phải có typography nhất quán bất kể nguồn dữ liệu.

---

# 19. Responsive verification

Kiểm tra:

```text
desktop
tablet
mobile
```

Font-weight không được thay đổi theo breakpoint trừ khi có thiết kế đặc biệt được ghi rõ.

Đặc biệt kiểm tra:

```text
sm:
md:
lg:
```

có class dạng:

```text
md:font-semibold
lg:font-bold
```

đang tác động vào form control hay không.

---

# 20. Verification commands

Sau khi sửa:

```bash
npm run lint
npm run test:unit
npm run build
```

Nếu các E2E có liên quan:

```bash
npm run test:e2e
```

Nếu project có real-stack environment phù hợp:

```bash
npm run test:e2e:realstack
```

Ngoài automated tests phải có manual UI spot check.

---

# 21. Definition of Done

Task chỉ được coi là DONE khi đáp ứng **tất cả** các điều sau.

- [ ] Native `input` có baseline `font-weight: 400`.
- [ ] Native `textarea` có baseline `font-weight: 400`.
- [ ] Native `select` có baseline `font-weight: 400`.
- [ ] Không còn `font-medium` không chủ ý trên editable input values.
- [ ] Không còn `font-semibold` không chủ ý trên editable input values.
- [ ] Không còn `font-bold` không chủ ý trên editable input values.
- [ ] Không còn `font-extrabold/font-black` trên editable values.
- [ ] `AutoGrowTextField` đã normalize.
- [ ] `CountrySelect` / `react-select` đã normalize.
- [ ] Profile editable fields đã normalize.
- [ ] Gallery editable fields đã normalize.
- [ ] Account Management select/filter values đã normalize.
- [ ] Các shared form controls khác đã được audit.
- [ ] Các custom controls khác đã được audit.
- [ ] Visit Request đã được spot check.
- [ ] Visit Management đã được spot check.
- [ ] Profile đã được spot check.
- [ ] Partner đã được spot check.
- [ ] Account Management đã được spot check.
- [ ] Student đã được spot check.
- [ ] Gallery đã được spot check.
- [ ] Agenda đã được spot check.
- [ ] Minutes đã được spot check.
- [ ] News/FAQ/Email/Documents/Admin đã được spot check nếu có form controls.
- [ ] Placeholder không bị bold.
- [ ] Autofilled value không bị bold.
- [ ] API-hydrated value không bị bold.
- [ ] Readonly/disabled value không bị bold ngoài ý muốn.
- [ ] Mobile không có breakpoint đổi value thành bold.
- [ ] Label/headings/buttons/badges không bị sửa nhầm.
- [ ] Rich text formatting không bị phá.
- [ ] Không thay đổi business logic/API/DB.
- [ ] `npm run lint` pass.
- [ ] `npm run test:unit` pass.
- [ ] `npm run build` pass.
- [ ] E2E liên quan pass.
- [ ] Có `audit:form-typography` hoặc regression guard tương đương.
- [ ] Repo-wide search cuối cùng không còn violation không được giải thích.

---

# 22. Repo-wide search cuối cùng

Trước khi báo cáo hoàn thành phải chạy lại search toàn repo với các nhóm sau:

```text
<input
<textarea
<select
react-select
contentEditable
font-medium
font-semibold
font-bold
font-extrabold
font-black
fontWeight
```

Sau đó đối chiếu từng result có liên quan form controls.

Không được báo cáo:

```text
"Đã fix toàn bộ"
```

nếu chỉ kiểm tra các file đã biết từ feedback.

---

# 23. Báo cáo sau triển khai

Báo cáo cuối cùng phải có bảng:

| ID | File | Component | Control type | Before | After | Shared impact | Tested |
|---|---|---|---|---|---|---|---|
| TYP-01 | ... | ... | input | font-bold | font-normal | ... | PASS |
| TYP-02 | ... | ... | textarea | font-semibold | font-normal | ... | PASS |
| TYP-03 | ... | ... | react-select | 500 | 400 | ... | PASS |

Và tổng kết:

```text
Native controls audited:
Custom controls audited:
Shared components fixed:
Local overrides fixed:
Files changed:
Features verified:
Automated tests:
Manual tests:
Remaining exceptions:
```

Nếu còn exception:

```text
File:
Component:
Reason:
Why it must remain bold:
```

Không được để exception không có lý do.

---

# 24. Nguyên tắc triển khai quan trọng nhất

Không sửa theo tư duy:

```text
"User thấy chỗ nào bold thì sửa chỗ đó."
```

Phải sửa theo:

```text
GLOBAL BASELINE
        ↓
SHARED CONTROLS
        ↓
CUSTOM CONTROLS
        ↓
REPO-WIDE AUDIT
        ↓
FEATURE CLEANUP
        ↓
REGRESSION GUARD
        ↓
FULL VERIFICATION
```

Kết quả cần đạt:

> Mọi form control mới hoặc cũ trong PEMS mặc định hiển thị dữ liệu người dùng bằng `font-normal`, trong khi hierarchy của label, title, button và các thành phần nhấn mạnh vẫn được giữ đúng.

---

# 25. Scope lock

Task này chỉ giải quyết:

```text
FORM CONTROL VALUE TYPOGRAPHY
```

Không mở rộng sang:

```text
font-size toàn hệ thống
spacing
responsive overlap
card density
colors
field ordering
UX wording
business logic
permission
API
database
```

Các vấn đề đó phải xử lý thành task riêng để PR nhỏ, dễ review và giảm regression.
