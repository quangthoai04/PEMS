# PROMPT CẬP NHẬT LOGIC HIỂN THỊ & NGHIỆP VỤ QUẢN LÝ ĐƠN TIẾP KHÁCH THEO ROLE

## 0. Mục tiêu của prompt

Hãy cập nhật code hiện tại của hệ thống PEMS để màn hình quản lý đơn tiếp khách hiển thị và xử lý nghiệp vụ khác nhau theo từng vai trò người dùng.

Yêu cầu bắt buộc: **trước khi sửa code phải đọc và đối chiếu với SQL hiện tại, tài liệu tổng quan dự án, tài liệu use case, permission matrix và cấu trúc project** để đảm bảo không tự ý tạo logic lệch với database hoặc kiến trúc hiện có.

Mục tiêu không chỉ là sửa giao diện, mà phải đồng bộ:

- Database/schema hiện tại
- Entity/domain model
- Permission/RBAC
- API backend
- Query filter theo role
- Frontend layout
- Action button theo quyền
- Trạng thái nghiệp vụ
- Cơ chế hủy/từ chối/gán host
- Luồng lời mời tham dự
- Test case cho từng vai trò

---

## 1. Các file/tài liệu bắt buộc phải đọc trước khi code

Trước khi sửa bất kỳ file nào, hãy đọc và đối chiếu các tài liệu/code sau nếu tồn tại trong project:

### 1.1. Database / SQL

Cần đọc file SQL mới nhất, ví dụ:

```text
database/scripts/pems_full.sql
database/scripts/pems_v8_*.sql
database/seed/roles.sql
database/seed/permissions.sql
database/seed/permission_matrix.sql
```

Cần kiểm tra kỹ các bảng/cột liên quan:

```text
users
roles
permissions
role_permissions
campuses
departments

visit_requests
visit_request_campuses
visit_guest_members
visit_participants
visit_agendas
visit_logistics_items

minutes
minute_action_items

files
documents

login_logs
security_events
user_sessions
```

Đặc biệt cần kiểm tra các cột sau có tồn tại không, tên chính xác là gì, kiểu dữ liệu là gì:

```text
visit_requests.status
visit_requests.scope
visit_requests.host_campus_id
visit_requests.created_by
visit_requests.decided_by
visit_requests.decided_at
visit_requests.rejection_reason
visit_requests.cancelled_by
visit_requests.cancelled_at
visit_requests.cancellation_reason
visit_requests.cancellation_source
visit_requests.start_time
visit_requests.end_time
visit_requests.row_version

visit_request_campuses.visit_request_id
visit_request_campuses.campus_id
visit_request_campuses.status
visit_request_campuses.host_user_id
visit_request_campuses.assigned_by
visit_request_campuses.assigned_at
visit_request_campuses.campus_decided_by
visit_request_campuses.campus_decided_at
visit_request_campuses.campus_decision_note
visit_request_campuses.row_version

visit_participants.visit_request_id
visit_participants.user_id
visit_participants.status / participation_status
visit_participants.role / participant_role
visit_participants.invited_by
visit_participants.responded_at
```

Nếu tên cột thực tế khác với các tên trên, phải dùng đúng tên trong SQL hiện tại. Không tự invent tên cột mới nếu không có yêu cầu migrate/schema patch.

---

### 1.2. Tài liệu tổng quan dự án

Cần đọc các file tài liệu nếu có:

```text
PROJECT_STRUCTURE_FULL.md
CLEAN_ARCHITECTURE.md
DATABASE_SCHEMA.md
USE_CASE_LIST.md
USE_CASE_NOTES.md
PERMISSION_MATRIX.md
```

Mục đích đọc:

- Xác định đúng module/domain hiện tại của Visit Request
- Xác định UC nào đang quản lý danh sách đơn, chi tiết đơn, xử lý đơn, mời tham gia
- Xác định permission code chuẩn đang dùng
- Xác định role thực tế trong hệ thống
- Tránh tạo role/permission/action mới lệch với tài liệu

---

### 1.3. Code backend cần kiểm tra

Tìm các khu vực liên quan:

```text
Application/Features/VisitRequests
Application/Features/Delegations
Application/Features/VisitParticipants
Application/Common/Security
Application/Common/Interfaces
Domain/Entities
Infrastructure/Persistence
Infrastructure/Configurations
WebAPI/Controllers
```

Cần kiểm tra:

```text
VisitRequest entity
VisitRequestCampus entity
VisitParticipant entity
User entity
Role/Permission model
DbContext
Entity configurations
Commands/Queries hiện tại
Controllers hiện tại
Authorization attributes/policies
PermissionCodes/PermissionConstants
```

---

### 1.4. Code frontend cần kiểm tra

Tìm các file liên quan:

```text
VisitRequestManagement.tsx
VisitRequestDetail.tsx
VisitRequestList.tsx
VisitRequestCard.tsx
VisitRequestTable.tsx
VisitRequestFilters.tsx
VisitRequestService.ts
visitRequestApi.ts
auth context / permission utils
role-based route guard
components/dialogs/modals
```

Cần kiểm tra:

- Màn danh sách đơn hiện tại đang fetch API nào
- Frontend đang tự filter role hay backend filter
- Các nút Duyệt/Từ chối/Hủy/Gán host đang render theo điều kiện nào
- Có component modal nào dùng lại được không
- UI hiện tại có responsive/table/card như thế nào
- Có enum status/scope hard-code ở frontend không

---

## 2. Nguyên tắc nghiệp vụ bắt buộc

### 2.1. Mỗi role nhìn thấy dữ liệu khác nhau

Không được thiết kế màn hình kiểu một danh sách dùng chung cho tất cả role rồi chỉ ẩn/hiện nút ở frontend.

Backend phải trả dữ liệu đã được filter theo quyền người dùng.

Frontend chỉ render lại UI và action dựa trên:

```text
role
permissions
status
scope
campus
host_user_id
created_by
start_time
allowedActions từ backend nếu có
```

Khuyến nghị backend trả thêm field:

```json
{
  "allowedActions": [
    "VIEW_DETAIL",
    "HO_APPROVE",
    "HO_REJECT",
    "APPROVE_AND_ASSIGN_HOST",
    "CAMPUS_REJECT",
    "CANCEL_BY_HOST",
    "CANCEL_BY_VISITOR"
  ]
}
```

