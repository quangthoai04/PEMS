# PEMS — System-Wide Responsive UI Audit & Fix Plan

> Mục tiêu: giải quyết góp ý **“Lỗi giao diện khi sử dụng web trên điện thoại”** theo hướng chuẩn hóa responsive cho **toàn bộ frontend PEMS**, không vá riêng một vài màn hình đang bị lỗi.

---

# 0. SOURCE OF TRUTH / BASELINE

Repository:

```text
quangthoai04/PEMS
```

Branch:

```text
Dev
```

HEAD đã khóa khi lập kế hoạch:

```text
a7f164d77066319b8c7e82e261ed3b3e384cd41e
```

Commit message:

```text
Merge branch 'Canh_iter3_FixBug' into Dev
```

## Bắt buộc trước khi triển khai

1. Pull/checkout `Dev` mới nhất.
2. Ghi lại HEAD mới.
3. Nếu HEAD khác `a7f164d77066319b8c7e82e261ed3b3e384cd41e`:
   - audit lại các file frontend đã thay đổi;
   - không dùng snapshot code cũ để kết luận;
   - giữ nguyên các nguyên tắc và Definition of Done trong file này.
4. Không sửa responsive dựa trên ảnh feedback בלבד; ảnh chỉ là symptom.
5. Source of truth là code thực tế ở HEAD triển khai.

---

# 1. USER FEEDBACK CẦN GIẢI QUYẾT

Feedback hiện tại:

> Giao diện PEMS khi sử dụng trên điện thoại bị lỗi responsive: các thành phần đè lên nhau, bố cục xấu, chữ bị cắt/hỏng/khó đọc, button/input/dropdown/table/modal không co giãn đúng với màn hình.

Task này phải giải quyết vấn đề đó theo hướng:

```text
TOÀN HỆ THỐNG
+
MOBILE FIRST
+
RESPONSIVE THEO VIEWPORT
+
KHÔNG CHE LỖI BẰNG OVERFLOW-HIDDEN
+
KHÔNG LÀM THAY ĐỔI BUSINESS LOGIC
```

---

# 2. KẾT QUẢ MONG MUỐN

PEMS phải sử dụng tốt trên:

```text
điện thoại nhỏ
điện thoại phổ biến
điện thoại màn hình lớn
điện thoại landscape
tablet portrait
tablet landscape
laptop nhỏ
desktop
màn hình lớn
browser zoom
```

Không được có:

- component đè lên component khác;
- button đè input;
- button chạy ra ngoài viewport;
- text bị crop mà người dùng không thể đọc;
- text bị ép còn vài ký tự;
- field nhỏ tới mức khó nhập;
- dropdown/popover chạy ra khỏi màn hình;
- modal có nút nhưng không thể scroll tới;
- modal bị bàn phím điện thoại che;
- table ép cả page rộng hơn viewport;
- card có chiều rộng cố định làm page overflow;
- fixed/sticky header che content;
- sidebar/drawer rộng hơn viewport;
- pagination chạy khỏi màn hình;
- một hàng có quá nhiều button nhưng không wrap/stack;
- tooltip/popover bị modal/header che do z-index;
- ảnh/chart/video ép layout;
- lỗi khi xoay portrait ↔ landscape;
- lỗi khi tăng font hoặc zoom trình duyệt.

---

# 3. PHẠM VI

Audit toàn:

```text
frontend/pems-react/src/**
```

Không chỉ những màn user đã chụp ảnh feedback.

## Scope bao gồm

### Global / shell

```text
App.tsx
index.css
Header
Footer
DashboardLayout
Sidebar
Error pages
Toast
Global modal/overlay
```

### Public

```text
Home
News list/detail
Partner list/detail
Visit FPTU / Gallery
Campus public detail
FAQ
Privacy
Terms
Search
Login modal
Notification center
```

### Authentication / Identity

```text
Forgot Password
Reset Password
Change Password
Confirm Email
Invitation confirmation
Operational Contact confirmation
403
Invalid Account
Not Found
```

### Dashboard

```text
Dashboard Home
Profile
Account Management
Campus
Department
My Department
News
Email
Partner
Visit Management
Visit Detail
Visit Request V2
Visit Edit
Pending Edit
Quick Edit
Amendment
Visit Process
Visit Preparation
Visit During
Visit After
Contribution
Reception Detail
Process Summary
Invitation
Contact Invitation
Agenda
Agenda Template
Gallery
Gallery Location
Photos
Minutes
Documents
Post Visit Tasks
Feedback
Reports
FAQ
API Management
Session Management
Security Monitoring
Audit Logs
Notifications
```

### Role coverage

Phải test UI theo các role có quyền truy cập tương ứng:

```text
VISITOR
STUDENT
STAFF
STAFF_LEADER
DEPARTMENT_STAFF
DEPARTMENT_LEAD
HO
ADMIN
```

Không được chỉ test role đang được user feedback.

---

# 4. NHỮNG GÌ ĐÃ XÁC NHẬN TRONG CODE MỚI NHẤT

Đây là các root-cause candidate đã thấy trực tiếp ở HEAD hiện tại.

Không được hiểu đây là danh sách đầy đủ. Dev vẫn phải chạy repo-wide audit.

---

## RC-01 — Dashboard đang khóa viewport bằng `h-screen`

File:

```text
frontend/pems-react/src/components/layout/DashboardLayout.tsx
```

Hiện có pattern:

```tsx
<div
  id="dashboard-root"
  className="flex h-screen ... overflow-hidden ..."
>
```

và:

```tsx
<main
  id="dashboard-main"
  className="flex-1 max-h-screen overflow-y-auto ..."
>
```

### Rủi ro mobile

`h-screen` / `100vh` không phải lúc nào cũng tương ứng với vùng nhìn thấy thực tế trên mobile browser.

Chrome/Safari mobile còn có:

```text
address bar
bottom browser chrome
safe area
virtual keyboard
```

Khi dùng `100vh` + scroll container nội bộ:

- bottom content có thể bị che/cắt;
- modal/page có thể trông cao hơn vùng thực tế;
- keyboard mở làm field hoặc action footer biến mất;
- có thể hình thành nested-scroll khó sử dụng.

### Hướng xử lý

Audit shell theo dynamic viewport:

```text
dvh / svh
```

Ví dụ định hướng:

