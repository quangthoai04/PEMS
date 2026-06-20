<!-- =====================================================================
PEMS DOC UPDATE v8.2-clean-sync-permission-matrix
Generated: 2026-06-20
Mode: FULL DOCUMENT CLEAN SYNC.
UC-136 has been merged into the main Delegation Reception Management matrix.
Parser-risk addendum tables have been converted into prose/reference notes.
===================================================================== -->

# Role & Permission Matrix

> **Status:** Revised baseline v8.2 — aligned with SQL v8.2 strict visit visibility, SSO auto-provisioning, and UC-136 cancellation flow.  
> Tài liệu này là nguồn tham chiếu cho backend authorization, frontend menu visibility, UI action control và kiểm thử phân quyền. Khi đặc tả UC hoặc business rule thay đổi, ma trận này phải được cập nhật trước khi sinh SQL/Permission seed thủ công.

## 1. Purpose

File này mô tả ma trận phân quyền theo từng Use Case của hệ thống PEMS. Mỗi UC tương ứng với một `permission_code` trong database. Backend phải kiểm tra quyền dựa trên:

1. `permission_code` của API/action.
2. `permission_level` gồm `F/E/R/O`.
3. `role_code` của user.
4. `sub_role` nếu user thuộc nhóm `STAFF` hoặc `DEPARTMENT`.
5. Data scope, ownership và trạng thái nghiệp vụ của bản ghi.

> **Lưu ý quan trọng:** `F` không có nghĩa là toàn quyền toàn hệ thống. `F` chỉ là toàn quyền đối với hành động chính của UC đó.

---

## 2. Permission Legend

| Symbol | Meaning | Backend Meaning |
|---|---|---|
| F | Full Permission | Được thực hiện hành động chính của UC, ví dụ tạo mới, quản lý, phân công, cấu hình, xóa hoặc đóng hồ sơ. Vẫn phải kiểm tra scope, trạng thái nghiệp vụ và audit log. |
| E | Execute / Edit Permission | Được xử lý, chỉnh sửa, phê duyệt, cập nhật hoặc đổi trạng thái trong phạm vi nghiệp vụ được giao. Không tự động có quyền tạo/xóa nếu UC khác không cấp. |
| R | Read Permission | Chỉ được xem, tìm kiếm, lọc hoặc truy cập thông tin. Không được thay đổi dữ liệu. |
| O | Own / Personal Permission | Chỉ được thao tác với dữ liệu của chính user hiện tại, ví dụ profile, session, email cá nhân, lịch cá nhân, draft/outbox cá nhân. |
| — | No Permission | Không có quyền truy cập hoặc thực hiện UC này. Frontend nên ẩn chức năng và backend phải trả 403 nếu gọi trực tiếp. |

---

## 3. Role Scope

| Role in Matrix | DB Mapping | Scope |
|---|---|---|
| HO | `role_code = HO` | Head Office xử lý nghiệp vụ cấp liên cơ sở: xem/duyệt `MULTI_CAMPUS`, campus master, FAQ, report, agenda template và một số cấu hình nghiệp vụ. HO **được xem `SINGLE_CAMPUS` ở chế độ read-only để theo dõi** (chốt 2026-06) nhưng **không xử lý** `SINGLE_CAMPUS` (không duyệt/từ chối/gán host/hủy). |
| Admin | `role_code = ADMIN` | Quản trị kỹ thuật hệ thống, gồm role, permission, API configuration và API logs. Không phải super admin nghiệp vụ và không xem visit/delegation records. |
| Staff Leader | `role_code = STAFF`, `sub_role = Leader` | Điều phối cấp campus: xem/xử lý `SINGLE_CAMPUS` thuộc campus mình; xem `MULTI_CAMPUS` chỉ sau khi HO duyệt/release và chỉ với campus mình; duyệt news, quản lý account/department trong phạm vi được giao. |
| Staff | `role_code = STAFF`, `sub_role = Staff` | Nhân sự vận hành chính, tạo/cập nhật delegation, chuẩn bị logistics, quản lý partner, tài liệu, ảnh và tin tức. |
| Department Lead | `role_code = DEPARTMENT`, `sub_role = Leader` | Trưởng bộ phận, duyệt resource, phân công nhiệm vụ, quản lý personnel và theo dõi coordination tasks. |
| Department | `role_code = DEPARTMENT`, `sub_role = Staff` | Nhân sự bộ phận, nhận nhiệm vụ, xác nhận tham gia, cập nhật task và ký báo cáo nếu được phân công. |
| Student | `role_code = STUDENT` | Vai trò hỗ trợ, có thể tham gia đoàn phái, tạo minutes, gửi feedback, upload ảnh hoặc tạo news khi được giao. |
| VISITOR | `role_code = VISITOR` | Khách bên ngoài, chủ yếu gửi visit request, xem thông tin được cấp quyền và sử dụng email trong phạm vi tài khoản của mình. |

### 3.1. Effective Role Rule

Backend nên resolve effective role như sau:

| `role_code` | `sub_role` | Effective Role |
|---|---|---|
| ADMIN | NULL | Admin |
| HO | NULL | HO |
| STAFF | Leader | Staff Leader |
| STAFF | Staff | Staff |
| DEPARTMENT | Leader | Department Lead |
| DEPARTMENT | Staff | Department |
| STUDENT | NULL | Student |
| VISITOR | NULL | VISITOR |

