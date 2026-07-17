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
| B-2a | create-v2 aggregate SERVICE + write flag + v2 fingerprint + v2 DTOs (one-txn: request fsv=2 + backend scope/mixed/fingerprint/smallest-campus projection + N instances routed to Staff Leader + N form details + per-campus independent members + links + baseline revisions + VISIT_REQUEST_CREATED_V2 audit + INITIAL_CLAIM 72h) | reorg `a5cb3977` (was `0f67eff8`) | 11/11 service tests |
| B-2b | create-v2 command + `POST /api/v2/visit-requests` + two-flag gate + submissionId idempotency (transaction-owned, concurrent-race safe) | reorg `a5cb3977` (was `4dd1c1d4`) | 3/3 command tests · Unit 474/474 · Arch 14/14 · IT **320/320** |
| B-2.5 | create-v2 close-out: structural validator (MediatR pipeline, service still revalidates) + shared `V2CreateNotifier` post-commit Staff-Leader/HO notifications (first-create-only, best-effort) + public OTP create-v2 (`VerifyAndCreateVisitRequestV2` + `POST /api/v2/visit-requests/verify`, reuses the v1 OTP primitive, provisions registrant only) | _(this commit)_ | 17/17 targeted (11 service + 3 command + 3 public verify) |

> Note: after the `Dev` merge the Phase-A/B commits were reorganized into clean functional commits —
> B-1 = `1d056fd7`, B-2 = `a5cb3977` (the pre-merge hashes above are the original per-handler commits).

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

### Phase B-2 — create-v2 — ✅ DONE (reorg `a5cb3977`) + B-2.5 close-out ✅ DONE
Service + command + `POST /api/v2/visit-requests`, both flags default OFF, IT 320/320. **B-2.5 closed all three
deferred follow-ons**: (1) `CreateVisitRequestV2CommandValidator` structural FluentValidation in the MediatR
pipeline (service still revalidates every DB/clock rule); (2) shared `V2CreateNotifier` post-commit Staff-Leader
+ HO notifications — after-commit, first-create-only, best-effort (no outbox ⇒ not exactly-once), rollback never
notifies; (3) public OTP create-v2 (`VerifyAndCreateVisitRequestV2Command` + handler + `POST /api/v2/visit-requests/verify`
`[AllowAnonymous]`) reusing the v1 OTP primitive verbatim, provisioning ONLY the registrant (contact B stays
PENDING/INITIAL_CLAIM). 17/17 targeted green. INITIAL_CLAIM invite **email** to contact B is Phase D (identity
workflow). Original spec kept below for reference.

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

### Phase C — edit pending + resubmit v2 (plan §6.4) — 🚧 IN PROGRESS
**C-1 pending edit — ✅ DONE (this session):** `VisitRequestV2Canonical` shared recompute + edit DTOs (stable
visitInstanceId + expected row versions) + `VisitRequestV2EditService.ApplyPendingEditAsync` (stable-409
optimistic concurrency on request AND instance; immutable registrant/partner/account-binding emails;
change-detection so untouched siblings are true no-ops; add campus with availability recheck + routing +
baseline revision; remove campus only WAITING with no downstream data + orphan-member cleanup; copy-on-write
member full-replace; recompute scope/mixed/fingerprint/projection; SAFE_EDIT revisions + correlated
field-level audit) + handler/`PUT /api/v2/visit-requests/{id}/pending-edit` (both-flag gate; editors =
registrant or ACTIVE primary contact; v1 requests → `VISIT_REQUEST_NOT_PER_CAMPUS_V2`; 24h window;
post-commit best-effort leader notifications) + structural validator sharing the create-v2 campus rules.
Service 14/14 + command 1/1 targeted green.
**C-2 resubmit all-REJECTED — ✅ DONE (this session):** `ApplyResubmitAsync` + `POST /api/v2/visit-requests/{id}/resubmit`.
Campus set fixed + instance IDs KEPT (drop/add/swap → `RESUBMIT_CAMPUS_LIST_CHANGED`); `SELECT … FOR UPDATE`
row-version guard on BOTH edit flows (one winner under concurrency, stable 409 for the loser); decisions
snapshotted to audit_log_changes before clearing (history never deleted); three-phase flush honouring the
campus trigger (parent → PENDING first); availability recheck + re-route to CURRENT leaders;
resubmission_count++; RESUBMIT revisions; post-commit re-process notifications. Service 6/6 + command 1/1;
v2 test group 39/39.

