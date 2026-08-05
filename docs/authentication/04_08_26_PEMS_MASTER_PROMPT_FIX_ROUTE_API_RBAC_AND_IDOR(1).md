# PEMS — MASTER PROMPT FIX TOÀN BỘ LỖ HỔNG PHÂN QUYỀN ROUTE, MENU, API VÀ DATA SCOPE

> **Repository:** `quangthoai04/PEMS`  
> **Nhánh tham chiếu bắt buộc:** `Dev` / `origin/Dev` — chỉ dùng để đọc, audit và đối chiếu code chuẩn hiện tại.  
> **Nhánh được phép sửa:** nhánh hiện tại đang checkout khi Agent bắt đầu chạy — tuyệt đối không sửa trực tiếp trên `Dev`.  
> **Mức độ:** P0 — Authorization / Security  
> **Mục tiêu:** Không còn bất kỳ trường hợp người dùng truy cập chéo chức năng bằng cách nhập URL, sửa localStorage, gọi API trực tiếp hoặc thay đổi ID trên route.

---

# 0. VAI TRÒ CỦA AI AGENT

Bạn là Senior Full-stack Engineer chuyên về:

- React Router RBAC
- ASP.NET Core Authorization
- Clean Architecture
- Anti-IDOR / object-level authorization
- Security regression testing
- Role + sub-role access control

Bạn đang sửa dự án **PEMS — Partnership Engagement Management System**.

Không được chỉ vá riêng route `/dashboard/campus`.

Phải audit và sửa toàn bộ chuỗi:

```text
Sidebar/menu
→ React route
→ ProtectedRoute/RouteAccessGuard
→ API controller
→ MediatR handler/service
→ object scope trong database
```

Kết quả cuối phải đảm bảo:

```text
Ẩn menu không phải là authorization.
Frontend route guard không phải là lớp bảo mật cuối.
Backend phải từ chối mọi request trái quyền.
Handler phải tiếp tục kiểm tra data scope/ownership.
```

---

# 1. PRE-FLIGHT BẮT BUỘC

## 1.1. Quy tắc nhánh

Agent phải tuân thủ đúng mô hình:

```text
Dev/origin/Dev = nguồn tham chiếu để đọc và đối chiếu.
Current branch = nơi duy nhất được chỉnh sửa code.
```

Cấm tuyệt đối:

```text
Không checkout/switch sang Dev để sửa.
Không commit trực tiếp lên Dev.
Không push trực tiếp lên Dev.
Không reset current branch về Dev.
Không merge/rebase/cherry-pick Dev nếu người dùng chưa yêu cầu.
Không làm mất WIP hiện có trên current branch.
```

Nếu nhánh hiện tại chính là `Dev` hoặc `dev`, phải **dừng trước khi sửa** và báo người dùng chuyển sang nhánh làm việc khác. Không tự tạo nhánh, đổi nhánh hoặc sửa trên Dev.

## 1.2. Xác nhận current branch và cập nhật tham chiếu Dev

Chạy:

```bash
git status --short
git branch --show-current
git log -1 --oneline
git stash list
git fetch origin Dev
git rev-parse origin/Dev
```

Không chạy:

```bash
git checkout Dev
git switch Dev
git pull trên Dev
```

Ghi lại:

```text
Current working branch:
Current HEAD:
Reference Dev SHA:
Working tree:
Stash count:
```

## 1.3. Đọc Dev nhưng sửa current branch

Trước khi code, Agent phải đọc implementation trên `origin/Dev` và đồng thời kiểm tra file tương ứng trên current branch.

Cách đọc file từ Dev mà không đổi nhánh:

```bash
git show origin/Dev:<path/to/file>
```

Cách kiểm tra current branch khác Dev ở đâu:

```bash
git diff --name-status origin/Dev...HEAD
git diff origin/Dev...HEAD -- <path/to/file>
```

Nguyên tắc triển khai:

```text
1. Hiểu business logic và implementation chuẩn từ origin/Dev.
2. Kiểm tra current branch đã thay đổi gì so với Dev.
3. Áp dụng bản sửa trực tiếp vào working tree của current branch.
4. Giữ nguyên thay đổi hợp lệ đang có trên current branch.
5. Không sao chép đè toàn bộ file từ Dev nếu làm mất thay đổi của current branch.
6. Khi có conflict logic giữa Dev và current branch, ưu tiên tích hợp tối thiểu, rõ ràng và báo lại.
```

## 1.4. Bảo vệ WIP

Không reset, clean, stash, discard hoặc sửa các thay đổi WIP không thuộc task.

Trước và sau mỗi phase, chạy:

```bash
git status --short
git diff --check
```

Không được dùng:

```bash
git reset --hard
git clean -fd
git checkout -- .
git restore .
```

## 1.5. Chạy guard cấu trúc trên current branch

```powershell
.\scripts\guard-project-structure.ps1
```

## 1.6. Đọc tối thiểu các file sau trên cả hai nguồn

Với mỗi file quan trọng bên dưới:

```text
- Đọc bản origin/Dev để lấy baseline.
- Đọc bản current branch để xác định divergence.
- Chỉ chỉnh sửa bản current branch.
```

Danh sách:

```text
frontend/pems-react/src/App.tsx
frontend/pems-react/src/shared/auth/ProtectedRoute.tsx
frontend/pems-react/src/shared/auth/resolveEffectiveRole.ts
frontend/pems-react/src/shared/auth/AuthContext.tsx
frontend/pems-react/src/shared/auth/authStorage.ts
frontend/pems-react/src/shared/auth/permissionChecker.ts
frontend/pems-react/src/components/dashboard/Sidebar.tsx
frontend/pems-react/src/components/layout/DashboardLayout.tsx
frontend/pems-react/src/pages/ForbiddenPage.tsx

backend/PEMS.Application/Common/Security/EffectiveRole.cs
backend/PEMS.Application/Common/Security/RoleAccessPolicy.cs
backend/PEMS.Application/Common/Security/IRoleAccessPolicy.cs
backend/PEMS.Api/Filters/RoleAuthorizeAttribute.cs
backend/PEMS.Api/Extensions/AuthorizationExtensions.cs
backend/PEMS.Api/Program.cs

docs/permissions/PERMISSION_MATRIX.md
docs/permissions/PERMISSION_RULES.md
docs/use-cases/USE_CASE_LIST.md
docs/CampusManagement/00_CAMPUS_MANAGEMENT_COMMON_RULES_HO.md
```

