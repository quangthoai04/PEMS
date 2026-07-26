import httpClient from '../../../shared/api/httpClient';

// ──────────────────────────────────────────────────────────────────────────────
// Per-campus form v2 API (feature-flagged server-side: every endpoint 404s while
// the PerCampusFormV2Write flag is OFF). The backend derives visitScope /
// hasMixedCampusDetails / fingerprints — the client NEVER sends them.
// ──────────────────────────────────────────────────────────────────────────────

export interface V2VisitorDto {
  fullName: string;
  nationality: string;
  jobTitle: string;
  organization: string;
}

export interface V2SupportMemberDto {
  fullName: string;
  jobTitle: string;
  organization: string;
  nationality: string;
}

export interface V2ContactPointDto {
  fullName: string;
  organization: string;
  phone: string;
  email: string;
}

/** One fully-resolved campus snapshot — "same for all campuses" is a one-time UI copy, never inheritance. */
export interface V2CampusVisitForm {
  campusId: string; // campus CODE (e.g. "HN")
  plannedStartAt: string;
  plannedEndAt: string;
  delegationName: string;
  visitType: string;
  visitTypeOther?: string | null;
  purpose: string;
  workingContent?: string | null;
  visitors: V2VisitorDto[];
  externalSupportMembers: V2SupportMemberDto[];
  operationalContact: V2ContactPointDto;
  workingLanguage: string;
  transportationNote?: string | null;
  mediaConsentStatus: string;
  mediaConsentNote?: string | null;
  notes?: string | null;
  processing?: { mode: string; hostUserId?: number | null } | null;
}

export interface V2CreatePayload {
  submissionId: string;
  registrant: {
    fullName: string;
    nationality: string;
    organization: string;
    jobTitle: string;
    phone: string;
    email: string;
  };
  primaryContact: V2ContactPointDto;
  partnerId?: number | null;
  campusVisits: V2CampusVisitForm[];
}

export interface V2CampusRef {
  visitInstanceId: number;
  campusId: number;
  status: string;
}

export interface V2CreateResponse {
  visitRequestId: number;
  requestCode: string;
  visitScope: string;
  hasMixedCampusDetails: boolean;
  primaryContactAccessStatus: string; // ACTIVE | PENDING_CONFIRMATION
  contactClaimPending: boolean;
  instances: V2CampusRef[];
  idempotent: boolean;
  /** Request status straight from the committed row — never inferred client-side. */
  status: string;
  /** Vietnam wall-clock "yyyy-MM-ddTHH:mm:ss" (no offset). */
  submittedAt: string;
  campusCount: number;
  /**
   * Set only when the receipt was rebuilt from the submission LOOKUP after an uncertain result.
   * The lookup answers an anonymous caller, so it carries no campus list — the UI uses this to
   * show what it actually knows instead of an empty per-campus summary.
   */
  recoveredByLookup?: boolean;
}

export const createVisitRequestV2 = (payload: V2CreatePayload) =>
  httpClient.post<V2CreateResponse>('/v2/visit-requests', payload).then(r => r.data);

/** Public v2 OTP initiate (step 1). Validates the v2 form, mints the OTP and BINDS the
 * snapshot server-side so verify builds from exactly this form — the client's verify-time
 * form can no longer change campus/member/contact/time/content. Body is nested `{ form }`. */
export interface V2InitiateResponse {
  sessionToken: string;
  message: string;
  maskedEmail: string;
  expiresAt: string;
  resendAfterSeconds: number;
  maxAttempts: number;
}

export const initiateVisitRequestV2 = (payload: V2CreatePayload) =>
  httpClient
    .post<V2InitiateResponse>('/v2/visit-requests/initiate', { form: payload })
    .then(r => r.data);

/**
 * Public OTP sibling of create-v2 (step 2 of the public flow). The backend command binds
 * `{ form, otpCode, sessionToken }` — the form payload is NESTED, not flattened.
 */
