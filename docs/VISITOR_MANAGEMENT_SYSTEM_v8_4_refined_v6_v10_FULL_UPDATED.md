> [!WARNING]
> **LEGACY ARCHITECTURE NOTE (Campus-independent Approval Update)**
> This document has been updated to reflect the new Campus-independent Approval architecture.
> - **HO is now monitor/read-only.** There is no centralized multi-campus approval by HO.
> - **Staff Leader approval is per-campus.** Each Staff Leader directly receives and approves/rejects their own campus instance right after submission.
> - **Self-hosting is supported.** Staff Leaders can assign themselves as the host during approval.
> - **Approving a campus requires naming its host in the same act.** There is no "approved but
>   nobody hosting" state. `ASSIGNED` is very much still in the lifecycle: it is where a campus sits
>   once it has a host, until that host explicitly starts preparation (`ASSIGNED → BEFORE_VISIT`).
> - **Per-campus operational contact + confirmation gate.** A request first sits at
>   `PENDING_CONTACT_CONFIRMATION` while each campus waits for its OWN guest-side contact to
>   confirm. Nothing is assigned and no setup data may be written until the LAST one confirms.
> - **Proposed host.** An internal creator may record who should host their own campus
>   (`host_selection_mode` = SELF / SELECTED / WAIT_FOR_LATER). That is an intention, not an
>   assignment: it is revalidated and activated only when the gate opens, and falls back to
>   `WAITING_REQUEST_APPROVAL` if it no longer holds. Nobody is ever auto-substituted.
> - **New statuses:** `PENDING_CONTACT_CONFIRMATION` and `PARTIALLY_APPROVED` (request level),
>   `WAITING_CONTACT_CONFIRMATION` and `REJECTED` (campus level).
> - **Cancel logic:** Visitors can cancel requests in `PENDING_CONTACT_CONFIRMATION`,
>   `PENDING_APPROVAL` or `PARTIALLY_APPROVED` states.
> - **Transportation:** the per-campus `transportation_note` replaced the older request-level note.
>
> Canonical source for the two rules above: `PEMS_CANONICAL_BUSINESS_RULES` Mục 6.3 and Mục 8.
> Please refer to the latest codebase and SQL schema for the current implementation.

# VISITOR_MANAGEMENT_SYSTEM_v8_4_refined_v6_FULL_UPDATED

> **Bản FULL-PRESERVED cập nhật theo PEMS v8.4 refined v6 no dynamic permissions.**  
> File này gồm 2 phần:  
> - **PHẦN A — Nội dung chuẩn hiện tại để code/triển khai.**  
> - **PHẦN B — Nội dung gốc/legacy được giữ lại đầy đủ để đối chiếu lịch sử.**  
>
> Khi PHẦN A mâu thuẫn với PHẦN B, **luôn ưu tiên PHẦN A**. PHẦN B không được dùng làm nguồn code trực tiếp nếu có dấu hiệu legacy như `DEPT`, `STAFF_L`, `STAFF_P`, `Staff click nhận đón`, `auto Staff Leader làm host`, `Staff Leader/HO cancel sau APPROVED`, hoặc dynamic permissions.

## 0. Cách đọc file này

```text
1. Đọc PEMS_CANONICAL_BUSINESS_RULES_v8_4_refined_v6.md trước.
2. Đọc PHẦN A của file này để lấy nghiệp vụ/logic hiện hành.
3. Chỉ dùng PHẦN B để hiểu nguồn gốc tài liệu cũ, không dùng để sinh code nếu mâu thuẫn với PHẦN A.
4. Nếu cần code, backend phải kiểm tra lại bằng schema v8.4 refined v6 và seed v7/v6 dynamic time tương ứng.
```


# V10 Visitor/Delegation Management Addendum

> Áp dụng theo SQL v10 mới nhất. Nếu phần cũ mâu thuẫn, ưu tiên addendum này.

## V10.1 Bảng liên quan bổ sung

| Bảng | Vai trò |
|---|---|
| `visit_logistics_item_handovers` | Lưu ký mượn/ký trả đồ/resource theo BORROW/RETURN |
| `email_action_tokens` | Lưu token một lần cho nút xác nhận/từ chối/thương lượng/ký trong email |
| `sent_email_recipients` | Lưu tracking người nhận email gửi đi |

Không có bảng inbox email thật trong v10.

## V10.2 Logistics handover flow

```text
Logistics item được request/assigned/accepted trong visit_logistics_items
→ Khi giao/mượn đồ: tạo hoặc cập nhật handover_type = BORROW
→ Khi trả/nhận lại đồ: tạo hoặc cập nhật handover_type = RETURN
```

Ý nghĩa chữ ký:

```text
BORROW.borrower_signed_*  = bên mượn ký nhận
BORROW.provider_signed_*  = bên cho mượn ký bàn giao
RETURN.borrower_signed_*  = bên mượn ký trả
RETURN.provider_signed_*  = bên cho mượn ký nhận lại
```

`visit_logistics_items` không còn các field ký cũ. Mọi UI/API ký mượn/ký trả phải dùng `visit_logistics_item_handovers`.

## V10.3 Email button action flow

Có thể gửi email cho người tham gia/department/logistics assignee với nút:

```text
Xác nhận
Từ chối
Thương lượng
Chấp nhận đề xuất
Từ chối đề xuất
Ký nhận
Ký trả
```

Người nhận không cần đăng nhập. Backend validate `email_action_tokens` rồi update bảng nghiệp vụ tương ứng.

Nếu bấm lại email cũ hoặc bấm sang lựa chọn khác sau khi đã trả lời:

```text
Không update dữ liệu lần hai.
Trả result_status = ALREADY_RESPONDED.
Hiển thị: Bạn đã trả lời yêu cầu này rồi.
```

## V10.4 No inbound email phase

Không đọc Gmail/mailbox của khách hoặc mailbox hệ thống trong v10. Kết quả phản hồi lấy từ:

```text
email_action_tokens
visit_participants.status / responded_at
visit_logistics_items.status / proposal fields
visit_logistics_item_handovers signature fields
```

## V10.5 No logistics task transfer

Không chuyển nhiệm vụ logistics từ người A sang người B. Nếu đã có `assigned_to_user_id`, backend không cho đổi sang user khác.

