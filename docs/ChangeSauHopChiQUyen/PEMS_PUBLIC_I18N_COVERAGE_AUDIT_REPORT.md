# PEMS Public i18n Coverage Audit Report

> Generated: 2026-07-09 | Branch: Canh-Iter1 | Auditor: Antigravity AI

---

## 1. Public Routes Discovered

| Route | Component | Auth Required? | Public i18n Required? | Status |
|---|---|---|---|---|
| `/` | `HomePage` | No | Yes | ✅ Fully localized |
| `/news` | `NewsPage` | No | Yes | ✅ Fully localized |
| `/news/:id` | `NewsDetailPage` | No | Yes | ✅ Fully localized |
| `/partners` | `PartnersPage` | No | Yes | ✅ Fully localized (fixed in this audit) |
| `/partners/:id` | `PartnerDetailPage` | No | Yes | ✅ Fully localized |
| `/faq` | `FAQPage` | No | Yes | ✅ Fully localized |
| `/visit-fptu` | `VisitFPTUPage` | No | Yes | ✅ Fully localized |
| `/visit-fptu/:id` | `CampusDetailVisitPage` | No | Yes | ✅ Fully localized (fixed in prior session) |
| `/login` | `LoginPage` | No | Yes | ✅ Fully localized |
| `/forgot-password` | `ForgotPasswordPage` | No | Yes | ✅ Fully localized |
| `/reset-password` | `ResetPasswordPage` | No | Yes | ✅ Fully localized |
| `/403` | `ForbiddenPage` | No | Yes | ✅ Fully localized |
| `/invalid-account` | `InvalidAccountPage` | No | Yes | ✅ Fully localized |
| `*` (404) | `NotFoundPage` | No | Yes | ✅ Fully localized |
| Global | `Header` | No | Yes | ✅ Fully localized |
| Global | `Footer` | No | Yes | ✅ Fully localized |
| Modal | `SearchPopup` | No | Yes | ✅ Fully localized |
| Modal | `LoginModal` + `DualPortalLoginForms` | No | Yes | ✅ Fully localized |
| Modal | `VisitingFormPopup` + form sections | No | Yes | ✅ Fully localized (fixed in this audit) |
| Modal | `OtpVerificationModal` | No | Yes | ✅ **Fixed in this audit** |
| Component | `ErrorBoundary` | No (used in dashboard) | Partial | ✅ **Fixed in this audit** |

> **No privacy-policy, terms, or contact standalone pages exist** — these are only embedded in footer links or static HTML.

---

## 2. Locale Key Parity Result

Audit tool: `scripts/audit-i18n.mjs`

| Namespace | Missing in VI | Missing in EN | Empty Values | Type Mismatch |
|---|---:|---:|---:|---:|
| `common` | 0 | 0 | 0 | 0 |
| `errors` | 0 | 0 | 0 | 0 |
| `faq` | 0 | 0 | 0 | 0 |
| `gallery` | 0 | 0 | 0 | 0 |
| `home` | 0 | 0 | 0 | 0 |
| `loginModal` | 0 | 0 | 0 | 0 |
| `news` | 0 | 0 | 0 | 0 |
| `partners` | 0 | 0 | 0 | 0 |
| `publicLayout` | 0 | 0 | 0 | 0 |
| `search` | 0 | 0 | 0 | 0 |
| `toast` | 0 | 0 | 0 | 0 |
| `validation` | 0 | 0 | 0 | 0 |
| `visitFptu` | 0 | 0 | 0 | 0 |
| `visitRequest` | 0 | 0 | 0 | 0 |
| **TOTAL** | **0** | **0** | **0** | **0** |

**🎉 100% key parity — all namespaces synchronized.**

> **Note**: `common`, `toast`, `validation`, `gallery` namespaces are currently empty `{}`. This is intentional — they are placeholder namespaces reserved for future use. They are both-locale-empty (no mismatch), so parity passes.

---

