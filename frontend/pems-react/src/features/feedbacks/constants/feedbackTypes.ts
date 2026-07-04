export const FEEDBACK_TYPES = {
  VISITOR_OVERALL: 'VISITOR_OVERALL',
  HOST_DELEGATION_OVERALL: 'HOST_DELEGATION_OVERALL',
  HOST_PARTICIPANT: 'HOST_PARTICIPANT',
  HOST_LOGISTICS: 'HOST_LOGISTICS',
} as const;

export const FEEDBACK_TYPE_LABELS: Record<string, string> = {
  VISITOR_OVERALL: 'Khách đánh giá chuyến thăm',
  HOST_DELEGATION_OVERALL: 'Host đánh giá đoàn khách',
  HOST_PARTICIPANT: 'Host đánh giá bên tham gia',
  HOST_LOGISTICS: 'Host đánh giá hậu cần / đồ mượn',
};
