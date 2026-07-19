# PR-2 (Per-Campus Form v2 SQL) — Real MySQL Test Report

**Date:** 2026-07-15
**Engine:** MySQL Community Server **8.0.46** (Win64), local service `MySQL80`, `root@localhost:3306`.
**Default `sql_mode`:** `ONLY_FULL_GROUP_BY,STRICT_TRANS_TABLES,NO_ZERO_IN_DATE,NO_ZERO_DATE,ERROR_FOR_DIVISION_BY_ZERO,NO_ENGINE_SUBSTITUTION` (no `ANSI_QUOTES`).
**Client:** `C:\Program Files\MySQL\MySQL Server 8.0\bin\mysql.exe`.

All scripts were **executed on real MySQL 8.0.46**. Nothing below is a static-review claim.
The real dev databases `pems_db` / `pems_test` were **never touched** — every test ran in
throwaway databases (`pems_v2_fresh_test`, `pems_v2_upgrade_test`, `pems_v2_rollback_test`),
built from sandbox copies with the DB name rewritten so the master's `DROP DATABASE IF EXISTS`
could not hit production.

## 1. Files changed / added

| File | Status | Notes |
|------|--------|-------|
| `docs/database/scripts/percampus_v2_migration/00_README_IMPORT_ORDER.md` | new | Import order, MySQL 8.0.16+, safe rollback vs DOWN. |
| `docs/database/scripts/percampus_v2_migration/01_preflight_readiness.sql` | new | Report-only readiness (P01–P13). |
| `docs/database/scripts/percampus_v2_migration/02_up_additive.sql` | new | Idempotent DDL (tables/cols/constraints/indexes/triggers). |
| `docs/database/scripts/percampus_v2_migration/03_backfill.sql` | new | Idempotent detail/link/access/revision backfill. |
| `docs/database/scripts/percampus_v2_migration/04_verify.sql` | new | 15 verify checks (V01–V15) + presence roll-up. |
| `docs/database/scripts/percampus_v2_migration/05_rollback_down.sql` | new | Guarded destructive DOWN. |
| `docs/database/scripts/percampus_v2_migration/PR2_TEST_REPORT.md` | new | This report. |
| `docs/database/scripts/pems_full_v10_..._NOTIFICATIONS_FIXED.sql` | modified (+436/−22) | Fresh-create master synced: 8 tables, columns/indexes/checks, 3 triggers, seed-time backfill, corrected comments. |

## 2. Problems found while running (all fixed)

1. **`ERROR 3823` on import** — `CHECK (change_kind<>'TRANSFER' OR old_user_id IS NOT NULL)`
   referenced `old_user_id`, which carries an FK referential action (`ON DELETE SET NULL`).
   MySQL forbids a CHECK on such a column. **Fix:** removed the CHECK, enforce the invariant
   with `trg_identity_changes_transfer_bi/bu` (SIGNAL 45000). Applied to both files.
2. **V08 = 22 detail-vs-parent mismatches (fresh)** — the seed-time backfill ran *before* the
   master's `visit_requests` enrichment `UPDATE`s (7 of them), so the per-campus snapshot froze
   pre-enrichment `delegation_name/purpose/working_content/note_to_fptu`. **Fix:** moved the
   seed-time backfill to the very end of the seed section, after all enrichment.
3. **V09 = 204 missing baseline revisions (fresh)** — the fresh seed populated detail + links but
   not revision history. **Fix:** the relocated seed-time backfill now also writes one
   `form_revision=1` baseline row per instance.
4. **`ERROR 1146` in preflight** — check `P12` referenced `visit_instance_form_details`, which
   does not exist at preflight time (before UP). **Fix:** removed P12 (it was informational and
   misplaced).
5. **Cancel-trigger logic bug** — the original `IF visitor_user_id IS NULL` branch mishandled the
   core 3A case (owner-less + `PENDING_CONFIRMATION`): it allowed *any* VISITOR and blocked a
   STAFF registrant, and used non-NULL-safe `<>` comparisons. **Fix:** rewrote to discriminate on
   `primary_contact_access_status` with NULL-safe comparisons, matching §16.7/§19.6. Applied to
   both files and re-tested.

