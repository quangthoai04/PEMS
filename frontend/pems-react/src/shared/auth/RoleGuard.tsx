import React from 'react';
import { useAuth } from '../hooks/useAuth';
import type { PermissionLevel } from '../../features/authentication/types/authentication.types';

interface RoleGuardProps {
  children: React.ReactNode;
  /** Show children only if the user has one of these roles. */
  roles?: string[];
  /** Show children only if the user has this permission (at >= level, default R). */
  permission?: string;
  permissionLevel?: PermissionLevel;
  /** Rendered when the check fails (defaults to nothing). */
  fallback?: React.ReactNode;
}

/**
 * Inline visibility guard for menu items / buttons / sections. Hides UI the user
 * cannot use — this is UX only; the backend still authorizes every request.
 */
export function RoleGuard({ children, roles, permission, permissionLevel, fallback = null }: RoleGuardProps) {
  const { hasRole, hasPermission } = useAuth();

  const roleOk = !roles || roles.length === 0 || hasRole(roles);
  const permissionOk = !permission || hasPermission(permission, permissionLevel ?? 'R');

  if (roleOk && permissionOk) {
    return <>{children}</>;
  }
  return <>{fallback}</>;
}

export default RoleGuard;