6. Tạo inventory đầy đủ:

```text
- Tất cả frontend dashboard routes trong App.tsx
- Tất cả menu item trong Sidebar.tsx
- Tất cả controller/action backend
- Tất cả [Authorize], [AllowAnonymous], [RoleAuthorize]
- Tất cả handler/service có check role/scope
- Tất cả helper có nhánh ADMIN hoặc return true mặc định
```

Không bắt đầu sửa khi chưa có inventory và chưa xác nhận rõ mọi thay đổi sẽ được thực hiện trên current branch, không phải `Dev`.

---

# 2. BUG GỐC ĐÃ XÁC ĐỊNH

Ví dụ hiện tại:

```text
User đăng nhập bằng role ADMIN
Truy cập trực tiếp:
http://localhost:3000/dashboard/campus
```

Kết quả hiện tại:

```text
Trang CampusManagement vẫn render
API campus vẫn được gọi
ADMIN vẫn thấy dữ liệu campus
```

Nguyên nhân:

1. `/dashboard` chỉ kiểm tra đã đăng nhập.
2. Route con `campus` dùng `<ProtectedRoute>` nhưng không truyền role.
3. `ProtectedRoute` hiện chỉ kiểm tra authentication nếu không có `roles`.
4. `App.tsx` và `Sidebar.tsx` dùng logic role riêng, không có single source of truth.
5. `CampusesController` chỉ có `[Authorize]`.
6. `RoleAccessPolicy.CanAccessCampusManagement()` hiện cho `HO` và `ADMIN`.
7. Tài liệu Campus Management quy định actor duy nhất là `HO`.

Đây là lỗi hệ thống, không phải lỗi riêng Campus.

---

# 3. CÁC LỖ HỔNG PHẢI ĐÓNG

## 3.1. Frontend effective role bị gộp sai

Frontend hiện gộp:

```text
STAFF + LEADER
STAFF + STAFF
→ STAFF

DEPARTMENT + LEADER
DEPARTMENT + STAFF
→ DEPARTMENT
```

Phải chuẩn hóa thành đúng 8 effective role:

```ts
export type EffectiveRole =
  | 'ADMIN'
  | 'HO'
  | 'STAFF_LEADER'
  | 'STAFF'
  | 'DEPARTMENT_LEAD'
  | 'DEPARTMENT'
  | 'STUDENT'
  | 'VISITOR';
```

Mapping bắt buộc:

```text
ADMIN + NONE              → ADMIN
HO + NONE                 → HO
STAFF + LEADER            → STAFF_LEADER
STAFF + STAFF             → STAFF
DEPARTMENT + LEADER       → DEPARTMENT_LEAD
DEPARTMENT + STAFF        → DEPARTMENT
STUDENT + NONE            → STUDENT
VISITOR + NONE            → VISITOR
```

Nếu `STAFF` hoặc `DEPARTMENT` thiếu `subRole` hợp lệ:

```text
Không tự đoán
Không tự cấp role Staff
Không tự cấp Leader
Không render dashboard
Redirect /invalid-account
```

---

## 3.2. `hasRole()` đang bỏ qua subRole

Hiện tại `hasRole()` chỉ so sánh raw `roleCode`.

Phải ngừng dùng raw role cho route authorization.

Không được viết:

```ts
hasRole(['STAFF'])
```

khi cần phân biệt Staff Leader và Staff thường.

Phải dùng:

```ts
hasEffectiveRole(['STAFF_LEADER'])
hasEffectiveRole(['STAFF'])
```

---

## 3.3. `App.tsx`, `Sidebar.tsx`, backend đang dùng ba ma trận khác nhau

Hiện tại:

```text
Sidebar có điều kiện riêng
App.tsx có điều kiện riêng
Backend policy/controller có điều kiện riêng
```

Phải tạo một **frontend route policy duy nhất** và dùng lại cho:

```text
Route guard
Sidebar
Default landing route
403 back button
Route tests
```

Backend vẫn có policy riêng nhưng phải audit để đồng bộ với business rules.

---

## 3.4. Frontend đang đọc legacy `currentUser`

`App.tsx` và `Sidebar.tsx` đang đọc:

```ts
localStorage.getItem('currentUser')
```

Không được dùng localStorage để quyết định authorization.

Phải dùng state từ `AuthContext`, vì `AuthContext` đã bootstrap lại profile từ backend.

`currentUser` chỉ được giữ tạm cho legacy display nếu chưa thể xóa ngay, nhưng:

```text
Không dùng để check role
Không dùng để check subRole
Không dùng để quyết định route
Không dùng để quyết định action
```

---

## 3.5. Backend chưa fail-closed

`AddAuthorization()` hiện chưa có fallback policy.

Phải cấu hình:

```csharp
options.FallbackPolicy = new AuthorizationPolicyBuilder()
    .RequireAuthenticatedUser()
    .Build();
```

Kết quả:

```text
Mặc định mọi endpoint yêu cầu login.
Chỉ endpoint có [AllowAnonymous] mới public.
```

Sau đó audit toàn bộ `[AllowAnonymous]`.

---

## 3.6. Có endpoint P0 cấp JWT không cần xác thực

Trong `DashboardController` có endpoint:

```text
GET /api/dashboard/debug-user?email=...
```

Endpoint đang:

```text
[AllowAnonymous]
Tìm user theo email
Sinh JWT
Trả token
```

Phải **xóa hoàn toàn endpoint này**.

Không được:

```text
Ẩn bằng environment flag
Chỉ disable frontend
Chỉ đổi route
Giữ trong Production nhưng bảo “không ai biết”
```

Sau khi sửa phải có architecture test xác nhận không còn `debug-user`.

---

## 3.7. Nhiều controller nghiệp vụ không có auth ở class

Audit tối thiểu:

```text
DepartmentsController
DocumentsController
GalleriesController
DelegationsController
AgendaTemplatesController
VisitPhotosController
```

Một số handler có self-guard, nhưng controller vẫn phải fail-closed.

Phải bổ sung `[Authorize]` hoặc class-level `[RoleAuthorize]` phù hợp.

Handler vẫn phải giữ authorization theo object scope.

---

# 4. NGUYÊN TẮC AUTHORIZATION CHUẨN

## 4.1. Ba lớp bắt buộc

### Lớp 1 — Frontend route guard

Mục đích:

```text
Không render page trái quyền
Không gọi API thừa
Không cho deep-link bằng URL
Trải nghiệm người dùng rõ ràng
```

Không được coi đây là bảo mật cuối.

### Lớp 2 — Backend coarse role

Mục đích:

```text
Chặn role sai trước khi vào handler
Trả 401/403 nhất quán
Không để controller nào vô tình public
```

Ví dụ:

```csharp
[Authorize]
[RoleAuthorize(EffectiveRole.Ho)]
```

### Lớp 3 — Object/data scope trong handler

Mục đích:

```text
Chống đổi ID trên URL/body
Chống cross-campus
Chống cross-department
Chống đọc request không sở hữu
Chống host/participant giả mạo
```

Ví dụ:

```text
Staff Leader → đúng primary campus
Department Leader → đúng department và còn là head_user_id
Visitor → own request
Host → current host của instance
Participant → accepted participant của instance
Student → được assign/invite vào instance
```

---

## 4.2. ADMIN không phải business superuser

Không được viết:

```csharp
if (effectiveRole == EffectiveRole.Admin) return true;
```

trong mọi module nghiệp vụ.

ADMIN chỉ được vào System Administration Console và các chức năng được business rule cấp rõ.

Audit toàn repo các từ khóa:

```text
EffectiveRole.Admin
RoleCode.Admin
isRealAdmin
superuser
technical fallback
return true
```

Với mỗi hit:

```text
Giữ nếu đúng System Admin
Loại bỏ nếu vô tình cấp nghiệp vụ
Viết test cho quyết định đó
```

---

## 4.3. Không đổi quyền ngoài business rule

Không suy diễn.

Khi tài liệu và code mâu thuẫn:

1. Ưu tiên canonical business rule / tài liệu module hiện hành.
2. Ghi rõ conflict.
3. Không tự cấp thêm quyền để “cho tiện”.
4. Fail-closed khi chưa xác định được.

Ví dụ đã chốt:

```text
Campus Management → HO only
```

---

# 5. FRONTEND IMPLEMENTATION

## 5.1. Sửa `resolveEffectiveRole.ts`

File:

```text
frontend/pems-react/src/shared/auth/resolveEffectiveRole.ts
```

Yêu cầu:

```ts
export type EffectiveRole =
  | 'ADMIN'
  | 'HO'
  | 'STAFF_LEADER'
  | 'STAFF'
  | 'DEPARTMENT_LEAD'
  | 'DEPARTMENT'
  | 'STUDENT'
  | 'VISITOR';
```

Hàm phải:

```text
Normalize case
Normalize DEPT → DEPARTMENT nếu backend còn alias
Fail-closed với role lạ
Fail-closed với subRole thiếu
Không gộp Leader và Staff
```

Thêm unit test đầy đủ cho:

```text
8 mapping đúng
STAFF thiếu subRole
DEPARTMENT thiếu subRole
subRole sai
role sai
null user
mixed case
DEPT alias
```

---

## 5.2. Mở rộng `AuthContext`

File:

```text
frontend/pems-react/src/shared/auth/AuthContext.tsx
```

Bổ sung:

```ts
interface AuthContextValue {
  user: AuthUser | null;
  effectiveRole: EffectiveRole | null;
  isAuthenticated: boolean;
  isLoading: boolean;
  isReady: boolean;

  hasEffectiveRole: (roles: EffectiveRole[]) => boolean;
}
```

Yêu cầu:

```text
effectiveRole được derive từ user trong AuthContext
Không đọc lại localStorage ở component
Auth bootstrap phải hoàn tất trước khi route con render
Nếu backend profile đổi role/subRole, UI cập nhật theo profile mới
```

Không cần tạo permission runtime mới nếu project hiện dùng fixed role policy.

---

## 5.3. Tạo single source of truth cho dashboard route

Tạo file:

```text
frontend/pems-react/src/shared/auth/dashboardRouteAccess.ts
```

Định nghĩa:

```ts
export type DashboardRouteKey =
  | 'DASHBOARD_HOME'
  | 'PROFILE'
  | 'NEWS_LIST'
  | 'NEWS_CREATE'
  | 'NEWS_EDIT'
  | 'EMAIL_LIST'
  | 'EMAIL_CREATE'
  | 'EMAIL_DETAIL'
  | 'PARTNER_LIST'
  | 'PARTNER_CREATE'
  | 'PARTNER_DETAIL'
  | 'PARTNER_EDIT'
  | 'DEPARTMENT_LIST'
  | 'DEPARTMENT_DETAIL'
  | 'MY_DEPARTMENT'
  | 'ACCOUNT_LIST'
  | 'CAMPUS_LIST'
  | 'CAMPUS_DETAIL'
  | 'FAQ_LIST'
  | 'FAQ_DETAIL'
  | 'VISIT_LIST'
  | 'VISIT_CREATE'
  | 'VISIT_DETAIL'
  | 'VISIT_EDIT'
  | 'VISIT_PROCESS'
  | 'VISIT_INVITATION'
  | 'AGENDA_TEMPLATE'
  | 'VISIT_PHOTOS'
  | 'DOCUMENTS'
  | 'GALLERY'
  | 'GALLERY_LOCATIONS'
  | 'MINUTES'
  | 'POST_VISIT_TASKS'
  | 'REPORTS'
  | 'FEEDBACK'
  | 'API_MANAGEMENT'
  | 'ADMIN_SESSIONS'
  | 'ADMIN_SECURITY'
  | 'ADMIN_AUDIT_LOGS';
```

