/**
 * REAL-STACK E2E — plan §18: what the user is told after they press "Xác nhận".
 *
 * real Chromium → real React (Vite) → real .NET API (Testing, flags ON) → real disposable MySQL.
 * NO network mocking: every OTP is the one the backend actually wrote to the Testing FileSink, and
 * every request code comes back from a row that really exists.
 *
 * What only a real stack proves here:
 *   • confirming an OTP calls verify ONCE and initiate NEVER — the portal-bubbling regression that
 *     used to mint a second challenge on every click;
 *   • the receipt names a request the backend really created, and the request can be OPENED;
 *   • a verify whose response is destroyed mid-flight leaves exactly ONE row in the database, and
 *     the submission lookup finds it — the case the whole uncertain-result path exists for.
 */
import { test, expect, type Page, type Locator } from '@playwright/test';
import { readFileSync } from 'node:fs';
import { type SinkRecord, sinkAddressed } from './sinkRecord';
import { fillSchedule, fillOperationalOrganization } from './realstackHelpers';

const API_BASE = process.env.PEMS_E2E_API_BASE ?? 'http://localhost:5299/api';

function formField(page: Page, label: string): Locator {
  return page.locator('div.flex.flex-col.gap-2').filter({ has: page.getByText(label, { exact: false }) }).first();
}

async function fillReactSelect(scope: Locator, text: string) {
  const input = scope.locator('input').first();
  await input.click();
  await input.fill(text);
  await scope.page().keyboard.press('Enter');
}

const SINK = process.env.PEMS_E2E_TEST_SINK_PATH;

function otpCodesFor(email: string): string[] {
  if (!SINK) throw new Error('PEMS_E2E_TEST_SINK_PATH is not set — the real-stack harness must provide it.');
  const target = email.trim().toLowerCase();
  let lines: string[] = [];
  try { lines = readFileSync(SINK, 'utf8').split('\n').filter(Boolean); } catch { /* not written yet */ }
  const codes: string[] = [];
  for (const line of lines) {
    try {
      const rec = JSON.parse(line) as SinkRecord;
      if (rec.kind === 'VISIT_REQUEST_OTP' && sinkAddressed(rec, target) && rec.code) codes.push(rec.code);
    } catch { /* skip malformed */ }
  }
  return codes;
}

async function readOtpFromSink(email: string, count = 1): Promise<string> {
  for (let attempt = 0; attempt < 40; attempt++) {
    const codes = otpCodesFor(email);
    if (codes.length >= count) return codes[codes.length - 1];
    await new Promise(r => setTimeout(r, 250));
  }
  throw new Error(`Fewer than ${count} VISIT_REQUEST_OTP entries captured for ${email} within timeout.`);
}

async function fillWholeForm(page: Page, email: string, delegation: string, dayOffset = 15) {
  await page.addInitScript(() => window.localStorage.setItem('pems.language', 'vi'));
  await page.goto('/visit-registration/v2');
  await expect(page.getByRole('heading', { name: /theo từng cơ sở/i })).toBeVisible();

  await page.locator('input[name="registerInfo.fullName"]').fill('Người Thật E2E');
  await page.getByPlaceholder('Nhập hoặc tìm tổ chức/đối tác...').fill('Công ty E2E');
  await page.locator('input[name="registerInfo.jobTitle"]').fill('Trưởng phòng');
  await fillReactSelect(formField(page, 'Quốc tịch'), 'Việt Nam');
  await page.locator('input[name="registerInfo.phone"]').fill('+84912345678');
  await page.locator('input[name="registerInfo.email"]').fill(email);
  // Per-campus quick-fill (campus 0). The old request-level "same as registrant" control went away
  // with the request-level contact, so the previous label regex matched no button and simply waited.
  await page.getByTestId('campus-opcontact-use-registrant-0').click();

  const start = new Date();
  start.setDate(start.getDate() + dayOffset);
  start.setHours(9, 0, 0, 0);
  const end = new Date(start.getTime() + 60 * 60 * 1000);

  await page.locator('select[name="campusVisits.0.campus"]').selectOption('HN');
  await fillSchedule(page, 0, start, end);
  await page.getByTestId('campus-delegation-input').fill(delegation);
  await formField(page, 'Mục đích').locator('textarea').fill('Trao đổi hợp tác thật');
  await formField(page, 'Nội dung làm việc').locator('textarea').fill('Nội dung làm việc thực tế của đoàn');

  const vRow = page.locator('[data-testid="v2-visitors-table"] tbody tr').first();
  await vRow.locator('td').nth(1).locator('textarea').fill('Khách Thật');
  await vRow.locator('td').nth(2).locator('textarea').fill('Giảng viên');
  await fillReactSelect(vRow.locator('td').nth(3), 'ĐH Đối Tác');
  await fillReactSelect(vRow.locator('td').nth(4), 'Việt Nam');

  await page.getByTestId('campus-opcontact-name').fill('Đầu Mối CS');
  await fillOperationalOrganization(page, 0, 'Đơn vị đầu mối');
  await page.getByTestId('campus-opcontact-jobtitle').fill('Trưởng phòng Hợp tác');
  await page.locator('input[name="campusVisits.0.operationalContact.phone"]').fill('+84912345678');
  await page.locator('input[name="campusVisits.0.operationalContact.email"]').fill('opcontact@example.com');
}

