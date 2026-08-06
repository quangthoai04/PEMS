import httpClient from '../../../shared/api/httpClient';
import { API_ENDPOINTS } from '../../../shared/api/endpoints';
import type { SearchInformationParams, SearchInformationResult } from '../types/publicSearch.types';

/** Public site-wide search API — PublicContentController. Anonymous, no token required. */
export const publicSearchApi = {
  /**
   * `signal` lets the caller abort a request that is no longer wanted — the keyword changed, or the
   * site language did. Without it a slow VI response can land after the EN one and overwrite it.
   */
  async search(params: SearchInformationParams, signal?: AbortSignal): Promise<SearchInformationResult> {
    const { data } = await httpClient.get<SearchInformationResult>(
      API_ENDPOINTS.publicSearch.search,
      { params, signal },
    );
    return data;
  },
};
