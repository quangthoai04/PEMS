# PEMS — IMPLEMENTATION SPEC
## Chỉnh sửa email của tài khoản `PENDING_EMAIL_CONFIRMATION` phải gửi lại email xác nhận kích hoạt

> **Mục đích tài liệu:**  
> Đây là đặc tả độc lập cho yêu cầu: khi HO chỉnh sửa email của một tài khoản đang ở trạng thái `PENDING_EMAIL_CONFIRMATION`, hệ thống phải gửi tới email mới **đúng mẫu email xác nhận kích hoạt tài khoản**, giống luồng tạo tài khoản mới.
>
> **Repository:** `quangthoai04/PEMS`  
> **Nhánh làm việc bắt buộc:** nhánh hiện tại `Duy-Iter1`  
> **Nguồn code chuẩn:** HEAD hiện tại của `Duy-Iter1` tại thời điểm Agent bắt đầu  
> **Không chuyển sang `Dev`, không tạo nhánh mới, không reset/rebase/xóa WIP**
>
> **Actor chính:** HO trong trang Quản lý tài khoản.

---

# 1. Vấn đề hiện tại

Đối với tài khoản có:

```text
users.status = PENDING_EMAIL_CONFIRMATION
```

người dùng chưa thể đăng nhập vào hệ thống cho tới khi xác nhận quyền sở hữu email bằng liên kết kích hoạt.

Hiện tại, khi HO:

```text
Mở View Account Detail
→ Chỉnh sửa thông tin
→ Thay đổi email
→ Bấm Cập nhật
```

frontend đang gọi flow cập nhật thông tin chung:

```text
POST /api/accounts/updatebasicaccountinfo
```

Backend của flow này hiện:

```text
- Cập nhật users.email.
- Đồng bộ một phần auth provider.
- Xóa EmailVerifiedAt.
- Thu hồi session.
- Gửi thông báo tới email cũ.
- Gửi thông báo “email đã thay đổi” tới email mới.
```

Email gửi tới email mới chỉ là email thông báo thay đổi địa chỉ đăng nhập.

Email này không có:

```text
- Token xác nhận mới.
- Nút “Xác nhận email & kích hoạt tài khoản”.
- Link /confirm-email?token=...
- Khả năng chuyển account từ PENDING_EMAIL_CONFIRMATION sang ACTIVE.
```

Hậu quả:

```text
- Account vẫn ở trạng thái PENDING_EMAIL_CONFIRMATION.
- Token cũ gắn với email cũ.
- Email mới không nhận được liên kết xác nhận.
- Người nhận không có cách kích hoạt account.
- Account không thể đăng nhập.
```

---

# 2. Mục tiêu cần đạt được

Khi HO chỉnh sửa email của một tài khoản đang pending:

```text
HO mở chi tiết account
→ chỉnh sửa email
→ bấm Cập nhật
→ xác nhận thao tác
→ backend xác nhận account vẫn pending
→ cập nhật email mới
→ vô hiệu hóa confirmation token cũ
→ phát hành confirmation token mới
→ commit dữ liệu
→ gửi ACCOUNT_EMAIL_CONFIRMATION tới email mới
→ người nhận bấm “Xác nhận email & kích hoạt tài khoản”
→ account chuyển ACTIVE
```

Email gửi tới địa chỉ mới phải giống luồng tạo account mới về:

```text
- Template code.
- Subject.
- Nội dung.
- Nút xác nhận.
- Action URL.
- Thời hạn token.
- Các biến fullName, roleName, campusName, expiresInHours.
- Recipient policy.
- Security policy.
```

Không tạo một mẫu email xác nhận khác.

---

# 3. Quyết định kiến trúc

## 3.1. Không sửa chắp vá template thông báo đổi email

Không thêm nút xác nhận vào:

```text
ACCOUNT_EMAIL_CHANGED_NEW_NOTICE
```

Lý do:

```text
- Template này còn được dùng cho account ACTIVE.
- Account ACTIVE đổi email là nghiệp vụ khác.
- Pending account sửa email phải tiếp tục flow xác nhận quyền sở hữu.
- Trộn token xác nhận vào notice thông thường làm sai contract template.
```

## 3.2. Dùng flow chuyên biệt cho pending account

Backend hiện có flow chuyên biệt:

```http
POST /api/accounts/edit-pending-email
```

Flow này cần được dùng khi:

```text
status = PENDING_EMAIL_CONFIRMATION
AND
email mới khác email hiện tại
```

Frontend phải phân nhánh:

```text
Pending + email thay đổi
→ edit-pending-email

Pending + email không đổi
→ updatebasicaccountinfo để cập nhật tên nếu cần

Non-pending
→ updatebasicaccountinfo theo logic hiện tại
```

## 3.3. Không tạo endpoint mới

Không tạo:

```text
update-pending-basic-info-v2
resend-after-email-edit
edit-email-and-confirm
```

nếu `edit-pending-email` hiện có đã đúng hướng nghiệp vụ.

---

# 4. Luồng frontend cần thay đổi

## 4.1. File chính

```text
frontend/pems-react/src/pages/dashboard/accounts/AccountManagement.tsx
```

## 4.2. Xác định status từ detail response

Phải lấy status từ API chi tiết account.

Ví dụ:

```ts
const normalizedStatus = String(
  selectedAccount.rawStatus ?? ''
).trim().toUpperCase();

const isPendingEmailConfirmation =
  normalizedStatus === 'PENDING_EMAIL_CONFIRMATION';
```

Không chỉ dùng:

```text
selectedAccount.status
```

nếu đó là status đã được map từ row list.

## 4.3. Xác định email có thay đổi

```ts
const oldEmail = normalizeAccountEmail(
  selectedAccount.email ?? '',
);

const newEmail = normalizeAccountEmail(
  roleEditForm.email,
);

const emailChanged = oldEmail !== newEmail;
```

## 4.4. Phân nhánh submit

Pseudo-code:

```ts
if (isPendingEmailConfirmation && emailChanged) {
  await accountManagementApi.editPendingAccountEmail({
    userId,
    newEmail,
    fullName: normalizedFullName,
  });
} else {
  await accountManagementApi.updateBasicAccountInfo({
    userId,
    fullName: normalizedFullName,
    email: newEmail,
  });
}
```

Bảng hành vi:

| Status | Email thay đổi | API |
|---|---:|---|
| `PENDING_EMAIL_CONFIRMATION` | Có | `edit-pending-email` |
| `PENDING_EMAIL_CONFIRMATION` | Không | `updatebasicaccountinfo` |
| `ACTIVE` | Có/không | `updatebasicaccountinfo` |
| `INACTIVE` | Có/không | Giữ rule hiện tại |
| `LOCKED` | Có/không | Backend từ chối như hiện tại |

---

# 5. Trường hợp chỉnh sửa cả họ tên và email

Modal hiện cho phép sửa đồng thời:

```text
Họ và tên
Email
```

Không nên gọi hai API tách biệt:

```text
updatebasicaccountinfo
→ edit-pending-email
```

vì có thể xảy ra partial update.

Ví dụ:

```text
Tên cập nhật thành công
→ email cập nhật thất bại
```

hoặc:

```text
Email cập nhật thành công
→ tên cập nhật thất bại
```

## 5.1. Giải pháp khuyến nghị

Mở rộng command:

```csharp
public sealed class EditPendingAccountEmailCommand
{
    public ulong UserId { get; set; }

    public string NewEmail { get; set; } = default!;

    public string? FullName { get; set; }
}
```

Frontend request:

```ts
export interface EditPendingAccountEmailRequest {
  userId: string | number;
  newEmail: string;
  fullName?: string;
}
```

Handler cập nhật cả:

```text
users.full_name
users.email
```

trong cùng transaction.

## 5.2. Validate full name

Nếu có `FullName`:

```csharp
var newFullName =
    AccountIdentityRules.NormalizeFullName(request.FullName);

if (AccountIdentityRules.ValidateFullName(newFullName)
    is { } fullNameError)
{
    throw new ValidationException(fullNameError);
}
```

