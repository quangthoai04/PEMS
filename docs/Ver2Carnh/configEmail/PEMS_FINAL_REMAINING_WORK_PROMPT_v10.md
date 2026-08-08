# PEMS — FINAL REMAINING WORK PROMPT v10
## Complete the Remaining v8/v9 Closure Work
### Resubmit Frontend · Profile Sync · Account Tests · Instance Authorization Tests · Amendment · Feedback · Files · Transfer · Recovery Regression · Final Gates

> Repository: `PEMS`
>
> Working branch: `Cảnh-Iter1`
>
> Continue from the **CURRENT local working tree**.
>
> Preserve all current WIP and all completed fixes.
>
> **Do NOT commit unless explicitly requested by the user.**

---

# 0. Purpose

The previous implementation round completed the backend per-instance Resubmit and the email recovery/runbook work.

This prompt covers ONLY the remaining work still explicitly reported as unfinished.

Current confirmed remaining items:

```text
1. PHASE D — Per-instance Resubmit frontend
2. PHASE E — Self-service profile sync
3. PHASE F — ACCOUNT-01..06
4. PHASE G — Backend instance authorization matrix tests
5. PHASE H — Amendment permission tests
6. PHASE I — Feedback permission tests
7. PHASE J — File preview/download permission tests
8. PHASE K — Transfer / Resend / Cancel permission tests
9. PHASE M — Recovery regression completeness
10. Full regression gates + final audit
```

Do not reopen completed architecture unless a regression is found.

---

# 1. Non-negotiable rule — DO NOT GUESS

If an unresolved issue is discovered while implementing the remaining work:

```text
STOP only that subtask
→ show exact code/schema/test evidence
→ explain why the current model is ambiguous
→ show concrete options
→ ASK the user
→ do not invent a business rule
```

Continue independent work.

Do NOT ask again about decisions already confirmed in this prompt.

---

# 2. Confirmed current state — DO NOT REIMPLEMENT

The following are already completed/verified and must be preserved:

```text
- Branch/WIP preservation discipline.
- Existing contact management separated from Visit Request Edit.
- Detail View owns contact management.
- Same-email contact metadata update.
- Changed-email INITIAL_CONFIRMATION / TRANSFER.
- Current A remains contact while B transfer is pending.
- contactFullName fixed.
- Dynamic campus count/source-of-truth fixed.
- Controlled campus selection fixed.
- Edit/Resubmit single-toast behavior fixed.
- Create 72h.
- PRE-APPROVAL Edit 72h.
- Resubmit-after-Reject 72h.
- Approved Amendment excluded from registration 72h.
- Passive <72h has no automatic expiry/email.
- Reject email per-campus.
- Contact-expiry email.
- Reject notification recovery keyed by exact rejection audit/business event.
- Expiry recovery keyed by identityChangeId.
- SMTP outcome classification.
- OUTCOME_UNKNOWN is not automatically retried.
- Retry backoff.
- Attempt cap = 5.
- MySQL GET_LOCK per notification event.
- Post-commit business state remains truthful after notification failure.
- Operator recovery runbook exists.
- Per-instance Resubmit BACKEND exists.
- Per-instance Resubmit backend has 6 passing tests.
```

Do not replace the new instance Resubmit endpoint with the old request-wide endpoint.

---

# 3. Confirmed account lifecycle — preserve

The current canonical security lifecycle is accepted:

```text
GET contact confirmation token detail
→ anonymous masked summary

Accept / Decline
→ authenticated

Therefore:

invited person
→ opens link
→ logs in through canonical SSO
→ account exists/reused
→ accepts invitation
→ binding occurs
```

This is intentionally secure because possessing the invitation link alone is not enough to take control of a campus.

Do NOT:
- add anonymous Accept;
- add password/local authentication;
- add a parallel account provisioning path;
- create account just because email was typed.

Known product limitation:

```text
A contact whose email cannot authenticate through the supported SSO cannot complete acceptance.
```

Do not invent a workaround in this task.

