# PROMPT_IMPLEMENT_AUTH_HARDENING_TODOS

## Mục tiêu

Triển khai các TODO còn lại sau phase **Auth Hardening (post Core Auth Dual Portal)** cho dự án PEMS, nhưng **không refactor lại Core Auth đang chạy**.

Các phần Core Auth đã ổn và không được phá:

- Login bằng Credentials.
- Login Google SSO.
- Dual Portal policy: `VISITOR` / `INTERNAL`.
- RBAC / permissions hiện tại.
- JWT access token.
- Refresh token hash + rotation.
- DB-backed session.
- Logout revoke session.
- Revoke session khi đổi role / reset password / change password.
- SessionValidationMiddleware chặn user/role non-ACTIVE ở request kế tiếp.
- Error contract hiện tại: `{ success:false, errorCode, message, traceId }`.

Nhiệm vụ lần này chỉ gồm:

1. Implement UC-97 revoke session khi khóa/deactivate user.
2. Backend sanitize News HTML bằng `Ganss.Xss`.
3. Kiểm tra và harden `FileValidationService` để chặn SVG/HTML/JS upload.
4. Chốt production domain + secrets qua environment variables.
5. Cập nhật docs/changelog/test cases sau mỗi phần.

---

## Nguyên tắc bắt buộc

### Không được làm

- Không viết lại login flow.
- Không đổi contract response thành format mới.
- Không đổi tên error code auth đang dùng.
- Không bỏ `SessionValidationMiddleware`.
- Không chuyển refresh token sang cookie trong phase này.
- Không đổi localStorage/token storage của frontend trong phase này.
- Không tự ý sửa SQL schema lớn nếu không cần thiết.
- Không hard-code secret production vào `appsettings.Production.json`.
- Không commit JWT secret, DB password, SMTP password, Google Client Secret.

### Được làm

- Bổ sung logic revoke session khi account bị khóa/deactivate.
- Thêm service sanitize HTML phía backend.
- Sanitize News HTML trước khi lưu DB.
- Bổ sung test cases.
- Bổ sung validation upload file nguy hiểm.
- Cập nhật docs và changelog.
- Cập nhật cấu hình production theo hướng dùng environment variables / secret manager.

---

## Task 1 — Implement UC-97 revoke session khi khóa/deactivate user

### Bối cảnh

Hiện tại hệ thống đã revoke session khi:

- Logout.
- Đổi role.
- Reset password.
- Change password.

Tuy nhiên `ManageAccountStatusCommandHandler` của UC-97 vẫn là scaffold / chưa implement revoke session đầy đủ.

`SessionValidationMiddleware` đã có thể chặn user non-ACTIVE ở request kế tiếp, nhưng vẫn cần revoke session để DB phản ánh đúng trạng thái bảo mật.

### Việc cần làm

Tìm handler quản lý trạng thái account, dự kiến là:

```text
PEMS.Application/Features/Accounts/Commands/ManageAccountStatus/ManageAccountStatusCommandHandler.cs
```

Tên file có thể khác tùy cấu trúc thực tế. Hãy search toàn repo các keyword:

```text
ManageAccountStatusCommandHandler
ManageAccountStatusCommand
UC-97
ACCOUNT_DEACTIVATED
RevokeAllActiveSessionsAsync
```

Sau khi tìm đúng handler:

1. Hoàn thiện business logic khóa/deactivate user nếu handler còn scaffold.
2. Khi trạng thái user bị chuyển sang trạng thái không được phép đăng nhập / sử dụng hệ thống, gọi revoke toàn bộ active sessions của user đó.
3. Không revoke session nếu chỉ update metadata không ảnh hưởng trạng thái đăng nhập.
4. Ghi audit/security event nếu hệ thống đã có pattern tương ứng.
5. Trả response theo convention hiện tại của Application layer.

### Điều kiện cần revoke

Cần revoke khi user bị chuyển sang một trong các trạng thái tương đương:

```text
INACTIVE
LOCKED
DEACTIVATED
DISABLED
SUSPENDED
```

Tên enum thực tế phải lấy từ codebase, không được tự bịa enum mới nếu đã có enum sẵn.

Nếu trạng thái hiện tại trong project chỉ có `ACTIVE` / `INACTIVE`, thì chỉ cần xử lý:

```text
ACTIVE -> INACTIVE
```

Nếu có `LOCKED`, xử lý thêm:

```text
ACTIVE -> LOCKED
```

