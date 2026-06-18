# PROMPT TRIỂN KHAI ROLE-BASED ROUTING, MENU RBAC & CHỐNG DASHBOARD TRẮNG CHO PEMS

> **Mục tiêu của prompt:** Dùng cho bất kỳ AI coding assistant nào đọc vào cũng hiểu rõ phải kiểm tra gì, sửa gì, không được sửa gì, và nghiệm thu như thế nào.  
> **Bối cảnh lỗi:** Sau khi login thành công, frontend redirect về `localhost:3000/dashboard` nhưng màn hình trắng. Nguyên nhân khả năng cao là `/dashboard` đang được dùng chung cho mọi role, trong khi dashboard thống kê chỉ dành cho một số role có quyền `UC-69 View Dashboard Statistics`.  
> **Phạm vi chính:** Frontend React/TypeScript. Chỉ đụng backend nếu AuthResponse/GetCurrentUser thiếu dữ liệu cần thiết cho phân quyền.

---

## 0. VAI TRÒ CỦA BẠN

Bạn là:

- **Senior Frontend Engineer**
- **Full-stack RBAC Reviewer**
- **Security-focused React Architect**
- **Production Bug Fixer**

Bạn đang sửa dự án **PEMS — Partnership Engagement Management System**.

Nhiệm vụ của bạn là sửa luồng điều hướng sau đăng nhập, route guard, menu theo role/permission, và xử lý fallback để hệ thống **không còn trắng màn hình** sau khi login.

---

## 1. TÀI LIỆU BẮT BUỘC PHẢI ĐỌC TRƯỚC KHI CODE

Trước khi sửa bất kỳ file nào, phải đọc và đối chiếu các tài liệu sau trong repo:

```text
docs/ hoặc root docs:
- PROJECT_STRUCTURE_FULL.md
- AUTHENTICATION_FLOW_REPORT.md
- PERMISSION_MATRIX.md
- PERMISSION_RULES.md
- USE_CASE_LIST.md
- USE_CASE_NOTES.md
- DATABASE_SCHEMA.md
- CLEAN_ARCHITECTURE.md
```

Không được bắt đầu code nếu chưa hiểu các điểm sau:

1. Hệ thống có 6 `role_code` gốc:
   - `ADMIN`
   - `HO`
   - `STAFF`
   - `DEPT`
   - `STUDENT`
   - `VISITOR`

2. `STAFF` và `DEPT` bắt buộc có `sub_role`:
   - `Leader`
   - `Staff`

3. Frontend phải phân giải ra **effectiveRole** để hiển thị đúng workspace.

4. Backend vẫn là lớp kiểm tra quyền cuối cùng. Frontend chỉ dùng role/permission để:
   - Redirect đúng màn.
   - Ẩn/hiện menu.
   - Ẩn/hiện button/action.
   - Tránh gọi API không có quyền.
   - Tránh crash/trắng màn hình.

---

## 2. NGUYÊN NHÂN LỖI CẦN XỬ LÝ

Hiện tượng:

```text
Sau khi login thành công:
URL: http://localhost:3000/dashboard
Màn hình trắng.
```

Giả thuyết chính cần kiểm tra:

1. Login đã thành công nhưng frontend redirect cứng mọi role về `/dashboard`.
2. Component `/dashboard` có thể đang gọi API thống kê `UC-69`.
3. Trong permission matrix, `UC-69 View Dashboard Statistics` không cấp cho mọi role.
4. Khi user không có quyền hoặc API nghiệp vụ chưa implement, component bị throw error nhưng không có fallback.
5. AuthContext có thể chưa load xong user/permissions nhưng route đã render.
6. ProtectedRoute có thể chưa check role/permission rõ ràng.
7. Layout/menu có thể render item không đúng quyền.
8. Có thể có lỗi runtime do `user.role`, `permissions`, `subRole` bị `undefined`.

Mục tiêu sửa:

```text
Không role nào sau login được vào màn trắng.
Mọi role phải được đưa về đúng landing page.
Nếu page/API chưa implement thì hiện Under Development, không crash.
Nếu thiếu quyền thì hiện Not Authorized, không crash.
```

---

## 3. QUY TẮC CẤM TUYỆT ĐỐI

### 3.1. Không được phá auth đang chạy

Không code lại login từ đầu.

Không đổi logic bảo mật backend đang hoạt động nếu không cần.

Không xóa:

- JWT logic
- Refresh token logic
- Session validation
- AuthContext đang dùng được
- Axios interceptor đang refresh token
- Existing ProtectedRoute nếu chỉ cần nâng cấp

