# PEMS Frontend i18n Runtime Audit & Fix Report

> Date: 2026-07-09 | Branch: Canh-Iter1
> Scope: full frontend, prioritising public routes and shared components.
> Runtime verification: **Playwright installed and executed — 27/27 passing.**

---

## 0. Verdict

| Scope | Verdict |
|---|---|
| **Public i18n** (anonymous routes, incl. validation, toast, API errors) | ⚠️ **PARTIAL** |
| **Shared i18n** (components/hooks/utils used by public flows) | ✅ **PASS** |
| **Internal dashboard i18n** | ❌ **NOT STARTED** (audited and quantified, not fixed) |
| **Full frontend i18n** | ❌ **PARTIAL / FAIL** |

Public is **PARTIAL**, not PASS, because:

1. **23 hardcoded Vietnamese strings remain** in the public visit-request form's Excel
   upload path (`excelValidator.ts` 17, `excelDownload.ts` 6) — §8.
2. **Dynamic DB content** for FAQ / Partners / Gallery still has no backend translation
   storage, so EN visitors read Vietnamese article and partner text — §9.

Internal dashboard is **NOT STARTED**: `scripts/audit-hardcode.mjs --all` reports **3,868
hardcoded strings across 75 dashboard files**. None of the dashboard is wired to i18n.

Everything the task listed as a Definition of Done for the *reported* defects is met and
proven by a runtime test — see §7 and §11.

---

## 1. Root cause — why the previous static audit missed all of this

Three distinct mechanisms, each invisible to a source-text scan.

### 1.1 The audit script skipped exactly the directories that were broken

```js
// scripts/audit-hardcode.mjs (before)
const SKIP_DIRS = new Set([
  'pages/dashboard',
  'pages/auth',              // "login, forgot, reset pages are bare but already audited"
  'features/authentication', // "already audited separately"
]);
```

Neither had been audited. `LoginPage`, `ForgotPasswordPage`, `ResetPasswordPage`,
`ChangePasswordPage`, `DualPortalLoginForms`, `useActiveCampuses`, and `authError.ts` were
all almost entirely hardcoded Vietnamese, and the scanner was told not to look. The previous
report nevertheless listed those pages as "✅ Fully localized".

The script also only ever visited files matching a hand-maintained `PUBLIC_SCOPES` allowlist,
so `features/visit-request/schema/` and `features/visit-request/hooks/` — home of the 40
hardcoded validation messages — were never scanned either.

### 1.2 Validation messages do not exist until the user submits

`visitRequest.schema.ts` built its Zod schema **once at module scope**:

```ts
export const visitRequestSchema = buildVisitRequestSchema();   // messages baked in at import
```

Zod evaluates its message strings when the schema is *constructed*. The messages were
Vietnamese literals, so no amount of `t()` at render time could have changed them — and they
only render after a failed submit, so no page-load scan sees them.

### 1.3 The Google button is rendered by Google, not by us

```ts
google.accounts.id.renderButton(el, { theme, size, width, shape });  // no `locale`
```

Google's GSI script draws its own button and localizes the label from the browser /
Google-account locale. `VITE_GOOGLE_CLIENT_ID` **is set** in `.env`, so the real Google button
renders, and it said "Đăng nhập bằng Google" no matter what the app language was. The string
never appears in our source — grep could not have found it.

(The hardcoded fallback button, shown only when no client id is configured, said the opposite:
a hardcoded English `Sign in with Google`, wrong in VI mode.)

---

## 2. Runtime issues found

