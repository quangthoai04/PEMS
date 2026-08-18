import React from 'react';

interface Props {
  total: number;
  draft: number;
  published: number;
  archived: number;
}

export function DocumentSummaryCompact({ total, draft, published, archived }: Props) {
  return (
    <div className="flex flex-wrap items-center gap-x-2 gap-y-1 text-xs sm:text-sm font-normal text-slate-600">
      <span className="font-bold text-slate-700">Tổng quan:</span>
      <span>{total} tài liệu</span>
      <span className="text-slate-300">•</span>
      <span>Draft <span className="font-normal text-amber-600">{draft}</span></span>
      <span className="text-slate-300">•</span>
      <span>Published <span className="font-normal text-emerald-600">{published}</span></span>
      <span className="text-slate-300">•</span>
      <span>Archived <span className="font-normal text-slate-600">{archived}</span></span>
    </div>
  );
}
