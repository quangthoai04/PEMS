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
