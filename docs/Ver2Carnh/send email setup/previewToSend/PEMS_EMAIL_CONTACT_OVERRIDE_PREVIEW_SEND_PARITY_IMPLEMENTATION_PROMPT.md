# PEMS — Prompt triển khai Contact Override có kiểm soát và đồng bộ Preview → Send

## 0. Thông tin thực hiện

- Repository: `quangthoai04/PEMS`
- Baseline cần kiểm tra trước khi sửa: commit `adcae824dae374882b14d3ef747a1150ac5e0315`
- Phạm vi: email template contact block, màn soạn/xem trước email và các luồng gửi email có cho phép người dùng chỉnh nội dung.
- Mục tiêu thay đổi: cho phép người gửi thay đổi thông tin liên hệ theo từng email nhưng không cho sửa trực tiếp HTML của khối hệ thống; đồng thời bảo đảm màn quản lý mẫu, màn xem trước và email thực gửi dùng cùng logic.

> Không bắt đầu code ngay. Trước tiên phải kiểm tra branch, HEAD, WIP, schema hiện tại, các handler gửi email và test hiện có. Không ghi đè thay đổi chưa commit của người khác.

---

## 1. Bối cảnh và lỗi hiện tại

Hệ thống đang có hai cách hiển thị khối `{{contactInformationBlock}}`:

1. Màn quản lý template dùng `EmailContactHtmlRenderer.SampleBlock(...)`, nên hiển thị bảng mẫu theo đúng policy đã cấu hình.
2. Endpoint xem trước email trước khi gửi dùng `EmailContactHtmlRenderer.DisabledBlock(...)`, nên chỉ hiển thị khung nét đứt với nội dung hệ thống sẽ tự điền liên hệ.

Các màn sau đang gọi endpoint preview chung rồi đưa `bodyHtml` vào trình soạn thảo:

- `frontend/pems-react/src/features/delegations/components/LogisticsRequestSection.tsx`
- `frontend/pems-react/src/features/delegations/components/ParticipantInvitationSection.tsx`
- `frontend/pems-react/src/pages/dashboard/departments/SharedDashboardView.tsx`

Khi người dùng bấm gửi, frontend gửi lại HTML đang hiển thị dưới dạng `emailOverride.useEditedContent = true`.

Backend sau đó:

- xem nội dung là `SystemEmailContent.AuthoredByUser`;
- resolve contact thật theo policy;
- thêm trusted blocks vào cuối nội dung;
- có nguy cơ giữ cả khung preview giả và chèn thêm bảng contact thật.

Hậu quả:

- preview không giống cấu hình đã lưu;
- preview không giống email cuối;
- có nguy cơ contact block bị chèn trùng;
- người dùng không biết email cuối dùng đầu mối nào;
- `Reply-To` có thể không được thể hiện rõ trong preview.

Commit `adcae824...` chỉ cập nhật phần reset canonical SQL cho các bảng draft cũ. Không được kết luận lỗi trên do commit SQL này; phải kiểm tra toàn bộ chuỗi code hiện tại ở đúng HEAD.

---

## 2. Mục tiêu nghiệp vụ

Triển khai khối liên hệ theo nguyên tắc:

1. Khối HTML vẫn do backend quản lý.
2. Người dùng không sửa trực tiếp bảng HTML, placeholder hoặc system marker.
3. Người gửi được phép thay đổi contact cho riêng email đang gửi bằng form có cấu trúc.
4. Thay đổi theo từng email không làm thay đổi cấu hình chung của template.
5. Preview phải hiển thị đúng contact mà email cuối sẽ sử dụng.
6. `Reply-To`, thông tin hiển thị và contact source phải đồng bộ.
7. Backend phải xác thực lại toàn bộ dữ liệu khi gửi; không tin dữ liệu hiển thị từ frontend.
8. Không cho per-email override vượt qua capability của template.
9. Email cuối chỉ có đúng một contact block.
10. Các luồng gửi không mở modal chỉnh sửa vẫn tiếp tục dùng policy template hiện tại.

---

## 3. Quyết định thiết kế bắt buộc

