/**
 * REAL-STACK E2E — plan §26: importing a list, entering a schedule, and hitting a length limit.
 *
 * real Chromium → real React (Vite) → real .NET API (Testing, flags ON) → real disposable MySQL.
 * NO network mocking. The workbooks are built in this process and handed to the REAL file input,
 * so the browser parses them exactly as a user's own file would be parsed.
 *
 * What only the real stack proves here:
 *   • a schedule entered through the new picker is one the SERVER accepts — a wall-clock mangled
 *     on the way out would come back as a 400 (end before start / under 30 minutes), not as a
 *     cosmetic difference;
 *   • an overnight window survives that same round trip;
 *   • a file with bad rows leaves the typed form exactly as it was, in a real browser with real
 *     file handling rather than a synthetic change event.
 */
import { test, expect, type Page, type Locator } from '@playwright/test';
import { readFileSync } from 'node:fs';
import { type SinkRecord, sinkAddressed } from './sinkRecord';
import * as XLSX from 'xlsx';
import { fillSchedule, fillOperationalOrganization, dateKey, timeKey } from './realstackHelpers';

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

async function readOtpFromSink(email: string): Promise<string> {
  if (!SINK) throw new Error('PEMS_E2E_TEST_SINK_PATH is not set — the real-stack harness must provide it.');
  const target = email.trim().toLowerCase();
  for (let attempt = 0; attempt < 40; attempt++) {
    let lines: string[] = [];
    try { lines = readFileSync(SINK, 'utf8').split('\n').filter(Boolean); } catch { /* not written yet */ }
    for (let i = lines.length - 1; i >= 0; i--) {
      try {
        const rec = JSON.parse(lines[i]) as SinkRecord;
        if (rec.kind === 'VISIT_REQUEST_OTP' && sinkAddressed(rec, target) && rec.code) return rec.code;
      } catch { /* skip malformed */ }
    }
    await new Promise(r => setTimeout(r, 250));
  }
  throw new Error(`No VISIT_REQUEST_OTP captured for ${email} within timeout.`);
}

const HEADER = ['STT', 'Họ và tên', 'Chức vụ', 'Đơn vị công tác', 'Quốc tịch'];

/** A real .xlsx payload for Playwright's setInputFiles. */
function workbook(rows: (string | number)[][], name: string) {
  const ws = XLSX.utils.aoa_to_sheet(rows);
  const wb = XLSX.utils.book_new();
  XLSX.utils.book_append_sheet(wb, ws, 'Sheet1');
  return {
    name,
    mimeType: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet',
    buffer: Buffer.from(XLSX.write(wb, { type: 'array', bookType: 'xlsx' }) as ArrayBuffer),
  };
}

const guestRow = (n: number, over: Partial<Record<'fullName' | 'jobTitle' | 'organization' | 'nationality', string>> = {}) => [
  n,
  over.fullName ?? `Khách E2E ${n}`,
  over.jobTitle ?? 'Giảng viên',
  over.organization ?? 'ĐH Đối Tác',
  over.nationality ?? 'Việt Nam',
];

/** The guests table's first hidden file input (the support one is the second). */
const guestFileInput = (page: Page) => page.locator('input[type="file"]').first();

/** Registrant + contact, stopping before the campus card. */
async function fillHeader(page: Page, email: string) {
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
}

/** Everything on the campus card except the guest list and the schedule. */
async function fillCampusBody(page: Page, delegation: string) {
  await page.locator('select[name="campusVisits.0.campus"]').selectOption('HN');
  await page.getByTestId('campus-delegation-input').fill(delegation);
  await formField(page, 'Mục đích').locator('textarea').fill('Trao đổi hợp tác thật');
  await formField(page, 'Nội dung làm việc').locator('textarea').fill('Nội dung làm việc thực tế của đoàn');
  await page.getByTestId('campus-opcontact-name').fill('Đầu Mối CS');
  await fillOperationalOrganization(page, 0, 'Đơn vị đầu mối');
  await page.locator('input[name="campusVisits.0.operationalContact.phone"]').fill('+84912345678');
  await page.locator('input[name="campusVisits.0.operationalContact.email"]').fill('opcontact@example.com');
}

