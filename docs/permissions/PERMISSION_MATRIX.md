# Role & Permission Matrix

> **Status:** Draft baseline. Các quyền trong file này dùng làm bản khởi tạo để thiết kế hệ thống. Cần kiểm định lại sau khi đặc tả từng UC và business rule hoàn tất.

## 1. Purpose

File này mô tả ma trận phân quyền theo từng Use Case. Ma trận được dùng làm tài liệu tham chiếu cho backend authorization, frontend menu visibility, UI action control và kiểm thử phân quyền sau này.

## 2. Permission Legend

| Symbol | Meaning | Description |
|---|---|---|
| F | Full Permission | Có toàn quyền thực hiện hành động chính của UC, ví dụ tạo mới, quản lý, phân công, cấu hình, xóa hoặc đóng hồ sơ. |
| E | Execute / Edit Permission | Được xử lý, chỉnh sửa, phê duyệt, cập nhật hoặc đổi trạng thái trong phạm vi nghiệp vụ được giao. |
| R | Read Permission | Chỉ được xem, tìm kiếm, lọc hoặc truy cập thông tin; không được thay đổi dữ liệu. |
| O | Own / Personal Permission | Chỉ được thao tác với dữ liệu của chính người dùng, ví dụ profile, session, email cá nhân hoặc lịch cá nhân. |
| — | No Permission | Không có quyền truy cập hoặc thực hiện UC này. Frontend nên ẩn chức năng và backend phải chặn request. |

## 3. Role Scope

| Role | Scope |
|---|---|
| HO | Quản lý cấp Head Office, gồm campus, FAQ, report, agenda template và một số cấu hình nghiệp vụ. |
| Admin | Quản trị kỹ thuật hệ thống, gồm role, permission, API configuration và API logs. |
| Staff Leader | Điều phối/quản lý cấp staff hoặc campus, duyệt request, duyệt news, quản lý account/department trong phạm vi được giao. |
| Staff | Nhân sự vận hành chính, tạo/cập nhật delegation, chuẩn bị logistics, quản lý partner, tài liệu, ảnh và tin tức. |
| Department Lead | Trưởng bộ phận, duyệt resource, phân công nhiệm vụ, quản lý personnel và theo dõi coordination tasks. |
| Department | Nhân sự bộ phận, nhận nhiệm vụ, xác nhận tham gia, cập nhật task và ký báo cáo nếu được phân công. |
| Student | Vai trò hỗ trợ, có thể tham gia đoàn phái, tạo minutes, gửi feedback, upload ảnh hoặc tạo news khi được giao. |
| VISITOR | Khách bên ngoài, chủ yếu gửi visit request và xem thông tin liên quan đến delegation của mình. |

## 4. General Authorization Rules

- Frontend dùng ma trận này để ẩn/hiện menu, page, button và action.
- Backend luôn là lớp kiểm tra quyền cuối cùng, không được chỉ dựa vào frontend.
- Mọi API cần kiểm tra role permission, data scope, trạng thái nghiệp vụ hiện tại và quyền trên bản ghi cụ thể.
- Các thao tác create, update, delete, approve, reject, assign, publish, close hoặc change status cần có audit log.
- Các UC có quyền `O` chỉ được xử lý dữ liệu của chính user hiện tại.
- Các UC có quyền `R` không được cho phép thay đổi dữ liệu.
- Các UC có quyền `—` phải bị chặn ở backend kể cả khi user gọi API trực tiếp.

## 5. Permission Matrix By Feature Area

### 5.1. Common

> Các UC dùng cho nội dung công khai và truy cập cơ bản. Nội dung public chỉ nên lấy dữ liệu đã được published/visible.

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

> Các UC xác thực tài khoản. Cần kiểm tra trạng thái tài khoản, token/session và ghi log đăng nhập/đăng xuất.

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

> Quản lý mẫu email và thao tác email theo phạm vi người gửi/người nhận hoặc hồ sơ đoàn phái liên quan.

| UC ID | Action | HO | Admin | Staff Leader | Staff | Department Lead | Department | Student | VISITOR |
|---|---|:---:|:---:|:---:|:---:|:---:|:---:|:---:|:---:|
| UC-42 | View Email Template List | R | — | — | — | — | — | — | — |
| UC-43 | View Email Template Detail | R | — | — | — | — | — | — | — |
| UC-44 | Update Email Template | F | — | — | — | — | — | — | — |
| UC-45 | Create Email Template | F | — | — | — | — | — | — | — |
| UC-46 | Edit Email Content | O | — | O | O | O | O | O | O |
| UC-47 | Send Email | O | — | O | O | O | O | O | O |
| UC-48 | View Email | R | — | R | R | R | R | R | R |
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
| UC-64 | View List FAQ | F | — | — | — | — | — | — | — |
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

> Quản lý lịch cá nhân, lịch bộ phận và sự kiện liên quan đến đoàn phái.

| UC ID | Action | HO | Admin | Staff Leader | Staff | Department Lead | Department | Student | VISITOR |
|---|---|:---:|:---:|:---:|:---:|:---:|:---:|:---:|:---:|
| UC-72 | View My Events | — | — | R | R | R | R | R | — |
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

> Quản lý tin tức, duyệt bài, xuất bản, ẩn/hiện và tin song ngữ.

| UC ID | Action | HO | Admin | Staff Leader | Staff | Department Lead | Department | Student | VISITOR |
|---|---|:---:|:---:|:---:|:---:|:---:|:---:|:---:|:---:|
| UC-88 | Approve News | — | — | E | — | — | — | — | — |
| UC-89 | Publish News | — | — | — | F | — | — | F | — |
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

> Quản lý vai trò và ma trận quyền. Đây là nhóm chức năng có rủi ro cao, chỉ dành cho Admin.

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

## 6. Change Log

| Version | Date | Description |
|---|---|---|
| v0.1 | 2026-06-15 | Initial draft permission matrix formatted by feature area. |