### 3.1 Không cho sửa HTML trực tiếp

Không đưa bảng contact vào vùng Quill/contenteditable.

Không cho frontend gửi:

- HTML của contact block;
- `{{contactInformationBlock}}`;
- contact markers;
- system action markers;
- HTML tự tạo mang danh nghĩa Host/campus/department contact.

Frontend chỉ gửi dữ liệu cấu trúc. Backend là nơi duy nhất render contact block bằng `EmailContactHtmlRenderer`.

### 3.2 Ba chế độ contact cho một email

```text
TEMPLATE_DEFAULT
SYSTEM_USER
MANUAL
```

#### `TEMPLATE_DEFAULT`

- Dùng policy đã resolve từ `email_contact_policies`.
- Dùng source hiện tại: `HOST`, `SENDER`, `HOST_THEN_SENDER`, `CAMPUS_DEFAULT`, `DEPARTMENT_DEFAULT` hoặc `SUPPORT_CONTACT`.
- Không có override dữ liệu.

#### `SYSTEM_USER`

- Người gửi chọn một user hợp lệ trong hệ thống.
- Frontend chỉ gửi `userId`.
- Backend tự đọc tên, email, phone, department, campus và trạng thái user.
- Không nhận các field tên/email/phone do frontend tự điền cho chế độ này.
- User phải `ACTIVE` và thuộc đúng phạm vi được phép.

#### `MANUAL`

Dùng khi contact không có tài khoản PEMS.

Cho nhập:

- `displayName`
- `roleLabel`
- `email`
- `phone`
- `departmentName`
- `campusName`
- `replyToMode`
- `reason`

Tất cả là plain text. Không nhận HTML.

### 3.3 Quy tắc theo capability và requirement

| Capability / Requirement | UI | Override |
|---|---|---|
| `UNSUPPORTED` | Không hiện contact block và không hiện nút thay đổi | Cấm |
| Capability supported + `NONE` | Không hiện contact block | Không cho override trong email; muốn bật phải sửa cấu hình template |
| `OPTIONAL` | Hiện block nếu resolve được contact | Cho đổi contact; có thể chọn “Không hiển thị trong email này” |
| `REQUIRED` | Luôn phải có contact khả dụng | Cho đổi contact nhưng không cho tắt block |

Không được dùng per-email override để thêm contact vào các template credential hoặc template bị đánh dấu `UNSUPPORTED`, ví dụ:

- `ACCOUNT_EMAIL_CONFIRMATION`
- `AUTH_PASSWORD_RESET_OTP`
- `VISIT_REQUEST_OTP`
- `VISIT_REMINDER_HOST`

### 3.4 `Reply-To`

Các lựa chọn:

```text
POLICY_DEFAULT
CONTACT
SENDER
NONE
```

Quy tắc:

- `POLICY_DEFAULT`: dùng `ReplyToSource` đã cấu hình.
- `CONTACT`: chỉ hợp lệ nếu contact có email hợp lệ.
- `SENDER`: backend lấy email của người gửi đang đăng nhập.
- `NONE`: không set `Reply-To`.
- Email hiển thị trong block và email dùng cho `Reply-To = CONTACT` phải là cùng một giá trị.
- Không cho frontend truyền header `Reply-To` tùy ý.

### 3.5 Không tạo bảng mới mặc định

Ưu tiên tái sử dụng:

- `sent_emails`
- `sent_email_recipients`
- `audit_logs`
- snapshot body hiện có
- các trường metadata hiện có nếu đã tồn tại

Không tự ý tạo bảng mới trong task này.

Nếu schema hiện tại không có chỗ phù hợp để ghi nhận nguồn override:

1. Vẫn hoàn thành tính năng preview/send an toàn.
2. Ghi audit action bằng cơ chế hiện có.
3. Báo rõ phần provenance nào chưa thể lưu đầy đủ.
4. Dừng và xin quyết định trước khi thêm bảng hoặc thay đổi schema.

---

## 4. Contract đề xuất

Tên có thể điều chỉnh theo convention hiện tại nhưng không thay đổi ý nghĩa.