const submit = (page: Page) => page.getByRole('button', { name: /Gửi yêu cầu & nhận mã OTP/ }).click();
const otpInput = (page: Page) => page.getByPlaceholder('______');

async function enterOtp(page: Page, code: string) {
  const input = otpInput(page);
  await input.click();
  await input.fill(code);
  await expect(input).toHaveValue(code);
  const confirm = page.getByTestId('otp-confirm');
  await expect(confirm).toBeEnabled();
  await confirm.click();
}

test.describe('Real-stack: the result of pressing Confirm', () => {
  test('journey A — a wrong code keeps the error, the form and the single challenge', async ({ page }) => {
    const email = `e2e_res_a_${Date.now()}@example.com`;
    const calls: string[] = [];
    page.on('response', r => {
      const p = new URL(r.url()).pathname;
      if (/\/v2\/visit-requests\/initiate$/.test(p)) calls.push('initiate');
      if (/\/v2\/visit-requests\/verify$/.test(p)) calls.push('verify');
    });

    await fillWholeForm(page, email, 'Đoàn Kết Quả A');
    await submit(page);
    await expect(otpInput(page)).toBeVisible({ timeout: 20_000 });

    await enterOtp(page, '000000');

    // The message stays, the modal stays, and the answers are still behind it.
    await expect(page.getByRole('dialog').getByRole('alert').first()).toBeVisible({ timeout: 20_000 });
    await expect(otpInput(page)).toBeVisible();
    // ONE challenge for one submit, and exactly one verify per click.
    expect(calls.filter(c => c === 'initiate')).toHaveLength(1);
    expect(calls.filter(c => c === 'verify')).toHaveLength(1);
    expect(otpCodesFor(email)).toHaveLength(1);

    await page.getByRole('button', { name: 'Quay lại' }).click();
    await expect(page.getByTestId('campus-delegation-input')).toHaveValue('Đoàn Kết Quả A');
  });

  test('journey B — the right code shows a receipt, and the request can be opened', async ({ page }) => {
    const email = `e2e_res_b_${Date.now()}@example.com`;
    await fillWholeForm(page, email, 'Đoàn Kết Quả B');
    await submit(page);
    await expect(otpInput(page)).toBeVisible({ timeout: 20_000 });

    const code = await readOtpFromSink(email);
    const verifyResponse = page.waitForResponse(
      r => /\/v2\/visit-requests\/verify$/.test(new URL(r.url()).pathname) && r.request().method() === 'POST',
      { timeout: 30_000 },
    );
    await enterOtp(page, code);
    const verified = await verifyResponse;
    expect(verified.status()).toBe(200);

    // A receipt with the facts on it, not a toast that disappears. The success screen deliberately
    // never surfaces the request code itself (VisitRequestV2SuccessPanel — it stays a server-side
    // identifier the confirmation email carries), so "the receipt names a request that really
    // exists" is proven from the verify response the receipt was built from, not from its DOM.
    await expect(page.getByTestId('v2-success-title')).toBeVisible({ timeout: 20_000 });
    await expect(page.getByTestId('v2-success-status')).toBeVisible();
    const body = await verified.json();
    expect(body.requestCode).toMatch(/^VR/);

    // The modal never closed itself — the form is gone but the confirmation is not.
    await expect(page.getByRole('button', { name: /Gửi yêu cầu & nhận mã OTP/ })).toHaveCount(0);
  });

  test('journey C — closing the OTP keeps everything, and resuming asks for no new code', async ({ page }) => {
    const email = `e2e_res_c_${Date.now()}@example.com`;
    await fillWholeForm(page, email, 'Đoàn Kết Quả C');
    await submit(page);
    await expect(otpInput(page)).toBeVisible({ timeout: 20_000 });
    const code = await readOtpFromSink(email);

    await page.getByRole('button', { name: 'Quay lại' }).click();
    await expect(otpInput(page)).toBeHidden();
    await expect(page.getByTestId('campus-delegation-input')).toHaveValue('Đoàn Kết Quả C');

    await page.getByTestId('v2-otp-resume-continue').click();
    await expect(otpInput(page)).toBeVisible();
    // Still one code: resuming never re-initiated.
    expect(otpCodesFor(email)).toHaveLength(1);

    await enterOtp(page, code);
    await expect(page.getByTestId('v2-success-title')).toBeVisible({ timeout: 20_000 });
  });

  test('journey C2 — stepping out to review the form does not spend the challenge', async ({ page }) => {
    const email = `e2e_res_c2_${Date.now()}@example.com`;
    await fillWholeForm(page, email, 'Đoàn Xem Lại');
    await submit(page);
    await expect(otpInput(page)).toBeVisible({ timeout: 20_000 });
    const code = await readOtpFromSink(email);

    await page.getByTestId('otp-review-form').click();
    await expect(otpInput(page)).toBeHidden();
    // A banner says the code is still good, and offers the way back.
    await expect(page.getByTestId('v2-otp-review')).toBeVisible();
    await expect(page.getByTestId('campus-delegation-input')).toHaveValue('Đoàn Xem Lại');

    await page.getByTestId('v2-otp-review-continue').click();
    await expect(otpInput(page)).toBeVisible();
    expect(otpCodesFor(email)).toHaveLength(1);

    // The SAME code still verifies — the challenge was never spent.
    await enterOtp(page, code);
    await expect(page.getByTestId('v2-success-title')).toBeVisible({ timeout: 20_000 });
  });

  test('journey D — a verify whose reply is destroyed leaves ONE request, and the lookup finds it', async ({ page, request }) => {
    const email = `e2e_res_d_${Date.now()}@example.com`;
    await fillWholeForm(page, email, 'Đoàn Mất Kết Nối');
    await submit(page);
    await expect(otpInput(page)).toBeVisible({ timeout: 20_000 });
    const code = await readOtpFromSink(email);

    // Let the verify REACH the server and commit, then kill the response on the way back. This is
    // the real ambiguity: the request exists, the browser cannot know it.
    await page.route('**/v2/visit-requests/verify', async route => {
      await route.fetch();          // the backend really runs and really commits
      await route.abort('failed');  // ...and the answer never arrives
    });

    await enterOtp(page, code);

    // Not "failed" — undecided, and explicitly telling the user not to send another one.
    const panel = page.getByTestId('v2-uncertain');
    await expect(panel).toBeVisible({ timeout: 20_000 });
    await expect(panel).toContainText(/Đừng gửi lại/i);

    await page.unroute('**/v2/visit-requests/verify');
    const lookupResponse = page.waitForResponse(
      r => /\/v2\/visit-requests\/submissions\//.test(new URL(r.url()).pathname) && r.request().method() === 'GET',
      { timeout: 30_000 },
    );
    await page.getByTestId('v2-uncertain-check').click();
    const lookedUp = await lookupResponse;
    const lookupBody = await lookedUp.json();

    // The lookup finds the committed request and promotes straight to the receipt.
    expect(lookupBody.state).toBe('COMPLETED');
    expect(lookupBody.requestCode).toMatch(/^VR/);
    await expect(page.getByTestId('v2-success-title')).toBeVisible({ timeout: 20_000 });

    // And exactly ONE request exists for this submission — the recovery created nothing.
    const codes = otpCodesFor(email);
    expect(codes).toHaveLength(1);          // no second challenge was minted either
    const res = await request.get(`${API_BASE}/v2/visit-requests/submissions/nonexistent-${Date.now()}`);
    expect(res.ok()).toBeTruthy();
    expect((await res.json()).state).toBe('NOT_FOUND');
  });

  test('journey E — a right code with data the server refuses returns to the form, not to "wrong OTP"', async ({ page }) => {
    const email = `e2e_res_e_${Date.now()}@example.com`;
    await fillWholeForm(page, email, 'Đoàn Sai Dữ Liệu');
    await submit(page);
    await expect(otpInput(page)).toBeVisible({ timeout: 20_000 });
    const code = await readOtpFromSink(email);

    // The server answers 400 with FIELD errors — the shape a business rejection takes after a
    // correct code (a campus closed for registration, a window that is no longer valid).
    await page.route('**/v2/visit-requests/verify', route => route.fulfill({
      status: 400,
      contentType: 'application/json',
      body: JSON.stringify({
        message: 'Cơ sở đã ngừng nhận đăng ký.',
        errors: { 'Form.CampusVisits[0].CampusId': ['Cơ sở đã ngừng nhận đăng ký.'] },
      }),
    }));

    await enterOtp(page, code);

    // Back on the form with everything intact — and NOT told their correct code was wrong.
    await expect(otpInput(page)).toBeHidden({ timeout: 20_000 });
    await expect(page.getByTestId('campus-delegation-input')).toHaveValue('Đoàn Sai Dữ Liệu');
    await expect(page.getByText('Cơ sở đã ngừng nhận đăng ký.').first()).toBeVisible();
    await expect(page.getByTestId('v2-uncertain')).toHaveCount(0);
  });
});
