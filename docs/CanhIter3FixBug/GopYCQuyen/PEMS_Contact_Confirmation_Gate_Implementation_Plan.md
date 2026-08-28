# PEMS – Detailed Implementation Plan
## Change Contact Confirmation Gate from "Visibility + Action Gate" to "Approval Action Gate"

**Repository:** `quangthoai04/PEMS`  
**Reviewed baseline:** commit `34e85c9e3624ec3ebd6cfad7d5e0f37a08fc13a4`  
**Purpose:** Allow Staff Leader to see a visit request before all Operational Contacts confirm, while keeping Approve/Reject blocked until the Contact Confirmation Gate is open.

> **Important:** This document is an implementation plan only. It does **not** prescribe a blind search-and-replace. The current code has intentionally separated FILTER, AUTHORIZATION, ENTRY CONTEXT, request-level aggregate status, and campus-level status. The implementation must preserve those separations.

---

# 1. Executive Decision

## Current behavior

For a request with multiple campuses:

```text
Campus A → Contact confirmed
Campus B → Contact not confirmed
                ↓
visit_requests.status
= PENDING_CONTACT_CONFIRMATION
                ↓
Staff Leader cannot see/process the request
```

The current source explicitly defines `PENDING_CONTACT_CONFIRMATION` as a global gate and says that no Staff Leader of any campus may see or process the request while the gate is closed.

Relevant source:

- `backend/PEMS.Domain/Constants/VisitRequestConstants.cs`
- `backend/PEMS.Application/Delegations/Common/VisitRequestOwnership.cs`
- `backend/PEMS.Application/Delegations/Services/VisitRequestAggregateStatusService.cs`
- `backend/PEMS.Application/Delegations/Services/CampusApprovalExecutor.cs`
- `backend/PEMS.Application/Delegations/Queries/ViewGuestDelegationList/ViewGuestDelegationListQueryHandler.cs`

## Proposed behavior

Change only the **visibility consequence** of the gate:

```text
PENDING_CONTACT_CONFIRMATION

Staff Leader:
    VIEW       = YES
    APPROVE    = NO
    REJECT     = NO
```

After all required Operational Contacts confirm:

```text
PENDING_APPROVAL

Staff Leader:
    VIEW       = YES
    APPROVE    = YES
    REJECT     = YES
```

The gate therefore remains a real backend business rule, but becomes an **approval-action gate rather than a visibility gate**.

---

# 2. Business Objective

The goal is to improve transparency for Staff Leaders without allowing premature approval.

A Staff Leader should be able to know:

> "There is a request involving my campus that is waiting for Operational Contact confirmation."

But the Staff Leader must not be able to decide the campus until the request passes the confirmation condition.

This creates a clean distinction:

```text
Visibility:
"Can I see that this request exists?"

Authorization:
"Can I perform an approval/rejection action?"
```

The Contact Confirmation Gate controls the second question.

---

# 3. Verified Current Architecture

## 3.1 Request-level status is an aggregate

`VisitRequestAggregateStatusService` is the single source of truth for `visit_requests.status`.

The service computes request status from campus-instance state.

The order is intentionally:

```text
if any active campus has no Operational Contact
    → PENDING_CONTACT_CONFIRMATION

else if all active campuses are rejected
    → REJECTED

else if approved + pending campuses exist
    → PARTIALLY_APPROVED

else if approved campus exists
    → APPROVED

else if pending campuses exist
    → PENDING_APPROVAL
```

This aggregate logic must NOT be removed.

Source evidence:
`VisitRequestAggregateStatusService.Compute()`.

## 3.2 Campus-level decision remains authoritative

The real approve/reject decision lives on:

```text
visit_request_campuses.status
```

not on:

```text
visit_requests.status
```

`visit_requests.status` remains an aggregate/request-level state.

Therefore this change must NOT move approval decisions to the parent request.

## 3.3 Contact is campus-scoped

`VisitRequestOwnership.IsOperationalContact(...)` checks:

```text
instance.OperationalContactUserId == userId
```

for the specific campus.

A confirmed contact for campus A must not automatically become the contact for campus B.

This must remain unchanged.

## 3.4 Staff Leader authority is campus-scoped

`VisitRequestOwnership.IsCampusLeader(...)` requires:

```text
Staff role
+ Leader sub-role
+ PrimaryCampusId == target campus
```