Sau đó:

```csharp
user.FullName = newFullName;
```

Email xác nhận phải dùng họ tên mới.

---

# 6. Frontend endpoint

## 6.1. File

```text
frontend/pems-react/src/shared/api/endpoints.ts
```

Thêm trong:

```ts
API_ENDPOINTS.accounts
```

```ts
editPendingEmail:
  '/accounts/edit-pending-email',
```

Không hardcode URL trong component.

---

# 7. Frontend types

## 7.1. File

```text
frontend/pems-react/src/features/account-management/
types/accountManagement.types.ts
```

Thêm:

```ts
export interface EditPendingAccountEmailRequest {
  userId: string | number;
  newEmail: string;
  fullName?: string;
}

export interface EditPendingAccountEmailResponse {
  success: boolean;
  email: string;
  emailNotificationStatus:
    | 'SENT'
    | 'SKIPPED'
    | 'FAILED'
    | string;
  message: string;
}
```

Không dùng `any`.

---

# 8. Frontend API wrapper

## 8.1. File

```text
frontend/pems-react/src/features/account-management/
api/accountManagementApi.ts
```

Thêm method:

```ts
async editPendingAccountEmail(
  payload: EditPendingAccountEmailRequest,
): Promise<EditPendingAccountEmailResponse> {
  const { data } =
    await httpClient.post<EditPendingAccountEmailResponse>(
      API_ENDPOINTS.accounts.editPendingEmail,
      payload,
    );

  return data;
}
```

Không gọi `httpClient` trực tiếp trong JSX.

---

# 9. Confirmation dialog frontend

Khi pending account thay đổi email, dùng nội dung confirmation riêng.

## 9.1. Tiêu đề

```text
Xác nhận thay đổi email chờ kích hoạt
```

## 9.2. Nội dung

```text
Email tài khoản sẽ được thay đổi:

Từ: <email cũ>
Sang: <email mới>

Liên kết xác nhận đã gửi tới email cũ sẽ không còn hiệu lực.

Hệ thống sẽ phát hành một liên kết xác nhận mới và gửi tới email mới.

Tài khoản chỉ được kích hoạt sau khi người nhận hoàn tất xác nhận email.
```

## 9.3. Nút

```text
Hủy
Cập nhật và gửi email xác nhận
```

## 9.4. Trong lúc submit

```text
- Disable nút xác nhận.
- Disable thao tác có thể submit trùng.
- Hiển thị “Đang cập nhật...”.
- Không cho double-click.
- Không đóng dialog bằng backdrop hoặc Escape.
```

---

# 10. Backend command cần cập nhật

## 10.1. File

```text
backend/PEMS.Application/Accounts/Commands/
EditPendingAccountEmail/EditPendingAccountEmailCommand.cs
```

Mở rộng command:

```csharp
public sealed class EditPendingAccountEmailCommand
    : IRequest<EditPendingAccountEmailResponse>
{
    public ulong UserId { get; set; }

    public string NewEmail { get; set; } = default!;

    public string? FullName { get; set; }
}
```

Response giữ:

```csharp
public sealed class EditPendingAccountEmailResponse
{
    public bool Success { get; init; }

    public string Email { get; init; } = default!;

    public string EmailNotificationStatus { get; init; }
        = default!;

    public string Message { get; init; } = default!;
}
```

---

# 11. Backend handler cần củng cố

## 11.1. File

```text
backend/PEMS.Application/Accounts/Commands/
EditPendingAccountEmail/EditPendingAccountEmailCommandHandler.cs
```

## 11.2. Load đầy đủ dữ liệu

```csharp
var user = await _db.Users
    .Include(u => u.Role)
    .Include(u => u.AuthProviders)
    .FirstOrDefaultAsync(
        u => u.UserId == request.UserId,
        cancellationToken)
    ?? throw new NotFoundException(
        "Tài khoản không tồn tại.");
```

## 11.3. Authorization

Giữ:

```csharp
PendingAccountAuthorization.EnsureCanManagePending(
    _currentUser,
    user);
```

Backend là nguồn kiểm tra cuối cùng.

## 11.4. Kiểm tra status

```csharp
if (user.Status != UserStatuses.PendingEmailConfirmation)
{
    throw new BusinessRuleException(
        "Chỉ có thể sửa email của tài khoản đang chờ xác nhận.",
        "ACCOUNT_NOT_PENDING");
}
```

## 11.5. Validate email mới

```csharp
var newEmail =
    AccountIdentityRules.NormalizeEmail(request.NewEmail);

if (AccountIdentityRules.ValidateEmail(newEmail)
    is { } emailError)
{
    throw new ValidationException(emailError);
}
```

## 11.6. Email không đổi

```csharp
if (string.Equals(
    newEmail,
    user.Email,
    StringComparison.OrdinalIgnoreCase))
{
    throw new BusinessRuleException(
        "Email mới trùng với email hiện tại.",
        "EMAIL_UNCHANGED");
}
```

## 11.7. Email uniqueness

```csharp
var emailTaken = await _db.Users
    .AsNoTracking()
    .AnyAsync(
        u => u.Email == newEmail
          && u.UserId != user.UserId,
        cancellationToken);

if (emailTaken)
{
    throw new ConflictException(
        AccountIdentityRules.EmailAlreadyUsedMessage,
        AccountErrorCodes.EmailAlreadyExists);
}
```

---

# 12. Đồng bộ authentication provider

Khi pending account thay email:

## 12.1. Local password provider

```csharp
provider.ProviderEmail = newEmail;
```

## 12.2. Google SSO / FEID

Xóa hoặc unlink provider gắn với external identity cũ theo cùng policy đang dùng ở flow update basic info.

Ví dụ:

```csharp
var externalProviders = user.AuthProviders
    .Where(p =>
        p.ProviderType == ProviderTypes.GoogleSso
        || p.ProviderType == ProviderTypes.FeId)
    .ToList();

_db.UserAuthProviders.RemoveRange(externalProviders);
```

## 12.3. Email verification

```csharp
user.EmailVerifiedAt = null;
```

Pending account không được coi là đã xác minh email.

---

# 13. Phát hành confirmation token mới

Sau khi cập nhật email:

```csharp
var rawToken =
    await _confirmations.IssuePendingAsync(
        user.UserId,
        newEmail,
        isResend: false,
        cancellationToken);
```

Kết quả bắt buộc:

```text
- Token cũ chuyển SUPERSEDED.
- Token cũ không còn xác nhận được.
- Token mới gắn với email mới.
- Chỉ lưu token hash.
- Token mới có status PENDING.
- Chỉ một token PENDING hợp lệ.
- expires_at mới được tính lại.
- resend_count không tăng như resend thủ công.
```

---

# 14. Transaction và thứ tự thực hiện

Các thay đổi sau phải atomic:

```text
users.full_name
users.email
users.email_verified_at
auth_providers
confirmation cũ → SUPERSEDED
confirmation mới → PENDING
audit log
```

Thứ tự:

```text
1. Authorization.
2. Validate status.
3. Validate full name.
4. Validate email.
5. Check email uniqueness.
6. Update user.
7. Update/unlink auth providers.
8. IssuePendingAsync.
9. Add audit log.
10. SaveChanges.
11. Commit transaction.
12. Gửi email sau commit.
```

## 14.1. Database lỗi

Nếu DB thất bại:

```text
- Email cũ giữ nguyên.
- Full name cũ giữ nguyên.
- Token cũ không bị supersede.
- Token mới không tồn tại.
- Không gửi email.
```

## 14.2. Email gửi thất bại sau commit

Nếu provider gửi email lỗi:

```text
- Email mới vẫn được giữ.
- Token mới vẫn tồn tại.
- Account vẫn PENDING_EMAIL_CONFIRMATION.
- Response trả FAILED.
- HO có thể dùng “Gửi lại email xác nhận”.
```

