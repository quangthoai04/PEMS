/** Types cho báo cáo campus 3 phần của Staff Leader (GET /reports/staff-leader-report-v2). */

export type StaffLeaderV2Preset = 'THIS_MONTH' | 'THIS_QUARTER' | 'THIS_YEAR' | 'CUSTOM';

export interface StaffLeaderV2Filters {
  preset: StaffLeaderV2Preset;
  fromDate: string; // YYYY-MM-DD (chỉ dùng khi CUSTOM)
  toDate: string;
}

export interface StaffLeaderV2VisitTrendPoint {
  month: string;
  monthLabel: string;
  totalVisits: number;
  completedVisits: number;
  totalGuests: number;
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
  visitTrend: StaffLeaderV2VisitTrendPoint[];
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

export interface StaffLeaderV2ExpenseRow {
  visitInstanceId: number;
  groupCode: string;
  delegationName: string;
  visitDate: string | null;
  generalExpense: number;
  logisticsExpense: number;
  totalExpense: number;
  status: string;
}

export interface StaffLeaderV2Expenses {
  totalAmount: number;
  totalGeneral: number;
  totalLogistics: number;
  rows: StaffLeaderV2ExpenseRow[];
}

export interface StaffLeaderV2PartnerTypeStat {
  partnerType: string;
  label: string;
  count: number;
  visitCount: number;
}

export interface StaffLeaderV2PartnerStatusStat {
  status: string;
  label: string;
  count: number;
}

export interface StaffLeaderV2TopPartnerRow {
  partnerId: number;
  partnerCode: string | null;
  name: string;
  partnerType: string;
  cooperationStatus: string;
  visitCount: number;
  guestCount: number;
  feedbackAverage: number | null;
}

export interface StaffLeaderV2Partners {
  totalPartners: number;
  newPartnersInPeriod: number;
  activePartners: number;
  visitsWithPartnerCount: number;
  partnerVisitRatio: number;
  trendGranularity: 'YEAR' | 'MONTH' | 'WEEK' | 'DAY' | 'HOUR';
  trend: StaffLeaderV2PartnerTrendPoint[];
  partnersByType: StaffLeaderV2PartnerTypeStat[];
  partnersByStatus: StaffLeaderV2PartnerStatusStat[];
  topPartners: StaffLeaderV2TopPartnerRow[];
}

export interface StaffLeaderReportV2 {
  generatedAt: string;
  campusName: string;
  preset: StaffLeaderV2Preset;
  fromDate: string;
  toDate: string;
  visits: StaffLeaderV2Visits;
  partners: StaffLeaderV2Partners;
  personnel: StaffLeaderV2Personnel;
  departments: StaffLeaderV2Departments;
  expenses: StaffLeaderV2Expenses;
}

/** 1 hạng mục chi phí trong panel "Thống kê chi phí" (phần 4). */
export interface StaffLeaderExpenseVisitItem {
  itemOrigin: string;
  itemName: string;
  quantity: number;
  unitName: string | null;
  unitPrice: number;
  totalAmount: number;
}

/** 1 bảng kê chi phí (GENERAL của Host hoặc LOGISTICS của 1 đơn phòng ban). */
export interface StaffLeaderExpenseVisitReport {
  reportScope: 'GENERAL' | 'LOGISTICS' | string;
  departmentName: string | null;
  logisticsItemTitle: string | null;
  noExpense: boolean;
  reportNote: string | null;
  status: string;
  totalAmount: number;
  items: StaffLeaderExpenseVisitItem[];
}

/** 1 đoàn có dữ liệu chi phí trong khoảng ngày (panel phần 4). */
export interface StaffLeaderExpenseVisit {
  visitInstanceId: number;
  requestCode: string;
  delegationName: string;
  visitDate: string;
  totalExpense: number;
  status: string;
  reports: StaffLeaderExpenseVisitReport[];
}

export interface StaffLeaderExpenseVisits {
  totalAmount: number;
  rows: StaffLeaderExpenseVisit[];
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
