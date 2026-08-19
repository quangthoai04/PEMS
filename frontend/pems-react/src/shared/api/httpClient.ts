import axios, { AxiosError, AxiosRequestConfig } from 'axios';
import { authInterceptor } from './authInterceptor';
import { PUBLIC_AUTH_PATHS } from './authPaths';
import { authStorage, AUTH_EXPIRED_EVENT } from '../auth/authStorage';
import { showMessageErrorToast } from '../utils/toast';
import i18n from '../i18n/config';

const baseURL = import.meta.env.VITE_API_BASE_URL || '/api';

const httpClient = axios.create({
  baseURL,
});

/**
 * Per-request escape hatch from the generic "Phiên đăng nhập đã hết hạn" toast below.
 *
 * A 401 from a genuinely PROTECTED endpoint (e.g. `/auth/me`) is correct — the caller must still
 * clear auth state and redirect. But some callers make that same request purely to CHECK whether a
 * stored token is still good, and already handle a negative answer silently on their own (e.g.
 * AuthContext's bootstrap effect: try `/auth/me`, clear session quietly on failure). Without this
 * flag, a guest sitting on the public homepage with a stale/revoked token from a previous visit saw
 * a misleading "session expired" toast on every reload — they were never signed in on THIS visit at
 * all, bootstrap was just verifying an old token in the background. `AUTH_EXPIRED_EVENT` still
 * fires either way (other listeners still need to know), only the toast is suppressed.
 */
export interface PemsRequestConfig extends AxiosRequestConfig {
  suppressSessionExpiredToast?: boolean;
}

/**
 * True while AuthContext's bootstrap effect is still verifying a stored token (from mount until
 * its `/auth/me` call settles). AuthContext seeds `user`/`isAuthenticated` OPTIMISTICALLY and
 * synchronously from localStorage — on purpose, so a hard refresh with a genuinely valid session
 * never flashes a logged-out UI — but that same optimistic value is what OTHER ambient consumers
 * (NotificationsProvider's poll, the header avatar's `useAuthenticatedImage`, ...) read to decide
 * whether to fire their own authenticated request, before bootstrap has had a chance to correct it.
 * A stale/revoked session then made EACH of those independently 401 and show this file's generic
 * toast. Per-request `suppressSessionExpiredToast` covers bootstrap's own call; this wider window
 * is the safety net for every OTHER request that races it — the toast is suppressed, but the
 * request itself still runs and still fails, and `AUTH_EXPIRED_EVENT` still fires either way.
 */
let authBootstrapping = false;
export function setAuthBootstrapping(value: boolean): void {
  authBootstrapping = value;
}

httpClient.interceptors.request.use((config) => {
  const language = localStorage.getItem('pems.language') || 'vi';
  config.headers['Accept-Language'] = language;
  return config;
});

httpClient.interceptors.request.use(authInterceptor);

// Endpoints that must never trigger a refresh-retry loop: every PUBLIC_AUTH_PATHS endpoint (a
// 401 there is the auth attempt's own answer, not an expired session) plus `/auth/logout` — that
// one IS `[Authorize]` (so authInterceptor still attaches the bearer, hence it is not in
// PUBLIC_AUTH_PATHS), but retrying a failed logout via refresh-then-retry is pointless: the
// session is already being torn down, and AuthContext.logout() clears local state regardless of
// whether the server call succeeds.
const NO_REFRESH_PATHS = [...PUBLIC_AUTH_PATHS, '/auth/logout'];

// A deliberate logout revokes the session server-side; any OTHER request already in
// flight at that moment (e.g. notification polling) legitimately gets a 401 back from
// the session-validation middleware even though nothing actually "expired". Without this
// guard that 401 surfaces the generic "Phiên đăng nhập đã hết hạn" toast on top of the
// "Đăng xuất thành công" one the user already sees. AuthContext.logout() calls
// markDeliberateLogout() right before it starts; the window only needs to cover the
// brief moment between the server revoking the session and any stray requests failing.
let deliberateLogoutUntil = 0;
export function markDeliberateLogout(): void {
  deliberateLogoutUntil = Date.now() + 5000;
}

