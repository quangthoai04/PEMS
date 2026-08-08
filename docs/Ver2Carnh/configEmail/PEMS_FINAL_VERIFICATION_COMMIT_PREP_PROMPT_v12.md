# PEMS — FINAL VERIFICATION & COMMIT-PREP PROMPT v12
## Xác minh 2 điểm còn lại + audit toàn bộ diff trước khi chốt COMPLETE

> Repository: `PEMS`
>
> Branch: `Cảnh-Iter1`
>
> Tiếp tục từ CURRENT working tree.
>
> Mục tiêu: **không phát triển thêm feature mới**, chỉ đóng nốt 2 điểm verification còn thiếu và thực hiện final commit-preparation audit thật chặt.
>
> **Không commit trừ khi user yêu cầu rõ.**
>
> Không reset/clean/discard WIP. Không làm mất 11 stashes.

---

# 0. Current accepted state

Các phần sau hiện được xem là đã hoàn thành và KHÔNG được làm lại nếu không phát hiện regression thật:

```text
- Per-instance Resubmit backend
- Per-instance Resubmit frontend
- 72h on Resubmit
- Profile sync full_name + phone
- ACCOUNT-01..06
- Instance-scoped authorization
- Amendment permission
- Feedback permission
- File preview/download permission
- Transfer / Resend / Cancel
- Rights handover A → B
- Reject exact-event recovery
- Expiry recovery
- OUTCOME_UNKNOWN no-auto-retry
- Retry backoff/cap
- EXHAUSTION-01
- CONCURRENCY-01
- Recovery runbook
- Post-commit notification semantics
```

Không mở thêm business scope mới.

---

# 1. Preflight

Record:

```text
Branch
HEAD
git status --short
git diff --stat
stash count
```

Confirm:

```text
Branch = Cảnh-Iter1
Nothing committed
WIP preserved
11 stashes untouched
```

Do not reset, clean, checkout over modified files, or rewrite existing work.

---

# 2. VERIFY ISSUE #1 — Frontend unit failures must be proven pre-existing in this round

Current report:

```text
frontend unit
546 passed / 2 failed
```

Failures:

```text
VisitRequestV2DraftUx.test.tsx
media-consent cases
```

Previous reasoning that the directly named test/source files are absent from `git diff` is NOT sufficient by itself, because an imported dependency could still be modified.

You must prove the failures are pre-existing using one of these methods.

## Preferred method — clean HEAD baseline

Safely reproduce the two failing tests against clean HEAD without losing current WIP.

Requirements:

```text
1. preserve current WIP exactly
2. preserve all existing 11 stashes
3. run only the relevant failing test(s) against clean HEAD
4. record exact failure names/messages
5. restore current WIP exactly
6. verify:
   - branch unchanged
   - HEAD unchanged
   - WIP restored
   - stash count still 11
```

Do NOT permanently create/drop/reorder the user's existing stashes.

If using a temporary patch/worktree is safer than stash, prefer that.

## Acceptable alternative

If clean-HEAD execution is technically impractical, produce a complete dependency-chain proof:

```text
test file
→ all imported source files
→ all imported hooks/services/helpers
→ none intersect current diff
```

But clean HEAD proof is strongly preferred.

## Required conclusion

Only one of:

```text
A. PRE-EXISTING CONFIRMED
   same failures reproduce on clean HEAD

B. CAUSED BY CURRENT WIP
   failure disappears on clean HEAD
   → locate regression
   → fix
   → rerun frontend unit suite
```

Do not call them pre-existing based on memory.

---

# 3. VERIFY ISSUE #2 — Resolve "6 passing (FE-RESUBMIT-01…07)"

Current report is internally inconsistent:

```text
6 passing
but labels FE-RESUBMIT-01…07
```

Audit exact frontend tests.

Produce table:

| Test ID | Exists? | Actual test name | Pass? |
|---|---:|---|---:|
| FE-RESUBMIT-01 | | | |
| FE-RESUBMIT-02 | | | |
| FE-RESUBMIT-03 | | | |
| FE-RESUBMIT-04 | | | |
| FE-RESUBMIT-05 | | | |
| FE-RESUBMIT-06 | | | |
| FE-RESUBMIT-07 | | | |