```text
min-h-dvh
h-dvh
max-h-dvh
```

hoặc CSS fallback phù hợp.

Không thay đổi ngay một cách máy móc. Phải test:

```text
iOS Safari
Chrome Android
keyboard open
orientation change
```

---

## RC-02 — Dashboard dùng `overflow-hidden` ở root

Cũng tại:

```text
DashboardLayout.tsx
```

root hiện:

```text
overflow-hidden
```

### Rủi ro

Nếu page con bị tràn ngang:

```text
overflow-hidden
```

có thể CHE symptom thay vì giúp phát hiện source overflow.

Người dùng có thể mất phần nội dung mà không có cách scroll tới.

### Rule

Không dùng:

```css
body {
  overflow-x: hidden;
}
```

hoặc tăng thêm `overflow-hidden` như cách fix responsive toàn hệ thống.

Phải tìm element nào đang làm rộng viewport.

---

## RC-03 — Mobile Sidebar có width cố định

File:

```text
frontend/pems-react/src/components/dashboard/Sidebar.tsx
```

Mobile drawer hiện có:

```text
w-[290px]
```

Desktop cũng dùng:

```text
w-[290px]
w-[84px]
```

### Vấn đề

290px hiện vẫn dùng được trên nhiều điện thoại, nhưng width phải được clamp theo viewport.

Điện thoại rất nhỏ, zoom, split-screen hoặc browser side panel có thể khiến drawer chiếm gần/toàn bộ màn hình.

### Direction

Mobile drawer phải có kiểu:

```text
width <= viewport - safe gutter
```

Ví dụ concept:

```text
w-[min(290px,calc(100vw-1rem))]
```

hoặc tương đương.

Không để fixed pixel là điều kiện duy nhất.

---

## RC-04 — Public Header sử dụng nhiều fixed widths

File:

```text
frontend/pems-react/src/components/layout/Header.tsx
```

Đã thấy:

```text
w-[92px]
w-[84px]
w-[104px]
w-[148px]
w-[72px]
w-[140px]
xl:w-[170px]
2xl:w-[200px]
w-[132px]
```

Desktop nav còn sử dụng:

```text
whitespace-nowrap
overflow-hidden
truncate
```

### Vấn đề

Header đang đổi sang desktop nav từ `xl`, nhưng tổng:

```text
logo
+
fixed-width nav links
+
actions
+
profile
+
gutter
```

có thể sát giới hạn ở viewport trung gian.

Nếu không đủ width, cách hiện tại có thể:

```text
truncate label
crop nội dung
squeeze action
```

thay vì chuyển sang layout phù hợp.

### Direction

- Test kỹ tại 1280px / 1366px.
- Không chấp nhận nav label bị crop để “vừa”.
- Nếu không đủ width:
  - giảm gap hợp lý;
  - dùng content-based/flexible width;
  - hoặc chuyển breakpoint sang mobile/tablet nav trước khi xảy ra overflow.
- Không giảm font xuống quá nhỏ để nhét nav.

---

## RC-05 — Fixed/min widths trong Account Management

File:

```text
frontend/pems-react/src/pages/dashboard/accounts/AccountManagement.tsx
```

Filter search hiện có:

```text
min-w-[250px]
```

Pagination dùng:

```text
flex items-center justify-between
```

và render toàn bộ page number trong một hàng.

Có select:

```text
min-w-[70px]
```

Tab/header/action cũng có nhiều horizontal padding.

### Vấn đề

Trong dashboard mobile:

```text
viewport
- dashboard page padding
- card padding
```

có thể nhỏ hơn `250px`.

Pagination có thể vỡ khi:

```text
nhiều page number
+
page size
+
text mô tả
+
prev/next
```

cùng nằm một hàng.

### Direction

- Mobile filter control: `w-full`, không bắt buộc `min-w-[250px]`.
- Pagination:
  - stack trên mobile;
  - hoặc wrap;
  - chỉ hiển thị window page numbers thay vì toàn bộ `1...N`;
  - Prev / current / Next phải luôn reachable.
- Không làm mobile bằng cách giảm button xuống quá nhỏ.

---

## RC-06 — Account detail modal vẫn có base padding lớn

Cùng file:

```text
AccountManagement.tsx
```

Modal đã có điểm tốt:

```text
flex-col md:flex-row
w-full
max-w-4xl
max-h-[85vh]
```

Nhưng right content vẫn có:

```text
p-8
```

ngay từ mobile.

Header bên trong:

```text
flex items-start justify-between
```

có thể bị squeeze bởi:

```text
title dài
+
edit action
+
close
```

### Direction

Dùng responsive spacing:

```text
p-4 sm:p-6 md:p-8
```

Header action group:

```text
flex-col / wrap
```

khi cần.

Modal không được phụ thuộc vào desktop spacing ở 320–390px.

---

## RC-07 — Visit Management có nhiều fixed popover widths

File:

```text
frontend/pems-react/src/pages/dashboard/visit/VisitRequestManagement.tsx
```

Đã thấy:

```text
w-[190px]
w-[170px]
min-w-[210px]
min-w-[220px]
w-[280px]
w-max
whitespace-nowrap
shrink-0
```

Filter bar có `flex-wrap`, đây là điểm tốt, nhưng popover/dropdown vẫn có kích thước cứng.

### Vấn đề

Ví dụ:

```text
date popup = 280px
```

trong một card có padding trên viewport 320px có thể vượt vùng content.

`w-max` có thể lấy width theo label dài và chạy khỏi màn hình.

Absolute dropdown mặc định:

```text
left-0
```

có thể overflow bên phải nếu trigger nằm gần mép viewport.

### Direction

Popover phải được viewport-bound:

```text
max-width <= calc(100vw - gutter)
```

và có alignment động:

```text
left/right
```

hoặc trên mobile chuyển thành:

```text
full-width popover
bottom sheet
modal sheet
```

nếu control phức tạp.

---

## RC-08 — Visit Request V2 Modal dùng `96vh`

File:

```text
frontend/pems-react/src/features/visit-request/components/v2/VisitRequestV2Modal.tsx
```

Hiện có:

```text
h-[96vh]
max-w-[1400px]
```

### Điểm tốt hiện tại

Modal đã có:

```text
w-full
p-2 sm:p-4
grid rows header/body/footer
body overflow-y-auto
```

Đây là pattern đúng về cấu trúc.

