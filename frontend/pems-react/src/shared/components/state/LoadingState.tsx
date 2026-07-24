import { Loader2 } from 'lucide-react';
import { useTranslation } from 'react-i18next';

interface LoadingStateProps {
  /** Optional label; defaults to the shared "Loading…" string. */
  label?: string;
  /** Compact inline spinner (no min-height block) — e.g. inside a small card. */
  inline?: boolean;
  className?: string;
}

/**
 * The single loading indicator for async panels. It is deliberately distinct from both the empty and
 * the error states so a screen can never render "loading" as "no data". role="status" + aria-live so
 * assistive tech announces it.
 */
export function LoadingState({ label, inline = false, className = '' }: LoadingStateProps) {
  const { t } = useTranslation('common');
  const text = label ?? t('asyncState.loading');
  return (
    <div
      role="status"
      aria-live="polite"
      className={
        (inline
          ? 'flex items-center gap-2 text-sm text-gray-500 '
          : 'flex flex-col items-center justify-center gap-3 py-12 text-gray-500 ') + className
      }
    >
      <Loader2 className={(inline ? 'w-4 h-4' : 'w-8 h-8') + ' animate-spin text-slate-400'} aria-hidden="true" />
      <span className={inline ? '' : 'text-sm font-medium'}>{text}</span>
    </div>
  );
}

export default LoadingState;
