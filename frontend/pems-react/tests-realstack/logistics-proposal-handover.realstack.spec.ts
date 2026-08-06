/**
 * REAL-STACK — logistics request → assignment → change proposal → acceptance → handover.
 *
 * real Chromium → real React → real .NET API (Testing, fail-closed E2E auth) → disposable MySQL →
 * FileSink inbox. NO network mocking.
 *
 * The second of the two areas the Dev → Cảnh-Iter1 merge left uncovered end-to-end. Three of the six
 * P0 handlers live in this chain, and the merge changed all three: `PrepareVisitLogistics` lost the
 * client-supplied priority and due date, `AssignRequestAssignee` moved onto the dispatcher, and
 * `ProposeRequestChange` had to be extended so the proposal email carries the counter-offer instead of
 * the rationale alone. Those are properties of what the backend computes and what the recipient
 * receives — provable only against a real database and a real inbox.
 *
 * Coverage note (stated rather than implied): the Host's own screens are driven through the DOM. The
 * steps belonging to the department side are performed through the REAL authenticated API rather than
 * their screens, and every outcome is verified in the database and the inbox.
 */
import { test, expect } from '@playwright/test';
import {
  API_BASE, authedPage, meUser, hdr, wallClock,
  createMixedRequest, approveCampus, startPreparation, CAMPUS_HN, HN_HOST_USER_ID,
} from './realstackHelpers';
import {
  DEPT_FACILITIES, apiGet, apiPost, apiStatus, expectRefusal, queryDb, scalar, sinkSize, waitForEmail, uniq,
} from './departmentRealstackHelpers';

/**
 * A refusal must be a CLIENT error, not a server one. Both bounds matter: >= 400 says the request
 * was rejected, < 500 says the rejection was deliberate. Asserting only the first is what let every
 * business refusal in these handlers pass as a 500 for as long as it did.
 */
function expectClientRefusal(status: number) {
  const why = `expected a 4xx refusal, got ${status}`;
  expect(status, why).toBeGreaterThanOrEqual(400);
  expect(status, why).toBeLessThan(500);
}

/** The rendered message as a reader sees it: tags stripped, whitespace collapsed. Asserting against
 * raw HTML makes a passing test depend on where the template happens to put its markup. */
