# PROMPT SỬA FULL LOGIN PEMS THEO LOGIC SSO-FIRST + DUAL PORTAL

> Dùng file này để đưa cho AI/code agent thực hiện sửa code trực tiếp trong project PEMS hiện tại.  
> Mục tiêu: sửa **đúng luồng Authentication/Login**, không làm lại project từ đầu, không phá frontend hiện có, đảm bảo đăng nhập được bằng **SSO/FEID** và **email + password trong giai đoạn dev/test**.

---

## 0. Vai trò của AI/code agent

Bạn là **Senior Full-stack Developer + .NET Clean Architecture Architect + React TypeScript Engineer + Security Reviewer**.

Bạn đang làm trên project **PEMS — Partnership Engagement Management System** hiện tại.  
Nhiệm vụ của bạn là **sửa lại toàn bộ phần Login/Authentication/Account provisioning** để khớp với logic nghiệp vụ mới bên dưới.

Không được làm lại từ đầu. Không được phá route/UI frontend hiện có. Không được xóa code cũ nếu chưa kiểm tra dependency.

---

## 1. Tài liệu bắt buộc phải đọc trước khi sửa code

Trước khi code, hãy đọc và đối chiếu các file/tài liệu hiện có trong project:

```text
PROJECT_STRUCTURE_FULL.md
PROJECT_OVERVIEW.md
USE_CASE_LIST.md
USE_CASE_NOTES_UPDATED_SSO_FIRST.md
USE_CASE_NOTES.md
CLEAN_ARCHITECTURE.md
Technology.md
database/scripts/*.sql
database/seed/*.sql
```

Sau đó quét code thật:

```text
backend/PEMS.Api/Controllers/AuthenticationController.cs
backend/PEMS.Api/Controllers/AccountsController.cs
backend/PEMS.Api/Extensions/AuthenticationExtensions.cs
backend/PEMS.Api/Middleware/SessionValidationMiddleware.cs

backend/PEMS.Application/Authentication/**
backend/PEMS.Application/Accounts/**
backend/PEMS.Application/Profiles/**
backend/PEMS.Application/Common/Interfaces/**
backend/PEMS.Application/Common/Exceptions/**

backend/PEMS.Domain/Entities/User.cs
backend/PEMS.Domain/Entities/Role.cs
backend/PEMS.Domain/Entities/UserAuthProvider.cs
backend/PEMS.Domain/Constants/**
backend/PEMS.Domain/Enums/**

backend/PEMS.Infrastructure/Persistence/**
backend/PEMS.Infrastructure/Repositories/**
backend/PEMS.Infrastructure/Identity/**
backend/PEMS.Infrastructure/ExternalServices/**

frontend/pems-react/src/**
```

Nếu tên role/sub_role hiện tại khác tài liệu, **không tự ý đổi hàng loạt**. Hãy map theo code thật rồi cập nhật constant/adapter cho thống nhất.

---

## 2. Logic login mong muốn — bản chốt nghiệp vụ

### 2.1 Có 2 cổng đăng nhập

Hệ thống phải có 2 loại cổng đăng nhập rõ ràng:

```text
1. VISITOR_PORTAL
   - Dành cho Visitor/khách.
   - Không chọn campus khi login.
   - Cho phép auto-create tài khoản VISITOR nếu đăng nhập SSO/FEID lần đầu.

2. INTERNAL_PORTAL
   - Dành cho các role nội bộ: HO, ADMIN, STAFF/STAFF_L/STAFF_P, DEPT/DEPT_L/DEPT_P, STUDENT nếu hệ thống đang dùng role Student.
   - Bắt buộc chọn campus khi login.
   - Không auto-create account khi SSO/FEID chưa tồn tại trong DB.
```

> Lưu ý: Nếu project hiện tại đang gộp role thành `STAFF`, `DEPT` thay vì tách `STAFF_L`, `STAFF_P`, `DEPT_L`, `DEPT_P`, hãy giữ cấu trúc hiện tại và dùng `sub_role` nếu đã có. Không đổi mô hình role nếu không bắt buộc.

---

## 3. Chế độ đăng nhập theo môi trường

### 3.1 Giai đoạn dev/test đang triển khai code

Trong giai đoạn triển khai hiện tại, hệ thống phải cho phép:

```text
- Login bằng email + password.
- Login bằng Google SSO.
- Login bằng FEID nếu user là sinh viên/nhóm được cấu hình cho FEID.
```

Credential login chỉ phục vụ dev/test hoặc tài khoản có cấu hình `LOCAL_PASSWORD`.

