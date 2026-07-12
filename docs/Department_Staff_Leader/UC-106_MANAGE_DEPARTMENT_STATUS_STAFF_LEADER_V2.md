# UC-106 — MANAGE DEPARTMENT STATUS — STAFF LEADER  
## Bản cập nhật mới: Disable phòng ban đồng thời thu hồi quyền truy cập của tài khoản thuộc phòng ban

> **Project:** PEMS — Partnership Engagement Management System  
> **Module:** Department Management  
> **Use Case:** UC-106 — Manage Department Status  
> **Primary Actor:** Staff Leader  
> **Runtime Role:** `role_code = STAFF`, `sub_role = LEADER`  
> **Permission Model:** Fixed role policy, không dùng dynamic permissions  
> **Database Baseline:** SQL v10 / schema hiện tại của PEMS  
> **Updated:** 2026-07-12  
> **Status:** Baseline để AI Agent đọc source, đối chiếu và triển khai

---

# 0. Mục đích của tài liệu

Tài liệu này thay thế logic UC-106 cũ ở phần xử lý tài khoản khi disable phòng ban.

UC-106 cũ đã triển khai các nguyên tắc sau:

- Chỉ Staff Leader được bật/tắt trạng thái phòng ban.
- Chỉ được thao tác phòng ban `GENERAL` thuộc đúng campus.
- Phòng `IC` mặc định không được enable/disable.
- Disable chỉ đổi `departments.status`, không xóa dữ liệu lịch sử.
- Phòng ban `INACTIVE` không được chọn cho nhiệm vụ hoặc phân công mới.
- Trạng thái phải được audit.
- Tài khoản `ACTIVE` từng được coi là blocker và làm thao tác disable bị từ chối.

Yêu cầu mới thay đổi phần tài khoản như sau:

```text
Staff Leader được phép disable phòng ban GENERAL
ngay cả khi phòng ban còn tài khoản ACTIVE.

Khi department chuyển sang INACTIVE:
- Không thay đổi users.status.
- Toàn bộ tài khoản DEPARTMENT + LEADER/STAFF thuộc phòng ban
  không thể tiếp tục truy cập hệ thống.
- Toàn bộ session còn hiệu lực của các tài khoản đó phải bị revoke.
- Các tài khoản đó không thể login hoặc refresh token
  cho đến khi department được enable lại.
- Khi enable lại, chỉ user có users.status = ACTIVE mới được login.
- Session cũ không được khôi phục.
```

Tài liệu này phải được dùng làm baseline mới cho:

- UC-106 backend.
- Authentication local.
- Google SSO.
- Refresh token.
- Session validation.
- Frontend confirmation modal.
- Audit.
- Unit test.
- Integration test.
- Manual verification.

---

# 1. Quy tắc bắt buộc trước khi code

AI Agent phải thực hiện theo thứ tự:

```text
1. Search và đọc source hiện tại.
2. Đối chiếu SQL/schema thật.
3. Đọc command/query/handler/validator/controller hiện có của UC-106.
4. Đọc authentication handlers.
5. Đọc session revocation/session validation hiện tại.
6. Đọc entity/configuration DbContext liên quan.
7. Đọc frontend department management page/modal/service/type.
8. Đọc test hiện có.
9. Chỉ sau đó mới lập kế hoạch sửa.
10. Không sửa theo suy đoán.
```

Không được tự bịa:

- Tên file.
- Endpoint.
- Entity.
- Column.
- Enum.
- Session field.
- Error mapping.
- Audit metadata.
- Trạng thái terminal của task/logistics.
- Service abstraction.
- Route frontend.
- Test infrastructure.

Nếu source thật khác tài liệu:

```text
SQL/schema thật
→ source hiện tại
→ canonical business rules hiện tại
→ tài liệu UC này
→ tài liệu legacy
```

AI Agent phải báo rõ điểm lệch trước khi sửa.

---

# 2. Thông tin UC

| Thuộc tính | Giá trị |
|---|---|
| UC ID | UC-106 |
| Tên tiếng Anh | Manage Department Status |
| Tên tiếng Việt | Quản lý trạng thái phòng ban |
| Module | Department Management |
| Primary Actor | Staff Leader |
| Runtime Role | `STAFF + LEADER` |
| Target Entity | `departments` |
| Allowed Department Type | `GENERAL` |
| Supported Status | `ACTIVE`, `INACTIVE` |
| Campus Scope | `departments.campus_id = currentUser.primary_campus_id` |
| Authorization Model | Fixed role policy |
| Database liên quan | `departments`, `users`, `roles`, `user_sessions`, audit tables |
| Authentication liên quan | Credentials login, Google SSO, refresh token, session validation |

---

# 3. Mục tiêu nghiệp vụ

UC-106 mới cho phép Staff Leader:

1. Ngừng hoạt động một phòng ban `GENERAL`.
2. Kích hoạt lại một phòng ban `GENERAL`.
3. Ngăn toàn bộ Department Leader và Department Staff thuộc phòng ban `INACTIVE` truy cập hệ thống.
4. Revoke toàn bộ session còn hiệu lực của các tài khoản bị ảnh hưởng.
5. Giữ nguyên trạng thái cá nhân của từng tài khoản.
6. Giữ nguyên dữ liệu lịch sử và liên kết với phòng ban.
7. Loại phòng ban `INACTIVE` khỏi các lựa chọn phân công mới.
8. Vẫn chặn disable khi còn dependency nghiệp vụ non-terminal nếu dependency đó có thể làm đứt luồng vận hành.

---

# 4. Actor và authorization

## 4.1 Actor hợp lệ

Người thực hiện phải thỏa mãn:

```text
currentUser.role_code = STAFF
currentUser.sub_role = LEADER
currentUser.status = ACTIVE
currentUser.primary_campus_id IS NOT NULL
```

Không dùng các giá trị legacy:

```text
STAFF_LEADER
STAFF_L
IC_STAFF_LEADER
permission_code
permission_level
permissions
role_permissions
```

## 4.2 Campus scope

Backend chỉ cho phép thao tác department thỏa mãn:

```text
departments.campus_id = currentUser.primary_campus_id
```

Frontend không được quyết định scope bằng `campusId`.

Nếu gọi API với department thuộc campus khác:

```text
HTTP 403 Forbidden
errorCode = DEPARTMENT_OUTSIDE_CAMPUS_SCOPE
```

## 4.3 Department type

Chỉ cho phép thay đổi trạng thái:

```text
department_type = GENERAL
```

Đối với department `IC`:

- Không hiển thị toggle.
- Không cho direct API thay đổi trạng thái.
- Backend phải kiểm tra lại.
- Trả lỗi nghiệp vụ rõ ràng.

Đề xuất:

```text
HTTP 409 Conflict
errorCode = DEFAULT_IC_DEPARTMENT_STATUS_LOCKED
message = Không thể thay đổi trạng thái phòng Hợp tác quốc tế mặc định.
```

---

# 5. Ý nghĩa trạng thái

## 5.1 Department `ACTIVE`

Khi phòng ban `ACTIVE`:

- Có thể xuất hiện trong lựa chọn phân công mới.
- Có thể nhận task/logistics/routing mới nếu các rule khác cho phép.
- Department Leader và Department Staff thuộc phòng có thể login nếu:
  - `users.status = ACTIVE`
  - `roles.status = ACTIVE`
  - Các điều kiện auth khác hợp lệ.

## 5.2 Department `INACTIVE`

Khi phòng ban `INACTIVE`:

- Không được chọn cho nhiệm vụ mới.
- Không được chọn cho logistics/resource mới.
- Không được chọn cho department routing mới.
- Không bị hard-delete.
- Không xóa `head_user_id`.
- Không xóa `users.department_id`.
- Không thay đổi `users.status`.
- Không xóa lịch sử nhiệm vụ.
- Không xóa audit.
- Department Leader và Department Staff thuộc phòng không được truy cập hệ thống.
- Toàn bộ session hiện tại của họ phải bị revoke.

---

# 6. Phân biệt trạng thái user và department

Hai loại trạng thái phải được giữ độc lập:

```text
users.status = INACTIVE
→ Tài khoản cá nhân bị khóa.

departments.status = INACTIVE
→ Tài khoản bị mất quyền truy cập do đơn vị chủ quản ngừng hoạt động.
```

Tuyệt đối không bulk-update:

```sql
UPDATE users
SET status = 'INACTIVE'
WHERE department_id = @DepartmentId;
```

Lý do:

- Mất dấu nguyên nhân khóa.
- Không phân biệt khóa cá nhân và khóa theo phòng ban.
- Enable lại department có nguy cơ kích hoạt nhầm tài khoản bị khóa riêng.
- Audit không còn rõ nghĩa.
- Dễ làm sai các luồng account management khác.

---

# 7. Quy tắc truy cập hiệu lực

## 7.1 Rule tổng quát

```text
AccessAllowed =
    user.status == ACTIVE
    AND role.status == ACTIVE
    AND (
        role_code != DEPARTMENT
        OR linkedDepartment.status == ACTIVE
    )
```

## 7.2 Rule riêng cho role DEPARTMENT

Nếu:

```text
role_code = DEPARTMENT
sub_role = LEADER hoặc STAFF
```

thì bắt buộc:

```text
users.department_id IS NOT NULL
department tồn tại
departments.status = ACTIVE
```

Nếu không thỏa mãn:

```text
Không tạo session mới.
Không phát access token mới.
Không phát refresh token mới.
Không refresh token.
```

## 7.3 Không tin JWT claim cũ

Không được chỉ dựa vào:

```text
department_id trong JWT
role/sub_role snapshot trong JWT
status tại thời điểm login
```

Vì department có thể bị chuyển sang `INACTIVE` sau khi token đã được phát.

Backend phải kiểm tra trạng thái department hiện tại trong database ở các luồng bắt buộc.

---

# 8. Tài khoản bị ảnh hưởng khi disable

Backend xác định affected users theo quan hệ thật:

```text
users.department_id = targetDepartmentId
AND role_code = DEPARTMENT
AND sub_role IN (LEADER, STAFF)
```

Bao gồm:

- `DEPARTMENT + LEADER`: trưởng phòng.
- `DEPARTMENT + STAFF`: nhân sự phòng ban.

Không được chỉ dựa vào:

```text
departments.head_user_id
```

vì `head_user_id` không đại diện cho toàn bộ nhân sự.

Không ảnh hưởng đến:

- `STAFF + LEADER`
- `STAFF + STAFF`
- `HO`
- `ADMIN`
- `STUDENT`
- `VISITOR`
- Tài khoản thuộc department khác

Ngay cả khi dữ liệu bất thường khiến role khác có cùng `department_id`, logic revoke vẫn phải giới hạn đúng `role_code = DEPARTMENT`.

---

# 9. Preconditions

## 9.1 Preconditions chung

- Staff Leader đã authenticated.
- Staff Leader đang `ACTIVE`.
- Staff Leader có `primary_campus_id`.
- Department tồn tại.
- Department thuộc campus của actor.
- Department là `GENERAL`.
- `newStatus` hợp lệ.
- Request không chứa field giả mạo scope/audit.

## 9.2 Khi disable

