# PEMS — Operational Contact Confirmed-Handover Unification
## Audit + Implementation Prompt (2026-08-21)

> **Repository:** `quangthoai04/PEMS`  
> **Baseline branch:** `Dev`  
> **Baseline HEAD verified before writing this prompt:** `65e905621939e82d47a771476ec62f3a1343779a`  
> **Baseline commit message:** `fix(visit): refine save-approve flow and streamline reception UI`

---

# 0. Mission

Audit the **latest `Dev` code first**, then implement the following business-rule correction without regressing any existing PEMS visit, permission, multi-campus, confirmation, approval, history, notification, token, lifecycle, or frontend behavior.

The defect to fix is specifically this inconsistency:

- A campus already has a **confirmed operational contact A**.
- The campus is still `WAITING_REQUEST_APPROVAL`.
- The registrant changes the operational-contact email from A to B.
- Current code classifies the operation by **campus decision status**, so it uses `ReplaceOperationalContact`.
- That immediately rewrites the persisted contact snapshot to B, clears A's `OperationalContactUserId`, moves the campus back to `WAITING_CONTACT_CONFIRMATION`, and then invites B.
- If the invitation to B is cancelled, declined, or expires, A is not restored because A was already removed before B accepted.

The new rule is:

> **Once a campus has a confirmed operational-contact holder, changing to a different email is always a handover/TRANSFER. The current holder remains the current holder until the invited replacement accepts.**

Do not implement this as "restore A on cancel".  
The correct invariant is stronger:

> **A is never removed in the first place until B accepts.**

---

# 1. Mandatory source-code audit before editing

Do not blindly apply this document as a patch blueprint. First re-read the current `Dev` versions of at least these files and verify whether HEAD changed after the SHA above:

## Backend routing / contact commands

- `backend/PEMS.Application/Delegations/Commands/OperationalContact/SaveOperationalContactCommandHandler.cs`
- `backend/PEMS.Application/Delegations/Commands/OperationalContact/ReplaceOperationalContactCommandHandler.cs`
- `backend/PEMS.Application/Delegations/Commands/OperationalContact/InitiateOperationalContactTransferCommandHandler.cs`
- `backend/PEMS.Application/Delegations/Commands/OperationalContact/AcceptOperationalContactConfirmationCommandHandler.cs`
- `backend/PEMS.Application/Delegations/Commands/OperationalContact/DeclineOperationalContactConfirmationCommandHandler.cs`
- `backend/PEMS.Application/Delegations/Commands/OperationalContact/ManageOperationalContactHandlers.cs`
- `backend/PEMS.Application/Delegations/Commands/OperationalContact/ResendOperationalContactConfirmationCommandHandler.cs`
- the current reinvite handler for `ReinviteOperationalContactConfirmationCommand`
- `backend/PEMS.Application/Delegations/Commands/OperationalContact/OperationalContactGuards.cs`
- `backend/PEMS.Application/Delegations/Commands/OperationalContact/OperationalContactContracts.cs`

## Read model / allowed actions / aggregate status

- `backend/PEMS.Application/Delegations/Services/VisitFormRead/VisitFormReadService.cs`
- `backend/PEMS.Application/Delegations/Services/VisitRequestAggregateStatusService.cs`
- `backend/PEMS.Domain/Constants/VisitFormActions.cs`
- `backend/PEMS.Application/Delegations/Common/VisitRequestOwnership.cs`

## Approval / lifecycle interaction

- `backend/PEMS.Application/Delegations/Services/CampusApprovalExecutor.cs`
- the handlers/services that move a campus from `ASSIGNED` → `BEFORE_VISIT` → `DURING_VISIT`
- any DB trigger / migration SQL that enforces campus lifecycle transitions or contact invariants

## Identity-change persistence

- `backend/PEMS.Domain/Entities/Delegations/VisitRequestIdentityChange.cs`
- `backend/PEMS.Application/Delegations/Common/PendingContactSnapshot.cs`
- `backend/PEMS.Infrastructure/Services/OperationalContactInvitationService.cs`
- `backend/PEMS.Infrastructure/Services/OperationalContactMaintenanceService.cs`
- relevant `ApplicationDbContext` mappings / constraints
- database migration scripts/triggers involving:
  - `visit_request_identity_changes`
  - `visit_request_campuses`
  - `visit_instance_form_details`
  - pending invitation uniqueness

