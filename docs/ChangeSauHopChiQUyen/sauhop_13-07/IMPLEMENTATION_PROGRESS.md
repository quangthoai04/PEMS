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
    Deferred to a follow-on (noted): after-commit Staff-Leader notifications; a FluentValidation structural
    validator (business validation currently in the service); the public-OTP create-v2 path (this is the
    authenticated create). Both flags remain default OFF, so create-v2 is inert in production.

## Phase C — edit pending + resubmit v2 — ⬜ pending
## Phase D — identity claim/transfer + cancel 3A + expiry/redaction jobs — ⬜ pending
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
