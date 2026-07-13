/**
 * UC-86 Campus Management — status vs. operational readiness (doc §29), against the real UI
 * with a mocked network:
 *   TC-01 ACTIVE + readiness false → toggle ON, badge "Hoạt động" AND badge "Chưa sẵn sàng" + reason
 *   TC-02 disable preview has blockers → PATCH never called, blocker summary shown
 *   TC-03 preview passes but PATCH races into 409 → toggle stays ON, backend message shown
 *   TC-04 enable succeeds but campus not ready → warning (never "đã sẵn sàng nhận đăng ký")
 *   TC-05 create campus without Staff Leader → success + readiness warning
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

const READY = {
  isAvailableForVisitRegistration: true,
  activeIcDepartmentExists: true,
  activeStaffLeaderExists: true,
  readinessIssues: [],
};

const NOT_READY_NO_LEADER = {
  isAvailableForVisitRegistration: false,
  activeIcDepartmentExists: true,
  activeStaffLeaderExists: false,
  readinessIssues: ['ACTIVE_STAFF_LEADER_MISSING'],
};

const CAMPUS_ROWS = [
  {
    campusId: 1, campusCode: 'HN', name: 'Campus Hà Nội', city: 'Hà Nội',
    icHeadUserId: 10, icHeadName: 'Nguyễn Lãnh Đạo', status: 'ACTIVE',
    createdAt: '2026-01-01T00:00:00', updatedAt: null, canManageStatus: true,
    readiness: READY,
  },
  {
    campusId: 2, campusCode: 'QN', name: 'Campus Quy Nhơn', city: 'Gia Lai',
    icHeadUserId: null, icHeadName: null, status: 'ACTIVE',
    createdAt: '2026-01-01T00:00:00', updatedAt: null, canManageStatus: true,
    readiness: NOT_READY_NO_LEADER,
  },
  {
    campusId: 3, campusCode: 'CT', name: 'Campus Cần Thơ', city: 'Cần Thơ',
    icHeadUserId: null, icHeadName: null, status: 'INACTIVE',
    createdAt: '2026-01-01T00:00:00', updatedAt: null, canManageStatus: true,
    readiness: { ...NOT_READY_NO_LEADER, readinessIssues: ['CAMPUS_INACTIVE', 'ACTIVE_STAFF_LEADER_MISSING'] },
  },
];

function pagedList(rows: typeof CAMPUS_ROWS) {
  return {
    items: rows,
    page: 1,
    pageSize: 10,
    totalItems: rows.length,
    totalPages: 1,
    hasNextPage: false,
    hasPreviousPage: false,
  };
}

/** Base network mocks: auth bootstrap + campus list + filter options + a JSON catch-all. */
async function mockDashboardApis(page: Page, rows = CAMPUS_ROWS) {
  // Catch-all FIRST (Playwright matches the most recently registered route first). Match by
  // pathname so vite module URLs like /src/features/**/api/*.ts are never intercepted.
  await page.route((url) => url.pathname.startsWith('/api/'), (route) => route.fulfill({ json: {} }));
  await page.route('**/api/auth/me', (route) => route.fulfill({ json: { user: HO_AUTH_USER } }));
  await page.route('**/api/campuses/viewcampuslist**', (route) => route.fulfill({ json: pagedList(rows) }));
  await page.route('**/api/campuses/filter-options', (route) =>
    route.fulfill({ json: { cities: ['Hà Nội', 'Gia Lai', 'Cần Thơ'], campuses: [], statuses: [] } }));
}

async function gotoCampusManagement(page: Page) {
  await page.addInitScript(({ user }) => {
    window.localStorage.setItem('pems.language', 'vi');
    window.localStorage.setItem('token', 'test-access-token');
    window.localStorage.setItem('refreshToken', 'test-refresh-token');
    window.localStorage.setItem('pems_user', JSON.stringify(user));
    window.localStorage.setItem('currentUser', JSON.stringify({
      userId: user.userId, name: user.fullName, email: user.email, role: user.roleCode, campus: '',
    }));
  }, { user: HO_AUTH_USER });

  await page.goto('/dashboard/campus');
  await expect(page.getByRole('heading', { name: 'Quản lý campus' })).toBeVisible();
  await expect(page.getByText('Campus Hà Nội')).toBeVisible();
}

function rowOf(page: Page, name: string) {
  return page.locator('tbody tr', { hasText: name });
}

