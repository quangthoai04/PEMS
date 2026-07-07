/**
 * resolveHomeRoleBucket
 * Thin wrapper riêng cho Homepage (Quick Access / Guide Steps) — tách STAFF và DEPARTMENT
 * theo sub_role thành 4 bucket riêng, vì Quick Access khác nhau giữa Leader và Staff thường.
 * Không thay thế resolveEffectiveRole (dùng cho routing/guard) — chỉ dùng nội bộ trong
 * feature Homepage để chọn nội dung hiển thị.
 */

import type { AuthUser } from '../../features/authentication/types/authentication.types';
import { resolveEffectiveRole } from './resolveEffectiveRole';

export type HomeRoleBucket =
  | 'ADMIN'
  | 'HO'
  | 'STAFF_LEADER'
  | 'STAFF'
  | 'DEPT_LEADER'
  | 'DEPT_STAFF'
  | 'STUDENT'
  | 'VISITOR';

export function resolveHomeRoleBucket(user: AuthUser | null | undefined): HomeRoleBucket | null {
  const effectiveRole = resolveEffectiveRole(user);
  if (!effectiveRole || !user) return null;

  const subRole = (user.subRole ?? '').trim().toUpperCase();

  switch (effectiveRole) {
    case 'STAFF':
      return subRole === 'LEADER' ? 'STAFF_LEADER' : 'STAFF';
    case 'DEPARTMENT':
      return subRole === 'LEADER' ? 'DEPT_LEADER' : 'DEPT_STAFF';
    default:
      return effectiveRole;
  }
}
