/**
 * PEMS public i18n runtime smoke tests — PROPOSED, NOT YET EXECUTED.
 *
 * Playwright is NOT currently a dependency of this project, so this spec has never
 * been run. It is committed as an executable specification. See tests/README.md for
 * the exact commands needed to enable it.
 *
 * Scope note (important):
 *   These tests assert on STATIC UI chrome (header, nav, footer, language switcher)
 *   and on the absence of raw i18n keys. They deliberately do NOT assert that the
 *   whole page body is free of Vietnamese in EN mode, because News/FAQ/Partners/
 *   Gallery render dynamic content straight from the database, and only `news` has a
 *   translation table today. Asserting on the body would fail for reasons that are a
 *   backend data gap, not a frontend i18n defect.
 */
import { test, expect, type Page } from '@playwright/test';

const PUBLIC_ROUTES = ['/', '/news', '/partners', '/faq', '/visit-fptu', '/login'] as const;

/** Any Vietnamese-specific diacritic. Plain ASCII words like "Visit" never match. */
const VIETNAMESE_DIACRITICS =
  /[àáâãèéêìíòóôõùúăđĩũơưạảấầẩẫậắằẳẵặẹẻẽếềểễệỉịọỏốồổỗộớờởỡợụủứừửữựỳỵỷỹý]/i;

/**
 * Raw i18n keys leaking to the UI. Two shapes:
 *   'search:noResult'  — namespace prefix survived (key genuinely missing)
 *   'gallery.actions.viewDetails' — dotted key rendered as text
 *
 * Anchored to the real namespace names, otherwise ordinary page text such as the footer
 * email "international.fptu@fpt.edu.vn" matches and the test fails for no reason.
 *
 * A bare last-segment leak (t('loginModal:title') -> "title") cannot be detected from
 * text alone, which is exactly why audit-i18n.mjs checks it statically instead.
 */
const NAMESPACES = [
  'common', 'publicLayout', 'home', 'news', 'partners', 'faq', 'gallery',
  'visitRequest', 'validation', 'errors', 'toast', 'loginModal', 'search', 'visitFptu',
].join('|');
const RAW_KEY_PATTERN = new RegExp(`\\b(${NAMESPACES})[:.][a-zA-Z0-9_]+(\\.[a-zA-Z0-9_]+)*\\b`);

async function setLanguage(page: Page, lng: 'vi' | 'en') {
  await page.addInitScript((l) => window.localStorage.setItem('pems.language', l), lng);
}

/** Text of the static chrome only — excludes API-driven page content. */
async function chromeText(page: Page): Promise<string> {
  const parts = await page.locator('header, footer, nav').allInnerTexts();
  return parts.join('\n');
}

test.describe('public i18n — EN mode', () => {
  test.beforeEach(async ({ page }) => setLanguage(page, 'en'));

  for (const route of PUBLIC_ROUTES) {
    test(`${route} renders English static chrome`, async ({ page }) => {
      await page.goto(route);
      await page.waitForLoadState('networkidle');

      const chrome = await chromeText(page);
      expect(chrome.length, `no header/footer/nav found on ${route}`).toBeGreaterThan(0);
      expect(chrome, `Vietnamese diacritics in EN chrome on ${route}`).not.toMatch(
        VIETNAMESE_DIACRITICS,
      );
    });

    test(`${route} shows no raw translation keys`, async ({ page }) => {
      await page.goto(route);
      await page.waitForLoadState('networkidle');

      const body = await page.locator('body').innerText();
      expect(body, `raw i18n key visible on ${route}`).not.toMatch(RAW_KEY_PATTERN);
    });
  }
});

test.describe('public i18n — VI mode', () => {
  test.beforeEach(async ({ page }) => setLanguage(page, 'vi'));

  test('home renders Vietnamese nav and no mojibake', async ({ page }) => {
    await page.goto('/');
    await page.waitForLoadState('networkidle');

    const chrome = await chromeText(page);
    expect(chrome).toMatch(VIETNAMESE_DIACRITICS);
    // U+FFFD, or a '?' wedged between letters — the signature of a locale file
    // written through a non-UTF-8 codepage ("Tin t?c").
    expect(chrome, 'mojibake in VI chrome').not.toMatch(/�|[A-Za-zÀ-ỹ]\?[A-Za-zÀ-ỹ]/);
  });
});

test.describe('language switcher', () => {
  test('selected language survives a reload', async ({ page }) => {
    await setLanguage(page, 'en');
    await page.goto('/');
    await page.waitForLoadState('networkidle');

    const before = await chromeText(page);
    await page.reload();
    await page.waitForLoadState('networkidle');

    expect(await chromeText(page)).toBe(before);
    expect(await page.evaluate(() => localStorage.getItem('pems.language'))).toBe('en');
  });
});
