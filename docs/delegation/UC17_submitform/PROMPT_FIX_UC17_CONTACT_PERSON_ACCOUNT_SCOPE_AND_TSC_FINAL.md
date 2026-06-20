# PROMPT_FIX_UC17_CONTACT_PERSON_ACCOUNT_SCOPE_AND_TSC_FINAL

## Mục tiêu

Fix nốt logic và UI của public form **UC-17 Submit Visit Request** theo quyết định nghiệp vụ chính xác:

```text
1. Email ở phần "Thông tin người đăng ký form" chỉ dùng để nhận OTP xác thực gửi form.
2. "Thông tin đầu mối liên hệ" mới là nguồn để tạo/link tài khoản VISITOR.
3. Nếu người dùng tích "Tôi là người đăng ký form" / "Tôi là đầu mối liên hệ", hệ thống auto-fill thông tin người đăng ký form sang "Thông tin đầu mối liên hệ".
4. Bản chất tạo/link tài khoản VISITOR luôn lấy từ "Thông tin đầu mối liên hệ", kể cả khi thông tin đó được auto-fill từ người đăng ký.
5. Nếu chọn "Liên cơ sở", bắt buộc chọn ít nhất 2 cơ sở, không tự động đổi về "Một cơ sở".
6. Giữ email khách trong danh sách khách là required.
7. Không đưa lại CCCD/CMND/passportId.
8. Fix lỗi TypeScript còn lại ở component ExcelUpload mồ côi để `npx tsc --noEmit` pass.
```

Không sửa lại core flow UC-17 nếu không cần. Real flow hiện tại vẫn là:

```text
POST /api/visit-requests/initiate
POST /api/visit-requests/verify
POST /api/visit-requests/resend-otp
```

---

## 0. Quyết định nghiệp vụ bắt buộc

### 0.1 Phân biệt 2 nhóm thông tin

Trong form public UC-17 có 2 nhóm thông tin khác nhau:

```text
A. Thông tin người đăng ký form
B. Thông tin đầu mối liên hệ
```

Hai nhóm này có thể là cùng một người hoặc hai người khác nhau.

---

## 1. Thông tin người đăng ký form

### Ý nghĩa

Người đăng ký form là người:

```text
- đang thao tác trên form
- nhập email để nhận mã OTP
- xác thực OTP
- gửi form chính thức lên hệ thống
```

### Email người đăng ký form dùng để làm gì?

```text
registrantEmail chỉ dùng để:
- gửi OTP
- verify OTP
- xác nhận người thực hiện gửi form
```

### Email người đăng ký form KHÔNG dùng để làm gì?

```text
registrantEmail KHÔNG mặc định dùng để tạo tài khoản VISITOR
nếu người đăng ký form khác đầu mối liên hệ.
```

### Note bắt buộc dưới email người đăng ký form

Thêm helper text:

```text
Email này dùng để nhận mã OTP xác thực trước khi gửi form đăng ký.
```

Nếu muốn rõ hơn:

```text
Email này chỉ dùng để nhận mã OTP xác thực việc gửi form. Tài khoản theo dõi yêu cầu sẽ được tạo theo email ở phần Thông tin đầu mối liên hệ.
```

---

## 2. Thông tin đầu mối liên hệ

### Ý nghĩa

Đầu mối liên hệ là người:

```text
- FPTU sẽ liên hệ sau khi yêu cầu được gửi
- được lưu vào contact_person_json
- được tạo/link tài khoản VISITOR
- có thể đăng nhập Google lần sau để theo dõi yêu cầu
```

### Tài khoản VISITOR tạo từ đâu?

Tài khoản VISITOR phải được tạo/link từ:

```text
Thông tin đầu mối liên hệ
```

Cụ thể dùng:

```text
contactFullName
contactEmail
contactPhone
contactOrganization
contactJobTitle
contactNationality
```

Không lấy trực tiếp từ `registrant*` trừ khi người dùng đã tích checkbox để auto-fill sang contact.

### Note bắt buộc ở section "Thông tin đầu mối liên hệ"

Thêm note ở đầu section:

```text
Thông tin đầu mối liên hệ sẽ được FPTU sử dụng để trao đổi về yêu cầu tham quan. Email đầu mối liên hệ cũng là email dùng để tạo tài khoản VISITOR và đăng nhập Google lần sau để theo dõi yêu cầu.
```

