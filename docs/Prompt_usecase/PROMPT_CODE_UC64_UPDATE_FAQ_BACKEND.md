# PROMPT_CODE_UC64_UPDATE_FAQ_BACKEND.md

## 0. Mục tiêu

Triển khai backend cho **UC-64: Update FAQ** của hệ thống **PEMS — Partnership Engagement Management System**.

Use case này phục vụ màn **Chi tiết FAQ** trong khu vực **Quản lý FAQ của HO**. Luồng UI đã chốt:

```text
1. HO vào /dashboard/faq.
2. HO bấm icon mắt ở dòng FAQ trên màn View List FAQ.
3. Frontend mở trang Chi tiết FAQ.
4. Trang chi tiết hiển thị: loại FAQ, trạng thái, câu hỏi, câu trả lời, người tạo, thời gian tạo, người cập nhật gần nhất, thời gian cập nhật gần nhất.
5. HO bấm icon bút để chuyển sang chế độ chỉnh sửa.
6. HO có thể sửa: loại FAQ, câu hỏi, câu trả lời.
7. HO bấm Lưu.
8. Nếu có thay đổi thật thì backend cập nhật dữ liệu và updated_at/updated_by.
9. Nếu không có thay đổi thật thì backend không cập nhật updated_at/updated_by.
```

---

## 1. Quyết định nghiệp vụ đã chốt

```text
UC ID: UC-64
UC Name: Update FAQ
Module: FAQ Management
Primary Actor: HO
Allowed role: HO only
Authentication: Required
Authorization: Chỉ HO tuyệt đối
```

Endpoint cần có:

```http
GET /api/faqs/{faqId}
PUT /api/faqs/{faqId}
```

Ý nghĩa:

```text
GET /api/faqs/{faqId}
- Dùng khi bấm icon mắt ở màn UC-62 View List FAQ.
- Trả dữ liệu chi tiết FAQ và metadata người tạo/người cập nhật.

PUT /api/faqs/{faqId}
- Dùng khi bấm icon bút, sửa nội dung rồi bấm Lưu.
- Chỉ cập nhật faqType, question, answer.
```

Điểm quan trọng đã chốt:

```text
Nếu HO bấm chỉnh sửa nhưng không thay đổi gì rồi bấm Lưu:
- Không cập nhật updated_at.
- Không cập nhật updated_by.
- Không ghi nhận là một lần update thật.
- Response nên có changed = false.
```

---

## 2. Không code nhầm với UC khác

| Use case | Endpoint | Mục đích |
|---|---|---|
| UC-05 View FAQ public | `GET /api/public/faqs` | Public user xem FAQ đã publish |
| UC-62 View List FAQ | `GET /api/faqs` | HO xem danh sách FAQ quản lý |
| UC-63 Create FAQ | `POST /api/faqs` | HO tạo FAQ mới |
| UC-64 Update FAQ | `GET /api/faqs/{faqId}` + `PUT /api/faqs/{faqId}` | HO xem chi tiết và cập nhật FAQ |
| Change FAQ Visibility | `PATCH /api/faqs/{faqId}/visibility` | HO bật/tắt ẩn hiện sau khi đã tạo |

UC-64 **không sửa trạng thái ẩn/hiện**. Toggle trạng thái trên list là use case riêng.

---

## 3. Tài liệu bắt buộc phải đọc trước khi code

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
1. Ưu tiên Report 3.1 về UC ID: UC-64 Update FAQ.
2. Ưu tiên yêu cầu mới của user về no-change save: không cập nhật updated_at/updated_by nếu không có thay đổi thật.
3. Ưu tiên schema v10 về bảng/cột/enum/status.
4. Ưu tiên PROJECT_STRUCTURE_FULL.md mới nhất về đường dẫn file thật.
```

---

## 4. Source of truth theo schema v10

FAQ trong SQL v10:

```text
- Bảng: faqs
- FAQ chỉ dùng tiếng Việt.
- Không còn faqs.language_code.
- Backend không nhận và không trả languageCode.
- faq_type dùng enum nhóm chức năng hệ thống.
- status dùng PUBLISHED / HIDDEN.
- Dynamic permissions đã bị bỏ; không dùng permissions/role_permissions.
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

