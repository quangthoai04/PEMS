/**
 * VisitFeedbackModal — modal đánh giá chuyến thăm mở ngay trên trang danh sách / trang chi tiết
 * (không chuyển route). Desktop: modal giữa màn rộng vừa đủ; mobile: bottom sheet.
 * Header nhỏ: tên đoàn + trạng thái + campus. Body: các nhóm compact row (sao bắt buộc,
 * comment mở qua icon bút). Footer: nút "Gửi đánh giá" cố định dưới modal.
 */
import React, { useEffect, useState } from 'react';
import { createPortal } from 'react-dom';
import { CheckCircle2, Loader2, Send, Star, X } from 'lucide-react';
import toast from 'react-hot-toast';
import { feedbackApiError, useVisitFeedback } from '../hooks/useVisitFeedback';
import { FeedbackGroupSection } from './FeedbackGroupSection';

const STATUS_LABELS: Record<string, string> = {
  DURING_VISIT: 'Đang tiếp khách',
  AFTER_VISIT: 'Chờ đóng đoàn',
  CLOSED: 'Đã hoàn tất',
};

interface Props {
  open: boolean;
  visitInstanceId: number | string | null;
  onClose: () => void;
  /** Gọi sau khi gửi thành công — parent cập nhật row "Đã đánh giá" / ẩn nút. */
  onSubmitted?: (visitInstanceId: number) => void;
}

export function VisitFeedbackModal({ open, visitInstanceId, onClose, onSubmitted }: Props) {
  if (!open || !visitInstanceId) return null;
  return <VisitFeedbackModalInner visitInstanceId={visitInstanceId} onClose={onClose} onSubmitted={onSubmitted} />;
}

function VisitFeedbackModalInner({ visitInstanceId, onClose, onSubmitted }: {
  visitInstanceId: number | string;
  onClose: () => void;
  onSubmitted?: (visitInstanceId: number) => void;
}) {
  const {
    data, loading, loadError,
    drafts, setRating, setComment,
    ratedItems, submitting, submit,
  } = useVisitFeedback(visitInstanceId);

  // Khóa scroll nền khi modal mở.
  useEffect(() => {
    const prev = document.body.style.overflow;
    document.body.style.overflow = 'hidden';
    return () => { document.body.style.overflow = prev; };
  }, []);

  const handleSubmit = async () => {
    try {
      const msg = await submit();
      toast.success(msg);
      onSubmitted?.(Number(visitInstanceId));
      onClose();
    } catch (e: any) {
      toast.error(e?.response ? feedbackApiError(e, 'Không gửi được đánh giá. Vui lòng thử lại.') : (e?.message || 'Không gửi được đánh giá.'));
    }
  };

  const statusLabel = data ? (STATUS_LABELS[data.instanceStatus] ?? data.instanceStatus) : '';
  const disabled = !data?.canSubmit || submitting;
  let runningIndex = 1;

  return createPortal(
    <div className="fixed inset-0 z-[90] flex items-end sm:items-center justify-center p-0 sm:px-4">
      <div className="absolute inset-0 bg-black/50" onClick={submitting ? undefined : onClose} />
      <div className="relative flex w-full flex-col rounded-t-2xl bg-white shadow-2xl border border-slate-200 sm:w-[960px] sm:max-w-[96vw] sm:rounded-xl max-h-[92vh] sm:max-h-[88vh]">
        {/* Header nhỏ: tiêu đề + tên đoàn + trạng thái + campus */}
        <div className="flex items-start justify-between gap-3 border-b border-slate-200 px-4 py-2.5">
          <div className="min-w-0">
            <h3 className="flex items-center gap-1.5 text-sm font-bold text-slate-900">
              <Star className="h-4 w-4 text-[#F37021]" /> Đánh giá chuyến thăm
              {data && (
                <span className="ml-1 inline-flex rounded-full border border-slate-200 bg-slate-50 px-2 py-0.5 text-[11px] font-semibold text-slate-600">
                  {statusLabel}
                </span>
              )}
            </h3>
            {data && (
              <p className="mt-0.5 truncate text-xs text-slate-500">
                {data.delegationName}{data.campusName ? ` • ${data.campusName}` : ''}
              </p>
            )}
          </div>
          <button
            type="button"
            onClick={onClose}
            disabled={submitting}
            className="shrink-0 rounded-full p-1.5 text-slate-500 hover:bg-slate-100 outline-none"
            aria-label="Đóng"
          >
            <X className="h-4 w-4" />
          </button>
        </div>

        {/* Body scroll */}
        <div className="flex-1 overflow-y-auto px-4 py-3">
          {loading ? (
            <div className="flex items-center justify-center gap-2 py-10 text-sm text-slate-500">
              <Loader2 className="h-4 w-4 animate-spin" /> Đang tải dữ liệu đánh giá...
            </div>
          ) : loadError || !data ? (
            <p className="py-10 text-center text-sm text-red-600">{loadError || 'Không tải được dữ liệu.'}</p>
          ) : (
            <>
              {data.alreadySubmittedAllRequired ? (
                <div className="mb-3 flex items-center gap-2 rounded-lg border border-emerald-200 bg-emerald-50 px-3 py-2 text-sm font-semibold text-emerald-700">
                  <CheckCircle2 className="h-4 w-4 shrink-0" />
                  {data.actorType === 'VISITOR' ? 'Bạn đã đánh giá chuyến thăm này.' : 'Bạn đã đánh giá đầy đủ các mục của chuyến thăm này.'}
                </div>
              ) : data.submitHintMessage ? (
                <p className="mb-2 text-xs text-slate-500">{data.submitHintMessage}</p>
              ) : null}

              <div className="space-y-2.5">
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
                      forceShowComment={data.actorType === 'VISITOR'}
                      onRate={setRating}
                      onChangeComment={setComment}
                    />
                  );
                })}
              </div>
            </>
          )}
        </div>

        {/* Footer sticky trong modal */}
        {data?.canSubmit && (
          <div className="flex items-center justify-between gap-3 border-t border-slate-200 bg-white px-4 py-2.5 rounded-b-2xl sm:rounded-b-xl">
            <p className="text-xs text-slate-500">
              {ratedItems.length > 0 ? `${ratedItems.length} mục sẽ được gửi` : 'Chấm sao để gửi đánh giá'}
            </p>
            <button
              type="button"
              disabled={submitting || ratedItems.length === 0}
              onClick={handleSubmit}
              className="inline-flex items-center gap-1.5 rounded-lg bg-[#F37021] px-4 py-2 text-sm font-bold text-white hover:bg-[#e0611d] disabled:opacity-50 outline-none"
            >
              {submitting ? <Loader2 className="h-4 w-4 animate-spin" /> : <Send className="h-4 w-4" />}
              Gửi đánh giá
            </button>
          </div>
        )}
      </div>
    </div>,
    document.body
  );
}