### Note dưới email đầu mối liên hệ

```text
Email này sẽ được dùng để tạo tài khoản VISITOR. Lần sau, đầu mối liên hệ có thể đăng nhập bằng Google với email này để theo dõi yêu cầu đã gửi.
```

---

## 3. Checkbox "Tôi là đầu mối liên hệ"

### Label đề xuất

```text
Tôi cũng là đầu mối liên hệ
```

hoặc:

```text
Người đăng ký form cũng là đầu mối liên hệ
```

### Helper text dưới checkbox

```text
Khi chọn mục này, hệ thống sẽ tự điền Thông tin đầu mối liên hệ từ Thông tin người đăng ký form.
```

### Logic khi tích checkbox

Khi user tích checkbox:

```text
contactFullName = registrantFullName
contactOrganization = registrantOrganization
contactJobTitle = registrantJobTitle
contactPhone = registrantPhone
contactEmail = registrantEmail
contactNationality = registrantNationality
```

Sau đó tài khoản VISITOR vẫn được tạo/link bằng `contactEmail`.

Nghĩa là:

```text
Không tạo account trực tiếp từ registrantEmail.
Mà là registrantEmail được copy sang contactEmail, rồi account được tạo từ contactEmail.
```

### Logic khi bỏ tích checkbox

Khi user bỏ tích:

```text
- Cho phép nhập Thông tin đầu mối liên hệ riêng.
- Không tự ghi đè thông tin contact đang nhập riêng.
- Nếu trước đó đã auto-fill, có thể giữ dữ liệu để user chỉnh hoặc clear tùy UX hiện tại.
```

Khuyến nghị:

```text
- Khi bỏ tích, giữ dữ liệu đã auto-fill để user chỉnh sửa tiếp.
- Không clear trắng ngay để tránh mất dữ liệu ngoài ý muốn.
```

---

## 4. Mapping dữ liệu theo SQL

Dùng SQL full mới nhất:

```text
database/scripts/pems_full(3).sql
```

### 4.1 visit_requests

Các field `registrant_*` lưu người đăng ký form:

```text
registrant_full_name
registrant_organization
registrant_job_title
registrant_phone
registrant_email
registrant_nationality
```

Field `contact_person_json` lưu đầu mối liên hệ:

```json
{
  "fullName": "Tran Van B",
  "organization": "ABC University",
  "jobTitle": "Coordinator",
  "phone": "0911111111",
  "email": "contact@example.com",
  "nationality": "Vietnam"
}
```

Field `visitor_user_id` link tới user VISITOR được tạo/link bằng email đầu mối liên hệ:

```text
visitor_user_id = user_id của contactEmail
```

### 4.2 Không thêm cột mới

Không thêm các cột kiểu:

```text
contact_user_id
contact_email
contact_full_name
```

nếu SQL hiện tại đã dùng `contact_person_json`.

Không thêm lại:

```text
passportId
Số HC/CMND
CCCD
CMND
identityNumber
```

---

## 5. Backend rule bắt buộc

Ở handler tạo request sau khi OTP đúng:

```text
1. Verify OTP bằng registrantEmail.
2. Validate full form.
3. Build contact person từ contact fields.
4. Create/link VISITOR user bằng contactEmail.
5. Set visit_requests.visitor_user_id = visitor user được tạo từ contactEmail.
6. Lưu registrant_* từ thông tin người đăng ký form.
7. Lưu contact_person_json từ thông tin đầu mối liên hệ.
8. Insert visit_requests.status = PENDING_APPROVAL.
9. Insert visit_request_campuses.status = WAITING_REQUEST_APPROVAL.
```

### Pseudo code

```csharp
var registrantEmail = Normalize(request.RegistrantEmail);
var contactEmail = Normalize(request.ContactEmail);

// OTP chỉ verify email người đăng ký form
await VerifyOtpAsync(registrantEmail, request.OtpCode, cancellationToken);

// VISITOR account tạo/link bằng email đầu mối liên hệ
var visitorUser = await CreateOrLinkVisitorUserAsync(
    email: contactEmail,
    fullName: request.ContactFullName,
    phone: request.ContactPhone,
    organization: request.ContactOrganization,
    cancellationToken);

var contactPersonJson = Serialize(new
{
    fullName = request.ContactFullName,
    organization = request.ContactOrganization,
    jobTitle = request.ContactJobTitle,
    phone = request.ContactPhone,
    email = contactEmail,
    nationality = request.ContactNationality
});

var visitRequest = new VisitRequest
{
    VisitorUserId = visitorUser.UserId,

    RegistrantFullName = request.RegistrantFullName,
    RegistrantOrganization = request.RegistrantOrganization,
    RegistrantJobTitle = request.RegistrantJobTitle,
    RegistrantPhone = request.RegistrantPhone,
    RegistrantEmail = registrantEmail,
    RegistrantNationality = request.RegistrantNationality,

    ContactPersonJson = contactPersonJson,

    Status = VisitRequestStatuses.PendingApproval,
    EmailVerifiedAt = now
};
```

