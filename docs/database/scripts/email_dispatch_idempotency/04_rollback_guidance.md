# Rolling back the send-idempotency migration

Read this before undoing anything. "Roll back" means three different things here and only one of them
involves the database.

## 1. Roll back the backend, keep the table

**This is almost always the right move.** The table is additive and nothing that existed before G11
reads it. An older backend deployed against a database that has `email_send_idempotency` behaves exactly
as it did before — it never selects from the table, never inserts into it, and no trigger or view
depends on it.

So: redeploy the previous backend build. Do not touch the schema. The rows already in the table stay as
the record of what was sent while the new build was live.

There is one visible consequence and it is the one you are choosing: with the old backend, a report send
carries no idempotency key again, so a retry after a network timeout can once more produce a second
email. That is R-103 returning, not a new fault.

## 2. Roll back the frontend, keep the backend

**Do not do this.** The new backend *requires* `Idempotency-Key` on the six report/invoice send routes
and refuses a request without one — deliberately, because a silent legacy path would make the whole
guarantee optional. An older frontend does not send the header, so every "Gửi" button on the report
screens would fail with `EMAIL_IDEMPOTENCY_KEY_REQUIRED`.

If the frontend must go back, take the backend back with it (case 1).

## 3. Drop the table

**Only when there are no rows worth keeping**, and then only deliberately.

```sql
SELECT COUNT(*) AS total,
       SUM(state = 'SUCCEEDED')       AS succeeded,
       SUM(state = 'OUTCOME_UNKNOWN') AS unknown
FROM email_send_idempotency;
```

- `total = 0` — nothing has been sent under the new contract. Dropping loses nothing.
- `succeeded > 0` — each of those rows is the proof that a specific person sent a specific report at a
  specific time, linked to its `sent_emails` row. That is audit evidence. **Do not drop it** to tidy up
  after a failed deploy; roll the backend back instead (case 1) and leave the table alone.
- `unknown > 0` — these are the sends whose outcome was never established. They are the only record that
  the ambiguity existed. Dropping them destroys the one piece of information an operator would need to
  answer "did this invoice go out twice?".

If you have decided anyway:

```sql
SET @pems_idem_confirm_database = '<exact database name>';   -- read this back before continuing
SELECT DATABASE();                                            -- and confirm it matches
DROP TABLE email_send_idempotency;
```

`DROP TABLE` removes the two foreign keys with it. Nothing else references this table, so no other
object is affected — `users` and `sent_emails` are parents, never children.

## What must survive any of the three

None of these paths may touch:

- `email_templates` — the catalog. Rolling back a *backend* never requires a template change.
- `sent_emails`, `sent_email_recipients`, `sent_email_attachments` — the history of what was actually
  sent, including everything sent while the new build was live.
- `email_drafts`, `email_draft_recipients`, `email_draft_attachments`.
- `email_action_tokens`, `files`.

If a proposed rollback step reads "delete the emails sent during the incident", it is the wrong step.
The history is what tells you how bad the incident was.

## When NOT to roll back

- **A user reports "kết quả gửi chưa xác định".** That is the contract working: the provider's answer
  was lost and the system refused to guess. The user sends again with a new key when they decide to.
  Nothing is broken.
- **A user reports "đã gửi rồi" on a second click.** Also the contract working — the replay returned the
  first result instead of sending twice.
- **`EMAIL_IDEMPOTENCY_KEY_REQUIRED` in the logs.** Something is calling the API without a key: an old
  frontend build, a script, or a cached bundle. Fix the caller. Weakening the backend to accept keyless
  sends re-opens R-103 for everyone.
