# PROMPT — Fix Visit Request Role UI Filters, Temporary Host Logic, Department Access, Staff/Student Tabs, and Seed Coverage

## 0. Vai trò của AI/code agent

Bạn là Senior Full-stack Developer cho dự án **PEMS — Partnership Engagement Management System**.

Nhiệm vụ của bạn là sửa đúng các lỗi còn lại ở màn **Quản lý tiếp khách / VisitRequestManagement** và seed test data, không rewrite toàn bộ màn hình, không phá flow 2 tab đã làm.

Phải tuân thủ:

```text
- Database-first MySQL.
- Backend Clean Architecture: Controller chỉ gọi IMediator.
- Backend là nơi enforce RBAC/scope/action cuối cùng.
- Frontend chỉ render theo dữ liệu/allowedActions backend trả về.
- Không sửa trigger/constraint để né lỗi seed.
- Không disable foreign_key_checks/triggers trong seed.
- Không dùng mock data.
```

---

# 1. Bối cảnh hiện tại

Màn `VisitRequestManagement.tsx` đã có:

```text
- Tab Đơn phụ trách
- Tab Đơn mời tham dự
- Filter theo trạng thái/phạm vi/ngày
- Render action theo allowedActions[]
- HO có thể thấy SINGLE_CAMPUS read-only
- UC-27 invitation Accept/Decline page + API
```

Nhưng còn các lỗi nghiệp vụ/UI:

```text
1. Trang HO đang có filter "Loại xử lý" hoặc logic filter xử lý bị trùng với filter status.
2. Staff Leader đang bị hiển thị như Host thật khi HO duyệt multi-campus và auto gán tạm current_host_user_id = Staff Leader.
3. Staff Leader đang thấy action hủy đoàn trong một số case, sai nghiệp vụ.
4. Badge "Được giao làm host" đang dùng sai cho Staff Leader tạm nhận đơn sau HO duyệt.
5. Seed SQL chưa khớp logic host: các campus instance đã vận hành phải có host hợp lệ, nhưng pending/rejected thì không cần host.
6. Staff thường bị mất hoặc không thấy đúng Tab Đơn mời tham dự.
7. dept.leader.hn@fpt.edu.vn và dept.hn@fpt.edu.vn không vào được trang quản trị phòng ban / department pages.
8. Student chỉ nên có Tab Đơn mời tham dự, không có Tab Đơn phụ trách nếu chưa triển khai student task module.
```

---

# 2. Rule nghiệp vụ cần chốt lại

## 2.1. HO

HO được xem:

```text
- MULTI_CAMPUS
- SINGLE_CAMPUS read-only
```

HO được xử lý:

```text
- Chỉ approve/reject MULTI_CAMPUS khi request đang PENDING_APPROVAL.
```

HO không được:

```text
- Duyệt SINGLE_CAMPUS.
- Từ chối SINGLE_CAMPUS.
- Gán host cho SINGLE_CAMPUS.
- Hủy đơn/campus instance.
- Tham gia Tab Đơn mời tham dự.
```

### Yêu cầu UI filter cho HO

Bỏ filter `Loại xử lý` ở trang HO nếu đã thêm trước đó.

Lý do:

```text
- "Cần xử lý" đã tương đương với status/filter như "Cần HO duyệt".
- "Chỉ theo dõi" đã tương đương với scope SINGLE_CAMPUS hoặc các status đã xử lý.
- Giữ thêm filter Loại xử lý làm UI bị trùng nghĩa.
```

HO chỉ cần các filter:

```text
- Tìm kiếm
- Trạng thái
- Phạm vi
- Khoảng ngày
```

Status option gợi ý cho HO:

```text
Tất cả trạng thái
Cần HO duyệt
Đơn cơ sở chờ duyệt
Đã duyệt
Từ chối
Đã hủy
Trước tiếp khách
Trong tiếp khách
Chờ đóng đoàn
Đã đóng đoàn
```

Mapping:

```text
Cần HO duyệt:
  requestStatus = PENDING_APPROVAL
  visitScope = MULTI_CAMPUS

Đơn cơ sở chờ duyệt:
  requestStatus = PENDING_APPROVAL
  visitScope = SINGLE_CAMPUS

Đã duyệt:
  requestStatus = APPROVED

Từ chối:
  requestStatus = REJECTED

Đã hủy:
  requestStatus = CANCELLED OR campusStatus = CANCELLED
```

