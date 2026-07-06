// Status enums mirror the SQL v10 ENUMs (campus-independent approval). The request status
// is an AGGREGATE derived from the per-campus instance decisions; the real approve/reject
// decision lives on each campus instance.

/** visit_requests.status — aggregate of the campus-instance decisions. */
export const VisitRequestStatus = {
  PendingApproval: 'PENDING_APPROVAL',
  PartiallyApproved: 'PARTIALLY_APPROVED',
  Approved: 'APPROVED',
  Rejected: 'REJECTED',
  Cancelled: 'CANCELLED',
} as const;
export type VisitRequestStatus =
  (typeof VisitRequestStatus)[keyof typeof VisitRequestStatus];

/** visit_request_campuses.status — the actual visit progress per campus.
 * No WAITING_HOST_ASSIGNMENT anymore: approve assigns the host in the same action. */
export const VisitInstanceStatus = {
  WaitingRequestApproval: 'WAITING_REQUEST_APPROVAL',
  Assigned: 'ASSIGNED',
  BeforeVisit: 'BEFORE_VISIT',
  DuringVisit: 'DURING_VISIT',
  AfterVisit: 'AFTER_VISIT',
  Closed: 'CLOSED',
  Cancelled: 'CANCELLED',
  Rejected: 'REJECTED',
} as const;
export type VisitInstanceStatus =
  (typeof VisitInstanceStatus)[keyof typeof VisitInstanceStatus];

/** Vietnamese display labels — never show the raw technical enum to users. */
export const REQUEST_STATUS_LABELS: Record<VisitRequestStatus, string> = {
  PENDING_APPROVAL: 'Chờ xử lý',
  PARTIALLY_APPROVED: 'Duyệt một phần',
  APPROVED: 'Đã duyệt',
  REJECTED: 'Từ chối',
  CANCELLED: 'Đã hủy',
};

export const INSTANCE_STATUS_LABELS: Record<VisitInstanceStatus, string> = {
  WAITING_REQUEST_APPROVAL: 'Chờ xử lý tại cơ sở',
  ASSIGNED: 'Đã tiếp nhận',
  BEFORE_VISIT: 'Đang chuẩn bị',
  DURING_VISIT: 'Đang diễn ra',
  AFTER_VISIT: 'Hậu xử lý',
  CLOSED: 'Đã đóng',
  CANCELLED: 'Đã hủy',
  REJECTED: 'Từ chối',
};

/** A campus instance may only be cancelled while it is ASSIGNED or BEFORE_VISIT. */
export const CANCELLABLE_INSTANCE_STATUSES: VisitInstanceStatus[] = [
  VisitInstanceStatus.Assigned,
  VisitInstanceStatus.BeforeVisit,
];

/**
 * UC-136: the Cancel action is available only when the request is APPROVED/PARTIALLY_APPROVED
 * and the campus instance is still ASSIGNED or BEFORE_VISIT. Used to decide whether to show
 * the Cancel button (the backend remains the final authority).
 */
export function canCancelInstance(
  requestStatus: VisitRequestStatus,
  instanceStatus: VisitInstanceStatus
): boolean {
  return (
    (requestStatus === VisitRequestStatus.Approved ||
      requestStatus === VisitRequestStatus.PartiallyApproved) &&
    CANCELLABLE_INSTANCE_STATUSES.includes(instanceStatus)
  );
}

/** Visit scope — number of campuses only; EVERY campus instance is decided by its own Staff Leader. */
export const VisitScope = {
  SingleCampus: 'SINGLE_CAMPUS',
  MultiCampus: 'MULTI_CAMPUS',
} as const;
export type VisitScope = (typeof VisitScope)[keyof typeof VisitScope];

export const VISIT_SCOPE_LABELS: Record<string, string> = {
  SINGLE_CAMPUS: 'Đơn cơ sở',
  MULTI_CAMPUS: 'Liên cơ sở',
};

/** visit_participants.participant_role — exactly 4 values (IC_HOST is the host; the rest are invitees). */
export type ParticipantRole = 'IC_HOST' | 'IC_SUPPORT' | 'DEPT_SUPPORT' | 'STUDENT';

/** UI labels for the 4 participant roles ("Thành phần tham gia"). */
export const PARTICIPANT_ROLE_LABELS: Record<string, string> = {
  IC_HOST: 'Host',
  IC_SUPPORT: 'Staff hỗ trợ IC',
  DEPT_SUPPORT: 'Phòng ban hỗ trợ',
  STUDENT: 'Sinh viên hỗ trợ',
};



/**
 * Business actions the backend says the signed-in user may take on a row.
 * The backend is the single source of truth; the frontend only renders from this list.
 */
export const VISIT_ALLOWED_ACTIONS = {
  VIEW_DETAIL: 'VIEW_DETAIL',
  // Campus-independent approval: HO_APPROVE/HO_REJECT no longer exist — every decision is
  // per campus instance by its Staff Leader (approve luôn kèm gán host).
  APPROVE_AND_ASSIGN_HOST: 'APPROVE_AND_ASSIGN_HOST',
  CAMPUS_REJECT: 'CAMPUS_REJECT',
  // NOTE: Host được gán MỘT lần (UC chốt). Không có TRANSFER_HOST / đổi host trong phase này.
  CANCEL_BY_HOST: 'CANCEL_BY_HOST',
  CANCEL_BY_VISITOR: 'CANCEL_BY_VISITOR',
  ACCEPT_INVITATION: 'ACCEPT_INVITATION',
  DECLINE_INVITATION: 'DECLINE_INVITATION',
  ASSIGN_TO_DEPARTMENT_STAFF: 'ASSIGN_TO_DEPARTMENT_STAFF',
  OPEN_HOST_PROCESS: 'OPEN_HOST_PROCESS',
  OPEN_PROCESS_SUMMARY: 'OPEN_PROCESS_SUMMARY',
  OPEN_CONTRIBUTION: 'OPEN_CONTRIBUTION',
  OPEN_DEPARTMENT_TASK: 'OPEN_DEPARTMENT_TASK',
  OPEN_INVITATION: 'OPEN_INVITATION',
  VIEW_RECEPTION_DETAIL: 'VIEW_RECEPTION_DETAIL',
  VIEW_REQUEST_FORM: 'VIEW_REQUEST_FORM',
} as const;

