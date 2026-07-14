/**
 * UC-86 force-logout (doc 08 §12/§19) — real UI, mocked network:
 *   TC-01 an authenticated API answering 403 CAMPUS_INACTIVE_ACCESS_DENIED clears ALL auth
 *         state, redirects to /login and shows the campus-inactive message once.
 *   TC-02 a 403 with a DIFFERENT error code never triggers the forced logout.
 */
import { test, expect, type Page } from '@playwright/test';

const HO_AUTH_USER = {
  userId: '900',
  fullName: 'HO Tester',
  email: 'ho@test.local',
  roleCode: 'HO',
  subRole: null,
  primaryCampusId: null,
  campusCode: null,
  campusName: null,
  departmentId: null,
  mustChangePassword: false,
  mustSetPassword: false,
  effectiveRole: 'HO',
  status: 'ACTIVE',
};

const CAMPUS_INACTIVE_BODY = {
  success: false,
  errorCode: 'CAMPUS_INACTIVE_ACCESS_DENIED',
  message: 'Cơ sở của tài khoản hiện đã ngừng hoạt động. Vui lòng liên hệ Head Office để được hỗ trợ.',
};

async function seedAuth(page: Page) {
  await page.addInitScript(({ user }) => {
    window.localStorage.setItem('pems.language', 'vi');
    window.localStorage.setItem('token', 'test-access-token');
    window.localStorage.setItem('refreshToken', 'test-refresh-token');
    window.localStorage.setItem('pems_user', JSON.stringify(user));
    window.localStorage.setItem('currentUser', JSON.stringify({
      userId: user.userId, name: user.fullName, email: user.email, role: user.roleCode, campus: '',
    }));
  }, { user: HO_AUTH_USER });
}

async function mockBaseApis(page: Page) {
  // Catch-all by pathname so vite module URLs (/src/**/api/*.ts) are never intercepted.
  await page.route((url) => url.pathname.startsWith('/api/'), (route) => route.fulfill({ json: {} }));
  await page.route('**/api/auth/me', (route) => route.fulfill({ json: { user: HO_AUTH_USER } }));
  await page.route('**/api/campuses/filter-options', (route) =>
    route.fulfill({ json: { cities: [], campuses: [], statuses: [] } }));
}

test.describe('UC-86 campus forced logout', () => {
  test('TC-01: 403 CAMPUS_INACTIVE_ACCESS_DENIED → auth cleared, redirected to /login with the campus message', async ({ page }) => {
    await mockBaseApis(page);
    await page.route('**/api/campuses/viewcampuslist**', (route) =>
      route.fulfill({ status: 403, json: CAMPUS_INACTIVE_BODY }));
    await seedAuth(page);

    await page.goto('/dashboard/campus');

    // Forced logout: back on the login page with the explanation banner.
    await page.waitForURL('**/login', { timeout: 15_000 });
    await expect(page.getByRole('alert')).toContainText('Cơ sở của tài khoản hiện đã ngừng hoạt động');

    // Auth state fully cleared (access + refresh + user snapshots).
    const cleared = await page.evaluate(() => ({
      token: localStorage.getItem('token'),
      refreshToken: localStorage.getItem('refreshToken'),
      user: localStorage.getItem('pems_user'),
      legacyUser: localStorage.getItem('currentUser'),
      reasonKeyConsumed: sessionStorage.getItem('pems.forcedLogoutReason'),
    }));
    expect(cleared.token).toBeNull();
    expect(cleared.refreshToken).toBeNull();
    expect(cleared.user).toBeNull();
    expect(cleared.legacyUser).toBeNull();
    // The reason is shown once and consumed — a later visit shows no stale banner.
    expect(cleared.reasonKeyConsumed).toBeNull();

    // No stale banner on a later visit: the reason key was consumed above, so reloading never
    // re-shows the campus message. (addInitScript re-seeds auth on reload, so the app may
    // navigate away from /login — the assertion is only about the one-shot banner.)
    await page.reload();
    await page.waitForLoadState('domcontentloaded');
    await expect(page.getByRole('alert')).toHaveCount(0);
  });

  test('TC-02: a 403 with a different error code never force-logs-out', async ({ page }) => {
    await mockBaseApis(page);
    await page.route('**/api/campuses/viewcampuslist**', (route) =>
      route.fulfill({
        status: 403,
        json: { success: false, errorCode: 'CAMPUS_MANAGEMENT_FORBIDDEN', message: 'Bạn không có quyền xem danh sách campus.' },
      }));
    await seedAuth(page);

    await page.goto('/dashboard/campus');
    await expect(page.getByRole('heading', { name: 'Quản lý campus' })).toBeVisible();

    // Give the interceptor a moment, then verify we are STILL on the dashboard with auth intact.
    await page.waitForTimeout(1500);
    await expect(page).toHaveURL(/\/dashboard\/campus/);
    expect(await page.evaluate(() => localStorage.getItem('token'))).toBe('test-access-token');
  });
});
