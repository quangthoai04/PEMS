# PEMS — MASTER IMPLEMENTATION PROMPT  
## Làm lại toàn bộ chức năng Quản lý nhân sự phòng ban dành cho DEPARTMENT LEADER

> **Mục đích tài liệu**  
> Đây là prompt bàn giao tự chứa dành cho AI Agent/Developer. Người tiếp nhận phải đọc tài liệu này, kiểm tra code thật trên repository, sau đó triển khai đầy đủ chức năng quản lý nhân sự phòng ban cho **Department Leader** từ Backend, Frontend, Database mapping, Validation, Security, Email, Audit đến Test.
>
> Không được chỉ sửa giao diện hoặc vá từng lỗi rời rạc. Mục tiêu là tạo một luồng hoàn chỉnh, an toàn, nhất quán và chạy được trên codebase PEMS hiện tại.

---

# 1. Vai trò của AI Agent

Bạn là:

- Senior .NET/C# Clean Architecture Engineer.
- Senior React/TypeScript Engineer.
- MySQL Database-first Engineer.
- Security/RBAC/IDOR Reviewer.
- Email identity and authentication flow reviewer.
- QA Engineer phụ trách unit test, integration test và real-stack verification.

Bạn phải làm việc theo chuỗi đầy đủ:

```text
Codebase preflight
→ xác định source of truth
→ authorization/scope
→ DTO/Validator
→ Handler/Service
→ Controller/API
→ Entity/EF mapping nếu cần
→ Email/Auth provider/Session
→ Frontend API/types/hooks/page/modal
→ Unit test
→ Integration test
→ Frontend build
→ Real-stack/manual verification
→ Báo cáo kết quả
```

Không được báo hoàn thành khi mới sửa một layer.

---

# 2. Repository và branch

Repository:

```text
quangthoai04/PEMS
```

Branch cần làm việc:

```text
Duy-Iter1
```

Trước khi sửa code, bắt buộc:

```bash
git status
git branch --show-current
git log -5 --oneline
git rev-parse HEAD
git rev-parse origin/Duy-Iter1
```

Quy tắc Git:

- Không `reset --hard`.
- Không rebase hoặc rewrite history.
- Không checkout sang branch khác nếu chưa được yêu cầu.
- Không xóa WIP của người khác.
- Không merge/push tự động nếu chưa được chủ dự án cho phép.
- Nếu working tree có thay đổi sẵn, phải đọc và bảo toàn.
- Báo cáo rõ HEAD trước và sau khi làm.

---

# 3. Phạm vi chức năng cần hoàn thiện

Department Leader phải quản lý được nhân sự thuộc **chính phòng ban GENERAL của mình**, gồm:

1. Xem thông tin phòng ban của mình.
2. Lấy danh sách nhân sự phòng ban.
3. Tìm kiếm nhân sự.
4. Lọc nhân sự theo trạng thái.
5. Phân trang.
6. Thêm nhân sự mới.
7. Xem chi tiết nhân sự.
8. Chỉnh sửa thông tin nhân sự:
   - Họ và tên.
   - Email.
   - Số điện thoại.
   - Giới tính.
9. Cho phép sửa email **bất cứ lúc nào**, không phụ thuộc trạng thái tài khoản.
10. Vô hiệu hóa nhân sự.
11. Kích hoạt lại nhân sự.
12. Xem trước ảnh hưởng trước khi đổi trạng thái.
13. Đổi trưởng phòng.
14. Thu hồi session khi thay đổi danh tính/quyền.
15. Gửi email đúng nghiệp vụ.
16. Ghi audit log.
17. Có unit test, integration test và frontend verification đầy đủ.

---

# 4. Quy tắc role/sub-role chuẩn

Không tạo role mới.

Mapping runtime chuẩn:

| Người dùng | `role_code` | `sub_role` | Department type |
|---|---|---|---|
| Department Leader | `DEPARTMENT` | `LEADER` | `GENERAL` |
| Department Staff | `DEPARTMENT` | `STAFF` | `GENERAL` |

Không dùng các giá trị:

```text
DEPT
DEPARTMENT_LEADER
DEPT_LEADER
DEPT_L
DEPT_P
LEADER như role_code
```

Department Leader hợp lệ phải thỏa mãn:

```text
users.role_code = DEPARTMENT
users.sub_role = LEADER
users.status = ACTIVE
users.department_id IS NOT NULL
departments.department_type = GENERAL
departments.status = ACTIVE
departments.head_user_id = currentUser.user_id
departments.department_id = currentUser.department_id
```

Chỉ kiểm tra JWT có `DEPARTMENT + LEADER` là chưa đủ. Backend phải đọc lại DB để xác nhận người đang đăng nhập vẫn là `head_user_id` hiện tại.

---

# 5. Lỗ hổng và lỗi hiện tại phải được loại bỏ

AI Agent phải rà soát code thật và xử lý tối thiểu các vấn đề sau:

## 5.1. IDOR/BOLA theo `departmentId`

Không được tin `departmentId` từ:

- URL.
- Query string.
- Request body.
- Local storage.
- State frontend.

Các API dành riêng cho Department Leader phải tự lấy:

