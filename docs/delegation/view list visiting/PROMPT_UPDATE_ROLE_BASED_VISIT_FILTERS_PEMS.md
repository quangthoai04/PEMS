# PROMPT — Cập nhật bộ lọc danh sách tiếp khách theo role/tab trong PEMS

## 0. Vai trò của AI/code agent

Bạn là Senior Full-stack Developer cho dự án **PEMS — Partnership Engagement Management System**.

Bạn cần cập nhật màn quản lý đơn tiếp khách để **filter hiển thị theo role + tab hiện tại**, không dùng một bộ filter global cho tất cả role.

Mục tiêu không phải viết lại màn hình, mà là sửa đúng phạm vi:

```text
- Giữ nguyên layout chính của VisitRequestManagement.
- Không rewrite toàn bộ frontend.
- Không đổi route nếu không cần.
- Không đổi business flow đã chốt.
- Không bỏ allowedActions[] từ backend.
- Backend vẫn là nơi enforce scope/filter cuối cùng.
```

---

## 1. Bối cảnh hiện tại

File frontend chính hiện tại:

```text
frontend/pems-react/src/pages/dashboard/visit/VisitRequestManagement.tsx
```

Hiện tại file này đang có filter global:

```ts
const STATUS_FILTER_OPTIONS = [...]
const VISIT_SCOPE_OPTIONS = [...]
```

Vấn đề:

```text
Role nào cũng thấy cùng một danh sách trạng thái/phạm vi.
Điều này gây sai UX vì mỗi role chỉ có thể thấy/xử lý một phạm vi dữ liệu khác nhau.
```

Ví dụ:

```text
HO cần thấy filter “Cần HO duyệt” và “Chỉ theo dõi”.
Staff Leader cần thấy “Chờ duyệt tại campus” và “Đã phân công Host”.
Staff thường không nên thấy filter “Chờ duyệt” vì họ không xử lý phê duyệt.
Visitor không nên thấy các thuật ngữ kỹ thuật như ASSIGNED, BEFORE_VISIT, AFTER_VISIT.
Student chỉ nên thấy filter đơn giản như Sắp diễn ra / Đang diễn ra / Đã kết thúc.
```

---

## 2. Rule nghiệp vụ đã chốt

### 2.1. Role/effective role

Không dùng thuật ngữ mơ hồ như `IC Head`, `Staff / Host`.

Phải hiểu role theo `role_code` và `sub_role`:

```text
ADMIN
= role_code = ADMIN
= Không tham gia nghiệp vụ tiếp khách.

HO
= role_code = HO
= Xử lý/giám sát đơn liên cơ sở và xem đơn một cơ sở read-only.

Staff Leader
= role_code = STAFF
AND sub_role = Leader
= Xử lý đơn thuộc campus của mình.

Staff thường
= role_code = STAFF
AND sub_role = Staff
= Chỉ thấy đơn khi được gán Host, được giao việc, hoặc được mời tham gia.

Department Lead
= role_code = DEPARTMENT hoặc DEPT
AND sub_role = Leader
= Trưởng phòng ban ngoài IC.

Department Staff
= role_code = DEPARTMENT hoặc DEPT
AND sub_role = Staff
= Nhân sự phòng ban ngoài IC.

Student
= role_code = STUDENT
= Sinh viên hỗ trợ.

Visitor
= role_code = VISITOR
= Khách gửi request, chỉ xem đơn của chính mình.
```

Nếu code hiện tại dùng `DEPT`, giữ tương thích. Nếu đã chuẩn hóa thành `DEPARTMENT`, dùng `DEPARTMENT`. Không được dùng lẫn lộn mà không normalize.

---

## 3. Mục tiêu cần làm

Cập nhật filter của màn `VisitRequestManagement` theo hướng:

```text
1. Tạo filter config động theo role + tab.
2. Chỉ hiển thị filter phù hợp với role hiện tại.
3. Status filter phải dùng label nghiệp vụ dễ hiểu theo từng role.
4. Scope filter chỉ hiện với role cần filter scope.
5. Thêm filter “Loại xử lý” hoặc “Vai trò của tôi” cho role cần phân biệt relation.
6. Frontend gửi params rõ ràng cho backend.
7. Backend hỗ trợ các params mới nếu chưa có.
8. Backend vẫn validate role/scope và không tin frontend.
```

