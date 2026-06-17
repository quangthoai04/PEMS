import type { PermissionLevel, UserPermission } from '../../features/authentication/types/authentication.types';

// Permission level hierarchy: F > E > O > R > none.
// NOTE: "O" (Own) is NOT a global grant — callers requiring O must still verify
// ownership of the specific resource (the backend is the final authority).
const RANK: Record<PermissionLevel, number> = {
  F: 4,
  E: 3,
  O: 2,
  R: 1,
};

export function rankOf(level?: PermissionLevel | null): number {
  return level ? RANK[level] ?? 0 : 0;
}

export function satisfies(actual: PermissionLevel | undefined | null, required: PermissionLevel): boolean {
  return rankOf(actual) >= rankOf(required);
}

export function findLevel(
  permissions: UserPermission[],
  permissionCode: string,
): PermissionLevel | undefined {
  return permissions.find((p) => p.permissionCode === permissionCode)?.permissionLevel;
}

/** True when the user holds `permissionCode` at >= `minimumLevel`. */
export function hasPermission(
  permissions: UserPermission[],
  permissionCode: string,
  minimumLevel: PermissionLevel = 'R',
): boolean {
  return satisfies(findLevel(permissions, permissionCode), minimumLevel);
}

/** True when the user holds ANY of the given permission codes at >= `minimumLevel`. */
export function hasAnyPermission(
  permissions: UserPermission[],
  permissionCodes: string[],
  minimumLevel: PermissionLevel = 'R',
): boolean {
  return permissionCodes.some((code) => hasPermission(permissions, code, minimumLevel));
}

/** True when `roleCode` is in the allowed list (case-insensitive). */
export function hasRole(roleCode: string | undefined | null, allowed: string[]): boolean {
  if (!roleCode) return false;
  const upper = roleCode.toUpperCase();
  return allowed.some((r) => r.toUpperCase() === upper);
}
