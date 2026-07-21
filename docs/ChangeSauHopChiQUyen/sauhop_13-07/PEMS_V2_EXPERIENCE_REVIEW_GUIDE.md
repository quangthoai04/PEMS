# PEMS Per-Campus Form V2 — Review environment guide

**Status:** `V2 EXPERIENCE READY — the isolated review database is built, all outbound integrations
are neutralised, and all six interactive UI groups are verified end-to-end through real Chromium:
entry-point cutover, the public multi-campus form with OTP, pending-edit/resubmit, amendment
(submit+approve), identity-claim (accept+decline), assignment+invitation, and student photo-upload.
Two real defects were found and fixed along the way (a CORS block and a documents.owner_type enum
mismatch). Read the caveats in §5c before treating any sub-case as exhaustively covered. Phase I
contract-drop remains NOT READY.`

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

**CORS:** the API only accepts browser calls from origins in `Cors:AllowedOrigins` (committed list:
`:3000/:3001/:3002/:5173`). A frontend on any other port — including the `:5273` used here — is
blocked, and the symptom is subtle: the page serves (HTTP 200) but every API call fails with a
Network Error and the app bounces to `/login`. `Start-ReviewApi.ps1` adds the review frontend origin
via `Cors__AllowedOrigins__4` (its `-FrontendOrigin` parameter, default `http://localhost:5273`) — a
process env override, not an `appsettings.json` edit. Run the frontend on `:5173` (already allowed)
or keep the launcher's `:5273` default; if you pick another port, pass it to `-FrontendOrigin`.

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

> **`sent_emails.status` is NOT proof of a real send when SMTP is off.** With `Smtp:Enabled=false`,
> `EmailService` logs the message (`[EmailService-DEV] To:…`) and returns, but some handlers still
> record the `sent_emails` row as `SENT`. So a `SENT` row can mean "logged, not dispatched." The
> authoritative signal for an actual outbound is the API log line **`Sent email to …`** (the real
> SMTP path) versus **`[EmailService-DEV] To:…`** (logged only). Grep the log, don't trust the DB
> status column, when auditing whether mail left the machine.

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

### 5c. Interactive UI journeys (2026-07-20, real Chromium → review API → MySQL)