export const verifyAndCreateVisitRequestV2 = (
  payload: V2CreatePayload,
  otpCode: string,
  sessionToken: string,
) => httpClient
  .post<V2CreateResponse>('/v2/visit-requests/verify', { form: payload, otpCode, sessionToken })
  .then(r => r.data);

/**
 * "Did my submission go through?" — the answer to a verify whose RESPONSE was lost.
 *
 * Keyed on the client-minted submissionId, never on email: one person legitimately files several
 * requests, so "the newest one for this address" answers a different question. Read-only on the
 * server, so it is safe to call while deciding whether to retry.
 */
export type VisitSubmissionState = 'COMPLETED' | 'PENDING' | 'FAILED' | 'NOT_FOUND';

export interface VisitSubmissionLookup {
  state: VisitSubmissionState;
  visitRequestId: number | null;
  requestCode: string | null;
  status: string | null;
  submittedAt: string | null;
  campusCount: number | null;
}

export const getVisitSubmissionResult = (submissionId: string) =>
  httpClient
    .get<VisitSubmissionLookup>(`/v2/visit-requests/submissions/${encodeURIComponent(submissionId)}`)
    .then(r => r.data);

// ── Central v2 read model (GET /v2/visit-requests/{id}) ──────────────────────
// The backend resolves v1 (projection dual-read) and v2 (per-campus details) into ONE
// shape and returns ONLY the campus instances the caller may see — hidden campuses never
// appear and never influence counts. The client renders this payload verbatim.

export interface ResolvedMember {
  guestMemberId: number;
  memberType: string;
  fullName: string;
  organization: string;
  jobTitle: string;
  nationality: string;
  displayOrder: number;
}

export interface ResolvedCampusVisit {
  visitInstanceId: number;
  campusId: number;
  campusCode: string;
  campusName: string;
  plannedStartAt: string;
  plannedEndAt: string;
  timezone: string;
  instanceStatus: string;
  currentHostUserId: number | null;
  currentHostName: string | null;
  decidedByUserId: number | null;
  decidedByName: string | null;
  decidedAt: string | null;
  decisionActorRole: string | null;
  decisionNote: string | null;
  /** Per-campus cancellation (UC-136) — a campus can be cancelled without the whole request being. */
  cancelledByUserId: number | null;
  cancelledByName: string | null;
  cancelledAt: string | null;
  cancellationActorType: string | null;
  cancellationSource: string | null;
  cancellationReason: string | null;
  delegationName: string;
  visitType: string;
  visitTypeOther: string | null;
  purpose: string;
  workingContent: string | null;
  visitors: ResolvedMember[];
  supportMembers: ResolvedMember[];
  operationalContact: V2ContactPointDto;
  workingLanguage: string;
  transportationNote: string | null;
  mediaConsentStatus: string;
  mediaConsentNote: string | null;
  noteToFptu: string | null;
  formRevision: number;
  approvalRevision: number;
  rowVersion: number;
  activeAmendment: {
    amendmentId: number;
    amendmentNo: number;
    status: string;
    requestedAt: string;
    changedFieldCount: number;
  } | null;
  /** Backend-derived mutation actions for THIS instance (SUBMIT_AMENDMENT / APPROVE_AMENDMENT /
   * REJECT_AMENDMENT / WITHDRAW_AMENDMENT). The UI gates per-instance actions on this list.
   * Optional so older cached payloads (no field) fail safe to "no actions". */
  allowedActions?: string[];
}

