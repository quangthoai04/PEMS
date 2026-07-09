# PROMPT AI — TẠO TEST CODE THẬT CHO UC-66 SEARCH FAQ CỦA HO (UNIT TEST + INTEGRATION TEST, KHÔNG DOCKER, AN TOÀN DATABASE) — v1

> File này dùng để đưa cho AI Agent/Code Agent khi cần tạo hoặc cập nhật test tự động cho chức năng **Search FAQ** trong dự án PEMS.
>
> Use case này mặc định là **Search FAQ trong màn quản trị FAQ của HO**, không phải trang FAQ public. Nếu source/docs hiện tại chứng minh Search FAQ là public use case hoặc dùng endpoint khác, phải dừng lại báo rõ mismatch và hỏi người dùng trước khi code.
>
> Mục tiêu: **tạo test code thật, chạy được, đúng nghiệp vụ Search FAQ, không trùng lặp máy móc với UC-62 View List FAQ, và tuyệt đối không làm hỏng `pems_db` hoặc bất kỳ database dev/thật nào**.

---

## 0. Bối cảnh và kinh nghiệm phải kế thừa

Prompt này kế thừa chuẩn đã chốt từ các prompt/test **Create FAQ**, **Update FAQ** và **UC-62 View List FAQ**:

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
- Mỗi use case/test class phải có prefix dữ liệu test riêng.
- Cleanup không được xóa dữ liệu test class/use case khác.
- Nếu một test dùng tên DoesNotModify/ReadOnly thì phải thật sự reload DB và assert unchanged.
- Nếu lỗi xuất hiện khi chạy toàn bộ IntegrationTests, phải kiểm tra race condition/test cleanup trước khi nghi production code.
```

Bài học đặc biệt từ lỗi race condition giữa các FAQ test:

```text
Không dùng chung một hằng số kiểu FaqQuestionPrefix cho nhiều use case.
Không cleanup bằng prefix rộng như [IT-FAQ], [IT-UC63], [TEST].
Không dùng prefix overlap nhau, ví dụ [IT-FAQ] và [IT-FAQ-SEARCH].
Mỗi test class phải có prefix riêng, đủ rõ nghĩa, không trùng, không bao phủ prefix khác.
```

Prefix gợi ý cho use case này:

```text
[IT-UC66-SEARCH-FAQ]
```

Nếu source/docs hiện tại dùng UC ID khác cho Search FAQ, vẫn giữ tên nghiệp vụ trong prefix để tránh nhầm:

```text
[IT-SEARCH-FAQ]
```

---

## 0.1. Lưu ý quan trọng về UC ID và quan hệ với UC-62

Người dùng đang yêu cầu **UC-66 — Search FAQ**.

Trong tài liệu PEMS có thể tồn tại mâu thuẫn lịch sử về UC ID FAQ Management. AI Agent không được tự ý suy đoán hoặc đổi UC ID.

Quy tắc:


```text
- Ưu tiên yêu cầu hiện tại của người dùng: UC-66 Search FAQ.
- Search source/docs hiện tại để xác nhận mapping UC ID.
- Nếu source/docs có mâu thuẫn, báo rõ trong report.
- Không hardcode UC ID vào logic test nếu source không dùng.
- Tên folder/test nên dùng tên nghiệp vụ ổn định: Faqs/SearchFaq.
```

Gợi ý mapping cần đối chiếu:

```text
UC-05  View FAQ public              -> GET /api/public/faqs, public/anonymous.
UC-62  View List FAQ       -> GET /api/faqs, HO quản lý FAQ.
UC-63  Create FAQ          -> POST /api/faqs, HO tạo FAQ.
UC-64  Update FAQ          -> PUT /api/faqs/{faqId}, HO cập nhật nội dung FAQ.
UC-65  Change FAQ Visibility -> HO đổi PUBLISHED/HIDDEN.
UC-66  Search FAQ          -> search trong quản trị FAQ nếu tách riêng.
```

Nếu source hiện tại **không có endpoint Search FAQ riêng** và search chỉ là query parameter `keyword/search` trong `GET /api/faqs`, vẫn có thể tạo test riêng cho UC-66 trong folder `Faqs/SearchFaq`, nhưng phải gọi đúng endpoint thật hiện tại.

Không được tạo endpoint giả như:

```text
GET /api/faqs/search
GET /api/search/faqs
```

nếu source không có.

Nếu các search test đã nằm trong `ViewListFaqApiTests`, AI Agent có 2 hướng hợp lệ:

```text
1. Tách/move các test search-specific sang Faqs/SearchFaq nếu team muốn report UC-66 riêng.
2. Giữ nguyên test hiện tại và report mapping UC-66 -> các method search-specific đã có, không duplicate lại y hệt.
```

Không copy cùng một test giống hệt vào cả UC-62 và UC-66 chỉ để tăng số lượng.

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

Tạo test tự động cho chức năng **HO search FAQ trong màn quản trị FAQ**.

Test cần gồm 2 nhóm:

```text
1. Unit Test
   Chỉ kiểm tra logic nhỏ, chủ yếu là query validator, keyword normalizer/helper nếu source có.
   Không gọi API thật.
   Không dùng database thật.

