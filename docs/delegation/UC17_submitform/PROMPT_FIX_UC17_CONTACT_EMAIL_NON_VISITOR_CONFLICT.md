# PROMPT_FIX_UC17_CONTACT_EMAIL_NON_VISITOR_CONFLICT

## Mục tiêu

Cập nhật code UC-17 Public Visit Request để xử lý đúng case:

```text
Người dùng nhập email ở phần "Thông tin đầu mối liên hệ".
Email này đã tồn tại trong bảng users.
Nhưng user tương ứng KHÔNG có role VISITOR.
```

Khi gặp case này, hệ thống phải **chặn submit**, không tạo tài khoản VISITOR mới, không đổi role tài khoản hiện có, không link `visit_requests.visitor_user_id` vào tài khoản nội bộ, và trả lỗi chuẩn:

```json
{
  "success": false,
  "errorCode": "CONTACT_EMAIL_CANNOT_BE_USED_FOR_VISITOR_ACCOUNT",
  "message": "Email đầu mối liên hệ không thể dùng để tạo tài khoản VISITOR. Vui lòng nhập email khác hoặc liên hệ FPTU để được hỗ trợ."
}
```

---

## 1. Bối cảnh nghiệp vụ

Trong UC-17 Submit Visit Request, hệ thống có 2 nhóm email khác nhau:

```text
1. registrantEmail
   - Email của người đăng ký form.
   - Dùng để gửi OTP và verify OTP.
   - Không mặc định dùng để tạo tài khoản VISITOR.

2. contactEmail
   - Email của đầu mối liên hệ.
   - Dùng để tạo/link tài khoản VISITOR.
   - visit_requests.visitor_user_id phải trỏ tới user VISITOR được tạo/link từ contactEmail.
```

Nếu `contactEmail` đã tồn tại nhưng thuộc tài khoản nội bộ như `ADMIN`, `HO`, `STAFF`, `DEPT`, `STUDENT`, hệ thống không được tự chuyển role hoặc dùng tài khoản đó làm VISITOR.

---

## 2. Quyết định nghiệp vụ bắt buộc

### 2.1. Rule xử lý contactEmail

Backend phải xử lý theo thứ tự:

```text
1. Normalize contactEmail = trim + lowercase.
2. Tìm user theo contactEmail trong bảng users.
3. Nếu chưa tồn tại:
   → tạo mới user role VISITOR.
4. Nếu đã tồn tại và role_code = VISITOR:
   → nếu status ACTIVE thì link user này vào visit_requests.visitor_user_id.
   → nếu status không ACTIVE thì reject bằng VISITOR_ACCOUNT_INACTIVE.
5. Nếu đã tồn tại nhưng role_code != VISITOR:
   → reject bằng CONTACT_EMAIL_CANNOT_BE_USED_FOR_VISITOR_ACCOUNT.
```

### 2.2. Những việc KHÔNG được làm

```text
- Không tạo thêm tài khoản VISITOR bằng cùng email nếu email đã tồn tại.
- Không tự động đổi role tài khoản hiện có sang VISITOR.
- Không link visit_requests.visitor_user_id tới tài khoản nội bộ.
- Không cho submit thành công nếu contactEmail thuộc non-VISITOR account.
- Không chỉ check ở frontend.
- Không sửa SQL để thêm cột mới.
```

---

## 3. Scope cần sửa

### Backend bắt buộc

```text
- Check ở bước POST /api/visit-requests/initiate để báo lỗi sớm trước khi gửi OTP.
- Check lại ở bước POST /api/visit-requests/verify trước khi tạo visit_requests.
- Trả errorCode/message chuẩn qua ExceptionHandlingMiddleware.
- Đảm bảo logic nằm trong Application/Service/Handler, không viết business logic trong Controller.
```

### Frontend nên sửa

```text
- Khi backend trả CONTACT_EMAIL_CANNOT_BE_USED_FOR_VISITOR_ACCOUNT, UI phải hiển thị message cụ thể.
- Ưu tiên hiển thị lỗi tại field Email đầu mối liên hệ hoặc submit error ở section Contact.
- Không hiển thị generic message nếu backend đã trả message.
```

---

## 4. Files cần kiểm tra/sửa

### Backend

```text
backend/PEMS.Domain/Constants/VisitRequestConstants.cs
backend/PEMS.Application/Common/Exceptions/ConflictException.cs
backend/PEMS.Application/Common/Exceptions/BusinessRuleException.cs
backend/PEMS.Api/Middleware/ExceptionHandlingMiddleware.cs
backend/PEMS.Application/Delegations/Commands/InitiateVisitRequest/**
backend/PEMS.Application/Delegations/Commands/VerifyAndCreateVisitRequest/**
backend/PEMS.Infrastructure/Services/VisitRequestService.cs
backend/PEMS.Application/Common/Interfaces/IVisitRequestService.cs
backend/PEMS.Application/Common/Interfaces/IUserRepository.cs
```

