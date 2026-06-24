# PROMPT_CODE_UC63_CREATE_FAQ_BACKEND.md

## 0. Mục tiêu

Triển khai backend cho **UC-63: Create FAQ** của hệ thống **PEMS — Partnership Engagement Management System**.

Use case này phục vụ modal **+ Thêm mới FAQ** trên màn **Quản lý FAQ của HO** tại:

```text
/dashboard/faq
```

Modal có các trường:

```text
Loại FAQ *
Câu hỏi *
Trả lời *
Trạng thái hiển thị/ẩn *
Hủy
Tạo mới
```

Yêu cầu đã chốt:

```text
Default trạng thái khi mở modal tạo mới: PUBLISHED / Hiển thị
HO có thể đổi sang HIDDEN / Ẩn trước khi bấm Tạo mới.
Sau khi tạo xong, việc bật/tắt ẩn hiện ngay trên list dùng endpoint riêng Change FAQ Visibility.
```

---

## 1. Quyết định nghiệp vụ đã chốt

```text
UC ID: UC-63
UC Name: Create FAQ
Module: FAQ Management
Primary Actor: HO
Page: /dashboard/faq
Endpoint: POST /api/faqs
Allowed role: HO only
Authentication: Required
Authorization: Chỉ HO tuyệt đối
Default status: PUBLISHED / Hiển thị
Request: faqType, question, answer, status
Response: Created FAQ item
```

Không code nhầm với:

```text
UC-05: View FAQ public
UC-62: View List FAQ
```

Theo chốt hiện tại, **Report 3.1 là nguồn đúng cho UC numbering FAQ Management**:

```text
UC-62: View List FAQ
UC-63: Create FAQ
```

---

## 2. Tài liệu bắt buộc phải đọc trước khi code

AI Agent phải đọc trước:

```text
docs/architecture/PROJECT_STRUCTURE_FULL.md
docs/database/DATABASE_SCHEMA_v8_4_refined_v6_v10_no_dynamic_permissions_FULL_UPDATED.md
docs/PEMS_CANONICAL_BUSINESS_RULES_v8_4_refined_v6_v10_FULL_UPDATED.md
docs/PEMS_UC_IMPLEMENTATION_RULEBOOK_FRONTEND_BACKEND_DATABASE_VALIDATION_SECURITY_v8_4_refined_v6_v10_FULL_UPDATED.md
docs/PROJECT_OVERVIEW_v8_4_refined_v6_v10_FULL_UPDATED.md
Report 3.1_UCS_Template.docx
```

Nếu có mâu thuẫn:

```text
1. Ưu tiên Report 3.1 về UC ID: UC-63 Create FAQ.
2. Ưu tiên schema v10 về bảng/cột/enum/status.
3. Ưu tiên PROJECT_STRUCTURE_FULL.md mới nhất về đường dẫn file thật.
4. Ưu tiên code hiện tại nếu project đã có convention rõ ràng, nhưng không được trái schema v10.
```

---

## 3. Source of truth theo schema v10

FAQ trong SQL v10:

```text
Bảng: faqs
FAQ chỉ dùng tiếng Việt.
Không còn faqs.language_code.
Backend không nhận và không trả languageCode.
faq_type dùng enum nhóm chức năng hệ thống.
status dùng PUBLISHED / HIDDEN.
Dynamic permissions đã bị bỏ; không dùng permissions/role_permissions.
```

Enum `faq_type` hợp lệ:

```text
ACCOUNT_ACCESS
VISIT_REQUEST
DELEGATION_MANAGEMENT
LOGISTICS_RESOURCE
DOCUMENT_MEDIA
NOTIFICATION_EMAIL
OTHER
```

Status hợp lệ:

```text
PUBLISHED
HIDDEN
```

Không dùng FAQ type cũ:

```text
PROGRAM
TUITION_FEE
VISA
DORMITORY
Program
Tuition Fee
Visa
Dormitory
```

Không dùng status cũ nếu DB v10 là `PUBLISHED/HIDDEN`:

```text
VISIBLE
Visible
Ẩn/Hiển thị dạng raw value
```

Label tiếng Việt chỉ dùng cho UI/DTO, không dùng làm DB value.

---

## 4. Khác biệt với các UC liên quan

| Use case | Endpoint | Mục đích | Actor |
|---|---|---|---|
| UC-05 View FAQ public | `GET /api/public/faqs` | Public user xem FAQ đã publish | Public / anonymous |
| UC-62 View List FAQ | `GET /api/faqs` | HO xem danh sách FAQ quản lý | HO |
| UC-63 Create FAQ | `POST /api/faqs` | HO tạo FAQ mới | HO |
| Change FAQ Visibility | `PATCH /api/faqs/{faqId}/visibility` | HO bật/tắt ẩn hiện sau khi đã tạo | HO |

UC-63 chỉ tạo FAQ mới và set trạng thái ban đầu. Không nhét logic toggle sau tạo vào Create FAQ command.

---

## 5. Endpoint bắt buộc

```http
POST /api/faqs
```

Yêu cầu:

```text
Bắt buộc authenticated.
Chỉ HO được gọi.
Không AllowAnonymous.
Không dùng route /api/public/faqs.
Không dùng PublicContentController.
Không dùng dynamic permissions.
Không tạo bảng/cột mới.
```

Kết quả authorization:

```text
Không có token/session -> 401 Unauthorized
Có token nhưng không phải HO -> 403 Forbidden
HO active -> được tạo FAQ
```

Theo chốt hiện tại, Admin cũng không được tạo FAQ nếu policy là “HO only tuyệt đối”.

---

## 6. Controller cần dùng

Theo project structure mới, project đã có:

```text
backend/PEMS.Api/Controllers/FaqsController.cs
backend/PEMS.Api/Controllers/PublicContentController.cs
```

UC-63 là FAQ Management nội bộ, nên dùng:

```text
backend/PEMS.Api/Controllers/FaqsController.cs
```

Không dùng:

```text
backend/PEMS.Api/Controllers/PublicContentController.cs
```

Pseudo-code:

```csharp
[ApiController]
[Route("api/faqs")]
public sealed class FaqsController : ControllerBase
{
    private readonly IMediator _mediator;

    public FaqsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpPost]
    [RoleAuthorize(RoleCodes.HO)]
    public async Task<IActionResult> CreateFAQ(
        [FromBody] CreateFAQCommand command,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(command, cancellationToken);
        return Ok(ApiResponse.Success(result, "FAQ created successfully."));
    }
}
```

Nếu project hiện có response convention khác, giữ convention hiện tại. Controller chỉ nhận request, gọi MediatR và trả response. Không query DbContext trực tiếp trong Controller.

---

## 7. Request body

Frontend nên gửi rõ `status`.

```json
{
  "faqType": "ACCOUNT_ACCESS",
  "question": "Làm sao để đăng nhập hệ thống?",
  "answer": "Bạn có thể đăng nhập bằng tài khoản được cấp hoặc Google SSO.",
  "status": "PUBLISHED"
}
```

### Field rules

| Field | Required | Rule |
|---|---:|---|
| `faqType` | Yes | Một trong enum v10. |
| `question` | Yes | Trim xong không rỗng. Max theo DB nếu question là `VARCHAR(500)`, validate `<= 500`. |
| `answer` | Yes | Trim xong không rỗng. |
| `status` | Optional/Required theo convention | Nếu thiếu, backend default `PUBLISHED`. Nếu có thì chỉ nhận `PUBLISHED/HIDDEN`. |

Chốt behavior:

```text
Frontend default: PUBLISHED / Hiển thị.
Frontend nên luôn gửi status.
Backend vẫn fallback PUBLISHED nếu status null/empty để an toàn và đúng default đã chốt.
```

---

## 8. UI status mapping

Modal nên có switch/dropdown:

```text
Trạng thái *
[ON]  Hiển thị trên trang public FAQ
[OFF] Ẩn khỏi trang public FAQ
```

