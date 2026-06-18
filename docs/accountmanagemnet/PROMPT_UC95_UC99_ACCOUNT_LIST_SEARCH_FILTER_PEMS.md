# PROMPT TRIỂN KHAI UC-95 + UC-99 — ACCOUNT LIST / SEARCH / FILTER PEMS

> File này dùng để giao cho AI/code agent sửa code trực tiếp trong project PEMS.  
> Mục tiêu: triển khai thật **UC-95 View Account List** và **UC-99 Search and Filter Accounts** để trang Account Management lấy dữ liệu thật từ API, không còn phụ thuộc mock.

---

## 0. Vai trò của AI/code agent

Bạn là **Senior .NET Clean Architecture Developer + React TypeScript Engineer + Security Reviewer**.

Bạn đang làm trên project **PEMS — Partnership Engagement Management System** hiện tại.  
Nhiệm vụ của bạn là triển khai đầy đủ backend + frontend tối thiểu cho:

```text
UC-95 View Account List
UC-99 Search and Filter Accounts
```

Không làm lại toàn bộ hệ thống. Không phá UI hiện có. Không làm lan sang các UC khác nếu không cần thiết.

---

## 1. Bối cảnh nghiệp vụ

PEMS đã có hoặc đang triển khai:

```text
- Dual Portal Authentication: VISITOR / INTERNAL.
- Core Auth Backend SSO-first.
- Internal user không được auto-create khi login SSO/FEID lần đầu.
- Visitor có thể auto-create khi login đúng Visitor portal.
- Staff Leader/Admin/HO cần Account Management để xem, tìm, lọc tài khoản.
- UC-96 Create Account và UC-100 Update Account Role phụ thuộc vào danh sách account chạy thật.
```

Vì vậy phase này phải làm cho Account Management **hiển thị danh sách account thật**, có search/filter/paging/sort, và áp dụng đúng scope theo role/campus.

---

## 2. Phạm vi làm lần này

## 2.1 In scope

Làm các phần sau:

```text
1. Backend UC-95 View Account List
   - Query handler chạy thật.
   - Query DB thật.
   - Có paging.
   - Có sort.
   - Có scope theo current user.
   - DTO không lộ thông tin nhạy cảm.

2. Backend UC-99 Search and Filter Accounts
   - Search keyword theo email/fullName/role/campus/department nếu phù hợp.
   - Filter theo role/status/campus/department/provider/createdVia/accountType.
   - Có paging + sort giống list.
   - Không duplicate logic với UC-95.

3. API endpoint
   - GET /api/accounts hoặc route hiện có tương ứng.
   - Nhận query params.
   - Trả PaginatedResult.

4. Frontend tối thiểu
   - Tạo/cập nhật account-management API.
   - Tạo/cập nhật hook lấy list account.
   - Nối trang Account Management hiện tại vào API thật.
   - Giữ layout/UI hiện tại.
   - Có loading/empty/error state.
   - Không xóa mock nếu còn cần fallback, nhưng mặc định phải dùng API thật.

5. Error handling
   - Backend trả errorCode + message nếu lỗi.
   - Frontend map/hiển thị lỗi dễ hiểu.
```

## 2.2 Out of scope

Không làm trong phase này:

```text
- Không implement full UC-96 Create Account nếu chưa có.
- Không implement full UC-97 Manage Account Status.
- Không implement full UC-98 View Account Details.
- Không implement full UC-100 Update Account Role.
- Không làm FEID full.
- Không refactor lại toàn bộ trang Account Management.
- Không đổi mô hình role toàn hệ thống.
- Không auto-migrate database bừa.
- Không xóa mock data nếu chưa chắc không còn import.
```

Nếu UI đang có nút Create/Edit/Status thì chỉ đảm bảo không crash. Action thật để phase sau.

---

## 3. Tài liệu và code bắt buộc đọc trước

Trước khi sửa, đọc các file:

```text
PROJECT_STRUCTURE_FULL.md
PROJECT_OVERVIEW.md
USE_CASE_LIST.md
USE_CASE_NOTES_UPDATED_SSO_FIRST.md
USE_CASE_NOTES.md
CLEAN_ARCHITECTURE.md
Technology.md
database/scripts/*.sql
database/seed/*.sql
```

Quét code thật:

```text
backend/PEMS.Api/Controllers/AccountsController.cs
backend/PEMS.Api/Filters/PermissionAuthorizeAttribute.cs
backend/PEMS.Api/Middleware/ExceptionHandlingMiddleware.cs

backend/PEMS.Application/Accounts/Queries/ViewAccountList/
backend/PEMS.Application/Accounts/Queries/SearchandFilterAccounts/
backend/PEMS.Application/Accounts/Commands/CreateAccount/
backend/PEMS.Application/Accounts/Commands/UpdateAccountRole/
backend/PEMS.Application/Accounts/Models/
backend/PEMS.Application/Common/Interfaces/IApplicationDbContext.cs
backend/PEMS.Application/Common/Interfaces/ICurrentUserService.cs
backend/PEMS.Application/Common/Exceptions/

backend/PEMS.Domain/Entities/User.cs
backend/PEMS.Domain/Entities/Role.cs
backend/PEMS.Domain/Entities/Campus.cs
backend/PEMS.Domain/Entities/Department.cs
backend/PEMS.Domain/Entities/UserAuthProvider.cs
backend/PEMS.Domain/Entities/UserSession.cs
backend/PEMS.Domain/Constants/
backend/PEMS.Domain/Enums/

backend/PEMS.Infrastructure/Persistence/ApplicationDbContext.cs
backend/PEMS.Infrastructure/Persistence/Configurations/UserConfiguration.cs
backend/PEMS.Infrastructure/Persistence/Configurations/RoleConfiguration.cs
backend/PEMS.Infrastructure/Persistence/Configurations/CampusConfiguration.cs
backend/PEMS.Infrastructure/Persistence/Configurations/DepartmentConfiguration.cs
backend/PEMS.Infrastructure/Repositories/UserRepository.cs nếu có

frontend/pems-react/src/pages/**
frontend/pems-react/src/pages/dashboard/**
frontend/pems-react/src/features/account-management/**
frontend/pems-react/src/shared/api/endpoints.ts
frontend/pems-react/src/shared/api/httpClient.ts
frontend/pems-react/src/shared/auth/**
frontend/pems-react/src/shared/types/**
```

Nếu tên folder khác thực tế, dùng cấu trúc thật đang có và ghi lại trong changelog.

---

## 4. Quy tắc kiến trúc bắt buộc

## 4.1 API layer

`AccountsController` chỉ được:

```text
- Nhận query params.
- Gọi IMediator.Send().
- Trả response.
```

Không được:

```text
- Query DbContext trực tiếp trong controller.
- Viết business rule scope role/campus trong controller.
- Map entity phức tạp trong controller.
- Tự parse permission thủ công trong controller nếu đã có attribute/pipeline.
```

Ví dụ đúng:

```csharp
[HttpGet]
[PermissionAuthorize("UC-95.VIEW_ACCOUNT_LIST")]
public async Task<IActionResult> GetAccounts([FromQuery] ViewAccountListQuery query)
{
    var result = await _mediator.Send(query);
    return Ok(result);
}
```

Nếu project dùng `ApiResponse<T>`, bọc theo chuẩn hiện tại.

## 4.2 Application layer

Query handler chịu trách nhiệm:

```text
- Validate nghiệp vụ đọc.
- Lấy current user.
- Áp dụng scope theo role/campus/permission.
- Gọi DbContext/repository/query service.
- Trả DTO + pagination.
```

## 4.3 Infrastructure / Persistence

Query read-only phải:

```text
- Dùng AsNoTracking().
- Projection Select trực tiếp sang DTO.
- Không Include dư nếu có thể join/select.
- Tránh N+1.
- Không trả passwordHash/token/providerSubject/secret.
```

## 4.4 Frontend

Frontend phải:

```text
- Không đổi layout lớn.
- Không xóa component/page cũ.
- Chỉ nối data source từ mock sang API.
- Có debounce search nếu search theo input.
- Có loading/error/empty state.
- Không hardcode role/campus nếu API đã trả.
```

---

## 5. Permission và scope truy cập

## 5.1 Permission liên quan

Các UC liên quan:

```text
UC-95 View Account List
UC-99 Search and Filter Accounts
```

Endpoint list/search phải yêu cầu permission:

```text
UC-95.VIEW_ACCOUNT_LIST
UC-99.SEARCH_AND_FILTER_ACCOUNTS
```

Nếu hệ thống hiện chỉ cho một permission attribute trên endpoint, dùng `UC-95` cho `GET /api/accounts`, còn `UC-99` có thể dùng chung trong handler hoặc route search riêng nếu project đang tách.

Không được để endpoint Account List `[AllowAnonymous]`.

---

## 5.2 Scope theo role

Phải áp dụng scope để user không nhìn quá quyền.

### ADMIN

```text
- Xem toàn bộ account.
- Filter mọi role/status/campus/department/provider.
- Có thể thấy HO/Admin/Staff/Dept/Student/Visitor.
```

### HO

```text
- Xem toàn bộ account theo quyền hiện tại.
- Có thể filter mọi campus.
- Có thể xem account nội bộ toàn hệ thống.
```

