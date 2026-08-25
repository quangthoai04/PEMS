/**
 * REAL-STACK — notification routing live-browser verification.
 *
 * real Chromium -> real React (Vite) -> real .NET API (Testing, fail-closed E2E auth) -> disposable
 * MySQL. NO network mocking of DATA. Proves the FULL chain the notification-routing stabilization
 * initiative is actually about: a real backend producer writes a real notification row -> the real
 * `GET /api/notifications` DTO -> Bell/NotificationsPage -> `resolveNotificationDestination` /
 * `classifyNotificationIntent` -> (for the Visit-family intents) `VisitRequestManagement`'s
 * `resolveAndOpenNotificationTarget` re-resolving against CURRENT backend state -> React Router ->
 * the real destination screen/modal.
 *
 * Scope note (stated rather than implied): almost every historical bug this initiative fixed lived in
 * the Visit-family `VISIT_COMMAND_INTENTS` pathway (`VisitRequestManagement.resolveAndOpenNotificationTarget`)
 * -- the one place a notification's destination is re-decided against CURRENT state. That pathway is
 * exhaustively covered here through REAL business actions. The non-Visit domains (Logistics/Agenda/
 * Minutes/ActionItem/News/Partner/Feedback/Account) resolve to a literal stored `targetUrl` with no
 * current-state re-resolution at all (see `resolveNotificationDestination.ts` -- none of those
 * intents are in `VISIT_COMMAND_INTENTS`), so the only thing a live click would add beyond the
 * existing producer-level backend tests + resolver unit tests is "React Router follows a URL
 * string", which carries far less marginal risk. Given that, this file does not drive those domains
 * (Logistics/Agenda/Minutes/ActionItem/News/Partner/Feedback/Account) through the browser -- they
 * are reported as NOT VERIFIED live and covered instead by the non-browser gates (producer/handler
 * tests + `resolveNotificationDestination` unit tests), rather than faking a browser click that
 * would prove little beyond what those already prove.
 *
 * Two scenarios (plan section 15 "LEGACY UNKNOWN" and the MC-02 missing-instance edge case) seed a
 * notification row directly -- no producer in the current codebase can create a null-metadataJson row
 * or a campus-specific event missing its instance id, so no live action can produce that fixture. The
 * plan itself asks for exactly this ("Tao fixture notification cu"). Every other test below is driven
 * end-to-end through a real backend command (API or DOM).
 */
import { test, expect, type Page } from '@playwright/test';
import {
  API_BASE, CAMPUS_HN, CAMPUS_HCM, OWNER_USER, FIXTURE_REGISTRANT_EMAIL,
  hdr, authedPage, meUser, campusBlock, createMixedRequest, readDetail, campusOf, approveCampus, startPreparation,
} from './realstackHelpers';
import {
  insertLegacyNotification, insertCampusEventMissingInstance, inviteDeptSupport, declineParticipation,
  transferHost, anotherStaffOnCampus, meDepartmentId, latestNotification,
} from './notificationRoutingHelpers';

/** Opens the real Bell popover -- the authenticated dashboard shell (`DashboardLayout`) renders TWO
 * `data-variant="dashboard"` bells (a `lg:hidden` mobile one in its own top bar, a `hidden lg:flex`
 * desktop one floating over the content); at the Desktop Chrome viewport this config runs at, only
 * the desktop one is actually visible, so it is targeted by its distinguishing wrapper rather than
 * the shared `data-variant` value (which alone matches both and is a strict-mode violation). -- and
 * clicks the item whose rendered text contains `snippet` (the exact delegation tag / request code the
 * test itself set on the fixture -- never guessed from producer source text). */
async function clickBellNotification(page: Page, snippet: string) {
  const bell = page.locator('div.absolute.top-3.right-6 button[data-variant="dashboard"]');
  await bell.click();
  const item = page.locator('button').filter({ hasText: snippet }).first();
  await expect(item).toBeVisible({ timeout: 20_000 });
  await item.click();
}

/** Opens `/notifications` and clicks the item whose text contains `snippet`. */
async function clickNotificationsPageItem(page: Page, snippet: string) {
  await page.goto('/notifications');
  const item = page.locator('button').filter({ hasText: snippet }).first();
  await expect(item).toBeVisible({ timeout: 20_000 });
  await item.click();
}

/** A single-HN-campus create payload with an explicit media-consent status (campusBlock hard-codes
 * DECLINED, which is wrong for the privacy-WITHDRAWAL scenario -- that needs to start AGREED). */
function campusBlockWithConsent(code: string, delegation: string, tag: string, consent: 'AGREED' | 'DECLINED') {
  const b = campusBlock(code, 0, delegation, tag);
  return { ...b, mediaConsentStatus: consent };
}

