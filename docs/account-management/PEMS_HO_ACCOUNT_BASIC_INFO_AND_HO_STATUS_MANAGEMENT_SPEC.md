# PEMS — Đặc tả triển khai HO chỉnh sửa thông tin tài khoản và quản lý trạng thái HO khác

## 0. Mục đích tài liệu

Tài liệu này là đặc tả triển khai end-to-end dành cho AI Agent. Phạm vi gồm hai chức năng trong màn hình Quản lý tài khoản của HO:

1. HO chỉnh sửa họ tên và email của tài khoản HO khác và STAFF LEADER.
2. HO chuyển trạng thái tài khoản HO khác giữa `ACTIVE` và `INACTIVE`.

Agent phải giữ nguyên tất cả luồng đã hoàn thiện trước đó. Không được mở rộng ngầm quyền của HO sang đổi vai trò, đổi campus, đổi phòng ban hoặc xử lý trạng thái bảo mật `LOCKED`.

## 1. Bối cảnh và điều kiện tiên quyết

- Dự án: `quangthoai04/PEMS`.
- Backend: ASP.NET Core, MediatR, EF Core, MySQL.
- Frontend: React, TypeScript, Vite.
- Màn hình: Quản lý tài khoản của HO.
- Database nguồn: `pems_full_v10_TTS_Gallery_FULL_UPDATED_NOTIFICATIONS_FIXED.sql`.
- Phiên bản triển khai phải được đặt trên working branch đã có các flow account-management trước đó.

Các flow được xem là đã hoàn thiện và không thuộc phạm vi viết lại:

- HO xem danh sách/detail của HO và STAFF LEADER.
- HO tạo HO hoặc STAFF LEADER.
- Kiểm tra một tài khoản HO trên mỗi campus.
- Thay thế STAFF LEADER.
- Enable/disable STAFF LEADER.
- ADMIN lock/unlock tài khoản.
- STAFF LEADER quản lý STAFF/STAFF, DEPARTMENT/LEADER và STUDENT.
- Chỉnh sửa role/department/MSSV của account do STAFF LEADER quản lý.
- Tạo STUDENT có MSSV.
- Các flow SSO/FEID/local password, session, audit và notification hiện có.

## 2. Mục tiêu nghiệp vụ

### 2.1. Chỉnh sửa thông tin cơ bản

Khi HO mở modal detail của một target hợp lệ, HO có thể bấm `Chỉnh sửa thông tin` và sửa:

- Họ và tên.
- Email đăng nhập.

Tất cả thông tin còn lại phải giữ nguyên và bị disable.

### 2.2. Quản lý trạng thái HO khác

HO được phép chuyển trạng thái một tài khoản HO khác:

```text
ACTIVE ↔ INACTIVE
```

HO không được:

- Thay đổi trạng thái chính mình.
- Gửi trạng thái `LOCKED`.
- Enable/disable target đang `LOCKED`.
- Lock hoặc unlock bảo mật một HO khác.

## 3. Ma trận quyền cuối cùng

| Target account | Sửa họ tên/email | ACTIVE ↔ INACTIVE | Đổi role | Đổi campus/phòng ban | LOCKED ↔ ACTIVE |
| --- | ---: | ---: | ---: | ---: | ---: |
| HO khác | Có | Có | Không | Không | Không |
| Chính HO đang đăng nhập | Không | Không | Không | Không | Không |
| `STAFF/LEADER` | Có | Giữ quyền hiện tại | Không | Không | Không |
| Target `LOCKED` | Không | Không | Không | Không | Không |
| Role khác | Không | Không | Không | Không | Không |

`HO khác` nghĩa là target có `user_id` khác caller HO.

## 4. Scope của HO

HO chỉ thao tác trên các target thuộc scope màn hình HO:

- `role = HO`; hoặc
- `role = STAFF` và `sub_role = LEADER`.

Quy ước đã xác nhận:

- Quyền áp dụng cho mọi target hợp lệ xuất hiện trong danh sách quản lý của HO, kể cả khác campus.
- HO không được thao tác lên chính mình.
- Target `LOCKED` không được chỉnh sửa thông tin hoặc enable/disable.
- Backend phải tính quyền từ current user và dữ liệu database, không tin role, sub-role hoặc campus do client gửi.

## 5. Phần A — Chỉnh sửa thông tin HO và STAFF LEADER

### 5.1. Nút trong modal detail

Khi HO mở detail của target hợp lệ, header modal hiển thị:

```text
Chỉnh sửa thông tin
```

Nút chỉ hiển thị khi backend trả:

```ts
canEditBasicInfo: true
```

Không dùng `canUpdateRole` để quyết định nút này vì HO không có quyền đổi role.

Không hiển thị nút nếu:

- Target là caller.
- Target `LOCKED`.
- Target ngoài HO hoặc STAFF/LEADER.
- Caller không phải HO hoặc không có permission account-management.

### 5.2. Target là HO

| Trường | Trạng thái trong edit mode |
| --- | --- |
| Họ và tên | Sửa được |
| Email | Sửa được |
| Giới tính | Disable |
| Số điện thoại | Disable |
| Vai trò | Disable, giữ `HO` |
| Cơ sở trực thuộc | Disable |
| Trạng thái | Không sửa trong form này |

### 5.3. Target là STAFF LEADER

| Trường | Trạng thái trong edit mode |
| --- | --- |
| Họ và tên | Sửa được |
| Email | Sửa được |
| Giới tính | Disable |
| Số điện thoại | Disable |
| Vai trò | Disable, giữ `STAFF` |
| Cơ sở trực thuộc | Disable |
| Chức vụ | Disable, giữ `Trưởng phòng` |
| Phòng ban | Disable, giữ phòng IC hiện tại |
| Trạng thái | Không sửa trong form này |

HO không được thấy control có thể thay đổi:

- Role.
- Sub-role.
- Campus.
- Department.
- MSSV.
- `ic_head_user_id` hoặc các liên kết tổ chức khác.

### 5.4. Giữ nút Thay thế STAFF LEADER

Khi target là STAFF LEADER:

- Nút `Chỉnh sửa thông tin` xuất hiện ở header modal.
- Nút `Thay thế Staff Leader` hiện có tiếp tục giữ ở sidebar.
- Hai chức năng độc lập.
- Chỉnh sửa thông tin không thay đổi role, sub-role, campus, IC department hoặc `ic_head_user_id`.

## 6. Style field enabled và disabled

Field sửa được phải có nền trắng. Field disable phải có nền xám rõ ràng nhưng vẫn dễ đọc.

Style đề xuất:

```tsx
const disabledFieldClass =
  'bg-slate-100 text-slate-500 border-slate-200 cursor-not-allowed opacity-100';

const enabledFieldClass =
  'bg-white text-gray-900 border-gray-300 focus:border-[#004c91] focus:ring-2 focus:ring-[#004c91]/20';
```

Yêu cầu:

- Không chỉ dùng `opacity-70` vì làm nội dung khó đọc.
- Icon dropdown của select disable cũng có màu xám.
- Field disable dùng thuộc tính `disabled` thật, không chỉ style giả.
- Giá trị `null`, `undefined` hoặc rỗng hiển thị `-`.
- Dùng chung component/style với các modal account-management khác để tránh lệch giao diện.

## 7. State frontend cho edit mode HO

Tạo state riêng:

```ts
interface HoBasicInfoEditForm {
  fullName: string;
  email: string;
}
```

Phân biệt rõ edit context:

```ts
type AccountEditMode =
  | 'NONE'
  | 'STAFF_LEADER_MANAGED_ACCOUNT'
  | 'HO_BASIC_INFO';
```

Khởi tạo từ response detail:

```ts
setHoEditForm({
  fullName: selectedAccount.fullName,
  email: selectedAccount.email,
});
```

Quy tắc state:

- Không mutate `selectedAccount` trong lúc nhập.
- Dùng response detail làm nguồn dữ liệu chính, không dùng item list nếu detail đã tải xong.
- Sidebar giữ snapshot cũ cho tới khi save thành công.
- Bấm Hủy reset form, error, loading và edit mode.
- Đóng modal/click overlay thực hiện cùng reset.
- API lỗi giữ nội dung đang nhập.
- Thành công đóng edit mode và refetch detail/list.

### 7.1. Dirty state

```ts
const isDirty =
  editForm.fullName.trim() !== selectedAccount.fullName.trim() ||
  normalizeEmail(editForm.email) !== normalizeEmail(selectedAccount.email);
```

Nút `Cập nhật` bị disable khi:

- `isDirty = false`.
- Form không hợp lệ.
- Request đang chạy.
- Permission không còn hợp lệ.

## 8. Validation thông tin cơ bản

### 8.1. Họ và tên

Frontend và backend cùng kiểm tra:

- Bắt buộc.
- Trim khoảng trắng đầu/cuối.
- Không chấp nhận chuỗi chỉ gồm khoảng trắng.
- Tối đa 150 ký tự.
- Cho phép Unicode và tiếng Việt.
- Không tự động thay đổi cách viết hoa/thường.

Thông báo:

```text
Vui lòng nhập họ và tên.
```

```text
Họ và tên không được vượt quá 150 ký tự.
```

### 8.2. Email

Frontend và backend cùng kiểm tra:

- Bắt buộc.
- Trim khoảng trắng.
- Chuẩn hóa lowercase trước khi so sánh/lưu.
- Đúng định dạng email.
- Tối đa 150 ký tự.
- Không trùng user khác.
- Duplicate check phải loại trừ chính target.
- Không thêm domain restriction mới.

Thông báo:

```text
Vui lòng nhập email.
```

```text
Email không đúng định dạng.
```

```text
Email này đã được sử dụng bởi một tài khoản khác.
```

Backend phải xử lý cả pre-check và duplicate-key race từ unique index, trả `409 Conflict`, không để lộ raw MySQL exception.

## 9. API cập nhật thông tin cơ bản

### 9.1. Endpoint

Để đảm bảo HO không thể thay đổi role/campus/department, dùng endpoint riêng:

```http
POST /api/accounts/updatebasicaccountinfo
```

Request:

```ts
interface UpdateBasicAccountInfoRequest {
  userId: string;
  fullName: string;
  email: string;
}
```

Endpoint không nhận:

- `newRoleCode`.
- `subRole`.
- `primaryCampusId`.
- `departmentId`.
- `studentCode`.
- `status`.

Response đề xuất:

```ts
interface UpdateBasicAccountInfoResponse {
  userId: string;
  fullName: string;
  email: string;
  emailChanged: boolean;
  revokedSessions: number;
  emailNotificationStatus:
    | 'NOT_REQUIRED'
    | 'SENT'
    | 'FAILED'
    | 'PARTIAL';
  message: string;
}
```

Nếu đã có service cập nhật fullName/email từ flow STAFF LEADER, endpoint mới phải tái sử dụng service đó, không sao chép validation và provider logic.

### 9.2. Authorization handler

Handler kiểm tra theo thứ tự:

1. Caller đã đăng nhập.
2. Caller có `role = HO`.
3. Caller có permission account-management.
4. Target tồn tại.
5. Target không phải caller.
6. Target không `LOCKED`.
7. Target là `HO` hoặc `STAFF/LEADER`.
8. Target nằm trong scope quản lý của HO.
9. Request chỉ chứa fullName/email hợp lệ.

Role, sub-role, campus và department phải được load từ DB và không được thay đổi.

## 10. Xử lý khi email thay đổi

Phải tái sử dụng flow đổi email đã hoàn thiện trước đó.

Khi email thay đổi:

1. Chuẩn hóa email mới.
2. Kiểm tra unique.
3. Cập nhật `users.email`.
4. Cập nhật `user_auth_providers.provider_email`.
5. Reset liên kết GOOGLE_SSO/FEID theo logic đã thống nhất.
6. Revoke toàn bộ active sessions.
7. Email cũ không còn đăng nhập được.
8. Email mới liên kết lại SSO/FEID ở lần đăng nhập tiếp theo.
9. Cập nhật `email_verified_at` theo flow xác minh hiện có.
10. Ghi audit log.

Frontend hiển thị confirmation:

```text
Bạn đang thay đổi email đăng nhập từ {oldEmail} sang {newEmail}.

Tài khoản sẽ bị đăng xuất khỏi các phiên hiện tại và phải liên kết lại SSO/FEID khi đăng nhập lần tiếp theo.
```

Nếu email không thay đổi:

- Không reset provider.
- Không revoke session vì email.
- Không hiển thị confirmation đổi email.

Nếu chỉ đổi họ tên, không bắt buộc revoke session.

## 11. Permission flag chỉnh sửa

Bổ sung vào list/detail DTO:

```ts
canEditBasicInfo: boolean;
```

Backend tính:

```text
caller = HO
AND target != caller
AND target.status != LOCKED
AND (
  target.role = HO
  OR (
    target.role = STAFF
    AND target.sub_role = LEADER
  )
)
```

Reason tùy chọn:

```ts
editBasicInfoDisabledReason?:
  | 'SELF_ACCOUNT'
  | 'ACCOUNT_LOCKED'
  | 'TARGET_ROLE_NOT_MANAGEABLE'
  | 'NO_PERMISSION'
  | null;
```

Frontend dùng flag để render nút; backend vẫn kiểm tra lại lúc submit.

## 12. Phần B — HO enable/disable HO khác

### 12.1. Chuyển đổi hợp lệ

HO chỉ được:

```text
ACTIVE ↔ INACTIVE
```

Không được:

```text
ACTIVE → LOCKED
INACTIVE → LOCKED
LOCKED → ACTIVE
LOCKED → INACTIVE
```

`LOCKED` tiếp tục thuộc flow bảo mật của ADMIN.

### 12.2. Giao diện status

Tái sử dụng toggle hiện có trong danh sách:

- HO khác `ACTIVE`: toggle bật.
- HO khác `INACTIVE`: toggle tắt.
- Chính HO: không hiển thị toggle.
- HO `LOCKED`: không hiển thị toggle.
- STAFF LEADER: giữ toggle hiện tại.

Không cần thêm một status control khác trong modal detail nếu table đã có toggle.

### 12.3. Confirmation modal

Disable:

```text
Bạn có chắc muốn vô hiệu hóa tài khoản HO {email}?

Tất cả phiên đăng nhập hiện tại của tài khoản này sẽ bị thu hồi ngay lập tức.
```

Enable:

```text
Bạn có chắc muốn kích hoạt lại tài khoản HO {email}?

Tài khoản sẽ có thể đăng nhập và sử dụng hệ thống trở lại.
```

Yêu cầu:

- Disable dùng nút đỏ.
- Enable dùng nút xanh.
- Khi request chạy, disable các nút submit/cancel phù hợp.
- API lỗi giữ modal và hiển thị message backend.
- Không đổi toggle lạc quan trước khi server xác nhận.

## 13. Cập nhật `CanManageStatus`

Sửa read model trong `AccountListQueryExecutor`.

Logic mới cho caller HO:

```text
canManageStatus = true khi:

caller = HO
AND target != caller
AND target.status != LOCKED
AND (
  target.role = HO
  OR (
    target.role = STAFF
    AND target.sub_role = LEADER
  )
)
```

Reason:

| Trường hợp | Reason |
| --- | --- |
| Target là caller | `SELF_ACCOUNT` |
| Target `LOCKED` | `ACCOUNT_LOCKED` |
| Target ngoài scope | `TARGET_ROLE_NOT_MANAGEABLE` |
| Không có permission | `NO_PERMISSION` |