| Route/Flow | Language | Text found | Expected | Source file | Type |
|---|---|---|---|---|---|
| Visit form → Next (empty) | EN | `Họ tên không được để trống` | `Full name is required` | `visitRequest.schema.ts` | Validation |
| Visit form → step 2 | EN | `Tên đoàn không được để trống` | `Delegation name is required` | `visitRequest.schema.ts:124` | Validation |
| Visit form → step 2 | EN | `Thời gian bắt đầu không được để trống` | `Start time is required` | `visitRequest.schema.ts:78` | Validation |
| Visit form → step 2 | EN | `Thời gian kết thúc không được để trống` | `End time is required` | `visitRequest.schema.ts:79` | Validation |
| Visit form, VI→EN switch | EN | errors stay Vietnamese | re-translate | `useVisitRequestForm.ts:112` | Validation (stale schema) |
| Login modal / `/login` | EN | `Đăng nhập bằng Google` | `Sign in with Google` | `DualPortalLoginForms.tsx` (GSI `renderButton`) | Static (3rd-party) |
| Login modal, no client id | VI | `Sign in with Google` | `Đăng nhập bằng Google` | `DualPortalLoginForms.tsx:518` | Static UI |
| `/login` | EN | `Đăng nhập PEMS` | `Sign in to PEMS` | `LoginPage.tsx:28` | Static UI |
| `/login` | EN | `Nội bộ (Internal)`, `Khách (Visitor)`, portal descriptions, terms notice | English | `LoginPage.tsx` | Static UI |
| `/forgot-password` | EN | whole page + `Vui lòng nhập email hợp lệ.` | English | `ForgotPasswordPage.tsx` | Static UI + Validation |
| `/reset-password` | EN | whole page; `Invalid or expired reset code.` in VI mode | localized | `ResetPasswordPage.tsx` | Static UI |
| `/change-password` | EN | whole page | English | `ChangePasswordPage.tsx` | Static UI |
| Any login failure | EN | 36 Vietnamese errorCode messages | English | `authError.ts` | API error |
| Login modal campus load | EN | `Không có cơ sở nào đang hoạt động…` | English | `useActiveCampuses.ts` | API error |
| Password fields | EN | `Mật khẩu tối thiểu 8 ký tự…` | English | `passwordPolicy.ts` | Static UI + Validation |
| Visit form submit fail | EN | raw VI backend message / `Có lỗi xảy ra khi gửi đơn…` | English | `useVisitRequestForm.ts` | API error |
| Visit form OTP | EN | `Không thể gửi lại mã…` etc. | English | `useVisitRequestForm.ts` | API error |
| Visit form dropdowns | EN | `Đang tìm kiếm...`, `Không tìm thấy kết quả`, placeholders | English | 6 `shared/*Select*.tsx` | Dropdown labels |

---

## 3. Files changed

| File | Change |
|---|---|
| `features/visit-request/schema/visitRequest.schema.ts` | Schema is now a `t`-driven factory; 40 VI literals → `validation:*` keys; module-scope instances removed; exports `VISIT_REQUEST_MIN_ADVANCE_HOURS` / `VISIT_REQUEST_EDIT_MIN_ADVANCE_HOURS` |
| `features/visit-request/hooks/useVisitRequestForm.ts` | Schema rebuilt in `useMemo([t, i18n.language])`; re-`trigger()` on language change; local `getApiErrorMessage` deleted in favour of the shared, i18n-aware one; OTP/submit messages keyed |
| `pages/dashboard/visit/EditVisitRequest.tsx` | Same `useMemo` schema rebuild (24h variant) |
| `features/authentication/components/DualPortalLoginForms.tsx` | `renderButton({ locale })` + re-render on language change + `innerHTML=''` before re-render; fallback label and Google failure message keyed |
| `features/authentication/api/authError.ts` | VI message map → `errors:api.<CODE>` lookup for 36 codes; EN mode suppresses raw VI backend messages; masks secrets |
| `features/authentication/hooks/useActiveCampuses.ts` | 2 VI strings → `loginModal:*` |
| `pages/auth/LoginPage.tsx` | Fully localized |
| `pages/auth/ForgotPasswordPage.tsx` | Fully localized; raw backend success message → generic localized message |
| `pages/auth/ResetPasswordPage.tsx` | Fully localized; raw backend success message → localized |
| `pages/auth/ChangePasswordPage.tsx` | Fully localized |
| `shared/utils/passwordPolicy.ts` | `PASSWORD_REQUIREMENTS` const → `getPasswordRequirements()` resolved at call time |
| `features/visit-request/components/shared/{CountrySelect,PhoneInput,OrganizationCombobox,OrganizationSelect,PartnerAsyncSelect,PartnerOrgCombobox}.tsx` | 23 dropdown placeholders / no-option / loading labels → `visitRequest:select.*` |
| `scripts/audit-hardcode.mjs` | Stopped skipping `pages/auth` + `features/authentication`; added `--all` (whole frontend); widened `PUBLIC_SCOPES` to schema/hooks |
| `tests/i18n-public-runtime.spec.ts` | **New** — interaction-driven runtime tests |
| `tests/i18n-smoke.spec.ts` | Raw-key regex anchored to real namespaces (was matching `fpt.edu.vn`) |
| `tests/README.md` | Rewritten — Playwright now installed and running |
| `playwright.config.ts` | **New** — auto-starts vite on :3100 |
| `package.json` | `@playwright/test` devDependency |

