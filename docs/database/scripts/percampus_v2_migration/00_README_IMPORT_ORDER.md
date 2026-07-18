# PEMS — Per-Campus Form v2 + Identity Edit — SQL Migration Package (PR-2)

Additive database foundation for the plan
`docs/ChangeSauHopChiQUyen/sauhop_13-07/PEMS_MULTI_CAMPUS_PER_CAMPUS_FORM_AND_IDENTITY_EDIT_PLAN.md`.

This package is **PR-2 only** (plan §29): the SQL layer. It is **purely additive** —
no v1 column is dropped, no live schedule is rewritten. Backend and frontend still
run on v1 until later PRs; the feature flag `PerCampusVisitFormV2` stays **OFF**.

## Minimum environment

- **MySQL 8.0.16+** (required):
  - `CHECK` constraints are enforced (the 30-minute duration rule).
  - Functional expressions in `CHECK` (`TIMESTAMPDIFF`) are allowed.
  - `UNIQUE` indexes on **VIRTUAL generated columns** back the "one PENDING row"
    guards (MySQL has no partial/filtered unique index).
- Engine `InnoDB`, charset `utf8mb4`, collation `utf8mb4_unicode_ci` (matches baseline).
- Run each `.sql` file with a client that honours `DELIMITER` (the `mysql` CLI, or
  MySQL Workbench "Run Script"). Do **not** paste trigger/procedure bodies into a
  tool that splits on `;`.

## Files & run order (existing/running database)

| # | File | Mutates? | Purpose |
|---|------|----------|---------|
| 1 | `01_preflight_readiness.sql` | No (report only) | Lists blockers. **Every `violation_count` must be 0** (P08 is informational: owner-less rows that stay PENDING_CONFIRMATION) before step 3. Fix reported rows **by hand** — nothing is auto-corrected. |
| 2 | *(take a backup)* | — | Back up the database / snapshot a production copy first (plan §24.1). |
| 3 | `02_up_additive.sql` | DDL | Adds columns, tables, constraints, indexes; replaces the cancel trigger with the 3A branch. Idempotent (re-runnable). |
| 4 | `03_backfill.sql` | DML | Clones per-campus detail, links members to every instance, sets `primary_contact_access_status`, seeds baseline revision history. Idempotent. |
| 5 | `04_verify.sql` | No (report only) | Post-backfill checks. **All `violation_count` = 0** and all presence checks = 1. |

### Additive follow-on patches (apply after `02_up`, any order; each idempotent)

| # | File | Mutates? | Purpose |
|---|------|----------|---------|
| 6 | `06_up_identity_claim_tokens.sql` | DDL (ENUM extend) | `email_action_tokens` gains `VISIT_CONTACT_CLAIM` context + `VISIT_REQUEST_IDENTITY_CHANGE` target (Phase D claim). |
| 7 | `07_up_transfer_tokens.sql` | DDL (ENUM extend) | `email_action_tokens` gains `VISIT_CONTACT_TRANSFER` context (Phase D-4 transfer). |
| 8 | `08_up_pending_v2_forms.sql` | DDL | Adds `visit_request_pending_forms` (Phase G-4A public v2 OTP initiate — binds the validated v2 snapshot to a submit intent). `CREATE TABLE IF NOT EXISTS` → re-runnable. |
| 9 | `09_up_op_contact_optional.sql` | DDL | Relaxes `visit_instance_form_details.operational_contact_organization` + `operational_contact_email` to NULL (Phase H-4 fix — the operational contact org/email are OPTIONAL; a blank value now persists as NULL instead of violating the `TRIM(x) <> ''` CHECK). Guarded MODIFY → re-runnable. |

The fresh master (below) already integrates 06/07/08/09 — these patches are only for an existing v1/PR-2 DB.

`05_rollback_down.sql` is the **destructive** DOWN. It is *not* the normal rollback —
the safe rollback is "flag OFF + dual-read" (see below). Only run DOWN with a backup
and a conscious decision; it drops the v2 tables (including `visit_request_pending_forms`)
and the data they hold.

## Fresh / new environment

