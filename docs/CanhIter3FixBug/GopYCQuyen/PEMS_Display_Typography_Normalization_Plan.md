# PEMS — Kế hoạch chuẩn hóa Typography cho phần HIỂN THỊ toàn hệ thống

## 1. Mục tiêu

Chuẩn hóa typography cho toàn bộ **display/read-only UI** của PEMS để giao diện thống nhất, dễ đọc và không bị lạm dụng `font-medium`, `font-semibold`, `font-bold` cho dữ liệu thông thường.

File này **bổ sung** cho kế hoạch:

```text
PEMS_Form_Control_Typography_Normalization_Plan.md
```

Hai scope phải được hiểu như sau:

```text
FORM CONTROL TYPOGRAPHY
=> text nằm trong input / textarea / select / custom editable controls

DISPLAY TYPOGRAPHY
=> text đang được hiển thị cho người dùng ngoài form controls
```

Mục tiêu cuối cùng:

> Chỉ dùng độ đậm để thể hiện hierarchy hoặc emphasis có chủ đích. Dữ liệu hiển thị thông thường phải dùng typography trung tính, nhất quán và dễ đọc.

---

# 2. Nguyên tắc cốt lõi

## 2.1 Dữ liệu thông thường

Các nội dung dạng dữ liệu/value thông thường nên mặc định:

```text
font-normal
font-weight: 400
```

Ví dụ:

```text
Họ tên trong dòng thông tin
Email
Số điện thoại
Ngày tháng
Địa chỉ
Quốc gia
Tên đơn vị
Chức vụ
Tên campus
Mô tả metadata
Giá trị field trong detail page
Table body values
Read-only values
Summary values thông thường
Thông tin khách mời
Thông tin người liên hệ
Thông tin chuyến thăm
```

## 2.2 Nội dung được phép mạnh hơn

Có thể dùng:

```text
font-medium
font-semibold
font-bold
```

khi có vai trò hierarchy/emphasis rõ ràng.

Ví dụ:

```text
Page title
Section title
Card title
Modal title
Field label
Table header
Button
Badge/status
Tab title
Important KPI
Important identifier
Critical warning
Primary entity title
```

---

# 3. Typography contract đề xuất

| Semantic role | Default weight |
|---|---:|
| Page title | 600–700 |
| Section title | 600 |
| Card title | 600 |
| Modal title | 600 |
| Field label | 500–600 |
| Normal display value | **400** |
| Metadata | **400** |
| Secondary description | **400** |
| Helper text | **400** |
| Read-only detail value | **400** |
| Table body value | **400** |
| Empty state description | **400** |
| Button | 500–600 |
| Badge / status | 500–600 |
| Table header | 600 |
| KPI / hero number | 600–700 nếu thực sự là KPI |
| Warning heading | 600 |
| Warning description | 400 |

---

# 4. Không áp dụng quy tắc “mọi dữ liệu đều phải normal” một cách máy móc

Một số dữ liệu có thể đồng thời đóng vai trò **primary entity title**.

Ví dụ:

```text
Tên người dùng ở đầu trang Profile
Tên Visit Request ở header detail
Tên đối tác ở card header
Tên bài News ở title
Tên Gallery ở card title
```

Các trường hợp này có thể giữ `font-semibold` / `font-bold`.

Điểm quyết định là:

```text
Nó đang là TITLE/HIERARCHY?
hay
Nó chỉ đang là VALUE?
```

Nếu chỉ là value bên cạnh label:

```text
Họ và tên: Nguyễn Văn A
```

thì `Nguyễn Văn A` mặc định không cần bold.

---

# 5. Scope triển khai

Audit toàn:

```text
frontend/pems-react/src/**
```

Không chỉ tìm form controls.

Cần audit:

```text
span
p
div
td
dd
li
strong
b
Typography wrapper
Card content
Modal content
Detail content
Summary content
Read-only field components
```

kết hợp với:

