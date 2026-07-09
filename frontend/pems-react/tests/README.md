# Frontend tests

## Status

Playwright **is installed and these tests run**. As of 2026-07-09, `npx playwright test`
runs 27 tests against a real Chromium browser and a real dev server, and all 27 pass.

```bash
cd frontend/pems-react
npx playwright test                                  # everything
npx playwright test tests/i18n-public-runtime.spec.ts # the interaction-driven suite
```

`playwright.config.ts` starts `vite --port=3100` automatically (`webServer`), so no
separate dev server is needed. No backend is required: the specs cover client-side
rendering, validation, and language switching, and public pages degrade to their error /
empty states when the API is unreachable.

`tests/` is excluded from `npm run lint` (`tsc --noEmit`) via `exclude` in `tsconfig.json`,
because Playwright compiles the specs itself.

## The two suites

**`i18n-public-runtime.spec.ts`** — interaction-driven. Covers exactly the class of bug a
static scan cannot see, because the text only exists after a user acts:

- Zod validation messages after submitting the visit-request form empty (EN and VI).
- Validation messages re-translating when the language is switched while errors are on screen.
- The Google SSO button label.
- Public auth pages (`/login`, `/forgot-password`, `/reset-password`) and their validation.
- No raw i18n keys on public routes.

**`i18n-smoke.spec.ts`** — static-chrome smoke tests: header/nav/footer language, mojibake
detection, and language persistence across reload.

## Gotchas worth knowing

- **Raw-key regex must be anchored to real namespace names.** A generic
  `word.word.word` pattern matches the footer email `international.fptu@fpt.edu.vn` and
  fails for no reason.
- **The header language dropdown is `visibility:hidden` until hovered**, so it is absent
  from the accessibility tree — `getByRole` cannot find it. It also sits behind the modal
  overlay, so a forced click lands on the overlay. Use `switchLanguageTo()`, which
  dispatches the click on the element directly (this still runs the real handler).

## What these tests do NOT cover

They do not assert that the whole page body is free of Vietnamese in EN mode. News / FAQ /
Partners / Gallery render dynamic content straight from the database, and only `news` has a
translation table. Asserting on the body would fail on a backend data gap rather than a
frontend i18n defect.

Static key integrity is enforced separately and offline by `scripts/audit-i18n.mjs`, which
gates on mojibake, namespace registration, and unresolved `t()` call sites, and exits
non-zero. `scripts/audit-hardcode.mjs` scans the public + shared scope by default and the
whole frontend with `--all`.