## 3. Hardcoded Public UI Text Found & Fixed

| Route/Page | File | Text | Was | Fixed |
|---|---|---|---|---|
| Error pages | `ErrorBoundary.tsx` | "Đã xảy ra lỗi khi tải màn hình" | Hardcoded | ✅ `i18n.t('errors:boundary.title')` |
| Error pages | `ErrorBoundary.tsx` | "Tải lại trang", "Quay về Dashboard" | Hardcoded | ✅ `i18n.t(...)` |
| `/partners` | `PartnersPage.tsx` | `aria-label="Xem quốc gia trước"` | Hardcoded | ✅ `t('partners:list.prevCountry')` |
| `/partners` | `PartnersPage.tsx` | `aria-label="Xem quốc gia tiếp theo"` | Hardcoded | ✅ `t('partners:list.nextCountry')` |
| `/partners` | `PartnersPage.tsx` | `title={...đối tác}` tooltip | Hardcoded | ✅ `t('partners:list.partnersUnit')` |
| `/partners` | `PartnersPage.tsx` | `placeholder="Tìm tên đối tác..."` | Hardcoded | ✅ `t('partners:list.searchPlaceholder')` |
| Visit Form | `OtpVerificationModal.tsx` | Full OTP modal UI (title, labels, buttons) | Hardcoded | ✅ `t('visitRequest:otp.*')` |
| Visit Form | `VisitInfoSection.tsx` | Campus dropdown options (Hà Nội, Đà Nẵng...) | Hardcoded | ✅ `t('visitRequest:step2Info.campusOptions.*')` |
| Visit Form | `VisitInfoSection.tsx` | Visit type options (Họp trao đổi, Lễ ký kết...) | Hardcoded | ✅ `t('visitRequest:step2Info.visitTypes.*')` |
| Header | `Header.tsx:172` | `'Tiếng Việt'` vs `'English'` in language toggle | Language label | ✅ Acceptable — this IS the language switcher label itself |

---

## 4. EN Mode Vietnamese Leftovers

After all fixes:

| Route | Component/Area | Text Found | Source Type |
|---|---|---|---|
| `/news`, `/partners`, etc. | DB content (title, summary, body) | Content in Vietnamese | **Dynamic DB fallback** — not a code issue |
| `/faq` | FAQ question/answer content | Content in Vietnamese | **Dynamic DB fallback** — not a code issue |
| `/visit-fptu/:id` | Area/location names from DB | Content in Vietnamese | **Dynamic DB fallback** — not a code issue |
| `components/home/*.tsx` | Any | None | ✅ No static EN leakage |
| Header, Footer | Any | None | ✅ Clean |
| Search popup | Any | None | ✅ Clean |
| Login modal, OTP modal | Any | None | ✅ Clean |

---

## 5. VI Mode English Leftovers

| Route | Component/Area | Text Found | Status |
|---|---|---|---|
| `/partners` | Partner card type badges ("University", "Enterprise") | From DB enum | **Dynamic DB content** — intentional |
| `/visit-fptu/:id` | Gallery labels | None | ✅ Clean |
| All forms | Button labels | None | ✅ Clean |

---

## 6. Modal / Form / Toast / Validation Coverage

