# PROMPT AI — TẠO TEST CODE THẬT CHO CREATE FAQ (UNIT TEST + INTEGRATION TEST, KHÔNG DOCKER, AN TOÀN DATABASE)

> File này dùng để đưa cho AI Agent/Code Agent khi cần tạo test tự động cho chức năng **Create FAQ** trong dự án PEMS.
>
> Mục tiêu quan trọng nhất: **tạo test code thật, chạy được, nhưng tuyệt đối không được làm hỏng database dev/thật**.

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

Tạo test tự động cho chức năng **Create FAQ**.

Test cần gồm 2 nhóm:

```text
1. Unit Test
   Kiểm tra logic nhỏ, chạy cô lập, không gọi API thật, không dùng database thật.

2. Integration Test
   Kiểm tra nhiều phần chạy cùng nhau, ví dụ API + Authentication/Authorization giả + Controller + MediatR + Validator + Handler + database test riêng.
```

Sau khi hoàn thành, team phải có thể biết rõ:

```text
- Test code đã được tạo ở đâu.
- Test nào là Unit Test.
- Test nào là Integration Test.
- Test nào pass.
- Test nào fail.
- Nếu chưa chạy được test nào thì lý do là gì.
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
Report 3.1_UCS_Template.docx nếu cần đối chiếu UC/spec
Report 5.2_L1-UnitTests_Template.xlsx nếu cần đối chiếu format unit test report
Source code backend hiện tại
Existing tests hiện tại
```

### 2.2. Source code bắt buộc kiểm tra

AI Agent phải tự search đúng file trong project, không được bịa path.

Tối thiểu cần kiểm tra:

```text
Backend controller liên quan FAQ
Create FAQ Command
Create FAQ CommandHandler
Create FAQ CommandValidator
Create FAQ Request/Response DTO
Faq entity
EF Configuration của faqs
DbContext
Constants/Enums liên quan FAQ type/status
Authorization/Role check liên quan FAQ
Existing Unit Tests
Existing Integration Tests
Existing test infrastructure
SQL fresh-create mới nhất trong docs/database/scripts/
```

Nếu tên class/path trong project khác ví dụ trong file này, hãy dùng tên thật trong source hiện tại.

---

## 3. Nghiệp vụ Create FAQ phải giữ đúng

Chức năng Create FAQ dùng cho màn quản lý FAQ nội bộ.

### 3.1. Actor hợp lệ

```text
Chỉ HO được tạo FAQ.
```

Backend phải tự kiểm tra quyền. Frontend ẩn/hiện nút không đủ để bảo mật.

Các role khác gọi API trực tiếp phải bị chặn.

```text
Không đăng nhập -> 401 Unauthorized.
Đăng nhập nhưng không phải HO -> 403 Forbidden.
HO hợp lệ -> được tạo FAQ nếu dữ liệu hợp lệ.
```

### 3.2. Field đầu vào

Request Create FAQ gồm các field nghiệp vụ chính:

```text
faqType
question
answer
status
```

Không dùng:

```text
languageCode
language_code
```

FAQ hiện tại chỉ dùng tiếng Việt.

### 3.3. FAQ type hợp lệ

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

### 3.4. Status hợp lệ

Chỉ dùng:

```text
PUBLISHED
HIDDEN
```

Nếu request không truyền status, hoặc status null/empty theo convention hiện tại của code, default nên là:

```text
PUBLISHED
```

Không dùng status cũ:

```text
VISIBLE
Visible
Hidden dạng raw label UI
```

### 3.5. Validation bắt buộc

```text
question bắt buộc, sau khi trim không được rỗng.
answer bắt buộc, sau khi trim không được rỗng.
faqType bắt buộc và phải thuộc enum hiện tại.
status nếu có thì phải thuộc PUBLISHED/HIDDEN.
```

### 3.6. Duplicate question

Không cho tạo FAQ trùng câu hỏi.

Quy tắc so sánh:

```text
Trim khoảng trắng đầu/cuối.
So sánh không phân biệt hoa/thường.
Kiểm tra trên toàn bộ bảng faqs, gồm cả PUBLISHED và HIDDEN.
```

Ví dụ trùng:

```text
"Làm sao đăng nhập?"
"  làm sao đăng nhập?  "
"LÀM SAO ĐĂNG NHẬP?"
```

### 3.7. Sanitize nội dung

Trước khi lưu, question/answer phải được sanitize theo logic hiện tại của project.

Mục tiêu:

```text
Không lưu script nguy hiểm.
Không để XSS đơn giản lọt qua.
Sau sanitize, nội dung không được rỗng.
```

### 3.8. Audit fields

