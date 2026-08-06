/**
 * REAL-STACK E2E — public per-campus v2 create (journey A).
 *
 * real Chromium → real React (Vite) → real .NET API (Testing, flags ON) → real disposable MySQL.
 * NO network mocking: the OTP is read from the Testing-only FileSink inbox (PEMS_E2E_TEST_SINK_PATH),
 * exactly as the backend wrote it — proving initiate-v2 → OTP delivery → verify-v2 → real persistence and
 * that the UI summary renders the backend-created request. Also proves the snapshot binding: an OTP verified
 * for this submission creates the request the backend bound at initiate.
 */
import { test, expect, type Page, type Locator } from '@playwright/test';
import { readFileSync } from 'node:fs';
import { type SinkRecord, sinkAddressed } from './sinkRecord';
import { fillSchedule, fillOperationalOrganization } from './realstackHelpers';

/** The FormField (label→control wrapper) whose visible label contains `label`. */
function formField(page: Page, label: string): Locator {
  return page.locator('div.flex.flex-col.gap-2').filter({ has: page.getByText(label, { exact: false }) }).first();
}

/**
 * Fill a react-select control (CountrySelect / OrganizationCombobox — both Creatable, free text allowed):
 * open it, type, and commit the matched-or-created option with Enter.
 */
async function fillReactSelect(scope: Locator, text: string) {
  const input = scope.locator('input').first();
  await input.click();
  await input.fill(text);
  await scope.page().keyboard.press('Enter');
}

const SINK = process.env.PEMS_E2E_TEST_SINK_PATH;

/** Latest OTP code the backend wrote to the sink for `email` (polled, since the write is async post-initiate). */
async function readOtpFromSink(email: string): Promise<string> {
  if (!SINK) throw new Error('PEMS_E2E_TEST_SINK_PATH is not set — the real-stack harness must provide it.');
  const target = email.trim().toLowerCase();
  for (let attempt = 0; attempt < 40; attempt++) {
    let lines: string[] = [];
    try {
      lines = readFileSync(SINK, 'utf8').split('\n').filter(Boolean);
    } catch { /* file may not exist yet */ }
    for (let i = lines.length - 1; i >= 0; i--) {
      try {
        const rec = JSON.parse(lines[i]) as SinkRecord;
        if (rec.kind === 'VISIT_REQUEST_OTP' && sinkAddressed(rec, target) && rec.code) return rec.code;
      } catch { /* skip malformed */ }
    }
    await new Promise(r => setTimeout(r, 250));
  }
  throw new Error(`No VISIT_REQUEST_OTP captured for ${email} in the sink within timeout.`);
}

async function fillCampus0(page: Page, delegation: string) {
  // Campus + schedule: 10 days out, exactly 30 minutes (valid under v2).
  const start = new Date();
  start.setDate(start.getDate() + 10);
  start.setHours(9, 0, 0, 0);
  const end = new Date(start.getTime() + 30 * 60 * 1000);

  await page.locator('select[name="campusVisits.0.campus"]').selectOption('HN');
  // Date + start time + end time through the real picker (no datetime-local any more).
  await fillSchedule(page, 0, start, end);
  await page.getByTestId('campus-delegation-input').fill(delegation);

  // Purpose + working content are Controller/AutoGrowTextarea (no DOM name); reach them by FormField label.
  // Working content is now REQUIRED by the backend, so it must be filled.
  await formField(page, 'Mục đích').locator('textarea').fill('Trao đổi hợp tác thật');
  await formField(page, 'Nội dung làm việc').locator('textarea').fill('Nội dung làm việc thực tế của đoàn');

  // The first visitor row: fullName/jobTitle are aria-labelled inputs; organization/nationality are
  // Creatable react-selects. Scope to the desktop visitors table so mobile duplicates never match.
  const vRow = page.locator('[data-testid="v2-visitors-table"] tbody tr').first();
  await vRow.locator('td').nth(1).locator('textarea').fill('Khách Thật');   // fullName (auto-grow)
  await vRow.locator('td').nth(2).locator('textarea').fill('Giảng viên');   // jobTitle (auto-grow)
  await fillReactSelect(vRow.locator('td').nth(3), 'ĐH Đối Tác');           // organization
  await fillReactSelect(vRow.locator('td').nth(4), 'Việt Nam');             // nationality

  await page.getByTestId('campus-opcontact-name').fill('Đầu Mối CS');
  await fillOperationalOrganization(page, 0, 'Đơn vị đầu mối');
  await page.getByTestId('campus-opcontact-jobtitle').fill('Trưởng phòng Hợp tác');
  await page.locator('input[name="campusVisits.0.operationalContact.phone"]').fill('+84912345678');
  await page.locator('input[name="campusVisits.0.operationalContact.email"]').fill('opcontact@example.com');
}

test.describe('Real-stack: public per-campus v2 create', () => {
  test('fills the real form, receives a real OTP from the sink, and creates a real request', async ({ page }) => {
    const email = `e2e_${Date.now()}@example.com`;

    await page.addInitScript(() => window.localStorage.setItem('pems.language', 'vi'));
    await page.goto('/visit-registration/v2');
    await expect(page.getByRole('heading', { name: /theo từng cơ sở/i })).toBeVisible();

    // Registrant.
    await page.locator('input[name="registerInfo.fullName"]').fill('Người Thật E2E');
    // Organization is a free-solo PartnerOrgCombobox (typing a new value keeps it as a manually entered
    // organization, partnerId null) — reach it by its accessible placeholder, not the old input name.
    await page.getByPlaceholder('Nhập hoặc tìm tổ chức/đối tác...').fill('Công ty E2E');
    await page.locator('input[name="registerInfo.jobTitle"]').fill('Trưởng phòng');
    // Registrant nationality is a CountrySelect (react-select) — reach it by its FormField, not a name.
    await fillReactSelect(formField(page, 'Quốc tịch'), 'Việt Nam');
    await page.locator('input[name="registerInfo.phone"]').fill('+84912345678');
    await page.locator('input[name="registerInfo.email"]').fill(email);

    // Per-campus quick-fill (campus 0): copies the registrant into THIS campus's operational contact.
    // The old request-level "same as registrant" control went away with the request-level contact, so
    // the previous label regex matched no button and simply waited out the timeout.
    await page.getByTestId('campus-opcontact-use-registrant-0').click();

    await fillCampus0(page, 'Đoàn Real Stack');

    // Submit → real POST /v2/visit-requests/initiate → OTP modal.
    await page.getByRole('button', { name: /Gửi yêu cầu & nhận mã OTP/ }).click();
    await expect(page.getByText(/Xác thực OTP|OTP/i).first()).toBeVisible({ timeout: 20_000 });

    // Read the OTP the REAL backend wrote to the Testing sink, then verify → real create.
    const otp = await readOtpFromSink(email);
    expect(otp).toMatch(/^\d{6}$/);
    await page.getByPlaceholder('______').fill(otp);
    await page.getByRole('button', { name: 'Xác nhận' }).click();

    // The success summary renders the backend-created request code (only produced on a real DB insert).
    await expect(page.getByText('Đã gửi yêu cầu tham quan')).toBeVisible({ timeout: 20_000 });
    await expect(page.getByText(/Mã yêu cầu:\s*VR/)).toBeVisible();
    // The receipt now states status and submitted time alongside the code, so assert the
    // structured fields rather than one assembled sentence.
    await expect(page.getByTestId('v2-success-status')).toBeVisible();
    await expect(page.getByTestId('v2-success-submitted-at')).toBeVisible();
  });
});
