# PROMPT — Hoàn thiện Dashboard Bảng Lịch cho Staff và Staff Leader

## 1. Vai trò AI Agent

Bạn là Senior Full-stack Engineer cho hệ thống PEMS, gồm:

- Senior .NET 8 Clean Architecture Developer
- Senior React TypeScript Engineer
- Senior UI/UX Dashboard Engineer
- Database-first MySQL Engineer
- Authorization / Role Scope Reviewer
- QA Engineer

Nhiệm vụ: đọc full source hiện tại, hoàn thiện trang dashboard bảng lịch cho role `STAFF + STAFF` và `STAFF + LEADER`.

Trước khi sửa, bắt buộc search và đọc source hiện tại. Không sửa theo suy đoán.

---

## 2. Bối cảnh màn hình

Hiện tại trang dashboard của Staff và Staff Leader có bảng lịch tháng nhưng chưa hoàn thiện, chưa lấy data thật từ database/API.

Hai role này dùng dashboard khá giống nhau, nhưng khác quyền thao tác:

```text
Staff Leader:
- Xem lịch văn phòng của campus mình.
- Xem toàn bộ yêu cầu đến thăm thuộc campus mình.
- Có thể xử lý yêu cầu, chấp nhận/từ chối nếu đúng scope.
- Có thể gán host.
- Khi gán host phải gửi email mời host.
- Có chọn mẫu email giống flow mời thành phần tham gia đã có trong hệ thống.

Staff thường:
- Xem lịch văn phòng của campus mình.
- Xem lịch của tôi: chỉ hiển thị các yêu cầu tham quan mà mình là host.
- Có thể chấp nhận hoặc từ chối làm host nếu được mời/gán theo flow hiện tại.
- Xem chi tiết yêu cầu tham quan.
```

Không dùng chữ “thư mời” hoặc “đơn yêu cầu” trên bảng lịch. Trên dashboard này chỉ gọi thống nhất là:

```text
Yêu cầu đến thăm
Yêu cầu tham quan
Lịch văn phòng
Lịch của tôi
```

---

## 3. Tài liệu/source phải đọc trước khi sửa

Đọc và đối chiếu tối thiểu:

```text
1. PROJECT_OVERVIEW_v8_4_refined_v6_v10_FULL_UPDATED.md
2. PEMS_CANONICAL_BUSINESS_RULES_v8_4_refined_v6_v10_FULL_UPDATED.md
3. VISITOR_MANAGEMENT_SYSTEM_v8_4_refined_v6_v10_FULL_UPDATED.md
4. PEMS_UC_IMPLEMENTATION_RULEBOOK_FRONTEND_BACKEND_DATABASE_VALIDATION_SECURITY_v8_4_refined_v6_v10_FULL_UPDATED.md
5. PROMPT_STANDARDIZE_ROLE_SUBROLE_DEPARTMENT_v8_4_refined_v6_v10_FULL_UPDATED.md
6. PROJECT_STRUCTURE_FULL.md
7. PEMS_UI_DESIGN_SYSTEM_PROMPT.md
8. PEMS_v10_NEW_FINAL_SQL_TABLE_FIELD_DICTIONARY_MATCHED.docx
9. SQL fresh-create mới nhất trong docs/database/scripts/
10. Source backend/frontend hiện tại
```

Đọc source thật của các module liên quan:

```text
Frontend:
- Dashboard Staff / Staff Leader hiện tại.
- Component bảng lịch hiện tại.
- API service đang dùng cho dashboard/calendar/visit request.
- Types/interfaces liên quan visit request, visit request campus, host assignment.
- Modal xem chi tiết yêu cầu tham quan hiện có.
- Modal chọn host / mời participant / chọn email template hiện có.
- Email template selection UI đã làm trước đó.

Backend:
- DashboardController nếu có.
- VisitRequestsController.
- VisitInvitationsController.
- EmailsController.
- EmailTemplatesController.
- Query/Handler lấy danh sách visit request / visit request campus.
- Handler gán host.
- Handler Staff accept/reject host hoặc invitation nếu đã có.
- Entity/DbContext/EF config liên quan:
  - visit_requests
  - visit_request_campuses
  - visit_participants
  - users
  - departments
  - campuses
  - email_templates
  - sent_emails
  - sent_email_recipients
  - email_action_tokens nếu flow email button đang dùng
```

