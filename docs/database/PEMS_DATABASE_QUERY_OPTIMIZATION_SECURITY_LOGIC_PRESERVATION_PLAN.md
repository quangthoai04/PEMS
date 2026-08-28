# PEMS — Database Query Optimization, Security & Logic Preservation Plan

> **Purpose:** implement the database/query optimization work from the N+1 / N+M / Cartesian / query fan-out audit without changing existing PEMS business behavior, authorization, multi-campus semantics, transaction/concurrency behavior, or security guarantees.
>
> **Repository evidence reviewed before writing this plan:** current `Dev` source at the audited repository revision exposed through GitHub, including `SaveMinutesCommandHandler`, `ResolveNotificationVisitTargetQueryHandler`, `ViewGuestDelegationListQueryHandler`, `GetHODashboardOverviewQueryHandler`, `EmailImageLayoutNormalizer`, `ActionItemDueReminderHostedService`, `EmailSendReservationStore`, `VisitInstanceConcurrencyGuard`, `VisitRequestV2EditService`, `VisitAmendmentService`, permission documentation, and the existing database audit documentation. The exact implementation must still re-open and trace every affected call chain immediately before modifying it; this document is a controlled implementation plan, not permission to assume an audit finding is still identical to the current code.

---

## 0. Non-negotiable rule

### Optimization must be semantics-preserving

The target is **not**:

```text
fewer SQL queries at any cost
```

The target is:

```text
Same input
+ Same authorization
+ Same campus/instance scope
+ Same validation
+ Same business rules
+ Same transaction boundary
+ Same locking/concurrency semantics
+ Same side effects
+ Same output
+ Same error behavior
+ Same ordering/deduplication semantics where contractually relevant
        ↓
Better data-access strategy
        ↓
Fewer / smaller / more efficient DB operations
```

If equivalence cannot be demonstrated, **DO NOT MERGE**.

---

# 1. Scope

This plan covers:

- N+1 query patterns.
- N+M / per-campus query fan-out.
- Cartesian explosion from multiple collection `Include`s.
- Excessive READ round-trips.
- Background-job query amplification.
- Notification resolver query amplification.
- Complex query/projection/pagination optimization.
- Write-side query optimization only after READ optimization is proven safe.
- SQL Injection and raw SQL audit.
- Authorization / campus scope / IDOR checks at the data-access boundary.
- Index audit using actual query plans.
- Before/after measurement and regression evidence.

It does **not** authorize:

- rewriting business rules;
- changing status transitions;
- weakening authorization;
- changing the meaning of a notification target;
- changing request/instance/campus identity semantics;
- removing validation to make a batch query easier;
- removing transaction or lock behavior to reduce query count;
- changing retry behavior merely because a batched write looks faster.

---

# 2. Code-grounded observations that affect the plan

## 2.1 `SaveMinutesCommandHandler` is not a simple CRUD handler

The current implementation authenticates the caller, loads the minute and visit instance, evaluates `MinuteAccess`, verifies the edit lock token/expiry, checks `RowVersion`, opens a transaction, reconciles participants/action items, saves, commits, and then creates notifications/emails. The handler also resolves the effective delegation name and loads the final DTO. Therefore, optimization in this handler must preserve **authorization, lock ownership, optimistic concurrency, transaction timing, reconciliation semantics, notification recipients, and final DTO hydration** — not merely the SQL count. fileciteturn5file0L2-L7

The participant reconciliation code also contains explicit duplicate/exclude/restore semantics and database uniqueness assumptions. A naive replacement of the reconciliation queries with a generic bulk operation can change behavior even if the SQL count decreases. Treat participant reconciliation as business logic, not merely data access. fileciteturn5file0L2-L7

## 2.2 Notification target resolution is instance-sensitive

`ResolveNotificationVisitTargetQueryHandler` deliberately avoids the merged `all` tab because a single request can contain multiple campus/instance relationship contexts. It queries the existing `ViewGuestDelegationListQuery` pipeline for multiple populations and then unions relation contexts. For an instance-scoped notification it explicitly filters contexts to the requested `VisitInstanceId`. Therefore, **never short-circuit on `VisitRequestId` alone**. The exact instance must be resolved and authorization must remain tied to the existing relation pipeline. fileciteturn6file0L2-L6

