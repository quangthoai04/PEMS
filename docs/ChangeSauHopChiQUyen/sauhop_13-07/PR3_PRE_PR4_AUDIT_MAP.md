# PR-3 → PR-4 Audit Map — global-projection form-field reads on `VisitRequest`

**Scope.** Repository-wide classification of every backend read/query/export/email/notification that still
references the *global* form-projection columns on `visit_requests`:
`delegation_name, visit_type, purpose, working_content, working_language, transportation_note,
media_consent_*, note_to_fptu`.

**Goal.** Before PR-4 turns on any per-campus v2 **write**, every code path that surfaces these fields to a
user (detail, list/search, email, notification, export/report) must either read the correct per-campus value
via the dual-read resolver, or be explicitly guarded so a mixed v2 order can never render the wrong campus's
data. This file is the living map; the per-file zero-unclassified pass completes as the **pre-PR-4 gate after
Group #4** (see §7).

`PerCampusFormV2` remains **OFF**; no v2 data exists. Nothing in this map is a behaviour change on its own — it
records *what* must be dual-read-safe before the write flag flips.

---

## 1. Classification taxonomy (corrected)

A reference is classified by **route / input key / DTO and by what the code actually does**, never by folder or
command name.

| Class | Definition | v2 rule |
|---|---|---|
| **A. Request-level read** | Input key is `visitRequestId`; DTO is the flat legacy request form. | Mixed → `409 FORM_VERSION_UPGRADE_REQUIRED` (transitional legacy guard only). Non-mixed reads per-campus detail. Final v2 product must render each campus as its own section — **not** "mixed → 409" forever. |
| **B. Instance-level read** | Route/key carries `visitInstanceId` (or a `participantId`/token bound to one instance). | Mixed request **must return 200**, sourcing form content **only** from the target instance's `visit_instance_form_details` + links; never global, never a sibling. Missing detail → `409 VISIT_FORM_DETAIL_MISSING`. |
| **C. Aggregate / list / report** | Spans multiple instances of a request (or many requests). | Scope-before-aggregate; per-campus sections. Never "pick the first campus". May defer *implementation* to PR-8, but must be v2-safe **or guarded** before the write flag flips (§6). |
| **D. Command that reads form for email/notification** | A write command whose handler **also reads** a form field to build an email/notification body. | This is a **read consumer**. When acting on a v2 instance it must source the field from the **target instance**, not `visit.DelegationName` (the first/global projection). |
| **P. Projection-writer** | Handler **assigns** `visit.<field> = …`. | Legitimate v1 write path. In PR-4 the v2 create/edit path writes per-campus detail instead; these stay for v1-compat until legacy columns are retired (not in this program). |

> **Correction 1 — read consumers hiding inside commands.** `ApproveCampusInstance`, `RejectCampusInstance`,
> `InviteVisitParticipant`, `AssignDepartmentStaff` are **not pure projection-writers**. Each operates on one
> campus instance yet reads the *global* `DelegationName` to compose an email/notification. On a v2 instance they
> must use the target instance's delegation name, not the first-campus projection. (Evidence in §4.)
>
> **Correction 2 — `ExportDeptLeaderInvoice` is a read/export, proven by code.** It queries
> `VisitRequestCampuses` keyed by `request.VisitInstanceId`, **reads** `ci.VisitRequest.DelegationName`, and
> stamps it into the PDF (`meta.DelegationName` → "Tên đoàn"); it never assigns the projection. So it is an
> **instance-level Class-B read/export**, not a Class-P writer. (Evidence in §5.)
>
> **Correction 3 — list/dashboard/report gating.** Class-C surfaces may have their *implementation* deferred to
> PR-8, but the v2 **write** flag must not be enabled for users until those surfaces are v2-safe or explicitly
> guarded — otherwise a newly-created mixed order would render wrong on lists/reports.

---

## 2. Class-B read-detail handlers — MIGRATED (Group #3 + #4a), each its own commit

All source v2 (incl. mixed) form content from the **target instance** via
`IVisitFormReadService.ResolveCampusFormContentAsync(request, new[]{ targetInstanceId }, ct)`; missing →
`409 VISIT_FORM_DETAIL_MISSING`; v1 byte-identical. See `PR3_TEST_REPORT.md` §6b–6h.

