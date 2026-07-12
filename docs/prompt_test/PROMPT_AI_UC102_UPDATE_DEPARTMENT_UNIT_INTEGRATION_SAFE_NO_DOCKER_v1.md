# PROMPT AI — TẠO TEST CODE THẬT CHO UC-102 UPDATE DEPARTMENT (UNIT TEST + INTEGRATION TEST, KHÔNG DOCKER, AN TOÀN DATABASE) — v1

> File này dùng để đưa cho AI Agent/Code Agent khi cần tạo hoặc cập nhật test tự động cho chức năng **Update Department** trong dự án PEMS.
>
> Use case này thuộc nhóm **Department Structure Management / Department Management**. Actor nghiệp vụ hiện tại theo ma trận quyền mới là **Staff Leader**.
>
> Mục tiêu: **tạo test code thật, chạy được, đúng nghiệp vụ Update Department, kiểm tra trước `pems_test` có đủ dữ liệu nền để chạy Integration Test hay chưa, và tuyệt đối không làm hỏng `pems_db` hoặc bất kỳ database dev/thật nào**.

---

## 0. Bối cảnh và kinh nghiệm phải kế thừa

Prompt này kế thừa chuẩn đã chốt từ các prompt/test **Create FAQ**, **Update FAQ**, **UC-62 View List FAQ**, **UC-66 Search FAQ** và **UC-101 Add New Department**:

```text
- Dùng xUnit.
- Dùng FluentValidation.TestHelper cho Unit Test validator.
- Dùng WebApplicationFactory cho Integration Test.
- Dùng TestAuthHandler để giả lập đăng nhập theo role.
- Dùng database test riêng, ví dụ pems_test.
- Không dùng Docker/Testcontainers.
- Không dùng appsettings.Development.json.
- Không đọc/copy/in secret thật.
- Không tự ý sửa production code để ép test pass.
- Tên test phải nói đúng hành vi thật sự được assert.
- Integration Test dùng chung pems_test phải tắt parallelization.
- Mỗi use case/test class phải có prefix dữ liệu test riêng.
- Cleanup không được xóa dữ liệu test class/use case khác.
- Nếu một test dùng tên DoesNotPersist/DoesNotModify/ReadOnly thì phải thật sự kiểm tra DB state.
- Với payload invalid trong update, cố tình gửi field khác khác dữ liệu cũ để bắt lỗi update partial.
- Nếu lỗi xuất hiện khi chạy toàn bộ IntegrationTests, phải kiểm tra race condition/test cleanup trước khi nghi production code.
```

Bài học đặc biệt từ lỗi race condition giữa các test trước:

```text
Không dùng chung một hằng số prefix cleanup cho nhiều use case.
Không cleanup bằng prefix rộng như [IT-DEPARTMENT], [TEST], [IT-UC102] nếu prefix đó có thể bị use case khác dùng lại.
Không dùng prefix overlap nhau, ví dụ [IT-DEPT] và [IT-DEPT-UPDATE].
Mỗi test class phải có prefix riêng, đủ rõ nghĩa, không trùng, không bao phủ prefix khác.
```

Prefix gợi ý cho use case này:

```text
[IT-UC102-UPDATE-DEPARTMENT]
```

Nếu source/docs hiện tại dùng UC ID khác cho Update Department, vẫn giữ tên nghiệp vụ trong prefix để tránh nhầm:

```text
[IT-UPDATE-DEPARTMENT]
```

---

## 0.1. Lưu ý quan trọng về UC ID

Người dùng đang yêu cầu use case **Update Department**.

Các tài liệu PEMS hiện có thể lệch UC ID:

```text
- USE_CASE_LIST.md / PERMISSION_MATRIX.md mới: UC-102 Update Department.
- Một số PROJECT_OVERVIEW cũ: UC-99 Update Department.
- RTW template có thể ghi UC-100 Update Department.
```

Quy tắc:

```text
- Ưu tiên tên nghiệp vụ ổn định: Update Department.
- Ưu tiên USE_CASE_LIST.md/PERMISSION_MATRIX.md mới nhất nếu cần UC ID cho report.
- Nếu source/docs có mâu thuẫn, báo rõ trong report.
- Không hardcode UC ID vào logic test nếu source không dùng.
- Tên folder/test nên dùng tên nghiệp vụ ổn định: Departments/UpdateDepartment.
```

Không tự đổi sang:

```text
- Add New Department
- Search and Filter Departments
- View Department List
- View Department Details
- Manage Department Status
- Add Department Personnel
- Reassign Department Lead
```

---

## 0.2. Ai sẽ đọc file này?

File này phải dễ hiểu cho nhiều bộ phận:

```text
Product/BA  -> hiểu test đang kiểm tra nghiệp vụ gì.
Dev         -> biết cần tạo/sửa test code ở đâu.
Tester/QA   -> biết test nào là Unit Test, test nào là Integration Test.
Reviewer    -> biết phạm vi được sửa, không được sửa, và cách kiểm tra pass/fail.
AI Agent    -> biết chính xác phải làm gì, không được tự đoán.
```

Không viết test kiểu lý thuyết. Không chỉ liệt kê test case. Phải tạo hoặc cập nhật file test thật trong source code.

---

## 1. Mục tiêu của task

Tạo test tự động cho chức năng **Staff Leader cập nhật Department**.

Test cần gồm 2 nhóm:

```text
1. Unit Test
   Chỉ kiểm tra logic nhỏ, chủ yếu là command validator hoặc helper thuần nếu có.
   Không gọi API thật.
   Không dùng database thật.

2. Integration Test
   Kiểm tra Update Department chạy qua nhiều layer thật:
   API + Authentication/Authorization giả + Controller + MediatR + Validator + Handler + database test riêng.
```

Sau khi hoàn thành, team phải biết rõ:

```text
- Update Department đang dùng endpoint nào trong source thật.
- Actor hợp lệ thật là ai.
- Request DTO thật gồm các field nào.
- departmentId nằm trên route hay trong body.
- Schema departments thật có field nào và không có field nào.
- Update Department có được đổi status không, hay status thuộc UC Manage Department Status riêng.
- Update Department có update head_user_id không, hay head/lead thuộc UC khác.
- pems_test có đủ active campus, IC department, Staff Leader test và target department test để chạy chưa.
- Test nào pass/fail và đã chạy bằng lệnh nào.
```

---

## 2. Nguồn phải đọc trước khi sửa

Trước khi tạo/sửa test, AI Agent phải search và đọc source hiện tại. Không sửa theo suy đoán.

### 2.1. Tài liệu/source PEMS cần đối chiếu

Đọc các nguồn phù hợp dưới đây:

```text
PROJECT_OVERVIEW_v8_4_refined_v6_v10_FULL_UPDATED.md
PEMS_CANONICAL_BUSINESS_RULES_v8_4_refined_v6_v10_FULL_UPDATED.md
PEMS_UC_IMPLEMENTATION_RULEBOOK_FRONTEND_BACKEND_DATABASE_VALIDATION_SECURITY_v8_4_refined_v6_v10_FULL_UPDATED.md
PROMPT_STANDARDIZE_ROLE_SUBROLE_DEPARTMENT_v8_4_refined_v6_v10_FULL_UPDATED.md
PROJECT_STRUCTURE_FULL.md
PEMS_v10_NEW_FINAL_SQL_TABLE_FIELD_DICTIONARY_MATCHED.docx
CLEAN_ARCHITECTURE.md
USE_CASE_LIST.md
PERMISSION_MATRIX.md
PERMISSION_RULES.md
Report 3.1_UCS_Template.docx nếu cần đối chiếu UC/spec
Report 5.2_L1-UnitTests_Template.xlsx nếu cần đối chiếu format unit test report
Report 5.2_L2-IntegrationTests_Template.xlsx nếu cần đối chiếu format integration test report
Source code backend hiện tại
Existing tests hiện tại, đặc biệt Department/AddDepartment/Account/Faq tests đã làm xong
SQL fresh-create mới nhất trong docs/database/scripts/
```

### 2.2. Source code bắt buộc kiểm tra

AI Agent phải tự search đúng file trong project, không được bịa path.

Tối thiểu cần kiểm tra:

```text
Backend controller liên quan department: DepartmentsController hoặc controller tương đương
Update Department Command/Request DTO
Update Department CommandHandler
Update Department CommandValidator
Update Department Response DTO nếu có
Department entity
EF Configuration của departments
ApplicationDbContext
Constants/Enums liên quan department_type/status
Authorization/Role check liên quan Department Management
Existing Unit Tests: tests/PEMS.UnitTests/Departments/... nếu có
Existing Integration Tests: tests/PEMS.IntegrationTests/Departments/... nếu có
Existing AddDepartment tests nếu đã tạo trước đó
Existing test infrastructure: PemsWebApplicationFactory, TestAuthHandler, DatabaseResetHelper
SQL fresh-create mới nhất trong docs/database/scripts/
```

Nếu tên class/path trong project khác ví dụ trong file này, dùng tên thật trong source và ghi rõ trong report.

### 2.3. Source-first rule

Không được tự bịa:

```text
- endpoint update department
- request body field
- response DTO
- department status/type enum
- actor được phép
- scope Staff Leader theo campus
- duplicate rule
- no-change behavior
- audit behavior
- SQL table/field
```

Nếu tài liệu, code, SQL hoặc comment cũ mâu thuẫn nhau, phải hỏi lại, không được tự suy đoán

```text
1. SQL fresh-create mới nhất
2. SQL Table & Field Dictionary mới nhất
3. PEMS_CANONICAL_BUSINESS_RULES
4. PEMS_UC_IMPLEMENTATION_RULEBOOK
5. PROMPT_STANDARDIZE_ROLE_SUBROLE_DEPARTMENT
6. PROJECT_OVERVIEW
7. VISITOR_MANAGEMENT_SYSTEM
8. Source code hiện tại
9. Tài liệu legacy chỉ dùng để đối chiếu
```

Riêng authorization/security: nếu source hiện tại thiếu authorization nhưng tài liệu/policy nói rõ chỉ Staff Leader được phép, **không được sửa expected test thành OK cho anonymous/role sai**. Viết test theo policy đúng để phát hiện lỗi bảo mật, báo test fail là production security issue, không tự sửa production code khi chưa được duyệt.

---

## 3. Nghiệp vụ Update Department phải giữ đúng

### 3.1. Actor hợp lệ

Theo Permission Matrix hiện tại:

```text
Chỉ Staff Leader được Update Department.
```

Backend phải tự kiểm tra quyền. Frontend ẩn/hiện nút không đủ để bảo mật.

Các role khác gọi API trực tiếp phải bị chặn:

```text
Không đăng nhập -> 401 Unauthorized.
Đăng nhập nhưng không phải Staff Leader -> 403 Forbidden.
Staff Leader hợp lệ -> được cập nhật department nếu dữ liệu hợp lệ và trong scope hợp lệ.
```

Không mặc định Admin/HO có toàn quyền. Nếu nghiệp vụ hiện tại là Staff Leader-only thì Admin/HO phải bị 403.

### 3.2. Cảnh báo security đã biết về DepartmentsController

Tài liệu implementation hiện có cảnh báo: `DepartmentsController` có thể thiếu `[Authorize]`/role guard trong code hiện tại.

Yêu cầu cho AI Agent:

```text
- Phải kiểm tra source thật xem DepartmentsController/UpdateDepartment endpoint đã có authorization chưa.
- Nếu thiếu authorization, vẫn tạo authorization Integration Tests theo policy Staff Leader-only.
- Không bỏ authorization test chỉ vì source đang thiếu guard.
- Không sửa production code nếu chưa được duyệt.
- Report rõ: test fail do endpoint thiếu auth/role guard, nghi ngờ production security issue.
```

### 3.3. Endpoint/API phải lấy từ source thật

Không được tự bịa route.

AI Agent phải search trong `DepartmentsController` hoặc controller tương đương để xác định endpoint thật.

Các khả năng thường gặp:

```text
PUT /api/departments/{departmentId}
PUT /api/departments/{id}
PATCH /api/departments/{departmentId}
PUT /api/departments/update/{departmentId}
POST /api/departments/{departmentId}/update
```

Dùng đúng endpoint hiện tại trong source.

### 3.4. Schema departments hiện tại

Theo SQL v10 mới nhất, bảng `departments` có các field chính:

```text
department_id
campus_id
name
department_type
head_user_id
status
created_at
created_by
updated_at
updated_by
```

Ràng buộc quan trọng:

```text
PRIMARY KEY: department_id
UNIQUE: (campus_id, name)
FK: campus_id -> campuses(campus_id)
department_type: IC / GENERAL
status: ACTIVE / INACTIVE
name: VARCHAR(150)
```

Không tự test hoặc code các field không có trong schema/source như:

```text
description
phone
email
address
permission_code
permission_level
language_code
```

### 3.5. Update Department không phải Manage Department Status

Vì đã có UC riêng **Manage Department Status**, Update Department không được tự giả định sẽ đổi `status`.

Quy tắc:

```text
- Nếu source request DTO không có status: test phải assert status giữ nguyên khi update.
- Nếu source request DTO có status: phải đọc source để xem đây có thật sự là Update Department hay đang trộn với Manage Department Status.
- Không tự thêm test đổi ACTIVE/INACTIVE nếu source tách status sang UC Manage Department Status.
```

### 3.6. Update Department không phải Add Department Personnel/Reassign Department Lead

Nếu request DTO có `headUserId`, phải đọc source để xác định đây là field được phép update trong UC-102 hay thuộc UC khác.

Không tự bịa rule:

```text
- Không assume head_user_id bắt buộc nếu source không yêu cầu.
- Không assume Department Leader assignment nằm trong Update Department nếu source tách sang Add Department Personnel/Reassign Department Lead.
- Nếu source có update head_user_id, test invalid/non-existing/head user wrong campus chỉ viết sau khi đọc source.
```

### 3.7. Scope campus của Staff Leader

Staff Leader chỉ được thao tác trong scope campus của mình nếu source/policy Department Management đang áp dụng campus scope.

Cần kiểm tra:

```text
- Staff Leader có primary_campus_id.
- Target department thuộc cùng campus với Staff Leader.
- Staff Leader không được update department campus khác.
```

Nếu source hiện tại không enforce scope, test theo policy đúng có thể fail. Không sửa expected để pass; report production issue.

### 3.8. Duplicate rule

