/**
 * CancellationReasonModal — a small popup that shows only the "Lý do hủy" of a cancelled visit
 * request / campus instance (UC-136). Used from the management list so a Host (or any in-scope
 * role) can review the cancellation reason without opening the full detail.
 *
 * It renders the shared CancellationReasonPanel, so the labels/formatters stay identical to the
 * panel shown inside the detail modal.
 */

import { X } from 'lucide-react';
import { motion } from 'motion/react';
import { CancellationReasonPanel } from './CancellationReasonPanel';

export interface CancellationReasonModalProps {
  isOpen: boolean;
  onClose: () => void;
  /** Title context, e.g. the delegation name. */
  delegationName?: string | null;
  cancellationLevel?: string | null;
  cancelledByName?: string | null;
  cancelledByUserId?: number | null;
  cancelledAt?: string | null;
  cancellationActorType?: string | null;
  cancellationSource?: string | null;
  cancellationReason?: string | null;
  /** Optional context line for the panel (e.g. campus name). */
  contextLabel?: string | null;
}

export function CancellationReasonModal({
  isOpen, onClose, delegationName,
  cancellationLevel, cancelledByName, cancelledByUserId, cancelledAt,
  cancellationActorType, cancellationSource, cancellationReason, contextLabel,
}: CancellationReasonModalProps) {
  if (!isOpen) return null;

  return (
    <div className="fixed inset-0 z-[110] flex items-center justify-center bg-slate-900/40 backdrop-blur-sm p-4" onClick={onClose}>
      <motion.div
        initial={{ opacity: 0, scale: 0.95, y: 10 }}
        animate={{ opacity: 1, scale: 1, y: 0 }}
        onClick={(e) => e.stopPropagation()}
        className="bg-white rounded-3xl shadow-2xl w-full max-w-md overflow-hidden relative border border-gray-100"
      >
        <div className="px-6 py-4 bg-[#004c91] flex items-center justify-between">
          <h3 className="text-lg font-bold text-white">Lý do hủy</h3>
          <button type="button" onClick={onClose} aria-label="Đóng" className="text-white/85 hover:text-white hover:bg-white/10 rounded-full p-1.5 cursor-pointer">
            <X className="w-5 h-5" />
          </button>
        </div>
        <div className="p-5 space-y-3 text-left">
          {delegationName ? (
            <div>
              <span className="text-[11px] uppercase tracking-wider font-bold text-gray-400">Đoàn khách</span>
              <p className="text-base font-bold text-slate-800 mt-0.5">{delegationName}</p>
            </div>
          ) : null}
          <CancellationReasonPanel
            cancellationLevel={cancellationLevel}
            cancelledByName={cancelledByName}
            cancelledByUserId={cancelledByUserId}
            cancelledAt={cancelledAt}
            cancellationActorType={cancellationActorType}
            cancellationSource={cancellationSource}
            cancellationReason={cancellationReason}
            contextLabel={contextLabel}
          />
        </div>
        <div className="px-6 py-4 bg-gray-50 flex items-center justify-end border-t border-gray-100">
          <button type="button" onClick={onClose} className="px-6 py-2 rounded-xl font-bold text-white bg-[#004c91] hover:bg-[#00386b] shadow-sm transition-all outline-none text-sm cursor-pointer">Đóng</button>
        </div>
      </motion.div>
    </div>
  );
}
