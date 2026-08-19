import React, { createContext, useCallback, useContext, useEffect, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { showSuccessToast } from '../utils/toast';
import { authenticationApi } from '../../features/authentication/api/authenticationApi';
import type {
  AuthUser,
  ChangePasswordRequest,
  LoginPortal,
} from '../../features/authentication/types/authentication.types';
import { authStorage, AUTH_EXPIRED_EVENT } from './authStorage';
import { hasRole } from './permissionChecker';
import { resolveEffectiveRole, type EffectiveRole } from './resolveEffectiveRole';
import { markDeliberateLogout, setAuthBootstrapping } from '../api/httpClient';

interface AuthContextValue {
  user: AuthUser | null;

  loginPortal: LoginPortal | null;
  selectedCampusId: string | null;
  isAuthenticated: boolean;
  isLoading: boolean;
  /**
   * True once auth bootstrap has settled, so guards know whether `user` being null
   * means "signed out" or merely "profile still loading". Route guards must not
   * decide anything before this flips — otherwise a hard refresh briefly renders as
   * unauthenticated and bounces the user off the page they asked for.
   */
  isReady: boolean;

  /**
   * The single role value the UI is allowed to make decisions from, derived from the
   * profile the backend returned. `null` means the account's (roleCode, subRole) pair
   * is not a valid workspace — callers must route to /invalid-account, never guess.
   */
  effectiveRole: EffectiveRole | null;

  login: (email: string, password: string) => Promise<AuthUser>;
  loginWithGoogle: (idToken: string) => Promise<AuthUser>;
  logout: () => Promise<void>;
  refreshProfile: () => Promise<void>;
  changePassword: (payload: ChangePasswordRequest) => Promise<void>;
  /** Patch the current user in place (e.g. after an avatar upload) and persist to storage. */
  updateUser: (patch: Partial<AuthUser>) => void;

  /**
   * Raw `roleCode` check. Does NOT see sub_role, so it cannot tell a Staff Leader from a
   * Staff, or a Department Lead from Department staff. Kept for legacy display code only —
   * use `hasEffectiveRole` for anything that decides access.
   */
  hasRole: (roles: string[]) => boolean;

  /** Role check on the resolved effective role. This is what authorization should use. */
  hasEffectiveRole: (roles: EffectiveRole[]) => boolean;
}

const AuthContext = createContext<AuthContextValue | undefined>(undefined);

export function AuthProvider({ children }: { children: React.ReactNode }) {
  const { t } = useTranslation(['toast']);
  const [user, setUser] = useState<AuthUser | null>(() => authStorage.getUser());

  const [loginPortal, setLoginPortal] = useState<LoginPortal | null>(() => authStorage.getLoginPortal());
  const [selectedCampusId, setSelectedCampusId] = useState<string | null>(() => authStorage.getSelectedCampusId());
  const [isLoading, setIsLoading] = useState<boolean>(() => !!authStorage.getAccessToken());
  // Separate from isLoading: bootstrap runs once, and until it has finished the stored
  // user is unverified. Guards wait on this so a refresh doesn't flash a redirect.
  const [isReady, setIsReady] = useState<boolean>(() => !authStorage.getAccessToken());

  const applySession = useCallback((nextUser: AuthUser) => {
    authStorage.setUser(nextUser);
    setUser(nextUser);
  }, []);

  const clearSession = useCallback(() => {
    authStorage.clear();
    setUser(null);

    setLoginPortal(null);
    setSelectedCampusId(null);
  }, []);

  // Validate the stored token on first load by fetching the live profile.
  useEffect(() => {
    let cancelled = false;

    async function bootstrap() {
      if (!authStorage.getAccessToken()) {
        setIsLoading(false);
        setIsReady(true);
        return;
      }
      // Opens the window (see httpClient.ts) other ambient consumers race against — ANY request
      // that 401s while this is true (not just this call) has its "session expired" toast
      // suppressed too, since none of them were asked for by something the user actually did.
      setAuthBootstrapping(true);
      try {
        // suppressSessionExpiredToast: this call only VERIFIES a stored token is still good — the
        // user never asked for anything auth-specific on this page load. A stale/revoked token
        // (previous visit, dev backend restart, a revoked session) is handled silently below
        // (clearSession(), no toast); without the flag the shared 401 interceptor would show a
        // misleading "session expired" toast even to a guest who was never signed in this visit.
        const profile = await authenticationApi.getMe({ suppressSessionExpiredToast: true });
        if (!cancelled) applySession(profile.user);
      } catch {
        if (!cancelled) clearSession();
      } finally {
        setAuthBootstrapping(false);
        if (!cancelled) {
          setIsLoading(false);
          setIsReady(true);
        }
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

      setLoginPortal(null);
      setSelectedCampusId(null);
    };
    window.addEventListener(AUTH_EXPIRED_EVENT, handler);
    return () => window.removeEventListener(AUTH_EXPIRED_EVENT, handler);
  }, []);

  // Portal/campus are no longer chosen by the caller — the backend resolves them from the
  // account and returns them as part of the user (roleCode / primaryCampusId). Derive the
  // local tracking state from that response instead of a client-supplied value.
  const applyLoginResult = useCallback(
    (result: AuthUser) => {
      const portal: LoginPortal = result.roleCode === 'VISITOR' ? 'VISITOR' : 'INTERNAL';
      authStorage.setLoginPortal(portal);
      if (portal === 'INTERNAL' && result.primaryCampusId) {
        authStorage.setSelectedCampusId(result.primaryCampusId);
        setSelectedCampusId(result.primaryCampusId);
      } else {
        authStorage.clearSelectedCampusId();
        setSelectedCampusId(null);
      }
      setLoginPortal(portal);
      applySession(result);
    },
    [applySession],
  );

  const login = useCallback(
    async (email: string, password: string) => {
      const result = await authenticationApi.login(email, password);
      authStorage.setTokens(result.accessToken, result.refreshToken);
      applyLoginResult(result.user);
      return result.user;
    },
    [applyLoginResult],
  );

  const loginWithGoogle = useCallback(
    async (idToken: string) => {
      const result = await authenticationApi.loginWithGoogle(idToken);
      authStorage.setTokens(result.accessToken, result.refreshToken);
      applyLoginResult(result.user);
      return result.user;
    },
    [applyLoginResult],
  );

  const logout = useCallback(async () => {
    // Revoking the session server-side can make any OTHER request already in flight
    // (e.g. notification polling) legitimately 401 a moment later — mark this a
    // deliberate logout so httpClient doesn't surface a confusing "session expired"
    // toast on top of the logout-success one below.
    markDeliberateLogout();
    try {
      await authenticationApi.logout(authStorage.getRefreshToken());
      showSuccessToast(t('toast:auth.logoutSuccess'), 'auth-status', 2000);
    } catch {
      // Ignore network/expired errors — we always clear locally.
      showSuccessToast(t('toast:auth.logoutSuccess'), 'auth-status', 2000);
    } finally {
      clearSession();
    }
  }, [clearSession, t]);

  const refreshProfile = useCallback(async () => {
    const profile = await authenticationApi.getMe();
    applySession(profile.user);
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

  const updateUser = useCallback((patch: Partial<AuthUser>) => {
    setUser((prev) => {
      if (!prev) return prev;
      const next = { ...prev, ...patch };
      authStorage.setUser(next); // persists pems_user + the legacy currentUser mirror
      return next;
    });
  }, []);

  // Derived from the context user (which bootstrap re-fetches from the backend), never
  // from localStorage. Editing `currentUser` in devtools therefore cannot change what the
  // UI thinks the role is.
  const effectiveRole = useMemo(() => resolveEffectiveRole(user), [user]);

  const value = useMemo<AuthContextValue>(
    () => ({
      user,

      loginPortal,
      selectedCampusId,
      isAuthenticated: !!user,
      isLoading,
      isReady,
      effectiveRole,
      login,
      loginWithGoogle,
      logout,
      refreshProfile,
      changePassword,
      updateUser,

      hasRole: (roles) => hasRole(user?.roleCode, roles),
      hasEffectiveRole: (roles) => !!effectiveRole && roles.includes(effectiveRole),
    }),
    [user, loginPortal, selectedCampusId, isLoading, isReady, effectiveRole, login, loginWithGoogle, logout, refreshProfile, changePassword, updateUser],
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
