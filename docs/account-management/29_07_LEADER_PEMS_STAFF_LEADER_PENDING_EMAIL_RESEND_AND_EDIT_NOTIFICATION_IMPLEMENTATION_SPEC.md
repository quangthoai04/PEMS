# PEMS — IMPLEMENTATION SPEC
## Staff Leader: Gửi lại email xác nhận và xử lý email khi chỉnh sửa tài khoản

> **Mục đích tài liệu:**  
> Đây là đặc tả độc lập cho trang **Quản lý tài khoản do Staff Leader quản lý**, gồm hai yêu cầu:
>
> 1. Với tài khoản có trạng thái `PENDING_EMAIL_CONFIRMATION`, khi mở modal chi tiết phải có nút **“Gửi lại email xác nhận”**.
> 2. Khi Staff Leader chỉnh sửa email của account, hệ thống phải gửi email phù hợp theo trạng thái account, xử lý tương tự luồng HO đã chốt.
>
> **Repository:** `quangthoai04/PEMS`  
> **Nhánh làm việc bắt buộc:** nhánh hiện tại `Duy-Iter1`  
> **Nguồn code chuẩn:** HEAD hiện tại của `Duy-Iter1` tại thời điểm Agent bắt đầu  
> **Không chuyển sang `Dev`, không tạo nhánh mới, không reset/rebase/xóa WIP**
>
> **Actor chính:** Staff Leader trong trang Quản lý tài khoản.

---

# 1. Phạm vi tài khoản Staff Leader được quản lý

Staff Leader chỉ được thao tác các tài khoản nằm trong phạm vi campus của mình và thuộc đúng nhóm role/sub-role nghiệp vụ đang cho phép.

Phạm vi hiện tại cần giữ:

```text
STAFF / STAFF
DEPARTMENT / LEADER
STUDENT
```

Không mở rộng quyền cho:

```text
- HO.
- Staff Leader khác.
- Account ngoài campus.
- Account của chính Staff Leader.
- Visitor nếu nghiệp vụ hiện tại chưa cho phép.
- Account không còn thuộc scope sau khi modal đã mở.
- Account LOCKED nếu rule hiện tại không cho chỉnh sửa.
```

Backend phải kiểm tra lại toàn bộ scope ở từng mutation.

Không tin các cờ frontend như:

```text
isStaffLeader
canViewDetails
canUpdateRole
canManageStatus
```

làm nguồn authorization cuối cùng.

---

# 2. Yêu cầu tổng thể

## 2.1. Gửi lại email xác nhận

Khi Staff Leader mở detail account có:

```text
users.status = PENDING_EMAIL_CONFIRMATION
```

modal phải hiển thị nút:

```text
Gửi lại email xác nhận
```

Nút này phục vụ khi:

```text
- Email xác nhận lần đầu gửi thất bại.
- Người nhận không thấy email.
- Email vào spam.
- Email bị xóa hoặc thất lạc.
- Liên kết cũ đã hết hiệu lực hoặc bị supersede.
```

## 2.2. Staff Leader chỉnh sửa email account

Khi Staff Leader sửa email, hệ thống phải phân loại theo status:

```text
PENDING_EMAIL_CONFIRMATION
ACTIVE
INACTIVE
LOCKED
```

Không gửi cùng một mẫu email cho mọi status.

---

# 3. Chức năng 1 — Nút “Gửi lại email xác nhận”

## 3.1. Điều kiện hiển thị

Nút chỉ hiển thị khi đồng thời thỏa:

```text
- Detail API đã tải thành công.
- status detail = PENDING_EMAIL_CONFIRMATION.
- Staff Leader có quyền quản lý pending account này.
- Account cùng campus.
- Account thuộc đúng role/sub-role scope.
```

Không hiển thị khi:

```text
ACTIVE
INACTIVE
LOCKED
```

Không dùng riêng status của row list.

Nguồn chuẩn:

```text
details.status
```

sau khi normalize.

Ví dụ:

