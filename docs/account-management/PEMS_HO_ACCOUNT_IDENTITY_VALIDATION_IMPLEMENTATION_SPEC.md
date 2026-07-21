# PEMS — HO Account Identity Validation Consolidated Implementation Spec

> **Mục đích:** Tài liệu bàn giao tự chứa để AI Agent đọc và triển khai đồng bộ validation cho các luồng quản lý tài khoản của role **HO** trong PEMS.
>
> **Phạm vi chính:**
>
> 1. Modal **Khởi tạo tài khoản mới**.
> 2. Modal **Chỉnh sửa thông tin tài khoản**.
> 3. Modal **Thay thế Trưởng phòng IC** ở chế độ **Tạo tài khoản mới**.
>
> **Nguyên tắc bắt buộc:** Frontend chỉ giúp báo lỗi sớm và cải thiện UX. Backend vẫn là nguồn kiểm tra cuối cùng và phải từ chối mọi payload không hợp lệ kể cả khi người dùng gọi API trực tiếp.

---

# 1. Bối cảnh code hiện tại

Repository:

```text
quangthoai04/PEMS
```

Nhánh cần kiểm tra trước khi code:

```text
Dev
```

Không được tin tuyệt đối vào nội dung tài liệu này nếu code trên nhánh thực tế đã thay đổi. Trước khi sửa, AI Agent phải đọc lại các file hiện hành.

Các file trọng tâm hiện tại:

```text
frontend/pems-react/src/pages/dashboard/accounts/AccountManagement.tsx

frontend/pems-react/src/features/account-management/components/ReplaceStaffLeaderModal.tsx
frontend/pems-react/src/features/account-management/api/accountManagementApi.ts
frontend/pems-react/src/features/account-management/api/accountError.ts
frontend/pems-react/src/features/account-management/types/accountManagement.types.ts

backend/PEMS.Application/Accounts/Commands/CreateAccount/CreateAccountCommand.cs
backend/PEMS.Application/Accounts/Commands/CreateAccount/CreateAccountCommandValidator.cs
backend/PEMS.Application/Accounts/Commands/CreateAccount/CreateAccountCommandHandler.cs

backend/PEMS.Application/Accounts/Commands/UpdateBasicAccountInfo/UpdateBasicAccountInfoCommand.cs
backend/PEMS.Application/Accounts/Commands/UpdateBasicAccountInfo/UpdateBasicAccountInfoCommandValidator.cs
backend/PEMS.Application/Accounts/Commands/UpdateBasicAccountInfo/UpdateBasicAccountInfoCommandHandler.cs

backend/PEMS.Application/Accounts/Commands/ReplaceStaffLeader/ReplaceStaffLeaderCommand.cs
backend/PEMS.Application/Accounts/Commands/ReplaceStaffLeader/ReplaceStaffLeaderCommandValidator.cs
backend/PEMS.Application/Accounts/Commands/ReplaceStaffLeader/ReplaceStaffLeaderCommandHandler.cs

backend/PEMS.Application/Accounts/Common/
tests/PEMS.UnitTests/Accounts/
tests/PEMS.IntegrationTests/
```

Trạng thái validation hiện tại:

- Create Account backend đã có:
  - email bắt buộc;
  - email đúng định dạng;
  - email tối đa 150 ký tự;
  - họ tên bắt buộc;
  - họ tên tối đa 150 ký tự.
- Create Account frontend mới kiểm tra:
  - role;
  - campus;
  - họ tên không rỗng;
  - email theo regex đơn giản.
- Update Basic Account Info đã kiểm tra:
  - họ tên bắt buộc;
  - họ tên tối đa 150;
  - email bắt buộc;
  - email tối đa 150;
  - email đúng định dạng;
  - email không trùng.
- Replace Staff Leader ở mode `CREATE_NEW_USER` đã kiểm tra:
  - họ tên bắt buộc;
  - email bắt buộc;
  - email đúng định dạng;
  - họ tên/email tối đa 150.
- Chưa có rule dùng chung cho:
  - chuẩn hóa họ tên;
  - độ dài tối thiểu;
  - tập ký tự hợp lệ;
  - domain email whitelist;
  - dấu cộng trong email;
  - field-level validation nhất quán;
  - thông báo lỗi thống nhất.

---

# 2. Mục tiêu triển khai

Sau khi hoàn thành:

1. Ba luồng của HO phải dùng cùng một bộ quy tắc validation cho `fullName` và `email`.
2. Frontend phải báo lỗi ngay tại field tương ứng.
3. Backend phải kiểm tra lại toàn bộ rule.
4. Không duplicate nhiều regex và message khác nhau trong nhiều component/validator.
5. Dữ liệu phải được normalize trước khi so sánh, kiểm tra thay đổi và ghi database.
6. Không phá vỡ các business rule hiện có:
   - HO chỉ tạo đúng role được phép;
   - không tạo trùng email;
   - không tạo Staff Leader khi campus đã có Staff Leader;
   - thay đổi email phải thu hồi session và xử lý lại SSO/FEID theo flow hiện tại;
   - thay Staff Leader phải transaction-safe;
   - backend không tin dữ liệu role/campus/department do client tự suy đoán.

---

# 3. Quy tắc chuẩn hóa dùng chung

## 3.1. Chuẩn hóa họ và tên

Tạo hàm dùng chung:

```text
normalizeFullName(value)
```

Quy tắc:

1. Nếu `null`/`undefined`, xem như chuỗi rỗng.
2. Loại bỏ ký tự điều khiển.
3. `trim()` khoảng trắng đầu/cuối.
4. Chuyển nhiều khoảng trắng liên tiếp, tab, xuống dòng thành đúng một dấu cách.
5. Không tự động đổi hoa/thường tên người.
6. Không tự ý bỏ dấu tiếng Việt.
7. Không tự ý đổi dấu gạch nối, dấu nháy đơn hoặc dấu chấm hợp lệ.

Ví dụ:

```text
"  Nguyễn   Văn   An  "  -> "Nguyễn Văn An"
"Trần\tMinh\nAnh"        -> "Trần Minh Anh"
"O'Connor"                -> "O'Connor"
"Jean-Luc Picard"         -> "Jean-Luc Picard"
```

## 3.2. Chuẩn hóa email

Tạo hàm dùng chung:

```text
normalizeAccountEmail(value)
```

Quy tắc:

1. Nếu `null`/`undefined`, xem như chuỗi rỗng.
2. `trim()` khoảng trắng đầu/cuối.
3. Chuyển toàn bộ thành lowercase.
4. Không được có khoảng trắng ở bất kỳ vị trí nào.
5. Không sửa dấu chấm, không sửa local-part theo quy tắc riêng của Gmail.
6. Không tự chuyển domain khác thành domain được phép.

Ví dụ:

```text
"  User.Name@FPT.EDU.VN  "
-> "user.name@fpt.edu.vn"
```

---

# 4. Validation họ và tên

## 4.1. Bắt buộc

Sau normalize, họ và tên không được rỗng.

Message:

```text
Vui lòng nhập họ và tên.
```

## 4.2. Độ dài

Sau normalize:

```text
Tối thiểu: 2 ký tự
Tối đa: 150 ký tự
```

Message:

```text
Họ và tên phải có ít nhất 2 ký tự.
Họ và tên không được vượt quá 150 ký tự.
```

Không yêu cầu tên phải có ít nhất hai từ. Tên một từ vẫn hợp lệ nếu có ít nhất 2 ký tự.

Ví dụ hợp lệ:

```text
An
Li
Minh
Nguyễn Văn An
```

## 4.3. Ký tự được phép

Cho phép:

- Chữ Unicode, bao gồm đầy đủ tiếng Việt.
- Khoảng trắng.
- Dấu gạch nối: `-`
- Dấu nháy đơn ASCII: `'`
- Dấu nháy đơn Unicode phổ biến: `’`
- Dấu chấm: `.`

Không cho phép:

- Chữ số.
- Emoji.
- HTML tag.
- Ký tự điều khiển.
- Các ký tự nguy hiểm hoặc không thuộc cấu trúc tên như:
  - `<`
  - `>`
  - `{`
  - `}`
  - `[`
  - `]`
  - `\`
  - `/`
  - `=`
  - `@`
  - `#`
  - `$`
  - `%`
  - `^`
  - `&`
  - `*`
- Chuỗi chỉ gồm dấu câu/khoảng trắng.
- Dấu câu lặp bất hợp lý để tạo chuỗi vô nghĩa.

Ví dụ hợp lệ:

```text
Nguyễn Văn An
Trần Minh-Anh
O'Connor
D’Arcy
J. Smith
Đỗ Thị Hồng
```

Ví dụ không hợp lệ:

```text
Nguyễn Văn 123
<script>alert(1)</script>
@@@
😊 Nguyễn Văn An
---
...
```

Message thống nhất:

```text
Họ và tên chỉ được chứa chữ cái, khoảng trắng, dấu chấm, dấu nháy đơn và dấu gạch nối.
```

