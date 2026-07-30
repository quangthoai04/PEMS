# Rollback guidance — `contact_guard_closure` (G12)

## What this migration did

It replaced the bodies of exactly five triggers. It created no table, altered no column, added no
index or constraint, and wrote no row. Nothing it did is data-destructive, so there is no data to
restore and no backup to reload for the migration itself.

| Trigger | Table | Timing |
|---|---|---|
| `trg_visit_requests_primary_contact_guard_bi` | `visit_requests` | BEFORE INSERT |
| `trg_visit_requests_primary_contact_guard_bu` | `visit_requests` | BEFORE UPDATE |
| `trg_users_protect_active_primary_contact_bu` | `users` | BEFORE UPDATE |
| `trg_visit_request_identity_changes_user_guard_bi` | `visit_request_identity_changes` | BEFORE INSERT |
| `trg_visit_request_identity_changes_user_guard_bu` | `visit_request_identity_changes` | BEFORE UPDATE |

## Read this before deciding to roll back

The pre-G12 bodies **also rejected every invalid relation**. What they did badly was *report*:

- A VISITOR whose account was still `PENDING_EMAIL_CONFIRMATION` produced
  `22001 Data too long for column 'v_user_status'` instead of
  `45000 PRIMARY_CONTACT_VISITOR_ACCOUNT_INACTIVE`. Every account passes through that state, so this
  was reachable in normal operation, not a corner case.
- A user whose role row could not be read was reported as `PRIMARY_CONTACT_USER_NOT_FOUND`, which is
  false and sends whoever is debugging it to the wrong place.
- `trg_users_protect_active_primary_contact_bu` compared a role code that a zero-row
  `SELECT ... INTO` had left NULL. `NULL <> 'VISITOR'` is UNKNOWN, and `IF` treats UNKNOWN as false,
  so on that path the guard stopped guarding.

Rolling back reinstates all three. **The application is expected to depend on the stable codes**, so
a rollback is a behavioural regression for callers, not a return to a neutral state.

## When a rollback is genuinely the right call

Only one case: the hardened guard rejects a write that your data model says is legitimate, and you
need the system moving again before the root cause can be fixed properly. That means the guard found
a real invariant violation in existing data — see "If the guard starts rejecting live writes" below,
which is almost always the better path.

## How to roll back

Re-create the five triggers from the canonical script as it stood before G12:

```bash
git show <pre-G12-commit>:docs/database/scripts/PEMS_FULL_V2_NO_SEED_DATA_GALLERY_DOCUMENT_AI_FIXED.sql \
  > /tmp/canonical_pre_g12.sql
```

Extract the block that begins at `CREATE TRIGGER trg_visit_requests_primary_contact_guard_bi` and
ends at the `END$$` closing `trg_visit_request_identity_changes_user_guard_bu`, then apply it with
the same `DROP TRIGGER IF EXISTS` preamble and `DELIMITER $$` framing used by
`02_up_replace_triggers.sql`.

Do **not** simply drop the five triggers and stop. That leaves the tables with no guard at all, and
the application does not re-implement these checks — invalid primary-contact relations would start
being written and would then have to be found and repaired by hand.

After a rollback, `03_verify.sql` will exit non-zero. That is correct and expected: it is asserting
the hardened bodies, which are no longer installed.

## If the guard starts rejecting live writes after the migration

This means the database already held rows that violate the invariant, and the pre-G12 guard's
reporting was hiding which ones. Do not roll back to make the symptom go away.

1. Run `01_preflight.sql`. Its five `issue_count` queries name the violating shape.
2. Repair those rows deliberately — reassign the primary contact to an ACTIVE VISITOR, or move the
   request back to `PENDING_CONFIRMATION` with `visitor_user_id` NULL.
3. Re-run `01_preflight.sql` until every `issue_count` is 0.

The migration itself repairs nothing on purpose: silently rewriting ownership of a visit request is
not something a schema migration should decide.

## Verifying either direction

`03_verify.sql` SIGNALs on any FAIL, so the mysql client exits non-zero. Use the exit code — it is a
gate, not a report to read by eye.

```bash
mysql -u<user> -p <database> < 03_verify.sql; echo "exit=$?"
```

Expected after a successful migration: 34 PASS, 0 FAIL, exit 0.
