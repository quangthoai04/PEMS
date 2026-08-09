# PEMS — PROMPT TRIỂN KHAI LẠI FILTER / RELATION / AUTHORIZATION / ENTRY CONTEXT

## 0. Vai trò của AI Agent

Bạn đang làm việc trên dự án **PEMS**. Hãy đọc code hiện tại trước khi sửa, đặc biệt là luồng **Quản lý tiếp khách / danh sách đơn / quyền theo quan hệ / điều hướng vào chi tiết hoặc trang xử lý**.

Mục tiêu của task này là sửa **nguyên nhân gốc** của việc hệ thống đang trộn lẫn:

1. **FILTER** — vì sao một đơn xuất hiện trong danh sách.
2. **AUTHORIZATION** — user thực sự có quyền gì trên request/campus.
3. **ENTRY CONTEXT** — khi click vào row thì mặc định đi tới màn hình nào.

### Nguyên tắc bắt buộc

```text
FILTER != AUTHORIZATION != ENTRY CONTEXT
```

Không được dùng tab/filter hiện tại làm nguồn quyết định quyền nghiệp vụ.

---

# 1. Preflight bắt buộc

Trước khi sửa:

1. Xác nhận branch hiện tại.
2. Ghi lại HEAD hiện tại.
3. Ghi lại working tree trước khi thay đổi.
4. Không reset/revert/xóa thay đổi không thuộc task.
5. Đọc code thực tế trước khi kết luận.
6. Không invent endpoint, route, DTO hoặc business rule nếu code hiện tại đã có nguồn chuẩn.
7. Không thay đổi database/schema trong task này, trừ khi audit chứng minh thật sự bắt buộc. Mặc định: **KHÔNG đổi DB**.
8. Giữ nguyên các business rule lifecycle/deadline/concurrency hiện tại, chỉ sửa cách xác định relation/filter/entry context.

Ưu tiên kiểm tra các file sau trước:

```text
backend/PEMS.Application/Delegations/Queries/ViewGuestDelegationList/ViewGuestDelegationListQueryHandler.cs

backend/PEMS.Application/Delegations/Services/VisitFormRead/VisitFormReadService.cs

frontend/pems-react/src/pages/dashboard/visit/VisitRequestManagement.tsx

frontend/pems-react/src/features/visit-request/utils/visitVersionRouting.ts

frontend/pems-react/src/features/delegations/types/delegations.types.ts
```

Đồng thời search toàn repo các keyword:

```text
TabRegistered
registeredView
REGISTRANT_VIEWER
CurrentUserRelation
ResolveRelation
QueryAllMergedAsync
seenRequestIds
seenInstanceIds
OPEN_HOST_PROCESS
OPEN_PROCESS_SUMMARY
APPROVE_AND_ASSIGN_HOST
CAMPUS_REJECT
EDIT_PENDING_REQUEST
RESUBMIT_REJECTED_REQUEST
CurrentHostUserId
OperationalContactUserId
RegistrantUserId
```

---

# 2. Vấn đề hiện tại cần sửa

## 2.1 Visitor: `registered` đang loại trừ contact-owner

Code hiện tại đang có logic kiểu:

```csharp
vr.RegistrantUserId == userId
&& (
    !vr.CampusInstances.Any(ci => ci.OperationalContactUserId != null)
    || !vr.CampusInstances.Any(ci => ci.OperationalContactUserId == userId)
)
```

Hệ quả:

- user thật sự là `Registrant`;
- đồng thời là `Operational Contact`;
- lọc **Tôi là người đăng ký** lại không thấy đơn;
- lọc **Tôi là đầu mối** thì thấy.

Đây là sai semantics của filter.

### Target

`Tôi là người đăng ký` phải match đúng:

```csharp
vr.RegistrantUserId == currentUserId
```

Không được loại user chỉ vì họ đồng thời có relation khác.

---

## 2.2 `registeredView` đang bị dùng như authorization boundary

Hiện tại có tư tưởng:

```text
registered = strictly read-only
```

và có đường code kiểu:

```csharp
if (tab == TabRegistered)
    return actions;
```

hoặc:

```csharp
bool canEditPending = !registeredView && ...
bool canResubmit = !registeredView && ...
```

Điều này sai nếu user thực sự là Registrant có quyền request-level.

### Target

Filter `registered` chỉ trả lời:

> “Request nào mà tôi là Registrant?”

Nó **không được phép** tự động biến user thành read-only.

---

## 2.3 `Tất cả` hiện đang dedupe theo kiểu first-wins và có thể làm mất context

