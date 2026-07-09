# PROMPT AI — TẠO TEST CODE THẬT CHO UC-62 VIEW LIST FAQ CỦA HO (UNIT TEST + INTEGRATION TEST, KHÔNG DOCKER, AN TOÀN DATABASE) — v1

> File này dùng để đưa cho AI Agent/Code Agent khi cần tạo hoặc cập nhật test tự động cho chức năng **View List FAQ** trong dự án PEMS.
>
> Use case này là **danh sách FAQ trong màn quản trị của HO**, không phải trang FAQ public.
>
> Mục tiêu: **tạo test code thật, chạy được, đúng nghiệp vụ View List FAQ dưới góc nhìn HO, nhưng tuyệt đối không được làm hỏng `pems_db` hoặc bất kỳ database dev/thật nào**.

---

## 0. Bối cảnh và những kinh nghiệm phải kế thừa

Prompt này kế thừa toàn bộ kinh nghiệm đã chốt từ các prompt/test **Create FAQ** và **Update FAQ**:

```text
- Dùng xUnit.
- Dùng WebApplicationFactory cho Integration Test.
- Dùng TestAuthHandler để giả lập đăng nhập theo role.
- Dùng database test riêng, ví dụ pems_test.
- Không dùng Docker/Testcontainers.
- Không dùng appsettings.Development.json.
- Không đọc/copy/in secret thật.
- Không tự ý sửa production code để ép test pass.
- Tên test phải nói đúng hành vi thật sự được assert.
- Integration Test dùng chung pems_test phải tắt parallelization.
- Mỗi use case/test class phải có prefix dữ liệu test riêng, không dùng chung prefix cleanup.
- Cleanup không được xóa dữ liệu test class/use case khác.
```

Bài học đặc biệt từ lỗi race condition Create FAQ / Update FAQ:

```text
Không dùng chung một hằng số kiểu FaqQuestionPrefix cho nhiều use case.
Không cleanup bằng prefix rộng như [IT-FAQ], [IT-UC63], [TEST].
Không dùng prefix overlap nhau, ví dụ [IT-FAQ] và [IT-FAQ-VIEW-LIST].
Mỗi test class phải có prefix riêng, đủ rõ nghĩa, không trùng, không bao phủ prefix khác.
```

Prefix gợi ý cho use case này:

```text
[IT-UC62-VIEW-LIST-FAQ]
```

Nếu source/docs hiện tại dùng UC ID khác cho View List FAQ, vẫn giữ tên nghiệp vụ trong prefix để tránh nhầm:

```text
[IT-VIEW-LIST-FAQ]
```

---

## 0.1. Lưu ý quan trọng về UC ID

Người dùng đang yêu cầu **UC-62 — View List FAQ**.

Tuy nhiên trong tài liệu PEMS có thể tồn tại mâu thuẫn lịch sử về UC ID FAQ Management. AI Agent không được tự ý suy đoán hoặc đổi UC ID.

Quy tắc:

```text
- Ưu tiên yêu cầu hiện tại của người dùng: UC-62 View List FAQ dưới góc nhìn HO.
- Search source/docs hiện tại để xác nhận mapping UC ID.
- Nếu source/docs có mâu thuẫn, báo rõ trong report.
- Không hardcode UC ID vào logic test nếu source không dùng.
- Tên folder/test nên dùng tên nghiệp vụ ổn định: Faqs/ViewListFaq.
```

Gợi ý mapping cần đối chiếu:

```text
UC-05  View FAQ public        -> GET /api/public/faqs, public/anonymous.
UC-62  View List FAQ của HO   -> GET /api/faqs, HO quản lý FAQ.
UC-63  Create FAQ             -> POST /api/faqs, HO tạo FAQ.
UC-64  Update FAQ             -> PUT /api/faqs/{faqId}, HO cập nhật FAQ.
UC-65/67 hoặc tên tương đương -> Change FAQ Visibility, HO đổi PUBLISHED/HIDDEN.
UC-66/68 hoặc tên tương đương -> Search FAQ nếu tách riêng.
```

Nếu tài liệu cũ ghi View List FAQ là UC-64, không đổi tên task ngay. Hãy ghi nhận là **UC mapping mismatch** và vẫn đặt folder theo nghiệp vụ:

```text
tests/PEMS.UnitTests/Faqs/ViewListFaq/
tests/PEMS.IntegrationTests/Faqs/ViewListFaq/
```
Ai sẽ đọc file này?

File này phải dễ hiểu cho nhiều bộ phận:

```text
Product/BA  -> hiểu test đang kiểm tra nghiệp vụ gì.
Dev         -> biết cần tạo/sửa test code ở đâu.
Tester/QA   -> biết test nào là Unit Test, test nào là Integration Test.
Reviewer    -> biết phạm vi được sửa, không được sửa, và cách kiểm tra pass/fail.
AI Agent    -> biết chính xác phải làm gì, không được tự đoán.
```

Không viết test theo kiểu lý thuyết. Không chỉ liệt kê test case. Phải tạo hoặc cập nhật file test thật trong source code.
---

## 1. Mục tiêu của task

Tạo test tự động cho chức năng **HO xem danh sách FAQ trong màn quản trị**.

