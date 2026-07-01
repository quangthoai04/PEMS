# PEMS — Đề xuất UI & Prompt Code cho Feedback Management

> **Mục tiêu tài liệu:** mô tả lại UI quản lý feedback dựa trên database `feedbacks` và `feedback_rating_items`, giúp người dùng dễ xem tổng hợp feedback theo từng đoàn khách, lọc/search nhanh, xem rõ ai feedback ai, feedback thuộc đoàn nào, khi nào, nội dung gì và điểm theo từng tiêu chí.

---

## 1. Bối cảnh database

### 1.1. Bảng `feedbacks`

Bảng `feedbacks` lưu **một đánh giá giữa hai user trong một chuyến thăm**.

Mỗi dòng thể hiện:

```text
Ai gửi feedback
→ gửi cho ai
→ trong visit request / visit instance nào
→ vai trò của hai bên trong chuyến thăm
→ điểm tổng
→ comment
→ thời điểm gửi
```

Các field chính:

| Field | Ý nghĩa |
|---|---|
| `feedback_id` | ID feedback. |
| `visit_request_id` | Đơn/yêu cầu thăm tổng. |
| `visit_instance_id` | Campus visit instance, có thể null. |
| `submitted_by_user_id` | User gửi feedback. |
| `submitter_role` | Vai trò người gửi: `VISITOR`, `HOST`, `LOGISTICS`. |
| `submitter_context` | Ngữ cảnh người gửi, ví dụ: Host chính, Xe điện, Teabreak, Khách đại diện. |
| `submitter_name_snapshot` | Tên người gửi tại thời điểm gửi. |
| `target_user_id` | User được đánh giá. |
| `target_role` | Vai trò người được đánh giá: `VISITOR`, `HOST`, `LOGISTICS`. |
| `target_context` | Ngữ cảnh đối tượng được đánh giá. |
| `target_name_snapshot` | Tên người được đánh giá tại thời điểm gửi. |
| `rating` | Điểm tổng từ 1 đến 5. |
| `comment` | Nội dung feedback. |
| `submitted_at` | Thời điểm gửi feedback. |

Luồng role hợp lệ theo constraint:

```text
VISITOR   → HOST
LOGISTICS → HOST
HOST      → VISITOR
HOST      → LOGISTICS
```

---

### 1.2. Bảng `feedback_rating_items`

Bảng `feedback_rating_items` lưu **điểm chi tiết theo từng tiêu chí** của một feedback.

Ví dụ một feedback có điểm tổng 4 sao, bên dưới có các tiêu chí:

```text
COMMUNICATION      5 sao
SUPPORT_QUALITY    4 sao
PUNCTUALITY        3 sao
```

Các field chính:

| Field | Ý nghĩa |
|---|---|
| `feedback_rating_item_id` | ID dòng tiêu chí. |
| `feedback_id` | Feedback cha. |
| `criterion_code` | Mã tiêu chí. |
| `criterion_label` | Tên hiển thị tiêu chí. |
| `rating` | Điểm tiêu chí từ 1 đến 5. |
| `display_order` | Thứ tự hiển thị. |
| `created_at` | Thời điểm tạo dòng tiêu chí. |

Quan hệ:

```text
feedbacks.feedback_id
→ feedback_rating_items.feedback_id

1 feedback
→ nhiều rating items
```

---

## 2. Định hướng UI tổng thể

UI nên tách thành 2 lớp:

```text
Lớp 1: Tổng hợp feedback theo đoàn khách / visit
Lớp 2: Chi tiết từng feedback cá nhân trong đoàn đó
```

Lý do:

- Người quản lý thường muốn biết nhanh đoàn nào được đánh giá tốt/xấu.
- Sau đó mới cần xem sâu từng feedback cụ thể.
- Bảng `feedbacks` là dữ liệu dạng từng dòng đánh giá, nếu hiển thị thẳng tất cả ngay từ đầu thì người dùng khó nhìn tổng quan.

---

## 3. Vấn đề của UI hiện tại

UI hiện tại đang hiển thị đơn giản:

```text
STT | Tên đoàn khách | Trung bình đánh giá | Thời gian | Hành động
```

Cách này dễ nhìn nhưng thiếu nhiều thông tin quan trọng:

```text
- Feedback thuộc visit_request_id nào?
- Có visit_instance_id không?
- Ai là người gửi feedback?
- Người gửi thuộc vai trò nào?
- Người được đánh giá là ai?
- Người được đánh giá thuộc vai trò nào?
- Chiều đánh giá là Visitor → Host hay Host → Logistics?
- Comment cụ thể là gì?
- Có bao nhiêu tiêu chí đánh giá chi tiết?
- Feedback gửi lúc nào?
```

Vì vậy nên giữ màn tổng hợp nhưng bổ sung tab và modal/detail rõ hơn.

---

## 4. Đề xuất trang danh sách Feedback Management

### 4.1. Header

Tiêu đề:

```text
Quản lý feedback
```

Subtitle đề xuất:

```text
Tổng hợp và tra cứu đánh giá của các đoàn khách đã hoàn tất
```

Nếu màn này dùng cho Staff Leader campus Hà Nội, hiển thị badge scope:

```text
Campus: Hà Nội
```

Không cần dropdown lọc campus nếu backend đã scope theo campus của Staff Leader.

---

### 4.2. Summary cards

Thêm các card thống kê phía trên bảng:

```text
Tổng đoàn có feedback
Tổng số feedback
Điểm trung bình
Feedback 1–2 sao
Feedback mới nhất
Tỷ lệ có đánh giá chi tiết
```

Ví dụ:

```text
Tổng đoàn: 24
Tổng feedback: 86
Điểm trung bình: 4.2/5
Cảnh báo thấp: 5 feedback
Mới nhất: 24/10/2026
Có tiêu chí chi tiết: 78%
```

Card **Feedback 1–2 sao** nên nổi bật hơn vì đây là nhóm cần xử lý trước.

---

## 5. Search và filter

### 5.1. Filter chính luôn hiển thị

Thanh filter nên gồm:

```text
[Search] [Mức đánh giá] [Chiều đánh giá] [Vai trò người gửi] [Khoảng ngày] [Lọc] [Reset]
```

#### Search

Placeholder:

```text
Tìm theo tên đoàn, người đánh giá, người được đánh giá, nội dung...
```

Search nên tìm trong:

```text
visit/delegation name
submitter_name_snapshot
target_name_snapshot
submitter_context
target_context
comment
criterion_label
feedback_id
visit_request_id
visit_instance_id
```

#### Dropdown mức đánh giá

```text
Tất cả mức độ
5 sao
4 sao
3 sao
1–2 sao
Dưới 3 sao
Cần chú ý
```

Quy ước gợi ý:

```text
Cần chú ý = rating <= 2
```

#### Dropdown chiều đánh giá

Theo database chỉ có các chiều hợp lệ:

```text
Tất cả chiều đánh giá
VISITOR → HOST
LOGISTICS → HOST
HOST → VISITOR
HOST → LOGISTICS
```

UI tiếng Việt:

```text
Tất cả chiều đánh giá
Khách đánh giá Host
Logistics đánh giá Host
Host đánh giá Khách
Host đánh giá Logistics
```

#### Dropdown vai trò người gửi

```text
Tất cả người gửi
VISITOR
HOST
LOGISTICS
```

UI tiếng Việt:

```text
Tất cả người gửi
Khách
Host
Logistics
```

#### Khoảng ngày

Filter theo:

```text
feedbacks.submitted_at
```

Control:

```text
Từ ngày gửi feedback
Đến ngày gửi feedback
```

---

### 5.2. Bộ lọc nâng cao

Thêm nút collapse:

```text
Bộ lọc nâng cao
```

Khi mở ra, hiển thị:

```text
Người đánh giá
Người được đánh giá
Submitter context
Target context
Visit request ID
Visit instance ID
Có comment / Không có comment
Có rating item chi tiết / Không có rating item
Criterion code
Điểm tiêu chí từ - đến
```

Nếu màn này chỉ dành cho Staff Leader campus Hà Nội:

```text
Không hiển thị filter campus.
Backend tự scope theo currentUser.primary_campus_id.
```

---

## 6. Cấu trúc tabs

Trang danh sách nên có 2 tab:

```text
Tab 1: Tổng hợp theo đoàn
Tab 2: Tất cả feedback
```

Tab mặc định nên là **Tổng hợp theo đoàn**.

---

## 7. Tab 1 — Tổng hợp theo đoàn