Nếu `role_code` là `STAFF` hoặc `DEPARTMENT` mà `sub_role` bị thiếu, backend phải coi là dữ liệu không hợp lệ và không được cấp quyền ngầm định.

---

## 4. General Authorization Rules

- Frontend dùng ma trận này để ẩn/hiện menu, page, button và action.
- Backend luôn là lớp kiểm tra quyền cuối cùng, không được chỉ dựa vào frontend.
- Mọi API cần kiểm tra role permission, data scope, trạng thái nghiệp vụ hiện tại và quyền trên bản ghi cụ thể.
- Các thao tác create, update, delete, approve, reject, assign, publish, close hoặc change status cần có audit log.
- Các UC có quyền `O` chỉ được xử lý dữ liệu của chính user hiện tại hoặc dữ liệu mà user là owner/participant hợp lệ.
- Các UC có quyền `R` không được cho phép thay đổi dữ liệu.
- Các UC có quyền `—` phải bị chặn ở backend kể cả khi user gọi API trực tiếp.
- `F` chỉ full trong phạm vi UC đó, không tự động bao gồm UC khác.
- Nếu một API thực hiện nhiều hành động, backend phải kiểm tra đủ permission tương ứng với từng hành động hoặc tách API.


### 4.0. Strict Visit / Delegation Visibility Rule

Permission level `R` hoặc `E` trong nhóm Delegation Reception Management chỉ là điều kiện cần. Backend vẫn phải áp dụng data scope sau:

| Role | `SINGLE_CAMPUS` | `MULTI_CAMPUS` pending HO | `MULTI_CAMPUS` after HO approval/release |
|---|---:|---:|---:|
| ADMIN | No access | No access | No access |
| HO | View (read-only, monitor) | View/decide | View |
| Staff Leader, same campus | View/decide | No access | View own campus instance |
| Staff Leader, other campus | No access | No access | No access |
| Staff / Department / Student / VISITOR | Only if assigned, participant, owner, or explicitly linked | No access unless linked by a valid rule after release | Only within assigned/linked scope |

Implementation source query:

- HO list/detail: `vw_visit_requests_for_ho` and `visit_scope = 'MULTI_CAMPUS'`.
- Staff Leader list/detail: `vw_visit_requests_for_staff_leader` plus `visible_campus_id = CurrentUser.PrimaryCampusId`.
- ADMIN list/detail: no route or `vw_visit_requests_for_admin`, which returns zero rows.

`approval_display_status` such as `WAITING_HO_APPROVAL`, `HO_APPROVED`, `WAITING_STAFF_LEADER_APPROVAL`, and `STAFF_LEADER_APPROVED` is a display/status-label concept. It must not replace the lifecycle column `visit_requests.status`.

### 4.1. Public Endpoint Rule

Một số UC trong nhóm Common có thể là public endpoint. Nếu endpoint được thiết kế public, backend **không cần gắn `RequirePermission`**, nhưng vẫn phải filter dữ liệu theo trạng thái `published/visible/active`.

Ví dụ:

- Public homepage.
- Public FAQ.
- Public news.
- Public gallery.
- Public contact information.

Nếu endpoint yêu cầu đăng nhập để xem bản nội bộ, backend mới áp dụng permission `R` theo ma trận.

### 4.2. Pre-auth Endpoint Rule

Các UC sau là pre-auth endpoint nên **không được check RBAC bằng `RequirePermission` trước khi user đăng nhập**:

- UC-10 Login via SSO.
- UC-11 Login via Credentials.
- UC-13 Forgot Password.

Các endpoint này phải được bảo vệ bằng rule bảo mật khác, ví dụ:

- Account status.
- Portal validation.
- Rate limit.
- CAPTCHA nếu cần.
- Lockout policy.
- Audit/security log.

UC-12 Logout có thể kiểm tra user đã authenticated/session hợp lệ, nhưng không nên phụ thuộc vào quyền nghiệp vụ.


### 4.3. Login Portal & Provisioning Rule

Hệ thống có hai nhóm cổng đăng nhập chính:

- **Cổng Visitor / Student-facing:** dùng cho VISITOR và các trường hợp sinh viên/khách theo chính sách đăng nhập bằng SSO/FEID. Nếu email Google/FEID thuộc role VISITOR và đăng nhập đúng cổng Visitor thì hệ thống cho phép vào nếu tài khoản đã tồn tại; nếu chưa tồn tại thì hệ thống có thể auto-provision tài khoản VISITOR không gắn campus, không department, không sub_role và lưu `users.created_via = 'SSO_AUTO_PROVISION'`. Visitor không chọn `selected_campus_id` khi login.
- **Cổng Internal:** dùng cho HO, ADMIN, STAFF, DEPARTMENT, STUDENT nội bộ theo cấu hình hệ thống. Cổng này bắt buộc chọn campus khi role cần campus. Nếu email chưa có tài khoản nội bộ trong hệ thống thì không auto-provision và phải từ chối đăng nhập. Nếu tài khoản có role không phù hợp với cổng đang dùng, backend phải trả lỗi rõ ràng, ví dụ: “Tài khoản của bạn không phù hợp với cổng đăng nhập này.”

