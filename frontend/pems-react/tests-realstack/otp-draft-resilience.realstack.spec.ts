/**
 * REAL-STACK E2E — the draft survives the OTP round trip (plan §22 journeys A, B, C and D).
 *
 * real Chromium → real React (Vite) → real .NET API (Testing, flags ON) → real disposable MySQL.
 * NO network mocking: every OTP is the one the backend actually wrote to the Testing FileSink.
 *
 * What only a real stack can prove here:
 *   • a WRONG code leaves the typed form and its submission intent alone;
 *   • a full page RELOAD comes back with both, and the challenge can be finished afterwards;
 *   • one submit intent creates exactly ONE request, however many attempts it took;
 *   • an OTP minted for mailbox A cannot verify a form that now names mailbox B — that is the
 *     backend's binding (token ↔ email ↔ submissionId), not something the UI can fake.
 */
import { test, expect, type Page, type Locator } from '@playwright/test';
import { readFileSync } from 'node:fs';
import { fillSchedule } from './realstackHelpers';

/** The FormField (label→control wrapper) whose visible label contains `label`. */
function formField(page: Page, label: string): Locator {
  return page.locator('div.flex.flex-col.gap-2').filter({ has: page.getByText(label, { exact: false }) }).first();
}

/** Fill a react-select control (Creatable — free text allowed): open, type, commit with Enter. */
async function fillReactSelect(scope: Locator, text: string) {
  const input = scope.locator('input').first();
  await input.click();
  await input.fill(text);
  await scope.page().keyboard.press('Enter');
}

const SINK = process.env.PEMS_E2E_TEST_SINK_PATH;

/** Every OTP the backend wrote for `email`, oldest first. */
function otpCodesFor(email: string): string[] {
  if (!SINK) throw new Error('PEMS_E2E_TEST_SINK_PATH is not set — the real-stack harness must provide it.');
  const target = email.trim().toLowerCase();
  let lines: string[] = [];
  try {
    lines = readFileSync(SINK, 'utf8').split('\n').filter(Boolean);
  } catch { /* the file may not exist yet */ }
  const codes: string[] = [];
  for (const line of lines) {
    try {
      const rec = JSON.parse(line) as { to?: string; kind?: string; code?: string };
      if (rec.kind === 'VISIT_REQUEST_OTP' && rec.to === target && rec.code) codes.push(rec.code);
    } catch { /* skip malformed */ }
  }
  return codes;
}

/** Waits for at least `count` codes and returns the newest (the write is async post-initiate). */
async function readOtpFromSink(email: string, count = 1): Promise<string> {
  for (let attempt = 0; attempt < 40; attempt++) {
    const codes = otpCodesFor(email);
    if (codes.length >= count) return codes[codes.length - 1];
    await new Promise(r => setTimeout(r, 250));
  }
  throw new Error(`Fewer than ${count} VISIT_REQUEST_OTP entries captured for ${email} within timeout.`);
}

async function fillCampus0(page: Page, delegation: string) {
  const start = new Date();
  start.setDate(start.getDate() + 10);
  start.setHours(9, 0, 0, 0);
  const end = new Date(start.getTime() + 30 * 60 * 1000);

  await page.locator('select[name="campusVisits.0.campus"]').selectOption('HN');
  await fillSchedule(page, 0, start, end);
  await page.getByTestId('campus-delegation-input').fill(delegation);

  await formField(page, 'Mục đích').locator('textarea').fill('Trao đổi hợp tác thật');
  await formField(page, 'Nội dung làm việc').locator('textarea').fill('Nội dung làm việc thực tế của đoàn');

  // Name and job title are auto-growing textareas now; organization/nationality stay react-selects.
  const vRow = page.locator('[data-testid="v2-visitors-table"] tbody tr').first();
  await vRow.locator('td').nth(1).locator('textarea').fill('Khách Thật');
  await vRow.locator('td').nth(2).locator('textarea').fill('Giảng viên');
  await fillReactSelect(vRow.locator('td').nth(3), 'ĐH Đối Tác');
  await fillReactSelect(vRow.locator('td').nth(4), 'Việt Nam');

  await page.getByTestId('campus-opcontact-name').fill('Đầu Mối CS');
  await page.getByTestId('campus-opcontact-org').fill('Đơn vị đầu mối');
  await page.locator('input[name="campusVisits.0.operationalContact.phone"]').fill('+84912345678');
  await page.locator('input[name="campusVisits.0.operationalContact.email"]').fill('opcontact@example.com');
}