```text
font-medium
font-semibold
font-bold
font-extrabold
font-black
fontWeight
```

---

# 6. Không mass replace

Tuyệt đối không chạy:

```text
font-medium -> font-normal
font-semibold -> font-normal
font-bold -> font-normal
```

trên toàn repo.

Việc sửa phải dựa trên semantic role.

Ví dụ:

```tsx
<h2 className="font-bold">Thông tin chuyến thăm</h2>
```

=> GIỮ.

```tsx
<span className="font-semibold">{visit.phone}</span>
```

=> cần xem xét chuyển normal nếu đây chỉ là value.

---

# 7. Classification bắt buộc khi audit

Mỗi occurrence có weight từ 500 trở lên phải được xếp vào một trong các nhóm.

## TYPE-A — Heading

Ví dụ:

```text
Page title
Section heading
Card title
Modal title
```

Action:

```text
KEEP nếu hierarchy hợp lý.
```

## TYPE-B — Label

Ví dụ:

```text
Họ và tên
Email
Số điện thoại
Trạng thái
Campus
Ngày bắt đầu
```

Action:

```text
KEEP font-medium/semibold nếu nhất quán.
```

## TYPE-C — Normal display value

Ví dụ:

```text
Nguyễn Văn A
abc@gmail.com
0901234567
FPT University
18/08/2026
```

Action:

```text
NORMALIZE về font-normal.
```

## TYPE-D — Primary entity title

Ví dụ:

```text
Tên visitor ở profile hero
Tên partner ở card header
Tên Visit trong detail header
```

Action:

```text
KEEP medium/semibold/bold nếu đúng hierarchy.
```

## TYPE-E — Status / Badge

Ví dụ:

```text
APPROVED
PENDING
REJECTED
COMPLETED
```

Action:

```text
Có thể KEEP medium/semibold.
```

Không dùng font weight là cách duy nhất phân biệt status; màu/background/icon vẫn phải là tín hiệu chính.

## TYPE-F — Table header

Action:

```text
KEEP semibold.
```

## TYPE-G — Table body data

Action:

```text
NORMALIZE về font-normal,
trừ primary entity column có chủ đích.
```

## TYPE-H — KPI / important metric

Ví dụ:

```text
Total visits
Pending requests
Completion %
```

Action:

```text
KEEP bold nếu đây là dashboard KPI.
```

## TYPE-I — Warning / Alert

Phân biệt:

```text
Alert heading => semibold
Alert description => normal
```

Không bold toàn bộ một đoạn cảnh báo dài.

## TYPE-J — Helper / description / metadata

Action:

```text
font-normal
```

---

# 8. Page detail pattern chuẩn

Một detail block nên hướng đến dạng:

```text
Thông tin người đăng ký            <- section title / semibold

Họ và tên                           <- label / medium
Nguyễn Văn A                        <- value / normal

Email                               <- label / medium
abc@example.com                     <- value / normal

Số điện thoại                       <- label / medium
0901234567                          <- value / normal
```

Không nên:

```text
Họ và tên
NGUYỄN VĂN A                        <- bold

Email
abc@example.com                     <- semibold

Số điện thoại
0901234567                          <- medium
```

nếu các value không có lý do cần emphasis.

---

# 9. Inline label/value pattern

Ví dụ:

```tsx
<span className="font-medium">Email:</span>
<span className="font-normal">{email}</span>
```

Không nên:

```tsx
<span className="font-semibold">
  Email: {email}
</span>
```

nếu cả label và value cùng bị bold ngoài ý muốn.

---

# 10. Table pattern chuẩn

## Header

```text
Tên | Email | Role | Campus | Status
```

=> `font-semibold`

## Body

```text
Nguyễn Văn A
abc@example.com
Visitor
Hòa Lạc
Active
```

Mặc định:

```text
font-normal
```

Ngoại lệ:

- tên có thể `font-medium` nếu là primary clickable entity
- status badge có thể `font-medium/semibold`
- không nên để nhiều cột body cùng medium/bold

---

# 11. Card pattern chuẩn

Ví dụ card Visit:

```text
Visit Request #123                 <- title / semibold

Người đăng ký                      <- label / medium
Nguyễn Văn A                       <- value / normal

Campus                             <- label / medium
Hòa Lạc                            <- value / normal

Ngày thăm                          <- label / medium
20/08/2026                         <- value / normal

APPROVED                           <- badge / semibold
```

Nếu card có 7–10 dòng và tất cả đều `font-medium`, cần normalize lại.

---

# 12. Modal pattern chuẩn

Trong modal:

```text
Modal title            => semibold
Section title          => semibold
Field label            => medium/semibold
Display value          => normal
Description            => normal
Button                 => medium/semibold
Status badge           => medium/semibold
```

Không để toàn bộ modal content bị `font-medium`.

---

# 13. Read-only field pattern

Read-only không có nghĩa là phải bold.

Không nên dùng:

```text
font-semibold
```

để thể hiện read-only state.

Nên phân biệt bằng:

```text
background
border
opacity
icon
cursor
helper text
```

Value vẫn:

```text
font-normal
```

---

# 14. Profile

Audit:

```text
Profile header
Personal info
Account info
Role
Campus
Phone
Email
Nationality
Organization
Position
```

Rule:

```text
Profile name ở hero/header:
có thể bold.

Tên trong detail row:
normal.

Email/phone/value:
normal.

Labels:
medium/semibold.
```

---

# 15. Visit Request

Audit cả create/edit lẫn display/detail.

Display scope:

```text
Registrant
Organization
Contact
Visit purpose
Visit date
Campus
Guest list
External support
Operational contact
Languages
Media
Vehicles
Notes
Consent summary
Approval summary
```

Đặc biệt tránh việc:

```text
label medium
value cũng medium
```

trên toàn bộ card.

---

# 16. Visit Management

Audit:

```text
Visit cards
Visit list
Visit detail header
Campus detail
Registrant info
Host info
Status
Relationship views
Timeline
Summary
```

Rule:

```text
Visit title/ID có thể semibold.

Thông tin chi tiết:
value = normal.
```

---

# 17. Delegation / Members

Audit:

```text
Guest rows
External Support rows
Operational Contact
Participant cards
Member detail
Relationship info
```

Tên người trong member card có thể `font-medium` nếu là primary row title.

Các value:

```text
email
phone
organization
position
member type description
```

nên `font-normal`.

---

# 18. Partner

Audit:

```text
Partner list
Partner detail
Partner card
Approval detail
Relationship detail
Suggested partner
Public partner display
```

Pattern:

```text
Partner name => primary title / semibold

Address/email/phone/status explanation:
normal

Status badge:
semibold
```

---

# 19. Account Management

Audit kỹ:

```text
Account list
Account detail
Role
Campus
Email
Status
Created date
Updated date
Account type
```

Không đổi table header.

Đặc biệt kiểm tra các table body cell hiện đang có:

```text
font-medium
font-semibold
font-bold
```

Phân loại:

```text
account name = có thể primary entity
email = normal
role = normal hoặc badge
campus = normal
date = normal
```

---

# 20. Student

Audit:

```text
Invitation list
Invitation detail
Visit summary
Campus
Host
Date/time
Invitation status
Message
```

Body data mặc định normal.

---

# 21. Gallery

Audit:

```text
Gallery list/cards
Gallery detail
Title
Location
Date
Description
Metadata
Uploader
```

Gallery title:

```text
medium/semibold nếu là card title.
```

Metadata:

```text
normal.
```

Không để title weight lan sang metadata container.

---

# 22. Agenda

Audit:

```text
Agenda item
Time
Location
Host
Description
Reminder
Status
```

Pattern:

```text
Agenda item title => medium/semibold
Time/location/description => normal
```