## Frontend

- `frontend/pems-react/src/features/visit-request/components/ContactIdentityActions.tsx`
- `frontend/pems-react/src/features/visit-request/components/v2/CampusVisitDetailCard.tsx`
- `frontend/pems-react/src/features/visit-request/components/v2/OperationalContactReadOnly.tsx`
- `frontend/pems-react/src/features/visit-request/api/visitRequestV2Api.ts`
- `frontend/pems-react/src/features/visit-request/utils/visitV2Actions.ts`
- all i18n keys used by the contact-management UI

## Tests

At minimum inspect:

- `tests/PEMS.IntegrationTests/VisitRequests/OperationalContactManagementTests.cs`
- `tests/PEMS.IntegrationTests/VisitRequests/OperationalContactLifecycleLockTests.cs`
- `tests/PEMS.IntegrationTests/VisitRequests/OperationalContactConfirmationWorkflowTests.cs`
- `tests/PEMS.IntegrationTests/VisitRequests/PerCampusFormV2ReadTests.cs`
- history tests that map operational-contact identity events
- `frontend/pems-react/src/features/visit-request/__tests__/ContactIdentityActions.test.tsx`

Before changing code, state whether any current implementation differs materially from the assumptions in this document.

---

# 2. Canonical business invariant after the fix

Identity-change classification must be based on **whether the campus currently has a confirmed contact holder**, not primarily on whether the campus has already been approved.

Use these semantics:

| Current state | Submitted change | Canonical workflow |
|---|---|---|
| No confirmed contact holder | Same normalized email | Profile/update or reinvite behavior as currently defined |
| No confirmed contact holder | Different normalized email | `REPLACE` / fresh `INITIAL_CONFIRMATION` |
| Confirmed holder A exists | Same normalized email | Metadata/profile update only |
| Confirmed holder A exists | Different normalized email B | `TRANSFER` / handover |
| Transfer B pending | Cancel | Keep A unchanged |
| Transfer B pending | Decline | Keep A unchanged |
| Transfer B pending | Expire | Keep A unchanged |
| Transfer B pending | Accept | Atomically replace A with B |

The key predicate is conceptually:

```csharp
var hasCurrentConfirmedContact = instance.OperationalContactUserId is not null;
```

Do not use only `WAITING_REQUEST_APPROVAL` vs `ASSIGNED/BEFORE_VISIT` to decide REPLACE vs TRANSFER.

---

# 3. The exact before/after behavior to achieve

## 3.1 Confirmed A, campus still waiting for approval

Initial state:

```text
campus.status = WAITING_REQUEST_APPROVAL
current contact = A
OperationalContactUserId = A
FormDetail contact snapshot = A
A confirmed = true
```

Registrant edits A → B.

### Current broken behavior

```text
A removed immediately
OperationalContactUserId = null
FormDetail snapshot overwritten with B
campus.status = WAITING_CONTACT_CONFIRMATION
B receives INITIAL_CONFIRMATION
```

### Required behavior

```text
current contact remains A
OperationalContactUserId remains A
FormDetail snapshot remains A
campus.status remains WAITING_REQUEST_APPROVAL

a TRANSFER invitation is created:
OldUserId = A
OldEmailNormalized = A email
NewEmailNormalized = B email
PendingSnapshotJson = B's proposed details
Status = PENDING
```

B is **not** the current contact yet.

---

## 3.2 If B accepts

Only at successful acceptance:

```text
OperationalContactUserId: A -> B
FormDetail snapshot: A -> B
confirmation source -> TRANSFER
confirmed timestamp -> acceptance time
transfer row -> APPLIED
```

For a transfer that was initiated while the campus was `WAITING_REQUEST_APPROVAL`:

```text
campus.status remains WAITING_REQUEST_APPROVAL
```

Do not fabricate an approval or decision.

For a transfer accepted after the campus has since become `ASSIGNED` or `BEFORE_VISIT`:

- swap only the operational contact;
- preserve campus decision;
- preserve host;
- preserve planned schedule;
- preserve amendments;
- preserve sibling campuses.

---

## 3.3 If B cancels / declines / expires

Required result:

```text
A remains current contact
A's FormDetail snapshot remains unchanged
A's OperationalContactUserId remains unchanged
campus status remains unchanged
request aggregate remains unchanged
B's invitation is merely settled
```

There must be no restoration transaction because no destructive replacement occurred.

---

# 4. Global confirmation gate decision — CHOSEN RULE

A pending transfer must **not close the global contact-confirmation gate**.

Reason:

- the campus still has a valid confirmed holder A;
- B is only a proposed replacement;
- there is no period in which the campus lacks an accountable contact.

Therefore:

```text
confirmed A + pending transfer B
=> HasOperationalContact = true
```

`VisitRequestAggregateStatusService` currently derives this from:

```csharp
c.OperationalContactUserId is not null
```

Prefer preserving that aggregate rule.

Do **not** change request status to `PENDING_CONTACT_CONFIRMATION` merely because a transfer exists.

Do **not** bump `ContactGateRevision` merely because a transfer is initiated, cancelled, declined, or expires.

Add regression tests proving this.

---

# 5. Approval while a transfer is pending — CHOSEN RULE

Because A remains a valid confirmed contact, a Staff Leader may still approve the campus while B's transfer invitation is pending, subject to all existing approval rules.

Example:

```text
A confirmed
campus WAITING_REQUEST_APPROVAL
transfer A -> B is PENDING
Staff Leader approves campus
campus becomes ASSIGNED
A is still current contact
transfer remains PENDING
```

Then:

- if B accepts before the visit starts: transfer may apply and B becomes current contact;
- if the visit reaches a lifecycle state where transfer is no longer allowed before B accepts: acceptance must be refused by the existing lifecycle lock;
- cancel/decline cleanup must still remain possible after the visit starts.

Do not make pending transfer itself an approval blocker.

---

# 6. Backend implementation requirements

## 6.1 `SaveOperationalContactCommandHandler`

Current router behavior is status-oriented.

Change classification to identity-holder-oriented.

Required conceptual routing:

```csharp
if (Normalize(incomingEmail) == Normalize(storedEmail))
{
    // metadata/profile correction
    UpdateOperationalContactProfile;
}
else if (currentOperationalContactUserId is not null)
{
    // a real confirmed holder exists
    InitiateOperationalContactTransfer;
}
else
{
    // nobody currently holds the campus
    ReplaceOperationalContact;
}
```

Important:

- still reload and re-authorize inside destination handlers;
- do not make the router the only enforcement layer;
- no direct business mutation should be added to the router;
- if database state is inconsistent, destination guards must fail safely.

The router projection must therefore load the current contact relation in addition to the stored email/status facts it currently reads.

---

## 6.2 `ReplaceOperationalContactCommandHandler`

Make REPLACE strictly mean:

> "There is currently no confirmed holder, and the registrant is replacing an unconfirmed candidate / invitation."

Defense-in-depth requirement:

- after loading the tracked instance, explicitly refuse REPLACE if `OperationalContactUserId != null`;
- do not permit a direct API call to bypass the new router and destructively replace a confirmed holder;
- return the existing stable contact-change conflict code unless the codebase already has a more precise stable code.

Preserve existing legitimate REPLACE behavior when no current holder exists:

- supersede an old pending INITIAL_CONFIRMATION for that campus;
- rewrite the unconfirmed contact snapshot;
- create a fresh INITIAL_CONFIRMATION for the new address;
- self-match to the verified registrant may still link immediately if that behavior is currently valid;
- keep multi-campus scope isolated to this `VisitInstanceId`.

Do not change REPLACE into a rollback-capable workflow.

---

## 6.3 `InitiateOperationalContactTransferCommandHandler`

Redefine TRANSFER as:

> "Handover from an existing confirmed holder to a proposed replacement before the visit has started."

It is no longer limited semantically to "after campus decision".

Keep these existing invariants:

- a current holder must exist;
- `OldUserId` captures the holder at invitation creation;
- `OldEmailNormalized` captures the current holder's snapshot email;
- proposed B details go only into `PendingSnapshotJson`;
- B's address goes into `NewEmailNormalized/NewEmailMasked`;
- current `FormDetail` stays A until acceptance;
- current `OperationalContactUserId` stays A until acceptance;
- campus status stays unchanged;
- request status stays unchanged;
- host / decision / schedule stay unchanged;
- one PENDING identity change per campus remains enforced;
- token minting remains atomic with the pending identity change;
- delivery remains post-commit/best-effort as currently designed.

Do not create a second "pre-approval transfer" command if the existing TRANSFER model can safely represent it.

---

## 6.4 `OperationalContactGuards.EnsureTransferWindowOpen`

The existing guard currently allows only decided-not-started states (`ASSIGNED`, `BEFORE_VISIT`) and explicitly rejects `WAITING_REQUEST_APPROVAL`.

Update the transfer lifecycle rule carefully.

A transfer should be applicable when:

1. request is live;
2. a confirmed current holder exists;
3. campus has not started;
4. campus is in a supported pre-start lifecycle state.

At minimum the intended supported states are:

```text
WAITING_REQUEST_APPROVAL
ASSIGNED
BEFORE_VISIT
```

Do not allow transfer mutation in:

```text
WAITING_CONTACT_CONFIRMATION
REJECTED
CANCELLED
DURING_VISIT
AFTER_VISIT
CLOSED
```

unless current codebase semantics discovered during audit prove a different canonical state set.

The same lifecycle predicate must be re-used/re-tested at every point that can make a transfer meaningful:

- initiate;
- accept;
- resend;
- read-model action emission.

Do not reintroduce a clock-based "24-hour transfer cutoff".  
The existing 24 hours are invitation validity, not business lead time.

---

## 6.5 Accept handler

Keep the existing central accept path for both `INITIAL_CONFIRMATION` and `TRANSFER`.

For TRANSFER:

- verify `instance.OperationalContactUserId == change.OldUserId`;
- ensure target identity/account rules still pass;
- parse B's pending snapshot;
- apply B's details only here;
- clear/update any stale `OperationalContactGuestMemberId` link exactly as current canonical transfer behavior requires;
- set holder to B;
- set confirmation source/time;
- mark change APPLIED;
- invalidate all outstanding invitation tokens;
- write existing transfer audit/history;
- notify current/old holder and registrant using existing notification semantics.

For a transfer created while `WAITING_REQUEST_APPROVAL`:

- acceptance must not change the campus to `ASSIGNED`;
- acceptance must not reopen/close the global gate because a valid holder existed before and after;
- no host activation should be triggered solely by the transfer.

If the campus was approved between initiation and acceptance:

- `ASSIGNED` / `BEFORE_VISIT` acceptance must still work;
- existing decision/host/schedule must survive byte-for-byte/field-for-field except fields intentionally changed by contact transfer.

If the campus started before acceptance:

- preserve the existing lifecycle refusal;
- do not partially apply B;
- keep A as holder;
- leave the pending transfer available for cleanup according to existing behavior.

---

## 6.6 Cancel / Decline / Expire

Do not add restoration logic.

For `TRANSFER`:

```text
cancel -> settle invitation only
decline -> settle invitation only
expire -> settle invitation only
```

A remains current holder automatically.

Preserve:

- token invalidation;
- retention;
- redaction;
- audit events;
- history events;
- idempotency;
- cleanup-after-start behavior.

For `INITIAL_CONFIRMATION`, preserve current semantics:

- no holder exists;
- cancel/decline/expire leaves the campus without a confirmed contact;
- gate remains closed;
- registrant may reinvite or replace.

---

## 6.7 Resend / Reinvite

Do not merge these concepts.

Preserve:

- RESEND = same still-PENDING identity-change row, new token/version, cap/cooldown;
- REINVITE = previous initial confirmation is already settled and there is no confirmed holder.

For a pending TRANSFER:

- resend may remain available only while transfer lifecycle still allows the handover;
- if visit has started, resend should remain blocked as current lifecycle-lock tests expect;
- cancel cleanup must remain available.