---

## 4. Frontend changes

### 4.1. File cần sửa

Ưu tiên sửa các file sau:

```text
frontend/pems-react/src/pages/dashboard/visit/VisitRequestManagement.tsx
frontend/pems-react/src/features/delegations/api/delegationsApi.ts
frontend/pems-react/src/features/delegations/types/delegations.types.ts
```

Nếu cần tách helper để code sạch hơn, tạo file mới:

```text
frontend/pems-react/src/features/delegations/config/visitRequestFilterConfig.ts
```

Không nhét toàn bộ logic config dài vào component nếu làm file quá khó đọc.

---

### 4.2. Thay filter global bằng filter config động

Hiện tại đang có:

```ts
const STATUS_FILTER_OPTIONS = [...]
const VISIT_SCOPE_OPTIONS = [...]
```

Hãy thay bằng:

```ts
const filterConfig = getVisitRequestFilterConfig({
  roleCode,
  subRole,
  activeTab,
  isVisitor,
});
```

Tạo type:

```ts
type VisitStatusFilterOption = {
  value: string;
  label: string;
  requestStatus?: string;
  campusStatus?: string;
  visitScopes?: string[];
  cancelledOnly?: boolean;
  relation?: string;
  readOnlyOnly?: boolean;
  actionableOnly?: boolean;
  timing?: 'UPCOMING' | 'ONGOING' | 'ENDED';
};

type VisitScopeFilterOption = {
  value: string;
  label: string;
};

type VisitRelationFilterOption = {
  value: string;
  label: string;
};

type VisitFilterConfig = {
  showKeyword: boolean;
  showStatus: boolean;
  showScope: boolean;
  showRelation: boolean;
  statusLabel?: string;
  scopeLabel?: string;
  relationLabel?: string;
  statusOptions: VisitStatusFilterOption[];
  scopeOptions: VisitScopeFilterOption[];
  relationOptions: VisitRelationFilterOption[];
};
```

---

### 4.3. Normalize role/subRole

Trong `VisitRequestManagement.tsx`, normalize role/subRole chắc chắn:

```ts
const roleCode = (user?.roleCode || '').toUpperCase();
const subRole = (user?.subRole || '').toUpperCase();

const isAdmin = roleCode === 'ADMIN';
const isHO = roleCode === 'HO';
const isStaff = roleCode === 'STAFF';
const isStaffLeader = isStaff && subRole === 'LEADER';
const isRegularStaff = isStaff && subRole === 'STAFF';
const isVisitor = roleCode === 'VISITOR';
const isDept = roleCode === 'DEPARTMENT' || roleCode === 'DEPT';
const isDeptLeader = isDept && subRole === 'LEADER';
const isDeptStaff = isDept && subRole === 'STAFF';
const isStudent = roleCode === 'STUDENT';
```

Lưu ý: code hiện tại đang so sánh `subRole === 'LEADER'` và `subRole === 'STAFF'`, nhưng nếu backend trả `Leader` hoặc `Staff` thì sẽ sai. Phải uppercase `subRole`.

---

## 5. Filter config chi tiết theo role

### 5.1. ADMIN

Admin không dùng màn này.

```text
Không hiển thị filter.
Không hiển thị tab.
Không hiển thị danh sách nghiệp vụ.
```

Component hiện đã có empty state cho Admin, giữ nguyên.

---

### 5.2. HO

HO chỉ có tab danh sách chính, không có Tab Đơn mời tham dự.

HO được xem:

```text
- MULTI_CAMPUS: có thể xử lý nếu đang chờ HO duyệt.
- SINGLE_CAMPUS: chỉ xem read-only để theo dõi.
```

Filter cho HO:

```text
Status:
- Tất cả trạng thái
- Cần HO duyệt
- Đơn cơ sở chờ duyệt
- Đã duyệt
- Từ chối
- Đang chuẩn bị tiếp khách
- Đang tiếp khách
- Chờ đóng đoàn
- Đã đóng đoàn
- Đã hủy

Scope:
- Tất cả phạm vi
- Liên cơ sở
- Đơn cơ sở

Loại xử lý:
- Tất cả
- Cần xử lý
- Chỉ theo dõi
```

