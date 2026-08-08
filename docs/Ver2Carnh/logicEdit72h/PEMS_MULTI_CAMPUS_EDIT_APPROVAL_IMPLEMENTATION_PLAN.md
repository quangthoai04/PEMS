# PEMS — KẾ HOẠCH TRIỂN KHAI LOGIC SỬA / DUYỆT ĐƠN LIÊN CƠ SỞ

> Mục đích: dùng làm implementation specification cho AI Agent / Developer sửa Backend + Frontend + test theo đúng business rule đã chốt.
>
> Phạm vi: Per-Campus v2, chỉnh sửa đơn trước duyệt, chỉnh sửa sau duyệt, amendment, quyền theo relation, rule thời gian, campus-set immutability.
>
> Nguyên tắc triển khai: **minimal change, reuse kiến trúc/service hiện có, không tạo workflow/song song nếu có thể mở rộng logic hiện tại, không reset sibling campus**.

---

# 1. Mục tiêu nghiệp vụ

Hệ thống phải xử lý đơn liên cơ sở theo nguyên tắc:

```text
TRƯỚC DUYỆT
Registrant / Operational Contact sửa
        ↓
Staff Leader của đúng campus duyệt
        ↓
Approve bắt buộc gán Host
        ↓
SAU DUYỆT
Current Host tiếp quản campus
        ↓
Host duyệt / từ chối amendment
```

Các campus trong cùng một request phải độc lập về lifecycle.

Ví dụ:

```text
Request MC-001

HN  = ASSIGNED
HCM = WAITING_REQUEST_APPROVAL
DN  = REJECTED
```

thì:

```text
HN  → Safe Edit / Amendment
HCM → EditPendingCampus
DN  → ResubmitInstance
```

Không một mutation của HCM hoặc DN được reset:

- status HN;
- Host HN;
- decision HN;
- approval revision HN;
- row version HN;
- form revision HN.

---

# 2. Phạm vi thay đổi

## 2.1. Backend

Cần rà và cập nhật tối thiểu các nhóm sau:

- `VisitMutationPolicy`
- `VisitMutationGuard`
- `VisitFormReadService`
- pending-edit v2
- per-campus edit command/handler mới
- safe edit
- amendment submit
- amendment approve/reject
- approve campus
- host transfer
- authorization / ownership checks
- audit/history
- aggregate status recompute
- validation thời gian
- integration tests / unit tests

Các file đã liên quan trong code hiện tại gồm:

```text
backend/PEMS.Api/Controllers/VisitRequestsController.cs

backend/.../UpdatePendingVisitRequestV2CommandHandler.cs
backend/.../VisitMutationGuard.cs
backend/.../VisitMutationPolicy.cs
backend/.../VisitRequestAggregateStatusService.cs
backend/.../ApproveCampusInstanceCommandHandler.cs
backend/.../VisitSafeEditService.cs
backend/.../VisitFieldClassifier.cs
backend/.../VisitAmendmentService.cs
backend/.../VisitFormReadService.cs
```

Agent phải tìm đúng path hiện tại trên branch đang triển khai trước khi sửa.

## 2.2. Frontend

Cần cập nhật tối thiểu:

- action/capability constants;
- detail page;
- campus card;
- pending edit form;
- per-campus pending edit flow;
- safe edit;
- amendment form;
- amendment approve/reject UI cho Host;
- self-approval UI;
- confirm override 72h cho Staff Leader;
- remove toàn bộ Add/Remove campus khỏi màn Edit;
- i18n VI/EN;
- tests.

File đã liên quan hiện tại:

```text
VisitRequestV2DetailView.tsx
visitV2Actions.ts / action constants tương đương
các modal/form edit v2
campus card / campus table
API client v2
```

---

# 3. Nguyên tắc phân quyền cốt lõi

Không phân quyền chỉ dựa vào `role`.

Backend phải tính relation của user với **request và từng visit instance**:

```csharp
isRegistrant
isOperationalContactOfInstance
isCampusLeader
isCurrentHost
isHO
```

Một user có thể đồng thời có nhiều relation.

Ví dụ:

```text
STAFF + Registrant
→ quyền Registrant

STAFF_LEADER + Registrant
→ quyền Registrant + Staff Leader

Registrant + CurrentHost
→ quyền Registrant + Host
```

## 3.1. Không viết authorization kiểu role-only

Không dùng logic dạng:

```csharp
if (currentUser.Role == STAFF)
{
    // allow request edit
}
```

Phải dùng relation.

Ví dụ:

```csharp
var isRegistrant =
    visit.RegistrantUserId == actorId;

var isOperationalContact =
    instance.OperationalContactUserId == actorId;

var isCampusLeader =
    currentUser.RoleCode == RoleCodes.Staff
    && currentUser.SubRole == UserSubRoles.Leader
    && currentUser.PrimaryCampusId == instance.CampusId;

var isCurrentHost =
    instance.CurrentHostUserId == actorId; // dùng đúng property thực tế trong entity hiện tại
```

> Không invent field mới nếu entity hiện tại đang dùng tên khác. Reuse field Host hiện có.

---

# 4. Quyền của Registrant

Registrant là owner phía requester của toàn request.

## 4.1. Quyền xem

Registrant được xem:

- toàn request;
- tất cả campus trong request;
- trạng thái từng campus;
- thông tin form của tất cả campus;
- history/audit được phép hiển thị cho requester.

## 4.2. Quyền sửa

Registrant được sửa toàn bộ dữ liệu phía requester theo lifecycle.

### Trường hợp tất cả campus còn pending

Nếu tất cả instance đều:

```text
WAITING_REQUEST_APPROVAL
```

thì vẫn cho:

```text
EditPendingRequest
```

để sửa toàn đơn.

Nhưng **không được Add / Remove / Replace campus**.

### Trường hợp request đã mixed state

Ví dụ:

```text
HN  ASSIGNED
HCM WAITING_REQUEST_APPROVAL
```

không cho whole-request pending edit.

Thay vào đó:

```text
HN  → Safe Edit / Amendment
HCM → EditPendingCampus
```