Do not add "reinvite transfer" unless audit shows a real product requirement.  
After an expired/declined/cancelled transfer, A still holds the campus; the user can initiate a fresh handover normally.

---

# 7. Read-model and allowed-action requirements

`VisitFormReadService` is the backend source of truth for frontend mutation buttons. Do not move business authorization to React.

Update `ContactActionsFor(...)` (or current equivalent) so action emission mirrors the new handlers.

## When there is NO confirmed holder

For supported undecided states:

- registrant may get `REPLACE_OPERATIONAL_CONTACT`;
- if no live invitation and the current unconfirmed address is eligible for reinvite, preserve `REINVITE_OPERATIONAL_CONTACT_CONFIRMATION`;
- active initial confirmation may offer resend/cancel under existing limits.

Do not emit `INITIATE_OPERATIONAL_CONTACT_TRANSFER`.

## When there IS a confirmed holder

For supported pre-start states:

- do not emit `REPLACE_OPERATIONAL_CONTACT`;
- emit `INITIATE_OPERATIONAL_CONTACT_TRANSFER` when there is no pending change and actor is allowed;
- if a TRANSFER is pending, emit resend/cancel as appropriate;
- profile update remains available wherever existing profile-update lifecycle allows it.

The new pre-approval case must therefore look like:

```text
WAITING_REQUEST_APPROVAL
OperationalContactUserId = A
no pending transfer
=> UPDATE_OPERATIONAL_CONTACT_PROFILE
=> INITIATE_OPERATIONAL_CONTACT_TRANSFER
=> NOT REPLACE_OPERATIONAL_CONTACT
```

A sibling campus's holder must still gain no rights here.

---

# 8. Important read-model/frontend semantic trap: `TRANSFER_PENDING`

Current backend read logic intentionally reports:

```text
OperationalContactUserId != null + pending TRANSFER
=> confirmationStatus = TRANSFER_PENDING
```

That correctly means:

> "There is still a confirmed current holder, but a handover is pending."

However `CampusVisitDetailCard.tsx` currently passes roughly:

```tsx
contactConfirmed={
  campus.operationalContact.confirmationStatus === 'CONFIRMED'
}
```

That treats `TRANSFER_PENDING` as if no confirmed holder exists.

Fix this semantic mismatch.

At minimum, current-contact existence on the frontend must treat both as holding a confirmed current contact:

```text
CONFIRMED
TRANSFER_PENDING
```

Prefer a small explicit helper rather than scattering string comparisons.

For example conceptually:

```ts
const hasConfirmedOperationalContact =
  status === 'CONFIRMED' || status === 'TRANSFER_PENDING';
```

Then pass that truth into `ContactIdentityActions`.

Do not infer permission from this flag; permissions still come from `allowedActions`.

---

# 9. Frontend UX after the fix

For confirmed A with pending handover to B, the screen must clearly distinguish:

```text
CURRENT CONTACT: A
A is still the active/confirmed holder

PENDING HANDOVER:
B is invited to replace A
```

The read-only current-contact block must continue displaying A's persisted snapshot.

The management panel should show the pending B address only from the pending invitation state (masked where current API intentionally masks it).

Changing A → B on a pre-approval campus must use the same "handover" UX as the current post-decision transfer:

- warn before saving that the current contact keeps rights until B accepts;
- if the current UX asks for transfer reason, keep that behavior consistently;
- do not show the old "replace reopens confirmation gate" warning when A is confirmed;
- cancel confirmation must say that cancelling leaves A unchanged.

Audit/update i18n strings that say transfer is specifically "after approval/after decision"; the new business definition is "handover from an existing confirmed holder before visit start".

Do not expose a full invited email if the existing API intentionally returns only a masked address.

---

# 10. Aggregate, gate, and multi-campus non-regression

The following must remain true:

1. A pending TRANSFER does not make the campus "unconfirmed".
2. A pending TRANSFER does not close the request-wide contact gate.
3. A pending TRANSFER on campus HN must not alter contact, status, actions, or invitation state on campus HCM/ĐN/etc.
4. A contact on campus HN must not gain management rights over sibling campuses.
5. INITIAL_CONFIRMATION on any campus with no holder still keeps the entire request behind the global confirmation gate according to current canonical aggregate behavior.
6. The last INITIAL_CONFIRMATION acceptance may still open the gate and activate eligible proposed hosts exactly as current code does.
7. TRANSFER acceptance must not masquerade as "last initial confirmation" and accidentally fire gate-open side effects.

