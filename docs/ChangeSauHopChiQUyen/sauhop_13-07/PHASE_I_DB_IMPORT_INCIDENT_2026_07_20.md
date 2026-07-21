# Incident — `pems_db` overwritten by a master-dump import (2026-07-20)

**Status:** `INCIDENT CLOSED WITHOUT RECOVERY — pems_db was overwritten by reproducible master
seed; owner confirmed no irreproducible data; no PITR performed; safe-import controls
implemented/tested as listed below.`

This record exists so the control gap is not repeated. It describes a technical failure and the
controls that were missing; it does not assign personal blame.

---

## 1. Timeline

| When (Asia/Ho_Chi_Minh) | What |
|---|---|
| 2026-07-20 ~14:27 | While building the Workstream B parity fixture, `PEMS_FULL_V11_REMOVED_TTS_19_07_26.sql` was piped into `mysql` with the intended target `pems_i_refusal`. |
| 2026-07-20 ~14:27:57 | Every table in `pems_db` was dropped and recreated from the dump's seed. |
| 2026-07-20 ~14:28 | Detected: `SHOW TABLES` on `pems_i_refusal` returned nothing, and the dump was found to contain its own database-control statements. |
| 2026-07-20 ~14:29 | All further database work stopped and the incident was reported to the owner before any other action. |
| 2026-07-20 (same session) | Owner confirmed no irreproducible data and declined PITR. |

## 2. Intended vs actual target

- **Intended:** `pems_i_refusal` (disposable, on the exact Phase I allowlist).
- **Actual:** `pems_db` (protected).
- **Result:** `pems_i_refusal` was created and left **empty**; `pems_db` was replaced.

## 3. The hazardous statements

`PEMS_FULL_V11_REMOVED_TTS_19_07_26.sql`
(SHA-256 `f10460a8f4fc8dc79d3625716c8f34d6c220b516c5efb2a94dda853e29714a69`, 1 325 734 bytes,
432 top-level statements) contains, at top level:

```sql
-- line 235
DROP DATABASE IF EXISTS pems_db;
-- line 237
CREATE DATABASE IF NOT EXISTS pems_db CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
-- line 240
USE pems_db;
```

plus two further `USE pems_db;` statements (around lines 4194 and 7711).

The database named on the mysql command line is only the session **default**. `USE` re-points the
session, so everything after line 240 executed against `pems_db` regardless of the target
requested. The leading `DROP DATABASE` means this was a replacement, not a merge.

## 4. Scope and data-loss assessment

| Database | Outcome |
|---|---|
| `pems_db` | **Overwritten** — dropped and recreated at the dump's master-seed state (76 tables, all `CREATE_TIME 2026-07-20 14:27:57`). |
| `pems_i_refusal` | Created, left **empty** (it was the intended target). **Still present** — see below. |
| `pems_pr3_test` | **Untouched.** |
| `pems_test` | **Absent** on this machine — did not exist before or after. |
| `pems_it_regression` | Unrelated to the incident; created and dropped by the C1/C2 integration fixture. |

**Owner decision:** `pems_db` held no manually entered or otherwise irreproducible data. Its
current master-seed state is accepted. **No PITR was performed.**

Binary logging is `ON` (`TCANH1209-bin.*`, through `.000019`), so point-in-time recovery was
technically available and was deliberately not used. The binlogs are **retained**: not replayed,
not purged, no `RESET MASTER`.

`pems_db` remains a **protected** database for all subsequent sessions.

## 5. Root cause — control gaps, not a single mistake

The proximate trigger was importing a dump without inspecting its header. That alone does not
explain why one oversight reached a protected database, and fixing only that would leave the
system just as fragile. The real gaps:

1. **The target was assumed, not enforced.** The runner treated `-DbName` as the final target. It
   is only a default that the payload can override.
2. **The allowlist guarded the wrong thing.** An exact-match disposable allowlist constrains which
   database the *client connects to*. A `USE` inside the payload defeats it entirely.
3. **Raw dumps were piped into `mysql` unexamined.** No statement-aware scan existed, so no control
   could have noticed `DROP DATABASE pems_db`.
4. **The credential was over-privileged.** Drills ran as `root`, which can write to every protected
   schema. With a restricted account the same mistake would have failed harmlessly.
5. **Disposable drills shared a server with protected data.** No isolation boundary existed.
6. **No regression test covered this class.** Nothing asserted that a hostile payload is rejected
   *before* a mysql process is spawned, so the gap was invisible until it fired.

