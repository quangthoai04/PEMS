/**
 * VisitProcess "Chuẩn bị đoàn khách" — capability split + logistics time defaults.
 *
 * Covers (network fully mocked, real UI):
 *   1. A department with canInviteParticipant=false / canReceiveLogistics=true is DISABLED in the
 *      participant-invitation dropdown (reason shown) but still SELECTABLE in the logistics picker.
 *   2. A new system logistics form pre-fills usage start/end with the campus instance's
 *      plannedStartAt/plannedEndAt — as Vietnam wall-clock, independent of the browser timezone
 *      (the whole suite runs under America/New_York).
 *   3. The default is editable and survives unrelated re-renders (typing other fields).
 *   4. An EXISTING logistics item shows its own saved usage time, not the planned window.
 *   5. Resetting an unsaved form (preview → send flow) returns to the planned defaults.
 *   6. IC_SUPPORT candidates render the Staff Leader / Staff labels from subRole.
 */
import { test, expect, type Page } from '@playwright/test';

// Prove Vietnam wall-clock independence: everything runs in a non-VN browser timezone.
test.use({ timezoneId: 'America/New_York' });

// The dashboard shell is a heavy dev-server page; give slow machines room.
test.setTimeout(120_000);

const AUTH_USER = {
  userId: '100',
  fullName: 'Host Test',
  email: 'host@test.local',
  roleCode: 'STAFF',
  subRole: 'STAFF',
  primaryCampusId: '1',
  campusCode: 'C1',
  campusName: 'Campus 1',
  departmentId: '900',
  departmentName: 'Phòng IC',
  mustChangePassword: false,
  mustSetPassword: false,
  effectiveRole: 'STAFF',
  status: 'ACTIVE',
};

const PERM = {
  visitInstanceId: 10,
  visitRequestId: 10,
  requestStatus: 'APPROVED',
  instanceStatus: 'BEFORE_VISIT',
  relation: 'HOST',
  hostAssigned: true,
  canViewOriginalRequest: true,
  canViewOverview: true,
  canViewBeforeVisit: true,
  canEditBeforeVisit: true,
  canViewDuringVisit: true,
  canEditDuringVisit: true,
  canViewAfterVisit: true,
  canEditAfterVisit: true,
  canAssignHost: false,
  canViewMinutes: true,
  canCreateMinutes: true,
  canEditMinutes: true,
  canViewNews: true,
  canCreateNews: true,
  canStartVisit: true,
  canCompleteVisit: false,
  canCloseVisit: false,
};

// Planned window of THIS campus instance — the expected datetime-local defaults are
// 2026-08-01T09:00 / 2026-08-01T11:00 (Vietnam wall-clock, +07:00 carried by the API).
const DETAIL = {
  visitRequestId: 10,
  visitInstanceId: 10,
  delegationName: 'Đoàn kiểm thử',
  instanceStatus: 'BEFORE_VISIT',
  plannedStartAt: '2026-08-01T09:00:00+07:00',
  plannedEndAt: '2026-08-01T11:00:00+07:00',
  campusName: 'Campus 1',
  hostUserId: 100,
  hostName: 'Host Test',
  relation: 'HOST',
  canEditBefore: true,
  preparationNote: null,
  agenda: [],
  requestSummary: {
    registrantName: 'Nguyễn Văn Khách',
    delegationName: 'Đoàn kiểm thử',
    visitScope: 'SINGLE_CAMPUS',
    campuses: [{
      visitInstanceId: 10, campusId: 1, campusName: 'Campus 1',
      plannedStartAt: '2026-08-01T09:00:00+07:00', plannedEndAt: '2026-08-01T11:00:00+07:00', isCurrent: true,
    }],
    guestMembers: [],
    externalSupportMembers: [],
  },
  host: { userId: 100, fullName: 'Host Test', email: 'host@test.local', statusLabel: 'Đã được phân công' },
  participants: [],
  notifications: [],
  publicNews: [],
};

