# PROMPT_CODE_UC88_VIEW_NEWS_LIST_BACKEND.md

## 0. Mục tiêu

Triển khai/cập nhật backend cho **UC-88: View News List** của hệ thống **PEMS — Partnership Engagement Management System**.

Use case này phục vụ màn **Quản lý tin tức** tại:

```text
/dashboard/news
```

Màn hình list có các thành phần chính:

```text
- Search box: Tìm kiếm tin tức
- Filter trạng thái: Tất cả trạng thái / Đã Duyệt / Chờ Duyệt / Từ Chối / Ẩn
- Bảng danh sách:
  + Tiêu đề
  + Mô tả
  + Ảnh
  + Người tạo hoặc Người duyệt tùy role
  + Ngày tạo hoặc Ngày duyệt tùy role
  + Trạng thái
  + Hành động
- Pagination
- Nút + Thêm tin tức mới chỉ hiển thị cho Staff thường / Student đủ điều kiện
```

Yêu cầu đã chốt:

```text
Staff thường và Student:
- Ai tạo thì chỉ nhìn được bài của người đó.
- Muốn viết tin tức về chuyến tiếp khách nào thì bắt buộc phải là participant ACCEPTED của đúng visitInstance đó.
- UC-88 chỉ là list; rule chọn đúng visitInstance sẽ được enforce trong Create News, nhưng list cần trả canCreateNews để UI biết có nên hiện nút tạo không.

Staff Leader:
- Xem bài theo campus của mình để duyệt bài.
- Không tạo bài.

HO:
- Chỉ xem bài đã duyệt.
- Read-only.
```

---

## 1. Quyết định nghiệp vụ đã chốt

```text
UC ID: UC-88
UC Name: View News List
Module: News Management
Page: /dashboard/news
Endpoint: GET /api/news
Authentication: Required
Authorization: Fixed role + scope, không dùng dynamic permissions
Default pageSize: 5
Default sort: created_at DESC, news_id DESC
Response style: Flat items + pagination + viewer/capability metadata
Search: Contains / LIKE
```

Lưu ý về UC numbering:

```text
- Theo cách gọi hiện tại của user và Report 3.1: UC-88 = View News List.
- Trong một số tài liệu/use-case list mới hơn có thể có lệch numbering, ví dụ View News List được gọi bằng UC khác.
- Khi code, không được code nhầm sang Approve News hoặc Create News.
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
Report 3.2_ScreenDesignSpec_Template.docx
```

Nếu có mâu thuẫn:

```text
1. Ưu tiên yêu cầu đã chốt trong file prompt này.
2. Ưu tiên schema v10 về bảng/cột/enum/status.
3. Ưu tiên PROJECT_STRUCTURE_FULL.md mới nhất về đường dẫn file thật.
4. Tài liệu cũ chỉ dùng đối chiếu, không dùng nếu mâu thuẫn với schema v10 hoặc yêu cầu mới.
```

---

## 3. Source of truth theo schema v10

Bảng liên quan:

```text
news
news_translations
news_content_sections
news_section_files
files
users
campuses
roles
visit_participants
visit_request_campuses
```

Bảng `news` v10 dùng cho News Management / Public News / Approval / Publish. Các cột quan trọng cần dùng cho UC-88:

```text
news_id
campus_id
visit_instance_id
author_user_id
cover_file_id
status
submitted_at
reviewed_by
reviewed_at
review_note
published_at
created_at
created_by
updated_at
updated_by
```

Status hợp lệ theo v10:

```text
PENDING_REVIEW
REJECTED
PUBLISHED
HIDDEN
```

Không dùng status cũ:

```text
DRAFT
ARCHIVED
APPROVED
VISIBLE
```

Bảng `news_translations` dùng để lấy tiêu đề/mô tả theo ngôn ngữ. Với list view, ưu tiên tiếng Việt:

```text
language_code = 'vi'
```

Nếu thiếu bản `vi`, fallback bản dịch đầu tiên theo convention hiện có.

Bảng `files` dùng lấy ảnh bìa từ:

```text
news.cover_file_id -> files.file_id
```

Bảng `visit_participants` dùng xác định điều kiện tạo bài:

```text
visit_participants.user_id = currentUserId
visit_participants.visit_instance_id = selectedVisitInstanceId
visit_participants.status = ACCEPTED
```

---

## 4. Phân biệt UC-88 với các use case khác

| Use case | Endpoint | Mục đích |
|---|---|---|
| UC-88 View News List | `GET /api/news` | Xem danh sách tin tức theo role/scope |
| Create News | `POST /api/news` | Staff/Student tạo bài mới cho visitInstance mà họ đã ACCEPTED |
| View News Detail | `GET /api/news/{newsId}` | Xem chi tiết bài viết |
| Update News | `PUT /api/news/{newsId}` | Tác giả sửa bài PENDING_REVIEW/REJECTED |
| Approve/Reject News | `PATCH /api/news/{newsId}/review` | Staff Leader duyệt/từ chối |
| Change News Visibility | `PATCH /api/news/{newsId}/visibility` | Staff Leader ẩn/hiện PUBLISHED/HIDDEN |
| Public News | `GET /api/public/news` | Public chỉ xem bài PUBLISHED |

UC-88 chỉ list dữ liệu. Không nhét logic tạo/sửa/duyệt/ẩn/hiện vào query list.

---

## 5. Endpoint bắt buộc

```http
GET /api/news
```

Ví dụ:

```http
GET /api/news?page=1&pageSize=5
GET /api/news?keyword=solbridge
GET /api/news?status=PENDING_REVIEW
GET /api/news?status=PUBLISHED&page=1&pageSize=5
GET /api/news?keyword=fpt&status=REJECTED&page=1&pageSize=5
```