### 3.2. Không được cấp quyền ngầm định

Không được viết kiểu:

```ts
if (user.roleCode === "ADMIN") return true;
```

với mọi thứ.

Lý do: `ADMIN` trong PEMS là admin kỹ thuật, không phải super admin nghiệp vụ.

### 3.3. Không được coi `F` là toàn quyền toàn hệ thống

`F` chỉ là full permission cho **UC cụ thể**.

Ví dụ:

```text
F ở UC-119 Configure Role Permissions
không có nghĩa là được xem Dashboard UC-69.
```

### 3.4. Không được coi permission level là thang kế thừa tự động

Không mặc định:

```text
F > E > R > O
```

trừ khi route/action đó khai báo rõ `acceptedLevels`.

Ví dụ:

- View page có thể chấp nhận `R`.
- Edit button có thể chỉ chấp nhận `E` hoặc `F`.
- Own page chỉ chấp nhận `O` nếu dữ liệu là của chính user.
- `R` tuyệt đối không được dùng để cho phép chỉnh sửa.

### 3.5. Không được chỉ ẩn bằng CSS

Sai:

```tsx
<div style={{ display: canAccess ? "block" : "none" }}>
```

Đúng:

```tsx
return canAccess ? <MenuItem /> : null;
```

### 3.6. Không được để API lỗi làm chết React tree

Các lỗi sau phải có fallback UI:

- `401 Unauthorized`
- `403 Forbidden`
- `404 Not Found`
- `500 Internal Server Error`
- `NotImplementedException`
- Network error
- Token expired
- Missing permission
- Missing role/subRole
- Missing user in storage

### 3.7. Không được tạo route/page/menu trùng bừa bãi

Trước khi tạo file mới phải tìm file cũ tương ứng.

Chỉ tạo mới khi thật sự chưa có.

---

## 4. ĐỊNH NGHĨA ROLE CHUẨN

### 4.1. Raw role từ backend

Backend trả về user có thể gồm:

```ts
type RawUser = {
  userId: string;
  fullName: string;
  email: string;
  roleCode: "ADMIN" | "HO" | "STAFF" | "DEPT" | "STUDENT" | "VISITOR";
  subRole?: "Leader" | "Staff" | null;
  primaryCampusId?: string | null;
  departmentId?: string | null;
  mustChangePassword?: boolean;
};
```

Tên field thực tế có thể là:

```text
roleCode / role_code / RoleCode
subRole / sub_role / SubRole
mustChangePassword / MustChangePassword
```

Bạn phải kiểm tra response thật và mapper hiện tại, không được đoán.

### 4.2. Effective role chuẩn frontend

Tạo enum/type thống nhất:

```ts
export type EffectiveRole =
  | "ADMIN"
  | "HO"
  | "STAFF_LEADER"
  | "STAFF"
  | "DEPARTMENT_LEAD"
  | "DEPARTMENT"
  | "STUDENT"
  | "VISITOR";
```

### 4.3. Hàm resolve effective role

Tạo hoặc cập nhật file:

```text
src/auth/resolveEffectiveRole.ts
hoặc
src/utils/auth/resolveEffectiveRole.ts
hoặc vị trí phù hợp với project hiện tại
```

Code yêu cầu:

```ts
export function resolveEffectiveRole(user: AuthUser | null | undefined): EffectiveRole | null {
  if (!user) return null;

  const roleCode = normalizeRoleCode(user.roleCode);
  const subRole = normalizeSubRole(user.subRole);

  switch (roleCode) {
    case "ADMIN":
      return "ADMIN";

    case "HO":
      return "HO";

    case "STAFF":
      if (subRole === "Leader") return "STAFF_LEADER";
      if (subRole === "Staff") return "STAFF";
      return null;

    case "DEPT":
      if (subRole === "Leader") return "DEPARTMENT_LEAD";
      if (subRole === "Staff") return "DEPARTMENT";
      return null;

    case "STUDENT":
      return "STUDENT";

    case "VISITOR":
      return "VISITOR";

    default:
      return null;
  }
}
```

Rule bắt buộc:

```text
Nếu roleCode = STAFF hoặc DEPT mà subRole bị thiếu:
- Không tự đoán là Staff.
- Không tự nâng thành Leader.
- Không cho vào dashboard.
- Redirect đến /invalid-account hoặc /unauthorized.
```

---

## 5. PERMISSION MODEL CHUẨN FRONTEND

### 5.1. Permission DTO

Frontend phải chuẩn hóa permission từ backend về dạng:

```ts
export type PermissionLevel = "F" | "E" | "R" | "O";

export type UserPermission = {
  permissionCode: string;
  permissionLevel: PermissionLevel;
  permissionGroup?: string;
};
```

Không được phụ thuộc vào case field tùy tiện. Nếu backend trả `permission_code`, cần map sang `permissionCode`.

### 5.2. Hàm kiểm tra permission

Tạo hook hoặc utility:

```ts
hasPermission(
  permissionCode: string,
  acceptedLevels?: PermissionLevel[]
): boolean
```

Quy tắc:

1. Nếu chưa login: `false`.
2. Nếu permissions chưa load: `false`.
3. Nếu permissionCode không tồn tại: `false`.
4. Nếu `acceptedLevels` không truyền:
   - Không tự cho qua bừa bãi.
   - Nên mặc định theo route/action config.
5. Nếu `acceptedLevels` có truyền:
   - Chỉ true nếu permission level của user nằm trong danh sách đó.

Ví dụ:

```ts
hasPermission("UC-69.VIEW_DASHBOARD_STATISTICS", ["R"]);
hasPermission("UC-70.EXPORT_STATISTICS_REPORT", ["E"]);
hasPermission("UC-15.UPDATE_PROFILE", ["O"]);
```

### 5.3. Cảnh báo về permission code

Trong tài liệu có thể ghi UC dạng:

```text
UC-69
UC-69 View Dashboard Statistics
UC-69.VIEW_DASHBOARD_STATISTICS
```

Nhưng database/backend có thể trả permission code dạng khác.

Bạn phải làm việc theo thứ tự:

1. Kiểm tra permission code thật backend trả trong AuthResponse hoặc `/auth/me/permissions`.
2. Kiểm tra constants backend nếu có:
   - `PermissionConstants.cs`
   - `AuthConstants.cs`
   - seed SQL permissions
3. Frontend route config phải dùng đúng code thực tế.
4. Nếu phát hiện lệch chuẩn permission code:
   - Không tự hack bằng wildcard nguy hiểm.
   - Tạo báo cáo rõ code nào lệch.
   - Chỉ tạo alias tạm nếu thật sự cần để unblock dev, và phải comment rõ `TODO: remove after permission seed normalized`.

---

## 6. AUTHCONTEXT BẮT BUỘC CÓ GÌ

Tìm AuthContext hiện tại, sau đó cập nhật để cung cấp ít nhất các giá trị sau:

```ts
type AuthContextValue = {
  user: AuthUser | null;
  effectiveRole: EffectiveRole | null;
  permissions: UserPermission[];

  accessToken: string | null;
  refreshToken?: string | null;

  isAuthenticated: boolean;
  isLoading: boolean;
  isReady: boolean;

  mustChangePassword: boolean;

  loginWithCredentials: (...args) => Promise<void>;
  loginWithSso: (...args) => Promise<void>;
  logout: () => Promise<void> | void;

  hasPermission: (permissionCode: string, acceptedLevels: PermissionLevel[]) => boolean;
  hasAnyPermission: (rules: PermissionRequirement[]) => boolean;
  hasAllPermissions: (rules: PermissionRequirement[]) => boolean;
  hasEffectiveRole: (roles: EffectiveRole[]) => boolean;
};
```

### 6.1. Auth init flow khi reload app

Khi user reload trang:

1. Đọc token từ storage.
2. Nếu không có token:
   - `isLoading = false`
   - `isAuthenticated = false`
3. Nếu có token:
   - Gắn Authorization header.
   - Gọi `/auth/me` hoặc endpoint current user hiện có.
   - Gọi permissions nếu AuthResponse chưa có permissions.
   - Resolve effectiveRole.
   - Set state đầy đủ.
4. Trong lúc load:
   - Render `LoadingScreen`.
   - Không render route con trước khi auth ready.
5. Nếu token hết hạn:
   - Axios interceptor refresh token.
   - Nếu refresh fail: clear storage + redirect login.
6. Nếu user bị khóa/session revoked:
   - logout local
   - redirect login
   - hiện message rõ.

### 6.2. Sau login thành công

Không navigate cứng về `/dashboard`.

Flow chuẩn:

```ts
const result = await authService.login(payload);

setTokens(result.tokens);
setUser(result.user);
setPermissions(result.permissions);

const effectiveRole = resolveEffectiveRole(result.user);

if (!effectiveRole) {
  navigate("/invalid-account", { replace: true });
  return;
}

if (result.user.mustChangePassword) {
  navigate("/change-password", { replace: true });
  return;
}

const defaultRoute = getDefaultRouteByRole(effectiveRole, result.permissions);
navigate(defaultRoute, { replace: true });
```

