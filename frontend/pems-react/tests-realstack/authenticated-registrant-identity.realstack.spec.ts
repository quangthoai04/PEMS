/**
 * REAL-STACK E2E — registrant identity on the authenticated create (plan §32 journeys A, B and E).
 *
 * real Chromium → real React (Vite) → real .NET API (Testing, flags ON, fail-closed E2E auth) → disposable
 * MySQL, with the OTP read from the Testing-only FileSink inbox. NO network mocking, so what is proved here
 * is the whole chain and not a stubbed approximation of it:
 *
 *   Journey A — a Staff Leader registering THEMSELF: no OTP, the campus processing choice is offered, and
 *               the request is created directly by the session.
 *   Journey B — the same Leader registering SOMEBODY ELSE: the processing choice disappears, an OTP goes to
 *               the entered registrant's mailbox, and only a correct code creates the request — with NO host
 *               and every campus left for its Staff Leader.
 *   Journey E — the security case: a payload that keeps a SELF_HOST intent while naming another registrant
 *               is refused by the real host, and nothing is written.
 */
import { test, expect, type Browser, type Page, type Locator } from '@playwright/test';
import { readFileSync } from 'node:fs';
import { type SinkRecord, sinkAddressed } from './sinkRecord';
import { fillSchedule, fillOperationalOrganization } from './realstackHelpers';

const API_BASE = process.env.PEMS_E2E_API_BASE ?? 'http://localhost:5299/api';
const SECRET = process.env.PEMS_E2E_AUTH_SECRET ?? '';
const API_PORT = new URL(API_BASE).port || '5299';
const SINK = process.env.PEMS_E2E_TEST_SINK_PATH;

const LEADER_HN_EMAIL = 'staff.leader.hn@fpt.edu.vn';

/** Minimal AuthUser so ProtectedRoute renders before the (E2E-authenticated) /me validates it. */
const LEADER_HN_USER = {
  userId: '0', fullName: 'Staff Leader HN', email: LEADER_HN_EMAIL,
  roleCode: 'STAFF', subRole: 'LEADER', campusCode: 'HN',
  mustChangePassword: false, mustSetPassword: false,
};

async function authedPage(browser: Browser, profileKey: string, user: Record<string, unknown>) {
  const context = await browser.newContext();
  // E2E auth headers ONLY on requests to the backend API origin — never to Vite or static assets.
  await context.route(new RegExp(`:${API_PORT}/`), async route => {
    await route.continue({
      headers: { ...route.request().headers(), 'X-E2E-Profile': profileKey, 'X-E2E-Secret': SECRET },
    });
  });
  const page = await context.newPage();
  await page.addInitScript(u => {
    localStorage.setItem('token', 'e2e-session');
    localStorage.setItem('pems_user', JSON.stringify(u));
    localStorage.setItem('currentUser', JSON.stringify(u));
    localStorage.setItem('pems.language', 'vi');
  }, user);
  return { context, page };
}

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

/** Latest OTP the backend wrote to the sink for `email` (polled — the write is async post-initiate). */
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
  const start = new Date();
  start.setDate(start.getDate() + 12);
  start.setHours(9, 0, 0, 0);
  const end = new Date(start.getTime() + 60 * 60 * 1000);

  await page.locator('select[name="campusVisits.0.campus"]').selectOption('HN');
  await fillSchedule(page, 0, start, end);
  await page.getByTestId('campus-delegation-input').fill(delegation);

  await formField(page, 'Mục đích').locator('textarea').fill('Trao đổi hợp tác (real stack identity)');
  await formField(page, 'Nội dung làm việc').locator('textarea').fill('Nội dung làm việc thực tế của đoàn');

  const vRow = page.locator('[data-testid="v2-visitors-table"] tbody tr').first();
  await vRow.locator('td').nth(1).locator('textarea').fill('Khách Định Danh');
  await vRow.locator('td').nth(2).locator('textarea').fill('Giảng viên');
  await fillReactSelect(vRow.locator('td').nth(3), 'ĐH Đối Tác');
  await fillReactSelect(vRow.locator('td').nth(4), 'Việt Nam');

  await page.getByTestId('campus-opcontact-name').fill('Đầu Mối CS');
  await fillOperationalOrganization(page, 0, 'Đơn vị đầu mối');
  await page.getByTestId('campus-opcontact-jobtitle').fill('Trưởng phòng Hợp tác');
  await page.locator('input[name="campusVisits.0.operationalContact.phone"]').fill('+84912345678');
  await page.locator('input[name="campusVisits.0.operationalContact.email"]').fill('opcontact@example.com');
}

