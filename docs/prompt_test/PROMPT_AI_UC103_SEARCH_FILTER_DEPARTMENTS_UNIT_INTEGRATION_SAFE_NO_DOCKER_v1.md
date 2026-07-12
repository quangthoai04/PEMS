# PROMPT AI — TẠO TEST CODE THẬT CHO UC-103 SEARCH AND FILTER DEPARTMENTS (UNIT TEST + INTEGRATION TEST, KHÔNG DOCKER, AN TOÀN DATABASE) — v1

> File này dùng để đưa cho AI Agent/Code Agent khi cần tạo hoặc cập nhật test tự động cho chức năng **Search and Filter Departments** trong dự án PEMS.
>
> Use case này thuộc nhóm **Department Structure Management / Department Management**. Theo ma trận quyền mới, actor nghiệp vụ chính là **Staff Leader** với quyền đọc/search/filter trong phạm vi được phép.
>
> Mục tiêu: **tạo test code thật, chạy được, đúng nghiệp vụ Search and Filter Departments, kiểm tra trước `pems_test` có đủ dữ liệu nền để chạy Integration Test hay chưa, và tuyệt đối không làm hỏng `pems_db` hoặc bất kỳ database dev/thật nào**.

---

## 0. Bối cảnh và kinh nghiệm phải kế thừa

Prompt này kế thừa chuẩn đã chốt từ các prompt/test **Create FAQ**, **Update FAQ**, **UC-62 View List FAQ**, **UC-66 Search FAQ**, **UC-101 Add New Department** và **UC-102 Update Department**:

```text
- Dùng xUnit.
- Dùng FluentValidation.TestHelper cho Unit Test validator nếu có validator.
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
- Nếu một test dùng tên DoesNotModify/ReadOnly thì phải thật sự reload DB và assert unchanged.
- Nếu lỗi xuất hiện khi chạy toàn bộ IntegrationTests, phải kiểm tra race condition/test cleanup trước khi nghi production code.
```

Bài học đặc biệt từ lỗi race condition giữa các test trước:

```text
Không dùng chung một hằng số prefix cleanup cho nhiều use case.
Không cleanup bằng prefix rộng như [IT-DEPARTMENT], [IT-DEPT], [TEST], [IT-UC103] nếu prefix đó có thể bị use case khác dùng lại.
Không dùng prefix overlap nhau, ví dụ [IT-DEPT] và [IT-DEPT-SEARCH-FILTER].
Mỗi test class phải có prefix riêng, đủ rõ nghĩa, không trùng, không bao phủ prefix khác.
```

Prefix gợi ý cho use case này:

```text
[IT-UC103-SEARCH-FILTER-DEPARTMENT]
```

Nếu source/docs hiện tại dùng UC ID khác cho Search and Filter Departments, vẫn giữ tên nghiệp vụ trong prefix để tránh nhầm:

```text
[IT-SEARCH-FILTER-DEPARTMENT]
```

---

## 0.1. Lưu ý quan trọng về UC ID

Người dùng đang yêu cầu use case **Search and Filter Departments**.

Các tài liệu PEMS hiện có thể lệch UC ID:

```text
- USE_CASE_LIST.md / PERMISSION_MATRIX.md mới: UC-103 Search and Filter Departments.
- RTW template có thể ghi UC-101 Search and Filter Departments.
- Một số PROJECT_OVERVIEW cũ có thể ghi UC-100 Search and Filter Departments.
```

Quy tắc:

```text
- Ưu tiên tên nghiệp vụ ổn định: Search and Filter Departments.
- Ưu tiên USE_CASE_LIST.md/PERMISSION_MATRIX.md mới nhất nếu cần UC ID cho report.
- Nếu source/docs có mâu thuẫn, báo rõ trong report.
- Không hardcode UC ID vào logic test nếu source không dùng.
- Tên folder/test nên dùng tên nghiệp vụ ổn định: Departments/SearchFilterDepartments.
```

Không tự đổi sang các use case liên quan:

```text
Add New Department
Update Department
View Department List
View Department Details
Manage Department Status
Add Department Personnel
Search Personnel
Search Coordination Tasks
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

Tạo test tự động cho chức năng **Staff Leader search/filter danh sách departments**.

Test cần gồm 2 nhóm:

```text
1. Unit Test
   Kiểm tra logic nhỏ, chủ yếu là query validator, criteria validator, paging/filter/sort validator hoặc helper thuần nếu source có.
   Không gọi API thật.
   Không dùng database thật.

2. Integration Test
   Kiểm tra search/filter departments chạy qua nhiều layer thật:
   API + Authentication/Authorization giả + Controller + MediatR + Query Handler + database test riêng.
