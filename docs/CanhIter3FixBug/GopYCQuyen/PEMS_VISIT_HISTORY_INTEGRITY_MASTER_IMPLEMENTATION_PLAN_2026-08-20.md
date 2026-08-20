# PEMS — MASTER IMPLEMENTATION PLAN
## Visit Request History Integrity, Revision Safety, Contact History, Decision Preservation & Lifecycle Audit

**Repository reviewed:** `quangthoai04/PEMS`  
**Baseline reviewed:** `Dev @ e62f9e915d0efd02931512f35fe4c896824ead58`  
**Plan date:** `2026-08-20`  
**Plan purpose:** Kế hoạch triển khai chỉnh sửa lịch sử đơn theo hướng an toàn, đầy đủ, không làm lệch logic hiện hữu, không để thay đổi một phần gây hỏng luồng khác.

---

# 1. MỤC TIÊU

Mục tiêu của đợt chỉnh sửa này là bảo đảm rằng các thay đổi nghiệp vụ quan trọng của một Visit Request được lưu lại đầy đủ, bất biến, có thể truy vết và hiển thị đúng trong “Lịch sử đơn”, bao gồm:

- chỉnh sửa nội dung đơn;
- thay đổi lịch / thời gian;
- sửa nhanh các trường cho phép;
- gửi lại đơn sau khi bị từ chối;
- quyết định duyệt / từ chối của từng campus;
- đổi / sửa đầu mối vận hành;
- chuyển giao đầu mối;
- amendment;
- chuyển giao Host;
- hủy đơn / hủy campus;
- các bước lifecycle nếu sản phẩm xác định đây là business history.

Đồng thời phải giữ nguyên các invariants hiện tại của PEMS:

- Pure V2;
- phân quyền theo request / campus / host / operational contact;
- optimistic concurrency;
- transaction boundary;
- notification sau commit ở các flow hiện có;
- campus isolation;
- contact confirmation gate;
- immutable campus set trong edit/resubmit;
- amendment lifecycle;
- revision semantics.

---

# 2. KẾT LUẬN KIẾN TRÚC

## 2.1. Không tạo một bảng history tổng mới trong đợt fix này

PEMS hiện đã có đủ các nguồn lưu lịch sử chuyên biệt:

1. `visit_instance_form_revision_history`
2. `visit_request_revision_history`
3. `audit_logs`
4. `audit_log_changes`
5. `visit_request_identity_change_events`
6. `visit_instance_amendments`
7. amendment change rows
8. các immutable audit rows của approval / rejection / host transfer

Vấn đề hiện tại là writer và reader chưa thống nhất.

Một số mutation đã lưu audit nhưng timeline không đọc.  
Một số mutation đáng ra phải tạo revision nhưng chỉ lưu audit.  
Một số history entry lại được dựng từ current state, khiến quá khứ có thể biến mất sau mutation tiếp theo.

## 2.2. Kiến trúc đích

```text
Business mutation
    ↓
Authorization + lifecycle guard
    ↓
Concurrency lock / row-version check
    ↓
Capture immutable BEFORE state
    ↓
Mutate current state
    ↓
Append immutable history source
    ↓
SaveChanges inside same transaction
    ↓
Commit
    ↓
Post-commit notification if required
```

History reader:

```text
Resolve visibility scope FIRST
    ↓
Read immutable event source
    ↓
Whitelist business-safe event types
    ↓
Map to structured history event
    ↓
Return only data allowed for that viewer
```

## 2.3. Quy tắc quan trọng nhất

> Không được dựng lại một sự kiện quá khứ bằng cách đọc trạng thái hiện tại của `visit_requests` hoặc `visit_request_campuses`.

Current state chỉ mô tả “bây giờ”.

History phải mô tả “đã từng xảy ra”.

---

# 3. PHẠM VI FILE DỰ KIẾN BỊ ẢNH HƯỞNG

## Backend — mutation writers

```text
backend/PEMS.Infrastructure/Services/VisitRequestV2EditService.cs
backend/PEMS.Infrastructure/Services/VisitSafeEditService.cs
backend/PEMS.Infrastructure/Services/VisitRevisionBaselineGuard.cs
backend/PEMS.Infrastructure/Services/VisitFormRevisionSnapshotBuilder.cs
backend/PEMS.Application/Delegations/Services/CampusApprovalExecutor.cs
backend/PEMS.Application/Delegations/Commands/RejectCampusInstance/RejectCampusInstanceCommandHandler.cs
backend/PEMS.Application/Delegations/Commands/OperationalContact/UpdateOperationalContactProfileCommandHandler.cs
backend/PEMS.Application/Delegations/Commands/OperationalContact/ReplaceOperationalContactCommandHandler.cs
backend/PEMS.Application/Delegations/Commands/CompleteVisitStage/CompleteVisitStageCommandHandler.cs
```

Có thể cần kiểm tra thêm các writer khác nếu search phát hiện action tương tự.

## Backend — history readers/contracts

```text
backend/PEMS.Application/Delegations/Commands/VisitAmendments/GetVisitRequestHistoryQueryHandler.cs
backend/PEMS.Application/Delegations/Commands/VisitAmendments/GetVisitHistoryDetailQueryHandler.cs
backend/PEMS.Application/Delegations/Commands/VisitAmendments/VisitAmendmentCommandContracts.cs
```

## Tests

Ít nhất:

```text
tests/PEMS.IntegrationTests/VisitRequests/VisitRequestHistoryV2Tests.cs
tests/PEMS.IntegrationTests/VisitRequests/UpdatePendingVisitInstanceV2ServiceTests.cs
tests/PEMS.IntegrationTests/VisitRequests/ResubmitRejectedVisitRequestV2ServiceTests.cs
tests/PEMS.IntegrationTests/VisitRequests/VisitSafeEditV2Tests.cs
tests/PEMS.IntegrationTests/VisitRequests/OperationalContactManagementTests.cs
tests/PEMS.IntegrationTests/VisitRequests/CampusApprovalDecisionV2Tests.cs
```

Nếu lifecycle được đưa vào history:

```text
CompleteVisitStage related integration tests
```

---

# 4. INVARIANTS KHÔNG ĐƯỢC PHÁ

Đây là phần bắt buộc phải đọc trước khi sửa code.

## 4.1. Transaction integrity

Một mutation thay đổi business state và history của mutation đó phải commit cùng transaction.

Không được:

```text
commit business change
→ sau đó mới cố ghi history
```

Nếu ghi history lỗi mà business state đã commit thì hệ thống sẽ lại rơi vào trạng thái “có thay đổi nhưng không có lịch sử”.

## 4.2. Notification không được kéo ngược business transaction

Các flow đang gửi notification sau commit phải giữ nguyên.

Không đưa notification vào transaction chỉ vì đang refactor history.

## 4.3. Campus isolation

Thay đổi một campus không được:

- bump revision campus khác;
- đổi row version campus khác;
- reset decision campus khác;
- thay member campus khác;
- đưa identity event campus khác vào viewer hiện tại.

## 4.4. Contact identity và contact profile là hai khái niệm khác nhau

Contact profile:

```text
name
organization
job title
phone
```

Contact identity:

```text
email
operational_contact_user_id
confirmation
transfer
invitation
```

Không được vô tình làm profile correction:

- reset confirmation;
- gửi invitation;
- đổi user relation;
- đóng contact gate.

## 4.5. Revision phải phản ánh nội dung snapshot thật

Nếu revision snapshot chứa:

- form content;
- schedule;
- members;

thì bất kỳ mutation nào làm thay đổi một trong các thành phần đó phải được xem xét xem có cần bump revision hay không.

Không được tạo revision snapshot với dữ liệu members rỗng chỉ vì mutation chỉ đổi schedule.

## 4.6. Baseline recovery không phải user action

`RECOVERED_BASELINE` chỉ là technical baseline.

Không hiển thị nó như một business event.

## 4.7. Authorization phải resolve trước khi đọc event details

History detail endpoint không được trở thành backdoor để đoán event id của campus khác.

Out-of-scope event nên tiếp tục trả kiểu “not found” theo pattern hiện có.

---

# 5. FIX GROUP A — SCHEDULE-ONLY EDIT PHẢI TẠO REVISION ĐÚNG

## 5.1. Lỗi hiện tại

Trong pending edit:

```text
contentChanged
scheduleChanged
```

Code đã gọi baseline guard nếu một trong hai thay đổi.

Nhưng revision row mới chỉ được append khi:

```text
contentChanged == true
```

Do đó:

```text
scheduleChanged = true
contentChanged = false
```

sẽ:

- đổi schedule;
- ghi audit;
- bump instance row version;
- nhưng không tạo revision snapshot mới.

Trong khi snapshot builder đã chứa:

```text
PlannedStartAt
PlannedEndAt
```

=> revision chain không phản ánh đầy đủ state của form.

## 5.2. Quyết định fix

Định nghĩa:

```csharp
var revisionChanged = contentChanged || scheduleChanged;
```

Nếu `revisionChanged`:

- baseline phải tồn tại trước mutation;
- bump `FormRevision`;
- append `VisitInstanceFormRevisionHistory`;
- snapshot phải chứa state sau mutation;
- member list phải là member thực tế của campus.

## 5.3. Cẩn thận với `ApplyFormDetail`

Hiện `ApplyFormDetail()` tự bump:

```text
detail.FormRevision
detail.RowVersion
```

Nếu schedule-only không gọi `ApplyFormDetail`, cần tránh duplicate logic.

Khuyến nghị không sửa vội `ApplyFormDetail()` để giảm blast radius.

Thay vào đó:

```text
if contentChanged:
    ApplyFormDetail(...)

else if scheduleChanged:
    detail.FormRevision += 1
    detail.RowVersion += 1
    detail.UpdatedAt = now
    detail.UpdatedBy = actorId
```

Sau đó append revision cho cả hai trường hợp.

## 5.4. Members snapshot

Schedule-only không được sử dụng:

```csharp
new List<VisitGuestMember>()
```

để build revision.

Phải dùng current members:

```csharp
V2CanonicalRefresh.MembersOf(request, instance)
```

hoặc equivalent persisted member reader bảo đảm đúng display order.

Nếu contentChanged thì dùng newly staged/relinked rows sau flush.

Nếu schedule-only thì dùng members hiện tại.

## 5.5. Whole-request pending edit

Cấu trúc staging nên đổi từ:

```text
changedInstances = content changed instances
```

thành một structure rõ nghĩa hơn:

```text
revisionInstances
```

mỗi item có:

```text
Instance
Content
ContentChanged
ScheduleChanged
NewMembers optional
```

Sau flush:

```text
if ContentChanged:
   LinkMembers(...)
   snapshotMembers = newMembers

else:
   snapshotMembers = current members
```

Sau đó append revision.

## 5.6. Per-campus pending edit

Áp dụng cùng invariant.

Hiện flow chỉ append revision trong:

```csharp
if (contentChanged)
```

Phải chuyển sang:

```text
if contentChanged || scheduleChanged
```

và bảo đảm snapshot members không bị rỗng.

## 5.7. Không thay đổi

Không thay đổi:

- campus set immutability;
- schedule validation;
- lead-time override logic;
- contact snapshot guard;
- request aggregate recompute;
- optimistic concurrency;
- partner-link recompute.

## 5.8. Test bắt buộc

### Case A1

```text
Whole request pending edit
only PlannedStartAt changes
```

Assert:

- form revision N → N+1;
- một revision history mới;
- snapshot start/end mới;
- members unchanged;
- content fields unchanged;
- sibling untouched.

### Case A2

```text
Only PlannedEndAt changes
```

same assertions.

### Case A3

```text
Per-campus pending edit
schedule only
```

Assert:

- target campus revision +1;
- sibling revision unchanged;
- target row version bumped;
- sibling row version unchanged.

### Case A4

```text
content + schedule changed together
```

Chỉ được tạo **một revision N+1**, không hai revision.

### Case A5

Legacy request không có revision baseline.

Assert:

```text
baseline N
then edit N+1
```

không duplicate revision.

---

# 6. FIX GROUP B — QUYẾT ĐỊNH DUYỆT / TỪ CHỐI PHẢI LÀ APPEND-ONLY HISTORY

## 6.1. Lỗi hiện tại

Timeline hiện đọc decision từ current campus row:

```text
DecidedAt
DecidedBy
DecisionNote
Status
```

Sau resubmit, code chủ động clear:

```text
DecisionActorRole
DecisionSource
DecidedBy
DecidedAt
DecisionNote
CurrentHostUserId
...
```

Do đó quyết định cũ biến mất khỏi timeline.

## 6.2. Không được fix bằng cách “không clear decision”

Không được giữ decision cũ trên current row.

DB/business model cần current row quay về:

```text
WAITING_REQUEST_APPROVAL
```

và không mang decision stale.

History phải tách khỏi current state.

## 6.3. Source of truth mới cho decision history

Sử dụng immutable decision audit rows.

Approval đã tạo audit.

Rejection đã tạo audit.

Cần chuẩn hóa các audit đó đủ dữ liệu để business history đọc được độc lập.

## 6.4. Approval audit phải capture

Tối thiểu:

```text
visit_request_campuses.status:
WAITING_REQUEST_APPROVAL -> ASSIGNED

decision_note:
null -> <note>

current_host_user_id:
null -> <host user id>
```

Nếu cần display host name, không nên lưu name như source of truth nếu có thể resolve user id khi đọc.

Tuy nhiên history detail có thể hiển thị name tại thời điểm đọc.

Nếu business yêu cầu “tên cũ bất biến kể cả user đổi tên sau này” thì phải snapshot display name riêng. Đây là quyết định sản phẩm khác, không tự thêm trong patch này.

## 6.5. Rejection audit phải capture

```text
visit_request_campuses.status:
WAITING_REQUEST_APPROVAL -> REJECTED

decision_note:
null -> <reason>
```

Reason hiện nằm ở current row và notification; muốn history tồn tại sau resubmit thì immutable audit phải có nó.

## 6.6. Action constants

Không nên rải string literal mới khắp code.

Nên đưa decision actions vào constants class phù hợp, ví dụ:

```text
CampusApproved
CampusApprovedWithScheduleWarning
CampusRejected
```

Nếu hiện có canonical constants rồi thì reuse.

## 6.7. History reader

Thay block:

```text
foreach current CampusInstances:
    if DecidedAt ...
```

bằng query audit.

Pseudo:

```csharp
var decisionAudits = await _db.AuditLogs
    .AsNoTracking()
    .Where(a =>
        a.VisitRequestId == requestId &&
        a.VisitInstanceId != null &&
        visibleInstanceIds.Contains(a.VisitInstanceId.Value) &&
        DecisionActions.Contains(a.Action))
```

Map:

```text
approval action -> INSTANCE_APPROVED
rejection action -> INSTANCE_REJECTED
```

`At = audit.CreatedAt`

`Actor = audit.ActorUserId`

`VisitInstanceId = audit.VisitInstanceId`

`Reason = decision_note audit change`

## 6.8. Legacy fallback

Không được làm mất history của đơn cũ chưa có chuẩn audit mới.

Có thể dùng fallback:

```text
if campus current decision exists
AND no immutable decision audit represents it
→ render current-row decision
```

Nhưng fallback chỉ dành cho legacy.

Không được merge audit event + current event thành duplicate.

Cần dedupe rõ ràng theo instance/current decision.

## 6.9. Resubmit

Resubmit hiện snapshot quyết định cũ vào:

```text
campus_decisions_before_resubmit_json
campus_decision_before_resubmit_json
```

Giữ nguyên.

Không xóa.

Sau khi decision audit reader hoạt động, snapshot này chủ yếu là recovery / migration safety net.

## 6.10. Test chuỗi bắt buộc

### B1

```text
reject
→ history contains rejection
```

### B2

```text
reject
→ resubmit
→ history STILL contains old rejection
```

### B3

```text
reject
→ resubmit
→ approve
```

Timeline phải có thứ tự:

```text
approve
resubmit
reject
```

hoặc descending timestamp equivalent.

### B4

Hai lần reject qua hai vòng resubmit:

```text
reject #1
resubmit
reject #2
```

Phải có hai rejection event riêng.

### B5

Approval sau đó lifecycle progresses:

```text
ASSIGNED
→ BEFORE_VISIT
→ DURING_VISIT
```

Approval event không được đổi thành lifecycle status.

### B6

Multi-campus:

```text
campus A approved
campus B rejected
```

Viewer campus A không được thấy decision campus B nếu scope không cho.

---

# 7. FIX GROUP C — CONTACT PROFILE HISTORY

## 7.1. Hiện trạng

`UpdateOperationalContactProfileCommandHandler` đã tạo:

```text
OPERATIONAL_CONTACT_PROFILE_UPDATED
```

và audit change per field:

```text
operational_contact_full_name
operational_contact_organization
operational_contact_job_title
operational_contact_phone
```

Đây là write-side tốt.

Không nên thêm form revision cho profile correction, vì code hiện định nghĩa form revision là “what campus is asked to host”.

## 7.2. Lỗi

Business history không surface action này.

## 7.3. Fix

Thêm event code:

```text
CONTACT_PROFILE_UPDATED
```

Thêm whitelist action trong timeline reader.

Nhưng chỉ khi:

```text
includeIdentity == true
```

và audit có đúng:

```text
VisitRequestId
VisitInstanceId
CampusId
```

## 7.4. History detail

`GetVisitHistoryDetailQueryHandler.AuditDetailAsync()` hiện chỉ cho HostTransferred.

Refactor thành whitelist mapping.

Ví dụ:

```text
HOST_TRANSFERRED
OPERATIONAL_CONTACT_PROFILE_UPDATED
OPERATIONAL_CONTACT_REPLACED_IMMEDIATE
LIFECYCLE...
```

Không dùng:

```text
if action is any audit action
```

Mỗi action phải có mapper riêng.

## 7.5. Field labels

Map:

```text
operational_contact_full_name
→ operational contact full name

operational_contact_organization
→ organization

operational_contact_job_title
→ job title

operational_contact_phone
→ phone
```

Frontend/i18n dùng label key.

Không trả technical field name trực tiếp nếu UI hiện đang dùng localized labels.

## 7.6. Privacy

Không thêm raw full email vào audit profile update.

Email identity vẫn đi qua identity workflow.

---

# 8. FIX GROUP D — CONTACT REPLACEMENT

## 8.1. Hai nhánh nghiệp vụ khác nhau

Replace contact trước decision có hai outcome:

### External email

```text
old contact replaced
→ operationalContactUserId cleared
→ WAITING_CONTACT_CONFIRMATION
→ invitation created
```

History identity hiện đã có:

```text
OPERATIONAL_CONTACT_INVITATION_CREATED
```

### Registrant verified self-match

```text
new email == verified registrant email
→ immediate link
→ no invitation
→ WAITING_REQUEST_APPROVAL
```

Nhánh này không có invitation event đại diện cho replacement.

## 8.2. Audit hiện có vấn đề scope

`OPERATIONAL_CONTACT_REPLACED` hiện cần được kiểm tra/bổ sung đầy đủ:

```text
CampusId
VisitInstanceId
```

Không được surface audit này trong business history nếu chưa có instance scope.

## 8.3. Không nên surface generic replacement cho cả hai nhánh một cách mù quáng

Nếu external branch hiển thị:

```text
CONTACT_REPLACED
```

và identity branch đồng thời hiển thị:

```text
CONTACT_INITIAL_CONFIRMATION_CREATED
```

UI có thể tạo hai event cho một thao tác.

Phải quyết định semantics rõ.

## 8.4. Khuyến nghị

External branch:

```text
identity timeline remains source of business event
```

Có thể giữ generic audit chỉ cho admin audit.

Self-match branch:

tạo một action/event rõ:

```text
OPERATIONAL_CONTACT_REPLACED_IMMEDIATE
```

hoặc identity event:

```text
OPERATIONAL_CONTACT_REPLACED_WITH_REGISTRANT
```

Khuyến nghị ưu tiên identity event nếu phù hợp model hiện tại.

Lý do:

- đây là thay đổi holder của contact role;
- history visibility của identity đã có sẵn;
- tránh mở thêm generic audit surface.

Nếu chọn AuditLog route thì phải thêm:

```text
CampusId
VisitRequestId
VisitInstanceId
```

và whitelist identity scope.

## 8.5. Không thay đổi

Không thay đổi:

- eligibility logic;
- registrant self-match condition;
- confirmation gate;
- invitation token lifecycle;
- post-commit delivery;
- pending invitation supersede;
- contact member link clearing.

## 8.6. Tests

### D1

Self-match replacement:

```text
one visible replacement event
no invitation-created event
contact confirmed
```

### D2

External replacement:

```text
invitation-created visible
no duplicate replacement line
```

### D3

Existing pending invitation is superseded:

Timeline có:

```text
old invitation superseded
new invitation created
```

theo đúng campus.

### D4

Viewer của campus A không thấy contact history campus B.

---

# 9. FIX GROUP E — SAFE EDIT REQUEST REVISION NUMBER COLLISION

## 9.1. Lỗi

`VisitSafeEditService`:

1. gọi `EnsureRequestBaselineAsync()`;
2. baseline revision 1 có thể chỉ đang staged;
3. sau đó query DB `MAX(request_revision)`;
4. DB chưa thấy staged baseline;
5. next revision cũng bị tính thành 1.

Kết quả có thể collision unique key:

```text
uq_vrrh_request_revision
```

## 9.2. Fix

Không tự query `MaxAsync + 1`.

Dùng duy nhất:

```csharp
VisitRevisionBaselineGuard.NextRequestRevisionAsync(...)
```

## 9.3. Không copy helper logic sang nơi khác

Helper này tồn tại để xử lý:

```text
persisted rows + EF Local staged rows
```

Đây phải trở thành single source of truth.

## 9.4. Code search bắt buộc

Trước merge, search toàn repo:

```text
MaxAsync(...RequestRevision...)
RequestRevision = ... + 1
```

Mọi writer request revision phải được rà.

Nếu có writer nào còn tự tính revision, phải xác minh vì sao.

## 9.5. Tests

### E1

Empty request history:

```text
safe edit request-level field
```