### 3.2 Khi build/chạy production thật

Production phải là SSO-first:

```text
- Tắt login email + password trên UI.
- API login credentials phải bị chặn bằng config nếu environment là production.
- Người dùng chỉ đăng nhập qua Google SSO hoặc FEID theo chính sách.
- Forgot password chỉ áp dụng cho tài khoản LOCAL_PASSWORD trong dev/test, không áp dụng cho tài khoản SSO/FEID production.
```

### 3.3 Config bắt buộc

Thêm/cập nhật config backend:

```json
{
  "AuthOptions": {
    "LoginMode": "DevMixed",
    "AllowPasswordLogin": true,
    "AllowGoogleSso": true,
    "AllowFeid": true,
    "AutoCreateVisitorOnExternalLogin": true,
    "AutoCreateInternalOnExternalLogin": false,
    "StudentFeidMinCohort": "K19"
  }
}
```

Các giá trị gợi ý:

```text
LoginMode:
- DevMixed
- ProductionSsoOnly
```

Ở production:

```json
{
  "AuthOptions": {
    "LoginMode": "ProductionSsoOnly",
    "AllowPasswordLogin": false,
    "AllowGoogleSso": true,
    "AllowFeid": true,
    "AutoCreateVisitorOnExternalLogin": true,
    "AutoCreateInternalOnExternalLogin": false
  }
}
```

Frontend cũng cần config tương ứng:

```env
VITE_AUTH_MODE=DevMixed
VITE_ENABLE_PASSWORD_LOGIN=true
VITE_ENABLE_GOOGLE_SSO=true
VITE_ENABLE_FEID=true
```

Production frontend:

```env
VITE_AUTH_MODE=ProductionSsoOnly
VITE_ENABLE_PASSWORD_LOGIN=false
VITE_ENABLE_GOOGLE_SSO=true
VITE_ENABLE_FEID=true
```

---

## 4. Quy tắc xử lý theo từng cổng

## 4.1 VISITOR_PORTAL

### Case V1 — SSO/FEID, email chưa tồn tại trong DB

Khi user đăng nhập qua Google SSO/FEID ở cổng Visitor và email chưa tồn tại:

```text
- Tạo mới user.
- Role = VISITOR.
- CampusId/PrimaryCampusId = NULL.
- DepartmentId = NULL.
- Status = ACTIVE.
- PasswordHash = NULL.
- CreatedVia = SSO_AUTO_PROVISION hoặc EXTERNAL_LOGIN.
- Ghi user_auth_provider với ProviderType = GOOGLE hoặc FEID.
- Tạo session/JWT.
- Cho đăng nhập vào Visitor dashboard/visitor area.
```

Không được yêu cầu chọn campus ở Visitor portal.

### Case V2 — SSO/FEID, email đã tồn tại và role là VISITOR

```text
- Không tạo user mới.
- Cập nhật/đồng bộ user_auth_provider nếu cần.
- Kiểm tra Status = ACTIVE.
- Tạo session/JWT.
- Cho đăng nhập bình thường.
```

### Case V3 — SSO/FEID, email đã tồn tại nhưng role là nội bộ

Nếu user có role nội bộ nhưng lại login ở Visitor portal:

```text
- Từ chối đăng nhập.
- Không tạo user mới.
- Không đổi role.
- Trả lỗi rõ ràng.
```

Message gợi ý:

```text
Tài khoản của bạn thuộc cổng nội bộ. Vui lòng đăng nhập tại cổng nội bộ và chọn đúng cơ sở.
```

Error code:

```text
WRONG_PORTAL_INTERNAL_ACCOUNT
```

HTTP status:

```text
403 Forbidden
```

### Case V4 — Visitor login bằng password trong dev/test

Chỉ cho phép nếu:

```text
- AuthOptions.AllowPasswordLogin = true.
- User có PasswordHash hoặc auth provider LOCAL_PASSWORD.
- Role là VISITOR.
- PortalType là VISITOR_PORTAL.
```

Nếu production hoặc AllowPasswordLogin = false:

```text
PASSWORD_LOGIN_DISABLED
```

---

## 4.2 INTERNAL_PORTAL

### Rule chung

Cổng Internal là cổng dành cho role nội bộ.

Bắt buộc payload login phải có:

```json
{
  "portalType": "INTERNAL",
  "selectedCampusId": 1
}
```

Nếu thiếu campus:

```text
CAMPUS_REQUIRED
Vui lòng chọn cơ sở trước khi đăng nhập.
```

HTTP status:

```text
400 Bad Request
```

