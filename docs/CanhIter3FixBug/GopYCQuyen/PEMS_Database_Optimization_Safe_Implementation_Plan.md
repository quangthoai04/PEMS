# PEMS Backend — Database Optimization, Security & Regression-Safe Implementation Plan

> **Purpose:** Tối ưu truy vấn Database của PEMS, giảm N+1/N+M, Cartesian explosion, query fan-out, unnecessary round-trips và các vấn đề SQL/security **nhưng tuyệt đối không làm thay đổi business logic, authorization, transaction, concurrency, retry, notification, status lifecycle hoặc output contract hiện tại**.
>
> **Repository:** `quangthoai04/PEMS`
>
> **Branch audited:** `Dev`
>
> **Audit baseline commit:** `9c5d5b195695b36de27ad80d25c147634fc3b6c8`
>
> **Audit date:** 2026-08-28
>
> **Implementation rule:** Không được coi audit finding là permission để sửa code ngay. Trước mỗi finding, phải đọc lại source code hiện tại và xác minh finding bằng code thực tế. Nếu source đã thay đổi so với baseline, phải re-audit finding đó trước khi sửa.

---

## 1. Mục tiêu và nguyên tắc bất biến

### 1.1. Mục tiêu performance

Tối ưu các vấn đề:

- N+1 query.
- N×M query/fan-out.
- Cartesian explosion do nhiều collection `Include`.
- Correlated subquery không cần thiết.
- Query trong loop có thể batch.
- Materialize dữ liệu quá sớm rồi xử lý aggregate bằng memory.
- Background job không bounded.
- `SaveChangesAsync()` không cần thiết trong loop.
- Query projection không tối ưu.
- Query authorization bị lặp.
- Các query có `IN` quá lớn hoặc pagination không hiệu quả.
- Index thiếu hoặc không phù hợp với workload thực tế.
- Raw SQL/dynamic SQL có nguy cơ SQL Injection.

### 1.2. Business logic phải được coi là bất biến

Không được thay đổi nếu không có yêu cầu nghiệp vụ riêng:

- Status lifecycle.
- Approval/rejection/resubmit/amendment flow.
- Global confirmation gate.
- Campus scope.
- Role/sub-role permission.
- Registrant ownership.
- Staff/Staff Leader behavior.
- Notification semantics.
- Email semantics.
- Retry semantics.
- At-most-once / claim-before-send semantics.
- Transaction boundary khi transaction đang bảo vệ business invariant.
- Row lock / isolation level.
- Idempotency/deduplication.
- Exception type và business error code.
- DTO/API response contract.
- Sort order.
- Pagination semantics.
- Fallback value.
- Snapshot semantics.
- Mixed-campus V2 semantics.

### 1.3. Nguyên tắc quan trọng nhất

> **Chỉ tối ưu execution strategy hoặc data-access strategy khi có bằng chứng rằng input, scope, predicates, authorization, transaction/concurrency semantics, side effects và output trước/sau là tương đương.**

Nếu chưa chứng minh được equivalence:

> **DO NOT CHANGE.**

---

# 2. Codebase facts đã được kiểm tra

## 2.1. `ApplicationDbContext`

`backend/PEMS.Infrastructure/Persistence/ApplicationDbContext.cs`

Đã kiểm tra trực tiếp:

- `ApplicationDbContext : DbContext, IApplicationDbContext`.
- Có `BeginTransactionAsync()`.
- Có `BeginSerializedTransactionAsync()` với `ReadCommitted`.
- DbContext quản lý các nhóm entity liên quan trực tiếp tới audit:
  - VisitRequests
  - VisitRequestCampuses
  - VisitGuestMembers
  - VisitInstanceFormDetails
  - VisitInstanceGuestMembers
  - Feedbacks
  - Minutes
  - MinuteParticipants
  - SentEmails
  - SentEmailRecipients
  - EmailActionTokens
  - Notifications
  - Users
  - Files
  - v.v.