These were driven through the actual browser UI (login form, detail/edit/amendment screens, claim
page) and confirmed in the database. Preconditions that are not themselves the journey under test
(creating a request, a leader's approve/reject) were set up via the authenticated API; the journey's
own action was always performed in the DOM.

Preconditions that are not themselves the journey under test (creating a request, a leader's
approve/reject/host-assign, advancing a visit stage) were set up via the authenticated API; the
journey's own action was always performed in the DOM.

| Group | Result | Evidence |
|---|---|---|
| Login form (INTERNAL) | **PASS** | real form → `/api/auth/login` 200 → token stored → dashboard reachable |
| **Entry-point cutover** | **PASS** | Home, FAQ (exact CTA) and Partners "Đăng ký tham quan" all navigate to `/visit-registration/v2` when v2 is enabled — the FAQ/Partners bug is fixed |
| **Public V2 form + OTP** | **PASS** | full form driven: registrant+contact, HN+HCM with distinct data, add-campus keeps campus-0 data intact, submit → **OtpVerificationModal** → OTP **logged, not sent** → verify. DB (req 9031): `fsv=2`, `has_mixed=1`, **2 instances / 2 details**, HN≠HCM matching the UI |
| **1 · Pending edit** | **PASS** | owner `pending-edit` → edits HN delegation → PUT 200. DB: HN `PE_HN_EDITED` `form_revision 1→2`; HCM untouched — per-campus copy-on-write |
| **1 · Resubmit** | **PASS** | leader rejects → owner `resubmit` → POST 200 |
| **3 · Identity claim** | **PASS** | claim emails **logged, not sent**. **Decline** (req 9032, authed invited contact) → `DECLINED`, access stays `PENDING_CONFIRMATION`, request not cancelled. **Accept** (req 9033) → `APPLIED`, contact linked (`new_user_id`) + `ACTIVE`. ⚠️ see caveat below |
| **4 · Amendment + approval** | **PASS** | owner submits amendment via modal → POST 200; leader approves via UI → POST 200. DB: canonical `AM_HN_AMENDED`, `APPROVED`, `approval_revision` bumped |
| **6 · Public OTP** | **PASS** | covered by "Public V2 form + OTP" above — the OTP modal is driven end to end in the browser (initiate → logged code → verify → create) |
| **2 · Assignment + invitation** | **PASS** | leader approves + self-hosts (instance `ASSIGNED`); host invites a **student** → student accepts via the invitation UI → participant `ACCEPTED`. Scope: an **HCM student gets 404** on the HN invitation (sibling isolation) |
| **5 · Photo upload** | **PASS (after a fix)** | instance advanced to `AFTER_VISIT`; the accepted **student** uploads a photo via the contribution UI → 200; `documents` row created (`VISIT_INSTANCE_MEDIA`, `PUBLISHED`, `created_by`=student). Storage stayed local (Drive off). **This surfaced a real bug — see below** |

### Two real defects found and fixed

1. **CORS** — the review frontend origin `:5273` was not in `Cors:AllowedOrigins`, so the browser
   could not talk to the API (page served 200 but every call failed and bounced to `/login`). Fixed
   in `Start-ReviewApi.ps1` via a process env override (see §3). No product code involved.
2. **`documents.owner_type` enum** — photo upload wrote `VISIT_INSTANCE_MEDIA`, a value the master
   schema's enum never contained, so every upload failed with MySQL 1265. Fixed by extending the enum
   (idempotent patch + master + fresh-target) with a regression test that reads the SQL and asserts
   the enum covers every value the code writes.

### Caveats — do not over-read these as exhaustive

- **Claim accept needs a real Google SSO in production.** The backend requires an authenticated
  session whose email equals the invited email; the UI's accept CTA is "Đăng nhập bằng Google". The
  review has no real Google accounts, so the invited contacts were provisioned directly (simulating
  the account Google-SSO would create on first login) and driven with a session token. The
  accept/decline *mechanism* is faithfully exercised; the Google login step itself is not, and cannot
  be, in this review.
- **Invite/decline variants.** Staff and Department invites use the same endpoint/mechanism as the
  Student invite that was driven; the invitation *decline* path was not each individually driven.
- **Photo validation.** The happy-path upload was driven; the image-only/5 MB *rejection* cases are
  enforced by the accept attribute + client validation (and covered by the frontend gates), not
  re-driven in the browser here.
- **Phone format** — the backend accepts national-format phones the frontend zod rejects (see §6);
  UI-created requests always use `+84…`, so the review used that format.

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

3. **Phone-format asymmetry (observation, NEEDS-BUSINESS-DECISION).** The backend create validator
   accepts any phone up to 50 chars (`OperationalContact.Phone`, `Registrant.Phone`), while the
   frontend V2 schema validates with `libphonenumber-js`'s `isValidPhoneNumber(value)` **without a
   country**, which requires international `+84…` format. A request created through the real UI is
   always frontend-validated, so it round-trips fine; but a request that gets a national-format phone
   (`0900000000`) into the database by any other path — API, import, seed — **cannot be saved by the
   V2 edit form**, because the hydrated value fails re-validation. This is pre-existing (v1 uses the
   same schema) and low-severity, but whether the backend should enforce the same format is a product
   decision, not one to self-select. Not changed here.

Deferred and unchanged by this environment: R6 per-occurrence disposition (F1), exact manifest
depth (F5), deterministic fresh target (F7), and the Phase I contract-drop itself.

---

## 7. Rules that still apply

- Never point the review stack at `pems_db`, `pems_test` or `pems_pr3_test`.
- Never pipe a raw dump into `mysql`. Use `Build-ReviewDatabase.ps1` / the safe importer.
- Do not run the Phase I destructive runner against `pems_review_v2`; it has its own exact
  four-name allowlist and the review database is deliberately not in it.
- Do not commit passwords, connection strings, or generated transform artifacts.