Test cần gồm 2 nhóm:

```text
1. Unit Test
   Kiểm tra logic nhỏ, chủ yếu là query validator, query parameter normalization, mapping/filter/sort helper nếu có.
   Không gọi API thật.
   Không dùng database thật.

2. Integration Test
   Kiểm tra API list FAQ chạy qua nhiều layer thật:
   API + Authentication/Authorization giả + Controller + MediatR + Query Handler + database test riêng.
```

Sau khi hoàn thành, team phải biết rõ:

```text
- Test code đã được tạo/sửa ở đâu.
- Test nào là Unit Test.
- Test nào là Integration Test.
- Test nào pass.
- Test nào fail.
- Nếu test nào chưa chạy được thì lý do là gì.
```

Không viết test kiểu lý thuyết. Không chỉ liệt kê test case. Phải tạo hoặc cập nhật file test thật trong source code.

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
Report 3.1_UCS_Template.docx nếu cần đối chiếu UC/spec
Report 5.2_L1-UnitTests_Template.xlsx nếu cần đối chiếu format unit test report
Report 5.2_L2-IntegrationTests_Template.xlsx nếu cần đối chiếu format integration test report
Source code backend hiện tại
Existing FAQ tests hiện tại: Create FAQ, Update FAQ, Change Visibility nếu đã có
Existing test infrastructure: PemsWebApplicationFactory, TestAuthHandler, DatabaseResetHelper
SQL fresh-create mới nhất trong docs/database/scripts/
```

### 2.2. Source code bắt buộc kiểm tra

AI Agent phải tự search đúng file trong project, không được bịa path.

Tối thiểu cần kiểm tra:

```text
Backend controller liên quan FAQ, đặc biệt FaqsController.
View List FAQ Query / QueryHandler / Validator nếu có.
Request/query parameter DTO của list FAQ nếu có.
Response DTO item/page result của list FAQ.
Public FAQ query/handler để không nhầm sang endpoint public.
Create FAQ / Update FAQ / Change Visibility để hiểu dữ liệu faqs và convention.
Faq entity.
EF Configuration của faqs nếu có.
ApplicationDbContext.
Constants/Enums liên quan FAQ type/status.
Authorization/Role check liên quan FAQ.
Existing Unit Tests: tests/PEMS.UnitTests/Faqs/...
Existing Integration Tests: tests/PEMS.IntegrationTests/Faqs/...
Existing test infrastructure: PemsWebApplicationFactory, TestAuthHandler, DatabaseResetHelper.
SQL fresh-create mới nhất trong docs/database/scripts/.
```

Nếu tên class/path trong project khác ví dụ trong file này, thì dùng tên thật trong source và báo lại trong report.

---

## 3. Nghiệp vụ View List FAQ phải giữ đúng

### 3.1. UC này là list quản trị của HO, không phải public FAQ

Không được nhầm với:

```text
UC-05 Public FAQ:
GET /api/public/faqs
Public/anonymous xem FAQ đã PUBLISHED.

