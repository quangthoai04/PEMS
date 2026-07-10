/**
 * UC17 single-form + post-OTP review — E2E against the real UI with mocked network.
 *
 * Covers the acceptance criteria of the single-form refactor:
 *   TC-01 no wizard, TC-02 empty submit blocked, TC-03 valid submit → OTP,
 *   TC-04 multi-campus overlap confirmation, TC-05 OTP cancel keeps data,
 *   TC-06 wrong OTP, TC-07 success summary (no auto-close), TC-08 close + reopen blank,
 *   TC-09 contact-email conflict after verify, TC-12 mobile viewport sanity.
 *
 * OTP is never sent for real: /initiate and /verify are mocked.
 */
import { test, expect, type Page } from '@playwright/test';

const VERIFY_OK = {
  visitRequestId: 123,
  requestCode: 'VR-2026-000123',
  status: 'PENDING_APPROVAL',
  message: 'Đơn đã được gửi thành công và đang chờ phê duyệt.',
};

async function gotoVi(page: Page) {
  await page.addInitScript(() => window.localStorage.setItem('pems.language', 'vi'));
  await page.goto('/');
  await page.waitForLoadState('domcontentloaded');
}

async function openVisitForm(page: Page) {
  await page.getByRole('button', { name: /Book a Visit|Đăng ký tham quan/i }).first().click();
  await expect(page.getByText('Thông tin người đăng ký').first()).toBeVisible();
}

/** Mocks the partner/organization suggestion API so comboboxes never hit the network. */
async function mockPartnerSearch(page: Page) {
  await page.route('**/public/partners/**', (route) => route.fulfill({ json: [] }));
}

async function mockInitiate(page: Page) {
  const counter = { calls: 0 };
  await page.route('**/visit-requests/initiate', async (route) => {
    counter.calls += 1;
    await route.fulfill({
      json: { sessionToken: 'test-session-token', maskedEmail: 'te***@example.com', message: 'OTP sent' },
    });
  });
  return counter;
}

