# PROMPT AI — TẠO TEST CODE THẬT CHO UC-105 VIEW DEPARTMENT DETAILS  
## UNIT TEST + INTEGRATION TEST, KHÔNG DOCKER, AN TOÀN `pems_test` — v1

> File này dùng để đưa cho AI Agent/Code Agent khi cần tạo hoặc cập nhật test tự động cho chức năng **View Department Details** trong dự án PEMS.
>
> Use case thuộc nhóm **Department Management**. Theo `USE_CASE_LIST.md`, tài liệu UC-105 và source hiện tại, UC ổn định là **UC-105 View Department Details**, actor nghiệp vụ duy nhất là **Staff Leader**.
>
> Mục tiêu bắt buộc:
>
> - Tạo **test code thật**, build và chạy được.
> - Test đúng source hiện tại, không code theo route hoặc contract đã lỗi thời trong tài liệu.
> - Có cả positive, negative, authorization, scope, boundary, projection và read-only.
> - Không tạo test giả, không dùng `Assert.True(true)`, không để `[Fact(Skip = ...)]`.
> - Không sửa production code chỉ để làm test xanh.
> - Trước Integration Test phải kiểm tra trực tiếp database đang dùng có đúng là `pems_test` và đủ prerequisite hay không bằng truy vấn read-only.
> - Tuyệt đối không động đến pems_db, không làm hỏng `pems_db`, database dev, database staging hoặc dữ liệu thật.
> - Không dùng Docker/Testcontainers.
>
> Yêu cầu chất lượng:
>
> ```text
> Viết testcase bao phủ đầy đủ hành vi thật của tính năng.
> Không viết testcase chỉ toàn positive để lấy kết quả pass.
> Không viết testcase self-fulfilling, nghĩa là seed gì rồi chỉ assert đúng thứ vừa seed mà không chứng minh handler đã lọc/scope/project đúng.
> Không dùng mock để che mất EF Core + Pomelo MySQL behavior cần kiểm tra thật.
> ```

---

# 0. BASELINE SOURCE HIỆN TẠI ĐÃ XÁC NHẬN

AI Agent phải đọc lại source tại thời điểm thực hiện vì code có thể thay đổi.

Baseline dưới đây được xác nhận trên:

```text
Repository: quangthoai04/PEMS
Branch: Dev
Commit tham chiếu: 438f08a3daf42bf797323d38233078892277d8a6
```

Nếu source mới khác baseline này:

1. Dùng source mới làm chuẩn.
2. Không ép test bám baseline cũ.
3. Ghi rõ mọi khác biệt trong báo cáo cuối.
4. Nếu khác biệt là production bug hoặc security regression, không tự sửa ngoài phạm vi; phải báo riêng.

## 0.1. UC và actor thật

```text
UC: UC-105 View Department Details
Primary Actor: Staff Leader
Effective role:
- role_code = STAFF
- sub_role = LEADER
```

Không dùng runtime role legacy:

```text
STAFF_LEADER
STAFF_L
IC_STAFF_LEADER
DEPT
DEPARTMENT_LEADER
DEPT_LEADER
```

## 0.2. Endpoint thật

Source hiện tại:

```http
GET /api/departments/viewdepartmentdetails?departmentId={departmentId}
```

Controller:

```text
backend/PEMS.Api/Controllers/DepartmentsController.cs
Action: ViewDepartmentDetails
Binding: [FromQuery] ViewDepartmentDetailsQuery
```

Điểm quan trọng:

```text
- Endpoint thật dùng query string.
- KHÔNG phải GET /api/departments/{departmentId}.
- Tài liệu UC-105 cũ từng đề xuất route /api/departments/{departmentId}, nhưng source/frontend hiện tại không dùng route đó.
- Test phải gọi endpoint thật.
```

## 0.3. Controller authorization thật

`DepartmentsController` hiện:

```text
- Có [ApiController].
- Có [Route("api/[controller]")].
- Không có [Authorize] ở class.
- Action ViewDepartmentDetails không có [Authorize]/[RoleAuthorize].
```

Vì vậy authorization hiện được enforce trong handler qua:

```text
StaffLeaderDepartmentScope.EnsureStaffLeaderCampus(...)
```

Hệ quả contract hiện tại:

```text
Anonymous -> handler guard -> 403 DEPARTMENT_MANAGEMENT_FORBIDDEN
Wrong role -> handler guard -> 403 DEPARTMENT_MANAGEMENT_FORBIDDEN
Staff Leader thiếu PrimaryCampusId claim -> 422 DEPARTMENT_NO_CAMPUS_ASSIGNED
```

Không được viết test kỳ vọng 401 chỉ vì request anonymous. Source hiện tại trả 403 cho UC này.

Nếu sau này controller được thêm `[Authorize]` và anonymous đổi thành 401:

```text
- Cập nhật test theo source mới.
- Ghi rõ đây là thay đổi authorization pipeline.
- Không giữ 403 chỉ để phù hợp prompt cũ.
```

## 0.4. Query thật

```csharp
public sealed class ViewDepartmentDetailsQuery : IRequest<ViewDepartmentDetailsDto>
{
    public ulong DepartmentId { get; set; }
}
```

Contract:

```text
- Chỉ có DepartmentId.
- Kiểu ulong.
- Không có campusId từ client.
- Không có status/type/head query param.
- DepartmentId mặc định của object mới là 0.
- Không có ViewDepartmentDetailsQueryValidator.
```

Không tự bịa validator.

## 0.5. Handler thật

File:

```text
backend/PEMS.Application/Departments/Queries/ViewDepartmentDetails/ViewDepartmentDetailsQueryHandler.cs
```

Thứ tự xử lý thật:

```text
1. Gọi StaffLeaderDepartmentScope.EnsureStaffLeaderCampus.
2. Query Departments.AsNoTracking().
3. Tìm đúng d.DepartmentId == request.DepartmentId.
4. Project Department + Campus + optional HeadUser.
5. Không tìm thấy -> NotFoundException("Department", id) -> HTTP 404.
6. Tìm thấy nhưng row.CampusId != current Staff Leader campus -> 403 DEPARTMENT_SCOPE_FORBIDDEN.
7. isGeneral = DepartmentType == "GENERAL".
8. Trả DTO.
```

Điểm security quan trọng:

```text
- Actor guard chạy trước resource lookup.
- Campus scope lấy từ currentUser.PrimaryCampusId.
- Client không được truyền campusId để override.
- Department tồn tại khác campus trả 403.
- Department không tồn tại trả 404.
```

## 0.6. EF/read model thật

Query hiện tại dùng:

```text
AsNoTracking()
Where exact DepartmentId
Select projection
FirstOrDefaultAsync()
```

Projection:

```text
DepartmentId
Name
CampusId
Campus.CampusCode
Campus.Name
HeadUserId
HeadUser.FullName nếu HeadUser != null
Status
DepartmentType
```

Không trả entity trực tiếp.

Không load:

```text
Head email
Head phone
Head role/sub-role
Head department
Department users
Department tasks
Audit fields
CreatedAt/CreatedBy
UpdatedAt/UpdatedBy
```

## 0.7. DTO thật

```text
ViewDepartmentDetailsDto
- DepartmentId: ulong
- Name: string
- CampusId: ulong
- CampusCode: string?
- CampusName: string
- HeadUserId: ulong?
- HeadFullName: string?
- Status: string
- DepartmentType: string
- CanEditName: bool
- CanToggleStatus: bool
```

UI flags:

```text
GENERAL:
- CanEditName = true
- CanToggleStatus = true

IC:
- CanEditName = false
- CanToggleStatus = false
```

`departmentType` có trong API response để frontend quyết định action, nhưng không phải field bắt buộc hiển thị trong modal.

## 0.8. Status behavior thật

Handler không filter status.

Do đó:

```text
ACTIVE department -> xem được nếu cùng campus.
INACTIVE department -> vẫn xem được nếu cùng campus.
```

Không được viết test giả định INACTIVE trả 404/403.

## 0.9. Head nullable thật

```text
head_user_id = NULL:
- HeadUserId = null
- HeadFullName = null
- Request vẫn 200 OK

head_user_id có user:
- HeadUserId trả đúng id
- HeadFullName trả đúng FullName
```

Không coi chưa gán trưởng phòng là lỗi.

## 0.10. Error response thật

### Wrong actor / no campus / cross-campus

Các case dùng `AuthBusinessException`, response có:

```json
{
  "success": false,
  "errorCode": "...",
  "message": "...",
  "traceId": "..."
}
```

Error codes:

```text
DEPARTMENT_MANAGEMENT_FORBIDDEN
DEPARTMENT_NO_CAMPUS_ASSIGNED
DEPARTMENT_SCOPE_FORBIDDEN
```

### Department không tồn tại

Handler hiện dùng:

```csharp
new NotFoundException("Department", request.DepartmentId)
```

`ExceptionHandlingMiddleware` trả:

```json
{
  "success": false,
  "message": "Department (...) was not found.",
  "traceId": "..."
}
```

Điểm cần test đúng:

```text
- HTTP 404.
- success = false.
- message có ý nghĩa.
- errorCode hiện không có/null.
```

`DepartmentErrorCodes.DepartmentNotFound` có tồn tại trong constants nhưng handler UC-105 hiện không sử dụng nó.

Không tự sửa handler để trả error code chỉ vì constant tồn tại.

## 0.11. Frontend contract thật

Frontend hiện dùng:

```text
API_ENDPOINTS.departments.details
= /departments/viewdepartmentdetails
```

API call:

```text
GET /departments/viewdepartmentdetails
params: { departmentId }
```

Frontend type `DepartmentDetail` khớp DTO backend.

## 0.12. Test hiện có

Legacy placeholder:

```text
tests/PEMS.ApplicationTests/Departments/ViewDepartmentDetailsQueryTests.cs
```

Hiện chỉ có:

```csharp
[Fact(Skip = "Pending UC specification")]
public async Task Handle_Should_Process_ViewDepartmentDetailsQuery()
{
    // TODO
}
```

Đây không phải test thật.

Hiện chưa thấy:

```text
tests/PEMS.IntegrationTests/Departments/ViewDepartmentDetails/ViewDepartmentDetailsApiTests.cs
```

## 0.13. Test infrastructure hiện tại

Đã có:

```text
PemsWebApplicationFactory
TestAuthHandler
DatabaseResetHelper
AssemblyInfo.cs DisableTestParallelization = true
```

`PemsWebApplicationFactory`:

```text
- Environment = Testing.
- Chỉ đọc backend/PEMS.Api/appsettings.Testing.json.
- Dùng MySQL thật qua Pomelo.
- Database phải là pems_test.
- Không fallback appsettings.Development.json.
- JWT được thay bằng TestAuthHandler.
- SessionValidationMiddleware vẫn chạy thật.
```

`DatabaseResetHelper` hiện có prefix UC-101..UC-104 nhưng chưa có UC-105:

```text
[IT-UC101-ADD-DEPARTMENT]
[IT-UC102-UPDATE-DEPARTMENT]
[IT-UC103-SEARCH-FILTER-DEPARTMENT]
[IT-UC104-VIEW-DEPARTMENT-LIST]
```

Prefix UC-105 bắt buộc bổ sung:

```csharp
public const string ViewDepartmentDetailsNamePrefix =
    "[IT-UC105-VIEW-DEPARTMENT-DETAILS] ";
```

---

# 1. KINH NGHIỆM PHẢI KẾ THỪA TỪ PROMPT UC-104

Kế thừa các quy tắc tốt từ prompt View Department List:

```text
- Dùng xUnit.
- Unit Test chỉ test pure contract có thật.
- Chỉ dùng FluentValidation.TestHelper khi production có validator thật.
- Integration Test chạy qua WebApplicationFactory/TestServer.
- Authentication dùng TestAuthHandler.
- Session phải là session thật trong pems_test để SessionValidationMiddleware chấp nhận.
- Mỗi UC có prefix riêng.
- Cleanup chỉ xóa row đúng prefix UC đó.
- Không exact-count toàn database.
- Không dùng dữ liệu seed nền làm target mutating.
- Read-only test phải snapshot trước/sau và reload DbContext.
- Không dùng EF InMemory thay MySQL cho hành vi cần SQL thật.
- Khi full suite fail, kiểm tra fixture/cleanup/race trước khi kết luận production bug.
```

Bài học chống pass giả áp dụng riêng cho detail:

```text
- Không chỉ seed một row rồi Assert.Equal(id), vì handler lỗi có thể trả row đầu tiên cùng campus.
- Phải seed target + same-campus distractor và gọi đúng target id.
- Không chỉ assert 403; phải assert đúng errorCode.
- 404 phải kiểm tra contract thật không có errorCode.
- Assigned head phải assert cả HeadUserId và HeadFullName.
- Unassigned head phải assert cả hai field null.
- GENERAL/IC phải assert cả DepartmentType và hai UI flags.
- Read-only không chỉ tin AsNoTracking; phải reload DB và so sánh state.
- Response security không thể kiểm tra bằng DTO deserialize vì extra JSON field sẽ bị bỏ qua; phải dùng JsonDocument để kiểm tra property set khi test overexposure.
```

---

# 2. LƯU Ý UC ID VÀ TÀI LIỆU LEGACY

Nguồn hiện tại:

```text
UC-101 Add New Department
UC-102 Update Department
UC-103 Search and Filter Departments
UC-104 View Department List
UC-105 View Department Details
UC-106 Manage Department Status
```

UC-105 không được trộn với:

```text
UC-102 Update Department
UC-104 View Department List
UC-106 Manage Department Status
UC-107 Add Department Personnel
UC-108 View Personnel Details
```

UC-105 chỉ đọc general detail.

Không test trong UC-105:

```text
- Rename department.
- Change status.
- Assign/reassign head.
- Add/remove personnel.
- Search/list/pagination.
- Task/delegation history.
- Modal state frontend.
```

Có thể test flags `CanEditName` và `CanToggleStatus` vì đây là response contract của UC-105, nhưng không gọi mutation endpoint trong class UC-105.

---

# 3. MỤC TIÊU TASK

Tạo/cập nhật test tự động cho:

```text
Staff Leader xem chi tiết một Department thuộc campus của mình.
```

Phạm vi:

## 3.1. Unit Test

Chỉ test pure contract thật:

```text
- ViewDepartmentDetailsQuery mới có DepartmentId mặc định = 0.
```

Không mock EF handler chỉ để có unit test.

## 3.2. Integration Test

Phải đi qua:

```text
HTTP request thật
-> TestServer
-> Controller thật
-> Model binding thật
-> Authentication/TestAuthHandler
-> SessionValidationMiddleware
-> MediatR
-> StaffLeaderDepartmentScope
-> ViewDepartmentDetailsQueryHandler
-> EF Core Pomelo
-> MySQL pems_test
-> ExceptionHandlingMiddleware
-> JSON response thật
```

Sau task, report phải trả lời được:

```text
- Endpoint và method thật là gì?
- DepartmentId bind từ route hay query?
- Actor hợp lệ là ai?
- Anonymous hiện trả 401 hay 403?
- Staff Leader không có campus trả gì?
- Department khác campus trả gì?
- Department không tồn tại trả gì?
- 404 có errorCode hay không?
- GENERAL và IC khác nhau ở flags nào?
- ACTIVE/INACTIVE có đều xem được không?
- head_user_id null được project thế nào?
- Response có lộ field nhạy cảm của head hoặc audit fields không?
- GET có thực sự read-only không?
- pems_test có đủ role/campus/IC/GENERAL/second campus không?
- Test nào pass/fail?
```

---

# 4. NGUỒN BẮT BUỘC ĐỌC TRƯỚC KHI SỬA

## 4.1. Tài liệu

```text
USE_CASE_LIST.md
PERMISSION_MATRIX.md
PERMISSION_RULES.md
docs/Department_Staff_Leader/UC-105_VIEW_DEPARTMENT_DETAILS_STAFF_LEADER.md
PROJECT_OVERVIEW_v8_4_refined_v6_v10_FULL_UPDATED.md
PEMS_CANONICAL_BUSINESS_RULES_v8_4_refined_v6_v10_FULL_UPDATED.md
PEMS_UC_IMPLEMENTATION_RULEBOOK_FRONTEND_BACKEND_DATABASE_VALIDATION_SECURITY_v8_4_refined_v6_v10_FULL_UPDATED.md
PROMPT_STANDARDIZE_ROLE_SUBROLE_DEPARTMENT_v8_4_refined_v6_v10_FULL_UPDATED.md
CLEAN_ARCHITECTURE.md
SQL fresh-create mới nhất
Prompt UC-104 được đính kèm để kế thừa cấu trúc và database-safety convention
```

