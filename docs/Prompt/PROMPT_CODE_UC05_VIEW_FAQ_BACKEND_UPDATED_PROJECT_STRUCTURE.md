# PROMPT_CODE_UC05_VIEW_FAQ_BACKEND__UPDATED_BY_PROJECT_STRUCTURE.md

## 0. Mục tiêu

Triển khai/cập nhật backend cho **UC-05: View FAQ** của hệ thống **PEMS — Partnership Engagement Management System** theo **PROJECT_STRUCTURE_FULL.md mới nhất trên nhánh `Canh-Iter1`**.

Chức năng này là public API để frontend lấy danh sách FAQ công khai và tự group theo `faqType` để hiển thị accordion trên trang `/faq`.

---

## 1. Quyết định nghiệp vụ đã chốt

```text
UC ID: UC-05
UC Name: View FAQ
Public route bắt buộc: GET /api/public/faqs
Authentication: Không yêu cầu đăng nhập
Authorization: AllowAnonymous / Public endpoint
Response style: Backend trả flat items + pagination
Frontend: Tự group items theo faqType
Search: Dùng Contains / LIKE
Sort: display_order ASC, created_at DESC, faq_id DESC
```

Backend **không dùng FULLTEXT mặc định** cho UC này vì nghiệp vụ yêu cầu contains matching trên `question`, `answer`, `faq_type`.

---

## 2. Tài liệu bắt buộc phải đọc trước khi code

AI Agent phải đọc các file sau trước khi sửa code:

```text
docs/architecture/PROJECT_STRUCTURE_FULL.md
docs/database/DATABASE_SCHEMA_v8_4_refined_v6_v10_no_dynamic_permissions_FULL_UPDATED.md
docs/PEMS_CANONICAL_BUSINESS_RULES_v8_4_refined_v6_v10_FULL_UPDATED.md
docs/PEMS_UC_IMPLEMENTATION_RULEBOOK_FRONTEND_BACKEND_DATABASE_VALIDATION_SECURITY_v8_4_refined_v6_v10_FULL_UPDATED.md
docs/PROJECT_OVERVIEW_v8_4_refined_v6_v10_FULL_UPDATED.md
docs/use-cases/USE_CASE_LIST.md
docs/use-cases/USE_CASE_NOTES.md
```

Nếu path docs thực tế khác nhau giữa `docs/architecture/`, `docs/GUIDE CLAUDE/architecture/`, hoặc file ở root docs, ưu tiên bản mới nhất đang nằm trong `docs/architecture/PROJECT_STRUCTURE_FULL.md`.

Nếu có mâu thuẫn giữa tài liệu cũ và schema v10, **ưu tiên schema v10**.

---

## 3. Cập nhật quan trọng theo PROJECT_STRUCTURE_FULL mới nhất

### 3.1. Không tạo bừa controller/module mới

Project hiện tại đã có:

```text
backend/PEMS.Api/Controllers/PublicContentController.cs
backend/PEMS.Api/Controllers/FaqsController.cs
```

Vì UC-05 là **public content**, AI Agent nên ưu tiên cập nhật/tạo action trong:

```text
backend/PEMS.Api/Controllers/PublicContentController.cs
```

Không ưu tiên tạo controller mới `PublicFaqsController.cs` nếu `PublicContentController.cs` đã có thể đảm nhiệm route này.

Route cuối cùng vẫn phải là:

```http
GET /api/public/faqs
```

Cách route khuyến nghị:

```csharp
[ApiController]
[Route("api/public")]
public sealed class PublicContentController : ControllerBase
{
    [HttpGet("faqs")]
    [AllowAnonymous]
    public async Task<IActionResult> GetFaqs(...)
}
```

Nếu controller hiện tại đã có route prefix khác, hãy chỉnh sao cho endpoint public cuối cùng đúng chính xác:

```text
/api/public/faqs
```

---

### 3.2. Application layer hiện đã có PublicContent/ViewFaq

Project structure hiện tại đã có module public content:

```text
backend/PEMS.Application/PublicContent/
├── Dtos/
├── Mappings/
│   └── PublicContentMappingProfile.cs
└── Queries/
    ├── SearchInformation/
    ├── ViewContactInfo/
    ├── ViewFaq/
    │   ├── ViewFaqDto.cs
    │   ├── ViewFaqQuery.cs
    │   └── ViewFaqQueryHandler.cs
    ├── ViewGallery/
    ├── ViewHomepage/
    ├── ViewNews/
    ├── ViewNotifications/
    ├── ViewPartners/
    └── ViewPolicyAndTerms/
```

Vì vậy **không tạo folder mới `ViewPublicFaqs` nếu không cần**.

Cần cập nhật trực tiếp các file hiện có:

```text
backend/PEMS.Application/PublicContent/Queries/ViewFaq/ViewFaqDto.cs
backend/PEMS.Application/PublicContent/Queries/ViewFaq/ViewFaqQuery.cs
backend/PEMS.Application/PublicContent/Queries/ViewFaq/ViewFaqQueryHandler.cs
```

Nếu chưa có validator, tạo thêm:

```text
backend/PEMS.Application/PublicContent/Queries/ViewFaq/ViewFaqQueryValidator.cs
```

---

### 3.3. Phân biệt public FAQ và FAQ management nội bộ

Project hiện có module FAQ management riêng:

```text
backend/PEMS.Application/Faqs/
├── Commands/
│   ├── ChangeFAQVisibility/
│   ├── CreateFAQ/
│   └── UpdateFAQ/
└── Queries/
    ├── SearchFAQ/
    └── ViewListFAQ/
```

Các file này phục vụ màn quản lý FAQ nội bộ như UC-64 đến UC-68.

UC-05 **không dùng** `Faqs/Queries/ViewListFAQ` làm response public nếu query đó trả cả HIDDEN/status/audit fields.

UC-05 phải nằm ở:

```text
PEMS.Application/PublicContent/Queries/ViewFaq
```

hoặc logic public tương đương, để đảm bảo:

```text
- Chỉ trả status = PUBLISHED.
- Không trả HIDDEN.
- Không trả status/audit/admin fields.
- Không yêu cầu đăng nhập.
```

---

### 3.4. Domain/Infrastructure path hiện tại

Project hiện có entity FAQ:

```text
backend/PEMS.Domain/Entities/Faqs/Faq.cs
```

Project hiện có enum visibility:

```text
backend/PEMS.Domain/Enums/FaqVisibilityStatus.cs
```

Project hiện có constants folder:

```text
backend/PEMS.Domain/Constants/
├── AuthConstants.cs
├── EmailActionConstants.cs
├── LogisticsHandoverConstants.cs
├── VisitParticipantConstants.cs
└── VisitRequestConstants.cs
```

Nếu cần constants cho FAQ type/status, có thể tạo:

```text
backend/PEMS.Domain/Constants/FaqConstants.cs
```

Nhưng chỉ tạo nếu thật sự cần và không trùng với enum/constants hiện có.

Project hiện tại trong Infrastructure có:

```text
backend/PEMS.Infrastructure/Persistence/ApplicationDbContext.cs
backend/PEMS.Infrastructure/Persistence/ApplicationDbContextFactory.cs
backend/PEMS.Infrastructure/Persistence/Configurations/UserConfiguration.cs
```

Nếu FAQ mapping đã nằm trong `ApplicationDbContext.cs`, không tạo mapping trùng.

Nếu FAQ mapping thiếu hoặc entity chưa khớp schema v10, có thể tạo/cập nhật:

```text
backend/PEMS.Infrastructure/Persistence/Configurations/FaqConfiguration.cs
```

và đăng ký trong `OnModelCreating`.

---

### 3.5. Test path hiện tại

Project hiện có test public content:

```text
tests/PEMS.ApplicationTests/PublicContent/ViewFAQQueryTests.cs
```

Cần cập nhật test này thay vì tạo test mới sai chỗ.

Có thể cần cập nhật thêm:

```text
tests/PEMS.ApplicationTests/Faqs/ViewListFAQQueryTests.cs
```

nhưng chỉ nếu thay đổi shared DTO/query làm ảnh hưởng FAQ management nội bộ.

---

### 3.6. Frontend path hiện tại, chỉ để đối chiếu contract

Task này là backend, nhưng nếu cần đối chiếu frontend contract thì đọc:

```text
frontend/pems-react/src/features/public-content/
├── adapters/publicContentAdapter.ts
├── api/publicContentApi.ts
├── hooks/usePublicContent.ts
└── types/publicContent.types.ts
```

Không sửa frontend trong task này nếu user chỉ yêu cầu backend.

Nếu backend response shape thay đổi so với frontend hiện tại, phải báo rõ cần đồng bộ frontend sau.

---

## 4. Source of truth về FAQ theo schema v10

FAQ hiện tại theo v10 có các rule quan trọng:

```text
- FAQ chỉ dùng tiếng Việt.
- Không còn faqs.language_code.
- Backend không nhận và không trả languageCode.
- Public FAQ chỉ lấy status = PUBLISHED.
- FAQ HIDDEN không được lộ ra public API trong bất kỳ trường hợp nào.
- faq_type dùng enum hệ thống mới, không dùng category cũ như Program/Tuition Fee/Visa/Dormitory.
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

Không dùng các enum/category cũ:

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

---

## 5. Endpoint bắt buộc

```http
GET /api/public/faqs
```

Yêu cầu:

```text
- Endpoint phải AllowAnonymous.
- Không yêu cầu JWT token.
- Không dùng RoleAuthorizeAttribute.
- Không dùng PermissionAuthorizeAttribute.
- Không dùng dynamic permissions.
- Không nhận status từ client.
- Không nhận includeHidden từ client.
- Không trả FAQ status = HIDDEN.
```

Controller chỉ được:

```text
1. Nhận query params.
2. Gọi IMediator.Send(query).
3. Trả ApiResponse/PagedResult/PaginatedResult theo convention hiện có.
```

Controller không được:

```text
- Query DbContext trực tiếp.
- Viết logic search/filter/sort trong Controller.
- Check business rule phức tạp trong Controller.
```

---

## 6. Query parameters

API nhận các query params sau:

```http
GET /api/public/faqs?keyword=otp&faqType=ACCOUNT_ACCESS&page=1&pageSize=10
```

| Param | Type | Required | Default | Rule |
|---|---:|---:|---:|---|
| `keyword` | string | No | null | Trim trước khi search. Nếu null/rỗng thì không áp dụng keyword filter. |
| `faqType` | string | No | null / ALL | Nếu null hoặc `ALL` thì không lọc type. Nếu có giá trị khác thì phải thuộc enum v10. |
| `page` | int | No | 1 | Phải >= 1. |
| `pageSize` | int | No | 10 | Phải trong khoảng 1–50. |

Không nhận các param sau:

```text
status
languageCode
createdBy
updatedBy
includeHidden
```

---

## 7. Response contract

Backend trả **flat items + pagination**. Frontend tự group items theo `faqType`.

Ví dụ response:

```json
{
  "success": true,
  "message": "Public FAQs loaded successfully.",
  "data": {
    "items": [
      {
        "faqId": 1,
        "faqType": "ACCOUNT_ACCESS",
        "faqTypeLabel": "Tài khoản và truy cập",
        "question": "Làm sao để đăng nhập hệ thống?",
        "answer": "Bạn có thể đăng nhập bằng tài khoản được cấp hoặc Google SSO.",
        "displayOrder": 1,
        "createdAt": "2026-06-24T10:00:00"
      }
    ],
    "pagination": {
      "page": 1,
      "pageSize": 10,
      "totalItems": 8,
      "totalPages": 1,
      "hasPrevious": false,
      "hasNext": false
    }
  }
}
```

Nếu project hiện tại dùng `PagedResult<T>` hoặc `PaginatedResult<T>`, hãy giữ đúng wrapper hiện có. Không tự tạo wrapper mới nếu đã có:

```text
backend/PEMS.Application/Common/Models/PagedResult.cs
backend/PEMS.Application/Common/Models/PaginatedResult.cs
backend/PEMS.Application/Common/Models/PaginationRequest.cs
```

Public DTO chỉ nên có:

```text
faqId
faqType
faqTypeLabel
question
answer
displayOrder
createdAt
```

Public DTO không được trả:

```text
status
languageCode
createdBy
updatedBy
updatedAt
internalNote
```

---

## 8. Search/filter/sort logic bắt buộc

### 8.1. Base query

Tất cả query public FAQ phải bắt đầu bằng điều kiện:

```sql
WHERE status = 'PUBLISHED'
```

Không được để client truyền status.

### 8.2. FAQ type filter

Nếu `faqType` là null/rỗng/`ALL`:

```text
Không áp dụng filter faq_type.
```

Nếu `faqType` có giá trị cụ thể:

```sql
AND faq_type = @faqType
```

Nếu `faqType` không nằm trong enum v10:

```text
Return 400 Bad Request.
Message gợi ý: "FAQ type is invalid."
```

### 8.3. Keyword search

Nếu `keyword` null/rỗng sau khi trim:

```text
Không áp dụng keyword filter.
```

Nếu có keyword:

```sql
AND (
  question LIKE CONCAT('%', @keyword, '%')
  OR answer LIKE CONCAT('%', @keyword, '%')
  OR faq_type LIKE CONCAT('%', @keyword, '%')
)
```

Yêu cầu:

```text
- Dùng Contains / LIKE.
- Không dùng FULLTEXT mặc định.
- Search trên question, answer, faq_type.
- Search kết hợp với faqType bằng AND logic.
- Trim leading/trailing whitespace của keyword.
- DB dùng utf8mb4_unicode_ci nên LIKE thường đã case-insensitive; không ép LOWER nếu không cần.
```

### 8.4. Sort

Sort cố định:

```sql
ORDER BY display_order ASC, created_at DESC, faq_id DESC
```

Giải thích:

```text
- display_order ASC: HO có thể điều chỉnh thứ tự hiển thị.
- created_at DESC: nếu cùng display_order thì FAQ mới hơn đứng trước.
- faq_id DESC: tie-breaker để thứ tự ổn định khi trùng display_order và created_at.
```

### 8.5. Paging

```text
offset = (page - 1) * pageSize
take = pageSize
```

---

## 9. File backend cần sửa/tạo theo project structure mới

### 9.1. Ưu tiên sửa file hiện có

```text
backend/PEMS.Api/Controllers/PublicContentController.cs

