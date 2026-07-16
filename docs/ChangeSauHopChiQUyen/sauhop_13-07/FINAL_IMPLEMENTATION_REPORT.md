# Per-Campus Form v2 — Implementation Report

**STATUS: IN PROGRESS** — this is NOT the final report. It becomes FINAL only when the entire Definition of
Done is met (create-v2 + edit/resubmit + identity + amendments + list/search/report/export/email + frontend +
E2E + contract cleanup all shipped and all test gates green).

**Branch:** `Canh-Iter1` · **Date:** 2026-07-16

> **Honest status.** This is a **multi-week, multi-PR program** (the master plan scopes it as Phases 0–5 +
> frontend + E2E + cutover). This session completed and **verified** the read-path (Phase A) and the
> command-side read consumers (Phase B-1) — 12 handlers, each tested and committed. The remaining phases
> (create-v2, edit/resubmit, identity state machine + expiry jobs, amendments, list/search/report/export/email
> migration, the entire frontend, E2E, contract migration) are **specified and ready to build** but were not
> implemented here: each needs its own build/test iteration and cannot be produced verifiably in one session
> without violating the DoD gate ("toàn bộ test gates xanh", v1 byte-identical, no untested production code).
> Nothing half-built or unverified was committed.

## 1. Verified & committed this session

| Phase | Scope | Commits | Gate |
|---|---|---|---|
| A | 6 read-detail handlers → instance-level dual-read (mixed→200 target-only, missing→409, v1 byte-identical, no N+1, no leak) | consolidated in Dev merge as `770caa33` (Group 3) + `fb9a11c6` (Group 4); pre-merge: `4bdc1c6d 76a68c53 bae71e03 00f0ee06 50bebef4` + docs | IT 306/306 |
| B-1 | 7 command/export read-consumers source target-instance delegation name for v2 (guarded, v1 byte-identical) | `38f22143` + `836041f0` (ExportDeptLeaderInvoice) | IT 306/306 |
| B-2a | create-v2 aggregate SERVICE + write flag + v2 fingerprint + v2 DTOs (one-txn: request fsv=2 + backend scope/mixed/fingerprint/smallest-campus projection + N instances routed to Staff Leader + N form details + per-campus independent members + links + baseline revisions + VISIT_REQUEST_CREATED_V2 audit + INITIAL_CLAIM 72h) | `0f67eff8` | 11/11 service tests |
| B-2b | create-v2 command + `POST /api/v2/visit-requests` + two-flag gate + submissionId idempotency (transaction-owned, concurrent-race safe) | `4dd1c1d4` | 3/3 command tests · Unit 474/474 · Arch 14/14 · IT **320/320** |

