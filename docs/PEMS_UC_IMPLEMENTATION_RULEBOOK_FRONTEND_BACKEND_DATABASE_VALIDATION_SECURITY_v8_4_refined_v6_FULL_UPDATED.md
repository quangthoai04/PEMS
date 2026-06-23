# PEMS_UC_IMPLEMENTATION_RULEBOOK_FRONTEND_BACKEND_DATABASE_VALIDATION_SECURITY_v8_4_refined_v6_FULL_UPDATED

> **Bản FULL-PRESERVED cập nhật theo PEMS v8.4 refined v6 no dynamic permissions.**  
> File này gồm 2 phần:  
> - **PHẦN A — Nội dung chuẩn hiện tại để code/triển khai.**  
> - **PHẦN B — Nội dung gốc/legacy được giữ lại đầy đủ để đối chiếu lịch sử.**  
>
> Khi PHẦN A mâu thuẫn với PHẦN B, **luôn ưu tiên PHẦN A**. PHẦN B không được dùng làm nguồn code trực tiếp nếu có dấu hiệu legacy như `DEPT`, `STAFF_L`, `STAFF_P`, `Staff click nhận đón`, `auto Staff Leader làm host`, `Staff Leader/HO cancel sau APPROVED`, hoặc dynamic permissions.

## 0. Cách đọc file này

```text
1. Đọc PEMS_CANONICAL_BUSINESS_RULES_v8_4_refined_v6.md trước.
2. Đọc PHẦN A của file này để lấy nghiệp vụ/logic hiện hành.
3. Chỉ dùng PHẦN B để hiểu nguồn gốc tài liệu cũ, không dùng để sinh code nếu mâu thuẫn với PHẦN A.
4. Nếu cần code, backend phải kiểm tra lại bằng schema v8.4 refined v6 và seed v7/v6 dynamic time tương ứng.
```

---

# PHẦN A — NỘI DUNG CHUẨN HIỆN TẠI / UPDATED CANONICAL CONTENT

# PEMS_UC_IMPLEMENTATION_RULEBOOK_v8_4_refined_v6_UPDATED

> Rulebook chuẩn cho AI/code agent triển khai Use Case PEMS.  
> Phiên bản này đã bổ sung business rules mới: no dynamic permissions, role/subRole canonical, delegation lifecycle, multi-campus visibility, cancel rule, required guest/support team, dynamic planned time seed.

## 0. Nguyên tắc tối cao

Khi triển khai UC, không được chỉ scaffold. Phải code chạy thật, đúng schema, đúng nghiệp vụ, có build/test.

Không được:

```text
Tạo file rỗng
Để NotImplementedException
Dùng mock data nếu UC cần DB thật
Bỏ permission/scope
Bỏ validation
Query DB tùy tiện trong Controller
Báo pass nếu build fail
Sửa seed để che bug logic
Thêm role/status/enum ngoài schema nếu chưa patch DB
```

---

## 1. Workflow bắt buộc khi nhận UC

```text
[ ] Xác định UC ID, actor, module.
[ ] Đọc PEMS_CANONICAL_BUSINESS_RULES_v8_4_refined_v6.md.
[ ] Đọc schema/field dictionary liên quan.
[ ] Quét code hiện tại trước khi sửa.
[ ] Xác định API route, DTO, validation, business validation.
[ ] Xác định role/subRole/scope.
[ ] Xác định status transition.
[ ] Xác định DB tables và indexes nếu cần.
[ ] Xác định frontend page/hook/api/component.
[ ] Viết hoặc cập nhật test/manual test checklist.
[ ] Build backend.
[ ] Build frontend nếu sửa frontend.
[ ] Cập nhật docs/changelog.
[ ] Báo cáo đầy đủ.
```

---

## 2. Clean Architecture rules

### 2.1 API layer

Controller chỉ:

```text
Nhận route/query/body
Gọi IMediator.Send()
Trả response
```

Controller không:

```text
Không query DbContext
Không xử lý business rule phức tạp
Không tự map entity phức tạp
Không try/catch lan man
```

### 2.2 Application layer

Handler/service chịu trách nhiệm:

```text
Command/Query handling
Business validation
Scope/permission logic
Status transition
DTO projection
Calling repository/db abstraction
Notification/email/audit orchestration
```

### 2.3 Domain layer

Domain chứa:

```text
Entity
Enum/constants
Core transition rule
Invariant methods
```

### 2.4 Infrastructure layer

Infrastructure chứa:

```text
EF Core DbContext/config
Repository implementation
Email/file/SSO/external API implementation
```

Read-only query:

```text
AsNoTracking
Projection trực tiếp sang DTO
Paging bắt buộc cho list
Không Include dư
Không N+1
```

---

## 3. Database-first rules

PEMS dùng database-first/manual SQL.

Không được:

```text
Auto migration bừa
Đổi schema trong code mà không có SQL patch
Seed runtime trong Program.cs
Xóa destructive nếu chưa có yêu cầu
```

Nếu cần schema change:

```text
Tạo SQL patch rõ tên
Idempotent nếu có thể
Không làm mất dữ liệu cũ
Ghi rõ import order
Cập nhật schema docs
```

---

## 4. Permission model

Schema hiện tại không dùng dynamic permissions DB.

Không query:

```text
permissions
role_permissions
```

Authorize bằng fixed policy dựa trên:

```text
role_code
sub_role / effectiveRole
primary_campus_id
department_id
ownership
coordinator/host/participant relationship
record status
```

Endpoint public có thể AllowAnonymous, nhưng handler vẫn phải validate policy/OTP/ownership nếu cần.

---

## 5. Role/SubRole rules

Runtime values hợp lệ:

```text
ADMIN, HO, STAFF, DEPARTMENT, STUDENT, VISITOR
sub_role: STAFF, LEADER hoặc NULL
```

Effective roles:

```text
Staff Leader = STAFF + LEADER
IC Staff = STAFF + STAFF
Department Leader = DEPARTMENT + LEADER
Department Staff = DEPARTMENT + STAFF
```

Không dùng:

```text
DEPT
STAFF_LEADER
DEPARTMENT_LEADER
STAFF_L
DEPT_L
```

Host candidate:

```text
ACTIVE + STAFF + STAFF + same campus + IC department
```

---

## 6. Account Management canonical rules

### 6.1 User invariant

```text
Internal user phải có primary_campus_id.
Visitor không có primary_campus_id/department_id/sub_role.
STAFF phải thuộc IC department.
DEPARTMENT phải thuộc GENERAL department.
ADMIN/HO/STUDENT không dùng sub_role.
```

### 6.2 Status

```text
ACTIVE
INACTIVE
LOCKED
```

`INACTIVE` khác `LOCKED`:

```text
INACTIVE: disable/nghỉ việc/không còn dùng.
LOCKED: khóa bảo mật/sai mật khẩu/security issue.
```

### 6.3 Create account

Backend phải validate:

```text
Email unique
Role hợp lệ
SubRole hợp lệ theo role
Campus active
Department active và đúng type
Actor có quyền tạo trong scope
Uniqueness Staff Leader/Department Leader
```

Không xóa cứng user đã có history.

---

## 7. Delegation/Visit canonical rules

### 7.1 `visit_requests.status`

```text
PENDING_APPROVAL
APPROVED
REJECTED
CANCELLED
```

### 7.2 `visit_request_campuses.status`

```text
WAITING_REQUEST_APPROVAL
WAITING_HOST_ASSIGNMENT
ASSIGNED
BEFORE_VISIT
DURING_VISIT
AFTER_VISIT
CLOSED
CANCELLED
```

### 7.3 Submit form

Submit chỉ tạo request. Không approve/assign/cancel/close.

Mỗi request phải có:

```text
>= 1 GUEST
>= 1 EXTERNAL_SUPPORT
```

Nút “Là tôi” ở EXTERNAL_SUPPORT copy thông tin người đăng ký form.

### 7.4 Single-campus

```text
PENDING_APPROVAL + WAITING_REQUEST_APPROVAL
→ Staff Leader đúng campus approve/reject
→ approve: request APPROVED, instance WAITING_HOST_ASSIGNMENT
→ assign host: instance ASSIGNED
```

### 7.5 Multi-campus

```text
PENDING_APPROVAL + all instances WAITING_REQUEST_APPROVAL
→ only HO sees request tổng
→ HO approve/reject
→ approve: request APPROVED, all instances WAITING_HOST_ASSIGNMENT
→ coordinator_user_id = Staff Leader từng campus
→ Staff Leader từng campus assign host
```

Khi HO chưa duyệt, campus con không thấy instance.

### 7.6 Cancel

Trước approve:

```text
Dùng REJECTED, không dùng CANCELLED.
```

Sau approve:

```text
Visitor self-service cancel.
Host external-confirmation cancel.
```

Không cho cancel:

```text
DURING_VISIT
AFTER_VISIT
CLOSED
CANCELLED
```

Không có Staff Leader/HO internal-decision cancel sau approved theo schema hiện tại.

---

## 8. API contract rules

Success:

```json
{
  "success": true,
  "data": {},
  "message": "Thành công"
}
```

Error:

```json
{
  "success": false,
  "errorCode": "CAMPUS_SCOPE_FORBIDDEN",
  "message": "Bạn không có quyền xem dữ liệu ở cơ sở này.",
  "traceId": "optional"
}
```

HTTP codes:

```text
200 query/action success
201 created
400 invalid input
401 unauthenticated
403 forbidden/scope denied
404 not found in allowed scope
409 conflict/status conflict/duplicate
422 business validation failed nếu project dùng
429 rate limit
500 unexpected error without secret leakage
```

---

## 9. DTO/security rules

Không trả ra frontend:

```text
password_hash
password_salt
refresh_token
provider_subject
provider_uid
security_stamp
otp_token
reset_token
client_secret
api secret
```

DTO list phải có action flags nếu UI cần:

```text
canViewDetails
canApprove
canReject
canAssignHost
canCancel
canClose
canEdit
```

Action flags chỉ để UI hiển thị. Backend vẫn check lại.

---

## 10. List/search/filter/sort rules

Mọi list endpoint phải paging:

```text
page >= 1
pageSize 1..100
keyword max 100
sortBy whitelist
sortDirection asc/desc
```

Không cho:

```text
?pageSize=999999
?all=true nếu không có UC/export riêng
raw SQL sort string
```

Search phải debounce frontend 400-600ms.

---

## 11. Delegation visibility implementation guide

### 11.1 HO

```text
WHERE vr.visit_scope = 'MULTI_CAMPUS'
```

HO không mặc định thấy single-campus.

### 11.2 Staff Leader

```text
WHERE vrc.campus_id = currentUser.primary_campus_id
AND (
  vr.visit_scope = 'SINGLE_CAMPUS'
  OR (vr.visit_scope = 'MULTI_CAMPUS' AND vr.status = 'APPROVED')
)
```

Không thấy multi-campus pending HO.

### 11.3 IC Staff

```text
WHERE vrc.current_host_user_id = currentUserId
   OR EXISTS visit_participants where user_id = currentUserId and status not removed/declined as policy
```

### 11.4 Department

```text
WHERE logistics/participant/task department_id = currentUser.department_id
```

### 11.5 Student

```text
WHERE participant_role = STUDENT AND user_id = currentUserId
```

### 11.6 Visitor

```text
WHERE vr.visitor_user_id = currentUserId
```

---

## 12. Validation examples

### 12.1 Assign host

```text
[ ] Instance exists.
[ ] Request status APPROVED.
[ ] Actor is Staff Leader same campus.
[ ] Instance status WAITING_HOST_ASSIGNMENT or allowed state.
[ ] current_host_user_id is NULL.
[ ] Candidate is ACTIVE STAFF + STAFF same campus IC department.
[ ] Instance not CANCELLED/CLOSED.
```

### 12.2 Cancel