```ts
const normalizedStatus = String(
  accountDetails.status ?? ''
).trim().toUpperCase();

const showResendButton =
  detailLoaded
  && normalizedStatus === 'PENDING_EMAIL_CONFIRMATION'
  && accountDetails.canResendEmailConfirmation === true;
```

---

# 4. Khuyến nghị bổ sung permission flags vào detail API

Để frontend không tự lặp logic authorization, nên mở rộng detail DTO:

```csharp
public bool CanResendEmailConfirmation { get; init; }

public bool CanEditPendingEmail { get; init; }
```

Frontend:

```ts
export interface AccountDetails {
  // Các field hiện có...

  canResendEmailConfirmation?: boolean;
  canEditPendingEmail?: boolean;
}
```

Backend query handler phải tính các cờ dựa trên:

```text
- Actor role/sub-role.
- Actor campus.
- Target campus.
- Target role/sub-role.
- Target status.
- Self-account rule.
- Locked rule.
- Permission account management.
```

Không để frontend tự suy luận từ `roleCode` và `campusId` nếu backend có thể trả quyền chính xác.

---

# 5. Tái sử dụng endpoint resend hiện có

Dùng chung endpoint:

```http
POST /api/accounts/resend-email-confirmation
```

Request:

```json
{
  "userId": 123
}
```

Response:

```ts
export interface ResendAccountEmailConfirmationResponse {
  success: boolean;
  emailNotificationStatus:
    | 'SENT'
    | 'SKIPPED'
    | 'FAILED'
    | string;
  resendCount: number;
  message: string;
}
```

Không tạo endpoint mới như:

```text
staff-leader-resend-confirmation
resend-staff-account-email
staff-leader-resend-account-confirmation
```

HO và Staff Leader phải dùng cùng flow backend.

---

# 6. Frontend resend phải dùng chung với HO

Nếu phía HO đã có:

```ts
API_ENDPOINTS.accounts.resendEmailConfirmation
```

và:

```ts
accountManagementApi.resendEmailConfirmation(...)
```

thì Staff Leader tái sử dụng nguyên API này.

Không tạo:

```ts
resendEmailConfirmationForStaffLeader(...)
```

Điều kiện UI chuyển từ HO-only sang permission-based:

```ts
const canResendPendingConfirmation =
  detailLoaded
  && normalizedStatus === 'PENDING_EMAIL_CONFIRMATION'
  && accountDetails.canResendEmailConfirmation === true;
```

---

# 7. Confirmation dialog resend

Khi bấm nút, chưa gọi API ngay.

## Tiêu đề

```text
Gửi lại email xác nhận
```

## Nội dung

```text
Hệ thống sẽ phát hành một liên kết xác nhận mới và gửi đến:

<email tài khoản>

Liên kết xác nhận cũ sẽ không còn hiệu lực.

Tài khoản chỉ được kích hoạt sau khi người nhận hoàn tất xác nhận email.
```

## Nút

```text
Hủy
Xác nhận gửi lại
```

## Quy tắc UI

```text
- Email lấy từ detail response.
- Email chỉ đọc.
- Không chỉnh sửa email tại dialog resend.
- Có loading state.
- Chống double-click.
- Không đóng bằng backdrop/Escape khi request đang chạy.
- Không reset detail modal khi gửi lỗi.
```

---

# 8. Backend resend phải giữ các rule bảo mật

Backend phải kiểm tra lại:

```text
- Actor đã đăng nhập.
- Actor role STAFF, sub-role LEADER.
- Actor có quyền quản lý account.
- Actor có campus.
- Target cùng campus.
- Target thuộc đúng role/sub-role scope.
- Target không phải chính actor.
- Target vẫn PENDING_EMAIL_CONFIRMATION.
- Email đích là users.email hiện tại.
- Cooldown chưa bị vi phạm.
- Chưa vượt giới hạn resend.
```

Giữ baseline:

```text
Cooldown: 60 giây.
Max resend: 5 lần.
```

