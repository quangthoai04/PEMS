# UC-106 — BỔ SUNG PARTICIPANT BLOCKER KHI DISABLE PHÒNG BAN
## Phạm vi: Production code cần thiết + Unit Test, chưa triển khai Integration Test

> **Project:** PEMS — Partnership Engagement Management System  
> **Module:** Department Management  
> **Use Case:** UC-106 — Manage Department Status  
> **Primary Actor:** Staff Leader  
> **Runtime Role:** `role_code = STAFF`, `sub_role = LEADER`  
> **Database baseline:** `pems_full_v10_TTS_Gallery_FULL_UPDATED_NOTIFICATIONS_FIXED(6).sql`  
> **Mục tiêu:** Bổ sung lời mời/lượt tham gia visit của người thuộc phòng ban làm blocker khi Staff Leader disable phòng ban  
> **Phạm vi lần này:** Chỉ triển khai production code tối thiểu cần thiết và Unit Test; chưa viết Integration Test

---

# 1. Bối cảnh hiện tại

UC-106 hiện đã có logic:

```text
Staff Leader disable GENERAL department
→ Backend kiểm tra logistics thuộc phòng ban
→ Nếu còn logistics ở 1 trong 5 trạng thái đang xử lý thì block
→ Nếu không có blocker thì tiếp tục disable department
```

Năm trạng thái logistics hiện đang block và phải giữ nguyên:

```text
REQUESTED
CHANGE_PROPOSED
ASSIGNED
ACCEPTED
IN_PROGRESS
```

Các trạng thái logistics không block:

```text
DONE
REJECTED
DECLINED
CANCELLED
```

Phần cần bổ sung:

```text
Ngoài logistics, backend phải kiểm tra lời mời và lượt tham gia visit
của Department Leader/Department Staff thuộc phòng ban sắp bị disable.
```

Nếu còn participant dependency đang có hiệu lực:

```text
→ Block disable department
→ Không đổi departments.status
→ Không cập nhật updated_at/updated_by
→ Không revoke session
→ Không đổi users.status
→ Không đổi visit_participants.status
→ Không tự remove/reassign participant
→ Không ghi audit status-change thành công
```

---

# 2. Quy tắc bắt buộc trước khi code

AI Agent phải đọc source hiện tại trước khi sửa:

```text
1. UC-106 command/handler/validator/controller hiện tại.
2. Logic logistics blocker hiện tại.
3. Blocker model/DTO hiện tại.
4. Entity/DbSet/EF mapping:
   - departments
   - users
   - roles
   - visit_participants
   - visit_request_campuses
5. Constants/enums:
   - role_code
   - sub_role
   - participant_role
   - participant status
   - visit campus status
6. Session revocation flow hiện tại.
7. Audit flow hiện tại.
8. Unit Test hiện có của UC-106.
9. Test infrastructure và mocking convention hiện tại.
```

Không được tự bịa:

- Tên class.
- Tên file.
- Endpoint.
- Entity property.
- Enum.
- Column.
- Foreign key.
- Error code.
- Blocker DTO.
- Repository/service abstraction.
- Transaction behavior.
- Terminal status.

Nếu source khác tài liệu này, phải báo rõ current state và ưu tiên source/schema thật.

---

# 3. Căn cứ database mới nhất

## 3.1 Phòng ban và tài khoản

Phòng ban:

```text
departments.department_type = IC | GENERAL
departments.status = ACTIVE | INACTIVE
```

Tài khoản thuộc phòng ban sử dụng:

```text
users.role_id
users.sub_role = LEADER | STAFF
users.department_id
```

Đối tượng thuộc target department phải xác định bằng:

```text
users.department_id = targetDepartmentId
AND roles.role_code = DEPARTMENT
AND users.sub_role IN (LEADER, STAFF)
```

Không được chỉ dựa vào:

```text
departments.head_user_id
```

vì `head_user_id` chỉ đại diện trưởng phòng, không bao phủ toàn bộ nhân sự.

---

## 3.2 Bảng `visit_participants`

Database hiện tại có các trường chính:

```text
participant_id
visit_instance_id
user_id
participant_role
status
invited_by
invited_at
responded_at
assigned_by
assigned_at
note
created_at
created_by
updated_at
updated_by
```

Participant role hợp lệ:

```text
IC_HOST
IC_SUPPORT
DEPT_SUPPORT
STUDENT
```

Participant status hợp lệ:

```text
INVITED
ACCEPTED
DECLINED
ASSIGNED
REMOVED
```

Quan hệ với phòng ban:

```text
visit_participants.user_id
→ users.user_id
→ users.department_id
```

`visit_participants` không có `department_id` trực tiếp.

Không được tự thêm hoặc giả định:

```text
visit_participants.department_id
```

Database có unique constraint:

```text
UNIQUE (visit_instance_id, user_id)
```

Một user chỉ có tối đa một participant record trong cùng một visit instance, nhưng có thể xuất hiện ở nhiều visit khác nhau.

---

## 3.3 Trạng thái visit instance

`visit_request_campuses.status` gồm:

```text
WAITING_REQUEST_APPROVAL
ASSIGNED
BEFORE_VISIT
DURING_VISIT
AFTER_VISIT
CLOSED
CANCELLED
REJECTED
```

Operational statuses dùng để participant block:

```text
ASSIGNED
BEFORE_VISIT
DURING_VISIT
AFTER_VISIT
```

Terminal statuses không block:

```text
CLOSED
CANCELLED
REJECTED
```

`WAITING_REQUEST_APPROVAL` không phải trạng thái Host invitation canonical vì visit chưa được duyệt và chưa có Host chính thức.

Dependency checker phải dùng explicit allowlist operational statuses, không dùng `NOT IN terminal statuses` một cách máy móc.

---

# 4. Đối tượng participant cần kiểm tra

Một participant chỉ được tính là dependency của target department khi thỏa mãn đầy đủ:

```text
users.department_id = targetDepartmentId
AND roles.role_code = DEPARTMENT
AND users.sub_role IN (LEADER, STAFF)
AND visit_participants.participant_role = DEPT_SUPPORT
```

Bao gồm:

```text
DEPARTMENT + LEADER
DEPARTMENT + STAFF
```

Không block target department bởi:

```text
STAFF + LEADER
STAFF + STAFF
STUDENT
HO
ADMIN
VISITOR
User thuộc department khác
```

Không tính participant role:

```text
IC_HOST
IC_SUPPORT
STUDENT
```

---

# 5. Quy tắc đặc biệt của status `ASSIGNED`

Database hiện tại xác định `ASSIGNED` trong `visit_participants` chỉ phù hợp canonical với:

```text
1. IC_HOST được gán làm Host chính.
2. DEPARTMENT + STAFF được Department Leader phân công nhiệm vụ phòng ban.
```

Các trường hợp invitation thông thường sau khi đồng ý phải dùng `ACCEPTED`, không phải `ASSIGNED`:

```text
STAFF + STAFF với IC_SUPPORT
STUDENT với STUDENT
DEPARTMENT + LEADER với DEPT_SUPPORT
```

Do đó:

## Department Staff

```text
DEPARTMENT + STAFF
participant_role = DEPT_SUPPORT
status = ASSIGNED
```

Là dependency hợp lệ và phải block disable.

## Department Leader

```text
DEPARTMENT + LEADER
participant_role = DEPT_SUPPORT
status = ASSIGNED
```

Không phải dữ liệu canonical.

Tuy nhiên, nếu dữ liệu legacy/anomaly này tồn tại:

```text
- Vẫn coi là blocker để tránh disable nhầm.
- Không âm thầm bỏ qua.
- Có thể ghi data-integrity warning nếu source đã có logging phù hợp.
```

UC-106 không có nhiệm vụ tự sửa anomaly này.

---

# 6. Ma trận participant status

## 6.1 `INVITED` — block

Điều kiện:

```text
participant_role = DEPT_SUPPORT
participant.status = INVITED
visit.status IN (
    ASSIGNED,
    BEFORE_VISIT,
    DURING_VISIT,
    AFTER_VISIT
)
```

Lý do:

- Lời mời còn chờ phản hồi.
- Disable department sẽ revoke session của người nhận.
- Người nhận không thể Accept/Decline.
- Host vẫn thấy lời mời đang chờ.
- Workflow bị treo.

Blocker type:

```text
PENDING_PARTICIPANT_INVITATIONS
```

---

## 6.2 `ACCEPTED` — block

