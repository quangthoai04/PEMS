# PEMS — System-wide Notification Semantic Routing Full Fix

> **Target branch:** `Canh_iter3_FixBug`  
> **Mục tiêu:** sửa triệt để toàn bộ notification đang chuyển sai màn hình/sai hành động, để mọi notification điều hướng theo **ý nghĩa nghiệp vụ thực sự**.  
> **Source of Truth:** code mới nhất trên GitHub của branch `Canh_iter3_FixBug`.  
> **Không commit / không push nếu chưa được yêu cầu.**

---

## 0. Mục tiêu bắt buộc

Sau fix, mọi notification phải đi theo pipeline:

```text
WHAT HAPPENED
eventKey
    ↓
WHAT CLICK MEANS
Navigation Intent
    ↓
WHAT RECORD
Structured Business Target
    ↓
WHAT IS TRUE NOW
Current State + Current Relation + Current Permission
    ↓
WHERE USER SHOULD GO
Correct Destination
```

Không được tiếp tục để `targetUrl` cũ, role tĩnh, `primaryEntryContext` chung hoặc `allowedActions` hiện tại tự ý thay đổi ý nghĩa notification.

Ví dụ:

```text
"Visitor đã cập nhật đơn đăng ký tham quan"
```

phải dẫn tới:

```text
Chi tiết đơn / Lịch sử thay đổi
```

KHÔNG được vì Staff Leader hiện còn quyền duyệt mà tự mở:

```text
Duyệt & phân công Host
```

Tương tự:

```text
"Bạn không còn phụ trách đoàn khách này"
```

không được đưa Host cũ vào Host Process chỉ vì notification cũ lưu `/dashboard/visit/process/{id}`.

---

# 1. LOCK SOURCE OF TRUTH

Trước khi sửa:

1. Làm đúng branch:

```text
Canh_iter3_FixBug
```

2. Lấy HEAD mới nhất và báo:

```text
Branch:
HEAD SHA:
Commit message:
```

3. Đọc lại code hiện tại. Không dùng line number/snapshot cũ nếu HEAD đã thay đổi.
4. Không dùng `Dev`, commit cũ hay docs cũ làm source of truth khi code hiện tại khác.
5. Không đoán business rule.

Nếu source chưa đủ:

```text
CHƯA ĐỦ BẰNG CHỨNG TỪ SOURCE HIỆN TẠI
```

rồi tiếp tục trace code.

---

# 2. FILE BẮT BUỘC PHẢI ĐỌC

## Frontend notification

```text
frontend/pems-react/src/features/notifications/components/NotificationBellButton.tsx
frontend/pems-react/src/pages/notifications/NotificationsPage.tsx
frontend/pems-react/src/features/notifications/utils/notificationSemantic.ts
frontend/pems-react/src/features/notifications/utils/resolveNotificationDestination.ts
frontend/pems-react/src/features/notifications/utils/resolveNotificationPresentation.ts
frontend/pems-react/src/features/notifications/types/notification.types.ts
```

## Các surface khác có click notification

```text
frontend/pems-react/src/pages/dashboard/departments/SharedDashboardView.tsx
frontend/pems-react/src/pages/dashboard/department-staff/StaffCalendarTab.tsx
```

Repo-wide search thêm mọi chỗ dùng:

```text
resolveNotificationDestination
targetUrl
actionType
metadataJson
navigate(...)
```

Không sửa Bell rồi bỏ `/notifications`, dashboard hoặc calendar.

## Visit destination

```text
frontend/pems-react/src/pages/dashboard/visit/VisitRequestManagement.tsx
frontend/pems-react/src/pages/dashboard/visit/VisitProcess.tsx
frontend/pems-react/src/pages/dashboard/visit/VisitRequestV2DetailPage.tsx
frontend/pems-react/src/features/visit-request/components/v2/VisitRequestV2DetailView.tsx
frontend/pems-react/src/features/visit-request/components/VisitHistoryTimeline.tsx
frontend/pems-react/src/features/visit-request/utils/visitVersionRouting.ts
frontend/pems-react/src/App.tsx
frontend/pems-react/src/shared/auth/dashboardRouteAccess.ts
```

## Backend notification

```text
backend/PEMS.Application/Notifications/Common/NotificationConstants.cs
backend/PEMS.Application/Notifications/Common/NotificationEventKeys.cs
backend/PEMS.Application/Notifications/Common/NotificationService.cs
backend/PEMS.Application/Notifications/Common/NotificationDto.cs
backend/PEMS.Application/Notifications/Common/INotificationService.cs
```

