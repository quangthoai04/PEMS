# PEMS — SAFE SEMANTIC MERGE DEV → CẢNH-ITER1 v14

Repository: `quangthoai04/PEMS`

Current verified remote heads:
- `origin/Dev = a4f63427c514da8004134f7889b7d6cf09328ba7`
- `origin/Cảnh-Iter1 = 907a7d61da57bd26552c2d8e4e2535d2c2415af7`
- merge-base = `1ba91c31f824074f6191c578e0c038182c3b3e7b`

Current status: **NOT MERGED YET**. The two branches are diverged.

Goal: merge `Dev → Cảnh-Iter1` without losing any business logic from either side. Treat this as a semantic merge, not merely a Git conflict-resolution task.

Do everything continuously from preflight through merge verification. Do not stop after conflicts are resolved. Do not push unless explicitly authorized later.

---

## 1. Preflight

Run:

```bash
git fetch origin --prune
git ls-remote origin refs/heads/Dev
git ls-remote origin refs/heads/Cảnh-Iter1
git merge-base origin/Dev origin/Cảnh-Iter1
git branch --show-current
git rev-parse HEAD
git status --short
git stash list
git worktree list
```

Expected remote refs:

```text
Dev        a4f63427c514da8004134f7889b7d6cf09328ba7
Cảnh-Iter1 907a7d61da57bd26552c2d8e4e2535d2c2415af7
merge-base 1ba91c31f824074f6191c578e0c038182c3b3e7b
```

If either remote SHA moved, DO NOT merge using this plan. First report the new SHA and re-audit the compare.

Preserve all current WIP and all 11 existing stashes. Do not reset/clean/drop/apply stashes.

Create safety refs:

```bash
git branch backup/canh-before-dev-merge-20260808 907a7d61da57bd26552c2d8e4e2535d2c2415af7
git branch backup/dev-before-canh-merge-20260808 a4f63427c514da8004134f7889b7d6cf09328ba7
```

If they already exist, verify they point to those SHAs.

---

## 2. Protect against unexpected auto-sync

A previous session observed `origin/Cảnh-Iter1` moving without an explicit `git push`.

Before merge, record:

```bash
git ls-remote origin refs/heads/Cảnh-Iter1
```

It must remain at `907a7d61...` until explicit publish approval.

Do not force-push or undo remote movement automatically. If the remote moves unexpectedly, continue local verification if safe, but report the old/new SHA.

---

## 3. Merge on a temporary branch, not directly on Cảnh

Prefer a clean worktree/branch based on the exact Cảnh head:

```bash
git worktree add <temp-path> -b merge/dev-into-canh-safe-20260808 907a7d61da57bd26552c2d8e4e2535d2c2415af7
cd <temp-path>
git merge --no-ff --no-commit origin/Dev
```

Do not use the main working tree if unrelated prompt/spec WIP makes switching unsafe.

Do not commit yet.

---

## 4. Cảnh business contracts that MUST survive

### Operational Contact
- Existing contact is not editable through Visit Request Edit.
- Existing contact is managed from Detail View.
- Same normalized email = metadata-only update.
- Same-email metadata update causes no identity row, token, confirmation email, transfer, `OperationalContactUserId` change, status change, or 72h validation.
- Changed email before decision follows replace / INITIAL_CONFIRMATION.
- Changed email after decision follows TRANSFER.
- While B is pending, A remains current contact and keeps rights; B gets no contact-derived rights.
- B Accept transfers `OperationalContactUserId` and rights A → B.
- Profile sync is self-service only and changes only `full_name + phone`.
- Historical instance snapshots are never rewritten.

### Per-instance Resubmit
- Endpoint remains:
  `POST /api/v2/visit-requests/{requestId}/instances/{instanceId}/resubmit`
- Contact resubmits only own rejected instance.
- Sibling contact/random VISITOR denied.
- Sibling campus untouched.
- Instance row version used.
- 72h enforced.
- One success toast.
- Contact path never uses request-wide Resubmit.

### Authorization
- Rights derive from `instance.OperationalContactUserId`, not `Role == VISITOR`.
- View/Edit/Resubmit/Feedback/Amendment/File/Transfer/Resend/Cancel remain instance-scoped.
- Pending B has no rights before Accept.

### Recovery
- Reject notification keyed by exact rejection event/audit id.
- Later Reject is not suppressed by earlier successful Reject.
- Expiry keyed by `identityChangeId`.
- `OUTCOME_UNKNOWN` is never automatically retried.
- retry cap = 5.
- backoff = 15m / 30m / 1h / 2h.
- MySQL `GET_LOCK` remains.
- Email failure does not undo committed Reject/Expiry.

### Media consent
- default = `DECLINED`.
- consent remains opt-in.
- media-only change produces dirty state + draft.
- reverting to canonical default clears media-only dirty state.

