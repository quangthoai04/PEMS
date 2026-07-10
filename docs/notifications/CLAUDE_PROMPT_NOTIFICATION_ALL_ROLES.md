# Prompt cho Claude AI — Nâng cấp Notification toàn hệ thống PEMS

Bạn hãy đọc kỹ source code hiện tại và database base mới của PEMS, sau đó nâng cấp hệ thống notification để thông báo hiển thị đúng cho tất cả role ở mọi trang sau khi user đăng nhập.

## 1. Bối cảnh dự án

PEMS là hệ thống quản lý đối tác và đoàn tham quan.

Tech stack:

- Backend: ASP.NET Core .NET 8, Clean Architecture, MediatR, EF Core, MySQL
- Frontend: React + Vite + TypeScript
- Database: MySQL
- Notification hiện tại mới hiển thị chủ yếu ở dashboard theo từng role
- Database base mới đã nâng cấp bảng `notifications`

Bảng `notifications` hiện đã có các cột mở rộng như:

```text
actor_user_id
category
priority
is_action_required
visit_request_id
visit_instance_id
campus_id
action_type
action_url
metadata_json
dedupe_key
archived_at
```

## 2. Nguyên tắc bắt buộc

Chỉ sửa logic notification và UI notification.

Không tự ý sửa các bảng nghiệp vụ khác như:

```text
visit_requests
visit_request_campuses
visit_participants
feedbacks
visit_logistics_items
news
partners
users
```

Nếu cần thêm dữ liệu phụ cho notification, hãy dùng `metadata_json`, `action_type`, `action_url`, `visit_request_id`, `visit_instance_id`, `campus_id` trong bảng `notifications`.

Không tạo notification theo role một cách chung chung. Chỉ tạo notification cho user thật sự liên quan đến entity phát sinh sự kiện.

Ví dụ user được nhận notification khi:

```text
- Là người tạo đơn
- Là host
- Là người được mời
- Là người đã accepted tham gia đoàn
- Là người được gán xử lý
- Là Staff Leader quản lý campus liên quan
- Là Dept Leader quản lý phòng ban liên quan
- Là người gửi yêu cầu/lời mời cần theo dõi kết quả
- Là HO theo dõi đơn liên cơ sở
```

## 3. Mục tiêu frontend

Nâng cấp notification thành Global Notification Center.

Yêu cầu:

```text
- Hiển thị chuông notification ở layout/header chung sau khi user đăng nhập
- Chuông notification xuất hiện ở homepage, dashboard và các trang khác
- Hiển thị badge số notification chưa đọc
- Dropdown hiển thị notification mới nhất
- Có trang /notifications để xem tất cả notification
- Có filter: Tất cả, Chưa đọc, Cần hành động, Đoàn khách, Thư mời, Feedback, Hậu cần, Tin tức, Đối tác, Hệ thống
- Click notification phải điều hướng đúng hoặc mở đúng modal
- Hỗ trợ mark as read, mark all as read
```

Nếu notification là host feedback về user, khi click cần mở modal gồm:

```text
- Thông tin chung của đoàn
- Có nút thu gọn / mở rộng phần thông tin đoàn
- Feedback của host về chính user đó
```

Không cho user xem feedback của người khác.

## 4. Mục tiêu backend

Cần chuẩn hóa service tạo notification.

Nên có một service trung tâm, ví dụ:

```text
INotificationService
NotificationService
NotificationFactory
NotificationRecipientResolver
```

Service này chịu trách nhiệm:

```text
- Xác định đúng recipient
- Tạo title/message/category/action_url/action_type/metadata_json
- Gắn visit_request_id, visit_instance_id, campus_id khi có
- Tạo dedupe_key để chống tạo trùng
- Không tạo notification trùng cho cùng recipient + dedupe_key
```

Không dùng SQL trigger để tạo notification. Tạo notification trong Application layer sau các nghiệp vụ chính.

## 5. Rule notification theo role

### Visitor

Nhận notification khi:

```text
- Đơn thăm quan được duyệt
- Đơn thăm quan bị từ chối
- Đơn được duyệt một phần theo từng campus
- Đoàn/đơn của mình bị hủy
- Sau chuyến tham quan: cảm ơn đã tham quan
- Có lời mời gửi feedback sau chuyến
- Feedback của visitor được ghi nhận nếu hệ thống có flow này
```

### Student

Nhận notification khi:

```text
- Có thư mời tham gia đoàn
- Lời mời được xác nhận hoặc trạng thái tham gia thay đổi
- Nhắc trước 30 phút khi đoàn sắp diễn ra, chỉ nếu đã ACCEPTED
- Đoàn đã ACCEPTED tham gia bị hủy
- Host feedback về chính student đó
```

### Department Leader

Nhận notification khi:

```text
- Phòng ban có yêu cầu hỗ trợ mới
- Có thư mời tham gia đoàn
- Cần gán Dept Staff xử lý yêu cầu
- Dept Staff chấp nhận/từ chối nhiệm vụ
- Host ký bàn giao
- Có nghiệm thu cần xử lý
- Có đề xuất/chỉnh sửa từ host hoặc staff
- Nhắc trước 30 phút nếu Dept Leader đã ACCEPTED tham gia
- Đoàn liên quan đến phòng ban bị hủy
- Host feedback về chính Dept Leader hoặc phòng ban của họ
```