UC-62 View List FAQ của HO:
GET /api/faqs
HO xem danh sách FAQ quản lý.
```

Quy tắc quan trọng:

```text
- Endpoint quản trị của HO phải yêu cầu đăng nhập.
- Chỉ HO được xem danh sách FAQ quản lý.
- Không dùng PublicContentController hoặc endpoint public để test UC này.
- HO list phải thấy cả FAQ PUBLISHED và HIDDEN.
- Public FAQ chỉ thấy PUBLISHED, nhưng đó là UC khác, không phải trọng tâm prompt này.
```

### 3.2. Actor hợp lệ

```text
Chỉ HO được xem danh sách FAQ quản lý.
```

Backend phải tự kiểm tra quyền. Frontend ẩn/hiện menu không đủ để bảo mật.

Các role khác gọi API trực tiếp phải bị chặn.

```text
Không đăng nhập -> 401 Unauthorized.
Đăng nhập nhưng không phải HO -> 403 Forbidden.
HO hợp lệ -> được xem danh sách FAQ.
```

Không mặc định Admin có toàn quyền. Nếu nghiệp vụ hiện tại là HO-only thì Admin phải bị 403 giống Create FAQ / Update FAQ.

Nên test đại diện các role không có quyền:

```text
Admin
Staff
StaffLeader
Visitor
```

Nếu test infrastructure có Department/Student và source quan trọng, có thể thêm, nhưng không test lan man chỉ để tăng số lượng.

### 3.3. Endpoint/API phải lấy từ source thật

Không được tự bịa route.

AI Agent phải search trong `FaqsController` hoặc controller tương đương để xác định endpoint thật, ví dụ có thể là:

```text
GET /api/faqs
GET /api/faqs?...
GET /api/faqs/list
GET /api/faqs/management
```

Dùng đúng endpoint hiện tại trong source. Nếu endpoint khác ví dụ, báo lại trong report.

Không dùng:

```text
GET /api/public/faqs
PublicContentController
```

trừ khi đang đối chiếu để chứng minh đây không phải public endpoint.

### 3.4. Dữ liệu FAQ theo schema/source hiện tại

FAQ trong schema v10 hiện tại cần đối chiếu:

```text
Bảng: faqs
Không còn faqs.language_code nếu source/schema mới đã bỏ.
faq_type dùng enum nhóm chức năng hệ thống.
status dùng PUBLISHED / HIDDEN.
Dynamic permissions đã bị bỏ; không dùng permissions/role_permissions runtime.
```

FAQ type hợp lệ hiện tại:

```text
ACCOUNT_ACCESS
VISIT_REQUEST
DELEGATION_MANAGEMENT
LOGISTICS_RESOURCE
DOCUMENT_MEDIA
NOTIFICATION_EMAIL
OTHER
```

Status hợp lệ:

```text
PUBLISHED
HIDDEN
```

Không dùng enum/status legacy nếu source/schema hiện tại đã bỏ:

```text
PROGRAM
TUITION_FEE
VISA
DORMITORY
VISIBLE
Visible
Draft
languageCode
language_code
```

Nếu source hiện tại vẫn có trường/enum cũ, không tự sửa production code. Báo mismatch và test theo source thật hoặc hỏi lại nếu rủi ro.

### 3.5. Dữ liệu HO list phải trả

AI Agent phải đọc response DTO thật trước khi assert. Không được tự bịa field.

Các field thường cần có trong list quản trị nếu source trả:

```text
faqId / id
faqType
faqTypeLabel nếu source có
question
answer nếu list quản trị cần hiển thị/truyền cho edit modal
displayOrder nếu source có
status
statusLabel nếu source có
createdAt
createdBy
updatedAt
updatedBy
updatedByName nếu source có join user
canEdit/canChangeVisibility/action flags nếu source có
```

Quy tắc:

```text
- Không assert field không tồn tại trong response DTO.
- Không assert languageCode nếu source/schema mới đã bỏ.
- Không dùng raw label tiếng Việt làm DB value.
- Nếu list DTO chỉ trả summary, không ép phải trả answer.
- Nếu UI cần edit modal lấy answer từ detail endpoint riêng, list không bắt buộc có answer. Hãy theo source thật.
```

### 3.6. PUBLISHED và HIDDEN trong HO management list

HO management list phải dùng cho quản trị, nên phải hiển thị cả:

```text
PUBLISHED
HIDDEN
```

Nếu chỉ trả PUBLISHED giống public FAQ thì đó là lỗi nghiệp vụ hoặc đang gọi nhầm endpoint public.

Test nên seed ít nhất 2 FAQ có cùng prefix:

```text
1 FAQ status PUBLISHED
1 FAQ status HIDDEN
```

Sau đó HO gọi list và assert cả hai record xuất hiện nếu source endpoint trả toàn bộ list quản trị.

### 3.7. Soft delete nếu source/schema có

Nếu bảng `faqs` hoặc entity có soft delete, ví dụ:

```text
is_deleted
deleted_at
deleted_by
status = DELETED
```

thì HO list không được hiển thị FAQ đã soft-deleted.

Chỉ viết test soft-delete nếu source/schema hiện tại thật sự có field/rule này. Không tự bịa soft delete.

### 3.8. Search/filter/sort/pagination

UC-62 là **View List FAQ**. Trong một số source, `GET /api/faqs` có thể gộp cả search/filter/sort/pagination. Trong tài liệu khác, Search FAQ có thể là use case riêng.

Quy tắc:

```text
- AI Agent phải đọc source thật để biết GET /api/faqs nhận query params nào.
- Không tự bịa query params.
- Nếu source hiện tại gộp search/filter/sort vào GET /api/faqs, test các case đại diện trong Integration Test.
- Nếu Search FAQ là UC riêng và endpoint riêng, không test sâu search ở prompt này; chỉ test list cơ bản và authorization.
```

Các query params có thể tồn tại, phải verify source trước:

```text
page
pageNumber
pageSize
search
keyword
faqType
status
sortBy
sortDirection
```

Rule nếu có pagination:

```text
page phải là số dương.
pageSize phải là số dương.
pageSize có max limit nếu source có.
Response phải có total/page/pageSize/items hoặc convention tương đương.
```

Rule nếu có filter:

```text
faqType filter chỉ nhận enum hiện tại.
status filter chỉ nhận PUBLISHED/HIDDEN.
Filter status trong HO list được phép trả PUBLISHED hoặc HIDDEN tùy chọn.
```

Rule nếu có search:

```text
Search trim keyword.
Search case-insensitive nếu source/spec có.
Search có thể áp dụng vào Question/Answer/FaqType theo source thật.
Search rỗng thì không filter.
```

Rule nếu có sort:

```text
SortBy phải whitelist field hợp lệ.
SortDirection chỉ asc/desc hoặc convention source thật.
Không nối chuỗi SQL trực tiếp từ sortBy/sortDirection.
```

### 3.9. Read-only behavior

View List FAQ là API đọc dữ liệu.

Khi gọi thành công:

```text
- Không được tạo FAQ mới.
- Không được sửa question/answer/faqType/status.
- Không được refresh updated_at/updated_by.
- Không được thay đổi audit fields.
```

Nếu đặt tên test có `DoesNotModify` hoặc `ReadOnly`, test phải seed dữ liệu, chụp snapshot trước GET, gọi GET, reload DB và assert record không đổi.

---

## 4. Phân biệt rõ Unit Test và Integration Test

### 4.1. Unit Test phù hợp với View List FAQ

Unit Test chỉ kiểm tra logic nhỏ, cô lập.

Chỉ tạo Unit Test nếu source có:

```text
ViewListFaqQueryValidator
GetFaqListQueryValidator
FaqListFilterValidator
FaqListSortHelper
FaqListQueryParameterNormalizer
FaqListDtoMapper thuần
```

Các case Unit Test phù hợp:

```text
ValidQuery_NoErrors
Page_DefaultOrPositive_NoError
Page_Zero_HasError nếu validator hiện tại chặn
Page_Negative_HasError nếu source dùng int
PageSize_Zero_HasError nếu validator hiện tại chặn
PageSize_AboveMax_HasError nếu source có max
FaqType_Allowed_NoError
FaqType_Invalid_HasError
Status_Allowed_NoError
Status_Invalid_HasError
SortBy_Allowed_NoError nếu source có sort
SortBy_NotAllowed_HasError nếu source có sort whitelist
SortDirection_Invalid_HasError nếu source có sort
Keyword_Whitespace_TreatedAsEmpty nếu source có normalizer/helper
```

Không ép Unit Test nếu list query không có validator hoặc validator chỉ là pass-through.

### 4.2. Integration Test phù hợp với View List FAQ

Integration Test kiểm tra API thật:

```text
HTTP request
-> Authentication/Authorization
-> Controller
-> MediatR Query
-> Handler
-> EF Core
-> DB test
-> Response DTO
```

Integration Test phù hợp cho:

```text
Anonymous / role không có quyền.
HO gọi list thành công.
PUBLISHED và HIDDEN đều xuất hiện trong management list.
Endpoint không dùng public behavior.
Paging/filter/search/sort nếu source hỗ trợ.
Invalid query params -> BadRequest nếu validator source có.
GET list không modify DB.
```

---

## 5. Quy ước tổ chức thư mục test

### 5.1. Unit Test folder

```text
tests/PEMS.UnitTests/Faqs/ViewListFaq/
```

File gợi ý:

```text
ViewListFaqQueryValidatorTests.cs
```

Hoặc dùng tên thật theo source:

```text
GetFaqListQueryValidatorTests.cs
ViewFAQListQueryValidatorTests.cs
```

### 5.2. Integration Test folder

```text
tests/PEMS.IntegrationTests/Faqs/ViewListFaq/
```

File gợi ý:

```text
ViewListFaqApiTests.cs
```

Hoặc tên theo source thật:

```text
GetFaqListApiTests.cs
FaqListApiTests.cs
```

### 5.3. Helper dùng chung và prefix dữ liệu test

Ưu tiên dùng lại:

```text
tests/PEMS.IntegrationTests/TestInfrastructure/PemsWebApplicationFactory.cs
tests/PEMS.IntegrationTests/TestInfrastructure/TestAuthHandler.cs
tests/PEMS.IntegrationTests/TestInfrastructure/DatabaseResetHelper.cs
```

Nếu cần bổ sung helper seed FAQ cho View List FAQ, bổ sung vào `DatabaseResetHelper` hoặc helper chung tương đương. Không copy helper lặp lại trong từng file test.

Helper nên hỗ trợ:

```text
- Tạo FAQ test với prefix do test class truyền vào.
- Tạo FAQ test với status PUBLISHED/HIDDEN.
- Tạo FAQ test với faqType khác nhau.
- Tạo FAQ test với created_at/updated_at tùy chỉnh nếu cần test sort.
- Trả về faqId.
- Cleanup record theo đúng prefix được truyền vào.
- Không hardcode FaqQuestionPrefix dùng chung cho mọi FAQ test.
- Không cleanup prefix của use case/test class khác.
```

Ví dụ hướng thiết kế:

```csharp
public const string ViewListFaqQuestionPrefix = "[IT-UC62-VIEW-LIST-FAQ] ";