export interface ResolvedVisitForm {
  visitRequestId: number;
  requestCode: string;
  /** Request-level optimistic-concurrency token — echoed back as expectedRequestRowVersion on edit/resubmit. */
  rowVersion: number;
  hasMixedCampusDetails: boolean;
  visitScope: string;
  requestStatus: string;
  createdSource: string;
  submittedAt: string;
  partnerId: number | null;
  /** Request-level cancellation (UC-136) — set only when the whole request was cancelled. */
  cancelledByUserId: number | null;
  cancelledByName: string | null;
  cancelledAt: string | null;
  cancellationReason: string | null;
  registrant: {
    fullName: string;
    organization: string;
    jobTitle: string;
    phone: string;
    email: string;
    nationality: string;
  };
  primaryContact: {
    fullName: string;
    organization: string;
    phone: string;
    email: string;
    accessStatus: string; // PENDING_CONFIRMATION | ACTIVE
    verifiedAt: string | null;
  };
  campusVisits: ResolvedCampusVisit[];
  viewer: {
    relation: string; // HOST | STAFF_LEADER | HO | VISITOR_OWNER | REGISTRANT | IC_SUPPORT | DEPT_SUPPORT | STUDENT | NONE
    canViewAllCampuses: boolean;
    isReadOnly: boolean;
    allowedActions: string[];
  };
}

export const getVisitRequestFormV2 = (visitRequestId: number) =>
  httpClient.get<ResolvedVisitForm>(`/v2/visit-requests/${visitRequestId}`).then(r => r.data);

// ── Pending edit / resubmit (stable visitInstanceId + row versions) ───────────

export interface V2CampusVisitEdit extends V2CampusVisitForm {
  /** null = add this campus; set = edit the existing instance (kept stable). */
  visitInstanceId?: number | null;
  expectedRowVersion?: number | null;
}

/** Mirrors backend `VisitRequestEditV2Dto` — the edit payload carries the request-level
 * snapshot too (registrant/primaryContact/partnerId), not just the campus list. */
export interface V2EditPayload {
  expectedRequestRowVersion: number;
  registrant: V2CreatePayload['registrant'];
  primaryContact: V2ContactPointDto;
  partnerId?: number | null;
  campusVisits: V2CampusVisitEdit[];
}

export interface V2EditResponse {
  visitRequestId: number;
  status: string;
  visitScope: string;
  hasMixedCampusDetails: boolean;
  requestRowVersion: number;
  instances: V2CampusRef[];
  message: string;
}

export const updatePendingVisitRequestV2 = (visitRequestId: number, edit: V2EditPayload) =>
  httpClient.put<V2EditResponse>(`/v2/visit-requests/${visitRequestId}/pending-edit`, edit).then(r => r.data);

export const resubmitVisitRequestV2 = (visitRequestId: number, edit: V2EditPayload) =>
  httpClient.post<V2EditResponse>(`/v2/visit-requests/${visitRequestId}/resubmit`, edit).then(r => r.data);

// ── Identity: INITIAL_CLAIM (72h) ────────────────────────────────────────────

export interface ContactClaimInfo {
  status: string; // PENDING | APPLIED | DECLINED | EXPIRED | CANCELLED | SUPERSEDED | INVALID
  actionable: boolean;
  maskedEmail: string | null;
  delegationName: string | null;
  requestCode: string | null;
  registrantFullName: string | null;
  expiresAt: string | null;
  requiresGoogleLoginEmailMatch: boolean;
}

export interface ContactClaimActionResponse {
  visitRequestId: number;
  requestCode: string;
  claimStatus: string;
  primaryContactAccessStatus: string;
  message: string;
}

export const getContactClaimInfo = (token: string) =>
  httpClient.get<ContactClaimInfo>(`/public/visit-contact-claims/${encodeURIComponent(token)}`).then(r => r.data);

export const acceptContactClaim = (token: string) =>
  httpClient.post<ContactClaimActionResponse>(`/v2/visit-contact-claims/${encodeURIComponent(token)}/accept`).then(r => r.data);

export const declineContactClaim = (token: string, reason?: string) =>
  httpClient.post<ContactClaimActionResponse>(`/v2/visit-contact-claims/${encodeURIComponent(token)}/decline`, { reason }).then(r => r.data);

export interface ContactClaimManageResponse {
  visitRequestId: number;
  primaryContactAccessStatus: string;
  claimStatus: string | null;
  resendCount: number;
  message: string;
}

export const resendContactClaim = (visitRequestId: number) =>
  httpClient.post<ContactClaimManageResponse>(`/v2/visit-requests/${visitRequestId}/contact-claim/resend`).then(r => r.data);

