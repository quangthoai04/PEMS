/**
 * Types cho báo cáo hiệu suất phòng ban của Department Leader — mirror
 * DepartmentLeaderReportOverviewDto từ GET /reports/department-leader-overview.
 * Scope dữ liệu = department_id của user hiện tại (backend enforce).
 */

export interface DeptLeaderReportFilters {
  preset: string;
  fromDate?: string;
  toDate?: string;
  logisticsStatus: string;
  itemType: string;
  assignedUserId: string;
  dueStatus: string;
  handoverStatus: string;
  feedbackRating: string;
}

export type DeptLeaderExportFormat = 'EXCEL' | 'PDF' | 'CSV';

export type DeptLeaderReportSection =
  | 'EXECUTIVE_SUMMARY'
  | 'TASK_PIPELINE'
  | 'STAFF_PERFORMANCE'
  | 'HANDOVER_SUMMARY'
  | 'INCIDENT_SUMMARY'
  | 'FEEDBACK_SUMMARY';

export interface DeptLeaderReportExportRequest extends DeptLeaderReportFilters {
  exportFormat: string;
  reportSections: string[];
}

// ── Filter summary ──────────────────────────────────────────

export interface DeptLeaderFilterSummary {
  preset: string;
  fromDate: string | null;
  toDate: string | null;
  logisticsStatus: string;
  itemType: string;
  assignedUserId: string;
  assignedUserName: string | null;
  dueStatus: string;
  handoverStatus: string;
  feedbackRating: string;
  departmentName: string;
  campusName: string;
  generatedByName: string | null;
}

// ── KPI ─────────────────────────────────────────────────────

export interface DeptLeaderKpis {
  newRequests: number;
  waitingAssignment: number;
  waitingStaffResponse: number;
  inProgress: number;
  completed: number;
  declined: number;
  overdue: number;
  missingHandoverSignature: number;
  averageResponseHours: number | null;
  averageFeedbackRating: number | null;
}

// ── Attention items ──────────────────────────────────────────

export interface DeptLeaderAttentionItem {
  key: string;
  label: string;
  count: number;
  severity: 'DANGER' | 'WARNING' | 'INFO' | 'SUCCESS';
  description: string;
  targetSection: string;
}

// ── Task status pipeline ─────────────────────────────────────

export interface DeptLeaderTaskPipelineItem {
  status: string;
  labelVi: string;
  count: number;
  percentage: number;
}

// ── Work type distribution ───────────────────────────────────

export interface DeptLeaderWorkTypeItem {
  itemType: string;
  labelVi: string;
  count: number;
  quantityTotal: number;
  percentage: number;
}

// ── Monthly trend ────────────────────────────────────────────

export interface DeptLeaderMonthlyTrend {
  month: string;
  monthLabel: string;
  totalTasks: number;
  completedTasks: number;
  overdueTasks: number;
}

// ── Staff performance ────────────────────────────────────────

export interface DeptLeaderStaffPerformance {
  userId: number;
  fullName: string;
  assignedCount: number;
  pendingResponseCount: number;
  acceptedCount: number;
  inProgressCount: number;
  completedCount: number;
  declinedCount: number;
  overdueCount: number;
  completionRate: number;
  averageResponseHours: number | null;
}

// ── Pending tasks ────────────────────────────────────────────

export interface DeptLeaderPendingTask {
  logisticsItemId: number;
  visitInstanceId: number | null;
  requestCode: string;
  delegationName: string;
  itemName: string;
  itemType: string;
  quantity: number;
  unit: string | null;
  status: string;
  dueAt: string | null;
  assignedToName: string | null;
  waitingHours: number;
  actionLabel: string;
  detailUrl: string | null;
}

// ── Proposal changes ─────────────────────────────────────────

export interface DeptLeaderProposalChange {
  logisticsItemId: number;
  itemName: string;
  proposedByName: string;
  proposedQuantity: number | null;
  proposedUsageStartAt: string | null;
  proposedUsageEndAt: string | null;
  proposalNote: string | null;
  proposalStatus: string;
  createdAt: string;
}

// ── Handover summary ─────────────────────────────────────────

export interface DeptLeaderHandoverItem {
  logisticsItemId: number;
  itemName: string;
  visitCode: string;
  delegationName: string;
  handoverType: 'BORROW' | 'RETURN' | string;
  borrowerSigned: boolean;
  providerSigned: boolean;
  itemCondition: string | null;
  conditionNote: string | null;
  attachmentFileId: number | null;
  statusLabel: string;
}

// ── Incident summary ─────────────────────────────────────────

export interface DeptLeaderIncidentItem {
  itemType: string;
  itemTypeLabelVi: string;
  itemName: string;
  totalQuantity: number;
  damagedCount: number;
  missingCount: number;
  needActionCount: number;
  latestNote: string | null;
}

// ── Feedback summary ─────────────────────────────────────────

export interface DeptLeaderFeedbackByType {
  itemType: string;
  labelVi: string;
  averageRating: number;
  feedbackCount: number;
}

export interface DeptLeaderFeedbackEntry {
  feedbackId: number;
  visitInstanceId: number;
  delegationName: string;
  itemName: string | null;
  rating: number;
  comment: string | null;
  submittedAt: string;
}

export interface DeptLeaderFeedbackSummary {
  averageRating: number | null;
  totalFeedbacks: number;
  lowFeedbackCount: number;
  feedbackByItemType: DeptLeaderFeedbackByType[];
  lowRatedItems: DeptLeaderFeedbackEntry[];
  recentFeedbacks: DeptLeaderFeedbackEntry[];
}

// ── Invoice items (Tab Hóa đơn) ──────────────────────────────

export interface DeptLeaderInvoiceVisit {
  visitInstanceId: number;
  requestCode: string;
  delegationName: string;
  plannedStartAt: string | null;
  plannedEndAt: string | null;
}

export interface DeptLeaderInvoiceItem {
  logisticsItemId: number;
  itemName: string;
  itemType: string;
  itemTypeLabelVi: string;
  quantity: number;
  unit: string | null;
  /** Đơn giá do Department Leader nhập (không từ DB). */
  unitPrice?: number;
  note?: string;
}

// ── Root overview ────────────────────────────────────────────

export interface DeptLeaderReportOverview {
  generatedAt: string;
  filterSummary: DeptLeaderFilterSummary;
  kpis: DeptLeaderKpis;
  attentionItems: DeptLeaderAttentionItem[];
  taskStatusPipeline: DeptLeaderTaskPipelineItem[];
  workTypeDistribution: DeptLeaderWorkTypeItem[];
  monthlyTrend: DeptLeaderMonthlyTrend[];
  staffPerformance: DeptLeaderStaffPerformance[];
  pendingTasks: DeptLeaderPendingTask[];
  pendingTasksTotal: number;
  proposalChanges: DeptLeaderProposalChange[];
  handoverSummary: DeptLeaderHandoverItem[];
  handoverTotal: number;
  incidentSummary: DeptLeaderIncidentItem[];
  feedbackSummary: DeptLeaderFeedbackSummary;
}