## 4.2. Backend source

```text
backend/PEMS.Api/Controllers/DepartmentsController.cs

backend/PEMS.Application/Departments/Queries/ViewDepartmentDetails/
- ViewDepartmentDetailsQuery.cs
- ViewDepartmentDetailsQueryHandler.cs
- ViewDepartmentDetailsDto.cs

backend/PEMS.Application/Departments/Common/
- StaffLeaderDepartmentScope.cs
- DepartmentErrorCodes.cs

backend/PEMS.Application/Common/Exceptions/
- NotFoundException.cs
- AuthBusinessException.cs

backend/PEMS.Api/Middleware/
- ExceptionHandlingMiddleware.cs
- SessionValidationMiddleware.cs

backend/PEMS.Application/Common/Interfaces/
- IApplicationDbContext.cs
- ICurrentUserService.cs

backend/PEMS.Domain/Entities/Departments/Department.cs
backend/PEMS.Domain/Entities/Campuses/Campus.cs
backend/PEMS.Domain/Entities/Users/User.cs

backend/PEMS.Infrastructure/Persistence/ApplicationDbContext.cs
cấu hình EF/FK cho Department, Campus, User nếu có
```

## 4.3. Frontend source để xác nhận contract

```text
frontend/pems-react/src/shared/api/endpoints.ts
frontend/pems-react/src/features/department-management/api/departmentManagementApi.ts
frontend/pems-react/src/features/department-management/types/departmentManagement.types.ts
frontend/pems-react/src/pages/dashboard/departments/DepartmentManagement.tsx
```

Không cần sửa frontend trong task test backend này, trừ khi user yêu cầu riêng.

## 4.4. Test source

```text
tests/PEMS.ApplicationTests/Departments/ViewDepartmentDetailsQueryTests.cs

tests/PEMS.UnitTests/Departments/ViewDepartmentList/ViewDepartmentListQueryTests.cs

tests/PEMS.IntegrationTests/Departments/ViewDepartmentList/ViewDepartmentListApiTests.cs
tests/PEMS.IntegrationTests/Departments/SearchFilterDepartments/SearchFilterDepartmentsApiTests.cs
tests/PEMS.IntegrationTests/Departments/AddNewDepartment/AddNewDepartmentApiTests.cs
tests/PEMS.IntegrationTests/Departments/UpdateDepartment/UpdateDepartmentApiTests.cs

tests/PEMS.IntegrationTests/TestInfrastructure/
- PemsWebApplicationFactory.cs
- TestAuthHandler.cs
- DatabaseResetHelper.cs

tests/PEMS.IntegrationTests/AssemblyInfo.cs
backend/PEMS.Api/appsettings.Testing.example.json
```

## 4.5. Source-first rule

Trước khi code, Agent phải trả lời nội bộ:

```text
1. Route thật là gì?
2. Query bind từ đâu?
3. Có validator thật không?
4. Actor guard nằm controller hay handler?
5. Handler check actor trước hay resource trước?
6. 404 dùng exception nào?
7. 404 có errorCode không?
8. Scope cross-campus trả 403 code gì?
9. IC flags là gì?
10. Response field set chính xác là gì?
11. Existing helper có thể seed/cleanup gì?
12. Live pems_test đang có prerequisite gì?
```

Không bắt đầu viết test nếu chưa trả lời được.

---

# 5. NGHIỆP VỤ UC-105 PHẢI GIỮ ĐÚNG

## 5.1. Actor hợp lệ

Chỉ:

```text
IsAuthenticated = true
RoleCode = STAFF
SubRole = LEADER
PrimaryCampusId != null
```

Không cấp quyền cho:

```text
HO
ADMIN
STAFF + STAFF
DEPARTMENT + LEADER
DEPARTMENT + STAFF
STUDENT
VISITOR
Anonymous
```

## 5.2. Campus scope

Rule:

```text
department.CampusId == currentUser.PrimaryCampusId
```

Không dùng:

```text
campusId query param
selected campus từ UI
departmentId suy luận campus ở client
email pattern
role label legacy
```

## 5.3. Exact identity

Handler phải trả đúng department được yêu cầu.

Test phải chứng minh:

```text
- Có target.
- Có same-campus distractor.
- Gọi target DepartmentId.
- Response là target, không phải distractor.
```

## 5.4. Department not found

```text
Valid Staff Leader + id không tồn tại -> 404.
```

Không biến thành:

```text
200 null
200 empty object
403
422
500
```

## 5.5. Other-campus department

```text
Department tồn tại + khác campus -> 403 DEPARTMENT_SCOPE_FORBIDDEN.
```

Không chỉ assert 403; phải assert code.

## 5.6. GENERAL flags

```text
DepartmentType = GENERAL
CanEditName = true
CanToggleStatus = true
```

## 5.7. IC flags

```text
DepartmentType = IC
CanEditName = false
CanToggleStatus = false
```

IC vẫn trả 200 nếu cùng campus.

Không tạo IC thứ hai trong cùng campus nếu DB trigger chỉ cho một IC/campus.

## 5.8. Status

```text
ACTIVE -> 200.
INACTIVE -> 200.
```

## 5.9. Nullable head

```text
No head:
HeadUserId = null
HeadFullName = null

Assigned head:
HeadUserId = expected
HeadFullName = expected
```

## 5.10. Response minimization

Expected top-level camelCase JSON properties:

```text
departmentId
name
campusId
campusCode
campusName
headUserId
headFullName
status
departmentType
canEditName
canToggleStatus
```

Không được lộ:

```text
headEmail
email
phone
roleCode
subRole
passwordHash
users
createdAt
createdBy
updatedAt
updatedBy
```

## 5.11. Read-only

GET detail không được:

```text
- Update Department.
- Gán head.
- Ghi UpdatedAt/UpdatedBy.
- Tạo/xóa Department.
- Đổi status.
```

Test phải reload DB sau request.

---

# 6. KIỂM TRA TRỰC TIẾP `pems_test` TRƯỚC INTEGRATION TEST

Phần này bắt buộc.

Không được chỉ đọc SQL seed rồi tuyên bố live database đủ dữ liệu.

## 6.1. Safety gate

Trước khi người dùng cho phép chạy Integration Test có ghi/cleanup DB, chỉ chạy:

```sql
SELECT ...
SHOW ...
DESCRIBE ...
EXPLAIN ...
```

Không tự chạy:

```text
INSERT
UPDATE
DELETE
TRUNCATE
ALTER
DROP
CREATE DATABASE
CREATE TABLE
mysql < file.sql
dotnet test IntegrationTests
```

Lý do:

```text
Integration tests hiện tạo users/sessions/departments và cleanup departments.
Đó là thao tác ghi DB dù endpoint UC-105 là read-only.
```

## 6.2. Xác nhận database

```sql
SELECT DATABASE() AS current_database;
```

Expected:

```text
pems_test
```

Nếu:

```text
NULL
pems_db
database khác
```

thì dừng.

Không chạy Integration Test.

## 6.3. Xác nhận schema/table

```sql
SELECT table_name
FROM information_schema.tables
WHERE table_schema = DATABASE()
  AND table_name IN (
      'roles',
      'campuses',
      'departments',
      'users',
      'user_sessions'
  )
ORDER BY table_name;
```

Expected đủ 5 bảng.

Kiểm tra columns:

```sql
SELECT
    table_name,
    column_name,
    data_type,
    is_nullable,
    column_type
FROM information_schema.columns
WHERE table_schema = DATABASE()
  AND (
      (table_name = 'departments' AND column_name IN (
          'department_id',
          'campus_id',
          'name',
          'department_type',
          'head_user_id',
          'status',
          'created_at',
          'created_by',
          'updated_at',
          'updated_by'
      ))
      OR
      (table_name = 'campuses' AND column_name IN (
          'campus_id',
          'campus_code',
          'name',
          'status'
      ))
      OR
      (table_name = 'users' AND column_name IN (
          'user_id',
          'full_name',
          'email',
          'role_id',
          'sub_role',
          'primary_campus_id',
          'department_id',
          'status'
      ))
  )
ORDER BY table_name, ordinal_position;
```

## 6.4. Role prerequisite

```sql
SELECT role_id, role_code, status
FROM roles
WHERE role_code IN (
    'STAFF',
    'HO',
    'ADMIN',
    'DEPARTMENT',
    'STUDENT',
    'VISITOR'
)
ORDER BY role_code;
```

