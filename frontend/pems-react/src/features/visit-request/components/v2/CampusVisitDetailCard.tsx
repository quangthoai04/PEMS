import React from 'react';
import { Building2, ChevronDown, Clock } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import type { ResolvedCampusVisit } from '../../api/visitRequestV2Api';
import ContactIdentityActions from '../ContactIdentityActions';
import InstanceResubmitPanel from '../InstanceResubmitPanel';
import OperationalContactReadOnly from './OperationalContactReadOnly';
import ReceptionHostReadOnly from './ReceptionHostReadOnly';
import { formatVietnamDateTime } from '../../../../shared/utils/vietnamTime';
import { VisitStatusBadge } from './shared/VisitStatusBadge';
import { ReadOnlyInfoGrid, type InfoRow } from './shared/ReadOnlyInfoGrid';
import { PersonListTable } from './shared/PersonListTable';
import { resolveCampusRevisionState } from './shared/campusRevisionState';

interface Props {
  campus: ResolvedCampusVisit;
  /**
   * Enables the per-campus contact workflow (resend / replace / transfer). Omitted on read-only
   * surfaces — a card with no request id renders the contact as information and nothing else.
   */
  visitRequestId?: number;
  /** Called after a contact mutation so the caller can refetch. */
  onContactChanged?: () => void;
  /**
   * Turns the header into a disclosure control. Used only where there is more than one campus: a
   * single-campus request has nothing to collapse against, and hiding its one card behind a chevron
   * adds a click to reach the only thing on the screen.
   */
  collapsible?: boolean;
  /** Whether the body is shown. Ignored unless `collapsible`. */
  expanded?: boolean;
  onToggle?: () => void;
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
export const CampusVisitDetailCard: React.FC<Props> = ({
  campus, visitRequestId, onContactChanged, collapsible = false, expanded = true, onToggle, children,
}) => {
  const { t } = useTranslation(['visitRequestV2', 'visitRequest']);
  // A non-collapsible card is always open; there is no state in which its body is hidden.
  const bodyShown = !collapsible || expanded;
  const bodyId = `campus-detail-body-${campus.visitInstanceId}`;

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
      // The consent answer alone. It used to carry the media note appended after an em dash, which
      // read as one fact; they are two, and the general note is its own row below.
      value: campus.mediaConsentStatus === 'AGREED'
        ? t('visitRequestV2:card.mediaAgreed')
        : t('visitRequestV2:card.mediaDeclined'),
    },
    { label: t('visitRequestV2:card.transportationNote'), value: campus.transportationNote },
    { label: t('visitRequestV2:card.purpose'), value: campus.purpose, multiline: true },
    { label: t('visitRequestV2:card.workingContent'), value: campus.workingContent, multiline: true },
    { label: t('visitRequestV2:card.notes'), value: campus.notes, multiline: true },
  ];

  return (
    <section
      data-testid={`campus-detail-card-${campus.visitInstanceId}`}
      aria-label={t('visitRequestV2:detail.cardAria', { campus: campus.campusName })}
      className="overflow-hidden rounded-xl border border-[#004c91]/20 bg-white shadow-sm"
    >
      {/* Campus header — the campus, where it stands, and when.
          Everything here stays visible when the card is collapsed, deliberately: which campus, what
          state it is in, whether a change is waiting and when it happens are exactly the facts
          somebody scans a multi-campus request for, and a closed row that showed only a name would
          force them to open all three to find the one they wanted. */}
      {React.createElement(
        collapsible ? 'button' : 'div',
        {
          ...(collapsible
            ? {
                type: 'button' as const,
                onClick: onToggle,
                'aria-expanded': expanded,
                'aria-controls': bodyId,
                'data-testid': `campus-detail-toggle-${campus.visitInstanceId}`,
              }
            : {}),
          className:
            'flex w-full flex-wrap items-center gap-2 bg-[#004c91] px-4 py-2.5 text-left'
            + (collapsible ? ' cursor-pointer hover:bg-[#003a70] focus:outline-none focus-visible:ring-2 focus-visible:ring-inset focus-visible:ring-white' : ''),
        },
        <>
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
          {collapsible && (
            <ChevronDown
              aria-hidden
              className={`h-4 w-4 shrink-0 text-white transition-transform ${expanded ? '' : '-rotate-90'}`}
            />
          )}
        </>,
      )}

      {/* Collapsed campuses are not rendered at all rather than hidden with CSS: each body mounts a
          contact panel that fetches its own state, and three campuses meant three requests for two
          cards nobody was looking at. The deep-link handler in the parent expands the target campus
          BEFORE it scrolls, so nothing ever scrolls to an element that is not there. */}
      {bodyShown && (
      <div id={bodyId} className="space-y-5 p-4 sm:p-5">
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

        {/* ── The three people of this campus, as three separate blocks (plan §12.2) ──
            The guest-side coordinator, the intended host while the gate is shut, and the official
            host once there is one. They can be three different people, so they never share a block
            and the contact is never rendered as one joined string: a screen that merges them puts
            the wrong phone number under the wrong heading, which is how somebody ends up ringing a
            guest to ask about room bookings. */}
        {/* Named so the edit form's "Thay đổi đầu mối" can land on THIS campus's contact block. */}
        <div id={`contact-${campus.visitInstanceId}`} className="scroll-mt-24">
          <OperationalContactReadOnly
            contact={campus.operationalContact}
            visitInstanceId={campus.visitInstanceId}
            showSource
          />
          {/* The resend / replace / transfer workflow belongs to THIS campus's contact, so it lives
              in this card rather than in a request-level panel — which is exactly how one campus's
              contact used to acquire rights over its siblings. Which actions exist is the backend's
              call, passed straight through; the panel renders nothing when none were granted. */}
          {visitRequestId != null && (
            <ContactIdentityActions
              visitRequestId={visitRequestId}
              visitInstanceId={campus.visitInstanceId}
              contactConfirmed={campus.operationalContact.confirmationStatus === 'CONFIRMED'}
              contact={campus.operationalContact}
              rowVersion={campus.rowVersion}
              allowedActions={campus.allowedActions}
              onChanged={onContactChanged}
            />
          )}

          {/* A refused campus can be sent back for review from here, by whoever the backend says may
              do it. Instance-scoped on purpose: the request-wide resubmit would drag an approved
              sibling back into review along with it. */}
          {visitRequestId != null && (
            <InstanceResubmitPanel
              visitRequestId={visitRequestId}
              campusVisit={campus}
              onResubmitted={() => onContactChanged?.()}
            />
          )}
        </div>
        <ReceptionHostReadOnly
          currentHost={campus.currentHost}
          proposedHost={campus.proposedHost}
          visitInstanceId={campus.visitInstanceId}
        />

        {/* Decision / revision — who decided what, in plain language. The technical decision_source
            is deliberately not surfaced: it is an audit discriminator, not something a reader of
            this screen can act on. The host lives in its own block above. */}
        <div className="rounded-xl border border-slate-200 bg-slate-50 p-3">
          <ReadOnlyInfoGrid
            rows={[
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
      )}
    </section>
  );
};