```text
currentUser.UserId
currentUser.DepartmentId
currentUser.PrimaryCampusId
```

sau đó xác minh lại bằng DB.

Department Leader phòng A không được:

- Xem nhân sự phòng B.
- Sửa nhân sự phòng B.
- Enable/disable nhân sự phòng B.
- Đổi trưởng phòng B.
- Thêm người vào phòng B.

## 5.2. Endpoint thiếu authorization

Mọi endpoint mới phải có:

```csharp
[Authorize]
[RoleAuthorize(EffectiveRole.DepartmentLead)]
```

Handler vẫn phải kiểm tra scope lần cuối.

Không dựa hoàn toàn vào frontend ẩn nút.

## 5.3. Frontend dùng dữ liệu từ `localStorage` để quyết định quyền

Frontend chỉ được dùng role để điều hướng sơ bộ.

Nguồn quyết định cuối cùng phải là:

- Backend authorization.
- Action flags do backend trả về.
- Scope kiểm tra trong handler.

## 5.4. Gender mapping sai

API dùng giá trị chuẩn:

```text
MALE
FEMALE
OTHER
```

Frontend label:

```text
MALE   → Nam
FEMALE → Nữ
OTHER  → Khác
```

Không gửi `"Nam"`, `"Nữ"`, `"Khác"` xuống backend.

Không để trường hợp mở modal rồi bấm Lưu làm `Male/Female` bị chuyển nhầm thành `Other`.

## 5.5. Email nhìn như sửa được nhưng backend không lưu

Phải sửa triệt để. Email trong modal edit phải hoạt động thật.

## 5.6. Frontend luôn báo thành công

Frontend phải đọc:

```text
success
message
emailNotificationStatus
changed
emailChanged
```

Không hiển thị thành công chỉ vì HTTP 200.

## 5.7. Hard-code thông tin phòng ban

Không hard-code:

```text
Phòng ban IT
```

Tên phòng ban, campus, trưởng phòng và thống kê phải đến từ backend.

## 5.8. API chi tiết không đúng role

Không tái sử dụng endpoint chỉ dành cho Staff Leader nếu handler không hỗ trợ Department Leader.

Tạo endpoint riêng hoặc sửa scope rõ ràng.

---

# 6. Kiến trúc mục tiêu

Nên tạo controller chuyên biệt:

```text
DepartmentLeaderPersonnelController
```

Route gợi ý:

```text
/api/department-leader
```

Endpoints:

```http
GET    /api/department-leader/department
GET    /api/department-leader/personnel
GET    /api/department-leader/personnel/{userId}
POST   /api/department-leader/personnel
PUT    /api/department-leader/personnel/{userId}
GET    /api/department-leader/personnel/{userId}/status-impact
PATCH  /api/department-leader/personnel/{userId}/status
GET    /api/department-leader/leader-candidates
POST   /api/department-leader/transfer-leadership
POST   /api/department-leader/personnel/{userId}/resend-email-confirmation
```

Không bắt buộc giữ đúng tên route trên nếu convention của repository yêu cầu khác, nhưng phải đảm bảo:

- Không nhận `departmentId` từ frontend.
- API có naming rõ ràng.
- Không dùng tên `RemovePersonnel` cho enable/disable.
- Controller chỉ nhận request, gọi MediatR và trả response.
- Business logic nằm trong handler/service.

---

# 7. Scope service dùng chung

Tạo hoặc mở rộng service dùng chung, ví dụ:

```text
IDepartmentLeaderPersonnelScopeService
```

Các hàm cần có về mặt ý nghĩa:

```text
ResolveCurrentDepartmentAsync()
EnsureCurrentUserIsActualDepartmentLeaderAsync()
GetScopedPersonnelAsync(userId)
EnsureTargetBelongsToCurrentDepartmentAsync(userId)
```

Service phải kiểm tra:

```text
Actor đã đăng nhập
Actor DEPARTMENT + LEADER
Actor ACTIVE
Actor có department_id
Department tồn tại
Department GENERAL
Department ACTIVE
Department.head_user_id = actor.user_id
Target role = DEPARTMENT
Target.department_id = actor.department_id
Target.primary_campus_id phù hợp department.campus_id
```

Không để mỗi handler tự viết một phiên bản scope khác nhau.

---

# 8. Chức năng 1 — Xem thông tin phòng ban

## API

```http
GET /api/department-leader/department
```

## Backend

Tự lấy department của actor.

Response gợi ý:

```json
{
  "departmentId": 15,
  "departmentName": "Phòng Đào tạo",
  "departmentType": "GENERAL",
  "departmentStatus": "ACTIVE",
  "campusId": 1,
  "campusName": "FPT University Hà Nội",
  "currentLeaderUserId": 1001,
  "currentLeaderName": "Nguyễn Văn A",
  "totalPersonnelCount": 12,
  "activePersonnelCount": 9,
  "inactivePersonnelCount": 1,
  "pendingEmailConfirmationCount": 1,
  "lockedPersonnelCount": 1
}
```

## Frontend

Màn hình không lấy tên phòng ban từ local storage.

Hiển thị:

- Tên phòng ban.
- Campus.
- Trưởng phòng hiện tại.
- Tổng nhân sự.
- Số ACTIVE.
- Số INACTIVE.
- Số PENDING.
- Số LOCKED.

