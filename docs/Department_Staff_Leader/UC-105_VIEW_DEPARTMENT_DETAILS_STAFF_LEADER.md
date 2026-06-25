# UC-105 — View Department Details (Staff Leader)

> Module: Department Management  
> Actor chính: Staff Leader  
> Effective role bắt buộc: `role_code = STAFF`, `sub_role = LEADER`  
> Schema source: SQL v10 — `departments`, `campuses`, `users`  
> Permission model: Fixed role policy, không dùng `permissions` / `role_permissions`

---

## 1. Mục tiêu UC

UC này cho phép **Staff Leader xem chi tiết tổng quát của một phòng ban thuộc campus của mình** thông qua modal mở từ màn hình danh sách phòng ban.

Thông tin detail chỉ hiển thị các trường tổng quát:

```text
Tên phòng ban
Cơ sở
Trưởng phòng
Trạng thái
```

Không hiển thị và không xử lý trong UC này:

```text
Loại phòng ban / department_type
Danh sách nhân sự
Danh sách task / delegation
Mô tả phòng ban
Lịch sử thay đổi
Nút disable / enable
Form gán trưởng phòng
```

`department_type` vẫn có thể nằm trong response DTO để frontend xác định rule ẩn/hiện nút Edit hoặc toggle, nhưng không hiển thị thành field trong modal.

---

## 2. Actor và scope

### 2.1 Actor

```text
Primary Actor: Staff Leader
Runtime role: role_code = STAFF, sub_role = LEADER
```

Không dùng các giá trị legacy sau trong backend/frontend runtime:

```text
STAFF_LEADER
STAFF_L
IC_STAFF_LEADER
DEPT
DEPARTMENT_LEADER
```

### 2.2 Scope dữ liệu

Staff Leader chỉ được xem phòng ban thuộc campus của mình:

```sql
departments.campus_id = currentUser.primary_campus_id
```

Backend không được tin `campusId` từ frontend để xác định scope. Nếu Staff Leader gọi trực tiếp API với `departmentId` thuộc campus khác, backend phải trả `403 Forbidden`.

---

## 3. Entry point trên UI

UC này được mở từ màn hình Department List.

Trong cột **Hành động** của mỗi dòng phòng ban, hiển thị icon:

```text
Eye / Xem chi tiết
```

Áp dụng cho cả:

```text
IC department
GENERAL department
ACTIVE department
INACTIVE department
```

Tức là phòng Hợp tác quốc tế mặc định (`department_type = IC`) vẫn xem detail được, chỉ không được toggle status và không nên cho Staff Leader edit tên.

---

## 4. Modal View Detail

### 4.1 Layout đề xuất

```text
Header:
[Icon phòng ban] Chi tiết phòng ban                                      [X]

Body:
Tên phòng ban:      Phòng Công nghệ thông tin
Cơ sở:              FPT University Hà Nội
Trưởng phòng:       Nguyễn Văn A
                    hoặc "Chưa gán trưởng phòng"
Trạng thái:          Hoạt động / Ngừng hoạt động

Footer:
[Đóng]
[Edit] / [Chỉnh sửa]  chỉ hiện khi canEditName = true
```

### 4.2 Hiển thị trạng thái

Mapping trạng thái:

```text
ACTIVE   -> Hoạt động
INACTIVE -> Ngừng hoạt động
```

Nên hiển thị bằng badge:

```text
ACTIVE   -> badge xanh / success
INACTIVE -> badge xám / neutral
```

### 4.3 Trưởng phòng chưa gán

Nếu `head_user_id = NULL` hoặc join không ra user hợp lệ:

```text
Trưởng phòng: Chưa gán trưởng phòng
```

Không coi đây là lỗi.

---

## 5. Backend API đề xuất

### 5.1 Endpoint

```http
GET /api/departments/{departmentId}
```

Controller chỉ nhận route, gọi MediatR Query và trả response. Không query DbContext trực tiếp trong Controller.

### 5.2 Query / Handler đề xuất

Tên gợi ý:

```text
PEMS.Application/Departments/Queries/ViewDepartmentDetails/
- ViewDepartmentDetailsQuery.cs
- ViewDepartmentDetailsQueryHandler.cs
- ViewDepartmentDetailsResponse.cs
```

### 5.3 SQL logic tham khảo

```sql
SELECT
    d.department_id,
    d.name,
    d.department_type,
    d.status,
    d.head_user_id,
    c.campus_id,
    c.campus_code,
    c.name AS campus_name,
    head.full_name AS head_full_name
FROM departments d
JOIN campuses c
    ON c.campus_id = d.campus_id
LEFT JOIN users head
    ON head.user_id = d.head_user_id
WHERE d.department_id = @DepartmentId
  AND d.campus_id = @CurrentUserPrimaryCampusId;
```

Nếu không tìm thấy record sau khi áp scope:

- Nếu department thật sự không tồn tại: `404 Not Found`.
- Nếu department tồn tại nhưng khác campus: `403 Forbidden`.

Để phân biệt 404/403, handler có thể query tồn tại theo `department_id` trước, sau đó check campus. Nếu không cần lộ tồn tại, có thể trả chung 404; tuy nhiên trong module nội bộ, trả 403 rõ ràng sẽ dễ debug hơn.

---

## 6. Response DTO

```json
{
  "departmentId": 12,
  "name": "Phòng Công nghệ thông tin",
  "campusId": 1,
  "campusCode": "HN",
  "campusName": "FPT University Hà Nội",
  "headUserId": null,
  "headFullName": null,
  "status": "ACTIVE",
  "departmentType": "GENERAL",
  "canEditName": true,
  "canToggleStatus": true
}
```

