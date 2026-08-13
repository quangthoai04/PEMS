# PEMS — Operational Contact Lifecycle Lock Fix Plan

**Date:** 14/08/2026  
**Repository:** `quangthoai04/PEMS`  
**Target branch:** `Dev`  
**Verified Dev HEAD:** `8f03c4f0e3d2257035cf8819b9135c2970e634cd`  
**Scope:** Backend lifecycle guards + read-model capabilities + regression tests. No database migration, no schema change, no API contract change.

---

## 0. Goal

Implement the final agreed rule for Operational Contact management:

> **Operational Contact may only be edited/replaced/transferred before the campus reaches `DURING_VISIT`. From `DURING_VISIT` onward, no mutation that changes the current Operational Contact identity or profile is allowed. The old 6-hour/24-hour cutoff must not be used for Operational Contact management.**

Important distinction:

- **Mutation is locked from `DURING_VISIT+`.**
- **Cleanup of an already-pending invitation remains allowed.**
- Therefore:
  - `Accept transfer` is blocked from `DURING_VISIT+`.
  - `Resend transfer` is blocked from `DURING_VISIT+`.
  - `Cancel pending transfer` remains allowed.
  - `Decline pending transfer` remains allowed.
- The existing **24-hour transfer invitation validity** is still valid and must remain unchanged. Only the **24-hour transfer lead-time cutoff** is removed.

---

## 1. Final business rules

### 1.1 Campus lifecycle matrix

| Campus status | Edit name/phone/job title/organization | Change email/person | Routing |
|---|---:|---:|---|
| `WAITING_CONTACT_CONFIRMATION` | ✅ | ✅ | Different email → `ReplaceOperationalContact` |
| `WAITING_REQUEST_APPROVAL` | ✅ | ✅ | Different email → `ReplaceOperationalContact` |
| `ASSIGNED` | ✅ | ✅ | Different email → `InitiateOperationalContactTransfer` |
| `BEFORE_VISIT` | ✅ | ✅ | Different email → `InitiateOperationalContactTransfer` |
| `DURING_VISIT` | ❌ | ❌ | Locked |
| `AFTER_VISIT` | ❌ | ❌ | Locked |
| `CLOSED` | ❌ | ❌ | Locked |
| `CANCELLED` | ❌ | ❌ | Locked |
| `REJECTED` | ❌ | ❌ | Contact management locked; resubmit flow remains separate |

`VisitInstanceStatuses.DecidedNotStarted` currently represents the decided-but-not-started lifecycle and should continue to mean:

- `ASSIGNED`
- `BEFORE_VISIT`

### 1.2 Pending transfer behavior after the visit starts

If a transfer was created while the campus was still `ASSIGNED` or `BEFORE_VISIT`, but the campus later moves to `DURING_VISIT` before the new person accepts:

| Action | `DURING_VISIT+` |
|---|---:|
| Edit Operational Contact profile | ❌ |
| Replace Operational Contact | ❌ |
| Start a new transfer | ❌ |
| Accept pending transfer | ❌ |
| Resend pending transfer | ❌ |
| Cancel pending transfer | ✅ |
| Decline pending transfer | ✅ |
| Read/view contact state | ✅ |

Rationale:

- `Accept` would actually replace the current Operational Contact relation, so it is a mutation and must be blocked.
- `Resend` renews the opportunity for a transfer to be applied and extends `ExpiresAt`, so it must be blocked.
- `Cancel` and `Decline` only settle a pending invitation and preserve the current Operational Contact, so they are cleanup actions and remain allowed.

### 1.3 Permission rules remain unchanged

Do **not** broaden permissions.

- Update contact profile: registrant or current Operational Contact of that exact campus.
- Replace before campus decision: registrant only.
- Transfer after decision but before visit starts: registrant or current Operational Contact of that exact campus.
- A contact of campus A has no rights over campus B.
- Existing invitation authentication, token matching, account eligibility, audit logs and per-campus isolation remain unchanged.

---

## 2. Non-goals / do not change

Do not change any of the following unless a failing test proves the current implementation is inconsistent with this plan:

1. Database schema.
2. SQL migration files.
3. REST routes.
4. Request/response DTO contract.
5. Transfer handshake semantics:
   - current contact retains authority until new person accepts;
   - transfer becomes effective only on successful accept.
6. Transfer invitation validity:
   - `TransferValidityHours = 24` stays.
7. Initial confirmation validity:
   - `InitialConfirmationValidityHours = 72` stays.
8. Resend cooldown and resend cap.
9. Audit/event naming unless required by current code.
10. 6-hour lead-time used by other workflows such as safe edit/amendment/host handover.
11. 72-hour registration scheduling floor.
12. Frontend status-based business-rule duplication.

