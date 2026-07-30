---
type: verification-report
status: draft
updated: 2026-07-29
links:
  - docs/email-standardization/02-decisions-and-contracts.md
  - docs/email-standardization/04-requirement-test-traceability.md
  - docs/email-standardization/05-final-verification-report.md
  - docs/email-standardization/06-deployment-readiness-runbook.md
  - docs/email-standardization/08-open-product-decisions.md
---

# G11 — Residual Technical Closure

Closes the two residual items from G9 that are technical rather than product decisions:

- **R-103** — a report/invoice send retried after a network timeout can produce a second outbound message.
- **R-106** — templates that use `{{actionBlock}}` without a registered action cannot be previewed.

`R-104` and `R-105` are **not** touched here. Both need a decision that is not the implementer's to make;
the evidence prepared for them lives in `08-open-product-decisions.md`.

---

## 1. G11-A — Audit of the six report/invoice send actions

Traced from route to frontend caller at HEAD `c39e6f04`, not from the earlier reports.

### 1.1 The six actions

All six are `POST` under `ReportsController` (`[Authorize]` at class level — no report endpoint is
anonymous). Every one of them ends at the same choke point, `IReportEmailSender.SendAsync`.

| # | Action | Route | Actor gate | Scope resolved by backend | Template | Attachment | Frontend caller | Retry behaviour at HEAD |
|---|---|---|---|---|---|---|---|---|
| 1 | `SendHoCampusReport` | `POST api/reports/ho-report-v2/send-campus-report` | `[RoleAuthorize(Ho)]` + in-handler `HoReportV2Guard.RequireHo` | campus from body, recipient = that campus's Staff Leader | `REPORT_CAMPUS_OPERATION` | PDF | `HoReportManagement.tsx:168` | none — a retry sends again |
| 2 | `SendStaffLeaderPersonnelReport` | `POST api/reports/staff-leader-report-v2/send-personnel-report` | `[RoleAuthorize(StaffLeader)]` + `StaffLeaderReportV2Guard.RequireStaffLeaderCampus` | campus from claims; user must be in it | `REPORT_PERSONAL_PERFORMANCE` | PDF | `StaffLeaderReportManagement.tsx:270` | none |
| 3 | `SendStaffLeaderDepartmentReport` | `POST api/reports/staff-leader-report-v2/send-department-report` | same | campus from claims; department must be in it | `REPORT_DEPARTMENT_COORDINATION` | PDF | `StaffLeaderReportManagement.tsx:306` | none |
| 4 | `SendStaffLeaderDeptInvoice` | `POST api/reports/staff-leader-report-v2/departments/{departmentId}/send-invoice` | same | campus from claims; department from route; recipient = department head | `REPORT_DEPARTMENT_INVOICE` | PDF | **none** | none |
| 5 | `SendDeptLeaderPersonnelReport` | `POST api/reports/dept-leader-report-v2/send-personnel-report` | `[RoleAuthorize(DepartmentLead)]` + dept-leader scope guard | department from claims; user must be in it | `REPORT_PERSONAL_PERFORMANCE` | PDF | `DeptReportManagement.tsx:264` | none |
| 6 | `SendDeptLeaderInvoiceToStaffLeader` | `POST api/reports/dept-leader-report-v2/send-invoice` | same | department from claims; recipient = campus Staff Leader | `REPORT_DEPARTMENT_INVOICE` | PDF | **none** | none |

In no case does the request name its recipient. Every addressee is resolved by the handler from the
caller's own claims plus a scope check — that property is unchanged by G11 and is re-asserted by test.

### 1.2 The two routes with no UI

`sendStaffLeaderDeptInvoice` and `sendDeptLeaderInvoiceToStaffLeader` are **defined** in
`frontend/pems-react/src/features/reports/api/reportsApi.ts` (lines 238 and 279) and **called from
nowhere**. Measured across the whole of `frontend/pems-react/src` — the only occurrences of either name
are the definitions themselves. There is no button, menu item or route that reaches them.

G11 does not add one. They keep their contract and are covered by direct route tests.