The resolver also has explicit behavior for Admin, no matching items, request existence, instance existence, request-scoped notifications, and exact instance resolution. An optimization must preserve all of those branches. fileciteturn6file0L2-L6

## 2.3 Current HO Dashboard has a dedicated query handler

The repository contains `GetHODashboardOverviewQueryHandler` and a corresponding DTO/controller path. The dashboard optimization must therefore be implemented against the actual handler rather than against a generic assumed "three queries per campus" implementation. Re-open the handler before changing it and document the exact query fan-out, filters, aggregation rules, and campus ordering observed in the current revision. fileciteturn18file17L86-L90

## 2.4 Background jobs have concurrency-sensitive responsibilities

The repository contains `ActionItemDueReminderHostedService`, as well as email reservation infrastructure such as `EmailSendReservationStore`. These areas must be treated as concurrency-sensitive. Query batching must not convert an atomic claim/reservation operation into a non-atomic read-then-send sequence. fileciteturn9file0L2-L5 fileciteturn12file2L11-L15

## 2.5 The existing database audit contains a separate baseline

The repository's database audit explicitly recorded build/test limitations and schema evidence at its own audited revision. In particular, that audit reported `dotnet build PEMS.slnx` failing, production backend building successfully, Architecture Tests passing, Integration Tests intentionally not being run because of bootstrap limitations, and frontend tests passing. Those facts are **historical baseline evidence for that audit revision**, not permission to claim that the current `Dev` branch has the same status. Before Phase 0 is closed, rerun the checks against the exact commit being optimized. fileciteturn16file0L1-L2

---

# 3. Phase 0 — Freeze the real baseline

## 3.1 Rule

**No production/query optimization code changes before baseline capture.**

Create a baseline record containing:

- commit SHA;
- branch;
- working-tree state;
- database version/schema identifier;
- build results;
- test results;
- known failures;
- flaky tests;
- query counts;
- representative response/output snapshots;
- timing measurements where available.

## 3.2 Test baseline

Run, where the current repository supports them:

```text
dotnet build PEMS.slnx
backend production build
Architecture Tests
Unit Tests
Integration Tests
frontend build
frontend typecheck/lint
frontend unit tests
```

For every failure record:

| Field | Required |
|---|---|
| Test | Yes |
| Status | PASS / FAIL / SKIPPED |
| Failure | Yes if not PASS |
| Root cause | Known / Unknown |
| Existing before optimization? | Yes / No / Unknown |
| Reproducible? | Yes / No |
| Evidence | Log/commit/test output |

### Regression rule

```text
FAIL BEFORE + FAIL AFTER
= existing failure, not automatically regression

PASS BEFORE + FAIL AFTER
= regression candidate → STOP
```

Do not hide baseline failures by changing tests unless the test itself is proven incorrect.

## 3.3 Performance baseline

Capture actual query counts for at least:

1. HO Dashboard.
2. Notification → Resolve Visit Target.
3. View Guest Delegation List.
4. View Email.
5. View Feedback.
6. Save Minutes.
7. Relevant background jobs.
8. Visit Create.
9. Visit Update.

For each scenario record:

```text
Input fixture
Caller role
Caller campus/scope
Number of campuses
Number of instances
Number of related records
SQL query count
Duration
Result count
Representative output hash/snapshot
```

Do not publish estimated "After" numbers.

---

# 4. Required safety contract for every finding

Before changing any finding, create a mini contract:

```text
Finding ID:
Affected file(s):
Affected method(s):
Current query pattern:
Why it is inefficient:
Business behavior currently implemented:
Authorization path:
Campus/instance scope:
Transaction boundary:
Concurrency/locking:
Side effects:
Expected output:
Expected error behavior:
Optimization strategy:
Invariants to prove:
Tests:
Measurement method:
Rollback strategy:
```

If any field is unknown, investigate the code before editing.

---

# 5. Phase 1 — Low-risk `AsSplitQuery()` optimization

Findings:

- `DB-NQ-002`
- `DB-NQ-010`
- `DB-NQ-019`

## 5.1. Investigation