Khi tạo thành công, dữ liệu phải có audit theo convention hiện tại:

```text
created_at
created_by
updated_at
updated_by
```

Không tự bịa field nếu schema hiện tại khác. Phải kiểm tra SQL/entity thật trước.

---

## 4. Phân biệt rõ Unit Test và Integration Test

### 4.1. Unit Test là gì?

Unit Test kiểm tra một phần nhỏ của code.

Ví dụ:

```text
Validator nhận question rỗng -> báo lỗi.
Validator nhận faqType sai -> báo lỗi.
Handler nhận status null -> set default PUBLISHED.
Helper sanitize script -> không lưu script.
```

Unit Test:

```text
- Chạy nhanh.
- Không gọi API thật.
- Không dùng database thật.
- Không phụ thuộc appsettings.Development.json.
- Không phụ thuộc Google SSO/SMTP/Google Drive thật.
- Dependency phải mock/fake rõ ràng.
```

### 4.2. Integration Test là gì?

Integration Test kiểm tra nhiều phần chạy cùng nhau.

Ví dụ:

```text
Gọi POST /api/faqs không token -> 401.
Gọi POST /api/faqs bằng role STAFF -> 403.
Gọi POST /api/faqs bằng role HO -> tạo FAQ thành công.
Gọi POST /api/faqs với question trùng -> bị chặn và DB test không tạo thêm record.
```

Integration Test có thể dùng:

```text
- API thật trong môi trường Testing.
- Authentication giả.
- Database test riêng.
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

Nếu logic phụ thuộc nhiều vào database, ví dụ:

```text
duplicate question
EF Core query phức tạp
kiểm tra dữ liệu thật sự đã lưu/chưa lưu
transaction
unique constraint
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

Với Create FAQ:

```text
tests/PEMS.UnitTests/Faqs/CreateFaq/
```

File gợi ý:

```text
CreateFaqCommandValidatorTests.cs
CreateFaqCommandHandlerTests.cs
```

### 5.2. Integration Test folder

Integration Test đặt trong:

```text
tests/PEMS.IntegrationTests
```

Bên trong dùng format:

```text
tests/PEMS.IntegrationTests/[Module]/[UseCaseName]/
```

Với Create FAQ:

```text
tests/PEMS.IntegrationTests/Faqs/CreateFaq/
```

File gợi ý:

```text
CreateFaqApiTests.cs
```

### 5.3. Helper dùng chung

Unit Test helper dùng chung đặt tại:

```text
tests/PEMS.UnitTests/TestHelpers/
```

Integration Test infrastructure dùng chung đặt tại:

```text
tests/PEMS.IntegrationTests/TestInfrastructure/
```

Ví dụ:

```text
PemsWebApplicationFactory.cs
TestAuthHandler.cs
DatabaseResetHelper.cs
TestHttpClientFactory.cs
TestDataFactory.cs
```

Không copy lặp helper giống nhau vào từng folder use case.

### 5.4. Không trộn loại test

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

Integration Test cần môi trường test riêng.

Tạo hoặc cập nhật class:

```text
PemsWebApplicationFactory
```

Class này dùng để khởi động API PEMS trong môi trường Testing.

Yêu cầu:

```text
- Kế thừa WebApplicationFactory<Program> nếu project đang dùng convention này.
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

---

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
dotnet test IntegrationTests nếu test sẽ ghi vào DB
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

---

### 8.3. Không import trực tiếp SQL fresh-create gốc

Không chạy trực tiếp file SQL gốc bằng lệnh kiểu này:

```bash
mysql pems_test < original_fresh_create.sql
```

Lý do:

Lệnh trên chỉ chọn `pems_test` làm database mặc định lúc bắt đầu chạy.  
Nhưng nếu bên trong file SQL có lệnh:

```sql
USE pems_db;
```

thì MySQL sẽ chuyển sang database `pems_db`.

Nếu bên trong file SQL có lệnh:

```sql
DROP DATABASE IF EXISTS pems_db;
CREATE DATABASE pems_db;
```

thì database dev `pems_db` có thể bị xóa và tạo lại, dù command ban đầu có ghi `pems_test`.

Vì vậy:

```text
Tên database trên command line không đủ để bảo vệ database dev.
File SQL bên trong có quyền chuyển database bằng lệnh USE.
File SQL bên trong cũng có thể xóa database khác bằng DROP DATABASE.
```

Quy tắc bắt buộc:

```text
1. Không import trực tiếp SQL fresh-create gốc.
2. Trước khi import, phải kiểm tra file SQL có DROP DATABASE / CREATE DATABASE / USE hay không.
3. Nếu file SQL có pems_db, không được chạy trực tiếp.
4. Phải tạo bản copy tạm riêng cho pems_test.
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