---

## 2.2. Staff Leader

Staff Leader là:

```text
role_code = STAFF
sub_role = Leader
```

Staff Leader có nhiệm vụ:

```text
- Duyệt đơn SINGLE_CAMPUS thuộc campus mình.
- Khi duyệt SINGLE_CAMPUS phải chọn một Staff làm Host chính thức.
- Với MULTI_CAMPUS sau khi HO duyệt, Staff Leader của từng campus được gán tạm để tiếp nhận đơn và chọn Host chính thức.
```

Staff Leader **không phải Host thật** trong case HO duyệt multi-campus.

Nếu DB bắt buộc `visit_request_campuses.current_host_user_id` không được NULL sau khi approved thì có thể tạm gán Staff Leader vào `current_host_user_id`, nhưng backend/frontend phải hiểu đây là:

```text
PENDING_HOST_ASSIGNMENT
hoặc
TEMP_CAMPUS_RESPONSIBLE
hoặc
Chờ bổ nhiệm Host
```

Không được hiển thị Staff Leader là:

```text
Được giao làm host
```

Không được cấp action Host cho Staff Leader tạm nhận đơn:

```text
- Không CANCEL_BY_HOST.
- Không PREPARE_VISIT như Host thật.
- Không Close Delegation như Host thật.
```

Staff Leader chỉ được action:

```text
- VIEW_DETAIL
- ASSIGN_OFFICIAL_HOST hoặc TRANSFER_HOST / APPROVE_AND_ASSIGN_HOST tùy enum hiện có
```

Nếu hiện tại chỉ có `TRANSFER_HOST`, có thể dùng `TRANSFER_HOST` cho case tạm nhận sau HO duyệt, nhưng UI label phải là:

```text
Chọn Host chính thức
```

không phải:

```text
Chuyển host phụ trách
```

### Badge/label cho Staff Leader

Nếu row thỏa:

```text
role_code = STAFF
sub_role = Leader
visitScope = MULTI_CAMPUS
requestStatus = APPROVED
host_assignment_source = AUTO_STAFF_LEADER
current_host_user_id = currentUser.id
```

thì badge phải là:

```text
Chờ bổ nhiệm Host
```

hoặc:

```text
Cần chọn Host chính thức
```

Không hiển thị:

```text
Được giao làm host
```

---

## 2.3. Staff thường

Staff thường là:

```text
role_code = STAFF
sub_role = Staff
```

Staff thường có 2 luồng:

```text
1. Đơn phụ trách: chỉ khi là Host thật.
2. Đơn mời tham dự: khi là participant IC_SUPPORT đã ACCEPTED.
```

Tab Đơn mời tham dự của Staff phải được khôi phục.

Điều kiện Staff thấy Tab Đơn mời tham dự:

```text
visit_participants.user_id = currentUser.id
participant_role = IC_SUPPORT
status = ACCEPTED
is_host = false
currentUser.id != visit_request_campuses.current_host_user_id
```

Seed bắt buộc không để `staff.hn@fpt.edu.vn` vừa là Host vừa là IC_SUPPORT participant trong cùng visit instance.

Nếu cần host cho các case invitation của Staff, dùng helper:

```text
staff.host.seed.hn@fpt.edu.vn
```

Staff.hn chỉ là participant trong các case:

```text
IC_SUPPORT INVITED
IC_SUPPORT ACCEPTED
IC_SUPPORT DECLINED
IC_SUPPORT REMOVED
```

---

## 2.4. Department Lead / Department Staff

Tài khoản:

```text
dept.leader.hn@fpt.edu.vn
dept.hn@fpt.edu.vn
```

phải vào được đúng trang/module phòng ban nếu permission cho phép.

Cần kiểm tra mismatch role code:

```text
SQL có thể dùng role_code = DEPT
Frontend/backend có thể đang check roleCode = DEPARTMENT
```

Phải chuẩn hóa bằng một hàm resolve role:

```ts
function isDepartmentRole(roleCode: string) {
  const normalized = roleCode?.toUpperCase();
  return normalized === 'DEPT' || normalized === 'DEPARTMENT';
}
```