### STAFF_L / Staff Leader

Nếu project có `role = STAFF` và `subRole = STAFF_L`, dùng subRole để phân biệt.

```text
- Chỉ xem account trong campus của mình.
- Xem STAFF/DEPT/STUDENT thuộc campus mình nếu có quyền.
- Có thể tìm Visitor theo email/keyword để chuẩn bị UC-100 UpdateRole.
- Không được xem account campus khác.
- Không được xem/sửa ADMIN/HO nếu policy hệ thống không cho.
```

Quy tắc an toàn cho Visitor:

```text
- Không dump toàn bộ Visitor không campus cho Staff Leader.
- Nếu chưa có mapping Visitor theo visit request/campus, Staff Leader chỉ được thấy Visitor khi có keyword đủ rõ, ưu tiên exact email search.
- Nếu không có keyword, không trả toàn bộ Visitor cho Staff Leader.
```

### STAFF_P / Staff thường

```text
- Nếu có UC-95/UC-99 thì xem trong campus của mình.
- Không xem ADMIN/HO.
- Không xem campus khác.
```

### DEPT / STUDENT / VISITOR

```text
- Mặc định không có quyền vào Account Management.
- Nếu không có UC-95/UC-99 thì trả 403.
- Không được list account hệ thống.
```

---

## 6. API contract

## 6.1 Endpoint đề xuất

Ưu tiên dùng route hiện có. Nếu chưa có, tạo:

```http
GET /api/accounts
```

Query params:

```text
page
pageSize
keyword
roleCode
subRole
status
campusId
departmentId
providerType
createdVia
accountType
hasCampus
fromDate
toDate
lastLoginFrom
lastLoginTo
sortBy
sortDirection
```

Ví dụ:

```http
GET /api/accounts?page=1&pageSize=20&keyword=nguyen&roleCode=STAFF&campusId=1&status=ACTIVE&sortBy=createdAt&sortDirection=desc
```

## 6.2 Query model

Tạo hoặc cập nhật:

```csharp
public sealed class ViewAccountListQuery : IRequest<PaginatedResult<AccountListItemDto>>
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;

    public string? Keyword { get; init; }
    public string? RoleCode { get; init; }
    public string? SubRole { get; init; }
    public string? Status { get; init; }
    public int? CampusId { get; init; }
    public int? DepartmentId { get; init; }
    public string? ProviderType { get; init; }
    public string? CreatedVia { get; init; }
    public string? AccountType { get; init; } // INTERNAL | VISITOR | ALL
    public bool? HasCampus { get; init; }

    public DateTime? FromDate { get; init; }
    public DateTime? ToDate { get; init; }
    public DateTime? LastLoginFrom { get; init; }
    public DateTime? LastLoginTo { get; init; }

    public string? SortBy { get; init; } = "createdAt";
    public string? SortDirection { get; init; } = "desc";
}
```

Nếu project đã có `SearchandFilterAccountsQuery`, có thể:

```text
Option A: hợp nhất logic vào ViewAccountListQuery.
Option B: để SearchandFilterAccountsQuery gọi chung AccountQueryService.
```

Không duplicate 2 query gần giống nhau.

## 6.3 Validator

Tạo/cập nhật validator:

```csharp
public sealed class ViewAccountListQueryValidator : AbstractValidator<ViewAccountListQuery>
{
    public ViewAccountListQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);

        RuleFor(x => x.SortDirection)
            .Must(x => string.IsNullOrWhiteSpace(x) ||
                       x.Equals("asc", StringComparison.OrdinalIgnoreCase) ||
                       x.Equals("desc", StringComparison.OrdinalIgnoreCase))
            .WithMessage("Sort direction must be asc or desc.");

        RuleFor(x => x.SortBy)
            .Must(BeAllowedSortColumn)
            .When(x => !string.IsNullOrWhiteSpace(x.SortBy))
            .WithMessage("Sort column is not supported.");
    }

    private static bool BeAllowedSortColumn(string? sortBy)
    {
        var allowed = new[]
        {
            "createdAt",
            "updatedAt",
            "lastLoginAt",
            "email",
            "fullName",
            "role",
            "status",
            "campus"
        };

        return allowed.Contains(sortBy, StringComparer.OrdinalIgnoreCase);
    }
}
```

## 6.4 Response DTO