2. Integration Test
   Kiểm tra search FAQ chạy qua nhiều layer thật:
   API + Authentication/Authorization giả + Controller + MediatR + Query Handler + database test riêng.
```

Mục tiêu của UC-66 là kiểm tra **search behavior**, không phải test lại toàn bộ UC-62 View List FAQ.

Sau khi hoàn thành, team phải biết rõ:

```text
- Search FAQ đang dùng endpoint nào trong source thật.
- Search FAQ là endpoint riêng hay là query param của GET /api/faqs.
- Search dùng param tên gì: keyword, search, q hoặc tên khác.
- Search scope thật là gì: Question, Answer, FaqType hoặc scope khác.
- Search có trim/case-insensitive/contains matching không.
- Search có kết hợp với faqType/status filter theo AND logic không.
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
Existing tests hiện tại, đặc biệt Create/Update/ViewList FAQ tests đã làm xong
SQL fresh-create mới nhất trong docs/database/scripts/

```

### 2.2. Source code bắt buộc kiểm tra

AI Agent phải tự search đúng file trong project, không được bịa path.

Tối thiểu cần kiểm tra:

```text
Backend controller liên quan FAQ: FaqsController hoặc controller tương đương
Public FAQ controller nếu có: PublicContentController hoặc controller tương đương
ViewListFAQQuery / SearchFAQQuery nếu có
ViewListFAQQueryHandler / SearchFAQQueryHandler nếu có
ViewListFAQQueryValidator / SearchFAQQueryValidator nếu có
ViewListFAQDto / response DTO thật
Faq entity
EF Configuration của faqs
ApplicationDbContext
Constants/Enums liên quan FAQ type/status
Authorization/Role check liên quan FAQ
Existing Unit Tests: tests/PEMS.UnitTests/Faqs/ViewListFaq/...
Existing Integration Tests: tests/PEMS.IntegrationTests/Faqs/ViewListFaq/...
Existing test infrastructure: PemsWebApplicationFactory, TestAuthHandler, DatabaseResetHelper
SQL fresh-create mới nhất trong docs/database/scripts/
```

Nếu tên class/path trong project khác ví dụ trong file này, dùng tên thật trong source và ghi rõ trong report.

### 2.3. Source-first rule

Không được tự bịa:

```text
- endpoint search
- query param keyword/search/q
- response DTO
- pagination metadata
- FAQ type/status enum
- role được phép
- search scope
- case-sensitive/case-insensitive behavior
- trim behavior
- SQL table/field
```

Nếu source và tài liệu mâu thuẫn, nhớ hỏi lại, không được suy đoán:

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

## 3. Nghiệp vụ Search FAQ phải giữ đúng

### 3.1. Actor hợp lệ

Với giả định UC-66 là **Search FAQ trong màn quản trị HO**:

```text
Chỉ HO được search FAQ trong endpoint quản trị.
```

Backend phải tự kiểm tra quyền. Frontend ẩn/hiện ô search không đủ để bảo mật.

Các role khác gọi API trực tiếp phải bị chặn:

```text
Không đăng nhập -> 401 Unauthorized.
Đăng nhập nhưng không phải HO -> 403 Forbidden.
HO hợp lệ -> được search FAQ nếu query hợp lệ.
```

Không mặc định Admin có toàn quyền. Nếu nghiệp vụ hiện tại là HO-only thì Admin phải bị 403 giống Create/Update/View List FAQ.

Nếu source chứng minh Search FAQ là public endpoint, không code theo giả định HO-only. Phải báo mismatch và hỏi người dùng.

### 3.2. Endpoint/API phải lấy từ source thật

Không được tự bịa route.

AI Agent phải search trong `FaqsController` hoặc controller tương đương để xác định endpoint thật.

Các khả năng thường gặp:

```text
GET /api/faqs?keyword=...
GET /api/faqs?search=...
GET /api/faqs?q=...
GET /api/faqs/search?keyword=...
GET /api/public/faqs?keyword=...
```

Dùng đúng endpoint hiện tại trong source.

Nếu source hiện tại gộp search vào `GET /api/faqs`, test UC-66 vẫn được phép gọi:

```text
GET /api/faqs?keyword=<token>
```

nhưng phải ghi rõ trong report:

```text
Search FAQ hiện không có endpoint riêng; source triển khai search như query parameter của ViewListFAQ endpoint.
```

### 3.3. Phân biệt Search FAQ quản trị và public FAQ search

Không nhầm các endpoint:

```text
GET /api/faqs             -> endpoint quản trị HO, có thể trả cả PUBLISHED và HIDDEN.
GET /api/public/faqs      -> endpoint public, anonymous/PUBLISHED-only.
```

Nếu UC-66 theo tài liệu hiện tại là Search FAQ quản trị, không dùng public endpoint để test.

Nếu UC-66 theo source/docs là public search, phải tạo prompt/test riêng cho public search và không áp dụng HO-only.

### 3.4. FAQ schema và enum hiện tại

FAQ hiện tại chỉ dùng tiếng Việt. Không còn `language_code`/`languageCode` trong FAQ DTO nếu source/schema mới đã bỏ.

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

FAQ status hợp lệ hiện tại:

```text
PUBLISHED
HIDDEN
```

Không dùng enum cũ:

```text
PROGRAM
TUITION_FEE
VISA
DORMITORY
VISIBLE
DRAFT
```

trừ khi test invalid input để kỳ vọng BadRequest.

### 3.5. Search scope

AI Agent phải đọc source handler để xác nhận search scope thật.

Nếu source hiện tại search theo `keyword` trong cùng endpoint `GET /api/faqs`, khả năng cần test:

```text
- Keyword match Question.
- Keyword match Answer.
- Keyword match FaqType nếu handler thật có search trong FaqType.
```

Không được giả định search trong Status nếu source không có.

Không được assert search trong Answer/FaqType nếu handler thật không hỗ trợ.

### 3.6. Keyword behavior

Đọc source để xác nhận:

```text
- Keyword null/empty/whitespace xử lý thế nào.
- Keyword có trim không.
- Search có case-insensitive không.
- Search dùng contains matching hay exact matching.
- Có minimum length không.
- Có maximum length không.
```

Nếu source/spec hiện tại xác nhận:

```text
Search trim keyword.
Search case-insensitive.
Search contains matching.
Keyword rỗng thì không filter.
```

thì test theo các rule đó.

Nếu source không có validate keyword length, không tự thêm test `Keyword_TooLong_HasError`.

### 3.7. Search kết hợp filter

Nếu source hiện tại gộp search/filter trong cùng endpoint, Search FAQ cần test đại diện cho AND logic:

```text
keyword + faqType filter -> kết quả phải vừa match keyword vừa đúng faqType.
keyword + status filter  -> kết quả phải vừa match keyword vừa đúng status.
```

Không cần test toàn bộ filter/sort/pagination của UC-62 trong UC-66.

### 3.8. Search trong HO management phải thấy HIDDEN nếu match

Nếu endpoint Search FAQ là endpoint quản trị của HO:

```text
HO search phải có thể thấy cả FAQ PUBLISHED và FAQ HIDDEN nếu chúng match keyword.
```

Đây là điểm phân biệt với public FAQ search.

### 3.9. Read-only behavior

Search FAQ là API đọc dữ liệu.

Khi gọi thành công:

```text
- Không được tạo FAQ mới.
- Không được sửa question/answer/faqType/status.
- Không được refresh updated_at/updated_by.
- Không được thay đổi created_at/created_by.
```

Không bắt buộc phải có test read-only riêng nếu UC-62 View List FAQ đã có test read-only và report chấp nhận reuse. Nếu tạo test tên `Ho_Search_DoesNotModifyFaqs`, phải seed dữ liệu, chụp snapshot trước GET, gọi search, reload DB và assert record không đổi.

---

## 4. Phân biệt rõ Unit Test và Integration Test

### 4.1. Unit Test phù hợp với Search FAQ

Unit Test chỉ kiểm tra logic nhỏ, cô lập.

Chỉ tạo Unit Test nếu source có:

```text
SearchFAQQueryValidator
ViewListFAQQueryValidator có rule riêng cho Keyword
SearchKeywordNormalizer
FaqSearchCriteriaValidator
FaqSearchHelper thuần
FaqSearchDtoMapper thuần
```

Các case Unit Test phù hợp:

```text
ValidSearchQuery_NoErrors
Keyword_Null_NoError nếu keyword optional
Keyword_Empty_NoError nếu empty nghĩa là no filter
Keyword_Whitespace_TreatedAsEmpty nếu có normalizer/helper
Keyword_TrimmedBeforeSearch nếu có normalizer/helper thuần
Keyword_TooLong_HasError nếu source có max length
Keyword_MinLength_HasError nếu source có min length
```

Không tạo Unit Test vô nghĩa chỉ để đủ số lượng. Nếu source hiện tại **không có rule keyword trong validator** và không có helper thuần, báo rõ:

```text
Không tạo Unit Test riêng cho UC-66 vì Search FAQ không có logic unit-level tách biệt; search behavior được cover ở Integration Test qua handler + DB thật.
```

### 4.2. Integration Test phù hợp với Search FAQ

Integration Test kiểm tra nhiều phần chạy cùng nhau:

```text
HTTP request -> Auth/TestAuthHandler -> Controller -> MediatR -> Validator -> Handler -> DB test
```

Search behavior phụ thuộc DB thật nên ưu tiên test bằng Integration Test.

Integration Test phải chứng minh:

```text
- HO được search.
- Role không có quyền bị chặn nếu endpoint quản trị.
- Keyword match đúng field source hỗ trợ.
- Keyword không match thì trả empty result.
- Case-insensitive/trim/contains behavior nếu source hỗ trợ.
- Search kết hợp filter nếu source hỗ trợ.
- Không lộ nhầm HIDDEN ra public endpoint nếu test public search; hoặc HO management search thấy HIDDEN nếu test HO.
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

