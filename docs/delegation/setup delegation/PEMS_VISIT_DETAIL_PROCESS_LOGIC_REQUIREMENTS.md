# PEMS — Đặc tả logic trang chi tiết sau duyệt, Host setup, phân quyền role, biên bản và tin tức

> File này dùng để đưa cho AI Agent đọc và code theo đúng nghiệp vụ đã chốt.
>
> Phạm vi chính:
> - Trang xem chi tiết đơn yêu cầu tham quan.
> - Phân biệt trước duyệt / sau duyệt / đã gán Host.
> - Form yêu cầu gốc của khách.
> - Trang quy trình tiếp khách: Trước / Trong / Sau tiếp khách.
> - Phân quyền hiển thị theo role.
> - Host chỉ gán một lần.
> - Biên bản chỉ có một bản trên mỗi campus instance, có cơ chế lock khi sửa.
> - Tin tức có thể nhiều bài, theo quyền người tham gia.

---

## 1. Mục tiêu

Thiết kế và triển khai lại luồng xem chi tiết đoàn tham quan để tránh các lỗi nghiệp vụ sau:

```text
- Đơn chưa duyệt nhưng đã thấy giao diện setup tiếp khách.
- Form yêu cầu gốc của khách bị trộn với dữ liệu Host setup.
- Staff Leader có thể thao tác thay Host sau khi đã gán Host.
- Có chức năng đổi Host dù nghiệp vụ chốt Host chỉ gán 1 lần.
- Nhiều người có thể sửa biên bản cùng lúc gây ghi đè dữ liệu.
- Role không liên quan thấy quá nhiều tab/thông tin nội bộ.
```

Nguyên tắc tổng thể:

```text
Trước duyệt:
→ Chỉ xem preview đơn yêu cầu như màn duyệt hiện tại.

Đã duyệt nhưng chưa gán Host:
→ Xem detail nhẹ + Staff Leader gán Host một lần.

Đã gán Host:
→ Mới mở trang quy trình tiếp khách đầy đủ.

Yêu cầu gốc:
→ Giống preview trước duyệt, read-only.

Setup vận hành:
→ Host tạo/sửa sau duyệt, không ghi đè form gốc.

Biên bản:
→ 1 biên bản / campus instance, chỉ 1 người sửa tại một thời điểm.

Tin tức:
→ Có thể nhiều bài, theo quyền người tham gia.
```

---

## 2. Thuật ngữ và entity liên quan

### 2.1. Request tổng

Bảng/khái niệm:

```text
visit_requests
```

Status chính:

```text
PENDING_APPROVAL
APPROVED
REJECTED
CANCELLED
```

Ý nghĩa:

```text
visit_requests là đơn tổng do Visitor/khách gửi.
Một request có thể là SINGLE_CAMPUS hoặc MULTI_CAMPUS.
```

### 2.2. Campus instance

Bảng/khái niệm:

```text
visit_request_campuses
```

Status campus instance:

```text
WAITING_REQUEST_APPROVAL
WAITING_HOST_ASSIGNMENT
ASSIGNED
BEFORE_VISIT
DURING_VISIT
AFTER_VISIT
CLOSED
CANCELLED
```

Ý nghĩa:

```text
Mỗi campus trong một đơn liên cơ sở có một tiến trình riêng.
Host, agenda, logistics, biên bản, album, tin tức nên gắn theo visit_instance_id/campus instance.
```

### 2.3. Người tham gia nội bộ

Bảng/khái niệm:

```text
visit_participants
```

Các trạng thái thường dùng:

```text
INVITED
ACCEPTED
DECLINED
ASSIGNED
REMOVED
```

Chỉ người có `ACCEPTED` mới được xem/thao tác một số phần như biên bản hoặc tin tức theo rule bên dưới.

---

## 3. Quy tắc điều hướng khi bấm “Xem”

Frontend không được luôn navigate vào `VisitProcess`. Phải điều hướng theo trạng thái thật.

### 3.1. Đơn chưa duyệt

Điều kiện:

```text
visit_requests.status = PENDING_APPROVAL
visit_request_campuses.status = WAITING_REQUEST_APPROVAL
```

