/** Types cho báo cáo phòng ban 2 phần của Department Leader (GET /reports/dept-leader-report-v2). */

export type DeptLeaderV2Preset = 'THIS_MONTH' | 'THIS_QUARTER' | 'THIS_YEAR' | 'CUSTOM';
export type DeptLeaderV2Granularity = 'YEAR' | 'MONTH' | 'WEEK' | 'DAY' | 'HOUR';

export interface DeptLeaderV2Filters {
  preset: DeptLeaderV2Preset;
  fromDate: string;
  toDate: string;
}

export interface DeptLeaderV2TrendPoint {
  month: string;
  monthLabel: string;
  totalTasks: number;
  completed: number;
}

export interface DeptLeaderV2Tasks {
  totalTasks: number;
  completed: number;
  rejected: number;
  notCompleted: number;
  feedbackCount: number;
  feedbackTotalStars: number;
  feedbackAverage: number | null;
  trendGranularity: DeptLeaderV2Granularity;
  trend: DeptLeaderV2TrendPoint[];
}

export interface DeptLeaderV2PersonnelRow {
  userId: number;
  fullName: string;
  email: string;
  role: 'DEPT_LEADER' | 'DEPT_STAFF';
  taskCount: number;
  totalHours: number;
  feedbackAverage: number | null;
  feedbackCount: number;
  declinedCount: number;
}

export interface DeptLeaderV2Personnel {
  totalStaff: number;
  averageFeedback: number | null;
  rows: DeptLeaderV2PersonnelRow[];
}

export interface DeptLeaderReportV2 {
  generatedAt: string;
  departmentName: string;
  preset: DeptLeaderV2Preset;
  fromDate: string;
  toDate: string;
  tasks: DeptLeaderV2Tasks;
  personnel: DeptLeaderV2Personnel;
}

/** 1 chữ ký trong biên bản bàn giao (panel hóa đơn). */
export interface DeptLeaderInvoiceSignatureV2 {
  name: string | null;
  signedAt: string | null;
}

/** 1 đơn hậu cần phòng ban ĐÃ HOÀN THÀNH trong khoảng ngày (panel hóa đơn). */
export interface DeptLeaderInvoiceItemV2 {
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
  borrowProviderSignature: DeptLeaderInvoiceSignatureV2 | null;
  borrowBorrowerSignature: DeptLeaderInvoiceSignatureV2 | null;
  returnProviderSignature: DeptLeaderInvoiceSignatureV2 | null;
  returnBorrowerSignature: DeptLeaderInvoiceSignatureV2 | null;
}