---

## 7. DEFAULT ROUTE THEO ROLE

Tạo file:

```text
src/config/roleHomeRoutes.ts
```

Nội dung đề xuất:

```ts
export const ROLE_HOME_ROUTES: Record<EffectiveRole, string> = {
  ADMIN: "/admin/system",
  HO: "/ho/dashboard",
  STAFF_LEADER: "/staff-leader/dashboard",
  STAFF: "/staff/delegations",
  DEPARTMENT_LEAD: "/department-lead/dashboard",
  DEPARTMENT: "/department/tasks",
  STUDENT: "/student/my-work",
  VISITOR: "/visitor/visit-requests",
};
```

### 7.1. Nguyên tắc chọn default route

1. Không chọn route gọi API chưa implement nếu có route ổn định hơn.
2. Không đưa role không có `UC-69` vào dashboard statistics.
3. Nếu route chưa có page thật:
   - Tạo placeholder page sạch.
   - Không gọi API.
   - Không để màn trắng.

### 7.2. Nội dung placeholder page

Tạo component dùng chung:

```text
FeaturePlaceholderPage
```

Hiển thị:

```text
Tính năng đang được triển khai.
Bạn đã đăng nhập với vai trò: {effectiveRole}.
Các chức năng khả dụng sẽ hiển thị ở menu bên trái.
```

Có nút:

```text
Về trang chính của tôi
Đăng xuất
```

---

## 8. SỬA `/dashboard`

### 8.1. Không render dashboard thống kê trực tiếp ở `/dashboard`

`/dashboard` phải trở thành route trung gian:

```tsx
<Route
  path="/dashboard"
  element={
    <ProtectedRoute>
      <RoleBasedDashboardRedirect />
    </ProtectedRoute>
  }
/>
```

### 8.2. RoleBasedDashboardRedirect

Tạo component:

```text
src/routes/RoleBasedDashboardRedirect.tsx
hoặc vị trí phù hợp
```

Logic:

```tsx
function RoleBasedDashboardRedirect() {
  const {
    isLoading,
    isAuthenticated,
    user,
    effectiveRole,
    permissions,
    mustChangePassword,
  } = useAuth();

  if (isLoading) return <LoadingScreen />;

  if (!isAuthenticated) {
    return <Navigate to="/login" replace />;
  }

  if (mustChangePassword) {
    return <Navigate to="/change-password" replace />;
  }

  if (!effectiveRole) {
    return <Navigate to="/invalid-account" replace />;
  }

  const target = getDefaultRouteByRole(effectiveRole, permissions);

  return <Navigate to={target} replace />;
}
```

### 8.3. Route nào được render DashboardStatistics?

Chỉ các route sau được phép render dashboard thống kê:

```text
/ho/dashboard
/staff-leader/dashboard
/department-lead/dashboard
```

Và vẫn phải check quyền:

```text
UC-69 View Dashboard Statistics
acceptedLevels: ["R"]
```

Nếu user không có quyền `UC-69`:

```text
Không gọi API statistics.
Render NotAuthorized.
```

---

## 9. PROTECTED ROUTE CHUẨN

Tạo hoặc nâng cấp `ProtectedRoute`.

Props cần hỗ trợ:

```ts
type PermissionRequirement = {
  permissionCode: string;
  acceptedLevels: PermissionLevel[];
};

type ProtectedRouteProps = {
  children: React.ReactNode;

  allowedRoles?: EffectiveRole[];

  requiredAllPermissions?: PermissionRequirement[];
  requiredAnyPermissions?: PermissionRequirement[];

  allowWhen?: (ctx: AuthContextValue) => boolean;

  fallbackPath?: string;
  showNotAuthorized?: boolean;
};
```

Logic bắt buộc:

```text
1. Nếu auth đang loading:
   -> LoadingScreen

2. Nếu chưa authenticated:
   -> Navigate /login

3. Nếu user.mustChangePassword = true
   và route hiện tại không phải /change-password:
   -> Navigate /change-password

4. Nếu không resolve được effectiveRole:
   -> Navigate /invalid-account

5. Nếu allowedRoles có khai báo:
   - effectiveRole phải nằm trong allowedRoles
   - nếu không: NotAuthorized

6. Nếu requiredAllPermissions có khai báo:
   - user phải có tất cả quyền được yêu cầu
   - nếu không: NotAuthorized

7. Nếu requiredAnyPermissions có khai báo:
   - user chỉ cần có một quyền hợp lệ
   - nếu không: NotAuthorized

8. Nếu pass hết:
   -> render children
```

