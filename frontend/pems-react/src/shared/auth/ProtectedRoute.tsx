import React from 'react';
import { Navigate, Outlet, useLocation } from 'react-router-dom';
import { useAuth } from '../hooks/useAuth';
import { resolveEffectiveRole } from './resolveEffectiveRole';
import type { LoginPortal, PermissionLevel } from '../../features/authentication/types/authentication.types';

interface ProtectedRouteProps {
  children?: React.ReactNode;
  /** Restrict to specific role codes (ADMIN, HO, STAFF, ...). */
  roles?: string[];
  /** Require a single permission code (optionally at a minimum level, default R). */
  permission?: string;
  /** Require ANY ONE of these permission codes (at the minimum level, default R). */
  anyPermissions?: string[];
  permissionLevel?: PermissionLevel;
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
 * Guards a route. Redirects unauthenticated users to /login, users who must
 * change their password to /change-password, and users lacking the required
 * role/permission to /403. The backend still enforces every protected action.
 */
export function ProtectedRoute({
  children,
  roles,
  permission,
  anyPermissions,
  permissionLevel,
  portals,
}: ProtectedRouteProps) {
  // All hooks must run unconditionally and before any early return — calling a
  // hook after a conditional `return` violates the Rules of Hooks and crashes
  // the whole tree ("Rendered more hooks than during the previous render"),
  // which is what blanked the screen after login.
  const { isAuthenticated, isLoading, user, hasRole, hasPermission, hasAnyPermission, loginPortal } = useAuth();
  const location = useLocation();

  if (isLoading) {
    return <FullScreenLoader />;
  }

  if (!isAuthenticated || !user) {
    return <Navigate to="/login" state={{ from: location }} replace />;
  }

  // Force password change before accessing any other protected page.
  const mustChange = user.mustChangePassword || user.mustSetPassword;
  if (mustChange && location.pathname !== '/change-password') {
    return <Navigate to="/change-password" replace />;
  }

  // Accounts whose (roleCode + subRole) cannot be mapped to a valid workspace
  // (e.g. STAFF/DEPT missing a Leader/Staff sub-role) get no implicit access.
  // We still let them reach /change-password so a forced reset isn't blocked.
  const effectiveRole = resolveEffectiveRole(user);
  if (!effectiveRole && location.pathname !== '/change-password') {
    return <Navigate to="/invalid-account" replace />;
  }

  if (roles && roles.length > 0 && !hasRole(roles)) {
    return <Navigate to="/403" replace />;
  }

  if (permission && !hasPermission(permission, permissionLevel ?? 'R')) {
    return <Navigate to="/403" replace />;
  }

  if (anyPermissions && anyPermissions.length > 0 && !hasAnyPermission(anyPermissions, permissionLevel ?? 'R')) {
    return <Navigate to="/403" replace />;
  }

  if (portals && portals.length > 0 && loginPortal && !portals.includes(loginPortal)) {
    return <Navigate to="/403" replace />;
  }

  return <>{children ?? <Outlet />}</>;
}

export default ProtectedRoute;