For a brand-new database, import the master fresh-create script — the v2 schema **and**
the seed-time per-campus population are already integrated there:

```
docs/database/scripts/pems_full_v10_TTS_Gallery_FULL_UPDATED_NOTIFICATIONS_FIXED.sql
```

That file seeds demo visit requests/campuses/members and then runs the fresh-create
equivalent of `03_backfill.sql` steps 1–2 (one detail row per instance + member links)
plus the `primary_contact_access_status` normalization. So **do not** run
`03_backfill.sql` against a freshly-imported master — it is already backfilled (the
`WHERE NOT EXISTS` guards make a re-run harmless, but it is unnecessary). You may still
run `04_verify.sql` as a smoke check (V09 baseline-revision rows are only created by the
standalone backfill / runtime v2 writes, so V09 may be non-zero on a pure fresh import —
that is expected).

## What this migration adds

- **`visit_requests`** (additive columns): `form_schema_version`,
  `has_mixed_campus_details`, `primary_contact_access_status`,
  `primary_contact_verified_at`; two supporting indexes.
- **`visit_request_campuses`**: named `ck_visit_instance_min_duration_30m`
  (`TIMESTAMPDIFF(MINUTE, start, end) >= 30`, the existing `end > start` check is
  kept); composite unique `uq_vrc_request_instance`.
- **`visit_guest_members`**: composite unique `uq_vgm_request_member`.
- **`visit_instance_form_details`** — one full form snapshot per campus instance
  (active v2 data), FULLTEXT search index, `form_revision` / `approval_revision`.
- **`visit_instance_guest_members`** — per-campus guest/support links with composite
  FKs binding member+instance to the same request (blocks cross-request links).
- **`visit_request_identity_changes`** + **`..._identity_change_events`** — the
  INITIAL_CLAIM / TRANSFER state machine and its append-only event log; a virtual
  `pending_guard` enforces one in-flight change per (request, relation).
- **`visit_instance_amendments`** + **`..._amendment_changes`** +
  **`visit_instance_form_revision_history`** + **`visit_request_revision_history`** —
  post-approval amendment/revision state; a virtual guard enforces one
  `PENDING_APPROVAL` amendment per instance.
- **`audit_logs` / `audit_log_changes`** — additive context/masking columns and
  indexes (no FK on the nullable `visit_request_id` / `visit_instance_id` so audit
  survives business-row deletion; audit is never cascade-deleted).
- **`visit_request_pending_forms`** (patch `08_up`, Phase G-4A) — public v2 OTP
  initiate binding: one row per submit intent holding the full canonical v2 snapshot
  + its fingerprint, so `verify` builds the request from exactly what was OTP-verified.
  Standalone (no FK — bound to a submission intent, not a request); consumed at verify.
- **`trg_visit_requests_cancel_validate_bu`** — rewritten for cancel exception **3A**
  (registrant may cancel only while the initial contact is `PENDING_CONFIRMATION`;
  once `ACTIVE` it reverts to the exact contact-owner rule; every other guard kept).

## Safe rollback (preferred over DOWN)

Per plan §24.3:

1. Set feature flag `PerCampusVisitFormV2` **OFF**.
2. Keep the backend in **dual-read** — it still reads the v1 global columns, which
   this migration never dropped.
3. Leave the v2 tables in place (they are additive and inert while the flag is OFF).

Only use `05_rollback_down.sql` before any production v2 writes, or after an
export/restore path has been proven lossless. Dropping the v2 tables destroys
per-campus detail, identity-change history and amendment history that cannot be
rebuilt from v1 columns once campuses diverge.

## Validation status

- **Executed and verified on MySQL 8.0.46** (fresh-create import, pre-v2→v2 upgrade,
  idempotent re-run, 29 constraint/trigger tests, and both rollback paths). Full results:
  [`PR2_TEST_REPORT.md`](PR2_TEST_REPORT.md).

## Not in this PR

- Backend entities / `DbContext` / commands / queries (PR-3+), frontend (PR-3),
  identity & amendment handlers, search, and the audit writer are **out of scope**
  for this SQL package.
