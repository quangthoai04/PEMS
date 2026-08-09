# PEMS — MASTER PLAN CẬP NHẬT LOGIC & SỬA LỖI VISIT V2

**Baseline rà soát:** nhánh `Dev`  
**HEAD tham chiếu:** `7eacbca8de24bfbed383c57222eaa22ec907175b`  
**Ngày lập kế hoạch:** 2026-08-09

---

# 1. Mục tiêu

Sửa các lỗi và khoảng trống logic hiện tại của luồng quản lý đơn tham quan V2, tập trung vào:

- quyền xem danh sách / chi tiết của `STAFF`;
- quyền chỉnh sửa của người đăng ký khi đơn đang chờ đầu mối xác nhận;
- đồng bộ frontend và backend đối với thông tin người đăng ký;
- contact gate toàn đơn trước khi `STAFF LEADER` được review;
- luồng hủy / gửi lại lời mời đầu mối;
- lịch sử thay đổi đầu mối;
- xác nhận / từ chối đầu mối cho người dùng đã đăng nhập;
- xác nhận / từ chối trực tiếp từ email mà không bắt buộc Google login;
- optimistic concurrency khi Staff Leader duyệt / từ chối;
- UX accordion nhiều campus: cho phép mở nhiều campus cùng lúc.

Nguyên tắc thực hiện:

1. Backend là source of truth cho permission, lifecycle và business rule.
2. Frontend không tự suy diễn quyền nếu backend đã trả `allowedActions` / `capabilities`.
3. Không mở rộng quyền chỉ để chữa lỗi navigation.
4. Không tạo database table mới nếu bảng / token / revision hiện tại đã đáp ứng.
5. Không dùng GET để thay đổi business state.
6. Các thao tác review phải dựa trên đúng phiên bản mà người review đã nhìn thấy.
7. Không trộn workflow “sửa nội dung đơn” với workflow “thay đổi identity đầu mối”.
8. Thay đổi tối thiểu, bám architecture hiện có.

---


---

# 1A. Danh sách lỗi master V01 → V18

| ID | Nhóm | Tóm tắt |
|---|---|---|
| V01 | Permission/navigation | STAFF thấy row nhưng mở generic detail bị 403 |
| V02 | Edit contract | FE cho sửa registrant snapshot nhưng BE coi immutable |
| V03 | Pending edit | Registrant chưa sửa ổn định khi đang chờ contact confirm |
| V04 | Security/gate | Staff Leader thấy request trước khi tất cả contact xác nhận |
| V05 | Notification | Staff Leader có thể được notify khi contact gate chưa mở |
| V06 | Contact cancel UX | Hủy lời mời chưa thể hiện hậu quả/state rõ |
| V07 | Contact reinvite | Cancel INITIAL xong khó mời lại cùng email |
| V08 | History identity | Event generic / thiếu detail/eventId ở một số dữ liệu |
| V09 | Logged-in invitee | Người được mời đã login chưa có surface Accept/Decline rõ |
| V10 | Email action | Accept/Decline contact email đang phụ thuộc login; cần public safe POST |
| V11 | Wording | Pending-contact notice có câu mô tả sai business rule |
| V12 | Concurrency | Staff Leader có thể approve/reject dựa trên revision cũ |
| V13 | Multi-campus UX | Accordion chỉ mở được một campus |
| V14 | Stale invitation content | Edit request khi invite pending làm email cũ stale |
| V15 | Revision history | Có revision event nhưng không có before snapshot để diff |
| V16 | News | Process cho tạo tin nhưng Create News lại báo không eligible |
| V17 | Lifecycle | BEFORE_VISIT → DURING_VISIT chưa có earliest gate T-6h |
| V18 | Transfer atomicity | Transfer có thể PENDING nhưng token creation lỗi, UI báo thất bại chung |

---

# 2. Trạng thái code Dev hiện tại

Một số phần đã được cập nhật trên `Dev` và **không nên làm lại từ đầu**:

## 2.1 Đã có

- General expense đã tách read và initialize:
  - GET chỉ đọc;
  - POST initialize mới tạo.
- History detail đã bổ sung xử lý identity-change detail.
- Snapshot history đã có normalizer tốt hơn cho legacy shape.
- Form chỉnh sửa đầu mối đã tránh gửi API khi không thay đổi gì.
- Nút hủy lời mời đã bắt đầu phân biệt:
  - `INITIAL_CONFIRMATION`;
  - `TRANSFER`.
- Amendment frontend đã có validation chi tiết hơn.
- Multi-campus detail đã được đổi thành accordion.

## 2.2 Nhưng accordion hiện đang sai yêu cầu mới

Code hiện tại được viết theo kiểu **single-open accordion**:

```text
mở Hà Nội
→ Hà Nội mở

mở TP.HCM
→ TP.HCM mở
→ Hà Nội tự đóng
```

Yêu cầu đúng:

```text
mở Hà Nội
→ Hà Nội mở

mở TP.HCM
→ Hà Nội vẫn mở
→ TP.HCM cũng mở
```

Mỗi campus phải có state mở/đóng độc lập.

---

# 3. Tổng hợp các vấn đề còn phải sửa

| ID | Vấn đề | Mức độ | BE | FE |
|---|---|---:|---:|---:|
| V01 | STAFF thấy row trong danh sách nhưng vào detail bị 403 | Cao | Có | Có |
| V02 | Form cho sửa thông tin registrant nhưng backend lại coi immutable | Cao | Có | Có |
| V03 | Người đăng ký chưa được sửa đơn đúng cách khi đang chờ contact confirm | Cao | Có | Có |
| V04 | Staff Leader thấy đơn quá sớm trước khi tất cả campus contact xác nhận | Rất cao | Có | Có |
| V05 | Notification Staff Leader có thể bị gửi khi contact gate chưa mở | Cao | Có | Không đáng kể |
| V06 | Hủy lời mời đầu mối chưa thể hiện hậu quả / trạng thái tiếp theo rõ ràng | Trung bình | Có | Có |
| V07 | Sau cancel INITIAL invite, cùng email khó gửi lời mời mới | Cao | Có | Có |
| V08 | Contact history còn event generic / runtime có thể thiếu eventId/detail | Trung bình | Có | Có |
| V09 | Visitor được mời làm contact nhưng đã login vẫn thiếu action rõ ràng | Cao | Có | Có |
| V10 | Email contact confirmation bắt login Google; cần Accept/Decline không login | Cao | Có | Có |
| V11 | Pending notice có câu business wording sai | Thấp | Không | Có |
| V12 | Staff Leader có thể approve/reject dựa trên màn hình cũ | Rất cao | Có | Có |
| V13 | Accordion nhiều campus chỉ mở được một campus tại một thời điểm | Trung bình | Không | Có |
| V14 | Edit trong lúc contact invitation pending có thể làm nội dung email cũ stale | Trung bình | Có thể | Có thể |
| V15 | History báo có phiên bản mới nhưng drawer không hiện field thay đổi vì thiếu snapshot phiên bản trước | Cao | Có | Có |
| V16 | Process cho hiện `Tạo bài tin tức` nhưng trang Create News lại báo chuyến không đủ điều kiện | Cao | Có | Có |
| V17 | BEFORE_VISIT có thể chuyển sang DURING_VISIT quá sớm; chưa có gate T-6 giờ | Cao | Có | Có |
| V18 | Chuyển giao đầu mối có thể tạo TRANSFER PENDING nhưng lỗi khi tạo token/email-action, UI chỉ báo lỗi chung | Rất cao | Có | Có |

---

# 4. Logic đích tổng thể

Luồng chuẩn phải là:

```text
SUBMIT
  ↓
PENDING_CONTACT_CONFIRMATION
  ↓
Registrant vẫn được sửa nội dung hợp lệ
  ↓
TẤT CẢ operational contacts của TẤT CẢ campus xác nhận
  ↓
Global contact gate mở
  ↓
PENDING_APPROVAL
  ↓
Staff Leader của từng campus mới thấy đơn trong review queue
  ↓
Staff Leader review đúng revision hiện tại
  ↓
APPROVE / REJECT
```

Ngoại lệ:

```text
Staff Leader đồng thời là Registrant
```

thì:

- được xem đơn của chính mình qua quan hệ `REGISTRANT`;
- được sửa trong phạm vi registrant được phép;
- **không** vì vai trò Staff Leader mà được review/approve khi global contact gate còn đóng.

---

# PHASE 1 — SỬA QUYỀN XEM LIST / DETAIL

# 5. V01 — STAFF thấy row nhưng detail trả 403

## 5.1 Root cause cần xử lý

List `all` có thể merge row từ nhiều nguồn quan hệ:

- registrant;
- host;
- participant / invitation;
- support;
- department task;
- staff-related scope.

Nhưng row sau merge có thể mất metadata nguồn, đặc biệt `participantId`.

Frontend sau đó fallback sang:

```text
/dashboard/visit/v2/{visitRequestId}
```

trong khi backend detail không cấp quyền normal request detail cho quan hệ đó.

Kết quả:

```text
list thấy row
→ click
→ generic request detail
→ backend 403
```

## 5.2 Không được fix bằng cách

Không làm:

```text
STAFF thấy row
→ cấp broad permission cho STAFF xem toàn bộ request detail
```

Cách đó phá scope.

## 5.3 Cách sửa

Khi merge list, giữ metadata nguồn:

```ts
sourceRelation
participantId
visitInstanceId
taskId
```

Ví dụ:

```text
ATTENDING / PARTICIPANT
→ route invitation / participation detail

HOST
→ route visit process / scoped campus detail

REGISTRANT
→ route request V2 detail

SUPPORT / DEPARTMENT_TASK
→ route đúng task/detail hiện hữu
```

Frontend route theo relation thực tế thay vì luôn dùng request detail.

## 5.4 File trọng tâm

```text
frontend/pems-react/src/pages/dashboard/visit/VisitRequestManagement.tsx
backend/.../ViewGuestDelegationListQueryHandler.cs
backend/.../VisitFormReadService.cs
```

## 5.5 Test

- STAFF participant `INVITED` thấy row → mở đúng invitation.
- STAFF participant `ACCEPTED` → mở đúng flow.
- STAFF participant `DECLINED` nếu vẫn xuất hiện history/list → không dẫn vào URL 403.
- Host → mở đúng campus scope.
- Không có test nào được pass bằng broadening request-detail permission.

---

# PHASE 2 — ĐỒNG BỘ EDIT REGISTRANT + PENDING CONTACT EDIT

# 6. V02 — Frontend cho sửa registrant nhưng backend coi immutable

## 6.1 Vấn đề

Frontend hiện cho nhập:

- Họ tên;
- Đơn vị;
- Chức vụ;
- Quốc tịch;
- Số điện thoại.

Nhưng backend pending-edit đang reject thay đổi các field này bằng immutable registrant rule.

Đây là contract drift.

## 6.2 Logic đích

Trong edit đơn, cho phép sửa **snapshot của đơn**:

```text
RegistrantFullName
RegistrantOrganization
RegistrantJobTitle
RegistrantNationality
RegistrantPhone
```

Giữ immutable:

```text
RegistrantEmail
RegistrantUserId / account binding
PartnerId
```

trừ khi có workflow identity riêng.

## 6.3 Quan trọng

Edit snapshot đơn:

```text
KHÔNG tự động sửa profile User/account.
```

Đây chỉ là dữ liệu người đăng ký được lưu trên request.

## 6.4 Backend

Refactor `ValidateImmutableFields()`:

- email vẫn immutable;
- account binding immutable;
- Partner identity immutable;
- bỏ 5 snapshot field trên khỏi immutable list.

Implement `ApplyCommonFields()` để:

- so sánh before/after;
- update request;
- ghi `AuditLogChange`;
- bump/request revision đúng architecture hiện tại;
- return `true` khi có thay đổi.

## 6.5 Frontend

Giữ form editable hiện có.

Email phải:

```text
readOnly / disabled
+ wording rõ lý do
```

## 6.6 Test

- đổi full name → save thành công;
- đổi organization → thành công;
- đổi job title → thành công;
- đổi nationality → thành công;
- đổi optional phone → thành công;
- đổi email bằng payload thủ công → backend reject;
- User profile không bị sửa theo request snapshot.

---

# 7. V03 — Cho phép sửa khi đang PENDING_CONTACT_CONFIRMATION

## 7.1 Vấn đề

Service-level logic đã xem:

```text
WAITING_CONTACT_CONFIRMATION
WAITING_REQUEST_APPROVAL
```

là pre-decision.

Nhưng command guard / read capability / frontend status chưa đồng nhất.

## 7.2 Logic đích

Registrant được sửa request trong cả:

```text
PENDING_CONTACT_CONFIRMATION
PENDING_APPROVAL
```

miễn:

- chưa có campus decision khiến request-level edit không còn an toàn;
- vẫn đáp ứng 72h / thời gian tối thiểu;
- version hợp lệ.

## 7.3 Đồng bộ 4 nơi

1. Mutation guard backend.
2. Read service `allowedActions`.
3. List capability `canEditPending`.
4. Frontend edit entry/status.

Không được có:

```text
backend nói được sửa
frontend ẩn nút
```

hoặc ngược lại.

## 7.4 Test

- request đang chờ contact confirm → registrant thấy `Sửa đơn`;
- save hợp lệ → thành công;
- contact invitation vẫn giữ đúng lifecycle sau edit;
- request chưa nhảy sang approval nếu vẫn còn contact pending.

---

# PHASE 3 — GLOBAL CONTACT GATE

# 8. V04 — Staff Leader chỉ được review sau khi TẤT CẢ contact xác nhận

## 8.1 Business rule chuẩn

Với multi-campus:

```text
HN confirmed
HCM pending
```

thì:

```text
Staff Leader HN: KHÔNG thấy request trong review queue
Staff Leader HCM: KHÔNG thấy request trong review queue
```

Chỉ khi:

```text
HN confirmed
HCM confirmed
```

thì global gate mở.

## 8.2 Phải áp dụng đồng nhất

### List

Staff Leader review query không trả request behind contact gate.

### Detail

Staff Leader không được direct-access qua normal leader scope khi gate đóng.

### History

Không dùng URL guessing để bypass gate.

### Submitted form/detail phụ

Các endpoint read liên quan phải dùng cùng visibility rule.

## 8.3 Registrant exception

Order xác định relation nên là:

```text
if isRegistrant
  → registrant scope
else if leader eligible AND gate open
  → leader campus scope
```

Không để leader branch override registrant branch.

## 8.4 Approval guard

Giữ approval guard backend hiện có và bổ sung test.

UI hide không đủ; backend vẫn phải reject khi gate đóng.

---

# 9. V05 — Không gửi Staff Leader notification khi gate chưa mở

## 9.1 Vấn đề

Registrant pending-edit có thể đang notify Staff Leader ngay sau mỗi edit.

Điều này trái với rule:

```text
Staff Leader chưa được review request trước khi global gate mở.
```

## 9.2 Logic đích

Khi edit trong `PENDING_CONTACT_CONFIRMATION`:

```text
không notify Staff Leader review
```

Khi **contact cuối cùng** xác nhận:

```text
gate.Opened == true
→ gửi approval-ready notification một lần
```

Dùng notifier tại final contact acceptance làm canonical trigger.

## 9.3 Test

- 2 campus, contact 1 confirm → không notify leader.
- registrant edit → không notify leader.
- contact cuối confirm → notify đúng leaders.
- retry accept/idempotent → không gửi duplicate.

---

# PHASE 4 — HỦY / GỬI LẠI CONTACT INVITATION

# 10. V06 — Hủy lời mời phải thể hiện rõ hậu quả

## 10.1 Backend semantics hiện tại cần giữ

Cancel:

- invalidate pending tokens;
- change status → `CANCELLED`;
- append identity-change event;
- không tự gán contact khác.

### INITIAL_CONFIRMATION

Sau cancel:

```text
campus vẫn WAITING_CONTACT_CONFIRMATION
không còn active invitation
global gate vẫn đóng
```

### TRANSFER

Sau cancel:

```text
current confirmed contact vẫn giữ role
invitee không nhận role
```

## 10.2 UI sau cancel

INITIAL:

```text
Đã hủy lời mời xác nhận.
Hiện chưa có lời mời đầu mối nào đang hiệu lực.
Cơ sở vẫn chờ xác nhận trước khi đơn được chuyển sang duyệt.
```

TRANSFER:

```text
Đã hủy lời mời chuyển giao.
Đầu mối hiện tại vẫn giữ quyền.
```

## 10.3 Trạng thái read model

Không được map:

```text
không confirmed + không pending
→ PENDING
```

Phải có state rõ như:

```text
NO_ACTIVE_INVITATION
```

hoặc một canonical state tương đương.

## 10.4 Confirmation modal trước cancel

Hiển thị đúng theo invitation kind.

---

# 11. V07 — Có action gửi lời mời mới sau cancel

## 11.1 Bug cần tránh

Sau cancel INITIAL:

```text
email hiện tại == email đang nhập
```

Nếu `SaveOperationalContact` route same-email sang update profile thì sẽ:

```text
không tạo token
không gửi email
```

Người dùng bị kẹt.

## 11.2 Logic đích

Có action rõ:

```text
Gửi lời mời mới
```

hoặc:

```text
Mời xác nhận lại
```

Không bắt người dùng đổi email giả rồi đổi lại.

## 11.3 Backend

Tạo command/use-case nhỏ, reuse invitation service hiện có:

```text
ReinviteOperationalContactConfirmation
```

Điều kiện:

- actor có quyền quản lý contact;
- campus chưa có confirmed contact tương ứng;
- không có active pending invitation;
- email snapshot hợp lệ;
- request/campus còn trong lifecycle cho phép.

Không cần bảng mới.

## 11.4 Resend vs Reinvite

Phân biệt:

```text
RESEND
→ invitation hiện tại vẫn PENDING
→ rotate/reissue token theo policy hiện có

REINVITE
→ invitation cũ CANCELLED/EXPIRED
→ tạo pending identity change / token mới
```

---

# PHASE 5 — CONTACT HISTORY

# 12. V08 — History phải cụ thể và mở được detail

## 12.1 Giữ phần đã có trên Dev

Latest Dev đã có:

- `IDCH` event source;
- identity detail handler;
- masked email;
- status transition;
- actor/campus/time;
- không expose token.

Không tạo history system mới.

## 12.2 Việc còn phải kiểm tra

Nếu UI runtime vẫn hiện:

```text
Vai trò đầu mối liên hệ có thay đổi (...)
```

thì kiểm tra actual API payload:

```text
GET /visit-requests/{id}/history
```

Mỗi identity event phải có:

```text
eventId = IDCH:<identityChangeEventId>
```

## 12.3 Map đầy đủ legacy event type

Canonical mapping nên bao phủ:

```text
INVITATION_CREATED
TRANSFER_REQUESTED
INVITATION_RESENT
INVITATION_CANCELLED
INVITATION_SUPERSEDED
CONFIRMED
TRANSFER_APPLIED
CONFIRMATION_DECLINED
TRANSFER_DECLINED
CONFIRMATION_EXPIRED
TRANSFER_EXPIRED
```

Nếu DB/seed dùng tên legacy khác, thêm alias vào mapping.

## 12.4 Detail drawer hiển thị

- loại sự kiện;
- campus;
- email masked;
- actor;
- timestamp;
- from status;
- to status;
- business reason nếu an toàn và thực sự có ý nghĩa.

Không hiển thị:

- raw token;
- token hash;
- full sensitive email nếu policy mask;
- correlation plumbing;
- internal snapshot JSON.

---

# PHASE 6 — INVITEE ĐÃ LOGIN

# 13. V09 — Visitor được mời làm operational contact phải thấy action

## 13.1 Không cấp full request detail trước accept

Pending invitee chưa phải confirmed operational contact.

Không mở rộng `VisitFormReadService` để họ xem toàn bộ request chỉ vì email match.

## 13.2 Tạo authenticated surface

Ví dụ:

```text
Lời mời đầu mối của tôi
```

Query theo:

```text
current authenticated user email
== pending identity change NewEmailNormalized
```

Hiển thị limited summary:

- request code;
- campus;
- đoàn;
- thời gian;
- masked/basic registrant info nếu policy cho phép;
- invitation expiry;
- Accept;
- Decline.

## 13.3 Authenticated Accept / Decline

Không cần raw token nếu đã login.

Backend kiểm tra:

```text
authenticated
current email matches invite target
identity change PENDING
not expired
request/campus still valid
```

Accept:

```text
link OperationalContactUserId
→ append event
→ nếu final pending contact thì mở global gate
```

Decline:

```text
mark declined
→ request vẫn behind contact gate
```

---

# PHASE 7 — ACCEPT / DECLINE TỪ EMAIL KHÔNG LOGIN

# 14. V10 — Email phải có hai action rõ ràng

Email:

```text
[Xác nhận]
[Từ chối]
```

Không bắt Google login chỉ để thực hiện action từ invitation email.

## 14.1 Không mutate bằng GET

Tuyệt đối không:

```text
GET /accept?token=...
→ accept ngay
```

Lý do:

- Outlook;
- Gmail;
- Defender;
- security scanner;

có thể prefetch URL.

## 14.2 Flow an toàn

Button email mở public confirmation page:

```text
GET page
→ validate token read-only
→ show action + summary
```

Sau đó người dùng bấm xác nhận trên page:

```text
POST Accept
hoặc
POST Decline
```

Không cần Google login.

## 14.3 Token

Reuse token infrastructure hiện tại.

Mint 2 token/action link độc lập:

```text
Token A
IntendedAction = ACCEPT

Token B
IntendedAction = DECLINE
```

Validate:

- token hash;
- intended action;
- recipient;
- identity change;
- status pending;
- expiry;
- replay / used state;
- action group nếu schema hiện có hỗ trợ.

## 14.4 Accept khi invitee chưa có account

Public accept vẫn cần cuối cùng có `OperationalContactUserId`.

Reuse user provisioning hiện có:

```text
find user by normalized email
→ nếu có: link existing user

nếu chưa có:
→ provision/link Visitor-compatible account theo service hiện có
→ đảm bảo lần Google login sau bằng email đó resolve về cùng User
```

Không tạo anonymous pseudo-user riêng.

## 14.5 Decline

Decline không nhất thiết phải provision account.

---

# PHASE 8 — WORDING

# 15. V11 — Sửa pending notice

Hiện tại bỏ câu:

```text
Việc duyệt của các cơ sở không chờ xác nhận này.
```

Chỉ giữ:

```text
Đầu mối {{email}} chưa xác nhận lời mời (hiệu lực 72 giờ).
```

Phải sửa cả VI/EN trong i18n.

Không hardcode trong component.

---

# PHASE 9 — OPTIMISTIC CONCURRENCY CHO APPROVE / REJECT

# 16. V12 — Không cho Staff Leader duyệt phiên bản chưa đọc

## 16.1 Scenario lỗi

```text
Leader mở campus revision v4
↓
Visitor sửa campus
↓
backend tạo v5
↓
Leader vẫn đang nhìn v4
↓
Leader bấm Approve
↓
backend load latest v5 rồi approve
```

Kết quả: Leader duyệt nội dung mà họ chưa đọc.

## 16.2 Contract mới

Approve:

```text
visitRequestId
visitInstanceId
hostUserId
decisionNote
expectedInstanceRowVersion
```

Reject:

```text
visitRequestId
visitInstanceId
decisionNote
expectedInstanceRowVersion
```

## 16.3 Backend

Trong transaction:

```text
SELECT current campus row FOR UPDATE
compare expectedInstanceRowVersion
```