Không rollback dữ liệu account chỉ vì email delivery thất bại.

---

# 15. Mẫu email gửi tới email mới

Bắt buộc dùng:

```csharp
SystemEmailTemplates.AccountEmailConfirmation
```

Không dùng:

```csharp
SystemEmailTemplates.AccountEmailChangedNewNotice
```

Không tạo template mới.

Không copy HTML email vào code.

---

# 16. Contract email phải giống luồng tạo account

Gửi:

```csharp
var result = await _dispatcher.SendAsync(
    new SystemEmailRequest(
        SystemEmailTemplates.AccountEmailConfirmation,
        new EmailRecipient(
            newEmail,
            user.FullName),
        await AccountEmailVariables.ForConfirmationAsync(
            _db,
            user.FullName,
            roleCode,
            user.SubRole,
            user.PrimaryCampusId,
            _confirmations.ExpiryHours,
            cancellationToken),
        TrustedBlocks:
            AccountEmailVariables.ConfirmationBlocks(
                _confirmations.BuildConfirmUrl(rawToken)),
        RelatedType: "User",
        RelatedId: user.UserId,
        SentBy: _currentUser.UserId),
    cancellationToken);
```

Email phải có:

```text
- Tiêu đề xác nhận email.
- Nội dung account được khởi tạo/chờ xác nhận.
- Role name.
- Campus name.
- Thời hạn xác nhận.
- Nút “Xác nhận email & kích hoạt tài khoản”.
- Fallback link.
- Token mới.
```

---

# 17. Email gửi tới địa chỉ cũ

Có thể tiếp tục gửi:

```csharp
SystemEmailTemplates.AccountPendingEmailChangedOldNotice
```

Email cũ chỉ nhận notice trung lập.

Không chứa:

```text
- Họ tên.
- Email mới.
- Role.
- Campus.
- Token.
- Link xác nhận.
```

Lý do:

```text
Email cũ có thể là địa chỉ nhập nhầm của người không liên quan.
```

Email cũ là best-effort.

Thất bại gửi notice cũ không được làm hỏng email xác nhận mới.

---

# 18. Audit log

Dùng action riêng:

```text
EDIT_PENDING_ACCOUNT_EMAIL
```

Nội dung gợi ý:

```json
{
  "oldEmail": "old@example.com",
  "newEmail": "new@example.com",
  "oldFullName": "Tên cũ",
  "newFullName": "Tên mới",
  "oldConfirmationSuperseded": true,
  "newConfirmationIssued": true
}
```

Không ghi:

```text
- Raw token.
- Full confirmation URL.
- Email body.
- Token plaintext.
```

---

# 19. Response API

Response phải phản ánh email xác nhận gửi tới email mới:

```ts
{
  success: boolean;
  email: string;
  emailNotificationStatus:
    | 'SENT'
    | 'SKIPPED'
    | 'FAILED'
    | string;
  message: string;
}
```

`emailNotificationStatus` không được đại diện cho notice gửi tới email cũ.

Email quyết định khả năng kích hoạt là email gửi tới địa chỉ mới.

---

# 20. Hiển thị kết quả trên frontend

## 20.1. SENT

```text
Đã cập nhật email và gửi liên kết xác nhận tới <email mới>.
Tài khoản sẽ được kích hoạt sau khi người nhận hoàn tất xác nhận.
```

## 20.2. SKIPPED

```text
Đã cập nhật email, nhưng email xác nhận không được gửi trong môi trường hiện tại.
Bạn có thể sử dụng chức năng “Gửi lại email xác nhận”.
```

Không báo thành công hoàn toàn.

## 20.3. FAILED

```text
Đã cập nhật email nhưng không thể gửi email xác nhận.
Tài khoản vẫn ở trạng thái chờ xác nhận email.
Vui lòng sử dụng chức năng “Gửi lại email xác nhận”.
```

## 20.4. Unknown

```text
Đã cập nhật email nhưng chưa xác định được trạng thái gửi email xác nhận.
```

---

# 21. Xử lý lỗi frontend

