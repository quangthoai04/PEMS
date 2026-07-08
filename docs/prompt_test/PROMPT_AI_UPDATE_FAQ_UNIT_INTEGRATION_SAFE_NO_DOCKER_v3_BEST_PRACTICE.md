# PROMPT AI — TẠO TEST CODE THẬT CHO UPDATE FAQ (UNIT TEST + INTEGRATION TEST, KHÔNG DOCKER, AN TOÀN DATABASE) — v3

> File này dùng để đưa cho AI Agent/Code Agent khi cần tạo test tự động cho chức năng **Update FAQ** trong dự án PEMS.
>
> Prompt này kế thừa kinh nghiệm đã chốt từ phần **Create FAQ**: dùng **xUnit + WebApplicationFactory + TestAuthHandler + database test riêng `pems_test`**, không bắt buộc Docker/Testcontainers, không dùng database dev/thật, và tên test phải phản ánh đúng nội dung assert.
>
> Mục tiêu quan trọng nhất: **tạo test code thật, chạy được, đúng nghiệp vụ Update FAQ, nhưng tuyệt đối không được làm hỏng `pems_db` hoặc bất kỳ database dev/thật nào**.

> **Cập nhật v3:** Bản này là bản dùng độc lập, đã bổ sung các lỗi thực tế cần tránh khi viết Integration Test cho nhiều use case dùng chung `pems_test`: không dùng chung prefix cleanup giữa Create/Update FAQ, phải tắt parallelization cho Integration Test assembly, helper phải nhận prefix riêng theo use case/test class, và no-change update phải test theo behavior thật của backend.

---

## 0.1. Những thay đổi trong bản v3

```text
1. Kế thừa toàn bộ nội dung quan trọng của bản v2, không cần dùng kèm bản cũ.
2. Rút gọn tên method Integration Test: ngắn gọn, semantic, không dùng số HTTP code.
3. Không dùng DoesNotModify/KeepsUnchanged nếu test không reload DB và kiểm tra record cũ không đổi.
4. Không dùng UpdatesAudit/KeepsCreateAudit nếu test không assert audit fields thật.
5. Không dùng PersistsSanitizedContent nếu test không đọc DB/response để xác nhận sanitize.
6. Sửa rõ behavior no-change update: test theo source backend hiện tại, không tự sửa production code theo giả định cũ.
7. Bổ sung rule bắt buộc không dùng chung cleanup prefix giữa các use case/test class.
8. Bổ sung rule Create FAQ và Update FAQ phải có prefix riêng, ví dụ [IT-UC63-CREATE-FAQ] và [IT-UC64-UPDATE-FAQ].
9. Bổ sung rule DatabaseResetHelper phải nhận prefix truyền vào hoặc helper riêng tương đương, không hardcode một FaqQuestionPrefix dùng chung.
10. Bổ sung rule tắt parallelization cho Integration Test assembly khi dùng chung database thật `pems_test`.
11. Bổ sung nguyên tắc chọn test case vừa đủ: không test lan man, không lặp biến thể đã được Unit Test bao phủ.
12. Bổ sung lỗi nghiêm cấm từ race condition thực tế: cleanup của test này không được xóa dữ liệu test class/use case khác.
```

---

## 0. Ai sẽ đọc file này?

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

Tạo test tự động cho chức năng **Update FAQ**.

Test cần gồm 2 nhóm:

```text
1. Unit Test
   Kiểm tra logic nhỏ, chủ yếu là Validator hoặc helper thuần.
   Không gọi API thật.
   Không dùng database thật.

2. Integration Test
   Kiểm tra API update FAQ chạy qua nhiều layer thật:
   API + Authentication/Authorization giả + Controller + MediatR + Validator + Handler + database test riêng.
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
Existing tests hiện tại, đặc biệt Create FAQ tests đã làm xong
```

### 2.2. Source code bắt buộc kiểm tra

AI Agent phải tự search đúng file trong project, không được bịa path.

Tối thiểu cần kiểm tra:

```text
Backend controller liên quan FAQ
Update FAQ Command
Update FAQ CommandHandler
Update FAQ CommandValidator
Update FAQ Request/Response DTO
Create FAQ Command/Handler/Validator để tái sử dụng convention đã có
Faq entity
EF Configuration của faqs
ApplicationDbContext
Constants/Enums liên quan FAQ type/status
Authorization/Role check liên quan FAQ
Existing Unit Tests: tests/PEMS.UnitTests/Faqs/CreateFaq/...
Existing Integration Tests: tests/PEMS.IntegrationTests/Faqs/CreateFaq/...
Existing test infrastructure: PemsWebApplicationFactory, TestAuthHandler, DatabaseResetHelper
SQL fresh-create mới nhất trong docs/database/scripts/
```

Nếu tên class/path trong project khác ví dụ trong file này, thì hỏi lại, không được tự ý suy đoán

### 2.3. Lưu ý về UC ID

Thắc mắc về Usecase ID thì hỏi lại, không được tự ý suy đoán

Quy tắc:

```text
- Không tự ý đổi UC ID trong source/test.
- Không hardcode UC ID nếu source hiện tại không dùng.
- Khi báo cáo, ghi rõ mapping tìm thấy trong source/tài liệu hiện tại.
- Tên folder/test nên dùng tên nghiệp vụ ổn định: Faqs/UpdateFaq.
```

---

## 3. Nghiệp vụ Update FAQ phải giữ đúng

Chức năng Update FAQ dùng cho màn quản lý FAQ nội bộ.

### 3.1. Actor hợp lệ

