# PEMS Per-Campus Form V2 — Review environment guide

**Status:** `IN PROGRESS — review code/fixtures prepared; real-stack review BLOCKED pending
restricted database bootstrap.`

Phase I contract-drop remains **NOT READY** and is not part of this environment. The review
database keeps all ten legacy columns; nothing here drops a column.

---

## 1. What you need to do first (one manual step)

Everything else is automated. This step is not, on purpose: it creates a database and grants
privileges, which is the exact statement class the import guard refuses, so no tooling performs it.

```bash
mysql -uroot -p < docs/database/scripts/review_env/bootstrap_review_db.sql
```

Before running it, open the file and replace `CHANGE_ME_BEFORE_RUNNING` with a password that is not
reused from any application account. Do not commit the value.

Then verify the account is genuinely restricted:

```sql
SHOW GRANTS FOR 'pems_review'@'localhost';
```

Expect a `USAGE` line plus one grant on `` `pems_review_v2`.* `` — and **no** mention of `pems_db`,
`pems_test` or `pems_pr3_test`, no `WITH GRANT OPTION`, no `SUPER`.

### Why a separate database

On 2026-07-20 a master dump imported with an intended target of a disposable database instead
overwrote the protected `pems_db`: the dump carried its own `DROP DATABASE` / `CREATE DATABASE` /
`USE pems_db`, and `USE` re-pointed the session. See
[`PHASE_I_DB_IMPORT_INCIDENT_2026_07_20.md`](./PHASE_I_DB_IMPORT_INCIDENT_2026_07_20.md).

So review does **not** run on `pems_db`, and does not run on the Phase I drill databases either —
those get dropped and rebuilt by destructive migrations, which would wipe review data mid-session.

---

## 2. Build the review database

```powershell
$env:MYSQL_BIN      = 'C:\Program Files\MySQL\MySQL Server 8.0\bin\mysql.exe'
$env:MYSQL_USER     = 'pems_review'
$env:MYSQL_PASSWORD = '<the password you chose>'

cd docs\database\scripts\review_env
.\Build-ReviewDatabase.ps1 -ScanOnly   # validates the payload, touches nothing
.\Build-ReviewDatabase.ps1             # performs the import
```

The master cannot be imported directly and the guard will say so. `Build-ReviewDatabase.ps1` runs
it through the asserted transformer, which removes exactly the five database-control statements
into a **new** artifact and re-runs the same guard on its own output:

| | |
|---|---|
| source | `PEMS_FULL_V11_REMOVED_TTS_19_07_26.sql` |
| source SHA-256 | `f10460a8f4fc8dc79d3625716c8f34d6c220b516c5efb2a94dda853e29714a69` |
| source statements | 432 |
| removed | 5 (`DROP DATABASE`, `CREATE DATABASE`, 3 × `USE pems_db`) |
| output statements | 427 |
| output SHA-256 | `b0018c7257338f24751babe8326cd49aafbb9ec6177526e9daf72e8be16dd2a9` |

The master already contains both the v1 compatibility columns and the v2 additive tables
(`visit_request_campuses`, `visit_instance_form_details`, `visit_request_pending_forms`, the
identity-change and amendment tables), so the `percampus_v2_migration` chain is **not** replayed —
that chain is for upgrading an older v1 database, not for a fresh one.

### To reset review data

```sql
DROP DATABASE `pems_review_v2`;
```

then re-run the bootstrap and `Build-ReviewDatabase.ps1`.

---

## 3. Run the stack with V2 flags on

The flags are `PerCampusFormV2:Enabled` (read) and `PerCampusFormV2Write:Enabled` (write). Both
default to **false** in code (`PerCampusFormV2Options`, `PerCampusFormV2WriteOptions`) and are not
set in any committed `appsettings*.json`. Turn them on **per process only** — never by editing a
committed config file:

```powershell
# Backend
$env:ConnectionStrings__DefaultConnection = 'server=localhost;port=3306;database=pems_review_v2;user=pems_review;Password=<pw>;AllowUserVariables=True;GuidFormat=None'
$env:PerCampusFormV2__Enabled      = 'true'
$env:PerCampusFormV2Write__Enabled = 'true'
dotnet run --project backend\PEMS.Api
```

```powershell
# Frontend (separate terminal)
cd frontend\pems-react
npm run dev
```

Confirm the flags actually reached the API before reviewing anything:

```
GET /api/public/features/per-campus-form-v2
```

`enabled` is the AND of read and write. Write ON with read OFF is an invalid combination that the
create-v2 handler rejects by design — it would create records no read path can surface.

---

## 4. Personas (from the master seed)

Passwords are **not** recorded here. Use the seed credentials you already hold, or reset them
directly in `pems_review_v2`.

