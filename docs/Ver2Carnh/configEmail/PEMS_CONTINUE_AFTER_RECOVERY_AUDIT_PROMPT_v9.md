# PEMS — Continuation Prompt v9
## Close Remaining v8 Work After Recovery Audit

### Target
- Repository: PEMS
- Branch: `Cảnh-Iter1`
- Continue from the CURRENT local working tree.
- Preserve all uncommitted WIP.
- Do not reset, clean, discard, overwrite, or commit unless explicitly requested.

---

# 0. Current state

Already completed or verified in the previous round:

- Reject recovery now keys on the exact rejection business event/audit-log id.
- Contact-expiry recovery is event-keyed by identityChangeId.
- Email machine codes survive into `sent_emails.error_message`.
- Ambiguous outbound outcomes are not auto-retried.
- Retry backoff exists.
- Automatic retry cap = 5.
- DB `GET_LOCK` prevents concurrent recovery sends for the same event.
- Post-commit notification failure no longer makes a committed mutation look rolled back.
- Visit Request Edit is separated from existing Operational Contact management.
- Same-email contact metadata update exists.
- Changed-email INITIAL_CONFIRMATION / TRANSFER exists.
- `contactFullName` mapping is fixed.
- Dynamic campus/select/toast/72h fixes remain green.

Still NOT complete:

1. Per-instance Resubmit.
2. Self-service account profile sync.
3. ACCOUNT-01..06 tests.
4. Full instance authorization matrix tests.
5. Amendment/File/Transfer/Feedback permission tests.
6. Operator recovery runbook.
7. Account provisioning timing and account eligibility must be resolved correctly before v8 can be considered complete.

---

# 1. Non-negotiable rule: DO NOT GUESS

If current code and confirmed business rules conflict, current code is not automatically the source of truth.

If a real ambiguity remains after audit:

STOP only that subtask
→ show exact evidence
→ explain the conflict/options
→ ASK the user
→ do not implement that unresolved choice

Continue independent work.

Do not ask again about decisions already confirmed below.

---

# 2. Confirmed decisions

Operational Contact after successful confirmation/account binding is a real actor for the assigned Visit Instance.

They are allowed, for the assigned instance only, to:

- View.
- Edit allowed instance-local data.
- Resubmit after Reject.
- Feedback / Respond.
- Create Amendment after approval.
- View / preview / download files of the assigned instance.
- Initiate contact transfer.
- Resend pending transfer.
- Cancel pending transfer.
- Manage their operational-contact metadata.

Authorization must be based on:

`currentUser.UserId == targetInstance.OperationalContactUserId`

not merely `Role == VISITOR`.

Also confirmed:
- Per-instance Resubmit = YES.
- Profile sync fields = `full_name + phone`.
- Operator recovery = runbook over existing surfaces.

Do not ask again whether these choices should be used.

---

# 3. PHASE A — Resolve account provisioning timing

The previous report concluded that accounts are created only by SSO auto-provision at login. That is code evidence, but not necessarily the desired Operational Contact lifecycle.

Audit the actual contact-confirmation acceptance path and answer:

- When a new contact email confirms and no user exists, is an account created immediately?
- Is binding deferred until first Google SSO login?
- Can a confirmed contact exist without `OperationalContactUserId`?
- Can that confirmed person exercise instance permissions before first SSO login?
- What exact state exists between contact confirmation and first login?

Required business outcome:

A new account must NOT be created merely because someone typed an email.

But after successful contact confirmation, the confirmed person must end up with a usable PEMS identity/account binding so they can later sign in and exercise the assigned instance rights.

A valid architecture may be:

typed email
→ no account
→ confirmation succeeds
→ reuse/provision PEMS identity/account
→ bind `OperationalContactUserId`
→ later Google SSO authenticates into that existing account

Do not invent password authentication or a parallel auth system.

STOP AND ASK if the canonical SSO architecture fundamentally requires creation only at first SSO login and changing it would alter the authentication lifecycle. Show:
- `LoginViaSsoCommandHandler`
- contact acceptance handler
- account schema constraints
- current `OperationalContactUserId` binding behavior
and ask which lifecycle to use.

---

# 4. PHASE B — Resolve account eligibility

Previous evidence says `EnsureActorMayTakeContactRole` currently requires ACTIVE + email match and allows internal accounts.

Audit:
- current role model;
- ACTIVE/inactive behavior;
- Visitor restrictions;
- current contact acceptance tests;
- whether internal accounts are intentionally used as Operational Contact.

Do not silently:
- convert internal account to VISITOR;
- create a duplicate VISITOR account for the same email;
- reject an internal account merely because its role is not VISITOR.

If there is no single canonical eligibility rule, ASK the user with evidence before changing it.

---

# 5. PHASE C — Implement per-instance Resubmit

