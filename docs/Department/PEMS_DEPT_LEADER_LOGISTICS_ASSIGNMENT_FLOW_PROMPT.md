# PROMPT CODE — Department Leader Logistics Assignment Flow theo Base SQL mới

## 0. Bối cảnh

Tôi đang code tiếp module **Department Leader — Nhiệm vụ tiếp khách** trong PEMS.

Tôi đã dùng base SQL mới có thêm bảng:

```text
visit_logistics_assignment_attempts
```

Bảng này dùng để lưu từng lần **Department Leader phân công đơn yêu cầu logistics cho nhân sự phòng ban**.

Mục tiêu nghiệp vụ:

```text
- Department Leader nhận đơn yêu cầu gửi tới phòng ban.
- Department Leader phân công cho nhân viên trong phòng ban hoặc tự nhận.
- Nếu nhân viên được phân công từ chối và nhập lý do, Department Leader được phân công lại cho người khác.
- Không cho đổi người phụ trách khi nhiệm vụ đã được nhận / đang xử lý / hoàn thành / đã có ký biên bản.
```

UI hiện tại đã đẹp rồi. Chỉ sửa logic, API, state, mapping trạng thái. **Không thay đổi UI layout.**

---

## 1. Nguyên tắc bắt buộc

```text
KHÔNG thay đổi UI layout hiện tại.
KHÔNG rewrite UI.
KHÔNG đổi style, className, màu sắc, bố cục nếu không cần.
KHÔNG xóa modal/action/state đã có.
KHÔNG tạo mock data.
KHÔNG tạo file rác.
KHÔNG hard-code data.
KHÔNG tạo bảng mới.
KHÔNG sửa schema nữa.
KHÔNG query permissions / role_permissions.
Dữ liệu phải lấy thật từ database.
Code clean, đúng Clean Architecture.
Controller chỉ gọi MediatR, không chứa business logic.
Backend build pass.
Frontend build pass.
```

---

## 2. Role và scope

User hiện tại là:

```text
role_code = DEPARTMENT
sub_role = LEADER
```

Department Leader chỉ được thao tác dữ liệu trong phòng ban của mình:

```text
currentUser.department_id = visit_logistics_items.requested_to_department_id
```

Nhân sự được phân công phải thuộc cùng phòng ban:

```text
users.department_id = currentUser.department_id
role_code = DEPARTMENT
users.status = ACTIVE
```

Nếu cho leader tự nhận thì cho phép:

```text
assigneeUserId = currentUser.userId
```

---

## 3. Bảng database liên quan

Dùng các bảng:

```text
visit_logistics_items
visit_logistics_assignment_attempts
visit_logistics_item_handovers
users
departments
visit_requests
visit_request_campuses
notifications
sent_emails / email_action_tokens nếu có gửi email action
```

Không thêm bảng mới.

---

## 4. Mapping trạng thái đơn yêu cầu logistics

Bảng chính lưu trạng thái hiện tại:

```text
visit_logistics_items
```

Bảng lưu lịch sử từng lần giao:

```text
visit_logistics_assignment_attempts
```

Mapping UI:

```text
Chưa có người chịu trách nhiệm:
- visit_logistics_items.status IN ('REQUESTED','RECEIVED','CHANGE_PROPOSED')
- assigned_to_user_id IS NULL

Đã giao:
- visit_logistics_items.status = 'ASSIGNED'
- assigned_to_user_id IS NOT NULL
- latest assignment attempt status = 'PENDING'

Từ chối bởi nhân viên:
- latest assignment attempt status = 'DECLINED'
- item quay về status = 'RECEIVED'
- assigned_to_user_id = NULL

Đã chấp nhận:
- visit_logistics_items.status = 'ACCEPTED'
- latest assignment attempt status = 'ACCEPTED'

Đang xử lý:
- visit_logistics_items.status = 'IN_PROGRESS'

Hoàn thành:
- visit_logistics_items.status = 'DONE'

Từ chối toàn bộ đơn yêu cầu:
- visit_logistics_items.status = 'REJECTED'
```

