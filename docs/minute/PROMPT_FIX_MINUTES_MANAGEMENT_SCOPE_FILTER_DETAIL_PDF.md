# PROMPT — Fix/Nâng cấp màn hình Quản lý biên bản cho Staff Leader theo campus scope, lọc/search đầy đủ, chi tiết đầy đủ và tải PDF

## 1. Bối cảnh

Bạn là **Senior Full-stack Engineer** cho dự án **PEMS — Partnership Engagement Management System**.

Tôi đang làm màn hình **Quản lý biên bản** tại route:

```text
/dashboard/minutes
```

Người dùng hiện tại là **Staff Leader của cơ sở Hòa Lạc / Hà Nội**.

UI hiện tại đã có:

- Trang danh sách biên bản.
- Search cơ bản.
- Bảng gồm: Tên biên bản, Đoàn khách, Thời gian, Hành động.
- Modal xem chi tiết có tên biên bản, ngày, danh sách người tham gia, ghi chú, đầu mục công việc.

Nhưng UI hiện tại còn thiếu:

1. Chưa khóa dữ liệu theo campus của Staff Leader.
2. Chưa hiển thị đầy đủ các thuộc tính trong bảng `minutes`.
3. Search/filter chưa đầy đủ.
4. Modal/detail chưa hiển thị đầy đủ field DB của `minutes`, `minute_participants`, `minute_action_items`.
5. Chưa có nút tải xuống biên bản dạng PDF.
6. Một số nội dung vẫn đang là mock hoặc chưa bám schema thật.

Mục tiêu: sửa thành màn hình quản lý biên bản dùng dữ liệu API thật, đúng scope campus, dễ tra cứu, xem đầy đủ thông tin, và có chức năng tải biên bản PDF.

---

## 2. Database cần bám sát

### 2.1. Bảng `minutes`

```text
minutes_id
visit_instance_id
title
content
status ENUM('DRAFT','SAVED')
edit_locked_by
edit_locked_at
edit_lock_expires_at
edit_lock_token
row_version
created_at
created_by
updated_at
updated_by
```

Ý nghĩa chính:

- Mỗi `visit_instance_id` chỉ có tối đa một biên bản.
- `content` là nội dung chính của biên bản.
- `status` gồm `DRAFT`, `SAVED`.
- Các field `edit_locked_*`, `edit_lock_token`, `row_version` phục vụ chống nhiều người cùng sửa / chống ghi đè.
- Khi visit instance đã `CLOSED`, quyền sửa biên bản phải bị khóa.

### 2.2. Bảng `minute_participants`

```text
minute_participant_id
minutes_id
user_id
guest_member_id
full_name_snapshot
role_snapshot
organization_snapshot
email_snapshot
attendance_status ENUM('PRESENT','ABSENT','EXCUSED')
attendance_note
checked_at
checked_by
display_order
created_at
```

Ý nghĩa chính:

- Lưu snapshot danh sách người tham gia biên bản.
- Một participant có thể là user nội bộ (`user_id`) hoặc khách trong đoàn (`guest_member_id`).
- Có điểm danh: `PRESENT`, `ABSENT`, `EXCUSED`.

### 2.3. Bảng `minute_action_items`

```text
action_item_id
minutes_id
title
note
due_date
status ENUM('TODO','IN_PROGRESS','DONE','CANCELLED')
completed_at
display_order
created_at
created_by
updated_at
updated_by
```

Ý nghĩa chính:

- Lưu các đầu việc phát sinh sau biên bản.
- Không gán người phụ trách trong schema hiện tại.
- Khi status chuyển sang `DONE`, backend phải tự set `completed_at`.

---

## 3. Yêu cầu scope cho Staff Leader campus Hòa Lạc / Hà Nội

Đây là yêu cầu quan trọng nhất.

### 3.1. Backend phải enforce scope

Không chỉ ẩn dữ liệu ở frontend. Backend phải là lớp chặn cuối cùng.

Với Staff Leader:

```text
currentUser.role_code = STAFF
currentUser.sub_role = LEADER
currentUser.primary_campus_id IS NOT NULL
```

