# PEMS — Master Prompt: Per-Campus Operational Contact Visibility & Proposed Host Activation

## 0. Vai trò và mục tiêu

Bạn là **Senior Software Architect, Senior .NET Engineer, Senior React/TypeScript Engineer, MySQL Database Engineer và QA/Security Reviewer** của dự án PEMS.

Nhiệm vụ là hoàn tất đồng thời hai lát cắt có quan hệ trực tiếp:

1. **Hiển thị đầy đủ “Đầu mối đoàn khách phối hợp tại cơ sở” trên mọi màn hình chi tiết**, đúng theo từng `visitInstanceId`.
2. **Thay direct processing cũ bằng mô hình Host dự kiến theo từng campus**, chỉ kích hoạt thành Current Host sau khi cổng xác nhận đầu mối đoàn khách được mở.

Đây là một chương trình hard cutover xuyên suốt:

```text
Database
→ Entity / EF configuration
→ Constants
→ DTO / Contract
→ Handler / Service
→ Trigger / Aggregate
→ API
→ Frontend types
→ Frontend API mapping
→ UI
→ Notification
→ Tests
→ Fresh database verification
```

Không được sửa một layer rồi tuyên bố hoàn thành.

---

# 1. Quyết định nghiệp vụ có hiệu lực cao nhất

Các quyết định dưới đây thay thế mọi logic hoặc test cũ mâu thuẫn.

## 1.1 Ba quan hệ độc lập

### Người đăng ký — Registrant

- Là người tạo và gửi đơn.
- Có thể là khách bên ngoài hoặc nhân sự FPTU tạo thay.
- Không mặc định là đầu mối đoàn khách hoặc Host.

### Đầu mối đoàn khách phối hợp tại cơ sở — Operational Contact

- Là người thuộc phía đoàn khách.
- Phối hợp với một campus FPTU cụ thể.
- Là dữ liệu **per-campus**.
- Mỗi campus có thể có một Operational Contact khác nhau.
- Không còn Primary Contact hoặc Contact Person đại diện cho toàn request.

### Người phụ trách tiếp đón — Current Host

- Là nhân sự phía FPTU chịu trách nhiệm chuẩn bị và tiếp đón.
- Là dữ liệu per-campus.
- Chỉ có hiệu lực sau khi được phân công chính thức.
- Không được dùng thay cho Operational Contact.

Ba người này có thể là ba người hoàn toàn khác nhau.

## 1.2 Nhãn UI bắt buộc

### Tiếng Việt

- Người đăng ký
- Thông tin người đăng ký
- Đầu mối đoàn khách phối hợp tại cơ sở
- Host dự kiến
- Người phụ trách tiếp đón

### Tiếng Anh

- Registrant
- Registrant information
- Guest Delegation Coordination Contact at Campus
- Proposed Reception Host
- Reception Host

Không dùng nhãn chung “Người phụ trách” cho cả Operational Contact và Host.

## 1.3 Giữ rule nội bộ không được giả làm đầu mối đoàn khách

Giữ nguyên:

```text
INTERNAL_REGISTRANT_CANNOT_BE_CONTACT
```

Nhân sự FPTU tạo đơn thay khách không được tự điền mình làm Operational Contact để bỏ qua cổng xác nhận.

Ví dụ hợp lệ:

```text
Registrant       = Staff Leader Hà Nội
OperationalContact = Ms. Anna, phía đoàn khách
ProposedHost       = Staff Leader Hà Nội
CurrentHost        = NULL trước khi gate mở
```

Không yêu cầu:

```text
OperationalContact = Registrant
```

để được chọn Host dự kiến.

## 1.4 Thay direct processing bằng proposed-host activation

Không giữ cơ chế:

```text
Submit
→ SELF_HOST / ASSIGN_HOST
→ gán Current Host ngay
→ ASSIGNED
```

Thay bằng:

```text
Submit
→ lưu Host dự kiến
→ chờ confirmation gate
→ gate mở
→ revalidate Host dự kiến
→ kích hoạt thành Current Host
→ ASSIGNED
```

Nếu không có Host dự kiến hoặc Host dự kiến không còn hợp lệ:

```text
gate mở
→ WAITING_REQUEST_APPROVAL
→ Staff Leader duyệt và chọn Host
→ ASSIGNED
```

---

# 2. Lifecycle và confirmation gate

## 2.1 Lifecycle hợp lệ

```text
WAITING_CONTACT_CONFIRMATION
→ WAITING_REQUEST_APPROVAL hoặc ASSIGNED
→ BEFORE_VISIT
→ DURING_VISIT
→ AFTER_VISIT
→ CLOSED
```

Các nhánh hợp lệ:

### Nhánh A — Không có Host dự kiến

```text
WAITING_CONTACT_CONFIRMATION
→ confirmation gate mở
→ WAITING_REQUEST_APPROVAL
→ Staff Leader approve + assign/self-host
→ ASSIGNED
```

### Nhánh B — Có Host dự kiến hợp lệ

