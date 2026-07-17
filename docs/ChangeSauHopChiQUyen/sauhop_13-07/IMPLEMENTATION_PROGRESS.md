# Per-Campus Form v2 — Implementation Progress (resumable)

Branch `Canh-Iter1`. This file is the resume anchor: it records exactly what is done, verified, and next.
Baseline at session start: IntegrationTests 306/306, Unit 435/435, Architecture 14/14 (HEAD `4d240bd7`).
`PerCampusFormV2` read flag OFF; write flag not yet created; no v2 data. pems_db/pems_test never mutated.

## Phase A — read-detail migration — ✅ COMPLETE
All Class-B read-detail handlers dual-read (mixed→200 target-only, missing→409 VISIT_FORM_DETAIL_MISSING,
v1 byte-identical): GetVisitInvitationDetail `5ee29ab3`, GetStaffCalendarDetail `4bdc1c6d`, GetRequestDetail
`76a68c53`, GetInvitationDetail `bae71e03`, GetVisitInvitationById `00f0ee06`, GetAgendaSetupForInstance
`50bebef4`. Full IT 306/306.

## Phase B — command read-consumers + create-v2 — 🚧 IN PROGRESS
- B-1 command read-consumers (source target-instance delegation name for v2, v1 byte-identical, guarded):
  ApproveCampusInstance, RejectCampusInstance, InviteVisitParticipant, AssignDepartmentStaff,
  ExecuteEmailAction, GetEmailActionInfo. — ✅ DONE. Each injects IVisitFormReadService; for
  `FormSchemaVersion >= PerCampus` the notification/email/landing delegation name comes from the TARGET
  instance's per-campus detail (never global, never a sibling); v1 unchanged. GetEmailActionInfo/
  ExecuteEmailAction use a shared `ResolveDelegationNameAsync` helper. Verified: build 0-err, Unit 474/474
  (baseline "435" was a stale incremental build), Arch 14/14, full IntegrationTests fresh master 306/306.