Query danh sách biên bản bắt buộc join:

```text
minutes m
JOIN visit_request_campuses vrc ON m.visit_instance_id = vrc.visit_instance_id
```

Điều kiện bắt buộc:

```text
vrc.campus_id = currentUser.primary_campus_id
```

Không hard-code Hòa Lạc / Hà Nội trong backend. Hòa Lạc chỉ là dữ liệu hiện tại của user đăng nhập. Backend phải dùng `currentUser.primary_campus_id`.

### 3.2. Frontend

- Không hiển thị filter Campus.
- Không gửi `campusId` lên API để lọc scope chính.
- Header chỉ hiển thị badge read-only:

```text
Campus: {currentUser.primaryCampusName}
```

Ví dụ:

```text
Campus: FPT University Hà Nội / Hòa Lạc
```

### 3.3. Detail cũng phải kiểm tra scope

Khi gọi:

```text
GET /api/minutes/{minutesId}
```

Backend phải kiểm tra biên bản đó thuộc `currentUser.primary_campus_id`. Nếu không thuộc campus của Staff Leader hiện tại, trả `403` hoặc `404` theo convention hiện tại của project.

### 3.4. Export PDF cũng phải kiểm tra scope

Khi gọi export PDF:

```text
GET /api/minutes/{minutesId}/export-pdf
```

Backend cũng phải kiểm tra biên bản thuộc campus của Staff Leader hiện tại trước khi xuất PDF.

---

## 4. Yêu cầu UI danh sách biên bản

### 4.1. Header

Đổi subtitle thành:

```text
Tra cứu, xem chi tiết và tải xuống biên bản của các đoàn khách thuộc campus bạn quản lý
```

Hiển thị badge scope:

```text
Campus: {currentUser.primaryCampusName}
```

### 4.2. Summary cards

Thêm các card tổng quan phía trên bảng:

```text
Tổng biên bản
DRAFT
SAVED
Đang bị khóa sửa
Tổng action item chưa hoàn thành
Biên bản cập nhật gần nhất
```

Gợi ý dữ liệu:

- `total_minutes`
- `draft_count`
- `saved_count`
- `locked_count`
- `open_action_item_count`
- `latest_updated_at`

Nếu backend chưa có summary API, tạo API summary hoặc trả summary trong list response.

---

## 5. Search/filter đầy đủ

### 5.1. Search chính

Search input placeholder:

```text
Tìm theo tên biên bản, nội dung, đoàn khách, người tham gia, đầu việc...
```

Search phải tìm trong:

```text
minutes.minutes_id
minutes.title
minutes.content
minutes.visit_instance_id
visit request/delegation name nếu backend join được
minute_participants.full_name_snapshot
minute_participants.role_snapshot
minute_participants.organization_snapshot
minute_participants.email_snapshot
minute_action_items.title
minute_action_items.note
```

Search debounce 300–500ms.

### 5.2. Filter chính luôn hiển thị

Các filter chính:

```text
[Search] [Trạng thái biên bản] [Trạng thái điểm danh] [Trạng thái đầu việc] [Khoảng ngày] [Lọc] [Reset]
```

#### Trạng thái biên bản

```text
Tất cả trạng thái
DRAFT
SAVED
```

#### Trạng thái điểm danh

```text
Tất cả điểm danh
PRESENT
ABSENT
EXCUSED
```

Filter này tìm các biên bản có participant tương ứng.

#### Trạng thái đầu việc

```text
Tất cả đầu việc
TODO
IN_PROGRESS
DONE
CANCELLED
Chưa hoàn thành = TODO + IN_PROGRESS
```

#### Khoảng ngày

Cho người dùng chọn loại ngày cần lọc:

```text
Ngày tạo
Ngày cập nhật
Ngày khóa sửa
Deadline đầu việc
```

Và chọn:

```text
Từ ngày
Đến ngày
```

Validation:

- Cho phép chỉ nhập từ ngày hoặc chỉ nhập đến ngày.
- Nếu `fromDate > toDate`, hiện lỗi inline và không gọi API.
- Format gửi API theo convention hiện tại của project, ưu tiên `yyyy-MM-dd` hoặc ISO string.