### 4.1 Backend input

```csharp
public sealed record EmailContactOverrideInput(
    string Mode,
    ulong? UserId,
    string? DisplayName,
    string? RoleLabel,
    string? Email,
    string? Phone,
    string? DepartmentName,
    string? CampusName,
    string? ReplyToMode,
    bool? HideForThisEmail,
    string? Reason);
```

Thêm optional field vào contract gửi email hiện có:

```csharp
EmailContactOverrideInput? ContactOverride
```

Ưu tiên thêm vào model dùng chung của email override/composer thay vì thêm lặp lại vào từng command.

Không thay đổi API route hiện có nếu không cần thiết.

### 4.2 Preview request

Endpoint preview phải nhận đủ context thật:

```csharp
public sealed record EmailContactPreviewContext(
    ulong? VisitInstanceId,
    ulong? CampusId,
    ulong? DepartmentId,
    EmailContactOverrideInput? Override);
```

Không chỉ nhận `templateCode + variables`.

### 4.3 Preview response

Bổ sung dữ liệu riêng cho khối hệ thống:

```ts
type EmailContactPreviewResult = {
  supported: boolean;
  requirement: 'NONE' | 'OPTIONAL' | 'REQUIRED';
  mode: 'TEMPLATE_DEFAULT' | 'SYSTEM_USER' | 'MANUAL';
  source: string | null;
  lockedContactBlockHtml: string | null;
  replyToDisplay: string | null;
  canOverride: boolean;
  canHide: boolean;
  availableModes: string[];
};
```

Response preview tổng:

```ts
{
  subject: string;
  bodyHtml: string;                  // chỉ phần được phép sửa
  lockedActionBlockHtml?: string;
  contact?: EmailContactPreviewResult;
}
```

`bodyHtml` không được chứa:

- contact placeholder;
- disabled contact block;
- contact block HTML thật;
- action block HTML thật;
- system markers.

---

## 5. Backend implementation

## 5.1 Chuẩn hóa override model

Kiểm tra các type hiện tại:

- `EmailOverride`
- `EmailCompose*Input`
- `SystemEmailRequest`
- `EmailContactRequest`
- `EmailContactResolution`
- `PreparedSystemEmail`

Tận dụng type dùng chung. Không tạo ba DTO gần giống nhau cho ba màn.

Đề xuất thêm:

```text
Emails/Contact/EmailContactOverrideInput.cs
Emails/Contact/EmailContactOverrideValidator.cs
```

Chỉ tạo service riêng nếu thực sự có ít nhất hai caller dùng chung. Với ba luồng hiện tại, một validator/resolver dùng chung là hợp lý.

## 5.2 Resolve contact theo override

Mở rộng `IEmailContactResolver` hoặc thêm method rõ nghĩa:

```csharp
Task<EmailContactResolution> ResolveAsync(
    EmailContactRequest request,
    EmailContactOverrideInput? overrideInput,
    CancellationToken cancellationToken);
```

Thứ tự xử lý:

1. Resolve capability.
2. Resolve effective policy.
3. Nếu capability `UNSUPPORTED`:
   - bỏ qua row sai từ DB;
   - cấm override;
   - trả `Requirement = NONE`.
4. Nếu policy `NONE`:
   - cấm override;
   - không render block.
5. Nếu không override hoặc mode `TEMPLATE_DEFAULT`:
   - giữ logic hiện tại.
6. Nếu `SYSTEM_USER`:
   - kiểm tra quyền và phạm vi;
   - query user `ACTIVE`;
   - dựng `EmailContactInformation` từ DB;
   - không dùng các field text do frontend truyền.
7. Nếu `MANUAL`:
   - validate dữ liệu;
   - dựng `EmailContactInformation` từ input đã chuẩn hóa.
8. Nếu `HideForThisEmail = true`:
   - chỉ cho `OPTIONAL`;
   - không cho `REQUIRED`;
   - không render block.
9. Resolve `Reply-To`.
10. Render bằng `EmailContactHtmlRenderer.Render(...)`.