Mapping:

| UI | Request/DB value | Ý nghĩa |
|---|---|---|
| Hiển thị | `PUBLISHED` | FAQ xuất hiện trên public FAQ qua `GET /api/public/faqs`. |
| Ẩn | `HIDDEN` | FAQ chỉ nằm trong màn quản lý của HO, không xuất hiện public. |

Default:

```text
PUBLISHED / Hiển thị
```

---

## 9. Response thành công

Response gợi ý:

```json
{
  "success": true,
  "message": "FAQ created successfully.",
  "data": {
    "faqId": 10,
    "faqType": "ACCOUNT_ACCESS",
    "faqTypeLabel": "Tài khoản & truy cập",
    "question": "Làm sao để đăng nhập hệ thống?",
    "answer": "Bạn có thể đăng nhập bằng tài khoản được cấp hoặc Google SSO.",
    "displayOrder": 0,
    "status": "PUBLISHED",
    "statusLabel": "Hiển thị",
    "createdAt": "2026-06-24T19:50:00",
    "createdBy": 2,
    "updatedAt": "2026-06-24T19:50:00",
    "updatedBy": 2
  }
}
```

Sau response thành công, frontend:

```text
1. Đóng modal.
2. Reset form.
3. Reload UC-62 list hoặc prepend item mới vào đầu danh sách.
4. Nếu status = PUBLISHED thì badge “Hiển thị”.
5. Nếu status = HIDDEN thì badge “Ẩn”.
```

---

## 10. Dữ liệu lưu vào DB

Khi tạo thành công:

```text
faq_type      = request.faqType
question      = sanitized + trimmed question
answer        = sanitized + trimmed answer
display_order = 0 hoặc default hiện có của entity/database
status        = request.status nếu có, nếu không thì PUBLISHED
created_at    = current timestamp
created_by    = currentUser.user_id
updated_at    = current timestamp
updated_by    = currentUser.user_id
```

Không lưu:

```text
language_code
visible_label
status_label
faq_type_label
```

Các label chỉ là DTO/UI mapping.

---

## 11. Main Flow

| Step | Actor | Mô tả |
|---:|---|---|
| 1 | HO | Mở `/dashboard/faq`. |
| 2 | HO | Click `+ Thêm mới FAQ`. |
| 3 | Frontend | Mở modal tạo FAQ, default status = `PUBLISHED / Hiển thị`. |
| 4 | HO | Chọn `Loại FAQ`. |
| 5 | HO | Nhập `Câu hỏi`. |
| 6 | HO | Nhập `Trả lời`. |
| 7 | HO | Giữ `Hiển thị` hoặc đổi sang `Ẩn`. |
| 8 | Frontend | Gọi `POST /api/faqs`. |
| 9 | Backend | Xác thực token/session. |
| 10 | Backend | Kiểm tra role HO. |
| 11 | Backend | Validate input. |
| 12 | Backend | Sanitize question/answer. |
| 13 | Backend | Check duplicate question theo trim + case-insensitive. |
| 14 | Backend | Insert record vào bảng `faqs`. |
| 15 | Backend | Trả response created FAQ. |
| 16 | Frontend | Đóng modal, reset form, refresh/prepend list FAQ. |

---

## 12. Validation backend

### 12.1. Input validation bằng FluentValidation

```text
faqType:
- Required
- Must be one of enum v10

question:
- Required
- Trim không được rỗng
- Max length theo DB, đề xuất 500 nếu entity/schema là VARCHAR(500)

answer:
- Required
- Trim không được rỗng

status:
- Optional input nhưng nếu null/empty thì backend default PUBLISHED
- Nếu có giá trị thì chỉ nhận PUBLISHED/HIDDEN
```

Message gợi ý:

```text
FAQ type is invalid.
Question is required.
Question must not exceed 500 characters.
Answer is required.
FAQ status is invalid.
```

### 12.2. Business validation trong Handler

Check duplicate question:

```text
Normalize = Trim + case-insensitive compare.
Nếu đã tồn tại FAQ với cùng question normalized -> reject.
```

Duplicate check phải check toàn bảng `faqs`, bao gồm cả `PUBLISHED` và `HIDDEN`.

Ví dụ:

```text
Existing: "Làm sao đăng nhập?"
New: "  làm sao đăng nhập?  "
=> Duplicate
```

Response đề xuất:

```text
409 Conflict
This question already exists in the system. Please enter a different question.
```

Nếu project convention trả validation 400 thay vì 409, có thể theo convention hiện tại, nhưng phải không tạo record.

---

## 13. Sanitize content

Trước khi lưu:

```text
question = remove HTML tags/scripts + trim
answer = remove HTML tags/scripts + trim
```

Ví dụ:

```text
Input: <script>alert(1)</script>Làm sao đăng nhập?
Saved: Làm sao đăng nhập?
```

Yêu cầu:

```text
Không lưu HTML/script vào DB.
Giữ plain text content.
Sau sanitize vẫn phải đảm bảo question/answer không rỗng.
```

Nếu project đã có helper sanitize chung, dùng helper đó. Không tự tạo helper trùng lặp nếu đã có service/utility hiện tại.

---

## 14. Application module cần dùng

Theo project structure hiện tại, dùng module:

```text
backend/PEMS.Application/Faqs/Commands/CreateFAQ/
```

Các file dự kiến:

```text
backend/PEMS.Application/Faqs/Commands/CreateFAQ/CreateFAQCommand.cs
backend/PEMS.Application/Faqs/Commands/CreateFAQ/CreateFAQCommandHandler.cs
backend/PEMS.Application/Faqs/Commands/CreateFAQ/CreateFAQCommandValidator.cs
backend/PEMS.Application/Faqs/Commands/CreateFAQ/CreateFAQResponse.cs
```

Có thể cần cập nhật:

```text
backend/PEMS.Api/Controllers/FaqsController.cs
backend/PEMS.Domain/Constants/FaqConstants.cs
backend/PEMS.Domain/Entities/Faqs/Faq.cs
backend/PEMS.Application/Common/Interfaces/IApplicationDbContext.cs
backend/PEMS.Infrastructure/Persistence/ApplicationDbContext.cs
```

Không dùng nhầm:

```text
backend/PEMS.Application/PublicContent/Queries/ViewFaq/
backend/PEMS.Api/Controllers/PublicContentController.cs
```

---

## 15. Command đề xuất

```csharp
public sealed record CreateFAQCommand(
    string FaqType,
    string Question,
    string Answer,
    string? Status
) : IRequest<CreateFAQResponse>;
```

Nếu project dùng class thay vì record, giữ convention hiện tại.

Backend behavior:

```text
status = string.IsNullOrWhiteSpace(request.Status)
    ? FaqConstants.Status.Published
    : request.Status.Trim();
```

---

## 16. Response DTO đề xuất

```csharp
public sealed class CreateFAQResponse
{
    public ulong FaqId { get; init; }

    public string FaqType { get; init; } = default!;
    public string FaqTypeLabel { get; init; } = default!;

    public string Question { get; init; } = default!;
    public string Answer { get; init; } = default!;

    public int DisplayOrder { get; init; }

    public string Status { get; init; } = default!;
    public string StatusLabel { get; init; } = default!;

    public DateTime CreatedAt { get; init; }
    public ulong? CreatedBy { get; init; }

    public DateTime? UpdatedAt { get; init; }
    public ulong? UpdatedBy { get; init; }
}
```

Nếu project dùng `long` thay vì `ulong`, theo convention hiện có của `Faq.cs`.

---

## 17. Constants/enum

Kiểm tra trước:

```text
backend/PEMS.Domain/Constants/FaqConstants.cs
backend/PEMS.Domain/Enums/FaqVisibilityStatus.cs
```

Nếu chưa đủ v10, cập nhật `FaqConstants.cs`.

Gợi ý:

```csharp
public static class FaqConstants
{
    public static class Status
    {
        public const string Published = "PUBLISHED";
        public const string Hidden = "HIDDEN";
    }

    public static class Type
    {
        public const string AccountAccess = "ACCOUNT_ACCESS";
        public const string VisitRequest = "VISIT_REQUEST";
        public const string DelegationManagement = "DELEGATION_MANAGEMENT";
        public const string LogisticsResource = "LOGISTICS_RESOURCE";
        public const string DocumentMedia = "DOCUMENT_MEDIA";
        public const string NotificationEmail = "NOTIFICATION_EMAIL";
        public const string Other = "OTHER";

        public static readonly IReadOnlySet<string> All = new HashSet<string>
        {
            AccountAccess,
            VisitRequest,
            DelegationManagement,
            LogisticsResource,
            DocumentMedia,
            NotificationEmail,
            Other
        };
    }

    public static string ToVietnameseTypeLabel(string faqType)
    {
        return faqType switch
        {
            Type.AccountAccess => "Tài khoản và truy cập",
            Type.VisitRequest => "Đăng ký tham quan",
            Type.DelegationManagement => "Quản lý đoàn tiếp khách",
            Type.LogisticsResource => "Hậu cần và tài nguyên",
            Type.DocumentMedia => "Tài liệu và truyền thông",
            Type.NotificationEmail => "Thông báo và email",
            Type.Other => "Khác",
            _ => "Khác"
        };
    }

    public static string ToVietnameseStatusLabel(string status)
    {
        return status switch
        {
            Status.Published => "Hiển thị",
            Status.Hidden => "Ẩn",
            _ => status
        };
    }
}
```

Không dùng label tiếng Việt làm DB value.

---

## 18. Handler pseudo-code

```csharp
public sealed class CreateFAQCommandHandler
    : IRequestHandler<CreateFAQCommand, CreateFAQResponse>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeProvider _dateTimeProvider;

    public CreateFAQCommandHandler(
        IApplicationDbContext dbContext,
        ICurrentUserService currentUser,
        IDateTimeProvider dateTimeProvider)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<CreateFAQResponse> Handle(
        CreateFAQCommand request,
        CancellationToken cancellationToken)
    {
        var now = _dateTimeProvider.Now;
        var currentUserId = _currentUser.UserId;

        var faqType = request.FaqType.Trim();
        var status = string.IsNullOrWhiteSpace(request.Status)
            ? FaqConstants.Status.Published
            : request.Status.Trim();

        var sanitizedQuestion = SanitizePlainText(request.Question).Trim();
        var sanitizedAnswer = SanitizePlainText(request.Answer).Trim();

        if (string.IsNullOrWhiteSpace(sanitizedQuestion))
            throw new ValidationException("Question is required.");

        if (string.IsNullOrWhiteSpace(sanitizedAnswer))
            throw new ValidationException("Answer is required.");

        var normalizedQuestion = sanitizedQuestion.ToLower();

        var exists = await _dbContext.Faqs
            .AsNoTracking()
            .AnyAsync(x => x.Question.Trim().ToLower() == normalizedQuestion, cancellationToken);

        if (exists)
        {
            throw new ConflictException("This question already exists in the system. Please enter a different question.");
        }

        var faq = new Faq
        {
            FaqType = faqType,
            Question = sanitizedQuestion,
            Answer = sanitizedAnswer,
            DisplayOrder = 0,
            Status = status,
            CreatedAt = now,
            CreatedBy = currentUserId,
            UpdatedAt = now,
            UpdatedBy = currentUserId
        };

        _dbContext.Faqs.Add(faq);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new CreateFAQResponse
        {
            FaqId = faq.FaqId,
            FaqType = faq.FaqType,
            FaqTypeLabel = FaqConstants.ToVietnameseTypeLabel(faq.FaqType),
            Question = faq.Question,
            Answer = faq.Answer,
            DisplayOrder = faq.DisplayOrder,
            Status = faq.Status,
            StatusLabel = FaqConstants.ToVietnameseStatusLabel(faq.Status),
            CreatedAt = faq.CreatedAt,
            CreatedBy = faq.CreatedBy,
            UpdatedAt = faq.UpdatedAt,
            UpdatedBy = faq.UpdatedBy
        };
    }
}
```