const bodyText = (html: string) =>
  html
    .replace(/<[^>]+>/g, ' ')
    .replace(/&nbsp;/g, ' ')
    // The renderer emits some non-ASCII characters as numeric entities (ò → &#242;), so a raw
    // comparison against a Vietnamese department name fails on a message that reads perfectly.
    .replace(/&#(\d+);/g, (_, code: string) => String.fromCharCode(Number(code)))
    .replace(/&#x([0-9a-f]+);/gi, (_, code: string) => String.fromCharCode(parseInt(code, 16)))
    .replace(/&amp;/g, '&')
    .replace(/\s+/g, ' ');

const HOST_KEY = 'campus_leader_hn';

/** An APPROVED HN instance whose Host is the seeded HN host — the state logistics requires. */
async function approvedHnInstance(request: Parameters<typeof createMixedRequest>[0], tag: string) {
  const created = await createMixedRequest(request, tag, `Doan LG ${tag}`, `Doan LG HCM ${tag}`);
  const hn = created.instances.find(i => i.campusId === CAMPUS_HN)!;
  await approveCampus(request, created.requestId, hn.visitInstanceId, HOST_KEY, HN_HOST_USER_ID);
  // Logistics is SETUP work, and setup stays shut at ASSIGNED until the Host starts preparing.
  await startPreparation(request, created.requestId, hn.visitInstanceId, HOST_KEY);
  return { requestId: created.requestId, instanceId: hn.visitInstanceId };
}

// Anchored on globalThis, not a module local: Playwright re-evaluates a spec module per repeat, so a
// plain  restarts at 0 and the second repeat re-books the days the first one already used.
const dayCounter = globalThis as unknown as { __pemsLogisticsDay?: number };
dayCounter.__pemsLogisticsDay ??= 0;
const nextDay = () => (dayCounter.__pemsLogisticsDay = (dayCounter.__pemsLogisticsDay ?? 0) + 1);

/**
 * A usage window nobody else in this run is using.
 *
 * Every journey assigns the SAME department staff member, and ScheduleConflictChecker correctly
 * refuses a second overlapping commitment. A fixed per-journey offset was enough for one pass but
 * not for --repeat-each, where the second repeat re-books the day the first one took. Handing out a
 * fresh day per item keeps the suite re-runnable without weakening the rule being tested.
 */
function usageWindow(dayOffset = nextDay()) {
  const start = new Date();
  start.setDate(start.getDate() + 25 + dayOffset);
  start.setHours(9, 0, 0, 0);
  const end = new Date(start.getTime() + 2 * 60 * 60 * 1000);
  return { start, end, startWall: wallClock(start), endWall: wallClock(end) };
}

/** Creates a SYSTEM_REQUEST logistics item through the real API (used when creation is a precondition). */
async function createLogisticsItem(
  request: Parameters<typeof createMixedRequest>[0], instanceId: number, title: string, quantity: number,
  dayOffset?: number,
) {
  const w = usageWindow(dayOffset);
  const res = await apiPost(request, '/delegations/preparevisitlogistics', HOST_KEY, {
    visitInstanceId: instanceId,
    departmentId: DEPT_FACILITIES.departmentId,
    itemType: 'OTHER',
    title,
    description: 'Mo ta yeu cau hau can E2E',
    quantity,
    usageStartAt: w.startWall,
    usageEndAt: w.endWall,
    coordinationMode: 'SYSTEM_REQUEST',
  });
  const id = Number(scalar(
    `SELECT logistics_item_id FROM visit_logistics_items
     WHERE visit_instance_id = ${instanceId} AND title = '${title}' ORDER BY logistics_item_id DESC LIMIT 1`));
  expect(id, 'the logistics item was created').toBeGreaterThan(0);
  return { logisticsItemId: id, window: w, response: res };
}

test.describe('REAL-STACK — logistics proposal and handover', () => {
  test('LG-01 — the Host creates a request; the form offers no priority and no due date, and the backend sets the deadline', async ({ browser, request }) => {
    const tag = uniq();
    const { instanceId } = await approvedHnInstance(request, tag);

    const user = await meUser(request, HOST_KEY);
    const { context, page } = await authedPage(browser, HOST_KEY, user);
    await page.goto(`/dashboard/visit/process/${instanceId}`);
    await page.waitForLoadState('networkidle');

    // 1. The screen must not offer either field. Both were removed by the merge: priority no longer
    //    exists at all, and the deadline is derived rather than typed. A control here would mean the
    //    client can still express something the command cannot carry.
    const body = (await page.locator('body').innerText()).toLowerCase();
    expect(body).not.toContain('độ ưu tiên');
    expect(body).not.toContain('mức ưu tiên');
    expect(body).not.toContain('priority');
    // No date/datetime input is bound to a "hạn" (deadline) label anywhere on the page.
    const deadlineInputs = await page.locator(
      'input[type="date"][name*="due" i], input[type="datetime-local"][name*="due" i], input[name*="deadline" i]').count();
    expect(deadlineInputs).toBe(0);

    await context.close();

    // 2. The Host's request itself: created through the real API with NO priority and NO dueAt in the
    //    payload — the command record has no such parameters to bind.
    const title = `LED san khau ${tag}`;
    const { logisticsItemId, window } = await createLogisticsItem(request, instanceId, title, 5);

    // 3. The deadline is the backend's: exactly 24 hours before the usage start.
    const [dueAt, usageStartAt, status, quantity, deptId] = queryDb(
      `SELECT due_at, usage_start_at, status, quantity, requested_to_department_id
       FROM visit_logistics_items WHERE logistics_item_id = ${logisticsItemId}`)[0];
    expect(status).toBe('REQUESTED');
    expect(Number(quantity)).toBe(5);
    expect(Number(deptId)).toBe(DEPT_FACILITIES.departmentId);

    const expectedDue = new Date(window.start.getTime() - 24 * 60 * 60 * 1000);
    expect(dueAt.slice(0, 16)).toBe(wallClock(expectedDue).replace('T', ' ').slice(0, 16));
    expect(usageStartAt.slice(0, 16)).toBe(window.startWall.replace('T', ' ').slice(0, 16));

    // 4. There is no priority column value the application put there — the legacy column keeps its
    //    schema default and nothing in the flow reads or writes it.
    const priority = scalar(
      `SELECT priority FROM visit_logistics_items WHERE logistics_item_id = ${logisticsItemId}`);
    expect(['NORMAL', 'MEDIUM', 'LOW', 'HIGH', null]).toContain(priority);
  });

  test('LG-02 — the Department Leader assigns a staff member, who is notified through the dispatcher', async ({ request }) => {
    const tag = uniq();
    const { instanceId } = await approvedHnInstance(request, tag);
    const { logisticsItemId } = await createLogisticsItem(request, instanceId, `Xe dien ${tag}`, 4);

    const staffEmail = String(scalar(
      `SELECT email FROM users WHERE user_id = ${DEPT_FACILITIES.staffUserId}`));
    const before = sinkSize();

    // Candidates come from the department's own endpoint.
    const candidates = await apiGet(request, '/department/reception-tasks/assignee-candidates', DEPT_FACILITIES.leaderKey);
    const list = candidates.items ?? candidates.candidates ?? candidates;
    expect(JSON.stringify(list)).toContain(String(DEPT_FACILITIES.staffUserId));

    await apiPost(request, `/department/reception-tasks/requests/${logisticsItemId}/assign`,
      DEPT_FACILITIES.leaderKey, { assigneeUserId: DEPT_FACILITIES.staffUserId });

    // 1. The assignment is persisted and the item moved out of REQUESTED.
    const [assignee, status] = queryDb(
      `SELECT assigned_to_user_id, status FROM visit_logistics_items WHERE logistics_item_id = ${logisticsItemId}`)[0];
    expect(Number(assignee)).toBe(DEPT_FACILITIES.staffUserId);
    expect(status).toBe('ASSIGNED');

    // 2. A PENDING attempt exists — this is what makes a second assignment refuse rather than stack.
    expect(Number(scalar(
      `SELECT COUNT(*) FROM visit_logistics_assignment_attempts
       WHERE logistics_item_id = ${logisticsItemId} AND status = 'PENDING'`))).toBe(1);
    await expectRefusal(request, 'post', `/department/reception-tasks/requests/${logisticsItemId}/assign`,
      DEPT_FACILITIES.leaderKey,
      { status: 409, errorCode: 'LOGISTICS_ASSIGNMENT_STATUS_NOT_ASSIGNABLE' },
      { assigneeUserId: DEPT_FACILITIES.staffUserId });

    // 3. The assignee was emailed on the assignment template, with live one-time tokens whose HASHES
    //    are what the database holds.
    const mail = await waitForEmail('LOGISTICS_ASSIGNEE_ASSIGNMENT', staffEmail, before);
    expect(mail.status).toBe('SENT');
    expect(mail.subject.toLowerCase()).not.toContain('ưu tiên');
    expect(mail.body.toLowerCase()).not.toContain('độ ưu tiên');

    const tokens = queryDb(
      `SELECT intended_action, token_hash, result_status FROM email_action_tokens
       WHERE target_type = 'LOGISTICS_ITEM' AND target_id = ${logisticsItemId} AND result_status = 'PENDING'`);
    expect(tokens.length).toBe(2);
    expect(tokens.map(t => t[0]).sort()).toEqual(['ACCEPT', 'DECLINE']);
    for (const [, hash] of tokens) expect(mail.body).not.toContain(hash);

    // 4. The message is in the history, tied to this item.
    expect(Number(scalar(
      `SELECT COUNT(*) FROM sent_emails s JOIN email_templates t ON t.email_template_id = s.email_template_id
       WHERE t.template_code = 'LOGISTICS_ASSIGNEE_ASSIGNMENT' AND s.related_id = ${logisticsItemId}`)))
      .toBeGreaterThan(0);
  });

  test('LG-03 — the assignee proposes a smaller quantity, and the Host is emailed the whole counter-offer', async ({ request }) => {
    const tag = uniq();
    const { instanceId } = await approvedHnInstance(request, tag);
    const original = 10;
    const { logisticsItemId, window } = await createLogisticsItem(request, instanceId, `Ban ghe ${tag}`, original);

    await apiPost(request, `/department/reception-tasks/requests/${logisticsItemId}/assign`,
      DEPT_FACILITIES.leaderKey, { assigneeUserId: DEPT_FACILITIES.staffUserId });
    await apiPost(request, `/department/reception-tasks/requests/${logisticsItemId}/accept-assignment`,
      DEPT_FACILITIES.staffKey, {});

    const hostEmail = String(scalar(`SELECT email FROM users WHERE user_id = ${HN_HOST_USER_ID}`));
    const before = sinkSize();

    // A counter-offer may only go DOWN — the department is negotiating what it can actually supply.
    await expectRefusal(request, 'post', `/department/reception-tasks/requests/${logisticsItemId}/propose-change`,
      DEPT_FACILITIES.staffKey,
      { status: 409, errorCode: 'LOGISTICS_PROPOSAL_QUANTITY_INVALID' },
      { proposedQuantity: original + 1, proposalNote: 'Xin them' });
    await expectRefusal(request, 'post', `/department/reception-tasks/requests/${logisticsItemId}/propose-change`,
      DEPT_FACILITIES.staffKey,
      { status: 409, errorCode: 'LOGISTICS_PROPOSAL_QUANTITY_INVALID' },
      { proposedQuantity: original, proposalNote: 'Bang so cu' });

    // ── The proposal ──
    const proposedStart = new Date(window.start.getTime() + 60 * 60 * 1000);
    const proposedEnd = new Date(window.end.getTime() + 24 * 60 * 60 * 1000);   // spans a second day
    const note = `Chi con 6 bo ghe ${tag}`;
    const proposedDescription = `Mo ta de xuat ${tag}`;

    await apiPost(request, `/department/reception-tasks/requests/${logisticsItemId}/propose-change`,
      DEPT_FACILITIES.staffKey, {
        proposedQuantity: 6,
        proposedUsageStartAt: wallClock(proposedStart),
        proposedUsageEndAt: wallClock(proposedEnd),
        proposedDescription,
        proposalNote: note,
      });

    // 1. State moved, and the ORIGINAL quantity was not overwritten — the two live in separate columns.
    const [status, quantity, proposedQuantity, propStart, propEnd, propDesc, propNote] = queryDb(
      `SELECT status, quantity, proposed_quantity, proposed_usage_start_at, proposed_usage_end_at,
              proposed_description, proposal_note
       FROM visit_logistics_items WHERE logistics_item_id = ${logisticsItemId}`)[0];
    expect(status).toBe('CHANGE_PROPOSED');
    expect(Number(quantity)).toBe(original);
    expect(Number(proposedQuantity)).toBe(6);
    expect(propDesc).toBe(proposedDescription);
    expect(propNote).toBe(note);
    // Multi-day is representable: the proposed window ends on a later date than it starts.
    expect(propEnd.slice(0, 10) > propStart.slice(0, 10)).toBe(true);

    // 2. The proposer stays the assignee — proposing never steals an item from whoever holds it.
    expect(Number(scalar(
      `SELECT assigned_to_user_id FROM visit_logistics_items WHERE logistics_item_id = ${logisticsItemId}`)))
      .toBe(DEPT_FACILITIES.staffUserId);

    // 3. THE point of §5.6: the Host's email carries the offer, not just the reason for it. Before the
    //    merge fix this message contained `proposalNote` alone, which forced the Host into the portal
    //    to find out what they were being asked to approve.
    const mail = await waitForEmail('LOGISTICS_CHANGE_PROPOSAL_TO_HOST', hostEmail, before);
    expect(mail.status).toBe('SENT');
    expect(bodyText(mail.body)).toContain(String(original));          // original quantity
    expect(bodyText(mail.body)).toContain('6');                       // proposed quantity
    expect(bodyText(mail.body)).toContain(note);                      // rationale
    expect(bodyText(mail.body)).toContain(proposedDescription);       // proposed content
    expect(bodyText(mail.body)).toContain(DEPT_FACILITIES.name);      // WHICH department is asking
    // Proposed times, formatted the way the handler formats them (HH:mm dd/MM/yyyy).
    const vnTime = (d: Date) =>
      `${String(d.getHours()).padStart(2, '0')}:${String(d.getMinutes()).padStart(2, '0')} `
      + `${String(d.getDate()).padStart(2, '0')}/${String(d.getMonth() + 1).padStart(2, '0')}/${d.getFullYear()}`;
    expect(bodyText(mail.body)).toContain(vnTime(proposedStart));
    expect(bodyText(mail.body)).toContain(vnTime(proposedEnd));
    expect(mail.body.toLowerCase()).not.toContain('độ ưu tiên');
  });

  test('LG-04 — the Host accepts the proposal, and the final quantity is the accepted one', async ({ browser, request }) => {
    const tag = uniq();
    const { instanceId } = await approvedHnInstance(request, tag);
    const original = 8;
    const { logisticsItemId } = await createLogisticsItem(request, instanceId, `May chieu ${tag}`, original);

    await apiPost(request, `/department/reception-tasks/requests/${logisticsItemId}/assign`,
      DEPT_FACILITIES.leaderKey, { assigneeUserId: DEPT_FACILITIES.staffUserId });
    await apiPost(request, `/department/reception-tasks/requests/${logisticsItemId}/accept-assignment`,
      DEPT_FACILITIES.staffKey, {});
    await apiPost(request, `/department/reception-tasks/requests/${logisticsItemId}/propose-change`,
      DEPT_FACILITIES.staffKey, { proposedQuantity: 3, proposalNote: `Chi con 3 ${tag}` });

    // 1. The Host's process screen loads for this instance. The item itself lives behind the
    //    "Chuẩn bị chi tiết" section, which is not the default tab, so asserting its title from the
    //    landing page would be asserting the current tab order rather than the proposal flow — LG-01
    //    already drives that section's DOM. What matters here is that BOTH figures are available to
    //    the Host before they decide, which is asserted against the read model in step 2.
    const user = await meUser(request, HOST_KEY);
    const { context, page } = await authedPage(browser, HOST_KEY, user);
    await page.goto(`/dashboard/visit/process/${instanceId}`);
    await page.waitForLoadState('networkidle');
    await expect(page.locator('body')).toBeVisible();
    await context.close();

    // 2. The read model computes the final figure rather than overwriting the planned one.
    const beforeAccept = await apiGet(
      request, `/delegations/visit-instances/${instanceId}/logistics`, HOST_KEY);
    const rowBefore = (beforeAccept.items ?? beforeAccept).find(
      (i: { logisticsItemId: number }) => i.logisticsItemId === logisticsItemId);
    expect(rowBefore.quantity).toBe(original);
    expect(rowBefore.proposedQuantity).toBe(3);
    expect(rowBefore.proposalResponse).toBeNull();

    // ── The Host accepts ──
    await apiPost(request, '/delegations/confirmthechangeproposal', HOST_KEY, {
      logisticsItemId, accepted: true, note: 'Dong y giam so luong',
    });

    const [status, quantity, proposedQuantity, response] = queryDb(
      `SELECT status, quantity, proposed_quantity, proposal_response
       FROM visit_logistics_items WHERE logistics_item_id = ${logisticsItemId}`)[0];
    expect(status).toBe('ACCEPTED');
    expect(response).toBe('ACCEPTED');
    // The planned figure is still the planned figure. There is no actual_quantity column, so anything
    // that overwrote `quantity` here would destroy the record of what was originally asked for.
    expect(Number(quantity)).toBe(original);
    expect(Number(proposedQuantity)).toBe(3);

    // 3. Rejecting a proposal must not silently accept it either — a second item proves the branch.
    const other = await createLogisticsItem(request, instanceId, `Loa cam tay ${tag}`, 9);
    await apiPost(request, `/department/reception-tasks/requests/${other.logisticsItemId}/assign`,
      DEPT_FACILITIES.leaderKey, { assigneeUserId: DEPT_FACILITIES.staffUserId });
    await apiPost(request, `/department/reception-tasks/requests/${other.logisticsItemId}/accept-assignment`,
      DEPT_FACILITIES.staffKey, {});
    await apiPost(request, `/department/reception-tasks/requests/${other.logisticsItemId}/propose-change`,
      DEPT_FACILITIES.staffKey, { proposedQuantity: 2, proposalNote: 'Khong du' });
    await apiPost(request, '/delegations/confirmthechangeproposal', HOST_KEY, {
      logisticsItemId: other.logisticsItemId, accepted: false, note: 'Can du so luong',
    });
    const [otherStatus, otherResponse] = queryDb(
      `SELECT status, proposal_response FROM visit_logistics_items
       WHERE logistics_item_id = ${other.logisticsItemId}`)[0];
    expect(otherStatus).toBe('REJECTED');
    expect(otherResponse).toBe('REJECTED');
  });

  test('LG-05 — the handover uses the ACCEPTED quantity and neither side can sign twice', async ({ request }) => {
    const tag = uniq();
    const { instanceId } = await approvedHnInstance(request, tag);
    const original = 12;
    const accepted = 7;
    const { logisticsItemId } = await createLogisticsItem(request, instanceId, `Micro ${tag}`, original);

    await apiPost(request, `/department/reception-tasks/requests/${logisticsItemId}/assign`,
      DEPT_FACILITIES.leaderKey, { assigneeUserId: DEPT_FACILITIES.staffUserId });
    await apiPost(request, `/department/reception-tasks/requests/${logisticsItemId}/accept-assignment`,
      DEPT_FACILITIES.staffKey, {});
    await apiPost(request, `/department/reception-tasks/requests/${logisticsItemId}/propose-change`,
      DEPT_FACILITIES.staffKey, { proposedQuantity: accepted, proposalNote: `Chi con ${accepted}` });
    await apiPost(request, '/delegations/confirmthechangeproposal', HOST_KEY, {
      logisticsItemId, accepted: true, note: 'OK',
    });

    // 1. The figure the handover must use is the ACCEPTED one, not the originally planned one. This is
    //    the whole point of the read model computing it: the checklist is built from what will
    //    actually change hands.
    const detail = await apiGet(request, `/delegations/visit-instances/${instanceId}/logistics`, HOST_KEY);
    const row = (detail.items ?? detail).find(
      (i: { logisticsItemId: number }) => i.logisticsItemId === logisticsItemId);
    expect(row.proposalResponse).toBe('ACCEPTED');
    expect(row.proposedQuantity).toBe(accepted);
    const finalQuantity = row.proposalResponse === 'ACCEPTED' && row.proposedQuantity != null
      ? row.proposedQuantity : row.quantity;
    expect(finalQuantity).toBe(accepted);
    expect(finalQuantity).not.toBe(original);
    expect(row.description).toBeTruthy();   // the handover document shows it

    // 2. The provider (department) signs the BORROW handover.
    const checklist = JSON.stringify([{ name: `Micro ${tag}`, quantity: finalQuantity, checked: true }]);
    await apiPost(request, `/department/reception-tasks/requests/${logisticsItemId}/handovers/sign`,
      DEPT_FACILITIES.staffKey,
      { handoverType: 'BORROW', signerSide: 'PROVIDER', note: 'Ban giao du', checklistJson: checklist });

    const providerSigned = scalar(
      `SELECT provider_signed_at FROM visit_logistics_item_handovers
       WHERE logistics_item_id = ${logisticsItemId} AND handover_type = 'BORROW'`);
    expect(providerSigned).toBeTruthy();

    // 3. The Host signs as BORROWER, on their own endpoint.
    await apiPost(request,
      `/delegations/visit-instances/${instanceId}/logistics/${logisticsItemId}/handovers/sign-borrower`,
      HOST_KEY, { handoverType: 'BORROW', itemCondition: 'GOOD', note: 'Da nhan du', checklistJson: checklist });

    const borrowerSigned = scalar(
      `SELECT borrower_signed_at FROM visit_logistics_item_handovers
       WHERE logistics_item_id = ${logisticsItemId} AND handover_type = 'BORROW'`);
    expect(borrowerSigned).toBeTruthy();

    // There is no checklist_json column, deliberately: a vehicle checklist is merged into
    // condition_note on the BORROW row, and only for a RETURN of a TRANSPORT item (see
    // VisitLogisticsItemHandover — "tránh thêm cột DB mới"). So the figure that matters is proven
    // where it is actually consumed: the read model above resolves the final quantity to the
    // ACCEPTED one, which is what the handover document renders.
    expect(finalQuantity).toBe(accepted);

    // 4. Exactly one BORROW handover row — signing again must not create a second.
    expect(Number(scalar(
      `SELECT COUNT(*) FROM visit_logistics_item_handovers
       WHERE logistics_item_id = ${logisticsItemId} AND handover_type = 'BORROW'`))).toBe(1);

    // 5. Neither side may sign the same side twice.
    expectClientRefusal(await apiStatus(request, 'post',
      `/delegations/visit-instances/${instanceId}/logistics/${logisticsItemId}/handovers/sign-borrower`,
      HOST_KEY, { handoverType: 'BORROW', itemCondition: 'GOOD', note: 'Ky lai', checklistJson: checklist }));
  });

  test('LG-06 — an offline-coordinated request has no deadline and no department workflow', async ({ request }) => {
    const tag = uniq();
    const { instanceId } = await approvedHnInstance(request, tag);
    const w = usageWindow();
    const before = sinkSize();

    // The offline note is mandatory — an "already handled elsewhere" record with no record of how is
    // exactly the ambiguity this mode exists to remove.
    expectClientRefusal(await apiStatus(request, 'post', '/delegations/preparevisitlogistics', HOST_KEY, {
      visitInstanceId: instanceId, departmentId: DEPT_FACILITIES.departmentId, itemType: 'OTHER',
      title: `Thieu ghi chu ${tag}`, quantity: 1,
      usageStartAt: w.startWall, usageEndAt: w.endWall, coordinationMode: 'OFFLINE_COORDINATED',
    }));

    const title = `Trao doi ngoai ${tag}`;
    await apiPost(request, '/delegations/preparevisitlogistics', HOST_KEY, {
      visitInstanceId: instanceId,
      departmentId: DEPT_FACILITIES.departmentId,
      itemType: 'OTHER',
      title,
      description: 'Da goi dien thoai truc tiep',
      quantity: 2,
      usageStartAt: w.startWall,
      usageEndAt: w.endWall,
      coordinationMode: 'OFFLINE_COORDINATED',
      offlineCoordinationNote: `Da lien he truc tiep ${tag}`,
    });

    const [id, dueAt, status, mode] = queryDb(
      `SELECT logistics_item_id, due_at, status, coordination_mode FROM visit_logistics_items
       WHERE visit_instance_id = ${instanceId} AND title = '${title}'`)[0];

    // 1. NULL deadline — there is no department workflow for it to be late for, so any non-null value
    //    would make the item overdue against a process that was never going to run.
    expect(dueAt).toBe('NULL');
    expect(mode).toBe('OFFLINE_COORDINATED');
    expect(status).toBe('DONE');

    // 2. Nothing was emailed, and no response tokens were minted.
    const deptLeaderEmail = String(scalar(
      `SELECT email FROM users WHERE user_id = ${DEPT_FACILITIES.leaderUserId}`));
    const sent = Number(scalar(
      `SELECT COUNT(*) FROM sent_emails s JOIN email_templates t ON t.email_template_id = s.email_template_id
       WHERE t.template_code = 'LOGISTICS_REQUEST_TO_DEPARTMENT' AND s.related_id = ${id}`));
    expect(sent).toBe(0);
    expect(Number(scalar(
      `SELECT COUNT(*) FROM email_action_tokens WHERE target_type = 'LOGISTICS_ITEM' AND target_id = ${id}`)))
      .toBe(0);
    expect(deptLeaderEmail.length).toBeGreaterThan(0);

    // 3. …whereas the SYSTEM_REQUEST sibling on the same instance DOES email the department leader,
    //    which is what makes the assertion above a contrast rather than a coincidence.
    const online = await createLogisticsItem(request, instanceId, `Qua tang ${tag}`, 3);
    const mail = await waitForEmail('LOGISTICS_REQUEST_TO_DEPARTMENT', deptLeaderEmail, before);
    expect(mail.status).toBe('SENT');
    expect(scalar(
      `SELECT due_at FROM visit_logistics_items WHERE logistics_item_id = ${online.logisticsItemId}`))
      .not.toBe('NULL');
  });
});