Áp dụng vào:

```text
- Sidebar visibility
- ProtectedRoute / RoleGuard
- Dashboard route resolution
- VisitRequestManagement showTabs
- Department Management route guard
- Permission checker nếu đang hard-code role string
```

Không được chỉ sửa mỗi UI nếu backend auth/permission vẫn chặn.

Department Lead:

```text
role_code = DEPT hoặc DEPARTMENT
sub_role = Leader
```

Department Staff:

```text
role_code = DEPT hoặc DEPARTMENT
sub_role = Staff
```

Trong màn Visit Request, Dept Lead/Dept Staff có thể có Tab Đơn mời tham dự nếu có DEPT_SUPPORT ACCEPTED.

---

## 2.5. Student

Student là:

```text
role_code = STUDENT
```

Hiện tại chưa triển khai Student task module, nên Student **chỉ có Tab Đơn mời tham dự**.

Không hiển thị Tab Đơn phụ trách cho Student.

Khi user là Student:

```text
activeTab mặc định = attending
loadDelegations phải gọi tab=attending
Không gọi tab=responsible
Không render nút chuyển sang Đơn phụ trách
```

Student chỉ thấy đơn khi:

```text
visit_participants.user_id = currentUser.id
participant_role = STUDENT
status = ACCEPTED
is_host = false
```

Student không có action:

```text
- Duyệt
- Từ chối đơn
- Gán host
- Hủy đơn
- Chuẩn bị tiếp khách
```

---

# 3. Sửa frontend

## 3.1. File cần kiểm tra/sửa

```text
frontend/pems-react/src/pages/dashboard/visit/VisitRequestManagement.tsx
frontend/pems-react/src/shared/auth/resolveEffectiveRole.ts
frontend/pems-react/src/shared/auth/RoleGuard.tsx
frontend/pems-react/src/shared/auth/ProtectedRoute.tsx
frontend/pems-react/src/components/dashboard/Sidebar.tsx
frontend/pems-react/src/shared/constants/roles.ts
frontend/pems-react/src/features/delegations/types/delegations.types.ts
frontend/pems-react/src/features/delegations/api/delegationsApi.ts
frontend/pems-react/src/shared/api/endpoints.ts
```

Tên file có thể khác, hãy quét project trước khi sửa.

---

## 3.2. Normalize subRole và roleCode

Hiện tại code có thể dùng:

```ts
const subRole = user?.subRole || '';
const isStaffLeader = isStaff && subRole === 'LEADER';
const isRegularStaff = isStaff && subRole === 'STAFF';
```

Nhưng SQL có thể trả:

```text
Leader
Staff
```

Phải normalize:

```ts
const roleCode = (user?.roleCode || '').toUpperCase();
const subRole = (user?.subRole || '').toUpperCase();

const isStaff = roleCode === 'STAFF';
const isStaffLeader = isStaff && subRole === 'LEADER';
const isRegularStaff = isStaff && subRole === 'STAFF';
const isDepartment = roleCode === 'DEPT' || roleCode === 'DEPARTMENT';
const isDepartmentLeader = isDepartment && subRole === 'LEADER';
const isDepartmentStaff = isDepartment && subRole === 'STAFF';
const isStudent = roleCode === 'STUDENT';
```

Áp dụng thống nhất toàn frontend.

---

## 3.3. Student chỉ có Đơn mời tham dự

Sửa tab logic:

```ts
const canUseAttendingTab = isRegularStaff || isDepartment || isStudent;
const canUseResponsibleTab = !isStudent && !isAdmin && !isVisitor;
```

Khi role là Student:

```ts
const defaultTab: Tab = isStudent ? 'attending' : 'responsible';
```

Trong `loadDelegations`:

```ts
const effectiveTab = isVisitor
  ? 'responsible'
  : isStudent
    ? 'attending'
    : targetTab;
```

UI tab:

```text
- Staff thường: hiển thị 2 tab.
- Department: hiển thị 2 tab nếu có responsible flow; nếu chưa có task module thì có thể chỉ hiển thị Đơn mời tham dự.
- Student: chỉ hiển thị Đơn mời tham dự, không hiển thị Đơn phụ trách.
- HO/Staff Leader: không hiển thị Đơn mời tham dự.
- Visitor: không dùng 2 tab, dùng Đơn của tôi.
- Admin: không vào màn nghiệp vụ.
```