## 5.3 Authorization

Không chỉ dựa vào frontend.

Điều kiện nền:

- Actor phải có quyền gửi email ở luồng hiện tại.
- Actor phải là đúng người đang thực hiện action.
- Không cho thay contact bằng arbitrary user ID ngoài phạm vi.

Phạm vi tối thiểu:

### Host gửi hậu cần hoặc lời mời

- Có thể dùng chính Host.
- Có thể chọn user `ACTIVE` thuộc cùng campus của `visitInstanceId`.
- Với department-related mail, có thể chọn leader/staff thuộc department liên quan.
- Không được chọn user campus khác nếu không có quyền HO/Admin.

### Department Leader

- Có thể chọn user `ACTIVE` trong department của mình.
- Không được chọn user phòng ban khác.

### HO/Admin

- Có thể chọn user `ACTIVE` trong phạm vi hệ thống theo permission hiện có.
- Không tự mở rộng quyền chỉ vì role code là HO/Admin; dùng permission service/guard hiện tại.

### Manual contact

Chỉ cho actor đã có quyền gửi email trong flow.

Nếu cần thêm guard dùng chung, đặt tên cụ thể, ví dụ:

```text
IEmailContactOverrideAuthorizer
EmailContactOverrideAuthorizer
```

Không nhúng authorization vào frontend.

## 5.4 Validation

### Chung

- Mode bắt buộc và thuộc enum cho phép.
- Không chấp nhận field không phù hợp với mode.
- Không chấp nhận HTML tag trong field text.
- Trim toàn bộ dữ liệu.
- Áp dụng giới hạn độ dài hợp lý theo column/contract hiện có.
- Không log toàn bộ contact input ở lỗi.

### `SYSTEM_USER`

- `UserId` bắt buộc.
- User tồn tại.
- User `ACTIVE`.
- Có ít nhất email hoặc phone khả dụng.
- Phạm vi đúng.
- Không nhận manual name/email/phone để tránh giả mạo dữ liệu DB.

### `MANUAL`

- `DisplayName` bắt buộc.
- `RoleLabel` bắt buộc hoặc dùng label an toàn đã định nghĩa rõ.
- Có ít nhất một trong `Email` hoặc `Phone`.
- Email dùng validator hiện có.
- Phone dùng validator/normalizer hiện có; không tạo regex mới nếu dự án đã có validator.
- `ReplyToMode = CONTACT` yêu cầu email hợp lệ.
- `Reason` bắt buộc cho manual override.
- Department/campus là text mô tả, không được dùng để vượt authorization.

### Field visibility

Nếu cho phép toggle theo từng email:

- Chỉ được giảm/giữ field hiển thị từ policy hiện tại, trừ khi yêu cầu nghiệp vụ xác nhận cho phép mở thêm.
- Không cho tắt cả email và phone khi block vẫn hiển thị.
- `REQUIRED` không cho ẩn block.
- `Reply-To = CONTACT` không cho ẩn email.

Ưu tiên đơn giản: giai đoạn đầu chỉ cho đổi người/contact; giữ nguyên field visibility theo template policy.

## 5.5 Preview đúng dữ liệu thật

Sửa `PreviewEmailTemplateQueryHandler`.

Không dùng `DisabledBlock(...)` cho modal gửi email có context thật.

Phân biệt hai loại preview:

### Template management preview

- Không có visit context.
- Tiếp tục dùng `SampleBlock(...)`.
- Dùng đúng effective policy đang chỉnh.
- Mục tiêu là cho admin thấy layout mẫu.

### Operational send preview

- Có `visitInstanceId/campusId/departmentId`.
- Resolve contact thật hoặc override thật.
- Trả `lockedContactBlockHtml`.
- Không nhét khối contact vào `bodyHtml` editable.

Với operational preview:

1. Render template/editor content với trusted contact block là empty string để placeholder không bị sót.
2. Trả block thật riêng trong response.
3. Action block tiếp tục trả riêng.
4. Frontend hiển thị block thật ở vùng read-only ngay dưới nội dung editable.
5. Khi gửi, backend resolve lại từ DB; không tái sử dụng HTML preview.

