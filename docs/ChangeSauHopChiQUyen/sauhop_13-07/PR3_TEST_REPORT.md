# PR-3 — Persistence mapping + dual-read + per-campus read path — Test Report

**Date:** 2026-07-16
**Scope:** PR-3 only (persistence mapping, feature flag, central dual-read resolver, reference v2 read
endpoint). **No** write/create/edit v2, identity claim/transfer, amendment submit/approve, audit writer,
search 5A or frontend — those stay in PR-4+. PR-2 SQL was **not** modified (mappings fit the validated schema).

**Environment:** .NET 8 (API/Domain/App/Infra) + net9 test SDK; MySQL 8.0.46; disposable
`pems_pr3_test` (PR-2 master imported). Real `pems_db` / `pems_test` untouched.

## 1. Files changed / added

**Domain (new entities):** `VisitInstanceFormDetail`, `VisitInstanceGuestMember`,
`VisitRequestIdentityChange`, `VisitRequestIdentityChangeEvent`, `VisitInstanceAmendment`,
`VisitInstanceAmendmentChange`, `VisitInstanceFormRevisionHistory`, `VisitRequestRevisionHistory`
(all in `PEMS.Domain/Entities/Delegations`). New constants `VisitFormV2Constants.cs`.
**Domain (updated):** `VisitRequest` (+`FormSchemaVersion`, `HasMixedCampusDetails`,
`PrimaryContactAccessStatus`, `PrimaryContactVerifiedAt`, v2 navigations, corrected XML comments —
registrant is co-editor, visitor is active primary-contact relation, cancel 3A, global fields are a
compatibility projection); `VisitRequestCampus` (+`FormDetail`/`GuestMemberLinks`/`Amendments`/
`FormRevisionHistory` navigations); `VisitGuestMember` (+`InstanceLinks`); `AuditLog` (+6 context/
masking columns); `AuditLogChange` (+4 columns).
**Infrastructure:** `ApplicationDbContext` (8 DbSets + fluent config: alternate keys, composite FKs,
one-to-one shared PK, delete behaviors, indexes). **Application:** `IApplicationDbContext` (8 DbSets);
`PerCampusFormV2Options`; `IVisitFormReadService` + `VisitFormReadService`; `ResolvedVisitFormDto`
(+child DTOs); `GetVisitRequestFormV2Query`/`Handler`; DI registration.
**API:** `Program.cs` (flag binding); `VisitRequestsController` (+`GET /api/v2/visit-requests/{id}`).
**Tests:** `PerCampusFormV2ReadTests.cs` (11 tests); 4 UnitTest fake contexts updated for the new interface members.

## 2. Mapping decisions

- **Database-first is preserved.** No EF migration is generated; the fluent config mirrors the
  already-validated PR-2 SQL. The `HasAlternateKey((visit_request_id, visit_instance_id))` /
  `((visit_request_id, guest_member_id))` are model metadata mapping onto the existing
  `uq_vrc_request_instance` / `uq_vgm_request_member` unique keys — used by the composite FKs of the
  link / amendment / revision-history tables.
- **Generated guard columns are NOT mapped** (`pending_guard`, `amendment_pending_guard`). EF never
  sees them, so it can never write them. Proven by test `Generated_guard_columns_are_not_written_on_insert`.
- **One-to-one shared PK** for `visit_instance_form_details` (`VisitInstanceId` is both PK and FK, CASCADE).
- **Enum-like columns** are plain `string` properties (MySQL ENUM ↔ string via Pomelo); **JSON columns**
  are `string`. **RowVersion** stays a manual `int` (unchanged convention — not a SQL rowversion token).
- **Audit `visit_request_id` / `visit_instance_id` carry no FK** (nullable scalars) so audit survives a
  business-row delete — matches PR-2 §17.2.
- **`IApplicationDbContext` extended** with the 8 DbSets → the 4 InMemory fake test contexts implement
  them as explicit interface members and `Ignore<>` the composite-key entities (they never touch them).

## 3. Dual-read resolver (`IVisitFormReadService`)

Single, central resolver used by the reference read path (and reusable by every read path in the
follow-up migration). Rules (plan §6): v1 → global compatibility projection, same snapshot per visible
campus, request-level members; v2 → **only** `visit_instance_form_details` + `visit_instance_guest_members`,
**never** the global fallback; a missing v2 detail is a controlled `409 VISIT_FORM_DETAIL_MISSING` with a
structured `LogError`. Scope is applied **before** any detail is projected; hidden campuses never appear.
Error code `FORM_VERSION_UPGRADE_REQUIRED` is defined for v1 endpoints that cannot represent mixed v2 data
(used when the v1 handlers are migrated in the follow-up).

