// ──────────────────────────────────────────────────────────────────────────────
// Typed action codes emitted by the v2 read model (backend authority). The detail
// view renders mutation UI ONLY when the matching code is present — never from role,
// relation, or status. Mirrors backend PEMS.Domain.Constants.VisitFormActions.
// ──────────────────────────────────────────────────────────────────────────────

export const VisitV2Action = {
  View: 'VIEW',
  // request level (viewer.allowedActions)
  EditPendingRequest: 'EDIT_PENDING_REQUEST',
  ResubmitRejectedRequest: 'RESUBMIT_REJECTED_REQUEST',
  SubmitSafeEdit: 'SUBMIT_SAFE_EDIT',
  // per instance (campusVisit.allowedActions)
  SubmitAmendment: 'SUBMIT_AMENDMENT',
  ApproveAmendment: 'APPROVE_AMENDMENT',
  RejectAmendment: 'REJECT_AMENDMENT',
  WithdrawAmendment: 'WITHDRAW_AMENDMENT',
} as const;

export type VisitV2ActionCode = (typeof VisitV2Action)[keyof typeof VisitV2Action];

/** True when the backend granted `action` in the given list. Undefined/empty → false (fail-safe). */
export const hasAction = (actions: string[] | undefined | null, action: VisitV2ActionCode): boolean =>
  Array.isArray(actions) && actions.includes(action);

// ── Amendment stable error codes (matched by code, never message text) ──────────
export const AmendmentErrorCode = {
  AlreadyPending: 'AMENDMENT_ALREADY_PENDING',
  NotEditable: 'AMENDMENT_NOT_EDITABLE',
  BaseRevisionConflict: 'AMENDMENT_BASE_REVISION_CONFLICT',
  ApproverScopeForbidden: 'AMENDMENT_APPROVER_SCOPE_FORBIDDEN',
  WindowExpired: 'AMENDMENT_WINDOW_EXPIRED',
  ConcurrencyConflict: 'CONCURRENCY_CONFLICT',
} as const;

export type AmendmentErrorCodeValue = (typeof AmendmentErrorCode)[keyof typeof AmendmentErrorCode];

/** Extract a stable backend errorCode from an axios-style error, if present. */
export const errorCodeOf = (error: unknown): string | null => {
  const data = (error as { response?: { data?: { errorCode?: unknown } } } | undefined)?.response?.data;
  return typeof data?.errorCode === 'string' ? data.errorCode : null;
};
