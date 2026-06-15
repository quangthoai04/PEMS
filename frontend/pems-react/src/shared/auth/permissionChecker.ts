export type PermissionCode = 'F' | 'E' | 'R' | 'O' | '—';

export function hasPermission(userPermission: PermissionCode, required: PermissionCode): boolean {
  const rank: Record<PermissionCode, number> = {
    '—': 0,
    'R': 1,
    'O': 2,
    'E': 3,
    'F': 4,
  };

  return rank[userPermission] >= rank[required];
}