Điều kiện:

```text
participant_role = DEPT_SUPPORT
participant.status = ACCEPTED
visit.status IN (
    ASSIGNED,
    BEFORE_VISIT,
    DURING_VISIT,
    AFTER_VISIT
)
```

Lý do:

- Người dùng đã đồng ý tham gia.
- Host đang dựa vào sự tham gia đó.
- Người tham gia có thể còn nhiệm vụ trước, trong hoặc sau visit.

Blocker type:

```text
ACTIVE_VISIT_PARTICIPATIONS
```

Áp dụng cho cả:

```text
DEPARTMENT + LEADER
DEPARTMENT + STAFF
```

---

## 6.3 `ASSIGNED` — block

Điều kiện:

```text
participant_role = DEPT_SUPPORT
participant.status = ASSIGNED
visit.status IN (
    ASSIGNED,
    BEFORE_VISIT,
    DURING_VISIT,
    AFTER_VISIT
)
```

Với `DEPARTMENT + STAFF`, đây là phân công chính thức và phải block.

Với `DEPARTMENT + LEADER`, nếu dữ liệu anomaly tồn tại thì vẫn block defensively.

Blocker type:

```text
ACTIVE_VISIT_PARTICIPATIONS
```

---

## 6.4 `DECLINED` — không block

```text
participant.status = DECLINED
→ Không còn nghĩa vụ tham gia.
```

---

## 6.5 `REMOVED` — không block

```text
participant.status = REMOVED
→ Người dùng đã được loại khỏi visit.
```

---

# 7. Ma trận visit status

## 7.1 Operational visit statuses — block

```text
ASSIGNED
BEFORE_VISIT
DURING_VISIT
AFTER_VISIT
```

### `ASSIGNED`

Visit đã được duyệt và có Host chính thức.

### `BEFORE_VISIT`

Visit đang chuẩn bị.

### `DURING_VISIT`

Visit đang diễn ra.

### `AFTER_VISIT`

Vẫn chưa đóng hoàn toàn. Có thể còn:

- Minutes.
- Media contribution.
- News contribution.
- Action item.
- Logistics/handover.
- Công việc sau chuyến thăm.

Chỉ `CLOSED` mới được xem là hoàn tất toàn bộ.

---

## 7.2 Terminal visit statuses — không block

```text
CLOSED
CANCELLED
REJECTED
```

Participant trong các visit này chỉ còn ý nghĩa lịch sử.

---

## 7.3 `WAITING_REQUEST_APPROVAL`

Không tạo canonical participant blocker.

Lý do:

```text
WAITING_REQUEST_APPROVAL
→ visit chưa được duyệt
→ chưa có Host chính thức
→ Host invitation chưa phải flow hợp lệ
```

Nếu xuất hiện participant record tại trạng thái này:

```text
- Xem là data anomaly.
- Không tạo business blocker canonical.
- Có thể ghi warning nếu source hỗ trợ.
```

---

# 8. Bảng quyết định cuối cùng

| Participant status | Visit status | Block disable? |
|---|---|---:|
| `INVITED` | `ASSIGNED` | Có |
| `INVITED` | `BEFORE_VISIT` | Có |
| `INVITED` | `DURING_VISIT` | Có |
| `INVITED` | `AFTER_VISIT` | Có |
| `ACCEPTED` | `ASSIGNED` | Có |
| `ACCEPTED` | `BEFORE_VISIT` | Có |
| `ACCEPTED` | `DURING_VISIT` | Có |
| `ACCEPTED` | `AFTER_VISIT` | Có |
| `ASSIGNED` | `ASSIGNED` | Có |
| `ASSIGNED` | `BEFORE_VISIT` | Có |
| `ASSIGNED` | `DURING_VISIT` | Có |
| `ASSIGNED` | `AFTER_VISIT` | Có |
| `DECLINED` | Bất kỳ | Không |
| `REMOVED` | Bất kỳ | Không |
| `INVITED/ACCEPTED/ASSIGNED` | `CLOSED` | Không |
| `INVITED/ACCEPTED/ASSIGNED` | `CANCELLED` | Không |
| `INVITED/ACCEPTED/ASSIGNED` | `REJECTED` | Không |
| Bất kỳ | `WAITING_REQUEST_APPROVAL` | Không phải canonical blocker |