UC-64 không sửa status, nhưng response detail vẫn trả status để frontend hiển thị badge.

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

Không dùng status cũ nếu DB v10 đang là `PUBLISHED/HIDDEN`:

```text
VISIBLE
Visible
Hidden label làm raw value
```

Label tiếng Việt chỉ dùng cho DTO/UI, không dùng làm DB value.

---

## 5. Authorization

Chỉ HO được xem chi tiết và cập nhật FAQ.

```text
Allowed role: HO only
```

Kết quả authorization:

```text
Không có token/session -> 401 Unauthorized
Có token nhưng không phải HO -> 403 Forbidden
HO active -> được xem/cập nhật
```

Không cho:

```text
Admin
Staff Leader
Staff
Department Leader
Department Staff
Student
Visitor
```

Không dùng dynamic permission. Không gắn `AllowAnonymous`.

---

## 6. Controller cần dùng

Theo project structure, dùng:

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

    [HttpGet("{faqId:long}")]
    [RoleAuthorize(RoleCodes.HO)]
    public async Task<IActionResult> GetFAQDetail(
        [FromRoute] long faqId,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new ViewFAQDetailQuery(faqId), cancellationToken);
        return Ok(ApiResponse.Success(result, "FAQ detail loaded successfully."));
    }

    [HttpPut("{faqId:long}")]
    [RoleAuthorize(RoleCodes.HO)]
    public async Task<IActionResult> UpdateFAQ(
        [FromRoute] long faqId,
        [FromBody] UpdateFAQRequest request,
        CancellationToken cancellationToken)
    {
        var command = new UpdateFAQCommand(
            faqId,
            request.FaqType,
            request.Question,
            request.Answer);

        var result = await _mediator.Send(command, cancellationToken);
        return Ok(ApiResponse.Success(
            result,
            result.Changed ? "FAQ updated successfully." : "No changes detected."));
    }
}
```

Nếu project hiện có convention khác về `RoleAuthorize`, `ApiResponse`, route constraint type hoặc request DTO, dùng đúng convention hiện tại.

Controller chỉ nhận request, gọi MediatR và trả response. Không query DbContext trực tiếp trong Controller.

---

## 7. GET detail endpoint

### 7.1. Endpoint

```http
GET /api/faqs/{faqId}
```

### 7.2. Mục đích

Dùng khi HO bấm icon mắt ở màn UC-62 View List FAQ.

### 7.3. Response gợi ý

```json
{
  "success": true,
  "message": "FAQ detail loaded successfully.",
  "data": {
    "faqId": 1,
    "faqType": "ACCOUNT_ACCESS",
    "faqTypeLabel": "Tài khoản và truy cập",
    "question": "Điều kiện để tham gia học kỳ trao đổi là gì?",
    "answer": "Sinh viên phải hoàn thành ít nhất 2 học kỳ tại trường...",
    "displayOrder": 0,
    "status": "PUBLISHED",
    "statusLabel": "Hiển thị",
    "createdAt": "2026-06-24T10:00:00",
    "createdBy": 2,
    "createdByName": "Head Office",
    "updatedAt": "2026-06-24T11:00:00",
    "updatedBy": 2,
    "updatedByName": "Head Office"
  }
}
```

### 7.4. Detail response phải có metadata

Bắt buộc trả các field để UI hiển thị:

```text
createdBy
createdByName
createdAt
updatedBy
updatedByName
updatedAt
```

Nếu chưa từng cập nhật:

```text
updatedBy = null
updatedByName = null
updatedAt = null
```

Frontend có thể hiển thị `Chưa cập nhật`.

Nếu UC-63 đã set `updated_by = created_by` và `updated_at = created_at` khi tạo, detail sẽ hiển thị cùng người/timestamp tạo. Agent phải kiểm tra schema/code hiện tại để không xử lý sai.

---

## 8. PUT update endpoint

### 8.1. Endpoint

```http
PUT /api/faqs/{faqId}
```

### 8.2. Request body

```json
{
  "faqType": "ACCOUNT_ACCESS",
  "question": "Điều kiện để tham gia học kỳ trao đổi là gì?",
  "answer": "Sinh viên phải hoàn thành ít nhất 2 học kỳ tại trường..."
}
```

### 8.3. Chỉ được update các field

```text
faqType
question
answer
```

### 8.4. Không nhận/update các field

```text
status
languageCode
createdBy
createdAt
updatedBy
updatedAt
displayOrder
faqId trong body
```

Lý do:

```text
- status thuộc use case Change FAQ Visibility.
- languageCode không còn trong schema v10.
- audit fields do backend tự xử lý.
- displayOrder không nằm trong flow UI này.
```

---

## 9. Response khi update có thay đổi thật

```json
{
  "success": true,
  "message": "FAQ updated successfully.",
  "data": {
    "faqId": 1,
    "faqType": "VISIT_REQUEST",
    "faqTypeLabel": "Đăng ký tham quan",
    "question": "Câu hỏi đã cập nhật?",
    "answer": "Câu trả lời đã cập nhật.",
    "displayOrder": 0,
    "status": "PUBLISHED",
    "statusLabel": "Hiển thị",
    "createdAt": "2026-06-24T10:00:00",
    "createdBy": 2,
    "createdByName": "Head Office",
    "updatedAt": "2026-06-25T01:45:00",
    "updatedBy": 2,
    "updatedByName": "Head Office",
    "changed": true
  }
}
```

Backend cập nhật:

```text
faq_type
question
answer
updated_at = current timestamp
updated_by = current HO user_id
```

Backend không cập nhật:

```text
status
created_at
created_by
display_order
```

---

## 10. Response khi không có thay đổi thật

Nếu dữ liệu request sau trim/sanitize giống DB:

```json
{
  "success": true,
  "message": "No changes detected.",
  "data": {
    "faqId": 1,
    "faqType": "ACCOUNT_ACCESS",
    "faqTypeLabel": "Tài khoản và truy cập",
    "question": "Điều kiện để tham gia học kỳ trao đổi là gì?",
    "answer": "Sinh viên phải hoàn thành ít nhất 2 học kỳ tại trường...",
    "displayOrder": 0,
    "status": "PUBLISHED",
    "statusLabel": "Hiển thị",
    "createdAt": "2026-06-24T10:00:00",
    "createdBy": 2,
    "createdByName": "Head Office",
    "updatedAt": "2026-06-24T11:00:00",
    "updatedBy": 2,
    "updatedByName": "Head Office",
    "changed": false
  }
}
```

Bắt buộc:

```text
- Không update entity.
- Không đổi updated_at.
- Không đổi updated_by.
- Không ghi audit update nếu audit đang theo changed fields.
```

So sánh “không có thay đổi” phải dựa trên giá trị đã sanitize/trim.

Ví dụ:

```text
DB question: "Điều kiện tham gia?"
Input: " <b>Điều kiện tham gia?</b> "
Sanitized input: "Điều kiện tham gia?"
=> changed = false
=> không update updated_at/updated_by
```

---

## 11. Main Flow

| Step | Actor | Mô tả |
|---:|---|---|
| 1 | HO | Ở `/dashboard/faq`, bấm icon mắt trên một FAQ row. |
| 2 | Frontend | Gọi `GET /api/faqs/{faqId}`. |
| 3 | Backend | Validate session, role HO, load FAQ detail kèm metadata. |
| 4 | Frontend | Render trang Chi tiết FAQ. |
| 5 | HO | Bấm icon bút để vào edit mode. |
| 6 | HO | Sửa `faqType`, `question`, hoặc `answer`. |
| 7 | HO | Bấm `Lưu`. |
| 8 | Frontend | Gọi `PUT /api/faqs/{faqId}`. |
| 9 | Backend | Validate role, input, duplicate question, sanitize content. |
| 10 | Backend | So sánh dữ liệu mới với dữ liệu hiện tại. |
| 11A | Backend | Nếu có thay đổi thật, cập nhật record và audit fields. |
| 11B | Backend | Nếu không có thay đổi, không cập nhật `updated_at/updated_by`. |
| 12 | Frontend | Cập nhật lại detail view/list row theo response. |

---

## 12. Validation backend

### 12.1. Route validation

```text
faqId:
- Required từ route.
- Phải là số hợp lệ.
- Phải tồn tại trong DB.
- Nếu không tồn tại -> 404 Not Found.
```

### 12.2. Input validation

```text
faqType:
- Required.
- Phải thuộc enum v10.

