# UC-86 — Manage Campus Status for HO

> File này đặc tả riêng chức năng **HO bật/tắt trạng thái hoạt động của campus** bằng toggle.

---

## 1. Thông tin UC

| Field | Value |
|---|---|
| UC ID | UC-86 |
| UC Name | Manage Campus Status |
| Type | UI |
| Primary Actor | HO |
| Module | Campus Management |
| Route | `/dashboard/campus` hoặc `/dashboard/campus/:campusId` |
| API | `PATCH /api/campuses/{campusId}/status` |

---

## 2. Mục tiêu chức năng

HO có thể chuyển campus giữa hai trạng thái:

```text
ACTIVE → INACTIVE
INACTIVE → ACTIVE
```

Mapping UI:

| DB status | UI label | Toggle |
|---|---|---|
| `ACTIVE` | Hoạt động | ON |
| `INACTIVE` | Ngừng hoạt động | OFF |

---

## 3. Preconditions

```text
HO đã đăng nhập thành công.
HO account ACTIVE.
Campus tồn tại.
HO đang ở danh sách hoặc chi tiết campus.
```

---

## 4. Postconditions

### Success

```text
campuses.status được cập nhật.
updated_by = current HO user_id.
updated_at được cập nhật.
Badge và toggle trên UI đổi đúng trạng thái.
Audit log được ghi.
```

### Failure

```text
Nếu campus không tồn tại: backend trả 404.
Nếu non-HO: backend trả 403.
Nếu deactivation bị dependency chặn: backend trả 409.
Nếu activate thiếu required master data: backend trả 422 hoặc 409 tùy convention.
```

---

## 5. Request DTO

```ts
export type ManageCampusStatusRequest = {
  status: 'ACTIVE' | 'INACTIVE';
};
```

---

## 6. Response DTO

```ts
export type ManageCampusStatusResponse = {
  campusId: number;
  status: 'ACTIVE' | 'INACTIVE';
  updatedAt: string;
  updatedBy: number;
};
```

---

## 7. Main Flow — Disable campus

```text
[U] Step 1. HO click toggle ON của campus ACTIVE.

[S] Step 2. Frontend hiển thị confirmation:
"Bạn có chắc muốn ngừng hoạt động campus này? Campus sẽ không còn xuất hiện trong các lựa chọn đăng ký/phân công mới."

[U] Step 3. HO xác nhận.

[S] Step 4. Frontend gọi PATCH /api/campuses/{campusId}/status với body { status: 'INACTIVE' }.

[S] Step 5. Backend kiểm tra current user là HO.

[S] Step 6. Backend kiểm tra campus tồn tại và đang ACTIVE.

[S] Step 7. Backend kiểm tra dependency nếu project đang có rule chặn disable.

[S] Step 8. Nếu hợp lệ, backend update:
- campuses.status = 'INACTIVE'
- campuses.updated_by = current HO user_id
- campuses.updated_at = now

[S] Step 9. Backend ghi audit log.

[S] Step 10. Frontend cập nhật badge thành "Ngừng hoạt động" và toggle OFF.
```

---

## 8. Main Flow — Enable campus

```text
[U] Step 1. HO click toggle OFF của campus INACTIVE.

[S] Step 2. Frontend gọi PATCH /api/campuses/{campusId}/status với body { status: 'ACTIVE' }.

[S] Step 3. Backend kiểm tra current user là HO.

[S] Step 4. Backend kiểm tra campus tồn tại và đang INACTIVE.

[S] Step 5. Backend kiểm tra required master data:
- campus_code có giá trị
- name có giá trị
- city có giá trị
- address có giá trị
- phone có giá trị
- email hợp lệ
- có phòng ban IC ACTIVE thuộc campus này

[S] Step 6. Nếu hợp lệ, backend update:
- campuses.status = 'ACTIVE'
- campuses.updated_by = current HO user_id
- campuses.updated_at = now

[S] Step 7. Backend ghi audit log.

[S] Step 8. Frontend cập nhật badge thành "Hoạt động" và toggle ON.
```

---

## 9. Dependency check khi disable

Nếu project muốn strict theo nghiệp vụ, backend phải block disable khi có dependency active.

Check tối thiểu:

```text
Có visit_request_campuses chưa CLOSED/CANCELLED không?
Có visit_logistics_items chưa hoàn tất/hủy không?
Có users ACTIVE đang thuộc campus này không?
Có departments ACTIVE quan trọng thuộc campus này không?
```

Nếu chặn:

```text
HTTP 409 Conflict
Message: Không thể ngừng hoạt động campus vì còn dữ liệu/phụ thuộc đang hoạt động.
Response nên trả dependency counts nếu có.
```

