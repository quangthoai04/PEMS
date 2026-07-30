---
type: runbook
status: draft
updated: 2026-07-29
links:
  - docs/email-standardization/05-final-verification-report.md
  - docs/database/scripts/email_template_cc_bcc_sync/04_rollback_guidance.md
---

# G10 — deployment readiness runbook

**Nothing in this file has been executed.** It is the plan, written while the work is fresh, so the
person who does run it is not reconstructing it from commit messages later.

```text
G10_DEPLOYMENT_AUTHORIZED = NO
TARGET_ENVIRONMENT        = NOT_SPECIFIED
TEST_RECIPIENT            = NOT_SPECIFIED
COMMIT_PUSH_AUTHORIZED    = NO
```

While those four hold, do not connect to a deployed database, do not commit or push, do not deploy, and
do not send a smoke email.

---

## 0. The one ordering rule

```text
Backup → DB preflight → DB sync → DB verify
       → DB schema migration (§5A) → schema verify
       → DB trigger migration  (§5B) → trigger verify
       → DB column migration   (§5C) → column verify
       → backend deploy → backend smoke
       → frontend deploy → frontend smoke → monitoring
```

> **Two new steps since G11 (2026-07-29).** The backend now requires a table that does not exist on a
> pre-G11 database, so the schema migration joins the database-first block. See §5A.
>
> **One more since G12 (2026-07-30).** Five primary-contact guard triggers are replaced. See §5B. It is
> ordered AFTER §5A only because both are database-first and doing them in a fixed order makes the
> verify output unambiguous; they do not depend on each other.
>
> **One more since G11 final closure (2026-07-30).** `email_templates` gains one additive column,
> `revision`. See §5C. The new backend issues EVERY template content write as a conditional UPDATE
> carrying `AND revision = :expected`, so on a database without the column both saving a template and
> restoring one fail outright. Like the other two it is database-first and independent of them.

**The database is brought up to the catalog before the new backend runs.** The reason is specific rather
than ceremonial: the new backend renders every email from `email_templates` and has no hard-coded
fallback — a deliberate property (R-01/R-02). Deploy it against a database that is missing
`DEPT_LEADERSHIP_GRANTED` and the first leadership transfer fails at send time with a template-not-found
error, after the business transaction has already committed. The old backend, by contrast, is perfectly
happy with the new catalog: every code it used is either still present and `ACTIVE`, or is one of the
nine retired codes it never called. So database-first is safe in both directions and backend-first is not.

---

## 1. Prerequisites — collect before starting

| # | What | Why it matters |
|---|---|---|
| 1 | Target environment named (staging / production) | Everything below is per-environment; "the server" is not an answer. |
| 2 | Database host, name, and an account with `SELECT`, `INSERT`, `UPDATE` on `email_templates` | The sync needs no `DELETE` and no DDL. If the account has more, that is fine, but it does not need it. |
| 3 | A backup owner and a backup location | Section 2. Someone must be accountable for the restore, not just the dump. |
| 4 | A test recipient mailbox that a human can open | Section 9/11. It must be a real inbox somebody checks, not a black hole. |
| 5 | Maintenance window, or an explicit decision that none is needed | The sync locks `email_templates` briefly; sends during that window could see a mid-update row. |
| 6 | SMTP configuration confirmed for the target | `Smtp:FromEmail`, `Smtp:User`, host/port/SSL. If `FromEmail` is unset the system falls back to `Smtp:User`, then `no-reply@pems.local` — check which one you are actually going to send as. |
| 7 | The previous backend and frontend build identifiers | Section 14 cannot be written after the fact. |

Also confirm the target's `Smtp:PickupDirectory` is **empty/unset**. It is a test facility: when set,
.NET writes `.eml` files to disk instead of sending, and every "successful" send would go nowhere.

---

## 2. Backup and rollback point

```bash
# The sync writes to exactly one table. This is what you need to undo it.
mysqldump -h <host> -u <user> -p --single-transaction --no-create-info \
  <database> email_templates > email_templates_before_sync_$(date +%Y%m%d_%H%M%S).sql
```

Take the full-database backup your normal deploy process requires as well — but note that it is for the
deploy as a whole. To undo *this* change you want the single-table dump, because restoring a whole
database to fix 30 content rows discards every email sent since.

Record: dump file path, its size, the backup owner, and the timestamp. If the dump is smaller than a few
hundred KB, open it — an empty dump means the credentials read a different database than you think.

