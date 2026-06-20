# PEMS Auth & News Security — TODO triển khai tiếp

> Mục tiêu: Tổng hợp các công việc còn lại sau phase Auth Hardening.  
> Phạm vi: Không làm lại Core Auth, không phá Dual Portal, không bật FEID UI, không sửa lại RBAC redirect đã fix.  
> Trọng tâm: News backend sanitize, UC-97 revoke session, upload guard, runtime test, cleanup session và production checklist.

---

## 0. Trạng thái hiện tại

Core Auth của PEMS đã đạt các điểm chính:

```text
- JWT access token.
- Refresh token lưu SHA-256 hash, không lưu plain text.
- Refresh token rotation mỗi lần refresh.
- DB-backed session trong user_sessions.
- Logout revoke đúng session.
- Đổi role / reset password / đổi password có revoke session.
- SessionValidationMiddleware kiểm tra session + user + role ACTIVE realtime.
- Production không lộ stackTrace.
- CORS theo config, không AllowAnyOrigin.
- HTTPS redirect đã bật.
- Frontend đã sanitize render-time cho News HTML.
```

Phase tiếp theo chỉ xử lý các TODO còn lại để tăng độ an toàn khi triển khai production.

---

## 1. Danh sách TODO theo ưu tiên

| Ưu tiên | TODO | Mục tiêu |
|---|---|---|
| P0 | Backend sanitize HTML cho News bằng Ganss.Xss | Chặn HTML độc trước khi lưu DB |
| P0 | Implement revoke session cho UC-97 ManageAccountStatus | Khóa/deactivate user phải revoke session ngay |
| P1 | Kiểm tra/chặn upload SVG/HTML/JS | Ngăn XSS qua file upload |
| P1 | Chạy runtime test auth/session/security headers/XSS | Build pass chưa đủ, cần test DB thật |
| P1 | Lên lịch cleanup expired/revoked sessions | Tránh user_sessions phình to |
| P2 | Nâng frontend sanitizer sang DOMPurify | An toàn hơn sanitizer hand-rolled |
| P2 | Refresh token reuse-detection nâng cao | Phát hiện token cũ bị dùng lại |
| P2 | Production secrets/domain checklist | Bắt buộc trước deploy thật |

---

# TODO 1 — Backend sanitize HTML cho News bằng Ganss.Xss

## 1.1 Mục tiêu

Hiện frontend đã sanitize HTML khi render News, nhưng backend vẫn cần sanitize trước khi lưu để tránh dữ liệu độc nằm trong database.

Flow mong muốn:

```text
Input HTML từ editor
↓
Backend sanitize bằng Ganss.Xss
↓
Lưu HTML sạch vào news_content_sections.section_body_html
↓
Tạo section_body_text từ HTML đã sanitize để search/preview
↓
Frontend vẫn sanitize lần nữa khi render
```

## 1.2 File cần kiểm tra

```text
backend/PEMS.Application/News/**
backend/PEMS.Application/Common/Interfaces/**
backend/PEMS.Infrastructure/Services/**
backend/PEMS.Infrastructure/DependencyInjection.cs
backend/PEMS.Domain/Entities/NewsContentSection.cs
```

Search keyword:

```text
section_body_html
SectionBodyHtml
NewsContentSection
AddMultilingualNews
EditNews
CreateNewsContentSection
UpdateNewsContentSection
```

## 1.3 Cài package

```bash
dotnet add backend/PEMS.Infrastructure package HtmlSanitizer
```

Hoặc nếu package name trong repo/NuGet dùng tên khác:

```bash
dotnet add backend/PEMS.Infrastructure package Ganss.Xss
```

## 1.4 Tạo interface Application

Tạo file:

```text
backend/PEMS.Application/Common/Interfaces/IHtmlSanitizerService.cs
```

Nội dung:

```csharp
namespace PEMS.Application.Common.Interfaces;

public interface IHtmlSanitizerService
{
    string Sanitize(string? html);
    string ToPlainText(string? sanitizedHtml);
}
```

## 1.5 Implement Infrastructure

Tạo file:

```text
backend/PEMS.Infrastructure/Services/HtmlSanitizerService.cs
```

Yêu cầu:

```text
- Dùng Ganss.Xss HtmlSanitizer.
- Dùng allowlist tag/attribute rõ ràng.
- Chặn script/iframe/object/embed/form/input/button nếu chưa cần.
- Chặn event attribute: onclick/onerror/onload...
- Chặn URL scheme nguy hiểm: javascript:, vbscript:, data:text/html.
- Không cho raw img tự do nếu PEMS đã dùng section_image_file_id.
```

Allowlist đề xuất:

```text
Tags:
p, br, strong, b, em, i, u, s,
ul, ol, li,
h2, h3, h4,
blockquote,
a, span

Attributes:
href, title, target, rel, class
```

Không khuyến khích cho phép:

```text
img, iframe, style, script, svg, object, embed, form, input, button
```

Lý do: PEMS đã có file upload và `section_image_file_id`, nên ảnh nên đi qua bảng `files`, không để người dùng tự nhúng `<img src="...">`.

## 1.6 Đăng ký DI

Trong file đăng ký service:

```csharp
services.AddScoped<IHtmlSanitizerService, HtmlSanitizerService>();
```

## 1.7 Áp dụng vào News handlers

Logic bắt buộc trước khi lưu:

```csharp
var sanitizedHtml = _htmlSanitizer.Sanitize(section.SectionBodyHtml);
var plainText = _htmlSanitizer.ToPlainText(sanitizedHtml);

newsSection.SectionBodyHtml = sanitizedHtml;
newsSection.SectionBodyText = plainText;
```

Không lưu trực tiếp HTML từ request.

## 1.8 Test case

Input:

```html
<p>Hello <strong>PEMS</strong></p>
<script>alert(1)</script>
<img src=x onerror=alert(1)>
<a href="javascript:alert(1)">Click</a>
<iframe src="https://evil.com"></iframe>
```

Kết quả mong muốn:

```text
- Giữ được <p>, <strong>.
- Xóa <script>.
- Xóa <iframe>.
- Xóa onerror.
- Xóa href="javascript:...".
- section_body_text chỉ còn plain text sạch.
```

## 1.9 Acceptance criteria

```text
[ ] Backend build pass.
[ ] News create/update không lưu script vào DB.
[ ] News create/update không lưu event attribute onerror/onclick/onload.
[ ] javascript: URL bị loại.
[ ] Nội dung format cơ bản vẫn giữ.
[ ] section_body_text được tạo từ HTML đã sanitize.
[ ] Frontend render News vẫn hoạt động.
[ ] Docs/changelog cập nhật.
```

---

# TODO 2 — Implement revoke session cho UC-97 ManageAccountStatus

## 2.1 Mục tiêu

Khi account bị khóa hoặc deactivate, session phải bị revoke ngay, không chỉ chờ middleware chặn ở request sau.

## 2.2 File cần kiểm tra

```text
backend/PEMS.Application/Accounts/**
backend/PEMS.Application/Authentication/**
backend/PEMS.Application/Common/Interfaces/**
backend/PEMS.Domain/Entities/User.cs
backend/PEMS.Domain/Entities/UserSession.cs
backend/PEMS.Infrastructure/Services/SessionService.cs
```

Search:

```text
ManageAccountStatusCommand
ManageAccountStatusCommandHandler
UpdateUserStatusCommand
LockUserCommand
DeactivateUserCommand
RevokeAllActiveSessionsAsync
```

## 2.3 Rule nghiệp vụ

Khi user status đổi sang:

```text
INACTIVE
LOCKED
```

thì phải:

```text
- Update user.Status.
- Revoke toàn bộ active sessions của user.
- Ghi security event.
- Ghi audit log nếu project có.
- Trả response thành công rõ ràng.
```

Khi user được mở khóa hoặc active lại:

```text
ACTIVE
```

không restore session cũ. User phải login lại.

## 2.4 Logic gợi ý

```csharp
await _userRepository.UpdateStatusAsync(userId, newStatus, cancellationToken);

if (newStatus is UserStatus.Inactive or UserStatus.Locked)
{
    await _sessionService.RevokeAllActiveSessionsAsync(
        userId,
        reason: "ACCOUNT_DEACTIVATED",
        cancellationToken);
}

await _securityEventService.WriteAsync(...);
```

## 2.5 Acceptance criteria

```text
[ ] UC-97 không còn NotImplementedException.
[ ] Đổi user sang LOCKED thì tất cả session active bị revoked.
[ ] Đổi user sang INACTIVE thì tất cả session active bị revoked.
[ ] User bị khóa gọi protected API nhận 401/403 phù hợp.
[ ] User bị khóa dùng refresh token cũ không refresh được.
[ ] User active lại phải login lại.
[ ] Có security event/audit log.
[ ] Backend build pass.
```

---

# TODO 3 — Kiểm tra và chặn upload SVG/HTML/JS

## 3.1 Mục tiêu

Ngăn XSS hoặc file độc qua upload.

Cần chặn nếu chưa xử lý kỹ:

```text
.svg
.html
.htm
.js
.mjs
.exe
.bat
.cmd
.ps1
```

Đặc biệt `image/svg+xml` có thể chứa script, nên nếu chưa sanitize SVG thì phải chặn SVG.

## 3.2 File cần kiểm tra

