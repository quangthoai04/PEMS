import httpClient from '../../../shared/api/httpClient';
import {
  PublicFaqListResponse,
  PublicNewsDetail,
  PublicNewsListResponse,
} from '../types/publicContent.types';

export interface PublicNewsListParams {
  pageIndex?: number;
  pageSize?: number;
  languageCode?: string;
  keyword?: string;
  isFeatured?: boolean;
}

export interface PublicFaqListParams {
  page?: number;
  pageSize?: number;
  keyword?: string;
  faqType?: string;
}

export const publicContentApi = {
  getPublicNewsList: async (params: PublicNewsListParams = {}): Promise<PublicNewsListResponse> => {
    const { data } = await httpClient.get<PublicNewsListResponse>('/public/news', { params });
    return data;
  },

  getPublicNewsDetail: async (newsId: number | string, languageCode?: string): Promise<PublicNewsDetail> => {
    const { data } = await httpClient.get<PublicNewsDetail>(`/public/news/${newsId}`, {
      params: languageCode ? { languageCode } : undefined,
    });
    return data;
  },

  getPublicFaqs: async (params: PublicFaqListParams = {}): Promise<PublicFaqListResponse> => {
    const { data } = await httpClient.get<PublicFaqListResponse>('/public/faqs', { params });
    return data;
  },
};