Record it in final debt/constraint only.

---

# 4. Confirmed account eligibility — preserve

Current canonical eligibility:

```text
ACTIVE account
+
normalized email matches invitation target
```

Internal accounts are currently allowed deliberately.

Preserve:
- no duplicate account;
- no silent role conversion;
- no reactivation of inactive users;
- no duplicate VISITOR account for same email.

Do not change this unless new contradictory code evidence is found.

---

# 5. PHASE D — Implement per-instance Resubmit frontend

The backend now exists:

```text
POST /api/v2/visit-requests/{requestId}/instances/{instanceId}/resubmit
```

The frontend must actually use it.

## 5.1 Required actor

Show the instance-level Resubmit action when:

```text
target instance = REJECTED
AND
current user is allowed by backend policy:
- registrant
OR
- current confirmed Operational Contact of THAT instance
```

Do not infer permission from `role == VISITOR`.

## 5.2 Required scope

For an Operational Contact:

```text
HN contact
→ Resubmit HN only
```

Do NOT call the old request-wide Resubmit endpoint.

Sibling instances must not be reset or resubmitted.

## 5.3 Edit + Resubmit UX

The assigned Operational Contact must be able to:

```text
open the rejected target instance
→ edit the allowed instance-local data
→ submit/resubmit the target instance
```

Do not expose request-level/sibling fields that the contact cannot own.

## 5.4 72h failure

If target schedule violates the 72h rule:

show the canonical translated business error.

Do not create a frontend-only alternative rule.

## 5.5 Concurrency

Pass/use the target instance row version expected by the new backend path.

A sibling campus update should not create a false stale conflict for the target instance.

## 5.6 Toast

On successful instance Resubmit:

```text
exactly one success toast
```

No duplicate StrictMode flash.
No refresh/back-forward replay.

## 5.7 Frontend tests

Add at least:

```text
FE-RESUBMIT-01
current HN Operational Contact + HN REJECTED
→ HN Resubmit action visible

FE-RESUBMIT-02
DN sibling contact viewing HN
→ no HN Resubmit action

FE-RESUBMIT-03
random VISITOR
→ no HN Resubmit action

FE-RESUBMIT-04
contact submits
→ calls new /instances/{instanceId}/resubmit endpoint
→ does NOT call old request-wide Resubmit

FE-RESUBMIT-05
success
→ one toast only

FE-RESUBMIT-06
72h backend error
→ correct localized message
```

---

# 6. PHASE E — Implement self-service account profile sync

This is confirmed and must now be built.

Approved global-account sync fields:

```text
full_name
phone
```

Do NOT sync:

```text
email
organization
jobTitle
nationality
role
status
```

unless a separate canonical account rule already independently manages them.

## 6.1 Difference detection

For the currently authenticated account holder:

```text
account.full_name vs current instance contact snapshot fullName
account.phone     vs current instance contact snapshot phone
```

Use canonical normalization for:
- whitespace;
- nullable/empty phone;
- normalized phone representation if the account currently stores one.

Do not generate a false dirty/difference state solely because formatting differs canonically.

## 6.2 Prompt

When at least one approved field differs, show:

> **Thông tin liên hệ trong yêu cầu này khác hồ sơ PEMS của bạn. Bạn có muốn cập nhật hồ sơ cá nhân không?**

Actions:

```text
Giữ nguyên hồ sơ
Cập nhật hồ sơ cá nhân
```

Add equivalent English localization.

## 6.3 Authorization

Only the authenticated account holder may update their own profile.

Explicitly deny:
- registrant updating another contact;
- sibling Operational Contact;
- random VISITOR;
- staff silently updating the contact's personal profile through this flow.

## 6.4 Keep profile

`Giữ nguyên hồ sơ`:

```text
→ no users update
→ no contact snapshot update
→ no identity change
→ no email
→ no status transition
```

The user should not be trapped in a mandatory sync.

## 6.5 Update profile

`Cập nhật hồ sơ cá nhân`:

```text
snapshot.fullName → users.full_name
snapshot.phone    → users.phone
```

Only these two fields.

Must NOT:
- change account email;
- change role/status;
- change organization/jobTitle;
- change contact identity;
- trigger confirmation;
- trigger Transfer;
- invoke 72h;
- create Amendment;
- rewrite any historical contact snapshot.

## 6.6 Snapshot directionality

This action is:

```text
CURRENT SNAPSHOT
→ CURRENT ACCOUNT PROFILE
```

It must NOT perform:

```text
ACCOUNT PROFILE
→ rewrite old request snapshots
```

Historical visit records must remain historically accurate.

## 6.7 Endpoint

Prefer a dedicated self-service account/profile endpoint or existing canonical self-profile update service.

Do NOT implement profile sync through Operational Contact replacement/transfer handlers.

Reuse existing account validation and audit rules.

## 6.8 Tests

Add:

```text
PROFILE-SYNC-01
same profile
→ no prompt

PROFILE-SYNC-02
different full_name
→ prompt

PROFILE-SYNC-03
different phone
→ prompt

PROFILE-SYNC-04
Keep profile
→ no account mutation

PROFILE-SYNC-05
Update profile
→ only full_name + phone updated

PROFILE-SYNC-06
email/org/jobTitle unchanged

PROFILE-SYNC-07
historical snapshots unchanged

PROFILE-SYNC-08
registrant cannot update another person's profile

PROFILE-SYNC-09
random VISITOR cannot update another person's profile

PROFILE-SYNC-10
no confirmation mail / no transfer / no 72h side effect
```

---

# 7. PHASE F — Implement ACCOUNT-01..06

These tests are required even if audit already suggests the behavior is correct.

## ACCOUNT-01 — typed email creates no account

```text
registrant enters a new Operational Contact email
invitation created
```

Expected:

```text
no new user row solely from typed email
```

## ACCOUNT-02 — SSO-before-Accept lifecycle

For a previously unknown invitee:

```text
invitation exists
→ user signs in through canonical SSO
→ account provision/reuse occurs
→ authenticated Accept
→ binding succeeds
```

Prove:
- account exists before binding;
- Accept does not work anonymously;
- correct account is bound.

## ACCOUNT-03 — existing eligible ACTIVE account

Expected:

```text
same normalized email
→ reuse existing UserId
→ no duplicate
→ accept binds existing UserId
```

## ACCOUNT-04 — different snapshot metadata

Existing account:

```text
email same
full_name/phone may differ from snapshot
```

Expected:

```text
same UserId
snapshot remains contextual
global account is NOT automatically overwritten
```

## ACCOUNT-05 — inactive / eligibility

Prove current canonical rule:

```text
inactive account cannot silently be reactivated through contact acceptance
```

Also cover internal account behavior that current code intentionally allows.

Do not silently convert roles.

## ACCOUNT-06 — decline / expiry / cancel

Expected:

```text
pending invitation does not bind contact
no account is created merely by invitation itself
no accidental OperationalContactUserId assignment
```

---

# 8. PHASE G — Full backend instance authorization matrix

The previous audit says the code is already mostly instance-scoped.

Now prove it with tests.

Test setup:

```text
Request R

HN instance:
OperationalContact = A

DN instance:
OperationalContact = B

Random Visitor = C
Registrant = R
```

Backend assertions:

| Action | Registrant | A on HN | B on DN | Random Visitor C |
|---|---:|---:|---:|---:|
| View HN | existing policy | ALLOW | DENY | DENY |
| Edit HN instance-local | existing policy | ALLOW | DENY | DENY |
| Resubmit HN | existing policy | ALLOW | DENY | DENY |
| Feedback HN | existing policy | ALLOW | DENY | DENY |
| Amendment HN | existing policy | ALLOW | DENY | DENY |
| Preview/download HN files | existing policy | ALLOW | DENY | DENY |
| Transfer HN | existing policy | ALLOW if current | DENY | DENY |
| Resend HN transfer | existing policy | ALLOW if current | DENY | DENY |
| Cancel HN transfer | existing policy | ALLOW if current | DENY | DENY |
| Mutate DN by A | existing policy | DENY | — | DENY |
| Add/remove campus | existing policy | DENY | DENY | DENY |
| Approve/Reject | role policy | DENY | DENY | DENY |