```text
Chỉ HO được update FAQ.
```

Backend phải tự kiểm tra quyền. Frontend ẩn/hiện nút không đủ để bảo mật.

Các role khác gọi API trực tiếp phải bị chặn.

```text
Không đăng nhập -> 401 Unauthorized.
Đăng nhập nhưng không phải HO -> 403 Forbidden.
HO hợp lệ -> được update FAQ nếu dữ liệu hợp lệ.
```

Không mặc định Admin có toàn quyền. Nếu nghiệp vụ hiện tại là HO-only thì Admin phải bị 403 giống Create FAQ.

### 3.2. Endpoint/API phải lấy từ source thật

Không được tự bịa route.

AI Agent phải search trong `FaqsController` hoặc controller tương đương để xác định endpoint thật, ví dụ có thể là:

```text
PUT /api/faqs/{id}
PUT /api/faqs
PATCH /api/faqs/{id}
```

Dùng đúng endpoint hiện tại trong source. Nếu endpoint khác ví dụ, báo lại trong report.

### 3.3. Field đầu vào

Update FAQ thường chỉ sửa các field nghiệp vụ:

```text
faqId / id
faqType
question
answer
```

Quy tắc theo UC hiện tại:

```text
- Edit FAQ modal không sửa Visible/Hidden status.
- Status PUBLISHED/HIDDEN phải được giữ nguyên khi update question/answer/faqType.
- Change FAQ Visibility là use case riêng.
```

Không dùng:

```text
languageCode
language_code
Visible/Hidden raw UI label
status trong Update FAQ nếu source hiện tại không có
```

Nếu production code hiện tại vẫn có status trong Update command, không tự sửa production code. Hãy báo rõ lệch nghiệp vụ và chỉ test theo hành vi source hiện tại hoặc dừng hỏi người dùng nếu rủi ro.

### 3.4. FAQ type hợp lệ

Chỉ dùng các giá trị FAQ type hiện tại:

```text
ACCOUNT_ACCESS
VISIT_REQUEST
DELEGATION_MANAGEMENT
LOGISTICS_RESOURCE
DOCUMENT_MEDIA
NOTIFICATION_EMAIL
OTHER
```

Không dùng enum cũ:

```text
PROGRAM
TUITION_FEE
VISA
DORMITORY
```

Nếu tài liệu cũ còn nhắc Program/Tuition/Visa/Dormitory, coi là legacy và ưu tiên SQL/constants/source hiện tại.

### 3.5. Status hợp lệ và quy tắc giữ nguyên status

Update FAQ không phải Change Visibility.

Khi update thành công:

```text
- Nếu FAQ đang PUBLISHED thì vẫn PUBLISHED.
- Nếu FAQ đang HIDDEN thì vẫn HIDDEN.
```

Không được tự ý đổi status sang default `PUBLISHED` khi update.

### 3.6. Validation bắt buộc

```text
id/faqId bắt buộc và phải hợp lệ theo convention hiện tại của source.
question bắt buộc, sau khi trim không được rỗng.
answer bắt buộc, sau khi trim không được rỗng.
faqType bắt buộc và phải thuộc enum hiện tại.
Không nhận languageCode/language_code.
Không nhận enum FAQ cũ.
```

Nếu source có giới hạn độ dài, test theo source thật. Nếu source không có hoặc chưa rõ, không tự bịa limit.

### 3.7. Duplicate question khi update

Không cho update FAQ thành câu hỏi trùng với FAQ khác.

Quy tắc so sánh:

```text
Trim khoảng trắng đầu/cuối.
So sánh không phân biệt hoa/thường.
Kiểm tra trên toàn bộ bảng faqs, gồm cả PUBLISHED và HIDDEN.
Phải loại trừ chính FAQ đang được edit.
```

Ví dụ:

```text
FAQ A id=100 question="Làm sao đăng nhập?"
FAQ B id=200 question="Cách gửi đơn?"

Update FAQ B thành "  LÀM SAO ĐĂNG NHẬP?  "
-> phải bị duplicate/conflict vì trùng FAQ A.

Update FAQ A giữ nguyên "Làm sao đăng nhập?"
-> không được coi là duplicate chính nó.
```

Duplicate phụ thuộc database thật nên ưu tiên test ở Integration Test, không ép thành Unit Test nếu phải mock EF Core phức tạp.

### 3.8. Sanitize nội dung

Trước khi lưu, `question` và `answer` phải được sanitize theo logic hiện tại của project.

Mục tiêu:

```text
Không lưu raw <script> hoặc HTML nguy hiểm.
Không để XSS đơn giản lọt qua.
Sau sanitize, nội dung không được rỗng nếu source có rule này.
```

### 3.9. Audit fields và no-change update

Khi update thật sự có thay đổi:

```text
updated_at phải được cập nhật theo convention hiện tại.
updated_by phải là HO user id thực hiện update.
created_at phải giữ nguyên.
created_by phải giữ nguyên.
```

Với case HO bấm Save nhưng payload mới giống hệt dữ liệu hiện tại, phải đọc source thật và viết test đúng behavior hiện tại của backend. Theo behavior đã xác nhận ở source hiện tại:

```text
No-change update:
- API trả OK.
- Response Changed = false nếu response DTO có field này.
- Backend không ghi DB.
- question/answer/faqType/status giữ nguyên.
- updated_at/updated_by không bị refresh.
- created_at/created_by giữ nguyên.
```

