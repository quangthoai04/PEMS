# PEMS — KẾ HOẠCH FIX TRIỆT ĐỂ NOTIFICATION SECOND-CLICK + SEMANTIC ROUTING TOÀN HỆ THỐNG

> Mục tiêu: xử lý triệt để các lỗi notification hiện tại trên PEMS, không chỉ vá riêng screenshot đang gặp.
>
> Phạm vi bắt buộc:
>
> `notification producer → notification DB contract → API DTO → semantic metadata → presentation → click resolver → one-shot command → current state/permission → đúng destination → close/re-open → legacy notification → regression test`
>
> **Source of Truth khi lập kế hoạch:** GitHub repository `quangthoai04/PEMS`, branch `Canh_iter3_FixBug`.
>
> HEAD đã audit khi lập tài liệu: `5435a197f82b3177d943ce115ccd9a5fa201efad`.
>
> **Trước khi implement phải kiểm tra lại HEAD. Nếu branch đã đổi, phải re-audit code mới nhất và cập nhật các điểm dưới đây theo code hiện tại.**

---

# 1. VẤN ĐỀ CẦN GIẢI QUYẾT

Hiện hệ thống có ít nhất 2 lỗi đã xác định bằng source code.

## BUG 1 — Click cùng một notification lần đầu mở được, đóng lại, lần hai không mở

Ví dụ:

```text
Bell
→ click notification
→ popup/modal đúng được mở
→ đóng popup
→ mở Bell lại
→ click chính notification đó lần 2
→ không có gì xảy ra
```

Đây là bug thật trong lifecycle của one-shot notification command.

## BUG 2 — Click notification đi sai ý nghĩa nghiệp vụ

Ví dụ notification:

```text
Visitor đã cập nhật đơn đăng ký tham quan
Visitor đã cập nhật thông tin đơn ...
Vui lòng xem lại thông tin mới nhất trước khi xử lý.
```

Ý nghĩa hợp lý:

```text
có dữ liệu vừa thay đổi
→ đọc chi tiết mới nhất
→ xem lịch sử thay đổi
→ sau đó mới quyết định có duyệt hay không
```

Nhưng code hiện tại có thể làm:

```text
notification UPDATED
→ openVisitRequestId
→ fetch current row
→ thấy CAMPUS_REVIEW + APPROVE_AND_ASSIGN_HOST
→ bật ngay popup duyệt/gán Host
```

Đây là semantic routing sai.

Notification nói **“có thay đổi, hãy xem lại”** không đồng nghĩa **“hãy bật ngay hành động duyệt”**.

---

# 2. BẰNG CHỨNG TỪ CODE HIỆN TẠI

Các file trọng tâm đã audit trên `Canh_iter3_FixBug`:

```text
frontend/pems-react/src/features/notifications/components/NotificationBellButton.tsx
frontend/pems-react/src/pages/notifications/NotificationsPage.tsx
frontend/pems-react/src/pages/dashboard/visit/VisitRequestManagement.tsx
frontend/pems-react/src/pages/dashboard/departments/SharedDashboardView.tsx
frontend/pems-react/src/features/notifications/context/NotificationsContext.tsx
frontend/pems-react/src/features/notifications/types/notification.types.ts
frontend/pems-react/src/features/notifications/utils/resolveNotificationPresentation.ts
frontend/pems-react/src/features/visit-request/components/v2/VisitRequestV2DetailView.tsx
frontend/pems-react/src/features/visit-request/components/v2/shared/VisitSectionCard.tsx
frontend/pems-react/src/features/visit-request/utils/visitVersionRouting.ts

backend/PEMS.Application/Notifications/Common/NotificationConstants.cs
backend/PEMS.Application/Notifications/Common/NotificationEventKeys.cs
backend/PEMS.Application/Delegations/Commands/CreateVisitRequestV2/V2CreateNotifier.cs
backend/PEMS.Application/Delegations/Commands/UpdatePendingVisitRequestV2/UpdatePendingVisitRequestV2CommandHandler.cs
backend/PEMS.Application/Delegations/Services/CampusApprovalExecutor.cs
```

Code hiện tại đã có một nền semantic khá tốt:

```text
eventKey
structured params
relatedType
relatedId
visitRequestId
visitInstanceId
campusId
actionType
targetUrl
```

Nhưng phần navigation vẫn chưa tách rõ:

```text
WHAT HAPPENED
WHY USER CLICKED
WHAT IS TRUE NOW
WHAT USER MAY DO NOW
```

---

# 3. ROOT CAUSE BUG 1 — SECOND CLICK BỊ CHẶN SAI

Trong `VisitRequestManagement.tsx` hiện có one-shot guard dạng:

```ts
const consumedNotificationCommandRef = React.useRef<string | null>(null);

const commandKey = `${rawRequestId}:${rawInstanceId}`;

if (consumedNotificationCommandRef.current === commandKey) return;

consumedNotificationCommandRef.current = commandKey;
```

Mục tiêu của guard:

```text
React StrictMode chạy effect 2 lần
→ không được mở cùng modal 2 lần
```

Mục tiêu đó đúng.

Nhưng bug hiện tại:

```text
command đã consume
→ URL openVisitRequestId/openVisitInstanceId đã được xóa
→ consumedNotificationCommandRef vẫn giữ commandKey cũ
```

Sau đó user click cùng notification lần 2:

```text
requestId/instanceId giống lần 1
→ commandKey giống lần 1
→ guard tưởng đây vẫn là StrictMode duplicate
→ return
→ không mở
```

## Fix bắt buộc

Guard phải tồn tại **theo lifecycle của một command đang hiện diện**, không tồn tại vĩnh viễn theo ID.

Conceptual fix:

```ts
useEffect(() => {
  const rawRequestId = searchParams.get('openVisitRequestId');
  const rawInstanceId = searchParams.get('openVisitInstanceId');

  if (rawRequestId === null && rawInstanceId === null) {
    consumedNotificationCommandRef.current = null;
    return;
  }

  // consume command...
}, [searchParams]);
```

Expected lifecycle:

```text
CLICK 1
→ command appears
→ consume
→ ref = key
→ StrictMode duplicate blocked
→ command removed from URL
→ effect sees no command
→ ref reset = null

CLICK 2 SAME NOTIFICATION
→ same command appears again
→ this is a NEW user action
→ allowed
```

## Không được fix kiểu

```text
bỏ hẳn StrictMode guard
```

vì sẽ quay lại lỗi double-open.

Không được tạo random timeout kiểu:

```ts
setTimeout(() => ref.current = null, 500)
```

vì lifecycle phải dựa vào command state, không dựa vào timing.

---

# 4. PHẢI TEST CẢ BELL REOPEN VÀ ACTION REOPEN

Có 2 hành vi khác nhau cần pin:

## 4.1 Bell popover lifecycle

```text
click bell
→ open
click outside / click bell
→ close
click bell lần 2
→ open
```

## 4.2 Same notification action lifecycle

```text
click same notification
→ destination/modal open
close destination/modal
→ reopen bell
→ click same notification
→ destination/modal must open again
```

Không được chỉ test Bell mở/đóng mà bỏ qua one-shot target.

---

