# PEMS Campus Management — Common Rules for HO

> Dùng file này làm nền bắt buộc trước khi AI Agent code bất kỳ chức năng nhỏ nào trong module **Quản lý Campus của HO**.

---

## 1. Scope chung

Module: **Campus Management / Quản lý campus**

Actor duy nhất được thao tác: **HO**

Các chức năng nhỏ trong module:

1. View Campus List — hiển thị danh sách campus.
2. Search and Filter Campus — tìm kiếm/lọc campus.
3. Add New Campus — thêm mới campus.
4. View Campus Details — xem chi tiết campus.
5. Update Campus — chỉnh sửa thông tin campus.
6. Manage Campus Status — bật/tắt hoạt động campus.

Route frontend đề xuất:

```text
/dashboard/campus
/dashboard/campus/:campusId
```

---

## 2. Authorization bắt buộc

SQL v8.4 refined v6 no dynamic permissions đã bỏ bảng `permissions` và `role_permissions`.

Vì vậy backend/frontend **không được query dynamic permission** và không dùng permission matrix runtime.

Rule đúng:

```text
CurrentUser.role_code == "HO"
CurrentUser.status == "ACTIVE"
```

Gợi ý backend:

```csharp
[RoleAuthorize(RoleCodes.HO)]
```

hoặc policy tương đương.

Không dùng:

```csharp
[PermissionAuthorize("UC-82.VIEW_CAMPUS_LIST")]
```

trừ khi project vẫn còn wrapper cũ nhưng bên trong wrapper đã chuyển sang fixed role policy.

Nếu user không phải HO gọi API trực tiếp:

```text
HTTP 403 Forbidden
Không thay đổi dữ liệu.
Không trả dữ liệu campus.
Ghi security/audit log nếu project đã có cơ chế log.
```

---

## 3. Database source of truth

### 3.1. Bảng `campuses`

Các field dùng trong module:

```text
campus_id BIGINT UNSIGNED PK AUTO_INCREMENT
campus_code VARCHAR(20) NOT NULL UNIQUE
name VARCHAR(150) NOT NULL
city VARCHAR(100) NULL
address VARCHAR(255) NULL
phone VARCHAR(30) NULL
email VARCHAR(150) NULL
ic_head_user_id BIGINT UNSIGNED NULL
status ENUM('ACTIVE','INACTIVE') NOT NULL DEFAULT 'ACTIVE'
created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP
created_by BIGINT UNSIGNED NULL
updated_at DATETIME NULL ON UPDATE CURRENT_TIMESTAMP
updated_by BIGINT UNSIGNED NULL
```

### 3.2. Bảng `departments`

Các field dùng khi tạo phòng ban IC tự động:

```text
department_id BIGINT UNSIGNED PK AUTO_INCREMENT
campus_id BIGINT UNSIGNED NOT NULL
name VARCHAR(150) NOT NULL
department_type ENUM('IC','GENERAL') NOT NULL
head_user_id BIGINT UNSIGNED NULL
status ENUM('ACTIVE','INACTIVE') NOT NULL DEFAULT 'ACTIVE'
created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP
created_by BIGINT UNSIGNED NULL
updated_at DATETIME NULL ON UPDATE CURRENT_TIMESTAMP
updated_by BIGINT UNSIGNED NULL
```

---

## 4. Quy tắc trùng dữ liệu đã chốt

Khi **Create Campus** hoặc **Update Campus**, chỉ cho phép trùng `city`.

| Field | Cho trùng không? | Rule |
|---|---:|---|
| `campus_code` | Không | Mã campus là định danh duy nhất. |
| `name` | Không | Không cho 2 campus cùng tên sau khi normalize. |
| `city` | Có | Thành phố chỉ dùng để hiển thị/lọc, không phải định danh duy nhất. |
| `address` | Không | Một địa chỉ chỉ thuộc một campus. |
| `phone` | Không | Một số điện thoại chỉ thuộc một campus. |
| `email` | Không | Một email chỉ thuộc một campus. |

### 4.1. Normalize trước khi check trùng

Backend phải normalize trước khi validate duplicate:

```text
campus_code: trim + uppercase
name: trim + collapse multiple spaces + compare case-insensitive
city: trim
address: trim + collapse multiple spaces + compare case-insensitive
phone: trim + remove spaces, dots, hyphens, parentheses for duplicate check
email: trim + lowercase
```

Ví dụ phone duplicate:

```text
024 7300 5588
024-7300-5588
(024) 7300.5588
```

phải được xem là cùng một số nếu normalize ra cùng giá trị.

### 4.2. Update phải loại trừ chính campus hiện tại

Khi update campus, duplicate query phải có điều kiện:

```sql
WHERE campus_id <> @currentCampusId
```

### 4.3. Thông báo lỗi chuẩn

```text
Mã campus đã tồn tại.
Tên campus đã tồn tại.
Địa chỉ này đã được sử dụng cho campus khác.
Số điện thoại này đã được sử dụng cho campus khác.
Email này đã được sử dụng cho campus khác.
```

---

## 5. Optional database unique index patch

Hiện schema gốc chỉ chắc chắn có unique constraint cho `campus_code`. Nếu muốn chặn trùng chắc ở tầng DB, tạo SQL patch riêng để thêm unique index cho:

```text
name
address
phone
email
```

Nhưng trước khi add unique index phải kiểm tra data seed hiện tại có duplicate hay không. Nếu có duplicate, patch sẽ fail.

Gợi ý kiểm tra duplicate:

```sql
SELECT UPPER(TRIM(campus_code)) AS v, COUNT(*) c FROM campuses GROUP BY v HAVING c > 1;
SELECT LOWER(TRIM(name)) AS v, COUNT(*) c FROM campuses GROUP BY v HAVING c > 1;
SELECT LOWER(TRIM(address)) AS v, COUNT(*) c FROM campuses WHERE address IS NOT NULL GROUP BY v HAVING c > 1;
SELECT LOWER(TRIM(email)) AS v, COUNT(*) c FROM campuses WHERE email IS NOT NULL GROUP BY v HAVING c > 1;
```

Phone cần normalize bằng backend hoặc generated column nếu muốn unique ở DB. Nếu chưa có generated column, backend phải chịu trách nhiệm chính cho unique phone.

---

## 6. API contract tổng hợp

```text
GET    /api/campuses
GET    /api/campuses/filter-options
GET    /api/campuses/{campusId}
POST   /api/campuses
PUT    /api/campuses/{campusId}
PATCH  /api/campuses/{campusId}/status
```

---

## 7. Clean Architecture rule

Controller chỉ:

```text
Nhận route/query/body.
Gọi IMediator.Send().
Trả ApiResponse/ActionResult.
```

Không được:

```text
Query DbContext trực tiếp trong Controller.
Viết business validation trong Controller.
Hard-code role bằng if/else dài trong Controller.
Bỏ qua validation.
Bỏ qua audit.
Trả mock data nếu UC yêu cầu DB thật.
```

Application Layer phải có Command/Query/Handler/Validator/DTO tương ứng.

---

## 8. Audit bắt buộc

Các thao tác sau phải ghi nhận audit:

```text
Create Campus
Update Campus
Enable Campus
Disable Campus
Auto-create IC Department after campus creation
```

Tối thiểu cập nhật:

```text
created_by
updated_by
created_at
updated_at
```

Nếu project có `audit_logs` và `audit_log_changes`, phải ghi log theo chuẩn hiện có.

---

## 9. Definition of Done chung

AI Agent chỉ được báo hoàn thành khi:

```text
[ ] Backend build pass.
[ ] Frontend TypeScript build pass.
[ ] API dùng DB thật, không mock.
[ ] Non-HO bị chặn 403 ở backend.
[ ] Create/Update validate đủ required fields.
[ ] Create/Update chỉ cho trùng city, không cho trùng code/name/address/phone/email.
[ ] Create Campus tự tạo IC department trong cùng transaction.
[ ] Danh sách có cột Mã code.
[ ] Search được theo tên campus và trưởng phòng IC.
[ ] Filter option lấy từ database.
[ ] Detail hiển thị đầy đủ thông tin campus.
[ ] Toggle status cập nhật DB và UI đúng.
```
