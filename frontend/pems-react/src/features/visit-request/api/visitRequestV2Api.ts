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
  /** Display snapshot of the organization — kept verbatim even if the partner is later renamed. */
  organization: string;
  /**
   * Which partner profile the organization was picked from, or null for free text. The identity the
   * whole partner chain runs on: without it the backend has only a name to go on, and the minutes
   * screen asks the user to link an organization they already chose (PART-01).
   */
  organizationPartnerId?: number | null;
  /**
   * The form's own identity for this row, so `operationalContactClientMemberKey` can name a person
   * who has no `guestMemberId` yet. The backend maps key → inserted id inside one transaction and
   * stores nothing (NP-03).
   */
  clientMemberKey?: string | null;
}

export interface V2SupportMemberDto {
  fullName: string;
  jobTitle: string;
  organization: string;
  organizationPartnerId?: number | null;
  nationality: string;
  /** @see V2VisitorDto.clientMemberKey — support staff may hold the contact role too. */
  clientMemberKey?: string | null;
}

export interface V2ContactPointDto {
  fullName: string;
  organization: string;
  phone: string;
  email: string;
  /** Optional — the detail screens show it, but nothing forces it to be filled in. */
  jobTitle?: string | null;
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
  /** "Ghi chú gửi FPTU" — one general remark per campus, independent of media consent. */
  notes?: string | null;
  /**
   * "Phương án người phụ trách tiếp đón" — an INTENTION, not an assignment. Omit it entirely for a
   * Visitor/external submit: the backend forces WAIT_FOR_LATER and REFUSES a payload that names
   * anybody, so sending one is a failed request rather than a silently ignored field.
   */
  hostSelection?: V2HostSelectionDto | null;
  /**
   * WHICH member the operational contact is — the `clientMemberKey` of one row of `visitors` or
   * `externalSupportMembers` (NP-03). `null`/absent when the contact is somebody outside the
   * delegation, which is normal.
   *
   * <p>A key rather than an array index, because an index names a position and a position stops
   * meaning the same person as soon as a row is added, removed or reordered. The backend treats it as
   * the only evidence of identity when present, and REFUSES a key that names nobody rather than
   * falling back to guessing — sending a stale one is a failed submit, so only send a key that is
   * still in this payload.</p>
   */
  operationalContactClientMemberKey?: string | null;
}

/** SELF | SELECTED | WAIT_FOR_LATER. */
export type V2HostSelectionMode = 'SELF' | 'SELECTED' | 'WAIT_FOR_LATER';

export interface V2HostSelectionDto {
  mode: V2HostSelectionMode;
  /** Required for SELECTED; resolved server-side for SELF; must be absent for WAIT_FOR_LATER. */
  proposedHostUserId?: number | null;
  confirmedHostConflict?: boolean;
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
  partnerId?: number | null;
  /** Every campus names its OWN operational contact — there is no request-level contact. */
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
  /** Campuses still waiting for their own operational contact to answer. 0 = the gate is open. */
  pendingContactConfirmations: number;
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
  /** Restores the CHOICE on an edit reload, not just the text it printed (PART-01). */
  organizationPartnerId?: number | null;
  jobTitle: string;
  nationality: string;
  displayOrder: number;
}

export interface ResolvedOperationalContact {
  fullName: string;
  organization: string;
  jobTitle: string;
  /** Genuinely nullable at runtime (VisitFormReadService.ResolveAsync passes the DB column through
   *  unnormalized) — unlike fullName/organization/jobTitle/email, which the backend coalesces to "".
   *  Phone is optional everywhere in this feature, so a null here is ordinary, not a data gap. */
  phone: string | null;
  email: string;
  /** PENDING | CONFIRMED | DECLINED | EXPIRED | TRANSFER_PENDING. */
  confirmationStatus: string;
  /** REGISTRANT_SELF_MATCH | EMAIL_CONFIRMATION | TRANSFER — null until confirmed. */
  confirmationSource: string | null;
  confirmedAt: string | null;
  /**
   * Which delegation member this contact IS — matches a `guestMemberId` in the campus's `visitors`
   * OR its `supportMembers` (NP-03). Null when they are not travelling with the delegation, or on a
   * request that predates the link. The edit form restores its picker from this.
   */
  guestMemberId?: number | null;
}