```csharp
public sealed class AccountListItemDto
{
    public int UserId { get; init; }
    public string Email { get; init; } = default!;
    public string FullName { get; init; } = default!;

    public string RoleCode { get; init; } = default!;
    public string RoleName { get; init; } = default!;
    public string? SubRole { get; init; }

    public int? CampusId { get; init; }
    public string? CampusCode { get; init; }
    public string? CampusName { get; init; }

    public int? DepartmentId { get; init; }
    public string? DepartmentName { get; init; }

    public string Status { get; init; } = default!;
    public string? CreatedVia { get; init; }

    public IReadOnlyList<string> Providers { get; init; } = Array.Empty<string>();

    public DateTime? LastLoginAt { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }

    public bool CanViewDetails { get; init; }
    public bool CanUpdateRole { get; init; }
    public bool CanManageStatus { get; init; }
}
```

Không trả:

```text
password_hash
refresh_token
refresh_token_hash
security_stamp
provider_subject
provider_uid
otp_token
reset_token
```

## 6.5 Paginated result

Nếu project chưa có type chuẩn, tạo:

```csharp
public sealed class PaginatedResult<T>
{
    public IReadOnlyList<T> Items { get; init; } = Array.Empty<T>();
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalItems { get; init; }
    public int TotalPages { get; init; }
    public bool HasNextPage { get; init; }
    public bool HasPreviousPage { get; init; }

    public static PaginatedResult<T> Create(
        IReadOnlyList<T> items,
        int page,
        int pageSize,
        int totalItems)
    {
        var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);

        return new PaginatedResult<T>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalItems = totalItems,
            TotalPages = totalPages,
            HasNextPage = page < totalPages,
            HasPreviousPage = page > 1
        };
    }
}
```

Nếu đã có `PagedResult`, dùng type hiện có, không tạo trùng.

---

## 7. Backend query logic

## 7.1 Base query

Pseudo-code:

```csharp
var query = _dbContext.Users
    .AsNoTracking()
    .Where(u => !u.IsDeleted) // nếu có soft-delete
    .Select(u => new
    {
        User = u,
        Role = u.Role,
        Campus = u.PrimaryCampus,
        Department = u.Department,
        Providers = u.AuthProviders.Select(p => p.ProviderType).Distinct()
    });
```

Nếu navigation chưa có, dùng join theo schema thật.

## 7.2 Apply current-user scope

Pseudo-code:

```csharp
var currentUser = await _currentUserService.GetRequiredUserAsync(ct);

if (currentUser.IsAdmin || currentUser.IsHo)
{
    // no campus restriction
}
else if (currentUser.IsStaffLeader)
{
    query = query.Where(x =>
        x.User.PrimaryCampusId == currentUser.CampusId
        || (
            x.Role.Code == "VISITOR"
            && !string.IsNullOrWhiteSpace(request.Keyword)
            // ưu tiên exact email search nếu không có mapping visitor-campus
        ));
}
else if (currentUser.HasPermission("UC-95.VIEW_ACCOUNT_LIST"))
{
    query = query.Where(x => x.User.PrimaryCampusId == currentUser.CampusId);
}
else
{
    throw new ForbiddenException("Bạn không có quyền xem danh sách tài khoản.");
}
```

Không được tin `campusId` frontend gửi để vượt scope.

## 7.3 Apply filters

### Keyword

Search keyword theo:

```text
email
fullName/displayName
phone nếu có
roleCode/roleName
campusCode/campusName
departmentName
```

Pseudo-code:

```csharp
if (!string.IsNullOrWhiteSpace(request.Keyword))
{
    var keyword = request.Keyword.Trim().ToLower();

    query = query.Where(x =>
        x.User.Email.ToLower().Contains(keyword) ||
        x.User.FullName.ToLower().Contains(keyword) ||
        x.Role.Code.ToLower().Contains(keyword) ||
        x.Role.Name.ToLower().Contains(keyword) ||
        (x.Campus != null && x.Campus.CampusName.ToLower().Contains(keyword)) ||
        (x.Department != null && x.Department.DepartmentName.ToLower().Contains(keyword)));
}
```

Nếu DB collation case-insensitive, không cần ToLower nhiều. Nhưng phải tránh lỗi null.

### Role

```csharp
if (!string.IsNullOrWhiteSpace(request.RoleCode))
{
    query = query.Where(x => x.Role.Code == request.RoleCode);
}
```

### SubRole

Chỉ áp dụng nếu bảng users có `sub_role` hoặc field tương đương:

```csharp
if (!string.IsNullOrWhiteSpace(request.SubRole))
{
    query = query.Where(x => x.User.SubRole == request.SubRole);
}
```

Nếu không có field, không tạo DB mới tùy tiện. Ghi TODO.

### Status

```csharp
if (!string.IsNullOrWhiteSpace(request.Status))
{
    query = query.Where(x => x.User.Status == request.Status);
}
```

### Campus

