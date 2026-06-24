# Frontend Implementation Checklist — Profile UC

## 1. Files/modules đề xuất

```text
src/features/profile/pages/ProfilePage.tsx
src/features/profile/api/profile.api.ts
src/features/profile/types/profile.types.ts
src/features/profile/components/AvatarUploader.tsx
src/features/profile/components/GenderSelect.tsx
src/features/profile/components/NationalitySearchableDropdown.tsx
src/features/profile/constants/nationalities.ts
```

Tên file có thể đổi theo cấu trúc project hiện tại, nhưng không đổi logic nghiệp vụ.

## 2. API service

```ts
export async function getMyProfile(): Promise<ViewProfileResponse>;

export async function updateMyProfile(
  payload: UpdateProfileRequest
): Promise<ViewProfileResponse>;

export async function uploadMyAvatar(
  file: File
): Promise<UploadAvatarResponse>;

export async function getFilePreviewBlob(
  fileId: number
): Promise<Blob>;
```

## 3. View Profile UI rules

```text
- Label dùng “Cơ sở”, không dùng “Campus”.
- Không hardcode ADMIN = Hà Nội.
- Không hardcode HO = Toàn quốc.
- Cơ sở hiển thị từ response displayCampusName/campus.name.
- VISITOR không hiển thị Cơ sở.
- Role hiển thị là role gốc: ADMIN, HO, STAFF, DEPARTMENT, STUDENT, VISITOR.
- Chức vụ hiển thị riêng cho STAFF/DEPARTMENT:
  LEADER => Trưởng phòng
  STAFF  => Nhân viên
```

## 4. Role-based display

### VISITOR

```text
Avatar
Họ và tên
Giới tính
Email
Số điện thoại
Quốc tịch
Vai trò: VISITOR
```

### STUDENT

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

### DEPARTMENT

```text
Avatar
Họ và tên
Giới tính
Email
Số điện thoại
Phòng ban
Vai trò: DEPARTMENT
Chức vụ
Cơ sở
```

### STAFF

```text
Avatar
Họ và tên
Giới tính
Cơ sở
Vai trò: STAFF
Số điện thoại
Email
Phòng ban
Chức vụ
```

### ADMIN

```text
Avatar
Họ và tên
Giới tính
Cơ sở
Vai trò: ADMIN
Email
Số điện thoại
```

### HO

```text
Avatar
Họ và tên
Giới tính
Cơ sở
Vai trò: HO
Email
Số điện thoại
```

## 5. Edit mode rules

Editable fields:

```text
Họ và tên
Giới tính
Số điện thoại
Avatar
Quốc tịch nếu role = VISITOR
```

Readonly fields:

```text
Email
Vai trò
Chức vụ
Cơ sở
Phòng ban
MSSV
Status
```

## 6. Gender select

UI options:

```ts
const GENDER_OPTIONS = [
  { label: 'Nam', value: 'MALE' },
  { label: 'Nữ', value: 'FEMALE' },
  { label: 'Khác', value: 'OTHER' },
] as const;
```

Validation:

```text
- Không nhập tự do.
- Chỉ gửi MALE / FEMALE / OTHER hoặc null nếu cho phép bỏ trống.
```

## 7. Nationality dropdown

Chỉ hiển thị editable cho VISITOR.

Requirements:

```text
- Có search input.
- Có max-height và scroll.
- Tìm không phân biệt hoa thường.
- Có thể tìm bằng label hoặc aliases.
- Gửi value tiếng Anh ổn định xuống backend.
```

Example:

```ts
const NATIONALITY_OPTIONS = [
  { label: 'Việt Nam', value: 'Vietnam', aliases: ['viet nam', 'vietnam', 'việt nam'] },
  { label: 'Hoa Kỳ', value: 'United States', aliases: ['hoa kỳ', 'my', 'mỹ', 'united states', 'usa'] },
  { label: 'Nhật Bản', value: 'Japan', aliases: ['nhật', 'nhat ban', 'japan'] },
  { label: 'Hàn Quốc', value: 'South Korea', aliases: ['hàn', 'han quoc', 'korea', 'south korea'] },
  { label: 'Khác', value: 'Other', aliases: ['khác', 'other'] },
];
```

## 8. Avatar upload UI

Flow:

```text
1. User bấm đổi avatar.
2. User chọn file.
3. Frontend validate sơ bộ:
   - image/jpeg, image/png, image/webp
   - dung lượng <= max size
4. Frontend preview file bằng object URL.
5. User bấm lưu ảnh.
6. Frontend upload multipart/form-data.
7. Sau khi success, update profile state/avatar.
```

## 9. Protected preview URL with JWT

Nếu `avatarUrl` là `/api/files/{fileId}/preview` và backend yêu cầu JWT Bearer, không dùng trực tiếp:

```tsx
<img src={profile.avatarUrl} />
```

Thay vào đó fetch Blob:

```ts
const res = await axios.get(profile.avatarUrl, {
  responseType: 'blob',
  headers: { Authorization: `Bearer ${token}` },
});

const objectUrl = URL.createObjectURL(res.data);
setAvatarSrc(objectUrl);
```

Cleanup:

```ts
useEffect(() => {
  return () => {
    if (avatarSrc?.startsWith('blob:')) URL.revokeObjectURL(avatarSrc);
  };
}, [avatarSrc]);
```

## 10. UX states

```text
Loading:
- Skeleton/card loading hoặc spinner nhẹ.

Empty avatar:
- Default avatar hoặc chữ cái đầu của fullName.

Upload in progress:
- Disable save avatar button.
- Hiển thị loading.

Validation error:
- Hiển thị dưới input tương ứng.

Success:
- Toast “Cập nhật hồ sơ thành công”.

Failure:
- Không clear form.
- Giữ dữ liệu user đang nhập.
```

## 11. Design system notes

```text
- Enterprise dashboard style.
- Card rounded-2xl border border-slate-200 bg-white shadow-sm.
- Primary color #004c91.
- Label text slate-500.
- Không lạm dụng màu.
- Button không xuống dòng.
- Mobile responsive.
```

## 12. Build verification

```bash
npm run build
```

Manual UI test:

```text
- Login từng role.
- View Profile render đúng field.
- Chữ Cơ sở hiển thị đúng.
- Gender dropdown chỉ có Nam/Nữ/Khác.
- Visitor có nationality searchable dropdown.
- Non-Visitor không sửa nationality.
- Upload avatar preview trước khi lưu.
- Upload thành công update header/sidebar/profile avatar.
```
