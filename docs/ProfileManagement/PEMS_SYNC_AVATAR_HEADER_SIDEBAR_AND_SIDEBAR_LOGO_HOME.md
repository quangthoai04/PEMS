# PEMS — Đồng bộ Avatar sau Upload và Click Logo Sidebar về Trang Chủ

> File này dùng cho AI Agent đọc và code bổ sung phần frontend sau khi upload avatar profile.  
> Mục tiêu: sau khi user upload avatar mới, avatar ở **Profile**, **Sidebar dashboard**, **Header trang chủ** phải cập nhật đồng bộ; đồng thời click logo trong sidebar dashboard phải điều hướng về trang chủ.

---

## 1. Bối cảnh hiện tại

Chức năng upload avatar đã hoàn chỉnh:

```text
- Backend upload ảnh lên Google Drive thành công.
- DB đã lưu users.avatar_url dạng /api/files/{fileId}/content.
- API upload avatar đã trả về avatarUrl.
- Avatar ở màn Profile có thể đã cập nhật.
```

Vấn đề còn lại:

```text
1. Avatar ở sidebar dashboard chưa tự cập nhật theo avatar mới.
2. Avatar ở header trang chủ chưa tự cập nhật theo avatar mới.
3. Logo trong sidebar dashboard hiện chưa click về trang chủ.
```

Ảnh minh họa vị trí cần cập nhật:

```text
- Sidebar dashboard: phần card user ở cuối sidebar.
- Header trang chủ: avatar ở góc phải header.
- Logo sidebar dashboard: logo FPT University ở đầu sidebar.
```

---

## 2. Mục tiêu cần code

Sau khi upload avatar thành công:

```text
User upload avatar mới ở Profile
→ API trả về avatarUrl mới
→ Profile cập nhật ảnh mới
→ AuthContext/currentUser cập nhật avatarUrl mới
→ Sidebar dashboard đọc currentUser.avatarUrl và đổi ảnh ngay
→ Header trang chủ đọc currentUser.avatarUrl và đổi ảnh ngay
→ Refresh trang vẫn hiển thị avatar mới
```

Đồng thời:

```text
Click logo FPT University ở sidebar dashboard
→ Điều hướng về trang chủ public "/"
```

---

## 3. Nguyên tắc bắt buộc

Không được:

```text
- Không sửa role/permission/RBAC.
- Không sửa menu visibility.
- Không rewrite layout dashboard/header.
- Không đổi API params nếu API upload avatar đã chạy.
- Không thêm thư viện mới.
- Không hardcode avatar riêng ở từng màn.
- Không để sidebar/header tự fetch user riêng nếu đã có AuthContext.
- Không làm mất style logo/sidebar hiện tại.
```

Nên làm:

```text
- Dùng AuthContext/currentUser làm nguồn avatar dùng chung.
- Sau upload avatar, cập nhật AuthContext.
- Sidebar và Header cùng đọc avatar từ AuthContext.
- Nếu currentUser được lưu trong localStorage/sessionStorage thì cập nhật storage cùng lúc.
- Dùng helper resolve URL nếu avatarUrl là relative URL.
```

---

## 4. Files cần kiểm tra

AI Agent cần kiểm tra các file thực tế trong repo, ví dụ:

```text
frontend/pems-react/src/shared/auth/AuthContext.tsx
frontend/pems-react/src/components/dashboard/Sidebar.tsx
frontend/pems-react/src/components/layout/Header.tsx
frontend/pems-react/src/features/profile/api/*
frontend/pems-react/src/features/profile/components/*
frontend/pems-react/src/pages/dashboard/profile/*
frontend/pems-react/src/shared/api/*
frontend/pems-react/src/shared/constants/appRoutes.ts
frontend/pems-react/src/App.tsx
```

Tên file thực tế có thể khác. Hãy search theo:

```text
useAuth
AuthContext
currentUser
avatarUrl
UploadProfileAvatar
uploadProfileAvatar
Sidebar
Header
FPT UNIVERSITY
```

---

## 5. Thiết kế đúng: avatar dùng chung từ AuthContext

### 5.1. Vấn đề cần tránh

Không làm kiểu mỗi nơi giữ avatar riêng:

```text
Profile có avatar state riêng
Sidebar có avatar state riêng
Header đọc localStorage trực tiếp
```

Vì như vậy sau upload avatar, chỉ Profile đổi, còn Sidebar/Header không đổi cho đến khi reload hoặc login lại.

