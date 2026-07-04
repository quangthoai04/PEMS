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

export interface StaffLeaderReportExportRequest extends StaffLeaderReportFilters {
  exportFormat: string;
  reportSections: string[];
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
  conflictCount: number;
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
  type: string;
  requestId: number;
  visitInstanceId: number | null;
  requestCode: string;
  delegationName: string;
  organizationName: string;
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
  hostName: string;
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

export interface StaffLeaderRatedVisit {
  visitInstanceId: number;
  delegationName: string;
  averageRating: number;
  feedbackCount: number;
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
  topRatedVisits: StaffLeaderRatedVisit[];
  lowRatedVisits: StaffLeaderRatedVisit[];
  ratingByHost: StaffLeaderRatingByHost[];
}

export interface StaffLeaderNewsMediaSummary {
  publishedNewsCount: number;
  pendingNewsCount: number;
  missingNewsCount: number;
  newsNotRequiredCount: number;
  mediaUploadedCount: number;
}

export interface StaffLeaderReportOverview {
  generatedAt: string;
  kpis: StaffLeaderKpis;
  attentionItems: StaffLeaderAttentionItem[];
  campusLifecyclePipeline: StaffLeaderLifecyclePipelineItem[];
  monthlyTrend: StaffLeaderMonthlyTrend[];
  hostWorkload: StaffLeaderHostWorkload[];
  logisticsByDepartment: StaffLeaderLogisticsByDepartment[];
  pendingActionRequests: StaffLeaderPendingActionRequest[];
  closeReadiness: StaffLeaderCloseReadiness[];
  feedbackSummary: StaffLeaderFeedbackSummary;
  newsMediaSummary: StaffLeaderNewsMediaSummary;
}