- Mapping relationship được cấu hình bằng Fluent API.
- Vì vậy khi tối ưu query phải kiểm tra cả EF navigation/mapping, không chỉ nhìn LINQ statement.

## 2.2. HO Dashboard

`GetHODashboardOverviewQueryHandler.Handle` đã được đọc trực tiếp.

Đã xác minh:

- Có authorization gate `RoleCode != "HO"` → `ForbiddenException`.
- KPI queries dùng các status/time predicate cụ thể.
- Pending requests dùng request-level projection.
- Upcoming visits dùng instance-level projection.
- Campus status hiện load active campuses rồi query `processing`, `upcomingCount`, `alerts` trong `foreach`.
- `alerts` JOIN `Feedbacks` với `VisitRequestCampuses`.
- Vì vậy DB-NQ-001 là một optimization có cơ sở: có thể batch aggregate theo `CampusId`, nhưng phải giữ nguyên predicate và semantics.

## 2.3. Pending V2 update

`UpdatePendingVisitRequestV2CommandHandler.Handle` đã được đọc trực tiếp.

Đã xác minh:

- Feature flag gate.
- Authentication gate.
- Load `VisitRequest` cùng:
  - `CampusInstances -> FormDetail`
  - `CampusInstances -> GuestMemberLinks`
  - `GuestMembers`
- Registrant ownership check.
- Editable lifecycle gate.
- Transaction bao quanh `_editService.ApplyPendingEditAsync(...)`.
- Notification xảy ra **sau commit**.
- Notification bị chặn khi request còn behind global confirmation gate.
- Campus IDs trước/sau edit đều có business meaning.

Do đó các optimization trên query này **không được làm mất navigation hoặc thay đổi transaction/notification timing**.

---

# 3. Quy trình bắt buộc trước khi sửa

Mỗi finding phải đi qua workflow sau.

```text
READ SOURCE
    ↓
TRACE CALL GRAPH
    ↓
VERIFY FINDING
    ↓
IDENTIFY BUSINESS INVARIANTS
    ↓
DESIGN MINIMAL OPTIMIZATION
    ↓
PROVE EQUIVALENCE
    ↓
IMPLEMENT
    ↓
BUILD
    ↓
UNIT TEST
    ↓
INTEGRATION TEST
    ↓
COMPARE DB BEHAVIOR
    ↓
COMPARE OUTPUT / SIDE EFFECT
    ↓
PERFORMANCE MEASURE
    ↓
SELF-REVIEW
```

Không được bỏ bước `VERIFY FINDING`.

---

# 4. Classification trước implementation

| Class | Ý nghĩa | Quy tắc |
|---|---|---|
| SAFE | Chỉ thay đổi cách đọc dữ liệu, output giữ nguyên | Có thể ưu tiên |
| SAFE-WITH-PROOF | Có thể an toàn nhưng phải chứng minh predicates/output | Phải có test |
| SENSITIVE | Có transaction/auth/concurrency/side effect | Phải review sâu |
| DO-NOT-CHANGE-YET | Chưa đủ bằng chứng hoặc có thể đổi semantics | Không sửa trong phase hiện tại |

---

# 5. Phase 0 — Baseline và Regression Guard

## Mục tiêu

Tạo trạng thái `BEFORE` đáng tin cậy.

### Bắt buộc

1. Checkout đúng branch/commit.
2. Build backend.
3. Chạy Unit Tests.
4. Chạy Integration Tests.
5. Ghi nhận test fail trước khi optimization.
6. Không sửa test chỉ để làm xanh.
7. Nếu test flaky, chạy lặp lại và ghi rõ.
8. Ghi nhận query count cho các đường đi quan trọng.
9. Ghi nhận response JSON/DTO cho các endpoint quan trọng.
10. Ghi nhận authorization behavior.
11. Ghi nhận side effects:
    - notification
    - email
    - DB writes
    - audit log
    - status change.

### Baseline cases