## ACCOUNT_NOT_PENDING

```text
Tài khoản không còn ở trạng thái chờ xác nhận email.
```

Sau đó:

```text
- Refetch detail.
- Refetch list.
- Chuyển về logic update bình thường nếu phù hợp.
```

## EMAIL_UNCHANGED

Hiển thị dưới field email:

```text
Email mới trùng với email hiện tại.
```

## EMAIL_ALREADY_EXISTS

Hiển thị dưới field email.

## Email validation error

Hiển thị dưới field email.

## 403

```text
Bạn không có quyền chỉnh sửa email của tài khoản này.
```

## 404

```text
Tài khoản không tồn tại hoặc không còn quyền truy cập.
```

## Network/server error

```text
Không thể cập nhật email tài khoản. Vui lòng thử lại sau.
```

---

# 22. Cấu hình confirm URL

Không hardcode:

```text
http://localhost:3000
http://localhost:5173
https://pems-fpt.site
```

Phải dùng:

```csharp
_confirmations.BuildConfirmUrl(rawToken)
```

Nguồn URL:

```text
App:FrontendBaseUrl
```

Route:

```text
/confirm-email?token=...
```

Môi trường:

```text
Development → URL frontend local đúng port.
Review → domain review.
Production → https://pems-fpt.site.
```

Không để email production chứa link localhost.

---

# 23. File dự kiến thay đổi

## Frontend

```text
frontend/pems-react/src/shared/api/endpoints.ts

frontend/pems-react/src/features/account-management/
types/accountManagement.types.ts

frontend/pems-react/src/features/account-management/
api/accountManagementApi.ts

frontend/pems-react/src/pages/dashboard/accounts/
AccountManagement.tsx
```

## Backend

```text
backend/PEMS.Application/Accounts/Commands/
EditPendingAccountEmail/EditPendingAccountEmailCommand.cs

backend/PEMS.Application/Accounts/Commands/
EditPendingAccountEmail/EditPendingAccountEmailCommandHandler.cs
```

## Backend cần audit nhưng có thể không cần sửa

```text
backend/PEMS.Api/Controllers/AccountsController.cs

backend/PEMS.Infrastructure/Email/
AccountEmailConfirmationService.cs

backend/PEMS.Application/Emails/Common/
SystemEmailTemplates.cs

backend/PEMS.Application/Accounts/Commands/
CreateAccount/CreateAccountCommandHandler.cs
```

## Tests

```text
tests/PEMS.UnitTests/Accounts/EmailConfirmation/
ResendAndEditPendingEmailTests.cs
```

Bổ sung frontend tests theo cấu trúc hiện có.

---

# 24. Backend test bắt buộc

```text
1. Pending account đổi email thành công.
2. Account vẫn PENDING_EMAIL_CONFIRMATION.
3. Email mới được lưu.
4. Full name được cập nhật cùng transaction nếu có.
5. Local provider email được cập nhật.
6. External provider cũ được unlink.
7. EmailVerifiedAt = null.
8. Confirmation cũ → SUPERSEDED.
9. Confirmation mới target email đúng.
10. Token mới chỉ lưu hash.
11. Chỉ một confirmation PENDING.
12. Token cũ không confirm được.
13. Token mới confirm được.
14. Template = ACCOUNT_EMAIL_CONFIRMATION.
15. Email có actionBlock xác nhận.
16. Biến role/campus/expiry giống create flow.
17. Notice trung lập gửi tới email cũ.
18. EMAIL_UNCHANGED hoạt động.
19. Duplicate email bị conflict.
20. ACTIVE → ACCOUNT_NOT_PENDING.
21. INACTIVE → ACCOUNT_NOT_PENDING.
22. LOCKED bị từ chối.
23. Actor ngoài scope bị 403.
24. Email provider lỗi không rollback DB.
25. Không log token.
26. Không log full confirmation URL.
```

---

# 25. Frontend test bắt buộc

