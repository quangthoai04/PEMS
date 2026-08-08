# PEMS — ADMIN ACCOUNT MANAGEMENT: GLOBAL READ + SECURITY LOCK ONLY

> **Implementation specification / AI Agent prompt**
>
> Mục tiêu của tài liệu này là hướng dẫn AI Agent cập nhật màn **Quản lý tài khoản** của PEMS để role `ADMIN` chỉ còn:
>
> 1. **Xem toàn bộ tài khoản trên toàn hệ thống**
> 2. **Tìm kiếm / lọc / xem thống kê**
> 3. **Xem chi tiết tài khoản ở chế độ read-only**
> 4. **Khóa bảo mật / mở khóa bảo mật**
> 5. **Điều hướng sang các module quản trị bảo mật hiện có như Phiên đăng nhập / Bảo mật / Nhật ký kiểm toán**
>
> `ADMIN` **KHÔNG còn là “super HO”** và **KHÔNG được làm nghiệp vụ quản lý nhân sự/tài khoản** như tạo tài khoản, sửa thông tin, đổi role/campus/department, kích hoạt/vô hiệu hóa nghiệp vụ, xử lý pending confirmation...
>
> **Nguyên tắc chính:**  
> `ADMIN = Global Read + Security Control`  
> `HO / STAFF_LEADER = Account Business Management`
>
> Không over-engineer. Không tạo architecture mới. Không thêm bảng/cột database nếu không thật sự bắt buộc. Ưu tiên tận dụng flow/API/service hiện có.

---

# 0. BASELINE / SOURCE OF TRUTH

Tại thời điểm lập tài liệu này, nhánh `Dev` được rà soát ở HEAD:

```text
a4f63427c514da8004134f7889b7d6cf09328ba7
```

Trước khi sửa code, AI Agent **BẮT BUỘC**:

1. Checkout/pull nhánh `Dev` mới nhất.
2. Ghi lại `git rev-parse HEAD`.
3. Kiểm tra working tree.
4. Đọc code hiện tại thay vì giả định tài liệu này vẫn khớp 100% nếu HEAD đã thay đổi.
5. Nếu code mới đã sửa một phần yêu cầu này, chỉ bổ sung phần còn thiếu.
6. Không rollback/refactor các thay đổi mới hơn trên `Dev`.

Các file đã xác nhận liên quan trực tiếp:

```text
frontend/pems-react/src/pages/dashboard/accounts/AccountManagement.tsx
frontend/pems-react/src/features/account-management/api/accountManagementApi.ts
frontend/pems-react/src/features/account-management/types/accountManagement.types.ts
frontend/pems-react/src/shared/auth/dashboardRouteAccess.ts

backend/PEMS.Api/Controllers/AccountsController.cs

backend/PEMS.Application/Accounts/Common/AccountListQueryExecutor.cs
backend/PEMS.Application/Accounts/Common/AccountProvisioningRules.cs

backend/PEMS.Application/Accounts/Queries/ViewAccountStatistics/ViewAccountStatisticsQueryHandler.cs
backend/PEMS.Application/Accounts/Queries/ViewAccountDetails/ViewAccountDetailsQueryHandler.cs

backend/PEMS.Application/Accounts/Commands/CreateAccount/CreateAccountCommandHandler.cs
backend/PEMS.Application/Accounts/Commands/ManageAccountStatus/ManageAccountStatusCommandHandler.cs
backend/PEMS.Application/Accounts/Commands/ManageAccountStatus/ManageAccountStatusCommandValidator.cs
backend/PEMS.Application/Accounts/Commands/UpdateAccountRole/UpdateAccountRoleCommandHandler.cs
```

Ngoài ra phải tìm và đọc các file hiện hữu sau trước khi sửa:

```text
AccountErrorCodes
AccountStatusConfirmModal
account-management hooks
account-management frontend tests
Accounts unit tests
Accounts integration tests
session/admin-security/audit related code
```

---

# 1. HIỆN TRẠNG ĐÃ XÁC NHẬN

## 1.1 Route

`/dashboard/accounts` hiện được dùng chung cho:

```text
ADMIN
HO
STAFF_LEADER
```

Điều này **GIỮ NGUYÊN**.

Không xóa quyền truy cập route của ADMIN.

ADMIN vẫn cần màn `Quản lý tài khoản` để quan sát toàn hệ thống.

---

## 1.2 Account list hiện tại

`AccountListQueryExecutor` hiện đã có scope:

- `ADMIN`: xem system-wide.
- `HO`: chỉ scope HO + Staff Leader.
- `STAFF_LEADER`: scope campus và target role phù hợp.

**GIỮ nguyên global-read của ADMIN.**

Không sửa `AccountProvisioningRules.IsPrivileged()` chỉ để cắt quyền mutate của ADMIN.

### CỰC KỲ QUAN TRỌNG

Không làm kiểu:

```csharp
IsPrivileged(role) => role == HO;
```

vì `ADMIN` vẫn cần privileged/global READ ở account list và có thể còn dependency ở module khác.

**Phải cắt quyền theo từng operation**, không thay đổi ý nghĩa global của `IsPrivileged`.

---

## 1.3 ADMIN hiện đang có quyền quá rộng

Code hiện tại cho ADMIN:

- tạo nhiều loại tài khoản;
- đổi role/campus/department;
- thao tác status rộng;
- frontend có nút `Tạo tài khoản mới`;
- frontend có action edit/status trong cùng màn HO/Staff Leader.

Đây là phần cần thu hẹp.

---

# 2. BUSINESS OWNERSHIP MỚI

Phải chốt ranh giới trách nhiệm như sau.

