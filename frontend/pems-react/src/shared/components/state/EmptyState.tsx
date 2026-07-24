import type { ReactNode } from 'react';
import { Inbox } from 'lucide-react';
import { useTranslation } from 'react-i18next';

interface EmptyStateProps {
  /** Business-meaning title, e.g. "Chưa có biên bản". Falls back to a generic "no data yet". */
  title?: string;
  description?: string;
  icon?: ReactNode;
  /** Optional call-to-action (e.g. a "create" button) rendered under the text. */
  action?: ReactNode;
  className?: string;
}

/**
 * The success-but-empty state: the request succeeded and the answer is genuinely "nothing here yet".
 * This must NEVER be used to render a failure — a failed request renders {@link ErrorState} instead, so
 * the user can tell "no minutes yet" apart from "we could not load the minutes".
 */
export function EmptyState({ title, description, icon, action, className = '' }: EmptyStateProps) {
  const { t } = useTranslation('common');
  return (
    <div className={'flex flex-col items-center justify-center gap-2 py-12 text-center text-gray-500 ' + className}>
      <div className="w-12 h-12 rounded-full bg-slate-100 flex items-center justify-center text-slate-400" aria-hidden="true">
        {icon ?? <Inbox className="w-6 h-6" />}
      </div>
      <p className="text-sm font-semibold text-slate-700">{title ?? t('asyncState.emptyTitle')}</p>
      {description && <p className="text-xs text-gray-500 max-w-sm">{description}</p>}
      {action && <div className="mt-2">{action}</div>}
    </div>
  );
}

export default EmptyState;
