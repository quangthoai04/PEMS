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

const decisionRoleLabel = (role?: string | null) => {
  if (!role) return '-';
  if (role === 'HO') return 'Head Office (HO)';
  if (role === 'STAFF_LEADER' || role === 'STAFF') return 'Trưởng IC (Staff Leader)';
  return role;
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
                {/* Rejection / cancellation info (shown above the form for decided requests) */}
                {isRejected && (
                  <div className="rounded-2xl border border-red-200 bg-red-50/70 p-5">
                    <h3 className="text-sm font-black text-red-700 uppercase tracking-wide mb-3 flex items-center gap-2">
                      <AlertCircle className="w-4 h-4" /> Thông tin từ chối
                    </h3>
                    <div className="grid grid-cols-1 md:grid-cols-3 gap-4 mb-3">
                      <div>
                        <p className="text-xs font-bold text-slate-500">Người từ chối</p>
                        <p className="mt-0.5 text-sm font-semibold text-slate-900">{data.decidedByName || '-'}</p>
                      </div>
                      <div>
                        <p className="text-xs font-bold text-slate-500">Vai trò</p>
                        <p className="mt-0.5 text-sm font-semibold text-slate-900">{decisionRoleLabel(data.decisionActorRole)}</p>
                      </div>
                      <div>
                        <p className="text-xs font-bold text-slate-500">Thời gian</p>
                        <p className="mt-0.5 text-sm font-semibold text-slate-900">{formatDateTime(data.decidedAt)}</p>
                      </div>
                    </div>
                    <div className="rounded-xl border border-red-100 bg-white px-4 py-3">
                      <p className="text-xs font-bold text-slate-500">Lý do từ chối</p>
                      <p className="mt-1 text-sm font-semibold text-red-950 whitespace-pre-wrap italic">
                        {data.decisionNote || 'Không có lý do chi tiết.'}
                      </p>
                    </div>
                  </div>
                )}
                {isCancelled && (
                  <div className="rounded-2xl border border-slate-300 bg-slate-50 p-5">
                    <h3 className="text-sm font-black text-slate-700 uppercase tracking-wide mb-3">Thông tin hủy</h3>
                    <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                      <div>
                        <p className="text-xs font-bold text-slate-500">Thời gian hủy</p>
                        <p className="mt-0.5 text-sm font-semibold text-slate-900">{formatDateTime(data.cancelledAt)}</p>
                      </div>
                      <div>
                        <p className="text-xs font-bold text-slate-500">Lý do hủy</p>
                        <p className="mt-0.5 text-sm font-semibold text-slate-900 whitespace-pre-wrap">{data.cancellationReason || '-'}</p>
                      </div>
                    </div>
                  </div>
                )}

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