Mismatch:

```http
409 Conflict
```

Stable error code:

```text
VISIT_INSTANCE_VERSION_CONFLICT
```

hoặc reuse canonical conflict code hiện có nếu đã có contract thống nhất.

Message VI:

```text
Thông tin đơn đã được cập nhật sau khi bạn mở màn hình.
Vui lòng tải phiên bản mới nhất và xem lại trước khi duyệt.
```

## 16.4 Frontend

Khi modal approve/reject mở, giữ đúng `rowVersion` của campus đang render.

Submit gửi version đó.

Nếu 409:

- không auto retry;
- không tự approve sau reload;
- đóng/disable decision action;
- hiển thị thông báo blocking;
- CTA `Tải phiên bản mới`;
- reload request/campus;
- Leader phải review và bấm quyết định lại.

## 16.5 Audit endpoint khác

Chỉ áp optimistic concurrency bắt buộc cho mutation phụ thuộc vào nội dung người dùng vừa review.

Ưu tiên audit:

```text
Approve campus
Reject campus
Approve amendment
Reject/decide amendment nếu decision dựa trên snapshot
```

Không blanket-add rowVersion vào mọi API.

---

# PHASE 10 — MULTI-CAMPUS ACCORDION

# 17. V13 — Cho phép mở nhiều campus cùng lúc

## 17.1 Vấn đề hiện tại

`VisitRequestV2DetailView.tsx` đang dùng state tương đương:

```ts
campusChoice: number | null | undefined
```

và derive:

```ts
openCampusId
```

Vì vậy tại một thời điểm chỉ có tối đa một campus mở.

Test hiện tại còn khóa hành vi:

```text
open HCM
→ HN closes
```

Đây phải được thay đổi.

## 17.2 Logic đích

Dùng collection các campus đang mở:

```ts
Set<number>
```

hoặc:

```ts
number[]
```

Khuyến nghị `Set<number>`.

Ví dụ:

```ts
const [openCampusIds, setOpenCampusIds] = useState<Set<number>>(...)
```

Toggle:

```text
nếu ID đang mở
→ remove

nếu ID đang đóng
→ add
```

Không đụng ID khác.

## 17.3 Default state

Với multi-campus:

- có thể giữ UX hiện tại là campus đầu tiên mở mặc định;
- các campus khác đóng;
- sau đó user được mở thêm bao nhiêu campus tùy ý.

Với single-campus:

```text
luôn mở
không cần chevron
```

## 17.4 Deep link

Link:

```text
#contact-{visitInstanceId}
```

phải:

```text
add target campus vào openCampusIds
```

không được replace toàn bộ open state.

Sau render mới scroll.

## 17.5 Background reload

Reload data không được đóng các campus user đang mở nếu campus đó vẫn còn trong payload.

Nếu campus biến mất khỏi authorized payload, prune ID khỏi set.

## 17.6 Test bắt buộc

```text
A mở mặc định
B đóng
click B
→ A mở + B mở

click A
→ A đóng + B vẫn mở

click A lại
→ A mở + B vẫn mở

deep-link B khi A đang mở
→ A vẫn mở + B mở

single campus
→ always expanded, no toggle
```

Xóa test expectation cũ:

```text
opening HCM closes HN
```

## 17.7 File trọng tâm

```text
frontend/pems-react/src/features/visit-request/components/v2/VisitRequestV2DetailView.tsx
frontend/pems-react/src/features/visit-request/__tests__/VisitRequestV2DetailView.test.tsx
```

Không cần backend / DB.

---

# PHASE 11 — EDIT TRONG LÚC INVITATION ĐANG PENDING

# 18. V14 — Tránh email invitation chứa dữ liệu cũ

## 18.1 Edge case

Invitation email có thể chứa:

- đoàn;
- campus;
- thời gian;
- thông tin visit.

Trong lúc invitation vẫn pending, registrant được phép edit.

Ví dụ:

```text
09:00 gửi invitation
10:00 registrant đổi giờ visit
invitee vẫn đang đọc email 09:00
```

Token vẫn hợp lệ nhưng body email đã stale.

## 18.2 Không vội rotate token

Không mặc định vô hiệu token chỉ vì request content thay đổi nếu identity/contact không đổi.

## 18.3 Cách xử lý ưu tiên

Public invitation landing page / authenticated invitation view luôn đọc **current summary** từ DB.

Email có thể ghi thêm:

```text
Thông tin chuyến thăm có thể được người đăng ký cập nhật.
Vui lòng xem thông tin mới nhất tại trang xác nhận.
```

Nếu business yêu cầu email phải luôn current, gửi `INVITATION_DETAILS_UPDATED` notification riêng khi các field trọng yếu thay đổi:

```text
plannedStartAt
plannedEndAt
campus
delegationName
```

Không tạo invitation/token mới nếu không cần.

---


# PHASE 12 — REVISION HISTORY PHẢI LUÔN CÓ BEFORE SNAPSHOT

# 19. V15 — Có event “đã sửa nội dung” nhưng drawer không hiện thay đổi

## 19.1 Triệu chứng thực tế

Ảnh lỗi cho thấy timeline có event:

```text
Kim Min Jae đã sửa nội dung tại FPT University TP.HCM khi còn đang chờ duyệt — phiên bản 2.
```

Nhưng khi mở `Chi tiết thay đổi`:

```text
Phiên bản: — → 2
Sự kiện này không có thay đổi chi tiết nào được ghi nhận.
```

Điểm quan trọng nhất là:

```text
— → 2
```

không phải:

```text
1 → 2
```

Điều này chứng minh history detail **không tìm thấy revision/snapshot trước đó** để so sánh.

Đây không phải chỉ là lỗi render frontend.

---

## 19.2 Root cause trên code hiện tại

`GetVisitHistoryDetailQueryHandler.InstanceRevisionDetailAsync()` đang làm đúng mô hình diff:

```text
load current revision history row
↓
find previous row của cùng visitInstanceId
where previous.FormRevision < current.FormRevision
order descending
↓
DiffSnapshots(previous.SnapshotJson, current.SnapshotJson)
```

Nhưng nếu previous row không tồn tại:

```text
previous == null
```

thì `DiffSnapshots()` chủ động trả:

```text
Fields = []
Collections = []
```

vì nó coi trường hợp không có before snapshot là không đủ bằng chứng để nói dữ liệu trước đó là gì.

Do đó UI nhận được:

```text
previousRevision = null
currentRevision = 2
fields = []
collections = []
```

và hiển thị đúng triệu chứng trong ảnh:

```text
— → 2
Sự kiện này không có thay đổi chi tiết nào được ghi nhận.
```

### Kết luận

Root cause chính cần xử lý là:

```text
revision 2 được ghi
nhưng revision 1 baseline của campus không tồn tại / không truy xuất được
```

hoặc baseline tồn tại nhưng không đúng `visitInstanceId` / `FormRevision` để query hiện tại tìm thấy.

---

## 19.3 Vì sao lỗi có thể xuất hiện dù create path mới đã ghi baseline

Code create V2 mới hiện tại đã ghi:

```text
FormRevision = 1
SnapshotJson = VisitFormRevisionSnapshotBuilder.Instance(...)
```

cho từng campus.

Vì vậy cần audit các nguồn dữ liệu khác:

```text
canonical SQL seed
legacy seed
migration/backfill
request được tạo trước khi baseline revision được bổ sung
test fixture
manual SQL
restore DB cũ
```

Một request seed/legacy có thể đang có:

```text
visit_instance_form_details.form_revision = 1
```

nhưng bảng:

```text
visit_instance_form_revision_history
```

không có row revision 1.

Khi user sửa:

```text
form_revision 1 → 2
```

service ghi revision history 2.

Lúc đọc history:

```text
revision 2 có
revision 1 không có
→ không có before snapshot
→ không diff được
```

---

## 19.4 Không được chữa bằng cách giả dữ liệu trước

Tuyệt đối không:

```text
previous missing
→ coi tất cả before = null / ""
→ hiển thị "(trống) → giá trị mới"
```

Vì như vậy UI sẽ bịa rằng dữ liệu cũ là trống.

Cũng không:

```text
lấy current DB state làm before
```

sau khi edit đã commit, vì lúc đó current state đã là after.

History phải phân biệt:

```text
KNOWN EMPTY
UNKNOWN / NOT RECORDED
```

---

## 19.5 Fix bắt buộc cho mọi write path trong tương lai

Trước khi mutation làm tăng revision của một campus, hệ thống phải đảm bảo tồn tại snapshot cho **revision hiện tại**.

Pseudo-flow:

```text
currentFormRevision = detail.FormRevision
↓
check visit_instance_form_revision_history
  where visitInstanceId = current instance
  and formRevision = currentFormRevision
↓
nếu đã có
  → tiếp tục
↓
nếu chưa có
  → chụp PRE-EDIT snapshot từ state đang còn chưa bị sửa
  → insert baseline recovery row
↓
apply edit
↓
revision tăng
↓
write normal after revision history
```

Quan trọng:

```text
baseline phải được chụp TRƯỚC ApplyFormDetail / schedule mutation / member replacement.
```

Đây là thời điểm cuối cùng còn biết chắc dữ liệu “before”.

---

## 19.6 Tạo helper dùng chung, không copy logic

Đề xuất helper nội bộ:

```text
EnsureCurrentInstanceRevisionSnapshotAsync(...)
```

Input tối thiểu:

```text
VisitRequest
VisitRequestCampus instance
VisitInstanceFormDetail detail
current linked members
actor/time
CancellationToken
```

Behavior:

```text
if history row(current FormRevision) exists
    no-op
else
    insert recovery baseline using VisitFormRevisionSnapshotBuilder
```

Phải reuse:

```text
VisitFormRevisionSnapshotBuilder.Instance(...)
```

không serialize anonymous object mới.

---

## 19.7 Source type của recovery baseline

Không invent enum mới nếu chưa cần.

Ưu tiên audit canonical SQL enum trước.

Nếu `MIGRATION` đã được phép cho revision source:

```text
SourceType = MIGRATION
```

có thể dùng cho recovery baseline.

Nhưng recovery row là **technical baseline**, không phải hành động người dùng.

Do đó timeline normal phải cân nhắc không render nó như một user-facing business event.

Mục đích của row này là:

```text
revision N baseline
→ làm before cho revision N+1
```

không phải tạo thêm một dòng lịch sử “người dùng đã sửa”.

Nếu architecture hiện tại có cách đánh dấu baseline mà không cần schema mới thì dùng cách đó.

Chỉ thêm enum/schema khi thật sự không có source phù hợp.

---

## 19.8 Member snapshot phải lấy đúng thành viên BEFORE edit

Khi tạo recovery baseline:

```text
Members
```

phải là các member đang link với `visitInstanceId` **trước edit**.

Không được dùng:

```text
newMembers
```

vì đó là after-state.

Query/link hiện tại cần lấy đúng:

```text
visit_instance_guest_members
→ guest_member
→ ordered by display order
```

và đưa vào canonical builder.

---

## 19.9 Schedule cũng phải nằm trong before snapshot

Builder hiện tại đã chứa:

```text
plannedStartAt
plannedEndAt
```

Recovery baseline phải gọi builder khi instance còn giữ lịch cũ.

Nếu capture sau câu:

```text
instance.PlannedStartAt = content.PlannedStartAt
```

thì diff schedule tiếp tục mất.

---

## 19.10 Áp dụng guard cho các write path nào

Audit và áp dụng canonical baseline guard trước mutation tại:

```text
PENDING_EDIT
PER-CAMPUS PENDING EDIT
RESUBMIT
SAFE_EDIT
AMENDMENT_APPLIED
```

Create bình thường phải tạo revision 1 như hiện tại.

Nguyên tắc:

```text
mọi path có thể ghi revision N+1
→ phải đảm bảo revision N snapshot đã tồn tại trước khi overwrite state
```