### Nếu contactEmail khác registrantEmail

Theo nghiệp vụ hiện tại:

```text
- Vẫn cho phép.
- OTP vẫn gửi tới registrantEmail.
- VISITOR account vẫn tạo/link bằng contactEmail.
```

Khuyến nghị ghi TODO bảo mật tương lai:

```text
Future hardening: nếu contactEmail khác registrantEmail, có thể gửi email thông báo/invitation hoặc verify thêm contactEmail.
```

Không block flow hiện tại nếu owner đã chốt.

---

## 6. Frontend rule bắt buộc

### 6.1 Data model

Form cần có đủ 2 nhóm:

```ts
type VisitRequestFormValues = {
  // Người đăng ký form
  registrantFullName: string;
  registrantOrganization: string;
  registrantJobTitle?: string | null;
  registrantPhone?: string | null;
  registrantEmail: string;
  registrantNationality?: string | null;

  // Đầu mối liên hệ
  contactSameAsRegistrant: boolean;
  contactFullName: string;
  contactOrganization: string;
  contactJobTitle?: string | null;
  contactPhone?: string | null;
  contactEmail: string;
  contactNationality?: string | null;

  // Các field khác...
};
```

Nếu code hiện tại dùng tên khác thì giữ tên đang dùng nhưng phải đảm bảo mapping đúng ý nghĩa.

### 6.2 Auto-fill khi checkbox bật

Nếu `contactSameAsRegistrant = true`, contact fields phải sync theo registrant fields.

Pseudo:

```ts
useEffect(() => {
  if (!contactSameAsRegistrant) return;

  setValue("contactFullName", registrantFullName);
  setValue("contactOrganization", registrantOrganization);
  setValue("contactJobTitle", registrantJobTitle);
  setValue("contactPhone", registrantPhone);
  setValue("contactEmail", registrantEmail);
  setValue("contactNationality", registrantNationality);
}, [
  contactSameAsRegistrant,
  registrantFullName,
  registrantOrganization,
  registrantJobTitle,
  registrantPhone,
  registrantEmail,
  registrantNationality
]);
```

### 6.3 Payload gửi backend

Payload initiate/verify phải gửi đủ 2 nhóm:

```json
{
  "registrantFullName": "Nguyen Van A",
  "registrantOrganization": "ABC University",
  "registrantJobTitle": "Officer",
  "registrantPhone": "0900000000",
  "registrantEmail": "registrant@example.com",
  "registrantNationality": "Vietnam",

  "contactFullName": "Tran Van B",
  "contactOrganization": "ABC University",
  "contactJobTitle": "Coordinator",
  "contactPhone": "0911111111",
  "contactEmail": "contact@example.com",
  "contactNationality": "Vietnam"
}
```

Nếu `contactSameAsRegistrant = true`, payload sẽ có contact giống registrant.

---

## 7. Validation

### 7.1 Người đăng ký form

```text
[ ] registrantFullName required.
[ ] registrantOrganization required.
[ ] registrantEmail required + email format.
[ ] registrantPhone optional hoặc theo rule hiện tại.
[ ] registrantNationality optional hoặc theo rule hiện tại.
```

### 7.2 Đầu mối liên hệ

```text
[ ] contactFullName required.
[ ] contactOrganization required.
[ ] contactEmail required + email format.
[ ] contactPhone optional hoặc theo rule hiện tại.
[ ] contactJobTitle optional.
[ ] contactNationality optional.
```

Nếu `contactSameAsRegistrant = true`, contact fields auto-filled nên validation vẫn pass.

### 7.3 Guest email

Giữ nguyên:

```text
guestMembers[].email = required
```

Không nới optional trong task này.

---