### Case I1 — SSO/FEID, email chưa tồn tại trong DB

Đây là điểm khác Visitor portal.

```text
- Không được auto-create user.
- Từ chối đăng nhập.
- Gợi ý người dùng liên hệ Staff Leader/IC/Admin để được tạo tài khoản.
```

Message gợi ý:

```text
Tài khoản của bạn chưa được tạo trong hệ thống nội bộ. Vui lòng liên hệ Staff Leader hoặc quản trị viên của cơ sở để được cấp quyền đăng nhập.
```

Error code:

```text
INTERNAL_ACCOUNT_NOT_FOUND
```

HTTP status:

```text
403 Forbidden
```

### Case I2 — SSO/FEID, email tồn tại nhưng role là VISITOR

```text
- Từ chối đăng nhập.
- Không tự chuyển role.
- Không tự gán campus.
```

Message gợi ý:

```text
Tài khoản của bạn hiện là Visitor nên không phù hợp với cổng nội bộ. Vui lòng liên hệ Staff Leader của cơ sở để được cập nhật vai trò nếu bạn cần truy cập nội bộ.
```

Error code:

```text
WRONG_PORTAL_VISITOR_ACCOUNT
```

HTTP status:

```text
403 Forbidden
```

### Case I3 — SSO/FEID, email tồn tại và role nội bộ

Cho login nếu tất cả điều kiện đúng:

```text
- Status = ACTIVE.
- Role thuộc nhóm internal.
- selectedCampusId tồn tại và active.
- User có PrimaryCampusId/CampusId khớp selectedCampusId.
- Nếu role cần department thì DepartmentId hợp lệ và active.
- Provider GOOGLE/FEID được phép theo config.
```

Nếu campus không khớp:

```text
CAMPUS_MISMATCH
Tài khoản của bạn không thuộc cơ sở đã chọn. Vui lòng chọn đúng cơ sở hoặc liên hệ quản trị viên.
```

HTTP status:

```text
403 Forbidden
```

### Case I4 — Internal login bằng password trong dev/test

Chỉ cho phép nếu:

```text
- AuthOptions.AllowPasswordLogin = true.
- LoginMode = DevMixed.
- User có PasswordHash hoặc LOCAL_PASSWORD provider.
- User role thuộc nhóm internal.
- selectedCampusId hợp lệ và khớp user campus.
```

Production phải chặn credentials login.

---

## 5. FEID cho sinh viên

Nếu hệ thống có đăng nhập FEID cho sinh viên:

```text
- Không hardcode rule mơ hồ trong handler.
- Tạo config `StudentFeidMinCohort`, ví dụ K19.
- Viết service riêng để check FEID eligibility:
  - IFeidEligibilityService ở Application.
  - FeidEligibilityService ở Infrastructure hoặc Identity.
- Nếu provider trả về studentCode/cohort, kiểm tra tại service.
- Nếu chưa có claim rõ ràng, để TODO và trả lỗi có kiểm soát, không cho login bừa.
```

Gợi ý rule:

```text
- FEID được phép nếu user đã tồn tại trong DB và role là STUDENT hoặc internal role được cấu hình cho FEID.
- FEID có thể auto-create chỉ khi portal là VISITOR_PORTAL và hệ thống cho phép visitor auto-provision.
- Không auto-create STUDENT/internal account bằng FEID.
```

---

## 6. Account provisioning và update role

## 6.1 Staff Leader update Visitor thành role nội bộ

Trường hợp user đang là VISITOR nhưng muốn đăng nhập cổng nội bộ:

```text
- User phải liên hệ Staff Leader của campus.
- Staff Leader dùng chức năng Update Account Role.
- Sau update:
  - Role đổi từ VISITOR sang role nội bộ được chọn.
  - CampusId/PrimaryCampusId phải được gán.
  - Campus mặc định lấy theo campus mà Staff Leader đang quản lý.
  - Nếu UI cho chọn campus thì chỉ cho chọn campus nằm trong quyền quản lý của Staff Leader.
  - Nếu role thuộc DEPT thì phải chọn Department hợp lệ thuộc cùng campus.
  - Nếu role là STUDENT thì phải có thông tin student nếu hệ thống yêu cầu.
  - UserAuthProvider cũ như GOOGLE/FEID vẫn giữ, không tạo user mới.
  - Thu hồi session/token hiện tại để user đăng nhập lại lấy role mới.
  - Ghi audit log.
```

Message sau update:

```text
Cập nhật vai trò thành công. Người dùng cần đăng nhập lại bằng cổng nội bộ và chọn đúng cơ sở.
```

