/**
 * resolveEffectiveRole
 * Quy đổi (roleCode + subRole) thô từ backend thành một "effective role" duy nhất —
 * nguồn chuẩn DUY NHẤT để frontend quyết định route/menu/action.
 *
 * Quy tắc an toàn: STAFF hoặc DEPARTMENT thiếu sub_role hợp lệ (LEADER/STAFF) sẽ
 * trả về null (tài khoản cấu hình lỗi) — KHÔNG tự đoán, KHÔNG tự cấp quyền.
 *
 * Quy ước chuẩn (xem docs/permissions/PERMISSION_MATRIX.md §3.1):
 *   STAFF      + sub_role LEADER = STAFF_LEADER
 *   STAFF      + sub_role STAFF  = STAFF
 *   DEPARTMENT + sub_role LEADER = DEPARTMENT_LEAD
 *   DEPARTMENT + sub_role STAFF  = DEPARTMENT
 *   ADMIN / HO / STUDENT / VISITOR = không dùng sub_role
 *
 * Trước đây hàm này gộp Leader và Staff thường vào cùng một giá trị ('STAFF',
 * 'DEPARTMENT'). Vì thế mọi guard viết bằng effective role không thể phân biệt
 * Staff Leader với Staff thường, và Department Leader với Department Staff — hai
 * cặp có quyền khác hẳn nhau (Gallery, My Department, Account management, Reports).
 * Tám giá trị dưới đây khớp 1-1 với `EffectiveRole` phía backend
 * (backend/PEMS.Application/Common/Security/EffectiveRole.cs), nên frontend và
 * backend nói cùng một ngôn ngữ role.
 */

import type { AuthUser } from '../../features/authentication/types/authentication.types';

export type EffectiveRole =
  | 'ADMIN'
  | 'HO'
  | 'STAFF_LEADER'
  | 'STAFF'
  | 'DEPARTMENT_LEAD'
  | 'DEPARTMENT'
  | 'STUDENT'
  | 'VISITOR';

/** Mọi effective role hợp lệ — dùng cho test matrix và cho policy exhaustiveness check. */
export const ALL_EFFECTIVE_ROLES: readonly EffectiveRole[] = [
  'ADMIN',
  'HO',
  'STAFF_LEADER',
  'STAFF',
  'DEPARTMENT_LEAD',
  'DEPARTMENT',
  'STUDENT',
  'VISITOR',
] as const;

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
 * to a valid workspace (STAFF/DEPARTMENT without a valid LEADER/STAFF sub-role,
 * or an unknown role code) — the caller should route such users to
 * /invalid-account rather than guessing a role for them.
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
      if (subRole === 'LEADER') return 'STAFF_LEADER';
      if (subRole === 'STAFF') return 'STAFF';
      return null; // STAFF must have a valid sub-role — no implicit grant
    // `DEPT` is a legacy alias some older payloads still use for DEPARTMENT.
    case 'DEPT':
    case 'DEPARTMENT':
      if (subRole === 'LEADER') return 'DEPARTMENT_LEAD';
      if (subRole === 'STAFF') return 'DEPARTMENT';
      return null; // DEPARTMENT must have a valid sub-role — no implicit grant
    case 'STUDENT':
      return 'STUDENT';
    case 'VISITOR':
      return 'VISITOR';
    default:
      return null;
  }
}