### 7.1. Mục đích

Tab này giúp người dùng nhìn nhanh:

```text
- Đoàn nào có nhiều feedback.
- Đoàn nào điểm thấp.
- Đoàn nào có feedback mới.
- Feedback chủ yếu đến từ chiều nào.
```

### 7.2. Cột đề xuất

```text
Đoàn khách | Phạm vi visit | Tổng feedback | Điểm TB | Phân bố sao | Feedback mới nhất | Cảnh báo | Hành động
```

#### Cột “Đoàn khách”

Hiển thị:

```text
Tên đoàn khách
REQ #visit_request_id · INST #visit_instance_id nếu có
```

Ví dụ:

```text
Đoàn Sở Giáo Dục Vĩnh Phúc
REQ #102 · INST #501
```

#### Cột “Phạm vi visit”

Hiển thị nếu backend join được:

```text
Single-campus / Multi-campus
Campus Hà Nội
Trạng thái visit
```

#### Cột “Tổng feedback”

Hiển thị tổng số feedback:

```text
8 phản hồi
```

Có thể hiển thị breakdown nhỏ:

```text
Visitor → Host: 3
Host → Visitor: 2
Host → Logistics: 3
```

#### Cột “Điểm TB”

Hiển thị số + sao:

```text
4.2 ★★★★☆
```

Không nên chỉ hiển thị star, vì số điểm giúp đọc chính xác hơn.

#### Cột “Phân bố sao”

Hiển thị text hoặc mini bar:

```text
5★ 4 | 4★ 2 | 3★ 1 | 1–2★ 1
```

#### Cột “Feedback mới nhất”

```text
24/10/2026 14:30
Nguyễn T → Host chính
```

#### Cột “Cảnh báo”

Nếu có rating thấp:

```text
Có 2 feedback thấp
```

Nếu không có:

```text
Không có
```

#### Cột “Hành động”

Icon/action:

```text
Eye: Xem chi tiết tổng hợp đoàn
MessageSquare: Xem danh sách feedback cá nhân
FileText/Download: Xuất báo cáo nếu có
```

---

## 8. Tab 2 — Tất cả feedback

### 8.1. Mục đích

Tab này hiển thị từng dòng `feedbacks`, phù hợp khi người dùng muốn tra cứu cụ thể **ai feedback ai**.

### 8.2. Cột đề xuất

```text
Feedback | Đoàn / Visit | Người đánh giá | Người được đánh giá | Chiều đánh giá | Điểm | Thời gian | Hành động
```

#### Cột “Feedback”

Hiển thị:

```text
FB #feedback_id
Comment ngắn 1–2 dòng
```

#### Cột “Đoàn / Visit”

```text
Tên đoàn khách
REQ #visit_request_id · INST #visit_instance_id
```

#### Cột “Người đánh giá”

Hiển thị đầy đủ:

```text
submitter_name_snapshot
submitter_role badge
submitter_context
User #submitted_by_user_id
```

Ví dụ:

```text
Nguyễn T
VISITOR · Khách đại diện
User #55
```

#### Cột “Người được đánh giá”

```text
target_name_snapshot
target_role badge
target_context
User #target_user_id
```

#### Cột “Chiều đánh giá”

Hiển thị dạng badge:

```text
Khách → Host
Host → Logistics
```

Giá trị kỹ thuật tương ứng:

```text
VISITOR → HOST
HOST → LOGISTICS
```

#### Cột “Điểm”

```text
3.0 ★★★☆☆
```

Nếu `rating <= 2`, dùng badge cảnh báo.

#### Cột “Thời gian”

```text
24/10/2026 09:30
```

#### Cột “Hành động”

```text
Xem chi tiết
```

---

## 9. Trang hoặc modal chi tiết tổng hợp theo đoàn

Khi bấm xem một đoàn, mở detail page hoặc modal lớn.

### 9.1. Header chi tiết

```text
Chi tiết feedback đoàn khách
Đoàn Sở Giáo Dục Vĩnh Phúc
REQ #102 · INST #501
```

Button:

```text
Quay lại
Xuất báo cáo
```

---

### 9.2. Summary cards trong detail

Nên có 5 card:

```text
Điểm trung bình
Số lượng phản hồi
Feedback thấp
Người được đánh giá nhiều nhất
Feedback mới nhất
```

