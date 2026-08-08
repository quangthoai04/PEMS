# PEMS — VERIFY EMAIL TEST COUNT BEFORE PUBLISH v15

Repository: `quangthoai04/PEMS`

Current local merge branch: `merge/dev-into-canh-safe-20260808`

Key SHAs:
- Cảnh parent: `907a7d61da57bd26552c2d8e4e2535d2c2415af7`
- Dev parent: `a4f63427c514da8004134f7889b7d6cf09328ba7`
- Merge commit: `424932ad446fa24d2dd173254170f471db8a84d2`

Mục tiêu duy nhất: xác minh chính xác vì sao trước merge báo `Emails integration = 854/854` nhưng merge commit báo `836/836`, và chỉ cho phép kết luận `MERGE VERIFIED — READY TO PUBLISH` khi chứng minh không có test email nào bị mất.

**Không push. Không force-push. Không update remote branch.**

---

## 1. One-shot execution

Thực hiện liên tục:

```text
preflight
→ verify SHAs
→ create clean Cảnh worktree
→ create clean merge worktree
→ identify exact canonical Emails integration command
→ force clean build on both
→ --list-tests on both
→ diff exact test names
→ run SAME full email suite on both
→ explain 854→836
→ fix only if real regression
→ rerun affected/full gates if changed
→ cleanup
→ final report
```

Không dừng giữa chừng để hỏi có làm tiếp không.

---

## 2. Preflight

Run:

```bash
git fetch origin --prune
git branch --show-current
git rev-parse HEAD
git status --short
git stash list
git worktree list

git rev-parse 907a7d61da57bd26552c2d8e4e2535d2c2415af7
git rev-parse 424932ad446fa24d2dd173254170f471db8a84d2

git ls-remote origin refs/heads/Cảnh-Iter1
git ls-remote origin refs/heads/Dev
```

Expected remote at start:

```text
origin/Cảnh-Iter1 = 907a7d61...
origin/Dev        = a4f63427...
```

If remote moved unexpectedly:
- report exact old/new SHA;
- do not force-reset;
- continue local verification only if safe.

Preserve all 11 stashes and current prompt/spec WIP.

---

## 3. Use two clean detached worktrees

Do not compare from current dirty/main tree.

```bash
git worktree add <temp-canh> --detach 907a7d61da57bd26552c2d8e4e2535d2c2415af7
git worktree add <temp-merge> --detach 424932ad446fa24d2dd173254170f471db8a84d2
```

Use the same environment for both:
- same .NET SDK;
- same Node/runtime where relevant;
- same DB;
- same `appsettings.Testing.json` if needed;
- same test runner options;
- same configuration.

If copying gitignored test config, copy the exact same file to both worktrees and record its hash.

---

## 4. Identify the exact canonical Emails integration command

Do not compare two different commands.

Find the exact command intended to represent **all Emails integration tests**.

Use the SAME:
- project;
- filter;
- configuration;
- runner settings;
- environment

for both SHAs.

Report the literal command in the final report.

Do not compare filtered vs unfiltered runs or stale vs fresh assemblies.

---

## 5. Force clean build on both

Before discovery and execution, clean/rebuild BOTH worktrees with equivalent commands.

Example:

```bash
dotnet clean
dotnet build
```

If DLL locks require redirected output, use the same safe redirected/gitignored strategy for both.

Do not rely on prior binaries.

---

## 6. List tests on Cảnh parent

On `907a7d61` run the canonical email command with `--list-tests`.

Capture full test names to:

```text
canh-email-tests.txt
```

Normalize only runner/header noise. Keep every full test name.

Count discovered tests.

Historical report said 854, but measure it; do not assume it.

---

## 7. List tests on merge commit

On `424932ad` run the exact SAME `--list-tests` command.

Capture:

```text
merge-email-tests.txt
```

Count discovered tests.

Historical report said 836, but measure it.

---

## 8. Diff the exact test name sets

Sort and compare by full test name.

Produce:

```text
Cảnh discovered:
Merge discovered:
Common:
Cảnh-only:
Merge-only:
```

If Cảnh-only = 0, then no tests were lost from the merge.

If Cảnh-only = 18, list all 18 exact test names.

Do not use vague conclusions like “probably a filter difference”.

---

## 9. If tests are really missing, trace the exact reason

For each missing test inspect:
- source file;
- class;
- `[Fact]` / `[Theory]` / equivalent attribute;
- namespace;
- project inclusion;
- trait/category/filter;
- target framework;
- conditional compilation;
- test SDK/discovery config.

