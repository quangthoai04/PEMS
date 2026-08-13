# PEMS — Remaining Bug Fix Plan: History Authorization UI + Feedback Deep-Link Replay

**Date:** 14/08/2026  
**Repository:** `quangthoai04/PEMS`  
**Target branch:** `Dev`  
**Verified Dev HEAD:** `8f03c4f0e3d2257035cf8819b9135c2970e634cd`

This prompt fixes the two remaining confirmed bugs discussed after the Operational Contact lifecycle plan.

> **Do not change Operational Contact lifecycle rules in this task.**
>
> This task has exactly two primary workstreams:
>
> 1. Detail page can be visible to a supporting participant while Change History is not authorized.
> 2. `feedbackVisitInstanceId` behaves like a persistent URL state instead of a one-shot command, causing the feedback modal to reopen.

---

# 1. Bug A — Supporting participant can view Detail but Change History fails with 403

## 1.1 Current user-visible problem

A Staff/participant who was invited to support a campus can open the Visit Request V2 detail page.

The page then always renders the **Change History** section.

The history component calls the history endpoint, but the history endpoint has a narrower authorization rule than detail visibility.

The backend correctly rejects the supporting participant with `403 Forbidden`.

The frontend then catches that 403 as if it were a technical loading failure and displays a message equivalent to:

```text
Unable to load change history.
Retry
```

This is incorrect UX because retrying cannot fix an intentional authorization denial.

---

# 2. Business decision for Bug A

## 2.1 Do not broaden UC-32 history permission

The fix must **not** grant full Change History access to every Staff/Student/supporting participant merely because they can view request detail.

Detail visibility and history visibility are separate permissions.

Keep the existing history authorization semantics.

The following relations/actors continue to receive history according to the existing UC-32/business rule:

- Registrant.
- Confirmed Operational Contact within its allowed scope.
- Head Office according to existing scope.
- Staff Leader according to existing campus scope.
- Current Host according to existing campus scope.

A supporting participant who is only participating in the visit does **not** gain UC-32 history access by virtue of participation.

If existing backend tests define additional legitimate history actors, preserve them. Do not remove currently valid access.

---

# 3. Root cause for Bug A

Relevant files:

```text
backend/PEMS.Application/Delegations/Services/VisitFormRead/VisitFormReadService.cs
backend/PEMS.Application/Delegations/Commands/VisitAmendments/GetVisitRequestHistoryQueryHandler.cs
backend/PEMS.Application/Delegations/Commands/VisitAmendments/GetVisitHistoryDetailQueryHandler.cs

frontend/pems-react/src/features/visit-request/components/v2/VisitRequestV2DetailView.tsx
frontend/pems-react/src/features/visit-request/components/VisitHistoryTimeline.tsx

tests/PEMS.IntegrationTests/VisitRequests/VisitRequestHistoryV2Tests.cs
tests/PEMS.IntegrationTests/VisitRequests/VisitHistoryDetailDiffV2Tests.cs
frontend/pems-react/src/features/visit-request/__tests__/VisitHistoryTimeline.test.tsx
```

Current architecture has two different questions:

```text
Can this actor view this Visit Request / campus detail?
```

and:

```text
Can this actor view UC-32 Change History?
```

Those questions intentionally have different answers.

The bug is that the V2 detail screen assumes:

```text
canViewDetail == true
=> canViewHistory == true
```

That assumption must be removed.

---

# 4. Required design for Bug A

## 4.1 Backend remains the source of truth

Do not make the frontend reconstruct history permission using role names such as:

```tsx
role === "STAFF"
subRole === "LEADER"
relation === "IC_SUPPORT"
```

The backend must tell the client whether the current viewer has Change History capability.

Use the project's existing capability/read-model convention if one already exists.

Preferred representation:

```text
VIEW_CHANGE_HISTORY
```

inside an existing capability collection.

If the current DTO does not have an appropriate capability collection, an additive boolean such as:

```text
canViewHistory
```

is acceptable.

Prefer the least invasive option that follows existing project conventions.

---

## 4.2 Capability must use the same authorization semantics as the history endpoint

Do **not** copy a simplified role matrix into `VisitFormReadService`.

The capability must answer the same effective question as `GetVisitRequestHistoryQueryHandler`:

> Would this actor have at least one history-visible campus/instance for this request?

For actors with campus-scoped history access, the endpoint may still return only the allowed campus history. That behavior remains unchanged.

Strongly prefer extracting/reusing a shared history-scope resolver/policy if necessary so these two locations cannot drift:

```text
VisitFormReadService
GetVisitRequestHistoryQueryHandler
```

The same visibility semantics should also be considered for:

```text
GetVisitHistoryDetailQueryHandler
```

Do not create one rule for timeline and another incompatible rule for history-detail drilldown.

---

# 5. Backend changes for Bug A

## 5.1 `GetVisitRequestHistoryQueryHandler.cs`

Preserve current authorization behavior.

Do not solve the bug by adding supporting participants to `visibleInstanceIds`.

If refactoring is useful, move the existing history-scope decision into a reusable component/helper and call it from this handler.

Expected behavior remains:

```text
Supporting participant only
    -> history endpoint 403

Current Host of authorized campus
    -> history endpoint succeeds for allowed scope

Registrant / other existing UC-32 actor
    -> history endpoint succeeds according to existing scope
```

---

## 5.2 `GetVisitHistoryDetailQueryHandler.cs`

Ensure history-detail access is consistent with timeline/history-list access.

If a shared history visibility resolver is introduced, use it here where appropriate.

A user who is not permitted to view history must not be able to bypass the restriction by directly calling the history-detail endpoint with an event/history ID.

Do not broaden permissions.

---

## 5.3 `VisitFormReadService.cs`

Expose history capability in the V2 detail read model.

Conceptual behavior:

```text
Supporting participant:
    detailVisible = true
    VIEW_CHANGE_HISTORY = false

Current Host:
    detailVisible = true
    VIEW_CHANGE_HISTORY = true for the request if at least one visible history scope exists

Registrant:
    VIEW_CHANGE_HISTORY = true
```

Do not make the frontend infer this from `viewer.relation`.

If using a capability collection, keep naming consistent with existing capability naming patterns.

---

# 6. Frontend changes for Bug A

## 6.1 `VisitRequestV2DetailView.tsx`

Currently the Change History section is mounted unconditionally.

Change the page so the history section is only rendered/mounted when the backend says the viewer may view history.

Expected:

```tsx
canViewHistory
  ? <VisitHistoryTimeline ... />
  : null
```

or equivalent according to the current component structure.

Preferred UX:

> If the actor has no history capability, hide the Change History section entirely.

Do not show a broken section with a Retry button.

Do not call the history API when capability is false.

This is both a UX fix and a request-noise/security-boundary fix.

---

## 6.2 `VisitHistoryTimeline.tsx`

The parent capability gate is the main fix, but this component must still be defensive.

Current behavior effectively treats all failures the same.

Change error handling so:

```text
403 Forbidden
```

is **not** rendered as a retriable technical failure.

Expected categories:

```text
403
    -> non-retry permission state or render nothing

network / 5xx / unexpected failure
    -> existing technical error state + Retry

normal empty result
    -> normal empty history UI
```

If the page hides the section before calling, 403 should be rare and represent stale capability/client state or a race. Still handle it correctly.

Do not create infinite retry behavior for authorization failures.

Use existing API error helpers/type guards if present. Do not parse arbitrary error strings when a status code is available.

---

## 6.3 i18n

If a permission-specific fallback message is rendered, add concise EN/VI keys.

Example meaning:

```text
VI: Bạn không có quyền xem lịch sử thay đổi của đơn này.
EN: You do not have permission to view this change history.
```

