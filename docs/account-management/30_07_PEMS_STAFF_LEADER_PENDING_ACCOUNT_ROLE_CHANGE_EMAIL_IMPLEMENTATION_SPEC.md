# PEMS — IMPLEMENTATION SPEC
## Staff Leader đổi role của tài khoản `PENDING_EMAIL_CONFIRMATION` phải gửi email thông báo

> **Mục đích tài liệu:**  
> Đây là đặc tả độc lập cho yêu cầu bổ sung:
>
> Khi **Staff Leader** thực sự thay đổi role của một account có trạng thái `PENDING_EMAIL_CONFIRMATION`, hệ thống vẫn phải gửi email `ACCOUNT_ROLE_CHANGED`, giống như khi đổi role cho account `ACTIVE` hoặc `INACTIVE`.
>
> **Repository:** `quangthoai04/PEMS`  
> **Nhánh làm việc bắt buộc:** nhánh hiện tại `Duy-Iter1`  
> **Nguồn code chuẩn:** HEAD hiện tại của `Duy-Iter1` tại thời điểm Agent bắt đầu  
> **Không chuyển sang `Dev`, không tạo nhánh mới, không reset/rebase/xóa WIP**

---

# 1. Vấn đề hiện tại

Sau khi triển khai flow chỉnh sửa email và resend confirmation cho Staff Leader, hiện phát sinh lỗi nghiệp vụ:

```text
Staff Leader đổi role cho account PENDING_EMAIL_CONFIRMATION
→ Role trong database đã đổi
→ Nhưng account không nhận được email thông báo role changed
```

Trong khi đó, khi đổi role cho account có status khác như `ACTIVE` hoặc `INACTIVE`, hệ thống vẫn gửi `ACCOUNT_ROLE_CHANGED`.

Yêu cầu mới:

```text
Miễn là Staff Leader thực sự thay đổi role,
thì account phải nhận email ACCOUNT_ROLE_CHANGED,
không phụ thuộc status là ACTIVE, INACTIVE hay PENDING_EMAIL_CONFIRMATION.
```

---

# 2. Quy tắc nghiệp vụ mới

## 2.1. Pending account chỉ đổi role

Ví dụ:

```text
Status hiện tại:
PENDING_EMAIL_CONFIRMATION

Role cũ:
STAFF / STAFF

Role mới:
STUDENT

Email:
Không đổi
```

Kết quả bắt buộc:

```text
- Role được cập nhật.
- Account vẫn PENDING_EMAIL_CONFIRMATION.
- Confirmation token hiện tại giữ nguyên.
- Không tạo confirmation token mới.
- Không tăng resend_count.
- Không gửi ACCOUNT_EMAIL_CONFIRMATION mới.
- Gửi ACCOUNT_ROLE_CHANGED tới email hiện tại.
```

Email role changed chỉ là email thông báo. Email này không thay thế email xác nhận và không kích hoạt account.

## 2.2. Pending account đổi cả role và email

Ví dụ:

```text
Status:
PENDING_EMAIL_CONFIRMATION

Role cũ:
STAFF / STAFF

Role mới:
DEPARTMENT / LEADER

Email cũ:
old@fpt.edu.vn

Email mới:
new@fpt.edu.vn
```

Kết quả bắt buộc:

### Email mới nhận

```text
1. ACCOUNT_EMAIL_CONFIRMATION
2. ACCOUNT_ROLE_CHANGED
```

### Email cũ nhận

```text
ACCOUNT_PENDING_EMAIL_CHANGED_OLD_NOTICE
```

### Database

```text
- users.email = email mới.
- Role/sub-role/department được cập nhật.
- Status vẫn PENDING_EMAIL_CONFIRMATION.
- Confirmation token cũ → SUPERSEDED.
- Confirmation token mới → PENDING.
- Token mới target email = email mới.
- Chỉ một token confirmation còn hợp lệ.
```

Account chỉ chuyển `ACTIVE` sau khi người nhận bấm confirmation link mới.

## 2.3. Pending account chỉ sửa họ tên

```text
Role cũ = Role mới
Email cũ = Email mới
Chỉ fullName thay đổi
```

Kết quả:

```text
- Không gửi ACCOUNT_ROLE_CHANGED.
- Không phát hành confirmation token mới.
- Không gửi ACCOUNT_EMAIL_CONFIRMATION.
```