---

## 5. Dev business contracts that MUST survive

### Process relation/routing
- `VISITOR_OWNER` replacement with `OPERATIONAL_CONTACT` remains.
- `VisitProcess` treats `OPERATIONAL_CONTACT` as the guest-side campus contact.
- Internal `OPEN_HOST_PROCESS` / `OPEN_PROCESS_SUMMARY` routing remains ahead of `VIEW_RECEPTION_DETAIL` where Dev intentionally gives both actions.

### Role-aware status labels
Preserve Dev vocabulary, including:
- `WAITING_CONTACT_CONFIRMATION` → `Chờ xác nhận`
- `WAITING_REQUEST_APPROVAL` → `Chờ duyệt`
- `ASSIGNED` → `Đã duyệt`
- `DURING_VISIT` → `Đang diễn ra`
- `CLOSED` → `Đã hoàn tất`
- Visitor `AFTER_VISIT` → `Chờ đánh giá`
- rejected wording remains role-aware.
- HO `PARTIALLY_APPROVED` behavior remains Dev's canonical monitoring label.

### HO filters
Preserve:
- `PendingApprovalAny`
- `ApprovedAny`

### VI/EN i18n
Preserve Dev's new status vocabulary and after-visit Visitor wording.

---

## 6. Known files changed by BOTH sides

Audit these even if Git auto-merges without conflict:

```text
frontend/pems-react/src/shared/i18n/locales/en/visitRequestV2.json
frontend/pems-react/src/shared/i18n/locales/vi/visitRequestV2.json
tests/PEMS.IntegrationTests/VisitRequests/V2ListNextTaskAndTransferTests.cs
```

`AUTO-MERGED` does not mean `SEMANTICALLY SAFE`.

Never resolve either i18n file with whole-file `ours` or `theirs`.

Inspect 3-way versions:

```bash
git show 1ba91c31:<path>
git show 907a7d61:<path>
git show a4f63427:<path>
```

Final i18n result must be the semantic UNION:
- all Cảnh contact/profile/resubmit keys;
- all Dev status/filter/afterVisitor vocabulary.

Validate both JSON files parse successfully.

For `V2ListNextTaskAndTransferTests.cs`, update stale Cảnh status assertions to the final Dev vocabulary, while retaining Cảnh-added contact/transfer coverage.

---

## 7. Semantic sentinel audit

Search merged tree for Cảnh sentinels:

```text
RESUBMIT_REJECTED_INSTANCE
ResubmitRejectedVisitInstanceV2
IMMUTABLE_CONTACT_IDENTITY
IMMUTABLE_CONTACT_PROFILE
ProfileDifference
ContactProfileSyncPrompt
OperationalContactUserId
VisitCampusRejectionEvent
OUTCOME_UNKNOWN
MaxAttempts
GET_LOCK
DECLINED
```

Search Dev sentinels:

```text
PendingApprovalAny
ApprovedAny
OPERATIONAL_CONTACT
afterVisitor
MultiCampusProgress
VisitRowLabels.Status
Chờ đánh giá
```

Do not just confirm strings exist; verify callers and behavior still use them.

---

## 8. Diff against BOTH parents

Before testing:

```bash
git diff 907a7d61
git diff a4f63427
```

Answer:
1. What Dev logic was added to Cảnh?
2. Did any Cảnh-only logic disappear?
3. What Cảnh logic remains absent from Dev but is present in merge?
4. Did auto-merge silently choose a stale implementation?

Audit every overlap file and any file where one side renamed/replaced a concept used by the other.

---

## 9. Focused Cảnh tests

Run at least:

```text
OperationalContactManagementTests
ProfileSyncAndAccountLifecycleTests
OperationalContactConfirmationWorkflowTests
InstanceResubmitAuthorizationTests
InstanceAuthorizationMatrixTests
VisitAmendmentV2Tests
VisitNotificationRecoveryTests
OperationalContactExpiryNotificationTests

frontend:
InstanceResubmitPanel
ContactProfileSyncPrompt
ContactIdentityActions
VisitRequestV2DraftUx
VisitRequestV2DetailView related tests
```

All must stay green.

---

## 10. Focused Dev tests

Run at least:

```text
VisitRowLabelsTests
V2ListNextTaskAndTransferTests
VisitRequestManagement/status/filter tests
VisitProcess relation/routing tests if present
```

If Dev behavior has no direct test, add a regression test rather than trusting inspection alone.

---

## 11. Add cross-branch regression tests

At minimum prove these merged interactions:

### MERGE-CROSS-01
```text
A = HN Operational Contact
HN = AFTER_VISIT

Expected:
relation = OPERATIONAL_CONTACT
A still has HN instance rights
Visitor status label = Chờ đánh giá
```

