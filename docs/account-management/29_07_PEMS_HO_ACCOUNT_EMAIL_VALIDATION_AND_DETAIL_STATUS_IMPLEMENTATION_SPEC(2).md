# PEMS — IMPLEMENTATION SPEC  
## Cập nhật validation email và hiển thị trạng thái trong trang Quản lý tài khoản của HO

> **Mục đích tài liệu:**  
> Tài liệu này là đặc tả triển khai đầy đủ để một AI Agent hoặc developer khác có thể đọc và thực hiện thay đổi trực tiếp trên codebase PEMS mà không cần đọc lại toàn bộ hội thoại trước.
>
> **Repository:** `quangthoai04/PEMS`  
> **Nhánh làm việc bắt buộc:** nhánh hiện tại `Duy-Iter1`  
> **Nguồn code chuẩn để triển khai:** HEAD hiện tại của nhánh `Duy-Iter1` tại thời điểm Agent bắt đầu. Không chuyển sang `Dev` hoặc nhánh khác.  
> **Lưu ý:** commit `c39e6f0404978a5a05b0c52681e01c8837fc4b29` chỉ là mốc đối chiếu lịch sử khi lập bản mô tả ban đầu, không được dùng để checkout hoặc coi là HEAD triển khai.
>
> **Phạm vi actor:** Trang **Quản lý tài khoản do role HO quản lý**.

---

# 1. Mục tiêu thay đổi

Thực hiện đúng hai nhóm yêu cầu sau.

## 1.1. Thay đổi quy tắc email

Tại ba luồng sau:

1. Modal **Khởi tạo tài khoản mới**.
2. Modal **Chỉnh sửa thông tin tài khoản**.
3. Modal **Thay thế Staff Leader / Trưởng phòng IC**, tại tab **Tạo tài khoản mới**.

Email đăng nhập chỉ được phép sử dụng một trong hai domain:

```text
@gmail.com
@fpt.edu.vn
```

Không còn chấp nhận:

```text
@fe.edu.vn
```

Thông báo hiển thị thống nhất:

```text
Chỉ chấp nhận @gmail.com và @fpt.edu.vn.
```

## 1.2. Hiển thị trạng thái trong modal chi tiết tài khoản

Khi HO bấm nút **Xem tài khoản / View detail account**, modal chi tiết phải hiển thị thêm:

```text
Trạng thái tài khoản
```

Ví dụ:

```text
Hoạt động
Vô hiệu hóa
Bị khóa
Chờ xác nhận email
```

Trạng thái phải lấy từ response chi tiết tài khoản của backend, không chỉ lấy từ dữ liệu dòng trong bảng danh sách.

---

# 2. Phạm vi và nguyên tắc bắt buộc

## 2.1. Phải sửa đồng bộ cả frontend và backend

Không được chỉ sửa helper text hoặc regex phía frontend.

Backend phải tiếp tục là nguồn kiểm tra cuối cùng để ngăn người dùng:

- gọi API trực tiếp;
- sửa request bằng DevTools/Postman;
- bỏ qua validation trên trình duyệt.

Chuỗi bắt buộc:

```text
Frontend helper text
→ Frontend shared validation
→ Frontend submit gate
→ Backend authoritative validation
→ Handler business flow
→ Unit test
→ Integration test
→ Build
```

## 2.2. Không thay đổi database nếu không cần thiết

Yêu cầu này không cần:

- thêm cột;
- đổi schema;
- tạo migration;
- đổi bảng `users`;
- đổi enum status.

Backend hiện đã trả trường `status` trong API chi tiết tài khoản.

## 2.3. Không sửa ngoài phạm vi

Không được tự ý thay đổi:

- role/sub-role;
- quyền của HO;
- quy trình tạo tài khoản chờ xác nhận email;
- quy trình đổi Staff Leader;
- quy trình thu hồi session;
- logic gửi email;
- API route;
- trạng thái tài khoản;
- thiết kế database;
- nghiệp vụ quản lý campus/department.

## 2.4. Không dùng validation chỉ dựa trên suffix

Không dùng:

```csharp
email.EndsWith("gmail.com")
email.EndsWith("fpt.edu.vn")
```

hoặc:

```ts
email.endsWith('gmail.com')
```

Phải tách chính xác domain sau ký tự `@` và so sánh exact match.

Mục tiêu là từ chối các email giả như:

```text
user@sub.gmail.com
user@fpt.edu.vn.evil.com
user@gmail.com.vn
user@fakefpt.edu.vn
```

---

# 3. Trạng thái code hiện tại đã xác nhận

## 3.1. Frontend đang có validation dùng chung

File:

```text
frontend/pems-react/src/features/account-management/validation/accountIdentityValidation.ts
```

File này đang được dùng chung cho:

- tạo tài khoản;
- chỉnh sửa thông tin tài khoản;
- thay thế Staff Leader bằng tài khoản mới.

Hiện tại allowlist vẫn gồm:

```ts
['gmail.com', 'fpt.edu.vn', 'fe.edu.vn']
```

Thông báo hiện tại vẫn ghi ba domain.

## 3.2. Backend đang có validation dùng chung

File:

```text
backend/PEMS.Application/Accounts/Common/AccountIdentityRules.cs
```

Hiện tại allowlist backend vẫn gồm:

```csharp
"gmail.com"
"fpt.edu.vn"
"fe.edu.vn"
```

Thông báo backend vẫn ghi ba domain.

## 3.3. Ba modal đã gọi shared validator

### Modal tạo tài khoản

File:

```text
frontend/pems-react/src/pages/dashboard/accounts/AccountManagement.tsx
```

Đã gọi:

```ts
validateAccountEmail(...)
```

ở:

- `onBlur`;
- bước `Tiếp tục`;
- điều kiện khóa nút xác nhận.

### Modal chỉnh sửa thông tin account

Cũng nằm trong:

```text
frontend/pems-react/src/pages/dashboard/accounts/AccountManagement.tsx
```

Đã gọi:

```ts
validateAccountEmail(...)
```

khi blur và trước khi submit.

### Modal thay thế Staff Leader

File:

```text
frontend/pems-react/src/features/account-management/components/ReplaceStaffLeaderModal.tsx
```

Đã validate email khi:

```text
mode = CREATE_NEW_USER
```

Tab chọn nhân sự có sẵn không phụ thuộc vào email input ẩn.

## 3.4. API detail đã có trường status

Backend DTO:

```text
backend/PEMS.Application/Accounts/Queries/ViewAccountDetails/ViewAccountDetailsDto.cs
```

đã có:

```csharp
public string Status { get; init; }
```

Frontend type:

```text
frontend/pems-react/src/features/account-management/types/accountManagement.types.ts
```

đã có:

```ts
status: string;
```

`openViewDrawer()` hiện đã đọc:

```ts
details.status
```

và lưu vào:

```ts
rawStatus
```

nhưng giao diện chi tiết chưa render trạng thái.

---

# 4. Quy tắc email mới

## 4.1. Domain được phép

```text
gmail.com
fpt.edu.vn
```

So sánh sau khi:

1. trim đầu/cuối;
2. chuyển thành lowercase;
3. kiểm tra định dạng;
4. tách đúng một ký tự `@`;
5. exact-match domain.

## 4.2. Domain bị từ chối

Tối thiểu phải từ chối:

```text
fe.edu.vn
edu.vn
student.fpt.edu.vn
sub.gmail.com
gmail.com.vn
fpt.edu.vn.example.com
fakefpt.edu.vn
outlook.com
yahoo.com
```

## 4.3. Các quy tắc hiện tại phải giữ nguyên

Không được làm mất các rule đang có:

```text
- Email bắt buộc.
- Email tối đa 150 ký tự.
- Local part tối đa 64 ký tự.
- Chỉ có đúng một ký tự @.
- Không chứa whitespace/control character.
- Không cho plus addressing.
- Không cho local part bắt đầu hoặc kết thúc bằng dấu chấm.
- Không cho hai dấu chấm liên tiếp.
- Không cho subdomain.
- Không tự rewrite local part.
```

Ví dụ:

```text
abc+test@gmail.com   → không hợp lệ
.abc@gmail.com       → không hợp lệ
abc.@gmail.com       → không hợp lệ
a..b@gmail.com       → không hợp lệ
abc@@gmail.com       → không hợp lệ
```

## 4.4. Thông báo duy nhất

Frontend và backend phải dùng cùng một message:

```text
Chỉ chấp nhận @gmail.com và @fpt.edu.vn.
```

Không để sót bất kỳ helper text nào còn ghi:

```text
@fe.edu.vn
```

---

# 5. Thay đổi frontend — shared validation

## 5.1. File cần sửa

```text
frontend/pems-react/src/features/account-management/validation/accountIdentityValidation.ts
```

## 5.2. Sửa allowlist

Từ:

```ts
export const ALLOWED_ACCOUNT_EMAIL_DOMAINS = [
  'gmail.com',
  'fpt.edu.vn',
  'fe.edu.vn',
] as const;
```

thành:

```ts
export const ALLOWED_ACCOUNT_EMAIL_DOMAINS = [
  'gmail.com',
  'fpt.edu.vn',
] as const;
```

## 5.3. Sửa message

Từ message ba domain thành:

```ts
emailDomainNotAllowed:
  'Chỉ chấp nhận @gmail.com và @fpt.edu.vn.',
```