Hiện tại `QueryAllMergedAsync` có cơ chế kiểu:

```text
seenRequestIds
seenInstanceIds
```

và khi gặp row đã có thì bỏ source sau.

Điều này tránh duplicate nhưng có thể làm mất relation/context:

```text
REGISTRANT
HOST
CAMPUS_REVIEWER
PARTICIPANT
```

Nếu giữ row đầu tiên rồi bỏ phần còn lại thì:

- mất badge relation;
- mất entry context;
- mất secondary actions;
- có thể chọn sai primary action;
- có thể route sai trang.

### Target

`Tất cả` phải:

```text
GROUP/MERGE theo real-world request
→ giữ tất cả relation/context hợp lệ
→ merge quyền
→ chọn primary task/entry context
→ chỉ render 1 row/request
```

---

# 3. Domain model cần chốt

## 3.1 Relation và scope

| Relation | Scope |
|---|---|
| REGISTRANT | REQUEST |
| OPERATIONAL_CONTACT | CAMPUS được gán |
| HOST | CAMPUS đang phụ trách |
| CAMPUS_REVIEWER / STAFF_LEADER | CAMPUS của Staff Leader |
| PARTICIPANT | CAMPUS được mời/tham gia |

### Quy tắc

```text
REGISTRANT
→ quyền request-level và các quyền campus mà business rule cho phép

OPERATIONAL_CONTACT
→ chỉ quyền guest-side của campus được gán

HOST
→ chỉ quyền vận hành/process của campus đang host

STAFF_LEADER
→ quyền review/approve/reject tại campus của mình theo lifecycle

PARTICIPANT
→ quyền invitation/contribution theo assignment
```

Một user có thể đồng thời có nhiều relation.

---

# 4. Effective authorization

Backend phải tính relation thật từ dữ liệu, ví dụ:

```csharp
isRegistrant =
    request.RegistrantUserId == userId;

isContactHere =
    instance.OperationalContactUserId == userId;

isHostHere =
    instance.CurrentHostUserId == userId;

isCampusLeaderHere =
    role == STAFF
    && subRole == LEADER
    && currentUser.PrimaryCampusId == instance.CampusId;

isParticipantHere =
    ... // dùng logic canonical hiện có
```

Sau đó:

```text
Effective permissions
=
Registrant permissions
UNION
Operational Contact permissions
UNION
Host permissions
UNION
Staff Leader permissions
UNION
Participant permissions
```

Rồi mới áp:

```text
status
lifecycle
deadline
lead-time
rowVersion
pending amendment
confirmation gate
business constraints
```

### Cấm

Không được làm:

```csharp
if (tab == "registered")
{
    // bỏ quyền host/reviewer/registrant
}
```

Không được dùng:

```text
activeTab
rowTab
currentUserRelation
```

làm nguồn authorization.

---

# 5. Filter semantics cần cập nhật

## 5.1 Visitor

### `Tôi là đầu mối`

Match nếu:

```csharp
request.CampusInstances.Any(
    ci => ci.OperationalContactUserId == currentUserId
)
```

### `Tôi là người đăng ký`

Match nếu:

```csharp
request.RegistrantUserId == currentUserId
```

### `Tất cả`

Union:

```text
CONTACT
+
REGISTRANT
```

Sau đó dedupe/merge.

Một request có thể match cả hai filter cụ thể.

---

## 5.2 Regular Staff

Có thể đồng thời là:

```text
REGISTRANT
HOST
PARTICIPANT
```

### `Đơn tôi đăng ký`

```csharp
RegistrantUserId == currentUserId
```

### `Đơn/Đoàn tôi phụ trách`

```csharp
CurrentHostUserId == currentUserId
```

### `Lời mời tham dự`

Giữ canonical participant/invitation logic hiện tại.

### `Tất cả`

Merge:

```text
REGISTERED
+
HOSTED/RESPONSIBLE
+
ATTENDING
```

---

## 5.3 Staff Leader

Có thể đồng thời là:

```text
REGISTRANT
CAMPUS_REVIEWER
HOST
PARTICIPANT
```

### `Đơn tôi đăng ký`

```csharp
RegistrantUserId == currentUserId
```

### `Đoàn tôi phụ trách`

```csharp
CurrentHostUserId == currentUserId
```

### Campus review

Nếu user là Staff Leader đúng campus và instance đang chờ quyết định:

```text
same campus
+
WAITING_REQUEST_APPROVAL
```

