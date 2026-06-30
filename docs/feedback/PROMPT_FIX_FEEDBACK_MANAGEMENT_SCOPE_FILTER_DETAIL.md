# PROMPT FIX FEEDBACK MANAGEMENT — STAFF LEADER CAMPUS SCOPE, FILTER, DETAIL PAGE

Bạn là **Senior Full-stack Engineer** cho dự án **PEMS - Partnership Engagement Management System**.

## 1. Nhiệm vụ

Sửa hoàn chỉnh màn hình **Quản lý feedback** tại route:

```text
/dashboard/feedbacks
```

cho role **Staff Leader**.

## 2. Bối cảnh hiện tại

Tôi đang đăng nhập bằng **Staff Leader của campus Hòa Lạc / Hà Nội**.

UI hiện đã có:

- Trang **Quản lý feedback**.
- Summary cards.
- Search/filter.
- Tab **Tổng hợp theo đoàn**.
- Tab **Tất cả feedback**.

Nhưng hiện còn các lỗi:

1. Dữ liệu đang sai scope: đang hiển thị cả feedback/đoàn của campus khác như Cần Thơ, Đà Nẵng.
2. Phần **Bộ lọc nâng cao** mới chỉ hiện text: `Tính năng lọc nâng cao đang được phát triển...`, chưa làm thật.
3. Khi bấm icon mắt để xem chi tiết đoàn, trang detail chỉ hiện `{}` và chưa hiển thị dữ liệu thật.
4. Cần sửa để Staff Leader xem được tổng hợp feedback theo đoàn một cách dễ hiểu: đoàn nào, ai feedback, feedback ai, lúc nào, điểm bao nhiêu, comment gì, điểm từng tiêu chí ra sao.

---

## 3. Database liên quan

### 3.1. Bảng `feedbacks`

```text
feedback_id
visit_request_id
visit_instance_id
submitted_by_user_id
submitter_role ENUM('VISITOR','HOST','LOGISTICS')
submitter_context
submitter_name_snapshot
target_user_id
target_role ENUM('VISITOR','HOST','LOGISTICS')
target_context
target_name_snapshot
rating
comment
submitted_at
```

### 3.2. Bảng `feedback_rating_items`

```text
feedback_rating_item_id
feedback_id
criterion_code
criterion_label
rating
display_order
created_at
```

### 3.3. Bảng liên quan khác

```text
visit_requests
```

Dùng để lấy tên đoàn / thông tin request tổng.

```text
visit_request_campuses
```

Dùng để xác định campus của visit instance.

```text
feedbacks.visit_instance_id
→ visit_request_campuses.visit_instance_id
```

---

# YÊU CẦU 1 — Fix scope campus cho Staff Leader

Backend phải tự lấy `currentUser.primary_campus_id` từ token/session.

Không được để frontend truyền `campusId` để lọc scope chính.

Với Staff Leader, chỉ trả feedback thuộc campus mình quản lý.

Điều kiện scope bắt buộc:

```text
feedbacks.visit_instance_id phải join được sang visit_request_campuses
AND visit_request_campuses.campus_id = currentUser.primary_campus_id
```

Nếu `feedback.visit_instance_id IS NULL` thì không hiển thị cho Staff Leader, trừ khi project đã có business rule rõ cho feedback cấp request tổng.

Nếu chưa có rule rõ, mặc định chặn để tránh lộ dữ liệu campus khác.

Không hard-code campus Hòa Lạc/Hà Nội trong backend.

Hòa Lạc/Hà Nội chỉ là dữ liệu của user hiện tại. Backend phải dùng:

```text
currentUser.primary_campus_id
```

Frontend không hiển thị filter campus.

Header chỉ hiển thị badge read-only:

```text
Campus: {currentUser.primaryCampusName}
```

## Test bắt buộc

- Login Staff Leader Hòa Lạc/Hà Nội.
- Không thấy đoàn của Cần Thơ, Đà Nẵng, HCM, Quy Nhơn.
- Gọi trực tiếp API với `campusId` khác cũng không trả dữ liệu campus khác.
- Detail feedback của đoàn campus khác phải trả `403` hoặc `404` theo convention hiện tại.

