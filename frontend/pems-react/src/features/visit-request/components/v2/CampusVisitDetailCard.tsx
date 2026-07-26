import React from 'react';
import { Building2, Clock } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import type { ResolvedCampusVisit } from '../../api/visitRequestV2Api';
import { formatVietnamDateTime } from '../../../../shared/utils/vietnamTime';
import { VisitStatusBadge } from './shared/VisitStatusBadge';
import { ReadOnlyInfoGrid, type InfoRow } from './shared/ReadOnlyInfoGrid';
import { PersonListTable } from './shared/PersonListTable';
import { resolveCampusRevisionState } from './shared/campusRevisionState';

interface Props {
  campus: ResolvedCampusVisit;
  /** Optional slot rendered under the decision block (e.g. the pending-amendment panel). */
  children?: React.ReactNode;
}

/**
 * ONE campus's read-only snapshot (plan §9.2/§16) — the SAME component everywhere a per-campus
 * view is shown (post-submit summary, visitor/staff detail, review screens). It renders exactly
 * the scoped payload it receives; it never summarizes siblings, never picks a "representative"
 * campus, and request-level data is NOT repeated here.
 *
 * Everything a reader needs is on the page: the people lists are no longer hidden behind a
 * collapsed toggle, because "who is coming" is the first thing anyone opens this card for.
 */