A Staff Leader of campus A must not gain approval authority over campus B.

This must remain unchanged.

---

# 4. Target Behavior

Consider:

```text
Visitor creates request

Request:
    HN  → Contact confirmed
    HCM → Contact pending

Aggregate:
    PENDING_CONTACT_CONFIRMATION
```

## Staff Leader HN

Should see:

```text
Request #001
Campus: HN
Status: Waiting for contact confirmation

VIEW       ✓
APPROVE    ✗
REJECT     ✗
```

## Staff Leader HCM

Should also see the request if the normal responsibility/visibility rules say that the HCM campus instance belongs in their scope.

But:

```text
APPROVE    ✗
REJECT     ✗
```

## HO

Existing monitoring behavior remains unchanged:

```text
VIEW       ✓
APPROVE    ✗
REJECT     ✗
```

## Visitor / Registrant

Existing registrant behavior remains unchanged unless an unrelated requirement explicitly changes it.

---

# 5. Critical Design Rule

## Do NOT solve this by changing only the frontend

This is unsafe:

```text
if PENDING_CONTACT_CONFIRMATION
    disable Approve button
```

while the backend still accepts:

```text
POST /approve
```

A user could call the API directly.

The backend must remain authoritative.

The current `CampusApprovalExecutor.ApproveAndAssignHostAsync(...)` already contains:

```text
if request is behind contact gate
    throw ContactConfirmationRequired
```

That check must remain.

Therefore:

```text
Frontend
    ↓
shows disabled action

Backend
    ↓
independently refuses action
```

Both layers are required.

---

# 6. Files That Should Be Changed

## Priority 1 – Visibility/query layer

### File

`backend/PEMS.Application/Delegations/Queries/ViewGuestDelegationList/ViewGuestDelegationListQueryHandler.cs`

### Current responsibility

This handler is explicitly responsible for returning rows already filtered to the caller's responsibility scope.

The source documentation makes an important distinction:

- FILTER = why a row appears
- AUTHORIZATION = what the caller may do
- ENTRY CONTEXT = which screen opens

The implementation must preserve this separation.

### Required change

Remove the dependency that excludes Staff Leader rows solely because:

```text
visit_requests.status == PENDING_CONTACT_CONFIRMATION
```

from Staff Leader visibility.

Do NOT remove normal Staff Leader campus responsibility filtering.

The desired logic is:

```text
Is this request/campus visible to this Staff Leader
    ?
    ├── NO → do not return
    └── YES
          ↓
    Contact Gate?
          ├── CLOSED → return row, but no approval actions
          └── OPEN   → return row with normal approval actions
```

The gate must therefore affect **capabilities/actions**, not whether a legitimate campus row exists.

---

# 7. Action/Authorization Layer

## File

`backend/PEMS.Application/Delegations/Services/CampusApprovalExecutor.cs`

### Keep this guard

The following business invariant must remain:

```text
PENDING_CONTACT_CONFIRMATION
    → ApproveAndAssignHostAsync must reject
```

Reason:

The queue/list is not an authorization boundary.

Even if the UI or query accidentally exposes a row, direct API calls must not bypass the gate.

### Do NOT remove

```text
VisitRequestStatuses.IsBehindContactGate(...)
```

from approval execution.

### Expected result

Before confirmation:

```text
ApproveAndAssignHostAsync(...)
    → ContactConfirmationRequired
```

After confirmation:

```text
ApproveAndAssignHostAsync(...)
    → continue normal approval validation
```

---

# 8. Capability Calculation

The list DTO currently contains:

```text
AllowedActions
Capabilities
```

The handler computes relation contexts first and then builds allowed actions.

This is the safest place to express:

```text
VIEW ≠ APPROVE
```

## Required behavior

When:

```text
caller = Staff Leader of target campus
request = PENDING_CONTACT_CONFIRMATION
```

the returned row may contain:

```text
VIEW_DETAIL
```

but must not contain:

```text
APPROVE
REJECT
```

or whatever exact action codes are used by the existing approval UI.

Do NOT create a new authorization model if the existing capability system can express this cleanly.

Prefer:

```text
existing relation
+
existing lifecycle
+
contact gate condition
```

rather than introducing a second independent permission system.

---

# 9. `VisitRequestOwnership.cs`

## File

`backend/PEMS.Application/Delegations/Common/VisitRequestOwnership.cs`

### Current behavior