Compare:

```bash
git diff 907a7d61 424932ad -- tests/PEMS.IntegrationTests
```

Also inspect `.csproj`, test config and any filter-related changes.

Prove/disprove:
- tests deleted;
- file excluded;
- category changed so filter no longer matches;
- prior run used a subset;
- stale assembly caused wrong count;
- environment/discovery difference;
- reporting error.

Do not guess.

---

## 10. Run the SAME full Emails integration suite on both

After discovery comparison, run the exact same full email suite on both worktrees.

Record exact:

```text
passed
failed
skipped
total
```

Both runs must use the same DB/config/filter/build policy.

---

## 11. Decision logic

### Case A — both discover the same count

Example:

```text
Cảnh = 836
Merge = 836
Cảnh-only = 0
```

Conclusion:

```text
Earlier 854 was produced by a different command/filter/stale measurement.
No email tests were lost by the merge.
```

No code change required.

### Case B — both discover 854

Conclusion:

```text
The 836 merge report was measurement error.
No test loss.
```

No code change required.

### Case C — Cảnh = 854, Merge = 836

Treat as REAL regression until explained.

Do not publish.

Find exact missing tests and root cause, fix on:

```text
merge/dev-into-canh-safe-20260808
```

Then rerun `--list-tests` and full email tests.

Required target:

```text
Cảnh-only = 0
```

### Case D — tests intentionally renamed/replaced

Only accept if you can map every removed test to an equivalent or stronger replacement:

```text
Old test → New replacement test
```

No unexplained coverage loss.

---

## 12. If a fix is required

Apply the smallest correct fix only on the merge branch.

Do not rewrite either parent branch.
Do not rebase/squash the merge.

Prefer a follow-up commit after `424932ad` for auditability.

Suggested message if appropriate:

```text
test(email): restore full integration test discovery after merge
```

Do not amend the merge commit unless there is a compelling reason.

---

## 13. Rerun after any fix

Required:

```text
Cảnh discovered:
Final merge discovered:
Cảnh-only:
```

Target:

```text
Cảnh-only = 0
```

Then run Emails integration fully and require 0 failures.

---

## 14. Full gates if any file changed

If any code/test/project/config file changed, rerun:

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

Target:
- 0 build errors;
- 0 test failures.

If no files changed because this was only a reporting/measurement mismatch, at minimum rerun the merge SHA's Emails integration freshly.

---

## 15. Do not publish

This task is verification only.

Do NOT execute:

```bash
git push
git push --force
git push --force-with-lease
```

Do not update `origin/Cảnh-Iter1` or `origin/Dev`.
Do not create PR in this task.

---

## 16. Cleanup

Remove both temporary worktrees safely.

If `node_modules` junctions were used, remove only the junction and verify the real directory remains intact.

Verify:

```bash
git worktree list
git stash list
git status --short
```

Main tree and 11 stashes must remain intact.

---

## 17. Final report format

### A. Preflight

```text
Cảnh parent:
Merge SHA:
origin/Cảnh:
origin/Dev:
stashes:
WIP:
```

### B. Exact Emails integration command

```text
<literal command>
```

### C. Discovery counts

```text
Cảnh discovered:
Merge discovered:
Common:
Cảnh-only:
Merge-only:
```

### D. Missing test list

List every exact missing test, or:

```text
Missing from Merge: 0
```

### E. Root cause of 854 → 836

Give one exact evidence-based explanation.

### F. Full Emails execution

```text
Cảnh:
Merge:
```

### G. Fixes

```text
Files changed:
Commit created:
```

or:

```text
No code change required.
```

### H. Coverage conclusion

Use one:

```text
EMAIL COVERAGE PRESERVED — NO TESTS LOST
```

or:

```text
EMAIL COVERAGE REGRESSION FIXED — NO TESTS LOST
```

or:

```text
NOT READY — <exact unresolved test loss>
```

### I. Remote state

```text
origin/Cảnh before:
origin/Cảnh after:
origin/Dev:
git push executed: NO
unexpected movement: YES/NO
```

### J. Final verdict

Only use:

```text
MERGE VERIFIED — READY TO PUBLISH
```

if:
- no email tests are missing;
- merge Emails integration is green;
- any required fix is committed and tested.

Otherwise:

```text
MERGE NOT READY — <exact reason>
```