---

# 9. Hai nhóm blocker cần trả về

## 9.1 Lời mời đang chờ phản hồi

```text
type = PENDING_PARTICIPANT_INVITATIONS
```

Response mẫu:

```json
{
  "type": "PENDING_PARTICIPANT_INVITATIONS",
  "count": 2,
  "affectedUserCount": 2,
  "affectedVisitCount": 2,
  "message": "Còn 2 lời mời tham gia đang chờ phản hồi từ nhân sự phòng ban."
}
```

---

## 9.2 Người đã nhận lời hoặc được phân công

```text
type = ACTIVE_VISIT_PARTICIPATIONS
```

Response mẫu:

```json
{
  "type": "ACTIVE_VISIT_PARTICIPATIONS",
  "count": 3,
  "affectedUserCount": 2,
  "affectedVisitCount": 2,
  "message": "Còn 3 lượt tham gia đã được chấp nhận hoặc đang được phân công."
}
```

---

# 10. Quy tắc đếm

Phải phân biệt:

```text
count
→ số participant records/lượt tham gia

affectedUserCount
→ số user distinct

affectedVisitCount
→ số visit_instance_id distinct
```

Ví dụ:

```text
Một user tham gia 3 visit:
count = 3
affectedUserCount = 1
affectedVisitCount = 3
```

Không được hiển thị `3 nhân sự` nếu thực tế chỉ có một user với ba lượt tham gia.

---

# 11. Query logic tham khảo

Đây chỉ là pattern. AI Agent phải đối chiếu entity/property thật trước khi code.

```sql
SELECT
    vp.participant_id,
    vp.visit_instance_id,
    vp.user_id,
    vp.participant_role,
    vp.status AS participant_status,
    vrc.status AS visit_status
FROM visit_participants vp
JOIN users u
    ON u.user_id = vp.user_id
JOIN roles r
    ON r.role_id = u.role_id
JOIN visit_request_campuses vrc
    ON vrc.visit_instance_id = vp.visit_instance_id
WHERE u.department_id = @DepartmentId
  AND r.role_code = 'DEPARTMENT'
  AND u.sub_role IN ('LEADER', 'STAFF')
  AND vp.participant_role = 'DEPT_SUPPORT'
  AND vp.status IN ('INVITED', 'ACCEPTED', 'ASSIGNED')
  AND vrc.status IN (
      'ASSIGNED',
      'BEFORE_VISIT',
      'DURING_VISIT',
      'AFTER_VISIT'
  );
```

Phải kiểm tra:

- Tên entity/DbSet thật.
- Navigation properties.
- Foreign key thật.
- Constant/enum thật.
- Global query filters.
- Soft-delete rule nếu có.
- Async LINQ support trong Unit Test.

Không được copy raw SQL này nếu production code hiện tại dùng EF Core query abstraction.

---

# 12. Luồng backend sau khi bổ sung

```text
1. Resolve current user.
2. Kiểm tra actor là STAFF + LEADER.
3. Load target department.
4. Kiểm tra campus scope.
5. Kiểm tra department_type = GENERAL.
6. Kiểm tra 5 logistics blocker hiện tại.
7. Kiểm tra task/assignment/handover blocker hiện có nếu source đang hỗ trợ.
8. Kiểm tra participant blockers:
   - đúng target department
   - role_code = DEPARTMENT
   - sub_role = LEADER hoặc STAFF
   - participant_role = DEPT_SUPPORT
   - participant.status = INVITED/ACCEPTED/ASSIGNED
   - visit.status = ASSIGNED/BEFORE_VISIT/DURING_VISIT/AFTER_VISIT
9. Tổng hợp blockers.
10. Nếu có bất kỳ blocker:
    - trả business/dependency exception
    - department vẫn ACTIVE
    - updated_at/updated_by giữ nguyên
    - session không revoke
    - users.status không đổi
    - participant.status không đổi
    - không ghi audit status-change thành công
11. Nếu không có blocker:
    - tiếp tục flow disable hiện tại
    - set department INACTIVE
    - revoke session affected users
    - ghi audit
```

Participant check phải chạy trước:

```text
department.Status = INACTIVE
```

và trước khi gọi session revocation.

---

# 13. Tổng hợp logistics và participant blocker