Nếu một tài khoản VISITOR cần chuyển sang role nội bộ, Staff Leader hoặc role được cấp quyền phù hợp phải dùng UC-100 Update Account Role hoặc UC-96 Create Account để gán role, sub_role, campus và department hợp lệ. Sau khi chuyển role nội bộ, user phải đăng nhập qua cổng Internal và chọn đúng campus.

---

## 5. Permission Matrix By Feature Area

### 5.1. Common

> Các UC dùng cho nội dung công khai và truy cập cơ bản. Nội dung public chỉ nên lấy dữ liệu đã được published/visible. Nếu endpoint là public thì không cần `RequirePermission`; nếu endpoint là nội bộ thì áp dụng `R` theo bảng.

| UC ID | Action | HO | Admin | Staff Leader | Staff | Department Lead | Department | Student | VISITOR |
|---|---|:---:|:---:|:---:|:---:|:---:|:---:|:---:|:---:|
| UC-01 | View Homepage | R | R | R | R | R | R | R | R |
| UC-02 | Search Information | R | R | R | R | R | R | R | R |
| UC-03 | View Contact Info | R | R | R | R | R | R | R | R |
| UC-04 | View Policy & Terms | R | R | R | R | R | R | R | R |
| UC-05 | View FAQ | R | R | R | R | R | R | R | R |
| UC-06 | View News | R | R | R | R | R | R | R | R |
| UC-07 | View Partners | R | R | R | R | R | R | R | R |
| UC-08 | View Gallery | R | R | R | R | R | R | R | R |
| UC-09 | View Notifications | R | R | R | R | R | R | R | R |

### 5.2. Authentication

> UC-10, UC-11 và UC-13 là pre-auth endpoint, không gắn `RequirePermission` trước khi login. Level `O` trong bảng dùng để thể hiện đây là thao tác cá nhân của tài khoản, không phải quyền nghiệp vụ. Trong giai đoạn triển khai/dev, hệ thống có thể cho phép đăng nhập bằng mật khẩu local, SSO và FEID để kiểm thử. Khi build hệ thống thật/production, cơ chế chính là SSO/FEID theo đúng cổng đăng nhập; LOCAL_PASSWORD chỉ giữ cho DEV/test hoặc trường hợp đặc biệt. Cần kiểm tra portal, role, campus, trạng thái tài khoản, token/session, rate limit và ghi log đăng nhập/đăng xuất.

| UC ID | Action | HO | Admin | Staff Leader | Staff | Department Lead | Department | Student | VISITOR |
|---|---|:---:|:---:|:---:|:---:|:---:|:---:|:---:|:---:|
| UC-10 | Login via SSO | O | O | O | O | O | O | O | O |
| UC-11 | Login via Credentials | O | O | O | O | O | O | O | O |
| UC-12 | Logout | O | O | O | O | O | O | O | O |
| UC-13 | Forgot Password | O | O | O | O | O | O | O | O |

### 5.3. Profile Management

> Các UC hồ sơ cá nhân. Người dùng chỉ được thao tác trên dữ liệu của chính mình, không tự đổi role/campus/status.

| UC ID | Action | HO | Admin | Staff Leader | Staff | Department Lead | Department | Student | VISITOR |
|---|---|:---:|:---:|:---:|:---:|:---:|:---:|:---:|:---:|
| UC-14 | View Profile | O | O | O | O | O | O | O | O |
| UC-15 | Update Profile | O | O | O | O | O | O | O | O |
| UC-16 | Change Password | O | O | O | O | O | O | O | O |

### 5.4. Delegation Reception Management

> Nhóm UC lõi cho quản lý đoàn phái, logistics, biên bản, phản hồi, tài liệu và đóng hồ sơ. Admin không có quyền nghiệp vụ trong nhóm này. HO chỉ áp dụng cho `MULTI_CAMPUS`. Staff Leader chỉ áp dụng theo campus scope. Các role khác phải có assignment/ownership/participation/link hợp lệ.