## 6.2 Staff Leader/Admin tạo account nội bộ mới

Nếu người đó chưa có tài khoản:

```text
- Staff Leader hoặc role có quyền UC-96 Create Account tạo tài khoản.
- Với Staff Leader:
  - Campus mặc định = campus của Staff Leader.
  - Không được tạo account cho campus khác nếu không có quyền.
- Với HO/Admin:
  - Có thể chọn campus nếu permission cho phép.
- Production:
  - Không gửi mật khẩu tạm.
  - Tạo account theo email + role + campus để user login bằng SSO/FEID.
- Dev/test:
  - Có thể tạo password tạm nếu AllowPasswordLogin = true.
```

Tài khoản nội bộ **không được auto-provision khi login**.

---

## 7. Backend cần sửa/kiểm tra

## 7.1 Domain/Constants/Enums

Tạo hoặc cập nhật enum/constant:

```csharp
public enum LoginPortalType
{
    Visitor = 1,
    Internal = 2
}

public enum ExternalAuthProviderType
{
    Google = 1,
    Feid = 2,
    LocalPassword = 3
}

public enum AuthLoginMode
{
    DevMixed = 1,
    ProductionSsoOnly = 2
}
```

Không bắt buộc dùng enum nếu project đang dùng string constants, nhưng phải có constant tập trung.

Cần có role group helper:

```csharp
public static class RoleGroups
{
    public static readonly string[] VisitorRoles = { "VISITOR" };

    public static readonly string[] InternalRoles =
    {
        "ADMIN",
        "HO",
        "STAFF",
        "STAFF_L",
        "STAFF_P",
        "DEPT",
        "DEPT_L",
        "DEPT_P",
        "STUDENT"
    };
}
```

Nếu project hiện tại chỉ có `STAFF`, `DEPT`, giữ `STAFF`, `DEPT`; không ép tách role.

---

## 7.2 Application models

Tạo/cập nhật request DTO:

```csharp
public sealed class LoginViaCredentialsCommand : IRequest<AuthResponse>
{
    public string Email { get; init; } = default!;
    public string Password { get; init; } = default!;
    public string PortalType { get; init; } = default!; // VISITOR | INTERNAL
    public int? SelectedCampusId { get; init; }
}

public sealed class LoginViaSsoCommand : IRequest<AuthResponse>
{
    public string Provider { get; init; } = default!; // GOOGLE | FEID
    public string IdTokenOrCode { get; init; } = default!;
    public string PortalType { get; init; } = default!;
    public int? SelectedCampusId { get; init; }
}
```

AuthResponse phải trả đủ:

```csharp
public sealed class AuthResponse
{
    public string AccessToken { get; init; } = default!;
    public string RefreshToken { get; init; } = default!;
    public AuthUserDto User { get; init; } = default!;
    public IReadOnlyList<string> Permissions { get; init; } = Array.Empty<string>();
    public string PortalType { get; init; } = default!;
    public int? SelectedCampusId { get; init; }
}
```

AuthUserDto không được trả PasswordHash/token nội bộ.

---

## 7.3 Application services/interfaces

Tạo/cập nhật các interface:

```text
IAuthPolicyService
IExternalIdentityVerifier
IFeidEligibilityService
IUserProvisioningService
ISessionService
ITokenService
IUserRepository
IRoleRepository
ICampusRepository
IAuditLogService
```

Trong đó `IAuthPolicyService` chịu trách nhiệm kiểm tra:

```text
- Portal có hợp lệ không.
- Role có được phép vào portal không.
- Internal portal có campus chưa.
- Campus user có khớp selectedCampusId không.
- Password login có được bật không.
- External provider có được bật không.
```

Không nhét toàn bộ logic vào Controller.

---

## 7.4 Handler logic bắt buộc

### LoginViaSsoCommandHandler

Pseudo-flow bắt buộc:

```csharp
// 1. Validate provider enabled by config.
// 2. Verify token/code with Google/FEID provider.
// 3. Normalize email.
// 4. Load user by email including Role, Campus, AuthProviders.
// 5. Branch by PortalType.

if (portal == VISITOR)
{
    if (user == null)
    {
        if (!options.AutoCreateVisitorOnExternalLogin)
            throw new ForbiddenException(...);

        user = User.CreateVisitorFromExternalLogin(email, fullName, provider);
        await userRepository.AddAsync(user);
        await authProviderRepository.LinkAsync(user.Id, provider, providerSubject);
    }
    else
    {
        authPolicy.EnsureUserCanLoginVisitorPortal(user);
        await authProviderRepository.EnsureLinkedAsync(user.Id, provider, providerSubject);
    }

    return await authResultBuilder.BuildAsync(user, portal, selectedCampusId: null);
}

if (portal == INTERNAL)
{
    authPolicy.EnsureCampusSelected(selectedCampusId);

    if (user == null)
        throw new ForbiddenException("INTERNAL_ACCOUNT_NOT_FOUND", ...);

    authPolicy.EnsureUserCanLoginInternalPortal(user, selectedCampusId.Value);

    await authProviderRepository.EnsureLinkedAsync(user.Id, provider, providerSubject);

    return await authResultBuilder.BuildAsync(user, portal, selectedCampusId.Value);
}
```