| Chức năng | ADMIN | HO | STAFF LEADER |
|---|---:|---:|---:|
| Xem danh sách tài khoản toàn hệ thống | ✅ | ❌ scope riêng | ❌ scope campus |
| Tìm kiếm / lọc | ✅ | ✅ | ✅ |
| Xem chi tiết | ✅ | ✅ scope | ✅ scope |
| Tạo tài khoản | ❌ | ✅ theo scope | ✅ theo scope |
| Sửa họ tên/email | ❌ | ✅ theo flow hiện có | ✅ theo flow hiện có |
| Đổi role/sub-role | ❌ | Giữ behavior hiện tại | ✅ theo scope |
| Đổi campus/department | ❌ | Giữ behavior hiện tại | ✅ theo scope |
| ACTIVE ↔ INACTIVE | ❌ | ✅ theo scope | ✅ theo scope |
| Security LOCK | ✅ | ❌ | ❌ |
| Security UNLOCK | ✅ | ❌ | ❌ |
| Resend pending confirmation | ❌ | ✅ nếu flow hiện tại cho phép | ✅ nếu flow hiện tại cho phép |
| Edit pending email | ❌ | ✅ nếu flow hiện tại cho phép | ✅ nếu flow hiện tại cho phép |
| Cancel pending account | ❌ | ✅ nếu flow hiện tại cho phép | ✅ nếu flow hiện tại cho phép |
| Replace Staff Leader | ❌ | ✅ HO | ❌ |
| Reset password thay người dùng | ❌ | Không mở thêm | Không mở thêm |
| Xem session/security/audit | ✅ | ❌ | ❌ |

Không mở thêm quyền mới cho HO/Staff Leader trong task này.

Mục tiêu là **thu hẹp ADMIN**, không thiết kế lại toàn bộ account module.

---

# 3. Ý NGHĨA TRẠNG THÁI PHẢI TÁCH RÕ

## 3.1 `ACTIVE`

Account đang được phép sử dụng hệ thống.

## 3.2 `INACTIVE`

Đây là **business/personnel status**.

Ví dụ:

- nhân sự nghỉ việc;
- tạm ngừng sử dụng;
- không còn thuộc scope vận hành.

Người quản lý:

```text
HO / STAFF_LEADER
```

ADMIN chỉ xem.

---

## 3.3 `PENDING_EMAIL_CONFIRMATION`

Đây là lifecycle của account chưa xác nhận email.

ADMIN chỉ xem.

ADMIN không được:

- activate;
- resend confirmation;
- sửa pending email;
- cancel pending account.

Những flow đó thuộc actor nghiệp vụ đã được hệ thống quy định.

---

## 3.4 `LOCKED`

Từ sau thay đổi này, trên Account Management cần coi `LOCKED` là **security state**.

Ví dụ:

- suspicious login;
- suspected compromise;
- security investigation;
- security policy violation;
- automatic/security lock cần admin review.

Người xử lý:

```text
ADMIN
```

HO/Staff Leader không được unlock/lock security.

---

# 4. QUY TẮC SECURITY LOCK TỐI GIẢN — KHÔNG THÊM DB COLUMN

Để tránh thêm schema cho `previous_status`, áp dụng rule đơn giản:

## ADMIN được phép

```text
ACTIVE -> LOCKED
LOCKED -> ACTIVE
```

## ADMIN không được phép

```text
ACTIVE -> INACTIVE
INACTIVE -> ACTIVE
INACTIVE -> LOCKED
PENDING_EMAIL_CONFIRMATION -> LOCKED
PENDING_EMAIL_CONFIRMATION -> ACTIVE
LOCKED -> INACTIVE
```

Lý do:

- `INACTIVE` đã không sử dụng được nên không cần security-lock chồng lên.
- Không cần lưu `status_before_lock`.
- Unlock luôn rõ nghĩa: `LOCKED -> ACTIVE`.
- Không thêm bảng/cột database.
- Ít thay đổi nhất.

Nếu code hiện tại có automatic temporary lock dùng `LockedUntil`/`FailedLoginCount`, ADMIN unlock về `ACTIVE` được phép reset lockout counters theo behavior đang có.

---

# 5. ADMIN ACCOUNT MANAGEMENT UI — TARGET DESIGN

## 5.1 Header

Giữ:

```text
Quản lý tài khoản
```

Subtitle cho ADMIN nên mang nghĩa quan sát/bảo mật, ví dụ:

```text
Theo dõi tài khoản và xử lý các vấn đề bảo mật trên toàn hệ thống.
```

Không dùng wording kiểu:

```text
Tạo và quản lý nhân sự
Phân quyền nhân sự
```

---

## 5.2 Statistics

Giữ 4 thẻ hiện tại:

```text
Tổng số tài khoản
Tài khoản hoạt động
Tài khoản vô hiệu hóa
Tài khoản bị khóa
```

ADMIN statistics vẫn system-wide.

Không bắt buộc thêm card mới.

Nếu muốn hiển thị pending thì chỉ làm khi UI hiện tại đã có chỗ phù hợp; **không mở scope chỉ để thêm card**.

---

## 5.3 Search / Filter

ADMIN vẫn được:

```text
Tìm theo họ tên / MSSV / Email
Cơ sở
Loại tài khoản
Vai trò
Trạng thái
```

Campus filter của ADMIN mặc định:

```text
Toàn quốc
```

### Không hard-code campus

Trong `AccountManagement.tsx` hiện có:

```ts
const CAMPUSES = [...]
```

Nếu danh sách này còn được dùng cho dropdown thực tế, phải ưu tiên API `getActiveCampuses()` hiện có thay vì hard-code 5 campus.

Không tạo API mới nếu `getActiveCampuses()` đã đủ.

---

# 6. BẢNG ACCOUNT CHO ADMIN

Giữ các cột chính:

```text
STT
Họ và tên
Tên đăng nhập (Email)
Cơ sở
Vai trò
Tình trạng đăng nhập
Trạng thái
Hành động
```

## 6.1 Action của ADMIN

Chỉ còn:

```text
👁 Xem chi tiết
🔒 Khóa bảo mật
🔓 Mở khóa bảo mật
```

### ACTIVE account

Hiển thị:

```text
👁  🔒
```

### LOCKED account

Hiển thị:

```text
👁  🔓
```

### INACTIVE account

Chỉ:

```text
👁
```

Không cho security lock vì account đã inactive.

### PENDING_EMAIL_CONFIRMATION

Chỉ:

```text
👁
```

Không cho ADMIN xử lý confirmation.

### Account của chính ADMIN đang đăng nhập

Chỉ:

```text
👁
```

Không cho self-lock/self-unlock.

---

# 7. XÓA CÁC CONTROL NGHIỆP VỤ KHỎI UI ADMIN

Trong `AccountManagement.tsx`, khi effective role là `ADMIN`:

## BẮT BUỘC ẩn

```text
Tạo tài khoản mới
Edit profile / Chỉnh sửa thông tin
Chỉnh sửa vai trò
Role/campus/department editor
ACTIVE/INACTIVE toggle
Resend email confirmation
Edit pending email
Cancel pending account
Replace Staff Leader
Reset password action
Bất kỳ action mutate nhân sự nào khác
```

### Không xóa component/API dùng chung

Ví dụ:

- `createAccount()` vẫn cần HO/Staff Leader.
- `updateAccountRole()` vẫn cần actor khác.
- create modal vẫn cần HO/Staff Leader.
- status modal vẫn có thể cần HO/Staff Leader.

Chỉ render đúng theo role/capability.

Không delete shared feature chỉ vì ADMIN không dùng nữa.

---

# 8. ADMIN DETAIL DRAWER — READ ONLY

ADMIN bấm `Xem chi tiết`.

Drawer/modal vẫn mở như hiện tại nhưng **read-only**.

## 8.1 Thông tin cơ bản

Hiển thị nếu safe DTO đã có:

```text
Họ và tên
Email
Số điện thoại
Giới tính
Quốc tịch
MSSV
```

Không có Edit button.

---

## 8.2 Thông tin tổ chức

```text
Vai trò
Sub-role / Chức vụ
Cơ sở
Phòng ban
```

Read-only.

Không có:

```text
Đổi vai trò
Chuyển campus
Chuyển phòng ban
```

---

## 8.3 Thông tin account

```text
Trạng thái
Nguồn tạo
Ngày tạo
Ngày cập nhật
Lần đăng nhập cuối
Authentication providers
```

Ưu tiên dùng fields đang có trong DTO.

Không thêm backend query nếu không cần.

---

## 8.4 Security information

Có thể hiển thị các field **không nhạy cảm** nếu DTO hiện tại đã có hoặc có thể bổ sung nhỏ:

```text
Trạng thái bảo mật
Số lần đăng nhập thất bại
LockedUntil
LastLoginAt
Auth provider
```

### TUYỆT ĐỐI KHÔNG trả ra frontend

```text
PasswordHash
RefreshToken
AccessToken
OTP
TokenHash
Google secret/token
API credentials
encryption key
confirmation token
session secret
```

---

# 9. SESSION / SECURITY / AUDIT NAVIGATION

ADMIN đã có các module:

```text
Phiên đăng nhập
Bảo mật
Nhật ký kiểm toán
Quản lý API
```

Không duplicate cả module session/security vào Account Detail.

Nếu hợp lý với code hiện tại, Account Detail có thể có CTA:

```text
Xem phiên đăng nhập
Xem nhật ký bảo mật
```

và điều hướng sang module hiện có.

Nếu route hiện tại chưa hỗ trợ filter `userId`, không tạo một subsystem mới chỉ vì CTA này.

Có thể để CTA mở module tổng nếu việc thêm query filter gây scope lớn.

---

# 10. BACKEND PHẢI LÀ SECURITY SOURCE OF TRUTH

Ẩn button frontend **KHÔNG đủ**.

Direct API call của ADMIN tới endpoint mutate nghiệp vụ phải bị từ chối.

---

# 11. CREATE ACCOUNT — ADMIN PHẢI BỊ CHẶN

File:

```text
backend/PEMS.Application/Accounts/Commands/CreateAccount/CreateAccountCommandHandler.cs
```

Hiện có nhánh explicit:

```csharp
else if (_currentUser.RoleCode == RoleCodes.Admin)
{
    ...
}
```

Sau thay đổi:

## ADMIN

```text
403 Forbidden
```

với stable error code, ví dụ theo convention hiện có:

```text
ADMIN_ACCOUNT_CREATION_NOT_ALLOWED
```

Message:

```text
ADMIN chỉ được xem tài khoản và xử lý khóa bảo mật; không được tạo tài khoản.
```

### Guard phải chạy sớm

Không thực hiện:

- DB mutation;
- email normalization không cần thiết;
- slot validation;
- confirmation creation;
- email send;
- audit create;
- notification.

### Allowed caller sau thay đổi

Giữ đúng business flow hiện tại của:

```text
HO
STAFF_LEADER
```

Nếu handler hiện còn fallback cho các role khác chỉ nhờ route UI chặn, audit và fail-closed:

```text
ADMIN -> 403
HO -> flow hiện có
STAFF/LEADER -> flow hiện có
mọi role khác -> 403
```

Không mở rộng scope.

---

# 12. UPDATE ROLE — ADMIN PHẢI BỊ CHẶN

File:

```text
backend/PEMS.Application/Accounts/Commands/UpdateAccountRole/UpdateAccountRoleCommandHandler.cs
```

Hiện có:

```csharp
var isAdminCaller = ...
...
ResolveAdminRoleEditAsync(...)
```

Sau thay đổi:

## ADMIN

```text
403 Forbidden
```

trước transaction/lock/mutation nếu có thể.

Stable code gợi ý:

```text
ADMIN_ACCOUNT_EDIT_NOT_ALLOWED
```

ADMIN không được đổi:

```text
role
subRole
campus
department
studentCode
fullName
email
```

### Sau khi chặn ADMIN

Search usages của:

```text
ResolveAdminRoleEditAsync
```

Nếu không còn caller:

- có thể xóa private method dead code;
- chỉ xóa sau khi xác nhận không dùng nơi khác;
- không refactor ngoài scope.

HO/Staff Leader behavior không đổi nếu không liên quan.

---

# 13. BASIC INFO EDIT — ADMIN KHÔNG ĐƯỢC DÙNG

Endpoint hiện có:

```text
POST /api/accounts/updatebasicaccountinfo
```

Comment/code hiện hướng HO-only.

AI Agent phải verify handler.

Nếu handler đã explicit HO-only:

```text
không sửa
```

Nếu ADMIN có thể gọi trực tiếp:

```text
thêm backend guard 403
```

Không thay đổi behavior HO.

---

# 14. PENDING EMAIL FLOW — ADMIN READ ONLY

Các endpoint hiện có:

```text
POST /api/accounts/resend-email-confirmation
POST /api/accounts/edit-pending-email
POST /api/accounts/cancel-pending-account
```