Hành vi:

```text
Mở preview đơn yêu cầu giống màn trước khi duyệt.
Không vào trang quy trình tiếp khách.
```

Không hiển thị:

```text
- Tổng quan quy trình
- Trước tiếp khách
- Trong tiếp khách
- Sau tiếp khách
- Setup vận hành
- Agenda chính thức
- Logistics
- Biên bản
- Album
- Tin tức
```

Role:

```text
HO:
- Xem preview đơn liên cơ sở trong scope.
- Duyệt/từ chối nếu đúng rule.

Staff Leader:
- Xem preview đơn single-campus thuộc campus mình.
- Duyệt/từ chối nếu đúng rule.

Visitor:
- Xem lại đơn mình đã gửi ở dạng read-only.

Role khác:
- Không thấy nếu không có scope.
```

---

### 3.2. Đơn bị từ chối

Điều kiện:

```text
visit_requests.status = REJECTED
```

Hành vi:

```text
Mở preview read-only của đơn yêu cầu.
Hiển thị lý do từ chối nếu có decision_note.
Không vào trang quy trình tiếp khách.
```

Không hiển thị:

```text
- Trước tiếp khách
- Trong tiếp khách
- Sau tiếp khách
- Setup vận hành
- Logistics
- Biên bản
- Album
- Tin tức
```

---

### 3.3. Đơn đã hủy

Điều kiện:

```text
visit_requests.status = CANCELLED
hoặc visit_request_campuses.status = CANCELLED
```

Hành vi:

```text
Mở preview/detail read-only.
Hiển thị lý do hủy nếu có cancellation_reason.
Không cho thao tác setup.
```

Nếu cần xem lịch sử, chỉ hiển thị dạng read-only.

---

### 3.4. Đơn đã duyệt nhưng chưa gán Host

Điều kiện:

```text
visit_requests.status = APPROVED
visit_request_campuses.status = WAITING_HOST_ASSIGNMENT
current_host_user_id IS NULL
```

Hành vi:

```text
Mở detail nhẹ sau duyệt.
Chưa hiển thị đầy đủ trang quy trình tiếp khách.
```

Nội dung detail nhẹ:

```text
- Tổng quan campus instance.
- Yêu cầu gốc / form khách gửi, read-only.
- Trạng thái: Chờ phân công Host.
- Action gán Host nếu current user là Staff Leader đúng campus.
```

Không hiển thị:

```text
- Setup vận hành đầy đủ
- Agenda chính thức
- Logistics
- Biên bản
- Album
- Tin tức
- Nút chuyển giai đoạn
- Nút đóng đoàn
```

---

### 3.5. Đã gán Host

Điều kiện:

```text
visit_requests.status = APPROVED
current_host_user_id IS NOT NULL
visit_request_campuses.status IN:
- ASSIGNED
- BEFORE_VISIT
- DURING_VISIT
- AFTER_VISIT
- CLOSED
```

Hành vi:

```text
Mở trang quy trình tiếp khách đầy đủ.
```

Cấu trúc:

```text
Header thông tin đoàn/campus
1. Tổng quan
2. Yêu cầu gốc / Form đã gửi
3. Trước tiếp khách
4. Trong tiếp khách
5. Sau tiếp khách
6. Timeline / Nhật ký
```

---

## 4. Rule gán Host

### 4.1. Host chỉ gán một lần

Rule bắt buộc:

```text
Host chỉ được gán một lần cho mỗi visit_instance_id.
Không có chức năng đổi Host trong phase này.
```

Backend phải enforce:

```text
Nếu current_host_user_id đã có giá trị:
→ Không cho update lại Host.
→ Trả business error tiếng Việt sạch.
```

Frontend phải enforce:

```text
Nếu current_host_user_id đã có giá trị:
→ Ẩn nút Gán Host.
→ Không hiển thị chức năng Đổi Host.
```

Không implement:

```text
- Đổi Host.
- Reassign Host.
- Transfer Host.
- Override Host.
```

Nếu sau này có sự cố như Host nghỉ việc/sai Host thì tạo UC riêng.

