import React, { useState } from 'react';
import { FeedbackListItem } from '../types/feedbacks.types';
import { FeedbackRatingStars } from './FeedbackRatingStars';

interface Props {
  title: string;
  items: FeedbackListItem[];
  emptyText?: string;
}

const COMMENT_TRUNCATE_LENGTH = 140;

function FeedbackCommentText({ comment }: { comment: string }) {
  const [expanded, setExpanded] = useState(false);
  const isLong = comment.length > COMMENT_TRUNCATE_LENGTH;
  const displayText = expanded || !isLong ? comment : `${comment.slice(0, COMMENT_TRUNCATE_LENGTH)}…`;

  return (
    <p className="text-xs text-slate-500 leading-snug italic mt-0.5">
      "{displayText}"
      {isLong && (
        <button
          type="button"
          onClick={() => setExpanded((v) => !v)}
          className="ml-1 not-italic text-xs font-bold text-[#004c91] hover:underline"
        >
          {expanded ? 'Thu gọn' : 'Xem thêm'}
        </button>
      )}
    </p>
  );
}

const formatDate = (dateStr: string) => {
  try {
    const d = new Date(dateStr);
    return d.toLocaleDateString('vi-VN', { day: '2-digit', month: '2-digit', hour: '2-digit', minute: '2-digit' });
  } catch {
    return dateStr;
  }
};

export function FeedbackTypeSection({ title, items, emptyText = 'Chưa có đánh giá.' }: Props) {
  return (
    <div>
      <h3 className="text-sm font-bold text-slate-800 mb-1.5">{title} <span className="text-slate-400 font-medium">({items.length})</span></h3>

      {items.length === 0 ? (
        <p className="text-xs text-slate-400 italic py-1">{emptyText}</p>
      ) : (
        <div className="divide-y divide-slate-100">
          {items.map((fb) => (
            <div key={fb.feedbackId} className="py-2 flex items-start justify-between gap-3">
              <div className="min-w-0 flex-1">
                <p className="text-sm text-slate-700">
                  <span className="font-bold text-slate-800">{fb.submitterNameSnapshot}</span>
                  <span className="text-slate-400"> → </span>
                  <span className="font-medium">{fb.targetNameSnapshot}</span>
                  <span className="text-[11px] text-slate-400 ml-2">{formatDate(fb.submittedAt)}</span>
                </p>
                {fb.commentPreview && <FeedbackCommentText comment={fb.commentPreview} />}
              </div>
              <div className="flex gap-0.5 shrink-0 pt-0.5">
                <FeedbackRatingStars rating={fb.rating} />
              </div>
            </div>
          ))}
        </div>
      )}
    </div>
  );
}