- Department hiện tại là `ACTIVE`.
- Không có dependency nghiệp vụ non-terminal thuộc nhóm hard blocker.
- Active users không còn là blocker.
- Active sessions không còn là blocker.

## 9.3 Khi enable

- Department hiện tại là `INACTIVE`.
- Campus chứa department đang `ACTIVE`.
- Tên department vẫn hợp lệ theo unique constraint.
- Không bắt buộc có `head_user_id`.

Nếu chưa có trưởng phòng, có thể hiển thị warning:

```text
Phòng ban chưa được gán trưởng phòng.
```

Nhưng vẫn cho phép enable.

---

# 10. Main Flow — Disable department

## 10.1 User flow

```text
1. Staff Leader mở màn hình Quản lý phòng ban.
2. Staff Leader chọn toggle của GENERAL department đang ACTIVE.
3. Frontend gọi impact preview nếu có.
4. Hệ thống hiển thị confirmation modal.
5. Staff Leader xác nhận Ngừng hoạt động.
6. Frontend gửi request đổi trạng thái.
7. Backend resolve current user.
8. Backend kiểm tra STAFF + LEADER.
9. Backend validate departmentId và newStatus.
10. Backend load department.
11. Backend kiểm tra campus scope.
12. Backend kiểm tra department_type = GENERAL.
13. Backend kiểm tra open business dependencies.
14. Nếu có blocker, trả 409 và không thay đổi dữ liệu.
15. Backend tìm affected users đúng role/sub-role/department.
16. Backend tìm active sessions của affected users.
17. Backend đổi departments.status = INACTIVE.
18. Backend revoke sessions.
19. Backend cập nhật updated_by, updated_at.
20. Backend ghi audit before/after.
21. Commit transaction.
22. Frontend update row hoặc refetch list.
23. Request tiếp theo của affected users bị từ chối.
24. Affected users không thể login lại khi department còn INACTIVE.
```

## 10.2 Department changes

```text
departments.status = INACTIVE
departments.updated_by = currentUser.user_id
departments.updated_at = current timestamp
```

## 10.3 User changes

Không thay đổi:

```text
users.status
users.department_id
users.role_id
users.sub_role
users.primary_campus_id
```

## 10.4 Session changes

Revoke toàn bộ session còn hiệu lực của affected users.

AI Agent phải tìm và tái sử dụng session revocation service hiện có nếu project đã có.

Không tự viết raw SQL nếu source đã có service chuẩn.

Nếu schema hỗ trợ revoke reason, dùng:

```text
DEPARTMENT_DISABLED
```

Không thêm column mới nếu schema chưa có.

---

# 11. Main Flow — Enable department

```text
1. Staff Leader chọn toggle của GENERAL department đang INACTIVE.
2. Hệ thống hiển thị confirmation modal.
3. Staff Leader xác nhận Kích hoạt lại.
4. Backend kiểm tra actor.
5. Backend kiểm tra campus scope.
6. Backend kiểm tra department_type = GENERAL.
7. Backend kiểm tra campus.status = ACTIVE.
8. Backend kiểm tra unique name nếu cần theo source hiện tại.
9. Backend đổi departments.status = ACTIVE.
10. Backend cập nhật updated_by, updated_at.
11. Backend ghi audit.
12. Commit transaction.
13. Frontend cập nhật danh sách.
```

Khi enable:

- Không đổi `users.status`.
- Không unrevoke session cũ.
- Không xóa revoke metadata.
- Không phát token tự động.
- Không login tự động.
- User `ACTIVE` được phép login mới.
- User `INACTIVE` vẫn bị khóa.
- Session cũ vẫn không sử dụng được.

---

# 12. Dependency blocker rules

## 12.1 Không còn là blocker

Các điều kiện sau không được block disable:

| Điều kiện | Xử lý |
|---|---|
| Có Department Leader `ACTIVE` | Cho disable, revoke session |
| Có Department Staff `ACTIVE` | Cho disable, revoke session |
| Có active session | Cho disable, revoke session |
| Có `head_user_id` | Cho disable, giữ nguyên quan hệ |
| Có dữ liệu lịch sử | Cho disable, giữ nguyên |

`ACTIVE_USERS` phải chuyển từ blocker thành affected information.

## 12.2 Vẫn là hard blocker

Đề xuất giữ blocker với dependency nghiệp vụ non-terminal có thể làm đứt luồng:

- Department task chưa hoàn tất.
- Department assignment đang chờ hoặc đang thực hiện.
- Logistics/resource item chưa terminal.
- Handover đang chờ ký hoặc chưa hoàn tất.
- Participant/support assignment chưa kết thúc.
- Visit đang trong giai đoạn vận hành và còn phụ thuộc department.
- Bất kỳ dependency nào mà việc khóa toàn bộ nhân sự sẽ khiến workflow không thể tiếp tục.

AI Agent phải:

```text
- Đọc schema thật.
- Đọc entity thật.
- Đọc enum thật.
- Đọc constant thật.
- Xác định terminal status từ source.
- Không đoán enum.
- Không tạo field department_id nếu bảng không có.
- Không tạo join giả.
```

Nếu có blocker:

```text
HTTP 409 Conflict
errorCode = DEPARTMENT_STATUS_BLOCKED_BY_DEPENDENCIES
department status không đổi
session không revoke
audit status change không ghi
```

---

# 13. Transaction và tính nguyên tử

Disable phải là một transaction logic duy nhất:

```text
Check blockers
→ Update department
→ Revoke sessions
→ Write audit
→ Commit
```

Nếu bất kỳ bước nào lỗi:

```text
Rollback toàn bộ.
```

Không được xảy ra:

```text
department INACTIVE nhưng session chưa revoke
```

hoặc:

```text
session đã revoke nhưng department vẫn ACTIVE
```

Nếu project dùng TransactionBehaviour, AI Agent phải kiểm tra:

- Handler có tự mở transaction không.
- SaveChanges nằm ở đâu.
- Session revoke service có cùng DbContext/transaction không.
- Audit behaviour có chạy cùng transaction không.
- Không tạo nested transaction gây lỗi.

---

# 14. No-op rule

Nếu:

```text
newStatus == currentStatus
```

thì:

- Không cập nhật `updated_at`.
- Không cập nhật `updated_by`.
- Không ghi audit status change.
- Không revoke session.
- Không chạy dependency blocker nếu convention không cần.
- Trả response rõ ràng.

Đề xuất message:

```text
Trạng thái phòng ban không thay đổi.
```

HTTP dùng convention hiện tại của project:

- Có thể `200 OK` với `changed = false`.
- Hoặc `409 Conflict` nếu project đã chốt như vậy.

Ưu tiên giữ convention source hiện có.

---

# 15. Tích hợp Authentication

UC-106 mới bắt buộc sửa các luồng authentication liên quan.

## 15.1 Credentials login

Sau khi xác minh credentials, user status và role:

```text
Nếu role_code = DEPARTMENT:
- department_id phải tồn tại.
- department phải tồn tại.
- department.status phải ACTIVE.
```

Nếu department `INACTIVE`:

```text
Không tạo session.
Không phát token.
errorCode = DEPARTMENT_INACTIVE
message = Phòng ban của tài khoản đã ngừng hoạt động.
```

Thông báo UI đề xuất:

```text
Phòng ban của tài khoản đã ngừng hoạt động.
Vui lòng liên hệ Trưởng phòng Hợp tác quốc tế tại cơ sở.
```

## 15.2 Google SSO

Sau khi mapping Google identity sang user nội bộ:

- Kiểm tra user status.
- Kiểm tra role.
- Nếu role `DEPARTMENT`, kiểm tra linked department.
- Không tạo session/token nếu department `INACTIVE`.

Áp dụng cho:

```text
DEPARTMENT + LEADER
DEPARTMENT + STAFF
```

## 15.3 Refresh token

Refresh token phải kiểm tra lại trạng thái department hiện tại trong DB.

Nếu department đã `INACTIVE`:

- Từ chối refresh.
- Revoke session nếu session chưa revoke.
- Trả `DEPARTMENT_INACTIVE` hoặc `SESSION_REVOKED` theo convention.
- Không phát access token mới.
- Không phát refresh token mới.

## 15.4 Request với access token cũ

Khi UC-106 disable department:

```text
active session bị revoke
→ request tiếp theo phải bị 401
→ frontend xóa token
→ redirect login
```

AI Agent phải kiểm tra `SessionValidationMiddleware` hoặc cơ chế tương đương có thực sự kiểm tra DB mỗi request hay không.

Nếu không, phải báo rõ trước khi sửa vì chỉ revoke session sẽ không làm access token mất hiệu lực ngay.

---

# 16. Shared Access Eligibility Service

Không copy logic department access vào nhiều handler.

Nên dùng một service/policy/helper chung, nhưng tên phải theo source convention hiện tại.

Ví dụ trách nhiệm:

```text
AccountAccessEligibilityService
OrganizationAccessPolicy
UserAccessEligibilityChecker
```

Service kiểm tra:

```text
1. User tồn tại.
2. User.status = ACTIVE.
3. Role tồn tại.
4. Role.status = ACTIVE.
5. Role/sub-role hợp lệ.
6. Nếu role_code = DEPARTMENT:
   - department_id != null
   - department tồn tại
   - department.status = ACTIVE
7. Trả error code ổn định.
```

Các luồng dùng chung:

- Credentials login.
- Google SSO.
- Refresh token.
- Các luồng cấp session/token khác nếu có.

Không bắt buộc tạo service mới nếu source đã có abstraction tương đương. Phải ưu tiên tái sử dụng.

---

# 17. API contract

## 17.1 Status change endpoint

Recommended endpoint:

```http
PATCH /api/departments/{departmentId}/status
```

AI Agent phải xác minh route thật trước khi sửa.

Request body:

```json
{
  "newStatus": "INACTIVE",
  "reason": "Tạm ngừng hoạt động phòng ban"
}
```

Rule:

- `newStatus` bắt buộc.
- Chỉ nhận `ACTIVE` hoặc `INACTIVE`.
- `reason` tùy chọn.
- `reason` chỉ lưu audit metadata nếu hệ thống hỗ trợ.
- Không thêm `reason` column vào `departments` nếu chưa có SQL patch.

Frontend không gửi:

```json
{
  "campusId": 1,
  "departmentType": "GENERAL",
  "updatedBy": 10,
  "updatedAt": "2026-07-12T10:00:00Z"
}
```

## 17.2 Disable success response

```json
{
  "departmentId": 10,
  "name": "Phòng Công nghệ Thông tin",
  "oldStatus": "ACTIVE",
  "newStatus": "INACTIVE",
  "status": "INACTIVE",
  "changed": true,
  "affectedAccountCount": 8,
  "revokedSessionCount": 3,
  "updatedAt": "2026-07-12T16:00:00Z",
  "updatedBy": 5,
  "message": "Đã ngừng hoạt động phòng ban. 8 tài khoản không còn quyền truy cập hệ thống."
}
```

## 17.3 Enable success response

```json
{
  "departmentId": 10,
  "name": "Phòng Công nghệ Thông tin",
  "oldStatus": "INACTIVE",
  "newStatus": "ACTIVE",
  "status": "ACTIVE",
  "changed": true,
  "updatedAt": "2026-07-12T17:00:00Z",
  "updatedBy": 5,
  "message": "Đã kích hoạt lại phòng ban. Các tài khoản đang hoạt động có thể đăng nhập lại."
}
```

