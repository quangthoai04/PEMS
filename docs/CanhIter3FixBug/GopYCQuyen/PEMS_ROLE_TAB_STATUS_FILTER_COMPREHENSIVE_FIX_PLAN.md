# PEMS — Kế hoạch Fix Triệt Để Status Filter / Role × Tab / Multi-Campus

> Mục tiêu của tài liệu này là **fix triệt để** nhóm lỗi trạng thái/filter/visibility do PEMS đang có nhiều kiểu row khác nhau:
>
> - request-level row,
> - campus-instance-level row,
> - merged row (`tab=all`),
> - multi-campus request có nhiều campus cùng lúc ở các lifecycle status khác nhau.
>
> Tài liệu này **không yêu cầu ép tất cả role dùng cùng một semantics**. Ngược lại, mỗi role/tab phải lọc theo đúng **scope dữ liệu mà người dùng đang xem**.
>
> **Không commit/push trước khi hoàn thành audit + regression tests + manual smoke test.**

---

# 1. Vấn đề gốc cần xử lý

PEMS có một `VisitRequest` nhưng một request có thể có nhiều `CampusInstance`.

Ví dụ:

```text
Request X
├─ Hà Nội  = ASSIGNED
└─ Đà Nẵng = WAITING_REQUEST_APPROVAL
```

Nếu hệ thống chỉ gán một status duy nhất cho toàn request rồi dùng status đó cho mọi role/tab thì sẽ gây lỗi:

- Staff Leader Hà Nội thấy campus Hà Nội là `Đã duyệt`.
- HO có thể chỉ thấy request cha là `Chờ duyệt`.
- Visitor có thể không tìm được campus `Đã duyệt` trong request của chính họ.
- Staff/Staff Leader ở `registered` hoặc `all` có thể bị mất status của sibling campus sau khi merge.
- Filter/badge có thể đúng ở một role nhưng sai ở role khác.
- Một request có thể bị loại khỏi filter dù có campus thực sự match.
- Merge/pagination có thể lọc sai thứ tự và làm mất row.

Root cause kiến trúc:

```text
request-level status
!=
campus-instance status
!=
participant/invitation status
!=
merged-row display status
```

Không được dùng một loại status để thay thế tất cả các loại còn lại.

---

# 2. Invariant bắt buộc toàn hệ thống

## 2.1. FILTER != AUTHORIZATION != ENTRY CONTEXT

Giữ nguyên nguyên tắc đang có trong backend:

```text
FILTER
→ quyết định row nào xuất hiện.

AUTHORIZATION
→ quyết định user được làm gì với row đó.

ENTRY CONTEXT
→ quyết định click row sẽ mở màn nào.
```

Không được sửa quyền chỉ để làm filter đúng.

Không được sửa lifecycle chỉ để làm số row bằng nhau giữa các role.

## 2.2. Row semantics phải rõ ràng

Mỗi query/tab phải biết row của mình là:

```text
REQUEST_LEVEL
INSTANCE_LEVEL
MERGED_REQUEST_LEVEL
INVITATION_LEVEL
TASK_LEVEL
```

Không được filter bằng field không cùng cấp với row nếu chưa định nghĩa rõ semantics.

## 2.3. Multi-campus không được mất campus state

Một multi-campus request:

```text
Request X
├─ HN = ASSIGNED
├─ DN = WAITING_REQUEST_APPROVAL
└─ HCM = BEFORE_VISIT
```

thì hệ thống phải giữ được cả ba facts này.

Không được collapse thành một status rồi coi hai status còn lại như không tồn tại.

---

# 3. Role × Tab semantics chuẩn

## 3.1. HO — `responsible`

HO monitor toàn bộ request/campus trong phạm vi nghiệp vụ.

Row UI vẫn:

```text
1 VisitRequest = 1 parent row
```

nhưng filter `Trạng thái` của HO phải mang nghĩa:

```text
Một request được INCLUDED
nếu có ÍT NHẤT MỘT campus instance
match status đang chọn.
```

Ví dụ:

```text
HN = ASSIGNED
DN = WAITING_REQUEST_APPROVAL
```

expected:

```text
HO / Tất cả        → INCLUDED
HO / Đã duyệt      → INCLUDED
HO / Chờ duyệt     → INCLUDED
HO / Đang chuẩn bị → EXCLUDED
```

Không ép một request của HO chỉ nằm trong một bucket duy nhất.