| UC ID | Action | HO | Admin | Staff Leader | Staff | Department Lead | Department | Student | VISITOR |
|---|---|:---:|:---:|:---:|:---:|:---:|:---:|:---:|:---:|
| UC-17 | Submit Visit Request | — | — | — | — | — | — | — | F |
| UC-18 | Approve Cross-Campus Request | E | — | — | — | — | — | — | — |
| UC-19 | View Guest Delegation Details | R | — | R | R | R | R | R | R |
| UC-20 | View Guest Delegation List | R | — | R | R | R | R | R | R |
| UC-21 | Search Delegations | R | — | R | R | R | R | R | R |
| UC-22 | Process Visit Request | — | — | E | — | — | — | — | — |
| UC-23 | Create Guest Delegation | — | — | — | F | — | — | — | — |
| UC-24 | Update Guest Delegation | — | — | — | F | — | — | — | — |
| UC-25 | Prepare Visit Logistics | — | — | R | F | — | — | — | — |
| UC-26 | Update Visit Logistics | — | — | R | F | — | — | — | — |
| UC-27 | Confirm Participation | — | — | — | E | E | E | E | — |
| UC-28 | Approve Resource Request | — | — | — | — | F | — | — | — |
| UC-29 | Propose Resource Modification | — | — | — | — | F | F | — | — |
| UC-30 | Confirm The Change Proposal | — | — | — | E | R | R | — | — |
| UC-31 | Create Meeting Minutes | — | — | — | F | F | F | F | — |
| UC-32 | Edit Meeting Minutes | — | — | — | F | F | F | F | — |
| UC-33 | View Meeting Minutes Details | R | — | R | R | R | R | R | — |
| UC-34 | Submit Delegation Feedback | — | — | — | F | F | F | F | — |
| UC-35 | Scan Business Card | — | — | — | F | — | — | — | — |
| UC-36 | Create Partner Profile | — | — | — | F | — | — | — | — |
| UC-37 | Upload Attached Documents | — | — | — | F | — | — | — | — |
| UC-38 | Upload Visit Photos | — | — | — | F | — | — | F | — |
| UC-39 | Tag Faces on Photos | — | — | — | F | — | — | — | — |
| UC-40 | Create News Article | — | — | — | F | — | — | F | — |
| UC-41 | Close Delegation | — | — | — | F | — | — | — | — |
| UC-136 | Cancel Visit Request | E | — | E | O | — | — | — | O |


#### Delegation Data Scope Notes

- UC-18 `Approve Cross-Campus Request`: HO only, `visit_scope = 'MULTI_CAMPUS'`.
- UC-22 `Process Visit Request`: Staff Leader only, `visit_scope = 'SINGLE_CAMPUS'` and `vrc.campus_id = CurrentUser.PrimaryCampusId`.
- UC-136 `Cancel Visit Request`: HO only for `MULTI_CAMPUS`; Staff Leader only within `CurrentUser.PrimaryCampusId`; Staff only as valid host/assignment owner; Visitor only for their own request.
- UC-19/UC-20/UC-21: read/search/list must not query `visit_requests` directly without data scope. Use the SQL v8.2 visibility views or equivalent backend predicates.
- ADMIN must stay `—` for UC-17 to UC-41 and UC-136, and must not receive indirect visit/cancellation access through dashboard/search/detail/cancel APIs.
- Display labels such as `HO_APPROVED` are derived from `visit_scope`, `status`, and `decision_actor_role`; they are not permission codes.

### 5.5. Email Management

> Email trong hệ thống là chức năng soạn, gửi, xem và phản hồi email. Email **không bắt buộc** phải gắn với delegation/visit request.  
> Quyền `O` nghĩa là user chỉ được thao tác trên email/draft/outbox/conversation của chính mình hoặc email mà user là participant hợp lệ. UC-48 View Email cũng dùng `O` để tránh hiểu nhầm rằng user có thể đọc toàn bộ email hệ thống. Nếu email có liên kết với visit request/delegation thì backend phải kiểm tra thêm user có quyền trên visit request/delegation đó.

| UC ID | Action | HO | Admin | Staff Leader | Staff | Department Lead | Department | Student | VISITOR |
|---|---|:---:|:---:|:---:|:---:|:---:|:---:|:---:|:---:|
| UC-42 | View Email Template List | R | — | — | — | — | — | — | — |
| UC-43 | View Email Template Detail | R | — | — | — | — | — | — | — |
| UC-44 | Update Email Template | E | — | — | — | — | — | — | — |
| UC-45 | Create Email Template | F | — | — | — | — | — | — | — |
| UC-46 | Edit Email Content | O | — | O | O | O | O | O | O |
| UC-47 | Send Email | O | — | O | O | O | O | O | O |
| UC-48 | View Email | O | — | O | O | O | O | O | O |
| UC-49 | Reply to Email | O | — | O | O | O | O | O | O |

### 5.6. Partner Management

> Quản lý thông tin đối tác và yêu cầu tạo đối tác. Cần kiểm tra trùng tổ chức/người liên hệ trước khi duyệt hoặc tạo mới.

| UC ID | Action | HO | Admin | Staff Leader | Staff | Department Lead | Department | Student | VISITOR |
|---|---|:---:|:---:|:---:|:---:|:---:|:---:|:---:|:---:|
| UC-50 | Process Partner Creation Request | — | — | E | — | — | — | — | — |
| UC-51 | Edit Partner Information | — | — | — | E | — | — | — | — |
| UC-52 | View Partner Lists | — | — | R | R | — | — | — | — |
| UC-53 | Search Partners | — | — | R | R | — | — | — | — |
| UC-54 | View Partner Details | — | — | R | R | — | — | — | — |

### 5.7. Document Management

> Quản lý danh sách và tìm kiếm tài liệu theo quyền truy cập của người dùng.

| UC ID | Action | HO | Admin | Staff Leader | Staff | Department Lead | Department | Student | VISITOR |
|---|---|:---:|:---:|:---:|:---:|:---:|:---:|:---:|:---:|
| UC-55 | View Document List | — | — | R | R | — | — | — | — |
| UC-56 | Search Documents | — | — | R | R | — | — | — | — |

### 5.8. Gallery Management

> Quản lý thư viện ảnh/gallery. Chỉ nội dung visible/published mới được hiển thị public.

