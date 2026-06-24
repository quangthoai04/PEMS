# PROMPT_CODE_UC62_VIEW_LIST_FAQ_BACKEND.md

## 0. Mục tiêu

Triển khai backend cho **UC-62: View List FAQ** của hệ thống **PEMS — Partnership Engagement Management System**.

Đây là use case dành cho màn **Quản lý FAQ** của **HO** tại:

```text
/dashboard/faq
```

Màn hình tương ứng là bảng quản lý FAQ giống giao diện người dùng đã cung cấp: có ô tìm kiếm, filter loại FAQ, filter trạng thái, bảng danh sách, phân trang, nút xem chi tiết, nút bật/tắt hiển thị và nút thêm mới FAQ.

---

## 1. Quyết định đã chốt

```text
UC ID: UC-62
UC Name: View List FAQ
Module: FAQ Management
Primary Actor: HO
Page: /dashboard/faq
Endpoint: GET /api/faqs
Allowed role: HO only
Default sort: created_at DESC, faq_id DESC
Authentication: Required
Authorization: Chỉ HO tuyệt đối
Response style: Flat items + pagination
Search: Contains / LIKE
```

Lưu ý quan trọng:

```text
- Theo Report 3.1, UC-62: View List FAQ là use case đúng.
- Không đổi UC này thành UC-64.
- Không code nhầm sang UC-05 View FAQ public.
```

---

## 2. Khác biệt với UC-05 View FAQ public

| Tiêu chí | UC-05 View FAQ public | UC-62 View List FAQ management |
|---|---|---|
| Route | `GET /api/public/faqs` | `GET /api/faqs` |
| Page | Public FAQ page | `/dashboard/faq` |
| Actor | Public user | HO |
| Auth | Không cần đăng nhập | Bắt buộc đăng nhập |
| Role | Không check role | Chỉ HO |
| Status lấy dữ liệu | Chỉ `PUBLISHED` | Mặc định lấy cả `PUBLISHED` và `HIDDEN` |
| Mục đích | Người dùng đọc FAQ | HO quản lý FAQ |
| Response | Không trả status/audit fields | Có trả status và một số audit fields cần cho quản lý |
| Controller | `PublicContentController` | `FaqsController` |
| Application module | `PublicContent/Queries/ViewFaq` | `Faqs/Queries/ViewListFAQ` |

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
1. Ưu tiên Report 3.1 về UC ID: UC-62 View List FAQ.
2. Ưu tiên schema v10 về bảng/cột/enum/status.
3. Ưu tiên PROJECT_STRUCTURE_FULL.md mới nhất về đường dẫn file thật.
```

---

## 4. Source of truth theo schema v10

FAQ hiện tại theo SQL v10:

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

Không dùng các type cũ trong backend v10:

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

Nếu frontend hiện vẫn hiển thị label cũ như “Chương trình”, “Học phí”, “Visa”, “Ký túc xá”, đó là UI/data legacy cần cập nhật sau. Backend UC-62 phải theo enum v10.

---

## 5. Endpoint bắt buộc

```http
GET /api/faqs
```

Ví dụ:

```http
GET /api/faqs?page=1&pageSize=5
GET /api/faqs?keyword=otp
GET /api/faqs?faqType=ACCOUNT_ACCESS
GET /api/faqs?status=PUBLISHED
GET /api/faqs?keyword=otp&faqType=ACCOUNT_ACCESS&status=PUBLISHED&page=1&pageSize=5
```

Yêu cầu:

```text
- Endpoint phải yêu cầu authentication.
- Endpoint chỉ cho HO.
- Không AllowAnonymous.
- Không dùng route /api/public/faqs.
- Không lọc cứng status = PUBLISHED.
- Mặc định lấy cả PUBLISHED và HIDDEN.
```

---

## 6. Controller cần dùng

Theo project structure mới, project đã có:

```text
backend/PEMS.Api/Controllers/FaqsController.cs
backend/PEMS.Api/Controllers/PublicContentController.cs
```

UC-62 là quản lý FAQ nội bộ, nên phải dùng:

```text
backend/PEMS.Api/Controllers/FaqsController.cs
```

Không dùng:

```text
backend/PEMS.Api/Controllers/PublicContentController.cs
```

vì file đó dành cho public content / UC-05.

Pseudo-code controller:

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

    [HttpGet]
    [RoleAuthorize(RoleCodes.HO)]
    public async Task<IActionResult> GetFaqs(
        [FromQuery] ViewListFAQQuery query,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(query, cancellationToken);
        return Ok(ApiResponse.Success(result, "FAQ list loaded successfully."));
    }
}
```