## 4.4. Không kiểm tra uniqueness theo họ tên

Không được từ chối vì họ tên trùng với người khác.

Uniqueness chỉ áp dụng cho các định danh như:

- email;
- MSSV;
- các mã nghiệp vụ khác nếu đã có rule riêng.

---

# 5. Validation email

## 5.1. Bắt buộc

Sau normalize, email không được rỗng.

Message:

```text
Vui lòng nhập email.
```

## 5.2. Tổng độ dài

```text
Email tối đa 150 ký tự.
```

Message:

```text
Email không được vượt quá 150 ký tự.
```

Frontend phải thêm:

```tsx
maxLength={150}
```

Nhưng backend vẫn phải kiểm tra lại.

## 5.3. Độ dài local-part

Phần đứng trước ký tự `@`:

```text
Tối đa 64 ký tự.
```

Message:

```text
Phần tên email trước ký tự @ không được vượt quá 64 ký tự.
```

## 5.4. Định dạng email

Email phải thỏa mãn:

1. Có đúng một ký tự `@`.
2. Có local-part trước `@`.
3. Có domain sau `@`.
4. Không có khoảng trắng.
5. Local-part không bắt đầu bằng dấu chấm.
6. Local-part không kết thúc bằng dấu chấm.
7. Không có hai dấu chấm liên tiếp trong local-part.
8. Domain phải được so sánh chính xác sau khi lowercase.
9. Không chấp nhận chuỗi chứa ký tự điều khiển.
10. Không chấp nhận payload dạng display-name:
    - `Nguyen Van A <abc@gmail.com>`
11. Không chấp nhận email chứa dấu cộng `+`.

Ví dụ không hợp lệ:

```text
abc
abc@
@gmail.com
abc..def@gmail.com
.abc@gmail.com
abc.@gmail.com
abc @gmail.com
Nguyen Van A <abc@gmail.com>
abc+test@gmail.com
```

Message format chung:

```text
Email không đúng định dạng.
```

Message dấu cộng:

```text
Email dùng để đăng nhập không được chứa dấu cộng (+).
```

## 5.5. Domain whitelist

Domain phải chính xác là một trong ba giá trị:

```text
gmail.com
fpt.edu.vn
fe.edu.vn
```

Được phép:

```text
nguyenvana@gmail.com
nguyenvana@fpt.edu.vn
canhnvt@fe.edu.vn
```

Không được phép:

```text
abc@yahoo.com
abc@outlook.com
abc@fpt.com.vn
abc@student.fpt.edu.vn
abc@gmail.com.example.com
abc@fakefpt.edu.vn
```

Phải tách domain rồi so sánh exact match.

Không được dùng cách kiểm tra lỏng như:

```ts
email.includes('@fpt.edu.vn')
email.endsWith('fpt.edu.vn')
```

vì có thể chấp nhận sai subdomain hoặc domain giả.

Message:

```text
Email phải sử dụng một trong các tên miền: @gmail.com, @fpt.edu.vn hoặc @fe.edu.vn.
```

## 5.6. Email uniqueness

Backend phải tiếp tục là nguồn kiểm tra cuối cùng.

### Khi tạo mới

Áp dụng cho:

- Modal tạo tài khoản mới.
- Replace Staff Leader ở mode `CREATE_NEW_USER`.

Rule:

```text
Không tồn tại user khác có email đã normalize giống email mới.
```

Message:

```text
Email này đã được sử dụng bởi một tài khoản khác.
```

### Khi chỉnh sửa

Loại trừ chính tài khoản đang chỉnh sửa:

```text
u.Email == normalizedNewEmail
AND u.UserId != targetUserId
```

Message giữ nguyên:

```text
Email này đã được sử dụng bởi một tài khoản khác.
```

## 5.7. Case-insensitive

Các email sau phải được coi là cùng một email:

```text
User@FPT.EDU.VN
user@fpt.edu.vn
 USER@fpt.edu.vn
```

Backend phải normalize trước khi query uniqueness.

---

# 6. Phạm vi áp dụng theo từng màn hình

# 6.1. Modal Khởi tạo tài khoản mới

Áp dụng cho HO tại trang Account Management.

## 6.1.1. Họ và tên

Áp dụng đầy đủ:

- required;
- normalize;
- 2–150 ký tự;
- ký tự hợp lệ;
- field-level error;
- backend revalidate.

## 6.1.2. Email

Áp dụng đầy đủ:

- required;
- normalize lowercase;
- format;
- local-part ≤64;
- tổng ≤150;
- exact domain whitelist;
- không dấu cộng;
- uniqueness;
- field-level error;
- backend revalidate.