### Frontend

```text
frontend/pems-react/src/features/visit-request/api/visitRequestApi.ts
frontend/pems-react/src/features/visit-request/hooks/useVisitRequestForm.ts
frontend/pems-react/src/features/visit-request/components/sections/ContactSection.tsx
frontend/pems-react/src/pages/**/VisitingFormPopup.tsx
frontend/pems-react/src/shared/**/getApiErrorMessage.ts
```

Tên file thực tế có thể khác. Trước khi sửa, hãy search:

```bash
grep -R "CreateOrLinkVisitor\|VisitorUser\|contactEmail\|ContactEmail\|CONTACT_EMAIL" backend/PEMS.Application backend/PEMS.Infrastructure

grep -R "Có lỗi xảy ra khi gửi đơn\|getApiErrorMessage\|submitError\|contactEmail" frontend/pems-react/src
```

---

## 5. Backend implementation detail

### 5.1. Thêm error code constant

Trong `VisitRequestConstants.cs` hoặc nơi đang chứa error code UC-17, thêm:

```csharp
public const string ContactEmailCannotBeUsedForVisitorAccount =
    "CONTACT_EMAIL_CANNOT_BE_USED_FOR_VISITOR_ACCOUNT";

public const string VisitorAccountInactive =
    "VISITOR_ACCOUNT_INACTIVE";
```

Nếu project đang dùng enum/static class khác thì thêm theo convention hiện có.

---

### 5.2. Logic create/link VISITOR user

Tìm method đang tạo/link visitor user, ví dụ:

```text
CreateOrLinkVisitorUserAsync
EnsureVisitorUserAsync
GetOrCreateVisitorAsync
CreateVisitorUserAsync
```

Cập nhật rule như sau:

```csharp
private async Task<User> CreateOrLinkVisitorUserAsync(
    string contactEmail,
    string contactFullName,
    string? contactPhone,
    string? contactOrganization,
    CancellationToken cancellationToken)
{
    var normalizedEmail = contactEmail.Trim().ToLowerInvariant();

    var existingUser = await _db.Users
        .FirstOrDefaultAsync(x => x.Email == normalizedEmail, cancellationToken);

    if (existingUser is null)
    {
        var visitorUser = User.CreateVisitor(
            email: normalizedEmail,
            fullName: contactFullName,
            phone: contactPhone,
            organization: contactOrganization,
            createdVia: "VISIT_REQUEST"
        );

        _db.Users.Add(visitorUser);
        return visitorUser;
    }

    if (!string.Equals(existingUser.RoleCode, "VISITOR", StringComparison.OrdinalIgnoreCase))
    {
        throw new ConflictException(
            "Email đầu mối liên hệ không thể dùng để tạo tài khoản VISITOR. Vui lòng nhập email khác hoặc liên hệ FPTU để được hỗ trợ.",
            errorCode: VisitRequestErrorCodes.ContactEmailCannotBeUsedForVisitorAccount
        );
    }

    if (!string.Equals(existingUser.Status, "ACTIVE", StringComparison.OrdinalIgnoreCase))
    {
        throw new BusinessRuleException(
            "Tài khoản VISITOR tương ứng với email này hiện không hoạt động. Vui lòng nhập email khác hoặc liên hệ FPTU để được hỗ trợ.",
            errorCode: VisitRequestErrorCodes.VisitorAccountInactive
        );
    }

    return existingUser;
}
```

> Nếu entity hiện tại không có `RoleCode` trực tiếp mà dùng navigation `Role`, `RoleId`, hoặc enum, hãy map đúng theo schema/code hiện tại. Không hard-code sai property.

---

### 5.3. Check sớm ở initiate

Ở `InitiateVisitRequestCommandHandler` hoặc service validation được gọi bởi initiate, thêm rule:

```text
Nếu contactEmail đã tồn tại trong users và role_code != VISITOR
→ throw CONTACT_EMAIL_CANNOT_BE_USED_FOR_VISITOR_ACCOUNT.
```

Mục đích:

```text
- Không gửi OTP xong mới báo lỗi.
- User biết cần đổi email đầu mối liên hệ ngay từ đầu.
```

Pseudo:

```csharp
await _visitRequestService.ValidateContactEmailCanBeUsedForVisitorAsync(
    request.ContactEmail,
    cancellationToken);
```

---

### 5.4. Check lại ở verify

Ở `VerifyAndCreateVisitRequestCommandHandler`, trước khi insert `visit_requests`, vẫn phải gọi lại rule này hoặc dùng chính `CreateOrLinkVisitorUserAsync` đã có check.

Mục đích:

```text
- Chống bypass frontend.
- Chống race condition nếu email vừa được tạo thành internal account sau bước initiate.
```