Không chỉ fix riêng `ApplyPendingEditAsync`, nếu không lỗi sẽ xuất hiện lại ở safe edit/amendment/resubmit.

---

## 19.11 Request-level revision cũng phải audit tương tự

Tương tự với:

```text
visit_request_revision_history
```

Nếu sửa registrant snapshot:

```text
RequestRevision 1 → 2
```

thì phải có request revision 1 để diff.

Trước request-level mutation:

```text
ensure current request revision baseline exists
```

nếu DB/legacy data thiếu.

Không để campus fix xong nhưng registrant history vẫn có:

```text
— → 2
```

---

## 19.12 Sửa canonical seed / backfill

Vì lỗi trong ảnh xảy ra trên dữ liệu đang chạy, chỉ guard write path là chưa đủ.

Phải audit canonical SQL:

```text
visit_instance_form_details
visit_instance_form_revision_history
visit_request_revision_history
```

Invariant cần đạt cho seed mới:

### Mỗi campus

Nếu:

```text
form_revision = N
```

thì tối thiểu phải có snapshot tương ứng cho revision hiện tại hoặc chain cần thiết để history demo/test hoạt động.

Với seed mới tạo chưa từng edit:

```text
revision history 1
source = CREATE hoặc MIGRATION đúng semantics
snapshot = chính xác state revision 1
```

### Request level

Seed request cũng phải có:

```text
request revision baseline = 1
```

nếu request history feature dựa trên chain đó.

Không seed một timeline “edit revision 2” nếu không có dữ liệu revision 1 để diff.

---

## 19.13 Dữ liệu đã lỗi sau khi edit — không được tự bịa before

Đối với record như ảnh, nếu revision 2 đã được ghi nhưng revision 1 chưa từng tồn tại:

```text
sau commit không thể chắc chắn reconstruct revision 1 từ revision 2.
```

Có 3 trường hợp:

### A. Seed/demo có nguồn ban đầu xác định

Nếu canonical seed/script chứa chính xác dữ liệu trước edit:

```text
có thể repair baseline từ source seed đáng tin cậy
```

### B. Audit/change log có đủ giá trị cũ

Nếu thực tế audit row có field-level old values đầy đủ:

```text
có thể reconstruct có kiểm chứng
```

nhưng code hiện tại không được giả định điều này; phải verify trước.

### C. Không có bằng chứng

Không backfill fabricated data.

UI phải nói rõ:

```text
Không có snapshot của phiên bản trước nên không thể hiển thị chính xác các trường đã thay đổi.
```

thay vì:

```text
Sự kiện này không có thay đổi chi tiết nào được ghi nhận.
```

Hai câu có ý nghĩa khác nhau:

```text
NO_CHANGES
!=
COMPARISON_UNAVAILABLE
```

---

## 19.14 API/UI phải phân biệt “không đổi” và “không có before”

Hiện UI đang gom trường hợp:

```text
fields.length == 0
collections.length == 0
```

thành một thông báo chung.

Cần phân biệt ít nhất:

```text
HAS_DIFF
NO_RECORDED_DIFF
PREVIOUS_REVISION_MISSING
```

Minimal approach không cần DB migration:

Backend detail có thể trả thêm metadata:

```text
comparisonStatus
```

ví dụ:

```text
AVAILABLE
PREVIOUS_REVISION_MISSING
```

Hoặc nếu muốn ít contract change nhất, frontend có thể dựa trên:

```text
currentRevision > 1
&& previousRevision == null
```

để hiển thị `PREVIOUS_REVISION_MISSING`.

Khuyến nghị backend explicit status để frontend không phải suy diễn business semantics.

---

## 19.15 UI đích

### Có before + có diff

Hiển thị:

```text
Phiên bản: 1 → 2

Mục đích
Trước: ...
Sau: ...

Thời gian
Trước: ...
Sau: ...
```

### Có before nhưng normalized diff rỗng

Chỉ khi thật sự có trường hợp hợp lệ:

```text
Không có thay đổi nội dung hiển thị nào giữa hai snapshot.
```

### Không có previous revision

Hiển thị warning trung thực:

```text
Không tìm thấy dữ liệu của phiên bản trước nên hệ thống chưa thể hiển thị chính xác chi tiết thay đổi.
```

Không dùng câu:

```text
Sự kiện này không có thay đổi chi tiết nào được ghi nhận.
```

vì user thực tế đã sửa.

---

## 19.16 Test bắt buộc cho V15

### Test 1 — normal chain

Seed:

```text
revision 1 snapshot = purpose A
```

Edit:

```text
purpose A → purpose B
revision = 2
```

Expected detail:

```text
1 → 2
Purpose:
A → B
```

### Test 2 — legacy missing baseline BEFORE edit

DB trước edit:

```text
detail.FormRevision = 1
history revision 1 = missing
purpose = A
```

Thực hiện pending edit:

```text
A → B
```

Expected:

```text
write recovery baseline revision 1 with A
write normal revision 2 with B
history detail revision 2:
1 → 2
A → B
```

### Test 3 — member diff với missing baseline

Before:

```text
Guest A
```

History baseline missing.

Edit:

```text
Guest A → Guest B
```

Expected:

```text
recovery baseline stores Guest A
revision 2 stores Guest B
drawer reports member update/remove/add đúng canonical matcher
```

### Test 4 — schedule diff

Before:

```text
09:00 → 11:00
```

Edit:

```text
13:00 → 15:00
```

Expected drawer hiển thị old/new time.

### Test 5 — already-corrupted historical row

DB:

```text
only revision 2 exists
no reliable revision 1
```

Read detail:

```text
previousRevision = null
comparisonStatus = PREVIOUS_REVISION_MISSING
```

UI:

```text
hiển thị cảnh báo thiếu baseline
không hiển thị "(trống) → ..."
không khẳng định "không có thay đổi"
```

### Test 6 — request-level registrant revision

Missing request revision 1 before edit:

```text
ensure baseline
→ edit registrant snapshot
→ revision 2
→ drawer shows 1 → 2 + exact field diff
```

### Test 7 — no duplicate baseline

Nếu revision hiện tại đã có history:

```text
ensure helper
→ no-op
```

Không insert duplicate revision row.

### Test 8 — concurrency

Hai mutation cạnh tranh không được cùng insert recovery baseline gây duplicate/unique failure.

Baseline ensure phải chạy trong cùng transaction/lock strategy với edit.

---

## 19.17 File trọng tâm cho V15

Backend:

```text
backend/PEMS.Infrastructure/Services/VisitRequestV2EditService.cs
backend/PEMS.Infrastructure/Services/VisitRequestV2EditOps.cs
backend/PEMS.Infrastructure/Services/VisitFormRevisionSnapshotBuilder.cs
backend/PEMS.Infrastructure/Services/VisitSafeEditService.cs
backend/PEMS.Infrastructure/Services/VisitAmendmentService.cs
backend/PEMS.Application/Delegations/Commands/VisitAmendments/GetVisitHistoryDetailQueryHandler.cs
backend/PEMS.Application/Delegations/Commands/VisitAmendments/GetVisitRequestHistoryQueryHandler.cs
```

Kiểm tra thêm create/backfill/seed:

```text
backend/PEMS.Infrastructure/Services/VisitRequestV2CreateService.cs
canonical SQL seed / migration scripts
```

Frontend:

```text
frontend/.../VisitHistoryTimeline.tsx
frontend/... history detail drawer/component
frontend/.../visitRequestV2Api.ts
frontend/.../visitRequestV2.json
```

Tests:

```text
tests/PEMS.IntegrationTests/VisitRequests/VisitHistoryDetailDiffV2Tests.cs
tests/PEMS.IntegrationTests/VisitRequests/UpdatePendingVisitRequestV2ServiceTests.cs
tests/PEMS.IntegrationTests/VisitRequests/UpdatePendingVisitInstanceV2ServiceTests.cs
```

---

## 19.18 Definition of Done riêng cho V15

- [ ] Revision 2 mới không bao giờ được ghi mà thiếu usable revision 1 baseline, nếu state trước edit còn có thể chụp.
- [ ] Pending edit hiển thị đúng field before → after.
- [ ] Per-campus edit hiển thị đúng field before → after.
- [ ] Schedule change xuất hiện trong drawer.
- [ ] Member changes xuất hiện trong drawer.
- [ ] Request-level registrant changes xuất hiện trong drawer.
- [ ] Legacy/seed missing baseline được recovery trước mutation.
- [ ] Existing corrupted history không bị fabricate.
- [ ] UI phân biệt `PREVIOUS_REVISION_MISSING` với `NO_RECORDED_DIFF`.
- [ ] Recovery baseline không xuất hiện như một user action giả trong timeline.
- [ ] Không duplicate revision rows khi concurrent edit.
- [ ] Không cần database schema mới nếu source type/metadata hiện có đủ dùng.

---


# PHASE 13 — ĐỒNG BỘ QUYỀN TẠO TIN TỨC THEO VISIT INSTANCE

# 20. V16 — Process hiện “Tạo bài tin tức” nhưng Create News lại báo không đủ điều kiện

## 20.1 Triệu chứng thực tế

Tại:

```text
/dashboard/visit/process/3006
```

người dùng `IC Staff Hà Nội` thấy:

```text
TIN TỨC ĐOÀN KHÁCH
[ + Tạo bài tin tức ]
```

Nhưng sau khi bấm nút, trang:

```text
/dashboard/news/create?visitInstanceId=3006...
```

lại báo:

```text
Chuyến tiếp khách này chưa đủ điều kiện để viết tin tức
(chưa vào giai đoạn Sau tiếp khách, không yêu cầu tin tức,
hoặc bạn không phải Host/người tham gia).
```

Hai màn hình đang đưa ra **hai verdict trái nhau cho cùng user + cùng visitInstanceId**.

Đây là lỗi contract/authorization drift, không nên chữa bằng cách chỉ sửa message.

---

## 20.2 Root cause code hiện tại

Hiện có nhiều nơi tự tính eligibility tạo tin, nhưng rule không giống nhau.

### A. Nút trong Visit Process

`VisitNewsPostList` chỉ hiện nút khi API list trả:

```text
canCreate = true
```

API list dùng:

```text
GetVisitInstanceNewsQueryHandler
→ VisitNewsAccess.Evaluate(...)
```

`VisitNewsAccess` quy định:

```text
writer =
    current Host
    OR accepted IC_SUPPORT participant
    OR accepted STUDENT participant

writing window =
    AFTER_VISIT
    OR CLOSED
```

Ngoài ra `GetVisitInstanceNewsQueryHandler` đang coi participant:

```text
ACCEPTED
OR ASSIGNED
```

là relation có thể được đưa vào evaluator.

### B. Trang Create News

`CreateNews.tsx` lại gọi:

```text
GET /news/eligible-visit-instances
```

Endpoint này dùng một logic độc lập:

```text
GetEligibleVisitInstancesForNewsQueryHandler
```

Nó tự query lại:

```text
participant.Status == ACCEPTED
```

không đồng nhất với list handler đang chấp nhận:

```text
ACCEPTED || ASSIGNED
```

và tự lọc lại:

```text
AFTER_VISIT || CLOSED
!NewsNotRequired
MediaConsentStatus == AGREED
```

### C. Khi submit `/news`

`CreateNewsCommandHandler` lại có một bản rule thứ ba:

```text
role = regular Staff / Staff Leader / Student
```

sau đó với visit instance:

```text
isHost
OR any participant with Status == ACCEPTED
```

Điểm này cũng không giống `VisitNewsAccess`, vì command hiện không giới hạn participant role ở:

```text
IC_SUPPORT
STUDENT
```

mà một `DEPT_SUPPORT` có role account `STAFF` cũng có thể lọt qua nếu accepted.

### D. Visit process permission

`GetVisitProcessPermissionsQueryHandler` còn tự tính thêm:

```text
CanCreateNews = newsCreator
```