---

### 4.2. Ai được gán Host

Staff Leader đúng campus được gán Host khi:

```text
- Request đã APPROVED.
- Campus instance đang WAITING_HOST_ASSIGNMENT.
- current_host_user_id IS NULL.
- Candidate là STAFF + STAFF.
- Candidate ACTIVE.
- Candidate thuộc cùng campus.
- Candidate thuộc phòng IC active.
- Candidate không conflict lịch theo rule hiện có.
```

HO không gán Host thay Staff Leader nếu nghiệp vụ hiện tại không cho phép.

Visitor không gán Host.

---

## 5. Tab “Yêu cầu gốc / Form đã gửi”

Tab này là snapshot dữ liệu khách đã gửi trước khi duyệt.

### 5.1. Nguyên tắc

```text
- Giống preview trước khi duyệt.
- Read-only 100%.
- Không có nút sửa.
- Không có nút lưu.
- Không chứa dữ liệu Host setup.
- Nên reuse component/DTO preview trước duyệt.
```

Nên hiển thị nhãn:

```text
Dữ liệu gốc do khách gửi — chỉ đọc
```

### 5.2. Nội dung cần có

#### Nhóm 1 — Thông tin người đăng ký

```text
- Họ và tên
- Quốc tịch
- Đơn vị công tác
- Chức danh / phòng ban
- Số điện thoại
- Email
```

#### Nhóm 2 — Thông tin chuyến thăm / đoàn khách

```text
- Tên đoàn khách
- Cơ sở tới thăm: một cơ sở / liên cơ sở
- Thời gian dự kiến từng cơ sở
  + Cơ sở
  + Ngày bắt đầu
  + Thời gian bắt đầu
  + Thời gian kết thúc
- Mục đích thăm
- Nội dung làm việc / nội dung mong muốn làm việc
```

Nếu liên cơ sở, hiển thị bảng:

```text
| Cơ sở | Ngày | Bắt đầu | Kết thúc |
```

#### Nhóm 3 — Thành phần tham dự & liên hệ

```text
- Danh sách khách:
  + STT
  + Họ tên
  + Chức vụ
  + Đơn vị công tác
  + Quốc tịch
  + Email / số điện thoại nếu form có

- File danh sách khách nếu khách upload Excel.

- Team hỗ trợ khách ngoài hệ thống:
  + Họ tên
  + Vai trò / chức vụ
  + Đơn vị công tác
  + Quốc tịch
  + Email / số điện thoại nếu form có

- Thông tin đầu mối liên hệ:
  + Họ tên
  + Đơn vị công tác
  + Số điện thoại
  + Email
```

#### Nhóm 4 — Yêu cầu & xác nhận bổ sung

```text
- Ngôn ngữ sử dụng
- Xác nhận sử dụng hình ảnh & truyền thông
- Nhận diện phương tiện di chuyển tới FPTU
- Ghi chú cho FPTU
```

### 5.3. Không đưa vào “Yêu cầu gốc”

Các phần sau không phải dữ liệu khách submit nên không nằm trong tab này:

```text
- Agenda chính thức
- Tài liệu lưu trữ do Host/IC upload
- Công văn/Profile đoàn do Host lưu sau duyệt
- Logistics/resource nội bộ
- Phòng họp
- Teabreak
- Xe điện
- Người lái
- Welcome LED
- Staff hỗ trợ IC
- Department hỗ trợ
- Sinh viên hỗ trợ
- Ghi chú nội bộ
- Cảnh báo/nhắc việc nội bộ
- Biên bản
- Tin tức
- Album nội bộ
```

---

## 6. Tab “Tổng quan”

Tab này là dashboard ngắn để người xem hiểu tình hình.

Nội dung:

```text
- Tên đoàn
- Mã đơn
- Campus hiện tại
- Loại đơn: single-campus / multi-campus
- Host hiện tại
- Thời gian tiếp tại campus
- Trạng thái request tổng
- Trạng thái campus instance
- Tiến độ chuẩn bị
- Việc còn thiếu
- Cảnh báo quan trọng
```