Đề xuất trả đầy đủ toàn bộ blocker trong một lần.

Ví dụ:

```json
{
  "errorCode": "DEPARTMENT_STATUS_BLOCKED_BY_DEPENDENCIES",
  "message": "Không thể ngừng hoạt động phòng ban vì còn nghiệp vụ chưa hoàn tất.",
  "blockers": [
    {
      "type": "OPEN_LOGISTICS_ITEMS",
      "count": 1,
      "message": "Còn 1 yêu cầu hậu cần chưa hoàn tất."
    },
    {
      "type": "PENDING_PARTICIPANT_INVITATIONS",
      "count": 1,
      "affectedUserCount": 1,
      "affectedVisitCount": 1,
      "message": "Còn 1 lời mời tham gia đang chờ phản hồi."
    },
    {
      "type": "ACTIVE_VISIT_PARTICIPATIONS",
      "count": 1,
      "affectedUserCount": 1,
      "affectedVisitCount": 1,
      "message": "Còn 1 lượt tham gia đã được chấp nhận."
    }
  ]
}
```

Nếu contract hiện tại đang fail-fast và chỉ trả một blocker:

```text
- AI Agent phải báo rõ current state.
- Không âm thầm thay đổi API contract lớn.
- Nếu mở rộng aggregate blocker, phải nêu rõ file/DTO/client bị ảnh hưởng.
```

---

# 14. Không tự động xử lý participant

UC-106 không được tự động:

```text
INVITED → REMOVED
ACCEPTED → REMOVED
ASSIGNED → REMOVED
```

Không được:

- Thu hồi lời mời.
- Decline thay người nhận.
- Remove participant.
- Thay người.
- Reassign sang department khác.
- Đóng visit.
- Thay đổi `invited_by`.
- Thay đổi `assigned_by`.
- Ghi audit như thể Host thực hiện.

Phân chia trách nhiệm:

```text
UC-106
→ Quản lý trạng thái phòng ban.

Host/Department Leader
→ Quản lý participant và phân công visit.
```

Khi có blocker:

```text
Host/Department Leader phải xử lý participant trước.
Sau đó Staff Leader thực hiện disable lại department.
```

---

# 15. Error contract

HTTP:

```text
409 Conflict
```

Error code:

```text
DEPARTMENT_STATUS_BLOCKED_BY_DEPENDENCIES
```

Message:

```text
Không thể ngừng hoạt động phòng ban vì còn lời mời,
lượt tham gia hoặc nghiệp vụ chưa hoàn tất.
```

Khi có blocker:

```text
department vẫn ACTIVE
updated_at không đổi
updated_by không đổi
session không revoke
users.status không đổi
participant.status không đổi
audit success không ghi
```

---

# 16. UI behavior đề xuất

## Tiêu đề

```text
Chưa thể ngừng hoạt động phòng ban
```

## Nội dung

```text
Phòng ban vẫn còn nhân sự đang được mời hoặc đang tham gia
các chuyến thăm chưa kết thúc.

- 2 lời mời đang chờ phản hồi.
- 1 lượt tham gia đã được chấp nhận.
- 1 nhân sự đang được phân công.

Vui lòng yêu cầu Host hoặc người phụ trách xử lý lời mời/người tham gia
trước khi ngừng hoạt động phòng ban.
```

Có thể hiển thị:

- Tên người tham gia.
- Tên visit.
- Thời gian.
- Participant status.
- Visit status.
- Host phụ trách nếu actor được phép xem.

Không trả dữ liệu không cần thiết:

- Session ID.
- Token.
- Identity provider data.
- Số điện thoại.
- Dữ liệu nhạy cảm.

Khi có blocker, nút `Ngừng hoạt động` phải bị disable hoặc không cho confirm.

Frontend preview không thay thế backend guard.

---

# 17. Business rules bổ sung