---

# 23. Minutes

Audit:

```text
Minutes header
Participant table
Contact information
Roles
Notes
Summary
Contributions
```

Table body values:

```text
normal
```

Role/status có thể dùng badge.

Tên participant có thể `font-medium` nếu là primary entity trong row, nhưng email/organization/phone không nên cùng medium.

---

# 24. News

Audit:

```text
News cards
News list
News detail
Author
Publish date
Category
Excerpt
Metadata
```

Article title:

```text
bold/semibold
```

Metadata:

```text
normal
```

Rich-text body:

```text
KHÔNG normalize cưỡng bức.
```

Phải giữ formatting nội dung bài viết.

---

# 25. FAQ

Pattern:

```text
Question => medium/semibold
Answer => normal
Category metadata => normal
```

---

# 26. Email

Audit:

```text
Email list
Email detail
Sender
Recipients
Subject
Date
Template metadata
Configuration display
```

Subject có thể primary.

Sender/recipient/date:

```text
normal
```

---

# 27. Notifications

Audit:

```text
Notification title
Notification description
Timestamp
Unread/read state
```

## Important

Không nên dùng toàn bộ notification text bold để biểu thị unread.

Có thể:

```text
Unread title => medium/semibold
Description => normal
Timestamp => normal
Dot/background => unread indicator
```

Read state:

```text
normal
```

---

# 28. Admin / Configuration

Audit:

```text
Config keys
Config values
API status
System settings
Campus info
Security settings
```

Key/label:

```text
medium
```

Value:

```text
normal
```

---

# 29. Public pages

Audit:

```text
Home
Search
Contact
FAQ
News
Partner
Gallery
Terms
Privacy
```

Không normalize marketing hero typography.

Chỉ normalize dữ liệu/detail metadata nơi cần thiết.

---

# 30. Color và font weight phải tách vai trò

Không dùng đồng thời:

```text
bold
dark color
background
border
icon
```

cho mọi level.

Hierarchy nên rõ:

```text
Title        => size + weight
Label        => weight
Value        => normal
Secondary    => color
Status       => badge/color
Warning      => color/icon + heading weight
```

---

# 31. Audit typography theo container

Khi thấy container kiểu:

```tsx
<div className="font-semibold">
   <span>Label</span>
   <span>{value}</span>
</div>
```

phải kiểm tra inheritance.

Đây là nguồn lỗi phổ biến:

```text
font-semibold đặt ở parent
=> toàn bộ value con bị bold
```

Hướng sửa:

```tsx
<div>
  <span className="font-medium">Label</span>
  <span className="font-normal">{value}</span>
</div>
```

Hoặc bỏ weight ở parent.

---

# 32. Audit class utility responsive

Tìm:

```text
sm:font-medium
md:font-semibold
lg:font-bold
xl:font-bold
```

đặc biệt trên:

```text
card content
detail values
table cells
```

Display typography không được tự nhiên trở nên bold chỉ vì desktop breakpoint.

---

# 33. Audit inline style

Không chỉ Tailwind.

Tìm:

```text
fontWeight:
font-weight:
style={{ fontWeight:
```

với các giá trị:

```text
500
600
700
'bold'
```

---

# 34. Audit semantic HTML

Tìm:

```text
<strong>
<b>
```

Không mặc định xóa.

Phân loại:

```text
Nếu strong dùng để emphasize đúng semantic => KEEP.

Nếu chỉ dùng để làm value trông đậm => đổi sang span/text bình thường.
```

---

# 35. Không phá rich text

Các vùng:

```text
dangerouslySetInnerHTML
rich text editor
article body
email HTML
news body
formatted minutes content
```

không được global override về weight 400.

Nếu user content có:

```html
<strong>
<b>
<h2>
```

thì phải được giữ đúng formatting.

---

# 36. Không phá KPI / dashboard

Dashboard có thể cố ý dùng:

```text
text-3xl font-bold
```