`IsBehindContactGate(visit)` currently documents and represents:

> no Staff Leader may see or process the request.

This meaning becomes too broad after the proposed change.

### Recommended change

Do NOT immediately delete `IsBehindContactGate`.

It is still useful as the canonical request-level condition:

```text
IsBehindContactGate(visit)
```

should continue to mean:

```text
request.Status == PENDING_CONTACT_CONFIRMATION
```

What should change is **where this condition is consumed**.

The method should no longer be used as a blanket Staff Leader visibility exclusion.

Instead:

```text
IsBehindContactGate()
```

should be used for:

- approval authorization;
- rejection authorization;
- other actions that must wait for confirmation.

### Documentation must be updated

The existing comment currently says:

```text
While this is true NO Staff Leader of ANY campus may see or process the request.
```

After the change that statement would be false.

It should be rewritten to describe the new invariant:

```text
While this is true, Staff Leaders may view a request within their normal
responsibility scope, but they may not perform approval/rejection actions
until the confirmation gate opens.
```

Do not leave stale comments.

---

# 10. Aggregate Status Service

## File

`backend/PEMS.Application/Delegations/Services/VisitRequestAggregateStatusService.cs`

## Do NOT change the aggregation rule

This must remain:

```text
any active campus without confirmed Operational Contact
    → PENDING_CONTACT_CONFIRMATION
```

This status still has meaning.

The proposed change is NOT:

```text
PENDING_CONTACT_CONFIRMATION
→ PENDING_APPROVAL
```

while some campus remains unconfirmed.

That would destroy the existing gate state.

Instead:

```text
Campus A confirmed
Campus B unconfirmed

→ request.status = PENDING_CONTACT_CONFIRMATION
→ Staff Leader can VIEW
→ Staff Leader cannot APPROVE/REJECT
```

Then:

```text
Campus A confirmed
Campus B confirmed

→ request.status = PENDING_APPROVAL
→ Staff Leader can VIEW
→ Staff Leader can APPROVE/REJECT
```

The aggregate service therefore remains unchanged unless a test proves an unrelated defect.

---

# 11. Contact Confirmation Handler

The existing confirmation flow must remain responsible for recalculating the aggregate.

Relevant flow:

```text
Operational Contact confirms
        ↓
campus OperationalContactUserId becomes confirmed
        ↓
aggregate status recalculated
        ↓
if no unconfirmed campus remains
        ↓
gate opens
        ↓
request becomes PENDING_APPROVAL
```

Do not manually set:

```text
visit.Status = PENDING_APPROVAL
```

inside the UI or approval code.

Always use the existing aggregate service.

This avoids divergence between:

- EF application logic;
- database aggregate trigger;
- request status;
- `ContactGateRevision`.

---

# 12. ContactGateRevision

`ContactGateRevision` must remain intact.

The aggregate service currently increments the revision when the gate:

```text
opens
OR
closes
```

This is used as a deduplication/re-notification mechanism.

Do not remove or repurpose it as part of this visibility change.

The desired behavior is:

```text
Gate closed
    ↓
Staff Leader can see but cannot approve

Gate opens
    ↓
approval-ready notification logic can still use the existing transition
```

---

# 13. Status Filtering – IMPORTANT

The current repository has intentionally moved to:

```text
REQUEST_ANY_CAMPUS
```

semantics for status filtering.

A multi-campus request can therefore appear under multiple status filters when different campus instances genuinely have those statuses.

Example:

```text
HN  = ASSIGNED
HCM = WAITING_REQUEST_APPROVAL
```

The request can match both filters.

This behavior is covered by:

`tests/PEMS.IntegrationTests/VisitRequests/EffectiveStatusFilterTests.cs`

Do not "fix" the Contact Gate by reintroducing aggregate-only status filtering.

The filter population and authorization must remain separate.

---

# 14. Important Visibility Edge Cases

The implementation must explicitly test all of these.

## Case A – External Visitor, one campus

```text
HN contact unconfirmed
```

Expected:

```text
HN Staff Leader → sees request
Approve → disabled/absent
Reject → disabled/absent
```

## Case B – External Visitor, two campuses

```text
HN  → confirmed
HCM → unconfirmed
```

Expected:

```text
HN Staff Leader  → sees relevant HN row
HCM Staff Leader → sees relevant HCM row
Both             → cannot approve/reject
```

The request must NOT become approvable just because one campus is confirmed.