Không sửa production code để ép theo giả định cũ kiểu “no-change vẫn refresh audit”. Nếu tài liệu/spec cũ nói refresh audit nhưng source backend hiện tại không refresh, phải ghi nhận là prompt/spec mismatch và viết test theo source thật, trừ khi người dùng yêu cầu đổi nghiệp vụ.

---

## 4. Phân biệt rõ Unit Test và Integration Test

### 4.1. Unit Test là gì?

Unit Test kiểm tra một phần nhỏ của code.

Ví dụ phù hợp với Update FAQ:

```text
Validator nhận question rỗng -> báo lỗi.
Validator nhận answer rỗng -> báo lỗi.
Validator nhận faqType sai -> báo lỗi.
Validator nhận id không hợp lệ -> báo lỗi nếu command có id.
Validator nhận tất cả field hợp lệ -> không lỗi.
```

Unit Test:

```text
- Chạy nhanh.
- Không gọi API thật.
- Không dùng database thật.
- Không phụ thuộc appsettings.Development.json.
- Không phụ thuộc Google SSO/SMTP/Google Drive thật.
- Dependency phải mock/fake rõ ràng nếu có.
```

### 4.2. Integration Test là gì?

Integration Test kiểm tra nhiều phần chạy cùng nhau.

Ví dụ phù hợp với Update FAQ:

```text
Gọi update FAQ không token -> 401.
Gọi update FAQ bằng role STAFF -> 403.
Gọi update FAQ bằng role HO -> update thành công và DB thay đổi đúng.
Gọi update FAQ với question trùng FAQ khác -> conflict và DB giữ nguyên record cũ.
Gọi update FAQ với script tag -> thành công nhưng DB không lưu raw <script>.
```

Integration Test có thể dùng:

```text
- API thật trong môi trường Testing.
- Authentication giả.
- Database test riêng `pems_test`.
```

Integration Test không được dùng:

```text
- Database dev/thật.
- Google SSO thật.
- SMTP thật.
- Google Drive thật.
- appsettings.Development.json thật.
- Docker/Testcontainers.
```

### 4.3. Không ép mọi thứ thành Unit Test

Nếu logic phụ thuộc database, ví dụ:

```text
duplicate question exclude self
record not found
kiểm tra DB state trước/sau update
kiểm tra status unchanged
kiểm tra audit unchanged/changed
transaction/rollback khi fail
```

thì đưa case đó sang Integration Test.

Không cố mock EF Core `DbSet` bằng Moq nếu làm test rối, khó hiểu hoặc sai hành vi thật.

---

## 5. Quy ước tổ chức thư mục test

Dự án có nhiều chức năng, vì vậy test code phải chia rõ theo:

```text
Test Project
→ Module
→ Use Case / Action nghiệp vụ
→ File test
```

Không đặt tất cả test file lẫn lộn trực tiếp trong:

```text
tests/PEMS.UnitTests
tests/PEMS.IntegrationTests
```

### 5.1. Unit Test folder

Unit Test đặt trong:

```text
tests/PEMS.UnitTests
```

Bên trong dùng format:

```text
tests/PEMS.UnitTests/[Module]/[UseCaseName]/
```

Với Update FAQ:

```text
tests/PEMS.UnitTests/Faqs/UpdateFaq/
```

File gợi ý:

```text
UpdateFaqCommandValidatorTests.cs
```

Chỉ tạo `UpdateFaqCommandHandlerTests.cs` nếu Handler dependency có thể mock/fake rõ ràng và không cần DB thật.

### 5.2. Integration Test folder

Integration Test đặt trong:

```text
tests/PEMS.IntegrationTests
```

Bên trong dùng format:

```text
tests/PEMS.IntegrationTests/[Module]/[UseCaseName]/
```

Với Update FAQ:

```text
tests/PEMS.IntegrationTests/Faqs/UpdateFaq/
```

File gợi ý:

```text
UpdateFaqApiTests.cs
```

### 5.3. Helper dùng chung và prefix dữ liệu test

Ưu tiên dùng lại helper đã có từ Create FAQ:

```text
tests/PEMS.IntegrationTests/TestInfrastructure/PemsWebApplicationFactory.cs
tests/PEMS.IntegrationTests/TestInfrastructure/TestAuthHandler.cs
tests/PEMS.IntegrationTests/TestInfrastructure/DatabaseResetHelper.cs
```

Nếu cần bổ sung helper seed FAQ cho Update FAQ, bổ sung vào `DatabaseResetHelper` hoặc helper chung tương đương. Không copy helper lặp lại trong từng file test.

Bắt buộc tránh lỗi đã gặp: **không dùng chung một hằng số prefix cleanup cho nhiều use case/test class**.

Ví dụ sai:

```csharp
public const string FaqQuestionPrefix = "[IT-UC63] ";
// CreateFaqApiTests dùng prefix này
// UpdateFaqApiTests cũng dùng prefix này
// Khi xUnit chạy song song, DisposeAsync class này có thể xóa dữ liệu class kia.
```

Ví dụ đúng:

```csharp
private const string TestQuestionPrefix = "[IT-UC64-UPDATE-FAQ] ";
```

Hoặc tách rõ trong helper:

```csharp
public const string CreateFaqQuestionPrefix = "[IT-UC63-CREATE-FAQ] ";
public const string UpdateFaqQuestionPrefix = "[IT-UC64-UPDATE-FAQ] ";
```

Helper mới nên hỗ trợ:

```text
- Tạo FAQ test với prefix do test class truyền vào.
- Tạo FAQ test với status PUBLISHED/HIDDEN.
- Trả về faqId để update theo id.
- Cleanup record theo đúng prefix được truyền vào.
- Không hardcode FaqQuestionPrefix dùng chung cho mọi FAQ test.
- Không cleanup prefix của use case/test class khác.
```

Ví dụ hướng thiết kế helper:

```csharp
CreateTestFaqAsync(db, prefix, questionSuffix, answer, faqType, status, createdBy, updatedBy)
DeleteTestFaqsAsync(db, prefix)
```

### 5.4. Quy tắc chạy tuần tự Integration Test

Vì project Integration Test dùng chung một MySQL database thật `pems_test`, phải tắt parallelization ở assembly `PEMS.IntegrationTests` để tránh race condition giữa các test class.

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
- Lỗi có thể biểu hiện thành NotFound, DbUpdateConcurrencyException, hoặc business rule duplicate trả sai.
- Đây là lỗi test infrastructure, không phải lỗi production code.
```

Dù đã tắt parallelization, vẫn phải tách prefix theo use case/test class. Tắt parallelization là lớp an toàn runtime; tách prefix là lớp an toàn dữ liệu và ngữ nghĩa.

### 5.5. Không trộn loại test

Không đặt Integration Test vào:

```text
tests/PEMS.UnitTests
```

Không đặt Unit Test vào:

```text
tests/PEMS.IntegrationTests
```

Nếu test cần gọi API hoặc cần database test riêng, đó là Integration Test.

Nếu test chỉ kiểm tra validator/helper/rule nhỏ, đó là Unit Test.

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

Ưu tiên dùng lại test infrastructure đã tạo cho Create FAQ.

Nếu chưa có, tạo/cập nhật class:

```text
PemsWebApplicationFactory
```

Class này dùng để khởi động API PEMS trong môi trường Testing.

Yêu cầu:

```text
- Kế thừa WebApplicationFactory<Program> hoặc type API thật theo convention hiện tại.
- Set environment = Testing.
- Không dùng appsettings.Development.json.
- Override authentication bằng test scheme.
- Cho phép tạo request giả với role HO, STAFF, ADMIN, VISITOR.
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

### 8.4. Bắt buộc scan SQL trước khi import

Trước khi chạy bất kỳ SQL script nào, phải kiểm tra nội dung file.

Chỉ được dùng lệnh đọc file, ví dụ:

```bash
grep -n "DROP DATABASE\|CREATE DATABASE\|USE " path/to/script.sql
```

Phải báo cáo các dòng chứa:

```text
DROP DATABASE
CREATE DATABASE
USE <database_name>
pems_db
pems_dev
pems_local
```

Nếu thấy script có thao tác với `pems_db`, `pems_dev`, `pems_local` hoặc database dev/thật, phải dừng lại.

### 8.5. Nếu chưa có database test

Nếu chưa có `pems_test`, không tự ý tạo ngay.

Hãy tạo file hướng dẫn cho dev/tester, ví dụ:

```text
docs/testing/CREATE_TEST_DATABASE.md
```

Có thể tạo file config mẫu:

```text
backend/PEMS.Api/appsettings.Testing.example.json
```

Nội dung chỉ dùng placeholder:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Port=3306;Database=pems_test;User=YOUR_TEST_DB_USER;Password=YOUR_TEST_DB_PASSWORD;"
  }
}
```

Không tạo/commit `appsettings.Testing.json` chứa secret thật.

### 8.6. Không đọc/copy/in secret thật

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

## 8.1. Nguyên tắc chọn test case vừa đủ

Không viết test theo kiểu bao phủ máy móc mọi biến thể. Chỉ tạo test có ý nghĩa theo source thật và rủi ro chính của use case.

Một use case được coi là đủ test khi đã kiểm tra được:

```text
1. Ai được phép thực hiện.
2. Ai không được phép thực hiện.
3. Dữ liệu hợp lệ thì hệ thống xử lý đúng và DB thay đổi đúng.
4. Dữ liệu sai quan trọng thì bị chặn.
5. Rule nghiệp vụ đặc biệt được kiểm tra.
6. Khi request fail, DB không bị ghi/sửa sai.
7. Rủi ro bảo mật chính như authorization, XSS/sanitize, scope dữ liệu được kiểm tra nếu use case có liên quan.
```

### Unit Test

Unit Test chỉ kiểm tra logic nhỏ, cô lập, không gọi API và không dùng DB thật.

Ưu tiên Unit Test cho:

```text
- Validator.
- Required field.
- Enum/status hợp lệ/không hợp lệ.
- Boundary value.
- Rule nhỏ có thể test độc lập.
```

Không ép Unit Test cho logic phụ thuộc DB như duplicate, ownership, authorization pipeline hoặc DB state.

### Integration Test

Integration Test kiểm tra API thật và các layer chạy cùng nhau:

```text
API route -> Authentication/Authorization -> Controller -> MediatR -> Validator -> Handler -> DB test
```

Ưu tiên Integration Test cho:

```text
- Anonymous / role không có quyền.
- Role hợp lệ thực hiện thành công.
- Payload invalid đại diện.
- Business rule phụ thuộc DB.
- DB state sau success/fail.
- Audit/sanitize nếu source/schema có và test thật sự assert.
```

Không cần lặp mọi biến thể null/empty/whitespace ở Integration Test nếu Unit Test validator đã bao phủ. Integration Test chỉ cần vài case đại diện để chứng minh pipeline thật hoạt động.

Không tạo test thừa chỉ để tăng số lượng. Nếu một test không làm rõ thêm rủi ro nghiệp vụ, validation, authorization, DB state hoặc security, không cần viết.

---

## 9. Unit Test cần tạo

Tạo Unit Test theo source thật hiện tại.

### 9.1. Validator tests

Tạo test cho validator Update FAQ.

File gợi ý:

```text
tests/PEMS.UnitTests/Faqs/UpdateFaq/UpdateFaqCommandValidatorTests.cs
```

Các case tối thiểu:

```text
1. id/faqId invalid nếu command có id -> invalid.
   Ví dụ: id = 0, id < 0 nếu source dùng long/int.