### 5.3. Bộ lọc nâng cao

Hiện tại nếu chưa có, cần làm thật bằng collapsible section.

Các trường nâng cao:

```text
minutes_id
visit_instance_id
created_by
updated_by
edit_locked_by
lock status: All / Đang bị khóa / Không bị khóa / Lock đã hết hạn
row_version từ - đến
participant keyword
participant type: Internal user / Guest member / Unknown
checked_by
hasAbsentParticipants: All / Có / Không
hasExcusedParticipants: All / Có / Không
action item keyword
hasActionItems: All / Có / Không
hasOpenActionItems: All / Có / Không
hasOverdueActionItems: All / Có / Không
```

Nút trong filter nâng cao:

```text
Áp dụng lọc nâng cao
Xóa lọc nâng cao
```

Yêu cầu:

- Collapse/expand không làm mất giá trị đã nhập.
- Reset tổng phải clear cả filter chính và nâng cao.
- Desktop dùng grid gọn.
- Mobile xếp dọc.
- Không làm vỡ layout.

---

## 6. Bảng danh sách biên bản

Người dùng muốn hiển thị đầy đủ thuộc tính. Tuy nhiên không được làm vỡ layout toàn trang. Hãy thiết kế bảng desktop có đầy đủ field chính của `minutes`, còn các field dài có thể hiển thị dạng rút gọn/tooltip. Toàn bộ field phải có đầy đủ trong detail.

### 6.1. Cột bảng đề xuất

Bảng desktop từ `lg` trở lên. Nếu nhiều cột, cho phép scroll ngang **trong container bảng**, không để toàn trang horizontal scroll.

Cột bắt buộc:

```text
STT
Biên bản
Visit instance / Đoàn khách
Nội dung
Trạng thái
Khóa sửa
Participants
Action items
Audit
Hành động
```

### 6.2. Nội dung từng cột

#### Cột `Biên bản`

Hiển thị:

```text
minutes.title
MIN #minutes_id
status badge
```

#### Cột `Visit instance / Đoàn khách`

Hiển thị:

```text
visit_instance_id
tên đoàn / visit title nếu backend join được
thời gian đoàn diễn ra nếu backend join được từ visit_request_campuses hoặc visit_requests
host name nếu backend join được
```

Nếu không resolve được tên đoàn/host:

```text
INST #visit_instance_id
```

Không bịa dữ liệu.

#### Cột `Nội dung`

Hiển thị:

```text
content preview 2 dòng
```

Nếu null:

```text
Chưa có nội dung
```

#### Cột `Trạng thái`

Hiển thị:

```text
DRAFT / SAVED
row_version
```

#### Cột `Khóa sửa`

Hiển thị:

```text
edit_locked_by
edit_locked_at
edit_lock_expires_at
lock state: Đang khóa / Hết hạn / Không khóa
```

Không hiển thị full `edit_lock_token` trên bảng vì dài và nhạy cảm. Trong detail có thể hiển thị rút gọn hoặc chỉ hiển thị nếu cần debug/internal.

#### Cột `Participants`

Hiển thị summary:

```text
Tổng: X
Có mặt: A
Vắng: B
Có lý do: C
```

#### Cột `Action items`

Hiển thị summary:

```text
Tổng: X
TODO: A
IN_PROGRESS: B
DONE: C
CANCELLED: D
Quá hạn: E
```

#### Cột `Audit`

Hiển thị:

```text
created_at
created_by display name nếu có
updated_at
updated_by display name nếu có
```

#### Cột `Hành động`

Icon/button:

```text
Eye: Xem chi tiết
Download: Tải PDF
External/FileText: Mở bản xem PDF nếu có preview endpoint
```

Button phải có `title` và `aria-label`.

---

## 7. Mobile/tablet UI

Không ép bảng desktop lên mobile.

Mobile dùng card list. Mỗi card hiển thị:

```text
Title
MIN #minutes_id · INST #visit_instance_id
Status
Tên đoàn nếu có
Created/Updated date
Participant summary
Action item summary
Button: Xem chi tiết
Button: Tải PDF
```