### LoginViaCredentialsCommandHandler

Pseudo-flow bắt buộc:

```csharp
// 1. Check AuthOptions.AllowPasswordLogin.
// 2. If ProductionSsoOnly -> reject.
// 3. Normalize email.
// 4. Load user by email including Role, Campus.
// 5. If not found -> return INVALID_CREDENTIALS, except internal SSO not-found case only belongs SSO flow.
// 6. Verify password.
// 7. Apply same portal/campus/role policy as SSO.
// 8. Create session/JWT.
// 9. Audit login success/fail.
```

Không được bypass portal/campus check cho credential login.

---

## 7.5 Controller

`AuthenticationController` chỉ nhận request và gọi MediatR.

Không viết business logic trong Controller.

Endpoint gợi ý, tùy ApiRoutes hiện tại mà áp dụng:

```http
POST /api/auth/login
POST /api/auth/sso
POST /api/auth/logout
POST /api/auth/refresh-token
GET  /api/auth/me
```

Hoặc nếu tách provider:

```http
GET  /api/auth/sso/google/start?portalType=INTERNAL&selectedCampusId=1
POST /api/auth/sso/google/callback
GET  /api/auth/sso/feid/start?portalType=INTERNAL&selectedCampusId=1
POST /api/auth/sso/feid/callback
```

Nếu dùng OAuth redirect flow, phải lưu `portalType` và `selectedCampusId` vào `state` có ký/chống giả mạo.

---

## 8. Database/schema cần kiểm tra

Vì project theo hướng database-first/manual SQL, không tự auto-migrate bừa.

Tạo file SQL delta nếu thiếu cột/bảng:

```text
database/scripts/patch_auth_dual_portal_sso_first.sql
```

Kiểm tra tối thiểu các bảng/cột:

### users

Cần có hoặc tương đương:

```text
user_id
email unique
full_name/display_name
role_id
primary_campus_id nullable
department_id nullable
status
password_hash nullable
created_via
last_login_at
created_at
updated_at
```

`primary_campus_id` được phép NULL cho VISITOR.

### user_auth_providers

Cần có hoặc tương đương:

```text
user_auth_provider_id
user_id
provider_type       -- GOOGLE | FEID | LOCAL_PASSWORD
provider_subject    -- sub/id từ provider
provider_email
linked_at
last_used_at
```

Unique nên có:

```text
UNIQUE(provider_type, provider_subject)
UNIQUE(user_id, provider_type)
```

### user_sessions

Cần hỗ trợ revoke khi:

```text
- Logout.
- Update role.
- Manage account status: LOCKED/INACTIVE.
```

### login_logs / security_events / audit_logs

Cần ghi:

```text
- Login success/fail.
- Wrong portal.
- Campus mismatch.
- Internal account not found.
- Password login disabled.
- Role updated.
- Session revoked after role update.
```

---

## 9. Frontend cần sửa

## 9.1 Không phá UI hiện tại

Không được:

```text
- Viết lại toàn bộ frontend.
- Xóa các page/dashboard hiện có.
- Đổi route hàng loạt nếu không bắt buộc.
- Đổi layout/sidebar nếu không liên quan login.
```

Được phép:

```text
- Sửa LoginModalDualPortal/LoginPageDualPortal.
- Thêm auth API layer.
- Thêm type/constant/helper.
- Thêm error message mapping.
- Bọc route bằng portal/role guard.
```

---

## 9.2 Login UI mong muốn

Frontend phải thể hiện rõ 2 cổng:

```text
1. Cổng Visitor
   - Không có dropdown campus.
   - Có nút Google SSO.
   - Có nút FEID nếu bật.
   - Có form email/password nếu VITE_ENABLE_PASSWORD_LOGIN=true.
   - Text phụ: Dành cho khách/đối tác gửi và theo dõi yêu cầu thăm.

2. Cổng Nội bộ
   - Bắt buộc chọn campus trước khi login.
   - Có dropdown campus active.
   - Có nút Google SSO.
   - Có nút FEID nếu bật.
   - Có form email/password nếu VITE_ENABLE_PASSWORD_LOGIN=true.
   - Text phụ: Dành cho HO/Admin/IC/Department/Student đã được cấp tài khoản.
```