This is confirmed and mandatory.

Current report says Resubmit remains registrant-only and request-wide. Extend/refactor it so an assigned Operational Contact can resubmit only their rejected target instance.

Example:

HN = REJECTED
DN = APPROVED

HN Operational Contact resubmits

Expected:
HN → WAITING_REQUEST_APPROVAL
DN → stays APPROVED

## Authorization

Allow:
- registrant according to existing policy;
- current Operational Contact of the target instance.

Deny:
- random VISITOR;
- sibling Operational Contact.

## Target reset

For the target rejected campus only:
- clear `decided_by`;
- clear `decided_at`;
- clear `decision_note`;
- clear any other canonical decision metadata already reset by existing logic.

Do not clear sibling decision metadata.

## Aggregate recomputation

After target instance re-enters review, recompute request aggregate using the canonical aggregate service.

Do not hardcode aggregate request status.

## 72-hour rule

Because this is Resubmit-after-Reject:

`plannedStart >= serverNow + 72h`

must be enforced for the target submission.

Approved Amendment remains outside registration 72h.

## `resubmission_count`

Audit all usages of the existing request-level `resubmission_count`.

Classify it as:
- audit/display only;
- business validation;
- attempt limit;
- security input;
- dead/unused.

If it is used for business limits and cannot represent per-instance attempts, STOP AND ASK before adding a per-campus counter or schema change.

Do not add schema automatically.

---

# 6. PHASE D — Per-instance Resubmit frontend

For target instance:
- status = REJECTED;
- current user = assigned Operational Contact;

show the correct instance-local Edit/Resubmit flow.

Do not expose a whole-request Resubmit action that mutates siblings.

On success:
- exactly one success toast;
- no flash replay.

Show stable errors for:
- unauthorized contact;
- stale row version;
- target not rejected;
- 72h violation;
- canonical validation errors.

---

# 7. PHASE E — Implement self-service profile sync

Confirmed fields:

`full_name + phone`

Do NOT sync:
- email;
- organization;
- jobTitle.

Email is identity.
Organization/jobTitle remain contextual snapshot data.

## Difference detection

For the authenticated account holder, compare:
- `users.full_name` vs snapshot full name;
- `users.phone` vs snapshot phone;
using canonical normalization.

If different, show:

“Thông tin liên hệ trong yêu cầu này khác hồ sơ PEMS của bạn. Bạn có muốn cập nhật hồ sơ cá nhân không?”

Actions:
- `Giữ nguyên hồ sơ`
- `Cập nhật hồ sơ cá nhân`

## Authorization

Only the authenticated account holder may update their profile.

Registrant cannot do it for the contact.
Staff cannot silently do it.
Sibling contact cannot.

## Update semantics

If user chooses update:
- snapshot full name → `users.full_name`;
- snapshot phone → `users.phone`.

Do NOT:
- change email;
- change role/status;
- copy organization/jobTitle;
- rewrite historical snapshots;
- trigger contact confirmation;
- invoke registration 72h;
- create Amendment.

Historical/current snapshots remain contextual and unchanged.

---

# 8. PHASE F — ACCOUNT-01..06 tests

Write actual tests.

## ACCOUNT-01
Typing a contact email creates no account solely from entered email.

## ACCOUNT-02
Successful confirmation with no account:
test the resolved Phase A lifecycle exactly.

## ACCOUNT-03
Existing eligible account:
reuse same UserId, no duplicate.

## ACCOUNT-04
Same email, different metadata:
same account identity; contextual snapshot preserved; account not auto-overwritten.

## ACCOUNT-05
Incompatible/inactive/internal account:
test the final resolved eligibility rule.

## ACCOUNT-06
Decline/expiry/cancel:
no account created solely from pending email; no unintended binding.

---

# 9. PHASE G — Instance authorization matrix tests

Audit is not enough. Add backend authorization tests.

Use:
- Request R
- HN → Contact A
- DN → Contact B
- Random Visitor C
- Registrant owner

Verify:

| Action | Registrant | A on HN | B on DN | Random Visitor |
|---|---:|---:|---:|---:|
| View HN | existing policy | ALLOW | DENY | DENY |
| Edit HN instance-local | existing policy | ALLOW | DENY | DENY |
| Resubmit HN | existing policy | ALLOW | DENY | DENY |
| Feedback HN | existing policy | ALLOW | DENY | DENY |
| Amendment HN | existing policy | ALLOW | DENY | DENY |
| HN file preview/download | existing policy | ALLOW | DENY | DENY |
| Transfer HN | existing policy | ALLOW if current | DENY | DENY |
| Resend/Cancel HN transfer | existing policy | ALLOW if current | DENY | DENY |
| Mutate DN by A | existing policy | DENY | — | DENY |
| Add/remove campus | existing policy | DENY | DENY | DENY |
| Approve/Reject | role policy | DENY | DENY | DENY |