Repo-wide search tất cả producer:

```text
CreateNotificationRequest(
CreateAsync(
CreateManyAsync(
NotificationEventKeys.
NotificationActionTypes.
ActionUrl:
RelatedType:
RelatedId:
VisitRequestId:
VisitInstanceId:
CampusId:
```

---

# 3. ROOT CAUSES ĐÃ XÁC ĐỊNH

## RC-01 — Có semantic classifier nhưng runtime vẫn `targetUrl-first`

Hiện đã có:

```text
eventKey → NotificationNavigationIntent
```

nhưng `resolveNotificationDestination()` vẫn bắt đầu từ:

```ts
let link = item.targetUrl || undefined;
```

rồi route bằng URL string/regex.

Hệ quả:

```text
same eventKey
same target entity
different targetUrl shape
→ different runtime routing
```

Đây là lỗi kiến trúc.

---

## RC-02 — Chỉ một số URL Visit mới đi qua semantic one-shot command

Dạng:

```text
/dashboard/visit
/dashboard/visit?visitRequestId=123
```

có thể được rewrite.

Nhưng:

```text
/dashboard/visit?visitRequestId=123&tab=all
/dashboard/visit/process/456
```

có thể bypass semantic intent.

Sau fix, semantic Visit event không được phụ thuộc targetUrl có match regex hay không.

---

## RC-03 — Direct `/process/:id` có thể mở Host Process bằng snapshot cũ

Các nhóm như:

```text
HOST_ASSIGNED
HOST_TRANSFER_INCOMING
HOST_TRANSFER_OUTGOING
VISIT_REMINDER
```

có producer lưu direct process URL.

Nếu relation đã đổi thì direct URL stale.

Phải resolve **current relation của đúng instance** trước khi cho vào Host Process.

---

## RC-04 — `HOST_TRANSFER_OUTGOING` semantic và URL đang mâu thuẫn

Ý nghĩa đúng:

```text
HOST_TRANSFER_OUTGOING → VISIT_DETAIL
```

nhưng producer hiện có thể lưu:

```text
/dashboard/visit/process/{visitInstanceId}
```

cho Host cũ.

Phải sửa cả producer mới và runtime legacy fallback.

---

## RC-05 — `VISIT_REMINDER` dùng một process URL cho Host và Participant

Reminder có thể gửi cho:

```text
HOST
PARTICIPANTS
HOST_AND_PARTICIPANTS
```

nhưng cùng trỏ `/process/{instanceId}`.

Sai vì:

```text
STAFF có thể là Host của đoàn A
nhưng chỉ là Participant của đoàn B.
```

Static role không đủ. Phải dùng relation thật của đúng instance.

---

## RC-06 — `PARTICIPATION_INVITED` làm mất exact `participantId`

Notification đã biết participant nhưng resolver có thể rewrite về:

```text
/dashboard/visit?visitRequestId=...&tab=attending
```

rồi tìm lại từ list.

Nếu có `participantId`, phải đi exact route:

```text
/dashboard/visit/invitations/{participantId}
```

hoặc exact Department task route đúng workflow.

Không bỏ target id.

---

## RC-07 — Attending deep-link chỉ tìm trong page hiện tại

Current flow còn kiểu:

```text
load invitation page
→ map
→ client filter visitRequestId
```

Target ở page sau có thể falsely not found.

Phải có exact backend filter/query.

---

## RC-08 — Current-state resolver mới enforce mạnh Review/History

Các intent:

```text
VISIT_DETAIL
VISIT_READONLY_DETAIL
HOST_PROCESS
VISIT_INVITATION
CONTRIBUTION
```

chưa được semantic-gate đầy đủ.

Không được để fallback:

```ts
openEntryContext(row)
navigateByRelation(row)
```

mở interaction mạnh hơn notification cho phép.

---

## RC-09 — Generic `tab=all` có thể chọn sai relation context

Một account có thể đồng thời là:

```text
REGISTRANT
CAMPUS_REVIEWER
HOST
PARTICIPANT
```

Notification phải resolve relation đúng với semantic event, không lấy merged default context rồi coi đó là ý nghĩa notification.

---

## RC-10 — `items[0]` là unsafe fallback cho multi-campus

Nếu event cần instance cụ thể nhưng thiếu instanceId:

```text
items[0]
```

không được dùng để đoán campus.

Phải downgrade về safe request detail hoặc controlled fallback.