Không thêm dropdown `Trạng thái đơn`.

Không đổi thành nhiều parent rows.

## 3.2. Visitor — `responsible`

Visitor là request owner/contact và có thể có request multi-campus.

Với filter lifecycle của chuyến thăm, áp semantics giống HO nhưng chỉ trong **population mà Visitor có quyền xem**:

```text
Một request được INCLUDED
nếu có ít nhất một campus của request đó
match lifecycle status đang chọn.
```

Ví dụ:

```text
HN = ASSIGNED
DN = BEFORE_VISIT
```

expected:

```text
Visitor / Đã duyệt      → INCLUDED
Visitor / Đang chuẩn bị → INCLUDED
Visitor / Chờ duyệt     → EXCLUDED
```

Không quay lại union rộng kiểu:

```text
Đã duyệt =
ASSIGNED
+ BEFORE_VISIT
+ DURING_VISIT
+ AFTER_VISIT
+ CLOSED
```

`Đã duyệt` chỉ đại diện cho campus `ASSIGNED`.

## 3.3. Staff Leader — `responsible`

Đây là campus-instance view.

Population:

```text
chỉ campus thuộc PrimaryCampusId của Staff Leader
```

Filter phải match **chính instance row đó**.

Ví dụ:

```text
HN = ASSIGNED
DN = WAITING_REQUEST_APPROVAL
```

Staff Leader HN:

```text
Đã duyệt  → INCLUDED
Chờ duyệt → EXCLUDED
```

Không dùng sibling campus DN để đưa row HN vào `Chờ duyệt`.

## 3.4. Regular Staff — `responsible` / `hosted`

Đây là instance-level view.

Population:

```text
chỉ campus instance mà Staff là Host
```

Filter lifecycle phải match chính hosted instance.

Không dùng request aggregate.

## 3.5. Staff / Staff Leader — `registered`

`registered` là request-level row:

```text
1 request do user đăng ký = 1 row
```

Nếu request multi-campus, lifecycle filter phải có semantics request-owner tracking:

```text
Một registered request được INCLUDED
nếu có ít nhất một campus instance
match status đang chọn.
```

Ví dụ:

```text
HN = ASSIGNED
DN = BEFORE_VISIT
HCM = WAITING_REQUEST_APPROVAL
```

registered filter expected:

```text
Đã duyệt      → INCLUDED
Đang chuẩn bị → INCLUDED
Chờ duyệt     → INCLUDED
Đã hoàn tất   → EXCLUDED
```

Không dùng một aggregate request status duy nhất để loại request khỏi các lifecycle filter hợp lệ.

## 3.6. Staff / Staff Leader / Visitor — `all`

`tab=all` là merged population.

Sau merge:

```text
1 VisitRequest = 1 merged row
```

Nhưng filter phải được thực hiện theo semantics của **merged request-level tracking**, không dựa vào candidate thắng merge.

### Quy tắc bắt buộc:

1. Fetch đủ source populations.
2. Merge/dedupe theo `VisitRequestId`.
3. Union relations / matched contexts / campus progress.
4. Resolve final merged row.
5. Apply lifecycle status filter trên **final merged campus set**.
6. Sort.
7. Paginate.

Không được:

```text
lọc từng source
→ paginate source
→ merge
```

nếu điều đó có thể làm mất row hợp lệ từ source khác.

Không được filter sau pagination.

## 3.7. Department / Student — `responsible`

Đây chủ yếu là instance-level assignment population:

- logistics assigned,
- agenda responsibility,
- assigned participant.

Lifecycle filter nếu có phải match instance liên quan.

Không để sibling campus ngoài scope làm row xuất hiện.

## 3.8. `attending`

`attending` không được nhầm lifecycle status với invitation/task response status.

Ví dụ:

```text
INVITED
ACCEPTED
DECLINED
```

là status quan hệ tham dự, không phải:

```text
ASSIGNED
BEFORE_VISIT
DURING_VISIT
...
```

Không dùng canonical visit lifecycle resolver để thay invitation/task status.

---

# 4. Canonical status architecture

## 4.1. Giữ một resolver duy nhất cho lifecycle display

Backend nên có một canonical resolver cho **campus lifecycle**:

```text
WAITING_CONTACT_CONFIRMATION
WAITING_REQUEST_APPROVAL
ASSIGNED
BEFORE_VISIT
DURING_VISIT
AFTER_VISIT
CLOSED
REJECTED
CANCELLED
```