```text
WAITING_CONTACT_CONFIRMATION
→ confirmation gate mở
→ activate proposed Host
→ ASSIGNED
```

### Nhánh C — Tất cả contact self-match ngay khi tạo, có Host dự kiến hợp lệ

```text
Create request
→ gate đã mở trong transaction tạo
→ activate proposed Host
→ ASSIGNED
```

### Nhánh D — Tất cả contact self-match, không có Host dự kiến

```text
Create request
→ gate đã mở
→ WAITING_REQUEST_APPROVAL
```

### Nhánh E — Host dự kiến không còn hợp lệ

```text
WAITING_CONTACT_CONFIRMATION
→ contact cuối xác nhận thành công
→ proposed Host revalidation thất bại
→ WAITING_REQUEST_APPROVAL
```

Việc xác nhận Operational Contact vẫn thành công; không rollback chỉ vì Host dự kiến không hợp lệ.

## 2.2 Chuyển trạng thái bị cấm

```text
WAITING_CONTACT_CONFIRMATION → BEFORE_VISIT
WAITING_CONTACT_CONFIRMATION → DURING_VISIT
ASSIGNED → DURING_VISIT
```

Sau khi được kích hoạt thành Current Host, Host vẫn phải bấm:

```text
ASSIGNED
→ START_PREPARATION
→ BEFORE_VISIT
```

Setup mutations chỉ chạy ở `BEFORE_VISIT`.

## 2.3 Confirmation gate là request-level

Với request nhiều campus:

- Một contact đã xác nhận nhưng sibling còn pending thì gate vẫn đóng.
- Chưa kích hoạt bất kỳ Host dự kiến nào.
- Chỉ sau khi contact cuối cùng xác nhận thì xử lý proposal của tất cả campus.

Ví dụ:

```text
Hà Nội:
  contact = Anna
  proposedHost = A

Đà Nẵng:
  contact = John
  mode = WAIT_FOR_LATER
```

Anna xác nhận nhưng John chưa xác nhận:

```text
request gate vẫn đóng
Hà Nội chưa ASSIGNED
current_host_user_id Hà Nội vẫn NULL
```

John xác nhận:

```text
request gate mở
Hà Nội: proposal hợp lệ → ASSIGNED
Đà Nẵng: WAIT_FOR_LATER → WAITING_REQUEST_APPROVAL
```

Sau đó recompute aggregate request status bằng service hiện hành.

## 2.4 Ý nghĩa “Operational Contact confirmation không tự gán Host”

Quy tắc đúng sau khi hợp nhất là:

- Xác nhận contact **không tự chọn một Host mới**.
- Xác nhận contact chỉ được kích hoạt một Host đã được preauthorize trước đó.
- Không có proposal thì không auto-assign.
- Proposal không hợp lệ thì không auto-assign.
- Không tự fallback sang người khác.

---

# 3. Chính sách quyền Host dự kiến

## 3.1 Staff Leader cùng campus

Được:

- chọn chính mình làm Host dự kiến;
- chọn một IC Staff hợp lệ cùng campus làm Host dự kiến;
- chọn chờ phân công sau;
- cập nhật hoặc xóa proposal trong pre-decision window;
- khi gate mở, proposal hợp lệ được kích hoạt mà không cần duyệt lần hai.

## 3.2 IC Staff thường cùng campus

Được:

- chọn chính mình làm Host dự kiến;
- chọn chờ phân công sau;
- cập nhật proposal của chính mình trong pre-decision window.

Không được:

- chọn người khác;
- phân công nhân sự khác;
- sửa proposal của campus không thuộc relation hợp lệ.

Nếu IC Staff chọn chính mình và vẫn hợp lệ lúc gate mở:

```text
current_host_user_id = chính IC Staff đó
status = ASSIGNED
```

## 3.3 Không được chọn Host dự kiến

- Visitor;
- Operational Contact bên ngoài;
- HO;
- Admin;
- Department user;
- Student;
- actor khác campus;
- actor không có relation tạo đơn nội bộ hợp lệ.

## 3.4 Backend là nguồn quyết định quyền

Frontend không tự suy quyền chỉ bằng role.

Backend phải trả capability rõ ràng, theo convention hiện có, tương đương:

```text
canProposeSelfAsHost
canProposeOtherHost
canWaitForLaterAssignment
canUpdateProposedHost
```

Handler vẫn phải revalidate quyền; capability không phải security token.

---

# 4. Mô hình dữ liệu per-campus

## 4.1 Không tạo bảng mới

Bổ sung tối thiểu vào `visit_request_campuses` hoặc vị trí campus-scoped tương đương:

```text
host_selection_mode
proposed_host_user_id
proposed_host_by_user_id
proposed_host_at
proposed_host_note               -- chỉ nếu UI thật sự cần
proposed_host_activated_at       -- nếu cần idempotency/audit rõ ràng
proposed_host_activation_status  -- chỉ nếu cần biểu diễn NEEDS_RESELECTION
```