2. question null/empty/whitespace -> invalid.

3. answer null/empty/whitespace -> invalid.

4. faqType null/empty/whitespace -> invalid.

5. faqType invalid/legacy -> invalid.
   Ví dụ: PROGRAM, VISA, account_access nếu source yêu cầu uppercase constant.

6. Tất cả faqType hợp lệ hiện tại -> valid.
   ACCOUNT_ACCESS, VISIT_REQUEST, DELEGATION_MANAGEMENT,
   LOGISTICS_RESOURCE, DOCUMENT_MEDIA, NOTIFICATION_EMAIL, OTHER.

7. faqType có khoảng trắng đầu/cuối nhưng sau trim hợp lệ -> valid nếu validator hiện tại hỗ trợ trim.
   Nếu validator không hỗ trợ, không tự sửa production code; báo mismatch.

8. request hợp lệ đầy đủ -> valid.
```

Không test `status` trong Update FAQ nếu Update command hiện tại không có status.

Nếu Update command có status nhưng nghiệp vụ nói status thuộc Change FAQ Visibility, báo mismatch và hỏi người dùng trước khi mở rộng test.

### 9.2. Handler Unit Test

Chỉ viết Handler Unit Test nếu dependency có thể mock/fake rõ ràng.

Các case có thể viết ở unit-level nếu phù hợp source:

```text
1. question/answer được trim trước khi lưu nếu logic nằm trong Handler/helper.
2. sanitize đơn giản nếu logic nằm trong Handler/helper thuần có thể test cô lập.
3. audit updated_by/updated_at được set nếu có ICurrentUser/TimeProvider mock được.
```

Không ép test các case sau ở Unit Test nếu chúng phụ thuộc EF Core/database thật:

```text
duplicate question exclude self
record not found
status unchanged trong DB
created_at/created_by unchanged
transaction rollback / không modify record khi fail
```

Các case đó đưa sang Integration Test.

### 9.3. Tên method Unit Test nên dùng

Dùng tên rõ nghĩa, không dùng số HTTP trong Unit Test.

Ví dụ:

```csharp
ValidCommand_NoErrors()
FaqId_Zero_HasError()
Question_Empty_HasError()
Question_Whitespace_HasError()
Question_Null_HasError()
Answer_Empty_HasError()
Answer_Whitespace_HasError()
Answer_Null_HasError()
FaqType_Missing_HasError()
FaqType_NotAllowed_HasError()
FaqType_AnyAllowedValue_NoError()
FaqType_SurroundingWhitespace_NoErrorAfterTrim()
```

Tên test không được hứa điều không assert.

---

## 10. Integration Test cần tạo

Integration Test đặt trong:

```text
tests/PEMS.IntegrationTests/Faqs/UpdateFaq/
```

File gợi ý:

```text
UpdateFaqApiTests.cs
```

Ưu tiên dùng lại style đã chốt từ Create FAQ:

```text
- xUnit
- IClassFixture<PemsWebApplicationFactory>
- IAsyncLifetime cleanup sau mỗi test
- CreateClientAsAsync(EffectiveRole.Ho/Staff/Admin/Visitor)
- TestAuthHandler headers
- DatabaseResetHelper cleanup theo prefix riêng của test class/use case, không dùng chung FaqQuestionPrefix
- Semantic test names ngắn gọn, không dùng số HTTP hoặc Returns400/Returns403/Returns200
- Assembly-level DisableTestParallelization cho PEMS.IntegrationTests nếu chưa có
```

### 10.1. Setup dữ liệu FAQ cho Update tests

Update FAQ cần có FAQ tồn tại trước khi gọi update.

Ưu tiên tạo helper trong `DatabaseResetHelper`, ví dụ:

```text
CreateTestFaqAsync(db, question, answer, faqType, status, createdBy, updatedBy)
```

Hoặc tên tương đương theo style source hiện tại.

Yêu cầu:

```text
- Question phải dùng prefix riêng của Update FAQ để cleanup được, ví dụ `[IT-UC64-UPDATE-FAQ] `.
- Helper trả về faqId/id.
- Cho phép tạo FAQ PUBLISHED và HIDDEN.
- Không dùng dữ liệu seed thật làm target update nếu có thể tránh.
```

Không phụ thuộc vào Create FAQ API để seed Update FAQ nếu có thể, vì Update FAQ test nên độc lập với Create FAQ endpoint. Nếu buộc phải dùng Create API, phải ghi rõ trong report.

### 10.2. Các case Integration Test tối thiểu

Tạo các case sau, điều chỉnh HTTP status theo convention source thật:

#### Authentication / Authorization

```text
1. Anonymous_Unauthorized
   Không gắn auth headers -> update FAQ -> 401 Unauthorized.

2. Staff_Forbidden
   STAFF đã đăng nhập -> update FAQ -> 403 Forbidden.

3. Admin_Forbidden
   ADMIN đã đăng nhập -> update FAQ -> 403 Forbidden nếu nghiệp vụ HO-only.

