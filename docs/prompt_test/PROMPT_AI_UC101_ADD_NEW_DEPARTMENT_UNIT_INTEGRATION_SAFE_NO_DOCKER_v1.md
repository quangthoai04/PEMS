# PROMPT AI — TẠO TEST CODE THẬT CHO UC-101 ADD NEW DEPARTMENT (UNIT TEST + INTEGRATION TEST, KHÔNG DOCKER, AN TOÀN DATABASE) — v1

> File này dùng để đưa cho AI Agent/Code Agent khi cần tạo hoặc cập nhật test tự động cho chức năng **Add New Department** trong dự án PEMS.
>
> Use case này thuộc nhóm **Department Structure Management / Department Management**. Actor nghiệp vụ hiện tại theo ma trận quyền là **Staff Leader**.
>
> Mục tiêu: **tạo test code thật, chạy được, đúng nghiệp vụ Add New Department, kiểm tra trước `pems_test` có đủ dữ liệu nền để chạy Integration Test hay chưa, và tuyệt đối không làm hỏng `pems_db` hoặc bất kỳ database dev/thật nào**.

---

## 0. Bối cảnh và kinh nghiệm phải kế thừa

Prompt này kế thừa chuẩn đã chốt từ các prompt/test **Create FAQ**, **Update FAQ**, **UC-62 View List FAQ** và **UC-66 Search FAQ**:

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
- Nếu lỗi xuất hiện khi chạy toàn bộ IntegrationTests, phải kiểm tra race condition/test cleanup trước khi nghi production code.
```

Bài học đặc biệt từ lỗi race condition giữa các FAQ test:

```text
Không dùng chung một hằng số prefix cleanup cho nhiều use case.
Không cleanup bằng prefix rộng như [IT-DEPARTMENT], [TEST], [IT-UC101] nếu prefix đó có thể bị use case khác dùng lại.
Không dùng prefix overlap nhau, ví dụ [IT-DEPT] và [IT-DEPT-ADD].
Mỗi test class phải có prefix riêng, đủ rõ nghĩa, không trùng, không bao phủ prefix khác.
```

Prefix gợi ý cho use case này:

```text
[IT-UC101-ADD-DEPARTMENT]
```

Nếu source/docs hiện tại dùng UC ID khác cho Add New Department, vẫn giữ tên nghiệp vụ trong prefix để tránh nhầm:

```text
[IT-ADD-DEPARTMENT]
```

---

## 0.1. Lưu ý quan trọng về UC ID

Người dùng đang yêu cầu use case **Add New Department**.

Các tài liệu PEMS hiện có thể lệch UC ID:

```text
- USE_CASE_LIST.md / PERMISSION_MATRIX.md mới: UC-101 Add New Department.
- Một số PROJECT_OVERVIEW cũ: UC-98 Add New Department.
- RTW template có thể ghi UC-99 Add New Department.
- Report 3.1 UCS có block UC-101 Add New Department.
```

Quy tắc:

```text
- Ưu tiên tên nghiệp vụ ổn định: Add New Department.
- Ưu tiên USE_CASE_LIST.md/PERMISSION_MATRIX.md mới nhất nếu cần UC ID cho report.
- Nếu source/docs có mâu thuẫn, báo rõ trong report.
- Không hardcode UC ID vào logic test nếu source không dùng.
- Tên folder/test nên dùng tên nghiệp vụ ổn định: Departments/AddDepartment hoặc Departments/AddNewDepartment.
```

Không tự đổi sang Update Department, Search Department, View Department List hoặc Add Department Personnel.

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

Tạo test tự động cho chức năng **Staff Leader thêm mới Department**.

Test cần gồm 2 nhóm:

```text
1. Unit Test
   Chỉ kiểm tra logic nhỏ, chủ yếu là command validator hoặc helper thuần nếu có.
   Không gọi API thật.
   Không dùng database thật.

2. Integration Test
   Kiểm tra Add Department chạy qua nhiều layer thật:
   API + Authentication/Authorization giả + Controller + MediatR + Validator + Handler + database test riêng.
