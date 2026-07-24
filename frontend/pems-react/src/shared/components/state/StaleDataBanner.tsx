import { AlertCircle, RefreshCw } from 'lucide-react';
import { useTranslation } from 'react-i18next';

interface StaleDataBannerProps {
  /** Retry the refresh; button hidden when omitted. */
  onRetry?: () => void;
  className?: string;
}

/**
 * Shown when a refresh FAILED but we chose to keep the previously-loaded data on screen. It tells the
 * user plainly that what they see may be out of date instead of silently pretending it is current — so
 * freshness-sensitive actions can be gated on the caller's side.
 */
export function StaleDataBanner({ onRetry, className = '' }: StaleDataBannerProps) {
  const { t } = useTranslation('common');
  return (
    <div
      role="status"
      className={'flex items-start gap-2 rounded-lg border border-amber-200 bg-amber-50 px-3 py-2 text-amber-800 ' + className}
    >
      <AlertCircle className="w-4 h-4 mt-0.5 shrink-0" aria-hidden="true" />
      <div className="min-w-0 text-xs">
        <p className="font-semibold">{t('asyncState.stale.title')}</p>
        <p className="text-amber-700">{t('asyncState.stale.description')}</p>
      </div>
      {onRetry && (
        <button
          type="button"
          onClick={onRetry}
          className="ml-auto inline-flex items-center gap-1 text-amber-800 underline hover:no-underline shrink-0"
        >
          <RefreshCw className="w-3.5 h-3.5" aria-hidden="true" />
          {t('asyncState.retry')}
        </button>
      )}
    </div>
  );
}

export default StaleDataBanner;