Policy:

```ts
export type DashboardRoutePolicy = {
  key: DashboardRouteKey;
  allowedRoles: readonly EffectiveRole[];
  showInSidebar?: boolean;
  defaultForRoles?: readonly EffectiveRole[];
};
```

Utility bắt buộc:

```ts
canAccessDashboardRoute(role, routeKey): boolean
getDefaultDashboardRoute(role): string
getVisibleSidebarItems(role): ...
```

Fail-closed:

```text
Unknown routeKey → false
role null → false
policy thiếu → test/build fail
```

---

## 5.4. Route matrix mức cao

Phải audit theo code + business rule hiện hành. Baseline ban đầu:

| Route/module | Effective role |
|---|---|
| System Admin dashboard | `ADMIN` |
| Session management | `ADMIN` |
| Security monitoring | `ADMIN` |
| Audit logs | `ADMIN` |
| API management | `ADMIN` |
| Account management | `ADMIN`, `HO`, `STAFF_LEADER` |
| Campus management | `HO` |
| FAQ management | `HO` |
| My Department | `DEPARTMENT_LEAD` |
| Gallery | `STAFF_LEADER` |
| Gallery locations | `STAFF_LEADER` |
| Reports | `HO`, `STAFF_LEADER`, `DEPARTMENT_LEAD`, `DEPARTMENT` |
| Visit workspace | tất cả role hợp lệ trừ `ADMIN`, nhưng route detail/action phải theo object scope |
| Profile | mọi role hợp lệ |
| Notification | mọi role hợp lệ |

Các module còn lại phải đối chiếu handler/backend và docs trước khi chốt.

Không lấy điều kiện cũ trong Sidebar làm nguồn chuẩn nếu nó mâu thuẫn backend/docs.

---

## 5.5. Nâng cấp `ProtectedRoute`

File:

```text
frontend/pems-react/src/shared/auth/ProtectedRoute.tsx
```

Giữ chức năng auth hiện có, bổ sung `effectiveRoles` hoặc tạo `RouteAccessGuard`.

Khuyến nghị:

```tsx
<RouteAccessGuard routeKey="CAMPUS_LIST">
  <CampusManagement />
</RouteAccessGuard>
```

Logic chuẩn:

```text
isLoading / !isReady
→ FullScreenLoader

!isAuthenticated
→ redirect "/" hoặc login flow hiện hành

mustChangePassword
→ /change-password

effectiveRole == null
→ /invalid-account

routeKey không được phép
→ /403

được phép
→ render children
```

Yêu cầu:

```text
Dùng replace
Không render children trước khi pass
Không để child gọi API trước guard
Không redirect vòng lặp
Không tự chuyển sang module khác
```

---

## 5.6. Refactor `App.tsx`

File:

```text
frontend/pems-react/src/App.tsx
```

Loại bỏ:

```ts
const userStr = localStorage.getItem("currentUser");
const user = ...
const isDeptLeader = ...
const isDeptStaff = ...
const isHO = ...
const isStaffLeader = ...
```

Dùng:

```ts
const { effectiveRole } = useAuth();
```

Mọi dashboard route phải có route policy.

Ví dụ Campus:

```tsx
<Route
  path="campus"
  element={
    <RouteAccessGuard routeKey="CAMPUS_LIST">
      <CampusManagement />
    </RouteAccessGuard>
  }
/>
```

Campus detail:

```tsx
<Route
  path="campus/:id"
  element={
    <RouteAccessGuard routeKey="CAMPUS_DETAIL">
      <CampusDetail />
    </RouteAccessGuard>
  }
/>
```

Không được để route con chỉ bọc `<ProtectedRoute>` không role.

---

## 5.7. Refactor `Sidebar.tsx`

File:

```text
frontend/pems-react/src/components/dashboard/Sidebar.tsx
```

Loại bỏ role authorization dựa trên:

```text
currentUser
roleForSidebar
isRealAdmin
isStaffLeader
isDeptLeader
isDeptStaff
```

Dùng:

```ts
const { user, effectiveRole } = useAuth();
const items = getVisibleSidebarItems(effectiveRole);
```

Mỗi menu item phải map tới `routeKey`.

Quy tắc parity:

```text
Menu hiện → route guard phải cho vào.
Menu ẩn → gõ URL phải bị 403.
```

Detail route có thể không xuất hiện menu nhưng vẫn phải có policy.

---

## 5.8. Sửa `ForbiddenPage`

File:

```text
frontend/pems-react/src/pages/ForbiddenPage.tsx
```

Hiện tại nút “Về trang quản trị” chuyển cứng `/dashboard`.

Thay bằng:

```ts
const destination = getDefaultDashboardRoute(effectiveRole);
```

Không được tạo redirect loop.

Default route phải được khai báo trong route policy.

Ví dụ:

```text
ADMIN → /dashboard
HO → /dashboard
STAFF_LEADER → /dashboard
STAFF → /dashboard
DEPARTMENT_LEAD → /dashboard
DEPARTMENT → route workspace hợp lệ
STUDENT → /dashboard/visit
VISITOR → /dashboard/visit
```

Chốt theo dashboard hiện hành sau audit.

---

