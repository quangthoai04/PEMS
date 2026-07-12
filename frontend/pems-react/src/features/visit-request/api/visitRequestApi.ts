import httpClient from '../../../shared/api/httpClient';
import { API_ENDPOINTS } from '../../../shared/api/endpoints';
import type { VisitRequestSchema } from '../schema/visitRequest.schema';

export interface InitiateResponse {
  /** Opaque random challenge token (NOT the email) — pass back to verify/resend/recover. */
  sessionToken: string;
  maskedEmail: string;
  message: string;
  expiresAt?: string;
  /** Server-decided resend cooldown (seconds) — presentation seed only. */
  resendAfterSeconds?: number;
  /** Server-decided max wrong attempts — presentation seed only. */
  maxAttempts?: number;
}

export interface VerifyResponse {
  visitRequestId: number;
  requestCode: string;
  status: string;
  message: string;
}

/** 409 DUPLICATE_VISIT_REQUEST payload (response.data.data) — a result, not an OTP error. */
export interface DuplicateVisitRequestData {
  existingVisitRequestId: number;
  existingRequestCode: string;
  existingStatus: string;
  existingSubmittedAt: string;
}

// ── Visitor edit / resubmit (SQL v10 resubmit_agenda_cancel24) ────────────────

export interface EditableCampusSlotDto {
  visitInstanceId: number;
  campusId: number;
  campusCode: string;
  campusName: string;
  plannedStartAt: string;
  plannedEndAt: string;
  instanceStatus: string;
}

export interface EditableGuestMemberDto {
  fullName: string;
  organization: string | null;
  jobTitle: string | null;
  nationality: string | null;
}

export interface PreviousCampusDecisionDto {
  visitInstanceId: number;
  campusId: number;
  campusName: string;
  decisionNote: string | null;
  decidedByName: string | null;
  decidedAt: string | null;
}

/** GET /visit-requests/{id}/edit-detail — form-shaped snapshot for prefilling edit/resubmit. */
export interface EditableVisitRequestDetail {
  visitRequestId: number;
  requestCode: string;
  requestStatus: string;
  visitScope: string;
  mode: 'EDIT' | 'RESUBMIT';
  isEditablePending: boolean;
  isResubmittable: boolean;

  registrantFullName: string;
  registrantNationality: string;
  registrantOrganization: string;
  registrantJobTitle: string;
  registrantPhone: string;
  registrantEmail: string;

  delegationName: string;
  visitType: string;
  visitTypeOther: string | null;
  purpose: string;
  workingContent: string | null;

  contactPersonFullName: string;
  contactPersonOrganization: string;
  contactPersonPhone: string;
  contactPersonEmail: string;

  workingLanguage: string;
  transportationNote: string | null;
  mediaConsentStatus: string;
  mediaConsentNote: string | null;
  partnerId: number | null;
  partnerName: string | null;
  partnerIsActive: boolean;
  partnerProfileStatus: string | null;
  noteToFptu: string | null;

  campusVisits: EditableCampusSlotDto[];
  visitors: EditableGuestMemberDto[];
  supportMembers: EditableGuestMemberDto[];

  resubmissionCount: number;
  lastResubmittedAt: string | null;
  previousDecisions: PreviousCampusDecisionDto[];
}

export interface UpdatePendingResponse {
  visitRequestId: number;
  requestStatus: string;
  message: string;
}

export interface ResubmitResponse {
  visitRequestId: number;
  requestStatus: string;
  resubmissionCount: number;
  message: string;
}

export interface PublicPartnerOptionDto {
  partnerId: number;
  name: string;
  shortName: string | null;
  country: string | null;
  city: string | null;
  partnerType: string;
  displayName: string;
}

function toVietnamIso(value: string) {
  if (!value) return '';
  // datetime-local may return "2026-06-28T09:00" or "2026-06-28T09:00:00"
  // We need to produce a valid ISO-8601 with timezone: "2026-06-28T09:00:00+07:00"
  // Strip any existing timezone suffix first, then normalize to always have seconds.
  const clean = value.replace(/([+-]\d{2}:\d{2}|Z)$/, '');
  const parts = clean.split('T');
  if (parts.length !== 2) return `${value}+07:00`;
  const timeParts = parts[1].split(':');
  const hh = timeParts[0] || '00';
  const mm = timeParts[1] || '00';
  const ss = timeParts[2]?.split('.')[0] || '00'; // strip ms if present
  return `${parts[0]}T${hh}:${mm}:${ss}+07:00`;
}