cho số liệu chính.

Không đưa KPI vào rule normal.

Ví dụ:

```text
124 Visits
87% Completion
```

có thể giữ bold nếu đúng vai trò dashboard.

---

# 37. Global display baseline — KHÔNG nên dùng wildcard

Không triển khai kiểu:

```css
span,
p,
td,
div {
  font-weight: 400;
}
```

hoặc:

```css
* {
  font-weight: 400;
}
```

Đây là cách sai.

### Lý do

Nó sẽ phá:

```text
headings
buttons
labels
badges
tabs
KPI
navigation
semantic strong
rich text
```

Display typography phải normalize bằng:

```text
shared components
semantic cleanup
local overrides
design-system conventions
```

không phải wildcard CSS.

---

# 38. Shared display primitives/tokens

Sau audit có thể tạo chuẩn dùng cho code mới.

Ví dụ:

```text
src/shared/styles/typography.ts
```

Concept:

```ts
export const TYPOGRAPHY = {
  pageTitle: 'font-bold',
  sectionTitle: 'font-semibold',
  label: 'font-medium',
  value: 'font-normal',
  metadata: 'font-normal',
  button: 'font-semibold',
  tableHeader: 'font-semibold',
};
```

Hoặc reusable components:

```text
DisplayField
DetailRow
MetadataRow
SectionTitle
EntityTitle
```

---

# 39. Suggested `DisplayField` pattern

Ví dụ concept:

```tsx
<DisplayField
  label="Email"
  value={user.email}
/>
```

Default:

```text
label = medium
value = normal
```

Mục tiêu:

Không để mỗi page tự viết:

```text
font-medium
font-semibold
font-bold
```

cho display values.

---

# 40. Không mega-refactor

Không migrate toàn app sang `DisplayField` trong một PR nếu quá lớn.

Thứ tự:

```text
1. Define typography contract
2. Fix shared display components
3. Fix parent-level inherited weight
4. Audit high-impact pages
5. Audit remaining features
6. Add regression tooling
7. Introduce reusable primitives
8. Migrate legacy UI dần
```

---

# 41. Ưu tiên xử lý

## Priority 1 — Shared / inherited styles

Tìm các component/container có:

```text
font-medium
font-semibold
font-bold
```

ở parent cao.

Một sửa có thể ảnh hưởng nhiều values.

## Priority 2 — Tables

Vì table thường có rất nhiều data và dễ bị “nặng”.

Audit:

```text
td
table row renderer
cell renderer
DataTable
```

## Priority 3 — Detail pages/cards

Đây là nơi label/value hierarchy cần rõ nhất.

## Priority 4 — Modals

Audit read-only summary + confirmation modal.

## Priority 5 — Metadata/list descriptions

Đảm bảo không lạm dụng medium.

---

# 42. Search strategy

Chạy search toàn repo:

```text
font-medium
font-semibold
font-bold
font-extrabold
font-black
fontWeight
<strong
<b>
```

Sau đó cross-check với:

```text
td
span
p
div
dd
li
card
detail
summary
metadata
value
```

Không chỉ dựa vào tên class/file.

---

# 43. Suggested automated audit

Tạo script:

```text
scripts/audit-display-typography.mjs
```

Package script:

```text
audit:display-typography
```

Audit script không nên fail tất cả `font-semibold`.

Nó nên:

1. Scan occurrences.
2. Classify high-risk patterns.
3. Report candidates.
4. Allow exact whitelist.

---

# 44. High-risk patterns cho audit script

Có thể flag:

```tsx
<td className="font-bold">
<td className="font-semibold">
<td className="font-medium">

<span className="font-semibold">{email}</span>
<span className="font-bold">{phone}</span>

<p className="font-medium">{description}</p>

<div className="font-semibold">
  ...label + value...
</div>
```

Đây là candidate, không phải tự động coi là bug.

---

# 45. Low-risk / expected patterns