Resolver trả ít nhất:

```text
Code
Label
```

Ví dụ:

```text
ASSIGNED     → Đã duyệt
BEFORE_VISIT → Đang chuẩn bị
DURING_VISIT → Đang diễn ra
AFTER_VISIT  → Chờ đóng / Chờ đánh giá tùy role text
CLOSED       → Đã hoàn tất
```

Frontend không tự đoán status từ nhiều field khác nhau nếu backend đã trả canonical status.

## 4.2. Không dùng request aggregate làm campus lifecycle filter

Các request-level status như:

```text
PENDING_CONTACT_CONFIRMATION
PENDING_APPROVAL
PARTIALLY_APPROVED
APPROVED
REJECTED
CANCELLED
```

có thể cần cho nghiệp vụ aggregate nội bộ.

Nhưng không được mặc định dùng chúng làm lifecycle filter cho request-level UI nếu requirement là:

```text
match ANY campus status
```

`PARTIALLY_APPROVED` có thể giữ backend/internal.

Không thêm `Duyệt một phần` vào UI nếu business không yêu cầu.

---

# 5. Mapping lifecycle filter chuẩn

Áp cho các view cần lọc theo campus lifecycle:

```text
Chờ xác nhận
→ WAITING_CONTACT_CONFIRMATION

Chờ duyệt
→ WAITING_REQUEST_APPROVAL

Đã duyệt
→ ASSIGNED

Đang chuẩn bị
→ BEFORE_VISIT

Đang diễn ra
→ DURING_VISIT

Chờ đóng / Chờ đánh giá
→ AFTER_VISIT

Đã hoàn tất
→ CLOSED

Từ chối
→ REJECTED

Đã hủy
→ CANCELLED
```

Không map:

```text
Đã duyệt → APPROVED + BEFORE_VISIT + DURING_VISIT + AFTER_VISIT + CLOSED
```

Không map:

```text
Chờ duyệt → mọi request aggregate chưa complete
```

trừ khi UI label thật sự nói đó là aggregate filter.

---

# 6. Backend implementation plan

Audit tối thiểu:

```text
backend/PEMS.Application/Delegations/Queries/ViewGuestDelegationList/
  ViewGuestDelegationListQueryHandler.cs
  ViewGuestDelegationListDto.cs

backend/.../Services/
  VisitRowLabels.cs
  EffectiveStatusCodes.cs
```

## 6.1. Tạo helper filter rõ scope

Không rải logic OR status khắp handler.

Tạo helper/predicate có tên rõ nghĩa, ví dụ:

```text
ApplyRequestLevelCampusLifecycleFilter(...)
ApplyInstanceLifecycleFilter(...)
```

### Request-level semantics:

```text
q = q.Where(vr =>
    vr.CampusInstances.Any(ci => ci.Status == selectedCampusStatus)
);
```

### Instance-level semantics:

```text
q = q.Where(x => x.c.Status == selectedCampusStatus);
```

## 6.2. `CancelledOnly`

Audit kỹ.

Không để:

```text
request CANCELLED
OR campus CANCELLED
```

được dùng mù quáng ở mọi row shape.

Định nghĩa:

### Request-level tracking view:

```text
Đã hủy
→ request included nếu có campus CANCELLED
   hoặc business rule nói toàn request CANCELLED phải xuất hiện.
```

### Instance-level view:

```text
Đã hủy
→ chính instance row CANCELLED
```

Nếu request-level cancellation cascade thực sự làm tất cả campus bị hủy, test bằng dữ liệu thật.

## 6.3. `REJECTED`

Tương tự cancellation.

Request-level row có thể cần:

```text
Any campus REJECTED
```

nếu UI đang filter campus lifecycle.

Instance-level row:

```text
current instance REJECTED
```

Không dùng request aggregate `REJECTED` để thay thế mọi campus rejection case.

## 6.4. `tab=all`

Bắt buộc kiểm tra:

```text
CloneForMerge
QueryAllMergedAsync
MergeRelations
MergeCampusProgressItems
EffectiveStatus
EffectiveStatuses
Timing
CampusId
Keyword
FromDate
ToDate
```

Không được drop filter field khi clone.

Không được drop `CampusProgressItems`.

Không được chọn một candidate rồi mất sibling campus state.

Không được filter final status trước khi merge hoàn chỉnh.

---

# 7. Frontend implementation plan

Audit:

```text
frontend/pems-react/src/features/delegations/config/visitRequestFilterConfig.ts
frontend/pems-react/src/features/delegations/types/delegations.types.ts
frontend/pems-react/src/pages/dashboard/visit/VisitRequestManagement.tsx
```

## 7.1. Không để mỗi role tự invent semantics

Tạo mapping có chủ đích:

```text
REQUEST_ANY_CAMPUS
INSTANCE_EXACT
INVITATION_RESPONSE
TASK_RESPONSE
```

Config mỗi role/tab khai báo filter mode.

Ví dụ:

```text
HO responsible
→ REQUEST_ANY_CAMPUS

Visitor responsible
→ REQUEST_ANY_CAMPUS

Staff Leader responsible
→ INSTANCE_EXACT

Staff responsible
→ INSTANCE_EXACT

registered
→ REQUEST_ANY_CAMPUS

all
→ REQUEST_ANY_CAMPUS sau merge
```

## 7.2. URL state không được giữ stale legacy params

Khi đổi status:

- clear param cũ,
- set đúng param mới.

Khi chọn `Tất cả trạng thái` phải xóa sạch:

```text
effectiveStatus
effectiveStatuses
requestStatus
campusStatus
approvedAny
pendingApprovalAny
cancelledOnly
```

trừ field nào thực sự thuộc filter khác.

Không được để URL hiển thị ALL nhưng API vẫn gửi status cũ.

## 7.3. Badge

Instance-level row:

```text
badge = instance canonical status
```

Request-level row:

- parent badge có thể là summary/aggregate nếu UI vẫn cần;
- **không dùng parent badge để quyết định ANY-campus filter**.

Ví dụ:

```text
parent badge = Chờ duyệt

campus:
HN = Đã duyệt
DN = Chờ duyệt
```

HO chọn `Đã duyệt`:

```text
request vẫn INCLUDED
```

dù parent badge không phải `Đã duyệt`.

---

# 8. CampusProgressItems

Request-level / merged row phải giữ đủ campus details để UI và authorization không đoán:

```text
VisitInstanceId
CampusId
CampusName
InstanceStatus
HostUserId
OperationalContactUserId
PlannedStartAt
PlannedEndAt
Capabilities
```

Nếu DTO hiện có `CampusProgressItems`, không tạo duplicate DTO khác nếu không cần.

Audit:

```text
single-campus request
multi-campus request
merged request
registered request
Visitor request
HO request
```

đều có campus state phù hợp với scope.

---

# 9. Regression test matrix bắt buộc

Không chỉ test HO.

Tạo test theo role × tab.

## Case A — mixed approval

```text
HN = ASSIGNED
DN = WAITING_REQUEST_APPROVAL
```

Expected:

```text
HO:
ALL      → IN
ASSIGNED → IN
WAITING  → IN
BEFORE   → OUT

Visitor owner:
ALL      → IN
ASSIGNED → IN
WAITING  → IN

Staff Leader HN:
ASSIGNED → IN
WAITING  → OUT

Staff Leader DN:
WAITING  → IN
ASSIGNED → OUT
```

## Case B — mixed lifecycle

```text
HN = ASSIGNED
DN = BEFORE_VISIT
HCM = DURING_VISIT
```

Expected request-level tracking views:

```text
ASSIGNED     → IN
BEFORE_VISIT → IN
DURING_VISIT → IN
AFTER_VISIT  → OUT
```

Instance-level views chỉ match instance của user.

## Case C — after/closed/cancelled

```text
HN = CLOSED
DN = CANCELLED
```

Request-level tracking:

```text
CLOSED    → IN
CANCELLED → IN
ASSIGNED  → OUT
```

Staff/Leader instance-level:

```text
chỉ theo instance thuộc scope
```

## Case D — partially approved aggregate

```text
RequestStatus = PARTIALLY_APPROVED
HN = ASSIGNED
DN = WAITING_REQUEST_APPROVAL
```

Expected:

```text
HO / Đã duyệt  → IN
HO / Chờ duyệt → IN
```

Không để aggregate status loại mất campus `ASSIGNED`.

## Case E — request aggregate APPROVED nhưng campus khác phase

```text
RequestStatus = APPROVED
HN = ASSIGNED
DN = BEFORE_VISIT
```

Expected request-level tracking:

```text
Đã duyệt      → IN
Đang chuẩn bị → IN
```

## Case F — registered multi-campus

User là registrant:

```text
HN = ASSIGNED
DN = BEFORE_VISIT
HCM = WAITING_REQUEST_APPROVAL
```

`registered`:

```text
Đã duyệt      → IN
Đang chuẩn bị → IN
Chờ duyệt     → IN
CLOSED        → OUT
```

## Case G — all merged row

User đồng thời là:

```text
registrant + host HN
```

Request:

```text
HN = ASSIGNED
DN = BEFORE_VISIT
```

`tab=all`:

```text
Đã duyệt      → IN
Đang chuẩn bị → IN
```

Row chỉ xuất hiện một lần.

Relations phải giữ đủ:

```text
Registrant
Host(HN)
```

Không được mất action/capability do merge.

## Case H — invitation/task

Attending:

```text
INVITED / ACCEPTED / DECLINED
```

phải tiếp tục filter theo invitation status.

Không bị lifecycle filter refactor làm hỏng.

---

# 10. Seed regression bắt buộc

Dùng các seed thật nếu tồn tại:

```text
OPC-05-PARTIAL
OPC-06-APPROVED-MIXED
OPC-11-HO-MONITOR-ONLY
```

Mỗi seed báo:

```text
VisitRequestId
RequestStatus
VisitScope

CampusId
CampusName
InstanceStatus

HO:
ALL
WAITING
ASSIGNED
BEFORE
...

Visitor nếu có owner:
...

Staff Leader từng campus:
...
```

Không dựa vào tên scenario để suy đoán.

Đọc DB row thật / handler result thật.

---

# 11. Pagination / sorting safety

Đây là release blocker.

Request-level ANY-campus filter phải được áp:

```text
TRƯỚC Count
TRƯỚC Skip/Take
```

`tab=all`:

```text
merge final population
→ filter final merged row
→ sort
→ paginate
```

Không:

```text
paginate
→ filter
```

vì sẽ gây:

- trang thiếu row,
- total sai,
- request biến mất,
- page 1/page 2 không ổn định.

---

# 12. Search / Campus / Date / Scope audit

Status không được làm sai các filter khác.

Audit cross-product tối thiểu:

```text
status + keyword
status + campusId
status + fromDate/toDate
status + visitScope
status + timing
status + relation
```

Request-level view:

```text
CampusId filter
```

phải được định nghĩa rõ:

- request included nếu có campus match CampusId,
- status filter phải áp trên đúng scope theo requirement.

Nếu user chọn Campus Hà Nội + Đã duyệt thì không được match Đà Nẵng `ASSIGNED`.

Expected:

```text
CampusId = HN
AND
status = ASSIGNED
```

phải match **cùng campus HN**, không phải:

```text
Any campus is HN
AND
Any other campus is ASSIGNED
```

Đây là bug SQL rất dễ xảy ra nếu dùng hai `.Any()` độc lập.

### Bắt buộc test:

```text
HN = WAITING
DN = ASSIGNED

filter Campus=HN + Đã duyệt
→ OUT

filter Campus=DN + Đã duyệt
→ IN
```

Nếu cần, dùng predicate cùng biến:

```text
vr.CampusInstances.Any(ci =>
    (!campusId.HasValue || ci.CampusId == campusId)
    && ci.Status == selectedStatus
)
```

---

# 13. Date filter phải cùng scope

Tương tự campus/status.

Nếu HO chọn:

```text
Campus = HN
Status = ASSIGNED
Date = 20/08
```

thì match phải dựa trên **cùng HN instance** nếu đó là semantics UI.

Không cho:

```text
HN match campus
DN match status
HCM match date
```

rồi request vẫn lọt qua.

Audit toàn bộ predicate để tránh existential mismatch.

---

# 14. Không được sửa các phần sau để chữa status

Không thay đổi:

```text
Authorization
AllowedActions
Capabilities
Role permissions
VisitMutationPolicy
Lifecycle transitions
Database status transition rules
Notification access rules
```

trừ khi audit phát hiện bug độc lập và có approval riêng.

Không làm:

```text
HO row count = Staff Leader row count
```

bằng cách sửa quyền hoặc đổi population.

Không biến multi-campus thành nhiều request giả.

---

# 15. Test gates

Sau implementation phải chạy toàn bộ:

```text
dotnet build

PEMS.UnitTests
PEMS.IntegrationTests
PEMS.ArchitectureTests

npm run lint
npx vitest run
npm run build
```

Không chỉ chạy test mới.

---

# 16. Manual smoke test