// Single-flight refresh so concurrent 401s share one refresh request.
let refreshPromise: Promise<string | null> | null = null;

async function performRefresh(): Promise<string | null> {
  const refreshToken = authStorage.getRefreshToken();
  if (!refreshToken) return null;

  try {
    // Use a bare axios call so this request does not pass through the
    // response interceptor (which would recurse on another 401).
    const { data } = await axios.post(
      `${baseURL}/auth/refresh`,
      { refreshToken },
      { headers: { 'Content-Type': 'application/json' } },
    );
    authStorage.setTokens(data.accessToken, data.refreshToken);
    return data.accessToken as string;
  } catch {
    return null;
  }
}

/**
 * UC-86 force-logout (BR-AUTH-CAMPUS-08): sessionStorage key read once by the login page to
 * explain WHY the user was signed out ("cơ sở đã ngừng hoạt động").
 */
export const FORCED_LOGOUT_REASON_KEY = 'pems.forcedLogoutReason';

httpClient.interceptors.response.use(
  (response) => response,
  async (error: AxiosError) => {
    const original = error.config as (PemsRequestConfig & { _retry?: boolean }) | undefined;
    const status = error.response?.status;
    const url = original?.url ?? '';
    const suppressSessionExpiredToast = original?.suppressSessionExpiredToast === true || authBootstrapping;

    const isAuthPath = NO_REFRESH_PATHS.some((p) => url.includes(p));

    // UC-86 force-logout: the backend denies the account because its campus is INACTIVE
    // (403 + CAMPUS_INACTIVE_ACCESS_DENIED from the session middleware / refresh / login gate).
    // Clear ALL auth state and send the user to the login page — never keep them on the
    // dashboard with a toast. Auth-form endpoints are excluded: their pages render the error
    // inline and there is no signed-in state to clear. Other 403 codes never trigger this.
    const errorBody = error.response?.data as { errorCode?: string; message?: string } | undefined;
    if (status === 403 && errorBody?.errorCode === 'CAMPUS_INACTIVE_ACCESS_DENIED' && !isAuthPath) {
      sessionStorage.setItem(
        FORCED_LOGOUT_REASON_KEY,
        errorBody.message || 'Cơ sở của tài khoản hiện đã ngừng hoạt động. Vui lòng liên hệ Head Office để được hỗ trợ.',
      );
      authStorage.clear();
      window.dispatchEvent(new Event(AUTH_EXPIRED_EVENT));
      return Promise.reject(error);
    }

    if (status === 401 && !isAuthPath) {
      const isDeliberateLogout = Date.now() < deliberateLogoutUntil;

      if (!original || original._retry || !authStorage.getRefreshToken()) {
        authStorage.clear();
        window.dispatchEvent(new Event(AUTH_EXPIRED_EVENT));
        if (!isDeliberateLogout && !suppressSessionExpiredToast) showMessageErrorToast(i18n.t('toast:http.401'), 'session-expired');
        return Promise.reject(error);
      }

      original._retry = true;

      const pending = refreshPromise ?? (refreshPromise = performRefresh().finally(() => {
        refreshPromise = null;
      }));
      const newAccessToken = await pending;

      if (newAccessToken) {
        original.headers = original.headers ?? {};
        (original.headers as Record<string, string>).Authorization = `Bearer ${newAccessToken}`;
        return httpClient(original);
      }

      // Refresh failed → clear auth and notify the app to redirect to /login.
      authStorage.clear();
      window.dispatchEvent(new Event(AUTH_EXPIRED_EVENT));
      if (!isDeliberateLogout && !suppressSessionExpiredToast) showMessageErrorToast(i18n.t('toast:http.401'), 'session-expired');
    }

    return Promise.reject(error);
  },
);

export default httpClient;