test.describe('UC-86 Campus Management readiness & status', () => {
  test('TC-01: ACTIVE campus without Staff Leader shows ON toggle + separate not-ready badge with reason', async ({ page }) => {
    await mockDashboardApis(page);
    await gotoCampusManagement(page);

    const row = rowOf(page, 'Campus Quy Nhơn');
    await expect(row.getByText('Hoạt động', { exact: true })).toBeVisible();
    await expect(row.getByText('Chưa sẵn sàng')).toBeVisible();
    await expect(row.getByText('Chưa có Staff Leader đang hoạt động.')).toBeVisible();
    // Toggle reflects the administrative status only — it stays ON (title = disable action).
    await expect(row.getByTitle('Ngừng hoạt động')).toBeVisible();

    // Ready campus shows the ready badge; INACTIVE campus shows the no-registration badge.
    await expect(rowOf(page, 'Campus Hà Nội').getByText('Sẵn sàng nhận đăng ký')).toBeVisible();
    await expect(rowOf(page, 'Campus Cần Thơ').getByText('Không nhận đăng ký')).toBeVisible();
  });

  test('TC-02: disable preview with blockers never calls PATCH and shows the blocker summary', async ({ page }) => {
    await mockDashboardApis(page);
    let patchCalls = 0;
    await page.route('**/api/campuses/managecampusstatus', (route) => {
      patchCalls += 1;
      return route.fulfill({ json: {} });
    });
    await page.route('**/api/campuses/campusstatusimpact**', (route) =>
      route.fulfill({
        json: {
          campusId: 1, name: 'Campus Hà Nội', currentStatus: 'ACTIVE', targetStatus: 'INACTIVE',
          canChange: false, blockerCount: 4,
          blockersByStatus: { WAITING_REQUEST_APPROVAL: 2, ASSIGNED: 1, AFTER_VISIT: 1 },
          blockerExamples: [{
            visitInstanceId: 51, requestId: 50, requestCode: 'VR20260001',
            delegationName: 'Đoàn Đại học ABC', status: 'WAITING_REQUEST_APPROVAL',
            plannedStartAt: '2026-08-01T09:00:00', plannedEndAt: '2026-08-01T11:00:00',
          }],
          enableIssues: [], readiness: READY,
        },
      }));
    await gotoCampusManagement(page);

    await rowOf(page, 'Campus Hà Nội').getByTitle('Ngừng hoạt động').click();

    await expect(page.getByText('Không thể ngừng hoạt động campus.')).toBeVisible();
    await expect(page.getByText('2 đơn đang chờ xử lý')).toBeVisible();
    await expect(page.getByText('1 chuyến đã được phân công/chuẩn bị')).toBeVisible();
    await expect(page.getByText('1 chuyến đang hoặc đã tiếp khách nhưng chưa đóng')).toBeVisible();
    await expect(page.getByText('Đoàn Đại học ABC')).toBeVisible();
    // No actionable confirm button → PATCH cannot be sent from this modal.
    await expect(page.getByRole('button', { name: /Xác nhận ngừng hoạt động/ })).toHaveCount(0);

    // Footer close button (the header X also has aria-label "Đóng" → use exact text).
    await page.getByText('Đóng', { exact: true }).click();
    expect(patchCalls).toBe(0);
  });

  test('TC-03: preview passes but PATCH returns 409 → backend message shown, toggle stays ON', async ({ page }) => {
    await mockDashboardApis(page);
    await page.route('**/api/campuses/campusstatusimpact**', (route) =>
      route.fulfill({
        json: {
          campusId: 1, name: 'Campus Hà Nội', currentStatus: 'ACTIVE', targetStatus: 'INACTIVE',
          canChange: true, blockerCount: 0, blockersByStatus: {}, blockerExamples: [],
          enableIssues: [], readiness: READY,
        },
      }));
    await page.route('**/api/campuses/managecampusstatus', (route) =>
      route.fulfill({
        status: 409,
        json: {
          success: false,
          errorCode: 'CAMPUS_HAS_ACTIVE_VISITS',
          message: 'Không thể ngừng hoạt động campus vì còn chuyến thăm chưa hoàn tất.',
        },
      }));
    await gotoCampusManagement(page);

    await rowOf(page, 'Campus Hà Nội').getByTitle('Ngừng hoạt động').click();
    await expect(page.getByText('Campus sẽ không còn xuất hiện trong các lựa chọn đăng ký/phân công mới.')).toBeVisible();
    await page.getByRole('button', { name: /Xác nhận/ }).click();

    // Backend 409 message surfaces; no optimistic flip — the row still renders ACTIVE.
    await expect(page.getByText('Không thể ngừng hoạt động campus vì còn chuyến thăm chưa hoàn tất.')).toBeVisible();
    const row = rowOf(page, 'Campus Hà Nội');
    await expect(row.getByText('Hoạt động', { exact: true })).toBeVisible();
    await expect(row.getByTitle('Ngừng hoạt động')).toBeVisible();
  });

  test('TC-03b: disable success reports how many campus accounts were logged out', async ({ page }) => {
    await mockDashboardApis(page);
    await page.route('**/api/campuses/campusstatusimpact**', (route) =>
      route.fulfill({
        json: {
          campusId: 1, name: 'Campus Hà Nội', currentStatus: 'ACTIVE', targetStatus: 'INACTIVE',
          canChange: true, blockerCount: 0, blockersByStatus: {}, blockerExamples: [],
          enableIssues: [], readiness: READY,
        },
      }));
    await page.route('**/api/campuses/managecampusstatus', (route) =>
      route.fulfill({
        json: {
          campusId: 1, status: 'INACTIVE', updatedAt: '2026-07-13T10:00:00', updatedBy: 900,
          message: 'Đã ngừng hoạt động campus. 3 tài khoản không còn quyền truy cập hệ thống.',
          readiness: { ...READY, isAvailableForVisitRegistration: false, readinessIssues: ['CAMPUS_INACTIVE'] },
          affectedAccountCount: 3, revokedSessionCount: 5,
        },
      }));
    await gotoCampusManagement(page);

    // The confirmation modal warns that campus accounts will be logged out.
    await rowOf(page, 'Campus Hà Nội').getByTitle('Ngừng hoạt động').click();
    await expect(page.getByText(/sẽ bị đăng xuất và không đăng nhập lại được/)).toBeVisible();
    await page.getByRole('button', { name: /Xác nhận/ }).click();

    await expect(page.getByText('Đã ngừng hoạt động campus "Campus Hà Nội". 3 tài khoản thuộc cơ sở đã bị đăng xuất.')).toBeVisible();
  });

  test('TC-04: enable succeeds but campus not ready → warning, never a "ready" success', async ({ page }) => {
    await mockDashboardApis(page);
    await page.route('**/api/campuses/managecampusstatus', (route) =>
      route.fulfill({
        json: {
          campusId: 3, status: 'ACTIVE', updatedAt: '2026-07-13T10:00:00', updatedBy: 900,
          message: 'Đã kích hoạt campus.',
          readiness: NOT_READY_NO_LEADER,
        },
      }));
    await gotoCampusManagement(page);

    await rowOf(page, 'Campus Cần Thơ').getByTitle('Kích hoạt').click();

    await expect(page.getByText(/Đã kích hoạt campus "Campus Cần Thơ"\. Campus chưa xuất hiện trên form đăng ký tham quan/)).toBeVisible();
    await expect(page.getByText('Campus đã sẵn sàng nhận đăng ký.')).toHaveCount(0);
  });

  test('TC-05: create campus without Staff Leader → success + readiness warning toast', async ({ page }) => {
    await mockDashboardApis(page);
    await page.route('**/api/campuses/addnewcampus', (route) =>
      route.fulfill({
        json: {
          campusId: 9, campusCode: 'DN2', name: 'Campus Đà Nẵng 2', city: 'Đà Nẵng',
          address: '1 Đường Test', phone: '0236 730 0000', email: 'dn2@fpt.edu.vn',
          icHeadUserId: null, status: 'ACTIVE',
          icDepartment: { departmentId: 90, campusId: 9, name: 'Phòng Hợp tác Quốc tế', departmentType: 'IC', status: 'ACTIVE' },
        },
      }));
    await gotoCampusManagement(page);

    await page.getByRole('button', { name: 'Thêm mới campus' }).click();
    await page.getByPlaceholder('VD: HN, HCM...').fill('DN2');
    await page.getByPlaceholder('VD: FPT University Hà Nội').fill('Campus Đà Nẵng 2');
    await page.getByPlaceholder('Số nhà, đường, phường/xã...').fill('1 Đường Test');
    await page.getByPlaceholder('VD: 024 7300 5588').fill('0236 730 0000');
    await page.getByPlaceholder('VD: hn@fpt.edu.vn').fill('dn2@fpt.edu.vn');
    await page.getByRole('button', { name: 'Tạo mới' }).click();

    await expect(page.getByText('Đã tạo campus "Campus Đà Nẵng 2" và phòng ban IC mặc định.')).toBeVisible();
    await expect(page.getByText(/chưa xuất hiện trên form đăng ký tham quan vì chưa có Staff Leader/)).toBeVisible();
  });
});