## Case C – All contacts confirmed

```text
HN  → confirmed
HCM → confirmed
```

Expected:

```text
request.status = PENDING_APPROVAL

HN Staff Leader  → can approve/reject HN
HCM Staff Leader → can approve/reject HCM
```

## Case D – Staff Leader from unrelated campus

```text
DN Staff Leader
Request campuses = HN + HCM
```

Expected:

```text
DN Staff Leader → does not gain visibility merely because the request exists.
```

This preserves campus scoping.

## Case E – HO

Expected:

```text
HO → can still monitor request
HO → cannot approve/reject
```

No regression.

## Case F – Registrant is also Staff Leader

If the registrant happens to be a Staff Leader:

```text
registrant relation ≠ campus approval authority
```

Being the registrant must not bypass the Contact Gate for approval.

The current ownership code explicitly separates registrant rights from campus-leader decision rights.

## Case G – Direct API call

Before contact confirmation:

```text
POST approve
```

Expected:

```text
HTTP/business error
ContactConfirmationRequired
```

This is mandatory.

## Case H – Contact confirmation races with approval

Scenario:

```text
T1: Staff Leader loads request
T2: Staff Leader attempts approve
T3: Contact is still unconfirmed
```

Expected:

```text
T2 must still fail
```

The stale list page must not grant authority.

If Contact confirms before the approval transaction reaches the guard:

```text
T2 may proceed only if the current transaction sees the gate as open
```

The authoritative state must be checked at action time.

---

# 15. Do Not Change the Approval Transaction

`CampusApprovalExecutor` performs much more than changing a status.

Approval currently includes:

1. cancellation guard;
2. Contact Confirmation Gate guard;
3. campus lifecycle check;
4. existing-host guard;
5. host-required guard;
6. user locking;
7. visit-request locking;
8. campus locking;
9. host eligibility validation;
10. hosting conflict detection;
11. campus status mutation;
12. decision audit;
13. Host participant creation/update;
14. aggregate status recomputation;
15. save;
16. post-commit notification.

The Contact Gate change must not disturb these operations.

The desired modification is:

```text
visibility/capability behavior changes
+
approval backend guard remains
```

not:

```text
rewrite CampusApprovalExecutor
```

---

# 16. Reject Must Be Treated the Same Way

Do not only protect Approve.

The requirement is:

```text
Before gate opens:
    Approve = forbidden
    Reject  = forbidden
```

The existing rejection command also checks the global gate.

Relevant file:

`backend/PEMS.Application/Delegations/Commands/RejectCampusInstance/RejectCampusInstanceCommandHandler.cs`

That guard must remain.

The UI capability calculation must also not expose Reject before the gate opens.

---

# 17. Entry Context / Navigation

The existing list handler separates:

```text
authorization
```

from:

```text
entry context
```

Do not change the default screen solely because the gate is closed.

A Staff Leader should be able to open the request in a read-only/pending context.

If the current detail screen assumes that every Staff Leader-visible row is immediately actionable, this must be audited.

The safe approach is:

```text
row visible
    ↓
open detail
    ↓
detail reads current capabilities
    ↓
approval controls disabled/hidden when gate closed
```

Do not rely on the list page alone.

---

# 18. Detail Endpoint Must Also Be Audited

The implementation is incomplete if only the list is changed.

Search all detail/read handlers for:

```text
IsBehindContactGate
PENDING_CONTACT_CONFIRMATION
ContactConfirmationRequired
```

Any endpoint that currently returns:

```text forbidden / not found
```

solely because the Staff Leader is behind the Contact Gate must be reviewed.

Desired behavior:

```text
Staff Leader with legitimate campus scope
    ↓
can GET/view detail
    ↓
cannot mutate approval
```

But a Staff Leader with no legitimate relationship to the request must still be denied.

This distinction is essential.

---

# 19. Notification Behavior

Do not change contact-confirmation notifications unless required.

The existing aggregate service reports:

```text
Opened
Closed
GateRevision
```

This transition information should remain.

Potential notification behavior:

```text
Gate opens
    ↓
approval-ready notification
    ↓
Staff Leaders can now process
```

If Staff Leaders become visible before the gate opens, do NOT automatically send an "approval ready" notification early.

Visibility and readiness notification are different concepts.

---

# 20. Frontend Changes

The exact frontend paths must be discovered from the repository before editing.