### 1.3 Ordering inside one send

`ReportEmailSender.SendAsync` (the shared step, `Reports/Common/ReportEmailSender.cs`):

```
1  validate attachment file name + PDF signature      ← can refuse, nothing written
2  store PDF bytes → files row                        ← SaveChanges #1
3  dispatcher.PrepareAsync → sent_emails + recipient  ← SaveChanges #2 (status QUEUED)
4  sent_email_attachments row                         ← same SaveChanges #2
5  dispatcher.DeliverAsync → SMTP                     ← the outbound call
6  write back SENT / FAILED / QUEUED                  ← SaveChanges #3
7  throw if status != Sent                            ← Mandatory contract
```

- **No transaction is opened** anywhere in the six handlers or in the sender. Three independent
  `SaveChangesAsync` calls, and SMTP is called between the second and the third. That is deliberate — the
  dispatcher's own documentation says the reverse order would be worse — but it means there is a real
  crash window at step 5→6: the provider can accept a message whose `sent_emails` row is still `QUEUED`.
- Steps 2–4 write **before** delivery, so a second logical attempt produces a second `files` row, a second
  `sent_emails` row, a second `sent_email_attachments` row and a second MIME message. Nothing dedupes them.
- The failure path at step 3 removes the `files` row it just wrote; the blob is left unreferenced because
  the storage contract has no delete.

### 1.4 Existing idempotency infrastructure

| Candidate | Verdict |
|---|---|
| `backend/PEMS.Api/Filters/IdempotencyFilter.cs` | **Empty stub.** `namespace PEMS.Shared; public class IdempotencyFilter { }` — no members, no references anywhere in `backend/` or `tests/`, and not even in the assembly's own namespace. Not reused. |
| `backend/PEMS.Application/Common/Interfaces/IIdempotencyService.cs` | **Empty stub**, zero members, zero references. Not reused. |
| `backend/PEMS.Infrastructure/Idempotency/IdempotencyService.cs` | **Empty stub** in `namespace PEMS.Shared`, zero references. Not reused. |
| `tests/PEMS.IntegrationTests/Api/IdempotencyBehaviourTests.cs` | **Zero bytes.** An empty file, not a test. |
| `visit_request_fingerprint_guards` + `VisitRequestFingerprintGuard` | Real and working, but it is UC-17's *duplicate-submission* guard keyed on a business fingerprint of a visit request. Wrong scope and wrong key semantics for an email send. Its **technique** is reused (INSERT IGNORE, then `SELECT … FOR UPDATE` on the row) because that is how this repository already does database-level serialisation. |
| `IBusinessCardOcrThrottle` | In-memory, per-process, time-window throttle. Not persistent, not a key contract. |
| Provider-specific idempotency | None. `System.Net.Mail.SmtpClient` has no idempotency support; the pickup-directory path has none either. |

**Conclusion:** there is no reusable idempotency infrastructure. The three files named "Idempotency" are
name-only stubs, and reusing one because of its name is exactly what the brief forbids. G11 builds the
contract new, and leaves the stubs untouched (deleting them is out of scope and they are referenced by
nothing).

### 1.5 How a duplicate is produced today

- **Double-click:** already prevented in the UI. `useGuardedSend` keeps a per-row in-flight set read
  synchronously through a ref, so a second click in the same tick does nothing. This is a UI-session
  guard and its own documentation says so.
- **Network timeout:** not prevented. The browser gives up, `axios` rejects, the `finally` in
  `useGuardedSend` clears the row, the user presses "Gửi" again — and the server, which never saw the
  disconnect, runs the whole handler a second time. Two PDFs, two history rows, two emails.
- **Reload mid-send:** same shape. The in-flight set does not survive a reload.

That is R-103, stated precisely.

---

## 2. G11-B — The idempotency contract

### 2.1 Scope

Exactly the six send actions in §1.1. Not preview, not export/download, not security email, not the
scheduler, not invitations, not manual compose/reply. The behaviour activates on a marker interface, so a
command that does not declare itself idempotent is untouched — there is no "all POSTs" rule to leak.

