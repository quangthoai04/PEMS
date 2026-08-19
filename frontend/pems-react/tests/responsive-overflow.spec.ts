/**
 * System-wide responsive regression guard (docs/CanhIter3FixBug/GopYCQuyen/
 * PEMS_System_Wide_Responsive_UI_Audit_and_Fix_Plan.md §41/§42/§44).
 *
 * Two checks per route x viewport:
 *  1. Page-level horizontal overflow: documentElement.scrollWidth must not exceed clientWidth,
 *     EXCEPT for whitelisted intentional scrollers (table containers, .pems-email-body, code/pre
 *     viewers) which are excluded from the element-bounds check below.
 *  2. Critical-element bounds: every visible button/input/select/textarea/heading/nav/dialog must
 *     have its bounding rect within [0, viewportWidth] on the X axis. This catches what a plain
 *     document.scrollWidth check misses -- an element clipped by an ANCESTOR's overflow-hidden can
 *     leave the document itself not-overflowing while the element is still cut off / unreachable.
 *
 * Viewport matrix: the plan's minimum automated set (320, 390, 768, 1366). The full matrix (360,
 * 430, phone landscape, 1024, 1280, 1536, 1920) and every route/role combination is NOT covered here
 * -- this is a representative-route regression guard, not the full manual QA sweep.
 */
import { test, expect, type Page } from '@playwright/test';

const VIEWPORTS = [
  { name: '320', width: 320, height: 720 },
  { name: '390', width: 390, height: 844 },
  { name: '768', width: 768, height: 1024 },
  { name: '1366', width: 1366, height: 900 },
];

/** Selectors whose own internal horizontal scroll is intentional -- their CHILDREN may legitimately
 * extend past the viewport; the container itself scrolling them is the point (plan §19/§42's
 * exception list). Excluded from the element-bounds check. */
const INTENTIONAL_SCROLLER_SELECTOR = '.pems-email-body, [class*="overflow-x-auto"], table';

async function assertNoDocumentOverflow(page: Page, tolerance = 1) {
  const { scrollWidth, clientWidth } = await page.evaluate(() => ({
    scrollWidth: document.documentElement.scrollWidth,
    clientWidth: document.documentElement.clientWidth,
  }));
  expect(scrollWidth, `document.scrollWidth (${scrollWidth}) should not exceed clientWidth (${clientWidth})`)
    .toBeLessThanOrEqual(clientWidth + tolerance);
}

/**
 * Bounding-rect check for critical interactive/content elements. Catches an element clipped by an
 * ancestor's overflow-hidden, which a document-level scrollWidth check cannot see (the ancestor's
 * own box never grows).
 */
async function assertCriticalElementsInViewport(page: Page, viewportWidth: number, tolerance = 2) {
  const offenders = await page.evaluate(
    ({ selector, exclude, tol, vw }) => {
      const isInsideScroller = (el: Element) => el.closest(exclude) !== null;
      const nodes = Array.from(document.querySelectorAll(selector)) as HTMLElement[];
      const bad: { tag: string; text: string; left: number; right: number }[] = [];
      for (const el of nodes) {
        if (isInsideScroller(el)) continue;
        const style = window.getComputedStyle(el);
        if (style.display === 'none' || style.visibility === 'hidden') continue;
        const rect = el.getBoundingClientRect();
        if (rect.width === 0 && rect.height === 0) continue; // not laid out / not rendered
        if (rect.left < -tol || rect.right > vw + tol) {
          bad.push({ tag: el.tagName, text: (el.textContent || '').trim().slice(0, 40), left: rect.left, right: rect.right });
        }
      }
      return bad;
    },
    { selector: 'button, input, select, textarea, h1, h2, [role="dialog"], nav', exclude: INTENTIONAL_SCROLLER_SELECTOR, tol: tolerance, vw: viewportWidth }
  );
  expect(offenders, `critical elements out of [0, ${viewportWidth}]: ${JSON.stringify(offenders)}`).toEqual([]);
}