Cần đủ role cho authorization matrix.

Summary:

```sql
SELECT
    COUNT(DISTINCT CASE WHEN role_code = 'STAFF' THEN role_code END) AS has_staff,
    COUNT(DISTINCT CASE WHEN role_code = 'HO' THEN role_code END) AS has_ho,
    COUNT(DISTINCT CASE WHEN role_code = 'ADMIN' THEN role_code END) AS has_admin,
    COUNT(DISTINCT CASE WHEN role_code = 'DEPARTMENT' THEN role_code END) AS has_department,
    COUNT(DISTINCT CASE WHEN role_code = 'STUDENT' THEN role_code END) AS has_student,
    COUNT(DISTINCT CASE WHEN role_code = 'VISITOR' THEN role_code END) AS has_visitor
FROM roles
WHERE status = 'ACTIVE';
```

## 6.5. Active campuses

```sql
SELECT campus_id, campus_code, name, status
FROM campuses
WHERE status = 'ACTIVE'
ORDER BY campus_id;
```

```sql
SELECT COUNT(*) AS active_campus_count
FROM campuses
WHERE status = 'ACTIVE';
```

Required:

```text
>= 1 cho core success.
>= 2 cho cross-campus scope test.
```

## 6.6. Campus có IC + GENERAL

```sql
SELECT
    c.campus_id,
    c.campus_code,
    c.name AS campus_name,
    c.status AS campus_status,
    SUM(
        CASE
            WHEN d.department_type = 'IC'
             AND d.status = 'ACTIVE'
            THEN 1 ELSE 0
        END
    ) AS active_ic_count,
    SUM(
        CASE
            WHEN d.department_type = 'GENERAL'
             AND d.status = 'ACTIVE'
            THEN 1 ELSE 0
        END
    ) AS active_general_count,
    SUM(
        CASE
            WHEN d.department_type = 'GENERAL'
             AND d.status = 'INACTIVE'
            THEN 1 ELSE 0
        END
    ) AS inactive_general_count,
    COUNT(d.department_id) AS total_department_count
FROM campuses c
LEFT JOIN departments d
       ON d.campus_id = c.campus_id
WHERE c.status = 'ACTIVE'
GROUP BY c.campus_id, c.campus_code, c.name, c.status
ORDER BY c.campus_id;
```

Cần ít nhất một campus có:

```text
active_ic_count >= 1
active_general_count >= 1
```

Vì:

```text
STAFF/Staff Leader fixture cần IC department.
DEPARTMENT/Department Lead fixture cần GENERAL department.
IC detail test cần một IC department có thật.
```

## 6.7. Existing deterministic fixture users

`DatabaseResetHelper.EnsureTestUserAsync` trả existing user theo email mà không tái-validate toàn bộ trạng thái/scope.

Vì vậy phải kiểm tra các fixture cũ:

```sql
SELECT
    u.user_id,
    u.full_name,
    u.email,
    r.role_code,
    u.sub_role,
    u.primary_campus_id,
    u.department_id,
    u.status AS user_status,
    c.status AS campus_status,
    d.department_type,
    d.status AS department_status
FROM users u
JOIN roles r
  ON r.role_id = u.role_id
LEFT JOIN campuses c
  ON c.campus_id = u.primary_campus_id
LEFT JOIN departments d
  ON d.department_id = u.department_id
WHERE u.email LIKE 'it-uc63-%@it-uc63.pems.local'
ORDER BY u.email;
```

Đặc biệt Staff Leader fixture:

```sql
SELECT
    u.user_id,
    u.email,
    r.role_code,
    u.sub_role,
    u.primary_campus_id,
    u.department_id,
    u.status AS user_status,
    c.status AS campus_status,
    d.department_type,
    d.status AS department_status
FROM users u
JOIN roles r ON r.role_id = u.role_id
LEFT JOIN campuses c ON c.campus_id = u.primary_campus_id
LEFT JOIN departments d ON d.department_id = u.department_id
WHERE u.email = 'it-uc63-staffleader@it-uc63.pems.local';
```

Nếu tồn tại, expected:

```text
role_code = STAFF
sub_role = LEADER
user_status = ACTIVE
primary_campus_id != null
campus_status = ACTIVE
department_id != null
department_type = IC
department_status = ACTIVE
```

Nếu fixture cũ invalid:

```text
- Không để helper reuse mù quáng.
- Không tự sửa row.
- Báo rõ.
- Có thể bổ sung test-only validation/helper sau khi được cho phép.
```

## 6.8. Staff Leaders seed thật

```sql
SELECT
    u.user_id,
    u.email,
    r.role_code,
    u.sub_role,
    u.primary_campus_id,
    u.department_id,
    u.status AS user_status,
    c.campus_code,
    c.status AS campus_status,
    d.department_type,
    d.status AS department_status
FROM users u
JOIN roles r ON r.role_id = u.role_id
LEFT JOIN campuses c ON c.campus_id = u.primary_campus_id
LEFT JOIN departments d ON d.department_id = u.department_id
WHERE r.role_code = 'STAFF'
  AND u.sub_role = 'LEADER'
ORDER BY u.primary_campus_id, u.user_id;
```

Có valid seed Staff Leader là tốt nhưng Integration Test vẫn nên dùng deterministic helper user/session, không hardcode user_id.

## 6.9. Department distribution

```sql
SELECT
    campus_id,
    department_type,
    status,
    COUNT(*) AS department_count
FROM departments
GROUP BY campus_id, department_type, status
ORDER BY campus_id, department_type, status;
```

Mục đích:

```text
- Biết live seed.
- Không exact-count toàn campus.
- Xác định campus fixture có IC.
- Xác định second campus.
```

## 6.10. Check stale UC-105 rows

```sql
SELECT
    d.department_id,
    d.campus_id,
    d.name,
    d.department_type,
    d.status,
    d.head_user_id,
    d.created_by,
    d.updated_by
FROM departments d
WHERE d.name LIKE '[IT-UC105-VIEW-DEPARTMENT-DETAILS]%'
   OR d.name LIKE '[IT-VIEW-DEPARTMENT-DETAILS]%'
ORDER BY d.department_id;
```

Không tự delete trước khi được phép.

## 6.11. Check dependency của stale rows

Tối thiểu:

```sql
SELECT
    d.department_id,
    d.name,
    COUNT(u.user_id) AS user_reference_count
FROM departments d
LEFT JOIN users u
       ON u.department_id = d.department_id
WHERE d.name LIKE '[IT-UC105-VIEW-DEPARTMENT-DETAILS]%'
GROUP BY d.department_id, d.name
ORDER BY d.department_id;
```

Liệt kê FK nào đang trỏ tới departments:

```sql
SELECT
    table_name,
    column_name,
    constraint_name,
    referenced_table_name,
    referenced_column_name
FROM information_schema.key_column_usage
WHERE referenced_table_schema = DATABASE()
  AND referenced_table_name = 'departments'
ORDER BY table_name, column_name;
```

Nếu stale test department có dependency:

```text
- Không xóa cưỡng bức.
- Báo dependency.
- Không disable foreign_key_checks.
```

## 6.12. Prefix overlap

```sql
SELECT department_id, campus_id, name
FROM departments
WHERE name LIKE '[IT-UC101-ADD-DEPARTMENT]%'
   OR name LIKE '[IT-UC102-UPDATE-DEPARTMENT]%'
   OR name LIKE '[IT-UC103-SEARCH-FILTER-DEPARTMENT]%'
   OR name LIKE '[IT-UC104-VIEW-DEPARTMENT-LIST]%'
   OR name LIKE '[IT-UC105-VIEW-DEPARTMENT-DETAILS]%'
ORDER BY department_id;
```

Prefix UC-105 không được là prefix con của UC khác.

## 6.13. Session accumulation

```sql
SELECT
    u.user_id,
    u.email,
    COUNT(s.session_id) AS session_count,
    SUM(CASE WHEN s.revoked_at IS NULL AND s.expires_at > NOW() THEN 1 ELSE 0 END) AS active_session_count
FROM users u
LEFT JOIN user_sessions s
       ON s.user_id = u.user_id
WHERE u.email LIKE 'it-uc63-%@it-uc63.pems.local'
GROUP BY u.user_id, u.email
ORDER BY u.email;
```

Chỉ report.