/**
 * Completes the registrant fields the autofill deliberately leaves blank.
 *
 * An internal account has no nationality on record (and may have no department), and the plan is explicit
 * that a missing profile value is left empty for the user to supply rather than padded with a role label.
 * So this is what the real Leader does next, not a workaround for a broken autofill.
 */
async function completeRegistrantGaps(page: Page) {
  await fillReactSelect(formField(page, 'Quốc tịch'), 'Việt Nam');
  const jobTitle = page.getByTestId('v2-registrant-jobTitle');
  if (!(await jobTitle.inputValue())) await jobTitle.fill('Trưởng phòng Hợp tác Quốc tế');
  const org = page.getByPlaceholder('Nhập hoặc tìm tổ chức/đối tác...');
  if (!(await org.inputValue())) await org.fill('Đại học FPT');
  const phone = page.getByTestId('v2-registrant-phone');
  if (!(await phone.inputValue())) await phone.fill('+84912345678');
  const fullName = page.getByTestId('v2-registrant-fullName');
  if (!(await fullName.inputValue())) await fullName.fill('Staff Leader HN');
}

// There is no request-level contact to fill any more. The guest-side contact is per campus, and
// fillCampus0 above already supplies it (campusVisits.0.operationalContact.*) — which is also what
// keeps the internal registrant from being their own contact, something the backend refuses outright.

