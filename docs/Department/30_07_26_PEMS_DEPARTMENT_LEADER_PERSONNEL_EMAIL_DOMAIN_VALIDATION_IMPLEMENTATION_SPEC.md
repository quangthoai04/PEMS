# PEMS — IMPLEMENTATION SPEC
## Department Leader: Chỉ cho phép email `@gmail.com` và `@fpt.edu.vn` trong modal thêm mới và chỉnh sửa nhân sự

> **Mục đích tài liệu**
>
> Đây là đặc tả độc lập cho chức năng **Quản lý phòng ban** do role:
>
> ```text
> DEPARTMENT / LEADER
> ```
>
> quản lý.
>
> Yêu cầu áp dụng cho:
>
> ```text
> 1. Modal “Thêm nhân sự mới”
> 2. Modal “Chỉnh sửa thông tin nhân sự”
> ```
>
> Trong cả hai modal, trường email chỉ được chấp nhận khi có chính xác một trong hai tên miền:
>
> ```text
> @gmail.com
> @fpt.edu.vn
> ```
>
> Không còn chấp nhận:
>
> ```text
> @fe.edu.vn
> ```
>
> Quy tắc chỉnh sửa áp dụng cho nhân sự ở **mọi trạng thái tài khoản**:
>
> ```text
> ACTIVE
> INACTIVE
> PENDING_EMAIL_CONFIRMATION
> LOCKED
> ```
>
> **Repository:** `quangthoai04/PEMS`  
> **Nhánh làm việc bắt buộc:** nhánh hiện tại `Duy-Iter1`  
> **Nguồn code chuẩn:** HEAD hiện tại của `Duy-Iter1` tại thời điểm Agent bắt đầu  
> **Không chuyển sang `Dev`, không tạo nhánh mới, không reset/rebase/xóa WIP**
>
> Các đường dẫn, class và baseline trong tài liệu này phải được Agent đối chiếu lại với code thật trên HEAD hiện tại trước khi sửa.

---

# 1. Mục tiêu nghiệp vụ

Tại trang:

```text
/dashboard/my-department
```

Department Leader có thể:

```text
- Thêm nhân sự mới vào phòng ban của mình.
- Mở chi tiết một nhân sự.
- Chỉnh sửa họ tên, email, số điện thoại và giới tính.
```

Từ sau thay đổi này, email đăng nhập chỉ hợp lệ khi:

```text
local-part@gmail.com
local-part@fpt.edu.vn
```

Ví dụ hợp lệ:

```text
nguyenvana@gmail.com
nhansu@fpt.edu.vn
USER@GMAIL.COM
STAFF@FPT.EDU.VN
```

Sau normalize:

```text
nguyenvana@gmail.com
nhansu@fpt.edu.vn
user@gmail.com
staff@fpt.edu.vn
```

Ví dụ không hợp lệ:

```text
nhansu@fe.edu.vn
nhansu@yahoo.com
nhansu@outlook.com
nhansu@student.fpt.edu.vn
nhansu@mail.gmail.com
nhansu@gmail.com.vn
nhansu@fake-fpt.edu.vn
nhansu@fpt.edu.vn.evil.com
nhansu+test@gmail.com
```

---

# 2. Phạm vi áp dụng

## 2.1. Modal thêm nhân sự mới

Áp dụng khi Department Leader bấm:

```text
Thêm nhân sự
```

và nhập:

```text
Họ và tên
Email đăng nhập
Số điện thoại
Giới tính
```

Trước khi gọi API tạo nhân sự:

```text
POST /api/department-leader/personnel
```

frontend phải validate email theo đúng whitelist hai domain.

## 2.2. Modal chỉnh sửa thông tin nhân sự

Áp dụng khi Department Leader:

```text
Mở View Detail
→ Chỉnh sửa thông tin nhân sự
→ Thay đổi email
→ Lưu thay đổi
```

Trước khi gọi:

```text
PUT /api/department-leader/personnel/{userId}
```

frontend phải validate email theo cùng rule với create.

## 2.3. Áp dụng cho mọi status

Không phân nhánh validation theo status.

Các trạng thái sau đều dùng cùng email validator:

```text
ACTIVE
INACTIVE
PENDING_EMAIL_CONFIRMATION
LOCKED
```

Status chỉ ảnh hưởng tới hậu quả sau khi đổi email, ví dụ:

```text
PENDING_EMAIL_CONFIRMATION:
- Token cũ bị supersede.
- Phát hành confirmation token mới.
- Gửi email xác nhận mới.

ACTIVE / INACTIVE / LOCKED:
- Đồng bộ lại login identity.
- Thu hồi session.
- Gửi email thông báo đổi email theo contract hiện tại.
```

Status không được làm thay đổi danh sách domain được phép.

---

# 3. Kết quả audit baseline cần xác minh

## 3.1. Backend source of truth

Backend đang có shared rule:

```text
backend/PEMS.Application/Accounts/Common/
AccountIdentityRules.cs
```

Baseline mong đợi:

```csharp
public static readonly IReadOnlySet<string> AllowedEmailDomains =
    new HashSet<string>(StringComparer.Ordinal)
    {
        "gmail.com",
        "fpt.edu.vn"
    };
```

Thông báo lỗi:

```text
Chỉ chấp nhận @gmail.com và @fpt.edu.vn.
```

Agent phải xác minh HEAD hiện tại vẫn đúng như vậy.

## 3.2. Backend create personnel

File:

```text
backend/PEMS.Application/DepartmentLeaderPersonnel/Commands/
CreateDepartmentPersonnel/
CreateDepartmentPersonnelCommandHandler.cs
```

Handler phải normalize và validate:

```csharp
var email = AccountIdentityRules.NormalizeEmail(request.Email);

if (AccountIdentityRules.ValidateEmail(email) is { } emailError)
{
    throw new ValidationException(emailError);
}
```

## 3.3. Backend update personnel

File:

```text
backend/PEMS.Application/DepartmentLeaderPersonnel/Commands/
UpdateDepartmentPersonnel/
UpdateDepartmentPersonnelCommandHandler.cs
```

Handler phải normalize và validate email **trước** khi xử lý status hoặc mutation:

```csharp
var newEmail =
    AccountIdentityRules.NormalizeEmail(request.Email);

if (AccountIdentityRules.ValidateEmail(newEmail)
    is { } emailError)
{
    throw new ValidationException(emailError);
}
```

## 3.4. Frontend đang có dấu hiệu drift

File:

```text
frontend/pems-react/src/features/
department-leader-personnel/validation/
personnelValidation.ts
```

Baseline cũ có thể vẫn chứa:

```ts
export const ALLOWED_EMAIL_DOMAINS = [
  'gmail.com',
  'fpt.edu.vn',
  'fe.edu.vn',
] as const;
```

và message:

```text
Email phải sử dụng một trong các tên miền:
@gmail.com, @fpt.edu.vn hoặc @fe.edu.vn.
```

Đây là drift cần loại bỏ.

---

# 4. Quy tắc email chuẩn

## 4.1. Normalize

```ts
export function normalizeEmail(value: string): string {
  return value.trim().toLowerCase();
}
```

Backend:

```csharp
public static string NormalizeEmail(string? value)
    => string.IsNullOrEmpty(value)
        ? string.Empty
        : value.Trim().ToLowerInvariant();
```

Không thực hiện các phép sửa khác.

Không tự:

```text
- Xóa dấu cộng.
- Xóa dấu chấm.
- Sửa domain sai.
- Thay @fe.edu.vn thành @fpt.edu.vn.
```

## 4.2. Whitelist domain

Frontend:

```ts
export const ALLOWED_EMAIL_DOMAINS = [
  'gmail.com',
  'fpt.edu.vn',
] as const;
```

Backend:

```csharp
AllowedEmailDomains =
{
    "gmail.com",
    "fpt.edu.vn"
};
```

## 4.3. Exact domain match

Phải so sánh chính xác:

```ts
ALLOWED_EMAIL_DOMAINS.includes(domain)
```

Không dùng:

```ts
email.endsWith('gmail.com')
email.endsWith('fpt.edu.vn')
domain.includes('fpt.edu.vn')
```

Các domain sau phải bị từ chối:

```text
mail.gmail.com
student.fpt.edu.vn
gmail.com.vn
fake-fpt.edu.vn
fpt.edu.vn.evil.com
gmail.com.evil.org
```