Mapping đề xuất:

```ts
if (roleCode === 'HO') {
  return {
    showKeyword: true,
    showStatus: true,
    showScope: true,
    showRelation: true,
    statusLabel: 'Trạng thái',
    scopeLabel: 'Phạm vi',
    relationLabel: 'Loại xử lý',
    scopeOptions: [
      { value: '', label: 'Tất cả phạm vi' },
      { value: 'MULTI_CAMPUS', label: 'Liên cơ sở' },
      { value: 'SINGLE_CAMPUS', label: 'Đơn cơ sở' },
    ],
    relationOptions: [
      { value: '', label: 'Tất cả' },
      { value: 'ACTION_REQUIRED', label: 'Cần xử lý' },
      { value: 'READ_ONLY', label: 'Chỉ theo dõi' },
    ],
    statusOptions: [
      { value: '', label: 'Tất cả trạng thái' },
      { value: 'HO_PENDING', label: 'Cần HO duyệt', requestStatus: 'PENDING_APPROVAL', visitScopes: ['MULTI_CAMPUS'], actionableOnly: true },
      { value: 'SINGLE_PENDING_READONLY', label: 'Đơn cơ sở chờ duyệt', requestStatus: 'PENDING_APPROVAL', visitScopes: ['SINGLE_CAMPUS'], readOnlyOnly: true },
      { value: 'APPROVED', label: 'Đã duyệt', requestStatus: 'APPROVED' },
      { value: 'REJECTED', label: 'Từ chối', requestStatus: 'REJECTED' },
      { value: 'BEFORE_VISIT', label: 'Đang chuẩn bị tiếp khách', requestStatus: 'APPROVED', campusStatus: 'BEFORE_VISIT' },
      { value: 'DURING_VISIT', label: 'Đang tiếp khách', requestStatus: 'APPROVED', campusStatus: 'DURING_VISIT' },
      { value: 'AFTER_VISIT', label: 'Chờ đóng đoàn', requestStatus: 'APPROVED', campusStatus: 'AFTER_VISIT' },
      { value: 'CLOSED', label: 'Đã đóng đoàn', requestStatus: 'APPROVED', campusStatus: 'CLOSED' },
      { value: 'CANCELLED_ANY', label: 'Đã hủy', cancelledOnly: true },
    ],
  };
}
```

---

### 5.3. Staff Leader

Staff Leader chỉ xử lý trong campus của mình.

Không hiển thị Tab Đơn mời tham dự.

Filter cho Staff Leader:

```text
Status:
- Tất cả trạng thái
- Chờ duyệt tại campus
- Cần phân công Host
- Đã phân công Host
- Trước tiếp khách
- Trong tiếp khách
- Chờ đóng đoàn
- Đã đóng đoàn
- Từ chối
- Đã hủy

Scope:
- Tất cả trong campus tôi
- Đơn một cơ sở
- Liên cơ sở có campus tôi

Loại xử lý:
- Tất cả
- Cần xử lý
- Theo dõi
```

Mapping:

```ts
if (roleCode === 'STAFF' && subRole === 'LEADER') {
  return {
    showKeyword: true,
    showStatus: true,
    showScope: true,
    showRelation: true,
    statusLabel: 'Trạng thái',
    scopeLabel: 'Phạm vi',
    relationLabel: 'Loại xử lý',
    scopeOptions: [
      { value: '', label: 'Tất cả trong campus tôi' },
      { value: 'SINGLE_CAMPUS', label: 'Đơn một cơ sở' },
      { value: 'MULTI_CAMPUS', label: 'Liên cơ sở có campus tôi' },
    ],
    relationOptions: [
      { value: '', label: 'Tất cả' },
      { value: 'ACTION_REQUIRED', label: 'Cần xử lý' },
      { value: 'READ_ONLY', label: 'Theo dõi' },
    ],
    statusOptions: [
      { value: '', label: 'Tất cả trạng thái' },
      { value: 'PENDING_APPROVAL', label: 'Chờ duyệt tại campus', requestStatus: 'PENDING_APPROVAL' },
      { value: 'WAITING_REQUEST_APPROVAL', label: 'Cần phân công Host', requestStatus: 'APPROVED', campusStatus: 'WAITING_REQUEST_APPROVAL' },
      { value: 'ASSIGNED', label: 'Đã phân công Host', requestStatus: 'APPROVED', campusStatus: 'ASSIGNED' },
      { value: 'BEFORE_VISIT', label: 'Trước tiếp khách', requestStatus: 'APPROVED', campusStatus: 'BEFORE_VISIT' },
      { value: 'DURING_VISIT', label: 'Trong tiếp khách', requestStatus: 'APPROVED', campusStatus: 'DURING_VISIT' },
      { value: 'AFTER_VISIT', label: 'Chờ đóng đoàn', requestStatus: 'APPROVED', campusStatus: 'AFTER_VISIT' },
      { value: 'CLOSED', label: 'Đã đóng đoàn', requestStatus: 'APPROVED', campusStatus: 'CLOSED' },
      { value: 'REJECTED', label: 'Từ chối', requestStatus: 'REJECTED' },
      { value: 'CANCELLED_ANY', label: 'Đã hủy', cancelledOnly: true },
    ],
  };
}
```