question:
- Required.
- Trim/sanitize xong không được rỗng.
- Max 500 ký tự nếu schema/entity question là VARCHAR(500).

answer:
- Required.
- Trim/sanitize xong không được rỗng.
```

Message gợi ý:

```text
FAQ type is invalid.
Question is required.
Question must not exceed 500 characters.
Answer is required.
```

### 12.3. Business validation

Duplicate question:

```text
Check toàn bảng faqs.
So sánh trim + case-insensitive.
Loại trừ chính FAQ đang update.
```

Ví dụ:

```text
FAQ ID=200 có question: "How much is the annual tuition fee?"
HO đang sửa FAQ ID=100 thành: " how much is the annual tuition fee? "
=> Reject duplicate.
```

Response đề xuất:

```text
409 Conflict
This question already exists in the system. Please enter a different question.
```

Nếu project convention dùng 400 validation error, có thể theo convention hiện tại nhưng không được update DB.

---

## 13. Sanitize content

Trước khi so sánh và trước khi lưu:

```text
question = remove HTML tags/scripts + trim
answer = remove HTML tags/scripts + trim
```

Yêu cầu:

```text
- Không lưu HTML/script vào DB.
- Giữ plain text content.
- Sau sanitize vẫn phải kiểm tra required.
- So sánh changed/no-change bằng giá trị đã sanitize/trim.
```

Nếu project đã có helper sanitize chung, dùng helper đó. Không tự tạo nhiều helper trùng lặp nếu đã có service/utility hiện tại.

---

## 14. Application module cần dùng

Theo project structure hiện tại, dùng các module:

```text
backend/PEMS.Application/Faqs/Commands/UpdateFAQ/
backend/PEMS.Application/Faqs/Queries/ViewFAQDetail/
```

Các file dự kiến:

```text
backend/PEMS.Application/Faqs/Commands/UpdateFAQ/UpdateFAQCommand.cs
backend/PEMS.Application/Faqs/Commands/UpdateFAQ/UpdateFAQCommandHandler.cs
backend/PEMS.Application/Faqs/Commands/UpdateFAQ/UpdateFAQCommandValidator.cs
backend/PEMS.Application/Faqs/Commands/UpdateFAQ/UpdateFAQResponse.cs