Khi resend:

```text
- Token cũ → SUPERSEDED.
- Token mới được sinh.
- Chỉ lưu token hash.
- Chỉ một token PENDING hợp lệ.
- Account vẫn PENDING_EMAIL_CONFIRMATION.
- Không tạo user mới.
- Không đổi role.
- Không đổi campus/department.
- Không cấp quyền.
```

---

# 9. Xử lý kết quả resend

## SENT

```text
Đã gửi lại email xác nhận đến <email>.
```

## SKIPPED

```text
Yêu cầu đã được xử lý nhưng email không được gửi trong môi trường hiện tại.
```

Không báo đã gửi thành công.

## FAILED

```text
Không thể gửi email xác nhận.
Tài khoản vẫn ở trạng thái chờ xác nhận email.
```

## RESEND_TOO_SOON

```text
Vui lòng đợi một lát trước khi gửi lại email xác nhận.
```

## RESEND_LIMIT_REACHED

```text
Đã đạt số lần gửi lại tối đa.
Vui lòng chỉnh sửa email hoặc liên hệ quản trị.
```

## ACCOUNT_NOT_PENDING

```text
Tài khoản không còn ở trạng thái chờ xác nhận email.
```

Sau lỗi này:

```text
- Refetch detail.
- Refetch list nếu cần.
- Ẩn nút nếu status đã đổi.
```

---

# 10. Chức năng 2 — Staff Leader chỉnh sửa email account

Staff Leader có thể chỉnh sửa đồng thời:

```text
- Role.
- Department.
- Student code.
- Full name.
- Email.
- Người thay thế Department Head nếu cần.
```

Vì vậy cần phân loại account thành hai nhóm:

```text
A. PENDING_EMAIL_CONFIRMATION
B. ACTIVE hoặc INACTIVE
```

Hai nhóm không được gửi cùng một loại email.

---

# 11. Pending account — email mới phải nhận confirmation mail

Điều kiện:

```text
status = PENDING_EMAIL_CONFIRMATION
AND
emailChanged = true
```

Email mới phải nhận:

```text
ACCOUNT_EMAIL_CONFIRMATION
```

Email phải giống luồng tạo account mới:

```text
- Có nút “Xác nhận email & kích hoạt tài khoản”.
- Có link /confirm-email?token=...
- Có token mới.
- Có thời hạn token.
- Có fullName.
- Có roleName cuối cùng.
- Có campusName.
- Có expiresInHours.
```

Không gửi email mới bằng:

```text
ACCOUNT_EMAIL_CHANGED_NEW_NOTICE
```

vì template này không có token kích hoạt.

---

# 12. Không gọi hai API tuần tự khi Staff Leader sửa role + email

Không làm:

```text
UpdateAccountRole
→ EditPendingAccountEmail
```

hoặc:

```text
EditPendingAccountEmail
→ UpdateAccountRole
```

vì có thể xảy ra partial update.

Ví dụ:

```text
Role đã đổi nhưng email/token chưa đổi.
```

hoặc:

```text
Email/token đã đổi nhưng role chưa đổi.
```

Toàn bộ thay đổi phải commit trong một transaction.

---

# 13. Hướng triển khai khuyến nghị cho pending account

Trong `UpdateAccountRoleCommandHandler`:

```text
1. Load account và dependencies.
2. Kiểm tra authorization/scope.
3. Xác định status hiện tại.
4. Resolve role/campus/department cuối cùng.
5. Validate full name/email.
6. Xác định emailChanged.
7. Nếu pending + emailChanged:
   - cập nhật email;
   - đồng bộ auth providers;
   - EmailVerifiedAt = null;
   - supersede token cũ;
   - tạo token mới;
   - giữ raw token trong memory;
8. Cập nhật role/department/student code.
9. Ghi audit.
10. SaveChanges + commit.
11. Sau commit:
   - gửi ACCOUNT_EMAIL_CONFIRMATION tới email mới;
   - gửi notice trung lập tới email cũ.
```

