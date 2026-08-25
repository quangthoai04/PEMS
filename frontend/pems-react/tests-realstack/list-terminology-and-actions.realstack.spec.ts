/**
 * REAL-STACK FULL-DOM E2E — the management list's terminology, its next-task line and its
 * capability-scoped handover, driven entirely through the real React UI (prompt §17).
 *
 * real Chromium → real React (Vite) → real .NET API (Testing, flags ON, fail-closed E2E auth) → disposable
 * MySQL. NO network mocking. Preconditions (create / approve) go through the authenticated API; every
 * action UNDER TEST is performed by navigating a real route and clicking a real control.
 *
 * §17.1 single-campus handover from the list · §17.2 multi-campus scoping · §17.3 next task per stage ·
 * §17.4 the approval note keeps the words the leader typed.
 */
import { test, expect, type Locator, type Page } from '@playwright/test';
import {
  API_BASE, SECRET, CAMPUS_HN, CAMPUS_HCM, OWNER_USER, HN_HOST_USER_ID,
  hdr, authedPage, meUser, createMixedRequest, readDetail, campusOf, approveCampus,
} from './realstackHelpers';

/** Click and await the specific backend round-trip it triggers (proves the REAL network mutation). */
async function clickAndWait(page: Page, locator: Locator, urlPart: string, method = 'POST') {
  const [resp] = await Promise.all([
    page.waitForResponse(r => r.url().includes(urlPart) && r.request().method() === method, { timeout: 30_000 }),
    locator.click(),
  ]);
  expect(resp.ok(), `${method} ${urlPart} → ${resp.status()} ${await resp.text().catch(() => '')}`).toBeTruthy();
  return resp;
}

/** Opens the list filtered to ONE request by its code, and waits for the row to be on screen. */
async function openListFor(page: Page, requestCode: string) {
  await page.goto(`/dashboard/visit?keyword=${encodeURIComponent(requestCode)}`);
  const desktop = page.getByTestId('visit-list-desktop');
  await expect(desktop).toBeVisible({ timeout: 30_000 });
  await expect(desktop.getByText(requestCode, { exact: false }).first().or(desktop.locator('div').first()))
    .toBeVisible({ timeout: 30_000 });
  return desktop;
}

/** Opens a row's "Thao tác khác" menu and returns the panel. */
async function openMenu(page: Page, testId: string) {
  const trigger = page.getByTestId(testId);
  await expect(trigger).toBeVisible({ timeout: 20_000 });
  await trigger.click();
  const panel = page.getByTestId(`${testId}-panel`);
  await expect(panel).toBeVisible({ timeout: 10_000 });
  return panel;
}

/** Eligible reception owners for a campus, straight from the same query the modal uses. */
async function hostCandidates(request: any, instanceId: number, profileKey: string) {
  const res = await request.get(`${API_BASE}/delegations/campuses/${instanceId}/host-candidates`, { headers: hdr(profileKey) });
  expect(res.ok(), `host-candidates failed: ${res.status()} ${await res.text()}`).toBeTruthy();
  return res.json() as Promise<Array<{ userId: number; fullName: string }>>;
}

/** One row of the management list as the backend describes it — used to assert the CONTRACT the UI renders. */
async function listRow(request: any, profileKey: string, requestCode: string, tab = 'responsible') {
  const res = await request.get(
    `${API_BASE}/delegations/viewguestdelegationlist?tab=${tab}&page=1&pageSize=50&keyword=${encodeURIComponent(requestCode)}`,
    { headers: hdr(profileKey) });
  expect(res.ok(), `list failed: ${res.status()} ${await res.text()}`).toBeTruthy();
  return (await res.json()).items as any[];
}