```text
1. Pending + email thay đổi → gọi editPendingAccountEmail.
2. Pending + email không đổi → gọi updateBasicAccountInfo.
3. Active + email thay đổi → gọi updateBasicAccountInfo.
4. Pending + đổi tên và email → gửi cùng payload.
5. Confirmation dialog hiển thị email cũ/mới.
6. Dialog nói rõ link cũ mất hiệu lực.
7. Double-click → một request.
8. SENT → message đúng.
9. SKIPPED → không báo đã gửi thành công.
10. FAILED → nói rõ DB đã cập nhật nhưng email chưa gửi.
11. ACCOUNT_NOT_PENDING → refetch detail/list.
12. EMAIL_UNCHANGED → lỗi dưới field email.
13. Duplicate/domain error → lỗi dưới field email.
14. Sau success refetch list.
15. Account vẫn hiển thị Chờ xác nhận email.
16. Nút Gửi lại email xác nhận vẫn có thể dùng nếu delivery lỗi.
```

---

# 26. Manual verification

## 26.1. Pending account đổi email

1. Đăng nhập HO.
2. Mở Quản lý tài khoản.
3. Mở detail account `PENDING_EMAIL_CONFIRMATION`.
4. Bấm Chỉnh sửa.
5. Đổi email.
6. Bấm Cập nhật.
7. Kiểm tra confirmation dialog.
8. Xác nhận.
9. Kiểm tra DB:
   - users.email = email mới;
   - users.status vẫn pending;
   - confirmation cũ superseded;
   - confirmation mới pending.
10. Kiểm tra email mới:
   - đúng template xác nhận;
   - có nút xác nhận;
   - có fallback link;
   - link dùng token mới.
11. Bấm link mới.
12. Kiểm tra account chuyển ACTIVE.
13. Đăng nhập bằng email mới.

## 26.2. Token cũ

1. Giữ lại link cũ.
2. Sau khi đổi email, mở link cũ.
3. Expected:
   - không kích hoạt được;
   - trả lỗi stable.
4. Mở link mới.
5. Expected:
   - xác nhận thành công.

## 26.3. Email delivery failed

Giả lập provider fail.

Expected:

```text
- DB vẫn lưu email mới.
- Account vẫn pending.
- Confirmation mới tồn tại.
- Frontend báo FAILED.
- HO có thể dùng resend.
```

## 26.4. Production URL

Gửi email trong môi trường deploy.

Expected:

```text
Không chứa localhost.
```

---

# 27. Preflight bắt buộc

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
- Không reset.
- Không rebase.
- Không clean.
- Không ghi đè WIP.
- Không git add .
```

Nếu branch không đúng hoặc WIP ngoài task không xác định được:

```text
Dừng và báo cáo.
```

---

# 28. Search/audit bắt buộc

```bash
rg -n \
  "UpdateBasicAccountInfo|EditPendingAccountEmail|edit-pending-email|ACCOUNT_EMAIL_CONFIRMATION|ACCOUNT_EMAIL_CHANGED_NEW_NOTICE" \
  frontend backend tests
```

```bash
rg -n \
  "PENDING_EMAIL_CONFIRMATION|IssuePendingAsync|BuildConfirmUrl|EmailVerifiedAt|AuthProviders" \
  backend frontend tests
