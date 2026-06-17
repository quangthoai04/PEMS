import type { AuthUser, UserPermission } from '../../features/authentication/types/authentication.types';

// localStorage keys. 'token' is kept as the access-token key so the existing
// request interceptor keeps working. 'currentUser' mirrors the auth user in the
// shape the existing dashboard pages already read.
const ACCESS_TOKEN_KEY = 'token';
const REFRESH_TOKEN_KEY = 'refreshToken';
const USER_KEY = 'pems_user';
const PERMISSIONS_KEY = 'pems_permissions';
const LEGACY_USER_KEY = 'currentUser';

function readJson<T>(key: string): T | null {
  const raw = localStorage.getItem(key);
  if (!raw) return null;
  try {
    return JSON.parse(raw) as T;
  } catch {
    return null;
  }
}

/**
 * Mirror the authenticated user into the legacy `currentUser` object so the
 * existing dashboard/header components (which read localStorage directly) keep
 * working unchanged.
 */
function writeLegacyUser(user: AuthUser) {
  const legacy = {
    userId: user.userId,
    name: user.fullName,
    email: user.email,
    role: user.roleCode,
    subRole: user.subRole ?? undefined,
    campus: user.campusName ?? user.campusCode ?? '',
    departmentId: user.departmentId ?? undefined,
    avatarUrl: user.avatarUrl ?? undefined,
  };
  localStorage.setItem(LEGACY_USER_KEY, JSON.stringify(legacy));
}

export const authStorage = {
  getAccessToken: () => localStorage.getItem(ACCESS_TOKEN_KEY),
  setAccessToken: (token: string) => localStorage.setItem(ACCESS_TOKEN_KEY, token),

  getRefreshToken: () => localStorage.getItem(REFRESH_TOKEN_KEY),
  setRefreshToken: (token: string) => localStorage.setItem(REFRESH_TOKEN_KEY, token),

  getUser: () => readJson<AuthUser>(USER_KEY),
  setUser: (user: AuthUser) => {
    localStorage.setItem(USER_KEY, JSON.stringify(user));
    writeLegacyUser(user);
  },

  getPermissions: () => readJson<UserPermission[]>(PERMISSIONS_KEY) ?? [],
  setPermissions: (permissions: UserPermission[]) =>
    localStorage.setItem(PERMISSIONS_KEY, JSON.stringify(permissions)),

  setTokens: (accessToken: string, refreshToken: string) => {
    localStorage.setItem(ACCESS_TOKEN_KEY, accessToken);
    localStorage.setItem(REFRESH_TOKEN_KEY, refreshToken);
  },

  clear: () => {
    localStorage.removeItem(ACCESS_TOKEN_KEY);
    localStorage.removeItem(REFRESH_TOKEN_KEY);
    localStorage.removeItem(USER_KEY);
    localStorage.removeItem(PERMISSIONS_KEY);
    localStorage.removeItem(LEGACY_USER_KEY);
  },

  // Back-compat helpers (older code referenced these names).
  getToken: () => localStorage.getItem(ACCESS_TOKEN_KEY),
  setToken: (token: string) => localStorage.setItem(ACCESS_TOKEN_KEY, token),
  clearToken: () => localStorage.removeItem(ACCESS_TOKEN_KEY),
};

export const AUTH_EXPIRED_EVENT = 'pems:auth-expired';
