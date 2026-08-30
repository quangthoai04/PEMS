// Status enums mirror the SQL v10 ENUMs (campus-independent approval). The request status
// is an AGGREGATE derived from the per-campus instance decisions; the real approve/reject
// decision lives on each campus instance.

/** visit_requests.status — aggregate of the campus-instance decisions. */
export const VisitRequestStatus = {
  /** The confirmation gate is shut: at least one campus has no confirmed operational contact yet,
   *  and while the request sits here NO Staff Leader of ANY campus may see or process it. */
  PendingContactConfirmation: 'PENDING_CONTACT_CONFIRMATION',
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
  /** This campus's own operational contact has not confirmed yet. */
  WaitingContactConfirmation: 'WAITING_CONTACT_CONFIRMATION',
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

/** Vietnamese display labels — never show the raw technical enum to users.
 *  Must read the same as VisitRowLabels.Status on the server. */
export const REQUEST_STATUS_LABELS: Record<VisitRequestStatus, string> = {
  PENDING_CONTACT_CONFIRMATION: 'Chờ đầu mối xác nhận',
  PENDING_APPROVAL: 'Chờ xử lý',
  PARTIALLY_APPROVED: 'Duyệt một phần',
  APPROVED: 'Đã duyệt',
  REJECTED: 'Đã bị từ chối',
  CANCELLED: 'Đã hủy',
};

export const INSTANCE_STATUS_LABELS: Record<VisitInstanceStatus, string> = {
  WAITING_CONTACT_CONFIRMATION: 'Chờ đầu mối xác nhận',
  WAITING_REQUEST_APPROVAL: 'Chờ xử lý tại cơ sở',
  ASSIGNED: 'Đã phân công người phụ trách',
  BEFORE_VISIT: 'Đang chuẩn bị',
  DURING_VISIT: 'Đang tiếp khách',
  AFTER_VISIT: 'Chờ đóng đoàn',
  CLOSED: 'Đã đóng đoàn',
  CANCELLED: 'Đã hủy',
  REJECTED: 'Đã bị từ chối',
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
  IC_HOST: 'Người phụ trách tiếp đón',
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
  /**
   * Hand one campus's reception-owner role to a different eligible user (campus Staff Leader).
   * Instance-scoped: a multi-campus SUMMARY row never carries it, because the handover picks a
   * campus and the summary row cannot say which — those verdicts ride on campusProgressItems.
   * Present in allowedActions only when the backend's verdict is ENABLED; a refused verdict still
   * arrives in `capabilities` with its reason so the menu can show it disabled.
   */
  TRANSFER_HOST: 'TRANSFER_HOST',
  CANCEL_BY_HOST: 'CANCEL_BY_HOST',
  CANCEL_BY_VISITOR: 'CANCEL_BY_VISITOR',
  // Visitor sửa đơn khi còn chờ xử lý toàn bộ (PENDING + mọi campus WAITING, còn ≥ 24h).
  EDIT_PENDING_REQUEST: 'EDIT_PENDING_REQUEST',
  // Visitor sửa & gửi lại đơn khi TOÀN BỘ request đã bị từ chối.
  RESUBMIT_REJECTED_REQUEST: 'RESUBMIT_REJECTED_REQUEST',
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
 * What the signed-in user genuinely IS to a row. Several can be true at once — that is the whole point
 * of the list carrying them: one person is routinely the registrant of a visit, the Host of the campus
 * receiving it and the Staff Leader who approved that campus.
 *
 * Scope matters: REGISTRANT is about the whole request, every other relation is about ONE campus.
 */
export type VisitRowRelation =
  | 'REGISTRANT'
  | 'OPERATIONAL_CONTACT'
  | 'HOST'
  | 'CAMPUS_REVIEWER'
  | 'PARTICIPANT';

/** Which screen a row (or one of its relations) opens. Routing only — it grants nothing. */
export type VisitEntryContext =
  | 'REQUEST_DETAIL'
  | 'HOST_PROCESS'
  | 'CAMPUS_REVIEW'
  | 'PROCESS_SUMMARY'
  | 'RECEPTION_DETAIL'
  | 'CONTRIBUTION';

/** One relation the caller holds on a row, with the campus it applies to and where it opens. */
export interface VisitRelationContext {
  relation: VisitRowRelation;
  /** REQUEST (the whole delegation) or INSTANCE (one campus). */
  scope: 'REQUEST' | 'INSTANCE';
  visitInstanceId?: number | null;
  campusId?: number | null;
  campusName?: string | null;
  entryContext: VisitEntryContext;
  /** True when this relation has something waiting on the caller right now. */
  requiresAction: boolean;
  /** Lower = more urgent. 1 campus review · 2 host process · 3 invitation · 4 registrant · 5 tracking. */
  priority: number;
}

/**
 * Exact, current-state answer to "what is this notification actually about, and what may THIS
 * caller do with it right now" — scoped to the exact instance the notification named (or to the
 * request itself when it named none), never to whichever relation an aggregated list row happened
 * to rank highest. Output of `delegationsApi.resolveNotificationVisitTarget`.
 */
export interface NotificationVisitTarget {
  /** False when the request (or the named instance) no longer exists at all. */
  exists: boolean;
  /** False when it exists but the caller currently holds no relation to it. */
  hasAccess: boolean;
  visitRequestId: number;
  visitInstanceId?: number | null;
  campusId?: number | null;
  campusName?: string | null;
  requestStatus?: string | null;
  campusStatus?: string | null;
  visitScope?: string | null;
  requestCode?: string | null;
  delegationName?: string | null;
  canViewRequestDetail: boolean;
  relationContexts: VisitRelationContext[];
  participantId?: number | null;
  participantStatus?: string | null;
}

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
  /** Campus-instance concurrency token, echoed back by an instance-scoped action started here. */
  rowVersion?: number;

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

  /**
   * Per-campus mutation verdicts, refused ones included. This is where an instance-scoped action on a
   * multi-campus request belongs: the summary row above has no single campus to act on.
   */
  capabilities?: VisitActionCapability[];
  /** Convenience mirror of an ENABLED TRANSFER_HOST verdict for this campus. */
  canTransferHost?: boolean;
}

/**
 * One action, one verdict, straight from the backend. The UI renders from this and never re-derives
 * permission from role, relation or status — and because a refusal carries its reason and cutoff, a
 * closed action can be shown disabled with an explanation instead of vanishing.
 */
export interface VisitActionCapability {
  code: string;
  scope: 'REQUEST' | 'INSTANCE';
  visitInstanceId?: number | null;
  enabled: boolean;
  /** Stable code (VISIT_MUTATION_CUTOFF_REACHED, …). Match on this, never on the message. */
  disabledReasonCode?: string | null;
  disabledReason?: string | null;
  /** When the window closes — present whether or not it has passed. */
  cutoffAt?: string | null;
  plannedStartAt?: string | null;
  campusName?: string | null;
  requiredLeadHours: number;
}

/** The closed vocabulary of next tasks. An unknown code still renders via its label. */
export type VisitNextTaskCode =
  | 'REVIEW_AND_ASSIGN'
  | 'COMPLETE_PREPARATION'
  | 'CONFIRM_PREPARATION'
  | 'REVIEW_AMENDMENT'
  | 'ACCEPT_HOST_HANDOVER'
  | 'RUN_RECEPTION'
  | 'COMPLETE_POST_VISIT'
  | 'CLOSE_VISIT'
  | 'NONE';

/**
 * What THIS reader should do next on a row — a third layer, next to the status and the relation.
 *
 * The frontend does not compute it: one campus status means different work to its host, its approver
 * and the visitor who filed it, so any client-side status→task mapping is wrong for two of the three.
 */
export interface VisitNextTask {
  code: VisitNextTaskCode | string;
  label: string;
  requiresAction: boolean;
  scope: 'REQUEST' | 'INSTANCE';
  visitInstanceId?: number | null;
  dueAt?: string | null;
  /** The allowedActions entry that performs it, when the list can perform it at all. */
  actionCode?: string | null;
  disabledReason?: string | null;
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

  /**
   * ASSIGNED → BEFORE_VISIT. True only for the current Host of a live campus still at ASSIGNED —
   * the Staff Leader approved it and named them, and they have not opened preparation yet. This and
   * `canEditBeforeVisit` are never both true; render the "Bắt đầu chuẩn bị" button from this flag
   * and never from `relation === 'HOST'`.
   */
  canStartPreparation: boolean;

  // Operational stage transitions (Host only, live instance).
  canStartVisit: boolean;     // BEFORE_VISIT → DURING_VISIT
  /**
   * KHUYẾN NGHỊ, không phải hạn chót: mốc `plannedStartAt - 6h`, từ đó việc chuyển sang "Trong tiếp
   * khách" là bình thường. Host vẫn được chuyển sớm hơn — `canStartVisit` không phụ thuộc mốc này và
   * backend cũng không (NP-05). Suy từ lịch hiện tại nên lịch đổi thì mốc đổi theo; null ngoài
   * BEFORE_VISIT.
   */
  recommendedStartVisitAt?: string | null;
  /** True khi Host đang chuyển SỚM hơn mốc khuyến nghị — chỉ đổi lời trong hộp xác nhận, không khóa nút. */
  isBeforeRecommendedStartWindow?: boolean;
  canCompleteVisit: boolean;  // DURING_VISIT → AFTER_VISIT
  canCloseVisit: boolean;     // AFTER_VISIT → CLOSED

  /**
   * Whether to offer "Gửi cập nhật chuẩn bị". Current host only, and only while the preparation tab
   * is open — never derived from roleCode here: a Staff Leader and HO read the same page and can pull
   * the same report, and only the backend knows that writing to the guest is a different act.
   */
  canSendSetupProgressEmail: boolean;
}

/** One default recipient of the setup-progress message, carrying the group it belongs to. */
export interface SetupProgressEmailRecipient {
  email: string;
  name?: string | null;
  recipientType: 'TO' | 'CC' | 'BCC';
}

/**
 * The Host's setup-progress message, rendered and returned — not saved.
 *
 * There is no `draftId`. The message lives in the composer until it is sent, which is what removed the
 * class of failure where a composer opened on a dead id showed a form full of generated text that looked
 * exactly like a loaded draft.
 */
export interface SetupProgressEmailMessage {
  subject: string;
  /** The body as generated, which the composer opens on and may then edit. */
  bodyHtml: string;
  /** vi | en — the language BOTH the message and the attached report were produced in. */
  languageCode: string;
  /**
   * The Schedule Report, which the composer attaches by DEFAULT — null when it could not be produced.
   *
   * Null is a normal outcome rather than an error: the PDF lives on Google Drive, the message does not,
   * and a storage failure no longer takes the whole flow down with it. The composer opens either way,
   * the reason appears in `warnings`, and the Host may remove the report even when it IS there.
   */
  reportFileId: number | null;
  reportFileName: string | null;
  /** Vietnam wall-clock moment the report — and the body's tables — were built from. */
  reportGeneratedAt: string;
  /** The default envelope, derived from the instance. The Host may change it. */
  recipients: SetupProgressEmailRecipient[];
  /**
   * Informational — a missing guest address, a fallback recipient, a report that could not be generated.
   * Not a send gate.
   */
  warnings: string[];
}

/**
 * Response of re-syncing the setup-progress message from current setup data.
 *
 * The report and the tables in the body are two renderings of ONE snapshot, so this rebuilds both;
 * `reportGeneratedAt` is the moment that snapshot was taken and is stated in the body too.
 */
export interface SetupProgressEmailRefresh {
  /**
   * The rebuilt report — null when the body was rebuilt but the PDF could not be stored.
   *
   * That combination is why this is nullable rather than the call simply failing: the composer's body now
   * describes a NEWER moment than the report it was carrying, so the old file is no longer the current
   * snapshot and must be dropped rather than left on screen looking like one.
   */
  reportFileId: number | null;
  reportFileName: string | null;
  reportGeneratedAt: string;
  languageCode: string;
  subject: string;
  /** The rebuilt body. The composer puts this in the editor, replacing any manual edits. */
  bodyHtml: string;
  /** Always true; stated explicitly so a client cannot mistake the rebuild for an additive update. */
  bodyRewritten: boolean;
  /** Why the report is missing, when it is. Empty on a clean refresh. */
  warnings: string[];
}

/** The whole message, as the setup-progress send endpoint takes it. */
export interface SendSetupProgressEmailPayload {
  subject: string;
  bodyHtml: string;
  languageCode?: string | null;
  recipients: Array<{
    email: string;
    name?: string | null;
    recipientType: 'TO' | 'CC' | 'BCC';
    displayOrder: number;
  }>;
  attachments: Array<{
    fileId: number;
    attachmentType?: 'ATTACHMENT' | 'INLINE_IMAGE';
    contentId?: string | null;
    displayName?: string | null;
    displayOrder?: number;
  }>;
}

/** One agenda (lịch trình) item of a campus instance. */
export interface VisitAgendaItem {
  agendaId: number;
  title: string;
  startTime: string;
  endTime?: string | null;
  description?: string | null;
  location?: string | null;
  /** Free-typed name of the responsible person (visit_agendas.responsible_name). Plain text, not
   * tied to a real user account. Null = unassigned. */
  responsibleName?: string | null;
  /** Suggested role text from the source template item (display-only hint, NOT a person). */
  templateResponsibleRoleLabel?: string | null;
}

/** One row of "Quản lý việc sau tiếp khách" — an action item assigned to the caller. */
export interface MyActionItem {
  actionItemId: number;
  title: string;
  note: string | null;
  dueDate: string | null;
  status: string; // TODO | IN_PROGRESS | DONE | CANCELLED
  visitInstanceId: number;
  visitRequestId: number | null;
  delegationName: string;
}

export interface MyActionItemsFilterParams {
  q?: string;
  /** Deep-link từ chuông thông báo — có giá trị thì backend trả đúng 1 item này, bỏ qua các filter khác. */
  actionItemId?: number;
  status?: 'ALL' | 'TODO' | 'DONE' | 'CANCELLED';
  fromDate?: string;
  toDate?: string;
  sortDir?: 'asc' | 'desc';
  page?: number;
  pageSize?: number;
}

export interface MyActionItemsResponse {
  items: MyActionItem[];
  totalCount: number;
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
  metadataJson?: string | null;
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
  /** Free text the guest entered to identify the transportation to FPTU. */
  transportationNote?: string | null;
  /** "Ghi chú gửi FPTU" — the guest's one general remark, independent of media consent. */
  notes?: string | null;
  // noteToFptu is gone: no backend DTO has emitted it since the per-campus cutover, so every screen
  // that rendered it rendered undefined. It survives only in retired migration scripts.

  /**
   * "Đầu mối đoàn khách phối hợp tại cơ sở" — this instance's OWN guest-side coordinator. The old
   * contactPerson* names described a request-level relation that no longer exists; the backend has
   * sent per-campus operationalContact* for a while, so those fields simply rendered blank.
   */
  operationalContactFullName?: string | null;
  operationalContactOrganization?: string | null;
  operationalContactJobTitle?: string | null;
  operationalContactPhone?: string | null;
  operationalContactEmail?: string | null;

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
  /** Needed to call the request-scoped history/timeline endpoint — this page is keyed by
   * visitInstanceId alone, which the timeline endpoint does not accept. */
  visitRequestId: number;
  permissions: ProcessSummaryPermission;
  requestSummary?: VisitProcessRequestSummary | null;
  agendaSummary: VisitAgendaItem[];
  participantSummary: VisitParticipantListItem[];
  logisticsSummary: ContributionLogisticsItem[];
  /** Real workspace state, same shape the Contribution Page uses — every edit/upload/create flag on
   * these is always false here since Process Summary is read-only end to end. */
  minutesSummary: MinutesContributionStatus | null;
  mediaSummary: MediaContributionStatus | null;
  newsSummary: NewsContributionStatus | null;
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

/**
 * A GENERAL department of the instance's campus, with its resolved active leader. One list feeds
 * TWO independent host actions, each gated by its own flag (mirrors SupportDepartmentDto):
 * - canInviteParticipant: invite the leader as DEPT_SUPPORT (false khi không có leader active
 *   HOẶC leader đã có slot INVITED/ACCEPTED/ASSIGNED trong instance).
 * - canReceiveLogistics: gửi yêu cầu hậu cần qua hệ thống (chỉ false khi không có leader active —
 *   leader đã tham gia đoàn KHÔNG chặn logistics).
 */
export interface SupportDepartment {
  departmentId: number;
  departmentName: string;
  campusId: number;
  campusName?: string | null;
  leaderUserId?: number | null;
  leaderName?: string | null;
  leaderEmail?: string | null;
  canInviteParticipant: boolean;
  participantDisabledReason?: string | null;
  canReceiveLogistics: boolean;
  logisticsDisabledReason?: string | null;
}

export type InviteParticipantType = 'IC_SUPPORT' | 'STUDENT' | 'DEPT_SUPPORT';

export interface InviteVisitParticipantPayload {
  participantType: InviteParticipantType;
  userId?: number;
  departmentId?: number;
  message?: string | null;
  /** Optional host-edited email content from the "Xem trước email" modal. */
  /** The message the sender approved in the FINAL preview. Omitted when they did not edit. */
  approvedContent?: ApprovedEmailContentPayload;
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

/**
 * The message a sender edited and approved in the FINAL preview, presented back on send.
 *
 * Replaces `EmailOverridePayload`. The difference is `finalPreviewToken`: the backend signed the exact
 * subject, body and attachment set while the sender was looking at them, and refuses a send whose
 * content hashes differently. Nothing here is trusted on its own — altering one character between the
 * preview and the send is a refusal, not a silent substitution.
 *
 * Omitted entirely when the sender read the preview and pressed send without editing: there is nothing
 * of theirs to approve, and the backend renders the template as it always did.
 */
export interface ApprovedEmailContentPayload {
  /** Issued by POST /email-templates/final-preview. */
  finalPreviewToken: string;
  subject: string;
  /** Plain-text edit: the backend converts it to safe HTML. Preferred when both are present. */
  bodyText?: string;
  /** Rich editor: HTML body with inline images already rewritten to `cid:`. */
  bodyHtml?: string;
  /** File + inline-image references (validated and sent as real MIME parts). */
  attachments?: EmailAttachmentRefInput[];
}

/** A file/inline-image reference carried by the rich email editor (mirrors backend EmailAttachmentRefInput). */
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
  /**
   * GUEST | EXTERNAL_SUPPORT for a delegation row; null for internal and manual rows (MIN-02).
   * A stored snapshot, not a live lookup — the biên bản records what the person WAS at the meeting.
   */
  sourceMemberType: string | null;
  /** The campus's contact. An ADDITIONAL badge ("Khách · Đầu mối"), never a kind of its own. */
  isOperationalContact: boolean;
  /** Neither id set: a person who exists only in this biên bản, and the only row whose
   * name / role / organisation may be edited here (MIN-04). */
  isManual: boolean;
  /** ACTIVE | EXCLUDED (MIN-03). An EXCLUDED row is out of the biên bản but remembered, so sync
   * does not put the person back and the Host can restore them. */
  syncState: string;
  fullNameSnapshot: string;
  roleSnapshot: string | null;
  organizationSnapshot: string | null;
  emailSnapshot: string | null;
  attendanceStatus: string; // PRESENT | ABSENT | EXCUSED
  attendanceNote: string | null;
  checkedAt: string | null;
  checkedBy: number | null;
  displayOrder: number;
  participantKind: string; // INTERNAL | GUEST | EXTERNAL_SUPPORT | MANUAL
  /** Looked up live from the linked guest's nationality (null for INTERNAL/MANUAL rows). */
  guestNationality: string | null;
}

/** One action item (minute_action_items). */
export interface MinuteActionItem {
  actionItemId: number;
  minutesId: number;
  title: string;
  note: string | null;
  /** Người phụ trách (minute_action_items.assigned_to_user_id) — Host hoặc participant ACCEPTED
   * (IC_SUPPORT/DEPT_SUPPORT/STUDENT). Null = chưa gán. */
  assignedToUserId: number | null;
  /** Tên hiển thị của người phụ trách (join Users tại thời điểm đọc) — null khi chưa gán. Dùng field
   * này để hiển thị (không phụ thuộc responsibleCandidates — danh sách đó chỉ phản ánh candidate
   * ĐANG hợp lệ, có thể không còn chứa người đã được gán trước đó nếu participant status đổi). */
  assignedToUserName: string | null;
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
  /**
   * ACTIVE | EXCLUDED for a source-linked row; omit (or null) to leave it as it is (MIN-03).
   * Ignored for manual rows, which are deleted outright when dropped from the list.
   */
  syncState?: string | null;
}

/** Payload row for saving an action item (matches SaveMinuteActionItemInput on the backend). */
export interface SaveMinuteActionItemPayload {
  actionItemId: number | null;
  title: string;
  note: string | null;
  assignedToUserId: number | null;
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

  /** True when a request stores different content per campus. Pure V2: every request is per-campus,
   * so this only distinguishes the display label, never which UI version to render. */
  hasMixedCampusDetails?: boolean;

  campusId: number | null;
  campusName: string | null;
  campusCount: number;

  createdByUserId: number | null;
  currentHostUserId: number | null;
  hostName: string | null;
  currentUserIsHost: boolean;
  /**
   * Concurrency token of the campus instance this row is about — null on a multi-campus summary row,
   * which is not one instance. Echoed back by an instance-scoped action started from the list.
   */
  rowVersion?: number | null;

  visitorUserId: number | null;
  visitorName: string | null;

  // ── Actor relation (registrant vs contact owner) ──
  /** Tài khoản NGƯỜI ĐĂNG KÝ (read-only tracking relation). */
  registrantUserId?: number | null;
  /** Tab "Đơn tôi đăng ký": registrant đồng thời là host chính thức của ≥1 campus. */
  isAlsoHost?: boolean;
  alsoHostVisitInstanceId?: number | null;

  isCurrentUserParticipant: boolean;
  participantRole: string | null;
  /**
   * Người dùng hiện tại có participant row nào trên instance này không — id thật, dùng để mở
   * đúng màn hình lời mời / công việc phòng ban. Trên tab gộp "Tất cả các loại đơn", một dòng
   * gốc ATTENDING trước đây mất id này nên bị đẩy về request detail (403).
   */
  participantId?: number | null;
  /** INVITED | ACCEPTED | DECLINED | ASSIGNED — DECLINED vẫn hiện trong lịch sử nhưng đã hết quan hệ. */
  participantStatus?: string | null;
  /**
   * Single-valued legacy relation, DISPLAY ONLY. It cannot describe someone who is registrant AND host
   * AND campus reviewer at once — use {@link VisitRequestManagementItem.relationContexts} for the real
   * answer, and `allowedActions` for what they may do. Never gate an action on this.
   */
  currentUserRelation: string;

  // ── Multi-relation truth (backend-computed; a filter never changes it) ──
  /** Distinct relation codes the caller genuinely holds on this row. */
  relations?: VisitRowRelation[];
  /** The same relations with their campus scope, entry screen and urgency. Most urgent first. */
  relationContexts?: VisitRelationContext[];
  /**
   * Where this row should open by default — ROUTING, not permission. Absent when the caller holds none
   * of the five relations (HO monitoring, a Department/Student assignment row): those keep their own
   * established routing.
   */
  primaryEntryContext?: VisitEntryContext | null;
  /** The campus instance {@link primaryEntryContext} targets. Null for a request-scoped entry. */
  primaryEntryVisitInstanceId?: number | null;

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

  // ── Visitor edit / resubmit (SQL v10 resubmit_agenda_cancel24) ──
  resubmissionCount?: number;
  lastResubmittedAt?: string | null;
  lastResubmittedBy?: number | null;
  lastResubmittedByName?: string | null;
  /** Backend-computed: đơn còn sửa được (PENDING toàn phần + còn ≥ 24h). */
  canEditPending?: boolean;
  /** Backend-computed: đơn bị từ chối toàn bộ, có thể sửa & gửi lại. */
  canResubmit?: boolean;

  // Multi-campus expandable row (Phương án A). Backend-computed action booleans + per-campus
  // progress for the accordion. campusProgressItems is empty for single-campus / instance-level rows.
  canExpandCampuses?: boolean;
  /**
   * Backend nói rõ dòng này có mở được request detail (`/dashboard/visit/v2/{id}`) hay không —
   * tính từ ĐÚNG các quan hệ mà `VisitFormReadService` cho phép. `false` không có nghĩa dòng sai:
   * nó đến từ quan hệ khác (lời mời đã từ chối, phân công lịch trình) và phải điều hướng sang
   * màn hình của quan hệ đó, KHÔNG fallback về request detail.
   */
  canViewRequestDetail?: boolean;
  canViewRejectReason?: boolean;
  canViewCancelReason?: boolean;
  campusProgressItems?: CampusProgressItem[];

  allowedActions: AllowedAction[];

  /** Mutation verdicts for this row, refused ones included with their reason and cutoff. */
  capabilities?: VisitActionCapability[];

  // ── The three layers a row keeps apart: where it IS, what I am to it, what I should DO ──
  /** Process status in Vietnamese, resolved server-side from the campus status (or the aggregate). */
  statusLabel?: string | null;
  /**
   * The canonical status CODE `statusLabel` was resolved from (backend VisitRowLabels.EffectiveCode).
   * Filtering on `effectiveStatus`/`effectiveStatuses` matches this exact field, so a row can never
   * pass a status filter while showing a different badge (P0-01). Prefer this over re-deriving a
   * badge kind from `requestStatus`/`campusStatus` on the client.
   */
  effectiveStatusCode?: string | null;
  /** What the signed-in user is to this row ("Bạn phụ trách tiếp đón", "Chỉ theo dõi", …). */
  relationLabel?: string | null;
  /**
   * Which relationship view this row came from: RESPONSIBLE | INVITED | REGISTERED | HOSTED |
   * MY_REQUESTS. On the merged "Tất cả các loại đơn" tab (Staff Leader), rows from different
   * sources are mixed in one list — this is how the frontend tells them apart per-row instead
   * of assuming the page-level active tab describes every row.
   */
  tabType?: string | null;
  /** Backend-decided next task. NONE is a real answer, not an absent one. */
  nextTask?: VisitNextTask | null;

  /**
   * Where the search keyword matched, scoped to the campuses this caller may see (a hidden campus never
   * appears). Null/absent when there is no keyword. Stable field CODES only — the UI maps them to labels;
   * no raw matched text or PII is ever sent.
   */
  matchedContexts?: SearchMatchContext[] | null;

  /** What moved on this row since the caller last looked. Absent when nothing has. */
  changeSummary?: VisitListChangeSummary | null;
}

/** Badge vocabulary from the backend. An unrecognised code renders as a neutral "updated". */
export type VisitListEventCode = 'CONTENT_UPDATED' | 'STATUS_CHANGED' | 'HOST_CHANGED' | 'DECIDED';

/**
 * The "something moved here" signal — deliberately SEPARATE from the row's status.
 *
 * A request still sitting at "Chờ xử lý" after the visitor rewrote it is still, correctly, chờ xử lý.
 * Overwriting the status with "Đã sửa" would destroy the field people sort and filter by, so this
 * rides alongside it instead.
 */
export interface VisitListChangeSummary {
  hasUnreadChanges: boolean;
  unreadChangeCount: number;
  latestEventCode?: VisitListEventCode | string | null;
  latestChangedAt?: string | null;
  pendingAmendmentCount: number;
  /** True when something here is waiting on THIS person, not merely informing them. */
  requiresViewerAction: boolean;
  campusIndicators: VisitListCampusIndicator[];
}

export interface VisitListCampusIndicator {
  visitInstanceId: number;
  campusName?: string | null;
  eventCode: VisitListEventCode | string;
  occurredAt: string;
  requiresAction: boolean;
}

/** Scope discriminator for a {@link SearchMatchContext}. */
export type SearchMatchScope = 'REQUEST' | 'CAMPUS';

/** One place a search keyword matched (request-wide, or a specific authorized campus). */
export interface SearchMatchContext {
  scope: SearchMatchScope;
  visitInstanceId?: number | null;
  campusId?: number | null;
  campusName?: string | null;
  /** Stable field codes (REQUEST_CODE, DELEGATION_NAME, CAMPUS, …) mapped to VI/EN labels client-side. */
  matchedFields: string[];
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

/**
 * ONE campus's guest-side coordinator, as the submitted-detail screen reads it. Per campus — the
 * request-level SubmittedContactPerson it replaces is what let a mixed request show one campus's
 * contact as if it spoke for all of them.
 */
export interface SubmittedOperationalContact {
  fullName?: string | null;
  organization?: string | null;
  jobTitle?: string | null;
  phone?: string | null;
  email?: string | null;
  confirmed: boolean;
  confirmedAt?: string | null;
  /** REGISTRANT_SELF_MATCH | EMAIL_CONFIRMATION | TRANSFER. */
  confirmationSource?: string | null;
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

  /** This campus's OWN guest-side coordinator — never a sibling's, never a request-level stand-in. */
  operationalContact: SubmittedOperationalContact;

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
  hasMixedCampusDetails?: boolean;

  delegationName: string;
  visitType?: string | null;
  visitTypeOther?: string | null;
  purpose?: string | null;
  workingContent?: string | null;

  registrant: SubmittedRegistrant;

  // "Yêu cầu & Xác nhận bổ sung" — guest-entered.
  workingLanguage?: string | null;
  mediaConsentStatus?: string | null;
  /** Free text the guest entered to identify the transportation to FPTU. */
  transportationNote?: string | null;
  /** "Ghi chú gửi FPTU" — the guest's one general remark, independent of media consent. */
  notes?: string | null;
  // noteToFptu is gone — see the note on VisitProcessRequestSummary above.

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
  /** The INVITATION's own text — what the inviter wrote, and on a declined row the reason given back. */
  note: string | null;
  /**
   * "Ghi chú gửi FPTU" — the guest's one general remark about the invited campus, from
   * visit_instance_form_details.notes. Kept apart from `note` above on purpose: they are written by
   * different people about different things, and a screen showing both must not blur them.
   */
  notesToFptu?: string | null;
  allowedActions: InvitationAction[];
}

/** Body for POST /delegations/participants/{id}/respond. */
export interface RespondInvitationPayload {
  accept: boolean;
  declineReason?: string | null;
  /** Optional remark left by the invitee when accepting (accept=true only). Stored on the same
   * visit_participants.note column declineReason uses — the two never coexist on one call. */
  note?: string | null;
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
  /**
   * Canonical status filter — matches a row's own `effectiveStatusCode`/`statusLabel` exactly (see
   * backend `ViewGuestDelegationListQuery.EffectiveStatus`), so filter and badge can never disagree
   * (P0-01). Every option in this file should set this (or `effectiveStatuses` for a deliberate
   * group of several codes) instead of the legacy fields below, which stay only for backward compat.
   */
  effectiveStatus?: string;
  /**
   * Comma-separated union of `effectiveStatus` codes — ONLY for codes that render the SAME badge
   * text (e.g. Visitor/default-fallback's "Từ chối" reads the same for a couple of edge codes).
   * Never a lifecycle grouping across DIFFERENT badge text (e.g. must not also include BEFORE_VISIT/
   * DURING_VISIT/AFTER_VISIT/CLOSED under "Đã duyệt" — those have their own distinct badge text and
   * their own option). "Đã duyệt" itself is a SINGLE code (`effectiveStatus: 'ASSIGNED'`, not a
   * union — dropped the `ASSIGNED,APPROVED` aggregate union 2026-08-23): request-level views (HO,
   * Visitor, "registered", "all") match "any campus instance carries this exact status"
   * (REQUEST_ANY_CAMPUS — docs/CanhIter3FixBug/GopYCQuyen/PEMS_ROLE_TAB_STATUS_FILTER_COMPREHENSIVE_
   * FIX_PLAN.md), so a multi-campus request straddling two stages correctly appears under BOTH of
   * their filters at once instead of being force-collapsed into one canonical bucket.
   */
  effectiveStatuses?: string;
  /** @deprecated Legacy — kept working for any caller not yet on effectiveStatus; superseded by it. */
  requestStatus?: string;
  /** @deprecated Legacy — kept working for any caller not yet on effectiveStatus; superseded by it. */
  campusStatus?: string;
  campusStatuses?: string[];
  visitScope?: string;
  /** @deprecated Legacy — superseded by `effectiveStatus: 'CANCELLED'`, which additionally requires the row's own badge to actually read "Đã hủy" (not merely that some sibling campus was cancelled). */
  cancelledOnly?: boolean;
  /** @deprecated HO's old merged "Chờ duyệt" row — superseded by `effectiveStatus: 'WAITING_REQUEST_APPROVAL'`. */
  pendingApprovalAny?: boolean;
  /** @deprecated HO's old merged "Đã duyệt" row — superseded by `effectiveStatus: 'ASSIGNED'` (single code, no aggregate union). */
  approvedAny?: boolean;
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
  /** SYSTEM_REQUEST (default) = send to department via system; OFFLINE_COORDINATED = handled outside. */
  coordinationMode?: LogisticsCoordinationMode | null;
  /** Required when coordinationMode = OFFLINE_COORDINATED. */
  offlineCoordinationNote?: string | null;
  /** The message the sender approved in the FINAL preview. Omitted when they did not edit. */
  approvedContent?: ApprovedEmailContentPayload;
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
  coordinationMode?: LogisticsCoordinationMode;        // SYSTEM_REQUEST | OFFLINE_COORDINATED
  offlineCoordinationNote?: string | null;
  requestedToDepartmentId?: number | null;
  departmentName?: string | null;
  requestedAt?: string | null;
  requestedBy?: number | null;
  requestedByName?: string | null;
  usageStartAt?: string | null;
  usageEndAt?: string | null;
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
  /** JSON checklist xe điện (TRANSPORT, dòng BORROW) — null nếu không phải TRANSPORT. */
  checklistJson?: string | null;
}

export interface SignHandoverBorrowerPayload {
  handoverType: LogisticsHandoverType;
  itemCondition?: LogisticsItemCondition | null;
  note?: string | null;
  /** Cột "Tình trạng nghiệm thu" của checklist — chỉ gửi khi ký RETURN (Host điền sau khi bàn giao xong). */
  checklistJson?: string | null;
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

// ── VisitProcess scheduled reminders (visit_instance_reminder_settings). offsetMinutes (2026-08-27)
// replaced daysBefore + reminderTime: scheduled_at = planned_start_at - offsetMinutes. ──
export type VisitReminderChannel = 'IN_APP' | 'EMAIL';
export type VisitReminderTargetGroup = 'HOST' | 'PARTICIPANTS' | 'HOST_AND_PARTICIPANTS';
export type VisitReminderStatus = 'PENDING' | 'SENT' | 'CANCELLED' | 'FAILED';

/** One saved reminder schedule row (GET/PUT reminder-settings). */
export interface VisitReminderSetting {
  reminderSettingId: number;
  channel: VisitReminderChannel;
  targetGroup: VisitReminderTargetGroup;
  offsetMinutes: number;   // minutes before the visit's planned start
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
  offsetMinutes: number;   // minutes before the visit's planned start
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
  /** What the message is about. Signed into the preview token; the send re-derives and compares it. */
  visitInstanceId?: number | null;
  campusId?: number | null;
  departmentId?: number | null;
  /**
   * The scope this message belongs to, spelled exactly as the sending command will recompute it — e.g.
   * `visitInstance:41|participant:907`. Build it with `emailScopeKey`, never by hand: a mismatch is a
   * refused send, and the two spellings live in different codebases.
   */
  scopeKey?: string | null;
}

export interface PreviewEmailTemplateResult {
  templateCode: string;
  subject: string;
  /**
   * What the EDITOR opens on: the rendered body with an inert system node where the action block goes,
   * and no branded shell. Never render this read-only — that is `initialFinalPreviewHtml`'s job.
   */
  editableBodyHtml: string;
  /** The same editable content as readable plain text (no HTML tags) — bind the editor to this. */
  editableBodyText: string;
  /**
   * The whole message as it stands before anybody edits it — action block in position, branded shell,
   * assembled by the same backend composer the final preview uses. This is what VIEW shows.
   *
   * Its buttons are inert spans with no href and no token: near enough to the real thing to approve,
   * impossible to answer your own message with.
   */
  initialFinalPreviewHtml: string;
  isActionTemplate: boolean;
  systemActionDescription?: string | null;
  /** Read-only (disabled) preview of the system action block, if any. */
  lockedActionBlockHtml?: string | null;
  requiredActionPlaceholders: string[];
  editable: boolean;
  /** Body format of the source template: 'PLAIN_TEXT' | 'HTML' (email_templates.body_format). */
  bodyFormat: EmailBodyFormat;
  /**
   * Where a reply would go — the sender's own address, or the system support address for automated
   * mail. Reported as its own field rather than read out of the body, which would be guessing at
   * something the send decides from the account.
   */
  replyToEmail?: string | null;
  /** True when this template's send flow may offer "Chỉnh sửa". Decided by capability, not by wording. */
  runtimeEditable: boolean;
  /** Signed proof of this render, to be handed to `final-preview`. Null when not runtime-editable. */
  previewToken?: string | null;
  /** ISO-8601 instant after which `previewToken` stops being accepted. */
  expiresAt?: string | null;
}

// ── Final preview ("Xem trước kết quả") ──

export interface BuildFinalEmailPreviewPayload {
  previewToken: string;
  subject: string;
  /** Plain-text edit. Preferred over `editableBodyHtml` when both are present, matching the send. */
  editableBodyText?: string;
  editableBodyHtml?: string;
  attachments?: EmailAttachmentRefInput[];
  language?: 'VI' | 'EN';
}

export interface BuildFinalEmailPreviewResult {
  subject: string;
  /** The whole message as it will arrive — sender's words, locked action block, branded shell. */
  finalPreviewHtml: string;
  /** Presented on send. Binds actor, template, revision, scope, content and attachments. */
  finalPreviewToken: string;
  replyToEmail?: string | null;
  expiresAt: string;
}