## 4.4. Message thống nhất

Frontend và backend phải dùng đúng:

```text
Chỉ chấp nhận @gmail.com và @fpt.edu.vn.
```

Không còn runtime text nào ghi:

```text
@fe.edu.vn
```

---

# 5. Giữ nguyên các rule email hiện có

Không thay validator đầy đủ bằng regex sơ sài.

Phải tiếp tục giữ:

```text
1. Email bắt buộc.
2. Trim khoảng trắng đầu/cuối.
3. Chuyển lowercase.
4. Tối đa 150 ký tự.
5. Local-part tối đa 64 ký tự.
6. Không cho plus addressing (+).
7. Phải có đúng một ký tự @.
8. Local-part không rỗng.
9. Local-part không bắt đầu bằng dấu chấm.
10. Local-part không kết thúc bằng dấu chấm.
11. Không có hai dấu chấm liên tiếp.
12. Local-part chỉ dùng charset hiện tại.
13. Domain phải đúng cấu trúc.
14. Domain phải exact match gmail.com hoặc fpt.edu.vn.
```

Message theo thứ tự ưu tiên:

```text
Email rỗng
→ Vui lòng nhập email.

Email quá dài
→ Email không được vượt quá 150 ký tự.

Có dấu cộng
→ Email dùng để đăng nhập không được chứa dấu cộng (+).

Local-part quá dài
→ Phần tên email trước ký tự @ không được vượt quá 64 ký tự.

Sai cấu trúc
→ Email không đúng định dạng.

Domain ngoài whitelist
→ Chỉ chấp nhận @gmail.com và @fpt.edu.vn.
```

---

# 6. Thay đổi frontend validation

## 6.1. File bắt buộc

```text
frontend/pems-react/src/features/
department-leader-personnel/validation/
personnelValidation.ts
```

## 6.2. Sửa whitelist

Từ:

```ts
export const ALLOWED_EMAIL_DOMAINS = [
  'gmail.com',
  'fpt.edu.vn',
  'fe.edu.vn',
] as const;
```

thành:

```ts
export const ALLOWED_EMAIL_DOMAINS = [
  'gmail.com',
  'fpt.edu.vn',
] as const;
```

## 6.3. Sửa message

Từ:

```ts
return 'Email phải sử dụng một trong các tên miền: @gmail.com, @fpt.edu.vn hoặc @fe.edu.vn.';
```

thành:

```ts
return 'Chỉ chấp nhận @gmail.com và @fpt.edu.vn.';
```

## 6.4. Giữ exact match

```ts
if (
  !ALLOWED_EMAIL_DOMAINS.includes(
    domain as (typeof ALLOWED_EMAIL_DOMAINS)[number],
  )
) {
  return 'Chỉ chấp nhận @gmail.com và @fpt.edu.vn.';
}
```

---

# 7. Khuyến nghị dùng shared login-email validator

Hiện có khả năng project đang tồn tại ít nhất hai validator:

```text
features/account-management/validation/
accountIdentityValidation.ts

features/department-leader-personnel/validation/
personnelValidation.ts
```

Validator account management của HO/Staff Leader đã dùng:

```text
gmail.com
fpt.edu.vn
```

Trong khi validator Department Leader từng chứa thêm:

```text
fe.edu.vn
```

Để tránh drift lặp lại, hướng khuyến nghị là tách shared module:

```text
frontend/pems-react/src/shared/validation/
loginEmailValidation.ts
```

Module shared chứa:

```ts
export const ALLOWED_LOGIN_EMAIL_DOMAINS = [
  'gmail.com',
  'fpt.edu.vn',
] as const;

export const LOGIN_EMAIL_MESSAGES = {
  required: 'Vui lòng nhập email.',
  invalidFormat: 'Email không đúng định dạng.',
  tooLong: 'Email không được vượt quá 150 ký tự.',
  localPartTooLong:
    'Phần tên email trước ký tự @ không được vượt quá 64 ký tự.',
  plusNotAllowed:
    'Email dùng để đăng nhập không được chứa dấu cộng (+).',
  domainNotAllowed:
    'Chỉ chấp nhận @gmail.com và @fpt.edu.vn.',
} as const;

export function normalizeLoginEmail(...): string;

export function validateLoginEmail(...): string | null;
```