Prefer leaving `VisitRequestAggregateStatusService` unchanged if the current holder relation already gives the correct result.

---

# 11. Approval / host non-regression

Add tests proving a pre-approval transfer does not corrupt approval behavior.

Scenario:

```text
A confirmed
campus WAITING_REQUEST_APPROVAL
request gate already open
transfer A -> B pending
```

Staff Leader approves.

Expected:

```text
campus -> ASSIGNED
official host assigned normally
decision audit written normally
A remains OperationalContactUserId
transfer remains PENDING
request aggregate follows normal approval rules
```

Then B accepts before visit start.

Expected:

```text
OperationalContactUserId A -> B
campus remains ASSIGNED
CurrentHostUserId unchanged
DecidedBy unchanged
DecidedAt unchanged
DecisionNote unchanged
planned start/end unchanged
```

Do not couple operational-contact handover with host transfer.

---

# 12. History / audit requirements

Audit every history reader that handles these event types.

Do not create duplicate user-visible history entries for one handover operation.

Preserve existing canonical event families where possible:

- transfer requested;
- transfer accepted/applied;
- transfer declined;
- transfer cancelled;
- transfer expired;
- initial confirmation created/superseded/etc.

Update documentation/comments/readers that assume:

```text
TRANSFER == campus already approved
```

because that assumption is no longer true.

TRANSFER now means:

```text
existing confirmed holder -> proposed replacement
```

regardless of whether Staff Leader has already made the campus decision.

Do not weaken:

- campus scoping;
- history privacy;
- masked-email rules;
- retention/redaction behavior;
- immutable decision audit.

---

# 13. No schema change by default

The existing identity-change model already contains the fields needed for safe handover:

```text
OldUserId
OldEmailNormalized
NewUserId
NewEmailNormalized
NewEmailMasked
PendingSnapshotJson
ChangeKind
Status
```

Therefore do not add a new "old contact snapshot" column merely to support cancel.

The design specifically avoids restoration.

Only add/change a DB migration if the audit finds an actual DB constraint/trigger that prevents a valid pre-approval TRANSFER. If a schema/trigger change is necessary:

1. explain exactly why application-only changes are insufficient;
2. make the smallest compatible migration;
3. add migration/constraint tests;
4. preserve existing data and existing pending identity-change rows.

---

# 14. Explicit things NOT to change

Unless an audit proves the new invariant cannot work otherwise, do not change:

- request/campus authorization scope;
- registrant ownership rules;
- sibling-campus isolation;
- host selection/assignment rules;
- approval rules unrelated to contact confirmation;
- amendment flows;
- 72-hour visit registration lead-time logic;
- transfer invitation validity duration;
- resend cap;
- resend cooldown;
- token hashing / single-use behavior;
- public accept/decline security;
- account eligibility rules;
- notification resolver logic;
- form revision semantics;
- change-history privacy;
- retention/redaction;
- `visit_instance_form_details` ownership model;
- external support / visitor member linking except the existing contact-member link behavior on successful identity swap;
- unrelated UI permissions.

Do not "fix" unrelated code opportunistically in this change.

---

# 15. Required regression test matrix

Implement or update tests so each invariant below is executable.

## A. Router classification

### A1 — no holder + different email

```text
OperationalContactUserId = null
email A pending
save email B
```

Expected:

- REPLACE path;
- old initial invitation superseded if present;
- new INITIAL_CONFIRMATION pending;
- snapshot becomes B;
- gate remains/returns closed according to current rules.

### A2 — confirmed holder + different email before approval

```text
status = WAITING_REQUEST_APPROVAL
OperationalContactUserId = A
save email B
```

Expected:

- TRANSFER path;
- A relation unchanged;
- A snapshot unchanged;
- status unchanged;
- request status unchanged;
- transfer `OldUserId = A`;
- transfer pending snapshot contains B;
- exactly one outbound transfer invitation.

