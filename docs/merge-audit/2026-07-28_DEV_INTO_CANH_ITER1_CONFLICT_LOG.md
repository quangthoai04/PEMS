# Conflict log — `Dev` → `Cảnh-Iter1`

**Integration branch:** `merge/dev-into-canh-iter1`
**Created from:** `origin/Cảnh-Iter1` @ `52d666bbf3d905f733321f2af0c77dedc6761939`
**Merged in:** `origin/Dev` @ `d732e651b3d5b53932cff23a3750fc502928cf79`
**Merge base:** `06c73b9491b7fb5afb88d20fc64de5ed9a56500c`
**Backups:** `backup/canh-iter1-before-dev-merge`, `backup/dev-before-canh-merge`

Because the branch was cut from `Cảnh-Iter1`, throughout this log **`ours` = Cảnh-Iter1** and
**`theirs` = Dev**.

---

## 1. Inventory

Git reported **7 content conflicts**. Those are not the whole story: 15 files were edited on *both*
sides, so 8 of them merged **textually clean while being semantically wrong**. Each of those 8 was
read and verified against §5 rather than trusted.

| Kind | Count |
|---|---|
| Content conflicts (`UU`) | 7 |
| Auto-merged but touched on both sides (verified by hand) | 8 |
| Rename/modify (canonical SQL) | 1 |
| Delete/keep (frontend pages) | 2 |
| Modify/delete **not** flagged by git (compile-breaking) | 1 group (3 handlers) |

---

## 2. The seven content conflicts

### 2.1 `UpdateAccountRoleCommandHandler.cs` — 3 hunks

Conflicts were confined to the field list and constructor.

* **Dev kept:** `IUserMutationLockService`, lock-before-read, `AccountRoleChangeDependencyChecker`,
  structured blockers, atomic department-head handover, commit-before-revoke/email.
* **Cảnh kept:** `ISystemEmailDispatcher`, `SystemEmailTemplates.AccountRoleChanged`, template variables.
* **Resolution:** union of both dependencies; `IEmailService` dropped entirely.
* **Verified flow:** `SaveChanges → Commit → RevokeAllActiveSessions → SendRoleChangedNotification → return`
  (`:349-361`), and the no-op early return still writes no audit, revokes nothing and sends nothing.
* **Tests:** `UpdateAccountRoleCommandHandlerTests` (harness merged, below), `AccountRoleChangeConcurrencyTests` (8).

### 2.2 `PrepareVisitLogisticsCommandHandler.cs` — 2 hunks

Dev's side of both hunks was the **pre-dispatcher email construction** (hard-coded subject/body,
manual `SentEmail`/`SentEmailRecipient` rows). Cảnh's side is the dispatcher.

* **Resolution:** took Cảnh's side for both hunks — §4.1 forbids restoring a removed hard-coded helper,
  and Dev's `finalBody` local no longer exists anywhere else in the file.
* **Dev's business rules survive untouched:** no `Priority`, no client `DueAt`; the deadline is computed
  by the backend as `usageStart - 24h` for `SYSTEM_REQUEST` and `null` for `OFFLINE_COORDINATED` (`:161`).
* **Tests:** `PrepareVisitLogisticsCommandHandlerTests` — the `dueAt` assertion moved from the client's
  value to the server-computed one (`08:00 31/07/2026`).

### 2.3 `AssignRequestAssigneeCommand.cs` — 2 hunks

Same shape: Dev's side was the old hard-coded HTML body (including a mojibake'd Vietnamese literal).

* **Resolution:** Cảnh's `AttachTo` + `MarkDeliveryFailedAsync` kept; the HTML builder dropped.
* **Dev kept:** assignee lock, eligibility recheck under lock, terminal/in-flight guard, pending-attempt
  guard, handover-signed guard, schedule conflict, commit-before-deliver.
* **Priority prefix/label:** absent — §5.5 satisfied.

### 2.4 `ProposeRequestChangeCommand.cs` — 2 hunks

* **Resolution:** Cảnh's dispatcher path kept; Dev's `ProposalContentHtml` builder dropped.
* **§5.6 required a change beyond simply picking a side.** Cảnh's call sent only `proposalNote`, which
  §5.6 explicitly forbids when the proposal carries new quantity/time/content. The template, the code
  registry and the handler were all extended to carry the counter-offer itself:
  `delegationName, originalQuantity, proposedQuantity, proposedUsageStartAt, proposedUsageEndAt,
  proposedDescription`. Absent fields render as “Không đổi” rather than blank.