Yêu cầu:

```text
- Bắt buộc đăng nhập.
- Không AllowAnonymous.
- Không dùng public endpoint.
- Không dùng mock data.
- Không query DbContext trực tiếp trong Controller.
- Controller chỉ gọi IMediator.Send.
```

---

## 6. Controller cần dùng

Theo project structure hiện tại, project đã có:

```text
backend/PEMS.Api/Controllers/NewsController.cs
```

UC-88 phải dùng:

```text
backend/PEMS.Api/Controllers/NewsController.cs
```

Không dùng:

```text
backend/PEMS.Api/Controllers/PublicContentController.cs
```

Pseudo-code:

```csharp
[ApiController]
[Route("api/news")]
public sealed class NewsController : ControllerBase
{
    private readonly IMediator _mediator;

    public NewsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    [Authorize]
    public async Task<IActionResult> GetNewsList(
        [FromQuery] ViewNewsListQuery query,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(query, cancellationToken);
        return Ok(ApiResponse.Success(result, "News list loaded successfully."));
    }
}
```

Nếu project đang dùng `RoleAuthorizeAttribute` ở action/controller, có thể dùng role allow-list:

```text
HO
STAFF
STUDENT
```

Nhưng vẫn phải kiểm tra subRole/scope trong Handler.

---

## 7. Allowed roles

Cho phép gọi API:

```text
HO
STAFF + LEADER
STAFF + STAFF
STUDENT
```

Không cho:

```text
ADMIN
DEPARTMENT + LEADER
DEPARTMENT + STAFF
VISITOR
```

Backend phải trả:

```text
401 Unauthorized nếu chưa đăng nhập
403 Forbidden nếu role không được phép
```

Không dùng dynamic permission tables:

```text
permissions
role_permissions
```

---

## 8. Scope logic theo role

### 8.1. Staff thường

Điều kiện role:

```text
role_code = STAFF
sub_role = STAFF
```

Danh sách được xem:

```sql
WHERE news.author_user_id = @CurrentUserId
```

Không được xem bài của người khác, kể cả cùng campus.

`canCreateNews`:

```sql
EXISTS (
  SELECT 1
  FROM visit_participants vp
  WHERE vp.user_id = @CurrentUserId
    AND vp.status = 'ACCEPTED'
)
```

Ghi chú quan trọng:

```text
UC-88 chỉ trả canCreateNews để UI hiển thị nút + Thêm tin tức mới.
Khi thật sự tạo bài, Create News phải bắt user chọn selectedVisitInstanceId và backend phải kiểm tra đúng:
visit_participants.user_id = currentUserId
visit_participants.visit_instance_id = selectedVisitInstanceId
visit_participants.status = ACCEPTED
```

Action list:

```text
canViewDetail = true
canEdit = true nếu status = PENDING_REVIEW hoặc REJECTED và là bài của chính user
canApprove = false
canReject = false
canHide = false
canShow = false
```

---

### 8.2. Student

Điều kiện role:

```text
role_code = STUDENT
```

Danh sách được xem:

```sql
WHERE news.author_user_id = @CurrentUserId
```

Không được xem bài của student khác hoặc staff khác.

`canCreateNews`:

```sql
EXISTS (
  SELECT 1
  FROM visit_participants vp
  WHERE vp.user_id = @CurrentUserId
    AND vp.status = 'ACCEPTED'
)
```

Nếu muốn siết đúng role participant cho Student:

```sql
AND vp.participant_role = 'STUDENT'
```

Tuy nhiên nếu dữ liệu đã đảm bảo user role STUDENT chỉ được ghi participant_role STUDENT, điều kiện `status = ACCEPTED` là đủ.

Rule khi tạo bài:

```text
Student chỉ được tạo bài cho đúng visitInstance mà Student đó đã ACCEPTED.
```

Action list giống Staff thường:

```text
canViewDetail = true
canEdit = true nếu status = PENDING_REVIEW hoặc REJECTED và là bài của chính user
canApprove = false
canReject = false
canHide = false
canShow = false
```

---

### 8.3. Staff Leader

Điều kiện role:

```text
role_code = STAFF
sub_role = LEADER
```

Danh sách được xem:

```sql
WHERE news.campus_id = @CurrentUserPrimaryCampusId
```

Staff Leader xem mọi status trong campus mình:

```text
PENDING_REVIEW
REJECTED
PUBLISHED
HIDDEN
```

Không có nút tạo:

```text
canCreateNews = false
```

Action list:

```text
canViewDetail = true
canEdit = false
canApprove = true nếu status = PENDING_REVIEW
canReject = true nếu status = PENDING_REVIEW
canHide = true nếu status = PUBLISHED
canShow = true nếu status = HIDDEN
```

Lưu ý:

```text
Approve/Reject/Hide/Show xử lý bằng endpoint/use case riêng.
UC-88 chỉ trả availableActions để frontend hiển thị icon/toggle đúng.
```

---

### 8.4. HO

Điều kiện role:

```text
role_code = HO
```

Danh sách được xem:

```sql
WHERE news.status = 'PUBLISHED'
```

Không giới hạn campus. HO xem toàn hệ thống nhưng chỉ bài đã duyệt.

Không có nút tạo:

```text
canCreateNews = false
```

Action list:

```text
canViewDetail = true
canEdit = false
canApprove = false
canReject = false
canHide = false
canShow = false
```

Cột hiển thị cho HO nên ưu tiên:

```text
Tiêu đề
Mô tả
Ảnh
Người duyệt
Ngày duyệt
Hành động xem chi tiết
```

---

## 9. Query parameters

| Param | Type | Required | Default | Rule |
|---|---:|---:|---:|---|
| `keyword` | string | No | null | Trim. Search LIKE trên title, summary/description, author name/reviewer name nếu cần. |
| `status` | string | No | `ALL` | `ALL`, `PENDING_REVIEW`, `REJECTED`, `PUBLISHED`, `HIDDEN`. |
| `page` | int | No | `1` | Phải >= 1. |
| `pageSize` | int | No | `5` | Phải trong khoảng 1–50. |
| `sortBy` | string | No | `createdAt` | Giai đoạn này hỗ trợ `createdAt`, có thể thêm `reviewedAt`. |
| `sortDirection` | string | No | `desc` | `asc` hoặc `desc`. |

### 9.1. Status filter

Với Staff thường / Student / Staff Leader:

```text
status = null/empty/ALL -> không filter status
status = PENDING_REVIEW -> chỉ bài chờ duyệt trong scope
status = REJECTED       -> chỉ bài từ chối trong scope
status = PUBLISHED      -> chỉ bài đã duyệt trong scope
status = HIDDEN         -> chỉ bài ẩn trong scope
```

Với HO:

```text
HO luôn chỉ thấy PUBLISHED.
Nếu status null/ALL/PUBLISHED -> OK, vẫn PUBLISHED.
Nếu status khác PUBLISHED -> trả 400 Bad Request hoặc trả empty.
Khuyến nghị: trả 400 "Status filter is not allowed for HO."
```

### 9.2. Keyword search

Search dùng `LIKE/Contains`, không dùng FULLTEXT mặc định.

Search scope đề xuất:

```text
news_translations.title
news_translations.summary
author.full_name
reviewer.full_name
```

Pseudo SQL:

```sql
AND (
  title LIKE CONCAT('%', @keyword, '%')
  OR summary LIKE CONCAT('%', @keyword, '%')
  OR author.full_name LIKE CONCAT('%', @keyword, '%')
  OR reviewer.full_name LIKE CONCAT('%', @keyword, '%')
)
```

Search phải kết hợp với role scope và status filter bằng AND logic.

### 9.3. Sort

Default:

```sql
ORDER BY news.created_at DESC, news.news_id DESC
```

Nếu `sortBy=createdAt&sortDirection=asc`:

```sql
ORDER BY news.created_at ASC, news.news_id ASC
```

Nếu hỗ trợ `reviewedAt` cho HO:

```text
sortBy=reviewedAt chỉ nên dùng cho HO hoặc khi frontend cần sort ngày duyệt.
Nếu chưa cần, có thể chỉ validate createdAt.
```

---

## 10. Response contract

Backend trả flat items + pagination + viewer/capability metadata.

Ví dụ response cho Staff thường / Student:

```json
{
  "success": true,
  "message": "News list loaded successfully.",
  "data": {
    "viewerMode": "AUTHOR",
    "canCreateNews": true,
    "items": [
      {
        "newsId": 1,
        "title": "Trải nghiệm 6 tháng học tập tại SolBridge International School...",
        "description": "Một học kỳ ở Hàn Quốc đã mang đến cho mình những...",
        "coverImageUrl": "https://...",
        "coverThumbnailUrl": "https://...",
        "campusId": 1,
        "campusName": "FPT University Hà Nội",
        "visitInstanceId": 1001,
        "authorUserId": 12,
        "authorName": "Nguyễn Văn A",
        "createdAt": "2026-06-25T10:00:00",
        "updatedAt": "2026-06-25T11:00:00",
        "status": "PENDING_REVIEW",
        "statusLabel": "Chờ Duyệt",
        "reviewedBy": null,
        "reviewedByName": null,
        "reviewedAt": null,
        "availableActions": {
          "canViewDetail": true,
          "canEdit": true,
          "canApprove": false,
          "canReject": false,
          "canHide": false,
          "canShow": false
        }
      }
    ],
    "pagination": {
      "page": 1,
      "pageSize": 5,
      "totalItems": 8,
      "totalPages": 2,
      "hasPrevious": false,
      "hasNext": true
    }
  }
}
```

Ví dụ response cho Staff Leader:

```json
{
  "success": true,
  "message": "News list loaded successfully.",
  "data": {
    "viewerMode": "REVIEWER",
    "canCreateNews": false,
    "items": [
      {
        "newsId": 2,
        "title": "Thông báo mở đơn đăng ký học kỳ Fall 2024...",
        "description": "Phòng Hợp tác Quốc tế thông báo chương trình...",
        "coverImageUrl": "https://...",
        "coverThumbnailUrl": "https://...",
        "campusId": 1,
        "campusName": "FPT University Hà Nội",
        "visitInstanceId": 1002,
        "authorUserId": 21,
        "authorName": "Nguyễn Văn B",
        "createdAt": "2026-06-25T10:00:00",
        "updatedAt": "2026-06-25T11:00:00",
        "status": "PUBLISHED",
        "statusLabel": "Đã Duyệt",
        "reviewedBy": 5,
        "reviewedByName": "IC Staff Leader Hà Nội",
        "reviewedAt": "2026-06-25T12:00:00",
        "availableActions": {
          "canViewDetail": true,
          "canEdit": false,
          "canApprove": false,
          "canReject": false,
          "canHide": true,
          "canShow": false
        }
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

Ví dụ response cho HO:

```json
{
  "success": true,
  "message": "News list loaded successfully.",
  "data": {
    "viewerMode": "HO_READONLY",
    "canCreateNews": false,
    "items": [
      {
        "newsId": 3,
        "title": "Lễ ký kết biên bản ghi nhớ hợp tác...",
        "description": "Sáng nay, tại campus Hòa Lạc đã diễn ra...",
        "coverImageUrl": "https://...",
        "coverThumbnailUrl": "https://...",
        "campusId": 1,
        "campusName": "FPT University Hà Nội",
        "visitInstanceId": 1003,
        "authorUserId": 21,
        "authorName": "Nguyễn Văn B",
        "createdAt": "2026-06-25T10:00:00",
        "updatedAt": "2026-06-25T11:00:00",
        "status": "PUBLISHED",
        "statusLabel": "Đã Duyệt",
        "reviewedBy": 5,
        "reviewedByName": "IC Staff Leader Hà Nội",
        "reviewedAt": "2026-06-25T12:00:00",
        "availableActions": {
          "canViewDetail": true,
          "canEdit": false,
          "canApprove": false,
          "canReject": false,
          "canHide": false,
          "canShow": false
        }
      }
    ],
    "pagination": {
      "page": 1,
      "pageSize": 5,
      "totalItems": 20,
      "totalPages": 4,
      "hasPrevious": false,
      "hasNext": true
    }
  }
}
```

Nếu project đang dùng `PaginatedResult<T>` hoặc wrapper khác, giữ đúng convention hiện tại. Không tạo wrapper mới nếu đã có sẵn.

---

## 11. DTO đề xuất

File ưu tiên:

```text
backend/PEMS.Application/News/Queries/ViewNewsList/ViewNewsListDto.cs
```

DTO đề xuất:

```csharp
public sealed class ViewNewsListResponse
{
    public string ViewerMode { get; init; } = default!;
    public bool CanCreateNews { get; init; }
    public IReadOnlyList<ViewNewsListItemDto> Items { get; init; } = Array.Empty<ViewNewsListItemDto>();
    public PaginationMetadata Pagination { get; init; } = default!;
}
```

Hoặc nếu project đã có `PaginatedResult<T>`:

```csharp
public sealed class ViewNewsListResult
{
    public string ViewerMode { get; init; } = default!;
    public bool CanCreateNews { get; init; }
    public PaginatedResult<ViewNewsListItemDto> News { get; init; } = default!;
}
```

Item DTO:

```csharp
public sealed class ViewNewsListItemDto
{
    public long NewsId { get; init; }

    public string Title { get; init; } = default!;
    public string? Description { get; init; }

    public string? CoverImageUrl { get; init; }
    public string? CoverThumbnailUrl { get; init; }

    public long? CampusId { get; init; }
    public string? CampusName { get; init; }

    public long? VisitInstanceId { get; init; }

    public long AuthorUserId { get; init; }
    public string AuthorName { get; init; } = default!;

    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }

    public string Status { get; init; } = default!;
    public string StatusLabel { get; init; } = default!;

    public long? ReviewedBy { get; init; }
    public string? ReviewedByName { get; init; }
    public DateTime? ReviewedAt { get; init; }

    public NewsAvailableActionsDto AvailableActions { get; init; } = default!;
}
```

Actions DTO:

```csharp
public sealed class NewsAvailableActionsDto
{
    public bool CanViewDetail { get; init; }
    public bool CanEdit { get; init; }
    public bool CanApprove { get; init; }
    public bool CanReject { get; init; }
    public bool CanHide { get; init; }
    public bool CanShow { get; init; }
}
```

Nếu project dùng `ulong`, đổi theo entity hiện có.

---

## 12. Query object đề xuất

File:

```text
backend/PEMS.Application/News/Queries/ViewNewsList/ViewNewsListQuery.cs
```

```csharp
public sealed record ViewNewsListQuery(
    string? Keyword,
    string? Status,
    string? SortBy,
    string? SortDirection,
    int Page = 1,
    int PageSize = 5
) : IRequest<ViewNewsListResponse>;
```

Nếu project đang dùng class thay vì record, giữ convention hiện có.

---

## 13. Validator đề xuất

File:

```text
backend/PEMS.Application/News/Queries/ViewNewsList/ViewNewsListQueryValidator.cs
```

Rules:

```text
page >= 1
pageSize 1–50
status = null/empty/ALL/PENDING_REVIEW/REJECTED/PUBLISHED/HIDDEN
sortBy = null/createdAt/reviewedAt
sortDirection = null/asc/desc
```

Pseudo-code:

```csharp
public sealed class ViewNewsListQueryValidator : AbstractValidator<ViewNewsListQuery>
{
    public ViewNewsListQueryValidator()
    {
        RuleFor(x => x.Page)
            .GreaterThanOrEqualTo(1)
            .WithMessage("Page must be greater than or equal to 1.");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 50)
            .WithMessage("Page size must be between 1 and 50.");

        RuleFor(x => x.Status)
            .Must(BeValidStatus)
            .WithMessage("News status is invalid.");

        RuleFor(x => x.SortBy)
            .Must(BeValidSortBy)
            .WithMessage("Sort field is invalid.");