## 5.6 Send path

`SystemEmailDispatcher` vẫn là nơi cuối cùng resolve contact và render.

Mở rộng `SystemEmailRequest`:

```csharp
public EmailContactOverrideInput? ContactOverride { get; init; }
```

Trong `PrepareAsync`:

```text
request + ContactScope + ContactOverride
→ resolver
→ policy/capability/authorization/validation
→ contact resolution
→ contact HTML
→ Reply-To
→ renderer
→ sent email
```

Không cho caller tự truyền `ContactInformationBlock` trong `TrustedBlocks`.

Nếu caller truyền thủ công key này, từ chối hoặc ghi đè bằng block do dispatcher tạo. Chọn một behavior thống nhất và có test.

## 5.7 Authored content

Giữ nguyên nguyên tắc:

- người dùng chỉ chỉnh subject/body;
- action/contact/setup blocks do backend quản lý;
- authored body không được chứa trusted placeholders;
- system blocks được append theo thứ tự chuẩn.

Xác định thứ tự cuối cùng rõ ràng:

```text
Editable body
→ Contact block
→ Action block
```

Hoặc:

```text
Editable body
→ Action block
→ Contact block
```

Chọn thứ tự phù hợp nghiệp vụ hiện tại và dùng thống nhất ở preview/send. Với email cần người nhận bấm Đồng ý/Từ chối, nên ưu tiên:

```text
Editable body
→ Action block
→ Contact block
```

để contact nằm cuối email như phần hỗ trợ/liên hệ.

Không thay đổi thứ tự âm thầm nếu test hoặc template hiện tại đang phụ thuộc; phải audit trước.

## 5.8 Audit

Ghi nhận tối thiểu:

- actor user ID;
- template code;
- related type/id;
- override mode;
- source cuối cùng;
- selected user ID nếu dùng `SYSTEM_USER`;
- manual override hay không;
- reply-to mode;
- reason nếu có.

Không lưu OTP/token/action URL trong audit.

Không log full email body hoặc dữ liệu nhạy cảm.

Không tạo bảng mới mặc định. Tái sử dụng `AuditLog` hoặc metadata hiện có.

---

## 6. Frontend implementation

## 6.1 `EmailPreviewModal`

Sửa component dùng chung thay vì làm ba modal khác nhau.

Thêm state:

```ts
type ContactOverrideDraft = {
  mode: 'TEMPLATE_DEFAULT' | 'SYSTEM_USER' | 'MANUAL';
  selectedUserId?: number;
  displayName?: string;
  roleLabel?: string;
  email?: string;
  phone?: string;
  departmentName?: string;
  campusName?: string;
  replyToMode: 'POLICY_DEFAULT' | 'CONTACT' | 'SENDER' | 'NONE';
  hideForThisEmail?: boolean;
  reason?: string;
};
```

Hiển thị:

```text
Thông tin liên hệ
[Nguồn hiện tại]
[HTML preview read-only]

[Thay đổi thông tin liên hệ]
```

Khi mở chỉnh:

```text
(●) Theo cấu hình mẫu
( ) Chọn người trong hệ thống
( ) Nhập thủ công
```

Nút:

- `Áp dụng`
- `Hủy`
- `Khôi phục theo cấu hình mẫu`

Không cho sửa HTML của bảng.

## 6.2 Chọn user trong hệ thống

Dùng search component/service hiện có trước khi tạo mới.

Search result chỉ hiển thị user hợp lệ do backend trả về.

Không tải toàn bộ danh sách rồi filter client-side.

Khi chọn:

- frontend giữ `userId`;
- thông tin hiển thị lấy từ response preview mới;
- không tự dựng bảng contact;
- gọi lại preview API với override để backend trả `lockedContactBlockHtml` mới.

## 6.3 Manual form

Fields:

- Họ tên
- Vai trò
- Email
- Số điện thoại
- Phòng ban
- Cơ sở
- Reply-To
- Lý do thay đổi

Validation client chỉ để UX. Backend là nguồn quyết định cuối.