Nếu API phù hợp chưa tồn tại, hãy tạo API mới đúng Clean Architecture. Không tận dụng mock data. Không dùng dữ liệu hard-code.

---

## 4. Rule ưu tiên khi tài liệu/source mâu thuẫn

Khi tài liệu, code, SQL hoặc comment cũ mâu thuẫn nhau, ưu tiên:

```text
1. SQL fresh-create mới nhất
2. SQL Table & Field Dictionary mới nhất
3. PEMS_CANONICAL_BUSINESS_RULES
4. PEMS_UC_IMPLEMENTATION_RULEBOOK
5. PROMPT_STANDARDIZE_ROLE_SUBROLE_DEPARTMENT
6. VISITOR_MANAGEMENT_SYSTEM
7. PROJECT_OVERVIEW
8. Source code hiện tại
9. Tài liệu legacy chỉ dùng để đối chiếu, không dùng làm chuẩn code nếu mâu thuẫn
```

Không được dùng:

```text
- dynamic permissions
- permissions / role_permissions
- permission_code / permission_level runtime
- role legacy như DEPT, STAFF_L, STAFF_P, STAFF_LEADER as role_code
- field/table/enum không tồn tại trong SQL
- mock data cho dashboard production
```

Role chuẩn:

```text
Staff Leader = role_code STAFF + sub_role LEADER
Staff thường = role_code STAFF + sub_role STAFF
```

---

## 5. Mục tiêu cần hoàn thành

Hoàn thiện dashboard bảng lịch cho Staff và Staff Leader với data thật.

Dashboard cần có 2 chế độ chính:

```text
1. Lịch văn phòng
- Hiển thị toàn bộ yêu cầu đến thăm thuộc campus của user.
- Staff và Staff Leader đều nhìn được.
- Dùng để nắm tổng quan lịch tiếp khách của văn phòng/campus.

2. Lịch của tôi
- Chỉ hiển thị yêu cầu tham quan mà user hiện tại là host.
- Điều kiện chính: visit_request_campuses.current_host_user_id = currentUser.user_id
  hoặc logic host hiện tại trong source nếu đã có cách xác định khác.
```

Bảng lịch phải lấy theo tháng/tuần/ngày hiện tại từ API thật, có filter theo tháng, chế độ hiển thị và loại lịch.

---

## 6. Yêu cầu UI bảng lịch

Thiết kế lại bảng lịch theo hướng chuyên nghiệp, gọn, dễ đọc.

Giữ style PEMS enterprise dashboard:

```text
Primary blue: #004c91
Primary orange: #F37021
Text chính: slate-800 / slate-900
Text phụ: slate-500 / slate-600
Border nhẹ: slate-200 / slate-300
Background: slate-50 / white
```

Bảng lịch không được rối mắt:

```text
- Giảm ô/khung lớn không cần thiết.
- Không dùng card lồng card quá nhiều.
- Không để mỗi ngày thành khung quá nặng.
- Ngày hiện tại highlight nhẹ.
- Ngày quá khứ giảm độ nổi bật.
- Không tạo lịch cá nhân ở quá khứ.
- Không dùng chữ “thư mời”.
- Không dùng chữ “đơn yêu cầu” trong legend/dashboard calendar.
```

Legend màu trên bảng phải đổi thành nhóm liên quan yêu cầu đến thăm:

```text
- Mới / Chờ xử lý
- Cần xử lý
- Đã xử lý
- Bị hủy / Đã hết hạn
- Tôi là host
```

Nếu màu hiện tại đã có trong UI thì giữ palette nhẹ, chỉ đổi label và mapping cho đúng nghiệp vụ.

---

## 7. Data mapping bắt buộc

Mỗi event trên lịch phải đại diện cho một yêu cầu đến thăm / campus visit instance thật.

Event tối thiểu cần có:

```text
- visitRequestId
- visitRequestCampusId nếu có
- delegationName hoặc tên đoàn
- registrantFullName / người đăng ký
- campus
- plannedStartAt
- plannedEndAt
- status request
- status campus instance
- currentHostUserId
- currentHostName
- visitor/contact info cần thiết
- scope SINGLE_CAMPUS / MULTI_CAMPUS nếu có
- allowedActions hoặc action flags từ backend
```

Không để frontend tự đoán quyền bằng string status quá nhiều. Backend nên trả action flags rõ ràng:

```text
canViewDetail
canApprove
canReject
canAssignHost
canAcceptHost
canDeclineHost
canSendHostInvitationEmail
isCurrentHost
isPast
isCancelled
isExpired
```

