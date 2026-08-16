import React, { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { CheckCircle2, PenLine, X } from 'lucide-react';
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
  forceShowComment?: boolean;
  onRate: (rating: number) => void;
  onChangeComment: (comment: string) => void;
}

/**
 * Một dòng đánh giá compact: STT • tên + info phụ • sao • trạng thái.
 * Comment ẩn mặc định, chỉ hiện khi bấm icon bút (PenLine).
 * Mục đã gửi rồi hiển thị readonly với badge "Đã đánh giá".
 */
export function FeedbackTargetRow({ index, target, draft, disabled, forceShowComment, onRate, onChangeComment }: Props) {
  const { t } = useTranslation('feedback');
  const submitted = target.alreadySubmitted;
  const rating = submitted ? (target.existingRating ?? 0) : draft.rating;
  const comment = submitted ? (target.existingComment ?? '') : draft.comment;
  const [showComment, setShowComment] = useState(false);
  const isCommentVisible = forceShowComment || showComment;

  return (
    <div className={`px-3 py-2.5 ${submitted ? 'bg-emerald-50/40' : 'bg-white'}`}>
      {/* Main row */}
      <div className="flex items-center gap-2 min-w-0">
        <span className="w-5 shrink-0 text-center text-[11px] font-semibold text-slate-400">{index}</span>
        
        {/* Name + subtitle */}
        <div className="min-w-0 flex-1">
          <p className="truncate text-[13px] font-semibold text-slate-800 leading-tight">{target.name}</p>
          {target.subtitle && (
            <p className="truncate text-[11px] text-slate-400 leading-tight">{target.subtitle}</p>
          )}
        </div>

        {/* Stars */}
        <div className="shrink-0">
          <CompactStarRating
            value={rating}
            readOnly={submitted || disabled}
            onChange={onRate}
            size="sm"
          />
        </div>

        {/* Status badge + comment toggle */}
        <div className="flex items-center gap-1 shrink-0">
          {submitted ? (
            <span className="inline-flex items-center gap-1 rounded-full bg-emerald-100 px-2 py-0.5 text-[11px] font-bold text-emerald-700">
              <CheckCircle2 className="w-3 h-3" /> {t('row.alreadyRated')}
            </span>
          ) : rating > 0 ? (
            <span className="text-[11px] font-semibold text-[#004c91]">{rating}★</span>
          ) : (
            <span className="text-[11px] italic text-slate-400 hidden sm:inline">{t('row.notRatedYet')}</span>
          )}

          {/* Nút mở comment: ẩn nếu bị ép hiện, hoặc ẩn khi đang submit / disabled */}
          {!forceShowComment && (
            submitted ? (
              comment && (
                <button
                  type="button"
                  onClick={() => setShowComment(!showComment)}
                  title={t('row.viewComment')}
                  className="p-1 rounded text-slate-400 hover:text-[#004c91] hover:bg-slate-100 outline-none"
                >
                  <PenLine className="w-3.5 h-3.5" />
                </button>
              )
            ) : (
              <button
                type="button"
                onClick={() => setShowComment(!showComment)}
                disabled={disabled}
                title={showComment ? t('row.hideComment') : t('row.addComment')}
                className={`p-1 rounded outline-none transition-colors ${showComment ? 'text-[#F37021] bg-orange-50' : 'text-slate-300 hover:text-slate-500 hover:bg-slate-100'} disabled:opacity-40`}
              >
                <PenLine className="w-3.5 h-3.5" />
              </button>
            )
          )}
        </div>
      </div>

      {/* Comment area — ẩn mặc định, chỉ hiển thị khi isCommentVisible=true */}
      {isCommentVisible && (
        <div className="mt-1.5 pl-7 pr-1">
          {submitted ? (
            comment && (
              <p className="text-[12px] text-slate-600 bg-slate-50 px-2.5 py-1.5 rounded border border-slate-100 italic leading-relaxed">
                {comment}
              </p>
            )
          ) : (
            <div className="relative">
              <input
                type="text"
                value={comment}
                onChange={(e) => onChangeComment(e.target.value)}
                disabled={disabled}
                placeholder={t('row.commentPlaceholder')}
                className="w-full rounded-md border border-slate-200 pl-3 pr-8 py-1.5 text-[12px] text-slate-700 placeholder:text-slate-400 focus:border-[#F37021] focus:outline-none focus:ring-1 focus:ring-[#F37021]/30 disabled:bg-slate-50 disabled:opacity-70"
              />
              {comment && (
                <button
                  type="button"
                  onClick={() => onChangeComment('')}
                  className="absolute right-2 top-1/2 -translate-y-1/2 text-slate-300 hover:text-slate-500 outline-none"
                >
                  <X className="w-3 h-3" />
                </button>
              )}
            </div>
          )}
        </div>
      )}
    </div>
  );
}
