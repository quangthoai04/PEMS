/**
 * REAL-STACK E2E — fail-closed authenticated access (journeys B/C).
 *
 * real Chromium → real React (Vite) → real .NET API (Testing, flags ON, fail-closed E2E auth) → disposable
 * MySQL. The orchestration (`scripts/run-realstack-e2e.mjs`) starts the API with the four E2E-auth gates set
 * and a server-side profile file whose keys map to the DISPOSABLE DB's actual seeded identities. The browser
 * only ever sends an opaque profile KEY + the run secret (injected on backend requests only); role/campus are
 * resolved SERVER-SIDE. NO network mocking.
 *
 * Journey C — the running host enforces the fail-closed gate (no/wrong secret + unknown profile → 401) and
 *             resolves identity server-side (a profile key never carries more authority than the seed granted).
 * Journey B — an authenticated HO reaches the protected visit dashboard: the browser's own /auth/me is
 *             E2E-authenticated (200) and ProtectedRoute does not bounce to login.
 */
import { test, expect, type Browser } from '@playwright/test';

const API_BASE = process.env.PEMS_E2E_API_BASE ?? 'http://localhost:5299/api';
const SECRET = process.env.PEMS_E2E_AUTH_SECRET ?? '';
const API_PORT = new URL(API_BASE).port || '5299';

/** Minimal AuthUser so ProtectedRoute renders before the (E2E-authenticated) /me validates it. */
const HO_USER = {
  userId: '2', fullName: 'Head Office Coordinator', email: 'ho@fpt.edu.vn',
  roleCode: 'HO', mustChangePassword: false, mustSetPassword: false,
};

async function authedPage(browser: Browser, profileKey: string, user: Record<string, unknown>) {
  const context = await browser.newContext();
  // Inject the E2E auth headers ONLY on requests to the backend API origin (the API port) — never to Vite,
  // static assets, or any other origin. continue() replaces headers, so the originals are spread back in.
  await context.route(new RegExp(`:${API_PORT}/`), async route => {
    await route.continue({
      headers: { ...route.request().headers(), 'X-E2E-Profile': profileKey, 'X-E2E-Secret': SECRET },
    });
  });
  const page = await context.newPage();
  await page.addInitScript(u => {
    localStorage.setItem('token', 'e2e-session');
    localStorage.setItem('pems_user', JSON.stringify(u));
    localStorage.setItem('currentUser', JSON.stringify(u));
    localStorage.setItem('pems.language', 'vi');
  }, user);
  return { context, page };
}

test.describe('Real-stack: fail-closed authenticated v2', () => {
  test('the running host enforces the fail-closed gate and resolves identity server-side', async ({ request }) => {
    expect(SECRET, 'orchestration must provide PEMS_E2E_AUTH_SECRET').not.toBe('');

    // No secret → 401 (fail-closed at the REAL host, not just a unit test).
    expect((await request.get(`${API_BASE}/auth/me`,
      { headers: { 'X-E2E-Profile': 'ho_viewer' } })).status()).toBe(401);
    // Wrong secret → 401.
    expect((await request.get(`${API_BASE}/auth/me`,
      { headers: { 'X-E2E-Profile': 'ho_viewer', 'X-E2E-Secret': 'not-the-secret' } })).status()).toBe(401);
    // Unknown profile + valid secret → 401.
    expect((await request.get(`${API_BASE}/auth/me`,
      { headers: { 'X-E2E-Profile': 'ghost', 'X-E2E-Secret': SECRET } })).status()).toBe(401);

    // Valid profile + secret → 200 with the identity resolved SERVER-SIDE from the seed (/me returns { user }).
    const ho = await request.get(`${API_BASE}/auth/me`, { headers: { 'X-E2E-Profile': 'ho_viewer', 'X-E2E-Secret': SECRET } });
    expect(ho.status()).toBe(200);
    const hoUser = (await ho.json()).user;
    expect(hoUser.email).toBe('ho@fpt.edu.vn');
    expect(hoUser.roleCode).toBe('HO');

    // A different key resolves to a DIFFERENT seeded identity — a key never becomes another campus's leader.
    const hn = await request.get(`${API_BASE}/auth/me`, { headers: { 'X-E2E-Profile': 'campus_leader_hn', 'X-E2E-Secret': SECRET } });
    expect(hn.status()).toBe(200);
    const hnUser = (await hn.json()).user;
    expect(hnUser.email).toBe('staff.leader.hn@fpt.edu.vn');
    expect(hnUser.roleCode).toBe('STAFF');
    expect(hnUser.email).not.toBe('staff.leader.hcm@fpt.edu.vn');
  });

  test('an authenticated HO reaches the protected visit dashboard through the real stack', async ({ browser }) => {
    const { context, page } = await authedPage(browser, 'ho_viewer', HO_USER);
    try {
      // The app's own /auth/me fires on mount; it must be E2E-authenticated (200) through the browser.
      const mePromise = page.waitForResponse(
        r => r.url().includes('/auth/me') && r.request().method() === 'GET', { timeout: 25_000 });
      await page.goto('/dashboard/visit');
      const meRes = await mePromise;
      expect(meRes.status()).toBe(200);
      // ProtectedRoute did not bounce to a login route.
      await expect(page).toHaveURL(/\/dashboard\/visit/, { timeout: 15_000 });
    } finally {
      await context.close();
    }
  });
});
