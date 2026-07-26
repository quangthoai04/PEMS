import axios, { AxiosError, AxiosRequestConfig } from 'axios';
import { authInterceptor } from './authInterceptor';
import { authStorage, AUTH_EXPIRED_EVENT } from '../auth/authStorage';
import { showMessageErrorToast } from '../utils/toast';
import i18n from '../i18n/config';

const baseURL = import.meta.env.VITE_API_BASE_URL || '/api';

const httpClient = axios.create({
  baseURL,
});

httpClient.interceptors.request.use((config) => {
  const language = localStorage.getItem('pems.language') || 'vi';
  config.headers['Accept-Language'] = language;
  return config;
});

httpClient.interceptors.request.use(authInterceptor);

// Endpoints that must never trigger a refresh-retry loop.
const NO_REFRESH_PATHS = [
  '/auth/login',
  '/auth/google',
  '/auth/refresh',
  '/auth/forgot-password',
  '/auth/reset-password',
  '/auth/logout',
];

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
    const original = error.config as (AxiosRequestConfig & { _retry?: boolean }) | undefined;
    const status = error.response?.status;
    const url = original?.url ?? '';

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
        if (!isDeliberateLogout) showMessageErrorToast(i18n.t('toast:http.401'), 'session-expired');
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
      if (!isDeliberateLogout) showMessageErrorToast(i18n.t('toast:http.401'), 'session-expired');
    }

    return Promise.reject(error);
  },
);

export default httpClient;