### Rủi ro còn lại

`96vh` trên mobile có thể không theo visible viewport khi browser chrome/keyboard thay đổi.

### Direction

Dùng dynamic viewport / max-height clamp.

Ví dụ concept:

```text
height: calc(100dvh - mobile gutter)
max-height: ...
```

Footer phải:

- luôn reachable;
- không bị keyboard che;
- stack buttons trên màn hình nhỏ nếu thiếu width.

---

## RC-09 — Assign Host Modal dùng `88vh`

File:

```text
frontend/pems-react/src/components/modals/AssignHostModal.tsx
```

Hiện:

```text
max-h-[88vh]
max-w-lg
p-4
```

Header:

```text
flex items-center justify-between
```

Candidate card chứa:

```text
name
self-host badge
leader badge
current-host badge
email
department
schedule conflict
```

và một số text dùng:

```text
truncate
```

### Vấn đề

Candidate có nhiều badge/status có thể cạnh tranh cùng một dòng trên mobile.

`truncate` không được dùng để che thông tin quan trọng nếu không có cách đọc full content.

### Direction

- candidate heading phải `flex-wrap`;
- badge có thể xuống dòng;
- email/department dùng `break-words` / `overflow-wrap:anywhere`;
- chỉ truncate text secondary khi user vẫn có cách xem đầy đủ;
- max-height chuyển theo `dvh`.

---

## RC-10 — Một số responsive pattern tốt đã tồn tại

Không được redesign toàn bộ chỉ vì feedback mobile.

Ví dụ Visit Request V2 đã dùng:

```tsx
grid grid-cols-12
```

với:

```text
col-span-12
lg:col-span-*
```

Tức là mobile đã stack một cột.

Đây là pattern tốt cần giữ và áp dụng nhất quán.

---

## RC-11 — Hệ thống chưa có responsive regression guard riêng

`package.json` hiện đã có:

```text
lint
test:unit
audit:dark
audit:form-typography
test:e2e
test:e2e:realstack
```

Nhưng chưa có responsive-specific audit/gate.

### Direction

Bổ sung:

```text
audit:responsive
```

và Playwright viewport coverage.

---

# 5. ROOT CAUSE Ở CẤP KIẾN TRÚC UI

Các lỗi responsive hiện tại không nên được xem là từng bug độc lập.

Các nhóm nguyên nhân chính:

```text
RC-A: fixed width / min-width quá lớn
RC-B: fixed vh / h-screen trên mobile
RC-C: flex row không wrap/stack
RC-D: grid desktop được dùng ở base breakpoint
RC-E: absolute/fixed element không clamp theo viewport
RC-F: dropdown/popover có width theo content
RC-G: text truncate/nowrap đặt sai semantic
RC-H: table không có mobile strategy
RC-I: modal không có mobile viewport/keyboard strategy
RC-J: button/input group không có wrapping contract
RC-K: ảnh/chart/editor có intrinsic width làm nở container
RC-L: thiếu min-w-0 trên flex/grid child
RC-M: z-index không có scale chung
RC-N: responsive đang được xử lý theo từng page thay vì shared contract
RC-O: thiếu automated viewport regression
```

---

# 6. RESPONSIVE CONTRACT TOÀN HỆ THỐNG

PEMS phải theo mobile-first.

## Tailwind breakpoint convention

Giữ Tailwind breakpoint chuẩn trừ khi repo có config khác:

```text
base    < 640px
sm      >= 640px
md      >= 768px
lg      >= 1024px
xl      >= 1280px
2xl     >= 1536px
```

## Rule

Base class phải hoạt động được ở mobile.

Không viết desktop layout ở base rồi cố “undo” trên mobile.

### Tốt

```tsx
<div className="flex flex-col gap-3 sm:flex-row">
```

### Không tốt

```tsx
<div className="flex flex-row ... max-sm:flex-col">
```

Mobile-first dễ audit và ít regression hơn.

---

# 7. CONTAINER CONTRACT

Mọi page/container cần review:

```text
w-full
min-w-0
max-width hợp lý
responsive padding
```

Recommended pattern:

```text
w-full min-w-0
px-3 sm:px-4 md:px-6
```

Không thêm:

```text
min-width cố định lớn
```

trên page/container nếu không thật sự cần.

---

# 8. FLEX CONTRACT

Mọi horizontal group phải trả lời:

> Khi không đủ width thì nó làm gì?

Một trong các đáp án hợp lệ:

```text
wrap
stack
scroll inside own component
collapse
move into overflow menu
```

Không chấp nhận:

```text
"nó cứ squeeze đến khi vỡ"
```

### Button group

Recommended:

```text
flex flex-col gap-2 sm:flex-row sm:flex-wrap
```

hoặc:

```text
flex flex-wrap gap-2
```

### Action bar

Primary action trên mobile có thể:

```text
w-full sm:w-auto
```

---

# 9. GRID CONTRACT

Không để base:

```text
grid-cols-2
grid-cols-3
grid-cols-4
```

cho các form/card có nội dung dài nếu chưa chứng minh 320px vẫn an toàn.

Recommended:

```text
grid-cols-1
sm:grid-cols-2
lg:grid-cols-3
```

Form phức tạp có thể tiếp tục dùng:

```text
grid-cols-12
col-span-12
md/lg:col-span-*
```

như Visit Request V2.

---

# 10. `min-w-0` CONTRACT

Đây là một trong các fix quan trọng nhất cho Tailwind flex/grid UI.

Trong flex/grid:

```text
text child
input wrapper
card body
table-cell wrapper
title area
```

cần `min-w-0` khi phải co theo parent.

Nếu thiếu:

```text
min-width:auto
```

có thể khiến child không chịu shrink và đẩy viewport rộng ra.

Audit toàn hệ thống cho các flex child chứa:

```text
email
URL
organization name
visit name
partner name
campus name
Vietnamese/English labels
```

---

# 11. TEXT RESPONSIVE CONTRACT

Mục tiêu:

```text
Không crop thông tin quan trọng.
Không để long token làm nở layout.
Không giảm font vô lý để nhét nội dung.
```

---

## 11.1 Normal text

Dùng:

```text
break-words
```

khi phù hợp.

---

## 11.2 Email / URL / code / token

Có thể không có dấu cách.

Dùng utility tương đương:

```css
overflow-wrap: anywhere;
word-break: break-word;
```

Không để email dài ép card rộng hơn viewport.

---

## 11.3 `truncate`

Chỉ dùng nếu:

1. text thật sự secondary;
2. việc cắt không làm mất thông tin quan trọng;
3. có cách xem full text khi cần.

Không dùng `truncate` chỉ để “hết overflow”.

---

## 11.4 `whitespace-nowrap`

Mỗi occurrence phải audit.

Giữ cho:

```text
small badge
short date
small button label
```

nếu parent có khả năng wrap.

Không dùng để ép:

```text
long title
long organization
long notification
long nav label
```

trong vùng chật.

---

## 11.5 Font size

Không fix responsive bằng:

```text
text-[9px]
text-[10px]
```

cho content quan trọng.

Guideline:

```text
body / input / button: ~14px trở lên khi có thể
metadata: 12px trở lên
touch action label: phải dễ đọc
```

Các badge nhỏ có thể thấp hơn nhưng không dùng cho content chính.

---

# 12. BUTTON CONTRACT

Mobile touch target nên hướng tới tối thiểu:

```text
44 x 44 CSS px
```

Icon-only button:

```text
min-h-11
min-w-11
```

hoặc hit area tương đương.

Không để:

```text
icon 16px
padding 2px
```

thành target duy nhất trên mobile.

---

## Button text

Không để 3–5 action button dài cùng một hàng.

Giải pháp:

```text
wrap
stack
primary + overflow menu
```

Không giảm font để nhét.

---

# 13. INPUT / SELECT / TEXTAREA CONTRACT

Mọi field:

```text
w-full
min-w-0
```

trong mobile layout.

Không dùng fixed width nếu field không phải control cực ngắn.

### Field group

Desktop:

```text
label + field + action
```

Mobile:

```text
stack
```

nếu không đủ width.

### Input action

Ví dụ:

```text
input + scan button
input + clear button
input + verify button
```

phải test ở 320px.

---

# 14. REACT-SELECT / COMBOBOX CONTRACT

PEMS dùng `react-select`.

Audit:

```text
menu
menuPortal
control
valueContainer
singleValue
input
placeholder
option
```

Mobile requirements:

- control `w-full min-w-0`;
- selected value không làm control nở;
- menu không rộng hơn viewport;
- menu không bị modal `overflow-hidden` cắt;
- keyboard mở vẫn chọn option được;
- menuPortal z-index đúng khi nằm trong modal.

---

# 15. DROPDOWN / POPOVER CONTRACT

Đây là vùng có rủi ro rõ trong Visit Management.

Không để:

```text
w-max
min-w-[220px]
left-0
```

mà không clamp viewport.

Popover phải có:

```text
max-width: calc(100vw - gutters)
```

và một trong:

```text
align-left
align-right
dynamic positioning
mobile full-width
bottom-sheet
```

Date picker/popover phải test đặc biệt.

---

# 16. MODAL CONTRACT

Mọi modal toàn hệ thống phải theo cùng contract.

## Overlay

```text
fixed inset-0
p-2 sm:p-4
```

có safe-area khi cần.

## Modal

```text
w-full
max-w-*
max-h based on dvh
min-w-0
overflow-hidden
```

## Body

```text
overflow-y-auto
min-h-0
```

## Footer

Action phải luôn reachable.

Mobile:

```text
flex-col
```

hoặc wrap nếu label dài.

Desktop:

```text
sm:flex-row
```

---

## Không dùng cố định `vh` mà không test mobile

Audit:

```text
h-[96vh]
max-h-[88vh]
max-h-[85vh]
h-screen
max-h-screen
```

Ưu tiên dynamic viewport.

---

# 17. MOBILE KEYBOARD CONTRACT

Test form với virtual keyboard mở.

Đặc biệt:

```text
modal
OTP
visit request
search
email compose
profile edit
account create/edit
agenda/minutes
```

Khi keyboard mở:

- focused field phải còn nhìn thấy;
- page/modal phải scroll tới field;
- submit/footer không được khóa mất content;
- dropdown không bị nằm sau keyboard;
- không jump layout vô hạn.

---

# 18. SAFE AREA

Với thiết bị notch / home indicator:

Audit fixed elements:

```text
top header
bottom footer/action bar
drawer
full-screen modal
toast
```

Có thể dùng:

```css
env(safe-area-inset-top)
env(safe-area-inset-bottom)
```

ở shell phù hợp.

Không cần áp safe-area vào mọi component.

---

# 19. TABLE RESPONSIVE STRATEGY

Không có một cách duy nhất cho mọi table.

Phải classify.

---

## TYPE T1 — Table đơn giản

Có ít cột.

Có thể responsive bằng:

```text
column hide/reorder hợp lý
```

nhưng không làm mất dữ liệu critical.

---

## TYPE T2 — Data table nghiệp vụ nhiều cột

Ví dụ:

```text
Account Management
Visit Management
Email
Reports
Audit logs
```

Có thể dùng:

```text
overflow-x-auto
```

TRÊN TABLE CONTAINER.

Không cho cả page scroll ngang.

Table có thể cần:

```text
min-w-max
```

nếu column thực sự cần width, nhưng scroll phải nằm trong table card.

---

## TYPE T3 — Mobile-friendly business list

Nếu người dùng mobile thao tác thường xuyên:

Desktop:

```text
table
```

Mobile:

```text
card/list
```

Có thể render alternate layout, nhưng:

> Business logic / allowedActions / status mapping phải dùng chung.

Không copy logic authorization/action vào hai component riêng.

Visit Management hiện đã có concept:

```text
RowVariant = desktop | mobile
```

Có thể tiếp tục theo pattern này nếu implementation dùng chung data/action builders.

---

# 20. PAGINATION RESPONSIVE

Không render vô hạn page button trên một hàng.

Recommended:

```text
Prev
1
...
current-1 current current+1
...
N
Next
```

hoặc mobile đơn giản:

```text
Prev
Page X / Y
Next
```

Layout:

```text
flex-col sm:flex-row
```

Page size selector có thể xuống dòng.

---

# 21. TABS RESPONSIVE

Tabs dài không được đè nhau.

Chọn một:

```text
horizontal scroll trong tab bar
wrap
dropdown trên mobile
```

Không:

```text
overflow-hidden làm mất tab
```

Nếu horizontal scroll:

- chỉ tab bar scroll;
- active tab phải auto-scroll vào view;
- có visual cue nếu cần.

---

# 22. CARD RESPONSIVE

Card phải:

```text
w-full
min-w-0
```

Header card:

```text
title
status
actions
```

trên mobile phải wrap/stack.

Không đặt:

```text
title + 4 badges + 3 buttons
```

cùng một hàng không wrap.

---

# 23. BADGE / CHIP RESPONSIVE

Badge:

```text
inline-flex
max-w-full
```

Long status/detail có thể wrap.

Không đặt `whitespace-nowrap` cho badge có nội dung dài bất định.

Group badge:

```text
flex flex-wrap gap-1/2
```

---

# 24. IMAGE / MEDIA CONTRACT

Audit:

```text
img
video
iframe
canvas
three.js
gallery
avatar
news image
```

Default:

```text
max-w-full
h-auto
object-cover/object-contain
```

Không để intrinsic width đẩy container.

Fixed avatar/logo dimensions vẫn có thể hợp lệ nếu parent cho phép.

---

# 25. CHART / REPORT RESPONSIVE

PEMS dùng `recharts`.

Audit toàn chart:

- không hardcode width desktop;
- ưu tiên `ResponsiveContainer`;
- legend không đè chart;
- axis labels không thành unreadable;
- mobile có thể giảm tick count, không giảm font vô lý;
- report table có internal horizontal scroll nếu cần.

---

# 26. QUILL / RICH TEXT RESPONSIVE

PEMS có Quill.

Audit:

```text
toolbar
editor
preview
email HTML
news body
```

Toolbar trên mobile:

- được wrap hoặc scroll riêng;
- không làm page overflow.

Rich content:

```text
images max-width: 100%
tables internal scroll
long URLs wrap
```

Không ép toàn rich content `overflow:hidden`.

---

# 27. EMAIL HTML

`index.css` hiện đã có:

```text
.pems-email-body {
  overflow-x: auto;
}
```

và:

```text
.pems-email-body table {
  max-width: 100%;
}
```

Đây là một ví dụ đúng về **localized overflow**.

Giữ nguyên nguyên tắc:

> wide email/table scroll trong vùng của chính nó, không ép cả page.

---

# 28. HEADER / NAVIGATION RESPONSIVE

## Public Header

Audit:

```text
320
360
390
430
640
768
1024
1280
1366
1536
```

### Acceptance

- logo không đè action;
- search/menu icon không mất;
- nav desktop chỉ render khi thật sự đủ width;
- không crop label quan trọng;
- language control không đè profile;
- dropdown không ra khỏi viewport.

---

# 29. DASHBOARD TOP BAR RESPONSIVE

File:

```text
DashboardLayout.tsx
```

Mobile bar hiện có:

```text
lg:hidden
h-16
```

Audit:

```text
dashboard title
notification bell
logo
menu icon
```

với:

```text
320px
long translation
large font setting
```

Nếu thiếu width:

- title cho phép truncate chỉ khi vẫn có accessible name;
- logo có thể shrink;
- actions vẫn giữ touch target.

---

# 30. SIDEBAR / DRAWER RESPONSIVE

Mobile:

- width clamp viewport;
- full height theo dynamic viewport;
- nav area scroll độc lập;
- profile/logout luôn reachable;
- close button không bị notch che;
- overlay phủ đúng;
- body phía sau không scroll.

Desktop:

- collapsed / expanded không đè main content;
- transition không tạo horizontal scrollbar.

---

# 31. TOAST RESPONSIVE

Global Toaster hiện nằm:

```text
top-right
top: 96
z-index: 9999
```

Audit mobile:

- toast width không vượt viewport;
- không che menu/header quá nhiều;
- multi-line message đọc được;
- không chặn CTA lâu;
- không chồng modal action.

Có thể điều chỉnh theo viewport nếu cần.

---

# 32. Z-INDEX SCALE

Hiện code có nhiều lớp:

```text
z-30
z-40
z-50
z-[60]
z-[90]
z-[100]
z-[110]
z-[120]
z-9999
```

Không nhất thiết tất cả đang sai, nhưng phải audit.

Tạo layering contract:

```text
base
sticky
header
drawer
dropdown/popover
modal
nested modal
toast
```

Không để mỗi feature tự phát minh z-index vô hạn.

---

# 33. ABSOLUTE / FIXED POSITION AUDIT

Repo-wide tìm:

```text
absolute
fixed
sticky
inset-
top-
right-
bottom-
left-
translate-
```

High risk:

```text
absolute action button trên card
badge đặt top/right
floating button
sticky footer
fixed toolbar
dropdown
tooltip
```

Mobile phải đảm bảo:

- anchor không biến mất;
- element không che text;
- đủ room cho action;
- no off-screen position.

---

# 34. SEARCH / FILTER BAR CONTRACT

Filter bars là khu vực lỗi phổ biến.

Desktop:

```text
search + dropdowns + reset
```

Mobile:

```text
search full width
filters wrap/stack
reset accessible
```

Không yêu cầu mọi filter nằm một hàng.

Có thể dùng:

```text
Filter button
→ opens filter sheet/modal
```

trên mobile nếu số filter quá nhiều.

Visit Management hiện có rất nhiều filter; đây là candidate tốt cho mobile filter sheet nếu wrapping vẫn tạo UI xấu.

---

# 35. RESPONSIVE FORM ACTION FOOTER

Các form dài:

```text
Visit Request
Create/Edit News
Email
Partner
Account
Agenda
Minutes
```

Footer/action bar phải:

- không che field cuối;
- không bị keyboard che;
- button wrap/stack;
- disabled/loading state không thay width làm layout jump.

---

# 36. LONG CONTENT TEST DATA

Không test bằng text đẹp/ngắn בלבד.

Bắt buộc test:

```text
Tên 80–150 ký tự
Organization 200 ký tự
Email dài
URL dài
Campus/department dài
English translation dài hơn Vietnamese
Multi-status badges
10+ action/filter options
Long error message
Long toast
Long notification
Long filename
Long gallery title
Long agenda item
```

Responsive chỉ được xem là pass khi chịu được content thực tế/xấu.

---

# 37. VI / EN RESPONSIVE

Visitor/public có bilingual UI.

Phải test cả:

```text
VI
EN
```