```

Sau khi hoàn thành, team phải biết rõ:

```text
- Add New Department đang dùng endpoint nào trong source thật.
- Actor hợp lệ thật là ai.
- Request DTO thật gồm các field nào.
- Schema departments thật có field nào và không có field nào.
- pems_test có đủ active campus, IC department và Staff Leader test để chạy chưa.
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
Existing tests hiện tại, đặc biệt Account/Department/Faq tests đã làm xong
SQL fresh-create mới nhất trong docs/database/scripts/
```

### 2.2. Source code bắt buộc kiểm tra

AI Agent phải tự search đúng file trong project, không được bịa path.

Tối thiểu cần kiểm tra:

```text
Backend controller liên quan department: DepartmentsController hoặc controller tương đương
Add Department Command/Request DTO
Add Department CommandHandler
Add Department CommandValidator
Add Department Response DTO nếu có
Department entity
EF Configuration của departments
ApplicationDbContext
Constants/Enums liên quan department_type/status
Authorization/Role check liên quan Department Management
Existing Unit Tests: tests/PEMS.UnitTests/Departments/... nếu có
Existing Integration Tests: tests/PEMS.IntegrationTests/Departments/... nếu có
Existing test infrastructure: PemsWebApplicationFactory, TestAuthHandler, DatabaseResetHelper
SQL fresh-create mới nhất trong docs/database/scripts/
```

Nếu tên class/path trong project khác ví dụ trong file này, dùng tên thật trong source và ghi rõ trong report.

### 2.3. Source-first rule

Không được tự bịa:

```text
- endpoint add department
- request body field
- response DTO
- department status/type enum
- actor được phép
- scope Staff Leader theo campus
- duplicate rule
- audit behavior
- SQL table/field
```

Nếu tài liệu, code, SQL hoặc comment cũ mâu thuẫn nhau, ưu tiên theo thứ tự:

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

Riêng authorization/security:  tài liệu/policy nói rõ chỉ Staff Leader được phép, **không được sửa expected test thành OK cho anonymous/role sai**. Viết test theo policy đúng để phát hiện lỗi bảo mật, báo test fail là production security issue, không tự sửa production code khi chưa được duyệt.

---

## 3. Nghiệp vụ Add New Department phải giữ đúng

### 3.1. Actor hợp lệ

Theo Permission Matrix hiện tại:

```text
Chỉ Staff Leader được Add New Department.
```

Backend phải tự kiểm tra quyền. Frontend ẩn/hiện nút không đủ để bảo mật.

Các role khác gọi API trực tiếp phải bị chặn:

```text
Không đăng nhập -> 401 Unauthorized.
Đăng nhập nhưng không phải Staff Leader -> 403 Forbidden.
Staff Leader hợp lệ -> được tạo department nếu dữ liệu hợp lệ và trong scope hợp lệ.
```

Không mặc định Admin/HO có toàn quyền. Nếu nghiệp vụ hiện tại là Staff Leader-only thì Admin/HO phải bị 403.

### 3.2. Cảnh báo security đã biết về DepartmentsController

Tài liệu implementation hiện có cảnh báo: `DepartmentsController` có thể thiếu `[Authorize]`/role guard trong code hiện tại.

Yêu cầu cho AI Agent:

```text
- Phải kiểm tra source thật xem DepartmentsController/AddDepartment endpoint đã có authorization chưa.
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
POST /api/departments
POST /api/departments/add
POST /api/departments/create
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
department_type: IC hoặc GENERAL
status: ACTIVE hoặc INACTIVE, default ACTIVE
```

Không tự bịa field nếu schema/source không có, đặc biệt:

```text
description
code
shortName
email
phone
languageCode
```

Nếu Report 3.1 UCS nói form có `description` nhưng SQL/source hiện tại không có cột này, ghi nhận là **spec/schema mismatch**. Không sửa SQL/schema, không test field đó trừ khi production source thật đã có mapping hợp lệ.

### 3.5. Department type hợp lệ

Department chỉ có 2 loại:

```text
IC
GENERAL
```

Không dùng giá trị cũ/khác:

```text
INTERNATIONAL
ACADEMIC
ADMINISTRATIVE
DEPT
```

### 3.6. Campus và scope

Add New Department phải gắn department với campus hợp lệ.

Yêu cầu cần xác nhận từ source:

```text
- Staff Leader có được chọn campus trong request không?
- Hay backend luôn dùng currentUser.primary_campus_id?
- Nếu request có campusId, backend có chặn Staff Leader tạo department ngoài campus của mình không?
- Target campus phải ACTIVE không?
```

Theo nghiệp vụ an toàn, Staff Leader chỉ nên tạo department trong campus thuộc scope của mình. Nếu source không enforce scope, test nên phát hiện và report production issue.

### 3.7. Duplicate department name per campus

Không cho tạo 2 department cùng `name` trong cùng `campus_id`.

Quy tắc theo SQL:

```text
UNIQUE (campus_id, name)
```

Yêu cầu test:

```text
- Duplicate cùng campus -> Conflict/BadRequest theo convention source, và không tạo record thứ hai.
- Cùng tên khác campus có thể hợp lệ nếu source cho phép Staff Leader tạo theo campus khác hoặc helper seed trực tiếp được dùng để chuẩn bị dữ liệu. Nếu Staff Leader scope chỉ cho một campus, không ép test same-name-different-campus ở API level.
```

Nếu source thực hiện trim/case-insensitive duplicate ở handler, test theo source thật. Nếu SQL unique chỉ case/collation theo DB, không tự bịa behavior.

### 3.8. Default status và audit

Khi tạo mới thành công:

```text
status phải là ACTIVE nếu source/schema default như vậy.
created_at phải được set theo convention hiện tại.
created_by phải là Staff Leader user id nếu handler set audit.
updated_at/updated_by thường nên null khi mới tạo, trừ khi source convention khác.
```

Không tự ép audit nếu source không expose hoặc không set. Nhưng nếu schema/source có audit, nên test bằng Integration Test.

### 3.9. Read/write side effect

Add New Department là write use case.

Khi request fail:

```text
- Không được tạo department rác.
- Không được tạo duplicate.
- Không được tạo department ở sai campus.
```

Nếu tên test có `DoesNotPersist`, phải kiểm tra DB thật để chứng minh không có record mới với prefix/token.

---

## 4. Bắt buộc kiểm tra `pems_test` trước khi chạy Integration Test

Người dùng yêu cầu riêng: **kiểm tra cả `pems_test` xem đủ dữ liệu test chưa**.

### 4.1. Mục tiêu kiểm tra

Trước khi chạy Integration Test Add Department, AI Agent phải xác nhận `pems_test` có đủ dữ liệu nền:

```text
1. Database hiện tại đúng là pems_test, không phải pems_db.
2. Có ít nhất 1 campus ACTIVE để Staff Leader thuộc về.
3. Có ít nhất 1 department_type = IC, status ACTIVE trong campus đó để Staff Leader test có department_id hợp lệ.
4. Có hoặc tạo được Staff Leader test: role_code = STAFF, sub_role = LEADER, status ACTIVE, primary_campus_id hợp lệ, department_id thuộc IC department cùng campus.
5. Không có dữ liệu test cũ với prefix Add Department còn sót lại, hoặc cleanup được bằng prefix riêng.
6. Không dùng production/dev seed thật làm target bị sửa/xóa nếu có thể tránh.
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