For each query:

1. Locate the exact LINQ query.
2. List every collection navigation being included.
3. Determine whether multiple collection joins can multiply rows.
4. Determine whether downstream code depends on tracking, ordering, relationship fix-up, or a particular materialization shape.
5. Capture SQL before the change.
6. Capture result/object-graph snapshot before the change.

## 5.2. Implementation rule

Prefer the smallest possible change:

```csharp
query.AsSplitQuery()
```

Do not simultaneously change:

- filters;
- includes;
- projections;
- authorization;
- DTO mapping;
- ordering;
- pagination;
- transaction behavior.

## 5.3. Correctness checks

For a representative `VisitRequest` verify:

```text
VisitRequest
├── CampusInstances
└── GuestMembers
```

and compare before/after:

- parent IDs;
- child IDs;
- relationship membership;
- counts;
- null behavior;
- required fields;
- ordering if contractually relevant.

## 5.4. Gate

Do not proceed to Phase 2 until:

- build passes;
- relevant Unit Tests pass;
- relevant Integration Tests pass;
- object graph equivalence passes;
- query shape is verified;
- no authorization regression is found.

---

# 6. Phase 2 — Simple READ batching

Only optimize **READ** operations first. Do not mix them with write batching.

## 6.1. DB-NQ-012 — `SaveMinutesCommandHandler`

### Current risk

This handler is business-rule-heavy and transaction/concurrency-sensitive. It must not be treated as a generic N+1 refactor. The code already contains validation, participant reconciliation, action-item reconciliation, a transaction, optimistic concurrency and lock checks. fileciteturn5file0L2-L7

### Safe target

Where individual user lookups are genuinely independent READs:

```text
IDs from current validated inputs
        ↓
Distinct IDs
        ↓
One parameterized IN query
        ↓
Dictionary<UserId, User>
        ↓
Existing per-item validation/behavior
```

### Must not change

- missing-user behavior;
- inactive-user behavior;
- guest/request membership validation;
- participant duplicate protection;
- exclude/restore behavior;
- contact badge logic;
- action-item eligibility;
- transaction scope;
- notification/email behavior.

The current handler's participant reconciliation explicitly distinguishes rows that survive the save from rows that are excluded/restored, so the batch query must not flatten these states. fileciteturn5file0L2-L7

### Verification

Create cases for:

- zero participants;
- one participant;
- many participants;
- duplicate input IDs;
- missing user;
- inactive user;
- guest/user cross-source duplicate;
- excluded then re-added participant.

---

## 6.2. DB-NQ-014 — `GetRequestDetailQueryHandler`

The repository contains a request-detail query path under `DepartmentReceptionTasks/Queries/GetRequestDetail`. Re-open the actual handler and trace its complete projection before batching. fileciteturn7file0L1-L5

### Proposed strategy

Batch only independent user reads:

```text
attempt users
+ responder
+ proposedBy
        ↓
Distinct User IDs
        ↓
One/few parameterized query
        ↓
Dictionary
```

### Preserve

- role resolution;
- fallback name;
- `proposedByRole`;
- null behavior;
- authorization;
- campus scope;
- output DTO.

Do not replace an explicit missing-user condition with silent omission unless the existing code already does that.

---

## 6.3. DB-NQ-016 — `EmailImageLayoutNormalizer`

The repository contains `EmailImageLayoutNormalizer` under the Email utilities area. fileciteturn8file0L1-L5

### Target

Batch database metadata lookup:

```text
FileId[]
  ↓
Distinct FileId[]
  ↓
SELECT ... WHERE FileId IN (...)
  ↓
Dictionary<FileId, File>
```

### Deliberately out of scope for this phase

Do **not** add parallel storage I/O merely because database reads were batched.

Reason: storage concurrency can change:

- error ordering;
- retry behavior;
- throughput/load;
- cancellation behavior;
- external side effects.

Storage optimization gets a separate investigation if profiling proves it necessary.

---

## 6.4. DB-NQ-017 — EmailActionTokens

Batching is allowed only for the non-sensitive READ portion.

Preserve exactly:

```text
transaction
+
FOR UPDATE / locking behavior
+
claim semantics
+
uniqueness/idempotency
```