Tests must exercise backend guards, not merely frontend visibility.

---

# 10. PHASE H — Amendment tests

Permission is confirmed.

Test:
HN APPROVED
A = HN current Operational Contact

A creates an Amendment targeted to HN.

Expected:
- allowed;
- target = HN only;
- sibling campuses unchanged;
- registration 72h does NOT apply;
- canonical Amendment cutoff still applies.

Sibling contact/random Visitor:
- denied.

If the current Amendment model cannot target an instance without data-model change, STOP AND ASK with evidence before changing schema/business model.

---

# 11. PHASE I — Feedback tests

Permission is confirmed.

Test:
- assigned contact can send Feedback/Response for own instance;
- sibling/random Visitor denied;
- no sibling/request-wide side effects.

If current feedback storage is request-wide and cannot be scoped safely, show exact handler/model and ASK.

---

# 12. PHASE J — File preview/download tests

Permission is confirmed.

Authorization chain:

file
→ owning business object
→ `visitInstanceId`
→ current user is target instance Operational Contact

Test:
- A can preview/download HN file;
- A cannot access DN file;
- random Visitor cannot access;
- direct guessed `fileId` cannot bypass authorization.

If a file is truly request-wide/shared and cannot map to an instance, ASK before deciding contact access.

---

# 13. PHASE K — Transfer / Resend / Cancel tests

## Transfer
A current on HN may initiate transfer to B.

While pending:
- A remains current;
- A keeps rights;
- B has no instance rights.

After B accepts:
- B account reuse/provision follows resolved account lifecycle;
- B becomes current;
- A loses contact-based rights.

## Resend
Current A may resend pending transfer subject to canonical cooldown/resend/token rules.
Sibling/random Visitor denied.

## Cancel
Current A may cancel pending transfer.
B → CANCELLED.
A remains current.
Sibling/random Visitor denied.

---

# 14. PHASE L — Create operator recovery runbook

Chosen approach:

`Runbook over existing surfaces`

This is not complete until an actual Markdown runbook exists.

Do NOT build a new endpoint or Admin UI in this phase.

Create a repository-appropriate document such as:

`VISIT_NOTIFICATION_RECOVERY_RUNBOOK.md`

The runbook must document:

## Event lookup
How to locate:
- Reject notification event;
- Contact-expiry event;
- business event id;
- email-history record;
- template code;
- related object/event.

## Failure classification
Explain:
- PROVEN_NOT_DISPATCHED;
- CONFIG/RENDER PRE-OUTBOUND;
- SENT;
- OUTCOME_UNKNOWN;
- RETRY_EXHAUSTED.

## Safe retry
- PROVEN_NOT_DISPATCHED → safe deliberate retry according to runbook.
- OUTCOME_UNKNOWN → never blind retry; human verification/decision required.

## Attempt cap / backoff
Document:
- cap = 5;
- backoff behavior;
- terminal observable condition;
- how to locate an exhausted event.

## Existing surfaces
Document exact email-history endpoint/UI and any deliberate SQL/manual step.

No schema change.

---

# 15. PHASE M — Recovery regression tests

Keep/prove:

Reject #1 SENT
→ Resubmit
→ Reject #2 first attempt fails
→ Reject #2 independently recoverable
→ old Reject #1 SENT cannot suppress it

Also prove:
- OUTCOME_UNKNOWN → no automatic resend;
- pre-outbound config failure → safe retry;
- retry cap reached → loud/observable terminal condition;
- two workers cannot dispatch same event concurrently;
- contact expiry remains EXPIRED after notification failure.

---

# 16. PHASE N — Post-commit regression

Verify:

Reject DB commit succeeds
→ notification render/send fails
→ API still reports committed Reject correctly

Expiry commit succeeds
→ notification fails
→ invitation stays EXPIRED
→ failure remains observable/recoverable

Never send notification before business commit.

---

# 17. Preserve 72h boundaries

Keep:

- Create → 72h.
- PRE-APPROVAL Edit → 72h.
- Resubmit after Reject → 72h.

This includes Operational Contact per-instance Resubmit.

Do NOT apply registration 72h to:
- Approved Amendment;
- Feedback;
- file access;
- contact metadata update;
- account provisioning;
- profile sync;
- INITIAL_CONFIRMATION;
- TRANSFER;
- Resend/Cancel/Accept/Decline;
- expiry;
- email recovery;
- passive time.

---

# 18. Preserve Visit Edit / Contact separation

Existing campus:
- contact read-only in Visit Edit;
- no contact-change button.

Detail:
- owns contact management.

New campus:
- may collect initial contact if required.

Keep tooltip guidance instead of long permanent text.

---

# 19. Remaining stop-and-ask points