export type AllowedAction = (typeof VISIT_ALLOWED_ACTIONS)[keyof typeof VISIT_ALLOWED_ACTIONS];

/**
 * One campus instance inside a multi-campus request, used by the expandable-row accordion
 * (Phương án A). Action visibility is backend-computed (booleans) — never gate on status text.
 */
export interface CampusProgressItem {
  visitInstanceId: number;
  campusId: number;
  campusCode: string | null;
  campusName: string | null;

  plannedStartAt?: string | null;
  plannedEndAt?: string | null;

  instanceStatus: VisitInstanceStatus;

  hostUserId?: number | null;
  hostName?: string | null;

  // Per-campus decision (campus-independent approval): who approved/rejected and why.
  decisionNote?: string | null;
  decidedBy?: number | null;
  decidedByName?: string | null;
  decidedAt?: string | null;
  decisionActorRole?: string | null;

  cancellationReason?: string | null;
  cancelledBy?: number | null;
  cancelledByName?: string | null;
  cancelledAt?: string | null;
  cancellationActorType?: string | null;
  cancellationSource?: string | null;

  canViewCampusDetail: boolean;
  canCancelCampusVisit: boolean;
  canViewCancelReason: boolean;
  /** True when this instance is REJECTED with a reason (show "Xem lý do từ chối"). */
  canViewRejectReason?: boolean;
}

/**
 * Phase 2: permission flags for the visit-process detail page (GET process-permissions).
 * Backend is the single source of truth — gate tab view/edit on these, never on status text.
 */
export interface VisitProcessPermission {
  visitInstanceId: number;
  visitRequestId: number;
  requestStatus: VisitRequestStatus;
  instanceStatus: VisitInstanceStatus;
  relation: string; // HOST | STAFF_LEADER | HO | VISITOR_OWNER | IC_SUPPORT | DEPT_SUPPORT | STUDENT | NONE
  hostAssigned: boolean;

  canViewOriginalRequest: boolean;
  canViewOverview: boolean;

  canViewBeforeVisit: boolean;
  canEditBeforeVisit: boolean;

  canViewDuringVisit: boolean;
  canEditDuringVisit: boolean;

  canViewAfterVisit: boolean;
  canEditAfterVisit: boolean;

  canAssignHost: boolean;

  canViewMinutes: boolean;
  canCreateMinutes: boolean;
  canEditMinutes: boolean;

  canViewNews: boolean;
  canCreateNews: boolean;

  // Operational stage transitions (Host only, live instance).
  canStartVisit: boolean;     // ASSIGNED/BEFORE_VISIT → DURING_VISIT
  canCompleteVisit: boolean;  // DURING_VISIT → AFTER_VISIT
  canCloseVisit: boolean;     // AFTER_VISIT → CLOSED
}

/** One agenda (lịch trình) item of a campus instance. */
export interface VisitAgendaItem {
  agendaId: number;
  title: string;
  startTime: string;
  endTime?: string | null;
  description?: string | null;
  location?: string | null;
  /** Concrete assigned person (visit_agendas.responsible_user_id). Null = unassigned. */
  responsibleUserId?: number | null;
  responsibleUserName?: string | null;
  responsibleUserEmail?: string | null;
  /** Suggested role text from the source template item (display-only hint, NOT a person). */
  templateResponsibleRoleLabel?: string | null;
}

/** A person eligible to be the responsible person of an agenda item: the active host or an ACCEPTED
 * supporting participant of the instance. Source for the "Người phụ trách" dropdown. */
export interface AgendaResponsibleCandidate {
  userId: number;
  fullName: string;
  email: string;
  participantRole: string;
  displayRole: string;
  isMainHost: boolean;
}

/** Real before-visit setup data for the VisitProcess page (from GET process-detail). */
export interface VisitProcessDetail {
  visitRequestId: number;
  visitInstanceId: number;
  delegationName: string;
  instanceStatus: VisitInstanceStatus;
  plannedStartAt: string;
  plannedEndAt: string;
  campusName?: string | null;
  hostUserId?: number | null;
  hostName?: string | null;
  relation: string;
  canEditBefore: boolean;
  /** Host's internal "Ghi chú chung" (visit_request_campuses.preparation_note). Null/empty when unset. */
  preparationNote?: string | null;
  agenda: VisitAgendaItem[];
  /** Read-only mirror of the guest's original registration (registrant + delegation + campuses +
   * guests). Null only for callers not allowed to see it. */
  requestSummary?: VisitProcessRequestSummary | null;
  /** Assigned official host (read-only). Null when none assigned yet. */
  host?: VisitProcessHost | null;
  /** Host snapshot + invited supporters of this instance. */
  participants?: VisitParticipantListItem[];
  notifications?: VisitorNotification[];
  publicNews?: VisitorPublicNewsListItem[];
}

export type VisitorNotification = {
  notificationId: number;
  title: string;
  message?: string | null;
  notificationType: string;
  isRead: boolean;
  readAt?: string | null;
  createdAt: string;
};

export type VisitorPublicNewsListItem = {
  newsId: number;
  title: string;
  summary?: string | null;
  thumbnailUrl?: string | null;
  slug?: string | null;
  publishedAt?: string | null;
  authorName?: string | null;
};

/** Read-only snapshot of what the guest submitted (shown on the VisitProcess "Thông tin" sections). */
export interface VisitProcessRequestSummary {
  registrantName?: string | null;
  registrantEmail?: string | null;
  registrantPhone?: string | null;
  registrantOrganization?: string | null;
  registrantJobTitle?: string | null;
  registrantNationality?: string | null;