---

## 8. Modal hoặc page chi tiết biên bản

Hiện modal detail đang thiếu nhiều field và còn giống mock. Cần sửa để hiển thị dữ liệu thật từ API.

Khi bấm icon mắt:

```text
GET /api/minutes/{minutesId}
```

Không dùng mock data.

### 8.1. Header detail

Hiển thị:

```text
Chi tiết biên bản cuộc họp
{minutes.title}
MIN #minutes_id · INST #visit_instance_id
Status badge: DRAFT/SAVED
Campus badge
```

Buttons:

```text
Tải PDF
Đóng
```

Nếu có quyền sửa và visit instance chưa CLOSED:

```text
Sửa biên bản
```

Nếu đang bị lock bởi người khác:

```text
Đang được chỉnh sửa bởi {editLockedByName} đến {edit_lock_expires_at}
```

### 8.2. Section `Thông tin biên bản`

Hiển thị đầy đủ field bảng `minutes`:

```text
minutes_id
visit_instance_id
title
content
status
edit_locked_by
edit_locked_by_name nếu có
edit_locked_at
edit_lock_expires_at
edit_lock_token rút gọn hoặc ẩn nếu không cần debug
row_version
created_at
created_by
created_by_name nếu có
updated_at
updated_by
updated_by_name nếu có
```

`content` phải hiển thị đầy đủ, không chỉ ghi chú mock. Nếu content là HTML/rich text, sanitize trước khi render.

### 8.3. Section `Thông tin đoàn / visit instance`

Backend nên resolve thêm từ `visit_request_campuses` và `visit_requests` nếu có thể:

```text
Tên đoàn khách
visit_request_id
visit_instance_id
Campus
Host chính
Thời gian đoàn diễn ra
Trạng thái visit instance
```

Nếu field nào không có trong API/codebase, không bịa. Hiển thị fallback:

```text
Chưa có dữ liệu
```

### 8.4. Section `Danh sách người tham gia / điểm danh`

Hiển thị bảng đầy đủ từ `minute_participants`:

```text
minute_participant_id
minutes_id
user_id
guest_member_id
full_name_snapshot
role_snapshot
organization_snapshot
email_snapshot
attendance_status
attendance_note
checked_at
checked_by
checked_by_name nếu có
display_order
created_at
```

UI gợi ý:

- Badge `PRESENT` màu xanh.
- Badge `ABSENT` màu đỏ/xám.
- Badge `EXCUSED` màu vàng.
- Nếu `user_id` có giá trị: badge `Internal`.
- Nếu `guest_member_id` có giá trị: badge `Guest`.
- Nếu cả hai null: badge `Snapshot only`.

Có summary đầu section:

```text
Tổng người tham gia
Có mặt
Vắng
Vắng có lý do
```

### 8.5. Section `Đầu mục công việc`

Hiển thị bảng đầy đủ từ `minute_action_items`:

```text
action_item_id
minutes_id
title
note
due_date
status
completed_at
display_order
created_at
created_by
created_by_name nếu có
updated_at
updated_by
updated_by_name nếu có
```

UI gợi ý:

- Badge `TODO`, `IN_PROGRESS`, `DONE`, `CANCELLED`.
- Nếu `due_date < now` và status chưa `DONE/CANCELLED`, hiển thị badge `Quá hạn`.
- Nếu không có action item, hiển thị empty state:

```text
Biên bản này chưa có đầu mục công việc.
```

### 8.6. Section `Audit & concurrency`

Hiển thị thông tin kỹ thuật dễ hiểu:

```text
Người tạo
Ngày tạo
Người cập nhật
Ngày cập nhật
Row version
Trạng thái lock
Người đang lock
Lock hết hạn
```

---

## 9. Chức năng tải xuống PDF

Người dùng muốn có nút tải xuống biên bản dạng PDF.

### 9.1. Frontend

Thêm nút:

```text
Tải PDF
```

Vị trí:

- Trong cột hành động ở bảng danh sách.
- Trong header modal/detail.