Nếu có Unit Test riêng cho Search FAQ, đặt trong:

```text
tests/PEMS.UnitTests/Faqs/SearchFaq/
```

File gợi ý:

```text
SearchFaqQueryValidatorTests.cs
SearchFaqKeywordNormalizerTests.cs
```

Nếu source dùng chung `ViewListFAQQueryValidator`, có thể đặt tên rõ:

```text
SearchFaqQueryValidatorTests.cs
```

nhưng trong comment phải ghi:

```text
UC-66 Search FAQ currently reuses ViewListFAQQuery/ViewListFAQQueryValidator because source implements search as keyword query inside GET /api/faqs.
```

Không copy toàn bộ `ViewListFaqQueryValidatorTests` sang Search FAQ.

### 5.2. Integration Test folder

Integration Test đặt trong:

```text
tests/PEMS.IntegrationTests/Faqs/SearchFaq/
```

File gợi ý:

```text
SearchFaqApiTests.cs
```

Nếu team quyết định giữ search tests trong `ViewListFaqApiTests`, phải ghi report mapping rõ ràng và tránh duplicate.

### 5.3. Helper dùng chung và prefix dữ liệu test

Ưu tiên dùng lại:

```text
tests/PEMS.IntegrationTests/TestInfrastructure/PemsWebApplicationFactory.cs
tests/PEMS.IntegrationTests/TestInfrastructure/TestAuthHandler.cs
tests/PEMS.IntegrationTests/TestInfrastructure/DatabaseResetHelper.cs
```

