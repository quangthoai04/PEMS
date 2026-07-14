# PROMPT AI — TẠO TEST CODE THẬT CHO UC-104 VIEW DEPARTMENT LIST (UNIT TEST + INTEGRATION TEST, KHÔNG DOCKER, AN TOÀN DATABASE) — v1

> File này dùng để đưa cho AI Agent/Code Agent khi cần tạo hoặc cập nhật test tự động cho chức năng **View Department List** trong dự án PEMS.
>
> Use case thuộc nhóm **Department Structure Management / Department Management**. Theo `USE_CASE_LIST.md`, `PERMISSION_MATRIX.md` mới và source hiện tại, UC ổn định là **UC-104 View Department List**, actor nghiệp vụ là **Staff Leader**.
>
> Mục tiêu: **tạo test code thật, chạy được, đúng source hiện tại, không viết test giả để lấy kết quả xanh, kiểm tra trực tiếp `pems_test` bằng các truy vấn read-only trước khi chạy Integration Test, và tuyệt đối không làm hỏng `pems_db` hoặc database dev/thật**.

---

## 0. Baseline source hiện tại đã xác nhận trước khi viết prompt

AI Agent phải đọc lại source tại thời điểm thực hiện vì code có thể thay đổi. Baseline hiện đã xác nhận như sau:

```text
UC mới nhất:
- UC-104 View Department List.
- PERMISSION_MATRIX mới: chỉ Staff Leader có quyền R.
- PROJECT_OVERVIEW legacy có mapping cũ UC-101 và từng ghi Staff Leader + HO.
- Không dùng mapping legacy để tự cho HO quyền nếu source/policy mới không cho phép.

Endpoint thật:
- GET /api/departments/viewdepartmentlist
- Controller action: DepartmentsController.ViewDepartmentList
- Query bind từ query string bằng [FromQuery].

Query thật:
- ViewDepartmentListQuery
- Page mặc định 1
- PageSize mặc định 20
- Keyword nullable
- Status nullable
- SortBy mặc định "name"
- SortDirection mặc định "asc"

Handler thật:
- ViewDepartmentListQueryHandler là thin handler.
- Handler gọi trực tiếp DepartmentListQueryExecutor.ExecuteAsync.
- UC-104 và UC-103 dùng chung đúng một executor/read model.

Authorization/scope thật:
- StaffLeaderDepartmentScope.EnsureStaffLeaderCampus.
- Actor hợp lệ: IsAuthenticated + role_code STAFF + sub_role LEADER.
- Campus lấy từ currentUser.PrimaryCampusId ở server, không nhận campusId từ client.
- Anonymous và role sai hiện bị 403 DepartmentManagementForbidden vì controller chưa có [Authorize].
- Staff Leader thiếu PrimaryCampusId bị 422 NoCampusAssigned.

Validation/input handling thật:
- Hiện không có ViewDepartmentListQueryValidator.
- Không tự tạo FluentValidation test giả cho validator không tồn tại.
- page < 1 -> 1.
- pageSize < 1 -> 20.
- pageSize > 100 -> 100.
- sortBy whitelist: name, status, headname, createdat.
- sortBy null/blank/không hợp lệ -> name.
- sortDirection chỉ "desc" mới descending; giá trị khác -> ascending.
(Bên backend code phần này không có sort thì không phải test)
- status chỉ ACTIVE/INACTIVE mới được áp dụng; giá trị khác bị bỏ qua, không trả 400.
- keyword được trim, chuyển lower-case và tìm trong Department.Name hoặc HeadUser.FullName.

Read model thật:
- Chỉ lấy departments thuộc campus của Staff Leader.
- AsNoTracking, không ghi DB.
- Sắp xếp có ThenBy(DepartmentId) để ổn định.
- Head user là LEFT JOIN/navigation nullable.
- CanToggleStatus = true chỉ khi DepartmentType == GENERAL.
- IC department có CanToggleStatus = false.

Response thật:
PaginatedResult<DepartmentListItemDto>
- Items
- Page
- PageSize
- TotalItems
- TotalPages
- HasNextPage
- HasPreviousPage

DepartmentListItemDto:
- DepartmentId
- CampusId
- CampusName
- Name
- HeadUserId
- HeadFullName
- Status
- DepartmentType
- CanToggleStatus
- CreatedAt
- UpdatedAt

Test hiện có cần xử lý:
- tests/PEMS.ApplicationTests/Departments/ViewDepartmentListQueryTests.cs
- Hiện chỉ có [Fact(Skip = "Pending UC specification")] và TODO.
- Đây không phải test thật, không được tính là coverage/pass.
- Chưa thấy ViewDepartmentListApiTests Integration Test riêng.

Test infrastructure hiện tại:
- PemsWebApplicationFactory dùng environment Testing và appsettings.Testing.json.
- Dùng MySQL thật pems_test, không Docker.
- TestAuthHandler dùng headers để tạo claims.
- DatabaseResetHelper có prefix UC-101, UC-102, UC-103 nhưng chưa có UC-104.
- AssemblyInfo.cs đã DisableTestParallelization = true.
```

Nếu source khi thực hiện khác baseline trên, phải dùng source mới và ghi rõ phần khác biệt trong báo cáo.

---

## 0.1. Bối cảnh và kinh nghiệm phải kế thừa

Kế thừa chuẩn từ Create/Update/View/Search FAQ và UC-101/102/103 Department:

```text
- Dùng xUnit.
- Chỉ dùng FluentValidation.TestHelper khi source thật có validator.
- Dùng WebApplicationFactory cho Integration Test.
- Dùng TestAuthHandler để giả lập role/session.
- Dùng database test riêng pems_test.
- Không dùng Docker/Testcontainers.
- Không dùng appsettings.Development.json.
- Không đọc/copy/in secret thật.
- Không tự sửa production code để ép test pass.
- Tên test phải đúng với assertion thật.
- Integration Test dùng chung pems_test phải tắt parallelization.
- Mỗi test class/use case có prefix riêng.
- Cleanup không xóa dữ liệu test class khác.
- Test ReadOnly/DoesNotModify phải reload DB và assert đủ state.
- Không assert exact count của toàn database khi không cô lập dữ liệu.
- Mọi test search/list dùng token phải có positive row và distractor/negative row do chính test kiểm soát.
- Khi test fail trong full suite, kiểm tra race condition/cleanup/test fixture trước production code.
```