---

## 3.4. Bỏ filter Loại xử lý cho HO

Nếu đã thêm relation filter hoặc xử lý kiểu:

```text
Loại xử lý
Cần xử lý
Chỉ theo dõi
```

thì bỏ riêng với HO.

HO chỉ render:

```text
Tìm kiếm
Trạng thái
Phạm vi
Khoảng ngày
```

Nếu filter config đang có:

```ts
showRelation: true
```

thì với HO đổi thành:

```ts
showRelation: false
```

---

## 3.5. Sửa badge Host

Hiện tại logic có thể là:

```tsx
if (row.currentUserIsHost && activeTab !== 'attending') {
  badges.push('Được giao làm host')
}
```

Phải sửa để phân biệt Host thật và Staff Leader tạm nhận sau HO duyệt.

Backend nên trả thêm field nếu chưa có:

```ts
type VisitRequestManagementItem = {
  currentUserIsHost?: boolean;
  currentUserRelation?: string;
  hostAssignmentSource?: string;
  isTemporaryCampusResponsible?: boolean;
  isPendingOfficialHostAssignment?: boolean;
}
```

Frontend rule:

```ts
const isPendingHostAssignment =
  row.currentUserRelation === 'PENDING_HOST_ASSIGNMENT'
  || row.currentUserRelation === 'TEMP_CAMPUS_RESPONSIBLE'
  || row.hostAssignmentSource === 'AUTO_STAFF_LEADER' && isStaffLeader;

if (isPendingHostAssignment) {
  badge = 'Chờ bổ nhiệm Host' hoặc 'Cần chọn Host chính thức';
} else if (row.currentUserIsHost && activeTab !== 'attending') {
  badge = 'Được giao làm Host';
}
```

Không hiển thị badge Host thật cho Staff Leader tạm nhận.

---

## 3.6. Sửa action label cho Staff Leader tạm nhận

Nếu action là `TRANSFER_HOST` nhưng row đang là pending host assignment:

```text
Button title phải là: Chọn Host chính thức
Không phải: Chuyển host phụ trách
```

Gợi ý:

```tsx
const transferHostTitle = isPendingHostAssignment
  ? 'Chọn Host chính thức'
  : 'Chuyển host phụ trách';
```

---

# 4. Sửa backend

## 4.1. File cần kiểm tra/sửa

```text
backend/PEMS.Application/Delegations/Queries/ViewGuestDelegationList/ViewGuestDelegationListQueryHandler.cs
backend/PEMS.Application/Delegations/Queries/ViewGuestDelegationList/ViewGuestDelegationListDto.cs
backend/PEMS.Application/Delegations/Queries/SearchDelegations/SearchDelegationsQueryHandler.cs
backend/PEMS.Application/Delegations/Queries/ViewGuestDelegationDetails/ViewGuestDelegationDetailsQueryHandler.cs
backend/PEMS.Application/Delegations/Commands/ProcessVisitRequest/ProcessVisitRequestCommandHandler.cs
backend/PEMS.Application/Delegations/Commands/ApproveCrossCampusRequest/ApproveCrossCampusRequestCommandHandler.cs
backend/PEMS.Application/Delegations/Commands/CancelVisitRequest/CancelVisitRequestCommandHandler.cs nếu có
```

Tên file có thể khác, hãy quét project trước khi sửa.

---

## 4.2. Backend phải phân biệt Host thật và Staff Leader tạm nhận

Khi HO duyệt MULTI_CAMPUS:

```text
DB có thể set current_host_user_id = Staff Leader của campus.
Đây chỉ là người chịu trách nhiệm tạm thời để chọn Host chính thức.
Không phải Host vận hành đoàn.
```

Backend DTO phải trả rõ:

```text
currentUserRelation = PENDING_HOST_ASSIGNMENT hoặc TEMP_CAMPUS_RESPONSIBLE
currentUserIsHost = false
isTemporaryCampusResponsible = true
isPendingOfficialHostAssignment = true
allowedActions = VIEW_DETAIL + TRANSFER_HOST hoặc ASSIGN_OFFICIAL_HOST
```

Không trả:

```text
CANCEL_BY_HOST
PREPARE_VISIT
CLOSE_DELEGATION
```

cho Staff Leader tạm nhận.

Host thật là khi:

```text
role_code = STAFF
sub_role = Staff
current_host_user_id = currentUser.id
host_assignment_source != AUTO_STAFF_LEADER
```

hoặc khi có participant:

```text
participant_role = IC_HOST
is_host = true
status = ASSIGNED
user_id = currentUser.id
```

và người đó không phải Staff Leader.

---

## 4.3. Staff Leader không có cancel-by-host

Trong `BuildAllowedActions` hoặc logic tương đương:

Không được thêm:

```text
CANCEL_BY_HOST
```

cho:

```text
role_code = STAFF
sub_role = Leader
```

Dù `current_host_user_id = currentUser.id` do auto assign sau HO duyệt.

Nếu Staff Leader được quyền từ chối/hủy theo nghiệp vụ riêng thì phải dùng action khác, ví dụ:

```text
CAMPUS_REJECT
CANCEL_BY_STAFF_LEADER
```

Nhưng hiện tại theo yêu cầu mới: Staff Leader không có chức năng hủy đoàn trong màn này.

---

## 4.4. Staff Tab Đơn mời tham dự

Backend query `tab=attending` phải lấy đúng:

```sql
visit_participants.user_id = currentUser.id
AND visit_participants.status = 'ACCEPTED'
AND visit_participants.is_host = 0
AND visit_participants.participant_role IN ('IC_SUPPORT', 'DEPT_SUPPORT', 'STUDENT')
AND visit_requests.status NOT IN ('REJECTED', 'CANCELLED')
AND visit_request_campuses.status <> 'CANCELLED'
```

Không loại nhầm Staff chỉ vì cùng campus hoặc vì host của instance là helper.

Nếu user là Staff thường:

```text
participant_role phải là IC_SUPPORT.
```

Nếu user là Dept/Department:

```text
participant_role phải là DEPT_SUPPORT.
```

Nếu user là Student:

```text
participant_role phải là STUDENT.
```

---

## 4.5. Department access

Kiểm tra backend trả user role code đang là:

```text
DEPT
```

hay:

```text
DEPARTMENT
```

Nếu SQL dùng `DEPT` nhưng frontend/route guard check `DEPARTMENT`, phải normalize ở response hoặc frontend.

Không đổi bừa DB role nếu permission matrix đang dựa vào role hiện tại.

Cách an toàn:

```text
- Backend AuthUserDto có thể trả roleCode đúng theo DB.
- Frontend normalize DEPT/DEPARTMENT về Department effective role.
- RoleGuard/Sidebar dùng effective role thay vì raw role string.
```

---

# 5. Sửa SQL seed

## 5.1. Nguyên tắc seed host

Chỉnh lại seed theo rule:

```text
1. Các campus instance có trạng thái đang chờ duyệt hoặc request bị từ chối thì có thể không có host.
2. Các campus instance sau khi request đã APPROVED và campus status đã vận hành thì bắt buộc có current_host_user_id hợp lệ.
3. Trạng thái CANCELLED sau khi đã từng approved cũng nên giữ host nếu đã có host trước khi hủy.
4. Không dùng current_host_user_id trỏ tới user không tồn tại.
5. Không hard-code user_id nếu có thể lấy theo email.
6. Không dùng AUTO_STAFF_LEADER cho SINGLE_CAMPUS.
```

Cụ thể:

```text
Không cần host:
- request_status = PENDING_APPROVAL
- request_status = REJECTED
- campus_status = WAITING_REQUEST_APPROVAL khi request chưa được duyệt

Cần host hợp lệ:
- request_status = APPROVED
- campus_status IN (ASSIGNED, BEFORE_VISIT, DURING_VISIT, AFTER_VISIT, CLOSED, CANCELLED)
```

## 5.2. Multi-campus sau HO duyệt

Khi HO duyệt MULTI_CAMPUS:

```text
visit_requests.status = APPROVED
visit_request_campuses.status = ASSIGNED hoặc BEFORE_VISIT tùy case seed
current_host_user_id = Staff Leader của campus
host_assignment_source = AUTO_STAFF_LEADER
```

Nhưng đây chỉ là:

```text
Chờ bổ nhiệm Host
```

Không seed participant `IC_HOST` cho Staff Leader nếu Staff Leader chỉ là tạm nhận.

Sau khi Staff Leader chọn Host chính thức:

```text
current_host_user_id = Staff thường
host_assignment_source = MANUAL_APPROVAL hoặc STAFF_LEADER_ASSIGNMENT nếu enum cho phép
```

và có thể seed participant:

```text
participant_role = IC_HOST
is_host = 1
status = ASSIGNED
user_id = Staff thường
```

## 5.3. Single-campus đã duyệt

Với SINGLE_CAMPUS đã duyệt:

```text
host_assignment_source = MANUAL_APPROVAL
host_assigned_by = Staff Leader
current_host_user_id = Staff thường
```

Không dùng:

```text
AUTO_STAFF_LEADER
```

cho SINGLE_CAMPUS.

## 5.4. Staff participant cases

Với case Staff tham dự:

```text
current_host_user_id = helper host khác
visit_participants.user_id = staff.hn@fpt.edu.vn
participant_role = IC_SUPPORT
is_host = 0
status = INVITED / ACCEPTED / DECLINED / REMOVED
```

Không để `staff.hn@fpt.edu.vn` là host trong cùng instance.

## 5.5. Department participant cases

Với case Department Lead/Department Staff tham dự:

```text
current_host_user_id = helper host HN hoặc Staff Host thật
visit_participants.user_id = dept.leader.hn@fpt.edu.vn hoặc dept.hn@fpt.edu.vn
participant_role = DEPT_SUPPORT
is_host = 0
status = INVITED / ACCEPTED / DECLINED / REMOVED
```

## 5.6. Student participant cases

Với case Student:

```text
current_host_user_id = helper host HN hoặc Staff Host thật
visit_participants.user_id = student@fpt.edu.vn
participant_role = STUDENT
is_host = 0
status = INVITED / ACCEPTED / DECLINED / REMOVED
```

Student không có host, không có responsible tab.

---

# 6. Diagnostic SQL bắt buộc sau khi seed

Tạo file diagnostic hoặc thêm cuối patch SQL các query sau.

## 6.1. Check host FK null/sai

```sql
SELECT
  'invalid_operational_instance_without_host' AS check_name,
  COUNT(*) AS invalid_count
FROM visit_request_campuses vrc
JOIN visit_requests vr ON vr.visit_request_id = vrc.visit_request_id
WHERE vr.status = 'APPROVED'
  AND vrc.status IN ('ASSIGNED','BEFORE_VISIT','DURING_VISIT','AFTER_VISIT','CLOSED','CANCELLED')
  AND vrc.current_host_user_id IS NULL;
```

## 6.2. Check current host không tồn tại

```sql
SELECT
  'invalid_current_host_fk' AS check_name,
  COUNT(*) AS invalid_count
FROM visit_request_campuses vrc
LEFT JOIN users u ON u.user_id = vrc.current_host_user_id
WHERE vrc.current_host_user_id IS NOT NULL
  AND u.user_id IS NULL;
```

## 6.3. Check SINGLE_CAMPUS dùng sai AUTO_STAFF_LEADER

```sql
SELECT
  'invalid_single_campus_auto_staff_leader_source' AS check_name,
  COUNT(*) AS invalid_count
FROM visit_request_campuses vrc
JOIN visit_requests vr ON vr.visit_request_id = vrc.visit_request_id
WHERE vr.visit_scope = 'SINGLE_CAMPUS'
  AND vrc.host_assignment_source = 'AUTO_STAFF_LEADER';
```

## 6.4. Check Staff Leader bị seed là IC_HOST participant

```sql
SELECT
  'invalid_staff_leader_as_ic_host_participant' AS check_name,
  COUNT(*) AS invalid_count
FROM visit_participants vp
JOIN users u ON u.user_id = vp.user_id
WHERE u.role_id IN (
  SELECT role_id FROM roles WHERE role_code = 'STAFF'
)
AND UPPER(u.sub_role) = 'LEADER'
AND vp.participant_role = 'IC_HOST';
```

## 6.5. Check participant accepted bị trùng host