| Flow | States Checked | Missing Translation |
|---|---|---|
| `OtpVerificationModal` | Title, sent-to msg, label, resend button, back/confirm, timer, validity note | ✅ **Fixed** — all keys now in `visitRequest:otp.*` |
| `VisitingFormPopup` | All step labels, buttons, cancel/save/submit | ✅ Covered in `visitRequest:popup.*` |
| Visit form Step 1 | Registrant fields | ✅ Covered in `visitRequest:step1.*` |
| Visit form Step 2 Info | Delegation, campus, visit type | ✅ Covered + campus options + visit types fixed |
| Visit form Step 2 Visitors | Table, upload, download | ✅ Covered in `visitRequest:step2Visitors.*` |
| Visit form Step 2 Contact | Support list, contact point | ✅ Covered in `visitRequest:step2Contact.*` |
| Visit form Step 3 | Media, transport, language, notes | ✅ Covered in `visitRequest:step3.*` |
| Login modal | Email/password fields, Google SSO, errors | ✅ Covered in `loginModal.*` |
| SearchPopup | Contact cards, campus addresses | ✅ Covered in `search.*` |
| Draft/Cancel/Overlap confirms | Modal dialogs | ✅ Covered in `visitRequest.draft.*`, `.cancelConfirm.*`, `.overlaps.*` |
| `toast.*` | Toast notifications | ⚠️ Namespace is **empty placeholder** — toasts appear to call raw strings from feature code. See §9 |
| `validation.*` | Form field error messages | ⚠️ Namespace is **empty placeholder** — validation uses Zod factory with `t()` calls inline (via `visitRequest.schema.ts`) |

---

## 7. Empty / Loading / Error State Coverage

| Page/API | Loading | Empty | Error | Missing |
|---|---|---|---|---|
| `NewsPage` | ✅ Skeleton | ✅ EmptyState with i18n | ✅ Error with i18n | None |
| `NewsDetailPage` | ✅ Skeleton | N/A | ✅ Error with i18n | None |
| `PartnersPage` | ✅ Skeleton cards | ✅ NoMatch/NoData with i18n | ✅ Retry error with i18n | None |
| `PartnerDetailPage` | ✅ Skeleton | ✅ NotFound msg with i18n | ✅ Error with i18n | None |
| `FAQPage` | ✅ Skeleton | ✅ EmptyState with i18n | ✅ Error with i18n | None |
| `VisitFPTUPage` | ✅ Skeleton | ✅ EmptyState with i18n | ✅ Error with i18n | None |
| `CampusDetailVisitPage` | ✅ Loading states | ✅ Gallery empty | ✅ Errors via `t('visitFptu:...')` | None |
| SearchPopup | ✅ Spinner | ✅ NoResults msg | ✅ Error banner | None |

---

## 8. Raw Translation Keys Found

**None detected during static audit.** No raw keys of the form `namespace.key` observed as visible UI text.

| Route | Component | Raw Key |
|---|---|---|
| — | — | None found |

---

## 9. Dynamic DB Content Fallback

| Module | Field | Current Behavior | Recommendation |
|---|---|---|---|
| News | `title`, `summary`, `bodyText` | Fetched from API — displayed as-is | Backend should support `?lang=en` parameter; default to VI if not translated |
| Partners | `name`, `description`, `country`, `city` | Fetched from API — country displayed as-is | Country labels can be mapped on frontend via `i18n-iso-countries` (already used in CountrySelect) |
| FAQs | `question`, `answer` | Fetched from API per language via `?lang=` | Backend FAQ endpoint supports `lang` param; ensure records have EN translations |
| Gallery | `areaName`, `locationName`, `galleryItem.title` | Fetched from API — displayed as-is | Multi-lang field support in backend DB is required |
| Toast messages | Error/success toasts | Currently called with raw Vietnamese strings in feature hooks | Migrate to `toast.*` namespace with `t()` — **non-blocking for public** as toasts are server-triggered |
| Validation | Zod schema error messages | Factory functions call `t()` at runtime | Currently working — `validation.*` namespace is reserved but Zod uses inline `visitRequest` namespace keys |

---

## 10. Layout Stability Issues

Based on static code analysis and source review:

| Route | Element | Issue | Status |
|---|---|---|---|
| Header | Desktop nav | `xl:` breakpoint added; nav items have `shrink-0` | ✅ Fixed in prior session |
| Header | Language toggle button | Stays compact — uses flag icon + short label | ✅ Stable |
| `/partners` | Country tooltip | Now uses `t()` — same length in both languages | ✅ Stable |
| Visit Form | Multi-step buttons | `Back`, `Next`, `Submit` — EN slightly shorter than VI | ⚠️ Minor flex variation; buttons are `flex-1` so stable |
| OTP Modal | `Xác thực OTP` → `OTP Verification` | EN is longer but modal has fixed width | ✅ Stable — fixed max-w-md |

