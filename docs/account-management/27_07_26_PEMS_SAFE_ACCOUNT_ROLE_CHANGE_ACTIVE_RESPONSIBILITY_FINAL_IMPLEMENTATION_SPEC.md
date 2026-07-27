# PEMS — ĐẶC TẢ TRIỂN KHAI HOÀN CHỈNH  
## Ràng buộc an toàn khi STAFF LEADER thay đổi role của tài khoản

---

## 0. Mục đích tài liệu

Tài liệu này là prompt bàn giao tự chứa dành cho AI Agent triển khai chức năng:

> STAFF LEADER chỉ được thay đổi role của một tài khoản nội bộ thuộc phạm vi quản lý khi việc thay đổi đó không làm tài khoản mất quyền trong lúc vẫn đang giữ trách nhiệm ở một đoàn khách đang hoạt động.

Tài liệu được tổng hợp sau khi đối chiếu:

- Codebase PEMS hiện tại trên nhánh `Dev`.
- Handler `UpdateAccountRole`.
- Các flow Host, Coordinator, Participant, Logistics và Department Head.
- Database baseline mới nhất:
  - `PEMS_FULL_V2_NO_SEED_DATA_GALLERY_DOCUMENT_AI_FIXED.sql`
- Đặc tả cũ:
  - `25_07_PEMS_SAFE_ACCOUNT_ROLE_CHANGE_ACTIVE_VISIT_DEPENDENCY_IMPLEMENTATION_SPEC(1).md`

AI Agent phải đọc code thật tại HEAD trước khi sửa. Không được tin tuyệt đối tên file, hash commit hoặc hành vi cũ nếu code đã thay đổi.

---

# 1. Mục tiêu nghiệp vụ

Khi STAFF LEADER đổi role của một tài khoản giữa:

```text
STAFF + STAFF
DEPARTMENT + LEADER
STUDENT + NULL
```

backend phải kiểm tra xem tài khoản đó còn giữ trách nhiệm đang hoạt động nào trong hệ thống tiếp khách hay không.

Nếu còn trách nhiệm:

- Từ chối thay đổi role.
- Trả lỗi nghiệp vụ rõ ràng.
- Không tự động chuyển giao.
- Không tự động gỡ trách nhiệm.
- Không tạo role tạm.
- Không giữ quyền role cũ.
- Không tạo dual-role.
- Không để lại partial update.

Chỉ cho đổi role khi toàn bộ trách nhiệm đã được xử lý hợp lệ bằng đúng flow nghiệp vụ.

---

# 2. Phạm vi caller

Chức năng này chỉ áp dụng cho caller:

```text
role_code = STAFF
sub_role  = LEADER
```

Tức STAFF LEADER của campus.

Bắt buộc giữ các rule:

- Không được đổi role của chính mình.
- Không được quản lý tài khoản campus khác.
- Không được đổi role tài khoản `LOCKED`.
- Không tin dữ liệu campus gửi từ frontend.
- Campus scope phải được xác định từ authenticated caller.
- Backend phải chặn direct API call ngoài phạm vi, không chỉ ẩn nút trên giao diện.

---

# 3. Phạm vi target được phép quản lý

## 3.1. Role hiện tại hợp lệ

STAFF LEADER chỉ được đổi role nếu target hiện tại thuộc đúng một trong ba shape:

```text
STAFF + STAFF
DEPARTMENT + LEADER
STUDENT + NULL
```

## 3.2. Role mới hợp lệ

Role mới cũng chỉ được thuộc đúng ba shape:

```text
STAFF + STAFF
DEPARTMENT + LEADER
STUDENT + NULL
```

## 3.3. Các target bị cấm

Không cho STAFF LEADER đổi role của:

```text
ADMIN
HO
VISITOR
STAFF + LEADER
DEPARTMENT + STAFF
Tài khoản campus khác
Chính STAFF LEADER đang đăng nhập
Tài khoản LOCKED
```

## 3.4. Visitor

Visitor trên trang quản lý tài khoản hiện là chế độ chỉ đọc.

Không cho phép:

- Đổi Visitor thành STAFF.
- Đổi Visitor thành DEPARTMENT LEADER.
- Đổi Visitor thành STUDENT.
- Dùng direct API để bypass frontend.

Nếu cần chức năng Visitor → internal account trong tương lai, phải xây một flow riêng vì còn liên quan đến:

- `visitor_user_id`
- quyền sở hữu đầu mối chính
- `primary_contact_access_status`
- đơn tiếp khách đang hoạt động
- trigger database bảo vệ Visitor
- auth provider
- chuyển quyền sở hữu đơn

Không đưa flow đó vào task này.

## 3.5. Error code đề xuất

Bổ sung:

```text
ACCOUNT_ROLE_TARGET_NOT_MANAGEABLE
```

HTTP đề xuất:

```text
403 Forbidden
```

---

# 4. Các phép chuyển role hợp lệ

| Role hiện tại | Role mới |
|---|---|
| `STAFF + STAFF` | `STUDENT + NULL` |
| `STAFF + STAFF` | `DEPARTMENT + LEADER` |
| `STUDENT + NULL` | `STAFF + STAFF` |
| `STUDENT + NULL` | `DEPARTMENT + LEADER` |
| `DEPARTMENT + LEADER` | `STAFF + STAFF` |
| `DEPARTMENT + LEADER` | `STUDENT + NULL` |
| `DEPARTMENT + LEADER` | `DEPARTMENT + LEADER` ở department khác |

No-op hoặc chỉ sửa identity vẫn dùng cùng endpoint nhưng không được coi là structural role change.

