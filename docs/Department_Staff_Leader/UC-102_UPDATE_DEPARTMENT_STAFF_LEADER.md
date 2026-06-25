# UC-102 — Update Department (Staff Leader)

> Module: Department Management  
> Actor chính: Staff Leader  
> Effective role bắt buộc: `role_code = STAFF`, `sub_role = LEADER`  
> Entry point: nút Edit trong modal UC-105 View Department Details  
> Phạm vi update: chỉ được chỉnh sửa `departments.name`  
> Permission model: Fixed role policy, không dùng `permissions` / `role_permissions`

---

## 1. Mục tiêu UC

UC này cho phép **Staff Leader chỉnh sửa tên phòng ban GENERAL thuộc campus của mình**.

UC này được tích hợp ngay trong modal detail phòng ban:

```text
Department List
→ bấm Eye / Xem chi tiết
→ mở Department Detail Modal
→ bấm Edit
→ modal chuyển sang edit mode
→ chỉ sửa Tên phòng ban
→ Lưu / Hủy
```

---

## 2. Phạm vi chỉnh sửa đã chốt

Chỉ cho sửa:

```text
Tên phòng ban / departments.name
```

Không cho sửa trong UC này:

```text
campus_id
campusName
head_user_id / trưởng phòng
department_type
status
description
created_at / created_by
updated_at / updated_by do frontend tự gửi
```

Backend tự set `updated_by` và `updated_at` khi có thay đổi thật sự.

---

## 3. Actor và scope

### 3.1 Actor

```text
Primary Actor: Staff Leader
Runtime role: role_code = STAFF, sub_role = LEADER
```

### 3.2 Scope dữ liệu

Staff Leader chỉ được update phòng ban thuộc campus của mình:

```sql
departments.campus_id = currentUser.primary_campus_id
```

Nếu gọi API update department thuộc campus khác:

```text
403 Forbidden
```

---

## 4. Rule bảo vệ phòng IC mặc định

Vì khi HO tạo campus, backend đã tạo sẵn phòng Hợp tác quốc tế / IC cho campus đó, Staff Leader **không tạo thêm IC** và cũng **không nên sửa tên IC mặc định** trong UC này.

Chốt rule:

```text
GENERAL department:
- Hiển thị nút Edit trong modal detail.
- Cho update tên nếu hợp lệ.

IC department:
- Không hiển thị nút Edit trong modal detail.
- Backend chặn update nếu bị gọi API trực tiếp.
```

Backend check:

```text
if department.department_type == "IC"
    return 409 Conflict
```

Message đề xuất:

```text
Không thể chỉnh sửa phòng Hợp tác quốc tế mặc định.
```

---

## 5. Entry point trên UI

UC này không có page riêng.

Entry point nằm trong modal UC-105:

```text
Department Detail Modal
Footer: [Đóng] [Edit]
```

Điều kiện hiển thị nút Edit:

```text
canEditName = true
```

Backend nên trả `canEditName` trong detail DTO. Frontend dùng field này để ẩn/hiện button, nhưng backend vẫn phải validate lại khi gọi API update.

---

## 6. UI edit mode

Khi bấm Edit, modal chuyển từ read-only mode sang edit mode.

### 6.1 Layout edit mode

```text
Header:
[Icon edit] Chỉnh sửa phòng ban                                      [X]

Body:
Tên phòng ban:      [Input: Phòng Công nghệ thông tin]
Cơ sở:              FPT University Hà Nội               (readonly)
Trưởng phòng:       Nguyễn Văn A / Chưa gán trưởng phòng (readonly)
Trạng thái:          Hoạt động / Ngừng hoạt động          (readonly)

Footer:
[Hủy]
[Lưu]
```

### 6.2 Field editable

Chỉ input `Tên phòng ban` được edit.

Các field còn lại là text readonly, không phải disabled input nếu không cần.

---

## 7. Backend API đề xuất

Nên dùng endpoint riêng để AI Agent không update nhầm các field khác:

```http
PATCH /api/departments/{departmentId}/name
```

Không nên dùng endpoint generic kiểu:

```http
PUT /api/departments/{departmentId}
```

vì dễ làm AI Agent hoặc frontend gửi nhầm `campusId`, `departmentType`, `headUserId`, `status`.

---

## 8. Request body

```json
{
  "name": "Phòng Truyền thông"
}
```

Không nhận các field sau:

```json
{
  "campusId": 1,
  "departmentType": "GENERAL",
  "headUserId": 10,
  "status": "ACTIVE",
  "description": "...",
  "updatedBy": 1,
  "updatedAt": "2026-06-25T10:00:00"
}
```