## 2.4. Pending account chỉ sửa MSSV

```text
Role cũ = STUDENT
Role mới = STUDENT
Chỉ studentCode thay đổi
```

Kết quả:

```text
- Không gửi ACCOUNT_ROLE_CHANGED.
- Không phát hành confirmation token mới.
- Không gửi ACCOUNT_EMAIL_CONFIRMATION.
```

## 2.5. No-op

Khi không có thay đổi thực tế:

```text
- Không ghi audit mới.
- Không revoke session.
- Không gửi email.
- Không tạo token.
- Không bump updated_at.
```

---

# 3. Ma trận email bắt buộc

| Status | Role đổi | Email đổi | Email phải gửi |
|---|---:|---:|---|
| `PENDING_EMAIL_CONFIRMATION` | Có | Không | `ACCOUNT_ROLE_CHANGED` |
| `PENDING_EMAIL_CONFIRMATION` | Có | Có | `ACCOUNT_EMAIL_CONFIRMATION` + `ACCOUNT_ROLE_CHANGED` |
| `PENDING_EMAIL_CONFIRMATION` | Không | Có | `ACCOUNT_EMAIL_CONFIRMATION` |
| `PENDING_EMAIL_CONFIRMATION` | Không | Không | Không gửi |
| `ACTIVE` | Có | Không | `ACCOUNT_ROLE_CHANGED` |
| `ACTIVE` | Có | Có | Email-change notices + `ACCOUNT_ROLE_CHANGED` |
| `INACTIVE` | Có | Không | `ACCOUNT_ROLE_CHANGED` |
| `INACTIVE` | Có | Có | Email-change notices + `ACCOUNT_ROLE_CHANGED` |
| Bất kỳ | Không có thay đổi | Không | Không gửi |

---

# 4. Root cause cần audit

File chính:

```text
backend/PEMS.Application/Accounts/Commands/
UpdateAccountRole/UpdateAccountRoleCommandHandler.cs
```

Cần audit:

```text
- Điều kiện có gọi SendRoleChangedNotificationAsync hay không.
- Có branch nào loại trừ PENDING_EMAIL_CONFIRMATION hay không.
- Có branch pending + email change chỉ gửi confirmation mà bỏ role changed hay không.
- Có dùng hasAnyChange thay vì roleChanged hay không.
- Helper gửi role email có nuốt exception hay không.
- Response backend có trả delivery status hay không.
- Frontend có bỏ qua response hay không.
```

---

# 5. Xác định chính xác role đã thay đổi

Không dùng:

```csharp
hasAnyChange
```

vì biến này thường bao gồm fullName, email, studentCode, role, sub-role, department và campus.

Cần biến riêng:

```csharp
var roleChanged =
    oldRoleCode != shape.RoleCode
    || oldSubRole != shape.SubRole
    || oldDepartmentId != shape.DepartmentId
    || oldPrimaryCampusId != shape.PrimaryCampusId;
```

Nếu code hiện có đã có `hasStructuralChange` và semantic đúng với role assignment change, có thể dùng:

```csharp
var roleChanged = hasStructuralChange;
```

Nhưng phải chứng minh `hasStructuralChange` không bao gồm fullName, email hoặc studentCode-only.

---

# 6. Không loại trừ pending status

Điều kiện gửi đúng:

```csharp
var shouldSendRoleChangedEmail = roleChanged;
```

Không được viết:

```csharp
var shouldSendRoleChangedEmail =
    roleChanged
    && user.Status != UserStatuses.PendingEmailConfirmation;
```

Status chỉ quyết định confirmation flow, không được làm mất role-change notification.

---

# 7. Không tạo confirmation token mới nếu chỉ đổi role

Điều kiện:

```text
status = PENDING_EMAIL_CONFIRMATION
roleChanged = true
emailChanged = false
```

Bắt buộc:

```text
- Không gọi IssuePendingAsync.
- Không supersede confirmation token.
- Không tạo token mới.
- Không tăng resend_count.
- Không gửi ACCOUNT_EMAIL_CONFIRMATION.
```

Confirmation token chứng minh quyền sở hữu email. Role change không làm email ownership thay đổi.

---

# 8. Khi đổi role và email cùng lúc

Điều kiện:

```text
status = PENDING_EMAIL_CONFIRMATION
roleChanged = true
emailChanged = true
```

Flow:

```text
1. Lock target user.
2. Validate Staff Leader scope.
3. Resolve role shape mới.
4. Validate email mới.
5. Check duplicate email.
6. Update role/sub-role/department/campus/studentCode.
7. Update fullName/email nếu có.
8. Sync auth providers.
9. Set EmailVerifiedAt = null.
10. Supersede token cũ.
11. Issue token mới.
12. Ghi audit.
13. SaveChanges.
14. Commit transaction.
15. Gửi ACCOUNT_EMAIL_CONFIRMATION.
16. Gửi ACCOUNT_PENDING_EMAIL_CHANGED_OLD_NOTICE.
17. Gửi ACCOUNT_ROLE_CHANGED.
18. Trả delivery status từng email.
```

Không gọi hai API nối tiếp. Tất cả DB mutation phải commit trong cùng transaction.

---

# 9. Template email role changed

Dùng template hiện có:

```csharp
SystemEmailTemplates.AccountRoleChanged
```

Template code:

```text
ACCOUNT_ROLE_CHANGED
```

Không tạo template mới.

Các biến:

```text
fullName
oldRoleName
newRoleName
campusName
```

Recipient:

```csharp
new EmailRecipient(
    user.Email,
    user.FullName)
```

Nếu email đổi cùng request, recipient phải là email mới sau mutation.

---

# 10. Dữ liệu email phải dùng snapshot cuối cùng

`ACCOUNT_ROLE_CHANGED` phải dùng:

```text
fullName: Tên sau mutation.
oldRoleName: Role/sub-role trước mutation.
newRoleName: Role/sub-role sau mutation.
campusName: Campus sau mutation.
```

Ví dụ:

```csharp
new Dictionary<string, string>
{
    ["fullName"] = user.FullName,
    ["oldRoleName"] =
        ResolveRoleDisplayName(oldRoleCode, oldSubRole),
    ["newRoleName"] =
        ResolveRoleDisplayName(shape.RoleCode, shape.SubRole),
    ["campusName"] =
        string.IsNullOrWhiteSpace(campusName)
            ? "—"
            : campusName,
}
```

Không dùng dữ liệu stale từ frontend row.

---

# 11. Thứ tự post-commit

Sau khi DB commit:

```text
1. Revoke sessions nếu có structural/identity change theo rule.
2. Gửi confirmation email nếu pending + emailChanged.
3. Gửi notice tới email cũ nếu pending + emailChanged.
4. Gửi email-change notices nếu active/inactive + emailChanged.
5. Gửi ACCOUNT_ROLE_CHANGED nếu roleChanged.
6. Trả response.
```

Các email phải độc lập:

```csharp
var confirmationEmailStatus = "NOT_REQUIRED";
var roleChangeEmailStatus = "NOT_REQUIRED";

if (pendingEmailChanged)
{
    confirmationEmailStatus =
        await SendPendingConfirmationEmailAsync(...);
}

if (roleChanged)
{
    roleChangeEmailStatus =
        await SendRoleChangedNotificationAsync(...);
}
```

Nếu confirmation email fail, vẫn thử gửi role-change email. Nếu role-change email fail, không làm confirmation email bị coi là thất bại.

---

# 12. Sửa helper gửi role email

Đổi helper sang trả delivery status:

```csharp
private async Task<string> SendRoleChangedNotificationAsync(
    User user,
    AccountProvisioningRules.ResolvedShape shape,
    string oldRoleCode,
    string? oldSubRole,
    ulong? actorId,
    CancellationToken cancellationToken)
{
    var campusName = shape.PrimaryCampusId is null
        ? null
        : await _db.Campuses
            .AsNoTracking()
            .Where(c => c.CampusId == shape.PrimaryCampusId)
            .Select(c => c.Name)
            .FirstOrDefaultAsync(cancellationToken);

    try
    {
        var result = await _dispatcher.SendAsync(
            new SystemEmailRequest(
                SystemEmailTemplates.AccountRoleChanged,
                new EmailRecipient(
                    user.Email,
                    user.FullName),
                new Dictionary<string, string>
                {
                    ["fullName"] = user.FullName,
                    ["oldRoleName"] =
                        ResolveRoleDisplayName(
                            oldRoleCode,
                            oldSubRole),
                    ["newRoleName"] =
                        ResolveRoleDisplayName(
                            shape.RoleCode,
                            shape.SubRole),
                    ["campusName"] =
                        string.IsNullOrWhiteSpace(campusName)
                            ? "—"
                            : campusName,
                },
                RelatedType: "User",
                RelatedId: user.UserId,
                SentBy: actorId),
            cancellationToken);

        return result.NotificationStatus;
    }
    catch
    {
        return "FAILED";
    }
}
```

