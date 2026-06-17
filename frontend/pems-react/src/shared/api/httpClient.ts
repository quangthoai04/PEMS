import axios, { AxiosError, AxiosRequestConfig } from 'axios';
import { authInterceptor } from './authInterceptor';
import { authStorage, AUTH_EXPIRED_EVENT } from '../auth/authStorage';

const baseURL = import.meta.env.VITE_API_BASE_URL || '/api';

const httpClient = axios.create({
  baseURL,
  headers: {
    'Content-Type': 'application/json',
  },
});

httpClient.interceptors.request.use(authInterceptor);

// Endpoints that must never trigger a refresh-retry loop.
const NO_REFRESH_PATHS = [
  '/auth/login',
  '/auth/google',
  '/auth/refresh',
  '/auth/forgot-password',
  '/auth/reset-password',
];

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

httpClient.interceptors.response.use(
  (response) => response,
  async (error: AxiosError) => {
    const original = error.config as (AxiosRequestConfig & { _retry?: boolean }) | undefined;
    const status = error.response?.status;
    const url = original?.url ?? '';

    const isAuthPath = NO_REFRESH_PATHS.some((p) => url.includes(p));

    if (status === 401 && original && !original._retry && !isAuthPath && authStorage.getRefreshToken()) {
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
    }

    return Promise.reject(error);
  },
);

export default httpClient;