Nếu chưa có `allowedActions`, frontend phải dùng permission + status + scope để render, nhưng backend vẫn phải validate lại 100%.

---

### 2.2. Admin không tham gia luồng tiếp khách

Admin không thuộc luồng nghiệp vụ tiếp khách.

Admin không được:

- Duyệt đơn
- Từ chối đơn
- Hủy đơn
- Gán host
- Chuẩn bị tiếp khách
- Chấp nhận/từ chối lời mời trong vai trò nghiệp vụ
- Can thiệp trạng thái đơn trong màn nghiệp vụ thông thường

Admin chỉ dùng cho:

- Quản lý tài khoản
- Quản lý vai trò/quyền
- Quản lý cấu hình
- Kiểm tra log kỹ thuật nếu có

Nếu hiện tại màn Visit Request cho Admin thao tác nghiệp vụ thì phải gỡ.

---

### 2.3. Không có chức năng sửa đơn sau khi gửi

Sau khi Visitor gửi form, hệ thống không có chức năng sửa/cập nhật/bổ sung thông tin đơn.

Cần gỡ hoặc ẩn toàn bộ logic:

```text
Edit request
Update request
Update submitted request information
Bổ sung thông tin đơn
Sửa đơn sau khi gửi
```

Visitor chỉ có thể:

- Xem đơn của mình
- Theo dõi trạng thái
- Xem lý do từ chối nếu có
- Hủy đơn trước thời gian bắt đầu nếu đủ điều kiện

---

### 2.4. Phân biệt Từ chối và Hủy

Tuyệt đối không dùng chung `REJECTED` và `CANCELLED`.

#### Từ chối

Từ chối xảy ra khi đơn còn ở giai đoạn chờ duyệt.

Người có quyền từ chối:

```text
HO: chỉ từ chối đơn liên cơ sở đang chờ HO duyệt
Staff Leader: từ chối đơn thuộc campus mình
```

Từ chối bắt buộc nhập lý do.

#### Hủy

Hủy xảy ra sau khi đơn đã được duyệt/gán host hoặc vẫn còn trước thời gian bắt đầu, tùy vai trò.

Người có quyền hủy:

```text
Visitor: hủy đơn do chính mình gửi trước thời gian bắt đầu
Host: hủy đơn sau khi đã được duyệt và đã được gán làm host, trước thời gian bắt đầu
```

Hủy bắt buộc nhập lý do.

---

### 2.5. Chấp nhận/từ chối tham gia là trang riêng

Chức năng **chấp nhận tham gia** hoặc **từ chối tham gia** không nằm trong trang này.

Nó phải nằm ở trang riêng, ví dụ:

```text
Lời mời tham gia
Đơn mời tham gia
Participation Invitations
```

Trang hiện tại chỉ hiển thị các đơn đã liên quan đến người dùng sau khi họ đã chấp nhận tham gia.

Nếu user được mời nhưng chưa phản hồi, đơn không hiển thị trong tab `Đơn mời tham dự`.

Nếu user từ chối tham gia, đơn không hiển thị trong tab `Đơn mời tham dự`.

---

## 3. Cấu trúc màn hình cần cập nhật

Trang quản lý đơn tiếp khách hiện tại cần chia thành 2 tab:

```text
Tab 1: Đơn phụ trách
Tab 2: Đơn mời tham dự
```

Tên khuyến nghị:

```text
Đơn phụ trách | Đơn mời tham dự
```

### 3.1. Tab 1 — Đơn phụ trách

Tab này hiển thị các đơn mà người dùng có trách nhiệm xử lý trực tiếp, được giao, hoặc là đơn do chính họ tạo tùy role.

Đây là tab xử lý nghiệp vụ chính.

### 3.2. Tab 2 — Đơn mời tham dự

Tab này hiển thị các đơn mà người dùng đã **chấp nhận tham gia** từ trang lời mời riêng.

Tab này không có:

- Nút chấp nhận tham gia
- Nút từ chối tham gia
- Nút duyệt đơn
- Nút từ chối đơn
- Nút gán host
- Các action nghiệp vụ của HO/Staff Leader

### 3.3. Visitor không có Tab 2

Visitor chỉ thấy đơn do mình gửi.

Có thể hiển thị title là:

```text
Đơn của tôi
```

Không hiển thị tab `Đơn mời tham dự`.

### 3.4. Admin không có màn nghiệp vụ này

Admin không tham gia luồng tiếp khách.

Nếu route hiện tại cho Admin vào màn này, có thể:

- Redirect sang Dashboard/Admin
- Hoặc hiển thị empty state: `Admin không tham gia luồng xử lý đơn tiếp khách`
- Không hiển thị action nghiệp vụ

---

## 4. Role thực tế trong hệ thống và cách xử lý Staff Leader

Theo database hiện tại, role có thể đang là:

```text
ADMIN
HO
STAFF
DEPT
STUDENT
VISITOR
```

Nếu database chưa có role `STAFF_LEADER`, không tự ý thêm role mới khi chưa có yêu cầu sửa SQL.

Cần xác định Staff Leader bằng permission hoặc field hiện có.

Ví dụ:

```text
STAFF + permission APPROVE_OWN_CAMPUS_REQUEST_AS_STAFF_LEADER
STAFF + permission ASSIGN_HOST_STAFF
STAFF + chức danh/flag nếu DB đã có
```

Trong code, nên dùng khái niệm nghiệp vụ:

```text
isStaffLeader = user has permission ASSIGN_HOST_STAFF
```

Không hard-code rằng mọi `STAFF` đều là Staff Leader.

Nếu hiện tại đang gộp `STAFF = Staff Leader + Staff`, cần kiểm tra permission matrix để phân biệt người có quyền duyệt/gán host và người chỉ là host/staff thường.

---

## 5. Logic chi tiết theo role

## 5.1. HO

### 5.1.1. HO thấy gì ở Tab 1

HO có thể xem danh sách và chi tiết các đơn của các cơ sở, nhưng thao tác bị giới hạn.

HO thấy:

```text
Đơn liên cơ sở chờ HO duyệt
Đơn liên cơ sở HO đã duyệt/từ chối
Đơn một cơ sở nếu business yêu cầu HO được xem tổng quan
```

### 5.1.2. Với đơn một cơ sở

