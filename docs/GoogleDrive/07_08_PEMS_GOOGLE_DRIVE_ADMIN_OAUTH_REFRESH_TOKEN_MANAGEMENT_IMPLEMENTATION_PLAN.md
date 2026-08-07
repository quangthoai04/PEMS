# PEMS — KẾ HOẠCH TRIỂN KHAI GOOGLE DRIVE OAUTH REFRESH TOKEN QUA ADMIN API MANAGEMENT

> Mục tiêu của tài liệu này: hướng dẫn AI Agent triển khai đầy đủ luồng **ADMIN tự kết nối/kết nối lại Google Drive trên màn Quản lý API**, backend tự nhận `refresh_token`, mã hóa và lưu DB, để toàn bộ hệ thống dùng token mới ngay mà **không cần sửa `appsettings.Development.json`, không cần sửa Railway Variable `GoogleDrive__RefreshToken`, không cần restart/redeploy chỉ để thay token**.
>
> Scope chỉ tập trung Google Drive OAuth credential. Không refactor các upload flow hiện có nếu không cần thiết.

---

## 1. Bối cảnh hiện tại

PEMS hiện dùng Google Drive theo OAuth user-delegated account.

Các cấu hình hiện nằm trong `GoogleDriveOptions` / `GoogleDrive` section:

- `Enabled`
- `AuthMode`
- `ClientId`
- `ClientSecret`
- `RedirectUri`
- `RefreshToken`
- `RootFolderId`
- `AvatarFolderId`
- `DocumentPartnerFolderId`
- `GalleryFolderId`
- `GalleryAreaFolderId`
- `GalleryLocationFolderId`
- `GalleryItemFolderId`
- `GalleryDelegationFolderId`
- `GalleryAudioFolderId`
- `NewsFolderId`
- `MinutesFolderId`
- `VisitRequestDocumentFolderId`
- `VisitRequestPhotoFolderId`

Luồng OAuth hiện tại ở:

`backend/PEMS.Api/Controllers/GoogleDriveOAuthController.cs`

Hiện tại:

```text
GET /api/google-drive/oauth/connect
    -> redirect Google consent
    -> GET /api/google-drive/oauth/callback?code=...
    -> backend exchange code
    -> render refresh_token ra HTML
    -> developer copy token
    -> paste vào appsettings.Development.json
    -> production còn phải sửa Railway GoogleDrive__RefreshToken
    -> restart/redeploy
```

Controller hiện là DEV-only và `[AllowAnonymous]` toàn controller.

Màn ADMIN API Management hiện coi Google Drive là `ENVIRONMENT`, read-only. Backend `ApiIntegrationMapper` chỉ cho 4 purpose DB-managed:

- `BUSINESS_CARD_OCR`
- `NEWS_TRANSLATION`
- `FACE_DETECTION`
- `EMAIL_DELIVERY`

Vì vậy card Google Drive hiện có trạng thái kiểu:

```text
READ-ONLY (ENV)
Credential: Chưa có
```

và không có nút reconnect/test dành cho ADMIN.

---

# 2. Mục tiêu cuối cùng

Sau khi triển khai:

```text
ADMIN
  -> Quản lý API
  -> Google Drive Storage
  -> [Kết nối Google Drive] hoặc [Kết nối lại Google Drive]
  -> đăng nhập tài khoản Google dùng chung
  -> Allow
  -> Google callback về backend
  -> backend nhận authorization code
  -> exchange code -> refresh_token
  -> mã hóa refresh_token
  -> lưu vào Production/Local DB
  -> redirect về trang Quản lý API
  -> GoogleDriveStorageService đọc token mới từ DB
  -> toàn bộ upload/download Drive dùng token mới ngay
```

Không còn thao tác thủ công:

```text
KHÔNG copy refresh_token
KHÔNG sửa appsettings.Development.json
KHÔNG sửa Railway GoogleDrive__RefreshToken
KHÔNG commit token
KHÔNG restart/redeploy chỉ để đổi token
```

---

# 3. Quyết định kiến trúc bắt buộc

## 3.1 Chỉ chuyển RefreshToken sang DB

Không chuyển toàn bộ Google Drive config sang DB trong scope này.

Giữ các cấu hình hạ tầng ổn định trong environment/appsettings:

```text
GoogleDrive__ClientId
GoogleDrive__ClientSecret
GoogleDrive__RedirectUri
GoogleDrive__RootFolderId
GoogleDrive__AvatarFolderId
GoogleDrive__DocumentPartnerFolderId
GoogleDrive__GalleryFolderId
GoogleDrive__GalleryAreaFolderId
GoogleDrive__GalleryLocationFolderId
GoogleDrive__GalleryItemFolderId
GoogleDrive__GalleryDelegationFolderId
GoogleDrive__GalleryAudioFolderId
GoogleDrive__NewsFolderId
GoogleDrive__MinutesFolderId
GoogleDrive__VisitRequestDocumentFolderId
GoogleDrive__VisitRequestPhotoFolderId
```

Chỉ credential runtime thay đổi thường xuyên:

```text
RefreshToken -> encrypted DB
```

Lý do: folder resolver hiện đọc synchronous từ `GoogleDriveOptions`; chuyển folder IDs sang DB sẽ mở rộng scope sang Gallery/Visit/Report/FileUpload không cần thiết.

---

## 3.2 Không tạo bảng mới nếu schema hiện tại đủ dùng

Ưu tiên tái sử dụng bảng:

`api_configurations`

Entity hiện có:

`backend/PEMS.Domain/Entities/ApiIntegrations/ApiConfiguration.cs`

Có sẵn các field phù hợp:

```text
oauth_client_id
oauth_client_secret_encrypted
oauth_token_url
oauth_scope
credentials_json_encrypted
secret_ref
last_test_status
last_tested_at
last_test_message
updated_at
updated_by
```

Refresh token phải được lưu encrypted trong:

```text
credentials_json_encrypted
```

Không thêm column mới nếu không thật sự cần.