Ưu tiên field hiện có nếu đã có cùng nghĩa.

## 4.2 Constraint

```text
WAIT_FOR_LATER
→ proposed_host_user_id IS NULL

SELF hoặc SELECTED
→ proposed_host_user_id IS NOT NULL
```

Foreign key:

```text
proposed_host_user_id    → users.user_id
proposed_host_by_user_id → users.user_id
```

## 4.3 Không được làm

- Không dùng `current_host_user_id` để chứa Host dự kiến.
- Không set Current Host trước khi gate mở.
- Không đưa Host dự kiến lên request-level.
- Không đưa Operational Contact trở lại request-level.
- Không tạo compatibility property `ContactPerson*` hoặc `PrimaryContact*`.

---

# 5. Backend create/edit contract

## 5.1 Campus create DTO

Dùng tên theo convention codebase, tương đương:

```text
CampusCreateDto {
    ...
    hostSelectionMode
    proposedHostUserId
}
```

Giá trị:

```text
SELF
SELECTED
WAIT_FOR_LATER
```

Đây không phải lifecycle status.

## 5.2 Validation

### WAIT_FOR_LATER

```text
proposedHostUserId = NULL
```

### SELF

- Backend tự resolve current user nếu có thể.
- Không tin user ID frontend truyền.
- Nếu nhận ID thì phải bằng current user.

### SELECTED

- `proposedHostUserId` bắt buộc.
- Chỉ Staff Leader cùng campus.
- Target phải qua Host eligibility hiện hành.

### External/Visitor create

- Không nhận quyền chọn Host.
- Backend force `WAIT_FOR_LATER`.
- Payload giả mạo phải bị bỏ qua hoặc từ chối theo API convention, nhưng không được chấp nhận.

## 5.3 Pending edit

Khi gate còn đóng và campus còn pre-decision:

- actor đủ quyền được đổi hoặc xóa Host dự kiến;
- bắt buộc rowVersion;
- không đổi Current Host;
- đổi proposal không được đổi contact identity;
- đổi Operational Contact không được tự đổi proposal, trừ khi business rule hiện hành yêu cầu reset có bằng chứng.

Sau `ASSIGNED`:

- không dùng proposed-host flow để đổi Current Host;
- dùng Host transfer flow riêng;
- thay Operational Contact không tự đổi Current Host.

## 5.4 Endpoint cập nhật proposal

Nếu create form chưa đủ, bổ sung endpoint campus-scoped theo convention hiện hành, tương đương:

```http
PUT /api/v2/visit-requests/{visitRequestId}/campuses/{visitInstanceId}/proposed-host
```

Payload:

```json
{
  "hostSelectionMode": "SELF | SELECTED | WAIT_FOR_LATER",
  "proposedHostUserId": 123,
  "rowVersion": 4
}
```

Chỉ cho phép khi:

- request còn live;
- campus chưa ASSIGNED;
- campus chưa bị quyết định;
- còn pre-decision;
- chưa bắt đầu visit;
- actor đúng scope request-instance-campus.

---

# 6. Kích hoạt proposal khi confirmation gate mở

## 6.1 Transaction bắt buộc

Khi contact cuối cùng xác nhận:

1. Lock visit request.
2. Lock các campus instance theo thứ tự ổn định.
3. Kiểm tra gate revision và trạng thái hiện tại.
4. Apply confirmation.
5. Recompute confirmation gate.
6. Nếu gate vừa mở:
   - duyệt từng campus pre-decision;
   - revalidate proposed Host;
   - proposal hợp lệ → activate;
   - thiếu/không hợp lệ → `WAITING_REQUEST_APPROVAL`.
7. Recompute aggregate request status.
8. Ghi audit.
9. Commit.
10. Gửi notification theo cơ chế post-commit/outbox hiện hành.

Không để campus `ASSIGNED` trong khi request gate còn đóng.

## 6.2 Revalidation điều kiện chung

- request còn live;
- campus còn pre-decision;
- gate thật sự đã mở;
- proposed user còn tồn tại;
- account ACTIVE;
- không bị khóa/vô hiệu hóa;
- đúng campus;
- đúng IC department;
- đúng role/sub-role;
- chưa có Current Host khác;
- proposal chưa bị thay thế hoặc xóa;
- proposal còn đúng revision/rowVersion;
- người tạo proposal có quyền hợp lệ;
- conflict lịch giữ đúng semantics hiện hành; nếu hiện chỉ là warning thì không biến thành blocker.

## 6.3 SELF của IC Staff

- proposed user chính là actor tạo proposal;
- actor là `STAFF + STAFF`;
- cùng campus;
- ACTIVE;
- thuộc IC department.

## 6.4 SELF của Staff Leader

- proposed user chính Staff Leader;
- `STAFF + LEADER`;
- ACTIVE;
- cùng campus;
- self-host được nghiệp vụ cho phép.

## 6.5 SELECTED

- chỉ Staff Leader tạo;
- target là IC Staff hợp lệ cùng campus;
- áp dụng cùng eligibility rule với Host candidate hiện hành.