CreateTestFaqAsync(db, question, answer, faqType, status, createdAt, createdBy, updatedAt, updatedBy)
DeleteTestFaqsAsync(db, prefix)
```

Hoặc dùng prefix truyền vào:

```csharp
CreateTestFaqAsync(db, prefix, questionSuffix, answer, faqType, status)
DeleteTestFaqsAsync(db, prefix)
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
- Lỗi có thể biểu hiện thành NotFound, DbUpdateConcurrencyException, duplicate/filter/sort trả sai.
- Đây là lỗi test infrastructure, không phải lỗi production code.
```

Dù đã tắt parallelization, vẫn phải tách prefix theo use case/test class. Tắt parallelization là lớp an toàn runtime; tách prefix là lớp an toàn dữ liệu và ngữ nghĩa.

---

## 6. Phạm vi AI Agent được sửa

AI Agent được phép tạo/sửa:

```text
tests/PEMS.UnitTests/...
tests/PEMS.IntegrationTests/...
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
SMTP password
JWT secret
OAuth client secret
Refresh token
API key
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

Ưu tiên dùng lại test infrastructure đã tạo cho FAQ tests.

Yêu cầu:

```text
- WebApplicationFactory chạy API PEMS trong environment Testing.
- Không dùng appsettings.Development.json.
- Override authentication bằng test scheme.
- Cho phép tạo request giả với role HO, Admin, Staff, StaffLeader, Visitor.
- Cho phép gọi request không đăng nhập để test 401.
- Connection string phải trỏ tới database test riêng.
- Không dùng Docker.
- Không dùng Testcontainers.
- Không gọi service ngoài thật như Google SSO, SMTP, Google Drive.
```