```csharp
if (request.CampusId.HasValue)
{
    query = query.Where(x => x.User.PrimaryCampusId == request.CampusId.Value);
}
```

Nhưng trước đó đã apply scope. Nếu Staff Leader filter campus khác, kết quả phải rỗng hoặc 403 tùy policy. Ưu tiên 403 nếu cố tình vượt scope.

### Department

```csharp
if (request.DepartmentId.HasValue)
{
    query = query.Where(x => x.User.DepartmentId == request.DepartmentId.Value);
}
```

### ProviderType

```csharp
if (!string.IsNullOrWhiteSpace(request.ProviderType))
{
    query = query.Where(x => x.User.AuthProviders.Any(p => p.ProviderType == request.ProviderType));
}
```

### CreatedVia

```csharp
if (!string.IsNullOrWhiteSpace(request.CreatedVia))
{
    query = query.Where(x => x.User.CreatedVia == request.CreatedVia);
}
```

### AccountType

```text
INTERNAL: role != VISITOR
VISITOR: role == VISITOR
ALL: không lọc
```

### HasCampus

```csharp
if (request.HasCampus.HasValue)
{
    query = request.HasCampus.Value
        ? query.Where(x => x.User.PrimaryCampusId != null)
        : query.Where(x => x.User.PrimaryCampusId == null);
}
```

### Date filters

```csharp
if (request.FromDate.HasValue)
    query = query.Where(x => x.User.CreatedAt >= request.FromDate.Value);

if (request.ToDate.HasValue)
    query = query.Where(x => x.User.CreatedAt < request.ToDate.Value.AddDays(1));
```

Last login tương tự.

## 7.4 Sort

Allowed sort:

```text
createdAt
updatedAt
lastLoginAt
email
fullName
role
status
campus
```

Không dùng dynamic string raw SQL.

Pseudo-code:

```csharp
query = (request.SortBy?.ToLower(), request.SortDirection?.ToLower()) switch
{
    ("email", "asc") => query.OrderBy(x => x.User.Email),
    ("email", _) => query.OrderByDescending(x => x.User.Email),

    ("fullname", "asc") => query.OrderBy(x => x.User.FullName),
    ("fullname", _) => query.OrderByDescending(x => x.User.FullName),

    ("role", "asc") => query.OrderBy(x => x.Role.Code),
    ("role", _) => query.OrderByDescending(x => x.Role.Code),

    ("status", "asc") => query.OrderBy(x => x.User.Status),
    ("status", _) => query.OrderByDescending(x => x.User.Status),

    ("lastloginat", "asc") => query.OrderBy(x => x.User.LastLoginAt),
    ("lastloginat", _) => query.OrderByDescending(x => x.User.LastLoginAt),

    ("updatedat", "asc") => query.OrderBy(x => x.User.UpdatedAt),
    ("updatedat", _) => query.OrderByDescending(x => x.User.UpdatedAt),

    ("campus", "asc") => query.OrderBy(x => x.Campus.CampusName),
    ("campus", _) => query.OrderByDescending(x => x.Campus.CampusName),

    ("createdat", "asc") => query.OrderBy(x => x.User.CreatedAt),
    _ => query.OrderByDescending(x => x.User.CreatedAt)
};
```

## 7.5 Pagination

```csharp
var totalItems = await query.CountAsync(ct);

var items = await query
    .Skip((request.Page - 1) * request.PageSize)
    .Take(request.PageSize)
    .Select(...)
    .ToListAsync(ct);

return PaginatedResult<AccountListItemDto>.Create(
    items,
    request.Page,
    request.PageSize,
    totalItems);
```

---

## 8. CanAction flags

Trong DTO nên trả flags để frontend ẩn/hiện button:

```text
CanViewDetails
CanUpdateRole
CanManageStatus
```

Rule gợi ý:

```text
ADMIN/HO:
- CanViewDetails = true
- CanUpdateRole = true theo permission UC-100
- CanManageStatus = true theo permission UC-97

STAFF_L:
- CanViewDetails = true nếu cùng campus hoặc Visitor search hợp lệ
- CanUpdateRole = true nếu cùng campus hoặc Visitor cần convert
- CanManageStatus = true nếu cùng campus và có UC-97
- Không true với ADMIN/HO

STAFF_P:
- tùy permission, thường chỉ CanViewDetails
```

Nếu chưa có permission checker service, tính đơn giản theo permission current user đang có trong CurrentUser hoặc bỏ flags false, nhưng không được mở quá quyền.

---

## 9. Error handling

Tạo/đảm bảo error codes:

```csharp
public static class AccountErrorCodes
{
    public const string AccountListForbidden = "ACCOUNT_LIST_FORBIDDEN";
    public const string InvalidAccountFilter = "INVALID_ACCOUNT_FILTER";
    public const string CampusScopeForbidden = "CAMPUS_SCOPE_FORBIDDEN";
    public const string UnsupportedSortColumn = "UNSUPPORTED_SORT_COLUMN";
}
```

Response lỗi:

```json
{
  "success": false,
  "errorCode": "CAMPUS_SCOPE_FORBIDDEN",
  "message": "Bạn không có quyền xem tài khoản ở cơ sở này."
}
```

Status:

```text
400 INVALID_ACCOUNT_FILTER / UNSUPPORTED_SORT_COLUMN
403 ACCOUNT_LIST_FORBIDDEN / CAMPUS_SCOPE_FORBIDDEN
500 lỗi không mong muốn
```

---

## 10. Frontend triển khai

## 10.1 Types

Tạo/cập nhật:

```text
frontend/pems-react/src/features/account-management/types/account.types.ts
```

Type gợi ý:

```ts
export interface AccountListItem {
  userId: number;
  email: string;
  fullName: string;

  roleCode: string;
  roleName: string;
  subRole?: string | null;

  campusId?: number | null;
  campusCode?: string | null;
  campusName?: string | null;

  departmentId?: number | null;
  departmentName?: string | null;

  status: string;
  createdVia?: string | null;
  providers: string[];

  lastLoginAt?: string | null;
  createdAt: string;
  updatedAt?: string | null;

  canViewDetails: boolean;
  canUpdateRole: boolean;
  canManageStatus: boolean;
}

export interface AccountListQueryParams {
  page?: number;
  pageSize?: number;
  keyword?: string;
  roleCode?: string;
  subRole?: string;
  status?: string;
  campusId?: number;
  departmentId?: number;
  providerType?: string;
  createdVia?: string;
  accountType?: 'INTERNAL' | 'VISITOR' | 'ALL';
  hasCampus?: boolean;
  fromDate?: string;
  toDate?: string;
  lastLoginFrom?: string;
  lastLoginTo?: string;
  sortBy?: string;
  sortDirection?: 'asc' | 'desc';
}

export interface PaginatedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalItems: number;
  totalPages: number;
  hasNextPage: boolean;
  hasPreviousPage: boolean;
}
```

## 10.2 API

Tạo/cập nhật:

```text
frontend/pems-react/src/features/account-management/api/accountManagementApi.ts
```

```ts
import { httpClient } from '@/shared/api/httpClient';
import { API_ENDPOINTS } from '@/shared/api/endpoints';
import type {
  AccountListItem,
  AccountListQueryParams,
  PaginatedResult,
} from '../types/account.types';

export const accountManagementApi = {
  getAccounts(params: AccountListQueryParams) {
    return httpClient.get<PaginatedResult<AccountListItem>>(
      API_ENDPOINTS.accounts.list,
      { params }
    );
  },
};
```

Cập nhật endpoints:

```ts
export const API_ENDPOINTS = {
  accounts: {
    list: '/accounts',
    detail: (id: number | string) => `/accounts/${id}`,
    create: '/accounts',
    updateRole: (id: number | string) => `/accounts/${id}/role`,
    manageStatus: (id: number | string) => `/accounts/${id}/status`,
  },
};
```

Điều chỉnh route nếu backend đang dùng `/api/accounts` tùy httpClient baseURL.

## 10.3 Hook

Tạo/cập nhật:

```text
frontend/pems-react/src/features/account-management/hooks/useAccountList.ts
```

Không bắt buộc dùng React Query nếu project chưa có. Nếu đang dùng state thường:

```ts
export function useAccountList(initialParams?: AccountListQueryParams) {
  const [params, setParams] = useState<AccountListQueryParams>({
    page: 1,
    pageSize: 20,
    sortBy: 'createdAt',
    sortDirection: 'desc',
    ...initialParams,
  });

  const [data, setData] = useState<PaginatedResult<AccountListItem> | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const fetchAccounts = useCallback(async () => {
    setLoading(true);
    setError(null);

    try {
      const result = await accountManagementApi.getAccounts(params);
      setData(result);
    } catch (err) {
      setError(getApiErrorMessage(err));
    } finally {
      setLoading(false);
    }
  }, [params]);

  useEffect(() => {
    fetchAccounts();
  }, [fetchAccounts]);

  return {
    data,
    accounts: data?.items ?? [],
    loading,
    error,
    params,
    setParams,
    refetch: fetchAccounts,
  };
}
```

Nếu project có React Query/TanStack Query, dùng style hiện có.

## 10.4 Nối vào Account Management page