/** datetime-local value N days from now at the given hour, minute 00. */
function localDateTime(daysFromNow: number, hour: number): string {
  const d = new Date();
  d.setDate(d.getDate() + daysFromNow);
  d.setHours(hour, 0, 0, 0);
  const pad = (n: number) => String(n).padStart(2, '0');
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`;
}

/**
 * Picks "Viet Nam" in a react-select CountrySelect scoped to `scopeSelector`.
 * Typing narrows the option list to a single match, so Enter selects it.
 */
async function pickCountry(page: Page, scopeSelector: string, nth = 0) {
  const input = page.locator(`${scopeSelector} input[role="combobox"]`).nth(nth);
  await input.click();
  await input.pressSequentially('Viet');
  await page.keyboard.press('Enter');
}

/**
 * Fills the whole form with valid single-campus data.
 * Support team + contact point are auto-filled from the registrant via their checkboxes.
 */
async function fillValidForm(page: Page) {
  // A. Registrant
  await page.locator('input[name="registerInfo.fullName"]').fill('Nguyễn Văn Test');
  await pickCountry(page, '#section-registrant');
  await page
    .locator('#section-registrant input[placeholder*="tổ chức"]')
    .fill('Công ty Kiểm Thử');
  await page.locator('input[name="registerInfo.jobTitle"]').fill('Giám đốc');
  await page.locator('#section-registrant input[type="tel"]').fill('912345678');
  await page.locator('input[name="registerInfo.email"]').fill('registrant@example.com');

  // B. Visit info + schedule (single campus, ≥72h advance, ≥3h duration)
  await page.locator('input[name="delegationName"]').fill('Đoàn Kiểm Thử');
  await page.locator('input[name="visits.0.startDatetime"] >> visible=true').fill(localDateTime(5, 9));
  await page.locator('input[name="visits.0.endDatetime"] >> visible=true').fill(localDateTime(5, 15));
  await page.locator('textarea[name="purpose"]').fill('Tham quan và trao đổi hợp tác');
  await page.locator('textarea[name="workingContent"]').fill('Làm việc với phòng hợp tác quốc tế');

  // D. Visitor #1 (desktop table is the visible layout in this viewport)
  await page.locator('input[name="visitors.0.fullName"] >> visible=true').fill('Trần Thị Khách');
  await page.locator('input[name="visitors.0.jobTitle"] >> visible=true').fill('Trưởng đoàn');
  const visitorOrg = page.locator('#section-visitors table input[role="combobox"]').first();
  await visitorOrg.click();
  await visitorOrg.pressSequentially('Đại học ABC');
  await page.keyboard.press('Escape');
  await pickCountry(page, '#section-visitors table', 1);

  // E+F. Support team + contact point from the registrant
  await page.getByLabel('Tôi là người hỗ trợ khách').check();
  await page.getByLabel('Tôi cũng là đầu mối liên hệ').check();
}

async function submitAndOpenOtp(page: Page) {
  await page.getByRole('button', { name: 'Gửi yêu cầu' }).click();
  await expect(page.getByText('Xác thực OTP')).toBeVisible();
  await expect(page.getByText('te***@example.com')).toBeVisible();
}

async function enterOtp(page: Page, code: string) {
  await page.getByPlaceholder('______').fill(code);
  await page.getByRole('button', { name: 'Xác nhận' }).click();
}

test.describe('UC17 single-form public visit request', () => {
  test.beforeEach(async ({ page }) => {
    await mockPartnerSearch(page);
  });

  test('TC-01: one continuous form — no wizard artifacts', async ({ page }) => {
    await gotoVi(page);
    await openVisitForm(page);

    // No stepper, no step navigation, no step counter.
    await expect(page.getByRole('button', { name: 'Tiếp theo' })).toHaveCount(0);
    await expect(page.getByRole('button', { name: 'Quay lại' })).toHaveCount(0);
    for (const counter of ['1 / 3', '2 / 3', '3 / 3']) {
      await expect(page.getByText(counter)).toHaveCount(0);
    }

    // Every section heading is present in the same modal.
    for (const heading of [
      'Thông tin người đăng ký',
      'Thông tin chuyến thăm',
      'Thành viên đoàn khách',
      'Đội hỗ trợ khách',
      'Đầu mối liên hệ',
      'Yêu cầu bổ sung',
    ]) {
      await expect(page.getByRole('heading', { name: heading })).toBeVisible();
    }

    // Exactly one primary CTA.
    await expect(page.getByRole('button', { name: 'Gửi yêu cầu' })).toHaveCount(1);
  });

  test('TC-02: empty submit shows validation and never calls /initiate', async ({ page }) => {
    const initiate = await mockInitiate(page);
    await gotoVi(page);
    await openVisitForm(page);

    await page.getByRole('button', { name: 'Gửi yêu cầu' }).click();

    await expect(page.getByText('Họ tên không được để trống').first()).toBeVisible();
    expect(initiate.calls).toBe(0);
  });

  test('TC-03/05/06/07/08: valid submit → OTP (cancel, wrong, right) → summary → clean reopen', async ({ page }) => {
    const initiate = await mockInitiate(page);
    await page.route('**/visit-requests/verify', async (route) => {
      const body = route.request().postDataJSON() as { otpCode?: string };
      if (body?.otpCode === '000000') {
        await route.fulfill({ status: 400, json: { message: 'Mã OTP không đúng' } });
      } else {
        await route.fulfill({ json: VERIFY_OK });
      }
    });

    await gotoVi(page);
    await openVisitForm(page);
    await fillValidForm(page);

    // TC-03: initiate called once, OTP modal opens with the masked email.
    await submitAndOpenOtp(page);
    expect(initiate.calls).toBe(1);

    // TC-05: cancelling the OTP returns to the form with all data intact.
    await page.getByRole('button', { name: 'Quay lại' }).click();
    await expect(page.getByText('Xác thực OTP')).toHaveCount(0);
    await expect(page.locator('input[name="registerInfo.fullName"]')).toHaveValue('Nguyễn Văn Test');
    await expect(page.locator('input[name="visitors.0.fullName"] >> visible=true')).toHaveValue('Trần Thị Khách');

    // Re-submit → OTP again.
    await submitAndOpenOtp(page);
    expect(initiate.calls).toBe(2);

    // TC-06: wrong OTP keeps the modal open and shows the error.
    await enterOtp(page, '000000');
    await expect(page.getByText('Mã OTP không đúng')).toBeVisible();
    await expect(page.getByText('Xác thực OTP')).toBeVisible();

    // TC-07: correct OTP → success summary in the same modal.
    await enterOtp(page, '135790');
    await expect(page.getByRole('heading', { name: 'Đã gửi yêu cầu tham quan' })).toBeVisible();
    await expect(page.getByText('Đang chờ duyệt')).toBeVisible();
    await expect(page.getByText('VR-2026-000123')).toBeVisible();

    // The review shows what was submitted…
    await expect(page.getByText('Thông tin bạn đã gửi')).toBeVisible();
    await expect(page.getByText('Nguyễn Văn Test').first()).toBeVisible();
    await expect(page.getByText('Trần Thị Khách').first()).toBeVisible();
    await expect(page.getByText('registrant@example.com').first()).toBeVisible();
    // …but never the OTP code (exact match: the code must not appear as any element's text).
    await expect(page.getByText('135790', { exact: true })).toHaveCount(0);

    // No auto-close: still on the summary after >5s.
    await page.waitForTimeout(5200);
    await expect(page.getByRole('heading', { name: 'Đã gửi yêu cầu tham quan' })).toBeVisible();

    // TC-08: closing does not ask for cancel-confirmation and reopening shows a blank form.
    await page.getByRole('button', { name: 'Đóng' }).click();
    await expect(page.getByText('Hủy form đăng ký?')).toHaveCount(0);
    await expect(page.getByRole('heading', { name: 'Đã gửi yêu cầu tham quan' })).toHaveCount(0);

    await openVisitForm(page);
    await expect(page.getByText('Khôi phục thông tin đã nhập?')).toHaveCount(0);
    await expect(page.locator('input[name="registerInfo.fullName"]')).toHaveValue('');
    await expect(page.getByText('VR-2026-000123')).toHaveCount(0);
    await expect(page.getByText('Trần Thị Khách')).toHaveCount(0);
  });

  test('TC-04: multi-campus overlap requires explicit confirmation before /initiate', async ({ page }) => {
    const initiate = await mockInitiate(page);
    await gotoVi(page);
    await openVisitForm(page);
    await fillValidForm(page);

    // Switch to multi-campus and add an overlapping second campus.
    await page.locator('select[name="visitMode"]').selectOption('multiple');
    await page.getByRole('button', { name: 'Thêm cơ sở' }).click();
    await page.locator('select[name="visits.1.campus"]').selectOption('DN');
    await page.locator('input[name="visits.1.startDatetime"] >> visible=true').fill(localDateTime(5, 10));
    await page.locator('input[name="visits.1.endDatetime"] >> visible=true').fill(localDateTime(5, 14));

    // Submit → confirmation modal; declining must NOT call /initiate.
    await page.getByRole('button', { name: 'Gửi yêu cầu' }).click();
    await expect(page.getByText('Xác nhận lịch trình trùng thời gian')).toBeVisible();
    await page.getByRole('button', { name: 'Kiểm tra lại' }).click();
    expect(initiate.calls).toBe(0);

    // Submit again and accept → exactly one /initiate call, OTP opens.
    await page.getByRole('button', { name: 'Gửi yêu cầu' }).click();
    await expect(page.getByText('Xác nhận lịch trình trùng thời gian')).toBeVisible();
    await page.getByRole('button', { name: 'Vẫn tiếp tục' }).click();
    await expect(page.getByText('Xác thực OTP')).toBeVisible();
    expect(initiate.calls).toBe(1);
  });

  test('TC-09: contact-email conflict after verify returns to the form with a field error', async ({ page }) => {
    await mockInitiate(page);
    await page.route('**/visit-requests/verify', (route) =>
      route.fulfill({
        status: 409,
        json: {
          errorCode: 'CONTACT_EMAIL_CANNOT_BE_USED_FOR_VISITOR_ACCOUNT',
          message: 'Email đầu mối liên hệ không thể dùng để tạo tài khoản Visitor.',
        },
      })
    );

    await gotoVi(page);
    await openVisitForm(page);
    await fillValidForm(page);
    await submitAndOpenOtp(page);
    await enterOtp(page, '135790');

    // OTP modal closes, no success summary, data intact, error pinned to the email field.
    // The errorCode is translated through errors:api.* — the raw backend message is not shown.
    await expect(page.getByText('Xác thực OTP')).toHaveCount(0);
    await expect(page.getByRole('heading', { name: 'Đã gửi yêu cầu tham quan' })).toHaveCount(0);
    await expect(page.locator('input[name="registerInfo.fullName"]')).toHaveValue('Nguyễn Văn Test');
    await expect(
      page
        .getByText('Email đầu mối liên hệ này không thể dùng để tạo tài khoản Visitor. Vui lòng dùng email khác.')
        .first()
    ).toBeVisible();
    await expect(page.locator('input[name="contactPoint.email"]')).toHaveValue('registrant@example.com');
  });

  test('TC-12: 390×844 viewport — full-screen modal, no horizontal scroll, submit reachable', async ({ page }) => {
    await page.setViewportSize({ width: 390, height: 844 });
    await gotoVi(page);
    await openVisitForm(page);

    // The page behind the modal must not scroll horizontally.
    const pageOverflow = await page.evaluate(
      () => document.documentElement.scrollWidth - document.documentElement.clientWidth
    );
    expect(pageOverflow).toBeLessThanOrEqual(0);

    // The modal body is the single scroll area and must not overflow horizontally.
    const bodyOverflow = await page.evaluate(() => {
      const el = document.querySelector('[role="dialog"] .overflow-y-auto');
      return el ? el.scrollWidth - el.clientWidth : -1;
    });
    expect(bodyOverflow).toBeLessThanOrEqual(0);

    // Header and footer stay visible; the CTA is reachable without horizontal scroll.
    await expect(page.getByRole('heading', { name: 'Đăng ký tham quan trường' })).toBeVisible();
    await expect(page.getByRole('button', { name: 'Gửi yêu cầu' })).toBeVisible();

    // The desktop-only visitor table is replaced by the stacked list on mobile.
    await expect(page.locator('#section-visitors table')).toBeHidden();
  });
});
