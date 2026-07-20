# PEMS Per-Campus Form V2 — Review environment guide

**Status:** `V2 EXPERIENCE READY (core journeys) — the isolated review database is built, all outbound
integrations are neutralised, and the core create/read/authorization/report journeys are verified
end-to-end on the real stack. Interactive UI journeys for edit/amendment/transfer/photo remain for
hands-on owner review.`

Phase I contract-drop remains **NOT READY** and is not part of this environment. The review
database keeps all ten legacy columns; nothing here drops a column.

> **Two setup steps need root, done once by the owner** (see §1): the database bootstrap, and a
> one-line server setting the master's validation triggers require —
> `SET PERSIST log_bin_trust_function_creators = 1;`. After the triggers are imported you may set
> it back to `0`.

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

Re-run the build with `-Reset`, which drops every table **inside** `pems_review_v2` (never
`DROP DATABASE`, so it stays within the schema the restricted account owns) and re-imports:

```powershell
.\Build-ReviewDatabase.ps1 -Reset
```

After a reset, re-apply the review-only data steps the journeys rely on: set a known password on the
seed users and set the `api_configurations` rows to `DISABLED` (both are plain `UPDATE`s inside the
review database — the outbound neutralisation is review data, not production config).

---

## 3. Run the stack with V2 flags on — and every outbound integration OFF

**Do not just point `dotnet run` at the review database.** A review database holds
production-shaped seed data, so the reminder background job fires against real email addresses, and
`appsettings.json` ships `Smtp:Enabled=true` with live Gmail credentials. The first review start-up
did exactly this and sent one real email before it was caught. Choosing the database and choosing
what the process may talk to are two separate decisions.

Use the launcher, which sets the V2 flags and pins **every** outbound switch off, all as process
environment variables — it never edits `appsettings.json`, never removes a credential, and never
changes production behaviour:

```powershell
$env:MYSQL_BIN      = 'C:\Program Files\MySQL\MySQL Server 8.0\bin\mysql.exe'
$env:MYSQL_USER     = 'pems_review'
$env:MYSQL_PASSWORD = '<the password you chose>'

cd docs\database\scripts\review_env
.\Start-ReviewApi.ps1                 # API on http://localhost:5299
```

```powershell
# Frontend (separate terminal), pointed at the review API
cd frontend\pems-react
$env:VITE_API_BASE_URL = 'http://localhost:5299/api'
npx vite --port 5273 --strictPort
```

The launcher disables: SMTP (`Smtp__Enabled=false` — the mail pipeline still runs and *logs*
messages, so email-driven journeys stay reviewable, nothing leaves the machine), Google Drive
storage (`GoogleDrive__Enabled=false`, `Storage__Provider=Local`), Turnstile, FeID SSO, and the
Document AI OCR default code. Google Translate and Document AI credentials live in the
`api_configurations` table, not in appsettings; every such row in the review database is set to
`DISABLED`.

**Verify no outbound before reviewing.** With the API running, its only external socket should be
MySQL on `localhost:3306`:

```powershell
netstat -ano | findstr <api-pid>     # expect only :5299 listen + :3306 to localhost
```

Confirm the flags reached the API:

```
GET /api/public/features/per-campus-form-v2   ->   {"readEnabled":true,"writeEnabled":true,"enabled":true}
```

`enabled` is the AND of read and write. Write ON with read OFF is an invalid combination the
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

### 5a. Verified end-to-end on the real review stack (2026-07-20)

These ran against the built `pems_review_v2` through the review API and, where noted, real
Chromium. Requests were created via the authenticated create-v2 endpoint and the results checked in
the database and through the read API.

| Journey | Result | Evidence |
|---|---|---|
| Public V2 form renders | **PASS** | real Chromium loads `/visit-registration/v2`, 50 form controls, consumes `/api/public/features/per-campus-form-v2` |
| Create V2 **single-campus** | **PASS** | req 9012 → `form_schema_version=2`, `has_mixed=0`, one detail row |
| Create V2 **multi-campus** | **PASS** | req 9013 → two detail rows |
| Create V2 **mixed** | **PASS** | req 9014 → HN=`CAMPUS_TOUR`, HCM=`WORKSHOP` as two distinct canonical rows |
| `has_mixed` derivation | **PASS** | genuinely-identical content → `has_mixed=0` (req 9015); any per-campus difference → `has_mixed=1`. Content-sensitive, not scope-sensitive |
| Owner reads mixed detail | **PASS** | both `MIXED_HN_DELEGATION` and `MIXED_HCM_DELEGATION` present, each on its own campus |
| Staff Leader **HN** scope | **PASS** | sees only the HN campus of the mixed request; HCM sibling absent; `canViewAllCampuses=false` |
| Staff Leader **HCM** scope | **PASS** | mirror — sees only HCM |
| Unrelated-campus leader | **PASS** | Da Nang leader gets **403** on the request |
| **Canonical report filter** | **PASS** | HO overview `visitType=WORKSHOP` surfaces req 9014 (whose only WORKSHOP is the HCM *canonical* detail; its projection is `CAMPUS_TOUR`) and excludes 9013; `MEETING` is the mirror. Proves the report filters on the canonical detail, never the compatibility projection — the core C1/C2 invariant, live |
| Mixed request-level label | **PASS** | req 9014 shows `"Khác nhau theo cơ sở"` at request level |
| Identity A **=** contact | **PASS** | req 9012 → `PRIMARY_CONTACT_ACCESS = ACTIVE`, owner user set |
| Identity A **≠** contact | **PASS** | req 9016 → `PENDING_CONFIRMATION` + an `INITIAL_CLAIM` / `PRIMARY_CONTACT` identity-change row |
| 10 legacy columns retained | **PASS** | no contract-drop; V1 compatibility intact |
| No outbound traffic | **PASS** | API's only external socket is MySQL on localhost; zero SMTP/HTTP egress after the launcher |

No product defect was found. One journey initially failed on a wrong test assertion (a Staff Leader
was expected to be *denied*, but the documented contract scopes them to their own campus); corrected
against `PerCampusFormV2ReadTests` §4 and re-verified.

### 5b. Remaining for hands-on interactive UI review

These need a person clicking through the UI (or a longer browser-automation pass) and were **not**
executed this session:

- multi-campus **form editing** in the browser: add/remove a campus, "same for all" copy, per-campus
  guest/support lists not sharing mutable state;
- **pending-edit / resubmit** hydration per campus through the UI;
- **host / department / student** assignment and invitation screens;
- **contact transfer** and **amendment** UI (the underlying claim/amendment tables are exercised by
  create and by the unit suites, but the transfer/amendment *screens* were not driven here);
- **student photo upload** in the browser (the image-only contract is covered by the frontend gates;
  it was not re-driven end to end here);
- public **OTP** initiate/verify in the browser (create-v2 was exercised via the authenticated
  endpoint; the OTP path with SMTP disabled logs the code rather than emailing it — read it from the
  API log to complete the flow).

---

## 6. Known limitations and open decisions

Also verified independently of this environment, on real MySQL/Pomelo:

- uniform **and** mixed v2 report/invoice reads source the canonical per-campus detail, never the
  compatibility projection (20/20 regressions; reverting either fix makes them fail) — now also
  confirmed live in §5a;
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