## 6.1.3. Role

Giữ đúng business rule hiện hành:

HO chỉ được tạo:

```text
HO
STAFF + LEADER
```

Không cho client tự quyết định sub-role trái rule.

Backend phải tiếp tục derive:

- sub-role;
- IC department;
- campus scope;
- các mapping liên quan.

## 6.1.4. Campus

Giữ các validation hiện có:

- bắt buộc chọn campus;
- campus phải tồn tại;
- campus phải ACTIVE;
- tạo Staff Leader:
  - campus phải có đúng IC department hợp lệ;
  - không được có Staff Leader hiện tại;
  - nếu đã có thì yêu cầu dùng flow Thay thế Trưởng phòng IC;
- tạo HO:
  - giữ rule uniqueness HO theo campus đang có;
  - backend re-check trong transaction.

## 6.1.5. UX

Nút:

```text
Xác nhận tạo
```

Phải:

- disabled khi form invalid;
- disabled khi đang submit;
- chống double click;
- không gửi request nếu validation frontend fail;
- vẫn xử lý lỗi backend nếu request bị từ chối.

Không chỉ hiển thị một lỗi chung cuối modal. Mỗi field phải có lỗi ngay bên dưới.

Ví dụ:

```text
Họ và tên
[ Nguyễn Văn 123 ]
Họ và tên chỉ được chứa chữ cái, khoảng trắng, dấu chấm, dấu nháy đơn và dấu gạch nối.

Email
[ abc@yahoo.com ]
Email phải sử dụng một trong các tên miền: @gmail.com, @fpt.edu.vn hoặc @fe.edu.vn.
```

---

# 6.2. Modal Chỉnh sửa thông tin tài khoản

Đây là flow HO chỉnh sửa thông tin cơ bản của tài khoản nằm trong scope hiện hành.

## 6.2.1. Họ và tên

Áp dụng cùng rule với create:

- required;
- normalize;
- 2–150 ký tự;
- ký tự hợp lệ;
- field-level error.

## 6.2.2. Email

Áp dụng cùng rule với create:

- required;
- normalize;
- format;
- local-part ≤64;
- tổng ≤150;
- exact domain whitelist;
- không dấu cộng;
- uniqueness, loại trừ chính target user.

## 6.2.3. No-op detection

Nút:

```text
Cập nhật
```

Phải disabled khi:

```text
normalizedFullName == normalizedOldFullName
AND
normalizedEmail == normalizedOldEmail
```

So sánh email không phân biệt hoa/thường.

Không gửi API cho no-op để tránh:

- audit thừa;
- revoke session thừa;
- gửi email thừa;
- cập nhật `updated_at` không cần thiết.

## 6.2.4. Khi email thay đổi

Trước khi submit thật, phải có confirmation hiển thị rõ:

```text
Email cũ: ...
Email mới: ...

Sau khi cập nhật:
- các phiên đăng nhập hiện tại sẽ bị thu hồi;
- email mới sẽ được dùng để đăng nhập;
- liên kết SSO/FEID có thể cần được liên kết lại;
- email cũ không còn dùng để đăng nhập.
```

Không bỏ flow confirmation hiện tại.

Backend phải giữ các hành vi:

- update `users.email`;
- update `provider_email`;
- reset `provider_subject` cho Google SSO/FEID khi cần;
- clear `email_verified_at`;
- revoke active sessions;
- audit;
- gửi thông báo tới email cũ và email mới theo flow hiện hành.

## 6.2.5. Scope/authorization

Không được làm yếu các guard hiện có.

Backend tiếp tục chặn:

- caller không phải HO;
- HO sửa chính mình;
- target `LOCKED`;
- target ngoài scope HO;
- target không phải HO hoặc Staff Leader theo business rule hiện hành;
- direct API call cố gửi thêm role/campus/department/status.

Command edit basic info chỉ được mang:

```text
UserId
FullName
Email
```

Không thêm các field quyền khác vào payload này.

---

# 6.3. Modal Thay thế Trưởng phòng IC

Có hai mode:

```text
EXISTING_USER
CREATE_NEW_USER
```

## 6.3.1. Mode EXISTING_USER

Không validate input họ tên/email ẩn hoặc không sử dụng.

Backend phải giữ rule ứng viên:

- đúng `STAFF + STAFF`;
- ACTIVE;
- cùng campus;
- đúng IC department;
- không phải current leader;
- vẫn hợp lệ tại thời điểm transaction;
- không tin danh sách frontend.