```text
[ ] Request exists in user scope.
[ ] Request status APPROVED.
[ ] Instance status not DURING/AFTER/CLOSED/CANCELLED.
[ ] Visitor owner OR Host current_host_user_id.
[ ] If Host: cancellation_source = EXTERNAL_CONFIRMATION and reason detailed.
[ ] If Visitor: cancellation_source = SELF_SERVICE.
```

### 12.3 Submit form

```text
[ ] At least one selected campus.
[ ] planned time valid.
[ ] planned_end_at > planned_start_at.
[ ] At least one GUEST.
[ ] At least one EXTERNAL_SUPPORT.
[ ] Required strings not blank.
[ ] OTP verified if public visitor flow.
```

---

## 13. Seed rules

Manual rich seed được phép dùng dynamic time:

```sql
CURRENT_DATE
CURRENT_TIMESTAMP
DATE_ADD
DATE_SUB
INTERVAL
```

Không dùng:

```text
Stored procedure
Loop
Cursor
RAND
INSERT IGNORE
copy-paste data chỉ thay vài chữ
```

Seed phải cover:

```text
All role/subRole
All visit_requests.status
All visit_request_campuses.status
All enum values quan trọng
Single-campus/multi-campus
HO pending invisibility
Visitor cancel full/partial
Host external cancel
Guest/support team required
Dynamic planned_start_at/planned_end_at consistency
Records for all campus accounts, not only HN
```

---

## 14. Frontend convention

Frontend phải có:

```text
API layer riêng
Hooks riêng
Types/constants enum chung
Loading state
Empty state
Error state
Pagination/search/filter/sort state
Abort/ignore stale request nếu search debounce
Button visibility theo canAction + role/scope/status
```

Không gọi fetch trực tiếp trong page nếu project đã có httpClient.

Không giữ mock mặc định khi API thật có.

---

## 15. Test checklist mẫu cho delegation

```text
[ ] Visitor submit form thiếu GUEST -> 400/validation error.
[ ] Visitor submit form thiếu EXTERNAL_SUPPORT -> 400/validation error.
[ ] Multi-campus pending HO: Staff Leader không thấy instance.
[ ] HO thấy multi-campus pending.
[ ] HO approve: Staff Leader từng campus thấy WAITING_HOST_ASSIGNMENT.
[ ] Staff Leader assign host: candidate list đúng.
[ ] Staff Leader không thấy campus khác.
[ ] IC Staff host thấy instance được gán.
[ ] Department Leader thấy logistics department mình.
[ ] Student thấy invitation của mình.
[ ] Visitor self-cancel future approved request.
[ ] Host external cancel with detailed reason.
[ ] Cancel DURING_VISIT bị chặn.
[ ] Pending request muốn dừng dùng reject.
```

---

## 16. Build commands

Backend:

```bash
dotnet restore
dotnet build
```

Frontend:

```bash
cd frontend/pems-react
npm install
npm run build
```

Nếu có test/lint/typecheck:

```bash
dotnet test
npm run lint
npm run typecheck
```

Không báo PASS nếu chỉ build một project con mà solution fail.

---

## 17. Documentation/changelog required

Mỗi UC hoàn thành phải có:

```text
docs/<module>/<UC_ID>_<UC_NAME>.md
docs/architecture/REFACTOR_CHANGELOG.md hoặc changelog tương ứng
```

Format báo cáo:

```markdown
# Báo cáo hoàn thành UC-XX

## 1. Summary
## 2. Backend files changed
## 3. Frontend files changed
## 4. Database files changed
## 5. API contract
## 6. Validation rules
## 7. Permission/scope rules
## 8. Anti-spam/performance rules
## 9. Manual test results
## 10. Build results
## 11. Known limitations
## 12. TODO next phase
```

---

## 18. Definition of Done

```text
[ ] Endpoint/API chạy thật.
[ ] Backend permission/scope đúng.
[ ] Input validation đúng.
[ ] Business validation đúng.
[ ] Status transition đúng.
[ ] Không trả data nhạy cảm.
[ ] Frontend nối API thật nếu có UI.
[ ] Loading/empty/error state.
[ ] Audit/notification/email nếu action cần.
[ ] Manual test cases chạy.
[ ] Backend build pass.
[ ] Frontend build pass nếu sửa frontend.
[ ] Docs/changelog cập nhật.
[ ] Không còn mock/scaffold/NotImplementedException.
```

---

# PHẦN B — NỘI DUNG GỐC / LEGACY PRESERVED CONTENT

> Phần này được giữ nguyên để đối chiếu lịch sử. Không dùng phần này để code nếu mâu thuẫn với PHẦN A hoặc file canonical.

# PEMS — QUY ƯỚC THỰC THI CODE UC THỐNG NHẤT

> File này dùng làm **rulebook/prompt chuẩn** cho AI/code agent khi triển khai từng Use Case trong PEMS.  
> Mục tiêu: mọi UC được code thống nhất từ **Backend, Frontend, Database, Validation, Anti-spam, Permission, API contract, DTO, Query params, Paging, Search, Filter, Sort, Test case, Build command và Definition of Done**.

---

## 0. Mục đích của file

Khi triển khai bất kỳ UC nào, đặc biệt các UC dạng quản trị như:

```text
UC-95 View Account List
UC-96 Create Account
UC-97 Manage Account Status
UC-98 View Account Details
UC-99 Search and Filter Accounts
UC-100 Update Account Role
```

AI/code agent phải đọc file này trước để đảm bảo:

```text
- Không code lệch kiến trúc.
- Không phá frontend hiện có.
- Không bỏ validate.
- Không bỏ phân quyền.
- Không query DB tùy tiện.
- Không trả dữ liệu nhạy cảm.
- Không tạo endpoint public sai.
- Không báo hoàn thành nếu chưa build/test.
```

---

## 1. Vai trò bắt buộc của AI/code agent

Bạn là:

```text
Senior .NET Clean Architecture Developer
Senior React TypeScript Engineer
Database-first MySQL Engineer
Security Reviewer
Performance/Anti-spam Reviewer
```

Bạn phải triển khai UC theo hướng **chạy thật**, không chỉ scaffold.