---

# V11 Visitor/Delegation Management Addendum — 2026-07-02

> Addendum này ghi nhận các tính năng/hành vi đã có trong code nhưng chưa từng mô tả ở PHẦN A bên dưới, và sửa các điểm PHẦN A hiện sai so với code, sau khi rà soát trực tiếp nhánh `Canh-Iter1` (2026-07-02). 4 commit gần nhất của repo đều tập trung vào module này ("visit process details and public news viewing", "visit process management features", "visit contribution and news submission", "visit process management workflows") nên đây là khu vực lệch tài liệu nhiều nhất.

## V11.1 Trang/luồng mới chưa từng được mô tả

```text
VisitProcessSummaryPage.tsx        -> trang tổng hợp tiến độ visit process
VisitContributionPage.tsx          -> trang đóng góp media/minutes/news (dùng MediaContributionSection,
                                       MinutesContributionSection, NewsContributionSection)
VisitorVisitDetailPage.tsx         -> trang chi tiết chuyến thăm public-facing cho Visitor + xem news
HoVisitProcessDetail.tsx           -> chi tiết visit process theo góc nhìn HO
DeptLeadAssignmentTab.tsx          -> tab phân công cho Department Leader
DeptLeadVisitTasksPage.tsx         -> trang task của Department Leader (bọc SharedDashboardView)
```

Không file canonical nào trong 4 file đang audit từng nhắc các trang này; chỉ có prompt làm việc rời rạc dưới `docs/delegation/**` (ví dụ `PROMPT_VISITOR_VISIT_DETAIL_PAGE.md`) mô tả một phần. Nếu cần mô tả UX chi tiết, đối chiếu trực tiếp các file trên.

## V11.2 UC-27 Participant Invitations — đã triển khai (trước đây "pending")

Trang mời tham gia không còn là mục backlog. `VisitRequestManagement.tsx` hiển thị banner "lời mời đang chờ" cho actor được mời, điều hướng tới `VisitParticipantInvitationDetail.tsx` (route `visit/invitations/:participantId`) để Accept/Decline thật. Tab "đang tham gia" (attending tab) giữ nguyên là read-only sau khi đã accept, đúng thiết kế cũ.

## V11.3 Host assignment có kiểm tra xung đột lịch

`GetHostCandidatesQueryHandler.cs` so khớp từng candidate với `CalendarEvent` cá nhân (ẩn thông tin sự kiện private) và các campus instance khác mà candidate đang làm host trùng khung giờ (`ASSIGNED/BEFORE_VISIT/DURING_VISIT`), trả về `Conflicts`/`ConflictCount` để Staff Leader tham khảo. §8.3 bên dưới chỉ mô tả điều kiện tĩnh (role/subRole/status/campus/department) — điều kiện đó vẫn đúng và được enforce độc lập, đây chỉ là UX bổ sung.

## V11.4 CompleteVisitStage — điều kiện đóng hồ sơ (AFTER_VISIT → CLOSED) chi tiết hơn §9.5

§9.5 bên dưới chỉ ghi `closed_by/closed_at/close_note`. Thực tế `CompleteVisitStageCommandHandler.cs` chặn CLOSED nếu chưa đủ **cả 5 điều kiện**: (1) hiện tại (giờ VN) >= `planned_end_at`; (2) không còn logistics item nào chưa terminal; (3) mọi `visit_logistics_item_handovers` đã ký đủ hai bên; (4) mọi `minute_action_items` đã DONE/CANCELLED; (5) có ít nhất một news PUBLISHED hoặc host xác nhận "không cần news". Gate `SETUP_SAVE_AVAILABLE` cho phần "Info tổng quát" (bút chì sửa) tại `VisitProcess.tsx` vẫn là `false` — chưa nối API save, tách biệt khỏi phần reminder/preparation-note đã hoạt động qua `canConfigurePrep`.

## V11.5 Cập nhật §10.1 — Visitor được tự hủy cả khi còn PENDING_APPROVAL

§10.1 bên dưới ("Nếu request đang PENDING_APPROVAL: Không cancel. Dùng reject flow") **chỉ còn đúng cho Staff Leader/HO**. `CancelVisitRequestCommandHandler.cs` đã bổ sung luồng Visitor tự hủy request của chính mình ngay cả khi còn `PENDING_APPROVAL` (`cancellation_actor_type = VISITOR`, `cancellation_source = SELF_SERVICE`), enforce bằng trigger DB `trg_visit_requests_cancel_validate_bu` và có seed case riêng. Khi đó `visit_requests.status = CANCELLED`, các `visit_request_campuses` đang `WAITING_REQUEST_APPROVAL` chuyển `CANCELLED` theo.

**Cascade khi hủy** (chưa từng ghi ở §10/§11 cũ): hủy một campus instance tự động chuyển mọi `visit_logistics_items` chưa terminal của instance đó sang `CANCELLED` (tự ghi `DecisionNote`), và vô hiệu hoá mọi `email_action_tokens` đang pending cho instance đó.

## V11.6 Cập nhật §11 — Logistics status enum đã thay đổi

DB hiện tại đã bỏ `PLANNED`, `RECEIVED`, `READY` và thêm `DECLINED` (comment schema "FINAL UPDATE 2026-06-26"). Enum đúng: `REQUESTED, CHANGE_PROPOSED, ASSIGNED, ACCEPTED, IN_PROGRESS, DONE, REJECTED, DECLINED, CANCELLED`. Xem bản sửa trực tiếp ở §11 bên dưới.

## V11.7 Reminders và preparation note — hoàn toàn chưa được mô tả

```text
visit_request_campuses.preparation_note   -> ghi chú chuẩn bị của Host trong BEFORE_VISIT
visit_instance_reminder_settings          -> cấu hình nhắc lịch (channel IN_APP|EMAIL, target_group,
                                              days_before/reminder_time -> scheduled_at)
VisitReminderDispatchHostedService        -> background job gửi reminder thật
```

Chỉ Host, chỉ khi instance `ASSIGNED`/`BEFORE_VISIT`, mới được cấu hình (`canConfigurePrep`).

## V11.8 Email action "Negotiate" / "Handover Signature" — schema có, handler public-token chưa xử lý

