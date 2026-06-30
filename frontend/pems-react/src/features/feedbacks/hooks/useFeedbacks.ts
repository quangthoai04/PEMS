import { useState, useCallback } from 'react';
import { feedbacksApi } from '../api/feedbacksApi';
import { FeedbackFilterParams, FeedbackVisitSummaryItem, PaginatedResult, FeedbackListItem } from '../types/feedbacks.types';

export const useFeedbacks = () => {
  const [summaries, setSummaries] = useState<PaginatedResult<FeedbackVisitSummaryItem> | null>(null);
  const [rawFeedbacks, setRawFeedbacks] = useState<PaginatedResult<FeedbackListItem> | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<Error | null>(null);

  const fetchSummaries = useCallback(async (params: FeedbackFilterParams) => {
    setLoading(true);
    setError(null);
    try {
      const data = await feedbacksApi.getVisitSummaries(params);
      setSummaries(data);
    } catch (err: any) {
      setError(err);
    } finally {
      setLoading(false);
    }
  }, []);

  const fetchRawFeedbacks = useCallback(async (params: FeedbackFilterParams) => {
    setLoading(true);
    setError(null);
    try {
      const data = await feedbacksApi.getRawFeedbacks(params);
      setRawFeedbacks(data);
    } catch (err: any) {
      setError(err);
    } finally {
      setLoading(false);
    }
  }, []);

  return {
    summaries,
    rawFeedbacks,
    loading,
    error,
    fetchSummaries,
    fetchRawFeedbacks,
  };
};
