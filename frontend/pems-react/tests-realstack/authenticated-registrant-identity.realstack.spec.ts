/**
 * REAL-STACK E2E — registrant identity on the authenticated create (plan §32 journeys A and E).
 *
 * real Chromium → real React (Vite) → real .NET API (Testing, flags ON, fail-closed E2E auth) → disposable
 * MySQL. NO network mocking, so what is proved here is the whole chain and not a stubbed approximation:
 *
 *   Journey A — a Staff Leader registering THEMSELF: no OTP, the campus processing choice is offered, and
 *               the request is created directly by the session.
 *   Journey E — the security case: a payload that keeps a SELF_HOST intent while naming another registrant
 *               is refused by the real host, and nothing is written.
 *
 * Journey B ("the same Leader registering somebody else, an OTP goes to their mailbox") was DELETED — the
 * capability it tested no longer exists in the UI for an authenticated session. `VisitRequestFormV2.tsx`'s
 * own comment states the current contract plainly: "Authenticated create IS self-registration, always
 * (plan CanhIter3FixBug)... No 'Tôi là người đăng ký' button, no delegated-OTP path." The registrant email
 * input Journey B retyped to become a guest's address does not render at all for an authenticated Staff/
 * Staff Leader actor any more (only the public/unauthenticated branch still has one) — confirmed live and
 * by source. This was a deliberate product change (commit c140d73e, "unify authenticated creation..."),
 * not a regression, so the test was removed rather than adapted.
 */
import { test, expect, type Page, type Locator } from '@playwright/test';
import { fillSchedule, fillOperationalOrganization, authedPage, meUser } from './realstackHelpers';

const API_BASE = process.env.PEMS_E2E_API_BASE ?? 'http://localhost:5299/api';
const SECRET = process.env.PEMS_E2E_AUTH_SECRET ?? '';

const LEADER_HN_EMAIL = 'staff.leader.hn@fpt.edu.vn';

// This file used to seed the browser identity from a hand-typed local object (LEADER_HN_USER) via
// its own local authedPage copy, instead of the canonical realstackHelpers.authedPage + meUser()
// every other browser-driven spec in this suite uses. Audited side by side (localStorage keys, route
// interception, X-E2E headers, /auth/me hydration timing) -- all IDENTICAL between the two
// authedPage implementations. The one real difference was the SEEDED USER SHAPE: the hand-typed
// object supplied `campusCode: 'HN'` (a string) but never `primaryCampusId` (the numeric field the
// real AuthUserDto carries), because nobody kept it in sync with the backend contract by hand.
// `meUser()` fetches the REAL, current `/auth/me` response for the profile instead of guessing it,
// so the local placeholder never drifts from the server-side contract again. No production auth
// logic was touched -- this only changes which object the TEST seeds into localStorage.

/**
 * Dismisses the "restore your draft?" prompt if it appears (VisitRequestFormV2's own autosave --
 * unrelated to this test's identity: it can legitimately surface for a role/route a browser has
 * touched before). Discards rather than restores, since these journeys fill the form themselves.
 */
async function dismissDraftPromptIfShown(page: Page) {
  const discard = page.getByTestId('v2-draft-discard');
  if (await discard.isVisible({ timeout: 3_000 }).catch(() => false)) await discard.click();
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

  // Operational contact source (MEMBER vs EXTERNAL) starts genuinely undecided — `null`, never a
  // default guess — so the free-text contact fields below do not exist until EXTERNAL is explicitly
  // chosen (CampusVisitCard.tsx). The form is fresh here (no contact data yet), so the choice applies
  // immediately with no confirmation step.
  await page.getByTestId('campus-opcontact-source-external-0').click();
  await page.getByTestId('campus-opcontact-name').fill('Đầu Mối CS');
  await fillOperationalOrganization(page, 0, 'Đơn vị đầu mối');
  await page.getByTestId('campus-opcontact-jobtitle').fill('Trưởng phòng Hợp tác');
  await page.locator('input[name="campusVisits.0.operationalContact.phone"]').fill('+84912345678');
  await page.locator('input[name="campusVisits.0.operationalContact.email"]').fill('opcontact@example.com');
}

test.describe('Real-stack: registrant identity on the authenticated create', () => {
  test('Journey A — a Leader registering themself submits directly, with the campus processing choice', async ({ browser, request }) => {
    const leader = await meUser(request, 'campus_leader_hn');
    const { context, page } = await authedPage(browser, 'campus_leader_hn', leader);
    try {
      await page.goto('/visit/create-v2');
      await expect(page.getByRole('heading', { name: /theo từng cơ sở/i })).toBeVisible({ timeout: 25_000 });
      await dismissDraftPromptIfShown(page);

      // An authenticated internal actor (Staff/Staff Leader) gets the registrant panel auto-applied
      // from their profile, fully READ-ONLY — no "Tôi là người đăng ký" button and no editable gaps to
      // fill exist any more for this actor (VisitRequestFormV2.tsx's `isInternalActor` branch, shipped
      // in c140d73e "unify authenticated creation..."). The real profile round-trip is still what
      // proves this, just via the summary card instead of a click + hydrated input.
      const registrantSummary = page.getByTestId('v2-registrant-readonly');
      await expect(registrantSummary).toBeVisible({ timeout: 15_000 });
      await expect(registrantSummary).toContainText(LEADER_HN_EMAIL);
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
      // The success screen never shows the request code itself (VisitRequestV2SuccessPanel) — the
      // create response body is the real proof a request now exists.
      expect((await created.json()).requestCode).toMatch(/^VR/);
      await expect(page.getByTestId('v2-success-title')).toBeVisible({ timeout: 20_000 });
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
        // A campus must carry >=1 visitor (CreateVisitRequestV2CommandValidator) — an empty list here
        // trips that ordinary structural rule (400) before the request ever reaches the SELF_HOST
        // forgery guard this test exists to prove (409), which is not what this test is about.
        visitors: [{ fullName: 'Khach Bi Mao Danh', nationality: 'VN', jobTitle: 'GV', organization: 'Org' }],
        externalSupportMembers: [],
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