thì giữ quyền canonical hiện có:

```text
APPROVE_AND_ASSIGN_HOST
CAMPUS_REJECT
```

### `Tất cả`

Phải merge:

```text
CAMPUS REVIEW
+
HOST
+
REGISTRANT
+
ATTENDING
```

Không được mất relation nào chỉ vì source khác được merge trước.

---

# 6. Entry Context — click row phải đi đâu?

Đây là phần **khác authorization**.

## 6.1 Filter cụ thể

### Visitor — `Tôi là người đăng ký`

```text
default entry = REQUEST_DETAIL
```

### Visitor — `Tôi là đầu mối`

```text
default entry = REQUEST_DETAIL
```

Nhưng quyền trong detail vẫn tính từ relation thật:

- contact-only → campus scope;
- registrant + contact → vẫn có request-level permission từ Registrant.

---

### Staff — `Đơn tôi đăng ký`

```text
default entry = REQUEST_DETAIL
```

### Staff — `Đơn/Đoàn tôi phụ trách`

```text
default entry = HOST_PROCESS
```

Route phải dùng canonical route hiện có, ví dụ:

```text
/dashboard/visit/process/{visitInstanceId}
```

---

### Staff Leader — `Đơn tôi đăng ký`

```text
default entry = REQUEST_DETAIL
```

### Staff Leader — `Đoàn tôi phụ trách`

```text
default entry = HOST_PROCESS
```

### Staff Leader — campus đang chờ duyệt

```text
default entry = CAMPUS_REVIEW
```

và có:

```text
APPROVE_AND_ASSIGN_HOST
CAMPUS_REJECT
```

---

# 7. `Tất cả` — thiết kế chuẩn

## 7.1 Một request chỉ render 1 row

Không được:

```text
Request X — Registrant
Request X — Host
Request X — Reviewer
```

Phải thành:

```text
Request X
[Người đăng ký]
[Host HN]
[Cần bạn duyệt: HN]
```

---

## 7.2 Merge relations/context

Backend nên trả đủ context thực tế.

Có thể dùng additive DTO fields theo hướng:

```ts
relations: [
  "REGISTRANT",
  "HOST",
  "CAMPUS_REVIEWER"
]
```

Nếu cần instance-specific context:

```ts
relationContexts: [
  {
    relation: "REGISTRANT",
    scope: "REQUEST",
    visitInstanceId: null,
    campusId: null
  },
  {
    relation: "HOST",
    scope: "CAMPUS",
    visitInstanceId: 123,
    campusId: 1
  },
  {
    relation: "CAMPUS_REVIEWER",
    scope: "CAMPUS",
    visitInstanceId: 124,
    campusId: 2
  }
]
```

Không bắt buộc dùng đúng tên trên nếu codebase có naming phù hợp hơn, nhưng semantics phải tương đương.

### Compatibility

Ưu tiên:

- additive DTO;
- giữ field cũ trong giai đoạn migration;
- không phá API contract không cần thiết;
- frontend migrate sang source mới;
- field cũ chỉ còn compatibility, không dùng làm authorization input.

---

# 8. Primary action trong `Tất cả`

Một row có thể có nhiều relation và nhiều quyền.

Không render quá nhiều nút trực tiếp.

Chọn **1 primary action** dựa trên việc cần xử lý ưu tiên nhất.

### Priority đề xuất

```text
1. CAMPUS_REVIEW_REQUIRED
2. HOST_PROCESS_REQUIRED
3. INVITATION_ACTION_REQUIRED
4. REGISTRANT_ACTION_REQUIRED
5. VIEW/TRACKING
```

Ví dụ:

## Case A — Staff Leader + Registrant, campus đang chờ duyệt

```text
Request X
[Người đăng ký]
[Cần bạn duyệt: HN]

Primary:
[Duyệt đơn]
```

Click:

```text
→ campus review HN
```

Secondary trong `...`:

```text
Xem đơn đăng ký
Sửa đơn nếu còn quyền Registrant
Xem lịch sử
...
```

---

## Case B — Staff Leader + Registrant + Host

Nếu không còn pending review nhưng Host đang có việc:

```text
Request X
[Người đăng ký]
[Host HN]

Primary:
[Xử lý]
```

Click:

```text
→ /dashboard/visit/process/{instanceId}
```

Secondary:

```text
Xem đơn đăng ký
Sửa đơn nếu còn quyền
Xem lịch sử
...
```

---

## Case C — chỉ Registrant

Nếu có edit/resubmit cần làm:

```text
Primary:
[Sửa đơn] hoặc [Gửi lại]
```

Nếu không:

```text
Primary:
[Xem đơn]
```

---

# 9. Secondary actions

Secondary actions nằm trong menu:

```text
[...]
```

Nguyên tắc:

```text
Primary action
= việc quan trọng/phù hợp nhất với current entry context

Secondary actions
= các quyền hợp lệ còn lại của cùng user
```

Ví dụ menu có thể có:

```text
Xem đơn đăng ký
Sửa đơn
Gửi lại
Xem lịch sử
Hủy đơn
Xem lý do từ chối
Mở trang xử lý Host
...
```

Chỉ render nếu backend cho phép.

---

# 10. CurrentUserRelation hiện tại không đủ

Nếu code đang dùng:

```text
CurrentUserRelation = một giá trị duy nhất
```

và relation đó được suy từ `tab`, thì không đủ cho case:

```text
Registrant + Host + Reviewer
```

### Target

Có thể giữ:

```text
CurrentUserRelation
RelationLabel
TabType
```

tạm thời cho compatibility/display.

Nhưng thêm source chuẩn mới dạng multi-relation/context và:

```text
KHÔNG dùng CurrentUserRelation làm authorization input.
```

---

# 11. Không được làm mất quyền khi đổi filter

Đây là acceptance rule bắt buộc.

Ví dụ Staff:

```text
User A
= Registrant Request X
= Host HN
```

### Ở `Đơn tôi đăng ký`

```text
Request X xuất hiện
entry = Request Detail
```

### Ở `Đoàn tôi phụ trách`

```text
Request X/HN xuất hiện
entry = Host Process
```

Nhưng backend effective permissions của User A với Request X/HN phải vẫn phản ánh cả hai relation.

Filter chỉ đổi **entry context**, không đổi identity/permission của user.

---

# 12. Self-overlap của Staff Leader

Nếu code/business rule hiện tại cho phép Staff Leader:

```text
vừa là Registrant
vừa là campus reviewer
```

thì không được dùng filter để ngăn quyền review.

Nếu muốn cấm self-approval, đó là **business rule riêng**, phải có rule explicit và tests riêng.

Task này không tự ý thêm self-approval prohibition.

---

# 13. Backend implementation target

Ưu tiên sửa tối thiểu, đúng architecture.

## 13.1 `ViewGuestDelegationListQueryHandler.cs`

Cần audit và sửa:

- Visitor registered query không loại contact-owner.
- `registeredView` không còn đồng nghĩa `strictly read-only`.
- Không `return actions` chỉ vì `TabRegistered`.
- `AllowedActions` tính từ relation thật.
- `Capabilities` tính từ relation thật.
- `ResolveRelation` không còn là nguồn truth cho authorization.
- `QueryAllMergedAsync` không first-wins làm mất context.
- Merge relation/context theo request.
- Chọn `primaryEntryContext` / `primaryAction` phù hợp.
- Dedupe final UI row theo request nhưng không mất instance context.

---

## 13.2 `VisitFormReadService.cs`

Giữ nguyên nguyên tắc canonical hiện có:

```text
Registrant owns REQUEST
Operational Contact owns ONE CAMPUS
```

Dùng cùng semantics này cho list để:

```text
List permissions == Detail permissions
```

Không để list nói một kiểu, detail nói kiểu khác.

---

# 14. Frontend implementation target

## 14.1 `VisitRequestManagement.tsx`

Cần sửa theo nguyên tắc:

```text
filter → population only
allowedActions/capabilities → backend authority
entryContext → routing
```

### Không dùng `activeTab`/`rowTab` để tước quyền

`rowTab` vẫn có thể dùng để:

- display label;
- xác định filter-origin;
- default entry context ở filter cụ thể.

Nhưng không được dùng làm business authorization.

---

## 14.2 Routing

Giữ các route canonical hiện có.

Ví dụ:

```text
REQUEST_DETAIL
→ /dashboard/visit/v2/{visitRequestId}

HOST_PROCESS
→ /dashboard/visit/process/{visitInstanceId}

CAMPUS_REVIEW
→ route/review flow canonical đang tồn tại
```

Không invent route mới nếu route hiện tại đã giải quyết được.

---

## 14.3 UI badges

Ở `Tất cả`, row có thể hiển thị tối đa các relation quan trọng:

```text
[Người đăng ký]
[Host Hà Nội]
[Cần bạn duyệt: Đà Nẵng]
```

Nếu quá nhiều:

- ưu tiên 1–2 badge quan trọng;
- phần còn lại tooltip/menu;
- không làm row quá rối.

---

# 15. Test matrix bắt buộc

## 15.1 Visitor

### V1 — chỉ Registrant

```text
registered → thấy
contact → không thấy
all → thấy 1 lần
```

Quyền request-level đúng lifecycle.

### V2 — chỉ Contact HN

```text
registered → không thấy
contact → thấy
all → thấy 1 lần
```

Không có request-level edit/resubmit chỉ vì là Contact.

### V3 — Registrant + Contact HN

```text
registered → thấy
contact → thấy
all → thấy 1 lần
```

Ở cả hai filter, effective authorization vẫn có quyền Registrant.

Contact relation không làm giảm quyền Registrant.

---

## 15.2 Regular Staff

### S1 — chỉ Registrant

```text
registered → thấy
host/responsible → không thấy
all → thấy 1 lần
```

Entry registered → Request Detail.

### S2 — chỉ Host

```text
registered → không thấy
host/responsible → thấy
all → thấy 1 lần
```

Entry host → Host Process.

### S3 — Registrant + Host

```text
registered → thấy
host/responsible → thấy
all → thấy 1 lần
```

- registered entry → Request Detail;
- host entry → Host Process;
- all → primary Host Process nếu có host task;
- secondary → request detail/edit nếu hợp lệ.

Không mất quyền Host khi ở registered.
Không mất quyền Registrant khi ở hosted/responsible.

---

## 15.3 Staff Leader

### SL1 — Registrant + Reviewer pending

```text
registered → thấy
campus review/all → thấy
all → 1 row
```

Primary ở all:

```text
CAMPUS_REVIEW
```

Secondary:

```text
Request Detail / Registrant actions nếu hợp lệ
```

### SL2 — Registrant + Host

```text
registered → thấy
hosted → thấy
all → 1 row
```

Primary all:

```text
HOST_PROCESS
```

### SL3 — Registrant + Reviewer + Host

Nếu lifecycle tạo được case hợp lệ:

```text
all → 1 row
relations chứa đủ context
```

Priority:

```text
pending review > host process > registrant tracking
```

### SL4 — Staff Leader không phải Registrant nhưng reviewer

Không được cấp request-level registrant actions.

### SL5 — Staff Leader là Registrant nhưng không phải reviewer campus khác

Không được review campus ngoài `PrimaryCampusId`.

---

## 15.4 Participant overlap

Staff/Staff Leader vừa Host hoặc Registrant vừa Participant:

- attending filter vẫn match participant;
- all chỉ 1 request row;
- không mất Host/Registrant context;
- primary action theo priority;
- invitation/contribution là secondary nếu không phải task ưu tiên hơn.

---

# 16. Routing acceptance tests

Phải test click/navigation, không chỉ test API.

### Registered context

```text
→ Request Detail
```

### Host context

```text
→ /dashboard/visit/process/{visitInstanceId}
```

### Staff Leader review context

```text
→ đúng màn review / approve-reject của campus
```

### All context

```text
→ action priority đúng
```

Menu `...`:

```text
→ secondary action mở đúng route
```

---

# 17. Authorization invariants bắt buộc

Các invariant này phải có test:

```text
Filter change MUST NOT change effective authorization.
```

```text
Contact of campus A MUST NOT gain contact authority over campus B.
```

```text
Host of campus A MUST NOT gain host authority over campus B.
```

```text
Staff Leader MUST NOT review campus outside PrimaryCampusId.
```

```text
Registrant request-level permission MUST NOT disappear because the same user is also Host/Contact/Reviewer.
```

```text
Being Host/Reviewer MUST NOT automatically grant request-level Registrant actions.
```

```text
All-tab merge MUST NOT lose a relation/context/action because another source was merged first.
```

---

# 18. Không thay đổi các business rule ngoài scope

Không tự ý thay:

- per-campus approval;
- Staff Leader approve + assign Host;
- Host process lifecycle;
- operational contact confirmation;
- amendment authority;
- 72h / 24h / cutoff rules;
- cancel rules;
- resubmit rules;
- status aggregation;
- email;
- DB seed;
- permissions của role không liên quan.

Nếu phát hiện rule hiện tại mâu thuẫn, báo cáo riêng, không silently rewrite.

---

# 19. Backward compatibility

Nếu frontend/backend đang phụ thuộc field:

```text
tabType
currentUserRelation
relationLabel
allowedActions
capabilities
currentUserIsHost
isAlsoHost
```