## 6. Corrective and preventive actions

| # | Action | State | Evidence |
|---|---|---|---|
| C1 | Statement-aware safety guard: real tokenizer (comments, strings, backticks, `DELIMITER`, versioned comments), fail-closed on unclassifiable input | **DONE** | `lib/SqlSafetyGuard.ps1` |
| C2 | Guard rejects database-control, admin/server, mysql-client (`SOURCE`, `\.`) statements and fully-qualified protected references | **DONE** | tests A–D, F, G |
| C3 | Safe importer runs the guard **before** spawning mysql | **DONE** | `import_disposable_fixture.ps1` |
| C4 | Regression proof that mysql is never invoked for the incident payload (fake-mysql spy) | **DONE** | tests J1, J2, L9 |
| C5 | Raw importer never silently strips `USE`/`CREATE DATABASE`; transformation is explicit, asserted, hashed, reproducible, atomic | **DONE** | tests L1–L8 |
| C6 | The real master dump is rejected for direct import | **DONE** | tests K1, K2 |
| C7 | TOCTOU closed: the scanned bytes are imported, not a re-read of the path | **DONE** | tests I3, I4 |
| C8 | Credential privilege classification; refuses root/global/protected grants | **DONE (code)** | `import_disposable_fixture.ps1` §4 |
| C9 | Restricted drill account provisioned | **BLOCKED — owner action** | `restricted_drill_user.sql` (never run by tooling) |
| C10 | `SELECT DATABASE()` asserted before **and** after the payload, in-session | **DONE** | `import_disposable_fixture.ps1` §6, §8 |
| C11 | Protected-schema fingerprints captured read-only and re-checked after import | **DONE** | `import_disposable_fixture.ps1` §5, §9 |
| C12 | Isolated MySQL instance for drills | **NOT DONE** | requires infrastructure the agent may not change |

C10 and C11 are backstops, not the guard: once a session exists it is already too late to discover
a hostile payload. C1–C3 are what actually prevent a recurrence.

## 7. Consequence for the remaining Phase I work

Per the safety gate, destructive MySQL drills stay **BLOCKED** until C9 (a restricted drill
credential) or C12 (an isolated instance) exists. The only credential available on this machine is
`root`, and the agent must not create users or grant privileges on a shared server.

Work that needs no database — the R6 occurrence appendix, F5 static manifest work, code and unit
tests — continues.

**Open cleanup item for the owner:** the empty `pems_i_refusal` database still exists. It was
deliberately **not** dropped. Dropping it means executing `DROP DATABASE` as `root` outside the
validated harness — the exact statement class the new guard refuses — and doing that by hand to
tidy up would contradict the control this incident produced. It holds no data and is on the
disposable allowlist, so leaving it is harmless. Drop it manually, or let the drill harness reclaim
it once a restricted account exists.

**Verified read-only after the fix:** the only login accounts on this server are `root@localhost`
and the three `mysql.*` internal accounts. No restricted drill account exists yet, which is why
C9 is the gating item.

## 8. Database touch ledger (this program, cumulative)

| Database | Touched | Detail |
|---|---|---|
| `pems_db` | **YES — incident** | Overwritten by master import 2026-07-20 14:27:57; owner-accepted; no recovery attempted. |
| `pems_test` | No | Absent on this machine. |
| `pems_pr3_test` | No | Untouched throughout. |
| `pems_i_refusal` | Yes | Created empty by the failed import; dropped. |
| `pems_i_upgrade`, `pems_i_rollback`, `pems_i_fresh` | Earlier sessions | Disposable drill targets; dropped after use. |
| `pems_it_regression` | Yes | Created/dropped per run by the C1/C2 integration fixture (EF `EnsureCreated`, never a migration script). |

## 9. Related evidence

- Guard + transformer suite: `phase_1_candidate/tests/Test-SqlSafetyGuard.ps1` — **50 passed, 0 failed**
  (local run at this HEAD; **not** a CI run).
- Master dump scan: `SAFE FOR DIRECT IMPORT = NO`, 5 `DATABASE_CONTROL` findings.
- `PEMS_FULL_V11_ACTOR_RELATION_SEED_FIXED_20_07_26.sql` was present untracked earlier in the
  session and was **removed by the owner** before it could be scanned. It was never imported,
  modified or committed. If it reappears, scan it read-only with `Test-SqlFileSafety` before any
  use; a sibling of the master dump should be assumed to carry the same `USE pems_db;` header.