---

### 8.5. Cách chuẩn bị SQL test an toàn

Nếu cần import schema vào `pems_test`, không sửa file SQL gốc.

Phải tạo bản copy tạm dành riêng cho test, ví dụ:

```text
docs/testing/tmp/fresh_create_for_pems_test.sql
```

Trong bản copy tạm, thay database dev bằng database test:

```text
pems_db -> pems_test
```

Sau khi thay, kiểm tra lại:

```bash
grep -n "pems_db" docs/testing/tmp/fresh_create_for_pems_test.sql
grep -n "DROP DATABASE" docs/testing/tmp/fresh_create_for_pems_test.sql
grep -n "CREATE DATABASE" docs/testing/tmp/fresh_create_for_pems_test.sql
grep -n "USE " docs/testing/tmp/fresh_create_for_pems_test.sql
```

Điều kiện an toàn trước khi import:

```text
- Không còn `pems_db`.
- Không còn `USE pems_db`.
- Không còn `DROP DATABASE pems_db`.
- Không còn `CREATE DATABASE pems_db`.
- Nếu có DROP/CREATE/USE thì chỉ được trỏ tới `pems_test`.
```

Nếu còn dấu vết database dev/thật, không được chạy import.

---

### 8.6. Ưu tiên user MySQL riêng cho test

Ưu tiên dùng MySQL user riêng cho Integration Test.

User này chỉ nên có quyền trên:

```text
pems_test
```

Không nên dùng:

```text
root
user có quyền DROP pems_db
user đang dùng cho app dev thật
```

Nếu hiện tại chỉ có user quyền rộng, phải báo rõ rủi ro và hỏi người dùng trước khi chạy bất kỳ lệnh DB ghi/xóa nào.

---

### 8.7. Nếu chưa có database test

Nếu chưa có `pems_test`, không tự ý tạo ngay.

Hãy tạo file hướng dẫn cho dev/tester, ví dụ:

```text
docs/testing/CREATE_TEST_DATABASE.md
```

Nội dung có thể gồm:

```sql
CREATE DATABASE pems_test
CHARACTER SET utf8mb4
COLLATE utf8mb4_unicode_ci;
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

---

### 8.8. Không đọc/copy/in secret thật

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

### 8.9. Nếu phát hiện thao tác nhầm database

Nếu phát hiện đã thao tác nhầm vào database dev/thật, phải dừng ngay.

Không được tự sửa tiếp.

Không được chạy import lại.

Không được chạy test tiếp.

Phải báo cáo:

```text
- Database nào bị ảnh hưởng.
- Command nào đã chạy.
- File SQL nào đã chạy.
- Các dòng SQL nguy hiểm đã xuất hiện.
- Số lượng bảng hiện tại của database bị ảnh hưởng.
- Số lượng record chính hiện tại.
- Có khả năng mất dữ liệu không.
- Đề xuất backup/restore trước khi tiếp tục.
```

---

## 9. Nếu gặp hộp thoại lựa chọn DB setup

Nếu tool/AI Agent hiển thị lựa chọn kiểu:

```text
1. Có, tạo pems_test và chạy test luôn.
2. Không, chỉ scaffold code + hướng dẫn, không động DB.
3. Other.
```

Quy tắc chọn:

```text
Nếu người dùng chưa xác nhận rõ ràng cho phép thao tác DB:
→ Chọn option 2: chỉ scaffold code + hướng dẫn, không động DB.

