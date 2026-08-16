/**
 * Trang "Đánh giá chuyến thăm" — chỉ dùng cho deep-link (chuông thông báo).
 * Từ danh sách / visit process, đánh giá mở bằng VisitFeedbackModal ngay tại chỗ,
 * không chuyển route. Logic dùng chung qua useVisitFeedback.
 */
import React, { useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { ArrowLeft, CheckCircle2, Loader2, Send, Star } from 'lucide-react';
import toast from 'react-hot-toast';
import { feedbackApiError, useVisitFeedback } from '../../../features/feedbacks/hooks/useVisitFeedback';
import { FeedbackGroupSection } from '../../../features/feedbacks/components/FeedbackGroupSection';

// "yyyy-MM-ddTHH:mm:ss" → "HH:mm dd/MM/yyyy" (string slicing, không lệch múi giờ)
function fmt(value?: string | null): string {
  if (!value) return '—';
  const [d, t] = value.split('T');
  const [y, m, day] = (d || '').split('-');
  if (!y) return value;
  return `${(t || '').slice(0, 5)} ${day}/${m}/${y}`;
}

export function VisitFeedbackPage() {
  const { visitInstanceId } = useParams<{ visitInstanceId: string }>();
  const navigate = useNavigate();
  const { t } = useTranslation('feedback');

  const {
    data, loading, loadError,
    drafts, setRating, setComment,
    ratedItems, submitting, submit,
  } = useVisitFeedback(visitInstanceId);

  const handleSubmit = async () => {
    try {
      const msg = await submit();
      toast.success(msg);
    } catch (e: any) {
      toast.error(e?.response ? feedbackApiError(e, t('submitError')) : (e?.message || t('submitError')));
    }
  };

  if (loading) {
    return (
      <div className="flex items-center justify-center gap-2 py-16 text-slate-500">
        <Loader2 className="h-5 w-5 animate-spin" /> {t('loading')}
      </div>
    );
  }

  if (loadError || !data) {
    return (
      <div className="mx-auto max-w-xl py-12 text-center">
        <p className="text-sm text-red-600">{loadError || t('loadErrorFallback')}</p>
        <button
          type="button"
          onClick={() => navigate(-1)}
          className="mt-4 inline-flex items-center gap-1.5 rounded-lg border border-slate-200 px-3 py-1.5 text-xs font-bold text-slate-600 hover:bg-slate-50 outline-none"
        >
          <ArrowLeft className="h-3.5 w-3.5" /> {t('back')}
        </button>
      </div>
    );
  }

  const statusLabel = t(`status.${data.instanceStatus}`, { defaultValue: data.instanceStatus });
  const disabled = !data.canSubmit || submitting;
  let runningIndex = 1;

  return (
    <div className="mx-auto w-full max-w-4xl pb-20">
      {/* Compact header: 1 vùng mỏng — tên đoàn, status, thời gian, campus */}
      <div className="mb-3 flex flex-wrap items-center gap-x-3 gap-y-1 border-b border-slate-200 pb-3">
        <button
          type="button"
          onClick={() => navigate(-1)}
          className="rounded-lg p-1.5 text-slate-500 hover:bg-slate-100 outline-none"
          aria-label={t('back')}
        >
          <ArrowLeft className="h-4 w-4" />
        </button>
        <h1 className="text-base font-bold text-slate-900 flex items-center gap-1.5">
          <Star className="h-4 w-4 text-[#F37021]" /> {t('title')}
        </h1>
        <span className="inline-flex rounded-full border border-slate-200 bg-slate-50 px-2 py-0.5 text-[11px] font-semibold text-slate-600">
          {statusLabel}
        </span>
        <div className="basis-full text-xs text-slate-500 sm:basis-auto sm:ml-auto">
          <span className="font-semibold text-slate-700">{data.delegationName}</span>
          {data.campusName ? ` • ${data.campusName}` : ''} • {fmt(data.plannedStartAt)}
        </div>
      </div>

      {data.alreadySubmittedAllRequired ? (
        <div className="mb-3 flex items-center gap-2 rounded-lg border border-emerald-200 bg-emerald-50 px-3 py-2 text-sm font-semibold text-emerald-700">
          <CheckCircle2 className="h-4 w-4 shrink-0" />
          {data.actorType === 'VISITOR' ? t('alreadySubmittedVisitor') : t('alreadySubmittedOther')}
        </div>
      ) : data.submitHintMessage ? (
        <p className="mb-3 text-xs text-slate-500">{data.submitHintMessage}</p>
      ) : null}

      <div className="space-y-3">
        {data.groups.map((g) => {
          const start = runningIndex;
          runningIndex += g.targets.length;
          return (
            <FeedbackGroupSection
              key={g.groupCode}
              group={g}
              startIndex={start}
              drafts={drafts}
              disabled={disabled}
              onRate={setRating}
              onChangeComment={setComment}
            />
          );
        })}
      </div>

      {/* Sticky save bar — không bắt user kéo hết trang */}
      {data.canSubmit && (
        <div className="fixed bottom-0 left-0 right-0 z-40 border-t border-slate-200 bg-white/95 backdrop-blur px-4 py-2.5">
          <div className="mx-auto flex max-w-4xl items-center justify-between gap-3">
            <p className="text-xs text-slate-500">
              {ratedItems.length > 0 ? t('itemsToSubmit', { count: ratedItems.length }) : t('rateToSubmitHint')}
            </p>
            <button
              type="button"
              disabled={submitting || ratedItems.length === 0}
              onClick={handleSubmit}
              className="inline-flex items-center gap-1.5 rounded-lg bg-[#F37021] px-4 py-2 text-sm font-bold text-white hover:bg-[#e0611d] disabled:opacity-50 outline-none"
            >
              {submitting ? <Loader2 className="h-4 w-4 animate-spin" /> : <Send className="h-4 w-4" />}
              {t('submit')}
            </button>
          </div>
        </div>
      )}
    </div>
  );
}
