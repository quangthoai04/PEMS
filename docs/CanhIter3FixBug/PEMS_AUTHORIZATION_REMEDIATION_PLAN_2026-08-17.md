# PEMS Authorization Remediation — Implementation Plan (SEC-01..21)

*Revision 6 (approved) — correction on top of rev.5's approval. Change: Staff Leader's Visit scope in PEMS is
strictly `PrimaryCampusId` jurisdiction — it is now its own exclusive branch in the SEC-12 visibility
computation, never expanded by unioning in a Staff Leader's separate Host/Participant/OperationalContact
relationships on other campuses. The union computation (still used, per rev.5) applies only to the
remaining relationship types: Host, Department Staff assignment, Operational Contact, Participant. The one
exception: a Staff Leader who is ALSO the request's registrant still sees the whole request via the
registrant relationship — the campus-jurisdiction narrowing only kicks in for a Staff Leader who is not the
registrant. All other rev.5 content (SEC-01..21 except this one correction) is unchanged and approved.*

*Revision 5 — final review pass. Changes: SEC-09's shared transfer service now handles third-party
concurrency correctly (pre-read → lock old+new together → lock department → always re-verify, not just
when the actor claims to be the head); SEC-12 no longer treats `CreatedBy` as request ownership (only
`RegistrantUserId` is, per domain model); the Minutes+Admin gap flagged-but-not-fixed in rev.4 is now
actually closed.*

## Context

`docs/CanhIter3FixBug/PEMS_FULL_SECURITY_AUTHORIZATION_BUGS_REMEDIATION_2026-08-17.md` documents 21
authorization/access-control defects found in an audit of `Dev` @ `ceeb4e3b`, which is exactly what local
`Dev` points at; `Canh_iter3_FixBug` is 4 commits ahead with no overlapping backend authorization changes.
Every finding has been re-verified against the actual current code across six review passes. That audit doc
is the source of truth for each bug's original description, severity, and the fixed business rules (Photo
Upload = Host OR Accepted Participant of that exact Visit; Face Scan = Staff/Staff Leader AND Host of that
exact Visit; Visit Document Upload = Host of that exact Visit only; Admin has no internal Partner permission
but still sees Public Partner on the Homepage). This document is the final, resolved implementation design —
where it differs from the audit doc's own suggested fix, this document wins; it is the product of six review
rounds correcting assumptions the audit doc did not (and could not) resolve on its own.

Order: P0 → P1 → P2, matching the audit doc's own priority ordering. Items marked "unchanged since revN" did
not need correction across the review rounds — see the audit doc's own section for that item's full
description; where this document gives a design (P0-2/SEC-09, P0-4/P0-5/SEC-10-11, P1-4/SEC-12, and every
Admin-explicit-deny item), that design is authoritative over the audit doc's suggestion.

## Architecture summary — what's reused vs what's new

| Concept | Reality (verified) | Action |
|---|---|---|
| Account management gate | `IRoleAccessPolicy.CanAccessAccountManagement` (Admin/HO/StaffLeader). | Reuse for SEC-03/04. |
| Department personnel management gate | `IRoleAccessPolicy.CanAccessDepartmentManagement` (StaffLeader/DepartmentLead/HO) — coarse gate only. | Reuse for SEC-05..08. SEC-09 shares a transfer service with the canonical self-service flow (see P0-2). |
| `MinuteAccess` | `Evaluate` (materialized) + `WhereAuthorizedFor` (EF-translatable). `isHo` unconditional allow. | Both now explicitly deny Admin, first check, before Host/Participant — see P0-4/P0-5. |
| Visit-instance media/document/partner-link helpers | Narrow, purpose-specific checks (Upload/FaceScan/DocumentUpload/View-Delete/PartnerLink). | Every one explicitly denies Admin before Host/Participant relationship. |
| Department leadership transfer mechanics | Previously only correct in the canonical self-service handler. | The shared `IDepartmentLeadershipTransferService` has an explicit, uniform concurrency contract used by BOTH the self-service and third-party callers — see P0-2. |
| `GetSubmittedVisitRequestFormDetailQueryHandler` visibility model | Was a nested ternary of mutually-exclusive branches, prone to silently dropping a caller's legitimate access when they hold more than one relationship simultaneously. | Replaced with a union of independently-computed relationship sets for Host/Department-Staff/Operational-Contact/Participant. Staff Leader is carved OUT of that union into its own exclusive branch, confined to `PrimaryCampusId` only, EXCEPT when the Staff Leader is also the registrant — see P1-4. |
| Staff Leader Visit scope | A Staff Leader's authority over Visit data in PEMS is defined by campus jurisdiction (`PrimaryCampusId`) alone when they are not the registrant. It must never be widened by a separate personal relationship (Host, Participant, Operational Contact) they happen to also hold on a different campus's instance. When they ARE the registrant, the registrant relationship (sees-everything) applies instead — jurisdiction never narrows ownership. | Staff Leader is its own exclusive branch in SEC-12's visibility computation, evaluated only after the `isHo \|\| isRegistrant` check — see P1-4. |
| Request-level ownership | `RegistrantUserId` is the ONLY request-level owner. `CreatedBy` is a plain audit column (confirmed: `VisitRequestV2CreateService.cs` sets both from the same value at creation, but that is a creation-time coincidence, not a business relationship) and must not be read as an ownership signal anywhere in live authorization. | Removed from both the entry gate and the visibility computation — see P1-4. |

---

## P0

### P0-1 — SEC-01: `UpdateAccountRole` privilege escalation

**File:** `backend/PEMS.Application/Accounts/Commands/UpdateAccountRole/UpdateAccountRoleCommandHandler.cs`,
`backend/PEMS.Application/Accounts/Common/AccountProvisioningRules.cs`.

