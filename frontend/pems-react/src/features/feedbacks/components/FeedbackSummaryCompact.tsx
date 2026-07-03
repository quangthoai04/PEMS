import React from 'react';
import { Star } from 'lucide-react';

interface Props {
  totalDelegations?: number;
  totalFeedbacks?: number;
  avgRating: number;
  lowRating: number;
  latest?: string | null;
}

const formatDate = (dateStr: string) => {
  try {
    const d = new Date(dateStr);
    return d.toLocaleDateString('vi-VN', { day: '2-digit', month: '2-digit', year: 'numeric', hour: '2-digit', minute: '2-digit' });
  } catch {
    return dateStr;
  }
};

export function FeedbackSummaryCompact({ totalDelegations, totalFeedbacks, avgRating, lowRating, latest }: Props) {
  return (
    <div className="flex flex-wrap items-center gap-x-2 gap-y-1 text-xs sm:text-sm font-medium text-slate-600 mb-4">
      <span className="font-bold text-slate-700">Tổng quan:</span>
      {typeof totalDelegations === 'number' && (
        <>
          <span>{totalDelegations} đoàn</span>
          <span className="text-slate-300">•</span>
        </>
      )}
      {typeof totalFeedbacks === 'number' && (
        <>
          <span>{totalFeedbacks} lượt đánh giá</span>
          <span className="text-slate-300">•</span>
        </>
      )}
      <span className="inline-flex items-center gap-1">
        Điểm TB {avgRating.toFixed(1)}
        <Star className="w-3.5 h-3.5 fill-yellow-400 text-yellow-400" />
      </span>
      <span className="text-slate-300">•</span>
      <span className={lowRating > 0 ? 'text-red-600 font-bold' : ''}>Cảnh báo {lowRating}</span>
      {latest && (
        <>
          <span className="text-slate-300">•</span>
          <span>Mới nhất {formatDate(latest)}</span>
        </>
      )}
    </div>
  );
}