---

# 5. Quyền của Operational Contact

Operational Contact chỉ có quyền trên campus mà họ được gán.

Ví dụ:

```text
HN  → Contact A
HCM → Contact B
```

A được:

```text
Xem HN
Sửa HN trong phạm vi requester-side được phép
Safe Edit HN
Submit Amendment HN
```

A không được:

```text
Sửa HCM
Sửa toàn request
Approve campus
Reject campus
Approve Amendment
Reject Amendment
Add / Remove campus
```

## 5.1. Operational Contact identity phải tách khỏi normal edit

Không dùng pending-edit / safe-edit / amendment để đổi identity đầu mối một cách âm thầm.

Giữ workflow riêng:

```text
same email
→ metadata correction

different email trước decision
→ replace + confirmation

different email sau decision
→ transfer
```

Frontend phải tách rõ:

```text
Sửa thông tin đơn
```

và:

```text
Thay đổi đầu mối
```

---

# 6. Quyền của STAFF bình thường

Role `STAFF` tự nó không tạo quyền sửa request.

Quyền chỉ xuất hiện nếu user đồng thời có relation:

```text
STAFF + Registrant
→ quyền Registrant

STAFF + Operational Contact
→ quyền Contact

STAFF + Current Host
→ quyền Host

STAFF không liên quan
→ không có mutation right
```

---

# 7. Staff Leader trước approval

Khi instance:

```text
WAITING_REQUEST_APPROVAL
```

Staff Leader của **đúng campus** là approval authority.

Có quyền:

```text
Approve
Reject
Assign Host
```

Authorization phải check đúng campus.

Không được Staff Leader HN duyệt HCM.

---

# 8. Approve bắt buộc gán Host

Approve campus phải là một workflow atomic:

```text
WAITING_REQUEST_APPROVAL
        ↓
Staff Leader chọn Host
        ↓
Approve
        ↓
ASSIGNED
```

Không tồn tại business case hợp lệ:

```text
ASSIGNED
Host = null
```

Nếu dữ liệu tồn tại case này thì xem là:

```text
INVARIANT / DATA CORRUPTION
```

không tạo fallback business logic.

## 8.1. Backend invariant

Approve handler phải:

1. validate Staff Leader đúng campus;
2. validate Host hợp lệ;
3. assign Host;
4. set decision;
5. set status `ASSIGNED`;
6. bump revision/row version phù hợp;
7. recompute aggregate;
8. commit trong cùng transaction.

Nếu assign Host fail thì approve không được commit.

---

# 9. Sau approval: Current Host tiếp quản campus

Khi Staff Leader đã:

```text
Approve + Assign Host
```

thì quyền chủ quản chuyển sang Current Host.

Host có quyền:

- xem campus;
- xử lý setup;
- agenda;
- logistics;
- phối hợp với requester;
- approve amendment;
- reject amendment.

## 9.1. Amendment authority sau approval

Sau approval:

```text
canDecideAmendment = isCurrentHost
```

Không còn:

```text
canDecideAmendment = isCampusLeader
```

Staff Leader không phải amendment authority thông thường sau khi đã bàn giao Host.

---

# 10. Không có Staff Leader fallback sau approval

Không implement:

```text
if Host == null
    allow StaffLeader to approve amendment
```

Vì `Approve` bắt buộc phải có Host.

Nếu Host null sau approval thì phải xử lý như invariant violation, không hợp thức hóa bằng fallback.

---

# 11. Transfer Host

Nếu:

```text
Host A
→ Transfer
→ Host B
```

sau khi transfer hoàn tất:

```text
A mất quyền approve/reject amendment
B nhận quyền approve/reject amendment
```

Authority luôn đi theo Current Host hiện tại.

Pending amendment nếu đang tồn tại phải được quyết định theo **Current Host tại thời điểm decision**, không theo Host lúc amendment được tạo.

---

# 12. Staff Leader đồng thời là Registrant

Ví dụ:

```text
A = Registrant
A = Staff Leader HN
HN = WAITING_REQUEST_APPROVAL
```

A có:

```text
Registrant right → sửa
Staff Leader right → duyệt
```

Frontend nên hỗ trợ:

```text
[Lưu]
[Lưu và duyệt]
```

## 12.1. Lưu

```text
Save edit
→ HN vẫn WAITING_REQUEST_APPROVAL
```

Không auto-approve.

## 12.2. Lưu và duyệt

Luồng atomic:

```text
validate edit
→ save edit
→ validate Staff Leader authority
→ assign Host
→ approve
→ audit
→ commit
```

Nếu bất kỳ bước nào lỗi thì rollback toàn bộ.

---

# 13. Registrant + Host sau approval

Ví dụ:

```text
A = Registrant
A = CurrentHost HN
HN = ASSIGNED
```

A vừa là requester-side vừa là amendment authority.

Không bắt user:

```text
Submit Amendment
→ mở lại
→ tự Approve
```

Phải xử lý:

```text
Submit change
→ create amendment
→ auto approve
→ apply
```

UI chỉ cần action kiểu:

```text
[Cập nhật]
```

## 13.1. Vẫn phải lưu amendment history

Không update thẳng active form rồi bỏ qua history.

Phải giữ:

```text
requested_by = A
decided_by   = A
status       = APPROVED
```

và audit rõ đây là self-approved amendment.

Không cần tạo bảng mới nếu history/amendment hiện tại đã đủ lưu thông tin.

---

# 14. Operational Contact + Host

Nếu:

```text
A = Operational Contact HN
A = CurrentHost HN
```

thì approval-sensitive change của HN:

```text
Create Amendment
→ Auto Approve
→ Apply
```

Rule tổng quát:

```csharp
requesterSide =
    isRegistrant || isOperationalContact;

canDecideAmendment =
    isCurrentHost;

selfApprove =
    requesterSide && canDecideAmendment;
```

---

# 15. Staff Leader + Host

Nếu Staff Leader tự gán mình làm Host:

```text
A = Staff Leader HN
A = CurrentHost HN
```

sau approval quyền amendment của A đến từ:

```text
isCurrentHost
```

