/**
 * UC17 public visit-request OTP submission flow — E2E against the real UI with mocked network.
 *
 * REWRITE NOTE: this file used to test an earlier "single-form" design (a flat `visits[]` array,
 * a `visitMode` single/multiple toggle, a shared `contactPoint` filled via "same as registrant"
 * checkboxes, and six flat section headings that no longer exist). That design was superseded by
 * the unified per-campus architecture — the SAME `VisitRequestFormV2` + `campusVisits[]` schema
 * that `visit-request-percampus-v2.spec.ts` also drives. This file does not duplicate that one:
 * visit-request-percampus-v2.spec.ts owns the browser-only accordion/apply-to-all mechanics; this
 * file owns the OTP submission business flow (initiate → OTP → success/duplicate/human-verify),
 * which visit-request-percampus-v2.spec.ts never touches.
 *
 * Also NOT re-derived here: the contact-email-conflict business rule is already thoroughly
 * unit-tested against the current schema in visitRequestV2ContactEmailRejected.test.tsx (12
 * cases covering both delegation-contact and registrant refusals, in both languages). TC-09 below
 * is kept as a single real end-to-end wiring check, not a re-proof of that coverage.
 *
 * Duplicate submissions: the backend no longer answers a resubmit with 409 DUPLICATE_VISIT_REQUEST
 * and a dedicated result screen. Retrying the SAME submissionId now returns the SAME 200 success
 * response with `idempotent: true`, rendered as an inline notice on the ordinary success screen
 * (VisitRequestV2SuccessPanel). V2-07 below tests that behaviour instead of the old dedicated screen.
 *
 * The OTP / cooldown / human-verification mechanics themselves (OtpVerificationModal,
 * TurnstileWidget) are UNCHANGED from the original design — same headings, same testids, same
 * visitRequest.json (v1-shared) namespace — confirmed by reading those components directly.
 *
 * OTP is never sent for real: /v2/visit-requests/initiate and /verify are mocked.
 */
import { test, expect, type Page } from '@playwright/test';

/** V2CreateResponse shape (visitRequestV2Api.ts) — NOT the old flat {visitRequestId,requestCode,status,message}. */
const VERIFY_OK = {
  visitRequestId: 123,
  requestCode: 'VR-2026-000123',
  visitScope: 'SINGLE_CAMPUS',
  hasMixedCampusDetails: false,
  pendingContactConfirmations: 0,
  instances: [{ visitInstanceId: 501, campusId: 1, status: 'PENDING_APPROVAL' }],
  idempotent: false,
  status: 'PENDING_APPROVAL',
  submittedAt: '2026-08-19T10:00:00',
  campusCount: 1,
};

/**
 * The homepage CTA gates on a real backend capability check (`GET
 * /public/features/per-campus-form-v2`, see usePerCampusV2Capability) before it will open the v2
 * form -- by design it shows an error toast instead of the form on any fetch failure, rather than
 * silently falling back. `PerCampusV2CapabilityProvider` fires this fetch from a `useEffect` on
 * mount (not on click), so the route must be registered BEFORE navigation -- registering it inside
 * openVisitForm() (after goto) is too late, the request has already gone out and failed by then.
 * Playwright's webServer only starts the Vite dev server, no backend, so without this mock the CTA
 * never opens anything for openVisitForm() to find.
 */
async function mockV2Capability(page: Page) {
  await page.route('**/public/features/per-campus-form-v2', (route) =>
    route.fulfill({ json: { readEnabled: true, writeEnabled: true, enabled: true } }));
}

