/**
 * resolveEffectiveRole
 * Quy đổi (roleCode + subRole) thô từ backend thành một "effective role" duy nhất
 * để frontend dùng cho điều hướng / hiển thị. Đây chỉ là tiện ích bổ sung —
 * KHÔNG thay thế role check hiện có; dùng dần ở ProtectedRoute/Sidebar.
 *
 * Quy tắc an toàn: STAFF hoặc DEPT thiếu sub_role hợp lệ (Leader/Staff) sẽ trả về
 * null (tài khoản cấu hình lỗi) — KHÔNG tự đoán, KHÔNG tự cấp quyền.
 */

import type { AuthUser } from '../../features/authentication/types/authentication.types';

export type EffectiveRole =
  | 'ADMIN'
  | 'HO'
  | 'STAFF'
  | 'DEPT'
  | 'STUDENT'
  | 'VISITOR';

function normalizeRoleCode(roleCode?: string | null): string {
  return (roleCode ?? '').trim().toUpperCase();
}

/** Returns 'LEADER' | 'STAFF' | 'DEPT' | '' (empty = missing / NONE). */
function normalizeSubRole(subRole?: string | null): 'LEADER' | 'STAFF' | 'DEPT' | '' {
  const value = (subRole ?? '').trim().toUpperCase();
  if (value === 'LEADER') return 'LEADER';
  if (value === 'STAFF') return 'STAFF';
  if (value === 'DEPT') return 'DEPT';
  return ''; // covers null, '', 'NONE', and any unexpected value
}

/**
 * Resolve the effective role. Returns `null` when the account cannot be mapped
 * to a valid workspace (e.g. STAFF/DEPT without a valid Leader/Staff/Dept sub-role, or an
 * unknown role code) — the caller should route such users to /invalid-account.
 */
export function resolveEffectiveRole(user: AuthUser | null | undefined): EffectiveRole | null {
  if (!user) return null;

  const roleCode = normalizeRoleCode(user.roleCode);
  const subRole = normalizeSubRole(user.subRole);

  switch (roleCode) {
    case 'ADMIN':
      return 'ADMIN';
    case 'HO':
      return 'HO';
    case 'STAFF':
      if (subRole === 'LEADER' || subRole === 'STAFF') return 'STAFF';
      return null; // STAFF must have a valid sub-role — no implicit grant
    case 'DEPT':
      if (subRole === 'LEADER' || subRole === 'STAFF') return 'DEPT';
      return null; // DEPT must have a valid sub-role — no implicit grant
    case 'STUDENT':
      return 'STUDENT';
    case 'VISITOR':
      return 'VISITOR';
    default:
      return null;
  }
}
