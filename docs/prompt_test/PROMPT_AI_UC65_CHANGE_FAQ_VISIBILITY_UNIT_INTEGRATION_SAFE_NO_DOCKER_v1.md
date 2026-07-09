# PROMPT AI — TẠO TEST CODE THẬT CHO UC-65 CHANGE FAQ VISIBILITY (UNIT TEST + INTEGRATION TEST, KHÔNG DOCKER, AN TOÀN DATABASE) — v1

> File này dùng để đưa cho AI Agent/Code Agent khi cần tạo hoặc cập nhật test tự động cho chức năng **Change FAQ Visibility** trong dự án PEMS.
>
> Người dùng đang gọi use case này là **UC-65 — Change FAQ Visibility**. Tuy nhiên tài liệu PEMS có thể tồn tại mismatch lịch sử về UC ID FAQ Management. AI Agent phải đọc source/docs hiện tại, ghi rõ mapping trong report, nhưng vẫn đặt tên folder/test theo nghiệp vụ ổn định: `Faqs/ChangeFaqVisibility`.
>
> Mục tiêu: **tạo test code thật, chạy được, đúng nghiệp vụ đổi trạng thái hiển thị FAQ của HO, nhưng tuyệt đối không được làm hỏng `pems_db` hoặc bất kỳ database dev/thật nào**.

---

## 0. Bối cảnh và kinh nghiệm phải kế thừa

Prompt này kế thừa các kinh nghiệm đã chốt từ test/prompt FAQ trước đó:

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
- Nếu lỗi xuất hiện khi chạy toàn bộ IntegrationTests, phải kiểm tra race condition/test cleanup trước khi nghi production code.
```

Bài học đặc biệt từ lỗi race condition giữa các FAQ test:

```text
Không dùng chung một hằng số kiểu FaqQuestionPrefix cho nhiều use case.
Không cleanup bằng prefix rộng như [IT-FAQ], [IT-UC63], [TEST].
Không dùng prefix overlap nhau, ví dụ [IT-FAQ] và [IT-FAQ-VISIBILITY].
Mỗi test class phải có prefix riêng, đủ rõ nghĩa, không trùng, không bao phủ prefix khác.
```

Prefix gợi ý cho use case này:

```text
[IT-UC65-CHANGE-FAQ-VISIBILITY]
```

Nếu source/docs hiện tại xác nhận Change FAQ Visibility là UC-67 hoặc ID khác, vẫn giữ tên nghiệp vụ trong prefix để tránh nhầm:

```text
[IT-CHANGE-FAQ-VISIBILITY]
```

Không dùng chung với:

```text
[IT-CREATE-FAQ]
[IT-UPDATE-FAQ]
[IT-VIEW-LIST-FAQ]
[IT-SEARCH-FAQ]
```

---

## 0.1. Lưu ý quan trọng về UC ID

Người dùng đang yêu cầu **UC-65 — Change FAQ Visibility**.

Tuy nhiên các tài liệu PEMS có thể đang lệch nhau, ví dụ:

```text
chỉ HO là người có quyền thay đổi trạng thái FAQs
```

Quy tắc:

```text
- Ưu tiên yêu cầu hiện tại của người dùng: UC-65 Change FAQ Visibility.
- Search source/docs hiện tại để xác nhận mapping UC ID.
- Nếu source/docs có mâu thuẫn, báo rõ trong report.
- Không hardcode UC ID vào logic test nếu source không dùng.
- Tên folder/test dùng tên nghiệp vụ ổn định: Faqs/ChangeFaqVisibility.
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

Nếu source/docs khác ví dụ trên, không tự ý đổi nghiệp vụ. Hãy ghi nhận là **UC mapping mismatch** và vẫn đặt folder theo nghiệp vụ:

```text
tests/PEMS.UnitTests/Faqs/ChangeFaqVisibility/
tests/PEMS.IntegrationTests/Faqs/ChangeFaqVisibility/
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

Tạo test tự động cho chức năng **HO đổi trạng thái hiển thị FAQ**.

Test cần gồm 2 nhóm:

```text
1. Unit Test
   Kiểm tra logic nhỏ, chủ yếu là validator/command validation nếu source có.
   Không gọi API thật.
   Không dùng database thật.