Nếu frontend gửi thừa field, backend nên bỏ qua hoặc reject theo API contract. Khuyến nghị reject bằng validation nếu contract strict.

---

## 9. Response DTO

### 9.1 Có thay đổi thật sự

```json
{
  "departmentId": 12,
  "name": "Phòng Truyền thông",
  "campusName": "FPT University Hà Nội",
  "headFullName": null,
  "status": "ACTIVE",
  "departmentType": "GENERAL",
  "updatedAt": "2026-06-25T10:30:00",
  "changed": true,
  "message": "Đã cập nhật tên phòng ban."
}
```

### 9.2 Không có thay đổi

Nếu tên sau trim giống tên hiện tại:

```json
{
  "departmentId": 12,
  "name": "Phòng Truyền thông",
  "changed": false,
  "message": "Không có thay đổi nào để cập nhật."
}
```

Không update DB, không đổi `updated_at`, không đổi `updated_by`, không ghi audit log.

---

## 10. Validation rules

### 10.1 Input validation

Viết ở FluentValidation:

| Field | Rule |
|---|---|
| `name` | Required sau khi trim |
| `name` | Tối đa 150 ký tự |

Message đề xuất:

```text
Tên phòng ban không được để trống.
Tên phòng ban không được vượt quá 150 ký tự.
```

### 10.2 Business validation

Viết trong Handler:

| Case | Response |
|---|---|
| User không phải Staff Leader | 403 |
| Department không tồn tại | 404 |
| Department ngoài campus của Staff Leader | 403 |
| Department là IC mặc định | 409 |
| Tên mới trùng department khác trong cùng campus | 409 |
| Tên mới giống tên hiện tại sau trim | 200, `changed = false` |

---

## 11. Duplicate name rule

Schema có unique key theo logic:

```text
uq_departments_campus_name (campus_id, name)
```

Vì vậy tên phòng ban không được trùng trong cùng campus.

Handler phải check trước:

```sql
SELECT 1
FROM departments
WHERE campus_id = @CurrentUserPrimaryCampusId
  AND department_id <> @DepartmentId
  AND LOWER(TRIM(name)) = LOWER(TRIM(@NewName))
LIMIT 1;
```

Nếu trùng:

```text
409 Conflict
Tên phòng ban đã tồn tại trong cơ sở này.
```

Không để lỗi raw từ database trả thẳng ra frontend.

---

## 12. Update logic chuẩn

Pseudo flow:

```text
1. Lấy currentUser.
2. Check currentUser là Staff Leader.
3. Trim request.name.
4. Validate name required và max length.
5. Load department theo departmentId.
6. Nếu không tồn tại -> 404.
7. Nếu department.campus_id != currentUser.primary_campus_id -> 403.
8. Nếu department.department_type = IC -> 409.
9. So sánh normalized current name và new name.
10. Nếu không đổi -> return changed = false.
11. Check duplicate name trong cùng campus, bỏ qua chính department hiện tại.
12. Nếu trùng -> 409.
13. Update departments.name = trimmedName.
14. Set updated_by = currentUser.user_id.
15. Set updated_at = NOW() hoặc để DB ON UPDATE CURRENT_TIMESTAMP nếu entity/config đã hỗ trợ.
16. Ghi audit log field-level: name old -> new.
17. Return response changed = true.
```

---

## 13. Audit log

Khi có thay đổi thật sự, ghi audit:

```text
entity_type: DEPARTMENT
entity_id: departmentId
action: UPDATE_DEPARTMENT_NAME
actor_user_id: currentUser.user_id
campus_id: currentUser.primary_campus_id
```

Field-level change:

```text
field_name: name
old_value: <old department name>
new_value: <new department name>
```

Không ghi audit nếu `changed = false`.

---

## 14. Main flow

```text
1. Staff Leader mở màn Department Management.
2. Staff Leader bấm Eye để mở Department Detail Modal.
3. Modal hiển thị detail theo UC-105.
4. Nếu department là GENERAL, modal hiển thị nút Edit.
5. Staff Leader bấm Edit.
6. Modal chuyển sang edit mode.
7. Staff Leader sửa Tên phòng ban.
8. Staff Leader bấm Lưu.
9. Frontend validate sơ bộ: name không rỗng, không quá 150 ký tự.
10. Frontend gọi PATCH /api/departments/{departmentId}/name.
11. Backend validate role, scope, department type, duplicate name.
12. Backend update name nếu có thay đổi thật sự.
13. Backend trả response.
14. Frontend reload detail modal hoặc cập nhật local detail state.
15. Frontend refresh row tương ứng trong Department List.
16. Modal quay lại view mode.
17. Hiển thị toast: "Đã cập nhật tên phòng ban."
```

---

## 15. Alternative flows