4. Visitor_Forbidden
   VISITOR đã đăng nhập -> update FAQ -> 403 Forbidden.
```

#### Happy path / DB state

```text
5. Ho_ValidPayload_UpdatesRecord
   Seed FAQ cũ.
   HO update question, answer, faqType.
   Expect OK.
   DB cùng faqId có question/answer/faqType mới.

6. Ho_Published_KeepsStatus
   Seed FAQ status PUBLISHED.
   HO update content.
   Expect OK.
   DB vẫn status PUBLISHED.

7. Ho_Hidden_KeepsStatus
   Seed FAQ status HIDDEN.
   HO update content.
   Expect OK.
   DB vẫn status HIDDEN.

8. Ho_NoChange_KeepsRecordUnchanged
   Seed FAQ cũ, lưu snapshot question/answer/faqType/status/audit.
   HO gửi payload giống hệt dữ liệu cũ.
   Expect OK.
   Nếu response DTO có Changed thì assert Changed=false.
   Reload DB và assert record giữ nguyên, updated_at/updated_by không refresh, created_at/created_by giữ nguyên.
```

#### Validation / no modification

Các test invalid phải kiểm tra không chỉ response, mà còn kiểm tra record cũ trong DB **không bị thay đổi**.

```text
9. EmptyQuestion_DoesNotModify
   Seed FAQ cũ.
   Gửi question = "" với answer marker mới.
   Expect BadRequest.
   Reload DB: question/answer/faqType/status/audit chính vẫn như cũ.

10. EmptyAnswer_DoesNotModify
    Seed FAQ cũ.
    Gửi answer = "".
    Expect BadRequest.
    Reload DB: record cũ unchanged.

11. InvalidFaqType_DoesNotModify
    Gửi faqType = PROGRAM hoặc enum legacy.
    Expect BadRequest.
    Reload DB: record cũ unchanged.

12. NonExistingFaq_NotFound
    Gọi update với id không tồn tại.
    Expect NotFound hoặc status theo convention source thật.
    Không tạo record mới.
```

#### Duplicate question

```text
13. DuplicateOtherFaq_DoesNotModify
    Seed FAQ A question = Q1.
    Seed FAQ B question = Q2.
    Update FAQ B question thành "  Q1.ToUpperInvariant()  ".
    Expect Conflict nếu project dùng 409 cho duplicate.
    Reload DB: FAQ B vẫn giữ Q2, answer/type/status cũ.
    DB vẫn chỉ có một FAQ với normalized Q1.

14. SameQuestionSelf_UpdatesRecord
    Seed FAQ A question = Q1.
    Update FAQ A với question = "  Q1  " hoặc cùng nội dung sau trim.
    Expect OK.
    Không bị conflict với chính nó.
```

#### Sanitize / XSS

```text
15. ScriptTag_SanitizedBeforeSave
    Seed FAQ cũ.
    HO update question/answer có chứa <script>.
    Expect OK.
    DB updated record không chứa raw <script> trong Question/Answer.
    Status vẫn unchanged.
```

#### Audit

```text
16. Ho_ValidPayload_UpdatesAudit
    Seed FAQ với created_at/created_by/updated_at/updated_by cũ nếu có thể.
    HO update.
    Expect updated_by = HO user id.
    updated_at thay đổi hoặc >= thời điểm trước update.
    created_at/created_by không đổi.
```

Nếu audit fields khó kiểm vì entity/source không expose, không ép. Báo rõ trong report.

### 10.3. Tên method Integration Test nên dùng

Dùng tên semantic, ngắn gọn, bám sát hành vi chính. Không dùng số HTTP code trong tên test. Vì file test đã nằm trong `UpdateFaqApiTests`, tên method không cần lặp lại `UpdateFaq` quá nhiều.

Bộ tên gợi ý:

```csharp
Anonymous_Unauthorized()
Staff_Forbidden()
Admin_Forbidden()
Visitor_Forbidden()

Ho_ValidPayload_UpdatesRecord()
Ho_Published_KeepsStatus()
Ho_Hidden_KeepsStatus()
Ho_NoChange_KeepsRecordUnchanged()

EmptyQuestion_DoesNotModify()
EmptyAnswer_DoesNotModify()
InvalidFaqType_DoesNotModify()
NonExistingFaq_NotFound()

