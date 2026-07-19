import React, { useState } from 'react';
import { Building2, ChevronDown, Clock } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import type { ResolvedCampusVisit } from '../../api/visitRequestV2Api';
import { formatVietnamDateTime } from '../../../../shared/utils/vietnamTime';

interface Props {
  campus: ResolvedCampusVisit;
  /** Optional slot rendered under the decision block (e.g. the pending-amendment panel). */
  children?: React.ReactNode;
}

const STATUS_STYLES: Record<string, string> = {
  PENDING: 'bg-amber-100 text-amber-800',
  ASSIGNED: 'bg-blue-100 text-blue-800',
  APPROVED: 'bg-green-100 text-green-800',
  REJECTED: 'bg-red-100 text-red-700',
  CANCELLED: 'bg-gray-200 text-gray-600',
  COMPLETED: 'bg-emerald-100 text-emerald-800',
};

/**
 * ONE campus's read-only snapshot (plan §9.2) — the SAME component everywhere a per-campus
 * view is shown (post-submit summary, visitor/staff detail, review screens). It renders
 * exactly the scoped payload it receives; it never summarizes siblings, never picks a
 * "representative" campus, and request-level data is NOT repeated here.
 */
export const CampusVisitDetailCard: React.FC<Props> = ({ campus, children }) => {
  const { t, i18n } = useTranslation(['visitRequestV2', 'visitRequest']);
  const [peopleOpen, setPeopleOpen] = useState(false);
  const locale = i18n.language === 'vi' ? 'vi-VN' : 'en-US';

  const row = (label: string, value: React.ReactNode) =>
    value == null || value === '' ? null : (
      <div className="grid grid-cols-1 gap-0.5 sm:grid-cols-[180px_1fr] sm:gap-3">
        <dt className="text-xs font-semibold uppercase tracking-wide text-slate-400">{label}</dt>
        <dd className="text-sm text-slate-800 dark:text-slate-100 break-words">{value}</dd>
      </div>
    );

  const statusCls = STATUS_STYLES[campus.instanceStatus] ?? 'bg-slate-100 text-slate-700';
  const peopleId = `cvd-people-${campus.visitInstanceId}`;

  return (
    <section
      aria-label={t('visitRequestV2:detail.cardAria', { campus: campus.campusName })}
      className="rounded-2xl border border-slate-200 dark:border-slate-700 bg-white dark:bg-slate-800 p-4 sm:p-5 shadow-sm"
    >
      {/* Header: campus + schedule + status */}
      <div className="flex flex-wrap items-center gap-2">
        <Building2 className="h-5 w-5 shrink-0 text-[#004c91]" aria-hidden />
        <h3 className="text-base font-extrabold text-slate-900 dark:text-slate-50">{campus.campusName}</h3>
        <span className={`rounded-full px-2.5 py-0.5 text-xs font-bold ${statusCls}`}>
          {t(`visitRequestV2:status.${campus.instanceStatus}`, campus.instanceStatus)}
        </span>
        {campus.activeAmendment && (
          <span className="rounded-full bg-amber-100 px-2.5 py-0.5 text-xs font-bold text-amber-800" role="status">
            {t('visitRequestV2:detail.amendmentBadge', { no: campus.activeAmendment.amendmentNo })}
          </span>
        )}
        <span className="ml-auto flex items-center gap-1 text-xs font-medium text-slate-500">
          <Clock className="h-3.5 w-3.5" aria-hidden />
          <time dateTime={campus.plannedStartAt}>{formatVietnamDateTime(campus.plannedStartAt)}</time>
          {' → '}
          <time dateTime={campus.plannedEndAt}>{formatVietnamDateTime(campus.plannedEndAt)}</time>
        </span>
      </div>

      {/* Per-campus content */}
      <dl className="mt-4 space-y-2.5">
        {row(t('visitRequestV2:card.delegationName'), campus.delegationName)}
        {row(
          t('visitRequestV2:card.visitType'),
          campus.visitType === 'OTHER' && campus.visitTypeOther
            ? campus.visitTypeOther
            : t(`visitRequest:step2Info.visitTypes.${campus.visitType}`, campus.visitType),
        )}
        {row(t('visitRequestV2:card.purpose'), campus.purpose)}
        {row(t('visitRequestV2:card.workingContent'), campus.workingContent)}
        {row(
          t('visitRequestV2:card.workingLanguage'),
          campus.workingLanguage === 'VI' ? t('visitRequestV2:card.languageVi') : t('visitRequestV2:card.languageEn'),
        )}
        {row(
          t('visitRequestV2:card.mediaConsent'),
          campus.mediaConsentStatus === 'AGREED'
            ? `${t('visitRequestV2:card.mediaAgreed')}${campus.mediaConsentNote ? ` — ${campus.mediaConsentNote}` : ''}`
            : t('visitRequestV2:card.mediaDeclined'),
        )}
        {row(t('visitRequestV2:card.transportationNote'), campus.transportationNote)}
        {row(t('visitRequestV2:card.notes'), campus.noteToFptu)}
        {row(
          t('visitRequestV2:card.operationalContact'),
          campus.operationalContact.fullName
            ? [campus.operationalContact.fullName, campus.operationalContact.organization,
               campus.operationalContact.phone, campus.operationalContact.email]
                .filter(Boolean).join(' · ')
            : null,
        )}
      </dl>

      {/* People — collapsible, keyboard reachable */}
      <div className="mt-4">
        <button
          type="button"
          aria-expanded={peopleOpen}
          aria-controls={peopleId}
          className="flex items-center gap-1.5 text-sm font-bold text-[#004c91] hover:underline"
          onClick={() => setPeopleOpen(o => !o)}
        >
          <ChevronDown className={`h-4 w-4 transition-transform ${peopleOpen ? 'rotate-180' : ''}`} aria-hidden />
          {t('visitRequestV2:detail.peopleToggle', {
            visitors: campus.visitors.length,
            support: campus.supportMembers.length,
          })}
        </button>
        <div id={peopleId} className={peopleOpen ? 'mt-3 space-y-4' : 'hidden'}>
          {([['visitors', campus.visitors], ['supportMembers', campus.supportMembers]] as const).map(([kind, list]) =>
            list.length === 0 ? null : (
              <div key={kind} className="overflow-x-auto">
                <table className="w-full min-w-[480px] text-left text-xs">
                  <caption className="mb-1 caption-top text-left text-xs font-bold text-slate-600 dark:text-slate-300">
                    {kind === 'visitors' ? t('visitRequestV2:card.visitors') : t('visitRequestV2:card.supportTeam')}
                  </caption>
                  <thead>
                    <tr className="border-b border-slate-200 dark:border-slate-600 text-slate-500">
                      <th scope="col" className="py-1 pr-2 font-medium">{t('visitRequestV2:person.fullName')}</th>
                      <th scope="col" className="py-1 pr-2 font-medium">{t('visitRequestV2:person.jobTitle')}</th>
                      <th scope="col" className="py-1 pr-2 font-medium">{t('visitRequestV2:person.organization')}</th>
                      <th scope="col" className="py-1 font-medium">{t('visitRequestV2:person.nationality')}</th>
                    </tr>
                  </thead>
                  <tbody>
                    {list.map(m => (
                      <tr key={m.guestMemberId} className="border-b border-slate-100 dark:border-slate-700">
                        <td className="py-1.5 pr-2 font-medium text-slate-800 dark:text-slate-100">{m.fullName}</td>
                        <td className="py-1.5 pr-2 text-slate-600 dark:text-slate-300">{m.jobTitle}</td>
                        <td className="py-1.5 pr-2 text-slate-600 dark:text-slate-300">{m.organization}</td>
                        <td className="py-1.5 text-slate-600 dark:text-slate-300">{m.nationality}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            ))}
        </div>
      </div>

      {/* Host / decision / revision */}
      <div className="mt-4 rounded-xl bg-slate-50 dark:bg-slate-700/40 p-3">
        <dl className="space-y-1.5">
          {row(t('visitRequestV2:detail.host'), campus.currentHostName)}
          {campus.decidedAt &&
            row(
              t('visitRequestV2:detail.decision'),
              `${campus.decidedByName ?? '—'} · ${new Date(campus.decidedAt).toLocaleString(locale)}${
                campus.decisionNote ? ` · ${campus.decisionNote}` : ''
              }`,
            )}
          {row(
            t('visitRequestV2:detail.revision'),
            t('visitRequestV2:detail.revisionValue', {
              form: campus.formRevision,
              approval: campus.approvalRevision,
            }),
          )}
        </dl>
      </div>

      {children}
    </section>
  );
};
