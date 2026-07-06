> [!WARNING]
> **LEGACY ARCHITECTURE NOTE (Campus-independent Approval Update)**
> This document has been updated to reflect the new Campus-independent Approval architecture.
> - **HO is now monitor/read-only.** There is no centralized multi-campus approval by HO.
> - **Staff Leader approval is per-campus.** Each Staff Leader directly receives and approves/rejects their own campus instance right after submission.
> - **Self-hosting is supported.** Staff Leaders can assign themselves as the host during approval.
> - **ASSIGNED is removed.** Approving a request now requires assigning a host immediately.
> - **New statuses:** `PARTIALLY_APPROVED` (request level) and `REJECTED` (campus level) are added. 
> - **Cancel logic:** Visitors can cancel requests in `PENDING_APPROVAL` or `PARTIALLY_APPROVED` states.
> - **Transportation:** `transportation_note` and `transportation_note` are replaced by `transportation_note`.
> Please refer to the latest codebase and SQL schema for the current implementation.

# PEMS — Yêu cầu tách trang xử lý / summary / contribution theo role

## 1. Mục tiêu

Tài liệu này đặc tả yêu cầu thiết kế và triển khai lại luồng truy cập trang xử lý tiếp khách trong PEMS, nhằm tránh lỗi Staff hoặc role không liên quan vẫn xem được quy trình nội bộ của một chuyến thăm.

Mục tiêu chính:

- Không cho nhiều role cùng vào chung trang xử lý của Host rồi chỉ ẩn nút bằng frontend.
- Tách rõ trang theo mục đích sử dụng:
  - Host xử lý.
  - Leader/HO giám sát.
  - Participant đóng góp.
  - Department xử lý task.
  - Visitor xem public-safe.
  - Admin không tham gia nghiệp vụ tiếp khách.
- Backend là nơi quyết định quyền cuối cùng.
- Frontend chỉ render route, section và action theo permission/allowedActions backend trả về.

Không được:

- Không dùng mock data.
- Không tạo lại dynamic permissions table.
- Không phân quyền bằng label tiếng Việt hoặc `statusText`.
- Không chỉ ẩn nút bằng frontend trong khi API vẫn cho gọi.
- Không cho xem theo role chung chung kiểu `role_code = STAFF`.

---

## 2. Vấn đề hiện tại

Route hiện tại:

```text
/dashboard/visit/process/:visitInstanceId
```

đang được dùng như trang xử lý quy trình tiếp khách. Tuy nhiên trang này có nguy cơ bị truy cập bởi Staff hoặc role không liên quan nếu họ đoán URL, hoặc nếu frontend fallback permission sai.

Vấn đề nghiệp vụ:

- Trang `process` hiện tại là trang thao tác vận hành của Host.
- Không nên cho mọi role vào trang này rồi chỉ ẩn nút.
- Người giám sát chỉ nên xem summary read-only.
- Người tham gia đã `ACCEPTED` nên có trang contribution riêng để xem summary và đóng góp kết quả như biên bản, ảnh/media, tin tức.
- Department task và invitation cần tách mục đích rõ ràng.

---

## 3. Nguyên tắc phân quyền chốt

Không phân quyền kiểu:

```text
role_code = STAFF => được xem process
```

Phải phân quyền theo:

```text
role_code
+ sub_role
+ primary_campus_id / department_id
+ relation với visitInstanceId
+ participant status
+ task assignment
+ visit request scope
+ campus instance status
```

Rule tổng:

```text
Host chính            => Host Operation Page
HO / Staff Leader     => Summary Page read-only
Participant accepted  => Contribution Page
Department task user  => Department Task Page
Người được mời        => Invitation Page
Visitor               => Reception Detail public-safe
Admin                 => Không tham gia luồng visit
```

---

## 4. Danh sách route cần chốt

| Loại trang | Route | Mục đích | Ai được vào |
|---|---|---|---|
| Host Operation Page | `/dashboard/visit/process/:visitInstanceId` | Trang xử lý chính của Host | Chỉ Host chính |
| Summary Page | `/dashboard/visit/process-summary/:visitInstanceId` | Xem tổng quan/kết quả read-only | HO, Staff Leader, người có quyền giám sát |
| Contribution Page | `/dashboard/visit/contribution/:visitInstanceId` | Người tham gia đã nhận lời xem summary + đóng góp biên bản/media/tin tức | IC Support, Staff participant, Department, Student đã `ACCEPTED/ASSIGNED` |
| Invitation Page | `/dashboard/visit/invitations/:participantId` | Xem và phản hồi lời mời tham gia | Người được mời nhưng chưa/đang phản hồi |
| Department Task Page | `/dashboard/visit/department-tasks/:participantId` | Xử lý nhiệm vụ/logistics cụ thể của phòng ban | Department Leader/Staff |
| Visitor Reception Detail | `/dashboard/visit/reception-detail/:id` | Trang khách xem thông tin public-safe | Visitor |

---

## 5. Mô tả từng loại trang

### 5.1. Host Operation Page

Route:

```text
/dashboard/visit/process/:visitInstanceId
```

Mục đích:

- Trang xử lý chính của Host.
- Đây là trang thao tác vận hành nội bộ.

Chỉ cho Host chính vào:

```text
current_user.user_id = visit_request_campuses.current_host_user_id
```

Host được thao tác:

- Xem form yêu cầu gốc.
- Chỉnh agenda.
- Mời người tham gia.
- Gửi yêu cầu logistics.
- Cấu hình reminder.
- Lưu ghi chú chuẩn bị.
- Xử lý trước/trong/sau tiếp khách.
- Chuyển trạng thái phase.
- Đóng đoàn.

Không cho:

- Staff thường không phải Host.
- IC Support.
- Staff Leader.
- HO.
- Department.
- Student.
- Visitor.
- Admin.

Nếu không phải Host chính:

- Backend trả `403` hoặc `404` theo convention hiện tại.
- Frontend hiển thị Access Denied.
- Không render fallback page.

---

### 5.2. Summary Page

Route đề xuất:

```text
/dashboard/visit/process-summary/:visitInstanceId
```

Mục đích:

- Trang xem tổng quan/kết quả read-only.
- Dành cho người có quyền giám sát hoặc theo dõi.

Dành cho:

- HO với đơn `MULTI_CAMPUS` đã được HO duyệt.
- Staff Leader cùng campus với visit instance.
- Host nếu cần xem lại summary read-only.
- Người có quyền giám sát theo relation hợp lệ.

Trang này chỉ đọc, không có thao tác vận hành.

Không được có:

- Nút lưu agenda.
- Nút gửi logistics.
- Nút mời/xóa participant.
- Nút chuyển phase.
- Nút đóng đoàn.
- Nút sửa thông tin chuẩn bị.
- Nút thao tác biên bản/media/news nếu không có quyền contribution riêng.

Có thể hiển thị:

- Thông tin request gốc ở mức được phép.
- Agenda đã chốt.
- Danh sách người tham gia.
- Logistics summary.
- Trạng thái từng phase.
- Biên bản đã tạo.
- Media đã upload.
- Tin tức liên quan.
- Feedback/kết quả sau tiếp khách.
- Timeline trạng thái cơ bản.

---

### 5.3. Contribution Page

Route đề xuất:

```text
/dashboard/visit/contribution/:visitInstanceId
```

Mục đích:

- Trang dành cho người được mời tham gia và đã `ACCEPTED/ASSIGNED`.
- Trang này gồm:
  - Summary read-only của chuyến thăm.
  - Workspace đóng góp kết quả: biên bản, ảnh/media, tin tức.

Điều kiện được vào:

```sql
EXISTS visit_participants
WHERE visit_participants.visit_instance_id = targetVisitInstanceId
  AND visit_participants.user_id = currentUser.user_id
  AND visit_participants.status IN ('ACCEPTED', 'ASSIGNED')
```

Hoặc:

```text
current user là Host chính, nếu muốn Host có link review contribution
```

Không cho:

- Người chưa `ACCEPTED`.
- Người đã `DECLINED`.
- Người đã `REMOVED`.
- Người không có participant relation.
- Visitor.
- Admin.
- Staff đoán URL nhưng không liên quan.

Điểm quan trọng:

```text
Được vào Contribution Page không có nghĩa là được sửa tất cả.
```

Quyền xem/sửa từng section phải do backend trả về.

---

### 5.4. Invitation Page

Route:

```text
/dashboard/visit/invitations/:participantId
```

Mục đích:

```text
Tôi có nhận lời tham gia chuyến này không?
```

Dành cho:

- IC Staff được mời hỗ trợ.
- Department user được mời tham gia.
- Student được mời hỗ trợ.
- Staff participant chưa phản hồi.

Có:

- Thông tin đoàn cơ bản.
- Campus.
- Thời gian.
- Người mời.
- Vai trò được mời.
- Mô tả lời mời.
- Nút Chấp nhận.
- Nút Từ chối + lý do.

Sau khi `ACCEPTED`:

```text
Điều hướng sang /dashboard/visit/contribution/:visitInstanceId
```

---

### 5.5. Department Task Page

Route:

```text
/dashboard/visit/department-tasks/:participantId
```

Mục đích:

```text
Tôi/phòng ban của tôi cần xử lý nhiệm vụ gì?
```

Dành cho:

- Department Leader.
- Department Staff.

Có:

- Yêu cầu logistics/task được giao.
- Mô tả nhiệm vụ.
- Deadline.
- Người giao.
- Department phụ trách.
- Trạng thái task.
- Nút nhận việc.
- Nút từ chối.
- Nút đề xuất thay đổi.
- Nút cập nhật hoàn tất.
- Handover/ký nhận/ký trả nếu task liên quan thiết bị.

Khác với Invitation Page:

- Invitation Page: xác nhận có tham gia không.
- Department Task Page: xử lý công việc cụ thể đã được giao.

---

### 5.6. Visitor Reception Detail

Route:

```text
/dashboard/visit/reception-detail/:id
```

Mục đích:

- Trang public-safe cho Visitor.

Visitor không được vào:

- `/process`
- `/process-summary`
- `/contribution`
- `/department-tasks`

Visitor chỉ xem:

- Đơn của mình.
- Form đã gửi.
- Lịch/campus/trạng thái được phép.
- Lý do hủy/từ chối nếu có.
- Thông tin public-safe.

---

## 6. Role matrix — role nào thấy gì

### 6.1. ADMIN

Danh sách:

- Không tham gia danh sách tiếp khách nghiệp vụ.

Trang được vào:

- Không vào `process`, `summary`, `contribution`, `task`.

Được làm:

- Không thao tác nghiệp vụ tiếp khách.

Rule:

```text
Nếu Admin mở các route visit process/contribution/summary => 403/Access Denied.
```

---

### 6.2. VISITOR

Danh sách:

- Chỉ thấy “Đơn của tôi”.

Trang được vào:

- `reception-detail`
- form yêu cầu đã gửi
- modal lý do hủy/từ chối

Thấy:

- Thông tin public-safe.
- Lịch tiếp khách đã được duyệt.
- Campus/progress ở mức public-safe.
- Lý do hủy/từ chối nếu có.

Được làm:

- Hủy đơn `PENDING_APPROVAL`.
- Hủy single-campus sau duyệt nếu chưa bắt đầu.
- Hủy multi-campus tổng nếu chưa campus nào bắt đầu.
- Hủy từng campus chưa bắt đầu nếu multi-campus đã có campus bắt đầu.

Không được:

- Xem logistics nội bộ.
- Xem biên bản nội bộ.
- Xem audit nội bộ.
- Vào Host Process.
- Vào Contribution nội bộ.

---

### 6.3. HO

Danh sách:

- Thấy đơn liên cơ sở.
- Có thể thấy đơn single-campus ở read-only nếu rule dự án đã chốt như vậy.

Trang được vào:

- `ho-detail` cho xử lý/duyệt multi-campus.
- `process-summary` cho multi-campus đã duyệt.

Thấy:

- Request tổng.
- Danh sách campus con.
- Tiến trình từng campus.
- Trạng thái từng campus.
- Kết quả từng campus ở read-only.

Được làm:

- Duyệt/từ chối multi-campus khi request đang `PENDING_APPROVAL`.
- Theo dõi sau khi duyệt.

Không được:

- Vào Host Operation Page.
- Sửa agenda/logistics/minutes/media/news.
- Chuyển phase.
- Đóng đoàn.
- Xử lý single-campus.

Backend allow summary nếu:

```text
role_code = HO
AND visit_requests.visit_scope = MULTI_CAMPUS
AND visit_requests.status = APPROVED
```

---

### 6.4. STAFF LEADER

Danh sách:

- Thấy đơn single-campus thuộc campus mình.
- Thấy campus instance của multi-campus thuộc campus mình sau khi HO duyệt.
- Không thấy đơn campus khác để thao tác.

Trang được vào:

- `process-summary` với campus instance thuộc campus mình.

Thấy:

- Form yêu cầu.
- Agenda đã chốt.
- Thành phần tham gia.
- Logistics summary.
- Trạng thái phase.
- Biên bản/media/news/kết quả sau tiếp khách nếu có.

Được làm:

- Duyệt/từ chối single-campus trước vận hành.
- Gán Host khi duyệt.
- Với multi-campus sau HO duyệt: gán/chốt Host cho campus mình nếu đúng rule.
- Sau khi Host đã được giao: chỉ giám sát read-only.

Không được:

- Vào Host Operation Page nếu không phải `current_host_user_id`.
- Sửa agenda/logistics.
- Chuyển phase.
- Đóng đoàn.
- Thao tác thay Host.

Backend allow summary nếu:

```text
role_code = STAFF
AND sub_role = LEADER
AND users.primary_campus_id = visit_request_campuses.campus_id
```

---

### 6.5. IC STAFF — HOST CHÍNH

Danh sách:

- Thấy đơn mình được gán làm Host.

Trang được vào:

- `process/:visitInstanceId`

Thấy:

- Full quy trình trước/trong/sau.
- Request gốc.
- Agenda.
- Participant.
- Logistics.
- Reminder.
- Preparation note.
- Minutes.
- Media.
- News.
- Feedback/kết quả.
- Phase transition bar.

Được làm:

- Chỉnh agenda.
- Mời participant.
- Gửi logistics.
- Lưu reminder.
- Lưu ghi chú chuẩn bị.
- Xử lý trong tiếp khách.
- Xử lý sau tiếp khách.
- Upload/review media nếu có.
- Tạo/review news nếu có.
- Tạo/sửa minutes theo lock rule.
- Chuyển `BEFORE_VISIT -> DURING_VISIT`.
- Chuyển `DURING_VISIT -> AFTER_VISIT`.
- Đóng đoàn khi đủ điều kiện.

Backend allow process nếu:

```text
role_code = STAFF
AND user_id = visit_request_campuses.current_host_user_id
```

---

### 6.6. IC STAFF — SUPPORT ĐÃ ACCEPTED/ASSIGNED

Danh sách:

- Thấy lời mời/tham gia của mình.

Trang được vào:

- `invitations/:participantId` nếu chưa phản hồi.
- `contribution/:visitInstanceId` nếu đã `ACCEPTED/ASSIGNED`.

Thấy trong Contribution:

- Request summary.
- Agenda.
- Participant summary.
- Logistics summary phù hợp.
- Minutes/media/news theo permission.

Được làm tùy permission:

- Viết/sửa biên bản nếu được giao.
- Upload media nếu được giao hoặc rule cho IC Support cho phép.
- Viết/sửa tin tức nếu được giao.
- Xem summary để hiểu bối cảnh.

Không được:

- Vào Host Operation Page.
- Sửa agenda.
- Gửi logistics mới.
- Mời/xóa participant.
- Chuyển phase.
- Đóng đoàn.

Backend allow contribution nếu:

```text
EXISTS visit_participants
WHERE user_id = currentUser
  AND visit_instance_id = target
  AND participant_role = IC_SUPPORT
  AND status IN ('ACCEPTED', 'ASSIGNED')
```

---

### 6.7. IC STAFF KHÔNG LIÊN QUAN

Danh sách:

- Không thấy đơn đó.

Trang được vào:

- Không.

Nếu đoán URL:

- `/process` => `403/404`
- `/summary` => `403/404`
- `/contribution` => `403/404`

Không được xem dữ liệu.

---

### 6.8. DEPARTMENT LEADER

Danh sách:

- Thấy task/logistics/yêu cầu liên quan department mình.
- Có thể thấy lời mời nếu được mời tham gia.

Trang được vào:

- `department-tasks/:participantId`
- `contribution/:visitInstanceId` nếu có participant relation `ACCEPTED/ASSIGNED`
- Summary chỉ khi có rule riêng cho giám sát department; nếu không thì không.

Thấy:

- Summary cần thiết của chuyến thăm.
- Agenda.
- Task/logistics thuộc department mình.
- Trạng thái xử lý task.
- Participant liên quan.

Được làm:

- Nhận/xác nhận yêu cầu department.
- Phân công Department Staff.
- Từ chối/propose thay đổi nếu rule cho phép.
- Cập nhật trạng thái task.
- Contribution nếu được giao media/news/minutes.

Không được:

- Vào Host Operation Page.
- Xem logistics của department khác nếu không liên quan.
- Chuyển phase.
- Sửa agenda.

---

### 6.9. DEPARTMENT STAFF

Danh sách:

- Thấy nhiệm vụ được Department Leader giao.
- Thấy lời mời/tham gia nếu là participant.

Trang được vào:

- `department-tasks/:participantId`
- `contribution/:visitInstanceId` nếu `ACCEPTED/ASSIGNED`

Thấy:

- Task được giao.
- Summary cần thiết.
- Logistics liên quan mình.
- Agenda liên quan.
- Minutes/media/news nếu được phép.

Được làm:

- Nhận task.
- Từ chối task nếu rule cho phép.
- Cập nhật tiến độ task.
- Hoàn tất task.
- Ký nhận/ký trả nếu liên quan handover.
- Đóng góp media/news/minutes nếu được giao.

Không được:

- Vào Host Operation Page.
- Xem toàn bộ logistics nội bộ.
- Chuyển phase.
- Đóng đoàn.

---

### 6.10. STUDENT

Danh sách:

- Thấy lời mời tham gia của mình.

Trang được vào:

- `invitations/:participantId` nếu chưa phản hồi.
- `contribution/:visitInstanceId` nếu `ACCEPTED/ASSIGNED`.

Thấy:

- Summary cơ bản.
- Agenda.
- Vai trò/nhiệm vụ của mình.
- Media/news/minutes nếu được phép.

Được làm:

- Accept/decline lời mời.
- Upload media nếu được giao.
- Viết tin nếu được giao.
- Xem summary để biết lịch/nhiệm vụ.

Không được:

- Vào Host Operation Page.
- Sửa agenda.
- Xem logistics nội bộ không liên quan.
- Chuyển phase.
- Đóng đoàn.

---

## 7. Contribution Page — thiết kế UI chi tiết

Route:

```text
/dashboard/visit/contribution/:visitInstanceId
```

Tên trang đề xuất:

```text
Đóng góp kết quả chuyến thăm
```

hoặc:

```text
Kết quả sau tiếp khách
```

### 7.1. Layout tổng thể

#### Header

- Breadcrumb: `Dashboard / Quản lý tiếp khách / Đóng góp kết quả`.
- Tên đoàn.
- Badge trạng thái chuyến thăm.
- Campus.
- Thời gian.
- Host chính.
- Vai trò của tôi trong chuyến thăm.

#### Access state

- Loading skeleton khi đang tải.
- Access Denied nếu `403/404`.
- Empty state nếu chưa có dữ liệu section.
- Read-only banner nếu instance `CLOSED/CANCELLED`.

---

### 7.2. Nhóm A — Summary read-only

#### Section A1. Thông tin yêu cầu

Hiển thị:

- Tên đoàn.
- Tổ chức/đơn vị.
- Mục đích chuyến thăm.
- Loại chuyến thăm.
- Campus.
- Thời gian.
- Host chính.
- Số lượng khách.
- Ngôn ngữ/phiên dịch nếu có.
- Ghi chú được phép chia sẻ.

Không hiển thị mặc định:

- Thông tin liên hệ cá nhân nhạy cảm nếu không cần.
- Audit nội bộ.
- Ghi chú duyệt/từ chối nội bộ.
- Decision note nội bộ nếu không được phép.

#### Section A2. Agenda/Lịch trình

Hiển thị:

- Thời gian bắt đầu/kết thúc.
- Nội dung.
- Địa điểm.
- Người phụ trách.
- Ghi chú công khai nếu có.

Không cho sửa agenda trên Contribution Page.

#### Section A3. Thành phần tham gia

Hiển thị:

- Host.
- IC Support.
- Department Support.
- Student Support.
- Vai trò.
- Trạng thái cơ bản nếu được phép.

Không có nút mời/xóa participant.

#### Section A4. Logistics/Hậu cần liên quan

Hiển thị theo permission:

- IC Support: có thể xem logistics summary rộng hơn nếu `canViewFullLogisticsSummary = true`.
- Department: chỉ xem task/logistics thuộc department mình hoặc được assign cho mình.
- Student: chỉ xem logistics/task liên quan nhiệm vụ của mình.
- Người viết tin/biên bản/media: xem logistics ở mức summary để hiểu bối cảnh.

Trạng thái logistics hiển thị dạng:

- Đã yêu cầu.
- Đã phân công.
- Đã chấp nhận.
- Đang xử lý.
- Hoàn tất.
- Từ chối.
- Đề xuất thay đổi.
- Đã hủy.

Không hiển thị mặc định:

- Note nội bộ nhạy cảm.
- Ký mượn/trả chi tiết của department khác.
- Thông tin staff department khác nếu không liên quan.

---

### 7.3. Nhóm B — Workspace đóng góp

#### Section B1. Biên bản

Render nếu:

```text
canViewMinutes = true
```

Nếu:

```text
canEditMinutes = true
```

thì:

- Cho tạo/sửa biên bản.
- Áp dụng lock rule hiện tại.
- Một thời điểm chỉ một editor nếu hệ thống đang có lock.
- Nếu bị khóa bởi người khác thì hiển thị read-only + thông báo.

Nếu:

```text
canEditMinutes = false
```

thì:

- Chỉ xem biên bản nếu đã có.
- Không hiện nút lưu/sửa.

Rule trạng thái:

- `DURING_VISIT`: có thể mở biên bản nếu có quyền.
- `AFTER_VISIT`: cho hoàn thiện biên bản nếu có quyền.
- `CLOSED/CANCELLED`: read-only.

#### Section B2. Ảnh/Media

Render nếu:

```text
canViewMedia = true
```

Nếu:

```text
canUploadMedia = true
```

thì:

- Cho upload ảnh/video.
- Xem danh sách media mình đã upload.
- Sửa/xóa media nếu chưa khóa.
- Validate tối thiểu 1 ảnh nếu rule sau tiếp khách yêu cầu.