HO chỉ được xem.

HO không được:

```text
Duyệt
Từ chối
Gán host
Hủy
```

### 5.1.3. Với đơn liên cơ sở

HO được:

```text
Xem danh sách
Xem chi tiết
Duyệt
Từ chối
```

Điều kiện HO được duyệt/từ chối:

```text
currentUser.role == HO
AND request.scope == MULTI_CAMPUS
AND request.status == PENDING_HO_APPROVAL
```

Khi HO từ chối:

```text
Bắt buộc nhập lý do
request.status = REJECTED
request.rejection_reason = reason
request.decided_by = currentUser.id
request.decided_at = now
```

Khi HO duyệt:

```text
request.status = HO_APPROVED
request.decided_by = currentUser.id
request.decided_at = now
```

Sau khi HO duyệt, đơn mới hiện cho Staff Leader của các campus nằm trong form.

Nếu HO từ chối, Staff Leader không cần thấy đơn đó.

### 5.1.4. HO thấy gì ở Tab 2

HO có Tab 2 nếu HO được mời tham dự và đã chấp nhận lời mời ở trang riêng.

Tab 2 của HO chỉ là xem thông tin tham dự, không dùng để duyệt.

---

## 5.2. Staff Leader

Staff Leader là người có quyền xử lý đơn thuộc campus của mình.

Trong DB có thể không có role `STAFF_LEADER`, nên cần xác định bằng permission.

Ví dụ:

```text
APPROVE_OWN_CAMPUS_REQUEST_AS_STAFF_LEADER
REJECT_OWN_CAMPUS_REQUEST_AS_STAFF_LEADER
ASSIGN_HOST_STAFF
VIEW_OWN_CAMPUS_VISIT_REQUEST
```

### 5.2.1. Staff Leader thấy gì ở Tab 1

Staff Leader thấy:

```text
Đơn một cơ sở thuộc campus mình
Đơn liên cơ sở đã được HO duyệt và có campus mình
Đơn đã gán host thuộc campus mình để theo dõi
```

Không thấy:

```text
Đơn một cơ sở của campus khác
Đơn liên cơ sở chưa được HO duyệt
Đơn liên cơ sở không chứa campus của mình
Đơn liên cơ sở đã bị HO từ chối, trừ khi chỉ hiển thị lịch sử dạng read-only
```

### 5.2.2. Điều kiện thấy đơn một cơ sở

```text
request.scope == SINGLE_CAMPUS
AND request.host_campus_id == currentUser.primary_campus_id
```

Nếu SQL dùng bảng mapping campus:

```text
request_campuses contains currentUser.primary_campus_id
```

### 5.2.3. Điều kiện thấy đơn liên cơ sở

```text
request.scope == MULTI_CAMPUS
AND request.status == HO_APPROVED
AND visit_request_campuses contains currentUser.primary_campus_id
```

### 5.2.4. Staff Leader duyệt đơn

Khi Staff Leader bấm `Duyệt`, không được duyệt ngay.

Phải mở popup chọn Staff làm host.

Popup cần có:

```text
Danh sách Staff thuộc cùng campus
Tên Staff
Email
Chức vụ/đơn vị nếu có
Số đơn đang phụ trách nếu có
Cảnh báo trùng lịch nếu có
```

Sau khi chọn host và xác nhận, mới tính là duyệt thành công.

Nếu là đơn một cơ sở, có thể update trực tiếp vào `visit_requests`.

Nếu là đơn liên cơ sở, nên update phần campus tương ứng trong `visit_request_campuses`.

### 5.2.5. Cảnh báo trùng lịch khi chọn host

Khi chọn host, kiểm tra các đơn khác của Staff đó.

Điều kiện overlap:

```text
currentRequest.start_time < otherRequest.end_time
AND currentRequest.end_time > otherRequest.start_time
```

Chỉ kiểm tra các đơn chưa kết thúc/hủy/từ chối:

```text
otherRequest.status NOT IN (REJECTED, CANCELLED, COMPLETED)
```

Cảnh báo hiển thị:

```text
Nhân sự này đang có lịch tiếp khách khác trùng hoặc gần trùng với thời gian của đoàn hiện tại. Bạn vẫn có thể chọn, nhưng nên kiểm tra lại lịch trước khi xác nhận.
```

Cảnh báo không bắt buộc chặn thao tác, trừ khi business muốn chặn.

### 5.2.6. Staff Leader từ chối đơn

Khi Staff Leader từ chối:

```text
Bắt buộc nhập lý do
```

Với đơn một cơ sở:

```text
request.status = REJECTED
request.rejection_reason = reason
request.decided_by = currentUser.id
request.decided_at = now
```

Với đơn liên cơ sở đã HO duyệt:

Cần kiểm tra business hiện tại. Nếu campus vẫn được quyền từ chối phần campus mình, update tại bảng `visit_request_campuses`.

```text
visit_request_campuses.status = REJECTED
visit_request_campuses.campus_decision_note = reason
visit_request_campuses.campus_decided_by = currentUser.id
visit_request_campuses.campus_decided_at = now
```

Nếu business chốt rằng sau HO duyệt campus không được từ chối thì không render nút từ chối cho đơn liên cơ sở sau HO duyệt.

### 5.2.7. Badge cho đơn liên cơ sở HO đã duyệt

Đơn liên cơ sở Staff Leader nhận từ HO cần có nhãn rõ ràng:

```text
Liên cơ sở · HO đã duyệt
```

Hoặc:

```text
Đơn từ HO
Cần campus xử lý
```

Khuyến nghị dùng badge:

```text
Liên cơ sở · HO đã duyệt
```

---

## 5.3. Staff / Host

Staff thường không được xem toàn bộ đơn.

Staff chỉ thấy đơn khi được giao làm host hoặc được assign vào công việc cụ thể.

### 5.3.1. Staff thấy gì ở Tab 1

Staff thấy:

```text
Đơn được Staff Leader giao làm host
Đơn được giao chuẩn bị
Đơn có task/logistics/agenda/minutes liên quan đến Staff
```

Không thấy:

```text
Đơn chưa được giao
Đơn toàn campus
Đơn của Staff khác
Đơn đang chờ duyệt
```

### 5.3.2. Staff không có quyền

Staff không được:

```text
Duyệt đơn
Từ chối đơn
Gán host
Xem toàn bộ đơn campus nếu không được giao
```