backend/PEMS.Application/PublicContent/Queries/ViewFaq/ViewFaqDto.cs
backend/PEMS.Application/PublicContent/Queries/ViewFaq/ViewFaqQuery.cs
backend/PEMS.Application/PublicContent/Queries/ViewFaq/ViewFaqQueryHandler.cs

tests/PEMS.ApplicationTests/PublicContent/ViewFAQQueryTests.cs
```

### 9.2. Có thể tạo thêm nếu chưa có

```text
backend/PEMS.Application/PublicContent/Queries/ViewFaq/ViewFaqQueryValidator.cs
backend/PEMS.Domain/Constants/FaqConstants.cs
backend/PEMS.Infrastructure/Persistence/Configurations/FaqConfiguration.cs
```

Chỉ tạo các file trên nếu thật sự thiếu trong code hiện tại.

### 9.3. Có thể phải cập nhật nếu schema mapping thiếu

```text
backend/PEMS.Application/Common/Interfaces/IApplicationDbContext.cs
backend/PEMS.Infrastructure/Persistence/ApplicationDbContext.cs
backend/PEMS.Infrastructure/DependencyInjection.cs
backend/PEMS.Application/DependencyInjection.cs
```

Chỉ cập nhật `DependencyInjection.cs` nếu cần đăng ký validator/mapping/service mới và project chưa auto-register.

---

## 10. Không dùng nhầm file/module

Không dùng các file sau làm public endpoint UC-05 nếu chúng là API quản lý nội bộ:

```text
backend/PEMS.Api/Controllers/FaqsController.cs
backend/PEMS.Application/Faqs/Queries/ViewListFAQ/ViewListFAQQuery.cs
backend/PEMS.Application/Faqs/Queries/SearchFAQ/SearchFAQQuery.cs
```

Các file trên phục vụ FAQ Management, có thể trả status hoặc dữ liệu quản trị. UC-05 chỉ trả dữ liệu public.

---

## 11. Constants/enum đề xuất

### 11.1. Ưu tiên kiểm tra enum có sẵn

Kiểm tra trước:

```text
backend/PEMS.Domain/Enums/FaqVisibilityStatus.cs
```

Nếu enum đã có `PUBLISHED` / `HIDDEN`, dùng enum/constant hiện có.

Nếu enum đang dùng tên cũ `Visible` / `Hidden`, phải kiểm tra mapping entity/database để đảm bảo DB vẫn lưu đúng:

```text
PUBLISHED
HIDDEN
```

Không tự đổi enum nếu làm vỡ các handler FAQ management. Nếu mismatch, báo rõ và sửa đồng bộ.

### 11.2. FaqConstants nếu cần tạo mới

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
}
```