Nếu:

```text
canUploadMedia = false
```

thì:

- Chỉ xem media được phép hiển thị.
- Không có upload button.

Rule trạng thái:

- `BEFORE_VISIT`: chưa mở upload ảnh sau tiếp khách, trừ media chuẩn bị nếu có rule riêng.
- `DURING_VISIT`: có thể chưa mở hoặc cho draft.
- `AFTER_VISIT`: mở upload.
- `CLOSED`: read-only.
- `CANCELLED`: read-only hoặc ẩn nếu không có dữ liệu.

#### Section B3. Tin tức

Render nếu:

```text
canViewNews = true
```

Nếu:

```text
canCreateNews = true
```

hoặc:

```text
canEditNews = true
```

thì:

- Cho tạo bài tin nếu chưa có.
- Cho sửa bài nếu status chưa `APPROVED`.
- Nếu `REJECTED` thì cho sửa và gửi lại.
- Nếu `PENDING_APPROVAL` thì chỉ xem trạng thái hoặc cho sửa tùy rule đã chốt.
- Nếu `APPROVED` thì chỉ xem, không sửa.

Nếu khách không đồng ý truyền thông hoặc Host xác nhận không cần bài tin:

- Không cho tạo tin tức.
- Hiển thị lý do:

```text
Chuyến thăm này không yêu cầu bài tin tức.
```

---

## 8. Summary Page — thiết kế UI chi tiết

Route:

```text
/dashboard/visit/process-summary/:visitInstanceId
```

Tên trang đề xuất:

```text
Tổng quan quy trình tiếp khách
```

hoặc:

```text
Theo dõi kết quả tiếp khách
```

### Layout

#### Header

- Tên đoàn.
- Campus.
- Thời gian.
- Host.
- Status badge.
- Relation badge: `HO`, `Staff Leader`, `Read-only`.

#### Sections read-only

- Thông tin yêu cầu.
- Agenda.
- Thành phần tham gia.
- Logistics summary.
- During visit result.
- After visit result.
- Minutes.
- Media.
- News.
- Feedback nếu có.
- Timeline trạng thái cơ bản.

Không có:

- Edit icon.
- Save button.
- Transition button.
- Upload button.
- Invite participant button.
- Send logistics button.
- Close visit button.

Nếu một section chưa có dữ liệu:

```text
Chưa có dữ liệu cho phần này.
```

Nếu user không có permission section:

```text
Bạn không có quyền xem phần này.
```

hoặc không render section.

---

## 9. Host Operation Page — sửa trang hiện tại

File frontend hiện tại:

```text
VisitProcess.tsx
```

Yêu cầu sửa:

- Trang này chỉ dành cho Host chính.
- Không fallback permission về true.
- Nếu không có permission hợp lệ thì không render page.

Sai hiện tại cần tránh:

```ts
const canViewBefore = perm ? perm.canViewBeforeVisit : true;
const canViewDuring = perm ? perm.canViewDuringVisit : true;
const canViewAfter = perm ? perm.canViewAfterVisit : true;
```

Đổi thành:

```ts
const canViewBefore = !!perm?.canViewBeforeVisit;
const canViewDuring = !!perm?.canViewDuringVisit;
const canViewAfter = !!perm?.canViewAfterVisit;
```

Thêm state loading rõ ràng:

```ts
const [permLoading, setPermLoading] = useState(true);
```

Khi load permissions:

- Pending => loading.
- Success => render nếu `perm.canAccessHostProcess = true`.
- `403/404` => Access Denied.
- Other error => error state.

Không chỉ chặn Visitor.
Mọi role không phải Host chính đều bị chặn khỏi Host Operation Page.

Access Denied UI:

- Icon `Lock`.
- Title: `Không có quyền truy cập`.
- Text: `Bạn không có quyền xử lý quy trình tiếp khách của chuyến này.`
- Button: `Về danh sách`.

---

## 10. Backend — endpoint và Permission DTO

### 10.1. Host Operation Permission

Endpoint:

```http
GET /api/delegations/visit-process/{visitInstanceId}/permissions
```

Mục đích:

- Chỉ phục vụ Host Operation Page.

Nếu user không phải Host chính:

- Trả `403/404`.
- Không trả permission read-only cho role khác.

DTO đề xuất:

```csharp
public class VisitProcessPermissionDto
{
    public bool CanAccessHostProcess { get; set; }

    public int VisitRequestId { get; set; }
    public int VisitInstanceId { get; set; }
    public string InstanceStatus { get; set; } = default!;

    public bool CanViewBeforeVisit { get; set; }
    public bool CanEditBeforeVisit { get; set; }

    public bool CanViewDuringVisit { get; set; }
    public bool CanEditDuringVisit { get; set; }

    public bool CanViewAfterVisit { get; set; }
    public bool CanEditAfterVisit { get; set; }

    public bool CanStartVisit { get; set; }
    public bool CanCompleteVisit { get; set; }
    public bool CanCloseVisit { get; set; }

    public string Relation { get; set; } = "HOST";
}
```

Backend rule:

Allow nếu:

```text
role_code = STAFF
AND user_id = visit_request_campuses.current_host_user_id
AND instance không bị hard-deleted
AND user active
```

Deny nếu:

- Admin.
- Visitor.
- HO.
- Staff Leader không phải current host.
- IC Support.
- Department.
- Student.
- Staff không liên quan.

---

### 10.2. Process Summary Permission

Endpoint:

```http
GET /api/delegations/visit-instances/{visitInstanceId}/summary-permissions
```

hoặc gộp vào:

```http
GET /api/delegations/visit-instances/{visitInstanceId}/summary
```

DTO đề xuất:

```csharp
public class ProcessSummaryPermissionDto
{
    public bool CanViewSummaryPage { get; set; }

    public string Relation { get; set; } = default!;
    // HO_READONLY / STAFF_LEADER_READONLY / HOST_READONLY / OTHER_READONLY

    public bool CanViewRequestSummary { get; set; }
    public bool CanViewAgendaSummary { get; set; }
    public bool CanViewParticipantSummary { get; set; }
    public bool CanViewLogisticsSummary { get; set; }
    public bool CanViewMinutesSummary { get; set; }
    public bool CanViewMediaSummary { get; set; }
    public bool CanViewNewsSummary { get; set; }
    public bool CanViewFeedbackSummary { get; set; }
    public bool CanViewTimeline { get; set; }

    public bool IsReadOnly { get; set; } = true;
}
```

Summary allow rules:

#### Staff Leader cùng campus

```text
role_code = STAFF
AND sub_role = LEADER
AND users.primary_campus_id = visit_request_campuses.campus_id
```

#### HO

```text
role_code = HO
AND visit_requests.visit_scope = MULTI_CAMPUS
AND visit_requests.status = APPROVED
```

#### Host chính

- Có thể allow read-only summary nếu cần.

Deny:

- Visitor.
- Admin.
- Staff không liên quan.
- Staff Leader khác campus.
- HO với single-campus nếu không có rule riêng.
- Department/Student không relation.

---

### 10.3. Contribution Permission

Endpoint:

```http
GET /api/delegations/visit-instances/{visitInstanceId}/contribution
```

Có thể trả cả permission + data summary.

DTO đề xuất:

```csharp
public class ContributionPageDto
{
    public ContributionPermissionDto Permissions { get; set; } = default!;
    public VisitContributionSummaryDto Summary { get; set; } = default!;
    public MinutesContributionDto? Minutes { get; set; }
    public MediaContributionDto? Media { get; set; }
    public NewsContributionDto? News { get; set; }
}

public class ContributionPermissionDto
{
    public bool CanViewContributionPage { get; set; }

    public string Relation { get; set; } = default!;
    // HOST / IC_SUPPORT / DEPARTMENT_RELATED / STUDENT_RELATED

    public string? ParticipantRole { get; set; }
    public string? ParticipantStatus { get; set; }

    public bool CanViewRequestSummary { get; set; }
    public bool CanViewAgendaSummary { get; set; }
    public bool CanViewParticipantSummary { get; set; }

    public bool CanViewLogisticsSummary { get; set; }
    public bool CanViewRelatedLogisticsOnly { get; set; }
    public bool CanViewFullLogisticsSummary { get; set; }

    public bool CanViewMinutes { get; set; }
    public bool CanEditMinutes { get; set; }

    public bool CanViewMedia { get; set; }
    public bool CanUploadMedia { get; set; }

    public bool CanViewNews { get; set; }
    public bool CanCreateNews { get; set; }
    public bool CanEditNews { get; set; }

    public bool IsReadOnly { get; set; }
}
```

Contribution allow rules:

#### Host chính

```text
current_user.user_id = visit_request_campuses.current_host_user_id
```

#### Participant accepted/assigned

```sql
EXISTS visit_participants
WHERE visit_instance_id = target
  AND user_id = current user
  AND status IN ('ACCEPTED', 'ASSIGNED')
```

#### Department related

Allow nếu user có department task/logistics/participant relation hợp lệ với instance.

#### Student related

Allow nếu:

```sql
EXISTS visit_participants
WHERE user_id = current user
  AND status IN ('ACCEPTED', 'ASSIGNED')
```

Deny:

- Invited nhưng chưa accepted.
- Declined.
- Removed.
- Not related.
- Visitor.
- Admin.

---

### 10.4. Action endpoint vẫn phải tự validate

Các endpoint thao tác riêng phải tự check lại permission:

- Save minutes.
- Lock/unlock minutes.
- Upload media.
- Delete media.
- Create news.
- Edit news.
- Submit news for approval.

Không được dựa vào việc frontend đã gọi contribution permission.

Ví dụ:

- User có `canViewContributionPage` nhưng `canEditMinutes = false` mà gọi SaveMinutes => `403`.
- User có `canViewMedia` nhưng `canUploadMedia = false` mà gọi UploadMedia => `403`.
- User có `canEditNews` nhưng bài đã `APPROVED` => `409/403` theo convention.

---

## 11. Backend — cách suy luận quyền từ schema hiện có

Ưu tiên dùng schema hiện tại, không tự ý thêm bảng nếu chưa được yêu cầu.

Nguồn relation:

- `users`
- `roles/sub_role`
- `visit_requests`
- `visit_request_campuses`
- `visit_participants`
- `departments`
- logistics/task tables hiện có
- minutes tables hiện có
- news tables hiện có
- files/media tables hiện có

Không tạo dynamic permissions table.

### 11.1. Cách xác định người được vào Contribution

- Participant status `ACCEPTED/ASSIGNED`.
- Hoặc current Host.
- Hoặc department task relation.

### 11.2. Cách xác định quyền edit từng section

#### Minutes

- Host có thể edit theo phase nếu rule hiện tại cho phép.
- Người được giao viết biên bản có thể edit.
- Nếu chưa có assignment table, dùng rule hiện có của minutes lock/editor.
- Respect one-editor lock rule.
- `CLOSED/CANCELLED` => read-only.

#### Media

- Host có thể upload trong `AFTER_VISIT` nếu chưa closed.
- IC Support có thể upload nếu rule business cho phép hoặc được giao.
- Student/Department chỉ upload nếu được giao.
- `CLOSED/CANCELLED` => read-only.

#### News

- Host/IC Support/người được giao viết tin có thể tạo/sửa nếu chưa `APPROVED`.
- `APPROVED` => read-only.
- Nếu khách không đồng ý truyền thông hoặc `news_not_required = true` => không cho tạo news.
- `REJECTED` => người có quyền có thể sửa và gửi lại.

