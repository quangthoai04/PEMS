# PEMS Visitor i18n Coverage Audit Report

> Generated: 2026-08-16 | Branch: Canh_iter3_FixBug
> Scope: every route the `VISITOR` role can reach — anonymous public pages **and**
> the authenticated "my visit" dashboard journey (`/dashboard/visit/**`, `/dashboard/profile`, ...).
> This is a superset of the earlier public-only audits in this folder
> ([`PEMS_PUBLIC_I18N_COVERAGE_AUDIT_REPORT.md`](./PEMS_PUBLIC_I18N_COVERAGE_AUDIT_REPORT.md),
> [`PEMS_PUBLIC_ROUTES_I18N_AUDIT_REPORT.md`](./PEMS_PUBLIC_ROUTES_I18N_AUDIT_REPORT.md)), which
> only covered pre-login routes and are now several weeks stale.

---

## 0. Verdict

| Scope | Verdict |
|---|---|
| **Locale JSON parity** (VI ⇄ EN, all 19 namespaces) | ✅ **PASS** — 0 missing keys either direction, 0 mojibake, 0 empty values |
| **Namespace registration** (`config.ts` resources + ns) | ✅ **PASS** — all 19 locale files registered correctly |
| **Public / anonymous routes** (home, news, partners, visit-fptu, faq, legal, auth, public registration form) | ✅ **PASS** (minor exceptions, §4) |
| **Authenticated VISITOR "manage my visit" journey** (`/dashboard/visit/**`, profile, feedback) | ❌ **FAIL** — effectively 0% i18n coverage |
| **Overall: "100% translated for visitor"** | ❌ **NOT MET** |

The front door (marketing pages + the public visit-registration form) is well localized. Everything
a VISITOR sees **after** registering/logging in — which is where they spend most of their time
tracking their own request — is almost entirely hardcoded Vietnamese.

---

## 1. Methodology

1. Resolved the exact route set a `VISITOR` can reach from `frontend/pems-react/src/App.tsx` (public
   `<Route>` list) and `frontend/pems-react/src/shared/auth/dashboardRouteAccess.ts` (the single
   source of truth for `/dashboard/**` role access — `VISITOR` is in `allowedRoles` for `VISIT_LIST`,
   `VISIT_CREATE`, `VISIT_DETAIL`, `VISIT_EDIT`, `VISIT_PROCESS`, `VISIT_INVITATION`,
   `VISIT_CONTACT_INVITATIONS`, `VISIT_FEEDBACK`, `PROFILE`, `DASHBOARD_HOME`).
2. Verified `frontend/pems-react/src/shared/i18n/config.ts` namespace registration against the 19
   files under `src/shared/i18n/locales/{en,vi}/`.
3. Wrote a script to flatten every locale JSON and diff EN vs VI key sets (parity, mojibake regex
   `�|[A-Za-zÀ-ỹ]\?[A-Za-zÀ-ỹ]`, empty-string values).
4. Wrote a script that statically extracts every literal `t('ns:key')` / `Trans i18nKey="ns:key"`
   call across the frontend (528 files, 1259 calls) and resolves each key path against the real VI
   locale JSON, flagging any that don't resolve.
5. For every file on the VISITOR-reachable route list, checked for `useTranslation` usage and ran a
   heuristic scan for lines containing Vietnamese-diacritic text that are not comments and not
   already passed through `t()`/`Trans`.
6. Manually read every flagged file to confirm true positives (excluding JSX comments, i18next
   pluralization suffixes `_one`/`_other`, and intentional same-text loanwords like "Email"/"Video").
7. Spot-checked backend controllers for `LanguageCode` support on public content endpoints (FAQ,
   Partners, Visit FPTU gallery).

---

## 2. What is covered

- **Locale JSON**: all 19 namespaces (`common`, `publicLayout`, `home`, `news`, `partners`, `faq`,
  `gallery`, `visitRequest`, `visitRequestV2`, `validation`, `errors`, `toast`, `loginModal`,
  `search`, `visitFptu`, `notifications`, `visitFaceScan`, `files`, `legal`) are key-parity clean and
  correctly registered in `config.ts`. The mojibake/unregistered-namespace defects from the
  2026-07-09 audits are fixed and have stayed fixed.
- **Public marketing pages**: `PublicHomePage`, `NewsPage`/`NewsDetailPage`, `PartnersPage`/
  `PartnerDetailPage`, `VisitFPTUPage`, `FAQPage`, `PrivacyPolicyPage`/`TermsOfServicePage`,
  `Header`/`Footer` — fully wired to `t()`.