export const CampusVisitDetailCard: React.FC<Props> = ({ campus, children }) => {
  const { t } = useTranslation(['visitRequestV2', 'visitRequest']);

  const visitTypeLabel = campus.visitType === 'OTHER' && campus.visitTypeOther
    ? campus.visitTypeOther
    : t(`visitRequest:step2Info.visitTypes.${campus.visitType}`, campus.visitType);

  // Approval wording comes from the lifecycle, never from approvalRevision — that number is 1 from
  // the moment the request is created, long before anybody decides anything.
  const revisionState = resolveCampusRevisionState({
    instanceStatus: campus.instanceStatus,
    formRevision: campus.formRevision,
    approvalRevision: campus.approvalRevision,
    decidedAt: campus.decidedAt,
    activeAmendmentNo: campus.activeAmendment?.amendmentNo ?? null,
  });

  // One label for approving, rejecting and cancelling reads as an admin field, and it puts a
  // rejection reason under a heading that says "note". decision_note carries a different KIND of
  // sentence in each outcome, so it gets the heading that outcome actually earned.
  const decisionNoteLabel =
    campus.instanceStatus === 'REJECTED' ? t('visitRequestV2:detail.decisionNoteRejected')
      : campus.instanceStatus === 'CANCELLED' ? t('visitRequestV2:detail.decisionNoteCancelled')
        : t('visitRequestV2:detail.decisionNoteApproved');

  const contact = campus.operationalContact;
  const contactSummary = contact?.fullName
    ? [contact.fullName, contact.organization, contact.phone, contact.email].filter(Boolean).join(' · ')
    : null;

  const rows: InfoRow[] = [
    { label: t('visitRequestV2:card.delegationName'), value: campus.delegationName },
    { label: t('visitRequestV2:card.visitType'), value: visitTypeLabel },
    {
      label: t('visitRequestV2:card.workingLanguage'),
      value: campus.workingLanguage === 'VI'
        ? t('visitRequestV2:card.languageVi')
        : t('visitRequestV2:card.languageEn'),
    },
    {
      label: t('visitRequestV2:card.mediaConsent'),
      value: campus.mediaConsentStatus === 'AGREED'
        ? `${t('visitRequestV2:card.mediaAgreed')}${campus.mediaConsentNote ? ` — ${campus.mediaConsentNote}` : ''}`
        : t('visitRequestV2:card.mediaDeclined'),
    },
    { label: t('visitRequestV2:card.transportationNote'), value: campus.transportationNote },
    { label: t('visitRequestV2:card.operationalContact'), value: contactSummary },
    { label: t('visitRequestV2:card.purpose'), value: campus.purpose, multiline: true },
    { label: t('visitRequestV2:card.workingContent'), value: campus.workingContent, multiline: true },
    { label: t('visitRequestV2:card.notes'), value: campus.noteToFptu, multiline: true },
  ];

  return (
    <section
      data-testid={`campus-detail-card-${campus.visitInstanceId}`}
      aria-label={t('visitRequestV2:detail.cardAria', { campus: campus.campusName })}
      className="overflow-hidden rounded-xl border border-[#004c91]/20 bg-white shadow-sm"
    >
      {/* Campus header — the campus, where it stands, and when. */}
      <div className="flex flex-wrap items-center gap-2 bg-[#004c91] px-4 py-2.5">
        <Building2 className="h-4 w-4 shrink-0 text-white" aria-hidden />
        <h3 className="text-sm font-bold uppercase tracking-tight text-white">{campus.campusName}</h3>
        <VisitStatusBadge
          kind="instance"
          status={campus.instanceStatus}
          data-testid={`campus-status-${campus.visitInstanceId}`}
          className="ring-0"
        />
        {campus.activeAmendment && (
          <span className="rounded-full bg-[#f37021] px-2.5 py-0.5 text-xs font-bold text-white" role="status">
            {t('visitRequestV2:detail.amendmentBadge', { no: campus.activeAmendment.amendmentNo })}
          </span>
        )}
        <span className="ml-auto flex items-center gap-1 text-xs font-medium text-white/90">
          <Clock className="h-3.5 w-3.5" aria-hidden />
          <time dateTime={campus.plannedStartAt}>{formatVietnamDateTime(campus.plannedStartAt)}</time>
          {' → '}
          <time dateTime={campus.plannedEndAt}>{formatVietnamDateTime(campus.plannedEndAt)}</time>
        </span>
      </div>

      <div className="space-y-5 p-4 sm:p-5">
        <ReadOnlyInfoGrid rows={rows} />

        <PersonListTable
          data-testid={`campus-visitors-${campus.visitInstanceId}`}
          title={t('visitRequestV2:card.visitors')}
          rows={campus.visitors.map(m => ({
            id: m.guestMemberId,
            fullName: m.fullName,
            jobTitle: m.jobTitle,
            organization: m.organization,
            nationality: m.nationality,
          }))}
          emptyMessage={t('visitRequestV2:person.noVisitors')}
        />

        <PersonListTable
          data-testid={`campus-support-${campus.visitInstanceId}`}
          title={t('visitRequestV2:card.supportTeam')}
          rows={campus.supportMembers.map(m => ({
            id: m.guestMemberId,
            fullName: m.fullName,
            jobTitle: m.jobTitle,
            organization: m.organization,
            nationality: m.nationality,
          }))}
          emptyMessage={t('visitRequestV2:person.noSupport')}
        />

        {/* Decision / host / revision — who decided what, in plain language. The technical
            decision_source is deliberately not surfaced: it is an audit discriminator, not
            something a reader of this screen can act on. */}
        <div className="rounded-xl border border-slate-200 bg-slate-50 p-3">
          <ReadOnlyInfoGrid
            rows={[
              { label: t('visitRequestV2:detail.host'), value: campus.currentHostName },
              { label: t('visitRequestV2:detail.decidedBy'), value: campus.decidedByName },
              {
                label: t('visitRequestV2:detail.decidedAt'),
                value: campus.decidedAt ? formatVietnamDateTime(campus.decidedAt) : null,
              },
              { label: decisionNoteLabel, value: campus.decisionNote },
              {
                label: t('visitRequestV2:revision.title'),
                value: (
                  <>
                    {t(revisionState.headlineKey, revisionState.values)}
                    {revisionState.noteKey && (
                      <span className="mt-0.5 block text-xs font-normal text-slate-500">
                        {t(revisionState.noteKey, revisionState.values)}
                      </span>
                    )}
                  </>
                ),
              },
            ]}
          />
        </div>

        {children}
      </div>
    </section>
  );
};