---

### 5.4. Staff thường — Tab Đơn phụ trách

Staff thường chỉ thấy đơn mình là Host hoặc được giao việc.

Filter:

```text
Status:
- Tất cả trạng thái
- Đã phân công
- Trước tiếp khách
- Trong tiếp khách
- Chờ đóng đoàn
- Đã đóng đoàn
- Đã hủy

Vai trò của tôi:
- Tất cả
- Tôi là Host
- Tôi được giao việc
```

Không hiển thị scope filter.

Mapping:

```ts
if (roleCode === 'STAFF' && subRole === 'STAFF' && activeTab === 'responsible') {
  return {
    showKeyword: true,
    showStatus: true,
    showScope: false,
    showRelation: true,
    statusLabel: 'Trạng thái',
    relationLabel: 'Vai trò của tôi',
    scopeOptions: [],
    relationOptions: [
      { value: '', label: 'Tất cả' },
      { value: 'HOST', label: 'Tôi là Host' },
      { value: 'TASK_ASSIGNEE', label: 'Tôi được giao việc' },
    ],
    statusOptions: [
      { value: '', label: 'Tất cả trạng thái' },
      { value: 'ASSIGNED', label: 'Đã phân công', requestStatus: 'APPROVED', campusStatus: 'ASSIGNED' },
      { value: 'BEFORE_VISIT', label: 'Trước tiếp khách', requestStatus: 'APPROVED', campusStatus: 'BEFORE_VISIT' },
      { value: 'DURING_VISIT', label: 'Trong tiếp khách', requestStatus: 'APPROVED', campusStatus: 'DURING_VISIT' },
      { value: 'AFTER_VISIT', label: 'Chờ đóng đoàn', requestStatus: 'APPROVED', campusStatus: 'AFTER_VISIT' },
      { value: 'CLOSED', label: 'Đã đóng đoàn', requestStatus: 'APPROVED', campusStatus: 'CLOSED' },
      { value: 'CANCELLED_ANY', label: 'Đã hủy', cancelledOnly: true },
    ],
  };
}
```

---

### 5.5. Staff thường / Department / Student — Tab Đơn mời tham dự

Tab Đơn mời tham dự chỉ chứa các lời mời đã `ACCEPTED`.

Filter đơn giản:

```text
Status:
- Tất cả trạng thái
- Sắp diễn ra
- Đang diễn ra
- Đã kết thúc
- Đã hủy
```

Không hiển thị scope filter.
Không hiển thị relation filter.

Mapping:

```ts
function getParticipantAttendingFilterConfig(): VisitFilterConfig {
  return {
    showKeyword: true,
    showStatus: true,
    showScope: false,
    showRelation: false,
    statusLabel: 'Trạng thái',
    scopeOptions: [],
    relationOptions: [],
    statusOptions: [
      { value: '', label: 'Tất cả trạng thái' },
      { value: 'UPCOMING', label: 'Sắp diễn ra', timing: 'UPCOMING' },
      { value: 'DURING_VISIT', label: 'Đang diễn ra', requestStatus: 'APPROVED', campusStatus: 'DURING_VISIT' },
      { value: 'CLOSED', label: 'Đã kết thúc', requestStatus: 'APPROVED', campusStatus: 'CLOSED' },
      { value: 'CANCELLED_ANY', label: 'Đã hủy', cancelledOnly: true },
    ],
  };
}
```