Kiểm tra dữ liệu test Add Department còn sót:

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
- Không dùng dữ liệu thật làm đối tượng bị delete/update.
- Tên department test phải có prefix riêng.
- Staff Leader test phải có role_code STAFF + sub_role LEADER.
- Staff Leader phải có primary_campus_id.
- Staff Leader phải có department_id thuộc IC department cùng campus.
- Cleanup không được xóa campus/department/user seed thật.
```

Ví dụ hướng thiết kế:

```csharp
public const string AddDepartmentNamePrefix = "[IT-UC101-ADD-DEPARTMENT] ";

EnsureActiveCampusAsync(db)
EnsureIcDepartmentForCampusAsync(db, campusId)
EnsureTestUserAsync(db, EffectiveRole.StaffLeader)
CreateTestDepartmentAsync(db, prefix, nameSuffix, campusId, departmentType, status, createdBy)
DeleteTestDepartmentsAsync(db, prefix)
```

Không dùng chung prefix với Update Department / View Department List / Search Departments / Add Department Personnel.

---

## 5. Phân biệt rõ Unit Test và Integration Test

### 5.1. Unit Test phù hợp với Add New Department

Unit Test chỉ kiểm tra logic nhỏ, cô lập.

Ưu tiên Unit Test cho:

```text
- Command validator.
- Required field.
- CampusId/Id phải > 0 nếu request có campusId.
- Department name null/empty/whitespace.
- Department name max length theo source/schema, ví dụ 150 nếu validator áp dụng.
- DepartmentType null/empty/invalid.
- DepartmentType IC/GENERAL hợp lệ.
- Request hợp lệ đầy đủ -> no errors.
```

Không ép Unit Test cho logic phụ thuộc DB như:

```text
- campus tồn tại hay không.
- campus ACTIVE hay INACTIVE.
- duplicate department name trong cùng campus.
- Staff Leader đúng campus hay không.
- record có persist DB hay không.
- audit created_at/created_by.
```

Các case đó đưa sang Integration Test.

### 5.2. Integration Test phù hợp với Add New Department

Integration Test kiểm tra nhiều phần chạy cùng nhau:

```text
HTTP request -> Auth/TestAuthHandler -> Controller -> MediatR -> Validator -> Handler -> DB test
```

Integration Test phải chứng minh:

```text
- Staff Leader được tạo Department.
- Role không có quyền bị chặn.
- Payload hợp lệ tạo record đúng trong DB.
- Payload invalid không tạo record rác.
- Duplicate same campus bị chặn.
- Campus/scope rule được enforce nếu source có.
- Audit/status đúng nếu source/schema hỗ trợ.
```

---

## 6. Quy ước tổ chức thư mục test

### 6.1. Unit Test folder

Unit Test đặt trong:

```text
tests/PEMS.UnitTests/Departments/AddDepartment/
```

Hoặc nếu source dùng tên `AddNewDepartment`, dùng:

```text
tests/PEMS.UnitTests/Departments/AddNewDepartment/
```

File gợi ý:

```text
AddDepartmentCommandValidatorTests.cs
AddNewDepartmentCommandValidatorTests.cs
CreateDepartmentCommandValidatorTests.cs
```

Dùng đúng tên command thật trong source.

### 6.2. Integration Test folder

Integration Test đặt trong:

```text
tests/PEMS.IntegrationTests/Departments/AddDepartment/
```

Hoặc:

```text
tests/PEMS.IntegrationTests/Departments/AddNewDepartment/
```

File gợi ý:

```text
AddDepartmentApiTests.cs
AddNewDepartmentApiTests.cs
CreateDepartmentApiTests.cs
```

Dùng đúng tên nghiệp vụ/folder ổn định, không đặt lẫn trong FAQ tests.

### 6.3. Helper dùng chung và prefix dữ liệu test

Ưu tiên dùng lại:

```text
tests/PEMS.IntegrationTests/TestInfrastructure/PemsWebApplicationFactory.cs
tests/PEMS.IntegrationTests/TestInfrastructure/TestAuthHandler.cs
tests/PEMS.IntegrationTests/TestInfrastructure/DatabaseResetHelper.cs
```

Nếu cần bổ sung helper seed Department cho Add Department, bổ sung vào `DatabaseResetHelper` hoặc helper chung tương đương. Không copy helper lặp lại trong từng file test.

Helper nên hỗ trợ:

```text
- Tạo department test với prefix riêng.
- Tạo department test với campus_id, department_type, status.
- Trả về departmentId.
- Cleanup department theo đúng prefix được truyền vào.
- Không hardcode DepartmentNamePrefix dùng chung cho mọi Department test.
- Không cleanup prefix của use case/test class khác.
```

Ví dụ:

```csharp
public const string AddDepartmentNamePrefix = "[IT-UC101-ADD-DEPARTMENT] ";