Credential envelope đề xuất trước khi encrypt:

```json
{
  "refreshToken": "..."
}
```

Không lưu plaintext.

---

## 3.3 Reuse ISecretProtector

PEMS đã có:

```text
ISecretProtector
AesGcmSecretProtector
```

Dùng chính service này để encrypt/decrypt RefreshToken.

Không tự viết crypto mới.

Production nên cấu hình cố định:

```text
Security__SecretProtectionKey=<base64 của đúng 32 bytes>
```

Không nên để credential DB phụ thuộc lâu dài vào việc derive key từ JWT secret, vì rotate JWT có thể làm ciphertext cũ không decrypt được.

---

## 3.4 DB token ưu tiên, ENV token chỉ fallback migration

Trong giai đoạn rollout:

```text
1. Nếu DB có refresh token hợp lệ -> dùng DB
2. Nếu DB chưa có -> fallback GoogleDriveOptions.RefreshToken
3. Nếu cả DB và ENV đều không có -> GOOGLE_DRIVE_CONFIG_MISSING
```

Sau khi production ADMIN reconnect thành công và các test pass:

```text
xóa Railway GoogleDrive__RefreshToken
xóa RefreshToken khỏi appsettings.Development.json nếu còn
```

Nhưng chỉ xóa sau khi DB token được chứng minh hoạt động.

---

# 4. Không được làm

AI Agent KHÔNG được:

1. Ghi trực tiếp vào `appsettings.Development.json` từ backend.
2. Ghi `refresh_token` vào Railway Variable qua code.
3. Hiển thị `refresh_token` trên frontend hoặc callback HTML.
4. Log `refresh_token`, `ClientSecret`, authorization code hoặc token response đầy đủ.
5. Trả credential raw trong DTO/API response.
6. Tạo bảng mới chỉ để lưu một token nếu `api_configurations` đã đáp ứng.
7. Thay đổi `IFileUploadService`, Gallery, Visit Photo, Visit Document, Report flow nếu không cần.
8. Hard-code folder IDs.
9. Cho HO hoặc role khác reconnect Drive.
10. Bỏ OAuth `state` protection.
11. Cache RefreshToken vĩnh viễn từ startup options khiến reconnect xong vẫn phải restart backend.
12. Dùng `appsettings` làm runtime state store.

---

# 5. File phải đọc trước khi code

AI Agent phải đọc lại code thật trên branch hiện tại trước khi sửa, tối thiểu:

### Google Drive

```text
backend/PEMS.Api/Controllers/GoogleDriveOAuthController.cs
backend/PEMS.Application/Common/Storage/GoogleDriveOptions.cs
backend/PEMS.Application/Common/Storage/GoogleDriveErrorCodes.cs
backend/PEMS.Application/Common/Interfaces/IGoogleDriveStorageService.cs
backend/PEMS.Infrastructure/FileStorage/GoogleDrive/GoogleDriveStorageService.cs
backend/PEMS.Infrastructure/FileStorage/GoogleDrive/GoogleDriveFolderResolver.cs
backend/PEMS.Infrastructure/DependencyInjection.cs
```

### API Management

```text
backend/PEMS.Api/Controllers/ApiIntegrationsController.cs
backend/PEMS.Application/ApiIntegrations/Common/ApiIntegrationConstants.cs
backend/PEMS.Application/ApiIntegrations/Common/ApiIntegrationDtos.cs
backend/PEMS.Application/ApiIntegrations/Common/ApiIntegrationMapper.cs
backend/PEMS.Application/ApiIntegrations/Common/ApiIntegrationAccess.cs
backend/PEMS.Application/ApiIntegrations/Queries/GetApiIntegrations/GetApiIntegrationsQueryHandler.cs
backend/PEMS.Application/ApiIntegrations/Commands/TestApiIntegration/TestApiIntegrationCommandHandler.cs
backend/PEMS.Domain/Entities/ApiIntegrations/ApiConfiguration.cs
```

### Secret protection

```text
backend/PEMS.Application/Common/Interfaces/ISecretProtector.cs
backend/PEMS.Infrastructure/Security/AesGcmSecretProtector.cs
backend/PEMS.Api/Extensions/SecretConfigurationValidator.cs
```

### Frontend

```text
frontend/pems-react/src/pages/dashboard/apis/ApiManagement.tsx
frontend/pems-react/src/features/api-management/api/apiManagementApi.ts
frontend/pems-react/src/features/api-management/types/apiManagement.types.ts
frontend/pems-react/src/shared/api/endpoints.ts
```

Nếu tên/path đã thay đổi trên branch hiện tại, tìm equivalent hiện tại và dùng code thật làm chuẩn.

---

# 6. Backend — Google Drive Integration identity

## 6.1 Thêm constants

File:

`backend/PEMS.Application/ApiIntegrations/Common/ApiIntegrationConstants.cs`

Thêm Google Drive integration constants, ví dụ:

```csharp
public static class GoogleDriveIntegrationConstants
{
    public const string ApiCode = "GOOGLE_DRIVE_STORAGE";
    public const string ProviderName = "GOOGLE_DRIVE";
    public const string Purpose = "GOOGLE_DRIVE_STORAGE";
}
```

Lưu ý:

- Trước khi thêm, kiểm tra seed hiện tại xem Google Drive card đang dùng `api_code`, `provider_name`, `purpose` nào.
- Nếu DB đã có row Google Drive thì phải reuse đúng row đó.
- Không tạo row duplicate chỉ vì thêm constants.
- Nếu seed hiện tại dùng tên khác, constants phải khớp dữ liệu canonical hiện có.

---

# 7. Backend — Credential Resolver mới

## 7.1 Interface

Tạo:

`backend/PEMS.Application/Common/Interfaces/IGoogleDriveCredentialResolver.cs`

Contract nên tối giản, ví dụ:

```csharp
public interface IGoogleDriveCredentialResolver
{
    Task<string?> ResolveRefreshTokenAsync(CancellationToken cancellationToken);
}
```