export interface ResolvedProposedHost {
  userId: number | null;
  fullName: string;
  organizationOrDepartment: string;
  selectionMode: V2HostSelectionMode;
  /** PENDING | ACTIVATED | NEEDS_RESELECTION. */
  proposalStatus: string | null;
  proposedAt: string | null;
}

export interface ResolvedCurrentHost {
  userId: number;
  fullName: string;
  email: string;
  phone: string;
  departmentName: string;
}

export interface ResolvedHostSelectionCapabilities {
  canProposeSelfAsHost: boolean;
  canProposeOtherHost: boolean;
  canWaitForLaterAssignment: boolean;
  canUpdateProposedHost: boolean;
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
  /**
   * "Đầu mối đoàn khách phối hợp tại cơ sở" — the guest-side coordinator of THIS campus. Never the
   * host and never the registrant; two campuses of one request routinely have two different people.
   * Fields can be empty while the invitation is outstanding — render the block anyway, because the
   * email and the status are exactly what the reader needs then.
   */
  operationalContact: ResolvedOperationalContact;
  /** "Người phụ trách tiếp đón" — the OFFICIAL host. null = nobody assigned yet. */
  currentHost: ResolvedCurrentHost | null;
  /** "Host dự kiến" — the intended host while the gate is shut, or a record of one that fell through. */
  proposedHost: ResolvedProposedHost | null;
  /** What the CALLER may do about this campus's host. Backend verdict; never re-derived from a role. */
  hostSelection: ResolvedHostSelectionCapabilities;
  workingLanguage: string;
  transportationNote: string | null;
  mediaConsentStatus: string;
  /** "Ghi chú gửi FPTU" — one general remark per campus, independent of media consent. */
  notes: string | null;
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
   * REJECT_AMENDMENT / WITHDRAW_AMENDMENT / TRANSFER_HOST). The UI gates per-instance actions on this
   * list. Optional so older cached payloads (no field) fail safe to "no actions". */
  allowedActions?: string[];
  /** Per-campus verdicts INCLUDING refused ones — lets the UI disable with a real reason. */
  capabilities?: VisitActionCapability[];
  /**
   * True when a change this viewer submits for THIS campus is approved in the same call, because they
   * are both the requester side and the campus's current Host.
   *
   * It changes a LABEL — "Cập nhật" instead of "Gửi đề xuất thay đổi" — and nothing else. The browser
   * still calls the same endpoint and the backend still writes the amendment and its decision. Never
   * infer this from a role: it used to be `user.roleCode === 'STAFF'` here, which was wrong for a staff
   * account that happened to be the registrant and hosted nothing.
   */
  amendmentSelfApproves?: boolean;
  /**
   * True when the viewer is the Staff Leader of THIS campus AND filed the request, so they may file a
   * start inside the 72-hour registration floor (after confirming) and may approve in the same call as
   * an edit. Both halves are required: leading the campus without having filed the request does not open
   * the edit at all, and filing the request while leading a DIFFERENT campus opens the edit but sets
   * this false — that viewer is a requester here. A hint for the UI only; the backend decides both again.
   *
   * It says nothing about approving or rejecting the campus normally. Those stay with the campus's
   * leader whoever filed the request, and come from the list screen's own actions.
   */
  canOverrideScheduleLeadTime?: boolean;
  /**
   * The backend's OWN verdict on whether "Lưu và duyệt" may be offered — a separate contract from
   * `canOverrideScheduleLeadTime`, even though both currently come from the same leader-registrant
   * pairing on the backend. The button must render from THIS field; `canOverrideScheduleLeadTime` is
   * the 72-hour floor's business alone and must not double as a save-and-approve proxy.
   */
  canSaveAndApprove?: boolean;
}

/**
 * One action, one verdict, straight from the backend policy. The refused entries are the point: a
 * hidden button leaves the user guessing whether the action exists at all, while a disabled one with
 * "hạn cuối 08:00 ngày 21/08" tells them what happened and when it happened.
 */
