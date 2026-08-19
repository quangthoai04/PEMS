/**
 * Per-campus form v2 — browser-level component/contract test (mocked network).
 *
 * This is a COMPONENT/CONTRACT test per the Phase-H matrix (§12): it drives the REAL per-campus form
 * (`CampusVisitCard` + `useFieldArray` + the CSS-hide accordion + the apply-to-all dialog) in a real
 * Chromium DOM, with the backend mocked. It is NOT a full real-stack E2E (no real backend / MySQL / OTP).
 * It intentionally covers only the BROWSER-ONLY behaviours that jsdom cannot assert — accordion visibility
 * with data retention, and the apply-to-all confirmation dialog appearing over the real layout. The copy /
 * apply-all / deep-copy LOGIC is unit-tested (`visitRequestV2Form.test.ts`, `useVisitRequestFormV2.test.tsx`).
 *
 * The public v2 route `/visit-registration/v2` renders regardless of the server flag; the mocks stand in
 * for the flag-gated backend.
 */
import { test, expect, type Page } from '@playwright/test';

async function mockCampuses(page: Page) {
  await page.route('**/campuses/available-for-registration', route =>
    route.fulfill({
      json: [
        { campusId: 1, campusCode: 'HN', campusName: 'Hà Nội', city: 'Hà Nội' },
        { campusId: 2, campusCode: 'DN', campusName: 'Đà Nẵng', city: 'Đà Nẵng' },
        { campusId: 4, campusCode: 'HCM', campusName: 'Hồ Chí Minh', city: 'TP. Hồ Chí Minh' },
      ],
    }));
  await page.route('**/public/partners/**', route => route.fulfill({ json: [] }));
}

async function gotoForm(page: Page) {
  await page.addInitScript(() => window.localStorage.setItem('pems.language', 'vi'));
  await page.goto('/visit-registration/v2');
  await page.waitForLoadState('domcontentloaded');
  await expect(page.getByRole('heading', { name: /theo từng cơ sở/i })).toBeVisible();
}

// delegationName is a Controller-bound AutoGrowTextField (plan §21.2, long org names wrap instead
// of scrolling sideways) -- it renders as a <textarea data-testid="campus-delegation-input"> with
// no `name` HTML attribute (RHF tracks it by the Controller `name` prop, not the DOM attribute).
const del = (i: number) => `[data-testid="campus-delegation-input"] >> nth=${i}`;
const campusSel = (i: number) => `select[name="campusVisits.${i}.campus"]`;

test.describe('Per-campus form v2 (browser component/contract)', () => {
  test.beforeEach(async ({ page }) => {
    await mockCampuses(page);
    await gotoForm(page);
  });

  test('collapsing a campus card hides its body via CSS but never loses typed data', async ({ page }) => {
    await page.locator(campusSel(0)).selectOption('HN');
    await page.locator(del(0)).fill('Giữ Dữ Liệu');

    const body = page.locator('[id^="campus-card-body-"]').first();
    const toggle = page.locator('button[aria-controls^="campus-card-body-"]').first();
    await expect(body).toBeVisible();
    await expect(toggle).toHaveAttribute('aria-expanded', 'true');

    // Collapse → body hidden (CSS `hidden`, fields stay mounted so RHF never unregisters).
    await toggle.click();
    await expect(toggle).toHaveAttribute('aria-expanded', 'false');
    await expect(body).toBeHidden();

    // Re-expand → the typed value is still there (no unmount, no data loss).
    await toggle.click();
    await expect(toggle).toHaveAttribute('aria-expanded', 'true');
    await expect(page.locator(del(0))).toHaveValue('Giữ Dữ Liệu');
  });

  test('adding a campus opens a new independent card and apply-to-all raises a confirm dialog', async ({ page }) => {
    await page.locator(campusSel(0)).selectOption('HN');
    await page.locator(del(0)).fill('Đoàn Áp Dụng');

    // Add a second campus — it appears empty and expanded.
    await page.getByRole('button', { name: /Thêm cơ sở/ }).click();
    await expect(page.locator(del(1))).toBeVisible();
    await expect(page.locator(del(1))).toHaveValue('');

    // Give campus 2 its own content, then trigger apply-to-all from campus 1.
    await page.locator(campusSel(1)).selectOption('HCM');
    await page.locator(del(1)).fill('Nội dung riêng HCM');
    await page.getByRole('button', { name: /Áp dụng cho cơ sở khác/ }).first().click();

    // A confirmation dialog appears over the real layout and names the campus it would overwrite —
    // apply-to-all never mutates silently (the dialog is browser-DOM behaviour).
    const dialog = page.getByRole('dialog', { name: /Áp dụng cho các cơ sở còn lại/ });
    await expect(dialog).toBeVisible();
    await expect(dialog.getByText(/Hồ Chí Minh/)).toBeVisible();

    // Cancel → the target keeps its own content (no silent overwrite).
    await dialog.getByRole('button', { name: 'Hủy' }).click();
    await expect(dialog).toBeHidden();
    await expect(page.locator(del(1))).toHaveValue('Nội dung riêng HCM');
  });
});