# 5. ROOT CAUSE BUG 2 — SEMANTIC BỊ MẤT KHI ROUTING

Ví dụ backend producer:

```text
UpdatePendingVisitRequestV2CommandHandler
```

đang tạo:

```text
eventKey = VISIT_REQUEST_UPDATED_PENDING
actionType = OPEN_VISIT_DETAIL
visitRequestId = ...
targetUrl = /dashboard/visit?visitRequestId=...
```

Frontend `getNotificationLink()` hiện rewrite URL visit list thành:

```text
/dashboard/visit?openVisitRequestId=...
```

Sau đó `VisitRequestManagement` fetch current row.

Nếu current row:

```text
primaryEntryContext = CAMPUS_REVIEW
allowedActions contains APPROVE_AND_ASSIGN_HOST
```

thì code có thể mở approval modal.

Như vậy semantic:

```text
VISIT_REQUEST_UPDATED_PENDING
```

đã bị mất.

Routing chỉ còn biết:

```text
requestId
instanceId
current row
```

Đây là root cause kiến trúc.

---

# 6. KIẾN TRÚC ĐÚNG BẮT BUỘC

Tách 5 khái niệm:

```text
1. EVENT
   Chuyện gì đã xảy ra?

2. PRESENTATION
   Hiển thị title/message VI/EN như thế nào?

3. NAVIGATION INTENT
   Người dùng click notification để xem/ xử lý ngữ cảnh gì?

4. CURRENT BUSINESS STATE
   Trạng thái của entity BÂY GIỜ là gì?

5. CURRENT AUTHORIZATION
   Người dùng BÂY GIỜ còn được phép làm gì?
```

Luồng chuẩn:

```text
Business event
    ↓
eventKey + structured IDs + params + action intent
    ↓
Notification DB
    ↓
Frontend semantic parser
    ├── presentation resolver
    │      → VI / EN title + message
    │
    └── navigation-intent resolver
           ↓
       semantic intent
           ↓
       if target needs current-state resolution
           ↓
       fetch CURRENT state + allowedActions
           ↓
       OPEN MAXIMUM context permitted by original intent
           ↓
       never escalate intent
```

---

# 7. QUY TẮC QUAN TRỌNG NHẤT — CURRENT STATE ĐƯỢC HẠ INTENT, KHÔNG ĐƯỢC NÂNG INTENT

Ví dụ:

## Case A — notification cần duyệt

```text
event = VISIT_REQUEST_WAITING_APPROVAL
intent = REVIEW
```

Current state vẫn pending:

```text
→ REVIEW UI
```

Current state đã được người khác duyệt:

```text
→ DETAIL hiện tại
→ KHÔNG hiện stale Approve
```

Đây là **hạ intent** theo current state.

## Case B — notification chỉ báo cập nhật

```text
event = VISIT_REQUEST_UPDATED_PENDING
intent = HISTORY / DETAIL
```

Dù current state vẫn pending và user có quyền approve:

```text
→ vẫn HISTORY / DETAIL
→ TUYỆT ĐỐI KHÔNG tự nâng thành REVIEW modal
```

Đây là rule bắt buộc.

Công thức:

```text
ORIGINAL SEMANTIC INTENT
sets the maximum interaction level

CURRENT STATE/AUTHORIZATION
may downgrade it

CURRENT STATE/AUTHORIZATION
must never upgrade it into a stronger action
```

---

# 8. TẠO SHARED SEMANTIC METADATA PARSER

Hiện `resolveNotificationPresentation.ts` có logic parse `metadataJson` nội bộ.

Không nên để navigation parse metadata lần hai theo cách khác.

Tạo utility chung, ví dụ:

```text
frontend/pems-react/src/features/notifications/utils/notificationSemantic.ts
```

API conceptual:

```ts
export type ParsedNotificationSemantic = {
  eventKey: string | null;
  params: Record<string, unknown>;
};

export function parseNotificationSemantic(
  metadataJson?: string | null
): ParsedNotificationSemantic;
```

Sau đó:

```text
resolveNotificationPresentation
resolveNotificationDestination
tests
```

đều reuse cùng parser.

Không parse semantic từ:

```text
title
message
localized text
```

---

# 9. TÁCH ROUTING RA KHỎI NotificationBellButton

Hiện `getNotificationLink()` nằm trong:

```text
NotificationBellButton.tsx
```

nhưng lại được import bởi:

```text
NotificationsPage.tsx
SharedDashboardView.tsx
StaffCalendarTab.tsx
StaffDashboardCalendar.tsx
...
```

Đây là coupling sai hướng:

```text
page business routing
→ import từ UI Bell component
```

Tạo utility riêng:

```text
frontend/pems-react/src/features/notifications/utils/resolveNotificationDestination.ts
```

Bell chỉ render Bell.

NotificationsPage chỉ render page.

Routing nằm trong semantic resolver.

---

# 10. ĐỊNH NGHĨA NAVIGATION INTENT RÕ RÀNG

Có thể dùng vocabulary tương đương nếu project đã có tên khác, nhưng phải đủ phân biệt:

```ts
type NotificationNavigationIntent =
  | 'VISIT_REVIEW'
  | 'VISIT_HISTORY'
  | 'VISIT_DETAIL'
  | 'VISIT_READONLY_DETAIL'
  | 'HOST_PROCESS'
  | 'VISIT_INVITATION'
  | 'CONTRIBUTION'
  | 'FEEDBACK_HOST_MODAL'
  | 'FEEDBACK_VISITOR_MODAL'
  | 'LOGISTICS_DETAIL'
  | 'HANDOVER_DETAIL'
  | 'AGENDA_DETAIL'
  | 'MINUTES_DETAIL'
  | 'ACTION_ITEM_DETAIL'
  | 'NEWS_DETAIL'
  | 'PARTNER_DETAIL'
  | 'ACCOUNT_DETAIL'
  | 'NOTIFICATION_DETAIL'
  | 'NOTIFICATION_PAGE';
```

Không bắt buộc phải serialize đúng string trên vào DB ngay ở phase đầu.

Frontend có thể derive intent từ:

```text
new actionType
→ eventKey
→ relatedType
→ legacy actionType
→ targetUrl fallback
```

---

# 11. PRECEDENCE CỦA SEMANTIC RESOLVER

Resolver phải có thứ tự rõ ràng:

```text
1. Explicit modern actionType
2. eventKey semantic
3. relatedType + structured IDs
4. legacy actionType
5. legacy targetUrl
6. NotificationDetailModal / NotificationsPage safe fallback
```

Không được:

```text
targetUrl first
```

vì targetUrl cũ có thể không còn đúng UX hiện tại.

Không được:

```text
title/message contains(...)
```

---

# 12. ONE-SHOT COMMAND PHẢI CARRY CẢ INTENT

Hiện command dạng:

```text
openVisitRequestId
openVisitInstanceId
```

chưa đủ.

Cần thêm semantic intent nếu đi qua Visit Management resolver.

Ví dụ:

```text
/dashboard/visit
?openVisitRequestId=123
&openVisitInstanceId=456
&notificationIntent=REVIEW
```

hoặc:

```text
notificationIntent=HISTORY
notificationIntent=DETAIL
notificationIntent=HOST
notificationIntent=INVITATION
```