Nếu cần bổ sung helper seed FAQ cho Search FAQ, bổ sung vào `DatabaseResetHelper` hoặc helper chung tương đương. Không copy helper lặp lại trong từng file test.

Helper nên hỗ trợ:

```text
- Tạo FAQ test với prefix do test class truyền vào.
- Tạo FAQ test với status PUBLISHED/HIDDEN.
- Tạo FAQ test với faqType khác nhau.
- Tạo FAQ test với question/answer chứa token riêng.
- Trả về faqId.
- Cleanup record theo đúng prefix được truyền vào.
- Không hardcode FaqQuestionPrefix dùng chung cho mọi FAQ test.
- Không cleanup prefix của use case/test class khác.
```

Ví dụ hướng thiết kế:

```csharp
public const string SearchFaqQuestionPrefix = "[IT-UC66-SEARCH-FAQ] ";

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
- Lỗi có thể biểu hiện thành NotFound, DbUpdateConcurrencyException, duplicate/search/filter trả sai.
- Đây là lỗi test infrastructure, không phải lỗi production code.
```

Dù đã tắt parallelization, vẫn phải tách prefix theo use case/test class. Tắt parallelization là lớp an toàn runtime; tách prefix là lớp an toàn dữ liệu và ngữ nghĩa.

---

## 6. Phạm vi AI Agent được sửa

AI Agent được phép tạo/sửa:

```text
tests/PEMS.UnitTests/Faqs/SearchFaq/...
tests/PEMS.IntegrationTests/Faqs/SearchFaq/...
tests/PEMS.UnitTests/TestHelpers/...
tests/PEMS.IntegrationTests/TestInfrastructure/...
docs/testing/...
backend/PEMS.Api/appsettings.Testing.example.json
file prompt/test documentation liên quan
```

AI Agent được phép sửa `ViewListFaqApiTests` hoặc `ViewListFaqQueryValidatorTests` **chỉ khi** cần tách/move search-specific tests sang UC-66 và không làm mất coverage của UC-62.

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

Ưu tiên dùng lại test infrastructure đã có từ Create/Update/View List FAQ.

Yêu cầu:

```text
- PemsWebApplicationFactory khởi động API trong environment Testing.
- Không dùng appsettings.Development.json.
- Override authentication bằng test scheme.
- Cho phép tạo request giả với role HO, STAFF, STAFF+LEADER, ADMIN, VISITOR.
- Cho phép gọi request không đăng nhập để test 401.
- Connection string phải trỏ tới database test riêng.
- Không dùng Docker.
- Không dùng Testcontainers.
- Không gọi service ngoài thật như Google SSO, SMTP, Google Drive.
```

Nếu project đã có test infrastructure tương đương, ưu tiên dùng lại và bổ sung thiếu sót, không tạo trùng.

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

### 8.2. Không được tự động tạo/drop/import database nếu chưa được xác nhận

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
4. Dữ liệu sai quan trọng thì bị chặn.
5. Rule nghiệp vụ đặc biệt được kiểm tra.
6. Khi request fail, DB không bị ghi/sửa sai nếu use case có write side effect.
7. Rủi ro bảo mật chính như authorization, data exposure, XSS/sanitize hoặc scope dữ liệu được kiểm tra nếu liên quan.
```

Với Search FAQ, tránh test thừa:

```text
Không test sâu Create FAQ.
Không test sâu Update FAQ.
Không test Change FAQ Visibility.
Không test toàn bộ pagination/sort/filter của UC-62 nếu đã có UC-62 riêng.
Không duplicate nguyên bộ ViewListFaqApiTests chỉ để tạo UC-66.
```

Chỉ test các case search có giá trị:

```text
- Search theo field được source hỗ trợ.
- Search không match.
- Search trim/case-insensitive nếu source hỗ trợ.
- Search kết hợp filter theo AND logic nếu source gộp filter/search.
- Search management không nhầm public endpoint.
- Authorization nếu UC-66 là HO management search.
```

---

## 10. Unit Test cần tạo

Chỉ tạo Unit Test theo source thật hiện tại.

### 10.1. Validator/Normalizer tests

File gợi ý:

```text
tests/PEMS.UnitTests/Faqs/SearchFaq/SearchFaqQueryValidatorTests.cs
```

Nếu source dùng chung `ViewListFAQQueryValidator`, test có thể khởi tạo validator đó, nhưng tên và comment phải nói rõ đang test search keyword behavior của UC-66.

Các case tối thiểu, chỉ viết nếu source có rule tương ứng:

```text
1. ValidSearchQuery_NoErrors
   Query search hợp lệ -> không lỗi.

2. Keyword_Null_NoError
   Nếu keyword optional -> không lỗi.

3. Keyword_Empty_NoError
   Nếu empty keyword nghĩa là no filter -> không lỗi.

4. Keyword_Whitespace_NoError hoặc Keyword_Whitespace_TreatedAsEmpty
   Chỉ viết nếu validator/helper có behavior rõ với whitespace.

5. Keyword_TooLong_HasError
   Chỉ viết nếu source có max length.

6. Keyword_MinLength_HasError
   Chỉ viết nếu source có min length.
```

Không copy các test sau từ UC-62 nếu chúng đã được test ở `ViewListFaqQueryValidatorTests` và không phải search-specific:

```text
Page_NotPositive_HasError
PageSize_OutOfRange_HasError
FaqType_Invalid_HasError
Status_Invalid_HasError
SortBy_NotAllowed_HasError
SortDirection_Invalid_HasError
```

Nếu Search FAQ không có Unit-level logic riêng, báo rõ trong report:

```text
Không tạo Unit Test riêng cho UC-66 vì source hiện tại triển khai search trong ViewListFAQQueryHandler và không có validator/helper riêng cho Keyword. Search behavior được test ở Integration Test.
```

### 10.2. Tên method Unit Test nên dùng

Dùng tên rõ nghĩa, không dùng số HTTP trong Unit Test.

Ví dụ:

```csharp
ValidSearchQuery_NoErrors()
Keyword_Null_NoError()
Keyword_Empty_NoError()
Keyword_Whitespace_NoError()
Keyword_TooLong_HasError()
Keyword_MinLength_HasError()
```

Tên test không được hứa điều không assert.

---

## 11. Integration Test cần tạo

Integration Test đặt trong:

```text
tests/PEMS.IntegrationTests/Faqs/SearchFaq/
```

File gợi ý:

```text
SearchFaqApiTests.cs
```

Ưu tiên dùng lại style đã chốt từ Create/Update/View List FAQ:

```text
- xUnit.
- IClassFixture<PemsWebApplicationFactory>.
- IAsyncLifetime cleanup sau mỗi test.
- CreateClientAsAsync(EffectiveRole.Ho/Staff/StaffLeader/Admin/Visitor).
- TestAuthHandler headers.
- DatabaseResetHelper cleanup theo prefix riêng của Search FAQ.
- Semantic test names ngắn gọn, không dùng số HTTP hoặc Returns400/Returns403/Returns200.
- Assembly-level DisableTestParallelization cho PEMS.IntegrationTests nếu chưa có.
```

### 11.1. Setup dữ liệu FAQ cho Search tests

Search FAQ cần seed FAQ có token riêng để cô lập kết quả.

Yêu cầu:

```text
- Dùng prefix riêng của Search FAQ, ví dụ [IT-UC66-SEARCH-FAQ].
- Mỗi test dùng token GUID riêng.
- Token nên đặt trong Question hoặc Answer tùy test.
- Không phụ thuộc dữ liệu seed thật trong pems_test.
- Không assert exact total count toàn DB nếu không filter/cô lập được.
- Khi cần assert no match, dùng token GUID chưa seed.
```

Ví dụ:

```text
Question: [IT-UC66-SEARCH-FAQ] question-search <token>?
Answer:   Câu trả lời chứa mã <token>.
```

Với search theo `FaqType`, vì FaqType là enum cố định không thể chứa token riêng, phải cẩn thận:

```text
- Chỉ viết test FaqType search nếu source thật search trong FaqType.
- Không assert total count nếu DB có nhiều FAQ cùng FaqType.
- Có thể assert response chứa ít nhất một item có FaqType cần tìm hoặc seeded item xuất hiện nếu sorting/pageSize đảm bảo.
- Nếu không thể làm deterministic, bỏ case này và ghi rõ lý do trong report.
```

### 11.2. Các case Integration Test tối thiểu

Điều chỉnh HTTP status/query param theo source thật.

#### Authentication / Authorization

Nếu UC-66 là HO management search:

```text
1. Anonymous_Unauthorized
   Không gắn auth headers -> search FAQ -> 401 Unauthorized.