```

Sau khi hoàn thành, team phải biết rõ:

```text
- Search and Filter Departments đang dùng endpoint nào trong source thật.
- Search/filter là endpoint riêng hay query param của View Department List endpoint.
- Query param thật là gì: keyword/search/q, campusId, status, page, pageSize, sortBy, sortDirection...
- Actor hợp lệ thật là ai: Staff Leader only hay Staff Leader + HO theo code hiện tại.
- Scope dữ liệu thật: Staff Leader thấy department theo campus nào.
- Search scope thật: name, description, campus name, head user, hoặc field khác.
- Filter thật hỗ trợ: campus, status, departmentType, hoặc field khác.
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
Existing Department tests hiện tại nếu có
SQL fresh-create mới nhất trong docs/database/scripts/
```

### 2.2. Source code bắt buộc kiểm tra

AI Agent phải tự search đúng file trong project, không được bịa path.

Tối thiểu cần kiểm tra:

```text
Backend controller Department: DepartmentsController hoặc controller tương đương
SearchAndFilterDepartmentsQuery / ViewDepartmentListQuery nếu source gộp search/filter vào list
SearchAndFilterDepartmentsQueryHandler / ViewDepartmentListQueryHandler nếu có
SearchAndFilterDepartmentsQueryValidator / ViewDepartmentListQueryValidator nếu có
Department DTO/response DTO thật
Department entity
EF Configuration của departments
ApplicationDbContext
Constants/Enums liên quan department_type/status
Authorization/Role check liên quan Department Management
Existing Unit Tests: tests/PEMS.UnitTests/Departments/...
Existing Integration Tests: tests/PEMS.IntegrationTests/Departments/...
Existing test infrastructure: PemsWebApplicationFactory, TestAuthHandler, DatabaseResetHelper
SQL fresh-create mới nhất trong docs/database/scripts/
```

Nếu tên class/path trong project khác ví dụ trong file này, dùng tên thật trong source và ghi rõ trong report.

### 2.3. Source-first rule

Không được tự bịa:

```text
- endpoint search/filter departments
- query param keyword/search/q/campusId/status/page/pageSize/sortBy
- response DTO
- pagination metadata
- department_type/status enum
- role được phép
- campus scope behavior
- search scope
- filter behavior
- case-sensitive/case-insensitive behavior
- SQL table/field
```

Nếu source và tài liệu mâu thuẫn, báo rõ trong report và không suy đoán. Quy tắc ưu tiên:

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

---

## 3. Nghiệp vụ Search and Filter Departments phải giữ đúng

### 3.1. Actor hợp lệ

Theo ma trận quyền mới, **Search and Filter Departments** thuộc Department Management và actor hợp lệ là:

```text
Staff Leader = role_code STAFF + sub_role LEADER
```

Staff Leader có quyền đọc/search/filter departments trong phạm vi được phép.

Không mặc định các role sau có quyền nếu source/policy không cho phép:

```text
Admin
HO
Staff thường
Department Leader
Department Staff
Student
Visitor
```

Nếu source hiện tại đang cho HO xem/search departments theo legacy docs, phải report rõ mismatch:

```text
- PERMISSION_MATRIX mới: Staff Leader only.
- PROJECT_OVERVIEW legacy: có thể ghi Staff Leader + HO.
- Source hiện tại: [kết quả thật].
```

Không tự sửa expected để test pass nếu endpoint đang thiếu authorization. Nếu controller/endpoint gọi được ẩn danh hoặc role sai vẫn gọi được, test phải phát hiện lỗi bảo mật và report.

### 3.2. Endpoint/API phải lấy từ source thật

Không được tự bịa route.

AI Agent phải search trong `DepartmentsController` hoặc controller tương đương để xác định endpoint thật.

Các khả năng thường gặp:

```text
GET /api/departments
GET /api/departments?keyword=...
GET /api/departments?search=...
GET /api/departments/search?keyword=...
GET /api/departments/filter?keyword=...
```

Dùng đúng endpoint hiện tại trong source.

Nếu source hiện tại gộp search/filter vào `GET /api/departments` hoặc View Department List endpoint, test UC-103 vẫn được phép gọi endpoint đó với query param thật, nhưng phải ghi rõ trong report:

```text
Search and Filter Departments hiện không có endpoint riêng; source triển khai search/filter như query parameter của endpoint list departments.
```

### 3.3. Phân biệt với các UC liên quan

Không test nhầm các UC sau trong UC-103:

```text
Add New Department       -> tạo department mới.
Update Department        -> sửa tên/type/campus/head/status nếu source cho phép.
View Department List     -> load danh sách tổng quát/pagination/sort nếu không có search/filter cụ thể.
View Department Details  -> xem chi tiết một department.
Manage Department Status -> active/inactive department.
Add Department Personnel -> thêm nhân sự vào department.
Search Personnel         -> tìm nhân sự, không phải tìm department.
```

UC-103 chỉ tập trung vào:

```text
Search/filter departments + permission scope + pagination metadata nếu endpoint trả paginated list.
```

### 3.4. Department schema và enum hiện tại

Theo schema v10 mới, bảng `departments` có các field chính:

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
- campus_id là FK sang campuses.
- unique theo (campus_id, name).
- department_type chỉ có IC / GENERAL.
- status chỉ có ACTIVE / INACTIVE.
```

Không tự test field không tồn tại trong schema/source thật, đặc biệt:

```text
description
phone
email
address
language_code
```

Lưu ý: một số UC/spec cũ có thể ghi “search department name/description”. Nếu SQL/source hiện tại không có `description` trong bảng departments/DTO, không viết test search theo description. Ghi rõ trong report:

```text
Spec cũ nhắc description, nhưng schema/source departments hiện tại không có field description, nên không tạo test description search.
```

### 3.5. Search scope

AI Agent phải đọc source handler để xác nhận search scope thật.

Nếu source chỉ search theo `departments.name`, test chỉ assert search theo name.

Nếu source search thêm các field khác, chỉ test khi field đó tồn tại trong source/schema/DTO thật, ví dụ:

```text
campus name
head user full name
department_type
status label
```

Không được giả định search trong description nếu schema/source không có.

### 3.6. Filter scope

AI Agent phải đọc source handler/validator để xác nhận filter thật.

Các filter thường có thể có:

```text
campusId
status
keyword
page
pageSize
sortBy
sortDirection
departmentType
```

Không test filter không tồn tại trong source.

Nếu source hỗ trợ `status`, giá trị hợp lệ theo DB là:

```text
ACTIVE
INACTIVE
```

Nếu source hỗ trợ `departmentType`, giá trị hợp lệ theo DB là:

```text
IC
GENERAL
```

Nếu source hỗ trợ `ALL`, `all`, hoặc null để bỏ filter, test theo source thật.

### 3.7. Campus scope / data scope

Search and Filter Departments phải tôn trọng scope dữ liệu.

Với Staff Leader, cần kiểm tra source hiện tại xử lý scope thế nào:

```text
- Staff Leader chỉ thấy departments thuộc primary_campus_id của mình.
- Hoặc Staff Leader có thể filter campus trong phạm vi được phép.
- Hoặc source legacy cho HO xem toàn hệ thống.
```

Không được để Staff Leader nhìn thấy department của campus khác nếu nghiệp vụ/source yêu cầu campus-scoped.

Integration Test nên có ít nhất một case chứng minh:

```text
Seed department cùng campus với Staff Leader.
Seed department ở campus khác.
Staff Leader search/filter.
Expect chỉ department cùng scope xuất hiện.
Department ngoài scope không xuất hiện.
```

Nếu source hiện tại chưa enforce scope, test sẽ fail. Không sửa test expected để pass; report đây là security/scope issue.

### 3.8. Search/filter behavior

Đọc source để xác nhận:

```text
- Keyword null/empty/whitespace xử lý thế nào.
- Keyword có trim không.
- Search có case-insensitive không.
- Search dùng contains matching hay exact matching.
- Keyword có minimum/maximum length không.
- Search + status/campus filter dùng AND logic không.
```

Nếu source/spec hiện tại xác nhận:

```text
Search trim keyword.
Search case-insensitive.
Search contains matching.
Keyword rỗng thì không filter.
Search + filter dùng AND logic.
```

thì test theo các rule đó.

Nếu source không có validate keyword length, không tự thêm test `Keyword_TooLong_HasError`.

### 3.9. Read-only behavior

Search/filter departments là API đọc dữ liệu.

Khi gọi thành công:

```text
- Không được tạo department mới.
- Không được sửa name/department_type/campus_id/head_user_id/status.
- Không được refresh updated_at/updated_by.
- Không được thay đổi created_at/created_by.
```

Không bắt buộc phải có test read-only riêng nếu UC-104 View Department List đã có test read-only và report chấp nhận reuse. Nếu tạo test tên `StaffLeader_Search_DoesNotModifyDepartments`, phải seed dữ liệu, chụp snapshot trước GET, gọi search/filter, reload DB và assert record không đổi.

---

## 4. Phân biệt rõ Unit Test và Integration Test

### 4.1. Unit Test phù hợp với Search and Filter Departments

Unit Test chỉ kiểm tra logic nhỏ, cô lập.

Chỉ tạo Unit Test nếu source có:

```text
SearchAndFilterDepartmentsQueryValidator
ViewDepartmentListQueryValidator có rule search/filter/paging/status/campusId
DepartmentSearchCriteriaValidator
DepartmentSearchKeywordNormalizer
DepartmentListFilterHelper thuần
DepartmentListDtoMapper thuần
```

Các case Unit Test phù hợp, chỉ viết nếu source có rule tương ứng:

```text
ValidQuery_NoErrors
Keyword_Null_NoError nếu keyword optional
Keyword_Empty_NoError nếu empty nghĩa là no filter
Keyword_Whitespace_NoError hoặc Keyword_Whitespace_TreatedAsEmpty nếu source có behavior rõ
Keyword_TooLong_HasError nếu source có max length
CampusId_Zero_HasError nếu source validate campusId > 0
Status_Null_NoError nếu status optional
Status_Allowed_NoError với ACTIVE/INACTIVE nếu source hỗ trợ
Status_Invalid_HasError với VISIBLE/DRAFT/DELETED nếu validator có rule
DepartmentType_Allowed_NoError với IC/GENERAL nếu source hỗ trợ
DepartmentType_Invalid_HasError nếu source có rule
Page_NotPositive_HasError nếu source validate page
PageSize_OutOfRange_HasError nếu source validate pageSize
SortBy_NotAllowed_HasError nếu source whitelist sort fields
```

Không tạo Unit Test vô nghĩa chỉ để đủ số lượng. Nếu source hiện tại không có validator/helper riêng cho search/filter, báo rõ:

```text
Không tạo Unit Test riêng cho UC-103 vì source hiện tại triển khai search/filter trong handler và không có logic unit-level tách biệt; behavior được test ở Integration Test qua handler + DB thật.
```

### 4.2. Integration Test phù hợp với Search and Filter Departments

Integration Test kiểm tra nhiều phần chạy cùng nhau:

```text
HTTP request -> Auth/TestAuthHandler -> Controller -> MediatR -> Validator -> Handler -> DB test
```

Search/filter behavior và scope phụ thuộc DB thật nên ưu tiên test bằng Integration Test.

Integration Test phải chứng minh:

```text
- Staff Leader được search/filter nếu endpoint đúng quyền.
- Role không có quyền bị chặn.
- Keyword match đúng field source hỗ trợ.
- Keyword no match trả empty result.
- Filter campus/status/departmentType hoạt động nếu source hỗ trợ.
- Search + filter dùng AND logic nếu source hỗ trợ.
- Staff Leader không thấy department ngoài scope.
- API đọc không làm thay đổi DB nếu có test read-only.
```

---

## 5. Quy ước tổ chức thư mục test

Dự án có nhiều chức năng, vì vậy test code phải chia rõ theo:

```text
Test Project
→ Module
→ Use Case / Action nghiệp vụ
→ File test
```

### 5.1. Unit Test folder

Nếu có Unit Test riêng cho Search and Filter Departments, đặt trong:

```text
tests/PEMS.UnitTests/Departments/SearchFilterDepartments/
```

File gợi ý:

```text
SearchFilterDepartmentsQueryValidatorTests.cs
DepartmentSearchCriteriaValidatorTests.cs
```

Nếu source dùng chung `ViewDepartmentListQueryValidator`, có thể đặt tên rõ:

```text
SearchFilterDepartmentsQueryValidatorTests.cs
```

nhưng trong comment phải ghi:

```text
UC-103 Search and Filter Departments currently reuses ViewDepartmentListQuery/ViewDepartmentListQueryValidator because source implements search/filter as query parameters of the list endpoint.
```

Không copy toàn bộ test của View Department List nếu chúng không phải search/filter-specific.