Search for:

```text
AllowedActions
Capabilities
APPROVE
REJECT
PENDING_CONTACT_CONFIRMATION
ContactConfirmationRequired
```

Expected UI behavior:

### Gate closed

```text
Status: Chờ xác nhận đầu mối

Approve → disabled or absent
Reject  → disabled or absent
View    → enabled
```

Prefer using backend `AllowedActions` / capability information instead of duplicating the business rule in React.

### Gate open

```text
Status: Chờ duyệt

Approve → enabled
Reject  → enabled
```

---

# 21. Testing Strategy

## 21.1 Unit tests

Add/adjust tests for the capability resolver.

Minimum matrix:

| Request gate | Caller | Expected View | Expected Approve | Expected Reject |
|---|---|---:|---:|---:|
| Closed | Staff Leader of campus | YES | NO | NO |
| Open | Staff Leader of campus | YES | YES | YES |
| Closed | unrelated Staff Leader | NO | NO | NO |
| Closed | HO | YES | NO | NO |
| Open | HO | YES | NO | NO |

## 21.2 Integration tests

Use the real MySQL/Pomelo test DB pattern already used by:

`EffectiveStatusFilterTests.cs`

Do not rely only on mocks for SQL/filter behavior.

Minimum integration scenarios:

1. Visitor creates one-campus request with unconfirmed contact.
2. Staff Leader can find the request.
3. Staff Leader cannot approve.
4. Staff Leader cannot reject.
5. Contact confirms.
6. Aggregate changes to `PENDING_APPROVAL`.
7. Staff Leader can approve.
8. Staff Leader can reject.
9. Multi-campus request with one confirmed + one unconfirmed campus.
10. Both relevant Staff Leaders can view their campus rows.
11. Neither can approve before the last contact confirms.
12. Direct API approve still fails while gate is closed.
13. Unrelated campus Staff Leader remains excluded.
14. HO behavior remains unchanged.
15. Status filtering still obeys `REQUEST_ANY_CAMPUS`.

---

# 22. Regression Tests That Must Not Break

The following existing invariants must be preserved.

## Campus authority

```text
Staff Leader of HN
≠
Staff Leader of HCM
```

A leader cannot approve another campus.

## Host requirement

Approval still requires a Host.

Do not introduce:

```text
APPROVED without Host
```

The existing `HOST_REQUIRED_ON_APPROVAL` invariant remains.

## Cancellation

Cancelled requests remain terminal.

`CANCELLED` must still prevent approval.

## Campus lifecycle

Only:

```text
WAITING_REQUEST_APPROVAL
```

can enter normal approval.

Do not allow:

```text
ASSIGNED
BEFORE_VISIT
DURING_VISIT
AFTER_VISIT
CLOSED
REJECTED
CANCELLED
```

to be approved again.

## Aggregate status

Do not make the request status manually mutable from UI.

## Audit

Approval/rejection must continue to write the existing audit records.

## Host participant

Approval must continue to create/update the Host participant as currently implemented.

## Notifications

Approval notifications remain post-commit.

---

# 23. Database / Trigger Safety

The source documents that DB triggers:

```text
trg_visit_campuses_aggregate_ai
trg_visit_campuses_aggregate_au
```

also compute the aggregate request status.

Therefore:

**Do not change database trigger behavior for this requirement unless testing proves it is necessary.**

The desired state is still:

```text
unconfirmed campus exists
    ↓
visit_requests.status = PENDING_CONTACT_CONFIRMATION
```

The only changed behavior is:

```text
Staff Leader visibility
```

and:

```text
action capability
```

not the aggregate status itself.

---

# 24. Recommended Implementation Sequence

## Phase 1 – Baseline

Before editing:

- create a dedicated branch;
- run current backend tests;
- record current behavior;
- verify the current Contact Gate tests;
- verify status-filter tests.

Do not start implementation if baseline tests already fail without recording the failures.

## Phase 2 – Backend capability/visibility

Modify:

```text
ViewGuestDelegationListQueryHandler
```

so legitimate Staff Leader rows are not excluded solely because the aggregate is:

```text
PENDING_CONTACT_CONFIRMATION
```

Then calculate:

```text
AllowedActions
Capabilities
```

with the gate applied to approval/rejection.

## Phase 3 – Backend authorization audit

Keep and verify:

```text
CampusApprovalExecutor
RejectCampusInstanceCommandHandler
```