Do not replace row-level claim semantics with:

```text
SELECT all
→ process all
→ mark all
```

unless concurrency equivalence is formally tested.

---

# 7. Phase 3 — HO Dashboard query fan-out

## DB-NQ-001

The current code has a dedicated `GetHODashboardOverviewQueryHandler`; use that handler as the source of truth for the implementation. fileciteturn18file17L86-L90

### Step 1 — Trace actual fan-out

Do not assume "3 × N" until the current code proves it.

Record:

- number of campus iterations;
- queries inside each iteration;
- filters per query;
- date boundaries;
- status conditions;
- feedback conditions;
- any role/authorization conditions;
- ordering;
- fallback behavior.

### Step 2 — Convert only independent aggregation reads

Preferred pattern:

```text
Authorized campus set
        ↓
Set-based query per metric
        ↓
GroupBy(CampusId)
        ↓
Dictionary<CampusId, Metric>
        ↓
Existing DTO assembly
```

### Step 3 — Authorization first

The set-based query must operate on the **same authorized campus set** as the original code.

Never change:

```text
Authorized campuses
```

to:

```text
All campuses
```

and filter afterward.

### Step 4 — Compare every metric

At minimum:

```text
processing count
upcoming count
alerts
```

Compare per campus, not only the grand total.

```text
Before[campus].processing == After[campus].processing
Before[campus].upcoming  == After[campus].upcoming
Before[campus].alerts    == After[campus].alerts
```

Also compare campus ordering.

---

# 8. Phase 4 — Notification Resolver

## DB-NQ-003

This is **high-risk** and must not be optimized by changing target-resolution semantics.

The current resolver intentionally queries the existing `ViewGuestDelegationListQuery` pipeline for several populations and unions their relation contexts because the merged "all" representation can lose a caller's other campus relationship. It then resolves an exact `VisitInstanceId` when the notification is instance-scoped. fileciteturn6file0L2-L6

## 8.1. Step 1 — Remove unused enrichment only

If the resolver does not consume expensive fields such as:

- `ChangeSummary`;
- `NextTask`;

introduce a narrowly scoped option such as:

```text
SkipEnrichment = true
```

only after tracing the full call chain and proving those fields are not used indirectly by:

- authorization;
- relation-context construction;
- DTO mapping;
- fallback logic;
- logging/auditing;
- downstream handlers.

## 8.2. Step 2 — Regression matrix

Test:

- Admin;
- unauthorized caller;
- authorized caller;
- request-scoped notification;
- instance-scoped notification;
- multiple campuses under one request;
- caller with relationships on multiple campuses;
- exact target exists;
- request exists but target instance is inaccessible;
- request exists but requested instance does not exist;
- no matching relation.

## 8.3. Step 3 — Short-circuit only after exact target resolution

Allowed:

```text
Exact VisitInstanceId resolved
+ access verified
→ stop unnecessary downstream work
```

Not allowed:

```text
VisitRequestId found
→ return
```

The resolver's current code explicitly distinguishes request-scoped and instance-scoped resolution, and for an instance-scoped request filters relation contexts by the requested instance. fileciteturn6file0L2-L6

---

# 9. Phase 5 — Background Jobs

Findings:

- `DB-NQ-004`
- `DB-NQ-005`
- `DB-NQ-008`

The repository contains `ActionItemDueReminderHostedService` and email reservation infrastructure, so these paths must be treated as concurrency-sensitive. fileciteturn9file0L2-L5 fileciteturn12file2L11-L15

## 9.1. DB-NQ-004 — batch READs

Potential batch targets:

- Minutes;
- Instances;
- Users;
- Delegation names.

But keep per-item write behavior if retry/failure isolation depends on it.

Before changing `SaveChanges` frequency, trace:

```text
Entity creation
→ generated ID
→ foreign-key consumers
→ SaveChanges
→ side effects
→ retry
→ transaction
```

## 9.2. DB-NQ-005 — Email job

Batching candidates:

- instance metadata;
- campus metadata;
- recipients.

Do not batch away atomic claiming.

Required conceptual sequence remains:

```text
Find candidate
      ↓
Atomically claim/reserve
      ↓
Send
      ↓
Record result
```