Vì SQL có unique `(campus_id, name)`, Update Department cần tránh tạo duplicate name trong cùng campus.

Rule cần kiểm tra theo source:

```text
- Update department B thành name đã tồn tại ở department A cùng campus -> Conflict/BadRequest theo convention source.
- Duplicate check phải exclude chính department đang update.
- Same name của chính nó khi update field khác không được bị coi là duplicate.
- Same name ở campus khác có thể hợp lệ ở DB level, nhưng không viết API test nếu Staff Leader không có quyền cập nhật khác campus.
```

### 3.9. Audit behavior

Khi update thật sự có thay đổi:

```text
- updated_at phải được refresh nếu source có audit.
- updated_by phải là Staff Leader user id nếu source có audit.
- created_at/created_by phải giữ nguyên.
```

Nếu no-change update được source xử lý riêng:

```text
- Đọc source để xác nhận no-change có ghi DB không.
- Test theo behavior thật hiện tại, không theo giả định.
- Nếu no-change không ghi DB, expected: OK/Changed=false nếu response có field này, updated_at/updated_by không đổi.
- Nếu no-change vẫn refresh audit, expected phải ghi rõ và assert đúng.
```

Không sửa production code để ép theo prompt nếu source có behavior rõ và hợp lý; nếu spec/source mâu thuẫn, report mismatch.

### 3.10. Input normalization / sanitize

Chỉ test các rule có trong source thật:

```text
- name trim trước khi lưu nếu source có trim.
- name không được empty/whitespace.
- name max length theo validator/source, schema gợi ý 150.
- department_type phải là IC/GENERAL.
- sanitize HTML/script nếu source có xử lý text sanitize.
```

Không tự bịa sanitize/trim nếu source không có.

---

## 4. Kiểm tra `pems_test` trước khi chạy Integration Test

Người dùng yêu cầu riêng: **kiểm tra cả `pems_test` xem đủ dữ liệu test chưa**.

### 4.1. Mục tiêu kiểm tra

Trước khi chạy Integration Test Update Department, AI Agent phải xác nhận `pems_test` có đủ dữ liệu nền:

```text
1. Database hiện tại đúng là pems_test, không phải pems_db.
2. Có ít nhất 1 campus ACTIVE để Staff Leader thuộc về.
3. Có ít nhất 1 department_type = IC, status ACTIVE trong campus đó để Staff Leader test có department_id hợp lệ.
4. Có hoặc tạo được Staff Leader test: role_code = STAFF, sub_role = LEADER, status ACTIVE, primary_campus_id hợp lệ, department_id thuộc IC department cùng campus.
5. Có thể tạo target department test để update, dùng prefix riêng Update Department.
6. Không có dữ liệu test cũ với prefix Update Department còn sót lại, hoặc cleanup được bằng prefix riêng.
7. Không dùng production/dev seed thật làm target bị update/delete nếu có thể tránh.
```

### 4.2. Chỉ được chạy read-only SELECT khi chưa được duyệt ghi DB

Các lệnh kiểm tra ban đầu chỉ được là read-only SELECT.

Không chạy INSERT/UPDATE/DELETE/TRUNCATE/ALTER/DROP nếu chưa được người dùng xác nhận.

Gợi ý query kiểm tra:

```sql
SELECT DATABASE() AS current_database;
```

Expected:

```text
current_database = pems_test
```

Kiểm tra campus active:

```sql
SELECT campus_id, campus_code, name, status
FROM campuses
WHERE status = 'ACTIVE'
ORDER BY campus_id;
```

Kiểm tra department IC active:

```sql
SELECT d.department_id, d.campus_id, c.campus_code, d.name, d.department_type, d.status
FROM departments d
JOIN campuses c ON c.campus_id = d.campus_id
WHERE d.department_type = 'IC'
  AND d.status = 'ACTIVE'
  AND c.status = 'ACTIVE'
ORDER BY d.campus_id, d.department_id;
```

Kiểm tra Staff Leader active hợp lệ:

```sql
SELECT u.user_id, u.email, r.role_code, u.sub_role, u.primary_campus_id,
       u.department_id, d.department_type, u.status AS user_status,
       c.status AS campus_status, d.status AS department_status
FROM users u
JOIN roles r ON r.role_id = u.role_id
JOIN campuses c ON c.campus_id = u.primary_campus_id
JOIN departments d ON d.department_id = u.department_id
WHERE r.role_code = 'STAFF'
  AND u.sub_role = 'LEADER'
  AND u.status = 'ACTIVE'
  AND c.status = 'ACTIVE'
  AND d.department_type = 'IC'
  AND d.status = 'ACTIVE';
```

Kiểm tra dữ liệu test Update Department còn sót:

```sql
SELECT department_id, campus_id, name, department_type, status, created_by, updated_by
FROM departments
WHERE name LIKE '[IT-UC102-UPDATE-DEPARTMENT]%'
   OR name LIKE '[IT-UPDATE-DEPARTMENT]%';
```

Kiểm tra dữ liệu test Add Department còn sót để tránh nhầm prefix/collision:

```sql
SELECT department_id, campus_id, name, department_type, status
FROM departments
WHERE name LIKE '[IT-UC101-ADD-DEPARTMENT]%'
   OR name LIKE '[IT-ADD-DEPARTMENT]%';
```

### 4.3. Nếu `pems_test` chưa đủ dữ liệu nền

Nếu thiếu active campus / IC department / Staff Leader hợp lệ:

```text
- Không chạy Integration Test ngay.
- Không tự tạo dữ liệu nền bằng INSERT nếu chưa được duyệt.
- Báo rõ thiếu gì.
- Đề xuất một trong hai hướng:
  1. Dùng existing DatabaseResetHelper.EnsureTestUserAsync/EnsureStaffLeaderAsync nếu helper đã có và người dùng cho phép chạy Integration Test ghi DB.
  2. Tạo helper seed test-only có prefix rõ ràng và cleanup an toàn, nhưng chỉ chạy sau khi người dùng xác nhận.
```

### 4.4. Nếu cần helper seed dữ liệu nền

Nếu source test infrastructure đã có helper tương đương, dùng lại.

Nếu chưa có, có thể bổ sung helper trong `DatabaseResetHelper`, nhưng phải tuân thủ:

```text
- Helper tạo dữ liệu test chỉ trong pems_test.
- Không dùng dữ liệu thật làm đối tượng bị update/delete.
- Tên department test phải có prefix riêng.
- Staff Leader test phải có role_code STAFF + sub_role LEADER.
- Staff Leader phải có primary_campus_id.
- Staff Leader phải có department_id thuộc IC department cùng campus.
- Target department test để update phải thuộc campus của Staff Leader.
- Target department test không nên có user/task phụ thuộc nếu cleanup phải delete.
- Cleanup không được xóa campus/department/user seed thật.
```

Ví dụ hướng thiết kế:

```csharp
public const string UpdateDepartmentNamePrefix = "[IT-UC102-UPDATE-DEPARTMENT] ";

CreateTestDepartmentAsync(db, name, campusId, departmentType, status, createdBy, updatedAt, updatedBy)
DeleteTestDepartmentsAsync(db, prefix)
EnsureStaffLeaderAsync(db, campusId)
```

Hoặc dùng prefix truyền vào:

```csharp
CreateTestDepartmentAsync(db, prefix, nameSuffix, campusId, departmentType, status, createdBy)
DeleteTestDepartmentsAsync(db, prefix)
```

---

## 5. Phân biệt rõ Unit Test và Integration Test

### 5.1. Unit Test phù hợp với Update Department

Unit Test chỉ kiểm tra logic nhỏ, cô lập.

Phù hợp để test:

```text
- Command validator.
- Required field.
- Max length theo validator.
- Enum department_type hợp lệ/không hợp lệ.
- departmentId route/body <= 0 nếu validator nhận id.
- headUserId invalid nếu source có field này và validator có rule.
```

Không phù hợp để test bằng Unit Test nếu phải dùng DB:

```text
- department tồn tại hay không.
- duplicate name trong cùng campus.
- target department thuộc campus của Staff Leader hay không.
- updated_at/updated_by có persist DB không.
- no partial update / rollback khi fail.
```

Các case đó đưa sang Integration Test.

### 5.2. Integration Test phù hợp với Update Department

Integration Test kiểm tra nhiều phần chạy cùng nhau:

```text
HTTP request -> Auth/TestAuthHandler -> Controller -> MediatR -> Validator -> Handler -> DB test
```

Update Department là use case ghi DB, nên Integration Test phải kiểm tra DB state thật sau success/fail.

Integration Test phải chứng minh:

```text
- Staff Leader được update department hợp lệ trong scope.
- Role không có quyền bị chặn.
- Invalid payload bị chặn và record cũ không đổi.
- Non-existing department trả lỗi đúng.
- Duplicate name same campus bị chặn nếu source có rule/convention.
- Success update đúng field được phép.
- Status giữ nguyên nếu Update Department không quản lý status.
- Audit update đúng nếu source có audit.
- created_at/created_by không bị thay đổi.
```

---

## 6. Quy ước tổ chức thư mục test

### 6.1. Unit Test folder

Unit Test đặt trong:

```text
tests/PEMS.UnitTests/Departments/UpdateDepartment/
```

File gợi ý:

```text
UpdateDepartmentCommandValidatorTests.cs
```

Dùng đúng tên command thật trong source. Ví dụ source có thể gọi là:

```text
UpdateDepartmentCommandValidator
EditDepartmentCommandValidator
UpdateDepartmentDetailsCommandValidator
```

### 6.2. Integration Test folder

Integration Test đặt trong:

```text
tests/PEMS.IntegrationTests/Departments/UpdateDepartment/
```

File gợi ý:

```text
UpdateDepartmentApiTests.cs
```

Dùng đúng tên nghiệp vụ/folder ổn định, không đặt lẫn trong FAQ tests hoặc AddDepartment tests.

### 6.3. Helper dùng chung và prefix dữ liệu test

Ưu tiên dùng lại:

```text
tests/PEMS.IntegrationTests/TestInfrastructure/PemsWebApplicationFactory.cs
tests/PEMS.IntegrationTests/TestInfrastructure/TestAuthHandler.cs
tests/PEMS.IntegrationTests/TestInfrastructure/DatabaseResetHelper.cs
```

Nếu cần bổ sung helper seed Department cho Update Department, bổ sung vào `DatabaseResetHelper` hoặc helper chung tương đương. Không copy helper lặp lại trong từng file test.

Helper nên hỗ trợ:

```text
- Tạo department test với prefix riêng.
- Tạo department test với campus_id, department_type, status.
- Tạo department test với created_by/created_at/updated_by/updated_at nếu cần audit test.
- Trả về departmentId.
- Chụp snapshot department trước update.
- Cleanup department theo đúng prefix được truyền vào.
- Không hardcode DepartmentNamePrefix dùng chung cho mọi Department test.
- Không cleanup prefix của use case/test class khác.
```

Ví dụ:

```csharp
public const string UpdateDepartmentNamePrefix = "[IT-UC102-UPDATE-DEPARTMENT] ";

CreateTestDepartmentAsync(db, name, campusId, departmentType, status, createdBy)
SnapshotDepartmentAsync(db, departmentId)
AssertDepartmentUnchangedAsync(db, departmentId, snapshot)
DeleteTestDepartmentsAsync(db, prefix)
```

Hoặc dùng prefix truyền vào:

```csharp
CreateTestDepartmentAsync(db, prefix, nameSuffix, campusId, departmentType, status, createdBy)
DeleteTestDepartmentsAsync(db, prefix)
```

### 6.4. Quy tắc chạy tuần tự Integration Test

Vì project Integration Test dùng chung một MySQL database thật `pems_test`, phải tắt parallelization ở assembly `PEMS.IntegrationTests` nếu chưa có.

Thêm một file assembly-level, ví dụ:

```text
tests/PEMS.IntegrationTests/AssemblyInfo.cs
```

Nội dung:

```csharp
using Xunit;

[assembly: CollectionBehavior(DisableTestParallelization = true)]
```

Lý do:

```text
- xUnit mặc định có thể chạy các test class khác nhau song song.
- Các Integration Test dùng chung pems_test và cleanup DB trong DisposeAsync.
- Nếu chạy song song, cleanup của class này có thể xóa dữ liệu class kia đang dùng dở.
- Lỗi có thể biểu hiện thành NotFound, DbUpdateConcurrencyException, duplicate/search/filter trả sai.
- Đây là lỗi test infrastructure, không phải lỗi production code.
```

Dù đã tắt parallelization, vẫn phải tách prefix theo use case/test class. Tắt parallelization là lớp an toàn runtime; tách prefix là lớp an toàn dữ liệu và ngữ nghĩa.

---

## 7. Phạm vi AI Agent được sửa

AI Agent được phép tạo/sửa:

```text
tests/PEMS.UnitTests/Departments/UpdateDepartment/...
tests/PEMS.IntegrationTests/Departments/UpdateDepartment/...
tests/PEMS.UnitTests/TestHelpers/...
tests/PEMS.IntegrationTests/TestInfrastructure/...
docs/testing/...
backend/PEMS.Api/appsettings.Testing.example.json
file prompt/test documentation liên quan
```

AI Agent được phép sửa `DatabaseResetHelper` để thêm helper seed/snapshot/cleanup department test an toàn.

AI Agent không được tự ý sửa production code để ép test pass.

Không được tự ý sửa:

```text
Controller thật
Handler thật
Validator thật
Entity thật
DbContext thật
SQL schema thật
Frontend nghiệp vụ thật
appsettings.Development.json
.env
settings.local.json
Google credential JSON
```

Nếu phát hiện production code có lỗi, chỉ báo cáo:

```text
- Test nào fail.
- Expected là gì.
- Actual là gì.
- File production nghi ngờ có lỗi.
- Đề xuất hướng sửa.
```

Chờ người dùng/dev/reviewer duyệt rồi mới sửa production code.

---

## 8. Test infrastructure cho Integration Test

Ưu tiên dùng lại test infrastructure đã có từ các UC trước.

Yêu cầu:

```text
- PemsWebApplicationFactory khởi động API trong environment Testing.
- Không dùng appsettings.Development.json.
- Override authentication bằng test scheme.
- Cho phép tạo request giả với role StaffLeader, Staff, Ho, Admin, DepartmentLeader, DepartmentStaff, Visitor.
- Cho phép gọi request không đăng nhập để test 401.
- Connection string phải trỏ tới database test riêng.
- Không dùng Docker.
- Không dùng Testcontainers.
- Không gọi service ngoài thật như Google SSO, SMTP, Google Drive.
```

