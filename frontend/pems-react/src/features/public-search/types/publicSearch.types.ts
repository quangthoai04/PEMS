/** Public site-wide search types — mirrors backend SearchInformationDto (PublicContentController, anonymous). */

export interface SearchNewsResult {
  newsId: number;
  title: string;
  summary?: string | null;
  publishedAt?: string | null;
}

export interface SearchPartnerResult {
  partnerId: number;
  name: string;
  descriptionPreview?: string | null;
  country?: string | null;
  publicSlug?: string | null;
}

export interface SearchFaqResult {
  faqId: number;
  question: string;
  answerPreview?: string | null;
  faqType: string;
  faqTypeLabel: string;
}

export interface SearchGalleryResult {
  galleryItemId: number;

  title: string;
  descriptionPreview?: string | null;

  /** campusCode + locationId + galleryItemId are what the /visit-fptu deep link is built from. */
  campusCode: string;
  campusName: string;

  areaId: number;
  areaName: string;

  locationId: number;
  locationName: string;

  mediaKind: string;
  thumbnailUrl?: string | null;
}

export interface SearchHasMore {
  news: boolean;
  partners: boolean;
  galleries: boolean;
  faqs: boolean;
}

export interface SearchInformationResult {
  news: SearchNewsResult[];
  partners: SearchPartnerResult[];
  galleries: SearchGalleryResult[];
  faqs: SearchFaqResult[];

  /** Per-section "more matches exist than are shown here" — drives the Partner "view more" CTA. */
  hasMore: SearchHasMore;

  /** Rows returned in this response, i.e. what the popup renders — not a database-wide match count. */
  totalCount: number;
}

/** The only two values the backend search accepts; anything else is a validation error there. */
export type PublicSearchLanguage = 'vi' | 'en';

/**
 * i18n reports regional tags ('en-US', 'vi-VN'); the search API takes the bare language. Normalising
 * here rather than sending i18n.language straight through is what stops an 'en-US' UI from being
 * served Vietnamese results.
 */
export function normalizePublicSearchLanguage(language?: string): PublicSearchLanguage {
  return language?.toLowerCase().startsWith('en') ? 'en' : 'vi';
}

/** BCP-47 locale for Intl formatting, derived from the same normalised language. */
export function publicSearchLocale(language: PublicSearchLanguage): string {
  return language === 'en' ? 'en-US' : 'vi-VN';
}

export interface SearchInformationParams {
  keyword: string;
  limit?: number;
  /** Must already be normalised to 'vi' | 'en' — see normalizePublicSearchLanguage. */
  languageCode?: PublicSearchLanguage;
}