```

Phân loại từng hit:

```text
- Runtime frontend.
- Runtime backend.
- Test.
- Email registry.
- Template seed.
- Legacy.
- Docs.
```

Không thay thế hàng loạt mù quáng.

---

# 29. Build và test gate

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

# 30. Không được làm

```text
- Không thêm nút xác nhận vào ACCOUNT_EMAIL_CHANGED_NEW_NOTICE.
- Không tạo template xác nhận mới.
- Không tạo endpoint thứ ba.
- Không gọi hai API riêng cho tên và email.
- Không gửi email trước commit.
- Không rollback DB khi email delivery fail.
- Không tự chuyển account sang ACTIVE.
- Không giữ token cũ còn hiệu lực.
- Không lưu raw token.
- Không log full URL.
- Không hardcode domain.
- Không tạo user mới.
- Không đổi role/sub-role/campus/department.
- Không sửa schema nếu không cần.
- Không chuyển branch.
- Không reset WIP.
```

---

# 31. Definition of Done

```text
[ ] Branch = Duy-Iter1.
[ ] WIP được bảo toàn.
[ ] Pending email edit dùng edit-pending-email.
[ ] Pending email edit không dùng updatebasicaccountinfo cho phần email.
[ ] Email mới nhận ACCOUNT_EMAIL_CONFIRMATION.
[ ] Email có nút Xác nhận email & kích hoạt tài khoản.
[ ] Email có token mới.
[ ] Token cũ mất hiệu lực.
[ ] Chỉ một token PENDING.
[ ] Account vẫn PENDING_EMAIL_CONFIRMATION sau cập nhật.
[ ] Account chỉ ACTIVE sau khi confirm.
[ ] Full name + email cập nhật nguyên tử.
[ ] Auth provider đồng bộ.
[ ] EmailVerifiedAt = null.
[ ] Email cũ nhận notice trung lập.
[ ] SENT/SKIPPED/FAILED hiển thị đúng.
[ ] Không tạo user mới.
[ ] Không đổi role/campus/department.
[ ] Không tạo template mới.
[ ] Không hardcode URL.
[ ] Backend unit test xanh.
[ ] Backend integration test xanh.
[ ] Frontend test xanh.
[ ] Frontend type-check xanh.
[ ] Frontend build xanh.
[ ] Backend build xanh.
[ ] git diff --check xanh.
```

---

# 32. Mẫu báo cáo cuối cùng Agent phải trả

```markdown
# Kết quả triển khai pending email edit confirmation

## 1. Preflight
- Branch:
- HEAD:
- Working tree:
- WIP được bảo toàn:
- git diff --check:

## 2. Root cause
- Frontend API cũ:
- Backend email template cũ:
- Vì sao không kích hoạt được:

## 3. File đã sửa

### Frontend
- ...

### Backend
- ...

### Tests
- ...

## 4. Luồng mới
- Điều kiện pending:
- API được gọi:
- Transaction:
- Token supersede:
- Template gửi:
- Confirm URL:

## 5. Auth consistency
- Local provider:
- SSO/FEID:
- EmailVerifiedAt:
- Sessions:

## 6. Delivery outcome
- SENT:
- SKIPPED:
- FAILED:

## 7. Security
- Raw token:
- Token cũ:
- Active token count:
- Logging:
- Hardcoded URL:

## 8. Tests
- Backend build:
- Unit:
- Integration:
- Frontend type-check:
- Frontend test:
- Frontend build:
- Manual verification:

## 9. Kết luận
- PASS / FAIL / PARTIAL
- Blocker nếu có:
```

---

# 33. Lệnh giao việc ngắn gọn

```text
Đọc toàn bộ file này trước khi sửa.

Tiếp tục làm việc ngay trên nhánh Duy-Iter1. Không chuyển sang Dev, không tạo nhánh mới và không reset WIP.

Sửa luồng HO chỉnh sửa email của account PENDING_EMAIL_CONFIRMATION:

- Frontend phải phân nhánh pending + emailChanged sang POST /api/accounts/edit-pending-email.
- Không dùng updatebasicaccountinfo để gửi notice thông thường cho email mới của pending account.
- Mở rộng edit-pending-email để cập nhật cả fullName và email trong cùng transaction nếu modal sửa đồng thời.
- Đồng bộ auth providers, đặt EmailVerifiedAt = null.
- Supersede token cũ, phát hành token mới.
- Gửi template ACCOUNT_EMAIL_CONFIRMATION giống hệt create account, có nút xác nhận và link mới.
- Email cũ chỉ nhận notice trung lập.
- Account vẫn pending cho tới khi người nhận confirm.
- Phản ánh SENT/SKIPPED/FAILED trung thực.
- Không tạo template mới, không hardcode URL, không ACTIVE account tự động.

Chạy build/test đầy đủ và trả báo cáo theo mẫu cuối file.
```