---

# 9. Chức năng 2 — Danh sách, search, filter, pagination

## API

```http
GET /api/department-leader/personnel
```

Query:

```text
keyword
status
page
pageSize
sortBy
sortDirection
```

Không có `departmentId`.

Ví dụ:

```http
GET /api/department-leader/personnel?keyword=dao&status=ACTIVE&page=1&pageSize=10
```

## Search

Tìm theo:

```text
full_name
email
phone
```

Quy tắc:

- Trim keyword.
- Tối đa 100 ký tự.
- Không phân biệt hoa/thường.
- Scope department phải được áp dụng trước keyword.
- Search và filter dùng `AND`.
- Query read-only dùng `AsNoTracking()`.

## Filter status

Các giá trị:

```text
ALL
ACTIVE
INACTIVE
PENDING_EMAIL_CONFIRMATION
LOCKED
```

Không gom `INACTIVE` và `LOCKED`.

## Sort

Mặc định:

```text
Trưởng phòng hiện tại lên đầu
→ full_name tăng dần
→ user_id tăng dần để ổn định phân trang
```

## Response

```json
{
  "items": [
    {
      "userId": 1001,
      "fullName": "Nguyễn Văn A",
      "email": "a@fpt.edu.vn",
      "phone": "0912345678",
      "gender": "MALE",
      "status": "ACTIVE",
      "subRole": "LEADER",
      "position": "Trưởng phòng",
      "avatarUrl": null,
      "departmentName": "Phòng Đào tạo",
      "campusName": "FPT University Hà Nội",
      "createdAt": "2026-07-27T10:00:00+07:00",
      "canView": true,
      "canEdit": true,
      "canDisable": false,
      "canEnable": false,
      "canTransferLeadershipTo": false
    }
  ],
  "page": 1,
  "pageSize": 10,
  "totalItems": 12,
  "totalPages": 2,
  "hasNextPage": true,
  "hasPreviousPage": false
}
```

## Frontend

- Debounce search khoảng 300–500 ms.
- Đổi search/filter/pageSize thì reset page về 1.
- Có loading, empty, no-result và error state riêng.
- Không tải toàn bộ danh sách rồi filter phía client.
- Candidate đổi trưởng phòng không lấy từ danh sách phân trang hiện tại.

---

# 10. Chức năng 3 — Thêm nhân sự mới

## API

```http
POST /api/department-leader/personnel
```

Request:

```json
{
  "fullName": "Nguyễn Văn B",
  "email": "b@fpt.edu.vn",
  "phone": "0912345678",
  "gender": "MALE"
}
```

Frontend không gửi:

```text
roleCode
subRole
departmentId
campusId
status
createdVia
```

Backend tự gán:

```text
role_code = DEPARTMENT
sub_role = STAFF
department_id = current leader department
primary_campus_id = department campus
status = PENDING_EMAIL_CONFIRMATION
created_via = MANUAL_CREATED
```

Department Leader không được tạo thêm một `LEADER`.

## Validation

Họ tên:

- Bắt buộc.
- Normalize khoảng trắng.
- Độ dài theo shared account identity rules.

Email:

- Bắt buộc.
- Normalize lowercase + trim.
- Đúng định dạng.
- Không trùng user khác.
- Không trùng auth identity khác.

Phone:

- Bắt buộc nếu business rule hiện hành yêu cầu.
- Validate theo shared phone rules.

Gender:

```text
MALE
FEMALE
OTHER
```

Department/campus:

- Department ACTIVE.
- Campus ACTIVE.
- Department GENERAL.
- Actor vẫn là head hiện tại.

## Transaction

```text
Lock actor/department khi cần
→ kiểm tra email uniqueness
→ tạo user
→ tạo confirmation token
→ chỉ lưu token hash
→ audit
→ SaveChanges
→ Commit
→ gửi email sau commit
```

## Email

Tài khoản mới nhận email xác nhận.

Response phải báo đúng:

```text
SENT
SKIPPED
FAILED
```

Nếu gửi email thất bại:

- Account vẫn được tạo.
- Status vẫn `PENDING_EMAIL_CONFIRMATION`.
- Frontend hiển thị cảnh báo.
- Cho phép gửi lại email xác nhận.

---

# 11. Chức năng 4 — Xem chi tiết nhân sự

## API

```http
GET /api/department-leader/personnel/{userId}
```

## Scope

Chỉ trả nếu target:

```text
role_code = DEPARTMENT
department_id = current actor department
```

Target ngoài scope nên trả 404 hoặc 403 theo convention chống lộ tồn tại của project. Chọn một convention và dùng nhất quán.

## Dữ liệu trả về

```json
{
  "userId": 1002,
  "fullName": "Nguyễn Văn B",
  "email": "b@fpt.edu.vn",
  "phone": "0912345678",
  "gender": "MALE",
  "status": "ACTIVE",
  "roleCode": "DEPARTMENT",
  "subRole": "STAFF",
  "position": "Nhân viên",
  "avatarUrl": null,
  "departmentId": 15,
  "departmentName": "Phòng Đào tạo",
  "campusId": 1,
  "campusName": "FPT University Hà Nội",
  "createdAt": "2026-07-27T10:00:00+07:00",
  "updatedAt": "2026-07-27T10:00:00+07:00",
  "lastLoginAt": null,
  "canEdit": true,
  "canDisable": true,
  "canEnable": false,
  "canTransferLeadershipTo": true,
  "canResendEmailConfirmation": false
}
```