Ví dụ cảnh báo:

```text
- Chưa có agenda chính thức.
- Chưa gửi yêu cầu phòng họp.
- Teabreak đang chờ xác nhận.
- Có nhân sự từ chối tham gia.
- Chưa upload tài liệu.
```

---

## 7. Tab “Trước tiếp khách”

Tab này là phần Host setup vận hành sau khi đã được gán.

### 7.1. Nội dung

```text
1. Agenda chính thức
- Thời gian
- Nội dung
- Địa điểm
- Người phụ trách
- Ghi chú nội bộ

2. Thành phần tham gia nội bộ
- Host
- Staff hỗ trợ IC
- Phòng ban hỗ trợ
- Sinh viên hỗ trợ

3. Logistics / Resource
- Welcome LED
- Campus tour
- Người dẫn tour
- Xe điện
- Người lái
- Phòng họp
- Teabreak
- Khác

4. Tài liệu lưu trữ
- Công văn
- Profile đoàn
- Tài liệu liên quan
- Biên bản / tài liệu chuẩn bị
- File nội bộ khác

5. Cảnh báo & nhắc việc
- Nhắc Host
- Nhắc người tham gia
- Nhắc phòng ban

6. Ghi chú chung
- Ghi chú vận hành nội bộ
```

### 7.2. Quyền

```text
Host:
- Xem/sửa/toàn quyền trong campus instance mình.

Staff Leader:
- Xem read-only.
- Không sửa setup.
- Không gửi logistics request thay Host.
- Không chuyển giai đoạn.
- Không đóng đoàn.

HO:
- Xem summary/read-only.
- Không sửa setup từng campus.

Người được mời:
- Xem phần liên quan.
- Có thể ACCEPT/DECLINE lời mời tham gia của mình.

Visitor:
- Chỉ xem bản public nếu được phép.
- Không thấy logistics nội bộ, ghi chú nội bộ, phòng ban xử lý.
```

---

## 8. Tab “Trong tiếp khách”

Tab này dùng khi đoàn đang diễn ra.

Nội dung chính:

```text
- Feedback
- Tạo đối tác / scan card
- Biên bản cuộc họp
- Tài liệu
- Thông tin khác
```

---

## 9. Biên bản cuộc họp

### 9.1. Rule chính

```text
- Biên bản nằm trong tab “Trong tiếp khách”.
- Mỗi visit_instance_id chỉ có 1 biên bản.
- Chỉ người nội bộ được mời tham gia và đã ACCEPT mới được tạo/sửa biên bản.
- Chỉ 1 người được sửa biên bản tại một thời điểm.
- Visitor không tạo/sửa biên bản.
- HO và Staff Leader chỉ xem biên bản.
```

### 9.2. Ai được tạo/sửa biên bản

Có quyền tạo/sửa nếu thỏa một trong các điều kiện:

```text
- Là Host của visit_instance.
- Là IC Staff participant của visit_instance và status = ACCEPTED.
- Là Department participant của visit_instance và status = ACCEPTED.
- Là Student participant của visit_instance và status = ACCEPTED.
```

Không có quyền tạo/sửa:

```text
- Visitor.
- HO.
- Staff Leader.
- Người được mời nhưng chưa ACCEPT.
- Người đã DECLINED.
- Người bị REMOVED.
- Người không thuộc visit_instance.
```

### 9.3. Ràng buộc 1 biên bản / campus instance

Khuyến nghị DB:

```sql
UNIQUE KEY uq_minutes_visit_instance (visit_instance_id)
```

Nếu chưa muốn sửa DB:

```text
Backend vẫn phải check trước khi tạo.
Khi 2 người tạo cùng lúc, cần transaction/lock để tránh duplicate.
```

### 9.4. Cơ chế lock khi sửa

DB có thể dùng các field dạng:

```text
edit_locked_by
edit_locked_at
edit_lock_expires_at
edit_lock_token
row_version
```

Luồng:

```text
1. User mở tab biên bản.

2. Nếu chưa có biên bản:
   - User có quyền → hiện nút “Tạo biên bản”.
   - User không có quyền → chỉ hiển thị “Chưa có biên bản”.

3. User bấm “Tạo biên bản” hoặc “Sửa”.

4. Backend kiểm tra:
   - User có quyền không.
   - Visit instance đúng scope không.
   - Visit instance chưa CLOSED nếu không cho sửa sau đóng.
   - Biên bản có đang bị lock bởi người khác không.
   - Lock hết hạn chưa.

5. Nếu chưa bị lock:
   - Set edit_locked_by = currentUserId.
   - Set edit_locked_at = now.
   - Set edit_lock_expires_at = now + 10 phút.
   - Tạo edit_lock_token.
   - Trả token về frontend.

6. Trong lúc User A đang sửa:
   - User B/C chỉ xem read-only.
   - UI hiển thị “Nguyễn Văn A đang chỉnh sửa biên bản”.

7. User A bấm Save:
   - Backend kiểm tra đúng edit_lock_token.
   - Backend kiểm tra row_version nếu có.
   - Update content.
   - Tăng row_version.
   - Clear lock fields.
   - Người khác mới có thể sửa.

8. User A bấm Hủy:
   - Backend release lock nếu token đúng.
   - Không lưu thay đổi.

9. User A đóng tab không Save:
   - Lock tự hết hạn sau 10 phút.
   - Người khác có thể bấm sửa sau khi hết hạn.
```

### 9.5. UI trạng thái biên bản

#### Chưa có biên bản

Nếu user có quyền:

```text
Chưa có biên bản cho chuyến thăm này.
[Tạo biên bản]
```

Nếu user không có quyền:

```text
Chưa có biên bản cho chuyến thăm này.
```

#### Đã có biên bản, không ai sửa

```text
Biên bản cuộc họp
Trạng thái: Đã lưu nháp / Đã lưu
Cập nhật lần cuối: ...
Người cập nhật: ...

[Xem] [Sửa]
```

#### Có người khác đang sửa

```text
Biên bản đang được chỉnh sửa bởi Nguyễn Văn A.
Bạn chỉ có thể xem nội dung hiện tại. Quyền sửa sẽ mở lại sau khi người này lưu hoặc phiên sửa hết hạn.

[Xem]
```

#### Chính user đang sửa

```text
Bạn đang chỉnh sửa biên bản.
Phiên sửa còn 09:32

[Lưu] [Hủy chỉnh sửa]
```

---

## 10. Tab “Sau tiếp khách”

Tab này dùng sau khi tiếp khách.

Nội dung:

```text
- Upload album ảnh
- Đồng bộ Drive
- Scan/gán tên khuôn mặt
- Tạo bài tin tức
- Đóng đoàn
```

---

## 11. Tin tức

### 11.1. Rule chính

```text
Tin tức không giống biên bản.
Biên bản chỉ có 1 bản / campus instance.
Tin tức có thể có nhiều bài / campus instance.
```

### 11.2. Ai được viết tin tức

Đề xuất:

```text
- Host.
- IC Staff được mời và đã ACCEPT.
- Student được mời và đã ACCEPT.
```

Không nên cho Department viết tin tức trong phase này, trừ khi chốt rule mới.

```text
Department participant:
- Chỉ xem album/tin tức.
- Không viết tin tức mặc định.
```

Visitor:

```text
- Chỉ xem bài đã public/được chia sẻ.
- Không viết tin tức.
```

HO / Staff Leader:

```text
- Xem summary/read-only.
- Không viết bài thay người tham gia.
```

### 11.3. Workflow tin tức

Gợi ý:

```text
1. Người có quyền bấm “Tạo bài tin tức”.
2. Nhập tiêu đề, nội dung, ảnh liên quan.
3. Lưu nháp hoặc gửi duyệt.
4. Người có quyền review/publish xử lý theo UC news hiện có.
5. Visitor chỉ thấy bài đã published/public.
```

---

## 12. Timeline / Nhật ký

Timeline hiển thị lịch sử thao tác.

Nội dung:

```text
- Đơn được gửi.
- Đơn được duyệt/từ chối.
- Host được gán.
- Host hoàn tất chuẩn bị.
- Người tham gia ACCEPT/DECLINE.
- Logistics được gửi/xác nhận/từ chối.
- Biên bản được tạo/cập nhật.
- Bài tin tức được tạo/published.
- Đóng đoàn.
```

Quyền xem:

```text
Host:
- Xem đầy đủ trong campus instance.

Staff Leader:
- Xem read-only campus mình.

HO:
- Xem summary liên cơ sở.

Visitor:
- Chỉ xem timeline public.

Department/Student:
- Chỉ xem phần liên quan mình.

Admin:
- Không xem nghiệp vụ mặc định, chỉ audit kỹ thuật nếu có module riêng.
```

---

## 13. Phân quyền role sau khi đã gán Host

| Role | Tổng quan | Yêu cầu gốc | Trước tiếp khách | Trong tiếp khách | Sau tiếp khách | Timeline |
|---|---|---|---|---|---|---|
| Host | Xem | Read-only | Sửa/toàn quyền | Sửa/toàn quyền | Sửa/toàn quyền + đóng đoàn | Xem |
| Staff Leader | Xem | Read-only | Read-only | Read-only | Read-only | Xem |
| HO | Xem liên cơ sở | Read-only | Summary/read-only | Summary/read-only | Summary/read-only | Xem |
| Visitor | Xem public | Yêu cầu của tôi | Lịch trình public | Feedback nếu mở | Album/news public | Hạn chế |
| IC Staff accepted | Xem | Read-only | Xem + xác nhận phần mình | Biên bản/tài liệu/feedback theo quyền | Upload ảnh/news theo quyền | Liên quan |
| Dept participant accepted | Xem rút gọn | Rút gọn nếu cần | Xem + xác nhận phần mình | Feedback + biên bản | Xem album/news | Liên quan |
| Dept Leader nhận logistics | Xem rút gọn | Rút gọn nếu cần | Resource phòng mình | Feedback nếu có | Xem nếu liên quan | Liên quan |
| Dept Staff | Xem task | Không/rút gọn | Task của tôi | Task của tôi | Không/rút gọn | Task của tôi |
| Student accepted | Xem rút gọn | Rút gọn nếu cần | Xem + xác nhận phần mình | Feedback + biên bản | Upload ảnh/news | Liên quan |
| Admin | Không mặc định | Không | Không | Không | Không | Audit kỹ thuật nếu có |

---

## 14. Backend DTO/action flags đề xuất

Backend nên trả các boolean để frontend không tự đoán quá nhiều.

Ví dụ:

```ts
interface VisitProcessPermissionDto {
  canViewOriginalRequest: boolean;
  canViewOverview: boolean;

  canViewBeforeVisit: boolean;
  canEditBeforeVisit: boolean;

  canViewDuringVisit: boolean;
  canEditDuringVisit: boolean;

  canViewAfterVisit: boolean;
  canEditAfterVisit: boolean;

  canAssignHost: boolean;
  canCreateMinutes: boolean;
  canEditMinutes: boolean;
  canViewMinutes: boolean;

  canCreateNews: boolean;
  canViewNews: boolean;

  canCloseVisit: boolean;
}
```

Backend là source of truth:

```text
Frontend chỉ render theo permission flags.
Backend vẫn phải validate lại ở từng command.
Không tin frontend.
```

---

## 15. API/command gợi ý

### 15.1. Xem detail

```text
GET /api/visit-requests/{visitRequestId}/preview
→ dùng cho trước duyệt, rejected, cancelled, yêu cầu gốc.

GET /api/visit-instances/{visitInstanceId}/process-detail
→ dùng sau khi đã approved/assigned.
```

### 15.2. Gán Host

```text
POST /api/visit-instances/{visitInstanceId}/assign-host
```

Validate:

```text
- Current user là Staff Leader đúng campus.
- Request APPROVED.
- Instance WAITING_HOST_ASSIGNMENT.
- current_host_user_id IS NULL.
- Candidate hợp lệ.
- Không conflict lịch.
```

### 15.3. Biên bản

