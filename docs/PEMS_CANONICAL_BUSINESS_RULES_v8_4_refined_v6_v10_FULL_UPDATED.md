# V10 Schema Alignment Addendum — Email Actions, Logistics Handovers, FAQ, Partner Campus Scope

> Addendum này là phần cập nhật chuẩn theo SQL mới nhất: `pems_full_create_manual_wide_coverage_seed_v8_4_refined_v6_v10_clean_logistics_handover_fields.sql`.  
> Nếu các mục cũ trong file này mâu thuẫn với phần V10 addendum, ưu tiên phần V10.

## V10.1 Schema source of truth

```text
Schema: v8.4 refined v6 v10 clean logistics handover fields
Fresh-create SQL: pems_full_create_manual_wide_coverage_seed_v8_4_refined_v6_v10_clean_logistics_handover_fields.sql
Base table count: 49
Total field count: 719
Dynamic permissions: still removed
```

## V10.2 FAQ rules

FAQ hiện tại chỉ dùng tiếng Việt. Không còn `faqs.language_code`.

`faqs.faq_type` chỉ dùng các nhóm liên quan hệ thống:

```text
ACCOUNT_ACCESS
VISIT_REQUEST
DELEGATION_MANAGEMENT
LOGISTICS_RESOURCE
DOCUMENT_MEDIA
NOTIFICATION_EMAIL
OTHER
```

Frontend không hiển thị lựa chọn ngôn ngữ FAQ. Backend không nhận/không trả `languageCode` trong FAQ DTO. Public FAQ chỉ lấy `status = PUBLISHED`; màn quản lý có thể quản lý `PUBLISHED/HIDDEN`.

## V10.3 Partner campus ownership rules

`partners.owner_campus_id` là campus sở hữu hồ sơ đối tác và request duyệt đối tác.

Rule bắt buộc:

```text
1. Khi Staff tạo partner, backend tự set owner_campus_id = currentUser.primary_campus_id.
2. Frontend không được tự truyền owner_campus_id để tránh giả mạo campus.
3. Staff Leader chỉ thấy/duyệt/từ chối partner nếu partners.owner_campus_id = currentUser.primary_campus_id.
4. partners.profile_status vẫn dùng DRAFT / PENDING_APPROVAL / APPROVED / REJECTED.
5. Không thêm partner_review_logs hoặc partner_approval_requests trong v10.
```

## V10.4 Logistics handover/signature rules

`visit_logistics_items` chỉ lưu workflow yêu cầu hậu cần/resource: request, receive, assign, accept, negotiation/proposal, completion.

Các field ký cũ đã bị loại bỏ khỏi `visit_logistics_items`:

```text
handover_confirmed_by
handover_confirmed_at
handover_note
service_report_signed_by
service_report_signed_at
service_report_file_id
```

Toàn bộ ký mượn/ký trả lưu ở `visit_logistics_item_handovers`.

```text
handover_type = BORROW
- borrower_signed_by / borrower_signed_at: bên mượn ký nhận
- provider_signed_by / provider_signed_at: bên cho mượn ký bàn giao

handover_type = RETURN
- borrower_signed_by / borrower_signed_at: bên mượn ký trả
- provider_signed_by / provider_signed_at: bên cho mượn ký nhận lại
```

Mỗi logistics item tối đa có một row `BORROW` và một row `RETURN` nhờ unique `(logistics_item_id, handover_type)`. Bảng này cũng lưu `item_condition`, `condition_note`, `attachment_file_id`.

## V10.5 No task transfer rule

Không triển khai chuyển nhiệm vụ từ người A sang người B trong v10. Không thêm bảng `visit_logistics_assignment_logs`.

Backend phải chặn:

```text
Nếu visit_logistics_items.assigned_to_user_id đã có giá trị và item đã được phân công,
không cho đổi sang user khác.
```

Nếu gán nhầm hoặc không thể xử lý, dùng quy trình hủy/từ chối item hiện tại hoặc tạo logistics item mới theo nghiệp vụ; không gọi là transfer/reassign.

## V10.6 Email action button rules

Không làm chức năng nhận mail/inbox thật trong v10. Không thêm các bảng:

```text
email_threads
email_messages
email_message_recipients
```

PEMS vẫn dùng:

```text
sent_emails              -> lịch sử email gửi đi / outbox snapshot
sent_email_recipients    -> tracking người nhận và delivery status
email_action_tokens      -> token một lần cho nút bấm trong email
```

