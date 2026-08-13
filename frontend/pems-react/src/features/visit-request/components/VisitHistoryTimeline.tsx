import { useCallback, useEffect, useState } from 'react';
import axios from 'axios';
import { AlertCircle, Eye, Loader2, Lock, RefreshCw } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { getVisitRequestHistory, type VisitHistoryEntry, type VisitRequestHistory } from '../api/visitRequestV2Api';
import VisitHistoryDetailDrawer from './VisitHistoryDetailDrawer';
import { formatVietnamDateTime } from '../../../shared/utils/vietnamTime';

interface Props {
  visitRequestId: number;
  /**
   * Bumped by the parent after any mutation that writes history — a contact edit, an invitation sent,
   * resent or cancelled, an accept or a decline. This component owns its own fetch, so reloading the
   * request detail alone left the timeline showing the state from before the action the user just
   * took, on the very screen where they took it.
   */
  refreshKey?: number;
}

/** Decisions and outcomes are the turning points of a request — they get the accent. */
const EMPHASISED_EVENTS = new Set([
  'INSTANCE_APPROVED', 'INSTANCE_REJECTED', 'INSTANCE_CANCELLED', 'INSTANCE_DECIDED',
  'AMENDMENT_APPROVED', 'AMENDMENT_REJECTED', 'AMENDMENT_DECIDED',
  'REQUEST_CANCELLED', 'REQUEST_RESUBMITTED',
  // Who runs the visit changing is a turning point for everyone reading this timeline.
  'HOST_TRANSFERRED',
  // So is the campus acquiring — or losing — the person who coordinates it.
  'CONTACT_CONFIRMED', 'CONTACT_TRANSFER_ACCEPTED',
  'CONTACT_CONFIRMATION_DECLINED', 'CONTACT_TRANSFER_DECLINED', 'CONTACT_INVITATION_EXPIRED',
]);

