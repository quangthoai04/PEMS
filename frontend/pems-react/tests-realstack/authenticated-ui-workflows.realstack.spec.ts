/**
 * REAL-STACK FULL-DOM E2E — the v2 mutation + search workflows driven entirely through the REAL React UI.
 *
 * real Chromium → real React (Vite) → real .NET API (Testing, flags ON, fail-closed E2E auth) → disposable
 * MySQL. NO network mocking. These journeys PROMOTE the API-level journeys (E/F/G/H in
 * `authenticated-workflows.realstack.spec.ts`) to full browser automation: the action under test is performed
 * by navigating a real route, filling real inputs, and clicking a real button/modal — never by a direct API
 * call, `page.evaluate(fetch)`, or `route.fulfill`. Preconditions (create/approve/reject/submit-as-precondition)
 * and post-action state checks use the authenticated API (allowed by §4/§5); the browser only ever adds the
 * opaque profile key + run secret to backend-origin requests.
 *
 * §6 pending-edit · §7 resubmit · §8 safe-edit · §9 amendment submit · §10 leader approve / reject ·
 * §11 wrong-campus visibility + backend denial · §12 withdraw · §13 search matched-contexts isolation.
 */
import { test, expect, type Locator, type Page } from '@playwright/test';
import {
  API_BASE, SECRET, CAMPUS_HN, CAMPUS_HCM, OWNER_USER, HN_HOST_USER_ID,
  hdr, authedPage, meUser, campusBlock, createMixedRequest, readDetail, campusOf,
  approveCampus, rejectCampus, submitAmendmentApi,
} from './realstackHelpers';

/** Click a locator and await the specific backend round-trip it triggers (proves the REAL network mutation). */
async function clickAndWait(page: Page, locator: Locator, urlPart: string, method = 'POST') {
  const [resp] = await Promise.all([
    page.waitForResponse(r => r.url().includes(urlPart) && r.request().method() === method, { timeout: 30_000 }),
    locator.click(),
  ]);
  expect(resp.ok(), `${method} ${urlPart} → ${resp.status()} ${await resp.text().catch(() => '')}`).toBeTruthy();
  return resp;
}

/** Fill the list search box and await the debounced list refetch for that keyword. */
async function searchAndWait(page: Page, keyword: string) {
  const [resp] = await Promise.all([
    page.waitForResponse(
      r => r.url().includes('viewguestdelegationlist') && r.url().includes(`keyword=${encodeURIComponent(keyword)}`),
      { timeout: 30_000 },
    ),
    page.getByTestId('visit-search-input').fill(keyword),
  ]);
  return resp;
}

