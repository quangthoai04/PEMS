/**
 * CancellationReasonPanel — shows the "Lý do hủy" (post-approval cancellation) of a visit request
 * or a single campus instance (UC-136).
 *
 * Render it only when something is actually cancelled (isCancelled, or a CANCELLED request/campus).
 * The cancel reason is read from cancellation_reason (NEVER decision_note). Backend already
 * enforces who may see it; the frontend only renders what it receives.
 */

import { XCircle } from 'lucide-react';

const formatDateTime = (value?: string | null) => {
  if (!value) return '-';
  const d = new Date(value);
  if (Number.isNaN(d.getTime())) return '-';
  return d.toLocaleString('vi-VN', {
    hour: '2-digit', minute: '2-digit', day: '2-digit', month: '2-digit', year: 'numeric',
  });
};

const levelLabel = (level?: string | null) => {
  if (level === 'REQUEST') return 'Toàn bộ đơn';
  if (level === 'CAMPUS_INSTANCE') return 'Cơ sở hiện tại';
  return '-';
};

const actorTypeLabel = (actor?: string | null) => {
  if (actor === 'VISITOR') return 'Khách (Visitor)';
  if (actor === 'HOST') return 'Host';
  return actor || '-';
};

const sourceLabel = (source?: string | null) => {
  if (source === 'SELF_SERVICE') return 'Tự hủy';
  if (source === 'EXTERNAL_CONFIRMATION') return 'Xác nhận ngoài hệ thống';
  return source || '-';
};

export interface CancellationReasonPanelProps {
  cancellationLevel?: string | null;
  cancelledByName?: string | null;
  cancelledByUserId?: number | null;
  cancelledAt?: string | null;
  cancellationActorType?: string | null;
  cancellationSource?: string | null;
  cancellationReason?: string | null;
  /** Optional context line, e.g. the campus name for an instance-level cancel. */
  contextLabel?: string | null;
}

export function CancellationReasonPanel({
  cancellationLevel, cancelledByName, cancelledByUserId, cancelledAt,
  cancellationActorType, cancellationSource, cancellationReason, contextLabel,
}: CancellationReasonPanelProps) {
  const cancelledBy = cancelledByName || (cancelledByUserId ? `#${cancelledByUserId}` : '-');

  return (
    <div className="rounded-2xl border border-red-200 bg-red-50 p-5">
      <h3 className="text-sm font-black text-red-700 uppercase tracking-wide mb-3 flex items-center gap-2">
        <XCircle className="w-4 h-4 text-red-500" /> Lý do hủy{contextLabel ? ` — ${contextLabel}` : ''}
      </h3>
      <div className="grid grid-cols-1 md:grid-cols-3 gap-4 mb-3">
        <div>
          <p className="text-xs font-bold text-slate-500">Cấp hủy</p>
          <p className="mt-0.5 text-sm font-semibold text-slate-900">{levelLabel(cancellationLevel)}</p>
        </div>
        <div>
          <p className="text-xs font-bold text-slate-500">Người hủy</p>
          <p className="mt-0.5 text-sm font-semibold text-slate-900">{cancelledBy}</p>
        </div>
        <div>
          <p className="text-xs font-bold text-slate-500">Thời gian hủy</p>
          <p className="mt-0.5 text-sm font-semibold text-slate-900">{formatDateTime(cancelledAt)}</p>
        </div>
        <div>
          <p className="text-xs font-bold text-slate-500">Vai trò hủy</p>
          <p className="mt-0.5 text-sm font-semibold text-slate-900">{actorTypeLabel(cancellationActorType)}</p>
        </div>
        <div>
          <p className="text-xs font-bold text-slate-500">Nguồn hủy</p>
          <p className="mt-0.5 text-sm font-semibold text-slate-900">{sourceLabel(cancellationSource)}</p>
        </div>
      </div>
      <div className="rounded-xl border border-slate-200 bg-white px-4 py-3">
        <p className="text-xs font-bold text-slate-500">Nội dung</p>
        <p className="mt-1 text-sm font-semibold text-slate-900 whitespace-pre-wrap">
          {cancellationReason || 'Chưa ghi nhận lý do hủy.'}
        </p>
      </div>
    </div>
  );
}