Áp dụng cho:

```text
STAFF + sub_role = Staff + activeTab = attending
DEPARTMENT/DEPT + activeTab = attending
STUDENT + activeTab = attending
```

---

### 5.6. Department Lead / Department Staff — Tab Đơn phụ trách

Nếu hiện tại chưa làm module `Nhiệm vụ công việc`, không cố filter theo task status sâu.

Tạm thời dùng filter đơn giản nếu Department có tab responsible:

```text
Status:
- Tất cả trạng thái
- Đang chờ xác nhận
- Đã xác nhận
- Đang thực hiện
- Chờ nghiệm thu
- Hoàn thành
- Đã từ chối
- Đã hủy

Vai trò:
- Tất cả
- Yêu cầu hỗ trợ phòng ban
- Được mời tham gia
```

Nếu backend chưa có task/resource status tương ứng, không gửi params task status phức tạp. Chỉ giữ keyword/date, hoặc defer phần này sang module Department task.

---

### 5.7. Visitor

Visitor không dùng 2 tab. Chỉ có `Đơn của tôi`.

Filter phải dùng label thân thiện:

```text
Status:
- Tất cả trạng thái
- Đã gửi, chờ xử lý
- Đã được duyệt
- Đang chuẩn bị tiếp khách
- Đang tiếp khách
- Đã hoàn tất
- Bị từ chối
- Đã hủy
```

Không hiển thị scope filter trừ khi thật sự cần.

Mapping:

```ts
if (roleCode === 'VISITOR') {
  return {
    showKeyword: true,
    showStatus: true,
    showScope: false,
    showRelation: false,
    statusLabel: 'Trạng thái',
    scopeOptions: [],
    relationOptions: [],
    statusOptions: [
      { value: '', label: 'Tất cả trạng thái' },
      { value: 'PENDING_APPROVAL', label: 'Đã gửi, chờ xử lý', requestStatus: 'PENDING_APPROVAL' },
      { value: 'APPROVED', label: 'Đã được duyệt', requestStatus: 'APPROVED' },
      { value: 'BEFORE_VISIT', label: 'Đang chuẩn bị tiếp khách', requestStatus: 'APPROVED', campusStatus: 'BEFORE_VISIT' },
      { value: 'DURING_VISIT', label: 'Đang tiếp khách', requestStatus: 'APPROVED', campusStatus: 'DURING_VISIT' },
      { value: 'CLOSED', label: 'Đã hoàn tất', requestStatus: 'APPROVED', campusStatus: 'CLOSED' },
      { value: 'REJECTED', label: 'Bị từ chối', requestStatus: 'REJECTED' },
      { value: 'CANCELLED_ANY', label: 'Đã hủy', cancelledOnly: true },
    ],
  };
}
```

---

## 6. UI render changes

### 6.1. Trạng thái filter

Đổi mọi chỗ dùng `STATUS_FILTER_OPTIONS` sang `filterConfig.statusOptions`.

Ví dụ:

```ts
const selectedStatusLabel =
  filterConfig.statusOptions.find((o) => o.value === draftFilters.status)?.label
  ?? 'Tất cả trạng thái';
```

### 6.2. Scope filter

Chỉ render khi:

```tsx
{filterConfig.showScope && (
  // scope filter block
)}
```

Nếu `showScope = false`, không gửi `visitScopes` lên API.

### 6.3. Relation filter

Thêm filter mới:

```text
Loại xử lý
hoặc
Vai trò của tôi
```

State cần thêm:

```ts
const emptyFilters = {
  keyword: '',
  status: '',
  visitScopes: [] as string[],
  relation: '',
  fromDate: '',
  toDate: '',
};
```

Render tương tự dropdown status/scope.

### 6.4. Reset filter khi đổi tab

Khi đổi tab, nên reset filters hoặc re-normalize filters để tránh status không còn hợp lệ ở tab mới.

Khuyến nghị:

```ts
const handleChangeTab = (nextTab: Tab) => {
  if (activeTab === nextTab) return;
  const nextEmptyFilters = createEmptyFilters();
  setActiveTab(nextTab);
  setDraftFilters(nextEmptyFilters);
  setAppliedFilters(nextEmptyFilters);
  setCurrentPage(1);
  loadDelegations(nextTab, 1, pageSize, nextEmptyFilters);
};
```