* **Dev kept:** `ProposedQuantity >= 1` and strictly `< original`, mandatory reason, multi-day window,
  original quantity never overwritten, proposer auto-becomes assignee only when the seat is empty,
  `CHANGE_PROPOSED`, old tokens invalidated.

### 2.5 `SharedDashboardView.tsx` — 1 hunk

Dev deleted three variables (`dueAt`, `campusName`, `delegationName`) from the assign-preview context.
The renderer requires the declared set exactly, so deleting them would break the preview at runtime.

* **Resolution:** kept Cảnh's three lines. `p` carries no real `dueAt`, so the placeholder stands.
* Everything else in this 3.7k-line file is Dev's.

### 2.6 `UpdateAccountRoleCommandHandlerTests.cs` — 2 hunks

* **Resolution:** merged, not chosen. Harness now holds **both** `FakeSystemEmailDispatcher` (Cảnh) and
  `RecordingUserMutationLockService` (Dev). Dev's `AssertNoSideEffects` helper was **kept** and rewired:
  “no email” is now asserted as `Assert.Empty(Dispatcher.Sent)` instead of a Moq `Verify` on
  `IEmailService`, because the handler no longer composes its own message.

### 2.7 `InviteVisitParticipantCommandHandlerTests.cs` — 1 hunk

* **Resolution:** dispatcher (Cảnh) + recording lock service (Dev), returning Cảnh's 5-tuple, which all
  8 call sites in the file already destructure.

---

## 3. Auto-merged but touched on both sides — verified, not trusted

| File | Verdict |
|---|---|
| `AssignDepartmentStaffCommandHandler.cs` | Correct. Transaction → lock → re-read eligibility → `PrepareAsync` → SaveChanges → Commit → `DeliverAsync`. The old “no explicit transaction” comment is gone. |
| `InviteVisitParticipantCommandHandler.cs` | Correct. Lock → re-resolve → `Conflict` on drift; 3 role-specific templates; `recipientDeptId` and the Department-vs-participant notification routes intact. |
| `DependencyInjection.cs` | Correct union: `IUserMutationLockService` (Dev) + all 8 Cảnh email/file services. No interface registered twice; 5 hosted services and every external integration (Drive, Document AI, Translation, Vision) survive. |
| `LogisticsRequestSection.tsx` | Dev's UI kept. One repair needed — see §5. |
| `ParticipantInvitationSection.tsx` | Correct. |
| `VisitContactClaim/TransferWorkflowTests.cs` | Correct. |
| `PrepareVisitLogisticsCommandHandlerTests.cs` | Needed the `Priority`/`DueAt` argument removal. |

---

## 4. The conflict git did **not** report

`AccountConfirmationEmail` was **deleted on Cảnh-Iter1** (replaced by `AccountEmailVariables` plus the
`ACCOUNT_EMAIL_CONFIRMATION` template). Dev's new `DepartmentLeaderPersonnel` module — written on the
old base — calls it from three handlers. Git saw an untouched deletion and three new files, so it
reported nothing; the build reported 8 errors.

Per §7.2 and the §14 gate (“system email phải dùng dispatcher”), all six sends in that module moved onto
the dispatcher:

| Send | Template |
|---|---|
| Create personnel | `ACCOUNT_EMAIL_CONFIRMATION` (reused) |
| Resend confirmation | `ACCOUNT_EMAIL_CONFIRMATION` (reused) |
| Edit identity — old address | `ACCOUNT_EMAIL_CHANGED_OLD_NOTICE` / `..._PENDING_...` (reused, **zero variables**) |
| Edit identity — new address | `ACCOUNT_EMAIL_CONFIRMATION` or `ACCOUNT_EMAIL_CHANGED_NEW_NOTICE` (reused) |
| Disable / enable | `DEPT_PERSONNEL_ACCOUNT_DISABLED` / `_ENABLED` (**new**) |
| Leadership transfer | `DEPT_LEADERSHIP_GRANTED` / `_HANDED_OVER` (**new**) |

Four of the six reuse templates Cảnh already had for exactly this scenario. Only four new codes were
added, and the catalog contract test asserts registry and seed agree in both directions (30 = 30).

The privacy property of the old-address notice is now **stronger**: it was asserted by string-searching
rendered HTML, and is now asserted as “this template declares no variables at all”.

---

## 5. Canonical SQL — rename/modify

Dev renamed `PEMS_FULL_V2_NO_SEED_DATA_GALLERY.sql` → `..._DOCUMENT_AI_FIXED.sql`; Cảnh heavily edited
the old name. Git resolved the rename and applied Cảnh's edits onto the renamed file.

