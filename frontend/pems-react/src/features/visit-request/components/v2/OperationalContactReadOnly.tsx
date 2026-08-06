import { useTranslation } from 'react-i18next';

import { formatVietnamDateTime } from '../../../../shared/utils/vietnamTime';
import type { ResolvedOperationalContact } from '../../api/visitRequestV2Api';

interface Props {
  /** The contact of ONE campus. Never a sibling's, never a request-level stand-in. */
  contact: ResolvedOperationalContact;
  /** Scopes every test id so a two-campus screen has two addressable blocks, not one. */
  visitInstanceId: number;
  /** Show the confirmation SOURCE (self-match / email / transfer). Off for guest-facing screens. */
  showSource?: boolean;
  className?: string;
}

/**
 * "Đầu mối đoàn khách phối hợp tại cơ sở" — read-only, per campus.
 *
 * Deliberately dumb: it fetches nothing, reads no role, consults no localStorage and derives no
 * permission. It renders the campus object it is handed. Everything it could have decided for itself
 * is a decision that belongs to the backend, and every past bug in this area came from a component
 * deciding one of them locally.
 *
 * It also renders while the invitation is still outstanding. Hiding the block until `fullName`
 * arrives was the old behaviour and it hid the two facts a reader actually needs then — the address
 * the invitation went to, and that it has not been answered.
 */
export function OperationalContactReadOnly({
  contact,
  visitInstanceId,
  showSource = false,
  className = '',
}: Props) {
  const { t } = useTranslation(['visitRequestV2']);
  const tid = (suffix: string) => `operational-contact-${visitInstanceId}-${suffix}`;

  const hasAnything =
    contact.fullName || contact.organization || contact.jobTitle || contact.phone || contact.email;

  return (
    <section
      className={`rounded-lg border border-slate-200 bg-white p-4 ${className}`}
      data-testid={tid('block')}
      data-visit-instance-id={visitInstanceId}
    >
      <h4 className="mb-3 text-sm font-bold text-slate-800">
        {t('visitRequestV2:operationalContact.title')}
      </h4>

      {!hasAnything ? (
        <p className="text-sm text-slate-500" data-testid={tid('empty')}>
          {t('visitRequestV2:operationalContact.unavailable')}
        </p>
      ) : (
        <dl className="grid grid-cols-1 gap-x-6 gap-y-2 sm:grid-cols-2">
          <Field
            label={t('visitRequestV2:operationalContact.fullName')}
            value={contact.fullName}
            testId={tid('full-name')}
          />
          <Field
            label={t('visitRequestV2:operationalContact.organization')}
            value={contact.organization}
            testId={tid('organization')}
          />
          <Field
            label={t('visitRequestV2:operationalContact.jobTitle')}
            value={contact.jobTitle}
            testId={tid('job-title')}
          />
          <Field
            label={t('visitRequestV2:operationalContact.phone')}
            value={contact.phone}
            testId={tid('phone')}
          />
          <Field
            label={t('visitRequestV2:operationalContact.email')}
            value={contact.email}
            testId={tid('email')}
          />
          <Field
            label={t('visitRequestV2:operationalContact.confirmationStatus')}
            value={confirmationLabel(contact.confirmationStatus, t)}
            testId={tid('confirmation-status')}
          />
          {showSource && contact.confirmationSource ? (
            <Field
              label={t('visitRequestV2:operationalContact.confirmationSource')}
              value={sourceLabel(contact.confirmationSource, t)}
              testId={tid('confirmation-source')}
            />
          ) : null}
          {contact.confirmedAt ? (
            <Field
              label={t('visitRequestV2:operationalContact.confirmedAt')}
              value={formatVietnamDateTime(contact.confirmedAt)}
              testId={tid('confirmed-at')}
            />
          ) : null}
        </dl>
      )}
    </section>
  );
}

/** Each field is its own label/value pair — never one joined string, which is unreadable and unsearchable. */
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

/** Never render a raw enum: the reader is a person, and PENDING is not Vietnamese. */
function confirmationLabel(status: string, t: (k: string) => string): string {
  switch (status) {
    case 'CONFIRMED':
      return t('visitRequestV2:operationalContact.status.confirmed');
    case 'DECLINED':
      return t('visitRequestV2:operationalContact.status.declined');
    case 'EXPIRED':
      return t('visitRequestV2:operationalContact.status.expired');
    case 'TRANSFER_PENDING':
      return t('visitRequestV2:operationalContact.status.transferPending');
    default:
      return t('visitRequestV2:operationalContact.status.pending');
  }
}

function sourceLabel(source: string, t: (k: string) => string): string {
  switch (source) {
    case 'REGISTRANT_SELF_MATCH':
      return t('visitRequestV2:operationalContact.source.registrantSelfMatch');
    case 'TRANSFER':
      return t('visitRequestV2:operationalContact.source.transfer');
    default:
      return t('visitRequestV2:operationalContact.source.emailConfirmation');
  }
}

export default OperationalContactReadOnly;