function mapToPayload(data: VisitRequestSchema) {
  return {
    registrantFullName: data.registerInfo.fullName,
    registrantNationality: data.registerInfo.nationality,
    registrantOrganization: data.registerInfo.organization,
    registrantPosition: data.registerInfo.jobTitle,
    registrantPhone: data.registerInfo.phone,
    registrantEmail: data.registerInfo.email,

    partnerId: data.partnerId || null,

    delegationName: data.delegationName,
    visitScope: data.visitMode === 'multiple' ? 'MULTI_CAMPUS' : 'SINGLE_CAMPUS',

    campusVisits: data.visits.map((v) => ({
      campusId: v.campus,
      startDatetime: toVietnamIso(v.startDatetime),
      endDatetime: toVietnamIso(v.endDatetime),
    })),

    purpose: data.purpose,
    workingContent: data.workingContent,

    visitType: data.visitType,
    visitTypeOther: data.visitType === 'OTHER' ? data.visitTypeOther : null,

    workingLanguage: data.workingLanguage,

    transportationNote: data.transportationNote?.trim() || null,

    mediaConsentStatus: data.mediaConsentStatus,
    mediaConsentNote: data.mediaConsentNote || null,

    visitors: data.visitors.map((v) => ({
      fullName: v.fullName,
      nationality: v.nationality,
      jobTitle: v.jobTitle,
      organization: v.organization,
    })),

    supportMembers: data.supportTeam.map((s) => ({
      fullName: s.fullName,
      jobTitle: s.jobTitle,
      organization: s.organization,
      nationality: s.nationality,
    })),

    contactPerson: {
      fullName: data.contactPoint.fullName,
      organization: data.contactPoint.organization,
      phone: data.contactPoint.phone,
      email: data.contactPoint.email,
    },

    isContactSelf:
      data.contactPoint.email.toLowerCase() === data.registerInfo.email.toLowerCase(),

    notes: data.notes || null,
  };
}

export const visitRequestApi = {
  async initiate(data: VisitRequestSchema, submissionId: string): Promise<InitiateResponse> {
    const { data: res } = await httpClient.post<InitiateResponse>(
      API_ENDPOINTS.visitRequests.initiate,
      { ...mapToPayload(data), submissionId }
    );
    return res;
  },

  // The draft stays in the browser and the full form is resubmitted here together with
  // the OTP code + the submission intent id + the opaque challenge session token.
  async verify(
    data: VisitRequestSchema,
    otpCode: string,
    submissionId: string,
    sessionToken: string
  ): Promise<VerifyResponse> {
    const { data: res } = await httpClient.post<VerifyResponse>(
      API_ENDPOINTS.visitRequests.verify,
      { ...mapToPayload(data), otpCode, submissionId, sessionToken }
    );
    return res;
  },

  /** Supersedes the old challenge — the response carries a NEW sessionToken to swap in. */
  async resendOtp(
    registrantEmail: string,
    registrantFullName: string,
    submissionId: string,
    sessionToken: string
  ): Promise<InitiateResponse> {
    const { data: res } = await httpClient.post<InitiateResponse>(
      API_ENDPOINTS.visitRequests.resendOtp,
      { registrantEmail, registrantFullName, submissionId, sessionToken }
    );
    return res;
  },

  /**
   * Human-verification recovery after the challenge was burned by wrong attempts.
   * On success the old challenge stays dead and a brand-new sessionToken is returned.
   */
  async recoverOtp(
    submissionId: string,
    sessionToken: string,
    humanVerificationToken: string,
    registrantFullName: string
  ): Promise<InitiateResponse> {
    const { data: res } = await httpClient.post<InitiateResponse>(
      API_ENDPOINTS.visitRequests.otpRecover,
      { submissionId, sessionToken, humanVerificationToken, registrantFullName }
    );
    return res;
  },

  // ── Visitor edit / resubmit (owner-only, không cần OTP) ──

  async getEditableDetail(visitRequestId: number | string): Promise<EditableVisitRequestDetail> {
    const { data } = await httpClient.get<EditableVisitRequestDetail>(
      API_ENDPOINTS.visitRequests.editDetail(visitRequestId)
    );
    return data;
  },

  async updatePending(visitRequestId: number | string, data: VisitRequestSchema): Promise<UpdatePendingResponse> {
    const { data: res } = await httpClient.put<UpdatePendingResponse>(
      API_ENDPOINTS.visitRequests.pendingEdit(visitRequestId),
      mapToPayload(data)
    );
    return res;
  },

  async resubmitRejected(visitRequestId: number | string, data: VisitRequestSchema): Promise<ResubmitResponse> {
    const { data: res } = await httpClient.post<ResubmitResponse>(
      API_ENDPOINTS.visitRequests.resubmit(visitRequestId),
      mapToPayload(data)
    );
    return res;
  },

  async searchOrganizations(query: string): Promise<PublicPartnerOptionDto[]> {
    const { data } = await httpClient.get<PublicPartnerOptionDto[]>(
      API_ENDPOINTS.publicPartners.search,
      { params: { keyword: query, limit: 20 } }
    );
    return data;
  },
};
