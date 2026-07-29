/**
 * ProposalModal – Modal đề xuất thay đổi (giờ sử dụng + ghi chú) cho nhiệm vụ logistics.
 * Mở thẳng từ dòng item, không cần qua modal chi tiết; lỗi thì giữ nguyên dữ liệu đã nhập.
 */
import React, { useEffect, useState } from 'react';
import { X } from 'lucide-react';
import toast from 'react-hot-toast';
import { departmentReceptionTasksApi } from '../../../features/department-reception-tasks/api/departmentReceptionTasksApi';
import type { AssignedTask } from './useDeptStaffData';
import { toVietnamDateTimeLocalInput } from '../../../shared/utils/vietnamTime';

interface Props {
  item: AssignedTask | null;
  onClose: () => void;
  onSuccess: () => void;
}

function parseHHMM(val?: string) {
  if (!val) return '';
  if (val.includes('T')) {
    const local = toVietnamDateTimeLocalInput(val);
    return local ? local.slice(11, 16) : '';
  }
  const match = val.match(/(\d{2}:\d{2})/);
  if (match) return match[1];
  return '';
}

// "HH:mm dd/MM/yyyy" theo giờ Việt Nam từ 1 mốc ISO (không dùng new Date()/toISOString — giữ
// nguyên wall-clock đã có offset +07:00 sẵn trong chuỗi).
function fmtDateTime(iso?: string | null): string {
  const local = toVietnamDateTimeLocalInput(iso || undefined);
  if (!local) return '';
  const [d, t] = local.split('T');
  const [y, m, day] = d.split('-');
  return `${t} ${day}/${m}/${y}`;
}

// Khoảng thời gian đọc được: gộp 1 ngày nếu cùng ngày, tách rõ 2 mốc nếu khác ngày.
function fmtRange(startIso?: string | null, endIso?: string | null): string {
  const s = toVietnamDateTimeLocalInput(startIso || undefined);
  const e = toVietnamDateTimeLocalInput(endIso || undefined);
  if (!s || !e) return 'Chưa thiết lập';
  if (s.slice(0, 10) === e.slice(0, 10)) {
    const [d, t1] = s.split('T');
    const [, t2] = e.split('T');
    const [y, m, day] = d.split('-');
    return `${t1} - ${t2}, ${day}/${m}/${y}`;
  }
  return `${fmtDateTime(startIso)} - ${fmtDateTime(endIso)}`;
}