### 2.2 Client contract

```http
Idempotency-Key: <opaque>
```

- The server treats the value as **opaque and case-sensitive**. It is never parsed for meaning.
- Accepted shape: 8–200 characters, printable US-ASCII excluding space (`0x21`–`0x7E`). This rejects
  empty, over-long, CR/LF and every other control character by construction.
- Missing → `EMAIL_IDEMPOTENCY_KEY_REQUIRED` (400). There is **no legacy path**: a send without a key is
  refused, not quietly sent.
- Malformed → `EMAIL_IDEMPOTENCY_KEY_INVALID` (400).
- The raw key is **never stored**. Only `SHA-256(key)` as lower-case hex.
- The key is not derived from recipient, period or payload, and no `(sender, target, period)` predicate
  exists anywhere in the implementation.

### 2.3 Request fingerprint

Canonical, ordered, and built from business meaning — never from raw JSON, whose property order is not
stable:

```
operation-code   LF
actor-user-id    LF
<field>=<value>  LF   … in a fixed order defined per command
```

Line items are sorted by id and each is rendered `id:unitPrice` with the price in invariant form after
`InvoiceMoney` normalisation, so `100` and `100.00` are the same request. Dates are rendered as
`yyyy-MM-dd` (the business granularity of a report period), `null` as an empty marker. The fingerprint
is `SHA-256` of that text.

The stored record holds the **hash only** — no note text, no recipient address, no monetary values, no
token, no credential.

Same key + different fingerprint → `IDEMPOTENCY_KEY_REUSED_WITH_DIFFERENT_REQUEST` (409), and nothing is
sent.

### 2.4 State machine

```
                 ┌────────────┐
   reserve ─────▶│  RESERVED  │
                 └─────┬──────┘
                       │ handler starts
                 ┌─────▼──────┐
                 │ PREPARING  │──── refusal before any outbound call ──▶ FAILED_BEFORE_DISPATCH
                 └─────┬──────┘                                           (retryable, same key)
                       │ immediately before SMTP
                 ┌─────▼──────┐
                 │DISPATCHING │──── provider accepted ──▶ SUCCEEDED
                 └─────┬──────┘
                       │ anything else
                       ▼
                 OUTCOME_UNKNOWN   (never auto-retried)
```

Replay semantics for a second request carrying the same key **and** the same fingerprint:

| Stored state | Response | Side effects |
|---|---|---|
| `SUCCEEDED` | the original success result, replayed verbatim | none — no PDF, no `files` row, no `sent_emails` row, no MIME |
| `RESERVED` / `PREPARING` / `DISPATCHING` | 409 `EMAIL_IDEMPOTENCY_IN_PROGRESS` | none |
| `FAILED_BEFORE_DISPATCH` | the handler runs again under the same key | a normal first attempt |
| `OUTCOME_UNKNOWN` | 409 `EMAIL_IDEMPOTENCY_OUTCOME_UNKNOWN` | none — the user must decide, with a new key |

**What counts as proof that nothing left the process.** `EmailService` returns `Skipped`, or `Failed`
with code `SMTP_DISABLED` / `SMTP_MISCONFIGURED`, only from the branch that runs *before* any socket is
opened. Those are `FAILED_BEFORE_DISPATCH`. Every other outcome after the `DISPATCHING` transition —
including `SMTP_SEND_FAILED`, which is an exception from `SmtpClient` that cannot distinguish "refused
before acceptance" from "accepted, acknowledgement lost" — is `OUTCOME_UNKNOWN`.

**This is not exactly-once delivery, and G11 does not claim it.** SMTP has no such guarantee and PEMS has
no delivery webhook. The property being made true is narrower and checkable:

> One logical user action does not, by itself, produce a second outbound attempt.

`sent_emails.status` is untouched by all of this: acceptance is still `SENT`, never `DELIVERED`.

### 2.5 Concurrency

Serialisation is done by the database, not by application code:

1. `INSERT IGNORE` the reservation row (the unique key is `actor + operation + key hash`).
2. `SELECT … FOR UPDATE` that row inside a short transaction.
3. Read the state, decide, write the transition, commit.

