# Phase I — Legacy-Field Dependency Audit (corrected)

> **STATUS: IN PROGRESS — candidate hardened and drilled on disposable databases; the
> semantic occurrence audit is PARTIALLY COMPLETE; contract-drop NOT READY FOR EXECUTION.**
>
> This revision replaces an earlier version whose classifications and counts could not be
> reproduced from code. Every correction below was verified at `file:line` at HEAD; the
> specific defects of the previous revision are listed in §6 so they are not repeated.

## 1. Scope and method

Target: the 10 legacy global compatibility columns on `visit_requests` —
`delegation_name`, `visit_type`, `visit_type_other`, `purpose`, `working_content`,
`working_language`, `transportation_note`, `media_consent_status`, `media_consent_note`,
`note_to_fptu` (operational-contact fields are **not** in scope).

Method: symbol census over `backend/**/*.cs` excluding `obj/` and `bin/`, then **manual semantic
classification** per site — which entity is touched, whether the branch is guarded by
`FormSchemaVersion`, and whether the operation reads, writes, maps or serialises.

```bash
grep -rn "\bDelegationName\b" backend --include=*.cs | grep -v "/obj/" | grep -v "/bin/"   # and the other 9 symbols
```

## 2. Raw census (exact, measured at HEAD — no approximations)

| Symbol | Raw hits |
|---|---|
| DelegationName | 408 |
| VisitType | 150 |
| Purpose | 134 |
| WorkingContent | 83 |
| MediaConsentStatus | 76 |
| VisitTypeOther | 73 |
| WorkingLanguage | 66 |
| TransportationNote | 66 |
| MediaConsentNote | 65 |
| NoteToFptu | 51 |
| **Total raw symbol hits** | **1172** |
| **Distinct files touched** | **137** |

These are *symbol* hits, not classified occurrences: the same names are used by the canonical v2
entity (`VisitInstanceFormDetail`), DTOs, validators and tests. The previous revision's
"~120 reviewed / ~80 false positives" is neither exact nor reconcilable with this census and is
withdrawn.

## 3. Confirmed runtime blockers (verified at file:line)

A blocker = runtime code that reads or writes the **legacy `visit_requests` columns** and would
break, or silently change behaviour, once those columns are dropped.

| # | Field(s) | File:line | Category | Op | V1/V2 evidence | Blocker |
|---|---|---|---|---|---|---|
| B1 | all 10 | `PEMS.Application/Delegations/Services/VisitFormRead/VisitFormReadService.cs:240-244,380-382` | V2 dual-read with live V1 fallback | read | v2 branch reads `visit_instance_form_details`; **v1 branch reads `request.DelegationName/VisitType/VisitTypeOther/Purpose/WorkingContent`** | **YES** |
| B2 | DelegationName | `PEMS.Application/Reports/Commands/ExportDeptLeaderInvoice/ExportDeptLeaderInvoiceCommandHandler.cs:80,87` | V2 dual-read with live V1 fallback | read | `var delegationName = visit.DelegationName;` then overridden **only** when v2 | **YES** |
| B3 | DelegationName | `ExecuteEmailAction` / `GetEmailActionInfo` shared `ResolveDelegationNameAsync` | V2 dual-read with live V1 fallback | read | target-instance detail for v2, global column for v1 | **YES** |
| B4 | DelegationName | `PEMS.Infrastructure/BackgroundJobs/HoUnprocessedCampusAlertHostedService.cs` | V1-only runtime read | read | unguarded global read in a background job | **YES** |
| B5 | DelegationName | `PEMS.Infrastructure/BackgroundJobs/VisitReminderDispatchHostedService.cs` | V1-only runtime read | read | unguarded global read in a background job | **YES** |
| B6 | DelegationName | `PEMS.Infrastructure/Services/VisitContactClaimService.cs` | V1-only runtime read | read | global read for the claim landing/email | **YES** |
| B7 | all 10 | `PEMS.Infrastructure/Services/VisitRequestService.cs:149…` | **V1 create/WRITE** | write | object-initialiser populating a new `VisitRequest` from the v1 form | **YES** |
| B8 | all 10 | `PEMS.Infrastructure/Services/VisitRequestV2CreateService.cs` | compatibility projection write | write | writes the smallest-campus projection beside the canonical v2 detail | **YES** |
| B9 | subset | `PEMS.Infrastructure/Services/VisitRequestV2EditService.cs` | compatibility projection write | write | refreshes the projection after a pending edit | **YES** |
| B10 | subset | `PEMS.Infrastructure/Services/VisitSafeEditService.cs` | compatibility projection write | write | refreshes the projection after a safe edit | **YES** |

## 4. Reviewed and explicitly NOT a blocker