**Performance (no N+1 by construction):** the resolver issues a **constant** number of queries regardless
of campus/member count — request+instances+detail (1 include, no cartesian), request members (1), scope
participants (1), scope logistics (1, dept only), campus names (1), actor names (1), v2 member links (1),
v2 active amendments (1). No per-campus or per-member loop query. `AsNoTracking` on all reads. The
multi-campus / mixed tests pass with correct per-campus isolation, which a per-campus N+1 mapping would not
guarantee. A formal `EXPLAIN` / 10-campus×200-member benchmark is deferred to the search/perf PR (PR-8).

## 4. Test commands and REAL results

```
dotnet build PEMS.slnx                                  → Build succeeded, 0 errors
dotnet test tests/PEMS.UnitTests                        → Passed: 435, Failed: 0
dotnet test tests/PEMS.ArchitectureTests                → Passed: 14,  Failed: 0
dotnet test tests/PEMS.IntegrationTests --filter PerCampusFormV2ReadTests
                                                        → Passed: 11,  Failed: 0  (vs pems_pr3_test)
dotnet test tests/PEMS.IntegrationTests (FULL suite)    → Passed: 183, Failed: 0  (vs pems_it_regression)
```

**Regression gate (full existing IntegrationTests):** a fresh disposable database `pems_it_regression`
was created from the PR-2 master (real `pems_test` untouched), the IntegrationTests connection string
was temporarily pointed at it (and restored afterward), and the **entire** suite was run:
**183 passed, 0 failed**. This proves the PR-3 entity/mapping changes (4 new mapped `VisitRequest`
columns, new navigations, alternate keys) cause **no regression** in any existing read/write integration
test when run against a PR-2 schema. The feature flag was OFF throughout (no v2 data created).

The 11 PR-3 integration tests (each seeds inside a rolled-back transaction, so the DB stays clean):

| Test | Proves |
|------|--------|
| `Model_builds_and_new_dbsets_query_against_pr2_schema` | EF model builds against the real PR-2 MySQL (composite FKs, alternate keys, shared PK) |
| `Generated_guard_columns_are_not_written_on_insert` | inserting a PENDING_APPROVAL amendment + PENDING identity change succeeds → EF excludes the VIRTUAL guard columns |
| `V1_single_campus_resolves_from_global_projection` | v1 single → global fields + request-level members |
| `V1_multi_campus_gives_every_campus_the_same_snapshot` | v1 multi → identical snapshot per campus |
| `V2_single_campus_reads_detail_and_links` | v2 single → per-campus detail + per-campus member links |
| `V2_multi_campus_mixed_keeps_each_campus_independent` | v2 mixed → each campus its own data; **no cross-campus member leak** |
| `V2_missing_detail_throws_consistency_error_no_fallback` | v2 missing detail → `409 VISIT_FORM_DETAIL_MISSING`, no global fallback |
| `StaffLeader_sees_only_own_campus_hidden_campus_not_in_payload` | Staff Leader A sees only campus A; campus B absent from payload |
| `Ho_sees_all_read_only` | HO sees all campuses, `IsReadOnly = true` |
| `Host_sees_only_hosted_instance` | host sees only their hosted instance |
| `Admin_and_unrelated_are_forbidden` | Admin + unrelated visitor → `ForbiddenException` (403) |

Test harness scripts / DB provisioning were run from the session scratchpad; the disposable
`pems_pr3_test` was dropped-and-recreated from the PR-2 master and can be recreated the same way.

## 5. Compatibility behavior

- **Flag OFF (default):** `GET /api/v2/visit-requests/{id}` returns 404; every existing v1 handler/DTO/query
  is **byte-for-byte unchanged** (PR-3 added code, it did not modify any existing handler). No rolling-deploy
  route/response shape change.