---

### 5.5. Không được nuốt errorCode trong middleware

Đảm bảo `ExceptionHandlingMiddleware` trả được response dạng:

```json
{
  "success": false,
  "errorCode": "CONTACT_EMAIL_CANNOT_BE_USED_FOR_VISITOR_ACCOUNT",
  "message": "Email đầu mối liên hệ không thể dùng để tạo tài khoản VISITOR. Vui lòng nhập email khác hoặc liên hệ FPTU để được hỗ trợ."
}
```

Nếu `ConflictException` chưa hỗ trợ `ErrorCode`, cập nhật class:

```csharp
public sealed class ConflictException : Exception
{
    public string? ErrorCode { get; }

    public ConflictException(string message, string? errorCode = null)
        : base(message)
    {
        ErrorCode = errorCode;
    }
}
```

Nếu đã hỗ trợ rồi thì không sửa lại.

---

## 6. Frontend implementation detail

### 6.1. Không hiển thị generic error

Nếu code đang catch như sau:

```ts
catch {
  setSubmitError("Có lỗi xảy ra khi gửi đơn. Vui lòng thử lại.");
}
```

thì sửa để lấy message từ backend.

Helper đề xuất:

```ts
import axios from "axios";

export function getApiErrorMessage(error: unknown): string {
  if (axios.isAxiosError(error)) {
    const data = error.response?.data as any;

    if (typeof data?.message === "string" && data.message.trim()) {
      return data.message;
    }

    if (data?.errors) {
      const values = Object.values(data.errors).flat();
      const first = values[0];
      if (typeof first === "string" && first.trim()) return first;
    }

    if (typeof data?.errorCode === "string" && data.errorCode.trim()) {
      return data.errorCode;
    }
  }

  return "Có lỗi xảy ra khi gửi đơn. Vui lòng thử lại.";
}
```

### 6.2. Map lỗi về contactEmail nếu có thể

Nếu form dùng React Hook Form, khi backend trả code này thì set lỗi field:

```ts
if (errorCode === "CONTACT_EMAIL_CANNOT_BE_USED_FOR_VISITOR_ACCOUNT") {
  form.setError("contactPoint.email", {
    type: "server",
    message:
      "Email đầu mối liên hệ không thể dùng để tạo tài khoản VISITOR. Vui lòng nhập email khác hoặc liên hệ FPTU để được hỗ trợ.",
  });
}
```

Nếu field name thực tế là `contactEmail`, dùng:

```ts
form.setError("contactEmail", {
  type: "server",
  message,
});
```

Không đoán field name. Kiểm tra schema/type hiện tại trước khi sửa.

---

## 7. Test cases bắt buộc

### 7.1. Backend API test

#### Case A — contactEmail chưa tồn tại

```text
Input: contactEmail = new.visitor@example.com
Expected:
- Submit pass nếu form hợp lệ.
- Tạo user role VISITOR.
- visit_requests.visitor_user_id trỏ tới user mới.
```

#### Case B — contactEmail đã tồn tại role VISITOR ACTIVE

```text
Input: contactEmail = existing.visitor@example.com
Expected:
- Submit pass nếu form hợp lệ.
- Không tạo user mới.
- visit_requests.visitor_user_id trỏ tới existing VISITOR user.
```

#### Case C — contactEmail đã tồn tại role STAFF / HO / ADMIN / DEPT / STUDENT

```text
Input: contactEmail = staff.hn@fpt.edu.vn hoặc email nội bộ bất kỳ
Expected:
- API trả 409 Conflict.
- errorCode = CONTACT_EMAIL_CANNOT_BE_USED_FOR_VISITOR_ACCOUNT.
- message đúng tiếng Việt.
- Không tạo visit_requests.
- Không tạo visit_request_campuses.
- Không tạo user VISITOR mới.
- Không đổi role user cũ.
```

#### Case D — contactEmail đã tồn tại VISITOR nhưng status không ACTIVE

```text
Expected:
- API reject.
- errorCode = VISITOR_ACCOUNT_INACTIVE.
- Không tạo visit_requests.
```

---

### 7.2. Database verification SQL

Sau test case C, chạy:

```sql
SELECT user_id, email, role_code, status
FROM users
WHERE email = 'staff.hn@fpt.edu.vn';
```

Kỳ vọng:

```text
role_code vẫn là STAFF.
status không bị đổi.
Không có duplicate user cùng email.
```

Kiểm tra không tạo request:

```sql
SELECT visit_request_id, registrant_email, visitor_user_id, created_at
FROM visit_requests
ORDER BY visit_request_id DESC
LIMIT 5;
```

Kỳ vọng:

```text
Không có request mới dùng visitor_user_id của tài khoản nội bộ bị nhập vào contactEmail.
```

---

### 7.3. Frontend manual test

