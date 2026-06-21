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

/** How the current host was assigned (visit_request_campuses.host_assignment_source). */
export const HOST_ASSIGNMENT_SOURCE_LABELS: Record<string, string> = {
  AUTO_STAFF_LEADER: 'Tự gán Staff Leader',
  MANUAL_APPROVAL: 'Được giao khi duyệt',
  TRANSFERRED: 'Được chuyển host',
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
  hostAssignmentSource: string | null;
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
  expectedGuestCount: number | null;

  createdAt: string;
  submittedAt: string | null;
  cancelledAt: string | null;
  cancellationReason: string | null;
  decisionNote: string | null;

  allowedActions: AllowedAction[];
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
export type InvitationStatus = 'INVITED' | 'ACCEPTED' | 'DECLINED';

/** Actions on the invitation-detail screen (NOT on the "Đơn mời tham dự" tab). */
export type InvitationAction = 'VIEW_DETAIL' | 'ACCEPT_INVITATION' | 'DECLINE_INVITATION';

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
  requestStatus?: string;
  campusStatus?: string;
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
  statusLabel?: string;
  scopeLabel?: string;
  relationLabel?: string;
  statusOptions: VisitStatusFilterOption[];
  scopeOptions: VisitScopeFilterOption[];
  relationOptions: VisitRelationFilterOption[];
};