### 9.1. Không được throw trong ProtectedRoute

Sai:

```tsx
throw new Error("Forbidden");
```

Đúng:

```tsx
return <NotAuthorizedPage />;
```

---

## 10. ROUTE CONFIG TẬP TRUNG

Không rải role/permission khắp component.

Tạo file:

```text
src/config/routeConfig.tsx
```

Mỗi route cần có metadata:

```ts
export type AppRouteConfig = {
  path: string;
  element: React.ReactNode;
  layout?: "public" | "auth" | "app" | "admin";
  allowedRoles?: EffectiveRole[];
  requiredAnyPermissions?: PermissionRequirement[];
  requiredAllPermissions?: PermissionRequirement[];
  title?: string;
  isImplemented?: boolean;
};
```

Ví dụ:

```ts
{
  path: "/ho/dashboard",
  element: <DashboardStatisticsPage />,
  layout: "app",
  allowedRoles: ["HO"],
  requiredAnyPermissions: [
    {
      permissionCode: PERMISSIONS.VIEW_DASHBOARD_STATISTICS,
      acceptedLevels: ["R"],
    },
  ],
  title: "Dashboard HO",
  isImplemented: true,
}
```

Nếu page chưa implement:

```ts
{
  path: "/staff/delegations",
  element: <FeaturePlaceholderPage featureName="Delegations" />,
  layout: "app",
  allowedRoles: ["STAFF"],
  requiredAnyPermissions: [
    {
      permissionCode: PERMISSIONS.VIEW_GUEST_DELEGATION_LIST,
      acceptedLevels: ["R"],
    },
  ],
  title: "Delegations",
  isImplemented: false,
}
```

---

## 11. MENU CONFIG TẬP TRUNG

Tạo file:

```text
src/config/menuConfig.ts
```

Mỗi menu item:

```ts
export type MenuItemConfig = {
  key: string;
  label: string;
  path?: string;
  icon?: React.ReactNode;

  allowedRoles?: EffectiveRole[];
  requiredAnyPermissions?: PermissionRequirement[];
  requiredAllPermissions?: PermissionRequirement[];

  children?: MenuItemConfig[];
};
```

Render menu qua hàm:

```ts
filterMenuByAuth(menuConfig, authContext)
```

Rule:

1. Nếu role không hợp lệ: không render.
2. Nếu thiếu permission: không render.
3. Nếu parent không có child nào hợp lệ: không render parent.
4. Profile/change password/logout là menu cá nhân, chỉ cần authenticated.
5. Không render menu chỉ bằng CSS.

---

## 12. MENU GỢI Ý THEO ROLE

### 12.1. ADMIN

Default route:

```text
/admin/system
```

Menu:

```text
- Admin Home / System
- Role & Permissions
  - View Role List
  - Configure Role Permissions
  - Update Role Details
- API Configuration
  - View API Configuration
  - Create API Configuration
  - Update API Configuration
  - Test API Connection
  - Manage API Status
- API Logs
```

Không đưa Admin vào dashboard thống kê nếu không có `UC-69`.

### 12.2. HO

Default route:

```text
/ho/dashboard
```

Menu:

```text
- Dashboard Statistics
- Reports
- Campuses
- Delegations Read-only / Cross-campus requests
- FAQ
- Agenda Templates
- Feedback Summary
```

### 12.3. STAFF_LEADER

Default route:

```text
/staff-leader/dashboard
```

Menu:

```text
- Campus Dashboard
- Visit Request Approval
- Delegations
- Accounts
- Departments
- Gallery Management
- News Approval
- Reports
```

### 12.4. STAFF

Default route:

```text
/staff/delegations
```

Menu:

```text
- Delegations
- Visit Logistics
- Partners
- Documents
- Meeting Minutes
- News
- Gallery / Upload Photos
- Calendar
```

Không redirect Staff thường vào dashboard statistics nếu không có `UC-69`.

### 12.5. DEPARTMENT_LEAD

Default route:

```text
/department-lead/dashboard
```

Menu:

```text
- Department Dashboard
- Resource Requests
- Assign Tasks
- Coordination Tasks
- Personnel
- Department Calendar
```

### 12.6. DEPARTMENT

Default route:

```text
/department/tasks
```

Menu:

```text
- My Tasks
- Confirm Participation
- Coordination Tasks
- Calendar
```