backend/PEMS.Application/Faqs/Queries/ViewFAQDetail/ViewFAQDetailQuery.cs
backend/PEMS.Application/Faqs/Queries/ViewFAQDetail/ViewFAQDetailQueryHandler.cs
backend/PEMS.Application/Faqs/Queries/ViewFAQDetail/ViewFAQDetailDto.cs
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

## 15. Query/Command đề xuất

### 15.1. View detail query

```csharp
public sealed record ViewFAQDetailQuery(long FaqId)
    : IRequest<ViewFAQDetailDto>;
```

### 15.2. Update command

```csharp
public sealed record UpdateFAQCommand(
    long FaqId,
    string FaqType,
    string Question,
    string Answer
) : IRequest<UpdateFAQResponse>;
```

Nếu project đang dùng `ulong` hoặc `long` khác convention, theo `Faq.cs` hiện tại.

---

## 16. DTO/Response đề xuất

### 16.1. ViewFAQDetailDto

```csharp
public sealed class ViewFAQDetailDto
{
    public long FaqId { get; init; }

    public string FaqType { get; init; } = default!;
    public string FaqTypeLabel { get; init; } = default!;

    public string Question { get; init; } = default!;
    public string Answer { get; init; } = default!;

    public int DisplayOrder { get; init; }

    public string Status { get; init; } = default!;
    public string StatusLabel { get; init; } = default!;

    public DateTime CreatedAt { get; init; }
    public long? CreatedBy { get; init; }
    public string? CreatedByName { get; init; }

    public DateTime? UpdatedAt { get; init; }
    public long? UpdatedBy { get; init; }
    public string? UpdatedByName { get; init; }
}
```