nhưng chỉ dựa trên:

```text
isHost / accepted IC support / Student
AND isLive
```

chưa gắn với writing window `AFTER_VISIT/CLOSED`.

Kết luận:

```text
cùng một business action "CREATE VISIT NEWS"
đang có nhiều source of truth.
```

Chính drift này có thể tạo đúng triệu chứng:

```text
màn A nói được tạo
màn B nói không đủ điều kiện.
```

---

## 20.3 Việc cần kiểm tra riêng cho visitInstanceId = 3006

Trước khi code, agent phải lấy actual data / API response cho `3006`:

```text
visit_request_campuses.status
current_host_user_id
news_not_required
form_detail.media_consent_status
```

và relation của current user:

```text
current user id
visit_participants.status
visit_participants.participant_role
isHost
```

So sánh response:

```text
GET /.../visit-instances/3006/news
GET /news/eligible-visit-instances?includeAlreadyHasNews=true
```

Phải chỉ ra predicate nào làm `3006` biến mất khỏi eligible list.

Không được dừng ở câu:

```text
"có thể do chưa đủ điều kiện"
```

vì UI trước đó đã nhận một verdict `canCreate=true`.

---

## 20.4 Logic đích — một source of truth

Tạo/reuse một canonical policy/service, ví dụ:

```text
VisitNewsEligibility
```

Không nhất thiết phải là service DI phức tạp; có thể mở rộng `VisitNewsAccess` hiện tại nếu phù hợp architecture.

Canonical evaluator nhận:

```text
VisitRequestCampus instance
VisitRequest request
current user
participant relation/role
existing own news
```

và trả verdict có cấu trúc:

```text
InScope
CanCreate
ReasonCode
Reason
IsHost
IsEligibleParticipant
WritingWindowOpen
MediaConsentAllowed
NewsRequired
AlreadyHasOwnNews
```

Tất cả read/write path dùng chung verdict này.

---

## 20.5 Canonical rule đề xuất

### Người được viết tin gắn với chuyến

Cho phép:

```text
Current Host
OR participant đã xác nhận và role = IC_SUPPORT
OR participant đã xác nhận và role = STUDENT
```

Không cho:

```text
DEPT_SUPPORT
Visitor
HO
Staff Leader chỉ vì là Staff Leader
```

Ngoại lệ:

```text
Staff Leader đồng thời là Current Host
→ được viết với relation HOST
```

### Trạng thái chuyến

Chỉ viết từ:

```text
AFTER_VISIT
CLOSED
```

Không viết trong:

```text
WAITING_*
ASSIGNED
BEFORE_VISIT
DURING_VISIT
CANCELLED
REJECTED
```

### Media/news gate

Phải đồng thời:

```text
NewsNotRequired == false
MediaConsentStatus == AGREED
```

### Một bài / tác giả / chuyến

Giữ rule hiện tại:

```text
mỗi author tối đa một bài cho visitInstance
```

Nếu đã có bài:

```text
PENDING_REVIEW / REJECTED
→ dẫn sang sửa bài hiện tại nếu được phép

PUBLISHED / HIDDEN
→ không tạo bài thứ hai
```

---

## 20.6 Participant status phải thống nhất

Hiện đang có drift:

```text
list handler: ACCEPTED || ASSIGNED
eligible query: ACCEPTED
create command: ACCEPTED
```

Agent phải xác định ý nghĩa thật của `ASSIGNED`.

Nếu `ASSIGNED` là trạng thái hợp lệ tương đương đã tham gia/được gán trong workflow hiện tại, thì **cả ba path** phải dùng cùng helper.

Nếu chỉ `ACCEPTED` mới có quyền viết, thì list handler không được dùng `ASSIGNED` để bật `canCreate`.

Không được để mỗi endpoint chọn một tập status khác nhau.

---

## 20.7 `GetEligibleVisitInstancesForNews` không tự viết lại policy

Handler chỉ nên:

1. load candidate instances mà user có relation;
2. gọi canonical eligibility evaluator;
3. trả những item có `CanCreate=true`;
4. nếu `includeAlreadyHasNews=true`, vẫn có thể trả item với:
   ```text
   HasNews=true
   CanSelect=false
   ```
   để UI giải thích đúng.

Không duplicate business predicates trong LINQ nếu predicates đó cũng tồn tại ở create/list policy.

Có thể batch-load facts để tránh N+1, nhưng kết quả phải đi qua một business verdict chung.

---

## 20.8 Create command phải re-authorize bằng cùng policy

`POST /news` vẫn phải là final authority.

Pseudo-flow:

```text
load instance
load participant relation
load own existing news
↓
eligibility = VisitNewsEligibility.Evaluate(...)
↓
if !eligibility.CanCreate
    throw stable business error từ ReasonCode
↓
create
```

Không tin verdict frontend.

Không dùng một rule riêng trong command.

---

## 20.9 Process button phải đồng bộ

`VisitNewsPostList` tiếp tục render từ backend:

```text
list.canCreate
```

nhưng `list.canCreate` phải đến từ cùng canonical eligibility verdict.

`GetVisitProcessPermissions.CanCreateNews` cũng phải dùng cùng verdict hoặc bỏ flag nếu không còn consumer cần.

Không để:

```text
CanCreateNews=true
```

ở `BEFORE_VISIT/DURING_VISIT` nếu actual create command chỉ cho `AFTER_VISIT/CLOSED`.

---

## 20.10 Create News preset phải trả lý do chính xác

Hiện `CreateNews.tsx` khi không tìm thấy preset dùng một message gom nhiều nguyên nhân:

```text
chưa vào Sau tiếp khách
hoặc không yêu cầu tin
hoặc không phải Host/người tham gia
```

Sau fix, endpoint nên có một lookup/verdict cho preset hoặc trả reason code.

Ví dụ stable codes:

```text
NEWS_VISIT_NOT_IN_WRITING_WINDOW
NEWS_VISIT_NOT_IN_SCOPE
NEWS_VISIT_PARTICIPANT_ROLE_NOT_ALLOWED
NEWS_VISIT_NOT_REQUIRED
NEWS_VISIT_MEDIA_CONSENT_DENIED
NEWS_ALREADY_EXISTS_FOR_VISIT_INSTANCE
```

UI map i18n:

```text
Chuyến thăm chưa đến giai đoạn có thể viết tin.
Bạn không phải người phụ trách hoặc thành phần được phép viết tin cho chuyến này.
Chuyến này đã được xác nhận không cần bài tin tức.
Khách không đồng ý truyền thông.
Bạn đã có bài viết cho chuyến này.
```

Không dùng một sentence đoán 4 nguyên nhân.

---

## 20.11 Không tạo flow “visit news” thứ hai

Hiện `CreateNews.tsx` submit vào:

```text
POST /news
```

và đây là form News Management dùng chung.

Giữ nguyên hướng này.

Không tạo thêm form/UI khác chỉ để process page hoạt động.

Nếu `CreateVisitInstanceNewsCommandHandler` là workflow cũ/khác contract, agent phải audit usage và chọn **một write path canonical**, không để hai create handlers cùng sống với hai rule khác nhau.

---

## 20.12 Test bắt buộc cho V16

### Host

```text
AFTER_VISIT + Host + media agreed + news required
→ process canCreate=true
→ eligible preset có item
→ POST /news thành công
```

### Staff Leader self-host

```text
Staff Leader + CurrentHostUserId == user
→ được viết như HOST
```

### IC support

```text
accepted IC_SUPPORT
→ được viết
```

### Student

```text
accepted STUDENT
→ được viết
```

### Dept support

```text
accepted DEPT_SUPPORT
→ process không hiện Create
→ eligible không trả
→ direct POST bị reject
```

### Too early

```text
DURING_VISIT
→ process không hiện Create
→ eligible không trả
→ direct POST reject cùng reason code
```

### News waived

```text
NewsNotRequired=true
→ cả ba path cùng reject
```

### Media denied

```text
MediaConsentStatus != AGREED
→ cả ba path cùng reject
```

### Existing own news

```text
→ không tạo duplicate
→ preset dẫn sang existing edit/detail phù hợp
```

### Participant status

Test chính xác status set được business chấp nhận:

```text
ACCEPTED
ASSIGNED (nếu còn semantic hợp lệ)
```

và bảo đảm list/eligible/create cho cùng kết quả.

---

## 20.13 Definition of Done riêng cho V16

- [ ] Không còn trường hợp Process hiện `Tạo bài tin tức` nhưng preset Create News báo không eligible cho cùng user/instance.
- [ ] List, eligible-query, process-permission và create-command dùng cùng canonical eligibility.
- [ ] Participant role/status thống nhất.
- [ ] `DEPT_SUPPORT` không lọt qua create nếu policy không cho.
- [ ] Staff Leader chỉ viết khi thực sự là Host.
- [ ] Writing window thống nhất `AFTER_VISIT/CLOSED`.
- [ ] `NewsNotRequired` và media consent cho cùng verdict ở mọi path.
- [ ] Preset error có stable reason code, không message gom nhiều nguyên nhân.
- [ ] Không tạo duplicate news workflow/form.
- [ ] Có integration test tái hiện visit 3006-like scenario.

---

# PHASE 14 — GATE T-6 GIỜ CHO BEFORE_VISIT → DURING_VISIT

# 21. V17 — Chỉ được chuyển sang “Trong tiếp khách” từ 6 giờ trước giờ bắt đầu

## 21.1 Yêu cầu nghiệp vụ

Khi campus đang:

```text
BEFORE_VISIT
```

Host không được bấm hoàn tất chuẩn bị để chuyển sang:

```text
DURING_VISIT
```

quá sớm.

Thời điểm sớm nhất:

```text
plannedStartAt - 6 giờ
```

Ví dụ chuyến bắt đầu:

```text
29/08/2026 09:00
```

thì:

```text
trước 29/08/2026 03:00
→ KHÔNG được chuyển

từ 29/08/2026 03:00 trở đi
→ được phép, nếu các blocker chuẩn bị khác đã hoàn tất
```

Không yêu cầu phải đúng giờ bắt đầu mới chuyển.

Nếu chuyến đã quá giờ mà vẫn còn `BEFORE_VISIT`, vẫn cho Host chuyển sang `DURING_VISIT` nếu các điều kiện khác đạt, tránh làm workflow bị kẹt.

---

## 21.2 Root cause hiện tại

`CompleteVisitStageCommandHandler` ở nhánh:

```text
Stage = Before
```

hiện kiểm tra:

- instance phải `BEFORE_VISIT`;
- không còn participant invitation pending;
- phải có agenda;
- handover logistics đủ chữ ký;
- agenda tồn tại.

Nhưng **không có bất kỳ check nào với `PlannedStartAt`**.

Vì vậy nếu Host hoàn tất các item chuẩn bị sớm nhiều ngày:

```text
BEFORE_VISIT
→ Complete Before
→ DURING_VISIT
```

vẫn thành công.

---

## 21.3 Permission hiện tại cũng mở quá sớm

`GetVisitProcessPermissionsQueryHandler` đang trả:

```text
CanStartVisit =
    isHost
    && isLive
    && instance.Status == BEFORE_VISIT
```

Không xét thời gian.

Do đó frontend có thể render action như đang sẵn sàng dù còn hơn 6 giờ.

---

## 21.4 Next task cũng phải cập nhật

`VisitNextTaskBuilder` hiện:

```text
BEFORE_VISIT
+ preparation complete
→ "Xác nhận hoàn thành chuẩn bị"
```

không xét `plannedStartAt - 6h`.

Nếu chỉ thêm guard vào command:

```text
list nói cần bấm ngay
→ process cho bấm
→ backend trả 409
```

thì UX vẫn drift.

Phải cập nhật cả:

```text
command
permission
next-task
frontend
```

theo cùng policy.

---

## 21.5 Tạo policy dùng chung

Đề xuất constant/policy:

```csharp
public const int StartVisitEarlyWindowHours = 6;
```

