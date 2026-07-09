# PEMS Frontend-only i18n Completion Report

> Date: 2026-07-09 | Branch: Canh-Iter1
> Continues [`PEMS_FRONTEND_I18N_RUNTIME_AUDIT_FIX_REPORT.md`](./PEMS_FRONTEND_I18N_RUNTIME_AUDIT_FIX_REPORT.md),
> which left 23 hardcoded Vietnamese strings in the public Excel upload path as the last
> public-gating blocker. Those are now resolved.

---

## 1. Scope

Frontend-only UI static text. **No SQL, no backend, no database changes.**

Verified: `git status` shows changes only under `frontend/pems-react/` and `docs/`.
No route, auth-guard, role/permission, business-logic, or validation-rule change.

---

## 2. Files changed

| File | Change |
|---|---|
| `features/visit-request/components/ExcelUpload/excelValidator.ts` | Rewritten. Columns are now canonical ids (`fullName`/`jobTitle`/`organization`/`nationality`) matched against a VI + EN **alias table**, so a template downloaded in either language still parses. All 17 user-facing strings → `visitRequest:excel.errors.*`. Takes an `ExcelTranslator`. Duplicate/row-scan logic extracted and shared between the two validators (was copy-pasted). |
| `features/visit-request/components/ExcelUpload/excelDownload.ts` | Template headers, sheet names, and sample rows now generated from `visitRequest:excel.template.*` in the active language. Takes an `ExcelTranslator`. |
| `features/visit-request/components/sections/VisitorListSection.tsx` | Passes `excelT` into `validateVisitorExcel` / `downloadVisitorTemplate`. |
| `features/visit-request/components/sections/ContactSection.tsx` | Passes `excelT` into `validateSupportTeamExcel` / `downloadSupportTeamTemplate`. |
| `tests/excel-i18n.spec.ts` | **New** — 11 tests over the real validator with real `.xlsx` fixtures. |
| `tests/fixtures/*.xlsx` | **New** — 6 fixtures: valid VI-header, valid EN-header, missing-cell (VI & EN), missing-column, header-only. |

---

## 3. Locale keys added/updated

| Namespace | Keys |
|---|---|
| `visitRequest` | + `excel.errors.*` — `invalidFileType`, `noData`, `headerOnly`, `missingColumns` (`{{missing}}`, `{{required}}`), `requiredCell` (`{{row}}`, `{{column}}`) |
| `visitRequest` | + `excel.template.*` — `index`, `fullName`, `jobTitle`, `organization`, `nationality`, `visitorsSheet`, `supportSheet`, and 7 sample-row values |

All 19 keys present in both VI and EN; interpolation used throughout (no string concatenation).
`audit-i18n.mjs` confirms 0 missing keys, 0 empty values, 0 type mismatches.

---

## 4. Hardcoded findings resolved

| File | Old text | New key |
|---|---|---|
| `excelValidator.ts` | `File Excel không có dữ liệu.` | `visitRequest:excel.errors.noData` |
| `excelValidator.ts` | `File không có dữ liệu (chỉ có header hoặc rỗng).` | `visitRequest:excel.errors.headerOnly` |
| `excelValidator.ts` | `Thiếu cột bắt buộc: … File phải có các cột: …` (template literal ×2) | `visitRequest:excel.errors.missingColumns` `{{missing}}` `{{required}}` |
| `excelValidator.ts` | `Dòng {n}: Cột "{col}" không được để trống.` (template literal ×2) | `visitRequest:excel.errors.requiredCell` `{{row}}` `{{column}}` |
| `excelValidator.ts` | `['Họ và tên', 'Chức vụ', 'Đơn vị công tác', 'Quốc tịch']` ×3 required-column arrays | canonical ids + `COLUMN_ALIASES` (VI + EN) |
| `excelDownload.ts` | `['STT', 'Họ và tên', 'Chức vụ', 'Đơn vị công tác', 'Quốc tịch']` ×2 | `visitRequest:excel.template.*` |
| `excelDownload.ts` | Sheet names `Danh sách khách`, `Team hỗ trợ` | `visitRequest:excel.template.visitorsSheet` / `supportSheet` |
| `excelDownload.ts` | Sample rows `Nguyễn Văn A / Giám đốc / Công ty XYZ`, `Trần Thị B / Trưởng nhóm / Công ty ABC` | `visitRequest:excel.template.sample*` |

**23 → 0 public-gating findings.**

### Why the Excel headers could safely be localized

The prompt's constraint — *"if the header must be fixed for the parser, keep the technical
header"* — was checked against the code rather than assumed. The sheet is parsed **entirely
in the browser**: `validateVisitorExcel` returns plain objects that are appended to the
React-Hook-Form state and submitted as JSON. No backend endpoint ever sees the spreadsheet or
its headers.

So headers are safe to localize, **provided the parser is language-agnostic**. It now matches
each column against all of its aliases (case-insensitive, whitespace-normalised):

```ts
fullName:     ['họ và tên', 'full name'],
jobTitle:     ['chức vụ', 'job title', 'position'],
organization: ['đơn vị công tác', 'organization', 'organisation'],
nationality:  ['quốc tịch', 'nationality'],
// leading index column: 'stt' | 'no.' | 'no' | '#'
```

This means a template downloaded in Vietnamese still uploads while the UI is in English, and
vice versa — which the old code could not do (it used exact `indexOf` against Vietnamese
literals, so an English header row failed with *"Thiếu cột bắt buộc"*). Both directions are
covered by tests.

The four remaining Vietnamese strings flagged by `audit-hardcode.mjs` in `excelValidator.ts`
are **the alias table itself** — parse data, not user-facing text, in the same category as the
`countryFlag` / `countryMatch` lookup maps.

