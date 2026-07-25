# PEMS — KẾ HOẠCH TRIỂN KHAI LUỒNG TẠO ĐOÀN KHÁCH V2 VÀ ĐỒNG BỘ UI XEM ĐƠN

## 1. Mục đích tài liệu

Tài liệu này là prompt triển khai dành cho AI Agent/Developer tiếp tục hoàn thiện hai nhóm công việc:

1. **Hoàn thiện luồng tạo đoàn khách khi người dùng đã đăng nhập**
   - Nút “Tôi là người đăng ký”.
   - Tự điền thông tin hồ sơ hiện tại.
   - Phân biệt email trùng/khác email tài khoản đăng nhập.
   - Không gửi OTP khi đúng chính chủ.
   - Bắt buộc OTP khi tạo đơn hộ người khác.
   - Chỉ cho Staff/Staff Leader tự xử lý hoặc gán Host khi chính họ là người đăng ký.
   - Kiểm tra và hoàn thiện luồng dữ liệu Pure V2 tại trang xử lý.
   - Chuẩn hóa bảng danh sách đoàn và trạng thái hiển thị.
   - Rà soát các nhiệm vụ còn thiếu trong tài liệu bàn giao.

2. **Thiết kế lại UI màn Xem đơn V2**
   - Đồng nhất phong cách, màu sắc, bố cục và cách hiển thị dữ liệu với trang Xử lý đơn.
   - Giữ nguyên nghiệp vụ, quyền, API, routing và dữ liệu backend.
   - Chuẩn hóa section, badge, bảng danh sách người, panel quyết định và timeline.

Tài liệu phải được thực hiện theo từng slice nhỏ, có kiểm thử, không sửa dàn trải và không tuyên bố hoàn thành khi chưa đạt toàn bộ Definition of Done.

---

# 2. Vai trò của AI Agent

Bạn là:

- Senior Full-stack Engineer.
- Senior .NET Clean Architecture Developer.
- Senior React TypeScript Engineer.
- MySQL Database-first Engineer.
- Security và Authorization Reviewer.
- UI/UX Enterprise Dashboard Reviewer.
- QA/Test Engineer.

Khi triển khai phải đồng bộ đầy đủ chuỗi:

```text
Business rule
→ API contract
→ Backend authorization
→ Backend validation
→ Persistence
→ Frontend type/API
→ Frontend form
→ UI permission visibility
→ Read/detail/process screen
→ Test
→ Build
→ Manual verification
→ Report
```

Không được chỉ sửa frontend mà bỏ backend guard.  
Không được chỉ sửa backend mà để frontend tiếp tục gửi payload sai.  
Không được thay đổi nghiệp vụ chỉ để làm UI dễ hơn.

---

# 3. Nguồn sự thật và quy tắc ưu tiên

Trước khi sửa code phải đọc và đối chiếu:

```text
1. Code và test thực tế tại HEAD hiện tại.
2. SQL/database schema hiện tại.
3. PEMS_CANONICAL_BUSINESS_RULES...
4. PEMS_PER_CAMPUS_V2_MASTER_HANDOFF_PROMPT...
5. PEMS_UC_IMPLEMENTATION_RULEBOOK...
6. VISITOR_MANAGEMENT_SYSTEM...
7. PERMISSION_RULES / PERMISSION_MATRIX chỉ dùng phần còn hợp lệ.
8. PEMS_UI_DESIGN_SYSTEM_PROMPT.
9. Tài liệu legacy chỉ dùng để tham khảo lịch sử.
```

Quy tắc bắt buộc:

- Backend là nguồn quyết định cuối cùng.
- Frontend chỉ ẩn/hiện đúng quyền và tránh gọi API sai.
- Không dùng raw role label sai chuẩn.
- Staff Leader là `role_code = STAFF`, `sub_role = LEADER`.
- IC Staff là `role_code = STAFF`, `sub_role = STAFF`.
- Không dùng `STAFF_LEADER` như `role_code`.
- Không tự thêm status, enum, field hoặc table nếu schema chưa có.
- Không tạo mock data để che API chưa hoàn thiện.
- Không lấy campus đầu tiên làm đại diện cho request mixed.
- Không fallback ngầm từ V2 về V1.

---

# PHẦN A — HOÀN THIỆN LUỒNG TẠO ĐOÀN KHÁCH KHI ĐÃ ĐĂNG NHẬP

# 4. Hiện trạng cần xác minh

Trước khi sửa, rà soát code và ghi evidence cho các điểm sau:

```text
[ ] Form authenticated hiện có tự điền người đăng ký hay không.
[ ] Nút hiện tại “Dùng thông tin người đăng ký” đang copy sang phần nào.
[ ] Authenticated create có bypass OTP với mọi email hay không.
[ ] Backend có cho registrant_user_id và registrant_email thuộc hai người khác nhau hay không.
[ ] Processing controls có phụ thuộc email người đăng ký trùng actor hay không.
[ ] Staff/Staff Leader có thể gửi processing intent khi tạo hộ người khác hay không.
[ ] Primary Contact có đang cho copy từ internal actor và dẫn tới lỗi backend hay không.
[ ] Trang xử lý có đọc đúng instance-level Pure V2 hay không.
[ ] Trang xử lý còn mock/dead state nào hay không.
[ ] Màn Xem đơn có thiếu STT, badge tiếng Việt hoặc hiển thị raw enum hay không.
```

Báo cáo audit phải nêu rõ:

- File.
- Hàm/component.
- Dòng logic.
- Tác động.
- Mức độ ưu tiên.
- Cách sửa đề xuất.

---

# 5. Yêu cầu nghiệp vụ đã chốt

## 5.1 Nút “Tôi là người đăng ký”

Trong phần **Thông tin người đăng ký**, thêm nút:

```text
Tôi là người đăng ký
```

Khi bấm:

- Gọi API hồ sơ người dùng hiện tại.
- Điền vào form:
  - Họ và tên.
  - Email.
  - Số điện thoại.
  - Quốc tịch.
  - Chức vụ.
  - Đơn vị công tác.
- Không tự động ghi đè dữ liệu ngay khi mở form.
- Không tự động ghi đè bản nháp đã khôi phục.
- Chỉ điền khi người dùng chủ động bấm.
- Nếu một trường hồ sơ không có dữ liệu, để trống và yêu cầu người dùng bổ sung.
- Không lấy label role thay cho chức vụ nếu hồ sơ có chức danh thực tế.

Hiển thị trạng thái:

```text
Email trùng với tài khoản đang đăng nhập — không cần xác minh OTP.
```

Nếu người dùng sửa email thành email khác, trạng thái này phải biến mất ngay.

---

## 5.2 Chuẩn hóa email

So sánh email bằng:

```text
trim
+
lowercase
```

Không được:

- Tự bỏ dấu chấm Gmail.
- Tự bỏ `+alias`.
- Tự sửa domain.
- So sánh case-sensitive.
- Tin vào email frontend mà không kiểm tra backend.

Nên có helper dùng chung ở frontend và backend theo cùng quy tắc.

---

## 5.3 Luồng khi email trùng tài khoản đăng nhập

Điều kiện:

```text
normalized(form.registerInfo.email)
=
normalized(currentUser.email)
```

Kết quả:

- Không gửi OTP.
- Gọi authenticated create V2 trực tiếp.
- `registrant_user_id` phải bằng current user.
- Snapshot người đăng ký được lưu từ form đã validate.
- Backend vẫn kiểm tra tài khoản ACTIVE và đúng role được phép tạo đơn.
- Backend vẫn kiểm tra form Pure V2 đầy đủ.
- Không dựa vào frontend để kết luận chính chủ.

---

## 5.4 Luồng khi email khác tài khoản đăng nhập

Điều kiện:

```text
normalized(form.registerInfo.email)
!=
normalized(currentUser.email)
```

Kết quả:

- Không được gọi authenticated direct-create.
- Phải gửi OTP đến email người đăng ký được nhập.
- Chỉ tạo đơn sau khi OTP đúng.
- Không cấp ownership/relation trước OTP.
- Không cho actor nội bộ xử lý trực tiếp campus trong đơn tạo hộ.
- Không gửi processing intent trong payload.
- Mọi campus phải đi theo luồng chờ Staff Leader của campus xử lý.
- OTP replay hoặc double-submit không được tạo đơn thứ hai.
- Submission phải idempotent.

Backend direct-create khi nhận email khác actor phải trả error code ổn định, ví dụ:

```text
REGISTRANT_EMAIL_VERIFICATION_REQUIRED
```

Không chỉ trả message text khó xử lý.

---

## 5.5 Không nhầm OTP người đăng ký với Primary Contact Claim

Hai luồng phải tách biệt:

### OTP người đăng ký

Mục tiêu:

```text
Chứng minh người được ghi là người đăng ký sở hữu email.
```

### Primary Contact Claim

Mục tiêu:

```text
Mời đầu mối chính khác người đăng ký nhận quyền quản lý đơn.
```

