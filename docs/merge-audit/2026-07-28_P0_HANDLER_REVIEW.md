---
type: merge-audit
feature: _shared
status: final
updated: 2026-07-28
---

# P0 handler review — Dev → Cảnh-Iter1 integration branch

Reviewer read of the **final merged code** on `merge/dev-into-canh-iter1`, not of the diff and not of
the test results. The question each section answers is "does this file, as it now stands, still do
what both sides needed it to do" — a test suite that passes proves the assertions it makes, not the
ones nobody wrote.

Baseline: merge commit `8538c93f`, parents `52d666bb` (Cảnh-Iter1) + `d732e651` (Dev).

## Summary

| # | Handler | Result |
|---|---|---|
| 5.1 | UpdateAccountRoleCommandHandler | PASS |
| 5.2 | InviteVisitParticipantCommandHandler | PASS |
| 5.3 | AssignDepartmentStaffCommandHandler | **PASS after fix** — 2 defects found, both corrected |
| 5.4 | AssignRequestAssigneeCommand | PASS |
| 5.5 | PrepareVisitLogisticsCommandHandler | PASS |
| 5.6 | ProposeRequestChangeCommand | PASS |

Two defects were found, both in 5.3. Neither was caused by the merge picking the wrong side; one was
absent from **both** parents, the other is a case of correct prose from one side surviving onto
replaced code from the other. Details in 5.3.

## Shared foundation — IUserMutationLockService

Verified once here rather than repeated per handler.