Tên param có thể khác nhưng ý nghĩa phải rõ.

## Sau khi consume

Phải delete toàn bộ one-shot params:

```text
openVisitRequestId
openVisitInstanceId
notificationIntent
notificationId nếu có
```

bằng:

```ts
setSearchParams(next, { replace: true });
```

Không để các param này sống qua:

```text
search
filter
tab
pagination
sort
refresh UI
```

---

# 13. RESOLVE CURRENT VISIT TARGET THEO INTENT

Thay:

```ts
resolveAndOpenNotificationTarget(requestId, instanceId)
```

bằng conceptual:

```ts
resolveAndOpenNotificationTarget({
  requestId,
  instanceId,
  intent,
  notificationId,
});
```

## 13.1 Intent = REVIEW

```text
fetch CURRENT row
→ exact instance nếu notification có instanceId
→ nếu CURRENT primaryEntryContext=CAMPUS_REVIEW
  AND allowedActions contains APPROVE_AND_ASSIGN_HOST
  → open review/approve modal

else
  → current detail/context
```

## 13.2 Intent = HISTORY

```text
fetch/authorize current target
→ nếu user có VIEW_CHANGE_HISTORY
  → /dashboard/visit/v2/{requestId}#history
else
  → current detail
```

**Không bao giờ mở approval modal.**

## 13.3 Intent = DETAIL

```text
→ current request/campus detail
→ no auto destructive/decision action
```

## 13.4 Intent = HOST

```text
if CURRENT user still Host
AND allowedActions says OPEN_HOST_PROCESS
→ Host Process

else
→ current detail/read-only context
```

## 13.5 Intent = INVITATION

```text
→ participant/invitation screen
→ pending: Accept/Decline visible if allowed
→ accepted/declined: current read-only invitation detail
```

Không route Staff vào Host Process chỉ vì role là STAFF.

## 13.6 Intent = READONLY_DETAIL

```text
→ current detail/history
→ never auto-open mutation modal
```

---

# 14. FIX CỤ THỂ CHO “VISITOR ĐÃ CẬP NHẬT ĐƠN”

Current event:

```text
VISIT_REQUEST_UPDATED_PENDING
```

Expected semantic:

```text
NOTIFICATION
"Visitor đã cập nhật..."

        ↓

VISIT_HISTORY
        ↓

/dashboard/visit/v2/{visitRequestId}#history
        ↓

đọc thông tin mới nhất
+
xem lịch sử thay đổi
        ↓

nếu current user vẫn có quyền review
thì nút/action review nằm trong UI phù hợp
```

Không:

```text
UPDATED_PENDING
→ AssignHost/Approve popup ngay
```

## Backend phase lâu dài

Producer nên emit intent rõ hơn:

```text
ActionType = OPEN_VISIT_HISTORY
```

thay vì generic:

```text
OPEN_VISIT_DETAIL
```

## Backward compatibility

Notification cũ đã lưu:

```text
eventKey = VISIT_REQUEST_UPDATED_PENDING
actionType = OPEN_VISIT_DETAIL
```

frontend vẫn phải hiểu:

```text
eventKey wins semantic meaning
→ VISIT_HISTORY
```

Không cần chờ backfill DB mới fix UI.

---

# 15. HISTORY DEEP LINK

V2 detail hiện đã có:

```text
VisitHistoryTimeline
```

và có permission:

```text
VIEW_CHANGE_HISTORY
```

Cần support anchor ổn định:

```text
#history
```

Ví dụ:

```text
/dashboard/visit/v2/123#history
```

Có thể:

```tsx
<div id="history">
  <VisitSectionCard ...>
```

hoặc cho `VisitSectionCard` support `id`.

## Nếu history section collapsible

Deep link phải:

```text
load data
→ ensure history section open
→ scroll into view
```

Không chỉ gọi `scrollIntoView` khi body đang hidden.

## Nếu không có VIEW_CHANGE_HISTORY

Không render/refetch history trái phép.

Fallback:

```text
current detail
```

---

# 16. CURRENT EVENT KEY INVENTORY — PHẢI CÓ ROUTING MATRIX

Current `NotificationEventKeys.cs` đã có các event sau.

Mỗi event phải được audit:

```text
producer
recipient
eventKey
params
relatedType
relatedId
visitRequestId
visitInstanceId
campusId
actionType
targetUrl
expected semantic intent
current-state fallback
permission
```

---

# 17. VISIT / VISITOR EVENT MATRIX

## 17.1 `VISIT_REQUEST_WAITING_APPROVAL`

Meaning:

```text
Staff Leader có task cần review
```

Expected:

```text
VISIT_REVIEW
```

Required IDs:

```text
visitRequestId
visitInstanceId
campusId
```

If still pending + authorized:

```text
exact campus review
```

If already decided:

```text
current detail
```

---

## 17.2 `VISIT_REQUEST_UPDATED_PENDING`

Meaning:

```text
request chưa xử lý xong nhưng dữ liệu vừa thay đổi
```

Expected:

```text
VISIT_HISTORY
```

Never:

```text
auto approval modal
```

---

## 17.3 `VISIT_REQUEST_RESUBMITTED`

Expected:

```text
VISIT_HISTORY
```

Reviewers need to see:

```text
what was changed
current content
history
```

If current state no longer needs review:

```text
detail only
```

---

## 17.4 `VISIT_PRIVACY_CONSENT_WITHDRAWN`

Expected:

```text
VISIT_READONLY_DETAIL / HISTORY
```

Must show current cancelled/withdrawn consequences.

No active-process action should be resurrected.

---

## 17.5 `CAMPUS_APPROVED`

Recipient:

```text
Guest side / Visitor / registrant / operational contact
```

Expected:

```text
VISIT_DETAIL exact campus
```

Should show:

```text
approved/current host/current state
```

No review controls.

---

## 17.6 `CAMPUS_REJECTED`

Expected:

```text
VISIT_DETAIL exact campus
```

Prefer showing:

```text
rejected status
decision reason
history
```

No resubmit/edit action unless backend current allowedActions grants it.

---

## 17.7 `VISIT_CLOSED`

Expected:

```text
READONLY DETAIL / SUMMARY
```

No Host active setup unless current role/context explicitly supports historical view.

---

## 17.8 `VISIT_CANCELLED_BY_HOST`

Expected:

```text
READONLY DETAIL / HISTORY
```

No route into active Host Process merely because old targetUrl points there.

---

## 17.9 `HOST_CHANGED`

Expected:

```text
VISIT_DETAIL / HISTORY
```

Guest side needs to see current Host identity.

---

# 18. OPERATIONAL CONTACT EVENT MATRIX

## `OPCONTACT_TRANSFER_FROM`

Meaning:

```text
bạn không còn là operational contact / quyền chuyển đi
```

Expected:

```text
current detail/history
```

Do not open controls that require current operational-contact ownership.

## `OPCONTACT_TRANSFER_TO`

Meaning:

```text
bạn trở thành operational contact
```

Expected:

```text
current campus/request detail
```

If action is still pending/current flow requires acceptance:

```text
open exact invitation/confirmation context
```

Must verify producer semantics; do not guess from title text.

---

# 19. AMENDMENT EVENT MATRIX