Giá trị trả về:

```text
SENT
SKIPPED
FAILED
```

---

# 13. Mở rộng backend response

File:

```text
backend/PEMS.Application/Accounts/Commands/
UpdateAccountRole/UpdateAccountRoleResponse.cs
```

Bổ sung:

```csharp
public bool RoleChanged { get; init; }
public bool EmailChanged { get; init; }
public bool RequiresEmailConfirmation { get; init; }

public string RoleChangeEmailNotificationStatus
{
    get;
    init;
} = "NOT_REQUIRED";

public string ConfirmationEmailNotificationStatus
{
    get;
    init;
} = "NOT_REQUIRED";
```

Response đề xuất:

```csharp
public sealed class UpdateAccountRoleResponse
{
    public ulong UserId { get; init; }
    public string RoleCode { get; init; } = default!;
    public ulong? PrimaryCampusId { get; init; }
    public int RevokedSessions { get; init; }

    public bool RoleChanged { get; init; }
    public bool EmailChanged { get; init; }
    public bool RequiresEmailConfirmation { get; init; }

    public string RoleChangeEmailNotificationStatus
    {
        get;
        init;
    } = "NOT_REQUIRED";

    public string ConfirmationEmailNotificationStatus
    {
        get;
        init;
    } = "NOT_REQUIRED";

    public string Message { get; init; }
        = "Cập nhật tài khoản thành công.";
}
```

---

# 14. Giá trị delivery status

Mỗi email trả một trong:

```text
NOT_REQUIRED
SENT
SKIPPED
FAILED
```

Không trả HTTP error chỉ vì email fail sau khi role đã commit.

---

# 15. Cập nhật response trong handler

Ví dụ:

```csharp
var wasPendingEmailConfirmation =
    user.Status == UserStatuses.PendingEmailConfirmation;

var roleEmailStatus = "NOT_REQUIRED";

if (roleChanged)
{
    roleEmailStatus =
        await SendRoleChangedNotificationAsync(
            user,
            shape,
            oldRoleCode,
            oldSubRole,
            actorId,
            cancellationToken);
}

return new UpdateAccountRoleResponse
{
    UserId = user.UserId,
    RoleCode = shape.RoleCode,
    PrimaryCampusId = shape.PrimaryCampusId,
    RevokedSessions = revoked,
    RoleChanged = roleChanged,
    EmailChanged = resolvedEmail != oldEmail,
    RequiresEmailConfirmation = wasPendingEmailConfirmation,
    RoleChangeEmailNotificationStatus = roleEmailStatus,
    ConfirmationEmailNotificationStatus = confirmationEmailStatus,
    Message = "Cập nhật tài khoản thành công.",
};
```

---

# 16. Frontend type

File:

```text
frontend/pems-react/src/features/account-management/
types/accountManagement.types.ts
```

Sửa:

```ts
export interface UpdateAccountRoleResponse {
  userId: string;
  roleCode: string;
  primaryCampusId?: string | null;
  revokedSessions: number;

  roleChanged: boolean;
  emailChanged: boolean;
  requiresEmailConfirmation: boolean;

  roleChangeEmailNotificationStatus:
    | 'NOT_REQUIRED'
    | 'SENT'
    | 'SKIPPED'
    | 'FAILED'
    | string;

  confirmationEmailNotificationStatus:
    | 'NOT_REQUIRED'
    | 'SENT'
    | 'SKIPPED'
    | 'FAILED'
    | string;

  message: string;
}
```

---

# 17. Frontend phải dùng response thật

Không làm:

```ts
await accountManagementApi.updateAccountRole(...);
pushToast(
  'success',
  'Cập nhật tài khoản thành công. Đã gửi email thông báo cho người dùng.',
);
```

Phải giữ response:

```ts
const result =
  await accountManagementApi.updateAccountRole({
    // payload
  });
```

Sau đó đọc:

```text
result.roleChangeEmailNotificationStatus
result.confirmationEmailNotificationStatus
result.requiresEmailConfirmation
result.roleChanged
result.emailChanged
```

