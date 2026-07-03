/**
 * Types cho luồng feedback rule mới (v10):
 * - Visitor đánh giá chung chuyến thăm (VISITOR_OVERALL).
 * - Host đánh giá bên tham gia (HOST_PARTICIPANT) và bên hậu cần (HOST_LOGISTICS).
 * Mirror 1:1 các DTO backend (GetVisitFeedbackTargets / SubmitVisitFeedback / my-pending).
 */

export type FeedbackActorType = 'VISITOR' | 'HOST';

export type FeedbackType = 'VISITOR_OVERALL' | 'HOST_PARTICIPANT' | 'HOST_LOGISTICS';

export type FeedbackTargetType =
  | 'VISIT_REQUEST'
  | 'VISIT_INSTANCE'
  | 'VISIT_PARTICIPANT'
  | 'GUEST_MEMBER'
  | 'LOGISTICS_ITEM'
  | 'LOGISTICS_HANDOVER'
  | 'USER'
  | 'DEPARTMENT';

export interface FeedbackTarget {
  targetKey: string;
  feedbackType: FeedbackType;
  targetType: FeedbackTargetType;
  targetUserId?: number | null;
  targetParticipantId?: number | null;
  targetGuestMemberId?: number | null;
  targetLogisticsItemId?: number | null;
  targetHandoverId?: number | null;
  targetDepartmentId?: number | null;
  name: string;
  subtitle?: string | null;
  targetRole?: string | null;
  targetContext: string;
  alreadySubmitted: boolean;
  existingRating?: number | null;
  existingComment?: string | null;
}

export interface FeedbackGroup {
  groupCode: string;
  title: string;
  infoNote?: string | null;
  targets: FeedbackTarget[];
}

export interface ExistingFeedback {
  feedbackId: number;
  feedbackType: FeedbackType;
  targetType: FeedbackTargetType;
  targetName: string;
  rating: number;
  comment?: string | null;
  submittedAt: string;
}

export interface VisitFeedbackTargetsResponse {
  canSubmit: boolean;
  alreadySubmittedAllRequired: boolean;
  actorType: FeedbackActorType;
  visitRequestId: number;
  visitInstanceId: number;
  delegationName: string;
  campusName?: string | null;
  instanceStatus: string;
  plannedStartAt?: string | null;
  plannedEndAt?: string | null;
  groups: FeedbackGroup[];
  existingFeedbacks: ExistingFeedback[];
  submitHintMessage?: string | null;
}

export interface SubmitVisitFeedbackItem {
  feedbackType: FeedbackType;
  targetType: FeedbackTargetType;
  targetUserId?: number | null;
  targetParticipantId?: number | null;
  targetGuestMemberId?: number | null;
  targetLogisticsItemId?: number | null;
  targetHandoverId?: number | null;
  targetDepartmentId?: number | null;
  rating: number;
  comment?: string | null;
}

export interface SubmitVisitFeedbackResponse {
  savedCount: number;
  alreadySubmittedAllRequired: boolean;
  message: string;
}

export interface PendingFeedbackItem {
  visitRequestId: number;
  visitInstanceId: number;
  actorType: FeedbackActorType;
  delegationName: string;
  campusName?: string | null;
  instanceStatus: string;
  plannedStartAt?: string | null;
  alreadySubmitted: boolean;
}

export interface PendingFeedbackResponse {
  items: PendingFeedbackItem[];
  pendingCount: number;
}
