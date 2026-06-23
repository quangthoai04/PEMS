import { useAuthContext } from '../auth/AuthContext';
/**
 * Permission helpers bound to the current user. Use these to drive UI visibility
 * (the backend remains the final authority on every protected action).
 */
export function usePermission() {
  const { hasRole, user } = useAuthContext();

  return {
    roleCode: user?.roleCode,
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
