import React from 'react';
import { Navigate, Outlet, useLocation } from 'react-router-dom';
import { useAuth } from '../hooks/useAuth';
import type { LoginPortal, PermissionLevel } from '../../features/authentication/types/authentication.types';

interface ProtectedRouteProps {
  children?: React.ReactNode;
  /** Restrict to specific role codes (ADMIN, HO, STAFF, ...). */
  roles?: string[];
  /** Require a permission code (optionally at a minimum level, default R). */
  permission?: string;
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
export function ProtectedRoute({ children, roles, permission, permissionLevel }: ProtectedRouteProps) {
  const { isAuthenticated, isLoading, user, hasRole, hasPermission } = useAuth();
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

  if (roles && roles.length > 0 && !hasRole(roles)) {
    return <Navigate to="/403" replace />;
  }

  if (permission && !hasPermission(permission, permissionLevel ?? 'R')) {
    return <Navigate to="/403" replace />;
  }

  const currentPortal = useAuth().loginPortal;
  if (portals && portals.length > 0 && currentPortal && !portals.includes(currentPortal)) {
    return <Navigate to="/403" replace />;
  }

  return <>{children ?? <Outlet />}</>;
}

export default ProtectedRoute;