### 16.2. UpdateFAQResponse

```csharp
public sealed class UpdateFAQResponse
{
    public long FaqId { get; init; }

    public string FaqType { get; init; } = default!;
    public string FaqTypeLabel { get; init; } = default!;

    public string Question { get; init; } = default!;
    public string Answer { get; init; } = default!;

    public int DisplayOrder { get; init; }

    public string Status { get; init; } = default!;
    public string StatusLabel { get; init; } = default!;

    public DateTime CreatedAt { get; init; }
    public long? CreatedBy { get; init; }
    public string? CreatedByName { get; init; }

    public DateTime? UpdatedAt { get; init; }
    public long? UpdatedBy { get; init; }
    public string? UpdatedByName { get; init; }

    public bool Changed { get; init; }
}
```

Nếu project dùng `ulong`, đổi theo entity hiện có.

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

### 18.1. View detail handler

```csharp
public async Task<ViewFAQDetailDto> Handle(ViewFAQDetailQuery request, CancellationToken ct)
{
    var faq = await _dbContext.Faqs
        .AsNoTracking()
        .Where(x => x.FaqId == request.FaqId)
        .Select(x => new
        {
            x.FaqId,
            x.FaqType,
            x.Question,
            x.Answer,
            x.DisplayOrder,
            x.Status,
            x.CreatedAt,
            x.CreatedBy,
            x.UpdatedAt,
            x.UpdatedBy
        })
        .FirstOrDefaultAsync(ct);

    if (faq is null)
    {
        throw new NotFoundException("FAQ not found.");
    }

    // Nếu cần CreatedByName/UpdatedByName, join users hoặc query map user names.
    // Không gọi label helper trong EF projection nếu EF không translate được.

    return new ViewFAQDetailDto
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
```

### 18.2. Update handler

```csharp
public async Task<UpdateFAQResponse> Handle(UpdateFAQCommand request, CancellationToken ct)
{
    var faq = await _dbContext.Faqs
        .FirstOrDefaultAsync(x => x.FaqId == request.FaqId, ct);

    if (faq is null)
    {
        throw new NotFoundException("FAQ not found.");
    }

    var faqType = request.FaqType.Trim();
    var newQuestion = SanitizePlainText(request.Question).Trim();
    var newAnswer = SanitizePlainText(request.Answer).Trim();

    if (string.IsNullOrWhiteSpace(newQuestion))
    {
        throw new ValidationException("Question is required.");
    }

    if (string.IsNullOrWhiteSpace(newAnswer))
    {
        throw new ValidationException("Answer is required.");
    }

    var normalizedQuestion = newQuestion.ToLowerInvariant();

    var duplicateExists = await _dbContext.Faqs
        .AsNoTracking()
        .AnyAsync(x =>
            x.FaqId != request.FaqId &&
            x.Question.Trim().ToLower() == normalizedQuestion,
            ct);

    if (duplicateExists)
    {
        throw new ConflictException(
            "This question already exists in the system. Please enter a different question.");
    }

    var changed =
        faq.FaqType != faqType ||
        faq.Question != newQuestion ||
        faq.Answer != newAnswer;

    if (!changed)
    {
        return MapResponse(faq, changed: false);
    }

    faq.FaqType = faqType;
    faq.Question = newQuestion;
    faq.Answer = newAnswer;
    faq.UpdatedAt = _dateTimeProvider.Now;
    faq.UpdatedBy = _currentUser.UserId;

    await _dbContext.SaveChangesAsync(ct);

    return MapResponse(faq, changed: true);
}
```