Do not test only frontend action visibility.

Test service/handler/controller authorization.

---

# 9. PHASE H — Amendment authorization tests

Confirmed right:

```text
current Operational Contact may create Amendment
for their assigned APPROVED instance
```

Required tests:

```text
AMEND-CONTACT-01
A = current HN contact
HN APPROVED
→ A can create HN Amendment

AMEND-CONTACT-02
A cannot create DN Amendment

AMEND-CONTACT-03
random VISITOR denied

AMEND-CONTACT-04
registration 72h does NOT block Amendment

AMEND-CONTACT-05
canonical Amendment cutoff still applies

AMEND-CONTACT-06
HN Amendment does not mutate sibling campus
```

If current Amendment data model unexpectedly proves request-wide and cannot target HN without a model/schema change:

STOP and ASK with evidence.

Do not silently redesign Amendment.

---

# 10. PHASE I — Feedback / Response authorization tests

Confirmed right:

```text
current Operational Contact may Feedback/Respond
for assigned instance
```

Required tests:

```text
FEEDBACK-CONTACT-01
A can respond for HN

FEEDBACK-CONTACT-02
A cannot respond for DN

FEEDBACK-CONTACT-03
random VISITOR denied

FEEDBACK-CONTACT-04
response remains target-instance scoped
```

If current Feedback persistence is request-wide and a target-instance write cannot be represented safely:

STOP and ASK with evidence.

---

# 11. PHASE J — File preview/download authorization tests

Confirmed right:

```text
current Operational Contact may view/preview/download files
owned by assigned instance
```

Required authorization chain:

```text
fileId
→ actual owning business record
→ owning visitInstanceId
→ currentUser == OperationalContactUserId
```

Tests:

```text
FILE-CONTACT-01
A can preview HN file

FILE-CONTACT-02
A can download HN file

FILE-CONTACT-03
A cannot preview/download DN file

FILE-CONTACT-04
random VISITOR denied

FILE-CONTACT-05
guessing/direct fileId does not bypass authorization
```

If a file is truly request-wide/shared and cannot be assigned to one instance:

STOP and ASK specifically about that file category.

Do not generalize the ambiguity to all files.

---

# 12. PHASE K — Transfer / Resend / Cancel permission tests

These rights are confirmed.

## 12.1 Initiate Transfer

A is current HN contact.

Expected:

```text
A may initiate transfer to B
A remains current while pending
B has no Operational Contact rights yet
```

Tests:
- assigned current contact allowed;
- sibling contact denied;
- random VISITOR denied.

## 12.2 Resend

Current A may resend HN transfer invitation.

Preserve:
- cooldown;
- resend cap;
- token version;
- expiry semantics.

Sibling/random Visitor denied.

## 12.3 Cancel

Current A may cancel HN pending transfer.

Expected:

```text
B pending → CANCELLED
A remains current
```

Sibling/random Visitor denied.

## 12.4 Accept

After B authenticates and accepts:

```text
B becomes current OperationalContactUserId
B receives instance rights
A loses rights derived solely from current-contact relation
```

Test authorization handover explicitly.

## 12.5 Decline / Expiry

Expected:

```text
A remains current
B never receives instance rights
```

---

# 13. PHASE M — Complete recovery regression proof

Do not redesign recovery if current implementation passes.

Add/retain tests proving:

## REJECT-EVENT-01

```text
Reject #1
→ email SENT

target instance Resubmitted

Reject #2
→ first email attempt fails
```

Expected:

```text
Reject #2 remains independently recoverable
Reject #1 SENT cannot suppress it
```

## REJECT-EVENT-02

After Reject #2 recovery succeeds:

```text
later sweep → no duplicate
```