### 5.2. Integration Test folder

Integration Test đặt trong:

```text
tests/PEMS.IntegrationTests/Departments/SearchFilterDepartments/
```

File gợi ý:

```text
SearchFilterDepartmentsApiTests.cs
```

Nếu team quyết định giữ search/filter tests trong `ViewDepartmentListApiTests`, phải ghi report mapping rõ ràng và tránh duplicate.

### 5.3. Helper dùng chung và prefix dữ liệu test

Ưu tiên dùng lại:

```text
tests/PEMS.IntegrationTests/TestInfrastructure/PemsWebApplicationFactory.cs
tests/PEMS.IntegrationTests/TestInfrastructure/TestAuthHandler.cs
tests/PEMS.IntegrationTests/TestInfrastructure/DatabaseResetHelper.cs
```

Nếu cần bổ sung helper seed department cho Search and Filter Departments, bổ sung vào `DatabaseResetHelper` hoặc helper chung tương đương. Không copy helper lặp lại trong từng file test.

Helper nên hỗ trợ:

```text
- Tạo department test với prefix do test class truyền vào.
- Tạo department test trong campus chỉ định.
- Tạo department test với status ACTIVE/INACTIVE.
- Tạo department test với department_type IC/GENERAL.
- Tạo department test với name chứa token riêng.
- Trả về departmentId.
- Cleanup record theo đúng prefix được truyền vào.
- Không hardcode DepartmentNamePrefix dùng chung cho mọi Department test.
- Không cleanup prefix của use case/test class khác.
```

Ví dụ hướng thiết kế:

```csharp
public const string SearchFilterDepartmentNamePrefix = "[IT-UC103-SEARCH-FILTER-DEPARTMENT] ";

CreateTestDepartmentAsync(db, name, campusId, departmentType, status, createdBy, updatedBy)
DeleteTestDepartmentsAsync(db, prefix)
```

Hoặc dùng prefix truyền vào:

```csharp
CreateTestDepartmentAsync(db, prefix, nameSuffix, campusId, departmentType, status)
DeleteTestDepartmentsAsync(db, prefix)
```

### 5.4. Quy tắc chạy tuần tự Integration Test

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

## 6. Phạm vi AI Agent được sửa

AI Agent được phép tạo/sửa:

```text
tests/PEMS.UnitTests/Departments/SearchFilterDepartments/...
tests/PEMS.IntegrationTests/Departments/SearchFilterDepartments/...
tests/PEMS.UnitTests/TestHelpers/...
tests/PEMS.IntegrationTests/TestInfrastructure/...
docs/testing/...
backend/PEMS.Api/appsettings.Testing.example.json
file prompt/test documentation liên quan
```

AI Agent được phép sửa `ViewDepartmentListApiTests` hoặc `ViewDepartmentListQueryValidatorTests` **chỉ khi** cần tách/move search/filter-specific tests sang UC-103 và không làm mất coverage của UC-104 View Department List.

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

## 7. Test infrastructure cho Integration Test

Ưu tiên dùng lại test infrastructure đã có từ các Integration Test trước.

Yêu cầu:

```text
- PemsWebApplicationFactory khởi động API trong environment Testing.
- Không dùng appsettings.Development.json.
- Override authentication bằng test scheme.
- Cho phép tạo request giả với role STAFF + LEADER, STAFF + STAFF, ADMIN, HO, DEPARTMENT + LEADER, DEPARTMENT + STAFF, VISITOR.
- Cho phép gọi request không đăng nhập để test 401.
- Connection string phải trỏ tới database test riêng.
- Không dùng Docker.
- Không dùng Testcontainers.
- Không gọi service ngoài thật như Google SSO, SMTP, Google Drive.
```

Nếu project đã có test infrastructure tương đương, ưu tiên dùng lại và bổ sung thiếu sót, không tạo trùng.

---

## 8. Kiểm tra `pems_test` trước khi chạy Integration Test

Phần này là bắt buộc cho Department tests.

### 8.1. Vì sao phải kiểm tra `pems_test`?

Search/filter departments phụ thuộc dữ liệu nền thật trong database test:

```text
- Cần campus hợp lệ.
- Cần Staff Leader hợp lệ thuộc một campus.
- Cần department IC của Staff Leader nếu trigger/seed user yêu cầu department_id.
- Cần có ít nhất 2 campus nếu muốn test scope/campus filter.
- Cần seed departments test có prefix riêng để search/filter.
```

Nếu `pems_test` thiếu dữ liệu nền, Integration Test có thể fail vì setup sai, không phải vì business logic sai.

### 8.2. Các kiểm tra read-only cần làm trước

AI Agent phải kiểm tra bằng lệnh chỉ đọc, không tự ghi DB nếu chưa được xác nhận.

Các câu SQL gợi ý, điều chỉnh theo schema/source thật:

```sql
SELECT DATABASE();
```

Kết quả phải là:

```text
pems_test
```

Kiểm tra campus active:

```sql
SELECT campus_id, campus_code, name, status
FROM campuses
WHERE status = 'ACTIVE'
ORDER BY campus_id
LIMIT 10;
```

Kiểm tra department IC active dùng để gán cho Staff Leader test:

```sql
SELECT department_id, campus_id, name, department_type, status
FROM departments
WHERE department_type = 'IC' AND status = 'ACTIVE'
ORDER BY campus_id, department_id
LIMIT 10;
```

Kiểm tra Staff Leader active:

```sql
SELECT user_id, email, role_code, sub_role, primary_campus_id, department_id, status
FROM users
WHERE role_code = 'STAFF'
  AND sub_role = 'LEADER'
  AND status = 'ACTIVE'
ORDER BY user_id
LIMIT 10;
```

Kiểm tra dữ liệu test còn sót:

```sql
SELECT department_id, campus_id, name, department_type, status
FROM departments
WHERE name LIKE '[IT-UC103-SEARCH-FILTER-DEPARTMENT]%'
ORDER BY department_id;
```