async function typeOneGuest(page: Page, name: string) {
  const row = page.locator('[data-testid="v2-visitors-table"] tbody tr').first();
  await row.locator('td').nth(1).locator('textarea').fill(name);
  await row.locator('td').nth(2).locator('textarea').fill('Giảng viên');
  await fillReactSelect(row.locator('td').nth(3), 'ĐH Đối Tác');
  await fillReactSelect(row.locator('td').nth(4), 'Việt Nam');
}

const submit = (page: Page) => page.getByRole('button', { name: /Gửi yêu cầu & nhận mã OTP/ }).click();

async function verifyOtp(page: Page, email: string) {
  const input = page.getByPlaceholder('______');
  await expect(input).toBeVisible({ timeout: 20_000 });
  const code = await readOtpFromSink(email);
  await input.click();
  await input.fill(code);
  await expect(input).toHaveValue(code);
  await page.getByRole('button', { name: 'Xác nhận' }).click();
  await expect(page.getByText('Đã gửi yêu cầu tham quan')).toBeVisible({ timeout: 20_000 });
}

/** A visit `days` out, starting at `hour`:00 and running `durationHours`. */
function window_(days: number, hour: number, durationHours: number) {
  const start = new Date();
  start.setDate(start.getDate() + days);
  start.setHours(hour, 0, 0, 0);
  return { start, end: new Date(start.getTime() + durationHours * 3600 * 1000) };
}

