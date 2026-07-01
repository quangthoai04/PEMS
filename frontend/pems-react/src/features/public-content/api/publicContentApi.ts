import httpClient from '../../../shared/api/httpClient';
import { PublicNewsDetail } from '../types/publicContent.types';

export const publicContentApi = {
  getPublicNewsDetail: async (newsId: number | string): Promise<PublicNewsDetail> => {
    const { data } = await httpClient.get<PublicNewsDetail>(`/public/news/${newsId}`);
    return data;
  }
};