---

# 14. Tách shared service cho pending email change

Để HO và Staff Leader không có hai implementation khác nhau, nên tách service dùng chung:

```csharp
public interface IPendingAccountEmailChangeService
{
    Task<PreparedPendingEmailChange> PrepareAsync(
        User user,
        string newEmail,
        string? newFullName,
        CancellationToken cancellationToken);
}
```

Service chịu trách nhiệm:

```text
- Normalize email.
- Validate email.
- Check duplicate.
- Update user email.
- Update full name nếu có.
- Đồng bộ auth providers.
- EmailVerifiedAt = null.
- Supersede token cũ.
- Tạo token mới.
- Trả raw token.
- Trả old/new email metadata.
```

Cả:

```text
EditPendingAccountEmailCommandHandler
UpdateAccountRoleCommandHandler
```

dùng cùng service.

---

# 15. Confirmation email phải dùng dữ liệu sau cập nhật

Nếu Staff Leader đồng thời thay:

```text
Role + Email
```

thì email xác nhận phải hiển thị:

```text
- Full name cuối cùng.
- Role cuối cùng.
- Sub-role cuối cùng.
- Campus cuối cùng.
- Thời hạn xác nhận.
```

Không dùng snapshot cũ.

Ví dụ:

```csharp
AccountEmailVariables.ForConfirmationAsync(
    db,
    finalFullName,
    finalRoleCode,
    finalSubRole,
    finalCampusId,
    expiryHours,
    cancellationToken)
```

---

# 16. Pending account có gửi thêm role-changed email không?

Khuyến nghị:

```text
Chỉ gửi ACCOUNT_EMAIL_CONFIRMATION.
```

Không gửi thêm:

```text
ACCOUNT_ROLE_CHANGED
```

cho pending account.

Lý do:

```text
- Account chưa từng active.
- Confirmation mail đã thể hiện role/campus cuối cùng.
- Gửi thêm role-change notice gây trùng nội dung.
- Người nhận chưa thể đăng nhập.
```

Audit vẫn phải ghi đầy đủ role change.

---

# 17. Email cũ của pending account

Gửi:

```text
ACCOUNT_PENDING_EMAIL_CHANGED_OLD_NOTICE
```

Notice phải trung lập.

Không chứa:

```text
- Full name.
- Email mới.
- Role.
- Campus.
- Department.
- Token.
- Confirmation URL.
```

Lý do:

```text
Email cũ có thể là địa chỉ nhập nhầm của người không liên quan.
```

Notice cũ là best-effort.

Thất bại gửi notice cũ không được làm hỏng confirmation mail mới.

---

# 18. ACTIVE/INACTIVE account — xử lý giống HO

Khi:

```text
status = ACTIVE hoặc INACTIVE
AND
emailChanged = true
```

xử lý giống flow HO.

## Email cũ

Gửi:

```text
ACCOUNT_EMAIL_CHANGED_OLD_NOTICE
```

Nội dung trung lập.

Không tiết lộ:

```text
- Full name.
- Email mới.
- Role.
- Campus.
- Department.
```

## Email mới

Gửi:

```text
ACCOUNT_EMAIL_CHANGED_NEW_NOTICE
```

Thông báo email đăng nhập đã thay đổi.

---

# 19. Đồng bộ authentication provider

Khi email thay đổi:

## Local password provider

```csharp
provider.ProviderEmail = newEmail;
```

## Google SSO / FEID

Unlink hoặc xóa provider gắn với external identity cũ theo cùng policy của HO.

## Email verification

```csharp
user.EmailVerifiedAt = null;
```

## Session

```text
Thu hồi toàn bộ session hiện tại.
```

Người dùng không được tiếp tục dùng token gắn với identity cũ.

---

# 20. Trường hợp ACTIVE/INACTIVE đổi cả role và email

Có thể phát sinh:

```text
- Email đổi.
- Role đổi.
```