## 5.9. Không redirect toàn cục mọi API 403

Không thêm:

```ts
if (status === 403) window.location.href = '/403';
```

trong Axios interceptor.

Giữ nguyên special-case:

```text
CAMPUS_INACTIVE_ACCESS_DENIED
```

Các loại 403:

```text
Coarse role sai → route guard xử lý.
Object scope sai → page xử lý Forbidden/Not Found.
Session/campus inactive → auth interceptor xử lý logout.
```

---

# 6. BACKEND IMPLEMENTATION

## 6.1. Xóa endpoint `debug-user`

File:

```text
backend/PEMS.Api/Controllers/DashboardController.cs
```

Xóa toàn bộ action:

```text
GET /api/dashboard/debug-user
```

Xóa import/code chỉ phục vụ endpoint này.

Thêm test:

```text
GET /api/dashboard/debug-user → 404
Không sinh JWT
```

---

## 6.2. Thêm fallback authorization policy

File:

```text
backend/PEMS.Api/Extensions/AuthorizationExtensions.cs
```

Sửa:

```csharp
services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});
```

Thêm using cần thiết.

Sau đó audit `[AllowAnonymous]`.

Chỉ public endpoint hợp lệ mới được giữ.

---

## 6.3. Harden `RoleAuthorizeAttribute`

File:

```text
backend/PEMS.Api/Filters/RoleAuthorizeAttribute.cs
```

Hiện tại `EffectiveRole.Resolve()` có thể throw nếu role/subRole sai.

Phải:

```text
Catch InvalidOperationException
Trả 403
Không trả 500
Không cấp quyền mặc định
```

Phải xử lý `[AllowAnonymous]` đúng cách, tránh fallback policy chặn endpoint public.

Response 403 nên nhất quán:

```json
{
  "success": false,
  "errorCode": "FORBIDDEN",
  "message": "Bạn không có quyền thực hiện thao tác này."
}
```

Không làm thay đổi contract lớn nếu middleware hiện có format khác; dùng format project đang dùng.

---

## 6.4. Sửa Campus Management thành HO-only

### Controller

File:

```text
backend/PEMS.Api/Controllers/CampusesController.cs
```

Class:

```csharp
[ApiController]
[Authorize]
[Route("api/[controller]")]
```

Các action quản lý phải có:

```csharp
[RoleAuthorize(EffectiveRole.Ho)]
```

Áp dụng cho:

```text
addnewcampus
viewcampuslist
searchandfiltercampus
filter-options
viewcampusdetails
updatecampus
managecampusstatus
campusstatusimpact
assigncampuslead
```

Chỉ giữ public:

```text
active
available-for-registration
```

### Policy

File:

```text
backend/PEMS.Application/Common/Security/RoleAccessPolicy.cs
```

Sửa:

```csharp
CanAccessCampusManagement → chỉ EffectiveRole.Ho
CanManageCampus → chỉ EffectiveRole.Ho
```

Loại `EffectiveRole.Admin`.

### Handlers

Audit toàn bộ Campus handlers để chắc chắn dùng policy:

```text
CampusListQueryExecutor
GetCampusFilterOptionsQueryHandler
ViewCampusDetailsQueryHandler
AddNewCampusCommandHandler
UpdateCampusCommandHandler
ManageCampusStatusCommandHandler
GetCampusStatusImpactQueryHandler
AssignCampusLead handler
```

### Tests

Sửa test cũ đang cho ADMIN.

Bổ sung:

```text
HO → 200 / success
ADMIN → 403
STAFF_LEADER → 403
STAFF → 403
DEPARTMENT_LEAD → 403
DEPARTMENT → 403
STUDENT → 403
VISITOR → 403
ANONYMOUS → 401
```

Denied command phải không có side effect.

---

## 6.5. Audit và khóa tất cả controller nghiệp vụ

Tạo bảng audit:

| Controller | Auth hiện tại | Coarse role cần có | Handler scope | Thay đổi |
|---|---|---|---|---|

Audit tối thiểu:

```text
AccountsController
AdminController
ApiIntegrationsController
CampusesController
DashboardController
DepartmentsController
DepartmentLeaderController
DocumentsController
EmailsController
EmailTemplatesController
FaqsController
FeedbacksController
GalleriesController
MeetingMinutesController
NewsController
PartnersController
ReportsController
AgendaTemplatesController
DelegationsController
VisitRequestsController
VisitPhotosController
FilesController
NotificationsController
```

Yêu cầu:

```text
Không controller nghiệp vụ nào vô tình public.
Không action mutation nào chỉ dựa vào frontend.
Không controller-level role quá rộng khiến action nhạy cảm bị thừa quyền.
```

Dùng per-action `[RoleAuthorize]` khi read/write có quyền khác nhau.

---

## 6.6. Audit `RoleAccessPolicy`

File:

```text
backend/PEMS.Application/Common/Security/RoleAccessPolicy.cs
```

Đặc biệt kiểm tra:

```csharp
return true;
```

Hiện `CanViewVisitRequest()` có fallback rộng cho Staff/Dept/Student.

Không được giữ logic “thường thì được xem”.

Phải thay bằng scope chính xác hoặc delegate sang module-specific policy.

Không over-refactor toàn architecture nếu đã có policy/service riêng theo module.

Ưu tiên:

```text
Dùng existing service hiện có
Không tạo mega authorization service
Không trùng logic
```

---

## 6.7. Audit module Partner

File:

```text
backend/PEMS.Application/Partners/Common/PartnerAccess.cs
```

Hiện ADMIN có:

```text
full read
technical fallback edit/manage child
```

Phải đối chiếu business rule hiện hành.

Nếu ADMIN là technical admin và không có business access:

```text
Loại ADMIN khỏi CanViewPartnerModule
Loại ADMIN khỏi CanEditPartner
Loại ADMIN khỏi CanManagePartnerChildren
```

