/** Partner module types — mirrors backend PEMS.Application.Partners DTOs. */

export type PartnerProfileStatus = 'DRAFT' | 'PENDING_APPROVAL' | 'APPROVED' | 'REJECTED';
export type PartnerVisibility = 'PRIVATE' | 'INTERNAL' | 'PUBLIC';
export type PartnerType = 'UNIVERSITY' | 'COMPANY' | 'GOVERNMENT' | 'NGO' | 'OTHER';
export type PartnerAction = 'VIEW' | 'EDIT' | 'APPROVE' | 'REJECT' | 'MANAGE_CHILDREN';

export interface PartnerListItem {
  partnerId: number;
  partnerCode?: string | null;
  name: string;
  shortName?: string | null;
  country?: string | null;
  city?: string | null;
  partnerType: PartnerType;
  ownerCampusId: number;
  ownerCampusName: string;
  creatorName?: string | null;
  profileStatus: PartnerProfileStatus;
  visibility: PartnerVisibility;
  cooperationStatus: string;
  logoFileId?: number | null;
  createdAt: string;
  reviewedAt?: string | null;
  allowedActions: PartnerAction[];
}

export interface PartnerListResponse {
  items: PartnerListItem[];
  totalCount: number;
  page: number;
  pageSize: number;
  canCreate: boolean;
}

export interface PartnerListParams {
  search?: string;
  country?: string;
  /** Lọc đúng 1 đối tác — dùng khi đi từ thông báo sang trang quản lý. */
  partnerId?: number;
  campusId?: number;
  profileStatus?: string;
  visibility?: string;
  page?: number;
  pageSize?: number;
}

export interface PartnerDetail {
  partnerId: number;
  partnerCode?: string | null;
  name: string;
  shortName?: string | null;
  country?: string | null;
  city?: string | null;
  websiteUrl?: string | null;
  address?: string | null;
  description?: string | null;
  partnerType: PartnerType;
  cooperationStatus: string;
  profileStatus: PartnerProfileStatus;
  visibility: PartnerVisibility;
  logoFileId?: number | null;
  coverFileId?: number | null;
  publicSlug?: string | null;
  ownerCampusId: number;
  ownerCampusName: string;
  createdBy?: number | null;
  creatorName?: string | null;
  createdAt: string;
  reviewNote?: string | null;
  reviewedBy?: number | null;
  reviewerName?: string | null;
  reviewedAt?: string | null;
  allowedActions: PartnerAction[];

  englishName?: string | null;
  englishShortName?: string | null;
  englishDescription?: string | null;
  englishAddress?: string | null;
  hasEnglishTranslation?: boolean;
}

export interface TranslatePartnerDraftRequest {
  sourceLanguage?: string;
  targetLanguage?: string;
  name: string;
  shortName?: string | null;
  country?: string | null;
  city?: string | null;
  description?: string | null;
  address?: string | null;
}

export interface TranslatePartnerDraftResponse {
  success: boolean;
  message: string;
  sourceLanguage: string;
  targetLanguage: string;
  name: string;
  shortName?: string | null;
  country?: string | null;
  city?: string | null;
  description?: string | null;
  address?: string | null;
}

export interface CreatePartnerContactPayload {
  fullName: string;
  email?: string | null;
  phone?: string | null;
  jobTitle?: string | null;
  departmentName?: string | null;
  note?: string | null;
  avatarFileId?: number | null;
}

export interface CreatePartnerRequest {
  partnerCode?: string | null;
  name: string;
  shortName?: string | null;
  country?: string | null;
  city?: string | null;
  websiteUrl?: string | null;
  address?: string | null;
  description?: string | null;
  partnerType?: PartnerType | null;
  visibility?: PartnerVisibility | null;
  logoFileId?: number | null;
  coverFileId?: number | null;
  source?: 'MANUAL' | 'FROM_GUEST' | 'BUSINESS_CARD_OCR';
  initialContact?: CreatePartnerContactPayload | null;

  /**
   * English content, only sent once the admin has opened the "EN" panel (whether the text is the
   * untouched machine-translation preview or hand-edited afterward). Omitted when the EN panel
   * was never opened — the backend then auto-translates once and stores the result.
   */
  englishName?: string | null;
  englishShortName?: string | null;
  englishDescription?: string | null;
  englishAddress?: string | null;
}

export interface CreatePartnerResponse {
  partnerId: number;
  profileStatus: PartnerProfileStatus;
  ownerCampusId: number;
  initialContactId?: number | null;
  englishName?: string | null;
  englishShortName?: string | null;
  englishDescription?: string | null;
  englishAddress?: string | null;
}

