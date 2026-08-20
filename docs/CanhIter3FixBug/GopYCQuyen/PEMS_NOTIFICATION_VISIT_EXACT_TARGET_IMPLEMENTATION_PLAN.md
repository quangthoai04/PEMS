# PEMS — KẾ HOẠCH TRIỂN KHAI FIX NOTIFICATION VISIT EXACT TARGET SYSTEM-WIDE

> **Ngày lập kế hoạch:** 20/08/2026  
> **Repository:** `quangthoai04/PEMS`  
> **Branch làm việc hiện tại:** `Dev`  
> **HEAD tham chiếu khi lập kế hoạch:** `fd94bb3fc3a780f5d0e77566fbd902d1af00198c`  
> **Commit notification gần nhất cần đặc biệt lưu ý:** `e62f9e915d0efd02931512f35fe4c896824ead58` — `fix redirect notification`
>
> **Mục tiêu:** sửa triệt để lỗi notification Visit mở sai/không mở được target sau deploy, đặc biệt case Visitor/HO multi-campus, nhưng **không thay đổi business permission của các role hiện tại** và không gây regression Staff/Staff Leader/Department/Student.

---

# 1. BỐI CẢNH VÀ TRIỆU CHỨNG

Hiện tại khi Visitor bấm một số notification như:

- `Cơ sở đã tiếp nhận yêu cầu`
- `Cơ sở từ chối tiếp nhận`
- `Host phụ trách chuyến thăm đã thay đổi`

hệ thống có thể báo:

```text
Không tìm thấy đúng cơ sở được nhắc trong thông báo — có thể đã thay đổi.
```

Trong các case đã kiểm tra, dữ liệu notification vẫn có:

```text
VisitRequestId
VisitInstanceId
CampusId
eventKey
ActionType
ActionUrl
```

Vấn đề không nằm ở việc producer làm mất target.

Root cause chính là:

```text
Notification target = exact VisitInstance / exact campus
        ↓
Frontend semantic routing giữ exact VisitInstanceId
        ↓
VisitRequestManagement dùng management-list API để resolve target
        ↓
Visitor/HO multi-campus được backend trả dưới dạng request-summary
        ↓
top-level visitInstanceId = null
exact instance nằm trong campusProgressItems
        ↓
frontend chỉ tìm items[].visitInstanceId
        ↓
FALSE NEGATIVE
        ↓
"Không tìm thấy đúng cơ sở..."
```

---

# 2. NGUYÊN TẮC KIẾN TRÚC SAU KHI SỬA

Không sửa theo kiểu đặc biệt cho từng role:

```ts
if (role === 'VISITOR') {
  // special case
}
```

Không chọn đại:

```ts
items[0]
```

Không bỏ `VisitInstanceId` khỏi notification.

Không dùng `targetUrl` cũ làm nguồn chân lý duy nhất.

Luồng chuẩn phải là:

```text
WHAT HAPPENED
eventKey / actionType
        ↓
WHAT CLICK MEANS
Notification Intent
        ↓
WHAT RECORD
VisitRequestId + VisitInstanceId + CampusId
        ↓
WHAT IS TRUE NOW
Backend exact-target resolver
        ↓
Current lifecycle
Current relation
Current authorization
        ↓
WHERE USER SHOULD GO
Correct destination
```

Nguyên tắc quan trọng:

> **Notification Intent là mức trần quyền tương tác.**

Current state chỉ được:

```text
KEEP
hoặc
DOWNGRADE
```

Không được:

```text
UPGRADE
```

Ví dụ:

```text
VISIT_HISTORY
```

không được tự biến thành:

```text
VISIT_REVIEW
```

chỉ vì Staff Leader hiện đang có quyền duyệt.

---

# 3. PHẠM VI FIX

## 3.1. P0 — FIX BUG DEPLOY HIỆN TẠI

P0 chỉ tập trung vào notification Visit exact-target.

### P0 phải làm