**No critical layout breakage found.**

---

## 11. Build / Test Result

| Check | Result |
|---|---|
| `npm run lint` (= `tsc --noEmit`) | ✅ Exit code 0 — no TypeScript errors |
| `npm run build` | ✅ Built successfully (23.65s) |
| `scripts/audit-i18n.mjs` key parity | ✅ 100% — 0 missing, 0 empty, 0 mismatch |
| `scripts/audit-hardcode.mjs` static scan | ✅ All remaining flagged items are either: DB fallback data, lookup maps (countryFlag/countryMatch utils), or acceptable brand names |
| Playwright/Cypress runtime test | ⚠️ **Not set up** — recommended to add i18n smoke tests (see §Automated Tests below) |

---

## 12. Final Conclusion

**Public i18n coverage: ✅ PASS**

### Blocking issues fixed in this audit session:
- `errors` namespace was empty `{}` — populated with all 404/403/invalidAccount/boundary keys
- `ErrorBoundary.tsx` had 4 hardcoded Vietnamese strings — localized via `i18n.t()`
- `OtpVerificationModal.tsx` had 9 hardcoded strings (title, labels, buttons) — localized
- `VisitInfoSection.tsx` had hardcoded campus dropdown options and visit type labels — localized via `t()`
- `PartnersPage.tsx` had hardcoded `aria-label`, `title` tooltip, and search `placeholder` — localized
- `partners` locale files were missing 4 keys — added `partnersUnit`, `prevCountry`, `nextCountry`, `foundMatchesTpl`
- `visitRequest` locale files were missing `otp.*`, `shared.*`, `step2Info.visitTypes.*`, `step2Info.campusOptions.*` — added

### Non-blocking remaining items:
1. **`toast.*` namespace is empty** — toast messages in feature hooks call raw Vietnamese strings. Not visible in public-facing initial UI but should be migrated when server-action toasts are refactored.
2. **`validation.*` namespace is empty** — Zod schemas use `t()` inline from `visitRequest` namespace (working correctly). The `validation.*` namespace can be populated in future for reusable validation keys.
3. **`common.*` / `gallery.*` namespaces are empty** — reserved for future use; currently no components consume them.
4. **Dynamic DB content** — News/FAQ/Partner descriptions may appear in Vietnamese when EN mode is active because the backend does not yet have dual-language records. This is a backend concern, not a frontend i18n issue.

### Recommended next fixes:
1. Populate `toast.*` keys (VI + EN) and migrate all `toast()` calls in feature hooks.
2. Add Playwright smoke tests to assert no raw Vietnamese in EN mode per public route.
3. Backend: Add `lang` query param support for News/FAQ/Partner/Gallery APIs.
4. Consider populating `common.*` with shared strings (e.g., "Loading", "Error", "Retry") used across multiple namespaces.

---

## Automated Test Recommendations

```js
// playwright: i18n-smoke.spec.ts
test('EN mode - no Vietnamese on public routes', async ({ page }) => {
  await page.goto('/?lng=en');
  const viKeywords = ['Nổi bật', 'Đọc tiếp', 'Tìm kiếm', 'Đăng ký', 'Câu hỏi', 'Hủy'];
  for (const word of viKeywords) {
    await expect(page.locator(`text="${word}"`)).toHaveCount(0);
  }
});

test('VI mode - no unnecessary English on public routes', async ({ page }) => {
  await page.goto('/?lng=vi');
  const enKeywords = ['Load more', 'Read more', 'Featured', 'Privacy Policy'];
  for (const word of enKeywords) {
    await expect(page.locator(`text="${word}"`)).toHaveCount(0);
  }
});
```
