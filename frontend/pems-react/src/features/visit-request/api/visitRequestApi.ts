import httpClient from '../../../shared/api/httpClient';
import { API_ENDPOINTS } from '../../../shared/api/endpoints';

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



export interface PublicPartnerOptionDto {
  partnerId: number;
  name: string;
  shortName: string | null;
  country: string | null;
  city: string | null;
  partnerType: string;
  displayName: string;
}



// ── Authenticated create (Visitor / IC Staff / Staff Leader) ────────────────

/** Per-campus processing mode for the authenticated create (backend revalidates all). */
export interface CampusProcessingChoice {
  campusId: string; // campus CODE ("HN", "HCM", ...)
  mode: 'SEND_FOR_REVIEW' | 'SELF_HOST' | 'ASSIGN_HOST';
  hostUserId?: number | null;
}

export interface AuthenticatedCreateResponse {
  visitRequestId: number;
  requestCode: string;
  status: string;
  message: string;
  hasHostingConflictWarning: boolean;
}

export interface CreateHostCandidate {
  userId: number;
  fullName: string;
  email: string | null;
  campusId: number | null;
  departmentName: string | null;
  subRole: string | null;
  roleLabel: string | null;
  isSelf: boolean;
  isStaffLeaderSelfHostOption: boolean;
  hasScheduleConflict: boolean;
  conflictCount: number;
}

/** UC-86 §10 — one selectable campus on the registration form (GET /campuses/available-for-registration). */
export interface RegistrationCampusOption {
  campusId: number;
  campusCode: string;
  campusName: string;
  city: string | null;
}

export const visitRequestApi = {
  /**
   * Campus options for the visit form. Anonymous; the backend returns ONLY campuses that are
   * operationally available (ACTIVE + active IC department + valid Staff Leader) and rechecks
   * on submit — the dropdown is never the security boundary.
   */
  async getRegistrationCampuses(): Promise<RegistrationCampusOption[]> {
    const { data } = await httpClient.get<RegistrationCampusOption[]>(
      API_ENDPOINTS.campuses.availableForRegistration,
    );
    return data;
  },



  /** Staff Leader only — own-campus host candidates for ASSIGN_HOST in the create form. */
  async getCreateHostCandidates(startAt?: string, endAt?: string): Promise<CreateHostCandidate[]> {
    const { data } = await httpClient.get<CreateHostCandidate[]>(
      API_ENDPOINTS.visitRequests.createHostCandidates,
      { params: { startAt: startAt || undefined, endAt: endAt || undefined } }
    );
    return data;
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



  async searchOrganizations(query: string): Promise<PublicPartnerOptionDto[]> {
    const { data } = await httpClient.get<PublicPartnerOptionDto[]>(
      API_ENDPOINTS.publicPartners.search,
      { params: { keyword: query, limit: 20 } }
    );
    return data;
  },
};