---

# 18. Toast cho pending account chỉ đổi role

## SENT

```text
Đã cập nhật vai trò và gửi email thông báo tới người dùng.
Tài khoản vẫn đang chờ xác nhận email.
```

## SKIPPED

```text
Đã cập nhật vai trò nhưng email thông báo không được gửi trong môi trường hiện tại.
Tài khoản vẫn đang chờ xác nhận email.
```

## FAILED

```text
Đã cập nhật vai trò nhưng không thể gửi email thông báo.
Tài khoản vẫn đang chờ xác nhận email.
```

Không nói tài khoản đã được kích hoạt.

---

# 19. Toast cho pending account đổi cả role và email

## Cả hai SENT

```text
Đã cập nhật tài khoản, gửi email xác nhận và gửi thông báo thay đổi vai trò tới email mới.
Tài khoản sẽ được kích hoạt sau khi người nhận hoàn tất xác nhận.
```

## Confirmation SENT, role email FAILED

```text
Đã cập nhật tài khoản và gửi email xác nhận.
Không thể gửi email thông báo thay đổi vai trò.
```

## Confirmation FAILED, role email SENT

```text
Đã cập nhật tài khoản và gửi thông báo thay đổi vai trò,
nhưng không thể gửi liên kết xác nhận.
Vui lòng sử dụng chức năng “Gửi lại email xác nhận”.
```

## Cả hai FAILED

```text
Đã cập nhật tài khoản nhưng không thể gửi email xác nhận hoặc email thông báo thay đổi vai trò.
Vui lòng sử dụng chức năng “Gửi lại email xác nhận”.
```

SKIPPED phải hiển thị warning, không báo gửi thành công.

---

# 20. Không kích hoạt account khi đổi role

Sau role change, `users.status` vẫn phải là:

```text
PENDING_EMAIL_CONFIRMATION
```

Không được:

```text
- Chuyển ACTIVE.
- Cho login trước confirm.
- Xem role-change email như bằng chứng sở hữu email.
```

---

# 21. Audit log

Giữ action:

```text
UPDATE_ACCOUNT_ROLE
```

Audit phải ghi old/new của:

```text
fullName
email
roleCode
subRole
campusId
departmentId
studentCode
```

Không ghi raw token, full confirmation URL hoặc email body.

---

# 22. Email history

## Role-change email

```text
template_code = ACCOUNT_ROLE_CHANGED
related_type = User
related_id = target userId
sent_by = Staff Leader userId
recipient = email sau mutation
status = delivery status thật
```

## Confirmation email nếu email đổi

```text
template_code = ACCOUNT_EMAIL_CONFIRMATION
related_type = User
related_id = target userId
recipient = email mới
status = delivery status thật
```

Mỗi email phải có history record riêng.

---

# 23. SQL có cần sửa không?

Không cần sửa SQL nếu template `ACCOUNT_ROLE_CHANGED` đã tồn tại và đang dùng cho account status khác.

Không:

```text
- Tạo template mới.
- ALTER TABLE.
- Thêm migration.
- Đổi schema.
```

Chỉ audit template row tồn tại, `status = ACTIVE`, đủ VI/EN và placeholder khớp registry.

---

# 24. File dự kiến thay đổi

## Backend

```text
backend/PEMS.Application/Accounts/Commands/
UpdateAccountRole/UpdateAccountRoleCommandHandler.cs

backend/PEMS.Application/Accounts/Commands/
UpdateAccountRole/UpdateAccountRoleResponse.cs
```

## Frontend

```text
frontend/pems-react/src/features/account-management/
types/accountManagement.types.ts

frontend/pems-react/src/pages/dashboard/accounts/
AccountManagement.tsx
```

## Tests

```text
tests/PEMS.UnitTests/Accounts/UpdateAccountRole/
UpdateAccountRoleCommandHandlerTests.cs

tests/PEMS.IntegrationTests/Accounts/

frontend account-management tests
```

---

# 25. Backend tests bắt buộc