```text
BR-UC106-24
Khi disable department, backend phải kiểm tra visit_participants
của các tài khoản DEPARTMENT thuộc target department.

BR-UC106-25
Chỉ participant_role = DEPT_SUPPORT mới được tính là dependency
của GENERAL department.

BR-UC106-26
INVITED trên visit ASSIGNED, BEFORE_VISIT, DURING_VISIT
hoặc AFTER_VISIT là blocker.

BR-UC106-27
ACCEPTED trên visit ASSIGNED, BEFORE_VISIT, DURING_VISIT
hoặc AFTER_VISIT là blocker.

BR-UC106-28
ASSIGNED của DEPARTMENT + STAFF trên operational visit là blocker.

BR-UC106-29
ASSIGNED không phải trạng thái canonical cho DEPARTMENT + LEADER;
nếu tồn tại vẫn block defensively.

BR-UC106-30
DECLINED và REMOVED không block disable.

BR-UC106-31
Participant thuộc CLOSED, CANCELLED hoặc REJECTED visit
không block disable.

BR-UC106-32
Participant trên WAITING_REQUEST_APPROVAL
không phải canonical Host invitation dependency.

BR-UC106-33
Checker phải dùng explicit operational status allowlist,
không dùng NOT IN terminal một cách máy móc.

BR-UC106-34
UC-106 không tự động thay đổi participant status.

BR-UC106-35
Khi participant blocker tồn tại,
department status không đổi và session không revoke.

BR-UC106-36
Participant của department khác không block target department.

BR-UC106-37
IC_HOST, IC_SUPPORT và STUDENT participant
không block GENERAL department.

BR-UC106-38
Blocker count phải phân biệt participant record,
distinct user và distinct visit.

BR-UC106-39
Participant dependency phải được kiểm tra ở backend,
không chỉ ở frontend hoặc impact preview.
```

---

# 18. Phạm vi triển khai lần này

## Có triển khai

```text
- Production code tối thiểu cần thiết để participant blocker hoạt động.
- Unit Test cho participant dependency checker.
- Unit Test cho UC-106 handler.
- Cập nhật Unit Test cũ nếu kỳ vọng cũ không còn đúng.
```

## Chưa triển khai

```text
- Integration Test.
- API test với MySQL thật.
- WebApplicationFactory test.
- Playwright/E2E.
- Frontend automated test.
- Migration.
- SQL patch.
```

Database hiện tại đã có đủ bảng, field, enum và foreign key cần thiết.

---

# 19. Nguyên tắc Unit Test

Unit Test phải:

- Không kết nối MySQL.
- Không gọi HTTP.
- Không dùng `WebApplicationFactory`.
- Không dùng `TestServer`.
- Không chạy SQL fresh-create.
- Không biến thành Integration Test.
- Không dùng placeholder.
- Không dùng `Assert.True(true)`.
- Không skip test.
- Không chỉ assert exception mà bỏ qua state/side effects.

Ưu tiên pattern test hiện có của project:

```text
- Mock repository/service.
- Mock DbContext nếu project đang dùng.
- Fake async query provider nếu cần.
- Mock participant dependency checker khi test handler riêng.
- Tách checker tests và handler tests.
```

---

# 20. Unit Test cho participant dependency checker

## Nhóm A — Participant status

```text
1. DEPARTMENT + LEADER
   DEPT_SUPPORT
   INVITED
   visit = ASSIGNED
   → PENDING_PARTICIPANT_INVITATIONS.

2. DEPARTMENT + STAFF
   DEPT_SUPPORT
   INVITED
   visit = BEFORE_VISIT
   → block.

3. DEPARTMENT + LEADER
   DEPT_SUPPORT
   ACCEPTED
   visit = BEFORE_VISIT
   → ACTIVE_VISIT_PARTICIPATIONS.

4. DEPARTMENT + STAFF
   DEPT_SUPPORT
   ACCEPTED
   visit = DURING_VISIT
   → block.

5. DEPARTMENT + STAFF
   DEPT_SUPPORT
   ASSIGNED
   visit = ASSIGNED
   → block.

6. DEPARTMENT + STAFF
   DEPT_SUPPORT
   ASSIGNED
   visit = AFTER_VISIT
   → block.

7. DEPT_SUPPORT
   DECLINED
   operational visit
   → không block.

8. DEPT_SUPPORT
   REMOVED
   operational visit
   → không block.
```

---

## Nhóm B — Visit status

```text
9. INVITED + ASSIGNED
   → block.

10. INVITED + BEFORE_VISIT
    → block.

11. ACCEPTED + DURING_VISIT
    → block.

12. ACCEPTED + AFTER_VISIT
    → block.

13. ACCEPTED + CLOSED
    → không block.

14. ASSIGNED + CANCELLED
    → không block.

15. INVITED + REJECTED
    → không block.

16. INVITED + WAITING_REQUEST_APPROVAL
    → không tạo canonical blocker.
```