  delegationName: string;
  visitScope: string;            // SINGLE_CAMPUS | MULTI_CAMPUS
  visitType?: string | null;     // CAMPUS_TOUR | MEETING | ... | OTHER
  visitTypeOther?: string | null;
  purpose?: string | null;
  workingContent?: string | null;
  workingLanguage?: string | null;
  mediaConsentStatus?: string | null;
  mediaConsentNote?: string | null;
  /** Free text the guest entered to identify the transportation to FPTU. */
  transportationNote?: string | null;
  noteToFptu?: string | null;

  contactPersonFullName?: string | null;
  contactPersonOrganization?: string | null;
  contactPersonPhone?: string | null;
  contactPersonEmail?: string | null;

  campuses: VisitProcessCampus[];
  guestMembers: VisitProcessGuestMember[];
  externalSupportMembers: VisitProcessGuestMember[];
}

export interface VisitProcessCampus {
  visitInstanceId: number;
  campusId: number;
  campusName: string;
  plannedStartAt: string;
  plannedEndAt: string;
  isCurrent: boolean;
}

export interface VisitProcessGuestMember {
  guestMemberId: number;
  memberType: string;            // GUEST | EXTERNAL_SUPPORT
  fullName: string;
  organization?: string | null;
  jobTitle?: string | null;
  nationality?: string | null;
  displayOrder: number;
}

export interface VisitProcessHost {
  userId: number;
  fullName: string;
  email: string;
  phone?: string | null;
  departmentName?: string | null;
  statusLabel: string;
}

/** One participant row of a campus instance (host snapshot + invited supporters). */
export interface VisitParticipantListItem {
  participantId: number;
  userId: number;
  fullName: string;
  email: string;
  phone?: string | null;
  roleCode: string;
  subRole?: string | null;
  departmentId?: number | null;
  departmentName?: string | null;
  participantRole: ParticipantRole;     // IC_HOST | IC_SUPPORT | DEPT_SUPPORT | STUDENT
  isHost: boolean;
  status: InvitationStatus | 'REMOVED'; // INVITED | ACCEPTED | DECLINED | ASSIGNED | REMOVED
  invitedByUserId?: number | null;
  invitedByName?: string | null;
  invitedAt?: string | null;
  respondedAt?: string | null;
  assignedByUserId?: number | null;
  assignedByName?: string | null;
  assignedAt?: string | null;
  note?: string | null;
  departmentAssignment?: {
    departmentId: number;
    departmentName: string;
    leaderUserId: number;
    assignedStaffUserId?: number | null;
    assignedStaffName?: string | null;
  } | null;
}

// ── Contribution Page (GET visit-instances/{id}/contribution) ──────────────────────────────
// Permission gate + read-only summary + workspace status for participants who contribute results
// (minutes/media/news). The backend is the single source of truth; the page renders sections
// purely from these booleans and never falls back to `true`.
export interface ContributionPage {
  permissions: ContributionPermission;
  summary: VisitContributionSummary;
  workspace: ContributionWorkspaceStatus;
}

export interface ContributionPermission {
  canViewContributionPage: boolean;
  /** HOST | IC_SUPPORT | DEPARTMENT_RELATED | STUDENT_RELATED. Display only — never an auth input. */
  relation: string;
  participantRole?: string | null;
  participantStatus?: string | null;

  canViewRequestSummary: boolean;
  canViewAgendaSummary: boolean;
  canViewParticipantSummary: boolean;

  canViewLogisticsSummary: boolean;
  canViewRelatedLogisticsOnly: boolean;
  canViewFullLogisticsSummary: boolean;

  canViewMinutes: boolean;
  canEditMinutes: boolean;

  canViewMedia: boolean;
  canUploadMedia: boolean;

  canViewNews: boolean;
  canCreateNews: boolean;
  canEditNews: boolean;

  /** True when the instance is CLOSED/CANCELLED — the whole workspace is view-only. */
  isReadOnly: boolean;
}

export interface VisitContributionSummary {
  visitRequestId: number;
  visitInstanceId: number;
  delegationName: string;
  requestStatus: string;
  instanceStatus: VisitInstanceStatus;
  plannedStartAt: string;
  plannedEndAt: string;
  campusName?: string | null;
  hostUserId?: number | null;
  hostName?: string | null;
  guestCount: number;
  request?: VisitProcessRequestSummary | null;
  agenda: VisitAgendaItem[];
  participants: VisitParticipantListItem[];
  logistics: ContributionLogisticsItem[];
}

export interface ContributionLogisticsItem {
  logisticsItemId: number;
  itemType?: string | null;
  title: string;
  status: string;
  priority?: string | null;
  requestedToDepartmentId?: number | null;
  departmentName?: string | null;
  assignedToUserId?: number | null;
  assignedToName?: string | null;
}

export interface ContributionWorkspaceStatus {
  minutes: MinutesContributionStatus | null;
  media: MediaContributionStatus | null;
  news: NewsContributionStatus | null;
}

export interface MinutesContributionStatus {
  hasMinutes: boolean;
  status: string;
  content?: string | null;
  lockedByUserId?: number | null;
  lockedByName?: string | null;
  lockedUntil?: string | null;
  updatedAt?: string | null;
  canCurrentUserTakeLock: boolean;
  canCurrentUserEdit: boolean;
}

export interface MediaContributionStatus {
  items: ContributionMediaItem[];
  requiredMinimumCount: number;
  uploadedCount: number;
  isRequirementSatisfied: boolean;
  canCurrentUserUpload: boolean;
}

export interface ContributionMediaItem {
  mediaId: number;
  fileName: string;
  fileType: string;
  url: string;
  thumbnailUrl?: string | null;
  uploadedByUserId: number;
  uploadedByName: string;
  uploadedAt: string;
  description?: string | null;
  isPrimary: boolean;
}

export interface NewsContributionStatus {
  hasNews: boolean;
  newsId?: number | null;
  status: string;
  title?: string | null;
  description?: string | null;
  createdByName?: string | null;
  updatedAt?: string | null;
  rejectionReason?: string | null;
  newsNotRequired: boolean;
  mediaConsentAllowed: boolean;
  canCurrentUserCreate: boolean;
  canCurrentUserEdit: boolean;
}

