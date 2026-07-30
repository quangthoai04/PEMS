# Rollback — `email_templates.revision`

## Short answer

Rolling this back means dropping one column:

```sql
SET @pems_guard_confirm_database = 'the_database_name';
-- confirm it matches before running the next line
SELECT DATABASE();

ALTER TABLE email_templates DROP COLUMN revision;
```

No data is lost by dropping it. The column holds a write counter, not content: it
describes *how many times* a template has been saved, never *what* it says.

## Do not roll this back while the new backend is running

The order matters, and it is the reverse of the deploy order.

The backend issues every template content write as a conditional UPDATE carrying
`AND revision = :expected`. If the column is gone, that statement is a SQL error, and
**every** template save and restore fails — not silently, but not gracefully either.

So:

1. Roll the **backend** back first, to a build that predates G11 final closure.
2. Then drop the column, if you actually need it gone.

If you only need to stop using the feature, you do not need step 2 at all. An unused
extra column costs four bytes per row on thirty rows.

## What rolling back gives up

The old mechanism is what the column replaced, so a rollback restores its defect:
`updated_at` is `DATETIME` with no fractional part, so two saves landing inside the
same second store an identical stamp, compare equal, and the second silently
overwrites the first. Nobody sees an error; one person's wording is simply gone.

Restore-to-default also stops working, because it requires a version to restore
against — restoring without one is an unconditional overwrite of whatever a colleague
may have saved in the meantime.

## Re-applying after a rollback

`02_up_add_revision.sql` is idempotent and can simply be run again. Existing rows get
`revision = 1`, which the application treats as a valid starting point — it compares
revisions, it never assumes a particular value.

The one thing a re-apply cannot recover is the *history* of edits made while the column
was absent. That history was never in this column; it is in `audit_logs` for restores,
and nowhere at all for ordinary edits. If you need per-edit history of template content,
that is a separate change and a bigger one.

## Verifying either direction

`03_verify.sql` answers "is the column present and does the conditional write behave",
and raises `SQLSTATE 45000` if not, so it can gate a deploy.

After a **rollback**, expect it to FAIL — that is the correct result, and it confirms the
column really is gone rather than merely renamed.
