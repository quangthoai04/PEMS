---
type: merge-audit
feature: _shared
status: final
updated: 2026-07-29
---

# Latest Dev delta review — `d732e651..1a0f9c53`

Five commits Dev added after the baseline the original merge captured. Reviewed against the
integration branch before and after merging them, with the question: does anything here quietly
undo the email architecture, the concurrency work or the three defect fixes already on this branch.

Merge commit: `62e17cd2`. Total delta: **40 files, +1489 / −97**.

## The two facts that shape the whole review

**No SQL file is touched.** `git diff --name-status d732e651..origin/Dev -- "*.sql" docs/database`
returns nothing, so the canonical schema and its pinned SHA-256 are untouched by this synchronisation.

**No email file is touched.** No dispatcher, template, recipient-policy or `sent_email*` file appears
in the delta. The Cảnh email architecture therefore cannot have been regressed by these commits —
which is what made a 40-file merge produce exactly one conflict.

## Per-commit

### `d4286627` — Restructure delegation Drive folders with Photo/Document subfolders
- **Backend:** `VisitPhotoFolderService`, `IVisitPhotoFolderService`, `UploadVisitInstancePhotosCommandHandler`, `UploadNewsCoverImageCommandHandler`, `VisitPhotoFolder` entity
- **Tests:** both photo-folder test files updated with it
- **Email:** none. **Authorization:** unchanged. **Drive:** folder layout gains Photo/ and Document/ subfolders under the delegation folder.
- **Conflict with Cảnh email architecture:** none.
- **Decision:** accept as-is. The entity keeps its existing folder-id columns and adds to them rather than repurposing them, so folders created before this change still resolve; per-campus scoping is preserved.

### `f279130e` — Multi-partner document upload for in-progress visits
- **Backend:** new `VisitDocumentsController` + `UploadVisitDocument` command/handler/validator/response (233-line handler)
- **Frontend:** `visitDocumentsApi.ts`, `VisitDuringTab.tsx`, `endpoints.ts`
- **Email:** none. **Drive:** writes into the Document subfolder introduced by `d4286627` — the two commits are ordered dependencies.
- **Decision:** accept. New surface, no existing path rewritten.
- **Note for reviewers:** this is the largest new handler in the delta and the one with the most upload/partial-failure surface. It is **not** covered by the real-stack journeys in this branch, which stop at logistics and personnel. Flagged as coverage debt, not as a defect.

### `a6b5b139` — Trim Document Management type filter to active document types
- **Frontend only:** `DocumentFilterBar.tsx`, +4 / −1.
- **Decision:** accept. A filter list is narrowed; no canonical code is replaced by a label and no backend enum moves, so stored documents of other types remain readable — they are simply not offered as a filter.

### `e6742f4d` — Save-to-system action for logistics handover records
- **Backend:** new `SaveVisitLogisticsHandoverDocument` command/handler + `LogisticsHandoverPdfRenderer`, plus a `DelegationsController` route
- **Frontend:** `LogisticsHandoverSection.tsx`, `TaskHandoverModal.tsx`, `SharedDashboardView.tsx`, `StaffLeaderTaskModal.tsx`
- **This is the commit that overlaps the LG journeys**, so it was read rather than skimmed:
  - Authorization is explicit and **typed** — `ForbiddenException` for a caller who may not save this record, `NotFoundException` for a missing item/campus, `BusinessRuleException` for an invalid document type. Notably better than the surrounding reception-tasks handlers, which throw bare `Exception` (see §5 of the closure report).
  - **Both signatures are required before a record can be saved:** `if (handover.BorrowerSignedAt is null || handover.ProviderSignedAt is null) throw`. A half-signed handover cannot be archived as if it were complete.
- **Decision:** accept. Compatible with LG-05, which signs provider then borrower and then asserts exactly one BORROW row.

### `1a0f9c53` — Archive report exports to Drive via ReportArchiveService
- **Backend:** new `IReportArchiveService` / `ReportArchiveService`, wired into 7 export handlers + `ExportScheduleReportPdfQueryHandler`; `FilePurpose` and `FileValidationPolicy` extended; DI registration
- **Archive failure is best-effort by contract.** `ReportArchiveService.ArchiveAsync` wraps its work in `try/catch` and logs a warning; the interface documents "a Drive/DB hiccup here must never block the user from getting their report file". Verified at the call sites: the export returns its bytes regardless. So a Drive outage degrades archiving, not reporting.
- **Email ordering:** untouched. Archiving happens inside the export handler, not on the send path.

## The one conflict — `FilePurpose.cs`

Both sides added a report-related purpose in the same enum slot:

| Side | Member | DB value | Why it exists |
|---|---|---|---|
| Cảnh (this branch) | `ReportAttachment` | `REPORT_ATTACHMENT` | a report emailed as an attachment still needs a `files` row, because `sent_email_attachments.file_id` is NOT NULL |
| Dev | `ReportDocument` | `REPORT_DOCUMENT` | an *exported* report is archived to the flat Report Drive folder |

**Resolved as a union — both kept.** Taking either side alone silently removes a feature: drop
`ReportAttachment` and emailed reports lose the row their attachment link requires; drop
`ReportDocument` and `ReportArchiveService` references a member that no longer exists. They also answer
different questions, so collapsing them into one would make "was this report actually sent to
somebody" unanswerable from the file row alone.

Safe to carry both: `files.purpose` is `VARCHAR(100)`, not a DB ENUM, so no schema change is needed —
which is consistent with Dev not having shipped one.

Two follow-ups checked rather than assumed:
- `FileValidationPolicy` has an explicit rule for `ReportDocument` and none for `ReportAttachment`. That
  is not a regression: `git show HEAD:…FileValidationPolicy.cs` confirms Cảnh had no rule for it either,
  and the `_ =>` default (10 MB, document mimes) applies as it always did.
- No `IFileStorageFolderResolver` in Infrastructure switches on either member, so neither needed a
  folder-resolver arm.

## Effect on what this branch already guarantees

| Guarantee | Status after the merge |
|---|---|
| Six P0 handlers | `DepartmentReceptionTasks` and `Delegations/Commands/{InviteVisitParticipant,AssignDepartmentStaff,PrepareVisitLogistics}` are **not in the delta** — Dev touched none of them |
| Staff-status re-check (defect 1) | intact |
| Corrected transaction documentation (defect 2) | intact |
| Counter-offer variable classification (defect 3) | intact — no email file in the delta |
| Canonical SQL + pinned SHA-256 | unchanged, no SQL in the delta |
| Backend build | 0 errors across all four projects after the merge |

## Residual debt recorded, not fixed here

- `UploadVisitDocument` (233-line handler, multi-partner, Drive + DB) has no real-stack coverage.
- Drive-backed paths cannot be exercised in CI without credentials; see the closure report's CI section
  for how that boundary is drawn.