Nếu `EffectiveRole.StaffLeader` chưa có:

```text
Bổ sung mapping đúng:
EffectiveRole.StaffLeader => RoleCode.Staff + SubRole.Leader.
EnsureTestUserAsync phải gán department_id cho StaffLeader nếu DB trigger yêu cầu role_code = STAFF phải có department_id.
```

Không tạo user/test session bằng dữ liệu thật.

---

## 8. QUY TẮC AN TOÀN DATABASE CHO INTEGRATION TEST

Phần này là bắt buộc. Không được bỏ qua.

### 8.1. Nguyên tắc số 1

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

### 8.2. Không tự động tạo/drop/import database nếu chưa được xác nhận

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

### 8.3. Không import trực tiếp SQL fresh-create gốc

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

### 8.4. Không đọc/copy/in secret thật

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

## 9. Nguyên tắc chọn test case vừa đủ

Không viết test theo kiểu bao phủ máy móc mọi biến thể. Chỉ tạo test có ý nghĩa theo source thật và rủi ro chính của use case.

Một use case được coi là đủ test khi đã kiểm tra được:

```text
1. Ai được phép thực hiện.
2. Ai không được phép thực hiện.
3. Dữ liệu hợp lệ thì hệ thống xử lý đúng.
4. Query/filter/sort/pagination quan trọng nếu source hỗ trợ.
5. Dữ liệu trả về đúng phạm vi nghiệp vụ.
6. API đọc không ghi/sửa DB.
7. Rủi ro bảo mật chính như authorization, scope dữ liệu, SQL injection qua sort/filter được kiểm soát nếu source có input tương ứng.
```

Không tạo test thừa chỉ để tăng số lượng. Nếu một test không làm rõ thêm authorization, response scope, pagination/filter/sort, DB state hoặc security, không cần viết.

---

## 10. Unit Test cần tạo

Tạo Unit Test theo source thật hiện tại.

### 10.1. Query validator tests

Chỉ tạo nếu source có query validator hoặc helper tương đương.

File gợi ý:

```text
tests/PEMS.UnitTests/Faqs/ViewListFaq/ViewListFaqQueryValidatorTests.cs
```

Các case tối thiểu nếu source hỗ trợ:

```text
1. ValidQuery_NoErrors
   Query mặc định hoặc query hợp lệ -> không lỗi.

2. Page_Zero_HasError
   Nếu page/pageNumber phải >= 1.

3. Page_Negative_HasError
   Nếu source dùng int và validator chặn page âm.

4. PageSize_Zero_HasError
   Nếu pageSize phải >= 1.

5. PageSize_AboveMax_HasError
   Nếu source có max page size.

6. FaqType_AnyAllowedValue_NoError
   ACCOUNT_ACCESS, VISIT_REQUEST, DELEGATION_MANAGEMENT,
   LOGISTICS_RESOURCE, DOCUMENT_MEDIA, NOTIFICATION_EMAIL, OTHER.

7. FaqType_Invalid_HasError
   Ví dụ PROGRAM/VISA nếu source/schema mới đã bỏ legacy enum.

8. Status_AnyAllowedValue_NoError
   PUBLISHED/HIDDEN nếu list có status filter.

9. Status_Invalid_HasError
   Ví dụ VISIBLE/DRAFT nếu source/schema không dùng.

10. SortBy_Allowed_NoError
    Nếu source có sortBy whitelist.

11. SortBy_NotAllowed_HasError
    Nếu source có sortBy whitelist.

12. SortDirection_Invalid_HasError
    Nếu source có sortDirection.

13. Keyword_Whitespace_TreatedAsEmpty
    Chỉ nếu source có normalizer/helper và test được cô lập.
```

Không test `question`/`answer` required trong UC này, vì View List FAQ không tạo/cập nhật content.

### 10.2. Handler/helper Unit Test

Chỉ viết nếu logic có thể test cô lập rõ ràng.

Có thể viết nếu source có helper thuần:

```text
FaqListSortFieldWhitelist
FaqListQueryNormalizer
FaqListResponseMapper
FaqType/status label mapper
```

Không ép handler Unit Test nếu handler phụ thuộc EF Core/database thật nhiều. Các case DB như PUBLISHED/HIDDEN, filter, pagination, sorting nên ưu tiên Integration Test.

---

## 11. Integration Test cần tạo

Integration Test đặt trong:

```text
tests/PEMS.IntegrationTests/Faqs/ViewListFaq/
```

File gợi ý:

```text
ViewListFaqApiTests.cs
```

### 11.1. Setup dữ liệu FAQ cho View List tests