không phải `isCampusLeader`.

Nếu transfer Host cho B:

```text
A vẫn là Staff Leader
A không còn quyền approve amendment

B = CurrentHost
B có quyền approve amendment
```

---

# 16. Multi-campus phải độc lập từng instance

Ví dụ:

```text
Request MC-001

HN  = ASSIGNED
HCM = WAITING_REQUEST_APPROVAL
DN  = REJECTED
```

Các mutation phải target-only.

## 16.1. Sửa HCM

Được phép thay đổi HCM.

Không được thay đổi:

```text
HN.Status
HN.Host
HN.DecidedBy
HN.DecidedAt
HN.FormRevision
HN.ApprovalRevision
HN.RowVersion
```

## 16.2. Resubmit DN

Chỉ DN thay đổi.

HN/HCM không bị reset.

## 16.3. Amendment HN

Chỉ HN thay đổi sau khi Host HN duyệt.

Sibling không bị bump revision.

---

# 17. Bổ sung EditPendingCampus

Đây là gap chính cần sửa.

Hiện whole-request `EditPendingRequest` là all-or-nothing.

Khi request:

```text
HN  ASSIGNED
HCM WAITING_REQUEST_APPROVAL
```

thì HCM không được rơi vào dead-end.

Cần action mới:

```text
EDIT_PENDING_CAMPUS
```

Endpoint đề xuất:

```http
PUT /api/v2/visit-requests/{requestId}/instances/{instanceId}/pending-edit
```

Tên endpoint có thể điều chỉnh theo convention hiện tại nhưng phải giữ semantics per-instance.

## 17.1. Authorization

Cho requester-side hợp lệ:

```text
Registrant
Operational Contact của đúng instance
```

Nếu muốn minimal-compatible với handler cũ có thể rollout Registrant trước, nhưng target specification cuối cùng là Contact cũng được sửa campus họ phụ trách.

Không cho:

```text
Contact sibling
unrelated Staff
HO
Host của sibling
```

## 17.2. Lifecycle

Chỉ cho:

```text
WAITING_REQUEST_APPROVAL
```

Không cho:

```text
ASSIGNED
BEFORE_VISIT
REJECTED
DURING_VISIT
AFTER_VISIT
CLOSED
```

Các trạng thái kia dùng workflow riêng.

## 17.3. Fields

Per-campus pending edit được sửa dữ liệu campus đang pending, ví dụ:

- planned start/end;
- purpose/content nếu model hiện tại lưu per-campus;
- working language;
- visit type/other;
- guest members;
- support members;
- notes;
- transportation;
- operational contact display metadata nếu policy hiện tại cho phép.

Không dùng endpoint này để:

- thay email identity contact;
- add campus;
- remove campus;
- replace campus;
- mutate sibling;
- sửa field shared có thể làm thay đổi campus đã approved nếu chưa có policy riêng.

## 17.4. Concurrency

Bắt buộc:

- instance row version;
- form revision nếu model hiện tại dùng;
- fail conflict nếu stale.

## 17.5. Sau save

Target instance:

```text
vẫn WAITING_REQUEST_APPROVAL
```

Request aggregate recompute.

Ví dụ:

```text
HN  ASSIGNED
HCM WAITING_REQUEST_APPROVAL
```

sau edit HCM:

```text
HN  ASSIGNED
HCM WAITING_REQUEST_APPROVAL
Request = PARTIALLY_APPROVED
```

---

# 18. Campus Set Immutability

Đây là rule đã chốt.

## 18.1. Trong màn Create

Trước khi request được tạo:

```text
Add campus    ✅
Remove campus ✅
```

## 18.2. Sau khi request được tạo

Tập campus là bất biến:

```text
Add campus      ❌
Remove campus   ❌
Replace campus  ❌
```

Áp dụng cho mọi lifecycle.

Không có ngoại lệ khi tất cả campus vẫn pending.

## 18.3. Nếu muốn thêm campus

User phải:

```text
Tạo request mới
```

Không thêm campus vào request cũ.

## 18.4. Nếu muốn bỏ campus

Không xử lý bằng Edit.

Nếu sau này sản phẩm cần cancel một campus thì đó phải là workflow `CancelCampus` riêng.

Không hard-delete campus trong edit.

---

# 19. Backend enforce Campus Set Immutability

Không chỉ ẩn nút frontend.

Whole-request pending edit hiện tại nếu cho add/drop campus thì phải sửa lại.

Backend phải kiểm tra tập campus submit đúng bằng tập campus hiện hữu.

Ví dụ semantics:

```csharp
submittedCampusIds.SetEquals(existingCampusIds)
```

Nếu:

- xuất hiện campus mới;
- thiếu campus cũ;
- cố thay campus ID;

thì reject.

Error code đề xuất:

```text
VISIT_REQUEST_CAMPUS_SET_IMMUTABLE
```

Message VI:

```text
Danh sách cơ sở không thể thay đổi sau khi đơn đã được tạo.
Vui lòng tạo đơn mới nếu muốn đăng ký thêm cơ sở.
```

Message EN:

```text
The campus list cannot be changed after the request has been created.
Please create a new request if you want to visit an additional campus.
```

Không bắt buộc đúng tên error code nếu project đã có convention tương đương; ưu tiên reuse convention hiện tại.

---

# 20. Frontend Campus Set Immutability

Màn Edit phải bỏ:

```text
[+ Thêm cơ sở]
[Xóa cơ sở]
```

Không render control thay campus.

Chỉ màn Create mới cho add/remove campus.

Nếu API trả campus-set immutable error thì FE hiển thị message field/form-level phù hợp, không toast mơ hồ.

---

# 21. Shared request fields

Cần phân biệt:

- request-level shared fields;
- instance-level campus fields.

Không cho per-campus pending edit âm thầm mutate shared field nếu field đó có thể làm thay đổi ý nghĩa của sibling đã approved.

Agent phải audit DTO hiện tại.

Nếu field shared thật sự requester-profile và được phép safe-edit, giữ trong shared safe-edit workflow.