## OUTCOME-UNKNOWN-01

Provider call may have succeeded but DB outcome is uncertain.

Expected:

```text
OUTCOME_UNKNOWN
→ no automatic resend
```

## SAFE-RETRY-01

Failure proven before outbound call.

Expected:

```text
automatic retry allowed
```

## EXHAUSTION-01

Attempt cap reached.

Expected:
- loud observable terminal condition;
- runbook can locate it;
- event is not silently forgotten.

## CONCURRENCY-01

Two recovery workers target same event.

Expected:

```text
DB lock permits one active dispatch attempt
```

## EXPIRY-RECOVERY-01

Expiry committed + mail failure.

Expected:

```text
invitation remains EXPIRED
token remains invalid
safe recovery remains possible according to classification
```

---

# 14. Verify operator recovery runbook, do not rebuild it

Runbook already exists:

```text
docs/Ver2Carnh/configEmail/EMAIL_NOTIFICATION_RECOVERY_RUNBOOK.md
```

Verify it still matches final implementation.

It must accurately document:

```text
SENT
PROVEN_NOT_DISPATCHED
CONFIG/RENDER PRE-OUTBOUND
OUTCOME_UNKNOWN
RETRY_EXHAUSTED
```

and:

```text
OUTCOME_UNKNOWN
→ NEVER blind retry
```

Verify:
- event lookup examples are still valid;
- SQL/examples use current columns;
- attempt cap/backoff text matches code;
- Reject event identity now uses rejection event/audit id;
- Expiry uses identityChangeId;
- no instruction tells operator to replay Reject or revert EXPIRED.

Only update the runbook if implementation/tests expose stale documentation.

---

# 15. Post-commit regression

Prove:

```text
Reject commits
→ notification failure
→ API still reports committed Reject truthfully
```

and:

```text
Expiry commits
→ notification failure
→ EXPIRED remains true
```

Do not send email before business commit.

---

# 16. Preserve registration 72h boundaries

Keep:

```text
Create → 72h
PRE-APPROVAL Edit → 72h
Resubmit after Reject → 72h
```

Operational Contact per-instance Resubmit also uses 72h.

Do NOT apply registration 72h to:

```text
Approved Amendment
Feedback
File access
Contact metadata update
Profile sync
SSO/account login
INITIAL_CONFIRMATION
TRANSFER
Accept/Decline/Resend/Cancel
Expiry
Recovery
Passive time
```

---

# 17. Known product constraint — document only

Production authentication is SSO-only.

An invitee whose email cannot authenticate with the supported SSO cannot accept the authenticated invitation.

Do NOT invent a password/local-auth workaround.

Record this in final debt/constraint.

Do not treat it as a failure of this implementation unless the user later requests alternative authentication.

---

# 18. Stop-and-ask conditions that remain

Ask only if one of these is actually discovered:

```text
1. Per-instance Resubmit unexpectedly requires a new state/schema beyond the implemented backend.
2. Feedback storage is request-wide and cannot be safely scoped.
3. Amendment is structurally request-wide and cannot target one instance.
4. A specific file category is truly request-wide/shared and access ownership is undefined.
5. Profile-sync implementation would require fields beyond confirmed full_name + phone.
6. A schema/table/column change becomes necessary.
7. An existing handler mutates sibling campuses unexpectedly.
8. Existing account eligibility behavior contradicts the audited ACTIVE + email-match rule.
```

Do not ask speculative questions.

---

# 19. Do NOT do these

```text
- do not commit unless explicitly asked
- do not reset or discard WIP
- do not rewrite completed recovery architecture
- do not restore request-wide Resubmit for Operational Contact
- do not authorize by VISITOR role alone
- do not give A access to sibling DN/HCM
- do not create account from typed email
- do not add anonymous Accept
- do not invent password/local authentication
- do not auto-overwrite account profile
- do not sync email/org/jobTitle
- do not rewrite historical snapshots
- do not apply registration 72h to Amendment
- do not bypass file ownership checks
- do not give pending B rights before Accept
- do not auto-retry OUTCOME_UNKNOWN
- do not create new recovery endpoint/UI
- do not create schema without explicit approval when ambiguity remains
```

