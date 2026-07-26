// ──────────────────────────────────────────────────────────────────────────────
// Typed action codes emitted by the v2 read model (backend authority). The detail
// view renders mutation UI ONLY when the matching code is present — never from role,
// relation, or status. Mirrors backend PEMS.Domain.Constants.VisitFormActions.
// ──────────────────────────────────────────────────────────────────────────────
import type { VisitActionCapability } from '../api/visitRequestV2Api';

export const VisitV2Action = {
  View: 'VIEW',
  // request level (viewer.allowedActions)
  EditPendingRequest: 'EDIT_PENDING_REQUEST',
  ResubmitRejectedRequest: 'RESUBMIT_REJECTED_REQUEST',
  SubmitSafeEdit: 'SUBMIT_SAFE_EDIT',
  // primary-contact identity workflow (viewer.allowedActions). The panel used to decide these from
  // viewer.relation, which is why it could offer a resend past its cap or a transfer inside the 24h
  // window — decisions only the backend's own guards can make correctly.
  ResendContactClaim: 'RESEND_CONTACT_CLAIM',
  ReplacePendingContact: 'REPLACE_PENDING_CONTACT',
  InitiateContactTransfer: 'INITIATE_CONTACT_TRANSFER',
  ResendContactTransfer: 'RESEND_CONTACT_TRANSFER',
  CancelContactTransfer: 'CANCEL_CONTACT_TRANSFER',
  // per instance (campusVisit.allowedActions)
  SubmitAmendment: 'SUBMIT_AMENDMENT',
  ApproveAmendment: 'APPROVE_AMENDMENT',
  RejectAmendment: 'REJECT_AMENDMENT',
  WithdrawAmendment: 'WITHDRAW_AMENDMENT',
  TransferHost: 'TRANSFER_HOST',
} as const;

export type VisitV2ActionCode = (typeof VisitV2Action)[keyof typeof VisitV2Action];

/** True when the backend granted `action` in the given list. Undefined/empty → false (fail-safe). */
export const hasAction = (actions: string[] | undefined | null, action: VisitV2ActionCode): boolean =>
  Array.isArray(actions) && actions.includes(action);

// ── Structured capabilities ─────────────────────────────────────────────────
// `allowedActions` answers "may I?"; a capability additionally answers "why not, and until when".
// Both come from the same backend verdict, so they cannot disagree.

/** The capability for `action`, refused ones included, or undefined if the backend sent none. */
export const capabilityFor = (
  capabilities: VisitActionCapability[] | undefined | null,
  action: VisitV2ActionCode,
): VisitActionCapability | undefined =>
  Array.isArray(capabilities) ? capabilities.find(c => c.code === action) : undefined;

/**
 * Whether to render a REFUSED action as a disabled control with its reason, rather than hide it.
 *
 * Only for refusals the user can act on by waiting or by looking at the clock — a missed deadline.
 * A lifecycle refusal ("the visit already happened") or a relation refusal is not a near miss: the
 * action does not belong on this screen at all, and showing it greyed out implies it might come
 * back. Those stay hidden.
 */
export const shouldShowDisabled = (capability: VisitActionCapability | undefined): boolean =>
  capability?.enabled === false
  && capability.disabledReasonCode === VisitMutationErrorCode.CutoffReached;

export const VisitMutationErrorCode = {
  CutoffReached: 'VISIT_MUTATION_CUTOFF_REACHED',
  LifecycleNotAllowed: 'VISIT_MUTATION_LIFECYCLE_NOT_ALLOWED',
  RelationNotAllowed: 'VISIT_MUTATION_RELATION_NOT_ALLOWED',
} as const;

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