| Handler | Key | Commit |
|---|---|---|
| `GetSubmittedVisitRequestFormDetail` (Class A) | `visitRequestId` | 299c2b73 |
| `GetEditableVisitRequestDetail` (Class A) | `visitRequestId` (owner) | 7f0a79b6 |
| `GetVisitProcessDetail` (Class B) | `(visitRequestId, visitInstanceId)` | c5719550 |
| `GetVisitInstanceSummary` (Class B) | `visitInstanceId` | 3e720364 |
| `GetVisitInstanceContribution` (Class B) | `visitInstanceId` | 1cba63be |
| `GetVisitInvitationDetail` (Class B) | `participantId` → one instance | 5ee29ab3 |
| `GetStaffCalendarDetail` (Class B) | `visitInstanceId` | 4bdc1c6d |
| `GetRequestDetail` (Dept, Class B) | `logisticsItemId` → one instance | 76a68c53 |
| `GetInvitationDetail` (Dept, Class B) | `participantId` → one instance | bae71e03 |
| `GetVisitInvitationById` (Class B) | `participantId` → one instance (owner-scoped) | 00f0ee06 |
| `GetAgendaSetupForInstance` (Class B) | `visitInstanceId` | 50bebef4 |

---

## 3. Class-B read-detail handlers — GROUP #4 COMPLETE

All Class-B read-detail handlers are migrated (§2), each instance-level (mixed → 200 with the target
instance), each its own commit, each verified full-green (build → targeted tests → Unit 435 + Arch 14 + full
IntegrationTests on a fresh PR-2 master).

| Order | Handler | Route / key | Status |
|---|---|---|---|
| ~~0a~~ | ~~`GetStaffCalendarDetail`~~ | ~~`DashboardController` · `visitInstanceId`~~ | **DONE** — §2 / report §6i. |
| ~~0b~~ | ~~`GetRequestDetail` (DeptReceptionTasks)~~ | ~~`logisticsItemId` → one instance~~ | **DONE** — §2 / report §6j. |
| ~~0c~~ | ~~`GetInvitationDetail` (DeptReceptionTasks)~~ | ~~`participantId` → one instance~~ | **DONE** — §2 / report §6k. |
| ~~0d~~ | ~~`GetVisitInvitationById` (ViewMyVisitInvitations)~~ | ~~`participantId` → one instance~~ | **DONE** — §2 / report §6l. |
| ~~0e~~ | ~~`GetAgendaSetupForInstance`~~ | ~~`visitInstanceId` (visit_type)~~ | **DONE** — §2 / report §6m. |