---

## RC-11 — Backend chưa có contract bắt các routing field đồng nhất

Một notification hiện chứa độc lập:

```text
eventKey
actionType
targetUrl
relatedType
relatedId
visitRequestId
visitInstanceId
campusId
```

Cần test/validation để phát hiện producer semantic một kiểu nhưng URL một kiểu.

---

# 4. KIẾN TRÚC ĐÍCH

Tách rõ:

## 4.1 Event — chuyện gì xảy ra?

Nguồn chuẩn:

```text
MetadataJson.eventKey
```

Ví dụ:

```text
VISIT_REQUEST_UPDATED_PENDING
VISIT_REQUEST_WAITING_APPROVAL
HOST_ASSIGNED
HOST_TRANSFER_OUTGOING
PARTICIPATION_INVITED
VISIT_REMINDER
```

## 4.2 Navigation Intent — click để làm gì?

```text
VISIT_REVIEW
VISIT_HISTORY
VISIT_DETAIL
VISIT_READONLY_DETAIL
HOST_PROCESS
VISIT_INVITATION
CONTRIBUTION
FEEDBACK_MODAL
LOGISTICS_DETAIL
HANDOVER_DETAIL
AGENDA_DETAIL
MINUTES_DETAIL
ACTION_ITEM_DETAIL
NEWS_DETAIL
PARTNER_DETAIL
ACCOUNT_DETAIL
NOTIFICATION_DETAIL
```

## 4.3 Business Target — record nào?

Tùy event:

```text
visitRequestId
visitInstanceId
participantId
logisticsItemId
handoverId
agendaId
minutesId
actionItemId
newsId
partnerId
accountId
...
```

Ưu tiên `relatedType + relatedId` đúng chuẩn nếu DTO hiện chưa có field riêng.

**Không parse title/message để tìm target.**

## 4.4 Current State / Relation / Permission

Current backend state mới trả lời:

```text
user hiện còn relation gì?
record hiện trạng thái gì?
user hiện có quyền gì?
```

Current state chỉ được:

```text
GIỮ NGUYÊN hoặc DOWNGRADE semantic intent
```

Không được UPGRADE.

---

# 5. PRECEDENCE BẮT BUỘC

```text
1. Explicit modern actionType
2. eventKey semantic
3. structured target ids
4. current state/current relation/current permission
5. legacy targetUrl fallback
6. notification detail / safe fallback
```

`targetUrl` không được override eventKey semantic.

Legacy URL chỉ dùng khi:
- notification cũ không có eventKey;
- không có modern actionType;
- structured target không đủ;
- URL vẫn permission-safe.

---

# 6. FRONTEND — SEMANTIC-FIRST RESOLVER

`resolveNotificationDestination.ts` không được tiếp tục là một URL-regex router lớn.

Có thể tổ chức:

```ts
classifyNotificationIntent(item)
```

và:

```ts
resolveNotificationTarget(item, user)
```

hoặc abstraction tương đương.

Ví dụ conceptual type:

```ts
type NotificationTarget =
  | {
      kind: 'VISIT';
      intent:
        | 'VISIT_REVIEW'
        | 'VISIT_HISTORY'
        | 'VISIT_DETAIL'
        | 'VISIT_READONLY_DETAIL'
        | 'HOST_PROCESS'
        | 'VISIT_INVITATION'
        | 'CONTRIBUTION';
      visitRequestId?: number;
      visitInstanceId?: number;
      participantId?: number;
    }
  | { kind: 'LOGISTICS'; logisticsItemId: number }
  | { kind: 'NEWS'; newsId: number }
  | { kind: 'PARTNER'; partnerId: number };
```

Không bắt buộc đúng shape này, nhưng phải đạt:

```text
semantic intent + structured target
```

trước khi build navigation.

---

# 7. MỌI VISIT SEMANTIC EVENT PHẢI DÙNG CURRENT-STATE RESOLUTION

Nếu event là Visit semantic và có structured ids:

```text
không cần tin targetUrl cũ.
```

Có thể dùng one-shot command hiện tại:

```text
/dashboard/visit
?openVisitRequestId=...
&openVisitInstanceId=...
&notificationIntent=...
```

hoặc abstraction tương đương.

Quan trọng là không chỉ rewrite khi URL match một regex cụ thể.

---

# 8. `VisitRequestManagement` PHẢI ENFORCE TỪNG INTENT

Pseudo logic:

```ts
switch (intent) {
  case 'VISIT_REVIEW':
    // chỉ review/approve nếu CURRENT state còn pending
    // và CURRENT permission đúng
    // stale → downgrade detail

  case 'VISIT_HISTORY':
    // detail + #history
    // tuyệt đối không approve modal

  case 'VISIT_READONLY_DETAIL':
    // read-only/detail
    // không Host Process, không approve

  case 'HOST_PROCESS':
    // chỉ current Host đúng instance mới vào process
    // mất Host relation → detail

  case 'VISIT_INVITATION':
    // exact participant target
    // không generic request nếu participantId đã biết

  case 'CONTRIBUTION':
    // chỉ current contribution relation/capability
    // mất relation → safe fallback

  case 'VISIT_DETAIL':
    // detail phù hợp quyền hiện tại
    // không tự mở mutation flow
}
```

---

# 9. SEMANTIC LÀ MỨC INTERACTION TỐI ĐA

Bắt buộc:

```text
CURRENT STATE MAY DOWNGRADE.
CURRENT STATE MUST NEVER UPGRADE.
```

| Intent | Current state | Destination |
|---|---|---|
| VISIT_REVIEW | pending + allowed | review/approve |
| VISIT_REVIEW | already processed | detail |
| VISIT_HISTORY | pending | history |
| VISIT_HISTORY | approver still has approve permission | vẫn history |
| VISIT_READONLY_DETAIL | user happens to be Host | vẫn read-only/detail |
| HOST_PROCESS | still current Host | Host Process |
| HOST_PROCESS | Host changed | detail |
| VISIT_INVITATION | invitation current | invitation screen |
| VISIT_INVITATION | relation ended | safe fallback |

---

# 10. FIX CÁC EVENT ĐÃ XÁC ĐỊNH

## 10.1 `VISIT_REQUEST_UPDATED_PENDING`

```text
eventKey = VISIT_REQUEST_UPDATED_PENDING
intent = VISIT_HISTORY
destination = /dashboard/visit/v2/{visitRequestId}#history
```

KHÔNG mở:
- AssignHostModal;
- Approve;
- Reject;
- Host Process.

---

## 10.2 `VISIT_REQUEST_RESUBMITTED`

Ưu tiên:

```text
VISIT_HISTORY
```

Để reviewer xem thông tin/lịch sử mới nhất.

Không auto-open mutation dialog nếu semantic chỉ là “đã gửi lại”.

---

## 10.3 `VISIT_REQUEST_WAITING_APPROVAL`

Đây mới là:

```text
VISIT_REVIEW
```

Nếu current campus vẫn `WAITING_REQUEST_APPROVAL` và user đúng reviewer:
→ review/approve.

Nếu đã xử lý:
→ current detail.

Không resurrect approval.

---

## 10.4 `HOST_REASSIGNMENT_REQUIRED`

Nếu source xác nhận thông báo thật sự yêu cầu chọn lại Host:

```text
VISIT_REVIEW
```

nhưng vẫn current-state gated.

---

## 10.5 `HOST_ASSIGNED`

```text
HOST_ASSIGNED → HOST_PROCESS
```

chỉ nếu recipient hiện vẫn là current Host của instance.

Nếu stale:
→ detail.

---

## 10.6 `HOST_TRANSFER_INCOMING`

```text
HOST_TRANSFER_INCOMING → HOST_PROCESS
```

chỉ nếu recipient vẫn là current Host.

Nếu đã chuyển tiếp:
→ detail.

---

## 10.7 `HOST_TRANSFER_OUTGOING`

Bắt buộc:

```text
HOST_TRANSFER_OUTGOING → VISIT_DETAIL
```

Không `/process/{id}`.

Sửa producer mới và frontend bảo vệ legacy notification cũ.

---

## 10.8 `HOST_CHANGED`

Informational:

```text
→ VISIT_DETAIL
```

Không Host Process.

---

## 10.9 `VISIT_REMINDER`

Relation-aware.

### Current Host

```text
→ Host relevant page / Host Process
```

### Participant không phải Host

```text
→ participant / contribution / correct relation screen
```

### Relation đã mất

```text
→ safe detail nếu còn quyền
hoặc notification detail/error
```

Không dùng process URL cho tất cả recipient.

---

## 10.10 `PARTICIPATION_INVITED`

Nếu có participant id:

```text
→ /dashboard/visit/invitations/{participantId}
```

hoặc exact Department task route phù hợp workflow.

Không bỏ participantId để quay về generic list.

---

