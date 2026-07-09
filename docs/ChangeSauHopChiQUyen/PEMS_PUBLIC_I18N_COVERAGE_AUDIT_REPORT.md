# PEMS Public i18n Coverage Audit Report

> Generated: 2026-07-09 | Branch: Canh-Iter1
> Revision 2 — supersedes the 2026-07-09 rev-1 report, whose blanket
> **"Public i18n coverage: ✅ PASS"** verdict was **incorrect** and has been retracted.

> ⚠️ **Partly superseded by
> [`PEMS_FRONTEND_I18N_RUNTIME_AUDIT_FIX_REPORT.md`](./PEMS_FRONTEND_I18N_RUNTIME_AUDIT_FIX_REPORT.md)
> (same day, later).** This rev-2 report was still a *static* audit and it missed every defect
> that only appears after a user interaction. Three claims below are now known to be wrong:
>
> 1. **§0/§7 "no runtime test has ever been executed" / "Playwright is not installed"** — Playwright
>    is now installed and 27 tests run and pass.
> 2. **§4 "No backend endpoint emits `errorCode`"** — false. The auth endpoints do, and
>    `authError.ts` already switched on 36 of them (in a Vietnamese-only map).
> 3. **§2 "auth pages ✅ localized"** — false. `LoginPage`, `ForgotPasswordPage`,
>    `ResetPasswordPage` and `ChangePasswordPage` were almost entirely hardcoded Vietnamese.
>    `scripts/audit-hardcode.mjs` had been told to *skip* `pages/auth` and
>    `features/authentication` on the false assumption they were "already audited".
>
> Sections §1, §3, §5 (mojibake, namespace registration, the regression gate) remain accurate.

---

## 0. Verdict

| Scope | Verdict |
|---|---|
| **Static UI i18n** (public routes: keys wired, no raw keys, no mojibake) | ✅ **PASS** |
| **Locale key parity** (VI ⇄ EN, both directions, no empty/type-mismatch) | ✅ **PASS** |
| **Full public bilingual experience** (incl. dynamic DB content + runtime proof) | ⚠️ **PARTIAL / CONDITIONAL PASS** |

The third line cannot be a PASS while:

1. **Dynamic DB content** for FAQ / Partners / Gallery has no translation storage or
   `languageCode` plumbing in the backend (§6). EN visitors see Vietnamese DB text.
2. **No runtime i18n test has ever been executed.** Playwright is not installed; the
   smoke spec added in this session is committed but **has not been run** (§7).
3. **46 hardcoded Vietnamese strings remain** on the anonymous public visit-request
   form (§8).

Static UI i18n is claimed as PASS on the strength of an offline gate that now fails the
build on regression (`scripts/audit-i18n.mjs`, §5) — not on manual inspection.

---

## 1. Why rev-1's PASS was wrong

Rev-1 concluded PASS with only two "non-blocking" caveats (empty `toast.*`, dynamic DB
fallback). Re-auditing the actual source found **six defects that rev-1 missed entirely**,
three of which were user-visible on every page load.

| # | Defect | Impact | Rev-1 said |
|---|---|---|---|
| 1 | `loginModal`, `search`, `visitFptu` namespaces present as locale files but **never registered** in `config.ts` (neither `resources` nor `ns`) | **125 `t()` calls rendered a bare key segment**: `t('loginModal:title')` → literally `title`. Affected the login modal, search popup, and the whole Visit FPTU gallery, in **both** languages | "✅ Fully localized" for all three |
| 2 | **130 mojibake entries** across 7 VI locale files, committed in HEAD | VI users saw `Ch�ng t�i c� th? gi�p g� cho b?n?` instead of `Chúng tôi có thể giúp gì cho bạn?`. `faq` was 26/26 corrupt, `news` 31/32, `partners` 40/46 | "✅ Fully localized" |
| 3 | 5 keys `search:contacts*Address` referenced but absent | Search popup rendered the literal text `contactsHanoiAddress` as the campus address | Not detected |
| 4 | 2 keys referenced but absent: `publicLayout:footer.hqAddress`, `loginModal:googleMissingClientId` | Footer address blank/raw; login error fell back to a hardcoded VI string | "No raw keys detected" |
| 5 | `partners.json` (VI) declared `noMatchTitle` **twice** — a silent JSON key collision | Last-wins; the corrupt duplicate won | Not detected |
| 6 | `googleMissingClientId` fallback string leaked the env var name `VITE_GOOGLE_CLIENT_ID` to public users | Violates requirements §15 (no internal/debug detail in public errors) | Not detected |