```text
1. Pending + roleChanged + emailUnchanged gửi ACCOUNT_ROLE_CHANGED.
2. Email gửi tới email hiện tại.
3. oldRoleName đúng.
4. newRoleName đúng.
5. campusName dùng dữ liệu cuối cùng.
6. Account vẫn PENDING_EMAIL_CONFIRMATION.
7. Confirmation token cũ giữ nguyên.
8. Không tạo confirmation token mới.
9. resend_count không tăng.
10. Pending + roleChanged + emailChanged gửi confirmation email.
11. Trường hợp trên cũng gửi ACCOUNT_ROLE_CHANGED.
12. Cả confirmation và role-change gửi tới email mới.
13. Email cũ nhận pending-old notice.
14. Token cũ bị supersede khi email đổi.
15. Token mới confirm được.
16. Pending + fullName-only không gửi role email.
17. Pending + studentCode-only không gửi role email.
18. No-op không gửi email.
19. Active role change vẫn gửi như trước.
20. Inactive role change vẫn gửi như trước.
21. Role email FAILED không rollback role.
22. Confirmation FAILED vẫn thử gửi role email.
23. Role email FAILED trả FAILED.
24. Role email SKIPPED trả SKIPPED.
25. Không log token.
26. Không log confirmation URL.
```

---

# 26. Frontend tests bắt buộc

```text
1. Frontend giữ response của updateAccountRole.
2. Pending role-only + SENT hiển thị đúng.
3. Pending role-only + FAILED không báo đã gửi thành công.
4. Pending role-only vẫn nói account chờ xác nhận.
5. Pending role+email + cả hai SENT hiển thị đúng.
6. Confirmation FAILED + role SENT hướng dẫn resend.
7. Confirmation SENT + role FAILED báo partial đúng.
8. SKIPPED hiển thị warning.
9. Không đóng modal trước khi request kết thúc.
10. Double-click chỉ gửi một request.
11. Sau success refetch list.
12. Sau success refetch detail nếu pattern hiện tại yêu cầu.
13. Sau success reload statistics.
14. Account vẫn hiển thị Chờ xác nhận email.
15. Nút Gửi lại email xác nhận vẫn xuất hiện.
```

---

# 27. Manual verification

## Pending chỉ đổi role

1. Đăng nhập Staff Leader.
2. Mở account pending cùng campus.
3. Chọn role khác.
4. Không đổi email.
5. Submit.
6. Kiểm tra DB role đã đổi.
7. Kiểm tra status vẫn pending.
8. Kiểm tra confirmation token cũ vẫn pending.
9. Kiểm tra không có confirmation token mới.
10. Kiểm tra email `ACCOUNT_ROLE_CHANGED`.
11. Kiểm tra link confirmation cũ vẫn confirm được.

## Pending đổi role + email

1. Mở pending account.
2. Đổi role.
3. Đổi email.
4. Submit.
5. Kiểm tra DB commit nguyên tử.
6. Kiểm tra email mới nhận confirmation mail.
7. Kiểm tra email mới nhận role-change mail.
8. Kiểm tra email cũ nhận neutral notice.
9. Kiểm tra token cũ không dùng được.
10. Kiểm tra token mới confirm được.
11. Kiểm tra status vẫn pending trước confirm.
12. Confirm link mới.
13. Kiểm tra account ACTIVE.

## Delivery failed

1. Giả lập role-change dispatcher `FAILED`.
2. Submit role change.
3. Kiểm tra role vẫn commit.
4. Frontend báo email thất bại.
5. Không báo đã gửi thành công.

---

# 28. Preflight bắt buộc

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

Nếu branch không đúng hoặc WIP ngoài task chưa rõ, dừng và báo cáo.

---

# 29. Search/audit bắt buộc

```bash
rg -n \
  "UpdateAccountRole|SendRoleChangedNotificationAsync|ACCOUNT_ROLE_CHANGED|PENDING_EMAIL_CONFIRMATION|RoleChangeEmailNotificationStatus|ConfirmationEmailNotificationStatus" \
  frontend backend tests
```

```bash
rg -n \
  "IssuePendingAsync|ACCOUNT_EMAIL_CONFIRMATION|ACCOUNT_PENDING_EMAIL_CHANGED_OLD_NOTICE|hasStructuralChange|hasAnyChange|roleChanged" \
  backend frontend tests
```

---

# 30. Build và test gate

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

## Static

```bash
git diff --check
```

---

# 31. Không được làm