Không được:

- Dùng Primary Contact Claim thay OTP người đăng ký.
- Cho email khác actor được tạo đơn trực tiếp chỉ vì sau đó có claim.
- Tự link visitor owner trước explicit accept nếu nghiệp vụ claim yêu cầu accept.

---

# 6. Quy tắc hiển thị và gửi lựa chọn xử lý campus

Khối **Cách xử lý tại cơ sở này** chỉ hiển thị khi đồng thời thỏa mãn:

```text
user đã đăng nhập
AND email người đăng ký trùng email tài khoản hiện tại
AND role_code = STAFF
AND sub_role IN (STAFF, LEADER)
```

Nếu email khác actor:

- Ẩn toàn bộ processing controls.
- Xóa processing choices cũ khỏi state khi email bị thay đổi.
- Không gửi `processing` trong payload.
- Backend reject nếu client cố tình gửi.

---

## 6.1 Quyền của IC Staff

Actor:

```text
role_code = STAFF
sub_role = STAFF
department_type = IC
```

Tại campus chính của mình:

- **Tôi sẽ làm Host**.
- **Để Staff Leader xử lý sau**.

Không được:

- Gán Host cho người khác.
- Tự xử lý campus khác.
- Tự Host nếu không thuộc IC department hợp lệ.
- Gửi direct mode khi đang tạo hộ người khác.

---

## 6.2 Quyền của Staff Leader

Actor:

```text
role_code = STAFF
sub_role = LEADER
department_type = IC
```

Tại campus chính của mình:

- **Tôi sẽ làm Host**.
- **Gán Host cho IC Staff**.
- **Để xử lý sau**.

Tại campus khác:

- Chỉ hiển thị thông báo read-only:
  - Đơn sẽ được chuyển đến Staff Leader của campus đó.
- Không gửi processing intent.

Host candidate bắt buộc:

```text
status = ACTIVE
role_code = STAFF
sub_role = STAFF
primary_campus_id = actor.primary_campus_id
department_type = IC
department.status = ACTIVE
```

Không được hiển thị:

- Staff Leader khác.
- Department user.
- Student.
- Visitor.
- HO.
- Admin.
- User khác campus.
- User inactive/locked.

---

## 6.3 Default processing

Nếu không có direct mode hợp lệ:

```text
SEND_FOR_REVIEW
```

Ý nghĩa UI:

```text
Để Staff Leader xử lý sau
```

Không cần tạo thêm status database chỉ để biểu diễn câu chữ UI.

---

# 7. Primary Contact khi actor là nhân sự nội bộ

Nếu backend quy định nhân sự nội bộ không thể là Primary Contact của đoàn khách:

- Với Visitor:
  - Cho phép nút “Dùng thông tin người đăng ký”.
- Với Staff/Staff Leader:
  - Ẩn hoặc disable nút đó.
  - Hiển thị helper text:

```text
Đầu mối liên hệ phải là người đại diện phía đoàn khách, không phải nhân sự nội bộ đang tạo hộ.
```

Frontend không được cho người dùng nhập xong rồi chỉ báo lỗi khi submit nếu có thể ngăn sớm bằng UI.

Backend vẫn phải validate lại.

---

# 8. Thiết kế API/Backend đề xuất

## 8.1 Direct authenticated create

Backend kiểm tra:

```text
actor authenticated
actor ACTIVE
actor role hợp lệ
normalized registrant email = normalized actor email
full V2 validation
processing authorization
idempotency
```

Nếu email khác:

```text
409 hoặc 400 theo convention hiện tại
errorCode = REGISTRANT_EMAIL_VERIFICATION_REQUIRED
```

Không tạo dữ liệu.

---

## 8.2 Authenticated delegated OTP initiate

Cần endpoint hoặc command riêng có trách nhiệm:

- Nhận full V2 form.
- Validate toàn bộ form.
- Validate actor có quyền tạo đơn.
- Xóa/reject processing intent nếu registrant email khác actor.
- Tạo OTP challenge.
- Lưu snapshot hash/bound snapshot.
- Lưu submission ID.
- Không tạo visit request trước OTP.
- Rate limit.
- Resend invalidates OTP cũ.
- Không log raw OTP.

Tên route phải theo convention hiện có; không tự tạo route tùy tiện nếu hệ thống đã có pattern tương đương.

---

## 8.3 Authenticated delegated OTP verify-create

Khi OTP đúng:

- Load đúng pending snapshot.
- Validate challenge còn hạn.
- Validate submission ID.
- Validate snapshot không bị thay đổi.
- Tạo Pure V2 request.
- Không tạo processing direct mode.
- Tạo N campus instance.
- Tạo N detail snapshot.
- Tạo guest/support member links đúng từng campus.
- Tạo revision/audit.
- Route đến Staff Leader từng campus.
- Mark OTP challenge used.
- Commit trong transaction.
- Retry không tạo duplicate.

---

## 8.4 Error codes bắt buộc

Tối thiểu xem xét:

```text
REGISTRANT_EMAIL_VERIFICATION_REQUIRED
REGISTRANT_OTP_INVALID
REGISTRANT_OTP_EXPIRED
REGISTRANT_OTP_RATE_LIMITED
REGISTRANT_OTP_ALREADY_USED
REGISTRANT_SUBMISSION_NOT_FOUND
REGISTRANT_SUBMISSION_MISMATCH
INVALID_CAMPUS_SUBMISSION_MODE
SELF_HOST_NOT_ELIGIBLE
INVALID_HOST_CANDIDATE
```

Frontend phải map message tiếng Việt rõ ràng.

---

# 9. Kiểm tra Pure V2 tại trang xử lý

Rà soát luồng:

```text
route
→ frontend API
→ controller
→ query handler
→ IVisitFormReadService
→ visit_instance_form_details
→ visit_instance_guest_members
→ DTO
→ UI
```

Phải chứng minh:

```text
[ ] Trang xử lý dùng visitInstanceId làm scope.
[ ] Đọc đúng detail của campus hiện tại.
[ ] Không dùng sibling campus.
[ ] Không lấy campus đầu tiên.
[ ] Request mixed vẫn trả đúng 200 cho instance-level view.
[ ] Guest/support đúng instance.
[ ] Lịch đúng instance.
[ ] Operational contact đúng instance.
[ ] Missing detail trả lỗi ổn định.
[ ] Không fallback V1.
```

Nếu đã đúng thì không viết lại luồng. Chỉ bổ sung test và dọn phần chưa hoàn chỉnh.

---

# 10. Xóa mock/dead state tại trang xử lý

Rà soát và loại bỏ:

- Tên người mẫu hardcode.
- Campus hardcode không dùng.
- Ngày giờ mẫu.
- Participant mẫu.
- Album/news mẫu giả.
- State không được render.
- State được render nhưng không nối API thật.
- Fallback giả khiến người dùng tưởng dữ liệu đã lưu.

Quy tắc:

- Có API thật → nối dữ liệu thật.
- Chưa có API → hiển thị trạng thái “Chưa hỗ trợ” hoặc ẩn theo scope đã chốt.
- Không fake thành dữ liệu thật.

---

# 11. Chuẩn hóa trạng thái hiển thị

Không hiển thị raw enum như:

```text
WAITING_REQUEST_APPROVAL
BEFORE_VISIT
DURING_VISIT
AFTER_VISIT
CLOSED
INTERNAL_SELF_HOST
```

Tạo helper/component dùng chung:

```text
VisitStatusBadge
```

Mapping tiếng Việt tối thiểu:

```text
WAITING_REQUEST_APPROVAL → Chờ Staff Leader xử lý
PENDING_APPROVAL         → Chờ duyệt
PARTIALLY_APPROVED       → Đã duyệt một phần
APPROVED                 → Đã duyệt
ASSIGNED                 → Đã duyệt và gán Host
BEFORE_VISIT             → Đang chuẩn bị
DURING_VISIT             → Đang tiếp khách
AFTER_VISIT              → Sau tiếp khách
CLOSED                   → Đã đóng đoàn
REJECTED                 → Đã từ chối
CANCELLED                → Đã hủy
COMPLETED                → Hoàn tất
```

Phải đối chiếu status thật trong code/SQL trước khi thêm mapping.

---

# PHẦN B — ĐỒNG BỘ UI MÀN XEM ĐƠN V2 VỚI TRANG XỬ LÝ ĐƠN

# 12. Mục tiêu UI

Thiết kế lại màn **Xem đơn V2** để nhìn cùng một hệ thống với màn **Xử lý đơn**.

Không sao chép cứng toàn bộ trang xử lý, nhưng phải đồng nhất:

- Màu sắc.
- Section header.
- Số thứ tự.
- Badge.
- Typography.
- Spacing.
- Card.
- Label/value layout.
- Bảng danh sách người.
- Panel quyết định.
- Responsive.

Bộ màu:

```text
Primary blue:   #004c91
Primary orange: #F37021
Text primary:   slate-800 / slate-900
Text secondary: slate-500 / slate-600
Border:         slate-200 / slate-300
Page background: slate-50
Card background: white
```