- B-1 #7 ExportDeptLeaderInvoice (instance-level PDF export) v2-safe — ✅ DONE `836041f0`.
- B-2 create-v2:
  - **B-2a service core** — ✅ DONE. `PerCampusFormV2WriteOptions` (default OFF, bound in Program.cs);
    `VisitRequestFormDataV2`/`CampusVisitFormDto`/`RegistrantInputV2`/`CampusProcessingV2Dto`;
    `VisitRequestFingerprintBuilder.BuildV2` (per-campus canonical, version-tagged); `IVisitRequestV2CreateService`
    + `VisitRequestV2CreateService` (one-txn: request form_schema_version=2 + backend scope/has_mixed/fingerprint
    + smallest-campus compat projection + N instances routed to campus Staff Leader + N form_details +
    per-campus INDEPENDENT members + composite links + baseline instance/request revisions + VISIT_REQUEST_CREATED_V2
    audit + identity INITIAL_CLAIM 72h when contact≠registrant; two-phase SaveChanges, caller owns tx/commit).
    Tests: `CreateVisitRequestV2ServiceTests` **11/11** green (single/multi-same-mixed0/multi-mixed1/campus+time-
    only-mixed0/member-copy-independent-ids/A==B-ACTIVE/A≠B-PENDING+claim-72h/dur-29m-fail+30m-pass/end=start-fail/
    dup-campus-fail/smallest-campus-projection). Unit 474/474, Arch 14/14.
  - **B-2b command/endpoint** — ✅ DONE. `CreateVisitRequestV2Command` + handler: flag gate (write OFF → 404
    NotFound; write ON + read OFF → ConflictException PER_CAMPUS_V2_READ_REQUIRED; both ON → run), idempotency
    by submissionId (sequential returns same request; concurrent unique-index race on uq_visit_requests_submission_id
    is caught, rolled back, and returns the winner — never a duplicate, never a swallowed error), owns the
    transaction (partial failure rolls back all). `POST /api/v2/visit-requests` endpoint (`[Authorize]`,
    write-flag-gated 404). Tests: `CreateVisitRequestV2CommandTests` **3/3** (write-off→404 writes nothing;
    write-on+read-off→reject writes nothing; idempotent same-submission→same request, then cascade-cleanup so
    pems_pr3_test stays v2=0). Full IntegrationTests fresh master **320/320**; unit 474/474; arch 14/14.
  - **B-2.5 create-v2 close-out** — ✅ DONE. Closes the three B-2 follow-ons:
    (1) `CreateVisitRequestV2CommandValidator` — structural (payload-shape) FluentValidation that runs in the
        MediatR `ValidationBehaviour` BEFORE the handler (campus non-empty/no-dup/≤10, min-30-min, required/
        bounded fields, OTHER⇒visitTypeOther, EN|VI, AGREED|DECLINED, no-HTML transport note, guest/support
        bounds). The service STILL revalidates every DB/clock rule; the validator never replaces it. System-
        derived fields aren't in the DTO, so "client can't send them" is enforced by the DTO shape.
    (2) Post-commit notifications via shared `V2CreateNotifier.NotifyStaffLeadersAfterCommitAsync` — dispatched
        AFTER commit (rollback never notifies), best-effort (logged, never rolls back the committed request;
        no outbox ⇒ not exactly-once), first-create-only (idempotent replay never re-notifies): Staff Leader
        per campus + HO visibility for multi-campus. INITIAL_CLAIM invite email to contact B is Phase D.
    (3) Public OTP create-v2 — `VerifyAndCreateVisitRequestV2Command` + handler + `POST /api/v2/visit-requests/verify`
        (`[AllowAnonymous]`). Mirrors the proven v1 verify OTP-consume/idempotent-replay/race-replay mechanics,
        adds NO new OTP logic (reuses `IOtpService.VerifyChallengeAsync`), provisions ONLY the registrant
        (contact B stays PENDING/INITIAL_CLAIM — never linked before B accepts), builds the aggregate through
        the shared `CreateV2Async`, and consumes the OTP atomically with the create. Flag-gated identically
        (write OFF → 404, so the v1 public verify flow is byte-identical; write ON + read OFF → reject).
    Tests: `CreateVisitRequestV2CommandTests` now also assert first-create-only notification dispatch (Batches==1,
    replay adds none); `VerifyAndCreateVisitRequestV2CommandTests` **3/3** (write-off→404, write-on+read-off→reject,
    both without touching OTP/DB; retry-of-committed-submission replays WITHOUT verifying OTP — a throwing OTP/
    provision fake proves neither is consulted). Targeted **17/17** (11 service + 3 command + 3 public verify).
    pems_pr3_test/pems_db v2_requests = 0. Both flags remain default OFF.