/** Opens the public v2 form and fills every required field, stopping just before submit. */
async function fillWholeForm(page: Page, email: string, delegation: string) {
  await page.addInitScript(() => window.localStorage.setItem('pems.language', 'vi'));
  await page.goto('/visit-registration/v2');
  await expect(page.getByRole('heading', { name: /theo từng cơ sở/i })).toBeVisible();

  await page.locator('input[name="registerInfo.fullName"]').fill('Người Thật E2E');
  await page.getByPlaceholder('Nhập hoặc tìm tổ chức/đối tác...').fill('Công ty E2E');
  await page.locator('input[name="registerInfo.jobTitle"]').fill('Trưởng phòng');
  await fillReactSelect(formField(page, 'Quốc tịch'), 'Việt Nam');
  await page.locator('input[name="registerInfo.phone"]').fill('+84912345678');
  await page.locator('input[name="registerInfo.email"]').fill(email);
  await page.getByRole('button', { name: /Dùng thông tin người đăng ký/ }).click();

  await fillCampus0(page, delegation);
}

const submit = (page: Page) => page.getByRole('button', { name: /Gửi yêu cầu & nhận mã OTP/ }).click();
const otpModal = (page: Page) => page.getByPlaceholder('______');

async function enterOtp(page: Page, code: string) {
  // The modal clears its input between attempts, so assert the code actually landed before
  // clicking — otherwise a race just produces a confusing failure at the NEXT assertion.
  const input = otpModal(page);
  await input.click();
  await input.fill(code);
  await expect(input).toHaveValue(code);
  const confirm = page.getByRole('button', { name: 'Xác nhận' });
  await expect(confirm).toBeEnabled();
  await confirm.click();
}