Không trả:

- Password hash.
- Raw token.
- Refresh token.
- Auth provider subject không cần thiết.
- Security internals.

Frontend phải gọi API detail thật khi mở modal, không dùng dữ liệu thiếu từ list row làm nguồn duy nhất.

---

# 12. Chức năng 5 — Chỉnh sửa thông tin nhân sự

## 12.1. Quyết định nghiệp vụ đã chốt

Department Leader được chỉnh sửa:

```text
fullName
email
phone
gender
```

Email được sửa **bất cứ lúc nào**, kể cả target đang:

```text
PENDING_EMAIL_CONFIRMATION
ACTIVE
INACTIVE
LOCKED
```

Đổi email không tự đổi trạng thái:

```text
PENDING → vẫn PENDING
ACTIVE  → vẫn ACTIVE
INACTIVE → vẫn INACTIVE
LOCKED → vẫn LOCKED
```

Đổi email không:

- Tự kích hoạt.
- Tự mở khóa.
- Tự vô hiệu hóa.
- Thay role.
- Thay subRole.
- Thay department.
- Thay campus.

## 12.2. API

```http
PUT /api/department-leader/personnel/{userId}
```

Request:

```json
{
  "fullName": "Nguyễn Văn B",
  "email": "new.b@fpt.edu.vn",
  "phone": "0987654321",
  "gender": "MALE"
}
```

Nên dùng một command duy nhất để toàn bộ thay đổi hồ sơ và identity được xử lý atomically.

## 12.3. Lock và concurrency

Dùng service lock hiện có nếu repository đã có:

```text
IUserMutationLockService
```

Thứ tự lock phải nhất quán để tránh deadlock.

Tối thiểu lock:

- Target user.
- Department nếu cần xác minh head.
- Các user liên quan trong leadership flow.

Sau lock phải đọc lại target và actor scope.

## 12.4. Trường hợp email không đổi

So sánh sau normalize.

Nếu email không đổi:

- Không xóa auth provider.
- Không revoke session.
- Không tạo confirmation token.
- Không gửi email đổi địa chỉ.
- Chỉ cập nhật các field khác thực sự thay đổi.

Response:

```json
{
  "success": true,
  "changed": true,
  "emailChanged": false,
  "message": "Đã cập nhật thông tin nhân sự."
}
```

Nếu không có field nào đổi:

```json
{
  "success": true,
  "changed": false,
  "emailChanged": false,
  "message": "Không có thông tin nào thay đổi."
}
```

## 12.5. Validation email mới

- Normalize lowercase + trim.
- Validate định dạng.
- Kiểm tra độ dài.
- Không trùng `users.email` của user khác.
- Không xung đột với auth provider identity của user khác.
- DB unique constraint vẫn là lớp bảo vệ cuối.
- Race conflict phải map sang 409 rõ ràng.

## 12.6. Email change với tài khoản PENDING

Khi:

```text
status = PENDING_EMAIL_CONFIRMATION
```

thực hiện:

```text
1. Cập nhật users.email.
2. Giữ nguyên status PENDING_EMAIL_CONFIRMATION.
3. Chuyển confirmation token PENDING cũ thành SUPERSEDED.
4. Tạo token mới gắn với email mới.
5. Chỉ lưu token hash.
6. Reset resend_count theo rule edit-email hiện hành.
7. Gửi link xác nhận tới email mới sau commit.
8. Gửi thông báo trung lập tới email cũ.
9. Link cũ không còn dùng được.
```

Không tạo user mới.

## 12.7. Email change với tài khoản ACTIVE

Thực hiện:

```text
1. Cập nhật users.email.
2. Giữ nguyên ACTIVE.
3. Xóa Google SSO/FEID provider rows cũ.
4. Local password provider giữ password hash, cập nhật provider_email.
5. user.email_verified_at = NULL.
6. Supersede mọi pending confirmation token bất thường còn sót.
7. Thu hồi toàn bộ active sessions.
8. Gửi thông báo trung lập tới email cũ.
9. Gửi thông tin đăng nhập mới tới email mới.
10. Lần đăng nhập SSO tiếp theo sẽ liên kết provider mới.
```

Không tự tạo session mới.

## 12.8. Email change với tài khoản INACTIVE

Thực hiện cùng identity reset như ACTIVE:

- Cập nhật email.
- Xử lý auth providers.
- Revoke session còn sót.
- Gửi thông báo.
- Giữ nguyên `INACTIVE`.

Tài khoản vẫn không đăng nhập được cho đến khi được enable bằng chức năng status riêng.

## 12.9. Email change với tài khoản LOCKED

Theo quyết định đã chốt, vẫn được sửa email.

Thực hiện:

- Cập nhật email.
- Xử lý auth providers.
- Revoke session.
- Gửi thông báo.
- Giữ nguyên `LOCKED`.
- Không reset lock reason.
- Không reset failed login count nếu có.
- Không mở khóa.

