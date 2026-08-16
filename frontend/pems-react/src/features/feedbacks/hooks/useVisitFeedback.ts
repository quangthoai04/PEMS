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
import { getApiErrorMessage } from '../../../shared/utils/toast';
import i18n from '../../../shared/i18n/config';

/** Thin wrapper over the shared API-error helper (errorCode -> i18n, raw VI suppressed in EN mode). */
export function feedbackApiError(e: any, fallback: string): string {
  return getApiErrorMessage(e, fallback);
}

/**
 * Overrides the Visitor-only "overall visit" group's Vietnamese-only backend strings (group title,
 * target subtitle/context, submit hint) with i18n text when reading in English. The backend keeps
 * emitting Vietnamese for everyone else (Host groups are untouched — out of scope, Host is never
 * Visitor) and additionally sends stable codes (`groupCode: "OVERALL"`, `submitHintKey`) precisely so
 * the frontend can do this without guessing at free text. VI mode and the Host actor type are
 * returned unchanged.
 */
function applyVisitorEnglishOverrides(data: VisitFeedbackTargetsResponse): VisitFeedbackTargetsResponse {
  if (i18n.language !== 'en' || data.actorType !== 'VISITOR') return data;

  const groups = data.groups.map((g) => {
    if (g.groupCode !== 'OVERALL') return g;
    return {
      ...g,
      title: i18n.t('feedback:overallGroup.title'),
      targets: g.targets.map((tgt) => ({
        ...tgt,
        subtitle: data.campusName
          ? i18n.t('feedback:overallGroup.subtitleWithCampus', { campusName: data.campusName })
          : i18n.t('feedback:overallGroup.subtitleNoCampus'),
        targetContext: i18n.t('feedback:overallGroup.targetContext'),
      })),
    };
  });

  const submitHintMessage = data.submitHintKey
    ? i18n.t(`feedback:submitHint.${data.submitHintKey}`)
    : data.submitHintMessage;

  return { ...data, groups, submitHintMessage };
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
      setData(applyVisitorEnglishOverrides(res));
    } catch (e: any) {
      setLoadError(feedbackApiError(e, i18n.t('feedback:loadDataErrorFallback')));
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
      throw new Error(i18n.t('feedback:needAtLeastOneRating'));
    }
    setSubmitting(true);
    try {
      // Ignore the backend's raw message — it is Vietnamese-only prose, not a stable code, and would
      // leak untranslated text into English mode.
      await visitFeedbackApi.submit(visitInstanceId, ratedItems);
      setDrafts({});
      await load();
      return i18n.t('feedback:submitSuccess');
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