V10.3 addendum liệt kê "Thương lượng", "Ký nhận", "Ký trả" là action có thể bấm trong email không cần đăng nhập. Thực tế `ExecuteEmailActionCommandHandler`/`GetEmailActionInfoQueryHandler` mới xử lý 4/6 `action_context` (`PARTICIPATION_RESPONSE`, `LOGISTICS_REQUEST_RESPONSE`, `LOGISTICS_ASSIGNEE_RESPONSE`, `LOGISTICS_PROPOSAL_RESPONSE`); `LOGISTICS_NEGOTIATION` và `LOGISTICS_HANDOVER_SIGNATURE` chưa có handler xử lý và cũng chưa có nơi nào phát hành token cho 2 context này. Thương lượng/ký mượn-trả **vẫn hoạt động bình thường qua ứng dụng có đăng nhập** (`SignVisitLogisticsHandoverCommand`, `ProposeResourceModificationCommand`, `ConfirmTheChangeProposalCommand` đã code thật) — chỉ riêng phiên bản "bấm nút trong email, không cần đăng nhập" cho 2 action này là chưa xong.

---

# PHẦN A — NỘI DUNG CHUẨN HIỆN TẠI / UPDATED CANONICAL CONTENT

# VISITOR_MANAGEMENT_SYSTEM_v8_4_refined_v6_UPDATED

> File mô tả chuẩn module **Delegation Reception Management / Visitor Management** theo PEMS v8.4 refined v6.  
> Không dùng lại luồng cũ: “Staff click nhận đón”, “mỗi cơ sở duyệt lại sau HO”, “auto Staff Leader làm host”, hoặc “Staff Leader/HO cancel sau APPROVED”.

## 0. Source of truth

Ưu tiên đọc cùng file:

```text
PEMS_CANONICAL_BUSINESS_RULES_v8_4_refined_v6.md
```

Nếu có tài liệu cũ mâu thuẫn, xem tài liệu cũ là legacy/deprecated.

---

## 1. Mục đích module

Visitor/Delegation Management quản lý vòng đời yêu cầu thăm của khách hoặc staff nội bộ tạo thay:

```text
Submit request
→ Approval/rejection
→ Coordinator assignment
→ Host assignment
→ Preparation
→ Reception
→ After-visit processing
→ Close
→ Cancel khi chuyến thăm không diễn ra
```

---

## 2. Bảng chính liên quan

| Bảng | Vai trò |
|---|---|
| `visit_requests` | Request/form tổng |
| `visit_request_campuses` | Campus instance theo từng cơ sở trong request |
| `visit_guest_members` | Danh sách khách và team hỗ trợ khách ngoài hệ thống |
| `visit_participants` | Người nội bộ tham gia instance |
| `visit_agendas` | Lịch trình/agenda |
| `visit_logistics_items` | Logistics/resource/task |
| `minutes` | Biên bản họp |
| `feedbacks`, `feedback_rating_items` | Feedback/rating |
| `calendar_events` | Lịch visit/logistics/deadline/personal |
| `notifications`, `sent_emails` | Thông báo/email |
| `audit_logs`, `audit_log_changes` | Audit nghiệp vụ |

---

## 3. Actor và scope

| Actor | Quyền chính trong module |
|---|---|
| Visitor | Submit request, xem request của mình, tự hủy sau approved nếu chưa diễn ra, feedback sau visit |
| HO | Duyệt/từ chối multi-campus request tổng, assign coordinator, theo dõi multi-campus |
| Staff Leader | Duyệt/từ chối single-campus campus mình, gán host, theo dõi campus mình |
| IC Staff/Host | Vận hành instance được gán host/support, logistics, participants, minutes, feedback |
| Department Leader | Nhận logistics/resource, approve/assign/propose change |
| Department Staff | Thực hiện logistics/task được giao |
| Student | Hỗ trợ khi được invite/assign |
| Admin | Quản trị kỹ thuật, không mặc định thao tác delegation nghiệp vụ |

---

## 4. Status model

### 4.1 Request-level

`visit_requests.status`:

```text
PENDING_APPROVAL
APPROVED
REJECTED
CANCELLED
```

### 4.2 Campus-instance-level

`visit_request_campuses.status`:

```text
WAITING_REQUEST_APPROVAL
ASSIGNED
ASSIGNED
BEFORE_VISIT
DURING_VISIT
AFTER_VISIT
CLOSED
CANCELLED
```

---

## 5. Submit Visit Request

### 5.1 Input

Form submit gồm tối thiểu:

```text
Thông tin người đăng ký
Thông tin tổ chức/đối tác
Campus muốn thăm
Thời gian dự kiến
Mục đích/chủ đề
Danh sách khách
Danh sách team hỗ trợ khách
Agenda nếu có
Thông tin liên hệ
```

### 5.2 Required child rows

Mỗi request bắt buộc có:

```text
>= 1 visit_guest_members.member_type = GUEST
>= 1 visit_guest_members.member_type = EXTERNAL_SUPPORT
```

Nút “Là tôi” ở danh sách team hỗ trợ khách:

```text
Lấy thông tin người đăng ký form và tự fill vào một dòng EXTERNAL_SUPPORT.
```

### 5.3 Main flow

```text
1. User nhập form.
2. Nếu là public Visitor flow, xác minh OTP/email.
3. Backend validate toàn bộ form.
4. Tạo visit_requests với status = PENDING_APPROVAL.
5. Tạo visit_request_campuses cho mỗi campus, status = WAITING_REQUEST_APPROVAL.
6. Tạo visit_guest_members: GUEST và EXTERNAL_SUPPORT.
7. Tạo visit_agendas nếu có.
8. Gửi notification/email cho actor xử lý phù hợp.
9. Ghi audit/security log.
```

### 5.4 Submit không làm

```text
Không approve.
Không reject.
Không cancel.
Không assign coordinator.
Không assign host.
Không tạo logistics/participants nội bộ khi request chưa approved.
```

---

## 6. Single-campus flow

```text
Submit single-campus
→ visit_requests.status = PENDING_APPROVAL
→ campus instance = WAITING_REQUEST_APPROVAL
→ Staff Leader của campus nhìn thấy
```

### 6.1 Reject