2. Staff_Forbidden
   STAFF đã đăng nhập -> search FAQ -> 403 Forbidden.

3. StaffLeader_Forbidden
   STAFF + LEADER đã đăng nhập -> search FAQ -> 403 Forbidden.

4. Admin_Forbidden
   ADMIN đã đăng nhập -> search FAQ -> 403 Forbidden nếu nghiệp vụ HO-only.

5. Visitor_Forbidden
   VISITOR đã đăng nhập -> search FAQ -> 403 Forbidden.
```

Nếu các authorization case đã được cover đầy đủ trong UC-62 và team không muốn duplicate, tối thiểu giữ:

```text
Anonymous_Unauthorized
Staff_Forbidden hoặc Admin_Forbidden đại diện
Ho_KeywordMatchesQuestion
```

và ghi trong report rằng authorization full matrix đã được cover ở UC-62 vì Search FAQ dùng cùng endpoint/policy.

#### Search core behavior

```text
6. Ho_KeywordMatchesQuestion
   Seed FAQ có token trong Question, Answer không chứa token.
   HO gọi search keyword token.
   Expect OK.
   Response chứa FAQ đó.

7. Ho_KeywordMatchesAnswer
   Seed FAQ có token chỉ trong Answer, Question không chứa token.
   HO gọi search keyword token.
   Expect OK.
   Response chứa FAQ đó.

8. Ho_KeywordMatchesFaqType
   Chỉ viết nếu source search trong FaqType.
   HO search keyword là FaqType hợp lệ hoặc text tương ứng theo source.
   Expect OK.
   Response chứng minh FaqType được search.
   Không assert exact total count nếu DB không cô lập được.

9. Ho_KeywordCaseInsensitiveAndTrimmed
   Seed FAQ có token dạng lower/normal.
   HO gọi keyword có khoảng trắng đầu/cuối và đổi hoa/thường.
   Expect OK.
   Response vẫn chứa FAQ đó.

10. Ho_KeywordNoMatch_ReturnsEmptyResult
    HO search token GUID chưa seed.
    Expect OK.
    Items rỗng và TotalItems = 0 nếu response DTO có TotalItems.
```

#### Management scope

```text
11. Ho_SearchReturnsPublishedAndHiddenFaqs
    Seed 1 FAQ PUBLISHED và 1 FAQ HIDDEN cùng match keyword/token.
    HO search keyword.
    Expect cả 2 record xuất hiện.
    Mục tiêu: chứng minh search quản trị khác public search.
```

Không dùng case này để test Change Visibility. Chỉ seed status khác nhau để kiểm tra search scope.

#### Search + filter AND logic

Chỉ viết nếu source gộp search/filter trong cùng endpoint:

```text
12. Ho_SearchAndFaqTypeFilter_UsesAndLogic
    Seed nhiều FAQ cùng token nhưng khác FaqType.
    HO search keyword + faqType filter.
    Response chỉ chứa FaqType được chọn trong tập match keyword.

13. Ho_SearchAndStatusFilter_UsesAndLogic
    Seed nhiều FAQ cùng token nhưng khác Status.
    HO search keyword + status filter.
    Response chỉ chứa Status được chọn trong tập match keyword.