Bỏ chặn `HO_STATUS_CHANGE_REQUIRES_SPECIAL_FLOW` đối với HO khác ở trạng thái ACTIVE/INACTIVE.

Nếu biến/comment như `hoCrossCampus` không còn được sử dụng thì xóa để tránh dead code và tài liệu sai.

## 14. Cập nhật `ManageAccountStatusCommandHandler`

Thay logic chặn mọi target HO bằng logic scope mới:

```csharp
if (callerIsHo)
{
    var targetInScope =
        user.Role.RoleCode == RoleCodes.Ho
        || (
            user.Role.RoleCode == RoleCodes.Staff
            && user.SubRole == UserSubRoles.Leader
        );

    if (!targetInScope)
        throw new ForbiddenException(
            "Tài khoản này nằm ngoài phạm vi quản lý của HO.");

    if (newStatus == UserStatuses.Locked)
        throw new BusinessRuleException(
            "HO chỉ được phép kích hoạt hoặc vô hiệu hóa tài khoản.");

    if (user.Status == UserStatuses.Locked)
        throw new BusinessRuleException(
            "Tài khoản đang bị khóa vì lý do bảo mật và không thể thay đổi tại đây.");
}
```

Giữ self-account guard:

```csharp
if (actorId == user.UserId)
    throw new ForbiddenException(
        "Bạn không thể thay đổi trạng thái tài khoản của chính mình.");
```

Handler là final authorization gate; direct API call vẫn phải bị từ chối nếu không hợp lệ.

## 15. Khi disable HO

Luồng:

```text
ACTIVE → INACTIVE
```

Backend phải:

1. Set `users.status = INACTIVE`.
2. Cập nhật `updated_at`, `updated_by`.
3. Ghi audit.
4. Lưu DB.
5. Revoke toàn bộ active sessions của target.
6. Tạo notification theo flow hiện có.
7. Trả số session đã revoke.

Frontend phải:

- Đóng confirmation khi thành công.
- Hiển thị success toast.
- Refetch list và statistics.
- Nếu detail cùng target đang mở, đóng hoặc refetch detail.

## 16. Khi enable HO

Luồng:

```text
INACTIVE → ACTIVE
```

Backend phải:

1. Set `users.status = ACTIVE`.
2. Reset `failed_login_count = 0`.
3. Reset `locked_until = NULL`.
4. Cập nhật `updated_at`, `updated_by`.
5. Ghi audit.
6. Tạo notification.
7. Không cần revoke session.

Frontend refetch list/statistics sau thành công.

## 17. Quan hệ giữa hai chức năng

| Chức năng | Dữ liệu được thay đổi |
| --- | --- |
| Chỉnh sửa thông tin | `full_name`, `email`, auth provider liên quan |
| Enable/disable | `status`, lockout metadata, session khi disable |

Quy tắc:

- Edit thông tin không thay đổi status.
- Toggle status không thay đổi fullName, email, role, campus hoặc department.
- Target `INACTIVE` vẫn được phép sửa fullName/email.
- Target `LOCKED` không được edit hoặc toggle.
- Đổi email của target INACTIVE vẫn đồng bộ provider; target chỉ đăng nhập được sau khi được enable.

## 18. Session

### 18.1. Chỉnh sửa thông tin

- Chỉ đổi fullName: không bắt buộc revoke session.
- Đổi email: bắt buộc revoke toàn bộ active sessions.
- Dùng revoke reason phù hợp như `ACCOUNT_EMAIL_CHANGED` hoặc `ACCOUNT_BASIC_INFO_UPDATED`.
- Không dùng `ROLE_CHANGED` cho flow HO basic-info.

### 18.2. Status

- ACTIVE → INACTIVE: revoke toàn bộ active sessions với reason `ACCOUNT_DEACTIVATED` hiện có.
- INACTIVE → ACTIVE: không revoke session.

## 19. Audit log

### 19.1. Chỉnh sửa thông tin