### Pseudo code

```csharp
var previousStatus = user.Status;

// update status
user.Status = request.Status;
user.UpdatedAt = _dateTime.UtcNow;
user.UpdatedBy = currentUserId;

var shouldRevokeSessions =
    previousStatus == UserStatus.ACTIVE &&
    request.Status != UserStatus.ACTIVE;

if (shouldRevokeSessions)
{
    await _sessionService.RevokeAllActiveSessionsAsync(
        user.UserId,
        currentUserId,
        "ACCOUNT_DEACTIVATED",
        cancellationToken);
}
```

Nếu project đang dùng string enum thay vì C# enum, so sánh theo constant hiện có.

### Yêu cầu kỹ thuật

- Dùng service revoke session đã tồn tại, không tạo service trùng.
- Nếu method hiện có tên khác, dùng đúng method hiện tại.
- Không xóa session khỏi DB, chỉ set revoked fields.
- Reason nên thống nhất:

```text
ACCOUNT_DEACTIVATED
```

Hoặc nếu codebase đã có constant reason, dùng constant đó.

### Acceptance criteria

- Khi admin/staff có quyền khóa user, tất cả active sessions của user đó bị revoke.
- Access token cũ gọi protected API sau khi bị khóa phải trả `401`.
- Refresh token cũ không refresh được.
- User đó login lại phải bị chặn nếu status không ACTIVE.
- Không ảnh hưởng flow đổi role / reset password / change password đang chạy.
- Build backend pass.

### Test case cần thêm/cập nhật

Trong `docs/auth/AUTH_HARDENING_TEST_CASES.md`, cập nhật mục:

```text
[ ] Khóa user (ManageAccountStatus) → session bị revoke.
```

Sau khi test runtime pass, đổi thành:

```text
[x] Khóa user (ManageAccountStatus) → session bị revoke.
```

---

## Task 2 — Backend sanitize News HTML bằng Ganss.Xss

### Bối cảnh

Frontend đã có `sanitizeHtml()` khi render News, nhưng backend vẫn cần sanitize trước khi lưu DB để tránh dữ liệu độc hại tồn tại lâu dài.

Cần sanitize các trường HTML của News, đặc biệt:

```text
section_body_html
body_html
content_html
description_html
```

Tên field chính xác phải lấy từ entity/command thực tế.

### Package cần thêm

Thêm NuGet package vào project phù hợp, thường là Infrastructure:

```bash
dotnet add PEMS.Infrastructure/PEMS.Infrastructure.csproj package Ganss.Xss
```

Nếu team muốn service nằm ở Application nhưng implementation ở Infrastructure, interface đặt ở Application, implementation đặt ở Infrastructure.

### Interface đề xuất

Tạo interface:

```text
PEMS.Application/Common/Security/IHtmlSanitizerService.cs
```

Nội dung đề xuất:

```csharp
namespace PEMS.Application.Common.Security;

public interface IHtmlSanitizerService
{
    string Sanitize(string? html);
}
```

### Implementation đề xuất

Tạo implementation:

```text
PEMS.Infrastructure/Security/HtmlSanitizerService.cs
```

Nội dung đề xuất:

```csharp
using Ganss.Xss;
using PEMS.Application.Common.Security;

namespace PEMS.Infrastructure.Security;

public sealed class HtmlSanitizerService : IHtmlSanitizerService
{
    private readonly HtmlSanitizer _sanitizer;

    public HtmlSanitizerService()
    {
        _sanitizer = new HtmlSanitizer();

        // Không cho phép các tag nguy hiểm.
        _sanitizer.AllowedTags.Remove("script");
        _sanitizer.AllowedTags.Remove("iframe");
        _sanitizer.AllowedTags.Remove("object");
        _sanitizer.AllowedTags.Remove("embed");
        _sanitizer.AllowedTags.Remove("form");
        _sanitizer.AllowedTags.Remove("input");
        _sanitizer.AllowedTags.Remove("button");
        _sanitizer.AllowedTags.Remove("style");

        // Không cho phép event handler.
        _sanitizer.AllowedAttributes.Remove("onclick");
        _sanitizer.AllowedAttributes.Remove("onerror");
        _sanitizer.AllowedAttributes.Remove("onload");
        _sanitizer.AllowedAttributes.Remove("onmouseover");

        // Chỉ cho phép scheme an toàn.
        _sanitizer.AllowedSchemes.Clear();
        _sanitizer.AllowedSchemes.Add("http");
        _sanitizer.AllowedSchemes.Add("https");
        _sanitizer.AllowedSchemes.Add("mailto");
        _sanitizer.AllowedSchemes.Add("tel");
    }

    public string Sanitize(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return string.Empty;
        }

        return _sanitizer.Sanitize(html);
    }
}
```