Không trả ClientSecret nếu không cần. ClientId/ClientSecret vẫn lấy từ `GoogleDriveOptions`.

---

## 7.2 Implementation

Tạo:

`backend/PEMS.Infrastructure/FileStorage/GoogleDrive/GoogleDriveCredentialResolver.cs`

Dependency:

```text
IApplicationDbContext
ISecretProtector
IOptions<GoogleDriveOptions>
```

Logic:

```text
ResolveRefreshTokenAsync
    -> query api_configurations row Google Drive, DeletedAt == null
    -> nếu CredentialsJsonEncrypted có dữ liệu:
         decrypt bằng ISecretProtector
         parse JSON
         lấy refreshToken
         nếu non-empty -> return
    -> fallback _options.RefreshToken
    -> nếu không có -> return null hoặc throw ở caller theo convention hiện tại
```

Yêu cầu:

- `AsNoTracking()` cho read path.
- Không log token.
- Nếu ciphertext corrupt/decrypt fail: log sanitized warning + classify rõ, không fallback âm thầm nếu điều đó có thể che credential DB bị hỏng. Quyết định cụ thể phải nhất quán với error policy hiện tại.
- Prefer fail-closed cho DB credential corrupt.
- ENV fallback chủ yếu dành cho trường hợp DB credential chưa từng được cấu hình.

---

# 8. Backend — GoogleDriveStorageService dùng token động

File:

`backend/PEMS.Infrastructure/FileStorage/GoogleDrive/GoogleDriveStorageService.cs`

Hiện `GetAccessTokenAsync()` dùng `_options.RefreshToken`.

Sửa dependency để inject:

```text
IGoogleDriveCredentialResolver
```

Luồng mới:

```text
GetAccessTokenAsync()
    -> validate ClientId
    -> validate ClientSecret
    -> refreshToken = await credentialResolver.ResolveRefreshTokenAsync(ct)
    -> nếu thiếu -> GOOGLE_DRIVE_CONFIG_MISSING / code hiện tại phù hợp
    -> POST https://oauth2.googleapis.com/token
    -> access_token
```

Không thay đổi contract public của:

```text
IGoogleDriveStorageService
UploadFileAsync
DownloadAsync
DownloadRangeAsync
DeleteAsync
EnsureChildFolderAsync
...
```

Mục tiêu là mọi flow Google Drive hiện tại tự dùng token DB mới mà không sửa từng feature.

### Quan trọng

Không cache RefreshToken vĩnh viễn trong singleton/startup.

Reconnect xong phải có hiệu lực cho request Drive tiếp theo mà không restart process.

Access token có thể cache ngắn nếu code hiện tại đã có pattern an toàn, nhưng khi refresh fails vì invalid_grant hoặc sau reconnect phải có cách lấy lại refresh token mới. Đừng thêm cache phức tạp nếu hiện tại chưa có.

---

# 9. Backend — OAuth flow mới

## 9.1 Không tái sử dụng nguyên trạng flow DEV cũ

Flow cũ không đủ an toàn cho production vì:

- controller `[AllowAnonymous]` toàn bộ;
- `connect` không check ADMIN;
- không có OAuth `state`;
- callback render `refresh_token` ra HTML;
- không lưu DB;
- DEV-only.

Phải thay flow này.

---

# 10. Endpoint start OAuth cho ADMIN

Ưu tiên đặt action start trong API Management surface để rõ quyền quản trị:

```text
POST /api/api-integrations/google-drive/oauth/start
```

Controller:

`backend/PEMS.Api/Controllers/ApiIntegrationsController.cs`

Hoặc tách controller riêng nếu codebase hiện tại sạch hơn, nhưng route/behavior phải giữ rõ ADMIN-only.

### Authorization

Endpoint phải:

```text
[Authorize]
ApiIntegrationAccess.EnsureManage(currentUser)
```

Chỉ ADMIN được start reconnect.

### Response

Không redirect trực tiếp từ XHR nếu frontend đang dùng Axios. Trả:

```json
{
  "authorizationUrl": "https://accounts.google.com/o/oauth2/v2/auth?..."
}
```

Frontend sẽ:

```ts
window.location.assign(authorizationUrl)
```

---

# 11. OAuth authorization URL

Phải có ít nhất:

```text
client_id=<GoogleDrive ClientId>
redirect_uri=<GoogleDrive RedirectUri>
response_type=code
scope=https://www.googleapis.com/auth/drive
access_type=offline
prompt=consent
state=<protected-state>
```

Giữ `prompt=consent` để Google có khả năng trả refresh token mới khi reconnect.

Không đưa ClientSecret lên URL.

---

# 12. OAuth state protection

Bắt buộc thêm `state`.

Có thể triển khai service nhỏ:

```text
IGoogleDriveOAuthStateService
GoogleDriveOAuthStateService
```

hoặc equivalent phù hợp architecture hiện tại.

Ưu tiên reuse `ISecretProtector` thay vì tự tạo crypto mới.

Payload tối thiểu:

```json
{
  "adminUserId": 123,
  "nonce": "random-value",
  "expiresAtUtc": "..."
}
```

State phải:

- không thể sửa nội dung mà callback chấp nhận;
- hết hạn ngắn, đề xuất 5 phút;
- callback validate trước khi exchange code;
- không tin `userId` plain từ query.

Authorization code của Google là one-time; vì vậy không cần mở rộng schema chỉ để lưu nonce nếu implementation hiện tại không có distributed cache phù hợp.

Nếu codebase đã có ASP.NET DataProtection ổn định với shared key store thì có thể reuse; nếu không, dùng `ISecretProtector` hiện có để tránh tạo dependency vận hành mới.

---

# 13. Callback production

Giữ route hiện có nếu có thể để giảm thay đổi Google Cloud Console:

```text
GET /api/google-drive/oauth/callback
```

File:

`backend/PEMS.Api/Controllers/GoogleDriveOAuthController.cs`

### Callback có thể AllowAnonymous