Ví dụ:

```text
Điểm trung bình: 3.8/5
Số phản hồi: 12
Feedback thấp: 2
Được đánh giá nhiều nhất: Host chính
Mới nhất: 24/10/2026
```

---

### 9.3. Section phân tích theo chiều đánh giá

Thêm section:

```text
Tổng hợp theo chiều đánh giá
```

Bảng nhỏ:

| Chiều đánh giá | Số lượng | Điểm TB |
|---|---:|---:|
| Visitor → Host | 5 | 4.4 |
| Host → Visitor | 3 | 4.0 |
| Host → Logistics | 2 | 3.5 |
| Logistics → Host | 2 | 4.5 |

Phần này giúp Staff Leader biết feedback tiêu cực đến từ phía nào.

---

### 9.4. Section phân tích theo đối tượng được đánh giá

Thêm section:

```text
Người/nhóm được đánh giá
```

Bảng nhỏ:

| Người được đánh giá | Vai trò | Ngữ cảnh | Số feedback | Điểm TB | Thấp nhất |
|---|---|---|---:|---:|---:|
| Trần Văn A | HOST | Host chính | 5 | 4.2 | 3 |
| Nguyễn Văn B | LOGISTICS | Xe điện | 2 | 3.0 | 2 |

---

## 10. Danh sách feedback cá nhân trong detail

Mỗi feedback nên là một card.

Card đề xuất:

```text
[Người đánh giá] → [Người được đánh giá]
Vai trò/ngữ cảnh người gửi    Vai trò/ngữ cảnh người nhận
Rating tổng
Ngày gửi
Comment
Tiêu chí chi tiết
```

Ví dụ:

```text
Nguyễn T  →  Trần Văn A
VISITOR · Khách đại diện       HOST · Host chính
24/10/2026 09:30               3.0 ★★★☆☆

Góp ý:
"Tour diễn ra ổn nhưng thời tiết hơi nóng."

Tiêu chí:
Không gian tham quan     3/5
Chất lượng hỗ trợ        3/5
Giao tiếp                4/5
Đúng giờ                 2/5
```

Mỗi card có nút:

```text
Xem đầy đủ
```

Nút này mở modal chi tiết feedback.

---

## 11. Modal xem chi tiết một feedback

Modal này là nơi hiển thị **đầy đủ thuộc tính trong database**.

### 11.1. Section “Thông tin feedback”

```text
feedback_id
visit_request_id
visit_instance_id
rating
comment
submitted_at
```

### 11.2. Section “Người gửi feedback”

```text
submitted_by_user_id
submitter_role
submitter_context
submitter_name_snapshot
```

### 11.3. Section “Người được đánh giá”

```text
target_user_id
target_role
target_context
target_name_snapshot
```

### 11.4. Section “Chiều đánh giá”

Hiển thị kỹ thuật:

```text
VISITOR → HOST
```

Và tiếng Việt:

```text
Khách đánh giá Host
```

### 11.5. Section “Tiêu chí chi tiết”

Dữ liệu từ `feedback_rating_items`:

```text
feedback_rating_item_id
criterion_code
criterion_label
rating
display_order
created_at
```

Hiển thị dạng bảng:

```text
Tiêu chí | Mã tiêu chí | Điểm | Thứ tự | Ngày tạo
```

---

## 12. Backend/API đề xuất

Để UI tốt, backend không nên chỉ trả raw feedback. Nên có 3 API chính.

---

### 12.1. API danh sách tổng hợp theo đoàn

```http
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
page
pageSize
sortBy
sortDir
```

Response item:

```text
visit_request_id
visit_instance_id
visit_title / delegation_name
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

---

### 12.2. API danh sách feedback raw

```http
GET /api/feedbacks
```

Query params:

```text
q
visitRequestId
visitInstanceId
submitterUserId
targetUserId
submitterRole
targetRole
roleFlow
ratingFrom
ratingTo
submittedFrom
submittedTo
criterionCode
page
pageSize
sortBy
sortDir
```

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

---

### 12.3. API chi tiết feedback theo đoàn

Theo visit request:

```http
GET /api/feedbacks/visit-summary/{visitRequestId}
```

Theo campus instance:

```http
GET /api/feedbacks/visit-summary/{visitRequestId}/instances/{visitInstanceId}
```

Response nên gồm:

```text
summary
role_flow_breakdown
target_breakdown
feedbacks[]
feedbacks[].rating_items[]
```

---

### 12.4. API chi tiết một feedback

```http
GET /api/feedbacks/{feedbackId}
```

Response gồm:

```text
feedback fields đầy đủ
visit summary
submitter summary
target summary
rating_items[]
```

---

## 13. Scope cho Staff Leader campus Hà Nội

Nếu user hiện tại là Staff Leader campus Hà Nội:

```text
Không hiển thị filter campus.
Không trả feedback campus khác.
Không cho frontend truyền campusId để xem campus khác.
Backend tự lấy currentUser.primary_campus_id.
```

Với feedback có `visit_instance_id`:

```text
Join visit_request_campuses
Filter visit_request_campuses.campus_id = currentUser.primary_campus_id
```

Với feedback chỉ có `visit_request_id` mà `visit_instance_id IS NULL`, nếu chưa có rule rõ ràng thì mặc định:

```text
Staff Leader chỉ xem feedback có visit_instance_id thuộc campus của mình.
Không hiển thị feedback instance null nếu không xác định campus.
```

---

## 14. Layout tổng hợp cuối cùng

```text
Quản lý feedback
Tổng hợp và tra cứu đánh giá của các đoàn khách đã hoàn tất
[Campus: Hà Nội]

[ Tổng đoàn ] [ Tổng feedback ] [ Điểm TB ] [ Feedback thấp ] [ Mới nhất ]

[Search...] [Mức đánh giá] [Chiều đánh giá] [Vai trò người gửi] [Khoảng ngày] [Lọc] [Reset]
[Bộ lọc nâng cao]

Tabs:
- Tổng hợp theo đoàn
- Tất cả feedback

Tab Tổng hợp theo đoàn:
Đoàn khách | Tổng feedback | Điểm TB | Phân bố sao | Chiều đánh giá | Feedback mới nhất | Cảnh báo | Hành động

Tab Tất cả feedback:
Feedback | Đoàn / Visit | Người đánh giá | Người được đánh giá | Chiều đánh giá | Điểm | Thời gian | Hành động
```

---

# 15. Prompt cho AI Code

```text
Bạn là Senior Full-stack Engineer cho dự án PEMS - Partnership Engagement Management System.

Nhiệm vụ: nâng cấp màn hình Feedback Management để người dùng Staff Leader có thể xem tổng hợp feedback theo từng đoàn khách một cách dễ dàng, đồng thời có thể drill-down vào từng feedback cá nhân để biết rõ ai feedback, feedback ai, feedback thuộc đoàn nào, khi nào, nội dung gì và điểm theo từng tiêu chí.

Bối cảnh hiện tại:
- Route hiện tại: /dashboard/feedback hoặc route feedback management hiện có trong project.
- UI hiện tại đang hiển thị đơn giản: STT, Tên đoàn khách, Trung bình đánh giá, Thời gian, Hành động.
- Detail hiện tại có card Trung bình đánh giá, Số lượng phản hồi và danh sách đánh giá cá nhân.
- Cần nâng cấp UI/API theo database thật: feedbacks và feedback_rating_items.

Database cần bám sát:

feedbacks:
- feedback_id
- visit_request_id
- visit_instance_id
- submitted_by_user_id
- submitter_role ENUM('VISITOR','HOST','LOGISTICS')
- submitter_context
- submitter_name_snapshot
- target_user_id
- target_role ENUM('VISITOR','HOST','LOGISTICS')
- target_context
- target_name_snapshot
- rating TINYINT 1..5
- comment
- submitted_at

feedback_rating_items:
- feedback_rating_item_id
- feedback_id
- criterion_code
- criterion_label
- rating TINYINT 1..5
- display_order
- created_at

Role flow hợp lệ:
- VISITOR -> HOST
- LOGISTICS -> HOST
- HOST -> VISITOR
- HOST -> LOGISTICS

