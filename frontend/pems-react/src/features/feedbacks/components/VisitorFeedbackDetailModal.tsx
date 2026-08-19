/**
 * VisitorFeedbackDetailModal — hiển thị đánh giá chung của Visitor về chuyến thăm, mở từ
 * notification loại OPEN_VISITOR_FEEDBACK_MODAL. Chỉ Host hiện tại/Staff Leader campus đó xem
 * được (GET /feedbacks/visitor-feedback/{visitInstanceId} tự kiểm tra quyền phía server).
 */
import React, { useEffect, useState } from 'react';
import { createPortal } from 'react-dom';
import { ChevronDown, ChevronUp, Loader2, Star, X } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { visitFeedbackApi } from '../api/visitFeedbackApi';
import type { VisitorFeedbackResponse } from '../types/visitFeedback.types';
import { visitInstanceStatusLabelVi } from '../utils/visitInstanceStatusLabel';

import { formatVietnamDateTime } from '../../../shared/utils/vietnamTime';
interface Props {
  open: boolean;
  visitInstanceId: number | string | null;
  onClose: () => void;
}

export function VisitorFeedbackDetailModal({ open, visitInstanceId, onClose }: Props) {
  if (!open || !visitInstanceId) return null;
  return <VisitorFeedbackDetailModalInner visitInstanceId={visitInstanceId} onClose={onClose} />;
}

function VisitorFeedbackDetailModalInner({ visitInstanceId, onClose }: { visitInstanceId: number | string; onClose: () => void }) {
  const { t } = useTranslation(['notifications']);
  const [data, setData] = useState<VisitorFeedbackResponse | null>(null);
  const [loading, setLoading] = useState(true);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [expanded, setExpanded] = useState(false);

  useEffect(() => {
    const prev = document.body.style.overflow;
    document.body.style.overflow = 'hidden';
    return () => { document.body.style.overflow = prev; };
  }, []);

  useEffect(() => {
    let cancelled = false;
    setLoading(true);
    setLoadError(null);
    visitFeedbackApi.getVisitorFeedback(visitInstanceId)
      .then((res) => { if (!cancelled) setData(res); })
      .catch(() => { if (!cancelled) setLoadError(t('notifications:visitorFeedbackModal.loading')); })
      .finally(() => { if (!cancelled) setLoading(false); });
    return () => { cancelled = true; };
  }, [visitInstanceId, t]);

  return createPortal(
    <div className="fixed inset-0 z-[90] flex items-end sm:items-center justify-center p-0 sm:px-4">
      <div className="absolute inset-0 bg-black/50" onClick={onClose} />
      <div className="relative flex w-full flex-col rounded-t-2xl bg-white shadow-2xl border border-slate-200 sm:w-[640px] sm:max-w-[96vw] sm:rounded-xl max-h-[92dvh] sm:max-h-[88dvh]">
        <div className="flex items-start justify-between gap-3 border-b border-slate-200 px-4 py-2.5">
          <h3 className="flex items-center gap-1.5 text-sm font-bold text-slate-900">
            <Star className="h-4 w-4 text-[#F37021]" /> {t('notifications:visitorFeedbackModal.title')}
          </h3>
          <button
            type="button"
            onClick={onClose}
            className="shrink-0 rounded-full p-1.5 text-slate-500 hover:bg-slate-100 outline-none"
            aria-label={t('notifications:visitorFeedbackModal.close')}
          >
            <X className="h-4 w-4" />
          </button>
        </div>

        <div className="flex-1 overflow-y-auto px-4 py-3">
          {loading ? (
            <div className="flex items-center justify-center gap-2 py-10 text-sm text-slate-500">
              <Loader2 className="h-4 w-4 animate-spin" /> {t('notifications:visitorFeedbackModal.loading')}
            </div>
          ) : loadError || !data ? (
            <p className="py-10 text-center text-sm text-red-600">{loadError}</p>
          ) : (
            <>
              <div className="mb-3 rounded-lg border border-slate-200 bg-slate-50">
                <button
                  type="button"
                  onClick={() => setExpanded((v) => !v)}
                  className="flex w-full items-center justify-between gap-2 px-3 py-2 text-left"
                >
                  <span className="min-w-0">
                    <span className="block truncate text-sm font-semibold text-slate-800">
                      {data.delegationName}{data.campusName ? ` • ${data.campusName}` : ''}
                    </span>
                    <span className="block text-xs text-slate-500">{t('notifications:visitorFeedbackModal.delegationInfoTitle')}</span>
                  </span>
                  {expanded ? <ChevronUp className="h-4 w-4 shrink-0 text-slate-500" /> : <ChevronDown className="h-4 w-4 shrink-0 text-slate-500" />}
                </button>
                {expanded && (
                  <div className="border-t border-slate-200 px-3 py-2 text-xs text-slate-600 space-y-1.5">
                    <div><span className="font-semibold text-slate-500">Mã đơn:</span> {data.requestCode}</div>
                    {data.organizationName && (
                      <div><span className="font-semibold text-slate-500">Tổ chức:</span> {data.organizationName}</div>
                    )}
                    {data.campusName && (
                      <div><span className="font-semibold text-slate-500">Cơ sở:</span> {data.campusName}</div>
                    )}
                    {data.hostName && (
                      <div><span className="font-semibold text-slate-500">Host:</span> {data.hostName}</div>
                    )}
                    <div><span className="font-semibold text-slate-500">Trạng thái:</span> {visitInstanceStatusLabelVi(data.instanceStatus)}</div>
                    {data.plannedStartAt && (
                      <div>
                        <span className="font-semibold text-slate-500">Thời gian:</span>{' '}
                        {formatVietnamDateTime(data.plannedStartAt)}
                        {data.plannedEndAt ? ` - ${formatVietnamDateTime(data.plannedEndAt)}` : ''}
                      </div>
                    )}
                  </div>
                )}
              </div>

              {data.feedbacks.length === 0 ? (
                <p className="py-6 text-center text-sm text-slate-500">{t('notifications:visitorFeedbackModal.noFeedback')}</p>
              ) : (
                <div className="space-y-3">
                  {data.feedbacks.map((f) => (
                    <div key={f.feedbackId} className="rounded-lg border border-slate-200 p-3">
                      <div className="flex items-center justify-between gap-2">
                        <span className="text-sm font-semibold text-slate-800">{f.visitorName || 'Visitor'}</span>
                        <div className="flex items-center gap-0.5">
                          {[1, 2, 3, 4, 5].map((n) => (
                            <Star
                              key={n}
                              className={`h-3.5 w-3.5 ${n <= f.rating ? 'fill-[#F37021] text-[#F37021]' : 'text-slate-300'}`}
                            />
                          ))}
                        </div>
                      </div>
                      {f.comment && <p className="mt-1.5 text-sm text-slate-600">{f.comment}</p>}
                    </div>
                  ))}
                </div>
              )}
            </>
          )}
        </div>
      </div>
    </div>,
    document.body
  );
}