2. Integration Test
   Kiểm tra API change visibility chạy qua nhiều layer thật:
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
Existing FAQ tests hiện tại: Create FAQ, Update FAQ, View List FAQ, Search FAQ nếu đã có
Existing test infrastructure: PemsWebApplicationFactory, TestAuthHandler, DatabaseResetHelper
SQL fresh-create mới nhất trong docs/database/scripts/
```

### 2.2. Source code bắt buộc kiểm tra

AI Agent phải tự search đúng file trong project, không được bịa path.

Tối thiểu cần kiểm tra:

```text
Backend controller liên quan FAQ, đặc biệt FaqsController.
Change FAQ Visibility Command / CommandHandler / CommandValidator nếu có.
Request body DTO của change visibility nếu có.
Response DTO của change visibility nếu có.
View List FAQ query/handler để hiểu list row và status badge.
Public FAQ query/handler để kiểm tra tác động public nếu cần.
Create FAQ / Update FAQ để hiểu convention faqs, audit, status.
Faq entity.
EF Configuration của faqs nếu có.
ApplicationDbContext.
Constants/Enums liên quan FAQ status/type.
Authorization/Role check liên quan FAQ.
Existing Unit Tests: tests/PEMS.UnitTests/Faqs/...
Existing Integration Tests: tests/PEMS.IntegrationTests/Faqs/...
Existing test infrastructure: PemsWebApplicationFactory, TestAuthHandler, DatabaseResetHelper.
SQL fresh-create mới nhất trong docs/database/scripts/.
```

Nếu tên class/path trong project khác ví dụ trong file này, thì dùng tên thật trong source và báo lại trong report.

---

## 3. Nghiệp vụ Change FAQ Visibility phải giữ đúng

### 3.1. UC này chỉ đổi trạng thái hiển thị, không sửa nội dung

Change FAQ Visibility là use case riêng với Create/Update/List/Search.

Không được nhầm với:

```text
Create FAQ:
- POST /api/faqs.
- Tạo FAQ mới.
- Có thể chọn status ban đầu nếu source hỗ trợ.

Update FAQ:
- PUT /api/faqs/{faqId}.
- Sửa faqType/question/answer.
- Không đổi status nếu status thuộc Change Visibility.

View List FAQ:
- GET /api/faqs.
- Chỉ đọc danh sách.
- Không đổi DB.

Search FAQ:
- Search/filter trong list hoặc endpoint riêng.
- Không đổi DB.
```

Change FAQ Visibility chỉ được làm:

```text
- Đổi status FAQ giữa PUBLISHED và HIDDEN.
- Cập nhật audit updated_at/updated_by nếu source/schema có.
- Không sửa question.
- Không sửa answer.
- Không sửa faqType.
- Không sửa created_at/created_by.
- Không tạo FAQ mới.
- Không xóa FAQ.
```

### 3.2. Actor hợp lệ

Theo thống nhất test FAQ hiện tại:

```text
Chỉ HO được đổi trạng thái hiển thị FAQ.
```

Backend phải tự kiểm tra quyền. Frontend ẩn/hiện nút không đủ để bảo mật.

Các role khác gọi API trực tiếp phải bị chặn:

```text
Không đăng nhập -> 401 Unauthorized.
Đăng nhập nhưng không phải HO -> 403 Forbidden.
HO hợp lệ -> được đổi visibility nếu FAQ tồn tại và input hợp lệ.
```

Không mặc định Admin có toàn quyền. Nếu source hiện tại là HO-only thì Admin phải bị 403 giống Create/Update/View List FAQ.

Nên test đại diện các role không có quyền:

```text
Admin
Staff
StaffLeader
Visitor
```

Nếu source hiện tại cho actor khác quyền đổi visibility, không tự sửa production code. Báo mismatch và hỏi người dùng hoặc test theo source thật nếu đã được xác nhận.

### 3.3. Endpoint/API phải lấy từ source thật

Không được tự bịa route.

AI Agent phải search trong `FaqsController` hoặc controller tương đương để xác định endpoint thật, ví dụ có thể là:

```text
PATCH /api/faqs/{faqId}/visibility
PATCH /api/faqs/{faqId}/status
PUT /api/faqs/{faqId}/visibility
POST /api/faqs/{faqId}/visibility
PATCH /api/faqs/{faqId}/toggle-visibility
```

Một số tài liệu/prompt cũ gợi ý:

```text
PATCH /api/faqs/{faqId}/visibility
```

nhưng AI Agent vẫn phải dùng endpoint thật trong source hiện tại. Nếu endpoint khác ví dụ, báo lại trong report.

Không dùng:

```text
GET /api/faqs
GET /api/public/faqs
PUT /api/faqs/{faqId} nếu đó là Update FAQ content, không phải Change Visibility
POST /api/faqs nếu đó là Create FAQ
```

trừ khi chỉ dùng public/list endpoint để verify side effect sau khi đổi visibility.

### 3.4. Request body: explicit status hay toggle?

AI Agent phải đọc source thật để xác định API hoạt động theo kiểu nào.

Có 2 kiểu phổ biến:

#### Kiểu A — explicit target status

Ví dụ:

```http
PATCH /api/faqs/{faqId}/visibility
Content-Type: application/json

