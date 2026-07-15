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

## 7. Definition of Done status

Build pass ✅ · mapping matches PR-2 MySQL ✅ (11 integration tests) · dual-read v1/v2 works ✅ ·
v2 mixed never reads the global projection ✅ · no cross-campus leak ✅ · no N+1 (constant query count) ✅ ·
v1 not regressed ✅ (handlers unchanged; 435 unit + 14 architecture tests green) · generated guard columns
not EF-written ✅ · tests + report complete ✅.
