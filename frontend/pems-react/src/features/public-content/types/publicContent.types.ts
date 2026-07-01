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
  sections: PublicNewsSection[];
};