Không tự xóa sessions nếu task không yêu cầu.

## 6.14. Database readiness table bắt buộc

Agent phải report:

| Prerequisite | Required | Actual | Ready? |
|---|---:|---:|---|
| Current DB là `pems_test` | 1 | ... | Có/Không |
| Bảng roles | 1 | ... | Có/Không |
| Bảng campuses | 1 | ... | Có/Không |
| Bảng departments | 1 | ... | Có/Không |
| Bảng users | 1 | ... | Có/Không |
| Bảng user_sessions | 1 | ... | Có/Không |
| Role STAFF | 1 | ... | Có/Không |
| Role HO | 1 | ... | Có/Không |
| Role ADMIN | 1 | ... | Có/Không |
| Role DEPARTMENT | 1 | ... | Có/Không |
| Role STUDENT | 1 | ... | Có/Không |
| Role VISITOR | 1 | ... | Có/Không |
| Active campuses | >= 1 | ... | Có/Không |
| Active campuses cho scope | >= 2 | ... | Có/Không |
| Active IC ở fixture campus | >= 1 | ... | Có/Không |
| Active GENERAL ở fixture campus | >= 1 | ... | Có/Không |
| Deterministic Staff Leader fixture valid hoặc có thể tạo | 1 | ... | Có/Không |
| Second campus cho cross-scope | 1 | ... | Có/Không |
| Stale UC-105 rows | 0 mong muốn | ... | Có/Không |
| Stale UC-105 rows có FK dependency | 0 | ... | Có/Không |

Không được viết:

```text
pems_test đủ dữ liệu
```

nếu chưa chạy các SELECT trên.

## 6.15. Kết luận từ fresh SQL đính kèm

Fresh SQL được rà soát tĩnh cho thấy baseline seed có tối thiểu:

```text
- 6 role chuẩn: ADMIN, HO, STAFF, DEPARTMENT, STUDENT, VISITOR.
- 5 ACTIVE campus: HN, HCM, DN, CT, QN.
- 5 ACTIVE IC departments, mỗi campus một IC.
- Nhiều ACTIVE GENERAL departments.
- Ít nhất một INACTIVE GENERAL department.
- Staff Leader seed cho nhiều campus.
- Department Leader/Staff/Student/Visitor/Admin/HO seed.
```

Vì vậy:

```text
Fresh-imported test database đúng schema/seed có vẻ đủ prerequisite cho UC-105.
```

Nhưng lưu ý:

```text
- SQL file dùng USE pems_db trong seed section.
- Đây không phải bằng chứng live pems_test đang đúng.
- Không import file trực tiếp vào pems_test trong task này.
- Live readiness vẫn phải xác nhận bằng SELECT.
```

## 6.16. Khi live pems_test thiếu dữ liệu

```text
- Không import fresh-create tự động.
- Không INSERT/UPDATE thủ công.
- Báo thiếu chính xác.
- Ưu tiên test helper tạo dữ liệu tối thiểu sau khi được phép.
- Không sửa seed production chỉ để test.
- Không dùng pems_db thay thế.
```

---

# 7. UNIT TEST, APPLICATION TEST VÀ INTEGRATION TEST

## 7.1. Không có validator thì không tạo validator test

Không có:

```text
ViewDepartmentDetailsQueryValidator
```

Không tạo:

```text
ViewDepartmentDetailsQueryValidatorTests
```

Không thêm validator production chỉ để có thứ test.

## 7.2. Existing skipped test không được tính

File skipped phải được xử lý.

Ưu tiên:

```text
- Tạo test chuẩn trong PEMS.UnitTests.
- Xóa placeholder cũ ở PEMS.ApplicationTests nếu project này không còn là chuẩn.
```

Hoặc:

```text
- Chuyển placeholder thành test thật nếu team vẫn dùng PEMS.ApplicationTests.
```

Không để hai test file trùng mục đích ở hai project.

Report quyết định.

## 7.3. Unit Test có ý nghĩa tối thiểu

Test đề xuất:

```text
NewQuery_DefaultsDepartmentIdToZero
```

Assert:

```csharp
var query = new ViewDepartmentDetailsQuery();
Assert.Equal(0UL, query.DepartmentId);
```

Ý nghĩa:

```text
- Chứng minh pure object contract hiện tại.
- Liên quan trực tiếp tới missing query param -> handler nhận id 0.
```

Không tạo thêm test kiểu:

```text
Set DepartmentId = 123 rồi đọc lại 123
```

vì chỉ test auto-property của C#.

## 7.4. Không mock handler chỉ để pass

Handler phụ thuộc:

```text
IApplicationDbContext
ICurrentUserService
EF async query provider
navigation projection
Pomelo translation
```

Mock `DbSet`/`IQueryable` phức tạp sẽ dễ:

```text
- Test implementation của mock.
- Không chứng minh SQL thật.
- Che lỗi navigation/FK/collation/provider.
```

Core handler behavior phải ở Integration Test.

## 7.5. Không dùng EF InMemory

Không dùng EF InMemory làm bằng chứng cho:

```text
FirstOrDefaultAsync translation
navigation projection
unsigned bigint behavior
query model binding
MySQL enum/string data
FK/trigger behavior
```

---

# 8. TỔ CHỨC FILE

## 8.1. Unit Test

Ưu tiên:

```text
tests/PEMS.UnitTests/Departments/ViewDepartmentDetails/ViewDepartmentDetailsQueryTests.cs
```

Namespace:

```csharp
PEMS.UnitTests.Departments.ViewDepartmentDetails
```

## 8.2. Integration Test

Tạo:

```text
tests/PEMS.IntegrationTests/Departments/ViewDepartmentDetails/ViewDepartmentDetailsApiTests.cs
```

Namespace:

```csharp
PEMS.IntegrationTests.Departments.ViewDepartmentDetails
```

## 8.3. Infrastructure

Sửa tối thiểu:

```text
tests/PEMS.IntegrationTests/TestInfrastructure/DatabaseResetHelper.cs
```

Bổ sung:

```csharp
public const string ViewDepartmentDetailsNamePrefix =
    "[IT-UC105-VIEW-DEPARTMENT-DETAILS] ";
```

Dùng lại:

```text
EnsureTestUserAsync
CreateActiveSessionAsync
CreateTestDepartmentAsync
DeleteTestDepartmentsAsync
```

Không copy helper vào test class nếu helper chung đã có.

## 8.4. Parallelization

Xác nhận vẫn có:

```csharp
[assembly: CollectionBehavior(DisableTestParallelization = true)]
```

Không tạo attribute trùng.

---

# 9. PHẠM VI ĐƯỢC SỬA

Được sửa:

```text
tests/PEMS.UnitTests/**
tests/PEMS.ApplicationTests/** để loại placeholder skipped nếu cần
tests/PEMS.IntegrationTests/Departments/ViewDepartmentDetails/**
tests/PEMS.IntegrationTests/TestInfrastructure/DatabaseResetHelper.cs
docs/prompt_test/** nếu lưu prompt
```

Không tự sửa:

```text
ViewDepartmentDetailsQueryHandler.cs
ViewDepartmentDetailsDto.cs
DepartmentsController.cs
StaffLeaderDepartmentScope.cs
ExceptionHandlingMiddleware.cs
SQL production
frontend production
appsettings.Development.json
appsettings.Production.json
```

Nếu test phát hiện production bug:

```text
- Giữ test đúng nghiệp vụ/source-of-truth.
- Báo fail.
- Không nới assertion để xanh.
- Không patch production nếu user chưa yêu cầu.
```

---

# 10. AUTH CLIENT VÀ SESSION

## 10.1. Valid Staff Leader client

Flow:

```text
1. EnsureTestUserAsync(EffectiveRole.StaffLeader).
2. Load user.
3. Validate PrimaryCampusId và DepartmentId.
4. CreateActiveSessionAsync.
5. Set TestAuth headers:
   - UserId
   - RoleCode = STAFF
   - SubRole = LEADER
   - SessionId
   - PrimaryCampusId
   - DepartmentId
```

Return:

```text
HttpClient
UserId
CampusId
DepartmentId
```

## 10.2. Staff Leader without campus claim

Không sửa DB user thành campus null vì trigger/data invariant có thể bị vi phạm.

Dùng valid underlying user/session nhưng request headers:

```text
- Có UserId.
- Có STAFF.
- Có LEADER.
- Có SessionId.
- Cố ý bỏ PrimaryCampusIdHeader.
- Có thể bỏ DepartmentIdHeader.
```