**Next** (post-Group-#4, per §6/§8): the Class-C list/dashboard/report surfaces and the export/report handlers
(§5, §6) become v2-safe or explicitly guarded under **PR-8**, and PR-4 ships behind its own write flag,
default OFF (§8). No read-detail handler still depends on the global projection.

Calendar **lists** (`GetDepartmentCalendar`, `GetStaffCalendar`) and other Class-C surfaces are per-instance
items — treated under §6 (PR-8 implementation, but guarded before write flip).

`ViewGuestDelegationDetails` remains a **stub** (documented in `PR3_TEST_REPORT.md` §6d with file/route
evidence) — no migration because it has no live consumer.

---

## 4. Class-D — commands that read the form for email/notification (Correction 1)

Each reads the **global** `DelegationName` while acting on **one** campus instance. Fix in the PR that owns the
write+notify path: source the delegation name from the **target instance** detail, not the first/global
projection. Until then these are covered by the write-flag gate (§8).

| Handler | Read site | What it feeds |
|---|---|---|
| `ApproveCampusInstance` | `…CommandHandler.cs` — `visit.DelegationName` (host-assigned + HO status notifications, e.g. lines 237, 264, 284, 300) | In-app notifications (no email) |
| `RejectCampusInstance` | `…CommandHandler.cs:145,165,181` — `visit.DelegationName` | In-app notifications |
| `InviteVisitParticipant` | `…CommandHandler.cs:106` — `instance.VisitRequest.DelegationName` | Invitation **email** body/subject |
| `AssignDepartmentStaff` | `…CommandHandler.cs:91,96` — `vr.DelegationName` / `instanceInfo.DelegationName` | Assignment **email** subject/body |
| `ExecuteEmailAction` | `…CommandHandler.cs:93,209,326,452` — `instance.VisitRequest.DelegationName` | Email-action info result DTO |
| `GetEmailActionInfo` | `…QueryHandler.cs:78,142,202` — `instance.VisitRequest.DelegationName` | Public email-action landing DTO (Class B query, instance-bound) |

---

## 5. Class-B/C exports & reports (Correction 2)

| Handler | Proven behaviour | Class |
|---|---|---|
| `ExportDeptLeaderInvoice` | Keyed by `request.VisitInstanceId`; **reads** `ci.VisitRequest.DelegationName` (`…CommandHandler.cs:61`) → PDF `meta.DelegationName` (`:126`, "Tên đoàn" `:232`). Never assigns the projection. | **B — instance read/export**, not P. v2 → target-instance delegation name. |
| `ExportHoReport`, `ExportStaffLeaderReport`, `ExportDeptLeaderReport` | Aggregate exports across instances/requests. | **C** — per-campus sections; PR-8 impl, guarded before write flip. |
| `GetHoReportOverview`, `GetStaffLeaderReportOverview`, `GetDeptLeaderReportOverview`, `GetDeptLeaderInvoiceVisits` | Aggregate/list report data. | **C** — same. |
| `ViewFeedbackSummary`, `SearchAndFilterMinutes`, `ExportMinutesPdf/Excel` | Aggregate/list read of form fields for reporting. | **C** — same. |

---

## 6. Class-C list / dashboard / report surfaces (Correction 3)

Dashboards, calendars, search/filter lists and reports that read global form fields across many instances:
`GetHODashboardOverview`, `GetDepartmentLeaderDashboardSummary`, `GetStaffCalendar`, `GetDepartmentCalendar`,
`GetAssignmentsProgressList`, `GetVisitInvitations`, `ViewMyVisitInvitations`, `ViewGuestDelegationList`,
`SearchAndFilterFeedback`, `SearchAndFilterMinutes`, plus the report handlers in §5.

**Rule:** implementation may land in **PR-8**, but the v2 **write flag must stay OFF** until each is v2-safe
(per-campus scoped) **or** explicitly guarded (e.g. hide/label mixed v2 rows). Otherwise a new mixed order shows
the wrong/empty projection on these surfaces.

---

## 7. Projection-writers (Class P) — legitimate v1 write paths

Verified by assignment grep (`visit.<field> = …`). These stay for v1-compat; PR-4's v2 create/edit writes
per-campus detail instead. No legacy columns are dropped in this program.

| Handler | Sites |
|---|---|
| `UpdatePendingVisitRequest` | `…CommandHandler.cs:169-182` (all 8 fields) |
| `ResubmitRejectedVisitRequest` | `…CommandHandler.cs:228-240` (all 8 fields) |
| v1 create path (`CreateAuthenticatedVisitRequest`, `VerifyAndCreateVisitRequest`) | object-initializer writes at creation — v1-compat |

**Non-`VisitRequest` false positives** (excluded): `UpdateAgendaTemplate.VisitType`,
`UpdateEmailTemplate.Purpose`, and all `AgendaTemplate` / `EmailTemplate` / `Feedback` / `ApiIntegration`
matches — these own their *own* `Purpose`/`VisitType` columns, unrelated to `visit_requests`.

---

## 8. Write-flag gate for PR-4

PR-4 (create request v2 in one transaction) may be **built** after Group #4, but ships behind its **own write
flag, default OFF**, separate from the read-side `PerCampusFormV2`. Flip the write flag ON only when **all** of:

1. Class-B read-detail handlers migrated (§2 done + §3 remaining).
2. Class-C list/search surfaces v2-safe or explicitly guarded (§6, PR-8).
3. Class-D email/notification read sites source per-campus (§4).
4. Essential exports/reports (§5) v2-safe or guarded.

Until then: `PerCampusFormV2` OFF, write flag OFF, no v2 rows.

---

## 9. Status

- **Verified & code-backed:** taxonomy + 3 corrections (§1), migrated handlers (§2), remaining Group-#4
  handlers (§3), Class-D read-consumers (§4), export/report classes incl. `ExportDeptLeaderInvoice` proof (§5),
  Class-P writers (§7).
- **Pending (pre-PR-4 gate, after Group #4):** exhaustive per-file *zero-unclassified* pass over the remaining
  Class-C list/report handlers, and the write-flag wiring for PR-4.
- **HEAD at this checkpoint:** `5ee29ab3` — Integration **267/267**, Unit **435/435**, Architecture **14/14**.