## `AMENDMENT_APPROVED`

Expected:

```text
VISIT_HISTORY
```

Open current detail at change-history context.

## `AMENDMENT_REJECTED`

Expected:

```text
VISIT_HISTORY
```

Show rejected amendment/reason if permission permits.

No popup to submit a new amendment automatically.

---

# 20. HOST EVENT MATRIX

## `HOST_ASSIGNED`

Expected:

```text
HOST_PROCESS
```

Current-state rule:

```text
still Host + OPEN_HOST_PROCESS
→ Host Process

no longer Host
→ current detail
```

## `HOST_PROPOSAL_PENDING`

Must audit exact producer/recipient.

Likely action is a pending response/decision context, but **do not implement based on this assumption**.

Required implementation task:

```text
read producer
read existing host-proposal UI
map exact ID + permission
pin by test
```

## `HOST_REASSIGNMENT_REQUIRED`

Expected recipient likely Staff Leader/coordinator.

Must route to the **actual current host reassignment UI**, not generic visit detail.

If requirement/action no longer exists:

```text
detail fallback
```

## `HOST_TRANSFER_INCOMING`

If current user is new Host:

```text
HOST_PROCESS
```

If transfer was superseded:

```text
DETAIL/HISTORY
```

## `HOST_TRANSFER_OUTGOING`

Old Host:

```text
DETAIL/HISTORY
```

Never open Host Process if user is no longer Host.

---

# 21. HO VISIBILITY EVENT MATRIX

Events:

```text
CAMPUS_APPROVED_HO_VISIBILITY
CAMPUS_REJECTED_HO_VISIBILITY
VISIT_CANCELLED_HO_VISIBILITY
HOST_CHANGED_HO_VISIBILITY
HO_CAMPUS_UNPROCESSED_ALERT
```

HO visibility events are not automatically action-required.

## Approved/rejected/cancelled/host-changed

Expected:

```text
exact request + campus read-only/current detail
```

## `HO_CAMPUS_UNPROCESSED_ALERT`

If current campus still genuinely unprocessed and HO has relevant action:

```text
current review/detail context according to actual allowedActions
```

If stale:

```text
detail only
```

Never resurrect stale action.

---

# 22. STAFF LEADER CANCELLATION

## `VISIT_CANCELLED_STAFF_LEADER`

Expected:

```text
READONLY DETAIL / HISTORY
```

No approve/host assignment.

---

# 23. PARTICIPATION MATRIX

Events:

```text
PARTICIPATION_INVITED
PARTICIPATION_ACCEPTED
PARTICIPATION_DECLINED
```

## `PARTICIPATION_INVITED`

Recipient participant:

```text
VISIT_INVITATION
```

If pending:

```text
Accept / Decline if allowed
```

If already answered:

```text
current invitation detail
```

## `PARTICIPATION_ACCEPTED`

Recipient may be Host/coordinator/other stakeholder depending producer.

Expected:

```text
exact participant/visit context
```

No participant response controls for the observer.

## `PARTICIPATION_DECLINED`

Expected:

```text
exact participant/visit context
```

Show decline state/reason if authorized.

Do not route based only on roleCode.

---

# 24. AGENDA / MINUTES / ACTION ITEMS

Current event keys:

```text
AGENDA_UPDATED
MINUTES_UPDATED
ACTION_ITEM_ASSIGNED
ACTION_ITEM_DUE
```

Current NotificationTypes additionally include:

```text
AGENDA_REQUIRED
MINUTES_CREATED
```

This reveals a coverage question:

```text
NotificationTypes vocabulary
!=
NotificationEventKeys vocabulary
```

Agent MUST audit whether producers for:

```text
AGENDA_REQUIRED
MINUTES_CREATED
```

currently emit semantic metadata.

If not, add eventKeys and locales rather than leaving new notifications generic.

## Expected semantic intents

```text
AGENDA_REQUIRED / AGENDA_UPDATED
→ exact visit instance → agenda section

MINUTES_CREATED / MINUTES_UPDATED
→ exact visit instance → minutes/current outcome section

ACTION_ITEM_ASSIGNED / ACTION_ITEM_DUE
→ exact action item, not merely whole visit if item ID exists
```

Required structured identity:

```text
relatedType
relatedId
visitInstanceId
```

If `relatedId` is action item ID, resolver must preserve it.

---

# 25. LOGISTICS MATRIX

Current NotificationTypes include more logistics states than current EventKeys list.

NotificationTypes currently include:

```text
LOGISTICS_REQUEST_CREATED
LOGISTICS_ASSIGNED
LOGISTICS_ASSIGNEE_RESPONDED
LOGISTICS_PROPOSAL_CREATED
LOGISTICS_PROPOSAL_RESPONDED
LOGISTICS_READY
LOGISTICS_DONE
LOGISTICS_HANDOVER_REQUIRED
LOGISTICS_HANDOVER_SIGNED
LOGISTICS_DUE_SOON
LOGISTICS_OVERDUE
EXPENSE_REPORT_REMINDER
```

Current EventKeys include at least:

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

Therefore MUST audit missing semantic coverage for:

```text
LOGISTICS_READY
LOGISTICS_DONE
LOGISTICS_HANDOVER_REQUIRED
LOGISTICS_DUE_SOON
LOGISTICS_OVERDUE
```

and any other producer not mapped.

## Required destination

If notification refers to one logistics item:

```text
→ exact logistics item
```

Required structured target:

```text
relatedType = LOGISTICS_ITEM
relatedId = logisticsItemId
visitInstanceId
```

Handover:

```text
relatedType = LOGISTICS_HANDOVER
relatedId = handoverId
```

No generic `/dashboard` route if exact item UI exists.

---

# 26. NEWS MATRIX

Events:

```text
NEWS_PENDING_APPROVAL
NEWS_APPROVED
NEWS_REJECTED
```

Expected:

```text
/dashboard/news?newsId={relatedId}
```

But destination must respect role.

Examples:

```text
reviewer + pending
→ exact review/manage record

author + approved/rejected
→ exact record/detail allowed to author
```

Do not route a user into management UI they cannot access.

---

# 27. PARTNER MATRIX

Events:

```text
PARTNER_PENDING_APPROVAL
PARTNER_APPROVED
PARTNER_REJECTED
```

Expected:

```text
exact partner record
```

Prefer:

```text
relatedType=PARTNER
relatedId=partnerId
```

Do not depend solely on old ActionUrl.

---

# 28. FEEDBACK MATRIX

Events:

```text
FEEDBACK_INVITE_VISITOR
HOST_FEEDBACK_INVITE
VISITOR_FEEDBACK_RECEIVED
HOST_FEEDBACK_RECEIVED
```

Current UI already has modal patterns:

```text
OPEN_HOST_FEEDBACK_MODAL
OPEN_VISITOR_FEEDBACK_MODAL
pendingFeedback synthetic item
```

Keep correct modal behavior.

Must test:

```text
open modal
close
open SAME feedback notification again
→ works
```

and:

```text
already submitted
→ pending feedback synthetic item disappears
```

---

# 29. VISIT REMINDER

Event:

```text
VISIT_REMINDER
```

Expected destination depends on current relation.

Rules:

```text
Host
→ current Host Process / appropriate tab

Participant
→ invitation/contribution/current visit context

Visitor/guest-side
→ visit detail

HO
→ read-only/current summary

Department
→ current department task/invitation context
```

Do not resolve only by static role.

Use current relation + allowedActions.

---

# 30. ACCOUNT NOTIFICATION MATRIX

Events:

```text
ACCOUNT_CREATED
ACCOUNT_LOCKED
ACCOUNT_UNLOCKED
ACCOUNT_STATUS_ACTIVATED
ACCOUNT_STATUS_DEACTIVATED
```

Critical rule:

```text
recipient must never be sent to /dashboard/accounts
unless that recipient actually has Account Management access
```

For a normal account receiving notification about itself:

```text
→ profile/home/authorized account-status information
```

For Admin/manager notification about another account:

```text
→ exact account management record if authorized
```

Audit producer and permission matrix before choosing route.

---

# 31. SYSTEM / UNKNOWN / LEGACY NOTIFICATION

Current NotificationTypes include:

```text
SYSTEM_ALERT
```

If no safe structured destination exists:

```text
→ NotificationDetailModal
```

or:

```text
→ /notifications
```

Do not manufacture a business destination.

---

# 32. COVERAGE GAP GUARD — EVENT KEY KHÔNG ĐƯỢC THIẾU

Create automated inventory/guard.

Goal:

```text
every CURRENT notification producer
→ known eventKey OR explicitly documented legacy exception
```

and:

```text
every known eventKey
→ VI locale exists
→ EN locale exists
→ navigation classification exists
```

A future developer adding a producer should fail test if they add:

```text
new notification
MetadataJson = null
```

without an approved exception.

---

# 33. BACKEND ACTION TYPE CLEANUP

Current action types are coarse:

```text
OPEN_VISIT_DETAIL
OPEN_VISIT_INVITATION
OPEN_HOST_FEEDBACK_MODAL
OPEN_VISITOR_FEEDBACK_MODAL
OPEN_LOGISTICS_DETAIL
OPEN_HANDOVER_DETAIL
OPEN_NEWS_DETAIL
OPEN_PARTNER_DETAIL
OPEN_ACCOUNT_DETAIL
OPEN_NOTIFICATION_PAGE
```

Long-term recommended additions:

```text
OPEN_CAMPUS_REVIEW
OPEN_VISIT_HISTORY
OPEN_VISIT_DETAIL
OPEN_VISIT_READONLY_DETAIL
OPEN_HOST_PROCESS
OPEN_VISIT_INVITATION
OPEN_CONTRIBUTION
OPEN_AGENDA_DETAIL
OPEN_MINUTES_DETAIL
OPEN_ACTION_ITEM_DETAIL
```

Do not create duplicate names if equivalent action type already exists on latest HEAD.

---

# 34. ACTION TYPE VÀ EVENT KEY KHÔNG GIỐNG NHAU

Correct model:

```text
eventKey = WHAT HAPPENED
actionType = WHERE/WHY CLICK SHOULD GO
```

Example:

```text
eventKey = VISIT_REQUEST_UPDATED_PENDING
actionType = OPEN_VISIT_HISTORY
```

Example:

```text
eventKey = VISIT_REQUEST_WAITING_APPROVAL
actionType = OPEN_CAMPUS_REVIEW
```

Example:

```text
eventKey = HOST_ASSIGNED
actionType = OPEN_HOST_PROCESS
```

---

# 35. STRUCTURED IDS LÀ SOURCE OF TRUTH CHO TARGET

Priority:

```text
relatedType
relatedId
visitRequestId
visitInstanceId
campusId
```

For domains needing additional IDs, either:

```text
relatedId
```

must carry exact domain ID, or DTO needs explicit typed ID.

Do not parse:

```text
requestCode
title
message
URL path string
```

to reconstruct ID unless legacy compatibility absolutely requires it and is safely provable.

---

# 36. LEGACY NOTIFICATION COMPATIBILITY

Old DB rows may have:

```text
MetadataJson = null
old ActionUrl
generic OPEN_VISIT_DETAIL
missing visitInstanceId
```

Must not break them.

Fallback pipeline:

```text
known eventKey
→ explicit actionType
→ relatedType + relatedId
→ structured visit IDs
→ legacy targetUrl rewrite
→ detail modal
```

For old visit notification that only has:

```text
visitRequestId
```

open request detail, not arbitrary campus mutation.

If it cannot identify exact campus:

```text
do not guess campus
```

---

# 37. BACKFILL POLICY

Optional SQL backfill is allowed only if semantic can be reconstructed with high confidence from:

```text
notification_type
related_type
related_id
visit_request_id
visit_instance_id
campus_id
action_type
metadata already present
```

Do NOT backfill based on:

```text
LIKE '%đã cập nhật%'
LIKE '%cần duyệt%'
```

unless there is no ambiguity and it is explicitly reviewed.

Historical ambiguous rows stay legacy.

---

# 38. MARK-AS-READ KHÔNG ĐƯỢC THAY ĐỔI DESTINATION

Current `NotificationsContext.markAsRead()` already:

```text
optimistically updates state
catches API failure internally
```

Therefore click routing should remain independent.

Required behavior:

```text
mark read success
→ navigate

mark read API failure
→ still navigate
→ log error
```

Do not block business navigation because a cosmetic read-state request failed.

---

# 39. BELL / PAGE / DASHBOARD / CALENDAR PHẢI DÙNG CÙNG RESOLVER

Known surfaces calling notification routing include:

```text
NotificationBellButton
NotificationsPage
SharedDashboardView
StaffCalendarTab
StaffDashboardCalendar
```

Audit repo-wide for more.

Expected:

```text
same NotificationItem
+
same user/current state

Bell click
NotificationsPage click
Dashboard change-notification click
Calendar notification click

→ same semantic destination
```

Only shell-specific behavior may differ:

```text
Bell closes its dropdown
Dashboard closes its local popover
```

Business target must be same.

---

# 40. KHÔNG IMPORT ROUTING TỪ UI COMPONENT

After refactor, this should disappear:

```ts
import { getNotificationLink } from '../components/NotificationBellButton';
```

Instead:

```ts
import { resolveNotificationDestination } from '../utils/resolveNotificationDestination';
```

---

# 41. NOTIFICATION DESTINATION OBJECT

Prefer typed result instead of returning raw string for every case.

Example conceptual:

```ts
type NotificationDestination =
  | {
      kind: 'VISIT_COMMAND';
      intent: 'REVIEW' | 'HISTORY' | 'DETAIL' | 'HOST' | 'INVITATION';
      visitRequestId: number;
      visitInstanceId?: number | null;
      notificationId: number;
    }
  | {
      kind: 'FEEDBACK_MODAL';
      modal: 'HOST' | 'VISITOR';
      visitInstanceId: number;
    }
  | {
      kind: 'ROUTE';
      path: string;
    }
  | {
      kind: 'DETAIL_MODAL';
    };
```

Benefits:

```text
routing is testable without React
semantic intent cannot disappear inside URL string
Bell/Page reuse same result
```

---

# 42. PERMISSION RULE

Notification creation does not grant permission.

On click:

```text
notification recipient at creation time
!=
permission guaranteed forever
```

