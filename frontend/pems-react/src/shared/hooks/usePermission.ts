import { useAuthContext } from '../auth/AuthContext';
import type { PermissionLevel } from '../../features/authentication/types/authentication.types';

/**
 * Permission helpers bound to the current user. Use these to drive UI visibility
 * (the backend remains the final authority on every protected action).
 */
export function usePermission() {
  const { permissions, hasPermission, hasAnyPermission, hasRole, user } = useAuthContext();

  return {
    permissions,
    roleCode: user?.roleCode,
    can: (code: string, minimumLevel: PermissionLevel = 'R') => hasPermission(code, minimumLevel),
    canAny: (codes: string[], minimumLevel: PermissionLevel = 'R') => hasAnyPermission(codes, minimumLevel),
    hasRole,
    isOwn: (resourceOwnerUserId?: string | null) =>
      !!resourceOwnerUserId && !!user && resourceOwnerUserId === user.userId,
    canAccessCampus: (campusId?: string | null) => {
      if (!user) return false;
      if (user.roleCode === 'ADMIN' || user.roleCode === 'HO') return true;
      return !!campusId && campusId === user.primaryCampusId;
    },
  };
}