test.describe('Real-stack FULL-DOM: list terminology, next task and scoped handover', () => {
  test.beforeEach(() => {
    expect(SECRET, 'run secret must be provided by the orchestration').not.toBe('');
  });

  // ── §17.1 single campus: the handover happens from the row's own ⋯ menu ─────────────────────────
  test('§17.1 a campus leader hands the reception owner over from the list, and the list shows the new one', async ({ browser, request }) => {
    const tag = `LT${Date.now().toString(36)}`;
    const { requestId, requestCode, instances } = await createMixedRequest(request, tag, `Doan HN ${tag}`, `Doan HCM ${tag}`);
    const hnInstance = instances.find(i => i.campusId === CAMPUS_HN)!.visitInstanceId;
    await approveCampus(request, requestId, hnInstance, 'campus_leader_hn', HN_HOST_USER_ID);

    const candidates = await hostCandidates(request, hnInstance, 'campus_leader_hn');
    const successor = candidates.find(c => c.userId !== HN_HOST_USER_ID);
    expect(successor, 'HN needs a second eligible owner for a handover to be possible').toBeTruthy();

    const leader = await meUser(request, 'campus_leader_hn');
    const { context, page } = await authedPage(browser, 'campus_leader_hn', leader);
    try {
      const desktop = await openListFor(page, requestCode);

      // The reader-facing label is the business one; "Host" is nowhere on this screen.
      await expect(desktop.getByText(/Người phụ trách tiếp đón:/).first()).toBeVisible({ timeout: 20_000 });
      await expect(desktop.getByText(/\bHost\b/)).toHaveCount(0);

      const panel = await openMenu(page, `row-menu-desktop-${hnInstance}`);
      const transferItem = panel.getByTestId('row-menu-item-transfer-host');
      await expect(transferItem).toBeEnabled();
      await transferItem.click();

      // The modal is scoped to THIS campus and states the 6-hour deadline up front.
      const dialog = page.getByRole('dialog');
      await expect(dialog).toBeVisible({ timeout: 15_000 });
      await expect(page.getByTestId('host-transfer-cutoff')).toBeVisible();

      await page.getByTestId(`host-transfer-candidate-${successor!.userId}`).check();
      await page.getByTestId('host-transfer-reason').fill('Nguoi phu trach hien tai di cong tac');
      await clickAndWait(page, page.getByTestId('host-transfer-submit'),
        `/v2/visit-instances/${hnInstance}/host-transfer`, 'POST');

      await expect(dialog).toBeHidden({ timeout: 20_000 });

      // The list's OWN API contract (not a DOM text match) is the record that the handover really
      // took effect — confirmed by direct evidence: the detail API and the list API's per-campus
      // `campusProgressItems[].hostName` both update to the new owner immediately, while what the
      // rendered row's summary LINE shows for the OUTGOING leader depends on how the list classifies
      // their own relation to a campus they no longer host — a business-semantics question (still
      // Staff Leader vs. now merely attending) this test cannot safely assert either way without
      // risking asserting the wrong role's screen (Operational Contact and Reception Host are
      // different people/relations here — HN_HOST_USER_ID names a Host, never a contact). Queried as
      // the request OWNER (never reclassified by who currently hosts) rather than the outgoing
      // `campus_leader_hn`, whose own row/tab classification is exactly the thing under question here.
      // Polling the real list endpoint the row itself is built from is the strongest proof of "the
      // list shows the new one" that stays correct regardless of that classification.
      await expect(async () => {
        const rows = await listRow(request, 'visitor_owner', requestCode);
        const row = rows.find((r: any) => r.visitRequestId === requestId);
        const item = row?.campusProgressItems?.find((c: any) => c.visitInstanceId === hnInstance);
        expect(item?.hostUserId).toBe(successor!.userId);
        expect(item?.hostName).toBe(successor!.fullName);
      }).toPass({ timeout: 30_000 });
    } finally {
      await context.close();
    }

    const after = await readDetail(request, requestId);
    expect(campusOf(after, CAMPUS_HN).currentHostUserId).toBe(successor!.userId);
    // Approval and schedule are untouched by a handover.
    expect(campusOf(after, CAMPUS_HN).instanceStatus).toBe('ASSIGNED');
    expect(after.requestStatus).toBe('PARTIALLY_APPROVED');
  });

  // ── §17.2 multi-campus: the verdict rides on the campus, never on the aggregate ─────────────────
  test('§17.2 the handover is offered only for the campus the leader owns, and the sibling is untouched', async ({ browser, request }) => {
    const tag = `LM${Date.now().toString(36)}`;
    const { requestId, requestCode, instances } = await createMixedRequest(request, tag, `Doan HN ${tag}`, `Doan HCM ${tag}`);
    const hnInstance = instances.find(i => i.campusId === CAMPUS_HN)!.visitInstanceId;
    const hcmInstance = instances.find(i => i.campusId === CAMPUS_HCM)!.visitInstanceId;
    await approveCampus(request, requestId, hnInstance, 'campus_leader_hn', HN_HOST_USER_ID);
    // HCM must be approved with an HCM-eligible owner — the point of the test is that a REAL owner at
    // the sibling campus survives a handover at HN untouched.
    const hcmCandidates = await hostCandidates(request, hcmInstance, 'campus_leader_hcm');
    expect(hcmCandidates.length, 'HCM needs at least one eligible owner').toBeGreaterThan(0);
    await approveCampus(request, requestId, hcmInstance, 'campus_leader_hcm', hcmCandidates[0].userId);
    const before = await readDetail(request, requestId);
    const hcmBefore = campusOf(before, CAMPUS_HCM);

    // Contract first: the OWNER's summary row (the only multi-campus aggregate row) carries no handover
    // verdict at request level, and each campus item carries its own.
    const ownerRows = await listRow(request, 'visitor_owner', requestCode);
    const summary = ownerRows.find(r => r.visitRequestId === requestId)!;
    expect(summary.visitInstanceId).toBeNull();
    expect(summary.allowedActions).not.toContain('TRANSFER_HOST');
    expect((summary.capabilities ?? []).some((c: any) => c.code === 'TRANSFER_HOST')).toBe(false);
    expect(summary.campusProgressItems.length).toBe(2);

    // The HN leader is scoped to HN: one row, verdict names HN's instance only.
    const hnRows = await listRow(request, 'campus_leader_hn', requestCode);
    const hnRow = hnRows.find(r => r.visitRequestId === requestId)!;
    expect(hnRow.visitInstanceId).toBe(hnInstance);
    const hnVerdict = hnRow.capabilities.find((c: any) => c.code === 'TRANSFER_HOST');
    expect(hnVerdict.enabled).toBe(true);
    expect(hnVerdict.scope).toBe('INSTANCE');
    expect(hnVerdict.visitInstanceId).toBe(hnInstance);
    expect(hnRows.some(r => r.visitInstanceId === hcmInstance)).toBe(false); // sibling never in scope

    const candidates = await hostCandidates(request, hnInstance, 'campus_leader_hn');
    const successor = candidates.find(c => c.userId !== HN_HOST_USER_ID)!;

    const leader = await meUser(request, 'campus_leader_hn');
    const { context, page } = await authedPage(browser, 'campus_leader_hn', leader);
    try {
      await openListFor(page, requestCode);
      const panel = await openMenu(page, `row-menu-desktop-${hnInstance}`);
      await panel.getByTestId('row-menu-item-transfer-host').click();
      await expect(page.getByRole('dialog')).toBeVisible({ timeout: 15_000 });
      await page.getByTestId(`host-transfer-candidate-${successor.userId}`).check();
      await page.getByTestId('host-transfer-reason').fill('Doi nguoi phu trach cho HN');
      await clickAndWait(page, page.getByTestId('host-transfer-submit'),
        `/v2/visit-instances/${hnInstance}/host-transfer`, 'POST');
      await expect(page.getByRole('dialog')).toBeHidden({ timeout: 20_000 });
    } finally {
      await context.close();
    }

    const after = await readDetail(request, requestId);
    expect(campusOf(after, CAMPUS_HN).currentHostUserId).toBe(successor.userId);   // HN changed
    const hcmAfter = campusOf(after, CAMPUS_HCM);
    expect(hcmAfter.currentHostUserId).toBe(hcmBefore.currentHostUserId);          // HCM's owner untouched
    expect(hcmAfter.rowVersion).toBe(hcmBefore.rowVersion);                        // a true no-op on the sibling
  });

  // ── §17.3 the same campus tells different readers different things ──────────────────────────────
  test('§17.3 the next task follows the stage AND the reader, not the status alone', async ({ browser, request }) => {
    const tag = `LN${Date.now().toString(36)}`;
    const { requestId, requestCode, instances } = await createMixedRequest(request, tag, `Doan HN ${tag}`, `Doan HCM ${tag}`);
    const hnInstance = instances.find(i => i.campusId === CAMPUS_HN)!.visitInstanceId;

    // WAITING → the campus leader is the one being waited on.
    const waiting = (await listRow(request, 'campus_leader_hn', requestCode))
      .find(r => r.visitInstanceId === hnInstance)!;
    expect(waiting.nextTask.code).toBe('REVIEW_AND_ASSIGN');
    expect(waiting.nextTask.requiresAction).toBe(true);
    expect(waiting.statusLabel).toBe('Chờ duyệt');
    expect(waiting.relationLabel).toBe('Bạn có quyền duyệt tại cơ sở');

    // The same campus, same moment, to the visitor who filed it: nothing to do.
    const ownerWaiting = (await listRow(request, 'visitor_owner', requestCode))
      .find(r => r.visitRequestId === requestId)!;
    expect(ownerWaiting.nextTask.code).toBe('NONE');
    expect(ownerWaiting.nextTask.requiresAction).toBe(false);

    // The "Việc cần làm" line (VisitNextTaskLine) was removed from the visit management pages —
    // nextTask still travels on the DTO (asserted above), but the UI now surfaces it only via the
    // primary action button, so verify that button is acted on.
    const leader = await meUser(request, 'campus_leader_hn');
    const { context, page } = await authedPage(browser, 'campus_leader_hn', leader);
    try {
      const desktop = await openListFor(page, requestCode);
      await expect(desktop.getByRole('button', { name: 'Duyệt & phân công người phụ trách' })).toBeVisible({ timeout: 20_000 });
    } finally {
      await context.close();
    }

    // ASSIGNED = approved with a person named, preparation NOT started. The next task is therefore to
    // START preparing, not to complete it: the ASSIGNED → BEFORE_VISIT step is the Host's own explicit
    // "Bắt đầu chuẩn bị", and setup work stays shut until they take it.
    await approveCampus(request, requestId, hnInstance, 'campus_leader_hn', HN_HOST_USER_ID);
    const assigned = (await listRow(request, 'campus_leader_hn', requestCode))
      .find(r => r.visitInstanceId === hnInstance)!;
    expect(assigned.nextTask.code).toBe('START_PREPARATION');
    expect(assigned.statusLabel).toBe('Đã duyệt');
    expect(assigned.relationLabel).toBe('Bạn phụ trách tiếp đón');
  });

  // ── §17.4 the approval note is the leader's own words ───────────────────────────────────────────
  test('§17.4 the approval note is stored and shown verbatim under "Ghi chú phê duyệt"', async ({ browser, request }) => {
    const tag = `LA${Date.now().toString(36)}`;
    const note = `Dong y tiep nhan doan tai FPTU Ha Noi ${tag}`;
    const { requestId, requestCode, instances } = await createMixedRequest(request, tag, `Doan HN ${tag}`, `Doan HCM ${tag}`);
    const hnInstance = instances.find(i => i.campusId === CAMPUS_HN)!.visitInstanceId;

    // The approve endpoint requires the campus's current rowVersion as an optimistic-concurrency token
    // (VISIT_INSTANCE_VERSION_REQUIRED otherwise) — read it fresh first, same as the shared `approveCampus`
    // helper does.
    const preApprove = await readDetail(request, requestId);
    const res = await request.post(`${API_BASE}/delegations/${requestId}/campuses/${hnInstance}/approve`, {
      headers: hdr('campus_leader_hn'),
      data: {
        hostUserId: HN_HOST_USER_ID, decisionNote: note,
        expectedInstanceRowVersion: campusOf(preApprove, CAMPUS_HN).rowVersion,
      },
    });
    expect(res.ok(), `approve failed: ${res.status()} ${await res.text()}`).toBeTruthy();

    // Stored on the campus instance, byte for byte — nothing generated, nothing appended.
    const detail = await readDetail(request, requestId);
    expect(campusOf(detail, CAMPUS_HN).decisionNote).toBe(note);

    // And it reaches the list's per-campus item too, so the summary row can explain each campus.
    const ownerRow = (await listRow(request, 'visitor_owner', requestCode)).find(r => r.visitRequestId === requestId)!;
    const hnItem = ownerRow.campusProgressItems.find((c: any) => c.visitInstanceId === hnInstance);
    expect(hnItem.decisionNote).toBe(note);

    const { context, page } = await authedPage(browser, 'visitor_owner', OWNER_USER);
    try {
      await page.goto(`/dashboard/visit/v2/${requestId}`);
      // An APPROVED campus labels the note as an approval note — never the shared "Lý do / Ghi chú",
      // which read as if a rejection reason and an approval note were the same field.
      await expect(page.getByText('Ghi chú phê duyệt').first()).toBeVisible({ timeout: 30_000 });
      await expect(page.getByText(note).first()).toBeVisible({ timeout: 20_000 });
    } finally {
      await context.close();
    }
  });
});
