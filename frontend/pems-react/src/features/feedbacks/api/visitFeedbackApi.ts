import httpClient from '../../../shared/api/httpClient';
import { API_ENDPOINTS } from '../../../shared/api/endpoints';
import type {
  PendingFeedbackResponse,
  SubmitVisitFeedbackItem,
  SubmitVisitFeedbackResponse,
  VisitFeedbackTargetsResponse,
} from '../types/visitFeedback.types';

/** API cho feedback rule mới: targets, batch submit và nhắc đánh giá (chuông + visit list). */
export const visitFeedbackApi = {
  getTargets: async (visitInstanceId: string | number): Promise<VisitFeedbackTargetsResponse> => {
    const { data } = await httpClient.get(API_ENDPOINTS.feedbacks.visitFeedbackTargets(visitInstanceId));
    return data;
  },

  submit: async (
    visitInstanceId: string | number,
    items: SubmitVisitFeedbackItem[],
  ): Promise<SubmitVisitFeedbackResponse> => {
    const { data } = await httpClient.post(API_ENDPOINTS.feedbacks.submitVisitFeedback(visitInstanceId), { items });
    return data;
  },

  getMyPending: async (): Promise<PendingFeedbackResponse> => {
    const { data } = await httpClient.get(API_ENDPOINTS.feedbacks.myPending);
    return data;
  },
};
