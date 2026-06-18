# PROMPT TRIỂN KHAI CORE AUTH BACKEND PEMS — DUAL PORTAL SSO-FIRST

> File này dùng để giao cho AI/code agent sửa code trực tiếp.  
> Mục tiêu: hoàn thành thật các UC Authentication core của PEMS, không chỉ scaffold, theo logic **Dual Portal + SSO-first + password login chỉ dùng dev/test**.

---

## 0. Vai trò của AI/code agent

Bạn là **Senior .NET Clean Architecture Developer + Security Engineer + React Integration Reviewer**.

Bạn đang làm trên project **PEMS — Partnership Engagement Management System** hiện tại.  
Nhiệm vụ của bạn là hoàn thành phần **Core Auth Backend** để các UC sau chạy thật:

```text
UC-10 Login via SSO
UC-11 Login via Credentials
UC-12 Logout
UC-13 Forgot Password
GET /auth/me
GET /auth/permissions nếu project đang tách endpoint permission
```

Không làm lại toàn bộ hệ thống. Không phá frontend hiện có. Không fake success cho SSO/FEID. Không tự tạo tài khoản nội bộ khi SSO lần đầu.

---

## 1. Mục tiêu cuối cùng của phase này

Sau phase này, hệ thống phải đạt:

```text
[ ] Backend build pass.
[ ] Swagger/API chạy được.
[ ] Login credentials chạy được trong dev/test.
[ ] Production/config SSO-only chặn credentials login.
[ ] Google SSO có pipeline thật hoặc adapter thật kiểm tra token/code.
[ ] FEID có adapter/service rõ ràng; nếu chưa có provider credential thật thì trả lỗi có kiểm soát, không fake login success.
[ ] Visitor portal login SSO lần đầu auto-create VISITOR.
[ ] Internal portal login SSO lần đầu không auto-create account.
[ ] Internal portal bắt buộc selectedCampusId.
[ ] Wrong portal trả 403 có errorCode rõ.
[ ] Campus mismatch trả 403 có errorCode rõ.
[ ] Logout revoke session/refresh token.
[ ] /auth/me kiểm tra token + session hiện tại.
[ ] Forgot password chỉ áp dụng LOCAL_PASSWORD/dev-test.
[ ] Có login_logs/security_events/audit log ở các điểm quan trọng nếu bảng/service đã có.
```

---

## 2. Phạm vi làm lần này

### 2.1 In scope

Chỉ làm các phần sau:

```text
1. Core Auth Backend
   - AuthOptions/LoginMode config.
   - Constants/enums cho portal/provider/login mode/error codes.
   - LoginViaSsoCommand.
   - LoginViaCredentialsCommand.
   - LogoutCommand.
   - ForgotPasswordCommand.
   - RefreshTokenCommand nếu đang có và đang được frontend dùng.
   - GetCurrentUserQuery (/auth/me).
   - GetCurrentUserPermissionsQuery nếu frontend đang gọi riêng.
   - AuthPolicyService.
   - UserProvisioningService cho Visitor auto-create.
   - SessionService/token service integration.
   - Error contract có errorCode.

2. Minimal frontend compatibility
   - Không làm lại UI.
   - Chỉ đảm bảo frontend cũ vẫn đọc được `message`.
   - Thêm `errorCode` vào response lỗi nhưng không xóa message cũ.

3. Minimal database check
   - Không auto-migrate bừa.
   - Nếu thiếu cột/bảng thì tạo SQL patch idempotent.
```

### 2.2 Out of scope

Không làm trong phase này:

```text
- Không làm lại toàn bộ frontend Login page.
- Không làm full Account Management UI.
- Không làm UC-96 Create Account full nếu chưa được yêu cầu trong phase này.
- Không làm UC-100 Update Account Role full nếu chưa được yêu cầu trong phase này.
- Không làm full automated tests nếu scope tool không yêu cầu, nhưng phải tạo test checklist và skeleton nếu project đã có test.
- Không tự đổi mô hình role toàn hệ thống.
- Không đổi database destructive.
```

---

## 3. Tài liệu và code bắt buộc đọc trước

Trước khi sửa, đọc các file tài liệu:

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
backend/PEMS.Api/Extensions/AuthenticationExtensions.cs
backend/PEMS.Api/Middleware/SessionValidationMiddleware.cs
backend/PEMS.Api/Middleware/ExceptionHandlingMiddleware.cs