Sau đó:

```text
accountIdentityValidation.ts
personnelValidation.ts
```

cùng import từ shared module.

Nếu phạm vi task cần tối thiểu hóa diff, có thể sửa trực tiếp `personnelValidation.ts`, nhưng phải thêm contract tests để ngăn drift.

---

# 8. Thay đổi modal “Thêm nhân sự mới”

## 8.1. File

```text
frontend/pems-react/src/features/
department-leader-personnel/components/
PersonnelFormModal.tsx
```

## 8.2. Hint email

Thay:

```text
Dùng @gmail.com, @fpt.edu.vn hoặc @fe.edu.vn.
```

bằng:

```text
Chỉ chấp nhận @gmail.com và @fpt.edu.vn.
```

## 8.3. Field-level error

Khi nhập:

```text
nhansu@fe.edu.vn
```

modal phải:

```text
- Hiển thị lỗi ngay dưới field email.
- Đổi border field sang trạng thái lỗi.
- Giữ modal mở.
- Giữ nguyên họ tên, số điện thoại, giới tính đã nhập.
- Không gọi createPersonnel API.
```

## 8.4. Revalidation khi sửa

Sau lần submit đầu tiên, khi form đã touched:

```text
- Mỗi lần thay đổi email phải validate lại.
- Khi email chuyển từ sai sang đúng, lỗi phải biến mất.
```

## 8.5. Submit payload

Chỉ gửi normalized email:

```ts
onSubmit({
  fullName: values.fullName.trim(),
  email: normalizeEmail(values.email),
  phone: values.phone.trim(),
  gender: values.gender as PersonnelGender,
});
```

---

# 9. Thay đổi modal “Chỉnh sửa thông tin nhân sự”

Modal edit phải dùng cùng validator với create.

Không viết:

```ts
if (mode === 'create') {
  validateEmail(...);
}
```

Phải luôn chạy:

```ts
const validation = validatePersonnelForm(values);
```

cho cả:

```text
mode = create
mode = edit
```

## 9.1. Áp dụng cho mọi status

Không được condition theo status:

```ts
if (personnel.status === 'ACTIVE') {
  validate...
}
```

Đúng:

```text
ACTIVE → validate
INACTIVE → validate
PENDING_EMAIL_CONFIRMATION → validate
LOCKED → validate
```

## 9.2. Hint trong edit modal

Đề xuất:

```text
Chỉ chấp nhận @gmail.com và @fpt.edu.vn.
Có thể sửa email ở mọi trạng thái tài khoản.
Đổi email sẽ thu hồi mọi phiên đăng nhập.
```

Có thể hiển thị thành hai dòng để dễ đọc.

---

# 10. Confirmation step khi đổi email

Giữ bước xác nhận hiện có khi email hợp lệ và thay đổi.

Thứ tự bắt buộc:

```text
1. Validate form.
2. Email sai → field error, dừng.
3. Email đúng nhưng không đổi → submit bình thường.
4. Email đúng và có đổi → mở confirmation step.
5. Người dùng xác nhận.
6. Gọi update API.
```

Không mở confirmation step cho email sai.

## 10.1. Pending account

Confirmation cần tiếp tục nói rõ:

```text
- Link xác nhận cũ sẽ hết hiệu lực.
- Link mới sẽ được gửi tới email mới.
- Status vẫn Chờ xác nhận email cho đến khi xác nhận.
```

## 10.2. Active/Inactive/Locked

Confirmation cần tiếp tục nói rõ:

```text
- Email đăng nhập sẽ thay đổi.
- Session hiện tại của account sẽ bị thu hồi.
- Account phải dùng email mới để đăng nhập.
- Status giữ nguyên.
```

---

# 11. Backend create validation

## 11.1. Validator

File:

```text
backend/PEMS.Application/DepartmentLeaderPersonnel/Commands/
CreateDepartmentPersonnel/
CreateDepartmentPersonnelCommandValidator.cs
```

Phải tiếp tục dùng:

```csharp
RuleFor(c => c.Email)
    .Must(v => AccountIdentityRules.ValidateEmail(v) is null)
    .WithMessage((_, v) =>
        AccountIdentityRules.ValidateEmail(v)
        ?? string.Empty);
```

## 11.2. Handler revalidation

File:

```text
CreateDepartmentPersonnelCommandHandler.cs
```

Phải giữ handler-level check.

Không chỉ tin FluentValidation pipeline.

## 11.3. Sai domain phải fail trước mutation

Với:

```text
user@fe.edu.vn
```

không được:

```text
- Tạo users row.
- Tạo confirmation token.
- Tạo audit create.
- Gửi email confirmation.
- Ghi sent_emails.
```

---

# 12. Backend update validation

## 12.1. Validator

File:

```text
backend/PEMS.Application/DepartmentLeaderPersonnel/Commands/
UpdateDepartmentPersonnel/
UpdateDepartmentPersonnelCommandValidator.cs
```

Phải dùng cùng `AccountIdentityRules.ValidateEmail`.

## 12.2. Handler revalidation

File:

```text
UpdateDepartmentPersonnelCommandHandler.cs
```

Validate email trước khi:

```text
- Lock/mutate account.
- Update users.email.
- Revoke session.
- Sync auth provider.
- Supersede token.
- Issue token mới.
- Gửi email.
- Ghi audit mutation.
```

## 12.3. Áp dụng cho mọi status

Một request email sai phải bị chặn giống nhau cho:

```text
ACTIVE
INACTIVE
PENDING_EMAIL_CONFIRMATION
LOCKED
```

---

# 13. Uniqueness check

Email đúng domain vẫn có thể bị từ chối nếu trùng.

Backend phải tiếp tục kiểm tra:

```text
users.email
user_auth_providers.provider_email
```

## Create

Không tạo account nếu email đã tồn tại.

## Update

Không cho đổi sang email thuộc account khác.

Không chuyển uniqueness check xuống frontend làm nguồn quyết định cuối cùng.

---

# 14. Xử lý backend error trên frontend

Frontend validation là UX aid.

Backend vẫn có thể trả lỗi khi:

```text
- Client stale.
- Request trực tiếp.
- Frontend bị bypass.
- Rule backend thay đổi.
```

Khi backend trả:

```text
Chỉ chấp nhận @gmail.com và @fpt.edu.vn.
```

frontend nên:

```text
- Giữ modal mở.
- Giữ dữ liệu.
- Gắn lỗi vào field email.
- Không chỉ hiển thị toast generic.
```

Khuyến nghị mở rộng `PersonnelFormModal`:

```ts
interface Props {
  open: boolean;
  mode: 'create' | 'edit';
  personnel?: PersonnelDetail | null;
  submitting: boolean;
  serverErrors?: PersonnelFormErrors;
  onClose: () => void;
  onSubmit: (...args) => void;
}
```

Hoặc quản lý lỗi server tại page rồi truyền xuống modal.

Nếu chưa thể làm field mapping trong task tối thiểu, ít nhất:

```text
- Toast phải hiển thị đúng message backend.
- Modal không được đóng khi request lỗi.
```

Nhưng Definition of Done khuyến nghị field error.

---

# 15. Rà soát màn hình legacy

Có thể vẫn tồn tại:

```text
frontend/pems-react/src/pages/dashboard/departments/
DepartmentDetailDashboard.tsx
```

Màn hình legacy từng dùng regex:

```ts
/^[^\s@]+@[^\s@]+\.[^\s@]+$/
```

Regex này chấp nhận mọi domain.

Cần audit route thực tế:

```text
- Department Leader có còn truy cập màn hình này không?
- Có entry point nào vẫn mở modal legacy không?
- Có API legacy nhận client-supplied departmentId không?
```

## Hướng khuyến nghị

Department Leader chỉ dùng:

```text
/dashboard/my-department
```

và API:

```text
/api/department-leader
```

Màn hình legacy cần:

```text
- Chặn Department Leader.
- Redirect sang /dashboard/my-department.
- Hoặc đồng bộ cùng shared validator nếu vẫn còn runtime use.
```

Không để tồn tại một entry point khác vẫn chấp nhận `@fe.edu.vn`.

---

# 16. Static scan bắt buộc

Chạy:

```bash
rg -n \
  "fe\.edu\.vn|ALLOWED_EMAIL_DOMAINS|ALLOWED_ACCOUNT_EMAIL_DOMAINS|AllowedEmailDomains|Chỉ chấp nhận @gmail\.com|Email phải sử dụng một trong các tên miền" \
  frontend backend tests docs
```

Phân loại từng hit:

```text
1. Runtime frontend.
2. Runtime backend.
3. Test.
4. Seed/demo data.
5. Documentation.
6. Historical/legacy.
```

Mục tiêu runtime:

```text
- Không còn fe.edu.vn trong login-email whitelist.
- Không còn hint hoặc error message cho phép fe.edu.vn.
```

Không xóa mù quáng email `@fe.edu.vn` trong dữ liệu lịch sử nếu chúng là test cho trường hợp invalid.

---

# 17. Frontend tests bắt buộc

## 17.1. Validator unit tests

File đề xuất:

```text
frontend/pems-react/src/features/
department-leader-personnel/validation/
personnelValidation.test.ts
```

Test:

```text
1. user@gmail.com → valid.
2. user@fpt.edu.vn → valid.
3. USER@GMAIL.COM → valid sau normalize.
4. USER@FPT.EDU.VN → valid sau normalize.
5. " user@gmail.com " → valid sau trim.
6. user@fe.edu.vn → invalid.
7. user@yahoo.com → invalid.
8. user@outlook.com → invalid.
9. user@student.fpt.edu.vn → invalid.
10. user@mail.gmail.com → invalid.
11. user@gmail.com.vn → invalid.
12. user@fpt.edu.vn.evil.com → invalid.
13. user@fake-fpt.edu.vn → invalid.
14. user+tag@gmail.com → invalid.
15. user..name@gmail.com → invalid.
16. .user@gmail.com → invalid.
17. user.@gmail.com → invalid.
18. empty email → required.
19. local-part > 64 → invalid.
20. total length > 150 → invalid.
```

## 17.2. Create modal tests

```text
1. Hint chỉ có gmail.com và fpt.edu.vn.
2. @fe.edu.vn hiện field error.
3. @fe.edu.vn không gọi onSubmit.
4. @gmail.com gọi onSubmit.
5. @fpt.edu.vn gọi onSubmit.
6. Uppercase được normalize.
7. Input sai không reset field khác.
8. Sửa thành email đúng thì lỗi biến mất.
9. submitting=true chặn double-submit.
```

## 17.3. Edit modal tests theo status

Chạy cùng matrix cho:

```text
ACTIVE
INACTIVE
PENDING_EMAIL_CONFIRMATION
LOCKED
```

Mỗi status:

```text
1. @fe.edu.vn bị từ chối.
2. @gmail.com được chấp nhận.
3. @fpt.edu.vn được chấp nhận.
4. Invalid email không mở confirmation.
5. Valid changed email mở confirmation.
6. Unchanged email không mở confirmation.
7. Status không làm thay đổi domain rule.
```

---

# 18. Backend tests bắt buộc

## 18.1. Create personnel

```text
1. gmail.com accepted.
2. fpt.edu.vn accepted.
3. uppercase domain accepted sau normalize.
4. fe.edu.vn rejected.
5. yahoo.com rejected.
6. subdomain rejected.
7. look-alike domain rejected.
8. plus addressing rejected.
9. Invalid domain không tạo user.
10. Invalid domain không tạo confirmation.
11. Invalid domain không gửi email.
12. Invalid domain không tạo audit row.
13. Direct API không bypass.
```

## 18.2. Update personnel cho mọi status

Với mỗi:

```text
ACTIVE
INACTIVE
PENDING_EMAIL_CONFIRMATION
LOCKED
```

test:

```text
1. Update sang fe.edu.vn bị từ chối.
2. users.email giữ nguyên.
3. users.status giữ nguyên.
4. Không revoke session.
5. Không sửa auth providers.
6. Không supersede confirmation token.
7. Không tạo token mới.
8. Không gửi email.
9. Không ghi audit mutation.
```

Positive tests:

```text
- gmail.com accepted.
- fpt.edu.vn accepted.
- uppercase normalized.
```

---