- HO Dashboard.
- Notification target resolution.
- View Guest Delegation List.
- View Email/List.
- View Feedback Summary.
- Save Minutes.
- Create/Update/Resubmit Visit.
- Reminder jobs.
- Email attachment authorization.
- Partner contacts.
- Department reception detail.

---

# 6. Phase 1 — Cartesian Explosion / Split Query

## Scope

- DB-NQ-002
- DB-NQ-010
- DB-NQ-019

## Strategy

Thêm `AsSplitQuery()` tại đúng query có nhiều sibling collection.

Không thay:

- Include graph.
- Where.
- ordering.
- projection.
- authorization.
- transaction.

### Required proof

Ví dụ:

```text
BEFORE:
VisitRequest
 × CampusInstances
 × GuestMembers

AFTER:
VisitRequest
 + CampusInstances
 + GuestMembers
```

Phải chứng minh collection counts và entity values giống nhau.

### Không được

- Xóa Include chỉ để giảm query mà chưa chứng minh field không được dùng.
- Đổi tracking behavior tùy tiện.
- Đổi transaction.
- Đổi navigation loading semantics.

### Verification

- Build.
- Unit tests.
- Integration tests.
- Test request có:
  - 0 campus / 1 campus / nhiều campus.
  - 0 guest / 1 guest / nhiều guest.
  - campus guest links.
- So sánh output trước/sau.

---

# 7. Phase 2 — Safe Read Batching

## Scope

- DB-NQ-012
- DB-NQ-014
- DB-NQ-016
- DB-NQ-017
- DB-NQ-022
- DB-NQ-023

## Pattern chuẩn

Từ:

```csharp
foreach (var item in items)
{
    var entity = await db.Entities
        .FirstOrDefaultAsync(...);
}
```

sang:

```csharp
var ids = items
    .Select(...)
    .Distinct()
    .ToList();

var entities = await db.Entities
    .Where(x => ids.Contains(x.Id))
    .ToDictionaryAsync(x => x.Id, ...);
```

### Bắt buộc

Giữ nguyên:

- Scope predicate.
- Tenant/campus/request ID.
- Exception behavior.
- Fallback.
- Null behavior.
- Ordering.
- Role data.
- Status checks.

---

# 8. DB-NQ-012 — SaveMinutes participant lookup

### File

`PEMS.Application/Delegations/Minutes/SaveMinutesCommandHandler.cs`

### Hiện trạng

`ReconcileParticipants` query Users hoặc GuestMember bên trong loop khi thêm participant mới.

### Fix

Batch:

- Internal user IDs → one Users query.
- Guest IDs → one GuestMembers query.

### Security invariant

Guest lookup **phải giữ**:

```text
GuestMember.VisitRequestId == current requestId
```

Không được đổi thành chỉ:

```text
GuestMemberId == input.GuestMemberId
```

Nếu bỏ request scope có thể cho phép participant của request khác lọt vào.

### Must preserve

- User not found exception.
- User inactive exception.
- `MINUTE_GUEST_NOT_IN_CURRENT_REQUEST`.
- Participant ordering.

---

# 9. DB-NQ-014 — Department reception detail

### File

`GetRequestDetailQueryHandler`

### Fix

Batch tất cả:

- attempt assignees
- responder
- proposedBy

thành một user lookup.

### Must preserve

- `Role` navigation.
- Fallback `AssigneeUserId.ToString()`.
- `proposedByRole`.
- Null behavior.

---

# 10. DB-NQ-016 — Email image normalization

### File

`EmailImageLayoutNormalizer.cs`

### Fix stage 1

Batch `Files` by `FileId`.

### Do not change yet

Không tự ý parallelize storage reads.

Không tự ý thay đổi storage semantics.

### Stage 2 only after proof

Có thể đánh giá:

- cache same FileId.
- `Image.IdentifyAsync` thay `Image.LoadAsync`.

Nhưng phải chứng minh output width/height giống nhau.

---

# 11. DB-NQ-017 — Operational contact tokens

