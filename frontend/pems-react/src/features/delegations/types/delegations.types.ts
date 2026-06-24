// Status enums mirror the SQL v8.3 ENUMs. The request status is a *decision* status
// only; visit progress is derived from the per-campus instance status.

/** visit_requests.status — request decision status only. */
export const VisitRequestStatus = {
  PendingApproval: 'PENDING_APPROVAL',
  Approved: 'APPROVED',
  Rejected: 'REJECTED',
  Cancelled: 'CANCELLED',
} as const;
export type VisitRequestStatus =
  (typeof VisitRequestStatus)[keyof typeof VisitRequestStatus];

/** visit_request_campuses.status — the actual visit progress per campus. */
export const VisitInstanceStatus = {
  WaitingRequestApproval: 'WAITING_REQUEST_APPROVAL',
  WaitingHostAssignment: 'WAITING_HOST_ASSIGNMENT',
  Assigned: 'ASSIGNED',
  BeforeVisit: 'BEFORE_VISIT',
  DuringVisit: 'DURING_VISIT',
  AfterVisit: 'AFTER_VISIT',
  Closed: 'CLOSED',
  Cancelled: 'CANCELLED',
} as const;
export type VisitInstanceStatus =
  (typeof VisitInstanceStatus)[keyof typeof VisitInstanceStatus];

/** Vietnamese display labels — never show the raw technical enum to users. */
export const REQUEST_STATUS_LABELS: Record<VisitRequestStatus, string> = {
  PENDING_APPROVAL: 'Chờ duyệt',
  APPROVED: 'Đã duyệt',
  REJECTED: 'Từ chối',
  CANCELLED: 'Đã hủy',
};

export const INSTANCE_STATUS_LABELS: Record<VisitInstanceStatus, string> = {
  WAITING_REQUEST_APPROVAL: 'Chờ duyệt đơn',
  WAITING_HOST_ASSIGNMENT: 'Chờ phân công host',
  ASSIGNED: 'Đã phân công',
  BEFORE_VISIT: 'Đang chuẩn bị',
  DURING_VISIT: 'Đang diễn ra',
  AFTER_VISIT: 'Hậu xử lý',
  CLOSED: 'Đã đóng',
  CANCELLED: 'Đã hủy',
};

/** A campus instance may only be cancelled while it is ASSIGNED or BEFORE_VISIT. */
export const CANCELLABLE_INSTANCE_STATUSES: VisitInstanceStatus[] = [
  VisitInstanceStatus.Assigned,
  VisitInstanceStatus.BeforeVisit,
];

/**
 * UC-136: the Cancel action is available only when the request is APPROVED and the
 * campus instance is still ASSIGNED or BEFORE_VISIT. Used to decide whether to show
 * the Cancel button (the backend remains the final authority).
 */
export function canCancelInstance(
  requestStatus: VisitRequestStatus,
  instanceStatus: VisitInstanceStatus
): boolean {
  return (
    requestStatus === VisitRequestStatus.Approved &&
    CANCELLABLE_INSTANCE_STATUSES.includes(instanceStatus)
  );
}

/** Visit scope — drives who approves (single → Staff Leader, multi → HO). */
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
  HO_APPROVE: 'HO_APPROVE',
  HO_REJECT: 'HO_REJECT',
  APPROVE_AND_ASSIGN_HOST: 'APPROVE_AND_ASSIGN_HOST',
  CAMPUS_REJECT: 'CAMPUS_REJECT',
  TRANSFER_HOST: 'TRANSFER_HOST',
  CANCEL_BY_HOST: 'CANCEL_BY_HOST',
  CANCEL_BY_VISITOR: 'CANCEL_BY_VISITOR',
  ACCEPT_INVITATION: 'ACCEPT_INVITATION',
  DECLINE_INVITATION: 'DECLINE_INVITATION',
  ASSIGN_TO_DEPARTMENT_STAFF: 'ASSIGN_TO_DEPARTMENT_STAFF',
} as const;

export type AllowedAction = (typeof VISIT_ALLOWED_ACTIONS)[keyof typeof VISIT_ALLOWED_ACTIONS];

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
  cancelledAt: string | null;
  cancellationReason: string | null;
  decisionNote: string | null;

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
  isOwnCampus: boolean;
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
  transportationType?: string | null;
  transportationDetail?: string | null;
  noteToFptu?: string | null;

  campuses: SubmittedCampusSchedule[];
  guestMembers: SubmittedGuestMember[];
  externalSupportMembers: SubmittedGuestMember[];

  // Decision info (populated once the request was decided).
  decidedByUserId?: number | null;
  decidedByName?: string | null;
  decisionActorRole?: string | null;
  decidedAt?: string | null;
  decisionNote?: string | null;

  // Cancellation info (UC-136).
  cancelledAt?: string | null;
  cancellationReason?: string | null;

  canApprove: boolean;
  canReject: boolean;
  canAssignHost: boolean;
}

/** A staff member who can be picked as host, with any schedule conflict pre-computed. */
export interface HostCandidate {
  userId: number;
  fullName: string;
  email: string;
  campusId: number | null;
  departmentName: string | null;
  subRole: string | null;
  activeAssignmentCount: number;
  hasScheduleConflict: boolean;
  conflicts: HostConflict[];
}

export interface HostConflict {
  visitRequestId: number;
  visitInstanceId: number;
  delegationName: string | null;
  startTime: string;
  endTime: string;
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
export type InvitationAction = 'VIEW_DETAIL' | 'ACCEPT_INVITATION' | 'DECLINE_INVITATION' | 'ASSIGN_TO_DEPARTMENT_STAFF';

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
  visitScopes?: string[];
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
