import httpClient from '../../../shared/api/httpClient';
import { API_ENDPOINTS } from '../../../shared/api/endpoints';
import type {
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

  async getFaqTypeCounts(): Promise<PublicFaqTypeCount[]> {
    const { data } = await httpClient.get<PublicFaqTypeCount[]>(API_ENDPOINTS.publicFaqs.typeCounts);
    return data;
  },
};