## 12.10. Auth providers

Không chỉ set:

```csharp
user.Email = newEmail;
```

Phải xử lý:

### Google SSO / FEID

- Xóa provider row cũ.
- Không sửa `provider_subject` cũ sang email mới.
- Cho phép re-link trong lần đăng nhập tiếp theo.

### Local password

- Giữ password hash.
- Cập nhật provider email.
- Đổi email không đổi mật khẩu.

## 12.11. Session revocation

Bất kỳ email change nào cũng phải revoke toàn bộ session.

Response trả:

```text
revokedSessions
```

Không để JWT/session cũ tiếp tục sử dụng email cũ.

## 12.12. Email notifications

Email cũ:

- Nội dung trung lập.
- Không tiết lộ email mới.
- Không tiết lộ tên, role, campus, department nếu email cũ có thể là người nhận nhầm.
- Không chứa token mới.

Email mới:

- Với PENDING: link xác nhận mới.
- Với ACTIVE/INACTIVE/LOCKED: thông báo email đăng nhập đã thay đổi.
- Nội dung lấy từ DB snapshot, không tin request.
- HTML encode toàn bộ giá trị.

Kết quả:

```text
SENT
PARTIAL
FAILED
SKIPPED
NOT_REQUIRED
```

Gửi email sau commit. Email lỗi không rollback identity change.

## 12.13. Modal xác nhận frontend

Nếu email thay đổi, hiển thị:

```text
Email hiện tại
Email mới
Trạng thái tài khoản
Hậu quả:
- đăng xuất khỏi tất cả thiết bị;
- phải dùng email mới để đăng nhập;
- trạng thái tài khoản giữ nguyên.
```

Nếu target PENDING:

```text
Link xác nhận cũ sẽ hết hiệu lực và link mới sẽ được gửi.
```

## 12.14. Response gợi ý

```json
{
  "success": true,
  "userId": 1002,
  "fullName": "Nguyễn Văn B",
  "email": "new.b@fpt.edu.vn",
  "phone": "0987654321",
  "gender": "MALE",
  "status": "ACTIVE",
  "changed": true,
  "emailChanged": true,
  "confirmationReissued": false,
  "authenticationRelinkRequired": true,
  "revokedSessions": 2,
  "emailNotificationStatus": "SENT",
  "message": "Đã cập nhật thông tin nhân sự. Email đăng nhập đã thay đổi và các phiên hiện tại đã bị thu hồi."
}
```

---

# 13. Chức năng 6 — Gửi lại email xác nhận

## API

```http
POST /api/department-leader/personnel/{userId}/resend-email-confirmation
```

Chỉ cho target:

```text
status = PENDING_EMAIL_CONFIRMATION
```

Backend:

- Kiểm tra actor scope.
- Lấy email hiện tại từ DB.
- Không nhận email từ frontend.
- Áp dụng cooldown.
- Áp dụng max resend.
- Supersede token cũ.
- Tạo token mới.
- Gửi email.
- Trả status trung thực.

---

# 14. Chức năng 7 — Xem trước ảnh hưởng enable/disable

## API

```http
GET /api/department-leader/personnel/{userId}/status-impact?targetStatus=INACTIVE
```

## Mục đích

Không đổi trạng thái ngay khi user bấm toggle.

Backend phải kiểm tra:

- Target có thuộc scope.
- Target có phải chính actor không.
- Target có phải leader hiện tại không.
- Target đang giữ nhiệm vụ chưa kết thúc không.
- Target có đang làm participant/support/assignee cho nghiệp vụ active không.
- Có active sessions không.
- Department/campus có cho phép enable không.

Response:

```json
{
  "userId": 1002,
  "currentStatus": "ACTIVE",
  "targetStatus": "INACTIVE",
  "canChangeStatus": false,
  "activeSessionCount": 2,
  "blockers": [
    {
      "code": "PERSONNEL_HAS_ACTIVE_RESPONSIBILITIES",
      "count": 3,
      "message": "Nhân sự đang có 3 nhiệm vụ chưa hoàn thành."
    }
  ],
  "warnings": []
}
```

Không tự động reassignment nhiệm vụ khi disable.

---

# 15. Chức năng 8 — Disable/Enable nhân sự

## API

```http
PATCH /api/department-leader/personnel/{userId}/status
```

Request:

```json
{
  "targetStatus": "INACTIVE",
  "reason": "Nhân sự đã chuyển công tác."
}
```

## Transition cho phép

```text
ACTIVE   → INACTIVE
INACTIVE → ACTIVE
```

Không gộp `LOCKED` vào enable.

Không cho:

```text
LOCKED → ACTIVE bằng chức năng này
PENDING_EMAIL_CONFIRMATION → ACTIVE bằng chức năng này
```

PENDING phải xác nhận email.

LOCKED cần flow bảo mật/mở khóa riêng.

## Blockers

Không cho Department Leader:

- Tự disable chính mình.
- Disable trưởng phòng hiện tại.
- Disable target ngoài phòng ban.
- Disable target có trách nhiệm active theo rule chốt.
- Enable khi department INACTIVE.
- Enable khi campus INACTIVE.
- Enable tài khoản PENDING.
- Enable tài khoản LOCKED.