# 19. Integration tests

Thêm HTTP-level tests:

```text
POST /api/department-leader/personnel
PUT /api/department-leader/personnel/{userId}
```

Expected:

## Invalid domain

```text
- HTTP validation response theo contract hiện tại.
- Error message đúng.
- DB không thay đổi.
- Không có sent_emails mới.
```

## Valid domain

```text
- Request thành công nếu các rule khác hợp lệ.
- Email được normalized.
- Các side effect hiện có vẫn hoạt động.
```

---

# 20. Có cần sửa SQL không?

Không cần.

Yêu cầu này không liên quan:

```text
- email_templates.
- Database schema.
- Seed email notification.
- Migration.
```

Không chạy:

```sql
ALTER TABLE ...
CREATE TABLE ...
INSERT INTO email_templates ...
UPDATE email_templates ...
```

Chỉ cần sửa:

```text
- Frontend whitelist.
- Frontend hint/error.
- Shared validation nếu áp dụng.
- Tests.
- Có thể dọn legacy entry point.
```

---

# 21. File dự kiến thay đổi

## Frontend bắt buộc

```text
frontend/pems-react/src/features/
department-leader-personnel/validation/
personnelValidation.ts

frontend/pems-react/src/features/
department-leader-personnel/components/
PersonnelFormModal.tsx
```

## Frontend tests

```text
frontend/pems-react/src/features/
department-leader-personnel/validation/
personnelValidation.test.ts

Frontend test cho PersonnelFormModal
```

## Backend tests

```text
tests/PEMS.UnitTests/DepartmentLeaderPersonnel/
CreateDepartmentPersonnelCommandTests.cs

tests/PEMS.UnitTests/DepartmentLeaderPersonnel/
UpdateDepartmentPersonnelCommandTests.cs
```

## Có thể cập nhật

```text
backend/PEMS.Application/Accounts/Common/
AccountIdentityRules.cs
```

Chỉ cập nhật comment nếu comment vẫn nói rule chỉ dùng cho HO trong khi Department Leader cũng dùng.

## Legacy audit

```text
frontend/pems-react/src/pages/dashboard/departments/
DepartmentDetailDashboard.tsx

frontend/pems-react/src/features/department-management/api/
departmentManagementApi.ts
```

---

# 22. Preflight bắt buộc

```bash
git status --short --branch
git branch --show-current
git rev-parse HEAD
git log -10 --oneline --decorate
git stash list
git diff --check
```

Điều kiện:

```text
- Branch phải là Duy-Iter1.
- Không chuyển sang Dev.
- Không tạo branch mới.
- Không reset/rebase/clean.
- Không ghi đè WIP.
- Không git add .
```

Nếu branch không đúng hoặc có WIP ngoài task chưa xác định:

```text
Dừng trước khi sửa và báo cáo.
```

---

# 23. Build và test gate

## Backend

```bash
dotnet build
```

```bash
dotnet test tests/PEMS.UnitTests/PEMS.UnitTests.csproj
```

```bash
dotnet test tests/PEMS.IntegrationTests/PEMS.IntegrationTests.csproj
```

## Frontend

```bash
cd frontend/pems-react
npm run type-check
npm run test -- --run
npm run build
```

Dùng đúng script thật trong `package.json`.

## Static

```bash
git diff --check
```

---

# 24. Không được làm

```text
- Không chỉ sửa hint mà bỏ validator.
- Không chỉ sửa frontend mà bỏ backend tests.
- Không dùng endsWith cho domain.
- Không dùng includes cho domain.
- Không giữ fe.edu.vn trong runtime whitelist.
- Không condition validation theo status.
- Không bỏ handler revalidation.
- Không bỏ FluentValidation.
- Không tự sửa domain sai.
- Không đóng modal khi validation lỗi.
- Không reset dữ liệu form khi email lỗi.
- Không mở confirmation cho email sai.
- Không sửa SQL.
- Không đổi schema.
- Không chuyển branch.
- Không reset WIP.
```

---

# 25. Definition of Done