export interface VisitActionCapability {
  code: string;
  scope: 'REQUEST' | 'INSTANCE';
  visitInstanceId?: number | null;
  enabled: boolean;
  /** Stable code (VISIT_MUTATION_CUTOFF_REACHED / …). Match on this, never on the message. */
  disabledReasonCode?: string | null;
  disabledReason?: string | null;
  cutoffAt?: string | null;
  plannedStartAt?: string | null;
  campusName?: string | null;
  requiredLeadHours: number;
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
  /**
   * How far the request is through the confirmation gate, counted over the campuses this caller may
   * see. There is no request-level contact to report: each campus has its own, and the only
   * request-level fact about them is how many have answered.
   */
  confirmationSummary: {
    total: number;
    confirmed: number;
    pending: number;
    declined: number;
    expired: number;
    /** True while the whole request is held at the gate — no Staff Leader may act on it. */
    gateOpen: boolean;
  };
  /**
   * The verdict on the WHOLE request, counted by the backend over every campus. **null** whenever
   * the caller does not see every campus — and that null is load-bearing: the campusVisits below are
   * permission-scoped, so no request-level claim may be derived from them.
   */
  requestOutcome: {
    /** ALL_CANCELLED | ALL_REJECTED | ALL_WAITING | MIXED | IN_PROGRESS | NO_CAMPUS */
    code: string;
    total: number;
    accepted: number;
    inProgress: number;
    waiting: number;
    rejected: number;
    cancelled: number;
    closed: number;
  } | null;
  campusVisits: ResolvedCampusVisit[];
  viewer: {
    relation: string; // HOST | STAFF_LEADER | HO | VISITOR_OWNER | REGISTRANT | IC_SUPPORT | DEPT_SUPPORT | STUDENT | NONE
    canViewAllCampuses: boolean;
    isReadOnly: boolean;
    allowedActions: string[];
    /** Request-scoped verdicts INCLUDING refused ones. */
    capabilities?: VisitActionCapability[];
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
 * snapshot too (registrant/partnerId), not just the campus list. The reception-host arrangement
 * is NOT part of an edit: it has its own campus-scoped endpoint (updateProposedHost). */
export interface V2EditPayload {
  expectedRequestRowVersion: number;
  registrant: V2CreatePayload['registrant'];
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

// ── Per-campus pending edit ──────────────────────────────────────────────────

/** The Host a "Lưu và duyệt" names. Approving without one is not a thing the backend accepts. */
export interface V2ApproveAfterSave {
  hostUserId: number;
  decisionNote?: string | null;
}

export interface V2InstancePendingEditPayload {
  content: V2CampusVisitEdit;
  /**
   * The leader-registrant's explicit "yes, this schedule, with less than 72 hours' notice". Sent only
   * after the backend has ASKED for it (409 LEAD_TIME_OVERRIDE_CONFIRMATION_REQUIRED) and the user has
   * said yes — never pre-set, because the backend honours it for that actor alone and setting it
   * hopefully would only hide a refusal the user needs to see.
   */
  overrideLeadTimeConfirmed?: boolean;
  approveAfterSave?: V2ApproveAfterSave | null;
}

export interface V2InstancePendingEditResponse {
  visitRequestId: number;
  visitInstanceId: number;
  visitRequestStatus: string;
  visitInstanceStatus: string;
  instanceRowVersion: number;
  requestRowVersion: number;
  approved: boolean;
  hostUserId: number | null;
  message: string;
}

/**
 * Edits ONE campus that is still waiting for its decision, leaving every sibling untouched.
 *
 * Deliberately NOT the request-wide `/pending-edit`: that one needs EVERY campus still waiting, so on a
 * mixed request (one approved, one waiting, one refused) it disappears — and until this existed the
 * waiting campus had no way to be corrected at all.
 */
export const updatePendingVisitInstance = (
  visitRequestId: number,
  visitInstanceId: number,
  body: V2InstancePendingEditPayload,
) =>
  httpClient
    .put<V2InstancePendingEditResponse>(
      `/v2/visit-requests/${visitRequestId}/instances/${visitInstanceId}/pending-edit`,
      body,
    )
    .then(r => r.data);

// ── Per-campus operational contact: confirmation (72h) and transfer (24h) ────
//
// Every action names BOTH the request and the campus. There is no request-wide contact action:
// the old request-level workflow could hand one person authority over campuses they were never
// invited to, which is exactly the hole this cutover closes.

/** What an anonymous holder of an invitation link may see. Masked address, ONE campus, no form content. */
export interface OperationalContactInvitationInfo {
  /** PENDING | APPLIED | DECLINED | EXPIRED | CANCELLED | SUPERSEDED | INVALID. */
  status: string;
  actionable: boolean;
  /** INITIAL_CONFIRMATION | TRANSFER — the link itself knows which; the URL does not decide. */
  kind: string | null;
  maskedEmail: string | null;
  requestCode: string | null;
  campusName: string | null;
  delegationName: string | null;
  plannedStartAt: string | null;
  plannedEndAt: string | null;
  expiresAt: string | null;
  /**
   * Người nhận có BẮT BUỘC phải đăng nhập trước khi trả lời không. Nay là `false` cho lời mời thường:
   * token đã dùng-một-lần, gắn với đúng một hành động và đúng một email do NGƯỜI ĐĂNG KÝ chọn — bắt
   * khách bên ngoài tạo tài khoản Google chỉ để nói có/không chính là lý do lời mời không ai trả lời.
   * Đăng nhập vẫn dùng được, chỉ không còn là con đường duy nhất.
   */
  requiresGoogleLoginEmailMatch: boolean;
  /**
   * Liên kết này mang câu trả lời nào — ACCEPT hay DECLINE. Email có một liên kết cho mỗi câu trả
   * lời, nên trang chỉ hiện đúng hành động người dùng đã bấm, thay vì cả hai nút mà một cái token
   * của họ không thực hiện được.
   */
  intendedAction?: 'ACCEPT' | 'DECLINE' | null;
}

/** The outcome for the ONE campus that was answered. */
export interface OperationalContactActionResponse {
  visitRequestId: number;
  visitInstanceId: number;
  requestCode: string;
  kind: string;
  changeStatus: string;
  campusStatus: string;
  /** Included because answering the LAST outstanding campus is what opens the global gate. */
  requestStatus: string;
  idempotent: boolean;
  message: string;
}

/** Owner-side view of ONE campus's contact state. Masked address only — never read back in full. */
/**
 * How this visit describes the signed-in contact versus what their PEMS profile says.
 *
 * Sent ONLY to the account the campus's contact relation points at — the server returns null for
 * everyone else, so a registrant never sees an offer to tidy up somebody else's identity.
 * Only the two fields the account schema owns are compared: there is no organization or job-title
 * column on a user, and email is identity rather than profile.
 */
export interface OperationalContactProfileDifference {
  fullNameDiffers: boolean;
  phoneDiffers: boolean;
  accountFullName: string | null;
  accountPhone: string | null;
  snapshotFullName: string | null;
  snapshotPhone: string | null;
}

export interface OperationalContactState {
  visitRequestId: number;
  visitInstanceId: number;
  campusStatus: string;
  contactConfirmed: boolean;
  confirmedEmailMasked: string | null;
  confirmedAt: string | null;
  confirmationSource: string | null;
  pendingChangeKind: string | null;
  pendingChangeStatus: string | null;
  pendingEmailMasked: string | null;
  expiresAt: string | null;
  resendCount: number;
  tokenVersion: number;
  /**
   * Null/absent when there is nothing to reconcile, or when the viewer is not the contact themselves.
   * Optional so a response from an older server — which simply omits it — is still a valid state
   * rather than a parse error.
   */
  profileDifference?: OperationalContactProfileDifference | null;
}

export interface OperationalContactManageResponse extends OperationalContactState {
  requestStatus: string;
  message: string;
}

/**
 * The five contact fields as the user filled them in.
 *
 * `jobTitle` is required and was MISSING from this type, so every replace and every transfer left it
 * out of the body and the backend's own validator refused the call with "Chức vụ đầu mối vận hành
 * không được để trống" — a field the form never showed.
 */
export interface OperationalContactInput {
  fullName: string;
  organization?: string | null;
  jobTitle: string;
  phone: string;
  email: string;
}

export const getOperationalContactInvitationInfo = (token: string) =>
  httpClient
    .get<OperationalContactInvitationInfo>(
      `/public/operational-contact-confirmations/${encodeURIComponent(token)}`)
    .then(r => r.data);

export const acceptOperationalContactInvitation = (token: string) =>
  httpClient
    .post<OperationalContactActionResponse>(
      `/operational-contact-confirmations/${encodeURIComponent(token)}/accept`)
    .then(r => r.data);

/**
 * Trả lời lời mời KHÔNG cần đăng nhập, từ trang xác nhận mà nút trong email mở ra.
 *
 * POST, không bao giờ GET: liên kết trong email chỉ MỞ TRANG, còn hành động chỉ chạy khi người thật
 * bấm trên trang — nếu GET mà thay đổi dữ liệu thì Outlook/Gmail/Defender quét link sẽ "trả lời" lời
 * mời thay người nhận.
 *
 * Backend nhận diện người trả lời từ chính token (dùng-một-lần, gắn đúng một hành động, gắn đúng
 * email do người đăng ký chọn) rồi liên kết/khởi tạo tài khoản tương ứng khi chấp nhận.
 */
export const publicAcceptOperationalContactInvitation = (token: string) =>
  httpClient
    .post<OperationalContactActionResponse>(
      `/public/operational-contact-confirmations/${encodeURIComponent(token)}/accept`)
    .then(r => r.data);

/** Từ chối không cần đăng nhập. Không tạo tài khoản: từ chối một vai trò không phải lý do để có tài khoản. */
export const publicDeclineOperationalContactInvitation = (token: string, reason?: string) =>
  httpClient
    .post<OperationalContactActionResponse>(
      `/public/operational-contact-confirmations/${encodeURIComponent(token)}/decline`, { reason })
    .then(r => r.data);

export const declineOperationalContactInvitation = (token: string, reason?: string) =>
  httpClient
    .post<OperationalContactActionResponse>(
      `/operational-contact-confirmations/${encodeURIComponent(token)}/decline`, { reason })
    .then(r => r.data);

// ── "Lời mời đầu mối của tôi" — the signed-in invitee's own surface ──────────
//
// The same two answers, reached WITHOUT a link. Somebody who is already using PEMS and whose account
// address is the invited one should not have to go back to their inbox and find an email to say yes.
//
// It is NOT a widening of what a pending invitee may read: they hold no relation the system has
// granted until they accept, `VisitFormReadService` still refuses them the full request, and these
// three endpoints are the only ones this screen calls. What the list carries is the limited summary a
// person needs in order to decide — which request, which campus, when, who is asking, how long the
// invitation lasts. No form content, no sibling campus, no token.

/** One outstanding invitation addressed to the signed-in account. Mirrors `MyOperationalContactInvitationDto`. */
export interface MyOperationalContactInvitation {
  identityChangeId: number;
  visitRequestId: number;
  visitInstanceId: number;
  /** INITIAL_CONFIRMATION | TRANSFER — the invitation knows which; the screen never decides it. */
  kind: string;
  requestCode: string | null;
  campusName: string | null;
  delegationName: string | null;
  plannedStartAt: string | null;
  plannedEndAt: string | null;
  /** Who is asking — enough to recognise an unexpected invitation. Their address is NOT included. */
  registrantFullName: string | null;
  registrantOrganization: string | null;
  expiresAt: string;
}

/**
 * The invitations still ANSWERABLE by the signed-in account: PENDING, not overdue, on a campus and a
 * request that are both still live. The backend filters all of that, so this list can never offer an
 * action the accept would then refuse.
 */
export const getMyOperationalContactInvitations = () =>
  httpClient
    .get<MyOperationalContactInvitation[]>('/v2/me/operational-contact-invitations')
    .then(r => r.data);

/** Accept ONE of the signed-in invitee's own invitations, by id rather than by link. */
export const acceptMyOperationalContactInvitation = (identityChangeId: number) =>
  httpClient
    .post<OperationalContactActionResponse>(
      `/v2/me/operational-contact-invitations/${identityChangeId}/accept`)
    .then(r => r.data);

/** Decline ONE of the signed-in invitee's own invitations, by id rather than by link. */
export const declineMyOperationalContactInvitation = (identityChangeId: number, reason?: string) =>
  httpClient
    .post<OperationalContactActionResponse>(
      `/v2/me/operational-contact-invitations/${identityChangeId}/decline`, { reason })
    .then(r => r.data);

export interface ResubmitInstanceResponse {
  visitRequestId: number;
  visitInstanceId: number;
  visitRequestStatus: string;
  visitInstanceStatus: string;
  instanceRowVersion: number;
  message: string;
}

/**
 * Sends ONE rejected campus back for review.
 *
 * Deliberately NOT the request-wide `/resubmit`: that endpoint requires every campus of the request to
 * be rejected and resets all of them, which would drag an approved sibling — host, schedule and all —
 * back into review because a different campus said no.
 */
export const resubmitVisitInstance = (
  visitRequestId: number,
  visitInstanceId: number,
  content: unknown,
) =>
  httpClient
    .post<ResubmitInstanceResponse>(
      `/v2/visit-requests/${visitRequestId}/instances/${visitInstanceId}/resubmit`,
      content,
    )
    .then((r) => r.data);

/**
 * Copies the two approved fields onto the signed-in user's OWN account profile.
 *
 * The canonical self-service profile command: it takes no target user — the server resolves the caller
 * from the session — so this cannot express "update someone else". Deliberately not routed through any
 * operational-contact handler; a contact snapshot and an account profile are different things, and the
 * one endpoint that may write an account is the one that already validates account fields.
 */
export const syncOwnAccountProfile = (payload: { fullName?: string; phone?: string }) =>
  httpClient.post('/profiles/updateprofile', payload).then((r) => r.data);

export const getOperationalContactState = (visitRequestId: number, visitInstanceId: number) =>
  httpClient
    .get<OperationalContactState>(
      `/v2/visit-requests/${visitRequestId}/instances/${visitInstanceId}/operational-contact`)
    .then(r => r.data);

export const resendOperationalContactConfirmation = (visitRequestId: number, visitInstanceId: number) =>
  httpClient
    .post<OperationalContactManageResponse>(
      `/v2/visit-requests/${visitRequestId}/instances/${visitInstanceId}/operational-contact-confirmation/resend`)
    .then(r => r.data);

/**
 * Mở lời mời xác nhận MỚI cho đúng email cơ sở đang có, khi lời mời trước đã kết thúc mà không ai
 * trả lời (hủy / từ chối / hết hạn) nên không còn gì để "gửi lại".
 *
 * Khác `resendOperationalContactConfirmation`: cái đó phát lại token trên lời mời vẫn PENDING và
 * tính vào hạn mức gửi lại. Cái này tạo identity change mới với token và hạn hiệu lực mới. Backend
 * từ chối nếu cơ sở đã có đầu mối xác nhận, hoặc đang có lời mời chờ trả lời.
 */
export const reinviteOperationalContactConfirmation = (visitRequestId: number, visitInstanceId: number) =>
  httpClient
    .post<OperationalContactManageResponse>(
      `/v2/visit-requests/${visitRequestId}/instances/${visitInstanceId}/operational-contact-confirmation/reinvite`)
    .then(r => r.data);

/**
 * Saves ONE campus's operational contact. The SERVER decides what the save means by comparing the
 * submitted address with the stored one:
 *
 * - same address → the person's details are corrected. No invitation, no email, no change to who holds
 *   the campus, no effect on approval.
 * - different address → the canonical identity workflow. A replace while nobody holds the campus yet, a
 *   transfer once somebody already does — regardless of whether the campus has been decided — and in a
 *   transfer nothing moves until the invited person accepts.
 *
 * The client deliberately does NOT classify the edit. It cannot: only the stored address decides, and a
 * client that guessed wrong would either email somebody about a corrected phone number or change who
 * runs a campus without asking anyone.
 *
 * `reason` is used only if the save turns out to be a transfer; `expectedRowVersion` only if it turns
 * out to be a correction, where it stops a stale form overwriting newer data.
 */
export const saveOperationalContact = (
  visitRequestId: number,
  visitInstanceId: number,
  body: OperationalContactInput & { reason?: string; expectedRowVersion?: number },
) =>
  httpClient
    .put<OperationalContactManageResponse>(
      `/v2/visit-requests/${visitRequestId}/instances/${visitInstanceId}/operational-contact`, body)
    .then(r => r.data);

/** Hand a campus that already has a confirmed contact to a new address, before it has started —
 *  whether or not it has been decided. Nothing moves until that person accepts. */
export const initiateOperationalContactTransfer = (
  visitRequestId: number, visitInstanceId: number, body: OperationalContactInput & { reason?: string },
) =>
  httpClient
    .post<OperationalContactManageResponse>(
      `/v2/visit-requests/${visitRequestId}/instances/${visitInstanceId}/operational-contact/transfer`, body)
    .then(r => r.data);

/** Close an in-flight invitation without changing who holds the campus. */
export const cancelOperationalContactChange = (
  visitRequestId: number, visitInstanceId: number, reason?: string,
) =>
  httpClient
    .post<OperationalContactManageResponse>(
      `/v2/visit-requests/${visitRequestId}/instances/${visitInstanceId}/operational-contact/cancel`,
      { reason })
    .then(r => r.data);


// ── Safe edit (apply-now fields; backend classifier is authoritative) ────────

/**
 * A SPARSE patch: send only what actually changed. Omitting a field means "not part of this edit";
 * sending "" clears it. A campus that changed nothing must NOT appear in `instances` at all.
 *
 * This used to be a full snapshot of every safe field of every campus, which meant a one-word note
 * correction re-sent the media-consent decision of every other campus — dragging a campus whose
 * window had closed into the payload and having the whole edit refused because of it.
 */
export interface SafeEditPayload {
  expectedRequestRowVersion: number;
  registrant?: {
    fullName: string;
    nationality: string;
    organization?: string | null;
    jobTitle?: string | null;
    phone?: string | null;
    /** Partner profile the organization was picked from, or null for free text (mirrors V2VisitorDto). */
    partnerId: number | null;
  } | null;
  instances?: Array<{
    visitInstanceId: number;
    expectedRowVersion: number;
    transportationNote?: string | null;
    /** AGREED | DECLINED, or omitted when unchanged. DECLINED applies even inside the cutoff. */
    mediaConsentStatus?: string | null;
    notes?: string | null;
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

// ── Host transfer (post-approval handover of ONE campus) ─────────────────────

export interface HostTransferPayload {
  newHostUserId: number;
  reason: string;
  expectedRowVersion: number;
}

export interface HostTransferResponse {
  visitInstanceId: number;
  previousHostUserId: number;
  previousHostName: string;
  newHostUserId: number;
  newHostName: string;
  rowVersion: number;
  message: string;
}

/**
 * Hands one campus's Host role to a different eligible user. NOT the approve-and-assign endpoint:
 * that one gives a campus its first Host as part of the approval and refuses to run twice.
 */
export const transferVisitHost = (visitInstanceId: number, payload: HostTransferPayload) =>
  httpClient
    .post<HostTransferResponse>(`/v2/visit-instances/${visitInstanceId}/host-transfer`, payload)
    .then(r => r.data);

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
  /** @see V2CampusVisitForm.operationalContactClientMemberKey — same durable-reference convention (NP-03). */
  operationalContactClientMemberKey?: string | null;
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
  /** Handle for the detail drawer. Null when the event has nothing more to show than its own line. */
  eventId: string | null;
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

/** One field that moved. */
export interface VisitHistoryFieldChange {
  fieldCode: string;
  labelKey: string;
  beforeValue: string | null;
  afterValue: string | null;
  /**
   * True when there is no recorded previous value at all — not a previous value that was empty.
   * "(trống)" for both would state that the field used to be blank, which nobody knows.
   */
  beforeUnknown?: boolean;
}

/** Someone joined the delegation, left it, or had their details corrected. */
export interface VisitHistoryCollectionChange {
  collectionCode: 'VISITORS' | 'SUPPORT_MEMBERS';
  changeType: 'ADDED' | 'REMOVED' | 'UPDATED';
  itemKey: string | null;
  before: Record<string, string> | null;
  after: Record<string, string> | null;
}

export interface VisitHistoryDetail {
  eventId: string;
  eventCode: string;
  occurredAt: string;
  actorName: string | null;
  campusId: number | null;
  campusName: string | null;
  reason: string | null;
  beforeRevision: number | null;
  afterRevision: number | null;
  fieldChanges: VisitHistoryFieldChange[];
  collectionChanges: VisitHistoryCollectionChange[];
  /**
   * Vì sao danh sách thay đổi trông như vậy — do BACKEND nói, không phải client suy diễn:
   *
   * - `AVAILABLE`: tìm được cả hai phiên bản và đã so sánh. Rỗng nghĩa là thật sự không đổi gì.
   * - `PREVIOUS_REVISION_MISSING`: không có snapshot của phiên bản trước để so. Rỗng ở đây nghĩa là
   *   "không biết đã đổi gì", KHÁC hẳn "không đổi gì".
   * - `NOT_APPLICABLE`: sự kiện này không phải diff phiên bản (đổi đầu mối, bàn giao host...).
   */
  comparisonStatus?: 'AVAILABLE' | 'PREVIOUS_REVISION_MISSING' | 'NOT_APPLICABLE';
}

/**
 * Clears the caller's unread-change badge for this request. Fired when the DETAIL screen has
 * loaded — never on a list row appearing, which would spend the badge before it was read.
 */
export const markVisitChangesSeen = (visitRequestId: number) =>
  httpClient
    .post<{ markedCount: number }>(`/v2/visit-requests/${visitRequestId}/changes/seen`)
    .then(r => r.data);

export const getVisitHistoryDetail = (visitRequestId: number, eventId: string) =>
  httpClient
    .get<VisitHistoryDetail>(
      `/v2/visit-requests/${visitRequestId}/history/${encodeURIComponent(eventId)}`)
    .then(r => r.data);

export interface VisitRequestHistory {
  visitRequestId: number;
  requestCode: string;
  entries: VisitHistoryEntry[];
}

export const getVisitRequestHistory = (visitRequestId: number) =>
  httpClient.get<VisitRequestHistory>(`/v2/visit-requests/${visitRequestId}/history`).then(r => r.data);

// ── Reception host: proposal before the gate, assignment after it ─────────────

export interface UpdateProposedHostPayload {
  hostSelectionMode: V2HostSelectionMode;
  /** Required for SELECTED; ignored for SELF; omit for WAIT_FOR_LATER. */
  proposedHostUserId?: number | null;
  /** The campus instance's rowVersion as last read. Mismatch → 409, never a silent overwrite. */
  rowVersion: number;
}

export interface UpdateProposedHostResponse {
  visitRequestId: number;
  visitInstanceId: number;
  hostSelectionMode: V2HostSelectionMode;
  proposedHostUserId: number | null;
  proposedHostName: string | null;
  proposalStatus: string | null;
  rowVersion: number;
  message: string;
}

/**
 * Sets, changes or clears ONE campus's proposed reception host. Campus-scoped, and refused once the
 * campus is decided — after that the host moves through the handover flow, never through a proposal.
 */
export const updateProposedHost = (
  visitRequestId: number,
  visitInstanceId: number,
  payload: UpdateProposedHostPayload,
) =>
  httpClient
    .put<UpdateProposedHostResponse>(
      `/v2/visit-requests/${visitRequestId}/campuses/${visitInstanceId}/proposed-host`,
      payload,
    )
    .then(r => r.data);