Không được:

```text
- Tạo file rỗng.
- Để NotImplementedException.
- Trả mock data nếu UC yêu cầu dùng DB thật.
- Viết business logic trong Controller.
- Gọi DbContext trực tiếp trong Controller.
- Bỏ qua permission/scope.
- Bỏ qua validation.
- Bỏ qua anti-spam/rate limit cho endpoint dễ spam.
- Báo pass nếu build fail.
```

---

## 2. Nguyên tắc tổng quát khi làm một UC

Mỗi UC phải được triển khai theo checklist:

```text
[ ] Xác định đúng UC ID, UC name, actor, permission code.
[ ] Đọc tài liệu use case/permission/database liên quan.
[ ] Quét code hiện tại trước khi sửa.
[ ] Xác định route API hiện có hoặc route cần tạo.
[ ] Xác định bảng DB và entity liên quan.
[ ] Xác định input DTO/query params.
[ ] Xác định output DTO/response.
[ ] Xác định validation input.
[ ] Xác định business validation.
[ ] Xác định phân quyền theo role/campus/department/own-scope.
[ ] Xác định anti-spam/rate limit.
[ ] Xác định frontend page/component/hook/API cần nối.
[ ] Xác định test case thủ công/tự động.
[ ] Build backend/frontend.
[ ] Cập nhật docs/changelog.
```

---

## 3. Quy tắc Clean Architecture bắt buộc

## 3.1 API Layer — `PEMS.Api`

Controller chỉ được làm:

```text
- Nhận route/query/body.
- Gọi IMediator.Send().
- Trả ApiResponse/ActionResult.
```

Controller không được làm:

```text
- Không query DbContext.
- Không gọi repository trực tiếp nếu project dùng MediatR.
- Không check business rule phức tạp.
- Không tự tạo token/session.
- Không tự map Entity sang DTO phức tạp.
- Không tự xử lý try/catch lan man.
```

Ví dụ đúng:

```csharp
[HttpGet]
[PermissionAuthorize("UC-95.VIEW_ACCOUNT_LIST")]
public async Task<IActionResult> GetAccounts([FromQuery] ViewAccountListQuery query)
{
    var result = await _mediator.Send(query);
    return Ok(result);
}
```

---

## 3.2 Application Layer — `PEMS.Application`

Application chịu trách nhiệm:

```text
- Command/Query.
- Handler.
- Validator.
- DTO/Response.
- Business validation.
- Permission/scope logic nếu thuộc nghiệp vụ.
- Gọi interface/repository/db context abstraction.
```

Mỗi UC dạng Command nên có:

```text
<UseCaseName>Command.cs
<UseCaseName>CommandHandler.cs
<UseCaseName>CommandValidator.cs
<UseCaseName>Response.cs
```

Mỗi UC dạng Query nên có:

```text
<UseCaseName>Query.cs
<UseCaseName>QueryHandler.cs
<UseCaseName>QueryValidator.cs nếu có query params phức tạp
<UseCaseName>Dto.cs
```

Không được để handler quá dài. Nếu logic lặp lại, tách service:

```text
IAccountScopeService
IAccountQueryService
IAuthPolicyService
IPermissionChecker
IRateLimitPolicyService nếu cần
```

---

## 3.3 Domain Layer — `PEMS.Domain`

Domain chứa:

```text
- Entity.
- Enum/Constants.
- Domain rule cốt lõi.
- Method thay đổi trạng thái.
```

Không nhét logic DB/API vào Domain.

Ví dụ entity có method:

```csharp
public void Lock(string reason, int changedByUserId)
{
    if (Status == UserStatus.Locked)
        throw new BusinessRuleException("Tài khoản đã bị khóa.");

    Status = UserStatus.Locked;
    UpdatedAt = DateTime.UtcNow;
}
```

---

## 3.4 Infrastructure Layer — `PEMS.Infrastructure`

Infrastructure chịu trách nhiệm:

```text
- EF Core DbContext.
- Entity configurations.
- Repository implementation.
- External service implementation.
- Email/SSO/File/Storage implementation.
```

Query read-only phải ưu tiên:

```text
- AsNoTracking().
- Projection trực tiếp sang DTO.
- Không Include dư thừa.
- Không N+1.
```

---

## 4. Quy tắc Database-first

PEMS theo hướng **database-first/manual SQL**.

Không được:

```text
- Không tự chạy auto migration bừa.
- Không tự đổi schema bằng code nếu chưa có SQL patch.
- Không xóa cột/bảng destructive.
- Không seed runtime trong Program.cs nếu project đã chốt manual seed.
```

Nếu cần thay đổi DB:

```text
- Tạo file SQL patch trong database/scripts/.
- Patch phải idempotent.
- Ghi rõ cần chạy patch nào.
- Không làm mất dữ liệu cũ.
```

Tên file patch nên theo UC:

```text
database/scripts/patch_uc95_uc99_account_list_indexes.sql
database/scripts/patch_uc96_create_account_fields.sql
database/scripts/patch_uc100_update_role_sessions.sql
```

---

## 5. Quy ước Permission và Role Scope

## 5.1 Permission code

Mỗi endpoint nghiệp vụ phải có permission tương ứng.

Ví dụ:

```text
UC-95.VIEW_ACCOUNT_LIST
UC-96.CREATE_ACCOUNT
UC-97.MANAGE_ACCOUNT_STATUS
UC-98.VIEW_ACCOUNT_DETAILS
UC-99.SEARCH_AND_FILTER_ACCOUNTS
UC-100.UPDATE_ACCOUNT_ROLE
```

Không được để endpoint quản trị là `[AllowAnonymous]`.

Login/logout/public endpoint có thể `[AllowAnonymous]`, nhưng handler vẫn phải tự check policy.

---

## 5.2 Rule phân quyền Account Management

### ADMIN

```text
- Có thể xem toàn bộ account.
- Có thể filter mọi campus/role/status/department.
- Có thể thấy account ADMIN/HO/STAFF/DEPT/STUDENT/VISITOR.
- Có thể quản trị kỹ thuật theo permission được cấp.
```