        RuleFor(x => x.SortDirection)
            .Must(BeValidSortDirection)
            .WithMessage("Sort direction is invalid.");
    }

    private static bool BeValidStatus(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return true;
        var normalized = value.Trim();
        if (string.Equals(normalized, "ALL", StringComparison.OrdinalIgnoreCase)) return true;

        return normalized is
            NewsConstants.Status.PendingReview or
            NewsConstants.Status.Rejected or
            NewsConstants.Status.Published or
            NewsConstants.Status.Hidden;
    }

    private static bool BeValidSortBy(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return true;
        return value.Trim() is "createdAt" or "reviewedAt";
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

## 14. Constants đề xuất

Kiểm tra trước file hiện có:

```text
backend/PEMS.Domain/Constants/NewsConstants.cs
backend/PEMS.Domain/Enums/...
```

Nếu chưa có, tạo/cập nhật:

```csharp
public static class NewsConstants
{
    public static class Status
    {
        public const string PendingReview = "PENDING_REVIEW";
        public const string Rejected = "REJECTED";
        public const string Published = "PUBLISHED";
        public const string Hidden = "HIDDEN";

        public static readonly IReadOnlySet<string> All = new HashSet<string>
        {
            PendingReview,
            Rejected,
            Published,
            Hidden
        };
    }

    public static class ViewerMode
    {
        public const string Author = "AUTHOR";
        public const string Reviewer = "REVIEWER";
        public const string HoReadonly = "HO_READONLY";
    }

    public static string ToVietnameseStatusLabel(string status)
    {
        return status switch
        {
            Status.PendingReview => "Chờ Duyệt",
            Status.Rejected => "Từ Chối",
            Status.Published => "Đã Duyệt",
            Status.Hidden => "Ẩn",
            _ => status
        };
    }
}
```

Không dùng label tiếng Việt làm DB value.

---

## 15. Handler pseudo-code

File:

```text
backend/PEMS.Application/News/Queries/ViewNewsList/ViewNewsListQueryHandler.cs
```

Pseudo-code tổng quát:

```csharp
public sealed class ViewNewsListQueryHandler
    : IRequestHandler<ViewNewsListQuery, ViewNewsListResponse>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUser;

    public ViewNewsListQueryHandler(
        IApplicationDbContext dbContext,
        ICurrentUserService currentUser)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
    }

    public async Task<ViewNewsListResponse> Handle(
        ViewNewsListQuery request,
        CancellationToken cancellationToken)
    {
        var currentUserId = _currentUser.UserId;
        var roleCode = _currentUser.RoleCode;
        var subRole = _currentUser.SubRole;
        var primaryCampusId = _currentUser.PrimaryCampusId;

        var viewerMode = ResolveViewerMode(roleCode, subRole);

        if (viewerMode is null)
        {
            throw new ForbiddenException("You do not have permission to view news.");
        }

        var keyword = request.Keyword?.Trim();
        var status = request.Status?.Trim();
        var sortBy = request.SortBy?.Trim();
        var sortDirection = request.SortDirection?.Trim().ToLowerInvariant();

        var query = _dbContext.News
            .AsNoTracking();

        query = ApplyRoleScope(query, viewerMode, currentUserId, primaryCampusId);

        query = ApplyStatusFilter(query, viewerMode, status);

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var pattern = $"%{keyword}%";

            // Join/search title/summary/author/reviewer according to actual entity structure.
            // Do not load all news into memory.
        }

        var canCreateNews = await ResolveCanCreateNewsAsync(
            viewerMode,
            currentUserId,
            cancellationToken);

        var totalItems = await query.CountAsync(cancellationToken);

        query = ApplySorting(query, sortBy, sortDirection);

        var rawItems = await query
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(x => new
            {
                x.NewsId,
                x.CampusId,
                x.VisitInstanceId,
                x.AuthorUserId,
                x.CoverFileId,
                x.Status,
                x.CreatedAt,
                x.UpdatedAt,
                x.ReviewedBy,
                x.ReviewedAt
            })
            .ToListAsync(cancellationToken);

        // Sau khi lấy raw news, map title/summary/cover/author/reviewer/campus.
        // Có thể query bằng join projection ngay từ đầu nếu project dễ làm.
        // Tránh N+1 query.

        var items = rawItems
            .Select(x => new ViewNewsListItemDto
            {
                NewsId = x.NewsId,
                Status = x.Status,
                StatusLabel = NewsConstants.ToVietnameseStatusLabel(x.Status),
                AvailableActions = BuildActions(viewerMode, x.Status, x.AuthorUserId, currentUserId)
            })
            .ToList();

        return new ViewNewsListResponse
        {
            ViewerMode = viewerMode,
            CanCreateNews = canCreateNews,
            Items = items,
            Pagination = PaginationMetadata.Create(request.Page, request.PageSize, totalItems)
        };
    }
}
```

### 15.1. Resolve viewer mode

```csharp
private static string? ResolveViewerMode(string roleCode, string? subRole)
{
    if (roleCode == RoleCodes.HO)
        return NewsConstants.ViewerMode.HoReadonly;

    if (roleCode == RoleCodes.Student)
        return NewsConstants.ViewerMode.Author;

    if (roleCode == RoleCodes.Staff && subRole == SubRoles.Staff)
        return NewsConstants.ViewerMode.Author;

    if (roleCode == RoleCodes.Staff && subRole == SubRoles.Leader)
        return NewsConstants.ViewerMode.Reviewer;

    return null;
}
```

### 15.2. Apply role scope

```csharp
private static IQueryable<News> ApplyRoleScope(
    IQueryable<News> query,
    string viewerMode,
    long currentUserId,
    long? primaryCampusId)
{
    return viewerMode switch
    {
        NewsConstants.ViewerMode.Author =>
            query.Where(x => x.AuthorUserId == currentUserId),

        NewsConstants.ViewerMode.Reviewer =>
            query.Where(x => x.CampusId == primaryCampusId),

        NewsConstants.ViewerMode.HoReadonly =>
            query.Where(x => x.Status == NewsConstants.Status.Published),

        _ => throw new ForbiddenException("You do not have permission to view news.")
    };
}
```

### 15.3. Apply status filter

```csharp
private static IQueryable<News> ApplyStatusFilter(
    IQueryable<News> query,
    string viewerMode,
    string? status)
{
    if (string.IsNullOrWhiteSpace(status) ||
        string.Equals(status, "ALL", StringComparison.OrdinalIgnoreCase))
    {
        return query;
    }

    if (viewerMode == NewsConstants.ViewerMode.HoReadonly &&
        status != NewsConstants.Status.Published)
    {
        throw new ValidationException("Status filter is not allowed for HO.");
    }

    return query.Where(x => x.Status == status);
}
```

Với HO, vì base scope đã là `PUBLISHED`, nếu status = ALL hoặc PUBLISHED thì OK.

### 15.4. Resolve canCreateNews

```csharp
private async Task<bool> ResolveCanCreateNewsAsync(
    string viewerMode,
    long currentUserId,
    CancellationToken cancellationToken)
{
    if (viewerMode != NewsConstants.ViewerMode.Author)
    {
        return false;
    }

    return await _dbContext.VisitParticipants
        .AsNoTracking()
        .AnyAsync(x =>
            x.UserId == currentUserId &&
            x.Status == VisitParticipantConstants.Status.Accepted,
            cancellationToken);
}
```

Lưu ý:

```text
canCreateNews = true chỉ nghĩa là user có ít nhất một visitInstance ACCEPTED.
Khi tạo bài, Create News phải validate selectedVisitInstanceId cụ thể.
```

### 15.5. Build actions

```csharp
private static NewsAvailableActionsDto BuildActions(
    string viewerMode,
    string status,
    long authorUserId,
    long currentUserId)
{
    if (viewerMode == NewsConstants.ViewerMode.Author)
    {
        var isOwner = authorUserId == currentUserId;

        return new NewsAvailableActionsDto
        {
            CanViewDetail = isOwner,
            CanEdit = isOwner && status is
                NewsConstants.Status.PendingReview or
                NewsConstants.Status.Rejected,
            CanApprove = false,
            CanReject = false,
            CanHide = false,
            CanShow = false
        };
    }

    if (viewerMode == NewsConstants.ViewerMode.Reviewer)
    {
        return new NewsAvailableActionsDto
        {
            CanViewDetail = true,
            CanEdit = false,
            CanApprove = status == NewsConstants.Status.PendingReview,
            CanReject = status == NewsConstants.Status.PendingReview,
            CanHide = status == NewsConstants.Status.Published,
            CanShow = status == NewsConstants.Status.Hidden
        };
    }

    if (viewerMode == NewsConstants.ViewerMode.HoReadonly)
    {
        return new NewsAvailableActionsDto
        {
            CanViewDetail = true,
            CanEdit = false,
            CanApprove = false,
            CanReject = false,
            CanHide = false,
            CanShow = false
        };
    }

    return new NewsAvailableActionsDto();
}
```

---

## 16. Join/projection requirements

List item cần các field:

```text
title
description/summary
coverImageUrl/coverThumbnailUrl
authorName
reviewedByName
campusName
```

Không được gây N+1 query.

Gợi ý projection:

```text
news
LEFT JOIN news_translations nt ON nt.news_id = news.news_id AND nt.language_code = 'vi'
LEFT JOIN files cover ON cover.file_id = news.cover_file_id
LEFT JOIN users author ON author.user_id = news.author_user_id
LEFT JOIN users reviewer ON reviewer.user_id = news.reviewed_by
LEFT JOIN campuses campus ON campus.campus_id = news.campus_id
```

Nếu EF navigation đã có đầy đủ, dùng projection với navigation. Không dùng `Include` dư nếu chỉ cần DTO.

---

## 17. Business Rules

| ID | Rule |
|---|---|
| BR-88-01 | `GET /api/news` là authenticated endpoint, không public. |
| BR-88-02 | Staff thường chỉ xem bài do chính mình tạo. |
| BR-88-03 | Student chỉ xem bài do chính mình tạo. |
| BR-88-04 | Staff Leader xem tất cả bài thuộc campus mình, mọi status. |
| BR-88-05 | HO chỉ xem bài `PUBLISHED`, read-only. |
| BR-88-06 | Staff/Student chỉ có nút thêm bài nếu có ít nhất một `visit_participants` row `status = ACCEPTED`. |
| BR-88-07 | Khi tạo bài, Staff/Student bắt buộc chọn đúng `visitInstanceId` mà chính user đã `ACCEPTED`. UC-88 chỉ trả `canCreateNews`; Create News phải enforce đúng visitInstance cụ thể. |
| BR-88-08 | Staff Leader không có nút thêm bài. |
| BR-88-09 | HO không có nút thêm bài. |
| BR-88-10 | Status filter chỉ nhận `ALL`, `PENDING_REVIEW`, `REJECTED`, `PUBLISHED`, `HIDDEN`. |
| BR-88-11 | Không dùng status cũ `DRAFT`, `ARCHIVED`, `APPROVED`, `VISIBLE`. |
| BR-88-12 | Default sort là `created_at DESC, news_id DESC`. |
| BR-88-13 | Search kết hợp với status filter và role scope bằng AND logic. |
| BR-88-14 | Controller không query DbContext trực tiếp; logic nằm trong QueryHandler. |
| BR-88-15 | Không dùng dynamic permissions/role_permissions. |
| BR-88-16 | List view không sửa dữ liệu; approve/reject/hide/show là use case khác hoặc detail action. |
| BR-88-17 | HO nếu truyền status khác `PUBLISHED/ALL/null` thì backend phải reject hoặc trả empty; khuyến nghị reject 400. |
| BR-88-18 | Department/Admin/Visitor không được xem News Management list. |

---

## 18. Alternative Flows

### AF-01 — Chưa đăng nhập

```text
When gọi GET /api/news không token/session
Then trả 401 Unauthorized
```

### AF-02 — Role không được phép

```text
Given user role ADMIN/DEPARTMENT/VISITOR
When gọi GET /api/news
Then trả 403 Forbidden
```

### AF-03 — Staff/Student chưa có bài

```text
Given Staff/Student đã đăng nhập
And user chưa tạo bài nào
When gọi GET /api/news
Then trả items = []
And totalItems = 0
```

### AF-04 — Staff/Student chưa ACCEPTED chuyến nào

```text
Given Staff/Student đã đăng nhập
And không có visit_participants ACCEPTED
When gọi GET /api/news
Then vẫn trả list bài của chính user nếu có
And canCreateNews = false
```

### AF-05 — Staff Leader campus không có bài

```text
Given Staff Leader campus HN
And không có news.campus_id = HN
When gọi GET /api/news
Then items = []
And totalItems = 0
```

### AF-06 — HO không có bài đã duyệt

```text
Given không có news status PUBLISHED
When HO gọi GET /api/news
Then items = []
And totalItems = 0
```

### AF-07 — Status invalid

```text
When gọi GET /api/news?status=APPROVED
Then trả 400 Bad Request
```

Vì `APPROVED` là status cũ/label, không phải DB status v10.

### AF-08 — HO lọc status không được phép

```text
Given HO gọi GET /api/news?status=REJECTED
Then trả 400 Bad Request
```

Khuyến nghị message:

```text
Status filter is not allowed for HO.
```

### AF-09 — Search không có kết quả

```text
When keyword không match title/description/author/reviewer trong scope
Then items = []
And totalItems = 0
```

---

## 19. Verification Criteria

### VC-01 — Staff thường chỉ xem bài của mình

```text
Given Staff A đã đăng nhập
And DB có bài của Staff A và Staff B
When gọi GET /api/news
Then chỉ trả bài có author_user_id = Staff A
```

### VC-02 — Student chỉ xem bài của mình

```text
Given Student A đã đăng nhập
And DB có bài của Student A và Student B
When gọi GET /api/news
Then chỉ trả bài có author_user_id = Student A
```

### VC-03 — Staff/Student có quyền tạo nếu có ACCEPTED participation

```text
Given current user là Staff thường hoặc Student
And tồn tại visit_participants.user_id = currentUserId với status = ACCEPTED
When gọi GET /api/news
Then response canCreateNews = true
```

### VC-04 — Staff/Student không có quyền tạo nếu chưa ACCEPTED

```text
Given current user là Staff thường hoặc Student
And không có visit_participants.user_id = currentUserId với status = ACCEPTED
When gọi GET /api/news
Then response canCreateNews = false
```

### VC-05 — Staff Leader xem bài trong campus mình

```text
Given Staff Leader thuộc campus HN
And DB có news ở HN và HCM
When gọi GET /api/news
Then chỉ trả news.campus_id = HN
And có đủ status PENDING_REVIEW, REJECTED, PUBLISHED, HIDDEN nếu tồn tại
```

### VC-06 — Staff Leader không tạo bài

```text
Given Staff Leader đăng nhập
When gọi GET /api/news
Then canCreateNews = false
```

### VC-07 — Staff Leader action theo status

```text
Given Staff Leader gọi GET /api/news
When item status = PENDING_REVIEW
Then canApprove = true
And canReject = true

When item status = PUBLISHED
Then canHide = true

When item status = HIDDEN
Then canShow = true
```

### VC-08 — HO chỉ xem PUBLISHED

```text
Given HO đăng nhập
And DB có PENDING_REVIEW, REJECTED, PUBLISHED, HIDDEN
When gọi GET /api/news
Then chỉ trả status = PUBLISHED
And canCreateNews = false
And tất cả availableActions chỉ có canViewDetail = true
```

### VC-09 — Filter status

```text
Given Staff Leader có nhiều bài thuộc nhiều status trong campus mình
When gọi GET /api/news?status=PENDING_REVIEW
Then chỉ trả bài PENDING_REVIEW trong campus của Staff Leader
```

### VC-10 — Reject status cũ

```text
When gọi GET /api/news?status=APPROVED
Then trả 400 Bad Request
```

### VC-11 — Search theo title/description

```text
Given có bài title hoặc description chứa "SolBridge"
When gọi GET /api/news?keyword=SolBridge
Then bài đó xuất hiện nếu nằm trong scope của current user
```

### VC-12 — Search không vượt scope

```text
Given Staff A search keyword match bài của Staff B
When Staff A gọi GET /api/news?keyword=<keyword>
Then bài Staff B không xuất hiện
```

### VC-13 — Non-allowed role bị chặn

```text
Given Visitor hoặc Department gọi GET /api/news
Then trả 403 Forbidden
```

### VC-14 — Unauthenticated bị chặn

```text
Given không có token
When gọi GET /api/news
Then trả 401 Unauthorized
```

### VC-15 — Paging đúng

```text
Given current scope có 12 bài
When gọi GET /api/news?page=2&pageSize=5
Then trả 5 items của page 2
And totalItems = 12
And totalPages = 3
```

---

## 20. Manual test bằng curl/Postman

### 20.1. Không token

```bash
curl -X GET "http://localhost:5265/api/news"
```

Expected:

```text
401 Unauthorized
```

### 20.2. Staff thường token

```bash
curl -X GET "http://localhost:5265/api/news?page=1&pageSize=5" \
  -H "Authorization: Bearer <STAFF_TOKEN>"
```

Expected:

```text
200 OK
viewerMode = AUTHOR
Chỉ bài author_user_id = current staff
canCreateNews phụ thuộc visit_participants ACCEPTED
```

### 20.3. Student token

```bash
curl -X GET "http://localhost:5265/api/news?page=1&pageSize=5" \
  -H "Authorization: Bearer <STUDENT_TOKEN>"
```

Expected:

```text
200 OK
viewerMode = AUTHOR
Chỉ bài author_user_id = current student
```

### 20.4. Staff Leader token

```bash
curl -X GET "http://localhost:5265/api/news?page=1&pageSize=5" \
  -H "Authorization: Bearer <STAFF_LEADER_TOKEN>"
```

Expected:

```text
200 OK
viewerMode = REVIEWER
Chỉ bài campus_id = currentUser.primary_campus_id
canCreateNews = false
```

### 20.5. HO token

```bash
curl -X GET "http://localhost:5265/api/news?page=1&pageSize=5" \
  -H "Authorization: Bearer <HO_TOKEN>"
```

Expected:

```text
200 OK
viewerMode = HO_READONLY
Chỉ bài status = PUBLISHED
canCreateNews = false
```

### 20.6. Status invalid

```bash
curl -X GET "http://localhost:5265/api/news?status=APPROVED" \
  -H "Authorization: Bearer <STAFF_LEADER_TOKEN>"
```

Expected:

```text
400 Bad Request
```

### 20.7. HO filter rejected

```bash
curl -X GET "http://localhost:5265/api/news?status=REJECTED" \
  -H "Authorization: Bearer <HO_TOKEN>"
```

Expected:

```text
400 Bad Request
```

---

## 21. Những điều không được làm

```text
- Không code nhầm UC-88 thành Approve News.
- Không code nhầm sang Public News.
- Không dùng /api/public/news cho màn dashboard.
- Không AllowAnonymous.
- Không cho Admin/Department/Visitor xem News Management list.
- Không cho Staff thường xem bài người khác cùng campus.
- Không cho Student xem bài người khác.
- Không cho HO xem bài PENDING_REVIEW/REJECTED/HIDDEN.
- Không cho Staff Leader tạo bài.
- Không cho HO tạo bài.
- Không dùng status cũ DRAFT/ARCHIVED/APPROVED/VISIBLE.
- Không dùng dynamic permissions/role_permissions.
- Không query DbContext trong Controller.
- Không load all news rồi filter in-memory.
- Không tạo bảng/cột mới nếu SQL v10 đã đủ.
- Không sửa frontend trong task backend này nếu user chỉ yêu cầu backend.
- Không nhét create/update/approve/reject/hide/show logic vào ViewNewsListQueryHandler.
- Không báo hoàn thành nếu chưa build/test hoặc chưa nói rõ phần nào chưa test được.
```

---

## 22. Definition of Done

AI Agent chỉ được báo hoàn thành khi đủ:

```text
[ ] Xác nhận đang triển khai UC-88: View News List.
[ ] Endpoint GET /api/news hoạt động.
[ ] Endpoint yêu cầu authentication.
[ ] Staff thường chỉ thấy bài của chính mình.
[ ] Student chỉ thấy bài của chính mình.
[ ] Staff Leader thấy bài theo campus mình.
[ ] HO chỉ thấy bài PUBLISHED.
[ ] Role Admin/Department/Visitor bị 403.
[ ] canCreateNews đúng cho Staff/Student dựa trên visit_participants ACCEPTED.
[ ] canCreateNews = false cho Staff Leader và HO.
[ ] Status filter dùng PENDING_REVIEW/REJECTED/PUBLISHED/HIDDEN.
[ ] Reject status cũ APPROVED/DRAFT/ARCHIVED/VISIBLE.
[ ] Search LIKE/Contains hoạt động trong scope.
[ ] Search + filter + role scope dùng AND logic.
[ ] Sort mặc định created_at DESC, news_id DESC.
[ ] Pagination hoạt động.
[ ] Response có viewerMode và availableActions.
[ ] Không dùng public endpoint.
[ ] Không query DbContext trong Controller.
[ ] Không dùng dynamic permissions.
[ ] dotnet build PASS.
[ ] Có unit/integration test hoặc manual API test rõ ràng.
```

---

## 23. Output mong muốn từ AI Agent sau khi code

Sau khi code xong, báo cáo theo format:

```text
1. Files read
- ...

2. Files changed
- backend/PEMS.Api/Controllers/NewsController.cs
- backend/PEMS.Application/News/Queries/ViewNewsList/...
- backend/PEMS.Domain/Constants/NewsConstants.cs
- tests/...

3. Endpoint implemented
- GET /api/news

4. Authorization/scope
- Staff thường: author_user_id = currentUserId
- Student: author_user_id = currentUserId
- Staff Leader: campus_id = currentUser.primary_campus_id
- HO: status = PUBLISHED
- Admin/Department/Visitor: 403

5. Logic implemented
- status filter
- LIKE search
- default sort created_at DESC, news_id DESC
- pagination
- viewerMode
- canCreateNews based on ACCEPTED visit_participants
- availableActions

6. Validation
- page/pageSize
- status
- sortBy/sortDirection

7. Test result
- dotnet build: PASS/FAIL
- dotnet test: PASS/FAIL
- Manual API test: PASS/FAIL

8. Notes / Risks
- ...
```