```text
[ ] Branch = Duy-Iter1.
[ ] WIP được bảo toàn.
[ ] Create modal chỉ chấp nhận gmail.com và fpt.edu.vn.
[ ] Edit modal chỉ chấp nhận gmail.com và fpt.edu.vn.
[ ] Edit validation áp dụng cho ACTIVE.
[ ] Edit validation áp dụng cho INACTIVE.
[ ] Edit validation áp dụng cho PENDING_EMAIL_CONFIRMATION.
[ ] Edit validation áp dụng cho LOCKED.
[ ] fe.edu.vn bị loại khỏi frontend whitelist.
[ ] Hint không còn nhắc fe.edu.vn.
[ ] Error message đúng: Chỉ chấp nhận @gmail.com và @fpt.edu.vn.
[ ] Domain dùng exact match.
[ ] Subdomain bị từ chối.
[ ] Look-alike domain bị từ chối.
[ ] Email normalize trim + lowercase.
[ ] Invalid email không gọi API.
[ ] Invalid email không mở confirmation.
[ ] Backend vẫn là source of truth.
[ ] Direct API với fe.edu.vn bị từ chối.
[ ] Domain sai không tạo user.
[ ] Domain sai không update user.
[ ] Domain sai không revoke session.
[ ] Domain sai không thay token.
[ ] Domain sai không gửi email.
[ ] Domain sai không ghi audit mutation.
[ ] Legacy entry point được chặn, redirect hoặc đồng bộ.
[ ] Không sửa SQL.
[ ] Frontend tests xanh.
[ ] Backend unit tests xanh.
[ ] Integration tests xanh.
[ ] Frontend type-check xanh.
[ ] Frontend build xanh.
[ ] Backend build xanh.
[ ] git diff --check xanh.
```

---

# 26. Mẫu báo cáo cuối cùng Agent phải trả

```markdown
# Kết quả triển khai Department Leader email validation

## 1. Preflight
- Branch:
- HEAD:
- Working tree:
- WIP được bảo toàn:
- git diff --check:

## 2. Audit
- Backend allowed domains:
- Frontend allowed domains trước sửa:
- Runtime hint trước sửa:
- Legacy entry point:
- SQL có cần sửa không:

## 3. File đã sửa

### Frontend
- ...

### Backend/tests
- ...

### Legacy
- ...

## 4. Validation contract
- Normalize:
- Max length:
- Plus addressing:
- Exact domain matching:
- Allowed domains:
- Error message:

## 5. Modal behavior
- Create:
- Edit:
- ACTIVE:
- INACTIVE:
- PENDING:
- LOCKED:
- Confirmation step:

## 6. Backend protection
- Validator:
- Handler revalidation:
- Direct API:
- Uniqueness:
- Mutation prevention:

## 7. Tests
- Frontend validator:
- Create modal:
- Edit modal:
- Backend unit:
- Integration:
- Type-check:
- Frontend build:
- Backend build:

## 8. Kết luận
- PASS / FAIL / PARTIAL
- Blocker nếu có:
```

---

# 27. Lệnh giao việc ngắn gọn

```text
Đọc toàn bộ file này trước khi sửa.

Tiếp tục làm việc trên nhánh Duy-Iter1. Không chuyển sang Dev, không tạo nhánh mới và không reset WIP.

Tại trang /dashboard/my-department của DEPARTMENT/LEADER:

- Modal Thêm nhân sự mới và modal Chỉnh sửa thông tin nhân sự chỉ được chấp nhận email @gmail.com hoặc @fpt.edu.vn.
- Loại bỏ @fe.edu.vn khỏi frontend whitelist và mọi runtime hint/error.
- Dùng exact domain match, không endsWith/includes.
- Giữ toàn bộ rule email hiện có: required, normalize, max length, no plus, local-part validation.
- Edit validation áp dụng giống nhau cho ACTIVE, INACTIVE, PENDING_EMAIL_CONFIRMATION và LOCKED.
- Invalid email phải hiện field error, giữ modal mở, không gọi API và không mở confirmation step.
- Backend AccountIdentityRules vẫn là source of truth; giữ validator + handler revalidation.
- Bổ sung frontend/backend/integration tests đầy đủ.
- Audit màn hình legacy để không còn entry point cho phép domain khác.
- Không sửa SQL hoặc schema.

Chạy build/test đầy đủ và trả báo cáo theo mẫu cuối file.
```