## Phase C — edit pending + resubmit v2 — 🚧 IN PROGRESS
- **C-1 pending edit v2** — ✅ DONE. `VisitRequestV2Canonical` (single source for scope/has_mixed/fingerprint —
  create service refactored onto it, no behavior change) + `VisitFormV2EditDtos` (`VisitRequestEditV2Dto` with
  `ExpectedRequestRowVersion`; `CampusVisitEditV2Dto` with stable `VisitInstanceId` + `ExpectedRowVersion`,
  null id = add-campus) + `VisitRequestV2EditOps` (copy-on-write member full-replace: removes only THIS
  instance's links, deletes a member row only when no sibling still links it; two-phase stage→flush→link) +
  `VisitRequestV2EditService.ApplyPendingEditAsync` (explicit request+instance row-version checks → stable 409
  `VISIT_REQUEST_VERSION_CONFLICT`/`VISIT_INSTANCE_VERSION_CONFLICT`; immutable registrant/partner/BOTH
  account-binding emails; per-instance change detection so untouched siblings get NO member churn/revision/
  row-version bump; add campus (availability recheck + Staff-Leader routing + baseline CREATE revision);
  remove campus only while WAITING with no participants/agendas/logistics (`VISIT_INSTANCE_NOT_REMOVABLE`),
  orphan member cleanup; campus-of-instance change rejected (`VISIT_INSTANCE_EDIT_INVALID` — remove+add);
  recompute scope/mixed/fingerprint/projection; SAFE_EDIT instance+request revision snapshots; field-level
  audit `UPDATE_PENDING_VISIT_REQUEST_V2` with correlation id). Handler + `PUT /api/v2/visit-requests/{id}/pending-edit`
  (both-flag gate; editor policy = registrant OR ACTIVE primary contact — PENDING contact/unrelated blocked;
  v1 requests rejected `VISIT_REQUEST_NOT_PER_CAMPUS_V2`; lifecycle gate fully-pending + 24h; post-commit
  best-effort leader notifications, failure-path sends none) + structural validator reusing the create-v2
  campus rules via `ToFormDto()`. Tests: service **14/14** (rolled-back txns) + command **1/1** (committed +
  child-first cleanup; flags/editor-policy/v1-reject/stale-409/notification lifecycle) — v2 group 32/32,
  pems_pr3_test v2=0.
- **C-2 resubmit rejected v2** — ✅ DONE. `ApplyResubmitAsync` on the shared edit service: `SELECT … FOR UPDATE`
  request row-version guard added to BOTH edit flows (plain-int row_version has no EF concurrency token —
  concurrent writers now serialize on the lock; exactly one winner, loser gets stable 409); in-txn all-REJECTED
  re-check; campus set FIXED + every `visitInstanceId` KEPT (drop/add/campus-swap all →
  `RESUBMIT_CAMPUS_LIST_CHANGED`); per-instance expected-row-version checks; schedule ≥ now+24h/≥30min;
  full operational-availability recheck per campus (same bar as create); decisions snapshotted to
  `audit_log_changes` (`campus_decisions_before_resubmit_json`) BEFORE clearing — rejection history never
  deleted; three-phase flush (parent → PENDING first so the campus trigger accepts REJECTED→WAITING, then
  instance resets + member staging, then links + RESUBMIT revisions); content full-replace via the C-1
  copy-on-write ops; re-route to CURRENT Staff Leader; resubmission_count++ + `LastResubmittedAt/By`;
  recompute scope/mixed/fingerprint/projection; RESUBMIT instance+request revision snapshots. Handler +
  `POST /api/v2/visit-requests/{id}/resubmit` (same two-flag gate + editor policy as pending-edit; post-commit
  best-effort re-process notifications) + structural validator (every slot must carry an instance id).
  Tests: service **6/6** (main flow: ids kept/decisions cleared/reroute/history preserved/canonical read-back;
  partially-rejected block; set-change ×3; stale request+instance 409 + winner + replay-409; <24h; immutable
  contact email) + command **1/1** (gates/policy/not-resubmittable fire before any write, no notification,
  request untouched). v2 group **39/39**, pems_pr3_test v2=0.
### Phase C verification (final): Unit **474/474** · Arch **14/14** · full IntegrationTests **345/345** on
fresh `pems_it_regression` from the PR-2 master; appsettings restored `pems_test`; pems_db/pems_pr3_test
v2_requests = 0; both flags default OFF. Phase C = ✅ COMPLETE.

## Phase D — identity claim/transfer + cancel 3A + expiry/redaction jobs — ⬜ pending (NEXT)
## Phase E — safe edit + post-approval amendment — ⬜ pending
## Phase F — list/search/dashboard/calendar/report/export/email + zero-unclassified audit — ⬜ pending
## Phase G — frontend multi-campus form + detail/edit/identity/amendment UI — ⬜ pending
## Phase H — final verification + E2E + rollout readiness — ⬜ pending
## Phase I — contract cleanup prep (guarded, never run on real DB) — ⬜ pending

## Verified test gates (updated each group)
- Unit 435/435 · Architecture 14/14 · IntegrationTests 306/306 (Phase A).

## Regression recipe (unchanged, clean each run)
Recreate disposable `pems_it_regression` from PR-2 master (`pems_full_v10_..._FIXED.sql`, sed db name,
byte-safe `mysql --default-character-set=utf8mb4 <`); junction trick (`scratchpad/reporoot/{backend,tests}`
→ real, `BaseOutputPath=scratchpad/reporoot/it_full_out/`); swap appsettings.Testing.json pems_test→
pems_it_regression under a bash trap that restores with **explicit sed-back to pems_test** (the file is
gitignored — `git diff` can't verify; grep the value). Never touch pems_db/pems_test.