```text
Actor: Staff Leader đúng campus
visit_requests.status = REJECTED
decision_actor_role = STAFF_LEADER
decided_by = current user
decided_at = now
decision_note bắt buộc
Thông báo/email cho Visitor
```

### 6.2 Approve

```text
Actor: Staff Leader đúng campus
visit_requests.status = APPROVED
decision_actor_role = STAFF_LEADER
decided_by = current user
decided_at = now
campus instance = ASSIGNED nếu chưa chọn host
```

### 6.3 Assign host

```text
Actor: Staff Leader đúng campus
Candidate: STAFF + STAFF, ACTIVE, same campus, IC department
Set current_host_user_id
Set host_assigned_by
Set host_assigned_at
campus instance = ASSIGNED
```

---

## 7. Multi-campus flow

```text
Submit multi-campus
→ visit_requests.status = PENDING_APPROVAL
→ all campus instances = WAITING_REQUEST_APPROVAL
→ only HO sees request tổng
```

### 7.1 Visibility before HO approval

Khi HO chưa duyệt:

```text
Staff Leader campus con không thấy instance.
IC Staff không thấy instance.
Department không thấy task/logistics.
Student không thấy invitation.
Không tạo participant/logistics/calendar/minutes.
```

### 7.2 HO reject

```text
Actor: HO
visit_requests.status = REJECTED
decision_actor_role = HO
decided_by = current user
decided_at = now
decision_note bắt buộc
Thông báo/email cho Visitor
```

### 7.3 Staff Leader approve (per campus)

> Thay cho "HO approve" của bản trước: HO không quyết định request.
> Nguồn chuẩn: `PEMS_CANONICAL_BUSINESS_RULES` Mục 8.

```text
Actor: Staff Leader của ĐÚNG campus đó
visit_request_campuses.status = ASSIGNED (kèm host bắt buộc)
decision_actor_role = STAFF_LEADER
decided_by = current user
decided_at = now
visit_requests.status = PARTIALLY_APPROVED hoặc APPROVED (tổng hợp từ các campus)
```

Với mỗi campus instance:

```text
status = ASSIGNED
coordinator_user_id = Staff Leader của campus đó
coordinator_assigned_by = HO
coordinator_assigned_at = now
```

Sau đó Staff Leader từng campus gán host chính thức.

---

## 8. Host assignment chi tiết

### 8.1 Điều kiện instance

Cho phép gán host khi:

```text
visit_requests.status = APPROVED
visit_request_campuses.status = ASSIGNED hoặc ASSIGNED nếu chưa set host và business cho phép
visit_request_campuses.current_host_user_id IS NULL
campus instance chưa CANCELLED/CLOSED
```

### 8.2 Điều kiện actor

```text
Actor phải là Staff Leader của đúng campus instance.
HO không trực tiếp gán host nếu không có UC riêng.
Admin không gán host nghiệp vụ.
```

### 8.3 Điều kiện host candidate

```text
role_code = STAFF
sub_role = STAFF
status = ACTIVE
primary_campus_id = instance.campus_id
department_type = IC
department status = ACTIVE
```

### 8.4 Sau khi gán host

```text
current_host_user_id = selected staff
host_assigned_by = Staff Leader
host_assigned_at = now
status = ASSIGNED
Tạo notification/email cho host
Ghi audit log
```

---

## 9. Lifecycle sau khi có host

```text
ASSIGNED
→ BEFORE_VISIT
→ DURING_VISIT
→ AFTER_VISIT
→ CLOSED
```

### 9.1 ASSIGNED

Đã có host, nhưng chưa bắt đầu preparation chính thức hoặc chưa chuyển sang BEFORE_VISIT.

### 9.2 BEFORE_VISIT

Host chuẩn bị:

```text
Setup thông tin đoàn
Agenda
Logistics/resource requests
Participant invitations
Student/department support
Calendar reminders
```

### 9.3 DURING_VISIT

Đang tiếp khách:

```text
Check agenda
Update logistics nếu cần
Meeting minutes
Partner/contact updates
Scan business card
Collect live notes
```

### 9.4 AFTER_VISIT

Sau tiếp khách:

```text
Finalize minutes
Upload documents/photos
Face tagging
News draft
Feedback
Action items
```

### 9.5 CLOSED

Đóng hồ sơ campus instance:

```text
closed_by
closed_at
close_note
```

Sau CLOSED, khóa chỉnh sửa vận hành chính nếu chưa có reopen flow.

---

## 10. Cancellation flow

### 10.1 Before approval

Nếu request đang `PENDING_APPROVAL`:

```text
Không cancel.
Dùng reject flow.
```

> **Cập nhật theo implementation hiện tại — 2026-07-02:** rule trên chỉ còn đúng cho Staff Leader/HO. Visitor hiện được tự hủy chính request của mình ngay cả khi còn `PENDING_APPROVAL` (xem "V11.5" đầu file để biết chi tiết metadata/cascade). Evidence: `backend/PEMS.Application/Delegations/Commands/CancelVisitRequest/CancelVisitRequestCommandHandler.cs`.

### 10.2 Visitor self-service cancel

Cho phép khi:

```text
visit_requests.status = APPROVED
instance chưa DURING_VISIT/AFTER_VISIT/CLOSED
visitor là owner của request
```

Set metadata:

```text
cancelled_by = visitor_user_id
cancelled_at = now
cancellation_actor_type = VISITOR
cancellation_source = SELF_SERVICE
cancellation_reason = visitor input
```

Case:

```text
Single-campus full cancel → request CANCELLED + instance CANCELLED.
Multi-campus full cancel → request CANCELLED + active future instances CANCELLED.
Multi-campus partial cancel → chỉ instance đó CANCELLED, request vẫn APPROVED nếu còn instance active.
```

### 10.3 Host cancel by external confirmation

Cho phép khi:

```text
current user = current_host_user_id
instance chưa DURING_VISIT/AFTER_VISIT/CLOSED
khách đã xác nhận hủy ngoài hệ thống
```

Set metadata:

```text
cancelled_by = host user id
cancelled_at = now
cancellation_actor_type = HOST
cancellation_source = EXTERNAL_CONFIRMATION
cancellation_reason bắt buộc ghi rõ kênh/thời điểm/người xác nhận/lý do
```

