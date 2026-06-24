# UC-14 — View Profile Specification

## 1. Mục đích
Cho phép user đã đăng nhập xem hồ sơ cá nhân của chính mình. Đây là màn hình self-service profile, không phải màn hình quản lý account.

## 2. Endpoint

```http
GET /api/profile/me
```

Không dùng:

```http
GET /api/profile/{userId}
```

Backend phải lấy `currentUserId` từ token/session/current user context.

## 3. Actor
Tất cả user đã đăng nhập:

```text
ADMIN
HO
STAFF
DEPARTMENT
STUDENT
VISITOR
```

## 4. Preconditions

- User đã đăng nhập.
- Session/token còn hiệu lực.
- User tồn tại trong bảng `users`.
- Account không bị chặn truy cập do `INACTIVE` hoặc `LOCKED`.

## 5. Postconditions

- Không thay đổi dữ liệu.
- Chỉ trả về profile của current user.
- Không trả về password hash, token, OTP, refresh token, security fields hoặc dữ liệu của user khác.

## 6. Data query rule

Backend query:

```sql
users
JOIN roles ON users.role_id = roles.role_id
LEFT JOIN campuses ON users.primary_campus_id = campuses.campus_id
LEFT JOIN departments ON users.department_id = departments.department_id
WHERE users.user_id = @currentUserId
```

## 7. Quy tắc hiển thị chung

- Tất cả chữ `Campus` trên UI phải đổi thành `Cơ sở`.
- Field `Cơ sở` lấy từ database:

```text
users.primary_campus_id -> campuses.campus_id -> campuses.name
```

- Không hardcode `ADMIN = Hà Nội`.
- Không hardcode `HO = Toàn quốc`.
- Nếu user cần có cơ sở nhưng không join được campus, hiển thị:

```text
Cơ sở: Chưa cấu hình
```

- VISITOR không hiển thị `Cơ sở` vì Visitor không gắn campus nội bộ.

## 8. Field hiển thị theo role

### 8.1 VISITOR

```text
Avatar
Họ và tên
Giới tính
Email
Số điện thoại
Quốc tịch
Vai trò: VISITOR
```

| Label UI | Field |
|---|---|
| Avatar | `users.avatar_url` |
| Họ và tên | `users.full_name` |
| Giới tính | `users.gender` |
| Email | `users.email` |
| Số điện thoại | `users.phone` |
| Quốc tịch | `users.nationality` |
| Vai trò | `roles.role_code = VISITOR` |

### 8.2 STUDENT

```text
Avatar
Họ và tên
Giới tính
MSSV
Cơ sở
Vai trò: STUDENT
Email
Số điện thoại
```

| Label UI | Field |
|---|---|
| Avatar | `users.avatar_url` |
| Họ và tên | `users.full_name` |
| Giới tính | `users.gender` |
| MSSV | `users.student_code` |
| Cơ sở | `campuses.name` qua `users.primary_campus_id` |
| Vai trò | `roles.role_code = STUDENT` |
| Email | `users.email` |
| Số điện thoại | `users.phone` |

### 8.3 DEPARTMENT

```text
Avatar
Họ và tên
Giới tính
Email
Số điện thoại
Phòng ban
Vai trò: DEPARTMENT
Chức vụ: Trưởng phòng / Nhân viên
Cơ sở
```

Mapping chức vụ:

| DB value | UI label |
|---|---|
| `LEADER` | Trưởng phòng |
| `STAFF` | Nhân viên |

| Label UI | Field |
|---|---|
| Avatar | `users.avatar_url` |
| Họ và tên | `users.full_name` |
| Giới tính | `users.gender` |
| Email | `users.email` |
| Số điện thoại | `users.phone` |
| Phòng ban | `departments.name` |
| Vai trò | `roles.role_code = DEPARTMENT` |
| Chức vụ | `users.sub_role` |
| Cơ sở | `campuses.name` qua `users.primary_campus_id` |

### 8.4 STAFF

```text
Avatar
Họ và tên
Giới tính
Cơ sở
Vai trò: STAFF
Số điện thoại
Email
Phòng ban: Hợp tác quốc tế (IC)
Chức vụ: Trưởng phòng / Nhân viên
```

Mapping chức vụ:

| DB value | UI label |
|---|---|
| `LEADER` | Trưởng phòng |
| `STAFF` | Nhân viên |

| Label UI | Field |
|---|---|
| Avatar | `users.avatar_url` |
| Họ và tên | `users.full_name` |
| Giới tính | `users.gender` |
| Cơ sở | `campuses.name` qua `users.primary_campus_id` |
| Vai trò | `roles.role_code = STAFF` |
| Số điện thoại | `users.phone` |
| Email | `users.email` |
| Phòng ban | `departments.name`, thường là IC |
| Chức vụ | `users.sub_role` |

### 8.5 ADMIN

```text
Avatar
Họ và tên
Giới tính
Cơ sở
Vai trò: ADMIN
Email
Số điện thoại
```

| Label UI | Field |
|---|---|
| Avatar | `users.avatar_url` |
| Họ và tên | `users.full_name` |
| Giới tính | `users.gender` |
| Cơ sở | `campuses.name` qua `users.primary_campus_id` |
| Vai trò | `roles.role_code = ADMIN` |
| Email | `users.email` |
| Số điện thoại | `users.phone` |

### 8.6 HO

```text
Avatar
Họ và tên
Giới tính
Cơ sở
Vai trò: HO
Email
Số điện thoại
```

| Label UI | Field |
|---|---|
| Avatar | `users.avatar_url` |
| Họ và tên | `users.full_name` |
| Giới tính | `users.gender` |
| Cơ sở | `campuses.name` qua `users.primary_campus_id` |
| Vai trò | `roles.role_code = HO` |
| Email | `users.email` |
| Số điện thoại | `users.phone` |

## 9. Main Flow

```text
[U] Step 1. User bấm avatar/tên người dùng trên header/sidebar.

[U] Step 2. User chọn “Hồ sơ cá nhân”.

[S] Step 3. Frontend gọi GET /api/profile/me.

[S] Step 4. Backend lấy currentUserId từ token/session.

[S] Step 5. Backend query users và join roles, campuses, departments.

[S] Step 6. Backend build ViewProfileResponse theo role/sub_role.

[S] Step 7. Backend không trả về password_hash, token, OTP, security fields.

[S] Step 8. Frontend render các field đúng theo role.

[S] Step 9. Nếu account ACTIVE, frontend hiển thị nút “Chỉnh sửa hồ sơ”.
```

## 10. DTO đề xuất

```ts
type ViewProfileResponse = {
  userId: number;

  fullName: string;
  avatarUrl: string | null;
  gender: 'MALE' | 'FEMALE' | 'OTHER' | null;
  email: string;
  phone: string | null;
  nationality: string | null;

  roleCode: 'ADMIN' | 'HO' | 'STAFF' | 'DEPARTMENT' | 'STUDENT' | 'VISITOR';
  subRole: 'LEADER' | 'STAFF' | null;

  displayRole: string;
  displayPosition: 'Trưởng phòng' | 'Nhân viên' | null;

  studentCode: string | null;

  campus: {
    campusId: number;
    campusCode: string;
    name: string;
  } | null;

  displayCampusName: string | null;

  department: {
    departmentId: number;
    name: string;
    departmentType: 'IC' | 'GENERAL';
  } | null;

  displayDepartmentName: string | null;

  status: 'ACTIVE' | 'INACTIVE' | 'LOCKED';
};
```