| UC ID | Action | HO | Admin | Staff Leader | Staff | Department Lead | Department | Student | VISITOR |
|---|---|:---:|:---:|:---:|:---:|:---:|:---:|:---:|:---:|
| UC-57 | View Gallery Item List | — | — | R | — | — | — | — | — |
| UC-58 | Search Gallery Items | — | — | R | — | — | — | — | — |
| UC-59 | Add Gallery Item | — | — | F | — | — | — | — | — |
| UC-60 | Update Gallery Item | — | — | E | — | — | — | — | — |
| UC-61 | Delete Gallery Item | — | — | F | — | — | — | — | — |

### 5.9. Minutes Management

> Quản lý danh sách và tìm kiếm biên bản họp đã lưu trữ theo quyền xem.

| UC ID | Action | HO | Admin | Staff Leader | Staff | Department Lead | Department | Student | VISITOR |
|---|---|:---:|:---:|:---:|:---:|:---:|:---:|:---:|:---:|
| UC-62 | View Minutes List | — | — | R | R | — | — | — | — |
| UC-63 | Search/Filter Minutes | — | — | R | R | — | — | — | — |

### 5.10. FAQ Management

> Quản lý FAQ, nội dung song ngữ và trạng thái hiển thị public.

| UC ID | Action | HO | Admin | Staff Leader | Staff | Department Lead | Department | Student | VISITOR |
|---|---|:---:|:---:|:---:|:---:|:---:|:---:|:---:|:---:|
| UC-64 | View List FAQ | R | — | — | — | — | — | — | — |
| UC-65 | Create FAQ | F | — | — | — | — | — | — | — |
| UC-66 | Update FAQ | E | — | — | — | — | — | — | — |
| UC-67 | Change FAQ Visibility | E | — | — | — | — | — | — | — |
| UC-68 | Search FAQ | R | — | — | — | — | — | — | — |

### 5.11. Report Management

> Quản lý thống kê và xuất báo cáo theo phạm vi role, campus và thời gian.

| UC ID | Action | HO | Admin | Staff Leader | Staff | Department Lead | Department | Student | VISITOR |
|---|---|:---:|:---:|:---:|:---:|:---:|:---:|:---:|:---:|
| UC-69 | View Dashboard Statistics | R | — | R | — | R | — | — | — |
| UC-70 | Export Statistics Report | E | — | E | — | E | — | — | — |
| UC-71 | Filter Dashboard By Time | R | — | R | — | R | — | — | — |

### 5.12. Calendar Management

> Quản lý lịch cá nhân, lịch bộ phận và sự kiện liên quan đến đoàn phái. `View My Events` là dữ liệu cá nhân nên dùng `O`; các quyền xem lịch chung/sự kiện theo phạm vi dùng `R`.

| UC ID | Action | HO | Admin | Staff Leader | Staff | Department Lead | Department | Student | VISITOR |
|---|---|:---:|:---:|:---:|:---:|:---:|:---:|:---:|:---:|
| UC-72 | View My Events | — | — | O | O | O | O | O | — |
| UC-73 | View Department Calendar | — | — | R | R | — | — | — | — |
| UC-74 | Switch View Mode | — | — | R | R | R | R | R | — |
| UC-75 | Add Personal Event | — | — | O | O | — | — | — | — |
| UC-76 | Delete Personal Event | — | — | O | O | — | — | — | — |
| UC-77 | Update Personal Event | — | — | O | O | — | — | — | — |
| UC-78 | View Event Details | — | — | R | R | R | R | R | — |

### 5.13. Feedback Management

> Xem, lọc và tổng hợp phản hồi theo đoàn phái, vai trò và phạm vi quyền.

| UC ID | Action | HO | Admin | Staff Leader | Staff | Department Lead | Department | Student | VISITOR |
|---|---|:---:|:---:|:---:|:---:|:---:|:---:|:---:|:---:|
| UC-79 | Search/Filter Feedback | — | — | R | R | — | — | — | — |
| UC-80 | View Feedback Summary | — | — | R | R | — | — | — | — |

### 5.14. Campus Management

> Quản lý dữ liệu master về campus. Thay đổi campus có thể ảnh hưởng đến phân công tài khoản, đoàn phái và báo cáo.

| UC ID | Action | HO | Admin | Staff Leader | Staff | Department Lead | Department | Student | VISITOR |
|---|---|:---:|:---:|:---:|:---:|:---:|:---:|:---:|:---:|
| UC-81 | Add New Campus | F | — | — | — | — | — | — | — |
| UC-82 | View Campus List | R | — | — | — | — | — | — | — |
| UC-83 | Search and Filter Campus | R | — | — | — | — | — | — | — |
| UC-84 | View Campus Details | R | — | — | — | — | — | — | — |
| UC-85 | Update Campus | E | — | — | — | — | — | — | — |
| UC-86 | Manage Campus Status | E | — | — | — | — | — | — | — |
| UC-87 | Assign Campus Lead | F | — | — | — | — | — | — | — |

### 5.15. News Management

> Quản lý tin tức, duyệt bài, xuất bản, ẩn/hiện và tin song ngữ. Student có thể tạo/chỉnh sửa nội dung khi được giao, nhưng không được publish trực tiếp. Publish nên do Staff vận hành hoặc theo quy trình đã được duyệt.

