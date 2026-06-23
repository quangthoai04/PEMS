// Types for the application
export type Gender = 'MALE' | 'FEMALE' | 'OTHER' | null;

export interface NewsItem {
  id: string;
  title: string;
  excerpt: string;
  imageUrl: string;
  date: string;
}

export interface StatItem {
  value: string;
  label: string;
  description?: string;
  highlight?: boolean;
}