ADMIN không được dùng.

AI Agent phải đọc từng handler.

Nếu đã chặn ADMIN đúng:

```text
giữ nguyên
```

Nếu chưa chặn:

```text
thêm explicit authorization fail-closed
```

Frontend ADMIN phải ẩn toàn bộ button liên quan.

---

# 15. REPLACE STAFF LEADER — ADMIN KHÔNG ĐƯỢC DÙNG

Endpoint:

```text
POST /api/accounts/replacestaffleader
```

Đây là nghiệp vụ HO.

Verify handler HO-only.

ADMIN direct call phải trả 403.

Không hiển thị action này trong ADMIN UI.

---

# 16. MANAGE STATUS — TÁCH BUSINESS STATUS VÀ SECURITY STATUS

Ưu tiên **reuse endpoint hiện có**, không tạo endpoint mới nếu không cần:

```text
POST /api/accounts/manageaccountstatus
```

File:

```text
ManageAccountStatusCommandHandler.cs
ManageAccountStatusCommandValidator.cs
```

## 16.1 ADMIN behavior

Chỉ cho:

```text
ACTIVE -> LOCKED
LOCKED -> ACTIVE
```

Cả hai đều yêu cầu `reason`.

Pseudo logic:

```csharp
if (caller == ADMIN)
{
    EnsureNotSelf();

    if (string.IsNullOrWhiteSpace(request.Reason))
        throw validation;

    if (current == ACTIVE && requested == LOCKED)
        SecurityLock();

    else if (current == LOCKED && requested == ACTIVE)
        SecurityUnlock();

    else
        throw business rule;
}
```

Không cho ADMIN dùng endpoint này để:

```text
ACTIVE <-> INACTIVE
```

---

## 16.2 HO behavior

Giữ behavior hiện tại.

Chỉ business status phù hợp:

```text
ACTIVE <-> INACTIVE
```

Không được:

```text
-> LOCKED
LOCKED -> anything
```

---

## 16.3 Staff Leader behavior

Giữ behavior hiện tại và target scope hiện tại.

Chỉ:

```text
ACTIVE <-> INACTIVE
```

Không security lock/unlock.

---

## 16.4 Mọi role khác

Fail closed:

```text
403
```

Không được chỉ dựa vào việc route `/dashboard/accounts` không hiện.

---

# 17. SECURITY LOCK FLOW

ADMIN bấm:

```text
Khóa bảo mật
```

Mở confirm modal.

## 17.1 Modal

Hiển thị:

```text
Khóa tài khoản vì lý do bảo mật?

Họ tên: ...
Email: ...
Vai trò: ...
Cơ sở: ...
```

Bắt buộc:

```text
Lý do khóa *
```

Có thể dùng predefined reason ở frontend:

```text
Phát hiện đăng nhập bất thường
Nghi ngờ tài khoản bị xâm nhập
Yêu cầu điều tra bảo mật
Vi phạm chính sách bảo mật
Khác
```

Nếu `Khác`, yêu cầu nhập mô tả.

Backend chỉ cần nhận `Reason` string hiện có.

Không thêm bảng reason lookup.

---

# 18. KHI LOCK THÀNH CÔNG

Trong cùng logical operation:

```text
Validate ADMIN
Validate target != self
Validate current status == ACTIVE
Validate requested status == LOCKED
Validate reason
Set users.status = LOCKED
Update UpdatedAt / UpdatedBy
Save audit
Revoke all active sessions
Create existing safe notification if current architecture already uses it
Return success
```

## 18.1 Session

BẮT BUỘC revoke toàn bộ active sessions.

Reuse:

```text
ISessionService.RevokeAllActiveSessionsAsync(...)
```

Không tự viết session revoke mới.

User đang online phải bị chặn ở request tiếp theo bởi session/account status validation hiện có.

---

# 19. SECURITY UNLOCK FLOW

ADMIN bấm:

```text
Mở khóa bảo mật
```

Modal:

```text
Mở khóa tài khoản?
```

Bắt buộc reason.

Ví dụ:

```text
Đã xác minh chủ tài khoản
Không phát hiện rủi ro
Điều tra hoàn tất
Khác
```

Backend:

```text
LOCKED -> ACTIVE
```

Khi set ACTIVE:

- reset `FailedLoginCount` nếu behavior hiện tại đã làm;
- clear `LockedUntil` nếu behavior hiện tại đã làm;
- không restore old sessions;
- user phải login lại.

---

# 20. SELF PROTECTION

ADMIN không được:

```text
khóa chính mình
mở khóa chính mình qua account list
```

Giữ self guard ở backend.

Frontend cũng không render security action trên row hiện tại.

Backend vẫn là lớp cuối.

---

# 21. AUDIT LOG

Hiện status flow ghi:

```text
MANAGE_ACCOUNT_STATUS
```

Đối với ADMIN security operation nên phân biệt rõ:

```text
SECURITY_LOCK_ACCOUNT
SECURITY_UNLOCK_ACCOUNT
```

Reuse `AuditLog` / `AuditLogChange`.

Không thêm bảng.

Audit tối thiểu cần có:

```text
ActorUserId
Target UserId
Action
Old status
New status
Reason
CreatedAt
```

Nếu request metadata service hiện có và audit convention đã lưu IP/UserAgent thì reuse.

Không tạo cách log riêng.

---

# 22. NOTIFICATION / EMAIL

Không tạo email template mới cho task này.

Nếu `ManageAccountStatusCommandHandler` hiện đã tạo notification:

- giữ notification;
- điều chỉnh message cho security lock/unlock nếu cần.

Ví dụ:

Lock:

```text
Tài khoản của bạn đã bị khóa vì lý do bảo mật.
```

Unlock:

```text
Tài khoản của bạn đã được mở khóa và có thể đăng nhập lại.
```

Không gửi credential/token.

---

# 23. CAPABILITY FLAGS — KHÔNG ĐỂ FRONTEND TỰ ĐOÁN QUÁ NHIỀU

Hiện `AccountListQueryExecutor` đã trả các capability như:

```text
CanViewDetails
CanUpdateRole
CanManageStatus
HideStatusToggleReason
IsCurrentUser
CanEditBasicInfo
```

