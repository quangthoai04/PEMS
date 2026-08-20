# PEMS — Notification Routing Stabilization & Full Implementation Plan

> **Mục tiêu:** ổn định toàn bộ luồng điều hướng khi người dùng bấm notification trong PEMS, bảo đảm **mỗi loại thông báo luôn đi đúng màn hình / đúng ngữ cảnh / đúng mức thao tác theo ý nghĩa nghiệp vụ**, không bị ảnh hưởng sai bởi URL cũ, role tĩnh, relation khác của cùng người dùng, tab hiện tại hoặc dữ liệu notification lịch sử.  
> **Target branch:** `Canh_iter3_FixBug`  
> **Baseline gần nhất đã audit:** `f4fb030f2e008ca4290c34a3757a17d0651c01b3`  
> **Lưu ý bắt buộc:** trước khi triển khai phải đọc lại HEAD mới nhất của branch. Nếu HEAD đã thay đổi, code mới là Source of Truth.

---

# 1. Bối cảnh và vấn đề hiện tại

PEMS hiện đã có hệ thống notification semantic mới:

```text
eventKey
→ NotificationNavigationIntent
→ current-state resolution
→ destination
```

Nhưng code thực tế vẫn đang tồn tại song song nhiều cơ chế:

```text
eventKey
actionType
targetUrl
relatedType / relatedId
visitRequestId
visitInstanceId
participantId
primaryEntryContext
allowedActions
current relation
```

Do đó một notification có thể được phân loại semantic đúng nhưng **đến bước cuối vẫn bị relation/URL khác cướp mất hướng đi**.

---

# 2. Các bug thực tế đã reproduce

## BUG-01 — Notification “Visitor đã cập nhật đơn” mở modal duyệt

Ví dụ:

```text
Visitor đã cập nhật đơn đăng ký tham quan

Visitor đã cập nhật thông tin đơn ...
Vui lòng xem lại thông tin mới nhất trước khi xử lý.
```

Ý nghĩa đúng:

```text
Xem thông tin mới nhất / lịch sử thay đổi
```

Nhưng có trường hợp lại mở:

```text
Duyệt & phân công người phụ trách
```

Root cause đã xác định:

```text
intent == null
→ vẫn được phép escalate thành VISIT_REVIEW
→ current row có CAMPUS_REVIEW
→ allowedActions có APPROVE_AND_ASSIGN_HOST
→ modal duyệt mở
```

Rule đúng:

```text
ONLY explicit VISIT_REVIEW may open approval.
```

---

## BUG-02 — Same notification click lần 2 không hoạt động

Đã từng xảy ra:

```text
click notification
→ open
→ close
→ click lại cùng notification
→ không mở
```

Nguyên nhân liên quan one-shot command guard giữ command key quá lâu.

Fix này phải được giữ và không regress.

---

## BUG-03 — Click notification A rồi B cùng route nhưng UI vẫn là A

Ví dụ:

```text
Nanning 47028
→ click Shinyway 47027
→ URL đổi 47027
→ UI vẫn Nanning
```

Root cause:

```text
URL-derived state được useState() một lần lúc mount
→ same-route navigation không remount
→ local state stale
```

Fix URL/state sync + stale async response guard phải được giữ.

---

## BUG-04 — “Khách rút quyền sử dụng hình ảnh/truyền thông” mở trang Lời mời tham dự

Đây là bug mới reproduce trên browser.

Notification:

```text
VISIT_PRIVACY_CONSENT_WITHDRAWN
```

semantic hiện được phân loại đúng:

```text
VISIT_READONLY_DETAIL
```

nhưng executor không enforce đầy đủ intent.

Flow sai:

```text
VISIT_PRIVACY_CONSENT_WITHDRAWN
→ VISIT_READONLY_DETAIL
→ current request row được resolve từ tab=all
→ user đồng thời có participant relation
→ primaryEntryContext = CONTRIBUTION
→ participantId tồn tại
→ /dashboard/visit/invitations/{participantId}
```

Kết quả:

```text
privacy notification
→ invitation page
```

SAI hoàn toàn về semantic.

---

# 3. Nguyên nhân kiến trúc cốt lõi