Nếu user bấm login internal mà chưa chọn campus:

```text
Vui lòng chọn cơ sở trước khi đăng nhập.
```

---

## 9.3 Auth API payload frontend

Tạo/cập nhật:

```text
frontend/pems-react/src/features/authentication/api/authApi.ts
frontend/pems-react/src/features/authentication/types/auth.types.ts
frontend/pems-react/src/shared/auth/authStorage.ts
frontend/pems-react/src/shared/auth/ProtectedRoute.tsx
frontend/pems-react/src/shared/auth/RoleGuard.tsx
frontend/pems-react/src/shared/auth/PortalGuard.tsx
frontend/pems-react/src/shared/constants/roles.ts
frontend/pems-react/src/shared/constants/auth.ts
```

Type gợi ý:

```ts
export type LoginPortalType = 'VISITOR' | 'INTERNAL';
export type AuthProvider = 'GOOGLE' | 'FEID' | 'LOCAL_PASSWORD';

export interface LoginCredentialsRequest {
  email: string;
  password: string;
  portalType: LoginPortalType;
  selectedCampusId?: number | null;
}

export interface SsoLoginRequest {
  provider: 'GOOGLE' | 'FEID';
  idTokenOrCode: string;
  portalType: LoginPortalType;
  selectedCampusId?: number | null;
}
```

---

## 9.4 Error message mapping

Frontend phải map error code từ backend ra message dễ hiểu:

```ts
export const AUTH_ERROR_MESSAGES: Record<string, string> = {
  CAMPUS_REQUIRED: 'Vui lòng chọn cơ sở trước khi đăng nhập.',
  CAMPUS_MISMATCH: 'Tài khoản của bạn không thuộc cơ sở đã chọn. Vui lòng chọn đúng cơ sở hoặc liên hệ quản trị viên.',
  WRONG_PORTAL_VISITOR_ACCOUNT: 'Tài khoản của bạn hiện là Visitor nên không phù hợp với cổng nội bộ. Vui lòng liên hệ Staff Leader của cơ sở để được cập nhật vai trò.',
  WRONG_PORTAL_INTERNAL_ACCOUNT: 'Tài khoản của bạn thuộc cổng nội bộ. Vui lòng đăng nhập tại cổng nội bộ và chọn đúng cơ sở.',
  INTERNAL_ACCOUNT_NOT_FOUND: 'Tài khoản của bạn chưa được tạo trong hệ thống nội bộ. Vui lòng liên hệ Staff Leader hoặc quản trị viên của cơ sở để được cấp quyền đăng nhập.',
  PASSWORD_LOGIN_DISABLED: 'Đăng nhập bằng mật khẩu đã bị tắt. Vui lòng sử dụng SSO/FEID.',
  INVALID_CREDENTIALS: 'Email hoặc mật khẩu không đúng.',
  ACCOUNT_INACTIVE: 'Tài khoản của bạn đã bị vô hiệu hóa.',
  ACCOUNT_LOCKED: 'Tài khoản của bạn đang bị khóa. Vui lòng liên hệ quản trị viên.',
};
```

Nếu backend chưa có error code chuẩn, hãy bổ sung error response format thống nhất.

---

## 9.5 Route guard

Sau khi login:

```text
- Nếu portalType = VISITOR và role = VISITOR:
  redirect về visitor dashboard/visitor home hiện có.

- Nếu portalType = INTERNAL và role thuộc internal:
  redirect về internal dashboard hiện có.

- Nếu role/portal không khớp:
  logout local state và hiện lỗi.
```

Không cho:

```text
- Visitor vào dashboard nội bộ.
- Internal account đăng nhập qua visitor portal.
- User nội bộ login thiếu campus.
```

---

## 10. Account Management frontend

Cập nhật màn Account Management để hỗ trợ case Staff Leader update Visitor thành role nội bộ.

Yêu cầu UI:

```text
- Danh sách user hiển thị role, sub_role nếu có, campus, status, provider.
- Form create account có:
  - email
  - fullName
  - role
  - subRole nếu hệ thống có
  - campus
  - department nếu role thuộc DEPT
  - password field chỉ hiện nếu dev/test và enable password login
- Form update role có:
  - role mới
  - subRole nếu có
  - campus
  - department nếu cần
```