export interface ContributionSectionStatus {
  canView: boolean;
  canEdit: boolean;
  /** Phase 1 marker — the full editor/list is not wired on this page yet. */
  placeholder: boolean;
}

export interface ProcessSummaryPage {
  permissions: ProcessSummaryPermission;
  requestSummary?: VisitProcessRequestSummary | null;
  agendaSummary: VisitAgendaItem[];
  participantSummary: VisitParticipantListItem[];
  logisticsSummary: ContributionLogisticsItem[];
  minutesSummary: ContributionSectionStatus;
  mediaSummary: ContributionSectionStatus;
  newsSummary: ContributionSectionStatus;
}

export interface ProcessSummaryPermission {
  canViewSummaryPage: boolean;
  relation: string;
  canViewRequestSummary: boolean;
  canViewAgendaSummary: boolean;
  canViewParticipantSummary: boolean;
  canViewLogisticsSummary: boolean;
  canViewMinutesSummary: boolean;
  canViewMediaSummary: boolean;
  canViewNewsSummary: boolean;
  canViewFeedbackSummary: boolean;
  canViewTimeline: boolean;
  isReadOnly: boolean;
  instanceStatus: string;
  campusName?: string | null;
  delegationName: string;
  hostName?: string | null;
  plannedStartAt: string;
  plannedEndAt: string;
}

/** A user eligible to be invited/assigned as a supporting participant, with conflict info. */
export interface ParticipantCandidate {
  userId: number;
  fullName: string;
  email: string;
  phone?: string | null;
  studentCode?: string | null;
  roleCode: string;
  subRole?: string | null;
  departmentId?: number | null;
  departmentName?: string | null;
  campusId?: number | null;
  campusName?: string | null;
  conflictCount: number;
  hasPrivateConflict: boolean;
  conflictSummary?: string | null;
  canInvite: boolean;
  disabledReason?: string | null;
}

/** A GENERAL department the host can invite to support, with its resolved active leader. */
export interface SupportDepartment {
  departmentId: number;
  departmentName: string;
  campusId: number;
  campusName?: string | null;
  leaderUserId?: number | null;
  leaderName?: string | null;
  leaderEmail?: string | null;
  canInvite: boolean;
  disabledReason?: string | null;
}

export type InviteParticipantType = 'IC_SUPPORT' | 'STUDENT' | 'DEPT_SUPPORT';

export interface InviteVisitParticipantPayload {
  participantType: InviteParticipantType;
  userId?: number;
  departmentId?: number;
  message?: string | null;
  /** Optional host-edited email content from the "Xem trước email" modal. */
  emailOverride?: EmailOverridePayload;
}

export interface InviteVisitParticipantResult {
  participantId: number;
  userId: number;
  participantRole: string;
  status: string;
  emailQueued: boolean;
  emailRecipient: string;
  message: string;
  /** SENT | FAILED. */
  emailStatus?: string;
  sentEmailId?: number;
}

/** Host-edited email content carried on send/invite commands (Part C). When useEditedContent is
 * true the backend uses this subject/body and injects the real system action block itself. */
export interface EmailOverridePayload {
  useEditedContent: boolean;
  subject: string;
  /** Plain-text edit (legacy plain-text editor): backend converts it to safe HTML. */
  bodyText?: string;
  /** Rich editor: sanitized HTML body (inline images already rewritten to cid:). Preferred when set. */
  bodyHtml?: string;
  /** File + inline-image references for the rich editor (validated + sent as real MIME parts). */
  attachments?: EmailAttachmentRefInput[];
}

/** A file/inline-image reference carried by the rich email editor (mirrors backend EmailDraftAttachmentInput). */
export interface EmailAttachmentRefInput {
  fileId: number;
  /** ATTACHMENT | INLINE_IMAGE (defaults to ATTACHMENT). */
  attachmentType?: 'ATTACHMENT' | 'INLINE_IMAGE';
  /** Required for INLINE_IMAGE (the cid the HTML body references). */
  contentId?: string | null;
  displayName?: string | null;
  displayOrder?: number;
}

/**
 * One snapshot attendance row (minute_participants). The source is distinguished purely by the SQL
 * columns: `userId` set → internal; `guestMemberId` set → guest; both null → manual. `participantKind`
 * is a derived display value from the backend.
 */
export interface MinuteParticipant {
  minuteParticipantId: number;
  minutesId: number;
  userId: number | null;
  guestMemberId: number | null;
  fullNameSnapshot: string;
  roleSnapshot: string | null;
  organizationSnapshot: string | null;
  emailSnapshot: string | null;
  attendanceStatus: string; // PRESENT | ABSENT | EXCUSED
  attendanceNote: string | null;
  checkedAt: string | null;
  checkedBy: number | null;
  displayOrder: number;
  participantKind: string; // INTERNAL | GUEST | MANUAL
}

/** One action item (minute_action_items). No assignee column exists in SQL. */
export interface MinuteActionItem {
  actionItemId: number;
  minutesId: number;
  title: string;
  note: string | null;
  dueDate: string | null; // ISO; render with first 10 chars for a date input
  status: string; // TODO | IN_PROGRESS | DONE | CANCELLED
  completedAt: string | null;
  displayOrder: number;
}

/** One picked-able system user for the manual-add dropdown. */
export interface MinuteUserSearchItem {
  userId: number;
  fullName: string;
  email: string | null;
  organization: string | null;
}

/**
 * Phase 3: the single meeting-minutes record for a campus instance (UC biên bản) with edit-lock
 * state + backend action flags. `editLockToken` is only present for the lock holder.
 */
export interface VisitMinute {
  exists: boolean;
  minutesId: number | null;
  visitInstanceId: number;
  title: string | null;
  content: string | null;
  status: string | null;
  rowVersion: number;
  /** When the minutes was last saved — drives the "Đã lưu · <time>" status line. */
  updatedAt?: string | null;
  editLockedBy?: number | null;
  editLockedByName?: string | null;
  editLockedAt?: string | null;
  editLockExpiresAt?: string | null;
  isLockedByOther: boolean;
  isLockedByMe: boolean;
  editLockToken?: string | null;
  canView: boolean;
  canCreate: boolean;
  canEdit: boolean;
  participants: MinuteParticipant[];
  actionItems: MinuteActionItem[];
}

