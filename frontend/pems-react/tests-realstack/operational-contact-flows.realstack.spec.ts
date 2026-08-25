/**
 * REAL-STACK E2E — Operational Contact live-browser closure (FLOW 01-08 + the Registrant→Visitor→MEMBER
 * smoke, plan CanhIter3FixBug "FINAL OPERATIONAL CONTACT CLOSURE").
 *
 * real Chromium → real React (Vite) → real .NET API (Testing, flags ON, fail-closed E2E auth) →
 * disposable MySQL. NO network mocking. Preconditions are created through the REAL authenticated API
 * (same convention every other real-stack spec in this repo uses); the action under test is always
 * driven through the DOM, and every flow creates its OWN request — no ordering dependency between tests.
 */
import { test, expect } from '@playwright/test';
import {
  API_BASE, hdr, authedPage, meUser, OWNER_USER,
  KIM, MOON, campusBlockKimLinked, campusBlockExternalLee, campusBlockLinkedPerson,
  createKimRequest, readDetail, campusOf, sinkSize, waitForContactEmail,
} from './operationalContactRealstackHelpers';

const HN_HOST_USER_ID = 3;

const tagOf = (prefix: string) => `${prefix}${Date.now().toString(36)}${Math.random().toString(36).slice(2, 6)}`;

/** Precondition helper (allowed by plan §B — real authenticated API, not a mock): a campus leader
 * approves their campus, exactly mirroring `realstackHelpers.ts`'s own `approveCampus`. */
async function approveCampus(request: import('@playwright/test').APIRequestContext, requestId: number, instanceId: number, leaderKey: string, hostUserId = HN_HOST_USER_ID) {
  const detail = await readDetail(request, requestId);
  const row = detail.campusVisits.find((c: any) => c.visitInstanceId === instanceId);
  const res = await request.post(`${API_BASE}/delegations/${requestId}/campuses/${instanceId}/approve`, {
    headers: hdr(leaderKey), data: { hostUserId, decisionNote: 'assign', expectedInstanceRowVersion: row.rowVersion },
  });
  expect(res.ok(), `campus approve failed: ${res.status()} ${await res.text()}`).toBeTruthy();
  return res.json();
}

/** Finds the ONE <select> inside `container` whose disabled placeholder option matches `placeholderText`
 * — used for the Copy-From-Campus source picker, which carries no `data-testid` of its own. */
async function selectWithPlaceholder(container: import('@playwright/test').Locator, placeholderText: string) {
  const selects = container.locator('select');
  const count = await selects.count();
  for (let i = 0; i < count; i++) {
    const opts = await selects.nth(i).locator('option').allTextContents();
    if (opts.some(o => o.includes(placeholderText))) return selects.nth(i);
  }
  throw new Error(`No <select> with a "${placeholderText}" placeholder option found`);
}