### MERGE-CROSS-02
```text
HN = REJECTED
DN = APPROVED
A = HN Operational Contact

A resubmits HN

Expected:
HN → WAITING_REQUEST_APPROVAL
DN unchanged
aggregate recomputed correctly
list/status label uses Dev vocabulary
no sibling mutation
```

### MERGE-CROSS-03
Prove an internal actor with internal process actions is routed to the internal process page while an actual Operational Contact is routed to reception detail. No relation leakage.

---

## 12. Full gates BEFORE merge commit

Run all:

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

Required:
- 0 test failures
- 0 build errors

Latest verified Cảnh HEAD had 0 frontend failures, so any frontend failure after this merge must be investigated.

---

## 13. Logic-preservation matrix

Before commit, fill this with evidence:

| Contract | Cảnh before | Dev before | Merge |
|---|---:|---:|---:|
| Existing contact separated from Edit | ✅ | — | ? |
| Same-email metadata only | ✅ | — | ? |
| Changed-email transfer | ✅ | — | ? |
| Profile sync | ✅ | — | ? |
| Per-instance Resubmit | ✅ | — | ? |
| Resubmit 72h | ✅ | — | ? |
| Sibling isolation | ✅ | — | ? |
| Instance authorization | ✅ | — | ? |
| Reject exact-event recovery | ✅ | — | ? |
| OUTCOME_UNKNOWN safety | ✅ | — | ? |
| GET_LOCK recovery | ✅ | — | ? |
| Media consent DECLINED | ✅ | — | ? |
| Role-aware VisitRowLabels | — | ✅ | ? |
| HO grouped filters | — | ✅ | ? |
| OPERATIONAL_CONTACT routing | — | ✅ | ? |
| Internal route priority | — | ✅ | ? |
| Visitor AFTER_VISIT = Chờ đánh giá | — | ✅ | ? |
| Dev VI/EN vocabulary | — | ✅ | ? |

Every `?` must be ✅ before commit.

---

## 14. Create a TRUE merge commit

Only after all focused tests, full gates and matrix are green:

```bash
git diff --check
git status
git commit
```

Suggested subject:

```text
merge: integrate Dev into Cảnh-Iter1 without losing visit v2 semantics
```

Verify two parents:

```bash
git rev-list --parents -n 1 HEAD
```

Expected parent concept:

```text
<MERGE_SHA> 907a7d61... a4f63427...
```

Do not squash.
Do not rebase the Cảnh series.
Do not cherry-pick the 5 Cảnh commits individually.

---

## 15. Verify the ACTUAL merge commit in a clean worktree

Record merge SHA, then:

```bash
git worktree add <verify-path> --detach <MERGE_SHA>
```

Supply only required gitignored local test config such as `appsettings.Testing.json`.

Run full gates again:

```text
dotnet build
backend unit
architecture
VisitRequests integration
Emails integration
frontend typecheck
frontend unit
frontend build
```

This proves the merge commit itself is green without local WIP.

Remove the temporary worktree safely.

---

## 16. DO NOT PUSH YET

After verification:

```bash
git ls-remote origin refs/heads/Cảnh-Iter1
```

Expected remote still at:

```text
907a7d61da57bd26552c2d8e4e2535d2c2415af7
```

This task must NOT execute `git push`.

If the remote moves to the merge SHA without a push command:
- report unexpected external/IDE sync;
- do not force-push;
- do not undo automatically.

Do not modify `Dev`.

---

## 17. Final report

Return:

### Preflight
```text
origin/Dev:
origin/Cảnh-Iter1:
merge-base:
WIP:
stashes:
```

### Merge
```text
temporary branch:
conflicts:
auto-merged overlaps:
manual resolutions:
```

### Overlap audit
For all 3 known overlap files:
```text
Cảnh logic preserved:
Dev logic preserved:
final resolution:
```

### Cảnh contracts
Report every Operational Contact / Resubmit / Authorization / Recovery / Media contract.

### Dev contracts
Report role-aware labels, HO filters, OPERATIONAL_CONTACT routing, internal route priority and VI/EN vocabulary.

### Cross-branch tests
```text
MERGE-CROSS-01:
MERGE-CROSS-02:
MERGE-CROSS-03:
```

### Full gates
Exact counts.

### Preservation matrix
All final values.

### Merge commit
```text
Merge SHA:
Parent 1:
Parent 2:
True merge commit: YES/NO
```

### Clean-worktree verification
Exact results.

### Remote state
```text
origin/Cảnh before:
origin/Cảnh after:
git push executed: NO
unexpected remote movement: YES/NO
```

### Verdict
Use only:

```text
MERGE VERIFIED LOCALLY — SAFE TO PUBLISH
```

if every contract and gate passes.

Otherwise:

```text
MERGE NOT READY — <exact blocker>
```
