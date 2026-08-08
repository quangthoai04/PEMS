# Runbook — visit notifications that did not get out

Scope: the two post-commit notifications that survive a mail failure — the **campus rejection** email and
the **contact-invitation expiry** notice. Both are sent after their business change has committed, so a
mail problem never means the business change failed. This is how an operator finds and finishes one that
automatic recovery has stopped working on.

No new admin surface was added. Everything below uses tables and endpoints that already exist.

---

## 1. What retries by itself, and what does not

A sweep runs every 15 minutes and looks back 7 days. For one business event it will:

| Situation | Automatic behaviour |
|---|---|
| A message was accepted by the provider | Nothing. Complete, and it will never send a second. |
| The failure is **proven** to have happened before anything was dispatched | Retries, with backoff (15m, 30m, 1h, 2h). |
| 5 attempts have been made | Stops. Needs a person. |
| The outcome **cannot be established** | Stops immediately and permanently. Needs a person. |

The last row is the important one. If the SMTP client threw, or a row was left `QUEUED` because the
process died mid-send, the provider may already have taken the message. Sending again could mean the
recipient gets it twice, so the system refuses to guess. PEMS has no delivery webhook, so "accepted by
the provider" is the strongest fact available — there is no proof of *delivery* anywhere in this system.

## 2. Find the affected events

Every attempt is a row in `sent_emails`, written **before** dispatch and updated with the truthful
outcome afterwards. `related_type` / `related_id` name the business event:

| Notification | `related_type` | `related_id` |
|---|---|---|
| Campus rejection | `VisitCampusRejectionEvent` | `audit_logs.audit_log_id` of the `REJECT_CAMPUS_INSTANCE` row |
| Contact-invitation expiry | `VisitRequestIdentityChange` | `visit_request_identity_changes.identity_change_id` |

A rejection is keyed to the **rejection event**, not to the campus, because one campus can be rejected,
resubmitted and rejected again — each of those owes its own message.

Outstanding notifications, newest first:

```sql
SELECT se.sent_email_id, se.related_type, se.related_id, se.status,
       se.error_message, se.last_attempt_at, se.created_at
FROM sent_emails se
JOIN email_templates t ON t.email_template_id = se.email_template_id
WHERE t.template_code IN ('VISIT_CAMPUS_REJECTED', 'VISIT_CONTACT_INVITATION_EXPIRED')
  AND se.created_at >= NOW() - INTERVAL 30 DAY
  AND NOT EXISTS (                      -- nothing for this event ever succeeded
      SELECT 1 FROM sent_emails ok
      WHERE ok.related_type = se.related_type
        AND ok.related_id  = se.related_id
        AND ok.email_template_id = se.email_template_id
        AND ok.status = 'SENT')
ORDER BY se.created_at DESC;
```

The same rows are readable in the UI through `GET /api/emails/viewemail`.

## 3. Classify the attempt — `error_message` says whether a retry is safe

`error_message` carries the machine code in brackets, then the human message. Five classes:

| Class | How it appears in `sent_emails` | Meaning | Auto-retry | Manual retry |
|---|---|---|---|---|
| **SENT** | `status = 'SENT'` | Provider accepted it. PEMS has no delivery webhook, so this is acceptance, not proof of delivery. | never | **never** |
| **PROVEN_NOT_DISPATCHED** | `status = 'QUEUED'` **with** an `error_message` | Skipped: mail is switched off in this environment. Nothing left the process. | yes | safe |
| **CONFIG/RENDER PRE-OUTBOUND** | `status = 'FAILED'` `[SMTP_DISABLED]`, `[SMTP_MISCONFIGURED]`, `[RESEND_MISCONFIGURED]`, `[RESEND_CREDENTIAL_ERROR]`; or **no row at all** (render threw before the row was written) | Refused before the provider was contacted. | yes, after the fault is fixed | safe |
| **OUTCOME_UNKNOWN** | `status = 'FAILED'` `[SMTP_SEND_FAILED]` / `[RESEND_SEND_FAILED]`; or `status = 'QUEUED'` with `error_message` **NULL** | The client threw, or the process died between writing the row and recording the outcome. The provider may already hold the message. | **never** | only after §5 |
| **RETRY_EXHAUSTED** | 5 attempt rows for the same `(template, related_type, related_id)`, none `SENT` | Automatic retry has given up. | stopped | safe once the fault is fixed (§4) |