Giữ các email nghiệp vụ riêng:

```text
Email cũ:
- ACCOUNT_EMAIL_CHANGED_OLD_NOTICE

Email mới:
- ACCOUNT_EMAIL_CHANGED_NEW_NOTICE
- ACCOUNT_ROLE_CHANGED nếu role thực sự đổi
```

Không tạo template tổng hợp mới nếu chưa có yêu cầu riêng.

---

# 21. Response của UpdateAccountRole

Bổ sung:

```csharp
public bool EmailChanged { get; init; }

public bool RequiresEmailConfirmation { get; init; }

public string EmailNotificationStatus { get; init; }
    = "NOT_REQUIRED";
```

Frontend:

```ts
export interface UpdateAccountRoleResponse {
  userId: string;
  roleCode: string;
  primaryCampusId?: string | null;
  revokedSessions: number;

  emailChanged: boolean;
  requiresEmailConfirmation: boolean;

  emailNotificationStatus:
    | 'NOT_REQUIRED'
    | 'SENT'
    | 'SKIPPED'
    | 'FAILED'
    | 'PARTIAL'
    | string;

  message: string;
}
```

Ý nghĩa:

```text
NOT_REQUIRED:
Không đổi email.

SENT:
Các email chính đã gửi thành công.

SKIPPED:
Email không gửi trong môi trường hiện tại.

FAILED:
Email chính gửi thất bại.

PARTIAL:
Có nhiều email và chỉ một phần gửi thành công.
```

---

# 22. Frontend toast sau khi chỉnh sửa email

## Pending + SENT

```text
Đã cập nhật tài khoản và gửi liên kết xác nhận đến <email mới>.
Tài khoản sẽ được kích hoạt sau khi người nhận hoàn tất xác nhận email.
```

## Pending + FAILED

```text
Đã cập nhật tài khoản nhưng không thể gửi email xác nhận.
Tài khoản vẫn ở trạng thái chờ xác nhận email.
Bạn có thể sử dụng chức năng “Gửi lại email xác nhận”.
```

## Pending + SKIPPED

```text
Đã cập nhật tài khoản nhưng email xác nhận không được gửi trong môi trường hiện tại.
Bạn có thể sử dụng chức năng “Gửi lại email xác nhận”.
```

## ACTIVE/INACTIVE + SENT

```text
Đã cập nhật tài khoản và gửi email thông báo thay đổi.
```

## PARTIAL

```text
Đã cập nhật tài khoản nhưng một số email thông báo chưa gửi được.
```

## FAILED

```text
Đã cập nhật tài khoản nhưng không thể gửi email thông báo.
```

Không báo email đã gửi nếu delivery không phải `SENT`.

---

# 23. Permission backend

Cả resend và edit email phải kiểm tra:

```text
- Actor authenticated.
- Actor role STAFF.
- Actor sub-role LEADER.
- Actor có permission Account Management.
- Actor có campus.
- Target cùng campus.
- Target đúng role/sub-role scope.
- Target không phải chính actor.
- Target không LOCKED nếu rule không cho edit.
```

Endpoint:

```text
edit-pending-email
resend-email-confirmation
updateaccountrole
```

đều phải kiểm tra scope riêng.

Không dựa vào việc Staff Leader mở được modal.

---

# 24. SQL có cần sửa không?

Với yêu cầu này, không cần tạo template mới nếu dùng đúng các template có sẵn:

```text
ACCOUNT_EMAIL_CONFIRMATION
ACCOUNT_PENDING_EMAIL_CHANGED_OLD_NOTICE
ACCOUNT_EMAIL_CHANGED_OLD_NOTICE
ACCOUNT_EMAIL_CHANGED_NEW_NOTICE
ACCOUNT_ROLE_CHANGED
```

Do đó:

```text
- Không thêm template code mới.
- Không thêm row email_templates mới.
- Không ALTER TABLE.
- Không thêm migration schema.
```

