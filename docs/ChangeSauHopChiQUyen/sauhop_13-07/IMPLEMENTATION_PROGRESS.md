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
## Phase G — frontend multi-campus form + detail/edit/identity/amendment UI — ✅ DONE (G-1/2/3 + G-4A/G-4B)
- **G-1 foundation** — ✅ DONE: typed v2 API client (`visitRequestV2Api.ts`), invitation landing page
  (`/visit-contact-claim/:token`, `/visit-contact-transfer/:token` — anonymous MASKED info; accept/decline
  need the matching Google login), `ContactIdentityPanel` (claim resend/replace; transfer initiate/
  resend/cancel), `VisitAmendmentPanel` (old→new diff, approve/reject-with-reason/withdraw),
  `VisitHistoryTimeline` (scoped masked timeline). NOTE: the G-1 commit was pushed before its scope
  could be amended, so the fix landed FORWARD (`0cec2972`): App.tsx routes wired + the two internal
  planning docs untracked (kept local-only). tsc 0 errors, vite build ✓.
- **G-2 per-campus form v2** — ✅ DONE:
  - `visitRequestV2.schema.ts` — registrant + primaryContact request-level; `campusVisits[]` each a
    COMPLETE independent snapshot (schedule/content/people/operationalContact/requirements) with stable
    `clientKey` identity; 30-MINUTE minimum in ms math (29m59s fails, 30m00s passes — never auto-adjusts
    the typed end time), 10-campus / 200-member caps, per-index duplicate-campus errors.
  - `visitRequestV2Form.ts` pure utils — `cloneCampusVisitContent` (deep copy; target keeps identity/
    campus/schedule), confirmed `applyContentToAllCampuses`, `buildV2CreatePayload`/`buildV2EditPayload`
    (REAL `VisitRequestFormDataV2`/`VisitRequestEditV2Dto` contracts — no sameForAll, no client-sent
    scope), `migrateV1DraftToV2` (global draft duplicated into every selected campus),
    `mapServerFieldPathToFormPath` (FluentValidation path → exact RHF campus/nested field),
    `applyImportedMembersToCampus` (per-campus Excel, never global).
  - `visitRequestV2DraftStorage.ts` — draftSchemaVersion **3** under its OWN key (v1 form + its draft
    untouched); load = v3 first, else one-time IN-MEMORY migration of the global draft (an existing v3
    draft is never overwritten by the older global one); sanitize strips OTP/session/files.
  - `useVisitRequestFormV2.ts` — `useFieldArray` campusVisits; add(copy)/remove(confirm-if-dirty)/
    copy-into/two-step confirmed apply-to-all; server-error mapping lands on the exact campus card +
    `firstErrorCampusIndex` drives expand/scroll; PUBLIC flow mints the OTP via the v1 initiate
    endpoint with an explicit v1 projection (see report §6 gap) and creates via
    `POST /v2/visit-requests/verify` with the REAL nested `{form, otpCode, sessionToken}` contract;
    AUTHENTICATED posts `/v2/visit-requests` directly. Fixed two G-1 client bugs against backend
    source: verify-v2 body was flattened (Form would bind null) and `V2EditPayload` was missing
    registrant/primaryContact/partnerId.
  - UI: `CampusVisitCard` (accordion that only CSS-hides its body — fields stay mounted, nothing
    unregisters; per-card error badge; per-campus Excel import ≤5MB via the existing validated parser;
    one-time copy-from + apply-to-all triggers), `VisitRequestFormV2` (registrant/contact once, cards,
    both destructive confirms as accessible dialogs, reuse of `OtpVerificationModal`),
    `VisitRequestV2Page` on NEW routes `/visit-registration/v2` (public) + `/visit/create-v2`
    (authenticated) — the v1 flow is untouched and flags stay OFF server-side (backend 404 surfaces
    honestly; no silent v1 fallback).
  - i18n: new `visitRequestV2` namespace (vi+en, registered in config) + `minDurationMinutes`/
    `maxCampuses` validation keys.
  - Tests: **Vitest + RTL introduced** (`npm run test:unit`, vitest.config.ts + jsdom + setup);
    33/33 — schema (30-min boundary, duplicate-campus paths, caps, OTHER-type), utils (deep-copy
    independence, apply-to-all purity + overwrite list, payload contracts incl. processing matching &
    partner mode, v1 projection dedupe, draft migration duplication + independence, server-path
    mapping, per-campus Excel apply), draft storage (round-trip stable clientKeys, expiry, namespace,
    migration + never-shadow rule, secret sanitize), hook (add/copy/remove independence, two-step
    apply-to-all, public initiate→verify same submissionId + real v2 payload, authenticated direct
    create, invalid → no API call + first-error campus index).
