# Phase H-1 — Per-Campus v2 Migration Lifecycle Drill

Executed on **MySQL 8.0** (project baseline, `localhost:3306`) against disposable databases only:
`pems_h_fresh`, `pems_h_upgrade`, `pems_h_rollback`. **No mutation** of `pems_db`, `pems_test`
or `pems_pr3_test` (read-only hygiene only). This drill re-validates the PR-2 package **plus the
Phase G-4A addition** (`visit_request_pending_forms`, patch `08_up`) and fixes the fresh-vs-upgrade
schema drift it introduced.

## 0. Drift found + fixed (H-1 package sync)

The Phase G-4A migration added `08_up_pending_v2_forms.sql` (upgrade path) but the table was **missing
from the fresh master** `pems_full_v10_..._FIXED.sql` — a real fresh-vs-upgrade drift. Fixed in this slice:

- **Master fresh-create** now defines `visit_request_pending_forms` in the v2 table block (after
  `visit_request_revision_history`), byte-compatible with `08_up`.
- **`00_README_IMPORT_ORDER.md`** now documents the additive follow-on patches `06`/`07`/`08` and the
  `visit_request_pending_forms` addition; notes the fresh master already integrates them.
- **`05_rollback_down.sql`** already drops `visit_request_pending_forms` (added with G-4A); confirmed here.

## 1. Fresh-create drill — `pems_h_fresh` — ✅ PASS

- `CREATE DATABASE pems_h_fresh` → import fixed master (`sed pems_db→pems_h_fresh`,
  `--default-character-set=utf8mb4`). Exit 0, ~7 s, 0 errors.
- All 9 v2 tables present, **including `visit_request_pending_forms`**:
  form_details, instance_guest_members, identity_changes, identity_change_events, amendments,
  amendment_changes, form_revision_history, request_revision_history, pending_forms.
- `visit_request_pending_forms` structure verified: 9 columns (correct types/nullability/defaults),
  PK `pending_form_id`, unique `uq_pending_forms_submission`, index `idx_pending_forms_expires`.
- v2 guards present: `ck_visit_instance_min_duration_30m` (1), `uq_identity_change_pending` (1),
  `uq_vgm_request_member` (composite).

## 2. Upgrade / backfill drill — `pems_h_upgrade` — ✅ PASS

Baseline source: the **pre-v2 master** `git show ed693f6d:<master>` (the last commit before the v2
schema was folded in — `v2 tables = 0` confirmed). Order: import baseline → `02_up_additive` →
`03_backfill` → `06_up` → `07_up` → `08_up` → `04_verify`. All exit 0.

`04_verify.sql` — **every V01–V15 `violation_count` = 0; presence V07/V10/V11 = 1**:

| Check | Result |
|-------|--------|
| V01 instances_without_detail | 0 |
| V02 detail_orphan | 0 |
| V03 member_link_count | 0 (expected 762, found 762) |
| V04 link_cross_request | 0 |
| V05 owned_request_not_active | 0 |
| V06 ownerless_request_active | 0 |
| V07 duration_constraint_present | 1 |
| V08 detail_vs_parent_mismatch | 0 |
| V09 missing_baseline_revision | 0 |
| V10 uq_vrc_request_instance | 1 |
| V11 uq_vgm_request_member | 1 |
| V12 v2_request_incomplete | 0 |
| V13 mixed_flag_mismatch | 0 |
| V14 duplicate_pending_identity | 0 |
| V15 duplicate_pending_amendment | 0 |

Backfill sanity: 204 instances = 204 form_details (0 without detail), 762 instance member links,
0 `primary_contact_access_status` NULL.

## 3. Idempotency — ✅ PASS

Re-ran `02_up` + `03_backfill` + `06` + `07` + `08` on the upgraded DB. Snapshot
`details|links|revisions|pending|tables` **identical before and after**: `204|762|0|0|71`. No
duplicate rows, no schema change.

## 4. Schema diff fresh vs upgrade — ✅ IDENTICAL

`information_schema.columns` signature and `information_schema.statistics` (index) signature are
**byte-identical** between `pems_h_fresh` and `pems_h_upgrade`; table count **71 = 71**. The master
fix converged the two paths — no undocumented drift remains.

## 5. Constraint boundaries — ✅ PASS

| Case | Expected | Observed |
|------|----------|----------|
| instance duration 29m59s (UPDATE) | reject | `ERROR 3819 ck_visit_instance_min_duration_30m` |
| instance duration exactly 30m00s | accept | accepted (TIMESTAMPDIFF(MINUTE)=30) |
| end = start | reject | `ERROR 3819 ck_visit_instance_min_duration_30m` |
| `pending_forms` duplicate `submission_id` | reject | `ERROR 1062 uq_pending_forms_submission` |

(29m59s / 30m / duplicate-campus / OTHER-blank are additionally enforced at the app layer —
`InitiateVisitRequestV2CommandValidator` unit tests + create-service integration tests.)

## 6. Rollback drill — `pems_h_rollback` — ✅ BOTH BRANCHES PASS

Built from the pre-v2 baseline + `02_up` + `06/07/08` (v2 data tables empty).

- **Refusal guard**: set `has_mixed_campus_details=1` on one request → run `05_rollback_down.sql` →
  aborts with `ERROR 1054 Unknown column 'DOWN_REFUSED__unreconstructable_v2_data_present…'`.
  `visit_request_pending_forms` and `visit_instance_form_details` **still present** — no partial drop.
- **Clean controlled DOWN**: reset guard input to 0 → `05_rollback_down.sql` exit 0 → **0 of 9 v2
  tables remain**, 0 additive `visit_requests` columns remain, `ck_visit_instance_min_duration_30m`
  gone, 0 orphan v2 indexes (`idx_pending_forms_expires`/`uq_pending_forms_submission`/
  `uq_vrc_request_instance`/`uq_vgm_request_member`). Schema back to v1-compatible.

Production rollback remains **flag-OFF + dual-read**, never this DOWN (per README + plan §24.3).

## 7. Query / index verification — ✅ PASS

`EXPLAIN` on the hot v2 read paths + the new `pending_forms` lookups (seeded rows):

| Query | type | key | rows |
|-------|------|-----|------|
| `pending_forms WHERE submission_id=?` (verify) | const | `uq_pending_forms_submission` | 1 |
| `pending_forms WHERE expires_at<NOW()` (sweep) | range | `idx_pending_forms_expires` | 1 |
| `form_details WHERE visit_instance_id=?` (per-campus detail) | const | `PRIMARY` (PK = visit_instance_id) | 1 |

Per-campus detail is a PK point lookup, so the read-path JOIN cannot degrade into a per-instance N+1;
the application-level N+1 avoidance (FormDetail nav JOIN, batched `VisitInstanceEffectiveName`) is
covered by the full IntegrationTests (372/372).

## Protected-DB hygiene

Read-only checks after the drill: `pems_db` and `pems_test` have **no `visit_request_pending_forms`
table** (untouched); `pems_pr3_test` v2_requests = 0, pending_forms = 0. `pems_pr3_test` carries the
additive `08_up` schema by design (the sanctioned direct-handler test DB) — it is **not** unmutated,
and the report wording is corrected accordingly.