async function assertResponsive(page: Page, viewportWidth: number) {
  await assertNoDocumentOverflow(page);
  await assertCriticalElementsInViewport(page, viewportWidth);
}

test.describe('Responsive regression -- public routes', () => {
  // Without this, i18next's browser language detector picks whatever the Playwright browser
  // context reports (English, in this environment) -- the VI locale's "Đăng nhập" button becomes
  // EN's "Sign in" and every VI-text locator below silently never matches (a `getByRole` wait times
  // out rather than failing fast, which looked identical to a genuine responsive bug at first).
  async function seedVietnamese(page: Page) {
    await page.addInitScript(() => window.localStorage.setItem('pems.language', 'vi'));
  }

  for (const vp of VIEWPORTS) {
    test(`Home @ ${vp.name}px has no overflow / clipped critical elements`, async ({ page }) => {
      await page.setViewportSize({ width: vp.width, height: vp.height });
      await seedVietnamese(page);
      await page.goto('/');
      await expect(page.locator('header').first()).toBeVisible();
      await assertResponsive(page, vp.width);
    });
  }

  // Login: `/login` redirects to `/` (App.tsx), so testing it means opening the REAL LoginModal
  // through the UI, not goto('/login') followed by a pass on the redirect target. The desktop
  // "Đăng nhập" button only exists in the DOM's accessible tree from the `xl` breakpoint up
  // (`hidden xl:flex` in Header.tsx) -- below that the real user opens the hamburger drawer first
  // and clicks "Đăng nhập" there, so the test has to do the same per-viewport branch, not assume
  // one button locator works at every width.
  for (const vp of VIEWPORTS) {
    test(`Login modal (opened via Home UI) @ ${vp.name}px has no overflow / clipped elements`, async ({ page }) => {
      await page.setViewportSize({ width: vp.width, height: vp.height });
      await seedVietnamese(page);
      await page.goto('/');
      const isMobileNav = vp.width < 1280; // Tailwind `xl` breakpoint, matches Header.tsx's `xl:hidden`/`hidden xl:flex`
      if (isMobileNav) {
        await page.getByRole('button', { name: 'Mở menu' }).click();
      }
      const loginTrigger = page.getByRole('button', { name: 'Đăng nhập' }).first();
      await loginTrigger.click();
      await expect(page.getByText('FPT University').first()).toBeVisible();
      // On the mobile-nav path, clicking "Đăng nhập" inside the hamburger drawer also closes that
      // drawer (`setIsMobileMenuOpen(false)`), which plays a 300ms Framer Motion slide-out exit
      // (`{ type: 'spring', duration: 0.3 }`). Asserting immediately caught the drawer mid-slide,
      // with its buttons' bounding rects partway between on-screen and off-screen -- a transient,
      // intentional animation frame, not a responsive bug. Give it time to finish closing.
      if (isMobileNav) {
        await page.waitForTimeout(400);
      }
      await assertResponsive(page, vp.width);
    });
  }
});