### 10.4 Không có internal-decision cancel sau approved

Không code:

```text
Staff Leader cancel after approved
HO cancel after approved
Admin cancel delegation
Department cancel delegation
SYSTEM cancel delegation
```

Nếu nghiệp vụ đổi ý, phải patch schema trước.

---

## 11. Logistics/resource flow

```text
Host tạo logistics item
→ requested_to_department_id = GENERAL department cùng campus
→ Department Leader nhận
→ Department Leader approve/assign/propose modification/reject
→ Department Staff xử lý nếu được assign
→ status đi qua các bước tương ứng
→ DONE khi hoàn tất
```

Status hợp lệ:

> **Cập nhật theo implementation hiện tại — 2026-07-02:** DB đã bỏ `PLANNED`, `RECEIVED`, `READY` và thêm `DECLINED`. Danh sách dưới đây theo `LogisticsItemStatus.cs` hiện tại.

```text
REQUESTED, CHANGE_PROPOSED, ASSIGNED, ACCEPTED,
IN_PROGRESS, DONE, REJECTED, DECLINED, CANCELLED
```

---

## 12. Participants flow

Participant internal gồm:

```text
IC_HOST
IC_SUPPORT
DEPT_SUPPORT
STUDENT
```

Status:

```text
INVITED
ACCEPTED
DECLINED
ASSIGNED
REMOVED
```

Unique rule:

```text
Một user chỉ có một row participant trong cùng visit_instance_id.
Không seed/code cùng user vừa IC_HOST vừa IC_SUPPORT trong một instance.
```

---

## 13. Time/status consistency

Backend không nhất thiết tự đổi status theo clock nếu chưa có scheduler, nhưng dữ liệu seed/test phải hợp lý:

```text
BEFORE_VISIT: planned_start_at ở tương lai gần.
DURING_VISIT: current timestamp nằm giữa planned_start_at và planned_end_at.
AFTER_VISIT: planned_end_at đã qua gần đây.
CLOSED: planned_end_at đã qua và closed_at sau planned_end_at.
CANCELLED: cancelled_at trước planned_start_at.
```

---

## 14. API rules

Mọi API phải:

```text
[ ] Check auth.
[ ] Check role/subRole.
[ ] Check scope.
[ ] Check status transition.
[ ] Check ownership/host/coordinator/participant relationship.
[ ] Không tin body campusId/departmentId/userId nếu chưa verify DB.
[ ] Ghi audit cho approve/reject/assign/cancel/close.
[ ] Gửi notification/email khi nghiệp vụ yêu cầu.
```

---

## 15. Frontend rules

Frontend phải:

```text
[ ] Không hiển thị multi-campus pending HO cho Staff Leader/Staff.
[ ] Nút approve single-campus chỉ cho Staff Leader đúng campus.
[ ] Nút approve multi-campus chỉ cho HO.
[ ] Nút assign host chỉ cho Staff Leader đúng campus sau APPROVED.
[ ] Host dropdown chỉ có IC Staff thường hợp lệ.
[ ] Nút cancel sau APPROVED chỉ cho Visitor owner hoặc Host đúng instance.
[ ] Không show cancel khi DURING/AFTER/CLOSED.
[ ] Form submit bắt buộc GUEST và EXTERNAL_SUPPORT.
[ ] Nút “Là tôi” copy dữ liệu người đăng ký vào EXTERNAL_SUPPORT.
```

---

## 16. Manual test checklist

```text
[ ] Visitor submit single-campus, Staff Leader HN thấy, HO không xử lý.
[ ] Visitor submit multi-campus, chỉ HO thấy trước approve.
[ ] Staff Leader từng campus duyệt phần của mình; không cơ sở nào chờ HO.
[ ] Staff Leader gán host, host dropdown không có Staff Leader/Department/Student.
[ ] IC Staff host thấy instance được gán.
[ ] Department Leader chỉ thấy logistics department mình.
[ ] Student chỉ thấy instance được invite.
[ ] Visitor thấy request của chính mình.
[ ] Visitor tự hủy single-campus future approved.
[ ] Visitor tự hủy multi-campus full.
[ ] Visitor hủy partial campus instance trong multi-campus.
[ ] Host hủy thay khách với EXTERNAL_CONFIRMATION.
[ ] Không cancel được DURING_VISIT/AFTER_VISIT/CLOSED.
[ ] Request pending muốn dừng thì reject, không cancel.
```

---

# PHẦN B — NỘI DUNG GỐC / LEGACY PRESERVED CONTENT

> Phần này được giữ nguyên để đối chiếu lịch sử. Không dùng phần này để code nếu mâu thuẫn với PHẦN A hoặc file canonical.

<!-- =====================================================================
PEMS DOC UPDATE v8.2-full-preserved-cancel-delegation-no-external-note
Generated: 2026-06-19
Mode: PRESERVE ORIGINAL CONTENT + ADD V8.2 OVERRIDE.
No original section below has been removed or compressed.
===================================================================== -->

# V8.2 Override — Luồng hủy đơn thuộc Delegation và không dùng external_confirmation_note

> Tài liệu gốc bên dưới được giữ nguyên để không mất nội dung. Tuy nhiên, nếu có đoạn cũ ghi “Đã duyệt — chưa có HOST”, “Staff click nhận đón”, hoặc “mỗi cơ sở duyệt lại sau HO”, thì áp dụng override V8.2 trong phần này.


## V8.2 Addendum — UC-136 Cancel Visit Request thuộc Delegation Reception Management

> Phần này là nội dung bổ sung, không xóa nội dung gốc. Nếu nội dung gốc có flow cũ như “đã duyệt nhưng chưa có host” hoặc “mỗi cơ sở duyệt lại sau HO”, hãy ưu tiên rule V8.2 trong phần addendum này.

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


## Luồng SINGLE_CAMPUS sau V8.2

```text
Visitor submit form + OTP verified
→ visit_requests.status = PENDING_APPROVAL
→ visit_request_campuses.status = WAITING_REQUEST_APPROVAL
→ Staff Leader campus duyệt hoặc từ chối
→ Nếu duyệt: Staff Leader chọn host ngay
→ visit_requests.status = APPROVED
→ visit_request_campuses.status = ASSIGNED
→ BEFORE_VISIT → DURING_VISIT → AFTER_VISIT → CLOSED
```

