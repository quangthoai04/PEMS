# PROMPT CẬP NHẬT CODE CŨ THEO LOGIC `visit_participants` 4 LOẠI

## 0. Mục tiêu

Hãy cập nhật lại toàn bộ code cũ liên quan đến `visit_participants`, màn **Thành phần tham gia**, tab **Đơn phụ trách**, tab **Đơn mời tham dự**, API lời mời tham gia và logic role/subrole theo đúng nghiệp vụ mới.

Yêu cầu quan trọng:

- Phải đọc SQL mới nhất trước khi sửa code.
- Phải đọc tài liệu tổng quan dự án, use case, permission matrix, database schema nếu có.
- Không tự tạo role/enum mới ngoài SQL.
- Không hard-code role giả như `DEPT_LEADER` hoặc `STAFF_LEADER` nếu DB không có role đó.
- `Department Leader` là role `DEPT` + subrole/flag/permission leader theo doc/SQL.
- `Staff Leader/IC Head` là role `STAFF` + subrole/flag/permission leader theo doc/SQL.
- Admin, HO, Visitor không nằm trong bảng người tham gia nội bộ của visit instance.

---

## 1. Chốt nghiệp vụ mới

Bảng `visit_participants` chỉ có đúng **4 loại participant**:

```text
IC_HOST
IC_SUPPORT
DEPT_SUPPORT
STUDENT
```

Không có các loại sau:

```text
STUDENT_BUDDY
MEDIA
INTERPRETER
OTHER
```

Không được dùng lại các enum cũ này trong backend, frontend, seed, type, filter, badge hoặc UI label.

---

## 2. Ý nghĩa 4 loại participant

### 2.1. `IC_HOST`

`IC_HOST` là **Staff được gán làm host chính** của visit instance.

Trên UI gọi là:

```text
Host
```

Điều kiện dữ liệu:

```text
participant_role = IC_HOST
is_host = true
status = ASSIGNED
user.role = STAFF
user active
user thuộc campus của visit_instance
```

Host là người phụ trách chính, nên:

- Host nằm ở tab **Đơn phụ trách**.
- Host không nằm ở tab **Đơn mời tham dự**.
- Host không cần accept/decline lời mời.
- Host được tạo qua flow gán host/chuyển host, không qua flow invite participant thường.

---

### 2.2. `IC_SUPPORT`

`IC_SUPPORT` là **Staff khác trong phòng IC/campus đó** được host mời hỗ trợ.

Trên UI gọi là:

```text
Staff hỗ trợ IC
```

Điều kiện:

```text
participant_role = IC_SUPPORT
is_host = false
user.role = STAFF
user active
user cùng campus với visit_instance
user không phải host chính
user không phải Staff Leader/IC Head nếu nghiệp vụ không cho mời Staff Leader
```

Flow:

```text
Host mời Staff hỗ trợ
        ↓
status = INVITED
        ↓
Người được mời chấp nhận
        ↓
status = ACCEPTED
        ↓
Xuất hiện ở tab Đơn mời tham dự
```

Nếu từ chối:

```text
status = DECLINED
```

và không xuất hiện ở tab **Đơn mời tham dự**.

---

### 2.3. `DEPT_SUPPORT`

`DEPT_SUPPORT` là **người thuộc phòng ban/Department khác** được mời tham gia hoặc hỗ trợ.

Trên UI gọi là:

```text
Phòng ban hỗ trợ
```

Điều kiện:

```text
participant_role = DEPT_SUPPORT
is_host = false
user.role = DEPT
user active
```

Lưu ý:

```text
Department Leader không phải role riêng.
Department Leader = role DEPT + subrole/flag/permission leader theo SQL/doc.
```

Không hard-code:

```text
DEPT_LEADER
DEPARTMENT_LEADER
DEPARTMENT_LEAD
```

nếu SQL không có role này.

Flow nghiệp vụ:

```text
Host mời người/phòng ban Department
        ↓
Department Leader có thể nhận hoặc gán cho người khác trong phòng ban nếu use case/schema có hỗ trợ
        ↓
Người được mời/gán chấp nhận thì status = ACCEPTED
        ↓
Người được mời/gán từ chối thì status = DECLINED
```

Nếu người được gán từ chối thì coi là từ chối luôn, không tự động quay lại pending nếu nghiệp vụ không có action mời/gán lại.

---

### 2.4. `STUDENT`

`STUDENT` là sinh viên hỗ trợ.

Trên UI gọi là:

```text
Sinh viên hỗ trợ
```

Không phân tách thành:

```text
Buddy
Media
Interpreter
Other
```

Điều kiện:

```text
participant_role = STUDENT
is_host = false
user.role = STUDENT
user active
```

Flow:

```text
Host mời Student
        ↓
status = INVITED
        ↓
Student ACCEPTED
        ↓
Xuất hiện ở tab Đơn mời tham dự
```

Nếu `DECLINED` thì không hiển thị ở tab **Đơn mời tham dự**.

---

## 3. SQL chuẩn cần dùng

Bảng `visit_participants` phải dùng enum mới:

```sql
participant_role ENUM('IC_HOST','IC_SUPPORT','DEPT_SUPPORT','STUDENT') NOT NULL DEFAULT 'IC_SUPPORT'
```

Không còn:

```sql
'STUDENT_BUDDY','MEDIA','INTERPRETER','OTHER'
```

File SQL mới đã được cung cấp kèm theo. Hãy dùng SQL mới làm source of truth khi cập nhật code.

---

## 4. Cập nhật backend enum/type

Tìm và sửa toàn bộ enum/type liên quan đến participant role.

Các enum hợp lệ duy nhất:

```text
IC_HOST
IC_SUPPORT
DEPT_SUPPORT
STUDENT
```

Cần xóa hoặc thay thế mọi reference cũ:

```text
STUDENT_BUDDY
MEDIA
INTERPRETER
OTHER
```

Cần kiểm tra các nơi thường bị hard-code:

```text
Domain enum
Application DTO
Command request
Query response
Validation
Mapper/Profile
Frontend TypeScript union type
Seed/sample data
Badge/label
Filter dropdown
```

---

## 5. Cập nhật UI “Thành phần tham gia”

Phần UI hiện tại đang có nhiều nhóm cũ. Cần đổi thành đúng 4 khối:

```text
1. Host
2. Staff hỗ trợ IC
3. Phòng ban hỗ trợ
4. Sinh viên hỗ trợ
```

Mapping UI:

```text
IC_HOST      -> Host
IC_SUPPORT   -> Staff hỗ trợ IC
DEPT_SUPPORT -> Phòng ban hỗ trợ
STUDENT      -> Sinh viên hỗ trợ
```

Không hiển thị:

```text
Buddy
Media
Interpreter
Other
Student Buddy
```

---

## 6. Không mời HO và Staff Leader

Theo nghiệp vụ mới:

```text
Không mời HO.
Không mời Admin.
Không mời Visitor.
Không mời Staff Leader/IC Head.
```

Staff Leader/IC Head là người:

```text
Duyệt đơn
Gán host
Chuyển host
Theo dõi đơn campus
```

nên không phải participant được mời tham dự.

HO xử lý đơn liên cơ sở nên cũng không nằm trong tab mời tham dự.

---

## 7. Tab “Đơn phụ trách”

Tab **Đơn phụ trách** dành cho người có trách nhiệm xử lý/chủ trì.

### Host

Nếu user là host:

```text
visit_participants.user_id = currentUser.id
AND participant_role = IC_HOST
AND is_host = true
AND status = ASSIGNED
```

hoặc nếu hệ thống lấy host từ instance:

```text
visit_request_campuses.host_user_id = currentUser.id
```

thì đơn phải xuất hiện ở tab **Đơn phụ trách**.

Host không được xuất hiện ở tab **Đơn mời tham dự**.

### Staff Leader/IC Head

Staff Leader/IC Head không phải role riêng nếu SQL không có. Xác định bằng:

```text
role = STAFF
AND subrole/flag/permission leader theo doc/SQL
```

Staff Leader thấy đơn campus mình, đơn cần gán/chuyển host theo logic hiện tại.

Staff Leader không có tab **Đơn mời tham dự**.

### HO

HO thấy đơn liên cơ sở cần xử lý theo logic đã implement.

HO không có tab **Đơn mời tham dự**.

---

## 8. Tab “Đơn mời tham dự”

Tab **Đơn mời tham dự** chỉ dành cho user được host/người phụ trách mời hỗ trợ, đã chấp nhận, và không phải host/người quản lý chính.

Query đúng:

```text
visit_participants.user_id == currentUser.id
AND visit_participants.status == ACCEPTED
AND visit_participants.is_host == false
AND visit_participants.participant_role IN (IC_SUPPORT, DEPT_SUPPORT, STUDENT)
AND visit_requests.status NOT IN (REJECTED, CANCELLED)
AND visit_request_campuses.status NOT IN (REJECTED, CANCELLED) nếu instance có status riêng
AND currentUser.id != visit_request_campuses.host_user_id nếu có host_user_id
AND currentUser.id != visit_requests.created_by
AND invited_by IS NULL OR invited_by != currentUser.id
```

Không lấy:

```text
IC_HOST
is_host = true
HO
Admin
Visitor
Staff Leader/IC Head
người tạo đơn
người là host của instance
người là assigned owner/phụ trách chính
```

Nếu có dữ liệu cũ làm user vừa là host vừa là participant ACCEPTED, backend vẫn phải loại khỏi tab **Đơn mời tham dự**.

---

## 9. API/Command cần kiểm tra và cập nhật

Tìm và sửa các API/command liên quan:

```text
GetVisitParticipants
InviteParticipant
ConfirmParticipation
DeclineParticipation
RemoveParticipant
AssignDepartmentParticipant
TransferHost
GetMyAttending
GetMyResponsibilities
ViewGuestDelegationList
```

Tên file có thể khác, phải search toàn project.

---

## 10. Validation khi mời participant

### 10.1. Mời `IC_SUPPORT`

Chỉ hợp lệ nếu:

```text
target user role = STAFF
target user active
target user cùng campus với visit_instance
target user không phải host chính
target user không phải Staff Leader/IC Head nếu nghiệp vụ không cho mời Staff Leader
```

Không cho mời:

```text
HO
Admin
Visitor
Student
Dept
current host
Staff Leader/IC Head
```

### 10.2. Mời `DEPT_SUPPORT`

Chỉ hợp lệ nếu:

```text
target user role = DEPT
target user active
```

Nếu có Department Leader thì xác định bằng:

```text
role = DEPT + subrole/flag/permission leader
```

Không tạo role riêng `DEPT_LEADER` nếu DB không có.

### 10.3. Mời `STUDENT`

Chỉ hợp lệ nếu:

```text
target user role = STUDENT
target user active
```

### 10.4. `IC_HOST` không dùng API invite thường

`IC_HOST` chỉ được tạo qua:

```text
Staff Leader duyệt và gán host
Staff Leader chuyển host
HO duyệt liên cơ sở auto-gán host tạm nếu schema hiện tại yêu cầu
```

Không cho user tự accept/decline host.

---

## 11. Confirm/Decline Participation

### Confirm

Chỉ cho confirm nếu:

```text
participant.user_id == currentUser.id
participant.status IN (INVITED, ASSIGNED nếu flow department assignment dùng ASSIGNED)
participant.is_host == false
participant.participant_role IN (IC_SUPPORT, DEPT_SUPPORT, STUDENT)
```

Sau confirm:

```text
status = ACCEPTED
responded_at = now
```

Không cho confirm:

```text
IC_HOST
is_host = true
HO
Admin
Visitor
Staff Leader/IC Head
```

### Decline

Chỉ cho decline nếu:

```text
participant.user_id == currentUser.id
participant.status IN (INVITED, ASSIGNED nếu flow department assignment dùng ASSIGNED)
participant.is_host == false
participant.participant_role IN (IC_SUPPORT, DEPT_SUPPORT, STUDENT)
```

Sau decline:

```text
status = DECLINED
responded_at = now
```

Nếu DEPT user được gán từ Department Leader rồi từ chối, trạng thái là `DECLINED`.

---

## 12. Transfer Host

Chuyển host là flow riêng, không phải invite participant thường.

Khi chuyển host:

```text
Old host:
- is_host = false hoặc status = REMOVED tùy schema/logic hiện tại

New host:
- participant_role = IC_HOST
- is_host = true
- status = ASSIGNED
- assigned_by = currentUser.id
- assigned_at = now
```

Đồng bộ với:

```text
visit_request_campuses.host_user_id = newHostUserId
```

Validate new host:

```text
user.role = STAFF
user active
user cùng campus
user không phải HO/Admin/Visitor
user không phải Staff Leader nếu nghiệp vụ không cho Staff Leader làm host thật
```

---

## 13. Permission/RBAC

Không tạo role mới nếu SQL không có.

Cần dùng đúng cách xác định:

```text
Staff Leader = STAFF + subrole/flag/permission leader
Department Leader = DEPT + subrole/flag/permission leader
```

Không hard-code role giả:

```text
STAFF_LEADER
DEPT_LEADER
DEPARTMENT_LEADER
```

Nếu permission seed đang theo UC code thì dùng đúng code trong seed hiện tại.

Các nhóm quyền cần kiểm tra:

```text
Quản lý host/chuyển host
Quản lý participant trong instance mình phụ trách
Mời IC_SUPPORT/DEPT_SUPPORT/STUDENT
Xem lời mời của mình
Chấp nhận lời mời
Từ chối lời mời
Xem tab Đơn mời tham dự
```

HO/Staff Leader không dùng tab **Đơn mời tham dự**.

---

