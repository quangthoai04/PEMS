# UC-15 — Update Profile Text Fields Specification

## 1. Mục đích
Cho phép user cập nhật các thông tin cá nhân an toàn trong self-service profile.

Endpoint:

```http
PATCH /api/profile/me
```

Backend lấy `currentUserId` từ token/session/current user context. Không nhận `userId` từ frontend.

## 2. Actor
Tất cả user đã đăng nhập:

```text
ADMIN
HO
STAFF
DEPARTMENT
STUDENT
VISITOR
```

## 3. Field được phép sửa

### 3.1 Áp dụng cho tất cả role

```text
Họ và tên
Giới tính
Số điện thoại
```

| UI field | DB field |
|---|---|
| Họ và tên | `users.full_name` |
| Giới tính | `users.gender` |
| Số điện thoại | `users.phone` |

### 3.2 Chỉ VISITOR được sửa thêm

```text
Quốc tịch
```

| UI field | DB field |
|---|---|
| Quốc tịch | `users.nationality` |

## 4. Field không được sửa trong UC-15

Các field sau là readonly trong self-service profile:

```text
Email
Vai trò / role
Chức vụ / sub_role
Cơ sở / primary_campus_id
Phòng ban / department_id
MSSV / student_code
FE ID / fe_id
Status
Password
created_at
created_by
updated_by do client gửi lên
```

Backend phải reject toàn bộ request nếu payload chứa field cấm. Không update một phần.

## 5. Gender / Giới tính

UI không cho nhập tự do. Dùng select/dropdown 3 lựa chọn:

| UI hiển thị | Value gửi backend | DB lưu |
|---|---|---|
| Nam | `MALE` | `MALE` |
| Nữ | `FEMALE` | `FEMALE` |
| Khác | `OTHER` | `OTHER` |

Nếu hệ thống cho phép bỏ trống, frontend gửi `null` hoặc không gửi field `gender`.

Validation:

```text
- Không nhận text tự do.
- Chỉ nhận MALE / FEMALE / OTHER / null.
- Backend phải validate lại, không tin hoàn toàn frontend.
```

## 6. Quốc tịch / Nationality

Quốc tịch chỉ cho VISITOR sửa.

UI dùng searchable dropdown có scroll, không nhập text tự do.

Yêu cầu UI:

```text
- Có ô search ở đầu dropdown.
- Có danh sách quốc tịch phổ biến.
- Có max-height, ví dụ 240px hoặc 280px.
- Có scroll dọc.
- Tìm kiếm không phân biệt hoa thường.
- Nên hỗ trợ tìm bằng tiếng Việt và tiếng Anh nếu có alias.
```

Frontend lưu danh sách dạng:

```ts
const NATIONALITY_OPTIONS = [
  { label: 'Việt Nam', value: 'Vietnam', aliases: ['viet nam', 'vietnam', 'việt nam'] },
  { label: 'Hoa Kỳ', value: 'United States', aliases: ['hoa kỳ', 'my', 'mỹ', 'united states', 'usa'] },
  { label: 'Nhật Bản', value: 'Japan', aliases: ['nhật', 'nhat ban', 'japan'] },
  { label: 'Hàn Quốc', value: 'South Korea', aliases: ['hàn', 'han quoc', 'korea', 'south korea'] },
  { label: 'Trung Quốc', value: 'China', aliases: ['trung quốc', 'china'] },
  { label: 'Singapore', value: 'Singapore', aliases: ['singapore'] },
  { label: 'Thái Lan', value: 'Thailand', aliases: ['thái lan', 'thai lan', 'thailand'] },
  { label: 'Malaysia', value: 'Malaysia', aliases: ['malaysia'] },
  { label: 'Indonesia', value: 'Indonesia', aliases: ['indonesia'] },
  { label: 'Philippines', value: 'Philippines', aliases: ['philippines'] },
  { label: 'Ấn Độ', value: 'India', aliases: ['ấn độ', 'an do', 'india'] },
  { label: 'Úc', value: 'Australia', aliases: ['úc', 'uc', 'australia'] },
  { label: 'Canada', value: 'Canada', aliases: ['canada'] },
  { label: 'Vương quốc Anh', value: 'United Kingdom', aliases: ['anh', 'uk', 'united kingdom'] },
  { label: 'Pháp', value: 'France', aliases: ['pháp', 'phap', 'france'] },
  { label: 'Đức', value: 'Germany', aliases: ['đức', 'duc', 'germany'] },
  { label: 'Ý', value: 'Italy', aliases: ['ý', 'y', 'italy'] },
  { label: 'Tây Ban Nha', value: 'Spain', aliases: ['tây ban nha', 'spain'] },
  { label: 'Hà Lan', value: 'Netherlands', aliases: ['hà lan', 'netherlands'] },
  { label: 'Khác', value: 'Other', aliases: ['khác', 'other'] },
];
```