Audit có thể phân nhóm riêng:

```text
heading
label
button
badge
table header
KPI
entity title
eyebrow
navigation
```

để giảm false positive.

---

# 46. Regression test matrix

Kiểm tra:

```text
Detail page
List page
Card view
Table view
Modal
Confirmation modal
Sidebar detail
Summary panel
Read-only form
Profile view
Dashboard
Mobile cards
```

Expected:

```text
Heading nổi bật.
Label rõ.
Value trung tính.
Status dễ nhận diện.
Không có cảm giác tất cả text đều bold.
```

---

# 47. Visual comparison checklist

Mỗi screen cần kiểm tra:

- [ ] Có nhìn ra page title ngay không?
- [ ] Có nhìn ra section title không?
- [ ] Label và value có phân cấp rõ không?
- [ ] Value có bị bold không cần thiết không?
- [ ] Có quá nhiều `font-medium` trong cùng một card không?
- [ ] Table body có nặng hơn table header không?
- [ ] Metadata có cạnh tranh với primary content không?
- [ ] Status có được phân biệt chủ yếu bằng badge/color không?
- [ ] Mobile có giữ cùng hierarchy không?
- [ ] Không có parent weight làm cả subtree bị bold?

---

# 48. Không thay đổi business logic

Task này chỉ là:

```text
DISPLAY TYPOGRAPHY NORMALIZATION
```

Không thay đổi:

```text
API
DTO
backend
database
authorization
permission
workflow
validation
status transitions
business rules
sorting
filter logic
data source
translations
field order
```

Nếu phát hiện bug khác:

```text
ghi riêng,
không fix chung trong typography task.
```

---

# 49. Không đổi nội dung dữ liệu

Không được:

```text
đổi label wording
rút gọn data
đổi format date
đổi phone
đổi email
đổi name
đổi status text
```

chỉ vì đang sửa typography.

---

# 50. Không đồng nhất font-size một cách tự động

Scope này chủ yếu là:

```text
font-weight / hierarchy
```

Không biến thành task đổi toàn bộ:

```text
font-size
line-height
spacing
colors
```

Nếu cần điều chỉnh nhỏ để hierarchy đúng thì phải ghi rõ, nhưng không mở rộng scope tùy tiện.

---

# 51. Definition of Done

Task chỉ được coi là DONE khi:

- [ ] Có typography contract rõ cho heading/label/value/status/table/KPI.
- [ ] Normal display values mặc định dùng weight 400.
- [ ] Table body data đã được audit.
- [ ] Detail page values đã được audit.
- [ ] Card values đã được audit.
- [ ] Modal display values đã được audit.
- [ ] Read-only values đã được audit.
- [ ] Metadata đã được audit.
- [ ] Shared display components đã được audit.
- [ ] Parent-level inherited font weight đã được audit.
- [ ] `fontWeight` inline styles đã được audit.
- [ ] `<strong>` / `<b>` đã được audit theo semantic.
- [ ] Profile đã được kiểm tra.
- [ ] Visit Request display/detail đã được kiểm tra.
- [ ] Visit Management đã được kiểm tra.
- [ ] Delegation/member display đã được kiểm tra.
- [ ] Partner đã được kiểm tra.
- [ ] Account Management đã được kiểm tra.
- [ ] Student đã được kiểm tra.
- [ ] Gallery đã được kiểm tra.
- [ ] Agenda đã được kiểm tra.
- [ ] Minutes đã được kiểm tra.
- [ ] News / FAQ / Email đã được kiểm tra.
- [ ] Notifications đã được kiểm tra.
- [ ] Admin/configuration đã được kiểm tra.
- [ ] Public display pages phù hợp đã được kiểm tra.
- [ ] Không phá rich-text formatting.
- [ ] Không phá KPI/dashboard typography.
- [ ] Không phá heading hierarchy.
- [ ] Không làm button/badge/table header mất emphasis.
- [ ] Desktop pass.
- [ ] Tablet pass.
- [ ] Mobile pass.
- [ ] Không thay đổi business logic.
- [ ] Không thay đổi API/backend/database.
- [ ] `npm run lint` pass.
- [ ] `npm run test:unit` pass.
- [ ] `npm run build` pass.
- [ ] E2E liên quan pass.
- [ ] Có audit/regression guard hoặc báo cáo repo-wide cuối cùng.