### 12.7. STUDENT

Default route:

```text
/student/my-work
```

Menu:

```text
- My Work
- My Delegations
- Confirm Participation
- Meeting Minutes nếu được giao
- Upload Visit Photos nếu có quyền
- Create News nếu có quyền
```

### 12.8. VISITOR

Default route:

```text
/visitor/visit-requests
```

Menu:

```text
- Submit Visit Request
- My Visit Requests
- My Delegations
- Notifications
- Profile
```

Visitor chỉ thao tác dữ liệu của chính họ nếu permission level là `O`.

---

## 13. ERROR BOUNDARY BẮT BUỘC

Tạo hoặc cập nhật:

```text
src/components/ErrorBoundary.tsx
```

Bọc App hoặc AppLayout:

```tsx
<ErrorBoundary>
  <App />
</ErrorBoundary>
```

Fallback UI phải có:

```text
Đã xảy ra lỗi khi hiển thị màn hình.
Không phải lỗi đăng nhập.
Bạn có thể quay lại trang chính hoặc đăng xuất.
```

Buttons:

```text
- Về trang chính của tôi
- Đăng xuất
- Tải lại trang
```

Không để stack trace lộ ra production UI.

Trong dev mode có thể `console.error(error)`.

---

## 14. XỬ LÝ API ERROR CHUẨN

### 14.1. Axios interceptor

Kiểm tra `httpClient.ts`.

Yêu cầu:

1. Request interceptor gắn `Authorization: Bearer <accessToken>`.
2. Response interceptor:
   - `401`: refresh token single-flight.
   - refresh thành công: retry request cũ.
   - refresh fail: clear auth + redirect login.
   - `403`: không retry vô hạn, trả lỗi để UI render NotAuthorized.
   - `500`: trả lỗi để UI render ErrorState/UnderDevelopment.
3. Không tạo vòng lặp refresh vô hạn.

### 14.2. Page-level API state

Mỗi page gọi API phải có state:

```ts
type AsyncState<T> =
  | { status: "idle" }
  | { status: "loading" }
  | { status: "success"; data: T }
  | { status: "forbidden" }
  | { status: "notFound" }
  | { status: "underDevelopment" }
  | { status: "error"; message: string };
```

Nếu API trả lỗi NotImplementedException hoặc 500 từ handler scaffold:

```tsx
return <FeaturePlaceholderPage featureName="..." reason="API chưa triển khai" />;
```

---

## 15. DASHBOARD STATISTICS PAGE RULE

Component `DashboardStatisticsPage` phải làm đúng:

```text
1. Check permission UC-69 trước.
2. Nếu thiếu quyền:
   - Không gọi API.
   - Render NotAuthorized.
3. Nếu có quyền:
   - Gọi API statistics.
4. Nếu API loading:
   - LoadingState.
5. Nếu API 403:
   - NotAuthorized.
6. Nếu API 500/NotImplemented:
   - FeatureUnderDevelopment.
7. Nếu data rỗng:
   - EmptyState.
8. Nếu data có:
   - Render dashboard.
```

Không được:

```tsx
const data = await api.getDashboard();
return <Chart data={data.items.map(...)} />;
```

mà không check null/error.

---

## 16. BACKEND CHỈNH NHẸ NẾU THIẾU DỮ LIỆU

Chỉ sửa backend nếu frontend không nhận đủ:

```text
- roleCode
- subRole
- permissions
- permissionLevel
- mustChangePassword
- primaryCampusId
- departmentId
```

Nếu phải sửa backend:

1. Không tạo handler mới trùng.
2. Sửa mapper/response hiện có:
   - `AuthResponse`
   - `AuthUserDto`
   - `UserPermissionDto`
   - `GetCurrentUserQueryHandler`
   - `GetCurrentUserPermissionsQueryHandler`
3. Không phá login/session/refresh token.
4. Không hard-code permission trong backend.
5. Permission vẫn lấy từ DB `roles`, `permissions`, `role_permissions`.

---

## 17. ROUTE MAPPING TỐI THIỂU PHẢI CÓ

Tạo tối thiểu các route sau, kể cả chỉ là placeholder:

```text
/login
/change-password
/unauthorized
/invalid-account
/dashboard

/admin/system
/ho/dashboard
/staff-leader/dashboard
/staff/delegations
/department-lead/dashboard
/department/tasks
/student/my-work
/visitor/visit-requests

/profile
```

Route `/dashboard` chỉ redirect, không render dashboard thật.

---

## 18. ACCEPTANCE TEST BẮT BUỘC

