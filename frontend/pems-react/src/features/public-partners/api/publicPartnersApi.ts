import httpClient from '../../../shared/api/httpClient';
import { API_ENDPOINTS } from '../../../shared/api/endpoints';
import type {
  PublicPartner,
  PublicPartnerCountry,
  PublicPartnerListParams,
  PublicPartnerListResponse,
} from '../types/publicPartners.types';

function cleanParams(params: Record<string, unknown>): Record<string, unknown> {
  return Object.fromEntries(
    Object.entries(params).filter(([, v]) => v !== undefined && v !== null && v !== ''),
  );
}

/** Public Partner Directory API — PublicPartnersController. Anonymous, no token required. */
export const publicPartnersApi = {
  async getPublicPartners(params: PublicPartnerListParams = {}): Promise<PublicPartnerListResponse> {
    const { data } = await httpClient.get<PublicPartnerListResponse>(API_ENDPOINTS.publicPartners.list, {
      params: cleanParams(params as Record<string, unknown>),
    });
    return data;
  },

  async getPublicPartnerDetail(idOrSlug: string | number): Promise<PublicPartner> {
    const { data } = await httpClient.get<PublicPartner>(API_ENDPOINTS.publicPartners.detail(idOrSlug));
    return data;
  },

  async getPublicPartnerCountries(): Promise<PublicPartnerCountry[]> {
    const { data } = await httpClient.get<PublicPartnerCountry[]>(API_ENDPOINTS.publicPartners.countries);
    return data;
  },
};