A row with no bracketed code predates this convention or came from another sender: treat it as
**OUTCOME_UNKNOWN**. An unclassified code is deliberately read as unknown — that is the safe direction
to be wrong in.

Find the exhausted ones — the events that will never move again on their own:

```sql
SELECT se.related_type, se.related_id, COUNT(*) AS attempts, MAX(se.last_attempt_at) AS last_try
FROM sent_emails se
JOIN email_templates t ON t.email_template_id = se.email_template_id
WHERE t.template_code IN ('VISIT_CAMPUS_REJECTED', 'VISIT_CONTACT_INVITATION_EXPIRED')
GROUP BY se.related_type, se.related_id
HAVING SUM(se.status = 'SENT') = 0 AND COUNT(*) >= 5;
```

Both terminal classes are also announced in the application log at `Error` level, naming the template,
the related type and the id.

## 4. Finish a notification that is safe to send

1. **Fix the cause first.** Retrying a misconfigured mail server just spends the attempt budget.
   - `SMTP_DISABLED` / `SMTP_MISCONFIGURED` → `Smtp:Enabled`, `Smtp:Host`, `Smtp:Port` for that environment.
   - `RESEND_*` → the Resend key in API configuration.
   - No row at all → the template: it is missing, or declares a variable the send point does not supply.
2. **Give the sweep back its budget** if it has spent 5 attempts. The attempt count is simply the number
   of rows, so remove the failed attempts for that one event — never rows with `status = 'SENT'`:

   ```sql
   -- Inspect first. Run the SELECT, confirm every row is a FAILED attempt of the ONE event you mean.
   SELECT sent_email_id, status, error_message, last_attempt_at
   FROM sent_emails
   WHERE related_type = 'VisitCampusRejectionEvent' AND related_id = :eventId;

   DELETE ser FROM sent_email_recipients ser
     JOIN sent_emails se ON se.sent_email_id = ser.sent_email_id
   WHERE se.related_type = 'VisitCampusRejectionEvent' AND se.related_id = :eventId
     AND se.status <> 'SENT';

   DELETE FROM sent_emails
   WHERE related_type = 'VisitCampusRejectionEvent' AND related_id = :eventId
     AND status <> 'SENT';
   ```

3. **Wait for the next sweep** (≤15 minutes). It picks the event up on its own; there is nothing to
   trigger. Confirm with the query in §2 — the event should now have a `SENT` row.

Events older than the 7-day look-back are no longer swept. By then the registrant has seen the state in
the dashboard, and a surprise notice about a fortnight-old decision is worse than none; if one genuinely
must go out, send it by hand and say why.

## 5. When the outcome cannot be established

Do **not** clear the rows and let the sweep resend. The recipient may already have the message.

1. Establish what actually happened, from the mail server's own logs for that timestamp and recipient —
   not from PEMS, which is the system that does not know.
2. If the logs show it never left: treat it as §4.
3. If the logs show it did leave, or cannot say: contact the recipient directly rather than sending a
   second automated copy of a rejection or an expiry notice. Both messages are about a decision the
   person may already be acting on, and a duplicate reads as a second decision.

## 6. What must never be done

- Never re-reject a campus to "resend" its email. The rejection is already recorded, the command refuses,
  and a second decision row would be a lie about what the Staff Leader did.
- Never move an invitation from `EXPIRED` back to `PENDING` to re-trigger a notice. The token is
  invalid; reviving it would make an expired invitation acceptable again.
- Never edit `sent_emails.status` to `SENT` to silence an alert. That row is the record of whether a
  person was told.
- Never delete a `SENT` row. It is the only evidence the message went out.

## 7. Concurrency

Recovery is safe to run in several application instances at once: each event is claimed with a MySQL
advisory lock (`GET_LOCK`) for the duration of its send, and the ledger in `sent_emails` is re-checked
under that claim. A manual step taken while a sweep is running is likewise safe — but do the `DELETE` in
§4 when you are not mid-sweep, so you are not removing a row that is about to be written back.