### File

`OperationalContactMaintenanceService.cs`

### Fix

Batch token READ theo `IdentityChangeId`.

### Must preserve

- Transaction.
- `FOR UPDATE` behavior.
- `RecipientEmail != claim.NewEmailMasked`.
- Claim order.
- Email send semantics.

Không được biến logic claim/send thành batch nếu chưa chứng minh concurrency safety.

---

# 12. DB-NQ-022 — In-memory O(N×M)

Không phải DB N+1.

Dùng:

```text
ToLookup(key)
```

thay vì repeatedly:

```text
Where(x => x.UserId == id)
```

### Must preserve

- Element order.
- Aggregation.
- Average/rating calculation.
- DTO shape.

---

# 13. DB-NQ-023 — SQL-side GroupBy

### File

`ViewGuestDelegationListQueryHandler.QueryInstanceLevelAsync`

Hiện tại:

```text
ToListAsync()
→ GroupBy()
→ Count()
```

### Fix

Đưa aggregate trước `ToListAsync()`:

```text
GroupBy(...)
→ Select(Count())
→ ToListAsync()
```

### Required proof

- Pomelo/MySQL phải translate thành SQL.
- Không được fallback sang client evaluation.
- Key/value phải giống nhau.

---

# 14. Phase 3 — High-impact user-facing query

## DB-NQ-001 — HO Dashboard

### File

`GetHODashboardOverviewQueryHandler.cs`

### Current behavior đã xác minh

Authorization:

```text
RoleCode == HO
```

Campus:

```text
ACTIVE
```

Campus metrics:

```text
DuringVisit
BeforeVisit / Assigned
Feedback Rating <= 2
```

### Fix

Prefetch 3 aggregate dictionaries:

```text
processingByCampus
upcomingByCampus
alertsByCampus
```

sau đó loop campus chỉ lookup dictionary.

### Must preserve exactly

- `ACTIVE` campus filter.
- `DuringVisit`.
- `PlannedStartAt >= now`.
- `BeforeVisit || Assigned`.
- Feedback `Rating <= 2`.
- Join condition `Feedback.VisitInstanceId == VisitRequestCampus.VisitInstanceId`.
- Campus ordering from `allCampuses`.
- Missing aggregate = 0.

### Do not alter

KPI queries unless independently audited.

---

# 15. Phase 4 — Notification Resolver

## DB-NQ-003

### File

`ResolveNotificationVisitTargetQueryHandler.cs`

### Risk

Đây là một trong các finding nhạy cảm nhất.

Resolver đang đi qua nhiều populations:

```text
responsible
attending
registered
hosted
```

Mục tiêu business là không làm mất relation context giữa các campus/population.

### Stage A — `SkipEnrichment`

Chỉ bỏ:

- ChangeSummary.
- NextTask.

nếu resolver thực sự không đọc hai field này.

### Stage B — short-circuit

Chỉ được short-circuit khi đã xác định **exact VisitInstanceId**.

Không được:

```text
request found → stop
```

nếu relation của instance chưa được xác minh.

### Required tests

- Request-level relation.
- Instance-level relation.
- Multi-campus.
- User thuộc nhiều relation.
- User chỉ xuất hiện ở population sau.
- Không có relation.
- Wrong VisitInstanceId.
- Wrong campus.
- Unauthorized user.

---

# 16. Phase 5 — Background jobs

## DB-NQ-004

`ActionItemDueReminderHostedService`

### Batch được

- Minutes.
- Instances.
- Users.
- Delegation names.

### Không batch tùy tiện

`DueReminderSentAt` và per-item persistence nếu retry semantics phụ thuộc vào nó.

### Required invariant

Email fail:

```text
email fail
→ do not stamp sent
→ retry remains possible
```

Email success:

```text
email success
→ stamp sent
```

---

## DB-NQ-005

`VisitReminderDispatchService`

### Batch được

- Instance.
- Campus.
- Recipients.

### PHẢI giữ per-row