Then resolve:

```text
Case A:
7 tests exist and pass
→ prior report count was typo
→ report 7/7

Case B:
only 6 tests exist
→ identify missing acceptance criterion
→ implement missing test
→ run it
→ report 7/7 if all pass

Case C:
numbering intentionally skips/combines one case
→ rename/report clearly so no misleading 01…07 range remains
```

Do not leave ambiguous reporting.

---

# 4. FINAL COMMIT-PREPARATION AUDIT — no new features

Current diff is large:

```text
~62 files
~+2556 / -429
```

Audit every changed/untracked file.

Group by feature:

```text
A. Contact management separation
B. Same-email metadata / changed-email identity
C. Contact invitation / transfer
D. Account/profile sync
E. Per-instance Resubmit backend
F. Per-instance Resubmit frontend
G. Authorization guards/tests
H. Amendment/Feedback/File/Transfer tests
I. Email recovery
J. Recovery runbook
K. Frontend i18n/UI
L. SQL seed-only changes
M. Test-only utilities
N. Unrelated/pre-existing WIP
```

For each file, record:

```text
path
group
reason changed
required for closure? YES/NO
```

Do NOT delete unrelated WIP automatically.

If a file is unrelated/pre-existing WIP:

```text
mark it as PRE-EXISTING/OTHER WIP
leave it untouched
```

The goal is separation/understanding, not destructive cleanup.

---

# 5. SQL audit

Current report says the only SQL diff is seed rows for two email templates from an earlier session.

Verify exactly:

```text
- no CREATE TABLE
- no ALTER TABLE
- no DROP TABLE
- no ADD COLUMN
- no trigger/procedure schema mutation introduced by this closure
```

Report:

```text
SQL files changed:
exact seed changes:
template codes:
why needed:
schema/DDL change = NO
```

If unexpected DDL exists, do NOT remove blindly. Show evidence and mark it.

---

# 6. Security audit

Search current diff and relevant code for accidental authorization shortcuts.

Verify NO new logic equivalent to:

```text
role == VISITOR → allow
```

for Operational Contact rights.

Confirm instance authorization still derives from:

```text
currentUser.UserId == targetInstance.OperationalContactUserId
```

or canonical shared guards resolving that relation.

Audit at least:

```text
View
Edit
Resubmit
Feedback
Amendment
Files
Transfer
Resend
Cancel
```

Also verify:

```text
pending transfer target B
→ no contact rights before Accept
```

and:

```text
after Accept
→ rights follow new OperationalContactUserId
```

---

# 7. Resubmit endpoint audit

Search all frontend/backend references to Resubmit.

Confirm:

```text
Operational Contact per-instance action
→ POST /v2/visit-requests/{id}/instances/{iid}/resubmit
```

and never:

```text
Operational Contact
→ old request-wide Resubmit endpoint
```

Registrant legacy/request-wide behavior may remain where canonical.

Verify:
- sibling state isolation;
- instance row version;
- 72h;
- one toast;
- localized errors.

---

# 8. Profile Sync audit

Verify final implementation still obeys:

```text
sync fields:
- full_name
- phone

do not sync:
- email
- organization
- jobTitle
- role
- status
```

Confirm:

```text
only current account holder
→ may execute self-profile sync
```

Verify partial update cannot blank unrelated profile fields.

Verify no historical snapshot is rewritten.

---

# 9. Recovery audit

Confirm final code still maps:

```text
Reject notification identity
→ exact rejection audit/business event id

Expiry notification identity
→ identityChangeId
```

Confirm:

```text
OUTCOME_UNKNOWN
→ no automatic resend
```

Confirm retry cap:

```text
5 attempts
```

Confirm backoff matches runbook:

```text
15m
30m
1h
2h
```

Confirm `GET_LOCK` protection remains.

No new automatic "force retry" path should bypass ambiguity classification.

---

# 10. Documentation audit

Verify:

```text
docs/Ver2Carnh/configEmail/EMAIL_NOTIFICATION_RECOVERY_RUNBOOK.md
```

