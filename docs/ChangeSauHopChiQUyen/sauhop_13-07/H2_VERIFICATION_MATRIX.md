# Phase H-2 — Verification & Regression Matrix

Actual results from this session (no hardcoded baselines). Commands and layer allocation follow the
Phase-H matrix rule (§15): each requirement is covered at the CHEAPEST correct layer, not duplicated into
Playwright.

## Test counts (actual, this run)

| Suite | Command | Result |
|-------|---------|--------|
| Backend Unit | `dotnet test PEMS.UnitTests` | **482 passed / 0 failed / 0 skipped** |
| Backend Architecture | `dotnet test PEMS.ArchitectureTests` | **14 passed / 0 / 0** |
| Backend Integration (full) | `dotnet test PEMS.IntegrationTests` on a fresh `pems_it_regression` built from the **fixed master** (validates the H-1 master fix end-to-end), run via the repo-root junction | **372 passed / 0 / 0** |
| Frontend unit/component | `npm run test:unit` (Vitest + RTL) | **56 passed / 0 / 0** |
| Frontend browser (Playwright) | `npm run test:e2e` (Chromium, mocked network) | **78 passed / 0 / 0** (76 existing + 2 new per-campus v2) |
| Frontend TypeScript/lint | `npm run lint` | 0 errors |
| Frontend build | `npm run build` | green (pre-existing chunk-size warning only) |

`npm ci` note: `npm ci` fails in this Windows environment with `EPERM unlink` on the native
`lightningcss.win32-x64-msvc.node` (an OS file-lock on a running native module — an environment defect, not
a lockfile/dependency defect). The reproducible install used is `npm install --legacy-peer-deps` — the same
command that produced the committed `package-lock.json` (React 19 forces `--legacy-peer-deps` for
`@types/react` / `@testing-library/dom` peers). The lockfile itself is consistent.

## Coverage matrix (requirement → owning layer)

| Requirement | SQL | Unit | Integration | Component (Vitest) | Playwright |
|-------------|-----|------|-------------|--------------------|------------|
| 30-min / end=start / duplicate-submission constraints | ✅ H-1 | ✅ validator | ✅ create svc | ✅ schema | — |
| Backfill single/multi-same/multi-mixed/zero-member/copy-on-write | ✅ H-1 04_verify | — | ✅ create+edit svc | — | — |
| Fresh-vs-upgrade schema parity + idempotency + rollback | ✅ H-1 | — | — | — | — |
| Public v2 OTP initiate → snapshot binding, tamper rejected | — | ✅ validator | ✅ PublicInitiate 4 | — | — |
| Authenticated / public v2 create payload contract | — | — | ✅ create+verify | ✅ hook | — |
| Pending-edit / resubmit rowVersion + 409 concurrency | — | — | ✅ edit+resubmit svc | ✅ EditPage | — |
| Identity claim / transfer / cancel-3A / expiry | — | — | ✅ claim+transfer workflow | ✅ panels | — |
| Safe-edit / amendment / approve-reject-withdraw / expiry | — | — | ✅ safe-edit+amendment | ✅ AmendmentPanel | — |
| Role/authorization scope + hidden-campus search isolation | — | — | ✅ IT (scoped read/search) | ✅ DetailView scoped | — |
| Mixed label "Khác nhau theo cơ sở" / no first-campus projection | — | — | ✅ V2MixedListSurfaces | ✅ DetailView | — |
| Per-campus form field-array: copy / apply-all / deep-copy | — | — | — | ✅ utils+hook | (dialog only) |
| Accordion CSS-hide keeps data (browser-only) | — | — | — | — | ✅ percampus-v2 |
| Apply-to-all confirm dialog over real layout (browser-only) | — | — | — | ✅ hook | ✅ percampus-v2 |
| Legacy 409 `FORM_VERSION_UPGRADE_REQUIRED` → v2 routing | — | — | — | ✅ formVersionErrors | — |
| i18n VI/EN, runtime re-translation, no raw keys, no mojibake | — | — | — | — | ✅ i18n specs |

## Downstream regression

The **full IntegrationTests (372)** exercise the downstream modules end to end against the master-built DB:
invitation, department assignment, calendar, reminder/logistics, agenda, minutes, feedback, partner links,
member resolution, invoice, reports, export, email, notification/audit — all green. No Gallery change was
made (no regression found). Frontend `tsc` + `build` + the 78 Playwright browser tests confirm no UI
regression from the Phase-G / G-4 / H-1 changes.

## Full real-stack E2E — H-4 UPDATE (harness built; journey A running real-stack)

**H-4 delivered the real-stack harness and ran journey A end to end** (see `IMPLEMENTATION_PROGRESS.md`):
real Chromium → real React (Vite) → real .NET API (Testing, both v2 flags ON) → disposable
`pems_e2e_realstack` MySQL, with the OTP read from a Testing-only file sink (never a public endpoint).
`npm run test:e2e:realstack` (`scripts/run-realstack-e2e.mjs`) creates the DB from the fixed master,
publishes + starts the backend with env overrides, points Vite at it, runs
`tests-realstack/public-create-v2.realstack.spec.ts`, and tears everything down. This journey **caught a real
production bug** (blank operational-contact org/email → 500), which was fixed with a regression test.

