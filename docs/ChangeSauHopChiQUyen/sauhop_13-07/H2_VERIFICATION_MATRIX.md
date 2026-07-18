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

## Full real-stack E2E — honest infrastructure status

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