---

## 3. Critical distinction: remove lead-time cutoff, keep invitation validity

Current `OperationalContactGuards.cs` contains two 24-hour concepts:

```csharp
public const int TransferValidityHours = 24;
public const int TransferLeadHours = 24;
```

They are not the same rule.

### Keep

```csharp
public const int TransferValidityHours = 24;
```

Meaning: once a transfer invitation is created/resend, its token/invitation validity window can remain 24 hours, subject to the lifecycle guards in this plan.

### Remove

```csharp
public const int TransferLeadHours = 24;
```

Meaning: the user must initiate transfer at least 24 hours before `PlannedStartAt`.

This cutoff is obsolete. The new decision is based only on campus lifecycle state.

A transfer at **1 minute before `PlannedStartAt`** is valid if the persisted campus status is still `BEFORE_VISIT`.

A transfer is invalid even **more than 24 hours before `PlannedStartAt`** if the persisted campus status is already `DURING_VISIT` due to workflow state.

---

# 4. Production code changes

## 4.1 `OperationalContactGuards.cs` — mandatory

Path:

```text
backend/PEMS.Application/Delegations/Commands/OperationalContact/OperationalContactGuards.cs
```

This is the main source of truth for Operational Contact writes.

### Change A — remove `TransferLeadHours`

Delete:

```csharp
/// <summary>Self-service transfer closes this long before the campus starts.</summary>
public const int TransferLeadHours = 24;
```

Do not remove `TransferValidityHours`.

### Change B — tighten `EnsureProfileUpdateAllowed`

Current behavior only blocks `CANCELLED` and `REJECTED`, which incorrectly allows profile edits during `DURING_VISIT`, `AFTER_VISIT` and `CLOSED`.

Replace with a positive whitelist:

```csharp
public static void EnsureProfileUpdateAllowed(
    VisitRequest visit,
    VisitRequestCampus instance)
{
    EnsureRequestLive(visit);

    if (instance.Status is
        VisitInstanceStatuses.WaitingContactConfirmation
        or VisitInstanceStatuses.WaitingRequestApproval
        or VisitInstanceStatuses.Assigned
        or VisitInstanceStatuses.BeforeVisit)
    {
        return;
    }

    throw new ConflictException(
        "Không thể chỉnh sửa đầu mối sau khi chuyến thăm đã bắt đầu hoặc đã kết thúc.",
        OperationalContactErrorCodes.ChangeConflict);
}
```

The exact Vietnamese message may be normalized to project wording, but the status contract must remain exactly as above.

### Change C — rewrite `EnsureTransferWindowOpen`

Current behavior:

- requires `BEFORE_VISIT`;
- then compares `PlannedStartAt - TransferLeadHours` with current time.

New behavior:

```csharp
public static void EnsureTransferWindowOpen(
    VisitRequest visit,
    VisitRequestCampus instance)
{
    EnsureRequestLive(visit);

    if (instance.Status is
        VisitInstanceStatuses.Assigned
        or VisitInstanceStatuses.BeforeVisit)
    {
        return;
    }

    throw new ConflictException(
        "Chỉ có thể chuyển giao đầu mối trước khi chuyến thăm bắt đầu.",
        OperationalContactErrorCodes.ChangeConflict);
}
```

Do not pass or inspect `DateTime vietnamNow`.

### Change D — keep `EnsureReplaceWindowOpen` semantics

Do not broaden replace.

Replace remains valid only in:

```text
WAITING_CONTACT_CONFIRMATION
WAITING_REQUEST_APPROVAL
```

`ASSIGNED` and `BEFORE_VISIT` are transfer territory.

### Change E — update comments

Remove comments that state:

- transfer has to leave 24 hours before start;
- transfer is blocked “inside the lead time”.

Update comments to describe lifecycle-only enforcement.

---

## 4.2 `InitiateOperationalContactTransferCommandHandler.cs` — mandatory

Path:

```text
backend/PEMS.Application/Delegations/Commands/OperationalContact/InitiateOperationalContactTransferCommandHandler.cs
```

Current call:

```csharp
OperationalContactGuards.EnsureTransferWindowOpen(
    visit,
    instance,
    now);
```

Change to:

```csharp
OperationalContactGuards.EnsureTransferWindowOpen(
    visit,
    instance);
```

Do **not** remove `_clock` or `now` from this handler.

`now` is still required for:

- `RequestedAt`
- `ExpiresAt`
- `CreatedAt`
- audit/event timestamps
- token validity calculation

Do not modify transfer handshake behavior.

---

## 4.3 `AcceptOperationalContactConfirmationCommandHandler.cs` — mandatory

Path:

```text
backend/PEMS.Application/Delegations/Commands/OperationalContact/AcceptOperationalContactConfirmationCommandHandler.cs
```

