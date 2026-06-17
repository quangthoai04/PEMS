import React, { createContext, useCallback, useContext, useEffect, useMemo, useState } from 'react';
import { authenticationApi } from '../../features/authentication/api/authenticationApi';
import type {
  AuthUser,
  ChangePasswordRequest,
  LoginPortal,
  PermissionLevel,
  UserPermission,
} from '../../features/authentication/types/authentication.types';
import { authStorage, AUTH_EXPIRED_EVENT } from './authStorage';
import { hasAnyPermission, hasPermission, hasRole } from './permissionChecker';

interface AuthContextValue {
  user: AuthUser | null;
  permissions: UserPermission[];
  isAuthenticated: boolean;
  isLoading: boolean;

  login: (email: string, password: string, loginPortal: LoginPortal) => Promise<AuthUser>;
  loginWithGoogle: (idToken: string, loginPortal: LoginPortal) => Promise<AuthUser>;
  logout: () => Promise<void>;
  refreshProfile: () => Promise<void>;
  changePassword: (payload: ChangePasswordRequest) => Promise<void>;

  hasPermission: (code: string, minimumLevel?: PermissionLevel) => boolean;
  hasAnyPermission: (codes: string[], minimumLevel?: PermissionLevel) => boolean;
  hasRole: (roles: string[]) => boolean;
}

const AuthContext = createContext<AuthContextValue | undefined>(undefined);

export function AuthProvider({ children }: { children: React.ReactNode }) {
  const [user, setUser] = useState<AuthUser | null>(() => authStorage.getUser());
  const [permissions, setPermissions] = useState<UserPermission[]>(() => authStorage.getPermissions());
  const [isLoading, setIsLoading] = useState<boolean>(() => !!authStorage.getAccessToken());

  const applySession = useCallback((nextUser: AuthUser, nextPermissions: UserPermission[]) => {
    authStorage.setUser(nextUser);
    authStorage.setPermissions(nextPermissions);
    setUser(nextUser);
    setPermissions(nextPermissions);
  }, []);

  const clearSession = useCallback(() => {
    authStorage.clear();
    setUser(null);
    setPermissions([]);
  }, []);

  // Validate the stored token on first load by fetching the live profile.
  useEffect(() => {
    let cancelled = false;

    async function bootstrap() {
      if (!authStorage.getAccessToken()) {
        setIsLoading(false);
        return;
      }
      try {
        const profile = await authenticationApi.getMe();
        if (!cancelled) applySession(profile.user, profile.permissions);
      } catch {
        if (!cancelled) clearSession();
      } finally {
        if (!cancelled) setIsLoading(false);
      }
    }

    bootstrap();
    return () => {
      cancelled = true;
    };
  }, [applySession, clearSession]);

  // The http client dispatches this when a refresh ultimately fails.
  useEffect(() => {
    const handler = () => {
      setUser(null);
      setPermissions([]);
    };
    window.addEventListener(AUTH_EXPIRED_EVENT, handler);
    return () => window.removeEventListener(AUTH_EXPIRED_EVENT, handler);
  }, []);

  const login = useCallback(
    async (email: string, password: string, loginPortal: LoginPortal) => {
      const result = await authenticationApi.login(email, password, loginPortal);
      authStorage.setTokens(result.accessToken, result.refreshToken);
      applySession(result.user, result.permissions);
      return result.user;
    },
    [applySession],
  );

  const loginWithGoogle = useCallback(
    async (idToken: string, loginPortal: LoginPortal) => {
      const result = await authenticationApi.loginWithGoogle(idToken, loginPortal);
      authStorage.setTokens(result.accessToken, result.refreshToken);
      applySession(result.user, result.permissions);
      return result.user;
    },
    [applySession],
  );

  const logout = useCallback(async () => {
    try {
      await authenticationApi.logout(authStorage.getRefreshToken());
    } catch {
      // Ignore network/expired errors — we always clear locally.
    } finally {
      clearSession();
    }
  }, [clearSession]);

  const refreshProfile = useCallback(async () => {
    const profile = await authenticationApi.getMe();
    applySession(profile.user, profile.permissions);
  }, [applySession]);

  const changePassword = useCallback(async (payload: ChangePasswordRequest) => {
    await authenticationApi.changePassword(payload);
    // Reflect must_change_password = false locally.
    setUser((prev) => {
      if (!prev) return prev;
      const next = { ...prev, mustChangePassword: false, mustSetPassword: false };
      authStorage.setUser(next);
      return next;
    });
  }, []);

  const value = useMemo<AuthContextValue>(
    () => ({
      user,
      permissions,
      isAuthenticated: !!user,
      isLoading,
      login,
      loginWithGoogle,
      logout,
      refreshProfile,
      changePassword,
      hasPermission: (code, minimumLevel) => hasPermission(permissions, code, minimumLevel),
      hasAnyPermission: (codes, minimumLevel) => hasAnyPermission(permissions, codes, minimumLevel),
      hasRole: (roles) => hasRole(user?.roleCode, roles),
    }),
    [user, permissions, isLoading, login, loginWithGoogle, logout, refreshProfile, changePassword],
  );

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuthContext(): AuthContextValue {
  const ctx = useContext(AuthContext);
  if (!ctx) {
    throw new Error('useAuthContext must be used within an <AuthProvider>.');
  }
  return ctx;
}