### 8.3. Nếu thiếu dữ liệu nền

Nếu thiếu campus/IC department/Staff Leader hợp lệ, không tự insert ngay.

Phải báo rõ:

```text
- Thiếu dữ liệu gì.
- Test nào cần dữ liệu đó.
- Có thể tạo bằng helper nào.
- Dữ liệu sẽ tạo trong pems_test, không phải pems_db.
- Có cần người dùng xác nhận trước khi seed/write DB không.
```

Không dùng department seed thật làm target bị update/delete nếu có thể tránh. Với Search/Filter, nên seed department test riêng bằng prefix rồi cleanup.

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
3. Dữ liệu hợp lệ thì hệ thống xử lý đúng.
4. Dữ liệu sai quan trọng thì bị chặn.
5. Rule nghiệp vụ đặc biệt được kiểm tra.
6. Khi request fail, DB không bị ghi/sửa sai nếu use case có write side effect.
7. Rủi ro bảo mật chính như authorization, data scope, data exposure được kiểm tra nếu liên quan.
```

Với Search and Filter Departments, tránh test thừa:

```text
Không test sâu Add New Department.
Không test sâu Update Department.
Không test Manage Department Status.
Không test View Department Details.
Không test Add Department Personnel.
Không test Search Personnel.
Không duplicate nguyên bộ ViewDepartmentListApiTests nếu search/filter là query param của list endpoint.
```

Chỉ test các case search/filter có giá trị:

```text
- Search theo department name hoặc field được source hỗ trợ.
- Search no match.
- Filter status nếu source hỗ trợ.
- Filter campus nếu source hỗ trợ.
- Search + filter AND logic nếu source hỗ trợ.
- Data scope: Staff Leader không thấy department ngoài campus/scope.
- Authorization nếu UC-103 là Staff Leader-only.
```

---

## 11. Unit Test cần tạo

Chỉ tạo Unit Test theo source thật hiện tại.

### 11.1. Validator/Normalizer tests

File gợi ý:

```text
tests/PEMS.UnitTests/Departments/SearchFilterDepartments/SearchFilterDepartmentsQueryValidatorTests.cs
```

Nếu source dùng chung `ViewDepartmentListQueryValidator`, test có thể khởi tạo validator đó, nhưng tên và comment phải nói rõ đang test search/filter query behavior của UC-103.

Các case tối thiểu, chỉ viết nếu source có rule tương ứng:

```text
1. ValidQuery_NoErrors
   Query search/filter hợp lệ -> không lỗi.

2. Keyword_Null_NoError
   Nếu keyword optional -> không lỗi.

3. Keyword_Empty_NoError
   Nếu empty keyword nghĩa là no filter -> không lỗi.

4. CampusId_Zero_HasError
   Nếu source validate campusId > 0.

5. CampusId_Null_NoError
   Nếu campus filter optional.

6. Status_Allowed_NoError
   ACTIVE/INACTIVE hợp lệ nếu source có status filter.

7. Status_Invalid_HasError
   VISIBLE/DRAFT/DELETED invalid nếu source có validator.

8. DepartmentType_Allowed_NoError
   IC/GENERAL hợp lệ nếu source có departmentType filter.

9. DepartmentType_Invalid_HasError
   Nếu source có validator.

10. Page_NotPositive_HasError
    Nếu source validate page.

11. PageSize_OutOfRange_HasError
    Nếu source validate pageSize.

12. SortBy_NotAllowed_HasError
    Nếu source whitelist sort fields.
```

Không tạo Unit Test vô nghĩa chỉ để đủ số lượng. Nếu Search and Filter Departments không có Unit-level logic riêng, báo rõ trong report:

```text
Không tạo Unit Test riêng cho UC-103 vì source hiện tại triển khai search/filter trong handler và không có validator/helper riêng; behavior được test ở Integration Test.
```

### 11.2. Tên method Unit Test nên dùng

Dùng tên rõ nghĩa, không dùng số HTTP trong Unit Test.

Ví dụ:

```csharp
ValidQuery_NoErrors()
Keyword_Null_NoError()
CampusId_Zero_HasError()
Status_Allowed_NoError()
Status_Invalid_HasError()
Page_NotPositive_HasError()
PageSize_OutOfRange_HasError()
SortBy_NotAllowed_HasError()
```

Tên test không được hứa điều không assert.

---

## 12. Integration Test cần tạo

Integration Test đặt trong:

```text
tests/PEMS.IntegrationTests/Departments/SearchFilterDepartments/
```

File gợi ý:

```text
SearchFilterDepartmentsApiTests.cs
```

Ưu tiên dùng lại style đã chốt từ các Integration Test trước:

```text
- xUnit.
- IClassFixture<PemsWebApplicationFactory>.
- IAsyncLifetime cleanup sau mỗi test.
- CreateClientAsAsync(EffectiveRole.StaffLeader/Staff/Admin/Ho/DepartmentLeader/Visitor).
- TestAuthHandler headers.
- DatabaseResetHelper cleanup theo prefix riêng của Search and Filter Departments.
- Semantic test names ngắn gọn, không dùng số HTTP hoặc Returns400/Returns403/Returns200.
- Assembly-level DisableTestParallelization cho PEMS.IntegrationTests nếu chưa có.
```

### 12.1. Setup dữ liệu department cho Search/Filter tests

Search/filter departments cần seed department có token riêng để cô lập kết quả.

Yêu cầu:

```text
- Dùng prefix riêng của Search and Filter Departments.
- Mỗi test dùng token GUID riêng.
- Token nên đặt trong department name.
- Seed department cùng campus với Staff Leader.
- Seed department ở campus khác để test scope nếu có đủ campus.
- Seed ACTIVE và INACTIVE để test status filter nếu source hỗ trợ.
- Không phụ thuộc dữ liệu seed thật trong pems_test.
- Không assert exact total count toàn DB nếu không filter/cô lập được.
- Khi cần assert no match, dùng token GUID chưa seed.
```

Ví dụ:

```text
Name: [IT-UC103-SEARCH-FILTER-DEPARTMENT] search-name <token>
DepartmentType: GENERAL
Status: ACTIVE
CampusId: same campus as Staff Leader
```

### 12.2. Các case Integration Test tối thiểu

Điều chỉnh HTTP status/query param theo source thật.

#### Authentication / Authorization

Nếu UC-103 là Staff Leader-only:

```text
1. Anonymous_Unauthorized
   Không gắn auth headers -> search/filter departments -> 401 Unauthorized.