`backend/PEMS.Infrastructure/Persistence/MySqlUserMutationLockService.cs` issues
`SELECT … WHERE id IN (…) ORDER BY id FOR UPDATE` after `ids.Distinct().OrderBy(id => id)`. Ordering
is applied in the implementation, so **callers cannot get it wrong by passing ids in request order** —
which is exactly what 5.1 relies on when it locks the target and the replacement head in one call.
Empty collections are a no-op, and a non-relational provider (the unit suite's EF InMemory context)
returns early, so unit tests exercise the handlers without needing row locks.

`IUserMutationLockService` appears in **14** handlers.

---

## 5.1 UpdateAccountRoleCommandHandler

- **Result:** PASS
- **File:** `backend/PEMS.Application/Accounts/Commands/UpdateAccountRole/UpdateAccountRoleCommandHandler.cs`
- **Relevant methods:** `Handle`, `HandOverDepartmentHeadAsync`, `EnsureStaffLeaderManageableTarget`, `EnsureStaffLeaderManageableNewRole`, `SendRoleChangedNotificationAsync`

**Transaction:** opened at `:61`, before any read.

**Lock:** `:72`, **before** the first read of the target at `:74`. The replacement department head is
included in the *same* `LockUsersAsync` call at `:68-71` rather than a second call, so the ascending
order is decided by the service. Departments are locked at `:228`, always **after** users, so no two
flows can hold the two resources in opposite order.

**Authorization:** non-privileged callers must have a campus and may only touch their own campus
(`:80-86`); a Staff Leader may not change their own role (`:100`), may not touch a `LOCKED` account
(`:102`), and both the target shape and the requested shape are restricted to the three manageable
shapes (`:106-107`, `:467-492`). Identity editability is derived from the target's **original** role
read from the DB, not from `NewRoleCode` (`:125-128`) — so the dropdown cannot widen a caller's rights.

**Business rules:**
- Ordering is the guarantee: everything up to `:308` writes to *locals only*. The entity is first
  mutated at `:310`, after the dependency check at `:241-253`. A blocked request therefore leaves the
  row byte-for-byte unchanged.
- The no-op path (`:209-220`) commits and returns early with `RevokedSessions = 0` — no audit row, no
  `UpdatedAt` bump, no session revoke, no email, no department write.
- Department head is never vacated silently: if the target still heads a department, the
  `DEPARTMENT_HEAD_ASSIGNMENT` blocker refuses the change. A successor supplied by the caller is
  handed over at `:236-239` **before** the dependency check, so the check reads a department whose
  `head_user_id` has already moved. Every refusal inside the handover throws rather than skipping
  silently, and the throw rolls the handover back with the rest of the transaction.
- A successor sent with a change that does not vacate a head seat is refused (`:258-261`), not dropped.

**Email:** `ISystemEmailDispatcher` only — `IEmailService` is not injected. Template is
`SystemEmailTemplates.AccountRoleChanged` (`:508`). It carries **both** the old and the new role label,
which is what makes the notice actionable. The send is post-commit (`:358`) and wrapped in a `catch`
(`:523-526`) so a dead SMTP server cannot fail a role change that is already durable.

**Ordering, verified literally:** `SaveChangesAsync :349` → `CommitAsync :350` → `RevokeAllActiveSessionsAsync :354` → email `:358` → return.

**Tests:** `tests/PEMS.UnitTests/Accounts/UpdateAccountRole/` (incl. Dev's `AssertNoSideEffects`, kept
and rewired onto the dispatcher), `tests/PEMS.IntegrationTests/Accounts/AccountRoleChangeConcurrencyTests.cs`.

**Reviewer notes:** the strongest handler of the six. The "resolve into locals, mutate last" discipline
is what makes the no-side-effect guarantee checkable rather than merely claimed.

---

## 5.2 InviteVisitParticipantCommandHandler

- **Result:** PASS
- **File:** `backend/PEMS.Application/Delegations/Commands/InviteVisitParticipant/InviteVisitParticipantCommandHandler.cs`
- **Relevant methods:** `Handle`, `ResolveInviteeAsync`, `ResolveContentAsync`, `AttachTo`, `MarkDeliveryFailedAsync`

**Transaction:** `:150`. **Lock:** `:158`, the first statement inside it.

**Re-resolve after lock:** `:160` calls `ResolveInviteeAsync` a second time and compares both the
resolved user id and the participant role against the pre-lock values (`:161`); any drift raises
`ConflictException` (`:162-163`). This is the check that makes the invite safe against a concurrent
role change — the first resolve at `:98` was unlocked and is treated as advisory.

**Authorization:** host-only (`:92`) and prep-window-only (`:94`), both re-derived from the instance
rather than trusted from the request. Each invitee type is re-validated against the DB in
`ResolveInviteeAsync` (`:392-480`), so a direct API call cannot bypass the candidate query: IC support
must be active STAFF (staff or leader sub-role) of an **active IC** department on the same campus and
must not be the current host; students must be active and same-campus; a department invite resolves
the *leader* server-side — the frontend never supplies the leader id.

**Template selection:** decided by the resolved participant role at `:141-145`, one of exactly three
(`VisitDepartmentLeaderInvitation` / `VisitStudentInvitation` / `VisitParticipantInvitation`). **The
host never picks the template.**

**recipientDeptId:** resolved at `:98`, returned from the DEPT branch at `:476`, and used at `:259-261`
to route a department recipient to `/dashboard/departments/{deptId}/invitations/{participantId}` while
an ordinary participant goes to the visit-process participants tab. Both routes intact.

**Token safety:** only `_tokens.Hash(raw)` is persisted (`:245-246`); the raw token exists solely inside
the URL embedded in the message (`:206-207`). The accept/decline block is built by the backend in
**both** content modes (`:230-231`), so a host who rewrites the message cannot move, forge or omit the
buttons — and `AuthoredByUser.Create` rejects a hand-written action block outright.

**Email:** `PrepareAsync :214` inside the transaction; `CommitAsync :283`; `DeliverAsync :296` after.
Attachment bytes are streamed post-commit so a slow file store cannot hold a DB transaction open, and
a failure there is recorded as `FAILED` (resendable) rather than thrown.

**Tests:** `tests/PEMS.UnitTests/Delegations/InviteVisitParticipant/`, plus integration coverage of the
three-template split.

**Reviewer notes:** this file merged textually clean, which is precisely why it was re-read in full.
Confirmed correct.

---

## 5.3 AssignDepartmentStaffCommandHandler

- **Result:** **PASS after fix** — two defects found and corrected on the integration branch
- **File:** `backend/PEMS.Application/Delegations/Commands/AssignDepartmentStaff/AssignDepartmentStaffCommandHandler.cs`

**Transaction:** `:80`. **Lock:** `:81`, immediately after.

### Defect 1 — status was never re-checked (fixed)

The spec for this phase requires re-reading **role, department and status** under the lock. The merged
code checked role and department (`:104`) and **not status**. A deactivated, locked or not-yet-confirmed
account could therefore be handed a live visit responsibility.

This matters beyond tidiness: `UserStatuses.PendingEmailConfirmation` is documented in
`backend/PEMS.Domain/Constants/AuthConstants.cs:38-45` as an account that "cannot log in … and holds no
effective authority". Assigning one creates a task whose owner cannot sign in to act on it. The sibling
flow `AssignRequestAssigneeCommand` already re-checked `Status == "ACTIVE"` in the same position, so the
two assignment paths disagreed with each other.

**Provenance — not merge-induced.** `git show d732e651:…` and `git show 52d666bb:…` confirm neither
parent had the check. This is a pre-existing gap in both lines that the merge inherited; it is reported
here because this review is the first time the file was read against the requirement.

**Fix:** added after the role/department check, reading the entity already loaded under the lock:

```csharp
if (targetStaff.Status != UserStatuses.Active)
    throw new ConflictException("Người được phân công phải là tài khoản đang hoạt động.");
```

**Test:** `A_leader_may_not_assign_an_account_that_is_not_active`, a `[Theory]` over `INACTIVE`,
`LOCKED` and `PENDING_EMAIL_CONFIRMATION`, asserting the refusal **and** that no participant row, no
`sent_emails` row and no token were written. `DelegationsTestData.CreateUser` already defaults to
`ACTIVE` with a per-call override, so no existing test needed changing. Targeted run: **13/13 passed**
(10 pre-existing + 3 new cases).

### Defect 2 — the class documentation contradicted the code (fixed)

The XML doc claimed, in bold, that the handler is "a sequence of saves with **no explicit
transaction**" and that making it atomic "would be a change to this command's lifecycle, which is a
separate decision".

That was true of Cảnh-Iter1. Dev then added the transaction and the lock. The merge correctly took
Dev's code — and correctly took Cảnh's surrounding prose, which had become false. Git cannot detect
this class of defect: both hunks were individually right.

Left uncorrected, the next reader is told the atomicity decision is still open when it has already been
made, and might "restore" the non-transactional shape. The comment now describes the transaction, the
lock and the commit-before-SMTP ordering that the code actually implements.

**Business rules (unchanged):** leader-only (`:88`); the invited participant must belong to the
caller's department (`:92`); the assignee must be DEPARTMENT and in the same department (`:104`); a
previously DECLINED/REMOVED row is reused rather than duplicated (`:149-178`); pending tokens on both
the leader's row and any prior assignment are invalidated (`:186-191`) so two live accept links never
coexist.

**Email:** dispatcher only, template `VisitDepartmentStaffAssignment` — an *assignment*, not the
invitation template it used to be recorded against. Prepare `:203` → `SaveChanges :246` →
`Commit :248` → `Deliver :257`. Participant row, tokens, attachments and notification all commit
together with the business mutation.

**Tests:** `tests/PEMS.UnitTests/Delegations/AssignDepartmentStaff/AssignDepartmentStaffCommandHandlerTests.cs`.

---

## 5.4 AssignRequestAssigneeCommand

- **Result:** PASS
- **File:** `backend/PEMS.Application/DepartmentReceptionTasks/Commands/AssignRequestAssignee/AssignRequestAssigneeCommand.cs`

**Transaction:** `:136`. **Lock:** `:142`, first statement inside.

**Re-read after lock:** `:144-148` re-queries the assignee for `DepartmentId == caller's department`
**and** `Status == "ACTIVE"`; failure raises `ConflictException` (`:149-151`). The pre-lock read at
`:97-101` applies the same predicate and is advisory.

**Guards, all present and all before the write:**

| Guard | Line | Behaviour |
|---|---|---|
| Department scope | `:78-79` | cannot assign another department's request |
| Terminal / in-flight status | `:82-84` | blocks ASSIGNED, ACCEPTED, CHANGE_PROPOSED, IN_PROGRESS, DONE, CANCELLED, REJECTED, DECLINED |
| Pending assignment attempt | `:86-89` | refuses while an attempt is still PENDING |
| Handover already signed | `:91-95` | refuses once either party has signed |
| Schedule conflict | `:111-119` | `ScheduleConflictChecker` over the item's usage window, falling back to the campus planned window |

**Priority:** absent. The email variables are `assigneeName`, `logisticsTitle`, `dueAt`, `campusName`,
`delegationName` (`:188-195`) — `dueAt` read from the **persisted** `l.DueAt`, which 5.5 computed. The
notification title/message/action-url (`:214-229`) carry no priority either.

**Email:** dispatcher, `SystemEmailTemplates.LogisticsAssigneeAssignment`, Prepare `:184` →
`Commit :234` → `Deliver :241`.

**Reviewer notes:** the drift message says "vai trò hoặc phòng ban … vừa thay đổi" while the predicate
tests department + status, not role directly. This is correct in practice rather than by accident: a
role change routes through `UpdateAccountRoleCommandHandler`, whose resolved shape moves or clears
`DepartmentId` for every shape a department staffer can become, so the department predicate catches it.
Recorded as an observation, not a defect.

---

## 5.5 PrepareVisitLogisticsCommandHandler

- **Result:** PASS
- **File:** `backend/PEMS.Application/Delegations/Commands/PrepareVisitLogistics/PrepareVisitLogisticsCommandHandler.cs`
- **Command:** `PrepareVisitLogisticsCommand.cs`

**Command shape — the important part.** The record's full parameter list is `VisitInstanceId`,
`DepartmentId`, `ItemType`, `Title`, `Description`, `Quantity`, `UsageStartAt`, `UsageEndAt`,
`CoordinationMode`, `OfflineCoordinationNote`, `EmailOverride`. There is **no `Priority`** and **no
`DueAt`** — the client cannot express either, so neither can be smuggled in.

**Deadline is computed server-side**, once, at `:161`:

```csharp
var dueAt = offline ? (DateTime?)null : usageStart?.AddHours(-24);
```

persisted at `:207` and rendered into the email at `:241` from that same local. `OFFLINE_COORDINATED`
therefore stores `due_at = NULL` and is created `DONE` (`:200`) — there is no department workflow for it
to be late for. `SYSTEM_REQUEST` starts `REQUESTED`.

**Static confirmation:** `LogisticsPriorityText`, `request.Priority` and `request.DueAt` return **zero**
matches across `backend/` and `frontend/`.

**Authorization:** host-only (`:78`), prep-window-only (`:80`), target department must be on the same
campus (`:98`), must be GENERAL and active (`:100`). For `SYSTEM_REQUEST` the recipient leader is
resolved server-side (`:107-123`) preferring `head_user_id`. A department is required for
`SYSTEM_REQUEST` (`:127`) and an offline note is required for `OFFLINE_COORDINATED` (`:129`).

**Duplicate suppression:** one active item per fixed `(item_type, title)` category, re-checked
server-side at `:169-182` — the UI hiding the create form is explicitly not treated as sufficient.

**Email:** dispatcher, `LOGISTICS_REQUEST_TO_DEPARTMENT`, no fallback body. `SaveChanges :297` →
`Commit :298` → `Deliver :313`. Offline mode sends nothing and ignores any email override (`:133-138`).

**Frontend:** `LogisticsRequestSection.tsx` no longer sends `dueAt`; it reproduces the −24h rule in a
`deadlineFor()` helper purely so the on-screen preview matches the email the backend will send.

---

## 5.6 ProposeRequestChangeCommand

- **Result:** PASS
- **File:** `backend/PEMS.Application/DepartmentReceptionTasks/Commands/ProposeRequestChange/ProposeRequestChangeCommand.cs`

**Validation:**

| Rule | Line |
|---|---|
| Proposal note mandatory (falls back to proposed description, then throws) | `:66-68` |
| `ProposedQuantity >= 1` when supplied | `:69` |
| Proposed quantity **strictly less than** the original when an original exists | `:78-79` |
| Proposed end after proposed start | `:96-97` |
| Department scope — proposer's department must own the request | `:83-84` |
| Department **staff** may only propose on an item assigned to them | `:86-89` |

**Multi-day:** `ProposedUsageStartAt` / `ProposedUsageEndAt` are parsed as full `DateTime` values
(`:92-95`) with `DateTimeKind.Unspecified` (wall-clock, per the project's no-UTC rule), so a proposal
spanning days is representable and validated only by end-after-start.

**Original quantity is never overwritten:** the write block at `:113-117` touches `ProposedQuantity`,
`ProposedDescription`, `ProposalNote`, `ProposedUsageStartAt`, `ProposedUsageEndAt` — `l.Quantity` is
not assigned anywhere in the file.

**Auto-assign:** `:105-110` sets the proposer as assignee **only** when `AssignedToUserId == null`, so a
leader proposing before delegating takes ownership, and a staff member proposing never steals an item
already assigned to someone else.

**State:** `Status = "CHANGE_PROPOSED"` (`:119`) with the previous proposal response cleared
(`:122-125`), and prior pending tokens invalidated (`:131-133`) so a stale email cannot act on an item
now awaiting the host's decision.

**Email — this is the §5.6 requirement that needed more than picking a side.** Cảnh-Iter1's dispatcher
call passed `proposalNote` alone. A proposal is a counter-offer; sending only the rationale forced the
host into the portal to discover the numbers they were being asked to approve. The merged handler sends
the offer itself (`:185-194`): `originalQuantity`, `proposedQuantity`, `proposedUsageStartAt`,
`proposedUsageEndAt`, `proposedDescription`, `proposalNote`, alongside `hostName`, `logisticsTitle`,
`departmentName`, `delegationName`. Omitted fields render as "Không đổi" via the `Unchanged` constant
(`:37-40`) rather than as a blank cell, because a blank reads as "they propose nothing here" — the
opposite of "no change here". The template body and the code-side registry entry in
`SystemEmailTemplates.cs` were both extended to match, in both languages.

**No priority.** **Ordering:** the business mutation is saved at `:135`, *before* `SendProposalEmailAsync`
is even called (`:139`); inside it, the tokens + notification commit at `:230-232` and delivery happens
at `:237`.

**Reviewer notes:** the handler throws bare `Exception` for its business refusals rather than the typed
exceptions used elsewhere. That is pre-existing Dev style in this file and changing it would move HTTP
status codes, so it is out of scope here — recorded as debt, not corrected under a merge.

---

## What this review changed

| File | Change |
|---|---|
| `AssignDepartmentStaffCommandHandler.cs` | added the missing `Status == ACTIVE` re-check; corrected the class doc that contradicted the code |
| `AssignDepartmentStaffCommandHandlerTests.cs` | added `A_leader_may_not_assign_an_account_that_is_not_active` (Theory ×3) |

No assertion was relaxed, no test was deleted, and no lock, transaction or dependency check was removed.