Expected:

```text
422 DEPARTMENT_NO_CAMPUS_ASSIGNED
```

## 10.3. Wrong-role clients

Test đủ:

```text
HO
ADMIN
STAFF + STAFF
DEPARTMENT + LEADER
DEPARTMENT + STAFF
STUDENT
VISITOR
```

Mỗi client:

```text
- Có deterministic user.
- Có active session.
- Có role/subrole claim đúng role.
- Không vô tình fail do session trước khi tới handler.
```

## 10.4. Anonymous

```text
Factory.CreateClient()
Không auth headers
```

Expected hiện tại:

```text
403 DEPARTMENT_MANAGEMENT_FORBIDDEN
```

---

# 11. DATABASE SAFETY

## 11.1. Chỉ `pems_test`

Connection string phải trỏ:

```text
database=pems_test
```

Agent phải parse database name an toàn để verify.

Không in:

```text
password
full connection string
secret
token
```

## 11.2. Dedicated prefix

```text
[IT-UC105-VIEW-DEPARTMENT-DETAILS] 
```

Tên test row:

```text
[IT-UC105-VIEW-DEPARTMENT-DETAILS] general-active {token}
[IT-UC105-VIEW-DEPARTMENT-DETAILS] general-inactive {token}
[IT-UC105-VIEW-DEPARTMENT-DETAILS] target {token}
[IT-UC105-VIEW-DEPARTMENT-DETAILS] distractor {token}
[IT-UC105-VIEW-DEPARTMENT-DETAILS] assigned-head {token}
[IT-UC105-VIEW-DEPARTMENT-DETAILS] readonly {token}
[IT-UC105-VIEW-DEPARTMENT-DETAILS] cross-campus {token}
```

## 11.3. Cleanup

Class implement:

```csharp
IAsyncLifetime
```

`DisposeAsync`:

```csharp
await DatabaseResetHelper.DeleteTestDepartmentsAsync(
    db,
    DatabaseResetHelper.ViewDepartmentDetailsNamePrefix);
```

Không:

```text
DELETE tất cả departments
DELETE theo contains token quá rộng
TRUNCATE
foreign_key_checks = 0
xóa seed IC
xóa user fixture
xóa campus fixture
```

## 11.4. IC department

Không tạo IC thứ hai trong campus Staff Leader.

Test IC:

```text
- Query existing IC department trong own campus.
- Không update.
- Không delete.
- Không prefix vào tên IC.
```

## 11.5. Head fixture

Ưu tiên reuse:

```text
EnsureTestUserAsync(EffectiveRole.DepartmentLead)
```

Nhưng phải xác nhận head user cùng campus với Staff Leader.

Nếu existing deterministic DepartmentLead fixture khác campus:

```text
- Không gán mù quáng.
- Có thể tạo test-only head candidate cùng campus bằng helper hiện có hoặc helper mới tối thiểu.
- Không sửa user seed thật.
```

Không delete user sau test theo convention hiện tại.

## 11.6. Test order independence

Mỗi test tự seed target của mình.

Không dựa vào:

```text
Test A chạy trước tạo row cho Test B.
Static shared DepartmentId.
Exact auto-increment id.
Existing row từ lần chạy trước.
```

---

# 12. NGUYÊN TẮC CHỌN TEST

UC-105 là detail endpoint, không cần test:

```text
pagination
sorting
keyword
status filter
list count
```

Cần tập trung:

```text
authorization
model binding
resource existence
campus scope
exact identity
DTO projection
GENERAL/IC flags
status visibility
nullable head
response minimization
read-only
```

Không duplicate UC-104 list tests.

---

# 13. UNIT TEST CẦN TẠO

## 13.1. `NewQuery_DefaultsDepartmentIdToZero`

```csharp
[Fact]
public void NewQuery_DefaultsDepartmentIdToZero()
{
    var query = new ViewDepartmentDetailsQuery();

    Assert.Equal(0UL, query.DepartmentId);
}
```

Không cần async.

Không test handler ở Unit Test nếu chỉ mock EF.

---

# 14. INTEGRATION TEST CẦN TẠO

Tạo class:

```csharp
public sealed class ViewDepartmentDetailsApiTests
    : IClassFixture<PemsWebApplicationFactory>, IAsyncLifetime
```

Constant:

```csharp
private const string Url = "/api/departments/viewdepartmentdetails";
```

Build URL:

```csharp
private static string BuildUrl(ulong departmentId)
    => $"{Url}?departmentId={departmentId}";
```

Đối với malformed:

```text
/api/departments/viewdepartmentdetails?departmentId=abc
```

## 14.1. Error response records

Có thể dùng:

```csharp
private sealed record ErrorResponse(
    bool Success,
    string? ErrorCode,
    string? Message,
    string? TraceId);
```

Lưu ý:

```text
- Wrong actor/cross-campus/no-campus có ErrorCode.
- NotFound không có ErrorCode.
- Malformed model binding có thể trả ValidationProblemDetails, không ép deserialize ErrorResponse nếu response thực tế khác.
```

---

## 14.2. Authorization matrix

### TC-01 `Anonymous_Forbidden`

Arrange:

```text
No auth headers.
Có thể không cần departmentId hoặc dùng id bất kỳ.
```

Act:

```http
GET /api/departments/viewdepartmentdetails?departmentId=1
```

Assert:

```text
403
success = false
errorCode = DEPARTMENT_MANAGEMENT_FORBIDDEN
traceId not blank
```

### TC-02 `Staff_Forbidden`

Actor:

```text
STAFF + STAFF
```

Assert:

```text
403
DEPARTMENT_MANAGEMENT_FORBIDDEN
```

### TC-03 `DepartmentLead_Forbidden`

Actor:

```text
DEPARTMENT + LEADER
```

Assert:

```text
403
DEPARTMENT_MANAGEMENT_FORBIDDEN
```

### TC-04 `Department_Forbidden`

Actor:

```text
DEPARTMENT + STAFF
```

Assert:

```text
403
DEPARTMENT_MANAGEMENT_FORBIDDEN
```

### TC-05 `Student_Forbidden`

Assert:

```text
403
DEPARTMENT_MANAGEMENT_FORBIDDEN
```

### TC-06 `Ho_Forbidden`

Quan trọng vì tài liệu legacy có thể từng cho HO read.

Assert:

```text
403
DEPARTMENT_MANAGEMENT_FORBIDDEN
```

### TC-07 `Admin_Forbidden`

Assert:

```text
403
DEPARTMENT_MANAGEMENT_FORBIDDEN
```

### TC-08 `Visitor_Forbidden`

Assert:

```text
403
DEPARTMENT_MANAGEMENT_FORBIDDEN
```

### TC-09 `StaffLeader_WithoutCampusClaim_UnprocessableEntity`

Arrange:

```text
Valid Staff Leader user/session.
Omit PrimaryCampusIdHeader.
```

Assert:

```text
422
success = false
errorCode = DEPARTMENT_NO_CAMPUS_ASSIGNED
traceId not blank
```

---

## 14.3. Input/model binding/not found

### TC-10 `MalformedDepartmentId_BadRequest`

Act:

```http
GET /api/departments/viewdepartmentdetails?departmentId=abc
```

Actor:

```text
Valid Staff Leader.
```

Assert:

```text
400 Bad Request
Response không phải 500
```

Không ép errorCode nếu automatic `[ApiController]` model-state response không có contract đó.

### TC-11 `StaffLeader_MissingDepartmentId_NotFound`

Act:

```http
GET /api/departments/viewdepartmentdetails
```

Vì property `ulong` mặc định 0:

```text
Handler lookup DepartmentId = 0.
```

Assert:

```text
404
success = false
errorCode null/absent
message chứa Department và 0
```

Test này chứng minh source contract hiện tại.

Nếu sau này thêm `[Required]`/validator và status đổi 400:

```text
Update test theo source mới, report change.
```

### TC-12 `StaffLeader_NonExistingDepartment_NotFoundWithoutErrorCode`

Arrange:

```text
Dùng id chắc chắn không tồn tại.
Ưu tiên query MAX(department_id) rồi chọn candidate an toàn.
Hoặc ulong.MaxValue nếu provider/binder hỗ trợ.
```

Act:

```text
Valid Staff Leader gọi id không tồn tại.
```

Assert:

```text
404
success = false
errorCode = null
message not blank
message chứa Department/id
traceId not blank
```

Không assert `DEPARTMENT_NOT_FOUND` vì handler hiện không trả code đó.

---

## 14.4. Campus scope

### TC-13 `StaffLeader_OtherCampusDepartment_ForbiddenWithScopeErrorCode`

Arrange:

```text
- Valid Staff Leader own campus.
- Chọn other ACTIVE campus.
- Seed GENERAL department ở other campus với UC-105 prefix.
```

Act:

```text
Call exact other-campus departmentId.
```

Assert:

```text
403
success = false
errorCode = DEPARTMENT_SCOPE_FORBIDDEN
message = "Bạn không có quyền xem phòng ban này." hoặc semantic tương đương theo source mới
```

Điểm chống pass giả:

```text
Department phải tồn tại thật.
Không dùng random nonexistent id vì sẽ test 404 chứ không test scope.
```

### TC-14 `StaffLeader_ExactId_ReturnsRequestedDepartment_NotSameCampusDistractor`

Arrange:

```text
- Seed target GENERAL in own campus.
- Seed distractor GENERAL in same campus.
- Tên khác nhau, cùng prefix + same token.
```

Act:

```text
GET target id.
```

Assert:

```text
200
response.DepartmentId = targetId
response.Name = targetName
response.DepartmentId != distractorId
response.Name != distractorName
```

Không chỉ `Assert.NotNull`.

---

## 14.5. Core projection

### TC-15 `StaffLeader_GeneralActiveDepartment_ReturnsFullDetailAndEditableFlags`

Arrange:

```text
- GENERAL
- ACTIVE
- own campus
- no head hoặc head controlled
- unique name
```

Load expected Campus trước request:

```text
CampusId
CampusCode
CampusName
```

Assert:

```text
200
DepartmentId exact
Name exact
CampusId exact
CampusCode exact
CampusName exact
Status = ACTIVE
DepartmentType = GENERAL
CanEditName = true
CanToggleStatus = true
```

Nếu no head:

```text
HeadUserId null
HeadFullName null
```

### TC-16 `StaffLeader_GeneralInactiveDepartment_IsStillViewable`

Arrange:

```text
Seed own-campus GENERAL status INACTIVE.
```

Assert:

```text
200
Status = INACTIVE
DepartmentType = GENERAL
CanEditName = true
CanToggleStatus = true
```

Test này bắt regression nếu handler sau này vô tình filter ACTIVE.

### TC-17 `StaffLeader_IcDepartment_IsViewableButNotEditableOrToggleable`

Arrange:

```text
Find existing own-campus IC department.
Không tạo IC mới.
```

Assert:

```text
200
DepartmentId exact
CampusId = own campus
DepartmentType = IC
CanEditName = false
CanToggleStatus = false
```

Không assert head null vì seed IC có thể được gán head trong live DB.

### TC-18 `StaffLeader_UnassignedHead_ReturnsNullHeadFields`

Arrange:

```text
Seed GENERAL with headUserId = null.
```

Assert:

```text
200
HeadUserId = null
HeadFullName = null
```

Không chấp nhận:

```text
HeadFullName = ""
HeadFullName = "Chưa gán trưởng phòng"
```

Backend DTO phải trả null; text fallback là trách nhiệm frontend.

### TC-19 `StaffLeader_AssignedHead_ReturnsHeadIdentity`

Arrange:

```text
- Ensure/reuse head user cùng campus.
- Record expected userId + FullName.
- Seed GENERAL with headUserId.
```

Assert:

```text
200
HeadUserId = expected
HeadFullName = expected exact
```

Thêm sanity:

```text
HeadFullName không bằng department name.
```

---

## 14.6. Response minimization/security

### TC-20 `StaffLeader_Response_ContainsOnlyExpectedPublicFields`

Không deserialize trực tiếp vào DTO trước khi kiểm tra property set.

Dùng:

```csharp
var json = await response.Content.ReadAsStringAsync();
using var document = JsonDocument.Parse(json);
var actualNames = document.RootElement
    .EnumerateObject()
    .Select(p => p.Name)
    .OrderBy(x => x)
    .ToArray();
```

Expected exact set:

```text
canEditName
canToggleStatus
campusCode
campusId
campusName
departmentId
departmentType
headFullName
headUserId
name
status
```

Assert không có:

```text
email
phone
passwordHash
roleCode
subRole
users
createdAt
createdBy
updatedAt
updatedBy
```

Lưu ý:

```text
Exact property set là intentional contract test.
Nếu production thêm field hợp lệ sau này, test sẽ fail và team phải review contract thay vì tự bỏ assertion.
```

---

## 14.7. Read-only

### TC-21 `StaffLeader_ViewDetails_DoesNotModifyDepartment`

Arrange:

```text
Seed controlled GENERAL department.
Load before với AsNoTracking.
Record:
- Name
- CampusId
- DepartmentType
- HeadUserId
- Status
- CreatedAt
- CreatedBy
- UpdatedAt
- UpdatedBy
Record count của rows đúng UC-105 prefix.
```

Act:

```text
GET detail.
Assert 200.
```

Reload bằng DI scope/DbContext mới.

Assert:

```text
All fields before == after.
Prefix row count unchanged.
Target vẫn tồn tại.
Không có additional UC-105 department.
```

Không reuse tracked entity để assert.

---

# 15. TEST NAMES ĐỀ XUẤT

Unit:

```text
NewQuery_DefaultsDepartmentIdToZero
```

Integration:

```text
Anonymous_Forbidden
Staff_Forbidden
DepartmentLead_Forbidden
Department_Forbidden
Student_Forbidden
Ho_Forbidden
Admin_Forbidden
Visitor_Forbidden
StaffLeader_WithoutCampusClaim_UnprocessableEntity

MalformedDepartmentId_BadRequest
StaffLeader_MissingDepartmentId_NotFound
StaffLeader_NonExistingDepartment_NotFoundWithoutErrorCode

StaffLeader_OtherCampusDepartment_ForbiddenWithScopeErrorCode
StaffLeader_ExactId_ReturnsRequestedDepartment_NotSameCampusDistractor

StaffLeader_GeneralActiveDepartment_ReturnsFullDetailAndEditableFlags
StaffLeader_GeneralInactiveDepartment_IsStillViewable
StaffLeader_IcDepartment_IsViewableButNotEditableOrToggleable
StaffLeader_UnassignedHead_ReturnsNullHeadFields
StaffLeader_AssignedHead_ReturnsHeadIdentity
StaffLeader_Response_ContainsOnlyExpectedPublicFields
StaffLeader_ViewDetails_DoesNotModifyDepartment
```

Không bắt buộc giữ tên y hệt nếu naming convention source mới khác, nhưng tên phải mô tả đúng assertion.

---

# 16. THIẾT KẾ DỮ LIỆU CHỐNG PASS GIẢ

## 16.1. Unique token

```csharp
private static string UniqueToken() => Guid.NewGuid().ToString("N");
```

## 16.2. Target + distractor

Ví dụ:

```text
target:
[IT-UC105-VIEW-DEPARTMENT-DETAILS] target {token}

distractor:
[IT-UC105-VIEW-DEPARTMENT-DETAILS] distractor {token}
```

Cùng campus để chứng minh filter exact id.

## 16.3. Cross-campus

```text
other campus row phải được seed thật.
Không dùng nonexistent id.
```

## 16.4. Expected values độc lập

Expected campus:

```text
Query Campus row trực tiếp trước request.
```

Expected head:

```text
Query User row trực tiếp trước request.
```

Không lấy expected từ response rồi assert response bằng chính nó.

## 16.5. Không hardcode auto-increment

Không dùng:

```text
departmentId = 1
campusId = 1
userId = 5
```

Trừ khi chỉ để gọi wrong-role guard và resource identity không phải mục tiêu test.

Core positive/scope test phải resolve IDs động.

## 16.6. Không exact-count toàn DB

Detail endpoint không cần count toàn database.

Chỉ count:

```text
rows mang UC-105 prefix
```

khi test read-only/cleanup.

---

# 17. COMMANDS ĐƯỢC PHÉP CHẠY

## 17.1. Build/unit không động live DB

```bash
dotnet build tests/PEMS.UnitTests/PEMS.UnitTests.csproj

dotnet test tests/PEMS.UnitTests/PEMS.UnitTests.csproj \
  --filter "FullyQualifiedName~ViewDepartmentDetails"
```