| Site | Why it is not a blocker |
|---|---|
| `PEMS.Infrastructure/Services/VisitRequestV2EditOps.cs:99,125,150` | Writes **`detail.DelegationName`** — the canonical `visit_instance_form_details` row, not the legacy projection. Unaffected by the column drop. (Previously misfiled as a projection-write blocker.) |
| `PEMS.Domain/Enums/OtpPurpose.cs`, `FilePurpose.cs`, `OtpService` `t.Purpose`, `GoogleDriveFolderResolver`, `FilesController` `purpose` query param | Unrelated same-name collisions on the word "Purpose". |
| `VisitInstanceFormDetail` entity + EF mapping | Canonical v2 storage — the migration target, not a dependency. |

## 5. Corrected counts

**At least 10 blocker sites** (not exhaustive — the two report readers in &sect;8b were missing from this table; superseded by &sect;8b) (a site may span several fields — expanding one site into one row per field
is what produced the previous revision's inconsistent totals).

| Category | Sites |
|---|---|
| V2 dual-read with a live V1 fallback | 3 (B1, B2, B3) |
| V1-only runtime read | 3 (B4, B5, B6) |
| V1 create/write | 1 (B7) |
| Compatibility projection write | 3 (B8, B9, B10) |
| **Total** | **10** |

## 6. Defects corrected from the previous revision

1. **Aggregate did not match its own table** — the table's rows split `13 + 1 + 17`, while §4
   asserted `14 + 1 + 16` (both happen to total 31, so the error was invisible in the total).
2. **`ExecuteEmailActionCommandHandler`** filed as a *compatibility projection write*; it is a
   **dual-read**.
3. **`VisitRequestV2EditOps`** filed as a blocker; it writes the **canonical v2 detail**.
4. **`VisitRequestService`** filed as a *V1 GET/read*; line 149 is a **V1 create/write**.
5. **`VisitFormReadService` omitted entirely**, despite reading all 10 legacy fields on its V1
   branch — arguably the single most important blocker.
6. **`ExportDeptLeaderInvoice` / HO report overview** excluded as "V2-aware"; both retain a live
   **V1 fallback** to `VisitRequest.*` and therefore remain blockers.
7. **Approximate counts** (`~120`, `~80`) replaced with the exact census in §2.

## 7. Readiness flags (evidence-based)

| Gate | Status | Evidence |
|---|---|---|
| Full v1→v2 backfill of persisted data | **NOT RUN** | no backfill artifact; the master seed still carries **117** `visit_requests` rows with `form_schema_version <> 2` (measured by `01_preflight.sql`) |
| Export / restore proof on real data | **NOT RUN** | no export artifact |
| Disposable refusal drill | **PASS** | §8 |
| Disposable upgrade (UP) drill | **PASS** | §8 |
| Disposable rollback (DOWN) drill | **PASS** | §8 |
| Fresh-target drill | **NOT RUN** | `00_fresh_target.sql` is generated by a blind regex rewrite and is not yet trusted (§9) |
| Zero runtime blockers | **FAIL** | the 10 sites in §3 are live at HEAD |

## 8. Disposable drill evidence (actually executed)

Disposable databases only. `pems_db`, `pems_test` and `pems_pr3_test` were never touched.
Runner: `docs/database/scripts/phase_1_candidate/run_migration.ps1` (fail-closed rewrite).

| Drill | Database | Result | Evidence |
|---|---|---|---|
| Refusal | `pems_i_refusal` | **PASS** | `Up` without `-OverrideBlockers`: preflight returned `PHASE1_PREFLIGHT_RESULT: FAIL` (`check_all_requests_v2` = 117 non-v2 rows; `check_runtime_blockers`), runner printed "REFUSED … payload was NOT executed", **exit 1**, `visit_requests` fingerprint unchanged (47 cols / 21 indexes / 7 CHECKs) |
| Upgrade | `pems_i_upgrade` | **PASS** | 12/12 preflight gates PASS → payload dropped **`visit_requests_chk_7`** (the *correct* visit_type CHECK, resolved by expression) → `PHASE1_UP_RESULT: DONE` → `PHASE1_VERIFY_RESULT: PASS`, exit 0 |
| Rollback | `pems_i_upgrade` | **PASS** | `Down` → `PHASE1_DOWN_RESULT: DONE` → verify(DOWN) PASS. **Schema fingerprint after DOWN == pre-UP (`4b6b715e5a3185283dc003d0f1632aae`)** and **data fingerprint after DOWN == pre-UP (`aa157ae803eb56b64b432d209168664f`)** ⇒ exact schema (incl. ordinal positions) and lossless data restore |
| Fresh target | — | **NOT RUN** | §9 |

Drill-fixture note: the raw master seed is correctly **refused** by the data gate (117 rows with
`form_schema_version <> 2`). To exercise UP/DOWN mechanics, the disposable copy was made
v2-consistent (`UPDATE visit_requests SET form_schema_version = 2`); the structural prerequisites
already held (`check_detail_per_instance`, `check_projection_parity` both PASS). This drills the
mechanism — it is **not** evidence that production data is ready.

## 8b. Follow-up session — corrections applied after this report's first revision

| ID | Finding | Status |
|---|---|---|
| F2 | `GetStaffLeaderDeptInvoiceItemsQuery` and `GetHoReportOverviewQueryHandler` gated the canonical read on `FormSchemaVersion >= PerCampus` **AND `HasMixedCampusDetails`**, so **uniform v2** fell back to the compatibility projection (7 sites) | **FIXED** `494bbdf5` — all v2 now reads `ci.FormDetail`; V1 unchanged. These were **missing from §3**, so the earlier "exactly 10 blocker sites" wording was not exhaustive and is withdrawn |
| F3 | `check_projection_parity` only proved a detail *existed*; it never compared values | **FIXED** `4b6735b1` — all 10 fields compared against the deterministic (campus_id ASC) projection with NULL-safe `<=>`, no COALESCE/TRIM |
| F4 | `Down` had **no read-only preflight**; `04_down_restore.sql` added columns and backfilled before testing `@unbackfilled`, so a failed gate left an auto-committed partial mutation | **FIXED** `4b6735b1` — mode-aware preflight (`UP`/`DOWN`) runs before both payloads; proven below |
| F1 | R6 occurrence-level appendix still incomplete | **OPEN** (§9) |
| F5 | Exact-manifest depth (ordered index members, `SEQ_IN_INDEX`, normalized CHECK expression, charset/collation, views/triggers/FK dependency sweep) | **PARTIAL** — columns/ordinals/defaults/comments and CHECK uniqueness-by-expression are enforced; the remaining manifest depth is **OPEN** |
| F6 | Phase II stopped too broadly | **ADDRESSED** — the independent instance-scoped readers were migrated (F2) without touching the mixed request-level email/notification question |
| F7 | Fresh target still blind-regex generated | **OPEN** — not run, not trusted |
| F9 | `VisitPhotoPanel` success toast still says `ảnh/video` | **OPEN** (cosmetic) |
| F10 | Phantom search hit (`RegistrantFullName/Nationality/JobTitle` searchable with no matched-context code) | **OPEN — NEEDS-BUSINESS-DECISION**, not self-selected |

**F4 refusal evidence (`pems_i_rollback`, pre-UP state):**
`check_down_state_is_post_up → FAIL: DOWN requires the post-UP state but 10 legacy column(s) still exist`
→ `PHASE1_PREFLIGHT_RESULT: FAIL` → "REFUSED: … restore payload was NOT executed (zero mutation)" → exit 1,
schema fingerprint unchanged (`4b6b715e5a3185283dc003d0f1632aae`).

**Full lifecycle re-drill (`pems_i_rollback`, after the fixes):** UP (16 gates PASS, dropped
`visit_requests_chk_7`) → verify(UP) PASS → **DOWN preflight PASS** → DOWN → verify(DOWN) PASS.
Schema FP `4b6b715e…` and data FP `60207c9bb800e52e03bb0a2b39b28996` both **identical to pre-UP**.

## 9. Known remaining gaps

- **The occurrence-level appendix is incomplete.** §3/§4 classify the blocker sites and the
  principal exclusions, but a per-occurrence disposition for all **1172** raw hits across **137**
  files has not been produced. Until it is, this audit must **not** be called "zero-unclassified".
- **`generate_fresh_target.ps1` performs a blind regex rewrite** of the master SQL with no
  assertion that the matched blocks are the intended ones, and `00_fresh_target.sql` has never been
  imported or verified. Both remain untrusted, so the fresh drill is NOT RUN.
- **DOWN is lossy for mixed-campus requests by construction** — the legacy columns can only carry
  the smallest-campus projection. The drill fingerprints matched because the fixture's projection
  was already consistent; that is not a general lossless guarantee for mixed v2 data.
- The restored `visit_type` CHECK is declared unnamed (as in the master), so MySQL re-assigns an
  auto-generated `visit_requests_chk_N`; the **expression** is restored exactly and the drill
  schema fingerprint matched, but the auto-name ordinal is not pinned.

## 10. Conclusion

Phase I contract-drop is **NOT READY FOR EXECUTION**. The candidate scripts are now fail-closed and
have passed refusal / upgrade / rollback drills on disposable databases with exact schema and data
fingerprint equality, but the 10 runtime blocker sites in §3 are live at HEAD, the persisted v1→v2
backfill has not been run or proven, and the fresh-target artifact is untrusted. No real database
has been modified.
