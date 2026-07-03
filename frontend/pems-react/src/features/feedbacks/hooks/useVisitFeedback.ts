/**
 * useVisitFeedback — state + logic dùng chung cho modal đánh giá (mở từ danh sách /
 * visit process) và trang đánh giá (deep-link từ chuông thông báo).
 * Load targets, quản lý draft rating/comment theo targetKey, submit batch.
 */
import { useCallback, useEffect, useMemo, useState } from 'react';
import { visitFeedbackApi } from '../api/visitFeedbackApi';
import type { FeedbackDraft } from '../components/FeedbackTargetRow';
import type {
  SubmitVisitFeedbackItem,
  VisitFeedbackTargetsResponse,
} from '../types/visitFeedback.types';

export function feedbackApiError(e: any, fallback: string): string {
  const data = e?.response?.data;
  if (typeof data === 'string' && data.trim()) return data;
  if (data?.message) return data.message;
  if (Array.isArray(data?.errors) && data.errors.length) return data.errors[0];
  return fallback;
}

export function useVisitFeedback(visitInstanceId: string | number | undefined) {
  const [data, setData] = useState<VisitFeedbackTargetsResponse | null>(null);
  const [loading, setLoading] = useState(true);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [drafts, setDrafts] = useState<Record<string, FeedbackDraft>>({});
  const [submitting, setSubmitting] = useState(false);

  const load = useCallback(async () => {
    if (!visitInstanceId) return;
    setLoading(true);
    setLoadError(null);
    try {
      const res = await visitFeedbackApi.getTargets(visitInstanceId);
      setData(res);
    } catch (e: any) {
      setLoadError(feedbackApiError(e, 'Không tải được dữ liệu đánh giá.'));
    } finally {
      setLoading(false);
    }
  }, [visitInstanceId]);

  useEffect(() => { void load(); }, [load]);

  const allTargets = useMemo(() => (data?.groups ?? []).flatMap((g) => g.targets), [data]);
  const targetByKey = useMemo(() => new Map(allTargets.map((t) => [t.targetKey, t])), [allTargets]);

  // Các mục đã chấm sao (>=1) và chưa gửi trước đó — payload gửi lên backend.
  const ratedItems = useMemo<SubmitVisitFeedbackItem[]>(() => {
    return allTargets
      .filter((t) => !t.alreadySubmitted)
      .map((t) => ({ t, d: drafts[t.targetKey] }))
      .filter(({ d }) => d && d.rating >= 1)
      .map(({ t, d }) => ({
        feedbackType: t.feedbackType,
        targetType: t.targetType,
        targetUserId: t.targetUserId ?? null,
        targetParticipantId: t.targetParticipantId ?? null,
        targetGuestMemberId: t.targetGuestMemberId ?? null,
        targetLogisticsItemId: t.targetLogisticsItemId ?? null,
        targetHandoverId: t.targetHandoverId ?? null,
        targetDepartmentId: t.targetDepartmentId ?? null,
        rating: d!.rating,
        comment: d!.comment.trim() || null,
      }));
  }, [allTargets, drafts]);

  const setRating = useCallback((key: string, rating: number) =>
    setDrafts((prev) => ({ ...prev, [key]: { rating, comment: prev[key]?.comment ?? '' } })), []);
  const setComment = useCallback((key: string, comment: string) =>
    setDrafts((prev) => ({ ...prev, [key]: { rating: prev[key]?.rating ?? 0, comment } })), []);

  /** Gửi batch; trả message thành công hoặc throw (caller hiển thị toast). */
  const submit = useCallback(async (): Promise<string> => {
    if (!visitInstanceId || ratedItems.length === 0) {
      throw new Error('Vui lòng chấm sao ít nhất một mục trước khi gửi.');
    }
    setSubmitting(true);
    try {
      const res = await visitFeedbackApi.submit(visitInstanceId, ratedItems);
      setDrafts({});
      await load();
      return res.message || 'Đã gửi đánh giá.';
    } finally {
      setSubmitting(false);
    }
  }, [visitInstanceId, ratedItems, load]);

  return {
    data, loading, loadError, reload: load,
    drafts, setRating, setComment,
    ratedItems, submitting, submit,
    targetByKey,
  };
}