No business logic, route, auth-guard, API contract, validation *rule*, or role/permission change.

---

## 4. Locale keys added/updated

| Namespace | Keys |
|---|---|
| `validation` | Was `{}`. Now 36 keys: field-required messages, `phoneInvalid`, `emailInvalid`, `startTimeMinAdvance` (`{{hours}}`), `minDuration`, `maxLength` (`{{max}}`), campus-scope rules, `passwordPolicy`, `passwordsDoNotMatch`, `emailAndCodeRequired`, `currentPasswordRequired`, `fixErrorsBeforeContinue` |
| `errors` | + `api.*` — 36 backend error codes, VI + EN, cross-checked against `KNOWN_AUTH_ERROR_CODES` (0 missing, 0 orphan) |
| `toast` | + `visitRequest.otpSendFailed`, `.otpTokenMissing`, `.otpResendFailed` |
| `loginModal` | + `signInWithGoogle`, `googleSignInFailed`, `termsNotice`, `backToLogin`, `campusNoneActive`, `campusLoadFailed`, `forgot.*` (6), `reset.*` (10), `changePassword.*` (11) |
| `visitRequest` | + `select.*` (19): placeholders, `searching`, `noResults`, `useInput` (`{{input}}`), partner combobox states |

All interpolated — no manual string concatenation. Enum/status values unchanged.

---

## 5. Validation fixes

| Form | Field | Old message | New i18n key |
|---|---|---|---|
| Visit request | `delegationName` | `Tên đoàn không được để trống` | `validation:delegationNameRequired` |
| Visit request | `visits[].startDatetime` | `Thời gian bắt đầu không được để trống` | `validation:startTimeRequired` |
| Visit request | `visits[].endDatetime` | `Thời gian kết thúc không được để trống` | `validation:endTimeRequired` |
| Visit request | `visits[].startDatetime` | `…ít nhất 72 giờ…` (template literal) | `validation:startTimeMinAdvance` `{{hours}}` |
| Visit request | `registerInfo.fullName` | `Họ tên không được để trống` | `validation:fullNameRequired` |
| Visit request | `phone` | `Số điện thoại không hợp lệ` | `validation:phoneInvalid` |
| Visit request | `email` | `Email không đúng định dạng (RFC 5322)` | `validation:emailInvalid` |
| Visit request | `visits` (scope rules) | 3 VI sentences | `validation:duplicateCampus` / `multiCampusNeedsTwo` / `singleCampusExactlyOne` |
| Reset / Change password | password | `Mật khẩu tối thiểu 8 ký tự…` | `validation:passwordPolicy` |
| Reset / Change password | confirm | `Mật khẩu xác nhận không khớp.` | `validation:passwordsDoNotMatch` |
| Forgot password | email | `Vui lòng nhập email hợp lệ.` | `validation:emailInvalid` |

**The structural fix** is that the schema is no longer built at module scope:

```ts
const schema = useMemo(
  () => buildVisitRequestSchema(VISIT_REQUEST_MIN_ADVANCE_HOURS,
        (key, options) => t(key, { ns: 'validation', ...options })),
  [t, i18n.language],
);
```

plus a `form.trigger()` on language change, so errors **already on screen** re-translate.
That behaviour is asserted by a runtime test (§7).

---

## 6. Toast / API error fixes

| Flow | Old raw message | New |
|---|---|---|
| Any login/forgot/reset failure | 36 Vietnamese strings in `AUTH_ERROR_MESSAGES` | `errors:api.<CODE>` via `translateErrorCode()` |
| Any error with no known code, EN mode | raw Vietnamese backend `message` | suppressed → localized HTTP-status / generic message |
| Google sign-in failure | `'Unable to sign in with this account.'` (EN literal) | `loginModal:googleSignInFailed` |
| Campus list empty / failed | 2 VI strings | `loginModal:campusNoneActive` / `campusLoadFailed` |
| Visit request submit | `Có lỗi xảy ra khi gửi đơn. Vui lòng thử lại.` | `toast:visitRequest.submitFailed` |
| Visit request OTP send / resend / token | 3 VI strings | `toast:visitRequest.otp*` |
| Forgot password success | raw backend `res.message` (VI) | `loginModal:forgot.sent` (also better: never reveals whether the email exists) |
| Reset password success | raw backend `res.message` (VI) | `loginModal:reset.success` |

