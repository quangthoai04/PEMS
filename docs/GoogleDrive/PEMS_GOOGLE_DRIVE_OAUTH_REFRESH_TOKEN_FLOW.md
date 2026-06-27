# PEMS — Google Drive OAuth RefreshToken Flow

> File này dùng cho AI Agent đọc và code bổ sung luồng lấy `RefreshToken` cho Google Drive API.  
> Mục tiêu là giúp chức năng upload avatar/profile có thể test end-to-end, không cần lấy token thủ công bằng công cụ ngoài.

---

## 1. Bối cảnh

Chức năng upload avatar/profile đã được code để upload file lên Google Drive, nhưng hiện tại:

```text
GoogleDrive:RefreshToken trong appsettings.Development.json đang rỗng.
```

Nếu `RefreshToken` rỗng, mọi upload Google Drive sẽ lỗi vì backend không có token dài hạn để xin `AccessToken`.

Luồng cần bổ sung:

```text
GET /api/google-drive/oauth/connect
→ Redirect sang Google OAuth consent screen
→ User đăng nhập bằng tài khoản Google Drive dùng chung của nhóm
→ User bấm Allow
→ Google redirect về callback
→ Backend đổi authorization code lấy access_token + refresh_token
→ Backend hiển thị refresh_token để copy vào appsettings.Development.json
```

---

## 2. Mục tiêu

Code thêm luồng OAuth để lấy hoặc lấy lại Google Drive `RefreshToken`.

Mục tiêu cụ thể:

```text
1. Lấy RefreshToken lần đầu để test upload avatar end-to-end.
2. Lấy RefreshToken mới khi token cũ hết hạn hoặc bị revoke.
3. Không cần tạo lại Google Cloud Project.
4. Không cần tạo lại OAuth Client.
5. Không expose ClientSecret ra frontend.
6. Không tự động commit hoặc ghi secret vào Git.
```

---

## 3. Thông tin cấu hình hiện có

Backend đọc từ:

```text
backend/PEMS.Api/appsettings.Development.json
```

Cấu hình mẫu:

```json
{
  "GoogleDrive": {
    "Enabled": true,
    "AuthMode": "OAuthUser",
    "ClientId": "CLIENT_ID_THAT",
    "ClientSecret": "CLIENT_SECRET_THAT",
    "RedirectUri": "http://localhost:5265/api/google-drive/oauth/callback",
    "RootFolderId": "ID_FOLDER_PEMS_STORAGE",
    "AvatarFolderId": "ID_FOLDER_AVATARS",
    "DocumentFolderId": "ID_FOLDER_DOCUMENTS",
    "GalleryFolderId": "ID_FOLDER_GALLERY",
    "NewsFolderId": "ID_FOLDER_NEWS",
    "MinutesFolderId": "ID_FOLDER_MINUTES",
    "VisitRequestFolderId": "ID_FOLDER_VISIT_REQUESTS",
    "RefreshToken": ""
  }
}
```

Các field bắt buộc cho OAuth:

```text
GoogleDrive:ClientId
GoogleDrive:ClientSecret
GoogleDrive:RedirectUri
```

Scope cần xin:

```text
https://www.googleapis.com/auth/drive
```

---

## 4. Endpoint cần code

## 4.1 GET /api/google-drive/oauth/connect

Endpoint này tạo Google OAuth authorization URL và redirect user sang Google.

Route đề xuất:

```http
GET /api/google-drive/oauth/connect
```

Chỉ dùng trong dev/configuration, không phải chức năng business user thông thường.

### Query params gửi sang Google

Authorization URL:

```text
https://accounts.google.com/o/oauth2/v2/auth
```

Tham số bắt buộc:

```text
client_id     = GoogleDrive:ClientId
redirect_uri  = GoogleDrive:RedirectUri
response_type = code
scope         = https://www.googleapis.com/auth/drive
access_type   = offline
prompt        = consent
```

Ý nghĩa:

```text
access_type=offline
→ Yêu cầu Google trả refresh_token để backend có thể gọi Drive API khi user không mở browser.

prompt=consent
→ Ép Google hiện lại màn Allow và tăng khả năng trả refresh_token, đặc biệt khi user đã từng cấp quyền trước đó.
```

### Response

Không trả JSON. Endpoint nên:

```text
return Redirect(authorizationUrl)
```

---

## 4.2 GET /api/google-drive/oauth/callback

Endpoint này nhận `code` từ Google và đổi code lấy token.

Route phải khớp chính xác với Redirect URI đã khai báo trong Google Cloud:

```http
GET /api/google-drive/oauth/callback?code=...
```

### Request từ Google

Google redirect về:

```text
http://localhost:5265/api/google-drive/oauth/callback?code=AUTHORIZATION_CODE
```

Có thể có lỗi:

```text
http://localhost:5265/api/google-drive/oauth/callback?error=access_denied
```

### Handler flow

```text
1. Nhận query param code.
2. Nếu có error hoặc thiếu code → trả thông báo lỗi rõ.
3. Gửi POST đến Google token endpoint.
4. Đổi authorization code lấy access_token và refresh_token.
5. Nếu có refresh_token:
   - Hiển thị refresh_token cho dev copy.
   - Không tự động commit / ghi vào appsettings.json.
6. Nếu không có refresh_token:
   - Hiển thị hướng dẫn revoke app access hoặc chạy lại connect với prompt=consent.
```

Google token endpoint:

```text
https://oauth2.googleapis.com/token
```

Body dạng `application/x-www-form-urlencoded`:

```text
code=AUTHORIZATION_CODE
client_id=GoogleDrive:ClientId
client_secret=GoogleDrive:ClientSecret
redirect_uri=GoogleDrive:RedirectUri
grant_type=authorization_code
```

### Response thành công

Có thể trả HTML/text đơn giản:

```text
Google Drive connected successfully.

Copy this RefreshToken into:
backend/PEMS.Api/appsettings.Development.json

GoogleDrive:RefreshToken = "1//xxxxxxxx"

Then restart backend and test avatar upload.
```

Không trả `ClientSecret`.

Không expose token qua frontend app chính. Đây là endpoint dev/config.

---

## 5. Bảo vệ endpoint

Vì đây là endpoint lấy secret/token, không để mở bừa trong production.

Yêu cầu tối thiểu:

```text
- Chỉ cho chạy khi ASPNETCORE_ENVIRONMENT = Development.
- Nếu không phải Development → trả 404 hoặc 403.
```

Pseudo:

```csharp
if (!_environment.IsDevelopment())
{
    return NotFound();
}
```

Hoặc:

```csharp
if (!_environment.IsDevelopment())
{
    return Forbid();
}
```

Khuyến nghị đơn giản nhất cho PEMS dev:

```text
Development-only.
```

Không cần bắt login nội bộ nếu mục tiêu chỉ để lấy token local, nhưng nếu hệ thống đã có auth thuận tiện thì có thể giới hạn thêm ADMIN/HO.

---

## 6. Nơi đặt code theo Clean Architecture

Vì đây là integration/dev utility, có thể đặt trong API layer hoặc tách service trong Infrastructure.

Gợi ý đơn giản:

```text
backend/PEMS.Api/Controllers/GoogleDriveOAuthController.cs
```

Controller chỉ xử lý OAuth redirect/callback dev utility.

Nếu muốn sạch hơn:

```text
PEMS.Application/Common/Interfaces/IGoogleDriveOAuthService.cs
PEMS.Infrastructure/FileStorage/GoogleDrive/GoogleDriveOAuthService.cs
PEMS.Api/Controllers/GoogleDriveOAuthController.cs
```

Nhưng không nên over-engineer nếu chỉ phục vụ lấy token dev.

---

## 7. Pseudo code controller

```csharp
[ApiController]
[Route("api/google-drive/oauth")]
public sealed class GoogleDriveOAuthController : ControllerBase
{
    private readonly GoogleDriveOptions _options;
    private readonly IWebHostEnvironment _environment;
    private readonly IHttpClientFactory _httpClientFactory;

    public GoogleDriveOAuthController(
        IOptions<GoogleDriveOptions> options,
        IWebHostEnvironment environment,
        IHttpClientFactory httpClientFactory)
    {
        _options = options.Value;
        _environment = environment;
        _httpClientFactory = httpClientFactory;
    }

    [HttpGet("connect")]
    public IActionResult Connect()
    {
        if (!_environment.IsDevelopment())
        {
            return NotFound();
        }

        if (string.IsNullOrWhiteSpace(_options.ClientId) ||
            string.IsNullOrWhiteSpace(_options.RedirectUri))
        {
            return BadRequest("Missing GoogleDrive ClientId or RedirectUri.");
        }

        var query = new Dictionary<string, string?>
        {
            ["client_id"] = _options.ClientId,
            ["redirect_uri"] = _options.RedirectUri,
            ["response_type"] = "code",
            ["scope"] = "https://www.googleapis.com/auth/drive",
            ["access_type"] = "offline",
            ["prompt"] = "consent"
        };

        var authorizationUrl = QueryHelpers.AddQueryString(
            "https://accounts.google.com/o/oauth2/v2/auth",
            query);

        return Redirect(authorizationUrl);
    }

    [HttpGet("callback")]
    public async Task<IActionResult> Callback(
        [FromQuery] string? code,
        [FromQuery] string? error,
        CancellationToken cancellationToken)
    {
        if (!_environment.IsDevelopment())
        {
            return NotFound();
        }

        if (!string.IsNullOrWhiteSpace(error))
        {
            return BadRequest($"Google OAuth error: {error}");
        }

        if (string.IsNullOrWhiteSpace(code))
        {
            return BadRequest("Missing authorization code.");
        }

        if (string.IsNullOrWhiteSpace(_options.ClientId) ||
            string.IsNullOrWhiteSpace(_options.ClientSecret) ||
            string.IsNullOrWhiteSpace(_options.RedirectUri))
        {
            return BadRequest("Missing GoogleDrive OAuth configuration.");
        }

        var httpClient = _httpClientFactory.CreateClient();

        var form = new Dictionary<string, string>
        {
            ["code"] = code,
            ["client_id"] = _options.ClientId,
            ["client_secret"] = _options.ClientSecret,
            ["redirect_uri"] = _options.RedirectUri,
            ["grant_type"] = "authorization_code"
        };

        var response = await httpClient.PostAsync(
            "https://oauth2.googleapis.com/token",
            new FormUrlEncodedContent(form),
            cancellationToken);

        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            return StatusCode((int)response.StatusCode, body);
        }

        // Parse JSON:
        // access_token, expires_in, token_type, scope, refresh_token

        // Return a simple text/html page with refresh_token only.
        // Do not return ClientSecret.
    }
}
```

