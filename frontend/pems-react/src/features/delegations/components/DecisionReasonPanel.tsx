/**
 * DecisionReasonPanel — shows the "Lý do từ chối" (pre-approval rejection) of a visit request.
 *
 * Render it only when the request is REJECTED and a decisionNote exists. The reject reason is
 * read from visit_requests.decision_note (NEVER cancellation_reason). Backend already enforces
 * who may see it (Visitor owner / HO multi-campus / Staff Leader own single-campus).
 */

import { AlertCircle } from 'lucide-react';

import { formatVietnamDateTime } from '../../../shared/utils/vietnamTime';
const formatDateTime = (value?: string | null) => {
  return formatVietnamDateTime(value, { fallback: '-' });
};

const decisionRoleLabel = (role?: string | null) => {
  if (!role) return '-';
  if (role === 'HO') return 'Head Office (HO)';
  if (role === 'STAFF_LEADER' || role === 'STAFF') return 'Trưởng IC (Staff Leader)';
  return role;
};

export interface DecisionReasonPanelProps {
  decisionActorRole?: string | null;
  decidedByName?: string | null;
  decidedByUserId?: number | null;
  decidedAt?: string | null;
  decisionNote?: string | null;
  /** Stack the three meta fields vertically (for narrow popups). */
  compact?: boolean;
}

export function DecisionReasonPanel({
  decisionActorRole, decidedByName, decidedByUserId, decidedAt, decisionNote, compact = false,
}: DecisionReasonPanelProps) {
  const processedBy = decidedByName || (decidedByUserId ? `#${decidedByUserId}` : '-');

  return (
    <div className="rounded-2xl border border-red-200 bg-red-50/70 p-5">
      <h3 className="text-sm font-black text-red-700 uppercase tracking-wide mb-3 flex items-center gap-2">
        <AlertCircle className="w-4 h-4" /> Lý do từ chối
      </h3>
      <div className={`grid gap-4 mb-3 ${compact ? 'grid-cols-1' : 'grid-cols-1 md:grid-cols-3'}`}>
        <div>
          <p className="text-xs font-bold text-slate-500">Vai trò xử lý</p>
          <p className="mt-0.5 text-sm font-normal text-slate-900">{decisionRoleLabel(decisionActorRole)}</p>
        </div>
        <div>
          <p className="text-xs font-bold text-slate-500">Người xử lý</p>
          <p className="mt-0.5 text-sm font-normal text-slate-900">{processedBy}</p>
        </div>
        <div>
          <p className="text-xs font-bold text-slate-500">Thời gian xử lý</p>
          <p className="mt-0.5 text-sm font-normal text-slate-900">{formatDateTime(decidedAt)}</p>
        </div>
      </div>
      <div className="rounded-xl border border-red-100 bg-white px-4 py-3">
        <p className="text-xs font-bold text-slate-500">Nội dung</p>
        <p className="mt-1 text-sm font-normal text-red-950 whitespace-pre-wrap italic">
          {decisionNote || 'Không có lý do chi tiết.'}
        </p>
      </div>
    </div>
  );
}