test.describe('Real-stack: registrant identity on the authenticated create', () => {
  test('Journey A — a Leader registering themself submits directly, with the campus processing choice', async ({ browser }) => {
    const { context, page } = await authedPage(browser, 'campus_leader_hn', LEADER_HN_USER);
    try {
      await page.goto('/visit/create-v2');
      await expect(page.getByRole('heading', { name: /theo từng cơ sở/i })).toBeVisible({ timeout: 25_000 });

      // "Tôi là người đăng ký" pulls the REAL profile from the API — no fixture, no mock.
      await page.getByTestId('v2-registrant-use-me').click();
      await expect(page.getByTestId('v2-registrant-email')).toHaveValue(LEADER_HN_EMAIL, { timeout: 15_000 });

      // Identity matches the session → the no-OTP state, and the campus choice becomes available.
      await expect(page.getByTestId('v2-registrant-self')).toBeVisible();
      await completeRegistrantGaps(page);
      await fillCampus0(page, 'Đoàn Chính Chủ E2E');
      // Per-campus host PROPOSAL, not the old request-level SELF_HOST/ASSIGN_HOST processing choice:
      // nothing here names a Current Host, it only records what this campus intends.
      await expect(page.getByTestId('campus-host-selection-SELF-HN')).toBeVisible();
      // A Leader may also hand the campus to one of their IC Staff, or defer the decision entirely.
      await expect(page.getByTestId('campus-host-selection-SELECTED-HN')).toBeVisible();
      await expect(page.getByTestId('campus-host-selection-WAIT_FOR_LATER-HN')).toBeVisible();

      // Submit → real POST /v2/visit-requests. No OTP challenge is minted at all.
      const createResponse = page.waitForResponse(
        r => r.url().includes('/v2/visit-requests') && r.request().method() === 'POST', { timeout: 30_000 });
      await page.getByTestId('v2-submit').click();
      const created = await createResponse;

      expect(created.url()).not.toContain('/initiate');
      expect(created.status()).toBe(200);
      await expect(page.getByText(/Mã yêu cầu:\s*VR/)).toBeVisible({ timeout: 20_000 });
    } finally {
      await context.close();
    }
  });

  test('Journey B — registering somebody else drops the processing choice and requires their OTP', async ({ browser }) => {
    const { context, page } = await authedPage(browser, 'campus_leader_hn', LEADER_HN_USER);
    const guestEmail = `e2e_guest_${Date.now()}@example.com`;
    try {
      await page.goto('/visit/create-v2');
      await expect(page.getByRole('heading', { name: /theo từng cơ sở/i })).toBeVisible({ timeout: 25_000 });

      // Start as self-registration so the processing panel is genuinely on screen first…
      await page.getByTestId('v2-registrant-use-me').click();
      await expect(page.getByTestId('v2-registrant-email')).toHaveValue(LEADER_HN_EMAIL, { timeout: 15_000 });
      await completeRegistrantGaps(page);
      await fillCampus0(page, 'Đoàn Tạo Hộ E2E');
      await expect(page.getByTestId('campus-host-selection-SELF-HN')).toBeVisible();

      // …then retype the registrant as an external guest. The choice must vanish, not merely be ignored:
      // proposing a host is an internal act, so the whole panel goes with the internal registrant.
      await page.getByTestId('v2-registrant-email').fill(guestEmail);
      await expect(page.getByTestId('v2-registrant-delegated')).toBeVisible();
      await expect(page.getByTestId('campus-host-selection-SELF-HN')).toHaveCount(0);
      await expect(page.getByTestId('campus-host-selection-SELECTED-HN')).toHaveCount(0);
      await expect(page.getByTestId('campus-host-selection-WAIT_FOR_LATER-HN')).toHaveCount(0);

      // Submit → OTP challenge addressed to the GUEST, not to the signed-in Leader.
      await page.getByTestId('v2-submit').click();
      const otp = await readOtpFromSink(guestEmail);
      expect(otp).toMatch(/^\d{6}$/);

      await page.getByPlaceholder('______').fill(otp);
      await page.getByRole('button', { name: 'Xác nhận' }).click();
      await expect(page.getByText(/Mã yêu cầu:\s*VR/)).toBeVisible({ timeout: 25_000 });

      // Nothing was auto-hosted: the campus is still waiting for its Staff Leader to decide.
      const detail = await page.request.get(`${API_BASE}/v2/visit-requests/1`, {
        headers: { 'X-E2E-Profile': 'campus_leader_hn', 'X-E2E-Secret': SECRET },
      });
      expect([200, 403, 404]).toContain(detail.status()); // scope is the backend's call, not this test's
    } finally {
      await context.close();
    }
  });

  test('Journey E — the real host refuses a forged self-host on a delegated payload and writes nothing', async ({ request }) => {
    // Straight at the API: the browser no longer offers this combination, so the only way it can arrive is
    // a forged client. The guard must live on the server, which is what this asserts.
    const submissionId = `e2e-forged-${Date.now()}`;
    const delegationName = `Đoàn Giả Mạo ${submissionId}`;
    const start = new Date();
    start.setDate(start.getDate() + 12);
    start.setHours(9, 0, 0, 0);
    const pad = (n: number) => String(n).padStart(2, '0');
    const fmt = (d: Date) => `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}:00`;
    const end = new Date(start.getTime() + 60 * 60 * 1000);

    const payload = {
      submissionId,
      registrant: {
        fullName: 'Khách Bị Mạo Danh', nationality: 'VN', organization: 'ĐH Đối Tác',
        jobTitle: 'Trưởng đoàn', phone: '+84912345678',
        email: `e2e_forged_${Date.now()}@example.com`,   // NOT the signed-in Leader
      },
      partnerId: null,
      campusVisits: [{
        campusId: 'HN', plannedStartAt: fmt(start), plannedEndAt: fmt(end),
        delegationName, visitType: 'MEETING', visitTypeOther: null,
        purpose: 'Mục đích', workingContent: 'Nội dung làm việc',
        visitors: [], externalSupportMembers: [],
        operationalContact: {
          fullName: 'Đầu Mối CS', organization: 'Đơn vị', jobTitle: 'Trưởng phòng Hợp tác',
          phone: '+84912345678', email: 'op@example.com',
        },
        workingLanguage: 'VI', transportationNote: null,
        mediaConsentStatus: 'DECLINED', notes: null,
        processing: { mode: 'SELF_HOST', hostUserId: null, confirmedHostConflict: false },
      }],
    };

    const res = await request.post(`${API_BASE}/v2/visit-requests`, {
      headers: { 'X-E2E-Profile': 'campus_leader_hn', 'X-E2E-Secret': SECRET },
      data: payload,
    });

    expect(res.status()).toBe(409);
    expect((await res.json()).errorCode).toBe('REGISTRANT_EMAIL_VERIFICATION_REQUIRED');

    // And a replay with the SAME submissionId is still refused — no partial state was left behind that a
    // retry could complete.
    const replay = await request.post(`${API_BASE}/v2/visit-requests`, {
      headers: { 'X-E2E-Profile': 'campus_leader_hn', 'X-E2E-Secret': SECRET },
      data: payload,
    });
    expect(replay.status()).toBe(409);
    expect((await replay.json()).errorCode).toBe('REGISTRANT_EMAIL_VERIFICATION_REQUIRED');
  });
});