- thêm backend exact notification Visit target resolver;
- frontend Visit notification dùng resolver này;
- giữ semantic classifier hiện tại;
- giữ permission/business rule hiện tại;
- đảm bảo Visitor multi-campus mở được đúng campus;
- đảm bảo HO multi-campus không bị lỗi tương tự;
- đảm bảo Staff/Staff Leader không mở nhầm campus;
- đảm bảo stale notification được downgrade theo quyền hiện tại;
- bổ sung regression test theo production data shape thật.

### P0 KHÔNG làm

Không thay đổi:

- quyền Visitor;
- quyền HO;
- quyền Staff Leader;
- quyền Staff;
- quyền Department;
- quyền Student;
- tab `responsible`;
- tab `registered`;
- tab `attending`;
- tab `hosted`;
- tab `all`;
- logic approve campus;
- logic reject campus;
- logic assign host;
- logic transfer host;
- logic invitation;
- logic contribution;
- logic tạo notification;
- logic News/Partner/Feedback/Logistics/Handover ngoài phần route gate cần thiết.

---

# 4. SOURCE OF TRUTH BẮT BUỘC

Trước khi code:

```bash
git checkout Dev
git pull
git rev-parse HEAD
git log -1 --oneline
```

Ghi lại:

```text
Branch:
HEAD:
Commit message:
```

Nếu HEAD khác với SHA trong tài liệu này thì phải đọc lại source mới nhất trước khi triển khai.

Các file cần đọc lại trước khi sửa:

## Frontend notification

```text
frontend/pems-react/src/features/notifications/components/NotificationBellButton.tsx
frontend/pems-react/src/pages/notifications/NotificationsPage.tsx
frontend/pems-react/src/features/notifications/utils/notificationSemantic.ts
frontend/pems-react/src/features/notifications/utils/resolveNotificationDestination.ts
frontend/pems-react/src/features/notifications/types/notification.types.ts
frontend/pems-react/src/features/notifications/context/NotificationsContext.tsx
```

## Frontend Visit destination

```text
frontend/pems-react/src/pages/dashboard/visit/VisitRequestManagement.tsx
frontend/pems-react/src/features/delegations/api/delegationsApi.ts
frontend/pems-react/src/features/delegations/types/delegations.types.ts
frontend/pems-react/src/App.tsx
```

## Backend Visit list / relation

```text
backend/PEMS.Application/Delegations/Queries/ViewGuestDelegationList/ViewGuestDelegationListQuery.cs
backend/PEMS.Application/Delegations/Queries/ViewGuestDelegationList/ViewGuestDelegationListQueryHandler.cs
backend/PEMS.Application/Delegations/Queries/ViewGuestDelegationList/ViewGuestDelegationListDto.cs
backend/PEMS.Api/Controllers/DelegationsController.cs
```

## Backend notification

```text
backend/PEMS.Application/Notifications/Common/NotificationConstants.cs
backend/PEMS.Application/Notifications/Common/NotificationEventKeys.cs
backend/PEMS.Application/Notifications/Common/NotificationService.cs
backend/PEMS.Application/Notifications/Common/NotificationDto.cs
```

## Tests hiện tại

```text
frontend/pems-react/src/pages/dashboard/visit/__tests__/VisitRequestManagementNotificationDeepLink.test.tsx
frontend/pems-react/src/features/notifications/utils/__tests__/resolveNotificationDestination.test.ts
frontend/pems-react/tests-realstack/notification-routing.realstack.spec.ts

tests/PEMS.IntegrationTests/VisitRequests/RelationFilterEntryContextTests.cs
tests/PEMS.IntegrationTests/VisitRequests/MergeCrossBranchContractTests.cs
tests/PEMS.UnitTests/Delegations/ViewGuestDelegationList/ViewGuestDelegationListQueryHandlerTests.cs
```

---

# 5. THIẾT KẾ P0 — EXACT NOTIFICATION VISIT TARGET RESOLVER

## 5.1. Không tiếp tục dùng management list làm exact-target resolver

Không dùng:

```text
GET management-list?visitRequestId=N
```

sau đó:

```ts
items.find(x => x.visitInstanceId === notification.visitInstanceId)
```

vì management list là API phục vụ **hiển thị danh sách**, có aggregation/merge.

Notification cần **exact business target**.