This is a required fix that is easy to miss.

### Problem

A transfer can be created in `BEFORE_VISIT`, remain pending, then be accepted after the campus becomes `DURING_VISIT`.

Current `EnsureCampusStillAcceptsContact()` only protects cancelled/rejected terminal cases and does not enforce the new transfer lifecycle rule.

Without this change:

```text
BEFORE_VISIT
  -> initiate transfer
  -> pending

DURING_VISIT
  -> invitee accepts
  -> ApplyTransfer(...)
  -> contact changes during visit   [BUG]
```

### Required change

Before `ApplyTransfer(...)`, enforce the transfer lifecycle again on the current locked/reloaded campus state.

Conceptually:

```csharp
if (change.ChangeKind == IdentityChangeKinds.Transfer)
{
    OperationalContactGuards.EnsureTransferWindowOpen(
        visit,
        instance);
}
```

This check must happen after the current visit/campus has been loaded and before the transfer relation is mutated.

Do not apply this guard to initial contact confirmation unless current lifecycle logic specifically requires it. Initial confirmation flow remains unchanged.

### Expected result

A transfer that was valid when initiated is not guaranteed to remain applicable forever. If the campus reaches `DURING_VISIT+` before acceptance:

- return conflict / business-rule rejection;
- do not change `OperationalContactUserId`;
- do not mark the transfer `APPLIED`;
- do not remove authority from the old contact.

---

## 4.4 `ResendOperationalContactConfirmationCommandHandler.cs` — mandatory

Path:

```text
backend/PEMS.Application/Delegations/Commands/OperationalContact/ResendOperationalContactConfirmationCommandHandler.cs
```

### Problem

Current resend checks:

- actor permission;
- pending invitation exists;
- invitation not expired;
- resend cooldown/cap;

and then moves `ExpiresAt` forward again.

Therefore a transfer created before the visit can currently be resent after the campus has already entered `DURING_VISIT`, despite the transfer no longer being legally applicable.

### Required change

After loading `change`, `visit`, `instance` and proving actor permission, enforce lifecycle for **transfer only**:

```csharp
if (change.ChangeKind == IdentityChangeKinds.Transfer)
{
    OperationalContactGuards.EnsureTransferWindowOpen(
        visit,
        instance);
}
```

Do this before:

- invalidating old tokens;
- incrementing `TokenVersion`;
- incrementing `ResendCount`;
- moving `ExpiresAt`;
- minting replacement tokens.

Initial confirmation resend remains unchanged.

### Expected result

At `DURING_VISIT+`, pending transfer:

- cannot be resent;
- does not receive a fresh 24-hour expiry;
- old current contact remains unchanged;
- cleanup remains possible through cancel/decline.

---

## 4.5 `VisitFormReadService.cs` — mandatory

Path:

```text
backend/PEMS.Application/Delegations/Services/VisitFormRead/VisitFormReadService.cs
```

The read model must advertise exactly what the command handlers will accept.

### Change A — remove `ContactTransferLeadHours`

Delete:

```csharp
private const int ContactTransferLeadHours = 24;
```

Remove the comment that describes this as a separate transfer clock.

### Change B — profile update action must be lifecycle-gated

Current code effectively adds:

```csharp
actions.Add(VisitFormActions.UpdateOperationalContactProfile);
```

for almost every non-cancelled/non-rejected live campus.

Change to a whitelist:

```csharp
var contactMutable =
    instance.Status is
        VisitInstanceStatuses.WaitingContactConfirmation
        or VisitInstanceStatuses.WaitingRequestApproval
        or VisitInstanceStatuses.Assigned
        or VisitInstanceStatuses.BeforeVisit;
```

Only emit:

```csharp
VisitFormActions.UpdateOperationalContactProfile
```

when `contactMutable == true`.

At `DURING_VISIT`, `AFTER_VISIT`, `CLOSED`, `CANCELLED` and `REJECTED`, do not advertise profile mutation.

### Change C — transfer capability becomes lifecycle-only

Current:

```csharp
var transferable =
    VisitInstanceStatuses.DecidedNotStarted.Contains(instance.Status)
    && instance.PlannedStartAt.AddHours(-ContactTransferLeadHours) >= now;
```

New:

```csharp
var transferable =
    VisitInstanceStatuses.DecidedNotStarted.Contains(instance.Status);
```

No clock comparison.

### Change D — preserve replace semantics

Keep:

```text
WAITING_CONTACT_CONFIRMATION
WAITING_REQUEST_APPROVAL
```

Registrant-only replace.

### Change E — pending transfer action semantics

Current read model already treats `CancelOperationalContactChange` as not gated by transfer lead time. Preserve that concept, but adjust it to the new lifecycle semantics.