---

## 8. Token response DTO

Google token endpoint trả JSON dạng:

```json
{
  "access_token": "ya29.xxxxx",
  "expires_in": 3599,
  "refresh_token": "1//xxxxx",
  "scope": "https://www.googleapis.com/auth/drive",
  "token_type": "Bearer"
}
```

DTO:

```csharp
public sealed class GoogleOAuthTokenResponse
{
    [JsonPropertyName("access_token")]
    public string? AccessToken { get; set; }

    [JsonPropertyName("expires_in")]
    public int ExpiresIn { get; set; }

    [JsonPropertyName("refresh_token")]
    public string? RefreshToken { get; set; }

    [JsonPropertyName("scope")]
    public string? Scope { get; set; }

    [JsonPropertyName("token_type")]
    public string? TokenType { get; set; }
}
```

Callback nên ưu tiên hiển thị `RefreshToken`.

Nếu `RefreshToken` null:

```text
Không có refresh_token trong response.
Hãy thử:
1. Đảm bảo connect URL có prompt=consent và access_type=offline.
2. Vào Google Account Security → Third-party access → gỡ quyền app PEMS.
3. Chạy lại /api/google-drive/oauth/connect.
```

---

## 9. Sau khi lấy RefreshToken

Dev làm thủ công:

```text
1. Copy RefreshToken từ callback page.
2. Mở:
   backend/PEMS.Api/appsettings.Development.json
3. Dán vào:
   GoogleDrive:RefreshToken
4. Lưu file.
5. Restart backend.
6. Test upload avatar.
```

Ví dụ:

```json
{
  "GoogleDrive": {
    "RefreshToken": "1//xxxxxxxxxxxxxxxx"
  }
}
```

Không commit file này.

---

## 10. Khi RefreshToken hết hạn sau 7 ngày

Trong giai đoạn dev, nếu OAuth app có:

```text
User type: External
Publishing status: Testing
Scope: Google Drive
```

thì refresh token có thể hết hạn sau 7 ngày.

Khi hết hạn, upload Google Drive thường lỗi:

```text
invalid_grant
GOOGLE_DRIVE_TOKEN_EXPIRED
```

Cách xử lý:

```text
1. Mở lại:
   http://localhost:5265/api/google-drive/oauth/connect
2. Đăng nhập bằng tài khoản Google Drive dùng chung của nhóm.
3. Bấm Allow.
4. Google redirect về callback.
5. Copy RefreshToken mới.
6. Dán vào appsettings.Development.json.
7. Restart backend.
8. Test upload lại.
```

Không cần:

```text
- Không cần tạo lại Google Cloud Project.
- Không cần tạo lại Google Drive API.
- Không cần tạo lại OAuth Client.
- Không cần đổi FolderId.
- Không cần sửa code.
```

---

## 11. Error handling khi upload dùng token hết hạn

GoogleDriveStorageService nên bắt lỗi token rõ ràng.

Nếu Google token endpoint trả lỗi:

```json
{
  "error": "invalid_grant",
  "error_description": "Token has been expired or revoked."
}
```

Backend nên trả lỗi nghiệp vụ:

```text
GOOGLE_DRIVE_TOKEN_EXPIRED
Google Drive token đã hết hạn. Vui lòng chạy lại /api/google-drive/oauth/connect để lấy RefreshToken mới.
```

Frontend hiển thị:

```text
Google Drive token đã hết hạn. Vui lòng liên hệ người phụ trách cấu hình để kết nối lại.
```

---

