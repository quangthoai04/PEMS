/**
 * SubmittedVisitRequestDetailModal — read-only detail of a visit request, built on the shared
 * SubmittedVisitRequestInfoPanel. Used in three contexts (driven by the server-computed flags
 * on the fetched data, never by the modal itself):
 *   • Pre-approval review     → footer Từ chối / Duyệt / Đóng   (canApprove/canReject)
 *   • Approved / waiting host  → footer Gán Host / Đóng          (canAssignHost)
 *   • Rejected detail          → rejection info + footer Đóng
 *
 * The modal contains NO approve/reject/assign-host logic: the footer buttons only call the
 * callbacks passed by the parent, which route to the existing UC-18 / UC-22 commands.
 */

import { useEffect, useState } from 'react';
import { X, Check, AlertCircle, Loader2, UserCog } from 'lucide-react';
import { motion, AnimatePresence } from 'motion/react';
import { delegationsApi } from '../../features/delegations/api/delegationsApi';
import type { SubmittedVisitRequestFormDetail } from '../../features/delegations/types/delegations.types';
import { VISIT_SCOPE_LABELS } from '../../features/delegations/types/delegations.types';
import { SubmittedVisitRequestInfoPanel } from '../../features/delegations/components/SubmittedVisitRequestInfoPanel';
import { DecisionReasonPanel } from '../../features/delegations/components/DecisionReasonPanel';
import { CancellationReasonPanel } from '../../features/delegations/components/CancellationReasonPanel';

interface Props {
  isOpen: boolean;
  visitRequestId: number | null;
  onClose: () => void;
  onApprove?: (data: SubmittedVisitRequestFormDetail) => void;
  onReject?: (data: SubmittedVisitRequestFormDetail) => void;
  onAssignHost?: (data: SubmittedVisitRequestFormDetail) => void;
}

const formatDateTime = (value?: string | null) => {
  if (!value) return '-';
  const d = new Date(value);
  if (Number.isNaN(d.getTime())) return '-';
  return d.toLocaleString('vi-VN', {
    hour: '2-digit', minute: '2-digit', day: '2-digit', month: '2-digit', year: 'numeric',
  });
};

const getFriendlyError = (e: any): string => {
  const status = e?.response?.status;
  if (status === 401) return 'Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.';
  if (status === 403) return 'Bạn không có quyền xem đơn này.';
  if (status === 404) return 'Không tìm thấy đơn đăng ký tham quan.';
  if (status === 409 || status === 422 || status === 400)
    return e?.response?.data?.message || 'Đơn không còn ở trạng thái phù hợp.';
  return e?.response?.data?.message || 'Không thể tải thông tin đơn. Vui lòng thử lại.';
};

const statusLabel = (status: string) => {
  switch (status) {
    case 'PENDING_APPROVAL': return 'Chờ duyệt';
    case 'APPROVED': return 'Đã duyệt';
    case 'REJECTED': return 'Từ chối';
    case 'CANCELLED': return 'Đã hủy';
    default: return status;
  }
};

const headerTitle = (status?: string) => {
  if (status === 'REJECTED') return 'Chi tiết đơn bị từ chối';
  if (status === 'PENDING_APPROVAL') return 'Xem đơn đăng ký tham quan';
  return 'Chi tiết đơn đăng ký tham quan';
};