Hiển thị lỗi ngay dưới field.

Không cho bấm gửi nếu preview contact override đang lỗi.

## 6.4 Preview refresh

Mỗi lần thay contact:

1. Không mất subject/body người dùng đang sửa.
2. Chỉ refresh `lockedContactBlockHtml` và contact metadata.
3. Không tự gọi “Khôi phục mẫu gốc”.
4. Không ghi đè content editor.
5. Chống race condition khi người dùng đổi lựa chọn liên tục.
6. Có loading riêng cho contact block, không khóa toàn modal nếu không cần.

## 6.5 Gửi

Payload gửi:

```ts
emailOverride: {
  useEditedContent: true,
  subject,
  bodyHtml,
  attachments,
  contactOverride
}
```

Không gửi `lockedContactBlockHtml`.

Không gửi `Reply-To` header tùy ý.

Khi lỗi contact:

- giữ modal mở;
- giữ subject/body/attachments/contact form;
- hiển thị error code/message phù hợp;
- không tự reset về template default.

## 6.6 Các call site phải cập nhật

### `LogisticsRequestSection.tsx`

- Preview `LOGISTICS_REQUEST_TO_DEPARTMENT`.
- Truyền:
  - `visitInstanceId`
  - `campusId` nếu có
  - `departmentId`
- Cho Host đổi contact trong phạm vi hợp lệ.
- Mặc định dùng `HOST_THEN_SENDER`.

### `ParticipantInvitationSection.tsx`

- Áp dụng cho:
  - `VISIT_PARTICIPANT_INVITATION`
  - `VISIT_STUDENT_INVITATION`
  - `VISIT_DEPARTMENT_LEADER_INVITATION`
- Truyền đúng `visitInstanceId/campusId`.
- Mặc định dùng Host.
- Không cho contact override làm thay đổi người nhận invitation.

### `SharedDashboardView.tsx`

- Áp dụng cho:
  - logistics assignee assignment;
  - department staff assignment/invitation.
- Truyền đúng department/campus context.
- Department Leader chỉ chọn contact trong department hợp lệ.

### Các màn khác

Search toàn repository các call:

```text
previewEmailTemplate
EmailPreviewModal
emailOverride
useEditedContent
SystemEmailContent.AuthoredByUser
ContactScope
```

Lập matrix đầy đủ trước khi sửa. Không chỉ sửa ba file được biết nếu còn call site khác.

---

## 7. Rà soát toàn bộ template

Tạo matrix cho toàn bộ `SystemEmailTemplates.AllCodes`:

| Template | Capability | Requirement | Có preview chỉnh sửa? | Có ContactScope? | Preview đúng? | Send đúng? | Cần sửa |
|---|---|---|---|---|---|---|---|

Phải kiểm tra:

1. Template có support contact block hay không.
2. Policy effective từ DB.
3. Body VI/EN có `{{contactInformationBlock}}` đúng với policy.
4. Caller có truyền đủ `ContactScope`.
5. Preview có context thật.
6. Send handler có đi qua `SystemEmailDispatcher`.
7. Authored mode có chèn đúng một block.
8. Template `NONE/UNSUPPORTED` không hiển thị UI override.
9. `Reply-To` đúng với policy/override.
10. VI và EN đồng nhất.

Không kết luận “toàn bộ mẫu đã đúng” chỉ từ unit test của bốn template smoke hiện tại.

---

## 8. File dự kiến thay đổi

Tên file có thể thay đổi theo codebase thực tế. Không tạo file mới nếu logic phù hợp đã tồn tại.

### Backend

- `backend/PEMS.Application/Common/Interfaces/IEmailTemplateRenderer.cs`
- `backend/PEMS.Application/Emails/Common/SystemEmailDispatcher.cs`
- `backend/PEMS.Application/Emails/Common/SystemEmailContent.cs`
- `backend/PEMS.Infrastructure/Email/EmailTemplateRenderer.cs`
- `backend/PEMS.Application/Emails/Contact/IEmailContactResolver.cs`
- `backend/PEMS.Application/Emails/Contact/EmailContactResolver.cs`
- `backend/PEMS.Application/Emails/Contact/EmailContactHtmlRenderer.cs`
- `backend/PEMS.Application/Emails/Queries/PreviewEmailTemplate/*`
- DTO/input dùng chung của `EmailOverride`
- các handler gửi email liên quan để truyền `ContactScope` và `ContactOverride`
- controller/endpoint search contact candidate nếu chưa có API phù hợp

