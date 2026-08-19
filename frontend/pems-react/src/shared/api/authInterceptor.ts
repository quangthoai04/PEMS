import { PUBLIC_AUTH_PATHS } from './authPaths';

/**
 * A public/anonymous auth endpoint (login, refresh, forgot/reset-password, ...) must be able to
 * accept NEW credentials on its own terms — an old, revoked, or otherwise stale session sitting
 * in `localStorage` from a previous visit is never a reason to refuse the attempt. Attaching a
 * dead Bearer token to these requests used to let the backend's session/lifetime validation
 * reject the request BEFORE the new credentials were even checked, surfacing an unlocalized
 * "Authentication required." / "Your session has been revoked." toast on top of a login screen
 * the user had just filled in correctly — see PEMS_Login401_StaleAuth_I18n_Fix (auth 401 audit).
 */
export const authInterceptor = (config: any) => {
  const url: string = config?.url ?? '';
  const isPublicAuthPath = PUBLIC_AUTH_PATHS.some((p) => url.includes(p));
  if (isPublicAuthPath) return config;

  const token = localStorage.getItem('token');
  if (token) {
    config.headers.Authorization = `Bearer ${token}`;
  }
  return config;
};