Không tự sửa nếu tài liệu canonical cho phép; ghi evidence và chốt theo nguồn chuẩn.

Thêm cross-campus tests.

---

## 6.8. Audit module Email

Files:

```text
backend/PEMS.Api/Controllers/EmailsController.cs
backend/PEMS.Api/Controllers/EmailTemplatesController.cs
```

Phải phân biệt:

```text
Email workspace
Template read/preview
Template write
Manual send
Sent history
Contact settings
```

Không dùng một class-level role list quá rộng cho mọi action.

Ví dụ:

```text
Template write → HO only
Template preview/read → composing roles
Email send → role + object scope
```

Đảm bảo route frontend khớp với backend.

---

## 6.9. Audit module Department

Files:

```text
backend/PEMS.Api/Controllers/DepartmentsController.cs
backend/PEMS.Api/Controllers/DepartmentLeaderController.cs
```

`DepartmentLeaderController` đã có coarse role tốt nhưng phải giữ DB recheck `head_user_id`.

`DepartmentsController` cần:

```text
[Authorize]
Role gate theo action
Campus/department scope trong handler
```

Không cho Department Leader truyền `departmentId` tùy ý cho chức năng personnel.

---

## 6.10. Audit Gallery và Location

Files:

```text
backend/PEMS.Api/Controllers/GalleriesController.cs
frontend/.../GalleryManagement
frontend/.../LocationManagement
```

Coarse role:

```text
STAFF_LEADER
```

Handler tiếp tục kiểm tra:

```text
Current primary campus
Gallery item/location thuộc campus
Active account
Valid Staff Leader state
```

Cross-campus ID phải trả 403 hoặc 404 theo convention hiện có.

---

## 6.11. Audit Documents và Files

Files:

```text
backend/PEMS.Api/Controllers/DocumentsController.cs
backend/PEMS.Api/Controllers/FilesController.cs
backend/PEMS.Application/Files/Common/FileAccessAuthorizationService.cs
```

Yêu cầu:

```text
Document list/detail phải auth
File download phải theo relation
Không role nào được walk file_id
Không chỉ dựa vào upload owner nếu business object khác scope
```

Giữ và tái sử dụng `FileAccessAuthorizationService`.

---

## 6.12. Audit Visit, Agenda, Photos, Minutes, Feedback

Các route này không thể chỉ khóa theo role.

Phải giữ object-level scope:

```text
Visit request ownership
Visit instance campus
Current host
Accepted participant
Assigned department
Assigned student
Feedback target
Minutes lock owner
Agenda template scope
```

Route guard chỉ là coarse gate.

Backend phải chống:

```text
Đổi visitRequestId
Đổi visitInstanceId
Đổi participantId
Đổi minutesId
Đổi agendaTemplateId
Đổi fileId
```

---

# 7. FRONTEND ROUTE AUDIT MATRIX

Tạo test matrix:

```text
8 effective roles × toàn bộ dashboard route
```

Roles:

```text
ADMIN
HO
STAFF_LEADER
STAFF
DEPARTMENT_LEAD
DEPARTMENT
STUDENT
VISITOR
```

Với mỗi route:

```text
ALLOW
DENY_403
INVALID_ACCOUNT
REDIRECT_DEFAULT
```

Các route phải bao gồm cả:

```text
List
Create
Detail
Edit
Nested route
Dynamic ID route
```

Ví dụ:

```text
/dashboard/campus
/dashboard/campus/1
/dashboard/accounts
/dashboard/departments
/dashboard/my-department
/dashboard/gallery
/dashboard/gallery/locations
/dashboard/admin/security
/dashboard/visit/v2/1
/dashboard/visit/process/1
/dashboard/partners/1/edit
```

---

# 8. TESTING BẮT BUỘC

## 8.1. Frontend unit tests

### Effective role

```text
8 valid mappings
invalid role
invalid subRole
missing subRole
case normalization
DEPT alias
```

### Route guard

Với mỗi role-route:

```text
Allowed → page render
Denied → /403
Child không mount khi denied
API mock không bị gọi khi denied
```

### Sidebar parity

Assertion:

```text
Sidebar item visible
⇔
canAccessDashboardRoute == true
```

### localStorage tampering

Case:

```text
AuthContext user = ADMIN
Sửa currentUser trong localStorage thành HO
Route campus vẫn phải deny
```

Ngược lại:

```text
AuthContext user = HO
Sửa currentUser thành ADMIN
Route campus vẫn phải allow theo AuthContext
```

### Reload/deep-link

```text
Paste URL trực tiếp
Refresh browser
Back/forward
Query string
Hash fragment
```

Không bypass.

---

## 8.2. Backend integration tests

Cho mỗi module chính, test:

```text
ADMIN
HO
STAFF_LEADER
STAFF
DEPARTMENT_LEAD
DEPARTMENT
STUDENT
VISITOR
ANONYMOUS
INVALID ROLE/SUBROLE
```

Denied request phải xác nhận:

```text
Status 401 hoặc 403 đúng
Không trả dữ liệu nhạy cảm
Không insert
Không update
Không delete
Không gửi email
Không upload file
Không tạo audit nghiệp vụ giả
```

---

## 8.3. Anti-IDOR tests

Tối thiểu:

```text
Staff Leader campus A truy cập object campus B
Department Leader dept A truy cập dept B
Visitor A truy cập request Visitor B
Host A truy cập instance Host B
Student A truy cập photo folder không được assign
Participant A truy cập invitation Participant B
User A download file của object B
```

Expected:

```text
403 hoặc 404 theo convention module
Không leak existence nếu module yêu cầu anti-enumeration
```

---

## 8.4. Architecture tests

Tạo hoặc cập nhật architecture test để fail nếu:

```text
Có controller nghiệp vụ không có auth/fallback
Có [AllowAnonymous] ngoài whitelist
Còn endpoint debug-user
Có route dashboard không có routeKey/policy
Có authorization decision đọc currentUser
Có effective role bị gộp
Có default authorization helper return true
Campus policy còn ADMIN
```