Khi click:

```text
GET /api/minutes/{minutesId}/export-pdf
```

Response expected:

```text
Content-Type: application/pdf
Content-Disposition: attachment; filename="minutes-{minutesId}.pdf"
```

Frontend phải tải file về máy.

Tên file gợi ý:

```text
bien-ban-{minutes_id}-{yyyyMMdd}.pdf
```

### 9.2. Backend

Tạo endpoint export PDF nếu chưa có:

```text
GET /api/minutes/{minutesId}/export-pdf
```

Controller chỉ gọi MediatR, không chứa logic tạo PDF.

Handler cần:

1. Load `minutes` theo `minutesId`.
2. Join `visit_request_campuses` để check campus scope.
3. Load `minute_participants` theo `minutes_id`, order by `display_order`.
4. Load `minute_action_items` theo `minutes_id`, order by `display_order`.
5. Resolve thêm:
   - tên đoàn
   - host
   - thời gian đoàn diễn ra
   - campus
   - created/updated user display names
   nếu codebase đã có entity/fields.
6. Generate PDF từ dữ liệu thật.
7. Trả file PDF.

Không lưu binary PDF vào database.

Nếu project đã có thư viện PDF/export sẵn thì dùng lại. Không tự thêm thư viện mới nếu không cần. Nếu chưa có thư viện PDF, báo rõ và dùng phương án phù hợp với project hiện tại sau khi kiểm tra dependency.

### 9.3. Nội dung PDF

PDF phải bao gồm:

```text
Tiêu đề biên bản
Thông tin đoàn / visit instance
Campus
Host
Thời gian đoàn diễn ra
Trạng thái biên bản
Nội dung biên bản
Danh sách người tham gia + điểm danh
Đầu mục công việc
Audit cơ bản: ngày tạo, người tạo, ngày cập nhật
```

Không đưa `edit_lock_token` vào PDF.

---

## 10. API đề xuất

Nếu API hiện tại chưa đủ, tạo hoặc cập nhật theo Clean Architecture hiện tại.

### 10.1. List API

```text
GET /api/minutes
```

Query params:

```text
q
status
attendanceStatus
actionItemStatus
dateType
fromDate
toDate
minutesId
visitInstanceId
createdBy
updatedBy
editLockedBy
lockState
rowVersionFrom
rowVersionTo
participantKeyword
participantType
checkedBy
hasAbsentParticipants
hasExcusedParticipants
actionItemKeyword
hasActionItems
hasOpenActionItems
hasOverdueActionItems
page
pageSize
sortBy
sortDir
```

Response item nên gồm:

```text
minutes_id
visit_instance_id
title
content_preview
status
edit_locked_by
edit_locked_by_name
edit_locked_at
edit_lock_expires_at
lock_state
row_version
created_at
created_by
created_by_name
updated_at
updated_by
updated_by_name
visit_title
visit_request_id
campus_name
host_name
planned_start_at
planned_end_at
participant_total
participant_present_count
participant_absent_count
participant_excused_count
action_item_total
action_item_todo_count
action_item_in_progress_count
action_item_done_count
action_item_cancelled_count
action_item_overdue_count
```

### 10.2. Detail API

```text
GET /api/minutes/{minutesId}
```

Response:

```text
minutes
visit_instance_summary
participants[]
action_items[]
audit_summary
lock_summary
```

Trong đó `participants[]` có đầy đủ field `minute_participants`.

`action_items[]` có đầy đủ field `minute_action_items`.

### 10.3. Export PDF API

```text
GET /api/minutes/{minutesId}/export-pdf
```

Trả file PDF.

---

## 11. Validation và security

1. Backend phải kiểm tra Staff Leader scope theo campus ở list, detail, export PDF.
2. Không dùng dữ liệu mock.
3. Không lộ biên bản campus khác.
4. Không để frontend truyền campusId để xem campus khác.
5. Search dùng parameterized query/LINQ an toàn, không string concat SQL.
6. Không render HTML content trực tiếp nếu chưa sanitize.
7. Không hiển thị full `edit_lock_token` ở table. Nếu cần debug trong detail thì rút gọn hoặc ẩn.
8. Không cho export PDF nếu user không có quyền xem biên bản.
9. Nếu visit instance đã `CLOSED`, không cho sửa biên bản.
10. Nếu update action item sang `DONE`, backend phải set `completed_at`.