Test concurrent workers to prove that the same logical email cannot be claimed twice.

## 9.3. DB-NQ-008 — pending users

If the actual job semantics support it, use deterministic batching such as:

```csharp
.OrderBy(u => u.CreatedAt)
.Take(200)
```

But verify the real schedule, state transition and failure behavior first.

For 500 pending records, expected eventual behavior is:

```text
Tick 1 → 200
Tick 2 → 200
Tick 3 → 100
```

This is only an example acceptance scenario; do not assume it matches the current job contract until the handler is traced.

Test for:

- starvation;
- duplicate processing;
- skipped records;
- records stuck forever;
- concurrent workers;
- failed item retry.

---

# 10. Phase 6 — Complex queries

Findings:

- `DB-NQ-006`
- `DB-NQ-007`
- `DB-NQ-009`
- `DB-NQ-011`
- `DB-NQ-015`

Do not modify these until Phase 1–5 have passed.

For every complex query inspect:

- projection;
- authorization predicate;
- campus/tenant predicate;
- correlated subquery;
- pagination;
- ordering;
- `UNION`/set operations;
- deduplication;
- null semantics;
- count semantics.

## 10.1. `DB-NQ-009` — `MergeFetchCap`

Do not change a cap such as:

```text
1000 → 300
```

merely because 300 is faster.

Measure first:

- maximum records/campus;
- average;
- P95;
- P99 where sample size is meaningful;
- worst-case request;
- pagination behavior;
- deduplication behavior.

Then decide whether a lower cap is semantically safe.

If a lower cap can omit valid results, it is **not** an optimization; it is a behavior change.

---

# 11. Phase 7 — Write optimization

Findings:

- `DB-NQ-018`
- `DB-NQ-020`
- `DB-NQ-021`

This phase has a higher regression risk than simple READ batching.

## 11.1. Never use this rule

```text
Many SaveChanges()
→ one SaveChanges()
→ automatically better
```

## 11.2. Required dependency analysis

Before changing write batching, trace:

```text
Entity A creation
→ generated ID
→ Entity B FK
→ SaveChanges
→ database constraint
→ downstream side effect
```

Also inspect:

- transaction boundary;
- optimistic concurrency;
- pessimistic locking;
- retry semantics;
- partial failure behavior;
- audit events;
- notifications;
- email enqueue/reservation;
- storage operations.

## 11.3. SaveMinutes warning

`SaveMinutesCommandHandler` currently opens a transaction, mutates the minute, reconciles children, calls `SaveChangesAsync`, commits, and only then performs notification/email work. Any optimization that moves these boundaries can change atomicity and side-effect timing. fileciteturn5file0L2-L7

Therefore, write optimization must be reviewed separately from READ batching.

---

# 12. Phase 8 — SQL Injection and data-access security audit

Security is **parallel to all phases**, not a final checkbox.

## 12.1. Static scan

Search the backend for:

```text
FromSqlRaw
FromSqlInterpolated
ExecuteSqlRaw
ExecuteSqlInterpolated
SqlQueryRaw
ExecuteSql
string interpolation in SQL
string concatenation in SQL
dynamic SQL
stored procedure execution
```

The repository currently contains raw-SQL-related matches in infrastructure/application areas, including `OtpService`, concurrency/reservation infrastructure, and service implementations; every hit must be classified by actual usage rather than assumed vulnerable. fileciteturn11file0L1-L5 fileciteturn11file1L6-L10 fileciteturn11file2L11-L15

## 12.2. Parameterization

Never construct SQL like:

```csharp
FromSqlRaw($"SELECT * FROM Users WHERE Name = '{name}'")
```

Prefer parameterized APIs or safe interpolated APIs whose parameters are actually bound as parameters.

Do not assume `FromSqlInterpolated` is safe if the surrounding code later concatenates SQL fragments.

## 12.3. Dynamic identifiers

Values can normally be parameterized; SQL identifiers cannot be treated the same way.

For dynamic:

- `ORDER BY`;
- column names;
- table names;
- sort direction;

use explicit whitelists.

Example conceptual pattern:

```text
user input
   ↓
AllowedSortFields dictionary
   ↓
known SQL expression
```