View List FAQ là API đọc nhiều record. Để test ổn định:

```text
- Seed FAQ test với prefix riêng của View List FAQ.
- Không dùng dữ liệu seed thật làm điều kiện assert chính.
- Nếu endpoint có search/keyword filter, dùng prefix làm keyword để cô lập dữ liệu test.
- Nếu endpoint không có search, chỉ assert các record test xuất hiện, không assert total count tuyệt đối nếu DB có seed sẵn.
- Cleanup chỉ xóa FAQ có prefix riêng của View List FAQ.
```

Ví dụ prefix:

```text
[IT-UC62-VIEW-LIST-FAQ] published ...
[IT-UC62-VIEW-LIST-FAQ] hidden ...
```

### 11.2. Các case Integration Test tối thiểu

Điều chỉnh HTTP status, route, response shape theo source thật.

#### Authentication / Authorization

```text
1. Anonymous_Unauthorized
   Không gắn auth headers -> GET /api/faqs -> 401 Unauthorized.

2. Staff_Forbidden
   STAFF đã đăng nhập -> GET /api/faqs -> 403 Forbidden.

3. StaffLeader_Forbidden
   STAFF + LEADER đã đăng nhập -> GET /api/faqs -> 403 Forbidden.

4. Admin_Forbidden
   ADMIN đã đăng nhập -> GET /api/faqs -> 403 Forbidden nếu nghiệp vụ HO-only.

5. Visitor_Forbidden
   VISITOR đã đăng nhập -> GET /api/faqs -> 403 Forbidden.
```

Nếu source hiện tại cho Admin xem FAQ list vì nghiệp vụ đã đổi, không sửa production code. Báo mismatch và test theo source thật hoặc hỏi người dùng.

#### Happy path / management scope

```text
6. Ho_ReturnsFaqList
   Seed ít nhất 1 FAQ có prefix riêng.
   HO gọi endpoint list.
   Expect OK.
   Response chứa FAQ đã seed.

7. Ho_ReturnsPublishedAndHiddenFaqs
   Seed 1 FAQ PUBLISHED và 1 FAQ HIDDEN cùng prefix.
   HO gọi endpoint list.
   Expect OK.
   Response chứa cả 2 record.
   Đây là khác biệt quan trọng với public FAQ.

8. Ho_ListItem_ContainsManagementFields
   HO gọi list.
   Assert response item có các field quản trị theo DTO thật:
   faqId, faqType, question, status, createdAt/updatedAt/updatedBy... nếu source trả.
   Không assert field không có trong DTO.
   Không assert languageCode nếu source/schema đã bỏ.
```

#### Read-only behavior

```text
9. Ho_GetList_DoesNotModifyFaqs
   Seed FAQ test.
   Lưu snapshot question/answer/faqType/status/updatedAt/updatedBy.
   HO gọi GET list.
   Reload DB.
   Assert FAQ giữ nguyên.
```

Chỉ dùng tên `DoesNotModify` nếu test thật sự reload DB và assert snapshot không đổi.

#### Empty/no matching data

```text
10. Ho_FilteredPrefixNoMatch_ReturnsEmptyResult
    Chỉ viết nếu endpoint có search/keyword filter hoặc filter đủ để cô lập kết quả.
    HO gọi list với keyword/prefix không tồn tại.
    Expect OK.
    Response items rỗng, total = 0 nếu source có total.
```

Không viết test “NoFaqs_ReturnsEmptyList” nếu DB test có seed sẵn và không thể cô lập dữ liệu. Nếu muốn test no data, phải có cách filter bằng prefix không tồn tại hoặc reset DB an toàn.

#### Pagination nếu source hỗ trợ

```text
11. Ho_Pagination_ReturnsRequestedPage
    Chỉ viết nếu endpoint có pagination.
    Seed đủ FAQ có prefix riêng.
    Gọi page/pageSize theo source.
    Expect items đúng page size, metadata đúng nếu response có.
    Không assert total toàn DB nếu không filter được bằng prefix.

12. Page_Zero_BadRequest
    Chỉ viết nếu validator/source chặn page = 0 qua API.
```

#### Filter/search nếu source hỗ trợ trong cùng endpoint

```text
13. FaqType_Filter_ReturnsOnlyMatchingType
    Chỉ viết nếu GET /api/faqs có faqType filter.
    Seed các FAQ cùng prefix với nhiều faqType.
    HO gọi filter faqType.
    Response chỉ chứa faqType đó trong tập dữ liệu test.

14. Status_Filter_ReturnsOnlyMatchingStatus
    Chỉ viết nếu GET /api/faqs có status filter.
    Seed PUBLISHED/HIDDEN cùng prefix.
    HO gọi status=PUBLISHED hoặc HIDDEN.
    Response chỉ chứa status tương ứng trong tập dữ liệu test.

15. Keyword_Search_ReturnsMatchingFaqs
    Chỉ viết nếu GET /api/faqs gộp search.
    Seed keyword riêng trong question/answer.
    HO search keyword.
    Response chứa record phù hợp.

16. InvalidFaqTypeFilter_BadRequest
    Chỉ viết nếu validator/source chặn faqType invalid.

17. InvalidStatusFilter_BadRequest
    Chỉ viết nếu validator/source chặn status invalid.

18. SortBy_NotAllowed_BadRequest
    Chỉ viết nếu source có sortBy whitelist và validator chặn sortBy lạ.
```