`email_action_tokens` dùng cho các action không cần đăng nhập:

```text
PARTICIPATION_RESPONSE
LOGISTICS_ASSIGNEE_RESPONSE
LOGISTICS_NEGOTIATION
LOGISTICS_PROPOSAL_RESPONSE
LOGISTICS_HANDOVER_SIGNATURE
```

Action hợp lệ:

```text
ACCEPT
DECLINE
NEGOTIATE
APPROVE_PROPOSAL
REJECT_PROPOSAL
CONFIRM_BORROW
CONFIRM_RETURN
```

Security rule:

```text
1. Chỉ lưu token_hash, không lưu token raw.
2. Token phải có expires_at.
3. Token chỉ dùng một lần.
4. Backend validate target_type + target_id vì đây là polymorphic reference.
5. Nếu người dùng đã trả lời rồi rồi bấm link khác, không update nghiệp vụ lần hai; trả result_status = ALREADY_RESPONDED.
6. Email action page có thể AllowAnonymous nhưng handler phải validate token, status hiện tại, recipient và target.
```

---

# PEMS_CANONICAL_BUSINESS_RULES_v8_4_refined_v6

> **Authoritative source of truth for PEMS business logic.**  
> Nếu bất kỳ tài liệu cũ, code comment, seed cũ, hoặc prompt cũ mâu thuẫn với file này, ưu tiên file này.

## 0. Phiên bản và phạm vi áp dụng

File này áp dụng cho PEMS theo hướng:

```text
Schema: v8.4 refined v6 no dynamic permissions
Architecture: Database-first + Clean Architecture
Permission model: Fixed role policy, không dùng dynamic permissions table
Seed model: Manual rich seed, có thể dùng dynamic time cho planned_start_at/planned_end_at
```

Mục tiêu của file là giúp AI Agent, Backend Developer, Frontend Developer và Database Engineer code đúng nghiệp vụ, không suy diễn theo tài liệu cũ.

---

## 1. Role/SubRole canonical rules

PEMS chỉ dùng các `role_code` cố định:

```text
ADMIN
HO
STAFF
DEPARTMENT
STUDENT
VISITOR
```

Không dùng role riêng cho leader. Staff Leader và Department Leader được xác định bằng `role_code + sub_role`.

| Nhóm người dùng | role_code | sub_role | Ý nghĩa |
|---|---|---|---|
| Admin | `ADMIN` | `NULL` | Quản trị kỹ thuật, cấu hình hệ thống, API, audit, tài khoản theo policy |
| Head Office | `HO` | `NULL` | Xử lý nghiệp vụ liên cơ sở/multi-campus |
| Staff Leader | `STAFF` | `LEADER` | Trưởng IC của một campus; duyệt single-campus và điều phối host campus mình |
| IC Staff | `STAFF` | `STAFF` | Nhân sự IC thường; có thể được gán làm host/support |
| Department Leader | `DEPARTMENT` | `LEADER` | Trưởng phòng ban GENERAL; nhận và phân công logistics/task |
| Department Staff | `DEPARTMENT` | `STAFF` | Nhân sự phòng ban GENERAL; thực hiện logistics/task được giao |
| Student | `STUDENT` | `NULL` | Sinh viên hỗ trợ khi được invite/assign |
| Visitor | `VISITOR` | `NULL` | Khách ngoài, chỉ xem/thao tác request của chính mình |

Cấm dùng các giá trị sau trong DB, backend, frontend, seed và docs hiện hành:

```text
DEPT
STAFF_LEADER
IC_STAFF_LEADER
DEPT_LEADER
DEPARTMENT_LEADER
LEADER as role_code
STAFF_L as role_code
STAFF_P as role_code
DEPT_L as role_code
DEPT_P as role_code
```

Các tên như `STAFF_L`, `STAFF_P`, `DEPT_L`, `DEPT_P` chỉ được nhắc trong mục legacy/mapping nếu cần đọc tài liệu cũ; không được dùng làm giá trị runtime.

---

## 2. Department và campus invariant

Campus có 5 cơ sở chuẩn:

```text
HN, HCM, DN, CT, QN
```

Department có 2 loại:

```text
IC
GENERAL
```

Quy tắc bắt buộc:

```text
1. Staff Leader = STAFF + LEADER, phải thuộc department_type = IC.
2. IC Staff = STAFF + STAFF, phải thuộc department_type = IC.
3. Department Leader = DEPARTMENT + LEADER, phải thuộc department_type = GENERAL.
4. Department Staff = DEPARTMENT + STAFF, phải thuộc department_type = GENERAL.
5. Mỗi campus chỉ nên có đúng 1 Staff Leader ACTIVE.
6. Mỗi GENERAL department chỉ nên có đúng 1 Department Leader ACTIVE.
7. Internal user bắt buộc có primary_campus_id.
8. Visitor không có primary_campus_id, department_id, sub_role.
9. Admin/HO/Student không dùng sub_role.
10. Không tạo user mới vào campus/department đang INACTIVE.
```

---

## 3. Permission model hiện tại

PEMS v8.4 refined v6 đã bỏ dynamic permission DB.

Không được code theo kiểu:

```text
SELECT * FROM permissions
SELECT * FROM role_permissions
Runtime authorize bằng permission rows trong DB
```

Backend/frontend phải dùng fixed policy dựa trên:

```text
role_code
sub_role / effectiveRole
primary_campus_id
department_id
ownership
visitor_user_id
coordinator_user_id
current_host_user_id
participant relationship
logistics assignment
record status
```

Frontend chỉ dùng policy để ẩn/hiện menu, route, button và tránh gọi API sai. Backend luôn là lớp quyết định cuối cùng.

---

## 4. Submit Visit Request canonical rules

Submit form chỉ tạo yêu cầu thăm, không xử lý duyệt, assign host, cancel hay close.

Luồng đúng:

```text
Visitor/Staff nhập form
→ xác minh OTP/email nếu là visitor public flow
→ backend validate full form
→ insert visit_requests với status = PENDING_APPROVAL
→ insert visit_request_campuses cho từng campus với status = WAITING_REQUEST_APPROVAL
→ insert visit_guest_members
→ insert visit_agendas nếu có
→ gửi notification/email phù hợp
```

Submit không được:

```text
Không approve request
Không reject request
Không cancel request
Không assign host
Không set IN_PROGRESS/COMPLETED ở visit_requests
Không tạo PENDING_EMAIL_VERIFICATION trong visit_requests
```

---

## 5. Guest list và support team validation

Trên form đăng ký thăm, có 2 nhóm người ngoài hệ thống:

```text
Danh sách khách                 → visit_guest_members.member_type = GUEST
Danh sách team hỗ trợ khách     → visit_guest_members.member_type = EXTERNAL_SUPPORT
```

Rule bắt buộc:

```text
1. Mỗi visit_request phải có ít nhất 1 GUEST.
2. Mỗi visit_request phải có ít nhất 1 EXTERNAL_SUPPORT.
3. GUEST và EXTERNAL_SUPPORT đều phải có full_name, organization, job_title, nationality.
4. UI nút “Là tôi” trong team hỗ trợ khách sẽ copy thông tin người đăng ký form vào một dòng EXTERNAL_SUPPORT.
5. Người đăng ký form có thể đồng thời là EXTERNAL_SUPPORT.
6. Người đăng ký form không tự động là GUEST, trừ khi họ thực sự nằm trong đoàn khách.
```

DB không enforce được rule “ít nhất một child row” bằng foreign key. Backend phải validate trước khi commit transaction.

---

## 6. Request status và campus instance status

### 6.1 `visit_requests.status`

`visit_requests` là trạng thái tổng của request/form.

Chỉ dùng các status:

```text
PENDING_APPROVAL
APPROVED
REJECTED
CANCELLED
```

Không đưa lifecycle vận hành như `BEFORE_VISIT`, `DURING_VISIT`, `CLOSED` lên `visit_requests.status`.

### 6.2 `visit_request_campuses.status`

`visit_request_campuses` là trạng thái vận hành theo từng campus instance.

Chỉ dùng các status:

```text
WAITING_REQUEST_APPROVAL
WAITING_HOST_ASSIGNMENT
ASSIGNED
BEFORE_VISIT
DURING_VISIT
AFTER_VISIT
CLOSED
CANCELLED
```

Ý nghĩa:

| Status | Ý nghĩa | Host |
|---|---|---|
| `WAITING_REQUEST_APPROVAL` | Chờ Staff Leader hoặc HO duyệt | Chưa có host |
| `WAITING_HOST_ASSIGNMENT` | Request tổng đã approve, campus chờ Staff Leader gán host | Chưa có host |
| `ASSIGNED` | Đã có host chính thức | Có current_host_user_id |
| `BEFORE_VISIT` | Giai đoạn chuẩn bị/trước tiếp khách | Có host |
| `DURING_VISIT` | Đang diễn ra chuyến thăm | Có host |
| `AFTER_VISIT` | Đã tiếp xong, chờ hậu xử lý/feedback/minutes/news/gallery | Có host |
| `CLOSED` | Đã đóng hồ sơ campus instance | Có host/closed metadata |
| `CANCELLED` | Campus instance bị hủy trước khi diễn ra | Có cancellation metadata nếu sau approve |

---

## 7. Single-campus approval flow

Single-campus là request có đúng một campus.

```text
Visitor/Staff submit
→ visit_requests.status = PENDING_APPROVAL
→ visit_request_campuses.status = WAITING_REQUEST_APPROVAL
→ Staff Leader đúng campus nhìn thấy request
→ Staff Leader approve hoặc reject
```

Nếu reject:

```text
visit_requests.status = REJECTED
decision_actor_role = STAFF_LEADER
decided_by = Staff Leader
decided_at = thời điểm xử lý
decision_note bắt buộc nếu reject
Gửi notification/email cho Visitor
```

Nếu approve:

```text
visit_requests.status = APPROVED
decision_actor_role = STAFF_LEADER
decided_by = Staff Leader
decided_at = thời điểm xử lý
visit_request_campuses.status = WAITING_HOST_ASSIGNMENT nếu chưa gán host ngay
```

Sau đó Staff Leader gán Staff thường làm host:

```text
current_host_user_id = IC Staff được chọn
host_assigned_by = Staff Leader
host_assigned_at = thời điểm gán
visit_request_campuses.status = ASSIGNED
```

Nếu UI cho chọn host ngay trong lúc approve, có thể đi thẳng:

```text
WAITING_REQUEST_APPROVAL → ASSIGNED
```

nhưng vẫn phải validate host candidate đúng rule.

---

## 8. Multi-campus approval flow

Multi-campus là request có từ 2 campus trở lên.

Rule quan trọng nhất:

```text
Khi HO chưa duyệt, Staff Leader/Staff/Department/Student tại các campus con chưa được thấy các đoàn/campus instance trong cùng form đó.
```

Luồng đúng:

```text
Visitor/Staff submit multi-campus
→ visit_requests.status = PENDING_APPROVAL
→ mỗi campus instance = WAITING_REQUEST_APPROVAL
→ chỉ HO nhìn thấy request tổng
→ HO approve hoặc reject request tổng
```

Nếu HO reject:

```text
visit_requests.status = REJECTED
decision_actor_role = HO
decided_by = HO
decided_at = thời điểm xử lý
decision_note bắt buộc nếu reject
Campus instances giữ WAITING_REQUEST_APPROVAL hoặc hiển thị derived rejected theo request tổng
Không tạo participant/logistics/calendar/minutes cho campus con
```

Nếu HO approve:

```text
visit_requests.status = APPROVED
decision_actor_role = HO
decided_by = HO
decided_at = thời điểm xử lý
Mỗi campus instance chuyển sang WAITING_HOST_ASSIGNMENT
coordinator_user_id = Staff Leader của campus tương ứng
coordinator_assigned_by = HO
coordinator_assigned_at = thời điểm approve
```

Sau đó Staff Leader từng campus mới nhìn thấy campus instance của mình và gán host chính thức.

Không làm:

```text
Không để từng Staff Leader duyệt lại request tổng sau HO.
Không auto coi Staff Leader là host chính thức.
Không cho Staff Leader campus khác thấy instance không thuộc campus mình.
```

---

## 9. Host assignment canonical rules

Host chính thức của campus instance lưu ở:

```text
visit_request_campuses.current_host_user_id
```

Host candidate hợp lệ:

```text
user.status = ACTIVE
role_code = STAFF
sub_role = STAFF
primary_campus_id = campus_id của visit_request_campuses
department.department_type = IC
department.status = ACTIVE
user_id != current Staff Leader nếu Staff Leader đang thao tác
```

Không được hiện trong danh sách host:

```text
Staff Leader = STAFF + LEADER
Department Leader/Staff = DEPARTMENT + LEADER/STAFF
Student
HO
Admin
Visitor
Inactive/Locked user
User khác campus
```