### 5.3.3. Staff/Host có quyền hủy khi đủ điều kiện

Host chỉ được hủy khi:

```text
request.host_user_id == currentUser.id
AND request.status IN (APPROVED, ASSIGNED, PREPARING, READY)
AND now < request.start_time
AND request.status NOT IN (REJECTED, CANCELLED, COMPLETED)
```

Nếu dùng bảng `visit_request_campuses` cho đơn liên cơ sở:

```text
visit_request_campuses.host_user_id == currentUser.id
AND visit_request_campuses.status IN (ASSIGNED, PREPARING, READY)
AND now < request.start_time
```

Khi hủy:

```text
Bắt buộc nhập lý do
status = CANCELLED
cancellation_reason = reason
cancelled_by = currentUser.id
cancelled_at = now
```

Nếu DB có `cancellation_source`, cần map phù hợp:

```text
SELF_SERVICE = Visitor tự hủy
EXTERNAL_CONFIRMATION = phía ngoài xác nhận hủy
INTERNAL/HOST_CANCELLED = Host nội bộ hủy nếu enum có hỗ trợ
```

Nếu enum chưa có HOST/INTERNAL, không tự sửa SQL nếu chưa được yêu cầu. Có thể phân biệt bằng `cancelled_by` và role của user.

### 5.3.4. Nhãn trạng thái chuẩn bị cho Staff

Với Staff/Host, UI nên hiển thị nhãn theo mức độ chuẩn bị:

```text
Cần chuẩn bị
Tiếp tục chuẩn bị
Sẵn sàng tiếp đón
Đang tiếp đón
Cần hoàn tất sau tiếp đón
Đã hoàn tất
```

Logic gợi ý:

```text
Nếu đơn vừa được giao host và các phần chuẩn bị chính còn trống:
    Cần chuẩn bị

Nếu đã có một phần agenda/logistics/tài liệu/người tham gia nhưng chưa đủ:
    Tiếp tục chuẩn bị

Nếu các phần chuẩn bị chính đã có dữ liệu:
    Sẵn sàng tiếp đón

Nếu now nằm trong khoảng start_time - end_time:
    Đang tiếp đón

Nếu now > end_time và còn thiếu minutes/feedback/công việc sau tiếp đón:
    Cần hoàn tất sau tiếp đón

Nếu đã hoàn tất đầy đủ:
    Đã hoàn tất
```

Các dữ liệu có thể kiểm tra:

```text
visit_agendas
visit_logistics_items
documents/files
visit_participants
minutes
minute_action_items
feedbacks nếu có
```

---

## 5.4. Department Leader / Department

Nếu hệ thống hiện tại chỉ có role `DEPT`, không tự thêm `DEPT_LEADER` nếu SQL chưa có.

Nếu có permission phân biệt Department Leader, dùng permission.

### 5.4.1. Department thấy gì ở Tab 1

Department/Department Leader chỉ thấy Tab 1 nếu được giao nhiệm vụ cụ thể.

Ví dụ:

```text
Giao chuẩn bị nội dung chuyên môn
Giao tham gia phần làm việc chuyên ngành
Giao hỗ trợ agenda
Giao phối hợp đón đoàn
Giao task cụ thể
```

Nếu chỉ được mời tham gia, không hiển thị ở Tab 1.

### 5.4.2. Department thấy gì ở Tab 2

Department/Department Leader có Tab 2 nếu đã chấp nhận lời mời tham gia ở trang lời mời riêng.

Tab 2 chỉ để xem thông tin tham dự.

### 5.4.3. Department không có quyền

Department/Department Leader không được:

```text
Duyệt đơn
Từ chối đơn
Gán host
Hủy đơn nghiệp vụ chính
```

Trừ khi tài liệu use case/permission matrix hiện tại đã quy định khác. Nếu có khác, phải ghi chú rõ và không tự ý thay đổi.

---

## 5.5. Student

### 5.5.1. Student thấy gì ở Tab 1

Student chỉ thấy Tab 1 nếu được giao nhiệm vụ hỗ trợ cụ thể.

Ví dụ:

```text
Hỗ trợ check-in
Hỗ trợ hướng dẫn đoàn
Hỗ trợ hậu cần
Hỗ trợ sự kiện
Hỗ trợ truyền thông
Hỗ trợ phiên dịch
```

Nếu chỉ được mời tham dự, không hiển thị ở Tab 1.

### 5.5.2. Student thấy gì ở Tab 2

Student có Tab 2 nếu đã chấp nhận lời mời tham gia.

### 5.5.3. Student không có quyền

Student không được:

```text
Duyệt đơn
Từ chối đơn
Gán host
Hủy đơn
Xem đơn không liên quan
```

---

## 5.6. Visitor

Visitor là người gửi form.

### 5.6.1. Visitor thấy gì

Visitor chỉ thấy đơn do chính họ gửi.

Có thể đặt tab/title là:

```text
Đơn của tôi
```

Visitor không có tab `Đơn mời tham dự`.

### 5.6.2. Visitor được làm gì

Visitor được:

```text
Xem danh sách đơn của mình
Xem chi tiết đơn của mình
Theo dõi trạng thái
Xem lý do từ chối nếu có
Hủy đơn trước thời gian bắt đầu nếu đủ điều kiện
```

### 5.6.3. Visitor không được làm gì

Visitor không được:

```text
Sửa đơn
Cập nhật đơn
Bổ sung thông tin đơn
Duyệt đơn
Từ chối đơn
Gán host
Xem thông tin phân công nội bộ không cần thiết
Xem đơn người khác
```

### 5.6.4. Điều kiện Visitor được hủy

```text
request.created_by == currentUser.id
AND now < request.start_time
AND request.status NOT IN (REJECTED, CANCELLED, COMPLETED)
```

Khi hủy:

```text
Bắt buộc nhập lý do
request.status = CANCELLED
request.cancellation_reason = reason
request.cancelled_by = currentUser.id
request.cancelled_at = now
request.cancellation_source = SELF_SERVICE nếu DB hỗ trợ
```

---

## 5.7. Admin

Admin không tham gia luồng tiếp khách.

Admin không có nghiệp vụ trong trang này.

Không hiển thị các nút:

```text
Duyệt
Từ chối
Gán host
Hủy
Chuẩn bị
Chấp nhận tham gia
Từ chối tham gia
```

Nếu vẫn cần Admin xem log kỹ thuật, hãy dùng trang/admin module khác, không đưa vào màn xử lý đơn tiếp khách.

---

## 6. Tab 2 — Đơn mời tham dự

## 6.1. Bản chất Tab 2

Tab `Đơn mời tham dự` chỉ hiển thị các đơn mà user đã chấp nhận tham gia.

Không hiển thị lời mời đang chờ phản hồi.

Không hiển thị lời mời đã từ chối.

## 6.2. Role có Tab 2

Có Tab 2:

```text
HO
STAFF_LEADER / STAFF có permission leader
STAFF
DEPT_LEADER nếu có
DEPT
STUDENT
```

Không có Tab 2:

```text
VISITOR
ADMIN
```

## 6.3. Điều kiện dữ liệu Tab 2

```text
visit_participants.user_id == currentUser.id
AND visit_participants.status == ACCEPTED
AND visit_requests.status NOT IN (REJECTED, CANCELLED)
```

Nếu cần hiển thị lịch sử đã hoàn thành:

```text
COMPLETED vẫn được hiển thị
```

## 6.4. Thông tin hiển thị trong Tab 2

Nên hiển thị:

```text
Tên đoàn khách
Thời gian tiếp đón
Địa điểm
Campus
Vai trò tham gia của user
Nội dung buổi làm việc
Host chính
Trạng thái buổi tiếp đón
Ghi chú dành cho người tham gia nếu có
```

Không hiển thị:

```text
Nút duyệt
Nút từ chối đơn
Nút gán host
Nút chấp nhận/từ chối tham gia
Thông tin nội bộ không liên quan
Action của HO/Staff Leader
```

## 6.5. Luồng lời mời riêng

Trang lời mời riêng xử lý:

```text
INVITED -> ACCEPTED
INVITED -> DECLINED
```

Sau khi user bấm ACCEPTED, đơn mới hiện trong Tab 2.

Sau khi user bấm DECLINED, đơn không hiện trong Tab 2.

Trạng thái participation nên tách riêng với trạng thái đơn.

Gợi ý:

```text
INVITED
ACCEPTED
DECLINED
CANCELLED
```

Không dùng chung với:

```text
PENDING_APPROVAL
APPROVED
REJECTED
CANCELLED
```

---

## 7. Trạng thái đơn tiếp khách đề xuất

Cần đối chiếu với enum/status hiện tại trong SQL trước khi sửa.

Nếu hệ thống đã có status khác, map tương ứng, không tự đổi tên hàng loạt nếu không cần.

Gợi ý trạng thái nghiệp vụ:

```text
PENDING_APPROVAL
PENDING_HO_APPROVAL
HO_APPROVED
REJECTED
APPROVED
ASSIGNED
PREPARING
READY
IN_PROGRESS
COMPLETED
CANCELLED
```

Ý nghĩa:

```text
PENDING_APPROVAL:
Đơn một cơ sở chờ Staff Leader duyệt.

PENDING_HO_APPROVAL:
Đơn liên cơ sở chờ HO duyệt.

HO_APPROVED:
Đơn liên cơ sở đã được HO duyệt, đang chờ campus xử lý.

REJECTED:
Đơn bị từ chối trong giai đoạn duyệt.

APPROVED:
Đơn đã được duyệt.

ASSIGNED:
Đơn đã duyệt và đã gán host.

PREPARING:
Đang chuẩn bị.

READY:
Sẵn sàng tiếp đón.

IN_PROGRESS:
Đang diễn ra.

COMPLETED:
Đã hoàn tất.

CANCELLED:
Đã hủy.
```

Nếu hiện tại chưa có đủ status, tối thiểu cần hỗ trợ logic tương đương:

```text
PENDING_APPROVAL
PENDING_HO_APPROVAL
HO_APPROVED
REJECTED
APPROVED / ASSIGNED
CANCELLED
COMPLETED
```

---

## 8. API backend cần có hoặc cần cập nhật

Tên API có thể điều chỉnh theo convention hiện tại của project.

## 8.1. API lấy Tab 1 — Đơn phụ trách

Gợi ý:

```http
GET /api/visit-requests/my-responsibilities
```

Hoặc:

```http
GET /api/visit-requests?tab=responsible
```

Backend phải filter theo user hiện tại.

Không trả dữ liệu không thuộc quyền rồi để frontend lọc.

## 8.2. API lấy Tab 2 — Đơn mời tham dự

Gợi ý:

```http
GET /api/visit-requests/my-attending
```

Hoặc:

```http
GET /api/visit-requests?tab=attending
```

Filter:

```text
visit_participants.user_id == currentUser.id
AND visit_participants.status == ACCEPTED
AND request.status NOT IN (REJECTED, CANCELLED)
```

Không áp dụng cho Visitor/Admin.

## 8.3. API HO duyệt đơn liên cơ sở

```http
POST /api/visit-requests/{id}/ho-approve
```

Điều kiện:

```text
User là HO
request.scope == MULTI_CAMPUS
request.status == PENDING_HO_APPROVAL
```

Update:

```text
status = HO_APPROVED
decided_by = currentUser.id
decided_at = now
```

## 8.4. API HO từ chối đơn liên cơ sở

```http
POST /api/visit-requests/{id}/ho-reject
```

Body:

```json
{
  "reason": "Lý do từ chối"
}
```

Điều kiện:

```text
User là HO
request.scope == MULTI_CAMPUS
request.status == PENDING_HO_APPROVAL
reason không rỗng
```

Update:

```text
status = REJECTED
rejection_reason = reason
decided_by = currentUser.id
decided_at = now
```

## 8.5. API Staff Leader duyệt và gán host

```http
POST /api/visit-requests/{id}/approve-and-assign-host
```

Body:

```json
{
  "hostUserId": 123
}
```

Điều kiện:

```text
User có permission ASSIGN_HOST_STAFF
request thuộc campus của user
request.status IN (PENDING_APPROVAL, HO_APPROVED)
hostUserId thuộc cùng campus
hostUserId là Staff hợp lệ
```

Update đơn một cơ sở:

```text
request.status = ASSIGNED
request.host_user_id = hostUserId
request.decided_by = currentUser.id
request.decided_at = now
request.assigned_by = currentUser.id
request.assigned_at = now
```