/** Payload row for saving an attendance entry (matches SaveMinuteParticipantInput on the backend). */
export interface SaveMinuteParticipantPayload {
  minuteParticipantId: number | null;
  userId: number | null;
  guestMemberId: number | null;
  fullNameSnapshot: string;
  roleSnapshot: string | null;
  organizationSnapshot: string | null;
  emailSnapshot: string | null;
  attendanceStatus: string;
  attendanceNote: string | null;
}

/** Payload row for saving an action item (matches SaveMinuteActionItemInput on the backend). */
export interface SaveMinuteActionItemPayload {
  actionItemId: number | null;
  title: string;
  note: string | null;
  dueDate: string | null; // business wall-clock datetime "YYYY-MM-DDTHH:mm:ss" (no timezone) or null
  status: string;
}

/** Phase 4: one news post attached to a campus instance (UC tin tức). */
export interface VisitNews {
  newsId: number;
  visitInstanceId: number;
  title: string;
  summary: string | null;
  body: string | null;
  status: string; // PENDING_REVIEW | REJECTED | PUBLISHED | HIDDEN
  isPublished: boolean;
  authorUserId: number;
  authorName: string | null;
  submittedAt: string;
  publishedAt: string | null;
  reviewNote: string | null;
  rowVersion: number;
  /** Chỉ TÁC GIẢ và bài đang PENDING_REVIEW/REJECTED. */
  canEdit: boolean;
  /** Chỉ Staff Leader đúng campus và bài đang PENDING_REVIEW. */
  canApprove: boolean;
  canReject: boolean;
}

/** News list for a campus instance + whether the caller may add a post. */
export interface VisitNewsList {
  visitInstanceId: number;
  canView: boolean;
  canCreate: boolean;
  items: VisitNews[];
}

/** One row returned by GET /delegations/viewguestdelegationlist (camelCase JSON). */
export interface VisitRequestManagementItem {
  visitRequestId: number;
  visitInstanceId: number | null;
  requestCode: string | null;
  delegationName: string | null;
  partnerName: string | null;

  requestStatus: VisitRequestStatus;
  campusStatus: VisitInstanceStatus | null;
  visitScope: VisitScope | null;

  campusId: number | null;
  campusName: string | null;
  campusCount: number;

  createdByUserId: number | null;
  currentHostUserId: number | null;
  hostName: string | null;
  currentUserIsHost: boolean;

  visitorUserId: number | null;
  visitorName: string | null;

  isCurrentUserParticipant: boolean;
  participantRole: string | null;
  currentUserRelation: string;

  expectedStartAt: string | null;
  expectedEndAt: string | null;
  plannedStartAt: string | null;
  plannedEndAt: string | null;

  createdAt: string;
  submittedAt: string | null;

  // Cancellation info (UC-136) — instance-level preferred, request-level fallback.
  isCancelled?: boolean;
  cancellationLevel?: 'REQUEST' | 'CAMPUS_INSTANCE' | null;
  cancelledAt: string | null;
  cancellationReason: string | null;
  cancellationActorType?: string | null;
  cancellationSource?: string | null;
  cancelledBy?: number | null;
  cancelledByName?: string | null;

  // Decision info (UC-18/UC-22) — reject reason = decisionNote (never cancellationReason).
  // Lets the "Xem lý do từ chối" popup show who/when/role without a second fetch.
  decisionNote: string | null;
  decidedBy?: number | null;
  decidedByName?: string | null;
  decidedAt?: string | null;
  decisionActorRole?: string | null;

  // UC-136: backend-computed cancel-eligibility (APPROVED/PARTIALLY_APPROVED request + an
  // instance in ASSIGNED/BEFORE_VISIT that hasn't started). Action visibility is still driven
  // by allowedActions (CANCEL_BY_VISITOR/CANCEL_BY_HOST); this is the underlying flag.
  hasCancellableInstance?: boolean;
  hasStartedCampus?: boolean;

  // Multi-campus expandable row (Phương án A). Backend-computed action booleans + per-campus
  // progress for the accordion. campusProgressItems is empty for single-campus / instance-level rows.
  canExpandCampuses?: boolean;
  canViewRequestDetail?: boolean;
  canViewRejectReason?: boolean;
  canViewCancelReason?: boolean;
  campusProgressItems?: CampusProgressItem[];

  allowedActions: AllowedAction[];
}

// ── Submitted visit-request form snapshot ─────────────────────────────────────
// Read-only "what the guest submitted" detail, reused by the pre-approval review, the
// approved/waiting-host detail and the rejected detail screens. Mirrors the backend
// SubmittedVisitRequestFormDetailDto. Role/scope/status visibility is enforced server-side;
// canApprove/canReject/canAssignHost only gate which footer actions the modal shows.
// No agendas/participants/logistics here — those are host-created after approval.

export interface SubmittedRegistrant {
  fullName?: string | null;
  organization?: string | null;
  jobTitle?: string | null;
  phone?: string | null;
  email?: string | null;
  nationality?: string | null;
}

export interface SubmittedContactPerson {
  fullName?: string | null;
  organization?: string | null;
  phone?: string | null;
  email?: string | null;
}

export interface SubmittedCampusSchedule {
  visitInstanceId: number;
  campusId: number;
  campusCode: string;
  campusName: string;
  plannedStartAt: string;
  plannedEndAt: string;
  instanceStatus: string;
  coordinatorUserId?: number | null;
  currentHostUserId?: number | null;
  currentHostName?: string | null;
  isOwnCampus: boolean;

  // Per-campus decision info (campus-independent approval).
  decidedByUserId?: number | null;
  decidedByName?: string | null;
  decidedAt?: string | null;
  decisionActorRole?: string | null;
  decisionNote?: string | null;

  // Per-campus cancellation info (UC-136 instance-level cancel).
  cancelledByUserId?: number | null;
  cancelledByName?: string | null;
  cancelledAt?: string | null;
  cancellationActorType?: string | null;
  cancellationSource?: string | null;
  cancellationReason?: string | null;
}