---

## 3. Database preflight (read-only)

```bash
mysql -h <host> -u <user> -p <database> \
  < docs/database/scripts/email_template_cc_bcc_sync/01_preflight.sql \
  > preflight_before_$(date +%Y%m%d_%H%M%S).txt
```

Read the output before continuing. Specifically:

* **§0** — does `current_database` say what you expect? This is the last cheap moment to notice you are
  on the wrong host.
* **§1** — anything reading `MISSING` stops the deploy. The sync will refuse anyway; knowing why here is
  faster than reading a `SIGNAL` message later.
* **§3** — how many templates are canonical / legacy / unknown. **Look at the unknown list.** Those are
  templates somebody authored in the admin UI. The sync leaves them alone, but you should know they exist
  before you tell anyone "the catalog is now the 30 canonical templates".
* **§4** — how much history and how many drafts reference these rows. This is the number that makes
  "deactivate, never delete" concrete.
* **§5** — the six `CHECKSUM TABLE` values. **Keep this file.** Section 6 compares against it.

---

## 4. Template sync

```bash
mysql -h <host> -u <user> -p <database> <<SQL
SET @pems_sync_confirm_database = '<exact database name>';
SOURCE docs/database/scripts/email_template_cc_bcc_sync/02_sync_templates.sql;
SQL
```

The variable is the confirmation: the script refuses to write unless it equals the database you are
connected to. Type the name; do not paste it from a script that also chooses the connection, or the
guard is confirming your typo back to you.

It is session-scoped and the script clears it on the way out, so one confirmation authorises one run.
Do **not** put it in `~/.my.cnf` or an `init-command` — that disables the guard for every session on the
machine, including the one where somebody meant to connect to staging.

Expected output, three counts:

```text
inserted_templates            <n>
updated_templates             <n>
deactivated_legacy_templates  <0..9>
```

On a database that has never been synced, expect insertions and up to 9 deactivations. On one that is
already current, expect `0 / 0 / 0`. Anything surprising — a large `updated_templates` on a database you
believed was current — is worth understanding before you proceed, not after.

The whole script runs in one transaction. If it fails, nothing was applied.

---

## 5. Database verify

```bash
mysql -h <host> -u <user> -p <database> \
  < docs/database/scripts/email_template_cc_bcc_sync/03_verify.sql \
  > verify_after_$(date +%Y%m%d_%H%M%S).txt
echo "verify exit status: $?"
```

**The exit status is the gate.** The script ends with a `SIGNAL` that fires when any check reports
`FAIL`, so a non-zero status means stop — do not deploy the backend. A person scrolling the output and
seeing green near the bottom is not the same check.

Read §Z: 16 checks should pass, 0 fail, 1 informational (the count of templates outside the catalog).

Then compare §9 against the preflight's §5. **The six checksums must be identical.** They cover
`sent_emails`, `sent_email_recipients`, `sent_email_attachments`, `email_drafts`,
`email_draft_recipients` and `email_action_tokens` — everything the sync must not have touched. If the
system was live and sending during the window, they may legitimately differ; in that case say so
explicitly in the deploy record rather than skipping the comparison.

---

## 5A. Schema migration — send idempotency (added by G11)

Still inside the database-first block, and for the same reason as §0: the new backend **requires** this
table. Deploy it against a database without `email_send_idempotency` and every report/invoice send fails
at the reservation, before anything is generated.

```bash
mysql -h <host> -u <user> -p <database> \
  < docs/database/scripts/email_dispatch_idempotency/01_preflight.sql \
  > idem_preflight_$(date +%Y%m%d_%H%M%S).txt
```

Read §1 (both parent tables present with compatible column types) and §3 (whether the table already
exists — on a re-run it will, and the migration will make no change).

Then the migration. **The confirmation variable and the `USE` must name the same database**, on the same
session, or it refuses before any DDL:

```bash
{ echo "SET @pems_idem_confirm_database = '<database>';";
  cat docs/database/scripts/email_dispatch_idempotency/02_up_additive.sql; } \
| mysql -h <host> -u <user> -p <database>
```

The confirmation is **spent** when the script ends. A second run must name the target again — deliberate,
so one authorisation cannot ride a pooled connection into a migration nobody asked for.

