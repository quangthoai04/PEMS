import type { AuthUser } from '../../features/authentication/types/authentication.types';

/**
 * Resolves the landing route after login. The PEMS dashboard is a single
 * `/dashboard` entry that internally renders a role-specific view
 * (DashboardHome dispatches to Admin/HO/Shared views by role + sub-role), so all
 * authenticated roles land on `/dashboard`. Kept as a function so per-role
 * landing routes can be customised later without touching callers.
 */
export function getDashboardRoute(_user: Pick<AuthUser, 'roleCode' | 'subRole'>): string {
  return '/dashboard';
}