Bài học chống pass giả:

```text
- Không chỉ Assert.Contains(target) nếu backend có thể trả toàn bộ campus.
- Phải thêm same-campus distractor không thỏa điều kiện hoặc assert exact isolated result.
- Không chỉ assert HTTP 403; phải assert errorCode đúng nếu response contract có errorCode.
- Không chỉ assert một item xuất hiện; phải assert item không hợp lệ không xuất hiện.
- Pagination phải assert metadata và lát dữ liệu, không chỉ Items.Count.
- Read-only phải snapshot trước/sau, không chỉ tin comment AsNoTracking.
- Không dùng [Fact(Skip=...)] hoặc Assert.True(true) để báo coverage.
```

Prefix riêng đề xuất:

```text
[IT-UC104-VIEW-DEPARTMENT-LIST] 
```

Bắt buộc thêm hằng số riêng, ví dụ:

```csharp
public const string ViewDepartmentListNamePrefix = "[IT-UC104-VIEW-DEPARTMENT-LIST] ";
```

Không dùng chung hoặc overlap với:

```text
[IT-UC101-ADD-DEPARTMENT]
[IT-UC102-UPDATE-DEPARTMENT]
[IT-UC103-SEARCH-FILTER-DEPARTMENT]
```

---

## 0.2. Lưu ý UC ID và tài liệu legacy

Nguồn mới:

```text
USE_CASE_LIST.md:
- UC-101 Add New Department
- UC-102 Update Department
- UC-103 Search and Filter Departments
- UC-104 View Department List
- UC-105 View Department Details

PERMISSION_MATRIX.md:
- UC-104 View Department List -> Staff Leader: R
- HO/Admin/Staff/Department Lead/Department/Student/Visitor: không có quyền
```

Nguồn legacy có thể ghi:

```text
PROJECT_OVERVIEW cũ:
- UC-101 View Department List
- Actor STAFF_L, HO
```

Quy tắc:

```text
- Ưu tiên UC name ổn định và source hiện tại.
- Báo mismatch trong report.
- Không biến HO thành actor hợp lệ chỉ để phù hợp docs cũ.
- Không hardcode UC ID vào logic nghiệp vụ.
- Folder ổn định: Departments/ViewDepartmentList.
```

Không trộn UC-104 với:

```text
UC-103 Search and Filter Departments
UC-105 View Department Details
UC-106 Manage Department Status
UC-107 Add Department Personnel
```

---

## 1. Mục tiêu task

Tạo/cập nhật test tự động cho **Staff Leader xem danh sách Department trong campus của mình**.

Phạm vi gồm:

```text
1. Unit Test/Application Test có ý nghĩa thật
   - Chỉ test contract/pure logic hiện có.
   - Không tạo validator giả.
   - Không gọi API hoặc pems_test.

2. Integration Test
   - HTTP request thật qua TestServer.
   - Test authentication claims + session.
   - Controller thật.
   - MediatR handler thật.
   - DepartmentListQueryExecutor thật.
   - EF Core Pomelo MySQL thật.
   - Database test pems_test.
```

Sau task, report phải trả lời được:

```text
- Endpoint và HTTP method thật.
- Query params và giá trị mặc định.
- Actor và campus scope thật.
- UC-104 khác UC-103 ở mục tiêu test thế nào.
- Pagination metadata hoạt động ra sao.
- Default sorting hoạt động ra sao.
- DTO trả đúng field gì.
- GENERAL/IC khác nhau ở CanToggleStatus thế nào.
- Head nullable được project thế nào.
- API có read-only thật hay không.
- pems_test hiện tại có đủ prerequisite hay không.
- Test nào pass/fail và lệnh đã chạy.
```

---

## 2. Nguồn bắt buộc đọc trước khi sửa

### 2.1. Tài liệu cần đối chiếu

```text
USE_CASE_LIST.md
PERMISSION_MATRIX.md
PERMISSION_RULES.md
PROJECT_OVERVIEW_v8_4_refined_v6_v10_FULL_UPDATED.md
PEMS_CANONICAL_BUSINESS_RULES_v8_4_refined_v6_v10_FULL_UPDATED.md
PEMS_UC_IMPLEMENTATION_RULEBOOK_FRONTEND_BACKEND_DATABASE_VALIDATION_SECURITY_v8_4_refined_v6_v10_FULL_UPDATED.md
PROJECT_STRUCTURE_FULL.md
CLEAN_ARCHITECTURE.md
PEMS_v10_NEW_FINAL_SQL_TABLE_FIELD_DICTIONARY_MATCHED.docx
Report 3.1_UCS_Template.docx nếu cần
Report 5.2_L1-UnitTests_Template.xlsx nếu cần
Report 5.2_L2-IntegrationTests_Template.xlsx nếu cần
SQL fresh-create mới nhất trong docs/database/scripts/
```

### 2.2. Source code bắt buộc đọc

```text
backend/PEMS.Api/Controllers/DepartmentsController.cs
backend/PEMS.Application/Departments/Queries/ViewDepartmentList/ViewDepartmentListQuery.cs
backend/PEMS.Application/Departments/Queries/ViewDepartmentList/ViewDepartmentListQueryHandler.cs
backend/PEMS.Application/Departments/Common/DepartmentListQueryExecutor.cs
backend/PEMS.Application/Departments/Common/IDepartmentListCriteria.cs
backend/PEMS.Application/Departments/Common/DepartmentListItemDto.cs
backend/PEMS.Application/Departments/Common/StaffLeaderDepartmentScope.cs
backend/PEMS.Application/Common/Models/PaginatedResult.cs
Department entity + EF configuration
ApplicationDbContext
CurrentUserService/ICurrentUserService
DepartmentErrorCodes
ExceptionHandlingMiddleware
SessionValidationMiddleware
frontend/pems-react/src/shared/api/endpoints.ts
frontend department management types/service nếu cần xác nhận contract
```

### 2.3. Test source bắt buộc đọc