Expected:

```text
revision 1 = RECOVERED_BASELINE
revision 2 = SAFE_EDIT
```

### E2

Existing revisions 1..4:

next = 5.

### E3

Rollback mutation:

baseline không được commit riêng.

---

# 10. FIX GROUP F — LIFECYCLE HISTORY

## 10.1. Cần quyết định business scope trước khi triển khai

Các stage:

```text
ASSIGNED
BEFORE_VISIT
DURING_VISIT
AFTER_VISIT
CLOSED
```

Nếu product requirement là “Lịch sử đơn chỉ ghi thay đổi nội dung / quyết định”, có thể không surface toàn bộ lifecycle.

Nếu requirement là “mọi thay đổi quan trọng của đơn”, nên surface.

Kế hoạch này chọn phương án:

> Surface operational stage transitions vì chúng là thay đổi business state có actor và timestamp.

## 10.2. Audit hiện tại thiếu scope metadata

`CompleteVisitStageCommandHandler` audit cần bổ sung:

```text
CampusId
VisitRequestId
VisitInstanceId
SourceType = LIFECYCLE
```

## 10.3. Capture old/new status

Trước mutation:

```csharp
var oldStatus = instance.Status;
```

Audit changes:

```text
visit_request_campuses.status
oldStatus -> newStatus
```

## 10.4. Event codes

Nên có:

```text
INSTANCE_STAGE_CHANGED
INSTANCE_CLOSED
```

Nếu muốn chi tiết hơn:

```text
INSTANCE_VISIT_STARTED
INSTANCE_VISIT_COMPLETED
INSTANCE_CLOSED
```

Nhưng không cần quá nhiều event code nếu frontend có `StatusCode`.

## 10.5. History visibility

Lifecycle event chỉ được surface cho actor đã có quyền xem history của instance đó.

Không được dùng lifecycle audit để mở thêm quyền.

## 10.6. News-not-required side effect

`CLOSE_VISIT_INSTANCE` có thể set:

```text
NewsNotRequired = true
```

nếu Host xác nhận.

Cần quyết định xem đây là detail của close event hay một audit riêng.

Khuyến nghị:

- giữ nó như detail của close event;
- không tạo thêm timeline row;
- audit change có thể ghi nếu cần truy vết.

## 10.7. Tests

```text
BEFORE -> DURING
DURING -> AFTER
AFTER -> CLOSED
```

Mỗi transition đúng một event.

Failed transition không được tạo event.

Blocker 409 không được ghi audit/history.

---

# 11. CANCELLATION HISTORY

Hiện cancellation vẫn được dựng từ current request/campus row.

Khác với decision, cancellation thường là terminal và không bị clear bởi resubmit.

Tuy nhiên về kiến trúc dài hạn, vẫn nên chuyển cancellation sang immutable event source nếu có nguy cơ future workflow reopen.

Trong patch hiện tại:

- không bắt buộc đổi cancellation ngay nếu muốn giảm blast radius;
- nhưng phải bổ sung test bảo đảm không mutation nào clear cancellation metadata sau cancel;
- nếu future reopen/cancel-again được thêm thì phải chuyển ngay sang append-only audit.

---

# 12. AMENDMENT — KHÔNG REFACTOR KHÔNG CẦN THIẾT

Amendment hiện là phần tương đối đúng:

```text
proposal rows
immutable change rows
decision
applied revision
```

Không được nhân tiện history refactor mà viết lại amendment architecture.

Chỉ test regression:

- amendment submitted;
- approved;
- rejected;
- withdrawn;
- applied revision;
- scope.

---

# 13. HOST TRANSFER — GIỮ NGUYÊN SEMANTICS

Host transfer đã là audit-only business history hợp lý vì nó thay đổi người phụ trách, không thay đổi form content.

Giữ:

```text
VisitAuditActions.HostTransferred
```

Nhưng khi refactor `AuditDetailAsync()` thành whitelist nhiều action, phải bảo đảm HostTransferred vẫn:

- có same event code;
- same fields;
- same visibility;
- same actor;
- không leak raw user ids.

---

# 14. HISTORY READER REFACTOR

## 14.1. Không query toàn bộ AuditLogs rồi render

Sai:

```text
all audit logs = business history
```

Audit log có cả technical/security plumbing.

Chỉ whitelist.

## 14.2. Suggested mapper structure

Thay vì một block dài:

```csharp
if action == A ...
else if action == B ...
```

nên có explicit sets/functions:

```text
DecisionAuditActions
ContactProfileAuditActions
LifecycleAuditActions
HostTransferAuditActions
```

và mapper:

```text
MapDecisionAudit
MapContactAudit
MapLifecycleAudit
MapHostTransferAudit
```

## 14.3. Event source

Tiếp tục dùng:

```text
VisitHistoryEventSources.Audit
```

với immutable audit id.

Không cần tạo nhiều event-source prefixes mới nếu không cần.

## 14.4. Dedupe

Dedupe phải deterministic.

Không dedupe theo timestamp.

Không dedupe theo message string.

Dùng source identity:

```text
auditLogId
revisionHistoryId
identityChangeEventId
amendmentId + phase
```

Legacy current-state fallback chỉ được emit khi không có immutable event tương ứng.

---

# 15. HISTORY DETAIL REFACTOR

## 15.1. Whitelist action

`AuditDetailAsync` phải reject mọi audit action không được business history surface.

## 15.2. Action-specific field mapping

Ví dụ:

### Host transfer

```text
currentHostName
```

### Contact profile

```text
operational_contact_full_name
operational_contact_organization
operational_contact_job_title
operational_contact_phone
```

### Decision

```text
status
decision_note
host
```

