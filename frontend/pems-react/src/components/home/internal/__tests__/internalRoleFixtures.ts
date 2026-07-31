/**
 * Fixtures dùng chung cho các test i18n của Internal Homepage.
 *
 * Bao đủ 7 nhóm role nội bộ (role_code + sub_role) + 1 VISITOR để kiểm tra không regression.
 * Dữ liệu động (fullName / campusName / departmentName / roleName) cố tình mang dấu tiếng Việt
 * để test khẳng định được là chúng KHÔNG bị dịch khi đổi ngôn ngữ.
 */

import type { AuthUser } from '../../../../features/authentication/types/authentication.types';
import type { HomeRoleBucket } from '../../../../shared/auth/resolveHomeRoleBucket';

/** Dữ liệu động do backend/DB trả về — phải giữ nguyên ở cả VI lẫn EN. */
export const DYNAMIC = {
  fullName: 'Nguyễn Đình Duy',
  campusName: 'FPT University Hà Nội',
  departmentName: 'Phòng Công tác Sinh viên',
  /** Chuỗi cố định tiếng Việt từ backend — KHÔNG được dùng làm nguồn cho nhãn role. */
  roleName: 'Chức danh từ backend',
} as const;

export function makeUser(overrides: Partial<AuthUser> & Pick<AuthUser, 'roleCode'>): AuthUser {
  return {
    userId: 'user-1',
    fullName: DYNAMIC.fullName,
    email: 'user@fpt.edu.vn',
    roleCode: overrides.roleCode,
    roleName: DYNAMIC.roleName,
    subRole: null,
    campusCode: 'HN',
    campusName: DYNAMIC.campusName,
    departmentId: 'dept-9',
    departmentName: DYNAMIC.departmentName,
    mustChangePassword: false,
    mustSetPassword: false,
    effectiveRole: overrides.roleCode,
    status: 'ACTIVE',
    ...overrides,
  };
}

export type InternalBucket = Exclude<HomeRoleBucket, 'VISITOR'>;

export interface RoleCase {
  /** Nhãn dễ đọc trong test output. */
  name: string;
  bucket: InternalBucket;
  user: AuthUser;
}

/** 7 nhóm role nội bộ theo bảng ở Mục 2 của tài liệu yêu cầu. */
export const INTERNAL_ROLE_CASES: RoleCase[] = [
  { name: 'ADMIN', bucket: 'ADMIN', user: makeUser({ roleCode: 'ADMIN', subRole: null }) },
  { name: 'HO', bucket: 'HO', user: makeUser({ roleCode: 'HO', subRole: 'NONE' }) },
  { name: 'STAFF/LEADER', bucket: 'STAFF_LEADER', user: makeUser({ roleCode: 'STAFF', subRole: 'LEADER' }) },
  { name: 'STAFF/STAFF', bucket: 'STAFF', user: makeUser({ roleCode: 'STAFF', subRole: 'STAFF' }) },
  { name: 'DEPARTMENT/LEADER', bucket: 'DEPT_LEADER', user: makeUser({ roleCode: 'DEPARTMENT', subRole: 'LEADER' }) },
  { name: 'DEPARTMENT/STAFF', bucket: 'DEPT_STAFF', user: makeUser({ roleCode: 'DEPARTMENT', subRole: 'STAFF' }) },
  { name: 'STUDENT', bucket: 'STUDENT', user: makeUser({ roleCode: 'STUDENT', subRole: null }) },
];

/** Ký tự có dấu tiếng Việt — dùng để bắt chuỗi VI sót lại khi đang ở chế độ EN. */
const VIETNAMESE_CHARS =
  /[àáảãạăằắẳẵặâầấẩẫậđèéẻẽẹêềếểễệìíỉĩịòóỏõọôồốổỗộơờớởỡợùúủũụưừứửữựỳýỷỹỵ]/i;

export function hasVietnameseDiacritics(text: string): boolean {
  return VIETNAMESE_CHARS.test(text);
}

/** Raw i18n key bị render ra UI (vd "internal.quickAccess.HO.dashboard.label"). */
export function hasRawTranslationKey(text: string): boolean {
  return /internal\.(hero|roleLabels|quickAccess|guide|cta)\./.test(text);
}
