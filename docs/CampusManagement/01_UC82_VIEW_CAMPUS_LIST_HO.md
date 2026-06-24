# UC-82 — View Campus List for HO

> File này đặc tả riêng chức năng **hiển thị danh sách campus có trong database** cho HO.

---

## 1. Thông tin UC

| Field | Value |
|---|---|
| UC ID | UC-82 |
| UC Name | View Campus List |
| Type | UI |
| Primary Actor | HO |
| Module | Campus Management |
| Route | `/dashboard/campus` |
| API | `GET /api/campuses` |

---

## 2. Mục tiêu chức năng

HO mở màn hình Quản lý campus và xem danh sách tất cả campus đang có trong database.

Danh sách phải hiển thị cả campus `ACTIVE` và `INACTIVE`.

Màn hình phải có thêm cột **Mã code** so với UI hiện tại.

---

## 3. Preconditions

```text
HO đã đăng nhập thành công.
Tài khoản HO có status = ACTIVE.
Token/session hợp lệ.
HO truy cập /dashboard/campus.
```

---

## 4. Postconditions

### Success

```text
Danh sách campus được load từ bảng campuses.
Danh sách hiển thị cả ACTIVE và INACTIVE.
Dữ liệu mặc định sort theo name ASC.
Table có đủ cột: STT, Mã code, Tên campus, Cơ sở/Thành phố, Trưởng phòng IC, Trạng thái, Hành động.
Toggle hiển thị đúng theo status.
```

### Failure

```text
Nếu không có campus: hiển thị empty state "Chưa có campus nào."
Nếu token hết hạn: redirect login.
Nếu user không phải HO: backend trả 403.
Nếu server lỗi: hiển thị error state, không crash UI.
```

---

## 5. UI table columns

| Column | Source | Display Rule |
|---|---|---|
| STT | calculated | Theo page index. |
| Mã code | `campuses.campus_code` | Hiển thị uppercase, ví dụ `HN`, `HCM`. |
| Tên campus | `campuses.name` | Bold, có thể click hoặc có action view riêng. |
| Cơ sở / Thành phố | `campuses.city` | Ví dụ `Hà Nội`. |
| Trưởng phòng IC | `users.full_name` từ `ic_head_user_id` | Nếu null hiển thị `Chưa phân công`. |
| Trạng thái | `campuses.status` | Badge `Hoạt động` / `Ngừng hoạt động`. |
| Hành động | FE action | View icon + status toggle. |

---

## 6. Main Flow

```text
[U] Step 1. HO mở /dashboard/campus.

[S] Step 2. Frontend gọi GET /api/campuses?page=1&pageSize=10.

[S] Step 3. Backend kiểm tra current user là HO và account ACTIVE.

[S] Step 4. Backend query bảng campuses.

[S] Step 5. Backend LEFT JOIN users theo campuses.ic_head_user_id = users.user_id để lấy icHeadName.

[S] Step 6. Backend sort mặc định theo campuses.name ASC.

[S] Step 7. Backend trả dữ liệu phân trang.

[S] Step 8. Frontend render table với đủ cột, trong đó có cột Mã code.

[U] Step 9. HO có thể click view icon hoặc toggle status.
```

---

## 7. API request

```http
GET /api/campuses?page=1&pageSize=10
```

Query params:

```ts
export type CampusListQuery = {
  keyword?: string;
  city?: string;
  campusId?: number;
  status?: 'ACTIVE' | 'INACTIVE';
  page?: number;
  pageSize?: number;
  sortBy?: 'name' | 'campusCode' | 'city' | 'status';
  sortOrder?: 'asc' | 'desc';
};
```

---

## 8. API response DTO

```ts
export type CampusListItemDto = {
  campusId: number;
  campusCode: string;
  name: string;
  city: string | null;
  icHeadUserId: number | null;
  icHeadName: string | null;
  status: 'ACTIVE' | 'INACTIVE';
  createdAt: string;
  updatedAt: string | null;
};

export type PagedCampusListResponse = {
  items: CampusListItemDto[];
  page: number;
  pageSize: number;
  totalItems: number;
  totalPages: number;
};
```