test.describe('Real-stack: Excel import, schedule and length limits', () => {
  test('journey 1 — a valid file is reported, imported, and submitted for real', async ({ page }) => {
    const email = `e2e_xls_ok_${Date.now()}@example.com`;
    const { start, end } = window_(11, 9, 1);

    await fillHeader(page, email);
    await fillCampusBody(page, 'Đoàn Excel OK');
    await fillSchedule(page, 0, start, end);

    // One guest typed by hand FIRST: the import must add to this person, not replace them.
    await typeOneGuest(page, 'Người Gõ Tay');

    await guestFileInput(page).setInputFiles(
      workbook([HEADER, guestRow(1), guestRow(2), guestRow(1)], 'danh-sach-khach.xlsx'));

    const report = page.getByTestId('v2-excel-visitors-success');
    await expect(report).toBeVisible({ timeout: 20_000 });
    await expect(report).toContainText('danh-sach-khach.xlsx');
    await expect(report).toContainText('3');   // total rows read
    await expect(report).toContainText('2');   // imported (the repeat was skipped)

    const rows = page.locator('[data-testid="v2-visitors-table"] tbody tr');
    await expect(rows).toHaveCount(3);
    await expect(rows.first().locator('td').nth(1).locator('textarea')).toHaveValue('Người Gõ Tay');

    await submit(page);
    await verifyOtp(page, email);
    await expect(page.getByText(/Mã yêu cầu:\s*VR/)).toBeVisible();
  });

  test('journey 2 — a file with bad rows reports every one and changes nothing', async ({ page }) => {
    const email = `e2e_xls_bad_${Date.now()}@example.com`;
    const { start, end } = window_(11, 14, 2);

    await fillHeader(page, email);
    await fillCampusBody(page, 'Đoàn Excel Lỗi');
    await fillSchedule(page, 0, start, end);
    await typeOneGuest(page, 'Người Gõ Tay');

    await guestFileInput(page).setInputFiles(workbook([
      HEADER,
      guestRow(1, { fullName: '' }),
      guestRow(2, { organization: '' }),
      guestRow(3, { nationality: 'x'.repeat(101) }),
      guestRow(4),
    ], 'danh-sach-loi.xlsx'));

    const panel = page.getByTestId('v2-excel-visitors-error');
    await expect(panel).toBeVisible({ timeout: 20_000 });

    // EVERY faulty row, not just the first — the whole point of the change.
    const errorRows = page.locator('[data-testid="v2-excel-visitors-error-table"] tbody tr');
    await expect(errorRows).toHaveCount(3);
    await expect(errorRows.nth(0)).toContainText('2');
    await expect(errorRows.nth(2)).toContainText('4');

    // The form is untouched: still exactly the one person who was typed in.
    const rows = page.locator('[data-testid="v2-visitors-table"] tbody tr');
    await expect(rows).toHaveCount(1);
    await expect(rows.first().locator('td').nth(1).locator('textarea')).toHaveValue('Người Gõ Tay');

    // The report can be taken away and fixed offline.
    const download = page.waitForEvent('download');
    await page.getByTestId('v2-excel-visitors-download').click();
    expect((await download).suggestedFilename()).toBe('danh-sach-loi-error-report.xlsx');
  });

  test('journey 3 — a same-day 08:00–09:00 window reads back as one hour and is accepted', async ({ page }) => {
    const email = `e2e_sched_same_${Date.now()}@example.com`;
    const { start, end } = window_(12, 8, 1);

    await fillHeader(page, email);
    await fillCampusBody(page, 'Đoàn Cùng Ngày');
    await typeOneGuest(page, 'Khách Cùng Ngày');
    await fillSchedule(page, 0, start, end);

    await expect(page.getByTestId('campus-0-duration')).toContainText('1');
    // Same-day means one date field; the second only appears when asked for.
    await expect(page.getByTestId('campus-0-end-date')).toHaveCount(0);

    await submit(page);
    await verifyOtp(page, email);

    // The created request echoes the wall-clock it was given — no drift on the way through.
    // The submitted snapshot now sits behind "Xem lại thông tin đã gửi": the receipt leads with
    // the request code, and the full per-campus detail is revealed on request.
    await page.getByTestId('v2-success-review').click();
    const summary = page.getByTestId('campus-summary-0');
    await expect(summary).toContainText(`${dateKey(start).split('-').reverse().join('/')} ${timeKey(start)}`);
    await expect(summary).toContainText(timeKey(end));
  });

  test('journey 4 — a 22:00 → 01:00 window is three hours across two dates, and the server takes it', async ({ page }) => {
    const email = `e2e_sched_multi_${Date.now()}@example.com`;
    const start = new Date();
    start.setDate(start.getDate() + 13);
    start.setHours(22, 0, 0, 0);
    const end = new Date(start.getTime() + 3 * 3600 * 1000); // 01:00 the NEXT day

    await fillHeader(page, email);
    await fillCampusBody(page, 'Đoàn Qua Đêm');
    await typeOneGuest(page, 'Khách Qua Đêm');
    await fillSchedule(page, 0, start, end);

    await expect(page.getByTestId('campus-0-multiday')).toBeChecked();
    await expect(page.getByTestId('campus-0-end-date')).toHaveValue(dateKey(end));
    await expect(page.getByTestId('campus-0-duration')).toContainText('3');

    // A shift on the wire would arrive as "ends before it starts" or "under 30 minutes" and be
    // refused; a created request is therefore evidence the wall-clock survived.
    await submit(page);
    await verifyOtp(page, email);

    // The submitted snapshot now sits behind "Xem lại thông tin đã gửi": the receipt leads with
    // the request code, and the full per-campus detail is revealed on request.
    await page.getByTestId('v2-success-review').click();
    const summary = page.getByTestId('campus-summary-0');
    await expect(summary).toContainText(`${dateKey(start).split('-').reverse().join('/')} 22:00`);
    await expect(summary).toContainText(`${dateKey(end).split('-').reverse().join('/')} 01:00`);
  });

  test('journey 5 — an over-long value is counted, refused, and submittable once fixed', async ({ page }) => {
    const email = `e2e_len_${Date.now()}@example.com`;
    const { start, end } = window_(14, 10, 2);

    await fillHeader(page, email);
    await fillCampusBody(page, 'Đoàn Độ Dài');
    await typeOneGuest(page, 'Khách Độ Dài');
    await fillSchedule(page, 0, start, end);

    // 201 characters into a 200-character field.
    const delegation = page.getByTestId('campus-delegation-input');
    await delegation.fill('Đ'.repeat(201));
    await expect(page.getByText('201/200')).toBeVisible();

    await submit(page);
    // No OTP: the form refused it, and said why, on the field itself.
    await expect(page.getByPlaceholder('______')).toHaveCount(0);
    await expect(page.getByText(/200 ký tự/).first()).toBeVisible();

    await delegation.fill('Đoàn Độ Dài Hợp Lệ');
    await submit(page);
    await verifyOtp(page, email);
  });
});