```bash
mysql -h <host> -u <user> -p <database> \
  < docs/database/scripts/email_dispatch_idempotency/03_verify.sql \
  > idem_verify_$(date +%Y%m%d_%H%M%S).txt
echo "verify exit status: $?"
```

**The exit status is the gate**, exactly as in §5: 25 checks pass, 0 fail, 3 informational. A non-zero
status means stop — do not deploy the backend.

The migration is additive and idempotent: it creates one table and reads or writes nothing else. Running
it twice changes nothing and creates no duplicate index (measured on a disposable database, before and
after snapshots byte-identical).

Rollback for this step is `04_rollback_guidance.md` in the same folder. The short version: rolling the
**backend** back is safe and needs no schema change; rolling the **frontend** back alone is not, because
the new backend requires the `Idempotency-Key` header the old frontend does not send.

---

## 5B. Trigger migration — primary-contact guards (added by G12)

Folder: `docs/database/scripts/contact_guard_closure/`.

**Read `01_preflight.sql` first, and read its output.** It is read-only and does two things that matter:

1. It reports, per trigger, whether the installed body is pre-G12 or already hardened.
2. It runs five queries counting rows that **already violate** the invariant these guards enforce.

That second group is the one to stop on. This migration replaces trigger bodies and repairs no data — on
purpose, because silently rewriting which account owns a visit request is not a decision a schema
migration should take. If a count is non-zero, those rows stay, and **every later write touching them
will start failing**. Fix them first: reassign the primary contact to an ACTIVE VISITOR, or move the
request back to `PENDING_CONFIRMATION` with `visitor_user_id` NULL. Re-run `01` until all five read 0.

```bash
mysql -u<user> -p <database> < 01_preflight.sql

# Name the database you intend to change, on the SAME session as the migration.
{ echo "SET @pems_guard_confirm_database = '<database>';"; cat 02_up_replace_triggers.sql; } \
  | mysql -u<user> -p <database>

mysql -u<user> -p <database> < 03_verify.sql; echo "exit=$?"
```

Without that confirmation variable — or with a value that does not match `DATABASE()` — the migration
aborts with SQLSTATE 45000 **before any DDL**. The confirmation is spent at the end, so a pooled
connection cannot carry authorisation into a migration nobody asked for.

**The exit status is the gate.** Expected: **34 checks pass, 0 fail**. `03_verify.sql` SIGNALs on any
failure, so the client exits non-zero; a pipeline does not need to parse the table. Verified both ways on
a disposable database — a deliberately dropped guard produced exit 1 and one FAIL.

Idempotent: running `02` twice leaves a byte-identical trigger snapshot (measured, MD5 of all 32 trigger
bodies plus their action order). The migrated database was also compared against a fresh canonical
import: all 32 trigger bodies identical as raw bytes.

What this changes is how failures are **reported**, plus closing two NULL paths:

* a VISITOR whose account is still `PENDING_EMAIL_CONFIRMATION` now yields
  `PRIMARY_CONTACT_VISITOR_ACCOUNT_INACTIVE` instead of `22001 Data too long`. Every new account passes
  through that state, so this was reachable in normal operation;
* a user whose role row cannot be read is no longer misreported as "user not found";
* `trg_users_protect_active_primary_contact_bu` no longer compares a role code that a zero-row
  `SELECT … INTO` left NULL — `NULL <> 'VISITOR'` is UNKNOWN, which `IF` treats as false, so on that
  path the guard had stopped guarding.

Rollback: `04_rollback_guidance.md`. It is safe in the sense that no data is lost, but it reinstates all
three reporting defects — the application is expected to depend on the stable codes, so a rollback is a
behavioural regression for callers rather than a return to neutral. Do **not** simply drop the five
triggers: the application does not re-implement these checks, and the tables would be left unguarded.

---

## 5C. Column migration — template revision (added by G11 final closure)

`docs/database/scripts/email_template_revision/`

One additive column, nothing else:

```sql
email_templates.revision INT UNSIGNED NOT NULL DEFAULT 1
```

**Why it must precede the backend.** Update and restore-to-default are both issued as
`UPDATE … SET …, revision = revision + 1 WHERE email_template_id = ? AND revision = ?`. Without the
column that statement is a SQL error, so an operator opening a template and pressing Lưu gets a failure
rather than a save — and restore does not work at all.

**What it replaces.** The optimistic-concurrency token used to be `updated_at`, which is DATETIME with no
fractional part: two saves inside the same second stored an identical stamp, compared equal, and the
second silently overwrote the first. Nobody saw an error; one person's wording was simply gone.