export const replacePendingContact = (
  visitRequestId: number,
  body: { fullName: string; organization: string; phone: string; email: string },
) => httpClient.put<ContactClaimManageResponse>(`/v2/visit-requests/${visitRequestId}/contact-claim`, body).then(r => r.data);

// ── Identity: TRANSFER (24h) — old owner keeps rights until explicit accept ──

export interface ContactTransferInfo {
  status: string;
  actionable: boolean;
  maskedEmail: string | null;
  delegationName: string | null;
  requestCode: string | null;
  requestedByName: string | null;
  expiresAt: string | null;
  requiresGoogleLoginEmailMatch: boolean;
}

export interface ContactTransferState {
  visitRequestId: number;
  hasPendingTransfer: boolean;
  identityChangeId: number | null;
  status: string | null;
  newEmailMasked: string | null;
  expiresAt: string | null;
  resendCount: number;
}

export interface ContactTransferManageResponse {
  visitRequestId: number;
  transferStatus: string | null;
  newEmailMasked: string | null;
  expiresAt: string | null;
  resendCount: number;
  message: string;
}

export interface ContactTransferActionResponse {
  visitRequestId: number;
  requestCode: string;
  transferStatus: string;
  primaryContactAccessStatus: string;
  idempotent: boolean;
  message: string;
}

export const getContactTransferInfo = (token: string) =>
  httpClient.get<ContactTransferInfo>(`/public/visit-contact-transfers/${encodeURIComponent(token)}`).then(r => r.data);

export const acceptContactTransfer = (token: string) =>
  httpClient.post<ContactTransferActionResponse>(`/v2/visit-contact-transfers/${encodeURIComponent(token)}/accept`).then(r => r.data);

export const declineContactTransfer = (token: string, reason?: string) =>
  httpClient.post<ContactTransferActionResponse>(`/v2/visit-contact-transfers/${encodeURIComponent(token)}/decline`, { reason }).then(r => r.data);

export const initiateContactTransfer = (
  visitRequestId: number,
  body: { fullName: string; organization: string; phone: string; email: string; reason?: string },
) => httpClient.post<ContactTransferManageResponse>(`/v2/visit-requests/${visitRequestId}/contact-transfer`, body).then(r => r.data);

export const getActiveContactTransfer = (visitRequestId: number) =>
  httpClient.get<ContactTransferState>(`/v2/visit-requests/${visitRequestId}/contact-transfer`).then(r => r.data);

export const resendContactTransfer = (visitRequestId: number) =>
  httpClient.post<ContactTransferManageResponse>(`/v2/visit-requests/${visitRequestId}/contact-transfer/resend`).then(r => r.data);

export const cancelContactTransfer = (visitRequestId: number, reason?: string) =>
  httpClient.post<ContactTransferManageResponse>(`/v2/visit-requests/${visitRequestId}/contact-transfer/cancel`, { reason }).then(r => r.data);

// ── Safe edit (apply-now fields; backend classifier is authoritative) ────────

export interface SafeEditPayload {
  expectedRequestRowVersion: number;
  registrant?: { fullName: string; organization?: string | null; jobTitle?: string | null; phone?: string | null } | null;
  contact?: { fullName: string; organization?: string | null; phone: string } | null;
  instances?: Array<{
    visitInstanceId: number;
    expectedRowVersion: number;
    transportationNote?: string | null;
    noteToFptu?: string | null;
    mediaConsentStatus: string; // AGREED | DECLINED (→ DECLINED applies even <24h)
    mediaConsentNote?: string | null;
  }> | null;
}

export interface SafeEditResponse {
  visitRequestId: number;
  appliedChanges: Array<{ fieldPath: string; visitInstanceId: number | null; changeClass: string }>;
  requestRowVersion: number;
  instanceRowVersions: Record<number, number>;
  message: string;
}

export const patchSafeDetails = (visitRequestId: number, patch: SafeEditPayload) =>
  httpClient.patch<SafeEditResponse>(`/v2/visit-requests/${visitRequestId}/safe-details`, patch).then(r => r.data);