/** Per-campus decision counters (campus-independent approval). */
export interface CampusDecisionSummary {
  total: number;
  pending: number;
  approved: number;
  rejected: number;
  cancelled: number;
}

export interface SubmittedGuestMember {
  guestMemberId: number;
  memberType: string;
  fullName: string;
  organization?: string | null;
  jobTitle?: string | null;
  nationality?: string | null;
  displayOrder: number;
}

export interface SubmittedVisitRequestFormDetail {
  visitRequestId: number;
  requestCode: string;
  createdSource?: string | null;
  submittedAt?: string | null;
  emailVerifiedAt?: string | null;
  requestStatus: string;
  visitScope: string;

  delegationName: string;
  visitType?: string | null;
  visitTypeOther?: string | null;
  purpose?: string | null;
  workingContent?: string | null;

  registrant: SubmittedRegistrant;
  contactPerson: SubmittedContactPerson;

  // "Yêu cầu & Xác nhận bổ sung" — guest-entered.
  workingLanguage?: string | null;
  mediaConsentStatus?: string | null;
  mediaConsentNote?: string | null;
  /** Free text the guest entered to identify the transportation to FPTU. */
  transportationNote?: string | null;
  noteToFptu?: string | null;

  campuses: SubmittedCampusSchedule[];
  /** Per-campus decision counters (campus-independent approval). */
  campusDecisionSummary?: CampusDecisionSummary;
  guestMembers: SubmittedGuestMember[];
  externalSupportMembers: SubmittedGuestMember[];

  // Decision info mirror of the caller-relevant instance (single-campus / own campus);
  // multi-campus decision detail rides on each campuses[] entry. decisionNote = reject reason.
  decidedByUserId?: number | null;
  decidedByName?: string | null;
  decisionActorRole?: string | null;
  decidedAt?: string | null;
  decisionNote?: string | null;

  // Cancellation info (UC-136). cancellationReason = cancel reason (never the reject reason).
  isCancelled?: boolean;
  cancellationLevel?: 'REQUEST' | 'CAMPUS_INSTANCE' | null;
  cancelledByUserId?: number | null;
  cancelledByName?: string | null;
  cancelledAt?: string | null;
  cancellationActorType?: string | null;
  cancellationSource?: string | null;
  cancellationReason?: string | null;

  // Approve = approve + assign host in ONE action (no separate assign-host step anymore).
  canApprove: boolean;
  canReject: boolean;
  canCancel: boolean;
}

/** A staff member who can be picked as host, with any schedule conflict pre-computed. */
export interface HostCandidate {
  userId: number;
  fullName: string;
  email: string;
  campusId: number | null;
  departmentName: string | null;
  subRole: string | null;
  /** Display label for the role ("Staff Leader" for the self-host option, "IC Staff" otherwise). */
  roleLabel?: string | null;
  /** True when this candidate IS the calling Staff Leader (self-host option). */
  isSelf?: boolean;
  /** True for the "Tôi làm host chính" option. */
  isStaffLeaderSelfHostOption?: boolean;
  hasScheduleConflict: boolean;
  conflictCount: number;
  conflicts: HostConflict[];
}

export interface HostConflict {
  source: 'CALENDAR' | 'VISIT_INSTANCE';
  title: string | null;
  startAt: string;
  endAt: string;
  visitInstanceId?: number | null;
  calendarEventId?: number | null;
}

export interface RejectVisitRequestPayload {
  reason: string;
}

export interface CancelVisitRequestPayload {
  /** Reason for cancellation; for an external (host) confirmation, include channel/time/who. */
  cancellationReason: string;
}

export interface CancelledCampus {
  visitInstanceId: number;
  status: string;
}

export interface CancelVisitRequestResult {
  visitRequestId: number;
  requestStatus: string;
  cancelledCampuses: CancelledCampus[];
  message: string;
}

/** Invitation status as returned by GET /delegations/my-invitations (UC-27). */
export type InvitationStatus = 'INVITED' | 'ACCEPTED' | 'DECLINED' | 'ASSIGNED';

/** Actions on the invitation-detail screen (NOT on the "Đơn mời tham dự" tab). */
export type InvitationAction = 'VIEW_DETAIL' | 'ACCEPT_INVITATION' | 'DECLINE_INVITATION' | 'ASSIGN_TO_DEPARTMENT_STAFF' | 'VIEW_REQUEST_FORM' | 'OPEN_CONTRIBUTION';

/** One visit-participation invitation addressed to the signed-in user (UC-27). */
export interface VisitInvitation {
  participantId: number;
  visitRequestId: number;
  visitInstanceId: number;
  requestCode: string | null;
  delegationName: string | null;
  organizationName: string | null;
  campusId: number;
  campusName: string | null;
  participantRole: ParticipantRole;
  status: InvitationStatus;
  plannedStartAt: string;
  plannedEndAt: string;
  purpose: string | null;
  workingContent: string | null;
  invitedByUserId: number | null;
  invitedByName: string | null;
  invitedAt: string | null;
  respondedAt: string | null;
  assignedByName?: string | null;
  assignedAt?: string | null;
  visitRequestStatus?: string;
  campusVisitStatus?: string;
  note: string | null;
  allowedActions: InvitationAction[];
}

/** Body for POST /delegations/participants/{id}/respond. */
export interface RespondInvitationPayload {
  accept: boolean;
  declineReason?: string | null;
}

export interface RespondInvitationResult {
  participantId: number;
  status: InvitationStatus;
  message: string;
}

export type VisitStatusFilterOption = {
  value: string;
  label: string;
  description?: string;
  requestStatus?: string;
  campusStatus?: string;
  campusStatuses?: string[];
  visitScope?: string;
  cancelledOnly?: boolean;
  relation?: string;
  readOnlyOnly?: boolean;
  actionableOnly?: boolean;
  timing?: 'UPCOMING' | 'ONGOING' | 'ENDED';
};

export type VisitScopeFilterOption = {
  value: string;
  label: string;
};