## Luồng MULTI_CAMPUS sau V8.2

> Cập nhật theo campus-independent approval + confirmation gate. HO chỉ theo dõi read-only.
> Nguồn chuẩn: `PEMS_CANONICAL_BUSINESS_RULES` Mục 6.3.

```text
Visitor submit form + OTP verified
→ visit_requests.status = PENDING_CONTACT_CONFIRMATION
→ mỗi campus instance = WAITING_CONTACT_CONFIRMATION
→ mỗi cơ sở mời đầu mối đoàn khách CỦA CƠ SỞ ĐÓ xác nhận
→ đầu mối cuối cùng xác nhận → gate mở, xử lý từng cơ sở độc lập:
    • có Host dự kiến còn hợp lệ → ASSIGNED (kích hoạt đề xuất)
    • chờ phân công sau / đề xuất hết hiệu lực → WAITING_REQUEST_APPROVAL
→ Staff Leader từng cơ sở duyệt + gán host cho cơ sở còn lại
→ Host bấm "Bắt đầu chuẩn bị": ASSIGNED → BEFORE_VISIT
→ từng campus vận hành độc lập: BEFORE_VISIT → DURING_VISIT → AFTER_VISIT → CLOSED
```

## Luồng hủy sau V8.2

```text
Visitor tự hủy đơn của mình
→ cancellation_source = SELF_SERVICE
→ cancellation_reason = lý do visitor nhập, nếu có

Host hủy thay khách sau khi xác nhận ngoài hệ thống
→ cancellation_source = EXTERNAL_CONFIRMATION
→ cancellation_reason = ghi rõ kênh xác nhận, thời điểm, người xác nhận, lý do

Staff Leader/HO hủy do quyết định nội bộ
→ cancellation_source = INTERNAL_DECISION
→ cancellation_reason bắt buộc
```

---

# HỆ THỐNG QUẢN LÝ TIẾP KHÁCH QUỐC TẾ — TÀI LIỆU LUỒNG CHÍNH

> Tài liệu này mô tả toàn bộ luồng nghiệp vụ, phân quyền và quy trình xử lý của hệ thống quản lý tiếp khách quốc tế tới thăm các cơ sở của Trường Đại học FPT Việt Nam (5 cơ sở trên toàn quốc).

---

## 1. TỔNG QUAN HỆ THỐNG

### 1.1 Mục đích
Hệ thống hỗ trợ các nghiệp vụ Hợp tác Quốc tế (HTQT), bao gồm:
- Quản lý lịch và công khai lịch chương trình
- Quản lý lịch của cán bộ HTQT và sinh viên liên quan
- Quản lý hoạt động tiếp khách (Visiting Request, Visit Online Tour)
- Quản lý đối tác (bao gồm cả đối tác chưa ký kết)

### 1.2 Phạm vi
- **5 Cơ sở (CS):** HN, HCM, ĐN, CT, QN (ví dụ)
- **HO:** Văn phòng trung tâm, quản lý toàn bộ 5 cơ sở

---

## 2. CÁC ROLE TRONG HỆ THỐNG

| Role | Mô tả |
|---|---|
| **HO** | Quản lý chung, toàn quyền với 5 cơ sở. Xử lý các đoàn khách liên cơ sở |
| **Admin** | Quản trị viên kỹ thuật, cấu hình hệ thống và API |
| **Staff** | Nhân sự phòng Hợp tác Quốc tế (IC) tại một cơ sở cụ thể |
| **Staff_Lead** | Trưởng phòng IC, đứng đầu một cơ sở |
| **Dept** | Nhân sự thuộc các phòng ban khác (bao gồm Trưởng phòng và Nhân viên) |
| **Student** | Sinh viên hỗ trợ (buddy, media, v.v.) |
| **Visitor (có tài khoản)** | Khách có thể đăng nhập, xem dữ liệu được phân quyền |
| **Visitor (không có tài khoản)** | Khách đăng ký thăm mà không cần đăng nhập |

---

## 3. CÁC TRẠNG THÁI CỦA ĐOÀN KHÁCH

```
Chờ duyệt → Từ chối
          → Đã duyệt (chưa có HOST)
              → Trước tiếp khách  (Tab 1 — sau khi Staff nhận đón & tạo đoàn)
                  → Trong tiếp khách (Tab 2 — sau khi HOST xác nhận tab 1)
                      → Sau tiếp khách  (Tab 3 — sau khi HOST xác nhận tab 2)
                          → Đã đóng đoàn (sau khi HOST đóng đoàn)
```

---

## 4. LUỒNG CHÍNH — INPUT 1: KHÁCH TỰ GỬI YÊU CẦU

### 4.1 Trường hợp A — Khách muốn thăm 1 cơ sở duy nhất

```
[Visitor gửi form yêu cầu]
        ↓
[Staff_Lead & Staff của cơ sở đó thấy yêu cầu — trạng thái: Chờ duyệt]
        ↓ (chỉ Staff_Lead được ra quyết định)
   ┌────┴────┐
[Từ chối]  [Duyệt]
    ↓           ↓
Staff_Lead   Trạng thái: "Đã duyệt — chưa có HOST"
điền lý do       ↓
Khách thấy   [Một Staff click "Nhận đón"]
lý do từ chối    ↓
             [Trang tạo đoàn khách — Staff điền thông tin & tạo]
                 ↓
             Trạng thái: "Trước tiếp khách"
             HOST mặc định = Staff vừa nhận đón
             (HOST có thể đổi cho Staff khác cùng phòng IC)
                 ↓
             [Gửi lời mời tham gia tới: Staff khác, Dept_Lead, Student]
                 ↓
             → Tiếp tục quy trình 3 tab (xem Mục 6)
```

### 4.2 Trường hợp B — Khách muốn thăm liên cơ sở (≥2 cơ sở)