Điều chỉnh tên service current user/time theo code thật. Không dùng `DateTime.Now` trực tiếp nếu project có DateTimeProvider. Không gọi SaveChangesAsync nếu project convention dùng UnitOfWork/TransactionBehaviour tự động và handler khác không gọi.

---

## 19. Business Rules

| ID | Rule |
|---|---|
| BR-63-01 | Chỉ HO được tạo FAQ. |
| BR-63-02 | Nếu chưa đăng nhập, backend trả 401. |
| BR-63-03 | Nếu đăng nhập nhưng không phải HO, backend trả 403. |
| BR-63-04 | `faqType` bắt buộc và phải thuộc enum v10. |
| BR-63-05 | `question` bắt buộc, trim/sanitize xong không được rỗng. |
| BR-63-06 | `question` không được vượt quá độ dài DB, đề xuất validate 500 ký tự nếu schema là VARCHAR(500). |
| BR-63-07 | `answer` bắt buộc, trim/sanitize xong không được rỗng. |
| BR-63-08 | `status` nếu không truyền thì default `PUBLISHED`. Nếu truyền thì chỉ nhận `PUBLISHED/HIDDEN`. |
| BR-63-09 | Không nhận/trả `languageCode`. |
| BR-63-10 | Không cho tạo FAQ có question trùng, so sánh trim + case-insensitive trên toàn bảng `faqs`. |
| BR-63-11 | Backend sanitize question/answer trước khi lưu để chống XSS. |
| BR-63-12 | Khi tạo, `created_by` và `updated_by` là current HO user id. |
| BR-63-13 | Khi tạo, `created_at` và `updated_at` là current timestamp. |
| BR-63-14 | Nếu status = `PUBLISHED`, FAQ sẽ xuất hiện ở public FAQ. Nếu status = `HIDDEN`, FAQ chỉ xuất hiện trong management list. |
| BR-63-15 | Bật/tắt ẩn hiện sau khi tạo dùng endpoint riêng Change FAQ Visibility, không nhét vào Create FAQ. |
| BR-63-16 | Không dùng dynamic permissions/role_permissions. |

---

## 20. Alternative Flows

```text
AF-01 — HO hủy modal:
HO bấm Hủy hoặc X. Frontend đóng modal, không gọi API. DB không thay đổi.

AF-02 — Thiếu câu hỏi:
POST /api/faqs với question rỗng sau trim/sanitize -> 400, không tạo record.

AF-03 — Thiếu câu trả lời:
POST /api/faqs với answer rỗng sau trim/sanitize -> 400, không tạo record.

AF-04 — FAQ type không hợp lệ:
faqType = VISA -> 400 Bad Request, FAQ type is invalid.

AF-05 — Status không hợp lệ:
status = VISIBLE -> 400 Bad Request, FAQ status is invalid.

AF-06 — Trùng câu hỏi:
Question sau trim/case-insensitive đã tồn tại -> 409 Conflict hoặc validation error theo convention, không tạo record.

AF-07 — Chưa đăng nhập:
Không có token/session -> 401 Unauthorized.

AF-08 — Không phải HO:
Role khác HO gọi trực tiếp API -> 403 Forbidden.

AF-09 — Không gửi status:
Backend default status = PUBLISHED, tạo FAQ ở trạng thái Hiển thị.
```

---

## 21. Quan hệ với Toggle ẩn/hiện sau khi tạo

Yêu cầu “sau khi tạo xong muốn ẩn hiện thì cũng có thể chỉnh sửa ẩn hiện ngay trên màn list view FAQ” phải xử lý bằng endpoint riêng.

Đề xuất endpoint:

```http
PATCH /api/faqs/{faqId}/visibility
```