### A3 — confirmed holder + same normalized email

Expected:

- profile-update path only;
- no identity-change row;
- no invitation;
- holder/status/gate unchanged.

---

## B. Pending transfer outcomes from `WAITING_REQUEST_APPROVAL`

### B1 Cancel

Expected:

- transfer -> CANCELLED;
- A user id unchanged;
- A snapshot unchanged;
- campus status unchanged;
- request status unchanged;
- live tokens = 0.

### B2 Decline

Expected same preservation of A.

### B3 Expire

Expected same preservation of A.

### B4 Accept

Expected:

- A -> B atomically;
- FormDetail becomes B only now;
- campus stays `WAITING_REQUEST_APPROVAL`;
- request does not perform a false gate close/open cycle;
- transfer -> APPLIED;
- old links invalidated.

---

## C. Approval crossover

### C1 Approve while transfer pending

Expected approval succeeds normally and A remains contact.

### C2 B accepts after approval but before start

Expected contact swaps to B, while host/decision/schedule remain unchanged.

### C3 Campus starts before B accepts

Expected acceptance refused with current lifecycle conflict behavior, no partial mutation, A remains holder.

### C4 Cleanup after start

Existing cancel/decline cleanup tests must continue passing.

---

## D. Existing INITIAL_CONFIRMATION behavior

Prove no regression:

- initial invitation acceptance;
- decline;
- cancel;
- expire;
- resend;
- reinvite;
- replace pending candidate A with B;
- registrant self-match if currently supported;
- final campus confirmation opening the global gate;
- proposed-host activation on gate open.

---

## E. Multi-campus

At least one 2-campus integration fixture:

```text
HN has confirmed A and pending transfer B
HCM has confirmed C
```

Assert:

- HN transfer initiation does not alter HCM;
- request gate stays open;
- HCM Staff Leader visibility/rights do not change because of HN's pending handover;
- B acceptance changes HN only;
- HCM contact C remains untouched;
- a contact of HCM cannot manage HN transfer.

Also test the inverse where a sibling still lacks initial confirmation: the global gate must stay closed for that real reason, not because of the transfer.

---

## F. Frontend

Add/update tests for:

1. `TRANSFER_PENDING` still means a current confirmed holder exists.
2. Pre-approval confirmed A receives TRANSFER action, not REPLACE action.
3. Editing email A -> B displays the handover warning.
4. The form displays/uses A as the stored current contact while B is pending.
5. Pending transfer notice displays B's masked invitation target.
6. Cancel-transfer modal says current contact remains.
7. After cancellation/refetch, A is still the read-only current contact.
8. No "no active initial invitation / campus still waiting for confirmation" message is shown for a campus that still has A.
9. Read-only/HO/host/no-action viewers remain unchanged.
10. Existing validation/toast/error-code tests remain green.

---

# 16. Existing tests that must be deliberately updated, not blindly preserved

Some current tests encode the behavior being corrected.

In particular, audit tests equivalent to:

- `A_changed_address_before_a_decision_runs_the_canonical_replace`
- history fixtures that force a confirmed contact in `WAITING_REQUEST_APPROVAL` and then expect an external email change to clear `ContactUserId`
- frontend tests that treat "undecided" as sufficient to imply REPLACE even when a confirmed holder exists.

Do not merely make production code satisfy stale assertions.

Update those tests so they reflect the new identity-holder invariant while preserving separate coverage for the legitimate "no confirmed holder + replace pending candidate" case.

---

# 17. Concurrency / atomicity requirements

Preserve all current transactional guarantees.

A transfer initiation must never commit:

```text
PENDING transfer
without usable accept/decline links
```

A transfer acceptance must never commit:

```text
FormDetail = B
but OperationalContactUserId = A
```

or:

```text
OperationalContactUserId = B
but FormDetail = A
```

or any partial state.

Keep the existing `OldUserId` stale-holder check.

Add/retain a race test if practical:

- transfer proposed from A;
- some other valid operation changes holder before B accepts;
- B acceptance must fail rather than overwrite the newer holder.

Do not use campus row version as the sole transfer identity invariant if current design intentionally uses holder identity instead.

