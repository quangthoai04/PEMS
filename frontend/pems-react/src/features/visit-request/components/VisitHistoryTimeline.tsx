import { useCallback, useEffect, useState } from 'react';
import { AlertCircle, Loader2, RefreshCw } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { getVisitRequestHistory, type VisitHistoryEntry, type VisitRequestHistory } from '../api/visitRequestV2Api';
import { formatVietnamDateTime } from '../../../shared/utils/vietnamTime';

interface Props {
  visitRequestId: number;
}

/** Decisions and outcomes are the turning points of a request — they get the accent. */
const EMPHASISED_EVENTS = new Set([
  'INSTANCE_APPROVED', 'INSTANCE_REJECTED', 'INSTANCE_CANCELLED', 'INSTANCE_DECIDED',
  'AMENDMENT_APPROVED', 'AMENDMENT_REJECTED', 'AMENDMENT_DECIDED',
]);

/** Event codes the client knows how to phrase; anything else degrades to a neutral sentence. */
const KNOWN_EVENTS = new Set([
  'REQUEST_REVISION', 'INSTANCE_CONTENT_CREATED', 'INSTANCE_CONTENT_REVISED',
  'INSTANCE_APPROVED', 'INSTANCE_REJECTED', 'INSTANCE_CANCELLED', 'INSTANCE_CLOSED', 'INSTANCE_DECIDED',
  'AMENDMENT_SUBMITTED', 'AMENDMENT_APPROVED', 'AMENDMENT_REJECTED', 'AMENDMENT_WITHDRAWN',
  'AMENDMENT_DECIDED', 'CONTACT_IDENTITY_CHANGED',
]);

/**
 * Scoped, masked business-history timeline (plan §9.5/§19): applied revisions, PROPOSED amendments
 * (clearly separated — a proposal is never presented as active content), campus decisions and —
 * for request managers/HO only — the masked identity events. The server scopes the entries; this
 * component renders exactly what it was given.
 *
 * Sentences are built HERE from the structured event, so the timeline can be translated and never
 * shows an internal enum, a `source=` fragment or a `PENDING→APPLIED` arrow to a visitor.
 *
 * Times are formatted with the shared wall-clock helper: PEMS stores DATETIME as Vietnam local time,
 * so passing them through `new Date()` shifted every timestamp by the viewer's own offset.
 */
export default function VisitHistoryTimeline({ visitRequestId }: Props) {
  const { t } = useTranslation(['visitRequestV2']);
  const [history, setHistory] = useState<VisitRequestHistory | null>(null);
  const [error, setError] = useState(false);
  const [loading, setLoading] = useState(true);

  const load = useCallback(() => {
    let cancelled = false;
    setLoading(true);
    setError(false);
    getVisitRequestHistory(visitRequestId)
      .then(h => { if (!cancelled) setHistory(h); })
      .catch(() => { if (!cancelled) setError(true); })
      .finally(() => { if (!cancelled) setLoading(false); });
    return () => { cancelled = true; };
  }, [visitRequestId]);

  useEffect(() => load(), [load]);

  /** The one-line business sentence for an entry. */
  const describe = (e: VisitHistoryEntry): string => {
    const key = KNOWN_EVENTS.has(e.eventCode) ? e.eventCode : 'UNKNOWN';
    return t(`visitRequestV2:history.events.${key}`, {
      actor: e.actorName ?? t('visitRequestV2:history.someone'),
      campus: e.campusName ?? t('visitRequestV2:history.thisRequest'),
      formRevision: e.formRevision ?? 0,
      amendmentNo: e.amendmentNo ?? 0,
      email: e.maskedEmail ?? '',
    });
  };

  if (loading) {
    return (
      <p className="flex items-center gap-2 text-sm text-slate-500" role="status">
        <Loader2 className="h-4 w-4 animate-spin" aria-hidden /> {t('visitRequestV2:history.loading')}
      </p>
    );
  }

  if (error) {
    return (
      <div role="alert" className="rounded-xl border border-red-200 bg-red-50 p-3 text-sm text-red-700">
        <div className="flex items-start gap-2">
          <AlertCircle className="mt-0.5 h-4 w-4 shrink-0" aria-hidden />
          <p className="font-semibold">{t('visitRequestV2:history.loadFailed')}</p>
        </div>
        <button
          type="button"
          data-testid="history-retry"
          onClick={() => load()}
          className="mt-2 inline-flex items-center gap-1.5 rounded-lg border border-red-300 bg-white px-3 py-1.5 text-sm font-bold text-red-700 hover:bg-red-100"
        >
          <RefreshCw className="h-4 w-4" aria-hidden /> {t('visitRequestV2:detail.retry')}
        </button>
      </div>
    );
  }

  if (!history || history.entries.length === 0) {
    return <p className="text-sm italic text-slate-400">{t('visitRequestV2:history.empty')}</p>;
  }

  return (
    <ol
      aria-label={t('visitRequestV2:detail.historyTitle')}
      data-testid="visit-history-timeline"
      className="relative space-y-4 border-l-2 border-[#004c91]/20 pl-5"
    >
      {history.entries.map((e, idx) => {
        const emphasised = EMPHASISED_EVENTS.has(e.eventCode);
        return (
          <li key={`${e.at}-${idx}`} className="relative text-sm">
            <span
              aria-hidden
              className={`absolute -left-[27px] top-1 h-3 w-3 rounded-full ring-2 ring-white ${
                emphasised ? 'bg-[#f37021]' : 'bg-[#004c91]'
              }`}
            />
            <div className="flex flex-wrap items-center gap-2">
              <time className="text-xs font-medium text-slate-400" dateTime={e.at}>
                {formatVietnamDateTime(e.at)}
              </time>
              {e.eventCode === 'AMENDMENT_SUBMITTED' && (
                <span className="rounded bg-amber-100 px-1.5 py-0.5 text-[11px] font-semibold text-amber-800">
                  {t('visitRequestV2:history.notInForceYet')}
                </span>
              )}
            </div>
            <p className="mt-0.5 break-words font-semibold text-slate-800">{describe(e)}</p>
            {e.reason && (
              <p className="break-words text-xs text-slate-500">
                {t('visitRequestV2:history.reason')}: {e.reason}
              </p>
            )}
          </li>
        );
      })}
    </ol>
  );
}