Nếu field approval-sensitive và ảnh hưởng nhiều campus, không nhét vào per-campus edit.

---

# 22. Safe Edit sau approval

Áp dụng cho:

```text
ASSIGNED
BEFORE_VISIT
```

Safe Edit là thay đổi nhỏ không cần approval lại.

Reuse `VisitFieldClassifier`.

Các field SAFE hiện tại phải được audit lại nhưng không mở rộng tùy tiện.

Ví dụ có thể gồm:

```text
notes
transportation note
một số display metadata
```

Nếu field approval-sensitive / structural thì phải đi amendment.

---

# 23. Amendment sau approval

Requester-side:

```text
Registrant
Operational Contact của đúng campus
```

được submit amendment trên target campus.

Current Host của target campus:

```text
Approve
Reject
```

Không dùng Staff Leader để decide amendment sau approval.

---

# 24. Approval-sensitive / Structural fields

Các field hiện classifier coi là approval-sensitive / structural tiếp tục dùng amendment.

Ví dụ:

```text
Delegation name
Purpose
Working content
Working language
Operational-contact business data phù hợp
Guest list
Support members
PlannedStartAt
PlannedEndAt
```

Agent phải dựa trên classifier hiện tại và không tùy tiện đổi classification ngoài phạm vi yêu cầu.

---

# 25. Apply Amendment

Khi Host approve:

1. verify Current Host;
2. verify amendment vẫn PENDING;
3. verify target instance;
4. verify lifecycle;
5. verify base revision;
6. verify row version/concurrency;
7. validate proposed values;
8. apply target-only;
9. bump target revision;
10. bump target row version;
11. recompute canonical projection/fingerprint nếu kiến trúc hiện tại yêu cầu;
12. audit;
13. commit.

Không reset:

```text
ASSIGNED → WAITING_REQUEST_APPROVAL
```

Campus giữ lifecycle approved.

---

# 26. Rule thời gian — hằng số

Tách rõ hai khái niệm:

```csharp
MinScheduleLeadHours = 72;
MutationCutoffHours  = 6;
```

Không dùng một constant cho cả hai.

## 26.1. Ý nghĩa 72 giờ

```text
72h = rule của schedule trước approval / request approval lần đầu.
```

## 26.2. Ý nghĩa 6 giờ

```text
6h = action window sau approval cho Safe Edit / Amendment.
```

---

# 27. Create — rule 72 giờ

Khi tạo request/campus:

```text
plannedStartAt >= now + 72h
plannedEndAt > plannedStartAt
duration >= 30 phút
```

Không ngoại lệ cho requester.

---

# 28. Pending Edit trước approval — không đổi schedule

Nếu Registrant / Contact sửa content nhưng không đổi:

```text
PlannedStartAt
PlannedEndAt
```

thì:

```text
KHÔNG recheck 72h
```

Lý do: thời gian trôi qua không được biến một request hợp lệ thành invalid nếu user chỉ sửa nội dung.

---

# 29. Pending Edit trước approval — có đổi schedule

Nếu Registrant / Contact đổi schedule:

```text
newStart >= now + 72h
newEnd > newStart
duration >= 30 phút
```

Nếu `<72h`:

```text
reject
```

---

# 30. Staff Leader override 72 giờ trước approval

Staff Leader đúng campus được quyền override rule 72h.

Nếu Staff Leader sửa:

```text
newStart < now + 72h
```

thì không block cứng.

Frontend phải hiện confirm.

Ví dụ:

```text
Lịch mới không đáp ứng thời gian đăng ký trước tối thiểu 72 giờ.

Với quyền Staff Leader của cơ sở này, bạn có thể xác nhận tiếp tục với lịch này.

[Quay lại]
[Xác nhận và tiếp tục]
```

## 30.1. Không auto-approve chỉ vì override

Nếu Staff Leader chọn:

```text
Lưu
```

thì vẫn:

```text
WAITING_REQUEST_APPROVAL
```

Nếu chọn:

```text
Lưu và duyệt
```

thì:

```text
confirm override
→ save
→ assign Host
→ approve
```

---

# 31. Backend enforce Staff Leader override

Không chỉ dựa frontend.

Nếu:

```text
scheduleChanged
AND newStart < now + 72h
```

thì:

### Registrant / Contact

```text
reject
```

### Staff Leader đúng campus + chưa confirm

```text
return confirmation-required
```

### Staff Leader đúng campus + confirmed

```text
allow
```

Payload có thể thêm transient flag:

```json
{
  "overrideLeadTimeConfirmed": true
}
```

Không cần column DB mới chỉ để lưu flag này.

Audit action thực tế.

---

# 32. Audit override 72 giờ

Khi Staff Leader override, phải có audit.

Tối thiểu lưu được:

```text
actor
requestId
instanceId / campusId
oldStart
newStart
timestamp
action = LEAD_TIME_OVERRIDE
```

Nếu audit framework hiện tại có metadata JSON / diff thì reuse.

Không tạo bảng mới nếu audit hiện tại đã đủ.

---

# 33. Staff Leader approve lịch cũ

Nếu Staff Leader approve nhưng không đổi schedule:

```text
KHÔNG recheck 72h
```

Ví dụ:

```text
Request tạo đúng rule ngày 01/08.
Lịch 10/08.

Staff Leader đến 08/08 mới duyệt.
```

Không reject chỉ vì hiện tại còn <72h.

---

# 34. Staff Leader sửa lịch rồi approve

Nếu Staff Leader chỉnh schedule:

- `>=72h`: approve bình thường;
- `<72h`: cho override sau confirm.

Không block Staff Leader bằng hard validation 72h.

---

# 35. Resubmit

Campus:

```text
REJECTED
```

khi resubmit phải có schedule hợp lệ:

```text
start >= now + 72h
end > start
duration >= 30 phút
```

Resubmit được coi như lần xin approval lại trước Staff Leader.

Không áp logic amendment sau approval vào resubmit.

---

# 36. Sau approval: bỏ rule 72 giờ

Khi:

```text
ASSIGNED
BEFORE_VISIT
```

không yêu cầu:

```text
newStart >= now + 72h
```

