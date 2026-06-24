# UC-85 — Update Campus for HO

> File này đặc tả riêng chức năng **HO chỉnh sửa thông tin master data của campus**.

---

## 1. Thông tin UC

| Field | Value |
|---|---|
| UC ID | UC-85 |
| UC Name | Update Campus |
| Type | UI |
| Primary Actor | HO |
| Module | Campus Management |
| Route | `/dashboard/campus/:campusId` |
| API | `PUT /api/campuses/{campusId}` |

---

## 2. Mục tiêu chức năng

HO chỉnh sửa thông tin campus.

UC này chỉ chỉnh **master data** của campus:

```text
Mã code
Tên campus
Thành phố
Địa chỉ
Số điện thoại
Email
```

UC này **không đổi status**. Status đổi bằng UC-86 Manage Campus Status.

UC này **không gộp đổi trưởng phòng IC**. Việc gán trưởng phòng IC nên tách sang UC-87 Assign Campus Lead hoặc luồng account/department riêng.

---

## 3. Editable fields

```text
Mã code *
Tên campus *
Thành phố *
Địa chỉ *
Số điện thoại *
Email *
```

Không edit trong UC này:

```text
status
ic_head_user_id
created_at
created_by
IC department mặc định
```

---

## 4. Preconditions

```text
HO đã đăng nhập thành công.
HO account ACTIVE.
Campus tồn tại.
HO đang ở Campus Detail page.
```

---

## 5. Postconditions

### Success

```text
Campus được update trong bảng campuses.
updated_at được cập nhật.
updated_by = current HO user_id.
Audit log được ghi.
UI quay về view mode hoặc reload detail với dữ liệu mới.
```

### Failure

```text
Nếu validate lỗi: không update DB.
Nếu duplicate: backend trả 409.
Nếu campus không tồn tại: backend trả 404.
Nếu non-HO: backend trả 403.
```

---

## 6. Request DTO

```ts
export type UpdateCampusRequest = {
  campusCode: string;
  name: string;
  city: string;
  address: string;
  phone: string;
  email: string;
};
```

---

## 7. Response DTO

```ts
export type UpdateCampusResponse = {
  campusId: number;
  campusCode: string;
  name: string;
  city: string;
  address: string;
  phone: string;
  email: string;
  status: 'ACTIVE' | 'INACTIVE';
  updatedAt: string;
  updatedBy: number;
};
```

---

## 8. Main Flow

```text
[U] Step 1. HO đang ở màn Campus Detail.

[U] Step 2. HO click icon edit.

[S] Step 3. Frontend chuyển sang edit mode hoặc mở edit form/modal.

[S] Step 4. Form pre-fill dữ liệu hiện tại:
- Mã code
- Tên campus
- Thành phố
- Địa chỉ
- Số điện thoại
- Email

[U] Step 5. HO chỉnh sửa một hoặc nhiều field.

[U] Step 6. HO click "Lưu thay đổi".

[S] Step 7. Frontend validate required + format cơ bản.

[S] Step 8. Frontend gọi PUT /api/campuses/{campusId}.

[S] Step 9. Backend kiểm tra current user là HO.

[S] Step 10. Backend kiểm tra campus tồn tại.

[S] Step 11. Backend normalize dữ liệu.

[S] Step 12. Backend check duplicate, loại trừ chính campus hiện tại:
- campus_code không trùng campus khác
- name không trùng campus khác
- address không trùng campus khác
- phone không trùng campus khác
- email không trùng campus khác
- city được phép trùng

[S] Step 13. Backend update campuses:
- campus_code
- name
- city
- address
- phone
- email
- updated_by
- updated_at

[S] Step 14. Backend ghi audit log.

[S] Step 15. Frontend quay về view mode và hiển thị dữ liệu mới.
```

---

## 9. Validation rules

| Field | Required | Rule |
|---|---:|---|
| `campusCode` | Yes | Trim, uppercase, max 20, unique excluding current campus. |
| `name` | Yes | Trim, max 150, unique excluding current campus. |
| `city` | Yes | Trim, max 100, allowed duplicate. |
| `address` | Yes | Trim, max 255, unique excluding current campus. |
| `phone` | Yes | Trim, max 30, valid phone, unique after normalize excluding current campus. |
| `email` | Yes | Trim, lowercase, max 150, valid email, unique excluding current campus. |

---

## 10. Duplicate rules for update

Chỉ cho trùng `city`.

Không cho trùng với campus khác:

```text
campus_code
name
address
phone
email
```

Duplicate query phải loại trừ current campus:

```sql
WHERE campus_id <> @currentCampusId
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
PEMS.Application/Campuses/Commands/UpdateCampus/
├── UpdateCampusCommand.cs
├── UpdateCampusCommandHandler.cs
├── UpdateCampusCommandValidator.cs
└── UpdateCampusResponse.cs
```

Pseudocode duplicate:

```csharp
if (await CampusCodeExists(code, excludeCampusId: campusId))
    throw new ConflictException("Mã campus đã tồn tại.");

if (await CampusNameExists(name, excludeCampusId: campusId))
    throw new ConflictException("Tên campus đã tồn tại.");

if (await CampusAddressExists(address, excludeCampusId: campusId))
    throw new ConflictException("Địa chỉ này đã được sử dụng cho campus khác.");

if (await CampusPhoneExists(normalizedPhone, excludeCampusId: campusId))
    throw new ConflictException("Số điện thoại này đã được sử dụng cho campus khác.");

if (await CampusEmailExists(email, excludeCampusId: campusId))
    throw new ConflictException("Email này đã được sử dụng cho campus khác.");
```

Không update:

```text
status
ic_head_user_id
created_at
created_by
```

---

## 12. Frontend implementation notes

Edit form có thể nằm ngay trong detail page hoặc modal.

Fields pre-fill từ `CampusDetailDto`:

```text
campusCode
name
city
address
phone
email
```

Buttons:

```text
Hủy
Lưu thay đổi
```

Cancel rule:

```text
Nếu user chưa sửa gì: thoát edit mode ngay.
Nếu user đã sửa dữ liệu: hỏi xác nhận trước khi hủy.
Nếu chọn Không: giữ form và dữ liệu đã nhập.
Nếu chọn Có: thoát edit mode và bỏ thay đổi.
```

---

## 13. Alternative Flows

### AF-01 — No changes

```text
Nếu HO click Lưu thay đổi nhưng dữ liệu không đổi:
Frontend có thể hiển thị "Không có thay đổi nào để lưu."
Không cần gọi API.
```

### AF-02 — Duplicate data

```text
Backend trả 409.
Frontend giữ form mở và hiển thị lỗi tương ứng.
```

### AF-03 — Invalid input

```text
Backend trả 422.
Frontend hiển thị lỗi field và giữ dữ liệu đang nhập.
```

### AF-04 — Campus not found

```text
Backend trả 404.
Frontend hiển thị "Không tìm thấy campus."
```

### AF-05 — Unauthorized

```text
Non-HO gọi API thì backend trả 403.
```

---

## 14. Business Rules

### BR-85-01 — Required fields

Không cho lưu nếu thiếu code, name, city, address, phone, email.

### BR-85-02 — Preserve status

UC-85 không đổi `campuses.status`.

### BR-85-03 — Preserve IC Head

UC-85 không đổi `campuses.ic_head_user_id`.

### BR-85-04 — Preserve IC department

Update campus không được xóa hoặc đổi phòng ban IC mặc định.

### BR-85-05 — Only city can duplicate

Chỉ `city` được phép trùng khi update. Các field code/name/address/phone/email không được trùng với campus khác.

### BR-85-06 — Audit required

Mọi update phải ghi `updated_by`, `updated_at` và audit log.

---

## 15. Verification Criteria

```text
Given HO is viewing campus detail
When HO clicks edit
Then form is pre-filled with campusCode, name, city, address, phone and email.
```

```text
Given HO changes phone and email to valid unique values
When HO clicks Save
Then campuses.phone and campuses.email are updated
And updated_by equals current HO user_id
And updated_at is refreshed.
```

```text
Given another campus already has email "hn@fpt.edu.vn"
When HO updates current campus email to "HN@FPT.EDU.VN"
Then backend lowercases and detects duplicate
And returns 409.
```

```text
Given another campus has city "Hà Nội"
When HO updates current campus city to "Hà Nội" with unique code/name/address/phone/email
Then update succeeds.
```

```text
Given another campus has phone "02473005588"
When HO updates current campus phone to "024 7300 5588"
Then backend detects duplicate after phone normalization
And returns 409.
```

---

## 16. Definition of Done

```text
[ ] Edit form pre-fill đúng dữ liệu hiện tại.
[ ] Update chỉ gửi master data fields.
[ ] Backend không update status/ic_head_user_id trong UC này.
[ ] Backend chỉ cho trùng city.
[ ] Backend không cho trùng code/name/address/phone/email với campus khác.
[ ] Update duplicate check loại trừ current campus.
[ ] updated_by và updated_at được ghi.
[ ] Audit log được ghi.
[ ] Backend build pass.
[ ] Frontend build pass.
```