Ask only if genuinely unresolved after code audit:

1. Account creation/binding timing after contact confirmation vs first SSO login.
2. Eligibility of internal/inactive/non-VISITOR accounts.
3. Request-level `resubmission_count` has business-limit semantics that cannot represent per-instance attempts.
4. Per-instance Resubmit requires new state/schema.
5. Feedback is request-wide and cannot be scoped safely.
6. Amendment is request-wide and cannot target an instance.
7. File is request-wide/shared and ownership is ambiguous.
8. Any schema/table/column change.
9. Any existing handler unexpectedly mutates siblings/request aggregate.
10. Any account-provisioning change conflicts with canonical SSO activation rules.

Do not ask about permissions already confirmed.

---

# 20. Do NOT do these

- Do not commit unless explicitly asked.
- Do not reset/discard WIP.
- Do not treat current code as final business truth when it conflicts with confirmed requirement.
- Do not authorize by VISITOR role alone.
- Do not resubmit the whole request for one Operational Contact.
- Do not mutate sibling campus on instance Resubmit.
- Do not create account merely when email is typed.
- Do not create duplicate accounts for same normalized email.
- Do not auto-overwrite account profile.
- Do not sync email/organization/jobTitle into account profile.
- Do not rewrite historical snapshots.
- Do not apply registration 72h to Amendment.
- Do not bypass file authorization with fileId.
- Do not give pending B contact rights before acceptance.
- Do not auto-retry OUTCOME_UNKNOWN.
- Do not build a new recovery Admin UI/endpoint in this round.
- Do not create schema without explicit approval if current schema is insufficient.

---

# 21. Suggested execution order

A. Preflight  
B. Resolve account provisioning lifecycle  
C. Resolve account eligibility  
D. Implement per-instance Resubmit backend  
E. Implement per-instance Resubmit frontend  
F. Add Resubmit tests  
G. Implement self-service profile sync  
H. Add ACCOUNT-01..06  
I. Add instance authorization matrix tests  
J. Add Amendment tests  
K. Add Feedback tests  
L. Add File tests  
M. Add Transfer/Resend/Cancel tests  
N. Write operator recovery runbook  
O. Run recovery/post-commit regression  
P. Run full gates  
Q. Final audit  

---

# 22. Final report format

## Preflight
Branch, start/end HEAD, WIP preserved, stashes untouched.

## Account lifecycle
Before confirmation, after confirmation, lookup/provision, binding, first SSO login.

## Account eligibility
ACTIVE, VISITOR, internal, inactive, email-match rules.

## Per-instance Resubmit
Old path, new path, authorization, decision reset, sibling behavior, aggregate recomputation, 72h, `resubmission_count`.

## Profile sync
Prompt, fields, authorization, historical snapshot behavior.

## Instance authorization
Full matrix for registrant, assigned contact, sibling contact, random VISITOR.

## Amendment
Target scope, authorization, 72h exclusion, canonical cutoff.

## Feedback
Scope, authorization, sibling isolation.

## Files
Ownership resolution, preview/download, sibling denial.

## Transfer
Initiate, pending A/B, resend, cancel, accept, rights handover.

## Recovery runbook
File path, event lookup, machine-code classification, safe retry, OUTCOME_UNKNOWN, exhaustion.

## Recovery verification
Repeated Reject, expiry, ambiguous outcome, safe retry, concurrency.

## Changed files
File + reason.

## Tests/gates
- dotnet build
- backend unit
- architecture
- VisitRequests integration
- Emails integration
- frontend typecheck
- frontend unit
- frontend build

## Remaining blocked decisions/debt
Only real unresolved items.

Do not report unimplemented work as done.

---

# 23. Definition of Done

- Account lifecycle after contact confirmation explicitly resolved.
- Account eligibility explicitly resolved.
- Per-instance Resubmit exists backend + frontend.
- Operational Contact can Resubmit only own rejected instance.
- Sibling states remain unchanged.
- Target decision metadata cleared correctly.
- Request aggregate recomputed canonically.
- 72h enforced on contact Resubmit.
- `resubmission_count` audited safely.
- Profile sync exists for `full_name + phone`.
- Only account holder can sync.
- Historical snapshots unchanged.
- ACCOUNT-01..06 implemented.
- Backend instance authorization matrix tested.
- Amendment permission tested.
- Feedback permission tested.
- File permission tested.
- Transfer/Resend/Cancel permissions tested.
- Operator recovery runbook exists.
- Reject recovery remains business-event keyed.
- Expiry recovery remains event-based.
- OUTCOME_UNKNOWN is not auto-retried.
- Retry exhaustion remains observable.
- Post-commit business truth remains correct.
- No regression to 72h/Amendment/contact/campus/toast fixes.
- Nothing committed unless explicitly requested.