{
  "status": "HIDDEN"
}
```

hoặc:

```json
{
  "status": "PUBLISHED"
}
```

Với kiểu này, validator cần test `status` hợp lệ/không hợp lệ.

#### Kiểu B — toggle không cần body

Ví dụ:

```http
PATCH /api/faqs/{faqId}/visibility
```

Backend tự đổi:

```text
PUBLISHED -> HIDDEN
HIDDEN -> PUBLISHED
```

Với kiểu này, không được viết Unit Test/Integration Test cho `InvalidStatus` vì API không nhận status.

Quy tắc:

```text
- Không tự bịa request body.
- Không ép test InvalidStatus nếu source endpoint là toggle không body.
- Không sửa production code chỉ để khớp prompt.
- Báo rõ trong report endpoint đang dùng kiểu A hay kiểu B.
```

### 3.5. Status hợp lệ theo schema/source hiện tại

FAQ status hiện tại dùng DB value:

```text
PUBLISHED
HIDDEN
```

Không dùng raw UI/legacy value làm DB value:

```text
VISIBLE
Visible
Ẩn
Hiển thị
DRAFT
DELETED nếu source/schema không có
```

Label tiếng Việt chỉ dùng cho UI/DTO, không dùng làm request/DB value.

Nếu source hiện tại vẫn dùng `VISIBLE/HIDDEN` hoặc enum khác, không tự sửa production code. Báo mismatch và hỏi người dùng nếu rủi ro.

### 3.6. Success behavior

Khi HO đổi visibility thành công:

```text
- Nếu FAQ đang PUBLISHED và action là Hide/toggle -> status thành HIDDEN.
- Nếu FAQ đang HIDDEN và action là Show/toggle -> status thành PUBLISHED.
- question giữ nguyên.
- answer giữ nguyên.
- faqType giữ nguyên.
- created_at giữ nguyên.
- created_by giữ nguyên.
- updated_at được cập nhật theo thời điểm request nếu source/schema có.
- updated_by là HO user id nếu source/schema có.
```

Nếu response DTO có các field sau, assert theo source thật:

```text
faqId/id
status
statusLabel
changed nếu source có
updatedAt
updatedBy
message
```

Không assert field không tồn tại trong response DTO.

### 3.7. Trường hợp gửi status giống trạng thái hiện tại, chỉ áp dụng nếu API nhận body status

Ví dụ:
- FAQ hiện tại đang PUBLISHED.
- Request cũng gửi status = PUBLISHED.
- Đây là case không có thay đổi thật, nên phải đọc source để biết backend xử lý theo kiểu OK/no-change, OK/refresh audit, hay BadRequest/Conflict.

Nếu API là toggle không body, bỏ qua toàn bộ case này vì client không gửi target status.

Nếu API kiểu explicit target status và HO gửi status trùng với status hiện tại, AI Agent phải đọc source thật để xác định behavior.

Có thể là:

```text
Option 1: OK + Changed=false + không update audit.
Option 2: OK + vẫn refresh audit.
Option 3: BadRequest/Conflict vì status không đổi.
```

Quy tắc:

```text
- Test theo behavior thật của backend hiện tại.
- Không tự sửa production code theo giả định cũ.
- Nếu source/spec mismatch, báo rõ trong report.
- Nếu đặt tên KeepsRecordUnchanged/DoesNotModify thì phải reload DB và assert snapshot không đổi.
```

Nếu API là toggle không body, không có same-status case.


### 3.8. Failure behavior

Khi request fail:

```text
- FAQ status phải giữ nguyên.
- question/answer/faqType phải giữ nguyên.
- updated_at/updated_by không được bị refresh nếu request fail trước khi update.
- Không tạo FAQ mới.
- Không xóa FAQ.
```

Các failure chính:

```text
- Anonymous -> Unauthorized.
- Non-HO -> Forbidden.
- faqId không hợp lệ -> BadRequest nếu validator/source chặn.
- faqId không tồn tại -> NotFound.
- status invalid -> BadRequest nếu API nhận status body.
```

### 3.9. Tác động tới public FAQ
 
Test chính của UC-65 là DB status thay đổi đúng. Public endpoint chỉ là kiểm tra side effect phụ, không bắt buộc nếu UC-05 Public FAQ đã có test riêng.
Vì public FAQ chỉ hiển thị FAQ `PUBLISHED`, đổi visibility có tác động public:

```text
PUBLISHED -> HIDDEN:
FAQ không còn xuất hiện ở GET /api/public/faqs.