test.describe('Responsive regression -- dashboard (HO role, mocked auth)', () => {
  const HO_AUTH_USER = {
    userId: '900', fullName: 'HO Responsive Tester', email: 'ho-responsive@test.local',
    roleCode: 'HO', subRole: null, primaryCampusId: null, campusCode: null, campusName: null,
    departmentId: null, mustChangePassword: false, mustSetPassword: false, effectiveRole: 'HO', status: 'ACTIVE',
  };

  const PAGED_EMPTY = { items: [], page: 1, pageSize: 20, totalItems: 0, totalPages: 0, hasNextPage: false, hasPreviousPage: false };

  // IMPORTANT: every override below matches on `url.pathname` (not a `**/...**` glob string).
  // A glob route matches its pattern as a substring of the full URL, and this dev server also
  // serves the app's OWN source files over HTTP -- `**/api/notifications**` does not just match a
  // backend call, it also matches the Vite module request for
  // `/src/features/notifications/api/notificationsApi.ts` (the substring "api/notifications" is
  // sitting right there in that file's own path). That collision made Vite's module response get
  // replaced with this mock's JSON body, which the browser then refused to load as a script
  // ("Expected a JavaScript-or-Wasm module script but the server responded with a MIME type of
  // application/json"), and the app never mounted. Matching on `pathname` compares only the URL's
  // path component against a real backend path, which cannot collide with `/src/**`.
  async function mockDashboardApis(page: Page) {
    await page.route((url) => url.pathname.startsWith('/api/'), (route) => route.fulfill({ json: {} }));
    await page.route((url) => url.pathname === '/api/auth/me', (route) => route.fulfill({ json: { user: HO_AUTH_USER } }));
    await page.route((url) => url.pathname.startsWith('/api/accounts'), (route) => route.fulfill({ json: PAGED_EMPTY }));
    // AccountManagement additionally loads these as plain arrays (not the paged {items:[]} shape),
    // and does `campusOptions.map(...)` etc. directly on the response with no defensive fallback.
    await page.route((url) => url.pathname === '/api/campuses/active', (route) => route.fulfill({ json: [] }));
    await page.route((url) => url.pathname === '/api/accounts/statistics', (route) => route.fulfill({
      json: { totalAccounts: 0, activeAccounts: 0, inactiveAccounts: 0, lockedAccounts: 0 },
    }));
    await page.route((url) => url.pathname === '/api/accounts/campus-departments', (route) => route.fulfill({ json: [] }));
    await page.route((url) => url.pathname.startsWith('/api/visit-requests'), (route) => route.fulfill({ json: PAGED_EMPTY }));
    // NotificationBellButton is mounted on every dashboard route (it lives in DashboardLayout's
    // header) and does `data.items.map(...)` with no defensive fallback -- the blanket `{}` mock
    // above made it throw into the route's <ErrorBoundary>, which was failing every dashboard test
    // for a reason that had nothing to do with responsive layout.
    await page.route((url) => url.pathname === '/api/notifications/unread-count', (route) => route.fulfill({ json: { count: 0 } }));
    await page.route((url) => url.pathname.startsWith('/api/notifications'), (route) => route.fulfill({ json: PAGED_EMPTY }));
  }

  async function seedAuth(page: Page, user: typeof HO_AUTH_USER = HO_AUTH_USER) {
    await page.addInitScript(({ user }) => {
      window.localStorage.setItem('pems.language', 'vi');
      window.localStorage.setItem('token', 'test-access-token');
      window.localStorage.setItem('refreshToken', 'test-refresh-token');
      window.localStorage.setItem('pems_user', JSON.stringify(user));
      window.localStorage.setItem('currentUser', JSON.stringify({
        userId: user.userId, name: user.fullName, email: user.email, role: user.roleCode, campus: '',
      }));
    }, { user });
  }

  for (const vp of VIEWPORTS) {
    test(`Dashboard home @ ${vp.name}px has no overflow / clipped elements`, async ({ page }) => {
      await page.setViewportSize({ width: vp.width, height: vp.height });
      await mockDashboardApis(page);
      await seedAuth(page);
      await page.goto('/dashboard');
      await expect(page.locator('#dashboard-root')).toBeVisible();
      await assertResponsive(page, vp.width);
    });
  }

  for (const vp of VIEWPORTS) {
    test(`Account Management @ ${vp.name}px has no overflow / clipped elements`, async ({ page }) => {
      await page.setViewportSize({ width: vp.width, height: vp.height });
      await mockDashboardApis(page);
      await seedAuth(page);
      await page.goto('/dashboard/accounts');
      await expect(page.locator('#dashboard-root')).toBeVisible();
      await assertResponsive(page, vp.width);
    });
  }

  for (const vp of VIEWPORTS) {
    test(`Visit Management list @ ${vp.name}px has no overflow / clipped elements`, async ({ page }) => {
      await page.setViewportSize({ width: vp.width, height: vp.height });
      await mockDashboardApis(page);
      await seedAuth(page);
      await page.goto('/dashboard/visit');
      await expect(page.locator('#dashboard-root')).toBeVisible();
      await assertResponsive(page, vp.width);
    });
  }
});