CreateTestDepartmentAsync(db, name, campusId, departmentType, status, createdBy)
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
tests/PEMS.UnitTests/Departments/AddDepartment/...
tests/PEMS.IntegrationTests/Departments/AddDepartment/...
tests/PEMS.UnitTests/TestHelpers/...
tests/PEMS.IntegrationTests/TestInfrastructure/...
docs/testing/...
backend/PEMS.Api/appsettings.Testing.example.json
file prompt/test documentation liên quan
```

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
- Cho phép tạo request giả với role STAFF+LEADER, STAFF+STAFF, HO, ADMIN, DEPARTMENT+LEADER, VISITOR.
- Cho phép gọi request không đăng nhập để test 401.
- Connection string phải trỏ tới database test riêng.
- Không dùng Docker.
- Không dùng Testcontainers.
- Không gọi service ngoài thật như Google SSO, SMTP, Google Drive.
```

Nếu project đã có test infrastructure tương đương, ưu tiên dùng lại và bổ sung thiếu sót, không tạo trùng.

Đặc biệt với Staff Leader test user:

```text
EffectiveRole.StaffLeader phải map đúng:
role_code = STAFF
sub_role = LEADER
primary_campus_id != null
department_id != null
department_type = IC
```

Nếu DB trigger yêu cầu STAFF phải có department_id, helper phải set department_id đúng. Không seed Staff Leader thiếu department_id.

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

Với Add New Department, tránh test thừa:

```text
Không test sâu Update Department.
Không test Manage Department Status.
Không test Search/View Department List.
Không test Add Department Personnel.
Không test tạo account Staff/Department nếu không cần cho Add Department.
Không duplicate toàn bộ Department Management test khác.
```

Chỉ test các case có giá trị cho Add Department:

```text
- Staff Leader-only authorization.
- Valid create.
- Required/invalid input.
- Duplicate same campus.
- Campus active/scope nếu source hỗ trợ.
- Default ACTIVE status.
- Audit created_by/created_at nếu source hỗ trợ.
- No partial/garbage insert on failure.
```

---

## 11. Unit Test cần tạo

Chỉ tạo Unit Test theo source thật hiện tại.

### 11.1. Validator tests

File gợi ý:

```text
tests/PEMS.UnitTests/Departments/AddDepartment/AddDepartmentCommandValidatorTests.cs
```

Dùng đúng tên command thật. Ví dụ source có thể gọi là:

```text
AddDepartmentCommandValidator
CreateDepartmentCommandValidator
AddNewDepartmentCommandValidator
```

Các case tối thiểu, điều chỉnh theo source thật:

```text
1. ValidCommand_NoErrors
   Payload hợp lệ -> không lỗi.

2. Name_Null_HasError
   Nếu name nullable/string? trong command.

3. Name_Empty_HasError
   name = "" -> lỗi.

4. Name_Whitespace_HasError
   name chỉ whitespace -> lỗi.

5. Name_TooLong_HasError
   Nếu validator/source có max length, theo schema gợi ý name VARCHAR(150).

6. Name_MaxLength_NoError
   Nếu validator có max length 150, đúng 150 ký tự phải hợp lệ.

7. CampusId_Zero_HasError
   Nếu request có campusId/id trên body/route.

8. CampusId_Negative_HasError
   Nếu source dùng signed int/long và có thể truyền âm.

9. DepartmentType_NullOrEmpty_HasError
   Nếu field departmentType bắt buộc.

10. DepartmentType_Invalid_HasError
    Ví dụ "ACADEMIC", "INTERNATIONAL", "DEPT".

11. DepartmentType_Ic_NoError
    IC hợp lệ.

12. DepartmentType_General_NoError
    GENERAL hợp lệ.
```

Không test `description` nếu source/schema không có field này.

Không test duplicate/campus exists ở Unit Test nếu phải dùng DB.

### 11.2. Handler Unit Test

Chỉ viết Handler Unit Test nếu dependency có thể mock/fake rõ ràng.

Có thể viết nếu source có helper thuần cho:

```text
- trim name trước khi lưu.
- normalize department type.
- map response DTO thuần.
```

Không ép test các case sau ở Unit Test nếu phụ thuộc EF/database:

```text
- duplicate department same campus.
- campus not found/inactive.
- scope Staff Leader campus.
- created_at/created_by persist.
- DB rollback/no persist on fail.
```

Các case đó đưa sang Integration Test.

### 11.3. Tên method Unit Test nên dùng

Dùng tên rõ nghĩa, không dùng số HTTP trong Unit Test.

Ví dụ:

```csharp
ValidCommand_NoErrors()
Name_Empty_HasError()
Name_Whitespace_HasError()
Name_TooLong_HasError()
Name_MaxLength_NoError()
CampusId_Zero_HasError()
DepartmentType_Invalid_HasError()
DepartmentType_Ic_NoError()
DepartmentType_General_NoError()
```

Tên test không được hứa điều không assert.

---

## 12. Integration Test cần tạo

Integration Test đặt trong:

```text
tests/PEMS.IntegrationTests/Departments/AddDepartment/
```

File gợi ý:

```text
AddDepartmentApiTests.cs
```

Ưu tiên dùng lại style đã chốt từ các UC trước:

```text
- xUnit.
- IClassFixture<PemsWebApplicationFactory>.
- IAsyncLifetime cleanup sau mỗi test.
- CreateClientAsAsync(EffectiveRole.StaffLeader/Staff/Admin/Ho/DepartmentLeader/Visitor).
- TestAuthHandler headers.
- DatabaseResetHelper cleanup theo prefix riêng của Add Department.
- Semantic test names ngắn gọn, không dùng số HTTP hoặc Returns400/Returns403/Returns200.
- Assembly-level DisableTestParallelization cho PEMS.IntegrationTests nếu chưa có.
```

### 12.1. Setup dữ liệu cho Add Department tests

Add Department cần có dữ liệu nền an toàn:

```text
- Active campus.
- Active IC department để Staff Leader test thuộc về.
- Active Staff Leader test user thuộc campus đó.
- Prefix department name riêng cho Add Department.
```

Không phụ thuộc vào department seed thật làm record bị update/delete nếu có thể tránh.

Với department name test:

```text
[IT-UC101-ADD-DEPARTMENT] valid <guid>
[IT-UC101-ADD-DEPARTMENT] duplicate <guid>
```

Cleanup chỉ xóa department có name prefix riêng và không có user phụ thuộc.

Nếu test department có thể bị FK bởi users/head_user/task sau khi tạo, cleanup phải xóa theo thứ tự an toàn hoặc chỉ tạo department không gắn user/head.

### 12.2. Các case Integration Test tối thiểu

Điều chỉnh HTTP status/endpoint theo source thật.

#### Authentication / Authorization

```text
1. Anonymous_Unauthorized
   Không gắn auth headers -> add department -> 401 Unauthorized.

2. Staff_Forbidden
   STAFF + STAFF đã đăng nhập -> add department -> 403 Forbidden.

3. Ho_Forbidden
   HO đã đăng nhập -> add department -> 403 Forbidden.

4. Admin_Forbidden
   ADMIN đã đăng nhập -> add department -> 403 Forbidden.

5. DepartmentLeader_Forbidden
   DEPARTMENT + LEADER đã đăng nhập -> add department -> 403 Forbidden.

6. Visitor_Forbidden
   VISITOR đã đăng nhập -> add department -> 403 Forbidden.
```

Nếu muốn giảm số lượng test, tối thiểu phải có:

```text
Anonymous_Unauthorized
Staff_Forbidden
Ho_Forbidden hoặc Admin_Forbidden
StaffLeader_ValidPayload_CreatesDepartment
```