## 5.4. Không sửa thuật toán nếu không cần

Giữ nguyên cơ chế:

```ts
splitEmail(...)
hasValidEmailShape(...)
hasAllowedEmailDomain(...)
normalizeAccountEmail(...)
validateAccountEmail(...)
```

Chỉ thay danh sách domain và message.

## 5.5. Quét toàn frontend

Chạy tìm kiếm:

```bash
rg -n "@fe\.edu\.vn|fe\.edu\.vn|Chỉ chấp nhận @gmail\.com" frontend/pems-react/src
```

Mọi helper text thuộc account management phải được đồng bộ.

Không xóa `@fe.edu.vn` ở tài liệu/seed/module khác nếu ngoài phạm vi, trừ khi đó là shared account validation runtime thực sự.

---

# 6. Thay đổi frontend — modal tạo tài khoản mới

## 6.1. File

```text
frontend/pems-react/src/pages/dashboard/accounts/AccountManagement.tsx
```

## 6.2. Helper text cần sửa

Từ:

```text
Chỉ chấp nhận @gmail.com, @fpt.edu.vn hoặc @fe.edu.vn.
```

thành:

```text
Chỉ chấp nhận @gmail.com và @fpt.edu.vn.
```

## 6.3. Hành vi bắt buộc

Khi nhập:

```text
new.account@fe.edu.vn
```

và blur hoặc bấm **Tiếp tục**:

```text
- Input email có trạng thái lỗi.
- Hiển thị message mới.
- Không mở modal xác nhận tạo tài khoản.
- Không tạo pending payload.
- Không gọi API.
- Không mất dữ liệu form.
```

Khi nhập:

```text
new.account@gmail.com
new.account@fpt.edu.vn
```

thì luồng hoạt động bình thường nếu các trường khác hợp lệ.

## 6.4. Không duplicate validator

Không viết thêm regex/domain check riêng trong component.

Tiếp tục dùng:

```ts
validateAccountEmail(...)
normalizeAccountEmail(...)
```

---

# 7. Thay đổi frontend — modal chỉnh sửa thông tin tài khoản

## 7.1. File

```text
frontend/pems-react/src/pages/dashboard/accounts/AccountManagement.tsx
```

## 7.2. Helper text cần sửa

Từ ba domain thành:

```text
Chỉ chấp nhận @gmail.com và @fpt.edu.vn.
```

## 7.3. Hành vi bắt buộc

Nếu HO thay email thành:

```text
staff.leader@fe.edu.vn
```

thì:

```text
- Không gọi endpoint update basic info.
- Không mở confirmation đổi email.
- Hiển thị lỗi ngay dưới input email.
- Giữ nguyên dữ liệu đang nhập.
```

Email hợp lệ:

```text
staff.leader@gmail.com
staff.leader@fpt.edu.vn
```

vẫn đi qua luồng xác nhận đổi email hiện tại.

## 7.4. Chính sách đối với dữ liệu legacy

Chốt triển khai theo hướng nghiêm ngặt:

```text
Mọi email được gửi từ modal chỉnh sửa phải thỏa allowlist mới.
```

Nếu tài khoản hiện tại đang dùng `@fe.edu.vn`, HO phải đổi sang `@gmail.com` hoặc `@fpt.edu.vn` trước khi lưu.

Agent phải chạy truy vấn kiểm tra dữ liệu legacy và báo cáo số lượng, nhưng không tự động sửa database nếu chưa được yêu cầu.

Truy vấn kiểm tra:

```sql
SELECT
    user_id,
    full_name,
    email,
    status
FROM users
WHERE LOWER(email) LIKE '%@fe.edu.vn';
```

---

# 8. Thay đổi frontend — modal thay thế Staff Leader

## 8.1. File

```text
frontend/pems-react/src/features/account-management/components/ReplaceStaffLeaderModal.tsx
```

## 8.2. Chỉ áp dụng ở tab tạo tài khoản mới

Áp dụng khi:

```text
mode = CREATE_NEW_USER
```

Không áp dụng vào:

```text
mode = EXISTING_USER
```

## 8.3. Helper text cần sửa

Từ:

```text
Chỉ chấp nhận @gmail.com, @fpt.edu.vn hoặc @fe.edu.vn.
```

thành:

```text
Chỉ chấp nhận @gmail.com và @fpt.edu.vn.
```

## 8.4. Hành vi bắt buộc

Với `CREATE_NEW_USER`:

```text
new.leader@fe.edu.vn
```

phải:

```text
- Hiển thị lỗi dưới input email.
- Không mở confirm dialog.
- Không gọi API replace Staff Leader.
```

Với `EXISTING_USER`:

```text
- Không validate trường email ẩn.
- Không bị lỗi email làm chặn tab chọn nhân sự có sẵn.
- Vẫn chỉ yêu cầu candidate + reason như hiện tại.
```

---

# 9. Thay đổi backend — validation source of truth

## 9.1. File

```text
backend/PEMS.Application/Accounts/Common/AccountIdentityRules.cs
```

## 9.2. Sửa allowlist

Từ:

```csharp
public static readonly IReadOnlySet<string> AllowedEmailDomains =
    new HashSet<string>(StringComparer.Ordinal)
    {
        "gmail.com",
        "fpt.edu.vn",
        "fe.edu.vn"
    };
```

thành:

```csharp
public static readonly IReadOnlySet<string> AllowedEmailDomains =
    new HashSet<string>(StringComparer.Ordinal)
    {
        "gmail.com",
        "fpt.edu.vn"
    };
```

## 9.3. Sửa message

```csharp
public const string EmailDomainNotAllowedMessage =
    "Chỉ chấp nhận @gmail.com và @fpt.edu.vn.";
```

## 9.4. Giữ nguyên authoritative validation

Không làm yếu các hàm:

```csharp
NormalizeEmail(...)
GetLocalPart(...)
GetDomain(...)
HasAllowedEmailDomain(...)
HasValidLocalPart(...)
ContainsPlusAddressing(...)
HasValidEmailShape(...)
ValidateEmail(...)
```

---

# 10. Rà soát ba backend write path

## 10.1. Create Account

Rà soát:

```text
backend/PEMS.Application/Accounts/Commands/CreateAccount/
```

Đảm bảo handler/validator gọi shared rule mới trước khi:

```text
- tạo user;
- tạo account confirmation;
- commit dữ liệu;
- gửi email;
- ghi audit thành công.
```

Direct API với:

```json
{
  "email": "new.account@fe.edu.vn"
}
```

phải bị từ chối.

## 10.2. Update Basic Account Info

Rà soát:

```text
backend/PEMS.Application/Accounts/Commands/UpdateBasicAccountInfo/
```

Đảm bảo email mới được:

```text
normalize
→ validate shape
→ validate exact domain
→ validate uniqueness
```

trước khi:

```text
- update users.email;
- revoke session;
- gửi thông báo đổi email;
- commit.
```

## 10.3. Replace Staff Leader

Rà soát:

```text
backend/PEMS.Application/Accounts/Commands/ReplaceStaffLeader/
```

Trong mode:

```text
CREATE_NEW_USER
```

phải gọi shared validation.

Trong mode:

```text
EXISTING_USER
```

không được yêu cầu fullName/email của tài khoản mới.

Request email `@fe.edu.vn` phải bị từ chối trước khi:

```text
- tạo tài khoản mới;
- demote leader cũ;
- promote leader mới;
- revoke session;
- gửi email;
- commit transaction.
```

---

# 11. Hiển thị trạng thái trong modal chi tiết tài khoản

## 11.1. Không cần sửa API contract

Các file sau đã có status:

```text
backend/PEMS.Application/Accounts/Queries/ViewAccountDetails/ViewAccountDetailsDto.cs

frontend/pems-react/src/features/account-management/types/accountManagement.types.ts
```

Không tạo field mới.

## 11.2. Mapping khi mở detail

File:

```text
frontend/pems-react/src/pages/dashboard/accounts/AccountManagement.tsx
```

Trong:

```ts
openViewDrawer(...)
```

tiếp tục dùng:

```ts
details.status
```

Lưu status detail thành nguồn ưu tiên:

```ts
rawStatus: details.status
```

Có thể bổ sung:

```ts
status: mapAccountStatusForList(details.status)
```

nếu component cần, nhưng không bắt buộc nếu badge detail dùng `rawStatus`.

Không chỉ dựa vào `acc.status` của row list.

## 11.3. Tạo status metadata dùng chung

Nên tạo helper hoặc constant trong file/component phù hợp.

Ví dụ:

```ts
const ACCOUNT_STATUS_META: Record<
  string,
  { label: string; className: string }
> = {
  ACTIVE: {
    label: 'Hoạt động',
    className: 'bg-emerald-50 text-emerald-700 border-emerald-200',
  },
  INACTIVE: {
    label: 'Vô hiệu hóa',
    className: 'bg-amber-50 text-amber-700 border-amber-200',
  },
  LOCKED: {
    label: 'Bị khóa',
    className: 'bg-red-50 text-red-700 border-red-200',
  },
  PENDING_EMAIL_CONFIRMATION: {
    label: 'Chờ xác nhận email',
    className: 'bg-sky-50 text-sky-700 border-sky-200',
  },
};
```

Fallback:

```ts
const normalizedStatus = String(
  data.rawStatus ?? data.status ?? ''
).trim().toUpperCase();

const statusMeta =
  ACCOUNT_STATUS_META[normalizedStatus] ?? {
    label: normalizedStatus || 'Không xác định',
    className: 'bg-slate-50 text-slate-700 border-slate-200',
  };
```

## 11.4. Vị trí hiển thị

Trong modal **Thông tin chi tiết**, thêm trường:

```text
TRẠNG THÁI TÀI KHOẢN
```

Vị trí khuyến nghị:

```text
Họ và tên
Email
Giới tính
Số điện thoại
Vai trò
Trạng thái tài khoản
Cơ sở trực thuộc
Chức vụ
Phòng ban
```

## 11.5. Render badge read-only

Không dùng input/select để hiển thị status.

Ví dụ:

```tsx
<div className="flex flex-col min-w-0">
  <span className="block text-[10px] font-bold uppercase tracking-wider mb-1 text-gray-500">
    Trạng thái tài khoản
  </span>

  <div className="min-h-[42px] flex items-center rounded-lg border border-gray-100 bg-gray-50/50 px-3">
    <span
      className={`inline-flex items-center rounded-full border px-3 py-1 text-xs font-bold ${statusMeta.className}`}
    >
      {statusMeta.label}
    </span>
  </div>
</div>
```

## 11.6. Chế độ edit

Giữ trạng thái hiển thị read-only cả khi modal chuyển sang:

```text
Chỉnh sửa thông tin tài khoản
```

Không cho sửa status trong modal này.

Luồng thay đổi status vẫn sử dụng action riêng hiện có ngoài bảng.

## 11.7. Status tối thiểu cần hỗ trợ

```text
ACTIVE
INACTIVE
LOCKED
PENDING_EMAIL_CONFIRMATION
```

Nếu có status lạ:

```text
- Không crash.
- Không render trống.
- Hiển thị raw value hoặc "Không xác định".
```

---

# 12. Danh sách file dự kiến thay đổi

## 12.1. Production code bắt buộc

```text
frontend/pems-react/src/features/account-management/validation/accountIdentityValidation.ts

frontend/pems-react/src/pages/dashboard/accounts/AccountManagement.tsx

frontend/pems-react/src/features/account-management/components/ReplaceStaffLeaderModal.tsx

backend/PEMS.Application/Accounts/Common/AccountIdentityRules.cs
```

## 12.2. Handler cần audit, có thể không cần sửa nếu đã dùng shared rules đúng

```text
backend/PEMS.Application/Accounts/Commands/CreateAccount/CreateAccountCommandHandler.cs

backend/PEMS.Application/Accounts/Commands/UpdateBasicAccountInfo/UpdateBasicAccountInfoCommandHandler.cs

backend/PEMS.Application/Accounts/Commands/ReplaceStaffLeader/ReplaceStaffLeaderCommandHandler.cs
```

## 12.3. Test files dự kiến sửa/thêm

```text
tests/PEMS.UnitTests/Accounts/Common/AccountIdentityRulesTests.cs

tests/PEMS.UnitTests/Accounts/CreateAccount/CreateAccountIdentityTests.cs
```

Tìm và cập nhật test cho:

```text
UpdateBasicAccountInfo
ReplaceStaffLeader
ViewAccountDetails / AccountManagement frontend
```

## 12.4. File không cần sửa nếu không phát hiện drift

```text
backend/PEMS.Application/Accounts/Queries/ViewAccountDetails/ViewAccountDetailsDto.cs

backend/PEMS.Application/Accounts/Queries/ViewAccountDetails/ViewAccountDetailsQueryHandler.cs

frontend/pems-react/src/features/account-management/types/accountManagement.types.ts

backend/PEMS.Api/Controllers/AccountsController.cs

database schema
SQL migration
```

---

# 13. Test bắt buộc

## 13.1. Shared validation frontend

Test các trường hợp:

| Input | Expected |
|---|---|
| `abc@gmail.com` | Valid |
| `abc@fpt.edu.vn` | Valid |
| `ABC@GMAIL.COM` | Valid sau normalize |
| ` abc@fpt.edu.vn ` | Valid sau trim |
| `abc@fe.edu.vn` | Invalid |
| `abc@student.fpt.edu.vn` | Invalid |
| `abc@sub.gmail.com` | Invalid |
| `abc@gmail.com.vn` | Invalid |
| `abc@fpt.edu.vn.evil.com` | Invalid |
| `abc+test@gmail.com` | Invalid |
| `a..b@gmail.com` | Invalid |
| `abc@@gmail.com` | Invalid |
| chuỗi rỗng | Required error |

Message domain phải đúng tuyệt đối:

```text
Chỉ chấp nhận @gmail.com và @fpt.edu.vn.
```

## 13.2. Modal tạo tài khoản

### Case hợp lệ

