import React from 'react';
import { useTranslation } from 'react-i18next';
import { Info } from 'lucide-react';
import { FeedbackTargetRow, type FeedbackDraft } from './FeedbackTargetRow';
import type { FeedbackGroup } from '../types/visitFeedback.types';

interface Props {
  key?: React.Key;
  group: FeedbackGroup;
  startIndex: number;            // STT liên tục qua các group
  drafts: Record<string, FeedbackDraft>;
  disabled: boolean;
  forceShowComment?: boolean;
  onRate: (targetKey: string, rating: number) => void;
  onChangeComment: (targetKey: string, comment: string) => void;
}

/**
 * Một nhóm đánh giá dạng compact list: header mỏng + các row.
 * Trên desktop (modal rộng ~960px), dùng 2 cột để tận dụng không gian ngang.
 * Group "Đoàn khách" hoặc chỉ có 1 target sẽ hiển thị 1 cột toàn chiều rộng.
 */
export function FeedbackGroupSection({ group, startIndex, drafts, disabled, forceShowComment, onRate, onChangeComment }: Props) {
  const { t } = useTranslation('feedback');
  // Dùng 2 cột nếu group có >1 target và không phải group "Đoàn khách" chung
  const useGrid = group.targets.length > 1 && group.groupCode !== 'DELEGATION';

  return (
    <section className="rounded-lg border border-slate-200 bg-white overflow-hidden">
      {/* Header group */}
      <div className="flex items-center justify-between border-b border-slate-100 bg-slate-50 px-3 py-1.5">
        <h3 className="text-[13px] font-bold text-[#004c91]">{group.title}</h3>
        <span className="text-[11px] font-semibold text-slate-400">{t('targetCount', { count: group.targets.length })}</span>
      </div>

      {/* Info note nếu có */}
      {group.infoNote && (
        <p className="flex items-start gap-1.5 border-b border-slate-100 px-3 py-2 text-xs text-slate-500">
          <Info className="mt-0.5 h-3.5 w-3.5 shrink-0 text-slate-400" /> {group.infoNote}
        </p>
      )}

      {/* Target rows */}
      {group.targets.length > 0 && (
        useGrid ? (
          // 2-column grid on wider screens — tận dụng modal 960px
          <div className="grid grid-cols-1 sm:grid-cols-2 divide-y sm:divide-y-0 sm:gap-px sm:bg-slate-100">
            {group.targets.map((t, i) => (
              <div key={t.targetKey} className="bg-white sm:border-b sm:border-slate-100">
                <FeedbackTargetRow
                  index={startIndex + i}
                  target={t}
                  draft={drafts[t.targetKey] ?? { rating: 0, comment: '' }}
                  disabled={disabled}
                  forceShowComment={forceShowComment}
                  onRate={(r) => onRate(t.targetKey, r)}
                  onChangeComment={(v) => onChangeComment(t.targetKey, v)}
                />
              </div>
            ))}
          </div>
        ) : (
          // 1 cột (group Đoàn khách, hoặc group chỉ có 1 phần tử)
          <div className="divide-y divide-slate-100">
            {group.targets.map((t, i) => (
              <FeedbackTargetRow
                key={t.targetKey}
                index={startIndex + i}
                target={t}
                draft={drafts[t.targetKey] ?? { rating: 0, comment: '' }}
                disabled={disabled}
                forceShowComment={forceShowComment}
                onRate={(r) => onRate(t.targetKey, r)}
                onChangeComment={(v) => onChangeComment(t.targetKey, v)}
              />
            ))}
          </div>
        )
      )}
    </section>
  );
}
