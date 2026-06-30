import httpClient from '../../../shared/api/httpClient';
import { API_ENDPOINTS } from '../../../shared/api/endpoints';
import {
  FeedbackFilterParams,
  FeedbackListItem,
  FeedbackVisitSummaryItem,
  PaginatedResult,
  FeedbackDetail,
} from '../types/feedbacks.types';

export const feedbacksApi = {
  getVisitSummaries: async (params: FeedbackFilterParams): Promise<PaginatedResult<FeedbackVisitSummaryItem>> => {
    const { data } = await httpClient.get(API_ENDPOINTS.feedbacks.visitSummary, { params });
    return data;
  },

  getRawFeedbacks: async (params: FeedbackFilterParams): Promise<PaginatedResult<FeedbackListItem>> => {
    const { data } = await httpClient.get(API_ENDPOINTS.feedbacks.raw, { params });
    return data;
  },

  getVisitSummaryDetail: async (visitRequestId: string | number) => {
    const { data } = await httpClient.get(API_ENDPOINTS.feedbacks.visitSummaryDetail(visitRequestId));
    return data;
  },

  getVisitInstanceSummaryDetail: async (visitRequestId: string | number, visitInstanceId: string | number) => {
    const { data } = await httpClient.get(API_ENDPOINTS.feedbacks.visitInstanceSummaryDetail(visitRequestId, visitInstanceId));
    return data;
  },

  getFeedbackDetail: async (feedbackId: string | number): Promise<FeedbackDetail> => {
    const { data } = await httpClient.get(API_ENDPOINTS.feedbacks.detail(feedbackId));
    return data;
  },
};