Điều chỉnh theo service thật trong project:

```text
ICurrentUserService
IDateTimeProvider
SaveChangesAsync convention
Exception classes
```

Không gọi SaveChangesAsync nếu project dùng UnitOfWork/TransactionBehaviour tự động và convention hiện tại không gọi trong handler.

---

## 19. Business Rules

| ID | Rule |
|---|---|
| BR-64-01 | Chỉ HO được xem chi tiết và update FAQ. |
| BR-64-02 | `GET /api/faqs/{faqId}` trả cả FAQ `PUBLISHED` và `HIDDEN` cho HO. |
| BR-64-03 | `PUT /api/faqs/{faqId}` chỉ sửa `faqType`, `question`, `answer`. |
| BR-64-04 | UC-64 không sửa `status`; bật/tắt visibility là use case riêng. |
| BR-64-05 | Không nhận/trả `languageCode`. |
| BR-64-06 | `faqType` phải thuộc enum v10. |
| BR-64-07 | `question` và `answer` bắt buộc, sau trim/sanitize không được rỗng. |
| BR-64-08 | `question` không được trùng FAQ khác, so sánh trim + case-insensitive, loại trừ chính nó. |
| BR-64-09 | Question/Answer phải được sanitize HTML/script trước khi lưu. |
| BR-64-10 | Nếu dữ liệu mới khác dữ liệu hiện tại, cập nhật `updated_at`, `updated_by`. |
| BR-64-11 | Nếu không có thay đổi thật, không cập nhật `updated_at`, `updated_by`. |
| BR-64-12 | Response detail/update phải có `createdByName`, `updatedByName`, `createdAt`, `updatedAt` để màn detail hiển thị audit metadata. |
| BR-64-13 | Không dùng dynamic permissions/role_permissions. |
| BR-64-14 | Không query DbContext trong Controller. |

---

## 20. Alternative Flows

### AF-01 — FAQ không tồn tại

```text
GET /api/faqs/{faqId} hoặc PUT /api/faqs/{faqId}
faqId không tồn tại
-> 404 Not Found
```

### AF-02 — Chưa đăng nhập

```text
Không có token/session
-> 401 Unauthorized
```

### AF-03 — Không phải HO

```text
Role khác HO gọi API
-> 403 Forbidden
```

### AF-04 — Question rỗng

```text
question rỗng sau trim/sanitize
-> 400 Bad Request
-> Không cập nhật DB
```

### AF-05 — Answer rỗng

```text
answer rỗng sau trim/sanitize
-> 400 Bad Request
-> Không cập nhật DB
```

### AF-06 — FAQ type không hợp lệ

```text
faqType = VISA
-> 400 Bad Request
```

Vì `VISA` là type cũ, không còn hợp lệ nếu đang dùng schema v10 enum mới.

### AF-07 — Trùng câu hỏi với FAQ khác

```text
question mới trùng FAQ khác, không tính chính nó
-> 409 Conflict hoặc 400 validation theo convention
-> Không cập nhật DB
```

### AF-08 — Không thay đổi gì

```text
HO bấm Lưu nhưng faqType/question/answer sau sanitize giống DB
-> 200 OK
-> changed = false
-> Không cập nhật updated_at/updated_by
```

### AF-09 — Body cố tình gửi status

```text
Request PUT /api/faqs/{faqId} có field status
-> Backend bỏ qua field status hoặc reject tùy convention.
Khuyến nghị: DTO không có status nên model binding bỏ qua.
Quan trọng: status trong DB không được thay đổi bởi UC-64.
```

---

## 21. Verification Criteria

### VC-01 — HO xem detail FAQ

```text
Given HO đã đăng nhập
When HO gọi GET /api/faqs/{faqId}
Then backend trả detail gồm faqType, question, answer, status, createdByName, createdAt, updatedByName, updatedAt
```

