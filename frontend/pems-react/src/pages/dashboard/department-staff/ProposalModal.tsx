/**
 * ProposalModal – Modal đề xuất thay đổi (giờ sử dụng + ghi chú) cho nhiệm vụ logistics.
 * Mở thẳng từ dòng item, không cần qua modal chi tiết; lỗi thì giữ nguyên dữ liệu đã nhập.
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

function toTimeInput(iso?: string) {
  if (!iso) return '';
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) return '';
  return `${String(d.getHours()).padStart(2, '0')}:${String(d.getMinutes()).padStart(2, '0')}`;
}

export function ProposalModal({ item, onClose, onSuccess }: Props) {
  const [start, setStart] = useState('');
  const [end, setEnd] = useState('');
  const [note, setNote] = useState('');
  const [loading, setLoading] = useState(false);

  useEffect(() => {
    setStart(toTimeInput(item?.startAt));
    setEnd(toTimeInput(item?.endAt));
    setNote('');
  }, [item?.itemType, item?.itemId]);

  if (!item) return null;

  const handleSubmit = async () => {
    if (!note.trim()) { toast.error('Vui lòng nhập ghi chú đề xuất'); return; }
    if (!start || !end) { toast.error('Vui lòng chọn giờ bắt đầu và giờ kết thúc'); return; }
    if (end <= start) { toast.error('Giờ kết thúc phải sau giờ bắt đầu'); return; }
    const baseDate = (item.startAt || '').slice(0, 10);
    setLoading(true);
    try {
      await departmentReceptionTasksApi.proposeChange(item.logisticsItemId || item.itemId, {
        proposedUsageStartAt: `${baseDate}T${start}:00`,
        proposedUsageEndAt: `${baseDate}T${end}:00`,
        proposalNote: note.trim(),
        proposedDescription: note.trim(),
      });
      toast.success('Đã gửi đề xuất thay đổi');
      onSuccess();
      onClose();
    } catch (e: any) {
      toast.error(e?.response?.data?.message || 'Gửi đề xuất thất bại');
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="fixed inset-0 z-50 bg-slate-900/40 flex items-center justify-center p-4" onClick={onClose}>
      <div className="w-full max-w-md bg-white rounded-2xl shadow-2xl border border-slate-200 overflow-hidden" onClick={e => e.stopPropagation()}>
        <div className="px-5 py-3.5 border-b border-slate-100 flex items-center justify-between">
          <div className="min-w-0">
            <h3 className="text-sm font-black text-[#004c91]">Đề xuất thay đổi</h3>
            <p className="text-xs text-slate-500 mt-0.5 truncate">{item.delegationName ? `${item.delegationName} — ` : ''}{item.title}</p>
          </div>
          <button onClick={onClose} className="w-8 h-8 rounded-full hover:bg-slate-100 flex items-center justify-center text-slate-400 shrink-0">
            <X className="w-4 h-4" />
          </button>
        </div>
        <div className="p-5 space-y-3">
          <div className="grid grid-cols-2 gap-3">
            <div>
              <label className="block text-[11px] font-black text-slate-500 uppercase tracking-wide mb-1">Giờ bắt đầu mới</label>
              <input type="time" value={start} onChange={e => setStart(e.target.value)}
                className="w-full px-3 py-2 text-xs border border-slate-200 rounded-xl outline-none focus:border-blue-400" />
            </div>
            <div>
              <label className="block text-[11px] font-black text-slate-500 uppercase tracking-wide mb-1">Giờ kết thúc mới</label>
              <input type="time" value={end} onChange={e => setEnd(e.target.value)}
                className="w-full px-3 py-2 text-xs border border-slate-200 rounded-xl outline-none focus:border-blue-400" />
            </div>
          </div>
          <div>
            <label className="block text-[11px] font-black text-slate-500 uppercase tracking-wide mb-1">Ghi chú đề xuất *</label>
            <textarea rows={3} value={note} onChange={e => setNote(e.target.value)}
              placeholder="Mô tả lý do và nội dung thay đổi đề xuất..."
              className="w-full px-3 py-2 text-sm border border-slate-200 rounded-xl outline-none focus:border-blue-400 resize-none" />
          </div>
          <div className="flex gap-2 pt-1">
            <button onClick={handleSubmit} disabled={loading || !note.trim() || !start || !end}
              className="flex-1 py-2.5 bg-amber-600 hover:bg-amber-700 text-white text-sm font-black rounded-xl transition-colors disabled:opacity-50">
              {loading ? 'Đang gửi...' : 'Gửi đề xuất'}
            </button>
            <button onClick={onClose} className="px-4 py-2.5 text-sm font-bold text-slate-600 bg-slate-100 rounded-xl hover:bg-slate-200">Hủy</button>
          </div>
        </div>
      </div>
    </div>
  );
}