For a pending **TRANSFER**:

#### Before `DURING_VISIT`

If normal resend cap/expiry rules pass:

- emit resend;
- emit cancel.

#### From `DURING_VISIT+`

- do **not** emit resend;
- do emit cancel for an authorized owner/current contact as cleanup.

For a pending initial confirmation, keep existing behavior.

Suggested structure:

```csharp
if (pending is not null)
{
    var isPendingTransfer =
        pending.Kind == IdentityChangeKinds.Transfer;

    var mayResend =
        pending.ExpiresAt > now
        && pending.ResendCount < MaxContactResends
        && (!isPendingTransfer || transferable);

    if (mayResend)
        actions.Add(VisitFormActions.ResendOperationalContactConfirmation);

    actions.Add(VisitFormActions.CancelOperationalContactChange);

    return actions;
}
```

Adapt to the actual current method structure; do not blindly paste if variable names differ.

Important: `CancelOperationalContactChange` is cleanup and remains available after the transfer mutation window closes.

### Change F — no frontend lifecycle inference

The read model is the capability source of truth for buttons. Do not force React to inspect status to compensate for backend capability drift.

---

## 4.6 `SaveOperationalContactCommandHandler.cs` — no routing change

Path:

```text
backend/PEMS.Application/Delegations/Commands/OperationalContact/SaveOperationalContactCommandHandler.cs
```

Keep current classification:

```text
same normalized email
    -> UpdateOperationalContactProfile

different email + undecided campus
    -> ReplaceOperationalContact

different email + otherwise
    -> InitiateOperationalContactTransfer
```

This is intentionally a router, not the lifecycle authority.

Direct API behavior at `DURING_VISIT+` becomes:

```text
same email
    -> profile handler
    -> EnsureProfileUpdateAllowed
    -> reject

different email
    -> transfer handler
    -> EnsureTransferWindowOpen
    -> reject
```

Do not duplicate the full status matrix in this router.

---

## 4.7 `UpdateOperationalContactProfileCommandHandler.cs` — no direct lifecycle condition

Path:

```text
backend/PEMS.Application/Delegations/Commands/OperationalContact/UpdateOperationalContactProfileCommandHandler.cs
```

This handler already calls:

```csharp
OperationalContactGuards.EnsureMayManageContact(...);
OperationalContactGuards.EnsureProfileUpdateAllowed(...);
```

Therefore the shared guard change is sufficient.

Do not add a second `if (status == DURING_VISIT...)` block here.

---

## 4.8 `ReplaceOperationalContactCommandHandler.cs` — preserve behavior

Path:

```text
backend/PEMS.Application/Delegations/Commands/OperationalContact/ReplaceOperationalContactCommandHandler.cs
```

Keep pre-decision-only behavior through `EnsureReplaceWindowOpen`.

No 6-hour or 24-hour rule should be introduced.

---

## 4.9 `CancelOperationalContactChangeCommandHandler.cs` — preserve cleanup after visit start

Current class is inside:

```text
backend/PEMS.Application/Delegations/Commands/OperationalContact/ManageOperationalContactHandlers.cs
```

Decision:

> Cancelling a pending transfer remains allowed from `DURING_VISIT+` because it does not mutate the current Operational Contact. It only settles the pending identity-change workflow and invalidates outstanding tokens.

Do not call `EnsureTransferWindowOpen()` from the cancel handler.

Expected behavior:

```text
A = current contact
C = pending transfer target

DURING_VISIT
Cancel pending transfer

Result:
A remains current contact
pending transfer -> CANCELLED
tokens invalidated
audit/event preserved
```

This is intentional cleanup, not an exception to the no-mutation rule.

---

## 4.10 `DeclineOperationalContactConfirmationCommandHandler.cs` — preserve cleanup after visit start

Path:

```text
backend/PEMS.Application/Delegations/Commands/OperationalContact/DeclineOperationalContactConfirmationCommandHandler.cs
```

Decision:

> Declining a pending transfer remains allowed from `DURING_VISIT+`.

Do not add `EnsureTransferWindowOpen()` to the decline handler.

Existing transfer-decline semantics are correct:

- current contact stays;
- campus decision/host/schedule stay;
- pending transfer is settled as declined;
- tokens are invalidated;
- audit/event is written.

---

# 5. Frontend changes

## 5.1 `ContactIdentityActions.tsx`

Path:

```text
frontend/pems-react/src/features/visit-request/components/ContactIdentityActions.tsx
```

Do **not** add business logic such as:

```tsx
if (status === "DURING_VISIT") { ... }
```

The component must continue to render from backend `allowedActions`.

Expected behavior after backend/read-model fix:

```text
WAITING_CONTACT_CONFIRMATION
    -> update/replace actions as allowed

WAITING_REQUEST_APPROVAL
    -> update/replace actions as allowed

ASSIGNED
    -> update/transfer actions as allowed

BEFORE_VISIT
    -> update/transfer actions as allowed

DURING_VISIT+
    -> no mutation action
    -> cancel cleanup may remain visible if there is a pending change
```

If the current frontend groups cancel under the same contact-management menu, preserve the existing capability-driven rendering unless UX specifically requires relocation.

## 5.2 i18n/text audit

Audit:

```text
frontend/pems-react/src/shared/i18n/locales/vi/visitRequestV2.json
frontend/pems-react/src/shared/i18n/locales/en/visitRequestV2.json
```

Search for text implying:

- transfer must happen 24 hours before start;
- transfer is unavailable because the visit begins within 24 hours;
- similar “lead time” wording specific to Operational Contact.

Remove/update those messages.

Do **not** remove text saying a transfer invitation/link is valid for 24 hours if that text refers to token validity.

---

# 6. Regression tests

Primary files:

```text
tests/PEMS.IntegrationTests/VisitRequests/OperationalContactManagementTests.cs
tests/PEMS.IntegrationTests/VisitRequests/OperationalContactConfirmationWorkflowTests.cs
tests/PEMS.IntegrationTests/VisitRequests/PerCampusFormV2ReadTests.cs
frontend/pems-react/src/features/visit-request/__tests__/ContactIdentityActions.test.tsx
```

Also review:

```text
tests/PEMS.ArchitectureTests/VisitLeadTimeScopeTests.cs
```

No need to weaken the existing 72-hour isolation assertions.

---

## 6.1 Profile edit matrix

Add/update tests:

| Case | Expected |
|---|---|
| profile edit at `WAITING_CONTACT_CONFIRMATION` | success |
| profile edit at `WAITING_REQUEST_APPROVAL` | success |
| profile edit at `ASSIGNED` | success |
| profile edit at `BEFORE_VISIT` | success |
| profile edit at `DURING_VISIT` | conflict / 409 |
| profile edit at `AFTER_VISIT` | conflict / 409 |
| profile edit at `CLOSED` | conflict / 409 |
| profile edit at `CANCELLED` | rejected |
| profile edit at `REJECTED` | rejected |

Assertions for rejection should prove:

- contact fields unchanged;
- no new audit mutation pretending success;
- no identity-change row created.

---

## 6.2 Transfer initiation matrix

| Case | Expected |
|---|---|
| transfer at `ASSIGNED` | success |
| transfer at `BEFORE_VISIT` | success |
| transfer at `DURING_VISIT` | conflict / 409 |
| transfer at `AFTER_VISIT` | conflict / 409 |
| transfer at `CLOSED` | conflict / 409 |
| transfer 1 minute before `PlannedStartAt`, status still `BEFORE_VISIT` | **success** |
| planned start is >24h away but status is already `DURING_VISIT` | **reject** |

The last two tests prove the rule is lifecycle-based, not clock-based.

---

## 6.3 Critical stale-transfer acceptance test

Mandatory regression case:

```text
Given
  campus is BEFORE_VISIT
  current contact = A
  registrant/current contact initiates transfer to C
  transfer is PENDING

And
  campus progresses to DURING_VISIT

When
  C tries to accept the still-pending transfer

Then
  request is rejected
  OperationalContactUserId remains A
  transfer is not APPLIED
  C does not obtain contact rights
  A retains contact rights
```

Also repeat for at least one later state such as `AFTER_VISIT` or `CLOSED`.

---

## 6.4 Resend-transfer lifecycle tests

Add:

```text
Pending TRANSFER + BEFORE_VISIT + valid resend budget
    -> resend succeeds

Pending TRANSFER + DURING_VISIT
    -> resend rejected

Pending TRANSFER + AFTER_VISIT
    -> resend rejected
```

On rejection assert:

- `TokenVersion` unchanged;
- `ResendCount` unchanged;
- `ExpiresAt` unchanged;
- old token state not invalidated by a failed resend attempt;
- no replacement token created.

---

## 6.5 Cancel cleanup tests

Add/retain:

```text
Pending TRANSFER + DURING_VISIT
    -> cancel succeeds

Pending TRANSFER + AFTER_VISIT
    -> cancel succeeds
```

Assert:

- identity change becomes `CANCELLED`;
- current Operational Contact does not change;
- outstanding tokens are invalidated;
- audit/event is created;
- no transfer is applied.

This protects the agreed “cleanup remains allowed” semantics.

---

## 6.6 Decline cleanup tests

Add/retain:

```text
Pending TRANSFER + DURING_VISIT
    -> invited person may decline
```

