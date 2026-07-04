/**
 * Types cho báo cáo Head Office (GET /reports/ho-overview).
 * Mirror của HoReportOverviewDto phía backend (JSON camelCase).
 */

export type HoReportPreset = 'THIS_MONTH' | 'THIS_QUARTER' | 'THIS_YEAR' | 'CUSTOM';
export type HoVisitScopeFilter = 'ALL' | 'SINGLE_CAMPUS' | 'MULTI_CAMPUS';
export type HoRequestStatusFilter = 'ALL' | 'PENDING_APPROVAL' | 'APPROVED' | 'REJECTED' | 'CANCELLED';
export type HoInstanceStatusFilter =
  | 'ALL'
  | 'WAITING_REQUEST_APPROVAL'
  | 'WAITING_HOST_ASSIGNMENT'
  | 'ASSIGNED'
  | 'BEFORE_VISIT'
  | 'DURING_VISIT'
  | 'AFTER_VISIT'
  | 'CLOSED'
  | 'CANCELLED';
export type HoAttentionSeverity = 'INFO' | 'WARNING' | 'DANGER' | 'SUCCESS';
export type HoExportFormat = 'PDF' | 'EXCEL' | 'CSV';
export type HoReportSection =
  | 'EXECUTIVE_SUMMARY'
  | 'APPROVAL_OVERVIEW'
  | 'CAMPUS_PERFORMANCE'
  | 'LIFECYCLE_CLOSE_READINESS'
  | 'FEEDBACK_QUALITY'
  | 'CONTENT_EMAIL_EFFECTIVENESS';

export interface HoReportFilters {
  preset: HoReportPreset;
  /** yyyy-MM-dd, chỉ dùng khi preset = CUSTOM. */
  fromDate?: string;
  toDate?: string;
  /** undefined = tất cả cơ sở. */
  campusId?: number;
  visitScope: HoVisitScopeFilter;
  requestStatus: HoRequestStatusFilter;
  campusInstanceStatus: HoInstanceStatusFilter;
  visitType: string; // 'ALL' hoặc mã loại chuyến
}

export interface HoReportExportRequest extends HoReportFilters {
  exportFormat: HoExportFormat;
  reportSections: HoReportSection[];
}

export interface HoReportFilterSummary {
  preset: HoReportPreset;
  fromDate: string;
  toDate: string;
  campusId: number | null;
  campusName: string;
  visitScope: string;
  requestStatus: string;
  campusInstanceStatus: string;
  visitType: string;
  generatedByUserId: number | null;
  generatedByName: string | null;
}

export interface HoReportKpis {
  totalRequests: number;
  multiCampusPending: number;
  pendingRequests: number;
  approvedRequests: number;
  rejectedRequests: number;
  cancelledRequests: number;
  activeCampusInstances: number;
  closedCampusInstances: number;
  overdueCloseInstances: number;
  averageDecisionHours: number | null;
  averageFeedbackRating: number | null;
  totalGuests: number;
}

export interface HoAttentionItem {
  key: string;
  label: string;
  count: number;
  severity: HoAttentionSeverity;
  description: string;
  targetSection: string;
}

export interface HoMonthlyTrend {
  month: string;
  monthLabel: string;
  totalRequests: number;
  singleCampusRequests: number;
  multiCampusRequests: number;
  approved: number;
  rejected: number;
  cancelled: number;
  totalGuests: number;
}

export interface HoApprovalBreakdown {
  approved: number;
  rejected: number;
  pending: number;
  cancelled: number;
  approvalRate: number;
  rejectionRate: number;
  averageDecisionHours: number | null;
}

export interface HoCampusPerformance {
  campusId: number;
  campusCode: string;
  campusName: string;
  totalInstances: number;
  waitingRequestApproval: number;
  waitingHostAssignment: number;
  assigned: number;
  beforeVisit: number;
  duringVisit: number;
  afterVisit: number;
  closed: number;
  cancelled: number;
  averageFeedbackRating: number | null;
  overdueCloseCount: number;
  guestCount: number;
}

export interface HoLifecyclePipelineItem {
  status: string;
  labelVi: string;
  count: number;
  percentage: number;
}

export interface HoPendingMultiCampusRequest {
  requestId: number;
  requestCode: string;
  delegationName: string;
  organizationName: string;
  submittedAt: string;
  plannedStartAt: string | null;
  plannedEndAt: string | null;
  requestedCampusCount: number;
  guestCount: number;
  waitingHours: number;
  status: string;
}

export interface HoCloseReadiness {
  visitInstanceId: number;
  requestId: number;
  requestCode: string;
  delegationName: string;
  campusName: string;
  plannedEndAt: string;
  hostName: string | null;
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

export interface HoRatedVisit {
  visitInstanceId: number;
  delegationName: string;
  campusName: string;
  averageRating: number;
  feedbackCount: number;
  plannedStartAt: string | null;
}

export interface HoCampusRating {
  campusId: number;
  campusName: string;
  averageRating: number;
  feedbackCount: number;
}

export interface HoFeedbackSummary {
  averageRating: number | null;
  totalFeedbacks: number;
  lowFeedbackCount: number;
  topRatedVisits: HoRatedVisit[];
  lowRatedVisits: HoRatedVisit[];
  ratingByCampus: HoCampusRating[];
}

export interface HoContentEmailSummary {
  publishedNewsCount: number;
  pendingNewsCount: number;
  instancesMissingNewsCount: number;
  emailSentCount: number;
  emailFailedCount: number;
  emailDeliveredRate: number | null;
  actionTokenRespondedCount: number;
  actionTokenExpiredCount: number;
  actionTokenPendingCount: number;
}

export interface HoReportOverview {
  generatedAt: string;
  filterSummary: HoReportFilterSummary;
  kpis: HoReportKpis;
  attentionItems: HoAttentionItem[];
  monthlyTrend: HoMonthlyTrend[];
  approvalBreakdown: HoApprovalBreakdown;
  campusPerformance: HoCampusPerformance[];
  lifecyclePipeline: HoLifecyclePipelineItem[];
  multiCampusPendingRequests: HoPendingMultiCampusRequest[];
  multiCampusPendingTotal: number;
  closeReadiness: HoCloseReadiness[];
  closeReadinessTotal: number;
  feedbackSummary: HoFeedbackSummary;
  contentAndEmailSummary: HoContentEmailSummary;
}