DuplicateOtherFaq_DoesNotModify()
SameQuestionSelf_UpdatesRecord()
ScriptTag_SanitizedBeforeSave()
Ho_ValidPayload_UpdatesAudit()
```

Không bắt buộc viết toàn bộ nếu source hiện tại không hỗ trợ hoặc case bị trùng về ý nghĩa. Nhưng nếu bỏ case nào, phải giải thích lý do trong report.

Quy tắc đặt tên test case: **ngắn gọn càng tốt**.

```text
- Tên test phải nói đúng hành vi chính của test.
- Không dùng số HTTP code trong tên test, ví dụ 200/400/401/403/404/409.
- Có thể dùng tên HTTP outcome như Unauthorized, Forbidden, BadRequest, NotFound, Conflict nếu đó là hành vi chính cần test.
- Không đặt tên kiểu chung chung Returns400/Returns403/Returns200.
- Không dùng tên quá dài chỉ để liệt kê toàn bộ assert.
- Không dùng DoesNotModify / KeepsUnchanged nếu test không thật sự reload DB và kiểm tra record cũ không đổi.
- Không dùng UpdatesAudit / KeepsCreateAudit nếu test không thật sự assert updated_at, updated_by, created_at, created_by.
- Không dùng PersistsSanitizedContent nếu test không thật sự đọc DB/response để kiểm tra nội dung đã sanitize.
- Với authorization test, tên có thể ngắn vì outcome chính là Unauthorized/Forbidden.
- Với business test, ưu tiên tên theo nghiệp vụ thay vì chỉ theo HTTP status.
```

Ví dụ đúng:

```text
Anonymous_Unauthorized
Staff_Forbidden
Ho_ValidPayload_UpdatesRecord
EmptyQuestion_DoesNotModify
DuplicateOtherFaq_DoesNotModify
SameQuestionSelf_UpdatesRecord
ScriptTag_SanitizedBeforeSave
Ho_ValidPayload_UpdatesAudit
```

Ví dụ sai:

```text
UpdateFaq_WhenHoSendValidPayload_Returns200AndUpdatesQuestionAnswerTypeAndAuditFieldsAndKeepsCreatedAudit
EmptyQuestion_DoesNotModify   // sai nếu test chỉ assert BadRequest, không reload DB
ScriptTag_PersistsSanitizedContent                     // sai nếu không đọc DB/response để kiểm tra sanitize
Returns400
Returns403
```

---

## 11. Kinh nghiệm đã chốt từ Create FAQ phải áp dụng lại

### 11.1. Quy tắc đặt tên test case đã chốt

Tên test phải ngắn gọn nhưng không được mơ hồ hoặc hứa quá nội dung assert.

```text
- Tên test phải nói đúng hành vi chính của test.
- Không dùng số HTTP code trong tên test, ví dụ 200/400/401/403/404/409.
- Có thể dùng tên HTTP outcome như Unauthorized, Forbidden, BadRequest, NotFound, Conflict nếu đó là hành vi chính cần test.
- Không đặt tên kiểu chung chung Returns400/Returns403/Returns200.
- Không dùng tên quá dài chỉ để liệt kê toàn bộ assert.
- Không dùng DoesNotModify / KeepsUnchanged nếu test không thật sự reload DB và kiểm tra record cũ không đổi.
- Không dùng UpdatesAudit / KeepsCreateAudit nếu test không thật sự assert updated_at, updated_by, created_at, created_by.
- Không dùng PersistsSanitizedContent nếu test không thật sự đọc DB/response để kiểm tra nội dung đã sanitize.
- Với authorization test, tên có thể ngắn vì outcome chính là Unauthorized/Forbidden.
- Với business test, ưu tiên tên theo nghiệp vụ thay vì chỉ theo HTTP status.
```

Ví dụ đúng:

```text
Anonymous_Unauthorized
Staff_Forbidden
Ho_ValidPayload_UpdatesRecord
EmptyAnswer_DoesNotModify
DuplicateOtherFaq_DoesNotModify
ScriptTag_SanitizedBeforeSave
Ho_ValidPayload_UpdatesAudit
```

Ví dụ sai:

```text
Returns400
Returns403
Ho_ValidPayload_ReturnsOkAndUpdatesQuestionAnswerFaqTypeStatusAuditCreatedAudit
EmptyQuestion_DoesNotModify       // sai nếu test chỉ assert BadRequest, không reload DB
Ho_ValidPayload_UpdatesAudit      // sai nếu không assert audit fields thật
ScriptTag_PersistsSanitizedContent // sai nếu không đọc DB/response để kiểm tra sanitize
```

### 11.2. Tên test phải nói đúng side effect DB

Update invalid khác create invalid:

```text
Create invalid -> không persist record mới.
Update invalid -> không modify record cũ.
```

Vì vậy nếu tên có `DoesNotModify`, test phải seed record cũ, lưu snapshot trước update, gọi invalid API, reload DB rồi assert record cũ không đổi. Nếu chỉ assert HTTP BadRequest/Conflict/NotFound, không được dùng `DoesNotModify`.

### 11.3. Invalid update khác invalid create

Create invalid cần kiểm tra:

```text
Không persist record mới.
```

Update invalid cần kiểm tra:

```text
Không modify record cũ.
```

Vì vậy Update test phải seed record cũ, lưu snapshot trước update, gọi invalid API, rồi reload DB để assert unchanged.

### 11.4. Dùng marker khi cần nhận diện dữ liệu lỗi

Nếu một field invalid không thể dùng làm dấu vết, hãy dùng field còn lại làm marker.

Ví dụ với EmptyQuestion update:

```text
question = ""
answer = unique answer marker
```

Sau đó assert record cũ không bị đổi sang answer marker.

Nhưng với Update FAQ, mục tiêu chính là kiểm tra **record cũ không bị thay đổi**, nên tốt nhất là snapshot record trước/sau thay vì chỉ query marker.

### 11.5. Không viết test thừa

Dự án có nhiều UC. Không cần test mọi biến thể nhỏ. Với Update FAQ, ưu tiên:

```text
- quyền gọi API
- happy path
- validation chính
- duplicate exclude self
- status unchanged
- audit
- sanitize
```

---

### 11.6. Kinh nghiệm từ lỗi race condition giữa Create FAQ và Update FAQ

Không được lặp lại lỗi sau:

```text
CreateFaqApiTests và UpdateFaqApiTests cùng dùng DatabaseResetHelper.FaqQuestionPrefix = "[IT-UC63] ".
Khi chạy riêng từng class thì pass.
Khi chạy toàn bộ project, xUnit chạy các class song song.
DisposeAsync của class này cleanup prefix chung và xóa dữ liệu class kia đang dùng.
Kết quả gây NotFound, DbUpdateConcurrencyException, hoặc duplicate rule trả sai.
```



```text
Cleanup chỉ được xóa dữ liệu có prefix riêng của đúng test class/use case hiện tại. 
Không được cleanup bằng prefix chung như [IT-UC63], [IT-FAQ], [TEST] nếu prefix đó có thể được nhiều test class dùng chung.