Không dùng:

- Gradient mạnh.
- Shadow đậm.
- Quá nhiều màu.
- Card lồng card không cần thiết.
- Animation trang trí.
- Raw technical value.

---

# 13. Phạm vi file cần rà soát

Tối thiểu kiểm tra:

```text
frontend/.../VisitRequestV2DetailPage.tsx
frontend/.../VisitRequestV2DetailView.tsx
frontend/.../CampusVisitDetailCard.tsx
frontend/.../VisitHistoryTimeline.tsx
frontend/.../RequestInfoReadOnly.tsx
frontend/.../VisitProcess.tsx
frontend/.../visitRequestV2 translation files
frontend shared components liên quan badge/table/card
```

Không thay đổi API contract chỉ để phục vụ layout nếu dữ liệu đã có.

---

# 14. Bố cục màn Xem đơn mới

## 14.1 Card tổng quan đầu trang

Hiển thị:

```text
Mã đơn
Badge trạng thái
Badge số cơ sở
Thời điểm gửi
Người đăng ký tóm tắt
Đầu mối liên hệ tóm tắt
Các action được backend cho phép
```

Yêu cầu:

- Mã đơn nổi bật.
- Không lặp quá nhiều thông tin.
- Các button action vẫn dựa vào `allowedActions`.
- Không tự suy luận quyền từ role/status ở frontend.
- Không thay đổi route.

---

## 14.2 Các section chính

Thiết kế theo phong cách trang xử lý:

```text
① THÔNG TIN NGƯỜI ĐĂNG KÝ
② ĐẦU MỐI LIÊN HỆ CỦA ĐƠN
③ THÔNG TIN TỪNG CƠ SỞ
④ LỊCH SỬ THAY ĐỔI
```

Header:

- Nền xanh `#004c91`.
- Chữ trắng.
- Số thứ tự tròn màu cam.
- Badge “Chỉ đọc” khi phù hợp.
- Có icon collapse nếu section được thu gọn.
- Keyboard accessible.

---

# 15. Thông tin người đăng ký và đầu mối

Dùng layout hai cột giống trang xử lý.

Ví dụ:

```text
Họ và tên                 Đơn vị / tổ chức
Chức vụ                   Số điện thoại
Quốc tịch                 Email
```

Yêu cầu:

- Label màu slate-500.
- Value màu slate-800.
- Không dùng paragraph dài.
- Empty value hiển thị “—”.
- Email/phone dài không phá layout.
- Mobile chuyển một cột.

Đầu mối liên hệ phải có:

- Họ và tên.
- Đơn vị.
- Số điện thoại.
- Email.
- Trạng thái xác nhận nếu có.

---

# 16. Card từng campus

Mỗi campus là một card riêng.

Header campus gồm:

- Icon campus.
- Tên campus.
- Badge trạng thái tiếng Việt.
- Khoảng thời gian.
- Badge amendment nếu có.
- Action campus nếu backend cho phép.

Nội dung dùng label/value:

```text
Tên đoàn
Loại hình
Mục đích
Nội dung làm việc
Ngôn ngữ
Đồng ý truyền thông
Phương tiện
Ghi chú
Đầu mối phối hợp tại cơ sở
```

Không được:

- Trộn dữ liệu nhiều campus.
- Dùng request-level snapshot thay instance-level detail.
- Lấy first campus làm đại diện.
- Hiển thị raw enum.

---

# 17. Bảng danh sách đoàn

## 17.1 Desktop

Mỗi nhóm có table:

```text
STT
Họ và tên
Chức vụ
Đơn vị công tác
Quốc tịch
```

Yêu cầu:

- Header nền `#004c91`.
- Chữ trắng.
- STT tự tính `index + 1`.
- Border nhẹ.
- Row hover nhẹ.
- Không lưu STT trong database.
- Có count rõ ràng.
- Không cắt text quan trọng.
- Không gây horizontal scroll toàn trang.
- Table có wrapper riêng nếu cần scroll.

---

## 17.2 Mobile

Chuyển từng người thành card:

```text
STT 1
Họ và tên
Chức vụ
Đơn vị công tác
Quốc tịch
```

Không được bỏ trường để làm card ngắn hơn.

---

## 17.3 Nhóm dữ liệu

Áp dụng nhất quán cho:

- Khách.
- Nhân sự hỗ trợ.
- Participant.
- Invitee.
- Nhân sự phối hợp nếu cùng shape.

Nên tạo component dùng chung:

```text
PersonListTable
```

Props gợi ý:

```text
title
rows
emptyMessage
showActions
renderActions
```

Component chỉ lo UI, không chứa authorization nghiệp vụ.

---

# 18. Panel quyết định, Host và phiên bản

Thiết kế panel nền `slate-50` hoặc `blue-50` rất nhẹ.

Hiển thị:

```text
Quyết định
Người quyết định
Thời điểm
Lý do/Ghi chú
Host hiện tại
Phiên bản nội dung
Phiên bản phê duyệt
```

Yêu cầu:

- Không hiển thị decision source kỹ thuật.
- Nếu cần audit detail, map sang câu tiếng Việt.
- Không hiển thị “null”.
- Không lẫn host của campus khác.

---

# 19. Timeline lịch sử

Thiết kế timeline:

- Trục màu xanh.
- Mốc sự kiện rõ.
- Sự kiện quan trọng nhấn cam.
- Thời gian rõ.
- Actor rõ.
- Nội dung thân thiện.
- Không hiển thị JSON.
- Không lộ dữ liệu campus ngoài scope.
- Loading, empty, error rõ ràng.
- Có retry nếu API thất bại.

---

# 20. Shared UI components đề xuất

Chỉ tạo khi thực sự giúp đồng bộ:

```text
VisitSectionCard
VisitSectionHeader
VisitStatusBadge
ReadOnlyInfoGrid
CampusDetailHeader
PersonListTable
VisitDecisionPanel
VisitHistoryTimeline
```

Nguyên tắc:

- Không nhét business logic vào component presentation.
- Không tự fetch API trong component nhỏ nếu container đã có dữ liệu.
- Không duplicate component gần giống nhau.
- Không refactor sâu ngoài phạm vi.

---

# PHẦN C — KẾ HOẠCH THỰC HIỆN THEO SLICE

# 21. Slice 0 — Preflight và audit

Thực hiện:

```text
git status
git branch --show-current
git log -n 10
git diff
xác định HEAD local/remote
xác định branch mục tiêu
```

Sau đó:

- Rà soát các file liên quan.
- Chạy baseline build/test.
- Ghi test count hiện tại.
- Không sửa code trước khi có baseline.
- Không reset/rebase/rewrite history.
- Không mutation database thật.

Deliverable:

```text
AUDIT_AUTHENTICATED_V2_CREATE_AND_DETAIL_UI.md
```

---

# 22. Slice 1 — Backend identity integrity

Commit gợi ý:

```text
feat(visit): enforce registrant identity for authenticated v2 create
```

Nhiệm vụ:

```text
[ ] Direct create chỉ chấp nhận email actor.
[ ] Email khác trả error code ổn định.
[ ] Không tạo dữ liệu khi vi phạm.
[ ] Thêm test direct-create email mismatch.
[ ] Thêm test case-insensitive match.
[ ] Thêm test whitespace normalization.
[ ] Thêm test idempotency.
```

---

# 23. Slice 2 — Authenticated delegated OTP

Commit gợi ý:

```text
feat(visit): add otp verification for delegated authenticated submissions
```

Nhiệm vụ:

```text
[ ] Initiate OTP với full V2 snapshot.
[ ] Verify OTP rồi mới create.
[ ] Resend invalidate OTP cũ.
[ ] Expiry.
[ ] Rate limit.
[ ] Hash-only storage.
[ ] Submission mismatch protection.
[ ] Replay protection.
[ ] Không processing intent.
[ ] Route từng campus đến Staff Leader.
```

---

# 24. Slice 3 — Frontend “Tôi là người đăng ký”

Commit gợi ý:

```text
feat(visit-ui): add current-user registrant autofill and otp routing
```

Nhiệm vụ:

```text
[ ] Nút chủ động.
[ ] Fetch profile.
[ ] Fill field đúng.
[ ] Không overwrite draft.
[ ] Email match state.
[ ] Same email direct-create.
[ ] Different email OTP flow.
[ ] Clear stale processing state.
[ ] Error/loading state.
[ ] Accessibility.
```

---

# 25. Slice 4 — Processing authorization UI

Commit gợi ý:

```text
fix(visit-ui): gate campus processing by verified registrant identity
```

Nhiệm vụ:

```text
[ ] Chỉ Staff/Leader chính chủ thấy processing.
[ ] Staff chỉ self-host/send-review.
[ ] Leader self-host/assign/send-review.
[ ] Other campus read-only.
[ ] Different registrant email ẩn controls.
[ ] Backend reject forged payload.
[ ] Primary Contact copy rule đúng internal/visitor.
```

