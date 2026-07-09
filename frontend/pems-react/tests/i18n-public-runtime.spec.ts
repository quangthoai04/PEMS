/**
 * Runtime i18n tests — these drive the real UI, because the defects they cover only
 * appear AFTER a user interaction and are invisible to a static scan:
 *
 *   1. Zod validation messages (only rendered after a failed submit).
 *   2. Validation language after an in-page language switch (schema rebuild).
 *   3. The Google SSO button label (rendered by Google's own script).
 */
import { test, expect, type Page } from '@playwright/test';

/** Any Vietnamese-specific diacritic. Plain ASCII English never matches. */
const VIETNAMESE =
  /[àáâãèéêìíòóôõùúăđĩũơưạảấầẩẫậắằẳẵặẹẻẽếềểễệỉịọỏốồổỗộớờởỡợụủứừửữựỳỵỷỹý]/i;

async function gotoWithLanguage(page: Page, path: string, lng: 'vi' | 'en') {
  await page.addInitScript((l) => window.localStorage.setItem('pems.language', l), lng);
  await page.goto(path);
  await page.waitForLoadState('domcontentloaded');
}

/** Clicks a language option in the header menu by its visible label. */
async function switchLanguageTo(page: Page, label: 'English' | 'Tiếng Việt') {
  await page.evaluate((text) => {
    const btn = [...document.querySelectorAll('button')].find(
      (b) => b.textContent?.trim() === text,
    );
    if (!btn) throw new Error(`language button "${text}" not found`);
    (btn as HTMLButtonElement).click();
  }, label);
}

/** Opens the public visit-request popup from the homepage hero CTA. */
async function openVisitForm(page: Page) {
  await page.getByRole('button', { name: /Book a Visit|Đăng ký tham quan/i }).first().click();
  await expect(page.getByText(/Registrant Information|Thông tin người đăng ký/i).first()).toBeVisible();
}

test.describe('visit-request validation is localized at runtime', () => {
  test('EN mode: submitting step 1 empty shows English validation, no Vietnamese', async ({ page }) => {
    await gotoWithLanguage(page, '/', 'en');
    await openVisitForm(page);

    await page.getByRole('button', { name: /^Next$/i }).click();

    // A real message from validation.json, produced by the Zod resolver.
    await expect(page.getByText('Full name is required').first()).toBeVisible();

    // The exact strings reported as broken must not appear anywhere.
    await expect(page.getByText('Họ tên không được để trống')).toHaveCount(0);
    await expect(page.getByText('Tên đoàn không được để trống')).toHaveCount(0);
    await expect(page.getByText('Thời gian bắt đầu không được để trống')).toHaveCount(0);
    await expect(page.getByText('Thời gian kết thúc không được để trống')).toHaveCount(0);
  });

  test('VI mode: submitting step 1 empty shows Vietnamese validation', async ({ page }) => {
    await gotoWithLanguage(page, '/', 'vi');
    await openVisitForm(page);

    await page.getByRole('button', { name: /Tiếp theo/i }).click();
    await expect(page.getByText('Họ tên không được để trống').first()).toBeVisible();
  });

  /**
   * Regression test for the module-scope Zod schema: a schema built once at import time
   * keeps whatever language was active on first render, so already-visible errors stayed
   * Vietnamese after switching to English.
   */
  test('switching language re-translates validation errors already on screen', async ({ page }) => {
    await gotoWithLanguage(page, '/', 'vi');
    await openVisitForm(page);

    await page.getByRole('button', { name: /Tiếp theo/i }).click();
    await expect(page.getByText('Họ tên không được để trống').first()).toBeVisible();

    // The header language menu sits behind the modal overlay and is `visibility:hidden`
    // until hovered, so neither getByRole nor a forced click reaches it. Dispatch the
    // click on the element itself — this still runs the real `changeLanguage` handler.
    await switchLanguageTo(page, 'English');

    await expect(page.getByText('Full name is required').first()).toBeVisible();
    await expect(page.getByText('Họ tên không được để trống')).toHaveCount(0);
  });
});

test.describe('login modal Google button', () => {
  test('EN mode: no Vietnamese Google button label', async ({ page }) => {
    await gotoWithLanguage(page, '/login', 'en');

    // Google's GSI iframe may or may not load (network); either way the visible label
    // in our own DOM must never be the Vietnamese one.
    await expect(page.getByText('Đăng nhập bằng Google')).toHaveCount(0);
    await expect(page.getByText('Đăng nhập PEMS')).toHaveCount(0);
    await expect(page.getByText('Sign in to PEMS').first()).toBeVisible();
  });

  test('VI mode: login page renders Vietnamese', async ({ page }) => {
    await gotoWithLanguage(page, '/login', 'vi');
    await expect(page.getByText('Đăng nhập PEMS').first()).toBeVisible();
    await expect(page.getByText('Sign in to PEMS')).toHaveCount(0);
  });
});

test.describe('public auth pages are localized', () => {
  for (const [path, en, vi] of [
    ['/forgot-password', 'Forgot password', 'Quên mật khẩu'],
    ['/reset-password', 'Reset password', 'Đặt lại mật khẩu'],
  ] as const) {
    test(`${path} renders EN in EN mode`, async ({ page }) => {
      await gotoWithLanguage(page, path, 'en');
      await expect(page.getByText(en).first()).toBeVisible();
      await expect(page.getByText(vi)).toHaveCount(0);
    });
  }

  test('/forgot-password shows an English validation error for a bad email', async ({ page }) => {
    await gotoWithLanguage(page, '/forgot-password', 'en');
    await page.locator('input[type="email"]').fill('not-an-email');
    await page.getByRole('button', { name: /Send reset code/i }).click();

    await expect(page.getByText('Invalid email format (RFC 5322)').first()).toBeVisible();
    await expect(page.getByText('Vui lòng nhập email hợp lệ.')).toHaveCount(0);
  });
});

test.describe('no raw i18n keys leak on public routes', () => {
  // A key rendered as text, e.g. "search:noResult" or "visitFptu.gallery.actions.up".
  // Anchored to the real namespace names so that emails and domains such as
  // "international.fptu@fpt.edu.vn" do not produce false positives.
  const NAMESPACES = [
    'common', 'publicLayout', 'home', 'news', 'partners', 'faq', 'gallery',
    'visitRequest', 'validation', 'errors', 'toast', 'loginModal', 'search', 'visitFptu',
  ].join('|');
  const RAW_KEY = new RegExp(`\\b(${NAMESPACES})[:.][a-zA-Z0-9_]+(\\.[a-zA-Z0-9_]+)*\\b`);

  for (const route of ['/', '/login', '/forgot-password', '/faq'] as const) {
    test(`${route} has no raw keys and English chrome in EN mode`, async ({ page }) => {
      await gotoWithLanguage(page, route, 'en');
      await page.waitForLoadState('domcontentloaded');

      const body = await page.locator('body').innerText();
      expect(body, `raw i18n key visible on ${route}`).not.toMatch(RAW_KEY);
    });
  }

  test('EN header/footer contain no Vietnamese', async ({ page }) => {
    await gotoWithLanguage(page, '/', 'en');
    const chrome = (await page.locator('header, footer').allInnerTexts()).join('\n');
    // The language switcher legitimately renders the word "Tiếng Việt".
    const withoutSwitcher = chrome.replace(/Tiếng Việt/g, '');
    expect(withoutSwitcher).not.toMatch(VIETNAMESE);
  });
});
