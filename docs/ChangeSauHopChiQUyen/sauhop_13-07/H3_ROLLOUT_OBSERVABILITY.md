# Phase H-3 — Rollout, Canary, Observability & Flag-Rollback

Verified against source this session. Nothing here is deployed, canaried, or config-changed — this is the
prepared runbook. Contract-drop (Phase I) is out of scope.

## 1. Feature flags (exact names + code defaults — verified in source)

| Flag | Config section | Option class | Code default | Gate |
|------|----------------|--------------|--------------|------|
| Read | `PerCampusFormV2` | `PerCampusFormV2Options.Enabled` | **`false`** (Program.cs binds with `?? new …Options()`) | v2 read endpoints 404 when OFF; all v1 read paths byte-identical. |
| Write | `PerCampusFormV2Write` | `PerCampusFormV2WriteOptions.Enabled` | **`false`** | v2 create / public initiate / verify / pending-edit / resubmit inert (404) when OFF. |

Combined gating (enforced in the create/initiate/verify/edit handlers):

- **Write OFF** → only the v1 flow runs, byte-identical (the v2 endpoints 404).
- **Write ON + Read OFF** → **rejected** with stable `PER_CAMPUS_V2_READ_REQUIRED` (would write records no
  read path can surface). Never a valid production state.
- **Read ON + Write ON** → v2 active.

Neither flag has an appsettings section in any tracked `appsettings*.json`, so both resolve to the code
default `false`. **Production default is OFF by construction, not by inference.**

## 2. Observability — what exists in source (audited)

The project has **no metrics/counter framework** (no `IMeterFactory` / OpenTelemetry / Prometheus). Per the
Phase-H rule, none was introduced. The observability model is **structured `ILogger` + append-only
`audit_logs`**:

- **Failure observability (H-3 instrumentation added this slice)**: `ExceptionHandlingMiddleware` now logs
  every `ConflictException` and `BusinessRuleException` at Information level by **stable `errorCode` + request
  path + traceId only** (no message, no PII). This makes the v2 failure codes observable in logs:
  `PER_CAMPUS_V2_PENDING_NOT_FOUND`, `PER_CAMPUS_V2_SUBMISSION_FORM_MISMATCH`, `PER_CAMPUS_V2_READ_REQUIRED`,
  `VISIT_REQUEST_VERSION_CONFLICT`, `VISIT_INSTANCE_VERSION_CONFLICT`, `VISIT_FORM_DETAIL_MISSING`,
  `FORM_VERSION_UPGRADE_REQUIRED`, plus the OTP challenge codes (already logged). Auth/OTP failures were
  already logged by code; validation failures log their message.
- **Lifecycle observability**: `audit_logs` (+ `audit_log_changes`) carry `correlation_id`, `source_type`,
  masked field values, `visit_request_id` / `visit_instance_id`, and stable action codes — e.g.
  `VISIT_REQUEST_CREATED_V2`, `UPDATE_PENDING_VISIT_REQUEST_V2`, `VISIT_SAFE_FIELDS_UPDATED`,
  `VISIT_AMENDMENT_APPROVED`, `VISIT_INSTANCE_FORM_REVISION_APPLIED`, and the identity-change event log
  (`visit_request_identity_change_events`, append-only, masked emails). These are the record of create /
  edit / safe-edit / amendment / identity lifecycle.
- **Log hygiene (verified)**: no `ILogger` statement in the v2 command/service/identity paths logs an OTP
  code, raw session/challenge token, token hash, full email, snapshot JSON or phone (grep-verified). Emails
  in audit/events are masked (`VisitRequestFingerprintBuilder.MaskEmail`). Correlation is via `traceId`
  (response + logs) and `correlation_id` (audit).

**Limitation (accurately stated)**: there are **no numeric counters/histograms** (create success rate,
per-code failure rate, query latency, canary health) — those require a metrics backend the project does not
have. The metrics named in §3 are therefore **log/audit-derived** (count matching structured log lines /
audit rows over a window), not push-gauge metrics. Standing up a metrics backend is a scoped follow-on.

## 3. Metrics to watch (log/audit-derived; thresholds are PLACEHOLDERS pending Product sign-off)