Tìm trang hiện tại, ví dụ:

```text
frontend/pems-react/src/pages/dashboard/AccountManagement.tsx
frontend/pems-react/src/pages/AccountManagement.tsx
frontend/pems-react/src/features/account-management/pages/AccountManagementPage.tsx
```

Yêu cầu:

```text
- Thay data mock bằng `useAccountList`.
- Giữ table/card layout hiện có.
- Search input cập nhật `keyword`.
- Filter role/status/campus/department/provider cập nhật params.
- Pagination cập nhật page/pageSize.
- Sort column cập nhật sortBy/sortDirection.
- Loading hiển thị spinner/skeleton hiện có.
- Error hiển thị alert/toast.
- Empty state: "Không tìm thấy tài khoản phù hợp."
```

Không đổi tên component nếu không cần.

## 10.5 Debounce search

Nếu đã có `useDebounce`, dùng lại:

```ts
const debouncedKeyword = useDebounce(keyword, 400);
```

Search nên reset về page 1 khi keyword/filter đổi:

```ts
setParams(prev => ({
  ...prev,
  keyword: debouncedKeyword,
  page: 1,
}));
```

## 10.6 Filter UI

Các filter tối thiểu:

```text
- Keyword
- Role
- Status
- Campus
- Department nếu có data nguồn
- Account type: ALL / INTERNAL / VISITOR
```

Nếu campus/department API chưa có, giữ dropdown hiện tại/mock nhẹ nhưng ghi TODO. Không block UC-95/UC-99 vì thiếu master data UI.

---

## 11. Database/index check

Không auto-migrate. Chỉ kiểm tra và tạo patch nếu thật sự cần.

Query list/search có thể cần index:

```sql
-- Gợi ý, không chạy nếu đã có
CREATE INDEX idx_users_email ON users(email);
CREATE INDEX idx_users_role_id ON users(role_id);
CREATE INDEX idx_users_primary_campus_id ON users(primary_campus_id);
CREATE INDEX idx_users_department_id ON users(department_id);
CREATE INDEX idx_users_status ON users(status);
CREATE INDEX idx_users_created_at ON users(created_at);
CREATE INDEX idx_user_auth_providers_user_id ON user_auth_providers(user_id);
CREATE INDEX idx_user_auth_providers_provider_type ON user_auth_providers(provider_type);
```

Nếu cần patch, tạo:

```text
database/scripts/patch_uc95_uc99_account_list_indexes.sql
```

Patch phải idempotent theo MySQL 8. Không destructive.

---

## 12. Manual test cases bắt buộc

Test bằng Swagger/Postman:

| # | Actor | Query | Expected |
|---|---|---|---|
| 1 | ADMIN | GET /accounts | 200, thấy danh sách toàn hệ thống |
| 2 | ADMIN | keyword email | 200, trả account khớp |
| 3 | ADMIN | filter role=VISITOR | 200, chỉ visitor |
| 4 | ADMIN | filter status=ACTIVE | 200, chỉ active |
| 5 | ADMIN | filter campusId=HN | 200, account campus HN |
| 6 | ADMIN | sortBy=email asc | 200, đúng sort |
| 7 | ADMIN | page/pageSize | 200, metadata đúng |
| 8 | HO | GET /accounts | 200 theo scope HO |
| 9 | STAFF_L campus HN | GET /accounts | 200, chỉ account campus HN theo scope |
| 10 | STAFF_L campus HN | filter campusId=HCM | 403 hoặc empty theo policy, không lộ data HCM |
| 11 | STAFF_L | search exact visitor email | 200, thấy visitor để chuẩn bị UpdateRole nếu policy cho |
| 12 | VISITOR | GET /accounts | 403 |
| 13 | Không token | GET /accounts | 401 |
| 14 | Invalid sortBy | GET /accounts?sortBy=passwordHash | 400 UNSUPPORTED_SORT_COLUMN |
| 15 | pageSize quá lớn | GET /accounts?pageSize=9999 | 400 hoặc clamp max 100 |
| 16 | providerType=GOOGLE | 200, chỉ account có Google provider |
| 17 | accountType=INTERNAL | 200, không có VISITOR |
| 18 | accountType=VISITOR | 200, chỉ VISITOR |
| 19 | hasCampus=false | 200, account campus null |
| 20 | keyword không có kết quả | 200, items empty, totalItems=0 |

Test frontend:

```text
[ ] Mở Account Management không còn dùng mock mặc định.
[ ] Loading hiển thị khi gọi API.
[ ] Table/card hiển thị dữ liệu từ DB.
[ ] Search đổi keyword gọi API.
[ ] Filter role/status/campus gọi API.
[ ] Pagination next/prev hoạt động.
[ ] Sort hoạt động nếu UI có sort.
[ ] Empty state đúng.
[ ] Lỗi 401/403 hiển thị message rõ.
[ ] Không crash các nút create/update/status hiện có.
```