---

# 5. Những điều hệ thống không được tự động làm

Khi phát hiện blocker, chỉ từ chối request.

Không được tự động:

- Gỡ Host.
- Chuyển Host.
- Gỡ Coordinator.
- Chuyển Coordinator.
- Hủy lời mời.
- Xóa participant.
- Đổi participant role.
- Gỡ người phụ trách agenda.
- Chuyển logistics assignee.
- Clear `departments.head_user_id`.
- Chọn Trưởng phòng thay thế.
- Giữ quyền role cũ sau khi đổi role.
- Tạo dual-role.
- Tạo pending role change.
- Tạo scheduled role change.
- Tạo background job đổi role.
- Xóa dữ liệu lịch sử.
- Sửa dữ liệu đoàn đã đóng.
- Xóa audit cũ.
- Xóa participant lịch sử.
- Xóa logistics lịch sử.

---

# 6. Phân loại loại thay đổi

Backend phải tính rõ:

```text
hasStructuralChange
hasIdentityChange
hasStudentCodeChange
hasAnyChange
```

## 6.1. Structural change

`hasStructuralChange = true` khi có ít nhất một thay đổi:

```text
oldRoleCode        != newRoleCode
oldSubRole         != newSubRole
oldDepartmentId    != newDepartmentId
oldPrimaryCampusId != newPrimaryCampusId
```

Chỉ khi `hasStructuralChange = true` mới chạy dependency checker.

## 6.2. Identity-only

Không chạy dependency blocker khi chỉ:

- Sửa họ tên.
- Sửa email.
- Giữ nguyên role/sub-role/department/campus.

Tuy nhiên vẫn phải giữ validation identity hiện tại.

## 6.3. Student code only

Không chạy dependency blocker khi:

- Target vẫn là `STUDENT`.
- Chỉ thay MSSV.
- Role/sub-role/department/campus giữ nguyên.

Vẫn phải kiểm tra:

- MSSV không rỗng.
- MSSV tối đa 30 ký tự.
- MSSV không trùng.

## 6.4. Pure no-op

Nếu request không tạo ra thay đổi thật:

```text
hasAnyChange = false
```

Thì:

- Không ghi audit.
- Không cập nhật `UpdatedAt`.
- Không revoke session.
- Không gửi email.
- Không cập nhật department.
- Không chạy dependency checker.
- Có thể trả current state như no-op success.

Frontend vẫn nên disable nút lưu khi không dirty.

---

# 7. Trạng thái visit instance dùng cho blocker

Tạo allowlist riêng cho Account Role Change:

```text
WAITING_REQUEST_APPROVAL
ASSIGNED
BEFORE_VISIT
DURING_VISIT
AFTER_VISIT
```

Không block khi:

```text
CLOSED
CANCELLED
REJECTED
```

Không dùng aggregate status của `visit_requests`.

Phải dùng trạng thái tại:

```text
visit_request_campuses.status
```

Không dùng trực tiếp allowlist của UC-106 vì UC-106 cố ý loại `WAITING_REQUEST_APPROVAL`.

Role-change checker phải fail-closed cả với dữ liệu bất thường có responsibility trên `WAITING_REQUEST_APPROVAL`.

---

# 8. Sáu nhóm blocker bắt buộc

---

## 8.1. ACTIVE_HOST_ASSIGNMENTS

Điều kiện:

```text
visit_request_campuses.current_host_user_id = targetUserId
AND visit_request_campuses.status thuộc active visit allowlist
```

Ý nghĩa:

- Target đang là Host chính của campus instance.
- Không được đổi role trước khi:
  - chuyển Host bằng flow hợp lệ; hoặc
  - đoàn kết thúc; hoặc
  - đoàn bị hủy/từ chối hợp lệ.

Dữ liệu blocker nên có:

```text
type
count
affectedVisitCount
sampleVisitInstanceIds
message
```

---

## 8.2. ACTIVE_COORDINATOR_ASSIGNMENTS

Điều kiện:

```text
visit_request_campuses.coordinator_user_id = targetUserId
AND visit_request_campuses.status thuộc active visit allowlist
```

Target phải được thay Coordinator hoặc đoàn phải kết thúc trước khi đổi role.

---

## 8.3. PENDING_PARTICIPANT_INVITATIONS

Điều kiện:

```text
visit_participants.user_id = targetUserId
AND visit_participants.status = INVITED
AND visit instance thuộc active visit allowlist
```

Người quản lý phải:

- Gỡ lời mời bằng flow hợp lệ; hoặc
- Hủy lời mời; hoặc
- Đợi target phản hồi.

Không tự động chuyển `INVITED` thành `REMOVED`.

---

## 8.4. ACTIVE_VISIT_PARTICIPATIONS

Điều kiện:

```text
visit_participants.user_id = targetUserId
AND visit_participants.status IN (ACCEPTED, ASSIGNED)
AND visit instance thuộc active visit allowlist
```

Áp dụng cho:

```text
IC_SUPPORT
DEPT_SUPPORT
STUDENT
```

Có thể fail-closed với dữ liệu bất thường khác.

### Tránh đếm Host hai lần

Khi Host được gán, hệ thống hiện thường đồng thời có:

```text
visit_request_campuses.current_host_user_id = target
```

và một participant row:

```text
participant_role = IC_HOST
status           = ASSIGNED
is_host          = true
```

Không được tạo hai blocker cho cùng một trách nhiệm.

Không tính participant row vào `ACTIVE_VISIT_PARTICIPATIONS` khi:

```text
participant_role = IC_HOST
AND visit_request_campuses.current_host_user_id = targetUserId
```

Nếu có participant `IC_HOST` nhưng `current_host_user_id` không còn trỏ target:

- Coi là dữ liệu bất thường.
- Vẫn block fail-closed.
- Không tự sửa dữ liệu trong task này.

### 8.4b. Trưởng phòng đã bàn giao việc xuống nhân viên

Khi Department Leader phân công một nhân viên tiếp nhận đoàn khách (`AssignDepartmentStaff`), dòng participant của chính Trưởng phòng bị đặt về `ASSIGNED` — enum participant không có trạng thái `DELEGATED` nào để ghi việc bàn giao. Vì vậy chỉ nhìn dòng đó thì không phân biệt được "đã bàn giao" với "đích thân tham gia".

Dấu hiệu phân biệt là dòng của người thay thế. Không tính dòng của target vào `ACTIVE_VISIT_PARTICIPATIONS` khi:

```text
participant_role = DEPT_SUPPORT
AND status       = ASSIGNED
AND tồn tại participant row khác trên CÙNG visit instance với:
      user_id       <> targetUserId
      assigned_by   =  targetUserId
      participant_role = DEPT_SUPPORT
      status        IN (ASSIGNED, ACCEPTED)
```

Hai giới hạn cố ý:

- Miễn trừ **chết theo người thay thế**. Nhân viên `DECLINED` hoặc bị `REMOVED` thì trách nhiệm dội về Trưởng phòng và blocker sống lại — đúng như màn "Phân công và tiến độ" lúc đó cho Trưởng phòng phân công lại.
- Chỉ áp cho status `ASSIGNED`. Trưởng phòng sau khi bàn giao vẫn có thể tự nhận lời mời của mình (`ACCEPTED`) — khi đó họ đang đích thân tham gia và phải block bình thường.

Tiêu chí này trùng với cách `GetAssignmentsProgressList` chọn "Người phụ trách" hiển thị, nên bộ đếm trách nhiệm và màn hình tiếp đón luôn nói cùng một điều về việc ai đang giữ việc.

> Lưu ý: rule UC-106 (chặn vô hiệu hóa phòng ban) **không** áp miễn trừ này — ở đó nhân viên thay thế cũng thuộc chính phòng ban đang bị vô hiệu hóa, nên dòng của họ vẫn block. Hai rule khác nhau là đúng, không phải drift.

---

## 8.5. ACTIVE_LOGISTICS_RESPONSIBILITIES

### Trường hợp chắc chắn phải block

```text
visit_logistics_items.assigned_to_user_id = targetUserId
AND logistics status thuộc active logistics allowlist
AND visit instance thuộc active visit allowlist
```

Active logistics statuses:

```text
REQUESTED
CHANGE_PROPOSED
ASSIGNED
ACCEPTED
IN_PROGRESS
```

Không block:

```text
DONE
REJECTED
DECLINED
CANCELLED
```

### Quy tắc `received_by`

Không block mọi item chỉ vì target từng là `received_by`.

Chỉ block khi:

```text
received_by = targetUserId
AND assigned_to_user_id IS NULL
AND status IN (REQUESTED, CHANGE_PROPOSED)
AND visit instance thuộc active visit allowlist
```

Khi item đã được giao cho người khác:

```text
assigned_to_user_id IS NOT NULL
AND assigned_to_user_id != targetUserId
```

thì `received_by` được coi là lịch sử tiếp nhận, không phải trách nhiệm cá nhân đang giữ role.

Khi target tự nhận xử lý và cả hai field cùng là target:

```text
received_by = targetUserId
assigned_to_user_id = targetUserId
```

thì block qua `assigned_to_user_id`.

Một logistics item chỉ được đếm một lần.

Không block chỉ vì target từng là:

```text
requested_by
assigned_by
proposed_by
proposal_responded_by
created_by
updated_by
```

---

## 8.6. DEPARTMENT_HEAD_ASSIGNMENT

Áp dụng khi target hiện tại là:

```text
DEPARTMENT + LEADER
```

và structural change làm target:

- Rời role Department Leader; hoặc
- Chuyển sang department khác.

Block khi:

```text
departments.head_user_id = targetUserId
```

Message:

```text
Tài khoản hiện đang là Trưởng phòng của {departmentName}.
Vui lòng chỉ định Trưởng phòng thay thế trước khi thay đổi vai trò hoặc phòng ban.
```

Không được giữ logic cũ:

```text
oldDepartment.HeadUserId = null
```

trong `UpdateAccountRole`.

### Bàn giao nằm TRONG chính thao tác đổi vai trò

Không dùng flow `ReassignDepartmentLead` riêng làm bước trước. Lý do là một vòng chết: command đó hạ trưởng phòng cũ xuống `DEPARTMENT + STAFF` — shape mà Staff Leader **không được phép quản lý** (Mục 3.3) — nên chính thao tác đổi vai trò mà nó dọn đường cho lại trở thành bất khả thi. Tài khoản kẹt lại, chỉ ADMIN/HO mới gỡ được.

Thay vào đó, `UpdateAccountRoleCommand` nhận thêm:

```text
replacementDepartmentHeadUserId  (ulong?, optional)
```

Quy tắc:

- Bắt buộc khi structural change làm target rời ghế trưởng phòng (đúng điều kiện block ở trên). Thiếu → vẫn trả blocker `DEPARTMENT_HEAD_ASSIGNMENT` như cũ.
- Gửi kèm khi thay đổi KHÔNG làm rời ghế → 422 `INVALID_DEPARTMENT_HEAD_REPLACEMENT`. Không im lặng bỏ qua.
- Người thay thế phải là `DEPARTMENT + STAFF`, `ACTIVE`, thuộc đúng phòng ban đang bàn giao và đúng cơ sở của phòng ban đó. Sai bất kỳ điều kiện nào → 422 cùng mã.
- Khóa hàng: cả target lẫn người thay thế được khóa trong **một lời gọi** `LockUsersAsync` ở đầu handler (service tự sắp xếp id tăng dần), rồi mới tới khóa `departments` — giữ nguyên thứ tự users→departments của Mục 13.4.

Trình tự trong transaction:

```text
lock users {target, replacement} → lock departments → bàn giao (head_user_id + sub_role) → SaveChanges
  → dependency check (đọc thấy head MỚI nên blocker này không bắn) → các validate còn lại → đổi role → commit
```

Bàn giao được flush trước bước kiểm tra để bộ kiểm tra đọc đúng head mới; nếu một blocker khác từ chối ngay sau đó, transaction rollback kéo theo cả bàn giao — phòng ban giữ nguyên trưởng phòng cũ (Mục 21).

Sau khi head đã được thay:

- Chỉ blocker Department Head được loại bỏ.
- Các blocker Host/Coordinator/Participant/Logistics vẫn phải kiểm tra.

Frontend: `GET /accounts/role-assignment-options` trả thêm `headedDepartment` (phòng ban target đang đứng đầu + danh sách nhân sự đủ điều kiện kế nhiệm), để modal hỏi người thay thế ngay trong cùng bước, thay vì báo lỗi rồi đẩy người dùng sang một luồng khác.

---

# 9. Agenda và trách nhiệm phụ

`visit_agendas.responsible_user_id` hiện được gán cho:

- Host; hoặc
- Participant `ACCEPTED`.

Vì vậy bình thường agenda responsibility đã được bao phủ bởi:

```text
ACTIVE_HOST_ASSIGNMENTS
hoặc
ACTIVE_VISIT_PARTICIPATIONS
```

Không cần tạo blocker agenda riêng ngay từ đầu.

Tuy nhiên phải audit flow gỡ participant:

- Nếu participant bị remove nhưng agenda vẫn trỏ người đó:
  - flow remove participant phải clear/reassign agenda; hoặc
  - role-change checker phải block orphan agenda như data anomaly.

Không mở rộng sang minute/news/photo nếu không có direct role-dependent responsibility chưa được bao phủ.

---

# 10. Thiết kế backend đề xuất

## 10.1. Class mới

Đề xuất tạo:

```text
backend/PEMS.Application/Accounts/Common/
├── AccountRoleChangeDependencyChecker.cs
├── AccountRoleChangeDependencyRule.cs
├── AccountRoleChangeCandidate.cs
├── AccountRoleChangeImpact.cs
└── AccountRoleChangeBlocker.cs
```

## 10.2. Blocker model

Ví dụ:

```csharp
internal sealed class AccountRoleChangeBlocker
{
    public string Type { get; init; } = default!;
    public int Count { get; init; }
    public int AffectedVisitCount { get; init; }
    public IReadOnlyList<ulong> SampleVisitInstanceIds { get; init; }
        = Array.Empty<ulong>();
    public string Message { get; init; } = default!;
}
```

## 10.3. Impact model

```csharp
internal sealed class AccountRoleChangeImpact
{
    public IReadOnlyList<AccountRoleChangeBlocker> Blockers { get; init; }
        = Array.Empty<AccountRoleChangeBlocker>();

    public bool CanChangeRole => Blockers.Count == 0;

    public int AffectedVisitCount { get; init; }
}
```

## 10.4. Checker signature

Ví dụ:

```csharp
internal static class AccountRoleChangeDependencyChecker
{
    public static Task<AccountRoleChangeImpact> CheckAsync(
        IApplicationDbContext db,
        ulong targetUserId,
        string oldRoleCode,
        string? oldSubRole,
        ulong? oldDepartmentId,
        AccountProvisioningRules.ResolvedShape newShape,
        CancellationToken cancellationToken);
}
```

Tên class có thể đổi theo convention hiện tại.

---

# 11. Error codes và error response

## 11.1. Bổ sung vào AccountErrorCodes

```text
ACCOUNT_ROLE_TARGET_NOT_MANAGEABLE
ACCOUNT_ROLE_CHANGE_BLOCKED_BY_ACTIVE_RESPONSIBILITIES
```

## 11.2. HTTP status

Khi còn blocker:

```text
409 Conflict
```

## 11.3. Không sửa middleware

Dùng `ConflictException` hiện có.

Structured payload hiện phải nằm dưới:

```text
data
```

không phải `details`.

## 11.4. Response chuẩn

```json
{
  "success": false,
  "errorCode": "ACCOUNT_ROLE_CHANGE_BLOCKED_BY_ACTIVE_RESPONSIBILITIES",
  "message": "Không thể thay đổi vai trò vì tài khoản còn trách nhiệm đang hoạt động.",
  "data": {
    "affectedVisitCount": 3,
    "blockers": [
      {
        "type": "ACTIVE_HOST_ASSIGNMENTS",
        "count": 1,
        "affectedVisitCount": 1,
        "sampleVisitInstanceIds": [5001]
      },
      {
        "type": "ACTIVE_LOGISTICS_RESPONSIBILITIES",
        "count": 2,
        "affectedVisitCount": 2,
        "sampleVisitInstanceIds": [5002, 5003]
      }
    ]
  }
}
```

## 11.5. Message tổng hợp

Ví dụ:

```text
Không thể thay đổi vai trò vì tài khoản còn trách nhiệm đang hoạt động:
- Host: 1 đoàn
- Lời mời đang chờ phản hồi: 2
- Lượt tham gia hỗ trợ: 1
- Nhiệm vụ hậu cần: 2
- Trưởng phòng hiện tại: Phòng Công tác Sinh viên

Vui lòng chuyển giao, hoàn tất hoặc hủy hợp lệ toàn bộ trách nhiệm trước khi thử lại.
```

Không đưa PII không cần thiết vào response.

---

# 12. Refactor UpdateAccountRoleCommandHandler

## 12.1. Trình tự bắt buộc

```text
1. Xác thực caller.
2. Begin transaction.
3. Lock target user row.
4. Re-read target + current role từ DB sau lock.
5. Validate caller/target scope.
6. Validate target current role nằm trong managed set.
7. Snapshot dữ liệu cũ.
8. Normalize FullName/Email vào biến local.
9. Resolve new role shape.
10. Resolve StudentCode vào biến local.
11. Tính các loại change.
12. Nếu pure no-op → return không side effect.
13. Nếu structural change:
    - lock department liên quan;
    - chạy dependency checker;
    - có blocker → throw 409.
14. Validate:
    - email uniqueness;
    - student_code uniqueness;
    - IC department;
    - GENERAL department;
    - department head conflict.
15. Chỉ sau khi pass mới mutate entity.
16. Ghi audit.
17. SaveChanges.
18. Commit transaction.
19. Sau commit:
    - revoke sessions;
    - gửi email non-fatal.
20. Return success.
```

## 12.2. Không mutate entity trước khi check hoàn tất

Không gán sớm:

```text
user.FullName
user.Email
user.RoleId
user.SubRole
user.DepartmentId
user.PrimaryCampusId
user.StudentCode
```

Dùng biến local:

```text
requestedFullName
requestedEmail
resolvedStudentCode
resolvedShape
```

Chỉ apply sau toàn bộ validation và dependency check.

## 12.3. Không chạm department khi identity-only

Nếu target vẫn là:

```text
DEPARTMENT + LEADER
cùng department
```

và chỉ sửa name/email:

- Không set lại `HeadUserId`.
- Không update `department.UpdatedAt`.
- Không update `department.UpdatedBy`.

## 12.4. Department transition

Chỉ chạm department khi có structural change thật.

- Trở thành Department Leader:
  - department phải GENERAL.
  - department ACTIVE.
  - cùng campus.
  - chưa có head khác.
  - target không còn blocker.
- Rời Department Leader:
  - phải được thay head trước.
  - không tự clear.
- Đổi sang department khác:
  - phải hết blocker head cũ.
  - department mới phải hợp lệ.
  - gán head mới trong cùng transaction.

## 12.5. Post-commit side effects

Giữ đúng thứ tự:

```text
DB commit
→ revoke sessions
→ gửi email non-fatal
```

Không thêm notification trong task này nếu handler hiện chưa có.

---

# 13. Chống race condition

Chỉ check dependency hai lần là chưa đủ.

Phải dùng shared locking protocol.

## 13.1. Abstraction đề xuất

```text
backend/PEMS.Application/Common/Interfaces/
└── IUserMutationLockService.cs
```

Infrastructure:

```text
backend/PEMS.Infrastructure/
└── MySqlUserMutationLockService.cs
```

Có thể dùng tên khác theo convention.

## 13.2. API đề xuất

```csharp
Task LockUsersAsync(
    IReadOnlyCollection<ulong> userIds,
    CancellationToken cancellationToken);

Task LockDepartmentsAsync(
    IReadOnlyCollection<ulong> departmentIds,
    CancellationToken cancellationToken);
```

## 13.3. SQL lock

```sql
SELECT user_id
FROM users
WHERE user_id IN (...)
ORDER BY user_id
FOR UPDATE;
```

Department:

```sql
SELECT department_id
FROM departments
WHERE department_id IN (...)
ORDER BY department_id
FOR UPDATE;
```

## 13.4. Quy tắc lock

Khi lock nhiều user:

- Sort ID tăng dần.
- Lock theo cùng thứ tự ở mọi handler.

Khi lock nhiều department:

- Sort ID tăng dần.
- Lock theo cùng thứ tự ở mọi handler.

## 13.5. Role change

```text
Begin transaction
→ lock target user
→ re-read current role/status/campus
→ check dependencies
→ mutate
→ commit
```

## 13.6. Flow tạo dependency

Mọi flow tạo responsibility mới phải:

```text
Begin transaction
→ lock target user
→ re-read role/status/campus/department
→ validate eligibility
→ create dependency
→ commit
```

Kết quả:

- Nếu role change lock trước:
  - assignment flow chờ.
  - sau commit thấy role mới.
  - assignment bị từ chối.
- Nếu assignment lock trước:
  - role change chờ.
  - sau commit thấy dependency.
  - role change trả 409.

---

# 14. Các flow phải tham gia locking protocol

Phải inventory tất cả nơi ghi vào:

```text
current_host_user_id
coordinator_user_id
visit_participants
assigned_to_user_id
received_by
departments.head_user_id
```

Tối thiểu gồm:

```text
ApproveCampusInstance
TransferVisitHost
CreateVisitRequestV2 direct SELF_HOST/ASSIGN_HOST
InviteVisitParticipant
RespondVisitParticipantInvitation
Các flow assign participant
AssignRequestAssignee
ConfirmRequest
Các flow reassign logistics
ReassignDepartmentLead
UpdateAccountRole
```