HIDDEN -> PUBLISHED:
FAQ xuất hiện lại ở GET /api/public/faqs.
```

Quy tắc test:

```text
- Ưu tiên assert DB status thay đổi đúng trong Change Visibility Integration Test.
- Có thể thêm 1-2 test side effect public nếu public endpoint đã ổn định và không làm test phụ thuộc quá nhiều vào UC-05.
- Nếu public FAQ đã có test riêng, chỉ cần test status DB và ghi trong report rằng public visibility được cover ở public FAQ tests.
```

Không dùng public endpoint để thực hiện change visibility. Chỉ dùng public endpoint để kiểm tra side effect nếu cần.

---

## 4. Phân biệt rõ Unit Test và Integration Test

### 4.1. Unit Test phù hợp với Change FAQ Visibility

Unit Test chỉ kiểm tra logic nhỏ, cô lập.

Chỉ tạo Unit Test nếu source có:

```text
ChangeFAQVisibilityCommandValidator
ChangeFAQStatusCommandValidator
ToggleFAQVisibilityCommandValidator
FaqVisibilityRequestValidator
Status parser/helper thuần
```

Các case Unit Test phù hợp:

```text
FaqId_Zero_HasError nếu command có id.
FaqId_Negative_HasError nếu source dùng signed int/long.
Status_Published_NoError nếu API nhận explicit status.
Status_Hidden_NoError nếu API nhận explicit status.
Status_Invalid_HasError nếu API nhận explicit status.
Status_NullOrEmpty_HasError nếu API nhận explicit status và required.
ValidCommand_NoErrors.
```

Không test `question`/`answer` required trong UC này, vì Change FAQ Visibility không sửa content.

Không ép Unit Test nếu endpoint toggle không có validator ngoài id hoặc source không có validator riêng.

### 4.2. Integration Test phù hợp với Change FAQ Visibility

Integration Test kiểm tra API thật:

```text
HTTP request
-> Authentication/Authorization
-> Controller
-> MediatR Command
-> Validator
-> Handler
-> EF Core
-> DB test
-> Response DTO
```

Integration Test phù hợp cho:

```text
Anonymous / role không có quyền.
HO đổi PUBLISHED -> HIDDEN.
HO đổi HIDDEN -> PUBLISHED.
DB status thay đổi đúng.
Content fields không bị đổi.
Audit updated_at/updated_by được cập nhật khi success.
Request fail không modify record.
NotFound.
InvalidStatus nếu API nhận status.
Public visibility side effect nếu cần.
```

---

## 5. Quy ước tổ chức thư mục test

### 5.1. Unit Test folder

```text
tests/PEMS.UnitTests/Faqs/ChangeFaqVisibility/
```

File gợi ý:

```text
ChangeFaqVisibilityCommandValidatorTests.cs
```

Hoặc dùng tên thật theo source:

```text
ChangeFAQVisibilityCommandValidatorTests.cs
ChangeFAQStatusCommandValidatorTests.cs
ToggleFAQVisibilityCommandValidatorTests.cs
```

### 5.2. Integration Test folder

```text
tests/PEMS.IntegrationTests/Faqs/ChangeFaqVisibility/
```

File gợi ý:

```text
ChangeFaqVisibilityApiTests.cs
```

Hoặc tên theo source thật:

```text
ChangeFAQVisibilityApiTests.cs
ChangeFaqStatusApiTests.cs
ToggleFaqVisibilityApiTests.cs
```

### 5.3. Helper dùng chung và prefix dữ liệu test

Ưu tiên dùng lại:

```text
tests/PEMS.IntegrationTests/TestInfrastructure/PemsWebApplicationFactory.cs
tests/PEMS.IntegrationTests/TestInfrastructure/TestAuthHandler.cs
tests/PEMS.IntegrationTests/TestInfrastructure/DatabaseResetHelper.cs
```

Nếu cần bổ sung helper seed FAQ cho Change Visibility, bổ sung vào `DatabaseResetHelper` hoặc helper chung tương đương. Không copy helper lặp lại trong từng file test.

Helper nên hỗ trợ:

```text
- Tạo FAQ test với prefix do test class truyền vào.
- Tạo FAQ test với status PUBLISHED/HIDDEN.
- Tạo FAQ test với faqType/question/answer tùy chỉnh.
- Tạo FAQ test với created_at/created_by/updated_at/updated_by nếu cần audit snapshot.
- Trả về faqId.
- Cleanup record theo đúng prefix được truyền vào.
- Không hardcode FaqQuestionPrefix dùng chung cho mọi FAQ test.
- Không cleanup prefix của use case/test class khác.
```

Ví dụ hướng thiết kế:

```csharp
public const string ChangeFaqVisibilityQuestionPrefix = "[IT-UC65-CHANGE-FAQ-VISIBILITY] ";

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
- Lỗi có thể biểu hiện thành NotFound, DbUpdateConcurrencyException, duplicate/filter/status trả sai.
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
3. FAQ PUBLISHED đổi được sang HIDDEN.
4. FAQ HIDDEN đổi được sang PUBLISHED.
5. Chỉ field status/audit thay đổi; content fields giữ nguyên.
6. Input/route invalid quan trọng bị chặn nếu source có validator.
7. FAQ không tồn tại trả đúng lỗi.
8. Khi request fail, DB không bị ghi/sửa sai.
9. Public visibility side effect được kiểm tra nếu scope/source cho phép.
```

Không tạo test thừa chỉ để tăng số lượng. Nếu một test không làm rõ thêm authorization, status transition, DB state, audit, failure/no-modification hoặc public visibility, không cần viết.

---

## 10. Unit Test cần tạo

Tạo Unit Test theo source thật hiện tại.

### 10.1. Command validator tests

Chỉ tạo nếu source có command validator hoặc request validator tương đương.

File gợi ý:

```text
tests/PEMS.UnitTests/Faqs/ChangeFaqVisibility/ChangeFaqVisibilityCommandValidatorTests.cs
```

Các case tối thiểu nếu source hỗ trợ:

#### Nếu API nhận explicit status body

```text
1. ValidCommand_Published_NoErrors
   faqId hợp lệ, status = PUBLISHED -> no errors.