### HO

```text
- Có thể xem account toàn hệ thống nếu có UC tương ứng.
- Có thể filter theo mọi campus.
- Có thể theo dõi account nội bộ toàn hệ thống.
- Không vượt permission được cấp trong RBAC.
```

### Staff Leader / STAFF_L

Nếu project tách `STAFF_L`, dùng role đó.  
Nếu project gộp `STAFF` và dùng `sub_role`, xác định Staff Leader bằng `subRole`.

```text
- Chỉ xem account thuộc campus của mình.
- Chỉ tạo/sửa account trong campus của mình.
- Không được thao tác account campus khác.
- Không được thao tác ADMIN/HO nếu policy không cho.
- Có thể tìm Visitor bằng email để chuẩn bị UC-100 Update Account Role.
- Không dump toàn bộ Visitor campus NULL nếu không có keyword rõ.
```

### Staff thường / STAFF_P

```text
- Chỉ xem trong campus của mình nếu có permission.
- Không thao tác role/campus nếu không có UC-100.
- Không xem campus khác.
```

### Department / Student / Visitor

```text
- Mặc định không được vào Account Management.
- Nếu gọi API account list thì trả 403.
- Visitor không bao giờ xem danh sách user hệ thống.
```

---

## 5.3 Rule scope dữ liệu

Không tin dữ liệu scope từ frontend.

Ví dụ frontend gửi:

```http
GET /api/accounts?campusId=2
```

Nhưng current user là Staff Leader campus 1 thì backend phải:

```text
- Trả 403 CAMPUS_SCOPE_FORBIDDEN
hoặc
- Trả empty theo policy an toàn
```

Ưu tiên 403 nếu đây là hành vi vượt quyền rõ ràng.

---

## 6. Quy tắc API Contract

## 6.1 Response thành công

Dùng format thống nhất nếu project đã có `ApiResponse<T>`:

```json
{
  "success": true,
  "data": {},
  "message": "Thành công"
}
```

Với list/paging:

```json
{
  "success": true,
  "data": {
    "items": [],
    "page": 1,
    "pageSize": 20,
    "totalItems": 100,
    "totalPages": 5,
    "hasNextPage": true,
    "hasPreviousPage": false
  }
}
```

Nếu project hiện đang trả trực tiếp object, không đổi contract hàng loạt nếu frontend đang phụ thuộc. Nhưng phải thống nhất trong UC mới.

---

## 6.2 Response lỗi

Mọi lỗi nghiệp vụ/validation/permission phải có:

```json
{
  "success": false,
  "errorCode": "CAMPUS_SCOPE_FORBIDDEN",
  "message": "Bạn không có quyền xem dữ liệu ở cơ sở này.",
  "traceId": "optional"
}
```

Không dùng message mơ hồ như:

```text
Error
Failed
Something went wrong
```

---

## 6.3 HTTP status code

```text
200 OK
- Query thành công.
- Search không có dữ liệu vẫn 200 với items empty.

201 Created
- Tạo mới thành công.

400 Bad Request
- Input sai format.
- Filter/sort không hợp lệ.
- page/pageSize sai.
- campusId/departmentId sai kiểu hoặc không hợp lệ.

401 Unauthorized
- Chưa đăng nhập.
- Token hết hạn/invalid.
- Session revoked.

403 Forbidden
- Có đăng nhập nhưng không có quyền UC.
- Vượt campus scope.
- Role không được phép thao tác.
- Wrong portal/campus mismatch trong auth.

404 Not Found
- Không tìm thấy tài nguyên trong scope được phép.

409 Conflict
- Trùng email.
- Conflict trạng thái.
- Row version conflict.

429 Too Many Requests
- Vượt rate limit/chống spam.

500 Internal Server Error
- Lỗi bất ngờ, không lộ secret.
```

---

## 7. Quy tắc DTO

## 7.1 DTO không được lộ dữ liệu nhạy cảm

Không trả ra frontend:

```text
password_hash
password_salt
refresh_token
refresh_token_hash
provider_subject
provider_uid
security_stamp
otp_token
reset_token
secret_key
client_secret
row internal secret
```

## 7.2 DTO Account List chuẩn

```csharp
public sealed class AccountListItemDto
{
    public int UserId { get; init; }
    public string Email { get; init; } = default!;
    public string FullName { get; init; } = default!;

    public string RoleCode { get; init; } = default!;
    public string RoleName { get; init; } = default!;
    public string? SubRole { get; init; }

    public int? CampusId { get; init; }
    public string? CampusCode { get; init; }
    public string? CampusName { get; init; }

    public int? DepartmentId { get; init; }
    public string? DepartmentName { get; init; }

    public string Status { get; init; } = default!;
    public string? CreatedVia { get; init; }

    public IReadOnlyList<string> Providers { get; init; } = Array.Empty<string>();

    public DateTime? LastLoginAt { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }

    public bool CanViewDetails { get; init; }
    public bool CanUpdateRole { get; init; }
    public bool CanManageStatus { get; init; }
}
```

## 7.3 DTO Create Account chuẩn

```csharp
public sealed class CreateAccountCommand : IRequest<CreateAccountResponse>
{
    public string Email { get; init; } = default!;
    public string FullName { get; init; } = default!;
    public string RoleCode { get; init; } = default!;
    public string? SubRole { get; init; }
    public int? CampusId { get; init; }
    public int? DepartmentId { get; init; }
    public string? TemporaryPassword { get; init; }
}
```

## 7.4 DTO Update Role chuẩn

```csharp
public sealed class UpdateAccountRoleCommand : IRequest<UpdateAccountRoleResponse>
{
    public int UserId { get; init; }
    public string NewRoleCode { get; init; } = default!;
    public string? NewSubRole { get; init; }
    public int? CampusId { get; init; }
    public int? DepartmentId { get; init; }
}
```

---

## 8. Query Params chuẩn cho List/Search/Filter

Các UC dạng list/search/filter nên dùng quy ước:

```text
page
pageSize
keyword
sortBy
sortDirection
```