Với đơn liên cơ sở, ưu tiên update theo campus:

```text
visit_request_campuses.status = ASSIGNED
visit_request_campuses.host_user_id = hostUserId
visit_request_campuses.assigned_by = currentUser.id
visit_request_campuses.assigned_at = now
```

## 8.6. API Staff Leader từ chối đơn campus

```http
POST /api/visit-requests/{id}/campus-reject
```

Body:

```json
{
  "reason": "Lý do từ chối"
}
```

Điều kiện:

```text
User có permission từ chối đơn campus
request thuộc campus user
request.status == PENDING_APPROVAL
reason không rỗng
```

Update:

```text
status = REJECTED
rejection_reason = reason
decided_by = currentUser.id
decided_at = now
```

Nếu là đơn liên cơ sở và business cho phép campus từ chối phần campus mình, update `visit_request_campuses`.

Nếu business không cho phép, không render action này.

## 8.7. API lấy staff có thể làm host

```http
GET /api/campuses/{campusId}/staff/host-candidates?requestId=...
```

Response nên gồm:

```json
[
  {
    "userId": 1,
    "fullName": "Nguyen Van A",
    "email": "a@example.com",
    "campusId": 1,
    "hasScheduleConflict": true,
    "conflicts": [
      {
        "requestId": 10,
        "title": "Đoàn ABC",
        "startTime": "2026-06-20T09:00:00",
        "endTime": "2026-06-20T11:00:00"
      }
    ]
  }
]
```

## 8.8. API kiểm tra trùng lịch host

Nếu không trả chung ở API host candidates, tạo API riêng:

```http
GET /api/staff/{staffId}/schedule-conflicts?startTime=...&endTime=...
```

Overlap:

```text
current.start < other.end
AND current.end > other.start
```

## 8.9. API Visitor hủy đơn

```http
POST /api/visit-requests/{id}/cancel-by-visitor
```

Body:

```json
{
  "reason": "Lý do hủy"
}
```

Điều kiện:

```text
request.created_by == currentUser.id
now < request.start_time
request.status NOT IN (REJECTED, CANCELLED, COMPLETED)
reason không rỗng
```

Update:

```text
status = CANCELLED
cancellation_reason = reason
cancelled_by = currentUser.id
cancelled_at = now
cancellation_source = SELF_SERVICE nếu DB hỗ trợ
```

## 8.10. API Host hủy đơn

```http
POST /api/visit-requests/{id}/cancel-by-host
```

Body:

```json
{
  "reason": "Lý do hủy"
}
```

Điều kiện:

```text
request.host_user_id == currentUser.id
request.status IN (APPROVED, ASSIGNED, PREPARING, READY)
now < request.start_time
reason không rỗng
```

Update:

```text
status = CANCELLED
cancellation_reason = reason
cancelled_by = currentUser.id
cancelled_at = now
```

---

## 9. Frontend cần cập nhật

## 9.1. Layout

Cập nhật màn quản lý thành 2 tab:

```text
Đơn phụ trách | Đơn mời tham dự
```

Visitor:

```text
Chỉ hiển thị Đơn của tôi
Không hiển thị Tab 2
```

Admin:

```text
Không hiển thị màn nghiệp vụ hoặc hiển thị empty state
```

## 9.2. Data fetching

Khi vào tab:

```text
Tab 1 -> gọi API my-responsibilities
Tab 2 -> gọi API my-attending
```

Không lấy toàn bộ rồi lọc frontend.

## 9.3. Render action button

Không render nút chỉ theo status.

Phải xét:

```text
role
permission
scope
campus
status
host_user_id
created_by
start_time
allowedActions
```

### HO

Hiển thị `Duyệt` / `Từ chối` chỉ khi:

```text
role == HO
AND request.scope == MULTI_CAMPUS
AND request.status == PENDING_HO_APPROVAL
```

### Staff Leader

Hiển thị `Duyệt` / `Từ chối` khi:

```text
user has ASSIGN_HOST_STAFF hoặc APPROVE_OWN_CAMPUS...
AND request thuộc campus user
AND request.status IN (PENDING_APPROVAL, HO_APPROVED)
```

Khi bấm duyệt:

```text
Mở popup chọn host
Không duyệt ngay
```

### Host

Hiển thị `Hủy` khi:

```text
request.host_user_id == currentUser.id
AND now < request.start_time
AND request.status IN (APPROVED, ASSIGNED, PREPARING, READY)
```

### Visitor

Hiển thị `Hủy` khi:

```text
request.created_by == currentUser.id
AND now < request.start_time
AND request.status NOT IN (REJECTED, CANCELLED, COMPLETED)
```

## 9.4. Modal từ chối

Tạo hoặc tái sử dụng modal:

```text
Tiêu đề: Từ chối đơn
Textarea: Lý do từ chối
Validation: bắt buộc nhập
Button: Xác nhận từ chối
```

## 9.5. Modal hủy

Tạo modal riêng:

```text
Tiêu đề: Hủy đơn
Textarea: Lý do hủy
Validation: bắt buộc nhập
Button: Xác nhận hủy
```

Không dùng chung wording với từ chối.

## 9.6. Modal chọn host

Khi Staff Leader bấm duyệt:

```text
Mở modal chọn Staff làm host
Hiển thị danh sách Staff cùng campus
Hiển thị cảnh báo trùng lịch nếu có
Chỉ cho xác nhận khi đã chọn host
Sau xác nhận mới gọi approve-and-assign-host
```

## 9.7. Badge/Nhãn hiển thị

Cần thêm badge để phân biệt:

```text
Một cơ sở
Liên cơ sở
HO đã duyệt
Đơn từ HO
Cần campus xử lý
Được giao làm host
Đơn mời tham dự
Cần chuẩn bị
Tiếp tục chuẩn bị
Sẵn sàng tiếp đón
Đang tiếp đón
Đã hủy
Bị từ chối
```

Với đơn liên cơ sở Staff Leader nhận từ HO, hiển thị:

```text
Liên cơ sở · HO đã duyệt
```

---

## 10. Permission/RBAC cần kiểm tra và map

Không tự thêm permission nếu hệ thống đang dùng UC code cố định mà chưa cập nhật seed.

Cần kiểm tra `PERMISSION_MATRIX.md` và seed SQL.