Frontend chỉ yêu cầu chọn candidate.

Message:

```text
Vui lòng chọn nhân sự thay thế.
```

## 6.3.2. Mode CREATE_NEW_USER

Áp dụng toàn bộ rule chung:

### Họ và tên

- required;
- normalize;
- 2–150;
- ký tự hợp lệ.

### Email

- required;
- normalize lowercase;
- format;
- local-part ≤64;
- tổng ≤150;
- domain whitelist;
- không dấu cộng;
- uniqueness.

## 6.3.3. Lý do thay thế

Rule mới:

```text
Bắt buộc
Tối thiểu 10 ký tự sau trim
Tối đa 500 ký tự
Không chỉ gồm khoảng trắng/dấu câu
```

Không hợp lệ:

```text
...
-----
abc
```

Hợp lệ:

```text
Điều chuyển nhân sự phụ trách Phòng Hợp tác Quốc tế từ tháng 8/2026.
```

Message:

```text
Vui lòng nhập lý do thay thế.
Lý do thay thế phải có ít nhất 10 ký tự.
Lý do thay thế không được vượt quá 500 ký tự.
Lý do thay thế phải chứa nội dung có ý nghĩa.
```

## 6.3.4. Confirmation cuối

Trước khi gọi API thay thế, modal confirm phải hiển thị:

- campus;
- IC department;
- Staff Leader hiện tại;
- Staff Leader mới;
- email người mới;
- mode;
- lý do;
- cảnh báo người cũ sẽ bị hạ xuống `STAFF + STAFF`;
- cảnh báo session liên quan sẽ bị thu hồi.

## 6.3.5. Backend transaction

Không thay đổi đặc tính transaction-safe hiện tại:

- kiểm tra consistency campus/head/IC department;
- load lại current leader;
- re-check trong transaction;
- create/promote new leader;
- demote old leader;
- repoint campus IC head;
- repoint IC department head;
- audit;
- revoke sessions;
- commit;
- gửi email sau commit.

Validation mới phải chạy trước write, nhưng không được thay thế concurrency re-check trong transaction.

---

# 7. Thiết kế frontend dùng chung

Tạo file dùng chung, ví dụ:

```text
frontend/pems-react/src/features/account-management/validation/accountIdentityValidation.ts
```

Nội dung nên gồm:

```ts
export const ACCOUNT_FULL_NAME_MIN_LENGTH = 2;
export const ACCOUNT_FULL_NAME_MAX_LENGTH = 150;
export const ACCOUNT_EMAIL_MAX_LENGTH = 150;
export const ACCOUNT_EMAIL_LOCAL_PART_MAX_LENGTH = 64;

export const ALLOWED_ACCOUNT_EMAIL_DOMAINS = [
  'gmail.com',
  'fpt.edu.vn',
  'fe.edu.vn',
] as const;

export function normalizeFullName(value?: string | null): string;
export function normalizeAccountEmail(value?: string | null): string;

export function validateFullName(value?: string | null): string | null;
export function validateAccountEmail(value?: string | null): string | null;
export function validateReplacementReason(value?: string | null): string | null;
```

Có thể trả về object nếu project ưu tiên field code:

```ts
type FieldValidationResult =
  | { valid: true; normalizedValue: string }
  | { valid: false; normalizedValue: string; message: string };
```

Khuyến nghị:

```ts
validateFullName(...)
validateAccountEmail(...)
```

không tự mutate React state. Component sẽ quyết định khi nào set normalized value.

## 7.1. React state lỗi

Tạo state rõ ràng thay vì một `createError` chung cho mọi lỗi:

```ts
type AccountIdentityFieldErrors = {
  fullName?: string;
  email?: string;
};
```

Create modal:

```ts
const [createFieldErrors, setCreateFieldErrors] = useState<AccountIdentityFieldErrors>({});
```

Edit modal:

```ts
const [editFieldErrors, setEditFieldErrors] = useState<AccountIdentityFieldErrors>({});
```

Replace modal:

```ts
const [replaceFieldErrors, setReplaceFieldErrors] = useState<{
  fullName?: string;
  email?: string;
  reason?: string;
}>({});
```

Lỗi server-level vẫn có thể hiển thị ở alert chung.

## 7.2. Khi validate

Thực hiện:

- `onBlur`: validate field và hiện lỗi.
- `onChange`: xóa/cập nhật lỗi khi người dùng sửa.
- `onSubmit`: validate toàn bộ lần cuối.
- trước API call: dùng normalized values.

Không chỉ dựa vào `type="email"` của browser.

## 7.3. Input attributes