2. ValidCommand_Hidden_NoErrors
   faqId hợp lệ, status = HIDDEN -> no errors.

3. FaqId_Zero_HasError
   faqId = 0 -> invalid.

4. FaqId_Negative_HasError
   Chỉ nếu source dùng int/long signed và cho phép test giá trị âm.

5. Status_Null_HasError
   Nếu status required.

6. Status_Empty_HasError
   Nếu status required.

7. Status_Invalid_HasError
   Ví dụ VISIBLE, DRAFT, DELETED, published nếu source yêu cầu exact uppercase.

8. Status_LegacyVisible_HasError
   VISIBLE/Visible là label/legacy, không phải DB value v10 nếu source dùng PUBLISHED/HIDDEN.
```

#### Nếu API toggle không body

```text
1. ValidCommand_NoErrors
   faqId hợp lệ -> no errors.

2. FaqId_Zero_HasError
   faqId = 0 -> invalid.

3. FaqId_Negative_HasError
   Chỉ nếu source dùng int/long signed.
```

Không test `question`, `answer`, `faqType` trong validator Change Visibility nếu command không nhận các field này.

### 10.2. Handler/helper Unit Test

Chỉ viết nếu logic có thể test cô lập rõ ràng.

Có thể viết nếu source có helper thuần:

```text
ToggleStatus(PUBLISHED) -> HIDDEN.
ToggleStatus(HIDDEN) -> PUBLISHED.
Status parser/helper.
Status label mapper.
```

Không ép Handler Unit Test nếu handler phụ thuộc EF Core/database thật nhiều. Các case DB như record not found, audit DB, status unchanged on failure nên ưu tiên Integration Test.

---

## 11. Integration Test cần tạo

Integration Test đặt trong:

```text
tests/PEMS.IntegrationTests/Faqs/ChangeFaqVisibility/
```

File gợi ý:

```text
ChangeFaqVisibilityApiTests.cs
```

### 11.1. Setup dữ liệu FAQ cho Change Visibility tests

Change Visibility cần có FAQ tồn tại trước khi gọi endpoint.

Yêu cầu:

```text
- Seed FAQ test trực tiếp qua helper DB, không phụ thuộc Create FAQ API nếu có thể.
- Question phải dùng prefix riêng của Change Visibility để cleanup được.
- Seed được cả FAQ PUBLISHED và HIDDEN.
- Snapshot record trước request để assert content fields/audit cũ khi cần.
- Cleanup chỉ xóa FAQ có prefix riêng của Change Visibility.
```

Không dùng dữ liệu seed thật làm target change visibility nếu có thể tránh.

### 11.2. Các case Integration Test tối thiểu

Điều chỉnh HTTP status, route, request body và response shape theo source thật.

#### Authentication / Authorization

```text
1. Anonymous_Unauthorized
   Không gắn auth headers -> gọi change visibility -> 401 Unauthorized.

2. Staff_Forbidden
   STAFF đã đăng nhập -> gọi change visibility -> 403 Forbidden.

3. StaffLeader_Forbidden
   STAFF + LEADER đã đăng nhập -> gọi change visibility -> 403 Forbidden.

4. Admin_Forbidden
   ADMIN đã đăng nhập -> gọi change visibility -> 403 Forbidden nếu nghiệp vụ HO-only.

5. Visitor_Forbidden
   VISITOR đã đăng nhập -> gọi change visibility -> 403 Forbidden.
```

Nếu source hiện tại cho Admin/StaffLeader đổi visibility vì nghiệp vụ đã đổi, không sửa production code. Báo mismatch và hỏi người dùng hoặc test theo source thật nếu được xác nhận.

#### Happy path / status transition

```text
6. Ho_Published_ChangesToHidden
   Seed FAQ status PUBLISHED.
   HO gọi change visibility để ẩn FAQ.
   Expect OK.
   Reload DB: status = HIDDEN.

