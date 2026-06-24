# Profile UC Test Cases and Acceptance Criteria

## 1. UC-14 View Profile — Positive Cases

### TC-01 — VISITOR view profile

Given VISITOR đã đăng nhập  
When gọi `GET /api/profile/me`  
Then hệ thống trả profile của chính VISITOR  
And UI hiển thị:

```text
Avatar
Họ và tên
Giới tính
Email
Số điện thoại
Quốc tịch
Vai trò: VISITOR
```

And UI không hiển thị `Cơ sở`, `Phòng ban`, `Chức vụ`.

### TC-02 — STUDENT view profile

Given STUDENT đã đăng nhập và có `primary_campus_id`  
When mở View Profile  
Then UI hiển thị `Cơ sở` theo `campuses.name`  
And không hardcode cơ sở.

### TC-03 — STAFF view profile

Given STAFF có `role_code = STAFF`, `sub_role = LEADER`  
When mở View Profile  
Then UI hiển thị:

```text
Vai trò: STAFF
Chức vụ: Trưởng phòng
```

Given STAFF có `sub_role = STAFF`  
Then UI hiển thị:

```text
Chức vụ: Nhân viên
```

### TC-04 — DEPARTMENT view profile

Given DEPARTMENT có `sub_role = LEADER`  
When mở View Profile  
Then UI hiển thị `Chức vụ: Trưởng phòng`.

Given DEPARTMENT có `sub_role = STAFF`  
Then UI hiển thị `Chức vụ: Nhân viên`.

### TC-05 — ADMIN cơ sở theo DB

Given ADMIN có `primary_campus_id` trỏ tới campus HCM  
When mở View Profile  
Then UI hiển thị `Cơ sở: TP.HCM`  
And không hiển thị hardcode `Hà Nội`.

### TC-06 — HO cơ sở theo DB

Given HO có `primary_campus_id` trỏ tới campus Đà Nẵng  
When mở View Profile  
Then UI hiển thị `Cơ sở: Đà Nẵng`  
And không hiển thị hardcode `Toàn quốc`.

## 2. UC-14 View Profile — Negative Cases

### TC-07 — Chưa đăng nhập

Given user chưa đăng nhập  
When gọi `GET /api/profile/me`  
Then backend trả 401.

### TC-08 — User không tồn tại

Given token chứa user_id không tồn tại  
When gọi `GET /api/profile/me`  
Then backend trả 404 hoặc 401 theo policy.

### TC-09 — Account bị INACTIVE/LOCKED

Given account không được phép truy cập  
When gọi `GET /api/profile/me`  
Then backend trả 403.

### TC-10 — Thiếu cơ sở với internal user

Given internal user có `primary_campus_id = NULL` hoặc join campus không ra  
When View Profile  
Then UI hiển thị `Cơ sở: Chưa cấu hình`  
And backend có thể log warning dữ liệu.

## 3. UC-15 Update Text — Positive Cases

### TC-11 — Update fullName/phone/gender

Given user ACTIVE đã đăng nhập  
When gửi `PATCH /api/profile/me` với:

```json
{
  "fullName": "Nguyen Van A",
  "phone": "0912345678",
  "gender": "MALE"
}
```

Then backend update `users.full_name`, `users.phone`, `users.gender`  
And set `updated_at`, `updated_by`  
And response trả profile mới.

### TC-12 — VISITOR update nationality

Given VISITOR đã đăng nhập  
When gửi nationality = `Japan`  
Then backend update `users.nationality = Japan`.

### TC-13 — Gender mapping UI

Given user chọn `Nam` trên UI  
When frontend submit  
Then payload gửi `gender = MALE`.

Given user chọn `Nữ`  
Then payload gửi `gender = FEMALE`.

Given user chọn `Khác`  
Then payload gửi `gender = OTHER`.

## 4. UC-15 Update Text — Negative Cases

### TC-14 — fullName rỗng

When gửi `fullName = "   "`  
Then backend trả 422  
And không update DB.

### TC-15 — gender invalid

When gửi `gender = "NAM"` hoặc `gender = "Male"`  
Then backend trả 422.

### TC-16 — non-Visitor gửi nationality

Given STAFF đã đăng nhập  
When gửi nationality trong PATCH profile  
Then backend reject 403/422  
And không update DB.

### TC-17 — payload chứa field cấm

When user gửi:

```json
{
  "roleId": 1,
  "primaryCampusId": 2,
  "status": "ACTIVE"
}
```

Then backend reject toàn bộ request  
And không update bất kỳ field nào.

## 5. UC-15 Upload Avatar — Positive Cases

### TC-18 — Upload avatar thành công

Given user ACTIVE đã đăng nhập  
And file là `image/png`, dung lượng hợp lệ  
When gọi `POST /api/profile/me/avatar`  
Then backend upload file lên Google Drive folder `profile-avatars`  
And insert 1 dòng vào `files`:

```text
storage_provider = GOOGLE_DRIVE
file_purpose = USER_AVATAR
uploaded_by = currentUserId
external_file_id is not null
```

And update:

```text
users.avatar_url = /api/files/{fileId}/preview
```

And response trả `avatarUrl`, `fileId`.

### TC-19 — Preview avatar qua backend

Given `users.avatar_url = /api/files/{fileId}/preview`  
When frontend gọi preview endpoint có auth hợp lệ  
Then backend stream ảnh từ Google Drive  
And response có `Content-Type` đúng.

## 6. UC-15 Upload Avatar — Negative Cases

### TC-20 — File rỗng

When upload file rỗng  
Then backend reject 422  
And không upload Drive  
And không insert files.

### TC-21 — File sai MIME

When upload `application/pdf` hoặc `.exe`  
Then backend reject 422.

### TC-22 — File quá lớn

When file vượt max size  
Then backend reject 422  
And frontend hiển thị lỗi dung lượng.

### TC-23 — Google Drive upload fail

Given Google Drive API lỗi  
When upload avatar  
Then backend không insert files  
And không update `users.avatar_url`.

### TC-24 — DB fail sau Drive upload

Given upload Drive thành công nhưng insert DB fail  
Then backend rollback DB  
And cố gắng xóa file vừa upload khỏi Drive.

### TC-25 — Unauthorized preview

Given user A cố xem file avatar private của user B  
When gọi `/api/files/{fileId}/preview`  
Then backend trả 403.

## 7. Acceptance Criteria tổng hợp

```text
AC-01: User đã đăng nhập xem được profile của chính mình.
AC-02: API profile không nhận userId từ frontend.
AC-03: View Profile hiển thị field theo role.
AC-04: Tất cả label Campus đổi thành Cơ sở.
AC-05: ADMIN và HO hiển thị Cơ sở theo database, không hardcode.
AC-06: Update Profile chỉ cho sửa allowed fields.
AC-07: Field nhạy cảm bị gửi trong payload phải bị reject.
AC-08: Gender chỉ có Nam/Nữ/Khác mapping MALE/FEMALE/OTHER.
AC-09: Quốc tịch chỉ Visitor sửa, dùng searchable dropdown.
AC-10: Avatar upload lên Google Drive, không lưu binary trong DB.
AC-11: Bảng files lưu metadata file.
AC-12: users.avatar_url lưu backend URL /api/files/{fileId}/preview.
AC-13: Preview file qua backend phải kiểm tra quyền.
AC-14: Upload lỗi không làm thay đổi avatar cũ.
AC-15: Sau update thành công, Profile/Header/Sidebar cập nhật avatar/tên mới.
```