// Leader already an active participant → invitation blocked, logistics still available.
const DEPARTMENTS = [{
  departmentId: 20,
  departmentName: 'Phòng Hành chính Test',
  campusId: 1,
  campusName: 'Campus 1',
  leaderUserId: 200,
  leaderName: 'Trưởng phòng A',
  leaderEmail: 'leader@test.local',
  canInviteParticipant: false,
  participantDisabledReason: 'Trưởng phòng này đã có trong danh sách tham gia của đoàn.',
  canReceiveLogistics: true,
  logisticsDisabledReason: null,
}];

const IC_CANDIDATES = [
  {
    userId: 101, fullName: 'Nguyễn Văn Staff', email: 'staff@test.local', roleCode: 'STAFF', subRole: 'STAFF',
    departmentId: 900, departmentName: 'Phòng IC', campusId: 1, campusName: 'Campus 1',
    conflictCount: 0, hasPrivateConflict: false, conflictSummary: null, canInvite: true, disabledReason: null,
  },
  {
    userId: 102, fullName: 'Trần Thị Leader', email: 'leader.ic@test.local', roleCode: 'STAFF', subRole: 'LEADER',
    departmentId: 900, departmentName: 'Phòng IC', campusId: 1, campusName: 'Campus 1',
    conflictCount: 0, hasPrivateConflict: false, conflictSummary: null, canInvite: true, disabledReason: null,
  },
];

const SAVED_LED_ITEM = {
  logisticsItemId: 77,
  itemType: 'LED',
  title: 'Welcome LED',
  description: 'đã lưu',
  quantity: 2,
  status: 'REQUESTED',
  coordinationMode: 'SYSTEM_REQUEST',
  requestedToDepartmentId: 20,
  departmentName: 'Phòng Hành chính Test',
  // Saved times differ from the planned window on purpose (AC-06).
  usageStartAt: '2026-08-02T13:00:00+07:00',
  usageEndAt: '2026-08-02T15:00:00+07:00',
};

async function mockApp(page: Page, opts?: { logisticsItems?: unknown[] }) {
  // Kill external requests (fonts/CDNs) fast — a hanging blocked fetch can stall first paint.
  await page.route(
    (url) => !url.host.startsWith('localhost') && !url.host.startsWith('127.0.0.1'),
    (route) => route.abort());
  // Catch-all API mock (lowest priority — later, more specific routes win in Playwright).
  // Predicate on the pathname so Vite dev-server module URLs containing "/api/" are NOT hijacked.
  await page.route((url) => url.pathname.startsWith('/api/'), (route) => route.fulfill({ json: {} }));
  await page.route('**/api/auth/me', (route) => route.fulfill({ json: { user: AUTH_USER } }));
  // The dashboard shell polls these; an {} answer leaves undefined arrays behind and can crash
  // the whole tree through the error boundary — answer with the real empty shapes.
  await page.route('**/api/notifications/unread-count', (route) => route.fulfill({ json: { unreadCount: 0 } }));
  await page.route('**/api/notifications?*', (route) => route.fulfill({ json: { items: [], totalCount: 0 } }));
  await page.route('**/api/feedbacks/my-pending', (route) => route.fulfill({ json: { items: [] } }));
  await page.route('**/api/delegations/visit-instances/10/process-permissions', (route) =>
    route.fulfill({ json: PERM }));
  await page.route('**/api/delegations/10/campuses/10/process-detail', (route) =>
    route.fulfill({ json: DETAIL }));
  await page.route('**/api/delegations/visit-instances/10/agenda-responsible-candidates*', (route) =>
    route.fulfill({ json: [] }));
  await page.route('**/api/agenda-templates/for-instance/10', (route) =>
    route.fulfill({
      json: {
        visitInstanceId: 10, visitRequestId: 10, campusId: 1, visitType: 'CAMPUS_TOUR',
        plannedStartAt: '2026-08-01T09:00:00+07:00', plannedEndAt: '2026-08-01T11:00:00+07:00',
        relation: 'HOST', canApply: true, defaultTemplateId: null, defaultScope: null,
        selectableTemplates: [],
      },
    }));
  await page.route('**/api/delegations/visit-instances/10/reminder-settings*', (route) =>
    route.fulfill({ json: { items: [] } }));
  await page.route('**/api/delegations/visit-instances/10/logistics', (route) =>
    route.fulfill({ json: { items: opts?.logisticsItems ?? [] } }));
  await page.route('**/api/delegations/visit-instances/10/support-departments*', (route) =>
    route.fulfill({ json: DEPARTMENTS }));
  await page.route('**/api/delegations/visit-instances/10/participant-candidates*', (route) =>
    route.fulfill({ json: IC_CANDIDATES }));
}