Chỉ sửa canonical SQL nếu audit phát hiện code registry có template nhưng seed thiếu row hoặc nội dung bị drift.

Đây là sửa drift, không phải yêu cầu nghiệp vụ mới.

---

# 25. File dự kiến thay đổi

## Frontend

```text
frontend/pems-react/src/pages/dashboard/accounts/
AccountManagement.tsx

frontend/pems-react/src/shared/api/endpoints.ts

frontend/pems-react/src/features/account-management/
api/accountManagementApi.ts

frontend/pems-react/src/features/account-management/
types/accountManagement.types.ts
```

Có thể tách component:

```text
frontend/pems-react/src/features/account-management/
components/PendingAccountEmailActions.tsx
```

## Backend

```text
backend/PEMS.Application/Accounts/Queries/
ViewAccountDetails/ViewAccountDetailsDto.cs

backend/PEMS.Application/Accounts/Queries/
ViewAccountDetails/ViewAccountDetailsQueryHandler.cs

backend/PEMS.Application/Accounts/Commands/
UpdateAccountRole/UpdateAccountRoleCommandHandler.cs

backend/PEMS.Application/Accounts/Commands/
UpdateAccountRole/UpdateAccountRoleResponse.cs

backend/PEMS.Application/Accounts/Commands/
EditPendingAccountEmail/

backend/PEMS.Application/Accounts/Commands/
ResendAccountEmailConfirmation/

backend/PEMS.Application/Accounts/Common/
```

Shared service khuyến nghị:

```text
PendingAccountEmailChangeService.cs
```

## Tests

```text
tests/PEMS.UnitTests/Accounts/EmailConfirmation/

tests/PEMS.UnitTests/Accounts/UpdateAccountRole/

tests/PEMS.IntegrationTests/Accounts/

frontend account-management tests
```

---

# 26. Test resend bắt buộc

```text
1. Staff Leader cùng campus thấy nút resend.
2. Pending account đúng scope thấy nút.
3. ACTIVE không thấy nút.
4. INACTIVE không thấy nút.
5. LOCKED không thấy nút.
6. Account ngoài campus không thấy nút.
7. Direct API ngoài campus bị 403.
8. Target ngoài role scope bị 403.
9. Resend tạo token mới.
10. Token cũ không confirm được.
11. Chỉ một token PENDING.
12. resend_count tăng đúng.
13. Cooldown hoạt động.
14. Max resend hoạt động.
15. Account vẫn pending sau resend.
16. SENT hiển thị đúng.
17. SKIPPED hiển thị đúng.
18. FAILED hiển thị đúng.
19. Double-click chỉ gửi một request.
20. ACCOUNT_NOT_PENDING refetch detail.
```

---

# 27. Test pending email edit bắt buộc

```text
1. Pending + emailChanged dùng confirmation flow.
2. Email mới nhận ACCOUNT_EMAIL_CONFIRMATION.
3. Email có nút kích hoạt.
4. Email cũ nhận neutral notice.
5. Token cũ → SUPERSEDED.
6. Token mới target email đúng.
7. Account vẫn pending.
8. Role + email commit nguyên tử.
9. Confirmation email dùng role cuối cùng.
10. Không gửi ACCOUNT_ROLE_CHANGED dư thừa.
11. Email fail không rollback DB.
12. Frontend hướng dẫn dùng resend khi FAILED.
13. Auth providers được đồng bộ.
14. EmailVerifiedAt = null.
15. Sessions được revoke nếu cần.
```

---

# 28. Test ACTIVE/INACTIVE email edit

```text
1. Email cũ nhận ACCOUNT_EMAIL_CHANGED_OLD_NOTICE.
2. Email mới nhận ACCOUNT_EMAIL_CHANGED_NEW_NOTICE.
3. Local provider email được cập nhật.
4. SSO/FEID cũ được unlink.
5. EmailVerifiedAt = null.
6. Session được revoke.
7. Email unchanged không gửi email.
8. Role changed + email changed gửi đúng notice.
9. PARTIAL được phản ánh trung thực.
10. Email fail không rollback role/email.
```