Action:

```text
UPDATE_ACCOUNT_BASIC_INFO
```

Audit gồm:

- FullName cũ/mới.
- Email cũ/mới.
- `authenticationRelinkRequired`.
- Actor HO.
- Target user.
- Campus target.

Không ghi:

- Password hash.
- Provider subject.
- Token/session secret.

### 19.2. Status

Giữ action:

```text
MANAGE_ACCOUNT_STATUS
```

Audit gồm:

- Status cũ.
- Status mới.
- Reason nếu có.
- Actor HO.
- Target HO.
- Campus target.

## 20. Notification và email

### 20.1. Đổi email

- Gửi thông báo tới email cũ.
- Gửi thông tin tài khoản tới email mới.
- Gửi sau khi transaction commit.
- Gửi thất bại không rollback dữ liệu.
- Trả `PARTIAL` nếu chỉ một email gửi thành công.

### 20.2. Enable/disable

- Giữ notification flow hiện tại.
- Disable phải revoke session trước khi trả kết quả.
- Notification lỗi không được tạo trạng thái dữ liệu không nhất quán.

## 21. Logic phải giữ nguyên

Không được ảnh hưởng:

- List/search/filter/pagination của HO.
- Scope chỉ hiển thị HO và STAFF LEADER.
- Tạo HO.
- Quy tắc một HO trên mỗi campus.
- HO INACTIVE vẫn được tính là HO đã tồn tại; không được tạo HO thứ hai cùng campus.
- Tạo và thay thế STAFF LEADER.
- Nút `Thay thế Staff Leader`.
- Enable/disable STAFF LEADER hiện tại.
- Flow chỉnh sửa account của STAFF LEADER.
- Role/department/MSSV của account do STAFF LEADER quản lý.
- MSSV khi tạo STUDENT.
- ADMIN lock/unlock.
- Quy tắc LOCKED.
- Session, notification và audit của các flow khác.

## 22. Các file dự kiến thay đổi

### Frontend

- `frontend/pems-react/src/pages/dashboard/accounts/AccountManagement.tsx`
- `frontend/pems-react/src/features/account-management/types/accountManagement.types.ts`
- `frontend/pems-react/src/features/account-management/api/accountManagementApi.ts`
- `frontend/pems-react/src/shared/api/endpoints.ts`
- Test component/API theo cấu trúc hiện có.

### Backend

- `backend/PEMS.Api/Controllers/AccountsController.cs`
- Thêm command/handler/validator/response cho `UpdateBasicAccountInfo`.
- `backend/PEMS.Application/Accounts/Common/AccountListItemDto.cs`
- `backend/PEMS.Application/Accounts/Common/AccountListQueryExecutor.cs`
- Detail DTO/handler nếu permission flag nằm trong detail response.
- `backend/PEMS.Application/Accounts/Commands/ManageAccountStatus/ManageAccountStatusCommandHandler.cs`
- Error code/status reason nếu cần.
- Session revoke reason nếu cần.
- Test application/API theo cấu trúc hiện có.

## 23. Database

Không cần migration vì database đã có:

- `users.full_name VARCHAR(150)`.
- `users.email VARCHAR(150)`.
- Unique index trên `users.email`.
- `users.status` với `ACTIVE`, `INACTIVE`, `LOCKED`.
- `user_auth_providers.provider_email`.
- `provider_subject` nullable.
- Session revoke reason dạng text.
- Audit log.

Không thêm hoặc thay đổi column.

## 24. Test cases frontend

### 24.1. Chỉnh sửa thông tin

1. HO thấy nút edit với HO khác.
2. HO thấy nút edit với STAFF/LEADER.
3. HO không thấy nút edit với chính mình.
4. Target LOCKED không có nút edit.
5. Chỉ fullName/email enable.
6. Field còn lại disable và có nền xám.
7. Role/campus/department không thể thay đổi.
8. FullName rỗng/quá 150 ký tự không submit.
9. Email sai format không submit.
10. Đổi email hiển thị confirmation.
11. Hủy confirmation không gọi API.
12. Không có thay đổi thì nút Cập nhật disable.
13. API lỗi giữ nội dung đang nhập.
14. Thành công refetch dữ liệu.
15. Nút Replace Staff Leader vẫn hoạt động.