Các permission nghiệp vụ cần có hoặc map tương đương:

```text
VIEW_VISIT_REQUEST_LIST
VIEW_VISIT_REQUEST_DETAIL

VIEW_ALL_CAMPUS_VISIT_REQUESTS
VIEW_OWN_CAMPUS_VISIT_REQUESTS
VIEW_ASSIGNED_VISIT_REQUESTS
VIEW_ACCEPTED_PARTICIPATION_REQUESTS

APPROVE_MULTI_CAMPUS_REQUEST_AS_HO
REJECT_MULTI_CAMPUS_REQUEST_AS_HO

APPROVE_OWN_CAMPUS_REQUEST_AS_STAFF_LEADER
REJECT_OWN_CAMPUS_REQUEST_AS_STAFF_LEADER

ASSIGN_HOST_STAFF

CANCEL_OWN_REQUEST_AS_VISITOR
CANCEL_ASSIGNED_REQUEST_AS_HOST

VIEW_INVITATION_LIST
ACCEPT_INVITATION
DECLINE_INVITATION
```

Lưu ý:

```text
ACCEPT_INVITATION
DECLINE_INVITATION
```

Không thuộc trang này. Hai quyền này thuộc trang lời mời riêng.

Nếu permission hiện tại đang theo format UC, ví dụ:

```text
UC-XX.ACTION_NAME
```

Thì phải dùng đúng permission code hiện có, không tự đặt tên mới trong code.

---

## 11. SQL/Schema compatibility checklist

Trước khi cập nhật code, cần xác nhận:

```text
1. visit_requests có đủ status/scope để phân biệt single-campus và multi-campus không?
2. visit_request_campuses có đủ dữ liệu để biết campus nào liên quan đến đơn liên cơ sở không?
3. Có cột host_user_id ở visit_requests hay visit_request_campuses không?
4. Nếu đơn liên cơ sở cần host riêng từng campus, host phải nằm ở visit_request_campuses.
5. visit_participants có status ACCEPTED/DECLINED/INVITED không?
6. Có bảng/task nào thể hiện người được giao nhiệm vụ không?
7. cancellation_source enum hiện tại có những giá trị nào?
8. rejection_reason và cancellation_reason đang lưu ở đâu?
9. Có row_version cho concurrency không?
10. Có audit fields decided_by/assigned_by/cancelled_by không?
```

Nếu schema hiện tại chưa đủ, không sửa bừa code. Hãy tạo ghi chú TODO hoặc migration/script riêng theo chuẩn database-first của project.

---

## 12. Concurrency và bảo vệ nghiệp vụ

Vì luồng duyệt/từ chối/hủy dễ bị double-click hoặc nhiều người xử lý cùng lúc, backend cần validate bằng status hiện tại.

Ví dụ HO duyệt:

```sql
WHERE visit_request_id = @id
AND status = 'PENDING_HO_APPROVAL'
```

Nếu có `row_version`, dùng thêm:

```sql
AND row_version = @rowVersion
```

Khi update thành công:

```text
row_version = row_version + 1
```

Nếu update 0 rows:

```text
Trả lỗi: Đơn đã được người khác xử lý hoặc trạng thái đã thay đổi.
```

Tương tự cho:

```text
HO reject
Staff Leader approve and assign host
Staff Leader reject
Host cancel
Visitor cancel
```

---

## 13. Luồng nghiệp vụ tổng hợp

## 13.1. Đơn một cơ sở

```text
Visitor gửi form
        ↓
Đơn thuộc một campus
        ↓
Staff Leader campus đó nhìn thấy trong Tab Đơn phụ trách
        ↓
Staff Leader xem chi tiết
        ↓
Staff Leader chọn:
    - Từ chối -> nhập lý do -> REJECTED
    - Duyệt -> mở popup chọn Staff host
        ↓
Chọn Staff host
        ↓
Hệ thống cảnh báo nếu Staff trùng lịch
        ↓
Xác nhận
        ↓
Đơn chuyển sang ASSIGNED/PREPARING
        ↓
Staff/Host được giao nhìn thấy trong Tab Đơn phụ trách
        ↓
Host chuẩn bị tiếp đón
        ↓
Nếu chưa tới thời gian bắt đầu, Host có thể hủy và nhập lý do
```

## 13.2. Đơn liên cơ sở

```text
Visitor gửi form
        ↓
Form có nhiều campus
        ↓
Đơn vào trạng thái PENDING_HO_APPROVAL
        ↓
HO nhìn thấy trong Tab Đơn phụ trách
        ↓
HO xem chi tiết
        ↓
HO chọn:
    - Từ chối -> nhập lý do -> REJECTED
    - Duyệt -> HO_APPROVED
        ↓
Đơn xuất hiện cho Staff Leader của từng campus liên quan
        ↓
Staff Leader mỗi campus xử lý phần campus mình
        ↓
Staff Leader chọn Staff/Host phụ trách campus mình
        ↓
Host được giao nhìn thấy trong Tab Đơn phụ trách
        ↓
Host chuẩn bị tiếp đón
        ↓
Nếu chưa tới thời gian bắt đầu, Host có thể hủy theo phạm vi được giao
```

## 13.3. Luồng lời mời tham dự

```text
Host/người phụ trách gửi lời mời tham gia
        ↓
Người được mời thấy ở trang Lời mời tham gia
        ↓
Người được mời chọn:
    - Chấp nhận
    - Từ chối
        ↓
Nếu Chấp nhận:
    participation_status = ACCEPTED
    đơn xuất hiện ở Tab Đơn mời tham dự
        ↓
Nếu Từ chối:
    participation_status = DECLINED
    đơn không xuất hiện ở Tab Đơn mời tham dự
```

---

## 14. Test cases bắt buộc

## 14.1. HO

```text
HO thấy đơn liên cơ sở chờ duyệt.
HO có nút Duyệt/Từ chối với đơn liên cơ sở đang PENDING_HO_APPROVAL.
HO từ chối bắt buộc nhập lý do.
HO duyệt xong đơn chuyển về Staff Leader các campus liên quan.
HO xem đơn một cơ sở nhưng không có nút Duyệt/Từ chối/Gán host/Hủy.
HO được mời tham dự và đã chấp nhận thì thấy đơn ở Tab 2.
HO chưa chấp nhận lời mời thì không thấy ở Tab 2.
```