```text
tests/PEMS.ApplicationTests/Departments/ViewDepartmentListQueryTests.cs
tests/PEMS.IntegrationTests/Departments/SearchFilterDepartments/SearchFilterDepartmentsApiTests.cs
tests/PEMS.IntegrationTests/Departments/AddNewDepartment/AddNewDepartmentApiTests.cs
tests/PEMS.IntegrationTests/Departments/UpdateDepartment/UpdateDepartmentApiTests.cs
tests/PEMS.IntegrationTests/TestInfrastructure/PemsWebApplicationFactory.cs
tests/PEMS.IntegrationTests/TestInfrastructure/TestAuthHandler.cs
tests/PEMS.IntegrationTests/TestInfrastructure/DatabaseResetHelper.cs
tests/PEMS.IntegrationTests/AssemblyInfo.cs
backend/PEMS.Api/appsettings.Testing.example.json
```

### 2.4. Source-first rule

Không được tự bịa:

```text
- route
- HTTP method
- actor
- anonymous status 401/403
- query params
- default paging
- sort whitelist
- invalid status behavior
- DTO fields
- campus scope
- head mapping
- CanToggleStatus rule
- pagination envelope
```

Nếu docs và source khác nhau:

```text
1. Báo rõ mismatch.
2. Authorization ưu tiên policy mới để phát hiện security issue.
3. Contract kỹ thuật dùng source hiện tại để viết test chạy thật.
4. Không sửa expected chỉ để test xanh.
5. Không sửa production code nếu chưa được duyệt.
```

---

## 3. Nghiệp vụ UC-104 phải giữ đúng

### 3.1. Actor hợp lệ

```text
Staff Leader = role_code STAFF + sub_role LEADER
```

Các actor khác phải bị chặn theo source hiện tại:

```text
Anonymous -> 403 DepartmentManagementForbidden
Staff -> 403 DepartmentManagementForbidden
HO -> 403 DepartmentManagementForbidden
Admin -> 403 DepartmentManagementForbidden
Department Leader -> 403 DepartmentManagementForbidden
Department Staff -> 403 DepartmentManagementForbidden
Student -> 403 DepartmentManagementForbidden
Visitor -> 403 DepartmentManagementForbidden
```

Không đổi Anonymous thành 401 theo generic template nếu source hiện tại vẫn handler-only và thật sự trả 403. Report rõ đây là behavior hiện tại; nếu sau này controller thêm `[Authorize]`, contract có thể đổi.

### 3.2. Staff Leader thiếu campus

Source hiện tại có nhánh:

```text
STAFF + LEADER hợp lệ nhưng PrimaryCampusId null
-> 422
-> errorCode = NoCampusAssigned
```

Chỉ viết test này nếu TestAuthHandler + SessionValidationMiddleware cho phép tạo request hợp lệ thiếu claim campus mà không bị chặn ở layer khác. Test phải assert cả status và errorCode.

### 3.3. Campus scope

```text
- Không có campusId query param.
- Campus luôn lấy từ current user claim/service.
- Staff Leader chỉ thấy departments cùng PrimaryCampusId.
- Department campus khác không được leak vào response.
```

Campus scope là security test bắt buộc.

### 3.4. View list khác Search and Filter

UC-104 và UC-103 dùng chung executor, nhưng mục tiêu test khác:

```text
UC-103 tập trung:
- keyword search
- head-name search
- filter status
- keyword + status AND logic
- invalid filter behavior

UC-104 tập trung:
- default list contract
- first page/default page size
- list ordering
- pagination slices + metadata
- own-campus data visibility
- DTO/result shape
- IC/GENERAL action flags
- assigned/unassigned head projection
- read-only behavior
```

Không copy nguyên toàn bộ `SearchFilterDepartmentsApiTests` rồi đổi endpoint/class name.

Cho phép dùng `keyword=<unique token>` trong một số UC-104 test **chỉ để cô lập dữ liệu**. Khi dùng như vậy, comment phải nói rõ mục tiêu test là paging/sort/result shape, không phải lặp lại UC-103.

### 3.5. Default criteria

Khi không gửi paging/sort fields:

```text
Page = 1
PageSize = 20
SortBy = name
SortDirection = asc
```

Test default criteria nên seed ít nhất ba tên cố ý không theo thứ tự insert:

```text
c-third
b-second
a-first
```

Gọi endpoint với `keyword=token` để cô lập nhưng bỏ page/pageSize/sortBy/sortDirection. Assert:

```text
- Page = 1
- PageSize = 20
- TotalItems = 3
- TotalPages = 1
- HasNextPage = false
- HasPreviousPage = false
- Items đúng thứ tự a, b, c
```

### 3.6. Pagination

Pagination test phải dùng dataset tự kiểm soát, ví dụ 5 rows chung token, pageSize 2.

Phải assert ít nhất:

```text
Page 1:
- 2 items đầu
- Page=1, PageSize=2, TotalItems=5, TotalPages=3
- HasNextPage=true, HasPreviousPage=false

Page 2:
- 2 items tiếp theo
- không overlap page 1
- HasNextPage=true, HasPreviousPage=true

Page 3:
- 1 item cuối
- HasNextPage=false, HasPreviousPage=true
```

Không chỉ assert `Items.Count`.

### 3.7. Status/type trong list

Khi không gửi status filter, list phải có thể trả cả:

```text
ACTIVE
INACTIVE
```

Danh sách cũng phải project đúng:

```text
GENERAL -> CanToggleStatus = true
IC -> CanToggleStatus = false
```

Không hiển thị `DepartmentType` như filter UI nếu product contract nói chỉ dùng để quyết định action, nhưng backend DTO vẫn phải map đúng.

### 3.8. Head nullable

```text
HeadUserId null -> HeadFullName null
HeadUserId có giá trị -> HeadFullName đúng FullName
```

Không dùng helper tạo user rác không cleanup nếu không cần.

Baseline hiện tại cho thấy `CreateHeadUserCandidateAsync` tạo user mới mỗi lần và không cleanup. Với UC-104:

```text
- Ưu tiên reuse deterministic test user từ EnsureTestUserAsync(EffectiveRole.DepartmentLead) nếu cùng campus và phù hợp schema.
- Hoặc bổ sung helper idempotent riêng cho head projection.
- Không gọi CreateHeadUserCandidateAsync lặp lại rồi để users tích tụ mà không báo cáo.
```

### 3.9. Read-only

UC-104 là query read-only.

Test tên `DoesNotModifyDepartments` hoặc `ReadOnly` phải snapshot đủ:

```text
DepartmentId
CampusId
Name
DepartmentType
HeadUserId
Status
CreatedAt
CreatedBy
UpdatedAt
UpdatedBy
```