```

Không cần test toàn bộ invalid filter trong UC-66 nếu UC-62 đã có:

```text
InvalidFaqTypeFilter_BadRequest
InvalidStatusFilter_BadRequest
SortBy_NotAllowed_BadRequest
Page_Zero_BadRequest
```

trừ khi report yêu cầu Search FAQ độc lập hoàn toàn.

#### Read-only behavior

Optional:

```text
14. Ho_Search_DoesNotModifyFaqs
    Seed FAQ, chụp snapshot question/answer/faqType/status/audit.
    HO search keyword match FAQ đó.
    Expect OK.
    Reload DB và assert record unchanged.
```

Chỉ dùng tên `DoesNotModify` nếu thật sự assert DB unchanged.

### 11.3. Bộ tên method Integration Test gợi ý

Dùng tên semantic, ngắn gọn, không dùng số HTTP code trong tên test.

Bộ tên gợi ý:

```csharp
Anonymous_Unauthorized()
Staff_Forbidden()
StaffLeader_Forbidden()
Admin_Forbidden()
Visitor_Forbidden()

Ho_KeywordMatchesQuestion()
Ho_KeywordMatchesAnswer()
Ho_KeywordMatchesFaqType()
Ho_KeywordCaseInsensitiveAndTrimmed()
Ho_KeywordNoMatch_ReturnsEmptyResult()
Ho_SearchReturnsPublishedAndHiddenFaqs()
Ho_SearchAndFaqTypeFilter_UsesAndLogic()
Ho_SearchAndStatusFilter_UsesAndLogic()
Ho_Search_DoesNotModifyFaqs()
```

Không bắt buộc viết toàn bộ nếu source hiện tại không hỗ trợ hoặc case bị trùng với UC-62. Nếu bỏ case nào, phải giải thích lý do trong report.

### 11.4. Không tạo test thừa

Không tạo các test sau trong UC-66 nếu đã cover ở UC-62 và không trực tiếp liên quan search:

```text
Ho_ReturnsFaqList
Ho_ListItem_ContainsManagementFields
Ho_Pagination_ReturnsRequestedPage
Page_Zero_BadRequest
FaqType_Filter_ReturnsOnlyMatchingType
Status_Filter_ReturnsOnlyMatchingStatus
InvalidFaqTypeFilter_BadRequest
InvalidStatusFilter_BadRequest
SortBy_NotAllowed_BadRequest
```

Chỉ đưa lại nếu Search FAQ được report như use case hoàn toàn độc lập và reviewer yêu cầu full API validation matrix.

---

## 12. Quy tắc đặt tên test case

Tên test phải ngắn gọn nhưng không được mơ hồ hoặc hứa quá nội dung assert.

```text
- Tên test phải nói đúng hành vi chính của test.
- Không dùng số HTTP code trong tên test, ví dụ 200/400/401/403/404/409.
- Có thể dùng tên HTTP outcome như Unauthorized, Forbidden, BadRequest, NotFound, Conflict nếu đó là hành vi chính cần test.
- Không đặt tên kiểu chung chung Returns400/Returns403/Returns200.
- Không dùng tên quá dài chỉ để liệt kê toàn bộ assert.
- Không dùng DoesNotModify / ReadOnly nếu test không thật sự reload DB và kiểm tra record cũ không đổi.
- Không dùng MatchesAnswer nếu token thật ra nằm cả trong Question.
- Không dùng CaseInsensitiveAndTrimmed nếu test không thật sự đổi case và thêm whitespace.
- Không dùng ReturnsPublishedAndHidden nếu không seed và assert cả PUBLISHED lẫn HIDDEN.
- Với authorization test, tên có thể ngắn vì outcome chính là Unauthorized/Forbidden.
- Với business test, ưu tiên tên theo nghiệp vụ thay vì chỉ theo HTTP status.
```

Ví dụ đúng:

```text
Ho_KeywordMatchesQuestion
Ho_KeywordMatchesAnswer
Ho_KeywordCaseInsensitiveAndTrimmed
Ho_KeywordNoMatch_ReturnsEmptyResult
Ho_SearchAndStatusFilter_UsesAndLogic
Ho_Search_DoesNotModifyFaqs
```

Ví dụ sai:

```text
SearchFaq_WhenHoSearchesKeyword_Returns200AndSearchesQuestionAnswerFaqTypeAndStatusAndPagination
Returns200
Keyword_Search_MatchesAnswerContent // sai nếu keyword cũng nằm trong Question
Ho_Search_DoesNotModifyFaqs          // sai nếu chỉ assert OK, không reload DB
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
dotnet test tests/PEMS.IntegrationTests/PEMS.IntegrationTests.csproj --filter "FullyQualifiedName~SearchFaqApiTests"
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
# Báo cáo tạo test UC-66 Search FAQ

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
| UC ID trong yêu cầu | UC-66 Search FAQ |
| UC ID trong source/docs nếu khác | ... |
| Search endpoint thật | ... |
| Search là endpoint riêng hay query param của list? | ... |
| Query param thật | keyword/search/q/... |
| Response DTO thật | ... |
| Actor hợp lệ | ... |
| Có phân biệt public search vs management search không? | ... |
| Search scope thật | Question/Answer/FaqType/... |
| Có trim/case-insensitive/contains không? | ... |
| Có kết hợp filter theo AND logic không? | ... |