## 3. Test results

### 3.1 Fresh-create (`pems_v2_fresh_test`)
- Import of the edited master: **exit 0, empty stderr**.
- `04_verify.sql`: **V01–V09, V12–V15 = 0**; V07/V10/V11 presence = 1.
- Seed consistency: `instances=204, details=204, member_links=762 (=Σ members×instances),
  owned_active=117, under_30min=0`, baseline revisions=204.

### 3.2 Upgrade (`pems_v2_upgrade_test`) — pre-v2 baseline → migrate
Order: import pre-v2 baseline (from `git HEAD`) → `01_preflight` → `02_up` → `03_backfill` → `04_verify`.
- Preflight: **P01–P13 all 0** (no blockers).
- UP: exit 0, empty stderr. Backfill: exit 0, empty stderr.
- Verify: **all violations 0**, `instances=details=revisions=204`, `links=762`, `owned_active=117, ownerless_pending=0`.

### 3.3 Idempotency (upgrade DB)
- Re-ran `02_up` and `03_backfill`: **exit 0, empty stderr**; counts unchanged (`det=204, lnk=762, rev=204`);
  verify still all-0. No `already exists` / duplicate-row errors.

### 3.4 Constraint & trigger tests — **29/29 PASS** (`scratchpad/pr2/constraint_tests.sh`, `cancel_tests.sh`)

Duration (`ck_visit_instance_min_duration_30m`, error 3819):
- 29m59s → reject; 30m00s → accept; 31m → accept; end=start → reject; end<start → reject.

Cross-request member link (composite FK, error 1452):
- member(reqA) into instance(reqB) tagged as reqB → reject; tagged as reqA → reject; correct same-request link → accept.

Identity pending guard (unique, error 1062) + TRANSFER trigger (error 1644):
- 1st PENDING → accept; 2nd PENDING same (request,relation) → reject; after SUPERSEDED, new PENDING → accept;
  TRANSFER + NULL old_user_id (INSERT) → reject; TRANSFER + old_user_id → accept; UPDATE→TRANSFER+NULL old → reject.

Amendment pending guard (unique) + composite FK:
- 1st PENDING_APPROVAL → accept; 2nd on same instance → reject; after REJECTED, new → accept;
  amendment tagged with wrong request → reject.

Cancel exception 3A (`trg_visit_requests_cancel_validate_bu`, error 1644):
- registrant(STAFF) cancels while contact PENDING, >24h → accept;
- registrant(VISITOR) cancels while PENDING → accept;
- unrelated VISITOR → reject; HO registrant → reject (role gate); DEPARTMENT registrant → reject (role gate);
- owner(VISITOR) cancels while contact ACTIVE → accept; registrant cancels while ACTIVE → reject;
- within 24h → reject; started/ongoing campus (DURING_VISIT w/ host+agenda) → reject;
- request status REJECTED → reject; HO cancels another's request → reject.

Generated-column guards compiled and enforced on real MySQL (VIRTUAL unique guards with CASCADE FKs
on their base columns imported without error — the FK-action restriction is STORED-only).

### 3.5 Rollback (`05_rollback_down.sql`)
- **Refusal:** on the upgrade DB with 2 amendment rows present, DOWN aborted at the guard
  (`Unknown column 'DOWN_REFUSED__…'`), **all 8 v2 tables preserved** (0 drops).
- **Clean DOWN:** on `pems_v2_rollback_test` (baseline + UP, no amendments/identity/mixed → 0
  unreconstructable rows), DOWN ran to completion (exit 0): **8 v2 tables dropped, 4 v2 columns
  dropped, `ck_visit_instance_min_duration_30m` dropped**, original cancel trigger restored.

### 3.6 Final master re-import (all comment + trigger edits)
- Edited master re-imported clean (**exit 0, empty stderr**); verify all-0; corrected cancel
  trigger and both identity triggers present.

