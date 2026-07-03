# PROMPT — Fix Feedback Management Page theo Database Feedback mới

Bạn là **Senior Full-stack Engineer + Senior Frontend UI/UX Engineer** cho PEMS.

## Nhiệm vụ

Sửa trang **“Quản lý feedback”** theo database feedback mới.

Yêu cầu chung:

- Không đổi schema nếu không cần.
- Không xóa chức năng đang chạy.
- Không dùng mock nếu API đã có dữ liệu thật.
- Code sạch, tách component rõ.
- UI compact, responsive mobile.
- Không refactor lan rộng gây conflict.

---

## 1. Sửa UI tổng quan phía trên

Hiện tại có 4 ô số liệu lớn làm trang bị dài, đặc biệt trên mobile.

Yêu cầu:

- Không hiển thị 4 card lớn xuống dòng nữa.
- Đổi thành 1 dòng tổng quan compact.
- Format gợi ý:

```text
Tổng quan: 8 đoàn • Điểm TB 3.5★ • Cảnh báo 3 • Mới nhất 10:00 15/08/2026
```

- Desktop: hiển thị trên 1 hàng, gọn, không dùng card lớn.
- Mobile: vẫn cố giữ trong 1–2 dòng nhỏ, có thể wrap nhẹ nhưng không thành 4 khối lớn.
- Giảm padding, giảm font size.
- Không dùng shadow/card lớn.

---

## 2. Sửa bộ lọc

Bỏ hoàn toàn phần **“Bộ lọc nâng cao”**.

Thay bằng filter trực tiếp, gọn, gồm:

- Search: tìm theo tên đoàn, người đánh giá, đối tượng được đánh giá.
- Lọc theo mức độ:
  - Tất cả
  - Cảnh báo 1–2 sao
  - Trung bình 3 sao
  - Tốt 4–5 sao
- Lọc theo thời gian:
  - Tất cả thời gian
  - 7 ngày gần nhất
  - 30 ngày gần nhất
  - Khoảng ngày tùy chọn nếu component date range đã có sẵn
- Nút Reset.

Yêu cầu UI:

- Không còn nút **“Bộ lọc nâng cao”**.
- Desktop: filter nằm cùng 1 hàng nếu đủ rộng.
- Mobile: filter xếp gọn 1 cột, không tràn ngang.

---

## 3. Sửa logic điểm trung bình từng đoàn

Dựa vào database feedback mới:

- Host đánh giá chung đoàn khách: `feedback_type = HOST_DELEGATION_OVERALL`.
- Host đánh giá các bên tham gia: `feedback_type = HOST_PARTICIPANT`.
- Host đánh giá các bên cho mượn đồ/hậu cần: `feedback_type = HOST_LOGISTICS`.
- Visitor/khách đánh giá chung chuyến thăm: `feedback_type = VISITOR_OVERALL`.

Điểm trung bình của từng đoàn phải tính bằng:

- Lấy tất cả rating hợp lệ liên quan đến `visit_request_id` / `visit_instance_id` đó.
- Bao gồm:
  - `HOST_DELEGATION_OVERALL`
  - `HOST_PARTICIPANT`
  - `HOST_LOGISTICS`
  - `VISITOR_OVERALL`
- Cộng toàn bộ sao lại rồi chia trung bình.
- Không chỉ lấy visitor feedback hoặc feedback mới nhất.
- Nếu không có feedback thì hiển thị **“Chưa có”**.
- Cảnh báo nếu có ít nhất một feedback rating 1–2 sao.

---

## 4. Sửa action icon mắt xem chi tiết

Khi bấm icon mắt ở cột hành động:

- Không mở trang có 4 ô số liệu nữa.
- Mở chi tiết feedback của đoàn.
- Có thể là modal hoặc trang detail hiện tại, nhưng phần detail không được có 4 card thống kê lớn.
- Ưu tiên modal/detail compact.

Nội dung chi tiết phải chia rõ 2 nhóm chính:

### 4.1. Host đánh giá đoàn khách

- Hiển thị `feedback_type = HOST_DELEGATION_OVERALL`.
- Hiển thị người đánh giá, thời gian, số sao, comment nếu có.
- Đây là đánh giá chung đoàn khách, không phải đánh giá từng khách.

### 4.2. Khách đánh giá

- Hiển thị `feedback_type = VISITOR_OVERALL`.
- Hiển thị khách/người gửi, thời gian, số sao, comment nếu có.
- Đây là khách đánh giá chung chuyến thăm/đoàn tiếp đón.

Nếu muốn hiển thị thêm chi tiết nội bộ thì đặt dưới dạng section phụ:

### 4.3. Host đánh giá bên tham gia

- Hiển thị `feedback_type = HOST_PARTICIPANT`.

### 4.4. Host đánh giá hậu cần/đồ mượn

- Hiển thị `feedback_type = HOST_LOGISTICS`.

Hai nhóm chính bắt buộc phải hiện rõ đầu tiên:

- Host đánh giá đoàn khách
- Khách đánh giá

---

## 5. UI chi tiết feedback

Thiết kế compact:

- Không dùng nhiều card lớn.
- Không dùng 4 ô thống kê trong detail.
- Dùng list/table nhỏ gồm:
  - Người đánh giá
  - Vai trò
  - Đối tượng
  - Sao
  - Nhận xét
  - Thời gian
- Comment dài thì rút gọn, có **“Xem thêm”**.
- Mobile: mỗi feedback là một row/card nhỏ, không tràn ngang.

---

## 6. Backend/API cần kiểm tra

Nếu API quản lý feedback hiện tại chưa trả đủ dữ liệu, bổ sung DTO/query phù hợp:

- `visit_request_id`
- `visit_instance_id`
- `delegation_name`
- `campus_name`
- `average_rating`
- `warning_count`
- `latest_feedback_at`
- `latest_feedback_submitter`
- `feedback_type`
- `submitter_role`
- `submitter_name_snapshot`
- `target_type`
- `target_name_snapshot`
- `rating`
- `comment`
- `submitted_at`

Ưu tiên backend trả `average_rating` đúng rule để frontend chỉ hiển thị.

Nếu đang tính ở frontend thì phải đảm bảo lấy đủ tất cả feedback type liên quan, không chỉ dòng mới nhất.

---

## 7. Clean code frontend

Tách component rõ:

```text
FeedbackManagementPage
FeedbackSummaryCompact
FeedbackFilterBar
FeedbackTable
FeedbackDetailModal hoặc FeedbackDetailPanel
FeedbackRatingStars
FeedbackTypeSection
```

Không nhét toàn bộ vào một file quá lớn nếu có thể tách.

---

## 8. Test sau khi sửa

Kiểm tra:

- Trang quản lý feedback desktop không còn 4 card lớn.
- Mobile không bị kéo dài bởi 4 ô số liệu.
- Không còn nút **“Bộ lọc nâng cao”**.
- Có filter mức độ, thời gian, reset.
- Điểm trung bình từng đoàn tính từ tất cả rating liên quan:
  - `HOST_DELEGATION_OVERALL`
  - `HOST_PARTICIPANT`
  - `HOST_LOGISTICS`
  - `VISITOR_OVERALL`
- Bấm icon mắt mở chi tiết.
- Detail hiển thị rõ:
  - Host đánh giá đoàn khách
  - Khách đánh giá
- Detail không còn 4 ô số liệu.
- Build frontend/backend không lỗi.

---

## 9. Báo cáo lại sau khi làm

Báo cáo theo format:

```text
Files changed:
- ...

Component/API/DTO updated:
- ...

Average rating calculation:
- ...

Build/test result:
- ...

Notes / unfinished items:
- ...
```