Request:

```json
{
  "status": "HIDDEN"
}
```

hoặc:

```json
{
  "status": "PUBLISHED"
}
```

Use case này có thể thuộc `Change FAQ Visibility`. Không gộp logic này vào `POST /api/faqs`.

---

## 22. Verification Criteria

```text
VC-01 — Tạo FAQ hiển thị mặc định
Given HO đã đăng nhập
When HO gửi POST /api/faqs với faqType hợp lệ, question hợp lệ, answer hợp lệ và không truyền status
Then backend tạo FAQ
And status = PUBLISHED

VC-02 — Tạo FAQ với status PUBLISHED
When HO gửi POST /api/faqs với status = PUBLISHED
Then backend tạo FAQ, status = PUBLISHED, statusLabel = Hiển thị

VC-03 — Tạo FAQ với status HIDDEN
When HO gửi POST /api/faqs với status = HIDDEN
Then backend tạo FAQ, status = HIDDEN, statusLabel = Ẩn
And FAQ không xuất hiện ở UC-05 public FAQ

VC-04 — Question rỗng
Given question rỗng sau trim/sanitize
When HO tạo FAQ
Then trả 400 và không tạo record

VC-05 — Answer rỗng
Given answer rỗng sau trim/sanitize
When HO tạo FAQ
Then trả 400 và không tạo record

VC-06 — Duplicate question
Given đã có FAQ với question "Làm sao đăng nhập?"
When HO tạo FAQ mới với question "  làm sao đăng nhập?  "
Then trả duplicate error và không tạo record

VC-07 — FAQ type cũ bị reject
When HO tạo FAQ với faqType = VISA
Then trả 400 và không tạo record

VC-08 — Status cũ bị reject
When HO tạo FAQ với status = VISIBLE
Then trả 400 và không tạo record

VC-09 — Non-HO bị chặn
Given user không phải HO
When gọi POST /api/faqs
Then trả 403

VC-10 — Không login bị chặn
Given không có token/session
When gọi POST /api/faqs
Then trả 401

VC-11 — Sanitize HTML/script
Given question hoặc answer có HTML/script
When HO tạo FAQ
Then backend lưu plain text đã sanitize
And response không chứa script/html nguy hiểm
```

---

## 23. Manual test bằng curl/Postman

### 23.1. Không token

```bash
curl -X POST "http://localhost:5265/api/faqs" \
  -H "Content-Type: application/json" \
  -d "{\"faqType\":\"ACCOUNT_ACCESS\",\"question\":\"Q?\",\"answer\":\"A\",\"status\":\"PUBLISHED\"}"
```

Expected:

```text
401 Unauthorized
```

### 23.2. Token non-HO

```bash
curl -X POST "http://localhost:5265/api/faqs" \
  -H "Authorization: Bearer <STAFF_TOKEN>" \
  -H "Content-Type: application/json" \
  -d "{\"faqType\":\"ACCOUNT_ACCESS\",\"question\":\"Q?\",\"answer\":\"A\",\"status\":\"PUBLISHED\"}"
```

Expected:

```text
403 Forbidden
```

### 23.3. Token HO, tạo PUBLISHED

```bash
curl -X POST "http://localhost:5265/api/faqs" \
  -H "Authorization: Bearer <HO_TOKEN>" \
  -H "Content-Type: application/json" \
  -d "{\"faqType\":\"ACCOUNT_ACCESS\",\"question\":\"Làm sao đăng nhập?\",\"answer\":\"Bạn đăng nhập bằng tài khoản được cấp hoặc Google SSO.\",\"status\":\"PUBLISHED\"}"
```

Expected:

```text
201 Created hoặc 200 OK theo convention
status = PUBLISHED
```

### 23.4. Token HO, tạo HIDDEN

```bash
curl -X POST "http://localhost:5265/api/faqs" \
  -H "Authorization: Bearer <HO_TOKEN>" \
  -H "Content-Type: application/json" \
  -d "{\"faqType\":\"ACCOUNT_ACCESS\",\"question\":\"FAQ ẩn test?\",\"answer\":\"Nội dung test.\",\"status\":\"HIDDEN\"}"
```

