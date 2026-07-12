/**
 * AssignedTaskList – Danh sách compact "Đơn/thư chưa xử lý" cho Department Staff.
 * List hết mọi item, mỗi dòng gọn: tên đoàn/nhiệm vụ, loại, thời gian + action trực tiếp.
 */
import React from 'react';
import { AlertCircle } from 'lucide-react';
import type { AssignedTask } from './useDeptStaffData';
import { toVietnamCalendarDate } from '../../../shared/utils/vietnamTime';

export const taskKey = (item: AssignedTask, action: string) =>
  `${item.itemType}_${item.itemId}_${action}`;

function typeLabel(item: AssignedTask) {
  return item.itemType === 'INVITATION' ? 'Thư mời tham gia' : 'Nhiệm vụ hỗ trợ (logistics)';
}

function timeLabel(item: AssignedTask) {
  // Giờ Việt Nam cố định (re-based) — không phụ thuộc timezone browser.
  const s = item.startAt ? toVietnamCalendarDate(item.startAt) : null;
  const e = item.endAt ? toVietnamCalendarDate(item.endAt) : null;
  if (!s || Number.isNaN(s.getTime())) return '';
  const hm = (d: Date) => `${String(d.getHours()).padStart(2, '0')}:${String(d.getMinutes()).padStart(2, '0')}`;
  const dmy = `${String(s.getUTCDate()).padStart(2, '0')}/${String(s.getUTCMonth() + 1).padStart(2, '0')}/${s.getUTCFullYear()}`;
  if (e && !Number.isNaN(e.getTime())) return `${hm(s)} - ${hm(e)} ${dmy}`;
  return `${hm(s)} ${dmy}`;
}

interface RowProps {
  item: AssignedTask;
  busyKey: string | null;
  onAccept: (item: AssignedTask) => void;
  onDecline: (item: AssignedTask) => void;
  onPropose: (item: AssignedTask) => void;
  onOpenDetail: (item: AssignedTask) => void;
}

export function AssignedTaskRow({ item, busyKey, onAccept, onDecline, onPropose, onOpenDetail }: RowProps) {
  const busy = busyKey != null && busyKey.startsWith(`${item.itemType}_${item.itemId}_`);
  const time = timeLabel(item);
  return (
    <div className="flex flex-col gap-1.5 py-2 sm:flex-row sm:items-center sm:gap-3">
      <div className="min-w-0 flex-1">
        <p className="text-[13px] font-bold text-slate-800 truncate">
          {item.delegationName ? `${item.delegationName} — ` : ''}{item.title}
        </p>
        <p className="text-[11px] font-semibold text-slate-500">
          {typeLabel(item)}{time ? ` · ${time}` : ''}
        </p>
      </div>
      <div className="flex flex-wrap items-center gap-1.5 shrink-0">
        {item.canAccept && (
          <button onClick={() => onAccept(item)} disabled={busy}
            className="px-2.5 py-1 rounded-lg bg-emerald-600 text-white text-[11px] font-black hover:bg-emerald-700 disabled:opacity-50">
            Nhận
          </button>
        )}
        {item.canDecline && (
          <button onClick={() => onDecline(item)} disabled={busy}
            className="px-2.5 py-1 rounded-lg border border-rose-200 bg-white text-rose-600 text-[11px] font-black hover:bg-rose-50 disabled:opacity-50">
            Từ chối
          </button>
        )}
        {item.itemType === 'REQUEST' && item.canProposeChange && (
          <button onClick={() => onPropose(item)} disabled={busy}
            className="px-2.5 py-1 rounded-lg border border-amber-200 bg-white text-amber-700 text-[11px] font-black hover:bg-amber-50 disabled:opacity-50">
            Đề xuất
          </button>
        )}
        <button onClick={() => onOpenDetail(item)}
          className="px-2.5 py-1 rounded-lg border border-slate-200 bg-white text-slate-600 text-[11px] font-black hover:bg-slate-50">
          Chi tiết
        </button>
      </div>
    </div>
  );
}

interface ListProps {
  items: AssignedTask[];
  busyKey: string | null;
  onAccept: (item: AssignedTask) => void;
  onDecline: (item: AssignedTask) => void;
  onPropose: (item: AssignedTask) => void;
  onOpenDetail: (item: AssignedTask) => void;
}

export function AssignedTaskList({ items, busyKey, onAccept, onDecline, onPropose, onOpenDetail }: ListProps) {
  if (items.length === 0) return null;
  return (
    <div className="rounded-xl border border-orange-200 bg-white overflow-hidden">
      <div className="flex items-center gap-2 px-3 py-2 bg-orange-50/70 border-b border-orange-100">
        <AlertCircle className="w-4 h-4 text-[#f37021]" />
        <span className="text-xs font-black text-[#f37021]">Đơn/thư chưa xử lý</span>
        <span className="px-1.5 py-0.5 text-[10px] font-black bg-[#f37021] text-white rounded-full">{items.length}</span>
      </div>
      <ul className="divide-y divide-slate-100 px-3">
        {items.map(item => (
          <li key={`${item.itemType}_${item.itemId}`}>
            <AssignedTaskRow
              item={item}
              busyKey={busyKey}
              onAccept={onAccept}
              onDecline={onDecline}
              onPropose={onPropose}
              onOpenDetail={onOpenDetail}
            />
          </li>
        ))}
      </ul>
    </div>
  );
}