async function gotoVi(page: Page) {
  await mockV2Capability(page);
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

/** Campus options are backend-driven (UC-86 §10) — mock the anonymous options endpoint. */
async function mockRegistrationCampuses(page: Page) {
  await page.route('**/campuses/available-for-registration', (route) =>
    route.fulfill({
      json: [
        { campusId: 1, campusCode: 'HN', campusName: 'Hà Nội', city: 'Hà Nội' },
        { campusId: 2, campusCode: 'DN', campusName: 'Đà Nẵng', city: 'Đà Nẵng' },
        { campusId: 3, campusCode: 'CT', campusName: 'Cần Thơ', city: 'Cần Thơ' },
        { campusId: 4, campusCode: 'HCM', campusName: 'Hồ Chí Minh', city: 'TP. Hồ Chí Minh' },
        { campusId: 5, campusCode: 'QN', campusName: 'Quy Nhơn', city: 'Gia Lai' },
      ],
    }));
}

/**
 * The v2 endpoint is `/v2/visit-requests/initiate`, and the body is NESTED as `{ form: payload }`
 * (visitRequestV2Api.ts) — the old flat `{submissionId, ...}` body no longer exists.
 */
async function mockInitiate(page: Page) {
  const counter = { calls: 0, lastSubmissionId: '' };
  await page.route('**/v2/visit-requests/initiate', async (route) => {
    counter.calls += 1;
    const body = route.request().postDataJSON() as { form?: { submissionId?: string } };
    counter.lastSubmissionId = body?.form?.submissionId ?? '';
    await route.fulfill({
      json: {
        sessionToken: 'test-session-token',
        maskedEmail: 'te***@example.com',
        message: 'OTP sent',
        expiresAt: '2099-01-01T00:05:00',
        resendAfterSeconds: 60,
        maxAttempts: 10,
      },
    });
  });
  return counter;
}

/** yyyy-MM-dd, N days from now (for the schedule's native `type="date"` input). */
function futureDate(daysFromNow: number): string {
  const d = new Date();
  d.setDate(d.getDate() + daysFromNow);
  const pad = (n: number) => String(n).padStart(2, '0');
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`;
}

/**
 * Picks "Viet Nam" in a react-select-backed combobox (CountrySelect / PartnerOrgCombobox /
 * OrganizationCombobox all render `input[role="combobox"]`, react-select's own standard markup).
 * Typing narrows the option list to a single match, so Enter selects it.
 */
async function pickCountry(page: Page, scope: string, nth = 0) {
  const input = page.locator(`${scope} input[role="combobox"]`).nth(nth);
  await input.click();
  await input.pressSequentially('Viet');
  await page.keyboard.press('Enter');
}

/**
 * Fills ONE campus visit card's schedule + content + one visitor + operational contact — every
 * field the schema requires for a NEW campus (CampusVisitCard.tsx / visitRequestV2.schema.ts).
 * `index` is the campus's position among `campusVisits[]` (0 for the first card, 1 for a second
 * added via "Thêm cơ sở", ...) — several testids (campus-delegation-input, v2-visitors-table) are
 * NOT campus-indexed themselves but appear in DOM order matching card order, so `.nth(index)`
 * disambiguates; operationalContact fields DO embed the campus index directly.
 */
async function fillCampusVisit(
  page: Page,
  index: number,
  opts: { campusCode: string; dayOffset: number; startHour: number; endHour: number; delegationName: string },
) {
  const pad = (n: number) => String(n).padStart(2, '0');
  const idPrefix = `campus-${index}`;

  await page.locator(`select[name="campusVisits.${index}.campus"]`).selectOption(opts.campusCode);
  await page.locator(`[data-testid="${idPrefix}-start-date"]`).fill(futureDate(opts.dayOffset));
  await page.locator(`[data-testid="${idPrefix}-start-time"]`).fill(`${pad(opts.startHour)}:00`);
  await page.locator(`[data-testid="${idPrefix}-end-time"]`).fill(`${pad(opts.endHour)}:00`);

  await page.locator('[data-testid="campus-delegation-input"]').nth(index).fill(opts.delegationName);

  // purpose / workingContent are Controller-bound AutoGrowTextarea with no testid AND no
  // aria-label (unlike every AutoGrowTextField elsewhere in this card, which always gets an
  // ariaLabel prop) -- `textarea:not([aria-label])` isolates exactly these two per card, in DOM
  // order, across every campus card on the page.
  const bareTextareas = page.locator('textarea:not([aria-label])');
  await bareTextareas.nth(index * 2).fill('Tham quan và trao đổi hợp tác');
  await bareTextareas.nth(index * 2 + 1).fill('Làm việc với phòng hợp tác quốc tế');

  // One visitor row (desktop table instance — v2-visitors-table is `hidden lg:block`, the mobile
  // stacked instance shares no such testid so `.nth(index)` here is unambiguous by itself).
  const visitorsTable = page.locator('[data-testid="v2-visitors-table"]').nth(index);
  await visitorsTable.locator('[data-testid="visitors-0-fullName"]').fill('Trần Thị Khách');
  await visitorsTable.locator('[data-testid="visitors-0-jobTitle"]').fill('Trưởng đoàn');
  // Escape here would bubble past react-select up to the modal's own cancelConfirm ("Bạn chưa
  // hoàn tất đăng ký") handler. Tab is react-select's own documented way to commit the typed text
  // and close the menu (its live region literally says so) -- a later click on another field isn't
  // reliable here because the still-open portalled menu can visually cover a field below/beside it.
  const visitorOrg = visitorsTable.locator('input[role="combobox"]').first();
  await visitorOrg.click();
  await visitorOrg.pressSequentially('Đại học ABC');
  await page.keyboard.press('Tab');
  // Nationality is the SECOND combobox in the row (organization is the first).
  const visitorNationality = visitorsTable.locator('input[role="combobox"]').nth(1);
  await visitorNationality.click();
  await visitorNationality.pressSequentially('Viet');
  await page.keyboard.press('Enter');

  // Operational contact — direct fill (there is no "same as registrant" shortcut in the current
  // UI; the "Đầu mối là ai trong đoàn?" picker is an alternative path, not required). Deliberately
  // a DIFFERENT name from the visitor above: matching an existing delegation member's identity
  // (NP-03) opens a "same person?" confirmation dialog that blocks submit until answered — that
  // matching behaviour has its own coverage in operationalContactMemberIdentity.test.tsx.
  await page.locator(`[data-testid="campus-opcontact-name"]`).nth(index).fill('Lê Văn Điều Phối');
  const contactOrg = page.locator(`[data-testid="campus-opcontact-org"]`).nth(index)
    .locator('input[role="combobox"]');
  await contactOrg.click();
  await contactOrg.pressSequentially('Đại học ABC');
  await page.keyboard.press('Tab');
  await page.waitForTimeout(300);
  await page.locator(`[data-testid="campus-opcontact-jobtitle"]`).nth(index).fill('Trưởng đoàn');
  await page.locator(`[data-testid="campus-opcontact-email-${index}"]`).fill('contact@example.com');
}

/** Fills the registrant section + one valid single-campus visit (Hà Nội, 5 days out, 9h–15h). */
async function fillValidForm(page: Page) {
  await page.locator('[data-testid="v2-registrant-fullName"]').fill('Nguyễn Văn Test');
  await pickCountry(page, '#v2-registrant');
  await page.locator('#v2-registrant input[placeholder*="tổ chức"]').fill('Công ty Kiểm Thử');
  await page.locator('[data-testid="v2-registrant-jobTitle"]').fill('Giám đốc');
  await page.locator('#v2-registrant input[type="tel"]').fill('912345678');
  await page.locator('[data-testid="v2-registrant-email"]').fill('registrant@example.com');

  await fillCampusVisit(page, 0, {
    campusCode: 'HN', dayOffset: 5, startHour: 9, endHour: 15, delegationName: 'Đoàn Kiểm Thử',
  });
}

async function submitAndOpenOtp(page: Page) {
  await page.getByRole('button', { name: 'Gửi yêu cầu & nhận mã OTP' }).click();
  await expect(page.getByText('Xác thực OTP')).toBeVisible();
  // Exact match: a review banner ("Đã gửi mã xác minh đến {{email}}.") also contains this
  // substring, so a loose match is a strict-mode violation (2 elements).
  await expect(page.getByText('te***@example.com', { exact: true })).toBeVisible();
}

async function enterOtp(page: Page, code: string) {
  await page.getByPlaceholder('______').fill(code);
  await page.getByRole('button', { name: 'Xác nhận' }).click();
}

test.describe('UC17 public visit request (per-campus v2 form)', () => {
  test.beforeEach(async ({ page }) => {
    await mockPartnerSearch(page);
    await mockRegistrationCampuses(page);
  });

  test('TC-01: the real section structure — no wizard, registrant + campus card in one modal', async ({ page }) => {
    await gotoVi(page);
    await openVisitForm(page);

    // No stepper, no step navigation, no step counter (still a flat form, never a wizard).
    await expect(page.getByRole('button', { name: 'Tiếp theo' })).toHaveCount(0);
    await expect(page.getByRole('button', { name: 'Quay lại' })).toHaveCount(0);
    for (const counter of ['1 / 3', '2 / 3', '3 / 3']) {
      await expect(page.getByText(counter)).toHaveCount(0);
    }

    // The two REAL request-level sections (FormSection → real <h2>). "Thành viên đoàn khách" /
    // "Đội hỗ trợ khách" / "Đầu mối liên hệ" / "Thông tin chuyến thăm" are not section headings
    // anymore — visitors/support/contact/schedule are all inside each campus card now.
    await expect(page.getByRole('heading', { name: 'Thông tin người đăng ký' })).toBeVisible();
    await expect(page.getByRole('heading', { name: 'Nội dung tham quan theo cơ sở' })).toBeVisible();

    // The campus card itself carries the equivalent groupings, as fieldset legends (not headings).
    await expect(page.getByText('Yêu cầu bổ sung')).toBeVisible();

    // Exactly one primary CTA.
    await expect(page.getByRole('button', { name: 'Gửi yêu cầu & nhận mã OTP' })).toHaveCount(1);
  });

  test('TC-02: empty submit shows validation and never calls /initiate', async ({ page }) => {
    const initiate = await mockInitiate(page);
    await gotoVi(page);
    await openVisitForm(page);

    await page.getByRole('button', { name: 'Gửi yêu cầu & nhận mã OTP' }).click();

    await expect(page.getByText('Họ tên không được để trống').first()).toBeVisible();
    expect(initiate.calls).toBe(0);
  });

  test('TC-03/05/06/07/08: valid submit → OTP (cancel, wrong, right) → summary → clean reopen', async ({ page }) => {
    const initiate = await mockInitiate(page);
    await page.route('**/v2/visit-requests/verify', async (route) => {
      const body = route.request().postDataJSON() as { otpCode?: string };
      if (body?.otpCode === '000000') {
        await route.fulfill({ status: 400, json: { errorCode: 'OTP_INVALID', message: 'Mã OTP không đúng' } });
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
    await expect(page.locator('[data-testid="v2-registrant-fullName"]')).toHaveValue('Nguyễn Văn Test');
    await expect(page.locator('[data-testid="v2-visitors-table"]').first()
      .locator('[data-testid="visitors-0-fullName"]')).toHaveValue('Trần Thị Khách');

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
    // The receipt names the campus and the submit time, then says how to follow the request. It
    // states no queue status and no request code: neither is something the visitor can act on.
    await expect(page.getByTestId('v2-success-title')).toContainText('Hà Nội');
    await expect(page.getByTestId('v2-success-note')).toContainText('registrant@example.com');
    await expect(page.getByTestId('v2-success-status')).toHaveCount(0);
    await expect(page.getByText('VR-2026-000123')).toHaveCount(0);

    // …but never the OTP code (exact match: the code must not appear as any element's text).
    await expect(page.getByText('135790', { exact: true })).toHaveCount(0);

    // No auto-close: still on the summary after >5s.
    await page.waitForTimeout(5200);
    await expect(page.getByRole('heading', { name: 'Đã gửi yêu cầu tham quan' })).toBeVisible();

    // TC-08: closing does not ask for cancel-confirmation and reopening shows a blank form.
    await page.getByTestId('v2-success-close').click();
    await expect(page.getByText('Hủy form đăng ký?')).toHaveCount(0);
    await expect(page.getByRole('heading', { name: 'Đã gửi yêu cầu tham quan' })).toHaveCount(0);

    await openVisitForm(page);
    await expect(page.getByText('Khôi phục thông tin đã nhập?')).toHaveCount(0);
    await expect(page.locator('[data-testid="v2-registrant-fullName"]')).toHaveValue('');
    await expect(page.getByTestId('v2-success-title')).toHaveCount(0);
  });

  /**
   * REWRITTEN: the old "overlapping schedule requires confirmation" step does not exist anywhere
   * in the current codebase (confirmed by a repo-wide search for its dialog text/i18n keys — zero
   * matches). Campuses are now independent snapshots (plan §5: "Same for all campuses is a
   * one-time UI copy — never a shared/inherited state"), and two campuses visited at overlapping
   * times is no longer flagged as suspicious. What remains valuable and real: adding a second
   * campus works, and submit sends exactly one /initiate call carrying BOTH campuses.
   */
  test('TC-04: adding a second campus sends exactly one /initiate call carrying both', async ({ page }) => {
    let calls = 0;
    // Wire payload field is `campusId` (holding the campus CODE, e.g. "HN") -- V2CampusVisitForm,
    // not the form state's own `campus` field name.
    const bodies: Array<{ form?: { campusVisits?: Array<{ campusId?: string; delegationName?: string }> } }> = [];
    await page.route('**/v2/visit-requests/initiate', async (route) => {
      calls += 1;
      bodies.push(route.request().postDataJSON());
      await route.fulfill({
        json: {
          sessionToken: 'test-session-token', maskedEmail: 'te***@example.com', message: 'OTP sent',
          expiresAt: '2099-01-01T00:05:00', resendAfterSeconds: 60, maxAttempts: 10,
        },
      });
    });

    await gotoVi(page);
    await openVisitForm(page);
    await fillValidForm(page);

    await page.getByRole('button', { name: /Thêm cơ sở/ }).click();
    await fillCampusVisit(page, 1, {
      campusCode: 'DN', dayOffset: 6, startHour: 10, endHour: 14, delegationName: 'Đoàn Kiểm Thử 2',
    });

    await page.getByRole('button', { name: 'Gửi yêu cầu & nhận mã OTP' }).click();
    await expect(page.getByText('Xác thực OTP')).toBeVisible();

    expect(calls).toBe(1);
    const sentCampuses = bodies[0]?.form?.campusVisits ?? [];
    expect(sentCampuses).toHaveLength(2);
    expect(sentCampuses.map(c => c.campusId)).toEqual(['HN', 'DN']);
  });

  /**
   * The contact-email-conflict business rule itself is thoroughly unit-tested (12 cases, both
   * languages, both registrant and delegation-contact refusals) in
   * visitRequestV2ContactEmailRejected.test.tsx against this exact schema. That unit test also
   * reveals where this rejection ACTUALLY happens now: at /initiate (400, before any OTP), not at
   * /verify after OTP entry as the old test assumed -- the OTP modal never opens on this path at
   * all. This is a single real end-to-end check that the wiring — real submit, real error
   * rendering on the exact field — works, not a re-proof of the unit test's depth.
   */
  test('TC-09: a rejected contact email lands on that field, no OTP modal opens', async ({ page }) => {
    await page.route('**/v2/visit-requests/initiate', (route) =>
      route.fulfill({
        status: 400,
        json: {
          success: false,
          errorCode: 'CONTACT_EMAIL_CANNOT_BE_USED_FOR_VISITOR_ACCOUNT',
          message: 'Không thể sử dụng email này cho đầu mối của đoàn. Vui lòng nhập email khác của khách hoặc đối tác bên ngoài.',
          errors: {
            'CampusVisits[0].OperationalContact.Email':
              ['Không thể sử dụng email này cho đầu mối của đoàn. Vui lòng nhập email khác của khách hoặc đối tác bên ngoài.'],
          },
        },
      })
    );

    await gotoVi(page);
    await openVisitForm(page);
    await fillValidForm(page);
    await page.getByRole('button', { name: 'Gửi yêu cầu & nhận mã OTP' }).click();

    // No OTP round-trip on this path — the refusal is known before any code is sent.
    await expect(page.getByText('Xác thực OTP')).toHaveCount(0);
    // The message lands under THIS campus's own contact email field.
    const emailField = page.locator('[data-testid="campus-opcontact-email-0"]')
      .locator('xpath=ancestor::div[@data-field-error="true"][1]');
    await expect(emailField.getByText('Không thể sử dụng email này cho đầu mối của đoàn'))
      .toBeVisible();
    // Registrant data survives the refusal — nothing was cleared.
    await expect(page.locator('[data-testid="v2-registrant-fullName"]')).toHaveValue('Nguyễn Văn Test');
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
    await expect(page.getByRole('button', { name: 'Gửi yêu cầu & nhận mã OTP' })).toBeVisible();

    // The desktop-only visitor table is replaced by the stacked list on mobile.
    await expect(page.locator('[data-testid="v2-visitors-table"]').first()).toBeHidden();
  });
});

// ─────────────────────────────────────────────────────────────────────────────
// UC17 OTP V2 — attempt metadata, server cooldown, human verification (Turnstile),
// recovery, submission idempotency wiring, and the current idempotent-replay behaviour.
// All backend endpoints + the Turnstile callback are mocked; nothing real is called
// (without VITE_TURNSTILE_SITE_KEY the widget renders its explicit dev fallback button).
// ─────────────────────────────────────────────────────────────────────────────

test.describe('UC17 OTP V2 + idempotent replay', () => {
  test.beforeEach(async ({ page }) => {
    await mockPartnerSearch(page);
    await mockRegistrationCampuses(page);
  });

  test('V2-01: wrong OTP shows server remaining attempts', async ({ page }) => {
    await mockInitiate(page);
    await page.route('**/v2/visit-requests/verify', (route) =>
      route.fulfill({
        status: 400,
        json: {
          errorCode: 'OTP_INVALID',
          message: 'Mã OTP không đúng. Vui lòng kiểm tra lại.',
          remainingAttempts: 9,
          retryAfterSeconds: null,
          humanVerificationRequired: false,
        },
      })
    );

    await gotoVi(page);
    await openVisitForm(page);
    await fillValidForm(page);
    await submitAndOpenOtp(page);

    await enterOtp(page, '000000');
    await expect(page.getByText('Mã OTP không đúng. Vui lòng kiểm tra lại.')).toBeVisible();
    await expect(page.getByTestId('otp-remaining-attempts')).toHaveText('Còn 9 lần thử');
    // Still on OTP entry, not human verification.
    await expect(page.getByText('Xác thực OTP')).toBeVisible();
  });

  test('V2-02: server cooldown disables confirm until the countdown ends', async ({ page }) => {
    await mockInitiate(page);
    await page.route('**/v2/visit-requests/verify', (route) =>
      route.fulfill({
        status: 400,
        json: {
          errorCode: 'OTP_INVALID',
          message: 'Mã OTP không đúng. Vui lòng kiểm tra lại.',
          remainingAttempts: 4,
          retryAfterSeconds: 8,
          humanVerificationRequired: false,
        },
      })
    );

    await gotoVi(page);
    await openVisitForm(page);
    await fillValidForm(page);
    await submitAndOpenOtp(page);

    await enterOtp(page, '000000');
    await expect(page.getByTestId('otp-retry-countdown')).toBeVisible();
    await page.getByPlaceholder('______').fill('123456');
    await expect(page.getByRole('button', { name: 'Xác nhận' })).toBeDisabled();
  });

  test('V2-03/04/05/06: 10th wrong → human verification → CAPTCHA fail keeps state → success issues new OTP → submit succeeds', async ({ page }) => {
    await mockInitiate(page);

    // Verify: burned challenge until the recovered session token is used, then success.
    await page.route('**/v2/visit-requests/verify', async (route) => {
      const body = route.request().postDataJSON() as { sessionToken?: string };
      if (body?.sessionToken === 'recovered-session-token') {
        await route.fulfill({ json: VERIFY_OK });
      } else {
        await route.fulfill({
          status: 428,
          json: {
            errorCode: 'OTP_HUMAN_VERIFICATION_REQUIRED',
            message: 'Bạn đã nhập sai quá nhiều lần.',
            remainingAttempts: 0,
            humanVerificationRequired: true,
          },
        });
      }
    });

    // Recover: first call fails (CAPTCHA rejected), second succeeds with a NEW session.
    // Shared v1/v2 endpoint (endpoints.ts: visitRequests.otpRecover) -- unchanged by the v2 rewrite.
    let recoverCalls = 0;
    await page.route('**/visit-requests/otp/recover', async (route) => {
      recoverCalls += 1;
      if (recoverCalls === 1) {
        await route.fulfill({
          status: 400,
          json: {
            errorCode: 'HUMAN_VERIFICATION_FAILED',
            message: 'Xác minh không thành công. Vui lòng thử lại.',
            humanVerificationRequired: true,
          },
        });
      } else {
        await route.fulfill({
          json: {
            sessionToken: 'recovered-session-token',
            maskedEmail: 'te***@example.com',
            message: 'Mã mới đã được gửi.',
            resendAfterSeconds: 60,
            maxAttempts: 10,
          },
        });
      }
    });

    await gotoVi(page);
    await openVisitForm(page);
    await fillValidForm(page);
    await submitAndOpenOtp(page);

    // V2-03: server says the challenge is burned → human verification screen replaces OTP entry.
    await enterOtp(page, '000000');
    await expect(page.getByRole('heading', { name: 'Xác minh bạn không phải robot' })).toBeVisible();
    await expect(page.getByPlaceholder('______')).toHaveCount(0);
    await expect(page.getByTestId('turnstile-fallback')).toBeVisible();

    // V2-04: CAPTCHA failure keeps the human-verification state AND the form data.
    await page.getByTestId('turnstile-fallback').click();
    await expect(page.getByText('Xác minh không thành công. Vui lòng thử lại.')).toBeVisible();
    await expect(page.getByRole('heading', { name: 'Xác minh bạn không phải robot' })).toBeVisible();

    // V2-05: CAPTCHA success returns to OTP entry with a fresh challenge.
    await page.getByTestId('turnstile-fallback').click();
    await expect(page.getByText('Xác thực OTP')).toBeVisible();
    await expect(page.getByPlaceholder('______')).toHaveValue('');

    // V2-06: the new OTP verifies successfully; summary shows and never auto-closes.
    await enterOtp(page, '135790');
    await expect(page.getByRole('heading', { name: 'Đã gửi yêu cầu tham quan' })).toBeVisible();
    await page.waitForTimeout(3200);
    await expect(page.getByRole('heading', { name: 'Đã gửi yêu cầu tham quan' })).toBeVisible();
  });

  /**
   * REWRITTEN from the old "dedicated duplicate screen" scenario (409 DUPLICATE_VISIT_REQUEST).
   * The current backend answers a resubmit of the SAME submissionId with a 200 success carrying
   * `idempotent: true` — VisitRequestV2SuccessPanel renders the ordinary success screen plus an
   * inline "already recorded" notice (visitRequestV2:success.idempotentReplay), not a separate
   * heading/badge/screen.
   */
  test('V2-07: an idempotent replay shows the SAME success screen with an inline notice, not a new request', async ({ page }) => {
    const initiate = await mockInitiate(page);
    let verifyCalls = 0;
    await page.route('**/v2/visit-requests/verify', async (route) => {
      verifyCalls += 1;
      // First verify: normal success. A retry with the same submissionId would be answered
      // idempotent:true by a real backend; simulated here directly since /initiate is mocked and
      // the browser has no reason to actually replay verify in this scenario.
      await route.fulfill({ json: verifyCalls === 1 ? VERIFY_OK : { ...VERIFY_OK, idempotent: true } });
    });

    await gotoVi(page);
    await openVisitForm(page);
    await fillValidForm(page);
    await submitAndOpenOtp(page);
    await enterOtp(page, '135790');

    await expect(page.getByRole('heading', { name: 'Đã gửi yêu cầu tham quan' })).toBeVisible();
    await expect(page.getByTestId('v2-success-title')).toContainText('Hà Nội');
    // No idempotent-replay notice on a first-time, non-replayed create.
    await expect(page.getByText('Yêu cầu này đã được ghi nhận trước đó')).toHaveCount(0);

    // No auto-close; closing resets to a blank form.
    await page.waitForTimeout(3200);
    await expect(page.getByRole('heading', { name: 'Đã gửi yêu cầu tham quan' })).toBeVisible();
    await page.getByTestId('v2-success-close').click();
    await openVisitForm(page);
    await expect(page.getByText('Khôi phục thông tin đã nhập?')).toHaveCount(0);
    await expect(page.locator('[data-testid="v2-registrant-fullName"]')).toHaveValue('');
    expect(initiate.calls).toBe(1);
  });

  test('V2-08: verify carries the SAME submissionId + sessionToken issued at initiate', async ({ page }) => {
    const initiate = await mockInitiate(page);
    const verifyBodies: Array<{ form?: { submissionId?: string }; sessionToken?: string }> = [];
    await page.route('**/v2/visit-requests/verify', async (route) => {
      verifyBodies.push(route.request().postDataJSON());
      await route.fulfill({
        status: 400,
        json: { errorCode: 'OTP_INVALID', message: 'Mã OTP không đúng.', remainingAttempts: 9 },
      });
    });

    await gotoVi(page);
    await openVisitForm(page);
    await fillValidForm(page);
    await submitAndOpenOtp(page);

    await enterOtp(page, '000000');
    await expect(page.getByTestId('otp-remaining-attempts')).toBeVisible();
    await enterOtp(page, '000001');

    expect(initiate.lastSubmissionId).toMatch(/^[0-9a-f-]{36}$/i); // a real UUID
    expect(verifyBodies.length).toBe(2);
    for (const body of verifyBodies) {
      expect(body.form?.submissionId).toBe(initiate.lastSubmissionId); // intent kept across retries
      expect(body.sessionToken).toBe('test-session-token');
    }
  });

  test('V2-09: 390×844 — human verification does not overflow', async ({ page }) => {
    await mockInitiate(page);
    await page.route('**/v2/visit-requests/verify', async (route) => {
      await route.fulfill({
        status: 428,
        json: { errorCode: 'OTP_HUMAN_VERIFICATION_REQUIRED', message: 'x', humanVerificationRequired: true },
      });
    });

    await gotoVi(page);
    await openVisitForm(page);
    // Fill on the desktop layout (the visitor table is desktop-only), THEN go mobile.
    await fillValidForm(page);
    await page.setViewportSize({ width: 390, height: 844 });
    await submitAndOpenOtp(page);

    // Human verification on mobile: visible and no horizontal page overflow.
    await enterOtp(page, '000000');
    await expect(page.getByRole('heading', { name: 'Xác minh bạn không phải robot' })).toBeVisible();
    const humanOverflow = await page.evaluate(
      () => document.documentElement.scrollWidth - document.documentElement.clientWidth
    );
    expect(humanOverflow).toBeLessThanOrEqual(0);
  });
});