### 5.2. Cách đúng

Dùng một nguồn chung:

```text
AuthContext.currentUser.avatarUrl
```

Sau upload avatar thành công:

```text
- Update profile state.
- Update AuthContext.currentUser.avatarUrl.
- Update localStorage/sessionStorage nếu AuthContext đang persist user.
```

Sidebar và Header chỉ cần đọc currentUser từ AuthContext.

---

## 6. Bổ sung hàm updateCurrentUser trong AuthContext

Nếu `AuthContext` đã có hàm update user thì dùng hàm sẵn.

Nếu chưa có, bổ sung hàm nhỏ, không rewrite toàn bộ context.

Ví dụ:

```ts
type UpdateCurrentUserPatch = Partial<AuthUser>;

const updateCurrentUser = (patch: UpdateCurrentUserPatch) => {
  setCurrentUser((prev) => {
    if (!prev) return prev;

    const nextUser = {
      ...prev,
      ...patch,
    };

    localStorage.setItem("currentUser", JSON.stringify(nextUser));
    return nextUser;
  });
};
```

Lưu ý:

```text
- Tên AuthUser, currentUser, setCurrentUser phải khớp code thật.
- localStorage key phải dùng đúng key hiện tại của project.
- Nếu project đang lưu auth object phức tạp hơn, ví dụ auth_user/token/user, chỉ update đúng phần user.
- Không tạo localStorage key mới nếu đã có key cũ.
```

Export trong context:

```ts
const value = {
  currentUser,
  login,
  logout,
  updateCurrentUser,
};
```

Hoặc nếu context đang dùng tên `user` thay vì `currentUser`, giữ theo tên hiện tại.

---

## 7. Sửa luồng upload avatar ở Profile

Tìm đoạn code gọi API upload avatar:

```ts
const result = await uploadProfileAvatar(selectedFile);
```

Sau khi API thành công, cần cập nhật state:

```ts
const result = await uploadProfileAvatar(selectedFile);

const nextAvatarUrl = result.avatarUrl;

setProfile((prev) => ({
  ...prev,
  avatarUrl: nextAvatarUrl,
}));

updateCurrentUser({
  avatarUrl: nextAvatarUrl,
});
```

Nếu API response đang bọc trong `data`, dùng đúng shape thật:

```ts
const response = await uploadProfileAvatar(selectedFile);
const nextAvatarUrl = response.data.avatarUrl;
```

hoặc:

```ts
const nextAvatarUrl = response.avatarUrl;
```

Tùy service hiện tại.

Yêu cầu:

```text
- Avatar ở Profile đổi ngay.
- AuthContext đổi ngay.
- Sidebar/Header đang mounted cũng re-render ngay.
```

---

## 8. Resolve avatar URL đúng backend

Backend đang lưu/trả avatarUrl dạng:

```text
/api/files/{fileId}/content
```

Đây là relative URL.

Nếu frontend và backend cùng domain hoặc Vite proxy `/api` đã cấu hình, có thể dùng trực tiếp.

Nếu frontend chạy port khác backend, ví dụ:

```text
Frontend: http://localhost:3000
Backend:  http://localhost:5265
```

thì cần helper ghép base URL:

```ts
export function resolveFileUrl(url?: string | null): string | null {
  if (!url) return null;

  if (url.startsWith("http://") || url.startsWith("https://")) {
    return url;
  }

  const baseUrl = import.meta.env.VITE_API_BASE_URL ?? "";
  return `${baseUrl}${url}`;
}
```

Ví dụ:

```text
resolveFileUrl("/api/files/421/content")
→ "http://localhost:5265/api/files/421/content"
```

Nếu project đã có helper sẵn như:

```text
buildApiUrl
getFileUrl
resolveAvatarUrl
```

thì dùng helper sẵn, không tạo trùng.

---

## 9. Fallback avatar khi ảnh lỗi

Vì file thật nằm trên Google Drive, có thể ảnh lỗi nếu:

```text
- Token Google Drive hết hạn.
- File bị xóa trong Google Drive.
- Endpoint /api/files/{id}/content lỗi.
```

Nên Sidebar/Header/Profile có fallback avatar default.

Ví dụ:

```tsx
<img
  src={avatarSrc}
  alt={currentUser?.fullName || "User avatar"}
  onError={(event) => {
    event.currentTarget.src = DEFAULT_AVATAR;
  }}
/>
```