Admin is already blocked explicitly (line ~72). `isStaffLeaderCaller` gets a strict allowlist. Everyone else
that falls through — including HO, DEPARTMENT_LEAD, DEPARTMENT, STUDENT, plain STAFF — reaches
`AccountProvisioningRules.ResolveAsync(privileged:false)`, whose `default` case accepts
`ADMIN`/`HO`/`STUDENT` roleCodes with only a campus-active check and no self-target check outside the
StaffLeader branch — a live privilege-escalation path (any authenticated caller in that fallthrough set can
promote themselves, including to ADMIN or HO).

**Fix:** add a self-escalation guard that applies to every actor, not only the StaffLeader branch — no actor
may change their own role via `UpdateAccountRole`, including StaffLeader and HO. Add this check immediately
after the existing Admin block, before any role-specific branch: `if (request.TargetUserId ==
currentUser.UserId) throw new ForbiddenException("Không thể tự thay đổi vai trò của chính mình.");`

**Tests to add:** HO targeting themselves → 403 (new — was previously allowed); StaffLeader targeting
themselves → 403 (regression, confirm still blocked); a plain STAFF/STUDENT/DEPARTMENT caller targeting
themselves with an elevated role (e.g. `ADMIN`) → 403; a plain STAFF caller targeting a DIFFERENT user with
an elevated role → 403 (confirms the underlying `default` case's over-permissiveness is not being relied on
elsewhere — if this test fails, `AccountProvisioningRules.ResolveAsync`'s `default` case itself needs a
second, narrower look, but self-escalation is the confirmed, in-scope bug for this item).

**Dependency:** none.

---

### P0-2 — SEC-05..09: legacy `/api/Departments` personnel actions

**Files:** `backend/PEMS.Api/Controllers/DepartmentsController.cs` (5 actions with no `[RoleAuthorize]`:
`searchpersonnel`, `viewpersonneldetails`, `updatedepartmentpersonnel`, `removepersonnel`,
`reassigndepartmentlead`), `backend/PEMS.Application/Departments/Queries/SearchPersonnel/SearchPersonnelQueryHandler.cs`,
`ViewPersonnelDetails/ViewPersonnelDetailsQueryHandler.cs`,
`Commands/UpdateDepartmentPersonnel/UpdateDepartmentPersonnelCommandHandler.cs`,
`Commands/RemovePersonnel/RemovePersonnelCommandHandler.cs`,
`Commands/ReassignDepartmentLead/ReassignDepartmentLeadCommandHandler.cs`. All 5 trust a client-supplied
`DepartmentId`/`UserId`/`NewLeaderUserId` with no scope check — confirmed live IDOR (any authenticated
caller who can reach these routes can read/mutate personnel of a department they have no relationship to).

**Actor model (verified, not guessed):** `DepartmentLeaderController`'s own doc comment states it was built
specifically to replace this IDOR-vulnerable legacy screen; `frontend/pems-react/src/App.tsx` (~line 230-241)
redirects `DEPARTMENT_LEAD` away from the legacy `DepartmentDetailDashboard` to `/dashboard/my-department`
with an explicit comment that the legacy screen's id is client-supplied; `DepartmentDetailDashboard.tsx`'s
`canEditMember` (line 52) is `isDeptLeader || isStaffLeader || isHO`;
`dashboardRouteAccess.ts` grants the legacy route to `STAFF_LEADER`/`DEPARTMENT_LEAD`/`DEPARTMENT`. Net: the
legacy screen is still a legitimate (if superseded) surface for StaffLeader/DepartmentLead/HO — not
StaffLeader-only.

**SEC-05..08 (personnel read/write) — fix:** introduce `DepartmentPersonnelManagementScope` using
`IRoleAccessPolicy.CanAccessDepartmentManagement` as the coarse gate, then `EnsureDepartmentInScope(currentUser,
departmentId)`: HO → global; StaffLeader → `department.CampusId == user.PrimaryCampusId`; DepartmentLead →
`department.DepartmentId == user.DepartmentId` **and** re-verified as the department's actual current
`HeadUserId` (mirrors `DepartmentLeaderPersonnelScopeService.EnsureCurrentUserIsActualDepartmentLeaderAsync`'s
already-established pattern of never trusting a stale JWT claim for this). Add `[RoleAuthorize]` to all 5
controller actions as a coarse pre-filter, with the handler-level scope check as the authoritative layer.

**SEC-09 (leadership reassignment) — `ReassignDepartmentLeadCommandHandler.cs`, plus a new shared
`IDepartmentLeadershipTransferService`.** Authorization alone is not sufficient here: the legacy handler's
actual transfer LOGIC has real gaps versus the canonical, fully-correct
`TransferDepartmentLeadershipCommandHandler` (candidate must be `DEPARTMENT`+`STAFF`, `ACTIVE`, correct
department+campus; atomic 3-way write; audit log; revoke both sessions; notify both parties) — those gaps
must close too, not just the IDOR.

**Concurrency contract (the part that took the most review rounds to get right):**

1. **Pre-read (outside any lock, best-effort — used only to know who to lock, never trusted for
   authorization):** `expectedCurrentLeaderUserId = department.HeadUserId`, read fresh.
   - Self-service: already known from `EnsureCurrentUserIsActualDepartmentLeaderAsync`'s own pre-check
     (`scope.ActorUserId`) — passed straight through, no second read.
   - Third-party: `ReassignDepartmentLeadCommandHandler` already loads `department` for its own
     `EnsureDepartmentInScopeForReassignment` check — reuse that row's `HeadUserId`, no extra query.
2. **Lock the expected-old and new leader together, in ONE `LockUsersAsync` call (ascending), THEN lock the
   department** — preserves the "users before departments, everywhere" invariant the codebase depends on to
   avoid cross-flow deadlock. If `expectedCurrentLeaderUserId == newLeaderUserId`, lock just the one id.
3. **Re-read the department UNDER LOCK and ALWAYS compare `department.HeadUserId ==
   expectedCurrentLeaderUserId` — unconditionally, for BOTH callers, never gated by a flag.** Mismatch → 409
   `ConflictException` ("Trưởng phòng của phòng ban này vừa thay đổi. Vui lòng tải lại trang và thử lại.",
   `LeadershipAlreadyChanged`). Without this, a third-party caller could demote the wrong account if the seat
   changed between its pre-read and its lock — a correctness bug independent of authorization, because a
   third-party caller never knows the current head in advance and must not mutate whichever account it
   *guesses* is the old leader without having actually locked that exact account first.