### Lifecycle

```text
status
```

## 15.3. Không expose internal plumbing

Không trả:

```text
correlationId
tokenVersion
raw token
hash
internal reason strings
user ids nếu UI không cần
raw JSON
```

---

# 16. BACKFILL DỮ LIỆU CŨ

Đây là phase riêng.

Không trộn backfill vào runtime fix.

## 16.1. Mục tiêu

Khôi phục những decision cũ đã mất khỏi current row do resubmit.

## 16.2. Nguồn khả dụng

Whole request resubmit audit:

```text
campus_decisions_before_resubmit_json
```

Per-campus resubmit audit:

```text
campus_decision_before_resubmit_json
```

Các snapshot này có:

```text
visitInstanceId
campusId
oldStatus
decidedBy
decidedAt
decisionActorRole
decisionNote
```

## 16.3. Không backfill mù

Script phải:

1. parse JSON;
2. tìm xem decision audit canonical đã tồn tại chưa;
3. chỉ insert nếu thiếu;
4. idempotent;
5. transaction;
6. log số row scanned / inserted / skipped;
7. chạy dry-run trước.

## 16.4. Không dùng `CreatedAt = migration time`

Backfilled event phải dùng:

```text
CreatedAt = original decidedAt
```

nếu audit schema cho phép.

Có thể gắn:

```text
SourceType = HISTORY_BACKFILL
```

để admin biết nguồn.

## 16.5. Không backfill trước runtime reader fix

Thứ tự:

```text
deploy runtime write/read fix
verify
then backfill
```

---

# 17. MIGRATION / DATABASE CHANGE POLICY

Ưu tiên không tạo schema migration nếu không thật sự cần.

Các fix chính có thể thực hiện trên schema hiện có.

Chỉ tạo migration nếu code audit chứng minh thiếu index / field / constraint cần thiết.

Nếu cần index cho history audit query, đánh giá:

```text
VisitRequestId
VisitInstanceId
Action
CreatedAt
```

trước khi thêm.

Không tự thêm index nếu query plan hiện đã dùng index phù hợp.

---

# 18. CONCURRENCY SCENARIOS PHẢI TEST

## 18.1. Two pending edits same campus

Expected:

- one wins;
- second gets 409;
- chỉ một history event mới;
- no duplicate baseline.

## 18.2. Schedule edit vs approval

Nếu approval lock/guard thắng trước:

- edit phải fail theo lifecycle/version;
- không được ghi schedule revision.

Nếu edit thắng:

- approval phải thấy revision mới theo expected row version rules.

## 18.3. Replace contact vs invitation accept

Giữ existing row lock semantics.

History event không được khiến transaction ordering thay đổi.

## 18.4. Resubmit vs another resubmit

Chỉ một commit.

No duplicate request revision.

No duplicate recovered baseline.

## 18.5. Safe edit vs lifecycle transition

Existing mutation policy quyết định.

History addition không thay đổi eligibility.

---

# 19. SECURITY / PRIVACY TEST MATRIX

Actors:

```text
Registrant
Current operational contact
HO
Staff Leader campus A
Staff Leader campus B
Current Host campus A
Support participant
Unrelated user
```

Phải test với request 2 campus.

## 19.1. Registrant

Thấy toàn request theo quyền hiện tại.

## 19.2. Operational contact campus A

Không được mặc định thấy identity detail campus B nếu resolver hiện chỉ cho visible instance scope.

## 19.3. Staff Leader A

Chỉ campus A.

Không identity history nếu existing policy không cho.

## 19.4. Host A

Chỉ instance host.

## 19.5. Unrelated

403 history endpoint.

## 19.6. Guess event id

Event ngoài scope:

```text
not found
```

không leak existence.

---

# 20. REGRESSION MATRIX THEO BUSINESS FLOW

## Flow 1 — Create

Expected history:

```text
request created
instance content created per campus
```

Không thêm duplicate event từ audit.

## Flow 2 — Pending content edit

Expected:

```text
new revision
```

## Flow 3 — Pending schedule-only edit

Expected:

```text
new revision
diff only schedule
members unchanged
```

## Flow 4 — Safe edit instance

Expected:

```text
safe-edit revision
```

## Flow 5 — Safe edit request-level

Expected:

```text
request revision
no revision collision
```

## Flow 6 — Approve

Expected:

```text
immutable approved event
```

## Flow 7 — Reject

Expected:

```text
immutable rejected event
reason preserved
```

## Flow 8 — Reject → resubmit

Expected:

```text
old rejection
resubmit
new active content
```

## Flow 9 — Reject → resubmit → approve

Expected:

```text
old rejection preserved
resubmit
new approval preserved
```

## Flow 10 — Contact profile correction

Expected:

```text
CONTACT_PROFILE_UPDATED
detail diff
identity unchanged
status unchanged
```

## Flow 11 — External contact replace

Expected:

```text
superseded event if applicable
new invitation event
no duplicate generic event
```

## Flow 12 — Self-match contact replace

Expected:

```text
one immediate replacement event
no invitation
```

## Flow 13 — Transfer contact post-decision

Expected:

```text
transfer requested
accepted / declined / expired
```

Existing behavior regression only unless gap found.

## Flow 14 — Amendment

No behavior change.

## Flow 15 — Host transfer

No behavior change except reader refactor regression.

## Flow 16 — Lifecycle

If included:

```text
stage changes append-only
```

## Flow 17 — Cancellation

Existing visible cancellation preserved.

---

# 21. IMPLEMENTATION ORDER

Không sửa tất cả trong một commit.

## Commit 1 — Revision integrity

Scope:

```text
VisitRequestV2EditService
VisitSafeEditService
revision tests
```