```text
1. Mở public form đăng ký tham quan.
2. Nhập registrantEmail hợp lệ để nhận OTP.
3. Ở phần Thông tin đầu mối liên hệ, nhập email đang tồn tại trong hệ thống với role STAFF/HO/ADMIN.
4. Bấm gửi form/initiate.
5. UI phải hiển thị message cụ thể:
   "Email đầu mối liên hệ không thể dùng để tạo tài khoản VISITOR. Vui lòng nhập email khác hoặc liên hệ FPTU để được hỗ trợ."
6. Không hiển thị generic message.
7. Đổi sang email mới chưa tồn tại hoặc email VISITOR ACTIVE → flow chạy bình thường.
```

---

## 8. Commands cần chạy

Backend:

```bash
dotnet build backend/PEMS.Api/PEMS.Api.csproj -p:BaseOutputPath=./.tmp-build/
```

Nếu có test project:

```bash
dotnet test tests/PEMS.ArchitectureTests/PEMS.ArchitectureTests.csproj -p:BaseOutputPath=./.tmp-build/
```

Frontend nếu sửa UI/error handling:

```bash
cd frontend/pems-react
npm run build
npx tsc --noEmit
```

---

## 9. Output report sau khi sửa

Sau khi update code, trả report theo format:

```md
# UC-17 Contact Email Non-Visitor Conflict Fix Report

## Summary
- Added backend validation for contactEmail existing as non-VISITOR user.
- Backend now rejects contactEmail that belongs to ADMIN/HO/STAFF/DEPT/STUDENT.
- Backend does not create duplicate VISITOR user and does not change existing user role.
- Frontend now displays backend message instead of generic submit error.

## Business Rule Implemented
- contactEmail is used to create/link VISITOR account only if:
  - no user exists with that email, or
  - existing user has role VISITOR and ACTIVE status.
- If existing user role != VISITOR, return:
  - errorCode: CONTACT_EMAIL_CANNOT_BE_USED_FOR_VISITOR_ACCOUNT
  - message: Email đầu mối liên hệ không thể dùng để tạo tài khoản VISITOR. Vui lòng nhập email khác hoặc liên hệ FPTU để được hỗ trợ.

## Files Changed
### Backend
- ...

### Frontend
- ...

## API Response Verified
```json
{
  "success": false,
  "errorCode": "CONTACT_EMAIL_CANNOT_BE_USED_FOR_VISITOR_ACCOUNT",
  "message": "Email đầu mối liên hệ không thể dùng để tạo tài khoản VISITOR. Vui lòng nhập email khác hoặc liên hệ FPTU để được hỗ trợ."
}
```

## Tests
- [ ] contactEmail new → creates VISITOR.
- [ ] contactEmail existing VISITOR ACTIVE → links existing user.
- [ ] contactEmail existing STAFF/HO/ADMIN/DEPT/STUDENT → rejects.
- [ ] contactEmail existing VISITOR inactive → rejects.
- [ ] No duplicate user created.
- [ ] Existing internal role not changed.
- [ ] No visit_request created on rejected case.

## Commands Run
```bash
dotnet build backend/PEMS.Api/PEMS.Api.csproj -p:BaseOutputPath=./.tmp-build/
npm run build
npx tsc --noEmit
```

## Remaining Notes
- ...
```

---

## 10. Definition of Done

```text
[ ] Backend normalize contactEmail trước khi lookup.
[ ] Nếu contactEmail chưa tồn tại → tạo VISITOR user.
[ ] Nếu contactEmail tồn tại role VISITOR ACTIVE → link user đó.
[ ] Nếu contactEmail tồn tại role != VISITOR → reject với CONTACT_EMAIL_CANNOT_BE_USED_FOR_VISITOR_ACCOUNT.
[ ] Nếu contactEmail tồn tại VISITOR nhưng inactive/locked → reject với VISITOR_ACCOUNT_INACTIVE.
[ ] Không tạo duplicate user cùng email.
[ ] Không đổi role user nội bộ.
[ ] Không link visitor_user_id tới tài khoản non-VISITOR.
[ ] Không tạo visit_requests khi bị reject.
[ ] API response có success=false, errorCode, message đúng.
[ ] Frontend hiển thị message cụ thể từ backend.
[ ] Backend build pass.
[ ] Frontend build/typecheck pass nếu có sửa frontend.
```

---

## 11. Kết luận

Luồng đúng sau khi fix:

```text
Visitor submit public form
→ OTP vẫn verify bằng registrantEmail
→ Backend lấy contactEmail để tạo/link VISITOR account
→ Nếu contactEmail chưa tồn tại: tạo VISITOR
→ Nếu contactEmail là VISITOR ACTIVE: link user đó
→ Nếu contactEmail thuộc role khác VISITOR: reject
→ Không thay đổi role, không tạo duplicate, không link sai tài khoản nội bộ
```