export interface UpdatePartnerRequest {
  partnerCode?: string | null;
  name: string;
  shortName?: string | null;
  country?: string | null;
  city?: string | null;
  websiteUrl?: string | null;
  address?: string | null;
  description?: string | null;
  partnerType?: PartnerType | null;
  cooperationStatus?: string | null;
  visibility?: PartnerVisibility | null;
  logoFileId?: number | null;
  coverFileId?: number | null;

  /**
   * English content as currently shown in the EN panel. Omitted when the EN panel was never
   * opened during this edit — existing EN content (if any) is then left untouched; if no EN
   * translation exists yet either, the backend auto-translates once and stores it.
   */
  englishName?: string | null;
  englishShortName?: string | null;
  englishDescription?: string | null;
  englishAddress?: string | null;
}

export interface PartnerContact {
  contactId: number;
  partnerId: number;
  fullName: string;
  email?: string | null;
  phone?: string | null;
  jobTitle?: string | null;
  departmentName?: string | null;
  note?: string | null;
  sourceType: 'MANUAL' | 'BUSINESS_CARD_OCR' | 'IMPORT';
  scannedCardFileId?: number | null;
  avatarFileId?: number | null;
  avatarUrl?: string | null;
  ocrConfidence?: number | null;
  isPrimary: boolean;
  status: 'ACTIVE' | 'INACTIVE';
  createdAt: string;
}

export interface PartnerAlias {
  partnerAliasId: number;
  partnerId: number;
  aliasName: string;
  aliasNameKey: string;
  source: string;
  status: 'ACTIVE' | 'INACTIVE';
  createdAt: string;
}

export interface PartnerDocument {
  documentId: number;
  fileId: number;
  title: string;
  description?: string | null;
  documentCategory?: string | null;
  status: string;
  originalFilename?: string | null;
  mimeType?: string | null;
  fileSize?: number | null;
  downloadUrl?: string | null;
  createdAt: string;
  creatorName?: string | null;
}

export interface PartnerMatchCandidate {
  partnerId: number;
  name: string;
  shortName?: string | null;
  profileStatus: PartnerProfileStatus;
  visibility: PartnerVisibility;
  ownerCampusId: number;
  ownerCampusName?: string | null;
  country?: string | null;
  city?: string | null;
  /** 0–100 match score. */
  matchScore: number;
  matchReason?: string | null;
  /** Whether the current user may link to this partner (campus scope). */
  canLink: boolean;
}

export interface PartnerMatchResult {
  matchStatus: 'NONE' | 'SUGGESTED' | 'PENDING_APPROVAL' | 'APPROVED';
  partnerId?: number;
  partnerName?: string;
  profileStatus?: PartnerProfileStatus;
  confidence?: number;
  reason?: string;
  /** Top scored candidates (best first); empty when nothing matched. */
  candidates?: PartnerMatchCandidate[];
}

export interface VisitGuestPartnerLink {
  linkId: number;
  visitRequestId: number;
  visitInstanceId?: number | null;
  guestMemberId?: number | null;
  minuteParticipantId?: number | null;
  partnerId: number;
  partnerName: string;
  partnerProfileStatus: PartnerProfileStatus;
  partnerContactId?: number | null;
  partnerContactName?: string | null;
  matchSource: string;
  matchStatus: 'SUGGESTED' | 'CONFIRMED' | 'REJECTED';
  confidenceScore?: number | null;
  note?: string | null;
  createdAt: string;
}

/** UI labels (runtime values stay DB enums). */
export const PROFILE_STATUS_LABELS: Record<PartnerProfileStatus, string> = {
  DRAFT: 'Bản nháp',
  PENDING_APPROVAL: 'Chờ duyệt',
  APPROVED: 'Đã duyệt',
  REJECTED: 'Từ chối',
};

export const VISIBILITY_LABELS: Record<PartnerVisibility, string> = {
  PRIVATE: 'Riêng tư',
  INTERNAL: 'Nội bộ',
  PUBLIC: 'Công khai',
};

export const PARTNER_TYPE_LABELS: Record<PartnerType, string> = {
  UNIVERSITY: 'Trường đại học',
  COMPANY: 'Doanh nghiệp',
  GOVERNMENT: 'Cơ quan nhà nước',
  NGO: 'Tổ chức phi chính phủ',
  OTHER: 'Khác',
};