### VC-02 — Update có thay đổi thật

```text
Given HO đã đăng nhập và FAQ tồn tại
When HO sửa faqType/question/answer rồi gọi PUT /api/faqs/{faqId}
Then backend cập nhật faq_type, question, answer
And updated_at được refresh
And updated_by = current HO user_id
And status giữ nguyên
And changed = true
```

### VC-03 — Không thay đổi gì

```text
Given HO mở edit nhưng không thay đổi gì
When HO bấm Lưu
Then backend trả changed = false
And không cập nhật updated_at
And không cập nhật updated_by
```

### VC-04 — Question rỗng

```text
Given question rỗng sau trim/sanitize
When HO bấm Lưu
Then trả 400
And không cập nhật DB
```

### VC-05 — Answer rỗng

```text
Given answer rỗng sau trim/sanitize
When HO bấm Lưu
Then trả 400
And không cập nhật DB
```

### VC-06 — Duplicate question với FAQ khác

```text
Given FAQ ID=200 có question "ABC"
When HO update FAQ ID=100 question thành " abc "
Then trả duplicate error
And không cập nhật DB
```

### VC-07 — Non-HO bị chặn

```text
Given user không phải HO
When gọi PUT /api/faqs/{faqId}
Then trả 403
```

### VC-08 — Không login bị chặn

```text
Given không có token
When gọi PUT /api/faqs/{faqId}
Then trả 401
```

### VC-09 — Update FAQ HIDDEN không đổi status

```text
Given HO update FAQ đang HIDDEN
When update question/answer/type thành công
Then status vẫn là HIDDEN
```

### VC-10 — Không nhận languageCode

```text
When gọi PUT /api/faqs/{faqId}
Then backend không yêu cầu, không lưu, không trả languageCode
```

### VC-11 — Sanitize trước khi so sánh

```text
Given DB question là "Điều kiện tham gia?"
When HO gửi question = " <b>Điều kiện tham gia?</b> "
Then sanitize ra cùng nội dung
And changed = false
And không update updated_at/updated_by
```

---

## 22. Manual test bằng curl/Postman

### 22.1. Không token — detail

```bash
curl -X GET "http://localhost:5265/api/faqs/1"
```

Expected:

```text
401 Unauthorized
```

### 22.2. Token HO — detail

```bash
curl -X GET "http://localhost:5265/api/faqs/1" \
  -H "Authorization: Bearer <HO_TOKEN>"
```

Expected:

```text
200 OK
Có createdByName, createdAt, updatedByName, updatedAt
```

### 22.3. Token non-HO — update

```bash
curl -X PUT "http://localhost:5265/api/faqs/1" \
  -H "Authorization: Bearer <STAFF_TOKEN>" \
  -H "Content-Type: application/json" \
  -d "{\"faqType\":\"ACCOUNT_ACCESS\",\"question\":\"Q?\",\"answer\":\"A\"}"
```

Expected:

```text
403 Forbidden
```

### 22.4. Token HO — update có thay đổi

```bash
curl -X PUT "http://localhost:5265/api/faqs/1" \
  -H "Authorization: Bearer <HO_TOKEN>" \
  -H "Content-Type: application/json" \
  -d "{\"faqType\":\"ACCOUNT_ACCESS\",\"question\":\"Câu hỏi đã cập nhật?\",\"answer\":\"Câu trả lời đã cập nhật.\"}"
```

Expected:

```text
200 OK
changed = true
updated_at/updated_by thay đổi
status giữ nguyên
```

### 22.5. Token HO — không thay đổi gì

```bash
curl -X PUT "http://localhost:5265/api/faqs/1" \
  -H "Authorization: Bearer <HO_TOKEN>" \
  -H "Content-Type: application/json" \
  -d "{\"faqType\":\"ACCOUNT_ACCESS\",\"question\":\"<same question>\",\"answer\":\"<same answer>\"}"
```

Expected:

```text
200 OK
changed = false
updated_at/updated_by không đổi
```