Nếu project đã có test infrastructure tương đương, ưu tiên dùng lại và bổ sung thiếu sót, không tạo trùng.

---

## 9. QUY TẮC AN TOÀN DATABASE CHO INTEGRATION TEST

Phần này là bắt buộc. Không được bỏ qua.

### 9.1. Nguyên tắc số 1

Integration Test chỉ được dùng database test riêng.

Database test gợi ý:

```text
pems_test
```

Không được dùng database dev/thật như:

```text
pems_db
pems_dev
pems_local
pems
database đang dùng cho app dev/demo
```

Nếu không chắc database nào là test, phải dừng lại và hỏi người dùng.

### 9.2. Không được tự động tạo/drop/import database nếu chưa được xác nhận

Mặc định, AI Agent **không được tự chạy lệnh ghi/xóa database**.

Không được tự chạy:

```text
DROP DATABASE
CREATE DATABASE
DROP TABLE
TRUNCATE
DELETE
UPDATE
INSERT
ALTER TABLE
mysql < script.sql
SOURCE script.sql
dotnet test PEMS.IntegrationTests nếu test sẽ ghi vào DB
```

Chỉ được chạy các lệnh đó sau khi:

```text
1. Đã báo cáo rõ sẽ thao tác trên database nào.
2. Đã chứng minh đó là database test.
3. Đã kiểm tra SQL script không trỏ nhầm database dev.
4. Đã hỏi người dùng xác nhận.
5. Người dùng trả lời rõ ràng cho phép tiếp tục.
```

Nếu người dùng chưa xác nhận, chỉ được tạo code/test/docs và chạy Unit Test không động DB.

### 9.3. Không import trực tiếp SQL fresh-create gốc

Không chạy trực tiếp file SQL gốc bằng lệnh kiểu này:

```bash
mysql pems_test < original_fresh_create.sql
```

Lý do:

```text
Tên database trên command line không đủ để bảo vệ database dev.
File SQL bên trong có thể có USE pems_db.
File SQL bên trong cũng có thể có DROP DATABASE/CREATE DATABASE pems_db.
```

Quy tắc bắt buộc:

```text
1. Không import trực tiếp SQL fresh-create gốc.
2. Trước khi import, phải kiểm tra file SQL có DROP DATABASE / CREATE DATABASE / USE hay không.
3. Nếu file SQL có pems_db, không được chạy trực tiếp.
4. Phải tạo bản copy tạm riêng cho pems_test nếu cần import.
5. Chỉ import sau khi bản copy tạm không còn thao tác nào vào pems_db.
```

### 9.4. Không đọc/copy/in secret thật

Không được mở hoặc in nội dung các file có thể chứa secret thật:

```text
appsettings.Development.json
.env
settings.local.json
Google credential JSON
SMTP password
JWT secret
OAuth client secret
Refresh token
API key
```

Nếu cần nhắc đến secret, dùng placeholder:

```text
YOUR_TEST_DB_PASSWORD
YOUR_TEST_JWT_SECRET
YOUR_GOOGLE_CLIENT_SECRET
```

---

## 10. Nguyên tắc chọn test case vừa đủ

Không viết test theo kiểu bao phủ máy móc mọi biến thể. Chỉ tạo test có ý nghĩa theo source thật và rủi ro chính của use case.

Một use case được coi là đủ test khi đã kiểm tra được:

```text
1. Ai được phép thực hiện.
2. Ai không được phép thực hiện.
3. Dữ liệu hợp lệ thì hệ thống xử lý đúng và DB thay đổi đúng.
4. Dữ liệu sai quan trọng thì bị chặn.
5. Rule nghiệp vụ đặc biệt được kiểm tra.
6. Khi request fail, DB không bị ghi/sửa sai.
7. Rủi ro bảo mật chính như authorization, scope campus, duplicate, audit được kiểm tra nếu liên quan.
```

Với Update Department, tránh test thừa:

```text
Không test sâu Add New Department.
Không test Manage Department Status nếu status tách UC riêng.
Không test Search/View Department List.
Không test Add Department Personnel.
Không test Reassign Department Lead nếu head/lead là UC riêng.
Không test tạo account Staff/Department nếu không cần cho Update Department.
Không duplicate toàn bộ Department Management test khác.
```

Chỉ test các case có giá trị cho Update Department:

```text
- Staff Leader-only authorization.
- Valid update.
- Required/invalid input.
- Non-existing department.
- Duplicate name same campus.
- Campus scope nếu source/policy hỗ trợ.
- Status giữ nguyên nếu status không thuộc Update Department.
- Audit updated_by/updated_at nếu source hỗ trợ.
- Created audit giữ nguyên.
- No partial update on failure.
```

---

## 11. Unit Test cần tạo

Chỉ tạo Unit Test theo source thật hiện tại.

### 11.1. Validator tests

File gợi ý:

```text
tests/PEMS.UnitTests/Departments/UpdateDepartment/UpdateDepartmentCommandValidatorTests.cs
```

Dùng đúng tên command thật. Ví dụ source có thể gọi là:

```text
UpdateDepartmentCommandValidator
EditDepartmentCommandValidator
UpdateDepartmentDetailsCommandValidator
```

Các case tối thiểu, điều chỉnh theo source thật:

```text
1. ValidCommand_NoErrors
   Payload hợp lệ -> không lỗi.

2. DepartmentId_Zero_HasError
   departmentId = 0 -> lỗi. Quan trọng nếu id nằm trên route và được đưa vào command.

3. DepartmentId_Negative_HasError
   Nếu source dùng signed int/long và có thể truyền âm.

4. Name_Null_HasError
   Nếu name nullable/string? trong command.

5. Name_Empty_HasError
   name = "" -> lỗi.

6. Name_Whitespace_HasError
   name chỉ whitespace -> lỗi.

7. Name_TooLong_HasError
   Nếu validator/source có max length, theo schema gợi ý name VARCHAR(150).

8. Name_MaxLength_NoError
   Nếu validator có max length 150, đúng 150 ký tự phải hợp lệ.

9. DepartmentType_NullOrEmpty_HasError
   Nếu field departmentType bắt buộc.

10. DepartmentType_Invalid_HasError
    Ví dụ "ACADEMIC", "INTERNATIONAL", "DEPT".

11. DepartmentType_Ic_NoError
    IC hợp lệ.

12. DepartmentType_General_NoError
    GENERAL hợp lệ.

13. HeadUserId_Zero_HasError
    Chỉ viết nếu request có headUserId và validator có rule.
```

Không test `description` nếu source/schema không có field này.

Không test duplicate/campus exists/scope ở Unit Test nếu phải dùng DB.

### 11.2. Handler Unit Test

Chỉ viết Handler Unit Test nếu dependency có thể mock/fake rõ ràng.

Có thể viết nếu source có helper thuần cho:

```text
- trim name trước khi lưu.
- normalize department type.
- map response DTO thuần.
- detect no-change thuần không cần DB.
```

Không ép test các case sau ở Unit Test nếu phụ thuộc EF/database:

```text
- duplicate department same campus.
- department not found.
- scope Staff Leader campus.
- updated_at/updated_by persist.
- DB rollback/no partial update on fail.
```

