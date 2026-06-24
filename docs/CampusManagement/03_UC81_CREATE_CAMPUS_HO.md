# UC-81 — Add New Campus for HO

> File này đặc tả riêng chức năng **HO thêm mới campus** và backend tự tạo phòng ban IC mặc định cho campus vừa tạo.

---

## 1. Thông tin UC

| Field | Value |
|---|---|
| UC ID | UC-81 |
| UC Name | Add New Campus |
| Type | UI |
| Primary Actor | HO |
| Module | Campus Management |
| Route | `/dashboard/campus` |
| API | `POST /api/campuses` |

---

## 2. Mục tiêu chức năng

HO tạo campus mới với đầy đủ thông tin master data.

Sau khi tạo campus thành công, backend phải tự động tạo phòng ban IC cho campus đó:

```text
campus_id = id của campus vừa tạo
name = "Phòng Hợp tác Quốc tế"
department_type = "IC"
status = "ACTIVE"
head_user_id = NULL
```

Campus và IC department phải được tạo trong cùng transaction.

---

## 3. UI thay đổi so với màn hiện tại

### 3.1. Bỏ field

```text
Chọn trưởng phòng IC
```

Không chọn trưởng phòng IC khi tạo campus.

`campuses.ic_head_user_id` khi create phải là `NULL`.

### 3.2. Đổi label

```text
Chọn vị trí  →  Chọn thành phố
```

### 3.3. Thêm field bắt buộc

```text
Mã code *
Tên campus *
Thành phố *
Địa chỉ *
Số điện thoại *
Email *
```

---

## 4. Preconditions

```text
HO đã đăng nhập thành công.
HO account ACTIVE.
HO đang ở /dashboard/campus.
Backend token/session hợp lệ.
```

---

## 5. Postconditions

### Success

```text
Một row mới được tạo trong campuses.
Một row mới được tạo trong departments với department_type = IC.
Campus mới có status = ACTIVE.
ic_head_user_id = NULL.
Form/modal đóng và reset.
Danh sách campus reload và hiển thị campus mới.
Audit log được ghi.
```

### Failure

```text
Không tạo campus nếu validate lỗi.
Không tạo campus nếu trùng dữ liệu unique.
Không tạo campus mồ côi nếu tạo department IC fail.
Nếu transaction fail, rollback cả campus và department.
```

---

## 6. Request DTO

```ts
export type CreateCampusRequest = {
  campusCode: string;
  name: string;
  city: string;
  address: string;
  phone: string;
  email: string;
};
```

Không có field:

```text
icHeadUserId
```

---

## 7. Response DTO

```ts
export type CreateCampusResponse = {
  campusId: number;
  campusCode: string;
  name: string;
  city: string;
  address: string;
  phone: string;
  email: string;
  icHeadUserId: null;
  status: 'ACTIVE';
  icDepartment: {
    departmentId: number;
    campusId: number;
    name: 'Phòng Hợp tác Quốc tế';
    departmentType: 'IC';
    status: 'ACTIVE';
  };
};
```

---

## 8. Main Flow

```text
[U] Step 1. HO click "+ Thêm mới campus".

[S] Step 2. Frontend mở modal Add New Campus.

[S] Step 3. Modal hiển thị các field:
- Mã code *
- Tên campus *
- Chọn thành phố *
- Địa chỉ *
- Số điện thoại *
- Email *

[U] Step 4. HO nhập dữ liệu hợp lệ.

[U] Step 5. HO click "Tạo mới".

[S] Step 6. Frontend validate required + format cơ bản.

[S] Step 7. Frontend gọi POST /api/campuses.

[S] Step 8. Backend kiểm tra user là HO.

[S] Step 9. Backend validate DTO bằng FluentValidation.

[S] Step 10. Backend normalize dữ liệu.

[S] Step 11. Backend check duplicate:
- campus_code không trùng
- name không trùng
- address không trùng
- phone không trùng
- email không trùng
- city được phép trùng, không check duplicate

[S] Step 12. Backend mở transaction.

[S] Step 13. Backend insert campuses:
- campus_code = normalized campusCode
- name = normalized name
- city = normalized city
- address = normalized address
- phone = normalized phone display value hoặc raw cleaned value theo convention hiện có
- email = lowercase email
- ic_head_user_id = NULL
- status = 'ACTIVE'
- created_by = current HO user_id
- created_at = now

[S] Step 14. Backend lấy campus_id vừa tạo.

[S] Step 15. Backend insert departments:
- campus_id = new campus_id
- name = 'Phòng Hợp tác Quốc tế'
- department_type = 'IC'
- head_user_id = NULL
- status = 'ACTIVE'
- created_by = current HO user_id
- created_at = now

[S] Step 16. Backend commit transaction.

[S] Step 17. Backend ghi audit log.

[S] Step 18. Frontend đóng modal, reset form, reload list và hiển thị campus mới.
```

---

## 9. Validation rules

| Field | Required | Rule |
|---|---:|---|
| `campusCode` | Yes | Trim, uppercase, max 20, unique, chỉ chữ/số/dấu `-`/`_`. |
| `name` | Yes | Trim, max 150, unique case-insensitive. |
| `city` | Yes | Trim, max 100, được phép trùng. |
| `address` | Yes | Trim, max 255, unique case-insensitive. |
| `phone` | Yes | Trim, max 30, valid phone, unique sau normalize. |
| `email` | Yes | Trim, lowercase, max 150, valid email, unique. |

---

## 10. Duplicate rules đã chốt

Chỉ cho trùng `city`.

Không cho trùng:

```text
campus_code
name
address
phone
email
```

Thông báo lỗi:

```text
Mã campus đã tồn tại.
Tên campus đã tồn tại.
Địa chỉ này đã được sử dụng cho campus khác.
Số điện thoại này đã được sử dụng cho campus khác.
Email này đã được sử dụng cho campus khác.
```

---

## 11. Backend implementation notes

Application structure gợi ý:

```text
PEMS.Application/Campuses/Commands/CreateCampus/
├── CreateCampusCommand.cs
├── CreateCampusCommandHandler.cs
├── CreateCampusCommandValidator.cs
└── CreateCampusResponse.cs
```

Handler bắt buộc dùng transaction.

Pseudocode:

```csharp
// 1. Normalize
var code = request.CampusCode.Trim().ToUpperInvariant();
var name = NormalizeText(request.Name);
var city = NormalizeText(request.City);
var address = NormalizeText(request.Address);
var phone = NormalizePhone(request.Phone);
var email = request.Email.Trim().ToLowerInvariant();

// 2. Duplicate checks
if (await CampusCodeExists(code)) throw Conflict("Mã campus đã tồn tại.");
if (await CampusNameExists(name)) throw Conflict("Tên campus đã tồn tại.");
if (await CampusAddressExists(address)) throw Conflict("Địa chỉ này đã được sử dụng cho campus khác.");
if (await CampusPhoneExists(phone)) throw Conflict("Số điện thoại này đã được sử dụng cho campus khác.");
if (await CampusEmailExists(email)) throw Conflict("Email này đã được sử dụng cho campus khác.");

// 3. Transaction
var campus = new Campus(...);
db.Campuses.Add(campus);
await db.SaveChangesAsync(); // only if project transaction pattern requires to get identity here

var department = new Department
{
    CampusId = campus.CampusId,
    Name = "Phòng Hợp tác Quốc tế",
    DepartmentType = "IC",
    HeadUserId = null,
    Status = "ACTIVE",
    CreatedBy = currentUserId
};
db.Departments.Add(department);

// Commit by TransactionBehaviour or explicit transaction depending on existing architecture.
```

Nếu project có `TransactionBehaviour`, không gọi `SaveChanges()` tùy tiện nhiều nơi; tuân theo pattern hiện có.

---

## 12. Frontend implementation notes

Modal title:

```text
Thêm mới campus
```

Fields:

```text
Mã code *
Tên campus *
Chọn thành phố *
Địa chỉ *
Số điện thoại *
Email *
```

Buttons:

```text
Hủy
Tạo mới
```

Inline validation:

```text
Vui lòng nhập mã campus.
Vui lòng nhập tên campus.
Vui lòng chọn thành phố.
Vui lòng nhập địa chỉ.
Vui lòng nhập số điện thoại.
Vui lòng nhập email.
Email không đúng định dạng.
Số điện thoại không đúng định dạng.
```

Nếu backend trả 409/422, giữ modal mở và giữ dữ liệu user đã nhập.

---

## 13. Alternative Flows

### AF-01 — Missing required field

Frontend hiển thị inline error và không gọi API.

### AF-02 — Duplicate campus code/name/address/phone/email

Backend trả `409 Conflict`, frontend hiển thị message tương ứng.

### AF-03 — Invalid email/phone

Backend trả `422 Unprocessable Entity`, frontend hiển thị lỗi field.

### AF-04 — IC department creation failed

Backend rollback transaction. Không được để campus đã tạo nhưng không có IC department.

### AF-05 — Unauthorized

Non-HO gọi API thì backend trả 403.

---

## 14. Business Rules

### BR-81-01 — Initial campus status is ACTIVE

Campus mới tạo mặc định `ACTIVE`.

### BR-81-02 — No IC Head during create

Không hiển thị field chọn trưởng phòng IC và không nhận `icHeadUserId` trong request create.

### BR-81-03 — Auto-create IC Department

Sau khi tạo campus, backend phải tạo phòng ban IC mặc định.

### BR-81-04 — Atomic transaction

Create campus và create IC department phải nằm trong cùng transaction.

### BR-81-05 — Only city can duplicate

Chỉ `city` được phép trùng. Không cho trùng `campus_code`, `name`, `address`, `phone`, `email`.

---

## 15. Verification Criteria

```text
Given HO opens Add New Campus modal
When the modal is displayed
Then it does not contain "Chọn trưởng phòng IC"
And it contains Mã code, Tên campus, Thành phố, Địa chỉ, Số điện thoại, Email.
```

```text
Given HO submits a valid new campus
When backend processes the request
Then a new campuses row is created
And a new departments row is created with department_type = IC
And departments.campus_id equals the new campus_id
And campus.status = ACTIVE
And campus.ic_head_user_id = NULL.
```

```text
Given campus_code HN already exists
When HO submits another campus with campusCode = HN
Then backend returns 409
And no new campus is created.
```

```text
Given city "Hà Nội" already exists
When HO creates another campus with city "Hà Nội" but different code/name/address/phone/email
Then creation succeeds.
```

```text
Given phone "02473005588" already exists
When HO creates another campus with phone "024 7300 5588"
Then backend detects duplicate after normalization
And returns 409.
```

---

## 16. Definition of Done

```text
[ ] Modal bỏ field chọn trưởng phòng IC.
[ ] Label "Chọn vị trí" đổi thành "Chọn thành phố".
[ ] Có đủ field required mới.
[ ] Backend validate required + format.
[ ] Backend chỉ cho trùng city.
[ ] Backend không cho trùng code/name/address/phone/email.
[ ] Campus và IC department tạo trong cùng transaction.
[ ] Rollback nếu tạo IC department fail.
[ ] Audit log được ghi.
[ ] Backend build pass.
[ ] Frontend build pass.
```
