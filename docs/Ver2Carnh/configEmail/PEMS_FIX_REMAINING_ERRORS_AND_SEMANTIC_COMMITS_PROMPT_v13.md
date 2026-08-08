# PEMS — FIX REMAINING FAILURES + SEMANTIC COMMIT PLAN v13
## Sửa 2 lỗi frontend còn lại, chạy full gates, rồi commit theo từng nhóm thay đổi cùng ý nghĩa

> Repository: `PEMS`
>
> Current branch expected: `Canh-Iter1`
>
> Start from the CURRENT working tree.
>
> Thực hiện liên tục:
>
> `preflight → fix 2 media-consent failures → focused tests → full gates → diff audit → semantic commits → post-commit verification → final report`
>
> User đã cho phép commit LOCAL trong task này.
>
> **Không push. Không dùng `git add .` / `git add -A`.**

---

# 0. Verified baseline

Latest verified state:

```text
HEAD: 1ba91c31f824074f6191c578e0c038182c3b3e7b
WIP: 98 entries
Stashes: 11, untouched
Nothing committed
```

Gates:

```text
dotnet build                  0 errors
backend unit               2403 passed
architecture                 28 passed
VisitRequests integration   466 passed
Emails integration          854 passed
frontend typecheck           pass
frontend unit              2121 passed / 2 failed
frontend build               pass
```

Only known failures:

```text
VisitRequestV2DraftUx.test.tsx

1. closing the create modal asks before it throws typed data away
   → warns before closing when the user changed a media-consent choice alone

2. a draft is written for anything the user filled in, and for nothing they did not
   → saves a draft carrying a media-consent answer that is not the default

AssertionError: expected false to be true
```

They were reproduced on clean detached HEAD with the same 54/2 split, so they are PRE-EXISTING CONFIRMED. This task now requires FIXING them.

---

# 1. Safety

Run:

```text
git branch --show-current
git rev-parse HEAD
git status --short
git stash list
git diff --stat
```

Preserve:
- all current WIP;
- all 11 stashes;
- unrelated files.

Do not:
- reset;
- clean;
- discard;
- checkout over modified files;
- mass-fix existing warnings;
- create schema changes unnecessarily.

Do not commit until all required tests are green.

---

# 2. Fix the two media-consent failures

Required behavior:

```text
untouched/default form
→ no unnecessary draft
→ no false unsaved-change warning

media-consent-only change
→ form becomes meaningfully dirty
→ closing modal warns
→ draft is persisted
→ restored draft retains the media-consent value
```

Audit:

```text
VisitRequestV2DraftUx.test.tsx
visitRequestV2DraftStorage.ts
useVisitRequestFormV2.ts
VisitRequestFormV2.tsx
```

and every helper involved in:

```text
isDirty
hasMeaningfulData
shouldSaveDraft
draft serialization
draft normalization
draft restoration
media-consent default values
watch/register state
modal close protection
```

Trace:

```text
UI control
→ form state
→ dirty/meaningful predicate
→ draft DTO
→ storage
→ restore
```

Do not assume the cause. Investigate possibilities such as omitted field, bad default comparison, sanitizer stripping the field, or mismatch between save/restore shape.

Do not skip/delete/weaken tests.

Prefer canonical value comparison, not a permanent `wasClicked` dirty flag.

If the user changes media consent then changes it back to the canonical default, it should not remain falsely dirty if the rest of the form equals the baseline.

Add/keep coverage for:

```text
MEDIA-DRAFT-01 untouched default → no draft
MEDIA-DRAFT-02 media-only change → meaningful dirty
MEDIA-DRAFT-03 media-only change → close warning
MEDIA-DRAFT-04 media-only change → draft saved
MEDIA-DRAFT-05 draft restore → value restored
MEDIA-DRAFT-06 change then revert → no false media-only dirty state
```

Run the exact failing file first, then the relevant draft/form subset.

---

# 3. Full gates before committing

After fixing media consent, run all:

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

Target:

```text
0 test failures
0 build errors
```

If DLLs are locked by the dev server, use the already-proven redirected output inside a gitignored repo location. Do not kill unrelated processes unnecessarily.

Do not begin commits until the two media-consent failures are green.

---

# 4. Reclassify the entire diff by semantic meaning

After green gates:

```text
git status --short
git diff --name-status
git diff --stat
```