---

# 6. BACKEND — CONTRACT MỚI

Khuyến nghị thêm một endpoint riêng.

Ví dụ:

```http
GET /api/delegations/notification-target
    ?visitRequestId=123
    &visitInstanceId=456
```

Hoặc route tương đương phù hợp convention hiện tại:

```http
GET /api/delegations/notification-visit-target
```

Tên cụ thể có thể điều chỉnh theo codebase, nhưng không được nhồi behavior đặc biệt vào list endpoint nếu không cần thiết.

---

# 7. BACKEND — REQUEST DTO

Ví dụ:

```csharp
public sealed record ResolveNotificationVisitTargetQuery(
    ulong VisitRequestId,
    ulong? VisitInstanceId
) : IRequest<NotificationVisitTargetDto>;
```

Không nhận từ frontend:

```text
roleCode
subRole
campusId của user
allowedActions
```

Các dữ liệu đó phải lấy từ:

```text
ICurrentUserService
+
database current state
```

---

# 8. BACKEND — RESPONSE DTO ĐỀ XUẤT

```csharp
public sealed class NotificationVisitTargetDto
{
    public ulong VisitRequestId { get; init; }
    public ulong? VisitInstanceId { get; init; }
    public ulong? CampusId { get; init; }

    public string? RequestStatus { get; init; }
    public string? CampusStatus { get; init; }

    public bool CanViewRequestDetail { get; init; }

    public List<string> AllowedActions { get; init; } = new();

    public List<VisitRelationContextDto> RelationContexts { get; init; } = new();

    public string? PrimaryEntryContext { get; init; }
    public ulong? PrimaryEntryVisitInstanceId { get; init; }

    public ulong? ParticipantId { get; init; }
    public string? ParticipantStatus { get; init; }

    public bool Exists { get; init; }
    public bool HasAccess { get; init; }

    public string? SafeFallback { get; init; }
}
```

Có thể bổ sung:

```text
delegationName
campusName
requestCode
```

nếu frontend cần hiển thị context.

---

# 9. BACKEND — VALIDATION EXACT TARGET

Nếu có `VisitInstanceId`:

```text
1. Load request theo VisitRequestId.
2. Load EXACT VisitRequestCampus:
   VisitRequestId == requestId
   AND
   VisitInstanceId == instanceId.
3. Nếu không tồn tại:
   return NOT_FOUND / target missing.
4. Không fallback sang sibling campus.
5. Không chọn instance đầu tiên.
```

Sai:

```csharp
var instance = request.CampusInstances.First();
```

Đúng:

```csharp
var instance = request.CampusInstances
    .SingleOrDefault(x => x.VisitInstanceId == query.VisitInstanceId);
```

Nếu `VisitInstanceId == null`:

- resolve request-level target;
- không đoán campus nếu multi-campus;
- chỉ mở request-level safe destination.

---

# 10. BACKEND — REUSE AUTHORIZATION, KHÔNG COPY BUSINESS RULE

Không copy toàn bộ logic relation từ:

```text
ViewGuestDelegationListQueryHandler
```

sang resolver mới.

Nên tách reusable service.

Ví dụ:

```text
IVisitRelationResolver
IVisitAuthorizationResolver
IVisitEntryContextResolver
```

Hoặc một service:

```text
VisitCurrentAccessResolver
```

Input:

```text
currentUser
request
optional exact instance
```

Output:

```text
relations
allowedActions
canViewRequestDetail
entry contexts
participant identity
```

Mục tiêu:

```text
List API
Notification Resolver
```

phải dùng chung một nguồn business authorization.

---

# 11. BACKEND — ROLE BEHAVIOR GIỮ NGUYÊN

## VISITOR

Có thể là:

```text
REGISTRANT
OPERATIONAL_CONTACT
```

Resolver chỉ cho:

- request detail;
- campus detail tương ứng nếu policy cho phép;
- feedback modal khi intent feedback;
- các self-service action đúng lifecycle nếu flow hiện tại đã có.

Không được tạo thêm quyền.

---

## HO

HO là view/monitor role.

Các event:

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

phải có ceiling:

```text
VISIT_READONLY_DETAIL
```

Không được mở:

```text
APPROVE
ASSIGN HOST
HOST PROCESS
```

---

## STAFF LEADER

Có thể đồng thời là:

```text
REGISTRANT
CAMPUS_REVIEWER
HOST
PARTICIPANT
```

Resolver phải xác định relation theo **exact instance**.

Ví dụ:

```text
HN: CAMPUS_REVIEWER
DN: PARTICIPANT
```

notification DN không được mở review HN.

---

## STAFF

Nếu current Host của exact instance:

```text
HOST_PROCESS
```

có thể mở Host Process.

Nếu từng là Host nhưng đã transfer:

```text
HOST_PROCESS notification cũ
```

phải downgrade về detail nếu vẫn có quyền đọc.

Không được dựa vào stale ActionUrl:

```text
/dashboard/visit/process/{oldInstance}
```

---

## DEPARTMENT

Giữ flow hiện tại:

```text
task
invitation
contribution
```

Không thay đổi relation rule.

---

## STUDENT

Giữ:

```text
VISIT_INVITATION
CONTRIBUTION
```

Không đưa vào Host/Review flow.

---

## ADMIN

Nếu current Visit management không support Admin thì resolver phải:

```text
return no-access / not applicable
```

Không silent navigate.

---

# 12. FRONTEND — GIỮ SEMANTIC CLASSIFIER HIỆN TẠI

Không bỏ:

```text
classifyNotificationIntent()
```

Giữ precedence:

```text
modern actionType
    >
eventKey semantic
    >
legacy fallback
```

Nhóm Visit vẫn gồm:

```text
VISIT_REVIEW
VISIT_HISTORY
VISIT_DETAIL
VISIT_READONLY_DETAIL
HOST_PROCESS
VISIT_INVITATION
CONTRIBUTION
```

Các intent khác không đi qua Visit exact resolver:

```text
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

---

# 13. FRONTEND — ONE-SHOT COMMAND VẪN CÓ THỂ GIỮ

Có thể tiếp tục dùng URL:

```text
/dashboard/visit
  ?openVisitRequestId=123
  &openVisitInstanceId=456
  &notificationIntent=VISIT_DETAIL