Nếu project đang dùng icon avatar mặc định thay vì ảnh default, giữ theo UI hiện tại.

---

## 10. Sửa Sidebar đọc avatar từ AuthContext

File cần kiểm tra:

```text
frontend/pems-react/src/components/dashboard/Sidebar.tsx
```

Logic mong muốn:

```tsx
const { currentUser } = useAuth();

const avatarSrc = currentUser?.avatarUrl
  ? resolveFileUrl(currentUser.avatarUrl)
  : DEFAULT_AVATAR;
```

Render avatar:

```tsx
<img
  src={avatarSrc}
  alt={currentUser?.fullName || "User avatar"}
  className="h-full w-full rounded-full object-cover"
  onError={(event) => {
    event.currentTarget.src = DEFAULT_AVATAR;
  }}
/>
```

Yêu cầu:

```text
- Không hardcode avatar default nếu currentUser.avatarUrl tồn tại.
- Không gọi API profile riêng trong Sidebar nếu AuthContext đã có user.
- Không phá card user hiện tại ở cuối sidebar.
```

Nếu Sidebar nhận user qua props, kiểm tra layout cha. Có thể cập nhật props từ AuthContext ở layout cha, nhưng không nên tạo nguồn user thứ hai.

---

## 11. Sửa Header trang chủ đọc avatar từ AuthContext

File cần kiểm tra:

```text
frontend/pems-react/src/components/layout/Header.tsx
```

Logic mong muốn:

```tsx
const { currentUser } = useAuth();

const avatarSrc = currentUser?.avatarUrl
  ? resolveFileUrl(currentUser.avatarUrl)
  : DEFAULT_AVATAR;
```

Render avatar ở góc phải header:

```tsx
<img
  src={avatarSrc}
  alt={currentUser?.fullName || "User avatar"}
  className="h-8 w-8 rounded-full object-cover"
  onError={(event) => {
    event.currentTarget.src = DEFAULT_AVATAR;
  }}
/>
```

Yêu cầu:

```text
- Header trang chủ dùng cùng AuthContext với sidebar.
- Không đọc localStorage trực tiếp nếu AuthContext đã có currentUser.
- Nếu Header đang đọc localStorage trực tiếp, cần sửa để dùng AuthContext hoặc đảm bảo AuthContext update localStorage đúng key.
```

---

## 12. Tránh cache ảnh cũ

Thông thường avatar URL mới sẽ đổi fileId:

```text
Cũ: /api/files/421/content
Mới: /api/files/422/content
```

nên browser tự load ảnh mới.

Nếu sau upload vẫn hiển thị ảnh cũ do cache, có thể thêm cache buster ở tầng display:

```ts
const avatarSrc = `${resolveFileUrl(currentUser.avatarUrl)}?v=${currentUser.updatedAt ?? ""}`;
```

Hoặc sau upload:

```ts
updateCurrentUser({
  avatarUrl: nextAvatarUrl,
  avatarVersion: Date.now(),
});
```

Lưu ý:

```text
- Không lưu ?v=... vào DB.
- Không bắt backend lưu avatarVersion nếu DB chưa có.
- Chỉ dùng cache buster ở frontend nếu thật sự gặp cache.
```

Với trường hợp URL đổi fileId, chưa cần cache buster.

---

## 13. Click logo sidebar về trang chủ

File cần sửa:

```text
frontend/pems-react/src/components/dashboard/Sidebar.tsx
```

Tìm phần logo ở đầu sidebar.

Nếu hiện tại là:

```tsx
<div>
  <img src={logo} alt="FPT University" />
</div>
```

Sửa thành:

```tsx
import { Link } from "react-router-dom";

<Link
  to="/"
  className="block cursor-pointer"
  aria-label="Về trang chủ"
  title="Về trang chủ"
>
  <img src={logo} alt="FPT University" />
</Link>
```

Nếu project có route constants:

```tsx
<Link to={APP_ROUTES.HOME}>
  ...
</Link>
```

Yêu cầu:

```text
- Click logo chuyển về trang chủ public "/".
- Không logout user.
- Không navigate về /dashboard.
- Không làm mất style logo.
- Không ảnh hưởng active menu dashboard.
```

Nếu route trang chủ không phải `/`, dùng đúng route đang khai báo trong `appRoutes.ts` hoặc `App.tsx`.

---

## 14. Backend chỉ sửa nếu thiếu avatarUrl trong response

Nếu API upload avatar đã trả:

```json
{
  "fileId": 422,
  "avatarUrl": "/api/files/422/content",
  "webViewUrl": "...",
  "thumbnailUrl": "..."
}
```

thì không cần sửa backend.

Nếu chưa trả `avatarUrl`, bổ sung response để frontend update AuthContext.

Không sửa lại logic upload Google Drive nếu đang chạy.

---

## 15. Test checklist

### 15.1. Test avatar đồng bộ

```text
[ ] Login bằng tài khoản Staff Leader.
[ ] Mở dashboard, nhìn avatar ở sidebar.
[ ] Mở trang chủ, nhìn avatar ở header.
[ ] Vào Profile.
[ ] Upload avatar mới.
[ ] Upload thành công.
[ ] Avatar ở Profile đổi ngay.
[ ] Avatar ở Sidebar đổi ngay, không cần logout/login.
[ ] Avatar ở Header trang chủ đổi ngay khi quay về trang chủ.
[ ] Refresh trang, avatar mới vẫn hiển thị.
```

### 15.2. Test fallback

```text
[ ] Nếu avatarUrl null → hiển thị avatar mặc định.
[ ] Nếu /api/files/{id}/content lỗi → fallback avatar mặc định.
[ ] Không bị crash UI khi ảnh lỗi.
```

### 15.3. Test logo sidebar

```text
[ ] Click logo FPT University trong sidebar dashboard.
[ ] Điều hướng về trang chủ "/".
[ ] User vẫn đang đăng nhập.
[ ] Header trang chủ vẫn hiển thị user/avatar.
[ ] Quay lại dashboard menu vẫn hoạt động bình thường.
```

### 15.4. Test build

```text
[ ] npm run build frontend thành công.
[ ] Không lỗi TypeScript.
[ ] Không lỗi import unused hoặc missing import.
```

---

## 16. Acceptance Criteria

```text
AC-01: Upload avatar thành công thì AuthContext/currentUser được cập nhật avatarUrl mới.
AC-02: Sidebar dashboard hiển thị avatar mới ngay sau upload.
AC-03: Header trang chủ hiển thị avatar mới ngay sau upload.
AC-04: Refresh trang vẫn hiển thị avatar mới.
AC-05: Avatar lỗi thì fallback về avatar mặc định.
AC-06: Click logo trong sidebar dashboard điều hướng về trang chủ "/".
AC-07: Không sửa role/permission/menu visibility.
AC-08: Không rewrite layout.
AC-09: Không thêm thư viện mới.
AC-10: Frontend build TypeScript thành công.
```

---

## 17. Prompt ngắn cho AI Agent

```text
Bổ sung đồng bộ avatar sau upload profile và click logo sidebar về trang chủ.

Bối cảnh:
- Backend upload avatar lên Google Drive đã chạy.
- DB lưu users.avatar_url dạng /api/files/{fileId}/content.
- API upload avatar trả về avatarUrl mới.
- Hiện avatar ở sidebar dashboard và header trang chủ chưa cập nhật ngay sau upload.
- Logo trong sidebar dashboard cần click về trang chủ.

Yêu cầu:
1. Kiểm tra AuthContext/currentUser.
2. Nếu chưa có, bổ sung hàm updateCurrentUser(patch) để cập nhật currentUser và localStorage/sessionStorage đúng key hiện tại.
3. Sau khi upload avatar thành công ở Profile, gọi updateCurrentUser({ avatarUrl: result.avatarUrl }).
4. Profile vẫn cập nhật avatar state như hiện tại.
5. Sidebar dashboard phải đọc avatar từ AuthContext/currentUser.avatarUrl, không hardcode riêng.
6. Header trang chủ phải đọc avatar từ AuthContext/currentUser.avatarUrl, không hardcode riêng.
7. Nếu avatarUrl là /api/files/{id}/content, dùng helper resolve URL hoặc cơ chế hiện có để ghép đúng backend base URL.
8. Thêm fallback avatar default khi ảnh lỗi.
9. Bọc logo trong Sidebar bằng Link hoặc navigate để click về trang chủ "/".
10. Không sửa role/permission/menu visibility.
11. Không rewrite layout, không thêm thư viện mới.
12. Build frontend sau khi sửa.

Kết quả mong muốn:
- Upload avatar xong thì Profile, Sidebar, Header đều đổi ảnh ngay.
- Refresh trang avatar mới vẫn còn.
- Click logo sidebar về trang chủ.
```