test.describe('Responsive regression -- role matrix (dashboard landing, mocked auth)', () => {
  // Each role sees a DIFFERENT dashboard landing component (see App.tsx's routing +
  // resolveEffectiveRole.ts) -- menu/actions/widgets differ per role, so "passes as HO" does not
  // imply "passes as STUDENT". Covers the 2 automated-critical viewports (390 mobile, 1366 desktop)
  // per role rather than the full 4-viewport matrix, to keep this a representative check, not a
  // duplicate of the dedicated Dashboard-home block above (which already covers HO at all 4).
  const ROLE_VIEWPORTS = [
    { name: '390', width: 390, height: 844 },
    { name: '1366', width: 1366, height: 900 },
  ];

  const ROLES: { label: string; user: typeof HO_AUTH_USER; path: string }[] = [
    {
      label: 'ADMIN',
      path: '/dashboard',
      user: { userId: '901', fullName: 'Admin Tester', email: 'admin-responsive@test.local', roleCode: 'ADMIN', subRole: null, primaryCampusId: null, campusCode: null, campusName: null, departmentId: null, mustChangePassword: false, mustSetPassword: false, effectiveRole: 'ADMIN', status: 'ACTIVE' },
    },
    {
      label: 'STAFF_LEADER',
      path: '/dashboard',
      user: { userId: '902', fullName: 'Staff Leader Tester', email: 'staffleader-responsive@test.local', roleCode: 'STAFF', subRole: 'LEADER', primaryCampusId: 1, campusCode: 'HN', campusName: 'Campus Hà Nội', departmentId: null, mustChangePassword: false, mustSetPassword: false, effectiveRole: 'STAFF_LEADER', status: 'ACTIVE' },
    },
    {
      label: 'STAFF (IC)',
      path: '/dashboard',
      user: { userId: '903', fullName: 'IC Staff Tester', email: 'staff-responsive@test.local', roleCode: 'STAFF', subRole: 'STAFF', primaryCampusId: 1, campusCode: 'HN', campusName: 'Campus Hà Nội', departmentId: null, mustChangePassword: false, mustSetPassword: false, effectiveRole: 'STAFF', status: 'ACTIVE' },
    },
    {
      label: 'DEPARTMENT_LEAD',
      path: '/dashboard',
      user: { userId: '904', fullName: 'Dept Lead Tester', email: 'deptlead-responsive@test.local', roleCode: 'DEPARTMENT', subRole: 'LEADER', primaryCampusId: 1, campusCode: 'HN', campusName: 'Campus Hà Nội', departmentId: 5, mustChangePassword: false, mustSetPassword: false, effectiveRole: 'DEPARTMENT_LEAD', status: 'ACTIVE' },
    },
    {
      label: 'DEPARTMENT_STAFF',
      path: '/dashboard',
      user: { userId: '905', fullName: 'Dept Staff Tester', email: 'deptstaff-responsive@test.local', roleCode: 'DEPARTMENT', subRole: 'STAFF', primaryCampusId: 1, campusCode: 'HN', campusName: 'Campus Hà Nội', departmentId: 5, mustChangePassword: false, mustSetPassword: false, effectiveRole: 'DEPARTMENT', status: 'ACTIVE' },
    },
    {
      // App.tsx redirects STUDENT's dashboard index to /dashboard/visit -- test that route directly.
      label: 'STUDENT',
      path: '/dashboard/visit',
      user: { userId: '906', fullName: 'Student Tester', email: 'student-responsive@test.local', roleCode: 'STUDENT', subRole: null, primaryCampusId: 1, campusCode: 'HN', campusName: 'Campus Hà Nội', departmentId: null, mustChangePassword: false, mustSetPassword: false, effectiveRole: 'STUDENT', status: 'ACTIVE' },
    },
    {
      // Same redirect as STUDENT.
      label: 'VISITOR',
      path: '/dashboard/visit',
      user: { userId: '907', fullName: 'Visitor Tester', email: 'visitor-responsive@test.local', roleCode: 'VISITOR', subRole: null, primaryCampusId: null, campusCode: null, campusName: null, departmentId: null, mustChangePassword: false, mustSetPassword: false, effectiveRole: 'VISITOR', status: 'ACTIVE' },
    },
  ];

  async function mockDashboardApis(page: Page) {
    await page.route((url) => url.pathname.startsWith('/api/'), (route) => route.fulfill({ json: {} }));
    await page.route((url) => url.pathname === '/api/notifications/unread-count', (route) => route.fulfill({ json: { count: 0 } }));
    await page.route((url) => url.pathname.startsWith('/api/notifications'), (route) => route.fulfill({
      json: { items: [], page: 1, pageSize: 20, totalItems: 0, totalPages: 0 },
    }));
    await page.route((url) => url.pathname.startsWith('/api/visit-requests'), (route) => route.fulfill({
      json: { items: [], page: 1, pageSize: 20, totalItems: 0, totalPages: 0, hasNextPage: false, hasPreviousPage: false },
    }));
    // VISITOR's own visit list reads its campus filter options from here (authenticationApi.getActiveCampuses).
    await page.route((url) => url.pathname === '/api/campuses/active', (route) => route.fulfill({ json: [] }));
    // ADMIN's dashboard landing (AdminDashboardView) fetches several `/admin/dashboard/*` panels, each
    // typed as a bare array/object (not the {items:[...]} paged shape) -- the blanket `{}` mock above
    // isn't an array, so e.g. `(loginActivity.data ?? []).some(...)` sees `{}` (not nullish) and throws.
    await page.route((url) => url.pathname === '/api/admin/dashboard/login-activity', (route) => route.fulfill({ json: [] }));
    await page.route((url) => url.pathname === '/api/admin/dashboard/summary', (route) => route.fulfill({
      json: {
        accounts: { total: 0, active: 0, inactive: 0, locked: 0, newLast30Days: 0 },
        sessions: { active: 0, expired: 0, revoked: 0 },
        logins24h: { success: 0, failed: 0 },
        security: { highLast7Days: 0, criticalLast7Days: 0 },
        integrations: { total: 0, active: 0, testFailed: 0, missingCredential: 0, quotaAbove80Percent: 0 },
      },
    }));
    // A DIFFERENT shape from the `security` field nested in the summary above -- this is the full
    // AdminSecurityOverview from a separate endpoint (adminApi.getSecurityOverview()).
    await page.route((url) => url.pathname === '/api/admin/dashboard/security', (route) => route.fulfill({
      json: { low: 0, medium: 0, high: 0, critical: 0, recentHighSeverity: [] },
    }));
    await page.route((url) => url.pathname === '/api/admin/dashboard/integrations', (route) => route.fulfill({ json: [] }));
    await page.route((url) => url.pathname === '/api/admin/dashboard/recent-audits', (route) => route.fulfill({ json: [] }));
  }

  for (const role of ROLES) {
    for (const vp of ROLE_VIEWPORTS) {
      test(`${role.label} dashboard landing @ ${vp.name}px has no overflow / clipped elements`, async ({ page }) => {
        await page.setViewportSize({ width: vp.width, height: vp.height });
        await mockDashboardApis(page);
        await page.route((url) => url.pathname === '/api/auth/me', (route) => route.fulfill({ json: { user: role.user } }));
        await page.addInitScript(({ user }) => {
          window.localStorage.setItem('pems.language', 'vi');
          window.localStorage.setItem('token', 'test-access-token');
          window.localStorage.setItem('refreshToken', 'test-refresh-token');
          window.localStorage.setItem('pems_user', JSON.stringify(user));
          window.localStorage.setItem('currentUser', JSON.stringify({
            userId: user.userId, name: user.fullName, email: user.email, role: user.roleCode, campus: '',
          }));
        }, { user: role.user });
        await page.goto(role.path);
        await expect(page.locator('#dashboard-root')).toBeVisible();
        await assertResponsive(page, vp.width);
      });
    }
  }
});