| Role | Email | Campus | Notes |
|---|---|---|---|
| HO | `ho@fpt.edu.vn` | — | Head Office overview/reports |
| Staff Leader | `staff.leader.hn@fpt.edu.vn` | HN (1) | approves the HN instance only |
| Staff Leader | `staff.leader.hcm@fpt.edu.vn` | HCM (2) | approves the HCM instance only |
| Staff | `staff.hn@fpt.edu.vn` | HN | host candidate |
| Dept Leader | `dept.leader.hn@fpt.edu.vn` | HN | department task surfaces |
| Dept Staff | `dept.hn@fpt.edu.vn` | HN | department task surfaces |
| Dept Leader | `facilities.leader.hn@fpt.edu.vn` | HN | logistics/facilities |
| Student | `student@fpt.edu.vn` | HN | contribution / photo upload |
| Student | `student.hcm@fpt.edu.vn` | HCM | second-campus student |
| Visitor | `visitor@example.com` | — | external registrant |
| Visitor | `lee.joonho@seoultech.example` | — | second registrant, for A ≠ B |

Campuses seeded: HN(1), HCM(2), DN(3), CT(4), QN(5).

---

## 5. Scenario data — read this before you judge coverage

**The master seed contains only v1 requests.** Its `INSERT INTO visit_requests` does not include
`form_schema_version`, so every seeded row defaults to 1. That is useful — it is the V1
compatibility baseline — but it means **no v2 scenario exists until you create one**.

The v2 scenarios are created by running the real journeys with the flags on, not by seeding SQL.
That is deliberate: hand-written rows can encode states the business flow cannot actually reach,
which makes a review look healthier than the product is.

Suggested order, each building on the last:

| # | Journey | What to check |
|---|---|---|
| 1 | Public V2 form, **single campus**, complete OTP | request is created with `form_schema_version = 2` and one `visit_instance_form_details` row |
| 2 | Public V2 form, **multi-campus uniform** (same content per campus) | one detail row per campus; `has_mixed_campus_details = 0` |
| 3 | Public V2 form, **multi-campus mixed** (different delegation name / purpose per campus) | each campus keeps its own values; adding/removing a campus must not copy or lose another campus's data |
| 4 | Registrant A **=** contact A | no identity claim is raised |
| 5 | Registrant A **≠** contact B | claim flow appears and behaves as designed |
| 6 | Visitor opens detail, pending-edit, resubmit | each campus hydrates with its own values |
| 7 | Staff Leader **HN** logs in | sees and acts on the HN instance only; the HCM sibling is not visible |
| 8 | Staff Leader **HCM** logs in | mirror of 7; approving HN must not approve HCM |
| 9 | Host / department / student assignment or invitation | per the permission matrix |
| 10 | Amendment / contact transfer | history and revision surfaces |
| 11 | Reports and per-campus email surfaces | each campus shows its own delegation name, not the smallest-campus projection |
| 12 | Student photo upload | JPG/JPEG/PNG/WEBP only, 5 MB max, messages say `ảnh` |

Negative checks worth running alongside: a user from another campus cannot read or write your
instance; duplicate submit does not create a second request; a stale row version is rejected; with
both flags OFF the V1 behaviour is unchanged; and no raw technical exception text reaches the UI.

---

## 6. Known limitations and open decisions

**Not yet executed by anyone.** The journeys in §5 are specified, not verified — the review
database has not been built because the bootstrap in §1 is an owner action. Treat the table above
as a checklist to run, not as a passed matrix.

Already verified independently of this environment, on real MySQL/Pomelo:

- uniform **and** mixed v2 report/invoice reads source the canonical per-campus detail, never the
  compatibility projection (20/20 regressions; reverting either fix makes them fail);
- the photo upload contract is image-only end to end.

Open business decisions — **not** self-selected, and they will surface during §5:

1. **Mixed request-level display.** A mixed v2 request has no single delegation name. Request-level
   rows currently show `"Khác nhau theo cơ sở"`. Uniform v2 request-level rows still read the
   compatibility projection; changing that is safe and deterministic, but the mixed case needs your
   rule for email and report surfaces.
2. **F10 search contract.** `RegistrantFullName` / `Nationality` / `JobTitle` are searchable but
   produce no matched-context code, so a row can match with nothing to show for it. Options are to
   keep them searchable and add field codes, or drop them from the keyword predicate. There is a
   privacy trade-off either way; it is not being decided by guesswork.

Deferred and unchanged by this environment: R6 per-occurrence disposition (F1), exact manifest
depth (F5), deterministic fresh target (F7), and the Phase I contract-drop itself.

---

## 7. Rules that still apply

- Never point the review stack at `pems_db`, `pems_test` or `pems_pr3_test`.
- Never pipe a raw dump into `mysql`. Use `Build-ReviewDatabase.ps1` / the safe importer.
- Do not run the Phase I destructive runner against `pems_review_v2`; it has its own exact
  four-name allowlist and the review database is deliberately not in it.
- Do not commit passwords, connection strings, or generated transform artifacts.