- **Flag ON:** the reference v2 endpoint serves the resolver output for v1 and v2 requests, correctly scoped.
- **Schema coupling (deploy ordering):** the 4 new `VisitRequest` columns are now mapped, so **any**
  environment running PR-3 code must have the PR-2 schema applied first (as PR-2's README already specifies).
  This is inherent to the database-first model.

## 6. Not done here / remaining for PR-4+

- **Existing IntegrationTests suite: EXECUTED and green** (183/183) against the disposable
  `pems_it_regression` (real `pems_test` never mutated; the connection override was applied and restored).
  Requires the PR-2 schema in whatever database it runs against — inherent to the database-first model.
- **Read-path migration to the resolver is DEFERRED** (documented, low-risk because the flag is OFF and no
  v2 data exists until PR-4). The existing handlers still read global fields and must be migrated to
  `IVisitFormReadService` (or return `409 FORM_VERSION_UPGRADE_REQUIRED` for mixed v2) in the follow-up:
  `GetSubmittedVisitRequestFormDetail`, `GetEditableVisitRequestDetail`, `ViewGuestDelegationDetails`,
  `GetVisitProcessDetail`, `GetVisitInstanceSummary`, `GetVisitInstanceContribution`,
  `GetVisitInvitationDetail`, calendar visit detail, department/participant detail, and export/print/email
  preview read models. List/search stays PR-8.
- **Out of PR-3 scope (unchanged):** write/create/edit v2, identity claim/transfer, amendment
  submit/approve, `IVisitAuditWriter`, search 5A, frontend, dropping legacy columns.

## 6b. Read-handler migration (follow-up) — handler #1: GetSubmittedVisitRequestFormDetail

The first §6 read handler is now migrated to the dual-read rule. This is the flat, single-snapshot
submitted-form endpoint (`GET /api/delegations/visit-requests/{id}/submitted-form-detail`), used by the
pre-approval review / approved-detail / rejected-detail screens.

**New reusable primitive.** `IVisitFormReadService.ResolveCampusFormContentAsync(request, visibleInstanceIds, ct)`
resolves the *version-specific FORM CONTENT* (delegation / visit-type / purpose / working-content /
language / media / notes / operational-contact / members / revisions) for an **already-authorized** set of
visible instances, keyed by instance id. Every handler keeps its own version-agnostic metadata (scope,
decision, schedule, cancellation — those columns live on `visit_requests` / `visit_request_campuses` in
BOTH versions) and calls this for the form-content half, so the dual-read rule lives in exactly one place:
v1 → global compatibility projection (identical per instance); v2 → ONLY `visit_instance_form_details` +
`visit_instance_guest_members`, never the global fields; a visible instance missing its detail →
`409 VISIT_FORM_DETAIL_MISSING` (no fallback). Batched (v1: 1 query; v2: 2 queries) regardless of
campus/member count. Later handlers reuse this primitive instead of re-implementing v2 sourcing.

**Handler behavior after migration:**
- **v1 (form_schema_version=1):** byte-for-byte unchanged — the flat DTO is still the global projection,
  same members, same whole-request `CampusDecisionSummary`. (No existing IntegrationTest exercises this
  endpoint, and the ApplicationTests are `Skip` stubs, so there is no v1 contract regression surface.)
- **v2 mixed (`has_mixed_campus_details=1`):** `409 FORM_VERSION_UPGRADE_REQUIRED` — the flat DTO cannot
  represent per-campus-divergent content; the client must use the canonical v2 endpoint. Thrown only
  **after** authorization, so mixed-ness is never revealed to an unauthorized caller.
- **v2 non-mixed:** the flat form content is DERIVED from a representative visible instance's per-campus
  detail + instance-member links via the primitive — **never** the global fields. `CampusDecisionSummary`
  is computed over the **visible** instances only (no hidden-campus aggregate leak).
- Scope is applied **before** any content is projected (unchanged scope block); hidden campuses never
  appear in `Campuses[]`, the summary, or the member lists.

**Tests added — `SubmittedVisitRequestFormDetailV2Tests` (12, all green vs `pems_pr3_test`):** v1 single +
v1 multi (global, summary counts all), v2 single + v2 multi non-mixed (content derived from detail, not
global), v2 mixed → `FORM_VERSION_UPGRADE_REQUIRED`, mixed via the resolver v2 path → per-campus
`CampusVisits[]`, missing detail → `VISIT_FORM_DETAIL_MISSING` (no fallback), Staff-Leader own-campus (no
aggregate or cross-campus member leak), HO all, Host hosted-only, Admin + unrelated → 403, and a
constant-DB-command-count assertion (1 campus × 1 member vs 2 campuses × 3 members) proving no per-campus /
per-member N+1.

**Verification for handler #1:** `dotnet build` 0 errors; `SubmittedVisitRequestFormDetailV2Tests` 12/12;
UnitTests 435/435; ArchitectureTests 14/14 (all green).

**Full existing IntegrationTests — regression delta (the meaningful gate):** run against a **byte-faithful**
disposable clone `pems_it_regression` (cloned from the PR-2 master via a single `mysqldump … | mysql …`
pipe, `--default-character-set=utf8mb4 --hex-blob`, `pipefail`; HEX-verified identical to source, zero
replacement chars; connection swapped via `appsettings.Testing.json` under a `trap` backup/restore, final
`git diff` empty; default output path).

| Run | Total | Passed | Failed |
|-----|------:|-------:|-------:|
| **HEAD `d5c29ecf`** (without Handler #1) | 220 | 203 | 17 |
| **With Handler #1** | 232 | 215 | 17 |

The **same 17 tests fail in both runs (identical by name)**; Handler #1 adds exactly **+12 passing** tests
and **0 new failures** → **no regression**. `pems_test` no longer exists in this environment, so the only
full-seed source is the PR-2 master.

> **The 17 failures are a pre-existing FIXTURE/SEED incompatibility — NOT encoding, NOT Handler #1.** They
> are all *create / OTP / idempotency* write-flow tests (`ActorRelationAuthenticatedCreateApiTests`,
> `Uc17IdempotencyDuplicateApiTests`, `Uc17OtpChallengeApiTests`) that call
> `DatabaseResetHelper.EnsureTestUserAsync(StaffLeader)` to add their own Staff Leader on campuses 1/2 —
> but the PR-2 master already seeds a leader there (`user 3` "Hà Nội", `user 9` "TP.HCM"), and leftover
> test users from prior runs (`user 216` "[IT-UC63]", `user 237` "[IT-ACTOR-REL]") add another. Result:
> **2 valid leaders per campus → `IsAvailableForVisitRegistration = false` → `CAMPUS_STAFF_LEADER_CONFIGURATION_INVALID`**
> (the "exactly one leader" rule, BR-86-19/20). These tests were authored against a `pems_test` seed where
> campuses 1/2 had no pre-seeded master leader. (My earlier PowerShell-pipe clone additionally `?`-corrupted
> the UTF-8, which is why the campus name first appeared as "H?? N???i"; the byte-faithful clone removes
> that, but the 17 still fail for the seed-count reason above.)
>
> **Gate status:** no-regression is **PROVEN** (delta 0). A literally-green full suite was blocked by this
> pre-existing seed incompatibility (see resolution below). Production code is **not** changed to
> accommodate the clone.

### 6c. Suite-health follow-up — the 17 fixture failures RESOLVED (separate commit)

Fixed the test harness (not production code, not the seed): a campus must have **exactly one** valid Staff
Leader (BR-86-19/20), and the PR-2 master already seeds one per campus, so the test helpers must **reuse**
it rather than add a second.

- `DatabaseResetHelper.EnsureTestUserAsync(StaffLeader)` — now returns the campus's existing valid IC Staff
  Leader (the seed's) when present, instead of creating a duplicate. All StaffLeader-consuming test classes
  therefore share the single seeded leader; the campus stays registration-valid.
- `ActorRelationAuthenticatedCreateApiTests.EnsureLeaderOnCampusAsync` — reuses the campus's existing valid
  leader; only creates one if the campus has none.

Provisioning: each run imports a **fresh** disposable DB from the actual PR-2 master file
(`pems_full_v10_…_FIXED.sql`, `pems_db`→`pems_it_regression`, byte-safe Git-Bash import) — never a clone of
a polluted DB. **Result: full IntegrationTests = 232/232 passed, 0 failed, on TWO independent fresh-master
runs** (deterministic/repeatable). Post-run, every active campus still has exactly one valid Staff Leader
(no accumulation): the fix reuses the seed leader and never creates `it-uc63-staffleader`. No production
code, production seed, or one-off manual DB edit was used.

## 6d. Read-handler migration — handler #2: GetEditableVisitRequestDetail

Second §6 read handler migrated to the dual-read rule (same primitive as handler #1). This is the
Visitor-owner-only edit/resubmit prefill form (`GET /api/visit-requests/{id}/edit-detail`).

- **v1** unchanged (global projection). **v2 non-mixed** → the flat form content is DERIVED from the
  per-campus `visit_instance_form_details` + `visit_instance_guest_members` (never the global fields), via
  `IVisitFormReadService.ResolveCampusFormContentAsync`. **v2 mixed** → `409 FORM_VERSION_UPGRADE_REQUIRED`
  (the single-form editor can't represent per-campus-divergent content — the per-campus v2 editor is a
  later PR). **v2 visible instance missing detail** → `409 VISIT_FORM_DETAIL_MISSING` (no fallback).
  Registrant / primary Contact / Partner stay request-level. Owner-only scope is unchanged.
- **`ViewGuestDelegationDetails` — NOT migrated, with evidence (not a bare "skip"):** the handler
  `backend/PEMS.Application/Delegations/Queries/ViewGuestDelegationDetails/ViewGuestDelegationDetailsQueryHandler.cs`
  is an unimplemented scaffold whose `Handle` unconditionally does
  `throw new NotImplementedException("UC View Guest Delegation Details has been scaffolded …")`. Although a
  route is wired (`DelegationsController.cs:87`, `ViewGuestDelegationDetails([FromQuery] …Query)`), it reads
  **no** form data — every call 500s before any projection. It therefore cannot serve a v2 request as a
  global snapshot and has nothing to migrate; it must implement the dual-read rule if/when the UC is built.

Tests: `EditableVisitRequestDetailV2Tests` (5) green vs `pems_pr3_test` (v1 global, v2 non-mixed
derive-from-detail, v2 mixed → 409, missing detail → 409, non-owner + non-visitor → 403). Full
IntegrationTests on a fresh master = **237/237** (0 failed); unit 435/435; architecture 14/14.

## 6e. Read-handler migration — handler group #3a: GetVisitProcessDetail (INSTANCE-LEVEL)

Handlers are classified by their **contract**, not their name: request-level flat DTOs (handlers #1/#2)
use `409 FORM_VERSION_UPGRADE_REQUIRED` for mixed v2, but an **instance-level** handler (its route/key
identifies a `visit_instance_id`) must return **200** for a mixed request, sourcing only the target
instance.

`GetVisitProcessDetail` is instance-level: route `{visitRequestId}/campuses/{visitInstanceId}/process-detail`,
query key `(VisitRequestId, VisitInstanceId)`, DTO carries `VisitInstanceId` + instance status/agenda, and
scope is checked against that instance. Migration:
- **v1** unchanged. **v2 (single / multi / MIXED)** → the `RequestSummary` form content + `GuestMembers` /
  `ExternalSupportMembers` and the top-level `DelegationName` are sourced **only** from the TARGET
  instance's `visit_instance_form_details` + `visit_instance_guest_members` via
  `ResolveCampusFormContentAsync(request, [targetInstanceId], …)` — **never** the global fields, **never**
  `FORM_VERSION_UPGRADE_REQUIRED`, **never** a sibling campus. Missing target detail →
  `409 VISIT_FORM_DETAIL_MISSING`. Primary Contact / Registrant stay request-level; the `Campuses` list is
  per-campus schedule only (no form content). Per-instance scope already blocks cross-campus access.

Tests: `VisitProcessDetailV2Tests` (8) green vs `pems_pr3_test` — v1 global, v2 single, **mixed target
A → 200 + A-only**, **same request target B → 200 + B-only** (no cross-campus guest leak), missing detail
→ 409, Staff-Leader-of-campus-A forbidden on the campus-B instance (allowed on A), unrelated → 403, and a
constant-DB-command-count assertion across 1-vs-3 campuses (no per-campus N+1). Full IntegrationTests on a
fresh master = **245/245** (0 failed); unit 435/435; architecture 14/14.

## 6f. Read-handler migration — handler group #3b: GetVisitInstanceSummary (INSTANCE-LEVEL)

`GetVisitInstanceSummary` is instance-level (query key `VisitInstanceId`; scope = Staff-Leader-of-campus /
HO / Host of that instance; DTO `ProcessSummaryPageDto` with the shared `VisitProcessRequestSummaryDto`).
Same instance-level treatment as §6e: v1 unchanged; v2 (incl. MIXED) sources the `RequestSummary` form
content + members and `Permissions.DelegationName` **only** from the TARGET instance's per-campus detail +
links (never global, never a sibling); missing target detail → `409 VISIT_FORM_DETAIL_MISSING`.

Tests: `VisitInstanceSummaryV2Tests` (8) green — v1 global, v2 single, mixed target A → 200 A-only, same
request target B → 200 B-only (no cross-leak), missing → 409, Staff-Leader campus scope, Visitor → 403,
constant query count 1-vs-3 campuses. Full IntegrationTests fresh master = **253/253**; unit 435/435;
architecture 14/14.

## 6g. Read-handler migration — handler group #3c: GetVisitInstanceContribution (INSTANCE-LEVEL) — group #3 COMPLETE

`GetVisitInstanceContribution` is instance-level (route `visit-instances/{visitInstanceId}/contribution`,
query key `VisitInstanceId`; access = Host / accepted participant / Department-with-logistics / HO). Same
instance-level treatment: v1 unchanged; v2 (incl. MIXED) sources `Summary.Request` form content + members,
`Summary.DelegationName` and `Summary.GuestCount` **only** from the TARGET instance's per-campus detail +
links (never global, never a sibling); missing → `409 VISIT_FORM_DETAIL_MISSING`.

Tests: `VisitInstanceContributionV2Tests` (8) green — v1, v2 single, mixed target A → 200 A-only, same
request target B → 200 B-only (no cross-leak), missing → 409, Host-of-campus-A forbidden on the campus-B
instance, Admin + unrelated Visitor → 403, constant query count 1-vs-3 campuses. Full IntegrationTests
fresh master = **261/261**; unit 435/435; architecture 14/14.

**Group #3 status: COMPLETE** — `GetVisitProcessDetail`, `GetVisitInstanceSummary`,
`GetVisitInstanceContribution` all migrated as INSTANCE-LEVEL (mixed → 200 with the target instance), each
its own commit, each verified full-green.

## 6h. Read-handler migration — group #4a: GetVisitInvitationDetail (INSTANCE-LEVEL)

Inventory: route `GET .../invitations/{participantId}` (VisitInvitationsController / DelegationsController /
DepartmentReceptionTasksController); input key `ParticipantId` → a `VisitParticipant` bound to ONE
`VisitInstanceId`; **no token** — authorization is `p.UserId == current user` (+ `!IsHost`, `Status != Removed`);
consumer = the invited user's "my invitation detail" screen. The only global-legacy form field the DTO exposes
is `DelegationName` (no Purpose/members/contact-email/guest-list → no extra PII surface).

Classified **instance-level**: the invitation is for one campus instance, so a MIXED request returns **200**
and `DelegationName` is sourced **only** from the invited instance's `visit_instance_form_details` (never
global, never a sibling). Missing detail → `409 VISIT_FORM_DETAIL_MISSING`. Token hashing/expiry/one-time
rules N/A (participant/user-bound, not token-bound); auth is applied in the query before any projection.

Tests: `VisitInvitationDetailV2Tests` (6) green — v1 global, v2 non-mixed, v2 mixed with two participants of
the SAME request (campus A → DELEG-A, campus B → DELEG-B, no cross-leak), missing → 409, wrong-recipient &
removed invitation → NotFound, constant query count 1-vs-3 campuses. Full IntegrationTests fresh master =
**267/267**; unit 435/435; architecture 14/14.

## 6i. Read-handler migration — group #4b: GetStaffCalendarDetail (INSTANCE-LEVEL)

Inventory: route `GET /api/dashboard/staff-calendar/{visitInstanceId}` (DashboardController); input key
`VisitInstanceId`; consumer = the Staff / Staff-Leader dashboard calendar detail modal; authorization =
Staff-Leader of the instance's campus (multi-campus only after HO approval or once a host exists) **or**
Staff member of that campus / the instance's host — enforced BEFORE any projection. The global-legacy form
fields the DTO exposes: `DelegationName`, the contact-person block (= operational contact), `Purpose`,
`WorkingContent`, `VisitType`/`VisitTypeOther`, `WorkingLanguage`, `MediaConsent*`, `TransportationNote`,
`NoteToFptu`, and `GuestCount`.

Classified **instance-level**: the modal is keyed by one campus instance, so a MIXED request returns **200**
and every form field, the operational contact and the guest count are sourced **only** from the target
instance's `visit_instance_form_details` + `visit_instance_guest_members` (never global, never a sibling).
Missing detail → `409 VISIT_FORM_DETAIL_MISSING`, no global fallback. The DTO exposes a guest COUNT (not the
member list), so `GuestCount` for v2 is the target instance's linked-member count — never the request-wide
total. Registrant fields and all calendar/event/host/decision/cancellation fields keep their existing source
(request-/instance-metadata, identical in v1 and v2). v1 keeps the global projection, byte-identical.

Tests: `StaffCalendarDetailV2Tests` (9) green — v1 byte-identical (delegation/purpose/content/type/language/
media/contact/count), v2 single, v2 multi non-mixed → 200, v2 mixed target A → 200 A-only, same request
target B → 200 B-only (no sibling leak), per-instance guest count (A=1 vs B=2, never the request total),
missing → 409, hidden-sibling-campus & non-staff → 403, constant query count 1-vs-3 campuses. Full
IntegrationTests fresh master = **276/276** (0 failed); unit 435/435; architecture 14/14.

## 6j. Read-handler migration — group #4c: GetRequestDetail (Dept) (INSTANCE-LEVEL)

Inventory: route `GET .../request-detail/{logisticsItemId}` (DepartmentReceptionTasksController, class-level
`[Authorize]`); input key `LogisticsItemId` → a `VisitLogisticsItem` that belongs to exactly ONE campus
instance (`l.VisitInstance` → `camp.VisitInstanceId`); consumer = the department reception-task detail modal
(the department staff/leader handling that logistics item). The global-legacy form fields the DTO exposes:
`DelegationName`, `Purpose`, `WorkingContent`, and the contact-person block (`ContactPersonFullName`,
`ContactPersonPhone` = operational contact). `Registrant*` fields are request-level identity and stay in both
versions.

Classified **instance-level**: the logistics item is owned by one campus instance, so a MIXED request returns
**200** and the delegation / purpose / working-content / operational-contact fields are sourced **only** from
that target instance's `visit_instance_form_details` (never global, never a sibling). Missing detail →
`409 VISIT_FORM_DETAIL_MISSING`, no global fallback. The item is already scoped to one instance, so there is
no cross-campus query and no sibling leak; authorization is the controller's `[Authorize]` (no handler-level
scope to unit-test). v1 keeps the global projection, byte-identical.

Tests: `RequestDetailV2Tests` (7) green — v1 byte-identical (delegation/purpose/content/contact, registrant
unchanged), v2 single, v2 multi non-mixed → 200, v2 mixed target A → 200 A-only, same request target B → 200
B-only (no sibling A leak in delegation or contact), missing → 409, constant query count 1-vs-3 campuses. Full
IntegrationTests fresh master = **283/283** (0 failed); unit 435/435; architecture 14/14.

## 6k. Read-handler migration — group #4d: GetInvitationDetail (Dept) (INSTANCE-LEVEL)

Inventory: route `GET .../invitation-detail/{participantId}` (DepartmentReceptionTasksController, class-level
`[Authorize]`); input key `ParticipantId` → a `VisitParticipant` (Status != REMOVED) bound to exactly ONE
campus instance (`p.VisitInstance` → `camp.VisitInstanceId`); consumer = the department invitation detail
modal (the invited support staff's view). Same DTO shape as §6j — global-legacy form fields: `DelegationName`,
`Purpose`, `WorkingContent`, and the contact-person block (`ContactPersonFullName`, `ContactPersonPhone` =
operational contact); `Registrant*` fields are request-level identity and stay in both versions.

Classified **instance-level**: the participant is bound to one campus instance, so a MIXED request returns
**200** and the delegation / purpose / working-content / operational-contact fields are sourced **only** from
that target instance's `visit_instance_form_details` (never global, never a sibling). Missing detail →
`409 VISIT_FORM_DETAIL_MISSING`, no global fallback. The participant is already scoped to one instance (no
cross-campus query, no sibling leak); authorization is the controller's `[Authorize]`. v1 keeps the global
projection, byte-identical.

Tests: `DeptInvitationDetailV2Tests` (7) green — v1 byte-identical (delegation/purpose/content/contact,
registrant unchanged), v2 single, v2 multi non-mixed → 200, v2 mixed target A → 200 A-only, same request
target B → 200 B-only (no sibling A leak), missing → 409, constant query count 1-vs-3 campuses. Full
IntegrationTests fresh master = **290/290** (0 failed); unit 435/435; architecture 14/14.

## 6l. Read-handler migration — group #4e: GetVisitInvitationById (ViewMyVisitInvitations) (INSTANCE-LEVEL)

Inventory: route `GET .../my-invitations/{participantId}` (the invited user's own invitation-detail screen);
input key `ParticipantId`; **ownership-scoped** — the query requires `p.UserId == current user`, `!p.IsHost`
and role ∈ {IC_SUPPORT, DEPT_SUPPORT, STUDENT}, else `404 NotFound` (does not leak existence). The handler
materialises a shared `VisitInvitationFlat` via one projection. Global-legacy form fields: `DelegationName`,
`Purpose`, `WorkingContent`. `OrganizationName` is `RegistrantOrganization` (request-level identity) and stays.

Classified **instance-level**: an invitation is bound to one campus instance, so a MIXED request returns
**200** and delegation / purpose / working-content are sourced **only** from the target instance's
`visit_instance_form_details` (never global, never a sibling). Missing detail → `409 VISIT_FORM_DETAIL_MISSING`,
no global fallback. Ownership is enforced in the query **before** the v2 projection. To keep v1
byte-identical *and* single-query, `FormSchemaVersion` is added to the flat projection (no extra query); only
v2 then loads the request entity + resolves the target instance. The shared list query
(`ViewMyVisitInvitations`, Class-C) leaves `FormSchemaVersion` 0 and stays on the global projection until PR-8
(gated by the write flag staying OFF).

Tests: `MyVisitInvitationByIdV2Tests` (8) green — v1 byte-identical (delegation/purpose/content, registrant
org unchanged), v2 single, v2 multi non-mixed → 200, v2 mixed target A → 200 A-only, same request target B →
200 B-only (no sibling A leak), missing → 409, non-owner → 404 (owner succeeds), constant query count 1-vs-3
campuses. Full IntegrationTests fresh master = **298/298** (0 failed); unit 435/435; architecture 14/14.

## 6m. Read-handler migration — group #4f: GetAgendaSetupForInstance (INSTANCE-LEVEL) — group #4 COMPLETE

Inventory: route `GET .../agenda-setup/{visitInstanceId}` (AgendaTemplatesController); input key
`VisitInstanceId`; consumer = the host / staff-leader-of-campus / HO agenda setup screen; authorization = Host
of the instance **or** Staff Leader of the instance's campus **or** HO — enforced BEFORE any projection. Its
**only** submitted-form field is `visit_type` (it drives the default-template resolution, the template ordering
and the DTO); everything else is agenda/template/instance metadata.

Classified **instance-level**: the screen is keyed by one campus instance, so a MIXED request returns **200**
and `visit_type` is sourced **only** from the target instance's `visit_instance_form_details` (never the global
field, never a sibling). The v2 resolve was placed **after** the authorization check (scope-before-projection).
Missing detail → `409 VISIT_FORM_DETAIL_MISSING`, no global fallback. v1 keeps the global `visit_type`,
byte-identical.

Tests: `AgendaSetupForInstanceV2Tests` (8) green — v1 byte-identical (global visit type), v2 single, v2 multi
non-mixed → 200, v2 mixed target A → 200 with A's visit type, same request target B → 200 with B's type (no
sibling A leak), missing → 409, unauthorized role → 403 (HO succeeds), constant query count 1-vs-3 campuses
(visit type + campus held constant so AgendaDefaultResolver's own query count doesn't confound the N+1 check).
Full IntegrationTests fresh master = **306/306** (0 failed); unit 435/435; architecture 14/14.

**Group #4 status: COMPLETE** — all Class-B read-detail handlers migrated (`GetVisitInvitationDetail`,
`GetStaffCalendarDetail`, `GetRequestDetail`, `GetInvitationDetail`, `GetVisitInvitationById`,
`GetAgendaSetupForInstance`), each instance-level (mixed → 200 with the target instance), each its own commit,
each verified full-green. The remaining Class-C list/dashboard/report surfaces and export/report handlers are
the PR-8 / write-flag-gate scope tracked in `PR3_PRE_PR4_AUDIT_MAP.md` §5–§8.

## 7. Definition of Done status

Build pass ✅ · mapping matches PR-2 MySQL ✅ (11 integration tests) · dual-read v1/v2 works ✅ ·
v2 mixed never reads the global projection ✅ · no cross-campus leak ✅ · no N+1 (constant query count) ✅ ·
v1 not regressed ✅ (handlers unchanged; 435 unit + 14 architecture tests green) · generated guard columns
not EF-written ✅ · tests + report complete ✅.