## 12. Không tự ghi token vào appsettings.json

Không yêu cầu backend tự sửa file config.

Không làm:

```text
- Không ghi RefreshToken vào appsettings.json.
- Không ghi RefreshToken vào appsettings.Development.json bằng code runtime nếu không có yêu cầu rõ.
- Không commit RefreshToken.
- Không trả ClientSecret ra response.
```

Lý do:

```text
- Tránh rủi ro lộ secret.
- Tránh backend có quyền sửa source/config file.
- Tránh GitHub Desktop vô tình hiện change chứa RefreshToken.
```

Trong giai đoạn dev, callback chỉ cần hiển thị token để người phụ trách copy thủ công.

---

## 13. Test checklist

### Google OAuth connect/callback

```text
[ ] Backend chạy ở Development.
[ ] Mở /api/google-drive/oauth/connect redirect sang Google.
[ ] Google hiện màn xin quyền Drive.
[ ] Đăng nhập bằng tài khoản Google Drive dùng chung.
[ ] Bấm Allow.
[ ] Google redirect về /api/google-drive/oauth/callback.
[ ] Callback đổi code thành công.
[ ] Callback hiển thị RefreshToken.
[ ] Không hiển thị ClientSecret.
[ ] Copy RefreshToken vào appsettings.Development.json.
[ ] Restart backend.
```

### Upload avatar sau khi có token

```text
[ ] Upload avatar JPG/PNG/WEBP thành công.
[ ] File xuất hiện trong Google Drive folder avatars.
[ ] Bảng files có external_file_id.
[ ] users.avatar_url được update.
[ ] Refresh page vẫn xem được avatar.
```

### Token hết hạn

```text
[ ] Khi RefreshToken sai/hết hạn, upload trả lỗi rõ.
[ ] Chạy lại /oauth/connect lấy token mới.
[ ] Dán token mới và restart backend.
[ ] Upload hoạt động lại.
```

---

## 14. Acceptance Criteria

```text
AC-01: GET /api/google-drive/oauth/connect redirect được sang Google OAuth.
AC-02: Authorization URL có scope Drive, access_type=offline, prompt=consent.
AC-03: GET /api/google-drive/oauth/callback nhận code và đổi được token.
AC-04: Callback hiển thị RefreshToken cho dev copy.
AC-05: Endpoint chỉ hoạt động trong Development hoặc bị chặn ở non-Development.
AC-06: Không expose ClientSecret ra response.
AC-07: Không tự commit hoặc ghi secret vào Git.
AC-08: Sau khi dán RefreshToken vào config, upload avatar lên Google Drive chạy end-to-end.
AC-09: Khi token hết hạn, có thể chạy lại connect/callback để lấy token mới mà không sửa code.
```

---

## 15. Prompt ngắn cho AI Agent

Dán đoạn này cho AI Agent:

```text
Code bổ sung Google Drive OAuth connect/callback để lấy RefreshToken cho PEMS.

Bối cảnh:
- Chức năng upload avatar/profile đã cần GoogleDrive:RefreshToken.
- appsettings.Development.json đã có ClientId, ClientSecret, RedirectUri, FolderIds nhưng RefreshToken đang rỗng.
- Backend ASP.NET Core .NET 8.
- Không commit secret.
- Mục tiêu là test end-to-end upload avatar lên Google Drive.

Yêu cầu:
1. Thêm GET /api/google-drive/oauth/connect.
2. Endpoint tạo Google OAuth authorization URL và redirect sang Google.
3. URL phải có:
   - client_id từ config
   - redirect_uri từ config
   - response_type=code
   - scope=https://www.googleapis.com/auth/drive
   - access_type=offline
   - prompt=consent
4. Thêm GET /api/google-drive/oauth/callback.
5. Callback nhận query param code hoặc error.
6. Nếu error/thiếu code, trả message rõ.
7. Nếu có code, POST đến https://oauth2.googleapis.com/token với:
   - code
   - client_id
   - client_secret
   - redirect_uri
   - grant_type=authorization_code
8. Parse token response.
9. Nếu có refresh_token, hiển thị trang/text hướng dẫn copy refresh_token vào GoogleDrive:RefreshToken trong appsettings.Development.json.
10. Nếu không có refresh_token, hiển thị hướng dẫn revoke app access và chạy lại connect.
11. Chỉ cho endpoint hoạt động trong Development; non-Development trả 404 hoặc 403.
12. Không expose ClientSecret.
13. Không tự động ghi token vào appsettings.json.
14. Sau khi lấy token, test upload avatar end-to-end.

Acceptance:
- /connect redirect sang Google.
- /callback lấy được RefreshToken.
- Dán RefreshToken vào appsettings.Development.json rồi restart backend.
- Upload avatar profile thành công lên Google Drive.
```