Test thủ công bằng các loại account sau.

### 18.1. ADMIN

Kỳ vọng:

```text
Login thành công.
Không vào /dashboard trắng.
Không vào /ho/dashboard.
Redirect đến /admin/system.
Không thấy menu Dashboard Statistics nếu không có UC-69.
Thấy menu Role/Permission/API nếu có quyền tương ứng.
```

### 18.2. HO

Kỳ vọng:

```text
Login thành công.
Redirect đến /ho/dashboard.
Nếu có UC-69: render dashboard hoặc UnderDevelopment nếu API chưa xong.
Nếu thiếu UC-69: NotAuthorized, không trắng.
```

### 18.3. STAFF Leader

Kỳ vọng:

```text
roleCode = STAFF
subRole = Leader
effectiveRole = STAFF_LEADER
Redirect đến /staff-leader/dashboard.
Menu Staff Leader đúng quyền.
```

### 18.4. STAFF thường

Kỳ vọng:

```text
roleCode = STAFF
subRole = Staff
effectiveRole = STAFF
Redirect đến /staff/delegations.
Không gọi API dashboard statistics nếu không có UC-69.
Không thấy menu dashboard statistics nếu không có quyền.
```

### 18.5. DEPT Leader

Kỳ vọng:

```text
roleCode = DEPT
subRole = Leader
effectiveRole = DEPARTMENT_LEAD
Redirect đến /department-lead/dashboard hoặc fallback hợp lệ.
```

### 18.6. DEPT Staff

Kỳ vọng:

```text
roleCode = DEPT
subRole = Staff
effectiveRole = DEPARTMENT
Redirect đến /department/tasks.
```

### 18.7. STUDENT

Kỳ vọng:

```text
roleCode = STUDENT
effectiveRole = STUDENT
Redirect đến /student/my-work.
Không thấy menu không có quyền.
```

### 18.8. VISITOR

Kỳ vọng:

```text
roleCode = VISITOR
effectiveRole = VISITOR
Redirect đến /visitor/visit-requests.
Chỉ thấy dữ liệu/menu thuộc phạm vi visitor.
```

### 18.9. STAFF/DEPT thiếu subRole

Kỳ vọng:

```text
Không cấp quyền ngầm định.
Redirect /invalid-account.
Hiển thị lỗi cấu hình tài khoản.
Không trắng màn hình.
```

### 18.10. Gọi thẳng route không có quyền

Ví dụ:

```text
Staff thường truy cập /admin/system
Visitor truy cập /ho/dashboard
Admin truy cập /staff/delegations nếu không có quyền
```

Kỳ vọng:

```text
Hiển thị NotAuthorized.
Không redirect loop.
Không trắng màn hình.
```

### 18.11. API lỗi

Test:

```text
API trả 401
API trả 403
API trả 500
API chưa implement
Network error
```

Kỳ vọng:

```text
401: refresh hoặc logout.
403: NotAuthorized.
500/NotImplemented: ErrorState hoặc UnderDevelopment.
Network: ErrorState.
Không trắng màn hình.
```

---

## 19. OUTPUT SAU KHI CODE XONG

Sau khi sửa, phải trả báo cáo gồm:

```text
1. Danh sách file đã đọc.
2. Danh sách file đã sửa.
3. Danh sách file tạo mới.
4. Root cause thật sự của màn trắng.
5. Luồng redirect sau login mới.
6. Bảng roleCode/subRole -> effectiveRole -> defaultRoute.
7. Bảng route chính -> allowedRoles -> requiredPermissions.
8. Cách xử lý 401/403/500.
9. Cách chống trắng màn hình.
10. Kết quả test từng role.
11. Những API/page còn placeholder do backend chưa implement.
12. Những permission code bị lệch nếu phát hiện.
```

---

## 20. CHECKLIST CODE REVIEW

Trước khi kết thúc, tự kiểm tra:

```text
[ ] Không còn navigate cứng mọi role về /dashboard sau login.
[ ] /dashboard chỉ còn là RoleBasedDashboardRedirect.
[ ] Có resolveEffectiveRole.
[ ] STAFF/DEPT thiếu subRole không được cấp quyền.
[ ] ProtectedRoute check được role.
[ ] ProtectedRoute check được requiredAnyPermissions.
[ ] ProtectedRoute check được requiredAllPermissions.
[ ] Menu render theo role + permission.
[ ] Button/action render theo permission.
[ ] DashboardStatistics check UC-69 trước khi gọi API.
[ ] API 403 không làm trắng màn hình.
[ ] API 500/NotImplemented không làm trắng màn hình.
[ ] Có LoadingScreen khi AuthContext đang init.
[ ] Có NotAuthorizedPage.
[ ] Có InvalidAccountPage.
[ ] Có FeaturePlaceholderPage.
[ ] Có ErrorBoundary.
[ ] Không hard-code Admin là toàn quyền.
[ ] Không coi F là toàn quyền toàn hệ thống.
[ ] Không dùng R để edit.
[ ] Không lộ stack trace trên UI production.
[ ] Không tạo file/module trùng không cần thiết.
```