7. Ho_Hidden_ChangesToPublished
   Seed FAQ status HIDDEN.
   HO gọi change visibility để hiện FAQ.
   Expect OK.
   Reload DB: status = PUBLISHED.
```

Nếu API là explicit status, request tương ứng là:

```text
PUBLISHED -> gửi HIDDEN.
HIDDEN -> gửi PUBLISHED.
```

Nếu API là toggle, gọi cùng endpoint không body.

#### Không sửa content fields

```text
8. Ho_ValidPayload_KeepsContentFields
   Seed FAQ với question/answer/faqType cụ thể.
   HO đổi visibility.
   Expect OK.
   Reload DB: question/answer/faqType/created_at/created_by giữ nguyên.
   Chỉ status và audit update thay đổi nếu source/schema có.
```

Có thể gộp assert này vào 2 happy path nếu test không quá dài. Nếu đã assert đủ trong happy path, không cần tách test riêng.

#### Audit

```text
9. Ho_ValidPayload_UpdatesAudit
   Seed FAQ với updated_at/updated_by null hoặc snapshot cũ.
   HO đổi visibility.
   Expect OK.
   Reload DB:
   - updated_by = HO user id.
   - updated_at không null và nằm trong khoảng thời gian request.
   - created_at/created_by giữ nguyên.
```

Nên capture thời gian như sau:

```csharp
var beforeChange = DateTime.UtcNow;
var response = await client.PatchAsJsonAsync(...);
var afterChange = DateTime.UtcNow;

Assert.True(saved.UpdatedAt!.Value >= beforeChange.AddSeconds(-2));
Assert.True(saved.UpdatedAt!.Value <= afterChange.AddSeconds(5));
```

Nếu source dùng timezone/local time khác, test theo convention source thật và báo rõ.

#### Not found / invalid route

```text
10. NonExistingFaq_NotFound
    HO gọi change visibility với faqId không tồn tại.
    Expect NotFound hoặc status theo convention source thật.
    Không tạo record mới.

11. FaqId_Zero_BadRequest
    HO gọi endpoint với faqId = 0 nếu route/validator chặn.
    Expect BadRequest.
```

Chỉ viết `FaqId_Zero_BadRequest` nếu source validator thật có rule id > 0 hoặc API route nhận 0 và validator chặn.

#### Invalid status nếu API nhận explicit status body

```text
12. InvalidStatus_DoesNotModify
    Seed FAQ status PUBLISHED hoặc HIDDEN.
    HO gửi status invalid, ví dụ VISIBLE/DRAFT.
    Expect BadRequest.
    Reload DB: status, question, answer, faqType, audit giữ nguyên.
```

Không viết case này nếu endpoint toggle không nhận body.

#### Same status/no-change nếu API nhận explicit status body

```text
13. SameStatus_HandledBySourceBehavior
    Chỉ viết nếu source có behavior rõ khi target status trùng current status.
    Test theo source thật:
    - Nếu OK + Changed=false + không update audit -> assert unchanged.
    - Nếu BadRequest/Conflict -> assert DB unchanged.
    - Nếu OK + refresh audit -> assert audit updated, nhưng phải có source rõ ràng.
```

Không tự bịa behavior.

#### Public visibility side effect nếu cần và public endpoint ổn định

```text
14. Hide_RemovesFaqFromPublicList
    Seed FAQ PUBLISHED có question/token riêng.
    HO đổi sang HIDDEN.
    Anonymous gọi GET /api/public/faqs với keyword/token nếu public endpoint hỗ trợ filter hoặc parse list nếu đủ cô lập.
    Expect FAQ không xuất hiện.

15. Show_AddsFaqToPublicList
    Seed FAQ HIDDEN có question/token riêng.
    HO đổi sang PUBLISHED.
    Anonymous gọi public FAQ endpoint.
    Expect FAQ xuất hiện.
```

Chỉ viết nếu public endpoint ổn định và có cách cô lập dữ liệu bằng keyword/prefix. Nếu public endpoint không hỗ trợ filter, không assert total tuyệt đối trên toàn DB.

### 11.3. Bộ tên method Integration Test gợi ý

Dùng tên semantic, ngắn gọn, không dùng số HTTP code trong tên test.

Bộ tối thiểu nên có:

```csharp
Anonymous_Unauthorized()
Staff_Forbidden()
StaffLeader_Forbidden()
Admin_Forbidden()
Visitor_Forbidden()

Ho_Published_ChangesToHidden()
Ho_Hidden_ChangesToPublished()
Ho_ValidPayload_KeepsContentFields()
Ho_ValidPayload_UpdatesAudit()