Họ tên:

```tsx
type="text"
maxLength={150}
autoComplete="name"
```

Email:

```tsx
type="email"
maxLength={150}
autoComplete="email"
inputMode="email"
```

Lý do:

```tsx
maxLength={500}
```

## 7.4. Hiển thị lỗi

Input lỗi phải có:

- border đỏ;
- message ngay dưới input;
- `aria-invalid`;
- `aria-describedby`.

Không xóa toàn bộ form khi API trả lỗi.

---

# 8. Thiết kế backend dùng chung

Tạo rule dùng chung, ví dụ:

```text
backend/PEMS.Application/Accounts/Common/AccountIdentityRules.cs
```

Gợi ý API:

```csharp
public static class AccountIdentityRules
{
    public const int FullNameMinLength = 2;
    public const int FullNameMaxLength = 150;
    public const int EmailMaxLength = 150;
    public const int EmailLocalPartMaxLength = 64;

    public static readonly IReadOnlySet<string> AllowedEmailDomains;

    public static string NormalizeFullName(string? value);
    public static string NormalizeEmail(string? value);

    public static bool IsValidFullName(string value);
    public static bool HasAllowedEmailDomain(string email);
    public static bool HasValidLocalPart(string email);
    public static bool ContainsPlusAddressing(string email);
}
```

Có thể thêm FluentValidation extension để tránh duplicate:

```text
backend/PEMS.Application/Accounts/Common/AccountIdentityValidationExtensions.cs
```

Ví dụ:

```csharp
public static IRuleBuilderOptions<T, string?> ApplyAccountFullNameRules<T>(...);
public static IRuleBuilderOptions<T, string?> ApplyAccountEmailRules<T>(...);
```

Ba validator bắt buộc dùng chung:

```text
CreateAccountCommandValidator
UpdateBasicAccountInfoCommandValidator
ReplaceStaffLeaderCommandValidator
```

Không để mỗi validator có regex/message khác nhau.

---

# 9. Thứ tự backend validation

Khuyến nghị thứ tự:

## 9.1. Full name

1. Normalize.
2. Required.
3. Minimum length.
4. Maximum length.
5. Character validation.
6. Meaningful content.

## 9.2. Email

1. Normalize.
2. Required.
3. Total max length.
4. Exactly one `@`.
5. Local-part present.
6. Domain present.
7. Local-part max 64.
8. No whitespace/control character.
9. No leading/trailing/consecutive dot.
10. No `+`.
11. General email format.
12. Exact allowed domain.
13. Uniqueness in handler/database.

Uniqueness là DB-dependent nên tiếp tục ở handler/service, không bắt buộc đặt trong pure validator.

---

# 10. Cập nhật từng backend validator

# 10.1. CreateAccountCommandValidator

Thay rule rời rạc hiện tại bằng shared rules:

```text
FullName:
- required
- normalize-aware
- 2–150
- allowed characters

Email:
- required
- normalize-aware
- valid structure
- local part <= 64
- total <= 150
- no plus
- allowed domain
```

Handler vẫn phải:

```csharp
var email = AccountIdentityRules.NormalizeEmail(request.Email);
var fullName = AccountIdentityRules.NormalizeFullName(request.FullName);
```

Sau đó dùng normalized values để:

- query duplicate;
- tạo entity;
- audit;
- gửi email.

# 10.2. UpdateBasicAccountInfoCommandValidator

Dùng cùng shared rules.

Handler không được duplicate logic khác chuẩn.

Dùng normalized:

```csharp
var newFullName = AccountIdentityRules.NormalizeFullName(request.FullName);
var newEmail = AccountIdentityRules.NormalizeEmail(request.Email);
```

Uniqueness exclude target.

# 10.3. ReplaceStaffLeaderCommandValidator

Mode `CREATE_NEW_USER`:

- dùng shared full name rule;
- dùng shared email rule.

Reason:

- required;
- 10–500;
- meaningful content.

Mode `EXISTING_USER`:

- không require FullName/Email;
- require `NewLeaderUserId`.

Handler mode create-new phải dùng normalized values trước uniqueness và insert.

---

# 11. Error message chuẩn

Dùng thống nhất cả frontend và backend:

```text
Vui lòng nhập họ và tên.
Họ và tên phải có ít nhất 2 ký tự.
Họ và tên không được vượt quá 150 ký tự.
Họ và tên chỉ được chứa chữ cái, khoảng trắng, dấu chấm, dấu nháy đơn và dấu gạch nối.

Vui lòng nhập email.
Email không đúng định dạng.
Email không được vượt quá 150 ký tự.
Phần tên email trước ký tự @ không được vượt quá 64 ký tự.
Email dùng để đăng nhập không được chứa dấu cộng (+).
Email phải sử dụng một trong các tên miền: @gmail.com, @fpt.edu.vn hoặc @fe.edu.vn.
Email này đã được sử dụng bởi một tài khoản khác.

Vui lòng nhập lý do thay thế.
Lý do thay thế phải có ít nhất 10 ký tự.
Lý do thay thế không được vượt quá 500 ký tự.
Lý do thay thế phải chứa nội dung có ý nghĩa.
```

Không parse message để điều khiển logic frontend. Nếu project đang có stable `errorCode`, bổ sung code phù hợp cho domain/duplicate/business conflict.

Gợi ý:

```text
ACCOUNT_FULL_NAME_INVALID
ACCOUNT_EMAIL_INVALID
ACCOUNT_EMAIL_DOMAIN_NOT_ALLOWED
ACCOUNT_EMAIL_PLUS_NOT_ALLOWED
ACCOUNT_EMAIL_ALREADY_EXISTS
REPLACEMENT_REASON_INVALID
```

Không bắt buộc đổi contract cũ nếu gây blast radius lớn, nhưng lỗi field frontend nên map rõ ràng.

---

# 12. Test bắt buộc

# 12.1. Unit test shared validation

## Full name hợp lệ

```text
Nguyễn Văn An
An
Jean-Luc Picard
O'Connor
D’Arcy
J. Smith
```

## Full name normalize

```text
"  Nguyễn   Văn   An  "
-> "Nguyễn Văn An"
```

## Full name không hợp lệ

```text
""
" "
"A"
"Nguyễn Văn 123"
"<script>alert(1)</script>"
"😊 Nguyễn Văn An"
"..."
"---"
chuỗi >150 ký tự
```

## Email hợp lệ

```text
user@gmail.com
user.name@gmail.com
user@fpt.edu.vn
user@fe.edu.vn
USER@FPT.EDU.VN
```

## Email normalize

```text
"  USER@FPT.EDU.VN "
-> "user@fpt.edu.vn"
```

## Email không hợp lệ

```text
""
"abc"
"abc@"
"@gmail.com"
"abc..def@gmail.com"
".abc@gmail.com"
"abc.@gmail.com"
"abc @gmail.com"
"abc+test@gmail.com"
"abc@yahoo.com"
"abc@student.fpt.edu.vn"
"abc@gmail.com.example.com"
local-part >64
total >150
```

# 12.2. Create Account validator/handler tests

Phải có test:

- HO tạo account với tên hợp lệ, Gmail.
- HO tạo account với `@fpt.edu.vn`.
- HO tạo account với `@fe.edu.vn`.
- normalize uppercase email.
- reject Yahoo/Outlook.
- reject plus addressing.
- reject tên có số.
- reject tên 1 ký tự.
- reject quá 150.
- duplicate email case-insensitive.
- payload direct API không bypass rule.
- business rule campus/role hiện có vẫn xanh.

# 12.3. Update Basic Account Info tests

Phải có:

- update chỉ tên.
- update email hợp lệ.
- normalize email.
- no-op không gây API call phía frontend.
- duplicate email exclude self.
- duplicate email của user khác bị reject.
- invalid domain bị reject.
- plus addressing bị reject.
- invalid full name bị reject.
- email change vẫn revoke sessions.
- email change vẫn reset SSO/FEID subject.
- HO không sửa self.
- LOCKED target bị chặn.
- out-of-scope target bị chặn.

# 12.4. Replace Staff Leader tests

Mode existing:

- không yêu cầu fullName/email.
- candidate phải đúng campus/IC department/role/status.
- không chọn current leader.

Mode create new:

- fullName valid.
- email valid và allowed domain.
- invalid domain reject.
- plus reject.
- duplicate reject.
- invalid name reject.
- reason <10 reject.
- reason >500 reject.
- reason chỉ dấu câu reject.
- transaction rollback khi lỗi.
- old leader demote.
- new leader create/promote.
- heads repoint.
- sessions revoked.
- audit created.

# 12.5. Frontend tests

Nếu project đang dùng Vitest/Testing Library, bổ sung:

- lỗi hiển thị đúng dưới field.
- submit disabled khi invalid.
- submit enabled khi valid.
- email uppercase được normalize trước payload.
- domain invalid không gọi API.
- plus email không gọi API.
- edit no-op disabled.
- Replace mode existing không validate hidden create fields.
- Replace mode create-new validate fullName/email/reason.
- double submit bị chặn.

