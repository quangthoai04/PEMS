# PROMPT NGẮN — NHIỆM VỤ TIẾP KHÁCH DEPARTMENT LEADER: LỊCH, THƯ MỜI, ĐƠN YÊU CẦU, PHÂN CÔNG

## 0. Bối cảnh

Tôi đang code trang **Nhiệm vụ tiếp khách** cho role **Department Leader** trong PEMS.

UI đã có sẵn:

```text
- Tab Bảng lịch
- Tab Phân công
- Tab Theo dõi tiến độ đoàn khách
- Calendar tháng như ảnh
- Item lịch: Thư mời / Yêu cầu
- Modal chi tiết thư mời
- Modal chi tiết đơn yêu cầu / nhiệm vụ được giao
- Button Xem chi tiết đoàn đón khách
- Button Từ chối
- Button Xác nhận tham gia
- Button Ủy quyền / Đổi người phụ trách
- Với đơn yêu cầu có thêm phần Đề xuất thay đổi
- Modal thêm lịch cá nhân
```

Yêu cầu: **nối chức năng thật với database**, không dùng mock data, không rewrite UI.

Stack:

```text
Frontend: React + TypeScript + Tailwind CSS
Backend: .NET 8 Clean Architecture + MediatR + EF Core
Database: MySQL v8.4 refined v6 v10 no dynamic permissions
```

---

## 1. Nguyên tắc bắt buộc

```text
KHÔNG rewrite UI.
KHÔNG dùng mock data.
KHÔNG tạo bảng mới nếu DB hiện tại đã đủ.
KHÔNG query permissions/role_permissions vì DB mới đã bỏ dynamic permission.
KHÔNG đọc inbox/mail thật.
KHÔNG chuyển nhiệm vụ logistics nếu DB/business v10 đang chặn transfer sau khi đã assigned.
Chỉ code đúng theo schema hiện tại.
```

Chỉ sửa file liên quan:

```text
- Department reception tasks page
- Calendar tab component
- Assignment tab component
- Progress tab component
- Invitation/detail modal
- Logistics request/detail modal
- Personal calendar modal
- API service/hooks/types liên quan reception tasks
- Backend controller/queries/commands liên quan calendar, visit participants, logistics items
```

---

## 2. Đối chiếu database — hiện tại đủ dùng

Dùng các bảng chính:

```text
visit_requests
visit_request_campuses
visit_participants
visit_agendas
visit_logistics_items
calendar_events
calendar_event_attendees
notifications
sent_emails
email_action_tokens nếu đã dùng email button action
users
departments
campuses
```

Không cần thêm bảng mới cho các chức năng trong prompt này.

Mapping nghiệp vụ:

```text
Thư mời tham gia
→ visit_participants
→ status: INVITED / ACCEPTED / DECLINED / ASSIGNED / REMOVED

Đơn yêu cầu / nhiệm vụ hỗ trợ phòng ban
→ visit_logistics_items
→ status: PLANNED / REQUESTED / CHANGE_PROPOSED / RECEIVED / ASSIGNED / ACCEPTED / IN_PROGRESS / READY / DONE / REJECTED / CANCELLED

Lịch hiển thị trên calendar
→ calendar_events
→ event_type/source_type/related_type/related_id nếu entity hiện tại có
→ hoặc build từ visit_participants + visit_logistics_items rồi trả DTO calendar item

Lịch cá nhân
→ calendar_events
→ owner_user_id = currentUser.userId nếu schema/entity có
→ visibility = PRIVATE
→ status = ACTIVE
```

---

## 3. Tab Bảng lịch — lấy dữ liệu thật

Calendar phải hiển thị 2 loại item chính:

```text
1. Thư mời
2. Đơn yêu cầu
```

Ngoài ra có thể hiển thị lịch cá nhân nếu user tạo.

Không dùng mock.

API gợi ý:

```text
GET /api/department/reception-tasks/calendar?month=2026-08
```

Response item gợi ý:

```ts
type DepartmentCalendarItemDto = {
  id: number;
  itemType: 'INVITATION' | 'REQUEST' | 'PERSONAL';
  title: string;
  fullTitle: string;
  date: string;
  startAt: string;
  endAt: string;
  status: string;
  visitInstanceId?: number | null;
  visitRequestId?: number | null;
  logisticsItemId?: number | null;
  participantId?: number | null;
};
```