## 17.4 No-op response

```json
{
  "departmentId": 10,
  "oldStatus": "ACTIVE",
  "newStatus": "ACTIVE",
  "status": "ACTIVE",
  "changed": false,
  "message": "Trạng thái phòng ban không thay đổi."
}
```

---

# 18. Impact preview

## 18.1 Mục tiêu

Confirmation modal nên hiển thị trước:

- Số tài khoản bị ảnh hưởng.
- Số active session.
- Danh sách blocker.
- Có thể thực hiện hay không.

## 18.2 Endpoint đề xuất

```http
GET /api/departments/{departmentId}/status-impact?newStatus=INACTIVE
```

Đây chỉ là route đề xuất. AI Agent phải kiểm tra xem source đã có query/preview endpoint hay chưa.

Response mẫu:

```json
{
  "departmentId": 10,
  "currentStatus": "ACTIVE",
  "targetStatus": "INACTIVE",
  "affectedAccountCount": 8,
  "activeSessionCount": 3,
  "canChangeStatus": false,
  "blockers": [
    {
      "type": "OPEN_LOGISTICS_ITEMS",
      "count": 2,
      "message": "Còn 2 yêu cầu hậu cần chưa hoàn tất."
    }
  ]
}
```

Nếu không tạo preview endpoint riêng, có thể:

- Dùng query có sẵn.
- Dùng modal static nếu chưa cần count.
- Nhưng PATCH vẫn phải validate blocker ở backend.

Frontend preview không thay thế backend guard.

---

# 19. Error contract

| Case | HTTP | Error code | Message |
|---|---:|---|---|
| Chưa đăng nhập | 401 | `UNAUTHENTICATED` | Phiên đăng nhập không hợp lệ |
| Không phải Staff Leader | 403 | `DEPARTMENT_STATUS_FORBIDDEN` | Bạn không có quyền thay đổi trạng thái phòng ban |
| Ngoài campus scope | 403 | `DEPARTMENT_OUTSIDE_CAMPUS_SCOPE` | Bạn không có quyền thao tác với phòng ban ngoài cơ sở của mình |
| Department không tồn tại | 404 | `DEPARTMENT_NOT_FOUND` | Không tìm thấy phòng ban |
| Department là IC | 409 | `DEFAULT_IC_DEPARTMENT_STATUS_LOCKED` | Không thể thay đổi trạng thái phòng Hợp tác quốc tế mặc định |
| Có dependency blocker | 409 | `DEPARTMENT_STATUS_BLOCKED_BY_DEPENDENCIES` | Không thể ngừng hoạt động vì còn nghiệp vụ chưa hoàn tất |
| Status invalid | 400 hoặc 422 | `INVALID_DEPARTMENT_STATUS` | Trạng thái phòng ban không hợp lệ |
| Enable khi campus inactive | 422 | `CAMPUS_INACTIVE` | Không thể kích hoạt phòng ban trong cơ sở đã ngừng hoạt động |
| Login khi department inactive | Theo auth convention | `DEPARTMENT_INACTIVE` | Phòng ban của tài khoản đã ngừng hoạt động |
| Session bị revoke | 401 | `SESSION_REVOKED` | Phiên đăng nhập đã bị thu hồi |

Blocker response mẫu:

```json
{
  "errorCode": "DEPARTMENT_STATUS_BLOCKED_BY_DEPENDENCIES",
  "message": "Không thể ngừng hoạt động phòng ban vì còn nghiệp vụ chưa hoàn tất.",
  "blockers": [
    {
      "type": "OPEN_DEPARTMENT_TASKS",
      "count": 2,
      "message": "Còn 2 nhiệm vụ phòng ban chưa hoàn tất."
    },
    {
      "type": "PENDING_HANDOVERS",
      "count": 1,
      "message": "Còn 1 biên bản bàn giao đang chờ xác nhận."
    }
  ]
}
```

---

# 20. Frontend behavior

## 20.1 Toggle visibility

| Department type | Toggle |
|---|---|
| `IC` | Không hiển thị |
| `GENERAL + ACTIVE` | Hiển thị bật |
| `GENERAL + INACTIVE` | Hiển thị tắt |

Frontend ưu tiên dùng:

```text
canToggleStatus
```

Không dựa vào:

```text
Tên phòng ban
Chuỗi "Phòng Hợp tác quốc tế"
Màu badge
Vị trí row
```

## 20.2 Disable confirmation modal

Tiêu đề:

```text
Ngừng hoạt động phòng ban?
```

Nội dung:

```text
Sau khi ngừng hoạt động:

- Trưởng phòng và toàn bộ nhân sự thuộc phòng ban sẽ bị đăng xuất.
- Các tài khoản này không thể đăng nhập cho đến khi phòng ban được kích hoạt lại.
- Phòng ban sẽ không xuất hiện trong các lựa chọn phân công mới.
- Dữ liệu và lịch sử nghiệp vụ vẫn được giữ nguyên.
```

Impact summary:

```text
Tài khoản bị ảnh hưởng: 8
Phiên đăng nhập đang hoạt động: 3
```

Nếu có blocker:

- Hiển thị blocker list.
- Disable nút confirm.
- Không gửi status change request.

Buttons:

```text
[Hủy] [Ngừng hoạt động]
```

## 20.3 Enable confirmation modal

Tiêu đề:

```text
Kích hoạt lại phòng ban?
```

Nội dung:

```text
Sau khi kích hoạt:

- Phòng ban có thể được chọn cho các phân công mới.
- Các tài khoản đang ở trạng thái hoạt động có thể đăng nhập lại.
- Các phiên đăng nhập cũ không được khôi phục.
- Những tài khoản bị khóa riêng vẫn tiếp tục bị khóa.
```

Buttons:

```text
[Hủy] [Kích hoạt lại]
```

## 20.4 Success message

Disable:

```text
Đã ngừng hoạt động phòng ban.
8 tài khoản không còn quyền truy cập và 3 phiên đăng nhập đã được thu hồi.
```

Enable:

```text
Đã kích hoạt lại phòng ban.
Các tài khoản đang hoạt động có thể đăng nhập lại.
```

## 20.5 Error handling

Frontend phải:

- Hiển thị blocker rõ ràng.
- Không tự đảo toggle trước khi API thành công, hoặc rollback đúng nếu dùng optimistic update.
- Không nuốt `DEPARTMENT_INACTIVE`.
- Không hiển thị lỗi chung nếu backend đã có error code cụ thể.
- Refetch row/list sau success nếu state update không chắc chắn.
- Giữ responsive behavior hiện tại.
- Không thêm thư viện mới nếu không cần.

---

# 21. Audit requirements

Audit status change phải ghi tối thiểu:

```text
Action = CHANGE_DEPARTMENT_STATUS
Entity = DEPARTMENT
EntityId
ActorUserId
ActorRole
ActorCampusId
OldStatus
NewStatus
Reason nếu có
AffectedAccountCount
RevokedSessionCount
Timestamp
```

Nếu có audit changes table:

```text
field_name = status
old_value = ACTIVE
new_value = INACTIVE
```

Không ghi audit rằng user status thay đổi vì `users.status` không đổi.

Nếu session revoke có audit riêng trong source hiện tại, tái sử dụng cơ chế đó.

Không tự thêm metadata column nếu schema chưa hỗ trợ.

---

# 22. Business rules

```text
BR-UC106-01
Chỉ Staff Leader có role_code = STAFF và sub_role = LEADER
được quản lý trạng thái department.

BR-UC106-02
Staff Leader chỉ được thao tác department thuộc primary campus của mình.

BR-UC106-03
Chỉ GENERAL department được enable hoặc disable.

BR-UC106-04
IC department không hiển thị toggle và backend phải chặn direct API.

BR-UC106-05
Disable chỉ đổi departments.status sang INACTIVE;
không hard-delete department hoặc dữ liệu lịch sử.

BR-UC106-06
Department INACTIVE không được xuất hiện trong lựa chọn phân công,
logistics, participant hoặc routing mới.

BR-UC106-07
Active user thuộc department không phải là blocker khi disable.

BR-UC106-08
Disable department không thay đổi users.status.

BR-UC106-09
Tài khoản DEPARTMENT + LEADER/STAFF thuộc department INACTIVE
không được login bằng credentials.

BR-UC106-10
Tài khoản DEPARTMENT + LEADER/STAFF thuộc department INACTIVE
không được login bằng Google SSO.

BR-UC106-11
Tài khoản thuộc department INACTIVE không được refresh token.

BR-UC106-12
Khi disable, mọi session còn hiệu lực của affected users phải bị revoke.

BR-UC106-13
Open task, logistics, handover hoặc dependency nghiệp vụ non-terminal
có thể block việc disable.

BR-UC106-14
Enable department yêu cầu campus đang ACTIVE.

BR-UC106-15
Enable department không thay đổi users.status.

BR-UC106-16
Enable department không khôi phục session cũ.

BR-UC106-17
User có users.status = INACTIVE vẫn không được login
sau khi department được enable.

BR-UC106-18
Access eligibility phải kiểm tra department status hiện tại trong database,
không chỉ dựa vào JWT claim.

BR-UC106-19
Department update, session revoke và audit phải cùng transaction.

BR-UC106-20
Mọi status change thật sự phải ghi audit before/after.

BR-UC106-21
Nếu target status giống current status, xử lý no-op,
không cập nhật audit fields và không revoke session.

BR-UC106-22
Không được auto-reassign user, task, logistics hoặc handover
khi disable department.

BR-UC106-23
Không được xóa users.department_id hoặc departments.head_user_id
khi disable.
```

---

# 23. Backend implementation checklist

AI Agent phải kiểm tra và cập nhật đúng các layer có liên quan.

## 23.1 API Layer

- Controller route thật.
- Authorization attribute/filter hiện tại.
- Request DTO.
- Response mapping.
- Error mapping.
- Không chứa business logic phức tạp trong controller.

## 23.2 Application Layer

- Command.
- Validator.
- Handler.
- Response.
- Dependency checker.
- Session revocation service.
- Shared access eligibility checker.
- Audit integration.
- Transaction behavior.

## 23.3 Domain Layer

Chỉ sửa domain entity nếu project hiện tại đặt transition logic trong entity.

Không ép refactor domain nếu source convention hiện tại không dùng domain methods.

## 23.4 Infrastructure Layer

- EF query đúng schema.
- Session repository/service.
- Audit persistence.
- Entity configurations.
- Không thêm migration tùy tiện.
- Không thêm field/table nếu chưa có SQL patch.

## 23.5 Authentication

- Credentials login.
- Google SSO.
- Refresh token.
- Session validation middleware/service.
- Các flow cấp token khác nếu có.

## 23.6 Frontend

- Page/component.
- API service.
- Type/interface.
- Confirmation modal.
- Blocker modal.
- Toast.
- Toggle state.
- Error interceptor.
- Login error message.

---

# 24. Validation rules