However, if the section is hidden for capability=false and `VisitHistoryTimeline` returns nothing on a defensive 403, new user-visible text may not be necessary.

Do not add verbose explanatory UI unless current design needs it.

---

# 7. Tests for Bug A

## 7.1 Backend integration tests

Use/update:

```text
tests/PEMS.IntegrationTests/VisitRequests/VisitRequestHistoryV2Tests.cs
tests/PEMS.IntegrationTests/VisitRequests/VisitHistoryDetailDiffV2Tests.cs
```

Add/retain these cases:

### A1 — Supporting participant can view detail but not history

Given a Staff/participant is legitimately invited/accepted/assigned to a campus and therefore can view V2 detail

When that actor calls the history endpoint

Then:

```text
detail endpoint/read model succeeds
history endpoint returns 403
history capability is false
```

This test is important because it locks the intentional mismatch as a valid business rule rather than treating it as a backend defect.

### A2 — Current Host can view scoped history

Given the actor is the current host for an authorized campus

Then:

```text
history capability = true
history endpoint succeeds
```

Preserve current campus scoping.

### A3 — Direct history detail cannot bypass permission

Given an actor has no history permission

When that actor calls the history-detail endpoint directly

Then access is refused.

### A4 — Existing legitimate actors remain valid

Preserve at least representative tests for:

```text
Registrant
Head Office / Staff Leader / confirmed Operational Contact
```

according to the current project authorization model.

Do not weaken existing tests just to make the new capability pass.

---

## 7.2 Frontend tests

Add/update tests around:

```text
VisitRequestV2DetailView
VisitHistoryTimeline
```

Required cases:

### A5 — No capability means no history request

Given detail data is returned with no `VIEW_CHANGE_HISTORY` capability

Then:

```text
Change History section is not mounted
getVisitRequestHistory is not called
no "Retry" history error is shown
```

### A6 — Capability true loads timeline

Given backend capability includes history access

Then the existing history timeline is rendered and loads normally.

### A7 — Defensive 403 is not retriable

Given `VisitHistoryTimeline` receives a 403 response

Then:

```text
generic load-failed + Retry UI is NOT shown
```

If the chosen UX renders a permission message, assert it.

### A8 — Real technical error remains retriable

Given history API returns 500/network failure

Then existing technical error UI and Retry behavior continue to work.

---

# 8. Acceptance criteria for Bug A

The History bug is fixed when all are true:

- [ ] Supporting participant can still open the V2 detail they are authorized to see.
- [ ] Supporting participant is not granted new UC-32 history permission.
- [ ] V2 detail read model exposes history capability from backend authorization.
- [ ] Frontend does not mount/call Change History when capability is false.
- [ ] 403 is not shown as a generic loading failure.
- [ ] 403 does not offer Retry.
- [ ] Current Host and other existing authorized actors still see history in the correct scope.
- [ ] Direct history-detail access cannot bypass the authorization rule.
- [ ] Backend and frontend regression tests pass.

---

# 9. Bug B — Feedback modal reopens because `feedbackVisitInstanceId` remains in URL

## 9.1 Current user-visible problem

A Visitor follows a feedback notification/deep link such as:

```text
/dashboard/visit?visitRequestId=2003&feedbackVisitInstanceId=3105
```

The Visit Request Management page reads:

```text
feedbackVisitInstanceId
```

and opens:

```text
VisitFeedbackModal
```

The user then:

```text
closes the modal
```

or:

```text
submits feedback successfully
```

The React modal state closes correctly.

However, the query parameter remains in the URL.

Later, the user:

```text
filters
changes page
changes tab
searches
or performs another action that updates searchParams
```

The effect runs again, sees the same `feedbackVisitInstanceId`, and reopens the modal.

This is a deterministic replay bug.

---

# 10. Business decision for Bug B

Treat:

```text
feedbackVisitInstanceId
```

as a **one-shot navigation intent**, not persistent page state.