If permission changed:

```text
resolve current state
→ safe fallback
```

Normal intended flow must not produce:

```text
recipient
→ click fresh notification
→ 403
```

Every producer must be cross-checked against destination access.

---

# 43. STALE NOTIFICATION CASES BẮT BUỘC

Test all:

## 43.1 Review already processed

```text
old notification says "needs review"
current state = approved
→ current detail
→ no Approve
```

## 43.2 Host changed

```text
old HOST_ASSIGNED
current user no longer Host
→ detail
→ no Host mutation UI
```

## 43.3 Invitation already declined

```text
old PARTICIPATION_INVITED
current invitation = DECLINED
→ invitation detail
→ no Accept unless backend actually allows reopening
```

## 43.4 Logistics already done

```text
old "needs action"
current logistics = DONE
→ read-only/current item
```

## 43.5 Entity removed/not accessible

```text
→ clear error
→ no guessed fallback into unrelated record
```

---

# 44. MULTI-CAMPUS RULES

If notification is campus-specific:

```text
visitInstanceId is REQUIRED for new notification
campusId should be present
```

Do not open:

```text
first campus in list
```

when notification is about campus B.

If request-level event:

```text
visitInstanceId may be null
```

Then destination must stay request-level.

---

# 45. HISTORY / DETAIL IS NOT APPROVAL

Do not conflate:

```text
VIEW_CHANGE_HISTORY
VIEW_REQUEST_DETAIL
APPROVE_AND_ASSIGN_HOST
```

They are separate permissions/capabilities.

A notification describing:

```text
update
amendment result
host changed
cancelled
decision completed
```

normally opens:

```text
DETAIL/HISTORY
```

not an action modal.

---

# 46. MODAL LIFECYCLE REQUIREMENTS

For every notification-opened modal:

```text
open
close button
backdrop close if supported
Escape if supported
reopen same notification
navigate away
back
```

must not:

```text
replay stale command
remain stuck open
fail second click
double-open under StrictMode
```

---

# 47. URL HISTORY REQUIREMENTS

Notification one-shot must use:

```text
replace
```

when consuming command.

User Back must not replay:

```text
Approve modal
Feedback modal
History auto-scroll command
```

unless user explicitly clicks notification again.

Persistent filters such as:

```text
visitRequestId
tab
page
```

must remain semantically different from one-shot commands.

---

# 48. BROWSER REFRESH CASE

If user refreshes while one-shot URL is still present before consume:

```text
→ consume once
→ open intended target
→ strip command
```

If user refreshes after command stripped:

```text
→ no replay
```

---

# 49. DOUBLE CLICK / RAPID CLICK

Click notification rapidly twice.

Expected:

```text
one destination
no duplicate modal
no duplicate mutation
```

Do not solve second-click bug by allowing concurrent duplicate commands.

Use interaction lock only for same active navigation cycle if necessary.

---

# 50. NETWORK FAILURE CASES

## Notification list fetch fails

```text
bell remains usable
show error/empty-safe UI
do not crash route
```

## Resolve current visit state fails

```text
show "Không thể mở nội dung từ thông báo..."
no guessed action
```

## Mark read fails

```text
navigation still happens
```

## Target entity API returns 403

```text
show no-access state
do not redirect into unrelated content
```

---

# 51. EVENT PRESENTATION PHẢI KHỚP ROUTING

Example:

```text
title: Visitor đã cập nhật đơn đăng ký tham quan
message: Vui lòng xem lại thông tin mới nhất...
```

Action intent cannot be:

```text
APPROVE_IMMEDIATELY
```

Add semantic consistency tests:

```text
UPDATED_PENDING
→ title says updated
→ intent HISTORY
```

```text
WAITING_APPROVAL
→ title says needs review
→ intent REVIEW
```

---

# 52. I18N

Routing must not depend on active language.

Same notification:

```text
VI
EN
```

must produce same destination.

Test:

```text
switch language
→ title/message rerender
→ semantic intent unchanged
```

---

# 53. PHASED IMPLEMENTATION

## P0 — Immediate second-click fix

Scope:

```text
VisitRequestManagement.tsx
deep-link regression test
```

Do:

```text
reset consumed command guard when command absent
pin StrictMode behavior
pin second-click behavior
```

Do not touch backend.

---

## P1 — Fix UPDATED_PENDING semantic mismatch

Scope:

```text
notification semantic parser
destination resolver
VisitRequestManagement
V2 detail history anchor
UpdatePending notification mapping tests
```

Expected:

```text
VISIT_REQUEST_UPDATED_PENDING
→ HISTORY
```

---

## P2 — Centralize frontend notification routing

Move logic out of:

```text
NotificationBellButton
```

Migrate:

```text
Bell
NotificationsPage
SharedDashboardView
StaffCalendarTab
StaffDashboardCalendar
all repo-wide consumers
```

---

## P3 — Full eventKey routing matrix

Audit every current `NotificationEventKeys` constant and every notification producer.

No unclassified current event.

Classification:

```text
REVIEW
HISTORY
DETAIL
READONLY_DETAIL
HOST_PROCESS
INVITATION
CONTRIBUTION
LOGISTICS_DETAIL
HANDOVER_DETAIL
AGENDA_DETAIL
MINUTES_DETAIL
ACTION_ITEM_DETAIL
NEWS_DETAIL
PARTNER_DETAIL
ACCOUNT_DETAIL
FEEDBACK_MODAL
DETAIL_MODAL
```

---

## P4 — Backend semantic action types

Add action types only after frontend compatibility layer is stable.

Update producers.

Do not remove legacy support in same change.

---

## P5 — Event-key coverage gaps

Audit NotificationTypes whose producers may still lack semantic eventKey:

```text
AGENDA_REQUIRED
MINUTES_CREATED
LOGISTICS_READY
LOGISTICS_DONE
LOGISTICS_HANDOVER_REQUIRED
LOGISTICS_DUE_SOON
LOGISTICS_OVERDUE
SYSTEM_ALERT
```

Add semantic EventKeys/locales where current producers exist.

---

## P6 — Legacy/backfill

Only safe reconstructable rows.

Optional, not blocker for frontend correctness.

---

# 54. FILES CẦN AUDIT / CÓ KHẢ NĂNG SỬA

## Frontend core

```text
frontend/pems-react/src/features/notifications/types/notification.types.ts
frontend/pems-react/src/features/notifications/context/NotificationsContext.tsx
frontend/pems-react/src/features/notifications/components/NotificationBellButton.tsx
frontend/pems-react/src/features/notifications/components/NotificationDetailModal.tsx
frontend/pems-react/src/features/notifications/utils/resolveNotificationPresentation.ts

NEW candidate:
frontend/pems-react/src/features/notifications/utils/notificationSemantic.ts
frontend/pems-react/src/features/notifications/utils/resolveNotificationDestination.ts
frontend/pems-react/src/features/notifications/hooks/useNotificationNavigator.ts
```

Do not create all 3 new files if architecture can stay simpler; avoid needless abstraction.

## Notification surfaces