Với Account Management:

```text
roleCode
subRole
status
campusId
departmentId
providerType
createdVia
accountType
hasCampus
fromDate
toDate
lastLoginFrom
lastLoginTo
```

Ví dụ:

```http
GET /api/accounts?page=1&pageSize=20&keyword=nguyen&roleCode=STAFF&campusId=1&status=ACTIVE&sortBy=createdAt&sortDirection=desc
```

---

## 9. Quy tắc Paging

Bắt buộc paging với mọi endpoint list.

Default:

```text
page = 1
pageSize = 20
max pageSize = 100
```

Không được cho:

```http
GET /api/accounts?all=true
GET /api/accounts?pageSize=999999
```

Nếu pageSize quá lớn:

```text
Option A: trả 400 PAGE_SIZE_TOO_LARGE
Option B: clamp về 100
```

Ưu tiên Option A để tránh frontend gọi sai mà không biết.

Response paging:

```json
{
  "items": [],
  "page": 1,
  "pageSize": 20,
  "totalItems": 0,
  "totalPages": 0,
  "hasNextPage": false,
  "hasPreviousPage": false
}
```

---

## 10. Quy tắc Search

## 10.1 Keyword

Keyword phải:

```text
- Trim.
- Giới hạn độ dài tối đa 100 ký tự.
- Không search nếu chỉ toàn khoảng trắng.
- Nếu keyword quá ngắn, tránh scan DB rộng.
```

Rule gợi ý:

```text
keyword length = 1:
- Chỉ cho exact email/userId nếu có.
- Không contains toàn DB.

keyword length >= 2:
- Cho search email/fullName/role/campus/department.
```

## 10.2 Search fields Account

Có thể search theo:

```text
email
fullName/displayName
phone nếu có
roleCode
roleName
campusCode
campusName
departmentName
```

Không search trong:

```text
passwordHash
providerSubject
token
internal secret
```

---

## 11. Quy tắc Filter

Filter phải validate against whitelist.

## 11.1 Role filter

```text
roleCode phải tồn tại trong bảng roles.
Không nhận role lạ.
```

## 11.2 Status filter

Whitelist:

```text
ACTIVE
INACTIVE
LOCKED
```

Nếu project có status khác, dùng status thật trong DB, không tự bịa.

## 11.3 Provider filter

Whitelist:

```text
LOCAL_PASSWORD
GOOGLE
FEID
```

## 11.4 Account type

```text
ALL
INTERNAL
VISITOR
```

Mapping:

```text
VISITOR = roleCode == VISITOR
INTERNAL = roleCode != VISITOR
ALL = không lọc theo accountType
```

## 11.5 Date filter

Validate:

```text
fromDate <= toDate
lastLoginFrom <= lastLoginTo
Không nhận date invalid.
```

---

## 12. Quy tắc Sort

Sort phải dùng whitelist, không dùng raw SQL string.

Allowed sort account:

```text
createdAt
updatedAt
lastLoginAt
email
fullName
role
status
campus
```

Nếu sortBy không hợp lệ:

```text
400 UNSUPPORTED_SORT_COLUMN
```

Sort direction:

```text
asc
desc
```

Mặc định:

```text
sortBy = createdAt
sortDirection = desc
```

---

## 13. Validation chuẩn

## 13.1 Input validation

Viết trong FluentValidation:

```text
- Required field.
- Email format.
- String length.
- Enum/whitelist.
- Page/pageSize.
- Date range.
- Positive id.
```

Ví dụ:

```csharp
RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
RuleFor(x => x.Keyword).MaximumLength(100);
RuleFor(x => x.SortDirection)
    .Must(x => x == null || x.Equals("asc", StringComparison.OrdinalIgnoreCase) || x.Equals("desc", StringComparison.OrdinalIgnoreCase));
```

## 13.2 Business validation

Viết trong Handler/Service:

```text
- Email đã tồn tại chưa.
- Role có tồn tại không.
- Campus active không.
- Department thuộc campus không.
- Current user có quyền scope này không.
- Status transition hợp lệ không.
```

Không query DB trong FluentValidation nếu project không thiết kế validator async repository.

---

## 14. Anti-spam / Rate limit / Query performance guard

## 14.1 Rate limit

Các endpoint list/search phải qua RateLimitMiddleware hoặc policy tương đương.

Gợi ý:

```text
GET /api/accounts:
- ADMIN/HO: 60 requests/phút/user.
- STAFF/STAFF_L: 30 requests/phút/user.
- Anonymous: không được gọi vì endpoint cần auth.
```

Nếu vượt:

```json
{
  "success": false,
  "errorCode": "RATE_LIMIT_EXCEEDED",
  "message": "Bạn thao tác quá nhanh. Vui lòng thử lại sau."
}
```

HTTP:

```text
429 Too Many Requests
```

## 14.2 Frontend debounce

Search input phải debounce:

```text
400ms đến 600ms
```

Không gọi API mỗi lần render.

Khi filter đổi:

```text
- Reset page về 1.
- Không spam nhiều request liên tục.
- Có thể hủy request cũ bằng AbortController hoặc ignore stale response.
```

## 14.3 Query guard

Bắt buộc:

```text
- AsNoTracking().
- Paging bắt buộc.
- pageSize max 100.
- Projection sang DTO.
- Không Include dư.
- Không sort raw string.
- Keyword max 100.
- Keyword quá ngắn thì hạn chế search.
- Staff Leader không được dump toàn bộ Visitor campus NULL.
```

## 14.4 Logging spam/security

Ghi security event nếu:

```text
- User cố xem campus khác.
- User không có quyền nhưng gọi account endpoint.
- User gọi endpoint quá nhiều nếu có rate limit logging.
```

Không log:

```text
- Token.
- Password.
- Provider subject.
- Reset token.
```

---

## 15. Database rule cho UC list/search

## 15.1 Index gợi ý

Không chạy nếu đã có index. Nếu thiếu và query chậm, tạo patch idempotent.