Google redirect không có JWT Authorization header, nên callback có thể `[AllowAnonymous]`, NHƯNG phải bắt buộc verify `state` đã được tạo bởi ADMIN start flow.

Không để toàn controller `[AllowAnonymous]` rồi expose connect endpoint cũ.

### Input

```text
code
error
state
```

### Validation order

```text
1. state phải có
2. decrypt/verify state
3. state chưa hết hạn
4. code phải có nếu không có error
5. validate ClientId/ClientSecret/RedirectUri config
6. exchange authorization code
7. parse refresh_token
8. save encrypted credential
9. audit
10. redirect frontend
```

Không exchange code trước khi validate state.

---

# 14. Token exchange

Token endpoint:

```text
https://oauth2.googleapis.com/token
```

POST form:

```text
code
grant_type=authorization_code
client_id
client_secret
redirect_uri
```

### Không log

Không log:

```text
code
client_secret
refresh_token
access_token
full response body
```

Nếu Google trả lỗi, chỉ log/return error code + sanitized description cần thiết.

---

# 15. Khi Google không trả refresh_token

Không được overwrite token DB hiện tại bằng null/rỗng.

Nếu callback response không có `refresh_token`:

```text
- giữ credential cũ
- đánh dấu reconnect thất bại
- redirect về ADMIN page với safe status
```

Ví dụ query chỉ chứa mã kết quả không nhạy cảm:

```text
/dashboard/apis?googleDriveOAuth=failed&reason=no_refresh_token
```

Frontend map reason sang message tiếng Việt.

Không đưa raw Google error description dài hoặc token-related detail vào URL.

---

# 16. Lưu refresh token vào DB

Sau khi có refresh token mới:

```text
load existing Google Drive api_configurations row
```

Nếu row tồn tại:

```text
CredentialsJsonEncrypted = Protect({ refreshToken })
UpdatedAt = VietnamNow
UpdatedBy = adminUserId từ protected state
LastTestStatus = null
LastTestedAt = null
LastTestMessage = null
```

Lý do reset test: credential đã thay đổi, phải test lại.

Nếu row chưa tồn tại:

- tạo đúng well-known Google Drive row theo canonical constants/seed;
- không tạo duplicate;
- status/capability phải nhất quán với API Management hiện tại.

### Transaction/order

Refresh token chỉ được coi là connected sau khi DB save thành công.

Nếu DB save fail:

- không có cách revoke chính xác token mới bắt buộc trong scope này;
- return safe failure;
- không log token.

---

# 17. Audit log

Phải ghi audit khi:

```text
CONNECT_GOOGLE_DRIVE
RECONNECT_GOOGLE_DRIVE
DISCONNECT_GOOGLE_DRIVE (nếu implement disconnect)
```

Audit chứa:

```text
ActorUserId
Action
EntityType = ApiConfiguration
EntityId
CreatedAt
```

Không chứa credential.

---

# 18. Google Drive API Management capability

File:

`backend/PEMS.Application/ApiIntegrations/Common/ApiIntegrationMapper.cs`

Google Drive không còn là pure `ENVIRONMENT`.

Nó là hybrid:

```text
ClientId/ClientSecret/RedirectUri/FolderIds -> ENVIRONMENT
RefreshToken -> DATABASE
```

Thêm/đổi management source để frontend phản ánh đúng, đề xuất:

```text
DATABASE
ENVIRONMENT
HYBRID
```

Google Drive:

```text
ManagementSource = HYBRID
```

### Capability đề xuất

Mở rộng DTO:

```text
CanConnectOAuth
CanDisconnectOAuth
```

Google Drive cho ADMIN:

```text
CanEdit = false
CanConnectOAuth = true
CanDisconnectOAuth = true hoặc false tùy scope
CanTest = true
CanToggleStatus = false (nếu không có business requirement bật/tắt từ DB)
CanConfigureQuota = false
```

Không ép Google Drive vào form edit generic của OCR.

HO:

```text
CanConnectOAuth = false
CanDisconnectOAuth = false
CanTest = false nếu policy test là manage-only
```

Giữ policy hiện tại:

```text
ADMIN = manage
HO = read-only
```

---

# 19. DTO phải không expose credential

File:

`backend/PEMS.Application/ApiIntegrations/Common/ApiIntegrationDtos.cs`

Có thể thêm các field non-secret:

```text
ManagementSource = HYBRID
CanConnectOAuth
CanDisconnectOAuth
HasCredential
CredentialStatus
```

Ví dụ:

```text
CredentialStatus = CONNECTED | NOT_CONFIGURED | EXPIRED | ERROR
```

Chỉ thêm nếu cần UI rõ hơn.

Tuyệt đối không thêm:

```text
RefreshToken
ClientSecret
EncryptedCredential
```

---

# 20. HasCredential cho Google Drive

`ApiIntegrationMapper` hiện xác định credential dựa vào các encrypted fields.

Với Google Drive:

```text
HasCredential = CredentialsJsonEncrypted có encrypted refresh token
                hoặc đang fallback ENV trong migration
```

Nếu muốn UI phân biệt source:

```text
CredentialSource = DATABASE | ENVIRONMENT_FALLBACK | NONE
```

nhưng không bắt buộc nếu sẽ sớm loại ENV fallback.

Không trả giá trị token.

---

# 21. Test connection Google Drive

File:

`backend/PEMS.Application/ApiIntegrations/Commands/TestApiIntegration/TestApiIntegrationCommandHandler.cs`

Hiện handler chỉ support OCR/Translation/Vision/Resend.

Thêm Google Drive purpose.

### Test phải kiểm tra thật

Không chỉ check token tồn tại.

Luồng:

```text
Resolve refresh token
    -> mint access token
    -> gọi Drive API bằng credential hiện tại
    -> verify ít nhất RootFolderId có thể truy cập
```

Không cần upload test file.