Yêu cầu scope:
1. Màn hình này dùng cho Staff Leader theo campus.
2. Nếu current user là Staff Leader campus Hà Nội thì chỉ thấy feedback của campus Hà Nội.
3. Không hiển thị filter Campus.
4. Frontend không được truyền campusId để lọc.
5. Backend tự lấy currentUser.primary_campus_id để lọc.
6. Với feedback có visit_instance_id, backend join visit_request_campuses và filter visit_request_campuses.campus_id = currentUser.primary_campus_id.
7. Với feedback visit_instance_id NULL, nếu chưa có business rule rõ ràng thì không hiển thị cho Staff Leader vì không xác định được campus scope.
8. Gọi API trực tiếp với campusId khác không được trả dữ liệu campus khác.

Yêu cầu UI trang danh sách:
1. Giữ layout dashboard/sidebar hiện tại.
2. Header:
   - Title: Quản lý feedback
   - Subtitle: Tổng hợp và tra cứu đánh giá của các đoàn khách đã hoàn tất
   - Badge read-only: Campus: {currentUser.primaryCampusName}, ví dụ Hà Nội.
3. Thêm summary cards:
   - Tổng đoàn có feedback
   - Tổng số feedback
   - Điểm trung bình
   - Feedback 1–2 sao / Cần chú ý
   - Feedback mới nhất
   - Tỷ lệ có đánh giá chi tiết nếu API trả được
4. Filter bar chính:
   - Search input placeholder: "Tìm theo tên đoàn, người đánh giá, người được đánh giá, nội dung..."
   - Rating level dropdown: Tất cả, 5 sao, 4 sao, 3 sao, 1–2 sao, Dưới 3 sao, Cần chú ý.
   - Role flow dropdown: Tất cả, Khách đánh giá Host, Logistics đánh giá Host, Host đánh giá Khách, Host đánh giá Logistics.
   - Submitter role dropdown: Tất cả người gửi, VISITOR, HOST, LOGISTICS.
   - Submitted date range: submittedFrom, submittedTo.
   - Button Lọc.
   - Button Reset.
5. Collapsible "Bộ lọc nâng cao":
   - Người đánh giá / submitterUserId hoặc keyword.
   - Người được đánh giá / targetUserId hoặc keyword.
   - Submitter context.
   - Target context.
   - Visit request ID.
   - Visit instance ID.
   - Có comment / Không có comment.
   - Có rating item chi tiết / Không có rating item.
   - Criterion code.
   - Điểm tiêu chí từ - đến.
6. Có 2 tab:
   - Tổng hợp theo đoàn.
   - Tất cả feedback.

Tab 1 - Tổng hợp theo đoàn:
1. Là tab mặc định.
2. Columns:
   - Đoàn khách: visit title/delegation name + REQ #visit_request_id + INST #visit_instance_id nếu có.
   - Phạm vi visit: campus name, scope/status nếu backend trả được.
   - Tổng feedback.
   - Điểm TB: số + sao.
   - Phân bố sao: 5★, 4★, 3★, 1–2★.
   - Feedback mới nhất: latest_submitted_at + latest_submitter_name.
   - Cảnh báo: low_rating_count.
   - Hành động: xem chi tiết.
3. Nếu low_rating_count > 0, hiển thị badge cảnh báo.
4. Không dùng horizontal scroll toàn trang; desktop table gọn, mobile chuyển card list.

Tab 2 - Tất cả feedback:
1. Hiển thị từng dòng feedbacks.
2. Columns:
   - Feedback: FB #feedback_id + comment preview.
   - Đoàn / Visit: visit title + REQ # + INST #.
   - Người đánh giá: submitter_name_snapshot + submitter_role badge + submitter_context + User #submitted_by_user_id.
   - Người được đánh giá: target_name_snapshot + target_role badge + target_context + User #target_user_id.
   - Chiều đánh giá: VISITOR -> HOST hoặc label tiếng Việt.
   - Điểm: rating number + stars; rating <= 2 thì dùng cảnh báo.
   - Thời gian: submitted_at.
   - Hành động: xem chi tiết.

Yêu cầu detail tổng hợp theo đoàn:
1. Khi bấm xem chi tiết ở Tab Tổng hợp theo đoàn, mở detail page hoặc modal lớn theo pattern hiện có.
2. Header:
   - Chi tiết feedback đoàn khách
   - Tên đoàn
   - REQ #visit_request_id · INST #visit_instance_id nếu có
   - Button quay lại
   - Button xuất báo cáo nếu đã có API, nếu chưa có thì không tự bịa.