```text
@gmail.com
@fpt.edu.vn
```

Expected:

```text
- Không có lỗi email.
- Mở được bước xác nhận nếu các field khác hợp lệ.
```

### Case không hợp lệ

```text
@fe.edu.vn
```

Expected:

```text
- Viền đỏ.
- Message đúng.
- Không mở confirm.
- Không gọi API.
- Form không bị reset.
```

## 13.3. Modal chỉnh sửa thông tin account

Case:

```text
Đổi email sang @fe.edu.vn
```

Expected:

```text
- Không mở confirm đổi email.
- Không gọi update API.
- Hiển thị lỗi dưới field email.
- Dữ liệu form vẫn còn.
```

Case hợp lệ:

```text
Đổi email sang @gmail.com hoặc @fpt.edu.vn
```

Expected:

```text
- Mở confirm đổi email.
- Sau xác nhận gọi API đúng một lần.
```

## 13.4. Replace Staff Leader

### CREATE_NEW_USER

```text
@fe.edu.vn
```

Expected:

```text
- Không mở confirm.
- Không gọi API.
- Không thay leader.
```

### EXISTING_USER

Expected:

```text
- Không validate field email ẩn.
- Chỉ cần candidate + reason hợp lệ.
```

## 13.5. Backend unit test

Bổ sung/cập nhật:

```text
ValidateEmail_AllowsGmail
ValidateEmail_AllowsFptEduVn
ValidateEmail_RejectsFeEduVn
ValidateEmail_RejectsSubdomain
ValidateEmail_RejectsLookalikeDomain
ValidateEmail_RejectsPlusAddressing
```

## 13.6. Backend handler test

### CreateAccount

```text
@fe.edu.vn → rejected
```

Xác nhận:

```text
- không có user mới;
- không có confirmation row;
- không gửi email;
- không commit mutation.
```

### UpdateBasicAccountInfo

```text
@fe.edu.vn → rejected
```

Xác nhận:

```text
- users.email không đổi;
- không revoke session;
- không gửi email;
- không ghi success audit.
```

### ReplaceStaffLeader CREATE_NEW_USER

```text
@fe.edu.vn → rejected
```

Xác nhận:

```text
- leader cũ không đổi role;
- không có leader mới;
- transaction rollback;
- không gửi email.
```

## 13.7. UI status test

Response detail:

```text
ACTIVE
```

Expected:

```text
Hoạt động
```

Response:

```text
INACTIVE
```

Expected:

```text
Vô hiệu hóa
```

Response:

```text
LOCKED
```

Expected:

```text
Bị khóa
```

Response:

```text
PENDING_EMAIL_CONFIRMATION
```

Expected:

```text
Chờ xác nhận email
```

Response:

```text
SOME_UNKNOWN_STATUS
```

Expected:

```text
- Không crash.
- Hiển thị fallback.
```

## 13.8. Kiểm tra nguồn status

Tạo test hoặc manual check chứng minh:

```text
Status trong detail modal lấy từ details.status của UC-98.
```

Không chỉ dùng status cũ của row list.

---

# 14. Preflight bắt buộc trước khi code

Agent phải chạy:

```bash
git status --short
git branch --show-current
git rev-parse HEAD
git log -5 --oneline
git diff --check
```

Điều kiện bắt buộc:

```text
- Tiếp tục làm việc ngay trên nhánh hiện tại; không tạo nhánh mới và không chuyển sang `Dev`.
- Branch hiện tại phải là `Duy-Iter1`.
- Nếu tên branch thực tế không phải `Duy-Iter1`, dừng trước khi sửa code và báo cáo lại; không tự checkout/switch.
- Không tự checkout/switch branch khi working tree đang có WIP chưa được bảo toàn.
- Source of truth là HEAD hiện tại của `Duy-Iter1` tại thời điểm bắt đầu.
```

Báo cáo:

```text
- Branch hiện tại và xác nhận branch = `Duy-Iter1`.
- HEAD hiện tại của `Duy-Iter1`.
- Working tree có WIP hay không.
- Có file untracked/modified hay không.
- `Duy-Iter1` đang ahead/behind remote tương ứng bao nhiêu commit nếu kiểm tra được.
- Không reset/rebase/xóa WIP.
```

Không được:

```text
git reset --hard
git clean -fd
git checkout -- .
git restore .
rebase
force push
```

nếu chưa được user yêu cầu rõ.

---

# 15. Search/audit trước khi sửa

Chạy:

```bash
rg -n "fe\.edu\.vn|ALLOWED_ACCOUNT_EMAIL_DOMAINS|AllowedEmailDomains|EmailDomainNotAllowedMessage" \
  frontend backend tests
```

Chạy:

```bash
rg -n "Chỉ chấp nhận @gmail\.com|Email phải sử dụng một trong các tên miền" \
  frontend backend tests
```

Chạy:

```bash
rg -n "details\.status|rawStatus|Trạng thái tài khoản|PENDING_EMAIL_CONFIRMATION" \
  frontend/pems-react/src/pages/dashboard/accounts \
  frontend/pems-react/src/features/account-management
```

Phân loại từng hit:

```text
- runtime production code;
- test;
- seed;
- docs;
- legacy;
- ngoài phạm vi.
```

Không thay thế hàng loạt mù quáng.

---

# 16. Build và test gate

## 16.1. Backend

Từ root phù hợp của repository:

```bash
dotnet build
```

Chạy unit test account:

```bash
dotnet test tests/PEMS.UnitTests/PEMS.UnitTests.csproj
```

Chạy integration test liên quan account nếu có filter:

```bash
dotnet test tests/PEMS.IntegrationTests/PEMS.IntegrationTests.csproj
```

Nếu full test tốn thời gian, chạy targeted trước rồi full regression sau.

## 16.2. Frontend

```bash
cd frontend/pems-react
npm run type-check
npm run test -- --run
npm run build
```

Dùng đúng script tồn tại trong `package.json`; nếu tên script khác, kiểm tra rồi dùng script thật.

## 16.3. Static checks

```bash
git diff --check
```

Quét lại:

```bash
rg -n "fe\.edu\.vn" \
  frontend/pems-react/src/features/account-management \
  frontend/pems-react/src/pages/dashboard/accounts \
  backend/PEMS.Application/Accounts \
  tests/PEMS.UnitTests/Accounts
```

Expected:

```text
Không còn @fe.edu.vn trong runtime allowlist/helper/error/test expectation mới,
trừ case test cố ý xác nhận bị từ chối.
```

---

# 17. Manual verification

## 17.1. Tạo account

Đăng nhập HO:

1. Mở Quản lý tài khoản.
2. Bấm Khởi tạo tài khoản mới.
3. Chọn role và campus hợp lệ.
4. Nhập `abc@fe.edu.vn`.
5. Blur hoặc bấm Tiếp tục.
6. Kiểm tra lỗi đúng.
7. Đổi thành `abc@gmail.com`.
8. Kiểm tra đi được bước xác nhận.

## 17.2. Edit account

1. Chọn một account HO/Staff Leader HO có quyền sửa.
2. Bấm Xem.
3. Bấm Chỉnh sửa thông tin.
4. Nhập email `abc@fe.edu.vn`.
5. Bấm Cập nhật.
6. Kiểm tra không gọi API.
7. Đổi thành `abc@fpt.edu.vn`.
8. Kiểm tra mở confirmation đổi email.

## 17.3. Replace Staff Leader

1. Mở detail Staff Leader.
2. Bấm Thay thế Staff Leader.
3. Chọn tab Tạo tài khoản mới.
4. Nhập `newleader@fe.edu.vn`.
5. Bấm Tiếp tục.
6. Kiểm tra bị chặn.
7. Chuyển sang tab chọn nhân sự có sẵn.
8. Kiểm tra tab không bị email ẩn chặn.

## 17.4. Detail status

1. Mở một account ACTIVE.
2. Kiểm tra badge Hoạt động.
3. Mở account INACTIVE.
4. Kiểm tra badge Vô hiệu hóa.
5. Mở account LOCKED.
6. Kiểm tra badge Bị khóa.
7. Mở account pending email.
8. Kiểm tra badge Chờ xác nhận email.

---

# 18. Trường hợp biên và bảo mật

## 18.1. Exact domain

Phải từ chối:

```text
a@sub.fpt.edu.vn
a@fpt.edu.vn.evil.com
a@gmail.com.vn
a@fakegmail.com
```

## 18.2. Normalize

Phải chấp nhận sau normalize:

```text
 USER.NAME@GMAIL.COM 
```

lưu/so sánh thành:

```text
user.name@gmail.com
```

## 18.3. Direct API

Frontend hợp lệ không đủ.

Postman/curl gửi `@fe.edu.vn` phải bị backend reject.

## 18.4. Duplicate email

Sau khi domain hợp lệ, backend vẫn phải kiểm tra uniqueness như hiện tại.

Không thay đổi thứ tự business rule theo cách làm lộ thông tin không cần thiết.

## 18.5. Transaction

Không được xảy ra partial mutation ở replace Staff Leader.

Nếu email không hợp lệ:

```text
leader cũ giữ nguyên
leader mới không được tạo
session không bị revoke
email không được gửi
```

---

# 19. Không được làm