Never insert arbitrary user input as an SQL identifier.

## 12.4. Authorization before data exposure

Audit every optimized query for:

```text
Caller identity
↓
Role/permission
↓
Campus/tenant scope
↓
Resource/instance ownership or relation
↓
Database query
↓
DTO
```

Do not weaken a query from an authorized relation-scoped query to a global ID lookup merely because the global lookup is cheaper.

## 12.5. IDOR

For every resource endpoint/query accepting an ID, test:

```text
own resource → allowed
same campus but not owned → according to role policy
other campus → denied when policy requires
unrelated instance → denied
non-existent resource → correct not-found/access behavior
```

## 12.6. Mass assignment

For write DTOs verify that callers cannot set fields they do not control, especially fields such as:

- `CampusId`;
- `Status`;
- owner/user IDs;
- approval fields;
- role fields;
- internal flags;
- audit fields.

Do not add a batch update path that accepts arbitrary entity fields.

## 12.7. Unrestricted data exposure

Batching often tempts developers to load an entire entity instead of only required fields.

Prefer:

```text
SELECT only required columns
```

when safe, but verify that no hidden business rule depends on fields being loaded.

---

# 13. Phase 9 — Index audit

Only after query shapes have stabilized.

For each important query inspect:

```text
WHERE
JOIN
ORDER BY
GROUP BY
```

then:

```text
Existing index?
↓
Composite index?
↓
Column order?
↓
Selectivity/cardinality?
↓
EXPLAIN?
↓
Measured improvement?
```

Example candidate shape:

```sql
WHERE CampusId = ?
  AND Status = ?
  AND PlannedStartAt >= ?
```

Do not blindly create an index without checking actual workload and column order.

Every new index must document:

- query benefiting;
- current execution plan;
- expected selectivity;
- new execution plan;
- read improvement;
- write/storage trade-off;
- migration/rollback.

Remember:

```text
More indexes
→ potentially faster reads
→ potentially slower INSERT/UPDATE/DELETE
→ more storage
→ more maintenance
```

---

# 14. Measurement standard

## 14.1. Query count

Measure actual DB commands, not LINQ statement count.

Record:

```text
Before query count
After query count
Delta
```

## 14.2. Duration

Record at least:

- cold/warm condition used;
- representative dataset size;
- number of repetitions;
- median where useful;
- P95 where sample size supports it.

Avoid claiming a performance improvement from one noisy run.

## 14.3. SQL shape

Inspect generated SQL for:

- accidental cartesian joins;
- missing predicates;
- unbounded result sets;
- duplicated predicates;
- unexpected client-side evaluation;
- accidental cross-campus data access;
- parameterization.

---

# 15. Functional equivalence matrix

Every optimized path must compare more than query count.

| Dimension | Before | After | Must match? |
|---|---|---|---|
| Result count | record | record | YES |
| IDs | snapshot | snapshot | YES |
| DTO fields | snapshot | snapshot | YES |
| Null/fallback | behavior | behavior | YES |
| Ordering | behavior | behavior | YES if contractually relevant |
| Deduplication | behavior | behavior | YES |
| Status | behavior | behavior | YES |
| Authorization | behavior | behavior | YES |
| Campus scope | behavior | behavior | YES |
| Instance target | behavior | behavior | YES |
| Exceptions | behavior | behavior | YES |
| Transaction | behavior | behavior | YES |
| Lock/claim | behavior | behavior | YES |
| Side effects | behavior | behavior | YES |

---

# 16. Required edge-case tests

At minimum, test:

- empty result;
- one record;
- many records;
- duplicate IDs;
- null relationships;
- missing user;
- inactive user;
- missing file;
- multiple campuses;
- multiple instances per request;
- wrong campus;
- wrong request;
- wrong instance;
- wrong recipient;
- boundary dates;
- pagination boundaries;
- large result sets;
- concurrent workers;
- retry after failure;
- repeated save/update;
- stale `RowVersion`;
- expired lock;
- wrong lock token.

---

# 17. Multi-campus safety rules

PEMS has request-level and instance/campus-level semantics. Therefore:

## Rule A — Never replace instance identity with request identity

