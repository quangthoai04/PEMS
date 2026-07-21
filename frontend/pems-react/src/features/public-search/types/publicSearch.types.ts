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
  country?: string | null;
  publicSlug?: string | null;
}

export interface SearchFaqResult {
  faqId: number;
  question: string;
  faqTypeLabel: string;
}

export interface SearchCampusResult {
  campusId: number;
  campusCode: string;
  name: string;
}

export interface SearchInformationResult {
  news: SearchNewsResult[];
  partners: SearchPartnerResult[];
  faqs: SearchFaqResult[];
  campuses: SearchCampusResult[];
  totalCount: number;
}

export interface SearchInformationParams {
  keyword: string;
  limit?: number;
  /** Preferred language for News/FAQ result text — same convention as News/FAQ public pages. */
  languageCode?: string;
}
