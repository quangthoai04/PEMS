import httpClient from '../../../shared/api/httpClient';
import {
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
  }
};