```

Nhưng khi `VisitRequestManagement` consume command:

### Cũ

```text
call getVisitRequestManagementList(...)
→ items.find(exact instance)
```

### Mới

```text
call getNotificationVisitTarget(
    requestId,
    instanceId
)
→ exact current target
→ route by intent + current access
```

---

# 14. FRONTEND — HÀM RESOLVE MỚI

Ví dụ:

```ts
const resolveAndOpenNotificationTarget = async (
  requestId: number,
  instanceId: number | null,
  intent: NotificationNavigationIntent | null,
) => {
  const target = await delegationsApi.resolveNotificationVisitTarget({
    visitRequestId: requestId,
    visitInstanceId: instanceId ?? undefined,
  });

  if (!target.exists) {
    showErrorToast(null, 'Nội dung được nhắc trong thông báo không còn tồn tại.');
    return;
  }

  if (!target.hasAccess) {
    showErrorToast(null, 'Bạn không còn quyền xem nội dung được nhắc trong thông báo.');
    return;
  }

  openByIntentAndCurrentState(target, intent);
};
```

---

# 15. FRONTEND — INTENT MATRIX

## VISIT_REVIEW

Chỉ được mở approval flow khi:

```text
intent == VISIT_REVIEW
AND
exact target still pending
AND
allowedActions contains APPROVE_AND_ASSIGN_HOST
AND
relation exact instance == CAMPUS_REVIEWER
```

Nếu không:

```text
safe detail
```

---

## VISIT_HISTORY

Luôn ưu tiên:

```text
request detail
#history
```

Không mở approval modal.

---

## VISIT_DETAIL

Mở safe detail của request/exact campus.

Không tự mở Host Process chỉ vì user hiện đang là Host.

---

## VISIT_READONLY_DETAIL

Ceiling read-only.

Không mở:

```text
HOST_PROCESS
CAMPUS_REVIEW
CONTRIBUTION action
```

nếu những màn đó có tính operational/mutating cao hơn intent.

---

## HOST_PROCESS

Chỉ mở Host Process khi backend xác nhận:

```text
current user vẫn là Host
exact instance đúng
lifecycle cho phép
OPEN_HOST_PROCESS được cấp
```

Nếu stale:

```text
safe detail
```

---

## VISIT_INVITATION

Ưu tiên exact:

```text
participantId
```

Nếu relation invitation không còn:

```text
safe detail
```

Không fallback sang review/host relation khác.

---

## CONTRIBUTION

Ưu tiên:

```text
participant screen
```

hoặc:

```text
/dashboard/visit/contribution/{exactInstanceId}
```

nếu backend cho phép.

Không để `primaryEntryContext` của co-existing Host/Reviewer hijack notification.

---

## LEGACY / UNKNOWN INTENT

Policy:

```text
SAFE DETAIL ONLY
```

Không mở:

```text
approval
host process
invitation
contribution
```

chỉ dựa trên current relation.

---

# 16. KHÔNG DÙNG PRIMARY ENTRY CONTEXT LÀM NGUỒN CHÂN LÝ CHO NOTIFICATION

`PrimaryEntryContext` hữu ích cho:

```text
user click normal list row
```

nhưng notification có semantic riêng.

Ví dụ cùng Staff Leader:

```text
same request
same exact campus
current PrimaryEntryContext = CAMPUS_REVIEW
```

nhưng notification:

```text
VISIT_HISTORY
```

thì vẫn phải mở history/detail.

Không được:

```text
notification
→ primaryEntryContext
→ review modal
```

---

# 17. P0 — TEST BẮT BUỘC

## T01 — Visitor multi-campus Campus Approved

Backend target:

```text
request = R1
instance = HN
```

List production shape:

```json
{
  "visitRequestId": 1,
  "visitInstanceId": null,
  "campusProgressItems": [
    { "visitInstanceId": 101 },
    { "visitInstanceId": 102 }
  ]
}
```

Expected:

```text
notification exact instance 101 opens successfully
```

Không còn toast false-negative.

---

## T02 — Visitor multi-campus Campus Rejected

Expected:

```text
opens request/campus rejection detail
```

Không mở sibling campus.

---

## T03 — Visitor Host Changed

Expected:

```text
exact changed campus
```

---

## T04 — HO multi-campus visibility

Expected:

```text
exact campus visible
read-only
no approve
no host process
```

---

## T05 — Staff Leader exact review

Notification:

```text
VISIT_REVIEW
instance HN
```

State:

```text
HN WAITING_REQUEST_APPROVAL
current user = reviewer HN
```

Expected:

```text
approve + assign host flow
```

---

## T06 — Staff Leader review notification stale

Sau notification:

```text
campus already approved by another user
```

Expected:

```text
safe detail
no approval modal
```

---

## T07 — Staff Host Assigned

State:

```text
current user still Host
```

Expected:

```text
Host Process
```

---

## T08 — Host Transfer Incoming stale

Sau đó Host bị chuyển tiếp sang người khác.

Expected:

```text
safe detail
not Host Process
```

---

## T09 — Host Transfer Outgoing

Expected:

```text
detail only
never Host Process
```

---

## T10 — Staff Leader multi-relation collision

Same user:

```text
REGISTRANT@Request
CAMPUS_REVIEWER@HN
HOST@HN
PARTICIPANT@DN
```

Test:

```text
VISIT_REVIEW@HN      → review HN
HOST_PROCESS@HN      → Host Process HN
VISIT_INVITATION@DN  → DN invitation
VISIT_HISTORY@Request → request history
```

---

## T11 — Department task

Regression only.

Expected current route unchanged.

---

## T12 — Student invitation

Regression only.

Expected current route unchanged.

---

## T13 — Unknown intent

Expected:

```text
safe detail only
```

---

## T14 — Exact instance deleted

Expected:

```text
clear error
no sibling fallback
```

---

## T15 — Exact instance no longer accessible

Expected:

```text
no-access message
no alternate powerful relation screen
```

---

## T16 — Rapid second-click race

Notification A slow.

Notification B fast.

Expected:

```text
B stays open
A cannot overwrite B
```

Giữ `notificationTargetVersionRef` hoặc equivalent stale-response guard.

---

# 18. TEST PHẢI DÙNG PRODUCTION SHAPE THẬT

Không mock multi-campus Visitor kiểu:

```json
[
  { "visitInstanceId": 101 },
  { "visitInstanceId": 102 }
]
```

nếu backend production trả:

```json
[
  {
    "visitRequestId": 1,
    "visitInstanceId": null,
    "campusProgressItems": [
      { "visitInstanceId": 101 },
      { "visitInstanceId": 102 }
    ]
  }
]
```

Regression test phải pin đúng contract thật.

---

# 19. P1 — HARDEN MULTI-RELATION MERGE

Chỉ làm sau khi P0 ổn định.

Hiện `tab=all` có thể ép nhiều relation instance-scoped vào một flat row.

Mục tiêu P1:

```text
RelationContexts
```

phải giữ identity theo:

```text
relation
scope
visitInstanceId
campusId
participantId
```

Ví dụ:

```json
[
  {
    "relation": "REGISTRANT",
    "scope": "REQUEST"
  },
  {
    "relation": "CAMPUS_REVIEWER",
    "scope": "INSTANCE",
    "visitInstanceId": 101,
    "campusId": 1
  },
  {
    "relation": "HOST",
    "scope": "INSTANCE",
    "visitInstanceId": 101,
    "campusId": 1
  },
  {
    "relation": "PARTICIPANT",
    "scope": "INSTANCE",
    "visitInstanceId": 102,
    "campusId": 2,
    "participantId": 9001
  }
]
```

Không chỉ giữ:

```text
IsCurrentUserParticipant = true
ParticipantId = ...
```

mà không biết participant thuộc campus nào.

---

# 20. P2 — NOTIFICATION READ-STATE CONSISTENCY

Hiện nhiều surface mark read trước khi biết target có mở thành công.

Cần xem xét đổi flow:

### Option A — mark read khi click, nhưng có retry-safe UX

Giữ semantics:

```text
click = read
```

nhưng nếu target fail:

- vẫn cho user retry;
- không mất target;
- hiển thị retry action rõ ràng.

### Option B — mark read sau khi successful resolve

Cần thống nhất product rule trước.

Không tự thay behavior mà chưa chốt.

---

# 21. P2 — CATEGORY / ACTION REQUIRED FILTER

Frontend đang có params:

```text
category
isActionRequired
```

Backend Notifications API hiện cần được rà để bảo đảm:

```text
filter trước pagination
```

Không được:

```text
paginate backend
→ filter frontend
```

vì sẽ sai:

```text
totalItems
totalPages
empty page
```

Đây là defect riêng, không nhét vào P0 nếu không cần.

---

# 22. FILE DỰ KIẾN SỬA — P0

## Backend

Có thể thêm:

```text
backend/PEMS.Application/Delegations/Queries/ResolveNotificationVisitTarget/
    ResolveNotificationVisitTargetQuery.cs
    ResolveNotificationVisitTargetDto.cs
    ResolveNotificationVisitTargetQueryHandler.cs