Sau automated tests, tự kiểm tra UI:

## HO

```text
Tất cả
Đã duyệt
Chờ duyệt
Đang chuẩn bị
Đang diễn ra
Chờ đóng
Đã hoàn tất
Đã hủy
Từ chối
```

Kiểm tra request multi-campus xuất hiện ở mọi filter mà có campus match.

## Visitor

Lặp lại với request của chính Visitor.

## Staff Leader

Đảm bảo chỉ campus của họ quyết định status filter.

## Regular Staff

Đảm bảo host instance không bị sibling campus ảnh hưởng.

## Registered

Đảm bảo multi-campus request có thể xuất hiện ở nhiều lifecycle filters nếu nhiều campus khác trạng thái.

## All

Đảm bảo:

```text
1 request = 1 row
```

và filter không làm mất relation/action.

---

# 17. Definition of Done

Chỉ được coi là xong khi tất cả điều sau đúng:

- [ ] HO lọc lifecycle theo ANY matching campus.
- [ ] Visitor request-level lifecycle filter không mất sibling campus status.
- [ ] Staff/Leader `registered` multi-campus được audit và test.
- [ ] `tab=all` lọc sau merge, trước pagination.
- [ ] Staff Leader responsible vẫn exact own-campus.
- [ ] Regular Staff responsible/hosted vẫn exact hosted-instance.
- [ ] Department/Student instance-level không regression.
- [ ] Attending invitation/task status không regression.
- [ ] `CampusId + Status` match cùng campus.
- [ ] `Date + Campus + Status` không existential mismatch.
- [ ] Cancelled/Rejected semantics có test.
- [ ] Không còn broad union `Đã duyệt`.
- [ ] Không thêm `Duyệt một phần` UI nếu không có requirement.
- [ ] Không thêm `Trạng thái đơn`.
- [ ] Không sửa authorization để làm đẹp count.
- [ ] Không nhân request multi-campus thành nhiều parent rows.
- [ ] Full backend/frontend gates green.
- [ ] Manual smoke test pass.

---

# 18. Yêu cầu báo cáo trước commit

Agent phải trả:

## A. Root cause

Cho từng role/tab có vấn đề:

```text
Role
Tab
Row shape
Old filter semantics
Why wrong
New semantics
```

## B. File changed

Liệt kê đầy đủ.

## C. SQL/query behavior

Cho các case mixed-campus.

## D. Test matrix

Báo pass/fail theo:

```text
HO
Visitor
Staff Leader
Staff
Registered
All
Department
Student
Attending
```

## E. Seed verification

Bắt buộc ít nhất:

```text
OPC-05-PARTIAL
OPC-11-HO-MONITOR-ONLY
```

## F. Safety

Xác nhận:

```text
No authorization change
No lifecycle change
No DB schema change
No notification permission change
No commit
No push
```

---

# 19. Lệnh cuối cho agent

> Không fix riêng HO theo kiểu vá cục bộ.
>
> Audit toàn bộ `role × tab × row shape × status semantics`.
>
> Chuẩn hóa request-level tracking filter thành `ANY matching campus` ở những view thực sự theo dõi toàn request.
>
> Giữ instance-level views filter chính xác trên instance thuộc scope.
>
> Đặc biệt bảo vệ `registered`, `all`, pagination, CampusId+Status và Date+Campus+Status khỏi mismatch.
>
> Không thay đổi authorization/lifecycle để chữa filter.
>
> Không commit/push trước khi báo cáo đầy đủ và manual review.

---

# 20. Source paths cần audit đầu tiên

```text
backend/PEMS.Application/Delegations/Queries/ViewGuestDelegationList/
  ViewGuestDelegationListQueryHandler.cs
  ViewGuestDelegationListDto.cs

frontend/pems-react/src/features/delegations/config/
  visitRequestFilterConfig.ts

frontend/pems-react/src/features/delegations/types/
  delegations.types.ts

frontend/pems-react/src/pages/dashboard/visit/
  VisitRequestManagement.tsx
```

Ngoài ra grep toàn repo:

```text
RequestStatus
CampusStatus
EffectiveStatus
EffectiveStatuses
ApprovedAny
PendingApprovalAny
CancelledOnly
PARTIALLY_APPROVED
CampusProgressItems
CloneForMerge
QueryAllMergedAsync
```

Mục tiêu là **không còn bất kỳ caller cũ nào âm thầm dùng legacy semantics khác với contract mới**.