### Frontend

- `frontend/pems-react/src/features/delegations/components/EmailPreviewModal.tsx`
- `frontend/pems-react/src/features/delegations/components/LogisticsRequestSection.tsx`
- `frontend/pems-react/src/features/delegations/components/ParticipantInvitationSection.tsx`
- `frontend/pems-react/src/pages/dashboard/departments/SharedDashboardView.tsx`
- email API/types dùng chung
- contact candidate search component/API nếu cần

### Tests

- unit tests cho resolver/validator/capability
- integration tests cho preview/send parity
- frontend component tests cho modal
- E2E hoặc real-stack test cho ít nhất một logistics flow và một invitation flow

---

## 9. Test bắt buộc

## 9.1 Backend unit tests

### Capability

- `UNSUPPORTED` từ chối override.
- Policy `NONE` không cho override.
- `OPTIONAL` cho default/system/manual/hide.
- `REQUIRED` cho default/system/manual nhưng cấm hide.

### Validation

- Manual thiếu tên → lỗi.
- Manual thiếu cả email và phone → lỗi.
- Email sai format → lỗi.
- Phone sai format → lỗi.
- `Reply-To = CONTACT` nhưng không có email → lỗi.
- User không `ACTIVE` → lỗi.
- User ngoài scope → forbidden.
- Manual field chứa HTML → encode/validate theo rule đã chọn, không render HTML.
- Không nhận manual fields trong mode `SYSTEM_USER`.

### Renderer

- Contact block HTML encode toàn bộ input.
- Chỉ đúng một block.
- Không có literal `{{contactInformationBlock}}`.
- Không có `DisabledBlock` trong send output.
- `Reply-To` khớp contact cuối.
- Authored content không thể tự chèn contact placeholder.

## 9.2 Backend integration tests

Ít nhất các case:

1. Cấu hình template thay đổi heading/phone/campus → operational preview phản ánh ngay.
2. Preview mặc định và send mặc định resolve cùng contact.
3. Chọn user khác → preview và MIME cuối cùng cùng tên/email/phone.
4. Manual override → preview và MIME cuối cùng cùng dữ liệu.
5. `Reply-To = CONTACT` → MIME header đúng.
6. `Reply-To = SENDER` → MIME header đúng.
7. Required contact không resolve được → không tạo send thành công.
8. Optional contact không resolve được → gửi không có block.
9. Email cuối chỉ có một contact table.
10. Không có câu “Khối thông tin liên hệ — hệ thống điền đầu mối…” trong MIME thật.
11. VI và EN.
12. Override không thay đổi row cấu hình template.
13. Reload modal trả lại default từ policy, không giữ override của email trước.
14. Unauthorized user ID bị từ chối ở backend.

## 9.3 Frontend tests

- Modal mặc định hiện block read-only.
- Nút “Thay đổi thông tin liên hệ” chỉ hiện khi được phép.
- Template unsupported không hiện nút.
- Optional có tùy chọn ẩn; required không có.
- Chọn `SYSTEM_USER` gọi preview refresh và không mất body.
- Manual validation.
- Lỗi API giữ nguyên dữ liệu form.
- Gửi payload chỉ có structured override, không có contact HTML.
- Khôi phục template không reset contact override ngoài ý muốn.
- Khôi phục contact default không reset subject/body.
- Không duplicate block trong DOM preview.

## 9.4 Real-stack smoke

Chạy tối thiểu:

### Logistics

- Host mở Teabreak.
- Preview mặc định thấy đúng Host/campus.
- Đổi contact sang user khác hoặc manual.
- Gửi.
- Đọc `.eml`/pickup file.
- So sánh subject/body/contact/Reply-To.
- Xác nhận chỉ một block.