Có thể reuse method/service hiện có nếu `IGoogleDriveStorageService` hỗ trợ kiểm tra folder. Nếu thiếu, thêm method nhỏ đúng abstraction hoặc dùng một query an toàn trong infrastructure.

### Error mapping

Reuse `GoogleDriveErrorCodes` hiện có:

```text
GOOGLE_DRIVE_CONFIG_MISSING
GOOGLE_DRIVE_TOKEN_EXPIRED
GOOGLE_DRIVE_AUTH_FAILED
GOOGLE_DRIVE_NOT_CONNECTED
GOOGLE_DRIVE_FOLDER_NOT_FOUND_OR_NO_PERMISSION
GOOGLE_DRIVE_UNAVAILABLE
```

Không đổi mọi lỗi thành "Vui lòng thử lại".

`invalid_grant` phải map thành:

```text
GOOGLE_DRIVE_TOKEN_EXPIRED
```

UI từ đó hiện nút reconnect.

---

# 22. Runtime behavior khi token hết hạn

Khi GoogleDriveStorageService exchange refresh token và nhận `invalid_grant`:

```text
throw BusinessRuleException / exception convention hiện tại
ErrorCode = GOOGLE_DRIVE_TOKEN_EXPIRED
```

Không tự xóa encrypted token khỏi DB trong upload request.

Lý do:

- giữ evidence credential đã từng được cấu hình;
- ADMIN reconnect sẽ overwrite credential;
- tránh write side-effect DB từ mọi upload/download failure path.

API Management có thể dựa vào `last_test_status` hoặc test mới để hiển thị expired.

---

# 23. SecretConfigurationValidator

File:

`backend/PEMS.Api/Extensions/SecretConfigurationValidator.cs`

Production hiện yêu cầu khi Drive enabled và OAuthUser:

```text
GoogleDrive:ClientSecret
GoogleDrive:RefreshToken
```

Sau migration phải đổi.

Production vẫn require:

```text
GoogleDrive:ClientId (nếu validator hiện check ở chỗ khác thì giữ)
GoogleDrive:ClientSecret
GoogleDrive:RedirectUri
```

Không còn bắt buộc:

```text
GoogleDrive:RefreshToken
```

vì token runtime có thể nằm trong DB.

### Lưu ý startup

Startup validator không nên query DB chỉ để chứng minh refresh token tồn tại, trừ khi architecture hiện tại đã có pattern đó.

Thiếu DB token nên được surface khi Drive được sử dụng / qua API Management status, không làm toàn backend không start nếu mục tiêu của ADMIN UI là cho phép reconnect sau startup.

---

# 24. Dependency Injection

Đăng ký:

```text
IGoogleDriveCredentialResolver -> GoogleDriveCredentialResolver
IGoogleDriveOAuthStateService -> GoogleDriveOAuthStateService (nếu tạo service)
```

Lifetime phải phù hợp với dependency DbContext:

```text
Scoped
```

Không inject scoped service vào singleton.

Kiểm tra lifetime hiện tại của `GoogleDriveStorageService` trước khi đổi. Nếu service đang singleton mà resolver cần DbContext scoped, phải điều chỉnh registration hợp lý, không dùng service locator.

---

# 25. Frontend — API types

File:

`frontend/pems-react/src/features/api-management/types/apiManagement.types.ts`

Update:

```ts
managementSource: 'DATABASE' | 'ENVIRONMENT' | 'HYBRID';
canConnectOAuth: boolean;
canDisconnectOAuth: boolean;
```

Nếu thêm:

```ts
credentialStatus?: 'CONNECTED' | 'NOT_CONFIGURED' | 'EXPIRED' | 'ERROR';
```

thì mirror backend DTO đúng camelCase.

Không có field token.

---

# 26. Frontend — API client

File:

`frontend/pems-react/src/features/api-management/api/apiManagementApi.ts`

Thêm:

```ts
async startGoogleDriveOAuth(): Promise<{ authorizationUrl: string }>
```

Endpoint:

```text
POST /api/api-integrations/google-drive/oauth/start
```

Optional:

```ts
async disconnectGoogleDrive(): Promise<ApiIntegration>
```

Nếu test connection dùng endpoint generic hiện có thì không cần method riêng:

```text
POST /api/api-integrations/{apiConfigId}/test
```

---

# 27. Frontend — endpoints.ts

Thêm canonical endpoint, ví dụ:

```ts
googleDriveOAuthStart: '/api-integrations/google-drive/oauth/start'
```

Giữ naming convention hiện tại.

Không hard-code route rải rác trong component.

---

# 28. Frontend — Google Drive card

File:

`frontend/pems-react/src/pages/dashboard/apis/ApiManagement.tsx`

Card Google Drive phải khác form edit generic.

### Trạng thái chưa kết nối

```text
Google Drive Storage
HYBRID
ACTIVE

Credential
Chưa kết nối

[Kết nối Google Drive]
[Test kết nối] (disabled nếu chưa có credential)
```

### Đã kết nối

```text
Google Drive Storage
HYBRID
ACTIVE

Credential
Đã cấu hình

[Test kết nối]
[Kết nối lại Google Drive]
```

### Token expired

```text
Google Drive Storage
Kết nối đã hết hạn

Tài khoản Google Drive cần được cấp quyền lại.

[Kết nối lại Google Drive]
```

Không có input refresh token.

Không có button `Chỉnh sửa` mở OCR form.

---

# 29. Frontend — start OAuth

Handler:

```ts
const reconnectGoogleDrive = async () => {
  const { authorizationUrl } = await apiManagementApi.startGoogleDriveOAuth();
  window.location.assign(authorizationUrl);
};
```

Có loading state để tránh double-click.

Nếu API fail, dùng toast convention hiện tại.

---

# 30. Frontend — xử lý callback redirect

Sau callback backend redirect về ví dụ:

```text
/dashboard/apis?googleDriveOAuth=success
```

Frontend khi mount:

```text
- đọc query param
- nếu success -> toast "Đã kết nối Google Drive thành công."
- reload integrations
- clear query param bằng history/navigation replace
```