## 6.6 Proposal không hợp lệ

- Không tự chọn người khác.
- Không gán fallback Host.
- Contact confirmation vẫn thành công.
- Campus về `WAITING_REQUEST_APPROVAL`.
- Giữ proposal cũ để audit hoặc đánh dấu `NEEDS_RESELECTION`.
- Thông báo Staff Leader chọn lại.
- Không gửi lỗi nghiệp vụ Host cho Operational Contact.

## 6.7 Idempotency và concurrency

Replay/concurrent confirmation phải bảo đảm:

- chỉ một transaction mở gate;
- không activate hai lần;
- không tạo `IC_HOST` relation trùng;
- không gửi notification trùng;
- không ghi audit trùng;
- không tăng revision hai lần;
- proposal update đồng thời không làm activate stale proposal.

---

# 7. Decision metadata, audit và notification

## 7.1 Khi lưu Host dự kiến

Ghi:

- proposed host;
- người đề xuất;
- thời điểm;
- mode;
- source/create route;
- old/new values;
- rowVersion/revision.

Không ghi `decided_at` ở bước này vì chưa có quyết định chính thức.

## 7.2 Khi proposal được kích hoạt

- set `current_host_user_id`;
- tạo/đồng bộ `IC_HOST` relation nếu flow hiện hành yêu cầu;
- set `decided_by` theo actor đã preauthorize proposal;
- set `decided_at` tại thời điểm activation;
- set decision source rõ nghĩa;
- set status `ASSIGNED`;
- audit transition và actor/source;
- set activation timestamp/status.

Ưu tiên source hiện có nếu đúng nghĩa. Nếu chưa có, bổ sung đồng bộ SQL/constants/EF/tests/docs, ví dụ:

```text
PREAUTHORIZED_HOST_ACTIVATION
```

Không tái sử dụng source sai nghĩa.

## 7.3 Notification

### Khi chỉ lưu proposal

Không gửi “đã được phân công chính thức”.

Nếu gửi thông báo, wording phải là:

```text
Bạn được đề xuất làm người phụ trách tiếp đón,
đang chờ đầu mối đoàn khách xác nhận.
```

### Khi activation thành công

- gửi notification chính thức cho Current Host;
- gửi Staff Leader nếu policy hiện hành cần;
- gửi update cho Registrant/Operational Contact theo recipient policy;
- không gửi trùng khi replay.

### Khi activation thất bại

- gửi Staff Leader yêu cầu chọn lại Host;
- không báo lỗi Host cho Operational Contact.

---

# 8. SQL, trigger và aggregate

## 8.1 Transition phải cho phép

```text
WAITING_CONTACT_CONFIRMATION → WAITING_REQUEST_APPROVAL
WAITING_CONTACT_CONFIRMATION → ASSIGNED
WAITING_REQUEST_APPROVAL     → ASSIGNED
ASSIGNED                     → BEFORE_VISIT
BEFORE_VISIT                 → DURING_VISIT
DURING_VISIT                 → AFTER_VISIT
AFTER_VISIT                  → CLOSED
```

## 8.2 WCC → ASSIGNED chỉ hợp lệ khi

- request gate đã mở;
- Current Host không null;
- proposal vừa được revalidate;
- request/campus còn live;
- transition nằm trong cùng transaction activation.

## 8.3 Guard bắt buộc

- `ASSIGNED` trở đi phải có Current Host.
- Không `ASSIGNED` sau gate đóng.
- Không Current Host khi vẫn `WAITING_CONTACT_CONFIRMATION`.
- `ASSIGNED` không được có setup data.
- `ASSIGNED → DURING_VISIT` bị chặn.
- Participant/logistics `ASSIGNED` không bị sửa.

Aggregate service phải coi `ASSIGNED` là campus đã được phê duyệt/phân công.

## 8.4 Fresh database gates

Expected baseline:

```text
81 tables
33 triggers
```

Không tạo bảng hoặc trigger mới nếu có thể sửa trigger hiện hành.

Bổ sung verify:

```text
ASSIGNED behind closed gate = 0
ASSIGNED without Current Host = 0
Current Host while WAITING_CONTACT_CONFIRMATION = 0
ASSIGNED with setup data = 0
proposed Host may exist while WCC
invalid proposal does not fail contact confirmation
beyond-gate without required contact = 0
participant ASSIGNED unchanged
logistics ASSIGNED unchanged
```

Canonical hash phải repin sau thay đổi SQL và ghi rõ lý do.

---

# 9. Backend read models cho màn chi tiết

Audit working tree hiện tại, không tin report cũ.

Kiểm tra tối thiểu:

1. `ResolvedVisitFormDto` / `ResolvedCampusVisitDto`.
2. `GetSubmittedVisitRequestFormDetail`.
3. `GetEditableVisitRequestDetail`.
4. `GetVisitProcessDetail`.
5. `GetVisitInstanceSummary`.
6. `GetVisitInstanceContribution`.
7. `GetVisitInvitationDetail`.
8. `GetMyVisitInvitationById` nếu có.
9. `GetDepartmentInvitationDetail` nếu có.
10. Staff calendar/detail consumer.
11. `ViewGuestDelegationList` và detail navigation payload.
12. Process summary.
13. Visitor visit detail.
14. HO read-only detail.

Mỗi campus DTO phải có object Operational Contact rõ ràng, tái sử dụng type hiện có nếu có:

```text
OperationalContact {
    fullName
    organization
    jobTitle
    phone
    email
    confirmationStatus
    confirmationSource
    confirmedAt
}
```

Mỗi campus DTO cần có thông tin Host theo trạng thái:

```text
ProposedHost {
    userId
    fullName
    organizationOrDepartment
    selectionMode
    proposalStatus
    proposedAt
}

CurrentHost {
    userId
    fullName
    email
    phone
    departmentName
}
```

Không trả raw user ID cho UI nếu actor không cần; DTO nội bộ có thể mang ID để action theo quyền.

Không tạo request-level Operational Contact đại diện.

Endpoint campus-scoped phải lấy đúng snapshot của `visitInstanceId`, không sibling và không `campus[0]`.

---

# 10. Frontend types, API và component dùng chung

## 10.1 Xóa contract legacy

Không tiếp tục dùng runtime:

```text
primaryContact
contactPerson
contactPersonFullName
contactPersonEmail
contactPersonPhone
contactPersonOrganization
primaryContactAccessStatus
primaryContactVerifiedAt
```

Thay bằng Operational Contact nằm trong đúng campus/instance DTO.

Không dùng `any` để né compile.

## 10.2 Quét và phân loại

```text
primaryContact
contactPerson
contactPersonFullName
contactPersonEmail
contactPersonPhone
contactPersonOrganization
operationalContact
proposedHost
currentHostName
hostName
SELF_HOST
ASSIGN_HOST
DIRECT_PROCESSING_NEEDS_SELF_MATCHED_CONTACT
directProcessing
processingMode
decideAtSubmit
```

Không global replace.

## 10.3 Component OperationalContactReadOnly

Hiển thị:

```text
Đầu mối đoàn khách phối hợp tại cơ sở
```

Các field:

- Họ và tên;
- Đơn vị / tổ chức;
- Chức vụ;
- Số điện thoại;
- Email;
- Trạng thái xác nhận;
- Nguồn xác nhận nếu được phép;
- Xác nhận lúc.

Yêu cầu:

- không ẩn block chỉ vì `fullName` null;
- email/organization/status vẫn hiện khi pending;
- từng field là label/value riêng;
- responsive;
- read-only;
- không tự fetch;
- không đọc role/localStorage;
- không tự suy permission;
- không lấy sibling campus;
- `data-testid` gắn `visitInstanceId`.

## 10.4 Component ProposedHostReadOnly

Khi gate còn đóng và có proposal:

```text
Host dự kiến
- Họ và tên
- Campus/phòng ban
- Trạng thái: Chờ đầu mối đoàn khách xác nhận
```

Không hiển thị người này dưới nhãn “Người phụ trách tiếp đón”.

Khi activation thành công:

- ẩn trạng thái proposal pending;
- hiển thị trong block “Người phụ trách tiếp đón”.

Khi invalid:

```text
Host dự kiến cần được chọn lại
```

chỉ hiển thị nếu backend trả trạng thái phù hợp.

---

# 11. Frontend create form

Trong từng campus card của form tạo nội bộ, thêm:

```text
Phương án người phụ trách tiếp đón
```

## Staff Leader

Hiện đủ:

1. Tôi sẽ là người phụ trách tiếp đón.
2. Chọn người phụ trách khác.
3. Chờ phân công sau.

## IC Staff

Chỉ hiện:

1. Tôi sẽ là người phụ trách tiếp đón.
2. Chờ phân công sau.

Không cho chọn người khác.

## Visitor/external

- Không hiện section.
- Backend mặc định `WAIT_FOR_LATER`.

## Chọn người khác

- dùng Host candidates API hiện hành;
- target đúng campus;
- eligibility đúng;
- conflict hiển thị theo semantics hiện có;
- gửi user ID, không gửi name/email thay thế.

## Copy campus

Không copy proposed Host sang campus khác.

Campus mới mặc định:

```text
WAIT_FOR_LATER
```

---

# 12. Các màn chi tiết bắt buộc sửa

## 12.1 VisitRequestV2DetailView

- Xóa request-level `primaryContact`.
- Mỗi `CampusVisitDetailCard` hiển thị Operational Contact đúng campus.
- Hiển thị Proposed Host hoặc Current Host theo trạng thái.
- Contact actions nhận `visitRequestId + visitInstanceId`.
- Proposed-host actions cũng campus-scoped.
- Không dùng một người đại diện cho toàn request.

## 12.2 CampusVisitDetailCard

Tách ba block:

1. Đầu mối đoàn khách phối hợp tại cơ sở.
2. Host dự kiến, nếu chưa activation.
3. Người phụ trách tiếp đón, nếu đã activation.

Không ghép contact thành một chuỗi.

Không dùng `contact?.fullName` làm điều kiện render.

## 12.3 SubmittedVisitRequestDetailModal / SubmittedVisitRequestInfoPanel

- Bỏ `data.contactPerson`.
- Uniform request lấy Operational Contact của campus được project.
- Mixed request dùng campus cards.
- Không flat-map contact hoặc Host đại diện.
- Không dùng localStorage role để quyết định lộ dữ liệu.

## 12.4 VisitProcess.tsx / RequestInfoReadOnly

Process detail là instance-scoped.

Hiển thị riêng:

- Thông tin người đăng ký.
- Đầu mối đoàn khách phối hợp tại cơ sở.
- Host dự kiến nếu còn pending.
- Người phụ trách tiếp đón nếu đã activation.

Không lặp sibling campus.

## 12.5 VisitorVisitDetailPage

- Xóa phụ thuộc `contactPersonFullName`.
- Không dùng contract Contact Person cũ.
- Hiển thị Operational Contact đúng instance.
- Host dự kiến và Current Host không được trộn.
- Pending contact có email vẫn render.

## 12.6 VisitProcessSummaryPage

- Bổ sung Operational Contact đúng instance.
- Bổ sung Proposed Host/Current Host đúng trạng thái.
- Không lấy request-level contact hoặc campus đầu tiên.

## 12.7 HoVisitProcessDetail

Hiện tại `campus.person = hostName` chỉ là Host.

Phải:

- giữ Current Host dưới “Người phụ trách tiếp đón”;
- thêm “Đầu mối đoàn khách phối hợp tại cơ sở”;
- hiển thị Host dự kiến nếu chưa activation;
- không phụ thuộc list payload thiếu dữ liệu;
- gọi detail/summary endpoint được backend scope nếu cần;
- HO read-only, không mở mutation.

## 12.8 VisitContributionPage

Audit và bổ sung Operational Contact/Host đúng instance nếu màn hiển thị thông tin đoàn.

## 12.9 Invitation detail

Audit:

- participant invitation;
- department invitation;
- Host handover invitation;
- contact confirmation/transfer detail.

Mỗi màn chỉ hiển thị contact/Host của campus liên quan.

## 12.10 Calendar detail

Nếu có thông tin chuyến thăm:

- hiển thị Operational Contact đúng instance;
- hiển thị Current Host đúng instance;
- không dùng Registrant hoặc Host thay cho contact;
- không mở rộng quyền.

---

# 13. I18N

## Tiếng Việt

- Người đăng ký
- Thông tin người đăng ký
- Đầu mối đoàn khách phối hợp tại cơ sở
- Host dự kiến
- Phương án người phụ trách tiếp đón
- Tôi sẽ là người phụ trách tiếp đón
- Chọn người phụ trách khác
- Chờ phân công sau
- Người phụ trách tiếp đón
- Trạng thái xác nhận
- Nguồn xác nhận
- Xác nhận lúc
- Chờ xác nhận
- Đã xác nhận
- Đã từ chối lời mời
- Lời mời đã hết hạn
- Đang chờ chuyển giao
- Chờ đầu mối đoàn khách xác nhận
- Host dự kiến cần được chọn lại
- Chưa có thông tin đầu mối đoàn khách

## Tiếng Anh

- Registrant
- Registrant information
- Guest Delegation Coordination Contact at Campus
- Proposed Reception Host
- Reception host arrangement
- I will be the reception host
- Select another reception host
- Assign later
- Reception Host
- Confirmation status
- Confirmation source
- Confirmed at
- Pending confirmation
- Confirmed
- Invitation declined
- Invitation expired
- Transfer pending
- Waiting for guest delegation contact confirmation
- Proposed host must be selected again
- Guest delegation contact information is not available

Không dùng “Primary Contact”.

Không để chuỗi tiếng Việt hard-code trong giao diện tiếng Anh.

---

# 14. Loại bỏ direct processing cũ

Phân loại từng call site trước khi xóa:

```text
SELF_HOST
ASSIGN_HOST
DIRECT_PROCESSING_NEEDS_SELF_MATCHED_CONTACT
directProcessing
processingMode
decideAtSubmit
```

Xóa hoặc migrate sang:

```text
hostSelectionMode
proposedHostUserId
```

Không giữ flow gán Current Host trước gate.

Không xóa logic Staff Leader self-host ở bước approve hiện hành nếu nó phục vụ nhánh `WAITING_REQUEST_APPROVAL → ASSIGNED`.

Giữ:

```text
INTERNAL_REGISTRANT_CANNOT_BE_CONTACT
```

Không thêm lại request-level Primary Contact để cứu test/frontend.

---

# 15. Test backend bắt buộc

## 15.1 Contact read-model và scope

