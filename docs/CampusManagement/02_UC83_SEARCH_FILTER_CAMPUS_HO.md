# UC-83 — Search and Filter Campus for HO

> File này đặc tả riêng chức năng **tìm kiếm và lọc danh sách campus** trong màn Quản lý Campus của HO.

---

## 1. Thông tin UC

| Field | Value |
|---|---|
| UC ID | UC-83 |
| UC Name | Search and Filter Campus |
| Type | UI |
| Primary Actor | HO |
| Module | Campus Management |
| Route | `/dashboard/campus` |
| API | `GET /api/campuses`, `GET /api/campuses/filter-options` |

---

## 2. Mục tiêu chức năng

HO có thể:

```text
Search theo tên campus.
Search theo trưởng phòng IC.
Filter theo campus/thành phố lấy từ database.
Filter theo trạng thái ACTIVE/INACTIVE.
Kết hợp search + filter bằng AND logic.
```

---

## 3. UI controls

| Control | Label | Source | Rule |
|---|---|---|---|
| Search box | `Tìm kiếm campus...` | User input | Search theo `campuses.name` hoặc `users.full_name` của IC Head. |
| Campus/City filter | `Tất cả cơ sở` | Database | Option lấy từ `campuses`; không hard-code. |
| Status filter | `Tất cả trạng thái` | Fixed enum | All / ACTIVE / INACTIVE. |
| Page size | `Hiển thị x bản ghi / trang` | FE config | 5 / 10 / 20. |

---

## 4. Main Flow — Search

```text
[U] Step 1. HO nhập keyword vào ô "Tìm kiếm campus...".

[S] Step 2. Frontend debounce 300–500ms.

[S] Step 3. Frontend gọi GET /api/campuses?keyword={keyword}&page=1.

[S] Step 4. Backend trim keyword.

[S] Step 5. Backend search case-insensitive trên:
- campuses.name
- users.full_name của campus.ic_head_user_id

[S] Step 6. Backend áp dụng các filter khác nếu có.

[S] Step 7. Backend trả kết quả phân trang.

[S] Step 8. Frontend cập nhật table.
```

---

## 5. Main Flow — Filter by campus/city

```text
[U] Step 1. HO mở dropdown "Tất cả cơ sở".

[S] Step 2. Frontend gọi GET /api/campuses/filter-options nếu chưa cache.

[S] Step 3. Backend trả danh sách campus/city lấy từ database.

[U] Step 4. HO chọn một campus/city.

[S] Step 5. Frontend gọi GET /api/campuses với city hoặc campusId tương ứng.

[S] Step 6. Backend filter theo điều kiện đã chọn.

[S] Step 7. Frontend render lại table.
```

---

## 6. Main Flow — Filter by status

```text
[U] Step 1. HO mở dropdown "Tất cả trạng thái".

[U] Step 2. HO chọn "Hoạt động" hoặc "Ngừng hoạt động".

[S] Step 3. Frontend gọi GET /api/campuses?status=ACTIVE hoặc status=INACTIVE.

[S] Step 4. Backend filter theo campuses.status.

[S] Step 5. Frontend render lại table.
```

---

## 7. Query params

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

Rule:

```text
Nếu keyword rỗng sau trim: không áp search.
Nếu city/campusId không có: không áp city/campus filter.
Nếu status không có: không áp status filter.
Search + Filter = AND logic.
Khi filter/search thay đổi: reset page về 1.
```

---

## 8. Filter options DTO

API:

```http
GET /api/campuses/filter-options
```

Response:

```ts
export type CampusFilterOptionsDto = {
  cities: string[];
  campuses: {
    campusId: number;
    campusCode: string;
    name: string;
    city: string | null;
    status: 'ACTIVE' | 'INACTIVE';
  }[];
  statuses: {
    value: 'ACTIVE' | 'INACTIVE';
    label: string;
  }[];
};
```

Frontend có thể dùng `cities` hoặc `campuses` tùy thiết kế dropdown. Nhưng option phải đến từ database, không hard-code.