```text
VisitRequestId
≠
VisitInstanceId
```

A request can have multiple campus instances.

## Rule B — Never broaden campus scope during batching

Bad:

```text
Load all campuses
→ filter in memory
```

when the current authorization contract scopes the query earlier.

## Rule C — Preserve relation context

If a query's output includes relation context, do not deduplicate only by `RequestId`.

Use the actual business key required by the code, potentially including:

```text
Relation
Scope
VisitInstanceId
```

The notification resolver currently deduplicates relation contexts using relation/scope/instance semantics; optimization must not collapse those distinctions. fileciteturn6file0L2-L6

---

# 18. Commit and rollback strategy

Do not fix all findings in one commit.

Recommended sequence:

```text
Commit 1  — baseline/instrumentation only, if needed
Commit 2  — AsSplitQuery
Commit 3  — simple READ batching
Commit 4  — HO Dashboard
Commit 5  — Notification Resolver
Commit 6  — Background Jobs
Commit 7  — Complex Queries
Commit 8  — Write optimization
Commit 9  — Security/raw SQL fixes
Commit 10 — Index migrations
```

If instrumentation is temporary, remove it in a separate controlled commit only after evidence is preserved.

## Stop-the-line conditions

Immediately stop the phase if:

- a previously passing test fails;
- output changes unexpectedly;
- a count changes unexpectedly;
- a different campus becomes visible;
- an unauthorized resource becomes accessible;
- notification target changes;
- duplicate email risk appears;
- claim semantics change;
- transaction behavior changes unexpectedly;
- generated IDs are unavailable when previously required;
- SQL becomes non-parameterized;
- result size becomes unexpectedly unbounded.

---

# 19. Definition of Done — per finding

A finding is **DONE** only if all applicable gates pass.

## Code

- [ ] Exact code path traced.
- [ ] Smallest safe change implemented.
- [ ] Business rule unchanged.
- [ ] Validation unchanged.
- [ ] Authorization unchanged.
- [ ] Campus/instance scope unchanged.
- [ ] DTO/output contract unchanged.
- [ ] Error behavior unchanged.
- [ ] Transaction unchanged unless separately approved.
- [ ] Concurrency/locking unchanged unless separately approved.
- [ ] Side effects unchanged.

## Security

- [ ] SQL input parameterized.
- [ ] Dynamic identifiers whitelisted.
- [ ] Raw SQL reviewed.
- [ ] Authorization preserved.
- [ ] IDOR tested.
- [ ] Campus scope tested.
- [ ] Mass assignment checked.
- [ ] Data exposure checked.

## Tests

- [ ] Build PASS.
- [ ] Unit Tests PASS.
- [ ] Relevant Integration Tests PASS.
- [ ] Relevant functional tests PASS.
- [ ] Edge cases PASS.
- [ ] Concurrency tests PASS where applicable.

## Performance

- [ ] Query count measured.
- [ ] SQL shape reviewed.
- [ ] Duration measured.
- [ ] No new N+1.
- [ ] No cartesian explosion.
- [ ] Result set remains bounded/appropriate.

## Evidence

- [ ] BEFORE recorded.
- [ ] AFTER recorded.
- [ ] Difference explained.
- [ ] Regression status recorded.
- [ ] Commit SHA recorded.

---

# 20. Final regression — Phase 10

## Functional

- [ ] Create visit.
- [ ] Update visit.
- [ ] Approve.
- [ ] Reject.
- [ ] Resubmit.
- [ ] Amend.
- [ ] Cancel.
- [ ] Notification.
- [ ] Email.
- [ ] Dashboard.
- [ ] Reports.
- [ ] Minutes.
- [ ] File operations.
- [ ] Partner operations.

## Security

- [ ] Unauthorized user.
- [ ] Wrong campus.
- [ ] Wrong request.
- [ ] Wrong instance.
- [ ] Wrong recipient.
- [ ] SQL Injection probes against relevant inputs.
- [ ] IDOR probes.
- [ ] Dynamic sort/filter validation.
- [ ] Raw SQL review.

## Database