### 24.2. Status

1. HO thấy toggle của HO khác ACTIVE.
2. HO thấy toggle của HO khác INACTIVE.
3. HO không thấy toggle của chính mình.
4. HO LOCKED không có toggle.
5. Disable hiển thị confirmation đúng.
6. Enable hiển thị confirmation đúng.
7. API lỗi không đổi toggle sai trạng thái.
8. Thành công refetch list/statistics.
9. Toggle STAFF LEADER vẫn hoạt động.
10. Nút chỉnh sửa thông tin không bị ảnh hưởng bởi toggle.

## 25. Test cases backend

### 25.1. Chỉnh sửa thông tin

1. HO sửa fullName/email của HO khác thành công.
2. HO sửa fullName/email STAFF/LEADER thành công.
3. HO không sửa chính mình.
4. HO không sửa target LOCKED.
5. HO không sửa role ngoài scope.
6. Request basic-info không thay đổi role/sub-role/campus/department/status.
7. Duplicate email trả 409.
8. Email giữ nguyên không reset provider.
9. Email đổi đồng bộ provider và revoke session.
10. Chỉ đổi fullName không bắt buộc revoke session.
11. Audit đúng actor/target/old/new.
12. Request lỗi không cập nhật một phần.
13. Notification/email lỗi không rollback DB.

### 25.2. Status

1. HO disable HO khác thành công.
2. Disable HO revoke toàn bộ active sessions.
3. HO enable HO khác thành công.
4. Enable reset failed-login count và temporary lockout.
5. HO không tự disable/enable.
6. HO không thể gửi LOCKED.
7. HO không thể enable/disable target LOCKED.
8. HO không quản lý status role ngoài HO và STAFF/LEADER.
9. Request cùng status hiện tại là idempotent no-op.
10. Audit đúng actor/target/status.
11. Disable HO không cho phép tạo HO thứ hai cùng campus.
12. STAFF LEADER không thể gọi API đổi trạng thái HO.

## 26. Definition of Done

Chỉ coi là hoàn thành khi:

- HO sửa được fullName/email của HO khác và STAFF LEADER.
- HO không thể thay đổi role/campus/department qua UI hoặc API.
- Field disable có nền xám rõ ràng.
- HO chuyển được HO khác giữa ACTIVE và INACTIVE.
- HO không tự chỉnh sửa hoặc đổi trạng thái chính mình.
- Target LOCKED không được chỉnh sửa hoặc toggle.
- Disable revoke toàn bộ session.
- Email change thực hiện đúng provider/session flow.
- HO INACTIVE vẫn chặn tạo HO thứ hai cùng campus.
- Replace Staff Leader và các flow cũ vẫn hoạt động.
- Frontend build/typecheck thành công.
- Backend build thành công.
- Test liên quan pass.
- Không cần thay đổi database schema.
- Diff không chứa thay đổi ngoài phạm vi.

## 27. Thứ tự triển khai khuyến nghị

1. Bổ sung permission flag `canEditBasicInfo` ở backend DTO/query.
2. Tạo command/endpoint `UpdateBasicAccountInfo` và backend tests.
3. Tích hợp email/provider/session/audit dùng service chung.
4. Thêm edit mode HO trên frontend và style disabled.
5. Sửa `CanManageStatus` để mở toggle cho HO khác.
6. Sửa `ManageAccountStatusCommandHandler` và tests.
7. Chạy frontend build/typecheck/test.
8. Chạy backend build/test.
9. Regression test Create HO, Replace Staff Leader, STAFF LEADER management và ADMIN lock/unlock.
10. Kiểm tra diff cuối cùng trước khi commit.
