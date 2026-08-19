/**
 * Root cause of the "public homepage F5 shows a false 'session expired' toast" bug: AuthContext's
 * bootstrap effect calls `GET /auth/me` on every page load whenever a token is stored — including
 * a stale/revoked one left over from a previous visit while the user is just browsing the PUBLIC
 * homepage. `/auth/me` correctly 401s (it IS protected), and the shared response interceptor's
 * blanket "clear auth + toast" reaction fired regardless of who asked or why, even though bootstrap
 * already handles a failed check silently on its own. `suppressSessionExpiredToast` is the escape
 * hatch: the interceptor still clears auth state and fires `AUTH_EXPIRED_EVENT` (other listeners
 * still need to know), it just skips the toast for a caller that already handles the failure quietly.
 *
 * Exercises the REAL interceptor pipeline via a custom axios adapter (no real network/backend),
 * mirroring the pattern axios-mock-adapter uses internally.
 */
import { describe, expect, it, vi, beforeEach, afterEach } from 'vitest';
import { AxiosError, type InternalAxiosRequestConfig, type AxiosResponse } from 'axios';

const showMessageErrorToastMock = vi.fn();
vi.mock('../../utils/toast', () => ({
  showMessageErrorToast: (...args: unknown[]) => showMessageErrorToastMock(...args),
}));

const httpClientModule = await import('../httpClient');
const httpClient = httpClientModule.default;
const { setAuthBootstrapping } = httpClientModule;
type PemsRequestConfig = import('../httpClient').PemsRequestConfig;
const { authStorage, AUTH_EXPIRED_EVENT } = await import('../../auth/authStorage');

/**
 * Makes every request to `url` "respond" with `status`/`data`, everything else 404s. A real axios
 * adapter is responsible for its OWN validateStatus-based rejection (dispatchRequest does not
 * re-check it), so this mirrors that: a non-2xx status rejects with a proper AxiosError carrying
 * `.config`/`.response`, exactly what the response interceptor under test reads.
 */
function mockAdapterFor(url: string, status: number, data: unknown) {
  httpClient.defaults.adapter = async (config: InternalAxiosRequestConfig): Promise<AxiosResponse> => {
    const matched = (config.url ?? '').includes(url);
    const resStatus = matched ? status : 404;
    const resData = matched ? data : { message: 'not mocked' };
    const response: AxiosResponse = {
      data: resData,
      status: resStatus,
      statusText: String(resStatus),
      headers: {},
      config,
    } as AxiosResponse;
    if (resStatus >= 200 && resStatus < 300) return response;
    throw new AxiosError(`Request failed with status code ${resStatus}`, undefined, config, null, response);
  };
}

describe('httpClient response interceptor — suppressSessionExpiredToast', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    authStorage.clear();
    authStorage.setTokens('stale-access-token', ''); // no refresh token → interceptor takes the immediate-clear branch
    setAuthBootstrapping(false);
  });
  afterEach(() => {
    httpClient.defaults.adapter = undefined;
    setAuthBootstrapping(false);
  });

  it('shows the session-expired toast for an ordinary protected call that 401s', async () => {
    mockAdapterFor('/auth/me', 401, { success: false, errorCode: 'SESSION_REVOKED', message: 'x' });

    await expect(httpClient.get('/auth/me')).rejects.toBeTruthy();

    expect(showMessageErrorToastMock).toHaveBeenCalledTimes(1);
  });

  it('does NOT show the toast when the request opts out via suppressSessionExpiredToast', async () => {
    mockAdapterFor('/auth/me', 401, { success: false, errorCode: 'SESSION_REVOKED', message: 'x' });
    const suppressConfig: PemsRequestConfig = { suppressSessionExpiredToast: true };

    await expect(httpClient.get('/auth/me', suppressConfig)).rejects.toBeTruthy();

    expect(showMessageErrorToastMock).not.toHaveBeenCalled();
  });

  it('still clears auth state and fires AUTH_EXPIRED_EVENT even when the toast is suppressed', async () => {
    mockAdapterFor('/auth/me', 401, { success: false, errorCode: 'SESSION_REVOKED', message: 'x' });
    const eventHandler = vi.fn();
    window.addEventListener(AUTH_EXPIRED_EVENT, eventHandler);
    const suppressConfig: PemsRequestConfig = { suppressSessionExpiredToast: true };

    await expect(httpClient.get('/auth/me', suppressConfig)).rejects.toBeTruthy();

    expect(authStorage.getAccessToken()).toBeNull();
    expect(eventHandler).toHaveBeenCalledTimes(1);
    window.removeEventListener(AUTH_EXPIRED_EVENT, eventHandler);
  });

  it('never suppresses the toast for a call that does not opt in (default false)', async () => {
    mockAdapterFor('/delegations/viewguestdelegationlist', 401, { success: false, errorCode: 'SESSION_REVOKED', message: 'x' });

    await expect(httpClient.get('/delegations/viewguestdelegationlist')).rejects.toBeTruthy();

    expect(showMessageErrorToastMock).toHaveBeenCalledTimes(1);
  });
});

describe('httpClient response interceptor — global bootstrap window (setAuthBootstrapping)', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    authStorage.clear();
    authStorage.setTokens('stale-access-token', '');
    setAuthBootstrapping(false);
  });
  afterEach(() => {
    httpClient.defaults.adapter = undefined;
    setAuthBootstrapping(false);
  });

  it('suppresses the toast for ANY request that 401s while bootstrapping is true — not just /auth/me', async () => {
    // Simulates an ambient consumer (e.g. NotificationsProvider, the header avatar fetch) racing
    // AuthContext's bootstrap check, hitting an unrelated protected endpoint with the SAME stale
    // token, before bootstrap has corrected the optimistic initial auth state.
    mockAdapterFor('/notifications/unread-count', 401, { success: false, errorCode: 'SESSION_REVOKED', message: 'x' });
    setAuthBootstrapping(true);

    await expect(httpClient.get('/notifications/unread-count')).rejects.toBeTruthy();

    expect(showMessageErrorToastMock).not.toHaveBeenCalled();
  });

  it('resumes showing the toast once bootstrapping ends', async () => {
    mockAdapterFor('/notifications/unread-count', 401, { success: false, errorCode: 'SESSION_REVOKED', message: 'x' });
    setAuthBootstrapping(true);
    setAuthBootstrapping(false);

    await expect(httpClient.get('/notifications/unread-count')).rejects.toBeTruthy();

    expect(showMessageErrorToastMock).toHaveBeenCalledTimes(1);
  });
});