cho amendment.

Sau approval Host có quyền linh động xử lý schedule với requester.

---

# 37. Cutoff 6 giờ sau approval

Chốt phương án A: dùng cutoff chung cho mọi Safe Edit và Amendment.

Nếu:

```text
currentStart - now >= 6h
```

thì:

```text
Safe Edit        ✅
Submit Amendment ✅
```

Nếu:

```text
currentStart - now < 6h
```

thì:

```text
Safe Edit        ❌
Submit Amendment ❌
```

Equality tại đúng mốc 6 giờ phải dùng semantics thống nhất với policy hiện tại; ưu tiên giữ behavior hiện có nếu đang `>=`.

---

# 38. Không có privacy exception

Bỏ hoàn toàn ngoại lệ:

```text
Media Consent → DECLINED
→ bypass cutoff
```

Không có:

```text
PRIVACY_URGENT deadline bypass
mixed payload bypass
special cutoff theo field
```

Tất cả Safe Edit / Amendment chịu cùng cutoff 6h.

> Nếu classifier vẫn giữ category `PRIVACY_URGENT` cho mục đích khác thì không nhất thiết xóa enum ngay; nhưng tuyệt đối không dùng category đó để bypass deadline.

---

# 39. Amendment đổi lịch sau approval

Nếu còn trong action window:

```text
currentStart - now >= 6h
```

thì proposed schedule chỉ cần:

```text
newStart > now
newEnd > newStart
duration >= 30 phút
```

Không cần:

```text
newStart >= now + 72h
```

---

# 40. Lý do amendment không cần 72 giờ

Lịch cũ vẫn là lịch chính thức cho tới khi Host approve.

Ví dụ:

```text
Current:
10/08 14:00

Proposal:
09/08 15:00
```

Nếu Host:

```text
Approve
→ dùng 09/08 15:00
```

Nếu Host:

```text
Reject
→ giữ 10/08 14:00
```

Do đó proposal sau approval được linh động hơn pre-approval request.

---

# 41. Host approve schedule amendment

Khi Host approve, không recheck 72h.

Chỉ validate:

```text
proposedStart > now
proposedEnd > proposedStart
duration >= 30 phút

amendment == PENDING
actor == CurrentHost
baseRevision hợp lệ
rowVersion hợp lệ
instance vẫn ASSIGNED / BEFORE_VISIT
```

Nếu proposed start đã qua:

```text
reject approval
```

---

# 42. Self-approved amendment vẫn phải validate

Registrant + Host hoặc Contact + Host:

```text
Create Amendment
→ Auto Approve
```

nhưng không bypass:

```text
currentStart - now >= 6h
newStart > now nếu đổi schedule
newEnd > newStart
duration >= 30 phút
concurrency
lifecycle
```

Self-approval chỉ bỏ bước chờ decision của user khác.

---

# 43. Lifecycle đóng mutation

Từ:

```text
DURING_VISIT
AFTER_VISIT
CLOSED
```

không cho:

```text
Pending Edit
Safe Edit
Amendment
Reschedule form
```

Các nghiệp vụ sau đó phải đi workflow riêng:

- check-in;
- actual agenda;
- report;
- feedback;
- close;
- các tác vụ vận hành hiện hữu.

---

# 44. Capability model cần cập nhật

Backend phải là source of truth.

Frontend không tự suy quyền từ status.

Cần capability per-instance mới:

```text
EDIT_PENDING_CAMPUS
```

Ví dụ response:

```json
{
  "code": "EDIT_PENDING_CAMPUS",
  "scope": "INSTANCE",
  "visitInstanceId": 3104,
  "enabled": true
}
```

## 44.1. Pending campus

Nếu:

```text
instance = WAITING_REQUEST_APPROVAL
actor = Registrant
```

hoặc:

```text
actor = Operational Contact của chính instance
```

thì:

```text
EDIT_PENDING_CAMPUS = enabled
```

## 44.2. Approved campus

Không trả `EDIT_PENDING_CAMPUS`.

Trả:

```text
SAFE_EDIT
SUBMIT_AMENDMENT
```

nếu còn cutoff.

## 44.3. Rejected campus

Trả:

```text
RESUBMIT_INSTANCE
```

theo policy.

## 44.4. Host

Sau approval Current Host có:

```text
APPROVE_AMENDMENT
REJECT_AMENDMENT
```

Không cấp hai action này cho Staff Leader chỉ vì role Leader.

---

# 45. Whole-request pending edit

Giữ `EditPendingRequest` chỉ khi toàn bộ request còn ở trạng thái phù hợp.

Nhưng handler phải bỏ khả năng topology mutation.

Whole-request edit được dùng để sửa:

- shared requester fields hợp lệ;
- content của các campus đang pending;
- schedule của các campus đang pending;
- members;
- notes;
- các field được phép khác.

Không được:

```text
add campus
remove campus
replace campus
```

---

# 46. Frontend — màn detail

Campus card phải render action theo capability backend.

Ví dụ:

```text
WAITING_REQUEST_APPROVAL
→ [Sửa thông tin cơ sở]
```

```text
ASSIGNED / BEFORE_VISIT
→ [Sửa nhanh]
→ [Đề xuất thay đổi]
```

```text
REJECTED
→ [Sửa và gửi lại]
```

Host:

```text
PENDING AMENDMENT
→ [Duyệt]
→ [Từ chối]
```

Không tự suy status nếu backend đã trả capability.

---

# 47. Frontend — self approval

Nếu backend cho biết user đồng thời là requester-side + Current Host:

- không cần hiển thị bước `Gửi đề xuất` rồi `Duyệt`;
- có thể hiển thị `Cập nhật`;
- backend vẫn tạo amendment + auto approve.

Frontend không được tự fake apply trực tiếp.

Backend là nơi quyết định self-approval.

---

# 48. Frontend — Staff Leader override 72h

Nếu Staff Leader sửa schedule `<72h`:

1. FE phát hiện và có thể cảnh báo sớm;
2. khi submit phải gửi request;
3. nếu backend trả `confirmation required`, mở confirm modal;
4. user confirm;
5. resubmit với `overrideLeadTimeConfirmed=true`.