Both must reject action while gate is closed.

Search the whole backend for additional approval/rejection entry points.

## Phase 4 – Detail/read path

Audit detail endpoints and entry context.

Ensure:

```text
VIEW allowed
ACTION denied
```

for the intended Staff Leader.

## Phase 5 – Frontend

Consume backend capability information.

Do not implement an independent React-only gate.

## Phase 6 – Automated tests

Run:

- unit tests;
- integration tests;
- status filter tests;
- approval/rejection tests;
- multi-campus regression tests.

## Phase 7 – Manual API verification

Using Postman:

### Before confirmation

```text
GET list
→ request visible

POST approve
→ rejected

POST reject
→ rejected
```

### After confirmation

```text
GET list
→ request visible

POST approve
→ success
```

## Phase 8 – Final regression

Verify:

- Visitor;
- Staff;
- Staff Leader;
- HO;
- multi-campus;
- single-campus;
- registrant = Staff Leader;
- contact = different user;
- cancelled request;
- rejected campus;
- approved campus;
- host assignment;
- notifications;
- status filters.

---

# 25. Acceptance Criteria

The implementation is complete only when all are true.

### Visibility

- [ ] Staff Leader can see a legitimate request even when `PENDING_CONTACT_CONFIRMATION`.
- [ ] Visibility remains restricted by normal campus responsibility.
- [ ] An unrelated Staff Leader cannot see the request merely because the gate is open/closed.

### Approval

- [ ] Staff Leader cannot approve while gate is closed.
- [ ] Staff Leader cannot reject while gate is closed.
- [ ] Direct API calls cannot bypass the gate.
- [ ] Approval becomes available after all required contacts confirm.

### Aggregate

- [ ] `PENDING_CONTACT_CONFIRMATION` is still computed when any active campus lacks a confirmed contact.
- [ ] `PENDING_APPROVAL` is reached only after the gate condition is satisfied.
- [ ] Existing aggregate behavior is unchanged otherwise.

### Multi-campus

- [ ] One confirmed campus does not open the gate while another required campus is unconfirmed.
- [ ] Each Staff Leader sees only their legitimate campus scope.
- [ ] One campus's contact does not grant authority over sibling campuses.

### Existing business rules

- [ ] Host is still required on approval.
- [ ] Cancelled requests remain non-approvable.
- [ ] Campus lifecycle guards remain intact.
- [ ] Audit records remain intact.
- [ ] Notifications remain intact.
- [ ] `REQUEST_ANY_CAMPUS` status-filter behavior remains intact.

---

# 26. Rollback Plan

If the change causes unexpected behavior, rollback should be possible without reverting unrelated work.

Recommended implementation isolation:

```text
Commit 1:
backend visibility/capability change

Commit 2:
backend/detail authorization adjustments

Commit 3:
frontend capability/UI change

Commit 4:
tests
```

Do not mix this change with unrelated business-rule changes.

If rollback is needed:

```text
revert visibility/capability changes
```

while leaving existing:

```text
aggregate status
approval executor
contact confirmation
database triggers
```

intact.

---

# 27. Risks

## Risk 1 – UI says "view only" but API allows approval

Severity: **Critical**

Mitigation:

- keep backend gate in approval;
- add direct API integration test.

## Risk 2 – Removing gate from query accidentally exposes unrelated requests

Severity: **Critical**

Mitigation:

- preserve campus responsibility filtering;
- test unrelated Staff Leader;
- do not replace the query with "all requests".

## Risk 3 – Changing aggregate status to make visibility work

Severity: **Critical**

Mitigation:

- do not change aggregate semantics;
- visibility must be independent from aggregate status.

## Risk 4 – Approve protected but Reject forgotten

Severity: **High**

Mitigation:

- audit both commands;
- add paired tests.

## Risk 5 – Detail page still blocks viewing

Severity: **High**

Mitigation:

- audit list + detail endpoints together.

## Risk 6 – Frontend duplicates backend business rules

Severity: **Medium**

Mitigation:

- use backend `AllowedActions` / capabilities.

## Risk 7 – Multi-campus request becomes incorrectly visible to every Staff Leader

Severity: **Critical**

Mitigation:

- test campus scoping explicitly.

## Risk 8 – Existing `REQUEST_ANY_CAMPUS` status filtering regresses

Severity: **High**

Mitigation:

- preserve existing integration tests;
- do not restore aggregate-bucket filtering.

---

# 28. Final Architectural Model

After implementation, the intended model is:

```text
                         VISIT REQUEST
                              │
                    ┌─────────┴─────────┐
                    │                   │
              Aggregate Status     Campus Instances
                    │                   │
                    │             ┌─────┴─────┐
                    │             │           │
                    │           HN          HCM
                    │             │           │
                    │          Contact      Contact
                    │          Status       Status
                    │
                    ↓
       PENDING_CONTACT_CONFIRMATION
                    │
                    │
          ┌─────────┴──────────┐
          │                    │
        VIEW                 ACTION
          │                    │
        ALLOW             APPROVE = DENY
        ALLOW             REJECT  = DENY
          │                    │
          └──────────┬─────────┘
                     │
              All required
              contacts confirm
                     │
                     ↓
              PENDING_APPROVAL
                     │
          ┌──────────┴──────────┐
          │                     │
        VIEW                 ACTION
          │                     │
        ALLOW             APPROVE = ALLOW
        ALLOW             REJECT  = ALLOW
```

The key architectural principle is:

> **Contact Confirmation Gate controls readiness to make an approval decision, not the Staff Leader's awareness that a request exists.**

---

# 29. Final Review Checklist Before Coding

Before implementation, verify all of the following against the actual current branch:

- [ ] Search every `IsBehindContactGate` call site.
- [ ] Search every `PENDING_CONTACT_CONFIRMATION` call site.
- [ ] Search every Approve command/handler/service.
- [ ] Search every Reject command/handler/service.
- [ ] Search every `AllowedActions` builder.
- [ ] Search every `Capabilities` builder.
- [ ] Search list query and detail query separately.
- [ ] Search frontend usage of approval action codes.
- [ ] Search all tests mentioning Contact Gate.
- [ ] Search all tests mentioning Staff Leader visibility.
- [ ] Confirm no alternate approval endpoint bypasses `CampusApprovalExecutor`.
- [ ] Confirm DB triggers still produce the expected aggregate status.
- [ ] Run the complete relevant test suite before declaring the change safe.

**Do not implement until this checklist has been completed against the actual branch.**

---

# 30. Source Evidence Reviewed

The plan is based on the current repository source reviewed during planning:

1. `backend/PEMS.Application/Delegations/Common/VisitRequestOwnership.cs`
   - registrant relation;
   - campus-scoped Operational Contact;
   - campus-scoped Staff Leader;
   - Contact Gate predicate;
   - separation of requester-side and campus decision rights.

2. `backend/PEMS.Application/Delegations/Services/VisitRequestAggregateStatusService.cs`
   - aggregate status calculation;
   - Contact Gate precedence;
   - gate transition and `ContactGateRevision`.

3. `backend/PEMS.Application/Delegations/Services/CampusApprovalExecutor.cs`
   - cancellation guard;
   - Contact Gate approval guard;
   - campus approval lifecycle;
   - host requirement;
   - locks;
   - participant creation;
   - aggregate recomputation;
   - audit and notification behavior.

4. `backend/PEMS.Domain/Constants/VisitRequestConstants.cs`
   - request-level status vocabulary;
   - campus-level status vocabulary;
   - definition of `PENDING_CONTACT_CONFIRMATION`;
   - `HOST_REQUIRED_ON_APPROVAL`;
   - related business-rule error codes.

5. `backend/PEMS.Application/Delegations/Queries/ViewGuestDelegationList/ViewGuestDelegationListQueryHandler.cs`
   - FILTER / AUTHORIZATION / ENTRY CONTEXT separation;
   - role-specific list populations;
   - Staff Leader campus responsibility;
   - `AllowedActions` / capability calculation;
   - merged `all` behavior.

6. `tests/PEMS.IntegrationTests/VisitRequests/EffectiveStatusFilterTests.cs`
   - real MySQL/Pomelo integration coverage;
   - `REQUEST_ANY_CAMPUS` status filtering;
   - multi-campus request behavior.

---

## Implementation principle

**Do not weaken the Contact Confirmation Gate. Reposition it.**

Before:

```text
Gate closed
→ cannot see
→ cannot act
```

After:

```text
Gate closed
→ can see
→ cannot act

Gate open
→ can see
→ can act
```

The **business invariant remains protected at the backend approval/rejection boundary**.
