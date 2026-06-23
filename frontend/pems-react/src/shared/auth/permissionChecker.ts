
/** True when `roleCode` is in the allowed list (case-insensitive). */
export function hasRole(roleCode: string | undefined | null, allowed: string[]): boolean {
  if (!roleCode) return false;
  const upper = roleCode.toUpperCase();
  return allowed.some((r) => r.toUpperCase() === upper);
}