async function gotoVisitProcess(page: Page) {
  await page.addInitScript((user) => {
    window.localStorage.setItem('token', 'test-access-token');
    window.localStorage.setItem('refreshToken', 'test-refresh-token');
    window.localStorage.setItem('pems_user', JSON.stringify(user));
    window.localStorage.setItem('pems_loginPortal', 'INTERNAL');
    window.localStorage.setItem('pems_selectedCampusId', '1');
  }, AUTH_USER);
  await page.goto('/dashboard/visit/process/10', { waitUntil: 'domcontentloaded' });
  // Generous timeout: the vite dev-server transform + dashboard shell is slow on loaded machines.
  await expect(page.getByText('2. Chuẩn bị chi tiết')).toBeVisible({ timeout: 90_000 });
}

/** Opens "3 Thiết lập & Điều phối sự kiện (Set up)" — the accordion hosting the invite panels. */
async function openParticipantsSection(page: Page) {
  await page.getByText('Thiết lập & Điều phối sự kiện (Set up)').click();
  await expect(page.getByText('2. Thành phần tham gia')).toBeVisible();
}

/** Expands "2. Chuẩn bị chi tiết" and opens the Welcome LED system-request form. */
async function openLedSystemForm(page: Page) {
  await page.getByText('2. Chuẩn bị chi tiết').click();
  await expect(page.getByText('Mục 1: Welcome LED')).toBeVisible();
  await page.getByText('Cần Welcome LED — gửi yêu cầu qua hệ thống').click();
}

/** The Welcome LED create-form card. "Mục 4: Khác" renders a second identical form, so the LED
 * card is pinned by its unique note placeholder; .last() then picks the DEEPEST div containing
 * both that textarea and the datetime inputs — the card container itself. */
function ledCard(page: Page) {
  return page.locator('div')
    .filter({ has: page.getByPlaceholder('Kích thước, nội dung hiển thị, đã gửi ảnh thiết kế...') })
    .filter({ has: page.locator('input[type="datetime-local"]') })
    .last();
}

// One page load covers: capability split (invite disabled ↔ logistics selectable), the IC
// candidate labels, the planned-time defaults (timezone-independent) and default editability.
// Consolidated on purpose — each dashboard page load is expensive on this machine.
test.describe('capability split + time defaults (one page)', () => {
  test('dept invitation/logistics split, candidate labels, planned-time defaults', async ({ page }) => {
    await mockApp(page);
    await gotoVisitProcess(page);
    await openParticipantsSection(page);

    // ── IC candidates: Staff Leader vs Staff labels (from subRole; role stays IC_SUPPORT). ──
    const icPanel = page.locator('div', { has: page.getByRole('heading', { name: 'Staff hỗ trợ IC' }) }).last();
    await icPanel.getByPlaceholder('Tìm theo tên / email...').click();
    await expect(page.getByText('Trần Thị Leader')).toBeVisible();
    await expect(page.getByText('Staff Leader hỗ trợ IC')).toBeVisible();
    await expect(page.getByText('Nguyễn Văn Staff')).toBeVisible();
    await expect(page.getByText('Staff hỗ trợ IC', { exact: true }).first()).toBeVisible();
    await page.keyboard.press('Escape');

    // ── Invitation panel: the department must be disabled with the participant reason. ──
    const invitePanel = page.locator('div', { has: page.getByRole('heading', { name: 'Phòng ban hỗ trợ' }) }).last();
    await invitePanel.getByPlaceholder('Tìm phòng ban (GENERAL) cùng cơ sở...').click();
    await expect(page.getByText('Phòng Hành chính Test')).toBeVisible();
    await expect(page.getByText('Trưởng phòng này đã có trong danh sách tham gia của đoàn.')).toBeVisible();
    await expect(page.getByRole('button', { name: 'Mời', exact: true })).toBeDisabled();
    await page.keyboard.press('Escape');

    // ── Logistics form: planned-time defaults (browser TZ = America/New_York, values stay VN). ──
    await openLedSystemForm(page);
    const card = ledCard(page);
    const inputs = card.locator('input[type="datetime-local"]');
    await expect(inputs.nth(0)).toHaveValue('2026-08-01T09:00');
    await expect(inputs.nth(1)).toHaveValue('2026-08-01T11:00');

    // ── Default is editable and the edit survives unrelated re-renders. ──
    const start = inputs.nth(0);
    await start.fill('2026-08-01T07:30');
    await card.getByPlaceholder('Kích thước, nội dung hiển thị, đã gửi ảnh thiết kế...').fill('ghi chú setup sớm');
    await expect(start).toHaveValue('2026-08-01T07:30');

    // ── Logistics picker: the SAME blocked-for-invitation department is selectable. ──
    await card.getByPlaceholder('Tìm phòng ban (GENERAL) cùng cơ sở...').click();
    const deptRow = page.locator('div', { hasText: 'Phòng Hành chính Test' }).filter({ has: page.getByRole('button', { name: 'Chọn' }) }).last();
    await expect(deptRow.getByRole('button', { name: 'Chọn' })).toBeVisible();
    await deptRow.click();

    // Selected panel appears with the leader + an enabled "Gửi yêu cầu" button.
    await expect(card.getByText('Trưởng phòng:')).toBeVisible();
    await expect(card.getByRole('button', { name: 'Gửi yêu cầu' })).toBeEnabled();
  });
});

