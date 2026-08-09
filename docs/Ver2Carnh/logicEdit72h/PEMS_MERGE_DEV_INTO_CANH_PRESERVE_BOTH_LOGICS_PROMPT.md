# PEMS — PROMPT XỬ LÝ XUNG ĐỘT DEV ↔ CẢNH-ITER1, GIỮ ĐỦ LOGIC HAI NHÁNH

## 0. Mục tiêu

Bạn đang làm việc trên repository **PEMS**.

Nhiệm vụ:

1. Kiểm tra lại **HEAD mới nhất** của:
   - `Dev`
   - `Cảnh-Iter1`
2. Merge **`origin/Dev` vào `Cảnh-Iter1` trước**.
3. Resolve conflict theo **semantic merge**, không chọn nguyên một bên.
4. Bảo đảm:
   - không mất logic mới vừa có trên `Dev`;
   - không mất refactor `FILTER != AUTHORIZATION != ENTRY CONTEXT` vừa có trên `Cảnh-Iter1`;
   - logic cuối phải theo **mô hình mới** của Cảnh, nhưng **hấp thụ đầy đủ các bug fix / lifecycle / routing / concurrency / confirmation-gate mới nhất của Dev**.
5. Chạy đầy đủ test/gates trên trạng thái đã merge.
6. Chỉ khi tất cả xanh mới báo `READY TO MERGE CẢNH -> DEV`.
7. **Không merge vào Dev trong task này nếu chưa được yêu cầu explicit.**

---

# 1. Trạng thái đã audit gần nhất — chỉ dùng làm mốc, phải fetch lại trước khi làm

Lần audit gần nhất:

```text
Dev:
6d957cfae3ccded7aa4f9e1dbde20f6d7db7b88b

Cảnh-Iter1:
3ab5d49403f1832f0b366728633b3a1deae72c52

merge-base:
7eacbca8de24bfbed383c57222eaa22ec907175b

status:
Cảnh-Iter1 ahead 1 / behind Dev 4
```

**Không được giả định các SHA trên vẫn mới nhất.**

Trước khi merge phải chạy/fetch lại để xác nhận:

```bash
git fetch origin --prune

git rev-parse origin/Dev
git rev-parse origin/Cảnh-Iter1
git merge-base origin/Dev origin/Cảnh-Iter1

git status --short
git branch --show-current
```

Nếu branch/HEAD khác mốc trên thì lấy **latest remote refs** làm source of truth.

---

# 2. Preflight bắt buộc

Trước khi chạm code:

```text
Branch hiện tại:
HEAD local:
origin/Dev:
origin/Cảnh-Iter1:
merge-base:
Working tree:
Stashes:
```

Quy tắc:

- working tree phải sạch hoặc chỉ chứa file rõ ràng không thuộc task;
- không reset/revert/stash/drop thay đổi không thuộc task;
- không force-push;
- không merge trực tiếp `Cảnh-Iter1 -> Dev` ở đầu task;
- không sửa DB/schema;
- không đổi business rule ngoài phần cần để hợp nhất logic hai nhánh;
- không dùng `ours/theirs` cho toàn file cốt lõi.

---

# 3. Chiến lược merge bắt buộc

Làm trên `Cảnh-Iter1`:

```bash
git checkout Cảnh-Iter1
git pull --ff-only origin Cảnh-Iter1

git fetch origin --prune
git merge origin/Dev
```

Nếu có conflict:

```text
KHÔNG:
- Accept All Current
- Accept All Incoming
- git checkout --ours <core-file>
- git checkout --theirs <core-file>

CHO CÁC FILE LOGIC CHÍNH
```

Phải đọc 3 phía:

```text
BASE
CẢNH
DEV
```

và dựng kết quả cuối theo semantics được chốt trong prompt này.

---

# 4. Nguyên tắc kiến trúc cuối cùng

Logic cuối bắt buộc giữ mô hình:

```text
FILTER != AUTHORIZATION != ENTRY CONTEXT
```

Trong đó:

```text
FILTER
→ chỉ xác định row nào xuất hiện

REAL RELATIONS
→ REGISTRANT
→ OPERATIONAL_CONTACT
→ HOST
→ CAMPUS_REVIEWER
→ PARTICIPANT

AUTHORIZATION
→ union quyền thật từ relations
→ sau đó áp lifecycle/status/deadline/concurrency/gates

ENTRY CONTEXT
→ xác định màn mặc định user được đưa vào
→ không phải nguồn authorization
```

Không được để merge từ Dev làm quay trở lại mô hình:

```text
tab == permission
registered == read-only
rowTab == authorization
```

---

# 5. Các file conflict/semantic-overlap trọng yếu

Audit gần nhất cho thấy hai nhánh cùng sửa ít nhất các file:

```text
backend/PEMS.Application/Delegations/Queries/ViewGuestDelegationList/ViewGuestDelegationListDto.cs

backend/PEMS.Application/Delegations/Queries/ViewGuestDelegationList/ViewGuestDelegationListQueryHandler.cs

frontend/pems-react/src/features/delegations/types/delegations.types.ts

frontend/pems-react/src/pages/dashboard/visit/VisitRequestManagement.tsx

frontend/pems-react/src/shared/i18n/locales/vi/visitRequestV2.json

frontend/pems-react/src/shared/i18n/locales/en/visitRequestV2.json
```

Phải search thêm conflict thật sau merge:

```bash
git diff --name-only --diff-filter=U
git status
```

Ngoài conflict marker, phải audit **semantic overlap** cả ở file Git tự auto-merge được.

---

# 6. `ViewGuestDelegationListDto.cs` — kết quả cuối phải giữ CẢ HAI NHÓM FIELD

## 6.1 Logic Cảnh phải giữ

Các additive field multi-relation / entry-context:

```text
Relations
RelationContexts
PrimaryEntryContext
PrimaryEntryVisitInstanceId
VisitRelationContextDto
VisitRowRelations
VisitEntryContexts
CampusProgressItemDto.OperationalContactUserId
```

Legacy fields vẫn giữ cho compatibility:

```text
TabType
CurrentUserRelation
RelationLabel
IsAlsoHost
Capabilities
```

Nhưng legacy relation/tab không được làm nguồn authorization.

---

## 6.2 Logic Dev phải giữ

Các field Dev mới thêm phục vụ participant/routing/scope phải được giữ:

```text
ParticipantId
ParticipantStatus
CanViewRequestDetail
```

và các field lifecycle/concurrency/list capability khác Dev mới thêm nếu tồn tại ở HEAD mới nhất.

### Kết quả

DTO cuối là **superset** của cả hai bên.

Không xóa field của Dev chỉ vì Cảnh đã có `RelationContexts`.

Không xóa field của Cảnh chỉ vì Dev có thêm scope fields.

---

# 7. `ViewGuestDelegationListQueryHandler.cs` — file quan trọng nhất

Đây là file phải semantic-merge thủ công.

---

## 7.1 Registered filter — giữ logic mới của Cảnh

Final:

```csharp
vr.RegistrantUserId == userId
```

Không được giữ mutual exclusion cũ kiểu:

```csharp
&& !contactOwner
```

Một user:

```text
REGISTRANT + OPERATIONAL_CONTACT
```

phải xuất hiện ở cả:

```text
Tôi là người đăng ký
Tôi là đầu mối
```

và trong `Tất cả` chỉ còn 1 request row sau merge context.

---

## 7.2 Không được phục hồi `registeredView` thành authorization boundary

Phải bỏ/không tái sinh các pattern:

```csharp
if (tab == TabRegistered)
    return actions;
```

hoặc:

```csharp
!registeredView && canEdit...
!registeredView && canResubmit...
```

Filter registered chỉ là population.

---

# 8. Pending edit — PHẢI GHÉP logic Cảnh + Dev

Cảnh đã bỏ tab gate.

Dev mới mở đúng lifecycle cho cả contact-confirmation stage.

Final semantics:

```text
Registrant có thể edit pending nếu request còn ở PRE-DECISION:
- PENDING_CONTACT_CONFIRMATION
- PENDING_APPROVAL
```

và các campus liên quan đều ở:

```text
WAITING_CONTACT_CONFIRMATION
hoặc
WAITING_REQUEST_APPROVAL
```

theo canonical `VisitMutationPolicy`.

Final pseudo:

```csharp
bool canEditPending =
    isRegistrant
    && request.Status is PendingContactConfirmation or PendingApproval
    && instances.Count > 0
    && instances.All(i =>
        i.Status is WaitingContactConfirmation or WaitingRequestApproval)
    && leadTimeRuleSatisfied;
```

**Không để `registeredView` trong biểu thức.**

Nếu HEAD mới nhất của Dev đã centralize `IsPreDecision(...)`, ưu tiên reuse canonical helper thay vì duplicate condition.

---

# 9. Staff Leader approval — giữ relation model của Cảnh + global gate của Dev

## 9.1 Logic Cảnh phải giữ

Authorization theo real relation:

```text
CAMPUS_REVIEWER
```

chỉ tại:

```text
PrimaryCampusId của account
```

không dựa vào tab.

Mỗi Staff/Staff Leader chỉ hoạt động trong đúng một campus.

Invariant:

```text
HOST context.CampusId == user.PrimaryCampusId
CAMPUS_REVIEWER context.CampusId == user.PrimaryCampusId
```

---

## 9.2 Logic Dev phải giữ

Dev đã thêm **global operational-contact confirmation gate**.

Staff Leader không được approve/reject nếu request vẫn đứng sau contact gate.

Final semantics:

```text
isCampusReviewerHere
AND requestActive
AND contactGateOpen
AND campus.Status == WAITING_REQUEST_APPROVAL
→ APPROVE_AND_ASSIGN_HOST
→ CAMPUS_REJECT
```

`contactGateOpen` phải dùng canonical helper hiện có, ví dụ:

```csharp
!VisitRequestStatuses.IsBehindContactGate(item.RequestStatus)
```

nếu đây vẫn là API mới nhất.

Không được merge theo kiểu lấy logic Cảnh rồi làm mất contact gate.

---

# 10. Approval concurrency — giữ nguyên logic Dev

Dev mới thêm optimistic-concurrency cho approve:

```text
ExpectedInstanceRowVersion
VISIT_INSTANCE_VERSION_CONFLICT
VisitInstanceConcurrencyGuard.EnsureUnchangedAsync(...)
```

Kết quả merge cuối phải giữ:

```text
FE gửi rowVersion user đã review
→ backend check trong transaction
→ campus đổi revision thì reject stale approval
```

Không được làm mất:

```text
ApproveCampusInstanceBody.ExpectedInstanceRowVersion
ApproveCampusInstanceCommand.ExpectedInstanceRowVersion
VisitInstanceConcurrencyGuard
decisionConflict FE handling
```

Search toàn repo sau merge:

```text
ExpectedInstanceRowVersion
VISIT_INSTANCE_VERSION_CONFLICT
EnsureUnchangedAsync
decisionConflict
```

---

# 11. `Tất cả` — giữ architecture Cảnh, nhưng merge thêm dữ liệu mới của Dev

Final rule:

```text
Tất cả
→ 1 VisitRequest = 1 row
→ merge tất cả relation/context thật
→ không first-wins làm mất dữ liệu
```

Relations có thể gồm:

```text
REGISTRANT
OPERATIONAL_CONTACT
HOST
CAMPUS_REVIEWER
PARTICIPANT
```

---

## 11.1 MergeCandidateInto phải audit tất cả field Dev mới

Đặc biệt phải không làm mất:

```text
ParticipantId
ParticipantStatus
CanViewRequestDetail
```

Nếu một candidate ATTENDING bị merge vào candidate khác:

```text
ParticipantId
ParticipantStatus
PARTICIPANT RelationContext
```

vẫn phải còn đủ để FE route đúng.

Nếu row primary là REGISTRANT nhưng user cũng PARTICIPANT:

```text
Relations chứa cả REGISTRANT + PARTICIPANT
participant metadata vẫn còn nếu cần secondary action
```

---

## 11.2 `CanViewRequestDetail`

Không được tự động set `true` chỉ vì row xuất hiện.

Phải giữ semantics mới của Dev:

```text
row xuất hiện vì một relation
!=
user có quyền mở toàn request detail
```

`CanViewRequestDetail` phải mirror canonical read scope của `VisitFormReadService`.

Merge rule phải theo effective permission:

```text
Nếu bất kỳ real relation hợp lệ nào cấp request-detail scope
→ true

Nếu chỉ PARTICIPANT relation và canonical scope không cho request detail
→ false
```

Không dùng tab để quyết định.

---

# 12. Participant / invitation routing — giữ logic Dev + entry-context Cảnh

Dev đã sửa bug:

```text
ATTENDING row trong "Tất cả"
trước đây mất participantId
→ frontend fallback request detail
→ có thể 403
```

Final phải giữ:

```text
ParticipantId thật
ParticipantStatus thật
```

và route:

```text
PARTICIPANT / ATTENDING
→ invitation/{participantId}
hoặc department-tasks/{participantId}
```

tùy canonical relation/role.

Nếu:

```text
ParticipantStatus == DECLINED
```

thì relation cũ không được ngầm cấp request detail.

Không fallback sang:

```text
/dashboard/visit/v2/{requestId}
```

nếu `CanViewRequestDetail == false`.

---

# 13. Entry Context — logic Cảnh là nguồn chính

Frontend phải ưu tiên backend-computed:

```text
PrimaryEntryContext
PrimaryEntryVisitInstanceId
```

Các context:

```text
CAMPUS_REVIEW
HOST_PROCESS
INVITATION / PARTICIPANT
REGISTRANT_REQUEST
TRACKING / VIEW
```

Priority trong `Tất cả`:

```text
1. CAMPUS_REVIEW_REQUIRED
2. HOST_PROCESS_REQUIRED
3. INVITATION_ACTION_REQUIRED
4. REGISTRANT_ACTION_REQUIRED
5. VIEW / TRACKING
```

Nhưng:

```text
INVITATION
→ cần ParticipantId
```

và:

```text
REQUEST_DETAIL
→ chỉ nếu CanViewRequestDetail
```

Nếu backend có enum/string naming khác ở latest HEAD, giữ canonical naming hiện có nhưng phải đúng semantics trên.

---

# 14. `VisitRequestManagement.tsx` — phải merge hai routing model

## Giữ từ Cảnh

```text
openEntryContext(row)
PrimaryEntryContext
relation badges
secondary actions trong (...)
OPEN_CONTRIBUTION không được chen trước HOST_PROCESS
```

## Giữ từ Dev

```text
participantId / participantStatus routing
CanViewRequestDetail
DECLINED không fallback request detail
decision conflict handling
global contact invitation UI / route nếu cùng file
rowVersion approve/reject handling
```

### Final row click

Pseudo:

```ts
if (openEntryContext(row)) return;

if (fallback needed) {
  route only through backend-granted capability/scope;
}
```

Nhưng `openEntryContext` bản final phải hiểu:

```text
PARTICIPANT context → ParticipantId
HOST_PROCESS → PrimaryEntryVisitInstanceId
CAMPUS_REVIEW → đúng visitInstanceId
REGISTRANT_REQUEST → request detail nếu canViewRequestDetail
```

Không để `activeTab`, `rowTab`, `isVisitor`, `isStaffLeader` trở lại làm permission source.

Chúng chỉ có thể dùng cho presentation/legacy fallback khi backend chưa trả context, không dùng để cấp quyền.

---

# 15. Secondary actions trong `...`

Final UI:

```text
Primary action ngoài row
Secondary actions trong [...]
```

Nếu user có:

```text
REGISTRANT + HOST
```

và primary là Host Process:

```text
[Xử lý] [...]
```

`...` có thể có:

```text
Xem đơn đăng ký
Sửa đơn nếu backend allowed
Xem lịch sử
...
```

Nếu có participant relation:

```text
Mở lời mời / phần tham gia
```

chỉ khi có real `ParticipantId`/action hợp lệ.

Không invent quyền frontend.

---

# 16. ChangeSummary — giữ logic Cảnh đã audit

Cảnh đã nới `AttachChangeSummariesAsync` để:

```text
REGISTRANT = REQUEST scope
```

registrant thấy change badge trên mọi campus thuộc request của mình.

Phải giữ thay đổi này.

Audit đã xác nhận payload chỉ đếm notification của chính user:

```csharp
Notifications.Where(n => n.RecipientUserId == userId ...)
```

Các field:

```text
unreadChangeCount
hasUnreadChanges
latestEventCode
latestChangedAt
requiresViewerAction
campusIndicators
```

không chứa free-text internal detail/PII.

`pendingAmendmentCount` vẫn host-gated riêng.

Không rollback phần này khi resolve Dev.

---

# 17. i18n — merge theo key, không chọn nguyên file

Hai nhánh đều thêm key vào:

```text
frontend/pems-react/src/shared/i18n/locales/vi/visitRequestV2.json
frontend/pems-react/src/shared/i18n/locales/en/visitRequestV2.json
```

Phải giữ:

## Cảnh

```text
relationBadge.*
menu secondary action labels
entry-context labels liên quan
```

## Dev

```text
previousRevisionMissing
contact invitation labels
decision conflict / news / lifecycle labels
mọi key mới có trên latest Dev
```

Sau resolve:

```bash
node -e "JSON.parse(require('fs').readFileSync('.../vi/visitRequestV2.json','utf8')); console.log('vi ok')"
node -e "JSON.parse(require('fs').readFileSync('.../en/visitRequestV2.json','utf8')); console.log('en ok')"
```

và chạy i18n parity tests nếu repo có.

---

# 18. Audit auto-merged files

Không chỉ xử lý file có conflict marker.

Sau merge phải so:

```bash
git diff --merge
git diff --stat HEAD^1 HEAD
git diff --stat HEAD^2 HEAD
```

và search các symbol quan trọng.

Bắt buộc search:

```text
TabRegistered
registeredView
CanViewRequestDetail
ParticipantId
ParticipantStatus
PrimaryEntryContext
PrimaryEntryVisitInstanceId
RelationContexts
MergeCandidateInto
BuildRelationContexts
ResolvePrimaryEntry
APPROVE_AND_ASSIGN_HOST
CAMPUS_REJECT
IsBehindContactGate
ExpectedInstanceRowVersion
VISIT_INSTANCE_VERSION_CONFLICT
WAITING_CONTACT_CONFIRMATION
PENDING_CONTACT_CONFIRMATION
VisitMutationPolicy
OPEN_HOST_PROCESS
OPEN_CONTRIBUTION
AttachChangeSummariesAsync
```

Nếu symbol của Dev biến mất ngoài chủ đích → investigate.

Nếu symbol của Cảnh biến mất ngoài chủ đích → investigate.

---

# 19. Tests bắt buộc sau merge

MySQL + `pems_pr3_test` đang khả dụng.

Không chỉ build.

## 19.1 Backend

Chạy:

```text
dotnet build
backend unit tests
architecture tests
```

và ít nhất:

```text
PEMS.IntegrationTests.VisitRequests
RelationFilterEntryContextTests
```

Nếu có test mới từ Dev về:

```text
contact confirmation gate
approval concurrency
pending edit in WAITING_CONTACT_CONFIRMATION
participant routing/scope
```

phải chạy luôn.

---

# 20. Matrix regression bắt buộc

## Visitor

```text
V1 only REGISTRANT
V2 only CONTACT
V3 REGISTRANT + CONTACT
```

Acceptance:

```text
registered filter đúng
contact filter đúng
all = 1 request row
Registrant quyền không mất do Contact
Contact-only không tự có request edit
```

---

## Staff

```text
S1 REGISTRANT
S2 HOST
S3 REGISTRANT + HOST
```

Acceptance:

```text
registered → Request Detail
hosted/responsible → Host Process
all → 1 row
primary theo task
secondary giữ request action nếu registrant
```

---

## Staff Leader

```text
SL1 REGISTRANT + REVIEWER pending
SL2 REGISTRANT + HOST
SL3 REGISTRANT + REVIEWER + HOST nếu lifecycle hợp lệ
SL4 reviewer-only
SL5 registrant nhưng campus khác
```

Acceptance:

```text
CAMPUS_REVIEWER chỉ PrimaryCampusId
global contact gate chưa mở → không approve/reject
gate mở + WAITING_REQUEST_APPROVAL → approve/reject
```

---

## Participant

```text
ATTENDING only
REGISTRANT + PARTICIPANT
HOST + PARTICIPANT
DECLINED participant
```

Acceptance:

```text
participantId không mất trong all merge
invitation route đúng
DECLINED không được fallback request detail nếu không có scope khác
```

---

# 21. Concurrency tests bắt buộc

Staff Leader review:

```text
render rowVersion N
campus thay đổi → rowVersion N+1
user approve bằng N
→ VISIT_INSTANCE_VERSION_CONFLICT
```

Phải vẫn pass sau merge.

Không được mất `ExpectedInstanceRowVersion`.

---

# 22. Contact gate tests bắt buộc

Multi-campus:

```text
HN contact confirmed
DN contact pending
```

Staff Leader HN:

```text
không thấy/không được approve request qua campus-review path
```

Registrant:

```text
vẫn thấy đơn của mình qua registered
vẫn có pending-edit nếu canonical policy cho phép
```

Khi DN confirm xong:

```text
global gate open
→ Staff Leader HN/DN thấy đúng campus của mình
→ approval action mở
```

---

# 23. Không được thay business rule ngoài scope

Không tự sửa:

```text
72h / RequiredLeadHours
24h transfer
cancel
resubmit
amendment
host handover
email dispatch
status aggregation
DB trigger
per-campus contact model
```

Nếu Dev/Cảnh mâu thuẫn ở các rule này:

```text
ưu tiên canonical domain policy hiện tại
```

và báo rõ conflict, không tự invent.

---

# 24. No logic loss proof

Trước khi kết luận, lập bảng:

| Logic | Cảnh | Dev | Final |
|---|---:|---:|---:|
| Registered != read-only | ✅ | old/mixed | ✅ |
| Multi relation contexts | ✅ | ❌/partial | ✅ |
| 1 request = 1 row in All | ✅ | ❌ | ✅ |
| PrimaryEntryContext | ✅ | ❌/partial | ✅ |
| ParticipantId routing | partial | ✅ | ✅ |
| CanViewRequestDetail | partial | ✅ | ✅ |
| DECLINED no bad fallback | partial | ✅ | ✅ |
| Global contact gate | old/partial | ✅ | ✅ |
| PendingContactConfirmation editable | old/partial | ✅ | ✅ |
| Approval rowVersion concurrency | ❌ | ✅ | ✅ |
| Registrant request-wide change badge | ✅ | previous narrower | ✅ |
| Host/reviewer constrained to PrimaryCampus | ✅ | DB-backed | ✅ |

Nếu bất kỳ Final nào chưa ✅ → task chưa complete.

---

# 25. Gates cuối cùng

Tối thiểu phải xanh:

```text
Backend:
- dotnet build
- full relevant unit
- architecture
- PEMS.IntegrationTests.VisitRequests
- RelationFilterEntryContextTests

Frontend:
- tsc --noEmit
- vite build
- full vitest
- VisitRequestManagementEntryContext.test.tsx

i18n:
- JSON parse
- parity test nếu có
```

Nếu test fail do merge:

- sửa nguyên nhân;
- không suppress;
- không skip;
- không đổi expected test chỉ để xanh nếu behavior sai.

---

# 26. Commit merge

Chỉ khi tất cả gate xanh.

Commit merge trên `Cảnh-Iter1`.

Không squash mất lịch sử nếu team đang dùng merge commit để trace Dev sync.

Ví dụ:

```text
merge Dev into Cảnh-Iter1: preserve relation model and latest visit fixes
```

Push:

```bash
git push origin Cảnh-Iter1
```

Sau push, fetch lại và xác nhận:

```text
origin/Cảnh-Iter1 HEAD == local HEAD
```

---

# 27. Kiểm tra khả năng merge ngược Cảnh -> Dev

Sau khi Cảnh đã chứa latest Dev:

```bash
git fetch origin
git merge-base origin/Dev origin/Cảnh-Iter1
git rev-list --left-right --count origin/Dev...origin/Cảnh-Iter1
```

