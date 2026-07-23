import httpClient from '../../../shared/api/httpClient';
import { API_ENDPOINTS } from '../../../shared/api/endpoints';
import type {
  CreatePartnerContactPayload,
  CreatePartnerRequest,
  CreatePartnerResponse,
  PartnerAlias,
  PartnerContact,
  PartnerDetail,
  PartnerDocument,
  PartnerListParams,
  PartnerListResponse,
  PartnerMatchResult,
  TranslatePartnerDraftRequest,
  TranslatePartnerDraftResponse,
  UpdatePartnerRequest,
  VisitGuestPartnerLink,
} from '../types/partners.types';

function cleanParams(params: Record<string, unknown>): Record<string, unknown> {
  return Object.fromEntries(
    Object.entries(params).filter(([, v]) => v !== undefined && v !== null && v !== ''),
  );
}

/** Partner Management API — PartnersController + VisitPartnerLinksController. */
export const partnersApi = {
  async getPartners(params: PartnerListParams): Promise<PartnerListResponse> {
    const { data } = await httpClient.get<PartnerListResponse>(API_ENDPOINTS.partners.list, {
      params: cleanParams(params as Record<string, unknown>),
    });
    return data;
  },

  async getPendingApprovals(page = 1, pageSize = 10): Promise<PartnerListResponse> {
    const { data } = await httpClient.get<PartnerListResponse>(
      API_ENDPOINTS.partners.pendingApprovals,
      { params: { page, pageSize } },
    );
    return data;
  },

  async getPartnerDetail(partnerId: number | string): Promise<PartnerDetail> {
    const { data } = await httpClient.get<PartnerDetail>(API_ENDPOINTS.partners.detail(partnerId));
    return data;
  },

  async createPartner(payload: CreatePartnerRequest): Promise<CreatePartnerResponse> {
    const { data } = await httpClient.post<CreatePartnerResponse>(
      API_ENDPOINTS.partners.create,
      payload,
    );
    return data;
  },

  async updatePartner(partnerId: number | string, payload: UpdatePartnerRequest) {
    const { data } = await httpClient.put(API_ENDPOINTS.partners.update(partnerId), payload);
    return data as { partnerId: number; profileStatus: string };
  },

  async approvePartner(partnerId: number | string, reviewNote?: string, makePublic = false) {
    const { data } = await httpClient.post(API_ENDPOINTS.partners.approve(partnerId), {
      reviewNote: reviewNote || null,
      makePublic,
    });
    return data as { partnerId: number; profileStatus: string; visibility: string };
  },

  async rejectPartner(partnerId: number | string, reason: string) {
    const { data } = await httpClient.post(API_ENDPOINTS.partners.reject(partnerId), { reason });
    return data as { partnerId: number; profileStatus: string };
  },

  async matchPartner(organization?: string, email?: string): Promise<PartnerMatchResult> {
    const { data } = await httpClient.get<PartnerMatchResult>(API_ENDPOINTS.partners.match, {
      params: cleanParams({ organization, email }),
    });
    return data;
  },

  /** One-shot VI→EN preview for the "EN" button on the create/edit form; never persists. */
  async translateDraft(payload: TranslatePartnerDraftRequest): Promise<TranslatePartnerDraftResponse> {
    const { data } = await httpClient.post<TranslatePartnerDraftResponse>(
      '/partners/translate-draft',
      { sourceLanguage: 'vi', targetLanguage: 'en', ...payload },
    );
    return data;
  },

  // ── Contacts ──────────────────────────────────────────────────────────
  async getContacts(partnerId: number | string, includeInactive = false): Promise<PartnerContact[]> {
    const { data } = await httpClient.get<PartnerContact[]>(
      API_ENDPOINTS.partners.contacts(partnerId),
      { params: { includeInactive } },
    );
    return data;
  },

  async createContact(
    partnerId: number | string,
    payload: CreatePartnerContactPayload & { isPrimary?: boolean },
  ): Promise<PartnerContact> {
    const { data } = await httpClient.post<PartnerContact>(
      API_ENDPOINTS.partners.contacts(partnerId),
      payload,
    );
    return data;
  },

  async updateContact(
    partnerId: number | string,
    contactId: number | string,
    payload: CreatePartnerContactPayload,
  ): Promise<PartnerContact> {
    const { data } = await httpClient.put<PartnerContact>(
      API_ENDPOINTS.partners.contact(partnerId, contactId),
      payload,
    );
    return data;
  },

  async deactivateContact(partnerId: number | string, contactId: number | string) {
    const { data } = await httpClient.delete(API_ENDPOINTS.partners.contact(partnerId, contactId));
    return data as { contactId: number; status: string };
  },

  async setPrimaryContact(partnerId: number | string, contactId: number | string) {
    const { data } = await httpClient.post(
      API_ENDPOINTS.partners.contactSetPrimary(partnerId, contactId),
    );
    return data as { contactId: number; isPrimary: boolean };
  },

  // ── Aliases ───────────────────────────────────────────────────────────
  async getAliases(partnerId: number | string): Promise<PartnerAlias[]> {
    const { data } = await httpClient.get<PartnerAlias[]>(API_ENDPOINTS.partners.aliases(partnerId));
    return data;
  },

  async createAlias(partnerId: number | string, aliasName: string): Promise<PartnerAlias> {
    const { data } = await httpClient.post<PartnerAlias>(
      API_ENDPOINTS.partners.aliases(partnerId),
      { aliasName },
    );
    return data;
  },

  async deactivateAlias(partnerId: number | string, aliasId: number | string) {
    const { data } = await httpClient.delete(API_ENDPOINTS.partners.alias(partnerId, aliasId));
    return data as { partnerAliasId: number; status: string };
  },

  // ── Documents ─────────────────────────────────────────────────────────
  async getDocuments(partnerId: number | string): Promise<PartnerDocument[]> {
    const { data } = await httpClient.get<PartnerDocument[]>(
      API_ENDPOINTS.partners.documents(partnerId),
    );
    return data;
  },

  async addDocument(
    partnerId: number | string,
    payload: { fileId: number; title: string; description?: string | null; documentCategory?: string | null },
  ): Promise<PartnerDocument> {
    const { data } = await httpClient.post<PartnerDocument>(
      API_ENDPOINTS.partners.documents(partnerId),
      payload,
    );
    return data;
  },

  // ── Visit guest partner links ─────────────────────────────────────────
  async getVisitPartnerLinks(visitInstanceId: number | string): Promise<VisitGuestPartnerLink[]> {
    const { data } = await httpClient.get<VisitGuestPartnerLink[]>(
      API_ENDPOINTS.visitPartnerLinks.list(visitInstanceId),
    );
    return data;
  },

  async linkGuestToPartner(
    visitInstanceId: number | string,
    payload: {
      guestMemberId?: number | null;
      minuteParticipantId?: number | null;
      partnerId: number;
      partnerContactId?: number | null;
      matchSource?: string;
      matchStatus?: string;
      note?: string | null;
    },
  ): Promise<VisitGuestPartnerLink> {
    const { data } = await httpClient.post<VisitGuestPartnerLink>(
      API_ENDPOINTS.visitPartnerLinks.create(visitInstanceId),
      payload,
    );
    return data;
  },

  async rejectLinkSuggestion(visitInstanceId: number | string, linkId: number | string) {
    const { data } = await httpClient.post(
      API_ENDPOINTS.visitPartnerLinks.rejectSuggestion(visitInstanceId, linkId),
    );
    return data as { linkId: number; matchStatus: string };
  },

  async createPartnerFromGuest(
    visitInstanceId: number | string,
    payload: {
      guestMemberId?: number | null;
      minuteParticipantId?: number | null;
      partnerName?: string | null;
      partnerCode?: string | null;
      country?: string | null;
      city?: string | null;
      websiteUrl?: string | null;
      address?: string | null;
      description?: string | null;
      partnerType?: string | null;
      contactEmail?: string | null;
      contactPhone?: string | null;
    },
  ) {
    const { data } = await httpClient.post(
      API_ENDPOINTS.visitPartnerLinks.createPartnerFromGuest(visitInstanceId),
      payload,
    );
    return data as {
      partnerId: number;
      profileStatus: string;
      linkId: number;
      initialContactId?: number | null;
    };
  },
};