Không bỏ sót handler ghi trực tiếp DB.

Các flow chỉ remove/complete dependency có thể không cần lock để đảm bảo correctness; trường hợp tệ nhất role change tạm bị block và người dùng retry.

---

# 15. ReassignDepartmentLead

Phải audit và giữ flow này là cách chính thức để thay Trưởng phòng **khi người cũ vẫn ở lại phòng ban** (bàn giao thuần túy, không kèm đổi vai trò).

> Khi việc thay head là hệ quả của một thao tác đổi vai trò, bàn giao chạy TRONG chính `UpdateAccountRole` (Mục 8.6) chứ không gọi flow này trước — gọi trước sẽ hạ người cũ xuống `DEPARTMENT + STAFF` và đẩy họ ra ngoài phạm vi quản lý của Staff Leader (Mục 3.3), làm chính thao tác đổi vai trò không thực hiện được nữa.

Yêu cầu:

- Caller là STAFF LEADER đúng campus.
- Department type = GENERAL.
- Department ACTIVE.
- Lock department.
- Lock old head và new head theo thứ tự ID.
- New head hợp lệ.
- Không để hai head.
- Không để department mất head giữa transaction.
- Audit old/new head.
- Không partial update.
- Commit nguyên tử.
- Revoke session nếu flow thay đổi role.
- Không tự chuyển các visit/logistics responsibility khác.

Sau khi reassign:

- UpdateAccountRole vẫn phải check blocker còn lại.
- Việc thay head chỉ gỡ `DEPARTMENT_HEAD_ASSIGNMENT`.

---

# 16. Frontend

## 16.1. Không tạo preview endpoint

Không cần API precheck.

Flow:

```text
Bấm Cập nhật
→ backend check
→ blocker thì 409
→ modal giữ nguyên
```

## 16.2. Error mapping

Bổ sung:

```text
ACCOUNT_ROLE_TARGET_NOT_MANAGEABLE
ACCOUNT_ROLE_CHANGE_BLOCKED_BY_ACTIVE_RESPONSIBILITIES
```

## 16.3. Ưu tiên backend message

Hiện error mapper có thể ưu tiên static mapping trước backend message.

Với blocker code:

```text
ACCOUNT_ROLE_CHANGE_BLOCKED_BY_ACTIVE_RESPONSIBILITIES
```

phải ưu tiên:

```text
body.message
```

rồi mới dùng static fallback.

Không để static message làm mất số lượng blocker.

## 16.4. Hành vi khi 409

- Modal/drawer giữ mở.
- Giữ role mới.
- Giữ department.
- Giữ MSSV.
- Giữ họ tên/email.
- Không mutate selectedAccount.
- Không refetch như success.
- Không reset form.
- Không toast success.
- Hiển thị message lỗi.
- Có thể render `data.blockers`.
- Người dùng vẫn đóng modal bằng Hủy/X.

## 16.5. Visitor

Visitor vẫn chỉ có:

```text
Xem chi tiết
```

Không thêm:

- đổi role
- đổi trạng thái
- chuyển internal account

---

# 17. Database

Không thay đổi schema.

Không:

- Thêm bảng.
- Thêm cột.
- Thêm pending role.
- Thêm dual role.
- Sửa enum.
- Sửa FK.
- Xóa dữ liệu lịch sử.
- Sửa trigger nếu chưa có bằng chứng bắt buộc.

Tái sử dụng dữ liệu hiện có.

## 17.1. Index cần kiểm tra bằng EXPLAIN

```text
visit_request_campuses(current_host_user_id, status)
visit_request_campuses(coordinator_user_id, status)
visit_participants(user_id, status)
visit_participants(visit_instance_id)
visit_logistics_items(assigned_to_user_id, status)
departments(head_user_id)
```

Đối với `received_by`, chỉ cân nhắc thêm index sau khi:

- Query thực tế.
- EXPLAIN.
- Có dữ liệu đủ lớn.
- Chứng minh chậm.

Không tự thêm migration chỉ vì dự đoán.

---

# 18. Test unit bắt buộc

## 18.1. Visit status matrix

- `WAITING_REQUEST_APPROVAL` block.
- `ASSIGNED` block.
- `BEFORE_VISIT` block.
- `DURING_VISIT` block.
- `AFTER_VISIT` block.
- `CLOSED` không block.
- `CANCELLED` không block.
- `REJECTED` không block.

## 18.2. Host/Coordinator

- Host active tạo blocker.
- Coordinator active tạo blocker.
- Host closed không block.
- Coordinator cancelled không block.

## 18.3. Participant

- `INVITED` → pending invitation blocker.
- `ACCEPTED` → active participation blocker.
- `ASSIGNED` → active participation blocker.
- `DECLINED` không block.
- `REMOVED` không block.
- Canonical Host participant không đếm hai lần.
- Orphan `IC_HOST` vẫn block fail-closed.

## 18.4. Logistics

- `assigned_to_user_id + REQUESTED` block.
- `assigned_to_user_id + ASSIGNED` block.
- `assigned_to_user_id + ACCEPTED` block.
- `assigned_to_user_id + IN_PROGRESS` block.
- `DONE` không block.
- `REJECTED` không block.
- `DECLINED` không block.
- `CANCELLED` không block.
- `received_by + no assignee + REQUESTED` block.
- `received_by + assignee khác` không block.
- Cùng target ở `received_by` và `assigned_to_user_id` chỉ đếm một item.
- `requested_by` một mình không block.
- `assigned_by` một mình không block.

## 18.5. Department Head