Nếu người dùng đã xác nhận rõ ràng cho phép thao tác DB:
→ Vẫn phải scan SQL, báo cáo rủi ro, xác nhận database test, rồi mới làm.
```

Không được chọn option tạo DB/chạy test ngay chỉ vì option đó ghi "Recommended".

---

## 10. Unit Test cần tạo

Tạo Unit Test theo source thật hiện tại.

### 10.1. Validator tests

Tạo test cho validator Create FAQ.

Các case tối thiểu:

```text
1. question null/empty/whitespace -> invalid.
2. answer null/empty/whitespace -> invalid.
3. faqType null/empty/invalid -> invalid.
4. faqType hợp lệ -> valid.
5. status invalid -> invalid.
6. status PUBLISHED -> valid.
7. status HIDDEN -> valid.
8. status null/empty nếu business rule cho phép default -> valid.
9. request hợp lệ đầy đủ -> valid.
```

Nên dùng:

```text
xUnit
FluentAssertions
FluentValidation.TestHelper nếu project đã dùng hoặc phù hợp
```

### 10.2. Handler tests

Chỉ viết Handler Unit Test nếu dependency có thể mock/fake rõ ràng.

Các case có thể viết ở unit-level:

```text
1. status null/empty -> default PUBLISHED.
2. question/answer được trim trước khi lưu nếu logic nằm trong Handler.
3. sanitize đơn giản nếu logic nằm trong Handler/helper có thể test cô lập.
4. audit fields được set theo current user/time provider nếu dependency mock được.
```

Không ép test duplicate DB bằng Unit Test nếu Handler query trực tiếp EF Core phức tạp.

Nếu duplicate phụ thuộc database thật/EF Core behavior, chuyển sang Integration Test và báo rõ lý do.

---

## 11. Integration Test cần tạo

Integration Test đặt trong:

```text
tests/PEMS.IntegrationTests/Faqs/CreateFaq/
```

Tạo file gợi ý:

```text
CreateFaqApiTests.cs
```

Các case tối thiểu:

```text
1. POST /api/faqs không đăng nhập -> 401.
2. POST /api/faqs với role STAFF -> 403.
3. POST /api/faqs với role ADMIN -> 403 nếu business rule là HO only.
4. POST /api/faqs với role VISITOR -> 403.
5. POST /api/faqs với role HO + payload hợp lệ + status PUBLISHED -> success.
6. POST /api/faqs với role HO + payload hợp lệ + status HIDDEN -> success.
7. POST /api/faqs với role HO + không truyền status -> default PUBLISHED.
8. POST /api/faqs với question rỗng -> 400.
9. POST /api/faqs với answer rỗng -> 400.
10. POST /api/faqs với faqType invalid -> 400.
11. POST /api/faqs với status invalid -> 400.
12. POST /api/faqs với question trùng trim + case-insensitive -> conflict/validation error theo convention hiện tại.
13. Khi request fail, database test không tạo record mới.
14. Khi request success, database test có record đúng faqType/status/question/answer/audit.
```

HTTP status cụ thể phải theo convention hiện tại của project. Nếu project dùng 409 cho duplicate, test 409. Nếu project dùng 400 hoặc custom error envelope, test theo source thật và ghi rõ.

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
# Báo cáo tạo test Create FAQ

## 1. Tóm tắt
[Đã tạo/sửa những gì]

## 2. File đã tạo/sửa
| Loại | File | Mục đích |
|---|---|---|
| Unit Test | ... | ... |
| Integration Test | ... | ... |
| Test Infrastructure | ... | ... |
| Docs/Config mẫu | ... | ... |

## 3. Unit Test đã tạo
[Liệt kê case]

## 4. Integration Test đã tạo
[Liệt kê case]

## 5. Kiểm tra an toàn database
| Mục | Kết quả |
|---|---|
| Có dùng Docker/Testcontainers không? | Không |
| Database test dự kiến | pems_test hoặc tên thật |
| Có dùng pems_db không? | Không |
| Có đọc/copy secret thật không? | Không |
| Có scan SQL trước khi import không? | Có/Chưa cần |
| Có chạy lệnh ghi DB chưa? | Có/Không + giải thích |

## 6. Kết quả chạy lệnh
```text
[dotnet build result]
[dotnet test UnitTests result]
[IntegrationTests result nếu đã được phép chạy]
```

## 7. Test fail nếu có
| Test | Expected | Actual | Nhận định |
|---|---|---|---|
| ... | ... | ... | ... |

## 8. Production code issue nếu có
[Chỉ báo cáo, không tự sửa nếu chưa được duyệt]

## 9. Việc cần người dùng xác nhận thêm
[Nếu còn]
```

Không được báo “hoàn thành” nếu chưa build/test hoặc chưa nói rõ lý do không chạy được.

---

## 14. Definition of Done

Task chỉ hoàn thành khi đạt đủ các điều kiện sau:

```text
- Test code thật đã được tạo/cập nhật.
- Unit Test nằm đúng tests/PEMS.UnitTests/[Module]/[UseCaseName]/.
- Integration Test nằm đúng tests/PEMS.IntegrationTests/[Module]/[UseCaseName]/.
- Không trộn Unit Test và Integration Test.
- Không dùng Docker/Testcontainers.
- Không dùng database dev/thật.
- Không import SQL fresh-create gốc trực tiếp.
- Không đọc/copy/in secret thật.
- Không tự ý sửa production code để ép test pass.
- Unit Test đã chạy hoặc báo rõ lý do chưa chạy.
- Integration Test chỉ chạy khi đạt DB safety gate và có xác nhận.
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
```

Nếu không chắc chắn, phải dừng lại và hỏi người dùng.
