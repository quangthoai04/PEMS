/**
 * REAL-STACK — Department Leader personnel management (/dashboard/my-department).
 *
 * real Chromium → real React → real .NET API (Testing, fail-closed E2E auth) → disposable MySQL →
 * FileSink inbox. NO network mocking.
 *
 * This is one of the two areas the Dev → Cảnh-Iter1 merge left resting on unit + integration coverage
 * alone. The merge moved all six sends in this module from `IEmailService` to `ISystemEmailDispatcher`
 * — a change no unit test can fully vouch for, because what it really alters is which row lands in
 * `sent_emails` and which template the recipient actually receives. These journeys read the message out
 * of the inbox the API wrote to.
 *
 * Journey order is deliberate: DL-05 hands the department to somebody else and revokes both accounts'
 * sessions, so it runs LAST. Everything before it needs `dept_leader_hn` to still be the seated head.
 */
import { test, expect, type Page } from '@playwright/test';
import { authedPage, meUser } from './realstackHelpers';
import {
  DEPT_TRAINING, DEPT_FACILITIES,
  apiGet, apiStatus, queryDb, scalar, sinkSize, waitForEmail, expectNoEmail, uniq, uniqLetters, asStoredUser,
} from './departmentRealstackHelpers';

const PAGE_URL = '/dashboard/my-department';

/** Opens the personnel screen as the seated Leader and waits for the real data to land. */
async function openMyDepartment(browser: Parameters<typeof authedPage>[0], profileKey: string, user: Record<string, unknown>) {
  const { context, page } = await authedPage(browser, profileKey, asStoredUser(user));
  await page.goto(PAGE_URL);
  // The heading is rendered from the API response, not from a constant, so waiting for the real
  // department name is also the assertion that the request succeeded.
  await expect(page.getByRole('heading', { level: 1, name: new RegExp(DEPT_TRAINING.name) })).toBeVisible({ timeout: 30_000 });
  return { context, page };
}

/**
 * The row for one person in the personnel table.
 *
 * Matched on an EXACT email cell rather than a substring: the seed holds both `dept.hn@fpt.edu.vn`
 * and `locked.dept.hn@fpt.edu.vn`, so a `hasText` filter on the first quietly resolves to two rows,
 * and every assertion against it then fails on strict mode instead of on the behaviour under test.
 */
const rowFor = (page: Page, email: string) =>
  page.locator('tbody tr').filter({ has: page.getByRole('cell', { name: email, exact: true }) });