Only self-service layers one ADDITIONAL check on top, using the same already-locked, already-re-read
`department` row (no new query): the actor must literally **be** that head, not merely be authorized to
oversee whoever the head happens to be.

```csharp
public interface IDepartmentLeadershipTransferService
{
    Task<DepartmentLeadershipTransferResult> TransferAsync(
        ulong departmentId,
        ulong expectedCurrentLeaderUserId,   // caller's pre-lock read — see step 1
        ulong newLeaderUserId,
        ulong actorUserId,
        bool actorMustBeCurrentLeader,       // true: self-service only. false: StaffLeader/HO third-party.
        CancellationToken ct);
}
```

```csharp
// Inside TransferAsync — sketch, exact code finalized at implementation time:
if (expectedCurrentLeaderUserId == newLeaderUserId)
    throw new BusinessRuleException("Người này đã là Trưởng phòng của phòng ban này.", LeaderCandidateInvalid);

await using var transaction = await _db.BeginTransactionAsync(ct);
try
{
    var usersToLock = new[] { expectedCurrentLeaderUserId, newLeaderUserId }.Distinct().ToArray();
    await _lockService.LockUsersAsync(usersToLock, ct);          // step 2
    await _lockService.LockDepartmentsAsync(new[] { departmentId }, ct);

    var department = await _db.Departments.FirstAsync(d => d.DepartmentId == departmentId, ct);  // step 3
    if (department.HeadUserId != expectedCurrentLeaderUserId)
        throw new ConflictException("Trưởng phòng của phòng ban này vừa thay đổi. Vui lòng tải lại trang và thử lại.", LeadershipAlreadyChanged);
    if (actorMustBeCurrentLeader && department.HeadUserId != actorUserId)
        throw new ForbiddenException();

    var outgoing = await _db.Users.Include(u => u.Role).FirstAsync(u => u.UserId == expectedCurrentLeaderUserId, ct);
    var incoming = await _db.Users.Include(u => u.Role).FirstOrDefaultAsync(u => u.UserId == newLeaderUserId, ct);
    EnsureUsableSuccessor(incoming, department.DepartmentId, department.CampusId);  // unchanged from canonical

    // atomic 3-way write (outgoing→Staff, incoming→Leader, HeadUserId) → audit → commit
    // → revoke BOTH sessions → notify BOTH parties (unchanged from the canonical handler's own steps)
}
catch { await transaction.RollbackAsync(ct); throw; } // SEC-20's fix propagates here automatically — see P2
```

`department.HeadUserId == null` at the pre-read (no seated head) is a distinct, earlier business error — both
callers check for this before calling `TransferAsync` and refuse with a clear message ("Phòng ban chưa có
Trưởng phòng để chuyển giao") rather than passing a null `expectedCurrentLeaderUserId`.

**Handlers:**
- `TransferDepartmentLeadershipCommandHandler.Handle`: after `EnsureCurrentUserIsActualDepartmentLeaderAsync`,
  call `TransferAsync(scope.DepartmentId, scope.ActorUserId, request.NewLeaderUserId, scope.ActorUserId,
  actorMustBeCurrentLeader: true, ct)`.
- `ReassignDepartmentLeadCommandHandler.Handle`: after `DepartmentPersonnelManagementScope.Resolve` +
  `EnsureDepartmentInScopeForReassignment` (already loaded `department` — reuse `department.HeadUserId` as
  the pre-read), call `TransferAsync(request.DepartmentId, department.HeadUserId!.Value,
  request.NewLeaderUserId, currentUser.UserId!.Value, actorMustBeCurrentLeader: false, ct)`.

Controller attribute for `reassigndepartmentlead` stays `[RoleAuthorize(EffectiveRole.StaffLeader,
EffectiveRole.Ho)]` — DepartmentLead excluded from this route (self-service goes through
`/api/department-leader/transfer-leadership` instead).

**Tests to add:** all 5 SEC-05..08 actions: cross-campus StaffLeader → 403; cross-department DepartmentLead →
403; DepartmentLead who is department-record-scoped but NOT the actual `HeadUserId` → 403; HO → 200 any
department. SEC-09: seed a department where `HeadUserId = X`, then — between the service's pre-read and its
lock — have a second, concurrent transfer actually commit changing `HeadUserId` to `Y` (via a second DB
connection/context, same pattern `AssignDepartmentStaffAtomicityTests` already uses to prove locking), then
let the original call proceed → 409 `LeadershipAlreadyChanged`, for BOTH the self-service AND the
StaffLeader/HO third-party path; `department.HeadUserId == null` → clear business error before any lock is
attempted, for both callers; candidate not `DEPARTMENT`+`STAFF`/wrong department/wrong campus/not `ACTIVE` →
business error, for both callers (parity with canonical).

**Dependency:** implement the shared service first, then wire both handlers to it in the same change.

---

### P0-3 — SEC-02 / SEC-03: Account Detail / List / Search PII leak

**Files:** `backend/PEMS.Application/Accounts/Queries/ViewAccountDetails/ViewAccountDetailsQueryHandler.cs`
(`EnforceScope`'s final `else` branch, lines ~156-175, checks only `targetCampusId == PrimaryCampusId`, no
role check), `backend/PEMS.Application/Accounts/Common/AccountListQueryExecutor.cs` (`IRoleAccessPolicy
accessPolicy` already injected but unused as a gate; "other campus-scoped roles" branch, lines ~101-107,
returns full PII rows to any authenticated same-campus caller). Confirmed live IDOR: any same-campus
authenticated user (e.g. a Student) can view any other same-campus user's full PII, and list/search returns
full PII rows to the same broad set.

**Fix:** both call sites gate on `IRoleAccessPolicy.CanAccessAccountManagement(currentUser)`
(Admin/HO/StaffLeader) before falling into the campus-scoped branch — a caller who is same-campus but not in
that set is denied, not silently admitted through the campus check alone. StaffLeader stays campus-scoped
(`targetCampusId == PrimaryCampusId`); HO/Admin per their existing global rules (Admin's own separate handling
elsewhere in `ViewAccountDetailsQueryHandler` is unaffected).

**Tests to add:** Student/Department/DepartmentLead same-campus caller on `ViewAccountDetails` for another
user → 403 (was 200); same set on `AccountListQueryExecutor`/search → filtered out / 403 depending on the
existing error contract; StaffLeader same-campus → still 200 (regression); StaffLeader cross-campus → still
403 (regression); HO any campus → still 200 (regression).

**Dependency:** none. Independent, but should land with P2's SEC-04 (`ViewAccountStatisticsQueryHandler`,
same missing-gate pattern) since both reuse the identical `CanAccessAccountManagement` check.

