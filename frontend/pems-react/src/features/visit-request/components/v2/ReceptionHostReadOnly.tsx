import { useTranslation } from 'react-i18next';

import type { ResolvedCurrentHost, ResolvedProposedHost } from '../../api/visitRequestV2Api';

interface Props {
  /** The official host of THIS campus, or null while nobody has been assigned. */
  currentHost: ResolvedCurrentHost | null;
  /** The proposal, if this campus carries one. */
  proposedHost: ResolvedProposedHost | null;
  visitInstanceId: number;
  className?: string;
}

/**
 * The reception host of ONE campus, under whichever of the two headings is truthful right now.
 *
 * <p>
 * The reason this is a single component rather than two: the two states are mutually exclusive and
 * keeping them together is what stops a screen from rendering both. "Host dự kiến" means somebody
 * was put forward and the guest side has not confirmed yet; "Người phụ trách tiếp đón" means the job
 * is actually theirs. Showing a proposal under the second heading tells a reader the visit is
 * staffed when it is not, and tells the person named that they should start preparing.
 * </p>
 *
 * <p>
 * Read-only. It fetches nothing and decides no permission — the caller passes the campus objects the
 * backend already resolved.
 * </p>
 */
export function ReceptionHostReadOnly({
  currentHost,
  proposedHost,
  visitInstanceId,
  className = '',
}: Props) {
  const { t } = useTranslation(['visitRequestV2']);
  const tid = (suffix: string) => `reception-host-${visitInstanceId}-${suffix}`;

  // Assigned wins outright. Once the job is real, the proposal that led to it is history and
  // showing it again beside the assignment reads as two different people.
  if (currentHost) {
    return (
      <section
        className={`rounded-lg border border-slate-200 bg-white p-4 ${className}`}
        data-testid={tid('current-block')}
        data-visit-instance-id={visitInstanceId}
      >
        <h4 className="mb-3 text-sm font-bold text-slate-800">
          {t('visitRequestV2:receptionHost.currentTitle')}
        </h4>
        <dl className="grid grid-cols-1 gap-x-6 gap-y-2 sm:grid-cols-2">
          <Field
            label={t('visitRequestV2:receptionHost.fullName')}
            value={currentHost.fullName}
            testId={tid('current-name')}
          />
          <Field
            label={t('visitRequestV2:receptionHost.department')}
            value={currentHost.departmentName}
            testId={tid('current-department')}
          />
          <Field
            label={t('visitRequestV2:receptionHost.phone')}
            value={currentHost.phone}
            testId={tid('current-phone')}
          />
          <Field
            label={t('visitRequestV2:receptionHost.email')}
            value={currentHost.email}
            testId={tid('current-email')}
          />
        </dl>
      </section>
    );
  }

  if (!proposedHost || proposedHost.selectionMode === 'WAIT_FOR_LATER') return null;

  const needsReselection = proposedHost.proposalStatus === 'NEEDS_RESELECTION';

  return (
    <section
      className={`rounded-lg border p-4 ${
        needsReselection ? 'border-amber-300 bg-amber-50' : 'border-slate-200 bg-white'
      } ${className}`}
      data-testid={tid('proposed-block')}
      data-visit-instance-id={visitInstanceId}
    >
      <h4 className="mb-3 text-sm font-bold text-slate-800">
        {t('visitRequestV2:receptionHost.proposedTitle')}
      </h4>
      <dl className="grid grid-cols-1 gap-x-6 gap-y-2 sm:grid-cols-2">
        <Field
          label={t('visitRequestV2:receptionHost.fullName')}
          value={proposedHost.fullName}
          testId={tid('proposed-name')}
        />
        <Field
          label={t('visitRequestV2:receptionHost.department')}
          value={proposedHost.organizationOrDepartment}
          testId={tid('proposed-department')}
        />
        <Field
          label={t('visitRequestV2:receptionHost.proposalStatus')}
          value={
            needsReselection
              ? t('visitRequestV2:receptionHost.needsReselection')
              : t('visitRequestV2:receptionHost.waitingForContact')
          }
          testId={tid('proposed-status')}
        />
      </dl>
    </section>
  );
}

function Field({
  label,
  value,
  testId,
}: {
  label: string;
  value: string | null | undefined;
  testId: string;
}) {
  return (
    <div className="min-w-0">
      <dt className="text-xs font-medium text-slate-500">{label}</dt>
      <dd className="break-words text-sm text-slate-900" data-testid={testId}>
        {value && value.trim().length > 0 ? value : '—'}
      </dd>
    </div>
  );
}

export default ReceptionHostReadOnly;