Failure:

```text
/dashboard/apis?googleDriveOAuth=failed&reason=...
```

Map các reason safe:

```text
access_denied
invalid_state
state_expired
no_refresh_token
token_exchange_failed
save_failed
```

Không đưa raw token exchange response vào URL.

---

# 31. Redirect URI local/production

Giữ callback path nếu có thể:

```text
/api/google-drive/oauth/callback
```

Local:

```text
http://localhost:5265/api/google-drive/oauth/callback
```

Production:

```text
https://<RAILWAY_BACKEND_DOMAIN>/api/google-drive/oauth/callback
```

Phải đăng ký chính xác Authorized redirect URIs trong Google Cloud OAuth Client.

Railway:

```text
GoogleDrive__RedirectUri=https://<RAILWAY_BACKEND_DOMAIN>/api/google-drive/oauth/callback
```

Không dùng localhost redirect URI trên production.

---

# 32. Frontend return URL

Không hard-code localhost trong backend callback.

Dùng config đã có hoặc thêm non-secret config phù hợp, ví dụ:

```text
App:FrontendBaseUrl
```

Local:

```text
http://localhost:5173
```

Production:

```text
https://pems-fpt.site
```

Callback build:

```text
{FrontendBaseUrl}/dashboard/apis?googleDriveOAuth=success
```

Nếu project đã có canonical frontend URL config thì reuse, không tạo key duplicate.

---

# 33. Disconnect — optional nhưng nên cân nhắc

Nếu implement:

```text
POST /api/api-integrations/google-drive/oauth/disconnect
```

ADMIN-only.

Behavior tối thiểu:

```text
clear CredentialsJsonEncrypted
reset LastTest*
audit DISCONNECT_GOOGLE_DRIVE
```

Không bắt buộc gọi Google revoke endpoint trong scope đầu tiên nếu chưa có requirement.

Nếu không implement disconnect, chỉ cần reconnect.

Không mở rộng scope nếu task chỉ yêu cầu refresh token replacement.

---

# 34. Database / seed

## Ưu tiên không thay schema

Kiểm tra canonical SQL:

- `api_configurations` đã có `credentials_json_encrypted` đủ size chưa;
- Google Drive row đã seed chưa;
- purpose/api_code hiện tại là gì.

Nếu row Google Drive đã có như card hiện tại thì chỉ update seed/capability metadata nếu cần.

Không tạo migration/table mới chỉ để lưu refresh token.

Nếu canonical SQL có hash pin test thì re-pin chỉ khi canonical SQL thực sự đổi.

---

# 35. Migration behavior local và production

## Local DB riêng mỗi máy

```text
Máy A -> DB A -> RefreshToken A
Máy B -> DB B -> RefreshToken B
```

Mỗi máy chỉ cần ADMIN reconnect cho DB local của chính máy đó.

## Shared dev DB

Nếu nhiều backend cùng dùng một DB dev:

```text
ADMIN reconnect 1 lần -> shared DB token mới -> tất cả backend dùng chung
```

## Production

```text
Railway API instance(s)
      -> Production MySQL
      -> 1 encrypted refresh token hiện hành
```

ADMIN production reconnect 1 lần -> toàn bộ production dùng token mới.

---

# 36. Railway rollout

## Phase 1 — deploy backward compatible

Giữ:

```text
GoogleDrive__RefreshToken=<token cũ>
```

Resolver:

```text
DB first -> ENV fallback
```

Deploy code mới.

## Phase 2 — ADMIN connect production

ADMIN vào:

```text
Dashboard -> Quản lý API -> Google Drive -> Kết nối lại Google Drive
```

Sau success:

```text
Production DB có encrypted refresh token mới
```

## Phase 3 — verify

Test tối thiểu:

```text
Google Drive Test Connection
Avatar upload
Gallery image upload
Gallery video/range read nếu phù hợp
Visit photo upload
Visit document upload
Report archive
File preview/download Drive
```

## Phase 4 — remove old secret source

Sau khi tất cả pass:

```text
xóa Railway GoogleDrive__RefreshToken
```

Không cần xóa các variable folder/client config khác.

---

# 37. Google OAuth 7-day expiry

Implementation ADMIN reconnect giúp loại bỏ thao tác copy/paste/redeploy.

Nhưng nếu OAuth Consent Screen của Google Cloud còn ở trạng thái `Testing`, refresh token có thể tiếp tục bị hết hiệu lực theo policy của Google test app.

Đây là vấn đề vận hành bên Google Cloud, không phải bug của DB token flow.

Sau khi feature reconnect hoàn tất, cần kiểm tra OAuth consent configuration và chuyển sang mode production/audience phù hợp nếu dự án đủ điều kiện.

Không hard-code assumption rằng refresh token "vĩnh viễn".

Dù production mode, token vẫn có thể bị revoke bởi user/security policy, vì vậy nút reconnect vẫn cần tồn tại.

---

# 38. Error handling

Phải phân biệt ít nhất:

```text
GOOGLE_DRIVE_CONFIG_MISSING
GOOGLE_DRIVE_TOKEN_EXPIRED
GOOGLE_DRIVE_AUTH_FAILED
GOOGLE_DRIVE_NOT_CONNECTED
GOOGLE_DRIVE_FOLDER_NOT_FOUND_OR_NO_PERMISSION
GOOGLE_DRIVE_UNAVAILABLE
```

OAuth-specific safe errors có thể bổ sung:

```text
GOOGLE_DRIVE_OAUTH_INVALID_STATE
GOOGLE_DRIVE_OAUTH_STATE_EXPIRED
GOOGLE_DRIVE_OAUTH_ACCESS_DENIED
GOOGLE_DRIVE_OAUTH_NO_REFRESH_TOKEN
GOOGLE_DRIVE_OAUTH_TOKEN_EXCHANGE_FAILED
GOOGLE_DRIVE_OAUTH_SAVE_FAILED
```

Reuse constants class hiện có nếu phù hợp, không rải string literal khắp code.