---

# 13. Acceptance Criteria

## AC-01 — Create full name validation

Given HO mở modal tạo tài khoản mới  
When nhập họ tên không hợp lệ  
Then lỗi hiển thị ngay dưới field  
And frontend không gọi API  
And direct API call vẫn bị backend từ chối.

## AC-02 — Create allowed email domain

Given HO nhập email  
When domain không thuộc `gmail.com`, `fpt.edu.vn`, `fe.edu.vn`  
Then hệ thống hiển thị thông báo domain không được phép  
And không tạo account.

## AC-03 — Create plus email

Given HO nhập `user+test@gmail.com`  
When submit  
Then frontend từ chối  
And backend cũng từ chối nếu gọi trực tiếp.

## AC-04 — Edit validation consistency

Given HO chỉnh sửa account  
When nhập họ tên/email  
Then áp dụng đúng cùng bộ rule với create.

## AC-05 — Edit no-op

Given normalized name/email không đổi  
Then nút Cập nhật disabled  
And không gọi API.

## AC-06 — Edit email side effects

Given email thay đổi hợp lệ  
When HO confirm  
Then account email được cập nhật  
And active sessions bị revoke  
And SSO/FEID relink behavior giữ nguyên  
And audit được tạo.

## AC-07 — Replace existing mode

Given mode `EXISTING_USER`  
Then hidden create-new name/email không được validate  
And chỉ candidate hợp lệ mới được dùng.

## AC-08 — Replace create mode

Given mode `CREATE_NEW_USER`  
Then name/email dùng cùng shared validation  
And reason phải 10–500 ký tự, có nội dung.

## AC-09 — Exact domain matching

Given email `abc@student.fpt.edu.vn` hoặc `abc@gmail.com.example.com`  
Then hệ thống phải từ chối.

## AC-10 — Backend source of truth

Given client bỏ qua frontend validation  
When gọi API trực tiếp với payload invalid  
Then backend trả lỗi 4xx an toàn  
And không có dữ liệu không hợp lệ được ghi.

---

# 14. Non-goals

Không triển khai ngoài phạm vi:

- Không thay đổi permission matrix.
- Không mở thêm role cho HO tạo.
- Không thay đổi schema role/sub-role.
- Không thay đổi logic replace transaction.
- Không cho HO edit role/campus/department/status qua endpoint basic-info.
- Không tự động sửa domain email.
- Không kiểm tra sự tồn tại thực tế của mailbox bằng gửi OTP.
- Không thêm third-party email validation API.
- Không dùng tên làm unique key.
- Không tự đổi format tên sang Title Case.
- Không migration dữ liệu cũ nếu chưa có yêu cầu riêng.

---

# 15. Thứ tự triển khai khuyến nghị

1. Đọc lại code hiện tại và test liên quan.
2. Tạo backend shared `AccountIdentityRules`.
3. Tạo FluentValidation extension dùng chung nếu phù hợp.
4. Cập nhật ba validator.
5. Cập nhật ba handler dùng normalized values.
6. Thêm unit tests backend.
7. Tạo frontend shared validation utility.
8. Cập nhật Create modal.
9. Cập nhật Edit modal.
10. Cập nhật Replace modal.
11. Thêm frontend tests.
12. Chạy:
    - backend build;
    - UnitTests;
    - IntegrationTests liên quan;
    - frontend typecheck/lint;
    - frontend unit tests;
    - project structure guard.
13. Kiểm tra không làm hỏng flow role/campus/session/audit/email hiện có.

Lệnh guard trước commit:

```powershell
.\scripts\guard-project-structure.ps1
```

---

# 16. Definition of Done

Chỉ được báo hoàn thành khi:

- Ba màn hình dùng cùng shared validation.
- Frontend và backend đồng nhất.
- Exact domain whitelist hoạt động.
- Email dấu cộng bị chặn.
- Full name Unicode hợp lệ được chấp nhận.
- Invalid name/email bị backend chặn.
- Field errors hiển thị đúng.
- No-op edit không submit.
- Existing-user replace không bị validation nhầm.
- Reason replace có rule 10–500 và meaningful.
- Unit tests mới xanh.
- Integration tests liên quan xanh.
- Frontend build/typecheck xanh.
- Không phá vỡ session revoke, SSO/FEID relink, audit, email và transaction hiện có.
- Không tạo commit rời rạc chỉ chứa thay đổi nhỏ không cần thiết; gom thay đổi theo functional slice hợp lý.
- Commit message không chứa tên hoặc attribution của AI.
