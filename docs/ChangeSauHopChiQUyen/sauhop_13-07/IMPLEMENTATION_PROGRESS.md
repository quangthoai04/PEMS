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

## Phase D — identity claim + TRANSFER + cancel 3A + expiry/redaction job — 🚧 IN PROGRESS (D-4 in flight)
- **D-1 INITIAL_CLAIM workflow (plan §16.4/§4.4)** — ✅ DONE.
  - SQL: master + idempotent `06_up_identity_claim_tokens.sql` extend the `email_action_tokens` ENUMs with
    `VISIT_CONTACT_CLAIM` / `VISIT_REQUEST_IDENTITY_CHANGE` (applied to pems_pr3_test; pems_db/pems_test untouched).
  - `IVisitContactClaimService` + `VisitContactClaimService` (Infrastructure): mints the single-use claim token
    (hash-only stored, group key `CONTACT_CLAIM:{id}`, expiry = claim expiry) + sends the invitation email with
    the FRONTEND claim-page URL; also owns the FOR-UPDATE claim locks (Application has no relational EF dep).
    Wired post-commit into BOTH create paths via `V2CreateNotifier.SendContactClaimInvitationAfterCommitAsync`
    (first-create-only, best-effort; crash before send is recovered by resend).
  - Generic anonymous email-action handlers (`GetEmailActionInfo`/`ExecuteEmailAction`) explicitly REJECT the
    claim context — link possession alone never applies a claim; the context is kept out of `All`.
  - Endpoints: anonymous masked landing `GET /api/public/visit-contact-claims/{token}` (write-flag-gated 404,
    masked email only, effective EXPIRED state, `Actionable`); `POST /api/v2/visit-contact-claims/{token}/accept`
    + `/decline` (`[Authorize]`; actor's DB email must equal `new_email_normalized`; VISITOR-only; ACTIVE-only).
  - Accept = one transaction on the FOR-UPDATE-locked claim: `visitor_user_id` set + access ACTIVE + verified_at
    + row-version bump, claim PENDING→APPLIED(new_user_id), token burned ACCEPT/SUCCESS, sibling tokens
    invalidated, event `PRIMARY_CONTACT_CLAIM_APPLIED` + masked field-level audit. Campus decisions untouched.
  - Decline = claim DECLINED + reason + 90d retention stamp + token burned; request stays alive, unowned.
- **D-2 registrant-side management + cancel-3A (plan §16.7)** — ✅ DONE.
  - `POST …/{id}/contact-claim/resend` — registrant-only while unclaimed; supersedes ALL outstanding tokens
    FIRST, restarts the 72h window, resend_count++ (cap 5 → `CONTACT_CLAIM_RESEND_LIMIT`), event RESENT,
    new token+email after commit.
  - `PUT …/{id}/contact-claim` (replace pending contact — the "typo fix"): old claim+tokens SUPERSEDED, contact
    snapshot rewritten (internal-account emails rejected), same-email-as-registrant → immediate ACTIVE link
    (no claim), different email → fresh PENDING claim + invitation; masked-only audit `PRIMARY_CONTACT_REPLACED`.
    Once the contact is ACTIVE both endpoints refuse (`CONTACT_CLAIM_NOT_PENDING`) — that's TRANSFER territory.
  - Cancel-3A in `CancelVisitRequestCommandHandler`: while `primary_contact_access_status=PENDING_CONFIRMATION`
    (+ no owner), the REGISTRANT may cancel a PENDING request under the same 24h/reason rules; audited as
    `VISIT_REQUEST_CANCELLED_BY_REGISTRANT_PENDING_CONTACT`; pending claims are CANCELLED + tokens burned in the
    same txn. Once ACTIVE the exception vanishes (owner-only again). DB trigger already allows it (PR-2).
- **D-3 expiry/redaction job (plan §16.8)** — ✅ DONE. `IVisitContactClaimMaintenanceService` +
  `VisitContactClaimMaintenanceService.RunOnceAsync(now, batch)` — two idempotent FOR-UPDATE-batched sweeps:
  EXPIRE (PENDING past expiry → EXPIRED + 90d retention + event + tokens invalidated; request NOT cancelled)
  and REDACT (terminal past retention → null full email/snapshot/reason + token recipient masked + redacted_at;
  masked email/kind/status/actors/timestamps KEPT; event `IDENTITY_CHANGE_REDACTED` + audit). Hosted job
  `VisitContactClaimMaintenanceHostedService` (poll `IdentityClaims:PollSeconds` 600s, batch 200) runs
  flag-independent (existing v2 data must age out even if flags turn off). APPLIED never redacted here.
  - Tests `VisitContactClaimWorkflowTests` **7/7** (committed + child-first cleanup, pems_pr3_test v2=0):
    accept happy path (link/ACTIVE/rowversion/campus untouched/replay-blocked) · wrong-account EMAIL_MISMATCH +
    flag-off 404 write nothing · decline terminal-but-alive · resend supersedes old link + new link works ·
    maintenance expiry→accept-refused→EXPIRED→redaction (PII gone, masked kept, idempotent) · cancel-3A allowed
    while pending (claim CANCELLED + 3A audit) and Forbidden after accept · replace re-invite (old claim
    SUPERSEDED, C accepts new link) + replace-with-registrant-email instant ACTIVE. v2 group **46/46**.
- **D-4 primary-contact TRANSFER 24h (handoff §6)** — ✅ DONE.
  - SQL: master + idempotent `07_up_transfer_tokens.sql` append `VISIT_CONTACT_TRANSFER` to the
    `email_action_tokens.action_context` ENUM (applied twice to pems_pr3_test — idempotent; pems_db/pems_test
    untouched). Transfer reuses the identity-change row (`change_kind=TRANSFER`, DB pending-guard = one
    PENDING change per request/relation across BOTH kinds) + token store; NO new tables.
  - Initiate `POST /api/v2/visit-requests/{id}/contact-transfer` — registrant or CURRENT ACTIVE contact only;
    requires an established owner (unclaimed contact → `CONTACT_ACCOUNT_NOT_ACTIVE`, that's claim territory);
    lifecycle gate: CANCELLED / any instance DURING_VISIT+ / earliest active start <24h all block; target
    rules: same-email `IDENTITY_CHANGE_EMAIL_UNCHANGED`, internal account
    `CONTACT_EMAIL_INTERNAL_ACCOUNT_CONFLICT`, inactive visitor `IDENTITY_CHANGE_TARGET_NOT_ALLOWED`;
    captures old_user_id/old_email + pending snapshot + `expected_request_row_version`; expiry **24h**
    (never 72h); pending pre-check (FOR UPDATE) + DB guard race both → stable 409
    `IDENTITY_CHANGE_ALREADY_PENDING`; event+audit `PRIMARY_CONTACT_TRANSFER_REQUESTED` (masked); invitation
    token+email post-commit (own context, FE page `/visit-contact-transfer/{token}`).
  - Landing `GET /api/public/visit-contact-transfers/{token}` — masked-only, mutation-free, unknown token ==
    malformed (no enumeration). Accept/decline `POST /api/v2/visit-contact-transfers/{token}/accept|decline`
    ([Authorize], exact invited email via the actor's DB row, VISITOR+ACTIVE): accept = one txn on the
    FOR-UPDATE-locked change — re-checks owner still == old_user_id, `expected_request_row_version` stamp
    (resend RE-STAMPS it so legit edits between invitations never brick the accept), lifecycle window —
    then swaps ONLY `visitor_user_id` + contact snapshot, keeps access ACTIVE, bumps row version, burns
    tokens, event+audit `PRIMARY_CONTACT_TRANSFER_APPLIED`; campus decision/status/host/schedule untouched;
    the OLD account is never locked/deleted (only the relation moves); post-commit notifications to old
    owner + registrant. Replay by the accepted user → idempotent applied response (no second swap);
    concurrent accepts serialize on the row lock (one winner). Decline/cancel/expire keep the old owner.
  - Manage: `GET/POST .../contact-transfer[/resend|/cancel]` (registrant or current ACTIVE owner; resend
    supersedes-tokens-first, restarts **24h**, cap 5; cancel → CANCELLED + retention stamp). Maintenance
    sweep now kind-aware: expiry event `PRIMARY_CONTACT_TRANSFER_EXPIRED` for TRANSFER rows (deadline is
    per-row: claim 72h / transfer 24h); redaction (90d) unchanged, APPLIED never redacted. Generic anonymous
    email-action handlers reject the transfer context too. Cancel-3A is NOT opened by a pending transfer
    (access stays ACTIVE with an owner → the registrant exception never fires; tested).
  - OTP_FALLBACK remains deliberately deferred (Product has not enabled non-Google confirmation).
  - Tests `VisitContactTransferWorkflowTests` **6/6**: accept-swaps-relation-only (old rights until apply,
    24h stamp, campus+old-account untouched, idempotent replay) · initiate guard matrix (unrelated
    forbidden/same-email/internal/flag-off/double-pending/unclaimed-contact) · wrong-account + stale
    row-version → resend re-stamps → new link applies · decline/cancel/expiry all keep the old owner
    (kind-aware expiry event) · pending transfer does NOT open cancel-3A · masked landing + owner state view
    (old owner loses the view after apply).

## Phase E — safe edit + post-approval amendment — 🚧 IN PROGRESS
- **E-1 classifier + safe edit (plan §16.6)** — ✅ DONE.
  - `VisitFieldClassifier` (Application) — THE single backend classification table over stable dotted field
    paths: SAFE (registrant name/org/job/phone; contact name/org/phone — NEVER the email; transportation
    note; note-to-FPTU; media note), PRIVACY_URGENT (`instance.mediaConsentStatus → DECLINED` only),
    APPROVAL_SENSITIVE (delegation/type/purpose/content/language/operational-contact/members), STRUCTURAL
    (schedule). Unknown paths return null → every caller fails closed.
  - `PATCH /api/v2/visit-requests/{id}/safe-details` + `IVisitSafeEditService`/`VisitSafeEditService`:
    full-snapshot-of-the-safe-subset convention, server-side diff, FOR-UPDATE row-version guard →
    stable 409 `VISIT_FORM_CONCURRENCY_CONFLICT` (request + per-instance), started/closed instances
    rejected, the 24h cutoff blocks normal safe edits but the media WITHDRAWAL (+its note) applies even
    inside it; apply = target-only detail mutation + form_revision bump + SAFE_EDIT instance/request
    revision snapshots + canonical recompute (mixed can flip from a note change) + field-level audit
    `VISIT_SAFE_FIELDS_UPDATED`; post-commit notify (URGENT priority + Host included for the withdrawal).
- **E-2 amendments + history (plan §16.6)** — ✅ DONE.
  - Submit `POST /api/v2/visit-requests/{id}/instances/{iid}/amendments`: requester side (registrant or
    ACTIVE contact); instance must be DECIDED (ASSIGNED/BEFORE_VISIT — WAITING routes to pending-edit) and
    start ≥24h away (`AMENDMENT_WINDOW_EXPIRED`); base form/approval revisions + instance row version must
    match (`AMENDMENT_BASE_REVISION_CONFLICT` / concurrency 409); diff vs the ACTIVE detail → immutable
    change rows (field_path, class, old/new JSON; empty diff rejected); ONE pending per instance (DB
    guard + pre-check → `AMENDMENT_ALREADY_PENDING`); NOTHING active mutates; audit
    `VISIT_AMENDMENT_SUBMITTED`; notify current campus leader + host.
  - Decide: approve/reject `POST /api/v2/visit-instances/{iid}/amendments/{aid}/approve|reject` — ONLY the
    CURRENT Staff Leader of that campus (`AMENDMENT_APPROVER_SCOPE_FORBIDDEN` for other-campus leader/HO/
    Admin/Host/requester); approve = FOR-UPDATE-locked amendment + base re-check + target-only apply
    (scalars/schedule/members via the C-1 copy-on-write ops) + form_revision AND approval_revision bump +
    post-apply revision snapshot (history has exactly one row per revision by unique key) + canonical
    recompute + audits `VISIT_AMENDMENT_APPROVED` + `VISIT_INSTANCE_FORM_REVISION_APPLIED`; sibling
    campuses and approval statuses NEVER reset. Reject requires a reason; withdraw = requester side;
    both leave the active snapshot untouched. Expire = `ExpireDueAsync` sweep (window passed or instance
    started) + `VisitAmendmentExpiryHostedService` (600s/200, flag-independent, idempotent).
  - History `GET /api/v2/visit-requests/{id}/history` — scoped metadata-only timeline (request/instance
    revisions, amendments + decisions, campus decisions, masked identity events for managers/HO only);
    leaders see only their campus, hosts only their instance; proposals never presented as active content.
  - Tests: `VisitSafeEditV2Tests` **4/4** (classifier table incl. fail-closed contact-email; apply+revision+
    audit+mixed-recompute+sibling-untouched; editor policy + stale-409s; cutoff + URGENT withdrawal) and
    `VisitAmendmentV2Tests` **4/4** (submit immutability + duplicate/base/empty guards; approve scope matrix +
    target-only apply + member copy-on-write + no-status-reset; reject/withdraw/expire keep active;
    pending-instance + late-window rejections). v2 group **60/60**.
## Phase F — list/search/dashboard/calendar/report/export/email + zero-unclassified audit — 🚧 IN PROGRESS
- **F-1 Class-C surface migration** — ✅ DONE (~35 surfaces; full list in `PR3_PRE_PR4_AUDIT_MAP.md` §10).
  Uniform rule: INSTANCE rows read the conditional `mixed-v2 ? instance.FormDetail.<field> : vr.<field>`
  (single JOIN via the FormDetail nav — no N+1, no correlated scalar subquery; mixed-with-missing-detail
  yields NULL, never the global value); REQUEST rows that cannot show per-campus content are labeled
  `"Khác nhau theo cơ sở"`; v1/non-mixed keep the projection (byte-identical by construction). Batched
  helper `VisitInstanceEffectiveName.ForInstancesAsync` + in-memory `Of`. Report visit_type FILTERS are
  mixed-aware (match any campus detail for request-level, own detail for instance-level); the Staff-Leader
  report resolves a mixed request through THEIR OWN campus's detail. Keyword search is scope-before-keyword
  everywhere it existed and matches per-instance details for mixed v2 (never the projection, never a
  hidden sibling for instance-scoped actors; the visitor path matches any owned campus's detail).
- **F-2 zero-unclassified report** — ✅ DONE (audit map §10): every remaining raw global-field read is a
  classified else-branch / already-effective row / dual-read handler / Class-P v1 writer / v1 fingerprint-
  validator / v2-safe-DTO renderer / non-VisitRequest false positive. ZERO unclassified.
- Tests: `V2MixedListSurfacesTests` **2/2** (helper matrix mixed/non-mixed + Staff-calendar end-to-end:
  each campus leader sees THEIR campus's name, never the sibling's, proving the Pomelo CASE+JOIN
  translation on a real surface).
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
