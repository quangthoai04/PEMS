# PEMS Profile UC Implementation Pack

## Mục đích
Bộ file này chia nhỏ đặc tả để AI Agent đọc và code chức năng:

- UC-14 — View Profile
- UC-15 — Update Profile
- Upload avatar lên Google Drive
- Preview/download file qua backend URL
- Cấu hình Google Drive Service Account
- Checklist backend/frontend/test

## Nguyên tắc bắt buộc

1. Đây là self-service profile: user chỉ được xem/sửa hồ sơ của chính mình.
2. Không dùng `userId` từ frontend để xác định người cần xem/sửa.
3. Backend luôn lấy `currentUserId` từ JWT/session/current user context.
4. Không cho sửa field nhạy cảm trong UC-15:
   - email
   - role_id / role_code
   - sub_role
   - primary_campus_id
   - department_id
   - student_code
   - fe_id
   - status
   - password_hash
5. Tất cả label `Campus` trên UI đổi thành `Cơ sở`.
6. `Cơ sở` luôn lấy từ database qua `users.primary_campus_id -> campuses.campus_id`, kể cả ADMIN và HO.
7. Không hardcode:
   - `ADMIN = Hà Nội`
   - `HO = Toàn quốc`
8. VISITOR không hiển thị `Cơ sở` theo rule hiện tại vì Visitor không có `primary_campus_id`.
9. Avatar upload lên Google Drive. Database không lưu binary ảnh.
10. Bảng `files` lưu metadata file.
11. `users.avatar_url` lưu backend URL dạng `/api/files/{fileId}/preview`, không lưu link Google Drive trực tiếp.
12. Nếu frontend dùng JWT Bearer token, không render avatar protected bằng `<img src>` trực tiếp nếu endpoint yêu cầu Authorization header. Hãy fetch Blob qua Axios/fetch có token rồi tạo object URL.

## Thứ tự đọc đề xuất cho AI Agent

1. `01_UC14_VIEW_PROFILE_SPEC.md`
2. `02_UC15_UPDATE_PROFILE_TEXT_SPEC.md`
3. `03_UC15_UPLOAD_AVATAR_GOOGLE_DRIVE_SPEC.md`
4. `04_FILE_PREVIEW_BACKEND_URL_SPEC.md`
5. `05_GOOGLE_DRIVE_CONFIGURATION_GUIDE.md`
6. `06_BACKEND_IMPLEMENTATION_CHECKLIST.md`
7. `07_FRONTEND_IMPLEMENTATION_CHECKLIST.md`
8. `08_TEST_CASES_AND_ACCEPTANCE_CRITERIA.md`

## Các endpoint mục tiêu

```http
GET /api/profile/me
PATCH /api/profile/me
POST /api/profile/me/avatar
GET /api/files/{fileId}/preview
```

## Các bảng liên quan

- `users`
- `roles`
- `campuses`
- `departments`
- `files`

## Quy ước role/sub_role

```text
ADMIN      + NULL
HO         + NULL
STAFF      + LEADER  => Staff Leader / Trưởng phòng IC
STAFF      + STAFF   => IC Staff / Nhân viên IC
DEPARTMENT + LEADER  => Department Leader / Trưởng phòng
DEPARTMENT + STAFF   => Department Staff / Nhân viên
STUDENT    + NULL
VISITOR    + NULL
```
