/**
 * REAL-STACK E2E — plan §24: the receipt an anonymous visitor actually sees, and the per-campus
 * operational contact.
 *
 * real Chromium → real React (Vite) → real .NET API (Testing, flags ON) → real disposable MySQL.
 * NO network mocking: every OTP is the one the backend wrote to the Testing FileSink, and every
 * request code comes back from a row that really exists.
 *
 * Journey A goes through the HOME PAGE CTA rather than the /visit-registration/v2 route, because
 * that is where the defect lived: the route rendered the receipt correctly, while the CTA modal —
 * the entry point every public user actually takes — was closed by its host in the same tick as
 * the receipt appeared. Only a journey that clicks the real button can prove that is gone.
 */
import { test, expect, type Page, type Locator } from '@playwright/test';
import { readFileSync } from 'node:fs';
import { type SinkRecord, sinkAddressed } from './sinkRecord';
import { fillSchedule, fillOperationalOrganization } from './realstackHelpers';

const API_BASE = process.env.PEMS_E2E_API_BASE ?? 'http://localhost:5299/api';
const SINK = process.env.PEMS_E2E_TEST_SINK_PATH;

function formField(page: Page, label: string): Locator {
  return page.locator('div.flex.flex-col.gap-2').filter({ has: page.getByText(label, { exact: false }) }).first();
}

async function fillReactSelect(scope: Locator, text: string) {
  const input = scope.locator('input').first();
  await input.click();
  await input.fill(text);
  await scope.page().keyboard.press('Enter');
}

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

async function readOtpFromSink(email: string): Promise<string> {
  for (let attempt = 0; attempt < 40; attempt++) {
    const codes = otpCodesFor(email);
    if (codes.length >= 1) return codes[codes.length - 1];
    await new Promise(r => setTimeout(r, 250));
  }
  throw new Error(`No VISIT_REQUEST_OTP captured for ${email} within timeout.`);
}

const otpInput = (page: Page) => page.getByPlaceholder('______');

async function enterOtp(page: Page, code: string) {
  const input = otpInput(page);
  await input.click();
  await input.fill(code);
  await expect(input).toHaveValue(code);
  const confirm = page.getByRole('button', { name: 'Xác nhận' });
  await expect(confirm).toBeEnabled();
  await confirm.click();
}

/** Fills the registrant + primary-contact block. Leaves the campus card to the caller. */
async function fillHeader(page: Page, email: string) {
  await page.locator('input[name="registerInfo.fullName"]').fill('Người Thật E2E');
  await page.getByPlaceholder('Nhập hoặc tìm tổ chức/đối tác...').fill('Công ty E2E');
  await page.locator('input[name="registerInfo.jobTitle"]').fill('Trưởng phòng');
  await fillReactSelect(formField(page, 'Quốc tịch'), 'Việt Nam');
  await page.getByTestId('v2-registrant-phone').fill('+84912345678');
  await page.locator('input[name="registerInfo.email"]').fill(email);
  await page.getByRole('button', { name: /Dùng thông tin người đăng ký/ }).first().click();
}

/** Everything on campus card 0 except the operational contact, which each journey drives itself. */
async function fillCampusBody(page: Page, delegation: string, dayOffset = 16) {
  const start = new Date();
  start.setDate(start.getDate() + dayOffset);
  start.setHours(9, 0, 0, 0);
  const end = new Date(start.getTime() + 60 * 60 * 1000);

  await page.locator('select[name="campusVisits.0.campus"]').selectOption('HN');
  await fillSchedule(page, 0, start, end);
  await page.getByTestId('campus-delegation-input').fill(delegation);
  await formField(page, 'Mục đích').locator('textarea').fill('Trao đổi hợp tác thật');
  await formField(page, 'Nội dung làm việc').locator('textarea').fill('Nội dung làm việc thực tế của đoàn');

  const row = page.locator('[data-testid="v2-visitors-table"] tbody tr').first();
  await row.locator('td').nth(1).locator('textarea').fill('Khách Thật');
  await row.locator('td').nth(2).locator('textarea').fill('Giảng viên');
  await fillReactSelect(row.locator('td').nth(3), 'ĐH Đối Tác');
  await fillReactSelect(row.locator('td').nth(4), 'Việt Nam');
}

async function fillOperationalContactByHand(page: Page) {
  await page.getByTestId('campus-opcontact-name').fill('Đầu Mối CS');
  await fillOperationalOrganization(page, 0, 'Đơn vị đầu mối');
  await page.getByTestId('campus-opcontact-phone-0').fill('+84912345678');
  await page.locator('input[name="campusVisits.0.operationalContact.email"]').fill('opcontact@example.com');
}