* Only one canonical file exists; the old name is gone and referenced nowhere (0 hits repo-wide).
* Each of the 9 email tables is defined **exactly once**; no duplicate `CREATE TABLE` anywhere.
* Dev's `proposed_quantity` / `proposed_usage_*` / `preparation_note` / `coordination_mode` / `due_at`
  all present. `file_purpose` is a free `VARCHAR(100)`, so `REPORT_ATTACHMENT` needs no enum entry.
* **`tests/.../CanonicalSqlScript.cs` still pointed at the pre-rename name.** On `Dev` that file does not
  exist, so the integration preflight could not resolve the script and the suite never ran there. Path
  and `ExpectedSha256` were both updated; new hash
  `322a8a94c2dc61192e46d14769acb41af287c486b8e942fbf5850655702d68a0`.

### Fresh import evidence (§9.8)

Imported into disposable `pems_merge_validation`; `pems_db` untouched (retargeting verified 0 surviving
references before a byte was sent).

| Metric | Merged | Dev baseline (`pems_dev_baseline`) |
|---|---|---|
| Import exit code | 0 | 0 |
| Base tables | 82 | 82 |
| Triggers | 32 | 32 |
| Foreign keys | 252 | 252 |
| Email templates | **30** | 16 |
| Duplicate template codes | 0 | — |

The script's own diagnostic block reports `contact_guard_negative_failures = 14`. Dev's **unmodified**
canonical file reports the identical 14, so this is pre-existing and not merge-induced. Recorded, not
hidden.

---

## 6. Frontend

* `TaskDetail.tsx` / `TaskInvitationDetail.tsx` stay deleted — 0 live imports, routes or links.
* `LogisticsRequestSection.tsx` referenced `payload.dueAt`, a field Dev removed from the payload type.
  Rather than print a placeholder, the preview now reproduces the **server's** rule (`usageStart - 24h`),
  so the preview matches the email that will actually be sent.

---

## 7. Tests changed, and why (no test was deleted or weakened)

| Test | Change | Reason |
|---|---|---|
| `Registry_holds_the_agreed_number_of_templates` | 26 → 30 | 4 new DEPT_* codes |
| `SystemEmailG4ClosureTests.Catalog` / `CatalogSize` | +4 codes, 26 → 30 | same |
| `SystemEmailTemplateContractTests` | 26 → 30 | same |
| `LogisticsEmailEndToEndTests.Proposal()` | +6 variables | §5.6 |
| `ProposeRequestChangeCommandHandlerTests` | expects the 10-variable set | §5.6 |
| `PrepareVisitLogisticsCommandHandlerTests` | dropped `Priority`/`DueAt`; `dueAt` asserts the computed value | Dev §5.4 |
| `RemindExpenseReports` / `AssignRequestAssignee` / `ProposeRequestChange` tests | dropped `Priority` initializer | column removed from the entity |
| `DepartmentLeaderTestHarness` + 4 suites | `Mock<IEmailService>` → `FakeSystemEmailDispatcher` | module moved to the dispatcher |
| `AccountRoleChangeConcurrencyTests.AssignHostAsync` | writes a **legal** ASSIGNED state | see below |
| `DepartmentHeadHandover_IsRolledBackWhenAnotherBlockerRefuses` | host assigned **before** the head move | see below |

The last two deserve the detail. Both tests set `current_host_user_id` on a
`WAITING_REQUEST_APPROVAL` row, which `trg_visit_campuses_assignment_validate_bu` has always refused —
they were producing a row the application could never create. They never failed on `Dev` only because
the canonical-script path was broken there and the whole suite silently skipped. With the path fixed
they ran for the first time and failed honestly. The fix makes the seed legal (status `ASSIGNED` plus
host *and* decision metadata) and orders host-then-head, which is also the only sequence the schema
permits. The assertions themselves were not weakened.

---

## 8. Gate results

| Gate | Result |
|---|---|
| Backend build (4 projects) | **0 errors** |
| Unit | **1690 / 1690** |
| Architecture | **14 / 14** |
| Integration | **941 / 941** |
| Frontend `tsc --noEmit` | **0 errors** |
| Frontend `vite build` | **success** |
| Frontend unit (vitest) | **760 / 760**, 58 files |
| Conflict markers | **0** |
| `git diff --check` | clean (CRLF notices only) |
| `request.Priority` / `request.DueAt` / `LogisticsPriorityText` | **0** |
| Old canonical SQL path | **0** |
| `IEmailService` in Application | only the interface + `SystemEmailDispatcher` / `ManualEmailSender` — no business handler |
| `IUserMutationLockService` | 14 handlers |

**Not run:** §12.6 real-stack journeys. See the final report.