Tuy nhiên vì tài liệu đang cảnh báo DepartmentsController thiếu authorization, nên nên giữ full matrix hoặc ít nhất đủ các role rủi ro cao.

#### Happy path / DB state

```text
7. StaffLeader_ValidPayload_CreatesDepartment
   Staff Leader tạo department hợp lệ.
   Expect OK/Created theo convention source.
   Reload DB: có department đúng campus/name/type/status.

8. StaffLeader_CreatesActiveDepartment
   Tạo department không truyền status nếu source không có status trong request.
   Reload DB: status = ACTIVE.

9. StaffLeader_ValidPayload_SetsCreateAudit
   Nếu source/schema set audit.
   Reload DB: created_by = staffLeaderUserId, created_at nằm trong khoảng request.
   updated_at/updated_by null nếu source convention như vậy.
```

Nếu response DTO trả `departmentId`, assert response id khớp DB.

#### Validation / no persist

```text
10. EmptyName_DoesNotPersist
    Gửi name = "".
    Expect BadRequest.
    Reload DB/query prefix/token: không có department rác được tạo.

11. WhitespaceName_DoesNotPersist
    Gửi name chỉ whitespace nếu validator có rule trim/not empty.
    Expect BadRequest.
    DB không persist.

12. InvalidDepartmentType_DoesNotPersist
    Gửi departmentType invalid.
    Expect BadRequest.
    DB không persist.

13. CampusId_Zero_BadRequest
    Nếu campusId nằm trong body/route.
    Expect BadRequest qua API pipeline thật.
```

Với invalid payload, nên cố tình gửi các field khác hợp lệ/khác dữ liệu cũ để chứng minh backend không tạo partial/garbage record.

#### Campus / scope / existence

```text
14. NonExistingCampus_NotFoundOrBadRequest
    Nếu request có campusId.
    Staff Leader gửi campusId không tồn tại.
    Expect NotFound/BadRequest theo source convention.
    DB không persist.

15. InactiveCampus_DoesNotPersist
    Chỉ viết nếu có thể seed/find inactive campus an toàn hoặc source có helper.
    Expect BadRequest/Conflict theo source.
    DB không persist.

16. StaffLeader_OtherCampus_ForbiddenOrBadRequest
    Nếu source phải enforce Staff Leader campus scope.
    Staff Leader campus A cố tạo department cho campus B.
    Expect Forbidden/BadRequest.
    DB không persist.
```

Nếu source hiện tại không có campus scope check, test này có thể fail. Không sửa expected để pass; report production issue.

#### Duplicate rule

```text
17. DuplicateNameSameCampus_DoesNotPersistSecondRecord
    Seed/create department A trong campus X với name N.
    Staff Leader tạo department B trong cùng campus X với name N.
    Expect Conflict/BadRequest theo source convention.
    DB chỉ có một department với name N trong campus X.

18. SameNameDifferentCampus_Allowed
    Chỉ viết nếu API/source cho phép chọn campus khác hoặc helper seed trực tiếp phù hợp và không vi phạm Staff Leader scope.
    Vì SQL unique là (campus_id, name), cùng name ở campus khác có thể hợp lệ về DB.
    Nếu Staff Leader chỉ được tạo trong campus của mình, không viết API test này.
```

#### Sanitize / input normalization

```text
19. ScriptTag_SanitizedBeforeSave
    Chỉ viết nếu source có sanitize cho department name hoặc textual fields.
    Nếu departments không có description và name không cho HTML, có thể chỉ test name saved không chứa raw <script> nếu source sanitize.

20. Name_TrimmedBeforeSave
    Chỉ viết nếu source thật trim name trước khi lưu.
    Gửi name có whitespace đầu/cuối.
    Reload DB: name đã trim.
```

Không tự bịa sanitize/trim nếu source không có.

### 12.3. Bộ tên method Integration Test gợi ý

Dùng tên semantic, ngắn gọn, không dùng số HTTP code trong tên test.

Bộ tên gợi ý:

```csharp
Anonymous_Unauthorized()
Staff_Forbidden()
Ho_Forbidden()
Admin_Forbidden()
DepartmentLeader_Forbidden()
Visitor_Forbidden()

StaffLeader_ValidPayload_CreatesDepartment()
StaffLeader_CreatesActiveDepartment()
StaffLeader_ValidPayload_SetsCreateAudit()

EmptyName_DoesNotPersist()
WhitespaceName_DoesNotPersist()
InvalidDepartmentType_DoesNotPersist()
CampusId_Zero_BadRequest()
NonExistingCampus_NotFound()
InactiveCampus_DoesNotPersist()
StaffLeader_OtherCampus_Forbidden()
DuplicateNameSameCampus_DoesNotPersistSecondRecord()
SameNameDifferentCampus_Allowed()
Name_TrimmedBeforeSave()
ScriptTag_SanitizedBeforeSave()
```