const submit = (page: Page) => page.getByRole('button', { name: /Gửi yêu cầu & nhận mã OTP/ }).click();

test.describe('Real-stack: the public receipt and the per-campus contact', () => {
  test('journey A — the CTA modal keeps the receipt, and the request really exists', async ({ page, request }) => {
    const email = `e2e_receipt_a_${Date.now()}@example.com`;

    // Capture the submission intent as it leaves, so the request can be looked up afterwards
    // through the real anonymous endpoint rather than trusted from the screen alone.
    let submissionId: string | null = null;
    page.on('request', r => {
      if (/\/v2\/visit-requests\/initiate$/.test(new URL(r.url()).pathname)) {
        // The initiate body NESTS the form: { form: { submissionId, ... } }.
        try { submissionId = JSON.parse(r.postData() ?? '{}').form?.submissionId ?? null; } catch { /* ignore */ }
      }
    });

    await page.addInitScript(() => window.localStorage.setItem('pems.language', 'vi'));
    await page.goto('/');
    // The real entry point: the home hero CTA, which opens the v2 form in a MODAL.
    await page.getByRole('button', { name: 'Đăng ký tham quan' }).first().click();
    await expect(page.getByTestId('v2-create-modal')).toBeVisible({ timeout: 20_000 });

    await fillHeader(page, email);
    await fillCampusBody(page, 'Đoàn Biên Lai');
    await fillOperationalContactByHand(page);

    await submit(page);
    await expect(otpInput(page)).toBeVisible({ timeout: 20_000 });
    await enterOtp(page, await readOtpFromSink(email));

    // THE regression: the modal is still here, with a receipt in it.
    await expect(page.getByTestId('v2-create-modal')).toBeVisible({ timeout: 20_000 });
    const codeEl = page.getByTestId('v2-success-code');
    await expect(codeEl).toBeVisible();
    await expect(codeEl).toContainText(/VR/);
    await expect(page.getByTestId('v2-success-status')).toBeVisible();
    await expect(page.getByTestId('v2-success-submitted-at')).toBeVisible();
    // No dashboard action for someone with no session to use it with.
    await expect(page.getByTestId('v2-success-view')).toHaveCount(0);

    // "Xem lại thông tin đã gửi" renders the snapshot that was submitted.
    await page.getByTestId('v2-success-review').click();
    const summary = page.getByTestId('campus-summary-0');
    await expect(summary).toContainText('Đoàn Biên Lai');
    await expect(summary).toContainText('Khách Thật');
    await expect(summary).toContainText('Đơn vị đầu mối');

    // One submission, one request — confirmed against the database through the real lookup.
    expect(otpCodesFor(email)).toHaveLength(1);
    expect(submissionId).toBeTruthy();
    const lookup = await request.get(`${API_BASE}/v2/visit-requests/submissions/${submissionId}`);
    expect(lookup.ok()).toBeTruthy();
    const body = await lookup.json();
    expect(body.state).toBe('COMPLETED');
    expect(await codeEl.textContent()).toContain(body.requestCode);
  });

  test('journey B — quick-fill copies the registrant, and the combobox choice is what gets stored', async ({ page, request }) => {
    const email = `e2e_receipt_b_${Date.now()}@example.com`;
    let submissionId: string | null = null;
    page.on('request', r => {
      if (/\/v2\/visit-requests\/initiate$/.test(new URL(r.url()).pathname)) {
        // The initiate body NESTS the form: { form: { submissionId, ... } }.
        try { submissionId = JSON.parse(r.postData() ?? '{}').form?.submissionId ?? null; } catch { /* ignore */ }
      }
    });

    await page.addInitScript(() => window.localStorage.setItem('pems.language', 'vi'));
    await page.goto('/visit-registration/v2');
    await expect(page.getByRole('heading', { name: /theo từng cơ sở/i })).toBeVisible();

    await fillHeader(page, email);
    await fillCampusBody(page, 'Đoàn Sao Chép');

    // Nothing typed into the campus contact yet, so the copy applies straight away.
    await page.getByTestId('campus-opcontact-use-registrant-0').click();
    await expect(page.getByTestId('campus-opcontact-name')).toHaveValue('Người Thật E2E');
    await expect(page.getByTestId('campus-opcontact-phone-0')).toHaveValue('+84912345678');
    await expect(page.locator('input[name="campusVisits.0.operationalContact.email"]')).toHaveValue(email);
    await expect(page.getByTestId('campus-opcontact-org')).toContainText('Công ty E2E');

    // Now REPLACE the organization through the combobox — the destination has data, so the second
    // half of §13 applies: the user is asked before anything is overwritten.
    await fillOperationalOrganization(page, 0, 'Ban Hợp Tác Quốc Tế');

    await submit(page);
    await expect(otpInput(page)).toBeVisible({ timeout: 20_000 });
    await enterOtp(page, await readOtpFromSink(email));
    await expect(page.getByTestId('v2-success-code')).toBeVisible({ timeout: 20_000 });

    // What the server stored is the combobox value, not the copied one.
    await page.getByTestId('v2-success-review').click();
    const summary = page.getByTestId('campus-summary-0');
    await expect(summary).toContainText('Người Thật E2E');
    await expect(summary).toContainText('Ban Hợp Tác Quốc Tế');

    expect(submissionId).toBeTruthy();
    const lookup = await request.get(`${API_BASE}/v2/visit-requests/submissions/${submissionId}`);
    expect((await lookup.json()).state).toBe('COMPLETED');
  });

  test('journey B2 — quick-fill never overwrites typed details without asking', async ({ page }) => {
    await page.addInitScript(() => window.localStorage.setItem('pems.language', 'vi'));
    await page.goto('/visit-registration/v2');
    await fillHeader(page, `e2e_receipt_b2_${Date.now()}@example.com`);

    await page.getByTestId('campus-opcontact-name').fill('Người Đã Nhập Tay');
    await page.getByTestId('campus-opcontact-use-registrant-0').click();

    // The question comes first, and the typed value is still there while it is unanswered.
    await expect(page.getByTestId('campus-opcontact-replace-confirm-0')).toBeVisible();
    await expect(page.getByTestId('campus-opcontact-name')).toHaveValue('Người Đã Nhập Tay');

    await page.getByTestId('campus-opcontact-replace-yes-0').click();
    await expect(page.getByTestId('campus-opcontact-name')).toHaveValue('Người Thật E2E');
  });

  test('journey C — an invalid phone says what a valid one looks like, and the fix goes through', async ({ page }) => {
    const email = `e2e_receipt_c_${Date.now()}@example.com`;
    await page.addInitScript(() => window.localStorage.setItem('pems.language', 'vi'));
    await page.goto('/visit-registration/v2');

    await fillHeader(page, email);
    await fillCampusBody(page, 'Đoàn Số Điện Thoại');
    await fillOperationalContactByHand(page);
    await page.getByTestId('campus-opcontact-phone-0').fill('090abc');

    await submit(page);

    // Not "invalid": the message names the field and states both accepted shapes.
    const message = page.getByText(/Số điện thoại đầu mối phối hợp không hợp lệ/);
    await expect(message).toBeVisible({ timeout: 10_000 });
    await expect(message).toContainText('0912345678');
    await expect(message).toContainText('+84912345678');
    // The caret is ON the bad field, not left for the user to find.
    await expect(page.getByTestId('campus-opcontact-phone-0')).toBeFocused();
    // …and the banner says how much is left rather than repeating a generic sentence.
    await expect(page.getByText(/Còn \d+ trường cần kiểm tra/)).toBeVisible();

    // Corrected to E.164, the same submit now reaches the OTP step.
    await page.getByTestId('campus-opcontact-phone-0').fill('+84912345678');
    await submit(page);
    await expect(otpInput(page)).toBeVisible({ timeout: 20_000 });
  });

  test('journey C2 — the counter appears on focus and turns red on an over-long paste', async ({ page }) => {
    await page.addInitScript(() => window.localStorage.setItem('pems.language', 'vi'));
    await page.goto('/visit-registration/v2');
    await fillHeader(page, `e2e_receipt_c2_${Date.now()}@example.com`);
    await page.locator('select[name="campusVisits.0.campus"]').selectOption('HN');

    const transport = formField(page, 'Nhận diện phương tiện di chuyển').locator('textarea');
    // Nothing is counted on a form nobody has started filling in.
    await expect(formField(page, 'Nhận diện phương tiện di chuyển').getByText('0/2000')).toHaveCount(0);

    await transport.focus();
    await expect(formField(page, 'Nhận diện phương tiện di chuyển').getByText('0/2000')).toBeVisible();

    await transport.fill('x'.repeat(2014));
    // The value is kept in full — the user can see what to cut.
    await expect(transport).toHaveValue('x'.repeat(2014));
    await expect(formField(page, 'Nhận diện phương tiện di chuyển').getByText('2014/2000')).toBeVisible();

    await submit(page);
    await expect(page.getByText(/Nhận diện phương tiện di chuyển không được vượt quá 2\.000 ký tự/)).toBeVisible({ timeout: 10_000 });
    // Still on the form: an over-long field is never quietly trimmed to make the submit succeed.
    await expect(otpInput(page)).toHaveCount(0);
  });
});