Meaning:

```text
URL contains feedbackVisitInstanceId
    -> consume it once
    -> open modal
    -> remove that parameter from the URL using replace
    -> React state owns the modal lifecycle from then on
```

Closing or submitting the modal must not require the query parameter to remain present.

Keep any unrelated URL state, for example:

```text
visitRequestId
tab
keyword
page
other current filters
```

unless existing behavior intentionally changes them.

Do not remove support for the legacy/deep-link notification route.

---

# 11. Root cause for Bug B

Relevant files:

```text
frontend/pems-react/src/pages/dashboard/visit/VisitRequestManagement.tsx
frontend/pems-react/src/features/notifications/components/NotificationBellButton.tsx
frontend/pems-react/src/pages/notifications/NotificationsPage.tsx
```

Primary bug is in:

```text
VisitRequestManagement.tsx
```

The page currently reads the query parameter in an effect and sets local modal state, but does not consume the parameter.

Its URL update helper clones current query parameters, so the action parameter can also be carried into:

```text
filter changes
pagination
tab changes
search changes
```

---

# 12. Required frontend changes for Bug B

## 12.1 `VisitRequestManagement.tsx` — consume the command

When `feedbackVisitInstanceId` is present:

1. Parse it.
2. If it is a valid positive instance ID:
   - set `feedbackModalInstanceId`;
   - immediately remove only `feedbackVisitInstanceId` from the query string;
   - call `setSearchParams(next, { replace: true })`.
3. If it is invalid:
   - do not open the modal;
   - remove the invalid parameter from the URL with `replace` so it cannot pollute later navigation.

Conceptual implementation:

```tsx
useEffect(() => {
  const raw = searchParams.get('feedbackVisitInstanceId');
  if (raw == null) return;

  const instanceId = Number(raw);
  const next = new URLSearchParams(searchParams);
  next.delete('feedbackVisitInstanceId');

  if (Number.isFinite(instanceId) && instanceId > 0) {
    setFeedbackModalInstanceId(instanceId);
  }

  setSearchParams(next, { replace: true });
}, [searchParams, setSearchParams]);
```

Adapt to the project's coding style.

The effect will run again after the replace, but since the parameter is now absent it must exit without reopening.

---

## 12.2 Harden `updateUrlParams()`

The one-shot consume effect is the primary fix.

Also prevent URL helper logic from accidentally propagating this command parameter.

When cloning current params inside the helper, ensure:

```tsx
next.delete('feedbackVisitInstanceId');
```

before applying page/filter/tab changes.

This is defense in depth against timing/race/future refactoring.

Do not indiscriminately clear all query parameters.

---

## 12.3 Modal close and submit

Keep the modal controlled by React state.

Expected:

```text
onClose
    -> set feedbackModalInstanceId to null

onSubmitted
    -> refresh/update any necessary list state
    -> close modal
```

Do not re-add `feedbackVisitInstanceId` on close or submit.

No extra URL cleanup should be required at close if the parameter was consumed on open.

---

## 12.4 Notification link creation

Audit:

```text
NotificationBellButton.tsx
```

but do not remove the legacy deep-link behavior solely for this bug.

Links containing:

```text
feedbackVisitInstanceId
```

may continue to be generated because they remain valid entry points.

The destination page now consumes the intent safely.

If there are modern notification paths that already open `VisitFeedbackModal` via local state, preserve them.

Do not perform a broad notification architecture rewrite in this bug-fix task.

---

## 12.5 Notifications page

Audit:

```text
NotificationsPage.tsx
```

to make sure its direct/local-state feedback modal flow does not depend on the URL parameter remaining present.

Do not rewrite it if it already behaves correctly.

---

# 13. Feedback stale-link behavior

A valid-looking deep link may target an instance whose required feedback has already been submitted.

That condition is separate from the replay bug.

Preserve the existing modal logic that determines whether feedback is already complete.

However, even for a stale link:

```text
feedbackVisitInstanceId
```

must still be consumed only once.

Expected:

```text
open from stale deep link
    -> page consumes query parameter
    -> modal/service resolves already-submitted state
    -> closing it does not cause a replay loop
```

Do not attempt to solve stale feedback by leaving the trigger parameter in the URL.

---

# 14. Tests for Bug B

Locate/add tests for `VisitRequestManagement`.

If no focused file exists, create one following the existing frontend test conventions.

Required scenarios:

### B1 — Deep link opens once

Initial URL:

```text
/dashboard/visit?visitRequestId=2003&feedbackVisitInstanceId=3105
```

Then:

```text
modal opens for instance 3105
feedbackVisitInstanceId is removed with replace
visitRequestId remains
```

### B2 — Close does not reopen

Given the modal was opened from the deep link

When the user closes it

Then it remains closed.

When another search-param change occurs afterward, such as:

```text
page=2
keyword=ha
tab=all
```

the modal must not reopen.

### B3 — Submit does not reopen

Given the modal was opened from the deep link

When feedback submission succeeds and the modal closes

Then changing filter/page/tab must not reopen it.

### B4 — Existing URL state is preserved

Initial URL contains:

```text
visitRequestId=2003
feedbackVisitInstanceId=3105
tab=all
keyword=ha
page=2
```

After consuming feedback intent:

```text
visitRequestId=2003
tab=all
keyword=ha
page=2
```

must remain.

Only:

```text
feedbackVisitInstanceId
```

is removed.

### B5 — Invalid parameter is sanitized

Examples:

```text
feedbackVisitInstanceId=abc
feedbackVisitInstanceId=0
feedbackVisitInstanceId=-1
```

Expected:

```text
modal does not open
parameter is removed
other query params remain
```

### B6 — Browser navigation does not replay consumed intent

Because the consume operation uses:

```text
replace: true
```

the original trigger entry should not remain as a history entry that immediately replays when navigating around the same page.

Test with the router/test harness where practical.

At minimum prove filter/page navigation and returning to prior page state do not reconstruct the action parameter.

### B7 — Modern local-state notification flow still works

If existing tests cover direct feedback modal opening without query params, ensure they continue to pass.

Do not make all feedback notifications depend on the query string.

---

# 15. Acceptance criteria for Bug B

The feedback bug is fixed when:

- [ ] A valid deep link opens the correct feedback modal once.
- [ ] `feedbackVisitInstanceId` is removed immediately with URL replace.
- [ ] Other query parameters are preserved.
- [ ] Closing the modal does not cause it to reopen.
- [ ] Successful submission does not cause it to reopen.
- [ ] Filter, pagination, search and tab changes do not replay the modal.
- [ ] Invalid parameter values do not open a modal and are cleaned from the URL.
- [ ] A stale/already-submitted feedback link cannot enter a reopen loop.
- [ ] Existing direct/local-state feedback flows remain working.
- [ ] Frontend regression tests pass.

---

# 16. Scope guard — do not over-fix

This task should remain focused.

## Do not change History business authorization

Do not add:

```text
Supporting Staff
Student participant
Department support participant
IC support participant
```

to UC-32 merely to remove a frontend error.

If a separate participation-scoped activity feed is wanted later, that is a new feature/use case.

## Do not replace all notification navigation

Do not rewrite the entire notification system in this task.

Fix the one-shot semantics at the destination and preserve existing compatible routes.

## Do not change Operational Contact

The Operational Contact lifecycle fix is a separate task/plan.

Do not modify its guards, transfer timing, accept/resend rules, or tests as part of these two bugs.

---

# 17. Suggested implementation order

1. Verify current branch/head:

```bash
git checkout Dev
git pull --ff-only
git rev-parse HEAD
```

The reviewed baseline is:

```text
8f03c4f0e3d2257035cf8819b9135c2970e634cd
```

If HEAD changed, re-read all affected files before editing.