Các case đó đưa sang Integration Test.

### 11.3. Tên method Unit Test nên dùng

Dùng tên rõ nghĩa, không dùng số HTTP trong Unit Test.

Ví dụ:

```csharp
ValidCommand_NoErrors()
DepartmentId_Zero_HasError()
Name_Empty_HasError()
Name_Whitespace_HasError()
Name_TooLong_HasError()
Name_MaxLength_NoError()
DepartmentType_Invalid_HasError()
DepartmentType_Ic_NoError()
DepartmentType_General_NoError()
```

Tên test không được hứa điều không assert.

---

## 12. Integration Test cần tạo

Integration Test đặt trong:

```text
tests/PEMS.IntegrationTests/Departments/UpdateDepartment/
```

File gợi ý:

```text
UpdateDepartmentApiTests.cs
```

Ưu tiên dùng lại style đã chốt từ các UC trước:

```text
- xUnit.
- IClassFixture<PemsWebApplicationFactory>.
- IAsyncLifetime cleanup sau mỗi test.
- CreateClientAsAsync(EffectiveRole.StaffLeader/Staff/Admin/Ho/DepartmentLeader/Visitor).
- TestAuthHandler headers.
- DatabaseResetHelper cleanup theo prefix riêng của Update Department.
- Semantic test names ngắn gọn, không dùng số HTTP hoặc Returns400/Returns403/Returns200.
- Assembly-level DisableTestParallelization cho PEMS.IntegrationTests nếu chưa có.
```

### 12.1. Setup dữ liệu cho Update Department tests

Update Department cần có dữ liệu nền an toàn:

```text
- Active campus.
- Active IC department để Staff Leader test thuộc về.
- Active Staff Leader test user thuộc campus đó.
- Target department test thuộc cùng campus với Staff Leader.
- Prefix department name riêng cho Update Department.
```

Không update/delete department seed thật nếu có thể tránh.

Với department name test:

```text
[IT-UC102-UPDATE-DEPARTMENT] old <guid>
[IT-UC102-UPDATE-DEPARTMENT] new <guid>
[IT-UC102-UPDATE-DEPARTMENT] duplicate <guid>
```

Cleanup chỉ xóa department có name prefix riêng và không có user phụ thuộc.

Nếu test department có thể bị FK bởi users/head_user/task sau khi tạo, cleanup phải xóa theo thứ tự an toàn hoặc chỉ tạo department không gắn user/head.

### 12.2. Snapshot rule cho DoesNotModify

Với các test fail mà tên có `DoesNotModify`, phải snapshot record cũ trước khi gọi API và assert đủ field quan trọng sau request:

```text
- campus_id giữ nguyên
- name giữ nguyên
- department_type giữ nguyên
- head_user_id giữ nguyên
- status giữ nguyên
- created_at giữ nguyên
- created_by giữ nguyên
- updated_at giữ nguyên/null nếu seed ban đầu null
- updated_by giữ nguyên/null nếu seed ban đầu null
```

Với payload invalid, cố tình gửi các field khác khác với dữ liệu cũ để chứng minh backend không update partial khi validation fail.

Ví dụ:

```text
Seed cũ:
name = [IT-UC102-UPDATE-DEPARTMENT] old <guid>
department_type = GENERAL
status = ACTIVE

Request invalid:
name = ""
department_type = IC

Expected DB sau fail:
name vẫn là old
department_type vẫn là GENERAL
status vẫn là ACTIVE
updated_at/updated_by không đổi
```

### 12.3. Các case Integration Test tối thiểu

Điều chỉnh HTTP status/endpoint theo source thật.

#### Authentication / Authorization

```text
1. Anonymous_Unauthorized
   Không gắn auth headers -> update department -> 401 Unauthorized.

2. Staff_Forbidden
   STAFF + STAFF đã đăng nhập -> update department -> 403 Forbidden.

3. Ho_Forbidden
   HO đã đăng nhập -> update department -> 403 Forbidden.

4. Admin_Forbidden
   ADMIN đã đăng nhập -> update department -> 403 Forbidden.

5. DepartmentLeader_Forbidden
   DEPARTMENT + LEADER đã đăng nhập -> update department -> 403 Forbidden.

6. Visitor_Forbidden
   VISITOR đã đăng nhập -> update department -> 403 Forbidden.
```

Nếu muốn giảm số lượng test, tối thiểu phải có:

```text
Anonymous_Unauthorized
Staff_Forbidden
Ho_Forbidden hoặc Admin_Forbidden
StaffLeader_ValidPayload_UpdatesDepartment
```

Tuy nhiên vì tài liệu đang cảnh báo DepartmentsController thiếu authorization, nên nên giữ full matrix hoặc ít nhất đủ các role rủi ro cao.

#### Happy path / DB state

```text
7. StaffLeader_ValidPayload_UpdatesDepartment
   Seed target department cùng campus.
   Staff Leader update payload hợp lệ.
   Expect OK theo convention source.
   Reload DB: name/department_type/head_user_id nếu có đã đổi đúng.

8. StaffLeader_Update_KeepsStatus
   Chỉ viết nếu Update Department không nhận status.
   Seed status ACTIVE hoặc INACTIVE theo source khả năng xử lý.
   Update name/type.
   Reload DB: status giữ nguyên.

9. StaffLeader_ValidPayload_UpdatesAudit
   Nếu source/schema set audit.
   Reload DB: updated_by = staffLeaderUserId, updated_at nằm trong khoảng request.
   created_at/created_by giữ nguyên.

10. StaffLeader_NoChange_KeepsRecordUnchanged
    Optional, chỉ viết nếu source có behavior no-change rõ hoặc response có Changed=false.
    Test theo source thật: có ghi DB hay không ghi DB.
```

Nếu response DTO trả `departmentId`, assert response id khớp DB.

#### Validation / no partial update

```text
11. DepartmentId_Zero_BadRequest
    Nếu departmentId nằm trên route/body và validator chặn id <= 0.
    Expect BadRequest qua API pipeline thật.

12. EmptyName_DoesNotModify
    Gửi name = "" và field khác đổi so với DB cũ.
    Expect BadRequest.
    Reload DB: target department giữ nguyên đủ field quan trọng.

13. WhitespaceName_DoesNotModify
    Gửi name chỉ whitespace nếu validator có rule trim/not empty.
    Expect BadRequest.
    DB không đổi.

14. InvalidDepartmentType_DoesNotModify
    Gửi departmentType invalid và name mới hợp lệ.
    Expect BadRequest.
    DB không đổi.
```

Với invalid payload, nên cố tình gửi các field khác hợp lệ/khác dữ liệu cũ để chứng minh backend không update partial.

#### Existence / scope

```text
15. NonExistingDepartment_NotFound
    Staff Leader update departmentId không tồn tại.
    Expect NotFound theo source convention.

16. StaffLeader_OtherCampus_ForbiddenOrNotFound
    Chỉ viết nếu có thể seed department campus khác an toàn.
    Staff Leader campus A cố update department campus B.
    Expect Forbidden/NotFound/BadRequest theo source convention.
    DB target không đổi.
```

Nếu source hiện tại không có campus scope check, test này có thể fail. Không sửa expected để pass; report production issue.

#### Duplicate rule