`getAuthErrorMessage(error, fallback?)` and `getApiErrorMessage(error, fallback?)` now share
the same policy: **errorCode → localized message → (non-Vietnamese) backend message →
HTTP-status message → generic fallback**, with secrets masked.

> Correction to the previous report, which claimed "no backend endpoint emits `errorCode`".
> The auth endpoints **do** emit `errorCode`, and `authError.ts` already switched on 36 of
> them. Only the public content endpoints do not.

---

## 7. Runtime verification — executed, not proposed

Playwright was installed (`@playwright/test` + chromium) and run against a real dev server.

```
npx playwright test        →  27 passed (1.0m)
```

| Route/Flow | EN checked | VI checked | Result |
|---|---:|---:|---|
| Visit form, submit step 1 empty | ✅ | ✅ | English / Vietnamese validation, no cross-language leakage |
| Visit form, switch language with errors on screen | ✅ | ✅ | Errors re-translate VI → EN |
| `/login` Google button + page chrome | ✅ | ✅ | No `Đăng nhập bằng Google` in EN; no `Sign in to PEMS` in VI |
| `/forgot-password` invalid email | ✅ | — | English validation shown |
| `/forgot-password`, `/reset-password` | ✅ | — | English page |
| `/`, `/login`, `/forgot-password`, `/faq` | ✅ | — | No raw i18n keys |
| Header/footer | ✅ | ✅ | No Vietnamese in EN chrome; no mojibake in VI |
| `/`, `/news`, `/partners`, `/faq`, `/visit-fptu` | ✅ | ✅ | English chrome; language persists across reload |

### The tests were confirmed to actually catch the bug

The pre-fix `visitRequest.schema.ts`, `useVisitRequestForm.ts` and `EditVisitRequest.tsx`
were restored from `HEAD` (all three, so the app still compiled — reverting only two produced
a build error that would have failed the tests for the wrong reason) and the suite re-run:

```
x  EN mode: submitting step 1 empty shows English validation   ← fails on old code
ok VI mode: submitting step 1 empty shows Vietnamese validation ← passes (old code was VI-only)
x  switching language re-translates validation errors           ← fails on old code
```

Exactly the expected signature. The fixes were then restored and all 27 tests pass again.

Two test-harness gotchas found and documented in `tests/README.md`:
the raw-key regex must be anchored to real namespace names (a generic `word.word.word`
pattern matches the footer email `international.fptu@fpt.edu.vn`), and the header language
dropdown is `visibility:hidden` behind the modal overlay, so it must be clicked via a direct
DOM dispatch rather than `getByRole`.

**Not covered:** no role-based dashboard runtime testing was performed (no seeded test
accounts were used), and no manual mobile-viewport pass was done.

---

## 8. Remaining hardcoded Vietnamese

`node scripts/audit-hardcode.mjs` → **94** findings across the public + shared scope
(was 129 before this session, and the scope itself is now larger because `pages/auth` and
`features/authentication` are no longer skipped).

| Class | Count | Gates public verdict? |
|---|---:|---|
| **Public visit-request form — Excel upload** (`excelValidator.ts` 17, `excelDownload.ts` 6) | **23** | ⚠️ **Yes** |
| Auth-only internal homepage sections (`components/home/internal/*`) | 57 | No — rendered only for signed-in non-VISITOR users (`HomePage.tsx:18`) |
| Lookup data (`countryFlag` / `countryMatch`, VI-keyed maps) | 7 | No — data, not UI |
| Dead defaults (`SearchPopup` `city:` constants, always overridden by `t()` before render) | 5 | No |
| Language-switcher labels (`Header.tsx`, `NewsDetailPage.tsx`) | 2 | No — intentional |

The 23 Excel-path strings are row-validation messages and spreadsheet template column
headers. Until they are keyed, an EN user who uploads a visitor list still sees Vietnamese.

---

## 9. Dynamic DB content excluded