Frontend chỉ render button theo flags backend trả về.

---

## 8. Logic quyền và scope

### Staff Leader

Staff Leader chỉ xử lý dữ liệu thuộc campus của mình.

Có thể:

```text
- Xem yêu cầu đến thăm thuộc campus mình.
- Xem chi tiết yêu cầu.
- Chấp nhận / từ chối xử lý nếu yêu cầu đúng trạng thái và đúng scope.
- Gán host khi nghiệp vụ cho phép.
- Gửi email mời host khi gán host.
- Xem ai đang là host.
- Xem ai đã từ chối nếu có lịch sử/attempts/participant response trong DB.
```

Không được:

```text
- Xử lý yêu cầu ngoài campus.
- Gán host ngoài campus.
- Sửa host nếu rule hiện tại là host assignment final / one-time.
- Thao tác trên visit đã CANCELLED/CLOSED hoặc trạng thái terminal không cho phép.
```

### Staff thường

Staff thường có thể:

```text
- Xem lịch văn phòng của campus mình.
- Xem lịch của tôi nếu mình là host.
- Xem chi tiết yêu cầu tham quan.
- Chấp nhận làm host nếu được mời/gán và trạng thái còn cho phép.
- Từ chối làm host nếu được mời/gán và trạng thái còn cho phép.
```

Không được:

```text
- Gán host.
- Approve/reject với quyền Staff Leader.
- Xử lý yêu cầu ngoài scope campus/host.
```

---

## 9. Modal xem chi tiết yêu cầu tham quan

Khi click vào event trên lịch, mở modal/detail drawer chuyên nghiệp, gọn, tránh ô/khung lớn.

Modal cần hiển thị theo layout compact:

```text
Header:
- Tên đoàn / tên yêu cầu
- Badge trạng thái
- Thời gian tham quan
- Campus
- Quick actions theo allowedActions

Nội dung chính:
- Thông tin người đăng ký / tổ chức
- Thông tin liên hệ
- Thời gian, địa điểm, campus
- Mục đích chuyến thăm
- Số lượng khách nếu có
- Ngôn ngữ, media consent, transportation nếu có trong request
- Host hiện tại
- Danh sách người đã từ chối / phản hồi nếu có dữ liệu
- Lý do từ chối nếu có
```

Button trong modal:

```text
Staff Leader:
- Chấp nhận
- Từ chối
- Gán host

Staff:
- Chấp nhận làm host
- Từ chối làm host
```

Chỉ hiển thị button nếu backend trả flag tương ứng. Không tự hiện button bằng role frontend nếu backend không cho.

---

## 10. Flow gán host và gửi email

Khi Staff Leader bấm “Gán host”:

```text
1. Mở modal chọn host.
2. Danh sách host lấy từ API thật, đúng campus, role STAFF + sub_role STAFF.
3. Hiển thị cảnh báo conflict lịch nếu backend hiện đã có logic conflict.
4. Sau khi chọn host, hiển thị bước chọn mẫu email.
5. Cho phép chọn email template giống flow mời thành phần tham gia đã có.
6. Preview nội dung email trước khi gửi nếu component hiện có hỗ trợ.
7. Submit gọi API gán host + gửi email.
8. Thành công thì update lại event trên calendar, modal detail và toast success.
9. Lỗi thì giữ modal, hiển thị message rõ ràng.
```

Email phải dùng module email hiện có:

```text
- email_templates
- sent_emails
- sent_email_recipients
- email_action_tokens nếu flow action button đang áp dụng
```

Không tự tạo inbox email thật. Không tự thêm bảng mới.

---

## 11. Backend API đề xuất nếu chưa có

Nếu source hiện tại chưa có API phù hợp, tạo API mới theo Clean Architecture.

Ví dụ endpoint, có thể điều chỉnh theo convention hiện tại:

```text
GET /api/dashboard/staff/calendar
```

Query params:

```text
viewMode=office|mine
from=YYYY-MM-DD
to=YYYY-MM-DD
calendarView=month|week|day
```

Response nên gồm:

```ts
{
  items: [
    {
      visitRequestId: number;
      visitRequestCampusId: number | null;
      title: string;
      delegationName: string | null;
      registrantFullName: string | null;
      campusName: string;
      plannedStartAt: string;
      plannedEndAt: string;
      requestStatus: string;
      campusStatus: string | null;
      currentHostUserId: number | null;
      currentHostName: string | null;
      isCurrentHost: boolean;
      displayStatus: string;
      colorType: "NEW" | "NEEDS_ACTION" | "PROCESSED" | "CANCELLED_OR_EXPIRED" | "MINE";
      allowedActions: {
        canViewDetail: boolean;
        canApprove: boolean;
        canReject: boolean;
        canAssignHost: boolean;
        canAcceptHost: boolean;
        canDeclineHost: boolean;
      };
    }
  ]
}
```

Nếu detail API chưa đủ dữ liệu, tạo/extend:

```text
GET /api/dashboard/staff/calendar/{visitRequestCampusId}/detail
```

Backend phải filter theo role/scope, không để frontend tự lọc toàn bộ dữ liệu nhạy cảm.

---

## 12. Validation / business rules

Backend phải validate:

```text
- from/to hợp lệ.
- Khoảng ngày không quá lớn nếu cần giới hạn performance.
- User phải đăng nhập.
- User phải là STAFF + LEADER hoặc STAFF + STAFF.
- Staff/Staff Leader chỉ thấy dữ liệu đúng campus/scope.
- Lịch của tôi chỉ trả item mà user là host.
- Không cho tạo lịch cá nhân trong quá khứ.
- Không cho thao tác accept/reject/assign host nếu visit đã CANCELLED/CLOSED/terminal.
- Không cho Staff thường gán host.
- Không cho Staff Leader gán host ngoài campus hoặc user không phải STAFF + STAFF.
- Không cho gán host lần hai nếu business rule hiện tại là assignment final.
```

---

## 13. Frontend requirements

Frontend cần:

```text
- Bỏ mock data dashboard calendar.
- Gọi API thật.
- Có loading state.
- Có empty state:
  “Không có yêu cầu đến thăm trong khoảng thời gian này.”
- Có error state gọn:
  “Không thể tải lịch yêu cầu đến thăm. Vui lòng thử lại.”
- Khi đổi tháng/viewMode/filter phải refetch data.
- Không reload toàn trang sau action, chỉ refresh calendar data.
- Toast success/error dùng helper toast chung nếu project đã có.
```

Không được:

```text
- Hard-code data.
- Hard-code quyền theo email.
- Hard-code campus.
- Dùng chữ “thư mời” trên calendar.
- Dùng chữ “đơn yêu cầu” trong legend calendar.
- Tạo component quá lớn nếu có thể tách nhỏ hợp lý.
- Làm vỡ layout dashboard hiện tại.
```

---

## 14. Test requirements

Tối thiểu cần tự kiểm tra:

### Backend

```text
- Staff Leader lấy lịch văn phòng chỉ thấy campus mình.
- Staff lấy lịch văn phòng chỉ thấy campus mình.
- Staff lấy lịch của tôi chỉ thấy item mình là host.
- Staff Leader thấy action assignHost khi đúng trạng thái.
- Staff thường không thấy assignHost.
- Staff thấy accept/decline host khi đúng trạng thái.
- CANCELLED/CLOSED không có action mutate.
- User ngoài role STAFF bị 403.
```

### Frontend

```text
- Dashboard load data thật.
- Đổi tháng refetch đúng.
- Đổi Lịch văn phòng / Lịch của tôi refetch đúng.
- Click event mở detail modal.
- Button trong modal render theo allowedActions.
- Gán host mở modal chọn host + chọn template email.
- Gán host thành công refresh calendar.
- Accept/reject host thành công refresh calendar.
- Empty/error/loading state hiển thị đúng.
```

Build bắt buộc:

```text
- Backend build không lỗi.
- Frontend build không lỗi.
```

Nếu không chạy được build/test, phải báo rõ lý do và command đã thử.

---

## 15. Output/report sau khi làm

Sau khi sửa, báo cáo theo format:

```text
1. Đã đọc những file/source nào
2. File backend đã sửa/tạo
3. File frontend đã sửa/tạo
4. API đã dùng/tạo
5. Mapping dữ liệu calendar
6. Rule phân quyền đã enforce ở backend
7. UI đã thay đổi gì
8. Flow gán host + gửi email hoạt động như nào
9. Test/build đã chạy
10. Còn hạn chế hoặc việc cần làm tiếp nếu có
```

Không báo “hoàn thành” nếu còn dùng mock data hoặc chưa nối API thật.