và helper:

```text
availableAt = plannedStartAt.AddHours(-6)

CanAdvanceBeforeToDuring(now, plannedStartAt)
    = now >= availableAt
```

Nên đặt ở một domain/application policy hiện hữu liên quan lifecycle thay vì magic number `6` ở nhiều file.

Ví dụ:

```text
VisitStageTransitionPolicy
```

hoặc mở rộng policy hiện tại nếu đã có nơi phù hợp.

---

## 21.6 Backend command — authoritative guard

Trong `CompleteVisitStageCommandHandler`, nhánh `Before`, sau status authorization và trước khi write:

```text
availableAt = instance.PlannedStartAt.AddHours(-6)

if VietnamNow < availableAt
    throw 409
```

Stable code đề xuất:

```text
VISIT_START_WINDOW_NOT_OPEN
```

Message:

```text
Chưa thể chuyển sang giai đoạn Trong tiếp khách.
Bạn chỉ có thể bắt đầu từ 6 giờ trước thời gian dự kiến của chuyến thăm.
```

Response nên có metadata nếu error contract hỗ trợ:

```text
availableAt
plannedStartAt
requiredEarlyWindowHours = 6
```

Backend là final authority, kể cả client gọi API trực tiếp.

---

## 21.7 Dùng Vietnam time đúng với dữ liệu lịch

`planned_start_at` đang được xử lý theo Vietnam wall-clock ở các workflow khác.

Guard phải dùng:

```text
IDateTimeService.VietnamNow
```

không dùng:

```text
DateTime.UtcNow
```

tránh lệch +7 giờ.

---

## 21.8 Permission DTO

Không chỉ trả boolean.

Bổ sung tối thiểu:

```text
CanStartVisit
StartVisitAvailableAt
```

Khuyến nghị thêm:

```text
StartVisitDisabledReasonCode
```

nếu DTO hiện tại cần hỗ trợ disabled action rõ lý do.

Rule:

```text
CanStartVisit =
    isHost
    && isLive
    && instance.Status == BEFORE_VISIT
    && now >= plannedStartAt - 6h
```

`StartVisitAvailableAt` vẫn trả khi status `BEFORE_VISIT` để frontend hiển thị thời điểm mở.

`GetVisitProcessPermissionsQueryHandler` hiện chưa inject clock; cần dùng `IDateTimeService` thay vì tự gọi system time.

---

## 21.9 Frontend VisitProcess

Không ẩn hoàn toàn nút khi còn quá sớm.

UX đề xuất:

```text
[ Xác nhận hoàn thành chuẩn bị ] (disabled)
```

và text:

```text
Có thể chuyển sang Trong tiếp khách từ 03:00 29/08/2026
(6 giờ trước thời gian bắt đầu).
```

Khi đến cửa sổ:

```text
CanStartVisit=true
→ button enabled
```

Nếu user mở trang trước T-6 và để tab đó mở tới T-6:

- có thể dùng timer nhẹ để cập nhật trạng thái client;
- nhưng khi click vẫn phải dựa backend;
- hoặc refetch permission khi countdown đạt availableAt.

Không cần polling liên tục theo giây.

---

## 21.10 Không khóa việc chuẩn bị trước T-6

Rule mới chỉ khóa **transition**:

```text
BEFORE_VISIT → DURING_VISIT
```

Không khóa các công việc chuẩn bị:

- agenda;
- participant invitation;
- logistics;
- setup;
- gửi cập nhật chuẩn bị;
- report chuẩn bị.

Host vẫn có thể hoàn tất tất cả công việc trước nhiều ngày.

Sau khi hoàn tất, UI có thể hiện:

```text
Đã hoàn tất công tác chuẩn bị.
Chờ đến 03:00 29/08/2026 để chuyển sang Trong tiếp khách.
```

---

## 21.11 Không thay đổi ASSIGNED → BEFORE_VISIT

Rule T-6 không áp cho:

```text
ASSIGNED → BEFORE_VISIT
```

Host vẫn được:

```text
Bắt đầu chuẩn bị
```

sớm theo workflow hiện tại.

Chỉ:

```text
BEFORE_VISIT → DURING_VISIT
```

mới bị earliest-start gate.

---

## 21.12 Không tự động chuyển stage

Đến T-6:

```text
không auto-update status sang DURING_VISIT
```

Host vẫn phải chủ động xác nhận hoàn thành chuẩn bị.

Lý do:

- còn các blocker nghiệp vụ;
- stage change có audit;
- có thể có logistics chưa hoàn thành;
- status phải phản ánh hành động thực tế của Host.

---

## 21.13 Next task behavior

Khi preparation chưa hoàn tất:

```text
Hoàn thiện lịch trình và công tác chuẩn bị
```

giữ nguyên.

Khi preparation đã hoàn tất nhưng còn trước T-6:

```text
Chờ đến thời điểm có thể bắt đầu tiếp khách
```

với:

```text
DueAt = plannedStartAt - 6h
RequiresAction = false
```

hoặc một code rõ ràng:

```text
WAIT_START_VISIT_WINDOW
```

nếu vocabulary hiện tại chấp nhận mở rộng.

Khi đã tới T-6:

```text
Xác nhận hoàn thành chuẩn bị
RequiresAction = true
```

Không để list gắn star/action-required quá sớm.

---

## 21.14 Schedule bị sửa khi đang BEFORE_VISIT

Nếu Visitor/flow hợp lệ làm `plannedStartAt` thay đổi trước stage start:

```text
availableAt
```

phải được tính lại theo schedule mới.

Không persist một timestamp cutoff riêng nếu có thể derive từ:

```text
current plannedStartAt - 6h
```

để tránh stale cutoff.

Ví dụ:

```text
ban đầu 09:00 → availableAt 03:00
sửa sang 14:00 → availableAt 08:00
```

Permission và command phải cùng đọc lịch mới nhất.

---

## 21.15 Concurrency

Scenario:

```text
Host mở page khi startAt=09:00
Visitor/amendment hợp lệ đổi startAt=14:00
Host giữ page cũ, 03:30 bấm start
```

Backend phải đọc `instance.PlannedStartAt` mới nhất trong transaction/request và reject vì window mới chưa mở.

Frontend reload permission sau 409.

Nếu stage command sau này dùng expected rowVersion, càng tốt; nhưng time gate không được phụ thuộc frontend snapshot.

---

## 21.16 Test bắt buộc cho V17

### Boundary

Với:

```text
plannedStartAt = 10:00
availableAt = 04:00
```

Test:

```text
03:59:59 → reject
04:00:00 → allow nếu không có blocker
04:00:01 → allow
```

### Far early

```text
T-24h
→ CanStartVisit=false
→ direct command 409
```

### Exact T-6

```text
→ permission true
→ command success
```

### After planned start

```text
T+1h nhưng status vẫn BEFORE_VISIT
→ cho chuyển nếu các blocker khác đạt
```

### Preparation incomplete at T-6

```text
window mở
nhưng agenda/pending invite/handover blocker còn
→ vẫn reject vì blocker
```

### Schedule edited

```text
startAt thay đổi
→ availableAt thay đổi
→ permission/command dùng lịch mới
```

### Role

```text
non-host
→ vẫn forbidden dù đang trong T-6
```

### Next task

```text
prep complete + before T-6
→ waiting task
prep complete + after T-6
→ confirm-preparation task
```

---

## 21.17 File trọng tâm cho V17

Backend:

```text
backend/PEMS.Application/Delegations/Commands/CompleteVisitStage/CompleteVisitStageCommandHandler.cs
backend/PEMS.Application/Delegations/Queries/GetVisitProcessPermissions/VisitProcessPermissionDto.cs
backend/PEMS.Application/Delegations/Queries/GetVisitProcessPermissions/GetVisitProcessPermissionsQueryHandler.cs
backend/PEMS.Application/Delegations/Services/VisitNextTaskBuilder.cs

SaveOperationalContactCommandHandler.cs
InitiateOperationalContactTransferCommandHandler.cs
ResendOperationalContactConfirmationCommandHandler.cs
ManageOperationalContactHandlers.cs
OperationalContactInvitationService.cs
IOperationalContactInvitationService.cs
EmailActionConstants.cs
```

Policy/constants:

```text
PEMS.Domain / PEMS.Application policy phù hợp với lifecycle
```

Frontend:

```text
frontend/pems-react/src/pages/dashboard/visit/VisitProcess.tsx
frontend/pems-react/src/features/delegations/types/delegations.types.ts
frontend i18n/errors mapping nếu có
```

Tests:

```text
CompleteVisitStage tests
GetVisitProcessPermissions tests
VisitNextTaskBuilder tests
VisitProcess frontend tests
```

---

## 21.18 Definition of Done riêng cho V17

- [ ] BEFORE_VISIT không thể chuyển DURING_VISIT trước `plannedStartAt - 6h`.
- [ ] Đúng T-6 thì có thể chuyển nếu các blocker khác đã hoàn tất.
- [ ] Backend direct API bị chặn trước T-6.
- [ ] Permission không bật `CanStartVisit` sớm.
- [ ] UI hiển thị disabled state + thời điểm được phép.
- [ ] Next task không yêu cầu Host xác nhận quá sớm.
- [ ] Preparation vẫn làm được bình thường trước T-6.
- [ ] Không auto-transition khi tới T-6.
- [ ] Schedule edit làm cutoff tính lại từ lịch mới.
- [ ] Dùng VietnamNow, không UtcNow.
- [ ] Có boundary tests T-6.
- [ ] Không cần DB migration vì cutoff derive từ `plannedStartAt`.

---


# PHASE 15 — ATOMICITY CHO CHUYỂN GIAO ĐẦU MỐI + EMAIL ACTION TOKEN

# 22. V18 — Transfer có thể “thành công một nửa”: DB đã PENDING nhưng UI báo lỗi

## 22.1 Triệu chứng thực tế

Khi người dùng đổi email đầu mối trên một campus đã có đầu mối xác nhận, UI hiển thị:

```text
Đã xảy ra lỗi. Vui lòng thử lại.
```

Trong khi thao tác này về nghiệp vụ là:

```text
current contact A
→ mời contact B nhận chuyển giao
→ A vẫn giữ quyền cho tới khi B xác nhận
```

Lỗi chung này đặc biệt nguy hiểm vì người dùng không biết:

```text
transfer chưa được tạo
hay
transfer đã được tạo nhưng email/token thất bại.
```

---

## 22.2 Luồng code hiện tại

`SaveOperationalContactCommandHandler` phân loại:

```text
email mới == email hiện tại
→ UpdateOperationalContactProfile

email mới != email hiện tại
+ campus undecided
→ ReplaceOperationalContact

email mới != email hiện tại
+ campus decided
→ InitiateOperationalContactTransfer
```

Đây là classification đúng về mặt nghiệp vụ.

Trong `InitiateOperationalContactTransferCommandHandler`:

```text
1. Validate actor
2. Validate transfer window
3. Validate current contact
4. Validate new email/account
5. Ensure không có pending change khác
6. Insert VisitRequestIdentityChange:
   ChangeKind = TRANSFER
   Status = PENDING
7. Insert history + audit
8. COMMIT transaction
9. Sau commit mới gọi SendInvitationAsync(identityChangeId)
```

Điểm nguy hiểm nằm ở bước 9.

---

## 22.3 `SendInvitationAsync()` hiện tại có hai phần khác nhau về độ an toàn

### A. Tạo email action token

Service tạo:

```text
email_action_tokens
action_context = VISIT_CONTACT_TRANSFER
target_type = VISIT_REQUEST_IDENTITY_CHANGE
intended_action = ACCEPT
status = PENDING
```

và gọi:

```text
SaveChangesAsync()
```

### B. Gửi email

Phần dispatcher gửi email được bọc:

```text
try/catch
```

nên SMTP failure chỉ log và không làm rollback business state.