thì không xóa ngay nếu chưa migrate hết consumer.

Ưu tiên:

1. thêm field multi-relation/context;
2. migrate logic;
3. giữ old field cho display/compatibility;
4. test;
5. chỉ remove khi chứng minh không còn consumer.

---

# 20. Performance

`all` không được tạo N+1 mới.

Nếu merge nhiều source:

- tiếp tục query batch;
- không query per-row;
- không fetch unbounded;
- giữ pagination semantics đúng;
- nếu hiện tại dùng `MergeFetchCap`, audit để tránh regression nhưng không dùng cap để che lỗi merge.

Nếu cần refactor merge, đảm bảo:

```text
same filters
same sort semantics
same pagination result
```

ngoại trừ thay đổi có chủ đích về relation overlap.

---

# 21. Deliverables bắt buộc

Sau khi code xong, báo cáo đúng format:

## 1. Preflight

```text
Branch:
HEAD before:
HEAD after:
Working tree before:
Working tree after:
```

## 2. Root cause

Nêu rõ:

- Visitor registered/contact mutual exclusion;
- registeredView used as authorization boundary;
- all-tab first-wins dedupe/context loss;
- single relation/tab-derived relation không đủ cho multi-relation user;
- routing/entry context đang trộn với authorization ở đâu.

## 3. Files changed

Liệt kê từng file + lý do.

## 4. Backend changes

Mô tả:

- filter semantics;
- multi-relation;
- permission union;
- all merge;
- primary entry context/action.

## 5. Frontend changes

Mô tả:

- filter behavior;
- routing;
- badges;
- primary action;
- `...` secondary actions.

## 6. Test matrix

Báo cáo pass/fail từng nhóm:

```text
Visitor
Staff
Staff Leader
Participant overlap
All merge
Routing
Authorization invariants
```

## 7. Build/test gates

Chạy ít nhất:

```text
dotnet build
relevant backend unit tests
relevant integration tests

frontend lint
frontend typecheck/build
relevant frontend tests
```

Nếu repo có canonical test command thì dùng command của repo.

## 8. Remaining risks

Chỉ báo cáo risk thực tế còn lại.

---

# 22. Definition of Done

Task chỉ được coi là hoàn thành khi tất cả điều sau đúng:

```text
[ ] Visitor Registrant + Contact xuất hiện ở cả 2 filter tương ứng.
[ ] Staff Registrant + Host xuất hiện ở cả registered và hosted/responsible.
[ ] Staff Leader Registrant + Reviewer/Host giữ đủ relation.
[ ] Filter không còn là authorization input.
[ ] registered không còn mặc định strictly read-only nếu user có quyền thật.
[ ] Tất cả chỉ render 1 row/request.
[ ] Tất cả không làm mất relation/context/action.
[ ] Primary action trong Tất cả theo priority đã chốt.
[ ] Secondary actions nằm trong `...`.
[ ] Registered entry mở Request Detail.
[ ] Host entry mở Host Process.
[ ] Staff Leader pending review mở review/approve-reject context.
[ ] Contact chỉ có campus scope nếu không đồng thời có relation mạnh khác.
[ ] Host chỉ có campus process scope nếu không đồng thời có relation khác.
[ ] Registrant giữ request-level permission theo lifecycle.
[ ] Không đổi DB/schema ngoài nhu cầu bắt buộc đã chứng minh.
[ ] Không phá business rule lifecycle hiện tại.
[ ] Backend tests xanh.
[ ] Frontend tests/build xanh.
```

---

# 23. Nguyên tắc chốt cuối cùng

Hãy triển khai đúng mô hình sau:

```text
FILTER
    ↓
Chỉ xác định population / vì sao row xuất hiện
```

```text
REAL RELATIONS
    ↓
REGISTRANT
CONTACT
HOST
STAFF_LEADER
PARTICIPANT
    ↓
UNION PERMISSIONS
    ↓
LIFECYCLE / STATUS / DEADLINE / BUSINESS RULE
    ↓
ALLOWED ACTIONS
```

```text
ENTRY CONTEXT
    ↓
Filter cụ thể:
    default route theo relation đang xem

Tất cả:
    merge mọi relation
    1 request = 1 row
    chọn task ưu tiên cao nhất
    primary action ngoài row
    secondary actions trong [...]
```

Không vá riêng từng symptom. Hãy sửa ở tầng relation/filter/authorization/entry-context để Visitor, Staff và Staff Leader cùng đúng một cách nhất quán.