Nếu project hiện có convention khác cho role authorize, hãy dùng đúng attribute/helper hiện có, nhưng logic cuối cùng phải là:

```text
Only HO can access.
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

---

## 7. Query parameters

| Param | Type | Required | Default | Rule |
|---|---:|---:|---:|---|
| `keyword` | string | No | null | Trim. Nếu rỗng thì không search. Search LIKE trên `question`, `answer`, `faq_type`. |
| `faqType` | string | No | `ALL` | `ALL` hoặc enum v10 hợp lệ. |
| `status` | string | No | `ALL` | `ALL`, `PUBLISHED`, `HIDDEN`. |
| `page` | int | No | `1` | Phải >= 1. |
| `pageSize` | int | No | `5` | Phải trong khoảng 1–50. |
| `sortBy` | string | No | `createdAt` | Giai đoạn này hỗ trợ `createdAt`, có thể hỗ trợ thêm `displayOrder`. |
| `sortDirection` | string | No | `desc` | `asc` hoặc `desc`. |

Default cho màn quản lý theo Report:

```text
page = 1
pageSize = 5
sortBy = createdAt
sortDirection = desc
```

Sort SQL thực tế khi default:

```sql
ORDER BY created_at DESC, faq_id DESC
```

---

## 8. Response contract

Backend trả flat items + pagination.

Ví dụ:

```json
{
  "success": true,
  "message": "FAQ list loaded successfully.",
  "data": {
    "items": [
      {
        "faqId": 1,
        "faqType": "ACCOUNT_ACCESS",
        "faqTypeLabel": "Tài khoản và truy cập",
        "question": "Làm sao để đăng nhập hệ thống?",
        "answer": "Bạn có thể đăng nhập bằng tài khoản được cấp hoặc Google SSO.",
        "displayOrder": 1,
        "status": "PUBLISHED",
        "statusLabel": "Hiển thị",
        "createdAt": "2026-06-24T10:00:00",
        "createdBy": 2,
        "createdByName": "Head Office",
        "updatedAt": "2026-06-24T11:00:00",
        "updatedBy": 2,
        "updatedByName": "Head Office"
      }
    ],
    "pagination": {
      "page": 1,
      "pageSize": 5,
      "totalItems": 12,
      "totalPages": 3,
      "hasPrevious": false,
      "hasNext": true
    }
  }
}
```

Nếu project đang dùng `PaginatedResult<T>`, `PagedResult<T>`, hoặc wrapper khác, giữ đúng convention hiện tại. Không tạo wrapper mới nếu đã có sẵn.

---

## 9. DTO đề xuất

File ưu tiên:

```text
backend/PEMS.Application/Faqs/Queries/ViewListFAQ/ViewListFAQDto.cs
```

DTO đề xuất:

```csharp
public sealed class ViewListFAQDto
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
    public string? CreatedByName { get; init; }

    public DateTime? UpdatedAt { get; init; }
    public ulong? UpdatedBy { get; init; }
    public string? UpdatedByName { get; init; }
}
```

Nếu project dùng `long` thay vì `ulong`, phải theo convention hiện tại của `Faq.cs`.

---

## 10. Application module cần dùng

Theo project structure mới, phải dùng module FAQ management:

```text
backend/PEMS.Application/Faqs/Queries/ViewListFAQ/
```

Các file dự kiến cần sửa/tạo:

```text
backend/PEMS.Application/Faqs/Queries/ViewListFAQ/ViewListFAQQuery.cs
backend/PEMS.Application/Faqs/Queries/ViewListFAQ/ViewListFAQQueryHandler.cs
backend/PEMS.Application/Faqs/Queries/ViewListFAQ/ViewListFAQQueryValidator.cs
backend/PEMS.Application/Faqs/Queries/ViewListFAQ/ViewListFAQDto.cs
```

Không dùng nhầm:

```text
backend/PEMS.Application/PublicContent/Queries/ViewFaq/
```

vì folder này dành cho UC-05 public FAQ.

---

## 11. Query object đề xuất

```csharp
public sealed record ViewListFAQQuery(
    string? Keyword,
    string? FaqType,
    string? Status,
    string? SortBy,
    string? SortDirection,
    int Page = 1,
    int PageSize = 5
) : IRequest<PaginatedResult<ViewListFAQDto>>;
```

Nếu project đang dùng class thay vì record, giữ convention hiện có.

Nếu project dùng `PaginatedResult<T>` thì dùng `PaginatedResult<T>`. Nếu dùng `PagedResult<T>` thì dùng `PagedResult<T>`. Không tự tạo thêm wrapper mới.

---

## 12. Validator đề xuất

File:

```text
backend/PEMS.Application/Faqs/Queries/ViewListFAQ/ViewListFAQQueryValidator.cs
```

Rules:

```text
page >= 1
pageSize 1–50
faqType = null/empty/ALL hoặc enum v10 hợp lệ
status = null/empty/ALL/PUBLISHED/HIDDEN
sortBy = null/createdAt/displayOrder
sortDirection = null/asc/desc
```

Pseudo-code:

```csharp
public sealed class ViewListFAQQueryValidator : AbstractValidator<ViewListFAQQuery>
{
    public ViewListFAQQueryValidator()
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

