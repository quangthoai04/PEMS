/**
 * resolveEffectiveRole
 * Quy đổi (roleCode + subRole) thô từ backend thành một "effective role" duy nhất
 * để frontend dùng cho điều hướng / hiển thị. Đây chỉ là tiện ích bổ sung —
 * KHÔNG thay thế role check hiện có; dùng dần ở ProtectedRoute/Sidebar.
 *
 * Quy tắc an toàn: STAFF hoặc DEPARTMENT thiếu sub_role hợp lệ (LEADER/STAFF) sẽ
 * trả về null (tài khoản cấu hình lỗi) — KHÔNG tự đoán, KHÔNG tự cấp quyền.
 *
 * Quy ước chuẩn (xem docs/permissions/PERMISSION_RULES.md):
 *   STAFF      + sub_role STAFF  = Staff thường
 *   STAFF      + sub_role LEADER = Staff Leader
 *   DEPARTMENT + sub_role STAFF  = Department Staff
 *   DEPARTMENT + sub_role LEADER = Department Leader
 *   ADMIN / HO / STUDENT / VISITOR = không dùng sub_role
 */

import type { AuthUser } from '../../features/authentication/types/authentication.types';

export type EffectiveRole =
  | 'ADMIN'
  | 'HO'
  | 'STAFF'
  | 'DEPARTMENT'
  | 'STUDENT'
  | 'VISITOR';

function normalizeRoleCode(roleCode?: string | null): string {
  return (roleCode ?? '').trim().toUpperCase();
}

/** Returns 'LEADER' | 'STAFF' | '' (empty = missing / NONE). sub_role is only ever LEADER or STAFF. */
function normalizeSubRole(subRole?: string | null): 'LEADER' | 'STAFF' | '' {
  const value = (subRole ?? '').trim().toUpperCase();
  if (value === 'LEADER') return 'LEADER';
  if (value === 'STAFF') return 'STAFF';
  return ''; // covers null, '', 'NONE', and any unexpected value
}

/**
 * Resolve the effective role. Returns `null` when the account cannot be mapped
 * to a valid workspace (e.g. STAFF/DEPARTMENT without a valid LEADER/STAFF
 * sub-role, or an unknown role code) — the caller should route such users to
 * /invalid-account.
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
    case 'DEPARTMENT':
      if (subRole === 'LEADER' || subRole === 'STAFF') return 'DEPARTMENT';
      return null; // DEPARTMENT must have a valid sub-role — no implicit grant
    case 'STUDENT':
      return 'STUDENT';
    case 'VISITOR':
      return 'VISITOR';
    default:
      return null;
  }
}