| UC ID | Action | HO | Admin | Staff Leader | Staff | Department Lead | Department | Student | VISITOR |
|---|---|:---:|:---:|:---:|:---:|:---:|:---:|:---:|:---:|
| UC-88 | Approve News | — | — | E | — | — | — | — | — |
| UC-89 | Publish News | — | — | — | F | — | — | — | — |
| UC-90 | View News List | — | — | R | R | — | — | R | — |
| UC-91 | View News Details | — | — | R | R | — | — | R | — |
| UC-92 | Add Multilingual News | — | — | — | F | — | — | F | — |
| UC-93 | Manage News Visibility | — | — | E | — | — | — | — | — |
| UC-94 | Edit News | — | — | — | E | — | — | E | — |

### 5.16. Account Management

> Quản lý tài khoản, trạng thái và role. Không hiển thị mật khẩu, token hoặc dữ liệu xác thực nhạy cảm.

| UC ID | Action | HO | Admin | Staff Leader | Staff | Department Lead | Department | Student | VISITOR |
|---|---|:---:|:---:|:---:|:---:|:---:|:---:|:---:|:---:|
| UC-95 | View Account List | R | — | R | — | — | — | — | — |
| UC-96 | Create Account | F | — | F | — | — | — | — | — |
| UC-97 | Manage Account Status | E | — | E | — | — | — | — | — |
| UC-98 | View Account Details | R | — | R | — | — | — | — | — |
| UC-99 | Search and Filter Accounts | R | — | R | — | — | — | — | — |
| UC-100 | Update Account Role | — | — | E | — | — | — | — | — |

### 5.17. Department Management

> Quản lý phòng ban, nhân sự bộ phận, nhiệm vụ điều phối và ký báo cáo bàn giao dịch vụ.

| UC ID | Action | HO | Admin | Staff Leader | Staff | Department Lead | Department | Student | VISITOR |
|---|---|:---:|:---:|:---:|:---:|:---:|:---:|:---:|:---:|
| UC-101 | Add New Department | — | — | F | — | — | — | — | — |
| UC-102 | Update Department | — | — | F | — | — | — | — | — |
| UC-103 | Search and Filter Departments | — | — | R | — | — | — | — | — |
| UC-104 | View Department List | — | — | R | — | — | — | — | — |
| UC-105 | View Department Details | — | — | R | — | R | R | — | — |
| UC-106 | Manage Department Status | — | — | E | — | — | — | — | — |
| UC-107 | Add Department Personnel | — | — | — | — | F | — | — | — |
| UC-108 | View Personnel Details | — | — | — | — | R | R | — | — |
| UC-109 | Search Personnel | — | — | — | — | R | R | — | — |
| UC-110 | Review Assigned Tasks | — | — | — | — | — | E | — | — |
| UC-111 | Assign Tasks | — | — | — | — | F | — | — | — |
| UC-112 | Sign The Service Delivery Report | — | — | — | E | E | E | — | — |
| UC-113 | Remove Personnel | — | — | — | — | F | — | — | — |
| UC-114 | View Coordination Tasks | — | — | — | — | R | R | — | — |
| UC-115 | Search Coordination Tasks | — | — | — | — | R | R | — | — |
| UC-116 | Reassign Department Lead | — | — | — | — | F | — | — | — |

### 5.18. Role & Permission Management

> Quản lý vai trò và ma trận quyền. Đây là nhóm chức năng có rủi ro cao, chỉ dành cho Admin kỹ thuật. Admin không tự động có quyền nghiệp vụ khác.

| UC ID | Action | HO | Admin | Staff Leader | Staff | Department Lead | Department | Student | VISITOR |
|---|---|:---:|:---:|:---:|:---:|:---:|:---:|:---:|:---:|
| UC-117 | View Role List | — | R | — | — | — | — | — | — |
| UC-118 | Create New Role | — | F | — | — | — | — | — | — |
| UC-119 | Configure Role Permissions | — | F | — | — | — | — | — | — |
| UC-120 | Update Role Details | — | F | — | — | — | — | — | — |
| UC-121 | Disable/Delete Role | — | F | — | — | — | — | — | — |

### 5.19. API Management

> Quản lý cấu hình API, trạng thái, giới hạn request và log. Secret/token cần được mã hóa hoặc che giấu.

| UC ID | Action | HO | Admin | Staff Leader | Staff | Department Lead | Department | Student | VISITOR |
|---|---|:---:|:---:|:---:|:---:|:---:|:---:|:---:|:---:|
| UC-122 | View API Configuration | — | R | — | — | — | — | — | — |
| UC-123 | Create API Configuration | — | F | — | — | — | — | — | — |
| UC-124 | Update API Configuration | — | E | — | — | — | — | — | — |
| UC-125 | Delete API Configuration | — | E | — | — | — | — | — | — |
| UC-126 | Test API Connection | — | F | — | — | — | — | — | — |
| UC-127 | Manage API Status | — | F | — | — | — | — | — | — |
| UC-128 | Configure Request Limit | — | F | — | — | — | — | — | — |
| UC-129 | View API Logs | — | R | — | — | — | — | — | — |
| UC-130 | Search API Logs | — | R | — | — | — | — | — | — |