---

## 12. DTO đề xuất cho file hiện có `ViewFaqDto.cs`

Cập nhật DTO hiện có theo public response:

```csharp
public sealed class ViewFaqDto
{
    public ulong FaqId { get; init; }
    public string FaqType { get; init; } = default!;
    public string FaqTypeLabel { get; init; } = default!;
    public string Question { get; init; } = default!;
    public string Answer { get; init; } = default!;
    public int DisplayOrder { get; init; }
    public DateTime CreatedAt { get; init; }
}
```

Nếu project dùng `long` thay vì `ulong` cho `BIGINT UNSIGNED`, hãy theo convention hiện tại của entity `Faq.cs`. Không tự đổi toàn bộ kiểu dữ liệu nếu project đã thống nhất.

---

## 13. Query object đề xuất cho file hiện có `ViewFaqQuery.cs`

```csharp
public sealed record ViewFaqQuery(
    string? Keyword,
    string? FaqType,
    int Page = 1,
    int PageSize = 10
) : IRequest<PagedResult<ViewFaqDto>>;
```

Nếu project đang dùng class thay vì record, giữ convention hiện tại.

Nếu project đang dùng `PaginatedResult<T>` thay vì `PagedResult<T>`, dùng wrapper đang có trong code.

---

## 14. Validator đề xuất

Tạo:

```text
backend/PEMS.Application/PublicContent/Queries/ViewFaq/ViewFaqQueryValidator.cs
```

Nội dung logic:

```csharp
public sealed class ViewFaqQueryValidator : AbstractValidator<ViewFaqQuery>
{
    public ViewFaqQueryValidator()
    {
        RuleFor(x => x.Page)
            .GreaterThanOrEqualTo(1)
            .WithMessage("Page must be greater than or equal to 1.");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 50)
            .WithMessage("Page size must be between 1 and 50.");

        RuleFor(x => x.FaqType)
            .Must(BeValidFaqType)
            .WithMessage("FAQ type is invalid.");
    }

    private static bool BeValidFaqType(string? faqType)
    {
        if (string.IsNullOrWhiteSpace(faqType))
        {
            return true;
        }

        var normalized = faqType.Trim();

        if (string.Equals(normalized, "ALL", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return FaqConstants.Type.All.Contains(normalized);
    }
}
```

Nếu không tạo `FaqConstants`, dùng enum/helper hiện có nhưng vẫn phải validate đủ enum v10.

---

## 15. Handler pseudo-code theo Clean Architecture

Application layer nên inject interface, không phụ thuộc trực tiếp Infrastructure.

Ưu tiên:

```csharp
private readonly IApplicationDbContext _dbContext;
```

Không ưu tiên inject trực tiếp:

```csharp
ApplicationDbContext
```

Pseudo-code:

```csharp
public sealed class ViewFaqQueryHandler
    : IRequestHandler<ViewFaqQuery, PagedResult<ViewFaqDto>>
{
    private readonly IApplicationDbContext _dbContext;

    public ViewFaqQueryHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<PagedResult<ViewFaqDto>> Handle(
        ViewFaqQuery request,
        CancellationToken cancellationToken)
    {
        var page = request.Page;
        var pageSize = request.PageSize;

        var keyword = request.Keyword?.Trim();
        var faqType = request.FaqType?.Trim();

        var query = _dbContext.Faqs
            .AsNoTracking()
            .Where(x => x.Status == FaqConstants.Status.Published);

        if (!string.IsNullOrWhiteSpace(faqType) &&
            !string.Equals(faqType, "ALL", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(x => x.FaqType == faqType);
        }

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var pattern = $"%{keyword}%";

            query = query.Where(x =>
                EF.Functions.Like(x.Question, pattern) ||
                EF.Functions.Like(x.Answer, pattern) ||
                EF.Functions.Like(x.FaqType, pattern));
        }

        var totalItems = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderBy(x => x.DisplayOrder)
            .ThenByDescending(x => x.CreatedAt)
            .ThenByDescending(x => x.FaqId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new ViewFaqDto
            {
                FaqId = x.FaqId,
                FaqType = x.FaqType,
                FaqTypeLabel = FaqConstants.ToVietnameseTypeLabel(x.FaqType),
                Question = x.Question,
                Answer = x.Answer,
                DisplayOrder = x.DisplayOrder,
                CreatedAt = x.CreatedAt
            })
            .ToListAsync(cancellationToken);

        return PagedResult<ViewFaqDto>.Create(
            items,
            page,
            pageSize,
            totalItems);
    }
}
```

Điều chỉnh tên property theo entity `Faq.cs` thật. Không đoán bừa nếu entity đang dùng tên khác.

---

## 16. Controller pseudo-code theo project structure mới

Cập nhật:

```text
backend/PEMS.Api/Controllers/PublicContentController.cs
```

Pseudo-code:

```csharp
[ApiController]
[Route("api/public")]
public sealed class PublicContentController : ControllerBase
{
    private readonly IMediator _mediator;

    public PublicContentController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet("faqs")]
    [AllowAnonymous]
    public async Task<IActionResult> GetFaqs(
        [FromQuery] ViewFaqQuery query,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(query, cancellationToken);
        return Ok(ApiResponse.Success(result, "Public FAQs loaded successfully."));
    }
}
```

Nếu `PublicContentController.cs` đã có constructor/action khác, chỉ thêm action mới hoặc chỉnh action FAQ hiện có, không phá các public content endpoint khác.

Không gắn:

```csharp
[RoleAuthorize(...)]
[Authorize]
[PermissionAuthorize(...)]
```

cho UC-05 public FAQ.

---

## 17. Business Rules

| ID | Rule |
|---|---|
| BR-05-01 | `GET /api/public/faqs` là public endpoint, không yêu cầu login. |
| BR-05-02 | Backend luôn lọc `status = PUBLISHED`. |
| BR-05-03 | Không bao giờ trả FAQ `HIDDEN` ở public endpoint. |
| BR-05-04 | Không nhận/trả `languageCode`. |
| BR-05-05 | Backend trả flat `items`, frontend tự group theo `faqType`. |
| BR-05-06 | Search dùng `LIKE/Contains`, case-insensitive theo DB collation. |
| BR-05-07 | Search trên `question`, `answer`, `faq_type`. |
| BR-05-08 | Search kết hợp với `faqType` bằng AND logic. |
| BR-05-09 | Sort cố định: `display_order ASC`, `created_at DESC`, `faq_id DESC`. |
| BR-05-10 | Public response không trả audit/admin fields. |
| BR-05-11 | Không dùng dynamic permission cho UC này vì schema v10 đã bỏ `permissions` và `role_permissions`. |
| BR-05-12 | Không query DB trong Controller; logic nằm trong Application Handler. |
| BR-05-13 | Không dùng `FaqsController`/FAQ Management query làm public response nếu response đó có status/audit/internal fields. |