---

### P0-4 / P0-5 — SEC-10 / SEC-11: Minutes Export and Search bypass `MinuteAccess`

**Files:** `backend/PEMS.Application/MeetingMinutes/Queries/ExportMinutes/ExportMinutesPdfQueryHandler.cs`,
`ExportMinutesExcelQueryHandler.cs` (campus-only checks, skipped entirely when `PrimaryCampusId` is null —
SEC-10), `backend/PEMS.Application/MeetingMinutes/Queries/SearchAndFilterMinutes/SearchAndFilterMinutesQueryHandler.cs`
(campus-only filter for non-HO, plus a flawed `!isHo && PrimaryCampusId==null => Forbidden` guard — SEC-11).
Both bypass the canonical `MinuteAccess` policy entirely, instead of reusing it.

**Design:** canonical `MinuteAccess.Evaluate(instance, visit, user, acceptedParticipantRole)` already returns
`(InScope, CanEdit)` correctly for single-record consumers (Detail/CreateLock/AcquireLock/Save) — the two
Export handlers must call it too, instead of their own ad-hoc campus check, and it must run BEFORE any
pagination/materialization, never after (an authorization filter applied after `Count`/`Skip`/`Take` produces
wrong page counts and can leak "this many exist" information even when individual rows are then filtered
out). For the list case (`SearchAndFilterMinutes`), add a new, EF-translatable `MinuteAccess.WhereAuthorizedFor(
IQueryable<Minute> minutes, IApplicationDbContext db, ICurrentUserService user)` that composes into `.Where()`
and reduces every component of `Evaluate`'s `InScope` (host / staffLeaderOfCampus / ho / guestSide / accepted)
to scalar-field comparisons or nullary-parameter `Any()` subqueries — both are reliably SQL-translatable via
Pomelo without needing to splice a pre-built `Expression<Func<>>` (which would require adding LINQKit,
deliberately avoided). Apply `WhereAuthorizedFor` before `Count()`/`Skip()`/`Take()` in
`SearchAndFilterMinutesQueryHandler`. **Remove** the `!isHo && PrimaryCampusId == null => Forbidden` guard
entirely — canonical `MinuteAccess` already allows registrant/operational-contact guest-side access, and
those callers may legitimately have no `PrimaryCampusId`; the guard was blocking a legitimate access path the
policy itself already handles correctly. **`MinuteAccess.WhereAuthorizedFor` must fail closed** if
unauthenticated or `UserId` is missing (`return minutes.Where(_ => false)`), not assume the caller already
checked authentication upstream.

**Admin gap — closed as part of this same change, not deferred:** `IRoleAccessPolicy.CanAccessVisitManagement`
already denies Admin codebase-wide for the whole Visit/Delegation domain; that principle now applies to
Minutes too, exactly like every other Visit-instance helper this plan touches. Add an explicit, unconditional
early-deny to **both** `MinuteAccess.Evaluate` and `MinuteAccess.WhereAuthorizedFor`, before any
Host/Participant/StaffLeader/HO branch:

```csharp
// MinuteAccess.Evaluate
public static (bool InScope, bool CanEdit) Evaluate(
    VisitRequestCampus instance, VisitRequest visit, ICurrentUserService user, string? acceptedParticipantRole)
{
    if (user.RoleCode == RoleCodes.Admin) return (false, false); // ADMIN excluded from all visit/delegation business records
    var userId = user.UserId!.Value;
    ... // rest unchanged
}

// MinuteAccess.WhereAuthorizedFor
public static IQueryable<Minute> WhereAuthorizedFor(IQueryable<Minute> minutes, IApplicationDbContext db, ICurrentUserService user)
{
    if (!user.IsAuthenticated || user.UserId is not { } uid) return minutes.Where(_ => false);
    if (user.RoleCode == RoleCodes.Admin) return minutes.Where(_ => false); // same principle, same place in the check order
    if (user.RoleCode == RoleCodes.Ho) return minutes;
    ... // rest unchanged
}
```