### 3.7 V12/V13 actively exercised with a real v2 fixture — **6/6 PASS** (`scratchpad/pr2/v2_fixture_test.sh`)
Because all seed rows are v1, V12/V13 are only *vacuously* 0 on seed data. To prove they actually
fire, a synthetic `form_schema_version=2` request (id 995001) was built on a fresh master import:
**2 campuses, DIFFERENT per-campus details** (delegation/purpose/visit_type/operational_contact/
language/media_consent differ → `COUNT(DISTINCT signature)=2`), **per-campus member links**,
**baseline `form_revision=1` revisions**, and **`has_mixed_campus_details=1`**.

- Positive: well-formed v2 request → **V12 = 0** and **V13 = 0** (checks evaluate real v2 rows,
  `v2_requests=1, details=2, links=2, revs=2, distinct signatures=2`).
- Negative V12: delete one instance's detail → **V12 ≥ 1 (detected)**; re-insert → V12 = 0.
- Negative V13: set `has_mixed_campus_details=0` while the two details differ → **V13 ≥ 1
  (detected)**; restore to 1 → V13 = 0.
- Fixture removed afterward (v2 request count back to 0); the throwaway DB was dropped.

## 4. Notes / remaining limitations

- **`form_schema_version` of seed rows stays 1** (all demo requests use the v1 global form, so `1`
  is correct/legacy) and **`has_mixed_campus_details` stays 0 on seed/backfill** (all campuses share
  the identical global form → genuinely not mixed). Because of that, V12/V13 are only *vacuously* 0
  on seed data — so they were additionally exercised positively **and** negatively against a
  synthetic `form_schema_version=2` fixture (see §3.7). No standing v2 seed rows are added by this PR;
  real v2 requests arrive with PR-4.
- **Started-campus cancel guard is shadowed by the 24h guard.** Any started campus has a past
  `planned_start_at` (<24h), so the 24h guard fires first; both reject. Behaviour (reject) verified;
  the `already started` message path is effectively unreachable but left intact (inherited, unchanged).
- **`operational_visit_instances_missing_agenda_final = 3`** and `seed_placeholder_terms_remaining = 81`
  appear in the master's own seed self-checks. These are **pre-existing** diagnostics in the baseline
  file (unrelated to v2) and are unchanged by this PR.
- **Backend/frontend untouched (correct for PR-2).** The C# `VisitRequest.cs` XML comments still say
  registrant is read-only / visitor is the sole editor — that reflects current v1 backend behaviour
  and must be updated when PR-4/PR-5 implement the co-editor and identity flows. Flagged, not changed,
  to stay within PR-2 scope.
- Test harness scripts live in the session scratchpad (`.../scratchpad/pr2/`), not committed.

## 5. Exact commands (representative)

```bash
MYSQL="/c/Program Files/MySQL/MySQL Server 8.0/bin/mysql.exe"; export MYSQL_PWD=123456
# sandbox copies (protect real pems_db)
git show HEAD:docs/.../pems_full_...FIXED.sql > baseline_master.sql
sed 's/pems_db/pems_v2_fresh_test/g'   pems_full_...FIXED.sql > fresh_edited.sql
sed 's/pems_db/pems_v2_upgrade_test/g' baseline_master.sql    > upgrade_baseline.sql
# fresh
"$MYSQL" -uroot --default-character-set=utf8mb4 < fresh_edited.sql
"$MYSQL" -uroot pems_v2_fresh_test < percampus_v2_migration/04_verify.sql
# upgrade
"$MYSQL" -uroot --default-character-set=utf8mb4 < upgrade_baseline.sql
for s in 01_preflight_readiness 02_up_additive 03_backfill 04_verify; do
  "$MYSQL" -uroot pems_v2_upgrade_test < percampus_v2_migration/$s.sql; done
# idempotency: re-run 02_up + 03_backfill + 04_verify (counts unchanged)
# rollback: "$MYSQL" -uroot <db> < percampus_v2_migration/05_rollback_down.sql
```

Constraint/trigger harnesses: `scratchpad/pr2/constraint_tests.sh`, `scratchpad/pr2/cancel_tests.sh`.