Handlers migrated (Phase A): GetVisitInvitationDetail, GetStaffCalendarDetail, GetRequestDetail (Dept),
GetInvitationDetail (Dept), GetVisitInvitationById, GetAgendaSetupForInstance.
Command/export consumers (Phase B-1, all 7 in the audit map): ApproveCampusInstance, RejectCampusInstance,
InviteVisitParticipant, AssignDepartmentStaff, ExecuteEmailAction, GetEmailActionInfo, **ExportDeptLeaderInvoice**
(instance-level PDF export — stamps the target instance's per-campus delegation name for v2).

**Pattern (reusable for all remaining read paths):** inject `IVisitFormReadService`; keep v1 locals =
`visit.*`; `if (FormSchemaVersion >= FormSchemaVersions.PerCampus) { var d = (await
ResolveCampusFormContentAsync(visit, new[]{ targetInstanceId }, ct))[targetInstanceId]; override locals; }`.
Missing v2 detail → `409 VISIT_FORM_DETAIL_MISSING`, no global fallback.

## 2. Safety invariants (all holding)

- `PerCampusFormV2` read flag **OFF** (no appsettings override; default `false`). Write flag **not created** yet.
- **No v2 data**: `visit_requests.form_schema_version >= 2` count = 0 in pems_db, pems_pr3_test, pems_it_regression.
- **Real DBs untouched**: pems_db / pems_test never mutated; all integration runs on disposable
  `pems_it_regression` recreated from the PR-2 master each run.
- **appsettings.Testing.json** = `pems_test` (restored; the file is gitignored — verified by grep, not git diff).
- **Plan doc** `PEMS_MULTI_CAMPUS_PER_CAMPUS_FORM_AND_IDENTITY_EDIT_PLAN.md` remains **untracked** (not committed).

## 3. Foundation already in place (from PR-2 / PR-3, pre-session)

- **SQL (PR-2)**: 8 new tables (`visit_instance_form_details`, `visit_instance_guest_members`,
  `visit_request_identity_changes` + `..._events`, `visit_instance_amendments` + `..._changes`,
  `visit_instance_form_revision_history`, `visit_request_revision_history`), plus additive columns
  (`form_schema_version`, `has_mixed_campus_details`, `primary_contact_access_status`,
  `primary_contact_verified_at`), 30-min CHECK, per-campus unique keys, cancel-3A trigger. Tested on MySQL 8.0.46.
- **Domain/persistence (PR-3)**: all 8 entities mapped in `ApplicationDbContext`; generated guard columns NOT
  EF-written; `IVisitFormReadService` dual-read resolver (v1 global / v2 detail+links, batched, no N+1).

## 4. Remaining work — ordered, actionable roadmap

### Phase B-2 — create-v2 — ✅ DONE (`0f67eff8`, `4dd1c1d4`)
Service + command + `POST /api/v2/visit-requests`, both flags default OFF, 14 tests green, IT 320/320.
Deferred follow-ons (noted, not blocking): after-commit Staff-Leader notifications; a FluentValidation
structural validator (business validation currently lives in the service); the public-OTP create-v2 path
(the shipped path is authenticated create). Original spec kept below for reference.

<details><summary>B-2 spec (delivered)</summary>
- New input types: `VisitRequestFormDataV2` (registrant + primaryContact + partnerId + visitScope +
  `IList<CampusVisitFormDto>`) and `CampusVisitFormDto` (VisitSlot fields + delegationName, visitType/other,
  purpose, workingContent, visitors[], supportMembers[], operationalContact, workingLanguage,
  transportationNote, mediaConsent[/note], notes, processing{mode,hostUserId}). Contract per plan §5.
- New `IVisitRequestService.CreateV2Async` (mirror `CreateAsync` in `VisitRequestService.cs`): in ONE
  transaction create request (form_schema_version=2, backend-computed visit_scope + `has_mixed_campus_details`
  from normalized copyable content — NOT campus_id/time, NOT client-sent) + N `visit_request_campuses` + N
  `visit_instance_form_details` + per-campus `visit_guest_members` (independent even when copied) +
  `visit_instance_guest_members` links + baseline `visit_instance_form_revision_history` /
  `visit_request_revision_history` + audit + identity INITIAL_CLAIM (§16.4: if normalized primaryContact email
  == registrant email → same account, both FKs, access ACTIVE; else create request but access
  PENDING_CONFIRMATION + `visit_request_identity_changes` PENDING + invitation). Keep the global compatibility
  projection populated (transition only). `VisitRequestFingerprintBuilder` → v2 canonical (sorted campus code,
  start/end, per-campus delegation/visitType, registrant+contact email) + fingerprint version. Idempotency via
  submission id.
- Per-campus FluentValidation (plan §6.2): campus exists/ACTIVE/no-dup; SINGLE=1/MULTI≥2; advance window (72h
  create); end>start; duration ≥30 min; visitType valid, OTHER needs description; required guest/support/
  contact/language/mediaConsent; transportationNote ≤2000, no HTML. Validate each campus independently.
- **Write feature flag** (`PerCampusFormV2WriteOptions`, default OFF, bound in Program.cs) — separate from read
  flag; the v2 create endpoint 404s when off.
- Reuse email normalize (§16.5: Trim + invariant lowercase; no Gmail dot/+tag rules; transaction-safe lookup,
  stable conflict code).
- Tests: create single / multi-same / multi-mixed; per-campus validation; identity same-account vs
  invitation-pending; idempotency; against disposable DB.
</details>

### Phase C — edit pending + resubmit v2 (plan §6.4) — ⬅️ NEXT
Per-campus edit; add/remove campus per lifecycle; full-replace guest/support scoped to target instance;
copy-on-write for legacy shared members; request+instance optimistic concurrency; recompute
scope/mixed/fingerprint/projection; resubmit all-REJECTED keeping instance IDs (no reset of other campuses'
decisions); idempotent audit/revision/notification. Editor policy = `actor == visitor_user_id || actor ==
registrant_user_id` (not extended to cancel/approve/etc.).

### Phase D — identity claim/transfer + cancel-3A + expiry jobs (plan §16.4/16.7/16.8, §4.4)
INITIAL_CLAIM (72h) + TRANSFER (24h) state machines; exact-email Google SSO / OTP-fallback + explicit accept;
resend supersedes old token; transfer self-service only before any campus DURING_VISIT; old account loses
request relation but is never locked/deleted; registrant+active contact are co-editors; registrant cancel-3A
only while access PENDING_CONFIRMATION (keep 24h/started-campus guards + reason); EXPIRED/CANCELLED retained 90
days then redacted (masked email); APPLIED per audit policy; atomic, concurrency-safe, field-level masked
audit; idempotent batchable expiry/redaction background jobs emitting `IDENTITY_CHANGE_REDACTED`.

### Phase E — safe edit + post-approval amendment (plan §16.6)
Safe/correction fields apply immediately (+revision, audit, notify); privacy-urgent media→DECLINED applies even
<24h HIGH/URGENT; approval-sensitive fields → per-campus amendment PENDING_APPROVAL (one pending per instance);
approved snapshot stays active until Staff-Leader approves; approve = atomic patch + bump form_revision &
approval_revision + immutable revision snapshot + field-level old/new; reject/withdraw leaves snapshot; no
reset of sibling campuses; lock self-service from DURING_VISIT.

### Phase F — list/search/dashboard/calendar/report/export/email (audit map §5/§6, plan §16.9)
Migrate every Class-C surface in `PR3_PRE_PR4_AUDIT_MAP.md`: dashboards, calendars, invitation lists, search
(parent + campuses actor may see; scope-before-search; request once with match context; no hidden-campus leak),
reports/invoice/export/print (instance output = target instance; request aggregate = per-campus SECTIONS, no
smallest-campus projection), email preview/templates. Then a repository-wide 10-field audit producing a
**zero-unclassified-reference** report.

### Phase G — frontend (plan §7/§8/§9)
Common registrant + request-level primary contact + campus cards/tabs, each with independent schedule/form/
guest/support/operational-contact/additional-requirements; "copy from campus" = UI-only; add/remove campus;
per-campus validation (≥30 min); submit `campusVisits[]`; detail view per campus with own status/host/revision;
edit-pending/resubmit; initial-claim/transfer contact UI; safe-edit/amendment + approve/reject UI; audit/
revision display by permission; legacy 409 routed to a v2 experience (no raw technical error). Gate: `npm run
build` AND `npm run lint` both 0 errors.

### Phase H — final verification + E2E + rollout readiness
SQL fresh import + verify + upgrade/backfill/idempotency/rollback on disposable MySQL; dotnet build; full
Unit/Arch/Integration on fresh disposable DB; frontend typecheck/lint/unit/build; backend+frontend E2E (single/
multi-same/multi-mixed; per-role permissions; missing detail; 29m59s fail / 30m pass; B claim/transfer/expiry;
cancel-3A; safe-edit/amendment; search no-leak; export/email per-campus; concurrency/idempotency); N+1 &
query-count bounds; assert no raw token/OTP/PII in logs/audit. Read & write flags separate, both default OFF;
prove flag-on flow by test; never auto-enable.

### Phase I — contract cleanup prep (only after zero legacy runtime refs + backfilled)
Prepare guarded migration to drop the 10 global form columns/index/check; update fresh-create to clean v2
schema; test on disposable DB; **never run destructive migration on a real DB**; document cutover + rollback.

## 5. Test counts (latest, verified)
- UnitTests **474/474** (the historical "435" baseline was a stale incremental build; 0 failures throughout).
- ArchitectureTests **14/14**.
- IntegrationTests **320/320** on a fresh disposable `pems_it_regression` (Phase-A read V2 classes + Phase-B-2
  create-v2 service (11) + command (3) classes). pems_pr3_test verified v2_requests = 0 after runs.

## 6. Known limitations / notes
- A **Dev merge** (`ae060dcf`) landed mid-session; Phase-A commits were consolidated into `770caa33`/`fb9a11c6`.
  The merged tree is green (306/306), so Phase-A behavior is intact.
- create-v2 (Phase B-2) is **done** (service + command + endpoint, 14 tests, IT 320/320). Everything downstream
  of it (edit/resubmit, identity confirm/transfer, amendments, list/search/report/export/email migration,
  frontend, E2E, contract cleanup) is **not yet implemented**; §4 is the ready-to-execute spec, Phase C next.
- No production seed/code was changed to make tests pass; the only test-infra change was adding the new
  `IVisitFormReadService` constructor arg (a bare mock) to one unit test.