| Field/Case | Rule |
|---|---|
| `departmentId` | Bắt buộc hợp lệ theo route/type |
| `newStatus` | Chỉ `ACTIVE` hoặc `INACTIVE` |
| `reason` | Optional, trim, giới hạn theo convention hiện tại |
| Campus scope | Backend guard |
| Department type | Chỉ `GENERAL` |
| Same status | No-op |
| Campus inactive khi enable | Reject |
| Open dependency | Reject |
| Active users | Không reject |
| Active sessions | Không reject |

Không validate DB-dependent rule trong FluentValidation nếu project convention đặt business validation trong handler.

---

# 25. Unit test requirements

## 25.1 Validator tests

- `ACTIVE` hợp lệ.
- `INACTIVE` hợp lệ.
- Empty status bị từ chối.
- Invalid status bị từ chối.
- Reason hợp lệ.
- Reason được trim nếu command/validator có convention.
- Reason vượt giới hạn bị từ chối nếu source có giới hạn.
- Validator không query DB.

## 25.2 Handler tests

- Non-Staff Leader bị từ chối.
- Staff Leader ngoài campus bị từ chối.
- Department không tồn tại.
- IC department bị chặn.
- Active users không block disable.
- Disable thành công đổi department status.
- Disable không đổi users.status.
- Revoke đúng Department Leader.
- Revoke đúng Department Staff.
- Không revoke Staff Leader.
- Không revoke IC Staff.
- Không revoke user department khác.
- Open dependency trả blocker.
- Có blocker thì không đổi status.
- Có blocker thì không revoke session.
- Enable khi campus inactive bị từ chối.
- Enable thành công.
- Enable không unrevoke session.
- User inactive không bị đổi thành active.
- Same status là no-op.
- Audit old/new đúng.
- Rollback khi session revoke lỗi.
- Rollback khi audit/save lỗi nếu cùng transaction.

---

# 26. Integration test requirements

## 26.1 UC-106 API

```text
1. Staff Leader disable GENERAL department cùng campus
   → 200, department INACTIVE.

2. Department có active Department Leader và Staff
   → disable vẫn thành công.

3. Sau disable
   → users.status vẫn ACTIVE.

4. Session của Department Leader bị revoke.

5. Session của Department Staff bị revoke.

6. Session của STAFF + LEADER không bị revoke.

7. Session của STAFF + STAFF không bị revoke.

8. Session user thuộc department khác không bị revoke.

9. Department có open task/logistics
   → 409, department vẫn ACTIVE, session chưa revoke.

10. IC department direct API
    → 409, không thay đổi.

11. Department campus khác
    → 403.

12. Non-Staff Leader
    → 403.

13. Enable department khi campus ACTIVE
    → thành công.

14. Enable department khi campus INACTIVE
    → bị từ chối.

15. Same status
    → no-op, updated_at không đổi.
```

## 26.2 Authentication

```text
16. DEPARTMENT + LEADER thuộc department INACTIVE login credentials
    → DEPARTMENT_INACTIVE.

17. DEPARTMENT + STAFF thuộc department INACTIVE login credentials
    → DEPARTMENT_INACTIVE.

18. DEPARTMENT + LEADER thuộc department INACTIVE login Google SSO
    → DEPARTMENT_INACTIVE.

19. DEPARTMENT + STAFF thuộc department INACTIVE login Google SSO
    → DEPARTMENT_INACTIVE.

20. Refresh token sau khi department bị disable
    → bị từ chối.

21. Access token từ session đã revoke gọi API
    → 401.

22. Enable lại department + users.status ACTIVE
    → login mới thành công.

23. Enable lại department + users.status INACTIVE
    → vẫn bị từ chối.

24. Session cũ sau khi enable
    → vẫn revoked.

25. Role khác có department_id trùng do dữ liệu bất thường
    → không bị revoke hoặc chặn sai nếu rule chỉ áp dụng DEPARTMENT.
```

## 26.3 Transaction

```text
26. Session revoke failure
    → department status rollback.

27. Department update failure
    → session không revoke.

28. Blocker xuất hiện
    → audit status change không được ghi.

29. Success
    → audit old/new và count đúng.
```

---

# 27. Manual acceptance scenarios

## Scenario 1 — Disable department có nhân sự

```text
Given GENERAL department đang ACTIVE
And có 1 Department Leader và 5 Department Staff đang ACTIVE
And không có dependency đang mở

When Staff Leader xác nhận ngừng hoạt động

Then department chuyển INACTIVE
And users.status của 6 tài khoản không đổi
And toàn bộ active session của 6 tài khoản bị revoke
And các tài khoản không thể login lại
And audit được ghi.
```

## Scenario 2 — User đang mở hệ thống

```text
Given Department Staff đang đăng nhập
When Staff Leader disable department của user đó
Then request tiếp theo bị 401
And frontend redirect login
And user login lại nhận DEPARTMENT_INACTIVE.
```

## Scenario 3 — Enable lại department

```text
Given department đang INACTIVE
And user.status = ACTIVE
When Staff Leader enable department
Then department chuyển ACTIVE
And user có thể login mới
And session cũ vẫn không dùng được.
```

## Scenario 4 — Tài khoản bị khóa riêng

```text
Given department đang INACTIVE
And user.status = INACTIVE
When Staff Leader enable department
Then user vẫn không thể login
Because tài khoản cá nhân vẫn bị khóa.
```

## Scenario 5 — Có blocker

```text
Given department có task/logistics non-terminal
When Staff Leader cố disable
Then API trả 409 cùng blocker list
And department vẫn ACTIVE
And session không revoke.
```

## Scenario 6 — IC department

```text
Given department_type = IC
When UI render
Then không có toggle.

When gọi direct API
Then backend từ chối
And status không đổi.
```

---

# 28. Out of scope