Quan trọng:

```text
Nhân viên từ chối nhiệm vụ KHÔNG được set visit_logistics_items.status = REJECTED.
REJECTED chỉ dùng khi Department Leader / bên xử lý từ chối toàn bộ đơn yêu cầu.
```

---

## 5. Phân công người phụ trách

Action: Department Leader chọn nhân sự trong phòng ban và phân công.

API gợi ý:

```text
POST /api/department/reception-tasks/requests/{logisticsItemId}/assign
```

Request:

```ts
{
  assigneeUserId: number
}
```

Backend logic:

```text
1. Check current user là Department Leader.
2. Check logistics item tồn tại.
3. Check item.requested_to_department_id = currentUser.department_id.
4. Check item.status IN ('REQUESTED','RECEIVED','CHANGE_PROPOSED').
5. Check item.assigned_to_user_id IS NULL.
6. Check assignee thuộc cùng department, role DEPARTMENT, status ACTIVE.
7. Insert visit_logistics_assignment_attempts:
   - logistics_item_id
   - assignee_user_id
   - assigned_by = currentUser.userId
   - assigned_at = NOW()
   - status = PENDING
8. Update visit_logistics_items:
   - assigned_to_user_id = assigneeUserId
   - assigned_by = currentUser.userId
   - assigned_at = NOW()
   - status = ASSIGNED
   - updated_by = currentUser.userId
   - updated_at = NOW()
9. Gửi notification/email nếu project đã có service.
```

Không cho assign nếu:

```text
status IN ('ACCEPTED','IN_PROGRESS','READY','DONE','CANCELLED','REJECTED')
```

Nếu đã có người phụ trách và chưa bị từ chối:

```text
Trả 409 Conflict:
“Nhiệm vụ đã được phân công và đang chờ phản hồi hoặc đã được nhận.”
```

Toast success:

```text
Đã phân công người phụ trách thành công.
```

---

## 6. Nhân viên chấp nhận nhiệm vụ

API gợi ý:

```text
POST /api/department/reception-tasks/requests/{logisticsItemId}/accept-assignment
```

Backend logic:

```text
1. Check current user = visit_logistics_items.assigned_to_user_id.
2. Check item.status = ASSIGNED.
3. Lấy latest visit_logistics_assignment_attempts của item:
   - logistics_item_id = item.id
   - assignee_user_id = currentUser.userId
   - status = PENDING
4. Update attempt:
   - status = ACCEPTED
   - responded_at = NOW()
   - response_source = PORTAL hoặc EMAIL_TOKEN
5. Update visit_logistics_items:
   - status = ACCEPTED
   - assignee_accepted_at = NOW()
   - assignee_response_note = NULL hoặc note nếu có
   - updated_by = currentUser.userId
   - updated_at = NOW()
```

Toast frontend:

```text
Đã xác nhận nhiệm vụ thành công.
```

---

## 7. Nhân viên từ chối nhiệm vụ

API gợi ý:

```text
POST /api/department/reception-tasks/requests/{logisticsItemId}/decline-assignment
```

Request:

```ts
{
  reason: string
}
```

Frontend validate:

```text
reason bắt buộc
trim không rỗng
max 500 hoặc 1000 ký tự theo convention hiện tại
```

Backend logic:

```text
1. Check current user = assigned_to_user_id.
2. Check item.status = ASSIGNED.
3. Reason bắt buộc, trim không rỗng.
4. Lấy latest assignment attempt status = PENDING.
5. Update attempt:
   - status = DECLINED
   - responded_at = NOW()
   - response_note = reason
   - response_source = PORTAL hoặc EMAIL_TOKEN
6. Update visit_logistics_items:
   - status = RECEIVED
   - assigned_to_user_id = NULL
   - assigned_by = NULL
   - assigned_at = NULL
   - assignee_response_note = reason
   - updated_by = currentUser.userId
   - updated_at = NOW()
7. Gửi notification cho Department Leader:
   “Nhân viên {name} đã từ chối nhiệm vụ. Vui lòng phân công người khác.”
```