3. Summary cards:
   - Điểm trung bình
   - Số lượng phản hồi
   - Feedback thấp
   - Người được đánh giá nhiều nhất
   - Feedback mới nhất
4. Section "Tổng hợp theo chiều đánh giá":
   - Visitor -> Host: số lượng, điểm TB
   - Host -> Visitor: số lượng, điểm TB
   - Host -> Logistics: số lượng, điểm TB
   - Logistics -> Host: số lượng, điểm TB
5. Section "Người/nhóm được đánh giá":
   - Người được đánh giá
   - Vai trò
   - Ngữ cảnh
   - Số feedback
   - Điểm TB
   - Thấp nhất
6. Section "Danh sách đánh giá cá nhân":
   - Mỗi feedback là một card.
   - Card hiển thị: người đánh giá -> người được đánh giá, role/context hai bên, rating tổng, ngày gửi, comment, tiêu chí chi tiết.
   - Có nút "Xem đầy đủ" mở modal chi tiết một feedback.

Yêu cầu modal chi tiết một feedback:
1. Hiển thị đầy đủ field database.
2. Section "Thông tin feedback":
   - feedback_id
   - visit_request_id
   - visit_instance_id
   - rating
   - comment
   - submitted_at
3. Section "Người gửi feedback":
   - submitted_by_user_id
   - submitter_role
   - submitter_context
   - submitter_name_snapshot
4. Section "Người được đánh giá":
   - target_user_id
   - target_role
   - target_context
   - target_name_snapshot
5. Section "Chiều đánh giá":
   - VISITOR -> HOST / HOST -> LOGISTICS...
   - Label tiếng Việt tương ứng.
6. Section "Tiêu chí chi tiết":
   - feedback_rating_item_id
   - criterion_code
   - criterion_label
   - rating
   - display_order
   - created_at
7. Không bỏ qua field nào trong modal detail.

Yêu cầu backend/API:
1. Không tạo bảng mới.
2. Không dùng mock data.
3. Không hard-code dữ liệu mẫu.
4. Tạo/cập nhật API danh sách tổng hợp theo đoàn:
   GET /api/feedbacks/visit-summary
5. Query params:
   - q
   - ratingLevel
   - submitterRole
   - targetRole
   - roleFlow
   - submittedFrom
   - submittedTo
   - hasLowRating
   - page
   - pageSize
   - sortBy
   - sortDir
6. Response item gồm:
   - visit_request_id
   - visit_instance_id
   - visit_title / delegation_name
   - campus_name
   - total_feedbacks
   - average_rating
   - latest_submitted_at
   - latest_submitter_name
   - low_rating_count
   - visitor_to_host_count
   - host_to_visitor_count
   - host_to_logistics_count
   - logistics_to_host_count
   - star_5_count
   - star_4_count
   - star_3_count
   - star_1_2_count
7. Tạo/cập nhật API danh sách feedback raw:
   GET /api/feedbacks
8. Query params:
   - q
   - visitRequestId
   - visitInstanceId
   - submitterUserId
   - targetUserId
   - submitterRole
   - targetRole
   - roleFlow
   - ratingFrom
   - ratingTo
   - submittedFrom
   - submittedTo
   - criterionCode
   - page
   - pageSize
   - sortBy
   - sortDir
9. Response item gồm:
   - feedback_id
   - visit_request_id
   - visit_instance_id
   - visit_title
   - submitted_by_user_id
   - submitter_role
   - submitter_context
   - submitter_name_snapshot
   - target_user_id
   - target_role
   - target_context
   - target_name_snapshot
   - rating
   - comment_preview
   - submitted_at
   - rating_item_count
10. Tạo/cập nhật API chi tiết feedback theo đoàn:
    GET /api/feedbacks/visit-summary/{visitRequestId}
    hoặc
    GET /api/feedbacks/visit-summary/{visitRequestId}/instances/{visitInstanceId}
11. Response gồm:
    - summary
    - role_flow_breakdown
    - target_breakdown
    - feedbacks[]
    - feedbacks[].rating_items[]
12. Tạo/cập nhật API chi tiết một feedback:
    GET /api/feedbacks/{feedbackId}
13. Response gồm:
    - feedback fields đầy đủ
    - visit summary
    - submitter summary
    - target summary
    - rating_items[]
