# Role & Permission Matrix

> **Status:** Revised baseline v0.2.  
> Tài liệu này là nguồn tham chiếu cho backend authorization, frontend menu visibility, UI action control và kiểm thử phân quyền. Khi đặc tả UC hoặc business rule thay đổi, ma trận này phải được cập nhật trước khi sinh SQL/Permission seed thủ công.

## 1. Purpose

File này mô tả ma trận phân quyền theo từng Use Case của hệ thống PEMS. Mỗi UC tương ứng với một `permission_code` trong database. Backend phải kiểm tra quyền dựa trên:

1. `permission_code` của API/action.
2. `permission_level` gồm `F/E/R/O`.
3. `role_code` của user.
4. `sub_role` nếu user thuộc nhóm `STAFF` hoặc `DEPT`.
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
| HO | `role_code = HO` | Quản lý cấp Head Office, gồm campus, FAQ, report, agenda template và một số cấu hình nghiệp vụ. |
| Admin | `role_code = ADMIN` | Quản trị kỹ thuật hệ thống, gồm role, permission, API configuration và API logs. Không phải super admin nghiệp vụ. |
| Staff Leader | `role_code = STAFF`, `sub_role = Leader` | Điều phối/quản lý cấp staff hoặc campus, duyệt request, duyệt news, quản lý account/department trong phạm vi được giao. |
| Staff | `role_code = STAFF`, `sub_role = Staff` | Nhân sự vận hành chính, tạo/cập nhật delegation, chuẩn bị logistics, quản lý partner, tài liệu, ảnh và tin tức. |
| Department Lead | `role_code = DEPT`, `sub_role = Leader` | Trưởng bộ phận, duyệt resource, phân công nhiệm vụ, quản lý personnel và theo dõi coordination tasks. |
| Department | `role_code = DEPT`, `sub_role = Staff` | Nhân sự bộ phận, nhận nhiệm vụ, xác nhận tham gia, cập nhật task và ký báo cáo nếu được phân công. |
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
| DEPT | Leader | Department Lead |
| DEPT | Staff | Department |
| STUDENT | NULL | Student |
| VISITOR | NULL | VISITOR |

Nếu `role_code` là `STAFF` hoặc `DEPT` mà `sub_role` bị thiếu, backend phải coi là dữ liệu không hợp lệ và không được cấp quyền ngầm định.

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

- **Cổng Visitor / Student-facing:** dùng cho VISITOR và các trường hợp sinh viên/khách theo chính sách đăng nhập bằng SSO/FEID. Nếu email Google/FEID thuộc role VISITOR và đăng nhập đúng cổng Visitor thì hệ thống cho phép vào nếu tài khoản đã tồn tại; nếu chưa tồn tại thì hệ thống có thể auto-provision tài khoản VISITOR không gắn campus. Visitor không chọn `selected_campus_id` khi login.
- **Cổng Internal:** dùng cho HO, ADMIN, STAFF, DEPT, STUDENT nội bộ theo cấu hình hệ thống. Cổng này bắt buộc chọn campus khi role cần campus. Nếu email chưa có tài khoản nội bộ trong hệ thống thì không auto-provision và phải từ chối đăng nhập. Nếu tài khoản có role không phù hợp với cổng đang dùng, backend phải trả lỗi rõ ràng, ví dụ: “Tài khoản của bạn không phù hợp với cổng đăng nhập này.”

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

> Nhóm UC lõi cho quản lý đoàn phái, logistics, biên bản, phản hồi, tài liệu và đóng hồ sơ.

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