## 10.11 Cancellation / Closed / HO visibility

Các nhóm:

```text
VISIT_CLOSED
VISIT_CANCELLED_BY_HOST
VISIT_CANCELLED_STAFF_LEADER
VISIT_CANCELLED_HO_VISIBILITY
VISIT_REQUEST_CANCELLED_BEFORE_APPROVAL
CAMPUS_APPROVED_HO_VISIBILITY
CAMPUS_REJECTED_HO_VISIBILITY
VISIT_REQUEST_PARTIALLY_APPROVED_HO_VISIBILITY
VISIT_REQUEST_FULLY_PROCESSED_HO_VISIBILITY
```

phải là:

```text
VISIT_READONLY_DETAIL
```

hoặc non-mutating detail tương đương.

---

# 11. FIX ATTENDING EXACT TARGET

Bỏ giới hạn:

```text
scan current invitation page
```

Thêm server-side exact filter/query ít nhất một trong:

```text
participantId
visitRequestId
visitInstanceId
```

Notification target phải mở chính xác không phụ thuộc:
- page;
- pageSize;
- sort;
- current tab.

Regression:

```text
50 invitations
target ở item 34
pageSize 10
click notification
→ vẫn mở đúng target
```

---

# 12. KHÔNG DÙNG `items[0]` ĐỂ ĐOÁN CAMPUS

Nếu có `instanceId`:
→ exact match.

Nếu event campus-specific mà thiếu instanceId:
→ safe request detail hoặc controlled fallback.

Không tự lấy campus đầu tiên.

---

# 13. BACKEND PRODUCER CONTRACT

Audit toàn bộ producer và lập bảng:

| Producer | Recipient | eventKey | actionType | relatedType/id | requestId | instanceId | targetUrl hiện tại | Destination đúng |
|---|---|---|---|---|---|---|---|---|

Bắt buộc kiểm:

```text
CreateVisitRequestV2/V2CreateNotifier.cs
OperationalContact/OperationalContactNotifier.cs
UpdatePendingVisitRequestV2/UpdatePendingVisitRequestV2CommandHandler.cs
ProposedHostNotifier.cs
TransferVisitHostCommandHandler.cs
VisitAmendmentHandlers.cs
CancelVisitRequestCommandHandler.cs
RejectCampusInstanceCommandHandler.cs
CampusApprovalExecutor.cs
VisitReminderDispatchService.cs
InviteVisitParticipantCommandHandler.cs
PrepareVisitLogisticsCommandHandler.cs
SignVisitLogisticsHandoverCommandHandler.cs
SaveMinutesCommandHandler.cs
ActionItemDueReminderHostedService.cs
News handlers
Partner handlers
Account handlers
```

Repo-wide search để không bỏ producer khác.

---

# 14. MODERN ACTION TYPES

Có thể giữ/bổ sung:

```text
OPEN_CAMPUS_REVIEW
OPEN_VISIT_HISTORY
OPEN_VISIT_READONLY_DETAIL
OPEN_HOST_PROCESS
OPEN_CONTRIBUTION
OPEN_VISIT_INVITATION
OPEN_LOGISTICS_DETAIL
OPEN_HANDOVER_DETAIL
OPEN_NEWS_DETAIL
OPEN_PARTNER_DETAIL
OPEN_ACCOUNT_DETAIL
```

`OPEN_VISIT_DETAIL` không được là catch-all rồi frontend tự đoán mutation.

Legacy:

```text
OPEN_VISIT_DETAIL + eventKey
→ eventKey quyết định

OPEN_VISIT_DETAIL + no eventKey
→ conservative detail fallback
```

Không auto-approve.

---

# 15. LEGACY NOTIFICATION

Không bắt buộc DB backfill để runtime đúng.

Precedence:

```text
modern actionType
→ eventKey
→ structured ids
→ current state
→ legacy targetUrl
→ safe fallback
```

Legacy direct `/process/...` vẫn phải current-permission/current-relation safe.

---

# 16. CẤM PARSE TITLE/MESSAGE ĐỂ ROUTING

Không:

```ts
title.includes(...)
message.includes(...)
```

Title/message chỉ là presentation.

Routing dựa trên:
- eventKey;
- actionType;
- relatedType/id;
- structured ids;
- current state;
- current relation;
- current permission.

---

# 17. TẤT CẢ SURFACE PHẢI CÙNG DESTINATION

Cùng notification + same current user + same backend state:

```text
Bell
/notifications
SharedDashboardView
StaffCalendarTab
mọi notification widget khác
```