**UPDATE (Slice 6a/6b):** the auth-gated journeys are no longer blocked. A NEW fail-closed E2E auth scheme
(`backend/PEMS.Api/Authentication/E2ETestAuthentication.cs`, `dc9ddb90`) — quadruple-gated (Testing + explicit flag +
run secret + server-side profile file), constant-time secret, identity resolved server-side from seeded profiles,
NOT the header-trusting `TestAuthHandler` — is wired into the real host by the orchestration (`edd1a8b3`), which mints
a run secret, writes the profile file from the disposable DB's seeded IDs, and seeds an active session per profile
(so the real `SessionValidationMiddleware` accepts the actor). Real-stack journeys now run **A–H 8/8 green** (`npm run test:e2e:realstack`): A public v2 create (real OTP); B an
authenticated HO reaching the protected visit dashboard; C the fail-closed gate + server-side identity resolution at
the running host; D an authenticated owner opening the per-campus v2 detail (both mixed-campus cards, own content) via
the real UI; E pending-edit target-only + sibling no-op; F a member-amendment lifecycle (submit keeps the active
snapshot → current campus leader's approve applies it target-only, sibling untouched); G a wrong-campus leader refused
the amendment-approve endpoint (403); H search scope-safe end to end (a hidden-campus keyword never leaks; contexts stay
authorized). **Journey F caught a real production defect** — the v2 read model surfaced the form-detail row_version
instead of the campus-instance row_version, so a safe-edit/amendment on a freshly-loaded ASSIGNED detail 409'd; fixed in
`4893c98d` with an integration regression. This is exactly the cross-boundary value of full real-stack E2E (real
read-model → real submit) that the layer tests could not reach.

## Original infrastructure notes (pre-H-4)

The existing Playwright suite (and the 2 new per-campus specs) are **browser component/contract tests with
mocked network** — the `webServer` starts only Vite (frontend); the backend and OTP are mocked via
`page.route`. Per §12 these do **not** count as full real-stack E2E.

A TRUE real-stack E2E (real Vite frontend → real .NET backend with both v2 flags ON → disposable MySQL →
test-only OTP sink) is **not stood up in this session**. What it would require, and why it is a scoped
follow-on rather than a claim:

- A Playwright `webServer` (or documented runner) that also boots the API host bound to a disposable DB with
  `PerCampusFormV2` read+write flags ON in the `Testing` environment only.
- A **test-only OTP retrieval** path compiled/enabled only under `Testing` (the DEV build already prints the
  OTP to the backend log when `Smtp:Enabled=false`; a Testing-only sink/endpoint would surface it to the
  spec). No public endpoint or backdoor may exist in production — this must be `#if`/DI-gated to Testing.
- Deterministic seeded auth/role fixtures for the dashboard (ProtectedRoute) journeys.

This is documented here as a limitation, not marked passed. The critical journeys it would cover are already
verified at the layers above (SQL constraints, backend authorization/concurrency/hidden-search Integration,
Vitest component behaviour, and browser-DOM behaviour for the accordion/dialog). The remaining unique value
of full real-stack E2E is cross-boundary wiring (real OTP round-trip, real 409 propagation, real auth
scoping in the browser) — recommended as the first task of a dedicated E2E-infra slice.

## Full-DOM promotion of the v2 mutation & search workflows (this session)

The Slice 6 mutation/search journeys that previously ran at the **real-host API** level (E/F/G/H in
`authenticated-workflows.realstack.spec.ts`) are now **promoted to full browser DOM automation** in a new
spec `tests-realstack/authenticated-ui-workflows.realstack.spec.ts` (helpers in `realstackHelpers.ts`). Each
one navigates a real route, fills real inputs and submits a real button/modal against the real running stack —
no `page.request`, no `page.evaluate(fetch)`, no `route.fulfill`, no DB write to fake the result. Preconditions
(create/approve/reject/submit-as-precondition) and post-action assertions use the authenticated API; the
**action under test is always the DOM**. Stable `data-testid`s were added (logic-neutral) to the detail view,
campus cards, amendment panel/modal, safe-edit modal, edit page and the list search box.

| # | Workflow | DOM action under test | Key assertions | Result |
|---|----------|-----------------------|----------------|--------|
| §6 | Pending edit | edit HN delegation in the real edit form → submit | HN changed + rowVersion bumped; **HCM sibling delegation + rowVersion untouched (true no-op)**; back to PENDING | **PASS** |
| §7 | Resubmit | edit a rejected request in the real resubmit form → submit | campus set fixed (same instance ids); status → PENDING; `resubmissionCount` +1 | **PASS** |
| §8 | Safe edit | change a per-campus transport note in the real modal → save | applied **immediately** (no amendment); sibling + amendment untouched | **PASS** |
| §9 | Amendment submit | add a guest in the real amendment modal → submit (reason required) | submit disabled with empty reason; **active snapshot unchanged**, amendment PENDING_APPROVAL; sibling untouched; no 2nd-pending affordance | **PASS** |
| §10a | Leader approve | HN leader clicks *Duyệt & áp dụng* in the real panel | active↔proposed diff shown; on approve the proposal applies **target-only**; sibling untouched | **PASS** |
| §10b | Leader reject | HN leader rejects in the real panel (reason required) | confirm disabled with empty reason; amendment REJECTED; **active snapshot unchanged**; sibling untouched | **PASS** |
| §11 | Wrong-campus | HCM leader opens the same request in the real UI | HCM leader sees only their own campus card; **the HN card + its approve action never render**; the host also refuses the API approve (403), HN leader passes the gate | **PASS** |
| §12 | Withdraw | requester clicks *Rút đề xuất* in the real panel | amendment WITHDRAWN; snapshot intact; the submit affordance returns (re-proposable) | **PASS** |
| §13 | Search isolation | HN leader types in the real list search box | a hidden-campus (HCM-only) delegation keyword **never surfaces** the request; the HN keyword surfaces it with an HN-only match context and **no HCM name/keyword leaks**; the owner (all campuses) sees the HCM keyword | **PASS** |

**Real-stack counts (this run, actual):**

| Suite | Command | Result |
|-------|---------|--------|
| Real-host API journeys A–H (kept) | `test:e2e:realstack` | **8 passed** |
| Full-DOM mutation/search journeys (new) | `test:e2e:realstack` | **9 passed** |
| **Total real-stack specs** | `test:e2e:realstack` | **17 passed / 0 failed** |

Not double-counted: the existing API-level journeys stay as defense-in-depth; the DOM journeys are additional.

**Other gates (this run):** Frontend `tsc` 0 · Vitest **99** · `vite build` ✓ · Backend Architecture **14/14** ·
E2E auth-guard **4/4** · targeted V2 IT (read + edit + resubmit + safe-edit + amendment + mixed-list, run against
the sanctioned self-rolling-back `pems_pr3_test`) **44/45** · Backend Unit **528/530**.

### Dev auto-merge overlaps found by the audit (NOT introduced by this session)

The environment auto-merged `Dev` into the branch (`64c83a59`) mid-stream. The overlap audit (§2) surfaced
three items, all **pre-existing on the merged branch, none caused by this session's changes** (which touch only
frontend test-ids + real-stack specs + the UnitTests harness dedup below):