test.describe('logistics usage-time — saved item & reset', () => {
  test('an existing logistics item shows ITS saved usage time, not the planned window', async ({ page }) => {
    await mockApp(page, { logisticsItems: [SAVED_LED_ITEM] });
    await gotoVisitProcess(page);
    await page.getByText('2. Chuẩn bị chi tiết').click();
    await expect(page.getByText('Mục 1: Welcome LED')).toBeVisible();

    // The saved item renders the form summary directly (no radio choice) with its own times.
    const card = ledCard(page);
    const inputs = card.locator('input[type="datetime-local"]');
    await expect(inputs.nth(0)).toHaveValue('2026-08-02T13:00');
    await expect(inputs.nth(1)).toHaveValue('2026-08-02T15:00');
    await expect(inputs.nth(0)).toBeDisabled(); // saved card is read-only
  });

  test('reset after preview-send returns the unsaved form to the planned defaults', async ({ page }) => {
    await mockApp(page);
    await page.route('**/api/email-templates/preview', (route) => route.fulfill({
      json: {
        templateCode: 'LOGISTICS_REQUEST_TO_DEPARTMENT',
        subject: '[PEMS] Yêu cầu hậu cần',
        bodyHtml: '<p>Nội dung email test</p>',
        editableBodyText: 'Nội dung email test',
        isActionTemplate: false,
        systemActionDescription: null,
        lockedActionBlockHtml: null,
        requiredActionPlaceholders: [],
        editable: true,
        bodyFormat: 'HTML',
      },
    }));
    await page.route('**/api/delegations/preparevisitlogistics', (route) => route.fulfill({
      json: {
        success: true, businessCreated: true, logisticsItemId: 88,
        emailStatus: 'SENT', sentEmailId: 1, message: 'Đã gửi yêu cầu hậu cần.',
      },
    }));
    await gotoVisitProcess(page);
    await openLedSystemForm(page);

    const card = ledCard(page);
    const start = card.locator('input[type="datetime-local"]').nth(0);
    // Fill the required fields, edit the default, then send via the preview modal.
    await card.getByPlaceholder(/VD: 2/).first().fill('1');
    await start.fill('2026-08-01T07:30');
    await card.getByPlaceholder('Tìm phòng ban (GENERAL) cùng cơ sở...').click();
    await page.locator('div', { hasText: 'Phòng Hành chính Test' })
      .filter({ has: page.getByRole('button', { name: 'Chọn' }) }).last().click();
    await card.getByTitle('Xem trước & sửa email').click();
    await page.getByRole('button', { name: 'Gửi với nội dung này' }).click();

    // onReset fires after the send: the (still unsaved on this mock) form returns to the defaults.
    await expect(start).toHaveValue('2026-08-01T09:00');
  });
});