- Head rời role bị block.
- Head đổi phòng bị block.
- Head chỉ sửa identity không block.
- Head giữ nguyên role/phòng không chạm department.

## 18.6. Multi-blocker

- Trả đồng thời tất cả blocker.
- Không throw blocker đầu tiên rồi dừng.
- `affectedVisitCount` distinct đúng.
- Sample ID không trùng.
- Count responsibility không bị double-count.

---

# 19. Handler tests bắt buộc

## 19.1. STAFF + STAFF

- Là Host → đổi Student bị 409.
- Là Host → đổi Department Leader bị 409.
- Là Coordinator → bị 409.
- Có INVITED → bị 409.
- Có ACCEPTED → bị 409.
- Có logistics active → bị 409.
- Chỉ history CLOSED → đổi được.
- Không dependency → đổi Student được.
- Không dependency → đổi Department Leader được.

## 19.2. STUDENT

- Có INVITED → đổi Staff bị 409.
- Có ACCEPTED → đổi Staff bị 409.
- Có ASSIGNED → đổi Department Leader bị 409.
- Participant REMOVED → đổi được.
- Visit CLOSED → đổi được.
- Không dependency → đổi Staff được.
- Không dependency → đổi Department Leader được.

## 19.3. DEPARTMENT + LEADER

- Còn là `head_user_id` → đổi Staff bị 409.
- Còn head → đổi Student bị 409.
- Còn head → đổi phòng bị 409.
- Head đã thay nhưng còn participation → vẫn bị 409.
- Head đã thay nhưng còn logistics → vẫn bị 409.
- Head đã thay và không dependency → đổi Staff được.
- Head đã thay và không dependency → đổi Student được.
- Identity-only được phép.
- Same role/same department không chạy blocker.

---

# 20. Scope tests

- Visitor direct API → 403.
- Department Staff direct API → 403.
- Staff Leader target → 403.
- Admin target → 403.
- HO target → 403.
- Campus khác → 403.
- Self role change → 403.
- Locked target → bị từ chối.
- New role ngoài managed set → bị từ chối.

---

# 21. Side-effect tests khi bị block

Xác nhận:

```text
users.role_id không đổi
users.sub_role không đổi
users.department_id không đổi
users.primary_campus_id không đổi
users.student_code không đổi
users.full_name không đổi
users.email không đổi
departments.head_user_id không đổi
participant không đổi
visit instance không đổi
logistics không đổi
không có audit success
không revoke session
không gửi email
```

Phải mock/record:

- Session service.
- Email service.
- Audit logs.

---

# 22. No-op và identity tests

- Pure no-op không audit.
- Pure no-op không revoke.
- Pure no-op không email.
- Name-only không dependency check.
- Email-only không dependency check.
- StudentCode-only không dependency check.
- Department Leader identity-only không update department.
- Duplicate email vẫn giữ behavior cũ.
- Duplicate MSSV vẫn giữ behavior cũ.
- IC department resolve vẫn giữ.
- GENERAL department validation vẫn giữ.

---

# 23. Integration test concurrency trên MySQL

Không dùng EF InMemory để xác nhận race condition.

Dùng MySQL disposable.

## 23.1. Host trước role change

```text
Transaction A assign Host + commit
Transaction B role change
```

Expected:

```text
Role change → 409
```

## 23.2. Role change lock trước

```text
Transaction A lock user + đổi Staff → Student
Transaction B assign Host
```

Expected:

```text
Transaction B chờ
→ re-read role mới
→ từ chối assign
```

## 23.3. Participant concurrent

Role change và participant invite/accept chạy đồng thời.

Expected:

- Không thể cả hai cùng thành công tạo state không hợp lệ.

## 23.4. Logistics concurrent

Role change và logistics assignment chạy đồng thời.

Expected:

- Một operation thắng.
- Operation còn lại thấy state mới và fail an toàn.

## 23.5. Department head concurrent

ReassignDepartmentLead và role change old head chạy đồng thời.

Expected:

- Không partial.
- Department head nhất quán.
- Old head chỉ đổi role khi reference đã thực sự đổi.

## 23.6. Rollback

Gây lỗi sau dependency check nhưng trước commit.

Expected:

- User không đổi.
- Department không đổi.
- Audit không có.
- Session không revoke.
- Email không gửi.

---

# 24. Frontend tests

- Blocker error code được nhận đúng.
- Backend message được hiển thị.
- Static mapping không che backend message.
- Modal vẫn mở.
- Role mới giữ nguyên.
- Department giữ nguyên.
- MSSV giữ nguyên.
- FullName/Email giữ nguyên.
- selectedAccount không bị mutate.
- Không toast success.
- Không refetch như success.
- Người dùng đóng bằng Hủy/X được.
- Success vẫn đóng modal và refetch.
- Visitor không có action đổi role.
- Target ngoài scope hiển thị lỗi đúng.

---

# 25. File dự kiến thay đổi

## Backend Application

```text
backend/PEMS.Application/Accounts/Common/
├── AccountErrorCodes.cs
├── AccountRoleChangeDependencyChecker.cs
├── AccountRoleChangeDependencyRule.cs
├── AccountRoleChangeCandidate.cs
├── AccountRoleChangeImpact.cs
└── AccountRoleChangeBlocker.cs

backend/PEMS.Application/Accounts/Commands/UpdateAccountRole/
├── UpdateAccountRoleCommand.cs
└── UpdateAccountRoleCommandHandler.cs
```

## Locking

```text
backend/PEMS.Application/Common/Interfaces/
└── IUserMutationLockService.cs

backend/PEMS.Infrastructure/
└── MySqlUserMutationLockService.cs
```