### 11.3. Nếu cần assignment rõ ràng về sau

Có thể đề xuất bảng `contribution_assignments`, nhưng không tự tạo trong task này nếu chưa được confirm.

Gợi ý:

```text
assignment_type: MINUTES / MEDIA / NEWS
visit_instance_id
assigned_user_id
assigned_by
status
```

---

## 12. Frontend — routing và điều hướng

### 12.1. Cập nhật route config

Thêm:

```text
/dashboard/visit/process-summary/:visitInstanceId
/dashboard/visit/contribution/:visitInstanceId
```

Giữ:

```text
/dashboard/visit/process/:visitInstanceId
/dashboard/visit/invitations/:participantId
/dashboard/visit/department-tasks/:participantId
/dashboard/visit/reception-detail/:id
```

---

### 12.2. Cập nhật VisitRequestManagement.tsx

Khi bấm nút xử lý/xem:

#### A. Nếu current user là Host chính

```ts
navigate(`/dashboard/visit/process/${row.visitInstanceId}`)
```

Điều kiện nên dựa vào backend DTO/relation:

```text
row.currentUserIsHost = true
```

hoặc:

```text
row.currentUserRelation = HOST
```

hoặc backend thêm action:

```text
OPEN_HOST_PROCESS
```

#### B. Nếu Staff Leader/HO chỉ xem

```ts
navigate(`/dashboard/visit/process-summary/${row.visitInstanceId}`)
```

#### C. Nếu participant đã ACCEPTED/ASSIGNED

```ts
navigate(`/dashboard/visit/contribution/${row.visitInstanceId}`)
```

#### D. Nếu activeTab = attending và invitation chưa accepted

```ts
navigate(`/dashboard/visit/invitations/${participantId}`)
```

#### E. Nếu Department Staff có task

```ts
navigate(`/dashboard/visit/department-tasks/${participantId}`)
```

#### F. Visitor

```ts
navigate(`/dashboard/visit/reception-detail/${idForRoute}`)
```

Không dùng:

- Role `STAFF` chung để quyết định route.
- `statusText`.
- Campus label.
- Frontend tự đoán quyền nếu backend không trả relation.

---

### 12.3. Đề xuất backend trả thêm allowedActions

Trong `VisitRequestManagementItem`, nên thêm hoặc chuẩn hóa:

```text
OPEN_HOST_PROCESS
OPEN_PROCESS_SUMMARY
OPEN_CONTRIBUTION
OPEN_INVITATION
OPEN_DEPARTMENT_TASK
VIEW_RECEPTION_DETAIL
```

Frontend chỉ render/navigate theo action này.

Ví dụ:

```ts
if (can('OPEN_HOST_PROCESS')) {
  navigate(`/dashboard/visit/process/${row.visitInstanceId}`);
} else if (can('OPEN_PROCESS_SUMMARY')) {
  navigate(`/dashboard/visit/process-summary/${row.visitInstanceId}`);
} else if (can('OPEN_CONTRIBUTION')) {
  navigate(`/dashboard/visit/contribution/${row.visitInstanceId}`);
} else if (can('OPEN_DEPARTMENT_TASK')) {
  navigate(`/dashboard/visit/department-tasks/${participantId}`);
} else if (can('OPEN_INVITATION')) {
  navigate(`/dashboard/visit/invitations/${participantId}`);
} else if (can('VIEW_RECEPTION_DETAIL')) {
  navigate(`/dashboard/visit/reception-detail/${idForRoute}`);
}
```

Backend phải build `allowedActions` theo relation thật.

---

## 13. Frontend — page/component cần tạo

### 13.1. VisitProcessSummaryPage.tsx

Responsibilities:

- Đọc `visitInstanceId` từ route.
- Gọi summary API.
- Nếu loading => skeleton.
- Nếu `403/404` => Access Denied.
- Render read-only sections theo permission.
- Không có nút thao tác.

Sections:

- `SummaryHeader`
- `RequestSummaryCard`
- `AgendaSummaryCard`
- `ParticipantSummaryCard`
- `LogisticsSummaryCard`
- `MinutesSummaryCard`
- `MediaSummaryCard`
- `NewsSummaryCard`
- `FeedbackSummaryCard`
- `TimelineSummaryCard`

---

### 13.2. VisitContributionPage.tsx

Responsibilities:

- Đọc `visitInstanceId`.
- Gọi contribution API.
- Nếu loading => skeleton.
- Nếu `403/404` => Access Denied.
- Render summary read-only.
- Render contribution workspace theo permission.

Sections:

- `ContributionHeader`
- `RequestSummaryCard`
- `AgendaSummaryCard`
- `ParticipantSummaryCard`
- `RelatedLogisticsSummaryCard`
- `MinutesContributionSection`
- `MediaContributionSection`
- `NewsContributionSection`

---

### 13.3. Shared components

Có thể reuse các component read-only hiện có:

- `RegistrantInfoReadOnly`
- `DelegationInfoReadOnly`
- Agenda summary UI
- Participant read-only UI
- Logistics summary UI

Không duplicate quá nhiều code.
Không làm UI quá dài: dùng accordion/card có collapse.

---

## 14. Phase rule cho Contribution Page

### ASSIGNED / BEFORE_VISIT

- Xem request summary.
- Xem agenda.
- Xem participant.
- Xem logistics liên quan.
- Chưa mở upload media sau tiếp khách.
- News có thể ẩn hoặc disabled tùy rule.
- Minutes có thể ẩn hoặc chỉ xem khung.

### DURING_VISIT

- Mở minutes nếu user có quyền.
- Vẫn chưa nên mở upload after-visit media nếu rule yêu cầu sau tiếp khách.
- Summary read-only.

### AFTER_VISIT

- Mở minutes hoàn thiện.
- Mở media upload.
- Mở news draft nếu có quyền.
- Hiển thị checklist kết quả.