## 14.2. Staff Leader

```text
Staff Leader campus A thấy đơn một cơ sở campus A.
Staff Leader campus A không thấy đơn campus B.
Staff Leader không thấy đơn liên cơ sở trước khi HO duyệt.
Staff Leader thấy đơn liên cơ sở sau khi HO duyệt nếu đơn có campus A.
Staff Leader không thấy đơn liên cơ sở không chứa campus A.
Staff Leader bấm Duyệt thì mở popup chọn host.
Không chọn host thì không cho xác nhận.
Chọn Staff cùng campus thì duyệt thành công.
Chọn Staff có lịch trùng thì hiển thị cảnh báo.
Từ chối đơn bắt buộc nhập lý do.
```

## 14.3. Staff / Host

```text
Staff không thấy đơn nếu chưa được giao.
Staff thấy đơn sau khi được gán host.
Staff không có nút duyệt/từ chối.
Host có nút hủy nếu đơn đã được giao và chưa tới thời gian bắt đầu.
Sau thời gian bắt đầu, nút hủy bị ẩn hoặc disable.
Host hủy bắt buộc nhập lý do.
Staff được mời tham dự và đã chấp nhận thì thấy đơn ở Tab 2.
```

## 14.4. Department / Department Leader

```text
Department không thấy đơn nếu không được giao nhiệm vụ hoặc chưa chấp nhận tham gia.
Department thấy ở Tab 1 nếu được giao nhiệm vụ cụ thể.
Department thấy ở Tab 2 nếu đã chấp nhận lời mời tham gia.
Department không có nút duyệt/từ chối/gán host/hủy.
```

## 14.5. Student

```text
Student không thấy đơn không liên quan.
Student thấy ở Tab 1 nếu được giao nhiệm vụ hỗ trợ cụ thể.
Student thấy ở Tab 2 nếu đã chấp nhận lời mời tham gia.
Student không có nút duyệt/từ chối/gán host/hủy.
```

## 14.6. Visitor

```text
Visitor chỉ thấy đơn do mình gửi.
Visitor không có Tab 2.
Visitor không có nút sửa/cập nhật/bổ sung thông tin.
Visitor có nút hủy nếu chưa tới thời gian bắt đầu.
Visitor hủy bắt buộc nhập lý do.
Visitor không xem được phân công nội bộ không cần thiết.
```

## 14.7. Admin

```text
Admin không tham gia luồng tiếp khách.
Admin không có action Duyệt/Từ chối/Gán host/Hủy.
Admin không được dùng màn này để xử lý nghiệp vụ.
```

## 14.8. Tab Đơn mời tham dự

```text
User được mời nhưng chưa phản hồi -> không xuất hiện ở Tab 2.
User đã chấp nhận -> xuất hiện ở Tab 2.
User đã từ chối -> không xuất hiện ở Tab 2.
Tab 2 không có nút chấp nhận/từ chối tham gia.
Tab 2 không có nút duyệt/từ chối đơn.
Tab 2 không có nút gán host.
```

---

## 15. Yêu cầu về UI/UX

## 15.1. Không làm layout quá phức tạp

Màn hình cần rõ ràng, dễ dùng, tránh filter quá dài gây vỡ UI.

Gợi ý:

```text
Hàng 1: Search + Status + Scope
Hàng 2: Từ ngày + Đến ngày + Button áp dụng/xóa lọc
```

Hoặc responsive bằng wrap/flex thay vì grid cố định.

## 15.2. Card/Table theo màn hình

Desktop:

```text
Dùng table hoặc grid rõ cột:
Tên đoàn | Loại đơn | Campus | Thời gian | Trạng thái | Người phụ trách | Hành động
```

Mobile/Tablet:

```text
Dùng card.
Mỗi card có badge, thời gian, campus, trạng thái, action.
```

## 15.3. Badge ưu tiên

Cần hiển thị rõ:

```text
Liên cơ sở · HO đã duyệt
Được giao làm host
Đơn mời tham dự
Cần chuẩn bị
Tiếp tục chuẩn bị
Sẵn sàng tiếp đón
```

## 15.4. Empty state theo role

Ví dụ:

```text
Bạn chưa có đơn phụ trách nào.
Bạn chưa có đơn mời tham dự nào.
Admin không tham gia luồng xử lý đơn tiếp khách.
```

---

## 16. Kết quả mong muốn sau khi cập nhật

Sau khi cập nhật, màn quản lý đơn tiếp khách phải đạt:

```text
1. Mỗi role nhìn thấy danh sách đúng phạm vi.
2. HO chỉ thao tác với đơn liên cơ sở.
3. Staff Leader xử lý đơn campus mình và đơn liên cơ sở sau khi HO duyệt.
4. Staff chỉ thấy đơn được giao.
5. Visitor chỉ thấy đơn của mình và chỉ được hủy trước thời gian bắt đầu.
6. Admin không tham gia luồng tiếp khách.
7. Đơn mời tham dự chỉ xuất hiện sau khi user đã chấp nhận ở trang lời mời riêng.
8. Không có sửa đơn sau khi gửi.
9. Từ chối và hủy là hai luồng riêng.
10. Khi Staff Leader duyệt bắt buộc chọn host.
11. Khi chọn host có cảnh báo trùng lịch.
12. Backend validate đầy đủ, không phụ thuộc vào frontend.
13. UI rõ ràng, không vỡ layout, có badge phân biệt loại đơn/trạng thái.
```

---

## 17. Lưu ý cuối cùng cho người/code AI thực hiện

Không được chỉ sửa frontend.

Phải làm theo thứ tự:

```text
1. Đọc SQL hiện tại.
2. Đọc tài liệu tổng quan project/use case/permission matrix.
3. Xác định đúng role và permission hiện có.
4. Xác định entity/table/column thực tế.
5. Cập nhật backend query/filter/action.
6. Cập nhật frontend tabs/action/modal/badge.
7. Cập nhật permission mapping nếu cần.
8. Chạy build backend.
9. Chạy build frontend.
10. Test từng role theo test cases.
```

Nếu phát hiện schema hiện tại thiếu cột/trạng thái cần thiết, không tự sửa lẻ trong code. Hãy tạo TODO/schema patch riêng theo chuẩn database-first của project.
