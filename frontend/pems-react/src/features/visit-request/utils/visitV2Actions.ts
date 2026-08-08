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
  // ── Operational-contact workflow. INSTANCE scope (campusVisit.allowedActions) ──
  //
  // The strings MUST be the ones PEMS.Domain.Constants.VisitFormActions emits. They were not: this
  // file spelled them RESEND_CONTACT_CLAIM / REPLACE_PENDING_CONTACT / INITIATE_CONTACT_TRANSFER /
  // CANCEL_CONTACT_TRANSFER while the backend sends RESEND_OPERATIONAL_CONTACT_CONFIRMATION /
  // REPLACE_OPERATIONAL_CONTACT / INITIATE_OPERATIONAL_CONTACT_TRANSFER /
  // CANCEL_OPERATIONAL_CONTACT_CHANGE. Nothing matched, `hasAnyAction` was false for every viewer, and
  // the contact panel on the detail screen rendered nothing at all — which is why managing a contact
  // appeared to have no home and drifted back into the request-edit form.
  //
  // There is no RESEND_CONTACT_TRANSFER: one resend code covers both kinds, because the invitation
  // itself knows which it is.
  /**
   * Send ONE rejected campus back for review. Instance scope, and NOT the same thing as
   * ResubmitRejectedRequest above — that one needs every campus rejected and revives the whole
   * request. The backend decides who gets this (registrant or the campus's own contact); the browser
   * only renders what it is told.
   */
  ResubmitRejectedInstance: 'RESUBMIT_REJECTED_INSTANCE',
  UpdateContactProfile: 'UPDATE_OPERATIONAL_CONTACT_PROFILE',
  ResendContactConfirmation: 'RESEND_OPERATIONAL_CONTACT_CONFIRMATION',
  ReplaceOperationalContact: 'REPLACE_OPERATIONAL_CONTACT',
  InitiateContactTransfer: 'INITIATE_OPERATIONAL_CONTACT_TRANSFER',
  CancelContactChange: 'CANCEL_OPERATIONAL_CONTACT_CHANGE',
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
  NoChanges: 'AMENDMENT_NO_CHANGES',
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