Classify every changed/untracked file:

```text
GROUP 1 — Operational Contact management + profile sync
GROUP 2 — Per-instance Resubmit backend + frontend
GROUP 3 — Instance authorization + Amendment/Feedback/File/Transfer rights/tests
GROUP 4 — Visit notification recovery + email seed + runbook
GROUP 5 — Media-consent draft/dirty-state fix
GROUP 6 — Shared test infrastructure only if genuinely cross-cutting
GROUP N — Pre-existing/unrelated WIP; DO NOT COMMIT
```

Do not group by technical layer such as "all backend" or "all tests". A semantic commit should contain backend + frontend + i18n + tests for the same business change.

Previous audit identified Group N as 12 entries, including 11 prompt/spec markdown files under `docs/Ver2Carnh/configEmail/` and `VisitV2FlashToastOnce.test.tsx`. Re-audit exact current status, then leave unrelated items untouched unless concrete dependency proves otherwise.

---

# 5. Commit 1 — Operational Contact management + profile sync

Suggested commit:

```text
feat(visit): finalize operational contact management and profile sync
```

Include only the semantic change:
- existing-contact management separated from Visit Request Edit;
- Detail contact management;
- same-email metadata update;
- changed-email identity/transfer pieces belonging to this work;
- pending-contact snapshot/name fixes;
- ProfileDifference;
- ContactProfileSyncPrompt;
- canonical self-profile sync;
- related i18n;
- directly related tests.

Do NOT include:
- per-instance Resubmit;
- recovery;
- media-consent;
- unrelated prompt/spec WIP.

Stage explicitly:

```text
git add -- <exact paths>
```

or, for mixed files:

```text
git add -p -- <path>
```

Then inspect:

```text
git diff --cached --name-status
git diff --cached --stat
git diff --cached
```

Only then commit.

---

# 6. Commit 2 — Per-instance Resubmit

Suggested:

```text
feat(visit): add instance-scoped resubmission
```

Include:
- `ResubmitRejectedVisitInstanceV2Command` + handler;
- instance resubmit service logic;
- controller route;
- allowedActions/capability wiring;
- frontend client;
- `InstanceResubmitPanel`;
- campus-detail wiring;
- related i18n;
- backend instance-resubmit tests;
- FE-RESUBMIT tests.

Ensure Operational Contact path calls:

```text
POST /v2/visit-requests/{id}/instances/{iid}/resubmit
```

and never the old request-wide endpoint.

Inspect staged diff before commit.

---

# 7. Commit 3 — Instance-scoped Operational Contact permissions

Suggested message depending on actual production diff:

```text
fix(auth): enforce operational contact instance-scoped permissions
```

or, if production guards were already committed with owning features and this is test closure only:

```text
test(auth): lock operational contact instance-scoped permissions
```

Semantic scope:
- shared authorization guards changed for this closure;
- Amendment authorization;
- Feedback authorization;
- File access authorization;
- Transfer/Resend/Cancel authorization;
- rights handover;
- authorization matrix;
- account accept/bind lifecycle tests where they primarily prove access/binding.

Do not force tests into a separate commit if they naturally belong with Commit 1 or 2. Semantic clarity is more important than a fixed commit count.

---

# 8. Commit 4 — Notification recovery + email seed

Suggested:

```text
fix(email): make visit notifications safely recoverable
```

Include:
- Reject notification keyed by exact rejection event/audit id;
- expiry recovery by identityChangeId;
- machine-readable error code persistence;
- safe retry classification;
- OUTCOME_UNKNOWN handling;
- retry cap/backoff;
- MySQL GET_LOCK concurrency;
- recovery worker/service;
- related tests;
- `VISIT_CAMPUS_REJECTED`;
- `VISIT_CONTACT_INVITATION_EXPIRED`;
- corresponding `email_contact_policies`;
- seed sync/preflight/verify files;
- `EMAIL_NOTIFICATION_RECOVERY_RUNBOOK.md`.

SQL must remain seed-only.

Before commit verify:

```text
no CREATE TABLE
no ALTER TABLE
no DROP TABLE
no ADD/DROP/MODIFY COLUMN
no new DDL/schema mutation
```

---

# 9. Commit 5 — Media-consent draft bug

Suggested:

```text
fix(visit): persist media consent changes in draft state
```