---

# 18. Documentation/comments cleanup

Update misleading comments and XML docs such as:

```text
"Replace before campus decision"
"Transfer after campus decision"
```

to the new precise distinction:

```text
REPLACE:
no confirmed holder exists; replace an unconfirmed candidate/invitation

TRANSFER:
a confirmed holder exists; propose a handover to another identity
```

Lifecycle remains a separate question:

```text
is this campus still in a state where a handover may be initiated/applied?
```

Do not leave old comments that contradict the new code.

---

# 19. Verification gates before declaring complete

Do not report completion until all applicable gates pass.

## Backend

Run targeted tests first:

- OperationalContactManagementTests
- OperationalContactLifecycleLockTests
- OperationalContactConfirmationWorkflowTests
- PerCampusFormV2ReadTests
- relevant history tests
- approval tests affected by `WAITING_REQUEST_APPROVAL`

Then run the broader backend unit/integration test suites used by the repository.

## Frontend

Run:

- `ContactIdentityActions.test.tsx`
- related CampusVisitDetail/read-model tests
- typecheck
- build
- broader frontend test suite if available

## Database

If any SQL trigger/migration changed:

- verify clean migration;
- verify existing database upgrade;
- verify no invalid pending-row duplicates;
- verify aggregate triggers and application aggregate service still agree.

---

# 20. Required final implementation report

After implementing, return a structured report containing:

## 20.1 Root cause confirmed from latest code

State the exact old branching condition and destructive mutation.

## 20.2 Files changed

List each file and why.

## 20.3 Final business rule

State plainly:

```text
No confirmed holder + new identity -> REPLACE
Confirmed holder + new identity -> TRANSFER
```

## 20.4 State-transition proof

Show these four end-to-end paths:

```text
A confirmed -> invite B -> B accepts
A confirmed -> invite B -> cancel
A confirmed -> invite B -> B declines
A confirmed -> invite B -> B expires
```

## 20.5 Approval crossover proof

Show that approval may occur while B is pending and does not remove A.

## 20.6 Multi-campus proof

Show sibling campus state is unchanged.

## 20.7 Test results

Report exact test commands and pass/fail counts.

## 20.8 Remaining risks

Do not say "all good" without identifying whether any risk remains around:

- old persisted invitations;
- DB triggers;
- cached frontend data;
- history assumptions;
- production migration compatibility.

---

# 21. Definition of Done

This task is complete only when all statements below are true:

- [ ] Editing confirmed A to B before Staff Leader approval no longer clears A.
- [ ] B is stored only as a pending TRANSFER until acceptance.
- [ ] Current `FormDetail` remains A while transfer is pending.
- [ ] Cancel leaves A untouched.
- [ ] Decline leaves A untouched.
- [ ] Expiry leaves A untouched.
- [ ] Accept atomically swaps A -> B.
- [ ] `WAITING_REQUEST_APPROVAL` supports a confirmed-holder transfer.
- [ ] Pending transfer does not close the global confirmation gate.
- [ ] Staff Leader may approve while the transfer is pending.
- [ ] Approval does not settle or destroy the pending transfer.
- [ ] Transfer acceptance after approval preserves host/decision/schedule.
- [ ] Transfer acceptance after visit start remains blocked.
- [ ] Cleanup after visit start remains allowed.
- [ ] No-holder replacement behavior still works.
- [ ] Same-email metadata update still works.
- [ ] Reinvite/resend semantics remain distinct.
- [ ] Multi-campus isolation remains intact.
- [ ] Backend remains the permission source of truth.
- [ ] `TRANSFER_PENDING` is not misread by the frontend as "no confirmed contact".
- [ ] History/audit has no duplicate or misleading event.
- [ ] Retention/redaction remains intact.
- [ ] Existing token atomicity/single-use behavior remains intact.
- [ ] Targeted backend tests pass.
- [ ] Targeted frontend tests pass.
- [ ] Full build/typecheck pass.
- [ ] No unrelated behavior was changed.

---

# 22. One-sentence implementation principle

> **Do not remove a confirmed current operational contact because somebody merely proposed a replacement; move the relation only when the replacement actually accepts.**