```text
backend/PEMS.Api/Filters/FileUploadValidationFilter.cs
backend/PEMS.Application/Common/Interfaces/IFileValidationService.cs
backend/PEMS.Infrastructure/Services/FileValidationService.cs
backend/PEMS.Application/Files/**
backend/PEMS.Application/News/**
backend/PEMS.Application/Gallery/**
```

## 3.3 Rule đề xuất

Cho phép:

```text
image/jpeg
image/png
image/webp
```

Chặn:

```text
image/svg+xml
text/html
application/javascript
text/javascript
application/x-msdownload
```

Kiểm tra tối thiểu:

```text
- Extension.
- MIME type.
- File size.
- Magic bytes nếu service có.
```

## 3.4 Acceptance criteria

```text
[ ] Upload .svg bị chặn.
[ ] Upload .html/.js bị chặn.
[ ] Upload file đổi đuôi nhưng MIME nguy hiểm bị chặn.
[ ] Upload png/jpg/webp hợp lệ vẫn chạy.
[ ] Error message rõ ràng.
[ ] Backend build pass.
```

---

# TODO 4 — Chạy runtime test trên môi trường có DB

## 4.1 Auth/session

```text
[ ] Login success tạo row user_sessions.
[ ] refresh_token_hash không phải raw token.
[ ] Refresh hợp lệ cấp access token mới + refresh token mới.
[ ] Refresh token cũ sau rotation bị 401.
[ ] Logout xong gọi protected API bị 401.
[ ] Logout xong refresh token cũ không dùng được.
[ ] User INACTIVE/LOCKED bị chặn.
[ ] Role inactive/soft-delete bị chặn.
```

## 4.2 Error handling

```text
[ ] Production 500 không có stackTrace.
[ ] Production 500 có errorCode INTERNAL_SERVER_ERROR.
[ ] Validation sai trả 400.
[ ] Wrong portal/campus mismatch vẫn trả 403, không phải 401.
```

## 4.3 CORS/security headers

```text
[ ] Origin hợp lệ gọi API được.
[ ] Origin lạ bị chặn.
[ ] Response có X-Content-Type-Options: nosniff.
[ ] Response có X-Frame-Options: DENY.
[ ] Swagger dev không bị CSP làm hỏng.
```

## 4.4 XSS News

```text
[ ] <script>alert(1)</script> không chạy.
[ ] <img src=x onerror=alert(1)> không chạy.
[ ] <a href="javascript:alert(1)"> bị loại href.
[ ] HTML hợp lệ vẫn hiển thị đúng format.
```

---

# TODO 5 — Lên lịch cleanup expired/revoked user_sessions

## 5.1 Mục tiêu

Tránh bảng `user_sessions` phình to theo thời gian.

Đã có script:

```text
database/scripts/cleanup_expired_user_sessions.sql
```

Cần lên lịch chạy.

## 5.2 Phương án

### Phương án A — MySQL Event Scheduler

```sql
SET GLOBAL event_scheduler = ON;

CREATE EVENT IF NOT EXISTS ev_cleanup_expired_user_sessions
ON SCHEDULE EVERY 1 DAY
DO
  DELETE FROM user_sessions
  WHERE expires_at < UTC_TIMESTAMP()
    AND revoked_at IS NOT NULL;
```

### Phương án B — Cron job

Chạy script mỗi ngày bằng cron/server task.

### Phương án C — Background job trong backend

Nếu project đã có Hangfire/Quartz/BackgroundService thì thêm job. Nếu chưa có, không thêm framework nặng chỉ vì việc này.

## 5.3 Acceptance criteria

```text
[ ] Có quyết định dùng MySQL Event / Cron / Background job.
[ ] Script chạy không xóa login_logs/security_events.
[ ] Script chỉ xóa session expired + revoked.
[ ] Có log hoặc ghi chú số bản ghi đã dọn nếu dùng job.
[ ] DATABASE_DEPLOYMENT.md cập nhật cách chạy.
```

---

# TODO 6 — Nâng frontend sanitizeHtml sang DOMPurify

## 6.1 Mục tiêu

Hiện frontend dùng sanitizer hand-rolled bằng DOMParser. Có thể dùng tạm, nhưng DOMPurify là thư viện chuyên dụng hơn.

## 6.2 Cài package

```bash
cd frontend/pems-react
npm install dompurify
npm install -D @types/dompurify
```

## 6.3 Sửa util

File:

```text
frontend/pems-react/src/shared/security/sanitizeHtml.ts
```

Gợi ý:

```ts
import DOMPurify from "dompurify";

export function sanitizeHtml(input: string | null | undefined): string {
  if (!input) return "";

  return DOMPurify.sanitize(input, {
    USE_PROFILES: { html: true },
    FORBID_TAGS: ["script", "iframe", "object", "embed"],
    FORBID_ATTR: ["onerror", "onclick", "onload", "onmouseover"]
  });
}
```