Nếu phase hiện tại muốn đơn giản hơn:

```text
Cho disable campus.
Nhưng campus INACTIVE phải bị ẩn khỏi các dropdown tạo mới visit/account/assignment.
Không xóa dữ liệu cũ.
```

Nên chọn một trong hai phương án và thống nhất với code hiện tại.

---

## 10. Backend implementation notes

Application structure gợi ý:

```text
PEMS.Application/Campuses/Commands/ManageCampusStatus/
├── ManageCampusStatusCommand.cs
├── ManageCampusStatusCommandHandler.cs
├── ManageCampusStatusCommandValidator.cs
└── ManageCampusStatusResponse.cs
```

Validator:

```text
campusId > 0
status must be ACTIVE or INACTIVE
```

Handler:

```text
Check HO.
Find campus by id.
If not found: 404.
If requested status == current status: return success no-op or message "Campus đã ở trạng thái này".
If disabling: check dependencies if strict mode.
If enabling: check required master data and active IC department.
Update status, updated_by, updated_at.
Audit log.
```

---

## 11. Frontend implementation notes

Danh sách campus:

```text
Toggle ON nếu status ACTIVE.
Toggle OFF nếu status INACTIVE.
Badge xanh: Hoạt động.
Badge xám: Ngừng hoạt động.
```

Khi disable:

```text
Nên có confirm modal vì ảnh hưởng đến dropdown tạo mới.
```

Khi enable:

```text
Có thể không cần confirm, nhưng phải xử lý lỗi nếu backend báo thiếu master data hoặc thiếu IC department.
```

Optimistic update:

```text
Không nên đổi UI ngay nếu chưa có API success.
Nếu dùng optimistic update thì phải rollback khi API fail.
```

---

## 12. Business Rules

### BR-86-01 — Toggle maps to campus status

Toggle ON = `ACTIVE`, toggle OFF = `INACTIVE`.

### BR-86-02 — No hard delete

Không xóa campus. Chỉ đổi `status`.

### BR-86-03 — Inactive campus remains visible in HO management

Campus INACTIVE vẫn hiển thị trong danh sách quản lý của HO.

### BR-86-04 — Inactive campus hidden from new business flows

Campus INACTIVE không xuất hiện trong form đăng ký mới, dropdown phân công mới, tạo account/cấu hình mới nếu nghiệp vụ yêu cầu active campus.

### BR-86-05 — Existing data preserved

Delegation, document, report, audit log cũ vẫn giữ liên kết với campus inactive.

### BR-86-06 — Activation requires valid master data

Không cho bật lại ACTIVE nếu campus thiếu required master data hoặc thiếu IC department ACTIVE.

---

## 13. Alternative Flows

### AF-01 — User cancels disable confirmation

```text
HO click toggle OFF nhưng chọn Hủy ở confirm modal.
Không gọi API.
Toggle giữ nguyên ON.
```

### AF-02 — Dependency blocks disable

```text
Backend trả 409.
Frontend hiển thị message lỗi.
Toggle giữ nguyên ON.
```

### AF-03 — Activation validation failed

```text
Backend trả 422/409.
Frontend hiển thị lỗi.
Toggle giữ nguyên OFF.
```

### AF-04 — Campus not found

```text
Backend trả 404.
Frontend reload list hoặc hiển thị not found.
```

### AF-05 — Unauthorized

```text
Non-HO gọi API thì backend trả 403.
```

---

## 14. Verification Criteria

```text
Given campus HN is ACTIVE
When HO toggles it OFF and confirms
Then campuses.status becomes INACTIVE
And UI badge changes to "Ngừng hoạt động"
And toggle is OFF.
```

```text
Given campus HN is INACTIVE and has valid master data + active IC department
When HO toggles it ON
Then campuses.status becomes ACTIVE
And UI badge changes to "Hoạt động"
And toggle is ON.
```

```text
Given campus HN is ACTIVE and has active dependencies
When HO attempts to disable it
Then backend returns 409
And campus remains ACTIVE.
```

```text
Given campus HN is INACTIVE but missing email
When HO attempts to enable it
Then backend rejects the request
And campus remains INACTIVE.
```

---

## 15. Definition of Done

```text
[ ] Toggle status calls PATCH API.
[ ] Toggle ON/OFF maps exactly to ACTIVE/INACTIVE.
[ ] Disable confirmation exists.
[ ] Backend updates status, updated_by, updated_at.
[ ] Backend validates activation master data.
[ ] Backend handles dependency blocking or explicitly documents simplified mode.
[ ] UI rollback/keeps state if API fails.
[ ] Non-HO bị chặn 403.
[ ] Backend build pass.
[ ] Frontend build pass.
```