1. Single-campus trả đúng Operational Contact.
2. Multi-campus A/B trả hai contact khác nhau.
3. Actor campus B không nhận contact campus A.
4. Không endpoint dùng `campus[0]`.
5. Pending confirmation vẫn trả email/organization/status.
6. Thiếu fullName nhưng có email vẫn trả object.
7. Confirmed contact trả đúng linked user.
8. GetVisitProcessDetail đúng instance.
9. GetVisitInstanceSummary đúng instance.
10. GetVisitInstanceContribution đúng instance.
11. Uniform projection đúng campus.
12. Mixed request không flat-map contact.
13. Pending transfer giữ owner cũ.
14. Accept transfer trả contact mới đúng campus.
15. Registrant, Operational Contact, Current Host là ba object riêng.

## 15.2 Create/proposal permission

1. Staff Leader SELF lưu proposal, Current Host null, campus WCC.
2. Staff Leader SELECTED lưu target hợp lệ, Current Host null.
3. IC Staff SELF được phép.
4. IC Staff SELECTED người khác bị từ chối.
5. Visitor giả mạo proposal bị từ chối/bỏ qua theo convention.
6. Cross-campus proposed Host bị từ chối.
7. WAIT_FOR_LATER lưu proposal user null.

## 15.3 Gate activation

1. Staff Leader SELF + final confirmation → ASSIGNED.
2. Staff Leader SELECTED + final confirmation → ASSIGNED.
3. IC Staff SELF + final confirmation → ASSIGNED.
4. WAIT_FOR_LATER + final confirmation → WRA.
5. Host INACTIVE → confirmation success, WRA.
6. Host chuyển campus → WRA.
7. Host mất role eligibility → WRA.
8. All contacts self-match + valid proposal → ASSIGNED trong create transaction.
9. All contacts self-match + WAIT_FOR_LATER → WRA.
10. Campus A proposal, B wait → A ASSIGNED, B WRA sau final confirmation.
11. Contact A xác nhận trước → không activation.
12. Invalid proposal không làm confirmation fail.

## 15.4 Idempotency/concurrency

1. Replay final confirmation không duplicate.
2. Concurrent final confirmation chỉ một activation.
3. Concurrent proposal update/final confirmation không activate stale proposal.
4. Không duplicate IC_HOST/audit/notification/revision.

## 15.5 Lifecycle

1. Auto-assigned Host ở `ASSIGNED` chưa được setup.
2. Start preparation → `BEFORE_VISIT`.
3. Setup mutation ở `ASSIGNED` bị chặn.
4. Direct `ASSIGNED → DURING_VISIT` bị chặn.
5. Thay contact sau ASSIGNED không đổi Host.
6. Host transfer không đổi contact.
7. Participant/logistics ASSIGNED không đổi.

## 15.6 Bốn integration test đang đỏ

Không:

- bỏ `INTERNAL_REGISTRANT_CANNOT_BE_CONTACT`;
- đổi expected đơn thuần;
- xóa toàn bộ khả năng chọn Host trước.

Viết lại theo mô hình:

- internal registrant khác Operational Contact;
- proposed Host lưu độc lập;
- Current Host null trước gate;
- final confirmation kích hoạt proposal hợp lệ;
- WAIT_FOR_LATER về WRA;
- Staff Leader SELF/SELECTED hoạt động;
- IC Staff SELF hoạt động;
- direct SELF_HOST/ASSIGN_HOST cũ không còn.

Mục tiêu là toàn bộ IntegrationTests xanh; tổng discovery mới có thể lớn hơn 1531.

---

# 16. Test frontend bắt buộc

1. Staff Leader thấy ba lựa chọn.
2. IC Staff chỉ thấy SELF và WAIT_FOR_LATER.
3. Visitor không thấy control proposal.
4. SELECTED tải candidate đúng campus.
5. Copy campus không copy proposal.
6. SELF không cho truyền user khác.
7. Pending gate hiển thị Host dự kiến.
8. Pending gate không hiển thị Current Host chính thức.
9. Sau activation hiển thị Current Host.
10. Invalid proposal hiển thị cần chọn lại.
11. Operational Contact và Proposed Host là hai block riêng.
12. Registrant, Operational Contact và Current Host là ba block riêng.
13. Thiếu fullName nhưng có email vẫn render contact.
14. Hai campus hiển thị hai contact khác nhau.
15. VI/EN đầy đủ.
16. Không raw enum.
17. Không còn Primary Contact/Contact Person legacy.
18. Contact action và proposed-host action dùng đúng `visitInstanceId`.
19. Campus A không gọi action bằng ID campus B.
20. HO detail read-only hiển thị đúng contact/proposal/current Host.

---

# 17. Real-stack/manual verification

Kiểm tra tối thiểu:

1. Visitor tạo single-campus.
2. Visitor tạo multi-campus.
3. Staff Leader tạo, chọn SELF.
4. Staff Leader tạo, chọn SELECTED.
5. Staff Leader chọn WAIT_FOR_LATER.
6. IC Staff tạo, chọn SELF.
7. IC Staff không thể chọn người khác.
8. Operational Contact pending.
9. Operational Contact confirmed.
10. Contact chỉ có email, không fullName.
11. Multi-campus final confirmation kích hoạt theo từng campus.
12. Invalid proposal fallback WRA.
13. Registrant, Operational Contact và Host là ba người khác nhau.
14. Staff Leader chỉ thấy campus mình.
15. HO xem read-only.
16. Current Host bấm Start preparation.
17. Host dự kiến chưa có setup permission.
18. Replay confirmation không gửi trùng.

---

# 18. Thứ tự triển khai

1. Preflight:
   - branch;
   - HEAD;
   - working tree;
   - stash;
   - baseline 1527 green / 4 red;
   - canonical hash hiện tại;
   - lifecycle checkpoint.

2. Lập hai matrix:
   - Direct processing → proposed-host replacement.
   - Screen → endpoint → DTO → Operational Contact/Proposed Host/Current Host → component.

3. Cập nhật canonical SQL, entity, EF và hash pin.

4. Cập nhật create/edit/proposed-host contract.

5. Triển khai gate activation transaction.

6. Cập nhật trigger/aggregate/allowedActions/capabilities.

7. Cập nhật notifications.

8. Cập nhật backend read models.

9. Cập nhật frontend types/API.

10. Cập nhật create form.

11. Tạo/tái sử dụng read-only components.

12. Cập nhật toàn bộ màn detail.

13. Viết/sửa focused tests.

14. Chạy:
    - solution build;
    - UnitTests;
    - ArchitectureTests;
    - IntegrationTests;
    - frontend typecheck;
    - frontend build;
    - frontend unit tests;
    - fresh disposable DB gates.

15. Chạy manual/real-stack.

16. Chỉ commit khi toàn bộ gate xanh.

Không chạy full suite sau từng file; chạy focused trước, full khi lỗi đã giảm đáng kể.

---

# 19. Điều cấm

Không:

- merge;
- deploy;
- đụng `pems_db`;
- đụng stash;
- reset/discard WIP ngoài scope;
- tạo bảng mới;
- tạo Primary Contact request-level;
- tạo `ContactPerson*`;
- lấy campus đầu tiên làm đại diện;
- nới confirmation gate;
- gán Current Host trước gate;
- tự động bắt đầu `BEFORE_VISIT`;
- dùng Host thay Operational Contact;
- dùng Registrant thay Operational Contact;
- dùng Proposed Host thay Current Host;
- dùng proposed-host flow để transfer sau ASSIGNED;
- cho IC Staff phân công người khác;
- cho frontend tự quyết định permission;
- dùng localStorage role để lộ dữ liệu;
- global replace;
- thay participant/logistics ASSIGNED;
- commit khi test còn đỏ.

---

# 20. Điều kiện commit

Chỉ commit khi:

1. Solution build 0 lỗi.
2. UnitTests xanh.
3. ArchitectureTests xanh.
4. IntegrationTests xanh toàn bộ.
5. Frontend typecheck xanh.
6. Frontend build xanh.
7. Frontend unit tests bị ảnh hưởng xanh.
8. Fresh DB:
   - 81 tables;
   - 33 triggers;
   - canonical hash khớp pin mới;
   - contact gates xanh;
   - lifecycle gates xanh;
   - không ASSIGNED sau gate đóng;
   - không ASSIGNED thiếu Host;
   - không Current Host ở WCC;
   - không ASSIGNED có setup data.
9. Không còn runtime legacy contact fields.
10. Không còn direct-processing path gán Current Host trước gate.
11. Mọi màn trong matrix đã được kiểm tra.
12. Không lộ dữ liệu sibling campus.
13. `ASSIGNED → BEFORE_VISIT` còn nguyên.
14. Participant/logistics ASSIGNED không đổi.

Tạo checkpoint commit:

```text
feat(visit): activate proposed campus host and expose per-campus guest contacts
```

Không merge và không deploy.

---

# 21. Báo cáo cuối

Báo theo cấu trúc:

1. Preflight.
2. Business decision implemented.
3. Direct-processing legacy matrix.
4. Screen/API/DTO/component matrix.
5. Database fields, constraints, triggers.
6. Permission policy:
   - Staff Leader;
   - IC Staff;
   - external actors.
7. Create/edit flow.
8. Confirmation activation transaction.
9. Multi-campus behavior.
10. Invalid-proposal fallback.
11. Operational Contact visibility theo từng màn.
12. Proposed Host và Current Host visibility.
13. Backend files changed.
14. Frontend files changed.
15. Test results từng suite.
16. Fresh DB evidence.
17. Canonical hash.
18. Lifecycle checkpoint.
19. Legacy grep.
20. Git diff stat.
21. Commit SHA.

Không báo hoàn thành nếu:

- chỉ đổi bốn expected test;
- chỉ sửa UI;
- chỉ thêm field;
- chưa activation proposal;
- chưa scope đúng Operational Contact theo campus;
- chưa full test/fresh DB.
