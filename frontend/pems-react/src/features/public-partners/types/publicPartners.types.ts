/** Public Partner types — mirrors backend PublicPartnerDto (PublicPartnersController, anonymous). */

export interface PublicPartner {
  partnerId: number;
  name: string;
  shortName?: string | null;
  country?: string | null;
  city?: string | null;
  websiteUrl?: string | null;
  address?: string | null;
  description?: string | null;
  partnerType: string;
  logoFileId?: number | null;
  coverFileId?: number | null;
  publicSlug?: string | null;
}

export interface PublicPartnerListResponse {
  items: PublicPartner[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export type PublicPartnerSort = 'name_asc' | 'newest' | 'country';

export interface PublicPartnerListParams {
  search?: string;
  country?: string;
  partnerType?: string;
  sort?: PublicPartnerSort;
  page?: number;
  pageSize?: number;
}

/** One distinct country among APPROVED + PUBLIC partners (GET /public/partners/countries). */
export interface PublicPartnerCountry {
  /** Exact `partners.country` value — pass this straight back as the list filter's `country`. */
  value: string;
  label: string;
  count: number;
}

/** One distinct partner_type among APPROVED + PUBLIC partners (GET /public/partners/types). */
export interface PublicPartnerType {
  /** Exact `partners.partner_type` enum value — pass this straight back as the list filter's `partnerType`. */
  value: string;
  label: string;
  count: number;
}