Two concurrent requests with the same key therefore queue on the row lock; the first becomes `RESERVED`
and the second reads `RESERVED` and is told in-progress. A unique-constraint violation on the insert is
an expected outcome, not a 500 — the row is re-read and the state machine applied.

The transaction covers only steps 1–3. It is committed and disposed **before** the handler runs, so no
database transaction is held open across PDF generation or across SMTP — consistent with how the
dispatcher already requires callers to behave.

### 2.6 Retention

No expiry, no purge, no reuse of a spent key, and no hosted cleanup service. A new logical send uses a
new key. Retention is written up as an operations policy in the runbook rather than implemented.

### 2.7 Authorization is unchanged

Idempotency is not authorization. `[RoleAuthorize]`, the in-handler scope guards and the backend's own
recipient resolution all run exactly as before, on the first request and on every replay. The actor is
part of the unique key and is read from the validated JWT — never from the payload — so one user's key
can never address another user's record, and a replay can only ever return the caller's own result.

---

## 3. G11-C — Database and schema

### 3.1 Reuse checked first

No existing table fits. `visit_request_fingerprint_guards` is UC-17's duplicate-submission guard, keyed
on a visit-request business fingerprint — wrong scope, wrong key semantics. Its *technique* is reused.

### 3.2 The table

`email_send_idempotency`, added to the canonical script and shipped as a standalone migration for
databases that already exist. Hashes and a result; never a copy of the request.

Two delete behaviours were chosen rather than defaulted:

- `actor_user_id` to `users` is **`ON DELETE RESTRICT`**. This table records what a person sent; deleting
  the person must not delete the evidence. PEMS hard-deletes no user anywhere in the backend, and nine
  other user foreign keys in the canonical schema already use RESTRICT.
- `sent_email_id` to `sent_emails` is **`ON DELETE SET NULL`**. If a history row is ever removed the
  reservation survives, because "a send happened" is the fact that must not be lost.

### 3.3 The package

`docs/database/scripts/email_dispatch_idempotency/` — `01_preflight.sql` (read-only), `02_up_additive.sql`
(guarded, `IF NOT EXISTS`), `03_verify.sql` (checks plus a `SIGNAL` gate), `04_rollback_guidance.md`.

The migration refuses to run unless the operator names the target on the same session, and **spends** the
confirmation on the way out so a pooled connection cannot carry authorisation into a second run.

### 3.4 Evidence

Two disposable databases, both dropped afterwards. `pems_db` was never connected to for writing.

| Step | Result |
|---|---|
| Fresh canonical import | 83 base tables · 32 triggers · 254 foreign keys · 30 templates · 22 historical `sent_emails` |
| Guard, no confirmation | refused, SQLSTATE 45000, table not created |
| Guard, wrong database named | refused, SQLSTATE 45000, table not created |
| Migration on a pre-G11 schema, run 1 | table created; verify **25 PASS / 0 FAIL / 3 INFO** |
| Migration run 2 | no-op; `SHOW CREATE TABLE` and index list byte-identical before and after |
| Verify after run 2 | 25 PASS / 0 FAIL |
| Verify gate, with a deliberately inconsistent row | **exit code 1**, naming the row that broke the invariant |
| Migrated schema vs fresh canonical | **identical** — every column, index, constraint and delete rule, with comments compared as raw bytes |
| G7 template sync on the new canonical, runs 1 and 2 | 0 inserted / 0 updated / 0 deactivated both times; verify 16 PASS / 0 FAIL both times |

**A charset defect was found and fixed during this step.** The migration's column comments are
Vietnamese and its `CHECK` constraint literals carry the creating connection's character set. Run through
the mysql CLI on Windows — which defaults to the console codepage — the comments landed as mojibake and
the constraint was recorded with `_cp850` literals. Caught by comparing raw bytes against a fresh
canonical import, not by reading the output: read back through the same mis-configured client, the
mangled text looks correct. `SET NAMES utf8mb4;` added, and asserted by test.