```text
ClaimAsync
```

và thứ tự:

```text
Claim
→ Send
```

Lý do: chống race/duplicate send.

### Không được

- `Task.WhenAll` cho claim/send.
- Batch claim.
- Send trước claim.

---

## DB-NQ-008

`AccountEmailConfirmationMaintenance`

### Current risk

Query stale users không có upper bound.

### Proposed

```text
OrderBy(CreatedAt)
Take(batchSize)
```

### Invariant

Không được làm mất user khỏi processing.

Ví dụ:

```text
500 stale
→ 200
→ 200
→ 100
```

### Phải kiểm tra

- Job interval.
- Batch size.
- Throughput.
- Backlog growth.

---

# 17. Phase 6 — Medium/Sensitive query rewrites

## DB-NQ-006 — Feedback summary

Batch latest feedback.

### Must preserve

- Pair key `(VisitRequestId, VisitInstanceId)`.
- Latest `SubmittedAt`.
- Deterministic tie-break nếu cần.
- `LatestSubmitterName`.

Không đổi response.

---

## DB-NQ-007 — RemindExpenseReports

Batch:

- Assignee users.
- Department leaders.
- Notification requests.

Có thể gom `CreateNotificationRequest` rồi gọi `CreateManyAsync`.

### Phải xác minh

`DedupeKey` behavior khi chuyển từ từng notification sang batch.

Nếu deduplication trong batch thay đổi semantics, không merge batch cho đến khi có test chứng minh behavior mong muốn.

---

## DB-NQ-011 — ViewEmailList

### Current issue

`Include(e => e.Recipients)` nằm trước projection và không còn có tác dụng sau projection.

Projection lại gọi `Recipients.FirstOrDefault(...)` nhiều lần.

### Fix

Projection một object TO:

```text
TO = first TO recipient
```

sau đó đọc field từ object đó.

### Push down filter

Chỉ push trước projection những filter thực sự map trực tiếp tới root columns:

- RelatedType.
- StartDate.
- EndDate.

Giữ sau projection:

- CounterpartName keyword.
- ProcessStatus.

### Must preserve

- Sent/received union.
- Pagination.
- Count.
- Recipient selection.
- Existing `receivedQuery` behavior.
- Existing hardcoded `System/Sender` behavior nếu đó là behavior hiện tại.

---

# 18. DB-NQ-009 — `QueryAllMergedAsync`

### Đây KHÔNG phải N+1

Đây là intentional fan-out do request-level và instance-level model khác nhau.

### Không được tự ý

```text
MergeFetchCap 1000 → 300
```

chỉ vì 1000 lớn.

### Trước khi thay cap

Đo:

- maximum instance count.
- distribution.
- records per campus.
- records per population.
- truncation rate.

### Safe optimization

Có thể thêm warning khi source chạm cap.

Có thể SQL-side GroupBy cho `campusCountByRequest`.

### Status filter

Không được push xuống pre-merge nếu status semantics yêu cầu filter sau merge.

---

# 19. Phase 7 — Partner query side-effect

## DB-NQ-013

### Current design issue

GET query handler có side-effect backfill.

### First step

Batch read lookup.

### Second step

Đánh giá riêng việc tách backfill khỏi GET.

Không tự ý chuyển sang background job/migration nếu chưa kiểm tra:

- khi nào backfill phải xảy ra.
- transaction.
- consistency.
- user expectation.
- API response dependency.

### Existing bug candidate

`existingContactNames` phải được cập nhật sau khi add contact để tránh duplicate trong cùng một invocation.

Không gộp bug fix này vào performance commit nếu không cần thiết; tách riêng để dễ regression review.

---

# 20. Phase 8 — Write optimization

## DB-NQ-018 — News sections

### Không được đơn giản hóa thành

> “Nhiều SaveChanges → một SaveChanges.”

Phải trace:

- `SectionId` usage.
- Navigation property.
- transaction.
- triggers.
- code đọc ID trước flush.
- ordering.
- multilingual loop.