```
[Visitor gửi form yêu cầu]
        ↓
[HO tiếp nhận — trạng thái: Chờ duyệt]
        ↓ (HO ra quyết định)
   ┌────┴────┐
[Từ chối]  [Duyệt]
    ↓           ↓
HO điền     HO chuyển tiếp yêu cầu tới từng Cơ sở liên quan
lý do            ↓ (mỗi cơ sở xử lý độc lập)
Gửi email   [Staff_Lead của từng cơ sở tiếp nhận]
cho khách        ↓
             ┌────┴────┐
          [Từ chối]  [Duyệt]
              ↓           ↓
          Staff_Lead   Trạng thái: "Đã duyệt — chưa có HOST"
          điền lý do       ↓
          HO thấy lý do [Một Staff tại cơ sở click "Nhận đón"]
          HO liên hệ       ↓
          thông báo khách  [Tạo đoàn khách]
                           ↓
                       → Tiếp tục quy trình 3 tab (xem Mục 6)
```

> **Lưu ý:** Với liên cơ sở, mỗi cơ sở tạo đoàn khách và quản lý quy trình độc lập nhau. HO theo dõi tổng thể.

---

## 5. LUỒNG CHÍNH — INPUT 2: STAFF CHỦ ĐỘNG TẠO ĐOÀN KHÁCH

### 5.1 Staff tạo đoàn thăm cơ sở của chính mình (Cơ sở A → A)

```
[Staff tại cơ sở A click "Tạo đoàn khách", chọn cơ sở A]
        ↓
[Staff điền thông tin & tạo đoàn]
        ↓
Trạng thái: "Trước tiếp khách"
HOST mặc định = Staff tạo đoàn
(có thể đổi HOST cho Staff khác cùng phòng IC)
        ↓
[Gửi lời mời tới: Staff khác, Dept_Lead, Student]
        ↓
→ Tiếp tục quy trình 3 tab (xem Mục 6)
```

### 5.2 Staff tạo đoàn thăm cơ sở khác (Cơ sở A → B)

```
[Staff tại cơ sở A click "Tạo đoàn khách", chọn cơ sở B]
        ↓
[Staff điền thông tin & gửi tới Cơ sở B]
        ↓
[Staff_Lead & Staff của Cơ sở B thấy đơn — trạng thái: Chờ duyệt]
        ↓ (chỉ Staff_Lead cơ sở B ra quyết định)
   ┌────┴────┐
[Từ chối]  [Duyệt]
    ↓           ↓
Staff_Lead B  Trạng thái: "Đã duyệt — chưa có HOST"
điền lý do        ↓
Staff A thấy  [Một Staff tại Cơ sở B click "Nhận đón"]
lý do từ chối     ↓
              [Tạo đoàn khách tại Cơ sở B]
                  ↓
              → Tiếp tục quy trình 3 tab (xem Mục 6)
```

### 5.3 Staff tạo đoàn thăm liên cơ sở (Cơ sở A → C & D)
thì sẽ để ho duyệt hoăc từ chối , nếu từ chối thì điền lí do, nếu duyệt thì auto các staff leader các cơ sở đó chịu trách nhiêm, staff leader có thể gán host cho người khác cũng được.
( chỉ ho mới nhìn đc đơn liên cơ sở, staff leader chỉ nhìn được đơn liên cơ sở mà ho đã duyệt và nhảy về campus tương ứng)
---

## 6. QUY TRÌNH 3 TAB TIẾP KHÁCH (DÙNG CHUNG CHO MỌI LUỒNG)

Sau khi đoàn khách được tạo thành công, HOST quản lý đoàn qua 3 tab:

### Tab 1 — TRƯỚC TIẾP KHÁCH

**Trạng thái đoàn:** `Trước tiếp khách`

**HOST thực hiện:**
- Xem chi tiết đoàn khách
- Thực hiện Setup & Detail Setup
- Gửi yêu cầu mượn đồ tới Trưởng phòng của các phòng ban khác
- Theo dõi xác nhận/từ chối từ những người được mời
- Theo dõi xác nhận cho mượn đồ từ phòng ban khác
- Khi mọi thứ hoàn tất → HOST **Xác nhận** để chuyển sang Tab 2

### Tab 2 — TRONG TIẾP KHÁCH

**Trạng thái đoàn:** `Trong tiếp khách`

**Các chức năng:**
- Feedback
- Tạo đối tác
- Tạo biên bản cuộc họp
- Scan card visit
- Thêm tài liệu cho đối tác

Khi hoàn tất → HOST **Xác nhận** để chuyển sang Tab 3

### Tab 3 — SAU TIẾP KHÁCH

**Trạng thái đoàn:** `Sau tiếp khách`

**Các chức năng:**
- Upload album ảnh (do sinh viên chụp trong buổi tiếp khách)
- Gán tên và thông tin card visit lên khuôn mặt trong ảnh
- Đăng bài tin tức về đoàn khách đã tới thăm

Khi hoàn tất → HOST **Đóng đoàn** → Trạng thái: `Đã đóng đoàn`

> **Lưu ý quan trọng:** Sau khi đóng đoàn, toàn bộ hoạt động trong 3 tab bị **disable** — không thể chỉnh sửa.

---

## 7. PHÂN QUYỀN TRONG QUY TRÌNH 3 TAB

### HOST (Staff được chỉ định)
- **Tab 1:** Toàn quyền — setup, detail setup, gửi yêu cầu mượn đồ, xác nhận chuyển tab
- **Tab 2:** Toàn quyền — feedback, tạo đối tác, tạo biên bản, scan card, thêm tài liệu
- **Tab 3:** Toàn quyền — upload ảnh, gán thông tin, đăng bài tin tức
- **Đặc quyền:** Có thể **Đóng đoàn**

### STAFF (Nhân sự phòng IC, không phải HOST)
- **Tab 1:** Xem toàn bộ; chỉ xác nhận/từ chối lời mời của chính mình
- **Tab 2:** Feedback, tạo đối tác, tạo biên bản cuộc họp, thêm tài liệu, scan card visit
- **Tab 3:** Upload album ảnh, đăng bài tin
- **Không thể:** Đóng đoàn

### DEPT (Nhân sự phòng ban khác — được mời tham gia)
- **Tab 1:** Xem toàn bộ; chỉ xác nhận/từ chối lời mời của chính mình
- **Tab 2:** Feedback, tạo biên bản cuộc họp
- **Tab 3:** Chỉ xem album ảnh và bài tin tức
- **Không thể:** Upload ảnh, đăng bài, đóng đoàn