### Phase D — identity INITIAL_CLAIM + cancel-3A + expiry/redaction job (plan §16.4/16.7/16.8, §4.4) — ✅ DONE (this session)
**SQL**: `email_action_tokens` ENUMs extended (+`VISIT_CONTACT_CLAIM` context, +`VISIT_REQUEST_IDENTITY_CHANGE`
target) — master updated + idempotent `06_up_identity_claim_tokens.sql`; applied to pems_pr3_test only.
**D-1 claim workflow**: `VisitContactClaimService` mints the single-use invitation token (hash-only, expiry =
claim expiry) + sends the FE claim-page link post-commit from BOTH create paths (first-create-only, best-effort,
recoverable via resend); the generic anonymous email-action handlers explicitly REJECT the claim context (link
possession alone never applies a claim). Anonymous masked landing `GET /api/public/visit-contact-claims/{token}`;
`POST /api/v2/visit-contact-claims/{token}/accept|decline` (`[Authorize]`, actor's DB email must equal
`new_email_normalized`, VISITOR + ACTIVE only). Accept = one txn on the FOR-UPDATE-locked claim: link
visitor_user_id + ACTIVE + verified_at + row-version bump, PENDING→APPLIED, token burned, sibling tokens
invalidated, `PRIMARY_CONTACT_CLAIM_APPLIED` event + masked audit; campus decisions untouched. Decline =
DECLINED + 90d retention stamp; the request stays alive and unowned.
**D-2 registrant management + cancel-3A**: resend (supersede-all-tokens-first, 72h restart, cap 5) + replace
pending contact ("typo fix": supersede claim, rewrite snapshot, same-email→instant registrant link,
different→fresh claim+invite; internal emails rejected); both refuse once the contact is ACTIVE (TRANSFER
territory). Cancel-3A: the REGISTRANT may cancel a PENDING request while access = PENDING_CONFIRMATION under
the same 24h/reason rules, audited `VISIT_REQUEST_CANCELLED_BY_REGISTRANT_PENDING_CONTACT`, pending claims
CANCELLED + tokens burned in-txn; exception disappears once ACTIVE.
**D-3 job**: `VisitContactClaimMaintenanceService.RunOnceAsync` — idempotent FOR-UPDATE-batched EXPIRE
(PENDING past its per-row deadline → EXPIRED + retention + event + tokens dead; request NOT cancelled) and
REDACT (terminal past 90d → full email/snapshot/reason nulled + token recipient masked; masked
email/kind/status/actors/timestamps kept; `IDENTITY_CHANGE_REDACTED` event+audit); hosted
`VisitContactClaimMaintenanceHostedService` (600s/200, flag-independent).
**D-4 primary-contact TRANSFER 24h (handoff §6)**: the registrant or the CURRENT ACTIVE contact proposes
handing the role to a new email (`POST /api/v2/visit-requests/{id}/contact-transfer` + GET state/resend/
cancel); the old owner keeps every right until the invited person — logged in with the exactly-matching
Google account — explicitly accepts (`/api/v2/visit-contact-transfers/{token}/accept`); the swap of
`visitor_user_id` + the contact snapshot happens in the same transaction as PENDING→APPLIED on the
FOR-UPDATE-locked change row; access stays ACTIVE throughout; campus decisions/status/host/schedule are never
touched and the old ACCOUNT is never locked/deleted. 24h expiry (resend restarts 24h + re-stamps
`expected_request_row_version` so legit edits between invitations never brick the accept), one PENDING change
per request via the DB guard (`IDENTITY_CHANGE_ALREADY_PENDING`), masked anonymous landing
(`/api/public/visit-contact-transfers/{token}`, enumeration-safe), invited-side decline + owner-side cancel
keep the old owner, kind-aware expiry event `PRIMARY_CONTACT_TRANSFER_EXPIRED`, and a pending transfer does
NOT open cancel-3A (contact is still ACTIVE). New SQL: `07_up_transfer_tokens.sql` (+master) appends the
`VISIT_CONTACT_TRANSFER` token context (additive+idempotent; applied to pems_pr3_test only).
**Deferred (documented)**: OTP_FALLBACK confirmation (non-Google not enabled by Product) — TRANSFER does not
depend on it (Google SSO exact-email, same as the claim).
Tests: `VisitContactClaimWorkflowTests` **7/7** + `VisitContactTransferWorkflowTests` **6/6**.