| Signal | Source | Rollback action if breached |
|--------|--------|------------------------------|
| v2 create success vs failure | audit `VISIT_REQUEST_CREATED_V2` count vs failure logs | create failure ratio ↑ over baseline → flags OFF |
| initiate/verify failure by code | logs `Conflict/Business (…)` for the v2 codes | `PENDING_NOT_FOUND`/`FORM_MISMATCH` spike → investigate client; flags OFF if systemic |
| pending-edit/resubmit conflict | logs `VISIT_REQUEST_/INSTANCE_VERSION_CONFLICT` | 409 conflict spike → flags OFF, inspect concurrency |
| missing v2 detail | logs `VISIT_FORM_DETAIL_MISSING` (409) | any sustained > 0 on read → flags OFF (backfill gap) |
| legacy mixed 409 | logs `FORM_VERSION_UPGRADE_REQUIRED` | spike → the v1→v2 routing UX is failing |
| identity/amendment lifecycle | audit event tables | stuck PENDING / expiry job failures → investigate job |
| query latency / DB errors | DB/app logs, `EXPLAIN` (H-1 index proof) | latency regression → flags OFF |
| E2E/canary smoke | manual canary run | smoke fail → do not widen cohort; flags OFF |

Numeric thresholds (e.g. "create failure > X% for Y min") are **left as placeholders for Product/SRE
approval** — the metric + rollback action are defined; the trigger number is a business decision.

## 4. Rollout runbook (ordered; nothing executed here)

1. **Backup + staging clone** of production; run the drill from H-1 on the clone first.
2. **Preflight** `01_preflight_readiness.sql` on the clone → every `violation_count` = 0.
3. **Additive SQL** — for an existing DB apply `02_up_additive` → `03_backfill` → `06`/`07`/`08` (all
   idempotent). A fresh DB imports the master (which now integrates all of the above — H-1 fix).
4. **Deploy backend** with **both flags OFF** (v1 unchanged; v2 endpoints 404).
5. **Verify schema** `04_verify.sql` → V01–V15 = 0, presence = 1 (as in H-1).
6. **Backfill by batch** if the DB predates v2 (the backfill is idempotent + `WHERE NOT EXISTS`-guarded).
7. **Verify counts/checksum/latency** — instance↔detail 1:1, member links, `has_mixed` correctness (V13),
   index usage (H-1 EXPLAIN).
8. **Deploy frontend** with the v2 routes present but the backend flags still OFF (routes 404 gracefully).
9. **Internal canary**: enable `PerCampusFormV2` **and** `PerCampusFormV2Write` for a test-account cohort
   only (both must be ON together — write-ON+read-OFF is rejected). Run the smoke journeys.
10. **Widen cohort gradually**, watching the §3 signals after each step.
11. **Full enable** only after the success gate holds across a soak window.
12. **Rollback = flags OFF** (both), backend stays in dual-read on the un-dropped v1 columns. **Never** run
    the DOWN / drop tables/columns as a production rollback (§6, H-1 rollback drill).

## 5. Exit / rollback criteria

Roll back (flags OFF) immediately on any of: sustained v2 create-failure increase; `PENDING_NOT_FOUND` /
`SUBMISSION_FORM_MISMATCH` spike; `VERSION_CONFLICT` spike; any `VISIT_FORM_DETAIL_MISSING` on a read path;
`FORM_VERSION_UPGRADE_REQUIRED` spike (v1→v2 routing broken); abnormal 403/404 authorization rates;
query-latency regression; background expiry/redaction job failures; notification-dispatch failures;
backfill checksum/data mismatch; or an E2E smoke failure. The trigger numbers are placeholders pending
Product/SRE approval; the signal and the action are fixed.

## 6. Why flags-OFF (not DOWN) is the production rollback

The v2 schema is additive — no v1 column was dropped, and the backend dual-reads the v1 columns whenever the
read flag is OFF. Turning the flags OFF instantly reverts every surface to byte-identical v1 behaviour with
**zero data loss**. The `05_rollback_down.sql` DOWN is destructive (drops the v2 tables incl.
`visit_request_pending_forms`, per-campus detail, identity/amendment history) and its refusal guard aborts
when unreconstructable v2 data exists (proven in H-1). DOWN is only for a pre-write environment or after a
proven lossless export — never the live rollback path.

## 7. Condition to proceed to Phase I (contract-drop)

Only after: v2 has run enabled in production long enough that **every** request is `form_schema_version = 2`
(no v1 rows read at runtime), the 10 legacy global columns have zero runtime readers, and a backfilled +
verified export exists. Phase I then prepares (never executes on a real DB) the guarded migration to drop
the 10 global columns. Not started this session.