```text
frontend/pems-react/src/pages/notifications/NotificationsPage.tsx
frontend/pems-react/src/pages/dashboard/departments/SharedDashboardView.tsx
frontend/pems-react/src/pages/dashboard/department-staff/StaffCalendarTab.tsx
frontend/pems-react/src/pages/dashboard/home/staff-calendar/StaffDashboardCalendar.tsx
```

Search repo for every:

```text
getNotificationLink
handleItemClick
actionType ===
notificationId
NotificationItem
```

## Visit resolution

```text
frontend/pems-react/src/pages/dashboard/visit/VisitRequestManagement.tsx
frontend/pems-react/src/features/visit-request/components/v2/VisitRequestV2DetailView.tsx
frontend/pems-react/src/features/visit-request/components/v2/shared/VisitSectionCard.tsx
frontend/pems-react/src/features/visit-request/utils/visitVersionRouting.ts
```

## Backend contracts

```text
backend/PEMS.Application/Notifications/Common/NotificationConstants.cs
backend/PEMS.Application/Notifications/Common/NotificationEventKeys.cs
backend/PEMS.Application/Notifications/Common/NotificationDto.cs
backend/PEMS.Application/Notifications/Common/NotificationService.cs
```

## Visit producers

At minimum:

```text
V2CreateNotifier.cs
UpdatePendingVisitRequestV2CommandHandler.cs
OperationalContactNotifier.cs
CampusApprovalExecutor.cs
RejectCampusInstanceCommandHandler.cs
CancelVisitRequestCommandHandler.cs
TransferVisitHostCommandHandler.cs
VisitAmendmentHandlers.cs
ProposedHostNotifier.cs
CompleteVisitStageCommandHandler.cs
HoUnprocessedCampusAlertHostedService.cs
```

## Other producer groups

Repo-wide search:

```text
CreateNotificationRequest(
CreateNotificationItem(
CreateManyAsync(
CreateAsync(
NotificationTypes.
NotificationEventKeys.
NotificationActionTypes.
ActionUrl:
MetadataJson:
```

Include:

```text
Participation
DepartmentReceptionTasks
Logistics
Agenda
Minutes
Action items
News
Partners
Accounts
Background reminders/jobs
```

---

# 55. AUTOMATED TESTS — MINIMUM REQUIRED

## Unit — semantic parser

```text
valid metadata
invalid JSON
unknown eventKey
missing params
legacy metadata null
```

## Unit — intent resolver

Pin every eventKey.

Minimum:

```text
VISIT_REQUEST_WAITING_APPROVAL → REVIEW
VISIT_REQUEST_UPDATED_PENDING → HISTORY
VISIT_REQUEST_RESUBMITTED → HISTORY
HOST_ASSIGNED → HOST
HOST_TRANSFER_OUTGOING → DETAIL
PARTICIPATION_INVITED → INVITATION
CAMPUS_REJECTED → DETAIL
VISIT_CANCELLED_BY_HOST → READONLY_DETAIL
NEWS_PENDING_APPROVAL → NEWS_DETAIL
PARTNER_PENDING_APPROVAL → PARTNER_DETAIL
```

## Unit — no language dependency

```text
same item VI/EN → same destination
```

---

# 56. DEEP-LINK REGRESSION TESTS

## DL-01 same notification second click

```text
open
close
same notification click again
→ opens again
```

## DL-02 StrictMode

```text
one command
→ one open
```

## DL-03 command strip

After consume:

```text
URL no openVisitRequestId
URL no openVisitInstanceId
URL no notificationIntent
```

## DL-04 filter/page change

After modal closed:

```text
search/filter/tab/page
→ no replay
```

## DL-05 browser refresh after consume

```text
→ no replay
```

---

# 57. CURRENT-STATE ROUTING TESTS

## CR-01 pending review

```text
event REVIEW
current pending
allowed APPROVE
→ approval flow
```

## CR-02 stale review

```text
event REVIEW
current approved
→ detail
```

## CR-03 updated but pending

```text
event HISTORY
current pending
allowed APPROVE
→ HISTORY
→ NOT approval modal
```

This test directly prevents current bug from returning.

## CR-04 old Host

```text
event HOST
current user not Host
→ detail
```

## CR-05 invitation answered

```text
event INVITATION
current declined
→ current invitation detail
```

---

# 58. SURFACE CONSISTENCY TEST

For same mock NotificationItem:

```text
Bell
NotificationsPage
SharedDashboardView
StaffCalendarTab
StaffDashboardCalendar
```

assert same resolved destination object.

UI close behavior can differ.

---

# 59. ROLE MATRIX

At minimum test:

```text
VISITOR
STUDENT
STAFF regular
STAFF_LEADER
DEPARTMENT_STAFF
DEPARTMENT_LEAD
HO
ADMIN
```

For each role, select relevant notifications only.

Do not fabricate notification scenarios a role never receives.

---

# 60. REAL-STACK SCENARIOS

Use real backend + DB + frontend if tooling permits.

## RS-01 Staff Leader new request

```text
create request
→ waiting approval notification
→ click
→ exact campus review
```

## RS-02 Staff Leader updated pending

```text
Visitor edits pending request
→ Staff Leader receives UPDATED_PENDING
→ click
→ detail/history
→ NOT approval popup
```

## RS-03 second click

```text
same UPDATED_PENDING notification
→ click
→ close
→ click again
→ opens again
```

## RS-04 stale review

```text
Staff Leader A receives review notification
Staff Leader/process decides it elsewhere
→ click old notification
→ current detail
```

## RS-05 Host assigned

```text
Host notification
→ Host Process
```

Transfer Host away:

```text
click old HostAssigned
→ detail fallback
```

## RS-06 Visitor campus rejected

```text
→ exact campus rejected state + reason
```

## RS-07 participant invitation

```text
Student/Staff
→ invitation context
```

## RS-08 logistics

```text
exact logistics item
```

## RS-09 News/Partner

```text
exact entity
```

---

# 61. MANUAL UX CHECK

Notification row should answer 3 questions before click:

```text
1. Chuyện gì vừa xảy ra?
2. Nó liên quan tới request/campus/entity nào?
3. Tôi cần hành động hay chỉ cần xem?
```

Examples:

Bad:

```text
Có thông báo mới
```

Good:

```text
Visitor đã cập nhật đơn đăng ký tham quan
OPC-01-SELF-SINGLE đã có thay đổi. Vui lòng xem lại thông tin mới nhất.
```

Badge:

```text
Cần hành động
```

chỉ khi notification truly action-required at creation.

But click still checks current state.

---

# 62. BUSINESS LOGIC PRESERVATION

Notification routing refactor must NOT change:

```text
approval authorization
host eligibility
amendment business rules
visit status transitions
participant invitation rules
logistics workflow
permissions
allowedActions backend computation
database mutation behavior
```

It changes:

```text
how an existing notification resolves to an existing screen/context
```

Backend actionType/eventKey additions are metadata/navigation contracts, not permission grants.

Every mutation remains server-authorized.

---

# 63. KHÔNG ĐƯỢC LÀM

Do NOT:

```text
parse title/message to choose route
hardcode Vietnamese text checks
route all VISIT notifications to approval
route all STAFF notifications to Host Process
trust ActionUrl over current semantic metadata
guess first campus when instanceId missing
add frontend permission guesses instead of allowedActions
keep one-shot command forever
remove StrictMode protection entirely
use timeout hacks to reset command
backfill ambiguous DB rows by string LIKE
silently swallow unsupported event into unrelated route
```

