import type { CampusOperationalReadiness } from './types/campusManagement.types';

/** UC-86 §22.1 — human explanation for each machine-readable readiness issue code. */
export const CAMPUS_READINESS_ISSUE_LABELS: Record<string, string> = {
  ACTIVE_STAFF_LEADER_MISSING: 'Chưa có Staff Leader đang hoạt động.',
  ACTIVE_IC_DEPARTMENT_MISSING: 'Chưa có phòng ban IC đang hoạt động.',
  MULTIPLE_ACTIVE_IC_DEPARTMENTS: 'Cấu hình không hợp lệ: nhiều hơn một phòng ban IC đang hoạt động.',
  MULTIPLE_ACTIVE_STAFF_LEADERS: 'Cấu hình không hợp lệ: nhiều hơn một Staff Leader hợp lệ.',
  IC_HEAD_MAPPING_INCONSISTENT: 'Trưởng phòng IC trên hồ sơ campus chưa khớp với Staff Leader hiện tại.',
};

/** Readiness reasons to show for an ACTIVE campus (CAMPUS_INACTIVE is implied by the status badge). */
export function campusReadinessReasons(readiness: CampusOperationalReadiness | null): string[] {
  if (!readiness) return [];
  return readiness.readinessIssues
    .filter((issue) => issue !== 'CAMPUS_INACTIVE')
    .map((issue) => CAMPUS_READINESS_ISSUE_LABELS[issue] ?? issue);
}

/**
 * Đơn vị hành chính cấp tỉnh của Việt Nam (34 đơn vị, hiệu lực từ 2025):
 * 6 thành phố trực thuộc Trung ương + 28 tỉnh.
 * Dùng cho dropdown "Chọn vị trí" khi tạo / sửa campus.
 */
export const CAMPUS_PROVINCES = [
  // 6 thành phố trực thuộc Trung ương
  'Hà Nội',
  'TP. Hồ Chí Minh',
  'Hải Phòng',
  'Đà Nẵng',
  'Cần Thơ',
  'Huế',
  // 28 tỉnh
  'Lai Châu',
  'Điện Biên',
  'Sơn La',
  'Lào Cai',
  'Lạng Sơn',
  'Cao Bằng',
  'Tuyên Quang',
  'Thái Nguyên',
  'Phú Thọ',
  'Bắc Ninh',
  'Hưng Yên',
  'Ninh Bình',
  'Quảng Ninh',
  'Thanh Hóa',
  'Nghệ An',
  'Hà Tĩnh',
  'Quảng Trị',
  'Quảng Ngãi',
  'Gia Lai',
  'Khánh Hòa',
  'Đắk Lắk',
  'Lâm Đồng',
  'Đồng Nai',
  'Tây Ninh',
  'Vĩnh Long',
  'Đồng Tháp',
  'An Giang',
  'Cà Mau',
];