Toast frontend:

```text
Đã từ chối nhiệm vụ.
```

Sau khi từ chối, Department Leader phải thấy lại nút phân công để chọn người khác.

---

## 8. Phân công lại sau khi bị từ chối

Cho phép phân công lại khi:

```text
visit_logistics_items.status = RECEIVED
assigned_to_user_id IS NULL
latest assignment attempt status = DECLINED
```

Khi phân công lại:

```text
- Không sửa attempt cũ.
- Insert attempt mới status = PENDING.
- Update lại assigned_to_user_id, assigned_by, assigned_at.
- status = ASSIGNED.
```

UI detail nên hiển thị lịch sử phân công từ `visit_logistics_assignment_attempts`:

```text
Nguyễn Văn A — Đã từ chối — 25/06/2026 10:20
Lý do: Bận lịch họp

Nguyễn Văn B — Đang chờ phản hồi — 25/06/2026 11:00
```

Không làm lịch sử bằng mock.

---

## 9. Không cho đổi người sau khi đã nhận

Không cho đổi người phụ trách nếu:

```text
status IN ('ACCEPTED','IN_PROGRESS','READY','DONE','CANCELLED','REJECTED')
```

Cũng không cho đổi nếu đã có ký biên bản trong:

```text
visit_logistics_item_handovers
```

Check:

```text
Có handover row BORROW/RETURN mà borrower_signed_at hoặc provider_signed_at khác NULL
```

Nếu có, trả lỗi:

```text
“Nhiệm vụ đã được xử lý hoặc đã có ký biên bản, không thể đổi người phụ trách.”
```

---

## 10. Đề xuất thay đổi đơn yêu cầu

Giữ logic hiện tại nếu đã có UI.

API gợi ý:

```text
POST /api/department/reception-tasks/requests/{logisticsItemId}/propose-change
```

Logic:

```text
- Chỉ Department Leader / người được phân công hợp lệ mới được đề xuất.
- Bắt buộc có nội dung đề xuất.
- Nếu có proposed start/end thì validate end > start.
- Update proposed_* trong visit_logistics_items.
- status = CHANGE_PROPOSED.
- proposed_by = currentUser.userId.
- proposed_at = NOW().
```

Khi bên yêu cầu phản hồi:

```text
proposal_response = ACCEPTED / REJECTED
proposal_responded_by
proposal_responded_at
proposal_response_note
```

Nếu proposal bị reject và business muốn từ chối đơn:

```text
status = REJECTED
decision_note = proposal_response_note
```

---

## 11. Biên bản bàn giao / nghiệm thu

Không lưu chữ ký trong `visit_logistics_items`.

Dùng bảng:

```text
visit_logistics_item_handovers
```

Mapping:

```text
BORROW:
- borrower_signed_by / borrower_signed_at = bên mượn ký nhận
- provider_signed_by / provider_signed_at = bên cho mượn ký bàn giao

RETURN:
- borrower_signed_by / borrower_signed_at = bên mượn ký trả
- provider_signed_by / provider_signed_at = bên cho mượn ký nhận lại
```

Đơn yêu cầu chỉ được DONE khi có RETURN và đủ 2 chữ ký:

```text
handover_type = RETURN
borrower_signed_at IS NOT NULL
provider_signed_at IS NOT NULL
```

---

## 12. Email action token nếu có

Nếu gửi mail cho nhân viên để accept/decline assignment, phải tạo token theo assignment attempt, không theo logistics item chung chung.

Dùng key dạng:

```text
LOGISTICS_ASSIGNMENT:{assignment_attempt_id}:{assignee_email}
```

Không dùng:

```text
LOGISTICS_ITEM:{logistics_item_id}:{assignee_email}
```

Vì một logistics item có thể được phân công nhiều lần sau khi người trước từ chối.

Token action:

```text
ACCEPT
DECLINE
```

Khi bấm email token:

```text
- Validate token hash.
- Check expires_at.
- Check used_at.
- Check target assignment_attempt_id còn PENDING.
- Nếu đã phản hồi rồi, trả ALREADY_RESPONDED.
- Update attempt + visit_logistics_items giống portal action.
```

---

## 13. Frontend cần sửa

Không đổi UI layout.

Chỉ sửa:

```text
- API service
- hooks
- DTO types
- action handlers
- status mapping
- visibility/disabled condition của button
- refetch sau action
```

Tab Bảng lịch:

```text
- Lấy data thật từ API.
- Không mock.
- Hover item giữ tooltip full title nếu đã có.
- Click item mở modal hiện tại.
```

Tab Phân công:

```text
- Lấy list thật từ visit_logistics_items.
- Status lấy theo mapping mới.
- Nút “Đổi người phụ trách/Phân công” chỉ hiện khi assigned_to_user_id IS NULL và status cho phép.
- Nếu latest attempt DECLINED thì hiện trạng thái “Chờ phân công lại” hoặc “Chưa có người chịu trách nhiệm”.
```

Modal detail đơn yêu cầu:

```text
- Hiển thị người phụ trách hiện tại nếu có.
- Hiển thị lịch sử phân công từ visit_logistics_assignment_attempts.
- Nếu nhân viên từ chối, hiển thị lý do từ chối.
- Action thành công thì refetch detail + calendar + assignment list.
```

Không reload full page.

---

## 14. Backend structure gợi ý

Tạo/sửa đúng feature hiện có, không tạo lung tung.

Gợi ý:

```text
PEMS.Application/DepartmentReceptionTasks/Commands/AssignRequestAssignee
PEMS.Application/DepartmentReceptionTasks/Commands/AcceptAssignedLogisticsTask
PEMS.Application/DepartmentReceptionTasks/Commands/DeclineAssignedLogisticsTask
PEMS.Application/DepartmentReceptionTasks/Queries/GetRequestDetail
PEMS.Application/DepartmentReceptionTasks/Queries/GetAssignmentHistory
PEMS.Application/DepartmentReceptionTasks/Queries/GetDepartmentAssigneeCandidates
```

Nếu entity mới chưa có:

```text
PEMS.Domain/Entities/VisitLogisticsAssignmentAttempt.cs
PEMS.Infrastructure/Persistence/Configurations/VisitLogisticsAssignmentAttemptConfiguration.cs
DbSet<VisitLogisticsAssignmentAttempt>
```

Không dùng migration tự động. DB đã có base mới.

---

## 15. Checklist nghiệm thu

```text
[ ] Không còn mock data.
[ ] Department Leader xem đúng đơn của phòng ban mình.
[ ] Phân công lần đầu tạo row visit_logistics_assignment_attempts.
[ ] Nhân viên chấp nhận update attempt ACCEPTED và item ACCEPTED.
[ ] Nhân viên từ chối bắt nhập lý do.
[ ] Từ chối xong item quay về RECEIVED và assigned_to_user_id NULL.
[ ] Department Leader phân công lại được cho người khác.
[ ] Lịch sử phân công hiển thị đúng.
[ ] Không cho đổi người nếu nhiệm vụ đã ACCEPTED/IN_PROGRESS/DONE hoặc đã có ký biên bản.
[ ] Đề xuất thay đổi vẫn hoạt động.
[ ] Ký bàn giao/nghiệm thu dùng visit_logistics_item_handovers.
[ ] DONE chỉ khi đủ điều kiện nghiệm thu.
[ ] Không đổi UI layout.
[ ] Không tạo file rác.
[ ] Không tạo bảng mới.
[ ] Backend build pass.
[ ] Frontend build pass.
```

---

## 16. Báo cáo sau khi làm

Báo cáo ngắn:

```text
Đã làm:
- Backend commands/queries...
- Frontend hooks/API/types...
- Status mapping...
- Assignment attempt flow...

DB:
- Dùng base mới có visit_logistics_assignment_attempts.
- Không đổi schema thêm.

Files changed:
- ...

Build:
- Backend: pass/fail
- Frontend: pass/fail
```
