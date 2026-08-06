import React from 'react';
import { useTranslation } from 'react-i18next';
import type { ResolvedVisitForm } from '../../../api/visitRequestV2Api';
import { formatVietnamDateTime } from '../../../../../shared/utils/vietnamTime';

interface Props {
  form: ResolvedVisitForm;
}

/**
 * "Where has this got to?" in one sentence, for whoever opened the page.
 *
 * Two different sentences, because there are two different readers.
 *
 * A caller who sees every campus gets the REQUEST's outcome, and gets it from the backend
 * (`requestOutcome`), which counted it over every campus of the request.
 *
 * A caller scoped to a subset — a Staff Leader on their own campus — gets a sentence about THOSE
 * CAMPUSES BY NAME, and never a request-level word. This is the part that was wrong: the component
 * used to compute "rejected at every campus" from `campusVisits`, which is the permission-scoped
 * list. A Staff Leader whose one visible campus had refused was told the whole request was dead
 * while a sibling campus was still deciding it. A scoped slice cannot be told apart from the whole,
 * so the safe rule is that it is never allowed to speak for the whole.
 */
export const VisitOutcomeSummary: React.FC<Props> = ({ form }) => {
  const { t } = useTranslation(['visitRequestV2']);
  const campuses = form.campusVisits;
  const outcome = form.requestOutcome;

  // Buckets over the VISIBLE campuses — used for the scoped sentence, and for the count line, which
  // has always been "of what you can see" and is labelled as such.
  const waiting = campuses.filter(
    c => c.instanceStatus === 'WAITING_REQUEST_APPROVAL' || c.instanceStatus === 'WAITING_CONTACT_CONFIRMATION',
  ).length;
  const accepted = campuses.filter(c => c.instanceStatus === 'ASSIGNED').length;
  const inProgress = campuses.filter(
    c => c.instanceStatus === 'BEFORE_VISIT' || c.instanceStatus === 'DURING_VISIT' || c.instanceStatus === 'AFTER_VISIT',
  ).length;
  const rejected = campuses.filter(c => c.instanceStatus === 'REJECTED').length;
  const cancelled = campuses.filter(c => c.instanceStatus === 'CANCELLED').length;
  const closed = campuses.filter(c => c.instanceStatus === 'CLOSED').length;

  const counts = (a: number, ip: number, w: number, r: number, ca: number, cl: number) => {
    const parts: string[] = [];
    if (a > 0) parts.push(t('visitRequestV2:outcome.accepted', { count: a }));
    if (ip > 0) parts.push(t('visitRequestV2:outcome.inProgress', { count: ip }));
    if (w > 0) parts.push(t('visitRequestV2:outcome.waiting', { count: w }));
    if (r > 0) parts.push(t('visitRequestV2:outcome.rejected', { count: r }));
    if (ca > 0) parts.push(t('visitRequestV2:outcome.cancelled', { count: ca }));
    if (cl > 0) parts.push(t('visitRequestV2:outcome.closed', { count: cl }));
    return parts.join(' · ');
  };

  /** Names the campuses in scope, so a scoped reader knows exactly which campus the verdict is about. */
  const namesOf = (status: string) =>
    campuses.filter(c => c.instanceStatus === status).map(c => c.campusName).filter(Boolean).join(', ');

  let headline: string;
  if (campuses.length === 0) {
    headline = t('visitRequestV2:outcome.noCampus');
  } else if (outcome) {
    // Full-request scope: the backend's verdict, verbatim.
    if (outcome.code === 'ALL_CANCELLED') headline = t('visitRequestV2:outcome.allCancelled');
    else if (outcome.code === 'ALL_REJECTED') headline = t('visitRequestV2:outcome.allRejected');
    else if (outcome.code === 'ALL_WAITING') headline = t('visitRequestV2:outcome.waitingAll', { count: outcome.waiting });
    else if (outcome.code === 'NO_CAMPUS') headline = t('visitRequestV2:outcome.noCampus');
    else headline = counts(outcome.accepted, outcome.inProgress, outcome.waiting, outcome.rejected, outcome.cancelled, outcome.closed);
  } else if (rejected === campuses.length) {
    // Scoped: say which campus refused, and say nothing about the request.
    headline = t('visitRequestV2:outcome.scopedRejected', { campuses: namesOf('REJECTED'), count: rejected });
  } else if (cancelled === campuses.length) {
    headline = t('visitRequestV2:outcome.scopedCancelled', { campuses: namesOf('CANCELLED'), count: cancelled });
  } else {
    headline = counts(accepted, inProgress, waiting, rejected, cancelled, closed);
  }

  // A reader whose scope holds more than one kind of ending needs a pointer to the per-campus cards,
  // because no single sentence can carry every reason.
  const mixedOutcome =
    [accepted + inProgress + closed, waiting, rejected, cancelled].filter(n => n > 0).length > 1;

  // Only a full-scope reader is told this is the whole request; everyone else is told it is their
  // scope, so a count of 1/1 is never read as "the request has one campus".
  const scopeNote = outcome
    ? null
    : t('visitRequestV2:outcome.scopeNote', { count: campuses.length });

  // How the request ENDED, when it ended: who did it, when, and why. Request-level cancellation is a
  // request-level fact, so it is shown only to a reader who has the request in scope.
  const cancellationRows = outcome && form.requestStatus === 'CANCELLED'
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
      {scopeNote && (
        <p data-testid="visit-outcome-scope-note" className="mt-0.5 text-xs text-slate-500">{scopeNote}</p>
      )}
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