## RC-01 — Semantic classifier đúng nhưng executor chưa semantic-first

Frontend hiện có nhiều intent:

```text
VISIT_REVIEW
VISIT_HISTORY
VISIT_DETAIL
VISIT_READONLY_DETAIL
HOST_PROCESS
VISIT_INVITATION
CONTRIBUTION
```

Nhưng `VisitRequestManagement.resolveAndOpenNotificationTarget()` mới xử lý rõ một số intent như:

```text
VISIT_REVIEW
VISIT_HISTORY
```

Còn các intent khác vẫn có thể rơi xuống:

```text
openEntryContext(row)
navigateByRelation(row)
```

Đây là nguồn gây sai.

---

## RC-02 — `primaryEntryContext` đang bị dùng như semantic notification

`primaryEntryContext` chỉ nên trả lời:

```text
Nếu user đang xem ROW này từ relation hiện tại,
màn mặc định của row là gì?
```

Nó không trả lời:

```text
Notification vừa bấm có ý nghĩa gì?
```

Hai khái niệm phải tách biệt.

---

## RC-03 — `tab=all` không phù hợp để resolve semantic notification

`tab=all` merge nhiều relation:

```text
registrant
reviewer
host
participant
...
```

và chọn một context đại diện.

Điều này đúng cho UI list.

Nhưng sai cho notification vì notification đã biết **relation / business intent cụ thể**.

---

## RC-04 — `targetUrl` cũ vẫn còn ảnh hưởng runtime

Hệ thống mới đã có semantic event nhưng nhiều producer vẫn phát:

```text
OPEN_VISIT_DETAIL
/dashboard/visit?visitRequestId=...
/dashboard/visit/process/...
```

Nếu resolver còn tin URL shape quá nhiều, semantic mới và URL cũ sẽ xung đột.

---

## RC-05 — Legacy notification không có metadata vẫn có thể bị nâng thành mutation

Các row cũ:

```text
metadataJson = null
actionType = OPEN_VISIT_DETAIL
```

không đủ bằng chứng để biết notification là:

```text
review?
history?
detail?
host?
```

Do đó UNKNOWN phải luôn:

```text
SAFE DETAIL
```

Không được:

```text
approve
reject
assign host
host process
accept invitation
```

---

## RC-06 — Structured target chưa đủ chính xác ở một số producer

Ví dụ privacy safe-edit producer đã biết các `touchedInstanceIds`, nhưng notification chỉ lưu:

```text
visitRequestId
```

Nếu một notification mang semantic campus-specific mà thiếu exact target, frontend dễ phải đoán.

---

## RC-07 — Coverage test hiện tại mới đảm bảo “event có intent”, chưa đảm bảo “click đi đúng”

Current coverage kiểu:

```text
eventKey exists
translation exists
classifyNotificationIntent() != null
```

chưa chứng minh:

```text
producer
→ DTO
→ resolver
→ current relation/state
→ destination cuối
```

---

# 4. Nguyên tắc kiến trúc bắt buộc sau fix

Mọi notification phải đi theo:

```text
1. WHAT HAPPENED
   eventKey

2. WHAT CLICK MEANS
   navigationIntent

3. WHICH BUSINESS RECORD
   structured target

4. WHAT IS TRUE NOW
   current state + current relation + current permission

5. WHERE TO GO
   destination
```

---

# 5. Quy tắc tối cao

## RULE-01 — Semantic là mức interaction tối đa

```text
Current state may DOWNGRADE.
Current state must NEVER UPGRADE.
```

Ví dụ:

```text
VISIT_HISTORY
+ current user vẫn có quyền approve
→ vẫn HISTORY
```

Không được:

```text
→ APPROVE MODAL
```

---

## RULE-02 — Chỉ explicit mutation intent mới được tự mở mutation

### Approval

Chỉ:

```text
VISIT_REVIEW
```

mới có thể mở:

```text
Approve / Reject / Assign Host
```

### Host Process

Chỉ:

```text
HOST_PROCESS
```

và current user phải thật sự là current Host.

### Invitation

Chỉ:

```text
VISIT_INVITATION
```

mới mở exact invitation.

### Contribution

Chỉ:

```text
CONTRIBUTION
```

mới mở contribution participant context.

---