- [ ] N+1.
- [ ] N+M.
- [ ] Cartesian explosion.
- [ ] Query count.
- [ ] Slow query.
- [ ] Large result set.
- [ ] Missing/ineffective index.
- [ ] Raw SQL.
- [ ] SQL Injection.

## Concurrency

- [ ] Email claim.
- [ ] Reminder claim.
- [ ] Visit update.
- [ ] Amendment.
- [ ] Approval.
- [ ] Background retry.

---

# 21. Final BEFORE / AFTER evidence table

Do not fill this table with estimates.

| Function | Before Queries | After Queries | Before Time | After Time | Output | Auth/Scope | Concurrency | Security |
|---|---:|---:|---:|---:|---|---|---|---|
| HO Dashboard | measured | measured | measured | measured | PASS/FAIL | PASS/FAIL | N/A | PASS/FAIL |
| Notification Resolver | measured | measured | measured | measured | PASS/FAIL | PASS/FAIL | PASS/FAIL | PASS/FAIL |
| Save Minutes | measured | measured | measured | measured | PASS/FAIL | PASS/FAIL | PASS/FAIL | PASS/FAIL |
| Email image layout | measured | measured | measured | measured | PASS/FAIL | PASS/FAIL | N/A | PASS/FAIL |
| Multi-collection Visit | measured | measured | measured | measured | PASS/FAIL | PASS/FAIL | N/A | PASS/FAIL |
| Background reminder | measured | measured | measured | measured | PASS/FAIL | PASS/FAIL | PASS/FAIL | PASS/FAIL |

`measured` is a placeholder until real measurements are captured.

---

# 22. Recommended execution order

```text
PHASE 0
Freeze real baseline
    ↓
PHASE 1
AsSplitQuery
    ↓
PHASE 2
Simple READ batching
    ↓
PHASE 3
HO Dashboard
    ↓
PHASE 4
Notification Resolver
    ↓
PHASE 5
Background Jobs
    ↓
PHASE 6
Complex Query / Authorization review
    ↓
PHASE 7
Write optimization
    ↓
PHASE 8
SQL Injection / Security hardening
    ↓
PHASE 9
Index + EXPLAIN
    ↓
PHASE 10
Full regression + BEFORE/AFTER report
```

Security review runs **continuously** across every phase.

---

# 23. Implementation instructions for the engineer/AI agent

Before editing any finding:

1. Read the exact current file from the target branch.
2. Read the complete method, not only the query line.
3. Trace called helpers/services that influence authorization, validation, transaction or output.
4. Read the relevant entity/configuration and database relationship definitions.
5. Read existing tests for the method.
6. Identify all callers and downstream consumers when behavior could be affected.
7. Reconcile the finding with the current code; if the finding no longer exists, do not force the old fix.
8. Write down the invariants before editing.
9. Make the smallest possible change.
10. Run the relevant tests immediately.
11. Measure query count and SQL shape.
12. Compare output/authorization/side effects.
13. Only then proceed to the next finding.

### Forbidden shortcuts

Do not:

- change business conditions to reduce queries;
- remove authorization predicates;
- replace instance-level resolution with request-level resolution;
- remove validation because a batch query cannot express it conveniently;
- change `SaveChanges` boundaries without dependency analysis;
- remove `FOR UPDATE`/claim behavior for performance;
- lower fetch caps without data-volume evidence;
- add indexes without `EXPLAIN`/workload evidence;
- introduce raw SQL with string concatenation;
- use arbitrary user input as a SQL identifier;
- claim performance improvements without actual measurements;
- modify tests simply to make a regression green.

---

# 24. Final safety principle

The correct definition of a successful PEMS query optimization is:

```text
                    PERFORMANCE
                         +
                 CORRECTNESS
                         +
                  AUTHORIZATION
                         +
                  MULTI-CAMPUS
                         +
                   CONCURRENCY
                         +
                    SECURITY
                         +
                  SIDE-EFFECTS
                         ↓
                SAFE OPTIMIZATION
```

A reduction from 100 queries to 5 queries is **not a success** if it causes one unauthorized record, one wrong campus instance, one missing notification, one duplicate email, one lost participant, one changed status, one broken transaction, or one SQL injection vector.

**When evidence conflicts with the optimization target, correctness and security win.**