Nếu Ganss.Xss mặc định đã remove event handler thì vẫn giữ cấu hình rõ ràng để dễ audit.

### Đăng ký DI

Tìm file đăng ký Infrastructure dependency, ví dụ:

```text
PEMS.Infrastructure/DependencyInjection.cs
```

Thêm:

```csharp
services.AddSingleton<IHtmlSanitizerService, HtmlSanitizerService>();
```

Nếu project đang dùng `Scoped` cho các security service, có thể dùng:

```csharp
services.AddScoped<IHtmlSanitizerService, HtmlSanitizerService>();
```

Không đăng ký trùng nhiều lần.

### Áp dụng vào News command handlers

Tìm các command handler liên quan News:

```text
AddMultilingualNews
EditNews
CreateNews
UpdateNews
NewsContentSection
section_body_html
```

Trong handler tạo/sửa News, inject:

```csharp
private readonly IHtmlSanitizerService _htmlSanitizer;
```

Khi map input vào entity:

```csharp
section.SectionBodyHtml = _htmlSanitizer.Sanitize(request.SectionBodyHtml);
```

Nếu request có nhiều ngôn ngữ / nhiều section:

```csharp
foreach (var section in request.Sections)
{
    var safeHtml = _htmlSanitizer.Sanitize(section.SectionBodyHtml);

    news.Sections.Add(new NewsContentSection
    {
        SectionTitle = section.SectionTitle?.Trim(),
        SectionBodyHtml = safeHtml,
        // các field khác giữ nguyên
    });
}
```

### Không được làm

- Không xóa toàn bộ HTML thành plain text nếu yêu cầu nghiệp vụ cần giữ định dạng.
- Không cho phép `script`, `iframe`, `object`, `embed`, `form`.
- Không cho phép URL scheme `javascript:`, `vbscript:`, `data:text/html`.
- Không bỏ sanitize frontend hiện có; backend sanitize là lớp bổ sung.

### Test payload cần kiểm tra

Dùng các payload sau khi create/update News:

```html
<script>alert(1)</script>
<img src=x onerror=alert(1)>
<a href="javascript:alert(1)">Click</a>
<iframe src="https://evil.com"></iframe>
<object data="evil"></object>
<p>Hello <strong>World</strong></p>
```

Kỳ vọng:

- Script bị xóa.
- Event handler `onerror` bị xóa.
- `javascript:` href bị xóa hoặc neutralize.
- `iframe/object/embed` bị xóa.
- HTML format an toàn như `p`, `strong`, `em`, `ul`, `li`, `a[href=https]` vẫn giữ.

### Acceptance criteria

- Backend sanitize trước khi lưu DB.
- Frontend vẫn render qua `sanitizeHtml()` như hiện tại.
- News an toàn khi render bằng `dangerouslySetInnerHTML`.
- Build backend pass.
- Không phá create/edit News nếu feature đã chạy.
- Có test hoặc manual test ghi trong docs.

---

## Task 3 — Kiểm tra và harden FileValidationService chặn SVG/HTML/JS

### Bối cảnh

Upload file là điểm rủi ro XSS/RCE nếu cho phép upload file có thể thực thi hoặc render script.

Cần kiểm tra service:

```text
FileValidationService
IFileValidationService
FileUploadValidationFilter
UploadedFile
```

### Việc cần kiểm tra

Search toàn repo:

```text
FileValidationService
IFileValidationService
ValidateFile
AllowedExtensions
AllowedMimeTypes
image/svg+xml
text/html
application/javascript
```

### Loại file phải chặn

#### Extension nguy hiểm

```text
.svg
.svgz
.html
.htm
.js
.mjs
.jsx
.ts
.tsx
.vbs
.php
.aspx
.jsp
```

Tùy nghiệp vụ có thể chặn thêm:

```text
.exe
.bat
.cmd
.ps1
.sh
jar
```

#### MIME nguy hiểm

```text
image/svg+xml
text/html
application/xhtml+xml
application/javascript
text/javascript
application/ecmascript
text/ecmascript
application/x-msdownload
application/x-sh
```