Nếu Search FAQ là UC riêng/endpoint riêng, không test sâu các case search ở UC-62. Chỉ ghi trong report rằng search/filter thuộc use case khác.

#### Public endpoint separation

```text
19. HoManagementList_IncludesHiddenButPublicListDoesNot
    Chỉ viết nếu public endpoint đã ổn định và không làm test phụ thuộc quá nhiều vào UC-05.
    Seed FAQ PUBLISHED/HIDDEN.
    GET /api/faqs bằng HO -> chứa cả hai.
    GET /api/public/faqs anonymous -> chỉ chứa PUBLISHED.
```

Case này có giá trị cao nhưng có thể làm test UC-62 phụ thuộc public endpoint. Nếu public FAQ test đã có riêng, chỉ cần test trong UC-62 rằng HO list chứa HIDDEN.

### 11.3. Bộ tên method Integration Test gợi ý

Dùng tên semantic, ngắn gọn, không dùng số HTTP code trong tên test.

Bộ tối thiểu nên có:

```csharp
Anonymous_Unauthorized()
Staff_Forbidden()
StaffLeader_Forbidden()
Admin_Forbidden()
Visitor_Forbidden()

Ho_ReturnsFaqList()
Ho_ReturnsPublishedAndHiddenFaqs()
Ho_ListItem_ContainsManagementFields()
Ho_GetList_DoesNotModifyFaqs()
```

Bổ sung nếu source hỗ trợ:

```csharp
Ho_Pagination_ReturnsRequestedPage()
Page_Zero_BadRequest()

FaqType_Filter_ReturnsOnlyMatchingType()
Status_Filter_ReturnsOnlyMatchingStatus()
Keyword_Search_ReturnsMatchingFaqs()
InvalidFaqTypeFilter_BadRequest()
InvalidStatusFilter_BadRequest()
SortBy_NotAllowed_BadRequest()
```

Không dùng tên sai/hứa quá mức:

```text
Ho_GetList_DoesNotModifyFaqs    // sai nếu test chỉ assert OK, không reload DB.
Ho_ListItem_ContainsAllFields   // sai nếu không assert đủ "all fields".
Returns200
Returns403
```

---

## 12. Quy tắc assert response list

Vì response shape có thể là `ApiResponse<PagedResult<FaqDto>>`, `PaginatedList<T>`, `items`, `data.items`, hoặc shape khác, AI Agent phải đọc source thật.

Không hardcode JSON path theo suy đoán.

Khi assert list:

```text
- Parse theo response DTO thật nếu có thể.
- Nếu dùng JsonDocument, kiểm tra path đúng theo response thật.
- Không assert exact total nếu database có seed sẵn và không filter/cô lập bằng prefix.
- Ưu tiên assert "contains seeded faqId/question" thay vì "count == N" nếu DB có dữ liệu khác.
- Nếu có prefix/search filter riêng, có thể assert total/count chính xác hơn.
```

Ví dụ kiểm tra presence:

```text
Response items phải chứa question bắt đầu bằng [IT-UC62-VIEW-LIST-FAQ].
Response items phải chứa cả faqId của FAQ PUBLISHED và FAQ HIDDEN đã seed.
```

Ví dụ kiểm tra không lộ legacy field:

```text
Nếu source/schema mới đã bỏ languageCode, không được assert response có languageCode.
Nếu cần kiểm tra không lộ languageCode, chỉ làm nếu response dùng JSON và source DTO không có field này.
```

---

## 13. Commands được phép chạy

### 13.1. Luôn được chạy nếu không động DB

```bash
dotnet build
dotnet test tests/PEMS.UnitTests/PEMS.UnitTests.csproj
```

Nếu đường dẫn project khác, dùng đường dẫn thật trong source.

### 13.2. Chỉ chạy Integration Test sau khi đạt DB safety gate

Chỉ được chạy:

```bash
dotnet test tests/PEMS.IntegrationTests/PEMS.IntegrationTests.csproj
```

khi đã thỏa mãn:

```text
- Database test riêng đã xác định rõ là pems_test hoặc tên test DB khác.
- Connection string Testing không trỏ tới pems_db/dev DB.
- Không dùng appsettings.Development.json.
- SQL script đã được scan an toàn nếu cần import.
- Người dùng đã xác nhận cho phép chạy Integration Test có DB.
```

Nếu chưa đủ điều kiện, không chạy Integration Test. Hãy báo rõ:

```text
Integration Test code đã tạo/cập nhật nhưng chưa chạy vì chưa có xác nhận an toàn database.
```

Sau khi được phép chạy, nên chạy:

```bash
dotnet test tests/PEMS.IntegrationTests/PEMS.IntegrationTests.csproj --filter "FullyQualifiedName~ViewListFaqApiTests"
dotnet test tests/PEMS.IntegrationTests/PEMS.IntegrationTests.csproj --filter "FullyQualifiedName~CreateFaqApiTests"
dotnet test tests/PEMS.IntegrationTests/PEMS.IntegrationTests.csproj --filter "FullyQualifiedName~UpdateFaqApiTests"
dotnet test tests/PEMS.IntegrationTests/PEMS.IntegrationTests.csproj
```