export type VisitRelationFilterOption = {
  value: string;
  label: string;
};

export type VisitFilterConfig = {
  showKeyword: boolean;
  showStatus: boolean;
  showScope: boolean;
  showRelation: boolean;
  showCampus?: boolean;
  statusLabel?: string;
  scopeLabel?: string;
  relationLabel?: string;
  statusOptions: VisitStatusFilterOption[];
  scopeOptions: VisitScopeFilterOption[];
  relationOptions: VisitRelationFilterOption[];
};

// ── Logistics item status (visit_logistics_items.status), SQL v10 2026-06-26. ──
// PLANNED / RECEIVED / READY were removed — never reintroduce them.
export type LogisticsItemStatus =
  | 'REQUESTED'
  | 'CHANGE_PROPOSED'
  | 'ASSIGNED'
  | 'ACCEPTED'
  | 'IN_PROGRESS'
  | 'DONE'
  | 'REJECTED'
  | 'DECLINED'
  | 'CANCELLED';

/** Statuses that admit no further workflow action. */
export const LOGISTICS_TERMINAL_STATUSES: LogisticsItemStatus[] = ['DONE', 'REJECTED', 'DECLINED', 'CANCELLED'];

/** Vietnamese badge label + tailwind classes for each logistics status. */
export const LOGISTICS_STATUS_META: Record<LogisticsItemStatus, { label: string; cls: string }> = {
  REQUESTED:       { label: 'Đã gửi yêu cầu', cls: 'bg-amber-50 text-amber-700 border-amber-200' },
  CHANGE_PROPOSED: { label: 'Đề xuất thay đổi', cls: 'bg-violet-50 text-violet-700 border-violet-200' },
  ASSIGNED:        { label: 'Đã phân công', cls: 'bg-blue-50 text-blue-700 border-blue-200' },
  ACCEPTED:        { label: 'Đã nhận nhiệm vụ', cls: 'bg-sky-50 text-sky-700 border-sky-200' },
  IN_PROGRESS:     { label: 'Đang xử lý', cls: 'bg-indigo-50 text-indigo-700 border-indigo-200' },
  DONE:            { label: 'Hoàn tất', cls: 'bg-emerald-50 text-emerald-700 border-emerald-200' },
  REJECTED:        { label: 'Từ chối yêu cầu', cls: 'bg-red-50 text-red-700 border-red-200' },
  DECLINED:        { label: 'Nhân sự từ chối', cls: 'bg-rose-50 text-rose-700 border-rose-200' },
  CANCELLED:       { label: 'Đã hủy', cls: 'bg-slate-100 text-slate-500 border-slate-200' },
};

// ── VisitProcess logistics requests (visit_logistics_items), Host → Department. ──
export type LogisticsItemType = 'ROOM' | 'TRANSPORT' | 'MEAL' | 'EQUIPMENT' | 'BANNER' | 'LED' | 'OTHER';
export type LogisticsPriority = 'LOW' | 'MEDIUM' | 'HIGH' | 'URGENT';

export type LogisticsCoordinationMode = 'SYSTEM_REQUEST' | 'OFFLINE_COORDINATED';

export interface PrepareVisitLogisticsPayload {
  visitInstanceId: number;
  /** Required for SYSTEM_REQUEST; may be null for OFFLINE_COORDINATED. */
  departmentId?: number | null;
  itemType: LogisticsItemType;
  title: string;
  description?: string | null;
  quantity?: number | null;
  usageStartAt?: string | null;   // "yyyy-MM-ddTHH:mm[:ss]" wall-clock
  usageEndAt?: string | null;
  priority?: LogisticsPriority | null;
  dueAt?: string | null;
  /** SYSTEM_REQUEST (default) = send to department via system; OFFLINE_COORDINATED = handled outside. */
  coordinationMode?: LogisticsCoordinationMode | null;
  /** Required when coordinationMode = OFFLINE_COORDINATED. */
  offlineCoordinationNote?: string | null;
  emailOverride?: EmailOverridePayload;
}

export interface PrepareVisitLogisticsResult {
  success: boolean;
  businessCreated: boolean;
  logisticsItemId: number;
  emailStatus: string;            // SENT | FAILED
  sentEmailId: number;
  message: string;
}

/** One logistics request row of a campus instance (GET .../logistics). */
export interface VisitInstanceLogisticsItem {
  logisticsItemId: number;
  itemType: LogisticsItemType;
  title: string;
  description?: string | null;
  quantity?: number | null;
  status: LogisticsItemStatus;
  priority: LogisticsPriority;
  coordinationMode?: LogisticsCoordinationMode;        // SYSTEM_REQUEST | OFFLINE_COORDINATED
  offlineCoordinationNote?: string | null;
  requestedToDepartmentId?: number | null;
  departmentName?: string | null;
  requestedAt?: string | null;
  requestedBy?: number | null;
  requestedByName?: string | null;
  usageStartAt?: string | null;
  usageEndAt?: string | null;
  dueAt?: string | null;
  completedAt?: string | null;
  assignedToUserId?: number | null;
  assignedToName?: string | null;
  assigneeResponseNote?: string | null;
  decisionNote?: string | null;        // close reason for REJECTED / CANCELLED / DECLINED
  // Change-proposal: `quantity` is the PLANNED figure; the FINAL ("chốt") quantity is
  // proposedQuantity when proposalResponse === 'ACCEPTED', else quantity.
  proposedQuantity?: number | null;
  proposedUsageStartAt?: string | null;
  proposedUsageEndAt?: string | null;
  proposedDescription?: string | null;
  proposalNote?: string | null;
  proposalResponse?: 'ACCEPTED' | 'REJECTED' | null;
  proposalResponseNote?: string | null;
  // Borrow/return handover signatures (visit_logistics_item_handovers).
  handovers?: LogisticsHandover[];
}

export type LogisticsHandoverType = 'BORROW' | 'RETURN';
export type LogisticsItemCondition = 'GOOD' | 'DAMAGED' | 'MISSING' | 'OTHER';

