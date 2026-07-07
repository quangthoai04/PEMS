import httpClient from '../../../shared/api/httpClient';
import { API_ENDPOINTS } from '../../../shared/api/endpoints';
import type { SearchInformationParams, SearchInformationResult } from '../types/publicSearch.types';

/** Public site-wide search API — PublicContentController. Anonymous, no token required. */
export const publicSearchApi = {
  async search(params: SearchInformationParams): Promise<SearchInformationResult> {
    const { data } = await httpClient.get<SearchInformationResult>(API_ENDPOINTS.publicSearch.search, { params });
    return data;
  },
};