## 14. Frontend cần cập nhật

### 14.1. TypeScript type

Cập nhật type:

```ts
type ParticipantRole = 'IC_HOST' | 'IC_SUPPORT' | 'DEPT_SUPPORT' | 'STUDENT';
```

Xóa:

```ts
'STUDENT_BUDDY'
'MEDIA'
'INTERPRETER'
'OTHER'
```

### 14.2. UI label

Mapping:

```ts
IC_HOST: 'Host'
IC_SUPPORT: 'Staff hỗ trợ IC'
DEPT_SUPPORT: 'Phòng ban hỗ trợ'
STUDENT: 'Sinh viên hỗ trợ'
```

### 14.3. Tab rendering

Không hiển thị tab **Đơn mời tham dự** cho:

```text
HO
Admin
Visitor
Staff Leader/IC Head
```

Chỉ hiển thị tab này cho user thường có khả năng được mời:

```text
STAFF thường
DEPT
STUDENT
```

Nếu user không có dữ liệu thì empty state, không báo lỗi.

### 14.4. Action rendering

Frontend không tự suy luận toàn bộ bằng role nếu backend có `allowedActions`.

Ưu tiên render theo:

```text
allowedActions[]
```

Backend vẫn phải validate lại toàn bộ.

---

## 15. SQL/data cần kiểm tra

Sau khi đổi enum, đảm bảo SQL không còn các giá trị:

```text
STUDENT_BUDDY
MEDIA
INTERPRETER
```

Trong `visit_participants.participant_role`.

Riêng từ `OTHER` có thể vẫn tồn tại ở bảng khác như gender, partner_type, file provider, logistics item. Không được replace toàn bộ `OTHER` trong SQL.

Chỉ loại bỏ `OTHER` khỏi enum `visit_participants.participant_role` và seed của `visit_participants`.

---

## 16. Test case bắt buộc

### Host

```text
Staff được gán host -> IC_HOST, is_host = true, status = ASSIGNED.
Host thấy đơn ở Tab Đơn phụ trách.
Host không thấy đơn đó ở Tab Đơn mời tham dự.
Host mời Staff hỗ trợ IC được.
Host mời Dept được.
Host mời Student được.
Host không mời được HO/Admin/Visitor/Staff Leader.
```

### IC Support

```text
Staff thường được mời -> IC_SUPPORT, status INVITED.
Chưa accept -> không xuất hiện Tab Đơn mời tham dự.
Accept -> xuất hiện Tab Đơn mời tham dự.
Decline -> không xuất hiện Tab Đơn mời tham dự.
Không có quyền duyệt/từ chối đơn/gán host.
```

### Department Support

```text
DEPT user được mời -> DEPT_SUPPORT.
Accept -> xuất hiện Tab Đơn mời tham dự.
Decline -> không xuất hiện Tab Đơn mời tham dự.
Department Leader = DEPT + subrole/flag/permission leader, không phải role riêng.
```

### Student

```text
Student được mời -> STUDENT.
Không còn Student Buddy/Media/Interpreter.
Accept -> xuất hiện Tab Đơn mời tham dự.
Decline -> không xuất hiện Tab Đơn mời tham dự.
```

### HO / Staff Leader

```text
HO không có Tab Đơn mời tham dự.
Staff Leader không có Tab Đơn mời tham dự.
HO không được mời làm participant.
Staff Leader không được mời làm participant support.
Nếu dữ liệu cũ có HO/Staff Leader trong visit_participants, backend phải loại khỏi Tab Đơn mời tham dự.
```

---

## 17. Kết quả mong muốn

Sau khi sửa xong:

```text
1. visit_participants chỉ còn 4 role: IC_HOST, IC_SUPPORT, DEPT_SUPPORT, STUDENT.
2. UI Thành phần tham gia chỉ còn 4 khối: Host, Staff hỗ trợ IC, Phòng ban hỗ trợ, Sinh viên hỗ trợ.
3. Không còn MEDIA, INTERPRETER, OTHER, STUDENT_BUDDY trong participant role.
4. Host nằm ở Đơn phụ trách, không nằm ở Đơn mời tham dự.
5. Đơn mời tham dự chỉ hiện IC_SUPPORT/DEPT_SUPPORT/STUDENT đã ACCEPTED.
6. Không mời HO/Admin/Visitor/Staff Leader.
7. Department Leader được hiểu là DEPT + subrole/flag/permission leader.
8. Staff Leader được hiểu là STAFF + subrole/flag/permission leader.
9. Backend filter đúng dữ liệu, frontend render theo allowedActions.
10. Nếu không có dữ liệu thì empty state, không báo lỗi.
```