Mục tiêu:

```text
Dev không còn commit riêng mà Cảnh thiếu.
```

Tức thường kỳ vọng:

```text
Dev behind Cảnh
Cảnh ahead Dev
```

Sau đó dùng merge-tree / dry-run / PR mergeability nếu có để kiểm tra:

```text
Cảnh -> Dev
```

không còn textual conflict.

**Không tự merge vào Dev nếu chưa được user yêu cầu.**

---

# 28. Báo cáo cuối bắt buộc

## 1. Preflight

```text
Branch:
Dev SHA:
Cảnh SHA before:
merge-base:
working tree:
```

## 2. Conflict list

```text
textual conflicts:
semantic overlaps:
```

## 3. Resolution per file

Với từng core file:

```text
Cảnh logic giữ:
Dev logic giữ:
Final semantics:
```

## 4. No-logic-loss matrix

Dùng bảng ở §24.

## 5. Tests/gates

Báo số pass/fail chính xác.

## 6. Post-merge branch state

```text
Cảnh SHA after:
origin/Cảnh SHA:
Dev SHA:
ahead/behind:
```

## 7. Merge readiness

Chỉ được kết luận một trong:

```text
READY TO MERGE CẢNH -> DEV
```

hoặc:

```text
NOT READY
```

Nếu `NOT READY`, nêu đúng blocker còn lại.

---

# 29. Definition of Done

Task chỉ complete khi:

```text
[ ] Dev latest đã được merge vào Cảnh-Iter1.
[ ] Không còn conflict marker.
[ ] Không dùng ours/theirs cho toàn core file.
[ ] Registered filter chỉ dựa RegistrantUserId.
[ ] registered không còn là read-only boundary.
[ ] Pending edit hỗ trợ cả PendingContactConfirmation + PendingApproval theo canonical policy.
[ ] Multi-relation của Cảnh vẫn giữ.
[ ] All = 1 request / 1 row.
[ ] Merge không mất ParticipantId / ParticipantStatus.
[ ] CanViewRequestDetail của Dev vẫn đúng.
[ ] Participant/DECLINED routing không fallback sai.
[ ] PrimaryEntryContext của Cảnh vẫn là routing source chính.
[ ] Global contact gate của Dev vẫn giữ.
[ ] Staff Leader approval chỉ đúng PrimaryCampus + gate open.
[ ] Approval ExpectedInstanceRowVersion của Dev vẫn giữ.
[ ] ChangeSummary request-scope của Registrant vẫn giữ.
[ ] i18n giữ key cả hai nhánh.
[ ] Backend build xanh.
[ ] Backend unit xanh.
[ ] Architecture xanh.
[ ] VisitRequests integration xanh.
[ ] RelationFilterEntryContextTests xanh.
[ ] FE typecheck xanh.
[ ] FE build xanh.
[ ] FE vitest xanh.
[ ] EntryContext FE tests xanh.
[ ] Cảnh đã push.
[ ] Cảnh không còn thiếu commit mới nhất của Dev.
[ ] Đã kiểm tra Cảnh -> Dev không còn conflict.
[ ] Chưa tự merge vào Dev nếu user chưa yêu cầu.
```

---

# 30. Nguyên tắc chốt

Mục tiêu không phải:

```text
"resolve cho hết conflict"
```

Mà là:

```text
Dev latest
    +
Cảnh relation/filter/authorization refactor
    ↓
ONE coherent final design
```

Logic cuối phải tuân:

```text
FILTER
→ population only

RELATIONS
→ source of identity/scope

AUTHORIZATION
→ union relation grants
→ lifecycle/gate/concurrency restrictions

ENTRY CONTEXT
→ navigation only
```

và đồng thời giữ toàn bộ fix mới của Dev về:

```text
global contact gate
pending-contact-confirmation editability
participantId / participantStatus
CanViewRequestDetail
declined participant routing
approval optimistic concurrency
latest lifecycle guards
```

Không được hy sinh logic của một nhánh để "merge sạch". Semantic correctness quan trọng hơn textual cleanliness.