Keep this commit small.

Include only:
- production files required for dirty/draft behavior;
- `VisitRequestV2DraftUx.test.tsx`;
- directly related draft/form tests if needed.

Do not bury this fix inside the large closure commits.

---

# 10. Optional Commit 6 — shared test infrastructure

Only if shared fixtures/helpers truly support multiple semantic groups and cannot cleanly travel with one feature.

Suggested:

```text
test(visit): add shared operational contact test fixtures
```

Avoid creating this commit if helpers naturally belong to an owning feature.

---

# 11. Selective staging rules

For every commit:

```text
git status --short
```

Then only:

```text
git add -- path1 path2 ...
```

or:

```text
git add -p -- path
```

Never:

```text
git add .
git add -A
```

Prefer not to use:

```text
git commit -am
```

After staging:

```text
git diff --cached --name-status
git diff --cached --stat
git diff --cached
```

Verify:
- all staged hunks belong to one semantic purpose;
- no Group N item is staged;
- no debug code;
- no temporary bypass;
- no unrelated WIP;
- no generated artifacts.

If a file contains hunks for multiple groups, use `git add -p`.

---

# 12. After each commit

Run:

```text
git show --stat --oneline HEAD
git show --name-status --oneline HEAD
git status --short
```

Record:
- SHA;
- subject;
- files;
- semantic scope.

If an accidental unrelated file enters a local commit, repair it before proceeding. Do not push.

---

# 13. Post-commit full gates

After all semantic commits are created, run again:

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

```text
frontend unit failures = 0
all backend suites = 0 failures
build errors = 0
```

The previous two media-consent failures must be gone.

---

# 14. Verify the committed series independently

Strongly prefer a temporary detached worktree at final closure HEAD:

```text
git worktree add <temp> --detach <final-closure-HEAD>
```

Run at least:
- frontend unit;
- VisitRequests integration;
- Emails integration;

or full gates if practical.

Purpose:

```text
prove committed commits are green independently of remaining unrelated WIP
```

Remove the temporary worktree safely afterwards.

Do not touch the 11 existing stashes.

---

# 15. Final git audit

Run:

```text
git log --oneline --decorate -n 10
git status --short
git diff --stat
git diff
git stash list
```

Expected:
- closure work committed;
- only Group N/pre-existing unrelated WIP remains uncommitted;
- 11 stashes intact;
- no accidental generated files;
- no accidental prompt/spec commit;
- no accidental schema change;
- no push performed.

Do not commit `node_modules`, junctions, temp worktrees, redirected build outputs, secrets, or transient test files.

---

# 16. Do not push

This prompt authorizes LOCAL commits only.

Do NOT:
- `git push`;
- create PR;
- merge;
- rebase remote branches;
- force-push.

---

# 17. Final report

Return only after fixes + gates + semantic commits + post-commit verification.

## Preflight

```text
Branch:
Start HEAD:
Initial WIP:
Initial stashes:
```

## Media-consent root cause

```text
Root cause:
Why media-only change was not dirty:
Why draft was not persisted:
Files fixed:
```

## Media-consent validation

```text
Before:
After:
MEDIA-DRAFT tests:
```

## Full gates before commits

Exact results.

## Commit series

| # | SHA | Commit message | Semantic scope | Files |
|---|---|---|---|---|

Explain any justified deviation from the recommended grouping.

## SQL audit

```text
Templates:
Policies:
DDL:
Schema changes:
```

## Group N preservation

List exact unrelated/pre-existing WIP left uncommitted.

## Post-commit gates

Exact results.

## Clean final-HEAD verification

```text
Temporary worktree:
Suites:
Results:
```

## Final git state

```text
Final HEAD:
Number of commits created:
Remaining git status:
Stash count:
Push performed: NO
```

## Remaining errors

Target:

```text
Known test failures: 0
Build errors: 0
Unresolved business blockers: 0
```

Report warnings separately; do not call them errors.

## Known product constraint

```text
Production authentication is SSO-only.
Invitee unable to authenticate through supported SSO cannot complete Accept.
```

## Verdict

Use only:

```text
FIXED + COMMITTED LOCALLY — READY FOR REVIEW
```

if all required tests are green and semantic commit audit is clean.

Otherwise:

```text
NOT READY — <exact blocker>
```

Do not end with “say the word and I’ll continue.”