```sql
CREATE INDEX idx_users_email ON users(email);
CREATE INDEX idx_users_role_id ON users(role_id);
CREATE INDEX idx_users_primary_campus_id ON users(primary_campus_id);
CREATE INDEX idx_users_department_id ON users.department_id;
CREATE INDEX idx_users_status ON users(status);
CREATE INDEX idx_users_created_at ON users(created_at);
CREATE INDEX idx_user_auth_providers_user_id ON user_auth_providers(user_id);
CREATE INDEX idx_user_auth_providers_provider_type ON user_auth_providers(provider_type);
```

Lưu ý: câu trên chỉ là gợi ý logic, phải sửa đúng syntax/table thật khi tạo patch. Với MySQL, không tạo index trùng.

## 15.2 Không lạm dụng full-text

Không thêm full-text index nếu chưa cần. Với dữ liệu account, search email/fullName cơ bản là đủ cho giai đoạn này.

---

## 16. Frontend convention

## 16.1 Cấu trúc đề xuất

```text
frontend/pems-react/src/features/account-management/
├── api/
│   └── accountManagementApi.ts
├── hooks/
│   ├── useAccountList.ts
│   └── useAccountManagement.ts
├── types/
│   └── account.types.ts
└── utils/
    └── accountMappers.ts nếu cần
```

Không bắt buộc di chuyển page nếu page hiện đang nằm ở `src/pages/dashboard`. Chỉ thêm API/hook và import vào page hiện có.

## 16.2 API layer

Không gọi `fetch/axios` trực tiếp trong page nếu project đã có `httpClient`.

Đúng:

```ts
accountManagementApi.getAccounts(params)
```

Sai:

```ts
fetch('/api/accounts?...') trực tiếp trong component
```

## 16.3 Hook

Hook chịu trách nhiệm:

```text
- params state.
- loading.
- error.
- refetch.
- debounce keyword nếu cần.
```

Page chịu trách nhiệm:

```text
- render UI.
- truyền params/filter.
- gọi handler từ hook.
```

## 16.4 Error display

Frontend phải ưu tiên `errorCode` nếu backend trả:

```ts
getApiErrorMessage(error)
```

Map message tiếng Việt:

```ts
{
  ACCOUNT_LIST_FORBIDDEN: 'Bạn không có quyền xem danh sách tài khoản.',
  CAMPUS_SCOPE_FORBIDDEN: 'Bạn không có quyền xem tài khoản ở cơ sở này.',
  UNSUPPORTED_SORT_COLUMN: 'Cột sắp xếp không hợp lệ.',
  RATE_LIMIT_EXCEEDED: 'Bạn thao tác quá nhanh. Vui lòng thử lại sau.'
}
```

---

## 17. Cách nối Account Management UI

Khi nối UI:

```text
[ ] Giữ layout hiện tại.
[ ] Không xóa mock ngay nếu chưa chắc, nhưng mặc định dùng API thật.
[ ] Loading state khi gọi API.
[ ] Empty state khi không có dữ liệu.
[ ] Error state khi 401/403/500.
[ ] Search input cập nhật keyword.
[ ] Filter cập nhật params.
[ ] Pagination cập nhật page/pageSize.
[ ] Sort cập nhật sortBy/sortDirection.
[ ] Button action dựa vào canViewDetails/canUpdateRole/canManageStatus.
```

Nếu backend UC-96/97/100 chưa xong:

```text
- Disable hoặc giữ button nhưng hiện message "Chức năng đang được triển khai".
- Không cho button gọi mock làm người dùng hiểu nhầm.
```

---

## 18. API contract mẫu cho UC-95/UC-99

## 18.1 Request

```http
GET /api/accounts?page=1&pageSize=20&keyword=nguyen&roleCode=STAFF&status=ACTIVE&campusId=1&sortBy=createdAt&sortDirection=desc
```

## 18.2 Response

```json
{
  "success": true,
  "data": {
    "items": [
      {
        "userId": 1,
        "email": "staff.hn@fpt.edu.vn",
        "fullName": "Nguyen Van A",
        "roleCode": "STAFF",
        "roleName": "Staff",
        "subRole": "STAFF_L",
        "campusId": 1,
        "campusCode": "HN",
        "campusName": "FPTU Hà Nội",
        "departmentId": null,
        "departmentName": null,
        "status": "ACTIVE",
        "createdVia": "ADMIN_CREATED",
        "providers": ["GOOGLE", "LOCAL_PASSWORD"],
        "lastLoginAt": "2026-06-18T10:00:00Z",
        "createdAt": "2026-06-01T10:00:00Z",
        "updatedAt": "2026-06-10T10:00:00Z",
        "canViewDetails": true,
        "canUpdateRole": true,
        "canManageStatus": true
      }
    ],
    "page": 1,
    "pageSize": 20,
    "totalItems": 1,
    "totalPages": 1,
    "hasNextPage": false,
    "hasPreviousPage": false
  }
}
```

---

## 19. Test case chuẩn

## 19.1 Backend manual tests

```text
[ ] Không token gọi GET /accounts -> 401.
[ ] VISITOR gọi GET /accounts -> 403.
[ ] DEPT/STUDENT không có permission -> 403.
[ ] ADMIN gọi GET /accounts -> 200 toàn hệ thống.
[ ] HO gọi GET /accounts -> 200 theo scope HO.
[ ] STAFF_L campus HN -> chỉ thấy campus HN.
[ ] STAFF_L campus HN filter campus HCM -> 403 hoặc empty theo policy.
[ ] Search keyword email -> đúng account.
[ ] Search keyword không có -> 200 items empty.
[ ] Filter role=VISITOR -> chỉ visitor.
[ ] Filter status=ACTIVE -> chỉ active.
[ ] Filter accountType=INTERNAL -> không có visitor.
[ ] Filter providerType=GOOGLE -> chỉ account có provider Google.
[ ] Sort email asc -> đúng.
[ ] Sort passwordHash -> 400 UNSUPPORTED_SORT_COLUMN.
[ ] pageSize=9999 -> 400 PAGE_SIZE_TOO_LARGE.
[ ] keyword quá dài -> 400.
[ ] fromDate > toDate -> 400.
[ ] Gọi quá nhanh -> 429 nếu rate limit bật.
```