UC-106 không thực hiện:

- Xóa department.
- Xóa user.
- Đổi `users.status`.
- Chuyển user sang department khác.
- Gán trưởng phòng.
- Thay trưởng phòng.
- Đổi department type.
- Đổi campus.
- Tự reassign task.
- Tự reassign logistics.
- Tự hủy task.
- Tự hủy logistics.
- Xóa dữ liệu lịch sử.
- Khôi phục session.
- Tự gửi mật khẩu.
- Tự tạo account mới.
- Tự gửi email/notification nếu chưa có yêu cầu phase riêng.
- Thêm dynamic permission tables.
- Thêm migration không có SQL patch.

---

# 29. Source files cần search trước khi sửa

AI Agent phải tự tìm source thật, không dùng path giả.

Ít nhất cần tìm:

```text
Backend:
- DepartmentsController
- ManageDepartmentStatus command/query/handler/validator/response
- Department entity/configuration
- DbContext
- Role constants
- Department status constants
- EffectiveRole resolver
- Session entity/service/repository
- SessionValidationMiddleware
- Credentials login handler
- Google SSO handler
- Refresh token handler
- Authentication error mapping
- Audit behaviour/service
- Dependency task/logistics handlers/entities
- Existing UC-106 tests
- Existing authentication tests

Frontend:
- Department Management page
- Department table row/actions
- Toggle component
- Status confirmation modal
- Department API service
- Department types/interfaces
- Auth interceptor
- Login error renderer
- Locale files nếu UI đã dùng i18n
```

---

# 30. Implementation strategy đề xuất

## Phase 1 — Audit source

Xuất báo cáo current state:

```text
- Endpoint hiện tại.
- Handler hiện tại.
- Có blocker active users hay không.
- Có session revoke service hay không.
- SessionValidationMiddleware kiểm tra gì.
- Login handlers đang kiểm tra gì.
- Refresh token đang kiểm tra gì.
- Frontend toggle hiện gọi API nào.
- Test hiện tại đang kỳ vọng gì.
```

Không sửa trong Phase 1 nếu người dùng yêu cầu review trước.

## Phase 2 — Backend UC-106

- Bỏ `ACTIVE_USERS` khỏi hard blockers.
- Tính affected accounts.
- Revoke sessions.
- Giữ nguyên users.status.
- Response trả count.
- Audit.
- Transaction.

## Phase 3 — Authentication

- Shared eligibility check.
- Credentials login.
- Google SSO.
- Refresh token.
- Session validation compatibility.

## Phase 4 — Frontend

- Modal text mới.
- Impact count.
- Blocker rendering.
- Success message.
- Error mapping.

## Phase 5 — Tests

- Unit tests.
- Integration tests.
- Auth tests.
- Transaction tests.
- Frontend tests nếu project có.

## Phase 6 — Verification

- Backend build.
- Frontend typecheck/build/lint.
- Unit tests.
- Integration tests với MySQL test thật.
- Manual API verification.
- Manual UI verification.

---

# 31. Definition of Done

UC-106 mới chỉ được coi là hoàn thành khi:

- Staff Leader chỉ thao tác đúng campus scope.
- IC department không thể toggle bằng UI hoặc direct API.
- GENERAL department enable/disable hoạt động với DB thật.
- Active users không còn block disable.
- `users.status` không bị đổi.
- Session của đúng Department Leader và Department Staff bị revoke.
- Session của role khác không bị revoke.
- Credentials login kiểm tra department status.
- Google SSO kiểm tra department status.
- Refresh token kiểm tra department status.
- Access token của revoked session bị từ chối.
- Enable không khôi phục session cũ.
- User bị khóa riêng không được kích hoạt nhầm.
- Inactive department bị loại khỏi mọi lựa chọn phân công mới.
- Open dependency blocker dùng field/enum thật.
- Update department, revoke session và audit cùng transaction.
- Audit lưu before/after.
- No-op không cập nhật audit fields.
- Unit tests pass.
- Integration tests trên MySQL test thật pass.
- Backend build 0 error.
- Frontend build 0 error.
- Frontend lint 0 error nếu project dùng lint.
- Không dùng mock data để chứng minh nghiệp vụ.
- Không tạo field/table/enum ngoài schema.
- Báo cáo cuối cùng nêu rõ file đã sửa và test đã chạy thật.

---

# 32. Output report bắt buộc của AI Agent

Sau khi triển khai, AI Agent phải trả báo cáo theo format:

```text
1. Current-state findings
2. Root cause
3. Business rule changes
4. Backend files changed
5. Authentication files changed
6. Frontend files changed
7. Database/schema impact
8. Tests added/updated
9. Commands executed
10. Actual results
11. Remaining risks
12. Manual verification steps
```

Không được báo:

```text
Hoàn thành
Build pass
Test pass
```

nếu chưa thực sự chạy.

Nếu không chạy được phải ghi:

```text
- Lệnh đã thử.
- Lỗi thực tế.
- Nguyên nhân.
- Phần nào mới chỉ được kiểm tra tĩnh.
```

---

# 33. Quyết định nghiệp vụ cuối cùng

Baseline mới của UC-106 là:

```text
Disable GENERAL department
→ không đổi users.status
→ active users không phải blocker
→ open business dependencies vẫn có thể block
→ department chuyển INACTIVE
→ revoke session của DEPARTMENT + LEADER/STAFF đúng department
→ chặn credentials login
→ chặn Google SSO
→ chặn refresh token
→ giữ dữ liệu lịch sử
→ enable lại chỉ cho phép login mới với user ACTIVE
→ không khôi phục session cũ
```

Đây là logic phải được triển khai thống nhất ở backend, authentication, session, frontend và test.
