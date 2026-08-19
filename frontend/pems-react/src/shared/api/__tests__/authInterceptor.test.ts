/**
 * `authInterceptor` used to attach WHATEVER token sat in `localStorage.token` to EVERY request,
 * `/auth/login` included. Combined with the backend's `SessionValidationMiddleware` not respecting
 * `[AllowAnonymous]` (see the backend-side fix + tests), a stale/revoked session on the browser
 * blocked the login attempt itself before the new credentials were ever checked. This is the
 * frontend half of the fix: never attach a stale bearer to a public auth endpoint in the first
 * place — defense in depth on top of the backend's own correction.
 */
import { describe, expect, it, beforeEach, afterEach } from 'vitest';
import { authInterceptor } from '../authInterceptor';

const ORIGINAL_TOKEN = 'stale-token-from-a-revoked-session';

describe('authInterceptor', () => {
  beforeEach(() => {
    localStorage.setItem('token', ORIGINAL_TOKEN);
  });
  afterEach(() => {
    localStorage.clear();
  });

  it.each([
    '/auth/login',
    '/auth/google',
    '/auth/feid',
    '/auth/refresh',
    '/auth/forgot-password',
    '/auth/reset-password',
  ])('never attaches a bearer token to the public auth endpoint %s', (url) => {
    const config = authInterceptor({ url, headers: {} });
    expect(config.headers.Authorization).toBeUndefined();
  });

  it('still attaches the token to a protected endpoint', () => {
    const config = authInterceptor({ url: '/auth/me', headers: {} });
    expect(config.headers.Authorization).toBe(`Bearer ${ORIGINAL_TOKEN}`);
  });

  it('attaches the token to /auth/logout (it is [Authorize] on the backend, not public)', () => {
    const config = authInterceptor({ url: '/auth/logout', headers: {} });
    expect(config.headers.Authorization).toBe(`Bearer ${ORIGINAL_TOKEN}`);
  });

  it('attaches the token to an ordinary business endpoint', () => {
    const config = authInterceptor({ url: '/delegations/viewguestdelegationlist', headers: {} });
    expect(config.headers.Authorization).toBe(`Bearer ${ORIGINAL_TOKEN}`);
  });

  it('adds no Authorization header at all when there is no stored token', () => {
    localStorage.clear();
    const config = authInterceptor({ url: '/auth/me', headers: {} });
    expect(config.headers.Authorization).toBeUndefined();
  });
});