---

# YÊU CẦU 2 — Sửa search/filter chính

Filter bar phải hoạt động thật, không chỉ UI.

## 2.1. Search input

Placeholder:

```text
Tìm theo tên đoàn, người đánh giá, người được đánh giá, nội dung...
```

Search phải tìm trong:

```text
tên đoàn / visit title / delegation name nếu backend resolve được
feedbacks.feedback_id
feedbacks.visit_request_id
feedbacks.visit_instance_id
feedbacks.submitter_name_snapshot
feedbacks.target_name_snapshot
feedbacks.submitter_context
feedbacks.target_context
feedbacks.comment
feedback_rating_items.criterion_label nếu join được
```

Search debounce:

```text
300-500ms
```

## 2.2. Dropdown mức độ

Các option:

```text
Tất cả mức độ
5 sao
4 sao
3 sao
1-2 sao
Dưới 3 sao
Cần chú ý rating <= 2
```

## 2.3. Dropdown chiều đánh giá

Các option enum thật:

```text
Tất cả chiều đánh giá
VISITOR → HOST
LOGISTICS → HOST
HOST → VISITOR
HOST → LOGISTICS
```

UI có thể hiển thị tiếng Việt:

```text
Khách đánh giá Host
Logistics đánh giá Host
Host đánh giá Khách
Host đánh giá Logistics
```

Nhưng API phải gửi enum thật, không gửi text tiếng Việt.

## 2.4. Dropdown người gửi

Các option:

```text
Tất cả người gửi
VISITOR
HOST
LOGISTICS
```

## 2.5. Date range

Cần thêm filter:

```text
Từ ngày
Đến ngày
```

Filter theo:

```text
feedbacks.submitted_at
```

Validate:

```text
fromDate <= toDate
```

Nếu `fromDate > toDate` thì hiện lỗi inline, không gọi API.

Cho phép chỉ nhập `fromDate` hoặc chỉ nhập `toDate`.

Format gửi API dạng ISO:

```text
yyyy-MM-dd
```

hoặc format hiện tại project đang dùng.

## 2.6. Buttons

Cần có:

```text
Lọc
Reset
```

Reset phải clear tất cả filter về mặc định và gọi lại API.

---

# YÊU CẦU 3 — Làm thật Bộ lọc nâng cao

Hiện tại phần này đang chỉ ghi:

```text
Tính năng lọc nâng cao đang được phát triển...
```

Hãy thay bằng form filter thật.

## 3.1. Các trường nâng cao

### Người đánh giá

Input search hoặc select nếu đã có API users.

Có thể lọc theo:

```text
submitted_by_user_id
submitter_name_snapshot
```

### Người được đánh giá

Input search hoặc select.

Có thể lọc theo:

```text
target_user_id
target_name_snapshot
```

### Submitter context

Input text.

Ví dụ:

```text
Host chính
Khách đại diện
Xe điện
Teabreak
```

### Target context

Input text.

### Visit request ID

Number input.

Validate số dương.

### Visit instance ID

Number input.

Validate số dương.

### Criterion code

Input hoặc dropdown lấy từ distinct `criterion_code` nếu API có.

Ví dụ:

```text
COMMUNICATION
SUPPORT_QUALITY
PUNCTUALITY
```

### Điểm tiêu chí

```text
criterionRatingFrom
criterionRatingTo
```

Validate:

```text
1 <= rating <= 5
criterionRatingFrom <= criterionRatingTo
```

### Có rating item chi tiết

Option:

```text
All
Có
Không
```

### Có comment

Option:

```text
All
Có
Không
```

## 3.2. Behavior của bộ lọc nâng cao

Bộ lọc nâng cao phải:

- Có nút **Áp dụng lọc nâng cao**.
- Có nút **Xóa lọc nâng cao**.
- Khi collapse/expand không làm mất giá trị đã nhập.
- Không làm vỡ layout.
- Desktop hiển thị dạng grid gọn.
- Mobile xếp dọc.

---

# YÊU CẦU 4 — Sửa tab “Tổng hợp theo đoàn”