export interface LogisticsHandover {
  handoverType: LogisticsHandoverType;
  borrowerSignedByName?: string | null;
  borrowerSignedAt?: string | null;
  providerSignedByName?: string | null;
  providerSignedAt?: string | null;
  itemCondition?: LogisticsItemCondition | null;
  conditionNote?: string | null;
}

export interface SignHandoverBorrowerPayload {
  handoverType: LogisticsHandoverType;
  itemCondition?: LogisticsItemCondition | null;
  note?: string | null;
}

export interface SignHandoverResult {
  logisticsItemId: number;
  handoverId: number;
  handoverType: LogisticsHandoverType;
  status: LogisticsItemStatus;
  signedByName: string;
  signedAt: string;
  message: string;
}

export interface GetVisitInstanceLogisticsResult {
  items: VisitInstanceLogisticsItem[];
}

// ── Email rich-editor shared enums (mirror SQL v10 email_rich_editor). ──
export type EmailBodyFormat = 'PLAIN_TEXT' | 'HTML';
export type EmailAttachmentType = 'ATTACHMENT' | 'INLINE_IMAGE';
export type EmailDraftStatus = 'DRAFT' | 'SENT' | 'DISCARDED';

// ── "Xem mail đã gửi" (sent_emails + sent_email_recipients + sent_email_attachments). ──
export interface SentEmailRecipientItem {
  recipientName?: string | null;
  recipientEmail: string;
  recipientType: string;            // TO | CC | BCC
  deliveryStatus: string;           // QUEUED | SENT | FAILED | DELIVERED
  sentAt?: string | null;
  deliveredAt?: string | null;
  errorMessage?: string | null;
}

export interface SentEmailAttachmentItem {
  sentEmailAttachmentId: number;
  fileId: number;
  attachmentType: EmailAttachmentType;  // ATTACHMENT | INLINE_IMAGE
  contentId?: string | null;
  displayName?: string | null;
  originalFilename?: string | null;
  mimeType?: string | null;
  fileSize?: number | null;
  webViewUrl?: string | null;
  downloadUrl?: string | null;
  thumbnailUrl?: string | null;
}

/** One email_action_tokens row — the live status of an action button embedded in the email. */
export type EmailActionResultStatus = 'PENDING' | 'SUCCESS' | 'ALREADY_RESPONDED' | 'EXPIRED' | 'INVALID' | 'FAILED';

export interface SentEmailActionTokenItem {
  actionContext: string;            // PARTICIPATION_RESPONSE | LOGISTICS_*
  intendedAction: string;           // ACCEPT | DECLINE | APPROVE_PROPOSAL | ...
  recipientEmail?: string | null;
  resultStatus: EmailActionResultStatus;
  usedAction?: string | null;
  usedAt?: string | null;
  expiresAt?: string | null;
  resultMessage?: string | null;
}

export interface SentEmailHistoryItem {
  sentEmailId: number;
  templateCode?: string | null;
  templateName?: string | null;
  subject: string;
  bodySnapshot?: string | null;
  bodyFormat?: EmailBodyFormat;     // PLAIN_TEXT | HTML — how bodySnapshot renders
  emailStatus: string;              // QUEUED | SENT | PARTIAL_FAILED | FAILED | DELIVERED
  sentByName?: string | null;
  sentAt?: string | null;           // "yyyy-MM-ddTHH:mm:ss" wall-clock
  deliveredAt?: string | null;
  createdAt?: string | null;
  relatedType?: string | null;
  relatedId?: number | null;
  recipients: SentEmailRecipientItem[];
  attachments?: SentEmailAttachmentItem[];
  actionTokens?: SentEmailActionTokenItem[];
}

export interface GetSentEmailsResult {
  items: SentEmailHistoryItem[];
}

// ── VisitProcess scheduled reminders (visit_instance_reminder_settings), SQL v10. ──
export type VisitReminderChannel = 'IN_APP' | 'EMAIL';
export type VisitReminderTargetGroup = 'HOST' | 'PARTICIPANTS' | 'HOST_AND_PARTICIPANTS';
export type VisitReminderStatus = 'PENDING' | 'SENT' | 'CANCELLED' | 'FAILED';

/** One saved reminder schedule row (GET/PUT reminder-settings). */
export interface VisitReminderSetting {
  reminderSettingId: number;
  channel: VisitReminderChannel;
  targetGroup: VisitReminderTargetGroup;
  daysBefore: number;
  reminderTime: string;   // "HH:mm"
  scheduledAt: string;    // "yyyy-MM-ddTHH:mm:ss" wall-clock
  status: VisitReminderStatus;
}

export interface GetVisitReminderSettingsResult {
  items: VisitReminderSetting[];
}

/** One desired reminder configuration sent on PUT (enabled=false cancels the matching PENDING row). */
export interface SaveVisitReminderSettingItem {
  channel: VisitReminderChannel;
  targetGroup: VisitReminderTargetGroup;
  daysBefore: number;
  reminderTime: string;   // "HH:mm"
  enabled: boolean;
}

export interface SaveVisitReminderSettingsResult {
  items: VisitReminderSetting[];
  message: string;
}

export interface UpdatePreparationNoteResult {
  visitInstanceId: number;
  preparationNote: string | null;
  message: string;
}

// ── Email template preview (read-only render for "Xem trước email"). ──
export interface PreviewEmailTemplatePayload {
  templateCode: string;
  context?: Record<string, string>;
  language?: 'VI' | 'EN';
}

export interface PreviewEmailTemplateResult {
  templateCode: string;
  subject: string;
  /** Editable message content as HTML (action buttons stripped) — for the rendered preview. */
  bodyHtml: string;
  /** The same editable content as readable plain text (no HTML tags) — bind the editor to this. */
  editableBodyText: string;
  isActionTemplate: boolean;
  systemActionDescription?: string | null;
  /** Read-only (disabled) preview of the system action block, if any. */
  lockedActionBlockHtml?: string | null;
  requiredActionPlaceholders: string[];
  editable: boolean;
  /** Body format of the source template: 'PLAIN_TEXT' | 'HTML' (email_templates.body_format). */
  bodyFormat: EmailBodyFormat;
}