Root cause of #1 and #2: an unregistered namespace does **not** fail loudly. i18next strips
the `ns:` prefix and returns the last key segment, so the page still renders and looks
plausible. And because those three namespaces were never loaded, nobody ever saw that their
VI content was also mojibake. A static-reading audit cannot catch either class of bug —
which is why both are now enforced by a script (§5).

---

## 2. Public routes

| Route | Component | Static UI | Notes |
|---|---|---|---|
| `/` | `HomePage` | ✅ | `faqPreview` / `galleryPreview` hardcoded VI — **fixed this session** |
| `/news`, `/news/:id` | `NewsPage`, `NewsDetailPage` | ✅ | `languageCode` now sent (§6) |
| `/partners`, `/partners/:id` | `PartnersPage`, `PartnerDetailPage` | ✅ | dynamic content still VI-only |
| `/faq` | `FAQPage` | ✅ | dynamic content still VI-only |
| `/visit-fptu`, `/visit-fptu/:id` | `VisitFPTUPage`, `CampusDetailVisitPage` | ✅ | namespace was unregistered — **fixed** |
| `/login`, `/forgot-password`, `/reset-password` | auth pages | ✅ | |
| `/403`, `/invalid-account`, `*` | error pages | ✅ | |
| Global | `Header`, `Footer` | ✅ | |
| Modal | `SearchPopup` | ✅ | namespace was unregistered — **fixed** |
| Modal | `LoginModal` + `DualPortalLoginForms` | ✅ | namespace was unregistered — **fixed** |
| Modal | `VisitingFormPopup` + form sections | ⚠️ | 46 hardcoded VI strings remain (§8) |

`components/home/internal/*` renders only for authenticated non-VISITOR users
(`HomePage.tsx:18`) and is therefore **out of the anonymous public scope**; its 57
hardcoded strings are tracked in §8 but do not gate the public verdict.

---

## 3. Locale key parity

`node scripts/audit-i18n.mjs` — 14 namespaces, **0 issues**, exit code 0.

Namespaces: `common`, `errors`, `faq`, `gallery`, `home`, `loginModal`, `news`, `partners`,
`publicLayout`, `search`, `toast`, `validation`, `visitFptu`, `visitRequest`.

`common`, `gallery`, `validation` remain intentionally empty `{}` (both locales, so parity
holds). `validation` is unused because Zod schemas call `t()` against the `visitRequest`
namespace; `gallery` is superseded by `visitFptu`.

---

## 4. Toast coverage

**`toast.*` is no longer empty.** VI and EN now define `common.*`, `http.*`, `mask.*`, and
`visitRequest.*`.

Rev-1 claimed "toast messages in feature hooks call raw Vietnamese strings" and implied this
affected public pages. That is **half right**, and the correction matters:

- A search for `react-hot-toast` / `sonner` / `toast(` / `toast.success` / `toast.error`
  across `src/` returns **33 files. Not one of them is on a public route.** Every toast call
  site is under `pages/dashboard/**` or `features/**` behind auth. The public visit-request
  form surfaces errors through inline state and an `onError` callback, never a toast.
- The one piece of toast machinery reachable from public code is the shared helper
  `src/shared/utils/toast.ts`, which hardcoded Vietnamese HTTP-status, network, default-error
  and secret-mask strings. **That file is now fully i18n-driven**, resolving messages at call
  time (not module-load time) so a language switch takes effect immediately.

Also added to the helper, per requirements §8.1:

- `errorCode` → `errors:api.<CODE>` lookup, guarded by `i18n.exists()` so no error code is
  invented. **No backend endpoint emits `errorCode` today**, so this path is currently inert
  and is listed as a backend task (§6).
- In EN mode a raw Vietnamese backend `message` is **suppressed** in favour of the localized
  HTTP-status message, so the public UI never mixes Vietnamese into an English screen.

**DoD "no raw Vietnamese toast in public-related code": met** — by localizing the shared
helper, and because no public route calls `toast` at all.

---

## 5. Regression gate (new)

`scripts/audit-i18n.mjs` previously checked key parity only and **always exited 0**. It now
also fails (exit 1) on:

1. **Mojibake** — any `U+FFFD`, or a `?` wedged between two letters (`Tin t?c`), in either locale.
2. **Unregistered namespaces** — a locale file that `config.ts` does not wire into both
   `resources` and `ns`.
3. **Unresolved call sites** — every literal `t('ns:key')` in `src/` must resolve in VI *and* EN.

Verified against pre-fix `HEAD`: the mojibake guard flags 25 entries in `vi/faq.json` and the
namespace guard flags `["loginModal","search","visitFptu"]`. Both defects would now break the
build. `config.ts` additionally logs missing keys to the console in dev (`saveMissing`).