- **G-3 read/detail/workflow UX** — ✅ DONE:
  - API: `getVisitRequestFormV2` typed to the central dual-read model (`ResolvedVisitForm` —
    viewer relation/canViewAllCampuses/isReadOnly/allowedActions + per-campus decision/host/
    revisions/activeAmendment). The client renders the scoped payload VERBATIM: hidden campuses
    never appear, no role-name authorization on the client, no first-campus projection.
  - `CampusVisitDetailCard` — the ONE read-only per-campus component (status chip, schedule,
    content, collapsible people tables with aria-expanded, operational contact, requirements,
    host/decision/revision block, pending-amendment badge, children slot).
  - `VisitRequestV2DetailView` — request-level data exactly once (+ `Khác nhau theo cơ sở` /
    `Varies by campus` badge only when >1 VISIBLE campuses differ), wires the G-1 panels into a
    real screen: ContactIdentityPanel (registrant/ACTIVE-contact manager only, hidden for
    read-only HO), per-campus VisitAmendmentPanel (decide = STAFF_LEADER, withdraw = manager;
    allowedActions stay UX-only — backend re-authorizes), VisitHistoryTimeline (masked, scoped).
  - Route `/dashboard/visit/v2/:visitRequestId` (`VisitRequestV2DetailPage`).
  - Legacy conflict routing: `formVersionErrors.ts` (`isFormVersionUpgradeRequired` matches the
    stable errorCode only) + EditVisitRequest load AND submit paths now navigate to the v2
    detail instead of showing the raw 409.
  - i18n `visitRequestV2.detail/status.*` (vi+en); statuses degrade gracefully via defaultValue.
  - Tests **46/46** total (`npm run test:unit`; +13 for G-3): card renders own snapshot only,
    accessible people toggle, amendment badge, OTHER-type text; view mixed-vs-same label rules,
    scoped single-card-no-sibling-hint case, identity panel present/absent by viewer, Staff-
    Leader amendment decision vs read-only HO, masked history rendered as-is, 404 (flag OFF)
    friendly message with no fallback fetch; 409 code-not-message matching.
- **G-4A public v2 OTP initiate + snapshot binding** — ✅ DONE (exit gate): `POST /api/v2/visit-requests/initiate`
  validates the FULL v2 form (create-v2 structural validator — 30-min minimum, zero support OK — NOT the v1
  3h/1-support rules), mints the OTP, and binds the canonical snapshot to the submit intent
  (`visit_request_pending_forms`, additive migration `08_up`). Verify-v2 now builds the request FROM THE BOUND
  SNAPSHOT, never the verify-time form: changing campus/member/contact/time/content between initiate and verify
  is a stable conflict (`PER_CAMPUS_V2_SUBMISSION_FORM_MISMATCH` / `..._PENDING_NOT_FOUND`). Frontend public flow
  switched to initiate-v2 (v1 projection removed; no silent fallback). Gates: build 0-err · Unit **482/482** ·
  Arch **14/14** · full IT **372/372** (fresh `pems_it_regression` via junction) · targeted v2 IT **7/7** ·
  FE tsc 0 / unit **46/46** / build ✓. Migration additive+idempotent (pems_pr3_test + pems_it_regression only;
  pems_db/pems_test untouched); flags OFF.