---

## 13. Build/test command

Backend:

```bash
dotnet restore
dotnet build
```

Nếu có test project:

```bash
dotnet test
```

Frontend:

```bash
cd frontend/pems-react
npm install
npm run build
```

Nếu có lint/typecheck:

```bash
npm run lint
npm run typecheck
```

Nếu `dotnet build PEMS.Api` lỗi copy DLL vì API đang chạy, phải:

```text
- Dừng API process đang giữ DLL.
- Build lại full API.
- Không báo pass nếu mới build PEMS.Application.
```

---

## 14. Output docs/changelog

Sau khi sửa, tạo/cập nhật:

```text
docs/accounts/UC95_UC99_ACCOUNT_LIST_SEARCH_FILTER.md
docs/architecture/REFACTOR_CHANGELOG.md
```

Nội dung changelog:

```markdown
# UC-95 + UC-99 Account List/Search/Filter Changelog

## 1. Summary
## 2. Backend files changed
## 3. Frontend files changed
## 4. API contract
## 5. Filters supported
## 6. Scope/security rules
## 7. Manual test results
## 8. Build result
## 9. TODO next phase
```

---

## 15. Definition of Done

Chỉ được báo hoàn thành khi:

```text
[ ] GET /api/accounts chạy thật.
[ ] Không còn NotImplementedException ở ViewAccountList/SearchandFilterAccounts.
[ ] Có paging metadata.
[ ] Có keyword search.
[ ] Có filter role/status/campus/accountType tối thiểu.
[ ] Có sort tối thiểu createdAt/email/fullName/status.
[ ] Có scope theo current user/campus.
[ ] Không lộ passwordHash/providerSubject/token.
[ ] Account Management page lấy API thật.
[ ] Search/filter trên UI gọi API thật.
[ ] Loading/empty/error state hoạt động.
[ ] dotnet build pass full backend.
[ ] npm run build pass.
[ ] Có changelog/docs.
```

---

## 16. Quy tắc tuyệt đối không được vi phạm

```text
[ ] Không để Account List public/anonymous.
[ ] Không để Staff Leader xem campus khác.
[ ] Không dump toàn bộ Visitor không campus cho Staff Leader nếu chưa có rule an toàn.
[ ] Không trả passwordHash/token/providerSubject.
[ ] Không sort bằng raw SQL string.
[ ] Không query DbContext trong Controller.
[ ] Không phá UI Account Management hiện có.
[ ] Không đổi role model toàn hệ thống.
[ ] Không auto-create account trong UC-95/UC-99.
[ ] Không làm UC-96/UC-100 lan man nếu chưa cần.
[ ] Không báo build pass nếu chỉ build Application nhưng API fail.
```

---

## 17. Gợi ý thứ tự triển khai

Làm theo thứ tự:

```text
PHASE 1 — Quét code Account hiện tại và xác định route thật.
PHASE 2 — Chuẩn hóa DTO/PaginatedResult nếu chưa có.
PHASE 3 — Implement ViewAccountListQuery + Validator.
PHASE 4 — Implement Account query service/repository.
PHASE 5 — Apply current-user scope.
PHASE 6 — Apply search/filter/sort/paging.
PHASE 7 — Update AccountsController.
PHASE 8 — Update frontend types/api/hook.
PHASE 9 — Nối Account Management page với hook.
PHASE 10 — Build backend/frontend.
PHASE 11 — Manual test matrix.
PHASE 12 — Docs/changelog.
```

---

## 18. Báo cáo sau khi hoàn thành

Báo cáo theo format:

```markdown
# Báo cáo UC-95 + UC-99 Account List/Search/Filter

## 1. Tóm tắt
## 2. File đã sửa/thêm
## 3. Backend đã implement gì
## 4. Frontend đã nối API như nào
## 5. API contract cuối cùng
## 6. Filter/sort hỗ trợ
## 7. Scope theo role/campus
## 8. Database/index patch nếu có
## 9. Test case đã chạy
## 10. Kết quả build
## 11. Rủi ro/TODO còn lại
```

---

# Ghi chú quan trọng

Mục tiêu của phase này là **list/search/filter account chạy thật để chuẩn bị demo UC-96 Create Account và UC-100 Update Account Role**.

Ưu tiên chạy thật, đúng scope bảo mật, không lộ dữ liệu nhạy cảm. Không cần refactor đẹp quá nếu làm vỡ UI hiện tại.