phải cho cùng destination.

Không duplicate semantic routing theo component.

---

# 18. GIỮ FIX SAME NOTIFICATION SECOND CLICK

Regression bắt buộc:

```text
click notification
→ open
→ close
→ click exact same notification
→ open lại
```

StrictMode guard chỉ chặn duplicate effect của cùng command lifecycle,
không chặn click mới.

---

# 19. GIỮ FIX SAME-ROUTE STALE TARGET

Regression:

```text
notification A → request 47028
notification B → request 47027
same mounted component
→ UI phải là 47027
```

Giữ:
- URL source of truth;
- external `searchParams` sync;
- stale response guard;
- clear old rows khi load target mới.

---

# 20. ASYNC RACE

Regression:

```text
click A → request A chậm
click B → request B nhanh

B về trước → B visible
A về sau → KHÔNG overwrite B
```

---

# 21. ONE-SHOT URL LIFECYCLE

Các command:

```text
openVisitRequestId
openVisitInstanceId
notificationIntent
feedbackVisitInstanceId
```

sau consume phải remove bằng `replace`.

Không replay khi:
- search;
- filter;
- page;
- sort;
- tab;
- modal close.

---

# 22. ROUTE + PERMISSION SAFETY

Trước khi coi destination đúng:

1. đối chiếu `App.tsx`;
2. đối chiếu `dashboardRouteAccess.ts`;
3. đối chiếu backend authorization/scope.

Recipient hợp lệ không được bị đẩy vào 403 chỉ vì notification routing chọn route sai role.

---

# 23. NON-VISIT EVENTS

Không broad-refactor những nhóm đã exact đúng.

Audit:

## News

```text
NEWS_PENDING_APPROVAL
NEWS_APPROVED
NEWS_REJECTED
→ exact newsId
```

## Partner

```text
PARTNER_PENDING_APPROVAL
PARTNER_APPROVED
PARTNER_REJECTED
→ exact partnerId
```

## Account

→ profile/accounts đúng recipient permission.

## Logistics

Giữ exact logistics/task/handover target nhưng audit current assignment và role.

## Agenda / Minutes / Action Items

Nếu đã có exact destination thì không map chung về Visit detail.

Nếu chưa đủ bằng chứng:
`CHƯA ĐỦ BẰNG CHỨNG`, không tự tạo route.

---

# 24. UNIT TEST BẮT BUỘC

## Semantic classifier

Mọi current `NotificationEventKeys` phải có:

```text
eventKey → intent
```

Unknown event:
→ conservative fallback.

## Resolver semantic-first

### N-01

```text
VISIT_REQUEST_UPDATED_PENDING
targetUrl = /dashboard/visit/process/...
→ vẫn VISIT_HISTORY
```

### N-02

```text
HOST_TRANSFER_OUTGOING
targetUrl = /dashboard/visit/process/456
→ NOT process
```

### N-03

```text
HOST_ASSIGNED
legacy direct process URL
→ current-state relation-safe flow
```

### N-04

```text
same event
URL A = /dashboard/visit?visitRequestId=123
URL B = /dashboard/visit?visitRequestId=123&tab=all

→ same semantic destination
```

Query-string shape không được đổi intent.

---

# 25. CURRENT-STATE REGRESSION TEST

## R-01 Updated Pending

```text
VISIT_HISTORY
+ campus pending
+ APPROVE_AND_ASSIGN_HOST allowed

→ /v2/{id}#history
→ no AssignHostModal
```

## R-02 Waiting Approval

```text
VISIT_REVIEW
+ pending
+ reviewer đúng
→ review/approve
```

## R-03 stale review

```text
VISIT_REVIEW notification cũ
+ current approved
→ detail
→ no approve modal
```

## R-04 Host assigned current

```text
HOST_PROCESS
+ still current Host
→ Host Process
```

## R-05 Host assigned stale

```text
HOST_PROCESS
+ current Host khác
→ detail
```

## R-06 outgoing Host

```text
VISIT_DETAIL
+ no longer Host
→ detail
→ never process
```

## R-07 Readonly cap

```text
VISIT_READONLY_DETAIL
+ primaryEntryContext current = HOST_PROCESS
→ vẫn read-only/detail
```

---

# 26. PARTICIPATION TEST

## P-01 Exact invitation

```text
PARTICIPATION_INVITED
participantId=789
→ invitations/789
```

## P-02 Department exact route

Nếu source xác nhận Department dùng:

```text
department-tasks/789
```

thì phải exact route đó.

## P-03 Beyond page 1

```text
50 invitations
target page 4
→ vẫn mở đúng
```

## P-04 stale removed/declined

Không resurrect accept/decline từ notification cũ.

---

# 27. REMINDER TEST

## RM-01 Host

```text
VISIT_REMINDER
current Host
→ Host relevant page
```

## RM-02 Student participant

```text
VISIT_REMINDER
accepted participant
→ participant/contribution
→ NOT Host Process
```

## RM-03 Staff participant nhưng không Host

Regression bắt buộc:

```text
role = STAFF
relation = PARTICIPANT
currentHostUserId != current user

VISIT_REMINDER
→ participant/relation-safe destination
→ NOT /process as Host
```

---

# 28. MULTI-CAMPUS TEST

## MC-01 exact instance

```text
request HN + DN
notification instance = DN
→ resolve DN
```

## MC-02 missing instance

```text
campus-specific semantic
instanceId missing
→ safe request detail
→ never items[0]
```

## MC-03 multi-relation user

```text
Staff Leader = registrant + reviewer + Host

HOST_ASSIGNED → Host relation
UPDATED_PENDING → history
WAITING_APPROVAL → review
```

Generic merged context không được biến ba event thành cùng navigation.

---

# 29. PRODUCER CONTRACT TEST

Các producer quan trọng phải assert đồng thời:

```text
recipient
eventKey
actionType
relatedType
relatedId
visitRequestId
visitInstanceId
campusId
ActionUrl / semantic target
IsActionRequired
```

Test phải fail nếu semantic và target mâu thuẫn.

Ưu tiên:
- UpdatePendingVisitRequestV2;
- V2CreateNotifier;
- OperationalContactNotifier;
- ProposedHostNotifier;
- TransferVisitHost;
- VisitReminderDispatchService;
- InviteVisitParticipant;
- Cancel;
- Approve/Reject;
- Amendment.

---

# 30. CROSS-SURFACE PARITY TEST

Cùng fixture phải cho cùng destination ở:
- Bell;
- NotificationsPage;
- SharedDashboardView;
- StaffCalendarTab.

Không copy routing logic.

---

# 31. BACKWARD COMPATIBILITY

Không phá:
- feedback modal;
- News;
- Partner;
- Account;
- Logistics;
- Handover;
- Agenda;
- Minutes;
- Action Items;
- pending feedback virtual item;
- read/unread;
- filters;
- i18n presentation.

Presentation và navigation là hai layer riêng.

---

# 32. KHÔNG ĐƯỢC LÀM

Không:
- thêm `if title.includes(...)`;
- hardcode text tiếng Việt;
- hardcode `STAFF => Host`;
- dùng targetUrl làm semantic source of truth;
- dùng `items[0]` đoán campus;
- scan page 1 tìm exact target;
- chỉ sửa Bell;
- tự tạo route mới khi route đúng đã có;
- tự thêm business rule không có bằng chứng;
- broad refactor ngoài notification routing;
- xóa test để build xanh;
- sửa expected test theo bug hiện tại.

---

# 33. PLAYWRIGHT / CHROME SAFETY

TUYỆT ĐỐI KHÔNG:

```text
taskkill /F /IM chrome.exe
taskkill /IM chromium.exe
Get-Process chrome | Stop-Process
pkill chrome
pkill chromium
killall chrome
killall chromium
```

Không kill Chrome cá nhân người dùng.

Chỉ close browser/context/page do Playwright tạo.

Nếu buộc cleanup process:
chỉ kill khi có positive ownership evidence:

```text
Playwright PID
ms-playwright
playwright_chromiumdev_profile
test-owned temp profile
```

Không chắc → KHÔNG KILL.

---

# 34. TEST GATE

Focused tests trước, sau đó full gate.

Backend:

```text
dotnet test
```

Frontend:

```text
npm run lint
npm run test:unit
npm run build
```

Nếu có notification Playwright regression thì chạy suite đó.

Không báo “100% fixed” chỉ vì unit test xanh.

---

# 35. REAL-STACK VERIFICATION NẾU MÔI TRƯỜNG CHO PHÉP

### Staff Leader
- new request waiting approval;
- updated pending;
- resubmitted;
- host reassignment;
- campus approve/reject.

### Staff current Host
- host assigned;
- host transfer incoming;
- host transfer outgoing;
- visit reminder.

### Staff participant, not Host
- participation invited;
- visit reminder.