NonExistingFaq_NotFound()
FaqId_Zero_BadRequest()
```

Bổ sung nếu API nhận explicit status body:

```csharp
InvalidStatus_DoesNotModify()
SameStatus_HandledBySourceBehavior()
```

Bổ sung nếu public endpoint ổn định và không làm test phụ thuộc quá mức vào UC-05:

```csharp
Hide_RemovesFaqFromPublicList()
Show_AddsFaqToPublicList()
```

Không dùng tên sai/hứa quá mức:

```text
ChangeVisibility_Returns200
InvalidStatus_DoesNotModify       // sai nếu test chỉ assert BadRequest, không reload DB.
Ho_ValidPayload_UpdatesAudit      // sai nếu không assert updated_at/updated_by thật.
Ho_ValidPayload_ChangesEverything // sai vì UC này không được đổi question/answer/faqType.
```

---

## 12. Quy tắc assert DB state

Vì Change Visibility là write API, Integration Test phải kiểm tra DB state thật.

### 12.1. Success assert

Khi success, nên reload DB bằng `AsNoTracking()` và assert:

```text
status đổi đúng.
question giữ nguyên.
answer giữ nguyên.
faqType giữ nguyên.
created_at giữ nguyên.
created_by giữ nguyên.
updated_at cập nhật nếu source/schema có.
updated_by = HO user id nếu source/schema có.
```

Nếu source response có DTO đáng tin cậy, có thể assert response thêm, nhưng không thay thế DB assert.

### 12.2. Failure assert

Khi request fail và test tên có `DoesNotModify`, bắt buộc:

```text
- Seed FAQ.
- Lưu snapshot trước request.
- Gọi request fail.
- Reload DB.
- Assert status/question/answer/faqType/audit giữ nguyên.
```

Không dùng tên `DoesNotModify` nếu chỉ assert HTTP BadRequest/Forbidden/NotFound.

### 12.3. Snapshot helper

Có thể tạo helper riêng trong test class:

```csharp
private sealed record FaqSnapshot(
    string Question,
    string Answer,
    string FaqType,
    string Status,
    DateTime CreatedAt,
    ulong? CreatedBy,
    DateTime? UpdatedAt,
    ulong? UpdatedBy);
```

và helper:

```csharp
SnapshotFaqAsync(faqId)
AssertFaqUnchangedAsync(faqId, snapshot)
```

Nếu entity field nullable/type khác, dùng type thật trong source.

---

## 13. Quan hệ với các use case FAQ khác

### 13.1. Không test lại Create FAQ

Không cần test:

```text
question required
answer required
faqType required
duplicate question khi create
sanitize question/answer khi create
status default khi create
```

Các case đó thuộc Create FAQ.

### 13.2. Không test lại Update FAQ content

Không cần test:

```text
update question
update answer
update faqType
duplicate exclude self khi update
no-change update content
sanitize content khi update
```

Các case đó thuộc Update FAQ.

### 13.3. Không test sâu View List/Search FAQ

Có thể dùng list/public endpoint để verify side effect, nhưng không test sâu:

```text
pagination
sort
search by answer/type
filter faqType/status
list DTO fields
```

Các case đó thuộc View List FAQ/Search FAQ.

### 13.4. Public visibility side effect

Change Visibility có thể cần chứng minh tác động public:

```text
HIDDEN không xuất hiện public.
PUBLISHED xuất hiện public.
```

Nhưng nếu public FAQ đã có test riêng, chỉ cần assert status DB và ghi rõ trong report.

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
- SQL script đã được scan an toàn nếu cần import.
- Người dùng đã xác nhận cho phép chạy Integration Test có DB.
```

Nếu chưa đủ điều kiện, không chạy Integration Test. Hãy báo rõ:

```text
Integration Test code đã tạo/cập nhật nhưng chưa chạy vì chưa có xác nhận an toàn database.
```

Sau khi được phép chạy, nên chạy:

```bash
dotnet test tests/PEMS.IntegrationTests/PEMS.IntegrationTests.csproj --filter "FullyQualifiedName~ChangeFaqVisibilityApiTests"
dotnet test tests/PEMS.IntegrationTests/PEMS.IntegrationTests.csproj --filter "FullyQualifiedName~CreateFaqApiTests"
dotnet test tests/PEMS.IntegrationTests/PEMS.IntegrationTests.csproj --filter "FullyQualifiedName~UpdateFaqApiTests"
dotnet test tests/PEMS.IntegrationTests/PEMS.IntegrationTests.csproj --filter "FullyQualifiedName~ViewListFaqApiTests"
dotnet test tests/PEMS.IntegrationTests/PEMS.IntegrationTests.csproj
```