### Phase E — safe edit + post-approval amendment (plan §16.6) — ✅ DONE (this session)
`VisitFieldClassifier` = THE single backend classification table (SAFE / PRIVACY_URGENT [media→DECLINED only] /
APPROVAL_SENSITIVE / STRUCTURAL; unknown paths fail closed — the primary-contact email is deliberately
unclassified: identity workflow only). Safe edit `PATCH /api/v2/visit-requests/{id}/safe-details`
(`VisitSafeEditService`): server-side diff of the safe subset, FOR-UPDATE + request/instance row-version 409s,
24h cutoff with the media-WITHDRAWAL exemption (applies even <24h, URGENT notification incl. Host), target-only
apply + form_revision bump + SAFE_EDIT revision snapshots + canonical (mixed/fingerprint/projection) recompute +
field-level audit `VISIT_SAFE_FIELDS_UPDATED`. Amendments (`VisitAmendmentService`): submit stores an IMMUTABLE
per-field proposal for ONE decided (ASSIGNED/BEFORE_VISIT) instance ≥24h before start — one PENDING per
instance (DB guard), base form/approval-revision conflicts are stable 409s, NOTHING active mutates; approve is
restricted to the CURRENT campus Staff Leader and applies target-only on the locked amendment (scalars +
schedule + member copy-on-write) with form+approval revision bumps, post-apply revision snapshot, canonical
recompute and audits (`VISIT_AMENDMENT_APPROVED` + `VISIT_INSTANCE_FORM_REVISION_APPLIED`) — sibling campuses
and approval statuses never reset; reject (reason required)/withdraw/expire leave the active snapshot;
`VisitAmendmentExpiryHostedService` sweeps overdue/started pending amendments idempotently. Scoped
metadata-only history timeline at `GET /api/v2/visit-requests/{id}/history` (masked identity events for
managers/HO only; leaders/hosts see only their scope; proposals never presented as active).
Tests: `VisitSafeEditV2Tests` 4/4 + `VisitAmendmentV2Tests` 4/4; v2 group **60/60**.

