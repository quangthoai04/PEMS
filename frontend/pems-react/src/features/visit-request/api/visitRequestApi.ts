import httpClient from '../../../shared/api/httpClient';
import { API_ENDPOINTS } from '../../../shared/api/endpoints';
import type { VisitRequestSchema } from '../schema/visitRequest.schema';

export interface InitiateResponse {
  sessionToken: string;
  maskedEmail: string;
  message: string;
}

export interface VerifyResponse {
  visitRequestId: number;
  requestCode: string;
  status: string;
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

    transportationType: data.transportationType,
    transportationDetail: (data.transportationType === 'FPTU_SUPPORT' || data.transportationType === 'OTHER') ? data.transportationDetail : null,

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
  async initiate(data: VisitRequestSchema): Promise<InitiateResponse> {
    const { data: res } = await httpClient.post<InitiateResponse>(
      API_ENDPOINTS.visitRequests.initiate,
      mapToPayload(data)
    );
    return res;
  },

  // SQL v8.3 has no pending_visit_requests table: the draft stays in the browser and
  // the full form is resubmitted here together with the OTP code.
  async verify(data: VisitRequestSchema, otpCode: string): Promise<VerifyResponse> {
    const { data: res } = await httpClient.post<VerifyResponse>(
      API_ENDPOINTS.visitRequests.verify,
      { ...mapToPayload(data), otpCode }
    );
    return res;
  },

  async resendOtp(registrantEmail: string, registrantFullName: string): Promise<{ message: string }> {
    const { data: res } = await httpClient.post<{ message: string }>(
      API_ENDPOINTS.visitRequests.resendOtp,
      { registrantEmail, registrantFullName }
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