## Khi disable

- `status = INACTIVE`.
- Ghi reason.
- Revoke all sessions.
- Audit.
- Gửi thông báo/email.
- Không xóa user khỏi department.
- Không hard delete.

## Khi enable

- `status = ACTIVE`.
- Không tự khôi phục session.
- Người dùng đăng nhập lại.
- Audit.
- Gửi thông báo nếu policy yêu cầu.

Tên command/API không dùng `RemovePersonnel`.

---

# 16. Chức năng 9 — Đổi trưởng phòng

## API candidates

```http
GET /api/department-leader/leader-candidates
```

Candidate phải thỏa:

```text
role_code = DEPARTMENT
sub_role = STAFF
status = ACTIVE
department_id = current department
primary_campus_id = department campus
```

Không dùng list page hiện tại làm nguồn candidates.

## API transfer

```http
POST /api/department-leader/transfer-leadership
```

Request:

```json
{
  "newLeaderUserId": 1003
}
```

Không có `departmentId`.

## Transaction bắt buộc

Thứ tự:

```text
1. Xác thực actor hiện tại.
2. Begin transaction.
3. Lock actor user.
4. Lock candidate user.
5. Lock department.
6. Đọc lại actor, candidate, department sau lock.
7. Xác nhận department.head_user_id vẫn là actor.
8. Xác nhận candidate vẫn ACTIVE + DEPARTMENT + STAFF + đúng department.
9. Old leader: sub_role LEADER → STAFF.
10. New leader: sub_role STAFF → LEADER.
11. Department.head_user_id = newLeaderUserId.
12. Audit.
13. SaveChanges.
14. Commit.
15. Revoke sessions của old leader và new leader.
16. Gửi thông báo/email sau commit.
```

Không có thời điểm department:

- Không có leader.
- Có hai leader.

Nếu hai request đồng thời:

- Chỉ một request được commit.
- Request còn lại trả conflict rõ ràng.

## Sau transfer

Old leader:

```text
DEPARTMENT + STAFF
```

New leader:

```text
DEPARTMENT + LEADER
```

Cả hai phải đăng nhập lại để nhận JWT mới.

Frontend của old leader:

```text
Hiển thị thành công
→ logout
→ chuyển về login hoặc trang phù hợp
```

Không trả raw exception message.

---

# 17. Frontend mục tiêu

Route dành cho Department Leader nên là:

```text
/dashboard/my-department
```

Không nên phụ thuộc:

```text
/dashboard/departments/:id
```

Nếu giữ route cũ, backend vẫn không tin ID.

## Màn hình gồm

```text
Breadcrumb
Thông tin phòng ban
Thống kê nhân sự
Search
Status filter
Nút Thêm nhân sự
Bảng nhân sự
Pagination
Modal thêm
Modal detail/edit
Modal status impact
Modal confirm email change
Modal transfer leadership
```

## Table columns

```text
STT
Họ và tên
Email
Trạng thái
Chức vụ
Hành động
```

## Action flags

Frontend dùng flags backend:

```text
canView
canEdit
canDisable
canEnable
canTransferLeadershipTo
canResendEmailConfirmation
```

Backend vẫn kiểm tra lại.

## Trạng thái UI

| Backend | Label |
|---|---|
| `ACTIVE` | Hoạt động |
| `INACTIVE` | Vô hiệu hóa |
| `PENDING_EMAIL_CONFIRMATION` | Chờ xác nhận email |
| `LOCKED` | Bị khóa |

## Loading/error

Mỗi mutation:

- Disable button trong khi submit.
- Không gửi double click.
- Hiển thị spinner.
- Không đóng modal trước response.
- Hiển thị message backend.
- Phân biệt failed email với failed database.

---

# 18. Validation

## Frontend

Frontend validation chỉ để phản hồi sớm.

Backend vẫn validate toàn bộ.

## Backend validators

Cần validator cho:

- List query.
- Create personnel.
- Update personnel.
- Status impact.
- Change status.
- Transfer leadership.
- Resend confirmation.

Không để validator TODO rỗng.

## Error codes gợi ý

```text
DEPARTMENT_LEADER_REQUIRED
DEPARTMENT_CONTEXT_MISSING
DEPARTMENT_NOT_ACTIVE
DEPARTMENT_SCOPE_FORBIDDEN

PERSONNEL_NOT_FOUND
PERSONNEL_SCOPE_FORBIDDEN
PERSONNEL_EMAIL_ALREADY_EXISTS
PERSONNEL_INVALID_STATUS
PERSONNEL_SELF_DISABLE_FORBIDDEN
CURRENT_LEADER_DISABLE_FORBIDDEN
PERSONNEL_HAS_ACTIVE_RESPONSIBILITIES
PERSONNEL_EMAIL_CONFIRMATION_PENDING
PERSONNEL_SECURITY_LOCKED

EMAIL_UNCHANGED
INVALID_EMAIL
ACCOUNT_EMAIL_ALREADY_EXISTS
AUTH_IDENTITY_CONFLICT

LEADER_CANDIDATE_INVALID
LEADER_CANDIDATE_NOT_ACTIVE
LEADER_CANDIDATE_WRONG_DEPARTMENT
LEADERSHIP_ALREADY_CHANGED
LEADERSHIP_TRANSFER_CONFLICT

RESEND_TOO_SOON
RESEND_LIMIT_REACHED
```