Rule UI:

```text
- Nếu người thao tác là Staff Leader:
  - Campus mặc định là campus của Staff Leader.
  - Không cho chọn campus khác nếu không có quyền.
- Nếu update từ VISITOR sang internal:
  - Bắt buộc gán campus.
  - Gợi ý message: "Sau khi đổi role, người dùng cần đăng nhập lại ở cổng nội bộ."
- Nếu role mới là VISITOR:
  - Campus có thể NULL.
  - Không cho giữ department.
```

---

## 11. Permission/UC mapping cần giữ

Các UC liên quan:

```text
UC-10 Login via SSO
UC-11 Login via Credentials
UC-12 Logout
UC-13 Forgot Password
UC-14 View Profile
UC-15 Update Profile
UC-16 Change Password
UC-95 View Account List
UC-96 Create Account
UC-97 Manage Account Status
UC-98 View Account Details
UC-99 Search and Filter Accounts
UC-100 Update Account Role
```

Không được bỏ permission check ở các API account management.

Login endpoint có thể `[AllowAnonymous]`, nhưng handler vẫn phải tự kiểm tra portal/campus/status/provider/config.

---

## 12. Error response format chuẩn

Nếu project chưa có format lỗi chuẩn, tạo/cập nhật format:

```json
{
  "success": false,
  "errorCode": "WRONG_PORTAL_VISITOR_ACCOUNT",
  "message": "Tài khoản của bạn hiện là Visitor nên không phù hợp với cổng nội bộ.",
  "traceId": "..."
}
```

Exception gợi ý:

```csharp
public class AuthBusinessException : Exception
{
    public string ErrorCode { get; }
    public int StatusCode { get; }
}
```

Hoặc dùng exception hiện có như `ForbiddenException`, `ValidationException`, `AuthenticationFailedException` nhưng phải có `ErrorCode`.

---

## 13. Test case bắt buộc

Tạo/cập nhật test ở:

```text
tests/PEMS.ApplicationTests/Authentication/
tests/PEMS.IntegrationTests/Security/
tests/PEMS.ApplicationTests/Accounts/
```

### 13.1 Auth policy tests

| Case | Expected |
|---|---|
| Visitor SSO email chưa tồn tại | Tạo VISITOR, campus NULL, login success |
| Visitor SSO email đã là VISITOR | Login success |
| Visitor portal email là internal role | 403 WRONG_PORTAL_INTERNAL_ACCOUNT |
| Internal SSO thiếu campus | 400 CAMPUS_REQUIRED |
| Internal SSO email chưa tồn tại | 403 INTERNAL_ACCOUNT_NOT_FOUND |
| Internal SSO email là VISITOR | 403 WRONG_PORTAL_VISITOR_ACCOUNT |
| Internal SSO email là STAFF đúng campus | Login success |
| Internal SSO email là STAFF sai campus | 403 CAMPUS_MISMATCH |
| Credentials login dev đúng portal/campus | Login success |
| Credentials login production | 403 PASSWORD_LOGIN_DISABLED |
| Locked account login | 403/423 ACCOUNT_LOCKED |
| Inactive account login | 403 ACCOUNT_INACTIVE |

### 13.2 Account role update tests

| Case | Expected |
|---|---|
| Staff Leader update VISITOR sang STAFF cùng campus | Role đổi, campus gán, session revoked |
| Staff Leader update VISITOR sang DEPT thiếu department | Validation error |
| Staff Leader update user sang campus khác | Forbidden nếu không có quyền |
| HO/Admin update role có chọn campus hợp lệ | Success |
| Update role xong user login lại internal đúng campus | Success |
| Visitor sau khi đổi role login visitor portal | 403 WRONG_PORTAL_INTERNAL_ACCOUNT |

---

## 14. Các lệnh phải chạy sau khi sửa

Backend:

```bash
dotnet restore
dotnet build
dotnet test
```

Nếu solution là `.slnx`, dùng file solution hiện tại.

Frontend:

```bash
cd frontend/pems-react
npm install
npm run build
```

Nếu có lint/typecheck:

```bash
npm run lint
npm run typecheck
```

Database:

```text
- Không tự chạy destructive SQL.
- Tạo patch SQL idempotent.
- Ghi rõ cần chạy script nào.
```

---

## 15. Output bắt buộc sau khi làm xong

Sau khi code xong, tạo/cập nhật các file docs:

```text
docs/auth/AUTH_DUAL_PORTAL_SSO_FIRST.md
docs/auth/AUTH_TEST_CASES.md
docs/architecture/REFACTOR_CHANGELOG.md
docs/api/API_ROUTE_CONVENTION.md nếu route auth thay đổi
docs/database/DATABASE_DEPLOYMENT.md nếu có patch SQL
```

Trong `AUTH_DUAL_PORTAL_SSO_FIRST.md`, ghi rõ:

```markdown
# Auth Dual Portal SSO-first

## 1. Login modes
## 2. Portal types
## 3. Visitor auto-provisioning
## 4. Internal account pre-provisioning
## 5. Campus validation
## 6. Role update from Visitor to Internal
## 7. Production behavior
## 8. Error codes
## 9. Test matrix
```

Trong `REFACTOR_CHANGELOG.md`, ghi rõ:

```markdown
# Refactor Changelog

## Summary
## Files Changed
## Backend Changes
## Frontend Changes
## Database Changes
## Tests Added
## Commands Run
## Remaining TODOs
## Risks
```

---

## 16. Checklist nghiệm thu cuối cùng

Chỉ báo hoàn thành khi đạt đủ:

```text
[ ] Có 2 cổng login rõ ràng: Visitor và Internal.
[ ] Visitor portal không yêu cầu campus.
[ ] Internal portal bắt buộc chọn campus.
[ ] Dev/test login được bằng email + password nếu bật config.
[ ] Production tắt email + password.
[ ] Google SSO login được theo portal.
[ ] FEID login có service/config kiểm tra eligibility.
[ ] Visitor SSO lần đầu auto-create VISITOR, campus NULL.
[ ] Internal SSO lần đầu không auto-create, trả message rõ.
[ ] Visitor login nhầm internal portal bị chặn.
[ ] Internal user login nhầm visitor portal bị chặn.
[ ] Internal user chọn sai campus bị chặn.
[ ] Staff Leader update Visitor sang role nội bộ có gán campus.
[ ] Sau update role, session cũ bị revoke.
[ ] Account Management frontend hỗ trợ create/update role/campus.
[ ] Frontend hiển thị đúng error message.
[ ] Không phá UI/route/dashboard hiện có.
[ ] dotnet build pass.
[ ] npm run build pass.
[ ] Có test case auth/account role update.
[ ] Có docs auth + changelog.
```

---

## 17. Nguyên tắc không được vi phạm

```text
- Không auto-create internal user khi login SSO/FEID.
- Không cho Visitor vào internal portal.
- Không cho internal user vào visitor portal bằng nhầm cổng.
- Không bỏ qua campus check ở internal portal.
- Không tự gán campus cho Visitor khi Visitor chỉ login hoặc gửi form public.
- Chỉ gán campus cho Visitor khi Staff Leader/Admin/HO update role sang internal.
- Không lưu password cho account production SSO/FEID.
- Không trả PasswordHash/token nội bộ ra frontend.
- Không viết business logic trong Controller.
- Không gọi DbContext trực tiếp trong Controller.
- Không refactor frontend hàng loạt ngoài phạm vi auth/account.
- Không xóa file cũ nếu chưa chắc.
```

---

## 18. Gợi ý thứ tự triển khai

Thực hiện theo thứ tự này để tránh vỡ code:

```text
PHASE 1 — Quét code Auth/Account hiện tại
PHASE 2 — Thêm AuthOptions + constants Portal/AuthProvider/LoginMode
PHASE 3 — Chuẩn hóa backend request/response/error code
PHASE 4 — Viết AuthPolicyService
PHASE 5 — Sửa LoginViaSsoCommandHandler
PHASE 6 — Sửa LoginViaCredentialsCommandHandler
PHASE 7 — Sửa Account Create/UpdateRole handler
PHASE 8 — Sửa session revoke khi update role/status
PHASE 9 — Sửa frontend LoginDualPortal
PHASE 10 — Sửa frontend authApi/authStorage/error mapping
PHASE 11 — Sửa Account Management UI phần role/campus
PHASE 12 — Thêm tests
PHASE 13 — Build/test/backend/frontend
PHASE 14 — Cập nhật docs/changelog
```

---

## 19. Báo cáo sau khi hoàn thành

Khi làm xong, báo cáo bằng format:

```markdown
# Báo cáo sửa Login Dual Portal SSO-first

## 1. Tóm tắt
## 2. File đã sửa
## 3. Backend đã thay đổi gì
## 4. Frontend đã thay đổi gì
## 5. Database/SQL patch nếu có
## 6. Test case đã chạy
## 7. Kết quả build
## 8. Các case login đã pass
## 9. Rủi ro/TODO còn lại
```