## 19.2 Frontend tests/manual checks

```text
[ ] Account Management load dữ liệu từ API.
[ ] Không còn mock mặc định.
[ ] Loading hiển thị đúng.
[ ] Empty state đúng.
[ ] Error 403 hiển thị message tiếng Việt.
[ ] Search debounce hoạt động.
[ ] Filter role/status/campus hoạt động.
[ ] Pagination hoạt động.
[ ] Sort hoạt động nếu UI hỗ trợ.
[ ] Button action ẩn/hiện theo canAction.
[ ] Không phá login/auth storage.
```

---

## 20. Build command bắt buộc

Backend:

```bash
dotnet restore
dotnet build
```

Nếu có test:

```bash
dotnet test
```

Frontend:

```bash
cd frontend/pems-react
npm install
npm run build
```

Nếu có lint/typecheck:

```bash
npm run lint
npm run typecheck
```

Nếu API đang chạy gây lỗi copy DLL:

```text
- Dừng process API đang chạy.
- Build lại full backend.
- Không báo pass nếu chỉ build được Application.
```

---

## 21. Documentation/changelog bắt buộc

Sau mỗi UC, tạo/cập nhật:

```text
docs/architecture/REFACTOR_CHANGELOG.md
docs/<module>/<UC_ID>_<UC_NAME>.md
```

Ví dụ:

```text
docs/accounts/UC95_UC99_ACCOUNT_LIST_SEARCH_FILTER.md
```

Nội dung changelog:

```markdown
# Changelog UC-95 + UC-99

## 1. Summary
## 2. Backend files changed
## 3. Frontend files changed
## 4. Database files changed
## 5. API contract
## 6. Validation rules
## 7. Permission/scope rules
## 8. Anti-spam/performance rules
## 9. Manual test results
## 10. Build results
## 11. Known limitations
## 12. TODO next phase
```

---

## 22. Definition of Done tổng quát

Một UC chỉ được coi là hoàn thành khi:

```text
[ ] Có backend endpoint chạy thật.
[ ] Có permission guard.
[ ] Có scope theo role/campus/own-data.
[ ] Có input validation.
[ ] Có business validation.
[ ] Có errorCode rõ.
[ ] Có anti-spam/rate limit hoặc giải thích vì sao chưa cần.
[ ] Có paging nếu là list.
[ ] Có search/filter/sort nếu UC yêu cầu.
[ ] Không trả dữ liệu nhạy cảm.
[ ] Frontend nối API thật nếu UC có UI.
[ ] Có loading/empty/error state trên frontend.
[ ] Build backend pass.
[ ] Build frontend pass nếu sửa frontend.
[ ] Manual test case chính đã chạy.
[ ] Có docs/changelog.
[ ] Không còn NotImplementedException trong UC đó.
```

---

## 23. Definition of Done riêng cho UC-95 + UC-99

```text
[ ] GET /api/accounts chạy thật.
[ ] ViewAccountListQueryHandler không còn scaffold.
[ ] SearchandFilterAccountsQueryHandler không còn scaffold hoặc được hợp nhất đúng.
[ ] Có paging metadata.
[ ] Có keyword search.
[ ] Có filter tối thiểu: role/status/campus/accountType/provider.
[ ] Có sort tối thiểu: createdAt/email/fullName/status.
[ ] Có scope theo current user.
[ ] Staff Leader không xem campus khác.
[ ] Visitor/Student/Dept không có quyền bị 403.
[ ] Không lộ passwordHash/providerSubject/token.
[ ] Account Management UI lấy API thật.
[ ] Search/filter/pagination trên UI gọi API thật.
[ ] dotnet build full backend pass.
[ ] npm run build pass.
[ ] Có docs/accounts/UC95_UC99_ACCOUNT_LIST_SEARCH_FILTER.md.
```

---

## 24. Báo cáo hoàn thành chuẩn

Khi code agent làm xong, phải báo cáo theo mẫu:

```markdown
# Báo cáo hoàn thành UC-XX

## 1. Tóm tắt
## 2. File đã sửa/thêm
## 3. Backend đã làm gì
## 4. Frontend đã làm gì
## 5. Database/SQL patch
## 6. API contract cuối cùng
## 7. DTO/request/response
## 8. Validation đã thêm
## 9. Anti-spam/rate limit đã thêm
## 10. Permission/scope đã áp dụng
## 11. Test case đã chạy
## 12. Kết quả build
## 13. Rủi ro/TODO còn lại
```

Không chấp nhận báo cáo kiểu:

```text
Đã làm xong.
```

Phải có file, test, build, rủi ro rõ ràng.

---

## 25. Prompt ngắn để bắt đầu một UC theo rulebook này

Khi muốn yêu cầu AI/code agent làm một UC, dùng mẫu:

```text
Hãy triển khai UC-XX <Tên UC> theo đúng file PEMS_UC_IMPLEMENTATION_RULEBOOK.md.

Yêu cầu:
- Đọc rulebook trước khi code.
- Không phá frontend hiện có.
- Backend theo Clean Architecture.
- Database-first, nếu cần schema thì tạo SQL patch idempotent.
- Có validation input + business validation.
- Có permission/scope theo role/campus.
- Có anti-spam/rate limit nếu endpoint có thể bị spam.
- Có API contract/DTO rõ.
- Có frontend API/hook/UI integration nếu UC có UI.
- Có test case thủ công.
- Build backend/frontend pass.
- Cập nhật docs/changelog.
- Báo cáo theo format chuẩn.
```

---

# Kết luận

File này là quy ước chung để code các UC của PEMS thống nhất.

Ưu tiên cao nhất:

```text
1. Chạy thật.
2. Đúng quyền.
3. Đúng scope campus/role.
4. Không lộ dữ liệu nhạy cảm.
5. Không phá frontend hiện có.
6. Có validate + chống spam.
7. Có build/test/changelog rõ ràng.
```