---

## 18. Security requirements

```text
- Endpoint AllowAnonymous nhưng vẫn đi qua global security headers/middleware.
- Không trả HIDDEN FAQ.
- Không nhận status/includeHidden từ client.
- Không trả audit/admin fields.
- Không log raw keyword theo cách gây lộ dữ liệu nhạy cảm nếu project có logging query.
- Không dùng raw SQL concat string từ input.
- Nếu dùng LIKE pattern, truyền qua EF parameterization.
- Nên dùng rate limit nhẹ theo rule public endpoint hiện có.
```

---

## 19. Performance requirements

```text
- Dùng AsNoTracking().
- Dùng Select projection sang DTO.
- Không Include entity liên quan nếu không cần.
- CountAsync trước paging.
- Apply Where trước Count/Skip/Take.
- Không load toàn bộ FAQ rồi filter trên memory.
- PageSize tối đa 50.
```

---

## 20. Verification Criteria / Test cases

Cập nhật/tạo test trong:

```text
tests/PEMS.ApplicationTests/PublicContent/ViewFAQQueryTests.cs
```

### VC-01 — Public user xem FAQ không cần login

```text
Given có 3 FAQ status = PUBLISHED và 1 FAQ status = HIDDEN
When unauthenticated user gọi GET /api/public/faqs
Then response HTTP 200
And trả đúng 3 FAQ PUBLISHED
And không trả FAQ HIDDEN
And không yêu cầu JWT token
```

### VC-02 — Không lộ HIDDEN qua search

```text
Given có FAQ HIDDEN với question chứa "secret"
When gọi GET /api/public/faqs?keyword=secret
Then response HTTP 200
And items = []
```

### VC-03 — Search trong answer

```text
Given có FAQ PUBLISHED có answer chứa "OTP"
When gọi GET /api/public/faqs?keyword=OTP
Then FAQ đó xuất hiện trong response
```

### VC-04 — Filter faqType

```text
Given có FAQ PUBLISHED thuộc ACCOUNT_ACCESS và VISIT_REQUEST
When gọi GET /api/public/faqs?faqType=ACCOUNT_ACCESS
Then chỉ FAQ ACCOUNT_ACCESS được trả
```

### VC-05 — Search + faqType dùng AND logic

```text
Given có:
- FAQ A: faqType = ACCOUNT_ACCESS, answer chứa "OTP"
- FAQ B: faqType = VISIT_REQUEST, answer chứa "OTP"

When gọi GET /api/public/faqs?faqType=ACCOUNT_ACCESS&keyword=OTP
Then chỉ FAQ A được trả
And FAQ B không xuất hiện
```

### VC-06 — Reject enum cũ

```text
When gọi GET /api/public/faqs?faqType=VISA
Then response HTTP 400
And message chứa "FAQ type is invalid"
```

### VC-07 — Không có languageCode/status/audit fields

```text
When gọi GET /api/public/faqs
Then mỗi item không có:
- languageCode
- status
- createdBy
- updatedBy
- updatedAt
```

### VC-08 — Sort đúng

```text
Given có nhiều FAQ PUBLISHED với display_order khác nhau
When gọi GET /api/public/faqs
Then thứ tự là:
display_order ASC
then created_at DESC
then faq_id DESC
```

### VC-09 — Paging đúng

```text
Given có 25 FAQ PUBLISHED
When gọi GET /api/public/faqs?page=2&pageSize=10
Then trả 10 items của page 2
And totalItems = 25
And totalPages = 3
And hasPrevious = true
And hasNext = true
```

### VC-10 — Page/pageSize invalid

```text
When gọi GET /api/public/faqs?page=0&pageSize=500
Then response HTTP 400
```

---

## 21. Manual test bằng curl/Postman

```bash
curl -X GET "http://localhost:5265/api/public/faqs"
```

```bash
curl -X GET "http://localhost:5265/api/public/faqs?keyword=otp"
```