---

# 39. Security checklist

Bắt buộc pass:

- [ ] OAuth start chỉ ADMIN.
- [ ] HO không reconnect được.
- [ ] Callback verify protected state.
- [ ] State có expiry ngắn.
- [ ] ClientSecret không gửi frontend.
- [ ] RefreshToken không gửi frontend.
- [ ] AccessToken không gửi frontend.
- [ ] Token response không log raw.
- [ ] DB chỉ lưu ciphertext.
- [ ] API DTO chỉ trả `hasCredential`/status.
- [ ] Audit không chứa secret.
- [ ] Query string redirect frontend không chứa secret.
- [ ] `Security__SecretProtectionKey` stable trên production.
- [ ] Không commit token vào repo.
- [ ] Không ghi runtime secret vào appsettings file.

---

# 40. Unit tests backend

Thêm test ít nhất cho resolver:

1. DB có encrypted refresh token -> trả DB token.
2. DB chưa có credential -> fallback ENV token.
3. DB không có + ENV không có -> missing.
4. DB encrypted payload corrupt -> fail-closed/safe error.
5. Token không bao giờ xuất hiện trong exception message/log assertion nếu test harness hỗ trợ.

OAuth state:

6. Valid state -> parse success.
7. Modified state -> reject.
8. Expired state -> reject.

OAuth callback:

9. Google `error=access_denied` -> safe failure.
10. Missing state -> reject.
11. Invalid state -> reject before token exchange.
12. Token exchange success + refresh_token -> encrypted DB save.
13. Token exchange success nhưng không refresh_token -> giữ token cũ.
14. DB save fail -> không expose token.

GoogleDriveStorageService:

15. Uses DB token instead of stale `_options.RefreshToken`.
16. DB token changed -> next token mint uses new DB value without restart.
17. `invalid_grant` -> `GOOGLE_DRIVE_TOKEN_EXPIRED`.

---

# 41. Integration/API tests

Thêm API-level tests:

1. Anonymous start OAuth -> 401/403 theo auth convention.
2. HO start OAuth -> 403.
3. ADMIN start OAuth -> 200 + authorizationUrl.
4. authorizationUrl có `state`, `access_type=offline`, `prompt=consent`.
5. authorizationUrl không chứa ClientSecret.
6. callback valid state -> DB credential encrypted.
7. raw refresh token không xuất hiện trong response.
8. callback invalid state -> không update DB.
9. reconnect -> overwrite credential cũ.
10. reconnect -> `LastTestStatus` reset.
11. Google Drive generic test endpoint support Drive purpose.
12. after reconnect, fake/stub Drive runtime resolves token mới.

Không gọi Google thật trong automated tests. Dùng fake HTTP client/provider.

---

# 42. Frontend tests

1. Drive card ENV cũ không còn render `READ-ONLY (ENV)` sau hybrid migration.
2. ADMIN thấy `Kết nối Google Drive` khi chưa credential.
3. ADMIN thấy `Kết nối lại Google Drive` khi đã credential.
4. Không có input RefreshToken.
5. Click connect gọi start endpoint.
6. Success query param -> success toast + reload list.
7. Failure query param -> safe error toast.
8. `GOOGLE_DRIVE_TOKEN_EXPIRED` -> hiện reconnect CTA.
9. HO/read-only không có connect button.
10. Existing OCR/Translation/Vision/Resend forms không regression.

---

# 43. Regression gates bắt buộc

Sau code phải chạy các gate phù hợp codebase hiện tại:

```text
backend build
backend unit tests
architecture tests nếu có
integration tests liên quan API integrations / Drive
frontend typecheck
frontend lint
frontend unit tests
frontend build
```

Đặc biệt smoke test các Google Drive consumers hiện có:

```text
Avatar
Gallery
News file
Visit Photo
Visit Document
Reports
Email attachment/file preview nếu đọc Drive
```

Không coi compile xanh là đủ.

---

# 44. File dự kiến thay đổi

## Backend — sửa

```text
backend/PEMS.Application/ApiIntegrations/Common/ApiIntegrationConstants.cs
backend/PEMS.Application/ApiIntegrations/Common/ApiIntegrationDtos.cs
backend/PEMS.Application/ApiIntegrations/Common/ApiIntegrationMapper.cs
backend/PEMS.Application/ApiIntegrations/Commands/TestApiIntegration/TestApiIntegrationCommandHandler.cs
backend/PEMS.Api/Controllers/ApiIntegrationsController.cs
backend/PEMS.Api/Controllers/GoogleDriveOAuthController.cs
backend/PEMS.Infrastructure/FileStorage/GoogleDrive/GoogleDriveStorageService.cs
backend/PEMS.Infrastructure/DependencyInjection.cs
backend/PEMS.Api/Extensions/SecretConfigurationValidator.cs
```

## Backend — mới

```text
backend/PEMS.Application/Common/Interfaces/IGoogleDriveCredentialResolver.cs
backend/PEMS.Infrastructure/FileStorage/GoogleDrive/GoogleDriveCredentialResolver.cs
```

Nếu tách OAuth state abstraction:

```text
backend/PEMS.Application/Common/Interfaces/IGoogleDriveOAuthStateService.cs
backend/PEMS.Infrastructure/FileStorage/GoogleDrive/GoogleDriveOAuthStateService.cs
```

Tên/path có thể điều chỉnh theo structure thực tế nhưng không tạo abstraction dư thừa.

## Frontend — sửa

```text
frontend/pems-react/src/pages/dashboard/apis/ApiManagement.tsx
frontend/pems-react/src/features/api-management/api/apiManagementApi.ts
frontend/pems-react/src/features/api-management/types/apiManagement.types.ts
frontend/pems-react/src/shared/api/endpoints.ts
```

## Tests

Thêm/sửa đúng test projects hiện tại, không tạo test framework mới.

---

# 45. Thứ tự triển khai khuyến nghị

## Phase A — Preflight