Mục tiêu là phát hiện cả lỗi trong test class mới và lỗi tương tác/race condition với các test FAQ cũ.

---

## 14. Output/report sau khi làm

Sau khi hoàn thành, báo cáo bằng tiếng Việt theo format:

```md
# Báo cáo tạo test UC-62 View List FAQ

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
| UC ID trong yêu cầu | UC-62 View List FAQ |
| UC ID trong source/docs nếu khác | ... |
| Endpoint list FAQ thật | ... |
| Request query params thật | ... |
| Response DTO thật | ... |
| Actor hợp lệ | ... |
| Public endpoint khác gì management endpoint | ... |
| Có search/filter trong cùng endpoint không? | ... |
| Có pagination/sort không? | ... |

## 4. Unit Test đã tạo
[Liệt kê case]

## 5. Integration Test đã tạo
[Liệt kê case]

## 6. Kiểm tra an toàn database
| Mục | Kết quả |
|---|---|
| Database test dự kiến | pems_test hoặc tên thật |
| Có dùng pems_db không? | Không |
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

## 9. Production code issue nếu có
[Chỉ báo cáo, không tự sửa nếu chưa được duyệt]

## 10. Việc cần người dùng xác nhận thêm
[Nếu còn]
```

Không được báo “hoàn thành” nếu chưa build/test hoặc chưa nói rõ lý do không chạy được.

---

## 15. Definition of Done

Task chỉ hoàn thành khi đạt đủ các điều kiện sau:

```text
- Test code thật đã được tạo/cập nhật.
- Unit Test, nếu có, nằm đúng tests/PEMS.UnitTests/Faqs/ViewListFaq/.
- Integration Test nằm đúng tests/PEMS.IntegrationTests/Faqs/ViewListFaq/.
- Không trộn Unit Test và Integration Test.
- Không dùng database dev/thật.
- Không import SQL fresh-create gốc trực tiếp.
- Không đọc/copy/in secret thật.
- Không tự ý sửa production code để ép test pass.
- Unit Test đã chạy hoặc báo rõ lý do chưa chạy.
- Integration Test chỉ chạy khi đạt DB safety gate và có xác nhận.
- Test names rõ nghĩa, không dùng số HTTP nếu có thể.
- Authorization test bao phủ Anonymous và các role không phải HO đại diện.
- HO list test xác nhận lấy đúng endpoint management, không nhầm public endpoint.
- HO list test xác nhận HIDDEN FAQ xuất hiện trong management list nếu source nghiệp vụ hiện tại yêu cầu.
- Test read-only chỉ dùng tên DoesNotModify/ReadOnly nếu thật sự assert DB unchanged.
- Nếu có pagination/filter/sort/search trong source, test các case đại diện và invalid query quan trọng.
- Nếu search/filter là UC riêng, report rõ không test sâu ở UC-62.
- Integration Test assembly đã tắt parallelization hoặc report rõ lý do chưa thêm.
- View List FAQ không dùng chung cleanup prefix với Create/Update/Visibility FAQ.
- DatabaseResetHelper seed/cleanup nhận prefix riêng hoặc có helper riêng tương đương.
- Chạy riêng ViewListFaqApiTests và chạy toàn bộ IntegrationTests nếu được phép để phát hiện race condition.
- Báo cáo pass/fail rõ ràng.
```

---

## 16. Nhắc lại các lỗi nghiêm cấm

Không được lặp lại các lỗi sau:

```text
- Dùng GET /api/public/faqs để test UC-62 HO View List FAQ.
- Viết test HO list nhưng chỉ thấy PUBLISHED, bỏ qua HIDDEN mà không giải thích.
- Tự bịa query params page/search/status/sortBy khi source không có.
- Assert exact total count khi DB có seed khác và không cô lập bằng prefix/filter.
- Dùng chung FaqQuestionPrefix với Create FAQ hoặc Update FAQ.
- Cleanup theo prefix quá rộng khiến test class này xóa dữ liệu test class khác.
- Để xUnit chạy song song các Integration Test class dùng chung pems_test.
- Thấy NotFound/DbUpdateConcurrencyException khi chạy toàn bộ test rồi vội sửa production code trước khi kiểm tra race condition test infrastructure.
- Chạy SQL fresh-create gốc khi chưa kiểm tra có USE pems_db.
- Tin rằng `mysql pems_test < script.sql` chắc chắn chỉ tác động pems_test.
- Dùng appsettings.Development.json cho Integration Test.
- Đọc/copy secret thật sang file test.
- Sửa production code để test pass mà chưa được duyệt.
- Đặt tên test có DoesNotModify/ReadOnly nhưng không assert DB unchanged.
- Test Create/Update/Visibility behavior sâu trong UC-62 nếu đã thuộc use case khác, trừ khi chỉ seed dữ liệu phục vụ list.
```

Nếu không chắc chắn, phải dừng lại và hỏi người dùng.
