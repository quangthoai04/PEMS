/**
 * DeclineReasonModal – Modal nhập lý do từ chối (thư mời / nhiệm vụ logistics).
 * Mở trực tiếp từ dòng item, submit gọi API luôn; lỗi thì giữ nguyên nội dung đã nhập.
 */
import React, { useEffect, useState } from 'react';
import { X } from 'lucide-react';
import toast from 'react-hot-toast';
import { departmentReceptionTasksApi } from '../../../features/department-reception-tasks/api/departmentReceptionTasksApi';
import type { AssignedTask } from './useDeptStaffData';

interface Props {
  item: AssignedTask | null;
  onClose: () => void;
  onSuccess: () => void;
}

export function DeclineReasonModal({ item, onClose, onSuccess }: Props) {
  const [reason, setReason] = useState('');
  const [loading, setLoading] = useState(false);

  useEffect(() => { setReason(''); }, [item?.itemType, item?.itemId]);

  if (!item) return null;

  const handleSubmit = async () => {
    if (!reason.trim()) { toast.error('Vui lòng nhập lý do từ chối'); return; }
    setLoading(true);
    try {
      if (item.itemType === 'INVITATION') {
        await departmentReceptionTasksApi.declineInvitation(item.participantId || item.itemId, reason.trim());
        toast.success('Đã từ chối thư mời');
      } else {
        await departmentReceptionTasksApi.declineAssignment(item.logisticsItemId || item.itemId, reason.trim());
        toast.success('Đã từ chối nhiệm vụ');
      }
      onSuccess();
      onClose();
    } catch (e: any) {
      toast.error(e?.response?.data?.message || 'Từ chối thất bại');
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="fixed inset-0 z-50 bg-slate-900/40 flex items-center justify-center p-4" onClick={onClose}>
      <div className="w-full max-w-md bg-white rounded-2xl shadow-2xl border border-slate-200 overflow-hidden" onClick={e => e.stopPropagation()}>
        <div className="px-5 py-3.5 border-b border-slate-100 flex items-center justify-between">
          <div className="min-w-0">
            <h3 className="text-sm font-black text-rose-600">Từ chối {item.itemType === 'INVITATION' ? 'thư mời' : 'nhiệm vụ'}</h3>
            <p className="text-xs text-slate-500 mt-0.5 truncate">{item.delegationName ? `${item.delegationName} — ` : ''}{item.title}</p>
          </div>
          <button onClick={onClose} className="w-8 h-8 rounded-full hover:bg-slate-100 flex items-center justify-center text-slate-400 shrink-0">
            <X className="w-4 h-4" />
          </button>
        </div>
        <div className="p-5 space-y-3">
          <div>
            <label className="block text-[11px] font-black text-slate-500 uppercase tracking-wide mb-1">Lý do từ chối *</label>
            <textarea rows={3} value={reason} onChange={e => setReason(e.target.value)} autoFocus
              placeholder="Nhập lý do từ chối..."
              className="w-full px-3 py-2 text-sm border border-slate-200 rounded-xl outline-none focus:border-rose-400 resize-none" />
          </div>
          <div className="flex gap-2">
            <button onClick={handleSubmit} disabled={loading || !reason.trim()}
              className="flex-1 py-2.5 bg-rose-600 hover:bg-rose-700 text-white text-sm font-black rounded-xl transition-colors disabled:opacity-50">
              {loading ? 'Đang gửi...' : 'Xác nhận từ chối'}
            </button>
            <button onClick={onClose} className="px-4 py-2.5 text-sm font-bold text-slate-600 bg-slate-100 rounded-xl hover:bg-slate-200">Hủy</button>
          </div>
        </div>
      </div>
    </div>
  );
}