```text
17. DuplicateNameSameCampus_DoesNotModify
    Seed department A với name N trong campus X.
    Seed department B với name M trong cùng campus X.
    Staff Leader update B thành name N.
    Expect Conflict/BadRequest theo source convention.
    Reload DB: B giữ nguyên đủ field quan trọng.
    DB vẫn chỉ có một department với name N trong campus X.

18. SameNameSelf_UpdatesRecord
    Seed department A với name N.
    Staff Leader update A giữ name N hoặc trim/case tương đương nếu source normalize, nhưng đổi field khác hợp lệ.
    Expect OK.
    Mục tiêu: duplicate check exclude chính department đang update.
```

#### Input normalization / sanitize

```text
19. Name_TrimmedBeforeSave
    Chỉ viết nếu source thật trim name trước khi lưu.
    Gửi name có whitespace đầu/cuối.
    Reload DB: name đã trim.

20. ScriptTag_SanitizedOrRejected
    Chỉ viết nếu source có sanitize/reject rule cho department name.
    Nếu source sanitize: expect OK và DB không chứa raw <script>.
    Nếu source validator reject: expect BadRequest và DB không đổi.
```

Không tự bịa sanitize/trim nếu source không có.

#### Head user / lead field nếu source có

Chỉ viết nếu request DTO thật có `headUserId` hoặc field tương đương:

```text
21. NonExistingHeadUser_DoesNotModify
    Gửi headUserId không tồn tại.
    Expect NotFound/BadRequest.
    DB không đổi.

22. HeadUserWrongCampus_DoesNotModify
    Gửi headUserId thuộc campus khác nếu source phải enforce campus scope.
    Expect Forbidden/BadRequest.
    DB không đổi.

23. ValidHeadUser_UpdatesHead
    Gửi headUserId hợp lệ nếu source cho phép update head_user_id.
    Expect OK.
    Reload DB: head_user_id đổi đúng.
```

Nếu source không có headUserId trong Update Department, không tạo các test này.

### 12.4. Bộ tên method Integration Test gợi ý

Dùng tên semantic, ngắn gọn, không dùng số HTTP code trong tên test.

Bộ tên gợi ý:

```csharp
Anonymous_Unauthorized()
Staff_Forbidden()
Ho_Forbidden()
Admin_Forbidden()
DepartmentLeader_Forbidden()
Visitor_Forbidden()

StaffLeader_ValidPayload_UpdatesDepartment()
StaffLeader_Update_KeepsStatus()
StaffLeader_ValidPayload_UpdatesAudit()
StaffLeader_NoChange_KeepsRecordUnchanged()

DepartmentId_Zero_BadRequest()
EmptyName_DoesNotModify()
WhitespaceName_DoesNotModify()
InvalidDepartmentType_DoesNotModify()
NonExistingDepartment_NotFound()
StaffLeader_OtherCampus_Forbidden()
DuplicateNameSameCampus_DoesNotModify()
SameNameSelf_UpdatesRecord()
Name_TrimmedBeforeSave()
ScriptTag_SanitizedOrRejected()
```

Không bắt buộc viết toàn bộ nếu source hiện tại không hỗ trợ hoặc case thuộc UC khác. Nếu bỏ case nào, phải giải thích lý do trong report.

### 12.5. Không tạo test thừa

Không tạo các test sau trong UC-102 nếu đã/đang thuộc UC khác:

```text
AddNewDepartment_CreatesDepartment
ManageDepartmentStatus_ChangesActiveInactive
SearchAndFilterDepartments_ReturnsOnlyMatching
ViewDepartmentList_ReturnsDepartments
ViewDepartmentDetails_ReturnsDepartment
AddDepartmentPersonnel_AddsUserToDepartment
ReassignDepartmentLead_ChangesLead
```

Không test frontend UI trong backend Integration Test.

---

## 13. Quy tắc đặt tên test case

Tên test phải ngắn gọn nhưng không được mơ hồ hoặc hứa quá nội dung assert.

```text
- Tên test phải nói đúng hành vi chính của test.
- Không dùng số HTTP code trong tên test, ví dụ 200/400/401/403/404/409.
- Có thể dùng tên HTTP outcome như Unauthorized, Forbidden, BadRequest, NotFound, Conflict nếu đó là hành vi chính cần test.
- Không đặt tên kiểu chung chung Returns400/Returns403/Returns200.
- Không dùng tên quá dài chỉ để liệt kê toàn bộ assert.
- Không dùng DoesNotModify nếu test không thật sự reload DB và kiểm tra record cũ không đổi.
- Không dùng UpdatesAudit nếu test không thật sự assert updated_at/updated_by.
- Không dùng KeepsStatus nếu test không thật sự assert status giữ nguyên.
- Không dùng SameNameSelf_UpdatesRecord nếu không thật sự chứng minh duplicate check exclude chính record.
- Với authorization test, tên có thể ngắn vì outcome chính là Unauthorized/Forbidden.
- Với business test, ưu tiên tên theo nghiệp vụ thay vì chỉ theo HTTP status.
```

Ví dụ đúng:

```text
StaffLeader_ValidPayload_UpdatesDepartment
EmptyName_DoesNotModify
DuplicateNameSameCampus_DoesNotModify
SameNameSelf_UpdatesRecord
StaffLeader_Update_KeepsStatus
StaffLeader_ValidPayload_UpdatesAudit
```

Ví dụ sai:

```text
UpdateDepartment_WhenStaffLeaderSendsValidPayload_Returns200AndUpdatesNameTypeAuditAndKeepsStatusAndCreatedBy
Returns200
InvalidPayload_Returns400
EmptyName_DoesNotModify          // sai nếu chỉ assert status code, không assert DB unchanged
StaffLeader_Update_KeepsStatus   // sai nếu không assert status
```

---

## 14. Commands được phép chạy

### 14.1. Luôn được chạy nếu không động DB

```bash
dotnet build
dotnet test tests/PEMS.UnitTests/PEMS.UnitTests.csproj
```

Nếu đường dẫn project khác, dùng đường dẫn thật trong source.

### 14.2. Chỉ chạy Integration Test sau khi đạt DB safety gate

Chỉ được chạy:

```bash
dotnet test tests/PEMS.IntegrationTests/PEMS.IntegrationTests.csproj
```

khi đã thỏa mãn:

```text
- Database test riêng đã xác định rõ là pems_test hoặc tên test DB khác.
- Connection string Testing không trỏ tới pems_db/dev DB.
- Không dùng appsettings.Development.json.
- pems_test đã được kiểm tra có đủ dữ liệu nền hoặc helper seed test đã được duyệt.
- SQL script đã được scan an toàn nếu cần import.
- Người dùng đã xác nhận cho phép chạy Integration Test có DB.
```

Nếu chưa đủ điều kiện, không chạy Integration Test. Hãy báo rõ:

```text
Integration Test code đã tạo/cập nhật nhưng chưa chạy vì chưa có xác nhận an toàn database hoặc pems_test chưa đủ dữ liệu nền.
```

Sau khi được phép chạy, nên chạy:

```bash
dotnet test tests/PEMS.IntegrationTests/PEMS.IntegrationTests.csproj --filter "FullyQualifiedName~UpdateDepartmentApiTests"
dotnet test tests/PEMS.IntegrationTests/PEMS.IntegrationTests.csproj --filter "FullyQualifiedName~AddDepartmentApiTests"
dotnet test tests/PEMS.IntegrationTests/PEMS.IntegrationTests.csproj
```