---

# 29. Test authorization

```text
1. Staff Leader không sửa HO.
2. Không sửa Staff Leader khác.
3. Không sửa account ngoài campus.
4. Không sửa chính mình.
5. Không sửa target ngoài role/sub-role scope.
6. Direct API vẫn bị chặn.
7. Locked target bị chặn theo rule.
```

---

# 30. Manual verification

## 30.1. Resend pending account

1. Đăng nhập Staff Leader.
2. Mở account cùng campus có status pending.
3. Bấm View Detail.
4. Kiểm tra có nút resend.
5. Bấm nút.
6. Xác nhận dialog hiển thị đúng email.
7. Bấm xác nhận.
8. Kiểm tra loading/double-click.
9. Kiểm tra token cũ mất hiệu lực.
10. Kiểm tra email mới có link mới.
11. Kiểm tra account vẫn pending.
12. Confirm link mới.
13. Kiểm tra account ACTIVE.
14. Mở lại detail, nút resend biến mất.

## 30.2. Pending account đổi email

1. Mở pending account.
2. Chỉnh sửa email.
3. Đồng thời đổi role nếu cần.
4. Submit.
5. Kiểm tra DB commit nguyên tử.
6. Kiểm tra email mới nhận confirmation mail.
7. Kiểm tra email cũ nhận notice trung lập.
8. Kiểm tra role trong email là role cuối cùng.
9. Kiểm tra link cũ không dùng được.
10. Kiểm tra link mới confirm được.

## 30.3. ACTIVE/INACTIVE đổi email

1. Mở account ACTIVE hoặc INACTIVE.
2. Đổi email.
3. Submit.
4. Kiểm tra old/new notice.
5. Kiểm tra auth provider.
6. Kiểm tra session revoke.
7. Kiểm tra frontend toast theo delivery status.

---

# 31. Preflight bắt buộc

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

Nếu branch không đúng hoặc WIP ngoài task chưa rõ:

```text
Dừng và báo cáo.
```

---

# 32. Search/audit bắt buộc

```bash
rg -n   "ResendAccountEmailConfirmation|resend-email-confirmation|EditPendingAccountEmail|edit-pending-email|UpdateAccountRole|ACCOUNT_EMAIL_CONFIRMATION|ACCOUNT_EMAIL_CHANGED_NEW_NOTICE|ACCOUNT_ROLE_CHANGED"   frontend backend tests
```

```bash
rg -n   "PENDING_EMAIL_CONFIRMATION|canResendEmailConfirmation|canEditPendingEmail|IssuePendingAsync|EmailVerifiedAt|AuthProviders"   frontend backend tests
```

Phân loại:

```text
- Runtime frontend.
- Runtime backend.
- Test.
- Email registry.
- Template seed.
- Docs.
- Legacy.
```

---

# 33. Build và test gate

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

Dùng script thật trong `package.json`.

## Static

```bash
git diff --check
```

---

# 34. Không được làm

```text
- Không tạo resend endpoint riêng cho Staff Leader.
- Không tạo template mới khi template hiện có đủ.
- Không gọi hai API tuần tự cho role + email.
- Không gửi notice thường cho pending email mới.
- Không gửi ACCOUNT_ROLE_CHANGED dư thừa cho pending.
- Không để token cũ còn hiệu lực.
- Không lưu raw token.
- Không log full confirmation URL.
- Không bỏ backend scope check.
- Không tin permission frontend.
- Không tự ACTIVE account khi edit/resend.
- Không đổi schema nếu không cần.
- Không chuyển branch.
- Không reset WIP.
```

---

# 35. Definition of Done