Assert:

- identity change becomes `DECLINED`;
- current contact remains unchanged;
- tokens invalidated;
- campus decision/host/schedule unchanged.

---

## 6.7 Read-model capability tests

`PerCampusFormV2ReadTests.cs` should prove backend capabilities match command guards.

Expected contact action matrix:

### `WAITING_CONTACT_CONFIRMATION`

Registrant:

```text
UPDATE_OPERATIONAL_CONTACT_PROFILE
REPLACE_OPERATIONAL_CONTACT
```

plus existing pending invitation actions where applicable.

### `WAITING_REQUEST_APPROVAL`

Registrant:

```text
UPDATE_OPERATIONAL_CONTACT_PROFILE
REPLACE_OPERATIONAL_CONTACT
```

### `ASSIGNED`

Authorized registrant/current contact:

```text
UPDATE_OPERATIONAL_CONTACT_PROFILE
INITIATE_OPERATIONAL_CONTACT_TRANSFER
```

when no transfer is pending.

### `BEFORE_VISIT`

Authorized registrant/current contact:

```text
UPDATE_OPERATIONAL_CONTACT_PROFILE
INITIATE_OPERATIONAL_CONTACT_TRANSFER
```

even if the campus is less than 24 hours or only minutes from `PlannedStartAt`.

### `DURING_VISIT`, `AFTER_VISIT`, `CLOSED`

No contact mutation actions:

```text
UPDATE_OPERATIONAL_CONTACT_PROFILE      absent
REPLACE_OPERATIONAL_CONTACT             absent
INITIATE_OPERATIONAL_CONTACT_TRANSFER   absent
RESEND_OPERATIONAL_CONTACT_CONFIRMATION absent for pending TRANSFER
```

If a pending transfer exists:

```text
CANCEL_OPERATIONAL_CONTACT_CHANGE       present
```

for an authorized cleanup actor, following current permission rules.

### `REJECTED`

No contact mutation action. Preserve the separate resubmit capability if current workflow allows it.

---

## 6.8 Frontend tests

Update `ContactIdentityActions.test.tsx` only as necessary to reflect server capabilities.

Frontend tests should not encode the lifecycle matrix by status. They should assert:

- action present in `allowedActions` → button/menu action rendered;
- action absent → not rendered;
- cancel cleanup can render even when mutation actions are absent.

---

# 7. Acceptance criteria

The implementation is complete only when all criteria below pass.

### AC1 — Profile lifecycle

Operational Contact profile may be edited only while campus is:

```text
WAITING_CONTACT_CONFIRMATION
WAITING_REQUEST_APPROVAL
ASSIGNED
BEFORE_VISIT
```

### AC2 — Replace lifecycle

Operational Contact identity/email may be replaced directly only while campus is undecided:

```text
WAITING_CONTACT_CONFIRMATION
WAITING_REQUEST_APPROVAL
```

### AC3 — Transfer lifecycle

Operational Contact transfer may be initiated only while campus is:

```text
ASSIGNED
BEFORE_VISIT
```

### AC4 — Lock from visit start

From `DURING_VISIT` onward no Operational Contact mutation may occur:

```text
profile edit      blocked
replace           blocked
start transfer    blocked
accept transfer   blocked
resend transfer   blocked
```

### AC5 — Cleanup remains allowed

A pending transfer may still be:

```text
cancelled by an authorized management actor
declined by the invited actor
```

from `DURING_VISIT+`, because these operations do not change the current Operational Contact.

### AC6 — No Operational Contact lead-time cutoff

Operational Contact management does not use a 6-hour or 24-hour pre-start cutoff.

A status of `BEFORE_VISIT` is sufficient regardless of remaining minutes/hours before `PlannedStartAt`.

### AC7 — Invitation validity remains

`TransferValidityHours = 24` remains unchanged. This is token/invitation validity, not authorization to apply the transfer after lifecycle lock.

### AC8 — Read/write consistency

Backend command guards and `VisitFormReadService` must return the same effective capability decision.

The UI must not offer an action that the corresponding backend command rejects solely because the read model used an obsolete time rule.

### AC9 — Existing authorization remains

Per-campus ownership, registrant/current-contact permissions, account eligibility, token authentication and current-contact handover semantics remain unchanged.

### AC10 — Atomicity

A rejected accept/resend after `DURING_VISIT` must not partially mutate:

- current contact relation;
- identity-change status;
- token version;
- resend count;
- invitation expiry;
- token validity state.

---

# 8. Suggested implementation order for the agent

Execute in this order to minimize temporary inconsistency:

1. **Verify branch**
   ```bash
   git checkout Dev
   git pull --ff-only
   git rev-parse HEAD
   ```
   If HEAD is no longer `8f03c4f0e3d2257035cf8819b9135c2970e634cd`, re-read the affected files before editing and preserve the semantics in this plan.

