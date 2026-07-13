import { test, expect } from '@playwright/test';

test.describe('Staff Leader Attending Tab Regression', () => {
  test('Staff Leader can see and use Attending Tab, and pending banner', async ({ page }) => {
    const AUTH_USER = {
      userId: '101',
      fullName: 'Test Staff Leader',
      roleCode: 'STAFF',
      subRole: 'LEADER',
      departmentId: '900',
      status: 'ACTIVE'
    };

    await page.addInitScript((user) => {
      window.localStorage.setItem('token', 'test-access-token');
      window.localStorage.setItem('refreshToken', 'test-refresh-token');
      window.localStorage.setItem('pems_user', JSON.stringify(user));
      window.localStorage.setItem('pems_loginPortal', 'INTERNAL');
      window.localStorage.setItem('pems_selectedCampusId', '1');
    }, AUTH_USER);

    // Kill external requests
    await page.route(
      (url) => !url.host.startsWith('localhost') && !url.host.startsWith('127.0.0.1'),
      (route) => route.abort()
    );

    await page.route((url) => url.pathname.startsWith('/api/'), (route) => route.fulfill({ json: {} }));
    
    // Mock user context as STAFF LEADER
    await page.route('**/api/auth/me', async route => {
      await route.fulfill({
        json: { user: AUTH_USER }
      });
    });

    // Mock invitations (banner)
    await page.route('**/api/delegations/my-invitations*', async route => {
      await route.fulfill({
        json: [
          {
            participantId: 500,
            participantRole: 'IC_SUPPORT',
            delegationName: 'Test Delegation',
            status: 'INVITED'
          }
        ]
      });
    });

    await page.route('**/api/notifications/unread-count', route => route.fulfill({ json: { unreadCount: 0 } }));
    await page.route('**/api/notifications?*', route => route.fulfill({ json: { items: [], totalCount: 0 } }));

    // Mock summary stats
    await page.route('**/api/delegations/guest-delegations/summary', async route => {
      await route.fulfill({
        json: { total: 1 }
      });
    });

    // Mock attending list
    await page.route('**/api/visit-invitations/my*', async route => {
      await route.fulfill({
        json: {
          items: [
            {
              visitRequestId: 'vr1',
              visitInstanceId: 1,
              delegationName: 'Test Delegation Attending',
              participantRole: 'IC_SUPPORT',
              campusStatus: 'ASSIGNED',
              allowedActions: ['OPEN_CONTRIBUTION']
            }
          ],
          total: 1
        }
      });
    });

    await page.goto('/dashboard/visit', { waitUntil: 'domcontentloaded' });

    // Should see pending banner
    await expect(page.getByRole('heading', { name: /Lời mời tham gia chờ phản hồi/i })).toBeVisible({ timeout: 5000 });

    // Should see attending tab
    const attendingTab = page.locator('button', { hasText: 'Lời mời tham dự' });
    await expect(attendingTab).toBeVisible();

    // Click attending tab
    await attendingTab.click();

    // Should see list item with the delegation
    await expect(page.locator('text=Test Delegation Attending').first()).toBeVisible({ timeout: 10000 });
    
    // Label should be "Staff Leader hỗ trợ IC"
    await expect(page.locator('text=Staff Leader hỗ trợ IC').first()).toBeVisible();
  });
});
