# PROMPT — HOÀN THIỆN TRANG QUẢN LÝ ROLE ADMIN PEMS

Hãy đọc kỹ source hiện tại của dự án PEMS trước khi sửa, đặc biệt:

- `frontend/pems-react/src/pages/dashboard/home/AdminDashboardView.tsx`
- `frontend/pems-react/src/pages/dashboard/apis/ApiManagement.tsx`
- `frontend/pems-react/src/pages/dashboard/accounts/AccountManagement.tsx`
- `frontend/pems-react/src/components/dashboard/Sidebar.tsx`
- `frontend/pems-react/src/App.tsx`
- `backend/PEMS.Api/Controllers/DashboardController.cs`
- `backend/PEMS.Api/Controllers/AccountsController.cs`
- `backend/PEMS.Api/Controllers/ApiIntegrationsController.cs`
- các bảng `users`, `user_sessions`, `login_logs`, `security_events`, `audit_logs`, `audit_log_changes`, `api_configurations`, `api_request_logs`, `api_usage_quotas`.

## Mục tiêu

Hoàn thiện khu vực quản trị cho role `ADMIN` theo hướng **System Administration Console**.
Không cho Admin tham gia nghiệp vụ tiếp khách, duyệt đơn, hậu cần, đối tác, tin tức hoặc FAQ.

## Menu Admin

Sidebar Admin gồm:

1. Dashboard
2. Quản lý tài khoản
3. Phiên đăng nhập
4. Bảo mật
5. Quản lý API
6. Nhật ký kiểm toán

Ẩn toàn bộ menu nghiệp vụ không thuộc Admin.

## Dashboard Admin

Xóa toàn bộ mock data hiện tại.

Hiển thị dữ liệu thật:

- Tổng tài khoản: ACTIVE / INACTIVE / LOCKED / mới trong 30 ngày.
- Phiên đang hoạt động, hết hạn, bị thu hồi.
- Đăng nhập thành công/thất bại trong 24 giờ.
- Security event mức HIGH/CRITICAL.
- API đang ACTIVE, test FAILED, chưa có credential, quota trên 80%.
- Biểu đồ login SUCCESS/FAILED theo thời gian.
- Biểu đồ API request SUCCESS/FAILED và response time.
- Danh sách audit log gần nhất.

Không hiển thị uptime, CPU, RAM, DB load hoặc dung lượng nếu backend chưa có telemetry thật. Khi chưa có nguồn dữ liệu, hiển thị “Chưa cấu hình giám sát hạ tầng”.

## Quản lý tài khoản

Cho Admin xem toàn bộ tài khoản, mọi campus và role.

Hỗ trợ:

- tìm kiếm, lọc, phân trang;
- xem chi tiết;
- tạo tài khoản;
- đổi role/campus/department;
- ACTIVE ↔ INACTIVE;
- LOCK/UNLOCK bằng flow riêng;
- thu hồi toàn bộ session sau khi disable hoặc đổi role.

Bắt buộc:

- không cho Admin tự disable chính mình;
- không cho disable/demote Admin ACTIVE cuối cùng;
- tuân thủ DB trigger về role, subRole, campus và department;
- dùng các capability backend như `CanViewDetails`, `CanUpdateRole`, `CanManageStatus`;
- không dùng `isAdmin = ADMIN || StaffLeader`.

## Phiên đăng nhập

Tạo trang đọc dữ liệu từ `user_sessions`:

- user, portal, provider, IP, user agent;
- createdAt, expiresAt, revokedAt;
- trạng thái ACTIVE / EXPIRED / REVOKED;
- revoke một session;
- revoke toàn bộ session của một user.

## Bảo mật

Tạo 2 tab:

### Login Logs

Đọc từ `login_logs`.

### Security Events

Đọc từ `security_events`.

Có filter theo thời gian, kết quả, provider, portal, severity, IP và user.

## Quản lý API

Giữ lại flow API thật hiện có nhưng sửa theo capability:

- chỉ OCR và Translation được Edit/Test/Enable/Quota nếu backend hỗ trợ;
- Google Drive, SMTP hoặc provider dùng environment chỉ hiển thị trạng thái read-only;
- ẩn dữ liệu Coverage/Test ngoài Development/Testing;
- không hiển thị credential, secret, token;
- chỉ cho Enable sau khi test SUCCESS;
- backend trả `canEdit`, `canTest`, `canToggleStatus`, `canConfigureQuota`, `managementSource`.

## Nhật ký kiểm toán

Tạo list/detail từ `audit_logs` và `audit_log_changes`.

Hiển thị:

- actor;
- action;
- entity type/id;
- campus;
- IP;
- requestId;
- thời gian;
- thay đổi trước/sau.

Mask password, token, credential, secret, cookie, refresh token và dữ liệu nhạy cảm.

## Backend

Tạo các query/API cần thiết theo Clean Architecture + CQRS + MediatR:

```text
GET /api/admin/dashboard/summary
GET /api/admin/dashboard/login-activity
GET /api/admin/dashboard/security
GET /api/admin/dashboard/integrations
GET /api/admin/dashboard/recent-audits

GET /api/admin/sessions
POST /api/admin/sessions/{id}/revoke
POST /api/admin/users/{id}/revoke-sessions

GET /api/admin/login-logs
GET /api/admin/security-events
GET /api/admin/audit-logs
GET /api/admin/audit-logs/{id}
```

Tất cả endpoint phải có:

```csharp
[Authorize]
[RoleAuthorize(EffectiveRole.Admin)]
```

Handler vẫn phải kiểm tra quyền lại, không chỉ dựa vào frontend.

## Bảo mật bắt buộc

Ưu tiên xóa hoặc khóa tuyệt đối endpoint:

```text
GET /api/dashboard/debug-user
```

Không được để endpoint anonymous có khả năng sinh JWT theo email.

## Yêu cầu triển khai

- Không thêm dynamic permissions hoặc bảng `permissions/role_permissions`.
- Không thay đổi nghiệp vụ của các role khác.
- Không hard-code số liệu dashboard.
- Có loading, empty, error, retry và responsive.
- Dùng design system hiện tại của PEMS.
- Tất cả thời gian hiển thị theo giờ Việt Nam.
- Không làm mất thay đổi đang có trong working tree.

## Kiểm thử tối thiểu

- Anonymous → 401.
- Non-Admin → 403.
- Admin → xem đúng dữ liệu toàn hệ thống.
- Admin không tự disable chính mình.
- Không disable Admin ACTIVE cuối cùng.
- Disable/đổi role phải revoke session.
- Secret không xuất hiện trong response/UI/log.
- Dashboard không còn mock data.
- Coverage API không xuất hiện ngoài Development/Testing.

Sau khi hoàn thành, báo cáo:

1. file đã sửa;
2. API đã thêm;
3. mock data đã xóa;
4. rule bảo mật đã bổ sung;
5. kết quả build/test thực tế;
6. phần nào chưa triển khai và lý do.