**The same defect existed in the G7 sync script, and was worse.** `02_sync_templates.sql` had no
`SET NAMES` either, so a CLI run rewrote **all thirty templates** as mojibake and reported 30 rows
updated on a database that was already converged. It survived G7 because the automated suite connects
through MySql.Data (already UTF-8), and because a before/after snapshot taken through the same client
compares mangled text to mangled text and finds no difference. Fixed in all three G7 scripts; re-measured
on a fresh import as a true 0/0/0 no-op with the content intact.
`Every_script_sets_its_connection_character_set` now asserts it.

### 3.5 Canonical SQL hash

Changed deliberately, from `18e97d4d…e286b8` to `b8213ee5…57c5a0`. Exactly three hunks, all this table:

1. `DROP TABLE IF EXISTS email_send_idempotency` in the reset list, before `sent_emails`;
2. the `CREATE TABLE`, after `account_email_confirmations`;
3. the file's own `merged_runtime_table_count` assertion, 81 to 83.

That third one was **already wrong before G11**: it read 81 while the script produced 82, and the file's
own header comment said 82 — so every import had been reporting a permanent `issue_count` of 1. Corrected
rather than left one further out of date. No seed row, no template, no trigger and no other table changed.
`DisposableDatabaseManager.ExpectedBaseTableCount` moved 82 to 83.

---

## 4. G11-D — Backend

| Layer | What lives there |
|---|---|
| Domain | `EmailSendIdempotency` entity |
| Application | `EmailSendStates`, `IIdempotentEmailSend`, `EmailSendOperations`, `IdempotencyKey`, `EmailSendFingerprint`, `IEmailSendReservationStore`, `EmailSendAttempt`, `EmailSendIdempotencyBehaviour` |
| Infrastructure | `EmailSendReservationStore` (persistence), `HttpIdempotencyKeyAccessor` (reads the header) |

The controller does not query the table and no handler repeats the logic — the behaviour is registered
once, after `ValidationBehaviour` so a malformed payload is refused before it spends a key.

**Prepare/deliver boundary.** `ReportEmailSender` marks `DISPATCHING` immediately before
`dispatcher.DeliverAsync`, and the transition is committed before the call. Everything above that line is
repeatable; nothing below it is. A refusal the email service decided *before* opening a socket withdraws
the dispatch claim (`EmailDeliveryCodes.ProvesNothingWasSent`), so a configuration problem does not
strand the user's key.

**One defect found by the tests and fixed at source.** `MarkFailedAsync` passed `DBNull.Value` as a
raw-SQL parameter; EF has no store type for it and throws. Every clean pre-dispatch failure would have
surfaced as a 500 that replaced the user's real business error. Replaced with `NULLIF` sentinels.

---

## 5. G11-E — Frontend

`useIdempotentSend` owns the key's lifetime; the four UI callers use it. The six `reportsApi` send
functions now take a **required** `idempotencyKey` — required, not optional, because an optional one is a
parameter somebody forgets, and forgetting it is the bug.

The rule is about intent, not timing:

| Outcome | Key |
|---|---|
| confirmed success | retired — the next click is a new send |
| refusal decided before anything was sent (4xx with a body) | retired |
| key itself rejected, or already spent on a different request | retired |
| timeout, offline, 5xx, 502 | **kept** |
| `EMAIL_IDEMPOTENCY_IN_PROGRESS`, `EMAIL_IDEMPOTENCY_OUTCOME_UNKNOWN` | **kept** |

Keys live in `sessionStorage`, so a reload mid-send does not become a second send; not `localStorage`,
because a key that outlives the tab would make tomorrow's deliberate re-send look like today's retry.

A dropped connection is **not** reported as "gửi thất bại" — that wording is what invites the second
click. It reads "Mất kết nối trước khi có kết quả. Bấm gửi lại để tiếp tục đúng lần gửi này."

---

## 6. G11-F — Real-stack evidence

Real backend, real renderer and dispatcher, real `EmailService` writing `.eml` files, disposable MySQL,
no email sent to anyone. Counts are of files and rows, not of mock invocations.