2. Staff_Forbidden
   STAFF thường đã đăng nhập -> search/filter departments -> 403 Forbidden.

3. Admin_Forbidden
   ADMIN đã đăng nhập -> 403 Forbidden nếu nghiệp vụ Staff Leader-only.

4. Ho_Forbidden
   HO đã đăng nhập -> 403 Forbidden nếu source/policy mới là Staff Leader-only.

5. Visitor_Forbidden
   VISITOR đã đăng nhập -> 403 Forbidden.

6. DepartmentLeader_Forbidden
   DEPARTMENT + LEADER đã đăng nhập -> 403 Forbidden nếu UC-103 không cho Department Leader search department management list.
```

Nếu source hiện tại cho HO search/filter departments theo legacy docs, không tự sửa expected. Phải report:

```text
Source hiện tại cho HO gọi endpoint, trong khi PERMISSION_MATRIX mới chỉ ghi Staff Leader. Cần xác nhận nghiệp vụ.
```

#### Happy path / search core behavior

```text
7. StaffLeader_KeywordMatchesName
   Seed department có token trong name, cùng campus Staff Leader.
   Staff Leader search keyword token.
   Expect OK.
   Response chứa department đó.

8. StaffLeader_KeywordNoMatch_ReturnsEmptyResult
   Staff Leader search token GUID chưa seed.
   Expect OK.
   Items rỗng và TotalItems = 0 nếu response DTO có TotalItems.

9. StaffLeader_KeywordCaseInsensitiveAndTrimmed
   Chỉ viết nếu source hỗ trợ trim/case-insensitive.
   Seed name chứa token lower/normal.
   Search bằng token uppercase + whitespace.
   Expect vẫn tìm thấy.
```

Không viết `KeywordMatchesDescription` nếu schema/source không có description.

#### Filter behavior

Chỉ viết nếu source hỗ trợ filter tương ứng:

```text
10. StaffLeader_StatusFilter_ReturnsOnlyMatchingStatus
    Seed ACTIVE và INACTIVE departments cùng token/campus.
    Search/filter status ACTIVE.
    Expect chỉ ACTIVE trong tập kết quả.

11. StaffLeader_CampusFilter_ReturnsOnlyMatchingCampus
    Seed departments ở campus A và campus B.
    Staff Leader lọc campus A nếu scope cho phép.
    Expect chỉ departments campus A.

12. StaffLeader_DepartmentTypeFilter_ReturnsOnlyMatchingType
    Chỉ viết nếu source hỗ trợ departmentType filter.
    Seed IC và GENERAL cùng token/campus.
    Filter GENERAL.
    Expect chỉ GENERAL.

13. StaffLeader_SearchAndStatusFilter_UsesAndLogic
    Seed nhiều departments cùng token nhưng khác status.
    Search keyword + status filter.
    Response chỉ chứa status được chọn trong tập match keyword.

14. StaffLeader_SearchAndCampusFilter_UsesAndLogic
    Seed nhiều departments cùng token nhưng khác campus.
    Search keyword + campus filter.
    Response chỉ chứa campus được chọn trong tập match keyword, đồng thời tôn trọng actor scope.
```

#### Scope/security behavior

```text
15. StaffLeader_DoesNotSeeOtherCampusDepartments
    Seed department cùng token ở campus của Staff Leader và campus khác.
    Staff Leader search token.
    Expect department cùng campus xuất hiện.
    Expect department campus khác không xuất hiện.
```

Case này rất quan trọng nếu source/business rule yêu cầu campus scope.

#### Query validation

Chỉ viết nếu source validator có rule tương ứng:

```text
16. Page_Zero_BadRequest
    page=0 -> BadRequest.

17. InvalidStatusFilter_BadRequest
    status=VISIBLE hoặc DRAFT -> BadRequest.

18. InvalidCampusId_BadRequest
    campusId=0 hoặc âm -> BadRequest nếu source validator chặn.

19. InvalidDepartmentTypeFilter_BadRequest
    departmentType=PROGRAM hoặc UNKNOWN -> BadRequest nếu source validator chặn.
```

Không lặp lại quá nhiều invalid query nếu Unit Test validator đã cover và Integration Test chỉ cần pipeline đại diện.

#### Read-only behavior

Optional:

```text
20. StaffLeader_Search_DoesNotModifyDepartments
    Seed department, chụp snapshot name/campus_id/department_type/head_user_id/status/audit.
    Staff Leader search/filter match department đó.
    Expect OK.
    Reload DB và assert record unchanged.
```

Chỉ dùng tên `DoesNotModify` nếu thật sự assert DB unchanged.

### 12.3. Bộ tên method Integration Test gợi ý

Dùng tên semantic, ngắn gọn, không dùng số HTTP code trong tên test.

Bộ tên gợi ý:

```csharp
Anonymous_Unauthorized()
Staff_Forbidden()
Admin_Forbidden()
Ho_Forbidden()
Visitor_Forbidden()
DepartmentLeader_Forbidden()