2. Fix History backend capability:
   - inspect existing history authorization;
   - reuse/extract shared scope resolver if needed;
   - expose `VIEW_CHANGE_HISTORY` or equivalent in detail read model.

3. Fix History frontend:
   - conditionally render/mount section;
   - distinguish 403 from technical errors.

4. Add History backend/frontend regression tests.

5. Fix `feedbackVisitInstanceId` one-shot consumption.

6. Harden `updateUrlParams()`.

7. Add feedback modal replay tests.

8. Run focused suites.

9. Run full frontend/backend regression gates appropriate for the repository.

---

# 18. Repository audit commands

Use these to find all affected code and avoid missing duplicate paths:

```bash
rg -n "VisitHistoryTimeline|GetVisitRequestHistory|GetVisitHistoryDetail" backend frontend tests

rg -n "feedbackVisitInstanceId" frontend backend tests

rg -n "setSearchParams|useSearchParams|updateUrlParams" \
  frontend/pems-react/src/pages/dashboard/visit \
  frontend/pems-react/src/features/notifications \
  frontend/pems-react/src/pages/notifications

rg -n "VIEW_CHANGE_HISTORY|canViewHistory|capabilities" \
  backend/PEMS.Application/Delegations \
  frontend/pems-react/src/features/visit-request
```

If similar one-shot action params are found, report them, but do not expand this fix unless they exhibit the same confirmed replay bug and can be fixed safely without unrelated behavior changes.

---

# 19. Validation

Backend focused tests:

```bash
dotnet test tests/PEMS.IntegrationTests/PEMS.IntegrationTests.csproj \
  --filter "FullyQualifiedName~VisitRequestHistoryV2Tests"

dotnet test tests/PEMS.IntegrationTests/PEMS.IntegrationTests.csproj \
  --filter "FullyQualifiedName~VisitHistoryDetailDiffV2Tests"
```

Also run read-model tests that cover V2 detail/capabilities if applicable.

Frontend focused tests:

```bash
cd frontend/pems-react

npm test -- VisitHistoryTimeline
npm test -- VisitRequestV2DetailView
npm test -- VisitRequestManagement
```

Use the actual repository test runner syntax if these commands differ.

Then:

```bash
npm run typecheck
npm run build
```

Run broader backend/frontend regression suites before marking complete.

---

# 20. Definition of Done

## History

- [ ] Detail visibility and history visibility are explicitly separated.
- [ ] Supporting participant detail access remains working.
- [ ] Supporting participant full Change History remains unauthorized.
- [ ] Backend detail read model exposes history capability.
- [ ] History capability uses the same effective authorization semantics as the history API.
- [ ] `VisitRequestV2DetailView` does not mount history without capability.
- [ ] 403 does not show generic Retry.
- [ ] Authorized Host/Registrant/etc. history remains working and scoped correctly.
- [ ] History-detail endpoint cannot bypass permission.
- [ ] Tests cover both allowed and denied relations.

## Feedback

- [ ] `feedbackVisitInstanceId` is treated as one-shot intent.
- [ ] Valid intent opens modal once.
- [ ] Parameter is consumed with `replace`.
- [ ] Invalid parameter is cleaned.
- [ ] Other URL state remains intact.
- [ ] Close does not replay.
- [ ] Submit does not replay.
- [ ] Filter/page/tab/search do not replay.
- [ ] Stale feedback deep link does not loop.
- [ ] Existing modern/direct feedback flow still works.
- [ ] Tests prove all above behavior.

---

# 21. Final report required from the implementing agent

When finished, return a concise implementation report containing:

1. Files changed.
2. History authorization/capability approach used.
3. Confirmation that supporting participant permission was **not broadened**.
4. Feedback one-shot URL behavior implemented.
5. Tests added/updated.
6. Focused test results.
7. Full regression/build results.
8. Any related action-query-param risks found during audit but intentionally left out of scope.
