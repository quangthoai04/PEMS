# PEMS — Follow-up Cleanup Prompt
## Legacy Operational-Contact Data Repair + Transfer Audit Scope + Country Data Parity

Repository: `quangthoai04/PEMS`

## Mission

Continue from the operational-contact confirmed-handover fix already implemented in the current working branch. Do **not** redo or revert that fix.

Resolve the remaining three findings safely:

1. legacy rows that may already have been corrupted by the old destructive confirmed-contact REPLACE behavior;
2. incomplete campus scoping on `OPERATIONAL_CONTACT_TRANSFER_REQUESTED` audit rows;
3. the remaining `countryDataParity.test.ts` frontend failure.

Before editing, inspect the **current working tree, current branch, and latest remote `Dev` HEAD**. Do not reset the current branch to an older baseline. Preserve any uncommitted/branch-local handover changes.

---

# 1. Non-negotiable invariants

The confirmed-handover rule must remain:

```text
No confirmed holder + changed identity => REPLACE
Confirmed holder + changed identity    => TRANSFER

Pending TRANSFER:
- current holder remains current
- FormDetail current-contact snapshot remains current holder
- campus/request status remains unchanged by initiation/cancel/decline/expiry
- only ACCEPT moves old holder -> new holder
```

Do not weaken any existing permission, lifecycle, multi-campus, token, history, notification, approval, host, amendment, or contact-gate rule merely to make tests pass.

---

# 2. Fix A — transfer audit scope completeness

Inspect:

`backend/PEMS.Application/Delegations/Commands/OperationalContact/InitiateOperationalContactTransferCommandHandler.cs`

The `AuditLog` written for `OPERATIONAL_CONTACT_TRANSFER_REQUESTED` must be explicitly campus scoped. Verify the latest code. If still missing, set BOTH:

```csharp
CampusId = instance.CampusId,
VisitInstanceId = instance.VisitInstanceId,
```

Do not assume only `VisitInstanceId` is missing.

The final audit must have:

```text
EntityType = VisitRequestCampus
EntityId = VisitInstanceId
VisitRequestId = request id
CampusId = target campus id
VisitInstanceId = target instance id
SourceType = IDENTITY
CorrelationId = same mutation correlation id
```

Do not create a duplicate audit row. Fix the existing writer.

Use `ReplaceOperationalContactCommandHandler` and other canonical campus-scoped audit writers as the reference.

### Tests

Add/update integration coverage that creates a transfer and asserts the persisted audit row has the correct `VisitRequestId`, `VisitInstanceId`, `CampusId`, `EntityId`, `Action`, and `SourceType`.

Add a multi-campus assertion proving one campus's transfer audit cannot be surfaced as another campus's history.

---

# 3. Fix B — legacy corrupted confirmed-contact rows

Handle this as a **forensic repair**, not a blind migration.

## Historical corruption pattern

The old bug could have produced:

```text
A was a confirmed holder
campus = WAITING_REQUEST_APPROVAL
registrant changed A -> B
old code ran destructive REPLACE
OperationalContactUserId became null
FormDetail was overwritten with B
campus moved to WAITING_CONTACT_CONFIRMATION
B invitation later CANCELLED / DECLINED / EXPIRED / SUPERSEDED
```

The new code prevents future corruption, but existing rows may remain.

## First deliverable: detector / dry-run

Before writing any repair, inspect immutable evidence sources in the latest schema/code:

- `visit_request_identity_changes`
- `visit_request_identity_change_events`
- `audit_logs`
- `audit_log_changes`
- `visit_instance_form_revision_history`
- `visit_request_revision_history`
- current `visit_request_campuses`
- current `visit_instance_form_details`
- relevant user/account rows

Implement a **read-only detector** that classifies candidate rows as:

```text
SAFE_AUTO_REPAIR
MANUAL_REVIEW
NOT_CORRUPTED
```

Do not select a row merely because `OperationalContactUserId IS NULL`; that is a valid initial-confirmation state.

Prefer strong evidence such as a campus-scoped `OPERATIONAL_CONTACT_REPLACED` audit whose change rows prove:

```text
operational_contact_user_id:
old value = non-null A
new value = null
```

combined with the replacement invitation for B ending non-applied.

## Safe auto-repair criteria

Only classify as `SAFE_AUTO_REPAIR` when all of these are proven:

1. exact request/campus/instance;
2. unique prior confirmed holder A;
3. destructive replace occurred before this handover fix;
4. B never became the valid applied holder;
5. no later legitimate contact replacement/transfer/profile mutation makes restoring A stale;
6. lifecycle state still permits a technical restoration without falsifying history;
7. DB triggers will accept the restored state;
8. the **exact old FormDetail contact snapshot** can be reconstructed from immutable pre-change evidence.

Do **not** reconstruct historical contact fields from A's current user profile. A's current name/phone/organization may differ from the old visit snapshot.

If the exact old snapshot cannot be reconstructed, classify as `MANUAL_REVIEW`.

## Forbidden shortcuts

Do not:

```text
restore every CANCELLED/DECLINED/EXPIRED invitation
take OldUserId then overwrite FormDetail from today's Users row
restore the latest non-null contact found somewhere in history
```