## RULE-03 — Unknown / legacy luôn conservative

```text
intent == null
→ SAFE DETAIL
```

Không mutation.

---

## RULE-04 — Không route bằng Title/Message

Cấm:

```ts
title.includes(...)
message.includes(...)
```

Routing phải dùng structured semantic.

---

# 6. Target architecture

## 6.1 Normalize notification trước

Tạo một normalized contract:

```ts
type NormalizedNotificationTarget = {
  eventKey: string | null;
  intent: NotificationNavigationIntent | null;

  entity:
    | { kind: 'VISIT_REQUEST'; visitRequestId: number }
    | { kind: 'VISIT_INSTANCE'; visitRequestId: number; visitInstanceId: number }
    | { kind: 'PARTICIPANT'; participantId: number; visitRequestId?: number; visitInstanceId?: number }
    | { kind: 'LOGISTICS_ITEM'; logisticsItemId: number }
    | { kind: 'HANDOVER'; handoverId: number }
    | { kind: 'ACTION_ITEM'; actionItemId: number }
    | { kind: 'NEWS'; newsId: number }
    | { kind: 'PARTNER'; partnerId: number }
    | { kind: 'ACCOUNT'; accountId?: number }
    | { kind: 'UNKNOWN' };

  legacyUrl?: string | null;
}
```

Shape cụ thể có thể khác, nhưng phải có đủ:

```text
event semantic
intent
exact business target
```

---

# 7. `resolveNotificationDestination` phải đổi trách nhiệm

Hiện function này vừa:

```text
parse URL
rewrite URL
classify semantic
role-special-case
legacy fallback
```

Nên chia thành các bước:

```text
parse semantic
→ normalize target
→ classify destination kind
→ build navigation command
```

Không tiếp tục bổ sung hàng loạt regex/if theo từng bug.

---

# 8. Visit notification phải dùng một semantic executor duy nhất

Tạo hoặc refactor thành function có shape tương đương:

```ts
resolveVisitNotificationDestination({
  intent,
  visitRequestId,
  visitInstanceId,
  participantId,
  currentUser,
  currentState
})
```

Không để Bell tự route một kiểu, NotificationsPage một kiểu, VisitRequestManagement một kiểu.

---

# 9. `VisitRequestManagement` — bắt buộc switch theo intent

Pseudo logic:

```ts
switch (intent) {

  case 'VISIT_REVIEW':
    return resolveReview(...);

  case 'VISIT_HISTORY':
    return resolveHistory(...);

  case 'VISIT_READONLY_DETAIL':
    return resolveReadonlyDetail(...);

  case 'VISIT_DETAIL':
    return resolveDetail(...);

  case 'HOST_PROCESS':
    return resolveHostProcess(...);

  case 'VISIT_INVITATION':
    return resolveInvitation(...);

  case 'CONTRIBUTION':
    return resolveContribution(...);

  default:
    return resolveLegacySafeDetail(...);
}
```

Không:

```ts
switch một phần
→ rồi generic openEntryContext()
```

---

# 10. Contract chi tiết cho từng Visit intent

## 10.1 `VISIT_REVIEW`

Cho phép:

```text
review / approve / reject / assign Host
```

nhưng chỉ nếu:

```text
- exact campus còn WAITING_REQUEST_APPROVAL;
- user là reviewer đúng scope;
- allowedActions hiện tại cho phép.
```

Nếu stale:

```text
→ detail
```

---

## 10.2 `VISIT_HISTORY`

Destination:

```text
/dashboard/visit/v2/{visitRequestId}#history
```

Không được:

```text
approval
host process
invitation
contribution
```

---

## 10.3 `VISIT_READONLY_DETAIL`

Destination mặc định:

```text
/dashboard/visit/v2/{visitRequestId}
```

Nếu có exact `visitInstanceId`:

```text
focus / expand đúng campus
```

Không được:

```text
AssignHostModal
Host Process
Invitation
Contribution
Edit mutation
```

Đây là contract dùng cho:

```text
VISIT_PRIVACY_CONSENT_WITHDRAWN
VISIT_CLOSED
VISIT_CANCELLED_*
HO visibility events
...
```

---

## 10.4 `VISIT_DETAIL`