Ví dụ:
- CreateFaqApiTests chỉ được cleanup question LIKE '[IT-UC63-CREATE-FAQ]%'
- UpdateFaqApiTests chỉ được cleanup question LIKE '[IT-UC64-UPDATE-FAQ]%'

Không dùng prefix có quan hệ bao phủ nhau, ví dụ:
- Sai: [IT-UC6] và [IT-UC63]
- Sai: [IT-FAQ] và [IT-FAQ-UPDATE]
vì cleanup prefix ngắn có thể xóa nhầm prefix dài.

Prefix phải đủ cụ thể, có tên use case hoặc tên test class rõ ràng.
Cleanup chỉ được xóa dữ liệu có prefix riêng, không trùng, không overlap, thuộc đúng test class/use case hiện tại. Tuyệt đối không cleanup bằng prefix chung có thể được nhiều test class dùng lại.
```

Prefix gợi ý:

```text
Create FAQ: [IT-UC63-CREATE-FAQ]
Update FAQ: [IT-UC64-UPDATE-FAQ]
```

Nếu tài liệu UC ID hiện tại khác, giữ tên nghiệp vụ trong prefix để tránh nhầm:

```text
[IT-CREATE-FAQ]
[IT-UPDATE-FAQ]
```

Không sửa production code khi lỗi xuất hiện do race condition/test cleanup. Phải sửa test infrastructure trước.

---

## 12. Commands được phép chạy

### 12.1. Luôn được chạy nếu không động DB

```bash
dotnet build
dotnet test tests/PEMS.UnitTests/PEMS.UnitTests.csproj
```

Nếu đường dẫn project khác, dùng đường dẫn thật trong source.

### 12.2. Chỉ chạy Integration Test sau khi đạt DB safety gate

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

---

## 13. Output/report sau khi làm

Sau khi hoàn thành, báo cáo bằng tiếng Việt theo format:

```md
# Báo cáo tạo test Update FAQ

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
| UC ID trong source/docs | ... |
| Endpoint update FAQ thật | ... |
| Request DTO thật | ... |
| Status có được update trong use case này không? | Không/Có + giải thích |
| Duplicate rule | ... |
| Actor hợp lệ | ... |

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

## 14. Definition of Done

Task chỉ hoàn thành khi đạt đủ các điều kiện sau:

```text
- Test code thật đã được tạo/cập nhật.
- Unit Test nằm đúng tests/PEMS.UnitTests/Faqs/UpdateFaq/.
- Integration Test nằm đúng tests/PEMS.IntegrationTests/Faqs/UpdateFaq/.
- Không trộn Unit Test và Integration Test.
- Không dùng database dev/thật.
- Không import SQL fresh-create gốc trực tiếp.
- Không đọc/copy/in secret thật.
- Không tự ý sửa production code để ép test pass.
- Unit Test đã chạy hoặc báo rõ lý do chưa chạy.
- Integration Test chỉ chạy khi đạt DB safety gate và có xác nhận.
- Test names rõ nghĩa, không dùng số HTTP nếu có thể.
- Test invalid update có assert record cũ không bị modify nếu tên có DoesNotModify.
- Test duplicate update phải exclude chính FAQ đang edit.
- Test update phải verify status unchanged nếu status không thuộc Update FAQ.
- No-change update phải test theo source hiện tại, không refresh audit nếu backend hiện tại không ghi DB.
- Integration Test assembly đã tắt parallelization hoặc report rõ lý do chưa thêm.
- Create FAQ và Update FAQ không dùng chung cleanup prefix.
- DatabaseResetHelper seed/cleanup nhận prefix riêng hoặc có helper riêng tương đương.
- Chạy riêng từng test class và chạy toàn bộ IntegrationTests nếu được phép để phát hiện race condition.
- Báo cáo pass/fail rõ ràng.
```

---

## 15. Nhắc lại các lỗi nghiêm cấm

Không được lặp lại các lỗi sau:

```text
- Chạy SQL fresh-create gốc khi chưa kiểm tra có USE pems_db.
- Tin rằng `mysql pems_test < script.sql` chắc chắn chỉ tác động pems_test.
- Chọn option tạo database/chạy test ngay chỉ vì tool ghi Recommended.
- Dùng appsettings.Development.json cho Integration Test.
- Dùng MySQL root để drop/create khi không cần.
- Đọc/copy secret thật sang file test.
- Sửa production code để test pass mà chưa được duyệt.
- Viết test Update FAQ nhưng lại đổi status như Change FAQ Visibility.
- Viết duplicate test nhưng không exclude chính FAQ đang edit.
- Đặt tên test có DoesNotModify/AndDoesNotModifyRecord nhưng không assert DB unchanged.
- Dùng chung một FaqQuestionPrefix cho nhiều use case/test class.
- Để xUnit chạy song song các Integration Test class dùng chung pems_test.
- Cleanup theo prefix quá rộng khiến test class này xóa dữ liệu test class khác.
- Thấy NotFound/DbUpdateConcurrencyException khi chạy toàn bộ test rồi vội sửa production code trước khi kiểm tra race condition test infrastructure.
```

Nếu không chắc chắn, phải dừng lại và hỏi người dùng.
