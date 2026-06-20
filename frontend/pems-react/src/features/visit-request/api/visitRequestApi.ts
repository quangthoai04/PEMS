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

function mapToPayload(data: VisitRequestSchema) {
  return {
    registerFullName: data.registerInfo.fullName,
    registerNationality: data.registerInfo.nationality,
    registerOrganization: data.registerInfo.organization,
    registerJobTitle: data.registerInfo.jobTitle,
    registerPhone: data.registerInfo.phone,
    registerEmail: data.registerInfo.email,

    delegationName: data.delegationName,
    visitScope: data.visitMode === 'multiple' ? 'MULTI_CAMPUS' : 'SINGLE_CAMPUS',

    visitSlots: data.visits.map((v) => ({
      campusId: v.campus,
      startDatetime: v.startDatetime,
      endDatetime: v.endDatetime,
    })),

    purpose: data.purpose,
    workingContent: data.workingContent,

    visitors: data.visitors.map((v) => ({
      fullName: v.fullName,
      email: v.email,
      nationality: v.nationality,
      jobTitle: v.jobTitle || null,
      organization: v.organization || null,
    })),

    supportTeam: data.supportTeam.map((s) => ({
      fullName: s.fullName,
      jobTitle: s.jobTitle,
      organization: s.organization,
      nationality: s.nationality,
    })),

    contactPoint: {
      fullName: data.contactPoint.fullName,
      organization: data.contactPoint.organization,
      phone: data.contactPoint.phone,
      email: data.contactPoint.email,
    },

    isContactSelf:
      data.contactPoint.email.toLowerCase() === data.registerInfo.email.toLowerCase(),

    language: data.language === 'vietnamese' ? 'VI' : 'EN',
    vehicle: data.vehicle || null,
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

  async resendOtp(registerEmail: string, registerFullName: string): Promise<{ message: string }> {
    const { data: res } = await httpClient.post<{ message: string }>(
      API_ENDPOINTS.visitRequests.resendOtp,
      { registerEmail, registerFullName }
    );
    return res;
  },

  async searchOrganizations(query: string): Promise<{ id: string; name: string }[]> {
    const { data } = await httpClient.get<{ id: string; name: string }[]>(
      API_ENDPOINTS.partners.search,
      { params: { q: query, limit: 10 } }
    );
    return data;
  },
};