Theo schema hiện tại, `current_host_user_id` chỉ nên set một lần. Không triển khai transfer host nếu chưa có schema/UC riêng.

---

## 10. Visibility matrix

| Actor | Được thấy gì |
|---|---|
| Admin | Không mặc định xem business delegation; chỉ quản trị kỹ thuật/config/audit/account theo policy |
| HO | Chỉ thấy multi-campus request/delegation tổng và các instance liên quan sau approve; không xử lý single-campus |
| Staff Leader | Thấy single-campus thuộc campus mình; thấy multi-campus instance thuộc campus mình sau khi HO approve |
| IC Staff | Thấy campus instance nếu là current host, IC_SUPPORT hoặc được assign liên quan |
| Department Leader | Thấy logistics/task/participant/resource thuộc department/campus mình được giao |
| Department Staff | Thấy task/logistics được Department Leader assign |
| Student | Thấy delegation/agenda/task nếu được invite/assign |
| Visitor | Chỉ thấy request của chính mình |

Backend API list/detail/action phải enforce scope, không chỉ hide trên frontend.

---

## 11. Cancellation canonical rules

### 11.1 Trước khi request được duyệt

Nếu `visit_requests.status = PENDING_APPROVAL`:

```text
Không dùng CANCELLED.
Nếu không tiếp nhận, dùng reject flow.
visit_requests.status = REJECTED.
decision_note ghi lý do.
```

Actor reject:

```text
Single-campus: Staff Leader đúng campus
Multi-campus: HO
```

### 11.2 Sau khi request đã APPROVED

Theo schema v8.4 refined v6 hiện tại, cancellation ở campus instance chỉ có:

```text
cancellation_actor_type = VISITOR | HOST
cancellation_source = SELF_SERVICE | EXTERNAL_CONFIRMATION
```

Vì vậy quyền cancel sau APPROVED chỉ gồm:

```text
Visitor: tự hủy request của chính mình hoặc hủy toàn bộ request nếu business cho phép.
Host: hủy campus instance mình phụ trách sau khi khách xác nhận hủy ngoài hệ thống.
```

Không có luồng sau APPROVED cho:

```text
Staff Leader cancel vì internal decision
HO cancel vì internal decision
Department cancel
Admin cancel delegation
SYSTEM cancel nếu chưa có schema/UC riêng
```

Nếu muốn Staff Leader/HO cancel vì internal decision, phải patch schema trước. Không được code vượt schema.

### 11.3 Status không được cancel

Không cho cancel campus instance nếu đang ở:

```text
DURING_VISIT
AFTER_VISIT
CLOSED
CANCELLED
```

Có thể cancel nếu đang ở:

```text
WAITING_HOST_ASSIGNMENT
ASSIGNED
BEFORE_VISIT
```

### 11.4 Visitor self-service cancel

```text
cancelled_by = visitor_user_id
cancelled_at = current timestamp
cancellation_actor_type = VISITOR
cancellation_source = SELF_SERVICE
cancellation_reason = lý do visitor nhập
```

Nếu hủy toàn bộ single-campus request:

```text
visit_requests.status = CANCELLED
visit_request_campuses.status = CANCELLED
```

Nếu hủy toàn bộ multi-campus request:

```text
visit_requests.status = CANCELLED
tất cả campus instance active trước chuyến thăm = CANCELLED
```

Nếu chỉ hủy một campus instance trong multi-campus:

```text
chỉ campus đó = CANCELLED
request tổng vẫn APPROVED nếu còn campus khác active
```

### 11.5 Host external-confirmation cancel

Host chỉ hủy instance mình đang phụ trách:

```text
current_host_user_id = current user id
cancellation_actor_type = HOST
cancellation_source = EXTERNAL_CONFIRMATION
cancellation_reason bắt buộc ghi kênh xác nhận, thời điểm, người xác nhận, lý do
```

---

## 12. Logistics/resource rules

Logistics gắn theo campus instance:

```text
visit_logistics_items.visit_instance_id
```

Status hợp lệ:

```text
PLANNED
REQUESTED
CHANGE_PROPOSED
RECEIVED
ASSIGNED
ACCEPTED
IN_PROGRESS
READY
DONE
REJECTED
CANCELLED
```

Quy tắc:

```text
1. Host/IC Staff tạo yêu cầu logistics cho campus instance mình phụ trách.
2. requested_to_department_id phải thuộc cùng campus và department_type = GENERAL.
3. Department Leader nhận, approve, assign hoặc propose modification.
4. Department Staff chỉ xử lý item được assign.
5. Logistics của campus instance CANCELLED/CLOSED không được chỉnh sửa nếu không có reopen/exception flow.
```

---

## 13. Participants rules

`visit_participants` chỉ lưu người nội bộ tham gia campus instance.

Participant role:

```text
IC_HOST
IC_SUPPORT
DEPT_SUPPORT
STUDENT
```

Rule:

```text
1. Mỗi (visit_instance_id, user_id) chỉ có một participant row.
2. Host chính thức ưu tiên đọc từ current_host_user_id.
3. Nếu snapshot host vào visit_participants thì participant_role = IC_HOST, is_host = TRUE.
4. IC_SUPPORT phải là STAFF + STAFF cùng campus.
5. DEPT_SUPPORT phải là DEPARTMENT user cùng campus/department phù hợp.
6. STUDENT phải là STUDENT user được invite/assign.
```

Participant status:

```text
INVITED
ACCEPTED
DECLINED
ASSIGNED
REMOVED
```

---

## 14. Minutes, feedback, gallery/news after visit

Minutes:

```text
Gắn với visit_instance_id
status = DRAFT hoặc SAVED
Không dùng FINAL nếu schema không có
Không cho sửa sau CLOSED nếu không có reopen flow
```

Feedback:

```text
Chỉ hợp lý khi visit đã DURING_VISIT/AFTER_VISIT/CLOSED hoặc sau thời điểm diễn ra.
Không seed/cấp feedback cho case visitor cancel trước chuyến thăm.
Nhân sự nội bộ có thể được đánh giá theo nghiệp vụ.
Khách mới/guest member không bị đánh sao như nhân sự nếu không có rule riêng.
```

News/gallery:

```text
Chỉ public nếu status/visibility cho phép.
Không publish nội dung của visit bị cancel trước khi diễn ra, trừ tin riêng có duyệt rõ.
```

---

## 15. Time/status consistency rules

`planned_start_at` và `planned_end_at` nằm ở `visit_request_campuses`.

Rule thời gian động khi seed/test:

| Campus status | planned_start_at/planned_end_at nên như thế nào |
|---|---|
| `WAITING_REQUEST_APPROVAL` | Tương lai xa, ví dụ hôm nay +10 đến +35 ngày |
| `WAITING_HOST_ASSIGNMENT` | Tương lai, ví dụ hôm nay +7 đến +28 ngày |
| `ASSIGNED` | Tương lai, ví dụ hôm nay +5 đến +20 ngày |
| `BEFORE_VISIT` | Tương lai gần, ví dụ hôm nay +1 đến +3 ngày |
| `DURING_VISIT` | `planned_start_at <= CURRENT_TIMESTAMP <= planned_end_at` |
| `AFTER_VISIT` | Đã kết thúc gần đây, ví dụ hôm qua đến 5 ngày trước |
| `CLOSED` | Đã kết thúc lâu hơn, có `closed_at` sau `planned_end_at` |
| `CANCELLED` | Thường planned vẫn ở tương lai; `cancelled_at` trước `planned_start_at` |

Không được để status mâu thuẫn thời gian, ví dụ:

```text
DURING_VISIT nhưng planned_start_at ở tháng trước và planned_end_at ở tháng trước.
BEFORE_VISIT nhưng planned_start_at đã qua nhiều ngày.
CLOSED nhưng planned_end_at ở tương lai.
CANCELLED sau DURING_VISIT nếu không có UC đặc biệt.
```

---

## 16. Manual rich seed rules

Seed PEMS dùng để test nghiệp vụ thật, không phải dữ liệu giả lặp lại.

Cho phép:

```text
CURRENT_DATE
CURRENT_TIMESTAMP
DATE_ADD
DATE_SUB
INTERVAL
```

Mục đích là tạo `planned_start_at/planned_end_at` động theo ngày import.

Không dùng để spam/generate:

```text
Stored procedure
Loop
Cursor
RAND()
UUID() nếu tạo dữ liệu vô nghĩa
INSERT IGNORE để che lỗi
Email/name copy-paste khác vài chữ
```

Seed bắt buộc cover:

```text
1. Tất cả role/subRole chính.
2. Single-campus đủ trạng thái.
3. Multi-campus đủ trạng thái.
4. Multi-campus pending HO chưa visible cho campus con.
5. Waiting host assignment.
6. Assigned/before/during/after/closed.
7. Visitor cancel full single-campus.
8. Visitor cancel full multi-campus.
9. Visitor cancel partial campus instance.
10. Host cancel by external confirmation.
11. Logistics đầy đủ enum/status.
12. Participants đầy đủ role/status.
13. Guest list + external support bắt buộc.
14. Dynamic planned time đúng status.
15. Bản ghi cho nhiều campus/account, không chỉ HN.
```

---

## 17. Backend invariant checklist

Mỗi API delegation phải kiểm tra:

```text
[ ] Current user authenticated đúng portal.
[ ] role_code/sub_role hợp lệ.
[ ] Scope campus/department/ownership/participant.
[ ] Request tổng status hợp lệ.
[ ] Campus instance status hợp lệ.
[ ] Host/coordinator/current participant đúng.
[ ] Không cho action khi CLOSED/CANCELLED nếu không có rule riêng.
[ ] Không tin campusId/departmentId/role từ frontend.
[ ] Error code rõ: 400/401/403/404/409/422.
[ ] Audit log cho action quan trọng.
[ ] Notification/email nếu nghiệp vụ cần.
```

---

## 18. Frontend invariant checklist

Frontend phải:

```text
[ ] Ẩn menu/button theo role/subRole/scope/status.
[ ] Không gọi API vượt scope nếu biết trước user không có quyền.
[ ] Không dùng mock data khi API thật đã có.
[ ] Không tự suy diễn trạng thái bằng text cũ.
[ ] Dùng enum/constants chung.
[ ] Với multi-campus pending HO: Staff Leader/Staff không render instance con.
[ ] Với cancel: chỉ render nút cho Visitor hoặc Host đúng status.
[ ] Với assign host: chỉ hiện Staff thường cùng campus.
[ ] Với form submit: validate GUEST và EXTERNAL_SUPPORT.
[ ] Với time/status: badge hiển thị theo status DB, không tự đổi status trên client.
```

---

## 19. DB verification queries

### 19.1 Kiểm tra role/subRole sai

```sql
SELECT u.user_id, u.full_name, u.email, r.role_code, u.sub_role
FROM users u
JOIN roles r ON r.role_id = u.role_id
WHERE r.role_code IN ('DEPT','STAFF_LEADER','DEPT_LEADER','DEPARTMENT_LEADER')
   OR (r.role_code IN ('STAFF','DEPARTMENT') AND u.sub_role NOT IN ('STAFF','LEADER'))
   OR (r.role_code NOT IN ('STAFF','DEPARTMENT') AND u.sub_role IS NOT NULL);
```

Kết quả đúng: `0 rows`.

### 19.2 Kiểm tra form thiếu GUEST hoặc EXTERNAL_SUPPORT

```sql
SELECT vr.visit_request_id, vr.request_code,
       SUM(vgm.member_type = 'GUEST') AS guest_count,
       SUM(vgm.member_type = 'EXTERNAL_SUPPORT') AS support_count
FROM visit_requests vr
LEFT JOIN visit_guest_members vgm ON vgm.visit_request_id = vr.visit_request_id
GROUP BY vr.visit_request_id, vr.request_code
HAVING guest_count = 0 OR support_count = 0;
```

Kết quả đúng: `0 rows`.

### 19.3 Kiểm tra multi-campus pending HO bị gắn dữ liệu vận hành

```sql
SELECT vr.visit_request_id, vr.request_code, vrc.visit_instance_id,
       COUNT(DISTINCT vp.participant_id) AS participant_count,
       COUNT(DISTINCT vli.logistics_item_id) AS logistics_count,
       COUNT(DISTINCT ce.calendar_event_id) AS calendar_count
FROM visit_requests vr
JOIN visit_request_campuses vrc ON vrc.visit_request_id = vr.visit_request_id
LEFT JOIN visit_participants vp ON vp.visit_instance_id = vrc.visit_instance_id
LEFT JOIN visit_logistics_items vli ON vli.visit_instance_id = vrc.visit_instance_id
LEFT JOIN calendar_events ce ON ce.visit_instance_id = vrc.visit_instance_id
WHERE vr.visit_scope = 'MULTI_CAMPUS'
  AND vr.status = 'PENDING_APPROVAL'
GROUP BY vr.visit_request_id, vr.request_code, vrc.visit_instance_id
HAVING participant_count > 0 OR logistics_count > 0 OR calendar_count > 0;
```