### CLOSED

- Toàn bộ read-only.

### CANCELLED

- Chỉ xem dữ liệu đã có trước khi hủy nếu được phép.
- Không có thao tác.

---

## 15. Security rule bắt buộc

Backend phải chặn:

- User không liên quan đoán `visitInstanceId`.
- Staff cùng campus nhưng không phải Host/participant/leader hợp lệ.
- Participant chưa `ACCEPTED`.
- Participant `DECLINED/REMOVED`.
- Visitor vào route nội bộ.
- Admin vào route nghiệp vụ.
- User gọi API edit section không có quyền.
- User sửa news đã `APPROVED`.
- User upload media khi instance `CLOSED/CANCELLED`.
- User edit minutes khi lock thuộc người khác.

Frontend phải:

- Không fallback permission về true.
- Không render page khi permission chưa load xong.
- Không dùng `statusText` để gate action.
- Không tự suy luận quyền từ label.
- Không chỉ ẩn nút mà vẫn để API mở.

---

## 16. Test case bắt buộc

### Case 1. Host chính

- Mở `/process/:visitInstanceId`.
- Vào được.
- Thấy nút thao tác đúng phase.
- `dotnet build` pass.
- `npm run build` pass.

### Case 2. IC Staff cùng campus nhưng không phải Host, không phải participant

- Mở `/process/:visitInstanceId` => `403/Access Denied`.
- Mở `/contribution/:visitInstanceId` => `403`.
- Mở `/summary/:visitInstanceId` => `403` nếu không phải Leader.

### Case 3. Staff Leader cùng campus

- Mở `/process/:visitInstanceId` => `403/Access Denied`.
- Mở `/process-summary/:visitInstanceId` => vào được read-only.
- Không thấy nút lưu/sửa/chuyển phase.

### Case 4. Staff Leader khác campus

- Mở summary/process/contribution => `403`.

### Case 5. HO với multi-campus approved

- Mở `/process/:visitInstanceId` => `403`.
- Mở `/process-summary/:visitInstanceId` => vào được read-only.
- Thấy tiến trình/kết quả campus.

### Case 6. HO với single-campus

- Không được xử lý.
- Nếu có rule cho xem single-campus read-only thì chỉ xem ở list/detail phù hợp.
- Không vào Host process.

### Case 7. IC Support được mời nhưng chưa ACCEPTED

- Vào `/invitations/:participantId` => xem lời mời.
- Vào `/contribution/:visitInstanceId` => `403`.

### Case 8. IC Support đã ACCEPTED

- Vào `/process` => `403`.
- Vào `/contribution` => vào được.
- Thấy request summary, agenda, participant, logistics summary phù hợp.
- Chỉ thao tác minutes/media/news nếu backend trả quyền.

### Case 9. Student đã ACCEPTED nhưng không được giao media/news/minutes

- Vào contribution được.
- Thấy summary cơ bản.
- Các section contribution read-only hoặc ẩn action.

### Case 10. Student được giao upload media

- Vào contribution.
- Thấy MediaSection có nút upload trong `AFTER_VISIT`.
- Không sửa minutes/news nếu không có quyền.

### Case 11. Staff được giao viết biên bản

- Vào contribution.
- Edit MinutesSection theo lock rule.
- Không upload media/news nếu không có quyền.

### Case 12. Staff được giao viết tin

- Tạo/sửa NewsSection nếu bài chưa `APPROVED`.
- `APPROVED` thì read-only.
- `REJECTED` thì sửa/gửi lại nếu rule cho phép.

### Case 13. Department Staff có task

- Vào `department-tasks/:participantId`.
- Xử lý task được giao.
- Không vào Host process.
- Contribution chỉ vào nếu accepted/assigned participant relation.

### Case 14. Visitor

- Vào `reception-detail` được nếu đúng owner.
- Vào process/summary/contribution/department-tasks => `403/Access Denied`.

### Case 15. Admin

- Vào các route nghiệp vụ visit => `403` hoặc màn không tham gia luồng tiếp khách.

### Case 16. CLOSED instance

- Process chỉ Host xem read-only nếu cần.
- Summary read-only.
- Contribution read-only.
- Không upload/edit/chuyển phase.

### Case 17. CANCELLED instance

- Summary/contribution chỉ hiện dữ liệu đã có nếu user có quyền.
- Không có thao tác.

### Case 18. Direct API security

- Gọi save minutes khi không có `canEditMinutes` => `403`.
- Gọi upload media khi không có `canUploadMedia` => `403`.
- Gọi edit news đã `APPROVED` => `409/403`.
- Gọi get contribution bằng user không relation => `403/404`.

---

## 17. Output sau khi triển khai

Sau khi sửa, báo cáo lại:

### Backend

- Các endpoint đã thêm/sửa.
- DTO đã thêm/sửa.
- Handler/Query/Command đã thêm/sửa.
- Rule permission cuối cùng.
- Các action endpoint đã validate lại quyền.

### Frontend

- Route mới.
- Page mới.
- Component mới.
- `VisitProcess.tsx` đã bỏ fallback permission true.
- `VisitRequestManagement.tsx` đã cập nhật điều hướng theo `allowedActions/relation`.
- Access Denied state.

### UI

- Summary Page hiển thị gì.
- Contribution Page hiển thị gì.
- Host Process Page còn lại cho ai.

### Test

- Kết quả từng test case.
- `npm run build`.
- `dotnet build`.

---

## 18. Chốt nghiệp vụ cuối cùng

```text
Host xử lý.
Leader/HO giám sát.
Participant đóng góp.
Department xử lý task.
Visitor xem public-safe.
Admin không tham gia.
```

Không role nào được xem quy trình nội bộ chỉ vì có role `STAFF`.
Mọi quyền phải dựa trên relation thật với `visitInstanceId`.