Per action — first send, replay under the same key, then a new key:

| Action | First send | Replay (same key) | New key | MIME files | `sent_emails` | attachments | `files` |
|---|---|---|---|---|---|---|---|
| `REPORT_HO_CAMPUS` | success | identical result, nothing new | new send | 1 → 1 → 2 | 1 → 1 → 2 | 1 → 1 | 1 → 1 |
| `REPORT_STAFF_LEADER_PERSONNEL` | success | identical | new send | 1 → 1 → 2 | 1 → 1 → 2 | 1 → 1 | 1 → 1 |
| `REPORT_STAFF_LEADER_DEPARTMENT` | success | identical | new send | 1 → 1 → 2 | 1 → 1 → 2 | 1 → 1 | 1 → 1 |
| `INVOICE_STAFF_LEADER_DEPARTMENT` | success | identical | new send | 1 → 1 → 2 | 1 → 1 → 2 | 1 → 1 | 1 → 1 |
| `REPORT_DEPT_LEADER_PERSONNEL` | success | identical | new send | 1 → 1 → 2 | 1 → 1 → 2 | 1 → 1 | 1 → 1 |
| `INVOICE_DEPT_LEADER_TO_STAFF_LEADER` | success | identical | new send | 1 → 1 → 2 | 1 → 1 → 2 | 1 → 1 | 1 → 1 |

All six also refuse a keyless request with `EMAIL_IDEMPOTENCY_KEY_REQUIRED` and send nothing, and all six
refuse the same key carrying a different request with `IDEMPOTENCY_KEY_REUSED_WITH_DIFFERENT_REQUEST`.

The two routes with no UI are additionally covered over **real HTTP** (`ReportInvoiceRouteTests`):
keyless gives 400, a replay gives one message and an identical body, an edited request under the same key
gives 409.

**The timeout scenario**, which is the one R-103 was about:

| Step | What happens |
|---|---|
| Request A calls send, the provider is unreachable, the client sees an exception | reservation becomes `OUTCOME_UNKNOWN`, `dispatch_started_at` set |
| History | status is **not** `SENT` — acceptance was never claimed |
| Client retries with the same key | **409 `EMAIL_IDEMPOTENCY_OUTCOME_UNKNOWN`**, no second outbound attempt |
| User decides to send again with a new key | new reservation, new send — their decision, not the system's |

**Concurrency.** Two requests with one key on two separate `DbContext`s: exactly one ran, the other got a
409 or a replay, and the database ended with 1 message, 1 `sent_emails`, 1 attachment, 1 file and 1
reservation. A unique-key collision never surfaced as a 500.

---

## 7. G11-G — Preview closure

Measured at HEAD: **14** of the 30 templates use `{{actionBlock}}`; **5** are registered in
`EmailActionTemplates`; **9** were not, and previewing them threw
`EMAIL_TEMPLATE_UNRESOLVED_PLACEHOLDER`.

The fix is preview-only. An unregistered action template now renders a neutral inert block that names no
business outcome — no "Chấp nhận", no "Từ chối", no "Xác nhận" — because what buttons those emails should
carry is a decision nobody has made, and inventing labels would put an answer to it in front of operators
as though it had been given. `EmailActionTemplates` was **not** extended, and no template content was
changed.

`EmailActionTemplates.For` has exactly one caller (the preview handler), measured — so nothing about the
send path could be affected by this.

**30/30 templates preview in VI and EN** (60 theory cases), and 60 more assert that no preview contains a
token, a clickable link, an `href`, `javascript:`, a script tag, an event handler or a bare six-digit
code. Send stays fail-closed for action templates, registered or not. Hot-editing a template in the
database still shows up on the next preview with no restart.

---

## 8. Regression

| Gate | Result | Baseline |
|---|---|---|
| Backend build, `--no-incremental` | 0 errors, **208** warnings | 208 |
| Backend unit | **1765 / 1765** | ≥ 1730 |
| Architecture | **14 / 14** | ≥ 14 |
| Integration, unfiltered | **1176 / 1176** (twice) | ≥ 1020 |
| Frontend `test:unit` | **914 / 914**, **69** files | ≥ 891 / 68 |
| `tsc --noEmit` | exit 0 | 0 |
| Vite production build | exit 0 | 0 |