## 4. Unit Test đã tạo
[Liệt kê case hoặc giải thích vì sao không tạo Unit Test riêng]

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
- Unit Test, nếu có, nằm đúng tests/PEMS.UnitTests/Faqs/SearchFaq/.
- Integration Test nằm đúng tests/PEMS.IntegrationTests/Faqs/SearchFaq/.
- Không trộn Unit Test và Integration Test.
- Không dùng database dev/thật.
- Không import SQL fresh-create gốc trực tiếp.
- Không đọc/copy/in secret thật.
- Không tự ý sửa production code để ép test pass.
- Unit Test đã chạy hoặc báo rõ lý do chưa chạy.
- Integration Test chỉ chạy khi đạt DB safety gate và có xác nhận.
- Test names rõ nghĩa, không dùng số HTTP nếu có thể.
- Search FAQ test xác nhận endpoint thật, không bịa route.
- Nếu Search FAQ dùng GET /api/faqs?keyword=..., report rõ search gộp trong ViewListFAQ endpoint.
- Không duplicate nguyên bộ UC-62 View List FAQ tests.
- Authorization test bao phủ Anonymous và các role không phải HO đại diện nếu UC-66 là management search.
- Search test chứng minh keyword match đúng field source hỗ trợ.
- Search no-match test không phụ thuộc dữ liệu seed thật.
- Search test không assert exact total count nếu không cô lập dữ liệu bằng token/prefix.
- Nếu tên có MatchesAnswer, token chỉ được nằm trong Answer.
- Nếu tên có CaseInsensitiveAndTrimmed, test thật sự đổi case và thêm whitespace.
- Nếu tên có DoesNotModify/ReadOnly, test thật sự snapshot DB trước/sau.
- Nếu search/filter là cùng endpoint, test AND logic bằng case đại diện.
- Integration Test assembly đã tắt parallelization hoặc report rõ lý do chưa thêm.
- Search FAQ không dùng chung cleanup prefix với Create/Update/ViewList/Visibility FAQ.
- DatabaseResetHelper seed/cleanup nhận prefix riêng hoặc có helper riêng tương đương.
- Chạy riêng SearchFaqApiTests và chạy toàn bộ IntegrationTests nếu được phép để phát hiện race condition.
- Báo cáo pass/fail rõ ràng.
```

---

## 16. Nhắc lại các lỗi nghiêm cấm

Không được lặp lại các lỗi sau:

```text
- Tự tạo endpoint Search FAQ giả khi source không có.
- Dùng GET /api/public/faqs để test UC-66 management search nếu source nghiệp vụ là HO management.
- Copy nguyên ViewListFaqApiTests sang SearchFaqApiTests và đổi tên class.
- Test sâu Change FAQ Visibility trong UC-66.
- Test sâu Create/Update FAQ trong UC-66.
- Test pagination/sort/filter toàn diện trong UC-66 nếu UC-62 đã cover và search không phụ thuộc trực tiếp.
- Assert exact total count khi DB có seed khác và không cô lập bằng token/prefix.
- Dùng chung FaqQuestionPrefix với Create/Update/ViewList/Visibility FAQ.
- Cleanup theo prefix quá rộng khiến test class này xóa dữ liệu test class khác.
- Để xUnit chạy song song các Integration Test class dùng chung pems_test.
- Thấy NotFound/DbUpdateConcurrencyException khi chạy toàn bộ test rồi vội sửa production code trước khi kiểm tra race condition test infrastructure.
- Chạy SQL fresh-create gốc khi chưa kiểm tra có USE pems_db.
- Tin rằng `mysql pems_test < script.sql` chắc chắn chỉ tác động pems_test.
- Dùng appsettings.Development.json cho Integration Test.
- Đọc/copy secret thật sang file test.
- Sửa production code để test pass mà chưa được duyệt.
- Đặt tên test có DoesNotModify/ReadOnly nhưng không assert DB unchanged.
- Đặt tên MatchesAnswer nhưng token cũng nằm trong Question.
```

Nếu không chắc chắn, phải dừng lại và hỏi người dùng.