---

## 12. UI style

Giữ phong cách enterprise dashboard hiện tại:

```text
Primary blue: #004c91
Primary orange: #F37021
Text chính: slate-800/slate-900
Text phụ: slate-500/slate-600
Card: rounded-2xl border border-slate-200 bg-white shadow-sm
Table header navy
Badge nhỏ gọn, màu nhẹ
```

Yêu cầu:

- Không dùng gradient mạnh.
- Không dùng shadow quá đậm.
- Không làm horizontal scroll toàn trang.
- Nếu bảng nhiều cột, scroll ngang chỉ nằm trong table container.
- Action icon phải có `title` và `aria-label`.
- Loading/empty/error state phải rõ.
- Mobile dùng card list.

---

## 13. Clean code yêu cầu

1. Không refactor sâu ngoài module Minutes Management.
2. Không đổi route/role logic nếu không cần.
3. Không dùng mock data.
4. Không hard-code campus Hòa Lạc/Hà Nội.
5. Không thêm bảng mới.
6. Không lưu PDF binary vào database.
7. Không tạo field mới nếu schema chưa có, trừ khi có SQL patch rõ ràng và được yêu cầu.
8. Không dùng `any` bừa bãi trong TypeScript.
9. Tách type/interface rõ ràng:

```text
MinutesListItem
MinutesDetail
MinuteParticipantDto
MinuteActionItemDto
MinutesFilterParams
MinutesStatus
AttendanceStatus
MinuteActionItemStatus
MinutesSummary
```

10. Tách API service/hook theo pattern hiện có:

```text
minutesApi.ts
useMinutes.ts hoặc query pattern hiện tại của project
```

11. Format date theo `dd/MM/yyyy HH:mm`.
12. Format empty/null rõ ràng: `Chưa có dữ liệu`.
13. Build không lỗi.

---

## 14. Build/test bắt buộc

Sau khi sửa:

```text
dotnet build
npm run build hoặc pnpm build theo project hiện tại
```

Manual test:

1. Login Staff Leader campus Hòa Lạc/Hà Nội.
2. Vào `/dashboard/minutes`.
3. Kiểm tra không thấy biên bản campus khác.
4. Search theo title hoạt động.
5. Search theo content hoạt động.
6. Search theo tên đoàn hoạt động.
7. Search theo participant hoạt động.
8. Search theo action item hoạt động.
9. Filter status `DRAFT/SAVED` hoạt động.
10. Filter attendance status hoạt động.
11. Filter action item status hoạt động.
12. Filter khoảng ngày hoạt động.
13. Bộ lọc nâng cao hoạt động.
14. Reset filter hoạt động.
15. Bảng hiển thị đầy đủ field chính của `minutes` và summary participants/action items.
16. Bấm mắt xem chi tiết mở dữ liệu thật, không còn mock.
17. Detail hiển thị đầy đủ field `minutes`.
18. Detail hiển thị đầy đủ participants.
19. Detail hiển thị đầy đủ action items.
20. Bấm tải PDF từ table tải được file PDF.
21. Bấm tải PDF trong detail tải được file PDF.
22. API detail biên bản campus khác bị chặn.
23. API export PDF biên bản campus khác bị chặn.
24. Frontend build pass.
25. Backend build pass.

---

## 15. Kết quả cần báo cáo sau khi code

Báo cáo theo format:

```text
1. Root cause UI/API cũ
2. File backend đã sửa
3. File frontend đã sửa
4. API đã thêm/sửa
5. Scope Staff Leader campus được enforce ở đâu
6. Search/filter đã làm những gì
7. Detail đã hiển thị những field nào
8. Export PDF hoạt động như thế nào
9. Manual test đã chạy
10. Build đã chạy
11. Phần chưa làm được và lý do nếu có
```