Destination:

```text
request detail / exact campus detail
```

Có thể hiển thị actions bình thường bên trong page nếu permission có,
nhưng **notification click không tự kích hoạt mutation**.

---

## 10.5 `HOST_PROCESS`

Chỉ mở:

```text
/dashboard/visit/process/{visitInstanceId}
```

nếu:

```text
currentHostUserId == currentUserId
```

và current backend state cho phép.

Nếu Host đã đổi:

```text
→ VISIT_DETAIL
```

---

## 10.6 `VISIT_INVITATION`

Phải có exact:

```text
participantId
```

Destination:

```text
/dashboard/visit/invitations/{participantId}
```

hoặc Department-specific exact route nếu workflow thật dùng route đó.

Không được lấy một participant relation bất kỳ từ merged request row.

---

## 10.7 `CONTRIBUTION`

Phải có:

```text
visitInstanceId
+
current participant relation
```

Destination:

```text
/dashboard/visit/contribution/{visitInstanceId}
```

Nếu relation stale:

```text
→ safe detail
```

---

# 11. Không dùng `tab=all` để quyết định semantic notification

Current UI list có thể tiếp tục dùng `tab=all`.

Nhưng notification resolver không được:

```text
semantic notification
→ query tab=all
→ primaryEntryContext thắng
```

Thay vào đó:

### Review notification

```text
query exact request + exact instance + reviewer relation
```

### Host notification

```text
query exact instance + Host relation
```

### Invitation

```text
query participantId
```

### Detail / read-only

```text
query request detail scope
```

---

# 12. Privacy withdrawal case phải fix bằng contract, không bằng if title

Producer:

```text
SubmitVisitSafeEditCommandHandler
```

Audit:

```text
urgent = privacy withdrawal
touchedInstanceIds
recipients
VisitRequestId
ActionType
eventKey
```

Đích đúng:

```text
VISIT_PRIVACY_CONSENT_WITHDRAWN
→ VISIT_READONLY_DETAIL
```

Nếu chỉ một campus bị ảnh hưởng:

```text
notification nên có exact VisitInstanceId
```

Nếu nhiều campus bị ảnh hưởng:

chọn một trong hai contract có bằng chứng từ business:

```text
A. one notification/request + affectedInstanceIds metadata
```

hoặc:

```text
B. one notification per affected campus
```

Không tự quyết nếu source chưa chứng minh.

---

# 13. Full event inventory bắt buộc

Phải đọc `NotificationEventKeys.cs` trực tiếp và tạo bảng machine-verifiable:

| EventKey | Producer | Recipient | ActionType | RelatedType | RelatedId | Structured Target | Intended Intent | Current Route | Correct Route | Status |
|---|---|---|---|---|---|---|---|---|---|---|

Không hand-count.

---

# 14. Visit lifecycle matrix cần khóa

## Request lifecycle

```text
VISIT_REQUEST_WAITING_APPROVAL → VISIT_REVIEW
VISIT_REQUEST_UPDATED_PENDING  → VISIT_HISTORY
VISIT_REQUEST_RESUBMITTED      → VISIT_HISTORY
VISIT_PRIVACY_CONSENT_WITHDRAWN → VISIT_READONLY_DETAIL
```

## Campus result

```text
CAMPUS_APPROVED → VISIT_DETAIL
CAMPUS_REJECTED → VISIT_DETAIL
```

## Closure/cancellation

```text
VISIT_CLOSED → VISIT_READONLY_DETAIL
VISIT_CANCELLED_BY_HOST → VISIT_READONLY_DETAIL
VISIT_CANCELLED_STAFF_LEADER → VISIT_READONLY_DETAIL
VISIT_REQUEST_CANCELLED_BEFORE_APPROVAL → VISIT_READONLY_DETAIL
```

---

# 15. Host event matrix

```text
HOST_ASSIGNED → HOST_PROCESS if still current Host, else DETAIL

HOST_PROPOSAL_PENDING → VISIT_DETAIL

HOST_REASSIGNMENT_REQUIRED → VISIT_REVIEW if still actionable

HOST_TRANSFER_INCOMING → HOST_PROCESS if still current Host, else DETAIL

HOST_TRANSFER_OUTGOING → VISIT_DETAIL

HOST_CHANGED → VISIT_DETAIL
```

