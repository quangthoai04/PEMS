import { ClipboardCheck, CircleCheck } from 'lucide-react';
import type { VisitNextTask } from '../types/delegations.types';

/**
 * "Việc cần làm" — the third layer of a row, beside the status and the relation.
 *
 * It is a separate line rather than a badge because it is a sentence, not a state: "Hoàn thiện lịch
 * trình và công tác chuẩn bị" cannot be squeezed into the chip that says "Đang chuẩn bị", and folding
 * it in there is what made the status badge try to be an instruction for one audience and a fact for
 * everyone else.
 *
 * The text always comes from the backend. Nothing here maps status → task.
 */
export function VisitNextTaskLine({
  task, testId,
}: {
  task?: VisitNextTask | null;
  testId?: string;
}) {
  if (!task) return null;

  const idle = task.code === 'NONE' || !task.requiresAction;
  const Icon = idle ? CircleCheck : ClipboardCheck;

  return (
    <div
      data-testid={testId}
      data-next-task-code={task.code}
      className="mt-1.5 flex items-start gap-1.5"
    >
      <Icon
        className={`mt-[2px] h-3.5 w-3.5 shrink-0 ${idle ? 'text-slate-300' : 'text-[#f37021]'}`}
        aria-hidden
      />
      <div className="min-w-0">
        <span className="block text-[10px] font-bold uppercase tracking-wide text-slate-400">
          Việc cần làm
        </span>
        {/* Two lines maximum, with the full sentence on title — a long task must never push the row
            into a different height than its neighbours. */}
        <span
          title={task.disabledReason ? `${task.label} — ${task.disabledReason}` : task.label}
          className={`block line-clamp-2 text-xs font-semibold leading-snug ${
            idle ? 'text-slate-400' : 'text-slate-700'
          }`}
        >
          {task.label}
        </span>
        {task.disabledReason && (
          <span className="mt-0.5 block text-[11px] font-normal leading-snug text-amber-700">
            {task.disabledReason}
          </span>
        )}
      </div>
    </div>
  );
}
