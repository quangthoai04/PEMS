# Rollback guidance — email template sync

What to do when the template sync went somewhere you did not want it to go. Read this **before**
running `02_sync_templates.sql`, not after: the single most useful thing here is the checkpoint you
take in advance.

The short version: **rolling the database back is usually the wrong first move.** The sync writes to
exactly one table, it never deletes a row, and it never touches history or drafts. Almost every
problem you can hit is either a content problem in `email_templates` (fix forward) or a backend
problem (roll the backend back, leave the database alone). Restoring a whole database to undo a
30-row content change costs you every email sent since the checkpoint.

---

## 1. Before you run anything: the checkpoint

The sync modifies only `email_templates`. That is the only table you must be able to restore.

```bash
# Enough to undo anything this sync can do, and small enough that nobody skips it.
mysqldump -h <host> -u <user> -p --single-transaction --no-create-info \
  <database> email_templates > email_templates_before_sync_$(date +%Y%m%d_%H%M%S).sql
```

Also take, and keep, the output of the two read-only scripts:

```bash
mysql -h <host> -u <user> -p <database> < 01_preflight.sql > preflight_before.txt
# ... run the sync ...
mysql -h <host> -u <user> -p <database> < 03_verify.sql   > verify_after.txt
```

The `CHECKSUM TABLE` block at the end of `01_preflight.sql` and section 9 of `03_verify.sql` print the
same six checksums. Identical output across the two files is your evidence that history, recipients,
drafts, draft recipients, attachments and action tokens were not touched. Keep both files; they are
what you will be asked for if anyone questions the change later.

A full-database backup is worth having for the deploy as a whole. It is not what you reach for to
undo this script.

---

## 2. Four different things people mean by "roll back"

Decide which one you are actually in before you type anything.

### 2.1 Roll back the CONTENT (a template renders wrongly)

Symptom: mail goes out, but the subject or body is wrong — bad wording, a placeholder that renders
empty, a missing translation.

This is not a rollback. Nothing is broken structurally, and reverting the whole sync would take away
29 correct templates to fix one wrong one. Restore the single row:

```sql
-- From the dump taken in section 1, lift just the row you need. Match on template_code.
UPDATE email_templates
SET subject_vi = ..., body_vi = ..., subject_en = ..., body_en = ..., variables_text = ...
WHERE template_code = 'THE_ONE_CODE';
```

Then re-run `03_verify.sql`. If check E1/E2 now fails, the content you restored uses placeholders
that no longer match `variables_text` — fix that too, or the renderer will refuse the template.

Do **not** re-run `02_sync_templates.sql` expecting it to help: it will put the canonical content
back, which is the content you just decided was wrong. Fix the canonical seed and regenerate
(section 5) if the canonical content is genuinely wrong.

### 2.2 Roll back the STATUS (something got deactivated that is still in use)

Symptom: a template you still rely on is now `INACTIVE`, and its caller fails at send time.

The sync deactivates exactly nine codes, all named explicitly in the script:
`ACCOUNT_CREATED_INTERNAL`, `VISIT_REQUEST_APPROVED`, `VISIT_REQUEST_REJECTED`, `VISIT_CANCELLED`,
`HOST_ASSIGNMENT`, `VISIT_REQUEST_SUBMITTED_NOTIFY`, `LOGISTICS_REQUEST`,
`LOGISTICS_REQUEST_SUBMITTED_NOTIFY`, `OTP_VISIT_REQUEST`.

If one of those is still being sent, you have found something more interesting than a bad deploy:
a caller that the audit concluded did not exist. Reactivate the row so mail keeps flowing —

```sql
UPDATE email_templates SET status = 'ACTIVE' WHERE template_code = 'THE_ONE_CODE';
```

— and then **record it**, because it contradicts the catalog. The right fix is either to route that
caller onto a canonical template or to add the code back to the catalog deliberately. Leaving a
reactivated legacy row in place with no note means the next sync deactivates it again and you will
be debugging the same outage twice.

If the deactivated template is **not** one of the nine, the sync did not do it. Look elsewhere.

### 2.3 Roll back the BACKEND (the code is wrong, the templates are fine)

Symptom: the templates are correct in the database, but the application errors, sends the wrong
template, or renders nothing.

Redeploy the previous backend build. **Leave the database alone.** The canonical catalog is a superset
in every direction that matters: every code the previous backend used is either still present and
`ACTIVE`, or is one of the nine legacy codes, which the previous backend also did not call — that is
why they are on the list. Reverting the templates to "match" an older backend would break the newer
one on the way back forward.