Không coi frontend check là authorization.

---

# 49. Error handling cần có

Rà convention hiện tại và reuse error pattern.

Tối thiểu cần phân biệt:

```text
CAMPUS_SET_IMMUTABLE
PENDING_CAMPUS_NOT_EDITABLE
LEAD_TIME_VIOLATION
LEAD_TIME_OVERRIDE_CONFIRMATION_REQUIRED
NOT_CURRENT_HOST
AMENDMENT_NOT_PENDING
AMENDMENT_STALE_REVISION
VISIT_MUTATION_CUTOFF_REACHED
VISIT_ALREADY_STARTED
FORBIDDEN_INSTANCE_SCOPE
```

Không nhất thiết tạo đúng tên trên nếu project đã có code tương đương.

Frontend phải map error đúng field/form/action.

Không dùng generic toast cho lỗi validation cụ thể.

---

# 50. Notifications

Rà notifier hiện tại và sửa scope.

## 50.1. EditPendingCampus

Sau khi Registrant / Contact sửa pending campus:

- notify Staff Leader của target campus nếu policy hiện tại có thông báo edit;
- không notify sibling Staff Leader không liên quan;
- không gửi thông báo kiểu request-wide nếu chỉ một campus thay đổi.

## 50.2. Amendment

Submit amendment:

- notify Current Host của target instance.

Không notify Staff Leader như approval authority sau khi campus đã được giao Host, trừ khi họ cũng là Host.

## 50.3. Amendment decision

Approve/reject:

- notify requester phù hợp;
- scope target campus.

## 50.4. Self-approval

Không gửi mail kiểu:

```text
Bạn có một amendment đang chờ chính bạn duyệt
```

Có thể gửi confirmation/history notification nếu product hiện tại cần, nhưng không tạo notification thừa.

---

# 51. Audit / history

Mọi action quan trọng phải audit.

Tối thiểu:

```text
PENDING_CAMPUS_EDITED
CAMPUS_APPROVED
CAMPUS_REJECTED
HOST_ASSIGNED
HOST_TRANSFERRED
AMENDMENT_SUBMITTED
AMENDMENT_APPROVED
AMENDMENT_REJECTED
AMENDMENT_SELF_APPROVED
LEAD_TIME_OVERRIDE
INSTANCE_RESUBMITTED
```

Không nhất thiết tạo event name mới nếu hệ thống hiện có event tương đương.

History phải cho biết:

- actor;
- relation/source;
- target campus;
- old/new revision;
- decision;
- thời điểm.

---

# 52. Không yêu cầu tạo bảng mới

Mặc định implementation này **không cần thêm table mới**.

Phải reuse:

- visit request;
- visit instance;
- form details;
- amendment tables;
- revision history;
- audit/event infrastructure;
- current Host field;
- contact identity workflow.

Chỉ tạo migration nếu code inspection chứng minh schema hiện tại không thể biểu diễn rule bắt buộc.

Không được tự ý thêm table chỉ để làm self-approval hoặc override flag.

---

# 53. Backend implementation order

Khuyến nghị làm theo thứ tự:

## Phase 1 — Domain policy

1. thêm/chuẩn hóa action `EditPendingCampus`;
2. sửa permission amendment decision từ Staff Leader → Current Host;
3. bỏ privacy cutoff bypass;
4. tách `MinScheduleLeadHours=72` và `MutationCutoffHours=6`;
5. sửa schedule amendment: không dùng 72h sau approval;
6. thêm Staff Leader override pre-approval.

## Phase 2 — Request handlers/services

1. sửa whole-request pending edit bỏ add/remove campus;
2. tạo per-instance pending edit handler/service;
3. enforce target-only revision;
4. sửa amendment submit;
5. sửa amendment approve/reject;
6. thêm self-approval;
7. giữ host invariant trong approve;
8. audit lead-time override.

## Phase 3 — Read capabilities

1. `EDIT_PENDING_CAMPUS`;
2. Host amendment decision actions;
3. bỏ Staff Leader amendment action sau approval;
4. requester/contact per-instance capabilities;
5. verify HO read-only.

## Phase 4 — API

1. endpoint per-campus pending edit;
2. DTO + rowVersion;
3. override confirmation flag;
4. error contracts;
5. controller authorization docs.

## Phase 5 — Frontend

1. action constants;
2. campus card actions;
3. per-campus pending edit;
4. remove Add/Remove campus;
5. Host amendment decision;
6. self-approval UX;
7. Staff Leader override confirm;
8. error mapping;
9. translation.

## Phase 6 — Tests

Unit + integration + FE tests + regression gates.

---

# 54. API contract đề xuất — EditPendingCampus

Ví dụ:

```http
PUT /api/v2/visit-requests/{requestId}/instances/{instanceId}/pending-edit
```

Body conceptual:

```json
{
  "rowVersion": 12,
  "formRevision": 5,
  "plannedStartAt": "2026-08-20T09:00:00+07:00",
  "plannedEndAt": "2026-08-20T10:00:00+07:00",
  "purpose": "...",
  "workingContent": "...",
  "workingLanguage": "...",
  "notes": "...",
  "guestMembers": [],
  "supportMembers": [],
  "overrideLeadTimeConfirmed": false
}
```

Không nhất thiết copy đúng DTO trên.

Reuse DTO/value objects hiện tại tối đa.

Không include:

```text
CampusId mới
RemoveCampusIds
AddCampusIds
ReplaceCampus
```

---

# 55. API contract — self approval

Ưu tiên không tạo endpoint self-approve riêng nếu service hiện tại có thể quyết định trong submit amendment.

Pseudo:

```csharp
var requesterSide = isRegistrant || isOperationalContact;
var canDecide = isCurrentHost;

var amendment = CreatePendingAmendment(...);

if (requesterSide && canDecide)
{
    ApproveAmendment(amendment, actorId, selfApproval: true);
}

await transaction.CommitAsync();
```

Cần cùng transaction.

---

# 56. API contract — Staff Leader override