---

## 9. Backend implementation notes

Application structure gợi ý:

```text
PEMS.Application/Campuses/Queries/ViewCampusList/
├── ViewCampusListQuery.cs
├── ViewCampusListQueryHandler.cs
├── ViewCampusListQueryValidator.cs
├── CampusListItemDto.cs
└── ViewCampusListResponse.cs
```

Query rule:

```csharp
// Pseudocode
var query = db.Campuses.AsNoTracking()
    .GroupJoin(db.Users.AsNoTracking(),
        c => c.IcHeadUserId,
        u => u.UserId,
        (c, users) => new { Campus = c, Users = users })
    .SelectMany(x => x.Users.DefaultIfEmpty(),
        (x, u) => new CampusListItemDto
        {
            CampusId = x.Campus.CampusId,
            CampusCode = x.Campus.CampusCode,
            Name = x.Campus.Name,
            City = x.Campus.City,
            IcHeadUserId = x.Campus.IcHeadUserId,
            IcHeadName = u != null ? u.FullName : null,
            Status = x.Campus.Status,
            CreatedAt = x.Campus.CreatedAt,
            UpdatedAt = x.Campus.UpdatedAt
        });
```

Không dùng `Include` dư thừa. Ưu tiên projection thẳng sang DTO.

---

## 10. Frontend implementation notes

Màn hình `/dashboard/campus` phải có:

```text
Breadcrumb: Dashboard / Quản lý campus
Title: Quản lý campus
Search input
Dropdown filter campus/city
Dropdown filter status
Button + Thêm mới campus
Table danh sách
Pagination
```

Table desktop cần thêm cột:

```text
MÃ CODE
```

Gợi ý order cột:

```text
STT | MÃ CODE | TÊN CAMPUS | CƠ SỞ | TRƯỞNG PHÒNG IC | TRẠNG THÁI | HÀNH ĐỘNG
```

---

## 11. Business Rules

### BR-82-01 — Only HO can view campus list

Chỉ HO được gọi API và xem màn hình.

### BR-82-02 — Display all statuses by default

Mặc định không filter status. Hiển thị cả `ACTIVE` và `INACTIVE`.

### BR-82-03 — Default sort by name

Mặc định sort `campuses.name ASC`.

### BR-82-04 — IC Head nullable

Nếu `ic_head_user_id` null hoặc user không tồn tại, UI hiển thị `Chưa phân công`.

### BR-82-05 — Code column is mandatory

Danh sách campus bắt buộc có cột `Mã code` map từ `campus_code`.

---

## 12. Verification Criteria

```text
Given HO is logged in
And database has 5 campus records
When HO opens /dashboard/campus
Then the table displays all 5 records
And the table includes column "Mã code"
And each row displays campusCode, name, city, icHeadName, status and actions.
```

```text
Given 4 ACTIVE campuses and 1 INACTIVE campus exist
When HO opens /dashboard/campus without filters
Then all 5 campuses appear
And ACTIVE campuses show green badge + toggle ON
And INACTIVE campus shows gray badge + toggle OFF.
```

```text
Given a Staff user is logged in
When Staff accesses /dashboard/campus or calls GET /api/campuses
Then backend returns HTTP 403
And no campus data is displayed.
```

---

## 13. Definition of Done

```text
[ ] Table lấy dữ liệu thật từ database.
[ ] Không còn mock campus list.
[ ] Có cột Mã code.
[ ] Hiển thị đúng ACTIVE/INACTIVE.
[ ] IC Head null không làm crash UI.
[ ] Sort mặc định theo name ASC.
[ ] Non-HO bị backend chặn 403.
[ ] Backend build pass.
[ ] Frontend build pass.
```
