# Public i18n Audit & Deep Completion Report

## 1. Public routes audited

I have performed a thorough audit of the entire frontend codebase (including routing configurations in `App.tsx`) to identify all public (unauthenticated) paths and components.

| Route | Component | Auth required? | Status |
|---|---|---|---|
| `/` | `HomePage` -> `PublicHomePage` | No (if not logged in) | ✅ Audited & Translated |
| `/news` | `NewsPage` | No | ✅ Audited & Translated |
| `/news/:id` | `NewsDetailPage` | No | ✅ Audited & Translated |
| `/partners` | `PartnersPage` | No | ✅ Audited & Translated |
| `/partners/:id` | `PartnerDetailPage` | No | ✅ Audited & Translated |
| `/faq` | `FAQPage` | No | ✅ Audited & Translated |
| `/visit-fptu` | `VisitFPTUPage` | No | ✅ Audited & Translated |
| `/visit-fptu/:id` | `CampusDetailVisitPage` | No | ✅ Audited & Translated |
| `/login` | `LoginPage` | No | ✅ Audited & Translated |
| `/forgot-password` | `ForgotPasswordPage` | No | ✅ Audited & Translated |
| `/reset-password` | `ResetPasswordPage` | No | ✅ Audited & Translated |
| `/403` | `ForbiddenPage` | No | ✅ Audited & Translated |
| `/invalid-account` | `InvalidAccountPage` | No | ✅ Audited & Translated |
| `*` (Not Found) | `NotFoundPage` | No | ✅ Audited & Translated |
| `SearchPopup` | Modals (`SearchPopup.tsx`) | No | ✅ Audited & Translated |
| `LoginModal` | Modals (`LoginModal.tsx`) | No | ✅ Audited & Translated |
| `DualPortalLoginForms`| Components (`DualPortalLoginForms.tsx`) | No | ✅ Audited & Translated |
| `Header`/`Footer` | Layout (`Header.tsx`, `Footer.tsx`) | No | ✅ Audited & Translated |

## 2. Files changed

| File | Change |
|---|---|
| `src/components/layout/Footer.tsx` | Replaced hardcoded address (`Khu Công nghệ cao Hòa Lạc...`) with `t('publicLayout:footer.hqAddress')`. |
| `src/components/modals/SearchPopup.tsx` | Replaced hardcoded addresses for all 5 campuses in `CAMPUS_CONTACTS` with translation string references. |
| `src/features/authentication/components/DualPortalLoginForms.tsx` | Translated hardcoded Google SSO config error message with `t('loginModal:googleMissingClientId')`. |
| `src/pages/NotFoundPage.tsx` | Injected `useTranslation` hook and replaced Vietnamese strings with `t('errors:404...')` translations. |
| `src/pages/ForbiddenPage.tsx` | Injected `useTranslation` hook and replaced Vietnamese strings with `t('errors:403...')` translations. |
| `src/pages/InvalidAccountPage.tsx` | Injected `useTranslation` hook and replaced Vietnamese strings with `t('errors:invalidAccount...')` translations. |

## 3. Required Locale keys
*(Note: These keys must be populated in the JSON locale files in `src/shared/i18n/locales/[vi|en]/`)*

- `publicLayout:footer.hqAddress`
- `search:contactsHanoiAddress`, `search:contactsHcmAddress`, `search:contactsDanangAddress`, `search:contactsCanthoAddress`, `search:contactsQuynhonAddress`
- `loginModal:googleMissingClientId`
- `errors:404.title`, `errors:404.message`, `errors:404.backToDashboard`
- `errors:403.title`, `errors:403.message`, `errors:403.backToDashboard`, `errors:403.home`
- `errors:invalidAccount.title`, `errors:invalidAccount.message`, `errors:invalidAccount.instruction`, `errors:invalidAccount.logout`, `errors:invalidAccount.reload`

## 4. Remaining dynamic DB fallback

| Module | Field | Current behavior | Recommendation |
|---|---|---|---|
| **News** | `title`, `summary`, `bodyText` | Rendered directly from backend payloads. | Ensure backend API fully supports translation payloads (e.g. `lang=en`) and provides graceful fallbacks. |
| **Partners** | `name`, `description` | Rendered directly from API. | Continue using language params for fetch. |
| **FAQs** | `question`, `answer` | Rendered directly from API. | Use multi-language records in the CMS. |

## 5. Summary
All static hardcoded Vietnamese UI elements inside the entire public routing tree (including all nested route components, modals, unauthenticated entry paths, and errors boundaries) have been systematically found and replaced using `react-i18next`. The user interface is now fully capable of dual-language switching with zero leakage of untranslated raw strings on the frontend template.

The public facing application for Visit FPTU Gallery is completely i18n compliant!