---

## 7. API params frontend gửi lên backend

Trong `loadDelegations`, khi apply status option:

```ts
const option = filterConfig.statusOptions.find((o) => o.value === targetFilters.status);

if (option?.cancelledOnly) params.cancelledOnly = true;
if (option?.requestStatus) params.requestStatus = option.requestStatus;
if (option?.campusStatus) params.campusStatus = option.campusStatus;
if (option?.visitScopes?.length) params.visitScopes = option.visitScopes.join(',');
if (option?.readOnlyOnly) params.readOnlyOnly = true;
if (option?.actionableOnly) params.actionableOnly = true;
if (option?.timing) params.timing = option.timing;
```

Nếu user chọn scope dropdown:

```ts
if (filterConfig.showScope && targetFilters.visitScopes.length > 0) {
  params.visitScopes = targetFilters.visitScopes.join(',');
}
```

Nếu user chọn relation dropdown:

```ts
if (filterConfig.showRelation && targetFilters.relation) {
  if (targetFilters.relation === 'READ_ONLY') params.readOnlyOnly = true;
  else if (targetFilters.relation === 'ACTION_REQUIRED') params.actionableOnly = true;
  else params.relation = targetFilters.relation;
}
```

Lưu ý nếu status option cũng set `visitScopes`, cần merge với scope dropdown cẩn thận:

```text
- Nếu status option có visitScopes cố định, ưu tiên status option.
- Nếu status option không có visitScopes, dùng scope dropdown.
```

---

## 8. Backend changes

### 8.1. Files cần kiểm tra/sửa

Tìm các file liên quan:

```text
backend/PEMS.Application/Delegations/Queries/ViewGuestDelegationList/ViewGuestDelegationListQuery.cs
backend/PEMS.Application/Delegations/Queries/ViewGuestDelegationList/ViewGuestDelegationListQueryHandler.cs
backend/PEMS.Application/Delegations/Queries/ViewGuestDelegationList/ViewGuestDelegationListQueryValidator.cs
backend/PEMS.Application/Delegations/Queries/ViewGuestDelegationList/ViewGuestDelegationListDto.cs
backend/PEMS.Api/Controllers/DelegationsController.cs
```

Nếu project dùng tên khác như `ViewGuestDelegationListQuery`, `SearchDelegationsQuery`, hãy sửa đúng file đang phục vụ API:

```http
GET /api/delegations/management-list
```

---

### 8.2. Thêm query params nếu chưa có

Query nên hỗ trợ:

```csharp
public string? Tab { get; set; }
public string? Keyword { get; set; }
public string? RequestStatus { get; set; }
public string? CampusStatus { get; set; }
public string? VisitScopes { get; set; }
public string? Relation { get; set; }
public bool? ReadOnlyOnly { get; set; }
public bool? ActionableOnly { get; set; }
public bool? CancelledOnly { get; set; }
public string? Timing { get; set; }
public DateTime? FromDate { get; set; }
public DateTime? ToDate { get; set; }
public int Page { get; set; } = 1;
public int PageSize { get; set; } = 10;
```

---

### 8.3. Backend filter rules

Backend phải enforce role/scope trước, filter params sau.

Order đúng:

```text
1. Resolve current user role/subRole/campus/department.
2. Apply role scope.
3. Apply tab scope.
4. Build CurrentUserRelation / AllowedActions / IsReadOnly.
5. Apply extra filters: requestStatus, campusStatus, visitScopes, relation, readOnlyOnly, actionableOnly, timing, date.
6. Paginate.
7. Return DTO.
```

Không để frontend truyền `relation=HOST` rồi xem dữ liệu người khác.

---

### 8.4. readOnlyOnly/actionableOnly

Backend nên tính `AllowedActions` trước rồi filter:

```csharp
item.IsReadOnly = item.AllowedActions.Count == 1
                  && item.AllowedActions.Contains("VIEW_DETAIL");
```

Hoặc nếu có action xem duy nhất:

```csharp
item.IsReadOnly = !item.AllowedActions.Any(a => a != "VIEW_DETAIL");
```

Filter:

```csharp
if (query.ReadOnlyOnly == true)
{
    items = items.Where(x => x.IsReadOnly).ToList();
}

if (query.ActionableOnly == true)
{
    items = items.Where(x => x.AllowedActions.Any(a => a != "VIEW_DETAIL")).ToList();
}
```

Nếu đang query DB trực tiếp và không muốn materialize quá sớm, có thể lọc theo business conditions tương ứng, nhưng phải đảm bảo kết quả giống `allowedActions`.

---

### 8.5. relation

Backend nên hỗ trợ các relation cơ bản:

```text
HOST
TASK_ASSIGNEE
PARTICIPANT_ACCEPTED
VISITOR_OWNER
HO_MONITOR
STAFF_LEADER_SCOPE
DEPT_SUPPORT
STUDENT_SUPPORT
```

Nếu hiện tại chưa có task module, có thể tạm hỗ trợ các relation đang có dữ liệu:

```text
HOST
PARTICIPANT_ACCEPTED
VISITOR_OWNER
HO_MONITOR
STAFF_LEADER_SCOPE
```

Không fake task relation nếu chưa có schema/task implementation.

---

### 8.6. timing

`timing=UPCOMING` nên lọc theo `planned_start_at` lớn hơn thời điểm hiện tại và đơn chưa hủy/chưa đóng.

Ví dụ:

```text
UPCOMING:
planned_start_at > now
AND request_status NOT IN (REJECTED, CANCELLED)
AND campus_status NOT IN (CANCELLED, CLOSED)

ONGOING:
campus_status = DURING_VISIT

ENDED:
campus_status = CLOSED
```

---

## 9. allowedActions vẫn là nguồn render nút

Không được render nút dựa vào filter config.

Nút action vẫn phải dựa vào:

```ts
row.allowedActions
```

Ví dụ:

```text
HO + SINGLE_CAMPUS:
allowedActions = [VIEW_DETAIL]

HO + MULTI_CAMPUS + PENDING_APPROVAL:
allowedActions = [VIEW_DETAIL, HO_APPROVE, HO_REJECT]

Staff Leader + SINGLE_CAMPUS + PENDING_APPROVAL:
allowedActions = [VIEW_DETAIL, APPROVE_AND_ASSIGN_HOST, CAMPUS_REJECT]

Staff Host:
allowedActions = [VIEW_DETAIL, PREPARE_VISIT, CANCEL_BY_HOST]
```

Frontend filter chỉ giúp chọn list, không quyết định quyền.

---

## 10. UI/UX requirements

### 10.1. Không tràn ngang

Filter bar phải responsive, không gây horizontal scroll toàn trang.

Có thể dùng grid động:

```tsx
className="grid grid-cols-1 gap-3 md:grid-cols-2 xl:grid-cols-[minmax(260px,1fr)_180px_170px_170px_210px_112px_44px] xl:items-end w-full"
```

Nhưng nếu `showScope` hoặc `showRelation` false thì layout phải tự co lại hợp lý.

### 10.2. Label thân thiện

Không hiển thị raw enum cho user.

Không hiển thị trực tiếp:

```text
PENDING_APPROVAL
WAITING_REQUEST_APPROVAL
ASSIGNED
BEFORE_VISIT
DURING_VISIT
AFTER_VISIT
CLOSED
```

Phải map sang tiếng Việt theo role.

### 10.3. Khi đổi role/tab

Nếu filter đang chọn giá trị không hợp lệ cho tab/role mới, reset filter.

Ví dụ:

```text
Staff đang ở Tab Đơn phụ trách chọn relation=HOST.
Chuyển sang Tab Đơn mời tham dự thì relation filter không còn.
Phải reset relation.
```

---

## 11. Manual test cases

### 11.1. HO

Login:

```text
ho@fpt.edu.vn
```

Kiểm tra:

```text
- Có filter Scope: Tất cả phạm vi / Liên cơ sở / Đơn cơ sở.
- Có filter Loại xử lý: Tất cả / Cần xử lý / Chỉ theo dõi.
- Chọn Cần xử lý → chỉ thấy đơn có action HO_APPROVE/HO_REJECT.
- Chọn Chỉ theo dõi → thấy SINGLE_CAMPUS read-only hoặc đơn không có action xử lý.
- Chọn Đơn cơ sở → thấy SINGLE_CAMPUS, chỉ có nút xem.
- Chọn Liên cơ sở + Cần HO duyệt → thấy MULTI_CAMPUS pending.
```