---

## 9. Backend implementation notes

Application structure gợi ý:

```text
PEMS.Application/Campuses/Queries/SearchAndFilterCampus/
├── SearchAndFilterCampusQuery.cs
├── SearchAndFilterCampusQueryHandler.cs
├── SearchAndFilterCampusQueryValidator.cs
└── CampusListItemDto.cs

PEMS.Application/Campuses/Queries/GetCampusFilterOptions/
├── GetCampusFilterOptionsQuery.cs
├── GetCampusFilterOptionsQueryHandler.cs
└── CampusFilterOptionsDto.cs
```

Pseudocode filter:

```csharp
if (!string.IsNullOrWhiteSpace(request.Keyword))
{
    var keyword = request.Keyword.Trim().ToLower();
    query = query.Where(x =>
        x.Campus.Name.ToLower().Contains(keyword) ||
        (x.IcHeadName != null && x.IcHeadName.ToLower().Contains(keyword)));
}

if (!string.IsNullOrWhiteSpace(request.City))
{
    var city = request.City.Trim().ToLower();
    query = query.Where(x => x.Campus.City != null && x.Campus.City.ToLower() == city);
}

if (request.CampusId.HasValue)
{
    query = query.Where(x => x.Campus.CampusId == request.CampusId.Value);
}

if (!string.IsNullOrWhiteSpace(request.Status))
{
    query = query.Where(x => x.Campus.Status == request.Status);
}
```

---

## 10. Business Rules

### BR-83-01 — Search scope

Search keyword áp dụng trên:

```text
campuses.name
users.full_name của IC Head
```

Không chỉ search theo city như tài liệu cũ.

### BR-83-02 — Filter option from database

Dropdown campus/city phải lấy từ database.

### BR-83-03 — AND logic

Nếu HO vừa search vừa chọn status/campus, kết quả phải thỏa tất cả điều kiện.

### BR-83-04 — City can duplicate

`city` được phép trùng giữa nhiều campus. Filter theo city có thể trả nhiều campus.

### BR-83-05 — Inactive still searchable

Campus INACTIVE vẫn xuất hiện trong màn quản lý và vẫn có thể search/filter.

---

## 11. Alternative Flows

### AF-01 — No result

```text
Nếu không có campus phù hợp:
Hiển thị "Không tìm thấy campus phù hợp."
Giữ nguyên keyword/filter đang chọn.
```

### AF-02 — Clear search

```text
Khi HO xóa keyword:
Frontend gọi lại API không có keyword.
Vẫn giữ filter status/city nếu đang có.
```

### AF-03 — Select all

```text
Khi HO chọn "Tất cả cơ sở" hoặc "Tất cả trạng thái":
Frontend bỏ filter tương ứng khỏi query params.
```

---

## 12. Verification Criteria

```text
Given campus "FPT University Hà Nội" exists
When HO searches "Hà Nội"
Then that campus appears in the list.
```

```text
Given campus HN has IC Head "Nguyễn Văn A"
When HO searches "Nguyễn Văn A"
Then campus HN appears in the result.
```

```text
Given campuses exist in city "Hà Nội" and "Đà Nẵng"
When HO selects city "Hà Nội"
Then only campuses with city = "Hà Nội" are displayed.
```

```text
Given 4 ACTIVE campuses and 1 INACTIVE campus exist
When HO filters status = INACTIVE
Then only the INACTIVE campus is displayed.
```

```text
Given HO searches keyword "A" and filters status = ACTIVE
When backend returns results
Then every result must match keyword AND status ACTIVE.
```

---

## 13. Definition of Done

```text
[ ] Search by campus name works.
[ ] Search by IC Head name works.
[ ] Filter option is loaded from database.
[ ] Filter by city/campus works.
[ ] Filter by status works.
[ ] Search + filter combine by AND logic.
[ ] Clear search restores list with current filters.
[ ] Non-HO receives 403.
[ ] Backend build pass.
[ ] Frontend build pass.
```