---

## 6. Dynamic DB content — remaining **backend** task

Frontend translates static UI; backend must serve translated content. Current state, verified
against source (not assumed):

| Module | Translation table | API `languageCode` | Frontend sends it | Status |
|---|---|---|---|---|
| **News** | ✅ `news_translations` | ✅ `ViewNewsQuery.LanguageCode`, `ViewPublicNewsDetailQuery` | ✅ list **wired this session**; detail already wired | ✅ Works end-to-end |
| **FAQ** | ❌ none | ❌ `ViewFaqQuery(Keyword, FaqType, Page, PageSize)` | n/a | ❌ **Backend task** |
| **Partners** | ❌ none | ❌ `PublicPartnersController` has no lang param | n/a | ❌ **Backend task** |
| **Gallery / Visit FPTU** | ❌ none | ❌ `PublicVisitFptuController` has no lang param | n/a | ❌ **Backend task** |

> Rev-1 stated *"Backend FAQ endpoint supports `lang` param"*. **This is false.**
> `ViewFaqQuery` accepts only `Keyword`, `FaqType`, `Page`, `PageSize`. Corrected here.

`Accept-Language` **is** already attached to every request by `httpClient.ts:11-15`, so the
backend has the signal available for error messages and content negotiation whenever it is
ready to use it.

Consequence: **in EN mode, FAQ questions/answers, partner descriptions, and gallery
area/location names still render in Vietnamese.** This is a data/backend gap, not a frontend
defect — and it is exactly why the full bilingual verdict is PARTIAL rather than PASS.

Remaining backend work:
1. Add translation storage for FAQ / Partner / Gallery (a `*_translations` table each, or a
   shared `content_translations`), mirroring `news_translations`.
2. Accept `languageCode` on the public FAQ / Partners / Visit-FPTU queries.
3. Fall back to VI when a translation is absent; do not return null. Consider
   `translationMissing: true` on the DTO.
4. Emit a stable `errorCode` on public error responses so the frontend `errors:api.<CODE>`
   map (already implemented) can localize them.
5. Guarantee `languageCode` cannot widen visibility (no draft/hidden content leaking).

---

## 7. Runtime tests — **added, NOT executed**

The project has **no Playwright and no Cypress**, and `tests/` was empty. Per the request, a
proposed spec is committed rather than skipped:

- `frontend/pems-react/tests/i18n-smoke.spec.ts`
- `frontend/pems-react/tests/README.md` — how to enable it

**It has never been run.** `@playwright/test` is not installed, so `tests/` is excluded from
`tsconfig.json` to keep `npm run lint` green.

It asserts English static chrome per public route, absence of raw i18n keys, absence of
mojibake in VI, and language persistence across reload. It deliberately does **not** assert
that the whole page body is Vietnamese-free in EN mode, because that would fail on the §6
backend gap rather than on a frontend defect.

---

## 8. Remaining hardcoded Vietnamese

`node scripts/audit-hardcode.mjs` → 117 findings, classified:

| Class | Count | Gates public verdict? |
|---|---:|---|
| **Anonymous public — visit-request form** | **46** | ⚠️ **Yes** |
| Auth-only (`components/home/internal/*`) | 57 | No — not anonymous-public |
| Lookup data (`countryFlag`/`countryMatch` VI-keyed maps) + language-switcher labels | 9 | No — data / intentional |
| Dead defaults (`SearchPopup` `city:` constants, always overridden by `t()` before render) | 5 | No — never rendered |

The 46 that matter, all reachable from the public "Đăng ký tham quan" form:

| File | Count |
|---|---:|
| `features/visit-request/components/ExcelUpload/excelValidator.ts` | 17 |
| `features/visit-request/components/ExcelUpload/excelDownload.ts` | 6 |
| `features/visit-request/components/shared/PartnerAsyncSelect.tsx` | 5 |
| `features/visit-request/components/shared/PartnerOrgCombobox.tsx` | 5 |
| `features/visit-request/components/shared/OrganizationCombobox.tsx` | 4 |
| `features/visit-request/components/shared/OrganizationSelect.tsx` | 4 |
| `features/visit-request/components/shared/CountrySelect.tsx` | 3 |
| `features/visit-request/components/shared/PhoneInput.tsx` | 2 |

Mostly combobox placeholders, "Đang tìm kiếm…" / "Không tìm thấy kết quả" states, and Excel
row-validation messages plus template column headers. Until these are keyed, **an EN user
filling in the public visit-request form still sees Vietnamese**.

---

## 9. Files changed this session