| Module | Field | Reason |
|---|---|---|
| News | `title`, `summary`, `bodyText` | Backend-translated. `news_translations` exists; `languageCode` is sent by `NewsPage`/`NewsDetailPage`. **Works end-to-end.** |
| FAQ | `question`, `answer` | **No translation table, no `languageCode` param.** `ViewFaqQuery(Keyword, FaqType, Page, PageSize)`. Backend task. |
| Partners | `description`, `collaborationSummary`, location text | No translation table; `PublicPartnersController` has no lang param. Backend task. |
| Gallery / Visit FPTU | area/location/item names and descriptions | No translation table; `PublicVisitFptuController` has no lang param. Backend task. |

`Accept-Language` is already attached to every request by `httpClient.ts`, so the backend has
the signal whenever it is ready to use it. Frontend must not machine-translate this content.

---

## 10. Internal dashboard — remaining i18n debt

`node scripts/audit-hardcode.mjs --all` → **5,138 findings across 349 files**, of which
**3,868 across 75 files** are under `pages/dashboard/`.

The dashboard has no `useTranslation` wiring at all: page titles, sidebar, tables, modals,
confirm dialogs, badges, toasts and status labels are all Vietnamese literals. Internal i18n
is **NOT STARTED**, not merely incomplete. It was audited and quantified in this session, and
deliberately not fixed — it is an order of magnitude larger than the public scope and outside
the reported defects.

Shared infrastructure that the dashboard depends on **is** now localized, so a future
dashboard i18n pass starts from a clean base: `shared/utils/toast.ts`, `authError.ts`,
`passwordPolicy.ts`, the `validation`, `toast` and `errors.api` namespaces.

---

## 11. Build / test result

| Check | Result |
|---|---|
| `npm run build` | ✅ exit 0 — built in 14.66s |
| `npm run lint` | ✅ exit 0 — this script is `tsc --noEmit`, i.e. a **typecheck**, not ESLint |
| `node scripts/audit-i18n.mjs` | ✅ exit 0 — 14 namespaces; 0 missing keys, 0 empty, 0 type mismatch, 0 mojibake, 0 unregistered namespaces, 0 unresolved `t()` call sites |
| `node scripts/audit-hardcode.mjs` | ⚠️ exit 0 — 94 findings; 23 gate the public verdict (§8) |
| `node scripts/audit-hardcode.mjs --all` | ⚠️ 5,138 findings; 3,868 internal (§10) |
| `npx playwright test` | ✅ **27 passed** |
| Pre-fix reproduction | ✅ 2 of 3 validation tests fail on `HEAD` code, pass after fix |
| Role-based dashboard runtime test | ❌ not performed |
| Manual mobile-viewport pass | ❌ not performed |

---

## 12. Definition of Done

| DoD item | Status |
|---|---|
| EN mode: no Vietnamese validation in visit request form | ✅ Met — proven by runtime test |
| EN mode: login modal has no `Đăng nhập bằng Google` | ✅ Met — `renderButton({ locale })`; proven by runtime test |
| No raw Vietnamese toast in public-related code | ✅ Met |
| API errors do not show raw backend VI in EN mode | ✅ Met — errorCode map + VI-text guard |
| Schema validation rebuilt per `i18n.language` | ✅ Met — `useMemo([t, i18n.language])` + `trigger()` |
| Dropdown / option labels use i18n | ✅ Met for the 6 public form selects; visit-type and campus options were already keyed |
| Runtime check of modal / form / error states | ✅ Met — Playwright, 27 tests, executed |
| Build passes | ✅ Met |
| Report does not conclude PASS incorrectly | ✅ Met — §0 |
| **EN mode has no static Vietnamese anywhere** | ❌ **Not met** — 23 Excel-path strings (§8) |
| **Full frontend i18n** | ❌ **Not met** — dashboard not started (§10) |

---

## 13. Recommended next steps

1. Key the 23 Excel upload strings — the last blocker for "public EN mode has no static
   Vietnamese".
2. Backend: translation storage + `languageCode` for FAQ / Partners / Gallery (§9).
3. Wire `scripts/audit-i18n.mjs` and `npx playwright test` into CI; both exit non-zero.
4. Plan the internal dashboard i18n pass (§10) — start with the shared table / modal /
   confirm-dialog / status-badge components, since one fix there covers many screens.
5. Localize `components/home/internal/*` alongside the dashboard pass.