Nếu production code có anomaly warning abstraction, verify warning; nếu chưa có thì chỉ verify không tạo business blocker.

---

## Nhóm C — Department, role và participant scope

```text
17. Participant user thuộc target department
    → được xét.

18. Participant user thuộc department khác
    → không block.

19. User role = STAFF dù có department_id trùng bất thường
    → không block.

20. User role = STUDENT
    → không block.

21. Participant role = IC_SUPPORT
    → không block.

22. Participant role = IC_HOST
    → không block.

23. Participant role = STUDENT
    → không block.
```

---

## Nhóm D — `ASSIGNED` canonical rule

```text
24. DEPARTMENT + STAFF
    DEPT_SUPPORT
    ASSIGNED
    → block hợp lệ.

25. DEPARTMENT + LEADER
    DEPT_SUPPORT
    ASSIGNED
    → vẫn block defensively;
      không âm thầm bỏ qua anomaly.
```

---

## Nhóm E — Count và grouping

```text
26. Một user có 3 participant records ở 3 visits:
    count = 3
    affectedUserCount = 1
    affectedVisitCount = 3.

27. Hai users cùng tham gia một visit:
    count = 2
    affectedUserCount = 2
    affectedVisitCount = 1.

28. Một INVITED và hai ACCEPTED:
    PENDING_PARTICIPANT_INVITATIONS count = 1
    ACTIVE_VISIT_PARTICIPATIONS count = 2.

29. Chỉ có DECLINED và REMOVED:
    blocker list rỗng.

30. Không có participant:
    blocker list rỗng.
```

---

# 21. Unit Test cho UC-106 handler

## Nhóm A — Có participant blocker

```text
31. Checker trả PENDING_PARTICIPANT_INVITATIONS
    → handler từ chối disable.

32. Checker trả ACTIVE_VISIT_PARTICIPATIONS
    → handler từ chối disable.

33. Có participant blocker
    → department.status giữ nguyên ACTIVE.

34. Có participant blocker
    → department.updated_at giữ nguyên.

35. Có participant blocker
    → department.updated_by giữ nguyên.

36. Có participant blocker
    → session revocation service không được gọi.

37. Có participant blocker
    → users.status không thay đổi.

38. Có participant blocker
    → participant.status không thay đổi.

39. Có participant blocker
    → không ghi audit status-change thành công.

40. Có participant blocker
    → không có partial update.
```

---

## Nhóm B — Tổng hợp blocker

```text
41. Có logistics blocker và participant blocker
    → trả đủ cả hai nhóm nếu contract hỗ trợ aggregate.

42. Participant blocker không làm mất logistics blocker hiện có.
```

Nếu current contract fail-fast, AI Agent phải báo rõ trước khi mở rộng.

---

## Nhóm C — Không có participant blocker

```text
43. Chỉ có DECLINED/REMOVED
    và không có blocker khác
    → flow disable tiếp tục.

44. Chỉ có participant trong CLOSED/CANCELLED/REJECTED visit
    → flow disable tiếp tục.

45. Participant thuộc department khác
    → flow disable target department tiếp tục.

46. Không có participant dependency
    → session revocation flow hiện tại vẫn được gọi.

47. Không có blocker
    → department chuyển INACTIVE.

48. Không có blocker
    → users.status vẫn giữ nguyên.
```

---

## Nhóm D — Enable path

Participant checker chỉ áp dụng khi:

```text
ACTIVE → INACTIVE
```

Cần test:

```text
49. Enable INACTIVE → ACTIVE
    → không chạy participant dependency checker.

50. Enable department
    → không thay đổi participant status.
```

---

# 22. Test fixture tối thiểu

Helper Unit Test nên hỗ trợ tạo object tối thiểu:

```text
Department:
- departmentId
- campusId
- departmentType
- status

User:
- userId
- roleCode
- subRole
- departmentId

Visit instance:
- visitInstanceId
- status

Participant:
- participantId
- visitInstanceId
- userId
- participantRole
- status
```

Helper có trách nhiệm rõ ràng, ví dụ:

```text
CreateGeneralDepartment(...)
CreateDepartmentLeader(...)
CreateDepartmentStaff(...)
CreateVisitInstance(...)
CreateDeptSupportParticipant(...)
```

Tên thật phải theo convention project.

Không seed object graph quá lớn khi test chỉ cần vài entity.

---

# 23. Không cần sửa database

Database hiện tại đã đủ:

```text
users.department_id
users.role_id
users.sub_role
visit_participants.user_id
visit_participants.visit_instance_id
visit_participants.participant_role
visit_participants.status
visit_request_campuses.status
```

Không cần:

- Thêm bảng.
- Thêm cột.
- Thêm enum.
- Thêm migration.
- Tạo SQL patch.
- Tạo index mới nếu chưa có bằng chứng hiệu năng.

---

# 24. Definition of Done

Chỉ coi là hoàn thành khi:

- Năm logistics blocker cũ vẫn hoạt động.
- `INVITED` trên operational visit block disable.
- `ACCEPTED` trên operational visit block disable.
- `ASSIGNED` của Department Staff trên operational visit block disable.
- `DECLINED` không block.
- `REMOVED` không block.
- `CLOSED`, `CANCELLED`, `REJECTED` không block.
- `WAITING_REQUEST_APPROVAL` không bị tính nhầm thành Host invitation hợp lệ.
- Participant department khác không block.
- `IC_HOST`, `IC_SUPPORT`, `STUDENT` không block.
- Có blocker thì department không đổi.
- Có blocker thì updated fields không đổi.
- Có blocker thì session không revoke.
- Có blocker thì users.status không đổi.
- Có blocker thì participant.status không đổi.
- Có blocker thì không ghi audit success.
- Không có blocker thì disable flow hiện tại vẫn chạy.
- Enable path không bị ảnh hưởng.
- Count participant/user/visit chính xác.
- Unit Tests mới pass.
- Unit Test project build pass.
- Chưa tạo Integration Test.
- Không sửa database.
- Không thêm migration.
- Không báo test pass nếu chưa chạy thật.

---

# 25. Báo cáo bắt buộc sau khi AI Agent triển khai

AI Agent phải trả báo cáo:

```text
1. Current-state findings
2. Existing logistics blocker implementation
3. Participant blocker design actually applied
4. Production files changed
5. Unit Test files added/updated
6. Business rules implemented
7. Error/blocker contract impact
8. Commands executed
9. Actual Unit Test results
10. Build result
11. Integration Test status: NOT IMPLEMENTED
12. Remaining risks
```

Không được báo:

```text
Hoàn thành
Tests pass
Build pass
```

nếu chưa chạy thật.

Nếu không chạy được:

```text
- Ghi lệnh đã chạy.
- Ghi lỗi thực tế.
- Ghi nguyên nhân.
- Ghi rõ phần nào chỉ mới kiểm tra tĩnh.
```

---

# 26. Baseline cuối cùng

```text
Khi Staff Leader disable GENERAL department:

A. Giữ nguyên logistics blockers:
   REQUESTED
   CHANGE_PROPOSED
   ASSIGNED
   ACCEPTED
   IN_PROGRESS

B. Bổ sung participant blocker khi:
   user.department_id = target department
   role_code = DEPARTMENT
   sub_role = LEADER hoặc STAFF
   participant_role = DEPT_SUPPORT

C. Participant status block:
   INVITED
   ACCEPTED
   ASSIGNED

D. Visit status block:
   ASSIGNED
   BEFORE_VISIT
   DURING_VISIT
   AFTER_VISIT

E. Không block:
   participant DECLINED
   participant REMOVED
   visit CLOSED
   visit CANCELLED
   visit REJECTED

F. WAITING_REQUEST_APPROVAL:
   không phải canonical participant dependency.

G. UC-106 không tự:
   remove participant
   decline invitation
   reassign người
   đóng visit

H. Khi có blocker:
   department vẫn ACTIVE
   updated fields không đổi
   session không revoke
   users.status không đổi
   participant.status không đổi
   audit success không ghi

I. Khi không có blocker:
   tiếp tục disable department và revoke session
   theo UC-106 hiện tại.

J. Phạm vi lần này:
   production code cần thiết
   + Unit Test
   - chưa Integration Test
   - không sửa database.
```