Có thể kiểm tra thêm số lượng department test prefix trước/sau.

Không cần khẳng định mọi bảng trong hệ thống không ghi nếu SessionValidationMiddleware có cập nhật session. Tên test phải giới hạn rõ: `DoesNotModifyDepartments`.

---

## 4. KIỂM TRA TRỰC TIẾP `pems_test` TRƯỚC INTEGRATION TEST

Phần này bắt buộc. Không được chỉ đọc SQL seed rồi kết luận live database đủ dữ liệu.

### 4.1. Chỉ SELECT trước khi người dùng cho phép chạy test ghi DB

Được phép chạy read-only:

```sql
SELECT ...
SHOW ...
DESCRIBE ...
```

Không được tự chạy trước xác nhận:

```text
INSERT
UPDATE
DELETE
TRUNCATE
ALTER
DROP
CREATE DATABASE
mysql < script.sql
dotnet test IntegrationTests nếu test sẽ seed/cleanup DB
```

### 4.2. Xác nhận đúng database

```sql
SELECT DATABASE() AS current_database;
```

Expected:

```text
pems_test
```

Nếu khác `pems_test`, dừng ngay. Không chạy Integration Test.

### 4.3. Kiểm tra role nền

```sql
SELECT role_id, role_code, status
FROM roles
WHERE role_code IN ('STAFF', 'HO', 'ADMIN', 'DEPARTMENT', 'STUDENT', 'VISITOR')
ORDER BY role_code;
```

Cần đủ role cho authorization matrix. Nếu thiếu role, `EnsureTestUserAsync` sẽ fail.

### 4.4. Kiểm tra active campus

```sql
SELECT campus_id, campus_code, name, status
FROM campuses
WHERE status = 'ACTIVE'
ORDER BY campus_id;
```

Yêu cầu:

```text
- Ít nhất 1 ACTIVE campus cho Staff Leader và list core.
- Ít nhất 2 ACTIVE campus cho test cross-campus scope.
```

Kiểm tra count:

```sql
SELECT COUNT(*) AS active_campus_count
FROM campuses
WHERE status = 'ACTIVE';
```

### 4.5. Tìm campus đủ IC + GENERAL prerequisite

```sql
SELECT
    c.campus_id,
    c.campus_code,
    c.name,
    SUM(CASE WHEN d.department_type = 'IC' AND d.status = 'ACTIVE' THEN 1 ELSE 0 END) AS active_ic_count,
    SUM(CASE WHEN d.department_type = 'GENERAL' AND d.status = 'ACTIVE' THEN 1 ELSE 0 END) AS active_general_count,
    COUNT(d.department_id) AS total_departments
FROM campuses c
LEFT JOIN departments d ON d.campus_id = c.campus_id
WHERE c.status = 'ACTIVE'
GROUP BY c.campus_id, c.campus_code, c.name
ORDER BY c.campus_id;
```

Cần ít nhất một campus có:

```text
active_ic_count >= 1
active_general_count >= 1
```

Lý do:

```text
- STAFF/Staff Leader test user cần IC department theo trigger.
- DEPARTMENT/Department Leader authorization test user cần GENERAL department theo trigger.
```

### 4.6. Kiểm tra valid Staff Leader hiện có

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
WHERE r.role_code = 'STAFF'
  AND u.sub_role = 'LEADER'
ORDER BY u.user_id;
```

Valid fixture phải có:

```text
user_status = ACTIVE
campus_status = ACTIVE
primary_campus_id not null
department_id not null
department_type = IC
department_status = ACTIVE
```

Nếu chưa có, helper có thể tạo sau khi người dùng cho phép Integration Test ghi DB.

### 4.7. Kiểm tra dữ liệu Department hiện tại theo campus/type/status

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
- Biết seed nền có bao nhiêu rows.
- Không viết assertion exact count toàn campus một cách mù quáng.
- Chọn chiến lược unique token + distractor.
```

### 4.8. Kiểm tra dữ liệu UC-104 cũ còn sót

```sql
SELECT
    department_id,
    campus_id,
    name,
    department_type,
    status,
    head_user_id,
    created_by,
    updated_by
FROM departments
WHERE name LIKE '[IT-UC104-VIEW-DEPARTMENT-LIST]%'
   OR name LIKE '[IT-VIEW-DEPARTMENT-LIST]%'
ORDER BY department_id;
```

Nếu có rows cũ:

```text
- Chưa được tự DELETE trước khi xác nhận.
- Báo số lượng và FK phụ thuộc.
- Chỉ cleanup bằng helper đúng prefix sau khi được phép.
```

### 4.9. Kiểm tra prefix Department test khác để tránh overlap

```sql
SELECT department_id, name
FROM departments
WHERE name LIKE '[IT-UC101-ADD-DEPARTMENT]%'
   OR name LIKE '[IT-UC102-UPDATE-DEPARTMENT]%'
   OR name LIKE '[IT-UC103-SEARCH-FILTER-DEPARTMENT]%'
   OR name LIKE '[IT-UC104-VIEW-DEPARTMENT-LIST]%'
ORDER BY department_id;
```

### 4.10. Kiểm tra test user/session tích tụ

Chỉ read-only:

```sql
SELECT user_id, email, status, primary_campus_id, department_id
FROM users
WHERE email LIKE '%@it-uc63.pems.local'
ORDER BY user_id;
```

```sql
SELECT user_id, COUNT(*) AS session_count
FROM user_sessions
WHERE user_id IN (
    SELECT user_id
    FROM users
    WHERE email LIKE '%@it-uc63.pems.local'
)
GROUP BY user_id
ORDER BY user_id;
```

Report nếu session/head-user test tích tụ bất thường. Không tự xóa.

### 4.11. Kết luận DB readiness bắt buộc

Agent phải xuất bảng:

| Prerequisite | Required | Actual | Ready? |
|---|---:|---:|---|
| Current DB là pems_test | 1 | ... | Có/Không |
| Role STAFF | 1 | ... | Có/Không |
| Role HO | 1 | ... | Có/Không |
| Role ADMIN | 1 | ... | Có/Không |
| Role DEPARTMENT | 1 | ... | Có/Không |
| Role STUDENT | 1 | ... | Có/Không |
| Role VISITOR | 1 | ... | Có/Không |
| Active campus | >=1 | ... | Có/Không |
| Active campus cho scope test | >=2 | ... | Có/Không |
| Active IC dept trong campus fixture | >=1 | ... | Có/Không |
| Active GENERAL dept trong campus fixture | >=1 | ... | Có/Không |
| Valid Staff Leader hoặc helper tạo được | >=1 | ... | Có/Không |
| Stale UC-104 test rows | 0 mong muốn | ... | Có/Không |