0 failed, 0 skipped. No test was deleted, skipped, retried or had an assertion loosened.

**One red run, and what caused it.** The full unfiltered integration run first came back with all 20
`EmailSendIdempotencyTests` red, while the same class passed under a filter and the whole `Emails`
namespace passed together. Rather than re-running, the live test database was queried mid-run: the
visit-request resubmit suite creates its rows through EF with generated keys, and its AUTO_INCREMENT
counter had climbed to `visit_requests` 991_626…991_636 with `visit_request_campuses` 991_637…991_647 —
straight through this suite's hard-coded "private" range at 991_600. The range-based cleanup then tried
to delete a request another suite still had children for. The suite's base moved to 8_400_000, well clear
of anything AUTO_INCREMENT reaches. Two consecutive full runs green afterwards.

This is a pre-existing hazard in the test convention (hard-coded ranges next to AUTO_INCREMENT), not a
product defect; the two other email suites that share a base (`EmailG8JourneyTests` and
`ReportInvoiceRouteTests`, both 991_400) are noted but left alone as out of scope.

**Test accounting** — +184 in total, every one reconciled:

| Suite | Delta | What it covers |
|---|---|---|
| `EmailSendIdempotencyContractTests` (new) | +35 unit | key validation, fingerprint semantics, delivery classification, dispatch claim |
| `EmailPreviewCoverageTests` (new) | +124 integration | 30 templates × 2 languages × 2 theories, plus 4 facts |
| `EmailSendIdempotencyTests` (new) | +20 integration | real-stack state machine, concurrency, per-action table, script invariants |
| `EmailTemplateSyncScriptTests` | +3 integration | `SET NAMES` invariant for the three G7 scripts |
| `ReportInvoiceRouteTests` | +5 integration | HTTP-level keyless refusal, replay, key reuse |
| `idempotentSend.test.tsx` (new) | +23 frontend | key lifetime, retry classification, message wording |

No test was removed, and no existing assertion was weakened. Two test files were **corrected**:
`ReportInvoiceRouteTests` now sends the header its routes require (the refusal itself gained its own
tests), and `EmailSendIdempotencyTests` had one test rewritten after it turned out to be retrying a
*different* request under one key — which the contract correctly refuses.

---

## 9. Status

```text
R-103: CLOSED
R-104: BLOCKED — awaiting owner role/UC/metric decision
R-105: BLOCKED — awaiting owner UX decision
R-106: CLOSED
```

What CLOSED means for R-103, stated exactly: one logical user action no longer produces a second outbound
attempt by itself. It does **not** mean exactly-once delivery — SMTP does not offer that, PEMS has no
delivery webhook, and when the provider's answer is lost the system says so instead of guessing.

---

## Extended 2026-07-30 — G11 final closure

R-103's protection now covers the sends where the **client** chooses the recipients, not only the six
report/invoice routes. Manual compose (`SendEmailCommand`) and reply (`ReplytoEmailCommand`, both modes)
declare themselves idempotent, and the normalised recipient set is part of the request fingerprint:

* re-serialising the same chips in a different order, or with different casing or spacing, is the **same**
  request — so a retry after a dropped connection is recognised as a retry rather than sent twice;
* adding, removing or moving an address between groups is a **different** request — refused rather than
  answered "already sent", which would have left the person just added receiving nothing while the screen
  reported success.

Eight command types, nine operation codes (`ReplytoEmailCommand` answers to two, because Reply and Reply
All address different people and must not share a reservation).

`SendEmailDraftCommand` is deliberately left on its own mechanism: the `DRAFT → SENT` conditional UPDATE is
a stronger guarantee than a fingerprint, and stacking two schemes on one path would make neither
authoritative.

The claim above is unchanged and still bounded the same way: **no exactly-once delivery is claimed** for
any of these routes. See `10-g11-final-closure.md` §5.3.
