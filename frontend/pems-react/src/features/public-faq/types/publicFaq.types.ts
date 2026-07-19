/** Public FAQ types — mirrors backend ViewFaqDto / PublicFaqTypeCountDto (PublicContentController, anonymous). */

export type PublicFaqTypeValue =
  | 'ACCOUNT_ACCESS'
  | 'VISIT_REQUEST'
  | 'DELEGATION_MANAGEMENT'
  | 'LOGISTICS_RESOURCE'
  | 'DOCUMENT_MEDIA'
  | 'NOTIFICATION_EMAIL'
  | 'OTHER';

export interface PublicFaqItem {
  faqId: number;
  faqType: string;
  faqTypeLabel: string;
  question: string;
  answer: string;
  displayOrder: number;
  createdAt: string;
}

export interface PublicFaqListResponse {
  items: PublicFaqItem[];
  page: number;
  pageSize: number;
  totalItems: number;
  totalPages: number;
  hasNextPage: boolean;
  hasPreviousPage: boolean;
}

export interface PublicFaqListParams {
  keyword?: string;
  faqType?: string;
  page?: number;
  pageSize?: number;
  languageCode?: string;
}

/** One faq_type with its PUBLISHED question count (GET /public/faqs/type-counts). Always all 7 types. */
export interface PublicFaqTypeCount {
  /** Exact `faqs.faq_type` enum value — pass this back as the list filter's `faqType`. */
  value: string;
  label: string;
  count: number;
}