```sql
SELECT
  'invalid_participant_is_also_current_host' AS check_name,
  COUNT(*) AS invalid_count
FROM visit_participants vp
JOIN visit_request_campuses vrc ON vrc.visit_instance_id = vp.visit_instance_id
WHERE vp.status = 'ACCEPTED'
  AND vp.is_host = 0
  AND vp.user_id = vrc.current_host_user_id;
```

Tất cả `invalid_count` phải bằng `0`.

---

# 7. Test cases sau khi sửa

## 7.1. HO

Login:

```text
ho@fpt.edu.vn
```

Kỳ vọng:

```text
- Không có filter Loại xử lý.
- Có filter Trạng thái, Phạm vi, Khoảng ngày.
- Thấy MULTI_CAMPUS chờ duyệt với action Duyệt/Từ chối.
- Thấy SINGLE_CAMPUS read-only với action Xem chi tiết בלבד.
- Không có Tab Đơn mời tham dự.
```

## 7.2. Staff Leader HN

Login:

```text
staff.leader.hn@fpt.edu.vn
```

Kỳ vọng:

```text
- Không có Tab Đơn mời tham dự.
- Không có action Hủy đoàn.
- Case MULTI_CAMPUS sau HO duyệt hiển thị badge: Chờ bổ nhiệm Host / Cần chọn Host chính thức.
- Không hiển thị badge: Được giao làm host cho Staff Leader tạm nhận.
- Action chọn Host chính thức mở modal chọn Staff thuộc HN.
```

## 7.3. Staff thường HN

Login:

```text
staff.hn@fpt.edu.vn
```

Kỳ vọng:

```text
- Có 2 tab: Đơn phụ trách, Đơn mời tham dự.
- Đơn phụ trách chỉ có case Staff là Host thật.
- Đơn mời tham dự hiển thị case IC_SUPPORT ACCEPTED.
- Case IC_SUPPORT INVITED nằm ở banner lời mời chờ phản hồi, không nằm trong Tab Đơn mời tham dự.
- Case DECLINED/REMOVED không hiện trong Tab Đơn mời tham dự.
```

## 7.4. Department Lead HN

Login:

```text
dept.leader.hn@fpt.edu.vn
```

Kỳ vọng:

```text
- Vào được trang quản trị phòng ban nếu permission cho phép.
- Vào được màn tiếp khách.
- Thấy Tab Đơn mời tham dự nếu có DEPT_SUPPORT ACCEPTED.
- Không bị chặn do mismatch DEPT vs DEPARTMENT.
```

## 7.5. Department Staff HN

Login:

```text
dept.hn@fpt.edu.vn
```

Kỳ vọng:

```text
- Vào được module phòng ban phù hợp với permission.
- Vào được màn tiếp khách nếu permission cho phép.
- Thấy Tab Đơn mời tham dự nếu có DEPT_SUPPORT ACCEPTED.
- Không bị chặn do mismatch DEPT vs DEPARTMENT.
```

## 7.6. Student

Login:

```text
student@fpt.edu.vn
```

Kỳ vọng:

```text
- Chỉ có Tab Đơn mời tham dự.
- Không có Tab Đơn phụ trách.
- Thấy case STUDENT ACCEPTED.
- Case STUDENT INVITED nằm ở banner lời mời chờ phản hồi.
- Không có action duyệt/từ chối/gán host/hủy.
```

## 7.7. Visitor

Login:

```text
visitor@example.com
```

Kỳ vọng:

```text
- Chỉ có Đơn của tôi.
- Không có 2 tab internal.
- Thấy các đơn do visitor tạo theo trạng thái.
- Không thấy phân công nội bộ không cần thiết.
```

---

# 8. Build/test bắt buộc

Sau khi sửa code:

```bash
dotnet build
```

Nếu có test project:

```bash
dotnet test
```

Frontend:

```bash
cd frontend/pems-react
npm run build
```

Nếu có:

```bash
npm run lint
npm run typecheck
```

Không báo pass nếu chưa chạy.

---

# 9. Output báo cáo sau khi làm

Báo cáo theo format:

```text
1. Summary
2. Files changed
3. Frontend changes
4. Backend changes
5. SQL seed changes
6. Permission/scope rules
7. Manual test result by account
8. Build/test result
9. Known limitations
10. TODO / cần xác nhận
```
