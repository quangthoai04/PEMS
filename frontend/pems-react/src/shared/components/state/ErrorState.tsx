import { AlertTriangle, RefreshCw } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { normalizeApiError, type ApiErrorCategory, type NormalizedApiError } from '../../api/normalizeApiError';

/** Maps a normalized category to its i18n title key under common:asyncState.error.*. */
export function errorTitleKey(err: NormalizedApiError): string {
  if (err.isVisitFormDetailMissing) return 'asyncState.error.formDetailMissing';
  const byCategory: Record<ApiErrorCategory, string> = {
    forbidden: 'asyncState.error.forbidden',
    notFound: 'asyncState.error.notFound',
    conflict: 'asyncState.error.conflict',
    validation: 'asyncState.error.validation',
    server: 'asyncState.error.server',
    network: 'asyncState.error.network',
    timeout: 'asyncState.error.timeout',
    unknown: 'asyncState.error.generic',
  };
  return byCategory[err.category];
}

interface ErrorStateProps {
  /** The failure — a raw thrown value or an already-normalized error. */
  error: unknown;
  /** Retry handler; the button is hidden when omitted. */
  onRetry?: () => void;
  /** Compact one-line variant for small panels. */
  inline?: boolean;
  className?: string;
}

/**
 * The failure state. It renders a category-appropriate TITLE (permission vs not-found vs conflict vs
 * server vs network vs timeout) plus the backend message, a retry button, and — when present — the
 * backend error code and a correlation id for support. It is the deliberate opposite of an empty
 * state: a 403/409/500/network failure lands here, never in "no data".
 */
export function ErrorState({ error, onRetry, inline = false, className = '' }: ErrorStateProps) {
  const { t } = useTranslation('common');
  const norm = (error as NormalizedApiError | undefined)?.category
    ? (error as NormalizedApiError)
    : normalizeApiError(error);
  const title = t(errorTitleKey(norm));

  if (inline) {
    return (
      <div role="alert" className={'flex items-center gap-2 text-sm text-red-600 ' + className}>
        <AlertTriangle className="w-4 h-4 shrink-0" aria-hidden="true" />
        <span className="min-w-0 truncate" title={norm.message || title}>{norm.message || title}</span>
        {onRetry && (
          <button type="button" onClick={onRetry} className="ml-1 inline-flex items-center gap-1 text-red-700 underline hover:no-underline">
            <RefreshCw className="w-3.5 h-3.5" aria-hidden="true" />
            {t('asyncState.retry')}
          </button>
        )}
      </div>
    );
  }

  return (
    <div role="alert" className={'flex flex-col items-center justify-center gap-2 py-10 text-center ' + className}>
      <div className="w-12 h-12 rounded-full bg-red-50 flex items-center justify-center text-red-500" aria-hidden="true">
        <AlertTriangle className="w-6 h-6" />
      </div>
      <p className="text-sm font-semibold text-slate-800">{title}</p>
      {norm.message && norm.message !== title && (
        <p className="text-xs text-gray-500 max-w-md">{norm.message}</p>
      )}
      {(norm.errorCode || norm.correlationId) && (
        <p className="text-[11px] text-gray-400">
          {norm.errorCode && <span>{t('asyncState.errorCodeLabel')}: {norm.errorCode}</span>}
          {norm.errorCode && norm.correlationId && <span> · </span>}
          {norm.correlationId && <span>{t('asyncState.correlationLabel')}: {norm.correlationId}</span>}
        </p>
      )}
      {onRetry && (
        <button
          type="button"
          onClick={onRetry}
          className="mt-2 inline-flex items-center gap-1.5 rounded-lg border border-gray-300 bg-white px-3 py-1.5 text-sm font-medium text-slate-700 hover:bg-gray-50"
        >
          <RefreshCw className="w-4 h-4" aria-hidden="true" />
          {t('asyncState.retry')}
        </button>
      )}
    </div>
  );
}

export default ErrorState;