### Student
- invitation;
- agenda;
- reminder;
- action item.

### Department
- invitation;
- logistics;
- handover.

### Visitor
- campus approved/rejected;
- host changed;
- cancelled/closed;
- feedback.

### HO
- multi-campus submitted;
- partial/final status;
- cancel visibility.

---

# 36. ACCEPTANCE CRITERIA

Task chỉ hoàn thành khi:

```text
AC-01  Notification meaning quyết định navigation intent.
AC-02  targetUrl legacy không override semantic.
AC-03  current state chỉ downgrade, không upgrade.
AC-04  updated request không mở approve modal.
AC-05  old Host không vào Host Process từ stale notification.
AC-06  Staff participant không bị coi là Host chỉ vì role STAFF.
AC-07  participant notification dùng exact participant target.
AC-08  invitation ngoài page 1 vẫn mở đúng.
AC-09  multi-campus exact instance không chọn sai campus.
AC-10  same notification click lần 2 vẫn hoạt động.
AC-11  click A rồi B cùng route không giữ target A.
AC-12  stale async response A không overwrite B.
AC-13  Bell / NotificationsPage / dashboard/calendar parity.
AC-14  legacy notification có safe fallback.
AC-15  producer → resolver → destination được test cho event quan trọng.
```

---

# 37. OUTPUT REPORT SAU FIX

```markdown
# A. Baseline
Branch:
HEAD before fix:
HEAD after local changes:
No commit/push:

# B. Root causes confirmed
RC-01 ...
RC-02 ...

# C. Producer audit
| Producer | Event | Old destination | New semantic destination | Result |

# D. Files changed
- ...

# E. Semantic routing matrix
| eventKey | intent | exact target | stale/current-state rule | destination |

# F. Tests added
- ...

# G. Test results
Backend:
Frontend unit:
Lint:
Build:
E2E:

# H. Remaining limitations
- ...

# I. Proof for reported bugs
1. Updated Pending
2. Same notification second click
3. Same-route A → B
4. Stale Host
5. Reminder participant
6. Invitation pagination
```

Nếu còn điểm chưa kiểm chứng, ghi:

```text
CHƯA ĐỦ BẰNG CHỨNG
```

Không tuyên bố hoàn tất giả.

---

# 38. THỨ TỰ TRIỂN KHAI

## P0 — Giữ các fix đang đúng
- same notification second click;
- same-route URL/state sync;
- stale response guard.

## P1 — Semantic-first resolver
Không để URL shape quyết định Visit semantic navigation.

## P2 — Enforce đầy đủ intent trong VisitRequestManagement
- VISIT_REVIEW;
- VISIT_HISTORY;
- VISIT_DETAIL;
- VISIT_READONLY_DETAIL;
- HOST_PROCESS;
- VISIT_INVITATION;
- CONTRIBUTION.

## P3 — Fix producer mismatch đã biết
Đặc biệt:
- HOST_TRANSFER_OUTGOING;
- VISIT_REMINDER;
- PARTICIPATION_INVITED.

## P4 — Exact-target backend support
Invitation exact lookup/filter, không page scan.

## P5 — Producer contract tests
Chặn semantic/actionType/URL drift trở lại.

## P6 — Full event matrix audit
Mọi current `NotificationEventKeys`.

---

# 39. NGUYÊN TẮC CHỐT

Mỗi notification phải trả lời được 5 câu:

```text
1. Chuyện gì vừa xảy ra?
   → eventKey

2. Người nhận click để làm gì?
   → navigation intent

3. Record nào là target?
   → structured target id

4. Người nhận hiện còn relation/quyền gì?
   → current backend state + permission

5. Màn hình an toàn và đúng ý nghĩa nhất hiện tại là gì?
   → resolved destination
```

Nếu một producer không trả lời nhất quán được 5 câu trên:

```text
NOTIFICATION ROUTING CỦA PRODUCER ĐÓ CHƯA ĐỦ CHUẨN
```

Không tiếp tục vá từng thông báo bằng URL riêng.

Mục tiêu cuối cùng là chuyển PEMS từ:

```text
stored URL + nhiều if
```

sang:

```text
semantic event
→ explicit intent
→ exact business target
→ current state/relation/permission
→ correct destination
```

để mọi thông báo cùng ý nghĩa luôn đi đúng hướng, không phụ thuộc ngôn ngữ, URL cũ, tab hiện tại, role tĩnh hoặc snapshot lịch sử lúc notification được tạo.