### 11.2. Staff Leader HN

Login:

```text
staff.leader.hn@fpt.edu.vn
```

Kiểm tra:

```text
- Không thấy Tab Đơn mời tham dự.
- Scope filter ghi rõ “Tất cả trong campus tôi”.
- Có filter Chờ duyệt tại campus.
- Có filter Đã phân công Host.
- Không thấy filter Cần HO duyệt.
- Không thấy dữ liệu campus khác.
```

### 11.3. Staff thường HN

Login:

```text
staff.hn@fpt.edu.vn
```

Tab Đơn phụ trách:

```text
- Không thấy scope filter.
- Có filter Vai trò của tôi: Tôi là Host / Tôi được giao việc.
- Không thấy filter Chờ duyệt.
```

Tab Đơn mời tham dự:

```text
- Chỉ có filter đơn giản: Sắp diễn ra / Đang diễn ra / Đã kết thúc / Đã hủy.
- Không có action Accept/Decline trong tab này.
```

### 11.4. Department Lead / Department Staff

Login:

```text
dept.leader.hn@fpt.edu.vn
dept.hn@fpt.edu.vn
```

Kiểm tra:

```text
- Tab Đơn mời tham dự dùng filter participant đơn giản.
- Không thấy filter Gán host / Chờ HO duyệt.
- Nếu có Tab Đơn phụ trách cho Department task, filter phải dùng task status riêng hoặc tạm không hiển thị status task nếu backend chưa hỗ trợ.
```

### 11.5. Student

Login:

```text
student@fpt.edu.vn
```

Kiểm tra:

```text
- Filter đơn giản.
- Không thấy scope filter.
- Không thấy Chờ duyệt / Gán host / Cần HO duyệt.
```

### 11.6. Visitor

Login:

```text
visitor@example.com
```

Kiểm tra:

```text
- Không có 2 tab.
- Header là Đơn của tôi.
- Status label thân thiện: Đã gửi, chờ xử lý / Đã được duyệt / Đang tiếp khách / Đã hoàn tất / Bị từ chối / Đã hủy.
- Không thấy scope filter nếu không cần.
```

### 11.7. Admin

Login:

```text
admin@fpt.edu.vn
```

Kiểm tra:

```text
- Không có filter/list nghiệp vụ.
- Hiển thị empty state Admin không tham gia luồng tiếp khách.
```

---

## 12. Build/test commands

Sau khi sửa, chạy:

### Frontend

```bash
cd frontend/pems-react
npm run build
```

Nếu có script thì chạy thêm:

```bash
npm run lint
npm run typecheck
```

Nếu không có script, ghi rõ trong report.

### Backend

```bash
dotnet build backend/PEMS.Api/PEMS.Api.csproj
```

Nếu có test project chạy được:

```bash
dotnet test
```

Không báo pass giả nếu chưa chạy.

---

## 13. Output/report sau khi sửa

Báo cáo theo format:

```text
1. Summary
2. Files changed
3. Frontend changes
4. Backend changes
5. API params/contract
6. Role-based filter behavior
7. Manual test cases
8. Build/test result
9. Known limitations
10. TODO / cần xác nhận
```

Trong report phải nói rõ:

```text
- Role nào thấy filter nào.
- Những filter nào bị ẩn theo role.
- Backend có hỗ trợ readOnlyOnly/actionableOnly/relation/timing chưa.
- Nếu backend chưa hỗ trợ relation nào thì ghi limitation.
```

---

## 14. Quy tắc không được vi phạm

Không được:

```text
- Không rewrite toàn bộ VisitRequestManagement.
- Không bỏ allowedActions[].
- Không để frontend quyết định quyền cuối cùng.
- Không hiện cùng bộ filter cho mọi role.
- Không hiển thị raw enum cho Visitor/Student.
- Không cho HO mutation SINGLE_CAMPUS.
- Không cho Staff thường thấy filter duyệt đơn.
- Không cho Admin vào list nghiệp vụ.
- Không dùng mock data nếu backend đã có API thật.
- Không làm layout filter tràn ngang.
```