## 8. Rule chọn liên cơ sở

### 8.1 Không auto downgrade

Nếu user chọn:

```text
visitScope = MULTI_CAMPUS
```

nhưng chỉ chọn 1 campus, không được tự động đổi về:

```text
visitScope = SINGLE_CAMPUS
```

### 8.2 Validation đúng

```text
SINGLE_CAMPUS:
- phải có đúng 1 campus.

MULTI_CAMPUS:
- phải có ít nhất 2 campus.
```

### 8.3 Error message

Khi `MULTI_CAMPUS` chỉ có 1 campus:

```text
Yêu cầu liên cơ sở cần ít nhất 2 cơ sở. Vui lòng thêm cơ sở thứ hai hoặc đổi sang Một cơ sở.
```

Có thể thêm button:

```text
Đổi sang Một cơ sở
```

Nhưng chỉ đổi scope khi user chủ động bấm.

---

## 9. Không đưa lại identity/passport field

Đảm bảo không còn:

```text
passportId
identityNumber
citizenId
idNumber
documentNumber
Số HC/CMND
CCCD
CMND
Passport No
```

trong:

```text
- FE type
- FE schema
- FE form state
- FE payload
- Excel template
- Excel import
- Backend DTO
- Backend validator
```

Search:

```bash
grep -R "passportId\|identityNumber\|citizenId\|idNumber\|documentNumber\|Số HC/CMND\|CCCD\|CMND" frontend/pems-react/src backend/PEMS.Application
```

---

## 10. Fix lỗi TypeScript ở ExcelUpload component mồ côi

### Hiện trạng

`npx tsc --noEmit` còn lỗi ở:

```text
frontend/pems-react/src/components/ExcelUpload/ExcelUpload.tsx
```

Lỗi do import:

```text
validateExcelFile
```

không tồn tại.

Report trước ghi file này là component mồ côi, không có nơi import.

### Cách xử lý

Search references:

```bash
grep -R "ExcelUpload" frontend/pems-react/src
grep -R "components/ExcelUpload/ExcelUpload" frontend/pems-react/src
```

Nếu không có reference thật:

```text
- Xóa ExcelUpload.tsx.
- Xóa folder nếu rỗng.
```

Nếu có reference thật:

```text
- Sửa import validateExcelFile sang module đúng.
- Hoặc update component theo ExcelUpload flow hiện tại.
```

Không sửa template mới đã bỏ passport/CMND.

---

## 11. File cần kiểm tra/sửa

### Frontend

```text
frontend/pems-react/src/features/visit-requests/**
frontend/pems-react/src/features/public/**
frontend/pems-react/src/pages/**
frontend/pems-react/src/shared/FormField.tsx
frontend/pems-react/src/**/RegisterInfoSection.tsx
frontend/pems-react/src/**/ContactInfoSection.tsx
frontend/pems-react/src/**/VisitInfoSection.tsx
frontend/pems-react/src/**/VisitorListSection.tsx
frontend/pems-react/src/**/visitRequest.types.ts
frontend/pems-react/src/**/visitRequest.schema.ts
frontend/pems-react/src/**/useVisitRequestForm.ts
frontend/pems-react/src/**/visitRequestApi.ts
frontend/pems-react/src/components/ExcelUpload/ExcelUpload.tsx
```

### Backend

```text
backend/PEMS.Application/Common/DTOs/VisitFormDtos.cs
backend/PEMS.Application/Delegations/Commands/IVisitRequestFormCommand.cs
backend/PEMS.Application/Delegations/Commands/VisitRequestFormValidationRules.cs
backend/PEMS.Application/Delegations/Commands/InitiateVisitRequest/**
backend/PEMS.Application/Delegations/Commands/VerifyAndCreateVisitRequest/**
backend/PEMS.Infrastructure/Services/VisitRequestService.cs
```

---

## 12. Manual test checklist

### 12.1 OTP / registrant

```text
[ ] Email người đăng ký form có note OTP.
[ ] OTP được gửi tới registrantEmail.
[ ] Verify OTP dùng registrantEmail.
[ ] Nếu registrantEmail khác contactEmail, OTP vẫn gửi tới registrantEmail.
```

### 12.2 Contact / visitor account