```text
- Không chỉ sửa helper text.
- Không bỏ backend validation.
- Không dùng regex khác nhau cho từng modal.
- Không copy-paste allowlist ở ba component.
- Không thêm migration DB.
- Không đổi API route.
- Không đổi role/sub-role.
- Không gộp status edit vào modal basic info.
- Không cho phép sửa status bằng dropdown trong detail modal.
- Không silently migrate email @fe.edu.vn trong DB.
- Không xóa dữ liệu legacy.
- Không báo hoàn thành nếu chưa test/build.
```

---

# 20. Definition of Done

Task chỉ hoàn thành khi đủ tất cả:

```text
[ ] Frontend allowlist chỉ còn gmail.com và fpt.edu.vn.
[ ] Backend allowlist chỉ còn gmail.com và fpt.edu.vn.
[ ] Message frontend/backend giống nhau.
[ ] Không còn helper text ba domain trong ba modal.
[ ] Modal tạo account chặn @fe.edu.vn.
[ ] Modal edit account chặn @fe.edu.vn.
[ ] Replace Staff Leader CREATE_NEW_USER chặn @fe.edu.vn.
[ ] Replace Staff Leader EXISTING_USER không bị email ẩn chặn.
[ ] Direct API chặn @fe.edu.vn.
[ ] Backend không tạo partial data khi validation fail.
[ ] Detail modal hiển thị status.
[ ] Status lấy từ response detail.
[ ] ACTIVE mapping đúng.
[ ] INACTIVE mapping đúng.
[ ] LOCKED mapping đúng.
[ ] PENDING_EMAIL_CONFIRMATION mapping đúng.
[ ] Có fallback cho status lạ.
[ ] Không thay schema.
[ ] Unit test backend xanh.
[ ] Integration test account xanh.
[ ] Frontend test/type-check/build xanh.
[ ] Backend build xanh.
[ ] git diff --check xanh.
[ ] Có báo cáo legacy @fe.edu.vn đang tồn tại bao nhiêu account.
```

---

# 21. Mẫu báo cáo cuối cùng Agent phải trả

```markdown
# Kết quả triển khai

## 1. Preflight
- Branch:
- HEAD:
- Working tree:
- WIP được bảo toàn:
- git diff --check:

## 2. Audit ban đầu
- Frontend allowlist cũ:
- Backend allowlist cũ:
- Helper text cũ:
- Các write path đã kiểm tra:
- API detail status hiện có:
- Số account legacy @fe.edu.vn:

## 3. File đã sửa

### Frontend
- ...

### Backend
- ...

### Tests
- ...

## 4. Thay đổi validation
- Domain cho phép:
- Domain bị loại:
- Message:
- Exact-domain behavior:
- Direct API behavior:

## 5. Thay đổi detail status
- Nguồn dữ liệu:
- Mapping:
- Fallback:
- Vị trí UI:
- Read-only trong edit mode:

## 6. Test
- Backend build:
- Unit tests:
- Integration tests:
- Frontend type-check:
- Frontend tests:
- Frontend build:
- Manual verification:

## 7. Regression/risk
- Existing @fe.edu.vn accounts:
- Database change:
- Role/permission change:
- Email/session flow impact:
- Known limitations:

## 8. Kết luận
- PASS / FAIL / PARTIAL
- Nếu PARTIAL: blocker chính xác và điểm tiếp tục.
```

---

# 22. Lệnh giao việc ngắn gọn cho AI Agent

```text
Đọc toàn bộ file này trước khi sửa code.

Thực hiện preflight và xác nhận đang đứng trên nhánh hiện tại `Duy-Iter1`. Nếu không phải `Duy-Iter1`, dừng trước khi sửa và không tự chuyển nhánh. Sau đó audit code thật tại HEAD hiện tại của `Duy-Iter1` và triển khai đồng bộ frontend + backend + tests cho hai yêu cầu:

1. Trong ba luồng HO:
   - Khởi tạo tài khoản mới;
   - Chỉnh sửa thông tin tài khoản;
   - Thay thế Staff Leader ở tab Tạo tài khoản mới;

   chỉ chấp nhận email có exact domain @gmail.com hoặc @fpt.edu.vn. Loại bỏ @fe.edu.vn khỏi frontend/backend allowlist và đồng bộ message thành:
   "Chỉ chấp nhận @gmail.com và @fpt.edu.vn."

2. Trong modal View account detail, hiển thị trạng thái tài khoản lấy từ UC-98 detail response. Hỗ trợ ACTIVE, INACTIVE, LOCKED, PENDING_EMAIL_CONFIRMATION và fallback an toàn. Status chỉ read-only, không thêm chức năng đổi trạng thái trong modal.

Không sửa schema, không đổi role/permission, không xóa dữ liệu legacy, không reset WIP. Chạy build/test đầy đủ và trả báo cáo đúng mẫu cuối file.
```