```text
- Không bỏ role-change email cho pending account.
- Không gửi role-change email cho identity-only change.
- Không gửi role-change email cho studentCode-only change.
- Không phát hành token mới khi chỉ đổi role.
- Không tăng resend_count khi chỉ đổi role.
- Không tự ACTIVE account.
- Không tạo template mới.
- Không sửa SQL nếu ACCOUNT_ROLE_CHANGED đã tồn tại.
- Không gộp confirmation và role-change thành một email.
- Không gộp hai email history record.
- Không bỏ delivery status.
- Không luôn báo “đã gửi”.
- Không rollback role khi email fail.
- Không chuyển branch.
- Không reset WIP.
```

---

# 32. Definition of Done

```text
[ ] Branch = Duy-Iter1.
[ ] WIP được bảo toàn.
[ ] Pending account đổi role nhận ACCOUNT_ROLE_CHANGED.
[ ] Email gửi tới email hiện tại sau mutation.
[ ] Pending role+email nhận cả confirmation và role-change email.
[ ] Pending role-only không làm thay đổi confirmation token.
[ ] resend_count không tăng khi chỉ đổi role.
[ ] Account vẫn PENDING_EMAIL_CONFIRMATION.
[ ] Không gửi role email cho fullName-only.
[ ] Không gửi role email cho studentCode-only.
[ ] Active role email không bị hồi quy.
[ ] Inactive role email không bị hồi quy.
[ ] Helper trả delivery status.
[ ] Response có role email status.
[ ] Response có confirmation email status.
[ ] Frontend không còn luôn báo đã gửi.
[ ] SENT/SKIPPED/FAILED được hiển thị trung thực.
[ ] Email fail không rollback role.
[ ] Không tạo template mới.
[ ] Không cần sửa SQL nếu template hiện tại đủ.
[ ] Backend unit test xanh.
[ ] Backend integration test xanh.
[ ] Frontend test xanh.
[ ] Frontend type-check xanh.
[ ] Frontend build xanh.
[ ] Backend build xanh.
[ ] git diff --check xanh.
```

---

# 33. Mẫu báo cáo cuối cùng Agent phải trả

```markdown
# Kết quả triển khai pending role-change email

## 1. Preflight
- Branch:
- HEAD:
- Working tree:
- WIP được bảo toàn:
- git diff --check:

## 2. Root cause
- Điều kiện gửi role email cũ:
- Vì sao pending bị bỏ qua:
- Frontend feedback cũ:

## 3. File đã sửa

### Backend
- ...

### Frontend
- ...

### Tests
- ...

## 4. Role-change flow
- roleChanged được xác định:
- Pending status:
- Recipient:
- Template:
- Delivery status:

## 5. Pending role + email flow
- Confirmation email:
- Role-change email:
- Old-email notice:
- Token supersede:
- Atomicity:

## 6. Token integrity
- Role-only token:
- Role+email token:
- resend_count:
- Active token count:

## 7. Frontend feedback
- SENT:
- SKIPPED:
- FAILED:
- Partial outcomes:

## 8. Tests
- Backend build:
- Unit:
- Integration:
- Frontend type-check:
- Frontend tests:
- Frontend build:
- Manual verification:

## 9. Kết luận
- PASS / FAIL / PARTIAL
- Blocker nếu có:
```

---

# 34. Lệnh giao việc ngắn gọn

```text
Đọc toàn bộ file này trước khi sửa.

Tiếp tục làm việc trên nhánh Duy-Iter1. Không chuyển sang Dev, không tạo nhánh mới và không reset WIP.

Sửa UpdateAccountRole flow của Staff Leader:

- Khi role thực sự thay đổi, luôn gửi ACCOUNT_ROLE_CHANGED, kể cả account đang PENDING_EMAIL_CONFIRMATION.
- Pending + role-only: không tạo token mới, không supersede token cũ, không tăng resend_count.
- Pending + role + email: gửi ACCOUNT_EMAIL_CONFIRMATION và ACCOUNT_ROLE_CHANGED tới email mới; email cũ nhận neutral notice.
- Không gửi role email cho fullName-only, email-only hoặc studentCode-only nếu role không đổi.
- Helper phải trả SENT/SKIPPED/FAILED.
- Response phải có roleChangeEmailNotificationStatus và confirmationEmailNotificationStatus.
- Frontend phải dùng response thật, không luôn báo đã gửi.
- Email fail không rollback role.
- Không tạo template mới và không sửa SQL nếu ACCOUNT_ROLE_CHANGED đã tồn tại.

Chạy build/test đầy đủ và trả báo cáo theo mẫu cuối file.
```