### Invitation

- Host mời participant.
- Đổi contact.
- Gửi.
- Kiểm tra action buttons vẫn đúng và không bị người dùng sửa.
- Contact nằm đúng vị trí.
- Reply-To đúng.

---

## 10. Acceptance criteria

Task chỉ được coi là hoàn tất khi:

1. Màn quản lý template và operational preview dùng cùng policy.
2. Operational preview không còn dùng khung `DisabledBlock` khi có context thật.
3. Contact block không nằm trong vùng editable.
4. Người gửi thay contact bằng form có cấu trúc.
5. Backend revalidate và re-resolve tại thời điểm gửi.
6. Email cuối chỉ có đúng một contact block.
7. `Reply-To` đồng bộ với contact cuối.
8. Override chỉ áp dụng cho email hiện tại.
9. Cấu hình template không bị sửa.
10. Template unsupported/credential không bị bypass.
11. Tất cả call site preview chỉnh sửa đã được audit.
12. VI/EN đều đúng.
13. Test unit/integration/frontend liên quan xanh.
14. Không tạo bảng mới hoặc thay schema khi chưa có quyết định rõ ràng.
15. Không để lại TODO chung chung hoặc fallback im lặng.

---

## 11. Gates thực hiện

Thực hiện theo thứ tự:

### G0 — Preflight

- branch/HEAD;
- WIP/stash;
- commit baseline;
- build/test baseline;
- DB patch/canonical hash;
- xác nhận không có outbound mail thật trong test.

### G1 — Audit

- toàn bộ preview call site;
- toàn bộ authored send path;
- toàn bộ template capability/policy;
- toàn bộ handler thiếu `ContactScope`.

Xuất matrix trước khi sửa.

### G2 — Backend contract và validator

- input;
- authorization;
- resolver;
- preview response.

### G3 — Dispatcher/send parity

- resolve lại tại send;
- reply-to;
- exactly-one block;
- không nhận contact HTML từ client.

### G4 — Shared modal

- UI read-only block;
- structured edit;
- restore;
- error state.

### G5 — Call sites

- logistics;
- participant invitation;
- department assignment;
- các call site khác tìm được trong audit.

### G6 — Tests

- unit;
- integration;
- frontend;
- real-stack smoke.

### G7 — Full gates

- backend build;
- backend unit;
- architecture;
- integration;
- frontend typecheck;
- frontend lint;
- frontend unit;
- frontend build;
- canonical SQL/hash nếu không thay schema vẫn phải xác nhận không đổi.

### G8 — Commit

Tách commit theo logic:

1. `feat(email): add structured per-message contact override`
2. `feat(frontend): add controlled contact editor to email preview`
3. `test(email): verify preview-send contact parity`

Không squash WIP của người khác. Không push nếu chưa được yêu cầu.

---

## 12. Báo cáo kết quả bắt buộc

Khi hoàn thành, báo cáo đúng format:

```text
1. Preflight
- Branch:
- HEAD trước/sau:
- WIP:
- DB hash:

2. Root cause
- Preview:
- Send:
- Duplicate risk:
- Affected call sites:

3. Implementation
- Backend files:
- Frontend files:
- API/contract:
- Authorization:
- Validation:
- Reply-To:
- Audit:

4. Template audit
- Tổng số template:
- Unsupported:
- NONE:
- OPTIONAL:
- REQUIRED:
- Caller thiếu ContactScope:
- Caller đã sửa:

5. Tests
- Backend unit:
- Architecture:
- Integration:
- Frontend unit:
- Typecheck/lint/build:
- Real-stack smoke:

6. Schema
- Có/không thay schema:
- Có/không tạo bảng:
- Canonical SQL hash:

7. Commits
- SHA:
- Message:

8. Remaining debt
- Chỉ liệt kê debt có bằng chứng và chưa nằm ngoài scope.
```

Không chỉ báo “đã sửa” hoặc “all green”. Phải cung cấp file, test và bằng chứng preview/MIME cụ thể.
