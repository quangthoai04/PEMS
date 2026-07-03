import React, { useEffect, useState } from 'react';
import { X } from 'lucide-react';

interface Props {
  open: boolean;
  targetName: string;
  initialValue: string;
  onSave: (value: string) => void;
  onClose: () => void;
}

/**
 * Modal nhập nhận xét (optional) cho một mục đánh giá — mở qua icon bút.
 * Desktop: modal nhỏ giữa màn; mobile: bottom sheet.
 */
export function CommentModal({ open, targetName, initialValue, onSave, onClose }: Props) {
  const [value, setValue] = useState(initialValue);

  useEffect(() => {
    if (open) setValue(initialValue);
  }, [open, initialValue]);

  if (!open) return null;

  return (
    <div className="fixed inset-0 z-[110] flex items-end sm:items-center justify-center">
      <div className="absolute inset-0 bg-black/40" onClick={onClose} />
      <div className="relative w-full sm:w-[440px] sm:rounded-xl rounded-t-2xl bg-white shadow-xl border border-slate-200 p-4">
        <div className="flex items-start justify-between gap-3 mb-2">
          <div className="min-w-0">
            <h4 className="text-sm font-bold text-slate-800">Nhận xét (không bắt buộc)</h4>
            <p className="text-xs text-slate-500 truncate">{targetName}</p>
          </div>
          <button type="button" onClick={onClose} className="p-1 rounded-full hover:bg-slate-100 text-slate-500 outline-none" aria-label="Đóng">
            <X className="w-4 h-4" />
          </button>
        </div>
        <textarea
          autoFocus
          value={value}
          onChange={(e) => setValue(e.target.value)}
          maxLength={4000}
          rows={4}
          placeholder="Nhập nhận xét của bạn..."
          className="w-full resize-none rounded-lg border border-slate-300 px-3 py-2 text-sm outline-none focus:border-[#004c91]"
        />
        <div className="mt-3 flex items-center justify-end gap-2">
          <button
            type="button"
            onClick={onClose}
            className="rounded-lg border border-slate-200 bg-white px-3.5 py-1.5 text-xs font-bold text-slate-600 hover:bg-slate-50 outline-none"
          >
            Hủy
          </button>
          <button
            type="button"
            onClick={() => { onSave(value); onClose(); }}
            className="rounded-lg bg-[#004c91] px-3.5 py-1.5 text-xs font-bold text-white hover:bg-[#003b70] outline-none"
          >
            Lưu nhận xét
          </button>
        </div>
      </div>
    </div>
  );
}