```bash
curl -X GET "http://localhost:5265/api/public/faqs?faqType=ACCOUNT_ACCESS"
```

```bash
curl -X GET "http://localhost:5265/api/public/faqs?faqType=ACCOUNT_ACCESS&keyword=otp&page=1&pageSize=10"
```

```bash
curl -X GET "http://localhost:5265/api/public/faqs?faqType=VISA"
```

Expected:

```text
- Các request hợp lệ trả 200.
- faqType=VISA trả 400.
- Không response nào trả HIDDEN FAQ.
- Không response nào có languageCode/status/audit fields trong item public.
```

---

## 22. Những điều không được làm

```text
- Không dùng /api/public-content/faqs; route đã chốt là /api/public/faqs.
- Không tạo PublicFaqsController nếu PublicContentController hiện tại đã đủ dùng.
- Không trả grouped response từ backend.
- Không dùng FULLTEXT mặc định.
- Không nhận/trả languageCode.
- Không dùng category cũ: Program/Tuition Fee/Visa/Dormitory.
- Không cho client truyền status/includeHidden.
- Không trả HIDDEN FAQ.
- Không query DbContext trong Controller.
- Không dùng RoleAuthorizeAttribute cho endpoint public này.
- Không tạo dynamic permission cho UC này.
- Không tạo bảng/cột mới nếu SQL v10 đã đủ.
- Không sửa frontend trong task backend này nếu user chỉ yêu cầu backend.
- Không mock data.
- Không tạo test sai project; ưu tiên tests/PEMS.ApplicationTests/PublicContent/ViewFAQQueryTests.cs.
- Không báo hoàn thành nếu chưa build/test hoặc chưa nói rõ lý do không chạy được.
```

---

## 23. Definition of Done

AI Agent chỉ được báo hoàn thành khi đã làm đủ:

```text
[ ] Cập nhật đúng PublicContentController.cs để có GET /api/public/faqs.
[ ] Endpoint AllowAnonymous, không yêu cầu token.
[ ] Cập nhật đúng PublicContent/Queries/ViewFaq hiện có.
[ ] Query chỉ lấy status = PUBLISHED.
[ ] Không nhận/trả languageCode.
[ ] Validate faqType enum v10.
[ ] Search bằng LIKE/Contains trên question, answer, faq_type.
[ ] Search + faqType kết hợp bằng AND logic.
[ ] Sort display_order ASC, created_at DESC, faq_id DESC.
[ ] Trả flat items + pagination.
[ ] Không trả status/audit/admin fields.
[ ] Dùng AsNoTracking + projection DTO.
[ ] Không query DB trong Controller.
[ ] Có validator hoặc validation tương đương theo convention.
[ ] Cập nhật tests/PEMS.ApplicationTests/PublicContent/ViewFAQQueryTests.cs.
[ ] dotnet build PASS trên PEMS.slnx hoặc project backend tương ứng.
[ ] Báo cáo rõ files changed, endpoint, test result.
```

---

## 24. Output mong muốn từ AI Agent sau khi code

Sau khi code xong, AI Agent phải báo cáo theo format:

```text
1. Files read
- docs/architecture/PROJECT_STRUCTURE_FULL.md
- DATABASE_SCHEMA...
- ...

2. Files changed
- backend/PEMS.Api/Controllers/PublicContentController.cs
- backend/PEMS.Application/PublicContent/Queries/ViewFaq/...
- tests/PEMS.ApplicationTests/PublicContent/ViewFAQQueryTests.cs
- ...

3. Endpoint implemented
- GET /api/public/faqs

4. Logic implemented
- status = PUBLISHED only
- flat items + pagination
- LIKE search
- sort display_order ASC, created_at DESC, faq_id DESC

5. Validation
- page/pageSize
- faqType enum v10

6. Security
- AllowAnonymous
- no hidden FAQ leak
- no audit fields in public response
- no role/dynamic permission required

7. Test result
- dotnet build: PASS/FAIL
- Application tests: PASS/FAIL
- API manual tests: PASS/FAIL
- Notes if any
```