```bash
# 1. Preflight (read-only). Says whether the column is already present, and reports row counts.
mysql -u <user> -p <db> < docs/database/scripts/email_template_revision/01_preflight.sql

# 2. Apply. The confirmation variable must match the connected database or the script refuses.
mysql -u <user> -p <db> -e "SET @pems_guard_confirm_database='<db>'; \
  SOURCE docs/database/scripts/email_template_revision/02_up_add_revision.sql;"

# 3. Verify. Exits non-zero if any check failed, so it can gate the deploy.
mysql -u <user> -p <db> < docs/database/scripts/email_template_revision/03_verify.sql
```

Expect from step 3: **9 PASS / 0 FAIL / 1 INFO**. The INFO line is a content digest — capture it, because
comparing it before and after is what proves the migration touched no wording.

Idempotent: running step 2 twice prints `revision already present — nothing to do` and changes nothing.
On MySQL 8.0 the column is added at the end of the table with a literal default, which is an INSTANT
operation — no table rebuild and no long lock regardless of row count.

Rollback: `04_rollback_guidance.md`. Roll the **backend** back first; dropping the column under a running
new backend breaks every template save. No data is lost by dropping it — the column counts writes, it
never holds content.

---

## 6. Confirm the catalog the backend needs

The backend's registry and the database must agree in both directions, which `03_verify.sql` checks A1/A2
already assert: all 30 canonical codes present and `ACTIVE`. If A1 or A2 failed, the sync did not do its
job and section 5 already stopped you.

The 9 retired codes should read `INACTIVE` (check B1) and still exist (check B2). Present-but-inactive is
the correct end state — history rows point at them.

---

## 7. Deploy the backend

Normal deploy process. Nothing email-specific, with one thing to confirm afterwards:

* `Smtp:PickupDirectory` is unset in the deployed configuration;
* `Smtp:FromEmail` is the address you intend to send as;
* the app starts clean — a template-contract failure would surface as errors on the first send, not at
  startup, so a quiet start is not by itself evidence.

---

## 8. Backend smoke — renderer and outbound

Use the **test recipient** from prerequisite 4 for everything in this section.

1. `POST /api/email-templates/preview` for one plain template (e.g. `ACCOUNT_ROLE_CHANGED`) with a
   complete variable context. Expect rendered subject and body.
2. The same call with a variable deliberately missing. Expect a refusal, not a placeholder. This proves
   the deployed renderer is the strict one — a lenient preview would be the single most misleading thing
   in the system.
3. One real system send to the test recipient. Confirm: it arrives; the From is the configured address;
   the `sent_emails` row is `SENT`; `body_snapshot` contains no raw token or action URL.

Note that 9 of the 30 templates cannot be previewed (see R-106); pick a template from the other 21 for
step 1 so a known gap is not mistaken for a deploy failure.

---

## 9. Deploy the frontend

Normal deploy process, after the backend is up and smoked. The frontend calls endpoints that must exist:
`GET /api/emails/drafts`, `POST /api/emails/replytoemail`, `GET /api/emails/recipient-limits`.

---

## 10. Frontend smoke — compose, draft, reply, history

Against the test recipient, in this order:

1. **Compose** — open the compose modal, add two TO, one CC, one BCC. Confirm the chips separate by group
   and that exceeding the limit is refused in the UI.
2. **Draft** — save it, reload the page, reopen from the "Nháp" tab. Every group must come back exactly as
   entered, in the same order.
3. **Send** — send the draft. It must produce exactly one message and one history row.
4. **History** — open the sent email's detail. The sender sees all recipients including BCC.
5. **Reply** — press reply. TO must be read-only and resolved by the server. Add a CC. Send.
6. Confirm the reply arrived threaded (`In-Reply-To` present) and that **the original's BCC is not on it**.

---

## 11. BCC privacy check — the one that must not be skipped

This needs a second mailbox: one test recipient in TO, one in BCC.

1. Send one message with both.
2. Open the message as the **TO** recipient (in the app, not the mailbox): they must see the TO and CC,
   and no sign that a BCC exists — no address, no count, no "hidden recipients" badge.
3. Open it as the **BCC** recipient: they see the visible envelope plus their own entry only.
4. Open it as the sender: they see everything.
5. In the delivered mail itself, view the raw source as the TO recipient: there must be no `Bcc:` header.