### DEPT (Trưởng phòng phòng ban — nhận yêu cầu mượn đồ)
- **Tab 1:** Xem toàn bộ chi tiết setup và detail setup
- **Tab 2:** Chỉ feedback
- **Không thể:** Đóng đoàn

### STUDENT (Sinh viên hỗ trợ)
- **Tab 1:** Xem toàn bộ; chỉ xác nhận/từ chối lời mời của chính mình
- **Tab 2:** Feedback, tạo biên bản cuộc họp
- **Tab 3:** Upload album ảnh, đăng bài tin
- **Không thể:** Đóng đoàn

### STAFF_LEAD (Trưởng phòng IC)
- Xem và phê duyệt/từ chối đơn yêu cầu tới tham quan
- Theo dõi chi tiết quy trình sau khi có HOST nhận đón
- **Không thể:** Thao tác bất kỳ hành động nào trong 3 tab

### HO
- Tiếp nhận & phê duyệt/từ chối đơn liên cơ sở
- Điều phối đơn về các cơ sở liên quan
- Theo dõi trạng thái phê duyệt từ các cơ sở
- Xem biên bản cuộc họp của các cơ sở
- **Không thể:** Thao tác quy trình nội bộ của từng cơ sở

### VISITOR (Khách)
- Gửi yêu cầu tới tham quan
- Theo dõi trạng thái đơn của mình
- Xem lý do từ chối (nếu bị từ chối)
- Xem thông tin setup và detail setup (nếu được duyệt)
- Feedback
- Xem bài tin tức và album ảnh về đoàn của mình

---

## 8. LUỒNG XỬ LÝ YÊU CẦU MƯỢN ĐỒ & THƯ MỜI THAM GIA

### Nguyên tắc
- **Trưởng phòng** của phòng ban khác là người **mặc định nhận** lời mời / yêu cầu mượn đồ từ HOST
- Trưởng phòng có thể **tự xử lý** hoặc **phân công cho nhân viên**

### Luồng xử lý Thư mời tham gia

```
[HOST gửi thư mời tới Trưởng phòng]
        ↓
[Trưởng phòng xem & quyết định]
     ┌──┴──┐
  [Tự làm]  [Phân công nhân viên]
     ↓              ↓
[Xác nhận]   [Nhân viên nhận nhiệm vụ]
hoặc               ↓
[Từ chối     [Xác nhận] hoặc [Từ chối + lý do]
+ lý do]

Trạng thái thư mời:
  Xác nhận → "Hoàn thành"
  Từ chối  → "Từ chối" + lý do
```

### Luồng xử lý Yêu cầu mượn đồ (2 bước B1 & B2)

#### Bước B1 — Xác nhận mượn

```
[Nhân viên/Trưởng phòng xem yêu cầu mượn đồ]
        ↓
   ┌────┼────┐
[Xác nhận] [Từ chối] [Đề xuất thay thế]
    ↓           ↓            ↓
Trạng thái: Trạng thái: [HOST xem xét đề xuất]
"Đang làm" "Từ chối"    ┌───┴───┐
→ Tiếp B2   + lý do  [Đồng ý] [Không đồng ý]
                         ↓           ↓
                     "Đang làm" "Từ chối" + lý do
                     → Tiếp B2
```

#### Bước B2 — Biên bản bàn giao & nghiệm thu (chỉ khi B1 = "Đang làm")

```
[Nhân viên bên cho mượn đồ & HOST ký kết biên bản]
        ↓
[Ký kết 4 lần: Bàn giao (2 lần) + Nghiệm thu (2 lần)]
        ↓
[Đủ 4 lần ký] → Trạng thái: "Hoàn thành"

Trạng thái đồng bộ tới: Nhân viên, Trưởng phòng, HOST
```

---

## 9. TÓM TẮT QUAN HỆ GIỮA CÁC ROLE

```
                    ┌─────────┐
                    │   HO    │ ← Quản lý liên cơ sở
                    └────┬────┘
          ┌──────────────┼──────────────┐
     ┌────┴────┐    ┌────┴────┐    ┌────┴────┐
     │ Cơ sở A│    │ Cơ sở B │    │ Cơ sở C │  ...
     └────┬────┘    └─────────┘    └─────────┘
          │
    ┌─────┴──────┐
    │ Staff_Lead │ ← Phê duyệt đơn, theo dõi quy trình
    └─────┬──────┘
          │
    ┌─────┴──────┐
    │   Staff    │ ← Nhận đón, làm HOST, thực thi 3 tab
    └─────┬──────┘
          │  (mời tham gia)
    ┌─────┼──────────┬──────────┐
    │     │          │          │
┌───┴──┐ ┌┴──────┐ ┌┴───────┐  │
│Dept  │ │Student│ │Dept    │  │
│Lead  │ │       │ │(nhân   │  │
│(mời) │ │       │ │viên)   │  │
└──────┘ └───────┘ └────────┘  │
                          (mượn đồ)
```

---

## 10. CÁC ĐIỂM ĐẶC BIỆT CẦN LƯU Ý

1. **Visitor không có tài khoản** vẫn có thể gửi form đăng ký tới thăm mà không cần đăng nhập.
2. **HOST mặc định** là Staff đầu tiên bấm "Nhận đón", nhưng có thể chuyển HOST sang Staff khác trong cùng phòng IC.
3. **Liên cơ sở từ phía khách:** HO phê duyệt trước, sau đó phân về từng cơ sở. Mỗi cơ sở có Staff_Lead phê duyệt riêng.
4. **Liên cơ sở từ phía Staff:** Staff tạo → HO phê duyệt → phân về cơ sở đích → Staff_Lead tại cơ sở đích phê duyệt.
5. **Đóng đoàn là hành động không thể đảo ngược** — toàn bộ 3 tab bị khóa.
6. **Biên bản bàn giao mượn đồ** yêu cầu đúng 4 lần ký mới hoàn thành, trạng thái đồng bộ thời gian thực cho tất cả các bên liên quan.
7. **Staff_Lead và HO** chỉ có vai trò giám sát sau khi đoàn đã được tạo — không thể thao tác trong quy trình 3 tab.