### Phase F — list/search/dashboard/calendar/report/export/email (audit map §5/§6, plan §16.9) — ✅ DONE (this session)
~35 Class-C surfaces migrated with one uniform rule (audit map §10 lists them all): instance rows read
`mixed-v2 ? instance.FormDetail.<field> : vr.<field>` (single FormDetail-nav JOIN, no N+1, NO global
fallback for mixed), request rows that cannot show per-campus content are labeled "Khác nhau theo cơ sở",
v1/non-mixed keep the byte-identical projection. Covers dashboards (HO/DeptLeader), calendars (staff/dept),
invitation + assignment lists, guest delegation list (staff + visitor paths), feedback surfaces (search/
summary/targets/pending/submit snapshots), minutes search + PDF/Excel exports, eligible-news, all report
overviews + invoice/V2 item queries + send-email commands (visit_type filters mixed-aware; the Staff-Leader
report resolves mixed via THEIR campus's detail), conflict/busy labels, related-visitor + document detail,
and instance-scoped notification/email texts. Keyword search is scope-before-keyword and matches per-
instance details for mixed v2 — a hidden sibling campus's content can no longer produce a hit for an
instance-scoped actor. Repository-wide 10-field sweep → **ZERO unclassified production references**
(classification table in audit map §10). Tests: `V2MixedListSurfacesTests` 2/2 (helper matrix + Staff
calendar end-to-end per-campus names).

### Phase G — frontend (plan §7/§8/§9) — 🚧 IN PROGRESS (G-1 + G-2 done)
**G-1 ✅** typed v2 API client + invitation landing (`/visit-contact-claim|transfer/:token`, masked, explicit
accept, wrong-account hint) + `ContactIdentityPanel` + `VisitAmendmentPanel` + `VisitHistoryTimeline`; routes
wired and the two internal planning docs untracked in the forward-fix commit `0cec2972` (the G-1 commit had
already been pushed, so history was NOT rewritten).
**G-2 ✅** per-campus form v2: `visitRequestV2.schema.ts` (campusVisits[] full snapshots, stable clientKeys,
30-MIN minimum in ms math — 29m59s fails/30m passes, 10/200 caps, per-index duplicate-campus), pure utils
(deep `cloneCampusVisitContent` preserving target identity/schedule, confirmed apply-to-all + overwrite list,
`buildV2CreatePayload`/`buildV2EditPayload` matching the REAL backend DTOs, `migrateV1DraftToV2`,
FluentValidation-path→RHF-path mapper, per-campus Excel apply), draft storage v3 (own key; global draft
migrated in-memory once, never shadows a newer v3), `useVisitRequestFormV2` (public v1-initiate→OTP→
`POST /v2/visit-requests/verify` with the correct NESTED `{form,otpCode,sessionToken}` body; authenticated
direct create; server errors land on the exact campus card), `CampusVisitCard` (CSS-hide accordion — never
unregisters; error badge; ≤5MB per-campus Excel), `VisitRequestFormV2` + `VisitRequestV2Page` on NEW routes
`/visit-registration/v2` + `/visit/create-v2` (v1 flow untouched; flags OFF ⇒ backend 404 surfaced honestly,
no silent v1 fallback), i18n namespace `visitRequestV2` (vi+en). **Vitest+RTL introduced**: `npm run
test:unit` → 33/33. Fixed two G-1 client/contract bugs (flattened verify body; V2EditPayload missing
registrant/primaryContact/partnerId).
**G-3 ✅** per-campus read/detail/workflow UX: `getVisitRequestFormV2` client on the central dual-read model;
`CampusVisitDetailCard` (ONE read-only component reused everywhere — status/schedule/content/people tables
with aria-expanded, operational contact, host/decision/revision, amendment badge); `VisitRequestV2DetailView`
(request-level once + "Khác nhau theo cơ sở"/"Varies by campus" only when >1 VISIBLE campuses, G-1 panels
wired: identity for the manager, amendment decide for STAFF_LEADER / withdraw for the manager, masked
history; the scoped payload is rendered verbatim — hidden campuses never appear and read-only HO gets no
action buttons); route `/dashboard/visit/v2/:id`; legacy `FORM_VERSION_UPGRADE_REQUIRED` (code-matched, never
message text) now routes EditVisitRequest load+submit to the v2 screen instead of a raw 409; i18n
detail/status keys vi+en. Gates: `npm run lint` 0, `npm run test:unit` **46/46**, `npm run build` green.
**Deferred within G (documented, not flag-OFF-blocking)**: a dedicated v2 EDIT/RESUBMIT form page (its API
client + payload builder + tests shipped in G-2; the create form component is reusable) and the public OTP
initiate v2 endpoint (backend gap below).

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

## 5. Test counts (latest, verified — end of Phase F backend, G-2 frontend)
- UnitTests **474/474** (the historical "435" baseline was a stale incremental build; 0 failures throughout).
- ArchitectureTests **14/14**.
- IntegrationTests **368/368** on a fresh disposable `pems_it_regression` recreated from the PR-2 master
  (352 through Phase D-4, +8 Phase E safe-edit/amendment, +2 Phase F mixed-surface, plus the v2 write group:
  create service 11 + create command 3 + public OTP verify 3 + pending-edit service 14 + pending-edit
  command 1 + resubmit service 6 + resubmit command 1 + contact-claim workflow 7 + transfer workflow 6).
  appsettings.Testing.json restored to `pems_test` (grep-verified); pems_db/pems_pr3_test/pems_it_regression
  v2_requests = 0, identity_changes = 0, claim tokens = 0; no live appsettings carries a PerCampusFormV2
  section (both flags default OFF).
- Frontend (end of G-3): `npm run test:unit` (Vitest+RTL, first suite in the repo) **46/46**;
  `npm run lint` (tsc) 0 errors; `vite build` green (pre-existing chunk-size warning only).

## 6. Known limitations / notes
- A **Dev merge** (`ae060dcf`) landed mid-session; the branch was later reorganized into clean functional
  commits (B-1 `1d056fd7`, B-2 `a5cb3977`). The merged tree is green, so Phase-A/B behavior is intact.
- Phases B, C, D (incl. **D-4 TRANSFER 24h**), E (safe-edit + amendments + history), F (Class-C surface
  migration + zero-unclassified report) and G (frontend G-1/G-2/G-3, with the two documented in-phase
  deferrals: v2 edit-form page + v2 public initiate endpoint) are **done**; next: H (E2E/rollout
  verification), then I (guarded contract-drop prep, disposable DB only).
- **Public v2 OTP initiate gap (backend, needed before flag-ON)**: there is no `/api/v2/visit-requests/initiate`.
  The public v2 form mints its OTP through the v1 `POST /visit-requests/initiate`, whose validator
  (`ApplyVisitRequestFormRules`) enforces the v1 shape — ≥3h slot duration and ≥1 support member — while v2
  legitimately allows 30-minute visits and 0 support members. The frontend sends an explicit v1 PROJECTION
  for initiate only (the CREATE always posts the full v2 contract to `/v2/visit-requests/verify`), so
  requests satisfying the v1 constraints work end-to-end, but a <3h or no-support public v2 submit is
  rejected at the OTP step. Both flags are OFF so nothing user-facing is affected today; a v2-shape-aware
  initiate endpoint should ship with the rollout (Phase H checklist item). Authenticated create-v2 has no
  such constraint.
- The G-1 commit `f9aa43f0` had been pushed to origin before its scope could be amended, so the correction
  landed as the FORWARD commit `0cec2972` (App.tsx invitation routes + untracking the two internal planning
  documents; the files remain in the worktree, local-only). History was not rewritten.
- Phase D scope note: only the OTP_FALLBACK confirmation method remains deliberately deferred (Product has
  not enabled non-Google confirmation; TRANSFER does not depend on it). The `06_up_identity_claim_tokens.sql`
  and `07_up_transfer_tokens.sql` additive ENUM patches were applied to **pems_pr3_test** (the dedicated
  direct-handler test DB) so claim/transfer tokens can be minted there; pems_db/pems_test remain untouched.
- v2 create/edit notifications are post-commit **best-effort** (no outbox in the project): a rollback never
  notifies and an idempotent replay never re-notifies, but a crash between commit and dispatch can drop a
  notification — documented, not exactly-once.
- No production seed/code was changed to make tests pass; the only test-infra change was adding the new
  `IVisitFormReadService` constructor arg (a bare mock) to one unit test.
