export interface FeedbackVisitSummaryItem {
  visitRequestId: number;
  visitInstanceId?: number | null;
  visitTitle: string;
  campusName?: string;
  totalFeedbacks: number;
  averageRating: number;
  latestSubmittedAt?: string;
  latestSubmitterName?: string;
  lowRatingCount: number;
}

export interface FeedbackListItem {
  feedbackId: number;
  visitRequestId: number;
  visitInstanceId?: number | null;
  visitTitle?: string;
  feedbackType: string;
  submittedByUserId: number;
  submitterRole: string;
  submitterContext?: string;
  submitterNameSnapshot: string;
  targetUserId: number;
  targetRole: string;
  targetContext?: string;
  targetNameSnapshot: string;
  rating: number;
  commentPreview?: string;
  submittedAt: string;
  ratingItemCount?: number;
}

export interface FeedbackRatingItem {
  feedbackRatingItemId: number;
  feedbackId: number;
  criterionCode: string;
  criterionLabel: string;
  rating: number;
  displayOrder: number;
  createdAt: string;
}

export interface FeedbackDetail extends FeedbackListItem {
  comment?: string;
  ratingItems?: FeedbackRatingItem[];
}

export interface FeedbackFilterParams {
  q?: string;
  ratingLevel?: string;
  feedbackType?: string;
  submitterRole?: string;
  fromDate?: string; // ISO date
  toDate?: string; // ISO date
  visitRequestId?: number;
  page?: number;
  pageSize?: number;
}

export interface PaginatedResult<T> {
  items: T[];
  totalItems: number;
  page: number;
  pageSize: number;
  totalPages: number;
}