Tab này là tab mặc định.

Mỗi dòng là một đoàn/campus instance đã có feedback.

## 4.1. Cột cần hiển thị

```text
STT
Đoàn khách / Phạm vi
Tổng FB
Điểm TB
Feedback mới nhất
Cảnh báo
Hành động
```

## 4.2. Cột “Đoàn khách / Phạm vi”

Hiển thị:

```text
Tên đoàn
REQ #visit_request_id
INST #visit_instance_id
Campus name nếu cần
```

Vì đã scope theo campus hiện tại nên campus chỉ hiển thị nhỏ, không cần filter.

## 4.3. Cột “Tổng FB”

Hiển thị tổng số feedback của đoàn/instance đó.

## 4.4. Cột “Điểm TB”

Hiển thị:

```text
average_rating dạng số 1 chữ số thập phân + star
```

## 4.5. Cột “Feedback mới nhất”

Hiển thị:

```text
latest_submitted_at
latest_submitter_name
role flow ngắn nếu có
```

## 4.6. Cột “Cảnh báo”

Nếu có feedback rating <= 2:

```text
Có X feedback thấp
```

Nếu không có:

```text
-
```

## 4.7. Cột “Hành động”

Icon mắt:

```text
Xem chi tiết đánh giá của đoàn
```

Không được hiển thị đoàn thuộc campus khác.

---

# YÊU CẦU 5 — Sửa tab “Tất cả feedback”

Tab này hiển thị từng dòng `feedbacks`.

## 5.1. Cột cần hiển thị

```text
STT
Feedback & Đoàn
Người đánh giá
Người được đánh giá
Chiều đánh giá
Điểm
Thời gian
Hành động
```

## 5.2. Cột “Feedback & Đoàn”

Hiển thị:

```text
FB #feedback_id
tên đoàn
comment preview 1-2 dòng
REQ #visit_request_id / INST #visit_instance_id
```

## 5.3. Cột “Người đánh giá”

Hiển thị:

```text
submitter_name_snapshot
submitter_role badge
submitter_context
User #submitted_by_user_id
```

## 5.4. Cột “Người được đánh giá”

Hiển thị:

```text
target_name_snapshot
target_role badge
target_context
User #target_user_id
```

## 5.5. Cột “Chiều đánh giá”

Hiển thị:

```text
VISITOR → HOST
```

Hoặc text tiếng Việt tương ứng.

## 5.6. Cột “Điểm”

Hiển thị:

```text
rating + star
```

Nếu `rating <= 2` thì dùng màu cảnh báo đỏ.

## 5.7. Cột “Thời gian”

Format:

```text
dd/MM/yyyy HH:mm
```

## 5.8. Cột “Hành động”

Icon mắt:

```text
Xem chi tiết một feedback
```

Không được hiển thị feedback thuộc campus khác.

---

# YÊU CẦU 6 — Làm trang/detail khi bấm mắt ở tab “Tổng hợp theo đoàn”

Hiện tại trang detail đang hiện:

```text
Chi tiết đang được xây dựng bằng API thật...
{}
```

Cần thay bằng UI thật.

## 6.1. Route detail

Khi bấm icon mắt ở dòng tổng hợp đoàn:

- Điều hướng hoặc mở page detail theo route hiện có.
- Nếu chưa có route tốt, dùng:

```text
/dashboard/feedbacks/visits/{visitRequestId}/instances/{visitInstanceId}
```

- Gọi API detail thật.
- Không hiển thị JSON raw `{}` cho user.

## 6.2. Header detail

Cần có:

```text
Nút quay lại
Title: Chi tiết đánh giá đoàn khách
Tên đoàn
REQ #visit_request_id
INST #visit_instance_id
Badge campus hiện tại
```

## 6.3. Summary cards

Cần có:

```text
Điểm trung bình
Số lượng feedback
Số feedback thấp rating <= 2
Feedback mới nhất
Người/nhóm được đánh giá nhiều nhất nếu backend có dữ liệu
```

## 6.4. Section “Tổng hợp theo chiều đánh giá”

Bảng nhỏ gồm:

```text
Chiều đánh giá
Số lượng
Điểm trung bình
Điểm thấp nhất
```

Các chiều:

```text
VISITOR → HOST
LOGISTICS → HOST
HOST → VISITOR
HOST → LOGISTICS
```

## 6.5. Section “Người/nhóm được đánh giá”

Bảng nhỏ gồm:

```text
Người được đánh giá
target_role
target_context
Số feedback
Điểm trung bình
Điểm thấp nhất
```

## 6.6. Section “Danh sách feedback cá nhân”

Mỗi feedback là một card.

Card phải hiển thị:

```text
submitter_name_snapshot → target_name_snapshot
submitter_role + submitter_context
target_role + target_context
submitted_at
rating tổng
comment
danh sách tiêu chí feedback_rating_items:
  - criterion_label
  - criterion_code
  - rating
```

Có nút:

```text
Xem đầy đủ
```

để mở modal chi tiết feedback.

---

# YÊU CẦU 7 — Modal chi tiết một feedback

Khi bấm mắt ở tab **Tất cả feedback** hoặc bấm **Xem đầy đủ** trong detail đoàn, mở modal chi tiết.

Modal phải hiển thị đầy đủ field DB.

## 7.1. Section “Thông tin feedback”

```text
feedback_id
visit_request_id
visit_instance_id
rating
comment
submitted_at
```

## 7.2. Section “Người gửi feedback”

```text
submitted_by_user_id
submitter_role
submitter_context
submitter_name_snapshot
```

## 7.3. Section “Người được đánh giá”

```text
target_user_id
target_role
target_context
target_name_snapshot
```

## 7.4. Section “Chiều đánh giá”

```text
submitter_role → target_role
text tiếng Việt tương ứng
```

## 7.5. Section “Tiêu chí chi tiết”

Từ `feedback_rating_items`:

```text
feedback_rating_item_id
criterion_code
criterion_label
rating
display_order
created_at
```

Nếu feedback không có rating item:

```text
Feedback này chưa có điểm theo tiêu chí.
```

---

# YÊU CẦU 8 — API đề xuất nếu hiện chưa đủ

Cập nhật hoặc tạo API theo pattern **Clean Architecture** hiện tại.

Controller chỉ gọi MediatR, không để logic query trong Controller.

## 8.1. API danh sách tổng hợp theo đoàn

```text
GET /api/feedbacks/visit-summary
```

Query params:

```text
q
ratingLevel
submitterRole
targetRole
roleFlow
submittedFrom
submittedTo
hasLowRating
submitterKeyword
targetKeyword
submitterContext
targetContext
visitRequestId
visitInstanceId
criterionCode
criterionRatingFrom
criterionRatingTo
hasRatingItems
hasComment
page
pageSize
sortBy
sortDir
```

Response item:

```text
visit_request_id
visit_instance_id
visit_title
campus_name
total_feedbacks
average_rating
latest_submitted_at
latest_submitter_name
low_rating_count
visitor_to_host_count
host_to_visitor_count
host_to_logistics_count
logistics_to_host_count
star_5_count
star_4_count
star_3_count
star_1_2_count
```

## 8.2. API danh sách từng feedback

```text
GET /api/feedbacks
```

Query params tương tự, nhưng trả từng feedback.

Response item:

```text
feedback_id
visit_request_id
visit_instance_id
visit_title
submitted_by_user_id
submitter_role
submitter_context
submitter_name_snapshot
target_user_id
target_role
target_context
target_name_snapshot
rating
comment_preview
submitted_at
rating_item_count
```

## 8.3. API detail theo đoàn/instance

```text
GET /api/feedbacks/visit-summary/{visitRequestId}/instances/{visitInstanceId}
```

Response:

```text
summary
role_flow_breakdown[]
target_breakdown[]
feedbacks[]
feedbacks[].rating_items[]
```

## 8.4. API detail một feedback

```text
GET /api/feedbacks/{feedbackId}
```

Response:

```text
feedback đầy đủ
rating_items[]
visit title/campus summary nếu có
```

Tất cả API trên phải enforce campus scope cho Staff Leader.

---

# YÊU CẦU 9 — Validate và security