### Chỉ batch khi chứng minh

EF có thể insert parent rồi child trong cùng `SaveChanges` và gán generated key chính xác.

---

## DB-NQ-020 — Proposed hosts

### Sensitive vì method được gọi 2 lần

Một lần trước transaction và một lần dưới transaction/lock.

### Optimization được phép

Batch user/department lookup.

### Critical invariant

Lần kiểm tra thứ hai phải sử dụng dữ liệu **bên trong transaction/lock context**, không dùng cache từ lần đầu.

Không dùng cached pre-transaction user data để quyết định security-sensitive validation.

### Preserve

- Candidate ID order.
- Registrant skip.
- NotFound exception.
- Role.
- SubRole.
- PrimaryCampus.
- Status.
- DepartmentType.

---

## DB-NQ-021 — Upload photos

Đây là N+1 DB nhưng file storage vốn là per-file I/O.

### Phase 1

Không parallelize storage.

### Phase 2 only after storage audit

Kiểm tra:

- filename collision.
- overwrite behavior.
- ordering.
- partial failure cleanup.
- transaction boundary between DB and storage.

---

# 21. SQL Injection / Database Security Audit

Performance optimization **không thay thế security audit**.

## 21.1. Search toàn repo

Tìm tất cả:

```text
FromSqlRaw
ExecuteSqlRaw
SqlQueryRaw
FromSqlInterpolated
ExecuteSqlInterpolated
string interpolation trong SQL
string concatenation trong SQL
dynamic ORDER BY
dynamic table/column name
```

## 21.2. Với mỗi raw SQL

Phải xác minh:

- Parameterization.
- User input source.
- Authorization.
- Campus/request scope.
- Sort field whitelist.
- Table/column whitelist.
- No raw string concatenation.
- No user-controlled SQL fragment.

## 21.3. Không chỉ tìm SQL injection

Kiểm tra thêm:

### IDOR / object scope

Ví dụ:

```text
user A
→ thay VisitRequestId của user B
→ API có trả data không?
```

### Campus isolation

```text
Staff campus A
→ query campus B
```

phải bị từ chối hoặc filter đúng theo business rule.

### Email authorization

Không được batch hóa authorization theo cách làm lộ:

- sender.
- recipient.
- email existence.
- relation của user khác.

### File authorization

Batch READ được, nhưng decision order và short-circuit phải giữ nguyên.

---

# 22. Index Audit

Chỉ audit index sau khi query shape đã ổn định.

## Với mỗi query quan trọng

Kiểm tra:

```text
WHERE
JOIN
ORDER BY
GROUP BY
LIMIT
```

và mapping FK.

### Quy trình

```text
Query
↓
Generated SQL
↓
EXPLAIN
↓
Rows examined
↓
Index used
↓
Selectivity/cardinality
↓
Candidate index
↓
EXPLAIN again
```

### Không tạo index hàng loạt

Mỗi index phải có:

- Query cần nó.
- Predicate/JOIN được hỗ trợ.
- Expected benefit.
- Write overhead assessment.
- Existing index overlap check.

---

# 23. Database schema / SQL migration policy

### Mặc định

Các optimization sau **không cần sửa SQL schema**:

- `AsSplitQuery`.
- Batch SELECT.
- `ToDictionary`.
- `ToLookup`.
- SQL-side GroupBy.
- Projection rewrite.
- Removing unnecessary Include.

### Chỉ sửa SQL/migration khi

Có bằng chứng rằng:

- index thiếu.
- index sai.
- constraint cần thiết để bảo vệ invariant.
- schema limitation thực sự là bottleneck.

### Không được

- Tạo index trùng.
- Đổi column type chỉ vì performance mà chưa đo.
- Đổi FK/constraint.
- Đổi cascade behavior.
- Đổi nullability.
- Đổi collation.
- Đổi precision/time semantics.

nếu chưa có migration impact review.

---

# 24. Query performance measurement

