# PROMPT NGẮN — CHỨC NĂNG QUẢN LÝ PHÒNG BAN / NHÂN SỰ DEPARTMENT

## 0. Bối cảnh

Tôi đang code chức năng **Quản lý phòng ban / Danh sách nhân sự** cho role **Department Leader** trong PEMS.

UI đã có sẵn:

```text
- Danh sách nhân sự
- Search nhân sự
- Modal Thêm nhân sự
- Modal xem/sửa chi tiết nhân sự
- Modal đổi trưởng phòng
- Icon gỡ nhân sự nếu đang có
```

Yêu cầu: **nối chức năng thật với database**, không dùng mock, không rewrite UI.

Stack:

```text
Frontend: React + TypeScript + Tailwind CSS
Backend: .NET 8 Clean Architecture + MediatR + EF Core
Database: MySQL v8.4 refined v6 no dynamic permissions
```

---

## 1. Database đã phù hợp

Không cần đổi schema.

Dùng các bảng:

```text
users
roles
departments
campuses
sent_emails
sent_email_recipients
```

Mapping:

```text
users.full_name          -> Họ và tên
users.email              -> Email
users.phone              -> SĐT
users.gender             -> Giới tính
users.role_id            -> role DEPARTMENT
users.sub_role           -> STAFF / LEADER
users.primary_campus_id  -> Cơ sở
users.department_id      -> Phòng ban
users.status             -> ACTIVE / INACTIVE / LOCKED
departments.head_user_id -> Trưởng phòng hiện tại
sent_emails              -> log email đã gửi
sent_email_recipients    -> người nhận email
```

Lưu ý quan trọng:

```text
- role_code phải là DEPARTMENT.
- Khi thêm nhân sự mới: sub_role = STAFF.
- Không query permissions/role_permissions vì DB mới đã bỏ dynamic permission.
- Authorization dùng fixed role policy + currentUser + department scope.
```

---

## 2. Danh sách nhân sự

Lấy data thật từ DB.

API gợi ý:

```text
GET /api/departments/{departmentId}/personnel?keyword=&page=&pageSize=
```

Điều kiện:

```text
users.department_id = departmentId
users.status = ACTIVE
role_code = DEPARTMENT
```

Department Leader chỉ được xem đúng phòng ban của mình.

Frontend cần:

```text
- Load list từ API thật.
- Search theo full_name/email/phone.
- Pagination nếu UI đang có.
- Loading/empty/error state.
```

---

## 3. Thêm nhân sự

Khi bấm **Tạo** trong modal thêm nhân sự:

### 3.1. Validate UI

```text
Họ và tên: bắt buộc, trim, tối đa 150 ký tự.
Email: bắt buộc, đúng format, tối đa 150 ký tự.
SĐT: bắt buộc, đúng format, tối đa 30 ký tự.
Giới tính: bắt buộc, MALE/FEMALE/OTHER/UNKNOWN.
Phòng ban: readonly, lấy từ department hiện tại.
Chức vụ: mặc định Nhân viên.
```

Nếu lỗi:

```text
- Hiển thị lỗi inline dưới field.
- Không đóng modal.
- Không clear form.
```

### 3.2. Backend tạo user thật

Command gợi ý:

```text
AddDepartmentPersonnelCommand
```

Logic:

```text
1. Check current user là Department Leader của đúng department.
2. Check department tồn tại, ACTIVE.
3. Check email chưa tồn tại trong users.
4. Lấy role_id của role_code = DEPARTMENT.
5. Tạo user:
   - full_name = input
   - email = input
   - phone = input
   - gender = input
   - role_id = DEPARTMENT role_id
   - sub_role = STAFF
   - primary_campus_id = department.campus_id
   - department_id = departmentId
   - status = ACTIVE
   - created_via = MANUAL_CREATED nếu field/entity đang có
   - password_hash = NULL vì login bằng SSO/Google/FEID
   - created_by = currentUser.userId
   - created_at = now
```

Không tạo password local.

---

## 4. Gửi mail tự động sau khi thêm nhân sự

Sau khi tạo user thành công, tự động gửi email tới email vừa tạo.

Subject:

```text
[PEMS] Vai trò tài khoản của bạn đã được cập nhật
```

Body, dùng data thật, không hard-code:

```text
Xin chào {FullName},

Vai trò tài khoản PEMS của bạn đã được cập nhật.

Thông tin mới:

Email đăng nhập: {Email}
Vai trò mới: DEPARTMENT staff
Cơ sở: {CampusName}
Phòng ban: {DepartmentName}

Thay đổi này có thể yêu cầu bạn đăng nhập lại để hệ thống áp dụng quyền truy cập mới.

Nếu bạn cho rằng thông tin này chưa chính xác, vui lòng liên hệ Staff Leader hoặc quản trị hệ thống để được hỗ trợ.

Trân trọng,
PEMS System
```

Dùng EmailService/SMTP hiện tại để gửi mail thật.

Log DB:

```text
sent_emails:
- related_type = 'USER'
- related_id = newUserId
- subject
- body_snapshot
- status = SENT / FAILED / QUEUED theo kết quả thật
- sent_by = currentUser.userId
- sent_at

sent_email_recipients:
- sent_email_id
- recipient_email = newUser.email
- recipient_name = newUser.full_name
- recipient_type = TO
- delivery_status = SENT / FAILED / QUEUED
```

Toast:

```text
Tạo user + gửi mail thành công:
“Đã thêm nhân sự và gửi email thông báo thành công.”

Tạo user thành công nhưng gửi mail lỗi:
“Đã thêm nhân sự, nhưng gửi email thông báo thất bại.”

Tạo user lỗi:
Hiển thị message lỗi từ backend.
```

Sau success:

```text
- Đóng modal.
- Reset form.
- Refresh danh sách nhân sự.
```

---

## 5. Xem / sửa thông tin nhân sự

Icon con mắt mở modal chi tiết nhân sự, lấy DB thật.

Hiển thị:

```text
full_name
email
phone
gender
campus name
department name
sub_role
status
created_at
updated_at
```

Khi bấm sửa/lưu:

```text
- Chỉ cho sửa: full_name, phone, gender.
- Không sửa role/campus/department trong modal này.
- Email không nên sửa vì dùng làm định danh SSO/login.
```

API gợi ý:

```text
GET /api/departments/{departmentId}/personnel/{userId}
PUT /api/departments/{departmentId}/personnel/{userId}
```

Toast:

```text
Success: “Đã cập nhật thông tin nhân sự thành công.”
Error: message từ backend.
```

---

## 6. Đổi trưởng phòng

Icon vương miện mở modal đổi trưởng phòng.

### 6.1. Load ứng viên

Chỉ lấy user hợp lệ:

```text
users.department_id = current department
users.status = ACTIVE
role_code = DEPARTMENT
```

Trưởng phòng hiện tại có thể hiển thị nhưng đánh dấu “Hiện tại”.

### 6.2. Khi xác nhận

Backend chạy transaction:

```text
1. Check current user có quyền đổi trưởng phòng trong đúng department.
2. Check newLeader thuộc đúng department, ACTIVE, role DEPARTMENT.
3. oldLeader = departments.head_user_id.
4. Nếu oldLeader != newLeader:
   - oldLeader.sub_role = STAFF nếu vẫn thuộc department.
   - newLeader.sub_role = LEADER.
   - departments.head_user_id = newLeader.user_id.
   - departments.updated_by = currentUser.userId.
   - departments.updated_at = now.
5. Return success.
```

API gợi ý:

```text
PUT /api/departments/{departmentId}/leader
```

Request:

```json
{
  "newLeaderUserId": 123
}
```

Toast:

```text
Success: “Đã thay đổi trưởng phòng thành công.”
Error: message từ backend.
```

Sau success:

```text
- Đóng modal.
- Refresh danh sách nhân sự.
- Badge Trưởng phòng cập nhật đúng.
```

---

## 7. Gỡ nhân sự nếu icon thùng rác đang có

Không hard delete.

Không set `department_id = NULL`.

Logic:

```text
users.status = INACTIVE
updated_by = currentUser.userId
updated_at = now
```

Không cho:

```text
- Department Leader tự gỡ chính mình.
- Gỡ trưởng phòng hiện tại nếu chưa đổi trưởng phòng.
```

Toast:

```text
Success: “Đã gỡ nhân sự khỏi danh sách hoạt động.”
Error: message từ backend.
```

---

## 8. Backend structure gợi ý

Dùng cấu trúc hiện tại, không tạo lung tung.

```text
PEMS.Application/Departments/Queries/ViewDepartmentPersonnelList
PEMS.Application/Departments/Queries/ViewDepartmentPersonnelDetail
PEMS.Application/Departments/Commands/AddDepartmentPersonnel
PEMS.Application/Departments/Commands/UpdateDepartmentPersonnel
PEMS.Application/Departments/Commands/ReassignDepartmentLead
PEMS.Application/Departments/Commands/RemoveDepartmentPersonnel
```

Controller:

```text
DepartmentsController
```

Controller chỉ gọi MediatR, không viết business logic.

---

## 9. Checklist nghiệm thu

```text
[ ] Danh sách nhân sự lấy từ DB thật.
[ ] Search hoạt động với DB thật.
[ ] Thêm nhân sự validate đủ field.
[ ] Tạo user mới role_code = DEPARTMENT, sub_role = STAFF.
[ ] Email duplicate bị chặn.
[ ] primary_campus_id lấy từ department.campus_id.
[ ] department_id đúng phòng ban hiện tại.
[ ] Tạo xong tự gửi mail thật.
[ ] Log email vào sent_emails và sent_email_recipients.
[ ] Có toast success/warning/error đúng.
[ ] Xem chi tiết lấy DB thật.
[ ] Sửa thông tin chỉ sửa field được phép.
[ ] Đổi trưởng phòng cập nhật departments.head_user_id.
[ ] Đổi trưởng phòng cập nhật sub_role old/new đúng.
[ ] Gỡ nhân sự set status = INACTIVE, không hard delete.
[ ] Không cho gỡ chính mình.
[ ] Không cho gỡ trưởng phòng hiện tại nếu chưa đổi trưởng phòng.
[ ] dotnet build pass.
[ ] npm run build pass.
```

---

## 10. Output mong muốn

Báo cáo ngắn:

```text
Đã làm:
- List personnel từ DB.
- Add personnel + validate + create user DEPARTMENT/STAFF.
- Auto send email + log sent_emails/sent_email_recipients.
- View/update personnel detail.
- Reassign department leader.
- Remove personnel bằng status INACTIVE.

DB:
- Không cần đổi schema.

Files changed:
- ...

Build:
- Backend: pass/fail
- Frontend: pass/fail
```