---

# 20. Execution order

```text
1. Preflight
2. PHASE D — Resubmit frontend
3. PHASE E — Profile sync
4. PHASE F — ACCOUNT-01..06
5. PHASE G — authorization matrix
6. PHASE H — Amendment tests
7. PHASE I — Feedback tests
8. PHASE J — File tests
9. PHASE K — Transfer/Resend/Cancel tests
10. PHASE M — recovery regression
11. Verify runbook
12. Post-commit regression
13. Full gates
14. Final audit
```

---

# 21. Required final report

## 1. Preflight

```text
Branch:
Start HEAD:
End HEAD:
WIP count before/after:
Stashes before/after:
```

## 2. Resubmit frontend

```text
Route/component:
Endpoint called:
Operational Contact visibility:
Sibling denial:
72h UX:
Toast:
```

## 3. Profile sync

```text
Detection:
Prompt:
Endpoint/service:
Fields:
Authorization:
Historical snapshots:
```

## 4. Account tests

Report ACCOUNT-01..06 individually.

## 5. Authorization matrix

Final actual results for:
- registrant;
- assigned Operational Contact;
- sibling Operational Contact;
- random VISITOR.

## 6. Amendment

```text
Current contact allowed:
Sibling denied:
Random Visitor denied:
72h excluded:
Canonical cutoff:
```

## 7. Feedback

```text
Scope:
Current contact:
Sibling:
Random Visitor:
```

## 8. Files

```text
Ownership resolution:
Preview:
Download:
Sibling denial:
Direct fileId denial:
```

## 9. Transfer

```text
Initiate:
Pending rights:
Resend:
Cancel:
Accept handover:
Decline/expiry:
```

## 10. Recovery regression

```text
Repeated Reject:
OUTCOME_UNKNOWN:
Safe retry:
Exhaustion:
Concurrency:
Expiry:
```

## 11. Runbook

```text
Path:
Updated?:
Why:
```

## 12. Changed files

File + reason.

## 13. Gates

Run:

```text
dotnet build
backend unit tests
architecture tests
VisitRequests integration tests
Emails integration tests
frontend typecheck
frontend unit tests
frontend build
```

Do not label failures as pre-existing unless proven against clean baseline.

## 14. Remaining debt / constraints

Include the SSO-only external-contact constraint.

Only real unresolved debt.

Do not claim unimplemented work as complete.

---

# 22. Definition of Done

- [ ] Per-instance Resubmit frontend calls the new instance endpoint.
- [ ] Assigned contact can reach Resubmit from browser.
- [ ] Sibling/random VISITOR cannot.
- [ ] One success toast.
- [ ] 72h error correctly surfaced.
- [ ] Profile sync exists.
- [ ] Profile sync uses only `full_name + phone`.
- [ ] Only account holder can sync.
- [ ] Historical snapshots unchanged.
- [ ] ACCOUNT-01..06 implemented.
- [ ] Full backend authorization matrix tested.
- [ ] Amendment permission tested.
- [ ] Feedback permission tested.
- [ ] File preview/download permission tested.
- [ ] Transfer permission tested.
- [ ] Resend permission tested.
- [ ] Cancel transfer permission tested.
- [ ] Rights handover A → B after Accept tested.
- [ ] Reject repeated-event recovery tested.
- [ ] OUTCOME_UNKNOWN no-auto-retry tested.
- [ ] Safe pre-outbound retry tested.
- [ ] Retry exhaustion tested.
- [ ] DB concurrency recovery tested.
- [ ] Expiry recovery tested.
- [ ] Runbook verified against final implementation.
- [ ] Post-commit semantics verified.
- [ ] Existing 72h/Amendment/contact/campus/toast fixes remain green.
- [ ] Full gates run.
- [ ] Nothing committed unless explicitly requested.