If any of these is wrong, roll the frontend back and stop. A leak here is not a cosmetic defect.

---

## 12. Monitoring for the first hours

Watch for, in this order of seriousness:

| Signal | Where | What it means |
|---|---|---|
| `EMAIL_TEMPLATE_NOT_FOUND` / `EMAIL_TEMPLATE_INACTIVE` | application errors | The catalog and the code disagree. The sync did not run, or ran against a different database. |
| `EMAIL_TEMPLATE_VARIABLE_MISSING` | application errors | A caller and a template disagree about variables. Affects only that one send path. |
| Rising `sent_emails.status = 'FAILED'` | database | Provider-side. Not a template problem; check SMTP. |
| Any raw token, OTP, password or action URL in a log line | log search | A logging regression. Treat as an incident, not a bug. |
| A `Bcc:` header in any delivered message | test mailbox | Stop. Section 11. |

Confirm explicitly that logs contain no secrets — that property is asserted by
`EmailServiceSensitiveLoggingTests`, but the deployed log configuration is not what that test covers.

---

## 13. Avoiding duplicates while smoking or retrying

Two different things. **Both are now closed for the six report/invoice sends** (G11 / R-103); the second
one is still open for manual compose and reply.

* **Double-click / repeated press** — closed everywhere. The compose, reply and report screens hold a
  per-row in-flight guard, so a second press during a send does nothing.
* **Retry after a network timeout** — closed for the six report/invoice sends. Each carries an
  `Idempotency-Key`; a retry reuses it, and the server replays the first result instead of sending again.
  Still open for manual compose/reply, which are not under the contract.

What an operator will see while smoking the report screens, and what each message means:

| Message | Meaning | What to do |
|---|---|---|
| "Đã gửi…" | the send succeeded | nothing; the next click is a new send |
| "Yêu cầu đang được xử lý…" | another request with this key is still running | wait — do not press again |
| "Chưa xác định được kết quả… email có thể đã được gửi" | the provider was contacted and its answer was lost | **check the mailbox and `sent_emails` before deciding**; sending again is a deliberate act with a new key |
| "Mất kết nối trước khi có kết quả" | the browser gave up; the server may still be working | press send again — it resumes the same attempt, it does not start a second one |
| "Nội dung đã thay đổi so với lần gửi trước" | the same key was used for a different request | press send again; the client starts a fresh attempt |

Still true regardless: if a send appears to hang, **check the recipient's mailbox and `sent_emails`
before assuming nothing happened.** The system will not send twice by itself, but it also cannot tell you
what a provider did with a message it never acknowledged.

---

## 14. Rollback

| Situation | Action |
|---|---|
| Template content is wrong | Restore that row from the section 2 dump, by `template_code`. Do not roll back the whole catalog. |
| A template you still need is `INACTIVE` | Reactivate it, then record it — it contradicts the catalog and will be deactivated again by the next sync. |
| The backend is faulty, templates are fine | Redeploy the previous backend. **Leave the database alone** — the catalog is compatible with both. |
| The frontend is faulty | Redeploy the previous frontend. No database action. |
| The sync ran against the wrong database | Restore `email_templates` from the dump, by `template_code`. Never `DELETE FROM email_templates` — the foreign keys are `ON DELETE SET NULL` and a blanket delete silently unlinks every historical email from its template, which no later restore repairs. |
| Provider/SMTP failure | Not a template problem and no rollback fixes it. Sends are recorded `FAILED` truthfully; fix the provider and resend. |

Full detail, including what must survive any rollback: `docs/database/scripts/email_template_cc_bcc_sync/04_rollback_guidance.md`.

---

## 15. What the owner must supply before G10 can start

| # | Item | Current value |
|---|---|---|
| 1 | Target environment | NOT_SPECIFIED |
| 2 | Target database (host + name) | NOT_SPECIFIED |
| 3 | Backup owner and location | NOT_SPECIFIED |
| 4 | Test recipient mailbox | NOT_SPECIFIED |
| 5 | Maintenance window | NOT_SPECIFIED |
| 6 | Authority to commit and push | NO |
| 7 | Authority to deploy the backend | NO |
| 8 | Authority to deploy the frontend | NO |
| 9 | Rollback version (previous backend + frontend build ids) | NOT_SPECIFIED |

Until all nine are supplied, `G10 execution: NOT STARTED`.
