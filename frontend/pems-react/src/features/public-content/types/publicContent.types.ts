export type PublicContent = {
  // TODO: define types
};

export type PublicNewsMedia = {
  fileId: number;
  fileName?: string | null;
  mimeType?: string | null;
  url: string;
  thumbnailUrl?: string | null;
  displayOrder: number;
};

export type PublicNewsSection = {
  sectionId: number;
  sectionTitle?: string | null;
  sectionBodyHtml?: string | null;
  sectionBodyText?: string | null;
  displayOrder: number;
  files?: PublicNewsMedia[];
};

export type PublicNewsDetail = {
  newsId: number;
  slug?: string | null;
  title: string;
  summary?: string | null;
  thumbnailUrl?: string | null;
  publishedAt?: string | null;
  authorName?: string | null;
  /** Language of the returned content (after fallback resolution). */
  languageCode: string;
  /** All languages this post has a translation for (vi first). */
  availableLanguages: string[];
  sections: PublicNewsSection[];
};

export type PublicNewsListItem = {
  newsId: number;
  title: string;
  slug?: string | null;
  summary?: string | null;
  coverFileId?: number | null;
  /** Anonymous-safe backend URL (only files of published news resolve). */
  coverUrl?: string | null;
  publishedAt?: string | null;
  languageCode: string;
  isFeatured: boolean;
};

export type PublicNewsListResponse = {
  items: PublicNewsListItem[];
  pageIndex: number;
  pageSize: number;
  totalItems: number;
  totalPages: number;
};

export type PublicFaqItem = {
  faqId: number;
  faqType: string;
  faqTypeLabel: string;
  question: string;
  answer: string;
  displayOrder: number;
  createdAt: string;
};

export type PublicFaqListResponse = {
  items: PublicFaqItem[];
  page: number;
  pageSize: number;
  totalItems: number;
  totalPages: number;
  hasNextPage: boolean;
  hasPreviousPage: boolean;
};