```

Có thể thêm shared:

```text
backend/PEMS.Application/Delegations/Common/VisitCurrentAccessResolver.cs
```

hoặc namespace service phù hợp architecture hiện tại.

Sửa:

```text
backend/PEMS.Api/Controllers/DelegationsController.cs
```

Có thể sửa:

```text
backend/PEMS.Application/Delegations/Queries/ViewGuestDelegationList/ViewGuestDelegationListQueryHandler.cs
```

chỉ để reuse extracted shared service, không thay list behavior P0.

---

## Frontend

Sửa:

```text
frontend/pems-react/src/features/delegations/api/delegationsApi.ts
frontend/pems-react/src/features/delegations/types/delegations.types.ts
frontend/pems-react/src/pages/dashboard/visit/VisitRequestManagement.tsx
```

Chỉ sửa nếu cần:

```text
frontend/pems-react/src/features/notifications/utils/resolveNotificationDestination.ts
```

Không duplicate click handler ở:

```text
NotificationBellButton
NotificationsPage
SharedDashboardView
StaffCalendarTab
StaffDashboardCalendar
```

Các surface này phải tiếp tục hội tụ về central routing.

---

# 23. TEST FILE DỰ KIẾN SỬA

Frontend:

```text
frontend/pems-react/src/pages/dashboard/visit/__tests__/VisitRequestManagementNotificationDeepLink.test.tsx
frontend/pems-react/src/features/notifications/utils/__tests__/resolveNotificationDestination.test.ts
frontend/pems-react/tests-realstack/notification-routing.realstack.spec.ts
```

Backend:

```text
tests/PEMS.IntegrationTests/VisitRequests/RelationFilterEntryContextTests.cs
```

Khuyến nghị thêm:

```text
tests/PEMS.IntegrationTests/VisitRequests/NotificationVisitTargetResolverTests.cs
```

---

# 24. TRIỂN KHAI THEO COMMIT NHỎ

Không gom toàn bộ vào một commit lớn.

## Commit 1

```text
feat(notifications): add exact visit notification target resolver
```

- backend resolver;
- DTO;
- endpoint;
- backend tests.

## Commit 2

```text
fix(notifications): resolve visit deep links from exact current target
```

- frontend API;
- `VisitRequestManagement`;
- current-state intent routing;
- frontend tests.

## Commit 3

```text
test(notifications): cover multi-campus and multi-relation notification routing
```

- realstack;
- regression matrix.

## Commit 4 — P1 riêng

```text
refactor(visits): preserve exact instance relation contexts in merged rows
```

Không merge P1 vào P0 nếu chưa cần.

---

# 25. ACCEPTANCE CRITERIA — P0

P0 chỉ được coi là hoàn thành khi:

- [ ] Visitor single-campus notification vẫn mở đúng.
- [ ] Visitor multi-campus `CAMPUS_APPROVED` mở được.
- [ ] Visitor multi-campus `CAMPUS_REJECTED` mở được.
- [ ] Visitor multi-campus `HOST_CHANGED` mở được.
- [ ] Không còn false toast `Không tìm thấy đúng cơ sở...` khi instance thực sự tồn tại và user còn quyền.
- [ ] HO multi-campus mở được đúng campus ở read-only mode.
- [ ] Staff current Host mở Host Process đúng instance.
- [ ] Former Host không mở được Host Process cũ.
- [ ] Staff Leader notification review mở đúng campus.
- [ ] Staff Leader historical notification không mở approve modal.
- [ ] Participant notification không bị co-existing Reviewer/Host relation hijack.
- [ ] Department flow không regression.
- [ ] Student flow không regression.
- [ ] Unknown/legacy notification không escalation.
- [ ] Exact deleted target không fallback sang sibling campus.
- [ ] Same-route second click vẫn hoạt động.
- [ ] Rapid-click stale response không overwrite target mới.
- [ ] Unit tests pass.
- [ ] Integration tests pass.
- [ ] Real-stack notification routing pass.

---

# 26. MANUAL QA MATRIX SAU DEPLOY

Tạo ít nhất các account test:

```text
Visitor
HO
Staff Leader
Staff
Department Leader
Department Staff
Student
```

Tạo dữ liệu:

```text
Single campus request
Multi-campus HN + HCM
Multi-campus approved one / rejected one
Host transfer A → B
Host transfer B → C
Staff Leader also registrant
Staff Leader also participant
Staff host one campus + participant another campus
```

Test từ:

```text
Bell
/notifications page
Dashboard change notification
Staff calendar notification
```

Mỗi notification kiểm tra:

```text
1. click
2. correct request
3. correct campus
4. correct role relation
5. correct current action
6. no stronger action than intent
7. no stale target
8. second click
9. back/forward
10. same-route second notification
```

---

# 27. ROLLBACK PLAN

Nếu P0 gây regression:

Frontend có thể rollback riêng commit sử dụng exact resolver và tạm quay về previous one-shot behavior.

Backend resolver mới là read-only endpoint nên có thể giữ lại mà không ảnh hưởng production behavior.

Không rollback database schema vì P0 không yêu cầu migration.

P0 nên tránh schema DB change để rollback đơn giản.

---

# 28. RỦI RO VÀ CÁCH CHẶN

## Risk 1 — Duplicate authorization logic

### Chặn

Extract shared service.

Không copy-paste business rule.

---

## Risk 2 — Resolver tự tạo quyền mới

### Chặn

AllowedActions phải đến từ current authorization engine.

Frontend không tự infer quyền từ role string.

---

## Risk 3 — Sai sibling campus

### Chặn

Exact `VisitInstanceId` phải match request.

Không `FirstOrDefault()` fallback.

---

## Risk 4 — Multi-relation collision

### Chặn

Notification intent + exact target đứng trên default list entry context.

---

## Risk 5 — Stale notification

### Chặn

Resolve current DB state tại thời điểm click.

---

## Risk 6 — Existing notification cũ không có semantic metadata

### Chặn

Legacy policy:

```text
safe detail only
```

---

# 29. DEFINITION OF DONE

Không chỉ:

```text
Visitor notification đã click được.
```

Mà phải đạt:

```text
Same notification semantics
+
same structured target
+
same current user relation
=
same destination
```

bất kể click từ:

```text
Bell
Notifications page
Dashboard
Calendar
```

và:

```text
Không role nào được tăng quyền vì notification.
Không campus nào bị đoán.
Không stale URL nào có quyền vượt current state.
```

---

# 30. TÓM TẮT QUYẾT ĐỊNH

## Sửa ngay P0

```text
Add exact backend Notification Visit Target Resolver
        ↓