### Logic validation đề xuất

Khi validate upload:

1. Kiểm tra file null / empty.
2. Kiểm tra size.
3. Normalize extension về lowercase.
4. Chặn extension trong denylist trước.
5. Kiểm tra MIME trong denylist.
6. Nếu đang dùng allowlist thì chỉ cho phép các MIME/extension nghiệp vụ thật sự cần.
7. Với file ảnh, kiểm tra magic bytes nếu đã có helper.
8. Không tin tuyệt đối vào `ContentType` từ client.
9. Không cho upload SVG dù được xem là image.

### Pseudo code

```csharp
private static readonly HashSet<string> BlockedExtensions = new(StringComparer.OrdinalIgnoreCase)
{
    ".svg", ".svgz", ".html", ".htm", ".js", ".mjs", ".jsx", ".ts", ".tsx",
    ".vbs", ".php", ".aspx", ".jsp", ".exe", ".bat", ".cmd", ".ps1", ".sh"
};

private static readonly HashSet<string> BlockedMimeTypes = new(StringComparer.OrdinalIgnoreCase)
{
    "image/svg+xml",
    "text/html",
    "application/xhtml+xml",
    "application/javascript",
    "text/javascript",
    "application/ecmascript",
    "text/ecmascript",
    "application/x-msdownload",
    "application/x-sh"
};

public void Validate(IFormFile file)
{
    var extension = Path.GetExtension(file.FileName);
    var contentType = file.ContentType;

    if (BlockedExtensions.Contains(extension))
    {
        throw new BusinessRuleException("Loại file này không được phép tải lên.");
    }

    if (BlockedMimeTypes.Contains(contentType))
    {
        throw new BusinessRuleException("Loại file này không được phép tải lên.");
    }

    // tiếp tục validate allowlist/size/magic bytes hiện có
}
```

Tên exception phải dùng đúng convention hiện tại của project.

### Acceptance criteria

- Upload `.svg` bị từ chối.
- Upload `.html` bị từ chối.
- Upload `.js` bị từ chối.
- File đổi đuôi nhưng MIME nguy hiểm vẫn bị từ chối.
- File ảnh hợp lệ như `.jpg`, `.jpeg`, `.png`, `.webp` vẫn upload được nếu nghiệp vụ cho phép.
- File PDF/doc/docx/xlsx hợp lệ vẫn upload được nếu nghiệp vụ cho phép.
- Không phá flow upload Gallery/News/Documents nếu có.

### Test case cần bổ sung

Trong docs test cases, thêm:

```text
[ ] Upload .svg bị chặn.
[ ] Upload .html bị chặn.
[ ] Upload .js bị chặn.
[ ] Upload ảnh hợp lệ vẫn pass.
[ ] Upload PDF/doc hợp lệ vẫn pass nếu nằm trong allowlist nghiệp vụ.
```

---

## Task 4 — Chốt production domain + secrets qua environment variables

### Bối cảnh

`appsettings.Production.json` không được chứa secret thật. Các secret phải override bằng environment variables hoặc secret manager.

### Việc cần kiểm tra

Kiểm tra các file:

```text
PEMS.Api/appsettings.json
PEMS.Api/appsettings.Development.json
PEMS.Api/appsettings.Production.json
frontend/pems-react/.env
frontend/pems-react/.env.production
```

### Không được commit secret thật

Không commit các giá trị thật của:

```text
JwtSettings:SecretKey
ConnectionStrings:DefaultConnection
SMTP password
GoogleAuth:ClientSecret
Feid:ClientSecret
API keys
DB password
```

### Environment variables gợi ý cho backend .NET

Dùng format `__` cho nested config:

```bash
JwtSettings__SecretKey="CHANGE_ME_FROM_SECRET_MANAGER"
ConnectionStrings__DefaultConnection="Server=...;Database=...;User=...;Password=..."
SmtpSettings__Password="CHANGE_ME_FROM_SECRET_MANAGER"
GoogleAuth__ClientId="..."
GoogleAuth__ClientSecret="..."
Cors__AllowedOrigins__0="https://your-frontend-domain.com"
AllowedHosts="your-api-domain.com"
```

Tên section chính xác phải khớp config hiện tại trong repo.

### Frontend production env

Trong frontend, chỉ để public config cần thiết:

```bash
VITE_API_BASE_URL=https://your-api-domain.com/api
VITE_GOOGLE_CLIENT_ID=your-google-client-id.apps.googleusercontent.com
```