14. Backend phải validate scope Staff Leader theo currentUser.primary_campus_id, không tin campusId từ frontend.
15. Với detail feedback, nếu feedback không thuộc campus scope của Staff Leader thì trả 403 hoặc 404 theo convention hiện tại.
16. Search q nên tìm trong:
    - visit/delegation name
    - submitter_name_snapshot
    - target_name_snapshot
    - submitter_context
    - target_context
    - comment
    - criterion_label
    - feedback_id
    - visit_request_id
    - visit_instance_id

Yêu cầu frontend code clean:
1. Không dùng mock data.
2. Không hard-code campus Hà Nội trong query.
3. Không truyền campusId lên API.
4. Dùng currentUser.primaryCampusName để hiển thị badge scope nếu có.
5. Tách type/interface rõ ràng:
   - FeedbackVisitSummaryItem
   - FeedbackListItem
   - FeedbackDetail
   - FeedbackRatingItem
   - FeedbackFilterParams
   - FeedbackRole
   - FeedbackRoleFlow
6. Tách service/hook theo pattern project:
   - feedbacksApi.ts
   - useFeedbacks.ts hoặc query pattern hiện tại.
7. Debounce search 300-500ms.
8. Format date theo dd/MM/yyyy HH:mm.
9. Format rating bằng số + stars.
10. Loading/empty/error state rõ ràng.
11. Empty state:
    - "Chưa có feedback nào trong campus của bạn."
    - "Không tìm thấy feedback phù hợp với bộ lọc."
12. Mobile dùng card list, không ép table gây horizontal scroll toàn trang.
13. Icon-only button phải có title và aria-label.
14. Không thêm thư viện mới nếu không cần.
15. Không refactor sâu ngoài Feedback Management.

Style UI:
- Enterprise dashboard, sạch, gọn, hiện đại.
- Primary blue #004c91.
- Orange #F37021 chỉ dùng cho CTA hoặc card nhấn mạnh.
- Card: rounded-2xl border border-slate-200 bg-white shadow-sm.
- Table header navy.
- Badge trạng thái/role nhỏ gọn.
- Rating thấp dùng warning/danger nhẹ, không quá chói.
- Không dùng gradient mạnh hoặc shadow quá đậm.
- Không để action column bị cắt chữ.

Build/test bắt buộc:
1. Nếu sửa backend: chạy dotnet build.
2. Nếu sửa frontend: chạy npm run build hoặc pnpm build theo project hiện tại.
3. Manual test:
   - Login Staff Leader campus Hà Nội.
   - Vào màn Quản lý feedback.
   - Không thấy filter Campus.
   - Không thấy feedback campus khác.
   - Summary cards hiển thị đúng.
   - Search theo tên đoàn hoạt động.
   - Search theo người đánh giá/người được đánh giá hoạt động.
   - Filter rating hoạt động.
   - Filter role flow hoạt động.
   - Filter ngày gửi hoạt động.
   - Tab Tổng hợp theo đoàn hoạt động.
   - Tab Tất cả feedback hoạt động.
   - Mở chi tiết đoàn hiển thị breakdown theo chiều đánh giá.
   - Mở modal chi tiết feedback hiển thị đầy đủ field database.
   - Gọi trực tiếp API detail feedback thuộc campus khác bị chặn.

Kết quả cần báo cáo sau khi code:
1. File đã sửa.
2. API đã thêm/sửa.
3. UI đã thay đổi gì.
4. Scope Staff Leader campus được enforce ở đâu.
5. Cách test thủ công.
6. Build đã chạy chưa.
7. Những phần chưa làm được và lý do nếu có.
```

---

## 16. Kết luận

UI Feedback Management nên chuyển từ danh sách đơn giản sang mô hình:

```text
Tổng hợp theo đoàn
→ Xem breakdown theo chiều đánh giá
→ Xem từng feedback cá nhân
→ Mở modal đầy đủ field database
```

Cách này giúp Staff Leader nhanh chóng trả lời các câu hỏi quan trọng:

```text
Đoàn nào bị đánh giá thấp?
Ai đánh giá?
Đánh giá ai?
Chiều đánh giá là gì?
Feedback thuộc visit nào?
Gửi lúc nào?
Nội dung góp ý là gì?
Điểm từng tiêu chí ra sao?
```