// ── Amendments (per decided campus; active snapshot never moves before approval) ─

export interface AmendmentProposalPayload {
  expectedInstanceRowVersion: number;
  baseFormRevision: number;
  baseApprovalRevision: number;
  reason?: string | null;
  delegationName: string;
  visitType: string;
  visitTypeOther?: string | null;
  purpose: string;
  workingContent?: string | null;
  workingLanguage: string;
  operationalContact: V2ContactPointDto;
  visitors: V2VisitorDto[];
  externalSupportMembers: V2SupportMemberDto[];
  plannedStartAt: string;
  plannedEndAt: string;
}

export interface AmendmentChange {
  fieldPath: string;
  changeClass: string;
  oldValueJson: string | null;
  newValueJson: string | null;
}

export interface AmendmentDto {
  amendmentId: number;
  visitRequestId: number;
  visitInstanceId: number;
  amendmentNo: number;
  status: string;
  baseFormRevision: number;
  baseApprovalRevision: number;
  requestedBy: number;
  requestedByName: string | null;
  requestedAt: string;
  reason: string | null;
  decidedBy: number | null;
  decidedByName: string | null;
  decidedAt: string | null;
  decisionNote: string | null;
  expiresAt: string | null;
  changes: AmendmentChange[];
}

export interface AmendmentDecisionResponse {
  amendmentId: number;
  visitInstanceId: number;
  status: string;
  newFormRevision: number | null;
  newApprovalRevision: number | null;
  message: string;
}

export const submitAmendment = (visitRequestId: number, visitInstanceId: number, proposal: AmendmentProposalPayload) =>
  httpClient.post<AmendmentDto>(`/v2/visit-requests/${visitRequestId}/instances/${visitInstanceId}/amendments`, proposal).then(r => r.data);

export const getActiveAmendment = (visitRequestId: number, visitInstanceId: number) =>
  httpClient.get<AmendmentDto | null>(`/v2/visit-requests/${visitRequestId}/instances/${visitInstanceId}/amendments/active`).then(r => r.data);

export const withdrawAmendment = (visitRequestId: number, visitInstanceId: number, amendmentId: number) =>
  httpClient.post<AmendmentDecisionResponse>(`/v2/visit-requests/${visitRequestId}/instances/${visitInstanceId}/amendments/${amendmentId}/withdraw`).then(r => r.data);

export const approveAmendment = (visitInstanceId: number, amendmentId: number, note?: string) =>
  httpClient.post<AmendmentDecisionResponse>(`/v2/visit-instances/${visitInstanceId}/amendments/${amendmentId}/approve`, { note }).then(r => r.data);

export const rejectAmendment = (visitInstanceId: number, amendmentId: number, note: string) =>
  httpClient.post<AmendmentDecisionResponse>(`/v2/visit-instances/${visitInstanceId}/amendments/${amendmentId}/reject`, { note }).then(r => r.data);

// ── Scoped, masked history timeline ──────────────────────────────────────────

/**
 * A STRUCTURED timeline entry: the backend states what happened, the client decides how to word it.
 * The old shape carried pre-assembled Vietnamese titles with audit fragments glued on
 * ("source=CREATE;approvalRevision=1"), which could not be translated and leaked internal enum names.
 */
export interface VisitHistoryEntry {
  at: string;
  eventCode: string;
  visitInstanceId: number | null;
  campusName: string | null;
  actorName: string | null;
  formRevision: number | null;
  approvalRevision: number | null;
  amendmentNo: number | null;
  statusCode: string | null;
  sourceType: string | null;
  reason: string | null;
  maskedEmail: string | null;
  fromStatus: string | null;
  toStatus: string | null;
}

export interface VisitRequestHistory {
  visitRequestId: number;
  requestCode: string;
  entries: VisitHistoryEntry[];
}

export const getVisitRequestHistory = (visitRequestId: number) =>
  httpClient.get<VisitRequestHistory>(`/v2/visit-requests/${visitRequestId}/history`).then(r => r.data);