backend/PEMS.Application/Authentication/**
backend/PEMS.Application/Accounts/**
backend/PEMS.Application/Profiles/**
backend/PEMS.Application/Common/Interfaces/**
backend/PEMS.Application/Common/Exceptions/**

backend/PEMS.Domain/Entities/User.cs
backend/PEMS.Domain/Entities/Role.cs
backend/PEMS.Domain/Entities/UserAuthProvider.cs
backend/PEMS.Domain/Entities/UserSession.cs
backend/PEMS.Domain/Constants/**
backend/PEMS.Domain/Enums/**

backend/PEMS.Infrastructure/Persistence/**
backend/PEMS.Infrastructure/Repositories/**
backend/PEMS.Infrastructure/Identity/**
backend/PEMS.Infrastructure/ExternalServices/**
backend/PEMS.Infrastructure/Services/**
```

Nếu file/tên folder khác thực tế, dùng cấu trúc thật trong `PROJECT_STRUCTURE_FULL.md` và ghi lại trong changelog.

---

## 4. Nguyên tắc kiến trúc bắt buộc

### 4.1 Controller không chứa business logic

`AuthenticationController` chỉ làm:

```text
- Nhận request DTO.
- Gọi IMediator.Send(command/query).
- Trả response.
```

Không được:

```text
- Gọi DbContext trực tiếp.
- Tự check role/campus trong controller.
- Tự tạo JWT trong controller.
- Tự hash password trong controller.
```

### 4.2 Handler chứa orchestration

Handler được phép:

```text
- Load user/session/provider.
- Gọi AuthPolicyService để check rule.
- Gọi UserProvisioningService để auto-create Visitor.
- Gọi TokenService/SessionService.
- Gọi Audit/LoginLog service.
```

Handler không nên chứa 200 dòng if/else khó bảo trì. Các rule lặp lại phải đưa vào service.

### 4.3 Validator chỉ kiểm tra input format

FluentValidation chỉ check:

```text
- Email rỗng/sai format.
- Password rỗng.
- PortalType hợp lệ.
- Provider hợp lệ.
- selectedCampusId là số dương nếu có.
- idTokenOrCode không rỗng.
```

Không dùng Validator để query database.

### 4.4 Business rule nằm ở service/handler

Các rule sau phải nằm ở `AuthPolicyService` hoặc handler:

```text
- Password login có được bật không.
- Provider SSO có được bật không.
- User role có đúng portal không.
- Internal portal có selectedCampusId không.
- User campus có khớp selectedCampusId không.
- Account ACTIVE/LOCKED/INACTIVE.
- Internal SSO có được auto-create không.
- Visitor SSO có được auto-create không.
```

---

## 5. Khái niệm chuẩn phải dùng

## 5.1 Portal

Hệ thống có 2 portal:

```text
VISITOR
INTERNAL
```

Hoặc nếu code hiện tại dùng tên khác:

```text
VISITOR_PORTAL
INTERNAL_PORTAL
```

Thống nhất mapping ở một nơi, không hardcode rải rác.

### VISITOR portal

```text
- Dành cho Visitor/khách.
- Không chọn campus khi login.
- SSO/FEID lần đầu có thể auto-create tài khoản VISITOR.
- Campus của VISITOR mặc định NULL.
```

### INTERNAL portal

```text
- Dành cho HO/Admin/Staff/Department/Student/internal users.
- Bắt buộc chọn campus khi login.
- Không auto-create account khi SSO/FEID lần đầu.
- User phải tồn tại trước trong DB.
```

---

## 5.2 Login mode

Tạo/cập nhật config:

```json
{
  "AuthOptions": {
    "LoginMode": "DevMixed",
    "AllowPasswordLogin": true,
    "AllowGoogleSso": true,
    "AllowFeid": true,
    "AutoCreateVisitorOnExternalLogin": true,
    "AutoCreateInternalOnExternalLogin": false,
    "StudentFeidMinCohort": "K19",
    "AccessTokenMinutes": 60,
    "RefreshTokenDays": 7
  }
}
```

Production:

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

### Rule

```text
DevMixed:
- Cho phép password login nếu AllowPasswordLogin = true.
- Cho phép SSO theo provider flag.

ProductionSsoOnly:
- Chặn password login tuyệt đối.
- Forgot password local không reset cho SSO-only account.
- Internal user vẫn phải tồn tại trước trong DB.
```

---

## 5.3 Provider

Provider chuẩn:

```text
LOCAL_PASSWORD
GOOGLE
FEID
```

Không dùng string rải rác. Tạo constants/enums:

```csharp
public static class AuthProviders
{
    public const string LocalPassword = "LOCAL_PASSWORD";
    public const string Google = "GOOGLE";
    public const string Feid = "FEID";
}
```

---

## 5.4 Role group

Tạo helper tập trung:

```csharp
public static class RoleGroups
{
    public static readonly string[] VisitorRoles =
    {
        "VISITOR"
    };

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

    public static bool IsVisitor(string roleCode)
        => VisitorRoles.Contains(roleCode, StringComparer.OrdinalIgnoreCase);

    public static bool IsInternal(string roleCode)
        => InternalRoles.Contains(roleCode, StringComparer.OrdinalIgnoreCase);
}
```

Nếu DB hiện tại chỉ có role gộp:

```text
ADMIN
HO
STAFF
DEPT
STUDENT
VISITOR
```

thì giữ nguyên, không ép tách `STAFF_L/STAFF_P` hoặc `DEPT_L/DEPT_P`. Nếu có `sub_role` thì dùng sub_role trong Account phase, không đưa vào core login nếu chưa cần.

---

## 6. API contract chuẩn

## 6.1 Login credentials request

```json
{
  "email": "staff.hn@fpt.edu.vn",
  "password": "Password@123",
  "portalType": "INTERNAL",
  "selectedCampusId": 1
}
```

Visitor:

```json
{
  "email": "visitor@example.com",
  "password": "Password@123",
  "portalType": "VISITOR",
  "selectedCampusId": null
}
```

C# command:

```csharp
public sealed class LoginViaCredentialsCommand : IRequest<AuthResponse>
{
    public string Email { get; init; } = default!;
    public string Password { get; init; } = default!;
    public string PortalType { get; init; } = default!;
    public int? SelectedCampusId { get; init; }
}
```

---

## 6.2 SSO request

Nếu frontend gửi idToken trực tiếp:

```json
{
  "provider": "GOOGLE",
  "idTokenOrCode": "eyJ...",
  "portalType": "VISITOR",
  "selectedCampusId": null
}
```

Nếu dùng OAuth redirect/callback:

```text
GET /api/auth/sso/google/start?portalType=INTERNAL&selectedCampusId=1
POST /api/auth/sso/google/callback
```

Khi dùng redirect flow, `portalType` và `selectedCampusId` phải được lưu trong `state` có ký/chống giả mạo.

C# command:

```csharp
public sealed class LoginViaSsoCommand : IRequest<AuthResponse>
{
    public string Provider { get; init; } = default!;
    public string IdTokenOrCode { get; init; } = default!;
    public string PortalType { get; init; } = default!;
    public int? SelectedCampusId { get; init; }
}
```

---

## 6.3 Auth response

```json
{
  "accessToken": "...",
  "refreshToken": "...",
  "expiresAt": "2026-06-18T12:00:00Z",
  "portalType": "INTERNAL",
  "selectedCampusId": 1,
  "user": {
    "userId": 123,
    "email": "staff.hn@fpt.edu.vn",
    "fullName": "Nguyen Van A",
    "roleCode": "STAFF",
    "roleName": "Staff",
    "subRole": "STAFF_L",
    "campusId": 1,
    "campusCode": "HN",
    "campusName": "FPTU Hà Nội",
    "departmentId": null,
    "departmentName": null,
    "status": "ACTIVE"
  },
  "permissions": [
    "UC-10.LOGIN_VIA_SSO",
    "UC-12.LOGOUT"
  ]
}
```

Không trả:

```text
- PasswordHash
- Refresh token hash nếu DB lưu hash
- Provider secret
- Security stamp
- Internal claims không cần thiết
```

---

## 6.4 Error response

Giữ message cũ nhưng thêm `errorCode`.

```json
{
  "success": false,
  "errorCode": "WRONG_PORTAL_VISITOR_ACCOUNT",
  "message": "Tài khoản của bạn hiện là Visitor nên không phù hợp với cổng nội bộ. Vui lòng liên hệ Staff Leader của cơ sở để được cập nhật vai trò.",
  "traceId": "00-..."
}
```

Nếu response hiện tại có format khác, không phá format cũ. Chỉ bổ sung field:

```text
errorCode
traceId nếu đang có
```

---

## 7. Error code bắt buộc

Tạo constants:

```csharp
public static class AuthErrorCodes
{
    public const string InvalidCredentials = "INVALID_CREDENTIALS";
    public const string PasswordLoginDisabled = "PASSWORD_LOGIN_DISABLED";

    public const string CampusRequired = "CAMPUS_REQUIRED";
    public const string CampusMismatch = "CAMPUS_MISMATCH";
    public const string CampusInactive = "CAMPUS_INACTIVE";

    public const string WrongPortalVisitorAccount = "WRONG_PORTAL_VISITOR_ACCOUNT";
    public const string WrongPortalInternalAccount = "WRONG_PORTAL_INTERNAL_ACCOUNT";
    public const string InternalAccountNotFound = "INTERNAL_ACCOUNT_NOT_FOUND";

    public const string AccountInactive = "ACCOUNT_INACTIVE";
    public const string AccountLocked = "ACCOUNT_LOCKED";

    public const string SsoProviderDisabled = "SSO_PROVIDER_DISABLED";
    public const string SsoTokenInvalid = "SSO_TOKEN_INVALID";
    public const string FeidNotConfigured = "FEID_NOT_CONFIGURED";
    public const string FeidNotEligible = "FEID_NOT_ELIGIBLE";

    public const string SessionRevoked = "SESSION_REVOKED";
    public const string TokenExpired = "TOKEN_EXPIRED";
    public const string Unauthorized = "UNAUTHORIZED";

    public const string ForgotPasswordNotAvailable = "FORGOT_PASSWORD_NOT_AVAILABLE";
}
```

### Status code rule

```text
400 Bad Request
- CAMPUS_REQUIRED
- validation input lỗi format
- provider/portal không hợp lệ

401 Unauthorized
- INVALID_CREDENTIALS
- token invalid
- token expired
- session expired/revoked khi gọi protected endpoint

403 Forbidden
- PASSWORD_LOGIN_DISABLED
- WRONG_PORTAL_VISITOR_ACCOUNT
- WRONG_PORTAL_INTERNAL_ACCOUNT
- INTERNAL_ACCOUNT_NOT_FOUND
- CAMPUS_MISMATCH
- SSO_PROVIDER_DISABLED
- FEID_NOT_ELIGIBLE
- ACCOUNT_INACTIVE
- ACCOUNT_LOCKED

500 Internal Server Error
- lỗi bất ngờ, nhưng phải không lộ secret
```

Không dùng 401 cho wrong portal/campus vì frontend interceptor có thể hiểu nhầm là token hết hạn và refresh/logout sai.

---

## 8. Luồng login SSO chi tiết

## 8.1 Bước chung

`LoginViaSsoCommandHandler` phải chạy theo thứ tự:

```text
1. Normalize input:
   - provider uppercase/trim.
   - portalType uppercase/trim.
   - email từ provider lowercase/trim.

2. Check provider allowed:
   - GOOGLE chỉ chạy nếu AllowGoogleSso = true.
   - FEID chỉ chạy nếu AllowFeid = true.

3. Verify external identity:
   - Gọi IExternalIdentityVerifier.VerifyAsync(provider, idTokenOrCode).
   - Không tin email do frontend gửi trực tiếp nếu chưa verify provider token/code.
   - Kết quả phải có: provider, subject, email, displayName, avatarUrl nếu có, raw claims nếu cần.

4. Load user by email:
   - Include Role.
   - Include Campus.
   - Include Department nếu cần.
   - Include AuthProviders.

5. Branch theo portalType.
6. Build auth response.
7. Ghi login log/security event.
```

---

## 8.2 VISITOR portal + SSO

### V1 — Email chưa tồn tại

Nếu:

```text
portalType = VISITOR
user == null
AutoCreateVisitorOnExternalLogin = true
```

Thực hiện:

```text
- Tạo user mới.
- Role = VISITOR.
- CampusId/PrimaryCampusId = NULL.
- DepartmentId = NULL.
- Status = ACTIVE.
- PasswordHash = NULL.
- CreatedVia = SSO_AUTO_PROVISION hoặc EXTERNAL_LOGIN.
- Link user_auth_provider:
  - ProviderType = GOOGLE/FEID
  - ProviderSubject = subject từ provider
  - ProviderEmail = email verified
- Tạo user session.
- Tạo JWT/refresh token.
- Return AuthResponse.
```

Không được:

```text
- Gán campus theo form visit request.
- Gán campus theo selectedCampusId nếu frontend gửi nhầm.
- Tạo role internal.
```

### V2 — Email tồn tại và role VISITOR

Thực hiện:

```text
- Check status ACTIVE.
- Ensure provider link.
- Update last_login_at.
- Create session/token.
- Return success.
```

### V3 — Email tồn tại nhưng role internal

Chặn:

```text
HTTP 403
errorCode = WRONG_PORTAL_INTERNAL_ACCOUNT
message = "Tài khoản của bạn thuộc cổng nội bộ. Vui lòng đăng nhập tại cổng nội bộ và chọn đúng cơ sở."
```

Không đổi role. Không tạo user mới.

---

## 8.3 INTERNAL portal + SSO

### I0 — Thiếu campus

Nếu:

```text
portalType = INTERNAL
selectedCampusId == null
```

Trả:

```text
HTTP 400
errorCode = CAMPUS_REQUIRED
message = "Vui lòng chọn cơ sở trước khi đăng nhập."
```

### I1 — Email chưa tồn tại

Nếu:

```text
portalType = INTERNAL
user == null
```

Trả:

```text
HTTP 403
errorCode = INTERNAL_ACCOUNT_NOT_FOUND
message = "Tài khoản của bạn chưa được tạo trong hệ thống nội bộ. Vui lòng liên hệ Staff Leader hoặc quản trị viên của cơ sở để được cấp quyền đăng nhập."
```

Không auto-create.

### I2 — Email tồn tại nhưng role VISITOR

Trả:

```text
HTTP 403
errorCode = WRONG_PORTAL_VISITOR_ACCOUNT
message = "Tài khoản của bạn hiện là Visitor nên không phù hợp với cổng nội bộ. Vui lòng liên hệ Staff Leader của cơ sở để được cập nhật vai trò nếu bạn cần truy cập nội bộ."
```

Không tự gán campus. Không tự update role.

### I3 — Email tồn tại và role internal

Điều kiện success:

```text
- User.Status = ACTIVE.
- Role thuộc RoleGroups.InternalRoles.
- selectedCampusId tồn tại.
- Campus active.
- User.PrimaryCampusId hoặc User.CampusId khớp selectedCampusId.
- Nếu role yêu cầu department thì DepartmentId hợp lệ.
```

Nếu campus sai:

```text
HTTP 403
errorCode = CAMPUS_MISMATCH
message = "Tài khoản của bạn không thuộc cơ sở đã chọn. Vui lòng chọn đúng cơ sở hoặc liên hệ quản trị viên."
```

Nếu đúng:

```text
- Ensure provider link.
- Update last_login_at.
- Create session/token.
- Return AuthResponse.
```

---

## 9. Luồng login credentials chi tiết

`LoginViaCredentialsCommandHandler` phải dùng chung policy với SSO, không viết logic riêng lệch nhau.

### Bước xử lý

```text
1. Check AuthOptions.AllowPasswordLogin.
2. Check LoginMode != ProductionSsoOnly.
3. Normalize email.
4. Load user by email including Role, Campus, Department, AuthProviders.
5. Nếu user không tồn tại:
   - Return INVALID_CREDENTIALS.
   - Không nói email có tồn tại hay không.
6. Check user status.
7. Check password:
   - User.PasswordHash phải tồn tại hoặc user có LOCAL_PASSWORD provider tùy model hiện tại.
   - Verify bằng password hasher đang dùng.
8. Apply portal policy:
   - VISITOR portal chỉ cho role VISITOR.
   - INTERNAL portal bắt buộc selectedCampusId và role internal/campus match.
9. Create session/token.
10. Ghi login log/security event.
```

### Nếu password login disabled

```text
HTTP 403
errorCode = PASSWORD_LOGIN_DISABLED
message = "Đăng nhập bằng mật khẩu đã bị tắt. Vui lòng sử dụng SSO/FEID."
```

### Nếu sai email/password

```text
HTTP 401
errorCode = INVALID_CREDENTIALS
message = "Email hoặc mật khẩu không đúng."
```

### Lockout/rate limit

Nếu project đã có `failed_login_count`, `locked_until`, `login_logs`:

```text
- Tăng failed_login_count khi sai password.
- Reset failed_login_count khi login thành công.
- Nếu quá giới hạn, lock account hoặc trả ACCOUNT_LOCKED theo rule hiện tại.
```

Nếu chưa có logic lockout thật, không fake. Ghi TODO rõ trong changelog.

---

## 10. AuthPolicyService bắt buộc

Tạo interface ở Application:

```csharp
public interface IAuthPolicyService
{
    void EnsurePasswordLoginAllowed();
    void EnsureExternalProviderAllowed(string provider);
    void EnsureValidPortal(string portalType);
    void EnsureCampusSelectedForInternal(string portalType, int? selectedCampusId);
    void EnsureAccountCanLogin(User user);
    void EnsureUserCanLoginVisitorPortal(User user);
    void EnsureUserCanLoginInternalPortal(User user, int selectedCampusId);
}
```

Implementation có thể nằm ở Application nếu không phụ thuộc Infrastructure, hoặc Infrastructure nếu cần config/service ngoài. Ưu tiên Application service với `IOptions<AuthOptions>` nếu project đang cho phép.

### Rule bên trong

```text
EnsureAccountCanLogin:
- ACTIVE -> ok
- INACTIVE -> ACCOUNT_INACTIVE
- LOCKED -> ACCOUNT_LOCKED

EnsureUserCanLoginVisitorPortal:
- role VISITOR -> ok
- role internal -> WRONG_PORTAL_INTERNAL_ACCOUNT

EnsureUserCanLoginInternalPortal:
- role VISITOR -> WRONG_PORTAL_VISITOR_ACCOUNT
- role không thuộc internal -> WRONG_PORTAL_VISITOR_ACCOUNT hoặc ROLE_NOT_ALLOWED
- campus null -> CAMPUS_MISMATCH
- campus != selectedCampusId -> CAMPUS_MISMATCH
```

---

## 11. External identity verifier

Tạo interface:

```csharp
public interface IExternalIdentityVerifier
{
    Task<ExternalIdentityResult> VerifyAsync(
        string provider,
        string idTokenOrCode,
        CancellationToken cancellationToken);
}
```

Model:

```csharp
public sealed class ExternalIdentityResult
{
    public string Provider { get; init; } = default!;
    public string ProviderSubject { get; init; } = default!;
    public string Email { get; init; } = default!;
    public bool EmailVerified { get; init; }
    public string? DisplayName { get; init; }
    public string? AvatarUrl { get; init; }
    public string? StudentCode { get; init; }
    public string? Cohort { get; init; }
}
```

### Google

Nếu Google SSO đã có package/service:

```text
- Dùng service hiện tại.
- Verify signature/audience/issuer/expiry.
- Không tin token nếu expired/audience sai.
```

Nếu chưa có:

```text
- Tạo GoogleExternalIdentityVerifier skeleton có TODO rõ.
- Không return success giả.
- Trả SSO_TOKEN_INVALID hoặc SSO_PROVIDER_DISABLED nếu chưa cấu hình.
```

### FEID

Nếu FEID chưa có credential/API thật:

```text
- Tạo FeidExternalIdentityVerifier rõ ràng.
- Nếu chưa config endpoint/client secret:
  - throw AuthBusinessException(FEID_NOT_CONFIGURED, 403)
- Không fake user, không fake email.
```

---

## 12. UserProvisioningService cho Visitor

Tạo interface:

```csharp
public interface IUserProvisioningService
{
    Task<User> CreateVisitorFromExternalIdentityAsync(
        ExternalIdentityResult identity,
        CancellationToken cancellationToken);
}
```

Rule:

```text
- Chỉ dùng cho VISITOR portal.
- Chỉ tạo role VISITOR.
- CampusId = NULL.
- DepartmentId = NULL.
- Status = ACTIVE.
- CreatedVia = SSO_AUTO_PROVISION.
- Không set password.
- Link provider.
- SaveChanges theo UnitOfWork/TransactionBehaviour hiện tại.
```

Nếu role VISITOR chưa tồn tại trong DB:

```text
- Throw server/config exception rõ:
  VISITOR_ROLE_NOT_CONFIGURED
- Không tự seed role trong runtime nếu project database-first/manual seed.
```

---

## 13. Session và token rule

### 13.1 Khi login success

Phải tạo:

```text
- access token JWT
- refresh token nếu project có refresh flow
- user_session record nếu project đang dùng DB-backed session
```

JWT claims tối thiểu:

```text
sub/userId
email
roleCode
campusId nếu có
sessionId hoặc jti
permissions version nếu project có
```

Không đưa quá nhiều PII vào JWT.

### 13.2 SessionValidationMiddleware

Khi gọi protected endpoint:

```text
- Decode JWT.
- Lấy sessionId/jti.
- Check user_session tồn tại, chưa revoked, chưa expired.
- Check user status ACTIVE.
- Nếu revoked/expired -> 401 SESSION_REVOKED hoặc TOKEN_EXPIRED.
```

### 13.3 Logout

`LogoutCommand`:

```text
- Lấy current user/session từ ICurrentUserService.
- Revoke current session/refresh token.
- Không revoke tất cả session trừ khi endpoint yêu cầu logout all.
- Trả message thành công.
```

Response:

```json
{
  "success": true,
  "message": "Đăng xuất thành công."
}
```

### 13.4 Refresh token

Nếu đang có refresh token:

```text
- Refresh token phải được lưu hash hoặc secure storage nếu project đã thiết kế.
- Khi refresh:
  - Check refresh token tồn tại.
  - Check session active.
  - Check user active.
  - Rotate refresh token nếu project đang dùng rotation.
```

Nếu chưa ổn, ghi TODO nhưng không làm vỡ login hiện tại.

---

## 14. Forgot password rule

UC-13 chỉ áp dụng cho `LOCAL_PASSWORD`.

### Flow

```text
1. Nhận email.
2. Normalize email.
3. Không tiết lộ email có tồn tại hay không nếu policy hiện tại yêu cầu.
4. Nếu user không tồn tại:
   - Return generic success message.
   - Ghi security event nhẹ nếu cần.
5. Nếu user tồn tại nhưng chỉ có SSO/FEID, không reset local password.
6. Nếu user có LOCAL_PASSWORD và AllowPasswordLogin = true:
   - Tạo OTP/reset token nếu bảng otp_tokens/reset_tokens đã có.
   - Gửi email nếu email service đã có.
   - Nếu email service chưa có, tạo token/log dev có kiểm soát, không expose trong production.
```

Message generic:

```text
Nếu email hợp lệ và tài khoản hỗ trợ đặt lại mật khẩu, hệ thống sẽ gửi hướng dẫn khôi phục.
```

### Production SSO-only

Nếu `LoginMode = ProductionSsoOnly`:

```text
- Không tạo reset token local.
- Return generic message hoặc message hướng dẫn dùng provider định danh tùy UX hiện tại.
```

Không được reset password cho tài khoản SSO-only.

---

## 15. /auth/me rule

Endpoint:

```http
GET /api/auth/me
```

Phải:

```text
- Yêu cầu authenticated.
- Check session active.
- Load user mới nhất từ DB.
- Load role/campus/department.
- Load permissions từ role_permissions.
- Return UserProfile/AuthUserDto + permissions.
```

Không được chỉ tin JWT cũ nếu role/campus vừa bị update.

Response gợi ý:

```json
{
  "user": {
    "userId": 123,
    "email": "staff.hn@fpt.edu.vn",
    "fullName": "Nguyen Van A",
    "roleCode": "STAFF",
    "roleName": "Staff",
    "subRole": "STAFF_L",
    "campusId": 1,
    "campusCode": "HN",
    "campusName": "FPTU Hà Nội",
    "departmentId": null,
    "status": "ACTIVE"
  },
  "permissions": [
    "UC-95.VIEW_ACCOUNT_LIST"
  ]
}
```

Nếu session revoked:

```text
HTTP 401
errorCode = SESSION_REVOKED
```

---

## 16. Database check

Không tự chạy migration/destructive SQL.

Kiểm tra schema thật:

### users

Cần có tương đương:

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
failed_login_count nếu có lockout
locked_until nếu có lockout
created_at
updated_at
```

Visitor phải cho phép:

```text
primary_campus_id = NULL
department_id = NULL
password_hash = NULL
```

### user_auth_providers

Cần có tương đương:

```text
user_auth_provider_id
user_id
provider_type
provider_subject
provider_email
linked_at
last_used_at
```

Unique nên có:

```sql
UNIQUE KEY uq_user_provider (user_id, provider_type),
UNIQUE KEY uq_provider_subject (provider_type, provider_subject)
```

Nếu chưa có, tạo patch idempotent:

```text
database/scripts/patch_auth_core_dual_portal.sql
```

### user_sessions

Cần hỗ trợ:

```text
session_id
user_id
refresh_token_hash nếu có
jwt_id/jti nếu có
created_at
expires_at
revoked_at
revoked_reason
ip_address
user_agent
```

### logs

Nếu có bảng:

```text
login_logs
security_events
audit_logs
```

thì ghi log ở các case:

```text
LOGIN_SUCCESS
LOGIN_FAILED_INVALID_CREDENTIALS
LOGIN_FAILED_WRONG_PORTAL
LOGIN_FAILED_CAMPUS_MISMATCH
LOGIN_FAILED_INTERNAL_ACCOUNT_NOT_FOUND
LOGIN_FAILED_PASSWORD_DISABLED
LOGOUT_SUCCESS
FORGOT_PASSWORD_REQUESTED
```

---

## 17. File cần tạo/cập nhật

Tùy cấu trúc thật, tạo/cập nhật các file tương đương.

### Backend API

```text
backend/PEMS.Api/Controllers/AuthenticationController.cs
backend/PEMS.Api/Extensions/AuthenticationExtensions.cs
backend/PEMS.Api/Middleware/ExceptionHandlingMiddleware.cs
backend/PEMS.Api/Middleware/SessionValidationMiddleware.cs
backend/PEMS.Api/Contracts/ApiResponse.cs nếu cần thêm errorCode
```

### Application

```text
backend/PEMS.Application/Authentication/Commands/LoginViaSso/
backend/PEMS.Application/Authentication/Commands/LoginViaCredentials/
backend/PEMS.Application/Authentication/Commands/Logout/
backend/PEMS.Application/Authentication/Commands/ForgotPassword/
backend/PEMS.Application/Authentication/Commands/RefreshToken/ nếu có
backend/PEMS.Application/Authentication/Queries/GetCurrentUser/
backend/PEMS.Application/Authentication/Queries/GetCurrentUserPermissions/
backend/PEMS.Application/Authentication/Models/AuthResponse.cs
backend/PEMS.Application/Authentication/Models/AuthUserDto.cs
backend/PEMS.Application/Authentication/Models/ExternalIdentityResult.cs
backend/PEMS.Application/Authentication/Options/AuthOptions.cs
backend/PEMS.Application/Authentication/Rules/AuthErrorCodes.cs
backend/PEMS.Application/Authentication/Rules/AuthProviders.cs
backend/PEMS.Application/Authentication/Rules/LoginPortalTypes.cs
backend/PEMS.Application/Authentication/Rules/RoleGroups.cs
backend/PEMS.Application/Common/Interfaces/IAuthPolicyService.cs
backend/PEMS.Application/Common/Interfaces/IExternalIdentityVerifier.cs
backend/PEMS.Application/Common/Interfaces/IUserProvisioningService.cs
backend/PEMS.Application/Common/Interfaces/ISessionService.cs
backend/PEMS.Application/Common/Interfaces/ITokenService.cs
```

### Infrastructure

```text
backend/PEMS.Infrastructure/Identity/AuthPolicyService.cs nếu implementation ở Infrastructure
backend/PEMS.Infrastructure/Identity/GoogleExternalIdentityVerifier.cs
backend/PEMS.Infrastructure/Identity/FeidExternalIdentityVerifier.cs
backend/PEMS.Infrastructure/Identity/UserProvisioningService.cs
backend/PEMS.Infrastructure/Identity/SessionService.cs
backend/PEMS.Infrastructure/Identity/TokenService.cs
backend/PEMS.Infrastructure/Repositories/UserRepository.cs
backend/PEMS.Infrastructure/Repositories/UserAuthProviderRepository.cs nếu có
```

### Config

```text
backend/PEMS.Api/appsettings.json
backend/PEMS.Api/appsettings.Development.json
backend/PEMS.Api/appsettings.Production.json nếu có
```

### Docs

Tạo/cập nhật:

```text
docs/auth/AUTH_CORE_BACKEND_DUAL_PORTAL.md
docs/auth/AUTH_ERROR_CODES.md
docs/architecture/REFACTOR_CHANGELOG.md
```

---

## 18. Pseudo-code LoginViaSsoCommandHandler

Dùng pseudo-code này làm chuẩn logic:

```csharp
public async Task<AuthResponse> Handle(LoginViaSsoCommand request, CancellationToken ct)
{
    var provider = NormalizeProvider(request.Provider);
    var portal = NormalizePortal(request.PortalType);

    _authPolicy.EnsureValidPortal(portal);
    _authPolicy.EnsureExternalProviderAllowed(provider);
    _authPolicy.EnsureCampusSelectedForInternal(portal, request.SelectedCampusId);

    var identity = await _externalIdentityVerifier.VerifyAsync(
        provider,
        request.IdTokenOrCode,
        ct);

    if (!identity.EmailVerified)
        throw new AuthBusinessException(AuthErrorCodes.SsoTokenInvalid, "Email từ nhà cung cấp chưa được xác minh.", 401);

    var email = NormalizeEmail(identity.Email);

    var user = await _userRepository.GetByEmailWithAuthDataAsync(email, ct);

    if (portal == LoginPortalTypes.Visitor)
    {
        if (user is null)
        {
            if (!_authOptions.AutoCreateVisitorOnExternalLogin)
                throw new AuthBusinessException(AuthErrorCodes.InternalAccountNotFound, "Tài khoản chưa tồn tại.", 403);

            user = await _userProvisioningService.CreateVisitorFromExternalIdentityAsync(identity, ct);
        }
        else
        {
            _authPolicy.EnsureAccountCanLogin(user);
            _authPolicy.EnsureUserCanLoginVisitorPortal(user);
            await _authProviderService.EnsureLinkedAsync(user, identity, ct);
        }

        return await _authResultBuilder.BuildAsync(user, portal, selectedCampusId: null, ct);
    }

    if (portal == LoginPortalTypes.Internal)
    {
        if (user is null)
        {
            await _loginLogService.WriteFailedAsync(email, AuthErrorCodes.InternalAccountNotFound, ct);
            throw new AuthBusinessException(
                AuthErrorCodes.InternalAccountNotFound,
                "Tài khoản của bạn chưa được tạo trong hệ thống nội bộ. Vui lòng liên hệ Staff Leader hoặc quản trị viên của cơ sở để được cấp quyền đăng nhập.",
                403);
        }

        _authPolicy.EnsureAccountCanLogin(user);
        _authPolicy.EnsureUserCanLoginInternalPortal(user, request.SelectedCampusId!.Value);
        await _authProviderService.EnsureLinkedAsync(user, identity, ct);

        return await _authResultBuilder.BuildAsync(user, portal, request.SelectedCampusId.Value, ct);
    }

    throw new AuthBusinessException("INVALID_PORTAL", "Cổng đăng nhập không hợp lệ.", 400);
}
```

---

## 19. Pseudo-code LoginViaCredentialsCommandHandler

```csharp
public async Task<AuthResponse> Handle(LoginViaCredentialsCommand request, CancellationToken ct)
{
    var portal = NormalizePortal(request.PortalType);

    _authPolicy.EnsureValidPortal(portal);
    _authPolicy.EnsurePasswordLoginAllowed();
    _authPolicy.EnsureCampusSelectedForInternal(portal, request.SelectedCampusId);

    var email = NormalizeEmail(request.Email);

    var user = await _userRepository.GetByEmailWithAuthDataAsync(email, ct);

    if (user is null)
    {
        await _loginLogService.WriteFailedAsync(email, AuthErrorCodes.InvalidCredentials, ct);
        throw new AuthBusinessException(
            AuthErrorCodes.InvalidCredentials,
            "Email hoặc mật khẩu không đúng.",
            401);
    }

    _authPolicy.EnsureAccountCanLogin(user);

    var passwordOk = await _passwordHasher.VerifyAsync(user, request.Password, ct);
    if (!passwordOk)
    {
        await _loginAttemptService.RecordFailedAttemptAsync(user, ct);
        throw new AuthBusinessException(
            AuthErrorCodes.InvalidCredentials,
            "Email hoặc mật khẩu không đúng.",
            401);
    }

    if (portal == LoginPortalTypes.Visitor)
    {
        _authPolicy.EnsureUserCanLoginVisitorPortal(user);
        return await _authResultBuilder.BuildAsync(user, portal, selectedCampusId: null, ct);
    }

    if (portal == LoginPortalTypes.Internal)
    {
        _authPolicy.EnsureUserCanLoginInternalPortal(user, request.SelectedCampusId!.Value);
        return await _authResultBuilder.BuildAsync(user, portal, request.SelectedCampusId.Value, ct);
    }

    throw new AuthBusinessException("INVALID_PORTAL", "Cổng đăng nhập không hợp lệ.", 400);
}
```

---

## 20. Manual test matrix bắt buộc

Sau khi sửa, chạy ít nhất các case này bằng Swagger/Postman.

| # | Input | Expected |
|---|---|---|
| 1 | Visitor SSO email chưa tồn tại | 200, tạo user VISITOR, campus NULL |
| 2 | Visitor SSO email đã là VISITOR | 200, không tạo trùng |
| 3 | Visitor portal nhưng email là STAFF/HO/Admin | 403 WRONG_PORTAL_INTERNAL_ACCOUNT |
| 4 | Internal SSO thiếu selectedCampusId | 400 CAMPUS_REQUIRED |
| 5 | Internal SSO email chưa tồn tại | 403 INTERNAL_ACCOUNT_NOT_FOUND |
| 6 | Internal SSO email là VISITOR | 403 WRONG_PORTAL_VISITOR_ACCOUNT |
| 7 | Internal SSO email là STAFF đúng campus | 200 |
| 8 | Internal SSO email là STAFF sai campus | 403 CAMPUS_MISMATCH |
| 9 | Credentials dev đúng visitor | 200 |
| 10 | Credentials dev đúng internal + campus đúng | 200 |
| 11 | Credentials dev internal thiếu campus | 400 CAMPUS_REQUIRED |
| 12 | Credentials dev internal sai campus | 403 CAMPUS_MISMATCH |
| 13 | Credentials sai password | 401 INVALID_CREDENTIALS |
| 14 | Credentials production disabled | 403 PASSWORD_LOGIN_DISABLED |
| 15 | Account INACTIVE login | 403 ACCOUNT_INACTIVE |
| 16 | Account LOCKED login | 403 ACCOUNT_LOCKED |
| 17 | Logout rồi gọi /auth/me bằng token cũ | 401 SESSION_REVOKED hoặc UNAUTHORIZED |
| 18 | /auth/me bằng token hợp lệ | 200 user + permissions |
| 19 | Forgot password LOCAL_PASSWORD dev/test | generic success + token/email flow |
| 20 | Forgot password SSO-only account | không reset local password, generic success hoặc FORGOT_PASSWORD_NOT_AVAILABLE theo policy |

---

## 21. Build/test command bắt buộc

Chạy ở root:

```bash
dotnet restore
dotnet build
```

Nếu có test project:

```bash
dotnet test
```

Nếu sửa nhẹ frontend compatibility:

```bash
cd frontend/pems-react
npm install
npm run build
```

Nếu build fail:

```text
- Không giấu lỗi.
- Ghi rõ file lỗi.
- Sửa lỗi compile trước khi báo hoàn thành.
```

---

## 22. Báo cáo sau khi hoàn thành

Tạo/cập nhật:

```text
docs/architecture/REFACTOR_CHANGELOG.md
docs/auth/AUTH_CORE_BACKEND_DUAL_PORTAL.md
docs/auth/AUTH_ERROR_CODES.md
```

Báo cáo theo format:

```markdown
# Báo cáo hoàn thành Core Auth Backend

## 1. Summary

## 2. UC đã hoàn thành
- UC-10 Login via SSO
- UC-11 Login via Credentials
- UC-12 Logout
- UC-13 Forgot Password
- GET /auth/me

## 3. Files changed

## 4. Backend logic implemented

## 5. Config added/updated

## 6. Error codes added

## 7. Database changes / SQL patch

## 8. Manual test results

## 9. Build/test result

## 10. Known limitations
- Google SSO limitation nếu chưa có credential thật.
- FEID limitation nếu chưa có endpoint/credential thật.

## 11. TODO next phase
- Frontend config + error map.
- Account Management Create + UpdateRole.
- FEID full integration.
- Automated tests.
```

---

## 23. Quy tắc tuyệt đối không được vi phạm

```text
[ ] Không auto-create internal user khi login SSO/FEID.
[ ] Không cho Visitor vào internal portal.
[ ] Không cho internal user vào visitor portal.
[ ] Không bỏ qua selectedCampusId ở internal portal.
[ ] Không tự gán campus cho Visitor khi Visitor chỉ login hoặc gửi form.
[ ] Không fake Google/FEID success nếu chưa verify token/code thật.
[ ] Không reset password cho SSO-only account.
[ ] Không trả passwordHash/secret ra API.
[ ] Không viết business logic trong Controller.
[ ] Không gọi DbContext trực tiếp từ Controller.
[ ] Không phá response message cũ của frontend.
[ ] Không dùng 401 cho wrong portal/campus.
[ ] Không tự seed role/campus trong runtime nếu project database-first/manual seed.
[ ] Không xóa code cũ nếu chưa chắc dependency.
```

---

## 24. Gợi ý thứ tự triển khai

Làm theo đúng thứ tự:

```text
PHASE 1 — Quét code auth hiện tại, ghi file inventory ngắn.
PHASE 2 — Thêm AuthOptions + constants: portal/provider/login mode/error codes.
PHASE 3 — Chuẩn hóa exception/error response thêm errorCode.
PHASE 4 — Viết AuthPolicyService.
PHASE 5 — Viết/chuẩn hóa ExternalIdentityVerifier.
PHASE 6 — Viết UserProvisioningService cho Visitor auto-create.
PHASE 7 — Sửa LoginViaSsoCommandHandler.
PHASE 8 — Sửa LoginViaCredentialsCommandHandler.
PHASE 9 — Sửa Logout/session revoke.
PHASE 10 — Sửa ForgotPassword.
PHASE 11 — Sửa /auth/me và permissions.
PHASE 12 — Kiểm tra database mapping/repository.
PHASE 13 — Build backend.
PHASE 14 — Manual test matrix.
PHASE 15 — Cập nhật docs/changelog.
```

---

## 25. Definition of Done

Chỉ được báo hoàn thành khi:

```text
[ ] dotnet build pass.
[ ] Các endpoint auth xuất hiện trong Swagger hoặc route hiện tại.
[ ] 20 manual test case ở mục 20 có kết quả rõ.
[ ] Error response có errorCode và message.
[ ] Không có fake SSO/FEID success.
[ ] Visitor auto-create chỉ xảy ra ở Visitor portal.
[ ] Internal account not found không auto-create.
[ ] Logout revoke session/token cũ.
[ ] /auth/me đọc DB/session mới nhất.
[ ] Có changelog và docs auth.
```

---

# Ghi chú cho code agent

Nếu gặp chỗ chưa chắc:

```text
- Không đoán bừa.
- Không xóa.
- Ghi TODO rõ.
- Ghi vào Known limitations.
- Hỏi lại user nếu là quyết định nghiệp vụ.
```

Ưu tiên làm chạy thật các UC core authentication hơn là refactor đẹp nhưng chưa chạy được.