### 5.20. Agenda Templates Management

> Quản lý mẫu agenda/lịch trình dùng lại cho đoàn phái. Cần ưu tiên soft delete để giữ lịch sử sử dụng.

| UC ID | Action | HO | Admin | Staff Leader | Staff | Department Lead | Department | Student | VISITOR |
|---|---|:---:|:---:|:---:|:---:|:---:|:---:|:---:|:---:|
| UC-131 | Create Agenda Template | F | — | — | — | — | — | — | — |
| UC-132 | Update Agenda Template | E | — | — | — | — | — | — | — |
| UC-133 | Delete Agenda Template | F | — | — | — | — | — | — | — |
| UC-134 | View Agenda Template List | R | — | — | — | — | — | — | — |
| UC-135 | View Agenda Template Detail | R | — | — | — | — | — | — | — |

---

## 6. Backend Enforcement Notes

### 6.1. Required Claims / Current User Context

Backend authorization cần có tối thiểu các thông tin sau từ JWT hoặc `CurrentUserService`:

| Field | Purpose |
|---|---|
| `UserId` | Check ownership, audit log và data scope. |
| `RoleCode` | Check role-level permission. |
| `SubRole` | Phân biệt Staff Leader/Staff và Department Lead/Department. |
| `CampusId` | Check campus scope. |
| `DepartmentId` | Check department/task/resource scope. |
| `Status` | Chặn account inactive/locked. |

### 6.2. Authorization Flow

Backend nên xử lý theo thứ tự:

1. Authenticate user/session nếu endpoint không phải public/pre-auth.
2. Resolve effective role từ `role_code` + `sub_role`.
3. Check `permission_code` có grant không.
4. Check `permission_level` đúng với hành động.
5. Check ownership nếu level là `O`.
6. Check campus/department/delegation/email/news scope.
7. Check business status.
8. Ghi audit log với các thao tác mutate data.

### 6.3. Email Scope Rule

Email không bắt buộc phải gắn với delegation. Backend áp dụng scope như sau:

- Email cá nhân/draft/outbox: user chỉ được thao tác email của chính mình.
- Email conversation: user chỉ được xem/trả lời nếu là sender, recipient, cc/bcc hợp lệ hoặc participant được hệ thống ghi nhận.
- Email gắn với visit request/delegation: ngoài email scope, phải check thêm quyền/scope với visit request/delegation liên quan.
- Visitor có thể soạn/gửi/trả lời email trong hệ thống theo quyền `O`, nhưng không được xem hoặc thao tác email của người khác.

### 6.4. Public Content Rule

Với public homepage/news/FAQ/gallery/partner/contact, endpoint public không dùng `RequirePermission`, nhưng query phải chỉ trả dữ liệu:

- `published` hoặc `visible`.
- `active`.
- Không bị soft delete.
- Không chứa dữ liệu nội bộ hoặc nhạy cảm.

### 6.5. News Student Rule

Student có thể tạo hoặc chỉnh sửa nội dung tin tức khi được giao, nhưng không được publish trực tiếp. Nếu cần cho Student publish trong tương lai, phải thêm rule rõ:

- Chỉ publish bài của chính mình.
- Bài phải ở trạng thái approved.
- Có audit log.
- Có thể cần UC riêng để phân biệt submit draft và publish.

---

## 7. Change Log

| Version | Date | Description |
|---|---|---|
| v0.1 | 2026-06-15 | Initial draft permission matrix formatted by feature area. |
| v0.2 | 2026-06-18 | Revised backend enforcement rules; clarified pre-auth/public endpoints; added effective role mapping for `sub_role`; changed UC-44 HO F→E, UC-64 HO F→R, UC-72 R→O; removed Student publish permission UC-89; clarified email scope is not mandatory delegation-based. |
| v0.3 | 2026-06-19 | Aligned with SQL v5: added `SSO_AUTO_PROVISION` rule; formalized ADMIN no visit access; limited HO to `MULTI_CAMPUS`; limited Staff Leader to campus scope; clarified visit access views and display status labels; kept UC-48 `O` own-scope. |
| v8.2 | 2026-06-20 | Merged UC-136 into the main Delegation Reception Management matrix; updated document status from SQL v5 to SQL v8.2; converted parser-risk UC-136 addendum permission table into reference notes. |

---

# Reference Notes — UC-136 Cancellation Flow


## V8.2 Reference — UC-136 Cancel Visit Request thuộc Delegation Reception Management

> Phần này là ghi chú triển khai bổ sung cho UC-136. Quyền chính thức của UC-136 đã được đưa vào bảng chính tại mục **5.4 Delegation Reception Management**. Nếu tài liệu cũ còn flow “đã duyệt nhưng chưa có host” hoặc “mỗi cơ sở duyệt lại sau HO”, ưu tiên rule V8.2 ở phần này.

### 1. Feature ownership

UC hủy đơn thăm thuộc **FE-02 — Quản lý Tiếp đón Đoàn khách / Delegation Reception Management** vì đây là thao tác trên vòng đời đoàn/visit request, không phải bước submit form.