matches final code.

Also search docs/comments for stale statements such as:

```text
Operational Contact is email-only
Resubmit is request-wide only
Operational Contact cannot Amendment
Operational Contact cannot access files
```

Do NOT perform broad documentation rewrite unless stale text is directly misleading for the implemented flow.

If found, update only the directly relevant documentation/comments.

---

# 11. Final focused tests

After resolving #2 and any audit fixes, run focused suites first:

```text
per-instance Resubmit frontend tests
Profile Sync tests
ACCOUNT-01..06
authorization matrix tests
Amendment tests
Feedback tests
File tests
Transfer tests
Recovery tests including:
- repeated Reject
- OUTCOME_UNKNOWN
- EXHAUSTION-01
- CONCURRENCY-01
- expiry
```

All newly introduced tests must pass.

---

# 12. Full gates

Run ALL:

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

If the two media-consent frontend failures are reproduced on clean HEAD:

```text
report them explicitly as PRE-EXISTING CONFIRMED
```

Otherwise fix the regression and rerun.

Do not stop after one suite.

---

# 13. Final diff audit

Before reporting:

```text
git status --short
git diff --stat
git diff
```

Verify:

```text
- no debug code
- no console/test diagnostics left accidentally
- no temporary auth bypass
- no hardcoded user IDs
- no TODO standing in for required implementation
- no duplicate endpoint
- no duplicate toast
- no untranslated new UI strings
- no request-wide Resubmit used by contact
- no schema change
- no OUTCOME_UNKNOWN auto retry
- no accidental account overwrite
- no historical snapshot rewrite
- no accidental commit
```

---

# 14. Do NOT do

```text
- do not develop new features
- do not ask "should I continue?"
- do not stop after verification issue #1
- do not stop after issue #2
- do not commit
- do not reset/clean/discard WIP
- do not alter the user's 11 existing stashes
- do not remove unrelated WIP
- do not create schema
- do not weaken authorization
- do not add password/local auth
- do not change SSO-only constraint
```

---

# 15. Required final report

## 1. Preflight

```text
Branch:
Start HEAD:
End HEAD:
WIP before/after:
Stashes before/after:
Nothing committed:
```

## 2. Frontend baseline proof

```text
Failing tests:
Clean HEAD result:
Current WIP result:
Conclusion:
```

If pre-existing, state exactly:

```text
PRE-EXISTING CONFIRMED
```

## 3. FE Resubmit test count correction

Provide the 01…07 table and final count.

## 4. Changed-file classification

Summarize counts by groups A–N.

List any unrelated/pre-existing WIP separately.

## 5. SQL audit

```text
SQL changed:
seed only:
DDL:
schema change:
```

## 6. Security audit

Report each:

```text
View:
Edit:
Resubmit:
Feedback:
Amendment:
Files:
Transfer:
Resend:
Cancel:
```

with instance-scoped result.

## 7. Resubmit audit

```text
frontend endpoint:
backend endpoint:
request-wide contact misuse:
row version:
72h:
toast:
```

## 8. Profile Sync audit

```text
fields:
authorization:
partial update safety:
historical snapshots:
```

## 9. Recovery audit

```text
Reject event key:
Expiry key:
OUTCOME_UNKNOWN:
cap:
backoff:
GET_LOCK:
runbook:
```

## 10. Tests/gates

Exact counts/results for all full gates.

## 11. Final diff audit

State whether any:
- debug;
- TODO;
- unsafe auth;
- schema changes;
- untranslated UI;
- duplicate endpoints;
- unrelated deletion;
remain.

## 12. Remaining constraint

Keep:

```text
Production authentication is SSO-only.
Invitee unable to authenticate through supported SSO cannot complete Accept.
```

## 13. Final verdict

Use:

```text
COMPLETE — VERIFIED
```

only if:
- the 2 frontend failures are proven pre-existing or fixed;
- FE-RESUBMIT count inconsistency is resolved;
- full gates are complete;
- final diff audit is clean.

Otherwise:

```text
COMPLETE EXCEPT: <exact verification blocker>
```

Do not report "say the word and I will continue".