## 6.4 Acceptance criteria

```text
[ ] npm install thành công.
[ ] npm run build pass.
[ ] News render không lỗi.
[ ] XSS test vẫn pass.
```

---

# TODO 7 — Refresh token reuse-detection nâng cao

## 7.1 Mục tiêu

Hiện refresh token rotation đã có: mỗi lần refresh update hash mới trên cùng session, token cũ không dùng lại được. Tuy nhiên chưa có chain để phát hiện token reuse nâng cao.

Đây là optional security nâng cao.

## 7.2 Khi nào cần làm

Chỉ làm nếu hệ thống yêu cầu security cao hơn:

```text
- Phát hiện token cũ bị dùng lại sau rotation.
- Revoke toàn bộ session nếu nghi token bị đánh cắp.
- Ghi security event TOKEN_REUSE_DETECTED.
```

## 7.3 Cách làm gợi ý

Thêm bảng hoặc field để lưu token family/rotation chain:

```text
refresh_token_family_id
previous_refresh_token_hash
rotated_at
reused_at
```

Hoặc tạo bảng:

```text
refresh_token_rotations
```

Nếu token cũ xuất hiện lại:

```text
- Ghi TOKEN_REUSE_DETECTED.
- Revoke toàn bộ session trong family.
- Bắt user login lại.
```

## 7.4 Acceptance criteria

```text
[ ] Token cũ sau rotation bị phát hiện reuse.
[ ] Security event TOKEN_REUSE_DETECTED được ghi.
[ ] Session family bị revoke.
[ ] Không phá refresh flow hiện tại.
```

---

# TODO 8 — Production secrets/domain checklist

## 8.1 Mục tiêu

Không commit secret thật vào repo. Production phải override bằng environment variables hoặc secret manager.

## 8.2 Cần kiểm tra

```text
JwtSettings.SecretKey
ConnectionStrings.DefaultConnection
SMTP password
GoogleAuth.ClientId
GoogleAuth.ClientSecret nếu có
Feid.ClientSecret nếu sau này dùng
Storage provider key
```

## 8.3 Domain

Placeholder hiện tại:

```text
https://pems.fpt.edu.vn
```

Trước deploy phải thay bằng domain thật:

```text
Frontend domain thật
Backend/API domain thật
Google OAuth authorized origin/redirect URI
CORS AllowedOrigins
VITE_API_BASE_URL
AllowedHosts
```

## 8.4 Acceptance criteria

```text
[ ] appsettings.Production.json không chứa secret thật.
[ ] Secret lấy từ env/secret manager.
[ ] CORS domain đúng domain thật.
[ ] Frontend env gọi đúng HTTPS API.
[ ] Google OAuth config đúng domain thật.
[ ] Build production pass.
```

---

# 9. Thứ tự triển khai đề xuất

```text
1. Backend sanitize HTML cho News bằng Ganss.Xss.
2. Implement UC-97 ManageAccountStatus revoke session.
3. Kiểm tra/chặn upload SVG/HTML/JS.
4. Chạy runtime test auth/session/security headers/XSS.
5. Lên lịch cleanup user_sessions.
6. Nâng frontend sanitizer sang DOMPurify.
7. Production secrets/domain checklist.
8. Optional: refresh token reuse-detection nâng cao.
```

---

# 10. Checklist tổng cuối phase

```text
[ ] Backend sanitize News HTML trước khi lưu.
[ ] Frontend vẫn sanitize News khi render.
[ ] UC-97 khóa/deactivate user revoke session.
[ ] File upload chặn SVG/HTML/JS.
[ ] Runtime test auth/session pass.
[ ] Runtime test XSS pass.
[ ] Runtime test CORS/security headers pass.
[ ] Cleanup session được lên lịch.
[ ] Production secrets không nằm trong repo.
[ ] Domain production đã thay placeholder.
[ ] Docs/changelog cập nhật.
[ ] Backend build pass.
[ ] Frontend build pass nếu có sửa frontend.
```

---

# 11. Kết luận

Sau khi hoàn thành các TODO trên, PEMS sẽ có cơ chế bảo mật hoàn chỉnh hơn:

```text
Dual Portal + SSO-first
+ JWT access token
+ refresh token hash & rotation
+ DB-backed session
+ realtime session validation
+ revoke session khi đổi security context
+ backend/frontend XSS hardening
+ CORS/HTTPS/security headers
+ cleanup session định kỳ
+ production secrets/domain chuẩn
```

Không thay đổi nền tảng auth hiện tại.  
Chỉ hoàn thiện các điểm còn thiếu trước production.
