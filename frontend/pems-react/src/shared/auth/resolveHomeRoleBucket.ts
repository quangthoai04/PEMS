/**
 * resolveHomeRoleBucket
 * Thin wrapper riêng cho Homepage (Quick Access / Guide Steps). Trước đây nó phải tự
 * đọc lại sub_role để tách Leader khỏi Staff thường, vì resolveEffectiveRole gộp hai
 * vai này làm một. Nay effective role đã đủ 8 giá trị nên đây chỉ còn là phép đổi tên
 * sang thuật ngữ của Homepage (DEPT_LEADER / DEPT_STAFF).
 *
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
  if (!effectiveRole) return null;

  switch (effectiveRole) {
    case 'DEPARTMENT_LEAD':
      return 'DEPT_LEADER';
    case 'DEPARTMENT':
      return 'DEPT_STAFF';
    default:
      return effectiveRole;
  }
}