### Department Staff

Nhận notification khi:

```text
- Được Dept Leader gán xử lý yêu cầu
- Có thư mời tham gia đoàn
- Có bàn giao/nghiệm thu/yêu cầu từ host liên quan nhiệm vụ của mình
- Có đề xuất/chỉnh sửa từ host hoặc staff
- Nhắc trước 30 phút nếu đã ACCEPTED tham gia
- Đoàn đã nhận nhiệm vụ hoặc ACCEPTED tham gia bị hủy
- Host feedback về chính Dept Staff đó
```

### Staff Leader

Nhận notification khi:

```text
- Có đơn thăm quan mới đến campus mình quản lý
- Có đơn liên cơ sở chứa campus mình quản lý
- Host chấp nhận/từ chối nhận đoàn nếu có flow phản hồi host
- Đoàn thuộc campus mình bị hủy
- Có visitor feedback cho đoàn thuộc campus mình
- Có news hoặc partner cần duyệt nếu module hiện tại có flow duyệt
- Nếu Staff Leader cũng là host thì nhận thêm notification giống Staff
```

### Staff

Nhận notification khi:

```text
- Được gán làm host cho đoàn
- Nhắc trước 30 phút khi đoàn mình host sắp diễn ra
- Đoàn mình host hoặc tham gia bị hủy
- Người mình mời chấp nhận/từ chối lời mời
- Phòng ban chấp nhận/từ chối yêu cầu hỗ trợ
- Có đề xuất từ phòng ban
- Logistics/bàn giao/nghiệm thu thay đổi trạng thái
- Visitor gửi feedback cho đoàn mình host
- News của mình được duyệt, bị từ chối, bị ẩn, được publish
- Partner của mình được duyệt, bị từ chối hoặc cần bổ sung
```

### HO

Nhận notification khi:

```text
- Có đơn liên cơ sở mới
- Trạng thái từng campus trong đơn liên cơ sở thay đổi
- Đơn liên cơ sở được duyệt một phần
- Tất cả campus trong đơn liên cơ sở đã xử lý xong
- Đơn liên cơ sở bị hủy
- Campus trong đơn liên cơ sở gần đến ngày tham quan nhưng chưa xử lý
```

## 6. Rule thông báo 30 phút trước đoàn

Tạo background job hoặc scheduled worker để kiểm tra đoàn sắp diễn ra.

Chỉ gửi notification cho:

```text
- Host của đoàn
- Student đã ACCEPTED
- Staff/Dept Staff/Dept Leader đã ACCEPTED hoặc được giao nhiệm vụ còn hiệu lực
```

Không gửi cho người đã DECLINED hoặc REMOVED.

Bắt buộc dùng `dedupe_key`, ví dụ:

```text
REMINDER_30M_VISIT_INSTANCE_{visitInstanceId}_USER_{userId}
```

Để tránh job chạy nhiều lần tạo notification trùng.

## 7. Gợi ý category/type/action

Dùng các category chính:

```text
VISIT
INVITATION
REMINDER
FEEDBACK
LOGISTICS
HANDOVER
NEWS
PARTNER
ACCOUNT
SYSTEM
```

Dùng `action_type` để frontend xử lý click:

```text
OPEN_VISIT_DETAIL
OPEN_VISIT_INVITATION
OPEN_HOST_FEEDBACK_MODAL
OPEN_LOGISTICS_DETAIL
OPEN_NEWS_DETAIL
OPEN_PARTNER_DETAIL
OPEN_NOTIFICATION_PAGE
```

`action_url` nên là URL frontend có thể điều hướng trực tiếp, ví dụ:

```text
/visit-requests/{id}
/visits/{visitInstanceId}
/notifications
/news/manage/{id}
/partners/manage/{id}
```

## 8. Yêu cầu kiểm thử

Sau khi sửa, cần kiểm tra tối thiểu:

```text
- Visitor chỉ thấy notification của đơn mình tạo
- Student chỉ thấy thư mời/feedback/reminder của đoàn mình liên quan
- Dept Leader chỉ thấy yêu cầu/phòng ban của mình
- Dept Staff chỉ thấy nhiệm vụ được gán cho mình
- Staff Leader chỉ thấy đơn thuộc campus mình quản lý
- Staff chỉ thấy đoàn mình host/tham gia hoặc item mình xử lý
- HO chỉ thấy đơn liên cơ sở
- Notification 30 phút không bị tạo trùng
- Click notification mở đúng trang hoặc đúng modal
- Mark as read hoạt động
- Badge unread cập nhật đúng
```

## 9. Kết quả cần bàn giao

Khi hoàn thành, hãy báo cáo theo format:

```text
1. Backend đã sửa những file nào
2. Frontend đã sửa những file nào
3. Notification rule nào đã implement
4. API notification hiện có hoặc mới thêm
5. Các case đã test
6. Các phần chưa làm hoặc cần xác nhận thêm
```

Lưu ý: ưu tiên làm đúng logic recipient trước, sau đó mới tối ưu giao diện.