VisitRequestManagement consume one-shot command
        ↓
Call exact resolver
        ↓
Intent ceiling + current state
        ↓
Correct destination
```

## Không sửa nhanh bằng

```text
items[0]
role-specific Visitor patch
drop VisitInstanceId
nested campus found → reuse aggregate row permissions
```

## Sau P0

```text
P1: harden multi-relation merge
P2: read-state consistency
P2: notification filter backend pagination
```

---

# 31. KẾT QUẢ KỲ VỌNG

Sau khi triển khai:

### Visitor

```text
CAMPUS_APPROVED
CAMPUS_REJECTED
HOST_CHANGED
```

trên multi-campus đều mở được đúng campus.

### HO

Per-campus visibility notification không còn fail vì request-summary shape.

### Staff Leader

Không mở sai campus khi có nhiều relation.

### Staff

Không vào Host Process nếu không còn là current Host.

### Department / Student

Flow hiện tại giữ nguyên.

### Toàn hệ thống

Notification không còn phụ thuộc vào việc list API đang aggregate row theo cách nào để xác định exact business target.

---

# 32. GHI CHÚ TRIỂN KHAI

Trong quá trình code:

1. Không commit/push nếu chưa được yêu cầu.
2. Không thay business rule dựa trên suy đoán.
3. Nếu phát hiện current source khác kế hoạch:
   - dừng;
   - trace lại;
   - cập nhật kế hoạch;
   - không ép source chạy theo tài liệu cũ.
4. Bất kỳ fix nào làm thay đổi permission matrix phải được tách thành issue/phase riêng.
5. Ưu tiên patch nhỏ, reversible, testable.

---

## Checklist cuối trước khi merge

```text
[ ] Source-of-truth HEAD được ghi nhận
[ ] Backend exact resolver implemented
[ ] Exact request-instance ownership validation
[ ] Shared authorization reused
[ ] Frontend no longer exact-matches aggregate list rows
[ ] Intent ceiling preserved
[ ] Legacy safe-detail policy preserved
[ ] Visitor multi-campus tests
[ ] HO multi-campus tests
[ ] Staff Host stale tests
[ ] Staff Leader multi-relation tests
[ ] Department regression tests
[ ] Student regression tests
[ ] Rapid-click race test
[ ] Same-notification second-click test
[ ] Real-stack pass
[ ] Manual QA pass
[ ] No DB migration in P0
[ ] Rollback verified
```