Mục tiêu là phát hiện cả lỗi trong test class mới và lỗi tương tác/race condition với các test FAQ cũ.

---

## 15. Output/report sau khi làm

Sau khi hoàn thành, báo cáo bằng tiếng Việt theo format:

```md
# Báo cáo tạo test UC-65 Change FAQ Visibility

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
| UC ID trong yêu cầu | UC-65 Change FAQ Visibility |
| UC ID trong source/docs nếu khác | ... |
| Endpoint change visibility thật | ... |
| Kiểu API | Explicit status body / Toggle no body |
| Request body thật | ... |
| Response DTO thật | ... |
| Actor hợp lệ | ... |
| Status hợp lệ | PUBLISHED/HIDDEN hoặc theo source thật |
| Public visibility side effect có test không? | Có/Không + lý do |

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

## 16. Definition of Done

Task chỉ hoàn thành khi đạt đủ các điều kiện sau:

```text
- Test code thật đã được tạo/cập nhật.
- Unit Test, nếu có, nằm đúng tests/PEMS.UnitTests/Faqs/ChangeFaqVisibility/.
- Integration Test nằm đúng tests/PEMS.IntegrationTests/Faqs/ChangeFaqVisibility/.
- Không trộn Unit Test và Integration Test.
- Không dùng database dev/thật.
- Không import SQL fresh-create gốc trực tiếp.
- Không đọc/copy/in secret thật.
- Không tự ý sửa production code để ép test pass.
- Unit Test đã chạy hoặc báo rõ lý do chưa chạy.
- Integration Test chỉ chạy khi đạt DB safety gate và có xác nhận.
- Test names rõ nghĩa, không dùng số HTTP nếu có thể.
- Authorization test bao phủ Anonymous và các role không phải HO đại diện.
- Success test xác nhận PUBLISHED -> HIDDEN.
- Success test xác nhận HIDDEN -> PUBLISHED.
- Success test xác nhận không sửa question/answer/faqType.
- Success test xác nhận audit updated_at/updated_by nếu source/schema có.
- Failure test có DoesNotModify phải reload DB và assert snapshot không đổi.
- InvalidStatus chỉ test nếu endpoint thật nhận status body.
- SameStatus/no-change chỉ test nếu source thật có behavior rõ.
- Không test sâu Create/Update/List/Search behavior trong UC này.
- Integration Test assembly đã tắt parallelization hoặc report rõ lý do chưa thêm.
- Change FAQ Visibility không dùng chung cleanup prefix với Create/Update/ViewList/Search FAQ.
- DatabaseResetHelper seed/cleanup nhận prefix riêng hoặc có helper riêng tương đương.
- Chạy riêng ChangeFaqVisibilityApiTests và chạy toàn bộ IntegrationTests nếu được phép để phát hiện race condition.
- Báo cáo pass/fail rõ ràng.
```

---

## 17. Nhắc lại các lỗi nghiêm cấm

Không được lặp lại các lỗi sau:

```text
- Dùng GET /api/faqs để thực hiện Change Visibility.
- Dùng PUT /api/faqs/{id} Update FAQ content để đổi status nếu source đã tách endpoint visibility riêng.
- Nhét Change Visibility vào Create FAQ hoặc Update FAQ test.
- Test question/answer/faqType required trong UC Change Visibility.
- Gửi status = VISIBLE/Ẩn/Hiển thị nếu source/schema dùng PUBLISHED/HIDDEN.
- Viết InvalidStatus_DoesNotModify nhưng chỉ assert BadRequest, không reload DB.
- Viết UpdatesAudit nhưng không assert updated_at/updated_by thật.
- Không assert question/answer/faqType giữ nguyên sau khi đổi visibility.
- Tự bịa request body status nếu endpoint thật là toggle không body.
- Tự bịa endpoint toggle nếu source thật là explicit status body.
- Assert public visibility bằng exact total count khi public DB có seed khác và không cô lập bằng keyword/prefix.
- Dùng chung FaqQuestionPrefix với Create/Update/ViewList/Search FAQ.
- Cleanup theo prefix quá rộng khiến test class này xóa dữ liệu test class khác.
- Để xUnit chạy song song các Integration Test class dùng chung pems_test.
- Thấy NotFound/DbUpdateConcurrencyException khi chạy toàn bộ test rồi vội sửa production code trước khi kiểm tra race condition test infrastructure.
- Chạy SQL fresh-create gốc khi chưa kiểm tra có USE pems_db.
- Tin rằng `mysql pems_test < script.sql` chắc chắn chỉ tác động pems_test.
- Dùng appsettings.Development.json cho Integration Test.
- Đọc/copy secret thật sang file test.
- Sửa production code để test pass mà chưa được duyệt.
```

Nếu không chắc chắn, phải dừng lại và hỏi người dùng.