```text
Feature: FE-02 Delegation Reception Management
UC: UC-136 Cancel Visit Request
Permission code: UC-136.CANCEL_VISIT_REQUEST
```

### 2. Không dùng `external_confirmation_note`

Không tạo cột `external_confirmation_note`. Khi Host hủy thay khách dựa trên xác nhận ngoài hệ thống, toàn bộ thông tin xác nhận được ghi vào `cancellation_reason`.

```text
cancellation_source = EXTERNAL_CONFIRMATION
cancellation_reason = "Khách xác nhận hủy qua email/điện thoại/Zalo..., thời gian..., người xác nhận..., lý do..."
```

### 3. Cancellation metadata chuẩn

Áp dụng cho `visit_requests` và `visit_request_campuses`:

```sql
cancelled_by BIGINT UNSIGNED NULL,
cancelled_at DATETIME NULL,
cancellation_actor_type ENUM('VISITOR','HOST','STAFF_LEADER','HO','SYSTEM') NULL,
cancellation_source ENUM('SELF_SERVICE','EXTERNAL_CONFIRMATION','INTERNAL_DECISION') NULL,
cancellation_reason TEXT NULL
```

### 4. Meaning của `cancellation_source`

| Value | Meaning | Khi dùng |
|---|---|---|
| `SELF_SERVICE` | Người dùng tự thao tác trên hệ thống | Visitor tự hủy đơn của chính họ |
| `EXTERNAL_CONFIRMATION` | Hủy dựa trên xác nhận ngoài hệ thống | Host hủy thay khách sau khi khách xác nhận qua email/điện thoại/Zalo/gặp trực tiếp |
| `INTERNAL_DECISION` | Nội bộ hủy vì lý do vận hành | HO/Staff Leader hủy vì campus không thể tiếp, trùng lịch, lý do tổ chức |

### 5. Rule hủy theo role

| Actor | Scope | Nguồn hủy hợp lệ | Ghi chú |
|---|---|---|---|
| Visitor | Đơn của chính họ | `SELF_SERVICE` | Chỉ hủy khi chưa vào giai đoạn `DURING_VISIT`, `AFTER_VISIT`, `CLOSED` |
| Host | Campus instance mình đang phụ trách | `EXTERNAL_CONFIRMATION` | Bắt buộc nhập `cancellation_reason` rõ kênh/thời điểm/người xác nhận |
| Staff Leader | Đơn/campus thuộc campus mình | `INTERNAL_DECISION` hoặc `EXTERNAL_CONFIRMATION` | Không xử lý campus khác |
| HO | `MULTI_CAMPUS` | `INTERNAL_DECISION` hoặc `EXTERNAL_CONFIRMATION` | Có thể hủy request tổng liên cơ sở nếu nghiệp vụ cho phép |
| Admin | Không có quyền nghiệp vụ visit/delegation | Không áp dụng | ADMIN không được hủy delegation |

### 6. Rule trạng thái

- `visit_requests.status = CANCELLED` dùng khi hủy request/delegation tổng.
- `visit_request_campuses.status = CANCELLED` dùng khi hủy một campus instance.
- Không cho hủy campus instance nếu đã vào `DURING_VISIT`, `AFTER_VISIT`, hoặc `CLOSED`.
- Không dùng `CANCELLED` thay cho `REJECTED`. Nếu đơn đang `PENDING_APPROVAL` và người duyệt không chấp nhận, dùng reject flow.

### 7. Vị trí code Clean Architecture

```text
PEMS.Application/Delegations/Commands/CancelVisitRequest/
├── CancelVisitRequestCommand.cs
├── CancelVisitRequestCommandHandler.cs
├── CancelVisitRequestCommandValidator.cs
└── CancelVisitRequestResponse.cs
```

Controller chỉ nhận request và gọi `IMediator`. Logic kiểm tra scope, current host, request/campus status, và cancellation metadata nằm trong Handler/Domain Entity.


## UC-136 Permission Reference

- Main matrix row: `| UC-136 | Cancel Visit Request | E | — | E | O | — | — | — | O |`
- Permission code: `UC-136.CANCEL_VISIT_REQUEST`
- Permission group: `Delegation Reception Management`
- Scope note: Visitor hủy đơn của chính họ; Host/Staff hủy trong campus/assignment scope; HO xử lý multi-campus; Admin không có nghiệp vụ visit/delegation.

## Seed permission đề xuất

```sql
INSERT INTO permissions (permission_code, name, permission_group, description, is_system)
VALUES (
  'UC-136.CANCEL_VISIT_REQUEST',
  'Cancel Visit Request',
  'Delegation Reception Management',
  'Cancel a visit request or a campus instance within valid scope and lifecycle status',
  TRUE
);
```

## Effective scope bổ sung

- `Visitor = O`: chỉ request có `visitor_user_id = CurrentUser.UserId` hoặc email/ownership đã xác minh.
- `Staff = O`: chỉ khi đang là `current_host_user_id` hoặc được liên kết hợp lệ với campus instance.
- `Staff Leader = E`: chỉ campus của `CurrentUser.PrimaryCampusId`.
- `HO = E`: chỉ `MULTI_CAMPUS`.
- `Admin = —`: technical admin không hủy visit/delegation.