## Repair execution

If deterministic safe cases exist, implement an idempotent repair path with:

```text
DRY RUN by default
explicit APPLY mode
transactional execution
no notification email
no invitation creation
no token minting
no fake business event implying the user changed contact now
```

Restore only state proven to have been destroyed by the old bug, including where recoverable:

```text
OperationalContactUserId = A
OperationalContactConfirmedAt = exact prior value
OperationalContactConfirmationSource = exact prior value
FormDetail contact snapshot = exact pre-corruption snapshot
campus status = state consistent with confirmed holder
request aggregate = recomputed through canonical aggregate logic
```

Never hard-code request aggregate status.

If prior confirmation timestamp/source cannot be proven and current invariants require them, do not invent values; send the row to manual review.

## Repair audit

Technical repair must be separately identifiable, using an existing canonical recovery action if one exists, otherwise a dedicated source/action such as `LEGACY_CONTACT_REPAIR`.

Capture target request/campus/instance, fields restored, evidence IDs/correlation IDs, reason, and timestamp.

Do not surface the repair as if it were a new user business action.

## Tests

Seed at least:

- safe corrupted case -> detector says SAFE; dry-run no-op; APPLY restores A exactly; second APPLY idempotent;
- later legitimate successor -> no restore;
- old user id recoverable but exact old snapshot missing -> MANUAL_REVIEW;
- normal initial-confirmation case -> NOT_CORRUPTED;
- multi-campus request -> only target campus repaired.

---

# 4. Fix C — frontend country parity failure

Do not delete, skip, `.todo`, weaken, or blindly update the test.

Inspect:

- `frontend/pems-react/src/test/countryDataParity.test.ts`
- `frontend/pems-react/src/shared/utils/countryNames.ts`
- `frontend/pems-react/scripts/generate-country-data.ts`
- `backend/PEMS.Domain/Data/countries.json`
- package scripts

## Reproduce first

Run:

```bash
npx vitest run src/test/countryDataParity.test.ts
```

Capture the exact failing assertion.

The test is a deliberate FE/BE parity guard. If the generated backend snapshot is stale, regenerate it using the repository's canonical generator/package script rather than hand-editing the JSON.

Expected generator is equivalent to:

```bash
npx tsx scripts/generate-country-data.ts
```

Then inspect the generated `countries.json` diff:

- every change must be explainable from current `countryNames.ts`;
- no unexpected country removal;
- no unintended alias reassignment;
- UTF-8/newline formatting must match the generator.

Run the parity test again.

If the failure is a real alias/country-source conflict rather than stale generation, fix the canonical source/generator, add a targeted test, regenerate JSON, and rerun. Do not change expected assertions simply to make the suite green.

---

# 5. Regression gates

Run targeted backend tests for:

- operational contact management;
- lifecycle lock;
- confirmation workflow;
- per-campus read;
- visit request/history detail;
- legacy detector/repair;
- multi-campus privacy.

Then run full:

```text
PEMS.UnitTests
PEMS.IntegrationTests
PEMS.ArchitectureTests
```

Frontend:

```text
countryDataParity.test.ts
visitV2Actions tests
ContactIdentityActions tests
CampusVisitDetail tests
full vitest
tsc
lint
build
```

This task specifically includes the country parity failure, so the final frontend suite must be green for the correct reason.

---

# 6. Scope discipline

Do not change unrelated:

- host transfer;
- participant assignment;
- logistics email workflows;
- amendments;
- approval semantics;
- notification targeting;
- visitor authentication;
- country UX beyond what parity requires.

Do not add a DB migration just for the transfer audit fields; those columns already exist.

For legacy repair, prefer a maintenance/backfill mechanism over a schema change.

---

# 7. Final report

Return:

1. current branch + exact HEAD;
2. root cause of each remaining issue;
3. files changed;
4. transfer-audit before/after proof;
5. exact country test failure and exact correction;
6. legacy detector counts: scanned / candidates / SAFE_AUTO_REPAIR / MANUAL_REVIEW / NOT_CORRUPTED;
7. if APPLY was run, exact number of rows repaired and evidence for each;
8. exact test pass/fail counts;
9. confirmation that confirmed-holder handover semantics remain intact;
10. any legacy candidates intentionally left for manual review.

# Definition of Done

- [ ] Transfer audit has `VisitRequestId`, `CampusId`, and `VisitInstanceId`.
- [ ] Transfer audit is correctly campus scoped in history/privacy tests.
- [ ] Legacy corruption detector is read-only by default.
- [ ] Detector separates safe repair from ambiguous/manual review.
- [ ] No repair guesses historical contact fields from a current profile.
- [ ] Safe repair is transactional and idempotent.
- [ ] Repair sends no invitations/notifications.
- [ ] Multi-campus isolation holds.
- [ ] `countryDataParity.test.ts` passes for the correct reason.
- [ ] `countries.json` is synced via the canonical generator.
- [ ] Full frontend suite is green.
- [ ] Full backend suites are green.
- [ ] Confirmed-holder handover semantics remain green.