```text
[ ] Branch = Duy-Iter1.
[ ] WIP được bảo toàn.
[ ] Pending detail của Staff Leader có nút resend.
[ ] Nút dựa trên detail status và backend permission.
[ ] ACTIVE/INACTIVE/LOCKED không hiện nút.
[ ] Staff Leader dùng chung resend endpoint với HO.
[ ] Cooldown và max resend được giữ.
[ ] Token cũ bị supersede.
[ ] Pending email edit gửi ACCOUNT_EMAIL_CONFIRMATION.
[ ] Pending email edit không gửi notice thường tới email mới.
[ ] Email mới có nút kích hoạt.
[ ] Email cũ nhận notice trung lập.
[ ] Role + identity + token commit nguyên tử.
[ ] Confirmation mail dùng role cuối cùng.
[ ] ACTIVE/INACTIVE email edit gửi old/new notices.
[ ] Auth providers được đồng bộ.
[ ] Sessions được revoke.
[ ] Response trả delivery status trung thực.
[ ] Frontend xử lý SENT/SKIPPED/FAILED/PARTIAL.
[ ] Không tạo endpoint trùng.
[ ] Không tạo template mới.
[ ] Không cần sửa SQL nếu catalog đủ.
[ ] Backend unit tests xanh.
[ ] Backend integration tests xanh.
[ ] Frontend tests xanh.
[ ] Frontend type-check xanh.
[ ] Frontend build xanh.
[ ] Backend build xanh.
[ ] git diff --check xanh.
```

---

# 36. Mẫu báo cáo cuối cùng Agent phải trả

```markdown
# Kết quả triển khai Staff Leader pending email flows

## 1. Preflight
- Branch:
- HEAD:
- Working tree:
- WIP được bảo toàn:
- git diff --check:

## 2. Audit
- Resend endpoint hiện có:
- Pending email edit hiện có:
- UpdateAccountRole flow:
- Permission scope:
- Template catalog:
- SQL có cần sửa không:

## 3. File đã sửa

### Frontend
- ...

### Backend
- ...

### Tests
- ...

## 4. Resend flow
- Điều kiện hiển thị:
- Permission flag:
- Cooldown:
- Max resend:
- Token supersede:
- Delivery mapping:

## 5. Pending email edit
- Transaction:
- Role/email atomicity:
- Template:
- Token:
- Old-email notice:
- Final role variables:

## 6. ACTIVE/INACTIVE email edit
- Old-email notice:
- New-email notice:
- Auth providers:
- Session revoke:
- Role-change email:

## 7. Security
- Raw token:
- Active token count:
- Scope:
- Logging:
- Direct API:

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

# 37. Lệnh giao việc ngắn gọn

```text
Đọc toàn bộ file này trước khi sửa.

Tiếp tục làm việc trên nhánh Duy-Iter1. Không chuyển sang Dev, không tạo nhánh mới và không reset WIP.

Thực hiện hai yêu cầu cho trang Quản lý tài khoản của Staff Leader:

1. Với detail account có status PENDING_EMAIL_CONFIRMATION và backend xác nhận Staff Leader có quyền quản lý, hiển thị nút "Gửi lại email xác nhận". Tái sử dụng endpoint resend-email-confirmation hiện có, giữ cooldown, max resend, token supersede và delivery status trung thực.

2. Khi Staff Leader chỉnh sửa email:
   - Pending account phải nhận ACCOUNT_EMAIL_CONFIRMATION có token mới và nút kích hoạt.
   - Email cũ nhận ACCOUNT_PENDING_EMAIL_CHANGED_OLD_NOTICE trung lập.
   - Role + identity + token phải commit trong một transaction.
   - ACTIVE/INACTIVE account dùng ACCOUNT_EMAIL_CHANGED_OLD_NOTICE và ACCOUNT_EMAIL_CHANGED_NEW_NOTICE giống flow HO.
   - Đồng bộ auth providers, reset EmailVerifiedAt và revoke sessions.
   - Không gọi hai API tuần tự cho role + email.
   - Không tạo template mới và không sửa SQL nếu catalog hiện tại đủ.

Chạy build/test đầy đủ và trả báo cáo theo mẫu cuối file.
```