Whitelist `[AllowAnonymous]` phải explicit, ví dụ:

```text
Login/auth public endpoints
Public content
Public FAQ
Public partner
Public visit registration/OTP
Public campus registration options
Health endpoint nếu chủ đích
```

Không dùng wildcard rộng.

---

## 8.5. Playwright E2E direct URL matrix

Đăng nhập từng role rồi gõ trực tiếp URL.

Case bắt buộc:

```gherkin
Given ADMIN đã đăng nhập
When mở /dashboard/campus
Then chuyển tới /403
And CampusManagement không mount
And không gọi GET /api/Campuses/viewcampuslist
And gọi API Campus trực tiếp trả 403
```

Thêm tương tự cho:

```text
ADMIN → gallery
ADMIN → visit
HO → admin/security
STAFF → campus
DEPARTMENT → gallery
STUDENT → account management
VISITOR → reports
```

Chốt expected theo route matrix cuối.

---

# 9. ERROR HANDLING

## 9.1. Status chuẩn

```text
401 → chưa đăng nhập/token invalid
403 → đã đăng nhập nhưng sai role/scope
404 → object không tồn tại hoặc anti-enumeration
409 → business conflict
```

Không trả 500 cho invalid role/subRole.

---

## 9.2. Không chuyển hướng lung tung

Cấm:

```text
Sai role route A → tự nhảy route B bất kỳ
403 API phụ → toàn app nhảy /403
Unknown URL → tự về dashboard
Department Staff vào email → tự quay dashboard không message
```

Quy tắc:

```text
Unknown URL → 404 page
Known route nhưng sai quyền → 403 page
Invalid account shape → invalid-account
Unauthenticated → auth entry
```

---

# 10. FILES DỰ KIẾN THAY ĐỔI

## Frontend

```text
frontend/pems-react/src/shared/auth/resolveEffectiveRole.ts
frontend/pems-react/src/shared/auth/AuthContext.tsx
frontend/pems-react/src/shared/auth/ProtectedRoute.tsx
frontend/pems-react/src/shared/auth/dashboardRouteAccess.ts       [new]
frontend/pems-react/src/App.tsx
frontend/pems-react/src/components/dashboard/Sidebar.tsx
frontend/pems-react/src/pages/ForbiddenPage.tsx
frontend/pems-react/src/shared/auth/permissionChecker.ts          [deprecate/update]
frontend/pems-react/src/shared/auth/RoleGuard.tsx                 [update nếu còn dùng]
```

Tests tương ứng:

```text
frontend/pems-react/src/shared/auth/__tests__/resolveEffectiveRole.test.ts
frontend/pems-react/src/shared/auth/__tests__/dashboardRouteAccess.test.ts
frontend/pems-react/src/shared/auth/__tests__/ProtectedRoute.test.tsx
frontend/pems-react/src/components/dashboard/__tests__/SidebarRouteParity.test.tsx
frontend/pems-react/tests-realstack/...role-route-access...
```

## Backend

```text
backend/PEMS.Api/Controllers/DashboardController.cs
backend/PEMS.Api/Extensions/AuthorizationExtensions.cs
backend/PEMS.Api/Filters/RoleAuthorizeAttribute.cs
backend/PEMS.Application/Common/Security/RoleAccessPolicy.cs
backend/PEMS.Application/Common/Security/IRoleAccessPolicy.cs

backend/PEMS.Api/Controllers/CampusesController.cs
backend/PEMS.Api/Controllers/DepartmentsController.cs
backend/PEMS.Api/Controllers/DocumentsController.cs
backend/PEMS.Api/Controllers/GalleriesController.cs
backend/PEMS.Api/Controllers/DelegationsController.cs
backend/PEMS.Api/Controllers/AgendaTemplatesController.cs
backend/PEMS.Api/Controllers/VisitPhotosController.cs

backend/PEMS.Api/Controllers/AccountsController.cs
backend/PEMS.Api/Controllers/ApiIntegrationsController.cs
backend/PEMS.Api/Controllers/EmailsController.cs
backend/PEMS.Api/Controllers/EmailTemplatesController.cs
backend/PEMS.Api/Controllers/PartnersController.cs
backend/PEMS.Api/Controllers/FeedbacksController.cs
backend/PEMS.Api/Controllers/MeetingMinutesController.cs
```

Chỉ sửa controller thật sự cần sau audit; không tạo diff giả.

---

# 11. THỨ TỰ TRIỂN KHAI

Mọi phase bên dưới đều chạy trên **current working branch**. `origin/Dev` chỉ được dùng làm baseline đọc/đối chiếu.

## Phase A — Audit và evidence

1. Ghi current branch, current HEAD và `origin/Dev` SHA.
2. Inventory routes/menu/controllers từ `origin/Dev`.
3. So sánh inventory với current branch.
4. Tạo role-route matrix hiện trạng.
5. Tạo backend endpoint-role matrix hiện trạng.
6. Liệt kê mismatch cụ thể giữa business rule, Dev baseline và current branch.
7. Không sửa code trước khi có audit table.

## Phase B — P0 closure

1. Xóa `debug-user`.
2. Thêm fallback policy.
3. Harden `RoleAuthorizeAttribute`.
4. Sửa Campus HO-only.
5. Viết test P0.

## Phase C — Frontend role normalization

1. Sửa 8 effective roles.
2. Mở rộng AuthContext.
3. Tạo route policy.
4. Viết unit tests.

## Phase D — Route/menu refactor

1. Refactor App.tsx.
2. Refactor Sidebar.
3. Refactor ForbiddenPage.
4. Xóa authorization dựa trên `currentUser`.

## Phase E — Backend module audit

Theo module:

```text
Admin
Account
Campus
Department
API integration
Email
Partner
Gallery
Document/File
Visit
Photo
Agenda
Minutes
Feedback
Report
FAQ
News
```

Mỗi module:

```text
Controller coarse role
Handler scope
Direct API tests
Cross-scope tests
```

## Phase F — Full regression

```text
Backend build
Backend unit
Architecture tests
Integration tests
Frontend typecheck
Frontend build
Frontend unit
Playwright direct-route matrix
```

---

# 12. COMMANDS KIỂM TRA

## Backend

```bash
dotnet build PEMS.slnx
dotnet test tests/PEMS.UnitTests/PEMS.UnitTests.csproj
dotnet test tests/PEMS.ArchitectureTests/PEMS.ArchitectureTests.csproj
dotnet test tests/PEMS.IntegrationTests/PEMS.IntegrationTests.csproj
```

## Frontend

```bash
cd frontend/pems-react
npm run lint
npm run build
npm run test:unit
npm run test:e2e
```

Nếu integration cần MySQL, báo rõ môi trường và chạy đúng disposable DB hiện có.

Không được bỏ test failing bằng cách:

```text
skip
disable
comment out
weaken assertion
increase timeout vô lý
```

---

# 13. TIÊU CHÍ NGHIỆM THU

Chỉ được báo hoàn thành khi tất cả điều kiện đạt:

```text
[ ] Không còn /api/dashboard/debug-user
[ ] Global fallback auth đã bật
[ ] [AllowAnonymous] đã whitelist
[ ] Frontend có đủ 8 effective role
[ ] Không route authorization nào đọc currentUser
[ ] Mọi dashboard route có policy
[ ] Sidebar dùng cùng route policy
[ ] 403 back button dùng default route theo role
[ ] Campus chỉ HO ở frontend và backend
[ ] ADMIN không còn được coi là business superuser mặc định
[ ] Controller audit hoàn tất
[ ] Direct API role matrix xanh
[ ] Anti-IDOR matrix xanh
[ ] Frontend route matrix xanh
[ ] Sidebar-route parity xanh
[ ] Deep-link E2E xanh
[ ] Backend unit xanh
[ ] Architecture tests xanh
[ ] Integration tests xanh
[ ] Frontend typecheck/build/unit xanh
```

---

# 14. BÁO CÁO CUỐI CÙNG BẮT BUỘC

Báo cáo theo format:

## 14.1. Preflight

```text
Current working branch:
Current HEAD before:
Reference origin/Dev SHA:
Working tree before:
Stashes:
Confirmation: no checkout/switch/write/commit/push on Dev
```

## 14.2. Root cause

Liệt kê lỗi cụ thể, không nói chung chung.

## 14.3. Authorization matrix

Đính kèm:

```text
Frontend route matrix
Sidebar matrix
Backend endpoint-role matrix
Object scope matrix
```

## 14.4. Files changed trên current branch

```text
file/path
- baseline đối chiếu từ origin/Dev
- current branch trước khi sửa khác gì
- thay đổi đã áp dụng trên current branch
- lý do
- ảnh hưởng
```

Phải xác nhận rõ:

```text
Không có file nào được sửa trực tiếp trên Dev.
Không có commit/push nào được thực hiện lên Dev.
```

## 14.5. Security closures

```text
debug-user removed
fallback policy enabled
anonymous whitelist
Campus HO-only
ADMIN implicit grants removed
IDOR protections verified
```

## 14.6. Test results

```text
Backend build:
Unit:
Architecture:
Integration:
Frontend typecheck:
Frontend build:
Frontend unit:
Playwright:
```

## 14.7. Remaining debt

Chỉ ghi phần thực sự chưa hoàn thành.

Không được báo “100% fixed” nếu chưa chạy đủ matrix.

---

# 15. QUY TẮC KHÔNG ĐƯỢC VI PHẠM

```text
Không chỉ ẩn menu.
Không chỉ thêm roles vào route Campus.
Không chỉ sửa frontend.
Không coi ADMIN là superuser toàn hệ thống.
Không tin localStorage cho authorization.
Không bỏ handler scope.
Không tạo dynamic permission system mới.
Không đổi database schema nếu không cần.
Không refactor toàn dự án ngoài scope.
Không đổi API contract không cần thiết.
Không bỏ test cũ.
Không sửa seed/SQL nếu task không yêu cầu.
Không push/merge khi chưa được yêu cầu.
Không checkout, sửa, commit hoặc push trực tiếp trên Dev.
```

---

# 16. QUY TẮC GIT VÀ PHẠM VI GIAO BÀN

Sau khi sửa xong:

```text
Code thay đổi chỉ nằm trên current branch.
Dev/origin/Dev không bị thay đổi.
Không tự push.
Không tự tạo PR.
Không tự merge.
Không tự rebase.
Không tự commit nếu người dùng chưa yêu cầu commit.
```

Báo cáo phải kèm:

```bash
git branch --show-current
git status --short
git diff --stat
git diff --check
git diff origin/Dev...HEAD -- <các file liên quan>
```

Nếu current branch có commit cũ khác Dev, phải phân biệt rõ:

```text
Thay đổi đã tồn tại trước task.
Thay đổi do task RBAC này tạo ra.
```

---

# 17. KẾT QUẢ KỲ VỌNG

Sau khi hoàn thành:

```text
Người dùng chỉ thấy menu đúng quyền.
Gõ URL trái quyền luôn vào 403.
Page trái quyền không mount và không gọi API.
Gọi API trực tiếp trái quyền trả 403.
Đổi ID không truy cập được dữ liệu ngoài scope.
Sửa localStorage không thể nâng quyền.
Invalid role/subRole không tạo 500 hoặc cấp quyền ngầm.
Unknown URL ra 404.
Không còn redirect vòng hoặc chuyển hướng lung tung.
```

Authorization phải được chứng minh bằng test ở cả:

```text
Frontend route
Backend role
Database/object scope
```
