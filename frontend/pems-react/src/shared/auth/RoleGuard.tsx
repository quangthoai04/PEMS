import React from 'react';
import { useAuth } from '../hooks/useAuth';

interface RoleGuardProps {
  children: React.ReactNode;
  /** Show children only if the user has one of these roles. */
  roles?: string[];

  /** Rendered when the check fails (defaults to nothing). */
  fallback?: React.ReactNode;
}

/**
 * Inline visibility guard for menu items / buttons / sections. Hides UI the user
 * cannot use — this is UX only; the backend still authorizes every request.
 */
export function RoleGuard({ children, roles, fallback = null }: RoleGuardProps) {
  const { hasRole } = useAuth();

  const roleOk = !roles || roles.length === 0 || hasRole(roles);

  if (roleOk) {
    return <>{children}</>;
  }
  return <>{fallback}</>;
}

export default RoleGuard;