**Tests to add:** Visitor/unrelated-Staff-same-campus (no relationship) on Detail/Export/Search → 403 (the
actual IDOR regression test — NOT HO, since canonical `MinuteAccess` explicitly grants HO unconditional
access and that must stay true); registrant/operational-contact with no `PrimaryCampusId` → still 200 (proves
the removed guard is not silently re-blocking a legitimate path); `SearchAndFilterMinutes` page/TotalCount
correctness with authorization applied pre-pagination (a filtered caller's page reflects only their
authorized rows, not a raw-then-filtered mismatch); Admin who IS the historical `CurrentHostUserId` of a
visit instance → 403 on Detail/Export/Search, all previously 200; Admin who holds an ACCEPTED
`VisitParticipants` row on an instance → same, 403; HO with no relationship to the visit, any campus → still
200 (regression — HO's unconditional access must be completely unaffected by the new Admin check).

**Dependency:** none new.

---

### P0-6 — SEC-14: Visit Photo upload — exact relationship only

**Files:** `backend/PEMS.Application/Delegations/VisitPhotos/VisitPhotoStudentScope.cs` (`isStaffOrAdmin`
bypass, line ~64), `docs/database/scripts/patches/2026-07-22_allow_staff_host_visit_photos_trigger.sql`
(`trg_visit_photos_validate_bi`).

**Business rule (chốt, non-negotiable):** Photo Upload = Host OR **ACCEPTED-status** Participant of that
exact Visit instance. `ASSIGNED` participants have not yet accepted and must NOT be allowed to upload —
confirmed by cross-referencing `GetVisitInstanceMinutesQueryHandler.cs`'s own "accepted" definition
(`Status==Accepted && !IsHost`), and explicitly chosen over the Visit-Media family's looser "Accepted OR
Assigned" precedent, which does not apply here.

**Fix (C#):** `VisitPhotoStudentScope` — remove the `isStaffOrAdmin` bypass entirely; add an explicit,
unconditional Admin early-deny before any relationship check (consistent with every other Visit-domain helper
this plan touches — Admin must never pass via a historical Host/Participant relationship either); allow
exactly: `instance.CurrentHostUserId == userId` OR an `ACCEPTED`-status `VisitParticipants` row for
`(visitInstanceId, userId)` — `ASSIGNED` excluded.

**Fix (DB trigger):** update `trg_visit_photos_validate_bi` to match byte-for-byte — remove `OR
r.role_code IN ('ADMIN', 'STAFF')` and narrow the `LEFT JOIN visit_participants` condition from `(vp.status =
'ACCEPTED' OR vp.status = 'ASSIGNED')` to `vp.status = 'ACCEPTED'` only. This keeps the trigger and the C#
check in lock-step — a caller the app approves but the trigger rejects (or vice versa) otherwise surfaces as
either a raw SQL `SIGNAL 45000` (500) instead of a clean 403, or a silent over-permission at the DB layer that
the app-layer fix alone wouldn't close.

**Tests to add:** ACCEPTED participant → 200 (regression); ASSIGNED (not yet accepted) participant → 403
(new — previously 200); Host → 200 (regression); STAFF with no Host/ACCEPTED relationship → 403 (new —
previously 200 via the removed bypass); Admin with no Host/ACCEPTED relationship → 403 (new); Admin who IS
historically the `CurrentHostUserId` → 403 (new — explicit early-deny, consistent with the Admin-exclusion
principle applied everywhere else in this plan). DB-trigger-level test (direct INSERT via the integration
test harness, bypassing the C# layer) for the same ASSIGNED-excluded and Admin/STAFF-bypass-removed cases, to
prove the trigger and the app layer agree.

**Dependency:** none.

---

## P1

### P1-1 — SEC-16: Face Scan

**File:** `backend/PEMS.Application/Delegations/VisitPhotos/FaceScans/Common/VisitPhotoFaceScanAccess.cs`.
`ResolveStaffAsync` is currently a pure pass-through to the broad `VisitInstanceMediaAccessScope.ResolveAsync`
— not actually "Staff/Staff Leader AND Host" as the business rule requires.

**Business rule (chốt):** Face Scan = Staff or Staff Leader role AND must be the Host of that exact Visit.

**Fix:** use `EffectiveRole.Resolve(user.RoleCode, user.SubRole)` in a fail-closed try/catch (an unresolvable
role/subrole combination — including null/invalid subrole — must DENY, never 500 or accidentally allow, per
the established `RoleAccessPolicy.TryResolve` idiom) to check the effective role is exactly `Staff` or
`StaffLeader`, AND `instance.CurrentHostUserId == userId`. Both conditions required — role alone or Host
status alone is insufficient.

**Tests to add:** Staff who is Host → 200 (regression); StaffLeader who is Host → 200 (regression); Staff who
is NOT Host → 403 (new — previously may have passed via the broad scope); a caller with an unresolvable
role/subrole combination who happens to also be the recorded Host → 403 (new — proves fail-closed, not a
crash or accidental allow); Admin who is Host → 403 (role check excludes Admin regardless of Host status —
this is the case that most clearly demonstrates the fix isn't just "add a Host check" but "Staff/StaffLeader
role AND Host," since the old broad scope may have allowed Admin-as-Host through).

**Dependency:** none.

---

### P1-2 — SEC-17: Visit Document upload must be Host-only

**File:** `backend/PEMS.Application/Delegations/VisitDocuments/Commands/UploadVisitDocument/UploadVisitDocumentCommandHandler.cs`
(line ~55 calls the shared, broad `VisitInstanceMediaAccessScope.ResolveAsync`).

**Business rule (chốt):** Visit Document Upload = Host of that exact Visit only — no participant, no Staff
Leader, no Admin exception.

**Fix:** introduce a new, narrow `VisitDocumentAccess.ResolveUploadAsync(db, user, visitInstanceId, ct)` that
does NOT reuse the broad `VisitInstanceMediaAccessScope` — it checks only `instance.CurrentHostUserId ==
userId`, with an explicit Admin early-deny before that check (consistent with the plan's Admin-exclusion
principle). Wire `UploadVisitDocumentCommandHandler` to this new helper instead of the shared broad scope.

**Tests to add:** Host → 200 (regression); ACCEPTED participant (not Host) → 403 (new — previously may have
passed via the broad scope); StaffLeader of the campus (not Host) → 403 (new); Admin (not Host, including
Admin-as-historical-Host — should still be denied per the explicit Admin early-deny) → 403 (new).

**Dependency:** none.

---

### P1-3 — SEC-13: Assign Department Staff

**File:** `backend/PEMS.Application/Delegations/Commands/AssignDepartmentStaff/AssignDepartmentStaffCommandHandler.cs`.
Missing an ownership check on `leaderParticipant.UserId == currentUser.UserId` — present in the sibling
`VisitInvitationResponse.ApplyAsync` pattern this handler should mirror, but absent here, meaning a caller
could assign department staff on behalf of a leader participant relationship they don't actually hold.

**Fix:** add the missing `leaderParticipant.UserId == currentUser.UserId` check, mirroring
`VisitInvitationResponse.ApplyAsync`'s existing pattern exactly (same error type/message shape, for
consistency with that established convention).

**Tests to add:** caller who is NOT the department's leader participant on this instance → 403 (new); the
actual leader participant → 200 (regression); parity test comparing the error shape against
`VisitInvitationResponse.ApplyAsync`'s equivalent rejection, to confirm the mirrored pattern is followed
exactly, not reinvented.

**Dependency:** none.

---

### P1-4 — SEC-12: Multi-campus sibling Visit data leak

**File:** `backend/PEMS.Application/Delegations/Queries/GetSubmittedVisitRequestFormDetailQueryHandler.cs`.

**(1) `CreatedBy` is not an ownership signal — `RegistrantUserId` is the only request-level owner.** The
create service (`VisitRequestV2CreateService.cs`) happens to set both fields to the same value at creation
time, but that is a creation-time coincidence, not a declared business relationship, and must not be read as
one anywhere in live authorization.
- `ownsFullRequest = isRegistrant;` — `CreatedBy` dropped entirely from the visibility computation.
- The entry gate's own `isVisitor` "owns" check (currently `isRegistrant ||
  VisitRequestOwnership.IsOperationalContactOfAny(visitRequest, userId) || visitRequest.CreatedBy ==
  userId`) has its `CreatedBy` clause removed too — leaving it in the entry gate while removing it from
  `ownsFullRequest` would re-open a crash (a `CreatedBy`-only caller would still be *admitted*, then fall
  through to an empty visible set downstream). Removing it from both closes the crash risk **and** correctly
  denies a caller who was never actually the registrant.
- **Fail-closed data-quality note, not an authorization-code fix:** a legacy row where `CreatedBy` is set but
  `RegistrantUserId` is null or different is a data-integrity question, not something the authorization layer
  should resolve by trusting `CreatedBy` — such a caller must simply be denied (`ForbiddenException`, same as
  anyone else with no relationship) until/unless a separate, explicit data audit confirms the row's true
  registrant and backfills `RegistrantUserId`. Recommend a one-off SQL audit query (`SELECT ... FROM
  visit_requests WHERE created_by IS NOT NULL AND (registrant_user_id IS NULL OR registrant_user_id !=
  created_by)`) run against production data as a follow-up, outside this authorization fix's scope.

**(2) Visible-instance computation: a union of independently-computed relationship sets for the
*non-jurisdictional* relationship types, plus a separate, exclusive Staff Leader branch — instead of the
nested-ternary/if-else-chain the original code built on.** The chain's fundamental problem: it picks exactly
ONE branch per caller, so a caller who genuinely holds more than one relationship simultaneously (e.g. an
accepted participant who is ALSO the confirmed operational contact of a different campus) has their access
decided by whichever branch happens to come first — silently dropping the others. A union has no such
priority-order hazard for those relationship types.

**Staff Leader is deliberately NOT part of that union.** A Staff Leader's authority is campus jurisdiction
(`PrimaryCampusId`), a structural/organizational scope — not a personal relationship like Host or Participant.
Unioning it in would mean a Staff Leader who separately happens to be recorded as Host or Participant on a
different campus's instance gets that other campus pulled into view too, which is exactly the scope-widening
this design rules out: a Staff Leader who is not the registrant sees Campus A instances *because they are
Staff Leader of Campus A*, full stop — never "Campus A, plus wherever else I personally show up." So Staff
Leader is its own exclusive branch, evaluated after `isHo || isRegistrant` but before the general union, and
a Staff Leader with no instance on their own campus is refused (403) rather than falling through to whatever
the union would have found for them under a different hat.

**This narrowing only applies when the Staff Leader is not the registrant.** The `isHo || isRegistrant`
branch is checked FIRST and still wins whenever it applies — a Staff Leader who is also the registrant of a
multi-campus request sees the whole request through the registrant (request-level owner) relationship, same
as any other registrant; the campus-jurisdiction narrowing in the `isStaffLeader` branch below only ever
executes for a Staff Leader who is *not* the registrant of this particular request.

```csharp
List<VisitRequestCampus> visibleInstances;
if (isHo || isRegistrant)
{
    visibleInstances = visitRequest.CampusInstances.ToList(); // the only two "sees everything" cases
}
else if (isStaffLeader)
{
    // Exclusive branch: campus jurisdiction only, never widened by a separately-held Host/
    // Participant/Operational-Contact relationship on another campus.
    visibleInstances = primaryCampusId.HasValue
        ? visitRequest.CampusInstances.Where(c => c.CampusId == primaryCampusId.Value).ToList()
        : new List<VisitRequestCampus>();

    if (visibleInstances.Count == 0)
        throw new ForbiddenException("Bạn không có quyền xem chi tiết đơn này."); // no instance of this request falls under this Staff Leader's campus
}
else
{
    var unionIds = new HashSet<ulong>();

    unionIds.UnionWith(visitRequest.CampusInstances // Host
        .Where(c => c.CurrentHostUserId == userId).Select(c => c.VisitInstanceId));

    if (isDepartmentStaff)
        unionIds.UnionWith(departmentStaffAssignedInstanceIds);

    unionIds.UnionWith(visitRequest.CampusInstances // Operational Contact
        .Where(c => c.OperationalContactUserId == userId).Select(c => c.VisitInstanceId));

    unionIds.UnionWith(participantInstanceIds); // Participant — materialized from the existing isParticipant query

    if (unionIds.Count == 0)
        throw new ForbiddenException("Bạn không có quyền xem chi tiết đơn này."); // defensive fail-closed net;
            // the entry gate above should already have refused a caller with zero relationships, but this
            // guards against the two checks ever drifting apart in a future edit

    visibleInstances = visitRequest.CampusInstances.Where(c => unionIds.Contains(c.VisitInstanceId)).ToList();
}
```

The existing entry-gate if/elseif chain (`isHo`/`isStaffLeader`/`isVisitor`/`isHost`/`isRegistrant`/
`isDepartmentStaff`/`isParticipant`, each with its own specific error message) is **left in place unchanged**
except for the `CreatedBy` removal in (1) above — it still serves a distinct, useful purpose (an informative,
role-specific refusal message before any instance-level computation runs) and is now effectively a coarse
pre-check the Staff Leader branch and the union computation's own fail-closed guards back up, not a redundant
piece to delete. `hostedInstances`/`departmentStaffAssignedInstanceIds`/`participantInstanceIds` continue to
be computed the same way they already are earlier in the method — only how they're *combined* changes.

**(3) `HasMixedCampusDetails` scoped-to-visible fix.** The current check reads the request-wide
`visitRequest.HasMixedCampusDetails` flag, which leaks/blocks based on hidden sibling campuses a partial
viewer (e.g. a Staff Leader confined to their own campus) should never see the existence of. Fix: compute
mixedness from `content`/`visibleIds` (already resolved via `ResolveCampusFormContentAsync`, itself already
scoped to `visibleIds`) using an adapter from the read-side `VisitCampusFormContent` DTO to the shape
`VisitRequestV2Canonical.ComputeHasMixed(IList<CampusVisitFormDto>)` expects — field-by-field compatibility
to be finalized at implementation time — so mixedness is judged only across what the caller can actually see,
never the full request.

**Tests to add** (`tests/PEMS.UnitTests/Delegations/GetSubmittedVisitRequestFormDetail/`):
- Operational-contact-only of Campus A → only Campus A visible (regression).
- Participant-only of Campus A → only Campus A visible (regression).
- Registrant who ALSO holds a `VisitParticipants` row on one instance → full request visible (regression, now
  naturally correct since registrant is a separate "sees everything" branch, not part of the union).
- Participant + Operational-Contact-of-a-different-campus held simultaneously → BOTH campuses visible (new —
  the multi-relationship union gap the redesign closes, for the relationship types that ARE unioned).
- A Staff Leader of Campus A who is ALSO the recorded Host of one instance on Campus B, but is NOT the
  registrant → only Campus A visible, Campus B stays hidden (Staff Leader is an exclusive
  campus-jurisdiction branch, never widened by a separately-held Host/Participant/Operational-Contact
  relationship elsewhere).
- A Staff Leader whose own campus has no instance on this particular request, NOT the registrant → 403, even
  though they may hold an unrelated Participant/Host row elsewhere on the same request (the Staff Leader
  branch does not fall through to the union).
- A Staff Leader of Campus A who IS the registrant of a request spanning Campus A + Campus B → BOTH Campus A
  and Campus B visible (`isRegistrant` is checked in the `isHo || isRegistrant` branch BEFORE the Staff
  Leader branch is ever reached; being the request-level owner is not diminished by also holding a narrower
  campus-jurisdiction role).
- A Staff Leader of Campus A, NOT the registrant, on a request spanning Campus A + Campus B → only Campus A
  visible (companion case to the one above — same request, same Staff Leader campus, but this time someone
  else registered it, so the exclusive campus-jurisdiction branch applies instead of the sees-everything
  branch).
- A Staff Leader of Campus A, NOT the registrant, on a request that only has a Campus B instance (no Campus A
  instance at all) → 403 (companion case — confirms the Staff Leader branch's empty-result guard, not just
  "some campus visible" but specifically zero for a request that never touches their campus).
- A caller with `CreatedBy == userId` but `RegistrantUserId` pointing at someone else (or null) → 403, entry
  gate refuses them outright, no crash.
- A caller with genuinely zero relationships who somehow reaches the visibility computation (simulated
  directly, bypassing the entry gate, to exercise the defensive empty-union guard) → `ForbiddenException`,
  not an empty 200 response.
- Staff-Leader-own-campus-only viewer on a request whose hidden sibling campus has different content, own
  campus uniform → 200 with own content, not a mixed-content 409.

**Dependency:** none. Independent of other P1 items.

---

### P1-5 — SEC-18: `VisitLinkSupport` — explicit Admin deny

**File:** `backend/PEMS.Application/Partners/VisitLinks/Common/VisitLinkSupport.cs` (line ~31 — `effective ==
EffectiveRole.Admin` hardcoded allow in `LoadInstanceWithAccessAsync`).

**Fix:** remove the `effective == EffectiveRole.Admin` allow clause. Add an explicit, unconditional early-deny
for Admin BEFORE any Host/Participant/StaffLeader relationship check — not merely removing Admin from the
allow-list, since Admin could otherwise still pass via an unrelated branch (e.g. being a historical
`CurrentHostUserId` or holding a `VisitParticipants` row): `if (effective == EffectiveRole.Admin) throw new
ForbiddenException(...)`. Public Partner endpoint (Homepage) is untouched — it has no authentication
requirement and is not part of this internal-access helper, consistent with the chốt rule that Admin has no
internal Partner permission but still sees Public Partner on the Homepage.

**Tests to add:** Admin with no relationship → 403 (regression from the removed hardcoded allow); Admin who
is historically the `CurrentHostUserId` of the instance → 403 (new — proves the early-deny, not just the
removed allow-clause, is what's blocking); Admin who holds an ACCEPTED/ASSIGNED `VisitParticipants` row →
403 (new, same reasoning); HO → 200 (regression, unaffected); Host/StaffLeader-of-campus/ACCEPTED-or-ASSIGNED
participant → 200 (regression, unaffected). Public Partner homepage endpoint, unauthenticated → 200
(regression, confirms this fix did not touch that separate surface).

**Dependency:** none.

---

## P2

### P2-1 — SEC-04: `ViewAccountStatisticsQueryHandler` same missing-gate pattern as SEC-02/03

**File:** `backend/PEMS.Application/Accounts/Queries/ViewAccountStatistics/ViewAccountStatisticsQueryHandler.cs`.
Same fix as P0-3: gate on `IRoleAccessPolicy.CanAccessAccountManagement` before falling into any
campus-scoped branch.

**Tests to add:** same shape as P0-3's tests, applied to the statistics endpoint.

**Dependency:** land together with P0-3 (same helper, same review).

### P2-2 — Admin Visit Photo View/Delete

**File:** `backend/PEMS.Application/Delegations/VisitPhotos/VisitInstanceMediaAccessScope.cs`. Two issues:
(a) line ~48, the StaffLeader clause of `isAdminOrLeader` is missing a campus comparison entirely — confirmed
real cross-campus leak (any StaffLeader, any campus, currently passes); (b) canonical
`CanAccessVisitManagement` denies Admin codebase-wide for this whole domain, so Admin must be explicitly,
unconditionally denied here too, before any Host/Participant check — not merely removed from the allow
expression (Admin could otherwise still pass via a historical Host/Participant relationship).

**Fix:** add `user.PrimaryCampusId == instance.CampusId` to the StaffLeader clause; add an explicit Admin
early-deny before any relationship check.

**Tests to add:** StaffLeader of a DIFFERENT campus → 403 (new — closes the cross-campus leak, more severe
than originally scoped); StaffLeader of the SAME campus → 200 (regression); Admin with no relationship → 403
(regression from removed allow); Admin who is historically the `CurrentHostUserId` → 403 (new, explicit
early-deny).

### P2-3 — SEC-15 — folds into P2-2

Same file/fix as P2-2 (the doc's SEC-15 and the StaffLeader-campus-gap+Admin-deny fix above are the same
code path).

### P2-4 — SEC-19: `MarkNotificationAsRead` swallowed exceptions become generic 500

**File:** `backend/PEMS.Application/Notifications/Commands/MarkNotificationAsRead/MarkNotificationAsReadCommandHandler.cs`.
`throw new Exception(...)` and `throw new UnauthorizedAccessException()` both fall through to a generic 500 —
`ExceptionHandlingMiddleware`'s switch only maps `NotFoundException`→404, `ForbiddenException`→403, etc.;
`Exception`/`UnauthorizedAccessException` are unmatched.

**Fix:** replace both throws with the correct typed exceptions (`NotFoundException` for a missing
notification, `ForbiddenException` for the ownership check) so the middleware maps them to the correct HTTP
status instead of a generic 500.

**Tests to add:** missing notification id → 404 (was 500); notification belonging to a different user → 403
(was 500); own notification → 200 (regression).

### P2-5 — SEC-20: `ReassignDepartmentLead` raw exception message leak — subsumed by P0-2

The doc's original SEC-20 finding (raw `ex.Message` returned to the client, plus a try/catch that swallows
and returns `Success=false` with HTTP 200 instead of propagating) is entirely inside
`ReassignDepartmentLeadCommandHandler`, which P0-2 rewrites onto the shared `IDepartmentLeadershipTransferService`.
That service's `catch { await transaction.RollbackAsync(ct); throw; }` shape (P0-2) already does the correct
thing — rollback, rethrow, let `ExceptionHandlingMiddleware` produce the correct typed response — so no
separate change is needed here; this item's fix is verified as a side effect of P0-2's tests, not a standalone
task. No unexpected-exception path should return `Success=false` with HTTP 200 anywhere in this handler after
P0-2 lands — confirm this specifically in P0-2's test pass.

### P2-6 — SEC-21: `AssignTasksCommandHandler` — remove

**File:** `backend/PEMS.Application/Departments/Commands/AssignTasks/AssignTasksCommandHandler.cs` —
`throw new NotImplementedException(...)`, zero callers repo-wide (confirmed via grep), and
`tests/PEMS.ApplicationTests/Departments/AssignTasksCommandTests.cs` is a
`[Fact(Skip="Pending UC specification")]` placeholder.

**Fix:** delete the handler, its command/DTO, the controller route (if any), and the skipped placeholder
test. Re-run the repo-wide caller grep immediately before deletion (not just once earlier in this process) to
confirm nothing new started depending on it.

**Dependency:** none. Safe to do last — pure removal, no behavior change for any real caller.

---

## Verification plan (applies across all 21 items)

1. **Backend:** `dotnet test` across all three test projects (`PEMS.UnitTests`, `PEMS.ArchitectureTests`,
   `PEMS.IntegrationTests`) after each of P0, P1, and P2 — 0 failures, including every new test listed above
   for that group, before moving to the next group.
2. **DB trigger (P0-6):** re-run the trigger-level integration test directly against MySQL (ACCEPTED-only,
   role-bypass removed) — confirmed independently of the C# unit tests, since a trigger bug would not
   otherwise surface in an EF-mocked test.
3. **Frontend:** `npm run lint && npm run build` — 0 errors, run once at the end (no frontend routes/components
   in this plan should require changes, but the gate confirms nothing broke incidentally).
4. **Runtime/Postman spot-check (representative, not exhaustive):** a genuine concurrent leadership-transfer
   race (two overlapping `reassigndepartmentlead`/`transfer-leadership` calls) → clean 409, not a corrupted
   department row; Admin account historically recorded as a visit's Host → 403 on Photo/Document/PartnerLink/
   Minutes Detail/Export/Search; a multi-relationship caller who is Participant on one campus and Operational
   Contact on another → sees BOTH; a Staff Leader who also happens to be Host of a sibling-campus instance,
   not the registrant → sees ONLY their own campus; the same Staff Leader when they ARE the registrant → sees
   the whole request.
5. **Definition of Done:** walk the audit doc's own Definition-of-Done section against this plan's items once
   implementation is complete, and report PASS/FAIL per SEC-01..21 individually.

---

## Implementation order

P0-1 → P0-2 → P0-3 → P0-4/P0-5 → P0-6 → P1-1 → P1-2 → P1-3 → P1-4 → P1-5 → P2-1 → P2-2/P2-3 → P2-4 → P2-5
(verification-only) → P2-6. No business rule is to change during implementation; any ambiguity encountered
that this document does not already resolve is to be raised, not assumed.