Không được viết “pems_test đủ dữ liệu” nếu chưa chạy các SELECT này.

### 4.12. Khi pems_test thiếu dữ liệu

```text
- Không tự import fresh-create.
- Không tự INSERT/UPDATE.
- Báo thiếu chính xác role/campus/IC/GENERAL/second campus.
- Đề xuất dùng existing helper hoặc bổ sung test-only helper.
- Chờ người dùng xác nhận trước thao tác ghi DB.
```

---

## 5. Phân biệt Unit Test, Application Test và Integration Test

### 5.1. Không có validator thì không tạo validator test giả

Source hiện tại không có:

```text
ViewDepartmentListQueryValidator
```

Do đó không tạo file kiểu:

```text
ViewDepartmentListQueryValidatorTests.cs
```

với validator tự bịa hoặc mock không tồn tại.

### 5.2. Existing skipped test không được tính

File hiện tại:

```text
tests/PEMS.ApplicationTests/Departments/ViewDepartmentListQueryTests.cs
```

chỉ có:

```csharp
[Fact(Skip = "Pending UC specification")]
```

Yêu cầu:

```text
- Không để nguyên rồi báo Unit/Application Test pass.
- Hoặc thay bằng test thật.
- Hoặc xóa placeholder nếu project đã chuyển chuẩn sang PEMS.UnitTests + IntegrationTests.
- Report rõ quyết định.
```

### 5.3. Unit Test có ý nghĩa có thể tạo

Một test pure-contract hợp lệ:

```csharp
NewQuery_UsesExpectedDefaults()
```

Assert:

```text
Page = 1
PageSize = 20
Keyword = null
Status = null
SortBy = name
SortDirection = asc
```

Test này kiểm tra default contract thật của query, không dùng DB.

Không tạo hàng loạt test chỉ gán property rồi đọc lại vì đó là test vô nghĩa.

### 5.4. Không mock thin handler chỉ để pass

`ViewDepartmentListQueryHandler` chỉ gọi static executor. Một unit test mock rỗng không chứng minh:

```text
- EF query
- campus scope
- LEFT JOIN head
- pagination
- sorting
- DTO mapping
```

Các behavior chính phải ở Integration Test với MySQL thật.

### 5.5. Không dùng EF InMemory để thay MySQL cho behavior SQL

Không dùng EF InMemory làm bằng chứng cho:

```text
- ToLower/Contains translation
- collation/case behavior
- navigation join
- ordering null
- pagination SQL
```

Vì source production dùng Pomelo MySQL. Integration Test pems_test là nguồn xác nhận chính.

---

## 6. Tổ chức thư mục/file

### 6.1. Unit Test

Ưu tiên:

```text
tests/PEMS.UnitTests/Departments/ViewDepartmentList/ViewDepartmentListQueryTests.cs
```

Hoặc dùng `PEMS.ApplicationTests` nếu đó vẫn là project test chuẩn đang được build/run. Không để hai file trùng mục đích ở hai project.

### 6.2. Integration Test

```text
tests/PEMS.IntegrationTests/Departments/ViewDepartmentList/ViewDepartmentListApiTests.cs
```

### 6.3. Test infrastructure

Có thể sửa:

```text
tests/PEMS.IntegrationTests/TestInfrastructure/DatabaseResetHelper.cs
```

Bổ sung:

```csharp
public const string ViewDepartmentListNamePrefix = "[IT-UC104-VIEW-DEPARTMENT-LIST] ";
```

Ưu tiên dùng lại:

```text
CreateTestDepartmentAsync
DeleteTestDepartmentsAsync
EnsureTestUserAsync
CreateActiveSessionAsync
```

Không copy nguyên helper vào class test nếu đã có helper chung.

### 6.4. Parallelization

`tests/PEMS.IntegrationTests/AssemblyInfo.cs` hiện đã có:

```csharp
[assembly: CollectionBehavior(DisableTestParallelization = true)]
```

Agent phải xác nhận vẫn còn. Không tạo attribute trùng ở file khác.

---

## 7. Phạm vi được sửa

Được phép:

```text
tests/PEMS.UnitTests/Departments/ViewDepartmentList/...
tests/PEMS.ApplicationTests/Departments/ViewDepartmentListQueryTests.cs
tests/PEMS.IntegrationTests/Departments/ViewDepartmentList/...
tests/PEMS.IntegrationTests/TestInfrastructure/DatabaseResetHelper.cs
docs/testing/...
docs/prompt_test/...
backend/PEMS.Api/appsettings.Testing.example.json nếu chỉ sửa template, không secret
```

Không tự ý sửa production code:

```text
DepartmentsController
ViewDepartmentListQuery
ViewDepartmentListQueryHandler
DepartmentListQueryExecutor
StaffLeaderDepartmentScope
DTO/Entity/DbContext
SQL schema
frontend business behavior
```

Nếu test phát hiện lỗi production, chỉ report:

```text
- test fail
- expected
- actual
- source nghi ngờ
- mức độ security/data issue
```

Chờ duyệt rồi mới sửa production.

---

## 8. Test infrastructure và auth client

### 8.1. Staff Leader client hợp lệ

Phải có headers/claims:

```text
UserId
RoleCode = STAFF
SubRole = LEADER
SessionId
PrimaryCampusId
DepartmentId
```

Staff Leader test user phải:

```text
ACTIVE
primary campus ACTIVE
department_id thuộc IC ACTIVE cùng campus
```

### 8.2. Wrong-role clients

Với wrong-role authorization tests:

```text
- Tạo user/session thật bằng EnsureTestUserAsync.
- Gắn role/subrole đúng.
- Assert errorCode = DepartmentManagementForbidden.
- Không chỉ assert 403.
```

Nếu cùng error code cũng được dùng cho lỗi context khác, phải kiểm tra `StaffLeaderDepartmentScope` và TestAuthHandler để chắc test fail đúng nguyên nhân.