### 6.1 Ý nghĩa các field permission UI

```text
canEditName:
- true  nếu department_type = GENERAL và department thuộc campus của Staff Leader
- false nếu department_type = IC

canToggleStatus:
- true  nếu department_type = GENERAL và không bị rule khác chặn ở UI
- false nếu department_type = IC
```

Lưu ý: `canEditName` và `canToggleStatus` chỉ để frontend ẩn/hiện button. Backend vẫn phải kiểm tra lại khi API update/toggle được gọi trực tiếp.

---

## 7. Business rules

| Mã | Rule |
|---|---|
| BR-105-01 | Chỉ Staff Leader (`STAFF` + `LEADER`) được xem detail phòng ban trong scope UC này. |
| BR-105-02 | Staff Leader chỉ xem được department có `campus_id = currentUser.primary_campus_id`. |
| BR-105-03 | Không hiển thị field `department_type` / Loại phòng ban trong modal detail. |
| BR-105-04 | Nếu `head_user_id = NULL`, hiển thị “Chưa gán trưởng phòng”. |
| BR-105-05 | IC department vẫn xem được detail. |
| BR-105-06 | IC department không hiển thị toggle status. |
| BR-105-07 | IC department không nên hiển thị nút Edit cho Staff Leader. |
| BR-105-08 | Modal detail là read-only, trừ khi user bấm Edit để chuyển sang UC-102 Update Department. |
| BR-105-09 | Frontend không được dùng detail modal để update `campus_id`, `department_type`, `head_user_id` hoặc `status`. |

---

## 8. Main flow

```text
1. Staff Leader mở màn Department Management.
2. Staff Leader bấm icon Eye / Xem chi tiết tại một dòng phòng ban.
3. Frontend gọi GET /api/departments/{departmentId}.
4. Backend xác thực user đã đăng nhập.
5. Backend kiểm tra effective role là Staff Leader.
6. Backend load department theo departmentId.
7. Backend kiểm tra department thuộc currentUser.primary_campus_id.
8. Backend join campus và head user.
9. Backend trả Department Detail DTO.
10. Frontend mở modal chi tiết.
11. Modal hiển thị tên phòng ban, cơ sở, trưởng phòng và trạng thái.
12. Nếu canEditName = true, modal hiển thị nút Edit.
13. User bấm Đóng hoặc X để đóng modal.
```

---

## 9. Alternative flows

### AF-01 — Department không tồn tại

```text
Điều kiện:
departmentId không tồn tại trong DB.

Backend:
404 Not Found

Frontend:
Hiển thị message: "Không tìm thấy phòng ban."
Không mở modal hoặc mở modal error state.
```

### AF-02 — Department thuộc campus khác

```text
Điều kiện:
department tồn tại nhưng d.campus_id != currentUser.primary_campus_id.

Backend:
403 Forbidden

Frontend:
Hiển thị message: "Bạn không có quyền xem phòng ban này."
```

### AF-03 — Trưởng phòng chưa gán

```text
Điều kiện:
d.head_user_id = NULL.

Frontend:
Modal vẫn mở bình thường.
Field Trưởng phòng hiển thị: "Chưa gán trưởng phòng".
```

### AF-04 — Lỗi server hoặc mất kết nối

```text
Backend:
500 hoặc network error.

Frontend:
Hiển thị error state: "Không thể tải chi tiết phòng ban. Vui lòng thử lại."
Cho phép bấm Thử lại hoặc Đóng.
```

---

## 10. Frontend integration

### 10.1 Action trên list

Trong cột Hành động của Department List thêm icon:

```text
Eye / Xem chi tiết
```

Không phá các action cũ:

```text
Toggle status chỉ dành cho GENERAL department.
Add New Department vẫn auto GENERAL.
Search/filter không có department type.
```

### 10.2 State gợi ý

```ts
const [selectedDepartmentId, setSelectedDepartmentId] = useState<number | null>(null);
const [detailModalOpen, setDetailModalOpen] = useState(false);
const [departmentDetail, setDepartmentDetail] = useState<DepartmentDetailDto | null>(null);
const [detailLoading, setDetailLoading] = useState(false);
const [detailError, setDetailError] = useState<string | null>(null);
const [isEditMode, setIsEditMode] = useState(false);
```

### 10.3 Khi bấm Edit

Nếu `departmentDetail.canEditName = true`, bấm Edit sẽ chuyển modal sang edit mode của UC-102.

Không mở page mới.

---

## 11. Manual test checklist

```text
[ ] Staff Leader xem detail department GENERAL thuộc campus mình thành công.
[ ] Staff Leader xem detail IC department thuộc campus mình thành công.
[ ] IC department không hiển thị nút Edit nếu chốt rule bảo vệ IC.
[ ] IC department không hiển thị toggle status.
[ ] Department có head_user_id NULL hiển thị "Chưa gán trưởng phòng".
[ ] Staff Leader không xem được department thuộc campus khác bằng direct URL/API.
[ ] User không phải Staff Leader bị chặn 403.
[ ] Modal đóng/mở không làm mất state filter/list hiện tại.
[ ] Loading state hiển thị khi đang gọi API.
[ ] Error state hiển thị khi API lỗi.
```

---

## 12. Không được làm

```text
Không thêm field description vào modal nếu DB chưa có cột description.
Không hiển thị department_type thành field "Loại phòng ban".
Không cho chọn/sửa campus trong modal.
Không cho gán trưởng phòng trong UC này.
Không cho đổi status trong UC này.
Không query DB trong Controller.
Không dùng mock data.
Không dùng role_code STAFF_LEADER.
Không dùng dynamic permissions table.
```