1. Rating filter chỉ nhận `1-5`.
2. `submittedFrom <= submittedTo`.
3. `criterionRatingFrom/To` chỉ nhận `1-5`.
4. Không dùng string concat SQL gây SQL injection.
5. Search dùng parameterized query/LINQ an toàn.
6. Backend luôn là lớp chặn scope cuối cùng.
7. Frontend chỉ ẩn campus filter là chưa đủ; backend phải chặn dữ liệu campus khác.

---

# YÊU CẦU 10 — UI style

- Giữ phong cách enterprise dashboard hiện tại.
- Primary blue: `#004c91`.
- Orange: `#F37021` chỉ dùng để nhấn mạnh nếu cần.
- Card: `rounded-2xl border border-slate-200 bg-white shadow-sm`.
- Badge nhỏ gọn, dễ đọc.
- Không dùng gradient mạnh.
- Không làm horizontal scroll toàn trang.
- Table desktop từ `lg` trở lên.
- Mobile dùng card list.
- Action icon phải có `title` và `aria-label`.
- Không hiển thị JSON raw cho người dùng.
- Loading, empty, error state phải rõ.

---

# YÊU CẦU 11 — Clean code

1. Không dùng mock data.
2. Không hard-code campus Hòa Lạc/Hà Nội trong query backend.
3. Không hard-code role bằng text rải rác; dùng constants/enums hiện có.
4. Không refactor sâu ngoài module Feedback Management.
5. Không tạo bảng mới nếu schema đã đủ.
6. Không đổi business rule role flow.
7. Không dùng `any` bừa bãi trong TypeScript.
8. Không để TypeScript build lỗi.
9. Không để backend build lỗi.
10. Nếu API/field nào chưa chắc tên trong codebase, phải search source hiện tại trước khi sửa.

---

# YÊU CẦU 12 — Build/test

Sau khi sửa phải chạy:

```text
dotnet build
```

nếu sửa backend.

```text
npm run build
```

hoặc:

```text
pnpm build
```

nếu sửa frontend, theo project hiện tại.

## Manual test

1. Login Staff Leader campus Hòa Lạc/Hà Nội.
2. Vào `/dashboard/feedbacks`.
3. Kiểm tra không còn dữ liệu campus Cần Thơ/Đà Nẵng/HCM/Quy Nhơn.
4. Search theo tên đoàn hoạt động.
5. Search theo người đánh giá hoạt động.
6. Search theo người được đánh giá hoạt động.
7. Search theo comment hoạt động.
8. Filter mức đánh giá hoạt động.
9. Filter chiều đánh giá hoạt động.
10. Filter người gửi hoạt động.
11. Filter khoảng ngày `submitted_at` hoạt động.
12. Bộ lọc nâng cao hoạt động.
13. Reset filter hoạt động.
14. Tab Tổng hợp theo đoàn hiển thị đúng dữ liệu.
15. Tab Tất cả feedback hiển thị đúng dữ liệu.
16. Bấm mắt ở Tổng hợp theo đoàn mở detail thật, không còn `{}`.
17. Detail đoàn hiển thị ai feedback, feedback ai, thời gian nào, comment gì, rating item gì.
18. Bấm xem chi tiết một feedback mở modal đầy đủ field.
19. Gọi API trực tiếp với campus khác không trả dữ liệu.
20. Build frontend/backend pass.

---

# Kết quả cần báo cáo

Sau khi code xong, báo cáo rõ:

1. Root cause lỗi scope campus.
2. File backend đã sửa.
3. File frontend đã sửa.
4. API đã thêm/sửa.
5. UI đã thay đổi.
6. Scope Staff Leader được enforce ở đâu.
7. Cách test.
8. Build đã chạy chưa.
9. Những phần chưa làm được và lý do nếu có.

---

# Ghi chú quan trọng

Lỗi lớn nhất không phải UI, mà là backend chưa khóa scope theo:

```text
currentUser.primary_campus_id
```

Frontend có đẹp đến đâu mà API vẫn trả Cần Thơ/Đà Nẵng/HCM/Quy Nhơn thì vẫn sai phân quyền.