Không bắt buộc viết toàn bộ nếu source hiện tại không hỗ trợ hoặc case bị trùng/không xác định. Nếu bỏ case nào, phải giải thích lý do trong report.

### 12.4. Không tạo test thừa

Không tạo các test sau trong Add Department nếu không trực tiếp liên quan:

```text
ViewDepartmentList_ReturnsDepartments
SearchDepartment_ByKeyword
ManageDepartmentStatus_ChangesToInactive
UpdateDepartment_UpdatesName
AddDepartmentPersonnel_CreatesUser
RemovePersonnel_RemovesUser
ReassignDepartmentLead_ChangesLead
```

Những test đó thuộc UC khác.

---

## 13. Quy tắc đặt tên test case

Tên test phải ngắn gọn nhưng không được mơ hồ hoặc hứa quá nội dung assert.

```text
- Tên test phải nói đúng hành vi chính của test.
- Không dùng số HTTP code trong tên test, ví dụ 200/400/401/403/404/409.
- Có thể dùng tên HTTP outcome như Unauthorized, Forbidden, BadRequest, NotFound, Conflict nếu đó là hành vi chính cần test.
- Không đặt tên kiểu chung chung Returns400/Returns403/Returns200.
- Không dùng tên quá dài chỉ để liệt kê toàn bộ assert.
- Không dùng DoesNotPersist nếu test không thật sự query DB để chứng minh không có record mới.
- Không dùng SetsCreateAudit nếu test không assert created_at/created_by thật.
- Không dùng CreatesActiveDepartment nếu test không assert status ACTIVE thật trong DB.
- Không dùng OtherCampus_Forbidden nếu test không setup Staff Leader campus A và target campus B thật.
- Với authorization test, tên có thể ngắn vì outcome chính là Unauthorized/Forbidden.
- Với business test, ưu tiên tên theo nghiệp vụ thay vì chỉ theo HTTP status.
```

Ví dụ đúng:

```text
Anonymous_Unauthorized
Staff_Forbidden
StaffLeader_ValidPayload_CreatesDepartment
EmptyName_DoesNotPersist
DuplicateNameSameCampus_DoesNotPersistSecondRecord
StaffLeader_ValidPayload_SetsCreateAudit
```

Ví dụ sai:

```text
AddDepartment_WhenStaffLeaderSendsValidPayload_Returns200AndCreatesDepartmentAndSetsStatusAndAudit
Returns400
EmptyName_DoesNotPersist     // sai nếu chỉ assert BadRequest, không query DB
SetsCreateAudit              // sai nếu không assert audit fields thật
```

---

## 14. Commands được phép chạy

### 14.1. Luôn được chạy nếu không động DB

```bash
dotnet build
dotnet test tests/PEMS.UnitTests/PEMS.UnitTests.csproj
```

Nếu đường dẫn project khác, dùng đường dẫn thật trong source.

### 14.2. Được chạy read-only DB check sau khi xác nhận connection Testing

Chỉ chạy các query SELECT ở mục 4 nếu:

```text
- Connection string Testing đã xác định.
- Không dùng appsettings.Development.json.
- Có thể chứng minh SELECT đang chạy trên pems_test.
- Không in secret connection string.
```

### 14.3. Chỉ chạy Integration Test sau khi đạt DB safety gate

Chỉ được chạy:

```bash
dotnet test tests/PEMS.IntegrationTests/PEMS.IntegrationTests.csproj
```

khi đã thỏa mãn:

```text
- Database test riêng đã xác định rõ là pems_test hoặc tên test DB khác.
- Connection string Testing không trỏ tới pems_db/dev DB.
- pems_test đã có đủ dữ liệu nền hoặc người dùng đã duyệt helper seed test-only.
- Không dùng appsettings.Development.json.
- SQL script đã được scan an toàn nếu cần import.
- Người dùng đã xác nhận cho phép chạy Integration Test có DB write.
```

Nếu chưa đủ điều kiện, không chạy Integration Test. Hãy báo rõ:

```text
Integration Test code đã tạo/cập nhật nhưng chưa chạy vì chưa có xác nhận an toàn database hoặc pems_test thiếu dữ liệu nền.
```

Sau khi được phép chạy, nên chạy:

```bash
dotnet test tests/PEMS.IntegrationTests/PEMS.IntegrationTests.csproj --filter "FullyQualifiedName~AddDepartmentApiTests"
dotnet test tests/PEMS.IntegrationTests/PEMS.IntegrationTests.csproj --filter "FullyQualifiedName~Departments"
dotnet test tests/PEMS.IntegrationTests/PEMS.IntegrationTests.csproj
```

Mục tiêu là phát hiện cả lỗi trong test class mới và lỗi tương tác/race condition với các test khác.

---

## 15. Output/report sau khi làm

Sau khi hoàn thành, báo cáo bằng tiếng Việt theo format:

```md
# Báo cáo tạo test Add New Department

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
| UC ID trong yêu cầu | Add New Department |
| UC ID trong source/docs nếu khác | ... |
| Endpoint thật | ... |
| Request DTO thật | ... |
| Response DTO thật | ... |
| Actor hợp lệ | ... |
| Staff Leader scope theo campus | ... |
| Department fields thật | ... |
| Có description trong schema/source không? | Có/Không + giải thích |
| Duplicate rule | ... |
| Default status | ... |
| Audit behavior | ... |

## 4. Kiểm tra pems_test trước khi chạy Integration Test
| Mục | Kết quả |
|---|---|
| SELECT DATABASE() | ... |
| Có trỏ pems_db không? | Không |
| Active campus đủ chưa? | Có/Không |
| Active IC department đủ chưa? | Có/Không |
| Active Staff Leader hợp lệ đủ chưa? | Có/Không |
| Có dữ liệu test Add Department còn sót không? | Có/Không + xử lý |
| Có cần seed helper không? | Có/Không |

## 5. Unit Test đã tạo
[Liệt kê case hoặc giải thích vì sao không tạo Unit Test riêng]

## 6. Integration Test đã tạo
[Liệt kê case]

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
- Unit Test, nếu có, nằm đúng tests/PEMS.UnitTests/Departments/AddDepartment/ hoặc tên tương đương theo source.
- Integration Test nằm đúng tests/PEMS.IntegrationTests/Departments/AddDepartment/ hoặc tên tương đương theo source.
- Không trộn Unit Test và Integration Test.
- Không dùng database dev/thật.
- Không import SQL fresh-create gốc trực tiếp.
- Không đọc/copy/in secret thật.
- Không tự ý sửa production code để ép test pass.
- Unit Test đã chạy hoặc báo rõ lý do chưa chạy.
- Trước khi chạy Integration Test, đã kiểm tra pems_test có đủ dữ liệu nền.
- Integration Test chỉ chạy khi đạt DB safety gate và có xác nhận.
- Test names rõ nghĩa, không dùng số HTTP nếu có thể.
- Add Department test xác nhận endpoint thật, không bịa route.
- Không test nhầm Update/Search/View/Status/Personnel Department trong UC này.
- Authorization test bao phủ Staff Leader allowed và các role không được phép đại diện.
- Nếu controller thiếu authorization, test phải fail đúng và report security issue, không tự sửa production code.
- Valid create test assert DB record thật được tạo đúng campus/name/type/status.
- Invalid input test có DoesNotPersist thì phải query DB chứng minh không có record rác.
- Duplicate same campus test phải assert không tạo record thứ hai.
- Nếu test audit thì phải assert created_at/created_by thật.
- Nếu test campus scope thì phải setup Staff Leader campus và target campus rõ ràng.
- Integration Test assembly đã tắt parallelization hoặc report rõ lý do chưa thêm.
- Add Department không dùng chung cleanup prefix với Department UC khác.
- DatabaseResetHelper seed/cleanup nhận prefix riêng hoặc có helper riêng tương đương.
- Chạy riêng AddDepartmentApiTests và chạy toàn bộ IntegrationTests nếu được phép để phát hiện race condition.
- Báo cáo pass/fail rõ ràng.
```

---

## 17. Nhắc lại các lỗi nghiêm cấm

Không được lặp lại các lỗi sau:

```text
- Tự tạo endpoint Add Department giả khi source không có.
- Dùng dynamic permissions/permissions table/role_permissions table.
- Dùng role_code giả như STAFF_LEADER, DEPT_LEADER, DEPT.
- Cho Admin/HO tạo Department nếu policy hiện tại là Staff Leader-only.
- Bỏ authorization tests chỉ vì DepartmentsController hiện thiếu auth.
- Sửa production code để test pass mà chưa được duyệt.
- Test description nếu schema/source không có description.
- Test sâu Update/Search/View/Manage Status Department trong UC Add Department.
- Assert exact DB count khi không cô lập bằng prefix/token.
- Dùng chung DepartmentNamePrefix với UC khác.
- Cleanup theo prefix quá rộng khiến test class này xóa dữ liệu test class khác.
- Để xUnit chạy song song các Integration Test class dùng chung pems_test.
- Chạy SQL fresh-create gốc khi chưa kiểm tra có USE pems_db.
- Tin rằng `mysql pems_test < script.sql` chắc chắn chỉ tác động pems_test.
- Dùng appsettings.Development.json cho Integration Test.
- Đọc/copy secret thật sang file test.
- Đặt tên DoesNotPersist nhưng không kiểm tra DB.
- Báo “xong” khi chưa kiểm tra pems_test hoặc chưa giải thích vì sao chưa chạy Integration Test.
```

Nếu không chắc chắn, phải dừng lại và hỏi người dùng.
