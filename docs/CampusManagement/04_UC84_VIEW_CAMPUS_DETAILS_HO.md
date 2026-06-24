# UC-84 — View Campus Details for HO

> File này đặc tả riêng chức năng **xem chi tiết đầy đủ thông tin campus** cho HO.

---

## 1. Thông tin UC

| Field | Value |
|---|---|
| UC ID | UC-84 |
| UC Name | View Campus Details |
| Type | UI |
| Primary Actor | HO |
| Module | Campus Management |
| Route | `/dashboard/campus/:campusId` |
| API | `GET /api/campuses/{campusId}` |

---

## 2. Mục tiêu chức năng

HO click icon xem chi tiết ở danh sách campus và xem đầy đủ thông tin campus.

Màn detail hiện tại không được chỉ hiển thị `Cơ sở đăng ký` và `Trưởng phòng IC`; phải hiển thị đầy đủ:

```text
Mã code
Tên campus
Thành phố
Địa chỉ
Số điện thoại
Email
Trưởng phòng IC
Trạng thái
Thông tin phòng ban IC
Audit information
```

---

## 3. Preconditions

```text
HO đã đăng nhập thành công.
HO account ACTIVE.
Campus tồn tại trong database.
HO click view icon hoặc truy cập /dashboard/campus/:campusId.
```

---

## 4. Postconditions

### Success

```text
Campus detail page hiển thị đầy đủ thông tin campus.
Có nút hoặc icon edit để chuyển sang UC-85 Update Campus.
Có nút Quay lại danh sách.
Nếu chưa có trưởng phòng IC, hiển thị "Chưa phân công".
Nếu thiếu IC department, hiển thị cảnh báo dữ liệu.
```

### Failure

```text
Nếu campus không tồn tại: backend trả 404, UI hiển thị not found.
Nếu non-HO gọi API: backend trả 403.
Nếu token hết hạn: redirect login.
```

---

## 5. UI information layout

### 5.1. Header

```text
Status badge: Hoạt động / Ngừng hoạt động
City badge: Thành phố
Title: Tên campus
Mã code: campus_code
Edit icon
```

### 5.2. Thông tin cơ sở

| Label | Source |
|---|---|
| Mã campus | `campuses.campus_code` |
| Tên campus | `campuses.name` |
| Thành phố | `campuses.city` |
| Địa chỉ | `campuses.address` |
| Số điện thoại | `campuses.phone` |
| Email | `campuses.email` |
| Trưởng phòng IC | `users.full_name` from `ic_head_user_id` |
| Trạng thái | `campuses.status` |

### 5.3. Thông tin phòng ban IC

| Label | Source |
|---|---|
| Tên phòng ban IC | `departments.name` |
| Loại phòng ban | `departments.department_type` = `IC` |
| Trưởng phòng ban | `departments.head_user_id` join users |
| Trạng thái phòng ban | `departments.status` |

### 5.4. Audit

| Label | Source |
|---|---|
| Ngày tạo | `campuses.created_at` |
| Người tạo | `campuses.created_by` join users nếu có |
| Ngày cập nhật | `campuses.updated_at` |
| Người cập nhật | `campuses.updated_by` join users nếu có |

---

## 6. Main Flow

```text
[U] Step 1. HO ở màn /dashboard/campus.

[U] Step 2. HO click icon mắt của một campus.

[S] Step 3. Frontend điều hướng đến /dashboard/campus/{campusId}.

[S] Step 4. Frontend gọi GET /api/campuses/{campusId}.

[S] Step 5. Backend kiểm tra current user là HO.

[S] Step 6. Backend query campus theo campusId.

[S] Step 7. Backend LEFT JOIN users để lấy icHeadName, createdByName, updatedByName.

[S] Step 8. Backend query IC department theo campusId và department_type = 'IC'.

[S] Step 9. Backend trả CampusDetailDto.

[S] Step 10. Frontend render detail page với đầy đủ thông tin.

[U] Step 11. HO có thể click edit icon để chuyển sang UC-85.

[U] Step 12. HO có thể click Quay lại để về danh sách.
```

---