        RuleFor(x => x.Status)
            .Must(BeValidStatus)
            .WithMessage("FAQ status is invalid.");

        RuleFor(x => x.SortBy)
            .Must(BeValidSortBy)
            .WithMessage("Sort field is invalid.");

        RuleFor(x => x.SortDirection)
            .Must(BeValidSortDirection)
            .WithMessage("Sort direction is invalid.");
    }

    private static bool BeValidFaqType(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return true;
        var normalized = value.Trim();
        if (string.Equals(normalized, "ALL", StringComparison.OrdinalIgnoreCase)) return true;
        return FaqConstants.Type.All.Contains(normalized);
    }

    private static bool BeValidStatus(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return true;
        var normalized = value.Trim();
        if (string.Equals(normalized, "ALL", StringComparison.OrdinalIgnoreCase)) return true;
        return normalized is FaqConstants.Status.Published or FaqConstants.Status.Hidden;
    }

    private static bool BeValidSortBy(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return true;
        return value.Trim() is "createdAt" or "displayOrder";
    }

    private static bool BeValidSortDirection(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return true;
        var normalized = value.Trim().ToLowerInvariant();
        return normalized is "asc" or "desc";
    }
}
```

---

## 13. Constants/enum

Kiểm tra trước file hiện có:

```text
backend/PEMS.Domain/Constants/FaqConstants.cs
backend/PEMS.Domain/Enums/FaqVisibilityStatus.cs
```

Nếu chưa đủ v10 enum/status, cập nhật hoặc tạo `FaqConstants.cs`.

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

Không sửa enum cũ theo kiểu làm vỡ UC-05 hoặc các command create/update/change visibility. Nếu có mismatch, đồng bộ cẩn thận.

---

## 14. Handler pseudo-code

File:

```text
backend/PEMS.Application/Faqs/Queries/ViewListFAQ/ViewListFAQQueryHandler.cs
```

Logic bắt buộc:

```csharp
public sealed class ViewListFAQQueryHandler
    : IRequestHandler<ViewListFAQQuery, PaginatedResult<ViewListFAQDto>>
{
    private readonly IApplicationDbContext _dbContext;

    public ViewListFAQQueryHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<PaginatedResult<ViewListFAQDto>> Handle(
        ViewListFAQQuery request,
        CancellationToken cancellationToken)
    {
        var page = request.Page;
        var pageSize = request.PageSize;

        var keyword = request.Keyword?.Trim();
        var faqType = request.FaqType?.Trim();
        var status = request.Status?.Trim();
        var sortBy = request.SortBy?.Trim();
        var sortDirection = request.SortDirection?.Trim().ToLowerInvariant();

        var query = _dbContext.Faqs
            .AsNoTracking();

        if (!string.IsNullOrWhiteSpace(faqType) &&
            !string.Equals(faqType, "ALL", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(x => x.FaqType == faqType);
        }

        if (!string.IsNullOrWhiteSpace(status) &&
            !string.Equals(status, "ALL", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(x => x.Status == status);
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

        query = ApplySorting(query, sortBy, sortDirection);

        var rawItems = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
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
            .ToListAsync(cancellationToken);

        var items = rawItems
            .Select(x => new ViewListFAQDto
            {
                FaqId = x.FaqId,
                FaqType = x.FaqType,
                FaqTypeLabel = FaqConstants.ToVietnameseTypeLabel(x.FaqType),
                Question = x.Question,
                Answer = x.Answer,
                DisplayOrder = x.DisplayOrder,
                Status = x.Status,
                StatusLabel = FaqConstants.ToVietnameseStatusLabel(x.Status),
                CreatedAt = x.CreatedAt,
                CreatedBy = x.CreatedBy,
                UpdatedAt = x.UpdatedAt,
                UpdatedBy = x.UpdatedBy
            })
            .ToList();

        return PaginatedResult<ViewListFAQDto>.Create(
            items,
            page,
            pageSize,
            totalItems);
    }

    private static IQueryable<Faq> ApplySorting(
        IQueryable<Faq> query,
        string? sortBy,
        string? sortDirection)
    {
        var isAsc = string.Equals(sortDirection, "asc", StringComparison.OrdinalIgnoreCase);

        return sortBy switch
        {
            "displayOrder" => isAsc
                ? query.OrderBy(x => x.DisplayOrder).ThenByDescending(x => x.CreatedAt).ThenByDescending(x => x.FaqId)
                : query.OrderByDescending(x => x.DisplayOrder).ThenByDescending(x => x.CreatedAt).ThenByDescending(x => x.FaqId),

            _ => isAsc
                ? query.OrderBy(x => x.CreatedAt).ThenBy(x => x.FaqId)
                : query.OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.FaqId)
        };
    }
}
```

Nếu cần `CreatedByName` / `UpdatedByName`, có thể join `users`. Nhưng nếu màn hiện tại không cần hiển thị tên người tạo/cập nhật, có thể chưa join để tránh phức tạp.

Không gọi `FaqConstants.ToVietnameseTypeLabel()` hoặc `ToVietnameseStatusLabel()` trực tiếp trong EF projection nếu EF không translate được. Hãy map label sau khi `ToListAsync`.

---

## 15. Business Rules

| ID | Rule |
|---|---|
| BR-62-01 | Chỉ HO được truy cập `GET /api/faqs`. |
| BR-62-02 | Nếu chưa đăng nhập, backend trả `401 Unauthorized`. |
| BR-62-03 | Nếu đăng nhập nhưng không phải HO, backend trả `403 Forbidden`. |
| BR-62-04 | Mặc định lấy cả FAQ `PUBLISHED` và `HIDDEN`. |
| BR-62-05 | Nếu filter `status`, chỉ nhận `PUBLISHED`, `HIDDEN`, `ALL`. |
| BR-62-06 | `faqType` chỉ nhận enum v10 hoặc `ALL`. |
| BR-62-07 | Không nhận/trả `languageCode`. |
| BR-62-08 | Search dùng contains/LIKE trên `question`, `answer`, `faq_type`. |
| BR-62-09 | Search kết hợp với `faqType` và `status` bằng AND logic. |
| BR-62-10 | Default sort là `created_at DESC, faq_id DESC`. |
| BR-62-11 | Response trả flat items + pagination. |
| BR-62-12 | Controller không query DbContext trực tiếp; logic nằm trong QueryHandler. |
| BR-62-13 | Không dùng dynamic permissions vì schema v10 đã bỏ `permissions` và `role_permissions`. |
| BR-62-14 | Không dùng public endpoint `/api/public/faqs` cho màn quản lý. |

---

## 16. Alternative Flows

### AF-01 — Chưa đăng nhập

```text
When gọi GET /api/faqs không có token
Then trả 401 Unauthorized
```

### AF-02 — Không phải HO

```text
Given user role ADMIN/STAFF/DEPARTMENT/STUDENT/VISITOR
When gọi GET /api/faqs
Then trả 403 Forbidden
```

### AF-03 — Không có FAQ nào

```text
Given bảng faqs không có record
When HO gọi GET /api/faqs
Then trả HTTP 200
And items = []
And totalItems = 0
```

### AF-04 — Filter/search không có kết quả

```text
When filter/search không match FAQ nào
Then trả HTTP 200
And items = []
And totalItems = 0
```

### AF-05 — faqType invalid

```text
When gọi GET /api/faqs?faqType=VISA
Then trả 400 Bad Request
```

Vì `VISA` là type cũ, không còn hợp lệ trong schema v10.

### AF-06 — status invalid

```text
When gọi GET /api/faqs?status=VISIBLE
Then trả 400 Bad Request
```

Vì status v10 là `PUBLISHED/HIDDEN`, không dùng `VISIBLE`.

---

## 17. Verification Criteria

### VC-01 — HO xem được danh sách FAQ

```text
Given HO đã đăng nhập và account ACTIVE
And hệ thống có FAQ PUBLISHED và HIDDEN
When gọi GET /api/faqs
Then trả 200
And response có cả PUBLISHED và HIDDEN
And sort theo created_at DESC, faq_id DESC
```

### VC-02 — Không đăng nhập bị chặn

```text
When gọi GET /api/faqs không token/session
Then trả 401 Unauthorized
```

### VC-03 — Non-HO bị chặn

```text
Given user role ADMIN/STAFF/DEPARTMENT/STUDENT/VISITOR
When gọi GET /api/faqs
Then trả 403 Forbidden
```

### VC-04 — Filter status PUBLISHED

```text
Given có FAQ PUBLISHED và HIDDEN
When gọi GET /api/faqs?status=PUBLISHED
Then chỉ trả FAQ PUBLISHED
```

### VC-05 — Filter status HIDDEN

```text
Given có FAQ PUBLISHED và HIDDEN
When gọi GET /api/faqs?status=HIDDEN
Then chỉ trả FAQ HIDDEN
```

### VC-06 — Filter faqType

```text
Given có FAQ ACCOUNT_ACCESS và VISIT_REQUEST
When gọi GET /api/faqs?faqType=ACCOUNT_ACCESS
Then chỉ trả FAQ ACCOUNT_ACCESS
```

### VC-07 — Search trong answer

```text
Given có FAQ có answer chứa "OTP"
When gọi GET /api/faqs?keyword=OTP
Then FAQ đó xuất hiện dù keyword nằm trong answer
```

### VC-08 — Search + filter dùng AND logic

```text
Given có:
- FAQ A: faqType = ACCOUNT_ACCESS, answer chứa "OTP"
- FAQ B: faqType = VISIT_REQUEST, answer chứa "OTP"

When gọi GET /api/faqs?keyword=OTP&faqType=ACCOUNT_ACCESS
Then chỉ FAQ A được trả
And FAQ B không xuất hiện
```

### VC-09 — Reject enum type cũ

```text
When gọi GET /api/faqs?faqType=VISA
Then trả 400 Bad Request
```

### VC-10 — Reject status cũ

```text
When gọi GET /api/faqs?status=VISIBLE
Then trả 400 Bad Request
```

### VC-11 — Paging đúng

```text
Given có 12 FAQ
When gọi GET /api/faqs?page=2&pageSize=5
Then trả 5 items của page 2
And totalItems = 12
And totalPages = 3
```

### VC-12 — Không có languageCode

```text
When gọi GET /api/faqs
Then mỗi item không có field languageCode
```

---

## 18. Manual test bằng curl/Postman

### 18.1. Không token

```bash
curl -X GET "http://localhost:5265/api/faqs"
```

Expected:

```text
401 Unauthorized
```

### 18.2. Token HO

```bash
curl -X GET "http://localhost:5265/api/faqs"   -H "Authorization: Bearer <HO_TOKEN>"
```

Expected:

```text
200 OK
Trả cả PUBLISHED và HIDDEN
Sort created_at DESC, faq_id DESC
```

### 18.3. Token non-HO

```bash
curl -X GET "http://localhost:5265/api/faqs"   -H "Authorization: Bearer <STAFF_TOKEN>"
```

Expected:

```text
403 Forbidden
```

### 18.4. Filter status

```bash
curl -X GET "http://localhost:5265/api/faqs?status=PUBLISHED"   -H "Authorization: Bearer <HO_TOKEN>"
```

Expected:

```text
200 OK
Chỉ PUBLISHED
```

### 18.5. Search

```bash
curl -X GET "http://localhost:5265/api/faqs?keyword=otp"   -H "Authorization: Bearer <HO_TOKEN>"
```

Expected:

```text
200 OK
Search trong question, answer, faq_type
```

### 18.6. Invalid old type

```bash
curl -X GET "http://localhost:5265/api/faqs?faqType=VISA"   -H "Authorization: Bearer <HO_TOKEN>"
```

Expected:

```text
400 Bad Request
```

---

## 19. Những điều không được làm

```text
- Không đổi UC-62 thành UC-64.
- Không code nhầm sang UC-05 public FAQ.
- Không dùng endpoint /api/public/faqs cho dashboard FAQ.
- Không dùng PublicContentController cho use case này.
- Không AllowAnonymous.
- Không cho Admin/Staff/Department/Student/Visitor truy cập.
- Không lọc cứng status = PUBLISHED.
- Không dùng languageCode.
- Không dùng enum type cũ Program/Tuition/Visa/Dormitory.
- Không dùng status cũ Visible/Hidden nếu DB v10 là PUBLISHED/HIDDEN.
- Không dùng FULLTEXT mặc định.
- Không query DbContext trong Controller.
- Không tạo bảng/cột mới nếu SQL v10 đã đủ.
- Không dùng dynamic permissions/role_permissions.
- Không sửa frontend trong task backend này nếu user chỉ yêu cầu backend.
- Không báo hoàn thành nếu chưa build/test hoặc chưa nói rõ phần nào chưa test được.
```

---

## 20. Definition of Done

AI Agent chỉ được báo hoàn thành khi đủ:

```text
[ ] Xác nhận đang triển khai UC-62: View List FAQ.
[ ] Endpoint GET /api/faqs hoạt động.
[ ] Endpoint yêu cầu authentication.
[ ] Endpoint chỉ HO được gọi.
[ ] Non-HO bị 403.
[ ] Unauthenticated bị 401.
[ ] Mặc định trả cả PUBLISHED và HIDDEN.
[ ] Filter status hoạt động.
[ ] Filter faqType enum v10 hoạt động.
[ ] Search LIKE trên question, answer, faq_type.
[ ] Search + filter dùng AND logic.
[ ] Default sort created_at DESC, faq_id DESC.
[ ] Pagination hoạt động.
[ ] Không nhận/trả languageCode.
[ ] Không dùng public endpoint.
[ ] Không query DbContext trong Controller.
[ ] Có validator.
[ ] dotnet build PASS.
[ ] Có unit/integration test hoặc manual API test rõ ràng.
```

---

## 21. Output mong muốn từ AI Agent sau khi code

Sau khi code xong, báo cáo theo format:

```text
1. Files read
- ...

2. Files changed
- backend/PEMS.Api/Controllers/FaqsController.cs
- backend/PEMS.Application/Faqs/Queries/ViewListFAQ/...
- backend/PEMS.Domain/Constants/FaqConstants.cs
- tests/...

3. Endpoint implemented
- GET /api/faqs

4. Authorization
- HO only
- unauthenticated -> 401
- non-HO -> 403

5. Logic implemented
- default includes PUBLISHED + HIDDEN
- status filter
- faqType filter enum v10
- LIKE search
- sort created_at DESC, faq_id DESC
- pagination

6. Validation
- page/pageSize
- faqType
- status
- sortBy/sortDirection

7. Test result
- dotnet build: PASS/FAIL
- dotnet test: PASS/FAIL
- Manual API test: PASS/FAIL

8. Notes / Risks
- ...
```