---

# 52. Báo cáo sau triển khai

Báo cáo phải có bảng:

| ID | File | Component | Semantic role | Before | After | Decision | Tested |
|---|---|---|---|---|---|---|---|
| DISP-01 | ... | ... | Normal value | semibold | normal | FIX | PASS |
| DISP-02 | ... | ... | Heading | bold | bold | KEEP | PASS |
| DISP-03 | ... | ... | Table value | medium | normal | FIX | PASS |
| DISP-04 | ... | ... | Status badge | semibold | semibold | KEEP | PASS |

Tổng kết:

```text
Files audited:
Weight occurrences reviewed:
Normal values normalized:
Headings preserved:
Labels preserved:
Badges/status preserved:
Table cells normalized:
Cards normalized:
Detail views normalized:
Shared components fixed:
Exceptions:
Automated checks:
Manual checks:
```

---

# 53. Mọi ngoại lệ phải có lý do

Nếu một display value vẫn giữ:

```text
font-medium
font-semibold
font-bold
```

phải giải thích:

```text
File:
Component:
Value:
Semantic role:
Reason:
```

Không chấp nhận:

```text
"giữ vì UI cũ đang như vậy"
```

làm lý do duy nhất.

---

# 54. Acceptance criteria cuối cùng

Một người nhìn vào UI phải nhận biết được hierarchy theo thứ tự:

```text
PAGE TITLE
    ↓
SECTION / CARD TITLE
    ↓
LABEL / PRIMARY ENTITY
    ↓
NORMAL VALUE
    ↓
METADATA / SECONDARY TEXT
```

Không được có tình trạng:

```text
Title = bold
Label = bold
Value = bold
Metadata = medium
Button = bold
Badge = bold
```

khiến toàn trang gần như cùng một mức emphasis.

---

# 55. Quan hệ với file Form Control Typography

Hai task phải thống nhất cùng contract:

```text
Editable input value      => normal
Selected form value       => normal
Read-only display value   => normal

Label                     => medium/semibold
Heading                   => semibold/bold
Button                    => medium/semibold
Badge/status              => medium/semibold
Primary entity title      => medium/semibold khi có chủ đích
```

Sau khi cả hai task hoàn thành, PEMS sẽ có một typography hierarchy nhất quán cho cả:

```text
INPUT
+
DISPLAY
```

---

# 56. Scope lock

Task này KHÔNG phải redesign toàn bộ PEMS.

Chỉ giải quyết:

```text
DISPLAY TYPOGRAPHY
FONT-WEIGHT HIERARCHY
CONSISTENCY
```

Không mở rộng tự ý sang:

```text
layout redesign
color redesign
spacing redesign
responsive redesign
component restructuring lớn
business logic
backend
API
database
permission
workflow
```

---

# 57. Nguyên tắc triển khai quan trọng nhất

Không sửa theo:

```text
'thấy bold thì đổi normal'
```

Mà phải audit theo:

```text
SEMANTIC ROLE
       ↓
HEADING / LABEL / VALUE / STATUS / KPI / METADATA
       ↓
DECIDE KEEP OR NORMALIZE
       ↓
VERIFY HIERARCHY
       ↓
REGRESSION GUARD
```

Kết quả cần đạt:

> PEMS có hierarchy typography rõ ràng và đồng nhất: tiêu đề và thành phần cần emphasis vẫn nổi bật, còn dữ liệu hiển thị thông thường nhẹ, dễ đọc và không bị đậm quá mức.