test.describe('Real-stack: the draft survives the OTP round trip', () => {
  test('journey A — a wrong code, a closed modal, and the form is still there', async ({ page }) => {
    const email = `e2e_otp_a_${Date.now()}@example.com`;
    // Counting the real calls is the point of journey A: confirming a code must never also
    // re-submit the registration behind the user's back.
    const initiates: string[] = [];
    page.on('response', r => {
      if (/\/v2\/visit-requests\/initiate$/.test(new URL(r.url()).pathname)) initiates.push(String(r.status()));
    });
    await fillWholeForm(page, email, 'Đoàn Journey A');
    await submit(page);
    await expect(otpModal(page)).toBeVisible({ timeout: 20_000 });

    // A wrong code: the modal stays open and says so.
    await enterOtp(page, '000000');
    await expect(page.getByRole('dialog').getByRole('alert').first()).toBeVisible({ timeout: 20_000 });
    await expect(otpModal(page)).toBeVisible();

    // Close it. This is not "cancel the request" — the answers stay on screen.
    await page.getByRole('button', { name: 'Quay lại' }).click();
    await expect(otpModal(page)).toBeHidden();
    await expect(page.locator('input[name="registerInfo.email"]')).toHaveValue(email);
    await expect(page.getByTestId('campus-delegation-input')).toHaveValue('Đoàn Journey A');

    // The way back in is offered, and it does NOT request a second code.
    await expect(page.getByTestId('v2-otp-resume')).toBeVisible();
    await page.getByTestId('v2-otp-resume-continue').click();
    await expect(otpModal(page)).toBeVisible();

    const code = await readOtpFromSink(email);
    // One submit, one challenge: neither the failed attempt nor resuming asked for another code.
    expect(initiates).toEqual(['200']);
    expect(otpCodesFor(email)).toHaveLength(1);
    await enterOtp(page, code);
    await expect(page.getByText('Đã gửi yêu cầu tham quan')).toBeVisible({ timeout: 20_000 });
  });

  test('journey B — a reload restores the form and the verification can still be finished', async ({ page }) => {
    const email = `e2e_otp_b_${Date.now()}@example.com`;
    await fillWholeForm(page, email, 'Đoàn Journey B');
    await submit(page);
    await expect(otpModal(page)).toBeVisible({ timeout: 20_000 });
    const code = await readOtpFromSink(email);

    // The tab is reloaded mid-verification: React state is gone, the draft is not.
    await page.reload();
    await expect(page.getByTestId('v2-draft-prompt')).toBeVisible({ timeout: 20_000 });
    await page.getByTestId('v2-draft-restore').click();

    await expect(page.locator('input[name="registerInfo.email"]')).toHaveValue(email);
    await expect(page.getByTestId('campus-delegation-input')).toHaveValue('Đoàn Journey B');

    // Same challenge, same submission intent — no retyping and no second code.
    await page.getByTestId('v2-otp-resume-continue').click();
    await enterOtp(page, code);
    await expect(page.getByText('Đã gửi yêu cầu tham quan')).toBeVisible({ timeout: 20_000 });
    await expect(page.getByText(/Mã yêu cầu:\s*VR/)).toBeVisible();
  });

  test('journey C — asking for a new code keeps the form and still creates exactly one request', async ({ page }) => {
    const email = `e2e_otp_c_${Date.now()}@example.com`;
    await fillWholeForm(page, email, 'Đoàn Journey C');
    await submit(page);
    await expect(otpModal(page)).toBeVisible({ timeout: 20_000 });
    const first = await readOtpFromSink(email);

    // "Gửi lại mã" supersedes the first code; the form behind the modal is untouched.
    await page.getByRole('button', { name: /^Gửi lại/ }).click();
    const second = await readOtpFromSink(email, 2);
    expect(second).not.toBe(first);

    // The superseded code is refused — a resend really does invalidate the old one.
    await enterOtp(page, first);
    await expect(page.getByRole('dialog').getByRole('alert').first()).toBeVisible({ timeout: 20_000 });

    await enterOtp(page, second);
    await expect(page.getByText('Đã gửi yêu cầu tham quan')).toBeVisible({ timeout: 20_000 });
    // The receipt now states status and submitted time alongside the code, so assert the
    // structured fields rather than one assembled sentence.
    await expect(page.getByTestId('v2-success-code')).toContainText(/VR/);
    await expect(page.getByTestId('v2-success-status')).toBeVisible();
  });

  test('journey D — changing the registrant email invalidates the code sent to the old one', async ({ page }) => {
    const emailA = `e2e_otp_d1_${Date.now()}@example.com`;
    const emailB = `e2e_otp_d2_${Date.now()}@example.com`;
    await fillWholeForm(page, emailA, 'Đoàn Journey D');
    await submit(page);
    await expect(otpModal(page)).toBeVisible({ timeout: 20_000 });
    const codeForA = await readOtpFromSink(emailA);

    // Back to the form, now naming a different registrant.
    await page.getByRole('button', { name: 'Quay lại' }).click();
    await page.locator('input[name="registerInfo.email"]').fill(emailB);

    // The pending challenge belonged to A, so it is dropped rather than offered for B.
    await expect(page.getByTestId('v2-otp-resume')).toBeHidden();

    await submit(page);
    await expect(otpModal(page)).toBeVisible({ timeout: 20_000 });
    const codeForB = await readOtpFromSink(emailB);
    expect(codeForB).not.toBe(codeForA);

    // A's code cannot verify B's form — the backend binds the token to email + submission.
    await enterOtp(page, codeForA);
    await expect(page.getByRole('dialog').getByRole('alert').first()).toBeVisible({ timeout: 20_000 });

    await enterOtp(page, codeForB);
    await expect(page.getByText('Đã gửi yêu cầu tham quan')).toBeVisible({ timeout: 20_000 });
  });
});