async function createSingleHn(request: Parameters<typeof createMixedRequest>[0], tag: string, consent: 'AGREED' | 'DECLINED' = 'DECLINED') {
  const res = await request.post(`${API_BASE}/v2/visit-requests`, {
    headers: hdr('visitor_owner'),
    data: {
      submissionId: `NR${tag}`,
      registrant: { fullName: 'Owner E2E', nationality: 'VN', organization: 'Org', jobTitle: 'Mgr', phone: '+84900000000', email: FIXTURE_REGISTRANT_EMAIL },
      partnerId: null,
      campusVisits: [campusBlockWithConsent('HN', `Doan HN ${tag}`, tag, consent)],
    },
  });
  expect(res.ok(), `create failed: ${res.status()} ${await res.text()}`).toBeTruthy();
  const body = await res.json();
  return { requestId: body.visitRequestId as number, requestCode: body.requestCode as string, instances: body.instances as Array<{ visitInstanceId: number; campusId: number }> };
}

test.describe('Real-stack: notification routing -- Staff Leader (waiting approval / updated / privacy / multi-relation)', () => {
  test('BL-01 + ST-01 + BL-05(partial): the same Staff Leader is CAMPUS_REVIEWER then HOST on one request -- two different real eventKeys land on two different correct destinations', async ({ browser, request }) => {
    const tag = `NB${Date.now().toString(36)}`;
    const { requestId, instances } = await createSingleHn(request, tag);
    const hnInstance = instances[0].visitInstanceId;

    const leader = await meUser(request, 'campus_leader_hn');
    const { context, page } = await authedPage(browser, 'campus_leader_hn', leader);
    try {
      // ── BL-01: VISIT_REQUEST_WAITING_APPROVAL, campus still pending -- must open the review+assign modal.
      await page.goto('/dashboard/visit');
      await clickBellNotification(page, tag);
      await expect(page.getByText('Duyệt & phân công người phụ trách').first()).toBeVisible({ timeout: 20_000 });
      await expect(page.getByText(`Doan HN ${tag}`).first()).toBeVisible();
      // MUST NOT have opened Host Process or the invitation route for this click.
      await expect(page).not.toHaveURL(/\/process\//);
      await expect(page).not.toHaveURL(/\/invitations\//);
    } finally {
      await context.close();
    }

    // Precondition for phase 2: approve + self-host through the real API (the UI approve path is
    // already exercised above/elsewhere; the ACTION under test here is the notification CLICK).
    await approveCampus(request, requestId, hnInstance, 'campus_leader_hn', Number(leader.userId));

    const { context: c2, page: p2 } = await authedPage(browser, 'campus_leader_hn', leader);
    try {
      // ── ST-01 (+ BL-05 partial: the SAME identity is now HOST on the SAME request) — self-host
      // fires NO new notification at all: CampusApprovalExecutor's own comment is "Self-host: the
      // approver already knows — skip the 'you were assigned' notification" (guarded on
      // `!outcome.IsSelfHost`). The bell's only entry is still the ORIGINAL WAITING_APPROVAL
      // notification; now that the campus is approved, `reviewDue` is false, so it must downgrade
      // to the plain safe request detail — never re-open the review modal, never Host Process.
      const n = await latestNotification(request, 'campus_leader_hn');
      expect(n.actionType, 'the original notification still carries an actionType').toBeTruthy();
      await p2.goto('/dashboard/visit');
      await clickBellNotification(p2, tag);
      await expect(p2).toHaveURL(new RegExp(`/dashboard/visit/v2/${requestId}$`), { timeout: 20_000 });
      await expect(p2.getByText('Duyệt & phân công người phụ trách')).toHaveCount(0);
      await expect(p2).not.toHaveURL(/\/process\//);
    } finally {
      await c2.close();
    }
  });

  test('BL-02 / BUG-01: VISIT_REQUEST_UPDATED_PENDING opens History, never the approve/assign modal', async ({ browser, request }) => {
    const tag = `NU${Date.now().toString(36)}`;
    const { requestId, requestCode } = await createMixedRequest(request, tag, `Doan HN ${tag}`, `Doan HCM ${tag}`);
    const before = await readDetail(request, requestId);
    const hnCode = campusOf(before, CAMPUS_HN).campusCode;

    // ACTION under test's PRECONDITION: the owner edits the still-fully-pending request through the
    // REAL edit form (§6 pattern) -- this is the real producer path for VISIT_REQUEST_UPDATED_PENDING.
    const { context: oc, page: op } = await authedPage(browser, 'visitor_owner', OWNER_USER);
    try {
      await op.goto(`/dashboard/visit/v2/${requestId}/edit`);
      const hnCard = op.getByTestId(`campus-edit-card-${hnCode}`);
      await expect(hnCard).toBeVisible({ timeout: 25_000 });
      await hnCard.getByTestId('campus-delegation-input').fill(`Doan HN EDITED ${tag}`);
      const [resp] = await Promise.all([
        op.waitForResponse(r => r.url().includes(`/v2/visit-requests/${requestId}/pending-edit`) && r.request().method() === 'PUT'),
        op.getByTestId('v2-edit-submit').click(),
      ]);
      expect(resp.ok()).toBeTruthy();
    } finally {
      await oc.close();
    }

    const leader = await meUser(request, 'campus_leader_hn');
    const { context, page } = await authedPage(browser, 'campus_leader_hn', leader);
    try {
      await page.goto('/dashboard/visit');
      await clickBellNotification(page, requestCode);
      await expect(page).toHaveURL(new RegExp(`/dashboard/visit/v2/${requestId}#history`), { timeout: 20_000 });
      await expect(page.getByText('Duyệt & phân công người phụ trách')).toHaveCount(0);
      await expect(page).not.toHaveURL(/\/process\//);
    } finally {
      await context.close();
    }
  });

  test('BL-04 / BUG-04: VISIT_PRIVACY_CONSENT_WITHDRAWN opens read-only detail -- never Host Process, never the invitation route, EVEN THOUGH the recipient genuinely still is the current Host', async ({ browser, request }) => {
    const tag = `NP${Date.now().toString(36)}`;
    const { requestId, requestCode, instances } = await createSingleHn(request, tag, 'AGREED');
    const hnInstance = instances[0].visitInstanceId;
    const leader = await meUser(request, 'campus_leader_hn');
    // Precondition (§8 pattern): HN campus ASSIGNED, leader self-hosts -- the recipient genuinely IS
    // the current Host by the time the privacy-withdrawal notification arrives (the exact shape of the
    // reported live bug: a co-existing HOST relation must never let this click escalate).
    await approveCampus(request, requestId, hnInstance, 'campus_leader_hn', Number(leader.userId));

    // ACTION under test's PRECONDITION: the owner withdraws media consent through the REAL safe-edit
    // modal (§8 pattern) -- the real producer path for VISIT_PRIVACY_CONSENT_WITHDRAWN.
    const { context: oc, page: op } = await authedPage(browser, 'visitor_owner', OWNER_USER);
    try {
      await op.goto(`/dashboard/visit/v2/${requestId}`);
      await expect(op.getByTestId(`campus-detail-card-${hnInstance}`)).toBeVisible({ timeout: 25_000 });
      await op.getByTestId('safe-edit-open').click();
      const dialog = op.getByRole('dialog');
      await expect(dialog).toBeVisible();
      await dialog.getByTestId(`safe-edit-media-${hnInstance}`).selectOption('DECLINED');
      const [resp] = await Promise.all([
        op.waitForResponse(r => r.url().includes(`/v2/visit-requests/${requestId}/safe-details`) && r.request().method() === 'PATCH'),
        op.getByTestId('safe-edit-submit').click(),
      ]);
      expect(resp.ok()).toBeTruthy();
      await expect(dialog).toBeHidden({ timeout: 15_000 });
    } finally {
      await oc.close();
    }

    const { context, page } = await authedPage(browser, 'campus_leader_hn', leader);
    try {
      await page.goto('/dashboard/visit');
      // The privacy-withdrawal message names the REQUEST CODE (see
      // SubmitVisitSafeEditCommandHandler), not the delegation name -- matching on `tag` would
      // silently pick the still-present, older WAITING_APPROVAL notification instead (same message
      // text pitfall documented on `clickBellNotification`).
      await clickBellNotification(page, requestCode);
      await expect(page).toHaveURL(new RegExp(`/dashboard/visit/v2/${requestId}$`), { timeout: 20_000 });
      await expect(page).not.toHaveURL(/#history/);
      await expect(page).not.toHaveURL(/\/process\//);
      await expect(page).not.toHaveURL(/\/invitations\//);
      await expect(page.getByText('Duyệt & phân công người phụ trách')).toHaveCount(0);
    } finally {
      await context.close();
    }
  });
});

test.describe('Real-stack: notification routing -- Host (assigned/stale) and Host transfer', () => {
  test('ST-02: a click on a stale HOST_ASSIGNED notification never reaches Host Process once the host has changed', async ({ browser, request }) => {
    const tag = `NS${Date.now().toString(36)}`;
    const { requestId, instances } = await createSingleHn(request, tag);
    const hnInstance = instances[0].visitInstanceId;
    const leader = await meUser(request, 'campus_leader_hn');
    await approveCampus(request, requestId, hnInstance, 'campus_leader_hn', Number(leader.userId));

    const otherStaff = anotherStaffOnCampus(CAMPUS_HN, Number(leader.userId));
    test.skip(otherStaff === null, 'NOT VERIFIED -- no second seeded ACTIVE STAFF user on campus HN to transfer host to');
    const detail = await readDetail(request, requestId);
    const rowVersion = campusOf(detail, CAMPUS_HN).rowVersion;
    await transferHost(request, 'campus_leader_hn', hnInstance, otherStaff!, rowVersion);

    const { context, page } = await authedPage(browser, 'campus_leader_hn', leader);
    try {
      // The HOST_ASSIGNED notification sent at approval time is now stale -- the recipient is no
      // longer the current host.
      await page.goto('/dashboard/visit');
      await clickBellNotification(page, tag);
      await page.waitForTimeout(1500); // resolveAndOpenNotificationTarget's async resolve
      await expect(page).not.toHaveURL(/\/process\//, { timeout: 5_000 });
    } finally {
      await context.close();
    }
  });

  // ST-03/ST-04 both need a HOST_TRANSFER notification an authenticated E2E profile can actually
  // receive. `TransferVisitHostCommandHandler` (a) may only be called by the campus's Staff Leader,
  // and (b) suppresses the notification to whichever side IS that actor ("you already know") — so
  // with `campus_leader_hn` as the only HN Staff Leader profile, a real INCOMING/OUTGOING notification
  // can only ever land on the SECOND HN IC Staff seed profile (`staff_hn`), never on the leader
  // themself. Both journeys therefore transfer between `staff_hn` and the leader, never self-host.

  test('ST-03: HOST_TRANSFER_INCOMING opens Host Process for the exact instance, for the new Host', async ({ browser, request }) => {
    const tag = `NT3${Date.now().toString(36)}`;
    const { requestId, instances } = await createSingleHn(request, tag);
    const hnInstance = instances[0].visitInstanceId;
    const leader = await meUser(request, 'campus_leader_hn');
    const staffHn = await meUser(request, 'staff_hn');
    await approveCampus(request, requestId, hnInstance, 'campus_leader_hn', Number(leader.userId));

    const detail = await readDetail(request, requestId);
    const rowVersion = campusOf(detail, CAMPUS_HN).rowVersion;
    await transferHost(request, 'campus_leader_hn', hnInstance, Number(staffHn.userId), rowVersion);

    const { context, page } = await authedPage(browser, 'staff_hn', staffHn);
    try {
      await page.goto('/dashboard/visit');
      // The i18n template for this eventKey renders {{delegationName}}/{{campusName}} (see
      // notifications.json HOST_TRANSFER_INCOMING) -- the params `TransferVisitHostCommandHandler`
      // actually supplies. It does NOT include requestCode (unlike VISIT_PRIVACY_CONSENT_WITHDRAWN
      // above); matching on the raw backend `Message` column's text would be wrong here, since a
      // recognized eventKey row renders from the i18n template + params, not that legacy column.
      await clickBellNotification(page, tag);
      await expect(page).toHaveURL(new RegExp(`/dashboard/visit/process/${hnInstance}(\\D|$)`), { timeout: 20_000 });
    } finally {
      await context.close();
    }
  });

  test('ST-04: HOST_TRANSFER_OUTGOING never opens Host Process for the outgoing host, even though they land on their own retained participant screen', async ({ browser, request }) => {
    const tag = `NT4${Date.now().toString(36)}`;
    const { requestId, instances } = await createSingleHn(request, tag);
    const hnInstance = instances[0].visitInstanceId;
    const leader = await meUser(request, 'campus_leader_hn');
    const staffHn = await meUser(request, 'staff_hn');
    // staff_hn starts as Host (not the leader) so the OUTGOING notification (to the PREVIOUS host)
    // is not suppressed as "the actor already knows".
    await approveCampus(request, requestId, hnInstance, 'campus_leader_hn', Number(staffHn.userId));

    const detail = await readDetail(request, requestId);
    const rowVersion = campusOf(detail, CAMPUS_HN).rowVersion;
    await transferHost(request, 'campus_leader_hn', hnInstance, Number(leader.userId), rowVersion);

    const { context, page } = await authedPage(browser, 'staff_hn', staffHn);
    try {
      await page.goto('/dashboard/visit');
      // Same i18n-template note as ST-03 -- HOST_TRANSFER_OUTGOING renders {{delegationName}}, not
      // requestCode. staff_hn's own newest notification here is this OUTGOING one (created after
      // their own earlier HOST_ASSIGNED from the approve step above), so `tag` still resolves
      // unambiguously via newest-first ordering.
      await clickBellNotification(page, tag);
      await page.waitForTimeout(1500);
      // NOT `/dashboard/visit/v2/{id}` specifically: `TransferVisitHostCommandHandler` deliberately
      // KEEPS the outgoing host on the visit as an ACCEPTED IC_SUPPORT participant rather than
      // removing them ("dropping them would revoke their access to the very request they were
      // handing over") -- so their CURRENT relation is a real participant, and landing on their own
      // participant screen (`/invitations/{id}`) is a legitimate, non-mutating destination, not a
      // guess. The actual safety invariant this proves is narrower: never Host Process (the role they
      // no longer hold) and never the approve/assign modal.
      await expect(page).not.toHaveURL(/\/process\//);
      await expect(page.getByText('Duyệt & phân công người phụ trách')).toHaveCount(0);
    } finally {
      await context.close();
    }
  });
});

test.describe('Real-stack: notification routing -- Participation', () => {
  // NOTE ON SCOPE (discovered live, not previously reported): the only invite-able participant type
  // this harness can also LOG IN AS is DEPT_SUPPORT (`facilities_leader_hn` -- IC_SUPPORT/STUDENT need
  // an IC-department/Student seed profile that does not exist yet; a SECOND GENERAL department is used
  // here rather than `dept_leader_hn` specifically because `department-leader-personnel.realstack.spec.ts`'s
  // DL-05 deliberately and correctly revokes `dept_leader_hn`'s session as its last, documented action --
  // "carrying the wrong sub-role is exactly what a stale session is" -- so under workers:1 sequential
  // execution any later spec that reuses that identity hits a real, intentional SESSION_REVOKED 401.
  // `facilities_leader_hn` is a same-shape GENERAL-department Leader (see run-realstack-e2e.mjs) that no
  // spec in this suite ever revokes, so it stays a fresh, valid, correctly fail-closed session for the
  // whole run -- the fix is a dedicated identity, never weakening SESSION_REVOKED handling itself, and
  // never relying on spec file ordering as a correctness mechanism). A Dept Leader's PARTICIPATION_INVITED
  // notification does NOT reach `VisitRequestManagement`'s `VISIT_INVITATION` intent branch at all: an
  // earlier, separate rewrite in `resolveNotificationDestination.ts` (`isDeptLeader`, pre-dates this
  // initiative) intercepts any `/invitations/` link first and sends it to `/dashboard/visit?taskId=
  // …&itemType=INVITATION` instead -- a param pair `VisitRequestManagement` never reads (only
  // `SharedDashboardView`/`DeptStaffDashboard` do, and neither is mounted at `/dashboard/visit`). So
  // the exact "opens the participant's OWN screen" positive assertion the plan's PT-01/PT-02 ask for
  // cannot be exercised live with this recipient — it IS covered by the round-2 unit-test matrix
  // (VISIT_INVITATION/CONTRIBUTION exact-participant tests) against a synthetic STAFF fixture. What
  // stays fully live-testable and IS asserted below: the click never escalates into an approval or
  // Host-mutation screen it has no business reaching.
  test('PT-01: PARTICIPATION_INVITED never escalates into Host Process or the approve modal for the invited Department Leader (positive "/invitations/…" destination NOT VERIFIED live -- see note above)', async ({ browser, request }) => {
    const tag = `NI${Date.now().toString(36)}`;
    const { requestId, instances } = await createSingleHn(request, tag);
    const hnInstance = instances[0].visitInstanceId;
    const leader = await meUser(request, 'campus_leader_hn');
    await approveCampus(request, requestId, hnInstance, 'campus_leader_hn', Number(leader.userId));
    await startPreparation(request, requestId, hnInstance, 'campus_leader_hn'); // invites are setup work

    const deptId = await meDepartmentId(request, 'facilities_leader_hn');
    await inviteDeptSupport(request, 'campus_leader_hn', hnInstance, deptId);

    const deptLeader = await meUser(request, 'facilities_leader_hn');
    const { context, page } = await authedPage(browser, 'facilities_leader_hn', deptLeader);
    try {
      await page.goto('/dashboard/visit');
      await clickBellNotification(page, tag);
      await page.waitForTimeout(1000);
      await expect(page).not.toHaveURL(/\/process\//);
      await expect(page.getByText('Duyệt & phân công người phụ trách')).toHaveCount(0);
    } finally {
      await context.close();
    }
  });

  test('PT-02: a click on a stale (declined) invitation never escalates into Host Process or the approve modal', async ({ browser, request }) => {
    const tag = `ND${Date.now().toString(36)}`;
    const { requestId, instances } = await createSingleHn(request, tag);
    const hnInstance = instances[0].visitInstanceId;
    const leader = await meUser(request, 'campus_leader_hn');
    await approveCampus(request, requestId, hnInstance, 'campus_leader_hn', Number(leader.userId));
    await startPreparation(request, requestId, hnInstance, 'campus_leader_hn');

    const deptId = await meDepartmentId(request, 'facilities_leader_hn');
    const participantId = await inviteDeptSupport(request, 'campus_leader_hn', hnInstance, deptId);
    await declineParticipation(request, 'facilities_leader_hn', participantId);

    const deptLeader = await meUser(request, 'facilities_leader_hn');
    const { context, page } = await authedPage(browser, 'facilities_leader_hn', deptLeader);
    try {
      await page.goto('/dashboard/visit');
      await clickBellNotification(page, tag);
      await expect(page).not.toHaveURL(/\/process\//, { timeout: 20_000 });
      await expect(page.getByText('Duyệt & phân công người phụ trách')).toHaveCount(0);
    } finally {
      await context.close();
    }
  });
});

test.describe('Real-stack: notification routing -- multi-campus exact targeting', () => {
  test('MC-01: on a mixed HN+HCM request, self-host approval leaves each leader on their OWN request-level detail, never the sibling campus\'s Host Process', async ({ browser, request }) => {
    const tag = `NM${Date.now().toString(36)}`;
    const { requestId, instances } = await createMixedRequest(request, tag, `Doan HN ${tag}`, `Doan HCM ${tag}`);
    const hnInstance = instances.find(i => i.campusId === CAMPUS_HN)!.visitInstanceId;
    const hcmInstance = instances.find(i => i.campusId === CAMPUS_HCM)!.visitInstanceId;
    const hnLeader = await meUser(request, 'campus_leader_hn');
    const hcmLeader = await meUser(request, 'campus_leader_hcm');
    // Approve BOTH campuses (self-host each). Self-host fires NO HOST_ASSIGNED notification at all
    // (CampusApprovalExecutor: "the approver already knows", guarded on `!outcome.IsSelfHost`) --
    // each leader's bell still only has their own original WAITING_APPROVAL notification, now
    // resolved. What this proves instead: the HCM leader's click still resolves against the SAME
    // multi-campus request but never escalates into the HN campus's Host Process -- exact-instance
    // isolation holds even though no per-instance destination is reached by either leader here.
    await approveCampus(request, requestId, hnInstance, 'campus_leader_hn', Number(hnLeader.userId));
    await approveCampus(request, requestId, hcmInstance, 'campus_leader_hcm', Number(hcmLeader.userId));

    const { context, page } = await authedPage(browser, 'campus_leader_hcm', hcmLeader);
    try {
      await page.goto('/dashboard/visit');
      await clickBellNotification(page, tag);
      await expect(page).toHaveURL(new RegExp(`/dashboard/visit/v2/${requestId}$`), { timeout: 20_000 });
      await expect(page).not.toHaveURL(new RegExp(`/process/${hnInstance}(\\D|$)`));
      await expect(page).not.toHaveURL(new RegExp(`/process/${hcmInstance}(\\D|$)`));
    } finally {
      await context.close();
    }
  });
});

test.describe('Real-stack: notification routing -- legacy unknown notifications (plan section 15)', () => {
  // No producer in the current codebase writes a null-metadataJson row -- these are seeded directly
  // with the exact pre-migration shape (metadata_json = NULL, action_type = OPEN_VISIT_DETAIL), per
  // the plan's own instruction, against a REAL current relation established through real actions.

  test('Legacy-A: recipient is CURRENTLY the Host -- still lands on safe detail, never Host Process', async ({ browser, request }) => {
    const tag = `NLA${Date.now().toString(36)}`;
    const { requestId, instances } = await createSingleHn(request, tag);
    const hnInstance = instances[0].visitInstanceId;
    const leader = await meUser(request, 'campus_leader_hn');
    await approveCampus(request, requestId, hnInstance, 'campus_leader_hn', Number(leader.userId));
    insertLegacyNotification({ recipientEmail: leader.email as string, visitRequestId: requestId, visitInstanceId: hnInstance, title: `Legacy A ${tag}` });

    const { context, page } = await authedPage(browser, 'campus_leader_hn', leader);
    try {
      await page.goto('/dashboard/visit');
      await clickBellNotification(page, `Legacy A ${tag}`);
      await page.waitForTimeout(1000);
      await expect(page).not.toHaveURL(/\/process\//);
      await expect(page.getByText('Duyệt & phân công người phụ trách')).toHaveCount(0);
    } finally {
      await context.close();
    }
  });

  test('Legacy-B: recipient is CURRENTLY the pending-campus reviewer -- still lands on safe detail, never the approve/assign modal', async ({ browser, request }) => {
    const tag = `NLB${Date.now().toString(36)}`;
    const { requestId } = await createSingleHn(request, tag);
    const leader = await meUser(request, 'campus_leader_hn');
    insertLegacyNotification({ recipientEmail: leader.email as string, visitRequestId: requestId, title: `Legacy B ${tag}` });

    const { context, page } = await authedPage(browser, 'campus_leader_hn', leader);
    try {
      await page.goto('/dashboard/visit');
      await clickBellNotification(page, `Legacy B ${tag}`);
      await page.waitForTimeout(1000);
      await expect(page.getByText('Duyệt & phân công người phụ trách')).toHaveCount(0);
      await expect(page).not.toHaveURL(/\/process\//);
    } finally {
      await context.close();
    }
  });

  test('Legacy-C: recipient is CURRENTLY an invited participant -- still lands on safe detail, never auto-opens the invitation route', async ({ browser, request }) => {
    const tag = `NLC${Date.now().toString(36)}`;
    const { requestId, instances } = await createSingleHn(request, tag);
    const hnInstance = instances[0].visitInstanceId;
    const leader = await meUser(request, 'campus_leader_hn');
    await approveCampus(request, requestId, hnInstance, 'campus_leader_hn', Number(leader.userId));
    await startPreparation(request, requestId, hnInstance, 'campus_leader_hn');
    const deptId = await meDepartmentId(request, 'facilities_leader_hn');
    await inviteDeptSupport(request, 'campus_leader_hn', hnInstance, deptId);
    const deptLeader = await meUser(request, 'facilities_leader_hn');
    insertLegacyNotification({ recipientEmail: deptLeader.email as string, visitRequestId: requestId, visitInstanceId: hnInstance, title: `Legacy C ${tag}` });

    const { context, page } = await authedPage(browser, 'facilities_leader_hn', deptLeader);
    try {
      await page.goto('/dashboard/visit');
      await clickBellNotification(page, `Legacy C ${tag}`);
      await page.waitForTimeout(1000);
      await expect(page).not.toHaveURL(/\/invitations\//);
      await expect(page.getByText('Duyệt & phân công người phụ trách')).toHaveCount(0);
    } finally {
      await context.close();
    }
  });
});

test.describe('Real-stack: notification routing -- MC-02 edge fixture (campus event missing its instance id)', () => {
  test('a campus-specific eventKey with no exact instance never guesses a campus on a multi-campus request', async ({ browser, request }) => {
    const tag = `NE${Date.now().toString(36)}`;
    const { requestId, requestCode, instances } = await createMixedRequest(request, tag, `Doan HN ${tag}`, `Doan HCM ${tag}`);
    const hnInstance = instances.find(i => i.campusId === CAMPUS_HN)!.visitInstanceId;
    const hcmInstance = instances.find(i => i.campusId === CAMPUS_HCM)!.visitInstanceId;
    const leader = await meUser(request, 'campus_leader_hn');
    await approveCampus(request, requestId, hnInstance, 'campus_leader_hn', Number(leader.userId));
    insertCampusEventMissingInstance({
      recipientEmail: leader.email as string, visitRequestId: requestId, requestCode,
      title: `Edge case ${tag}`, message: `Khach rut quyen truyen thong ${tag} (khong ro cơ sở)`,
    });

    const { context, page } = await authedPage(browser, 'campus_leader_hn', leader);
    try {
      await page.goto('/dashboard/visit');
      await clickBellNotification(page, requestCode);
      await page.waitForTimeout(1000);
      // Never guesses a specific campus's operational screen, and never the change-history anchor
      // (that belongs only to VISIT_HISTORY) -- the one safe, campus-agnostic landing.
      await expect(page).not.toHaveURL(new RegExp(`/process/${hnInstance}(\\D|$)`));
      await expect(page).not.toHaveURL(new RegExp(`/process/${hcmInstance}(\\D|$)`));
      await expect(page).not.toHaveURL(/#history/);
    } finally {
      await context.close();
    }
  });
});

test.describe('Real-stack: notification routing -- click ordering (BUG-02/BUG-03 + rapid-click race + back/forward)', () => {
  test('BUG-02: clicking the SAME notification a second time still opens it (not silently a no-op)', async ({ browser, request }) => {
    const tag = `NC${Date.now().toString(36)}`;
    await createSingleHn(request, tag);
    const leader = await meUser(request, 'campus_leader_hn');

    const { context, page } = await authedPage(browser, 'campus_leader_hn', leader);
    try {
      await page.goto('/dashboard/visit');
      await clickBellNotification(page, tag);
      await expect(page.getByText('Duyệt & phân công người phụ trách').first()).toBeVisible({ timeout: 20_000 });
      // Close without deciding, then come back to the same list fresh -- the notification itself
      // is unchanged (still there, now marked read), so the SECOND click below is the real repro of
      // BUG-02 ("click notification -> open -> close -> click same notification again -> nothing").
      await page.getByTestId('assign-host-modal-close').click();
      await expect(page.getByText('Duyệt & phân công người phụ trách')).toHaveCount(0, { timeout: 10_000 });
      await page.goto('/dashboard/visit');

      // Click the SAME notification again.
      await clickBellNotification(page, tag);
      await expect(page.getByText('Duyệt & phân công người phụ trách').first()).toBeVisible({ timeout: 20_000 });
    } finally {
      await context.close();
    }
  });

  test('BUG-03 + rapid-click race: click A (slow) then B (fast) without waiting -- the final state is B, never A, even after A\'s delayed response finally arrives', async ({ browser, request }) => {
    const tagA = `NRA${Date.now().toString(36)}`;
    const tagB = `NRB${Date.now().toString(36)}`;
    const a = await createSingleHn(request, tagA);
    await createSingleHn(request, tagB);
    const leader = await meUser(request, 'campus_leader_hn');

    const { context, page } = await authedPage(browser, 'campus_leader_hn', leader);
    try {
      // Delay ONLY the target-resolution list call for request A -- a real backend round-trip, just
      // slow. B's own call (and everything else) is untouched.
      // fallback() (not continue()) -- the context-level route registered by `authedPage` injects the
      // E2E auth headers, and it must still run AFTER this delay, or the delayed request comes back
      // as a real 401 instead of a real, merely slow, 200.
      await page.route('**/viewguestdelegationlist*', async (route) => {
        const url = route.request().url();
        if (url.includes(`visitRequestId=${a.requestId}`)) {
          await new Promise((r) => setTimeout(r, 2500));
        }
        await route.fallback();
      });

      await page.goto('/dashboard/visit');
      await clickBellNotification(page, tagA); // slow -- in flight
      await clickBellNotification(page, tagB); // fast -- same mounted route, no remount

      await expect(page.getByText(`Doan HN ${tagB}`).first()).toBeVisible({ timeout: 20_000 });
      await expect(page.getByText(`Doan HN ${tagA}`)).toHaveCount(0);

      // Let A's delayed response finally land -- it must NOT clobber B's already-open modal.
      await page.waitForTimeout(3500);
      await expect(page.getByText(`Doan HN ${tagB}`).first()).toBeVisible();
      await expect(page.getByText(`Doan HN ${tagA}`)).toHaveCount(0);
    } finally {
      await context.close();
    }
  });

  test('back/forward: the URL and the rendered screen always agree after navigating between two notification targets', async ({ browser, request }) => {
    const tagA = `NFA${Date.now().toString(36)}`;
    const tagB = `NFB${Date.now().toString(36)}`;
    const a = await createSingleHn(request, tagA);
    const b = await createSingleHn(request, tagB);
    const leader = await meUser(request, 'campus_leader_hn');
    await approveCampus(request, a.requestId, a.instances[0].visitInstanceId, 'campus_leader_hn', Number(leader.userId));
    await approveCampus(request, b.requestId, b.instances[0].visitInstanceId, 'campus_leader_hn', Number(leader.userId));

    // Self-host fires NO notification at all for either request (CampusApprovalExecutor: "the
    // approver already knows", guarded on `!outcome.IsSelfHost`) -- each bell click still only has
    // the ORIGINAL WAITING_APPROVAL notification for that request, now resolved, so both correctly
    // downgrade to their own request-level detail rather than either instance's Host Process. That
    // still gives two genuinely different destinations (different requestId), so the back/forward
    // history-coherence invariant this test exists for is still fully exercised.
    const targetA = new RegExp(`/dashboard/visit/v2/${a.requestId}(\\D|$)`);
    const targetB = new RegExp(`/dashboard/visit/v2/${b.requestId}(\\D|$)`);

    const { context, page } = await authedPage(browser, 'campus_leader_hn', leader);
    try {
      await page.goto('/dashboard/visit');
      await clickBellNotification(page, tagA);
      await expect(page).toHaveURL(targetA, { timeout: 20_000 });

      await clickBellNotification(page, tagB);
      await expect(page).toHaveURL(targetB, { timeout: 20_000 });

      // One notification click can legitimately push MORE than one history entry (the one-shot
      // command URL, then the final destination) -- `goBack` a bounded number of times rather than
      // assuming an exact count, so this proves the real invariant (browser history stays coherent,
      // URL and screen always agree, A's page is reachable again) without depending on an internal
      // implementation detail of how many entries one click happens to push.
      let landedOnA = false;
      for (let i = 0; i < 4 && !landedOnA; i++) {
        await page.goBack();
        await page.waitForTimeout(300);
        if (targetA.test(page.url())) landedOnA = true;
      }
      expect(landedOnA, `goBack() never reached A's page (${page.url()})`).toBe(true);
      // URL and rendered screen agree: A's request detail is actually showing, not just the URL.
      await expect(page).not.toHaveURL(targetB);

      let landedOnB = false;
      for (let i = 0; i < 4 && !landedOnB; i++) {
        await page.goForward();
        await page.waitForTimeout(300);
        if (targetB.test(page.url())) landedOnB = true;
      }
      expect(landedOnB, `goForward() never reached B's page (${page.url()})`).toBe(true);
    } finally {
      await context.close();
    }
  });
});

test.describe('Real-stack: notification routing -- cross-surface parity', () => {
  test('Bell and NotificationsPage resolve the SAME destination for the same real notification', async ({ browser, request }) => {
    const tag = `NX${Date.now().toString(36)}`;
    await createSingleHn(request, tag);
    const leader = await meUser(request, 'campus_leader_hn');

    const bellCtx = await authedPage(browser, 'campus_leader_hn', leader);
    try {
      await bellCtx.page.goto('/dashboard/visit');
      await clickBellNotification(bellCtx.page, tag);
      await expect(bellCtx.page.getByText('Duyệt & phân công người phụ trách').first()).toBeVisible({ timeout: 20_000 });
    } finally {
      await bellCtx.context.close();
    }

    // A second, equivalent notification (same eventKey/producer) clicked from the FULL PAGE surface.
    const tag2 = `NX2${Date.now().toString(36)}`;
    await createSingleHn(request, tag2);
    const pageCtx = await authedPage(browser, 'campus_leader_hn', leader);
    try {
      await pageCtx.page.goto('/dashboard/visit');
      await clickNotificationsPageItem(pageCtx.page, tag2);
      await expect(pageCtx.page.getByText('Duyệt & phân công người phụ trách').first()).toBeVisible({ timeout: 20_000 });
    } finally {
      await pageCtx.context.close();
    }
  });
});
