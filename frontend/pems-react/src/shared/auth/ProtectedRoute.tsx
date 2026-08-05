import React from 'react';
import { Navigate, Outlet, useLocation } from 'react-router-dom';
import { useAuth } from '../hooks/useAuth';
import { canAccessDashboardRoute, type DashboardRouteKey } from './dashboardRouteAccess';
import type { EffectiveRole } from './resolveEffectiveRole';
import type { LoginPortal } from '../../features/authentication/types/authentication.types';

interface ProtectedRouteProps {
  children?: React.ReactNode;

  /**
   * Preferred way to guard a dashboard route: name the route and let
   * `dashboardRouteAccess` decide. Keeps guard, Sidebar and 403 fallback reading one table.
   */
  routeKey?: DashboardRouteKey;

  /**
   * Ad-hoc effective-role list for the few routes that are not dashboard entries.
   * Prefer `routeKey` — a route allowed by a list written inline here is invisible to
   * the Sidebar/route parity test.
   */
  effectiveRoles?: EffectiveRole[];

  /**
   * Legacy raw `roleCode` list. It cannot distinguish Staff Leader from Staff, or
   * Department Lead from Department staff, so it must not be used for new routes.
   * @deprecated use `routeKey`.
   */
  roles?: string[];

  /** Restrict by login portal */
  portals?: LoginPortal[];
}

function FullScreenLoader() {
  return (
    <div className="min-h-screen flex items-center justify-center bg-white">
      <div className="flex flex-col items-center gap-3">
        <div className="w-10 h-10 border-4 border-[#004c91]/20 border-t-[#004c91] rounded-full animate-spin" />
        <p className="text-sm text-gray-500 font-medium">Đang tải...</p>
      </div>
    </div>
  );
}

/**
 * Guards a route. Redirects unauthenticated users to the auth entry, users who must change
 * their password to /change-password, accounts with an unmappable role to /invalid-account,
 * and users lacking the route's role to /403.
 *
 * This is a UX gate, not the security boundary: it stops the page mounting and firing its
 * API calls. The backend refuses the same request independently, and handlers still check
 * object scope, so a user who bypasses this in devtools gains nothing.
 */
export function ProtectedRoute({
  children,
  routeKey,
  effectiveRoles,
  roles,
  portals,
}: ProtectedRouteProps) {
  // All hooks must run unconditionally and before any early return — calling a
  // hook after a conditional `return` violates the Rules of Hooks and crashes
  // the whole tree ("Rendered more hooks than during the previous render"),
  // which is what blanked the screen after login.
  const { isAuthenticated, isLoading, isReady, user, effectiveRole, hasRole, loginPortal } = useAuth();
  const location = useLocation();

  // Wait for bootstrap: until the profile has been re-fetched we cannot tell "signed out"
  // from "not loaded yet", and guessing would bounce the user off a page they can access.
  if (isLoading || !isReady) {
    return <FullScreenLoader />;
  }

  if (!isAuthenticated || !user) {
    return <Navigate to="/" state={{ from: location }} replace />;
  }

  // Force password change before accessing any other protected page.
  const mustChange = user.mustChangePassword || user.mustSetPassword;
  if (mustChange && location.pathname !== '/change-password') {
    return <Navigate to="/change-password" replace />;
  }

  // Accounts whose (roleCode + subRole) cannot be mapped to a valid workspace
  // (e.g. STAFF/DEPARTMENT missing a Leader/Staff sub-role) get no implicit access.
  // We still let them reach /change-password so a forced reset isn't blocked.
  if (!effectiveRole && location.pathname !== '/change-password') {
    return <Navigate to="/invalid-account" replace />;
  }

  if (routeKey && !canAccessDashboardRoute(effectiveRole, routeKey)) {
    return <Navigate to="/403" replace />;
  }

  if (effectiveRoles && effectiveRoles.length > 0) {
    if (!effectiveRole || !effectiveRoles.includes(effectiveRole)) {
      return <Navigate to="/403" replace />;
    }
  }

  if (roles && roles.length > 0 && !hasRole(roles)) {
    return <Navigate to="/403" replace />;
  }

  if (portals && portals.length > 0 && loginPortal && !portals.includes(loginPortal)) {
    return <Navigate to="/403" replace />;
  }

  return <>{children ?? <Outlet />}</>;
}

export default ProtectedRoute;