Nên tiếp tục pattern này.

## Đề xuất bổ sung rõ nghĩa

Trong account list DTO/type:

```text
CanSecurityLock
CanSecurityUnlock
SecurityActionDisabledReason
```

### ADMIN

| Current status | CanSecurityLock | CanSecurityUnlock |
|---|---:|---:|
| ACTIVE | true, nếu không self | false |
| LOCKED | false | true, nếu không self |
| INACTIVE | false | false |
| PENDING_EMAIL_CONFIRMATION | false | false |

### HO / Staff Leader

```text
CanSecurityLock = false
CanSecurityUnlock = false
```

## ADMIN business capability

```text
CanUpdateRole = false
CanEditBasicInfo = false
CanManageStatus = false
```

Ở đây `CanManageStatus` nên tiếp tục mang nghĩa **business ACTIVE/INACTIVE toggle**.

Không overload nó để vừa mang nghĩa business status vừa security lock.

---

# 24. FRONTEND KHÔNG DÙNG `localStorage` LÀ SECURITY DECISION

`AccountManagement.tsx` hiện có logic đọc:

```ts
localStorage.getItem("currentUser")
```

Không cần refactor toàn bộ auth trong task này nếu blast radius lớn.

Nhưng:

- backend phải authoritative;
- nếu frontend cần xác định role để render, ưu tiên existing `useAuth()` / `effectiveRole` nếu tích hợp nhỏ;
- không coi `localStorage` là permission boundary;
- capability từ backend nên quyết định row actions.

Nếu việc đổi toàn page sang AuthContext làm thay đổi lớn, chỉ sửa phần cần thiết và để backend enforce.

---

# 25. ACCOUNT LIST DTO / FRONTEND TYPE

Nếu bổ sung capability fields, cập nhật đồng bộ:

```text
Backend DTO
AccountListQueryExecutor
frontend accountManagement.types.ts
AccountManagement.tsx
tests
```

Không thay đổi API shape không liên quan.

Không rename field cũ nếu không cần.

---

# 26. ACCOUNT DETAIL DTO

Ưu tiên reuse safe projection hiện tại.

Chỉ bổ sung field nếu ADMIN detail thực sự cần và source đã có.

Không mở rộng DTO bằng dữ liệu nhạy cảm.

Nếu action UI có thể quyết định bằng capability từ selected list row thì không cần duplicate capability vào detail DTO.

Nhưng nếu detail được load độc lập và đang tự render action theo detail DTO, phải update cho nhất quán.

Agent phải đọc code thực tế trước khi quyết định.

---

# 27. API CONTROLLER

`AccountsController` dùng chung endpoint.

Không bắt buộc tạo route mới.

Ưu tiên:

```text
giữ route
siết handler
```

Controller route access không thay thế handler authorization.

Không thêm `[RoleAuthorize(Admin)]` lên các endpoint shared vì sẽ làm hỏng HO/Staff Leader.

---

# 28. KHÔNG THAY ĐỔI ROUTE ACCESS

Trong:

```text
frontend/pems-react/src/shared/auth/dashboardRouteAccess.ts
```

giữ:

```text
ACCOUNT_LIST:
ADMIN
HO
STAFF_LEADER
```

ADMIN vẫn phải vào được Account Management.

Các route:

```text
ADMIN_SESSIONS
ADMIN_SECURITY
API_MANAGEMENT
ADMIN_AUDIT_LOGS
```

giữ ADMIN-only.

---

# 29. KHÔNG THAY ĐỔI STATISTICS SCOPE

`ViewAccountStatisticsQueryHandler` hiện:

```text
ADMIN -> system-wide
HO -> HO + Staff Leader
Staff Leader -> campus scope
```

Giữ nguyên.

Không vì cắt mutate quyền ADMIN mà cắt khả năng xem statistics.

---

# 30. KHÔNG THAY ĐỔI ACCOUNT LIST GLOBAL READ CỦA ADMIN

`AccountListQueryExecutor` phải tiếp tục:

```text
ADMIN -> system-wide
```

ADMIN vẫn cần lọc:

```text
campus
role
status
accountType
keyword
```

Không thu hẹp ADMIN về campus của chính admin.

---

# 31. ERROR CODE / ERROR MESSAGE

Reuse `AccountErrorCodes` nếu có.

Không trả raw exception.

Các stable code đề xuất nếu chưa có code tương đương:

```text
ADMIN_ACCOUNT_CREATION_NOT_ALLOWED
ADMIN_ACCOUNT_EDIT_NOT_ALLOWED
ADMIN_BUSINESS_STATUS_CHANGE_NOT_ALLOWED
SECURITY_LOCK_INVALID_STATE
SECURITY_UNLOCK_INVALID_STATE
SECURITY_REASON_REQUIRED
ACCOUNT_SECURITY_SELF_ACTION_FORBIDDEN
```

Không cần tạo đủ tất cả nếu existing generic code phù hợp.

Ưu tiên tái sử dụng convention hiện tại.

---

# 32. HTTP SEMANTICS

Gợi ý:

## 403

Caller role không được phép operation:

```text
ADMIN gọi create
ADMIN gọi update role
Role khác gọi manage account
```

## 400/422

Payload/transition không hợp lệ:

```text
thiếu reason
ACTIVE -> INACTIVE bởi ADMIN
INACTIVE -> LOCKED
PENDING -> LOCKED
```

## 404

Target user không tồn tại theo convention hiện tại.

## 409

Chỉ dùng nếu existing convention dùng conflict cho state transition/lifecycle.

Không đổi toàn bộ exception mapping chỉ vì task này.

---

# 33. FRONTEND SECURITY ACTION MODAL

Ưu tiên reuse:

```text
AccountStatusConfirmModal
```

nếu component có thể parameterize:

```text
title
description
reason
confirmLabel
variant
```

Nếu reuse làm component rối hơn hoặc ảnh hưởng HO/SL, tạo một component nhỏ riêng:

```text
AccountSecurityActionConfirmModal
```

Chỉ tạo mới khi cần.

Không tạo abstraction chung lớn.

---

# 34. UI COPY ĐỀ XUẤT

## Lock