Scope bắt buộc:

```text
Department Leader chỉ thấy:
- Thư mời / participant liên quan department hoặc chính user/phòng ban của mình.
- Đơn yêu cầu có visit_logistics_items.requested_to_department_id = currentUser.departmentId.
- Lịch cá nhân do currentUser tạo.
```

UI:

```text
- Item dài thì truncate trên calendar.
- Khi hover item, hiện tooltip/popup tên đầy đủ: fullTitle.
- Click item mở modal detail đúng loại.
```

---

## 4. Chi tiết thư mời

Khi click **Thư mời**, mở modal UI hiện tại.

Dữ liệu lấy DB thật từ:

```text
visit_participants
visit_request_campuses
visit_requests
visit_agendas nếu cần
users
```

Hiển thị:

```text
Người gửi
Thời gian gửi / invited_at
Đoàn khách / organization
Thời gian diễn ra
Nội dung thư mời / note
Danh sách người được mời nếu có
Button Xem chi tiết đoàn đón khách
Button Từ chối
Button Xác nhận tham gia
Button Ủy quyền / Đổi người phụ trách nếu UI đang có
```

API detail gợi ý:

```text
GET /api/department/reception-tasks/invitations/{participantId}
```

### Xác nhận tham gia

```text
POST /api/department/reception-tasks/invitations/{participantId}/accept
```

Logic:

```text
- Check current user/department scope.
- Chỉ cho ACCEPT nếu status = INVITED.
- Update visit_participants.status = ACCEPTED.
- responded_at = now.
- updated_by = currentUser.userId.
- Tạo notification/email nếu project đã có flow.
```

Toast:

```text
“Đã xác nhận tham gia thành công.”
```

### Từ chối

```text
POST /api/department/reception-tasks/invitations/{participantId}/decline
```

Request:

```ts
{
  reason: string;
}
```

Logic:

```text
- Bắt buộc nhập lý do.
- reason trim, không rỗng, max 500 hoặc theo rule hiện tại.
- Update visit_participants.status = DECLINED.
- note = reason.
- responded_at = now.
- updated_by = currentUser.userId.
```

Toast:

```text
“Đã từ chối thư mời.”
```

---

## 5. Chi tiết đơn yêu cầu / nhiệm vụ được giao

Khi click **Đơn yêu cầu**, mở modal UI hiện tại.

Dữ liệu lấy DB thật từ:

```text
visit_logistics_items
visit_request_campuses
visit_requests
users
departments
```

API detail gợi ý:

```text
GET /api/department/reception-tasks/requests/{logisticsItemId}
```

Hiển thị:

```text
Người gửi / requested_by
Thời gian gửi / requested_at
Thời gian sử dụng / usage_start_at - usage_end_at
Nội dung chi tiết công việc / description
Người phụ trách hiện tại / assigned_to_user_id
Trạng thái
Button Xem chi tiết đoàn đón khách
Button Từ chối
Button Xác nhận
Button Đề xuất thay đổi
Button Ủy quyền / Đổi người phụ trách nếu đang chưa gán hoặc business cho phép
```

---

## 6. Xác nhận đơn yêu cầu

API gợi ý:

```text
POST /api/department/reception-tasks/requests/{logisticsItemId}/confirm
```

Logic:

```text
- Check logistics item thuộc currentUser.departmentId.
- Nếu status = REQUESTED thì chuyển RECEIVED.
- received_by = currentUser.userId.
- received_at = now.
- Nếu đã có assigned_to_user_id thì có thể chuyển ASSIGNED hoặc giữ theo flow hiện tại.
```

Toast:

```text
“Đã xác nhận đơn yêu cầu thành công.”
```

---

## 7. Từ chối đơn yêu cầu

API gợi ý:

```text
POST /api/department/reception-tasks/requests/{logisticsItemId}/reject
```

Request:

```ts
{
  reason: string;
}
```

Logic:

```text
- Bắt buộc nhập lý do.
- Check item thuộc current department.
- Chỉ cho reject khi status còn REQUESTED / RECEIVED / CHANGE_PROPOSED theo rule hiện tại.
- status = REJECTED.
- decision_note hoặc assignee_response_note = reason, dùng field hiện có trong entity/schema.
- updated_by = currentUser.userId.
```

Toast:

```text
“Đã từ chối đơn yêu cầu.”
```

---