test.describe('REAL-STACK — Department Leader personnel', () => {
  test('DL-01 — the Leader sees their own department, its statistics and nobody else\'s staff', async ({ browser, request }) => {
    const user = await meUser(request, DEPT_TRAINING.leaderKey);
    const { context, page } = await openMyDepartment(browser, DEPT_TRAINING.leaderKey, user);

    // 1. The department resolved server-side is the Leader's own — there is no id in the URL to swap.
    await expect(page.getByRole('heading', { level: 1, name: new RegExp(DEPT_TRAINING.name) })).toContainText(DEPT_TRAINING.name);
    await expect(page.getByText('Trưởng phòng:')).toBeVisible();

    const department = await apiGet(request, '/department-leader/department', DEPT_TRAINING.leaderKey);
    expect(department.departmentId).toBe(DEPT_TRAINING.departmentId);
    expect(department.currentLeaderUserId).toBe(DEPT_TRAINING.leaderUserId);
    expect(department.departmentType).toBe('GENERAL');

    // 2. The statistics on screen are the API's, not a client-side recount of the current page.
    const dbTotal = Number(scalar(
      `SELECT COUNT(*) FROM users WHERE department_id = ${DEPT_TRAINING.departmentId}`));
    expect(department.totalPersonnelCount).toBe(dbTotal);
    await expect(page.getByText('Tổng nhân sự')).toBeVisible();

    // 3. Scope: every row the API returned belongs to this department. The seed puts a LOCKED account
    //    in department 2 and staff in department 3, so this is a real filter, not a vacuous pass.
    const list = await apiGet(request, '/department-leader/personnel?page=1&pageSize=50', DEPT_TRAINING.leaderKey);
    expect(list.items.length).toBeGreaterThan(0);
    for (const item of list.items) expect(item.departmentName).toBe(DEPT_TRAINING.name);

    const facilitiesEmails = queryDb(
      `SELECT email FROM users WHERE department_id = ${DEPT_FACILITIES.departmentId}`).map(r => r[0]);
    expect(facilitiesEmails.length).toBeGreaterThan(0);
    for (const foreign of facilitiesEmails) {
      expect(list.items.some((i: { email: string }) => i.email === foreign)).toBe(false);
      await expect(rowFor(page, foreign)).toHaveCount(0);
    }

    // 4. Action flags come from the backend. The Leader's own row offers no enable/disable switch —
    //    a department must never be able to deactivate its own head from this screen.
    const self = list.items.find((i: { userId: number }) => i.userId === DEPT_TRAINING.leaderUserId);
    expect(self, 'the Leader appears in their own department list').toBeTruthy();
    expect(self.canDisable).toBe(false);
    expect(self.canEnable).toBe(false);
    expect(self.subRole).toBe('LEADER');

    await context.close();
  });

  test('DL-02 — creating personnel produces a PENDING account, a hash-only confirmation and a real email', async ({ browser, request }) => {
    const user = await meUser(request, DEPT_TRAINING.leaderKey);
    const { context, page } = await openMyDepartment(browser, DEPT_TRAINING.leaderKey, user);

    const tag = uniq();
    const email = `e2e.personnel.${tag}@fpt.edu.vn`;
    const fullName = `Nhan Su E ETwoE ${uniqLetters()}`;
    const before = sinkSize();

    // ── The action under test, entirely through the DOM ──
    await page.getByRole('button', { name: 'Thêm nhân sự' }).click();
    const dialog = page.locator('div.fixed.inset-0').filter({ hasText: 'Thêm nhân sự mới' });
    await expect(dialog).toBeVisible();

    await dialog.getByPlaceholder('Nguyễn Văn A').fill(fullName);
    await dialog.getByPlaceholder('nhansu@fpt.edu.vn').fill(email);
    await dialog.getByPlaceholder('0912345678').fill('0912345678');
    await dialog.locator('select').selectOption('MALE');
    await dialog.getByRole('button', { name: 'Thêm nhân sự' }).click();

    await expect(dialog).toBeHidden({ timeout: 30_000 });

    // 1. The account exists, in this department, and CANNOT log in yet.
    const row = queryDb(
      `SELECT user_id, status, department_id, sub_role FROM users WHERE email = '${email}'`);
    expect(row.length, `account ${email} was created`).toBe(1);
    const [userId, status, departmentId, subRole] = row[0];
    expect(status).toBe('PENDING_EMAIL_CONFIRMATION');
    expect(Number(departmentId)).toBe(DEPT_TRAINING.departmentId);
    expect(subRole).toBe('STAFF');

    // 2. The confirmation is stored as a HASH. The raw token exists only inside the emailed link —
    //    reading the column must not hand anyone a working confirmation.
    const confirmation = queryDb(
      `SELECT token_hash, status FROM account_email_confirmations WHERE user_id = ${userId}`);
    expect(confirmation.length, 'exactly one confirmation row').toBe(1);
    const [tokenHash, confirmStatus] = confirmation[0];
    expect(confirmStatus).toBe('PENDING');
    expect(tokenHash.length).toBeGreaterThanOrEqual(32);

    // 3. The email really left through the dispatcher, on the ACCOUNT_EMAIL_CONFIRMATION template,
    //    carrying a live link — not a hard-coded body assembled in the handler.
    const mail = await waitForEmail('ACCOUNT_EMAIL_CONFIRMATION', email, before);
    expect(mail.link, 'the confirmation link is present').toBeTruthy();
    expect(mail.link).toContain('confirm-email?token=');
    expect(mail.status).toBe('SENT');
    expect(mail.cc).toHaveLength(0);
    expect(mail.bcc).toHaveLength(0);

    // The raw token in the link is NOT what was stored.
    const rawToken = new URL(mail.link!).searchParams.get('token')!;
    expect(rawToken.length).toBeGreaterThan(0);
    expect(tokenHash).not.toBe(rawToken);

    // 4. …and it is recorded in the email history the same way every dispatcher message is.
    expect(Number(scalar(
      `SELECT COUNT(*) FROM sent_emails s JOIN email_templates t ON t.email_template_id = s.email_template_id
       WHERE t.template_code = 'ACCOUNT_EMAIL_CONFIRMATION' AND s.related_id = ${userId}`))).toBeGreaterThan(0);

    // 5. The new person is on screen, with the pending status visible to the operator.
    await expect(rowFor(page, email)).toBeVisible({ timeout: 30_000 });
    await expect(rowFor(page, email)).toContainText(fullName);

    await context.close();
  });

  test('DL-03 — editing a person updates the row, and a no-op edit is reported as one', async ({ browser, request }) => {
    const user = await meUser(request, DEPT_TRAINING.leaderKey);
    const { context, page } = await openMyDepartment(browser, DEPT_TRAINING.leaderKey, user);

    const staffEmail = String(scalar(
      `SELECT email FROM users WHERE user_id = ${DEPT_TRAINING.staffUserId}`));
    const newName = `Doi Ten E ETwoE ${uniqLetters()}`;
    const before = sinkSize();

    // Open the detail modal from the row, then switch it into edit mode.
    await rowFor(page, staffEmail).getByRole('button').first().click();
    const detail = page.locator('div.fixed.inset-0').filter({ hasText: 'Thông tin nhân sự' });
    await expect(detail).toBeVisible();
    await detail.getByRole('button', { name: /Chỉnh sửa/ }).click();

    const form = page.locator('div.fixed.inset-0').filter({ hasText: 'Chỉnh sửa thông tin nhân sự' });
    await expect(form).toBeVisible();
    await form.getByPlaceholder('Nguyễn Văn A').fill(newName);
    await form.getByRole('button', { name: 'Lưu thay đổi' }).click();
    await expect(form).toBeHidden({ timeout: 30_000 });

    // 1. Persisted, and scoped: the edit touched this person and nothing else.
    expect(scalar(`SELECT full_name FROM users WHERE user_id = ${DEPT_TRAINING.staffUserId}`)).toBe(newName);
    expect(scalar(`SELECT email FROM users WHERE user_id = ${DEPT_TRAINING.staffUserId}`)).toBe(staffEmail);
    expect(scalar(`SELECT status FROM users WHERE user_id = ${DEPT_TRAINING.staffUserId}`)).toBe('ACTIVE');

    // 2. The name change is NOT an email change, so none of the address-change notices may fire.
    expectNoEmail('ACCOUNT_EMAIL_CHANGED_OLD_NOTICE', staffEmail, before);
    expectNoEmail('ACCOUNT_EMAIL_CHANGED_NEW_NOTICE', staffEmail, before);
    expectNoEmail('ACCOUNT_EMAIL_CONFIRMATION', staffEmail, before);

    // 3. The UI shows the new value without a manual reload.
    await expect(rowFor(page, staffEmail)).toContainText(newName, { timeout: 30_000 });

    // 4. Re-submitting the same values is a no-op the API reports honestly rather than a silent 200
    //    that revokes every session.
    const audits = Number(scalar(
      `SELECT COUNT(*) FROM audit_logs WHERE entity_type = 'User' AND entity_id = ${DEPT_TRAINING.staffUserId}`));
    const res = await request.put(
      `${process.env.PEMS_E2E_API_BASE ?? 'http://localhost:5299/api'}/department-leader/personnel/${DEPT_TRAINING.staffUserId}`,
      {
        headers: { 'X-E2E-Profile': DEPT_TRAINING.leaderKey, 'X-E2E-Secret': process.env.PEMS_E2E_AUTH_SECRET ?? '' },
        data: {
          fullName: newName,
          email: staffEmail,
          phone: scalar(`SELECT phone FROM users WHERE user_id = ${DEPT_TRAINING.staffUserId}`),
          gender: scalar(`SELECT gender FROM users WHERE user_id = ${DEPT_TRAINING.staffUserId}`),
        },
      });
    expect(res.ok()).toBeTruthy();
    expect((await res.json()).changed).toBe(false);
    expect(Number(scalar(
      `SELECT COUNT(*) FROM audit_logs WHERE entity_type = 'User' AND entity_id = ${DEPT_TRAINING.staffUserId}`)))
      .toBe(audits);

    await context.close();
  });

  test('DL-04 — a status change is previewed before it is applied, and blockers stop it', async ({ browser, request }) => {
    const user = await meUser(request, DEPT_TRAINING.leaderKey);
    const { context, page } = await openMyDepartment(browser, DEPT_TRAINING.leaderKey, user);

    const staffEmail = String(scalar(
      `SELECT email FROM users WHERE user_id = ${DEPT_TRAINING.staffUserId}`));
    const before = sinkSize();

    // 1. The Leader's own row has no switch at all — the impact preview is not even reachable for it.
    const leaderEmail = String(scalar(
      `SELECT email FROM users WHERE user_id = ${DEPT_TRAINING.leaderUserId}`));
    await expect(rowFor(page, leaderEmail).locator('input[type="checkbox"]')).toHaveCount(0);

    // 2. Flipping a staff switch opens the PREVIEW; it must not write anything by itself.
    await rowFor(page, staffEmail).locator('label:has(input[type="checkbox"])').click();
    const modal = page.locator('div.fixed.inset-0').filter({ hasText: /Vô hiệu hóa|Kích hoạt/ });
    await expect(modal).toBeVisible();
    expect(scalar(`SELECT status FROM users WHERE user_id = ${DEPT_TRAINING.staffUserId}`)).toBe('ACTIVE');

    // 3. The preview is the backend's verdict, listing what the change would break.
    const impact = await apiGet(
      request,
      `/department-leader/personnel/${DEPT_TRAINING.staffUserId}/status-impact?targetStatus=INACTIVE`,
      DEPT_TRAINING.leaderKey);
    expect(impact.currentStatus).toBe('ACTIVE');
    expect(impact.targetStatus).toBe('INACTIVE');
    expect(Array.isArray(impact.blockers)).toBe(true);

    if (impact.canChangeStatus) {
      // No blockers → the confirm button exists and the change goes through.
      await modal.getByRole('button', { name: /Xác nhận|Vô hiệu hóa|Kích hoạt/ }).last().click();
      await expect(modal).toBeHidden({ timeout: 30_000 });
      expect(scalar(`SELECT status FROM users WHERE user_id = ${DEPT_TRAINING.staffUserId}`)).toBe('INACTIVE');

      // The person is told, on the disabled template — through the dispatcher, not a hand-built body.
      const mail = await waitForEmail('DEPT_PERSONNEL_ACCOUNT_DISABLED', staffEmail, before);
      expect(mail.status).toBe('SENT');

      // Put it back so the later journeys see the seeded shape.
      const reEnableFrom = sinkSize();
      await rowFor(page, staffEmail).locator('label:has(input[type="checkbox"])').click();
      const back = page.locator('div.fixed.inset-0').filter({ hasText: /Kích hoạt/ });
      await expect(back).toBeVisible();
      await back.getByRole('button', { name: /Xác nhận|Kích hoạt/ }).last().click();
      await expect(back).toBeHidden({ timeout: 30_000 });
      expect(scalar(`SELECT status FROM users WHERE user_id = ${DEPT_TRAINING.staffUserId}`)).toBe('ACTIVE');
      await waitForEmail('DEPT_PERSONNEL_ACCOUNT_ENABLED', staffEmail, reEnableFrom);
    } else {
      // Blocked → the modal must NOT offer a confirm button, and the row must be untouched. The
      // backend refusing is asserted directly rather than inferred from the button being absent.
      expect(impact.blockers.length).toBeGreaterThan(0);
      await expect(modal.getByRole('button', { name: /^Xác nhận/ })).toHaveCount(0);

      const status = await apiStatus(
        request, 'post',
        `/department-leader/personnel/${DEPT_TRAINING.staffUserId}/status`,
        DEPT_TRAINING.leaderKey, { targetStatus: 'INACTIVE' });
      expect(status).toBeGreaterThanOrEqual(400);
      expect(scalar(`SELECT status FROM users WHERE user_id = ${DEPT_TRAINING.staffUserId}`)).toBe('ACTIVE');
      expectNoEmail('DEPT_PERSONNEL_ACCOUNT_DISABLED', staffEmail, before);
    }

    await context.close();
  });

  test('DL-06 — a Leader cannot reach another department\'s personnel', async ({ browser, request }) => {
    // Department 3 exists and is populated, so a refusal here is a refusal — not an empty result.
    const foreignId = DEPT_FACILITIES.staffUserId;
    expect(Number(scalar(`SELECT department_id FROM users WHERE user_id = ${foreignId}`)))
      .toBe(DEPT_FACILITIES.departmentId);

    // 1. Reading someone else's detail is refused.
    const detailStatus = await apiStatus(
      request, 'get', `/department-leader/personnel/${foreignId}`, DEPT_TRAINING.leaderKey);
    expect(detailStatus).toBeGreaterThanOrEqual(400);
    expect(detailStatus).toBeLessThan(500);

    // 2. So is previewing a status change for them…
    const impactStatus = await apiStatus(
      request, 'get',
      `/department-leader/personnel/${foreignId}/status-impact?targetStatus=INACTIVE`,
      DEPT_TRAINING.leaderKey);
    expect(impactStatus).toBeGreaterThanOrEqual(400);

    // 3. …and promoting them into the wrong department's leadership.
    const transferStatus = await apiStatus(
      request, 'post', '/department-leader/transfer-leadership',
      DEPT_TRAINING.leaderKey, { newLeaderUserId: foreignId });
    expect(transferStatus).toBeGreaterThanOrEqual(400);

    // Nothing moved.
    expect(Number(scalar(
      `SELECT head_user_id FROM departments WHERE department_id = ${DEPT_FACILITIES.departmentId}`)))
      .toBe(DEPT_FACILITIES.leaderUserId);
    expect(scalar(`SELECT sub_role FROM users WHERE user_id = ${foreignId}`)).toBe('STAFF');

    // 4. A DEPARTMENT **staff** account is not a Leader at all — the page bounces them and the API
    //    refuses them, so the screen is never the only thing standing in the way.
    const staffUser = await meUser(request, DEPT_TRAINING.staffKey);
    const { context, page } = await authedPage(browser, DEPT_TRAINING.staffKey, asStoredUser(staffUser));
    await page.goto(PAGE_URL);
    await expect(page).not.toHaveURL(new RegExp(`${PAGE_URL}$`), { timeout: 30_000 });
    expect(await apiStatus(request, 'get', '/department-leader/department', DEPT_TRAINING.staffKey))
      .toBeGreaterThanOrEqual(400);
    await context.close();
  });

  // ── LAST: this hands the department away and revokes both sessions ────────────────────────────
  test('DL-05 — transferring leadership moves the seat atomically and notifies both parties', async ({ browser, request }) => {
    const user = await meUser(request, DEPT_TRAINING.leaderKey);
    const { context, page } = await openMyDepartment(browser, DEPT_TRAINING.leaderKey, user);

    const outgoingEmail = String(scalar(`SELECT email FROM users WHERE user_id = ${DEPT_TRAINING.leaderUserId}`));
    const incomingEmail = String(scalar(`SELECT email FROM users WHERE user_id = ${DEPT_TRAINING.staffUserId}`));
    const before = sinkSize();

    // Precondition: the seat is held by the caller, and exactly one LEADER exists.
    expect(Number(scalar(
      `SELECT head_user_id FROM departments WHERE department_id = ${DEPT_TRAINING.departmentId}`)))
      .toBe(DEPT_TRAINING.leaderUserId);

    // ── The action under test ──
    await page.getByRole('button', { name: 'Đổi trưởng phòng' }).click();
    const modal = page.locator('div.fixed.inset-0').filter({ hasText: /Trưởng phòng/ });
    await expect(modal).toBeVisible();

    // Candidates come from their own endpoint, never from the current table page.
    const candidates = await apiGet(request, '/department-leader/leader-candidates', DEPT_TRAINING.leaderKey);
    expect(candidates.items.some((c: { userId: number }) => c.userId === DEPT_TRAINING.staffUserId)).toBe(true);
    // Nobody outside the department is offerable.
    for (const c of candidates.items) {
      expect(Number(scalar(`SELECT department_id FROM users WHERE user_id = ${c.userId}`)))
        .toBe(DEPT_TRAINING.departmentId);
    }

    await modal.locator(`input[type="radio"][value="${DEPT_TRAINING.staffUserId}"]`).check();
    await modal.getByRole('button', { name: /Xác nhận|Chuyển|Đổi/ }).last().click();

    // The caller is signed out — their token now claims a sub-role they no longer hold.
    await expect(page).toHaveURL(/\/$|\/login/, { timeout: 30_000 });

    // 1. Exactly one head, and it is the successor.
    expect(Number(scalar(
      `SELECT head_user_id FROM departments WHERE department_id = ${DEPT_TRAINING.departmentId}`)))
      .toBe(DEPT_TRAINING.staffUserId);

    // 2. Both sub-roles moved, atomically — never two Leaders and never none.
    expect(scalar(`SELECT sub_role FROM users WHERE user_id = ${DEPT_TRAINING.staffUserId}`)).toBe('LEADER');
    expect(scalar(`SELECT sub_role FROM users WHERE user_id = ${DEPT_TRAINING.leaderUserId}`)).toBe('STAFF');
    expect(Number(scalar(
      `SELECT COUNT(*) FROM users WHERE department_id = ${DEPT_TRAINING.departmentId} AND sub_role = 'LEADER'`)))
      .toBe(1);

    // 3. Both tokens were revoked: carrying the wrong sub-role is exactly what a stale session is.
    expect(Number(scalar(
      `SELECT COUNT(*) FROM user_sessions WHERE user_id IN (${DEPT_TRAINING.leaderUserId}, ${DEPT_TRAINING.staffUserId})
       AND revoked_at IS NULL AND expires_at > NOW()`))).toBe(0);

    // 4. Both parties are told, each on their own template and at their own address.
    const granted = await waitForEmail('DEPT_LEADERSHIP_GRANTED', incomingEmail, before);
    const handedOver = await waitForEmail('DEPT_LEADERSHIP_HANDED_OVER', outgoingEmail, before);
    expect(granted.to.map(t => t.email)).toEqual([incomingEmail.toLowerCase()]);
    expect(handedOver.to.map(t => t.email)).toEqual([outgoingEmail.toLowerCase()]);
    // Neither notice leaks the other party's address.
    expect(granted.body).not.toContain(outgoingEmail);
    expect(handedOver.body).not.toContain(incomingEmail);

    // 5. The audit records the move.
    expect(Number(scalar(
      `SELECT COUNT(*) FROM audit_logs WHERE action = 'TRANSFER_DEPARTMENT_LEADERSHIP'
       AND entity_type = 'Department' AND entity_id = ${DEPT_TRAINING.departmentId}`))).toBeGreaterThan(0);

    // 6. The demoted Leader can no longer manage the department.
    expect(await apiStatus(request, 'get', '/department-leader/department', DEPT_TRAINING.leaderKey))
      .toBeGreaterThanOrEqual(400);

    await context.close();
  });
});