```text
[ ] Section "Thông tin đầu mối liên hệ" có note tạo tài khoản VISITOR.
[ ] Email đầu mối liên hệ có note đăng nhập Google lần sau.
[ ] Tick "Tôi cũng là đầu mối liên hệ" → contact fields auto-fill từ registrant.
[ ] Sửa registrant khi checkbox còn tick → contact fields sync theo.
[ ] Untick → cho chỉnh contact riêng.
[ ] Payload có contact fields.
[ ] Backend tạo/link VISITOR bằng contactEmail.
[ ] visit_requests.visitor_user_id trỏ tới user có email = contactEmail.
[ ] contact_person_json lưu contactEmail.
[ ] registrant_email vẫn lưu email nhận OTP.
```

### 12.3 Multi-campus

```text
[ ] SINGLE_CAMPUS + 1 campus → pass.
[ ] SINGLE_CAMPUS + 0 campus → validation error.
[ ] MULTI_CAMPUS + 1 campus → báo lỗi, không tự đổi scope.
[ ] MULTI_CAMPUS + 2 campus → pass.
```

### 12.4 Guest list

```text
[ ] Guest email vẫn required.
[ ] Không còn Số HC/CMND/passportId.
```

### 12.5 TypeScript

```text
[ ] npm run build pass.
[ ] npx tsc --noEmit pass.
```

---

## 13. Commands cần chạy

Frontend:

```bash
cd frontend/pems-react
npm run build
npx tsc --noEmit
```

Backend nếu có sửa DTO/handler/service:

```bash
dotnet build backend/PEMS.Api/PEMS.Api.csproj -p:BaseOutputPath=./.tmp-build/
```

Nếu có test:

```bash
dotnet test tests/PEMS.ArchitectureTests/PEMS.ArchitectureTests.csproj -p:BaseOutputPath=./.tmp-build/
```

---

## 14. Output report bắt buộc

Sau khi sửa xong, trả report:

```md
# UC-17 Contact Person / Visitor Account Final Fix Report

## Summary
- Registrant email is used only for OTP verification.
- Contact person information is used to create/link VISITOR account.
- "Tôi cũng là đầu mối liên hệ" auto-fills contact fields from registrant fields.
- Multi-campus requires at least 2 campuses and does not auto-downgrade.
- Guest email remains required.
- ExcelUpload TypeScript issue fixed.
- No identity/passport fields restored.

## Files Changed
### Frontend
- ...

### Backend
- ...

## Data Mapping Verified
- registrant_email = OTP email.
- contact_person_json.email = contact email.
- visitor_user_id = user created/linked by contact email.

## Commands Run
```bash
npm run build
npx tsc --noEmit
dotnet build ...
```

## Manual Tests
- ...

## Remaining Notes
- ...
```

---

## 15. Definition of Done

```text
[ ] OTP gửi tới email người đăng ký form.
[ ] Tài khoản VISITOR tạo/link theo email đầu mối liên hệ.
[ ] Nếu tick "Tôi cũng là đầu mối liên hệ", thông tin đăng ký form auto-fill sang đầu mối liên hệ.
[ ] Dù auto-fill, code vẫn tạo/link user từ contact fields.
[ ] visit_requests.registrant_email lưu email nhận OTP.
[ ] visit_requests.contact_person_json lưu thông tin đầu mối liên hệ.
[ ] visit_requests.visitor_user_id trỏ tới user của contactEmail.
[ ] Liên cơ sở chọn 1 campus bị chặn bằng message rõ ràng.
[ ] Không tự động đổi về Một cơ sở.
[ ] Guest email vẫn required.
[ ] Không còn CCCD/CMND/passportId.
[ ] ExcelUpload tsc error được xử lý.
[ ] npm run build pass.
[ ] npx tsc --noEmit pass.
[ ] Backend build pass nếu có sửa backend.
```

---

## 16. Kết luận

Luồng đúng sau khi fix:

```text
Người đăng ký form nhập email
→ nhận OTP tại registrantEmail
→ xác thực OTP
→ form được submit
→ hệ thống lấy Thông tin đầu mối liên hệ
→ tạo/link VISITOR account bằng contactEmail
→ lưu visitor_user_id theo contact user
→ lưu registrant_* riêng để biết ai đã đăng ký/gửi form
→ lưu contact_person_json riêng để biết ai là đầu mối liên hệ
```

Không nhầm email nhận OTP với email tạo tài khoản, trừ khi user chọn "Tôi cũng là đầu mối liên hệ" khiến hai nhóm thông tin giống nhau.