English thường dài hơn Vietnamese ở một số CTA/menu.

Không được chỉ pass tiếng Việt.

---

# 38. ZOOM / TEXT RESIZE

Test:

```text
Browser zoom 125%
150%
200%
```

Mục tiêu:

- không mất chức năng;
- action vẫn reachable;
- text không overlap.

Data table có thể cần internal horizontal scroll ở 200%.

Không bắt buộc desktop layout giữ nguyên khi zoom; nó có thể reflow như viewport nhỏ hơn.

---

# 39. DEVICE / VIEWPORT TEST MATRIX

## Phone portrait

Bắt buộc:

```text
320 x 568
360 x 800
375 x 667
390 x 844
393 x 852
412 x 915
430 x 932
```

## Phone landscape

```text
667 x 375
844 x 390
852 x 393
915 x 412
932 x 430
```

## Small tablet / tablet

```text
540 x 720
600 x 960
768 x 1024
800 x 1280
820 x 1180
834 x 1194
1024 x 768
```

## Laptop / Desktop

```text
1024 x 768
1280 x 720
1366 x 768
1440 x 900
1536 x 864
1920 x 1080
```

Không cần test mỗi model vật lý nếu viewport-equivalent đã cover, nhưng critical flow nên test trên ít nhất một Android và một iOS browser thật nếu có thiết bị.

---

# 40. ORIENTATION TEST

Trên mobile/tablet:

```text
portrait → landscape
landscape → portrait
```

khi:

```text
modal đang mở
dropdown đang mở
form đang nhập
table đang scroll
sidebar đang mở
```

UI phải tự reflow.

Không require refresh.

---

# 41. PLAYWRIGHT RESPONSIVE REGRESSION

Project đã có:

```text
@playwright/test
npm run test:e2e
```

Bổ sung responsive spec.

Ví dụ:

```text
e2e/responsive/
```

hoặc theo cấu trúc test hiện tại.

Test tối thiểu:

```text
mobile 320
mobile 390
tablet 768
desktop 1366
```

---

# 42. AUTOMATED OVERFLOW ASSERTION

Tạo helper Playwright kiểm tra page-level horizontal overflow.

Concept:

```js
const hasHorizontalOverflow = await page.evaluate(() => {
  return document.documentElement.scrollWidth >
         document.documentElement.clientWidth + 1;
});
expect(hasHorizontalOverflow).toBe(false);
```

## Ngoại lệ

Không áp assertion trực tiếp vào:

```text
intentional table scroller
email body scroller
code/pre viewer
```

Page root vẫn không được overflow.

---

# 43. ELEMENT BOUNDS ASSERTION

Critical CTA có thể test:

```text
boundingBox.left >= 0
boundingBox.right <= viewportWidth
```

Đặc biệt:

```text
submit
close
create
approve
reject
save
filter
pagination
sidebar close
modal actions
```

---

# 44. SCREENSHOT REGRESSION

Tạo screenshot cho representative pages:

```text
public home
visit list
visit create
account management
profile
gallery
email
minutes
admin
```

ở:

```text
390
768
1366
```

Không cần screenshot mọi route trong CI nếu quá nặng.

Dùng representative layout archetype + functional overflow assertions cho toàn route matrix.

---

# 45. `audit:responsive`

Đề xuất thêm:

```json
"audit:responsive": "node scripts/audit-responsive.mjs"
```

Script scan high-risk patterns.

---

# 46. HIGH-RISK PATTERNS CHO AUDIT SCRIPT

Flag candidate:

```text
h-screen
max-h-screen
100vh
h-[*vh]
max-h-[*vh]

w-[*px]
min-w-[*px]
max-w-[*px]

w-max
min-w-max

whitespace-nowrap
truncate

overflow-hidden
overflow-x-hidden

grid-cols-2
grid-cols-3
grid-cols-4
grid-cols-5
grid-cols-6

fixed
absolute

left-[...]
right-[...]
top-[...]
bottom-[...]

text-[9px]
text-[10px]
text-[11px]
```

Script **không tự coi tất cả là bug**.

Output phải là candidate audit list.

---

# 47. SEARCH COMMANDS ĐỀ XUẤT

Có thể dùng `rg`:

```bash
rg -n "h-screen|max-h-screen|100vh|[0-9]+vh" frontend/pems-react/src

rg -n "w-\[[^\]]+\]|min-w-\[[^\]]+\]|max-w-\[[^\]]+\]" frontend/pems-react/src

rg -n "w-max|min-w-max|whitespace-nowrap|truncate" frontend/pems-react/src

rg -n "overflow-hidden|overflow-x-hidden|overflow-x-auto" frontend/pems-react/src

rg -n "grid-cols-[2-9]" frontend/pems-react/src

rg -n "\bfixed\b|\babsolute\b|\bsticky\b" frontend/pems-react/src

rg -n "text-\[(9|10|11)px\]" frontend/pems-react/src

rg -n "<table|<thead|<tbody|<td|<th" frontend/pems-react/src
```

Sau đó phân loại semantic.

---

# 48. KHÔNG MASS REPLACE

Tuyệt đối không:

```text
w-[...] -> w-full toàn repo
overflow-hidden -> overflow-visible toàn repo
whitespace-nowrap -> whitespace-normal toàn repo
grid-cols-2 -> grid-cols-1 toàn repo
h-screen -> h-dvh toàn repo
```

Mỗi occurrence phải được đánh giá context.

---

# 49. PHASE 1 — GLOBAL SHELL

Ưu tiên:

```text
App.tsx
index.css
DashboardLayout.tsx
Header.tsx
Sidebar.tsx
Footer.tsx
global modal/toast
```

Fix shell trước để page-level testing có nền đúng.

---

# 50. PHASE 2 — SHARED COMPONENTS

Audit:

```text
src/components/common/**
src/components/layout/**
src/components/modals/**
src/shared/**
src/features/**/components/shared/**
```

Ưu tiên:

```text
Modal shell
FormField
Input
Textarea
Select
CountrySelect
OrganizationCombobox
PhoneField
Date picker
Action menu
Pagination
Table wrapper
Card
Alert
Toast
Tooltip
Popover
```

Một shared fix đúng có thể giải quyết hàng chục màn.

---

# 51. PHASE 3 — HIGH-RISK COMPONENT TYPES

Theo thứ tự:

```text
1. modal / drawer
2. table / pagination
3. filters
4. long forms
5. cards
6. action bars
7. dropdown / popover
8. rich text
9. chart/report
10. media/gallery
```

---

# 52. PHASE 4 — FEATURE-BY-FEATURE AUDIT

Sau shared layer vẫn phải vào từng feature để tìm local override.

Checklist từng feature:

- [ ] 320px
- [ ] 390px
- [ ] 768px
- [ ] 1366px
- [ ] no page horizontal overflow
- [ ] no overlap
- [ ] text readable
- [ ] actions reachable
- [ ] form controls usable
- [ ] modal usable
- [ ] popup inside viewport
- [ ] long content
- [ ] loading/error/empty state
- [ ] VI/EN nếu feature bilingual

---

# 53. PHASE 5 — ROLE MATRIX

Responsive có thể khác theo role vì:

```text
menu khác
action khác
tab khác
filter khác
button khác
dashboard widgets khác
```

Bắt buộc test role-specific layout.

Ví dụ:

```text
Staff Leader có nhiều action hơn Staff
Admin có nhiều filter hơn Visitor
Visitor có EN/VI
HO có report/action khác
```

Một page pass ở Visitor không đồng nghĩa pass Staff Leader.

---

# 54. PHASE 6 — AUTOMATION / CI

Gate đề xuất:

```bash
npm run lint
npm run test:unit
npm run audit:dark
npm run audit:form-typography
npm run audit:responsive
npm run build
npm run test:e2e
```

Nếu real-stack phù hợp:

```bash
npm run test:e2e:realstack
```

---

# 55. MANUAL TEST BẮT BUỘC

Automation không thay thế visual/manual test.

Chrome DevTools:

```text
responsive mode
device emulation
orientation
touch
network throttle nếu cần
```

Test thêm browser thật nếu có.

---

# 56. KHÔNG SỬA RESPONSIVE BẰNG CÁC HACK SAU

## Không

```css
body {
  overflow-x: hidden;
}
```

để che overflow.

## Không

```text
scale(...)
zoom: ...
transform: scale(...)
```

để thu toàn UI.

## Không

```text
text-[9px]
```

chỉ để text vừa.

## Không

```text
display:none
```

cho data/action quan trọng chỉ vì mobile chật.

## Không

```text
JS detect userAgent = mobile
```

để quyết định layout.

Responsive nên dùng CSS/container/viewport first.

---

# 57. BUSINESS LOGIC OUT OF SCOPE

Task responsive KHÔNG được thay đổi:

```text
API
DTO
backend
database
authorization
permission
allowedActions
workflow
validation
status transition
business rules
query logic
email logic
notification logic
```

Nếu alternate mobile layout được tạo:

```text
cùng data
cùng permission
cùng action handler
cùng validation
```

Không clone business logic.

---

# 58. DO NOT BREAK DESKTOP

Responsive fix phải không regression desktop.

Mỗi component sửa mobile phải test lại:

```text
1024
1280
1366
1440
1920
```

Không chấp nhận:

```text
mobile đẹp nhưng desktop spacing/layout hỏng
```

---

# 59. VISUAL QUALITY REQUIREMENTS

“Responsive” không chỉ nghĩa là không overflow.

UI mobile phải:

- hierarchy rõ;
- spacing đều;
- card không quá dày;
- CTA dễ thấy;
- label/value dễ đọc;
- text line-height hợp lý;
- filter không chiếm cả màn hình vô tổ chức;
- modal không giống desktop bị thu nhỏ;
- action không thành một đống icon sát nhau;
- form flow theo một cột logic;
- table/list có cách đọc phù hợp.

---

# 60. RESPONSIVE ACCEPTANCE — NO PAGE OVERFLOW

Tại viewport 320px:

```text
documentElement.scrollWidth <= clientWidth + tolerance
```

cho mọi page, ngoại trừ overflow nằm bên trong component explicit scroller.

Không có page-level horizontal scrollbar.

---

# 61. RESPONSIVE ACCEPTANCE — NO CLIPPED CRITICAL CONTENT

Critical content gồm:

```text
page title
form label
form value
error
status
primary action
confirmation
account/visit identity
notification
required instruction
```

Không được bị clip/truncate không có cách đọc.

---

# 62. RESPONSIVE ACCEPTANCE — TOUCH

Mọi main interactive target trên mobile:

```text
button
icon action
close
menu
checkbox
radio
tab
pagination
```

phải dễ chạm.

Target nhỏ phải có padding/hit-area phù hợp.

---

# 63. RESPONSIVE ACCEPTANCE — FORMS

Mọi form:

- [ ] input không vượt viewport
- [ ] label wrap
- [ ] error wrap
- [ ] helper text wrap
- [ ] select menu usable
- [ ] keyboard không che field/action
- [ ] submit reachable
- [ ] button group stack/wrap
- [ ] date/time usable
- [ ] file upload usable
- [ ] Excel import panel usable

---

# 64. RESPONSIVE ACCEPTANCE — MODALS

Mọi modal:

- [ ] width <= viewport
- [ ] height <= visible dynamic viewport
- [ ] content scroll
- [ ] close reachable
- [ ] footer reachable
- [ ] nested confirm visible
- [ ] keyboard usable
- [ ] no background scroll
- [ ] safe area considered

---

# 65. RESPONSIVE ACCEPTANCE — TABLES

Mọi table:

- [ ] page itself không overflow
- [ ] table internal scroll hoặc mobile card strategy
- [ ] action column reachable
- [ ] status readable
- [ ] sticky cell không che cell khác
- [ ] long value không phá width
- [ ] header/body align

---

# 66. RESPONSIVE ACCEPTANCE — DROPDOWN

Mọi dropdown:

- [ ] nằm trong viewport
- [ ] option đọc được
- [ ] scroll khi option nhiều
- [ ] không bị parent overflow cắt
- [ ] z-index đúng
- [ ] keyboard/touch dùng được

---

# 67. RESPONSIVE ACCEPTANCE — LOADING / EMPTY / ERROR

Không chỉ test happy path.

Bắt buộc:

```text
loading skeleton
empty state
long error
server error
validation error
toast
alert banner
```

Các state này thường có text dài hơn và dễ gây overflow.

---

# 68. RESPONSIVE ACCEPTANCE — DYNAMIC DATA

Test:

