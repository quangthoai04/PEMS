import { useEffect, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { AlertCircle, Loader2, X } from 'lucide-react';
import {
  getVisitHistoryDetail,
  type VisitHistoryDetail,
  type VisitHistoryFieldChange,
} from '../api/visitRequestV2Api';
import { formatVietnamDateTime } from '../../../shared/utils/vietnamTime';

interface Props {
  visitRequestId: number;
  eventId: string;
  /** The sentence already shown on the timeline, so the drawer opens on familiar words. */
  eventLabel: string;
  onClose: () => void;
}

const MEMBER_FIELDS = ['fullName', 'jobTitle', 'organization', 'nationality'] as const;

/**
 * "What exactly changed" for one timeline event.
 *
 * Everything shown is a resolved before/after pair. The backend deliberately does not send the stored
 * snapshot JSON: those blobs carry every field of the form whether or not it moved, they are shaped
 * for storage rather than reading, and putting one in front of a Staff Leader would ask them to spot
 * a one-word difference in a wall of camelCase.
 */
export default function VisitHistoryDetailDrawer({ visitRequestId, eventId, eventLabel, onClose }: Props) {
  const { t } = useTranslation(['visitRequestV2']);
  const [detail, setDetail] = useState<VisitHistoryDetail | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(false);
  const closeRef = useRef<HTMLButtonElement>(null);

  useEffect(() => {
    let cancelled = false;
    setLoading(true);
    setError(false);
    getVisitHistoryDetail(visitRequestId, eventId)
      .then(d => { if (!cancelled) setDetail(d); })
      .catch(() => { if (!cancelled) setError(true); })
      .finally(() => { if (!cancelled) setLoading(false); });
    return () => { cancelled = true; };
  }, [visitRequestId, eventId]);

  // Escape closes, and focus lands on the close button — the drawer is opened from a keyboard-
  // reachable eye button, so it has to be leavable the same way.
  useEffect(() => {
    const onKeyDown = (e: KeyboardEvent) => {
      if (e.key === 'Escape') { e.stopPropagation(); onClose(); }
    };
    document.addEventListener('keydown', onKeyDown);
    closeRef.current?.focus();
    return () => document.removeEventListener('keydown', onKeyDown);
  }, [onClose]);

  const value = (v: string | null | undefined) =>
    v && v.trim() ? v : <span className="text-slate-400">{t('visitRequestV2:historyDetail.empty')}</span>;

  /**
   * Identity transitions store their before/after as workflow status codes. Left raw they reach the
   * screen as "PENDING → APPLIED"; known codes are translated and anything else falls through as-is
   * rather than being hidden.
   */
  const statusText = (fieldCode: string, raw: string | null): string | null => {
    if (fieldCode === 'operationalContactGuestMemberId') return relationText(raw);
    if (fieldCode !== 'identityChangeStatus' || !raw) return raw;
    return t(`visitRequestV2:historyDetail.identityStatus.${raw}`, { defaultValue: raw });
  };

  /**
   * The backend already resolved the relation to a NAME using that snapshot's own member list, or to
   * one of two stable codes when there is no name to give (plan CanhIter3FixBug §12) — never a raw
   * `GuestMemberId`. `NOT_IN_DELEGATION`/`UNRESOLVABLE` are translated the same way an identity status
   * code is; anything else is already the display text and passes through untouched.
   */
  const relationText = (raw: string | null): string | null => {
    if (raw !== 'NOT_IN_DELEGATION' && raw !== 'UNRESOLVABLE') return raw;
    return t(`visitRequestV2:historyDetail.relationStatus.${raw}`, { defaultValue: raw });
  };

  /**
   * The "before" cell. An absent history and an empty old value are NOT the same claim: the first says
   * nobody recorded what was there, the second says what was there was nothing. Rendering both as
   * "(trống)" is how the drawer came to assert that a filled-in field used to be blank.
   */
  const beforeCell = (f: VisitHistoryFieldChange) =>
    f.beforeUnknown
      ? <span className="italic text-slate-400">{t('visitRequestV2:historyDetail.unknownBefore')}</span>
      : value(statusText(f.fieldCode, f.beforeValue));

  const label = (labelKey: string, fieldCode: string) =>
    // Falls back to the raw field code only if the client has not been taught this field — better a
    // recognisable key than a blank column heading.
    t([labelKey, 'visitRequestV2:historyDetail.field.unknown'], { field: fieldCode });

  return (
    <div
      className="fixed inset-0 z-50 flex justify-end bg-black/40"
      onMouseDown={e => { if (e.target === e.currentTarget) onClose(); }}
    >
      <aside
        role="dialog"
        aria-modal="true"
        aria-label={t('visitRequestV2:historyDetail.title')}
        data-testid="history-detail-drawer"
        className="h-full w-full max-w-lg overflow-y-auto bg-white p-5 shadow-2xl"
      >
        <div className="mb-4 flex items-start justify-between gap-3">
          <div className="min-w-0">
            <h2 className="text-base font-extrabold text-[#004c91]">
              {t('visitRequestV2:historyDetail.title')}
            </h2>
            <p className="mt-0.5 break-words text-sm text-slate-600">{eventLabel}</p>
          </div>
          <button
            ref={closeRef}
            type="button"
            onClick={onClose}
            data-testid="history-detail-close"
            aria-label={t('visitRequestV2:common.cancel')}
            className="rounded p-1 text-slate-500 hover:bg-slate-100"
          >
            <X className="h-5 w-5" />
          </button>
        </div>

        {loading && (
          <p role="status" className="flex items-center gap-2 text-sm text-slate-500">
            <Loader2 className="h-4 w-4 animate-spin" aria-hidden />
            {t('visitRequestV2:historyDetail.loading')}
          </p>
        )}

        {error && (
          <div role="alert" className="flex items-start gap-2 rounded-xl border border-red-200 bg-red-50 p-3 text-sm text-red-700">
            <AlertCircle className="mt-0.5 h-4 w-4 shrink-0" aria-hidden />
            <p>{t('visitRequestV2:historyDetail.loadFailed')}</p>
          </div>
        )}

        {detail && !loading && !error && (
          <>
            <dl className="mb-4 grid grid-cols-1 gap-x-4 gap-y-1 rounded-xl bg-slate-50 px-3 py-2 text-sm sm:grid-cols-2">
              <div className="flex gap-2">
                <dt className="font-semibold text-slate-600">{t('visitRequestV2:historyDetail.actor')}</dt>
                <dd className="text-slate-900">{value(detail.actorName)}</dd>
              </div>
              <div className="flex gap-2">
                <dt className="font-semibold text-slate-600">{t('visitRequestV2:historyDetail.at')}</dt>
                <dd className="text-slate-900">{formatVietnamDateTime(detail.occurredAt)}</dd>
              </div>
              {detail.campusName && (
                <div className="flex gap-2">
                  <dt className="font-semibold text-slate-600">{t('visitRequestV2:historyDetail.campus')}</dt>
                  <dd className="text-slate-900">{detail.campusName}</dd>
                </div>
              )}
              {(detail.beforeRevision != null || detail.afterRevision != null) && (
                <div className="flex gap-2">
                  <dt className="font-semibold text-slate-600">{t('visitRequestV2:historyDetail.revision')}</dt>
                  <dd className="text-slate-900">
                    {detail.beforeRevision ?? '—'} → {detail.afterRevision ?? '—'}
                  </dd>
                </div>
              )}
              {detail.reason && (
                <div className="flex gap-2 sm:col-span-2">
                  <dt className="font-semibold text-slate-600">{t('visitRequestV2:historyDetail.reason')}</dt>
                  <dd className="break-words text-slate-900">{detail.reason}</dd>
                </div>
              )}
            </dl>

            {detail.fieldChanges.length > 0 && (
              <div className="mb-4 overflow-x-auto rounded-xl border border-slate-200">
                <table data-testid="history-detail-fields" className="w-full min-w-[420px] border-collapse text-sm">
                  <thead>
                    <tr className="bg-slate-50 text-left">
                      <th scope="col" className="border-b border-slate-200 px-3 py-2 text-xs font-bold uppercase tracking-wide text-slate-600">
                        {t('visitRequestV2:historyDetail.colField')}
                      </th>
                      <th scope="col" className="border-b border-slate-200 px-3 py-2 text-xs font-bold uppercase tracking-wide text-slate-600">
                        {t('visitRequestV2:historyDetail.colBefore')}
                      </th>
                      <th scope="col" className="border-b border-slate-200 px-3 py-2 text-xs font-bold uppercase tracking-wide text-slate-600">
                        {t('visitRequestV2:historyDetail.colAfter')}
                      </th>
                    </tr>
                  </thead>
                  <tbody>
                    {detail.fieldChanges.map(f => (
                      <tr key={f.fieldCode} className="odd:bg-white even:bg-slate-50/60">
                        <td className="border-b border-slate-100 px-3 py-2 font-medium text-slate-800">
                          {label(f.labelKey, f.fieldCode)}
                        </td>
                        <td
                          data-testid={`history-detail-before-${f.fieldCode}`}
                          className={`border-b border-slate-100 px-3 py-2 text-slate-600 ${
                            f.beforeUnknown ? '' : 'line-through decoration-slate-300'
                          }`}
                        >
                          {beforeCell(f)}
                        </td>
                        <td className="border-b border-slate-100 px-3 py-2 font-normal text-slate-900">
                          {value(statusText(f.fieldCode, f.afterValue))}
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}

            {detail.collectionChanges.length > 0 && (
              <ul data-testid="history-detail-collections" className="mb-4 space-y-2">
                {detail.collectionChanges.map((c, i) => (
                  <li key={`${c.collectionCode}-${c.itemKey}-${i}`} className="rounded-xl border border-slate-200 p-3">
                    <p className="flex flex-wrap items-center gap-2">
                      <span
                        className={`rounded px-1.5 py-0.5 text-[11px] font-bold ${
                          c.changeType === 'ADDED' ? 'bg-green-100 text-green-800'
                            : c.changeType === 'REMOVED' ? 'bg-red-100 text-red-800'
                              : 'bg-amber-100 text-amber-800'
                        }`}
                      >
                        {t(`visitRequestV2:historyDetail.changeType.${c.changeType}`)}
                      </span>
                      <span className="text-xs font-semibold text-slate-500">
                        {t(`visitRequestV2:historyDetail.collection.${c.collectionCode}`)}
                      </span>
                      <span className="font-semibold text-slate-900">{value(c.itemKey)}</span>
                    </p>
                    {/* Only shown for a correction: for a join or a departure the person's details
                        are not "before and after", they simply are. */}
                    {c.changeType === 'UPDATED' && c.before && c.after && (
                      <dl className="mt-1.5 space-y-0.5 text-xs">
                        {MEMBER_FIELDS.filter(f => c.before![f] !== c.after![f]).map(f => (
                          <div key={f} className="flex flex-wrap gap-1.5">
                            <dt className="font-semibold text-slate-600">
                              {t(`visitRequestV2:historyDetail.field.${f}`)}:
                            </dt>
                            <dd className="text-slate-500 line-through decoration-slate-300">
                              {value(c.before![f])}
                            </dd>
                            <dd className="font-normal text-slate-900">→ {value(c.after![f])}</dd>
                          </div>
                        ))}
                      </dl>
                    )}
                  </li>
                ))}
              </ul>
            )}

            {/* Danh sách rỗng có HAI nguyên nhân hoàn toàn khác nhau, và backend nói rõ là cái nào
                qua `comparisonStatus`. Trước đây cả hai đều in "không có thay đổi chi tiết nào được
                ghi nhận" — nói với người vừa sửa đơn rằng họ không sửa gì. */}
            {detail.fieldChanges.length === 0 && detail.collectionChanges.length === 0 && (
              detail.comparisonStatus === 'PREVIOUS_REVISION_MISSING' ? (
                <p
                  role="alert"
                  data-testid="history-detail-baseline-missing"
                  className="rounded-lg border border-amber-200 bg-amber-50 px-3 py-2 text-sm text-amber-800"
                >
                  {t('visitRequestV2:historyDetail.previousRevisionMissing')}
                </p>
              ) : (
                <p data-testid="history-detail-empty" className="text-sm italic text-slate-400">
                  {t('visitRequestV2:historyDetail.noChanges')}
                </p>
              )
            )}
          </>
        )}
      </aside>
    </div>
  );
}