---

## 5. Out of scope dynamic DB content

| Module | Field | Reason |
|---|---|---|
| News | `title`, `summary`, `bodyText` | Dynamic DB content — out of scope for frontend-only UI i18n. (Already backend-translated via `news_translations`; `languageCode` is sent.) |
| FAQ | `question`, `answer` | Dynamic DB content — out of scope for frontend-only UI i18n. |
| Partners | `description`, `collaborationSummary`, location text | Dynamic DB content — out of scope for frontend-only UI i18n. |
| Gallery / Visit FPTU | area / location / item name and description | Dynamic DB content — out of scope for frontend-only UI i18n. |

No machine translation of DB content was added on the frontend. No translation table was
created. `Accept-Language` continues to be sent by `httpClient.ts` for whenever the backend
chooses to use it.

---

## 6. Build / test result

| Check | Result |
|---|---|
| `npm run build` | ✅ exit 0 — built in 14.87s |
| `npm run lint` | ✅ exit 0 — note: this script is `tsc --noEmit`, i.e. a **typecheck**, not ESLint |
| `node scripts/audit-i18n.mjs` | ✅ exit 0 — 14 namespaces; 0 missing keys, 0 empty, 0 type mismatch, 0 mojibake, 0 unregistered namespaces, 0 unresolved `t()` call sites |
| `node scripts/audit-hardcode.mjs` | ✅ 75 findings, **0 public-gating** (breakdown below) |
| `node scripts/audit-hardcode.mjs --all` | ⚠️ 5,119 findings — 3,868 in `pages/dashboard` (75 files) |
| `npx playwright test` | ✅ **38 passed** (11 new Excel tests + 27 existing) |

### `audit-hardcode.mjs` (public + shared) — 75 findings, none gating

| Class | Count | Gating? |
|---|---:|---|
| Auth-only internal home sections (`components/home/internal/*`) | 57 | No — rendered only for signed-in non-VISITOR users (`HomePage.tsx:18`); counted as internal debt |
| Lookup data (`countryFlag` / `countryMatch`, VI-keyed maps) | 7 | No — data, not UI |
| Dead defaults (`SearchPopup` `city:` constants, always overridden by `t()` before render) | 5 | No |
| Excel header alias table (`excelValidator.ts`) | 4 | No — parse data, not UI |
| Language-switcher labels (`Header.tsx`, `NewsDetailPage.tsx`) | 2 | No — intentional |

### New Excel tests (`tests/excel-i18n.spec.ts`)

Run the real validator against real `.xlsx` fixtures and the real locale JSON:

- VI-header and EN-header templates both parse with zero errors — **cross-language compatibility**.
- EN mode: missing cell → `Row 2: column "Job Title" must not be empty.`
- EN mode: missing column → `Missing required column(s): Nationality…`
- EN mode: header-only file → `The file has no data rows (header only, or empty).`
- EN mode: no error text contains `không được`, `Vui lòng`, `thiếu`/`Thiếu`, `dòng`/`Dòng`, and no Vietnamese diacritic at all.
- VI mode: `Dòng 2: Cột "Chức vụ" không được để trống.` / `Thiếu cột bắt buộc: Quốc tịch…`
- Duplicate-row skipping still works (no behaviour regression from the refactor).

---

## 7. Verdict

| Scope | Verdict |
|---|---|
| **Public frontend static i18n** | ✅ **PASS** |
| **Shared frontend i18n** | ✅ **PASS** |
| **Internal dashboard i18n** | ❌ **NOT STARTED** |
| **Dynamic DB translation** | **OUT OF SCOPE** (frontend-only task) |

Public static i18n is claimed as PASS on the strength of: 0 public-gating hardcode findings,
0 issues from `audit-i18n.mjs`, and 38 passing runtime tests that exercise validation, toasts,
API errors, the Google SSO button, and Excel upload in both languages.

**Full frontend i18n remains PARTIAL**, because the dashboard is untouched — `--all` reports
3,868 hardcoded strings across 75 `pages/dashboard` files, plus the 57 in
`components/home/internal/*`. The dashboard has no `useTranslation` wiring at all. It was
audited and quantified, not fixed; per §5 of the prompt it is a separate phase and was not
mixed into this one.

---

## 8. Definition of Done

| DoD item | Status |
|---|---|
| 23 Excel upload strings are i18n'd | ✅ Met — 23 → 0 |
| `audit-hardcode.mjs` has no public-gating finding | ✅ Met — 0 of 75 gate the public verdict |
| EN mode has no static Vietnamese in public UI | ✅ Met — asserted by Playwright (diacritic regex + the specific banned words) |
| Playwright public runtime tests pass | ✅ Met — 38/38 |
| No SQL / backend / database change | ✅ Met — `git status` is frontend + docs only |

---

## 9. Suggested next phase (not done here)

Internal dashboard i18n, frontend-only, by module: `dashboardCommon`, `visitorDashboard`,
`staffDashboard`, `staffLeaderDashboard`, `departmentDashboard`, `hoDashboard`,
`adminDashboard`, `visitManagement`, `reportPages`, `accountManagement`, `faqManagement`,
`newsManagement`, `galleryManagement`, `partnerManagement`.

Start with the shared table / modal / confirm-dialog / status-badge components — one fix there
covers many screens. The shared infrastructure those screens depend on is already localized
(`shared/utils/toast.ts`, `authError.ts`, `passwordPolicy.ts`, and the `validation`, `toast`,
`errors.api` namespaces), so the phase starts from a clean base.