### 8.3. Staff Leader no-campus client

Có thể dùng user/session Staff Leader hợp lệ nhưng cố ý bỏ `PrimaryCampusIdHeader` để tạo claim campus null, nếu SessionValidationMiddleware vẫn cho qua.

Assert:

```text
422
NoCampusAssigned
```

Không tự tạo internal user vi phạm DB trigger chỉ để test thiếu campus.

---

## 9. Quy tắc an toàn database

### 9.1. Chỉ pems_test

Không dùng:

```text
pems_db
pems_dev
pems_local
pems
DB demo
DB dev đang chạy app
```

### 9.2. Không đọc secret

Không mở/in:

```text
appsettings.Development.json
.env
Google credentials
JWT secret
SMTP password
OAuth refresh token
```

Được kiểm tra tên DB bằng connection metadata an toàn hoặc SQL `SELECT DATABASE()` nhưng không in password.

### 9.3. Không import SQL gốc trực tiếp

Không chạy:

```bash
mysql pems_test < fresh_create.sql
```

trước khi scan `USE`, `DROP DATABASE`, `CREATE DATABASE`.

Nếu cần import, phải tạo bản copy an toàn cho pems_test và xin xác nhận riêng.

### 9.4. Cleanup

```text
- Chỉ xóa Department bắt đầu đúng ViewDepartmentListNamePrefix.
- Không xóa seed IC thật.
- Không xóa user/role/campus seed.
- Nếu Department có FK phụ thuộc, cleanup phải dừng và report; không cascade mù.
```

### 9.5. Head user fixture

Không tạo user mới mỗi test mà không cleanup/idempotency.

Nếu reuse DepartmentLead test user:

```text
- Xác nhận cùng campus.
- Chỉ set làm HeadUserId của Department test.
- Cleanup Department không xóa user.
```

---

## 10. Nguyên tắc chọn test vừa đủ, không duplicate UC-103

UC-104 đủ coverage khi chứng minh:

```text
1. Staff Leader được truy cập.
2. Role khác bị chặn.
3. Campus scope đúng.
4. Default page/pageSize/sort đúng.
5. Pagination slices + metadata đúng.
6. List trả đúng ACTIVE/INACTIVE khi không filter.
7. DTO mapping đúng.
8. GENERAL/IC CanToggleStatus đúng.
9. Head null/có giá trị map đúng.
10. Query không sửa departments.
```

Không cần copy lại toàn bộ:

```text
- keyword no-match
- head keyword matching
- case-insensitive search
- status invalid ignored
- search + filter AND logic
```

vì các behavior đó thuộc UC-103 và đã có class riêng. Chỉ test parity/shared executor một case nếu có giá trị.

---

## 11. Unit/Application Test cần tạo

### 11.1. Tối thiểu một test contract có ý nghĩa

```csharp
NewQuery_UsesExpectedDefaults()
```

Expected:

```text
Page = 1
PageSize = 20
Keyword = null
Status = null
SortBy = "name"
SortDirection = "asc"
```

### 11.2. Optional: PaginatedResult metadata

Chỉ thêm nếu `PaginatedResult<T>` chưa có test chung ở project:

```text
Create_CalculatesTotalPagesAndNavigationFlags
Create_EmptyResult_HasZeroPages
```

Không đặt các test common này trong UC-104 nếu đã có coverage chung.

### 11.3. Không tạo test giả

Cấm:

```csharp
Assert.True(true);
[Fact(Skip = "Pending...")]
```

Cấm test chỉ:

```text
new Query();
Assert.NotNull(query);
```

mà không kiểm tra contract có giá trị.

---

## 12. Integration Test cần tạo

File:

```text
tests/PEMS.IntegrationTests/Departments/ViewDepartmentList/ViewDepartmentListApiTests.cs
```

Class:

```csharp
public sealed class ViewDepartmentListApiTests
    : IClassFixture<PemsWebApplicationFactory>, IAsyncLifetime
```

Endpoint constant:

```csharp
private const string Url = "/api/departments/viewdepartmentlist";
```

Cleanup:

```csharp
DeleteTestDepartmentsAsync(db, DatabaseResetHelper.ViewDepartmentListNamePrefix)
```

### 12.1. Authorization matrix

#### 1. `Anonymous_Forbidden`

```text
No auth headers.
Expect 403.
Assert errorCode DepartmentManagementForbidden.
```

#### 2. `Staff_Forbidden`

```text
STAFF + STAFF.
Expect 403 + DepartmentManagementForbidden.
```

#### 3. `DepartmentLead_Forbidden`

```text
DEPARTMENT + LEADER.
Expect 403 + DepartmentManagementForbidden.
```

#### 4. `Department_Forbidden`

```text
DEPARTMENT + STAFF.
Expect 403 + DepartmentManagementForbidden.
```

#### 5. `Student_Forbidden`

```text
Expect 403 + DepartmentManagementForbidden.
```

#### 6. `Ho_Forbidden`

```text
Expect 403 + DepartmentManagementForbidden.
Đây là test quan trọng để bắt legacy permission leak.
```

#### 7. `Admin_Forbidden`

```text
Expect 403 + DepartmentManagementForbidden.
```

#### 8. `Visitor_Forbidden`

```text
Expect 403 + DepartmentManagementForbidden.
```

#### 9. `StaffLeader_WithoutCampus_UnprocessableEntity`

Optional nhưng có giá trị nếu infrastructure hỗ trợ:

```text
STAFF + LEADER + valid session, bỏ PrimaryCampusId claim.
Expect 422 + NoCampusAssigned.
```

### 12.2. Core list/default behavior

#### 10. `StaffLeader_DefaultCriteria_ReturnsFirstPageSortedByName`

Setup:

```text
3 GENERAL ACTIVE departments cùng campus, cùng unique token:
- c-third <token>
- a-first <token>
- b-second <token>
Seed không theo sort order.
```

Request:

```text
GET viewdepartmentlist?keyword=<token>
Không gửi page/pageSize/sortBy/sortDirection.
```

Assert:

```text
200
Page=1
PageSize=20
TotalItems=3
TotalPages=1
HasNextPage=false
HasPreviousPage=false
Items.Count=3
Thứ tự a-first, b-second, c-third
```

Phải assert exact isolated results, không chỉ Contains.

#### 11. `StaffLeader_Pagination_ReturnsCorrectSlicesAndMetadata`