```text
1 item
10 items
100+ items/pagination
0 item
multiple badges
multiple campus
multiple actions
long names
```

---

# 69. DEFINITION OF DONE — GLOBAL

Task chỉ được gọi DONE khi tất cả điều sau đạt.

- [ ] HEAD triển khai được ghi rõ.
- [ ] Repo-wide responsive pattern audit đã chạy.
- [ ] Global shell đã audit.
- [ ] Public Header/Footer đã audit.
- [ ] DashboardLayout đã audit.
- [ ] Sidebar mobile/desktop đã audit.
- [ ] Toast/overlay layering đã audit.
- [ ] Shared modals đã audit.
- [ ] Shared form controls đã audit.
- [ ] Shared dropdown/combobox/select đã audit.
- [ ] Table strategy đã audit.
- [ ] Pagination đã audit.
- [ ] Filter bars đã audit.
- [ ] Rich text đã audit.
- [ ] Charts/reports đã audit.
- [ ] Media/gallery đã audit.
- [ ] All public pages checked.
- [ ] All auth/identity pages checked.
- [ ] All dashboard features checked.
- [ ] All applicable roles checked.
- [ ] VI checked.
- [ ] EN checked where supported.
- [ ] 320px passes.
- [ ] 360/390px passes.
- [ ] 430px passes.
- [ ] phone landscape passes.
- [ ] 768px passes.
- [ ] 1024px passes.
- [ ] 1280/1366px passes.
- [ ] 1536/1920px passes.
- [ ] Browser zoom 150% passes.
- [ ] Browser zoom 200% has no loss of functionality.
- [ ] No page-level horizontal overflow.
- [ ] No critical content clipped.
- [ ] No overlapping controls.
- [ ] No button/input collision.
- [ ] No modal action unreachable.
- [ ] No dropdown off-screen.
- [ ] No mobile keyboard blocking critical action.
- [ ] No desktop regression.
- [ ] No business logic change.
- [ ] `npm run lint` passes.
- [ ] `npm run test:unit` passes.
- [ ] `npm run build` passes.
- [ ] responsive E2E passes.
- [ ] relevant existing E2E passes.
- [ ] responsive regression guard exists or equivalent CI check exists.

---

# 70. ROUTE/FEATURE REPORT BẮT BUỘC SAU TRIỂN KHAI

Báo cáo theo bảng:

| ID | Route/Feature | Component/File | Issue | Root cause | Fix | 320 | 390 | 768 | 1366 |
|---|---|---|---|---|---|---|---|---|---|
| RESP-001 | Dashboard shell | DashboardLayout.tsx | viewport clipping | h-screen + internal scroll | ... | PASS | PASS | PASS | PASS |
| RESP-002 | Visit filters | VisitRequestManagement.tsx | popover overflow | fixed width | ... | PASS | PASS | PASS | PASS |
| RESP-003 | Account filters | AccountManagement.tsx | filter overflow | min-w 250 | ... | PASS | PASS | PASS | PASS |

Không chỉ báo:

```text
"đã responsive"
```

---

# 71. EXCEPTION REPORT

Nếu vẫn giữ fixed width/nowrap/truncate:

Phải ghi:

```text
File:
Component:
Pattern:
Reason:
Why it is safe at 320px:
Test evidence:
```

Không được có exception ngầm.

---

# 72. PRIORITY FIX ORDER CHO CODE HIỆN TẠI

## P0

```text
DashboardLayout viewport/overflow
global modal viewport strategy
mobile keyboard
page-level overflow detection
```

## P1

```text
Header
Sidebar
Visit Management filters/popovers
Account Management filters/pagination/modal
shared table/pagination/filter patterns
```

## P2

```text
Visit Request V2 modal/form/cards
AssignHostModal
other shared modals
react-select/combobox/date picker
```

## P3

```text
remaining feature pages by role
reports/charts
rich text
gallery/media
```

## P4

```text
responsive E2E
audit:responsive
screenshot regression
final repo-wide sweep
```

---

# 73. IMPLEMENTATION PRINCIPLE

Không làm:

```text
User báo màn A lỗi
→ sửa màn A
→ đóng task
```

Phải làm:

```text
USER FEEDBACK
    ↓
LOCK LATEST DEV
    ↓
ROOT SHELL AUDIT
    ↓
SHARED COMPONENT AUDIT
    ↓
REPO-WIDE HIGH-RISK SEARCH
    ↓
FEATURE-BY-FEATURE AUDIT
    ↓
ROLE MATRIX
    ↓
VIEWPORT MATRIX
    ↓
AUTOMATED OVERFLOW TEST
    ↓
MANUAL VISUAL TEST
    ↓
REGRESSION GUARD
```

---

# 74. FINAL TARGET

Khi hoàn thành, PEMS phải có responsive behavior nhất quán:

```text
320px ────────────────> 1920px
 phone                  desktop

layout reflows
content remains readable
controls remain usable
actions remain reachable
no collisions
no hidden critical content
no page overflow
```

Không phải:

```text
desktop UI bị thu nhỏ để nhét vào phone
```

mà là:

```text
cùng chức năng
+
layout thích nghi đúng theo không gian
+
UI vẫn đẹp và dễ sử dụng
```

---

# 75. SCOPE LOCK

Task này xử lý:

```text
RESPONSIVE UI
LAYOUT REFLOW
OVERFLOW
TEXT READABILITY
TOUCH USABILITY
MOBILE/TABLET/DESKTOP CONSISTENCY
MODAL/DROPDOWN/TABLE RESPONSIVENESS
```

Không tự ý mở rộng sang:

```text
business workflow
permission
backend
database
API contract
status logic
notification business rules
new feature
major brand redesign
```

Nếu phát hiện bug khác:

```text
ghi nhận riêng
không trộn vào responsive task
```

---

# 76. KẾT LUẬN BẮT BUỘC CHO DEV

Đây là task **system-wide responsive normalization**, không phải một vài CSS patch.

Chỉ được báo hoàn thành khi:

1. toàn bộ frontend đã được audit;
2. shared/root causes đã được xử lý;
3. feature/role matrix đã được kiểm tra;
4. viewport 320px đến desktop đã có bằng chứng pass;
5. không còn page-level horizontal overflow;
6. critical UI không bị đè/cắt/khó đọc;
7. có regression protection để lỗi không quay lại.