---

# 64. DEFINITION OF DONE

Task chỉ hoàn tất khi:

- [ ] Latest `Canh_iter3_FixBug` HEAD re-audited before implementation.
- [ ] Same notification can be opened, closed, and opened again.
- [ ] StrictMode does not double-open.
- [ ] One-shot params are removed after consume.
- [ ] Search/filter/page changes do not replay old notification.
- [ ] `VISIT_REQUEST_UPDATED_PENDING` never auto-opens approval modal.
- [ ] `VISIT_REQUEST_WAITING_APPROVAL` opens exact campus review when still valid.
- [ ] Stale review opens current detail, not stale action.
- [ ] Host assignment respects current Host relationship.
- [ ] Invitation notification opens invitation context.
- [ ] Campus rejected/approved opens exact current campus detail.
- [ ] Cancelled/closed notifications are read-only/current-state safe.
- [ ] Logistics routes exact item.
- [ ] News routes exact record.
- [ ] Partner routes exact record.
- [ ] Account notification never routes recipient to unauthorized Account Management.
- [ ] Bell and NotificationsPage behave identically for same item.
- [ ] Dashboard/calendar notification surfaces use same resolver.
- [ ] All current eventKeys have navigation classification.
- [ ] All current eventKeys have VI + EN presentation.
- [ ] Current notification producers missing eventKey are identified and fixed/documented.
- [ ] All target IDs required by each event are present or safe fallback documented.
- [ ] Legacy notifications still work.
- [ ] No business permission/workflow regression.
- [ ] Relevant unit/integration/E2E tests pass.
- [ ] Manual real-stack scenarios documented.

---

# 65. FINAL REPORT FORMAT

Agent phải báo đúng format:

```text
Branch:
HEAD before:
HEAD after:

CONFIRMED ROOT CAUSES:
RC-01:
RC-02:
...

FILES CHANGED:
Frontend:
Backend:
Tests:
SQL/backfill:

SEMANTIC ROUTING MATRIX:
Event | Producer | Recipient | Intent | Structured IDs | Current-state fallback | Destination | Tests

SECOND-CLICK:
Before:
After:
StrictMode protection:
Replay protection:

UPDATED_PENDING:
Before destination:
After destination:
Proof it cannot auto-open approval:

LEGACY:
Rows covered:
Rows not reconstructable:
Fallback:

ROLE COVERAGE:
Visitor:
Student:
Staff:
Staff Leader:
Department Staff:
Department Lead:
HO:
Admin:

TESTS:
lint:
unit:
integration:
responsive:
E2E:
real-stack:

NOT VERIFIED:
...

KNOWN REMAINING BUGS:
...

FINAL CONCLUSION:
```

Không được ghi:

```text
100% fixed
```

nếu event/role/producer vẫn chưa audit.

---

# 66. PROMPT THỰC THI CHO AGENT

Copy nguyên khối dưới đây cho coding agent:

```text
@GitHub

Hãy implement kế hoạch notification trong file:

PEMS_Notification_Second_Click_Semantic_Routing_Full_Fix_Plan.md

Source of Truth:
repository quangthoai04/PEMS
branch Canh_iter3_FixBug

BẮT BUỘC trước khi sửa:
1. checkout/pull đúng branch;
2. git rev-parse HEAD;
3. báo HEAD;
4. đọc code hiện tại, không tin HEAD ghi trong plan nếu branch đã thay đổi.

Mục tiêu chính:

A. Fix triệt để lỗi:
click notification lần 1 mở được
→ close
→ click SAME notification lần 2 phải mở lại.

B. Fix semantic routing:
notification phải mở theo Ý NGHĨA EVENT,
không để current pending state tự nâng mọi Visit notification thành approval popup.

Case bắt buộc:

VISIT_REQUEST_UPDATED_PENDING
→ V2 detail / history
→ KHÔNG approval modal.

VISIT_REQUEST_WAITING_APPROVAL
→ exact campus review nếu CURRENT vẫn pending + allowed
→ nếu stale thì current detail.

Không broad refactor ngoài notification architecture cần thiết.

BẮT BUỘC:
- tách routing khỏi NotificationBellButton;
- reuse một semantic resolver cho Bell / NotificationsPage / dashboard/calendar surfaces;
- giữ presentation resolver và navigation dựa trên cùng eventKey parser;
- one-shot command phải carry intent;
- consume + strip URL;
- reset StrictMode command guard đúng lifecycle;
- không replay sau filter/page/search;
- không parse title/message;
- không guess campus;
- current state được downgrade intent nhưng KHÔNG được upgrade intent;
- legacy notification vẫn hoạt động.

Audit repo-wide tất cả notification producers:
CreateNotificationRequest
CreateNotificationItem
CreateAsync
CreateManyAsync
NotificationTypes
NotificationEventKeys
NotificationActionTypes
MetadataJson
ActionUrl

Lập full matrix:
Event
Producer
Recipient
EventKey
Target IDs
Action intent
Destination
Stale fallback
Permission
Test

Không được bỏ:
Visit
Operational Contact
Amendment
Host
Participation
HO visibility
Logistics
Agenda
Minutes
Action Item
News
Partner
Feedback
Reminder
Account
System/legacy.

Đặc biệt audit mismatch:
NotificationTypes có type nhưng NotificationEventKeys chưa chắc có event tương ứng.

Tests bắt buộc:
- same notification second click;
- StrictMode no double-open;
- one-shot strip;
- no replay on filters/pages;
- UPDATED_PENDING never approval;
- WAITING_APPROVAL current vs stale;
- Host current vs old Host;
- invitation current vs answered;
- Bell vs NotificationsPage same destination;
- role/permission safety;
- legacy fallback.

Sau implementation chạy các gate hiện có phù hợp:
npm run lint
npm run test:unit
npm run build
notification-specific tests
relevant integration tests
relevant Playwright/E2E

Không được kill Chrome cá nhân của user.
CẤM:
taskkill /F /IM chrome.exe
Stop-Process -Name chrome
pkill chrome
killall chrome
hoặc lệnh broad process kill tương đương.

Chỉ cleanup browser process do chính Playwright run tạo ra khi nhận diện chắc chắn.

Cuối cùng báo report đúng format ở §65.
Không commit/push nếu user chưa yêu cầu.
```

---

# 67. KẾT LUẬN KIẾN TRÚC

Sau fix, notification flow phải trở thành:

```text
EVENT
  ↓
semantic eventKey
  ↓
presentation + intent
  ↓
structured target
  ↓
current state
  ↓
current permission
  ↓
same semantic destination everywhere
  ↓
one-shot consumed
  ↓
close
  ↓
same notification can be clicked again
```

Điểm mấu chốt:

```text
Notification semantic decides WHY/WHAT CONTEXT to open.

Current backend state decides WHAT IS TRUE NOW.

Current authorization decides WHAT USER MAY DO NOW.

Current state may reduce an old action.
It must never transform a read/history notification
into a stronger mutation/review action.
```

Đó là nguyên tắc phải giữ cho mọi notification hiện tại và notification mới về sau.