Có thể build IntegrationTests project mà chưa chạy test:

```bash
dotnet build tests/PEMS.IntegrationTests/PEMS.IntegrationTests.csproj
```

## 17.2. Read-only pems_test preflight

Dùng MySQL client/IDE với credential test hiện có.

Không in credential.

Chạy các SELECT ở mục 6.

## 17.3. Integration Test sau safety gate + xác nhận

Targeted:

```bash
dotnet test tests/PEMS.IntegrationTests/PEMS.IntegrationTests.csproj \
  --filter "FullyQualifiedName~ViewDepartmentDetailsApiTests"
```

Nếu targeted pass, chạy nhóm Departments:

```bash
dotnet test tests/PEMS.IntegrationTests/PEMS.IntegrationTests.csproj \
  --filter "FullyQualifiedName~Departments"
```

Sau đó full Integration suite nếu được phép:

```bash
dotnet test tests/PEMS.IntegrationTests/PEMS.IntegrationTests.csproj
```

Full Unit suite:

```bash
dotnet test tests/PEMS.UnitTests/PEMS.UnitTests.csproj
```

Nếu solution có ArchitectureTests:

```bash
dotnet test tests/PEMS.ArchitectureTests/PEMS.ArchitectureTests.csproj
```

Không dùng:

```text
docker compose
testcontainers
appsettings.Development.json
pems_db
```

---

# 18. XỬ LÝ TEST FAIL

## 18.1. Không nới assertion ngay

Phân loại:

```text
- Source changed.
- Test setup invalid.
- pems_test prerequisite thiếu.
- Stale deterministic user invalid.
- Session middleware rejected.
- Cleanup/FK issue.
- Model binding contract khác.
- Production bug.
```

## 18.2. Các fail có thể là production bug thật

Ví dụ:

```text
- Other-campus trả 200.
- Wrong role trả 200.
- IC CanEditName true.
- IC CanToggleStatus true.
- INACTIVE cùng campus trả 404.
- Head email/phone bị lộ.
- GET làm UpdatedAt thay đổi.
- Exact target id nhưng response trả row khác.
- NotFound thành 500.
```

Không sửa test để chấp nhận.

## 18.3. Fail có thể là test expectation stale

Ví dụ:

```text
- Controller mới có [Authorize], anonymous đổi 403 -> 401.
- Handler mới dùng controlled DepartmentNotFound error code.
- Endpoint đổi sang route.
- DTO contract được version hóa thêm field.
```

Khi đó:

```text
- Đọc source mới.
- Xác định thay đổi có chủ đích.
- Update prompt/test.
- Report.
```

---

# 19. REPORT BẮT BUỘC SAU TASK

Dùng format:

## 1. Tóm tắt

```text
UC-105 tests đã tạo/cập nhật.
Target branch/commit.
Unit count.
Integration count.
Pass/fail.
```

## 2. File đã tạo/sửa/xóa

```text
Created:
- ...

Modified:
- ...

Deleted:
- placeholder skipped ...
```

## 3. Source mapping

```text
Endpoint:
Binding:
Actor:
Scope:
NotFound:
Cross-campus:
GENERAL flags:
IC flags:
Read-only:
```

## 4. Tài liệu/source mismatch

Bắt buộc ghi:

```text
- UC doc đề xuất route /api/departments/{id}, source dùng query endpoint.
- Controller hiện thiếu [Authorize], handler trả 403 cho anonymous.
- DepartmentNotFound constant tồn tại nhưng UC-105 404 hiện không có errorCode.
- Attached UC-104 prompt có thể mô tả test state cũ; source hiện tại đã có UC-104 tests hoàn chỉnh.
```

## 5. pems_test preflight

Bảng readiness.

Không in connection string/password.

## 6. Unit Tests

```text
Test name
Purpose
Result
```

## 7. Integration Tests

```text
Test name
Purpose
Result
```

## 8. DB safety

```text
Database verified = pems_test?
Rows created by prefix?
Rows remaining?
Seed IC modified? No.
Production/dev DB touched? No.
```

## 9. Commands

```text
Command
Exit code
Passed/Failed/Skipped
Duration nếu có
```

## 10. Failures

```text
Expected
Actual
Likely cause
Production bug hay test issue
```

## 11. Remaining risks

```text
- Live DB not checked?
- Full suite not run?
- Existing stale test users?
- Frontend component behavior not covered?
```

---

# 20. DEFINITION OF DONE

Task chỉ hoàn thành khi:

```text
[ ] Đã đọc source hiện tại.
[ ] Đã xác nhận endpoint thật.
[ ] Đã xác nhận no validator.
[ ] Đã xác nhận auth nằm ở handler hoặc cập nhật theo source mới.
[ ] Đã xử lý skipped placeholder.
[ ] Đã tạo Unit Test có ý nghĩa tối thiểu.
[ ] Đã tạo Integration Test class riêng UC-105.
[ ] Đã thêm prefix UC-105 riêng.
[ ] Đã có full wrong-role matrix.
[ ] Đã test no-campus.
[ ] Đã test malformed/missing/nonexistent id.
[ ] Đã test cross-campus bằng existing row thật.
[ ] Đã test exact target với distractor.
[ ] Đã test GENERAL ACTIVE.
[ ] Đã test GENERAL INACTIVE.
[ ] Đã test IC flags.
[ ] Đã test head null.
[ ] Đã test assigned head.
[ ] Đã test response field minimization.
[ ] Đã test read-only bằng reload DB.
[ ] Đã verify live database = pems_test trước Integration Test.
[ ] Đã report DB prerequisites.
[ ] Đã cleanup đúng prefix.
[ ] Không xóa/mutate seed IC.
[ ] Không dùng Docker/Testcontainers.
[ ] Không đọc/in secret.
[ ] Không sửa production để ép pass.
[ ] Targeted tests đã chạy hoặc ghi rõ chưa được phép.
[ ] Full suite đã chạy hoặc ghi rõ lý do chưa chạy.
[ ] Không còn test UC-105 bị Skip/TODO.
```

---

# 21. CÁC LỖI NGHIÊM CẤM

```text
1. Gọi sai endpoint /api/departments/{id}.
2. Tự thêm ViewDepartmentDetailsQueryValidator.
3. Kỳ vọng anonymous 401 mà không đọc source.
4. Cho HO read vì tài liệu legacy.
5. Dùng campusId từ client để scope.
6. Cross-campus test dùng id không tồn tại.
7. NotFound test kỳ vọng DEPARTMENT_NOT_FOUND dù handler không trả.
8. Chỉ assert HTTP status, không assert errorCode ở controlled errors.
9. IC test tạo IC thứ hai trong campus.
10. Mutate real IC row.
11. Dùng hardcoded department_id của seed làm target read-only test rồi update/delete nó.
12. Cleanup bằng DELETE toàn departments.
13. Disable foreign_key_checks.
14. Dùng pems_db.
15. Dùng appsettings.Development.json.
16. In connection string/password trong log/report.
17. Dùng EF InMemory thay MySQL.
18. Mock handler rồi tuyên bố đã test EF/scope.
19. Chỉ test positive.
20. Để [Fact(Skip)] hoặc TODO.
21. Assert.True(true).
22. Bắt exception rồi nuốt để test pass.
23. Đổi expected theo actual mà không điều tra.
24. Chạy Integration Test trước DB safety gate.
25. Báo live pems_test đủ chỉ vì fresh SQL có seed.
```

---

# 22. KẾT LUẬN GIAO CHO AI AGENT

Hãy thực hiện theo thứ tự:

```text
1. Đọc source và docs.
2. So sánh baseline prompt với source mới.
3. Chạy build/read-only checks.
4. Kiểm tra trực tiếp pems_test bằng SELECT.
5. Báo DB readiness.
6. Chờ xác nhận trước Integration Test nếu môi trường yêu cầu.
7. Tạo test code thật.
8. Chạy targeted Unit.
9. Chạy targeted Integration.
10. Chạy Departments/full suite.
11. Cleanup đúng prefix.
12. Report trung thực.
```

Ưu tiên cao nhất:

```text
Correctness > số lượng test.
Security scope > happy path.
Database safety > tốc độ.
Source hiện tại > tài liệu legacy.
Test fail đúng > test xanh giả.
```
Nếu không chắc source, permission, database hoặc cleanup, phải dừng và báo rõ trước khi tiếp tục.