/** Event codes the client knows how to phrase; anything else degrades to a neutral sentence. */
const KNOWN_EVENTS = new Set([
  'REQUEST_REVISION', 'REQUEST_CREATED', 'REQUEST_SAFE_EDIT_APPLIED', 'REQUEST_RESUBMITTED',
  'REQUEST_CANCELLED',
  'INSTANCE_CONTENT_CREATED', 'INSTANCE_CONTENT_REVISED', 'INSTANCE_SAFE_EDIT_APPLIED',
  'INSTANCE_CONTENT_RESUBMITTED', 'INSTANCE_AMENDMENT_APPLIED',
  'INSTANCE_APPROVED', 'INSTANCE_REJECTED', 'INSTANCE_CANCELLED', 'INSTANCE_CLOSED', 'INSTANCE_DECIDED',
  'AMENDMENT_SUBMITTED', 'AMENDMENT_APPROVED', 'AMENDMENT_REJECTED', 'AMENDMENT_WITHDRAWN',
  'AMENDMENT_DECIDED',
  // The contact workflow speaks in transitions, not in one word for all of them: an invitation sent,
  // resent, cancelled, accepted, declined or expired each need a different thing done next, and
  // "vai trò đầu mối có thay đổi" told the reader none of them. The generic code remains as the
  // fallback for a transition this client has not been taught.
  'CONTACT_IDENTITY_CHANGED',
  'CONTACT_INITIAL_CONFIRMATION_CREATED', 'CONTACT_TRANSFER_REQUESTED', 'CONTACT_INVITATION_RESENT',
  'CONTACT_INVITATION_CANCELLED', 'CONTACT_INVITATION_SUPERSEDED', 'CONTACT_CONFIRMED',
  'CONTACT_TRANSFER_ACCEPTED', 'CONTACT_CONFIRMATION_DECLINED', 'CONTACT_TRANSFER_DECLINED',
  'CONTACT_INVITATION_EXPIRED',
  // A full edit of a pending request is its own event — it used to be reported as a quick edit.
  'REQUEST_PENDING_EDIT_APPLIED', 'INSTANCE_PENDING_EDIT_APPLIED',
  'HOST_TRANSFERRED',
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
export default function VisitHistoryTimeline({ visitRequestId, refreshKey = 0 }: Props) {
  const { t } = useTranslation(['visitRequestV2']);
  const [history, setHistory] = useState<VisitRequestHistory | null>(null);
  /**
   * Two different failures, kept apart because they need opposite treatment. `forbidden` is the
   * backend's ANSWER — retrying it produces the same 403 forever — while `generic` is a network or
   * server fault that a retry may well fix. Collapsing both into one boolean is what put a Retry
   * button under an authorization refusal.
   *
   * The parent only mounts this component when the read model granted VIEW_CHANGE_HISTORY, so
   * `forbidden` here means the capability went stale between the two calls (a handover completed, a
   * contact was transferred). Rare, and still not something to offer a retry for.
   */
  const [error, setError] = useState<'forbidden' | 'generic' | null>(null);
  const [loading, setLoading] = useState(true);
  // The drawer carries the timeline's own sentence, so opening a detail keeps the same words on
  // screen rather than re-describing the event in a second vocabulary.
  const [openEvent, setOpenEvent] = useState<{ eventId: string; label: string } | null>(null);

  const load = useCallback(() => {
    let cancelled = false;
    setLoading(true);
    setError(null);
    getVisitRequestHistory(visitRequestId)
      .then(h => { if (!cancelled) setHistory(h); })
      .catch(err => {
        if (cancelled) return;
        // Read from the STATUS, never from the message text: the refusal carries a Vietnamese
        // sentence that translation or rewording would silently break this branch.
        const status = axios.isAxiosError(err) ? err.response?.status : undefined;
        setError(status === 403 ? 'forbidden' : 'generic');
      })
      .finally(() => { if (!cancelled) setLoading(false); });
    return () => { cancelled = true; };
    // refreshKey is a deliberate dependency: it is the parent saying "something you show has changed".
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [visitRequestId, refreshKey]);

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

  // An authorization refusal states itself and stops. No Retry: the answer will not change by asking
  // again, and a button that reliably fails is worse than none.
  if (error === 'forbidden') {
    return (
      <p
        role="status"
        data-testid="history-forbidden"
        className="flex items-start gap-2 rounded-xl border border-slate-200 bg-slate-50 p-3 text-sm text-slate-600"
      >
        <Lock className="mt-0.5 h-4 w-4 shrink-0" aria-hidden />
        {t('visitRequestV2:history.forbidden')}
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
    <>
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
            <div className="mt-0.5 flex items-start gap-1.5">
              <p className="min-w-0 break-words font-semibold text-slate-800">{describe(e)}</p>
              {/* Offered ONLY where the backend has a diff to show. A decision or a cancellation
                  states its outcome on this line, so an eye button there would open a drawer that
                  repeats what the reader just read. */}
              {e.eventId && (
                <button
                  type="button"
                  data-testid={`history-detail-open-${e.eventId}`}
                  onClick={() => setOpenEvent({ eventId: e.eventId!, label: describe(e) })}
                  title={t('visitRequestV2:historyDetail.open')}
                  aria-label={t('visitRequestV2:historyDetail.openFor', { event: describe(e) })}
                  className="shrink-0 rounded p-1 text-slate-400 hover:bg-slate-100 hover:text-[#004c91] focus:outline-none focus-visible:ring-2 focus-visible:ring-[#004c91]"
                >
                  <Eye className="h-4 w-4" aria-hidden />
                </button>
              )}
            </div>
            {e.reason && (
              <p className="break-words text-xs text-slate-500">
                {t('visitRequestV2:history.reason')}: {e.reason}
              </p>
            )}
          </li>
        );
      })}
      </ol>

      {openEvent && (
        <VisitHistoryDetailDrawer
          visitRequestId={visitRequestId}
          eventId={openEvent.eventId}
          eventLabel={openEvent.label}
          onClose={() => setOpenEvent(null)}
        />
      )}
    </>
  );
}