Không tạo endpoint đặc biệt nếu không cần.

Dùng cùng edit/save request với transient flag:

```json
{
  "overrideLeadTimeConfirmed": true
}
```

Backend chỉ honor flag nếu:

```text
actor == Staff Leader của target campus
```

User khác gửi `true` vẫn reject.

---

# 57. Transaction boundaries

Các action sau phải atomic:

```text
Save + Approve
Create Amendment + Self Approve + Apply
Approve + Assign Host
Host Transfer
Approve Amendment + Apply Form Revision
```

Không được để trạng thái nửa chừng.

---

# 58. Concurrency

Phải giữ optimistic concurrency.

Test bắt buộc:

- stale request rowVersion;
- stale instance rowVersion;
- stale form revision;
- stale amendment base revision;
- Host đã transfer trước lúc approve;
- two users approve cùng amendment;
- edit pending campus song song.

Expected:

```text
409 Conflict
```

hoặc project-specific concurrency response hiện tại.

---

# 59. Security / IDOR

Mọi per-instance endpoint phải verify:

```text
instance.VisitRequestId == requestId
```

Không cho:

```text
request A + instance B
```

để truy cập campus ngoài scope.

Contact phải match đúng instance.

Staff Leader phải match campus của instance.

Host phải match Current Host của instance.

---

# 60. Aggregate request status

Sau target-only mutation phải recompute aggregate bằng service canonical hiện tại.

Không set request status bằng tay nếu đã có aggregate service.

Ví dụ:

```text
HN ASSIGNED
HCM WAITING
```

→ request:

```text
PARTIALLY_APPROVED
```

Edit HCM xong vẫn:

```text
PARTIALLY_APPROVED
```

Nếu DN rejected/resubmit thì aggregate dựa trên canonical service hiện tại.

---

# 61. Canonical projection / fingerprint

Nếu per-campus form v2 hiện có canonical projection/fingerprint:

- sau target form edit;
- amendment apply;
- resubmit;

phải update đúng service hiện tại.

Không copy logic fingerprint thủ công.

Reuse canonical projection service.

---

# 62. Test matrix — permissions

Phải có ít nhất:

### Registrant

```text
Registrant xem tất cả campus ✅
Registrant edit pending campus ✅
Registrant approve campus ❌
Registrant approve amendment nếu không phải Host ❌
```

### Contact

```text
Contact HN edit HN ✅
Contact HN edit HCM ❌
Contact HN submit amendment HN ✅
Contact HN approve amendment ❌ nếu không phải Host
```

### Staff

```text
STAFF không relation → mutation ❌
STAFF + Registrant → quyền Registrant ✅
```

### Staff Leader

```text
Leader HN approve HN ✅
Leader HN approve HCM ❌
Leader HN approve amendment sau assignment ❌ nếu không phải Host
```

### Host

```text
Host HN approve amendment HN ✅
Host HN approve amendment HCM ❌
```

### HO

```text
View ✅
Mutation ❌
```

---

# 63. Test matrix — mixed-state request

Case:

```text
HN ASSIGNED
HCM WAITING_REQUEST_APPROVAL
DN REJECTED
```

Expected:

```text
Whole EditPendingRequest ❌
EditPendingCampus HCM ✅
SafeEdit HN ✅ nếu cutoff
Amendment HN ✅ nếu cutoff
Resubmit DN ✅
```

Sửa HCM:

```text
HN unchanged
DN unchanged
```

---

# 64. Test matrix — campus immutability

Sau create:

```text
Add campus → reject
Remove campus → reject
Replace campus → reject
```

Whole pending edit payload thiếu một campus:

```text
reject
```

Payload có campus mới:

```text
reject
```

Frontend edit:

```text
không có Add/Remove buttons
```

---

# 65. Test matrix — thời gian trước approval

## Create

```text
start < now+72h → reject
start == now+72h → allow
start > now+72h → allow
```

## Pending edit không đổi schedule

```text
current start chỉ còn 24h
edit content → allow
```

## Registrant đổi schedule

```text
newStart < now+72h → reject
newStart >= now+72h → allow
```

## Staff Leader đổi schedule

```text
newStart <72h
no confirm → confirmation required

newStart <72h
confirmed → allow
```

## Staff Leader approve lịch cũ

```text
currentStart < now+72h
schedule unchanged
→ allow
```

---

# 66. Test matrix — sau approval

Current:

```text
start = now + 10h
```

Expected:

```text
Safe Edit ✅
Amendment ✅
```

Current:

```text
start = now + 5h59m
```

Expected:

```text
Safe Edit ❌
Amendment ❌
```

Không có privacy bypass.

---

# 67. Test matrix — schedule amendment

Current start:

```text
now + 12h
```

Proposal:

```text
newStart = now + 2h
```

Expected:

```text
Submit amendment ✅
```

vì sau approval không áp 72h.

Host approve khi proposal vẫn future:

```text
✅
```

Host approve khi proposed start đã qua:

```text
❌
```

---

# 68. Test matrix — self approval

```text
Registrant == CurrentHost
```

Submit approval-sensitive change:

```text
amendment created
status = APPROVED
requested_by = actor
decided_by = actor
form applied
history created
```

Không tồn tại pending amendment chờ chính user đó.

Tương tự:

```text
OperationalContact == CurrentHost
```

---

# 69. Test matrix — Host transfer

1. A là Host.
2. amendment pending.
3. transfer Host A → B.
4. A approve:

```text
❌
```

5. B approve:

```text
✅
```

---

# 70. Test matrix — Save & Approve

User:

```text
Registrant + Staff Leader
```

Campus:

```text
WAITING_REQUEST_APPROVAL
```

`Lưu`:

```text
form updated
status vẫn WAITING_REQUEST_APPROVAL
```

`Lưu và duyệt`:

```text
form updated
Host assigned
status ASSIGNED
audit edit + approve
```

Nếu assign Host fail:

```text
toàn transaction rollback
```

---

# 71. Test matrix — sibling isolation

Request:

```text
HN ASSIGNED
HCM WAITING
```

Edit HCM.

Assert HN:

```text
status unchanged
host unchanged
decided_by unchanged
decided_at unchanged
rowVersion unchanged
formRevision unchanged
approvalRevision unchanged
```

Đây là regression gate bắt buộc.

---

# 72. Frontend acceptance criteria

## Detail page

- capability-driven;
- không tự suy quyền sai;
- per-campus actions rõ ràng;
- không hiện Add/Remove campus sau create.

## Pending edit

- mixed request chỉ mở đúng campus pending;
- không kéo sibling vào payload;
- schedule validation đúng role;
- Staff Leader override có confirm.

## Amendment

- requester/contact submit;
- Host decision;
- requester+Host không phải tự approve bằng UI hai bước;
- error stale/concurrency hiển thị rõ.

## i18n

Không để sót text VI khi EN.

Tất cả message mới cần VI/EN.

---

# 73. Không được làm

Không:

```text
reset toàn request khi một campus thay đổi
```

Không:

```text
dùng request aggregate status để thay thế instance authorization
```

Không:

```text
cho Staff Leader duyệt amendment sau approval chỉ vì role Leader
```

Không:

```text
cho Host sửa sibling campus
```

Không:

```text
cho add/remove campus sau create
```

Không:

```text
hard delete campus trong edit
```

Không:

```text
bypass 6h theo privacy/media
```

Không:

```text
áp 72h cho schedule amendment sau approval
```

Không:

```text
bỏ amendment history trong self-approval
```

Không:

```text
frontend-only authorization
```

---

# 74. Migration / database checklist

Trước khi tạo migration, kiểm tra:

- Host field đã tồn tại;
- amendment requested/decided fields đã tồn tại;
- revision history đã tồn tại;
- audit metadata đã đủ;
- action/capability là code constant hay DB enum;
- error codes không cần schema.

Mục tiêu:

```text
0 table mới
0 migration nếu không thực sự cần
```

Nếu MySQL ENUM / CHECK hiện tại chứa action/status cố định thì chỉ migration tối thiểu khi bắt buộc.

Không đổi schema rộng ngoài scope.

---

# 75. Suggested code structure

Ưu tiên reuse service hiện tại.

Ví dụ conceptual:

```text
VisitMutationPolicy
├── CanEditPendingRequest
├── CanEditPendingCampus
├── CanSafeEdit
├── CanSubmitAmendment
├── CanDecideAmendment
└── ValidateCutoff
```

Authorization relation:

```text
VisitActorRelationResolver
hoặc reuse helper hiện có
```

Không nhất thiết tạo class mới nếu project đã có ownership/permission helper.

Mục tiêu là **centralize rule**, không duplicate trong controller/handler/FE.

---

# 76. Definition of Done

Chỉ coi hoàn thành khi:

## Backend

- per-campus pending edit tồn tại;
- no dead-end mixed request;
- no add/remove campus after create;
- Host quyết định amendment;
- self-approval chạy atomic;
- Staff Leader override 72h có backend enforcement;
- amendment schedule không còn dùng 72h;
- privacy cutoff exception bị loại;
- sibling isolation đúng;
- aggregate đúng;
- audit đúng.

## Frontend

- action theo capability;
- per-campus edit UX;
- Host decision UX;
- self-approval UX;
- Staff Leader confirm override;
- Add/Remove campus bị loại khỏi Edit;
- i18n đầy đủ;
- lỗi validation không hiện toast mơ hồ.

## Tests

- unit green;
- integration green;
- FE unit green;
- build/typecheck/lint green;
- regression mixed-state green;
- concurrency green;
- authorization/IDOR green.

---

# 77. Kết quả business cuối cùng

Business rule chính thức:

```text
1. Campus được chọn lúc Create và bất biến sau khi request được tạo.

2. Registrant xem toàn request và sửa dữ liệu phía requester.

3. Operational Contact chỉ sửa campus được gán.

4. Multi-campus xử lý độc lập từng campus.

5. Pending campus trong mixed request phải có EditPendingCampus.

6. Rejected campus dùng ResubmitInstance.

7. Approved campus dùng Safe Edit / Amendment.

8. Trước approval:
   Staff Leader của đúng campus là approval authority.

9. Approve bắt buộc gán Host.

10. Sau approval:
    Current Host là người chủ quản và quyết định amendment.

11. Nếu requester đồng thời là Current Host:
    amendment được auto-approve nhưng vẫn lưu history/audit.

12. Staff Leader + Registrant trước approval:
    có Lưu và Lưu & Duyệt.

13. 72h:
    bắt buộc với Create và schedule mới do Registrant/Contact thay trước approval.

14. Staff Leader:
    được override <72h trước approval sau confirm + audit.

15. Staff Leader approve lịch cũ:
    không recheck 72h.

16. Sau approval:
    không áp 72h cho amendment.

17. Sau approval:
    Safe Edit + Amendment dùng cutoff chung 6h.

18. Schedule amendment:
    proposedStart chỉ cần ở tương lai, end > start, duration >= 30 phút.

19. Không có privacy/media cutoff exception.

20. Không Add / Remove / Replace campus sau khi request được tạo.

21. Muốn thêm campus:
    tạo request mới.

22. Không một mutation của campus A được reset status/host/decision/revision của campus B.
```

---

# 78. Yêu cầu cho AI Agent khi thực hiện

1. Checkout / inspect đúng branch target mới nhất trước khi sửa.
2. Không dựa vào tên file trong tài liệu nếu repo đã đổi path; search symbol thực tế.
3. Đọc implementation hiện tại trước khi thêm abstraction mới.
4. Reuse policy/service hiện có.
5. Không tạo bảng mới nếu không bắt buộc.
6. Không đổi schema ngoài scope.
7. Mỗi phase phải có test chứng minh behavior.
8. Sau khi sửa phải báo:
   - file thay đổi;
   - business rule nào đã implement;
   - test nào thêm;
   - test/build result;
   - phần nào còn nợ.
9. Không tuyên bố hoàn thành nếu mixed-state request vẫn còn dead-end.
10. Không merge/push nếu chưa được yêu cầu riêng.