- **G-4B dedicated v2 EDIT/RESUBMIT form page** — ✅ DONE (last Phase-G exit gate): `EditVisitRequestV2Page`
  (routes `/dashboard/visit/v2/:id/edit|resubmit`) reuses the SAME v2 schema + `CampusVisitCard` + utils (no
  third form model). New pure `resolvedFormToV2Schema` hydrates from the scoped read model with stable
  `visitInstanceId` + per-instance/request `rowVersion`; submit → `buildV2EditPayload` → update/resubmit;
  409 → stable conflict + reload; resubmit keeps campus set fixed, pending-edit may add/remove; account-binding
  emails read-only; backend re-authorizes. **Proven backend gap closed**: `ResolvedVisitFormDto.RowVersion`
  added + populated (the edit payload's `ExpectedRequestRowVersion` had no source in the read model). Detail
  view wires Edit/Resubmit by manager + status. Gates: backend build 0 · v2 read IT **23/23** · FE tsc 0 /
  lint 0 / unit **56/56** (10 new) / build ✓.
- **Phase G is DONE** (G-1/2/3 + G-4A + G-4B). Next: Phase H (E2E + SQL drill + rollout docs), then I.
## Phase H — final verification + E2E + rollout readiness — 🟨 IN PROGRESS
- **H-1 SQL migration lifecycle drill** — ✅ DONE (full evidence: `percampus_v2_migration/H1_MIGRATION_DRILL_REPORT.md`).
  Fixed a real **fresh-vs-upgrade drift**: `visit_request_pending_forms` (G-4A `08_up`) was missing from the
  fresh master → added to `pems_full_v10_..._FIXED.sql` + documented in README (patches 06/07/08). Drills on
  disposable DBs only (`pems_h_fresh`/`pems_h_upgrade`/`pems_h_rollback`; pems_db/pems_test never mutated):
  fresh import (all 9 v2 tables incl pending_forms), upgrade from pre-v2 baseline `ed693f6d` → `04_verify`
  **V01–V15 = 0** / presence 1 (762/762 links, 204/204 details), idempotent re-run (identical `204|762|0|0|71`),
  **schema diff fresh-vs-upgrade IDENTICAL** (71=71 tables, columns+indexes byte-identical), constraint
  boundaries (29m59s→3819, 30m OK, end=start→3819, pending dup submission→1062), rollback **refusal guard**
  (has_mixed=1 → aborts, no partial drop) + **clean DOWN** (0 of 9 v2 tables/columns/constraints left),
  EXPLAIN index usage (submission_id→const/uq, expires_at→range/idx, form_details→PK point lookup).
- **H-2 E2E + full regression** — ✅ DONE (matrix: `H2_VERIFICATION_MATRIX.md`). Full regression re-run green:
  Unit **482**, Arch **14**, full IT **372** (fresh `pems_it_regression` from the FIXED master — validates
  the H-1 master fix end-to-end), Vitest **56**, `tsc`/lint 0, `build` ✓. Playwright: added `test:e2e` script
  + a new per-campus v2 browser spec (accordion CSS-hide keeps data; apply-to-all confirm dialog) → full
  browser suite **78 passed** (76 existing, no regression + 2 new). `npm ci` blocked by a Windows native-file
  lock (`lightningcss.node` EPERM) — environment defect, documented; reproducible install =
  `npm install --legacy-peer-deps` (the command that produced the lockfile). Existing Playwright specs are
  **mocked-network component/contract** tests (not full real-stack E2E — §12); the real-stack harness
  (real backend + disposable MySQL + Testing-only OTP sink) is a documented follow-on, not claimed passed.
- **H-3 observability + rollout/canary/rollback docs** — ✅ DONE (`H3_ROLLOUT_OBSERVABILITY.md`).
  Source audit: **no metrics framework** in the project (none introduced — per §20); observability model =
  structured `ILogger` + append-only `audit_logs` (correlation_id + stable codes + masked PII). **Instrumentation
  gap closed**: `ExceptionHandlingMiddleware` now logs `ConflictException` + `BusinessRuleException` by STABLE
  errorCode + path + traceId ONLY (no message/PII) — makes the v2 failure codes observable
  (`PENDING_NOT_FOUND`/`SUBMISSION_FORM_MISMATCH`/`VERSION_CONFLICT`/`READ_REQUIRED`/`FORM_DETAIL_MISSING`/
  `FORM_VERSION_UPGRADE_REQUIRED`), which were previously silent. Regression test
  `ExceptionHandlingObservabilityTests` **2/2** (409/422 + code logged + PII/message NOT logged) → full IT
  now **374/374**. Rollout doc: exact flag names/defaults from source (`PerCampusFormV2` + `PerCampusFormV2Write`,
  both default `false`), ordered rollout, internal canary, log/audit-derived metrics with rollback actions
  (numeric thresholds = documented placeholders pending Product), and flag-OFF (not DOWN) as the production
  rollback.
- **H-4 real-stack E2E** — ✅ DONE. Built the real-stack harness: Testing-only `FileSinkEmailService` (double-
  gated by `ASPNETCORE_ENVIRONMENT=Testing` + `PEMS_E2E_TEST_SINK_ENABLED=true` + a sink path; fail-closed;
  never in prod), a real `.NET` backend published + run on a dedicated port (env overrides — never edits
  appsettings; both v2 flags ON; connection → disposable `pems_e2e_realstack`), Vite pointed at it via
  `VITE_API_BASE_URL`, and `playwright.realstack.config.ts` + `scripts/run-realstack-e2e.mjs`
  (`npm run test:e2e:realstack`, full create→run→teardown). **Journey A (public per-campus v2 create) RAN
  real-stack** (real Chromium → real React → real API → real MySQL; OTP read from the sink) — **1 passed**,
  request persisted (verified in the DB). It **caught a real production bug** (below). Sink guard tests **3/3**.
  Coverage: journeys B–H (auth-gated) still use the `TestAuthHandler` header scheme not yet wired into the
  real host — documented as the remaining H-4 follow-on; they stay covered at the Integration/Vitest layers.
- **H-4 production bug fixed** (caught by the real-stack E2E): a public v2 submit that left the operational
  contact **organization/email blank** hit `Check constraint 'ck_vifd_op_contact_email' is violated` (500).
  Those fields are optional (the validator + frontend allow blank; the field is a display snapshot) but the
  columns were `NOT NULL` with a `TRIM(x) <> ''` CHECK. Fix: columns → NULL (master + `09_up`), entity → `string?`,
  create/edit services normalize blank → NULL (`Clean`), read service coalesces to `""`. Regression
  `CreateVisitRequestV2ServiceTests.Blank_operational_contact_org_and_email_persist_as_null…`. Gates: Unit
  **482**, Arch **14**, full IT **378/378** (374 + op-contact regression + 3 sink guard; first run had 1
  transient flake — hardened `FileSinkEmailService.IsEnabledFor` to also require the sink path, then rerun
  clean 378/0), Vitest **56**, tsc/lint 0, build ✓; H-1 fresh+upgrade schema re-verified (op org/email nullable,
  fresh-vs-upgrade IDENTICAL). **Phase H DONE** (H-1/H-2/H-3/H-4); report stays IN PROGRESS (Phase I pending).
## Frontend V2 Cutover & Workflow Completion (post-H-4) — 🚧 in progress
Phase G shipped the v2 components/routes but the default runtime entry points still opened v1; this workstream
closes those frontend gaps so a normal user reaches v2 when the backend capability is ON, while v1 stays
byte-identical when the flags are OFF.

- **Slice 1 — v2 capability + default entry-point cutover** — ✅ DONE.
  - Backend: new PUBLIC read-only capability endpoint `GET /api/public/features/per-campus-form-v2` →
    `{ readEnabled, writeEnabled, enabled }` with `enabled = readEnabled && writeEnabled`. Anonymous, no
    mutation, exposes ONLY those three flags (no secret/other config). Both flags stay default OFF.
  - Frontend: single shared capability source — `getPerCampusFormV2Capability` API + `PerCampusV2CapabilityProvider`
    /`usePerCampusV2Capability` (session-cached one fetch, loading/error state, **fail-safe to v1** while loading,
    on error, or outside the provider — the client never guesses the flag). Canonical routes + branching centralised
    in `perCampusV2Entry`.
  - Entry-point cutover (capability ENABLED → v2, else v1, no flicker; CTA disabled while resolving):
    HeroSection CTA + FinalCtaSection CTA → `/visit-registration/v2`; VisitRequestManagement "Tạo đoàn khách" →
    `/visit/create-v2`; the dead `/dashboard/visit/create` prototype replaced by `CreateVisitRequestEntry`
    (version-aware redirect). v1 popup path unchanged when OFF. No v1 code deleted.
  - Tests: backend capability `PublicFeaturesCapabilityApiTests` **6/6** (4 flag combos DB-free + anonymous
    default-OFF HTTP shape + ON/ON via real DI). Frontend Vitest **+14** (entry decision, provider enabled/off/
    fail-safe/session-cache/outside-provider, FinalCta cutover on/off/error/loading). Full Vitest **70**, tsc 0, build ✓.

- **Slice 2 — version-aware detail/edit/resubmit routing** — ✅ DONE.
  - Backend: `VisitRequestManagementItemDto` now exposes `formSchemaVersion` (+ `hasMixedCampusDetails`),
    projected at both construction sites of `ViewGuestDelegationListQueryHandler` from the database — so the
    frontend routes on the real version, never guessed from mixed/campus-count.
  - Frontend: `visitVersionRouting` (`isPerCampusV2`, `resolveVisitRowRoutes`) drives VisitRequestManagement's
    detail / edit / resubmit / per-campus-form actions — v2 (mixed OR non-mixed) → `/dashboard/visit/v2/:id`
    (+ `/edit`, `/resubmit`); v1 → the legacy routes/flat modal. A v2 row no longer opens the flat modal or
    waits for a v1 409. Missing version (older cached payload) falls back to v1. The existing code-matched
    `FORM_VERSION_UPGRADE_REQUIRED` handling stays as defense-in-depth.
  - Deferred within Slice 2: making the *other* shared-modal call sites (HoVisitProcessDetail,
    VisitParticipantInvitationDetail) version-aware — those still rely on the backend 409 guard.
  - Tests: backend `V2MixedListSurfacesTests.Management_list_exposes_form_schema_version…` (list DTO carries
    v2 + mixed label). Frontend Vitest **+6** (`visitVersionRouting`). Full Vitest **76**, tsc 0, build ✓.

- **Slice 3 — post-submit per-campus summary** — ✅ DONE (frontend-only; no backend change).
  - After a successful authenticated create or public OTP verify, `VisitRequestV2Page` now renders
    `VisitRequestV2SubmittedSummary`: request-level identity (request code, registrant, primary contact + claim
    state, partner, aggregate status, campus count, mixed/uniform badge) plus ONE card per campus built from the
    IMMUTABLE submitted snapshot (`values`) — schedule/duration/timezone, delegation name, visit type (+other),
    purpose, working content, visitors, support team, operational contact, working language, transportation,
    media consent/note, campus note, and per-campus instance status linked reliably by campus code → campusId →
    `response.instances` (never positional). Never the first campus as representative; editing one campus cannot
    change another's card (immutable snapshot). No new form model — reads the existing schema + create response.
  - i18n: new `visitRequestV2:summary.*` block (VI + EN). Tests: Vitest **+3** (mixed keeps each campus its own
    content; multi-same renders every campus; blank optional operational contact renders without crashing).
    Full Vitest **79**, tsc 0, build ✓.

- **S0 — restore UnitTests compile** — ✅ DONE (`7895be2d`). The Dev expense-stats merge (`34ab5ba4`) added
  `VisitExpenseReports/Items/ReportEvents` to `IApplicationDbContext` but never updated the four EF InMemory test
  doubles (`DelegationsTestDbContext`, `PartnersTestDbContext`, UC-106 `TestApplicationDbContext`, `CampusTestDbContext`),
  so `PEMS.UnitTests` failed to compile. Implemented the three DbSet members in each. No production logic touched.
  **PEMS.UnitTests 510/510.**

- **Slice 4 — safe-edit + amendment UX + allowedActions-driven UI** — ✅ DONE (backend `603abd46` + this frontend commit).
  - Backend (`603abd46`): the v2 read model emitted only `VIEW`, forcing the frontend to infer permissions. Now the
    read service computes real actions mirroring the command-handler authorization (which still re-authorizes):
    `viewer.allowedActions` = EDIT_PENDING_REQUEST / RESUBMIT_REJECTED_REQUEST / SUBMIT_SAFE_EDIT (registrant/ACTIVE
    contact); per-instance `campusVisit.allowedActions` = SUBMIT_AMENDMENT (ASSIGNED/BEFORE_VISIT, ≥24h, no pending) /
    WITHDRAW_AMENDMENT (requester + pending) / APPROVE_AMENDMENT + REJECT_AMENDMENT (current campus Staff Leader +
    pending). HO / out-of-scope campuses get none. `VisitFormActions` constants; optional `IDateTimeService` (no
    call-site churn). Integration tests **+6** (owner/leader/HO scope, one-pending, no cross-campus) → read tests 17/17.
  - Frontend: `VisitRequestV2DetailView` now gates ALL mutation UI on `allowedActions` (typed `visitV2Actions`),
    never relation/status. New `VisitAmendmentSubmitModal` (per-campus proposal, reason required, member lists carried
    through, stable amendment error codes → steady messages) and `VisitSafeEditModal` (registrant/contact + per-instance
    transportation/note/media, immediate apply, 409 → stable message + reload, account email immutable). i18n
    `visitRequestV2:amend.*` / `safeEdit.*` (VI + EN). Vitest **+6** (allowedActions-driven visibility, HO read-only,
    amendment reason-required + AMENDMENT_ALREADY_PENDING mapping, safe-edit 409 reload + applied count).
  - Deferred within Slice 4: inline guest/support LIST editing inside the amendment proposal (scalar/schedule fields
    are editable now; member lists are carried through unchanged).

- **Slice 4.1 — v2 member-list amendments** — ✅ DONE (`32f9ba25`). Audit first: the backend already diffs
  guest/support lists (`VisitAmendmentService.BuildChangeRows`) and, on approve, replaces this instance's
  members copy-on-write (`VisitRequestV2EditOps.StageReplaceMembers`) with sibling isolation — the existing
  `Approve_by_current_campus_leader…` IT already proved the non-shared replace. The gap was purely the
  frontend: the amendment modal carried members through unchanged. Added a guest/support **editor** to
  `VisitAmendmentSubmitModal` (deep-clone, stable client keys, add/edit/remove, active-vs-proposed diff summary,
  at-least-one-visitor guard); the proposal is scoped to the selected instance. New IT
  `Amendment_member_change_is_copy_on_write_and_untouched_until_approved` proves a LEGACY shared member (linked
  to both campuses) survives on the sibling and that active members do not move before approval. Vitest **+4**,
  amendment IT **5/5**.
- **Slice 5A — version-aware shared detail modal** — ✅ DONE (`213a9b3c`). Audit map of the shared flat
  `SubmittedVisitRequestDetailModal` — **6 components / 7 production invocation sites**: `VisitRequestManagement`
  (2 invocations) already routes v2 to the v2 detail route and returns BEFORE opening the modal
  (`resolveVisitRowRoutes`); the 5 read-only components (1 invocation each — `HoVisitProcessDetail`,
  `VisitParticipantInvitationDetail`, `StaffCalendarTab`, `StaffLeaderTaskModal`, `StaffTasksTab`) opened the flat
  modal with NO version check. Central fix: exposed `form_schema_version` on the flat
  `SubmittedVisitRequestFormDetailDto` (backend projection) and branched the shared modal to
  `VisitRequestV2DetailView` whenever the request is v2 — including a UNIFORM v2 request that looks flat. The
  version drives the choice (caller prop → fetched field → v1 upgrade-required 409), never scope/campus-count/mixed
  flag; missing version fails safe to v1. **Zero-unclassified sweep:** all 7 invocations now resolve v1↔v2 through
  the central modal (no v2 request opens the flat v1 UI). Backend projection assertion added to
  `SubmittedVisitRequestFormDetailV2Tests` (flat detail IT **12/12**); frontend branch tests **5/5**.
- **Slice 5B — scope-safe search match contexts** — ✅ DONE (`3b9af03a`). Audit: `ViewGuestDelegationListQueryHandler`
  has two paths — instance-level (Staff Leader/Staff/Dept/Student: one row per authorized instance) and request-level
  (Visitor owner/HO/registrant: full campus visibility of own request). Both already do
  scope→keyword→count→order→pagination in SQL. New `VisitSearchMatchContextBuilder` computes `matchedContexts`
  **in memory AFTER pagination**, over each row's already-authorized campuses only — so a context can never change
  hit/count/order and a hidden sibling campus never appears. Fields mirror each path's keyword predicate exactly
  (instance-level also has campus/host/owner; request-level only delegation/code/reg-org/partner); stable CODES
  (`VisitSearchFieldCodes`), never raw snippets/PII; guest/support names excluded. Request-level path gains
  `+ ThenInclude(FormDetail)` for per-campus delegation (all campuses authorized there). FE `SearchMatchContexts`
  renders "Khớp tại: [Campus | Thông tin chung] — [field]" (VI/EN, unknown-code fallback), wired into the
  management list row. Security ITs (hidden-sibling no-leak + count parity, one row/multi-campus contexts,
  request-level match, guest-name excluded) → `V2MixedListSurfacesTests` **6/6**; FE component tests **5/5**.
- **Slice 5B.1 — matchedContexts consumer audit** — ✅ DONE (no code needed; recorded here). Repo-wide sweep of
  `VisitRequestManagementItem` / `matchedContexts` / `SearchMatchContexts` consumers: **Category A** (visit-request
  search on this DTO → must render) = ONLY `VisitRequestManagement.tsx`, already rendering `SearchMatchContexts`.
  **Category B** (keyword search over OTHER entities — accounts, audit-log, security, sessions, campus, departments,
  emails, FAQ, gallery, news) = N/A. **Category C** (own visit-request search, different DTO) = the "attending"
  invitations tab (`GetVisitInvitations`, FE `getMyInvitations`) — already scope-before-keyword + per-instance mixed
  match + no hidden-campus leak + no guest/support search (Phase F); an independent feature, matchedContexts not
  extended there (out of Slice-5 scope). No searchable surface renders V1/global-projection contexts.
- **Slice 6a — fail-closed E2E test-auth scheme** — ✅ DONE (`dc9ddb90`). New `E2ETestAuthentication.cs` (PEMS.Api):
  `E2ETestAuthGate` (quadruple gate: env=Testing + `PEMS_E2E_TEST_AUTH_ENABLED=true` + non-blank
  `PEMS_E2E_TEST_AUTH_SECRET` + `PEMS_E2E_TEST_AUTH_PROFILES` file; constant-time `SecretMatches`),
  `E2ETestProfileStore` (loads seeded profiles from the file, fail-closed to empty on missing/parse-error),
  `E2ETestAuthHandler` (browser sends only an opaque profile KEY + run secret; identity resolved SERVER-SIDE, never
  from a role/campus header; re-checks the gate per request). `AuthenticationExtensions.AddJwtAuthentication(cfg, env)`
  registers it + makes it the default scheme ONLY when the gate is open — Dev/Prod never register it (WAF's own Test
  scheme is unaffected). Distinct from the header-trusting `TestAuthHandler` (never promoted). Guard tests
  `E2ETestAuthGuardTests` **4/4**: four-part gate, constant-time compare, profile resolution (unknown/missing fail
  closed, leader-HN never resolves to HCM), handler behaviour (valid profile+secret → server-side claims, ignores
  spoof headers; wrong/missing secret + unknown profile fail; no header = anonymous; nothing authenticates outside
  Testing).
- **Slice 6b — authenticated real-stack foundation** — 🟨 PARTIAL/DONE-for-scope (`edd1a8b3`). Wired the Slice-6a
  fail-closed scheme into the H-4 harness and drove it through a REAL browser (real Chromium → real Vite → real
  published .NET API, Testing + both v2 flags ON + fail-closed E2E auth → disposable MySQL, no network mock).
  Orchestration now: mints a run-scoped secret, resolves the disposable DB's ACTUAL seeded identities into a
  server-side profile file (opaque key → identity, no secret), seeds an ACTIVE `user_sessions` row per profile, and
  passes the four auth gates as process env; the specs inject only the profile key + secret on backend requests
  (trace OFF so the secret is never persisted). `E2ETestProfile`/`E2ETestAuthHandler` gained a `SessionId` claim so
  the REAL `SessionValidationMiddleware` accepts the E2E actor exactly like a logged-in user (NO middleware bypass).
  Also fixed 4 pre-existing harness bugs that made the real-stack suite un-runnable here: stale v10→V11 master path,
  `new URL().pathname` vs `fileURLToPath` on a spaced repo path, publish bin-lock beside a running dev server (temp
  `BaseOutputPath`), unquoted shell args splitting a spaced path. **Journeys run real-stack 3/3 green:** A public v2
  create with a real OTP from the sink (re-verified); B an authenticated HO reaches the protected visit dashboard
  (the browser's own `/auth/me` is E2E-authenticated 200, `ProtectedRoute` does not bounce); C the running host
  enforces the fail-closed gate (no/wrong secret + unknown profile → 401) and resolves identity server-side
  (`ho_viewer`→HO, `campus_leader_hn`→STAFF, never HCM). **Remaining B–H workflow journeys** (authenticated create,
  detail uniform/mixed, pending-edit, resubmit, safe-edit, member-amendment submit, leader approve/reject,
  wrong-campus denial, withdraw, search no-leak, identity) build on this now-working foundation — NOT yet authored.
- **Slice 6b — authenticated real-stack Journeys D–H** — ✅ DONE (`09cdfa58` journeys + `4893c98d` fix). Added the
  workflow journeys on the working harness (preconditions via the REAL authenticated API; action under test via the
  real UI for D, asserted at the real host otherwise): **D** an authenticated owner opens the per-campus v2 detail and
  sees BOTH mixed-campus cards with their own content; **E** pending-edit changes only the target campus, the sibling
  is a true no-op (versions unchanged); **F** a member amendment keeps the active snapshot until approval, then the
  current campus leader's approve applies it target-only (sibling untouched); **G** a wrong-campus leader is refused
  the amendment-approve endpoint (403) while the correct leader passes the campus gate; **H** search is scope-safe end
  to end (a keyword only on a hidden sibling campus never surfaces the request for a campus-scoped actor; contexts stay
  on authorized campuses). **Real-stack A–H 8/8 green** (`npm run test:e2e:realstack`).
- **PRODUCTION BUG caught by Journey F + fixed** (`4893c98d`): the v2 read model returned the FORM-DETAIL row_version
  as the per-campus `rowVersion`, but pending-edit/safe-edit/amendment all check the CAMPUS INSTANCE row_version
  (`visit_request_campuses.row_version`). A campus-approve bumps the instance token without touching the form detail's,
  so a safe-edit/amendment on a freshly-loaded ASSIGNED detail 409'd with a spurious concurrency conflict. Fixed to
  emit the instance token; added an integration regression (`PerCampusFormV2ReadTests` → 18). No prior test caught it
  because they read the instance version straight from the DB — only the real-stack read-model→submit path exposed it.
- **Slice 6 — authenticated real-stack A–H — ✅ COMPLETE.**
- **Session gates (real, after Slice 6b/D–H, HEAD `09cdfa58`)** — `PEMS.UnitTests` **510/510** · Architecture **14/14** ·
  full `PEMS.IntegrationTests` **400/400** on freshly-built disposable `pems_it_regression` (V11 master, 76 tables;
  appsettings trap-restored byte-exact to pems_test) — the read-model fix broke no test · `PerCampusFormV2ReadTests`
  **18/18** · E2E auth guard IT **4/4** · **real-stack Journeys A–H 8/8** · Vitest **99** · tsc 0 · build ✓. Disposables
  (`pems_it_regression` + the orchestration's `pems_e2e_realstack`) dropped; `pems_pr3_test` 0 v2/leaked rows;
  `pems_db`/`pems_test` never connected to; the run secret + profile file + OTP inbox never persisted (temp workDir
  removed, secret only in process env, no secret in any log, Playwright trace OFF). Feature flags stay default OFF. No
  manual push/merge/PR.
- **Resume point** — **Phase I** (guarded contract-drop prep, disposable DBs only): readiness audit of the 10 legacy
  global fields + read-only preflight/UP/verify/DOWN candidate scripts, drilled on `pems_i_fresh`/`pems_i_upgrade`/
  `pems_i_refusal`/`pems_i_rollback` only. Expected honest conclusion: "guarded contract-drop prepared/tested on
  disposable databases; execution NOT READY while V1 fallback + legacy runtime reads remain; no real database modified."

## Slice 6c — Full Browser UI E2E promotion — ✅ COMPLETE
Promoted the v2 mutation + search journeys from real-host **API** level to full browser **DOM** automation
(`tests-realstack/authenticated-ui-workflows.realstack.spec.ts` + `realstackHelpers.ts`): pending-edit,
resubmit, safe-edit, amendment-submit, leader approve/reject, wrong-campus visibility + backend denial,
withdraw, search isolation — each navigates a real route and clicks a real button/modal against the real
running stack (no `page.request`/`page.evaluate(fetch)`/`route.fulfill`/DB-fake). Logic-neutral `data-testid`s
added to the detail view, campus cards, amendment panel/modal, safe-edit modal, edit page, list search box.
**Real-stack now 17/17** (8 kept API-level A–H + 9 new DOM). tsc 0 · Vitest 99 · build ✓ · Arch 14/14 ·
auth-guard 4/4 · targeted V2 IT 44/45 · Unit 528/530. See H2_VERIFICATION_MATRIX.md for the per-journey table.

**Dev auto-merge (`64c83a59`) overlaps found — none caused by this session** (frontend + UnitTests-harness
dedup only): (1) fixed the merge's duplicated `VisitExpense*` DbSet decls in 4 unit-test harnesses (project
would not compile); (2) 2 pre-existing photo-upload unit failures from the merge's `FileValidationPolicy.cs`
change — out of scope, left for the photo owner; (3) the merge added guest-name search to the list keyword
filter, which conflicts with the Slice 5B security test `Guest_member_names_are_not_searched_and_produce_no_row`
— flagged for human reconciliation (matchedContexts stay PII-free either way), neither the test nor the Dev
feature changed unilaterally.

**Full IT (~400) not re-run this session:** `pems_test` absent + `pems_pr3_test` stale/protected (see recipe
caveats); no backend production/IT code changed this session; v2 backend covered by real-stack 17/17 + V2 IT 44/45.

## Phase I — contract cleanup prep (guarded, never run on real DB) — ✅ COMPLETE (Blocked)
- **Zero-Unclassified Audit**: Completed. 10 legacy fields audited across codebase. Remaining V1 reads and Compatibility Projection Writes identified (documented in `PHASE_I_AUDIT_REPORT.md`).
- **Guarded Scripts**: Prepared 5 candidate scripts (`01_preflight.sql`, `02_guarded_up.sql`, `03_verify.sql`, `04_down_restore.sql`, `README.md`) with explicit `pems_i_fresh/upgrade/refusal/rollback` database guards.
- **Drill Execution**: Disposable MySQL environment drills could not be executed locally due to missing MySQL binaries on the execution environment. Full commands and missing binaries are documented in `PHASE_I_AUDIT_REPORT.md`.
- **Regression Gates**: Unit 530/530 · Architecture 14/14 · Frontend tsc 0 · build ✓.
- **Conclusion**: Phase I guarded contract-drop candidate prepared and tested structurally; execution remains **NOT READY** while V1 fallback, legacy runtime readers/writers, persisted V1 data and default-OFF flags remain. No real database was modified.

## Verified test gates (updated each group)
- Unit 435/435 · Architecture 14/14 · IntegrationTests 306/306 (Phase A).

## Regression recipe (unchanged, clean each run)
Recreate disposable `pems_it_regression` from PR-2 master (`pems_full_v10_..._FIXED.sql`, sed db name,
byte-safe `mysql --default-character-set=utf8mb4 <`); junction trick (`scratchpad/reporoot/{backend,tests}`
→ real, `BaseOutputPath=scratchpad/reporoot/it_full_out/`); swap appsettings.Testing.json pems_test→
pems_it_regression under a bash trap that restores with **explicit sed-back to pems_test** (the file is
gitignored — `git diff` can't verify; grep the value). Never touch pems_db/pems_test.