```text
GET /api/visit-instances/{visitInstanceId}/minutes
POST /api/visit-instances/{visitInstanceId}/minutes/create-or-lock
POST /api/minutes/{minutesId}/acquire-lock
PUT /api/minutes/{minutesId}/save
POST /api/minutes/{minutesId}/release-lock
```

Validate:

```text
- User có quyền.
- User accepted participant hoặc Host.
- Lock token đúng khi save/release.
- Không cho ghi đè nếu row_version lệch.
```

### 15.4. Tin tức

```text
GET /api/visit-instances/{visitInstanceId}/news
POST /api/visit-instances/{visitInstanceId}/news
PUT /api/news/{newsId}
POST /api/news/{newsId}/submit-review
```

Validate:

```text
- User có quyền viết tin tức.
- User thuộc visit instance.
- User là Host / IC Staff accepted / Student accepted.
```

---

## 16. Frontend implementation notes

### 16.1. Không dùng statusText để gate logic

Sai:

```ts
row.statusText === 'Đã duyệt'
row.statusText === 'Chờ duyệt'
```

Đúng:

```ts
row.requestStatus === 'APPROVED'
row.instanceStatus === 'WAITING_HOST_ASSIGNMENT'
```

Tốt nhất:

```ts
row.canAssignHost
permissions.canEditBeforeVisit
permissions.canCreateMinutes
```

### 16.2. Route decision

Khi bấm xem:

```ts
if (requestStatus === 'PENDING_APPROVAL') {
  openPreviewReadonlyOrApprovalModal();
}

if (requestStatus === 'REJECTED') {
  openPreviewWithRejectReason();
}

if (requestStatus === 'CANCELLED') {
  openPreviewWithCancelReason();
}

if (requestStatus === 'APPROVED' && instanceStatus === 'WAITING_HOST_ASSIGNMENT') {
  openPostApprovalLightDetail();
}

if (requestStatus === 'APPROVED' && currentHostUserId) {
  navigateToVisitProcess();
}
```

### 16.3. Component tách riêng

Gợi ý component:

```text
VisitRequestPreviewReadonly
PostApprovalLightDetail
VisitProcessPage
OriginalRequestTab
BeforeVisitSetupTab
DuringVisitTab
MinutesCard
AfterVisitTab
NewsList
TimelineTab
```

---

## 17. Acceptance criteria

### Trước duyệt

```text
[ ] PENDING_APPROVAL chỉ mở preview đơn.
[ ] Không hiển thị tab Trước/Trong/Sau tiếp khách.
[ ] HO/Staff Leader vẫn duyệt/từ chối đúng scope.
[ ] Visitor chỉ xem read-only.
```

### Sau duyệt chưa gán Host

```text
[ ] APPROVED + WAITING_HOST_ASSIGNMENT mở detail nhẹ.
[ ] Staff Leader đúng campus thấy nút Gán Host.
[ ] Không có nút Đổi Host.
[ ] Visitor thấy “Đang phân công Host”.
[ ] Không hiển thị setup nội bộ đầy đủ.
```

### Gán Host

```text
[ ] Host chỉ gán được khi current_host_user_id null.
[ ] Sau khi gán, current_host_user_id có giá trị.
[ ] Không thể gán lại bằng UI.
[ ] Gọi API trực tiếp để gán lại bị backend chặn.
```

### Sau khi đã gán Host

```text
[ ] Mở được VisitProcess.
[ ] Host sửa được tab Trước/Trong/Sau theo phase.
[ ] Staff Leader/HO chỉ read-only.
[ ] Visitor chỉ thấy public-safe view.
[ ] Yêu cầu gốc giống preview trước duyệt và read-only.
```

### Biên bản

```text
[ ] Mỗi visit_instance chỉ có 1 biên bản.
[ ] Host tạo/sửa được.
[ ] IC Staff accepted tạo/sửa được.
[ ] Department participant accepted tạo/sửa được.
[ ] Student accepted tạo/sửa được.
[ ] Người invited nhưng chưa ACCEPT không sửa được.
[ ] Visitor không tạo/sửa được.
[ ] HO/Staff Leader chỉ xem.
[ ] Khi User A đang sửa, User B chỉ xem.
[ ] User A save xong thì User B mới sửa được.
[ ] Lock hết hạn thì người khác có thể nhận quyền sửa.
```

