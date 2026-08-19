/**
 * RejectedReasonModal — a small popup that shows the "Lý do từ chối" (pre-approval rejection)
 * of a visit request from the management list, without opening the full detail. It surfaces the
 * delegation/request context (đoàn khách, mã đơn, phạm vi) plus the shared DecisionReasonPanel
 * (vai trò xử lý / người xử lý / thời gian xử lý / nội dung).
 *
 * The reject reason is read from visit_requests.decision_note (NEVER cancellation_reason).
 * Backend already enforces who may see a rejected row (Visitor owner / HO multi-campus /
 * Staff Leader own single-campus); this popup only renders what the list returned.
 */

import { X } from 'lucide-react';
import { motion } from 'motion/react';
import { DecisionReasonPanel } from './DecisionReasonPanel';
import { VISIT_SCOPE_LABELS } from '../types/delegations.types';

export interface RejectedReasonModalProps {
  isOpen: boolean;
  onClose: () => void;
  delegationName?: string | null;
  requestCode?: string | null;
  visitScope?: string | null;
  decisionActorRole?: string | null;
  decidedByName?: string | null;
  decidedByUserId?: number | null;
  decidedAt?: string | null;
  decisionNote?: string | null;
  contextLabel?: string | null;
}

const MetaRow = ({ label, value }: { label: string; value?: string | null }) => (
  <div>
    <span className="text-[11px] uppercase tracking-wider font-bold text-gray-400">{label}</span>
    <p className="text-base font-normal text-slate-800 mt-0.5">{value || '-'}</p>
  </div>
);

export function RejectedReasonModal({
  isOpen, onClose, delegationName, requestCode, visitScope,
  decisionActorRole, decidedByName, decidedByUserId, decidedAt, decisionNote, contextLabel,
}: RejectedReasonModalProps) {
  if (!isOpen) return null;

  const scopeLabel = visitScope ? (VISIT_SCOPE_LABELS[visitScope] ?? visitScope) : null;

  return (
    <div className="fixed inset-0 z-[110] flex items-center justify-center bg-slate-900/40 backdrop-blur-sm p-4" onClick={onClose}>
      <motion.div
        initial={{ opacity: 0, scale: 0.95, y: 10 }}
        animate={{ opacity: 1, scale: 1, y: 0 }}
        onClick={(e) => e.stopPropagation()}
        className="bg-white rounded-3xl shadow-2xl w-full max-w-md overflow-hidden relative border border-gray-100 flex flex-col max-h-[calc(100dvh-2rem)]"
      >
        <div className="px-6 py-4 bg-[#004c91] flex items-center justify-between shrink-0">
          <h3 className="text-lg font-bold text-white">Lý do từ chối</h3>
          <button type="button" onClick={onClose} aria-label="Đóng" className="text-white/85 hover:text-white hover:bg-white/10 rounded-full p-1.5 cursor-pointer">
            <X className="w-5 h-5" />
          </button>
        </div>
        {/* Was a flat max-h-[70vh] on the body alone -- header+body+footer could still exceed the
            visible viewport on a short screen since the body cap wasn't gutter-aware. Modal is now
            flex-col + dvh-clamped as a whole, same pattern as CancellationReasonModal. */}
        <div className="p-5 space-y-3 text-left overflow-y-auto min-h-0">
          {delegationName ? <MetaRow label="Đoàn khách" value={delegationName} /> : null}
          <div className="grid grid-cols-2 gap-3">
            <MetaRow label="Mã đơn" value={requestCode} />
            {scopeLabel ? <MetaRow label="Phạm vi" value={scopeLabel} /> : null}
            {contextLabel ? <MetaRow label="Cơ sở" value={contextLabel} /> : null}
          </div>
          <DecisionReasonPanel
            compact
            decisionActorRole={decisionActorRole}
            decidedByName={decidedByName}
            decidedByUserId={decidedByUserId}
            decidedAt={decidedAt}
            decisionNote={decisionNote}
          />
        </div>
        <div className="px-6 py-4 bg-gray-50 flex items-center justify-end border-t border-gray-100 shrink-0">
          <button type="button" onClick={onClose} className="px-6 py-2 rounded-xl font-bold text-white bg-[#004c91] hover:bg-[#00386b] shadow-sm transition-all outline-none text-sm cursor-pointer">Đóng</button>
        </div>
      </motion.div>
    </div>
  );
}