Mỗi optimization phải có:

```text
Before
After
```

## Metrics

- DB round-trip count.
- SQL statement count.
- Rows returned.
- Rows examined nếu có EXPLAIN.
- Query duration.
- Total endpoint duration.
- Memory allocation nếu relevant.
- Transaction duration.
- Lock duration.
- Background job processing time.

### Không được ghi số ước lượng như số đo thực tế

Ví dụ:

> “~92 queries”

chỉ là audit estimate cho đến khi instrumentation đo được.

Report phải phân biệt:

- Estimated.
- Measured.
- Verified.

---

# 25. Regression matrix

## Visit lifecycle

- Create.
- Submit.
- Approve.
- Reject.
- Resubmit.
- Update.
- Amend.
- Cancel.
- Close.

## Campus

- Single campus.
- Multi-campus.
- Mixed V2.
- Different campus details.

## Authorization

- HO.
- Staff Leader.
- Staff.
- Department Leader.
- Department Staff.
- Visitor.
- Student.
- Unauthorized user.

## Notifications

- Request notification.
- Instance notification.
- Multi-campus notification.
- Notification target resolution.
- Action URL.
- Action type.
- Global confirmation gate.

## Email

- Sent.
- Received.
- Recipient.
- Attachment.
- Inline image.
- Authorization.
- Deduplication.

## Background jobs

- Empty queue.
- Small queue.
- Full batch.
- Backlog > batch.
- Retry.
- Concurrent execution.
- Partial failure.

---

# 26. Before/After equivalence checklist

Mỗi modified handler phải trả lời:

### Input

- Input có đổi không?

### Query scope

- Có đổi `Where` không?
- Có đổi JOIN không?
- Có đổi campus/request scope không?

### Authorization

- Có đổi thứ tự authorization không?
- Có query dữ liệu trước khi authorize không?
- Có lộ existence/data không?

### Data

- Có record nào bị mất không?
- Có record nào được thêm không?
- Có duplicate không?

### Ordering

- Có đổi `OrderBy` không?

### Pagination

- Có đổi `Skip/Take` không?
- Có đổi count không?

### Null/fallback

- Null behavior có giống không?
- Fallback có giống không?

### Exceptions

- Exception type?
- Error code?
- Error message nếu contract phụ thuộc?

### Transaction

- Begin/commit/rollback có đổi không?

### Concurrency

- Lock?
- Isolation?
- Claim?
- Idempotency?

### Side effects

- Notification?
- Email?
- Audit log?
- Storage?
- Status update?

### Output

- DTO fields?
- Values?
- Collection counts?
- Ordering?

Nếu bất kỳ câu trả lời nào là:

> UNKNOWN

thì chưa được merge.

---

# 27. Commit strategy

Không tạo một commit khổng lồ cho toàn bộ DB optimization.

Khuyến nghị:

```text
commit 1:
DB-NQ-002 + 010 + 019
AsSplitQuery

commit 2:
DB-NQ-012 + 014 + 016 + 017
Safe read batching

commit 3:
DB-NQ-023 + 022
SQL aggregate / in-memory lookup

commit 4:
DB-NQ-001
HO dashboard

commit 5:
DB-NQ-003
Notification resolver

commit 6:
DB-NQ-004 + 005 + 008
Background jobs

commit 7:
DB-NQ-006 + 007 + 011
Sensitive read rewrites

commit 8:
DB-NQ-018 / 020 / 021
Write/storage optimization — only if proven safe

commit 9:
Security / SQL injection fixes

commit 10:
Index/migration changes
```

Nếu một finding cần rollback riêng thì commit phải rollback được riêng.

---

# 28. Definition of Done

Một optimization chỉ được coi là DONE khi:

- [ ] Source code đã được đọc lại trên branch hiện tại.
- [ ] Finding đã được xác minh.
- [ ] Business invariant đã được ghi nhận.
- [ ] Minimal change đã được chọn.
- [ ] Unit tests pass.
- [ ] Integration tests pass.
- [ ] Security tests pass nếu liên quan authorization.
- [ ] Before/after output equivalent.
- [ ] Query count giảm hoặc execution plan cải thiện.
- [ ] Không tăng bất thường result-set size.
- [ ] Transaction semantics không đổi.
- [ ] Concurrency semantics không đổi.
- [ ] Retry/idempotency semantics không đổi.
- [ ] No new SQL injection vector.
- [ ] Không có N+1 mới.
- [ ] Không có Cartesian explosion mới.
- [ ] Không có client-side evaluation ngoài ý muốn.
- [ ] Build pass.
- [ ] Git diff đã được self-review.
- [ ] Commit message phản ánh đúng phạm vi.
- [ ] Nếu cần rollback, rollback được độc lập.

---

# 29. Những điều tuyệt đối KHÔNG được làm

1. Không “fix” toàn bộ 23 finding trong một lần.
2. Không sửa business logic để giảm query count.
3. Không xóa authorization check vì performance.
4. Không bỏ campus/request scope để query đơn giản hơn.
5. Không batch claim/locking nếu chưa chứng minh concurrency.
6. Không thay transaction boundary tùy tiện.
7. Không đổi status lifecycle.
8. Không đổi notification timing.
9. Không đổi email retry semantics.
10. Không giảm `MergeFetchCap` chỉ vì con số 1000 lớn.
11. Không tạo index hàng loạt không dựa trên EXPLAIN/workload.
12. Không coi estimated query count là measured result.
13. Không sửa test để che regression.
14. Không thêm `AsNoTracking()` tùy tiện trong command cần tracked entity.
15. Không song song hóa storage/email chỉ vì thấy loop.
16. Không đưa filter vào SQL trước merge nếu làm mất relation context.
17. Không dùng cache dữ liệu đọc ngoài transaction cho security-sensitive validation.
18. Không commit nhiều optimization không liên quan trong cùng commit.

---

# 30. Final validation

Sau khi hoàn thành các phase:

```text
Build
  ↓
Unit Test
  ↓
Integration Test
  ↓
Security Test
  ↓
Functional Regression
  ↓
Concurrency Test
  ↓
Query Count
  ↓
EXPLAIN
  ↓
Slow Query Review
  ↓
Git Diff Review
  ↓
Final Self-Review
```

Final report phải có:

| Finding | Status | Before | After | Business Logic | Security | Test |
|---|---|---:|---:|---|---|---|
| DB-NQ-001 | ... | ... | ... | Preserved | Pass | ... |
| DB-NQ-002 | ... | ... | ... | Preserved | Pass | ... |
| ... | ... | ... | ... | ... | ... | ... |

Không ghi `PASS` nếu chưa có evidence.

---

# 31. Final implementation instruction

**AI/Coding Agent phải thực hiện đúng thứ tự sau:**

1. Đọc toàn bộ source liên quan đến finding.
2. Đọc entity/model/navigation liên quan.
3. Đọc interface DbContext.
4. Đọc transaction/locking code.
5. Đọc authorization/policy code.
6. Đọc tests hiện có.
7. Trace caller/callee khi method dùng chung.
8. Xác minh finding.
9. Nếu finding sai hoặc source đã thay đổi → không sửa theo audit cũ.
10. Đề xuất minimal patch.
11. Giải thích tại sao patch không đổi business logic.
12. Implement một finding hoặc một nhóm rất nhỏ có cùng semantics.
13. Build.
14. Test.
15. Đo query behavior.
16. Compare BEFORE/AFTER.
17. Self-review diff.
18. Chỉ sau khi đạt toàn bộ acceptance criteria mới chuyển sang finding tiếp theo.

> **Không được tối ưu chỉ dựa trên tên finding. Phải dựa trên source code thực tế của branch hiện tại.**
>
> **Nếu performance improvement yêu cầu thay đổi business semantics để đạt được, dừng lại và báo cáo, không tự ý thay đổi.**