Fix:

- schedule-only revision;
- current members snapshot;
- safe-edit NextRequestRevisionAsync.

Không đụng history audit reader trong commit này.

### Merge gate

All revision tests pass.

No behavior regression edit/resubmit.

---

## Commit 2 — Immutable campus decision history

Scope:

```text
CampusApprovalExecutor
RejectCampusInstanceCommandHandler
GetVisitRequestHistoryQueryHandler
GetVisitHistoryDetailQueryHandler if detail needed
history decision tests
resubmit tests
```

Fix:

- decision audits include immutable changes;
- history reads decision audit;
- legacy fallback;
- reject/resubmit history preservation.

### Merge gate

Reject → resubmit → approve scenario must pass.

---

## Commit 3 — Contact history completeness

Scope:

```text
UpdateOperationalContactProfileCommandHandler
ReplaceOperationalContactCommandHandler
history reader/detail
contracts
contact tests
history visibility tests
```

Fix:

- profile update visible;
- replacement self-match visible;
- no external duplicate;
- proper `CampusId` / `VisitInstanceId`;
- identity scope preserved.

### Merge gate

Cross-campus privacy tests pass.

---

## Commit 4 — Lifecycle business history

Scope:

```text
CompleteVisitStageCommandHandler
history reader
contracts
lifecycle tests
```

Chỉ thực hiện nếu product quyết định lifecycle thuộc business history.

### Merge gate

Stage failure produces zero history rows.

---

## Commit 5 — Optional legacy backfill

SQL / migration utility riêng.

Không phụ thuộc rollback của runtime code.

---

# 22. MỖI COMMIT PHẢI CÓ CHECKLIST

Trước commit:

```text
[ ] Re-read changed files completely.
[ ] Check no unrelated files changed.
[ ] Search for duplicate action strings.
[ ] Search for duplicate event codes.
[ ] Verify transaction boundaries.
[ ] Verify SaveChanges locations.
[ ] Verify notification remains post-commit where applicable.
[ ] Verify campus scope metadata on audit.
[ ] Verify no raw email/token leak.
[ ] Verify no current-state reconstruction was introduced.
```

Sau commit:

```text
[ ] Build succeeds.
[ ] Focused integration tests pass.
[ ] Full VisitRequests integration suite passes.
[ ] Architecture tests pass.
[ ] Existing amendment tests pass.
[ ] Existing contact lifecycle tests pass.
[ ] Existing notification tests pass.
```

---

# 23. TESTING STRATEGY

## 23.1. Unit tests không đủ

Các bug chính liên quan:

- EF Local staging;
- transaction;
- unique keys;
- row version;
- DB triggers;
- relational scope;
- actual audit rows.

Phải có integration tests với DB.

## 23.2. Assertion trực tiếp DB

Không chỉ assert API response.

Sau mỗi mutation cần query:

```text
visit_request_revision_history
visit_instance_form_revision_history
audit_logs
audit_log_changes
identity change events
current campus state
```

## 23.3. Sau đó mới gọi history query

Pattern:

```text
Arrange
→ perform mutation
→ assert persisted rows
→ execute GetVisitRequestHistory
→ assert timeline
→ execute history detail
→ assert before/after
```

Điều này giúp phân biệt:

```text
writer bug
reader bug
```

---

# 24. FAILURE / ROLLBACK RULES

## 24.1. History write fails

Business transaction phải rollback.

## 24.2. History read mapping fails

Không được làm mutation fail vì read-side code.

## 24.3. Notification fails after commit

Giữ existing best-effort/recovery semantics.

## 24.4. Backfill fails

Rollback backfill transaction.

Không rollback runtime fix.

---

# 25. OBSERVABILITY SAU DEPLOY

Trong vài ngày đầu cần theo dõi:

```text
duplicate revision unique-key errors
history endpoint 500s
history detail 404 spikes
contact-management 409 changes
resubmit errors
approval/rejection errors
```

Có thể thêm temporary structured logs cho:

```text
history source
visitRequestId
visitInstanceId
event code
auditLogId
revision id
```

Không log raw email/token.

---

# 26. DATA CONSISTENCY CHECK SAU DEPLOY

Chạy query/audit script để tìm:

## 26.1. Instance revision gaps

```text
current form_revision > max(history form_revision)
```

## 26.2. Request revision duplicates/gaps

Theo request.

## 26.3. Resubmitted requests có old decision snapshot nhưng không decision event

Candidate backfill.

## 26.4. Contact profile audit thiếu VisitInstanceId

Để đánh giá legacy visibility/backfill.

---

# 27. KHÔNG ĐƯỢC LÀM DỞ

Không merge nếu xảy ra một trong các tình trạng:

```text
writer đã tạo event mới nhưng reader chưa hiểu
reader đã whitelist action nhưng writer chưa gắn scope
schedule revision đã bump nhưng snapshot members sai
decision reader mới chạy song song current reader gây duplicate
contact profile visible nhưng history detail không enforce identity scope
runtime fix phụ thuộc backfill mới hoạt động
new event code không có frontend/i18n mapping
tests chỉ cover happy path
```

---

# 28. DEFINITION OF DONE

Đợt chỉnh sửa chỉ được coi là hoàn tất khi:

```text
[ ] Schedule-only whole-request edit tạo revision đúng.
[ ] Schedule-only per-campus edit tạo revision đúng.
[ ] Revision snapshot không làm mất member.
[ ] Safe-edit request revision không collision với recovered baseline.
[ ] Approval history đọc từ immutable event.
[ ] Rejection history đọc từ immutable event.
[ ] Rejection vẫn tồn tại sau resubmit.
[ ] Multiple decision cycles đều tồn tại.
[ ] Contact profile correction xuất hiện trong history đúng scope.
[ ] Self-match replacement có history.
[ ] External replacement không duplicate timeline.
[ ] Audit history actions đều có VisitRequestId + VisitInstanceId + CampusId khi cần.
[ ] History detail whitelist action rõ ràng.
[ ] Generic AuditLogs không bị expose.
[ ] Identity privacy giữ nguyên.
[ ] Campus isolation giữ nguyên.
[ ] Amendment không regress.
[ ] Host transfer không regress.
[ ] Lifecycle history hoàn chỉnh nếu được chọn triển khai.
[ ] Focused integration tests pass.
[ ] Full visit integration suite pass.
[ ] Architecture tests pass.
[ ] Build pass.
[ ] Không có migration ngoài kế hoạch.
[ ] Không có unrelated refactor.
```

---

# 29. REVIEW CHECKLIST DÀNH CHO CODE REVIEWER

Reviewer không chỉ xem diff.

Phải truy ngược từng event:

```text
1. Ai được phép tạo event?
2. Guard nào bảo vệ?
3. Row nào bị lock?
4. BEFORE state được capture ở đâu?
5. Current state mutate ở đâu?
6. History row append ở đâu?
7. Cùng transaction không?
8. Viewer nào được đọc?
9. Detail endpoint có same scope không?
10. Event có bị duplicate với source khác không?
11. Mutation sau này có xóa source history không?
12. Legacy row không có source mới thì sao?
```

Nếu không trả lời được đủ 12 câu thì chưa nên approve.

---

# 30. RỦI RO CHÍNH

## R1 — Double increment FormRevision

Khi thêm schedule-only revision rất dễ bump `FormRevision` hai lần nếu vừa gọi `ApplyFormDetail()` vừa có common revision helper.

Mitigation:

```text
one and only one revision increment per business mutation per instance
```

## R2 — Empty members snapshot

Schedule-only dùng `newMembers = []`.

Mitigation:

```text
read current linked members for schedule-only
```

## R3 — Duplicate decision timeline

Audit reader + current-state reader cùng emit.

Mitigation:

```text
immutable audit first, legacy fallback only if no audit
```

## R4 — Cross-campus contact leak

Whitelist `OPERATIONAL_CONTACT_REPLACED` mà audit không có `VisitInstanceId`.

Mitigation:

```text
writer scope metadata first
reader second
```

## R5 — External replace duplicate

Generic replacement audit + identity invitation event.

Mitigation:

```text
define one business event source per outcome
```

## R6 — Legacy backfill invents data

Using migration timestamp / current actor.

Mitigation:

```text
only use original snapshot values
never infer unknown values
```

## R7 — Refactor audit reader opens admin data

Generic audit query.

Mitigation:

```text
strict explicit whitelist
```

---

# 31. KẾ HOẠCH TRIỂN KHAI THỰC TẾ

## Phase 0 — Freeze baseline

Record:

```text
branch
HEAD SHA
schema version
test DB baseline
```

Không bắt đầu patch nếu Dev đã đổi mạnh ở các file mục tiêu mà chưa re-audit.

## Phase 1 — Revision integrity

Deploy/test first.

Không phụ thuộc frontend.

## Phase 2 — Decision immutability

Deploy writer + reader trong cùng release.

Không deploy writer-only hoặc reader-only nếu contract chưa tương thích.

## Phase 3 — Contact history

Deploy writer scope metadata trước/same release với reader whitelist.

## Phase 4 — Lifecycle

Independent, optional.

## Phase 5 — Backfill

Sau khi runtime ổn định.

---

# 32. FINAL ARCHITECTURE TARGET

```text
FORM CONTENT / SCHEDULE / MEMBERS
    ↓
VisitInstanceFormRevisionHistory
    ↓
History timeline
    ↓
Snapshot diff detail


REQUEST REGISTRANT SAFE/PENDING CHANGES
    ↓
VisitRequestRevisionHistory
    ↓
History timeline
    ↓
Snapshot diff detail


CONTACT IDENTITY
    ↓
VisitRequestIdentityChangeEvents
    ↓
Identity-scoped timeline


CONTACT PROFILE
    ↓
Whitelisted AuditLog + AuditLogChange
    ↓
Identity-scoped timeline/detail


CAMPUS APPROVAL / REJECTION
    ↓
Immutable Decision Audit + AuditLogChange
    ↓
Campus-scoped timeline/detail


AMENDMENT
    ↓
Amendment + immutable changes + applied revision
    ↓
Timeline/detail


HOST TRANSFER
    ↓
Whitelisted AuditLog
    ↓
Timeline/detail


LIFECYCLE
    ↓
Whitelisted scoped AuditLog
    ↓
Timeline/detail
```

---

# 33. NGUYÊN TẮC CUỐI CÙNG

Một lịch sử đúng phải thỏa cả bốn điều kiện:

```text
1. Được ghi ngay khi sự kiện xảy ra.
2. Không bị mutation tương lai làm biến mất.
3. Không thể hiện sai người / campus / thời điểm.
4. Không cho người không có quyền đọc.
```

Nếu chỉ thỏa 1–3 mà không thỏa 4 thì là lỗi bảo mật.

Nếu chỉ thỏa 2–4 mà không thỏa 1 thì là mất audit.

Nếu dựng từ current state thì dù giao diện trông đúng hôm nay, lịch sử vẫn có thể sai vào ngày mai.

Vì vậy mọi thay đổi của đợt này phải ưu tiên **append-only immutable source + scope-first read + transaction integrity**, và tránh refactor ngoài phạm vi nếu không có test bảo vệ.