Không direct stored process URL nếu relation đã stale.

---

# 16. Participation matrix

```text
PARTICIPATION_INVITED
→ exact invitation

PARTICIPATION_ACCEPTED
→ participant/visit current detail

PARTICIPATION_DECLINED
→ participant/visit current detail
```

Không dùng request-level merged relation để suy participant screen.

---

# 17. HO visibility matrix

```text
CAMPUS_APPROVED_HO_VISIBILITY
CAMPUS_REJECTED_HO_VISIBILITY
VISIT_CANCELLED_HO_VISIBILITY
HOST_CHANGED_HO_VISIBILITY
HO_CAMPUS_UNPROCESSED_ALERT
MULTI_CAMPUS_REQUEST_SUBMITTED_HO_VISIBILITY
VISIT_REQUEST_PARTIALLY_APPROVED_HO_VISIBILITY
VISIT_REQUEST_FULLY_PROCESSED_HO_VISIBILITY
```

Phải:

```text
read-only / monitoring detail
```

Không mutation.

---

# 18. Amendment matrix

```text
AMENDMENT_PROPOSED
→ exact amendment/request detail

AMENDMENT_APPROVED
→ history

AMENDMENT_REJECTED
→ history
```

---

# 19. Feedback matrix

```text
FEEDBACK_INVITE_VISITOR
→ visitor feedback modal

HOST_FEEDBACK_INVITE
→ host feedback modal

VISITOR_FEEDBACK_RECEIVED
→ feedback/read detail

HOST_FEEDBACK_RECEIVED
→ feedback/read detail
```

Modal routing cũng phải đi qua unified destination contract.

---

# 20. Reminder matrix

`VISIT_REMINDER` phải relation-aware.

### Current Host

```text
→ HOST_PROCESS / current Host page
```

### Participant không phải Host

```text
→ contribution / participant context
```

### Relation stale

```text
→ safe detail
```

Static role:

```text
STAFF
```

không đủ để quyết định.

---

# 21. Agenda / Minutes / Action Item

Audit producer thật.

Expected family:

```text
AGENDA_UPDATED → exact Agenda context
MINUTES_UPDATED → exact Minutes context
ACTION_ITEM_ASSIGNED → exact Action Item
ACTION_ITEM_DUE → exact Action Item
```

Không map chung về Visit detail nếu exact screen đã tồn tại.

---

# 22. Logistics / Handover

Audit:

```text
LOGISTICS_REQUEST_CREATED
LOGISTICS_ASSIGNED
LOGISTICS_ASSIGNEE_ACCEPTED
LOGISTICS_ASSIGNEE_DECLINED
LOGISTICS_PROPOSAL_CREATED
LOGISTICS_PROPOSAL_ACCEPTED
LOGISTICS_PROPOSAL_REJECTED
LOGISTICS_HANDOVER_SIGNED
LOGISTICS_EXPENSE_REMINDER
```

Mỗi event phải target exact:

```text
logisticsItemId
handoverId
department task / host process / expense context
```

theo producer và recipient thật.

---

# 23. News

```text
NEWS_PENDING_APPROVAL
NEWS_APPROVED
NEWS_REJECTED
```

Phải target exact:

```text
newsId
```

Role-safe destination.

---

# 24. Partner

```text
PARTNER_PENDING_APPROVAL
PARTNER_APPROVED
PARTNER_REJECTED
```

Phải exact:

```text
partnerId
```

---

# 25. Account

```text
ACCOUNT_CREATED
ACCOUNT_LOCKED
ACCOUNT_UNLOCKED
ACCOUNT_STATUS_ACTIVATED
ACCOUNT_STATUS_DEACTIVATED
```

Destination phải dựa recipient permission.

Không đưa recipient hợp lệ vào route 403.

---

# 26. Legacy policy

Phân legacy rows:

## Category A — Reconstructable

Có structured evidence đủ mạnh:

```text
eventKey có thể suy ra từ producer structure
relatedType/id
actionType
target ids
recipient context
```

Có thể backfill.

## Category B — Partially reconstructable

Không đủ semantic mutation.

Runtime:

```text
safe detail
```

## Category C — Ambiguous

Không backfill bằng đoán.

Runtime:

```text
notification detail
hoặc
safe request detail
```

---

# 27. Tuyệt đối không backfill bằng localized Title/Message

Không SQL kiểu:

```sql
WHERE title LIKE '%đã cập nhật%'
```

để quyết định semantic.

Không dùng text tiếng Việt làm business discriminator.

---

# 28. Producer contract validation

Mỗi producer phải đảm bảo consistency giữa:

```text
eventKey
actionType
relatedType
relatedId
VisitRequestId
VisitInstanceId
targetUrl
recipient
IsActionRequired
```

Ví dụ test phải fail nếu:

```text
eventKey = HOST_TRANSFER_OUTGOING
intent = VISIT_DETAIL
targetUrl = /process/...
```

---

# 29. Test architecture cần bổ sung

Current test:

```text
eventKey → intent
```

chưa đủ.

Thêm test:

```text
producer fixture
→ NotificationItem DTO
→ normalized semantic
→ current state fixture
→ final destination
```

---

# 30. Test multi-relation collision — bắt buộc

Tạo fixture Staff Leader đồng thời là:

```text
REGISTRANT
CAMPUS_REVIEWER
HOST
PARTICIPANT
```

trên cùng request.

Sau đó:

```text
VISIT_PRIVACY_CONSENT_WITHDRAWN
→ READONLY DETAIL

VISIT_REQUEST_UPDATED_PENDING
→ HISTORY

VISIT_REQUEST_WAITING_APPROVAL
→ REVIEW

HOST_ASSIGNED
→ HOST PROCESS

PARTICIPATION_INVITED
→ INVITATION
```

**Cùng request, cùng user, khác event → khác destination.**

Đây là test chống chính nhóm bug hiện tại.

---

# 31. Regression test case privacy hiện tại

Fixture:

```text
role = STAFF
subRole = LEADER

relations:
- CAMPUS_REVIEWER
- PARTICIPANT

participantId = 44020
participantStatus = DECLINED

eventKey = VISIT_PRIVACY_CONSENT_WITHDRAWN
intent = VISIT_READONLY_DETAIL
```

Expected:

```text
/dashboard/visit/v2/{requestId}
```

Not:

```text
/dashboard/visit/invitations/44020
```

Not:

```text
/dashboard/visit/process/{id}
```

Not:

```text
AssignHostModal
```

---

# 32. Regression review vs update

## Actual review

```text
VISIT_REQUEST_WAITING_APPROVAL
+ pending
+ correct reviewer
→ approval modal
```

## Updated request

```text
VISIT_REQUEST_UPDATED_PENDING
+ pending
+ reviewer still has approve permission
→ HISTORY
→ no modal
```

## Unknown legacy

```text
intent = null
+ pending
+ approve permission
→ SAFE DETAIL
→ no modal
```

---

# 33. Multi-campus exact targeting

## Case 1

```text
request: HN + HCM
notification instanceId = HCM
→ focus HCM
```

## Case 2

Campus-specific event nhưng thiếu instanceId:

```text
→ safe request detail
```

Không:

```text
items[0]
```

---

# 34. Same click regression

```text
click
→ open
→ close
→ click same notification
→ open
```

StrictMode:

```text
one user click → one open
```

---

# 35. Same-route A → B regression

```text
A = 47028
B = 47027

same mounted /dashboard/visit
→ B must replace A
```

---

# 36. Async race regression

```text
A request 1000ms
B request 100ms

click A
click B

B returns
A returns

FINAL UI = B
```

---

# 37. Back/Forward

```text
A → B → Back → A
A → Forward → B
```

URL và UI không được lệch nhau.

---

# 38. Cross-surface parity

Cùng `NotificationItem` phải cho cùng result ở:

```text
NotificationBellButton
NotificationsPage
SharedDashboardView
StaffCalendarTab
StaffDashboardCalendar
```

Không copy business routing riêng từng component.

---

# 39. Unified destination result

Resolver nên trả structured result thay vì chỉ string:

```ts
type NotificationDestination =
  | { kind: 'ROUTE'; path: string }
  | { kind: 'HOST_FEEDBACK_MODAL'; visitInstanceId: number }
  | { kind: 'VISITOR_FEEDBACK_MODAL'; visitInstanceId: number }
  | { kind: 'VISIT_FEEDBACK_MODAL'; visitInstanceId: number }
  | { kind: 'DETAIL_MODAL' };
```

Như vậy Bell/Page không tự if actionType riêng.

---

# 40. Backend current-state API

Không nhất thiết tạo một API khổng lồ.

Nhưng semantic resolution phải có đủ API để hỏi chính xác:

```text
review relation
host relation
participant relation
request read scope
```

Không ép tất cả qua `tab=all`.

---

# 41. Không mass-refactor business workflow

Scope này chỉ sửa:

```text
notification creation metadata
notification routing
deep-link resolution
current-state navigation
legacy fallback
tests
```

Không thay:
- approval business rule;
- status transitions;
- permission policy;
- visit lifecycle;
- role model.

---

# 42. Implementation order

## Phase 0 — Baseline

1. Lock HEAD.
2. Inventory eventKeys.
3. Inventory producers.
4. Inventory click surfaces.
5. Capture current failing browser scenarios.

## Phase 1 — Normalize notification contract

1. Tách semantic parsing.
2. Tách target parsing.
3. Tách legacy URL fallback.
4. Không route mutation từ legacy unknown.

## Phase 2 — Rewrite Visit executor

Implement đầy đủ:

```text
VISIT_REVIEW
VISIT_HISTORY
VISIT_DETAIL
VISIT_READONLY_DETAIL
HOST_PROCESS
VISIT_INVITATION
CONTRIBUTION
```

## Phase 3 — Fix known producer inconsistencies

Đặc biệt audit:

```text
SubmitVisitSafeEditCommandHandler
UpdatePendingVisitRequestV2CommandHandler
UpdatePendingVisitInstanceV2CommandHandler
TransferVisitHostCommandHandler
VisitReminderDispatchService
InviteVisitParticipantCommandHandler
V2CreateNotifier
CampusApprovalExecutor
RejectCampusInstanceCommandHandler
OperationalContactNotifier
ProposedHostNotifier
```

## Phase 4 — Non-Visit producer audit

```text
Agenda
Minutes
Action Item
Logistics
Handover
News
Partner
Account
Feedback
```

## Phase 5 — Legacy safety

1. `intent == null` → safe detail.
2. no mutation.
3. optional structured backfill only if evidence strong.

## Phase 6 — Automated guard

1. Event coverage.
2. Producer contract.
3. Destination classification.
4. Multi-relation collisions.
5. Cross-surface parity.

## Phase 7 — Real-stack verification

Run browser scenarios.

---

# 43. Browser verification matrix

## Staff Leader

```text
Waiting approval
Updated request
Updated campus
Privacy withdrawal
Host reassignment
Host transfer outgoing
Cancelled request
```

## Staff

```text
Host assigned
Host transfer incoming
Host transfer outgoing
Reminder as Host
Reminder as Participant but NOT Host
Participation invited
```

## Student

```text
Participation invitation
Reminder
Agenda
Action Item
```

## Department

```text
Invitation
Logistics task
Handover
```

## Visitor

```text
Campus approved
Campus rejected
Host changed
Cancelled
Closed
Feedback
```

## HO

```text
Multi-campus submitted
Partial approval
Fully processed
Cancellation
Campus result
Unprocessed alert
```

---

# 44. Test gate

Frontend:

```bash
npm run lint
npm run test:unit
npm run build
```

Backend:

```bash
dotnet build
dotnet test
```

Nếu integration project có pre-existing build failure:
- report chính xác lỗi;
- không đánh dấu integration pass;
- focused producer tests chạy được đến đâu ghi đến đó.

---

# 45. Playwright safety

TUYỆT ĐỐI KHÔNG:

```text
taskkill /F /IM chrome.exe
Stop-Process -Name chrome
pkill chrome
killall chrome
```

Không kill browser cá nhân.

Chỉ cleanup process chắc chắn thuộc Playwright.

---

# 46. Không được làm

Không:

```text
fix privacy bằng if(eventKey === privacy) route riêng
rồi giữ executor generic sai
```

Không:

```text
thêm một if cho từng bug user báo
```

Không:

```text
parse title/message
```

Không:

```text
role STAFF => Host
```

Không:

```text
primaryEntryContext => semantic notification
```

Không:

```text
tab=all => source of truth cho notification
```

Không:

```text
items[0] => đoán campus
```

---

# 47. Definition of Done

Chỉ được kết luận notification routing ổn khi:

```text
[ ] Mọi live eventKey có explicit semantic intent.
[ ] Mọi live producer được inventory.
[ ] Producer fields không semantic-conflict.
[ ] Mọi Visit intent có executor riêng.
[ ] VISIT_READONLY_DETAIL không thể mở Invitation/Host/Review.
[ ] VISIT_HISTORY không thể mở mutation.
[ ] Only VISIT_REVIEW can auto-open approval.
[ ] HOST_PROCESS chỉ cho current Host.
[ ] VISIT_INVITATION dùng exact participant target.
[ ] CONTRIBUTION dùng exact participant/instance relation.
[ ] Unknown legacy không mutation.
[ ] Multi-relation collision tests pass.
[ ] Multi-campus exact-target tests pass.
[ ] Same notification second-click pass.
[ ] Same-route A→B pass.
[ ] Async stale response pass.
[ ] Bell / NotificationsPage / Dashboard / Calendar parity pass.
[ ] Representative browser scenarios pass.
```

---

# 48. Final report format bắt buộc

```markdown
# A. Baseline
Branch:
HEAD:
No commit/push:

# B. Event inventory
Total eventKeys:
Total producers:
Unclassified:
Missing target:
Conflicting producer metadata:

# C. Root causes fixed
RC-01...
RC-02...

# D. Semantic matrix
| EventKey | Intent | Target | Current-state rule | Final destination |

# E. Producer audit
| Producer | Recipient | Event | ActionType | Structured target | Correct? |

# F. Files changed
- ...

# G. Regression proof
Privacy withdrawal:
Updated pending:
Waiting approval:
Host stale:
Participant invitation:
Reminder:
Multi-campus:
Same click:
A→B:
Race:

# H. Tests
Frontend:
Backend:
Integration:
E2E:

# I. Legacy
Reconstructable:
Ambiguous:
Backfill:
Runtime fallback:

# J. Remaining gaps
CHƯA ĐỦ BẰNG CHỨNG:
...
```

---

# 49. Acceptance cases đặc biệt bắt buộc

## AC-01 Privacy

```text
Khách rút quyền sử dụng hình ảnh/truyền thông
→ request/campus read-only detail
→ NEVER invitation
→ NEVER Host Process
→ NEVER approval
```

## AC-02 Updated request

```text
Visitor đã cập nhật đơn
→ history
→ NEVER approval modal
```

## AC-03 Waiting approval

```text
Có yêu cầu tiếp khách chờ duyệt
→ approval only if still pending + authorized
```

## AC-04 Multi-relation Staff Leader

Cùng user là reviewer + participant:

```text
privacy → detail
review → approval
invitation → invitation
```

Không relation collision.

## AC-05 Legacy unknown

```text
unknown old notification
→ safe detail
→ no mutation
```

---

# 50. Kết luận triển khai

Không tiếp tục xây notification routing theo mô hình:

```text
stored URL
+ role checks
+ primaryEntryContext
+ nhiều if theo bug
```

Mục tiêu phải chuyển hẳn sang:

```text
EVENT SEMANTIC
      ↓
EXPLICIT INTENT
      ↓
EXACT BUSINESS TARGET
      ↓
CURRENT STATE / RELATION / PERMISSION
      ↓
SAFE FINAL DESTINATION
```

Trong đó:

```text
Semantic quyết định “notification muốn nói gì”.
Current state quyết định “bây giờ còn được làm tới đâu”.
Current state chỉ được downgrade.
Không được đổi notification sang một hành động khác chỉ vì user có thêm relation/quyền khác.
```

Đây là nguyên tắc phải dùng cho toàn bộ phần notification từ bây giờ để tránh tình trạng:

```text
fix notification A
→ notification B sai
→ thêm if
→ notification C sai
```

và chấm dứt vòng lặp vá từng case riêng lẻ.