```text
Khóa tài khoản vì lý do bảo mật?

Tài khoản sẽ bị đăng xuất khỏi tất cả phiên đang hoạt động và không thể đăng nhập cho đến khi được mở khóa.
```

Button:

```text
Khóa bảo mật
```

## Unlock

```text
Mở khóa tài khoản?

Người dùng sẽ có thể đăng nhập lại. Các phiên cũ không được khôi phục.
```

Button:

```text
Mở khóa
```

## Invalid inactive

Không render action.

Tooltip nếu cần:

```text
Tài khoản đang vô hiệu hóa theo nghiệp vụ nên không cần khóa bảo mật.
```

## Pending

Không render action.

Tooltip nếu cần:

```text
Tài khoản đang chờ xác nhận email.
```

---

# 35. KHÔNG HIỂN THỊ NÚT DISABLED DÀY ĐẶC

Ở table:

- ACTIVE -> lock icon.
- LOCKED -> unlock icon.
- INACTIVE/PENDING/self -> chỉ Eye.

Không cần render nhiều icon disabled làm rối UI.

Nếu accessibility cần explanation, có tooltip ở status/detail.

---

# 36. FLOW ADMIN CUỐI CÙNG

```text
ADMIN
  |
  v
/dashboard/accounts
  |
  +-- Statistics toàn hệ thống
  |
  +-- Search / Filter
  |
  +-- Account list toàn hệ thống
          |
          +-- View detail
          |      |
          |      +-- Basic info (readonly)
          |      +-- Organization (readonly)
          |      +-- Account metadata (readonly)
          |      +-- Auth provider (readonly)
          |      +-- Security status
          |      +-- Link tới session/security/audit nếu phù hợp
          |
          +-- ACTIVE
          |      |
          |      +-- Security Lock
          |             |
          |             +-- reason required
          |             +-- ACTIVE -> LOCKED
          |             +-- revoke sessions
          |             +-- audit
          |
          +-- LOCKED
                 |
                 +-- Security Unlock
                        |
                        +-- reason required
                        +-- LOCKED -> ACTIVE
                        +-- reset lock counters theo logic hiện có
                        +-- old sessions không restore
                        +-- audit
```

Không có nhánh:

```text
Create
Edit info
Edit role
Change campus
Change department
ACTIVE/INACTIVE business toggle
Pending confirmation management
Replace leader
Reset another user's password
```

---

# 37. BACKEND FLOW — SECURITY LOCK

```text
POST manageaccountstatus
        |
        v
Authenticated?
        |
        v
Caller == ADMIN?
        |
        v
Target exists?
        |
        v
Target != caller?
        |
        v
Reason non-empty?
        |
        v
Current == ACTIVE?
        |
        v
Requested == LOCKED?
        |
        v
Update user
        |
        +-- status = LOCKED
        +-- UpdatedAt
        +-- UpdatedBy
        |
        v
Save/Audit
        |
        v
Revoke all sessions
        |
        v
Return success
```

Phải đảm bảo ordering hợp lý với transaction/SaveChanges hiện có.

Không rewrite transaction architecture nếu handler hiện không cần.

---

# 38. BACKEND FLOW — SECURITY UNLOCK

```text
Caller == ADMIN
      |
Target != self
      |
Reason required
      |
Current == LOCKED
      |
Requested == ACTIVE
      |
status = ACTIVE
FailedLoginCount = 0
LockedUntil = null
      |
Audit SECURITY_UNLOCK_ACCOUNT
      |
Save
      |
Return success
```

Không recreate/revive old sessions.

---

# 39. HO / STAFF LEADER KHÔNG ĐƯỢC SECURITY UNLOCK

Điều này phải đúng cả API.

Ví dụ:

```text
HO -> LOCKED -> ACTIVE
```

phải bị từ chối nếu thao tác đó mang nghĩa mở security lock.

Staff Leader tương tự.

Account `LOCKED` phải được xử lý bởi ADMIN.

---

# 40. KHÔNG TẠO DATABASE MIGRATION

Task này **KHÔNG CẦN**:

```text
new table
new column
new enum
new FK
new trigger
migration
canonical SQL update
```

Dùng:

```text
users.status
failed_login_count
locked_until
sessions
audit_logs
```

theo schema/code hiện có.

Nếu agent cho rằng bắt buộc thay schema, phải dừng và báo lý do trước; không tự ý làm.

---

# 41. KHÔNG THAY ĐỔI EMAIL TEMPLATE CATALOG

Không thêm template email.

Không chỉnh canonical email seed.

Không chỉnh email defaults.

Task này không yêu cầu.

---

# 42. KHÔNG THAY ĐỔI BUSINESS FLOW HO / STAFF LEADER NGOÀI PHẦN CẦN THIẾT

Các flow hiện có:

```text
HO create
HO Staff Leader management
Staff Leader create account
Staff Leader role edit
pending account confirmation
department leader assignment
```

phải tiếp tục chạy.

Đặc biệt không delete API/service dùng chung chỉ vì ADMIN không còn dùng.

---

# 43. FILE-BY-FILE PLAN

## 43.1 `AccountManagement.tsx`

Thay đổi:

- phân biệt `ADMIN` display mode rõ ràng;
- bỏ `Tạo tài khoản mới` cho ADMIN;
- bỏ edit/status business/pending/reset actions cho ADMIN;
- render Eye + Lock/Unlock security theo capability;
- Admin detail readonly;
- giữ HO/SL UI hiện có;
- nếu campus dropdown còn hard-code, dùng active campus API hiện có;
- không dùng localStorage như security authority.

---

## 43.2 `accountManagement.types.ts`

Nếu dùng capability pattern:

Thêm:

```ts
canSecurityLock: boolean;
canSecurityUnlock: boolean;
securityActionDisabledReason?: string | null;
```

Tên field phải theo convention hiện tại.

---

## 43.3 `accountManagementApi.ts`

Ưu tiên không tạo API mới.

Reuse:

```ts
manageAccountStatus({
  userId,
  status: 'LOCKED' | 'ACTIVE',
  reason
})
```

Không xóa các API HO/SL đang dùng.

---

## 43.4 `AccountListQueryExecutor.cs`

Giữ global list của ADMIN.

Update action capability:

ADMIN:

```text
CanViewDetails = true
CanUpdateRole = false
CanManageStatus = false
CanEditBasicInfo = false
CanSecurityLock = target ACTIVE && !self
CanSecurityUnlock = target LOCKED && !self
```

HO/SL:

```text
CanSecurityLock = false
CanSecurityUnlock = false
```

Giữ scope hiện có.

---

## 43.5 `CreateAccountCommandHandler.cs`

- block ADMIN 403;
- fail closed role khác ngoài intended creator;
- giữ HO/SL flow;
- không thay đổi email/confirmation logic cho allowed actor.

---

## 43.6 `UpdateAccountRoleCommandHandler.cs`

- block ADMIN 403;
- giữ HO/SL behavior hiện có;
- remove dead Admin-only helper chỉ khi thật sự unused.

---

## 43.7 `ManageAccountStatusCommandHandler.cs`

Implement role-specific state matrix.

ADMIN:

```text
ACTIVE -> LOCKED
LOCKED -> ACTIVE
reason required
self forbidden
```

HO/SL:

```text
ACTIVE <-> INACTIVE
LOCKED forbidden
```

Other roles:

```text
403
```

Security lock:

```text
revoke sessions
audit security action
```

Security unlock:

```text
clear lock counters
do not restore sessions
audit security action
```

---

## 43.8 `ManageAccountStatusCommandValidator.cs`

Không làm validator phụ thuộc DB.

Validate common payload.

Reason role/state-specific có thể để handler xử lý vì validator không nhất thiết biết caller/current target state.

Nếu validator hiện cho status whitelist:

```text
ACTIVE
INACTIVE
LOCKED
```

giữ whitelist phù hợp.

Không cho arbitrary status string.

---

## 43.9 `ViewAccountDetailsQueryHandler.cs`

Verify:

- ADMIN được view global;
- safe projection;
- không trả secrets.

Chỉ bổ sung security-safe field nếu thật sự cần cho UI.

---

## 43.10 `AccountsController.cs`

Ưu tiên không đổi route.

Không áp role attribute làm hỏng endpoint shared.

Handler chịu trách nhiệm scope/authorization.

---

## 43.11 Route policy

`ACCOUNT_LIST` vẫn:

```text
ADMIN
HO
STAFF_LEADER
```

Không thay đổi.

---

# 44. UNIT TESTS — BẮT BUỘC

Bổ sung/điều chỉnh test theo pattern hiện tại.

## ADMIN read

1. ADMIN list được account mọi campus.
2. ADMIN statistics system-wide.
3. ADMIN view detail account campus khác.

## ADMIN create/edit denied

4. ADMIN create account -> 403.
5. ADMIN update account role -> 403.
6. ADMIN basic info edit -> 403 nếu endpoint trước đây lọt.
7. ADMIN pending email resend -> 403.
8. ADMIN pending email edit -> 403.
9. ADMIN pending account cancel -> 403.
10. ADMIN replace Staff Leader -> 403.

Chỉ test các endpoint mà code hiện tại tồn tại.

## ADMIN security lock

11. ACTIVE target -> LOCKED success.
12. Reason required.
13. Self-lock denied.
14. INACTIVE -> LOCKED denied.
15. PENDING_EMAIL_CONFIRMATION -> LOCKED denied.
16. Lock revokes active sessions.
17. Audit action == `SECURITY_LOCK_ACCOUNT`.

## ADMIN security unlock

18. LOCKED -> ACTIVE success.
19. Reason required.
20. Self-unlock denied.
21. INACTIVE -> ACTIVE denied cho ADMIN.
22. Unlock clears `FailedLoginCount`.
23. Unlock clears `LockedUntil`.
24. Old revoked sessions không active lại.
25. Audit action == `SECURITY_UNLOCK_ACCOUNT`.

## HO / Staff Leader regression

26. HO ACTIVE <-> INACTIVE vẫn chạy đúng scope.
27. Staff Leader ACTIVE <-> INACTIVE vẫn chạy đúng scope.
28. HO không được LOCK.
29. HO không được UNLOCK security.
30. Staff Leader không được LOCK.
31. Staff Leader không được UNLOCK.
32. Staff Leader create flow vẫn hoạt động.
33. HO create flow vẫn hoạt động.
34. Existing role-edit flow của allowed actor vẫn hoạt động.

---

# 45. INTEGRATION TESTS — BẮT BUỘC

Dùng database/test fixture pattern hiện tại.

Ít nhất verify:

```text
API direct call
DB status
session row/state
audit row
no unintended mutation
```

### Case: ADMIN lock

Before:

```text
user.status = ACTIVE
2 active sessions
```

Action:

```text
ADMIN -> status LOCKED
```

After:

```text
user.status = LOCKED
active sessions = revoked
audit exists
```

### Case: denied mutation

ADMIN calls create/update role.

After:

```text
403
DB unchanged
no audit claiming success
no email send
no notification side effect
```

---

# 46. FRONTEND TESTS — BẮT BUỘC

ADMIN:

1. Không thấy `Tạo tài khoản mới`.
2. Không thấy edit role.
3. Không thấy edit basic info.
4. Không thấy ACTIVE/INACTIVE toggle.
5. ACTIVE row có Lock.
6. LOCKED row có Unlock.
7. INACTIVE row không có security button.
8. PENDING row không có security button.
9. Self row không có security button.
10. Lock modal bắt reason.
11. Unlock modal bắt reason.
12. Submit gọi `manageAccountStatus` đúng payload.
13. Detail ADMIN read-only.

Regression:

14. HO vẫn thấy create/actions đúng behavior hiện có.
15. Staff Leader vẫn thấy create/actions đúng behavior hiện có.

---

# 47. SECURITY TEST — DIRECT API

Không chỉ test UI.

Phải test bằng API trực tiếp:

```text
POST /api/accounts/createaccount
POST /api/accounts/updateaccountrole
POST /api/accounts/manageaccountstatus
POST /api/accounts/resend-email-confirmation
POST /api/accounts/edit-pending-email
POST /api/accounts/cancel-pending-account
POST /api/accounts/replacestaffleader
```

với ADMIN JWT.

Kỳ vọng:

- read endpoints -> allowed;
- business mutate -> denied;
- security lock/unlock -> allowed đúng state matrix.

---