StaffLeader_KeywordMatchesName()
StaffLeader_KeywordNoMatch_ReturnsEmptyResult()
StaffLeader_KeywordCaseInsensitiveAndTrimmed()
StaffLeader_StatusFilter_ReturnsOnlyMatchingStatus()
StaffLeader_CampusFilter_ReturnsOnlyMatchingCampus()
StaffLeader_DepartmentTypeFilter_ReturnsOnlyMatchingType()
StaffLeader_SearchAndStatusFilter_UsesAndLogic()
StaffLeader_SearchAndCampusFilter_UsesAndLogic()
StaffLeader_DoesNotSeeOtherCampusDepartments()
Page_Zero_BadRequest()
InvalidStatusFilter_BadRequest()
InvalidCampusId_BadRequest()
InvalidDepartmentTypeFilter_BadRequest()
StaffLeader_Search_DoesNotModifyDepartments()
```

Không bắt buộc viết toàn bộ nếu source hiện tại không hỗ trợ hoặc case bị trùng với View Department List. Nếu bỏ case nào, phải giải thích lý do trong report.

### 12.4. Không tạo test thừa

Không tạo các test sau trong UC-103 nếu đã cover ở UC-104 View Department List và không trực tiếp liên quan search/filter:

```text
View first page without search/filter.
DTO contains every display field.
Sort by every supported column nếu không gắn với search/filter.
View department details.
Create/update/status/personnel actions.
```

Chỉ đưa lại nếu Search and Filter Departments được report như use case hoàn toàn độc lập và reviewer yêu cầu full API validation matrix.

---

## 13. Quy tắc đặt tên test case

Tên test phải ngắn gọn nhưng không được mơ hồ hoặc hứa quá nội dung assert.

```text
- Tên test phải nói đúng hành vi chính của test.
- Không dùng số HTTP code trong tên test, ví dụ 200/400/401/403/404/409.
- Có thể dùng tên HTTP outcome như Unauthorized, Forbidden, BadRequest, NotFound, Conflict nếu đó là hành vi chính cần test.
- Không đặt tên kiểu chung chung Returns400/Returns403/Returns200.
- Không dùng tên quá dài chỉ để liệt kê toàn bộ assert.
- Không dùng DoesNotModify / ReadOnly nếu test không thật sự reload DB và kiểm tra record cũ không đổi.
- Không dùng MatchesName nếu token thật ra nằm ở field khác.
- Không dùng CaseInsensitiveAndTrimmed nếu test không thật sự đổi case và thêm whitespace.
- Không dùng ReturnsOnlyMatchingStatus nếu không assert tất cả item đều đúng status.
- Không dùng DoesNotSeeOtherCampusDepartments nếu không seed department ngoài campus và assert nó không xuất hiện.
- Với authorization test, tên có thể ngắn vì outcome chính là Unauthorized/Forbidden.
- Với business test, ưu tiên tên theo nghiệp vụ thay vì chỉ theo HTTP status.
```

Ví dụ đúng:

```text
StaffLeader_KeywordMatchesName
StaffLeader_StatusFilter_ReturnsOnlyMatchingStatus
StaffLeader_SearchAndStatusFilter_UsesAndLogic
StaffLeader_DoesNotSeeOtherCampusDepartments
StaffLeader_Search_DoesNotModifyDepartments
```

Ví dụ sai:

```text
SearchDepartments_WhenStaffLeaderSearchesKeyword_Returns200AndFiltersAndPaginatesAndSortsEverything
Returns200
Keyword_Search_MatchesName // sai nếu token không nằm trong name
StaffLeader_Search_DoesNotModifyDepartments // sai nếu chỉ assert OK, không reload DB
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
- Đã chạy SELECT DATABASE() và xác nhận là pems_test.
- Đã kiểm tra pems_test có đủ active campus / Staff Leader / IC department nền hoặc đã có kế hoạch seed an toàn.
- SQL script đã được scan an toàn nếu cần import.
- Người dùng đã xác nhận cho phép chạy Integration Test có DB.
```

Nếu chưa đủ điều kiện, không chạy Integration Test. Hãy báo rõ:

```text
Integration Test code đã tạo/cập nhật nhưng chưa chạy vì chưa có xác nhận an toàn database hoặc pems_test thiếu dữ liệu nền.
```

Sau khi được phép chạy, nên chạy:

```bash
dotnet test tests/PEMS.IntegrationTests/PEMS.IntegrationTests.csproj --filter "FullyQualifiedName~SearchFilterDepartmentsApiTests"
dotnet test tests/PEMS.IntegrationTests/PEMS.IntegrationTests.csproj --filter "FullyQualifiedName~AddDepartmentApiTests"
dotnet test tests/PEMS.IntegrationTests/PEMS.IntegrationTests.csproj --filter "FullyQualifiedName~UpdateDepartmentApiTests"
dotnet test tests/PEMS.IntegrationTests/PEMS.IntegrationTests.csproj
```

Mục tiêu là phát hiện cả lỗi trong test class mới và lỗi tương tác/race condition với các test Department cũ.

---

## 15. Output/report sau khi làm

Sau khi hoàn thành, báo cáo bằng tiếng Việt theo format:

```md
# Báo cáo tạo test UC-103 Search and Filter Departments

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
| UC ID trong yêu cầu | UC-103 Search and Filter Departments hoặc ID source thật |
| UC ID trong source/docs nếu khác | ... |
| Endpoint thật | ... |
| Search/filter là endpoint riêng hay query param của list? | ... |
| Query params thật | keyword/search/q, campusId, status, page, pageSize, ... |
| Response DTO thật | ... |
| Actor hợp lệ | ... |
| Có mismatch Staff Leader-only vs HO legacy không? | ... |
| Search scope thật | name/description/campus/head/... |
| Filter scope thật | campus/status/departmentType/... |
| Có trim/case-insensitive/contains không? | ... |
| Có kết hợp filter theo AND logic không? | ... |
| Scope theo campus được enforce không? | ... |

## 4. Unit Test đã tạo
[Liệt kê case hoặc giải thích vì sao không tạo Unit Test riêng]