Dùng exception middleware chuẩn.

Không `catch (Exception ex)` rồi trả `ex.Message` cho client.

---

# 19. Audit log

Các action bắt buộc:

```text
ADD_DEPARTMENT_PERSONNEL
VIEW_DEPARTMENT_PERSONNEL_DETAIL nếu policy audit read yêu cầu
UPDATE_DEPARTMENT_PERSONNEL
UPDATE_DEPARTMENT_PERSONNEL_IDENTITY
CORRECT_PENDING_DEPARTMENT_PERSONNEL_EMAIL
DISABLE_DEPARTMENT_PERSONNEL
ENABLE_DEPARTMENT_PERSONNEL
RESEND_DEPARTMENT_PERSONNEL_CONFIRMATION
TRANSFER_DEPARTMENT_LEADERSHIP
```

Audit lưu:

```text
ActorUserId
TargetUserId
CampusId
DepartmentId
Action
Old values
New values
Reason
EmailChanged
ConfirmationReissued
AuthenticationRelinkRequired
RevokedSessions
Timestamp
```

Email trong audit nên mask nếu policy yêu cầu.

Không lưu:

- Raw token.
- Password hash.
- JWT.
- Refresh token.
- Email HTML đầy đủ.

Không sửa dữ liệu lịch sử cũ khi user đổi email.

---

# 20. Database và EF

Project là database-first.

Không tự thêm migration bừa.

Trước khi sửa schema:

- Kiểm tra canonical SQL.
- Kiểm tra bảng/cột/constraint hiện có.
- Chỉ tạo SQL patch khi thật sự thiếu.
- Không tạo bảng mới nếu có thể dùng `users`, `user_auth_providers`, `sessions`, `account_email_confirmations`, `audit_logs`.

Kiểm tra:

- Unique constraint trên email.
- Index phục vụ `department_id + status`.
- FK users.department_id.
- FK departments.head_user_id.
- Status enum thực tế là `LOCKED` hay `LOCK`; dùng đúng constant/schema hiện hành, không đoán.
- EF mappings phải khớp schema.

---

# 21. Test bắt buộc

## 21.1. Authorization/scope

```text
Anonymous → 401
Department Staff → 403
Department Leader không còn là head_user_id → 403
Department Leader phòng A list/detail/update/status/transfer phòng B → 403/404
HO/Staff Leader gọi endpoint riêng này → 403
```

## 21.2. List/search/filter

```text
Chỉ trả DEPARTMENT users cùng department
Không trả role khác
Search theo tên
Search theo email
Search theo phone
Filter ACTIVE
Filter INACTIVE
Filter PENDING
Filter LOCKED
Search + filter AND
Pagination total đúng
Leader lên đầu
```

## 21.3. Create

```text
Tạo DEPARTMENT + STAFF
Server tự gán department/campus
Status bắt đầu PENDING
Email trùng → 409
Department inactive → reject
Campus inactive → reject
Email SENT/FAILED/SKIPPED đúng
Email lỗi không rollback account
Không tạo thêm leader
```

## 21.4. Detail

```text
View đúng target cùng phòng
Target phòng khác không được xem
Không lộ password/token/provider subject
Action flags đúng
```

## 21.5. Update profile

```text
Sửa name
Sửa phone
Sửa gender
Gender không bị map sai
No-op update
Không sửa role/subRole/department/status
```

## 21.6. Update email mọi trạng thái

### Common

```text
Email invalid → 400
Email duplicate → 409
Email same after normalize → no identity reset
Department scope enforced
All sessions revoked when email changes
Old email notice does not leak new email
Email failure does not rollback
```

### PENDING

```text
Status vẫn PENDING
Old token SUPERSEDED
New token PENDING
New token target_email = new email
Raw token không lưu DB
Old link fail
New link works
```

### ACTIVE

```text
Status vẫn ACTIVE
SSO/FEID old provider removed
Local provider email updated
email_verified_at cleared
Sessions revoked
Login with old email fails
New email can re-link on next login
```

### INACTIVE

```text
Status vẫn INACTIVE
Email đổi thành công
Không đăng nhập được cho tới khi enable
```

### LOCKED

```text
Status vẫn LOCKED
Email đổi thành công
Không tự mở khóa
Không reset security lock metadata
```

## 21.7. Status

```text
ACTIVE → INACTIVE
INACTIVE → ACTIVE
Self-disable blocked
Current leader disable blocked
PENDING enable blocked
LOCKED enable blocked
Active responsibilities blocked
Disable revokes sessions
```

## 21.8. Transfer leadership

```text
Valid candidate success
Candidate other department blocked
Candidate inactive blocked
Candidate pending blocked
Candidate locked blocked
Concurrent transfer only one success
Old leader STAFF
New leader LEADER
department.head_user_id correct
Both sessions revoked
No headless/two-head state
```

## 21.9. Frontend