Setup 5 rows chung token, tên `a`..`e`.

Call page 1, 2, 3 với pageSize 2 và sort name asc.

Assert exact IDs/order, metadata, flags, không overlap.

#### 12. `StaffLeader_PageBeyondLast_ReturnsEmptyItemsWithMetadata`

Optional:

```text
3 rows token, page=99, pageSize=2.
Items empty.
TotalItems=3.
TotalPages=2.
Page vẫn echo 99 theo executor.
HasPreviousPage=true.
HasNextPage=false.
```

### 12.3. Status/type/result shape

#### 13. `StaffLeader_StatusOmitted_ReturnsActiveAndInactive`

Setup:

```text
1 ACTIVE + token
1 INACTIVE + token
```

Request chỉ keyword token, không status.

Assert exact 2 rows và đúng status.

#### 14. `StaffLeader_GeneralDepartment_IsToggleable`

Seed GENERAL.

Assert item:

```text
DepartmentType=GENERAL
CanToggleStatus=true
```

#### 15. `StaffLeader_IcDepartment_IsNotToggleable`

Ưu tiên seed IC test có prefix riêng nếu schema cho phép. Không sửa seed IC thật.

Assert:

```text
DepartmentType=IC
CanToggleStatus=false
```

Nếu schema enforce một IC/campus, dùng existing IC read-only nhưng phải thiết kế request/page deterministic và không cleanup seed row.

#### 16. `StaffLeader_ResultItem_ContainsManagementFields`

Assert đầy đủ:

```text
DepartmentId
CampusId
CampusName
Name
HeadUserId
HeadFullName
Status
DepartmentType
CanToggleStatus
CreatedAt
UpdatedAt
```

Không chỉ assert ID/name.

#### 17. `StaffLeader_UnassignedHead_ReturnsNullHeadFields`

```text
HeadUserId=null
HeadFullName=null
```

#### 18. `StaffLeader_AssignedHead_ReturnsHeadIdentity`

Dùng deterministic same-campus head user.

Assert:

```text
HeadUserId đúng
HeadFullName đúng
```

Không tạo head user rác mỗi lần nếu không cleanup.

### 12.4. Campus security

#### 19. `StaffLeader_DoesNotSeeOtherCampusDepartments`

Setup:

```text
own row same token ở campus A
other row same token ở campus B
```

Assert:

```text
own row xuất hiện
other row không xuất hiện
```

Không chỉ assert item campus A có mặt.

Yêu cầu pems_test có >=2 active campuses hoặc test-only second campus được duyệt.

### 12.5. Shared executor parity

#### 20. `ViewListAndSearchFilter_SameCriteria_ReturnEquivalentResults`

Optional nhưng có giá trị vì UC-103/104 dùng chung executor.

Gọi:

```text
/api/departments/viewdepartmentlist?...same criteria...
/api/departments/searchandfilterdepartments?...same criteria...
```

Assert:

```text
Cùng items/order
Cùng Page/PageSize/TotalItems/TotalPages/flags
```

Dùng unique token để isolate.

Không cần lặp parity cho mọi filter.

### 12.6. Read-only

#### 21. `StaffLeader_ViewList_DoesNotModifyDepartments`

Snapshot target department trước request.

Sau request reload DB và assert toàn bộ:

```text
CampusId
Name
DepartmentType
HeadUserId
Status
CreatedAt
CreatedBy
UpdatedAt
UpdatedBy
```

Không đổi.

Có thể assert số rows có UC-104 prefix trước/sau bằng nhau.

---

## 13. Thiết kế dữ liệu để test không pass giả

### 13.1. Unique token

```csharp
Guid.NewGuid().ToString("N")
```

### 13.2. Distractor

Mỗi test isolation bằng keyword nên có:

```text
- target chứa token
- same-campus distractor không chứa token
```

Hoặc assert exact result count/order từ toàn bộ rows test tự tạo.

### 13.3. Không exact-count toàn campus

Sai:

```csharp
Assert.Equal(3, result.TotalItems);
```

nếu request không có unique token và campus có seed khác.

Đúng:

```text
- filter bằng unique token; hoặc
- snapshot baseline count rồi tính expected; hoặc
- dùng dedicated isolated campus đã được duyệt.
```

### 13.4. Pagination phải deterministic

```text
- Tên unique và có thứ tự rõ.
- Explicit sortBy=name, sortDirection=asc cho pagination slice test.
- Assert exact IDs theo page.
```

### 13.5. DTO mapping không dùng self-fulfilling expectation

Không build expected bằng chính `DepartmentListQueryExecutor`.

Expected lấy từ rows test đã seed và DB snapshot độc lập.

---

## 14. Test names đề xuất

```csharp
NewQuery_UsesExpectedDefaults()

Anonymous_Forbidden()
Staff_Forbidden()
DepartmentLead_Forbidden()
Department_Forbidden()
Student_Forbidden()
Ho_Forbidden()
Admin_Forbidden()
Visitor_Forbidden()
StaffLeader_WithoutCampus_UnprocessableEntity()

StaffLeader_DefaultCriteria_ReturnsFirstPageSortedByName()
StaffLeader_Pagination_ReturnsCorrectSlicesAndMetadata()
StaffLeader_PageBeyondLast_ReturnsEmptyItemsWithMetadata()
StaffLeader_StatusOmitted_ReturnsActiveAndInactive()
StaffLeader_GeneralDepartment_IsToggleable()
StaffLeader_IcDepartment_IsNotToggleable()
StaffLeader_ResultItem_ContainsManagementFields()
StaffLeader_UnassignedHead_ReturnsNullHeadFields()
StaffLeader_AssignedHead_ReturnsHeadIdentity()
StaffLeader_DoesNotSeeOtherCampusDepartments()
ViewListAndSearchFilter_SameCriteria_ReturnEquivalentResults()
StaffLeader_ViewList_DoesNotModifyDepartments()
```

Tên test không được nói quá assertion.

---

## 15. Commands được phép chạy

### 15.1. Không động database

```bash
dotnet build
dotnet test tests/PEMS.UnitTests/PEMS.UnitTests.csproj --filter "FullyQualifiedName~ViewDepartmentList"
```

Nếu test thật nằm trong ApplicationTests:

```bash
dotnet test tests/PEMS.ApplicationTests/PEMS.ApplicationTests.csproj --filter "FullyQualifiedName~ViewDepartmentList"
```

### 15.2. Read-only DB preflight

Chạy các SELECT ở mục 4. Không in connection password.

### 15.3. Integration Test chỉ sau DB safety gate + xác nhận

```bash
dotnet test tests/PEMS.IntegrationTests/PEMS.IntegrationTests.csproj \
  --filter "FullyQualifiedName~ViewDepartmentListApiTests"
```

Sau khi class riêng pass:

```bash
dotnet test tests/PEMS.IntegrationTests/PEMS.IntegrationTests.csproj \
  --filter "FullyQualifiedName~Departments"
```

Vì UC-103/104 chung executor, chạy thêm:

```bash
dotnet test tests/PEMS.IntegrationTests/PEMS.IntegrationTests.csproj \
  --filter "FullyQualifiedName~SearchFilterDepartmentsApiTests|FullyQualifiedName~ViewDepartmentListApiTests"
```

Cuối cùng nếu được phép:

```bash
dotnet test tests/PEMS.IntegrationTests/PEMS.IntegrationTests.csproj
```

Không báo Integration Test pass nếu chưa thực sự chạy.

---

## 16. Report bắt buộc sau task

```md
# Báo cáo tạo test UC-104 View Department List

## 1. Tóm tắt
[Đã tạo/sửa gì]

## 2. File đã tạo/sửa
| Loại | File | Mục đích |
|---|---|---|
| Unit/Application Test | ... | ... |
| Integration Test | ... | ... |
| Test Infrastructure | ... | ... |
| Docs | ... | ... |

## 3. Mapping source đã xác nhận
| Nội dung | Kết quả |
|---|---|
| UC mới | UC-104 View Department List |
| Legacy mismatch | ... |
| Endpoint | GET /api/departments/viewdepartmentlist |
| Query fields | ... |
| Validator | Không có/Có nếu source đổi |
| Actor | Staff Leader |
| Anonymous behavior | ... |
| Campus scope | ... |
| Shared executor với UC-103 | ... |
| Default paging/sort | ... |
| DTO fields | ... |
| CanToggleStatus | ... |

## 4. pems_test preflight
| Prerequisite | Actual | Ready? |
|---|---:|---|
| SELECT DATABASE() | ... | ... |
| Roles | ... | ... |
| Active campuses | ... | ... |
| Active IC | ... | ... |
| Active GENERAL | ... | ... |
| Valid Staff Leader | ... | ... |
| Stale UC-104 rows | ... | ... |

## 5. Unit/Application tests
[Liệt kê test thật; ghi rõ placeholder Skip đã xử lý thế nào]

## 6. Integration tests
[Liệt kê case]

## 7. DB safety
| Mục | Kết quả |
|---|---|
| Database | pems_test/... |
| Dùng pems_db | Không |
| Dùng Development settings | Không |
| Đọc/in secret | Không |
| Lệnh ghi DB đã chạy | Có/Không + xác nhận |
| Prefix cleanup | ... |
| Parallelization disabled | Có |

## 8. Kết quả lệnh
```text
[build]
[unit/application]
[integration class]
[department suite]
[full suite]
```

## 9. Test fail
| Test | Expected | Actual | Nhận định |
|---|---|---|---|
| ... | ... | ... | ... |

## 10. Production issue
[Chỉ report, không tự sửa]

## 11. Việc còn cần xác nhận
[...]
```

---

## 17. Definition of Done

Task chỉ hoàn thành khi:

```text
- Đã đọc source thật.
- Đã xác nhận endpoint GET /api/departments/viewdepartmentlist hoặc report nếu source đổi.
- Đã xác nhận UC-104/permission mới và mismatch legacy.
- Không tạo validator giả.
- Placeholder [Fact(Skip)] đã được thay/xóa hoặc report rõ.
- Có test default query contract có ý nghĩa hoặc giải thích vì sao không cần Unit Test.
- Có ViewDepartmentListApiTests thật.
- Authorization test assert status + errorCode.
- Staff Leader success path thật sự gọi API và đọc DB-backed response.
- Default page/pageSize/sort được assert.
- Pagination assert exact slices + metadata + flags.
- Campus scope có positive + negative row.
- DTO mapping đủ fields quan trọng.
- GENERAL/IC CanToggleStatus được kiểm tra.
- Head null/assigned được kiểm tra nếu fixture an toàn.
- Read-only test reload DB và assert unchanged.
- Không duplicate nguyên UC-103.
- Có prefix [IT-UC104-VIEW-DEPARTMENT-LIST] riêng.
- Cleanup chỉ dùng prefix riêng.
- Assembly parallelization vẫn disabled.
- Đã chạy SELECT trực tiếp trên pems_test và báo actual readiness.
- Không kết luận pems_test đủ chỉ từ SQL seed/source.
- Integration Test chỉ chạy sau DB safety gate và xác nhận.
- Không dùng pems_db/appsettings.Development.json/secret thật.
- Không sửa production code để ép pass.
- Report pass/fail thật.
```

---

## 18. Các lỗi nghiêm cấm

```text
- Copy SearchFilterDepartmentsApiTests và chỉ đổi route/class.
- Viết validator test khi source không có validator.
- Giữ [Fact(Skip)] rồi báo coverage.
- Assert.True(true).
- Chỉ Assert.Contains target mà không có distractor.
- Exact count toàn campus không cô lập.
- Dùng seed IC thật làm đối tượng update/delete.
- Tạo head user mới mỗi test mà không cleanup/idempotent.
- Dùng chung prefix UC-103/UC-104.
- Tự cho HO quyền theo PROJECT_OVERVIEW legacy.
- Đổi Anonymous expected sang 401 khi source thật đang trả 403 mà không report.
- Chạy Integration Test trước khi SELECT DATABASE().
- Import fresh-create gốc không scan USE/DROP/CREATE DATABASE.
- Dùng appsettings.Development.json.
- In password/secret vào log/report.
- Sửa controller/handler/executor chỉ để test xanh.
- Báo pems_test đủ dữ liệu khi chưa query live DB.
- Báo hoàn thành khi chưa chạy test hoặc chưa nói rõ lý do chưa chạy.
```

Nếu không chắc source, permission, database hoặc cleanup, phải dừng và báo rõ trước khi tiếp tục.