- **Public visit-registration form**: `VisitRequestFormV2` and most of its `features/visit-request/**`
  subcomponents (`CampusVisitCard`, `OtpVerificationModal`, combobox/date/phone fields, Excel import,
  contact-link prompts) call `t()` throughout.
- **Backend**: `FAQ` and `Partners` public endpoints now carry `LanguageCode` and have translation
  tables (`FaqTranslation`, `PartnerTranslation`). This closes part of the gap the July audits flagged
  — at that time neither had any translation storage.

---

## 3. Critical gap: the authenticated "manage my visit" journey is ~0% i18n

Every file below is on a route `VISITOR` can reach post-login, and has **no functioning
`useTranslation`/`t()` usage** — either the import is absent, or it exists but is never called.

| File | What the visitor sees there | i18n state |
|---|---|---|
| `pages/dashboard/visit/VisitRequestManagement.tsx` | **VISITOR's default landing page** after login (`/dashboard/visit`, the route App.tsx redirects VISITOR to) | `useTranslation` imported but **never invoked** (dead import); ~100 hardcoded Vietnamese lines (tabs, status labels, action buttons) |
| `pages/dashboard/visit/VisitorVisitDetailPage.tsx` | The visitor's own visit-process/status view (rendered from `VisitProcess.tsx` when the viewer is the request's operational contact) | 617 lines, **0** `t()` calls; even hardcodes `date-fns` `locale: vi` |
| `pages/dashboard/profile/Profile.tsx` | Self-service profile (UC-14/UC-15) — used by **every role**, including VISITOR | 0 i18n, incl. validation messages ("Số điện thoại phải có từ 8 đến 15 chữ số...") |
| `features/feedbacks/components/VisitFeedbackModal.tsx` | Modal the visitor uses to rate their visit | 0 i18n |
| `pages/dashboard/visit/VisitFeedbackPage.tsx` | Feedback page wrapper | 0 i18n |
| `pages/dashboard/visit/VisitContributionPage.tsx` | Post-visit contribution view | 0 i18n |
| `pages/dashboard/visit/VisitProcessSummaryPage.tsx` | Process summary view | 0 i18n |
| `pages/dashboard/visit/VisitParticipantInvitationDetail.tsx` | Invitation detail (`/dashboard/visit/invitations/:id`) | 0 i18n |
| `pages/dashboard/visit/VisitRequestDetail.tsx` | Request detail sub-route | 0 i18n |
| `pages/dashboard/visit/CreateVisitRequestEntry.tsx` | Entry point for "create visit request" | 0 i18n |
| `pages/account/ConfirmEmailPage.tsx` | Public — email confirmation after signup | 0 i18n |
| `pages/identity/VisitContactInvitationPage.tsx` | Public — "operational contact" invitation landing, emailed to external visitors | 0 i18n |
| `components/layout/DashboardLayout.tsx` | Dashboard shell wrapping every screen above | Some hardcoded strings |

**Practical consequence:** a visitor who switches the site to English, then logs in to check their
visit request, lands directly on a 100% Vietnamese screen (`VisitRequestManagement`), and every
subsequent step (detail, process tracking, feedback, editing their profile) stays Vietnamese
regardless of language setting.

`VisitProcess.tsx` itself (2000+ lines, also 0 `t()` calls) is primarily a **staff/host** tool, but two
visitor-gated sections inside it ("3. Album ảnh", "4. Bài tin tức", shown when `isVisitor` is true for
a non-owner participant) are also hardcoded Vietnamese with no fallback.

---

## 4. Broken `t()` key references (verified against real locale JSON)

Extracted and resolved 1259 literal `t('ns:key')`/`Trans` calls; 9 didn't resolve, 2 were false
positives (i18next pluralization `_one`/`_other` suffixes exist and work correctly:
`visitRequestV2:changeBadges.pendingAmendment`, `.unreadCount`). **7 are real defects**, all on
visitor-facing screens:

| Key | File | Behavior |
|---|---|---|
| `validation:fixErrorsCount` | `features/visit-request/components/v2/VisitRequestFormV2.tsx:678` | **No fallback at all** — renders the literal text `"fixErrorsCount"` to the user in **both** languages, on the public registration form's error-summary banner |
| `visitRequest:otp.rateLimited.title` / `.desc` | `features/visit-request/components/OtpVerificationModal.tsx:206,209` | Missing from JSON; called with a hardcoded Vietnamese `defaultValue` — English-mode users still see Vietnamese |
| `visitRequestV2:card.collapse` / `.expand` | `features/visit-request/components/v2/CampusVisitCard.tsx:1073-1074` | Same pattern — collapse/expand button `aria-label`/`title` fall back to hardcoded "Thu gọn"/"Mở rộng" |
| `visitRequestV2:amend.titleUpdate` / `.submitUpdate` | `features/visit-request/components/VisitAmendmentSubmitModal.tsx:316,410` | Same pattern; only reachable when `campus.amendmentSelfApproves === true` (likely a Host/self-approve path, not confirmed VISITOR-reachable) |

`fixErrorsCount` is the most severe: it has zero fallback and sits on the public registration form,
which every visitor who mistypes a field will hit.

---

## 5. Minor: hardcoded fallback content on a public page

`pages/CampusDetailVisitPage.tsx` (public, `/visit-fptu/:id` — campus photo/video gallery):

- `CAMPUS_FALLBACK` constant (lines 63-86): 5 hardcoded Vietnamese hero descriptions (Hà Nội, HCM,
  Đà Nẵng, Cần Thơ, Quy Nhơn), used as fallback when the API doesn't supply one.
- Lines ~1486-1489: hardcoded Vietnamese count-label template literals (`"${n} nội dung"`,
  `"${n} hình ảnh"`, `"${n} hỗn hợp"`).

Neither goes through `t()`.

---

## 6. Backend translation status for public content

| Content | `LanguageCode` support | Notes |
|---|---|---|
| News | ✅ | `ViewNewsQuery`/`ViewPublicNewsDetailQuery`; frontend sends it on all 6 public calls |
| FAQ | ✅ (now fixed) | `ViewFaqQuery.LanguageCode` present; `FaqTranslation` entity exists |
| Partners | ✅ (now fixed) | `GetPublicPartnersQuery.LanguageCode` present; `PartnerTranslation` entity exists |
| Visit FPTU gallery (item title/description) | ❌ | `PublicVisitFptuController` only has `languageCode` on the **audio-narration** endpoint (`gallery-items/{id}/audio/{languageCode}`); gallery item title/description text itself has no translated variant |

---

## 7. Recommended priority

1. **`validation:fixErrorsCount`** — add the missing key to both locale files. Zero-risk, highest
   visibility (breaks in both languages on the most-used public form).
2. **`VisitRequestManagement.tsx`** — highest-traffic gap (VISITOR's default post-login page). Wire
   the already-imported `useTranslation` hook and move the hardcoded tab/status/action strings into
   `visitRequestV2`/`common`.
3. **`VisitorVisitDetailPage.tsx`, `Profile.tsx`, `VisitFeedbackModal.tsx`** — next-highest traffic;
   visitor touches these on every visit cycle.
4. The 6 broken `defaultValue`-fallback keys (§4) — cheap fixes, just add the missing JSON keys.
5. Remaining 0%-i18n dashboard visit pages (§3) and the `CampusDetailVisitPage` hardcoded fallback
   content (§5) — lower traffic, can follow.
6. Gallery item title/description translation storage (§6) — backend schema work, larger effort;
   track separately from frontend fixes.

---

## Appendix — automated checks used

- Locale parity + mojibake script: flattened every `en/*.json` vs `vi/*.json`, diffed key sets,
  regex-scanned VI values for mojibake, checked for empty strings. 0 issues found.
- `t()` key-resolution script: regex `\bt\(\s*(['"\`])([a-zA-Z0-9_]+):([a-zA-Z0-9_.]+)\1` and
  `i18nKey=(['"\`])([a-zA-Z0-9_]+):([a-zA-Z0-9_.]+)\1` across all `.ts`/`.tsx` under `src/`
  (excluding `__tests__`), resolved against flattened VI locale JSON. 1259 calls found, 7 confirmed
  broken (§4).
- Hardcode heuristic: per-file scan for Vietnamese-diacritic characters on non-comment lines not
  already containing `t(`/`Trans`/`i18nKey`, run against the full VISITOR-reachable file list
  resolved from `App.tsx` + `dashboardRouteAccess.ts`. Manually verified every flagged file listed
  in §3 and §5; discarded false positives (JSX `{/* ... */}` comments, i18next plural suffixes,
  intentional same-text loanwords such as "Email"/"Video"/"English"/"Reset").
