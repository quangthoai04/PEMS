import httpClient from '../../../shared/api/httpClient';
import { API_ENDPOINTS } from '../../../shared/api/endpoints';
import type {
  MyHostFeedbackResponse,
  PendingFeedbackResponse,
  SubmitVisitFeedbackItem,
  SubmitVisitFeedbackResponse,
  VisitFeedbackTargetsResponse,
  VisitorFeedbackResponse,
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

  /** Feedback của host về CHÍNH user đang đăng nhập (dùng cho modal OPEN_HOST_FEEDBACK_MODAL). */
  getMyHostFeedback: async (visitInstanceId: string | number): Promise<MyHostFeedbackResponse> => {
    const { data } = await httpClient.get(API_ENDPOINTS.feedbacks.myHostFeedback(visitInstanceId));
    return data;
  },

  /** Feedback của Visitor về chuyến thăm (dùng cho modal OPEN_VISITOR_FEEDBACK_MODAL) — backend tự kiểm tra quyền Host/Staff Leader. */
  getVisitorFeedback: async (visitInstanceId: string | number): Promise<VisitorFeedbackResponse> => {
    const { data } = await httpClient.get(API_ENDPOINTS.feedbacks.visitorFeedback(visitInstanceId));
    return data;
  },
};