## 5. Integration Test đã tạo
[Liệt kê case]

## 6. Kiểm tra pems_test và an toàn database
| Mục | Kết quả |
|---|---|
| SELECT DATABASE() | ... |
| Database test dự kiến | pems_test hoặc tên thật |
| Có dùng pems_db không? | Không |
| Có active campus không? | Có/Không |
| Có IC department active không? | Có/Không |
| Có Staff Leader active hợp lệ không? | Có/Không |
| Có đủ campus thứ hai để test scope không? | Có/Không |
| Có dữ liệu test còn sót prefix UC-103 không? | Có/Không |
| Có dùng appsettings.Development.json không? | Không |
| Có đọc/copy secret thật không? | Không |
| Có scan SQL trước khi import không? | Có/Chưa cần |
| Có chạy lệnh ghi DB chưa? | Có/Không + giải thích |

## 7. Kết quả chạy lệnh
```text
[dotnet build result]
[dotnet test UnitTests result]
[IntegrationTests result nếu đã được phép chạy]
```

## 8. Test fail nếu có
| Test | Expected | Actual | Nhận định |
|---|---|---|---|
| ... | ... | ... | ... |

## 9. Production code/security issue nếu có
[Chỉ báo cáo, không tự sửa nếu chưa được duyệt]

## 10. Việc cần người dùng xác nhận thêm
[Nếu còn]
```

Không được báo “hoàn thành” nếu chưa build/test hoặc chưa nói rõ lý do không chạy được.

---

## 16. Definition of Done

Task chỉ hoàn thành khi đạt đủ các điều kiện sau:

```text
- Test code thật đã được tạo/cập nhật.
- Unit Test, nếu có, nằm đúng tests/PEMS.UnitTests/Departments/SearchFilterDepartments/.
- Integration Test nằm đúng tests/PEMS.IntegrationTests/Departments/SearchFilterDepartments/.
- Không trộn Unit Test và Integration Test.
- Không dùng database dev/thật.
- Không import SQL fresh-create gốc trực tiếp.
- Không đọc/copy/in secret thật.
- Không tự ý sửa production code để ép test pass.
- Unit Test đã chạy hoặc báo rõ lý do chưa chạy.
- Integration Test chỉ chạy khi đạt DB safety gate và có xác nhận.
- Đã kiểm tra pems_test bằng SELECT DATABASE() trước khi chạy Integration Test.
- Đã kiểm tra pems_test có đủ active campus / Staff Leader / IC department nền hoặc report rõ thiếu gì.
- Test names rõ nghĩa, không dùng số HTTP nếu có thể.
- Search/filter test xác nhận endpoint thật, không bịa route.
- Nếu search/filter dùng GET /api/departments?keyword=..., report rõ search/filter gộp trong list endpoint.
- Không duplicate nguyên bộ ViewDepartmentList tests.
- Authorization test bao phủ Anonymous và role không phải Staff Leader đại diện nếu UC-103 là Staff Leader-only.
- Search test chứng minh keyword match đúng field source hỗ trợ.
- No-match test không phụ thuộc dữ liệu seed thật.
- Filter test không assert exact total count nếu không cô lập dữ liệu bằng token/prefix.
- Nếu tên có MatchesName, token chỉ được nằm trong name.
- Nếu tên có CaseInsensitiveAndTrimmed, test thật sự đổi case và thêm whitespace.
- Nếu tên có DoesNotModify/ReadOnly, test thật sự snapshot DB trước/sau.
- Nếu search/filter là cùng endpoint, test AND logic bằng case đại diện.
- Integration Test assembly đã tắt parallelization hoặc report rõ lý do chưa thêm.
- Search and Filter Departments không dùng chung cleanup prefix với Add/Update/View/Status Department.
- DatabaseResetHelper seed/cleanup nhận prefix riêng hoặc có helper riêng tương đương.
- Chạy riêng SearchFilterDepartmentsApiTests và chạy toàn bộ IntegrationTests nếu được phép để phát hiện race condition.
- Báo cáo pass/fail rõ ràng.
```

---

## 17. Nhắc lại các lỗi nghiêm cấm

Không được lặp lại các lỗi sau:

```text
- Tự tạo endpoint Search and Filter Departments giả khi source không có.
- Dùng endpoint personnel/search coordination để test department search.
- Copy nguyên ViewDepartmentListApiTests sang SearchFilterDepartmentsApiTests và đổi tên class.
- Test sâu Add New Department trong UC-103.
- Test sâu Update Department trong UC-103.
- Test sâu Manage Department Status trong UC-103.
- Test View Department Details trong UC-103.
- Test Search Personnel trong UC-103.
- Assert search theo description nếu schema/source departments không có description.
- Assert exact total count khi DB có seed khác và không cô lập bằng token/prefix.
- Dùng chung DepartmentPrefix với Add/Update/View/Status Department.
- Cleanup theo prefix quá rộng khiến test class này xóa dữ liệu test class khác.
- Để xUnit chạy song song các Integration Test class dùng chung pems_test.
- Thấy NotFound/DbUpdateConcurrencyException khi chạy toàn bộ test rồi vội sửa production code trước khi kiểm tra race condition/test cleanup.
- Chạy SQL fresh-create gốc khi chưa kiểm tra có USE pems_db.
- Tin rằng `mysql pems_test < script.sql` chắc chắn chỉ tác động pems_test.
- Dùng appsettings.Development.json cho Integration Test.
- Đọc/copy secret thật sang file test.
- Sửa production code để test pass mà chưa được duyệt.
- Đặt tên test có DoesNotModify/ReadOnly nhưng không assert DB unchanged.
- Đặt tên MatchesName nhưng token không nằm trong department name.
- Nếu endpoint thiếu authorization, đổi expected thành OK để pass thay vì report security issue.
```

Nếu không chắc chắn, phải dừng lại và hỏi người dùng.
