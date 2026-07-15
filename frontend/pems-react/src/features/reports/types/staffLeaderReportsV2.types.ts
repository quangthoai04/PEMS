/** Types cho báo cáo campus 3 phần của Staff Leader (GET /reports/staff-leader-report-v2). */

export type StaffLeaderV2Preset = 'THIS_MONTH' | 'THIS_QUARTER' | 'THIS_YEAR' | 'CUSTOM';

export interface StaffLeaderV2Filters {
  preset: StaffLeaderV2Preset;
  fromDate: string; // YYYY-MM-DD (chỉ dùng khi CUSTOM)
  toDate: string;
}

export interface StaffLeaderV2PartnerTrendPoint {
  month: string;
  monthLabel: string;
  visitsWithPartner: number;
  newPartners: number;
  cumulativePartners: number;
}

export interface StaffLeaderV2Visits {
  totalVisits: number;
  totalGuests: number;
  completed: number;
  rejected: number;
  cancelled: number;
  notCompleted: number;
  feedbackCount: number;
  feedbackTotalStars: number;
  feedbackAverage: number | null;
  totalPartners: number;
  /** Độ chi tiết trục thời gian: YEAR | MONTH | WEEK | DAY | HOUR (backend chọn theo độ dài kỳ lọc). */
  trendGranularity: 'YEAR' | 'MONTH' | 'WEEK' | 'DAY' | 'HOUR';
  partnerTrend: StaffLeaderV2PartnerTrendPoint[];
}

export interface StaffLeaderV2PersonnelRow {
  userId: number;
  fullName: string;
  email: string;
  role: 'STAFF_LEADER' | 'STAFF' | 'STUDENT';
  visitCount: number;
  totalHours: number;
  feedbackAverage: number | null;
  feedbackCount: number;
  declinedCount: number;
}

export interface StaffLeaderV2Personnel {
  totalStaff: number;
  totalStudents: number;
  averageFeedback: number | null;
  rows: StaffLeaderV2PersonnelRow[];
}

export interface StaffLeaderV2DepartmentRow {
  departmentId: number;
  name: string;
  totalRequests: number;
  completed: number;
  rejected: number;
  feedbackAverage: number | null;
  feedbackCount: number;
}

export interface StaffLeaderV2Departments {
  totalDepartments: number;
  completedTotal: number;
  rejectedTotal: number;
  averageFeedback: number | null;
  rows: StaffLeaderV2DepartmentRow[];
}

export interface StaffLeaderReportV2 {
  generatedAt: string;
  campusName: string;
  preset: StaffLeaderV2Preset;
  fromDate: string;
  toDate: string;
  visits: StaffLeaderV2Visits;
  personnel: StaffLeaderV2Personnel;
  departments: StaffLeaderV2Departments;
}

/** 1 chữ ký trong biên bản bàn giao (panel hóa đơn). */
export interface StaffLeaderInvoiceSignature {
  name: string | null;
  signedAt: string | null;
}

/** 1 đơn hậu cần phòng ban đã nhận trong khoảng ngày (panel hóa đơn). */
export interface StaffLeaderInvoiceItem {
  logisticsItemId: number;
  title: string;
  itemType: string;
  quantity: number;
  status: string;
  requestCode: string;
  delegationName: string;
  usageStartAt: string;
  usageEndAt: string;
  hostName: string | null;
  assigneeName: string | null;
  borrowNote: string | null;
  returnNote: string | null;
  borrowProviderSignature: StaffLeaderInvoiceSignature | null;
  borrowBorrowerSignature: StaffLeaderInvoiceSignature | null;
  returnProviderSignature: StaffLeaderInvoiceSignature | null;
  returnBorrowerSignature: StaffLeaderInvoiceSignature | null;
}