The one thing worth checking before you conclude this: `03_verify.sql` sections A and E. If A2 says a
canonical code is `INACTIVE`, or E1/E2 report a placeholder mismatch, the database is contributing to
the failure and section 2.1/2.2 applies as well.

### 2.4 Roll back the DATABASE (you ran the script somewhere you should not have)

Symptom: you sourced `02_sync_templates.sql` against the wrong database.

This is the case the guard exists to prevent — the script refuses to run unless
`@pems_sync_confirm_database` is set to the exact name of the database you are connected to. If it
ran anyway, then somebody set that variable, so start by finding out who and on which connection.

The variable is session-scoped and the script clears it as its last statement, so one confirmation
authorises exactly one run. Do not "helpfully" set it in a shared `~/.my.cnf` or an `init-command`:
that turns the guard off permanently for every session on that machine, including the one where
somebody meant to connect to staging and did not.

Restore `email_templates` from the dump in section 1:

```sql
START TRANSACTION;
DELETE FROM email_templates WHERE template_code IN (/* the 30 canonical codes */);
-- then SOURCE the dump's INSERT statements for those codes
COMMIT;
```

Two things not to do while cleaning up:

* **Do not `DELETE FROM email_templates` wholesale.** `sent_emails.email_template_id` and
  `email_drafts.email_template_id` are foreign keys with `ON DELETE SET NULL`. A blanket delete will
  not error — it will quietly null out the template link on every historical email and every draft,
  and no restore of `email_templates` afterwards puts those links back. Losing history's link to its
  template is a worse outcome than the wrong template content.
* **Do not delete "templates that were not there before".** The three or four rows the sync inserted
  are indistinguishable, after the fact, from templates an operator created in the admin UI in the
  same window. Restore by `template_code` from your dump, never by "id greater than N" or
  "created_at after T".

---

## 3. What a rollback must never take with it

Whatever you do, these must come out the other side unchanged. `03_verify.sql` sections G, H and 9
check them; run it after any manual restore.

| Must survive | Why |
|---|---|
| `sent_emails.subject` and `body_snapshot` | This is what was actually sent. It is a record, not a cache — rewriting it to match a new template is falsifying history. |
| `sent_email_recipients` | Includes BCC rows. Deleting and re-deriving them from anywhere would change who the record says was copied. |
| `sent_email_attachments` and the `files` rows they point at | An attachment row without its file is a download that 500s; a file without its row is an orphan nobody can reach. |
| `email_drafts` and `email_draft_recipients` | Unsent user work. There is no other copy. |
| `email_action_tokens` | Live accept/decline/confirm links people are holding in their inbox. Deleting them silently breaks every outstanding invitation. |
| Templates outside the catalog | Operator-authored. Nothing in the schema distinguishes them from ours after the fact, so if you remove one it does not come back. |

---

## 4. When NOT to roll the database back

* **The backend is not deployed yet, and templates look wrong.** Fix forward. Nothing is sending mail
  from them.
* **Only one or two templates are wrong.** Section 2.1. A whole-catalog rollback to fix one row makes
  29 other templates stale.
* **The failure is a send failure, not a content failure** — SMTP refused, provider timed out, a
  recipient bounced. No template change causes those, and no template rollback fixes them.
* **`03_verify.sql` passes.** Then the sync did what it promised, and the problem you are looking at
  came from somewhere else. Rolling back the templates will remove a working change and leave the
  real fault in place.
* **You are past the point where mail has been sent from the new templates.** Rolling the catalog back
  then means new sends use old content while history holds the new — a state nobody reasons about
  correctly later. Fix forward and note the window.

---

## 5. Regenerating `02_sync_templates.sql`

The 30 `VALUES` rows in `02_sync_templates.sql` are lifted verbatim from the canonical schema script,
so the two cannot drift. If the canonical catalog changes deliberately, regenerate rather than
hand-edit — a hand-edited sync script that disagrees with the seed produces a database that fails its
own verification on the next fresh import.

Regeneration reads:

* source: `docs/database/scripts/PEMS_FULL_V2_NO_SEED_DATA_GALLERY_DOCUMENT_AI_FIXED.sql`,
  the single `INSERT INTO email_templates` block;
* the nine legacy codes, from `docs/email-standardization/03-system-template-catalog.md` section 8.

After regenerating, run the whole sequence against a disposable database — fresh import, sync, verify,
sync again, verify again — and confirm the second sync reports `0` inserted, `0` updated and `0`
deactivated. `EmailTemplateSyncScriptTests` does exactly this and is the gate that keeps the two
files honest.