export function ProposalModal({ item, onClose, onSuccess }: Props) {
  const [start, setStart] = useState('');
  const [end, setEnd] = useState('');
  const [quantity, setQuantity] = useState('');
  const [content, setContent] = useState('');
  const [note, setNote] = useState('');
  const [loading, setLoading] = useState(false);
  const [detail, setDetail] = useState<any>(null);

  // Đoàn khách diễn ra nhiều ngày → 2 ô chọn cần cả ngày lẫn giờ; trong 1 ngày → chỉ cần giờ.
  const startDatePart = toVietnamDateTimeLocalInput(item?.startAt || undefined)?.slice(0, 10);
  const endDatePart = toVietnamDateTimeLocalInput(item?.endAt || undefined)?.slice(0, 10);
  const isMultiDay = !!(startDatePart && endDatePart && startDatePart !== endDatePart);

  // "Chốt": số lượng đã được Host CHẤP NHẬN đề xuất thay quantity gốc — hiển thị số này ở ô "Số
  // lượng" thông tin (số dự kiến gốc không đổi, chỉ dùng để tính ràng buộc "đề xuất mới < X").
  const finalQuantity = detail?.proposalResponse === 'ACCEPTED' && detail?.proposedQuantity != null
    ? detail.proposedQuantity : detail?.quantity;
  const quantityTooHigh = quantity.trim() !== '' && detail?.quantity != null && Number(quantity) >= detail.quantity;

  useEffect(() => {
    if (isMultiDay) {
      setStart(toVietnamDateTimeLocalInput(item?.startAt || undefined) || '');
      setEnd(toVietnamDateTimeLocalInput(item?.endAt || undefined) || '');
    } else {
      const startVal = parseHHMM(item?.startAt || item?.time?.split('-')[0]?.trim());
      const endVal = parseHHMM(item?.endAt || item?.time?.split('-')[1]?.trim());
      setStart(startVal || '08:00');
      setEnd(endVal || '12:00');
    }
    setQuantity('');
    setContent('');
    setNote('');
    setDetail(null);

    const targetId = item?.logisticsItemId || item?.itemId;
    if (!targetId) return;
    (async () => {
      try {
        const data = await departmentReceptionTasksApi.getRequestDetail(targetId);
        setDetail(data);
        // Không seed từ quantity gốc — số lượng đề xuất phải NHỎ HƠN số lượng dự kiến, để trống
        // mặc định, chỉ khôi phục đề xuất dở dang nếu có.
        if (data?.proposedQuantity != null) setQuantity(String(data.proposedQuantity));
      } catch {
        // giữ form trống, không chặn thao tác đề xuất nếu không tải được chi tiết
      }
    })();
  }, [item?.itemType, item?.itemId, item?.startAt, item?.endAt, item?.time, isMultiDay]);

  if (!item) return null;

  const handleSubmit = async () => {
    if (loading) return; // chặn double-submit khi bấm liên tục
    if (!note.trim()) { toast.error('Vui lòng nhập lý do đề xuất'); return; }
    if (!start || !end) {
      toast.error(isMultiDay ? 'Vui lòng chọn đầy đủ ngày giờ bắt đầu và kết thúc' : 'Vui lòng chọn giờ bắt đầu và giờ kết thúc');
      return;
    }
    if (end <= start) { toast.error('Thời gian kết thúc phải sau thời gian bắt đầu'); return; }
    const qty = quantity.trim() ? Number(quantity) : null;
    if (qty != null && (!Number.isInteger(qty) || qty < 0)) {
      toast.error('Số lượng đề xuất phải là số nguyên ≥ 0');
      return;
    }
    if (qty != null && detail?.quantity != null && qty >= detail.quantity) {
      toast.error(`Số lượng đề xuất phải nhỏ hơn số lượng dự kiến (${detail.quantity})`);
      return;
    }
    const baseDate = (item.startAt || item.date || '').slice(0, 10) || new Date().toISOString().slice(0, 10);
    setLoading(true);
    try {
      await departmentReceptionTasksApi.proposeChange(item.logisticsItemId || item.itemId, {
        proposedQuantity: qty,
        proposedUsageStartAt: isMultiDay ? `${start}:00` : `${baseDate}T${start}:00`,
        proposedUsageEndAt: isMultiDay ? `${end}:00` : `${baseDate}T${end}:00`,
        proposalNote: note.trim(),
        proposedDescription: content.trim() || undefined,
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
      <div className="w-full max-w-lg bg-white rounded-2xl shadow-2xl border border-slate-200 overflow-hidden" onClick={e => e.stopPropagation()}>
        <div className="px-5 py-3.5 bg-[#004c91] text-white flex items-center justify-between">
          <div className="min-w-0">
            <h3 className="text-base font-black">Đề xuất thay đổi</h3>
            <p className="text-xs text-white/80 mt-0.5 truncate">{item.delegationName || item.title}</p>
          </div>
          <button onClick={onClose} className="w-8 h-8 rounded-full hover:bg-white/10 flex items-center justify-center text-white shrink-0">
            <X className="w-4 h-4" />
          </button>
        </div>

        <div className="p-5 space-y-4 max-h-[80vh] overflow-y-auto">
          {/* Detailed Task Info Summary Card */}
          <div className="bg-slate-50 rounded-xl p-3.5 border border-slate-200 text-xs space-y-2">
            <div className="flex justify-between items-start">
              <div>
                <span className="text-[10px] font-black uppercase text-slate-400 block">Đoàn khách</span>
                <span className="font-bold text-slate-800 text-sm">{item.delegationName || 'Đoàn khách'}</span>
              </div>
              {item.status && (
                <span className="px-2 py-0.5 bg-blue-100 text-[#004c91] text-[10px] font-black rounded-md">
                  {item.status}
                </span>
              )}
            </div>
            <div className="grid grid-cols-2 gap-2 pt-1 border-t border-slate-200/60">
              <div>
                <span className="text-[10px] font-black uppercase text-slate-400 block">Nhiệm vụ / Mượn gì</span>
                <span className="font-semibold text-slate-700">{item.title}</span>
              </div>
              <div>
                <span className="text-[10px] font-black uppercase text-slate-400 block">Thời gian hiện tại</span>
                <span className="font-semibold text-slate-700">
                  {item.startAt && item.endAt ? fmtRange(item.startAt, item.endAt) : (item.time || 'Chưa thiết lập')}
                </span>
              </div>
              <div>
                <span className="text-[10px] font-black uppercase text-slate-400 block">Người giao</span>
                <span className="font-semibold text-slate-700">{item.host || 'IC Host / Hệ thống'}</span>
              </div>
              <div>
                <span className="text-[10px] font-black uppercase text-slate-400 block">Địa điểm</span>
                <span className="font-semibold text-slate-700">{item.location || 'Tại cơ sở'}</span>
              </div>
              {finalQuantity != null && (
                <div>
                  <span className="text-[10px] font-black uppercase text-slate-400 block">Số lượng</span>
                  <span className="font-semibold text-slate-700">{finalQuantity}</span>
                </div>
              )}
            </div>
            {item.purpose && (
              <div className="pt-1 border-t border-slate-200/60">
                <span className="text-[10px] font-black uppercase text-slate-400 block">Nội dung / Ghi chú</span>
                <span className="font-medium text-slate-600 block line-clamp-2">{item.purpose}</span>
              </div>
            )}
          </div>

          <div>
            <label className="block text-[11px] font-black text-slate-500 uppercase tracking-wide mb-1">Số lượng mới</label>
            <input type="number" min={0} max={detail?.quantity != null ? detail.quantity - 1 : undefined} step={1}
              value={quantity} onChange={e => setQuantity(e.target.value)}
              className={`w-full px-3 py-2 text-xs font-bold border rounded-xl outline-none ${quantityTooHigh ? 'border-red-400 focus:border-red-500 ring-1 ring-red-200' : 'border-slate-200 focus:border-[#004c91]'}`} />
            {quantityTooHigh && (
              <p className="mt-1 text-[11px] font-semibold text-red-600">Số lượng đề xuất phải nhỏ hơn số lượng dự kiến ({detail.quantity}).</p>
            )}
          </div>

          <div className="grid grid-cols-2 gap-3 pt-1">
            <div>
              <label className="block text-[11px] font-black text-slate-500 uppercase tracking-wide mb-1">
                {isMultiDay ? 'Ngày giờ bắt đầu mới *' : 'Giờ bắt đầu mới *'}
              </label>
              <input type={isMultiDay ? 'datetime-local' : 'time'} value={start} onChange={e => setStart(e.target.value)}
                className="w-full px-3 py-2 text-xs font-bold border border-slate-200 rounded-xl outline-none focus:border-[#004c91]" />
            </div>
            <div>
              <label className="block text-[11px] font-black text-slate-500 uppercase tracking-wide mb-1">
                {isMultiDay ? 'Ngày giờ kết thúc mới *' : 'Giờ kết thúc mới *'}
              </label>
              <input type={isMultiDay ? 'datetime-local' : 'time'} value={end} onChange={e => setEnd(e.target.value)}
                className="w-full px-3 py-2 text-xs font-bold border border-slate-200 rounded-xl outline-none focus:border-[#004c91]" />
            </div>
          </div>

          <div>
            <label className="block text-[11px] font-black text-slate-500 uppercase tracking-wide mb-1">Nội dung chi tiết công việc (đề xuất)</label>
            <textarea rows={2} value={content} onChange={e => setContent(e.target.value)}
              placeholder="Nhập nội dung công việc đề xuất (nếu có thay đổi)..."
              className="w-full px-3 py-2 text-xs border border-slate-200 rounded-xl outline-none focus:border-[#004c91] resize-none" />
          </div>

          <div>
            <label className="block text-[11px] font-black text-slate-500 uppercase tracking-wide mb-1">Lý do đề xuất *</label>
            <textarea rows={2} value={note} onChange={e => setNote(e.target.value)}
              placeholder="Lý do đề xuất thay đổi..."
              className="w-full px-3 py-2 text-xs border border-slate-200 rounded-xl outline-none focus:border-[#004c91] resize-none" />
          </div>

          <div className="flex gap-2 pt-2">
            <button onClick={handleSubmit} disabled={loading || !note.trim() || !start || !end || quantityTooHigh}
              className="flex-1 py-2.5 bg-[#f37021] hover:bg-[#e05f10] text-white text-sm font-black rounded-xl transition-colors disabled:opacity-50 shadow-sm">
              {loading ? 'Đang gửi...' : 'Gửi đề xuất'}
            </button>
            <button onClick={onClose} className="px-5 py-2.5 text-sm font-bold text-slate-600 bg-slate-100 rounded-xl hover:bg-slate-200">Hủy</button>
          </div>
        </div>
      </div>
    </div>
  );
}