## 8. Đề xuất thay đổi cho đơn yêu cầu

Trong detail **Đơn yêu cầu** có nút:

```text
Đề xuất thay đổi
```

API gợi ý:

```text
POST /api/department/reception-tasks/requests/{logisticsItemId}/propose-change
```

Request tối giản:

```ts
{
  proposedUsageStartAt?: string | null;
  proposedUsageEndAt?: string | null;
  proposedDescription: string;
}
```

Logic:

```text
- Bắt buộc nhập nội dung đề xuất.
- Nếu có thời gian đề xuất thì validate end > start.
- Update các field proposed_* hiện có của visit_logistics_items.
- proposed_by = currentUser.userId.
- proposed_at = now.
- status = CHANGE_PROPOSED.
```

Toast:

```text
“Đã gửi đề xuất thay đổi.”
```

Không tạo bảng mới.

---

## 9. Ủy quyền / Gán người phụ trách

Button hiện UI là:

```text
Ủy quyền / Đổi người phụ trách
```

Nhưng theo schema/business v10, **không hỗ trợ chuyển nhiệm vụ logistics sau khi đã phân công**. Vì vậy code theo rule an toàn:

### 9.1. Với đơn yêu cầu logistics

Chỉ cho gán người phụ trách nếu:

```text
assigned_to_user_id IS NULL
status IN ('REQUESTED','RECEIVED','CHANGE_PROPOSED')
```

Khi gán:

```text
- List user trong phòng ban:
  users.department_id = currentUser.departmentId
  role_code = DEPARTMENT
  users.status = ACTIVE
- assigned_to_user_id = selectedUserId
- assigned_by = currentUser.userId
- assigned_at = now
- status = ASSIGNED
```

Nếu đã có `assigned_to_user_id`:

```text
- Không đổi người phụ trách.
- Disable button hoặc backend trả 409 Conflict:
  “Nhiệm vụ đã được phân công, không hỗ trợ đổi người phụ trách.”
```

API:

```text
POST /api/department/reception-tasks/requests/{logisticsItemId}/assign
```

Request:

```ts
{
  assigneeUserId: number;
}
```

Toast:

```text
Success: “Đã phân công người phụ trách thành công.”
Conflict: “Nhiệm vụ đã được phân công, không hỗ trợ đổi người phụ trách.”
```

### 9.2. Với thư mời

Nếu UI cho ủy quyền thư mời sang nhân sự khác trong phòng ban, chỉ làm nếu backend hiện tại có rule rõ ràng. Nếu chưa có, không tự chuyển `visit_participants.user_id` bừa bãi.

Có thể tạo command riêng:

```text
DelegateInvitationCommand
```

Nhưng phải check:

```text
- old participant thuộc current user/department.
- new user cùng department, ACTIVE, role DEPARTMENT.
- invitation chưa ACCEPTED/DECLINED.
- Ghi note/audit.
```

Nếu chưa rõ business, để button disabled và báo cần xác nhận rule.

---

## 10. Xem chi tiết đoàn đón khách

Button:

```text
Xem chi tiết đoàn đón khách
```

Hành vi:

```text
- Điều hướng tới route detail đoàn thật đang có.
- Không tạo detail giả.
- Dùng visitRequestId hoặc visitInstanceId từ DTO.
```

Nếu mở accordion trong modal thì lấy dữ liệu thật từ:

```text
visit_requests
visit_request_campuses
visit_guest_members
visit_agendas
visit_logistics_items
visit_participants
```

Hiển thị đủ 4 nhóm nếu đã có UI:

```text
1. Thông tin người tạo
2. Thông tin đoàn khách
3. Setup
4. Detail setup
```

---

## 11. Thêm lịch cá nhân — form tối giản

Modal **Lên Lịch Công Tác** không cần nhiều field như UI mẫu. Chỉ cần:

```text
Tiêu đề *
Nội dung
Thời gian trong ngày đó *
```

Không cần:

```text
Đơn vị FPTU chủ trì / host
Phân loại phức tạp
Chi tiết phái đoàn
Logistics checklist
```

API gợi ý:

```text
POST /api/department/reception-tasks/personal-events
```

Request:

```ts
{
  title: string;
  description?: string | null;
  date: string;       // ngày đang chọn trên calendar
  startTime: string;  // HH:mm
  endTime: string;    // HH:mm
}
```

Logic lưu DB:

```text
calendar_events.title = title
calendar_events.description = description
calendar_events.start_at = date + startTime
calendar_events.end_at = date + endTime
calendar_events.timezone = 'Asia/Ho_Chi_Minh'
calendar_events.visibility = PRIVATE
calendar_events.status = ACTIVE
calendar_events.owner_user_id = currentUser.userId nếu schema/entity có
calendar_events.created_by = currentUser.userId
```

Validate:

```text
- title bắt buộc, max 255.
- startTime/endTime bắt buộc.
- endAt > startAt.
- Chỉ tạo lịch cá nhân cho currentUser.
```

Toast:

```text
“Đã thêm lịch cá nhân thành công.”
```

Sau success:

```text
- Đóng modal.
- Reset form.
- Refetch calendar.
```

---

## 12. Frontend behavior

```text
- Calendar lấy API thật theo month.
- Hover item hiện tooltip fullTitle.
- Click item mở detail theo itemType.
- Mọi action accept/decline/confirm/reject/propose/assign thành công đều refetch calendar + assignment/progress list.
- Không refresh full page.
- Có loading/empty/error state.
- Toast success/error rõ ràng.
- Không để modal mất dữ liệu khi API lỗi.
```

---

## 13. Backend structure gợi ý

Dùng cấu trúc hiện tại, không tạo lung tung.

```text
PEMS.Application/DepartmentReceptionTasks/Queries/GetDepartmentCalendar
PEMS.Application/DepartmentReceptionTasks/Queries/GetInvitationDetail
PEMS.Application/DepartmentReceptionTasks/Queries/GetRequestDetail
PEMS.Application/DepartmentReceptionTasks/Commands/AcceptInvitation
PEMS.Application/DepartmentReceptionTasks/Commands/DeclineInvitation
PEMS.Application/DepartmentReceptionTasks/Commands/ConfirmRequest
PEMS.Application/DepartmentReceptionTasks/Commands/RejectRequest
PEMS.Application/DepartmentReceptionTasks/Commands/ProposeRequestChange
PEMS.Application/DepartmentReceptionTasks/Commands/AssignRequestAssignee
PEMS.Application/DepartmentReceptionTasks/Commands/CreatePersonalEvent
```

Controller:

```text
DepartmentReceptionTasksController
```

Controller chỉ gọi MediatR.

---

## 14. Checklist nghiệm thu

```text
[ ] Calendar không còn mock.
[ ] Calendar hiển thị đúng Thư mời và Đơn yêu cầu từ DB.
[ ] Hover item hiện tên đầy đủ.
[ ] Click Thư mời mở detail DB thật.
[ ] Click Đơn yêu cầu mở detail DB thật.
[ ] Xác nhận thư mời update visit_participants.status = ACCEPTED.
[ ] Từ chối thư mời bắt nhập lý do và update DECLINED.
[ ] Xác nhận đơn yêu cầu update đúng visit_logistics_items.
[ ] Từ chối đơn yêu cầu bắt nhập lý do và update REJECTED.
[ ] Đề xuất thay đổi update proposed_* và status CHANGE_PROPOSED.
[ ] Gán người phụ trách list user cùng department.
[ ] Gán người phụ trách update assigned_to_user_id nếu chưa có assignee.
[ ] Nếu task đã assigned thì không cho đổi người phụ trách theo v10.
[ ] Xem chi tiết đoàn dùng visitRequestId/visitInstanceId thật.
[ ] Thêm lịch cá nhân chỉ có tiêu đề, nội dung, thời gian trong ngày.
[ ] Lịch cá nhân lưu vào calendar_events.
[ ] Không dùng mock data.
[ ] Không query permissions/role_permissions.
[ ] dotnet build pass.
[ ] npm run build pass.
```

---

## 15. Output mong muốn

Báo cáo ngắn:

```text
Đã làm:
- Calendar lấy DB thật.
- Hiển thị Thư mời / Đơn yêu cầu.
- Tooltip full title khi hover.
- Detail modal lấy DB thật.
- Accept/Decline invitation.
- Confirm/Reject request.
- Propose request change.
- Assign assignee trong department.
- Personal event tối giản.

DB:
- Không cần đổi schema.
- Lưu ý: logistics reassignment sau khi đã assigned bị chặn theo v10.

Files changed:
- ...

Build:
- Backend: pass/fail
- Frontend: pass/fail
```