Nhưng `SaveChangesAsync()` khi tạo token lại xảy ra **trước try/catch gửi mail**.

Do business transfer đã commit trước đó, nếu insert token lỗi thì có thể xảy ra:

```text
TRANSFER row = PENDING   ✅
history/audit = recorded ✅
token = missing          ❌
API = 500                ❌
UI = "Đã xảy ra lỗi"     ❌
```

Đây là partial-commit bug.

---

## 22.4 Khả năng lỗi DB local — thiếu `VISIT_CONTACT_TRANSFER` trong ENUM

Repo có migration riêng:

```text
07_up_transfer_tokens.sql
```

để thêm:

```text
VISIT_CONTACT_TRANSFER
```

vào:

```text
email_action_tokens.action_context
```

Nếu local DB được import từ schema/SQL cũ mà chưa có enum này:

```text
INSERT email_action_tokens(action_context='VISIT_CONTACT_TRANSFER')
→ MySQL error
```

Điều này khớp với triệu chứng:

```text
backend không trả business message cụ thể
→ frontend rơi vào generic error toast.
```

### Preflight DB bắt buộc

Agent phải chạy:

```sql
SHOW COLUMNS
FROM email_action_tokens
LIKE 'action_context';
```

Expected phải chứa:

```text
VISIT_CONTACT_CLAIM
VISIT_CONTACT_TRANSFER
```

Và kiểm tra:

```sql
SHOW COLUMNS
FROM email_action_tokens
LIKE 'target_type';
```

Expected phải chứa:

```text
VISIT_REQUEST_IDENTITY_CHANGE
```

Không được sửa code trước khi xác minh schema runtime.

---

## 22.5 Kiểm tra xem transfer đã bị tạo dở chưa

Với request/campus bị lỗi, query:

```sql
SELECT
    identity_change_id,
    visit_request_id,
    visit_instance_id,
    change_kind,
    status,
    new_email_masked,
    token_version,
    requested_at,
    expires_at
FROM visit_request_identity_changes
WHERE visit_request_id = ?
ORDER BY identity_change_id DESC;
```

Nếu thấy:

```text
change_kind = TRANSFER
status = PENDING
```

nhưng không có matching token:

```sql
SELECT
    email_action_token_id,
    action_context,
    target_type,
    target_id,
    intended_action,
    result_status,
    expires_at
FROM email_action_tokens
WHERE target_type = 'VISIT_REQUEST_IDENTITY_CHANGE'
  AND target_id = ?;
```

thì đã xác nhận partial-commit.

---

## 22.6 Logic đích — một business transaction nhất quán

Mục tiêu:

```text
Không bao giờ có trạng thái:
TRANSFER=PENDING nhưng không thể xác nhận vì không có action token hợp lệ.
```

Có hai hướng triển khai.

### Hướng ưu tiên — tạo token trong cùng transaction business

```text
BEGIN
  validate transfer
  insert identity change PENDING
  insert history/audit
  mint raw token
  insert email_action_token
COMMIT

sau commit:
  dispatch email best-effort
```

Ưu điểm:

```text
business state + token
→ atomic
```

Nếu token insert lỗi:

```text
rollback toàn bộ transfer
```

UI có thể báo lỗi thật sự mà không để lại pending change dở.

Email vẫn best-effort sau commit.

### Hướng thay thế — outbox/recovery semantics

Chỉ dùng nếu architecture hiện tại bắt buộc token creation sau commit.

Khi đó phải có:

```text
Pending invitation delivery status
retry/recovery path
```

và API không được giả rằng transfer “thất bại hoàn toàn”.

Tuy nhiên đây phức tạp hơn và có thể cần schema/outbox.

Với codebase hiện tại, ưu tiên hướng atomic token trong business transaction nếu service boundaries cho phép.

---

## 22.7 Không để email failure rollback transfer

SMTP/provider failure khác với token creation failure.

Sau khi:

```text
identity change + token
```

đã commit thành công:

```text
send email
```

có thể best-effort.

Nếu mail fail:

```text
transfer vẫn PENDING
token vẫn tồn tại
UI có thể báo:
"Đã tạo lời mời chuyển giao nhưng chưa gửi được email. Bạn có thể gửi lại lời mời."
```

hoặc nếu product muốn success semantics đơn giản:

```text
API vẫn success
resend action dùng để recover.
```

Quan trọng:

```text
không trả generic 500 sau khi business state đã thành công.
```

---

## 22.8 Resend phải là recovery path thật sự

Nếu invitation đã PENDING nhưng email delivery fail:

```text
ResendOperationalContactConfirmation
```

phải có thể:

- invalidate/supersede old pending token version theo policy;
- mint token mới;
- gửi lại email;
- tăng resend count;
- ghi event;
- không tạo duplicate `VisitRequestIdentityChange`.

Resend không được yêu cầu cancel + transfer lại từ đầu.

---

## 22.9 UI error handling

`ContactIdentityActions` hiện dùng generic extractor, nhưng fallback cuối vẫn có thể thành:

```text
Đã xảy ra lỗi. Vui lòng thử lại.
```

Cần map stable errors cho operational-contact workflow, ví dụ:

```text
CONTACT_TRANSFER_PENDING_ALREADY_EXISTS
CONTACT_TRANSFER_WINDOW_CLOSED
CONTACT_TARGET_ACCOUNT_INACTIVE
CONTACT_TRANSFER_SAME_AS_CURRENT
CONTACT_INVITATION_TOKEN_CREATE_FAILED
CONTACT_INVITATION_EMAIL_DELIVERY_FAILED
CONTACT_CHANGE_CONFLICT
```

Không nhất thiết tạo đúng các code trên nếu codebase đã có equivalent; ưu tiên reuse canonical codes.

### Sau partial-delivery failure

Nếu business transfer đã commit và token tồn tại nhưng email send fail:

UI nên reload state và hiển thị:

```text
Đã tạo lời mời chuyển giao nhưng email chưa gửi thành công.
Bạn có thể bấm "Gửi lại lời mời".
```

Không để user bấm lại “Đổi người & gửi lời mời xác nhận” và nhận conflict khó hiểu.

---

## 22.10 Pending-state UX

Sau transfer thành công:

```text
Đầu mối hiện tại: A
Lời mời chuyển giao đang chờ: b***@example.com
Hết hạn: ...
```

Actions:

```text
Gửi lại lời mời
Hủy lời mời chuyển giao
```

Không hiển thị form như thể chưa có pending transfer.

Nếu current contact/registrant mở lại page, state query phải phản ánh chính xác pending transfer.

---

## 22.11 Cơ chế cũ giữ nguyên quyền phải được bảo toàn

Trong TRANSFER:

```text
current contact A
```

phải tiếp tục có quyền cho tới khi B accept.

Không được:

```text
clear OperationalContactUserId
```

khi initiate transfer.

Chỉ khi accept hợp lệ:

```text
A → B
```

mới swap holder.

Nếu decline/cancel/expire:

```text
A vẫn giữ quyền
```

---

## 22.12 DB migration strategy

Nếu runtime DB thiếu enum:

### Local/dev schema repair

Apply canonical migration/patch để có:

```text
VISIT_CONTACT_TRANSFER
VISIT_REQUEST_IDENTITY_CHANGE
```

### Canonical SQL

Phải đảm bảo full SQL hiện tại đã chứa enum này.

### Hash-pin/test

Nếu canonical SQL thay đổi:

- update expected SHA;
- chạy canonical schema tests;
- không chỉ patch local DB rồi bỏ quên source-of-truth SQL.

---

## 22.13 Test bắt buộc cho V18

### Token success

```text
initiate transfer
→ identity change PENDING
→ token PENDING exists
→ API success
```

### Token insert failure

Inject/fake token persistence failure:

```text
→ transfer transaction rollback
→ no PENDING identity change
→ no history/audit dangling
→ API stable error
```

### SMTP failure

```text
token persisted
dispatcher throws
→ transfer remains PENDING
→ API semantics predictable
→ resend available
```

### Duplicate click

```text
double submit
→ one PENDING transfer only
→ no duplicate token group
```

### Existing pending transfer

```text
second initiate
→ 409 stable conflict
→ UI shows correct pending state after refresh
```

### Cancel

```text
cancel transfer
→ pending tokens invalid
→ current contact unchanged
```

### Expire

```text
transfer expires
→ current contact unchanged
→ old token unusable
```

### Accept

```text
correct invited identity
→ holder changes A → B exactly once
```

### Decline

```text
→ transfer declined
→ A retains rights
```

### Schema contract

Integration/preflight test:

```text
email_action_tokens.action_context supports VISIT_CONTACT_TRANSFER
```

---

## 22.14 Definition of Done riêng cho V18

- [ ] Không còn partial state `TRANSFER=PENDING` mà không có usable action token do token insert failure.
- [ ] Token persistence lỗi thì transfer rollback hoặc recovery semantics được định nghĩa rõ.
- [ ] SMTP failure không rollback business state.
- [ ] Runtime DB có `VISIT_CONTACT_TRANSFER`.
- [ ] Canonical SQL có enum/token target đúng.
- [ ] UI không còn generic error cho các conflict nghiệp vụ đã biết.
- [ ] Pending transfer reload lại hiển thị đúng trạng thái.
- [ ] Resend hoạt động như recovery, không tạo duplicate change.
- [ ] Cancel/Decline/Expire giữ nguyên current contact.
- [ ] Accept swap holder đúng một lần.
- [ ] Có integration test cho token persistence failure và SMTP failure.
- [ ] Không cần tạo bảng mới nếu transaction boundary hiện tại có thể được chỉnh lại.

---

# 23. Thứ tự implementation khuyến nghị

Triển khai theo thứ tự để tránh sửa chồng nhau:

## Bước 1 — Permission + global gate

```text
V01 STAFF list/detail routing
V04 Staff Leader global contact gate
V05 notification gate
```

Lý do: đây là security/scope correctness.

## Bước 2 — Pending edit contract

```text
V02 registrant snapshot edit
V03 edit while contact pending
```

## Bước 3 — Concurrency

```text
V12 approve/reject expected rowVersion
```

Lý do: tránh duyệt nhầm version trước khi mở rộng edit.

## Bước 4 — Contact invitation lifecycle

```text
V06 cancel state
V07 reinvite
V09 logged-in invitee action
V10 no-login email action
V11 wording
```

## Bước 5 — History

```text
V08 validate eventId + legacy mapping
```

Không viết lại phần identity detail đã có.

### Bổ sung bắt buộc ngay trong bước History

```text
V15 ensure before-snapshot chain + repair seed/baseline + comparison-unavailable UX
```

Phải xử lý V15 trước khi coi history closure hoàn tất, vì event có `eventId` nhưng không có before snapshot vẫn khiến drawer vô nghĩa.

## Bước 6 — Multi-campus UX

```text
V13 multi-expand accordion
```

## Bước 7 — Edge case stale invitation body

```text
V14
```


## Bước 8 — News eligibility consistency

```text
V16 news eligibility / process-create consistency
```

Thực hiện sau các security gate chính nhưng trước khi closure phần Sau tiếp khách, vì hiện cùng một action đang có nhiều policy khác nhau.

## Bước 9 — Visit lifecycle T-6 gate

```text
V17 BEFORE_VISIT → DURING_VISIT earliest at plannedStartAt - 6h
```

Phải sửa đồng thời command + permission + next-task + frontend; không chỉ chặn ở UI.


## Bước 10 — Operational-contact transfer atomicity

```text
V18 transfer/token atomicity + DB enum verification + retry/recovery
```

Ưu tiên cao vì đây là lỗi có thể tạo trạng thái business đã commit nhưng UI báo thất bại.

---

# 24. File / module dự kiến tác động

Tên chính xác phải được agent verify lại trên HEAD trước khi sửa.

## Backend