| File | Change |
|---|---|
| `src/shared/i18n/config.ts` | Register `loginModal`, `search`, `visitFptu`; dev missing-key warning; document the silent-failure mode |
| `src/shared/i18n/locales/vi/{faq,loginModal,search,news,partners}.json` | Rewritten — mojibake repaired |
| `src/shared/i18n/locales/vi/{publicLayout,visitRequest}.json` | Corrupt blocks repaired (44 keys in `visitRequest`) |
| `src/shared/i18n/locales/en/{news,search,visitRequest}.json` | Repaired `©`, `→`, `·` glyphs |
| `src/shared/i18n/locales/{vi,en}/toast.json` | `{}` → `common.*`, `http.*`, `mask.*`, `visitRequest.*` |
| `src/shared/i18n/locales/{vi,en}/search.json` | + 5 `contacts*Address` keys (recovered from git history) |
| `src/shared/i18n/locales/{vi,en}/publicLayout.json` | + `footer.hqAddress` |
| `src/shared/i18n/locales/{vi,en}/loginModal.json` | + `googleMissingClientId` (no env-var leak) |
| `src/shared/i18n/locales/{vi,en}/home.json` | + `faqPreview.*`, `galleryPreview.*` |
| `src/shared/utils/toast.ts` | i18n-driven messages; `errorCode` mapping; EN-mode VI-message suppression |
| `src/pages/NewsPage.tsx` | Send `languageCode` on all 6 public news calls; refetch on language switch |
| `src/components/home/FaqPreviewSection.tsx` | Hardcoded VI → `t('home:faqPreview.*')` |
| `src/components/home/GalleryPreviewSection.tsx` | Hardcoded VI → `t('home:galleryPreview.*')` |
| `src/pages/CampusDetailVisitPage.tsx` | `title="Đóng"` → `t('visitFptu:gallery.actions.close')` |
| `src/components/layout/ErrorBoundary.tsx`, `src/pages/InvalidAccountPage.tsx`, `src/pages/PartnersPage.tsx`, `src/features/authentication/components/DualPortalLoginForms.tsx` | Stripped 18 hardcoded VI `defaultValue` fallbacks |
| `scripts/audit-i18n.mjs` | + mojibake / namespace-registration / call-site checks; exits non-zero |
| `tests/i18n-smoke.spec.ts`, `tests/README.md` | **New** — proposed, not executed |
| `tsconfig.json` | `exclude: [dist, node_modules, tests]` |

No backend, SQL, route, business-logic, enum, or layout changes.

---

## 10. Verification

| Check | Result |
|---|---|
| `npm run build` | ✅ exit 0 — built in 55.77s |
| `npm run lint` (`tsc --noEmit`) | ✅ exit 0 — no TypeScript errors |
| `node scripts/audit-i18n.mjs` | ✅ exit 0 — 0 issues across 14 namespaces (parity, mojibake, namespace registration, 480 call sites) |
| `node scripts/audit-hardcode.mjs` | ⚠️ exit 0 — 117 findings; 46 on the anonymous public surface (§8) |
| Regression guards vs pre-fix `HEAD` | ✅ mojibake guard flags 25 entries; namespace guard flags 3 namespaces |
| Playwright / Cypress runtime i18n test | ❌ **not executed** — not installed (§7) |
| Manual browser test (VI/EN, mobile, reload persistence) | ❌ **not performed** in this session |
| `dotnet build` | n/a — backend unchanged |

---

## 11. Definition of Done

| DoD item | Status |
|---|---|
| No raw Vietnamese toast in public-related code | ✅ Met — shared helper localized; no public route calls `toast` (§4) |
| EN mode has no static Vietnamese UI text | ⚠️ **Not met** — 46 strings in the public visit-request form (§8) |
| Dynamic DB fallback classified, not counted as frontend PASS | ✅ Met — §6, backend task, verdict held at PARTIAL |
| Verdict is truthful; no full PASS while backend/runtime gaps remain | ✅ Met — §0 |

---

## 12. Recommended next steps, in priority order

1. Key the 46 remaining strings in the public visit-request form (§8) — the last blocker for
   "EN mode has no static Vietnamese UI text".
2. Install Playwright and actually run `tests/i18n-smoke.spec.ts` (§7).
3. Backend: translation storage + `languageCode` for FAQ / Partners / Gallery (§6).
4. Backend: emit `errorCode` on public error responses; populate `errors:api.*`.
5. Wire `node scripts/audit-i18n.mjs` into CI / pre-commit — it now exits non-zero.
6. Localize `components/home/internal/*` when the authenticated UI is internationalized.