test.describe('Real-stack: Operational Contact live-browser closure', () => {
  // ── FLOW 01 — Linked Contact + Safe Edit ─────────────────────────────────────────────────────────
  test('FLOW 01 — linked contact: FullName/Org/JobTitle read-only, Email locked, Phone editable and persists', async ({ browser, request }) => {
    const tag = tagOf('F1');
    const { requestId, instances } = await createKimRequest(request, tag, [campusBlockKimLinked('HN', 0, tag, `Doan ${tag}`)]);
    const instanceId = instances[0].visitInstanceId;

    const { context, page } = await authedPage(browser, 'visitor_owner', OWNER_USER);
    try {
      await page.goto(`/dashboard/visit/v2/${requestId}`);
      await expect(page.getByTestId(`operational-contact-${instanceId}-full-name`)).toHaveText(KIM.fullName, { timeout: 25_000 });

      await page.getByTestId('safe-edit-open').click();
      await expect(page.getByTestId(`safe-edit-contact-${instanceId}`)).toBeVisible();

      const fullNameField = page.getByTestId(`safe-edit-contact-fullName-${instanceId}`);
      await expect(fullNameField).toHaveText(KIM.fullName);
      expect(await fullNameField.evaluate(el => el.tagName)).toBe('P'); // read-only, not an <input>
      const orgField = page.getByTestId(`safe-edit-contact-organization-${instanceId}`);
      expect(await orgField.evaluate(el => el.tagName)).toBe('P');
      const jobTitleField = page.getByTestId(`safe-edit-contact-jobTitle-${instanceId}`);
      expect(await jobTitleField.evaluate(el => el.tagName)).toBe('P');
      const emailField = page.getByTestId(`safe-edit-contact-email-${instanceId}`);
      expect(await emailField.evaluate(el => el.tagName)).toBe('P'); // ALWAYS locked, linked or not

      const phoneInput = page.getByTestId(`safe-edit-contact-phone-${instanceId}`);
      await expect(phoneInput).toBeEditable();
      await phoneInput.fill('0987000111');
      await page.getByTestId('safe-edit-submit').click();
      await expect(page.getByRole('dialog')).toHaveCount(0, { timeout: 15_000 });

      await page.reload();
      await expect(page.getByTestId(`operational-contact-${instanceId}-phone`)).toHaveText('+84987000111', { timeout: 25_000 });
      await expect(page.getByTestId(`operational-contact-${instanceId}-full-name`)).toHaveText(KIM.fullName);
    } finally {
      await context.close();
    }

    const detail = await readDetail(request, requestId);
    const cv = campusOf(detail, 'HN');
    expect(cv.operationalContact.phone).toBe('+84987000111');
    expect(cv.operationalContact.fullName).toBe(KIM.fullName);
    expect(cv.operationalContact.jobTitle).toBe(KIM.jobTitle);
    expect(cv.operationalContact.organization).toBe(KIM.organization);
    expect(cv.operationalContact.guestMemberId, 'relation still points at Kim').not.toBeNull();
  });

  // ── FLOW 02 — Pending Edit relation protection ───────────────────────────────────────────────────
  test('FLOW 02 — Pending Edit exposes no relation control, and a legitimate field still saves', async ({ browser, request }) => {
    const tag = tagOf('F2');
    const { requestId, instances } = await createKimRequest(request, tag, [campusBlockKimLinked('HN', 0, tag, `Doan ${tag}`)]);
    const instanceId = instances[0].visitInstanceId;
    const newDelegation = `Doan Sua ${tag}`;

    const { context, page } = await authedPage(browser, 'visitor_owner', OWNER_USER);
    try {
      await page.goto(`/dashboard/visit/v2/${requestId}/campus/${instanceId}/edit`);
      await expect(page.getByTestId('pending-campus-save')).toBeVisible({ timeout: 25_000 });

      // Read-only contact summary — no relation selector anywhere on this screen.
      await expect(page.getByTestId('campus-opcontact-readonly-0')).toBeVisible();
      await expect(page.getByTestId('campus-opcontact-source-member-0')).toHaveCount(0);
      await expect(page.getByTestId('campus-opcontact-source-external-0')).toHaveCount(0);
      await expect(page.getByTestId('campus-opcontact-pick-0')).toHaveCount(0);
      await expect(page.getByTestId('campus-opcontact-relation-readonly-0')).toBeVisible();
      await expect(page.getByTestId('campus-opcontact-relation-readonly-0').locator('select')).toHaveCount(0);

      // A legitimate, editable field.
      await page.getByTestId('campus-delegation-input').fill(newDelegation);
      await page.getByTestId('pending-campus-save').click();
      await expect(page).toHaveURL(new RegExp(`/dashboard/visit/v2/${requestId}$`), { timeout: 20_000 });
    } finally {
      await context.close();
    }

    const detail = await readDetail(request, requestId);
    const cv = campusOf(detail, 'HN');
    expect(cv.delegationName).toBe(newDelegation);
    expect(cv.operationalContact.fullName).toBe(KIM.fullName);
    expect(cv.operationalContact.guestMemberId, 'relation unchanged').not.toBeNull();
  });

  // ── FLOW 03 — Linked member → contact sync ───────────────────────────────────────────────────────
  test('FLOW 03 — editing the linked member through Pending Edit syncs the Operational Contact', async ({ browser, request }) => {
    const tag = tagOf('F3');
    const { requestId, instances } = await createKimRequest(request, tag, [campusBlockKimLinked('HN', 0, tag, `Doan ${tag}`)]);
    const instanceId = instances[0].visitInstanceId;

    const { context, page } = await authedPage(browser, 'visitor_owner', OWNER_USER);
    try {
      await page.goto(`/dashboard/visit/v2/${requestId}/campus/${instanceId}/edit`);
      await expect(page.getByTestId('pending-campus-save')).toBeVisible({ timeout: 25_000 });

      await page.getByTestId('visitors-0-jobTitle').first().fill('Senior Director');
      await page.getByTestId('pending-campus-save').click();
      await expect(page).toHaveURL(new RegExp(`/dashboard/visit/v2/${requestId}$`), { timeout: 20_000 });
    } finally {
      await context.close();
    }

    const detail = await readDetail(request, requestId);
    const cv = campusOf(detail, 'HN');
    expect(cv.visitors[0].jobTitle).toBe('Senior Director');
    expect(cv.operationalContact.jobTitle, 'contact synced from the linked member').toBe('Senior Director');
    expect(cv.operationalContact.guestMemberId).toBe(cv.visitors[0].guestMemberId);
    // The reverse never happens: fullName/organization were never touched by this edit.
    expect(cv.operationalContact.fullName).toBe(KIM.fullName);
    expect(cv.visitors[0].fullName).toBe(KIM.fullName);
  });

  // ── FLOW 04 — Safe Edit unlink + relink ──────────────────────────────────────────────────────────
  test('FLOW 04 — Safe Edit: explicit unlink, then explicit exact-match relink', async ({ browser, request }) => {
    const tag = tagOf('F4');
    const { requestId, instances } = await createKimRequest(request, tag, [campusBlockKimLinked('HN', 0, tag, `Doan ${tag}`)]);
    const instanceId = instances[0].visitInstanceId;

    const { context, page } = await authedPage(browser, 'visitor_owner', OWNER_USER);
    try {
      await page.goto(`/dashboard/visit/v2/${requestId}`);
      await page.getByTestId('safe-edit-open').click();
      await expect(page.getByTestId(`safe-edit-contact-${instanceId}`)).toBeVisible();

      await page.getByTestId(`safe-edit-contact-relation-${instanceId}`).selectOption('');
      await page.getByTestId('safe-edit-submit').click();
      await expect(page.getByRole('dialog')).toHaveCount(0, { timeout: 15_000 });
    } finally {
      await context.close();
    }

    let detail = await readDetail(request, requestId);
    let cv = campusOf(detail, 'HN');
    expect(cv.operationalContact.guestMemberId, 'unlinked').toBeNull();
    expect(cv.operationalContact.fullName, 'snapshot still valid after unlink').toBe(KIM.fullName);

    const { context: c2, page: p2 } = await authedPage(browser, 'visitor_owner', OWNER_USER);
    try {
      await p2.goto(`/dashboard/visit/v2/${requestId}`);
      await p2.getByTestId('safe-edit-open').click();
      await expect(p2.getByTestId(`safe-edit-contact-${instanceId}`)).toBeVisible();
      await p2.getByTestId(`safe-edit-contact-relation-${instanceId}`).selectOption({ label: KIM.fullName });
      await p2.getByTestId('safe-edit-submit').click();
      await expect(p2.getByRole('dialog')).toHaveCount(0, { timeout: 15_000 });
    } finally {
      await c2.close();
    }

    detail = await readDetail(request, requestId);
    cv = campusOf(detail, 'HN');
    expect(cv.operationalContact.guestMemberId, 'relinked, no fuzzy matching needed').not.toBeNull();
    expect(cv.operationalContact.fullName).toBe(KIM.fullName);
    expect(cv.operationalContact.jobTitle).toBe(KIM.jobTitle);
    expect(cv.operationalContact.organization).toBe(KIM.organization);
  });

  // ── FLOW 05 — Transfer Kim → Moon ────────────────────────────────────────────────────────────────
  test('FLOW 05 — Transfer: Initiate keeps Kim as holder; Accept is the actual handover', async ({ browser, request, page: anonPage }) => {
    const tag = tagOf('F5');
    const { requestId, instances } = await createKimRequest(request, tag, [campusBlockKimLinked('HN', 0, tag, `Doan ${tag}`)]);
    const instanceId = instances[0].visitInstanceId;
    const moonEmail = `moon.${tag}@example.com`;
    const before = sinkSize();

    const { context, page: kimPage } = await authedPage(browser, 'visitor_owner', OWNER_USER);
    try {
      await kimPage.goto(`/dashboard/visit/v2/${requestId}`);
      await expect(kimPage.getByTestId(`operational-contact-${instanceId}-full-name`)).toHaveText(KIM.fullName, { timeout: 25_000 });

      await kimPage.getByTestId('contact-edit-open').click();
      await expect(kimPage.getByTestId('contact-form')).toBeVisible();
      await kimPage.getByTestId('contact-field-fullName').fill(MOON.fullName);
      // organization is an OrganizationCombobox — the testid lands on its wrapper div, not the <input>.
      await kimPage.getByTestId('contact-field-organization').locator('input').fill(MOON.organization);
      await kimPage.getByTestId('contact-field-jobTitle').fill(MOON.jobTitle);
      await kimPage.getByTestId('contact-field-email').fill(moonEmail);
      await kimPage.getByTestId('contact-form-submit').click();

      // Immediately after Initiate: Kim is STILL the current holder, Transfer is Pending.
      await expect(kimPage.getByTestId('contact-transfer-pending')).toBeVisible({ timeout: 15_000 });
      await expect(kimPage.getByTestId(`operational-contact-${instanceId}-full-name`)).toHaveText(KIM.fullName);
    } finally {
      await context.close();
    }

    let detail = await readDetail(request, requestId);
    let cv = campusOf(detail, 'HN');
    expect(cv.operationalContact.fullName, 'Initiate never changes the holder').toBe(KIM.fullName);
    expect(cv.operationalContact.confirmationStatus).toBe('TRANSFER_PENDING');

    const mail = await waitForContactEmail('VISIT_CONTACT_TRANSFER', moonEmail, before);
    expect(mail.link, 'the transfer link is present').toBeTruthy();
    // The token is a PATH segment here (…/operational-contact-confirmation/{token}), not a query param
    // — unlike the account-email-confirmation link, which uses ?token=.
    const token = new URL(mail.link!).pathname.split('/').filter(Boolean).pop();
    expect(token).toBeTruthy();

    // Moon accepts through the REAL public confirmation page — token is the authorization, no account
    // needed. VI is set explicitly (same convention as `authedPage`): a fresh anonymous session with no
    // `pems.language` in localStorage falls back to English, which the VI-text assertion below would miss.
    await anonPage.addInitScript(() => localStorage.setItem('pems.language', 'vi'));
    await anonPage.goto(`/operational-contact-confirmation/${token}`);
    const acceptBtn = anonPage.getByRole('button', { name: 'Đồng ý tiếp nhận vai trò' });
    await expect(acceptBtn).toBeVisible({ timeout: 15_000 });
    await acceptBtn.click();
    await expect(anonPage.getByRole('status')).toBeVisible({ timeout: 15_000 });

    detail = await readDetail(request, requestId);
    cv = campusOf(detail, 'HN');
    expect(cv.operationalContact.fullName).toBe(MOON.fullName);
    // Email identity is normalized (trim + lower-case) server-side — the same rule the frontend's own
    // `normalizeEmail` mirrors — so comparing case-insensitively is correct, not a loosened assertion.
    expect(cv.operationalContact.email.toLowerCase()).toBe(moonEmail.toLowerCase());
    expect(cv.operationalContact.confirmationStatus).toBe('CONFIRMED');
    expect(cv.plannedStartAt, 'schedule unchanged by a contact handover').toBeTruthy();
    expect(cv.instanceStatus).not.toBe('REJECTED');
  });

  // ── FLOW 06 — Copy From Campus ───────────────────────────────────────────────────────────────────
  test('FLOW 06 — Copy From Campus: target keeps its OWN Operational Contact', async ({ browser, request }) => {
    const tag = tagOf('F6');
    const { requestId } = await createKimRequest(request, tag, [
      campusBlockKimLinked('HN', 0, tag, `Doan A ${tag}`),
      campusBlockExternalLee('HCM', 1, tag, `Doan B ${tag}`),
    ]);

    const { context, page } = await authedPage(browser, 'visitor_owner', OWNER_USER);
    try {
      await page.goto(`/dashboard/visit/v2/${requestId}/edit`);
      const hnCard = page.getByTestId('campus-edit-card-HN');
      const hcmCard = page.getByTestId('campus-edit-card-HCM');
      await expect(hnCard).toBeVisible({ timeout: 25_000 });
      await expect(hcmCard).toBeVisible();

      const copySelect = await selectWithPlaceholder(hcmCard, 'Chọn cơ sở nguồn');
      await copySelect.selectOption('0'); // HN is campus card index 0

      // Allowed content (delegation name) copied…
      await expect(hcmCard.getByTestId('campus-delegation-input')).toHaveValue(`Doan A ${tag}`, { timeout: 10_000 });
    } finally {
      await context.close();
    }

    // …but HCM's OWN Operational Contact was never touched by the copy (nothing was submitted).
    const detail = await readDetail(request, requestId);
    const hcm = campusOf(detail, 'HCM');
    expect(hcm.operationalContact.fullName).toBe('Lee Sang Hoon');
  });

  // ── FLOW 07 — Apply-To-All ───────────────────────────────────────────────────────────────────────
  test('FLOW 07a — Apply-To-All (safe): each target keeps its own Operational Contact', async ({ browser, request }) => {
    const tag = tagOf('F7a');
    const { requestId } = await createKimRequest(request, tag, [
      campusBlockKimLinked('HN', 0, tag, `Doan A ${tag}`),
      campusBlockExternalLee('HCM', 1, tag, `Doan B ${tag}`),
    ]);

    const { context, page } = await authedPage(browser, 'visitor_owner', OWNER_USER);
    try {
      await page.goto(`/dashboard/visit/v2/${requestId}/edit`);
      const hnCard = page.getByTestId('campus-edit-card-HN');
      await expect(hnCard).toBeVisible({ timeout: 25_000 });
      await hnCard.getByRole('button', { name: 'Áp dụng cho cơ sở khác' }).click();

      const dialog = page.getByRole('dialog');
      await expect(dialog).toBeVisible();
      await dialog.getByRole('button', { name: 'Áp dụng' }).click();
      await expect(dialog).toHaveCount(0, { timeout: 10_000 });

      await expect(page.getByTestId('campus-edit-card-HCM').getByTestId('campus-delegation-input'))
        .toHaveValue(`Doan A ${tag}`, { timeout: 10_000 });
      await page.getByTestId('v2-edit-submit').click();
      await expect(page).toHaveURL(new RegExp(`/dashboard/visit/v2/${requestId}$`), { timeout: 20_000 });
    } finally {
      await context.close();
    }

    const detail = await readDetail(request, requestId);
    const hn = campusOf(detail, 'HN');
    const hcm = campusOf(detail, 'HCM');
    expect(hn.delegationName).toBe(`Doan A ${tag}`);
    expect(hcm.delegationName, 'business content applied to the target').toBe(`Doan A ${tag}`);
    expect(hn.operationalContact.fullName).toBe(KIM.fullName);
    expect(hcm.operationalContact.fullName, "target keeps its OWN contact").toBe('Lee Sang Hoon');
  });

  test('FLOW 07b — Apply-To-All (unsafe): a protected target blocks the WHOLE operation atomically', async ({ browser, request }) => {
    const tag = tagOf('F7b');
    const { requestId } = await createKimRequest(request, tag, [
      campusBlockKimLinked('HN', 0, tag, `Doan A ${tag}`),
      campusBlockLinkedPerson('HCM', 1, tag, `Doan B ${tag}`, MOON, `moon.${tag}@example.com`),
    ]);
    const before = await readDetail(request, requestId);
    const hcmBefore = campusOf(before, 'HCM');
    expect(hcmBefore.operationalContact.guestMemberId, 'HCM starts linked to its own member (Moon)').not.toBeNull();

    const { context, page } = await authedPage(browser, 'visitor_owner', OWNER_USER);
    try {
      await page.goto(`/dashboard/visit/v2/${requestId}/edit`);
      const hnCard = page.getByTestId('campus-edit-card-HN');
      await expect(hnCard).toBeVisible({ timeout: 25_000 });
      await hnCard.getByRole('button', { name: 'Áp dụng cho cơ sở khác' }).click();
      await expect(page.getByRole('dialog')).toBeVisible();
      await page.getByRole('dialog').getByRole('button', { name: 'Áp dụng' }).click();

      // Blocked — HCM's delegation name must still be its OWN, never HN's copied value.
      await expect(page.getByTestId('campus-edit-card-HCM').getByTestId('campus-delegation-input'))
        .toHaveValue(`Doan B ${tag}`, { timeout: 10_000 });
    } finally {
      await context.close();
    }

    const detail = await readDetail(request, requestId);
    const hcm = campusOf(detail, 'HCM');
    expect(hcm.delegationName, 'no partial mutation').toBe(`Doan B ${tag}`);
    expect(hcm.operationalContact.fullName).toBe(MOON.fullName);
    expect(hcm.operationalContact.guestMemberId).toBe(hcmBefore.operationalContact.guestMemberId);
  });

  // ── FLOW 08 — Amendment ──────────────────────────────────────────────────────────────────────────
  test('FLOW 08 — Amendment: approving a linked-member edit syncs the contact, never the relation itself', async ({ browser, request }) => {
    const tag = tagOf('F8');
    const { requestId, instances } = await createKimRequest(request, tag, [campusBlockKimLinked('HN', 0, tag, `Doan ${tag}`)]);
    const instanceId = instances[0].visitInstanceId;
    await approveCampus(request, requestId, instanceId, 'campus_leader_hn', HN_HOST_USER_ID);

    const { context, page } = await authedPage(browser, 'visitor_owner', OWNER_USER);
    try {
      await page.goto(`/dashboard/visit/v2/${requestId}`);
      await expect(page.getByTestId(`amendment-open-${instanceId}`)).toBeVisible({ timeout: 25_000 });
      await page.getByTestId(`amendment-open-${instanceId}`).click();
      await expect(page.getByTestId('amendment-reason')).toBeVisible();

      await page.getByTestId('amendment-visitors-jobtitle').first().fill('Senior Director');
      await page.getByTestId('amendment-reason').fill(`Cap nhat chuc vu ${tag}`);
      await page.getByTestId('amendment-submit').click();
      await expect(page.getByTestId('amendment-reason')).toHaveCount(0, { timeout: 15_000 }); // modal closed
    } finally {
      await context.close();
    }

    let detail = await readDetail(request, requestId);
    let cv = campusOf(detail, 'HN');
    const selfApproves = cv.amendmentSelfApproves === true;

    if (!selfApproves) {
      expect(cv.operationalContact.jobTitle, 'not yet applied').toBe(KIM.jobTitle);
      expect(cv.activeAmendment, 'a pending proposal exists').toBeTruthy();

      const hostUser = await meUser(request, 'campus_leader_hn');
      const { context: c2, page: p2 } = await authedPage(browser, 'campus_leader_hn', hostUser);
      try {
        await p2.goto(`/dashboard/visit/v2/${requestId}`);
        const approveBtn = p2.getByTestId(`amendment-approve-${cv.activeAmendment.amendmentId}`);
        await expect(approveBtn).toBeVisible({ timeout: 25_000 });
        await approveBtn.click();
        // Wait for the mutation to actually finish — the click only fires the request; reading the API
        // in the very next line would race it. The panel unmounts once `activeAmendment` clears.
        await expect(approveBtn).toHaveCount(0, { timeout: 20_000 });
      } finally {
        await c2.close();
      }
    }

    detail = await readDetail(request, requestId);
    cv = campusOf(detail, 'HN');
    expect(cv.activeAmendment, 'no pending amendment left').toBeFalsy();
    expect(cv.visitors[0].jobTitle).toBe('Senior Director');
    expect(cv.operationalContact.jobTitle, 'contact synced on approval').toBe('Senior Director');
    expect(cv.operationalContact.guestMemberId, 'same relation preserved').toBe(cv.visitors[0].guestMemberId);
    expect(cv.operationalContact.fullName).toBe(KIM.fullName);
  });

  // ── PART C — Registrant → Visitor → MEMBER smoke (Create flow regression) ──────────────────────────
  test('SMOKE — Registrant→Visitor→MEMBER: add-registrant links Kim without a duplicate', async ({ page }) => {
    // The public, unauthenticated v2 entry (App.tsx: "auth-aware in place") — no login needed, matching
    // the plan's scenario of a not-yet-signed-in registrant filling the form.
    await page.goto('/visit-registration/v2');

    await page.getByTestId('v2-registrant-fullName').fill(KIM.fullName);
    await page.getByTestId('v2-registrant-phone').fill(KIM.phone);
    await page.getByTestId('v2-registrant-email').fill(KIM.email);
    await page.getByPlaceholder(/organization\/partner|tổ chức\/đối tác/i).fill(KIM.organization);
    await page.getByTestId('v2-registrant-jobTitle').fill(KIM.jobTitle);

    // Nationality — a strict CountrySelect (react-select). Which country lands is irrelevant to this
    // smoke (nothing here asserts on it); only that some value is set, since `addRegistrantAsVisitorAndLink`
    // blocks with an error when it is blank. Keyboard-driven (open, first option, commit) avoids
    // depending on the exact localized country name for a text match.
    const nationalityLabel = page.locator('label').filter({ hasText: /Quốc tịch|Nationality/ }).first();
    const nationalityField = nationalityLabel.locator('xpath=ancestor::div[contains(@class,"flex-col")][1]');
    const nationalityInput = nationalityField.getByRole('combobox');
    await nationalityInput.click();
    await nationalityInput.press('ArrowDown');
    await nationalityInput.press('Enter');
    await page.waitForTimeout(300); // let the react-select commit render before the next interaction

    await expect(page.getByTestId('campus-opcontact-use-registrant-0')).toBeEnabled({ timeout: 10_000 });
    await page.getByTestId('campus-opcontact-use-registrant-0').click();
    await expect(page.getByTestId('campus-opcontact-source-external-0')).toBeChecked();
    await expect(page.getByTestId('campus-opcontact-phone-0')).toHaveValue(KIM.phone);
    await expect(page.getByTestId('campus-opcontact-email-0')).toHaveValue(KIM.email);
    const visitorsBefore = await page.getByTestId('v2-visitors-table').first().locator('tbody tr').count();

    await page.getByTestId('campus-opcontact-source-member-0').click();
    await expect(page.getByTestId('campus-opcontact-switch-decision-0')).toBeVisible();
    // Before confirming: nothing destroyed yet.
    await expect(page.getByTestId('campus-opcontact-phone-0')).toHaveValue(KIM.phone);
    await expect(page.getByTestId('campus-opcontact-email-0')).toHaveValue(KIM.email);
    expect(await page.getByTestId('v2-visitors-table').first().locator('tbody tr').count()).toBe(visitorsBefore);

    await page.getByTestId('campus-opcontact-switch-add-registrant-0').click();

    await expect(page.getByTestId('campus-opcontact-picked-0')).toBeVisible({ timeout: 10_000 });
    expect(await page.getByTestId('v2-visitors-table').first().locator('tbody tr').count()).toBe(visitorsBefore); // reused the blank row
    await expect(page.getByTestId('campus-opcontact-phone-0')).toHaveValue(KIM.phone);
    await expect(page.getByTestId('campus-opcontact-email-0')).toHaveValue(KIM.email);
  });
});