1. Confirm branch/HEAD.
2. Confirm working tree/WIP, không overwrite unrelated changes.
3. Đọc files mục 5.
4. Xác định exact Google Drive api_configurations row hiện tại.
5. Xác định DI lifetime GoogleDriveStorageService.
6. Xác định canonical frontend base URL config hiện có.
7. Chạy baseline build/tests relevant.

## Phase B — Dynamic credential foundation

1. Add Google Drive constants đúng data hiện tại.
2. Add credential resolver DB -> ENV fallback.
3. Register DI.
4. Change GoogleDriveStorageService to resolver.
5. Unit test resolver/runtime.

## Phase C — OAuth secure reconnect

1. Add ADMIN start endpoint.
2. Add protected OAuth state.
3. Refactor callback.
4. Exchange token.
5. Encrypt + save DB.
6. Audit.
7. Redirect frontend.
8. Remove old token-render HTML behavior.

## Phase D — API Management integration

1. Mark Drive `HYBRID`.
2. Add OAuth capabilities.
3. Add Drive test connection.
4. Return non-secret status only.

## Phase E — Frontend

1. Types/endpoints/API client.
2. Drive card connect/reconnect.
3. Callback toast handling.
4. Expired state UI.
5. Keep HO read-only.

## Phase F — Startup/config migration

1. Update SecretConfigurationValidator.
2. Keep ENV RefreshToken fallback temporarily.
3. Verify local.
4. Verify production.

## Phase G — Closure

1. Run all tests.
2. Production ADMIN reconnect.
3. Smoke test Drive consumers.
4. Remove Railway `GoogleDrive__RefreshToken` only after verified.
5. Remove local plaintext RefreshToken where appropriate.

---

# 46. Acceptance Criteria

Feature chỉ được coi là DONE khi đạt toàn bộ:

### Functional

- [ ] ADMIN có nút `Kết nối Google Drive` / `Kết nối lại Google Drive`.
- [ ] Click mở đúng Google consent screen.
- [ ] Callback tự lưu refresh token vào DB.
- [ ] Token DB encrypted.
- [ ] Không copy/paste token thủ công.
- [ ] Không sửa appsettings để reconnect.
- [ ] Không sửa Railway variable để reconnect.
- [ ] Không restart backend để token mới có hiệu lực.
- [ ] Production ADMIN reconnect một lần -> toàn hệ thống production dùng token mới.
- [ ] Local DB riêng -> mỗi local environment quản lý token riêng.

### Security

- [ ] Start OAuth ADMIN-only.
- [ ] Callback state-protected.
- [ ] Refresh token không xuất hiện frontend/API/log/query string.
- [ ] ClientSecret không xuất hiện frontend.
- [ ] DB chỉ chứa ciphertext.
- [ ] HO không reconnect được.

### Migration

- [ ] Existing Railway RefreshToken vẫn fallback được trong rollout.
- [ ] Sau DB reconnect, DB token được ưu tiên.
- [ ] Sau remove Railway `GoogleDrive__RefreshToken`, production vẫn chạy.

### Regression

- [ ] Avatar upload pass.
- [ ] Gallery upload/read pass.
- [ ] Visit photo pass.
- [ ] Visit document pass.
- [ ] Report archive pass.
- [ ] File preview/download Drive pass.
- [ ] API Management existing providers không regression.

---

# 47. Definition of Done report cho AI Agent

Khi hoàn thành, report ngắn gọn theo format:

```text
1. Root cause / old flow
2. Architecture implemented
3. Files changed
4. DB behavior
5. OAuth security behavior
6. Local behavior
7. Railway/production behavior
8. Tests/gates executed + exact results
9. Manual production steps remaining
10. Whether GoogleDrive__RefreshToken can now be removed from Railway
```

Không báo DONE nếu chưa chứng minh runtime Drive thực sự dùng DB token mới.

---

# 48. Manual deployment checklist sau khi merge

## Google Cloud

- [ ] OAuth Client có local redirect URI.
- [ ] OAuth Client có production Railway backend callback URI.
- [ ] Consent screen/account scope đúng.
- [ ] Kiểm tra app còn `Testing` hay đã production/audience phù hợp.

## Railway

Giữ:

```text
GoogleDrive__ClientId
GoogleDrive__ClientSecret
GoogleDrive__RedirectUri
GoogleDrive__RootFolderId
GoogleDrive__AvatarFolderId
GoogleDrive__DocumentPartnerFolderId
GoogleDrive__GalleryFolderId
GoogleDrive__GalleryAreaFolderId
GoogleDrive__GalleryLocationFolderId
GoogleDrive__GalleryItemFolderId
GoogleDrive__GalleryDelegationFolderId
GoogleDrive__GalleryAudioFolderId
GoogleDrive__NewsFolderId
GoogleDrive__MinutesFolderId
GoogleDrive__VisitRequestDocumentFolderId
GoogleDrive__VisitRequestPhotoFolderId
Security__SecretProtectionKey
```

Tạm giữ trong rollout:

```text
GoogleDrive__RefreshToken
```

Sau khi ADMIN reconnect production + smoke tests pass:

```text
REMOVE GoogleDrive__RefreshToken
```

---

# 49. Trạng thái cuối cùng mong muốn

```text
                 GOOGLE CLOUD OAUTH CLIENT
                 ClientId / ClientSecret
                          |
                          |
ADMIN -> PEMS API Management -> Google Consent
                          |
                          v
                   OAuth Callback
                          |
                    refresh_token
                          |
                  ISecretProtector
                          |
                  Production MySQL
            api_configurations.credentials_json_encrypted
                          |
                          v
            IGoogleDriveCredentialResolver
                          |
                          v
             GoogleDriveStorageService
                          |
              access_token ngắn hạn
                          |
                          v
                   Google Drive API
```

Deployment config:

```text
ClientId / ClientSecret / RedirectUri / Folder IDs -> ENV/Railway
RefreshToken -> encrypted DB
```

Đây là kiến trúc cuối cùng cần đạt.
