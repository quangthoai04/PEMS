import React from 'react';
import { Info } from 'lucide-react';
import { FeedbackTargetRow, type FeedbackDraft } from './FeedbackTargetRow';
import type { FeedbackGroup } from '../types/visitFeedback.types';

interface Props {
  key?: React.Key;
  group: FeedbackGroup;
  startIndex: number;            // STT liên tục qua các group
  drafts: Record<string, FeedbackDraft>;
  disabled: boolean;
  onRate: (targetKey: string, rating: number) => void;
  onChangeComment: (targetKey: string, comment: string) => void;
}

/** Một nhóm đánh giá dạng compact list: header mỏng + các row, không dùng card lớn. */
export function FeedbackGroupSection({ group, startIndex, drafts, disabled, onRate, onChangeComment }: Props) {
  return (
    <section className="rounded-lg border border-slate-200 bg-white overflow-hidden">
      <div className="flex items-center justify-between border-b border-slate-100 bg-slate-50 px-3 py-1.5">
        <h3 className="text-[13px] font-bold text-[#004c91]">{group.title}</h3>
        <span className="text-[11px] font-semibold text-slate-400">{group.targets.length} mục</span>
      </div>
      {group.infoNote && (
        <p className="flex items-start gap-1.5 border-b border-slate-100 px-3 py-2 text-xs text-slate-500">
          <Info className="mt-0.5 h-3.5 w-3.5 shrink-0 text-slate-400" /> {group.infoNote}
        </p>
      )}
      {group.targets.length > 0 && (
        <div className="divide-y divide-slate-100">
          {group.targets.map((t, i) => (
            <FeedbackTargetRow
              key={t.targetKey}
              index={startIndex + i}
              target={t}
              draft={drafts[t.targetKey] ?? { rating: 0, comment: '' }}
              disabled={disabled}
              onRate={(r) => onRate(t.targetKey, r)}
              onChangeComment={(v) => onChangeComment(t.targetKey, v)}
            />
          ))}
        </div>
      )}
    </section>
  );
}