test.describe('Real-stack FULL-DOM: v2 mutation & search workflows', () => {
  test.beforeEach(() => {
    expect(SECRET, 'run secret must be provided by the orchestration').not.toBe('');
  });

  // ── §6 PENDING EDIT — owner edits ONE campus through the real edit form ──────────────────────────────────
  test('§6 pending-edit: owner edits HN via the real form; HCM sibling untouched', async ({ browser, request }) => {
    const tag = `UE${Date.now().toString(36)}`;
    const { requestId } = await createMixedRequest(request, tag, `Doan HN ${tag}`, `Doan HCM ${tag}`);
    const before = await readDetail(request, requestId);
    const hnCode = campusOf(before, CAMPUS_HN).campusCode;
    const hnBefore = campusOf(before, CAMPUS_HN);
    const hcmBefore = campusOf(before, CAMPUS_HCM);
    const newHn = `Doan HN EDITED ${tag}`;

    const { context, page } = await authedPage(browser, 'visitor_owner', OWNER_USER);
    try {
      await page.goto(`/dashboard/visit/v2/${requestId}/edit`);
      const hnCard = page.getByTestId(`campus-edit-card-${hnCode}`);
      await expect(hnCard).toBeVisible({ timeout: 25_000 });
      const delegation = hnCard.getByTestId('campus-delegation-input');
      await expect(delegation).toHaveValue(`Doan HN ${tag}`); // hydrated from the real read model
      await delegation.fill(newHn);

      await clickAndWait(page, page.getByTestId('v2-edit-submit'), `/v2/visit-requests/${requestId}/pending-edit`, 'PUT');
      await expect(page).toHaveURL(new RegExp(`/dashboard/visit/v2/${requestId}$`), { timeout: 20_000 });
    } finally {
      await context.close();
    }

    const after = await readDetail(request, requestId);
    const hnAfter = campusOf(after, CAMPUS_HN);
    const hcmAfter = campusOf(after, CAMPUS_HCM);
    expect(hnAfter.delegationName).toBe(newHn);                     // target campus changed via the DOM
    expect(hcmAfter.delegationName).toBe(`Doan HCM ${tag}`);        // sibling delegation untouched
    expect(hnAfter.rowVersion).toBeGreaterThan(hnBefore.rowVersion);
    expect(hcmAfter.rowVersion).toBe(hcmBefore.rowVersion);         // sibling is a true no-op
    expect(after.requestStatus).toBe('PENDING_APPROVAL');
  });

  // ── §7 RESUBMIT — owner resubmits a fully-rejected request through the real resubmit form ────────────────
  test('§7 resubmit: owner resubmits a rejected request via the real form (campus set fixed, count +1)', async ({ browser, request }) => {
    const tag = `UR${Date.now().toString(36)}`;
    const { requestId, requestCode, instances } = await createMixedRequest(request, tag, `Doan HN ${tag}`, `Doan HCM ${tag}`);
    const hnInstance = instances.find(i => i.campusId === CAMPUS_HN)!.visitInstanceId;
    const hcmInstance = instances.find(i => i.campusId === CAMPUS_HCM)!.visitInstanceId;
    // Precondition: reject BOTH campuses → the request is fully REJECTED.
    await rejectCampus(request, requestId, hnInstance, 'campus_leader_hn');
    await rejectCampus(request, requestId, hcmInstance, 'campus_leader_hcm');
    const before = await readDetail(request, requestId);
    expect(before.requestStatus).toBe('REJECTED');
    const hnCode = campusOf(before, CAMPUS_HN).campusCode;
    const beforeInstanceIds = before.campusVisits.map((c: any) => c.visitInstanceId).sort();

    const { context, page } = await authedPage(browser, 'visitor_owner', OWNER_USER);
    try {
      await page.goto(`/dashboard/visit/v2/${requestId}/resubmit`);
      const hnCard = page.getByTestId(`campus-edit-card-${hnCode}`);
      await expect(hnCard).toBeVisible({ timeout: 25_000 });
      await hnCard.getByTestId('campus-delegation-input').fill(`Doan HN RESUBMIT ${tag}`);
      await clickAndWait(page, page.getByTestId('v2-edit-submit'), `/v2/visit-requests/${requestId}/resubmit`, 'POST');
      await expect(page).toHaveURL(new RegExp(`/dashboard/visit/v2/${requestId}$`), { timeout: 20_000 });
    } finally {
      await context.close();
    }

    const after = await readDetail(request, requestId);
    expect(after.requestStatus).toBe('PENDING_APPROVAL');                 // back to pending
    expect(after.campusVisits.map((c: any) => c.visitInstanceId).sort()).toEqual(beforeInstanceIds); // campus set fixed
    expect(campusOf(after, CAMPUS_HN).delegationName).toBe(`Doan HN RESUBMIT ${tag}`);

    // resubmission_count is exposed on the list DTO (not the detail read model) — verify it incremented.
    const listRes = await request.get(
      `${API_BASE}/delegations/viewguestdelegationlist?tab=responsible&page=1&pageSize=50&keyword=${encodeURIComponent(requestCode)}`,
      { headers: hdr('visitor_owner') });
    const row = ((await listRes.json()).items as any[]).find(i => i.visitRequestId === requestId);
    expect(row, 'owner should see the resubmitted request in their list').toBeTruthy();
    expect(row.resubmissionCount).toBeGreaterThanOrEqual(1);
  });

  // ── §8 SAFE EDIT — owner applies an apply-now change through the real modal (no amendment) ───────────────
  test('§8 safe-edit: owner applies a per-campus safe change immediately; sibling + amendment untouched', async ({ browser, request }) => {
    const tag = `US${Date.now().toString(36)}`;
    const { requestId, instances } = await createMixedRequest(request, tag, `Doan HN ${tag}`, `Doan HCM ${tag}`);
    const hnInstance = instances.find(i => i.campusId === CAMPUS_HN)!.visitInstanceId;
    // Precondition (§8): the HN campus is ASSIGNED (>24h out).
    await approveCampus(request, requestId, hnInstance, 'campus_leader_hn');
    const newTransport = `Xe dua don ${tag}`;

    const { context, page } = await authedPage(browser, 'visitor_owner', OWNER_USER);
    try {
      await page.goto(`/dashboard/visit/v2/${requestId}`);
      await expect(page.getByTestId(`campus-detail-card-${hnInstance}`)).toBeVisible({ timeout: 25_000 });
      await page.getByTestId('safe-edit-open').click();
      const dialog = page.getByRole('dialog');
      await expect(dialog).toBeVisible();
      await dialog.getByTestId(`safe-edit-transportation-${hnInstance}`).fill(newTransport);
      await clickAndWait(page, page.getByTestId('safe-edit-submit'), `/v2/visit-requests/${requestId}/safe-details`, 'PATCH');
      // Apply-now: the modal closes and the detail reloads (never a pending-amendment state).
      await expect(dialog).toBeHidden({ timeout: 15_000 });
    } finally {
      await context.close();
    }

    const after = await readDetail(request, requestId);
    expect(campusOf(after, CAMPUS_HN).transportationNote).toBe(newTransport); // applied immediately
    expect(campusOf(after, CAMPUS_HN).activeAmendment).toBeNull();            // NOT an amendment
    expect(campusOf(after, CAMPUS_HCM).transportationNote).toBeNull();       // sibling untouched
    expect(campusOf(after, CAMPUS_HCM).activeAmendment).toBeNull();
  });

  // ── §9 MEMBER AMENDMENT SUBMIT — owner proposes a member change through the real modal ───────────────────
  test('§9 amendment-submit: owner adds a guest via the real modal; active snapshot stays, HCM untouched', async ({ browser, request }) => {
    const tag = `UA${Date.now().toString(36)}`;
    const { requestId, instances } = await createMixedRequest(request, tag, `Doan HN ${tag}`, `Doan HCM ${tag}`);
    const hnInstance = instances.find(i => i.campusId === CAMPUS_HN)!.visitInstanceId;
    await approveCampus(request, requestId, hnInstance, 'campus_leader_hn'); // → ASSIGNED (amendable)
    const s1 = await readDetail(request, requestId);
    const originalVisitors = campusOf(s1, CAMPUS_HN).visitors.length;
    const originalHcmVisitors = campusOf(s1, CAMPUS_HCM).visitors.length;

    const { context, page } = await authedPage(browser, 'visitor_owner', OWNER_USER);
    try {
      await page.goto(`/dashboard/visit/v2/${requestId}`);
      const hnCard = page.getByTestId(`campus-detail-card-${hnInstance}`);
      await expect(hnCard).toBeVisible({ timeout: 25_000 });
      await hnCard.getByTestId(`amendment-open-${hnInstance}`).click();
      const dialog = page.getByRole('dialog');
      await expect(dialog).toBeVisible();

      // Negative: submit is disabled while the required reason is empty.
      await expect(page.getByTestId('amendment-submit')).toBeDisabled();

      await dialog.getByTestId('amendment-add-visitor').click();
      // A guest row requires ALL fields (backend validator) — fill the freshly-added row completely.
      await dialog.getByTestId('amendment-visitors-fullname').last().fill(`Khach moi ${tag}`);
      await dialog.getByTestId('amendment-visitors-jobtitle').last().fill('Chuyen vien');

      // Organization (OrganizationCombobox, Creatable — free text allowed). Its data-testid lives on the
      // row-scoped WRAPPER div (`amendment-${kind}-organization-${rowKey}`), not the input itself, and the
      // row key is client-generated/unknown to this test — match the wrapper by testid prefix and take the
      // last (newest) row. The `visitors` segment of the testid already disambiguates from the support
      // section's own `amendment-support-organization-*` wrappers, so no extra scoping is needed.
      const orgWrapper = dialog.getByTestId(/^amendment-visitors-organization-/).last();
      const orgInput = orgWrapper.getByRole('combobox');
      await orgInput.click();
      await orgInput.fill('Org E2E');
      // OrganizationCombobox's `onInputChange` already commits free text on every keystroke — 'Org E2E'
      // is in the amendment's React state the instant `.fill()` fires. Its async search never offers a
      // matching/"create new" option for a genuinely novel string (proven live: the menu settles on the
      // plain "no options" placeholder, not a create row), so the value is committed by typing alone —
      // the visible control just needs to CLOSE afterward, which moving focus to the next field does via
      // OrganizationCombobox's own onBlur (re-commits the trimmed text, closes the menu).

      // Nationality (CountrySelect, strict — no free text, no testid at all). Only its accessible name
      // (aria-label = "<section> — <field>") locates it, and the support section renders the SAME field
      // label — matching the FULL "<visitors section> — Nationality" name (both languages) disambiguates
      // without needing any fieldset scoping. react-select's open menu is portaled to document.body, so
      // the option is NOT a descendant of the dialog and must be looked up page-wide.
      const nationalityInput = dialog.getByRole('combobox', {
        name: /danh sách khách.*quốc tịch|guest list.*nationality/i,
      }).last();
      await nationalityInput.click(); // also blurs + closes the Organization field above
      await expect(orgWrapper).toContainText('Org E2E'); // organization value really committed, not just typed
      await nationalityInput.fill('Việt Nam');
      const vnOption = page.getByRole('option', { name: 'Việt Nam', exact: true });
      await expect(vnOption).toBeVisible();
      await vnOption.click();
      // The pre-seeded HN visitor's own nationality ("VN") never resolves to a real option and renders
      // as the raw fallback text "VN" — only a genuinely committed pick renders the full label "Việt Nam".
      await expect(dialog).toContainText('Việt Nam');

      await dialog.getByTestId('amendment-reason').fill('Bo sung mot khach');
      await expect(page.getByTestId('amendment-submit')).toBeEnabled();
      await clickAndWait(page, page.getByTestId('amendment-submit'), `/v2/visit-requests/${requestId}/instances/${hnInstance}/amendments`, 'POST');
      await expect(dialog).toBeHidden({ timeout: 15_000 });
      // A second pending amendment must not be offer-able: the submit affordance is gone after one pending.
      await expect(hnCard.getByTestId(`amendment-open-${hnInstance}`)).toHaveCount(0);
    } finally {
      await context.close();
    }

    const s2 = await readDetail(request, requestId);
    // BEFORE approval the ACTIVE snapshot is unchanged; only a PENDING amendment is recorded.
    expect(campusOf(s2, CAMPUS_HN).visitors.length).toBe(originalVisitors);
    expect(campusOf(s2, CAMPUS_HN).activeAmendment?.status).toBe('PENDING_APPROVAL');
    expect(campusOf(s2, CAMPUS_HCM).visitors.length).toBe(originalHcmVisitors); // sibling untouched
    expect(campusOf(s2, CAMPUS_HCM).activeAmendment).toBeNull();
  });

  // ── §10a LEADER APPROVE — the current campus leader approves the amendment through the real panel ────────
  test('§10 leader-approve: HN leader approves the amendment via the real panel; applied target-only', async ({ browser, request }) => {
    const tag = `UP${Date.now().toString(36)}`;
    const { requestId, instances } = await createMixedRequest(request, tag, `Doan HN ${tag}`, `Doan HCM ${tag}`);
    const hnInstance = instances.find(i => i.campusId === CAMPUS_HN)!.visitInstanceId;
    await approveCampus(request, requestId, hnInstance, 'campus_leader_hn');
    const s1 = await readDetail(request, requestId);
    const originalVisitors = campusOf(s1, CAMPUS_HN).visitors.length;
    // Precondition: a pending amendment exists (owner-submitted via API — the SUBMIT is not the action here).
    const amendmentId = await submitAmendmentApi(request, requestId, hnInstance, campusOf(s1, CAMPUS_HN), `Khach duyet ${tag}`);

    const leader = await meUser(request, 'campus_leader_hn');
    const { context, page } = await authedPage(browser, 'campus_leader_hn', leader);
    try {
      await page.goto(`/dashboard/visit/v2/${requestId}`);
      const panel = page.getByTestId(`amendment-panel-${hnInstance}`);
      await expect(panel).toBeVisible({ timeout: 25_000 });
      // The proposed old→new diff is shown (proposal is never presented as already-applied).
      await expect(panel.getByRole('table')).toBeVisible();
      const approveBtn = page.getByTestId(`amendment-approve-${amendmentId}`);
      await expect(approveBtn).toBeVisible();
      await clickAndWait(page, approveBtn, `/v2/visit-instances/${hnInstance}/amendments/${amendmentId}/approve`, 'POST');
      await expect(panel).toHaveCount(0, { timeout: 15_000 }); // approved → active amendment cleared, panel gone
    } finally {
      await context.close();
    }

    const s2 = await readDetail(request, requestId);
    expect(campusOf(s2, CAMPUS_HN).visitors.length).toBe(originalVisitors + 1);         // proposal applied
    expect(campusOf(s2, CAMPUS_HN).visitors.some((v: any) => v.fullName === `Khach duyet ${tag}`)).toBe(true);
    expect(campusOf(s2, CAMPUS_HN).activeAmendment).toBeNull();
    expect(campusOf(s2, CAMPUS_HCM).delegationName).toBe(`Doan HCM ${tag}`);            // sibling untouched
  });

  // ── §10b LEADER REJECT — separate fixture; reject requires a reason ──────────────────────────────────────
  test('§10 leader-reject: HN leader rejects the amendment via the real panel; active snapshot unchanged', async ({ browser, request }) => {
    const tag = `UJ${Date.now().toString(36)}`;
    const { requestId, instances } = await createMixedRequest(request, tag, `Doan HN ${tag}`, `Doan HCM ${tag}`);
    const hnInstance = instances.find(i => i.campusId === CAMPUS_HN)!.visitInstanceId;
    await approveCampus(request, requestId, hnInstance, 'campus_leader_hn');
    const s1 = await readDetail(request, requestId);
    const originalVisitors = campusOf(s1, CAMPUS_HN).visitors.length;
    const amendmentId = await submitAmendmentApi(request, requestId, hnInstance, campusOf(s1, CAMPUS_HN), `Khach tuchoi ${tag}`);

    const leader = await meUser(request, 'campus_leader_hn');
    const { context, page } = await authedPage(browser, 'campus_leader_hn', leader);
    try {
      await page.goto(`/dashboard/visit/v2/${requestId}`);
      const panel = page.getByTestId(`amendment-panel-${hnInstance}`);
      await expect(panel).toBeVisible({ timeout: 25_000 });
      await page.getByTestId(`amendment-reject-${amendmentId}`).click();
      const confirm = page.getByTestId(`amendment-reject-confirm-${amendmentId}`);
      await expect(confirm).toBeDisabled(); // reason required
      await page.locator('#amendment-reject-note').fill('Khong phu hop lich');
      await expect(confirm).toBeEnabled();
      await clickAndWait(page, confirm, `/v2/visit-instances/${hnInstance}/amendments/${amendmentId}/reject`, 'POST');
      await expect(panel).toHaveCount(0, { timeout: 15_000 });
    } finally {
      await context.close();
    }

    const s2 = await readDetail(request, requestId);
    expect(campusOf(s2, CAMPUS_HN).visitors.length).toBe(originalVisitors);   // rejected → snapshot NOT applied
    expect(campusOf(s2, CAMPUS_HN).activeAmendment).toBeNull();
    expect(campusOf(s2, CAMPUS_HCM).delegationName).toBe(`Doan HCM ${tag}`);  // sibling untouched
  });

  // ── §11 WRONG-CAMPUS — the HCM leader never sees the HN action; the host also refuses it ─────────────────
  test('§11 wrong-campus: HCM leader sees no HN action in the UI, and the host denies it (403)', async ({ browser, request }) => {
    const tag = `UW${Date.now().toString(36)}`;
    const { requestId, instances } = await createMixedRequest(request, tag, `Doan HN ${tag}`, `Doan HCM ${tag}`);
    const hnInstance = instances.find(i => i.campusId === CAMPUS_HN)!.visitInstanceId;
    const hcmInstance = instances.find(i => i.campusId === CAMPUS_HCM)!.visitInstanceId;
    await approveCampus(request, requestId, hnInstance, 'campus_leader_hn');
    const s1 = await readDetail(request, requestId);
    const amendmentId = await submitAmendmentApi(request, requestId, hnInstance, campusOf(s1, CAMPUS_HN), `Khach wc ${tag}`);

    // UI: the HCM leader is scoped to the HCM campus only — the HN card (and its approve action) never render.
    const leader = await meUser(request, 'campus_leader_hcm');
    const { context, page } = await authedPage(browser, 'campus_leader_hcm', leader);
    try {
      await page.goto(`/dashboard/visit/v2/${requestId}`);
      await expect(page.getByTestId(`campus-detail-card-${hcmInstance}`)).toBeVisible({ timeout: 25_000 }); // own campus visible
      await expect(page.getByTestId(`campus-detail-card-${hnInstance}`)).toHaveCount(0);   // sibling campus hidden
      await expect(page.getByTestId(`amendment-approve-${amendmentId}`)).toHaveCount(0);   // no cross-campus action
    } finally {
      await context.close();
    }

    // Defense-in-depth: the host refuses the HCM leader's cross-campus approve, and lets the HN leader through the gate.
    const wrong = await request.post(`${API_BASE}/v2/visit-instances/${hnInstance}/amendments/${amendmentId}/approve`, { headers: hdr('campus_leader_hcm'), data: { note: 'nope' } });
    expect(wrong.status()).toBe(403);
    const right = await request.post(`${API_BASE}/v2/visit-instances/${hnInstance}/amendments/${amendmentId}/approve`, { headers: hdr('campus_leader_hn'), data: { note: 'ok' } });
    expect(right.ok(), `HN leader should be authorized to approve its own amendment: ${right.status()}`).toBeTruthy();
  });

  // ── §12 WITHDRAW — the requester withdraws their pending amendment through the real panel ────────────────
  test('§12 withdraw: owner withdraws the pending amendment via the real panel; snapshot intact, re-proposable', async ({ browser, request }) => {
    const tag = `UD${Date.now().toString(36)}`;
    const { requestId, instances } = await createMixedRequest(request, tag, `Doan HN ${tag}`, `Doan HCM ${tag}`);
    const hnInstance = instances.find(i => i.campusId === CAMPUS_HN)!.visitInstanceId;
    await approveCampus(request, requestId, hnInstance, 'campus_leader_hn');
    const s1 = await readDetail(request, requestId);
    const originalVisitors = campusOf(s1, CAMPUS_HN).visitors.length;
    const amendmentId = await submitAmendmentApi(request, requestId, hnInstance, campusOf(s1, CAMPUS_HN), `Khach rut ${tag}`);

    const { context, page } = await authedPage(browser, 'visitor_owner', OWNER_USER);
    try {
      await page.goto(`/dashboard/visit/v2/${requestId}`);
      const hnCard = page.getByTestId(`campus-detail-card-${hnInstance}`);
      await expect(hnCard).toBeVisible({ timeout: 25_000 });
      const withdrawBtn = page.getByTestId(`amendment-withdraw-${amendmentId}`);
      await expect(withdrawBtn).toBeVisible();
      await clickAndWait(page, withdrawBtn, `/instances/${hnInstance}/amendments/${amendmentId}/withdraw`, 'POST');
      await expect(page.getByTestId(`amendment-panel-${hnInstance}`)).toHaveCount(0, { timeout: 15_000 });
      // Withdrawn → the requester may propose again (the submit affordance returns).
      await expect(hnCard.getByTestId(`amendment-open-${hnInstance}`)).toBeVisible({ timeout: 15_000 });
    } finally {
      await context.close();
    }

    const s2 = await readDetail(request, requestId);
    expect(campusOf(s2, CAMPUS_HN).activeAmendment).toBeNull();             // withdrawn
    expect(campusOf(s2, CAMPUS_HN).visitors.length).toBe(originalVisitors); // snapshot never moved
    expect(campusOf(s2, CAMPUS_HCM).delegationName).toBe(`Doan HCM ${tag}`);
  });

  // ── §13 SEARCH — hidden-campus isolation + scoped match contexts, driven through the real list search ────
  test('§13 search: hidden-campus keyword never surfaces for a scoped leader; contexts stay authorized', async ({ browser, request }) => {
    const tag = `USR${Date.now().toString(36)}`;
    const alpha = `AlphaKW${tag}`; // HN-only delegation keyword
    const beta = `BetaKW${tag}`;   // HCM-only delegation keyword (hidden from the HN leader)
    const { requestId } = await createMixedRequest(request, tag, `Doan ${alpha}`, `Doan ${beta}`);
    const detail = await readDetail(request, requestId);
    const hnCampusName = campusOf(detail, CAMPUS_HN).campusName;
    const hcmCampusName = campusOf(detail, CAMPUS_HCM).campusName;

    // ── HN Staff Leader ──
    const hnLeader = await meUser(request, 'campus_leader_hn');
    const l = await authedPage(browser, 'campus_leader_hn', hnLeader);
    try {
      await Promise.all([
        l.page.waitForResponse(r => r.url().includes('viewguestdelegationlist'), { timeout: 30_000 }),
        l.page.goto('/dashboard/visit?tab=responsible'),
      ]);

      // Hidden HCM keyword must NOT surface the request for the HN-scoped leader (no row, no leak).
      await searchAndWait(l.page, beta);
      await expect(l.page.getByText(`Doan ${alpha}`)).toHaveCount(0);
      await expect(l.page.getByText(beta)).toHaveCount(0);

      // The HN keyword surfaces the request with a matched-context scoped to HN only.
      await searchAndWait(l.page, alpha);
      await expect(l.page.getByText(`Doan ${alpha}`).first()).toBeVisible({ timeout: 15_000 });
      await expect(l.page.getByRole('list', { name: /Khớp tại|Matched at/i }).first()).toBeVisible();
      await expect(l.page.getByText(beta)).toHaveCount(0);              // no hidden-campus content leaked
      await expect(l.page.getByText(hcmCampusName)).toHaveCount(0);     // hidden campus never named
      expect(hnCampusName).toBeTruthy();
    } finally {
      await l.context.close();
    }

    // ── Owner sees ALL their campuses → the HCM keyword surfaces the request for them ──
    const o = await authedPage(browser, 'visitor_owner', OWNER_USER);
    try {
      await Promise.all([
        o.page.waitForResponse(r => r.url().includes('viewguestdelegationlist'), { timeout: 30_000 }),
        o.page.goto('/dashboard/visit?tab=responsible'),
      ]);
      await searchAndWait(o.page, beta);
      // The owner's request-level row labels a mixed request explicitly; the row is present under the HCM keyword.
      const listRes = await request.get(
        `${API_BASE}/delegations/viewguestdelegationlist?tab=responsible&page=1&pageSize=50&keyword=${encodeURIComponent(beta)}`,
        { headers: hdr('visitor_owner') });
      const row = ((await listRes.json()).items as any[]).find(i => i.visitRequestId === requestId);
      expect(row, 'owner should see the request under any of their campus keywords').toBeTruthy();
      const ownerCampusCtxs = (row.matchedContexts ?? []).filter((c: any) => c.scope === 'CAMPUS');
      expect(ownerCampusCtxs.some((c: any) => c.campusId === CAMPUS_HCM)).toBe(true);
    } finally {
      await o.context.close();
    }
  });
});
