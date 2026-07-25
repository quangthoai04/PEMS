import React from 'react';
import { useTranslation } from 'react-i18next';
import type { ResolvedVisitForm } from '../../../api/visitRequestV2Api';
import { formatVietnamDateTime } from '../../../../../shared/utils/vietnamTime';

interface Props {
  form: ResolvedVisitForm;
}

/**
 * "Where has this request actually got to?" in one sentence, for whoever opened the page.
 *
 * The request-level status alone does not answer that on a multi-campus request: PARTIALLY_APPROVED
 * says nothing about which campus is still deciding, and a reader has to open every card to find out.
 * This counts the campus instances instead — and counts ONLY the ones the backend returned to this
 * caller, so a Staff Leader scoped to one campus never learns how many other campuses exist, what they
 * decided, who decided it, or why. Nothing here is derived from a request-level total.
 */
export const VisitOutcomeSummary: React.FC<Props> = ({ form }) => {
  const { t } = useTranslation(['visitRequestV2']);
  const campuses = form.campusVisits;

  // Buckets follow the visit_request_campuses enum; ASSIGNED is "accepted and hosted", the three
  // in-flight states are lumped together because a reader of this line only needs "it is under way".
  const waiting = campuses.filter(c => c.instanceStatus === 'WAITING_REQUEST_APPROVAL').length;
  const accepted = campuses.filter(c => c.instanceStatus === 'ASSIGNED').length;
  const inProgress = campuses.filter(
    c => c.instanceStatus === 'BEFORE_VISIT' || c.instanceStatus === 'DURING_VISIT' || c.instanceStatus === 'AFTER_VISIT',
  ).length;
  const rejected = campuses.filter(c => c.instanceStatus === 'REJECTED').length;
  const cancelled = campuses.filter(c => c.instanceStatus === 'CANCELLED').length;
  const closed = campuses.filter(c => c.instanceStatus === 'CLOSED').length;

  const parts: string[] = [];
  if (accepted > 0) parts.push(t('visitRequestV2:outcome.accepted', { count: accepted }));
  if (inProgress > 0) parts.push(t('visitRequestV2:outcome.inProgress', { count: inProgress }));
  if (waiting > 0) parts.push(t('visitRequestV2:outcome.waiting', { count: waiting }));
  if (rejected > 0) parts.push(t('visitRequestV2:outcome.rejected', { count: rejected }));
  if (cancelled > 0) parts.push(t('visitRequestV2:outcome.cancelled', { count: cancelled }));
  if (closed > 0) parts.push(t('visitRequestV2:outcome.closed', { count: closed }));

  const everyCampusRejected = campuses.length > 0 && rejected === campuses.length;
  const requestCancelled = form.requestStatus === 'CANCELLED';

  /** The headline sentence: the clearest true statement about where things stand. */
  let headline: string;
  if (campuses.length === 0) headline = t('visitRequestV2:outcome.noCampus');
  else if (requestCancelled) headline = t('visitRequestV2:outcome.allCancelled');
  else if (everyCampusRejected) headline = t('visitRequestV2:outcome.allRejected');
  else if (waiting === campuses.length) headline = t('visitRequestV2:outcome.waitingAll', { count: waiting });
  else headline = parts.join(' · ');

  // A request that ended in more than one way needs a pointer to the per-campus cards, because no
  // single sentence can carry every reason.
  const mixedOutcome = !requestCancelled
    && [accepted + inProgress + closed, waiting, rejected, cancelled].filter(n => n > 0).length > 1;

  // How the request ENDED, when it ended: who did it, when, and why. Without this the reader is left
  // with a bare "Đã hủy" and has to dig through the timeline to find out what happened.
  const cancellationRows = requestCancelled
    ? [
        { label: t('visitRequestV2:outcome.cancelledAt'), value: form.cancelledAt ? formatVietnamDateTime(form.cancelledAt) : null },
        { label: t('visitRequestV2:outcome.cancelledBy'), value: form.cancelledByName },
        { label: t('visitRequestV2:outcome.cancellationReason'), value: form.cancellationReason },
      ].filter(r => r.value)
    : [];

  // The most recent rejection IN SCOPE — never a request-level roll-up, so a scoped viewer only ever
  // sees a decision made on a campus they are allowed to see.
  const latestRejection = campuses
    .filter(c => c.instanceStatus === 'REJECTED' && c.decidedAt)
    .sort((a, b) => (a.decidedAt! < b.decidedAt! ? 1 : -1))[0];
  const rejectionRows = latestRejection
    ? [
        { label: t('visitRequestV2:outcome.latestRejection'), value: formatVietnamDateTime(latestRejection.decidedAt!) },
        { label: t('visitRequestV2:outcome.rejectedBy'), value: latestRejection.decidedByName },
        { label: t('visitRequestV2:outcome.rejectionReason'), value: latestRejection.decisionNote },
      ].filter(r => r.value)
    : [];

  // Cancellation is the final word on a request, so it outranks an earlier campus rejection.
  const detailRows = cancellationRows.length > 0 ? cancellationRows : rejectionRows;

  return (
    <div
      data-testid="visit-outcome-summary"
      className="rounded-xl border border-slate-200 bg-slate-50 p-3 sm:p-4"
    >
      <p className="text-xs font-bold uppercase tracking-wide text-slate-500">
        {t('visitRequestV2:outcome.title')}
      </p>
      <p className="mt-1 text-sm font-semibold text-slate-800">{headline}</p>
      {mixedOutcome && (
        <p className="mt-0.5 text-xs text-slate-500">{t('visitRequestV2:outcome.seePerCampus')}</p>
      )}
      {detailRows.length > 0 && (
        <dl className="mt-3 space-y-1 border-t border-slate-200 pt-3 text-sm">
          {detailRows.map(row => (
            <div key={row.label} className="flex flex-wrap gap-x-2">
              <dt className="text-slate-500">{row.label}:</dt>
              <dd className="min-w-0 break-words font-medium text-slate-800">{row.value}</dd>
            </div>
          ))}
        </dl>
      )}
    </div>
  );
};