---

# 26. Slice 5 — Pure V2 process audit và cleanup

Commit gợi ý:

```text
fix(visit): complete pure-v2 process detail and remove mock state
```

Nhiệm vụ:

```text
[ ] Chứng minh process detail đọc instance-level V2.
[ ] Bổ sung test mixed request.
[ ] Xóa mock/dead state.
[ ] Không fake API.
[ ] Missing dependency có error/retry.
[ ] Không che lỗi bằng []/null mặc định.
```

---

# 27. Slice 6 — Đồng bộ UI Xem đơn

Commit gợi ý:

```text
refactor(visit-ui): align v2 detail view with visit process design
```

Nhiệm vụ:

```text
[ ] Card tổng quan.
[ ] 4 section xanh-cam.
[ ] ReadOnlyInfoGrid.
[ ] Campus card mới.
[ ] Badge trạng thái chuẩn.
[ ] Person table có STT.
[ ] Mobile cards.
[ ] Decision panel.
[ ] History timeline.
[ ] Giữ allowedActions.
[ ] Không đổi API/routing.
```

---

# 28. Slice 7 — Dọn trạng thái và component dùng chung

Commit gợi ý:

```text
refactor(visit-ui): standardize visit status and person presentation
```

Nhiệm vụ:

```text
[ ] VisitStatusBadge dùng chung.
[ ] PersonListTable dùng chung.
[ ] Xóa mapping status trùng lặp.
[ ] Translation VI/EN.
[ ] Không raw enum.
[ ] Không duplicate table.
```

---

# 29. Slice 8 — Documentation và closure

Commit gợi ý:

```text
docs(visit): update authenticated v2 create and detail ui handoff
```

Cập nhật:

- Progress.
- Implementation report.
- Test report.
- Known limitations.
- Resume point nếu chưa xong.
- File nào thay đổi.
- API contract mới.
- Error code mới.
- Screenshot/manual test evidence.

---

# PHẦN D — KIỂM THỬ BẮT BUỘC

# 30. Backend tests

Tối thiểu:

```text
1. Same email exact match → create trực tiếp.
2. Same email khác case → create trực tiếp.
3. Same email có whitespace → create trực tiếp.
4. Different email → direct-create bị chặn.
5. Different email → OTP initiate thành công.
6. OTP sai → không tạo request.
7. OTP hết hạn → không tạo request.
8. OTP đúng → tạo đúng 1 request.
9. Verify replay → không duplicate.
10. Resend → OTP cũ invalid.
11. Different email + processing forged → reject/ignore theo rule đã chốt.
12. Staff self-host own campus.
13. Staff self-host other campus → reject.
14. Staff assign other → reject.
15. Leader assign valid IC Staff.
16. Leader assign invalid candidate → reject.
17. Mixed request process detail → đúng target instance.
18. Sibling campus không xuất hiện.
19. Missing instance detail → error ổn định.
```

---

# 31. Frontend tests

Tối thiểu:

```text
1. Nút “Tôi là người đăng ký” fill đúng.
2. Không auto overwrite.
3. Không overwrite draft chưa confirm.
4. Same email hiện trạng thái không OTP.
5. Sửa email khác → trạng thái biến mất.
6. Different email → OTP modal.
7. Processing controls ẩn khi email khác.
8. Staff thấy đúng 2 lựa chọn.
9. Leader thấy đúng 3 lựa chọn.
10. Campus khác read-only.
11. Primary Contact copy bị disable với internal actor.
12. Person table có STT.
13. Remove row renumber STT.
14. Mobile giữ đủ field.
15. Raw status không render.
16. allowedActions vẫn kiểm soát action.
17. Detail screen không crash khi danh sách rỗng.
18. Error/retry state hiển thị đúng.
```

---

# 32. Real-stack E2E

Chạy trên disposable database.

Journey A — Staff chính chủ:

```text
Login Staff
→ Tạo đoàn
→ Bấm Tôi là người đăng ký
→ Email trùng
→ Chọn self-host tại own campus
→ Submit không OTP
→ DB Pure V2 đúng
→ Trang xem đơn đúng
→ Trang xử lý đọc đúng instance
```

Journey B — Staff tạo hộ:

```text
Login Staff
→ Nhập registrant email khác
→ Processing controls biến mất
→ Submit
→ OTP gửi đúng email
→ Verify OTP
→ Request được tạo
→ Không auto-host
→ Chờ Staff Leader xử lý
```

Journey C — Staff Leader:

```text
Login Staff Leader
→ Same-email
→ Assign valid IC Staff
→ Submit
→ Host đúng
→ Notification đúng
→ Detail UI đúng
```

Journey D — Multi-campus:

```text
Own campus direct-process
+
Other campus send-for-review
→ mỗi campus có status/host/data độc lập
→ process detail không lẫn dữ liệu
```

Journey E — Security:

```text
Gửi payload forged processing khi registrant email khác
→ backend reject
→ không tạo partial data
```

---

# 33. Build gate

Bắt buộc chạy:

```bash
dotnet build
dotnet test ArchitectureTests
dotnet test UnitTests
dotnet test IntegrationTests
npm run lint
npm run test
npm run build
git diff --check
```

Nếu project dùng command khác, ghi command thật trong report.

Không được báo “đã hoàn thành” nếu:

- Chưa chạy build.
- Test fail.
- Chưa chạy real-stack journey chính.
- Chưa xác minh database.
- Chưa kiểm tra UI responsive.
- Còn mock data trong luồng chính.
- Còn raw status.
- Còn lỗ hổng direct-create email mismatch.

---

# PHẦN E — DEFINITION OF DONE

# 34. Definition of Done tổng thể

```text
[ ] Có nút “Tôi là người đăng ký”.
[ ] Điền đúng hồ sơ hiện tại.
[ ] Không ghi đè bản nháp ngoài ý muốn.
[ ] Email trùng actor không cần OTP.
[ ] Email khác actor bắt buộc OTP.
[ ] Backend chặn direct-create email mismatch.
[ ] OTP verify mới tạo request.
[ ] Không duplicate khi retry/replay.
[ ] Different-email submission không có direct processing.
[ ] Staff/Leader chỉ có quyền khi chính chủ.
[ ] Host candidate đúng canonical role/scope.
[ ] Primary Contact rule đúng.
[ ] Process detail đọc Pure V2 instance-level.
[ ] Không lẫn sibling campus.
[ ] Không còn mock/dead state trên luồng chính.
[ ] Màn Xem đơn đồng nhất với trang Xử lý đơn.
[ ] Section xanh-cam đúng design system.
[ ] Danh sách đoàn là bảng có STT.
[ ] Mobile giữ đủ dữ liệu.
[ ] Không raw enum/status.
[ ] Decision/Host/Revision panel rõ.
[ ] Timeline rõ và đúng scope.
[ ] Backend tests xanh.
[ ] Frontend tests xanh.
[ ] Integration tests xanh.
[ ] Real-stack E2E xanh.
[ ] Build xanh.
[ ] Docs cập nhật.
```

---

# 35. Định dạng báo cáo cuối cùng

Báo cáo phải có:

```text
1. Branch và commit.
2. Files changed.
3. Database/schema changes.
4. Backend changes.
5. Frontend changes.
6. Security/authorization changes.
7. UI changes.
8. Tests added.
9. Test result.
10. Manual/E2E evidence.
11. Known limitations.
12. Việc chưa hoàn thành.
13. Điểm resume chính xác.
```

Không dùng các câu chung chung như:

```text
Đã xử lý xong.
Đã fix toàn bộ.
Hoạt động bình thường.
```

Mọi kết luận phải có evidence.

---

# 36. Quy tắc an toàn khi triển khai

- Không sửa database thật ngoài yêu cầu.
- Không chạy destructive SQL trên `pems_db`.
- Dùng disposable DB cho integration/E2E.
- Không reset/rebase/force-push.
- Không xóa stash.
- Không thay đổi branch ngoài ý muốn.
- Không commit file credential production.
- Không log OTP/token raw.
- Không hạ authorization để test cho dễ.
- Không dùng frontend-only protection.
- Không bỏ test để build xanh giả.
- Không tuyên bố Phase hoàn thành nếu còn gate đỏ.

---

## Kết luận triển khai

Ưu tiên thực hiện theo thứ tự:

```text
P0 — Khóa lỗ hổng registrant identity
P0 — Bắt buộc OTP khi tạo hộ
P0 — Chặn processing khi không phải chính chủ
P1 — Nút “Tôi là người đăng ký”
P1 — Pure V2 process verification
P1 — Xóa mock/dead state
P1 — Đồng bộ UI Xem đơn
P1 — Bảng danh sách đoàn có STT
P1 — Chuẩn hóa status
P2 — Shared UI cleanup
P2 — Documentation closure
```

Chỉ chuyển sang cleanup cuối khi các luồng nghiệp vụ chính đã được kiểm chứng bằng test và real-stack E2E.
