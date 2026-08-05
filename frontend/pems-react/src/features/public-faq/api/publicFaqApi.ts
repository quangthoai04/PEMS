import httpClient from '../../../shared/api/httpClient';
import { API_ENDPOINTS } from '../../../shared/api/endpoints';
import type {
  PublicFaqItem,
  PublicFaqListParams,
  PublicFaqListResponse,
  PublicFaqTypeCount,
} from '../types/publicFaq.types';

/** Public FAQ (Help Center) API — PublicContentController. Anonymous, no token required. */
export const publicFaqApi = {
  async getPublicFaqs(params: PublicFaqListParams = {}): Promise<PublicFaqListResponse> {
    const { data } = await httpClient.get<PublicFaqListResponse>(API_ENDPOINTS.publicFaqs.list, { params });
    return data;
  },

  async getFaqTypeCounts(languageCode?: string): Promise<PublicFaqTypeCount[]> {
    const { data } = await httpClient.get<PublicFaqTypeCount[]>(API_ENDPOINTS.publicFaqs.typeCounts, {
      params: languageCode ? { languageCode } : undefined,
    });
    return data;
  },

  /**
   * One PUBLISHED FAQ, for the /faq?faqId= deep link. Rejects (404) when the FAQ is hidden, gone, or
   * has no content in this language — the caller is expected to keep the page usable and say so.
   */
  async getPublicFaqDetail(faqId: number, languageCode?: string): Promise<PublicFaqItem> {
    const { data } = await httpClient.get<PublicFaqItem>(API_ENDPOINTS.publicFaqs.detail(faqId), {
      params: languageCode ? { languageCode } : undefined,
    });
    return data;
  },
};