Không đưa secret vào frontend `.env`.

### CORS production

Trong `appsettings.Production.json`, thay placeholder bằng domain thật:

```json
{
  "Cors": {
    "AllowedOrigins": [
      "https://your-frontend-domain.com"
    ]
  },
  "AllowedHosts": "your-api-domain.com"
}
```

Nếu dùng nhiều domain:

```json
{
  "Cors": {
    "AllowedOrigins": [
      "https://your-frontend-domain.com",
      "https://www.your-frontend-domain.com"
    ]
  }
}
```

Không dùng:

```text
AllowAnyOrigin
*
```

trong production.

### Checklist production

- Backend chạy HTTPS.
- Frontend gọi HTTPS API.
- CORS chỉ allow frontend domain thật.
- Google OAuth Authorized JavaScript origins đã thêm frontend domain thật.
- Google OAuth redirect URI đúng nếu có redirect flow.
- JWT secret đủ dài, random, không dùng default dev.
- DB password không nằm trong git.
- SMTP password không nằm trong git.
- FEID provider vẫn disabled nếu chưa có credential thật.
- `ASPNETCORE_ENVIRONMENT=Production`.
- Production 500 không trả stackTrace.

### Acceptance criteria

- Không còn placeholder production domain trước khi deploy.
- Không có secret thật trong repo.
- App đọc secret từ env/secret manager.
- Production CORS chặn origin lạ.
- Build backend/frontend pass.

---

## Task 5 — Cập nhật docs/changelog/test cases sau mỗi việc

### File cần cập nhật

Cập nhật các file sau theo đúng thay đổi thực tế:

```text
docs/auth/AUTH_HARDENING_REPORT.md
docs/auth/AUTH_HARDENING_TEST_CASES.md
docs/auth/AUTH_SECURITY_CHECKLIST.md
docs/auth/AUTH_ERROR_CODES.md
docs/database/DATABASE_DEPLOYMENT.md
docs/architecture/REFACTOR_CHANGELOG.md
```

Nếu tên thư mục thực tế là `docs/authentication` thay vì `docs/auth`, dùng đúng thư mục hiện có.

### Nội dung cần cập nhật

#### AUTH_HARDENING_REPORT.md

Thêm section:

```md
## Update — UC-97 Account Status Session Revoke

- Implemented revoke all active sessions when account status changes from ACTIVE to non-ACTIVE.
- Reason: ACCOUNT_DEACTIVATED.
- Protected API with old access token now returns 401 after account deactivation.
- Old refresh token cannot be used after account deactivation.
```

Thêm section:

```md
## Update — Backend News HTML Sanitization

- Added Ganss.Xss.
- Added IHtmlSanitizerService.
- Sanitized News HTML before saving to database.
- Frontend render-time sanitize remains enabled.
```

Thêm section:

```md
## Update — Upload File Guard

- Blocked SVG/HTML/JS and other executable/scriptable upload types.
- Validation checks extension and MIME type.
- Existing allowed business file types remain supported.
```

#### AUTH_HARDENING_TEST_CASES.md

Cập nhật trạng thái test.

Ví dụ:

```md
- [x] Khóa user (ManageAccountStatus) → tất cả active sessions bị revoke.
- [x] User bị khóa gọi protected API bằng access token cũ → 401.
- [x] User bị khóa dùng refresh token cũ → 401.
- [x] Backend sanitize `<script>alert(1)</script>` trước khi lưu News.
- [x] Backend remove `onerror` khỏi HTML News.
- [x] Upload `.svg` bị chặn.
- [x] Upload `.html` bị chặn.
- [x] Upload `.js` bị chặn.
```

Nếu chưa test runtime, giữ `[ ]` và ghi rõ:

```md
Runtime test pending on staging DB.
```

#### AUTH_SECURITY_CHECKLIST.md

Đổi các TODO đã hoàn thành từ `[ ]` sang `[x]`.

Ví dụ:

```md
- [x] Khóa/deactivate user → revoke session.
- [x] Backend sanitize HTML khi lưu News.
- [x] Upload guard chặn SVG/HTML/JS.
- [x] Production override JWT SecretKey / DB password / SMTP password bằng env/secret manager.
```

Chỉ tick `[x]` nếu đã thực sự implement/test.

#### REFACTOR_CHANGELOG.md

Thêm entry:

```md
## 2026-06-20 — Auth Hardening TODO Completion

### Summary
Completed remaining auth hardening TODOs: UC-97 session revoke on account deactivation, backend News HTML sanitization, upload guard hardening, production secrets/domain checklist, and documentation sync.

### Backend
- Implemented session revoke in ManageAccountStatusCommandHandler.
- Added Ganss.Xss-based HtmlSanitizerService.
- Applied backend HTML sanitize to News create/update flow.
- Hardened FileValidationService denylist for SVG/HTML/JS/executable script types.

### Config
- Confirmed production secrets must be provided via environment variables / secret manager.
- Confirmed production CORS must use real frontend domain only.

### Tests
- Backend build PASS.
- Runtime auth/security test cases updated in AUTH_HARDENING_TEST_CASES.md.
```

---

## Build commands bắt buộc chạy

Chạy backend:

```bash
dotnet restore
dotnet build PEMS.Api/PEMS.Api.csproj
```

Nếu dev server đang khóa `bin`, build ra thư mục tạm:

```bash
dotnet build PEMS.Api/PEMS.Api.csproj -p:BaseOutputPath=./.tmp-build/
```

Chạy frontend:

```bash
cd frontend/pems-react
npm install
npm run build
```

Nếu có test project:

```bash
dotnet test
```

Nếu chưa có test project auth, ghi rõ trong changelog:

```text
Backend test project for auth is not configured yet; runtime/manual test cases documented.
```

---

## Manual runtime test checklist

### UC-97 revoke session

1. Login bằng user A.
2. Lưu access token + refresh token.
3. Gọi protected API → success.
4. Admin khóa/deactivate user A.
5. Dùng access token cũ gọi protected API.
6. Kỳ vọng: `401`.
7. Dùng refresh token cũ gọi `/auth/refresh`.
8. Kỳ vọng: `401`.
9. Kiểm tra DB `user_sessions.revoked_at IS NOT NULL`.

### Backend News sanitize

1. Create hoặc update News với payload:

```html
<p>Hello</p><script>alert(1)</script><img src=x onerror=alert(1)>
```

2. Kiểm tra DB.
3. Kỳ vọng:
   - Không còn `<script>`.
   - Không còn `onerror`.
   - Nội dung an toàn vẫn giữ format cơ bản.

### Upload guard

1. Upload `.svg`.
2. Kỳ vọng: reject.
3. Upload `.html`.
4. Kỳ vọng: reject.
5. Upload `.js`.
6. Kỳ vọng: reject.
7. Upload `.png` hợp lệ.
8. Kỳ vọng: pass nếu nghiệp vụ cho phép.
9. Upload `.pdf` hợp lệ.
10. Kỳ vọng: pass nếu nghiệp vụ cho phép.

### Production config

1. Chạy app với `ASPNETCORE_ENVIRONMENT=Production`.
2. Gọi endpoint gây lỗi bất ngờ có kiểm soát.
3. Kỳ vọng response không có `stackTrace`.
4. Gọi từ origin không nằm trong CORS.
5. Kỳ vọng bị chặn.
6. Gọi từ frontend domain thật.
7. Kỳ vọng pass.

---

## Output cuối cùng cần báo cáo

Sau khi triển khai xong, trả về báo cáo ngắn theo format:

```md
# Auth Hardening TODO Completion Report

## Completed
- UC-97 revoke session on account deactivation: DONE / PARTIAL / NOT DONE
- Backend News HTML sanitize: DONE / PARTIAL / NOT DONE
- FileValidationService upload guard: DONE / PARTIAL / NOT DONE
- Production domain/secrets checklist: DONE / PARTIAL / NOT DONE
- Docs/changelog/test cases update: DONE / PARTIAL / NOT DONE

## Files Changed
### Backend
- ...

### Frontend
- ...

### Database
- ...

### Docs
- ...

## Commands Run
```bash
dotnet build ...
npm run build
```

## Runtime Tests
- ...

## Notes / Risks
- ...
```

---

## Definition of Done

Chỉ coi là hoàn thành khi:

- Backend build pass.
- Frontend build pass nếu có đụng frontend/config.
- UC-97 revoke session hoạt động trên DB thật/staging.
- News HTML được sanitize trước khi lưu DB.
- Upload SVG/HTML/JS bị chặn.
- Production config không chứa secret thật.
- Docs/checklist/changelog được cập nhật đúng trạng thái thật.
- Không phá login/SSO/RBAC/session flow hiện có.