2. **Change shared guards**
   - `OperationalContactGuards.cs`
   - remove `TransferLeadHours`
   - tighten profile lifecycle
   - make transfer lifecycle-only

3. **Fix transfer initiation**
   - `InitiateOperationalContactTransferCommandHandler.cs`

4. **Fix stale transfer acceptance**
   - `AcceptOperationalContactConfirmationCommandHandler.cs`

5. **Fix transfer resend**
   - `ResendOperationalContactConfirmationCommandHandler.cs`

6. **Align read-model allowed actions**
   - `VisitFormReadService.cs`

7. **Preserve cleanup semantics**
   - verify `CancelOperationalContactChangeCommandHandler` has no lifecycle mutation guard
   - verify `DeclineOperationalContactConfirmationCommandHandler` remains cleanup-only

8. **Do not change router**
   - verify `SaveOperationalContactCommandHandler.cs` remains classification-only

9. **Audit frontend text**
   - remove obsolete 24-hour pre-start wording
   - keep 24-hour invitation-expiry wording

10. **Add/update tests**
    - management matrix
    - stale accept
    - resend lock
    - cancel/decline cleanup
    - read-model actions
    - frontend capability rendering

11. **Run focused test suites**

12. **Run broader regression gates**

---

# 9. Search/audit checklist

Before completing the fix, search the repository for obsolete rules:

```bash
rg -n "TransferLeadHours|ContactTransferLeadHours" .
rg -n "24 giờ|24 hours|24-hour|24 hour" backend frontend tests docs
rg -n "EnsureTransferWindowOpen" backend tests
rg -n "UpdateOperationalContactProfile|InitiateOperationalContactTransfer|ResendOperationalContactConfirmation|CancelOperationalContactChange" backend frontend tests
```

Expected after fix:

```text
TransferLeadHours
    -> no production reference

ContactTransferLeadHours
    -> no production reference
```

Do **not** blindly delete all “24 hours” references. Classify each reference:

```text
transfer lead-time before visit   -> remove/update
transfer invitation validity      -> keep
unrelated workflow 24h rule       -> leave untouched
```

Also audit comments and test names; stale tests that still assert “inside 24h transfer must fail” must be rewritten, not merely deleted.

---

# 10. Validation commands

Use the repository's existing commands/configuration. At minimum run focused suites:

```bash
dotnet test tests/PEMS.IntegrationTests/PEMS.IntegrationTests.csproj \
  --filter "FullyQualifiedName~OperationalContact"

dotnet test tests/PEMS.IntegrationTests/PEMS.IntegrationTests.csproj \
  --filter "FullyQualifiedName~PerCampusFormV2ReadTests"

dotnet test tests/PEMS.ArchitectureTests/PEMS.ArchitectureTests.csproj
```

Frontend:

```bash
cd frontend/pems-react
npm run typecheck
npm test -- ContactIdentityActions
```

Then run normal project regression gates if available:

```bash
dotnet test
npm test
npm run build
```

Do not mark the task complete based only on compilation.

---

# 11. Agent implementation constraints

The implementing agent must follow these rules:

1. **Backend is the final authority.**
   UI visibility is not security.

2. **One lifecycle rule, centralized.**
   Do not duplicate `DURING_VISIT` checks across every handler when an existing guard is the right abstraction.

3. **Re-check lifecycle at execution time.**
   Initiating a transfer before the visit does not grant permanent permission to accept/resend it later.

4. **Do not infer from `PlannedStartAt`.**
   For Operational Contact mutation authorization, persisted campus status is authoritative.

5. **Keep token expiry separate from lifecycle authorization.**
   A token may still be unexpired but no longer legally applicable because the campus is `DURING_VISIT+`.

6. **Cleanup is allowed; mutation is not.**
   Cancel and decline must not be accidentally blocked by reusing the transfer mutation guard.

7. **No partial writes on lifecycle rejection.**
   Check lifecycle before token invalidation/version/expiry/contact changes.

8. **Do not introduce a frontend status rule.**
   Keep `ContactIdentityActions.tsx` driven by `allowedActions`.

9. **Preserve per-campus isolation.**
   Never authorize based only on broad role.

10. **Do not alter unrelated lead-time policies.**
    The removal is specific to Operational Contact management.

---

# 12. Expected final behavior examples

## Example A — last-minute transfer is allowed before visit starts

```text
Campus status: BEFORE_VISIT
PlannedStartAt: 09:00
Current time: 08:59

Initiate transfer
=> SUCCESS
```

No 24-hour cutoff.

If the invitee accepts while campus remains `BEFORE_VISIT`:

```text
Accept
=> SUCCESS
```

## Example B — pending transfer becomes stale at visit start

```text
08:55 BEFORE_VISIT
A -> transfer to C
=> PENDING

09:00 campus -> DURING_VISIT

09:02 C clicks Accept
=> REJECT
=> A remains current contact
```

## Example C — stale transfer cannot be prolonged

```text
DURING_VISIT
pending transfer exists

Resend
=> REJECT
=> ExpiresAt unchanged
=> ResendCount unchanged
=> TokenVersion unchanged
```

## Example D — stale transfer can be cleaned up

```text
DURING_VISIT
pending transfer exists

Current authorized contact/registrant clicks Cancel
=> SUCCESS
=> current contact unchanged
=> pending transfer CANCELLED
```

or:

```text
Invited target clicks Decline
=> SUCCESS
=> current contact unchanged
=> pending transfer DECLINED
```

## Example E — profile update blocked after start

```text
DURING_VISIT
same email, changed phone

SaveOperationalContact
=> routes to UpdateOperationalContactProfile
=> EnsureProfileUpdateAllowed rejects
=> no field changed
```

---

# 13. Documentation follow-up after code fix

This task is primarily a code fix, but after implementation the project documents should be aligned.

Audit/update business wording in:

- UC-18 Operational Contact transfer
- UC-19 Operational Contact profile update
- SRS / RTW business rules related to Operational Contact
- User Manual text that implies contact profile can still be edited after `CLOSED`

Required documentation distinction:

```text
Old:
transfer requires >= 24 hours before planned start

New:
transfer is allowed while campus is ASSIGNED or BEFORE_VISIT
```

Keep:

```text
transfer invitation validity = 24 hours
```

Do not conflate these two rules.

---

# 14. Definition of Done

The fix is done when:

- [ ] `TransferLeadHours` removed.
- [ ] `ContactTransferLeadHours` removed.
- [ ] Profile edit allowed only in the four agreed pre-visit statuses.
- [ ] Transfer initiation allowed in `ASSIGNED` and `BEFORE_VISIT`.
- [ ] Transfer initiation has no clock cutoff.
- [ ] Pending transfer cannot be accepted at `DURING_VISIT+`.
- [ ] Pending transfer cannot be resent at `DURING_VISIT+`.
- [ ] Pending transfer can still be cancelled at `DURING_VISIT+`.
- [ ] Pending transfer can still be declined at `DURING_VISIT+`.
- [ ] Read-model `allowedActions` matches command behavior.
- [ ] Frontend has no duplicate lifecycle business rule.
- [ ] 24-hour invitation validity remains intact.
- [ ] Existing per-campus permissions remain intact.
- [ ] No DB/schema/API-contract change introduced.
- [ ] Regression tests cover lifecycle matrix and stale-transfer race.
- [ ] Focused backend tests pass.
- [ ] Architecture tests pass.
- [ ] Frontend typecheck/tests pass.
- [ ] Broader regression/build gates pass.

---

## Source-code baseline reviewed

The plan was prepared against `Dev` HEAD:

```text
8f03c4f0e3d2257035cf8819b9135c2970e634cd
```

Primary code reviewed:

```text
backend/PEMS.Application/Delegations/Commands/OperationalContact/OperationalContactGuards.cs
backend/PEMS.Application/Delegations/Commands/OperationalContact/InitiateOperationalContactTransferCommandHandler.cs
backend/PEMS.Application/Delegations/Commands/OperationalContact/AcceptOperationalContactConfirmationCommandHandler.cs
backend/PEMS.Application/Delegations/Commands/OperationalContact/ResendOperationalContactConfirmationCommandHandler.cs
backend/PEMS.Application/Delegations/Commands/OperationalContact/DeclineOperationalContactConfirmationCommandHandler.cs
backend/PEMS.Application/Delegations/Commands/OperationalContact/ManageOperationalContactHandlers.cs
backend/PEMS.Application/Delegations/Commands/OperationalContact/SaveOperationalContactCommandHandler.cs
backend/PEMS.Application/Delegations/Commands/OperationalContact/UpdateOperationalContactProfileCommandHandler.cs
backend/PEMS.Application/Delegations/Services/VisitFormRead/VisitFormReadService.cs
frontend/pems-react/src/features/visit-request/components/ContactIdentityActions.tsx
tests/PEMS.IntegrationTests/VisitRequests/OperationalContactManagementTests.cs
tests/PEMS.IntegrationTests/VisitRequests/OperationalContactConfirmationWorkflowTests.cs
tests/PEMS.IntegrationTests/VisitRequests/PerCampusFormV2ReadTests.cs
tests/PEMS.ArchitectureTests/VisitLeadTimeScopeTests.cs
```