# 48. ACCEPTANCE CRITERIA

Task chỉ được coi là hoàn thành khi tất cả đúng:

## ADMIN UI

- [ ] ADMIN vẫn vào `/dashboard/accounts`.
- [ ] ADMIN thấy toàn hệ thống.
- [ ] ADMIN search/filter/statistics hoạt động.
- [ ] Không có nút `Tạo tài khoản mới`.
- [ ] Không có edit info.
- [ ] Không có role editor.
- [ ] Không có campus/department mutation.
- [ ] Không có ACTIVE/INACTIVE business toggle.
- [ ] Không có pending confirmation actions.
- [ ] Không có reset-password-for-user action.
- [ ] ACTIVE row có Security Lock.
- [ ] LOCKED row có Security Unlock.
- [ ] Detail read-only.

## Backend

- [ ] ADMIN create -> forbidden.
- [ ] ADMIN update role -> forbidden.
- [ ] ADMIN business status change -> forbidden.
- [ ] ADMIN ACTIVE -> LOCKED -> allowed.
- [ ] ADMIN LOCKED -> ACTIVE -> allowed.
- [ ] ADMIN self security action -> forbidden.
- [ ] Reason required.
- [ ] Lock revokes sessions.
- [ ] Unlock không restore sessions.
- [ ] Security audit đúng action.
- [ ] HO/SL behavior không regress.

## Data

- [ ] Không schema migration.
- [ ] Không email-template changes.
- [ ] Không destructive DB change.
- [ ] Không sửa unrelated module.

---

# 49. NON-GOALS

Không làm trong task này:

```text
Thiết kế lại toàn bộ account module
Thêm account approval workflow
Thêm database table cho security case
Thêm ticket/incident system
Thêm email template mới
Thêm MFA
Thêm password reset workflow mới
Thêm admin impersonation
Thêm user deletion
Thêm soft-delete
Thay đổi authentication architecture
Thay đổi session architecture
Thay đổi role schema
Thay đổi permission matrix của module khác
```

---

# 50. CÁC BẪY CẦN TRÁNH

## Sai 1: Chỉ ẩn button frontend

Không đủ.

Backend phải 403 direct API.

---

## Sai 2: Bỏ ADMIN khỏi `IsPrivileged`

Sai vì ADMIN vẫn cần global read và có thể ảnh hưởng module khác.

---

## Sai 3: Xóa create/update API

Sai vì HO/Staff Leader vẫn dùng.

---

## Sai 4: Cho ADMIN dùng `INACTIVE`

Sai responsibility.

`INACTIVE` = business management.

`LOCKED` = security.

---

## Sai 5: Thêm `previous_status` column chỉ để unlock

Không cần.

Rule đơn giản:

```text
only ACTIVE can be security-locked
LOCKED unlocks to ACTIVE
```

---

## Sai 6: Cho HO/SL unlock LOCKED

Không đúng ownership.

LOCKED thuộc ADMIN/security.

---

## Sai 7: Gửi email/template mới

Ngoài scope.

---

## Sai 8: Dùng localStorage để bảo vệ API

Không có giá trị security.

Backend authorization mới là final gate.

---

# 51. TRÌNH TỰ IMPLEMENT KHUYẾN NGHỊ

## Phase 1 — Audit

1. Record Dev HEAD.
2. Search toàn bộ account mutations.
3. Xác nhận handler authorization hiện tại.
4. Xác nhận frontend action conditions.
5. Xác nhận tests hiện có.
6. Xác nhận session revoke behavior.
7. Xác nhận LOCKED/FailedLoginCount/LockedUntil behavior.

Không code trước khi có map.

---

## Phase 2 — Backend authorization

1. Block ADMIN create.
2. Block ADMIN update role/basic info/pending business flows.
3. Fail closed unsupported callers.
4. Không thay đổi allowed HO/SL path.

---

## Phase 3 — Security status semantics

1. Update `ManageAccountStatusCommandHandler`.
2. ADMIN ACTIVE->LOCKED.
3. ADMIN LOCKED->ACTIVE.
4. reason required.
5. self denied.
6. revoke sessions.
7. distinct audit actions.

---

## Phase 4 — Read model capabilities

1. Add security capability flags.
2. ADMIN business capabilities false.
3. HO/SL security capabilities false.
4. Update DTO/type.

---

## Phase 5 — Frontend

1. Remove Admin create.
2. Remove Admin business mutations.
3. Add Lock/Unlock security action.
4. Read-only detail.
5. Reuse modal/API.
6. Preserve HO/SL behavior.

---

## Phase 6 — Tests

1. Unit.
2. Integration.
3. Frontend.
4. Existing regression suite.
5. Build/typecheck/lint where project currently uses them.

---

# 52. OUTPUT AI AGENT PHẢI BÁO CÁO SAU KHI CODE

Không chỉ nói “done”.

Báo cáo theo format:

```text
1. Baseline
- Branch
- HEAD trước sửa
- Working tree

2. Files changed
- file
- reason
- exact behavior changed

3. ADMIN permissions after change
- allowed
- denied

4. Backend guards
- endpoint/handler
- direct API behavior

5. Security lock flow
- state transition
- session revoke
- audit

6. Frontend
- removed controls
- added controls
- detail readonly

7. Regression
- HO
- Staff Leader

8. Tests
- command
- passed/failed
- exact counts if available

9. DB/schema
- confirm NO schema change

10. Remaining issue
- only real unresolved issues
```

---

# 53. FINAL PRODUCT RULE

Sau implementation, người dùng phải có thể giải thích hệ thống bằng một câu:

> **HO và Staff Leader quản lý “ai là ai và họ làm việc ở đâu”; ADMIN chỉ quan sát toàn hệ thống và bảo vệ quyền truy cập của tài khoản.**

Hoặc theo technical boundary:

```text
ADMIN
= READ GLOBAL
+ SECURITY LOCK/UNLOCK
+ SESSION/SECURITY/AUDIT VISIBILITY

HO / STAFF_LEADER
= CREATE
+ PERSONNEL UPDATE
+ ORGANIZATIONAL ROLE/SCOPE
+ ACTIVE/INACTIVE
```

Không được tồn tại đường API nào cho phép ADMIN bypass boundary này.