```text
Search debounce
Filter resets page
Detail calls API
Email modal confirm
Status impact modal
Truthful email delivery toast
No hard-coded department
No TypeScript errors
Responsive desktop/mobile
```

---

# 22. Real-stack verification

Sau unit/integration test, chạy real stack trên disposable DB.

Journey tối thiểu:

```text
1. Login Department Leader.
2. Load department summary.
3. Load list.
4. Search.
5. Filter status.
6. Create new staff.
7. DB confirms PENDING account.
8. Change pending email.
9. Old token invalid, new token valid.
10. Confirm email.
11. Edit active email.
12. Old session rejected.
13. Login/re-link using new email.
14. Disable staff.
15. Enable staff.
16. View detail.
17. Transfer leadership.
18. Old leader loses management access.
19. New leader gains management access after login.
```

Không dùng production DB.

Không gửi email thật nếu test environment chưa dùng file sink/safe sink, trừ khi chủ dự án cho phép rõ ràng.

---

# 23. Thứ tự triển khai

## Phase 1 — Preflight và audit

- Đọc controller/handler/frontend hiện tại.
- Xác định exact status constants.
- Xác định auth provider/session services.
- Xác định email confirmation service.
- Chạy baseline build/test.
- Lập danh sách file cần sửa.

## Phase 2 — Security foundation

- Controller authorization.
- Scope service.
- Không nhận departmentId.
- Chuẩn error codes.
- Fix route guard.

## Phase 3 — Read functions

- Department summary.
- List.
- Search.
- Filter.
- Pagination.
- Detail.
- Action flags.

## Phase 4 — Create

- Create Department Staff.
- Pending confirmation.
- Truthful email result.
- Resend.

## Phase 5 — Edit profile and email identity

- FullName/phone/gender.
- Email any status.
- Auth provider reset.
- Confirmation reissue.
- Session revocation.
- Email notification.
- Audit.

## Phase 6 — Status management

- Status impact.
- Disable.
- Enable.
- Blockers.
- Session revocation.

## Phase 7 — Leadership transfer

- Candidates API.
- Transaction/locks.
- Role changes.
- Head update.
- Session revoke.
- Notifications.

## Phase 8 — Frontend

- Route/page.
- API/types/hooks.
- Modals.
- Search/filter.
- Toasts.
- Loading/error.
- Responsive.

## Phase 9 — Tests and closure

- Unit.
- Integration.
- Architecture.
- Frontend build.
- Real-stack.
- Static security scan.
- Final report.

---

# 24. Build và gate

Backend:

```bash
dotnet build
dotnet test
```

Frontend:

```bash
npm ci
npm run build
```

Chạy đúng project paths của repository.

Không được bỏ qua failing test bằng `Skip` mới.

Không xóa test để gate xanh.

Không đổi business rule chỉ để test pass.

---

# 25. Báo cáo cuối cùng AI Agent phải trả

Báo cáo gồm:

```text
1. Branch và HEAD trước/sau.
2. File backend đã sửa/tạo.
3. File frontend đã sửa/tạo.
4. SQL thay đổi, nếu có.
5. API contract cuối cùng.
6. Business rules đã enforce.
7. Các lỗ hổng security đã đóng.
8. Test count và kết quả.
9. Build frontend/backend.
10. Real-stack journeys.
11. Vấn đề còn lại.
12. Commit SHA nếu đã được phép commit.
```

Không được viết:

```text
Hoàn thành 100%
```

nếu chưa chạy test/build tương ứng.

---

# 26. Definition of Done

Chỉ coi chức năng hoàn thành khi:

- Department Leader chỉ quản lý đúng phòng ban mình.
- Không còn IDOR theo `departmentId`.
- List/search/filter/pagination chạy server-side.
- Create tạo đúng `DEPARTMENT + STAFF + PENDING`.
- Detail dùng API thật.
- Edit name/email/phone/gender chạy thật.
- Email sửa được ở PENDING, ACTIVE, INACTIVE và LOCKED.
- Đổi email xử lý đúng confirmation/auth provider/session.
- Disable/enable đúng transition.
- Không dùng enable để mở LOCKED/PENDING.
- Transfer leader transaction an toàn.
- Audit đầy đủ.
- Email result trung thực.
- Frontend không hard-code.
- Backend build xanh.
- Unit/integration/architecture tests xanh.
- Frontend build xanh.
- Real-stack journey chính xanh.
- Không làm hỏng module khác.

---

# 27. Kết luận nghiệp vụ cuối cùng

Luồng mục tiêu:

```text
Department Leader đăng nhập
→ backend xác minh họ vẫn là head của GENERAL department
→ xem thông tin phòng ban
→ xem/search/filter/phân trang nhân sự
→ thêm Department Staff mới
→ gửi xác nhận email
→ xem chi tiết
→ sửa name/email/phone/gender
→ email có thể sửa bất cứ lúc nào
→ identity/auth/session được xử lý an toàn
→ disable/enable theo đúng trạng thái và blocker
→ đổi trưởng phòng trong transaction
→ quyền cũ/mới có hiệu lực sau session revocation
→ audit, email và test đầy đủ
```

Đây là yêu cầu chốt. Không tự đơn giản hóa hoặc quay lại rule “chỉ được sửa email khi pending”.
