/** Business Card OCR types — mirrors backend PEMS.Application.BusinessCardOcr DTOs. */

export type OcrJobStatus =
  | 'UPLOADED'
  | 'PROCESSING'
  | 'SUCCEEDED'
  | 'FAILED'
  | 'CONFIRMED'
  | 'DISCARDED';

export interface BusinessCardOcrParsed {
  fullName?: string | null;
  email?: string | null;
  phone?: string | null;
  jobTitle?: string | null;
  departmentName?: string | null;
  organization?: string | null;
  websiteUrl?: string | null;
  address?: string | null;
}

export interface BusinessCardOcrMatchedPartner {
  partnerId: number;
  partnerName: string;
  profileStatus: string;
  confidence: number;
  reason: string;
}

export interface BusinessCardOcrJob {
  ocrJobId: number;
  status: OcrJobStatus;
  providerName: string;
  confidenceScore?: number | null;
  scannedCardFileId: number;
  parsed?: BusinessCardOcrParsed | null;
  matchedPartner?: BusinessCardOcrMatchedPartner | null;
  confirmedPartnerId?: number | null;
  confirmedContactId?: number | null;
  sourceVisitInstanceId?: number | null;
  sourceGuestMemberId?: number | null;
  sourceMinuteParticipantId?: number | null;
  errorMessage?: string | null;
  createdAt: string;
  processedAt?: string | null;
}

export interface ConfirmBusinessCardContactRequest {
  partnerId: number;
  fullName: string;
  email?: string | null;
  phone?: string | null;
  jobTitle?: string | null;
  departmentName?: string | null;
  note?: string | null;
  isPrimary?: boolean;
  visitInstanceId?: number | null;
  guestMemberId?: number | null;
  minuteParticipantId?: number | null;
}

export interface ConfirmBusinessCardContactResponse {
  ocrJobId: number;
  status: OcrJobStatus;
  partnerId: number;
  contactId: number;
  visitGuestPartnerLinkId?: number | null;
}

export interface ScanBusinessCardContext {
  visitInstanceId?: number | null;
  guestMemberId?: number | null;
  minuteParticipantId?: number | null;
  partnerId?: number | null;
  /** Display name of the preselected partner (e.g. when opened from Partner Detail) — avoids
   *  showing a bare "#id" badge while the partner record itself isn't refetched here. */
  partnerName?: string | null;
}