---

## 21. GỢI Ý CẤU TRÚC FILE SAU KHI SỬA

Có thể dùng cấu trúc sau, nhưng phải ưu tiên cấu trúc thật hiện tại của project:

```text
src/
├── auth/
│   ├── AuthContext.tsx
│   ├── ProtectedRoute.tsx
│   ├── useAuth.ts
│   ├── usePermission.ts
│   └── resolveEffectiveRole.ts
│
├── config/
│   ├── permissions.ts
│   ├── roleHomeRoutes.ts
│   ├── routeConfig.tsx
│   └── menuConfig.tsx
│
├── routes/
│   └── RoleBasedDashboardRedirect.tsx
│
├── components/
│   ├── ErrorBoundary.tsx
│   ├── LoadingScreen.tsx
│   ├── NotAuthorizedPage.tsx
│   ├── InvalidAccountPage.tsx
│   ├── FeaturePlaceholderPage.tsx
│   └── ErrorState.tsx
│
├── layouts/
│   └── AppLayout.tsx
│
├── pages/
│   ├── admin/
│   │   └── AdminSystemPage.tsx
│   ├── ho/
│   │   └── HoDashboardPage.tsx
│   ├── staffLeader/
│   │   └── StaffLeaderDashboardPage.tsx
│   ├── staff/
│   │   └── StaffDelegationsPage.tsx
│   ├── departmentLead/
│   │   └── DepartmentLeadDashboardPage.tsx
│   ├── department/
│   │   └── DepartmentTasksPage.tsx
│   ├── student/
│   │   └── StudentMyWorkPage.tsx
│   └── visitor/
│       └── VisitorVisitRequestsPage.tsx
│
└── services/
    ├── authService.ts
    └── httpClient.ts
```

Không bắt buộc đúng y hệt, nhưng phải đạt cùng mục tiêu.

---

## 22. PHASE TRIỂN KHAI ĐỀ XUẤT

Làm theo thứ tự để tránh phá hệ thống.

### Phase 1 — Audit

```text
- Tìm login redirect hiện tại.
- Tìm route /dashboard hiện tại.
- Tìm AuthContext.
- Tìm ProtectedRoute.
- Tìm axios interceptor.
- Tìm menu/sidebar.
- Tìm DashboardStatistics component/API.
```

### Phase 2 — Core auth utilities

```text
- Thêm EffectiveRole type.
- Thêm resolveEffectiveRole.
- Chuẩn hóa permission DTO.
- Thêm hasPermission/hasAnyPermission/hasAllPermissions.
```

### Phase 3 — Route guard

```text
- Nâng cấp ProtectedRoute.
- Thêm LoadingScreen.
- Thêm NotAuthorizedPage.
- Thêm InvalidAccountPage.
```

### Phase 4 — Redirect sau login

```text
- Thêm ROLE_HOME_ROUTES.
- Thêm getDefaultRouteByRole.
- Sửa login success không navigate cứng /dashboard.
- Sửa /dashboard thành redirect.
```

### Phase 5 — Menu RBAC

```text
- Tạo menuConfig.
- Filter menu theo auth context.
- Không render menu không quyền.
```

### Phase 6 — Dashboard hardening

```text
- DashboardStatistics check UC-69 trước khi gọi API.
- Thêm fallback cho API lỗi.
```

### Phase 7 — Final test

```text
- Test 8 role.
- Test thiếu subRole.
- Test route không quyền.
- Test API lỗi.
- Ghi báo cáo.
```

---

## 23. KẾT LUẬN BẮT BUỘC

Kết quả cuối cùng phải đảm bảo:

```text
Login thành công không đồng nghĩa với vào /dashboard.
Login thành công phải vào đúng workspace theo effectiveRole.
Frontend chỉ hiển thị menu/action user có quyền.
Backend vẫn giữ vai trò kiểm tra quyền cuối cùng.
Không còn màn hình trắng khi thiếu quyền, thiếu route, API lỗi hoặc backend chưa implement.
```