## 7. API response DTO

```ts
export type CampusDetailDto = {
  campusId: number;
  campusCode: string;
  name: string;
  city: string | null;
  address: string | null;
  phone: string | null;
  email: string | null;
  icHeadUserId: number | null;
  icHeadName: string | null;
  status: 'ACTIVE' | 'INACTIVE';
  createdAt: string;
  createdBy: number | null;
  createdByName: string | null;
  updatedAt: string | null;
  updatedBy: number | null;
  updatedByName: string | null;
  icDepartment: {
    departmentId: number;
    name: string;
    departmentType: 'IC';
    status: 'ACTIVE' | 'INACTIVE';
    headUserId: number | null;
    headUserName: string | null;
  } | null;
};
```

---

## 8. Backend implementation notes

Application structure gợi ý:

```text
PEMS.Application/Campuses/Queries/ViewCampusDetails/
├── ViewCampusDetailsQuery.cs
├── ViewCampusDetailsQueryHandler.cs
└── CampusDetailDto.cs
```

Query requirements:

```text
Use AsNoTracking().
Projection thẳng sang DTO.
Không Include dư thừa.
Nếu campus không tồn tại: throw NotFoundException.
Nếu user không phải HO: HTTP 403.
```

IC department query:

```sql
SELECT *
FROM departments
WHERE campus_id = @campusId
  AND department_type = 'IC'
LIMIT 1;
```

---

## 9. Frontend implementation notes

Detail page cần có:

```text
Breadcrumb: Dashboard / Quản lý campus / Chi tiết campus
Title: Chi tiết Campus
Hero/Header card
Status badge
City badge
Edit icon
Thông tin cơ sở
Thông tin phòng ban IC
Audit info
Button Quay lại
```

Nếu field null:

```text
icHeadName: Chưa phân công
address/phone/email: Chưa cập nhật
icDepartment null: Campus này chưa có phòng ban IC. Vui lòng kiểm tra dữ liệu.
```

---

## 10. Business Rules

### BR-84-01 — Full information required

Detail page phải hiển thị đầy đủ field campus, không chỉ city và IC Head.

### BR-84-02 — IC Head nullable

Nếu `ic_head_user_id` null, không lỗi UI, hiển thị `Chưa phân công`.

### BR-84-03 — IC Department missing is abnormal

Nếu không có department IC, không crash detail page. Hiển thị warning cho HO.

### BR-84-04 — Only HO can view detail

Non-HO bị backend chặn 403.

---

## 11. Alternative Flows

### AF-01 — Campus not found

```text
Backend trả 404.
Frontend hiển thị "Không tìm thấy campus."
Có nút quay lại danh sách.
```

### AF-02 — Session expired

```text
Backend trả 401.
Frontend hiển thị "Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại."
Redirect login.
```

### AF-03 — Missing IC department

```text
Detail vẫn hiển thị campus.
UI hiển thị warning: "Campus này chưa có phòng ban IC. Vui lòng kiểm tra dữ liệu."
```

---

## 12. Verification Criteria

```text
Given campus HN exists with code, name, city, address, phone and email
When HO opens /dashboard/campus/{campusId}
Then the page displays all fields.
```

```text
Given campus HN has ic_head_user_id = NULL
When HO opens campus detail
Then UI displays "Chưa phân công" for Trưởng phòng IC.
```

```text
Given campus HN has an IC department
When HO opens campus detail
Then UI displays department name "Phòng Hợp tác Quốc tế", department type IC and department status.
```

```text
Given campusId does not exist
When HO opens /dashboard/campus/{campusId}
Then backend returns 404
And frontend shows not found state.
```

---

## 13. Definition of Done

```text
[ ] Detail page lấy dữ liệu thật từ API.
[ ] Hiển thị campusCode, name, city, address, phone, email, icHead, status.
[ ] Hiển thị thông tin IC department.
[ ] Hiển thị audit info nếu có.
[ ] Null fields không crash UI.
[ ] Campus not found hiển thị 404 state.
[ ] Non-HO bị chặn 403.
[ ] Backend build pass.
[ ] Frontend build pass.
```