Cần DI registration tương ứng.

## Các flow tạo dependency

Sau khi inventory code thật:

```text
ApproveCampusInstance
TransferVisitHost
CreateVisitRequestV2
InviteVisitParticipant
RespondVisitParticipantInvitation
Các flow assign participant
AssignRequestAssignee
ConfirmRequest
Các flow reassign logistics
ReassignDepartmentLead
```

## Frontend

```text
frontend/pems-react/src/features/account-management/api/
└── accountError.ts

frontend/pems-react/src/pages/dashboard/accounts/
└── AccountManagement.tsx
```

Chỉ sửa type nếu cần đọc `data.blockers`.

## Tests

```text
tests/PEMS.UnitTests/Accounts/UpdateAccountRole/
tests/PEMS.IntegrationTests/Accounts/
frontend account-management unit tests
```

## Không sửa mặc định

```text
SQL schema
ExceptionHandlingMiddleware
API route
accountManagementApi.ts
accountManagement.types.ts
```

---

# 26. Git safety

Trước khi sửa:

```bash
git status --short --branch
git log -10 --oneline --decorate
git rev-parse HEAD
git branch --show-current
```

Bắt buộc:

- Xác nhận đúng branch.
- Đọc code thật tại HEAD.
- Không reset/rebase phá WIP.
- Không checkout branch khác.
- Không merge.
- Không push khi chưa được yêu cầu.
- Không deploy.
- Không chạm production DB.
- Không chạy SQL destructive.
- Không dùng `git add .`.
- Chỉ stage đúng file task.
- Không commit secret/log/cache/build output.

Commit message đề xuất nếu được yêu cầu:

```text
fix(accounts): block unsafe role changes with active responsibilities
```

---

# 27. Verify commands

## Backend

```bash
dotnet build PEMS.slnx
dotnet test tests/PEMS.UnitTests/PEMS.UnitTests.csproj
dotnet test tests/PEMS.ArchitectureTests/PEMS.ArchitectureTests.csproj
dotnet test tests/PEMS.IntegrationTests/PEMS.IntegrationTests.csproj
```

Không dùng production DB.

## Frontend

```bash
cd frontend/pems-react
npm run lint
npm run test:unit
npm run build
```

## Diff review

```bash
git status --short
git diff --stat
git diff --check
git diff -- backend/PEMS.Application/Accounts
git diff -- backend/PEMS.Infrastructure
git diff -- frontend/pems-react/src/features/account-management
git diff -- frontend/pems-react/src/pages/dashboard/accounts
```

---

# 28. Definition of Done

Chỉ báo hoàn thành khi tất cả điều kiện sau đạt:

- STAFF LEADER chỉ quản lý ba role hợp lệ.
- Visitor direct API bị chặn.
- Host active chặn role change.
- Coordinator active chặn role change.
- INVITED participant chặn.
- ACCEPTED/ASSIGNED participant chặn.
- Logistics cá nhân active chặn.
- Department Head phải được thay trước.
- Không tự clear `head_user_id`.
- Host không bị đếm hai lần.
- `received_by` lịch sử không overblock.
- CLOSED/CANCELLED/REJECTED không block.
- DECLINED/REMOVED participant không block.
- DONE/REJECTED/DECLINED/CANCELLED logistics không block.
- Identity-only không bị active responsibility blocker.
- MSSV-only không bị active responsibility blocker.
- Pure no-op không side effect.
- Blocked request trả 409.
- Error code đúng.
- Structured payload nằm dưới `data`.
- Modal giữ nguyên dữ liệu khi lỗi.
- Không partial update.
- Không audit success khi block.
- Không revoke session khi block.
- Không gửi email khi block.
- Transaction + shared row locking được áp dụng thống nhất.
- MySQL concurrency tests pass.
- Unit tests pass.
- Architecture tests pass.
- Integration tests pass.
- Frontend lint/test/build pass.
- Không thay đổi DB schema.
- Không dual-role.
- Không pending-role.
- Không auto-transfer.
- Không auto-remove.

---

# 29. Tóm tắt ngắn cho AI Agent

```text
1. Đọc code HEAD trước khi sửa.
2. STAFF LEADER chỉ đổi target hiện tại thuộc:
   STAFF/STAFF, DEPARTMENT/LEADER, STUDENT.
3. Role mới cũng chỉ thuộc ba shape trên.
4. Visitor không được chuyển thành internal trong flow này.
5. Chỉ chạy dependency checker khi role/sub-role/campus/department thay đổi thật.
6. Block khi target còn:
   - Host active;
   - Coordinator active;
   - Participant INVITED;
   - Participant ACCEPTED/ASSIGNED;
   - Logistics assigned_to_user_id active;
   - received_by chưa giao cho ai và còn REQUESTED/CHANGE_PROPOSED;
   - departments.head_user_id khi rời role/phòng.
7. Không đếm Host participant hai lần.
8. Không tự clear head.
9. Không mutate entity trước khi check pass.
10. Trả 409:
    ACCOUNT_ROLE_CHANGE_BLOCKED_BY_ACTIVE_RESPONSIBILITIES.
11. Structured payload dùng data.
12. Frontend giữ modal và backend message.
13. Dùng transaction + shared user/department row lock.
14. Áp dụng lock cho cả role change và mọi flow tạo dependency.
15. Không sửa schema.
16. Viết unit + MySQL concurrency integration + frontend tests.
17. Chạy build/test đầy đủ.
18. Không push/merge/deploy nếu chưa được yêu cầu.
```