Expected:

```text
201 Created hoặc 200 OK
status = HIDDEN
Không xuất hiện ở GET /api/public/faqs
```

### 23.5. Token HO, không truyền status

```bash
curl -X POST "http://localhost:5265/api/faqs" \
  -H "Authorization: Bearer <HO_TOKEN>" \
  -H "Content-Type: application/json" \
  -d "{\"faqType\":\"ACCOUNT_ACCESS\",\"question\":\"FAQ default status?\",\"answer\":\"Nội dung test.\"}"
```

Expected:

```text
201 Created hoặc 200 OK
status = PUBLISHED
```

---

## 24. Những điều không được làm

```text
Không code nhầm UC-63 thành UC-05 public FAQ.
Không dùng PublicContentController.
Không dùng /api/public/faqs.
Không AllowAnonymous.
Không cho Admin/Staff/Department/Student/Visitor tạo FAQ.
Không dùng enum type cũ Program/Tuition/Visa/Dormitory.
Không dùng status cũ Visible/Hidden nếu DB v10 là PUBLISHED/HIDDEN.
Không nhận/trả languageCode.
Không lưu HTML/script vào question/answer.
Không bỏ duplicate check.
Không query DbContext trong Controller.
Không tạo bảng/cột mới nếu SQL v10 đã đủ.
Không dùng dynamic permissions/role_permissions.
Không sửa frontend trong task backend này nếu user chỉ yêu cầu backend.
Không nhét toggle visibility sau tạo vào CreateFAQCommand.
Không báo hoàn thành nếu chưa build/test hoặc chưa nói rõ phần chưa test được.
```

---

## 25. Definition of Done

AI Agent chỉ được báo hoàn thành khi đủ:

```text
[ ] Xác nhận đang triển khai UC-63: Create FAQ.
[ ] Endpoint POST /api/faqs hoạt động.
[ ] Endpoint yêu cầu authentication.
[ ] Endpoint chỉ HO được gọi.
[ ] Non-HO bị 403.
[ ] Unauthenticated bị 401.
[ ] Request nhận faqType, question, answer, status.
[ ] Nếu status thiếu/null/empty thì default PUBLISHED.
[ ] Validate faqType enum v10.
[ ] Validate status PUBLISHED/HIDDEN.
[ ] Validate question/answer required sau trim/sanitize.
[ ] Check duplicate question trim + case-insensitive.
[ ] Sanitize HTML/script trước khi lưu.
[ ] Không nhận/trả languageCode.
[ ] Lưu created_by, updated_by là current HO.
[ ] Lưu created_at, updated_at.
[ ] Không dùng public endpoint.
[ ] Không query DbContext trong Controller.
[ ] Có validator.
[ ] dotnet build PASS.
[ ] Có unit/integration test hoặc manual API test rõ ràng.
```

---

## 26. Output mong muốn từ AI Agent sau khi code

Sau khi code xong, báo cáo theo format:

```text
1. Files read
- ...

2. Files changed
- backend/PEMS.Api/Controllers/FaqsController.cs
- backend/PEMS.Application/Faqs/Commands/CreateFAQ/...
- backend/PEMS.Domain/Constants/FaqConstants.cs
- tests/...

3. Endpoint implemented
- POST /api/faqs

4. Authorization
- HO only
- unauthenticated -> 401
- non-HO -> 403

5. Logic implemented
- default status PUBLISHED
- create PUBLISHED/HIDDEN
- duplicate question check
- sanitize question/answer
- save created_by/updated_by
- no languageCode

6. Validation
- faqType enum v10
- question/answer required
- status PUBLISHED/HIDDEN
- duplicate question

7. Test result
- dotnet build: PASS/FAIL
- dotnet test: PASS/FAIL
- Manual API test: PASS/FAIL

8. Notes / Risks
- ...
```