### Tin tức

```text
[ ] Host tạo bài tin được.
[ ] IC Staff accepted tạo bài tin được.
[ ] Student accepted tạo bài tin được.
[ ] Visitor không tạo bài tin.
[ ] Department mặc định không tạo bài tin nếu chưa chốt rule khác.
[ ] Có thể có nhiều bài tin cho một visit_instance.
```

---

## 18. Manual test checklist

### Test Staff Leader

```text
[ ] Xem PENDING_APPROVAL single-campus → chỉ preview + duyệt/từ chối.
[ ] Xem APPROVED + WAITING_HOST_ASSIGNMENT → thấy nút Gán Host.
[ ] Gán Host thành công.
[ ] Sau khi gán Host → không còn nút Gán Host / Đổi Host.
[ ] Vào VisitProcess → chỉ read-only.
```

### Test Host

```text
[ ] Sau khi được gán Host → vào VisitProcess.
[ ] Xem yêu cầu gốc read-only.
[ ] Sửa Trước tiếp khách.
[ ] Tạo agenda/logistics/tài liệu.
[ ] Vào Trong tiếp khách → tạo biên bản.
[ ] Vào Sau tiếp khách → tạo album/news/đóng đoàn theo quyền.
```

### Test người tham gia accepted

```text
[ ] INVITED chưa accept → không sửa biên bản.
[ ] ACCEPTED → sửa/tạo biên bản được.
[ ] Khi người khác đang sửa → chỉ xem.
[ ] Sau khi lock release → sửa được.
```

### Test Visitor

```text
[ ] PENDING_APPROVAL → xem yêu cầu của tôi read-only.
[ ] APPROVED chưa gán Host → thấy đang phân công Host.
[ ] Sau khi có Host → thấy public view.
[ ] Không thấy logistics nội bộ.
[ ] Không tạo/sửa biên bản.
[ ] Không tạo tin tức.
```

---

## 19. Prompt ngắn cho AI Agent

```text
Bạn là Senior Full-stack Engineer cho dự án PEMS.

Hãy triển khai logic trang chi tiết visit/delegation theo đặc tả trong file này.

Bắt buộc:
1. Trước duyệt chỉ mở preview đơn yêu cầu, không vào VisitProcess.
2. Đã duyệt nhưng chưa gán Host chỉ mở detail nhẹ, Staff Leader được gán Host một lần.
3. Không có chức năng đổi Host.
4. Sau khi đã gán Host mới vào VisitProcess đầy đủ.
5. Tab Yêu cầu gốc phải reuse preview trước duyệt, read-only, không chứa dữ liệu setup.
6. Host setup nằm ở Trước tiếp khách.
7. Biên bản: 1 bản / visit_instance, chỉ Host hoặc participant ACCEPTED được tạo/sửa.
8. Biên bản phải có lock: chỉ 1 người sửa tại một thời điểm.
9. Tin tức có thể nhiều bài, Host/IC Staff accepted/Student accepted được viết.
10. Frontend không dùng statusText để gate logic.
11. Backend phải validate scope/permission ở mọi command.

Sau khi sửa, báo cáo:
- Files read
- Files changed
- Route decision logic
- Permission logic
- Host assignment rule
- Minutes lock logic
- News permission
- Build result
- Manual test checklist
```

---

## 20. Ghi chú triển khai

Không cần làm tất cả trong một commit lớn. Nên chia phase:

```text
Phase 1:
- Route decision theo status.
- Preview trước duyệt.
- Detail nhẹ sau duyệt chưa gán Host.
- Host assign one-time.

Phase 2:
- VisitProcess role-based tabs.
- OriginalRequestTab read-only reuse preview.

Phase 3:
- Minutes one-per-instance + lock editing.

Phase 4:
- News permission + after visit refinements.
```

Ưu tiên phase 1 trước để tránh sai luồng nghiệp vụ nền tảng.