Nếu đã có nhiều Department tests khác, chạy thêm các class liên quan để bắt lỗi tương tác/race condition:

```bash
dotnet test tests/PEMS.IntegrationTests/PEMS.IntegrationTests.csproj --filter "FullyQualifiedName~Departments"
```

---

## 15. Output/report sau khi làm

Sau khi hoàn thành, báo cáo bằng tiếng Việt theo format:

```md
# Báo cáo tạo test UC-102 Update Department

## 1. Tóm tắt
[Đã tạo/sửa những gì]

## 2. File đã tạo/sửa
| Loại | File | Mục đích |
|---|---|---|
| Unit Test | ... | ... |
| Integration Test | ... | ... |
| Test Infrastructure | ... | ... |
| Docs/Config mẫu | ... | ... |

## 3. Mapping UC/source đã xác nhận
| Nội dung | Kết quả |
|---|---|
| UC ID trong yêu cầu | UC-102 Update Department |
| UC ID trong source/docs nếu khác | ... |
| Endpoint update thật | ... |
| HTTP method thật | PUT/PATCH/POST... |
| Request DTO thật | ... |
| departmentId nằm ở đâu | route/body/... |
| Response DTO thật | ... |
| Actor hợp lệ | ... |
| Có phân biệt Add/Update/Status/Personnel không? | ... |
| Field update thật | name/departmentType/headUserId/... |
| Status có được update không? | Có/Không + evidence |
| Scope campus Staff Leader | Có/Không + evidence |
| Duplicate rule | ... |
| Audit behavior | ... |

## 4. Unit Test đã tạo
[Liệt kê case hoặc giải thích vì sao không tạo Unit Test riêng]

## 5. Integration Test đã tạo
[Liệt kê case]

## 6. Kiểm tra pems_test
| Mục | Kết quả |
|---|---|
| SELECT DATABASE() | ... |
| Có active campus không? | Có/Không |
| Có active IC department không? | Có/Không |
| Có Staff Leader test hợp lệ không? | Có/Không |
| Có target department test/prefix cleanup không? | Có/Không |
| Có dữ liệu cũ prefix Update Department sót lại không? | Có/Không |
| Có phải seed thêm dữ liệu nền không? | Có/Không + đã xin phép chưa |

## 7. Kiểm tra an toàn database
| Mục | Kết quả |
|---|---|
| Database test dự kiến | pems_test hoặc tên thật |
| Có dùng pems_db không? | Không |
| Có dùng appsettings.Development.json không? | Không |
| Có đọc/copy secret thật không? | Không |
| Có scan SQL trước khi import không? | Có/Chưa cần |
| Có chạy lệnh ghi DB chưa? | Có/Không + giải thích |

## 8. Kết quả chạy lệnh
```text
[dotnet build result]
[dotnet test UnitTests result]
[IntegrationTests result nếu đã được phép chạy]
```

## 9. Test fail nếu có
| Test | Expected | Actual | Nhận định |
|---|---|---|---|
| ... | ... | ... | ... |

## 10. Production code issue nếu có
[Chỉ báo cáo, không tự sửa nếu chưa được duyệt]

## 11. Việc cần người dùng xác nhận thêm
[Nếu còn]
```

Không được báo “hoàn thành” nếu chưa build/test hoặc chưa nói rõ lý do không chạy được.

---

## 16. Definition of Done

Task chỉ hoàn thành khi đạt đủ các điều kiện sau:

```text
- Test code thật đã được tạo/cập nhật.
- Unit Test nằm đúng tests/PEMS.UnitTests/Departments/UpdateDepartment/ nếu có.
- Integration Test nằm đúng tests/PEMS.IntegrationTests/Departments/UpdateDepartment/.
- Không trộn Unit Test và Integration Test.
- Không dùng database dev/thật.
- Không import SQL fresh-create gốc trực tiếp.
- Không đọc/copy/in secret thật.
- Không tự ý sửa production code để ép test pass.
- Unit Test đã chạy hoặc báo rõ lý do chưa chạy.
- Integration Test chỉ chạy khi đạt DB safety gate và có xác nhận.
- pems_test đã được kiểm tra đủ active campus, IC department, Staff Leader và target department test.
- Test names rõ nghĩa, không dùng số HTTP nếu có thể.
- Update Department test xác nhận endpoint thật, không bịa route.
- Không duplicate nguyên bộ Add Department / Search Department tests.
- Authorization test bao phủ Anonymous và các role không phải Staff Leader đại diện.
- Valid update test reload DB và assert field được update đúng.
- Failure tests reload DB và assert target department không đổi nếu tên có DoesNotModify.
- Invalid payload cố tình đổi field khác để bắt update partial.
- Duplicate same campus test không assert mơ hồ; phải chứng minh target không đổi và không tạo duplicate.
- Status giữ nguyên nếu Update Department không quản lý status.
- Audit update test assert updated_at/updated_by thật nếu tên có UpdatesAudit.
- Integration Test assembly đã tắt parallelization hoặc report rõ lý do chưa thêm.
- Update Department không dùng chung cleanup prefix với Add/Search/View/Status Department.
- DatabaseResetHelper seed/cleanup nhận prefix riêng hoặc có helper riêng tương đương.
- Chạy riêng UpdateDepartmentApiTests và chạy toàn bộ IntegrationTests nếu được phép để phát hiện race condition.
- Báo cáo pass/fail rõ ràng.
```

---

## 17. Nhắc lại các lỗi nghiêm cấm

Không được lặp lại các lỗi sau:

```text
- Tự tạo endpoint Update Department giả khi source không có.
- Dùng endpoint Add Department để test Update Department.
- Test Manage Department Status trong UC-102 nếu status tách riêng.
- Test Add Department Personnel/Reassign Lead trong UC-102 nếu source tách UC riêng.
- Copy nguyên AddDepartmentApiTests sang UpdateDepartmentApiTests và đổi tên class.
- Assert exact DB count khi DB có seed khác và không cô lập bằng prefix/token.
- Dùng chung DepartmentNamePrefix với Add/Search/View/Status Department.
- Cleanup theo prefix quá rộng khiến test class này xóa dữ liệu test class khác.
- Update/delete department seed thật làm ảnh hưởng dữ liệu nền pems_test.
- Để xUnit chạy song song các Integration Test class dùng chung pems_test.
- Thấy NotFound/DbUpdateConcurrencyException khi chạy toàn bộ test rồi vội sửa production code trước khi kiểm tra race condition test infrastructure.
- Chạy SQL fresh-create gốc khi chưa kiểm tra có USE pems_db.
- Tin rằng `mysql pems_test < script.sql` chắc chắn chỉ tác động pems_test.
- Dùng appsettings.Development.json cho Integration Test.
- Đọc/copy secret thật sang file test.
- Sửa production code để test pass mà chưa được duyệt.
- Đặt tên test có DoesNotModify nhưng không assert DB unchanged.
- Đặt tên UpdatesAudit nhưng không assert audit fields.
- Đặt tên KeepsStatus nhưng không assert status.
```

Nếu không chắc chắn, phải dừng lại và hỏi người dùng.