Kết quả đúng: `0 rows`.

### 19.4 Kiểm tra host không phải IC Staff thường

```sql
SELECT vrc.visit_instance_id, vrc.current_host_user_id, u.email, r.role_code, u.sub_role, d.department_type
FROM visit_request_campuses vrc
JOIN users u ON u.user_id = vrc.current_host_user_id
JOIN roles r ON r.role_id = u.role_id
LEFT JOIN departments d ON d.department_id = u.department_id
WHERE vrc.current_host_user_id IS NOT NULL
  AND NOT (
    r.role_code = 'STAFF'
    AND u.sub_role = 'STAFF'
    AND d.department_type = 'IC'
    AND u.status = 'ACTIVE'
    AND u.primary_campus_id = vrc.campus_id
  );
```

Kết quả đúng: `0 rows`.

---

## Agenda Template module canonical rules (v10 — 4 tables)

Module Agenda chỉ dùng đúng 4 bảng: `agenda_templates`, `agenda_template_items`, `agenda_template_defaults`, `visit_agendas`. `visit_request_campuses` KHÔNG thuộc nhóm bảng agenda và KHÔNG được sửa schema — chỉ đọc `visit_instance_id`, `visit_request_id`, `campus_id`, `planned_start_at`, `planned_end_at`, `status`, `current_host_user_id`.

Quy tắc chốt:

- `visit_type` luôn lấy từ form gốc `visit_requests.visit_type` (join qua `visit_request_campuses.visit_request_id`); KHÔNG lưu/đọc `visit_type` ở `visit_request_campuses`. Enum: `CAMPUS_TOUR, MEETING, WORKSHOP, SIGNING_CEREMONY, EXCHANGE, OTHER`.
- `agenda_templates` cấu hình theo `visit_type` + phạm vi (`campus_id` NULL = GLOBAL, ngược lại theo campus). `campus_scope_key` do trigger sinh ra (`GLOBAL` hoặc `CAST(campus_id AS CHAR)`); ràng buộc `campus_id ⇔ GLOBAL` enforce bằng trigger, không bằng CHECK (MySQL 8 cấm CHECK trên cột có FK referential actions).
- `agenda_template_items` dùng `start_offset_minutes` + `duration_minutes` (KHÔNG dùng `TIME`). `duration_minutes > 0`.
- `agenda_template_defaults` map mặc định theo `(campus_scope_key, visit_type)`. Khi host mở setup agenda: chọn default theo **campus hiện tại + visit_type**, nếu không có thì **fallback GLOBAL + visit_type**. Chỉ xét template `ACTIVE`, chưa `deleted`.
- Default phải cùng `visit_type` và cùng scope với template; không cho dùng template `INACTIVE`/đã xóa làm default.
- Apply template → ghi `visit_agendas` thật:
  - `start_time = visit_request_campuses.planned_start_at + agenda_template_items.start_offset_minutes`
  - `end_time = start_time + agenda_template_items.duration_minutes`
  - `start_time`/`end_time` lưu `DATETIME` (đầy đủ ngày + giờ); backend là nguồn tính chính.
  - Host vẫn được chọn template khác (kể cả khác `visit_type`); response/audit ghi rõ khi lệch `visit_type`.
  - `replaceExisting=false` mà đã có agenda → 409 Conflict; `replaceExisting=true` → xóa agenda cũ rồi insert lại (không trùng `sequence_order`).
  - Chỉ Host phụ trách (`current_host_user_id`) và chỉ trong giai đoạn chuẩn bị (`ASSIGNED`/`BEFORE_VISIT`) mới được apply.
- Truy vết template nguồn (KHÔNG lưu ở `visit_request_campuses`): `visit_agendas.source_template_item_id → agenda_template_items.agenda_template_item_id → agenda_templates.agenda_template_id`.
- KHÔNG tạo bảng thứ 5 kiểu `visit_agenda_template_applications`; KHÔNG thêm `applied_agenda_template_id`/`agenda_template_applied_at`/`agenda_template_applied_by` vào `visit_request_campuses`.
- Phân quyền quản lý: HO quản lý scope GLOBAL; Staff Leader quản lý scope campus của mình. Backend luôn kiểm tra scope, không tin `campusId` từ frontend.
