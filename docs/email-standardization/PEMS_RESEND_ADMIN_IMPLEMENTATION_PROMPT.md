# PEMS — Prompt triển khai Resend do ADMIN cấu hình

## Mục tiêu

Thêm `ResendEmailService` vào backend PEMS trên nhánh `Dev`.

Yêu cầu bắt buộc:

- Giữ nguyên `IEmailService`.
- Giữ nguyên template renderer, TO/CC/BCC, Reply-To, attachment và lịch sử email.
- Khi `Email__Provider=Resend`, DI phải dùng `ResendEmailService`.
- API key và cấu hình Resend do role `ADMIN` nhập tại trang **Cấu hình API tích hợp**.
- Không trả API key về frontend, không ghi key vào log, audit hoặc source code.
- Không tạo bảng mới; tái sử dụng `api_configurations`.

---

## Kế hoạch triển khai

### 1. Backend Resend transport

Thêm package Resend vào:

```text
backend/PEMS.Infrastructure/PEMS.Infrastructure.csproj
```

Tạo:

```text
backend/PEMS.Infrastructure/Email/ResendEmailService.cs
```

`ResendEmailService` phải implement nguyên interface hiện tại:

```csharp
IEmailService
```

Luồng xử lý:

```text
OutboundEmail
→ EmailRecipientValidator
→ EmailRecipientPolicyEnforcer
→ đọc cấu hình Resend ACTIVE từ api_configurations
→ giải mã API key
→ map TO/CC/BCC/Reply-To/body/attachment/header
→ gọi Resend HTTPS API
→ trả EmailDeliveryResult
```

Quy tắc kết quả:

- Resend trả email ID → `EmailDeliveryResult.Sent(providerMessageId)`.
- Thiếu/disabled/misconfigured → `Failed`, không fallback âm thầm sang SMTP trong Production.
- Provider lỗi → `Failed` với message an toàn.
- Không log body, OTP, token, URL xác nhận, người nhận đầy đủ hoặc API key.

---

### 2. Cấu hình Resend do ADMIN quản lý

Tái sử dụng:

```text
api_configurations
```

Cấu hình cố định:

```text
api_code      = RESEND_EMAIL_DELIVERY
provider_name = Resend
purpose       = EMAIL_DELIVERY
base_url      = https://api.resend.com
auth_type     = BEARER
```

Lưu:

- API key đã mã hóa → `bearer_token_encrypted`.
- `fromEmail`, `fromName`, `replyToEmail`, `replyToName` → `settings_json`.
- Trạng thái → `status`.
- Kết quả test → `last_test_status`, `last_tested_at`, `last_test_message`.

Dùng `ISecretProtector` hiện có để mã hóa/giải mã API key.

API key:

- Tạo mới: bắt buộc nhập.
- Chỉnh sửa: để trống thì giữ nguyên key cũ.
- Nhập key mới: thay thế key cũ và reset trạng thái test.
- API list/detail chỉ trả `hasCredential=true/false`.
- Tuyệt đối không trả ciphertext hoặc plaintext key.

---

### 3. API ADMIN

Thêm command/validator/handler:

```text
backend/PEMS.Application/ApiIntegrations/Commands/UpsertResendConfig/
```

Thêm endpoint:

```http
POST /api/api-integrations/email-delivery/resend
```

Chỉ `ADMIN` được:

- Tạo/cập nhật cấu hình.
- Test kết nối.
- Bật/tắt Resend.

`HO` chỉ được xem trạng thái như cơ chế hiện tại.

Mở rộng `ApiIntegrationMapper`:

- Thêm `EMAIL_DELIVERY` vào danh sách cấu hình DB-managed.
- `HasCredential` của Resend kiểm tra `BearerTokenEncrypted`.

Mở rộng test kết nối:

```text
TestApiIntegrationCommandHandler
```

Với `EMAIL_DELIVERY`:

- Giải mã API key.
- Gửi email test đến email của ADMIN đang đăng nhập.
- Subject: `PEMS — Kiểm tra kết nối Resend`.
- Thành công khi Resend trả email ID.
- Không lưu hoặc trả API key trong log/error.

Chỉ cho bật cấu hình sau khi test thành công.

---

### 4. Frontend Admin

Cập nhật:

```text
frontend/pems-react/src/pages/dashboard/apis/ApiManagement.tsx
frontend/pems-react/src/features/api-management/types/apiManagement.types.ts
frontend/pems-react/src/features/api-management/api/apiManagementApi.ts
frontend/pems-react/src/shared/api/endpoints.ts
```

Thêm card:

```text
Resend — Gửi email hệ thống
```

Form gồm:

- Tên cấu hình.
- API key (`type=password`).
- From Email.
- From Name.
- Reply-To Email.
- Reply-To Name.
- Timeout.

Quy tắc ô API key:

```text
Tạo mới: bắt buộc.
Chỉnh sửa: luôn trống.
Placeholder: "Đã cấu hình — để trống để giữ nguyên".
Không có nút xem/copy key cũ.
```

---

### 5. Dependency Injection

Trong:

```text
backend/PEMS.Infrastructure/DependencyInjection.cs
```

Chọn implementation theo:

```text
Email:Provider
```

Logic:

```csharp
if (string.Equals(
    configuration["Email:Provider"],
    "Resend",
    StringComparison.OrdinalIgnoreCase))
{
    services.AddScoped<IEmailService, ResendEmailService>();
}
else
{
    services.AddScoped<IEmailService, EmailService>();
}
```

Không thay đổi các caller hiện tại vì toàn bộ hệ thống tiếp tục gọi `IEmailService`.

Railway chỉ cần:

```env
Email__Provider=Resend
Security__SecretProtectionKey=<base64-32-bytes>
```

Không lưu `Resend__ApiToken` trên Railway vì key do ADMIN nhập và được mã hóa trong DB.

---

## Không được thay đổi

- Không đổi contract `IEmailService`.
- Không sửa business flow của system email/manual email.
- Không đổi template renderer.
- Không gộp TO/CC/BCC.
- Không bỏ recipient policy.
- Không ghi `SENT` trước khi Resend chấp nhận request.
- Không tạo bảng mới.
- Không commit API key.
- Không fallback sang SMTP khi Resend được chọn nhưng cấu hình lỗi trong Production.

---

## Kiểm tra

### Backend

- Build toàn bộ backend.
- Unit test mapping TO/CC/BCC/Reply-To.
- Unit test attachment và HTML/plain text.
- Unit test API key không xuất hiện trong DTO/log.
- Integration test tạo/sửa config bởi ADMIN.
- Role khác cập nhật config → `403`.
- Để trống key khi edit → giữ key cũ.
- Key mới → reset trạng thái test.
- Resend trả ID → `SENT` và lưu `provider_message_id`.
- Resend lỗi → `FAILED`, không ghi `sent_at`.
- Config thiếu/disabled trong Production → fail-closed.

### Frontend

- ADMIN thấy card và form Resend.
- HO chỉ xem, không sửa/test/bật/tắt.
- Ô API key luôn là password và không hiện lại key cũ.
- Test kết nối hiển thị kết quả rõ ràng.
- Lint, test và build xanh.

---

## Điều kiện hoàn thành

- Gửi email từ PEMS xuất hiện trong Resend Dashboard.
- `Last used` của API key được cập nhật.
- System email và manual email đều đi qua `ResendEmailService`.
- TO/CC/BCC, Reply-To, attachment và lịch sử email vẫn đúng.
- Database chỉ ghi `SENT` sau khi Resend trả email ID.
- Không có secret Resend trong Git, response API, frontend state, log hoặc audit.