export function SubmittedVisitRequestDetailModal({
  isOpen, visitRequestId, onClose, onApprove, onReject, onAssignHost,
}: Props) {
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [data, setData] = useState<SubmittedVisitRequestFormDetail | null>(null);

  useEffect(() => {
    if (isOpen) document.body.style.overflow = 'hidden';
    else document.body.style.overflow = 'unset';
    return () => { document.body.style.overflow = 'unset'; };
  }, [isOpen]);

  useEffect(() => {
    if (!isOpen || visitRequestId == null) return;
    let active = true;
    setLoading(true);
    setError(null);
    setData(null);
    delegationsApi.getSubmittedVisitRequestFormDetail(visitRequestId)
      .then((res) => { if (active) setData(res); })
      .catch((e) => { if (active) setError(getFriendlyError(e)); })
      .finally(() => { if (active) setLoading(false); });
    return () => { active = false; };
  }, [isOpen, visitRequestId]);

  if (!isOpen) return null;

  const scopeLabel = data ? (VISIT_SCOPE_LABELS[data.visitScope] ?? data.visitScope) : '';
  const isRejected = data?.requestStatus === 'REJECTED';
  const isCancelled = data?.requestStatus === 'CANCELLED';

  return (
    <AnimatePresence>
      <motion.div
        initial={{ opacity: 0 }}
        animate={{ opacity: 1 }}
        exit={{ opacity: 0 }}
        className="fixed inset-0 z-[100] bg-black/60 backdrop-blur-sm flex items-center justify-center p-3 sm:p-6"
        onClick={onClose}
      >
        <motion.div
          initial={{ opacity: 0, scale: 0.95, y: 20 }}
          animate={{ opacity: 1, scale: 1, y: 0 }}
          exit={{ opacity: 0, scale: 0.95, y: 20 }}
          transition={{ duration: 0.25, ease: 'easeOut' }}
          onClick={(e) => e.stopPropagation()}
          className="bg-white w-full max-w-5xl max-h-[92vh] rounded-2xl shadow-2xl flex flex-col overflow-hidden relative border border-gray-100"
        >
          {/* Header */}
          <div className="flex-none px-6 py-5 sm:px-10 flex items-start justify-between text-white bg-[#004c91]">
            <div className="pr-8">
              <h2 className="text-xl sm:text-2xl font-black tracking-tight mb-1">{headerTitle(data?.requestStatus)}</h2>
              <p className="text-white/80 font-medium text-xs sm:text-sm">Thông tin khách đã gửi trong đơn</p>
              {data && (
                <div className="mt-3 flex flex-wrap gap-2 text-xs font-semibold">
                  <span className="inline-flex items-center rounded-full bg-white/15 px-3 py-1">Mã đơn: {data.requestCode || '-'}</span>
                  <span className={`inline-flex items-center rounded-full px-3 py-1 ${
                    isRejected ? 'bg-red-400/90 text-red-950'
                    : data.requestStatus === 'PENDING_APPROVAL' ? 'bg-yellow-400/90 text-yellow-950'
                    : isCancelled ? 'bg-slate-300/90 text-slate-800'
                    : 'bg-emerald-400/90 text-emerald-950'
                  }`}>{statusLabel(data.requestStatus)}</span>
                  <span className="inline-flex items-center rounded-full bg-white/15 px-3 py-1">Phạm vi: {scopeLabel}</span>
                  <span className="inline-flex items-center rounded-full bg-white/15 px-3 py-1">Ngày gửi: {formatDateTime(data.submittedAt)}</span>
                </div>
              )}
            </div>
            <button
              type="button"
              onClick={onClose}
              aria-label="Đóng"
              className="absolute top-4 right-4 sm:top-5 sm:right-6 p-2 text-white/70 hover:text-white hover:bg-white/20 rounded-full transition-all"
            >
              <X className="w-5 h-5 sm:w-6 sm:h-6" />
            </button>
          </div>

          {/* Body */}
          <div className="flex-1 overflow-y-auto px-4 sm:px-10 py-8 bg-white">
            {loading ? (
              <div className="py-20 flex flex-col items-center justify-center text-slate-500">
                <Loader2 className="w-10 h-10 animate-spin text-[#004c91] mb-3" />
                <p className="font-medium">Đang tải thông tin đơn...</p>
              </div>
            ) : error ? (
              <div className="py-20 flex flex-col items-center justify-center text-center">
                <AlertCircle className="w-12 h-12 text-red-400 mb-3" />
                <p className="font-semibold text-red-600 max-w-md">{error}</p>
              </div>
            ) : data ? (
              <div className="space-y-10">
                {/* Lý do từ chối (pre-approval reject) — decision_note, never cancellation_reason. */}
                {isRejected && data.decisionNote && (
                  <DecisionReasonPanel
                    decisionActorRole={data.decisionActorRole}
                    decidedByName={data.decidedByName}
                    decidedByUserId={data.decidedByUserId}
                    decidedAt={data.decidedAt}
                    decisionNote={data.decisionNote}
                  />
                )}

                {/* Lý do hủy — whole-request cancel (one panel covers all campuses). */}
                {isCancelled && (
                  <CancellationReasonPanel
                    cancellationLevel={data.cancellationLevel ?? 'REQUEST'}
                    cancelledByName={data.cancelledByName}
                    cancelledByUserId={data.cancelledByUserId}
                    cancelledAt={data.cancelledAt}
                    cancellationActorType={data.cancellationActorType}
                    cancellationSource={data.cancellationSource}
                    cancellationReason={data.cancellationReason}
                  />
                )}

                {/* Lý do hủy — instance-level cancel while the request is still active. */}
                {!isCancelled &&
                  data.campuses
                    .filter((c) => c.instanceStatus === 'CANCELLED')
                    .map((c) => (
                      <CancellationReasonPanel
                        key={c.visitInstanceId}
                        cancellationLevel="CAMPUS_INSTANCE"
                        contextLabel={c.campusName || c.campusCode || null}
                        cancelledByName={c.cancelledByName}
                        cancelledByUserId={c.cancelledByUserId}
                        cancelledAt={c.cancelledAt}
                        cancellationActorType={c.cancellationActorType}
                        cancellationSource={c.cancellationSource}
                        cancellationReason={c.cancellationReason}
                      />
                    ))}

                <SubmittedVisitRequestInfoPanel data={data} />
              </div>
            ) : null}
          </div>

          {/* Footer */}
          <div className="px-6 py-4 bg-gray-50 border-t border-gray-100 flex items-center justify-end gap-3">
            {data?.canReject && (
              <button
                type="button"
                onClick={() => onReject?.(data)}
                className="px-5 py-2.5 rounded-xl font-bold text-white bg-red-600 hover:bg-red-700 shadow-sm transition-colors outline-none text-sm inline-flex items-center gap-2"
              >
                <X className="w-4 h-4" /> Từ chối
              </button>
            )}
            {data?.canApprove && (
              <button
                type="button"
                onClick={() => onApprove?.(data)}
                className="px-5 py-2.5 rounded-xl font-bold text-white bg-green-600 hover:bg-green-700 shadow-sm transition-colors outline-none text-sm inline-flex items-center gap-2"
              >
                <Check className="w-4 h-4" /> Duyệt
              </button>
            )}
            {data?.canAssignHost && (
              <button
                type="button"
                onClick={() => onAssignHost?.(data)}
                className="px-5 py-2.5 rounded-xl font-bold text-white bg-[#004c91] hover:bg-[#00386b] shadow-sm transition-colors outline-none text-sm inline-flex items-center gap-2"
              >
                <UserCog className="w-4 h-4" /> Gán Host
              </button>
            )}
            <button
              type="button"
              onClick={onClose}
              className="px-5 py-2.5 rounded-xl font-bold text-gray-600 bg-white border border-gray-200 hover:bg-gray-100 transition-colors shadow-sm outline-none text-sm"
            >
              Đóng
            </button>
          </div>
        </motion.div>
      </motion.div>
    </AnimatePresence>
  );
}