### AF-01 — Tên phòng ban rỗng

```text
Điều kiện:
User xóa hết tên hoặc chỉ nhập khoảng trắng.

Frontend:
Hiển thị inline error: "Tên phòng ban không được để trống."
Không gọi API nếu validate được ở frontend.

Backend:
Nếu vẫn gọi API, trả 422 Validation Error.
```

### AF-02 — Tên vượt quá 150 ký tự

```text
Frontend:
Hiển thị inline error: "Tên phòng ban không được vượt quá 150 ký tự."

Backend:
422 Validation Error.
```

### AF-03 — Trùng tên trong cùng campus

```text
Backend:
409 Conflict
Message: "Tên phòng ban đã tồn tại trong cơ sở này."

Frontend:
Giữ modal edit mode.
Giữ dữ liệu user đã nhập.
Hiển thị lỗi dưới input tên phòng ban.
```

### AF-04 — Department thuộc campus khác

```text
Backend:
403 Forbidden

Frontend:
Hiển thị: "Bạn không có quyền cập nhật phòng ban này."
Có thể đóng modal hoặc giữ modal với error state.
```

### AF-05 — Department không tồn tại

```text
Backend:
404 Not Found

Frontend:
Hiển thị: "Không tìm thấy phòng ban."
Đóng modal và refresh list nếu cần.
```

### AF-06 — User bấm Hủy

```text
Frontend:
Không gọi API.
Discard dữ liệu đang nhập.
Modal quay lại view mode.
```

### AF-07 — Không thay đổi tên

```text
Điều kiện:
Tên sau trim giống tên hiện tại.

Backend:
Không update DB.
Không đổi updated_at / updated_by.
Không ghi audit.
Return changed = false.

Frontend:
Có thể quay lại view mode và hiển thị message nhẹ: "Không có thay đổi nào để cập nhật."
```

### AF-08 — IC department bị gọi API update

```text
Backend:
409 Conflict
Message: "Không thể chỉnh sửa phòng Hợp tác quốc tế mặc định."

Frontend:
Thông thường không xảy ra vì IC không có nút Edit.
Nếu xảy ra, hiển thị lỗi và reload detail.
```

### AF-09 — Token hết hạn

```text
Backend:
401 Unauthorized

Frontend:
Hiển thị: "Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại."
Redirect về login theo flow hiện tại.
```

---

## 16. Frontend integration

### 16.1 State gợi ý

```ts
const [isEditMode, setIsEditMode] = useState(false);
const [editName, setEditName] = useState('');
const [editError, setEditError] = useState<string | null>(null);
const [saving, setSaving] = useState(false);
```

### 16.2 Khi bấm Edit

```text
setEditName(departmentDetail.name)
setEditError(null)
setIsEditMode(true)
```

### 16.3 Khi bấm Hủy

```text
setEditName(departmentDetail.name)
setEditError(null)
setIsEditMode(false)
```

### 16.4 Khi lưu thành công

```text
- Update departmentDetail.name
- setIsEditMode(false)
- Refresh row trong list hoặc gọi lại list API
- Toast success nếu changed = true
- Toast info nhẹ nếu changed = false
```

---

## 17. Manual test checklist

```text
[ ] GENERAL department thuộc campus mình hiển thị nút Edit trong detail modal.
[ ] IC department không hiển thị nút Edit.
[ ] Staff Leader sửa tên GENERAL department thành công.
[ ] Sau khi sửa, modal detail cập nhật tên mới.
[ ] Sau khi sửa, row trong department list cập nhật tên mới.
[ ] Tên rỗng bị chặn.
[ ] Tên quá 150 ký tự bị chặn.
[ ] Tên trùng trong cùng campus trả 409 và modal giữ edit mode.
[ ] Tên giống hiện tại không update updated_at / updated_by.
[ ] Staff Leader không update được department campus khác.
[ ] User không phải Staff Leader bị chặn.
[ ] Direct API update IC department bị chặn 409.
[ ] Không có field campus/type/head/status bị update trong request này.
[ ] Backend build pass.
[ ] Frontend build pass nếu sửa UI/API service.
```

---

## 18. Không được làm

```text
Không cho sửa department_type.
Không cho sửa campus_id.
Không cho sửa head_user_id.
Không cho sửa status trong UC này.
Không thêm description nếu schema chưa có.
Không hiện filter/cột Loại phòng ban ở list vì đã chốt bỏ.
Không cho Staff Leader sửa tên IC department mặc định.
Không dùng role_code STAFF_LEADER.
Không dùng permission/role_permissions table.
Không dùng mock data.
Không query DbContext trong Controller.
Không báo hoàn thành nếu backend/frontend build lỗi.
```