```text
VisitFormReadService.cs
ViewGuestDelegationListQueryHandler.cs
UpdatePendingVisitRequestV2CommandHandler.cs
VisitRequestV2EditService.cs

ApproveCampusInstanceCommand.cs
ApproveCampusInstanceCommandHandler.cs
RejectCampusInstanceCommand.cs
RejectCampusInstanceCommandHandler.cs
CampusApprovalExecutor.cs

OperationalContactInvitationService.cs
SaveOperationalContactCommandHandler.cs
ReplaceOperationalContactCommandHandler.cs
CancelOperationalContactInvitationCommandHandler.cs
AcceptOperationalContactConfirmationCommandHandler.cs

GetVisitRequestHistoryQueryHandler.cs
GetVisitHistoryDetailQueryHandler.cs
VisitHistoryDetailContracts.cs

GetEligibleVisitInstancesForNewsQueryHandler.cs
CreateNewsCommandHandler.cs
VisitNewsAccess.cs
GetVisitInstanceNewsQueryHandler.cs

CompleteVisitStageCommandHandler.cs
VisitProcessPermissionDto.cs
GetVisitProcessPermissionsQueryHandler.cs
VisitNextTaskBuilder.cs

SaveOperationalContactCommandHandler.cs
InitiateOperationalContactTransferCommandHandler.cs
ResendOperationalContactConfirmationCommandHandler.cs
ManageOperationalContactHandlers.cs
OperationalContactInvitationService.cs
IOperationalContactInvitationService.cs
EmailActionConstants.cs
```

Có thể cần controller/API contract liên quan operational-contact public actions.

## Frontend

```text
VisitRequestManagement.tsx
VisitRequestV2DetailView.tsx
CampusVisitDetailCard.tsx
EditVisitRequestV2Page.tsx
ContactIdentityActions.tsx
VisitHistoryTimeline.tsx
AssignHostModal.tsx
visitRequestV2Api.ts
visitRequestV2.json (VI/EN)

CreateNews.tsx
VisitNewsPostList.tsx
VisitProcess.tsx
delegations.types.ts
```

Thêm invitation page/component nếu chưa có authenticated/public action surface phù hợp.

---

# 25. Database

## 25.1 Mục tiêu

**Không tạo migration/schema mới nếu không bắt buộc.**

Ưu tiên reuse:

- visit request / campus `rowVersion`;
- identity change tables;
- identity change events;
- email action token table;
- user provisioning service;
- revision history.

## 25.2 Chỉ tạo migration nếu audit chứng minh thiếu

Ví dụ chỉ khi token table thực tế không thể phân biệt:

```text
ACCEPT
DECLINE
```

Nhưng trước hết phải kiểm tra `IntendedAction` hiện có.

Không tạo bảng mới chỉ vì dễ code hơn.


### Bổ sung V18 — schema token transfer

Runtime/canonical DB phải xác nhận:

```text
email_action_tokens.action_context
  contains VISIT_CONTACT_TRANSFER

email_action_tokens.target_type
  supports VISIT_REQUEST_IDENTITY_CHANGE
```

Nếu thiếu:

- sửa canonical SQL/migration source-of-truth;
- update hash pin nếu canonical SQL thay đổi;
- không chỉ ALTER local DB thủ công rồi bỏ qua repository.

---

# 26. Test matrix bắt buộc

## 26.1 Permission

- STAFF row source nào thì mở đúng detail của source đó.
- Không còn list-visible → generic-detail-403 mismatch.
- Không broad permission.

## 26.2 Registrant edit

- edit snapshot fields thành công.
- email immutable.
- profile User không bị thay đổi.
- edit hoạt động khi `PENDING_CONTACT_CONFIRMATION`.

## 26.3 Global gate

### 2 campus:

```text
HN confirmed
HCM pending
```

Expected:

- cả 2 Staff Leader không thấy review row;
- direct URL leader scope bị chặn;
- registrant vẫn thấy;
- registrant-leader thấy qua registrant relation;
- approval bị reject.

Sau HCM confirm:

- gate opens;
- leaders thấy campus scope;
- approval enabled.

## 26.4 Notification

- không gửi leader trước final confirm;
- final confirm gửi một lần.

## 26.5 Cancel / reinvite

- cancel initial;
- cancel transfer;
- state sau cancel đúng;
- cùng email reinvite được;
- old token invalid;
- new token works.

## 26.6 History

- identity event có `eventId`;
- Eye mở detail;
- legacy event mapped;
- no token leaked;
- email masked.

- revision 2 có revision 1 baseline và hiển thị exact before → after;
- legacy missing baseline được chụp trước mutation;
- member/schedule diff không bị mất;
- previous revision thật sự không có thì UI báo `PREVIOUS_REVISION_MISSING`, không nói “không có thay đổi”.

## 26.7 Email action

- scanner GET không mutate;
- accept POST success;
- decline POST success;
- expired token reject;
- wrong intended action reject;
- replay idempotent/reject theo policy;
- no-login accept creates/links correct user.

## 26.8 Concurrency

```text
Leader loads v4
Visitor edits → v5
Leader approves with expected v4
→ 409
→ no approval written
```

Reload:

```text
Leader reviews v5
approve expected v5
→ success
```

Reject tương tự.

## 26.9 Accordion

- A + B mở đồng thời.
- đóng A không ảnh hưởng B.
- deep-link add B không đóng A.
- single-campus luôn mở.


## 26.10 News eligibility

- Process `canCreate=true` thì preset Create News phải select được cùng instance.
- Host AFTER_VISIT tạo được.
- Staff Leader self-host tạo được.
- accepted IC_SUPPORT tạo được.
- accepted STUDENT tạo được.
- DEPT_SUPPORT không được nếu policy cấm.
- BEFORE/DURING không được.
- media denied / news waived cho cùng reason ở list + eligible + create.
- participant status semantics thống nhất.
- direct POST không bypass canonical rule.

## 26.11 T-6 lifecycle gate

- T-6h-1s reject.
- đúng T-6h allow nếu preparation complete.
- sau T-6h allow.
- trước T-6 UI disabled.
- NextTask không yêu cầu action quá sớm.
- schedule đổi → cutoff đổi.
- non-host vẫn forbidden.
- backend dùng VietnamNow.


## 26.12 Transfer atomicity

- initiate transfer success → identity change + token cùng tồn tại.
- token persistence fail → không để dangling PENDING transfer.
- SMTP fail → transfer/token vẫn recover được qua resend.
- duplicate click → một pending change.
- pending conflict → stable 409.
- cancel/decline/expire giữ current contact.
- accept swap holder một lần.
- schema có `VISIT_CONTACT_TRANSFER`.

---

# 27. Regression gates

Sau từng phase:

## Backend

```bash
dotnet build
dotnet test
```

Chạy cả unit + integration suites liên quan.

## Frontend

```bash
npm run lint
npm run typecheck
npm run build
npm test
```

Hoặc command tương ứng đúng package scripts hiện tại.

## Real-stack

Tối thiểu chạy journey:

1. Visitor tạo multi-campus request.
2. Hai contact ở hai campus.
3. Một contact confirm, một pending.
4. Visitor edit.
5. Staff Leader chưa thấy.
6. Contact cuối confirm.
7. Staff Leader thấy request.
8. Visitor edit làm version thay đổi trước decision.
9. Staff Leader stale decision bị 409.
10. Reload và approve version mới.
11. Mở cùng lúc cả hai campus trong detail.
12. Cancel/reinvite contact.
13. Accept/Decline invitation qua đúng flow.

---

# 28. Không được làm

Không:

- mở broad request permission cho STAFF để chữa navigation;
- cho Staff Leader thấy review queue khi còn bất kỳ contact pending;
- auto-approve sau concurrency reload;
- mutate Accept/Decline bằng GET;
- dùng email như một field edit identity bình thường;
- tạo account phụ trùng email khi public accept;
- tạo database table mới trước khi audit token/revision tables hiện tại;
- hardcode tiếng Việt trong component;
- xóa history security masking;
- đổi single-open accordion sang “tất cả mở mặc định” nếu chưa có yêu cầu; chỉ cần cho phép multi-expand độc lập;
- rewrite toàn bộ module đã hoạt động chỉ để fix một state.
- giữ nhiều bản `news create eligibility` khác nhau giữa process/list/create-command.
- chữa lỗi news bằng cách chỉ bỏ cảnh báo preset mà không thống nhất backend authorization.
- chỉ disable nút BEFORE→DURING ở frontend mà không có backend T-6 guard.
- auto chuyển sang DURING_VISIT khi tới T-6; Host vẫn phải xác nhận và các blocker vẫn phải được kiểm tra.
- để transfer commit trước rồi token persistence fail mà API vẫn trả generic 500.
- chữa V18 bằng cách nuốt lỗi token và để invitation PENDING không có link xác nhận.
- chỉ sửa DB local mà không cập nhật canonical SQL/migration source-of-truth.

---

# 29. Definition of Done

Chỉ coi task hoàn tất khi đồng thời đạt:

- [ ] STAFF không còn thấy row dẫn tới route không có quyền.
- [ ] Registrant sửa được snapshot thông tin được phép.
- [ ] Registrant sửa được khi đang chờ contact confirmation.
- [ ] Staff Leader không thấy/review trước global contact gate.
- [ ] Registrant-Leader vẫn xem được đơn của mình qua registrant relation.
- [ ] Không notify leader trước final contact confirmation.
- [ ] Cancel invitation có state/hậu quả rõ ràng.
- [ ] Có thể reinvite cùng email sau cancel.
- [ ] Contact history có detail rõ, không generic với event đã biết.
- [ ] Revision history có usable before snapshot; edit thực tế phải hiện đúng các field `Trước → Sau`.
- [ ] Nếu historical baseline thật sự thiếu và không thể phục hồi, UI báo thiếu dữ liệu so sánh thay vì khẳng định không có thay đổi.
- [ ] Logged-in invitee có Accept/Decline rõ ràng.
- [ ] Email có Accept/Decline không bắt login và không mutate qua GET.
- [ ] Pending notice đã bỏ câu wording sai.
- [ ] Approve/Reject stale revision trả 409 và không ghi decision.
- [ ] Multi-campus cho phép mở nhiều accordion cùng lúc.
- [ ] Deep-link campus không đóng các campus đang mở.
- [ ] Single-campus UX giữ nguyên.
- [ ] Không phát sinh migration nếu không có lý do kỹ thuật bắt buộc.
- [ ] Backend tests xanh.
- [ ] Frontend tests xanh.
- [ ] Process và Create News đưa ra cùng verdict tạo tin cho cùng user + visit instance.
- [ ] News list/eligible/create command dùng cùng eligibility policy.
- [ ] BEFORE_VISIT → DURING_VISIT bị chặn trước T-6 và mở đúng từ T-6.
- [ ] Permission, NextTask và frontend đồng bộ với T-6 backend guard.
- [ ] Transfer không còn partial-commit giữa identity change và action token.
- [ ] Runtime/canonical DB hỗ trợ `VISIT_CONTACT_TRANSFER`.
- [ ] SMTP failure có recovery rõ ràng và không tạo false-failure UI.
- [ ] Real-stack journey chính xanh.

---

# 30. Báo cáo agent phải trả sau khi thực hiện

Agent phải báo ngắn gọn theo format:

## Preflight

```text
Branch:
HEAD start:
Working tree:
```

## Thay đổi

```text
V01:
V02:
...
V18:
```

Mỗi mục:

```text
Root cause:
Files changed:
Behavior before:
Behavior after:
Tests:
```

## Database

```text
Migration created: YES/NO
Reason:
```

## Gates

```text
Backend build:
Backend unit:
Backend integration:
Frontend lint:
Frontend typecheck:
Frontend build:
Frontend unit:
Real-stack:
```

## Git

```text
HEAD final:
Commits:
Uncommitted files:
```

Không báo `FINAL CLOSURE COMPLETE` nếu còn bất kỳ gate bắt buộc nào chưa chạy hoặc còn lỗi chưa phân loại.
