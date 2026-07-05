/**
 * Types cho báo cáo vận hành campus của Staff Leader — mirror StaffLeaderReportOverviewDto
 * từ GET /reports/staff-leader-overview (backend aggregate từ DB thật, scope campus của leader).
 */

export interface StaffLeaderReportFilters {
  preset: string;
  fromDate?: string;
  toDate?: string;
  visitStatus: string;
  requestStatus: string;
  hostUserId: string;
  departmentId: string;
  logisticsStatus: string;
  feedbackRating: string;
}

export type StaffLeaderExportFormat = 'EXCEL' | 'PDF' | 'CSV';

export type StaffLeaderReportSection =
  | 'EXECUTIVE_SUMMARY'
  | 'LIFECYCLE_SUMMARY'
  | 'HOST_WORKLOAD'
  | 'PENDING_ACTIONS'
  | 'LOGISTICS_SUMMARY'
  | 'CLOSE_READINESS'
  | 'FEEDBACK_SUMMARY'
  | 'PARTNER_SUMMARY';

export interface StaffLeaderReportExportRequest extends StaffLeaderReportFilters {
  exportFormat: string;
  reportSections: string[];
}

export interface StaffLeaderFilterSummary {
  preset: string;
  fromDate: string | null;
  toDate: string | null;
  visitStatus: string;
  requestStatus: string;
  hostUserId: string;
  hostName: string | null;
  departmentId: string;
  departmentName: string | null;
  logisticsStatus: string;
  feedbackRating: string;
  campusName: string;
  generatedByName: string | null;
}

export interface StaffLeaderKpis {
  pendingSingleCampusApproval: number;
  waitingHostAssignment: number;
  assignedVisits: number;
  beforeVisit: number;
  duringVisit: number;
  afterVisit: number;
  closedVisits: number;
  overdueOrNotClosed: number;
  averageFeedbackRating: number | null;
  totalGuests: number;
}

export interface StaffLeaderAttentionItem {
  type: string;
  label: string;
  count: number;
  severity: string;
  targetSection: string;
}

export interface StaffLeaderLifecyclePipelineItem {
  status: string;
  labelVi: string;
  count: number;
  percentage: number;
}

export interface StaffLeaderMonthlyTrend {
  month: string;
  monthLabel: string;
  totalInstances: number;
  closedInstances: number;
  cancelledInstances: number;
  activeInstances: number;
}

export interface StaffLeaderHostWorkload {
  hostUserId: number;
  hostName: string;
  assignedCount: number;
  upcoming7Days: number;
  beforeVisitCount: number;
  duringVisitCount: number;
  afterVisitCount: number;
  averageFeedbackRating: number | null;
}

export interface StaffLeaderLogisticsByDepartment {
  departmentId: number;
  departmentName: string;
  totalItems: number;
  requested: number;
  accepted: number;
  inProgress: number;
  done: number;
  rejected: number;
  overdueCount: number;
}

export interface StaffLeaderPendingActionRequest {
  type: 'APPROVAL' | 'ASSIGN_HOST' | string;
  requestId: number;
  visitInstanceId: number | null;
  requestCode: string;
  delegationName: string;
  organizationName: string;
  visitType: string;
  plannedStartAt: string | null;
  plannedEndAt: string | null;
  guestCount: number;
  status: string;
  waitingHours: number;
  actionLabel: string;
}

export interface StaffLeaderCloseReadiness {
  visitInstanceId: number;
  requestCode: string;
  delegationName: string;
  hostName: string | null;
  plannedEndAt: string;
  logisticsOpenCount: number;
  missingHandoverSignatureCount: number;
  openActionItemCount: number;
  hasMinutes: boolean;
  hasPublishedNews: boolean;
  newsNotRequired: boolean;
  feedbackCount: number;
  canClose: boolean;
  blockers: string[];
}

export interface StaffLeaderFeedbackEntry {
  feedbackId: number;
  visitInstanceId: number;
  delegationName: string;
  hostName: string | null;
  rating: number;
  comment: string | null;
  submittedAt: string;
  plannedStartAt: string | null;
}

export interface StaffLeaderRatingByHost {
  hostUserId: number;
  hostName: string;
  averageRating: number;
  feedbackCount: number;
}

export interface StaffLeaderFeedbackSummary {
  averageRating: number | null;
  totalFeedbacks: number;
  lowFeedbackCount: number;
  lowFeedbacks: StaffLeaderFeedbackEntry[];
  goodFeedbacks: StaffLeaderFeedbackEntry[];
  ratingByHost: StaffLeaderRatingByHost[];
}

export interface StaffLeaderPartnerTypeCount {
  partnerType: string;
  count: number;
}

export interface StaffLeaderTopPartner {
  partnerId: number;
  name: string;
  partnerType: string;
  country: string | null;
  cooperationStatus: string;
  profileStatus: string;
  visitCount: number;
  linkedGuestCount: number;
}

export interface StaffLeaderPartnerSummary {
  totalPartners: number;
  activePartners: number;
  pendingApprovalPartners: number;
  newPartnersInPeriod: number;
  visitsWithPartner: number;
  partnersByType: StaffLeaderPartnerTypeCount[];
  topPartners: StaffLeaderTopPartner[];
}

export interface StaffLeaderReportOverview {
  generatedAt: string;
  filterSummary: StaffLeaderFilterSummary;
  kpis: StaffLeaderKpis;
  attentionItems: StaffLeaderAttentionItem[];
  campusLifecyclePipeline: StaffLeaderLifecyclePipelineItem[];
  monthlyTrend: StaffLeaderMonthlyTrend[];
  hostWorkload: StaffLeaderHostWorkload[];
  logisticsByDepartment: StaffLeaderLogisticsByDepartment[];
  pendingActionRequests: StaffLeaderPendingActionRequest[];
  pendingActionTotal: number;
  closeReadiness: StaffLeaderCloseReadiness[];
  closeReadinessTotal: number;
  feedbackSummary: StaffLeaderFeedbackSummary;
  partnerSummary: StaffLeaderPartnerSummary;
}