Nên lưu DB bằng `value`, ví dụ `Vietnam`, `Japan`, `United States` để dữ liệu ổn định. Vì `users.nationality` là `VARCHAR(100)`, backend phải validate không vượt quá 100 ký tự.

## 7. Request DTO

```ts
type UpdateProfileRequest = {
  fullName?: string;
  gender?: 'MALE' | 'FEMALE' | 'OTHER' | null;
  phone?: string | null;
  nationality?: string | null; // chỉ VISITOR được gửi
};
```

## 8. Main Flow

```text
[U] Step 1. User mở View Profile.

[U] Step 2. User bấm “Chỉnh sửa hồ sơ”.

[S] Step 3. Frontend chuyển sang edit mode.

[U] Step 4. User chỉnh các field được phép:
- Họ và tên
- Giới tính
- Số điện thoại
- Quốc tịch nếu là VISITOR

[U] Step 5. User bấm “Lưu thay đổi”.

[S] Step 6. Frontend validate cơ bản:
- Họ và tên không được rỗng sau khi trim.
- Số điện thoại đúng format nếu user có nhập.
- Giới tính chỉ được chọn: Nam / Nữ / Khác.
- Gender gửi backend là MALE / FEMALE / OTHER.
- Quốc tịch chỉ áp dụng cho VISITOR.
- Quốc tịch chọn từ searchable dropdown, không nhập text tự do.
- nationality không vượt quá 100 ký tự.

[S] Step 7. Frontend gọi PATCH /api/profile/me.

[S] Step 8. Backend lấy currentUserId từ token/session.

[S] Step 9. Backend reject request nếu payload chứa field cấm:
email, role_id, sub_role, primary_campus_id, department_id, status, student_code, fe_id.

[S] Step 10. Backend validate lại toàn bộ dữ liệu.

[S] Step 11. Backend update bảng users:
- full_name
- gender
- phone
- nationality nếu current user là VISITOR

[S] Step 12. Backend set updated_at, updated_by = currentUserId.

[S] Step 13. Backend trả về profile mới.

[S] Step 14. Frontend thoát edit mode, cập nhật UI và hiện thông báo thành công.
```

## 9. Backend validation rule

```text
fullName:
- Required nếu field được gửi.
- Trim trước khi lưu.
- Không được rỗng.
- Max length theo DB/schema hiện tại.

phone:
- Nullable.
- Trim trước khi lưu.
- Validate format nếu có nhập.

Gender:
- Nullable nếu hệ thống cho phép bỏ trống.
- Nếu có value thì chỉ nhận MALE / FEMALE / OTHER.

Nationality:
- Chỉ cho VISITOR update.
- Nếu role khác VISITOR gửi nationality thì reject 403/422.
- Max 100 ký tự.
- Nên nằm trong danh sách value cho phép.
```

## 10. Alternative Flows

```text
AF-01 — fullName rỗng
Backend trả 422, không update.

AF-02 — phone sai format
Backend trả 422, không update.

AF-03 — gender không thuộc MALE/FEMALE/OTHER
Backend trả 422, không update.

AF-04 — role không phải VISITOR nhưng gửi nationality
Backend reject 403/422, không update.

AF-05 — payload chứa field cấm
Backend reject toàn bộ request, không update một phần.

AF-06 — session hết hạn
Backend trả 401, frontend redirect login hoặc hiển thị thông báo phiên hết hạn.
```