### 22.6. Invalid type

```bash
curl -X PUT "http://localhost:5265/api/faqs/1" \
  -H "Authorization: Bearer <HO_TOKEN>" \
  -H "Content-Type: application/json" \
  -d "{\"faqType\":\"VISA\",\"question\":\"Q?\",\"answer\":\"A\"}"
```

Expected:

```text
400 Bad Request
```

---

## 23. Những điều không được làm

```text
- Không code nhầm UC-64 thành UC-05 public FAQ.
- Không dùng PublicContentController.
- Không dùng /api/public/faqs.
- Không AllowAnonymous.
- Không cho Admin/Staff/Department/Student/Visitor update FAQ.
- Không update status trong UC-64.
- Không dùng enum type cũ Program/Tuition/Visa/Dormitory.
- Không dùng status cũ Visible/Hidden nếu DB v10 là PUBLISHED/HIDDEN.
- Không nhận/trả languageCode.
- Không lưu HTML/script vào question/answer.
- Không bỏ duplicate check.
- Không cập nhật updated_at/updated_by nếu không có thay đổi thật.
- Không query DbContext trong Controller.
- Không tạo bảng/cột mới nếu SQL v10 đã đủ.
- Không dùng dynamic permissions/role_permissions.
- Không sửa frontend trong task backend này nếu user chỉ yêu cầu backend.
- Không báo hoàn thành nếu chưa build/test hoặc chưa nói rõ phần chưa test được.
```

---

## 24. Definition of Done

AI Agent chỉ được báo hoàn thành khi đủ:

```text
[ ] Xác nhận đang triển khai UC-64: Update FAQ.
[ ] GET /api/faqs/{faqId} hoạt động.
[ ] PUT /api/faqs/{faqId} hoạt động.
[ ] Cả 2 endpoint yêu cầu authentication.
[ ] Cả 2 endpoint chỉ HO được gọi.
[ ] Non-HO bị 403.
[ ] Unauthenticated bị 401.
[ ] Detail response có createdByName, createdAt, updatedByName, updatedAt.
[ ] PUT chỉ update faqType, question, answer.
[ ] PUT không update status.
[ ] Validate faqType enum v10.
[ ] Validate question/answer required sau trim/sanitize.
[ ] Check duplicate question trim + case-insensitive, loại trừ chính nó.
[ ] Sanitize HTML/script trước khi so sánh/lưu.
[ ] Nếu có thay đổi thật thì updated_at/updated_by được cập nhật.
[ ] Nếu không có thay đổi thật thì updated_at/updated_by không đổi.
[ ] Không nhận/trả languageCode.
[ ] Không dùng public endpoint.
[ ] Không query DbContext trong Controller.
[ ] Có validator.
[ ] dotnet build PASS.
[ ] Có unit/integration test hoặc manual API test rõ ràng.
```

---

## 25. Output mong muốn từ AI Agent sau khi code

Sau khi code xong, báo cáo theo format:

```text
1. Files read
- ...

2. Files changed
- backend/PEMS.Api/Controllers/FaqsController.cs
- backend/PEMS.Application/Faqs/Commands/UpdateFAQ/...
- backend/PEMS.Application/Faqs/Queries/ViewFAQDetail/...
- backend/PEMS.Domain/Constants/FaqConstants.cs
- tests/...

3. Endpoints implemented
- GET /api/faqs/{faqId}
- PUT /api/faqs/{faqId}

4. Authorization
- HO only
- unauthenticated -> 401
- non-HO -> 403

5. Logic implemented
- detail loads metadata
- update faqType/question/answer only
- status unchanged
- duplicate question check
- sanitize question/answer
- no-change save does not touch updated_at/updated_by
- no languageCode

6. Validation
- faqId exists
- faqType enum v10
- question/answer required
- duplicate question

7. Test result
- dotnet build: PASS/FAIL
- dotnet test: PASS/FAIL
- Manual API test: PASS/FAIL

8. Notes / Risks
- ...
```