1. **UnitTests harness compile break (fixed).** The merge left `VisitExpense*` DbSet declarations **duplicated**
   inside four unit-test contexts (`DelegationsTestHarness`, `CampusUcTestHarness`, `Uc106TestHarness`,
   `PartnersTestDbContext`), so `PEMS.UnitTests` would not compile at all. Repaired by removing the duplicated
   block (the canonical copy from `8e9c9b0b` stays) — a merge-artifact cleanup. UnitTests now build and run.
2. **Two pre-existing photo-upload unit failures (not fixed — out of scope).**
   `UploadVisitInstancePhotosCommandHandlerTests.{SpoofedMime,OversizedFile}_FailsBeforeAnyDriveWork` now fail
   (“no exception thrown”). Root cause: the merge changed shared `backend/.../Common/Files/FileValidationPolicy.cs`.
   This is a VisitPhotos/file-validation regression unrelated to per-campus v2; left for the photo/merge owner.
3. **Guest-search vs Slice 5B security test (conflict — flagged for reconciliation, not unilaterally resolved).**
   The merge added `vr.GuestMembers.Any(name/jobTitle/organization contains keyword)` to the list keyword filter
   (`ViewGuestDelegationListQueryHandler`), so the Slice 5B IT `V2MixedListSurfacesTests.
   Guest_member_names_are_not_searched_and_produce_no_row` now fails (the guest keyword surfaces the row).
   Security analysis: this does **not** break the two invariants Slice 5B protects — scope-before-keyword still
   holds (a scoped leader only ever surfaces their own authorized campus row), and `matchedContexts` is still
   built solely from request/campus fields (guest names are **never** among them → no PII and no hidden campus
   in the rendered “Khớp tại” chip; a guest-only match yields an empty context list). What changed is only
   whether a guest-name keyword *surfaces a row the actor is already authorized to see*. This is a genuine
   product/security-posture decision between two teams and is **left for human reconciliation** (either retire/
   update the 5B row-exclusion assertion to “searchable but PII-free in results”, or revert the Dev clause);
   this session neither deleted the security test nor reverted the Dev feature. The new §13 DOM journey asserts
   only the surviving invariants (delegation-name hidden-campus isolation + PII-free contexts), so it is
   consistent with either resolution.

**Full `PEMS.IntegrationTests` (≈400) not re-run this session:** it is not cleanly runnable here without
out-of-scope / hygiene-violating setup — the factory tests need `pems_test` (absent) and 25 V2 IT files hardcode
`pems_pr3_test`, which is stale vs the current merged master (71 vs 76 tables; missing the newer expense tables)
and is a protected DB that must not be recreated. This session changed no backend production or IT-test code, and
the v2 backend paths are exercised end-to-end by the real-stack 17/17 plus the targeted V2 IT 44/45 above.
