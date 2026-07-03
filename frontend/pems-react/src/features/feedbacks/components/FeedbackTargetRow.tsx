import React from 'react';
import { CheckCircle2, PenLine } from 'lucide-react';
import { CompactStarRating } from './CompactStarRating';
import type { FeedbackTarget } from '../types/visitFeedback.types';

export interface FeedbackDraft {
  rating: number;
  comment: string;
}

interface Props {
  key?: React.Key;
  index: number;
  target: FeedbackTarget;
  draft: FeedbackDraft;
  disabled: boolean;
  onRate: (rating: number) => void;
  onChangeComment: (comment: string) => void;
}

/**
 * Một dòng đánh giá compact: STT • tên + info phụ • sao • trạng thái.
 * Kèm theo ô input nhận xét ở dưới.
 * Mục đã gửi rồi hiển thị readonly với badge "Đã đánh giá".
 */
export function FeedbackTargetRow({ index, target, draft, disabled, onRate, onChangeComment }: Props) {
  const submitted = target.alreadySubmitted;
  const rating = submitted ? (target.existingRating ?? 0) : draft.rating;
  const comment = submitted ? (target.existingComment ?? '') : draft.comment;

  return (
    <div className={`flex flex-col px-3 py-2 ${submitted ? 'bg-emerald-50/40' : 'bg-white'}`}>
      <div className="flex items-center gap-2">
        <span className="w-6 shrink-0 text-center text-xs font-semibold text-slate-400">{index}</span>
        <div className="min-w-0 flex-1">
          <p className="truncate text-sm font-semibold text-slate-800">{target.name}</p>
          {target.subtitle && <p className="truncate text-xs text-slate-500">{target.subtitle}</p>}
        </div>
        <CompactStarRating
          value={rating}
          readOnly={submitted || disabled}
          onChange={onRate}
          size="sm"
        />
        <span className="w-[86px] shrink-0 text-right">
          {submitted ? (
            <span className="inline-flex items-center gap-1 rounded-full bg-emerald-100 px-2 py-0.5 text-[11px] font-bold text-emerald-700">
              <CheckCircle2 className="w-3 h-3" /> Đã đánh giá
            </span>
          ) : rating > 0 ? (
            <span className="text-[11px] font-semibold text-[#004c91]">Đã chấm {rating}★</span>
          ) : (
            <span className="text-[11px] italic text-slate-400">Chưa chấm</span>
          )}
        </span>
      </div>
      
      <div className="mt-1.5 pl-8 pr-2 w-full lg:w-3/4">
        {submitted ? (
          comment && <p className="text-xs text-slate-600 bg-white/60 p-2 rounded border border-emerald-100/50 italic">{comment}</p>
        ) : (
          <input
            type="text"
            value={comment}
            onChange={(e) => onChangeComment(e.target.value)}
            disabled={disabled}
            placeholder="Nhận xét thêm (không bắt buộc)..."
            className="w-full rounded-md border border-slate-200 px-3 py-1.5 text-xs text-slate-700 placeholder:text-slate-400 focus:border-[#F37021] focus:outline-none focus:ring-1 focus:ring-[#F37021]/30 disabled:bg-slate-50 disabled:opacity-70 transition-shadow"
          />
        )}
      </div>
    </div>
  );
}
